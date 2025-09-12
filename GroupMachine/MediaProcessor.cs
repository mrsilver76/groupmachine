﻿/*
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

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

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
            ConcurrentDictionary<string, DateTime> albumDates = new();

            string prefix = Globals.TestMode ? $"Not {Globals.CopyModeText.ToLower(CultureInfo.CurrentCulture)}" : Globals.CopyModeText;
            string msg = $"{prefix} files to new albums{(Globals.TestMode ? " (test mode)" : "")}...";
            Logger.Write(msg);

            int success = 0, failure = 0;

            // Use Parallel.ForEach to process images concurrently
            Parallel.ForEach(Globals.ImageMetadataList, imageMetadata =>
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
            });

            if (softLinksCreated > 0)
                Logger.Write($"Note: {GrammarHelper.Pluralise(softLinksCreated, "hard link", "hard links")} could not be created, reverted to soft links instead.", true);

            Logger.Write($"Processed {GrammarHelper.Pluralise(success, "files", "files")} with {GrammarHelper.Pluralise(failure, "failure", "failures")}.");

            Logger.Write($"Setting album folder dates to match {Globals.MediaLabel}...");
            foreach (var album in albumDates)
            {
                if (Globals.TestMode) continue;

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
                Globals.SaveLastProcessedTimestamp(last.DateCreated);
            }
        }

        /// <summary>
        /// Creates album folders based on the unique album names extracted from the metadata.
        /// </summary>
        /// <returns></returns>
        public static bool CreateAlbumFolders()
        {
            var albumPaths = Globals.ImageMetadataList
                .Select(meta => meta.AlbumName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .Select(name => Path.Combine(Globals.DestinationFolder, name))
                .ToList();

            foreach (var albumPath in albumPaths)
            {
                if (Globals.TestMode)
                {
                    Logger.Write($"[Test] Album folder -> {albumPath}");
                    continue;
                }

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
                    if (FilesAreEqual(sourceFilePath, candidatePath))
                    {
                        // Same file already exists, skip
                        Logger.Write($"Identical file already exists at {Path.GetRelativePath(Path.GetDirectoryName(sourceFilePath) ?? ".", candidatePath)}", true);
                        return true;
                    }

                    // Generate next numbered name
                    candidatePath = Path.Combine(directory, $"{name} ({count++}){ext}");
                }

                Logger.Write($"{Globals.CopyModeText} {sourceFilePath} -> {Path.GetRelativePath(Path.GetDirectoryName(sourceFilePath) ?? ".", candidatePath)}", true);

                if (Globals.TestMode)
                    return true;

                try
                {
                    switch (Globals.CurrentCopyMode)
                    {
                        case Globals.CopyMode.Copy:
                            File.Copy(sourceFilePath, candidatePath);
                            break;
                        case Globals.CopyMode.Move:
                            File.Move(sourceFilePath, candidatePath);
                            break;
                        case Globals.CopyMode.HardSoftLink:
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
        /// Computes the SHA hash of the file at the specified path, selecting the optimal algorithm based on the system
        /// architecture.
        /// </summary>
        /// <param name="path">The path to the file for which the hash is to be computed. Must not be null or empty.</param>
        /// <returns>A byte array containing the computed hash of the file. The hash is computed using SHA-512 on 64-bit
        /// architectures and SHA-256 on other architectures.</returns>
        private static byte[] ComputeOptimalSHAHash(string path)
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
        private static bool FilesAreEqual(string path1, string path2)
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
        private static void CreateHardLinkCrossPlatform(string source, string destination)
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
                        Logger.Write($"Symlink fallback for {source} → {destination} due to error {err}", true);
                        softLinksCreated++;
                    }
                }
                else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    if (NativeMethods.Link(source, destination) != 0)
                    {
                        // Fallback to symbolic link
                        NativeMethods.Symlink(source, destination);
                        Logger.Write($"Symlink fallback for {source} → {destination} due to error {Marshal.GetLastPInvokeError()}", true);
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
