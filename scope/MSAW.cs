using System.Collections.Generic;
using System.Linq;

namespace DGScope
{
    public class MSAW
    {
        private bool active = true;
        public List<MSAWVolume> Volumes { get; set; } = new List<MSAWVolume>();
        // Seconds to project the track forward when looking for a predicted violation.
        public int LookAheadSeconds { get; set; } = 30;
        // True when at least one aircraft has an unacknowledged low-altitude alert
        // (drives the repeating aural alert). Updated each Calculate().
        public bool UnacknowledgedAlert { get; private set; }

        public bool Active
        {
            get => active;
            set
            {
                if (value == active)
                    return;
                active = value;
                if (!value)
                {
                    lock (RadarWindow.Aircraft)
                    {
                        RadarWindow.Aircraft.ToList().ForEach(x =>
                        {
                            x.LowAltitude = false;
                            x.LowAltitudeAcknowledged = false;
                        });
                    }
                }
            }
        }

        public void Calculate(ICollection<Aircraft> aircraftList, Radar radar)
        {
            List<Aircraft> aircraft;
            lock (aircraftList)
                aircraft = aircraftList.ToList();
            MSAWVolume[] vols;
            lock (Volumes)
                vols = Volumes.Where(v => v.Active).ToArray();
            bool anyUnacked = false;
            foreach (var ac in aircraft)
            {
                if (IsLowAltitude(ac, radar, vols))
                {
                    ac.LowAltitude = true;
                    if (!ac.LowAltitudeAcknowledged)
                        anyUnacked = true;
                }
                else
                {
                    ac.LowAltitude = false;
                    ac.LowAltitudeAcknowledged = false;
                }
            }
            UnacknowledgedAlert = anyUnacked;
        }

        private bool IsLowAltitude(Aircraft ac, Radar radar, MSAWVolume[] vols)
        {
            if (ac == null || ac.Deleted || vols.Length == 0)
                return false;
            // MSAW inhibited (automatic VFR inhibit or manual F7 V/Q <slew>).
            if (ac.IsMSAWInhibited)
                return false;
            // Need a Mode C altitude to evaluate.
            if (ac.PrimaryOnly || ac.Altitude == null || ac.Altitude.AltitudeType == AltitudeType.Unknown)
                return false;
            var loc = ac.SweptLocation(radar) ?? ac.Location;
            if (loc == null)
                return false;
            int alt = ac.TrueAltitude;
            if (InViolation(loc, alt, vols))
                return true;
            // Look-ahead: project the current position forward along the track.
            if (ac.GroundSpeed > 0 && LookAheadSeconds > 0)
            {
                var predicted = loc.FromPoint(ac.GroundSpeed * LookAheadSeconds / 3600d, ac.ExtrapolateTrack());
                if (InViolation(predicted, alt, vols))
                    return true;
            }
            return false;
        }

        private static bool InViolation(GeoPoint loc, int alt, MSAWVolume[] vols)
        {
            foreach (var v in vols)
            {
                if (alt >= v.Ceiling || alt < v.Floor)
                    continue;
                if (v.ContainsLocation(loc))
                    return true;
            }
            return false;
        }
    }
}
