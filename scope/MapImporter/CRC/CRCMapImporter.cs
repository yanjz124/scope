using BAMCIS.GeoJSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DGScope.MapImporter.CRC
{
    internal class CRCMapImporter
    {
        public static List<VideoMap> CRCARTCCFileToMaps (string filename)
        {
            CRCARTCC artcc;
            using (StreamReader file = File.OpenText(filename))
            {
                JsonSerializer serializer = new JsonSerializer();
                artcc = (CRCARTCC)serializer.Deserialize(file, typeof(CRCARTCC));
            }
            if (artcc == null)
            {
                return new List<VideoMap>();
            }
            var artcc_id = artcc.id;
            var mapdirectory = Directory.GetParent(filename).Parent.FullName + "\\VideoMaps\\" + artcc_id + "\\";
            var facilities = artcc.facility.childFacilities.Where(x => x.starsConfiguration != null && x.starsConfiguration.videoMapIds.Any());
            if (!facilities.Any())
            {
                return new List<VideoMap>();
            }
            Facility importfacility;
            if (facilities.Count() == 1)
            {
                importfacility = facilities.First();
            }
            else
            {
                var ids = facilities.Select(x => x.id).ToList();
                using (CRCFacilityPicker picker = new CRCFacilityPicker(facilities.Select(x => x.id).ToList()))
                {
                    if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        importfacility = facilities.Where(x => x.id == picker.PickedFacility).FirstOrDefault();
                        if (importfacility == null) 
                        {
                            return new List<VideoMap>();
                        }
                    }
                    else
                    {
                        return new List<VideoMap>();
                    }
                }
            }
            var importmapids = importfacility.starsConfiguration.videoMapIds.ToList();
            List<VideoMap> maps = new List<VideoMap>();
            var report = new MapLoadReport();
            var skipped = new List<string>();

            foreach (var importmapid in importmapids)
            {
                Videomap importmap = artcc.videoMaps.Where(x => x.id == importmapid).FirstOrDefault();
                if (importmap == null)
                {
                    continue;
                }
                if (importmap.starsId.HasValue)
                {
                    var name = importmap.name;
                    try
                    {
                        VideoMap map = new VideoMap();
                        var mappath = mapdirectory + importmap.id + ".geojson";
                        var mnemonic = importmap.shortName;
                        var importmapobj = GeoJSONMapExporter.GeoJSONFileToMaps(mappath, report);

                        // Take every map the file produced, not just the first. Files that
                        // fall back to lenient parsing can split across several maps, and
                        // keeping only the first silently dropped most of the drawing.
                        foreach (var loaded in importmapobj)
                            map.Lines.AddRange(loaded.Lines);

                        if (map.Lines.Count == 0)
                            skipped.Add(name);

                        map.Name = name;
                        map.Mnemonic = mnemonic;
                        map.Category = importmap.starsBrightnessCategory == "A" ? MapCategory.A : MapCategory.B;
                        map.Number = importmap.starsId.Value;
                        maps.Add(map);
                    }
                    catch (Exception ex)
                    {
                        // One unreadable map must not abandon the rest of the facility.
                        skipped.Add(name);
                        report.Error(name + ": " + ex.Message);
                    }
                }
            }

            // One summary at the end, rather than a modal dialog per failed map.
            if (skipped.Count > 0 || report.Errors.Count > 0)
            {
                var message = new StringBuilder();
                message.AppendLine($"Imported {maps.Count} map(s).");
                if (skipped.Count > 0)
                {
                    message.AppendLine();
                    message.AppendLine($"{skipped.Count} map(s) contained no drawable lines:");
                    foreach (var name in skipped.Take(15))
                        message.AppendLine("  " + name);
                    if (skipped.Count > 15)
                        message.AppendLine($"  ...and {skipped.Count - 15} more.");
                }
                var summary = report.Summary();
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    message.AppendLine();
                    message.AppendLine(summary);
                }
                System.Windows.Forms.MessageBox.Show(message.ToString(), "CRC map import",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }

            return maps;
        }
    }
}
