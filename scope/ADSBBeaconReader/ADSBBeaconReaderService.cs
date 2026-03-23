using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace DGScope.ADSBBeaconReader
{
    public class ADSBBeaconReaderService
    {
        private readonly ObservableCollection<Aircraft> aircraft;
        private readonly Func<GeoPoint> getLocation;
        private readonly Func<int> getRange;
        private readonly ADSBBeaconReaderSettings settings;
        private Timer pollTimer;
        private bool running;
        private const double PositionMatchThresholdNM = 1.5;
        private const double RevalidateThresholdNM = 3.0;
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DGScope Profile Manager", "adsb_beacon_reader.log");

        private static void Log(string msg)
        {
            try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {msg}\r\n"); } catch { }
        }
        private const int AltitudeMatchThresholdFt = 500;
        private const int RevalidateIntervalPolls = 3;

        // Track position-correlated assignments for re-validation
        private readonly Dictionary<Aircraft, PositionMatch> positionMatches =
            new Dictionary<Aircraft, PositionMatch>();
        private int pollCount;

        // SWIM-proof callsign cache: stores ADSB-discovered callsigns keyed by Aircraft reference.
        // SWIM continuously overwrites Aircraft.Callsign and FlightPlanCallsign for LADD targets,
        // so we need a separate store that the render loop can use to re-apply every frame.
        private readonly Dictionary<Aircraft, string> adsbCallsignCache =
            new Dictionary<Aircraft, string>();

        /// <summary>
        /// Try to get a cached ADSB callsign for an aircraft. Thread-safe.
        /// Returns null if no cached callsign exists.
        /// </summary>
        public string GetCachedCallsign(Aircraft ac)
        {
            lock (adsbCallsignCache)
            {
                return adsbCallsignCache.TryGetValue(ac, out var cs) ? cs : null;
            }
        }

        private class PositionMatch
        {
            public string Callsign;
            public string AdsbHex;
        }

        public ADSBBeaconReaderService(
            ObservableCollection<Aircraft> aircraft,
            Func<GeoPoint> getLocation,
            Func<int> getRange,
            ADSBBeaconReaderSettings settings)
        {
            this.aircraft = aircraft;
            this.getLocation = getLocation;
            this.getRange = getRange;
            this.settings = settings;
        }

        public void Start()
        {
            if (running) return;
            running = true;
            pollCount = 0;
            Log($"Service starting. Sources: {settings.Sources.Count} ({settings.Sources.Count(s => s.Enabled)} enabled). HideLADD: {settings.HideLADDCallsigns}");
            var interval = Math.Max(3, settings.PollIntervalSeconds) * 1000;
            pollTimer = new Timer(PollCallback, null, 0, interval);
        }

        public void Stop()
        {
            running = false;
            pollTimer?.Dispose();
            pollTimer = null;
            lock (positionMatches)
                positionMatches.Clear();
            lock (adsbCallsignCache)
                adsbCallsignCache.Clear();
        }

        private void PollCallback(object state)
        {
            if (!running) return;

            try
            {
                var location = getLocation();
                var range = getRange();
                Log($"Poll - Location: {location?.Latitude:F4},{location?.Longitude:F4} Range: {range}");
                if (location == null || (location.Latitude == 0 && location.Longitude == 0))
                {
                    Log("Skipping - no valid location");
                    return;
                }

                var enabledSources = settings.Sources.Where(s => s.Enabled).ToList();
                Log($"{enabledSources.Count} enabled sources");
                var allResults = new List<ADSBv2Aircraft>();

                foreach (var source in enabledSources)
                {
                    if (!running) return;
                    try
                    {
                        var results = QuerySource(source, location, range);
                        if (results != null)
                            allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error querying {source.Name}: {ex.Message}");
                    }

                    // Rate limit: 1 request per second per API
                    if (enabledSources.IndexOf(source) < enabledSources.Count - 1)
                        Thread.Sleep(1100);
                }

                Log($"Got {allResults.Count} ADSB aircraft total");
                if (allResults.Count > 0)
                    MatchAndEnrich(allResults);

                // Re-validate position matches at a slower rate
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
                Log($"Poll error: {ex.Message}");
            }
        }

        private List<ADSBv2Aircraft> QuerySource(ADSBSource source, GeoPoint location, int range)
        {
            var url = $"{source.BaseUrl.TrimEnd('/')}/lat/{location.Latitude:F6}/lon/{location.Longitude:F6}/dist/{range}";

            using (var client = new WebClient())
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

        private void RevalidatePositionMatches(List<ADSBv2Aircraft> latestResults)
        {
            // Build hex lookup from latest ADSB data
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

                    // If the cache no longer holds our callsign, someone else cleared it
                    string cached;
                    lock (adsbCallsignCache)
                        adsbCallsignCache.TryGetValue(radarAc, out cached);
                    if (cached != pm.Callsign)
                    {
                        if (toRemove == null) toRemove = new List<Aircraft>();
                        toRemove.Add(radarAc);
                        continue;
                    }

                    // Find the ADSB target we matched against
                    if (!adsbByHex.TryGetValue(pm.AdsbHex, out var adsbAc))
                    {
                        // ADSB target no longer in range — remove from cache
                        if (toRemove == null) toRemove = new List<Aircraft>();
                        toRemove.Add(radarAc);
                        continue;
                    }

                    // Check if the ADSB target has drifted away from the radar track
                    if (adsbAc.Latitude.HasValue && adsbAc.Longitude.HasValue
                        && radarAc.Location != null && (radarAc.Latitude != 0 || radarAc.Longitude != 0))
                    {
                        var adsbPos = new GeoPoint(adsbAc.Latitude.Value, adsbAc.Longitude.Value);
                        var dist = adsbPos.DistanceTo(radarAc.Location);
                        if (dist > RevalidateThresholdNM)
                        {
                            // Positions have diverged — mismatch, remove from cache
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

            // Also remove from callsign cache for position-match removals
            if (toRemove != null)
            {
                lock (adsbCallsignCache)
                {
                    foreach (var ac in toRemove)
                        adsbCallsignCache.Remove(ac);
                }
            }
        }

        private void MatchAndEnrich(List<ADSBv2Aircraft> results)
        {
            // Deduplicate ADSB results by hex
            var byHex = new Dictionary<string, ADSBv2Aircraft>(StringComparer.OrdinalIgnoreCase);
            foreach (var ac in results)
            {
                if (!string.IsNullOrEmpty(ac.Hex))
                    byHex[ac.Hex] = ac;
            }

            // Snapshot the aircraft list and build lookup indices outside the lock
            List<Aircraft> snapshot;
            lock (aircraft)
            {
                snapshot = aircraft.ToList();
            }

            // Build O(1) lookup by ModeSCode
            var byModeS = new Dictionary<int, Aircraft>();
            // Build squawk lookup (only store if unique)
            var bySquawk = new Dictionary<string, Aircraft>(StringComparer.OrdinalIgnoreCase);
            var duplicateSquawks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Collect unmatched aircraft with positions for position correlation
            var unmatched = new List<Aircraft>();

            foreach (var ac in snapshot)
            {
                if (ac.ModeSCode != 0 && !byModeS.ContainsKey(ac.ModeSCode))
                    byModeS[ac.ModeSCode] = ac;

                if (!string.IsNullOrEmpty(ac.Squawk))
                {
                    if (duplicateSquawks.Contains(ac.Squawk))
                    {
                        // Already known duplicate
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

                if (string.IsNullOrEmpty(ac.Callsign) && ac.Location != null
                    && (ac.Latitude != 0 || ac.Longitude != 0))
                {
                    unmatched.Add(ac);
                }
            }

            // Match and collect updates
            var updates = new List<KeyValuePair<Aircraft, string>>();
            var newPositionMatches = new List<KeyValuePair<Aircraft, PositionMatch>>();

            foreach (var adsbAc in byHex.Values)
            {
                var callsign = adsbAc.Flight?.Trim();
                if (string.IsNullOrEmpty(callsign))
                    continue;

                // LADD filtering
                bool isLADD = adsbAc.IsLADD;
                if (settings.HideLADDCallsigns && isLADD)
                    continue;

                Aircraft matched = null;
                bool matchedByPosition = false;

                // Primary match: by Mode S hex code (O(1) dictionary lookup)
                if (!string.IsNullOrEmpty(adsbAc.Hex))
                {
                    try
                    {
                        int modeS = Convert.ToInt32(adsbAc.Hex, 16);
                        if (modeS != 0)
                            byModeS.TryGetValue(modeS, out matched);
                    }
                    catch { }
                }

                // Secondary match: by squawk (O(1) dictionary lookup, already filtered to unique)
                if (matched == null && !string.IsNullOrEmpty(adsbAc.Squawk))
                {
                    bySquawk.TryGetValue(adsbAc.Squawk, out matched);
                }

                // Tertiary match: by approximate position and altitude
                if (matched == null && adsbAc.Latitude.HasValue && adsbAc.Longitude.HasValue)
                {
                    var adsbPos = new GeoPoint(adsbAc.Latitude.Value, adsbAc.Longitude.Value);
                    int? adsbAlt = ParseAltitude(adsbAc.AltitudeBaro);
                    Aircraft closest = null;
                    double closestDist = PositionMatchThresholdNM;
                    foreach (var ac in unmatched)
                    {
                        if (adsbAlt.HasValue && ac.PressureAltitude != 0)
                        {
                            if (Math.Abs(adsbAlt.Value - ac.PressureAltitude) > AltitudeMatchThresholdFt)
                                continue;
                        }
                        var dist = adsbPos.DistanceTo(ac.Location);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closest = ac;
                        }
                    }
                    matched = closest;
                    matchedByPosition = matched != null;
                }

                // Enrich if:
                // 1. Callsign is empty (normal uncorrelated target), OR
                // 2. LADD not respected AND this is a LADD aircraft where SWIM
                //    substituted the squawk as the callsign/FlightPlanCallsign
                bool isSwimLADD = !settings.HideLADDCallsigns && isLADD
                    && matched != null && !string.IsNullOrEmpty(matched.Squawk)
                    && (matched.Callsign == matched.Squawk
                        || matched.FlightPlanCallsign == matched.Squawk);

                bool needsEnrichment = (matched != null)
                    && (string.IsNullOrEmpty(matched.Callsign) || isSwimLADD);

                if (matched != null && needsEnrichment)
                {
                    updates.Add(new KeyValuePair<Aircraft, string>(matched, callsign));
                    unmatched.Remove(matched);

                    // Track position-correlated matches for re-validation
                    if (matchedByPosition && !string.IsNullOrEmpty(adsbAc.Hex))
                    {
                        newPositionMatches.Add(new KeyValuePair<Aircraft, PositionMatch>(
                            matched, new PositionMatch { Callsign = callsign, AdsbHex = adsbAc.Hex }));
                    }

                }
            }

            // Apply updates and cache callsigns in SWIM-proof store
            Log($"Matched {updates.Count} of {byHex.Count} ADSB aircraft (radar targets: {snapshot.Count}, unmatched: {unmatched.Count})");
            lock (adsbCallsignCache)
            {
                foreach (var update in updates)
                {
                    var ac = update.Key;
                    var cs = update.Value;
                    Log($"  {ac.Squawk}/{ac.ModeSCode:X6} -> {cs}");
                    ac.Callsign = cs;
                    ac.FlightPlanCallsign = cs;
                    // Store in SWIM-proof cache so render loop can re-apply every frame
                    adsbCallsignCache[ac] = cs;
                }
            }

            // Record position matches for future re-validation
            if (newPositionMatches.Count > 0)
            {
                lock (positionMatches)
                {
                    foreach (var pm in newPositionMatches)
                        positionMatches[pm.Key] = pm.Value;
                }
            }
        }
    }
}
