/*
 * GroupMachine - Groups photos and videos into albums (folders) based on time & location changes.
 * Copyright (c) 2025 Richard Lawrence
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
        /// on the configured criteria.
        /// </summary>
        /// <remarks>This method searches for media files in the folders specified by <see
        /// cref="Globals.SourceFolders"/>. The search can be recursive or limited to top-level directories,  depending
        /// on the value of <see cref="Globals.ScanRecursive"/>. Metadata is extracted from each file, including date
        /// and location information, and the results are filtered  based on the configured date range (<see
        /// cref="Globals.DateTakenFrom"/> and <see cref="Globals.DateTakenTo"/>). <para> Duplicate files are removed
        /// from the results, and any files without valid location data are counted and reported. The extracted metadata
        /// is stored in  <see cref="Globals.ImageMetadataList"/> for further processing. </para> <para> If no media
        /// files are found or if none match the specified criteria, the application will terminate. </para></remarks>
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
                Logger.Write($"No {Globals.MediaLabel} found in the specified source folders.");
                Environment.Exit(0);
            }

            // Now we have the list of files, extract metadata from each one in parallel

            Logger.Write($"Extracting metadata from {GrammarHelper.Pluralise(allFiles.Count, "file", "files")}...");

            // Setup the progress bar

            ProgressBar.Total = allFiles.Count;
            ProgressBar.Start();
            int completedCount = 0;

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
                    MediaMetadataExtractor.EnrichLocation(metadata);

                    Logger.Write($"Metadata from {Path.GetFileName(filePath)}: Date={metadata.DateCreated}, Location=({metadata.Latitude}, {metadata.Longitude}), Name={metadata.LocationName}", true);
                    concurrentList.Add(metadata);
                }
                catch (Exception ex)
                {
                    Logger.Write($"Error reading metadata from {Path.GetFileName(filePath)}: {ex.Message}", true);
                }
                finally
                {
                    Interlocked.Increment(ref completedCount);
                    ProgressBar.Completed = completedCount;
                }
            });

            ProgressBar.Stop();

            // Copy the concurrent list to a normal list for further processing
            Globals.ImageMetadataList = [.. concurrentList];

            // Sort the list by date taken. This can be done later on, but it's easier to
            // understand what is going on in the logs if done now.
            Globals.ImageMetadataList.Sort((x, y) => x.DateCreated.CompareTo(y.DateCreated));

            // Report how many files were found during the scan
            if (Globals.ImageMetadataList.Count > 0)
                Logger.Write($"Found {GrammarHelper.Pluralise(Globals.ImageMetadataList.Count, "media file", "media files")} to process.");
            else
            {
                Logger.Write($"No {Globals.MediaLabel} found matching the specified criteria. Finishing.");
                Environment.Exit(0);
            }
        }
    }
}