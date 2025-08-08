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

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static GroupMachine.Helpers;
using File = System.IO.File;

namespace GroupMachine
{
    class Program
    {
        // User-defined variables
        public static List<string> sourceFolders = []; // List of source folders to scan for media
        public static string destinationFolder = "";
        public static CopyMode copyMode = CopyMode.Unknown; // Default copy mode is unknown, must be set by user
        public static double distanceThreshold = 50.0; // Default distance in km
        public static double timeThreshold = 48.0; // Default time in hours
        public static bool scanRecursive = false; // Default recursiveness is false, as in this folder only
        public static bool testMode = false;
        public static string dateFormat = "dd MMMM yyyy"; // Default date format for album names
        public static string geonamesDatabase = ""; // Default GeoNames database name
        public static string appendFormat = ""; // Default append format for album names
        public static bool noVideos = false; // Default to including videos in the scan
        public static bool noPhotos = false; // Default to including photos in the scan
        public static bool useDateRange = true; // Default to using a date range for album names
        public static bool usePartNumbers = true; // Default to using part numbers for albums with the same name
        public static bool usePreciseLocation = false; // Default to using precise location for album names

        // Internal variables
        public static List<ImageMetadata> imageMetadataList = [];  // List to hold metadata for each media file
        public static Version version = Assembly.GetExecutingAssembly().GetName().Version!;  // Version of the application
        public static string appDataPath = ""; // Path to the app data folder
        private static GeoNamesLookup? geoNamesLookup;
        public static string mediaLabel = ""; // Label for media files being processed (photos or videos)
        static readonly ConcurrentDictionary<string, object> PathLocks = new();
        public static string copyModeText = ""; // Text representation of the copy mode for output
        public static int softLinksCreated = 0; // Counter for soft links created

        public enum CopyMode
        {
            Unknown,  // Default value, should not be used
            Move,
            Copy,
            HardLink
        }

        public class ImageMetadata
        {
            public string FileName { get; set; } = string.Empty;  // Full path to the image file
            public DateTime DateCreated { get; set; }  // Date and time the photo/video was taken
            public double Latitude { get; set; }  // Latitude of the photo location (video is unlikey to have this)
            public double Longitude { get; set; }  // Longitude of the photo location (video is unlikey to have this)
            public int AlbumID { get; set; }  // Unique ID for the album this photo/video belongs to
            public string AlbumName { get; set; } = string.Empty;  // Name of the album (folder) this photo/video belongs to
            public int Part { get; set; }  // Part number of the album, if applicable (eg. if photos/videos are taken on the same day but at different locations)
            public string? LocationName { get; set; }  // Name of the location based on GeoNames lookup, if available
        }

        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            InitialiseLogger();

            ParseArguments(args);
            Logger($"Using arguments: {string.Join(" ", args)}", true);

            // Display a header with some setting information
            ShowHeader(args);

            // Load geonames database if specified
            if (!string.IsNullOrEmpty(geonamesDatabase) && File.Exists(geonamesDatabase))
            {
                geoNamesLookup = new GeoNamesLookup();
                geoNamesLookup.LoadFromFile(geonamesDatabase);
            }
            else
            {
                geoNamesLookup = null;
                Logger("No GeoNames database found; location-based album naming disabled.", true);
            }

            // Start processing photo and vides
            ScanMedia();

            // Sort the photos by date taken
            SortMediaByDate();

            // Group the photos into albums based on time and distance thresholds
            AssignAlbumIDs();

            // Assign base album names based on the date range of the photos in each album
            AssignBaseAlbumNames();

            // Assign part numbers to albums with the same name, but different AlbumIDs
            AssignAndApplyPartNumbers();

            // Create the album folders based on the unique album names
            if (!CreateAlbumFolders())
            {
                Logger("Failed to create one or more album folders. Exiting.");
                Environment.Exit(1);
            }

            // Move or copy the items to their respective album folders
            ProcessMedia();

            Logger("GroupMachine finished.");
            CheckLatestRelease();
            Environment.Exit(0);
        }

