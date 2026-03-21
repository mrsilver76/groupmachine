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
 * along with this Options.  If not, see <https://www.gnu.org/licenses/>.
*/

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor.Formats.Xmp;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GroupMachine
{
    /// <summary>
    /// Provides functionality for extracting and enriching metadata from media files, such as dates, locations, and
    /// album names.
    /// </summary>
    /// <remarks>The <see cref="MediaMetadataExtractor"/> class includes methods for extracting metadata like
    /// creation dates and GPS locations from media files, as well as generating album names based on location data. It
    /// supports both photo and video files and handles fallback scenarios when metadata is incomplete or
    /// unavailable.</remarks>
    internal sealed class MediaMetadataExtractor
    {
        /// <summary>
        /// Given a list of metadata directories (inside a file), extracts the date and time the content was created.
        /// If no date is found in the metadata, it falls back to the file's creation or last write time.
        /// </summary>
        /// <param name="directories"></param>
        /// <param name="filePath"></param>
        /// <param name="metadata"></param>
        public static void ExtractDate(IReadOnlyList<MetadataExtractor.Directory> directories, string filePath, Globals.ImageMetadata metadata)
        {
            DateTime? dateTime = null;

            // Try EXIF metadata for photos (JPEG)

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime exifDateTime) == true)
            {
                dateTime = exifDateTime;
            }
            else
            {
                // Try QuickTime metadata for videos (MP4/MOV)

                // Look for TagCreated
                var qtMovie = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (qtMovie != null && qtMovie.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out DateTime dtMovie))
                {
                    dateTime = dtMovie;
                }

                // Look for TagCreationDate
                if (!dateTime.HasValue)
                {
                    var qtMeta = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
                    if (qtMeta != null && qtMeta.TryGetDateTime(QuickTimeMetadataHeaderDirectory.TagCreationDate, out DateTime dtMeta))
                    {
                        dateTime = dtMeta;
                    }

                }

                // Give up and use file creation or last write time
                if (!dateTime.HasValue)
                    dateTime = GetFallbackTimestamp(filePath);
            }

            // If we have a valid date, set it in the metadata

            if (dateTime.HasValue)
                metadata.DateCreated = dateTime.Value;
        }

        /// <summary>
        /// Attempts to extract geographic latitude and longitude information from the provided metadata directories and
        /// assigns the values to the specified image metadata object.
        /// </summary>
        /// <remarks>The method prioritizes GPS data from EXIF metadata, but will also scan for ISO 6709
        /// and other latitude/longitude patterns if GPS data is unavailable. If no valid location data is found, the
        /// latitude and longitude properties of the metadata object are set to 0. This method does not throw exceptions
        /// for missing or invalid location data.</remarks>
        /// <param name="directories">A read-only list of metadata directories to search for location information. Typically contains EXIF, GPS,
        /// and other metadata extracted from an image.</param>
        /// <param name="metadata">The image metadata object to which the extracted latitude and longitude values will be assigned. Cannot be
        /// null.</param>
        /// 
        public static void ExtractLocation(IReadOnlyList<MetadataExtractor.Directory> directories, Globals.ImageMetadata metadata)
        {
            metadata.Latitude = 0;
            metadata.Longitude = 0;

            // Get EXIF GPS

            var gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gpsDirectory != null)
            {
                var location = gpsDirectory.GetGeoLocation();
                if (location != null && !location.IsZero)
                {
                    if (IsValidLatitudeLongitude(location.Latitude, location.Longitude))
                    {
                        metadata.Latitude = location.Latitude;
                        metadata.Longitude = location.Longitude;
                        return;
                    }
                }
            }

            // Get XMP GPS (on Android, some iOS)

            var xmpDirectory = directories.OfType<XmpDirectory>().FirstOrDefault();
            if (xmpDirectory != null)
            {
                var xmp = xmpDirectory.XmpMeta;
                if (xmp != null)
                {
                    var lat = xmp.GetPropertyString("http://ns.adobe.com/exif/1.0/", "GPSLatitude");
                    var lon = xmp.GetPropertyString("http://ns.adobe.com/exif/1.0/", "GPSLongitude");

                    if (!string.IsNullOrWhiteSpace(lat) && !string.IsNullOrWhiteSpace(lon))
                    {
                        if (TryParseLatLonString($"{lat}, {lon}", out var parsedLat, out var parsedLon))
                        {
                            if (IsValidLatitudeLongitude(parsedLat, parsedLon))
                            {
                                metadata.Latitude = parsedLat;
                                metadata.Longitude = parsedLon;
                                return;
                            }
                        }
                    }

                    // Some vendors store combined position
                    var position = xmp.GetPropertyString("http://ns.adobe.com/exif/1.0/", "GPSPosition");
                    if (!string.IsNullOrWhiteSpace(position))
                    {
                        if (TryParseLatLonString(position, out var parsedLat, out var parsedLon))
                        {
                            if (IsValidLatitudeLongitude(parsedLat, parsedLon))
                            {
                                metadata.Latitude = parsedLat;
                                metadata.Longitude = parsedLon;
                                return;
                            }
                        }
                    }
                }
            }

            // Scan all tags for ISO 6709 (used by Apple), decimal or DMS

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    var value = directory.GetObject(tag.Type)?.ToString();
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    if (TryParseLatLonString(value, out var parsedLat, out var parsedLon))
                    {
                        if (IsValidLatitudeLongitude(parsedLat, parsedLon))
                        {
                            metadata.Latitude = parsedLat;
                            metadata.Longitude = parsedLon;
                            return;
                        }
                    }
                }
            }

            // Nothing can be found. Technically (0,0) is a valid coordinate in the Gulf of Guinea, but since it's
            // unlikely that anyone will take a photo there, we'll treat it as the value for "no location data".

            metadata.Latitude = 0;
            metadata.Longitude = 0;
        }

        /// <summary>
        /// Attempts to parse a latitude and longitude from a string in various common formats.
        /// </summary>
        /// <remarks>Parsing is case-insensitive for hemisphere indicators. The method supports several
        /// common coordinate notations and ignores extra whitespace. If the input is null, empty, or does not match a
        /// supported format, parsing fails and both latitude and longitude are set to zero. Validation of the latitude and longitude are not performed.</remarks>
        /// <param name="input">The input string containing latitude and longitude information. Supported formats include ISO 6709
        /// (+lat+lon), decimal degrees with hemisphere letters (e.g., "40.6892N 74.0445W"), degrees and decimal minutes
        /// (DM), and degrees-minutes-seconds (DMS).</param>
        /// <param name="latitude">When this method returns, contains the parsed latitude value if parsing succeeded; otherwise, zero.</param>
        /// <param name="longitude">When this method returns, contains the parsed longitude value if parsing succeeded; otherwise, zero.</param>
        /// <returns>true if the input string was successfully parsed into latitude and longitude values; otherwise, false.</returns>
        public static bool TryParseLatLonString(string input, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // Normalize symbols for DMS parsing

            input = input.Replace("°", " deg ")
                         .Replace("º", " deg ")
                         .Replace("'", "'")
                         .Replace("\"", "\"")
                         .Replace("  ", " ");

            // ISO 6709: +lat+lon or -lat-lon

            var iso6709 = Regex.Match(input, @"([+-]\d{1,2}(?:\.\d+)?)\s*,?\s*([+-]\d{1,3}(?:\.\d+)?)");
            if (iso6709.Success)
            {
                if (double.TryParse(iso6709.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) &&
                    double.TryParse(iso6709.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
                    return true;
            }

            // Decimal with hemisphere letters eg: 40.6892N 74.0445W

            var hemiMatch = Regex.Match(input, @"(-?\d{1,2}(?:\.\d+)?)\s*([NS])[, ]+\s*(-?\d{1,3}(?:\.\d+)?)\s*([EW])", RegexOptions.IgnoreCase);
            if (hemiMatch.Success)
            {
                if (double.TryParse(hemiMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) &&
                    double.TryParse(hemiMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
                {
                    if (hemiMatch.Groups[2].Value.Equals("S", StringComparison.OrdinalIgnoreCase)) latitude = -latitude;
                    if (hemiMatch.Groups[4].Value.Equals("W", StringComparison.OrdinalIgnoreCase)) longitude = -longitude;
                    return true;
                }
            }

            // DM style eg: 40,41.355N, 74,2.67E

            var dmMatches = Regex.Matches(input, @"(\d{1,3}),(\d{1,2}(?:\.\d+)?)\s*([NSEW])", RegexOptions.IgnoreCase);
            double? lat = null;
            double? lon = null;
            foreach (Match m in dmMatches)
            {
                if (m.Groups.Count != 4) continue;
                if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var deg)) continue;
                if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var min)) continue;

                var value = deg + (min / 60.0);
                var hemi = m.Groups[3].Value.ToUpper(CultureInfo.CurrentCulture);

                if (hemi == "N" || hemi == "S")
                    lat = (hemi == "S") ? -value : value;
                else if (hemi == "E" || hemi == "W")
                    lon = (hemi == "W") ? -value : value;
            }
            if (lat.HasValue && lon.HasValue)
            {
                latitude = lat.Value;
                longitude = lon.Value;
                return true;
            }

            // Full DMS eg: 40 deg 41' 21.30" N, 74 deg 2' 40.20" E

            var dmsMatches = Regex.Matches(input, @"(\d{1,3})\s*deg\s*(\d{1,2})'\s*(\d{1,2}(?:\.\d+)?)""\s*([NSEW])", RegexOptions.IgnoreCase);
            lat = null;
            lon = null;
            foreach (Match m in dmsMatches)
            {
                if (m.Groups.Count != 5) continue;
                if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var deg)) continue;
                if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var min)) continue;
                if (!double.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)) continue;

                var value = deg + (min / 60.0) + (sec / 3600.0);
                var hemi = m.Groups[4].Value.ToUpper(CultureInfo.CurrentCulture);

                if (hemi == "N" || hemi == "S")
                    lat = (hemi == "S") ? -value : value;
                else if (hemi == "E" || hemi == "W")
                    lon = (hemi == "W") ? -value : value;
            }
            if (lat.HasValue && lon.HasValue)
            {
                latitude = lat.Value;
                longitude = lon.Value;
                return true;
            }

            // Nothing matched
            return false;
        }

        /// <summary>
        /// Given an ImageMetadata object, enriches it with a location name based on the latitude and longitude.
        /// </summary>
        /// <param name="metadata"></param>
        public static void EnrichLocation(Globals.ImageMetadata metadata)
        {
            if (metadata.Latitude != 0 && metadata.Longitude != 0 && Globals.GeoNamesLookup != null)
            {
                try
                {
                    var place = Globals.GeoNamesLookup.FindNearest(metadata.Latitude, metadata.Longitude);
                    metadata.LocationName = place?.Name;
                }
                catch (Exception ex)
                {
                    Logger.Write($"Geo lookup failed for {Path.GetFileName(metadata.FileName)}: {ex.Message}", true);
                }
            }
        }

        /// <summary>
        /// Given a file path, retrieves the fallback timestamp based on the file's creation time or last write time.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static DateTime GetFallbackTimestamp(string filePath)
        {
            var creationTime = File.GetCreationTime(filePath);
            var lastWriteTime = File.GetLastWriteTime(filePath);
            var fallback = creationTime < lastWriteTime ? creationTime : lastWriteTime;

            Logger.Write($"Falling back to {(fallback == creationTime ? "creation time" : "last write time")} in {Path.GetFileName(filePath)}: {fallback}", true);
            return fallback;
        }

        /// <summary>
        /// Returns whether the file is a media file (photo or video) that we are interested in based on its extension.
        /// </summary>
        /// <remarks>This function is not very efficient but hasn't been optimised as the filter lists are small.</remarks>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool IsMediaFile(string filePath)
        {
            string ext = Path.GetExtension(filePath);

            if (!Globals.NoPhotos && (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (!Globals.NoVideos && (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase) || ext.Equals(".m4v", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        /// <summary>
        /// Generates an album name from a list of items, using up to four of the most
        /// frequently occurring location names. Ties in frequency are resolved by
        /// the order of first appearance.
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public static string GenerateAlbumNameFromLocations(List<Globals.ImageMetadata> items)
        {
            const int maxLocations = 4;

            var filteredLocations = items
                .Select(p => p.LocationName?.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            // Count frequencies
            var locationCounts = new Dictionary<string, int>();
            foreach (var loc in filteredLocations)
                if (loc != null)
                    locationCounts[loc] = locationCounts.GetValueOrDefault(loc) + 1;

            // Identify top N frequent locations
            var topLocations = locationCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(maxLocations)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            // Preserve order of first appearance, filtering only top locations
            var orderedTopLocations = new List<string>();
            var seen = new HashSet<string>();

            foreach (var loc in filteredLocations)
            {
                if (loc != null && topLocations.Contains(loc) && seen.Add(loc))
                    orderedTopLocations.Add(loc);
            }

            // If no locations were found, add a default "Unknown Location"
            if (orderedTopLocations.Count == 0)
                orderedTopLocations.Add("Unknown Location");

            // Format the album name with "and" for the last item
            string albumName = GrammarHelper.FormatListWithAnd(orderedTopLocations);

            // Append the date if specified
            if (!string.IsNullOrEmpty(Globals.AppendFormat) && items.Count > 0)
            {
                var firstDate = items[0].DateCreated;
                albumName += $" ({firstDate.ToString(Globals.AppendFormat, CultureInfo.CurrentCulture)})";
            }

            return albumName;
        }

        /// <summary>
        /// Iterates through Globals.ImageMetadataList and imputes missing or invalid GPS data
        /// for items where Latitude and Longitude are zero. Each missing location is filled
        /// from the nearest previous or next item with valid GPS within the time threshold
        /// defined by Globals.TimeThreshold. Items without a nearby anchor remain at (0,0).
        /// </summary>
        public static void ImputateMissingLocationData()
        {
            if (Globals.DistanceThreshold == 0)
                return;  // Distance threshold is zero, so skip imputation

            // Count items with missing/invalid GPS
            int count = Globals.ImageMetadataList.Count(x => x.Latitude == 0 && x.Longitude == 0);
            if (count == 0)
                return;  // No missing/invalid GPS data, nothing to do

            Logger.Write($"Imputating missing/invalid GPS data for {GrammarHelper.Pluralise(count, "file", "files")}...");

            TimeSpan threshold = TimeSpan.FromHours(Globals.TimeThreshold);

            for (int i = 0; i < Globals.ImageMetadataList.Count; i++)
            {
                var curr = Globals.ImageMetadataList[i];

                // Skip if GPS already present
                if (curr.Latitude != 0 || curr.Longitude != 0)
                    continue;

                // Find previous anchor within threshold
                Globals.ImageMetadata? prevAnchor = null;
                for (int j = i - 1; j >= 0; j--)
                {
                    var candidate = Globals.ImageMetadataList[j];
                    if ((candidate.Latitude != 0 || candidate.Longitude != 0) &&
                        (curr.DateCreated - candidate.DateCreated) <= threshold)
                    {
                        prevAnchor = candidate;
                        break;
                    }
                }

                // Find next anchor within threshold
                Globals.ImageMetadata? nextAnchor = null;
                for (int j = i + 1; j < Globals.ImageMetadataList.Count; j++)
                {
                    var candidate = Globals.ImageMetadataList[j];
                    if ((candidate.Latitude != 0 || candidate.Longitude != 0) &&
                        (candidate.DateCreated - curr.DateCreated) <= threshold)
                    {
                        nextAnchor = candidate;
                        break;
                    }
                }

                // No anchor within threshold, so skip imputation
                if (prevAnchor == null && nextAnchor == null)
                    continue;

                // Now lets pick the closest anchor
                Globals.ImageMetadata? chosenAnchor;
                Globals.ImageMetadata? rejectedAnchor = null;
                TimeSpan timeGap = TimeSpan.Zero;

                // If we have both anchors, pick the closest one
                if (prevAnchor != null && nextAnchor != null)
                {
                    var prevDiff = (curr.DateCreated - prevAnchor.DateCreated).Duration();
                    var nextDiff = (nextAnchor.DateCreated - curr.DateCreated).Duration();

                    if (prevDiff <= nextDiff)
                    {
                        chosenAnchor = prevAnchor;
                        rejectedAnchor = nextAnchor;
                        timeGap = prevDiff;
                    }
                    else
                    {
                        chosenAnchor = nextAnchor;
                        rejectedAnchor = prevAnchor;
                        timeGap = nextDiff;
                    }
                }
                else if (prevAnchor != null)  // Only previous anchor available
                {
                    chosenAnchor = prevAnchor;
                    timeGap = (curr.DateCreated - prevAnchor.DateCreated).Duration();
                }
                else if (nextAnchor != null)  // Only next anchor available
                {
                    chosenAnchor = nextAnchor;
                    timeGap = (nextAnchor.DateCreated - curr.DateCreated).Duration();
                }
                else  // Neither previous nor next anchor available, should never happen due to earlier check
                {
                    // This is to satisfy the compiler
                    continue;
                }

                // Copy location data
                curr.Latitude = chosenAnchor.Latitude;
                curr.Longitude = chosenAnchor.Longitude;
                curr.LocationName = chosenAnchor.LocationName;

                // Build log message with hh:mm:ss format
                string logMsg = $"Imputed location for {Path.GetFileName(curr.FileName)}: " +
                                $"Lat={curr.Latitude:F6}, Long={curr.Longitude:F6}, Name={curr.LocationName}, " +
                                $"from {Path.GetFileName(chosenAnchor.FileName)} ({timeGap:hh\\:mm\\:ss})";

                if (rejectedAnchor != null)
                {
                    var rejectedGap = (curr.DateCreated - rejectedAnchor.DateCreated).Duration();
                    logMsg += $", rejected {Path.GetFileName(rejectedAnchor.FileName)}: Name={rejectedAnchor.LocationName} ({rejectedGap:hh\\:mm\\:ss})";
                }

                Logger.Write(logMsg, true);
            }
        }

        /// <summary>
        /// Determines whether the specified latitude and longitude values represent a valid geographic coordinate.
        /// </summary>
        /// <remarks>The coordinate (0, 0) is considered invalid, even though it falls within the valid
        /// latitude and longitude ranges. This method does not check for additional geographic constraints, such as
        /// whether the coordinate corresponds to land or water.</remarks>
        /// <param name="latitude">The latitude component of the coordinate, in degrees. Must be in the range -90 to 90.</param>
        /// <param name="longitude">The longitude component of the coordinate, in degrees. Must be in the range -180 to 180.</param>
        /// <returns>true if both latitude and longitude are within their valid ranges and the coordinate is not (0, 0);
        /// otherwise, false.</returns>
        private static bool IsValidLatitudeLongitude(double latitude, double longitude)
        {
            return latitude >= -90 && latitude <= 90
                    && longitude >= -180 && longitude <= 180
                    && !(latitude == 0 && longitude == 0);
        }
    }
}
