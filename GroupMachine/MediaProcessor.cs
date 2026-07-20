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

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GroupMachine
{
    /// <summary>
    /// Provides functionality for processing media files, including copying or moving files to destination folders,
    /// creating album folders, and updating folder timestamps.
    /// </summary>
    /// <remarks>The <see cref="MediaProcessor"/> class is designed to handle media file organization tasks,
    /// such as grouping files into album folders based on metadata, ensuring unique file names, and maintaining
    /// consistent folder timestamps. It supports concurrent processing for improved performance and includes fallback
    /// mechanisms for file operations (e.g., soft links when hard links fail).</remarks>
    internal sealed class MediaProcessor
    {
        private static int softLinksCreated;  // Count of soft links created due to hard link failures

        /// <summary>
        /// Processes media by moving or copying them to the destination folder and updates
        /// folder timestamps.
        /// </summary>
        public static void ProcessMedia()
        {
            Logger.Write($"{Globals.CopyModeText} to new albums...");

            ConcurrentDictionary<string, DateTime> albumDates = new();
            int success = 0, failure = 0;

            // Set up the progress bar
            ProgressBar.Total = Globals.TotalFileBytesToProcess;
            ProgressBar.Start();

            // Use Parallel.ForEach to process images concurrently.
            Parallel.ForEach(Globals.ImageMetadataList, new ParallelOptions { MaxDegreeOfParallelism = Globals.MaxParallel }, imageMetadata =>
            {
                string albumPath = Path.Combine(Globals.DestinationFolder, imageMetadata.AlbumName);
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

                // Update the progress bar with the size of the processed file
                long fileSize = new FileInfo(imageMetadata.FileName).Length;
                ProgressBar.Completed += fileSize;

            });

            ProgressBar.Stop();

            if (softLinksCreated > 0)
                Logger.Write($"Note: {GrammarHelper.Pluralise(softLinksCreated, "hard link", "hard links")} could not be created, reverted to soft links instead.", true);

            Logger.Write($"Processed {GrammarHelper.Pluralise(success, "files", "files")} with {GrammarHelper.Pluralise(failure, "failure", "failures")}.");

            Logger.Write($"Setting album folder dates to match {Globals.MediaLabel}...");
            foreach (var album in albumDates)
            {
                try
                {
                    System.IO.Directory.SetCreationTime(album.Key, album.Value);
                    System.IO.Directory.SetLastWriteTime(album.Key, album.Value);
                }
                catch (Exception ex)
                {
                    Logger.Write($"ERROR: Failed to set date for album folder '{album.Key}': {ex.Message}");
                }
            }

            // Save the last processed timestamp
            if (Globals.ImageMetadataList.Count > 0)
            {
                var last = Globals.ImageMetadataList[^1];
                // Only save if it's newer than the existing timestamp
                if (Globals.LastProcessedTimestamp < last.DateCreated)
                    Globals.SaveLastProcessedTimestamp(last.DateCreated);
            }
        }

        /// <summary>
        /// Creates album folders based on the unique album names extracted from the metadata.
        /// </summary>
        /// <returns></returns>
        public static bool CreateAlbumFolders()
        {
            // Never allow this to happen if we are in test mode
            if (Globals.CurrentOperationMode == Globals.OperationMode.Simulation)
                return true;

            // Get unique album names so that folders can be created for them

            var albumPaths = Globals.ImageMetadataList
                .Select(meta => meta.AlbumName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .Select(name => Path.Combine(Globals.DestinationFolder, name))
                .ToList();

            foreach (var albumPath in albumPaths)
            {
                if (!System.IO.Directory.Exists(albumPath))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(albumPath);
                        Logger.Write($"Created album folder: {albumPath}", true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Write($"ERROR: Failed to create album folder '{albumPath}': {ex.Message}");
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
        private static bool CopyOrMoveFileWithUniqueName(string sourceFilePath, string destinationFilePath)
        {
            // Never allow this to happen if we are in test mode
            if (Globals.CurrentOperationMode == Globals.OperationMode.Simulation)
                return true;

            string directory = Path.GetDirectoryName(destinationFilePath) ?? ".";
            string name = Path.GetFileNameWithoutExtension(destinationFilePath);
            string ext = Path.GetExtension(destinationFilePath);

            int count = 1;
            string candidatePath = destinationFilePath;

            object dirLock = Globals.PathLocks.GetOrAdd(directory.ToLowerInvariant(), _ => new object());

            lock (dirLock)
            {
                while (File.Exists(candidatePath))
                {
                    if (Hashing.FilesAreEqual(sourceFilePath, candidatePath))
                    {
                        // Same file already exists, skip
                        Logger.Write($"Identical file already exists at {Path.GetRelativePath(Path.GetDirectoryName(sourceFilePath) ?? ".", candidatePath)}", true);
                        return true;
                    }

                    // Generate next numbered name
                    candidatePath = Path.Combine(directory, $"{name} ({count++}){ext}");
                }

                Logger.Write($"{Globals.CopyModeText} {sourceFilePath} -> {Path.GetRelativePath(Path.GetDirectoryName(sourceFilePath) ?? ".", candidatePath)}", true);

                try
                {
                    switch (Globals.CurrentOperationMode)
                    {
                        case Globals.OperationMode.Copy:
                            File.Copy(sourceFilePath, candidatePath);
                            break;
                        case Globals.OperationMode.Move:
                            File.Move(sourceFilePath, candidatePath);
                            break;
                        case Globals.OperationMode.HardSoftLink:
                            CreateHardLinkCrossPlatform(sourceFilePath, candidatePath);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write($"Error {Globals.CopyModeText.ToLower(CultureInfo.CurrentCulture)} file: {ex.Message}");
                    return false;
                }
            }

            return true;
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
        private static void CreateHardLinkCrossPlatform(string source, string destination)
        {
            try
            {
                // Resolve to absolute paths
                var srcFull = Path.GetFullPath(source);
                var dstFull = Path.GetFullPath(destination);

                if (OperatingSystem.IsWindows())
                {
                    if (!NativeMethods.CreateHardLink(dstFull, srcFull, IntPtr.Zero))
                    {
                        var err = Marshal.GetLastWin32Error();
                        // Fallback to symbolic link
                        NativeMethods.CreateSymbolicLink(dstFull, srcFull, 0); // 0 = file link
                        Logger.Write($"Symlink fallback for {srcFull} → {dstFull} due to error {err}", true);
                        softLinksCreated++;
                    }
                }
                else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    if (NativeMethods.Link(srcFull, dstFull) != 0)
                    {
                        // Fallback to symbolic link
                        NativeMethods.Symlink(srcFull, dstFull);
                        Logger.Write($"Symlink fallback for {srcFull} → {dstFull} due to error {Marshal.GetLastPInvokeError()}", true);
                        softLinksCreated++;
                    }
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported OS for hard linking.");
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Exception {ex.GetType().Name}: {ex.Message}", true);
                throw;
            }
        }

        /// <summary>
        /// Output to the console and logs a simulation of the albums that would be created and the files
        /// that would be moved, copied or linked into them, without actually performing any file operations.
        /// </summary>
        public static void LogSimulationPreview()
        {
            var albums = Globals.ImageMetadataList
                .GroupBy(x => x.AlbumName)
                .OrderBy(x => x.Key)
                .ToList();

            Logger.Write("-------------- Simulation preview: albums and files --------------");
            Logger.Write($"Found {GrammarHelper.Pluralise(Globals.ImageMetadataList.Count, "file", "files")} grouped into {GrammarHelper.Pluralise(albums.Count, "album", "albums")}.");

            foreach (var album in albums)
            {
                Logger.Write("");
                Logger.Write($"Album: \"{album.Key}\" ({GrammarHelper.Pluralise(album.Count(), "file", "files")})");

                foreach (var file in album)
                {
                    //string destination = Path.Combine(album.Key, Path.GetFileName(file.FileName));
                    Logger.Write($"  {file.FileName} → {Path.GetFileName(file.FileName)}");
                }
            }
            Logger.Write("-------------- Simulation complete: no changes made --------------");
            Logger.Write($"Full details in: {Logger.LogFilePath}");
        }


    }
}