        /// <summary>
        /// Displays the header information for the application, including version, copyright, and grouping mode.
        /// </summary>
        static void ShowHeader(string[] args)
        {
            Console.WriteLine($"GroupMachine v{OutputVersion(version)}, Copyright © 2025-{DateTime.Now.Year} Richard Lawrence");
            Console.WriteLine("Groups photos and videos into albums (folders) based on time & location changes.");
            Console.WriteLine("http://github.com/mrsilver76/groupmachine/");
            Console.WriteLine("This program is free software: you can redistribute it and/or modify");
            Console.WriteLine("it under the terms of the GNU General Public License as published by");
            Console.WriteLine("the Free Software Foundation, either version 2 of the License, or (at");
            Console.WriteLine("your option) any later version.");
            Console.WriteLine();

            Logger("Starting GroupMachine...");

            // Log details about the environment and arguments
            LogEnvironmentInfo(args);

            bool timeEnabled = timeThreshold > 0;
            bool distanceEnabled = distanceThreshold > 0;
            if (timeEnabled && distanceEnabled)
                Logger($"Grouping mode: time or distance.");
            else if (timeEnabled)
                Logger($"Grouping mode: time-based only.");
            else // distanceEnabled only
                Logger($"Grouping mode: location-based only.");

            if (timeEnabled)
                Logger($"Time threshold: {timeThreshold:F2} hours.");
            if (distanceEnabled)
                Logger($"Distance threshold: {distanceThreshold:F2} km ({distanceThreshold * 0.621371:F2} miles)");

            if (testMode)
                Logger("Simulation/test mode: no files will be moved, copied or linked.");
            else if (copyMode == CopyMode.Copy)
                Logger("Files will be copied to the destination folder.");
            else if (copyMode == CopyMode.Move)
                Logger("Files will be moved to the destination folder.");
            else
                Logger("Files will be linked to the destination folder.");

            if (IsAmbiguousDateFormat(dateFormat))
                Logger($"Warning: The date format '{dateFormat}' may be too minimal or ambiguous for album names.");
        }

        /// <summary>
        /// Checks if the provided date format is ambiguous or too minimal.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        static bool IsAmbiguousDateFormat(string format)
        {
            // Strip quotes, whitespace
            string clean = format.Trim().ToLowerInvariant();

            // Heuristic: format is too short or lacks variety
            if (clean.Length <= 3)
                return true;

            // Check for presence of at least two distinct components (d, M, y)
            bool hasDay = clean.Contains('d');
            bool hasMonth = clean.Contains('m');
            bool hasYear = clean.Contains('y');

            int components = Convert.ToInt32(hasDay) + Convert.ToInt32(hasMonth) + Convert.ToInt32(hasYear);

            return components < 2;
        }

