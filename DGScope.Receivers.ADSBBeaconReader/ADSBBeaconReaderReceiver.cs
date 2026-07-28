using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace DGScope.Receivers
{
    [DefaultReceiver]
    public class ADSBBeaconReaderReceiver : Receiver
    {
        [DisplayName("Poll Interval (s)"), Description("Seconds between polls of each ADS-B source (minimum 3).")]
        public int PollIntervalSeconds { get; set; } = 5;

        [DisplayName("Hide LADD Callsigns"), Description("Suppress callsigns for aircraft flagged LADD (Limiting Aircraft Data Displayed). The configured sources publish these unfiltered; turn this on to withhold them anyway.")]
        public bool HideLADDCallsigns { get; set; } = false;

        [DisplayName("Correlate By Position"), Description("Match ADS-B targets to radar tracks by position and altitude when the Mode S code and beacon code don't identify them. Required for most SWIM feeds, which don't publish Mode S codes.")]
        public bool CorrelateByPosition { get; set; } = true;

        [DisplayName("Write Log File"), Description("Write matching diagnostics to %LocalAppData%\\DGScope Profile Manager\\adsb_beacon_reader.log.")]
        public bool WriteLogFile { get; set; } = true;

        // An array, not a List: XmlSerializer replaces arrays wholesale but *appends* to
        // a list it got from the getter, which would duplicate the constructor-populated
        // built-ins every time the settings file was loaded.
        [DisplayName("Sources"), Description("ADS-B data sources to query for callsign enrichment.")]
        public ADSBSource[] Sources { get; set; }

        private const double PositionMatchThresholdNM = 1.5;
        private const double PositionMatchWithSquawkThresholdNM = 5.0;
        private const double RevalidateThresholdNM = 5.0;
        private const int AltitudeMatchThresholdFt = 500;
        private const int AltitudeMatchWithSquawkThresholdFt = 1000;
        private const int RevalidateIntervalPolls = 3;

        private Timer pollTimer;
        private volatile bool running;
        private int pollCount;
        private bool warnedNoAircraftList;
        private bool warnedNoLocation;

        /// <summary>
        /// Position-correlated assignments, kept so they can be re-validated and
        /// withdrawn if the ADS-B target drifts away from the radar track.
        /// </summary>
        private readonly Dictionary<Aircraft, PositionMatch> positionMatches =
            new Dictionary<Aircraft, PositionMatch>();

        private class PositionMatch
        {
            public string Callsign;
            public string AdsbHex;
        }

        public ADSBBeaconReaderReceiver()
        {
            // This receiver only annotates existing tracks with callsigns; it never
            // creates new targets, so tracks it can't match are simply left alone.
            CreateNewAircraft = false;
            Name = "ADS-B Beacon Reader";
            Sources = DefaultSources();
        }

        private static ADSBSource[] DefaultSources()
        {
            return new[]
            {
                new ADSBSource { Name = "adsb.lol", BaseUrl = "https://api.adsb.lol/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "adsb.fi", BaseUrl = "https://opendata.adsb.fi/api/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "airplanes.live", BaseUrl = "https://api.airplanes.live/v2", Enabled = true, IsBuiltIn = true }
            };
        }

        #region Non-discrete beacon codes

        /// <summary>
        /// Non-discrete squawk codes per FAA JO 7110.66H (NBCAP) and JO 7110.65.
        /// These are shared by multiple aircraft and unreliable for correlation.
        /// </summary>
        private static readonly HashSet<string> NonDiscreteSquawks = BuildNonDiscreteSet();

        private static HashSet<string> BuildNonDiscreteSet()
        {
            var set = new HashSet<string>();
            // 64 non-discrete codes: all codes ending in 00 (octal).
            for (int d1 = 0; d1 <= 7; d1++)
                for (int d2 = 0; d2 <= 7; d2++)
                    set.Add($"{d1}{d2}00");

            // Conspicuity / special-use codes (FAA JO 7110.65 Ch5 S2)
            set.Add("1200"); // VFR
            set.Add("1202"); // SAR (USAF/USCG)
            set.Add("1203"); // VFR formation lead
            set.Add("1255"); // Firefighting
            set.Add("1277"); // SAR/glider
            set.Add("2000"); // IFR without discrete assignment
            set.Add("4000"); // Military VFR / special ops
            set.Add("7400"); // UAS lost link
            set.Add("7500"); // Hijack
            set.Add("7600"); // Radio failure
            set.Add("7700"); // Emergency
            set.Add("7777"); // Military intercept

            // DC SFRA / FRZ codes (PCT allocation)
            set.Add("1226"); // VFR direct to/from Leesburg JYO within SFRA
            set.Add("5100"); // PCT SFRA allocation
            set.Add("5200"); // PCT FRZ allocation
            return set;
        }

        private static bool IsDiscreteSquawk(string squawk)
        {
            return !string.IsNullOrEmpty(squawk) && !NonDiscreteSquawks.Contains(squawk);
        }

        #endregion

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DGScope Profile Manager", "adsb_beacon_reader.log");

        /// <summary>
        /// A receiver left at 0/0 would query the Gulf of Guinea and match nothing.
        /// </summary>
        private bool HasUsableLocation =>
            Location != null && (Location.Latitude != 0 || Location.Longitude != 0);

        private void Log(string msg)
        {
            if (!WriteLogFile)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {msg}\r\n");
            }
            catch { }
        }

        public override void Start()
        {
            if (running) return;
            NormalizeSources();

            // Deliberately no exceptions here: Receiver.SetAircraftList calls Start()
            // outside the scope's try/catch, so throwing would take the whole app down
            // on a misconfigured receiver. Misconfiguration is reported in the log and
            // the poll simply does nothing until it's fixed.
            if (!HasUsableLocation)
                Log("Start: no Location set - set the receiver's Location (or the scope's " +
                    "home position) so the ADS-B sources can be queried around it.");
            if (!Sources.Any(s => s.Enabled))
                Log("Start: every ADS-B source is disabled; no callsigns will be read.");

            running = true;
            pollCount = 0;
            var where = HasUsableLocation
                ? $"{Location.Latitude:F4}/{Location.Longitude:F4} r{(int)Range}nm"
                : "no location";
            Log($"Start - {where}, {Sources.Count(s => s.Enabled)} sources, " +
                $"HideLADD={HideLADDCallsigns}, CorrelateByPosition={CorrelateByPosition}");
            var interval = Math.Max(3, PollIntervalSeconds) * 1000;
            pollTimer = new Timer(PollCallback, null, 0, interval);
        }

        public override void Stop()
        {
            running = false;
            pollTimer?.Dispose();
            pollTimer = null;
            lock (positionMatches)
                positionMatches.Clear();
        }

        /// <summary>
        /// Collapse duplicate sources by URL (keeping the last, i.e. the saved
        /// enabled/disabled state) and make sure the built-ins are present, so an older
        /// settings file or a hand-edited list still ends up with a usable set.
        /// </summary>
        private void NormalizeSources()
        {
            if (Sources == null)
                Sources = new ADSBSource[0];

            var deduped = new List<ADSBSource>();
            foreach (var source in Sources)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.BaseUrl))
                    continue;
                deduped.RemoveAll(s => string.Equals(s.BaseUrl, source.BaseUrl, StringComparison.OrdinalIgnoreCase));
                deduped.Add(source);
            }

            foreach (var builtIn in DefaultSources())
            {
                if (!deduped.Any(s => string.Equals(s.BaseUrl, builtIn.BaseUrl, StringComparison.OrdinalIgnoreCase)))
                    deduped.Add(builtIn);
            }

            Sources = deduped.ToArray();
        }

        private void PollCallback(object state)
        {
            if (!running) return;

            try
            {
                if (aircraft == null)
                {
                    // Only possible if the scope never handed over its track list. Log it
                    // once: silence here is indistinguishable from "no traffic matched".
                    if (!warnedNoAircraftList)
                    {
                        warnedNoAircraftList = true;
                        Log("Poll skipped: no track list attached yet.");
                    }
                    return;
                }
                if (!HasUsableLocation)
                {
                    if (!warnedNoLocation)
                    {
                        warnedNoLocation = true;
                        Log("Poll skipped: Location is 0,0 - set it on the receiver.");
                    }
                    return;
                }

                var enabledSources = Sources.Where(s => s.Enabled).ToList();
                if (pollCount == 0)
                    Log($"Polling {enabledSources.Count} source(s)...");
                var allResults = new List<ADSBv2Aircraft>();

                for (int i = 0; i < enabledSources.Count; i++)
                {
                    if (!running) return;
                    try
                    {
                        var results = QuerySource(enabledSources[i]);
                        if (results != null)
                            allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error querying {enabledSources[i].Name}: {ex.Message}");
                        Debug.WriteLine($"ADSB Beacon Reader: Error querying {enabledSources[i].Name}: {ex.Message}");
                    }

                    // Rate limit: 1 request per second per API.
                    if (i < enabledSources.Count - 1)
                        Thread.Sleep(1100);
                }

                Log($"Poll - {allResults.Count} ADS-B targets from {enabledSources.Count} sources");

                if (allResults.Count > 0)
                    MatchAndEnrich(allResults);

                // Re-validate position matches at a slower rate.
                pollCount++;
                if (pollCount >= RevalidateIntervalPolls)
                {
                    pollCount = 0;
                    if (allResults.Count > 0)
                        RevalidatePositionMatches(allResults);
                }
            }
            catch (Exception ex)
            {
                Log($"Poll error: {ex}");
                Debug.WriteLine($"ADSB Beacon Reader: Poll error: {ex.Message}");
            }
        }

        /// <summary>
        /// WebClient waits 100 seconds by default. With three sources polled in sequence
        /// that is five minutes of silence before a single unreachable host reports
        /// anything, which reads exactly like the receiver doing nothing at all.
        /// </summary>
        private class TimeoutWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null)
                    request.Timeout = 15000;
                return request;
            }
        }

        private List<ADSBv2Aircraft> QuerySource(ADSBSource source)
        {
            var url = $"{source.BaseUrl.TrimEnd('/')}/lat/{Location.Latitude:F6}/lon/{Location.Longitude:F6}/dist/{(int)Range}";

            using (var client = new TimeoutWebClient())
            {
                client.Headers.Add("Accept", "application/json");
                client.Headers.Add("User-Agent", "DGScope-BeaconReader");
                var json = client.DownloadString(url);
                var response = JsonConvert.DeserializeObject<ADSBv2Response>(json);
                return response?.Aircraft;
            }
        }

        private static int? ParseAltitude(object altBaro)
        {
            if (altBaro == null) return null;
            if (altBaro is long l) return (int)l;
            if (altBaro is int i) return i;
            if (altBaro is double d) return (int)d;
            if (int.TryParse(altBaro.ToString(), out int parsed)) return parsed;
            return null;
        }

        private void MatchAndEnrich(List<ADSBv2Aircraft> results)
        {
            // Deduplicate by hex (they should all report the same callsign anyway).
            var byHex = new Dictionary<string, ADSBv2Aircraft>(StringComparer.OrdinalIgnoreCase);
            foreach (var ac in results)
            {
                if (!string.IsNullOrEmpty(ac.Hex))
                    byHex[ac.Hex] = ac;
            }

            // Snapshot the track list and build the lookup indices outside the lock.
            List<Aircraft> snapshot;
            lock (aircraft)
                snapshot = aircraft.Where(x => !x.Deleted).ToList();

            var byModeS = new Dictionary<int, Aircraft>();
            var bySquawk = new Dictionary<string, Aircraft>(StringComparer.OrdinalIgnoreCase);
            var duplicateSquawks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatched = new List<Aircraft>();

            foreach (var ac in snapshot)
            {
                if (ac.ModeSCode != 0 && !byModeS.ContainsKey(ac.ModeSCode))
                    byModeS[ac.ModeSCode] = ac;

                if (!string.IsNullOrEmpty(ac.Squawk))
                {
                    if (duplicateSquawks.Contains(ac.Squawk))
                    {
                        // Already a known duplicate.
                    }
                    else if (bySquawk.ContainsKey(ac.Squawk))
                    {
                        bySquawk.Remove(ac.Squawk);
                        duplicateSquawks.Add(ac.Squawk);
                    }
                    else
                    {
                        bySquawk[ac.Squawk] = ac;
                    }
                }

                if (IsPositionCandidate(ac) && ac.Location != null && (ac.Latitude != 0 || ac.Longitude != 0))
                    unmatched.Add(ac);
            }

            Log($"Radar: {snapshot.Count} tracks, {unmatched.Count} need a callsign, " +
                $"{byModeS.Count} with Mode S, {bySquawk.Count} unique beacon codes");

            var updates = new List<KeyValuePair<Aircraft, string>>();
            var newPositionMatches = new List<KeyValuePair<Aircraft, PositionMatch>>();
            int laddSkipped = 0;

            foreach (var adsbAc in byHex.Values)
            {
                var callsign = adsbAc.Flight?.Trim();
                if (string.IsNullOrEmpty(callsign))
                    continue;

                if (HideLADDCallsigns && adsbAc.IsLADD)
                {
                    laddSkipped++;
                    continue;
                }

                Aircraft matched = null;
                bool matchedByPosition = false;
                string matchMethod = null;

                // Primary match: Mode S hex code.
                if (!string.IsNullOrEmpty(adsbAc.Hex))
                {
                    try
                    {
                        int modeS = Convert.ToInt32(adsbAc.Hex, 16);
                        if (modeS != 0 && byModeS.TryGetValue(modeS, out matched))
                            matchMethod = "hex";
                    }
                    catch { }
                }

                // Secondary match: a discrete beacon code that's unique on the scope.
                // Non-discrete codes (1200, 7700, xx00 …) are shared and never correlate.
                if (matched == null && IsDiscreteSquawk(adsbAc.Squawk)
                    && bySquawk.TryGetValue(adsbAc.Squawk, out matched))
                    matchMethod = "squawk";

                // Tertiary match: discrete beacon code plus a relaxed position/altitude
                // gate, to disambiguate the duplicates the secondary match had to drop.
                if (matched == null && CorrelateByPosition && IsDiscreteSquawk(adsbAc.Squawk)
                    && duplicateSquawks.Contains(adsbAc.Squawk))
                {
                    double dist;
                    matched = ClosestTrack(adsbAc, unmatched, PositionMatchWithSquawkThresholdNM,
                        AltitudeMatchWithSquawkThresholdFt, adsbAc.Squawk, out dist);
                    matchedByPosition = matched != null;
                    if (matched != null)
                        matchMethod = $"squawk+position({dist:F2}nm)";
                }

                // Quaternary match: position and altitude alone, on a tight gate. This is
                // what correlates SWIM tracks, which carry no Mode S code at all.
                if (matched == null && CorrelateByPosition)
                {
                    double dist;
                    matched = ClosestTrack(adsbAc, unmatched, PositionMatchThresholdNM,
                        AltitudeMatchThresholdFt, null, out dist);
                    matchedByPosition = matched != null;
                    if (matched != null)
                        matchMethod = $"position({dist:F2}nm)";
                }

                if (matched == null)
                    continue;

                if (!NeedsCallsign(matched) && matched.ADSBCallsign != callsign)
                {
                    Log($"  ADSB {adsbAc.Hex}/{adsbAc.Squawk}/{callsign} -> {matchMethod}, " +
                        $"skipped (track already has CS=\"{matched.Callsign}\")");
                    continue;
                }

                Log($"  ADSB {adsbAc.Hex}/{adsbAc.Squawk}/{callsign} -> {matchMethod}, " +
                    $"track CS=\"{matched.Callsign}\" SQ=\"{matched.Squawk}\" FPC=\"{matched.FlightPlanCallsign}\"");

                updates.Add(new KeyValuePair<Aircraft, string>(matched, callsign));
                unmatched.Remove(matched);

                if (matchedByPosition && !string.IsNullOrEmpty(adsbAc.Hex))
                {
                    newPositionMatches.Add(new KeyValuePair<Aircraft, PositionMatch>(
                        matched, new PositionMatch { Callsign = callsign, AdsbHex = adsbAc.Hex }));
                }
            }

            // Write to ADSBCallsign, which SWIM never overwrites, so the render loop can
            // keep re-applying it even when SWIM resets Callsign back to the beacon code.
            foreach (var update in updates)
                update.Key.ADSBCallsign = update.Value;

            Log($"Applied {updates.Count} callsigns ({laddSkipped} LADD targets suppressed)");

            if (newPositionMatches.Count > 0)
            {
                lock (positionMatches)
                {
                    foreach (var pm in newPositionMatches)
                        positionMatches[pm.Key] = pm.Value;
                }
            }
        }

        /// <summary>
        /// True when the track has no callsign of its own, or is showing its beacon code
        /// as one, or is already carrying a callsign this receiver supplied.
        /// </summary>
        private static bool NeedsCallsign(Aircraft ac)
        {
            return !string.IsNullOrEmpty(ac.ADSBCallsign) || IsPositionCandidate(ac);
        }

        /// <summary>
        /// True when the track is eligible for position correlation: it has no callsign
        /// of its own and none this receiver already supplied. Tracks we've already
        /// enriched are excluded so a second ADS-B target can't steal them.
        /// </summary>
        private static bool IsPositionCandidate(Aircraft ac)
        {
            if (!string.IsNullOrEmpty(ac.ADSBCallsign))
                return false;
            if (string.IsNullOrEmpty(ac.Callsign))
                return true;
            return !string.IsNullOrEmpty(ac.Squawk) && ac.Callsign == ac.Squawk;
        }

        /// <summary>
        /// Nearest candidate track to an ADS-B target within the given distance and
        /// altitude gates, or null if none qualifies.
        /// </summary>
        private static Aircraft ClosestTrack(ADSBv2Aircraft adsbAc, List<Aircraft> candidates,
            double maxDistanceNM, int maxAltitudeDiffFt, string requiredSquawk, out double distance)
        {
            distance = 0;
            if (!adsbAc.Latitude.HasValue || !adsbAc.Longitude.HasValue)
                return null;

            var adsbPos = new GeoPoint(adsbAc.Latitude.Value, adsbAc.Longitude.Value);
            int? adsbAlt = ParseAltitude(adsbAc.AltitudeBaro);
            Aircraft closest = null;
            double closestDist = maxDistanceNM;

            foreach (var ac in candidates)
            {
                if (requiredSquawk != null && ac.Squawk != requiredSquawk)
                    continue;
                if (adsbAlt.HasValue && ac.PressureAltitude != 0
                    && Math.Abs(adsbAlt.Value - ac.PressureAltitude) > maxAltitudeDiffFt)
                    continue;

                var dist = adsbPos.DistanceTo(ac.Location);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = ac;
                }
            }

            distance = closestDist;
            return closest;
        }

        /// <summary>
        /// Retract a callsign this receiver supplied. The render loop copies ADSBCallsign
        /// into Callsign/FlightPlanCallsign every frame, so those have to be cleared too —
        /// but only if they still hold the value we put there.
        /// </summary>
        private static void Withdraw(Aircraft ac, string callsign)
        {
            ac.ADSBCallsign = null;
            if (ac.Callsign == callsign)
                ac.Callsign = null;
            if (ac.FlightPlanCallsign == callsign)
                ac.FlightPlanCallsign = null;
        }

        private void RevalidatePositionMatches(List<ADSBv2Aircraft> latestResults)
        {
            var adsbByHex = new Dictionary<string, ADSBv2Aircraft>(StringComparer.OrdinalIgnoreCase);
            foreach (var ac in latestResults)
            {
                if (!string.IsNullOrEmpty(ac.Hex))
                    adsbByHex[ac.Hex] = ac;
            }

            List<Aircraft> toRemove = null;

            lock (positionMatches)
            {
                foreach (var kvp in positionMatches)
                {
                    var radarAc = kvp.Key;
                    var pm = kvp.Value;

                    // Someone else cleared or replaced the callsign we assigned.
                    if (radarAc.ADSBCallsign != pm.Callsign)
                    {
                        if (toRemove == null) toRemove = new List<Aircraft>();
                        toRemove.Add(radarAc);
                        continue;
                    }

                    // The ADS-B target we matched has left the coverage area.
                    if (!adsbByHex.TryGetValue(pm.AdsbHex, out var adsbAc))
                    {
                        Log($"  Revalidate: {pm.Callsign} ADS-B target gone, withdrawing");
                        Withdraw(radarAc, pm.Callsign);
                        if (toRemove == null) toRemove = new List<Aircraft>();
                        toRemove.Add(radarAc);
                        continue;
                    }

                    // The ADS-B target has drifted away from the radar track.
                    if (adsbAc.Latitude.HasValue && adsbAc.Longitude.HasValue
                        && radarAc.Location != null && (radarAc.Latitude != 0 || radarAc.Longitude != 0))
                    {
                        var adsbPos = new GeoPoint(adsbAc.Latitude.Value, adsbAc.Longitude.Value);
                        var dist = adsbPos.DistanceTo(radarAc.Location);
                        if (dist > RevalidateThresholdNM)
                        {
                            Log($"  Revalidate: {pm.Callsign} drifted {dist:F1}nm, withdrawing");
                            Withdraw(radarAc, pm.Callsign);
                            if (toRemove == null) toRemove = new List<Aircraft>();
                            toRemove.Add(radarAc);
                        }
                    }
                }

                if (toRemove != null)
                {
                    foreach (var ac in toRemove)
                        positionMatches.Remove(ac);
                }
            }
        }
    }
}
