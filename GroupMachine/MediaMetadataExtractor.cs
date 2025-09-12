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
 * along with this Options.  If not, see <https://www.gnu.org/licenses/>.
*/

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using System.Globalization;

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
        /// Given a list of metadata directories (inside a file), extracts the GPS location data and sets it in the ImageMetadata object.
        /// </summary>
        /// <param name="directories"></param>
        /// <param name="metadata"></param>
        public static void ExtractLocation(IReadOnlyList<MetadataExtractor.Directory> directories, Globals.ImageMetadata metadata)
        {
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            var loc = gps?.GetGeoLocation();

            if (loc != null)
            {
                metadata.Latitude = loc.Latitude;
                metadata.Longitude = loc.Longitude;
            }
            else
            {
                // Assume no location data for videos
                metadata.Latitude = 0;
                metadata.Longitude = 0;
            }
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
        /// Returns whether the file is a media file (photo or video) based on its extension.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static bool IsMediaFile(string filePath)
        {
            string[] validExtensions = [".jpg", ".jpeg", ".mp4", ".mov", ".m4v"];

            string ext = Path.GetExtension(filePath);
            return validExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
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
    }
}
