using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BAMCIS.GeoJSON;

namespace DGScope
{
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

        public static VideoMapList GeoJSONFileToMaps(string path)
        {
            var json = File.ReadAllText(path);
            try
            {
                return GeoJSONToMaps(json);
            }
            catch (Exception ex) { System.Windows.Forms.MessageBox.Show("Error with video map " + path + "\r\n" + ex.Message); }
            return null;
        }

        public static VideoMapList GeoJSONToMaps(string json)
        {
            VideoMapList maps = new VideoMapList();
            GeoJsonConfig.IgnoreLatitudeValidation = true;
            GeoJsonConfig.IgnoreLongitudeValidation = true;
            var data = GeoJson.FromJson(json);
            switch (data.Type)
            {
                case GeoJsonType.FeatureCollection:
                    var featureCollection = data as FeatureCollection;

                    // Check if this is a GeometryCollection-based format (multiple maps per file)
                    if (featureCollection.Features.Any(x => x.Geometry != null && x.Geometry.Type == GeoJsonType.GeometryCollection))
                    {
                        // Each feature with GeometryCollection becomes its own map
                        foreach (var feature in featureCollection.Features.Where(x => x.Geometry != null && x.Geometry.Type == GeoJsonType.GeometryCollection))
                        {
                            VideoMap newmap = new VideoMap();
                            var geometryCollection = feature.Geometry as GeometryCollection;

                            // Extract metadata from feature properties
                            if (feature.Properties.ContainsKey("name"))
                                newmap.Name = feature.Properties["name"];
                            if (feature.Properties.ContainsKey("number"))
                                newmap.Number = (int)feature.Properties["number"];
                            if (feature.Properties.ContainsKey("mnemonic"))
                                newmap.Mnemonic = feature.Properties["mnemonic"];
                            if (feature.Properties.ContainsKey("category"))
                                newmap.Category = (MapCategory)(int)feature.Properties["category"];

                            // Process all geometries in the collection
                            foreach (var geometry in geometryCollection.Geometries)
                            {
                                var lines = GeometryToLines(geometry);
                                if (lines != null && lines.Count > 0)
                                {
                                    newmap.Lines.AddRange(lines);
                                }
                            }

                            if (newmap.Lines.Count > 0)
                            {
                                maps.Add(newmap);
                            }
                        }
                    }
                    else
                    {
                        // Standard format: all features combined into a single map
                        VideoMap map = new VideoMap();
                        map.Name = "Imported map - " + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString();

                        foreach (var feature in featureCollection.Features.Where(x => x.Geometry != null))
                        {
                            var lines = GeometryToLines(feature.Geometry);
                            if (lines != null && lines.Count > 0)
                            {
                                map.Lines.AddRange(lines);
                            }
                        }

                        if (map.Lines.Count > 0)
                        {
                            maps.Add(map);
                        }
                    }
                    break;
            }
            return maps;
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

                default:
                    // Point, MultiPoint not supported for line rendering
                    return null;
            }
        }

        private static List<Line> LineStringToLines(LineString lineString)
        {
            if (lineString == null) return null;
            var points = lineString.Coordinates.ToArray();
            var lines = new List<Line>();
            for (int i = 1; i < points.Length; i++)
            {
                var end1 = new GeoPoint(points[i].Latitude, points[i].Longitude);
                var end2 = new GeoPoint(points[i - 1].Latitude, points[i - 1].Longitude);
                lines.Add(new Line(end1, end2));
            }
            return lines;
        }

        /// <summary>
        /// Converts a Polygon to line segments (exterior ring + interior holes)
        /// </summary>
        private static List<Line> PolygonToLines(BAMCIS.GeoJSON.Polygon polygon)
        {
            if (polygon == null || polygon.Coordinates == null || polygon.Coordinates.Count == 0)
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
            if (ring == null || ring.Coordinates == null || ring.Coordinates.Count < 2)
                return null;

            var points = ring.Coordinates.ToArray();
            var lines = new List<Line>();

            // Connect each point to the next (ring automatically closes)
            for (int i = 1; i < points.Length; i++)
            {
                var end1 = new GeoPoint(points[i].Latitude, points[i].Longitude);
                var end2 = new GeoPoint(points[i - 1].Latitude, points[i - 1].Longitude);
                lines.Add(new Line(end1, end2));
            }

            return lines;
        }

        /// <summary>
        /// Converts a MultiLineString to line segments
        /// </summary>
        private static List<Line> MultiLineStringToLines(MultiLineString multiLineString)
        {
            if (multiLineString == null || multiLineString.Coordinates == null || multiLineString.Coordinates.Count == 0)
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
            if (multiPolygon == null || multiPolygon.Coordinates == null || multiPolygon.Coordinates.Count == 0)
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
