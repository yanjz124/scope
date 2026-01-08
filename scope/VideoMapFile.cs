using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace DGScope
{
    [Serializable()]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class VideoMapFile
    {
        // File path to the GeoJSON file
        public string Filepath { get; set; }

        // Map number for this map (used for DCB mapping and visibility)
        public int MapNumber { get; set; }

        // Short mnemonic name (shown on DCB button)
        public string ShortName { get; set; }

        // Full descriptive name
        public string FullName { get; set; }

        // Brightness group (A or B)
        public MapCategory BrightnessGroup { get; set; } = MapCategory.A;

        // Which DCB button controls this map (0-35, 0 = none)
        public int DCBButton { get; set; }

        public override string ToString()
        {
            return $"{MapNumber}: {ShortName} ({Filepath})";
        }
    }
}
