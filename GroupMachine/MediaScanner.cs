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
using System.Collections.Concurrent;

namespace GroupMachine
{
    /// <summary>
    /// Scans the specified source folders for media files, extracts metadata, and filters the results based on the
    /// configured criteria.
    /// </summary>
    internal sealed class MediaScanner
    {
        /// <summary>
        /// Scans the specified source folders for media files, extracts their metadata, and filters the results based
        /// on the configured criteria. We don't enrich the location data here, as that is done at a later stage after
        /// the geonames database has been loaded.
        /// </summary>
        public static void ScanFolders()
        {
            SearchOption searchOption = Globals.ScanRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = new List<string>();

            foreach (var folder in Globals.SourceFolders)
            {
                Logger.Write($"Searching for {Globals.MediaLabel} in {folder}...");

                try
                {
                    var files = System.IO.Directory.GetFiles(folder, "*.*", searchOption)
                                         .Where(MediaMetadataExtractor.IsMediaFile);
                    allFiles.AddRange(files);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Error accessing {folder}: {ex.Message}", true);
                }
            }

            int beforeCount = allFiles.Count;
            allFiles = new HashSet<string>(allFiles, StringComparer.Ordinal).ToList();
            int afterCount = allFiles.Count;

            if (beforeCount != afterCount)
                Logger.Write($"Removed {GrammarHelper.Pluralise(beforeCount - afterCount, "duplicate file", "duplicate files")} from the search results.", true);

            if (allFiles.Count == 0)
            {
                if (Globals.ScanRecursive)
                    Logger.Write($"No {Globals.MediaLabel} found in the source folders.");
                else
                    Logger.Write($"No {Globals.MediaLabel} found in the source folders. Did you forget -r (--recursive)?");
                Environment.Exit(0);
            }

            // Now we have the list of files, we can work out the total size of the files to be processed,
            // which is used for progress reporting during the copy phase

            Logger.Write($"Calculating total size of files to process...");
            long totalBytes = 0;

            Parallel.ForEach(allFiles, filePath =>
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    Interlocked.Add(ref totalBytes, fileInfo.Length);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Error accessing {filePath} to calculate total size: {ex.Message}", true);
                    // We will continue without the size of the file, which may cause the progress bar to be inaccurate,
                    // but it's better than exiting
                }
            });

            Globals.TotalFileBytesToProcess = totalBytes;

            // Now we have the list of files, extract metadata from each one in parallel

            Logger.Write($"Extracting metadata from {GrammarHelper.Pluralise(allFiles.Count, "file", "files")}...");

            // Setup the progress bar

            ProgressBar.Total = Globals.TotalFileBytesToProcess;
            ProgressBar.Start();
            long completedBytes = 0;

            var concurrentList = new ConcurrentBag<Globals.ImageMetadata>();
           
            Parallel.ForEach(allFiles, filePath =>
            {
                try
                {
                    var directories = ImageMetadataReader.ReadMetadata(filePath);

                    var metadata = new Globals.ImageMetadata { FileName = filePath };

                    MediaMetadataExtractor.ExtractDate(directories, filePath, metadata);

                    if (Globals.DateTakenFrom.HasValue && metadata.DateCreated < Globals.DateTakenFrom.Value)
                        return;
                    if (Globals.DateTakenTo.HasValue && metadata.DateCreated >= Globals.DateTakenTo.Value)
                        return;

                    MediaMetadataExtractor.ExtractLocation(directories, metadata);
                    concurrentList.Add(metadata);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Error reading metadata from {Path.GetFileName(filePath)}: {ex.Message}", true);
                }
                finally
                {
                    // Add the file size to the completed bytes count for the progress bar, even if there was an error
                    long fileSize = new FileInfo(filePath).Length;
                    Interlocked.Add(ref completedBytes, fileSize);
                    ProgressBar.Completed = completedBytes;
                }
            });

            ProgressBar.Stop();

            // Copy the concurrent list to a normal list for further processing
            Globals.ImageMetadataList = [.. concurrentList];

            // Sort the list by date taken. This can be done later on, but it's easier to
            // understand what is going on in the logs if done now.
            Globals.ImageMetadataList.Sort((x, y) => x.DateCreated.CompareTo(y.DateCreated));
        }

        /// <summary>
        /// Enriches the location data for each media file in the ImageMetadataList by looking up
        /// the location name based on the latitude and longitude.
        /// </summary>
        public static void EnrichLocations()
        {
            Logger.Write("Enriching locations...");

            Parallel.ForEach(Globals.ImageMetadataList, metadata =>
            {
                MediaMetadataExtractor.EnrichLocation(metadata);

                Logger.Write(
                    $"Metadata from {Path.GetFileName(metadata.FileName)}: " +
                    $"Date={metadata.DateCreated}, " +
                    $"Location=({metadata.Latitude}, {metadata.Longitude}), " +
                    $"Name={metadata.LocationName}",
                    true);
            });
        }
    }
}