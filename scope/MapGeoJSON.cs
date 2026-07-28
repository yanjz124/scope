using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BAMCIS.GeoJSON;
using Newtonsoft.Json.Linq;

namespace DGScope
{
    /// <summary>
    /// Collects everything that went wrong while loading one or more video map files so
    /// the caller can show a single summary at the end, instead of a modal dialog per
    /// file that turns a bad map into an unusable import.
    /// </summary>
    public class MapLoadReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int FilesRead { get; set; }
        public int FilesFailed { get; set; }
        public int FeaturesSkipped { get; set; }

        public bool HasProblems => Errors.Count > 0 || Warnings.Count > 0;

        public void Error(string message)
        {
            Errors.Add(message);
        }

        public void Warn(string message)
        {
            Warnings.Add(message);
        }

        /// <summary>
        /// A short, bounded description suitable for one message box. Long lists are
        /// truncated so a facility-wide import can't produce an unreadable wall of text.
        /// </summary>
        public string Summary(int maxLines = 15)
        {
            var sb = new StringBuilder();
            if (FilesRead > 0)
                sb.AppendLine($"{FilesRead - FilesFailed} of {FilesRead} map files loaded.");
            if (FeaturesSkipped > 0)
                sb.AppendLine($"{FeaturesSkipped} unreadable feature(s) skipped.");

            AppendSection(sb, "Errors", Errors, maxLines);
            AppendSection(sb, "Warnings", Warnings, maxLines);
            return sb.ToString().TrimEnd();
        }