        /// <summary>
        /// Returns whether the file is a media file (photo or video) based on its extension.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        static bool IsMediaFile(string filePath)
        {
            string[] validExtensions = { ".jpg", ".jpeg", ".mp4", ".mov", ".m4v" };

            string ext = Path.GetExtension(filePath);
            return validExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Scans all source folders for media files (photos and videos) and extracts metadata.
        /// </summary>
        static void ScanMedia()
        {
            SearchOption searchOption = scanRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = new List<string>();

            foreach (var folder in sourceFolders)
            {
                Logger($"Searching for {mediaLabel} in {folder}...");

                try
                {
                    var files = System.IO.Directory.GetFiles(folder, "*.*", searchOption).Where(IsMediaFile);
                    allFiles.AddRange(files);
                }
                catch (Exception ex)
                {
                    Logger($"Error accessing {folder}: {ex.Message}", true);
                }
            }

            int beforeCount = allFiles.Count;
            allFiles = new HashSet<string>(allFiles, StringComparer.Ordinal).ToList();
            int afterCount = allFiles.Count;

            if (beforeCount != afterCount)
                Logger($"Removed {Pluralise((beforeCount - afterCount), "duplicate file", "duplicate files")} from the search results.", true);

            // If no files were found, exit early
            if (allFiles.Count == 0)
            {
                Logger($"No {mediaLabel} found in the specified source folders.");
                Environment.Exit(0);
            }

            Logger($"Extracting metadata from {Pluralise(allFiles.Count, "file", "files")}...");

            var concurrentList = new ConcurrentBag<ImageMetadata>();

            Parallel.ForEach(allFiles, filePath =>
            {
                try
                {
                    var directories = ImageMetadataReader.ReadMetadata(filePath);

                    var metadata = new ImageMetadata { FileName = filePath };

                    ExtractDate(directories, filePath, metadata);
                    ExtractLocation(directories, metadata);
                    EnrichLocation(metadata);

                    Logger($"Metadata from {Path.GetFileName(filePath)}: Date={metadata.DateCreated}, Location=({metadata.Latitude}, {metadata.Longitude}), Name={metadata.LocationName}", true);
                    concurrentList.Add(metadata);
                }
                catch (Exception ex)
                {
                    Logger($"Error reading metadata from {Path.GetFileName(filePath)}: {ex.Message}", true);
                }
            });

            imageMetadataList = concurrentList.ToList();

            Logger($"Found {Pluralise(imageMetadataList.Count, "media file", "media files")} with valid metadata.");
        }

        /// <summary>
        /// Given a list of metadata directories (inside a file), extracts the date and time the content was created.
        /// If no date is found in the metadata, it falls back to the file's creation or last write time.
        /// </summary>
        /// <param name="directories"></param>
        /// <param name="filePath"></param>
        /// <param name="metadata"></param>
        static void ExtractDate(IReadOnlyList<MetadataExtractor.Directory> directories, string filePath, ImageMetadata metadata)
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
        static void ExtractLocation(IReadOnlyList<MetadataExtractor.Directory> directories, ImageMetadata metadata)
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
        static void EnrichLocation(ImageMetadata metadata)
        {
            if (metadata.Latitude != 0 && metadata.Longitude != 0 && geoNamesLookup != null)
            {
                try
                {
                    var place = geoNamesLookup.FindNearest(metadata.Latitude, metadata.Longitude);
                    metadata.LocationName = place?.Name;
                }
                catch (Exception ex)
                {
                    Logger($"Geo lookup failed for {Path.GetFileName(metadata.FileName)}: {ex.Message}", true);
                }
            }
        }

        /// <summary>
        /// Given a file path, retrieves the fallback timestamp based on the file's creation time or last write time.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        static DateTime GetFallbackTimestamp(string filePath)
        {
            var creationTime = File.GetCreationTime(filePath);
            var lastWriteTime = File.GetLastWriteTime(filePath);
            var fallback = creationTime < lastWriteTime ? creationTime : lastWriteTime;

            Logger($"Falling back to {(fallback == creationTime ? "creation time" : "last write time")} in {Path.GetFileName(filePath)}: {fallback}", true);
            return fallback;
        }

        /// <summary>
        /// Sorts the media by the date the photo or video was taken or created.
        /// </summary>
        static void SortMediaByDate()
        {
            Logger($"Sorting {mediaLabel} by date taken...");
            imageMetadataList.Sort((x, y) => x.DateCreated.CompareTo(y.DateCreated));
        }

        /// <summary>
        /// Assign a unique album ID to each media item. The album ID is incremented when the time or distance threshold is exceeded
        /// between consecutive items.
        /// </summary>
        static void AssignAlbumIDs()
        {
            Logger($"Allocating {mediaLabel} to albums...");

            int currentAlbumID = 1;
            imageMetadataList[0].AlbumID = currentAlbumID;

            for (int i = 1; i < imageMetadataList.Count; i++)
            {
                var prev = imageMetadataList[i - 1];
                var curr = imageMetadataList[i];

                bool thresholdExceeded = false;
                List<string> reasons = [];

                // Check the time difference between the two items
                if (timeThreshold > 0)
                {
                    TimeSpan timeDiff = curr.DateCreated - prev.DateCreated;
                    if (timeDiff.TotalHours >= timeThreshold)
                    {
                        thresholdExceeded = true;
                        reasons.Add($"time ({timeDiff.TotalHours:F2} hrs)");
                    }
                }

                // Check if either item has a valid location. If not, we won't check the distance
                bool hasPrevLocation = prev.Latitude != 0 && prev.Longitude != 0;
                bool hasCurrLocation = curr.Latitude != 0 && curr.Longitude != 0;

                // Check the distance between the two items (this will usually be photos)
                if (distanceThreshold > 0 && hasCurrLocation && hasPrevLocation)
                {
                    double distance = GeoUtils.Haversine(curr.Latitude, curr.Longitude, prev.Latitude, prev.Longitude);
                    if (distance >= distanceThreshold)
                    {
                        thresholdExceeded = true;
                        reasons.Add($"distance ({distance:F2} km)");
                    }
                }

                // If either threshold is exceeded, log the reason and increment the album ID
                if (thresholdExceeded)
                {
                    var prevName = Path.GetFileName(prev.FileName);
                    var currName = Path.GetFileName(curr.FileName);
                    Logger($"Album break: {string.Join(" and ", reasons)} between {prevName} → {currName}", true);
                    currentAlbumID++;
                }
                curr.AlbumID = currentAlbumID;
            }
        }

        /// <summary>
        /// Assigns base album names to items by grouping them based on their album ID.
        /// </summary>
        /// <remarks>This methord groups items by their album ID and assigns a base album name to each
        /// image in the group. The album name is determined depending on the presence of location names:
        /// - If no location names are available, the album name is based on the date range of the items in the group.
        /// - If location names are available, the album name is generated based on the most common locations in the group.
        /// </remarks>
        static void AssignBaseAlbumNames()
        {
            Logger($"Grouping {mediaLabel} by album...");

            var groups = imageMetadataList.GroupBy(img => img.AlbumID);

            Logger("Determining album names...");
            foreach (var group in groups)
            {
                var items = group.ToList();

                if (geoNamesLookup == null || !items.Any(p => !string.IsNullOrEmpty(p.LocationName)))
                {
                    // No geonames DB or no location names: fallback to old date range logic
                    var firstDate = items.First().DateCreated.Date;
                    var lastDate = items.Last().DateCreated.Date;

                    string albumName = firstDate.ToString(dateFormat);

                    // If the first and last dates are different, append the last date to the album name
                    // to make a range - but only do this if useDateRange is true
                    if (firstDate != lastDate && useDateRange)
                        albumName += $" - {lastDate.ToString(dateFormat)}";

                    foreach (var img in items)
                        img.AlbumName = albumName;
                }
                else
                {
                    // Location names present, generate name based on locations
                    string locationBasedName = GenerateAlbumNameFromLocations(items);
                    foreach (var img in items)
                        img.AlbumName = locationBasedName;
                }
            }
        }

        /// <summary>
        /// Generates an album name from a list of items, using up to four of the most
        /// frequently occurring location names. Ties in frequency are resolved by
        /// the order of first appearance.
        /// </summary>
        /// <param name="photos"></param>
        /// <param name="appendFormat"></param>
        /// <returns></returns>

        static string GenerateAlbumNameFromLocations(List<ImageMetadata> items)
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
            string albumName = FormatListWithAnd(orderedTopLocations);

            // Append the date if specified
            if (!string.IsNullOrEmpty(appendFormat) && items.Count > 0)
            {
                var firstDate = items[0].DateCreated;
                albumName += $" ({firstDate.ToString(appendFormat)})";
            }

            return albumName;
        }

