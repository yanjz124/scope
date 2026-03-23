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
        // Don't initialize in constructor — XmlSerializer calls the constructor then
        // appends deserialized items, causing duplicates. Use EnsureBuiltInSources() instead.
        public List<ADSBSource> Sources { get; set; } = new List<ADSBSource>();
        public bool HideLADDCallsigns { get; set; } = true;
        public int PollIntervalSeconds { get; set; } = 5;

        [XmlIgnore]
        public bool AnyEnabled => Sources != null && Sources.Any(s => s.Enabled);

        /// <summary>
        /// Ensures built-in sources exist after deserialization (in case user has an older config)
        /// </summary>
        public void EnsureBuiltInSources()
        {
            if (Sources == null)
                Sources = new List<ADSBSource>();

            var builtIns = new[]
            {
                new ADSBSource { Name = "adsb.lol", BaseUrl = "https://api.adsb.lol/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "adsb.fi", BaseUrl = "https://opendata.adsb.fi/api/v2", Enabled = true, IsBuiltIn = true },
                new ADSBSource { Name = "airplanes.live", BaseUrl = "https://api.airplanes.live/v2", Enabled = true, IsBuiltIn = true }
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