        private static void AppendSection(StringBuilder sb, string title, List<string> items, int maxLines)
        {
            if (items.Count == 0)
                return;
            sb.AppendLine();
            sb.AppendLine(title + ":");
            foreach (var item in items.Take(maxLines))
                sb.AppendLine("  " + item);
            if (items.Count > maxLines)
                sb.AppendLine($"  ...and {items.Count - maxLines} more.");
        }
    }

    public static class GeoJSONMapExporter
    {
        public static string MapsToGeoJSON(VideoMapList maps)
        {
            List<Feature> features = new List<Feature>();
            foreach (var map in maps)
            {
                Feature feature = new Feature(MapToGeometryCollection(map));
                feature.Properties.Add("name", map.Name);
                feature.Properties.Add("number", map.Number);
                feature.Properties.Add("category", map.Category);
                feature.Properties.Add("mnemonic", map.Mnemonic);
                features.Add(feature);
            }
            FeatureCollection fc = new FeatureCollection(features);
            return fc.ToJson();
        }
        public static string MapToGeoJSON(VideoMap map)
        {
            
            FeatureCollection fc = new FeatureCollection(MapToFeatureList(map));
            return fc.ToJson();
        }

        private static List<Feature> MapToFeatureList(VideoMap map)
        {
            List<Feature> features = new List<Feature>();
            foreach (var line in map.Lines)
            {
                var lineString = LineToLineString(line);
                if (lineString != null)
                {
                    Feature feature = new Feature(lineString);
                    features.Add(feature);
                }
            }
            return features;
        }

        private static GeometryCollection MapToGeometryCollection(VideoMap map)
        {
            List<Geometry> linestrings = new List<Geometry>();
            foreach (var line in map.Lines)
            {
                var lineString = LineToLineString(line);
                if (lineString != null)
                    linestrings.Add(lineString);
            }
            return new GeometryCollection(linestrings);
        }

        private static LineString LineToLineString(Line line)
        {
            List<Position> positions = new List<Position>();
            if (Math.Abs(line.End1.Latitude) > 90 || Math.Abs(line.End2.Latitude) > 90 || Math.Abs(line.End1.Longitude) > 180 || Math.Abs(line.End2.Longitude) > 180)
                return null;
            positions.Add(new Position(line.End1.Longitude, line.End1.Latitude));
            positions.Add(new Position(line.End2.Longitude, line.End2.Latitude));
            LineString lineString = new LineString(positions);
            return lineString;
        }

        public static void MapToGeoJSONFile(VideoMap map, string filename)
        {
            File.WriteAllText(filename, MapToGeoJSON(map));
        }
        public static void MapsToGeoJSONFile(VideoMapList maps, string filename)
        {
            File.WriteAllText(filename, MapsToGeoJSON(maps));
        }

        /// <summary>
        /// Loads one GeoJSON file. Never throws and never returns null: a file that can't
        /// be read or parsed yields an empty list, with the reason recorded in the report.
        /// </summary>
        public static VideoMapList GeoJSONFileToMaps(string path, MapLoadReport report)
        {
            if (report != null)
                report.FilesRead++;

            string json;
            try
            {
                // Reading was outside the try before, so a missing or locked file aborted
                // the whole import rather than skipping the one map.
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                if (report != null)
                {
                    report.FilesFailed++;
                    report.Error(Path.GetFileName(path) + ": " + ex.Message);
                }
                return new VideoMapList();
            }

            try
            {
                return GeoJSONToMaps(json, report, Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                if (report != null)
                {
                    report.FilesFailed++;
                    report.Error(Path.GetFileName(path) + ": " + ex.Message);
                }
                return new VideoMapList();
            }
        }

        public static VideoMapList GeoJSONFileToMaps(string path)
        {
            var report = new MapLoadReport();
            var maps = GeoJSONFileToMaps(path, report);
            if (report.Errors.Count > 0)
                System.Windows.Forms.MessageBox.Show("Error with video map " + path + "\r\n" + report.Errors[0]);
            return maps;
        }

        /// <summary>
        /// Strips geometries the strict parser rejects - paths with fewer than two
        /// positions - so the rest of the document can still be read.
        /// </summary>
        private static string FixInvalidLineStrings(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                changedDuringFix = false;
                FixGeometriesRecursive(obj);
                // Returning the original reference when nothing needed fixing lets the
                // caller skip a re-parse, and re-serializing these files is expensive.
                return changedDuringFix ? obj.ToString() : json;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error preprocessing GeoJSON: {ex.Message}");
                return json;
            }
        }

        [ThreadStatic]
        private static bool changedDuringFix;

        private static void FixGeometriesRecursive(JToken token)
        {
            if (token == null) return;

            if (token is JObject jObj)
            {
                var typeValue = jObj["type"]?.Value<string>();
                switch (typeValue)
                {
                    case "LineString":
                    {
                        var coordinates = jObj["coordinates"] as JArray;
                        if (coordinates != null && coordinates.Count < 2)
                        {
                            // Mark for deletion; the parent removes it.
                            jObj["__delete__"] = true;
                            changedDuringFix = true;
                        }
                        return;
                    }

                    case "MultiLineString":
                    case "Polygon":
                        // Both hold an array of paths, and both reject short ones.
                        PruneShortPaths(jObj);
                        return;

                    case "MultiPolygon":
                    {
                        // Was not handled at all before, so a MultiPolygon with a
                        // degenerate ring kept failing the strict parse.
                        var polygons = jObj["coordinates"] as JArray;
                        if (polygons == null)
                            return;
                        var validPolygons = new JArray();
                        foreach (var polygon in polygons)
                        {
                            var rings = polygon as JArray;
                            if (rings == null)
                                continue;
                            var validRings = new JArray(rings.Where(r => r is JArray ring && ring.Count >= 2));
                            if (validRings.Count > 0)
                                validPolygons.Add(validRings);
                        }
                        if (validPolygons.Count != polygons.Count)
                            changedDuringFix = true;
                        jObj["coordinates"] = validPolygons;
                        return;
                    }

                    case "GeometryCollection":
                    {
                        var geometries = jObj["geometries"] as JArray;
                        if (geometries == null)
                            return;
                        foreach (var geom in geometries)
                            FixGeometriesRecursive(geom);
                        var validGeometries = new JArray(geometries.Where(g =>
                            !(g is JObject gObj && gObj["__delete__"]?.Value<bool>() == true)));
                        if (validGeometries.Count != geometries.Count)
                            changedDuringFix = true;
                        jObj["geometries"] = validGeometries;
                        return;
                    }

                    case "Feature":
                    {
                        var geometry = jObj["geometry"];
                        if (geometry != null)
                        {
                            FixGeometriesRecursive(geometry);
                            if (geometry is JObject geomObj && geomObj["__delete__"]?.Value<bool>() == true)
                            {
                                jObj["geometry"] = null;
                                changedDuringFix = true;
                            }
                        }
                        return;
                    }

                    case "FeatureCollection":
                    {
                        var features = jObj["features"] as JArray;
                        if (features != null)
                            foreach (var feature in features)
                                FixGeometriesRecursive(feature);
                        return;
                    }

                    case "Point":
                    case "MultiPoint":
                        // Always valid; nothing to prune.
                        return;
                }

                // Anything else: keep walking, in case geometries are nested in it.
                foreach (var prop in jObj.Properties().ToList())
                {
                    if (prop.Name != "__delete__")
                        FixGeometriesRecursive(prop.Value);
                }
            }
            else if (token is JArray jArr)
            {
                for (int i = 0; i < jArr.Count; i++)
                {
                    FixGeometriesRecursive(jArr[i]);
                }
            }
        }

        /// <summary>
        /// Drops coordinate paths with fewer than two positions from a geometry whose
        /// "coordinates" member is an array of paths (MultiLineString, Polygon).
        /// </summary>
        private static void PruneShortPaths(JObject geometry)
        {
            var coordinates = geometry["coordinates"] as JArray;
            if (coordinates == null)
                return;
            var valid = new JArray(coordinates.Where(path => path is JArray p && p.Count >= 2));
            if (valid.Count != coordinates.Count)
                changedDuringFix = true;
            geometry["coordinates"] = valid;
        }

        public static VideoMapList GeoJSONToMaps(string json)
        {
            return GeoJSONToMaps(json, null, null);
        }

        /// <summary>
        /// Reads a GeoJSON document into video maps using three escalating attempts, so a
        /// single malformed geometry never costs the whole file:
        ///   1. parse as written (the fast path, and what well-formed files use);
        ///   2. strip degenerate geometries, then parse again;
        ///   3. read the JSON by hand, ignoring schema violations entirely.
        /// </summary>
        public static VideoMapList GeoJSONToMaps(string json, MapLoadReport report, string source)
        {
            GeoJsonConfig.IgnoreLatitudeValidation = true;
            GeoJsonConfig.IgnoreLongitudeValidation = true;
            var label = string.IsNullOrEmpty(source) ? "map" : source;

            try
            {
                var strict = ParseWithGeoJsonLibrary(json, report);
                // Escalate not just on exceptions but also when a strict parse quietly
                // returns nothing from a file that plainly holds line geometry - that is
                // a silent miss, and it used to be indistinguishable from an empty map.
                if (strict.Count > 0 || !ContainsLineGeometry(json))
                    return strict;
                report?.Warn($"{label}: no lines from a strict read - retrying leniently.");
            }
            catch (Exception ex)
            {
                report?.Warn($"{label}: {ex.Message} - retrying without degenerate geometries.");
            }

            try
            {
                var repaired = FixInvalidLineStrings(json);
                if (!ReferenceEquals(repaired, json))
                {
                    var maps = ParseWithGeoJsonLibrary(repaired, report);
                    if (maps.Count > 0)
                        return maps;
                }
            }
            catch (Exception ex)
            {
                report?.Warn($"{label}: {ex.Message} - falling back to manual parsing.");
            }

            string manualFailure = null;
            try
            {
                var manual = TryManualGeoJSONParse(json, report);
                if (manual.Count > 0)
                    return manual;
            }
            catch (Exception ex)
            {
                manualFailure = ex.Message;
            }

            // Last tier: the document itself is broken - a truncated tail, a missing
            // comma - so no reader can walk its structure. Lift out the geometry objects
            // that are still individually intact and keep those.
            try
            {
                var salvaged = SalvageGeometries(json);
                if (salvaged.Count > 0)
                {
                    report?.Warn($"{label}: malformed file; recovered " +
                        $"{salvaged.Sum(m => m.Lines.Count)} line(s) from intact geometries.");
                    return salvaged;
                }
            }
            catch { /* nothing left to recover */ }

            if (manualFailure != null && report != null)
            {
                report.FilesFailed++;
                report.Error($"{label}: could not be read ({manualFailure}).");
            }
            return new VideoMapList();
        }

        /// <summary>
        /// Recovers geometry from a document too damaged to parse as a whole. Each
        /// "geometry" value is located textually and parsed on its own, so corruption
        /// anywhere else in the file costs only the geometries it actually touches.
        /// </summary>
        private static VideoMapList SalvageGeometries(string json)
        {
            var maps = new VideoMapList();
            var lines = new List<Line>();
            const string marker = "\"geometry\"";
            int index = 0;

            while ((index = json.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
            {
                index += marker.Length;
                int open = IndexOfGeometryBrace(json, index);
                if (open < 0)
                    continue;
                int close = FindMatchingBrace(json, open);
                if (close < 0)
                    break;

                try
                {
                    var geometry = JObject.Parse(json.Substring(open, close - open + 1));
                    var geometryLines = ManualGeometryToLines(geometry, null);
                    if (geometryLines != null)
                        lines.AddRange(geometryLines);
                }
                catch { /* this one geometry is unreadable; the rest still are */ }

                index = close + 1;
            }

            if (lines.Count > 0)
            {
                var map = new VideoMap { Name = ImportedMapName() };
                map.Lines.AddRange(lines);
                maps.Add(map);
            }
            return maps;
        }

        /// <summary>
        /// Position of the brace opening a "geometry" value, or -1 when the value is not
        /// an object (a null geometry, for instance) - so a null never causes the next
        /// feature's geometry to be picked up by mistake.
        /// </summary>
        private static int IndexOfGeometryBrace(string json, int afterMarker)
        {
            int i = afterMarker;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != ':') return -1;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            return (i < json.Length && json[i] == '{') ? i : -1;
        }

        /// <summary>
        /// Index of the brace closing the object that starts at <paramref name="open"/>,
        /// skipping over braces that appear inside strings. -1 if it is never closed.
        /// </summary>
        private static int FindMatchingBrace(string json, int open)
        {
            int depth = 0;
            bool inString = false, escaped = false;
            for (int i = open; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// Cheap text probe for a geometry type that can produce lines. Used only to
        /// decide whether an empty result is suspicious and worth a second attempt.
        /// </summary>
        private static bool ContainsLineGeometry(string json)
        {
            return json.IndexOf("\"LineString\"", StringComparison.OrdinalIgnoreCase) >= 0
                || json.IndexOf("\"MultiLineString\"", StringComparison.OrdinalIgnoreCase) >= 0
                || json.IndexOf("\"Polygon\"", StringComparison.OrdinalIgnoreCase) >= 0
                || json.IndexOf("\"MultiPolygon\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static VideoMapList ParseWithGeoJsonLibrary(string json, MapLoadReport report)
        {
            VideoMapList maps = new VideoMapList();
            var data = GeoJson.FromJson(json);

            switch (data.Type)
            {
                case GeoJsonType.FeatureCollection:
                    var featureCollection = data as FeatureCollection;
                    var features = featureCollection.Features ?? Enumerable.Empty<Feature>();

                    // Two conventions live side by side: one map per GeometryCollection
                    // feature, or one map built from every feature in the file.
                    if (features.Any(x => x.Geometry != null && x.Geometry.Type == GeoJsonType.GeometryCollection))
                    {
                        foreach (var feature in features.Where(x => x.Geometry != null && x.Geometry.Type == GeoJsonType.GeometryCollection))
                        {
                            VideoMap newmap = new VideoMap();
                            ApplyFeatureProperties(feature, newmap);

                            var geometryCollection = feature.Geometry as GeometryCollection;
                            foreach (var geometry in geometryCollection.Geometries ?? Enumerable.Empty<Geometry>())
                            {
                                var lines = SafeGeometryToLines(geometry, report);
                                if (lines != null && lines.Count > 0)
                                    newmap.Lines.AddRange(lines);
                            }

                            if (newmap.Lines.Count > 0)
                                maps.Add(newmap);
                        }
                    }
                    else
                    {
                        VideoMap map = new VideoMap();
                        map.Name = ImportedMapName();

                        // Some files name the whole collection rather than a feature.
                        var collectionName = TopLevelName(json);
                        if (!string.IsNullOrWhiteSpace(collectionName))
                            map.Name = collectionName;

                        foreach (var feature in features.Where(x => x.Geometry != null))
                        {
                            var lines = SafeGeometryToLines(feature.Geometry, report);
                            if (lines != null && lines.Count > 0)
                                map.Lines.AddRange(lines);
                        }

                        if (map.Lines.Count > 0)
                            maps.Add(map);
                    }
                    break;

                case GeoJsonType.Feature:
                    var singleFeature = data as Feature;
                    if (singleFeature?.Geometry != null)
                    {
                        VideoMap featureMap = new VideoMap();
                        featureMap.Name = ImportedMapName();
                        ApplyFeatureProperties(singleFeature, featureMap);

                        var featureLines = SafeGeometryToLines(singleFeature.Geometry, report);
                        if (featureLines != null && featureLines.Count > 0)
                        {
                            featureMap.Lines.AddRange(featureLines);
                            maps.Add(featureMap);
                        }
                    }
                    break;

                default:
                    // Any bare geometry at the top level, including Point/MultiPoint,
                    // which simply contribute no lines.
                    var geomLines = SafeGeometryToLines(data as Geometry, report);
                    if (geomLines != null && geomLines.Count > 0)
                    {
                        VideoMap geomMap = new VideoMap();
                        geomMap.Name = ImportedMapName();
                        geomMap.Lines.AddRange(geomLines);
                        maps.Add(geomMap);
                    }
                    break;
            }
            return maps;
        }

        private static string ImportedMapName()
        {
            return "Imported map - " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();
        }

        /// <summary>
        /// Reads the optional top-level "name" member without failing on malformed JSON.
        /// </summary>
        private static string TopLevelName(string json)
        {
            try { return JObject.Parse(json)["name"]?.Value<string>(); }
            catch { return null; }
        }

        /// <summary>
        /// Copies name/number/mnemonic/category off a feature. Each is read defensively:
        /// a property of an unexpected type should cost that one field, not the map.
        /// </summary>
        private static void ApplyFeatureProperties(Feature feature, VideoMap map)
        {
            var name = FeatureString(feature, "name");
            if (!string.IsNullOrWhiteSpace(name))
                map.Name = name;

            var mnemonic = FeatureString(feature, "mnemonic");
            if (!string.IsNullOrWhiteSpace(mnemonic))
                map.Mnemonic = mnemonic;

            var number = FeatureInt(feature, "number");
            if (number.HasValue)
                map.Number = number.Value;

            var category = FeatureInt(feature, "category");
            if (category.HasValue && Enum.IsDefined(typeof(MapCategory), category.Value))
                map.Category = (MapCategory)category.Value;
        }

        private static string FeatureString(Feature feature, string key)
        {
            try
            {
                if (feature?.Properties == null || !feature.Properties.ContainsKey(key))
                    return null;
                object value = feature.Properties[key];
                return value?.ToString();
            }
            catch { return null; }
        }

        private static int? FeatureInt(Feature feature, string key)
        {
            try
            {
                if (feature?.Properties == null || !feature.Properties.ContainsKey(key))
                    return null;
                object value = feature.Properties[key];
                if (value == null)
                    return null;
                return Convert.ToInt32(value);
            }
            catch { return null; }
        }

        /// <summary>
        /// Converts one geometry, isolating any failure to that geometry so the rest of
        /// the file still loads.
        /// </summary>
        private static List<Line> SafeGeometryToLines(Geometry geometry, MapLoadReport report)
        {
            try
            {
                return GeometryToLines(geometry);
            }
            catch (Exception ex)
            {
                if (report != null)
                {
                    report.FeaturesSkipped++;
                    if (report.Warnings.Count < 50)
                        report.Warn("Skipped a " + (geometry?.Type.ToString() ?? "null") + ": " + ex.Message);
                }
                return null;
            }
        }

        /// <summary>
        /// Last-resort reader that walks the raw JSON. It ignores the GeoJSON schema
        /// entirely, so files the strict parser rejects still yield whatever geometry
        /// they do contain. Grouping matches the strict parser: one map per
        /// GeometryCollection feature, otherwise one map for the whole collection.
        /// </summary>
        private static VideoMapList TryManualGeoJSONParse(string json, MapLoadReport report)
        {
            var maps = new VideoMapList();
            var obj = JObject.Parse(json);
            var topType = obj["type"]?.Value<string>();

            // Some CRC exports omit the top-level "type" entirely and open with "crs" or
            // "features". Infer the container from the members that are present.
            if (string.IsNullOrEmpty(topType))
            {
                if (obj["features"] != null) topType = "FeatureCollection";
                else if (obj["geometry"] != null) topType = "Feature";
                else if (obj["geometries"] != null) topType = "GeometryCollection";
            }

            if (topType == "FeatureCollection")
            {
                var features = obj["features"] as JArray ?? new JArray();
                bool perFeatureMaps = features.Any(f => f["geometry"]?["type"]?.Value<string>() == "GeometryCollection");

                if (perFeatureMaps)
                {
                    foreach (var feature in features)
                    {
                        if (feature["geometry"]?["type"]?.Value<string>() != "GeometryCollection")
                            continue;
                        var lines = ManualGeometryToLines(feature["geometry"], report);
                        if (lines == null || lines.Count == 0)
                            continue;
                        var map = new VideoMap { Name = ImportedMapName() };
                        ApplyManualProperties(feature["properties"], map);
                        map.Lines.AddRange(lines);
                        maps.Add(map);
                    }
                }
                else
                {
                    // One map for the file, matching the strict parser. Building a map per
                    // feature here made callers that take the first map lose the rest.
                    var map = new VideoMap { Name = ImportedMapName() };
                    var collectionName = obj["name"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(collectionName))
                        map.Name = collectionName;

                    foreach (var feature in features)
                        CollectFeatureLines(feature, map.Lines, report, 0);

                    if (map.Lines.Count > 0)
                        maps.Add(map);
                }
            }
            else if (topType == "Feature")
            {
                if (obj["geometry"] != null)
                {
                    var lines = ManualGeometryToLines(obj["geometry"], report);
                    if (lines != null && lines.Count > 0)
                    {
                        var map = new VideoMap { Name = ImportedMapName() };
                        ApplyManualProperties(obj["properties"], map);
                        map.Lines.AddRange(lines);
                        maps.Add(map);
                    }
                }
            }
            else
            {
                // Bare geometry at top level
                var lines = ManualGeometryToLines(obj, report);
                if (lines != null && lines.Count > 0)
                {
                    var map = new VideoMap { Name = ImportedMapName() };
                    map.Lines.AddRange(lines);
                    maps.Add(map);
                }
            }

            return maps;
        }

        /// <summary>
        /// Adds every line reachable from one entry of a "features" array. CRC files
        /// sometimes nest a whole FeatureCollection inside features[], which a flat walk
        /// skips entirely, so recurse into those as well.
        /// </summary>
        private static void CollectFeatureLines(JToken feature, List<Line> into, MapLoadReport report, int depth)
        {
            if (feature == null || depth > 8)
                return;

            var nested = feature["features"] as JArray;
            if (nested != null)
            {
                foreach (var child in nested)
                    CollectFeatureLines(child, into, report, depth + 1);
                return;
            }

            var geometry = feature["geometry"];
            if (geometry == null)
            {
                // A bare geometry sitting directly in features[].
                if (feature["coordinates"] != null || feature["geometries"] != null)
                    geometry = feature;
                else
                    return;
            }

            var lines = ManualGeometryToLines(geometry, report);
            if (lines != null && lines.Count > 0)
                into.AddRange(lines);
        }

        private static void ApplyManualProperties(JToken properties, VideoMap map)
        {
            if (properties == null)
                return;
            try
            {
                var name = properties["name"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(name))
                    map.Name = name;
                var mnemonic = properties["mnemonic"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(mnemonic))
                    map.Mnemonic = mnemonic;
                var number = properties["number"];
                if (number != null && number.Type != JTokenType.Null)
                    map.Number = number.Value<int>();
                var category = properties["category"];
                if (category != null && category.Type != JTokenType.Null)
                {
                    var value = category.Value<int>();
                    if (Enum.IsDefined(typeof(MapCategory), value))
                        map.Category = (MapCategory)value;
                }
            }
            catch { /* metadata is optional; geometry still loads without it */ }
        }

        /// <summary>
        /// Extracts line segments from a raw geometry token, covering every GeoJSON
        /// geometry type. Point and MultiPoint contribute nothing because a video map
        /// draws only lines; that is not an error. Bad coordinates are skipped
        /// individually so one corrupt pair can't discard the surrounding geometry.
        /// </summary>
        private static List<Line> ManualGeometryToLines(JToken geometry, MapLoadReport report)
        {
            if (!(geometry is JObject geometryObj))
                return null;

            var typeStr = geometryObj["type"]?.Value<string>();
            var lines = new List<Line>();

            try
            {
                switch (typeStr)
                {
                    case "LineString":
                        AddPath(geometryObj["coordinates"] as JArray, lines);
                        break;

                    case "MultiLineString":
                    case "Polygon":
                        // Both are an array of paths: line strings, or a ring per hole.
                        foreach (var path in AsArray(geometryObj["coordinates"]))
                            AddPath(path as JArray, lines);
                        break;

                    case "MultiPolygon":
                        foreach (var polygon in AsArray(geometryObj["coordinates"]))
                            foreach (var ring in AsArray(polygon))
                                AddPath(ring as JArray, lines);
                        break;

                    case "GeometryCollection":
                        foreach (var geom in AsArray(geometryObj["geometries"]))
                        {
                            var geomLines = ManualGeometryToLines(geom, report);
                            if (geomLines != null)
                                lines.AddRange(geomLines);
                        }
                        break;

                    case "Point":
                    case "MultiPoint":
                        // Nothing to draw, and nothing wrong.
                        break;

                    default:
                        if (report != null && !string.IsNullOrEmpty(typeStr) && report.Warnings.Count < 50)
                            report.Warn("Unrecognized geometry type '" + typeStr + "'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                if (report != null)
                {
                    report.FeaturesSkipped++;
                    if (report.Warnings.Count < 50)
                        report.Warn("Skipped a " + (typeStr ?? "geometry") + ": " + ex.Message);
                }
            }

            return lines.Count > 0 ? lines : null;
        }

        private static IEnumerable<JToken> AsArray(JToken token)
        {
            return token as JArray ?? Enumerable.Empty<JToken>();
        }

        /// <summary>
        /// Turns a coordinate array into segments, dropping any position that can't be
        /// read as a finite lon/lat pair.
        /// </summary>
        private static void AddPath(JArray path, List<Line> lines)
        {
            if (path == null || path.Count < 2)
                return;

            GeoPoint previous = null;
            foreach (var position in path)
            {
                GeoPoint point;
                if (!TryReadPosition(position, out point))
                {
                    // Break the path here rather than joining across the gap.
                    previous = null;
                    continue;
                }
                if (previous != null)
                    lines.Add(new Line(previous, point));
                previous = point;
            }
        }

        private static bool TryReadPosition(JToken position, out GeoPoint point)
        {
            point = null;
            var array = position as JArray;
            if (array == null || array.Count < 2)
                return false;
            try
            {
                double lon = array[0].Value<double>();
                double lat = array[1].Value<double>();
                if (double.IsNaN(lon) || double.IsNaN(lat) || double.IsInfinity(lon) || double.IsInfinity(lat))
                    return false;
                point = new GeoPoint(lat, lon);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Converts any GeoJSON geometry type to line segments for rendering
        /// </summary>
        private static List<Line> GeometryToLines(Geometry geometry)
        {
            if (geometry == null) return null;

            switch (geometry.Type)
            {
                case GeoJsonType.LineString:
                    return LineStringToLines(geometry as LineString);

                case GeoJsonType.Polygon:
                    return PolygonToLines(geometry as BAMCIS.GeoJSON.Polygon);

                case GeoJsonType.MultiLineString:
                    return MultiLineStringToLines(geometry as MultiLineString);

                case GeoJsonType.MultiPolygon:
                    return MultiPolygonToLines(geometry as BAMCIS.GeoJSON.MultiPolygon);

                case GeoJsonType.GeometryCollection:
                    // Recursively process all geometries in the collection
                    var allLines = new List<Line>();
                    var collection = geometry as GeometryCollection;
                    foreach (var geom in collection.Geometries)
                    {
                        var lines = GeometryToLines(geom);
                        if (lines != null && lines.Count > 0)
                        {
                            allLines.AddRange(lines);
                        }
                    }
                    return allLines.Count > 0 ? allLines : null;

                case GeoJsonType.Point:
                case GeoJsonType.MultiPoint:
                    // A video map draws lines only, so points contribute nothing. This is
                    // normal for CRC symbol and label maps, not a failure.
                    return null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Builds a segment between two positions, or null if either is unusable.
        /// </summary>
        private static Line SegmentBetween(Position a, Position b)
        {
            if (double.IsNaN(a.Latitude) || double.IsNaN(a.Longitude) ||
                double.IsNaN(b.Latitude) || double.IsNaN(b.Longitude) ||
                double.IsInfinity(a.Latitude) || double.IsInfinity(a.Longitude) ||
                double.IsInfinity(b.Latitude) || double.IsInfinity(b.Longitude))
                return null;
            return new Line(new GeoPoint(a.Latitude, a.Longitude), new GeoPoint(b.Latitude, b.Longitude));
        }

        private static List<Line> LineStringToLines(LineString lineString)
        {
            if (lineString?.Coordinates == null) return null;
            var points = lineString.Coordinates.ToArray();
            var lines = new List<Line>();
            for (int i = 1; i < points.Length; i++)
            {
                var segment = SegmentBetween(points[i], points[i - 1]);
                if (segment != null)
                    lines.Add(segment);
            }
            return lines;
        }

        /// <summary>
        /// Converts a Polygon to line segments (exterior ring + interior holes)
        /// </summary>
        private static List<Line> PolygonToLines(BAMCIS.GeoJSON.Polygon polygon)
        {
            if (polygon == null || polygon.Coordinates == null || !polygon.Coordinates.Any())
                return null;

            var lines = new List<Line>();

            // Process all rings (exterior + holes)
            foreach (var ring in polygon.Coordinates)
            {
                var ringLines = LinearRingToLines(ring);
                if (ringLines != null && ringLines.Count > 0)
                {
                    lines.AddRange(ringLines);
                }
            }

            return lines.Count > 0 ? lines : null;
        }

        /// <summary>
        /// Converts a LinearRing (closed loop) to line segments
        /// </summary>
        private static List<Line> LinearRingToLines(LinearRing ring)
        {
            if (ring == null || ring.Coordinates == null || ring.Coordinates.Count() < 2)
                return null;

            var points = ring.Coordinates.ToArray();
            var lines = new List<Line>();

            // Connect each point to the next (ring automatically closes)
            for (int i = 1; i < points.Length; i++)
            {
                var segment = SegmentBetween(points[i], points[i - 1]);
                if (segment != null)
                    lines.Add(segment);
            }

            return lines;
        }

        /// <summary>
        /// Converts a MultiLineString to line segments
        /// </summary>
        private static List<Line> MultiLineStringToLines(MultiLineString multiLineString)
        {
            if (multiLineString == null || multiLineString.Coordinates == null || !multiLineString.Coordinates.Any())
                return null;

            var lines = new List<Line>();

            foreach (var lineString in multiLineString.Coordinates)
            {
                var lineStringLines = LineStringToLines(lineString);
                if (lineStringLines != null && lineStringLines.Count > 0)
                {
                    lines.AddRange(lineStringLines);
                }
            }

            return lines.Count > 0 ? lines : null;
        }

        /// <summary>
        /// Converts a MultiPolygon to line segments
        /// </summary>
        private static List<Line> MultiPolygonToLines(BAMCIS.GeoJSON.MultiPolygon multiPolygon)
        {
            if (multiPolygon == null || multiPolygon.Coordinates == null || !multiPolygon.Coordinates.Any())
                return null;

            var lines = new List<Line>();

            foreach (var polygon in multiPolygon.Coordinates)
            {
                var polygonLines = PolygonToLines(polygon);
                if (polygonLines != null && polygonLines.Count > 0)
                {
                    lines.AddRange(polygonLines);
                }
            }

            return lines.Count > 0 ? lines : null;
        }
    }
}