        /// <summary>
        /// Given a list of items, sanitises them for folder names and formats them into a string with commas
        /// and "and" for the last item.
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        static string FormatListWithAnd(List<string> items)
        {
            var sanitized = items
                .Select(SanitizeForFolderName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            return sanitized.Count switch
            {
                0 => string.Empty,
                1 => sanitized[0],
                2 => $"{sanitized[0]} and {sanitized[1]}",
                _ => string.Join(", ", sanitized.Take(sanitized.Count - 1)) + ", and " + sanitized.Last()  // Use oxford comma to avoid ambiguity, especially for non-English languages
            };
        }

        /// <summary>
        /// Assigns and applies part numbers to items in the image metadata list based on album identifiers and names.
        /// </summary>
        /// <remarks>This method iterates through the image metadata list and assigns a sequential part
        /// number to any items where the album name is the same but the album ID is different. An example of
        /// this would be photos taken on the same day but where the distance threshold has been exceeded. Once
        /// completed, the album names are updated to reflect the fact</remarks>
        static void AssignAndApplyPartNumbers()
        {
            if (!usePartNumbers)
                return;

            Logger("Assigning part numbers and finalising album names...");

            if (imageMetadataList.Count == 0)
                return; // We shouldn't have got this far without metadata, but just in case

            imageMetadataList[0].Part = 1;
            int part = 1;

            // Assign the part numbers based on AlbumID and AlbumName

            for (int i = 1; i < imageMetadataList.Count; i++)
            {
                var prev = imageMetadataList[i - 1];
                var curr = imageMetadataList[i];

                if (curr.AlbumID != prev.AlbumID)
                {
                    // If the album IDs are different, but the album names are the same,
                    // then this is a different part of the same album
                    part = (curr.AlbumName == prev.AlbumName) ? part + 1 : 1;
                }

                curr.Part = part;
            }

            // Now append "(part n)" to the album names where applicable. We don't
            // want to append "(part 1)", as that is the default album name.

            foreach (var img in imageMetadataList)
                if (img.Part >= 2)
                    img.AlbumName += $" (part {img.Part})";
        }

        /// <summary>
        /// Processes media by moving or copying them to the destination folder and updates
        /// folder timestamps.
        /// </summary>
        static void ProcessMedia()
        {
            ConcurrentDictionary<string, DateTime> albumDates = new();

            string prefix = testMode ? $"Not {copyModeText.ToLower()}" : copyModeText;
            string msg = $"{prefix} files to new albums{(testMode ? " (test mode)" : "")}...";
            Logger(msg);

            int success = 0, failure = 0;

            // Use Parallel.ForEach to process images concurrently
            Parallel.ForEach(imageMetadataList, imageMetadata =>
            {
                string albumPath = Path.Combine(destinationFolder, imageMetadata.AlbumName);
                string destinationFilePath = Path.Combine(albumPath, Path.GetFileName(imageMetadata.FileName));

                // Copy or move the file to the destination folder with a unique name
                bool result = CopyOrMoveFileWithUniqueName(imageMetadata.FileName, destinationFilePath);

                if (result)
                    Interlocked.Increment(ref success);
                else
                    Interlocked.Increment(ref failure);

                // Safely update the album's earliest timestamp
                albumDates.AddOrUpdate(
                    albumPath,
                    imageMetadata.DateCreated,
                    (_, existing) => imageMetadata.DateCreated < existing ? imageMetadata.DateCreated : existing
                );
            });

            if (softLinksCreated > 0)
                Logger($"Note: {Pluralise(softLinksCreated, "hard link", "hard links")} could not be created, reverted to soft links instead.", true);

            Logger($"Processed {Pluralise(success, "files", "files")} with {Pluralise(failure, "failure", "failures")}.");

            Logger($"Setting album folder dates to match {mediaLabel}...");
            foreach (var album in albumDates)
            {
                if (testMode) continue;

                try
                {
                    System.IO.Directory.SetCreationTime(album.Key, album.Value);
                    System.IO.Directory.SetLastWriteTime(album.Key, album.Value);
                }
                catch (Exception ex)
                {
                    Logger($"ERROR: Failed to set date for album folder '{album.Key}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// reates album folders based on the unique album names extracted from the metadata.
        /// </summary>
        /// <returns></returns>
        static bool CreateAlbumFolders()
        {
            var albumPaths = imageMetadataList
                .Select(meta => meta.AlbumName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .Select(name => Path.Combine(destinationFolder, name))
                .ToList();

            foreach (var albumPath in albumPaths)
            {
                if (testMode)
                {
                    Logger($"[Test] Album folder -> {albumPath}");
                    continue;
                }

                if (!System.IO.Directory.Exists(albumPath))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(albumPath);
                        Logger($"Created album folder: {albumPath}", true);
                    }
                    catch (Exception ex)
                    {
                        Logger($"ERROR: Failed to create album folder '{albumPath}': {ex.Message}");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Copies or moves a file to a destination with a unique name if necessary. If a file with the same
        /// name already exists, it checks if the files are identical by comparing their hashes. This
        /// ensures that only non-identical files are copied or moved, and avoids overwriting existing files.
        /// </summary>
        /// <param name="sourceFilePath">The path to the source file.</param>
        /// <param name="destinationFilePath">The path to the destination file.</param>
        /// 
        static bool CopyOrMoveFileWithUniqueName(string sourceFilePath, string destinationFilePath)
        {
            string directory = Path.GetDirectoryName(destinationFilePath) ?? ".";
            string name = Path.GetFileNameWithoutExtension(destinationFilePath);
            string ext = Path.GetExtension(destinationFilePath);

            int count = 1;
            string candidatePath = destinationFilePath;

            object dirLock = PathLocks.GetOrAdd(directory.ToLowerInvariant(), _ => new object());

            lock (dirLock)
            {
                while (File.Exists(candidatePath))
                {
                    if (FilesAreEqual(sourceFilePath, candidatePath))
                    {
                        // Same file already exists, skip
                        Logger($"Identical file already exists at {Path.GetRelativePath(Path.GetDirectoryName(sourceFilePath) ?? ".", candidatePath)}", true);
                        return true;
                    }

                    // Generate next numbered name
                    candidatePath = Path.Combine(directory, $"{name} ({count++}){ext}");
                }

                Logger($"{copyModeText} {sourceFilePath} -> {Path.GetRelativePath(Path.GetDirectoryName(sourceFilePath) ?? ".", candidatePath)}", true);

                if (testMode)
                    return true;

                try
                {
                    switch (copyMode)
                    {
                        case CopyMode.Copy:
                            File.Copy(sourceFilePath, candidatePath);
                            break;
                        case CopyMode.Move:
                            File.Move(sourceFilePath, candidatePath);
                            break;
                        case CopyMode.HardLink:
                            CreateHardLinkCrossPlatform(sourceFilePath, candidatePath);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger($"Error {copyModeText.ToLower()} file: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Computes the SHA hash of the file at the specified path, selecting the optimal algorithm based on the system
        /// architecture.
        /// </summary>
        /// <param name="path">The path to the file for which the hash is to be computed. Must not be null or empty.</param>
        /// <returns>A byte array containing the computed hash of the file. The hash is computed using SHA-512 on 64-bit
        /// architectures and SHA-256 on other architectures.</returns>
        static byte[] ComputeOptimalSHAHash(string path)
        {
            using var stream = File.OpenRead(path);

            // Prefer SHA-512 on 64-bit architectures for better performance
            using HashAlgorithm hashAlgorithm = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 or Architecture.Arm64 => SHA512.Create(),
                _ => SHA256.Create()
            };

            return hashAlgorithm.ComputeHash(stream);
        }

        /// <summary>
        /// Compares the contents of two files to determine if they are identical.
        /// </summary>
        /// <remarks>This method uses a hash-based comparison to determine file equality.</remarks>
        /// <param name="path1">The file path of the first file to compare. Cannot be null or empty.</param>
        /// <param name="path2">The file path of the second file to compare. Cannot be null or empty.</param>
        /// <returns><see langword="true"/> if the contents of the two files are identical; otherwise, <see langword="false"/>.</returns>
        static bool FilesAreEqual(string path1, string path2)
        {
            var hash1 = ComputeOptimalSHAHash(path1);
            var hash2 = ComputeOptimalSHAHash(path2);
            return hash1.SequenceEqual(hash2);
        }

        /// <summary>
        /// Given a string input, sanitises it for use as a folder name by removing invalid characters,
        /// and collapsing whitespace.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string SanitizeForFolderName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = string.Concat(input.Where(c => !invalidChars.Contains(c))).Trim();

            // Collapse whitespace
            while (cleaned.Contains("  "))
                cleaned = cleaned.Replace("  ", " ");

            return cleaned;
        }

        /// <summary>
        /// Creates a hard link between two files, either on Windows or Unix-like systems.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <exception cref="IOException"></exception>
        public static void CreateHardLinkCrossPlatform(string source, string destination)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (!NativeMethods.CreateHardLink(destination, source, IntPtr.Zero))
                    {
                        var err = Marshal.GetLastWin32Error();
                        // Fallback to symbolic link
                        NativeMethods.CreateSymbolicLink(destination, source, 0); // 0 = file link
                        Logger($"Symlink fallback for {source} → {destination} due to error {err}", true);
                        softLinksCreated++;
                    }
                }
                else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    if (NativeMethods.Link(source, destination) != 0)
                    {
                        // Fallback to symbolic link
                        NativeMethods.Symlink(source, destination);
                        Logger($"Symlink fallback for {source} → {destination} due to error {Marshal.GetLastPInvokeError()}", true);
                        softLinksCreated++;
                    }
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported OS for hard linking.");
                }
            }
            catch
            {
                // Optional: swallow or rethrow; swallowing here to match “silently fall back” intent
            }
        }
    }
}
