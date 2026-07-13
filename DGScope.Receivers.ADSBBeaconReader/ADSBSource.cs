using System;
using System.ComponentModel;

namespace DGScope.Receivers
{
    [Serializable()]
    [TypeConverter(typeof(ExpandableObjectConverter))]
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
