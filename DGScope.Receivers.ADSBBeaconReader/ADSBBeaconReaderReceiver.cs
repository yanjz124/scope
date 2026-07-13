using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;

namespace DGScope.Receivers
{
    public class ADSBBeaconReaderReceiver : Receiver
    {
        [DisplayName("Poll Interval (s)"), Description("Seconds between polls of each ADS-B source (minimum 3).")]
        public int PollIntervalSeconds { get; set; } = 5;

        [DisplayName("Hide LADD Callsigns"), Description("Suppress callsigns for aircraft flagged LADD (Limiting Aircraft Data Displayed), for FAA SWIM compliance.")]
        public bool HideLADDCallsigns { get; set; } = true;

        [DisplayName("Sources"), Description("ADS-B data sources to query for callsign enrichment.")]
        public List<ADSBSource> Sources { get; set; }

        private Timer pollTimer;
        private volatile bool running;

        public ADSBBeaconReaderReceiver()
        {
            // This receiver only annotates existing tracks with callsigns; it never
            // creates new targets, so tracks it can't match are simply left alone.
            CreateNewAircraft = false;
            Name = "ADS-B Beacon Reader";
            Sources = DefaultSources();
        }

        private static List<ADSBSource> DefaultSources()
        {
            return new List<ADSBSource>
            {
                new ADSBSource { Name = "adsb.lol", BaseUrl = "https://api.adsb.lol/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "adsb.fi", BaseUrl = "https://opendata.adsb.fi/api/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "airplanes.live", BaseUrl = "https://api.airplanes.live/v2", Enabled = true, IsBuiltIn = true }
            };
        }

        public override void Start()
        {
            if (running) return;
            NormalizeSources();
            running = true;
            var interval = Math.Max(3, PollIntervalSeconds) * 1000;
            pollTimer = new Timer(PollCallback, null, 0, interval);
        }

        public override void Stop()
        {
            running = false;
            pollTimer?.Dispose();
            pollTimer = null;
        }

        /// <summary>
        /// XmlSerializer appends to the constructor-populated Sources list on deserialize,
        /// which can duplicate the built-ins. Collapse duplicates by URL (keeping the last,
        /// i.e. the saved enabled/disabled state) and make sure the built-ins are present.
        /// </summary>
        private void NormalizeSources()
        {
            if (Sources == null)
                Sources = new List<ADSBSource>();

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

            Sources = deduped;
        }

        private void PollCallback(object state)
        {
            if (!running) return;

            try
            {
                if (Location == null || (Location.Latitude == 0 && Location.Longitude == 0))
                    return;

                var enabledSources = Sources.Where(s => s.Enabled).ToList();
                var allResults = new List<ADSBv2Aircraft>();

                for (int i = 0; i < enabledSources.Count; i++)
                {
                    try
                    {
                        var results = QuerySource(enabledSources[i]);
                        if (results != null)
                            allResults.AddRange(results);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ADSB Beacon Reader: Error querying {enabledSources[i].Name}: {ex.Message}");
                    }

                    // Rate limit: 1 request per second per API.
                    if (i < enabledSources.Count - 1)
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

        private List<ADSBv2Aircraft> QuerySource(ADSBSource source)
        {
            var url = $"{source.BaseUrl.TrimEnd('/')}/lat/{Location.Latitude:F6}/lon/{Location.Longitude:F6}/dist/{(int)Range}";

            using (var client = new WebClient())
            {
                client.Headers.Add("Accept", "application/json");
                client.Headers.Add("User-Agent", "DGScope-BeaconReader");
                var json = client.DownloadString(url);
                var response = JsonConvert.DeserializeObject<ADSBv2Response>(json);
                return response?.Aircraft;
            }
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

            foreach (var adsbAc in byHex.Values)
            {
                var callsign = adsbAc.Flight?.Trim();
                if (string.IsNullOrEmpty(callsign))
                    continue;

                if (HideLADDCallsigns && adsbAc.IsLADD)
                    continue;

                Aircraft matched = null;

                // Primary match: by Mode S hex code. Never create a new track.
                if (!string.IsNullOrEmpty(adsbAc.Hex))
                {
                    try
                    {
                        int modeS = Convert.ToInt32(adsbAc.Hex, 16);
                        if (modeS != 0)
                            matched = GetPlane(modeS, false);
                    }
                    catch { }
                }

                // Secondary match: by squawk, only when it's unique on the scope.
                if (matched == null && !string.IsNullOrEmpty(adsbAc.Squawk))
                    matched = GetPlaneBySquawk(adsbAc.Squawk);

                // Write to ADSBCallsign, which SWIM never overwrites, so the render loop
                // can keep re-applying it even when SWIM resets Callsign back to the squawk.
                if (matched != null)
                    matched.ADSBCallsign = callsign;
            }
        }
    }
}
