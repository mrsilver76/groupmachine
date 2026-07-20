/*
 * GroupMachine - Groups photos and videos into albums (folders) based on time & location changes.
 * Copyright (c) 2025-2026 Richard Lawrence
 * http://github.com/mrsilver76/groupmachine/
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
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

        private static readonly STRtree<Coordinate> _photoTree = new();  // Spatial index for photo locations to filder out geonames places

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
        }

        // Feature codes for different levels of places. These will be used to prioritise which place to use when multiple are found nearby
        // based on the user defined LocationPrecision value.
        private static readonly HashSet<string> Level1PlaceCodes = new(StringComparer.OrdinalIgnoreCase) { "PPLC", "PPLA", "PPLA2", "PPL" };
        private static readonly HashSet<string> Level1AdminCodes = new(StringComparer.OrdinalIgnoreCase) { "ADM2", "ADM3" };
        private static readonly HashSet<string> Level2AdminCodes = new(StringComparer.OrdinalIgnoreCase) { "ADM3", "ADM4" };
        private static readonly HashSet<string> FallbackAdminCodes = new(StringComparer.OrdinalIgnoreCase) { "PCLI" };

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
        /// A set of feature codes that are considered relevant for hydrographic features, such as seas, gulfs and straits.
        /// </summary>

        private static readonly HashSet<string> AllowedHydrographicFeatures =
        [
            "SEA",       // sea
            "GULF",      // gulf
            "STRAIT"     // strait
        ];

        /// <summary>
        /// Loads a GeoNames database from the specified file and populates the spatial index with relevant places.
        /// </summary>
        /// <remarks>This method reads the specified file line by line, parses the data, and filters out
        /// irrelevant or invalid entries. Only places with valid coordinates and relevant feature classes and codes are
        /// added to the spatial index. The method uses a progress bar to indicate the loading and processing
        /// progress.</remarks>
        /// <param name="filePath">The path to the GeoNames database file. The file must be a tab-delimited text file.</param>
        public void LoadFromFile(string filePath)
        {
            // First build a spatial index of all the photo locations so we can filter out GeoNames places that
            // are too far away from any media content
            BuildPhotoTree();

            Logger.Write("Loading GeoNames database...");

            var fileInfo = new FileInfo(filePath);
            long totalKb = fileInfo.Length / 1024;
            ProgressBar.Total = (int)Math.Min(totalKb, int.MaxValue);
            ProgressBar.Completed = 0;
            ProgressBar.Start();

            int count = 0;
            int total = 0;

            using (var reader = new StreamReader(filePath))
            {
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    long bytesReadKb = reader.BaseStream.Position / 1024;
                    ProgressBar.Completed = (int)Math.Min(bytesReadKb, ProgressBar.Total);

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        continue;

                    var parts = line.Split('\t');
                    if (parts.Length < 16)
                        continue;

                    var featureClass = parts[6];
                    var featureCode = parts[7];

                    // Don't add this to the database if it's not a feature class or code we're interested in
                    if (!IsRelevantFeature(featureClass, featureCode))
                        continue;

                    // Make sure latitude and longitude aren't invalid
                    if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                        continue;
                    if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                        continue;

                    total++;

                    // Don't add this to the database if it's over 100km from any media content
                    if (!IsRelevantPlace(lat, lon))
                        continue;

                    // Add to places database
                    var place = new Place
                    {
                        Name = parts[1],
                        Latitude = lat,
                        Longitude = lon,
                        Admin1 = parts[10],
                        Country = parts[8],
                        Population = int.TryParse(parts[14], out var pop) ? pop : 0,
                        FeatureClass = featureClass,
                        FeatureCode = featureCode
                    };

                    var envelope = new Envelope(lon, lon, lat, lat);
                    _index.Insert(envelope, place);

                    count++;
                }
            }

            ProgressBar.Stop();
            Logger.Write($"Loaded {GrammarHelper.Pluralise(count, "relevant place", "relevant places")} from {GrammarHelper.Pluralise(total, "entry", "entries")}.");

            // If the count is zero then the selected GeoNames database does not cover the locations in the media. There's
            // no point trying to continue because the user will end up with date ranges, which can be achieved by not using
            // a GeoNames database at all. 

            if (count == 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ⚠️ No matching places were found in the GeoNames database!");
                Console.ResetColor();
                Console.WriteLine("      The selected database is probably for the wrong country.");
                Console.WriteLine($"      Try the 'allCountries' database for worldwide coverage.");
                Environment.Exit(-1);
            }

            Logger.Write("Building spatial index...");
            _index.Build();
        }

        /// <summary>
        /// Determines whether the specified feature class and feature code represent a relevant feature based on the
        /// current location precision level.
        /// </summary>
        /// <remarks>The relevance of a feature is determined by the global location precision level,
        /// which affects the inclusion of certain feature classes and codes. For example: <list type="bullet"> <item>
        /// <description>Level 1 precision includes only major places and administrative regions.</description> </item>
        /// <item> <description>Level 2 precision includes places, administrative regions, and additional
        /// features.</description> </item> <item> <description>Level 3 precision includes places, administrative
        /// regions, and selected spot features.</description> </item> </list></remarks>
        /// <param name="fc">The feature class, represented as a single character (e.g., "P" for places, "A" for administrative regions).</param>
        /// <param name="fcode">The feature code, which provides additional specificity within the feature class.</param>
        /// <returns><see langword="true"/> if the feature class and feature code are considered relevant for the current
        /// location precision level; otherwise, <see langword="false"/>.</returns>
        private static bool IsRelevantFeature(string fc, string fcode)
        {
            return Globals.LocationPrecision switch
            {
                1 => fc switch  // Level 1 precision includes only major places and admin regions
                {
                    "P" => Level1PlaceCodes.Contains(fcode),
                    "L" => true,
                    "A" => Level1AdminCodes.Contains(fcode) || FallbackAdminCodes.Contains(fcode),
                    "H" => AllowedHydrographicFeatures.Contains(fcode),
                    _ => false
                },
                2 => fc switch  // Level 2 precision includes places and admin regions
                {
                    "P" => true,
                    "L" => true,
                    "A" => Level2AdminCodes.Contains(fcode) || FallbackAdminCodes.Contains(fcode),
                    "H" => AllowedHydrographicFeatures.Contains(fcode),
                    _ => false
                },
                _ => fc switch  // Level 3 (default) precision includes places, admin regions, and selected spot features
                {
                    "P" => true,
                    "L" => true,
                    "S" => AllowedSpotFeatures.Contains($"{fc}.{fcode}"),
                    "A" => Level2AdminCodes.Contains(fcode) || FallbackAdminCodes.Contains(fcode),
                    "H" => AllowedHydrographicFeatures.Contains(fcode),
                    _ => false
                },
            };
        }

        /// <summary>
        /// Builds a spatial index of photo locations from the global image metadata list. This index is used to filter out
        /// loading GeoNames places that are too far away from any media content. Only images with valid GPS coordinates are
        /// included in the index.
        /// </summary>
        private static void BuildPhotoTree()
        {
            foreach (var image in Globals.ImageMetadataList)
            {
                // Ignore missing/invalid GPS coordinates
                if (image.Latitude == 0 && image.Longitude == 0)
                    continue;

                var coordinate = new Coordinate(image.Longitude, image.Latitude);

                _photoTree.Insert(
                    new Envelope(
                        coordinate.X,
                        coordinate.X,
                        coordinate.Y,
                        coordinate.Y),
                    coordinate);
            }

            Logger.Write($"Inserted {_photoTree.Count} photo locations into spatial index", true);
        }

        /// <summary>
        /// Determines whether a given latitude and longitude are within 1 degree of any photo location in the spatial index.
        /// If they are then the place is considered relevant and will be loaded from the GeoNames database. This helps
        /// to filter out places that are too far away from any media content.
        /// </summary>
        /// <remarks>+/- 1 degree is a rough approximation of 111 km.</remarks>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <returns></returns>
        private static bool IsRelevantPlace(double lat, double lon)
        {
            var env = new Envelope(
                lon - 1.0,
                lon + 1.0,
                lat - 1.0,
                lat + 1.0);

            return _photoTree.Query(env).Any();
        }

        /// <summary>
        /// Finds the most appropriate nearby GeoNames place for the specified coordinates.
        /// The search uses a tiered approach, prioritising visible landmarks, nearby places,
        /// and then broader geographic features when no more specific location can be found.
        /// The fallback prioritises preferred hydrographic features such as seas, gulfs and
        /// straits, followed by administrative regions and populated places.
        /// </summary>
        /// <param name="latitude">The latitude of the location to search.</param>
        /// <param name="longitude">The longitude of the location to search.</param>
        /// <returns>The best matching place, or null if no suitable location is found.</returns>
        public Place? FindNearest(double latitude, double longitude)
        {
            // Note to self: this logic could be optimised to avoid multiple queries and distance
            // calculations, but for now it's clear, works well and (thankfully) is extremely fast
            // at looking up places in the spatial index.

            var point = _geometryFactory.CreatePoint(new Coordinate(longitude, latitude));

            // First try to find interesting landmarks (spot features) within 100 m that are
            // likely to be the subject of a photo or video. If you can see it, then it's
            // probably worth naming the album after it.

            if (Globals.LocationPrecision == 3) // Only level 3 precision loads spot features
            {
                var landmark = QueryNearby(point, 0.001)
                    .Select(p => new
                    {
                        Place = p,
                        Distance = GeoUtils.Haversine(latitude, longitude, p.Latitude, p.Longitude)
                    })
                    .Where(x =>
                        x.Place.FeatureClass == "S" &&
                        AllowedSpotFeatures.Contains($"{x.Place.FeatureClass}.{x.Place.FeatureCode}"))
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();

                if (landmark != null)
                    return landmark.Place;
            }

            // If no landmark was found, look for a nearby local feature within 1 km.
            // Administrative regions, hydrographic features and spot features are
            // deliberately excluded from this search.

            var localFeature = QueryNearby(point, 0.01)
                .Select(p => new
                {
                    Place = p,
                    Distance = GeoUtils.Haversine(latitude, longitude, p.Latitude, p.Longitude)
                })
                .Where(x => x.Place.FeatureClass == "L")
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (localFeature != null)
                return localFeature.Place;

            // If no local feature was found, look for the nearest populated place within 10 km.

            var nearbySettlement = QueryNearby(point, 0.1)
                .Select(p => new
                {
                    Place = p,
                    Distance = GeoUtils.Haversine(latitude, longitude, p.Latitude, p.Longitude)
                })
                .Where(x => x.Place.FeatureClass == "P")
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (nearbySettlement != null)
                return nearbySettlement.Place;

            // As a final fallback, search within 100 km and prefer:
            //   1. Hydrographic features (H)
            //   2. Populated places (P)
            //   3. Administrative regions (A)
            //
            // Within each category, the nearest feature wins.

            var regionalCandidates = QueryNearby(point, 1.0)
                .Select(p => new
                {
                    Place = p,
                    Distance = GeoUtils.Haversine(latitude, longitude, p.Latitude, p.Longitude)
                })
                .OrderBy(x => x.Distance)
                .ToList();

            // Check for the nearest preferred hydrographic feature (such as a sea, gulf or strait)
            // first. This gives a useful album name for offshore photos without selecting a specific
            // feature such as a bay or channel

            var waterFeature = regionalCandidates
                .FirstOrDefault(x => x.Place.FeatureClass == "H");

            if (waterFeature != null)
                return waterFeature.Place;

            // If no hydrographic feature found, check for the nearest administrative region

            var administrativeArea = regionalCandidates
                .FirstOrDefault(x => x.Place.FeatureClass == "A");

            if (administrativeArea != null)
                return administrativeArea.Place;

            // If no administrative region found, check for the nearest populated place

            var populatedPlace = regionalCandidates
                .FirstOrDefault(x => x.Place.FeatureClass == "P");

            if (populatedPlace != null)
                return populatedPlace.Place;

            // Give up if no suitable place was found within 100 km

            Logger.Write($"No nearby place found for ({latitude}, {longitude}) within 100 km.", true);
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