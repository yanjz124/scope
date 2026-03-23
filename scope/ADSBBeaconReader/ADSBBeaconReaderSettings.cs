using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace DGScope.ADSBBeaconReader
{
    [Serializable()]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [JsonObject]
    public class ADSBBeaconReaderSettings
    {
        public List<ADSBSource> Sources { get; set; }
        public bool HideLADDCallsigns { get; set; } = true;
        public int PollIntervalSeconds { get; set; } = 5;

        public ADSBBeaconReaderSettings()
        {
            Sources = new List<ADSBSource>
            {
                new ADSBSource { Name = "adsb.lol", BaseUrl = "https://api.adsb.lol/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "adsb.fi", BaseUrl = "https://opendata.adsb.fi/api/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "airplanes.live", BaseUrl = "https://api.airplanes.live/v2", Enabled = true, IsBuiltIn = true }
            };
        }

        public bool AnyEnabled => Sources.Any(s => s.Enabled);

        /// <summary>
        /// Ensures built-in sources exist after deserialization (in case user has an older config)
        /// </summary>
        public void EnsureBuiltInSources()
        {
            var builtIns = new[]
            {
                new ADSBSource { Name = "adsb.lol", BaseUrl = "https://api.adsb.lol/v2", IsBuiltIn = true },
                new ADSBSource { Name = "adsb.fi", BaseUrl = "https://opendata.adsb.fi/api/v2", IsBuiltIn = true },
                new ADSBSource { Name = "airplanes.live", BaseUrl = "https://api.airplanes.live/v2", IsBuiltIn = true }
            };

            foreach (var builtIn in builtIns)
            {
                if (!Sources.Any(s => s.BaseUrl == builtIn.BaseUrl))
                {
                    Sources.Insert(0, builtIn);
                }
            }
        }
    }

    [Serializable()]
    public class ADSBSource
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public bool Enabled { get; set; }
        public bool IsBuiltIn { get; set; }

        public override string ToString()
        {
            return Name + (IsBuiltIn ? "" : " (" + BaseUrl + ")");
        }
    }
}
