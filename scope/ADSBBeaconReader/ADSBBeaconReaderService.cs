using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        private readonly object lockObj = new object();
        private const double PositionMatchThresholdNM = 1.5;
        private const int AltitudeMatchThresholdFt = 500;

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
            var interval = Math.Max(3, settings.PollIntervalSeconds) * 1000;
            pollTimer = new Timer(PollCallback, null, 0, interval);
        }

        public void Stop()
        {
            running = false;
            pollTimer?.Dispose();
            pollTimer = null;
        }

        private void PollCallback(object state)
        {
            if (!running) return;

            try
            {
                var location = getLocation();
                var range = getRange();
                if (location == null || (location.Latitude == 0 && location.Longitude == 0))
                    return;

                var enabledSources = settings.Sources.Where(s => s.Enabled).ToList();
                var allResults = new List<ADSBv2Aircraft>();

                foreach (var source in enabledSources)
                {
                    try
                    {
                        var results = QuerySource(source, location, range);
                        if (results != null)
                            allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ADSB Beacon Reader: Error querying {source.Name}: {ex.Message}");
                    }

                    // Rate limit: 1 request per second per API
                    if (enabledSources.IndexOf(source) < enabledSources.Count - 1)
                        Thread.Sleep(1100);
                }

                if (allResults.Count > 0)
                    MatchAndEnrich(allResults);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ADSB Beacon Reader: Poll error: {ex.Message}");
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
            return null; // "ground" or other non-numeric
        }

        private void MatchAndEnrich(List<ADSBv2Aircraft> results)
        {
            // Deduplicate by hex (last result wins, but they should all have the same callsign)
            var byHex = new Dictionary<string, ADSBv2Aircraft>(StringComparer.OrdinalIgnoreCase);
            foreach (var ac in results)
            {
                if (!string.IsNullOrEmpty(ac.Hex))
                    byHex[ac.Hex] = ac;
            }

            lock (aircraft)
            {
                foreach (var adsbAc in byHex.Values)
                {
                    var callsign = adsbAc.Flight?.Trim();
                    if (string.IsNullOrEmpty(callsign))
                        continue;

                    // LADD filtering
                    if (settings.HideLADDCallsigns && adsbAc.IsLADD)
                        continue;

                    Aircraft matched = null;

                    // Primary match: by Mode S hex code
                    if (!string.IsNullOrEmpty(adsbAc.Hex))
                    {
                        try
                        {
                            int modeS = Convert.ToInt32(adsbAc.Hex, 16);
                            if (modeS != 0)
                                matched = aircraft.FirstOrDefault(x => x.ModeSCode == modeS);
                        }
                        catch { }
                    }

                    // Secondary match: by squawk (only if unique)
                    if (matched == null && !string.IsNullOrEmpty(adsbAc.Squawk))
                    {
                        var squawkMatches = aircraft.Where(x => x.Squawk == adsbAc.Squawk).ToList();
                        if (squawkMatches.Count == 1)
                            matched = squawkMatches[0];
                    }

                    // Tertiary match: by approximate position and altitude
                    if (matched == null && adsbAc.Latitude.HasValue && adsbAc.Longitude.HasValue)
                    {
                        var adsbPos = new GeoPoint(adsbAc.Latitude.Value, adsbAc.Longitude.Value);
                        int? adsbAlt = ParseAltitude(adsbAc.AltitudeBaro);
                        Aircraft closest = null;
                        double closestDist = PositionMatchThresholdNM;
                        foreach (var ac in aircraft)
                        {
                            if (ac.Location == null || (ac.Latitude == 0 && ac.Longitude == 0))
                                continue;
                            if (!string.IsNullOrEmpty(ac.Callsign))
                                continue;
                            // Altitude check: if both have altitude, they must be within threshold
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
                    }

                    if (matched != null && string.IsNullOrEmpty(matched.Callsign))
                    {
                        matched.Callsign = callsign;
                    }
                }
            }
        }
    }
}
