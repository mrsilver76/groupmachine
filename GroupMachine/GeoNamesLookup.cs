/*
 * GroupMachine - Groups photos and videos into albums (folders) based on time & location changes.
 * Copyright (c) 2025 Richard Lawrence
 * http://github.com/mrsilver76/groupmachine/
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *  
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System.Globalization;

namespace GroupMachine
{
    /// <summary>
    /// GeoNamesLookup provides functionality to load and query the GeoNames database for geographic places.
    /// </summary>
    internal sealed class GeoNamesLookup
    {
        private readonly STRtree<Place> _index = new();  // Spatial index for fast lookups
        private static readonly HashSet<string> AdminFeatureCodes = ["ADM3", "ADM4"];  // Administrative features we care about
        private static readonly GeometryFactory _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);  // WGS84 coordinate system

        /// <summary>
        /// Represents a geographic place with coordinates, population, and feature info.
        /// </summary>
        internal sealed record Place
        {
            /// <summary>Name of the place (e.g. "Eiffel Tower")</summary>
            public string? Name { get; init; }

            /// <summary>Latitude in decimal degrees</summary>
            public double Latitude { get; init; }

            /// <summary>Longitude in decimal degrees</summary>
            public double Longitude { get; init; }

            /// <summary>Administrative region (e.g. state or province)</summary>
            public string? Admin1 { get; init; }

            /// <summary>Country code (e.g. "FR" for France)</summary>
            public string? Country { get; init; }

            /// <summary>Population of the place, if available</summary>
            public int Population { get; init; }

            /// <summary>Feature class (e.g. "P" for populated place, "S" for spot feature)</summary>
            public string? FeatureClass { get; init; }

            /// <summary>Feature code (e.g. "S.CSTL" for castle, "P.PPL" for populated place)</summary>
            public string? FeatureCode { get; init; }

            /// <summary>Geometric point representation of the place location</summary>
            public NetTopologySuite.Geometries.Point? Location { get; init; }
        }

        /// <summary>
        /// A set of feature codes that are considered relevant for tourist spots and would typcally be used in the name of an album or photo collection.
        /// </summary>
        private static readonly HashSet<string> AllowedSpotFeatures =
            [
            // Iconic cultural landmarks
            "S.CSTL",    // castle
	        "S.MNMT",    // monument
	        "S.PAL",     // palace
	        "S.TMPL",    // temple
	        "S.MSQE",    // mosque
	        "S.CH",      // church
	        "S.THTR",    // theatre
	        "S.OPRA",    // opera house

	        // Historical and archaeological
	        "S.HSTS",    // historical site
	        "S.ANS",     // archaeological site
	        "S.RUIN",    // ruin
	        "S.TMB",     // tomb
	        "S.PYRS",    // pyramids

	        // Natural or urban landmarks
	        "S.LTHSE",   // lighthouse
	        "S.TOWR",    // tower
	        "S.ARCH",    // arch
	        "S.CAVE",    // cave
	        "S.PIER",    // pier
	        "S.QUAY",    // quay
	        "S.GDN",     // garden
	        "S.SQR",     // public square

	        // Iconic institutions or activities
	        "S.MUS",     // museum
	        "S.ZOO",     // zoo
	        "S.UNIV",    // university (famous ones)
	        "S.LIBR",    // library (public-facing only, e.g. British Library)
	        "S.STDM",    // stadium
	        "S.RECG",    // golf course
	        "S.MAR",     // marina
	        "S.RSRT",    // resort
	        "S.SPA",     // spa
	        "S.MSSN",    // religious mission (if visible)
	        "S.SHRN",    // shrine
        ];

        /// <summary>
        /// Loads the GeoNames database from a file.
        /// </summary>
        /// <param name="filePath"></param>
        public void LoadFromFile(string filePath)
        {
            Logger.Write($"Loading GeoNames database...");

            var places = File.ReadLines(filePath)
                .AsParallel()
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
                .Select(line =>
                {
                    var parts = line.Split('\t');
                    if (parts.Length < 16)
                        return null;

                    var featureClass = parts[6];
                    var featureCode = parts[7];

                    if (!IsRelevantFeature(featureClass, featureCode))
                        return null;

                    if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                        return null;
                    if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                        return null;

                    return new Place
                    {
                        Name = parts[1],
                        Latitude = lat,
                        Longitude = lon,
                        Admin1 = parts[10],
                        Country = parts[8],
                        Population = int.TryParse(parts[14], out var pop) ? pop : 0,
                        FeatureClass = featureClass,
                        FeatureCode = featureCode,
                        Location = _geometryFactory.CreatePoint(new Coordinate(lon, lat))
                    };
                })
                .Where(p => p != null)
                .ToList();

            Logger.Write($"Inserting {GrammarHelper.Pluralise(places.Count, "place", "places")} into spatial index...");

            int skipped = 0;
            foreach (var place in places)
            {
                if (place is null) continue;
                
                if (place.Location is null)
                {
                    skipped++;
                    Logger.Write($"Skipping place with no location: {place.Name} ({place.FeatureClass}.{place.FeatureCode})", true);
                    continue;
                }
 
                _index.Insert(place.Location.EnvelopeInternal, place);
            }

            Logger.Write("Building spatial index...");
            _index.Build();

            if (skipped > 0)
                Logger.Write($"Spatial index built - {GrammarHelper.Pluralise(skipped, "invalid entry", "invalid entries")} skipped (non-fatal)");
        }

        /// <summary>
        /// Given a feature class and code, determine if it is relevant for our use case. We are primarily
        /// interested in populated places and administrative regions.
        /// </summary>
        /// <param name="fc">Feature class</param>
        /// <param name="fcode">Feature code</param>
        /// <returns></returns>
        /// <remarks>Spot features (that are tourist attractions) is currently disabled as it creates
        /// lots of extremely specific albums (eg. "Eiffel Tower") rather than something a little more
        /// generic such as "Paris". It could be a configurable feature in the future.</remarks>
        private static bool IsRelevantFeature(string fc, string fcode) =>
            fc switch
            {
                "P" => true,
                "S" => Globals.UsePreciseLocation && AllowedSpotFeatures.Contains($"{fc}.{fcode}"),
                "L" => true,
                "A" => AdminFeatureCodes.Contains(fcode),
                _ => false
            };

        /// <summary>
        /// Given a latitude and longitude, find the nearest place in the GeoNames database.
        /// We first try to find a non-admin place within 1 km, and if that fails, we expand the search to 5 km.
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <returns></returns>
        public Place? FindNearest(double latitude, double longitude)
        {
            var point = _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));

            var near = QueryNearby(point, 0.01)
                .Select(p => new
                {
                    Place = p,
                    Distance = GeoUtils.Haversine(latitude, longitude, p.Latitude, p.Longitude)
                })
                .Where(x => x.Place.FeatureClass != "A")
                .OrderBy(x => x.Distance)
                .ToList();

            if (near.Count != 0)
                return near.First().Place;

            var fallback = QueryNearby(point, 0.05)
                .Select(p => new
                {
                    Place = p,
                    Distance = GeoUtils.Haversine(latitude, longitude, p.Latitude, p.Longitude)
                })
                .OrderBy(x => x.Distance)
                .ToList();

            if (fallback.Count != 0)
                return fallback.First().Place;

            // If no place found, then give up and return nothing
            Logger.Write($"No nearby place found for ({latitude}, {longitude}) within 5 km.", true);
            return null;
        }

        /// <summary>
        /// Given a point and a radius in degrees, query the spatial index for places within that radius.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="radiusDegrees"></param>
        /// <returns></returns>
        private List<Place> QueryNearby(Point point, double radiusDegrees)
        {
            var env = new Envelope(
                point.X - radiusDegrees, point.X + radiusDegrees,
                point.Y - radiusDegrees, point.Y + radiusDegrees);

            return [.. _index.Query(env)];
        }
    }

    /// <summary>
    /// Utility class for geographic calculations.
    /// </summary>
    internal static class GeoUtils
    {
        /// <summary>
        /// Given two latitude/longitude pairs, calculate the Haversine distance between them.
        /// </summary>
        /// <param name="lat1"></param>
        /// <param name="lon1"></param>
        /// <param name="lat2"></param>
        /// <param name="lon2"></param>
        /// <returns></returns>
        public static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth radius in km
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        /// <summary>
        /// Converts an angle from degrees to radians.
        /// </summary>
        /// <param name="angle">The angle in degrees to be converted.</param>
        /// <returns>The equivalent angle in radians.</returns>
        private static double ToRad(double angle) => angle * Math.PI / 180.0;
    }
}