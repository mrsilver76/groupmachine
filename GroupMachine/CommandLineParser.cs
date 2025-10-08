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
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Globalization;

namespace GroupMachine
{
    /// <summary>
    /// Handles parsing of command line arguments.
    /// </summary>
    internal sealed class CommandLineParser
    {
        public static List<string> ParsedFlags = [];

        /// <summary>
        /// Parses the command line arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void ParseArguments(string[] args)
        {
            if (args.Length == 0)
                ConsoleOutput.ShowUsage();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower(CultureInfo.InvariantCulture);

                if (arg == "-m" || arg == "--move")
                    Globals.CurrentCopyMode = Globals.CopyMode.Move;
                else if (arg == "-c" || arg == "--copy")
                    Globals.CurrentCopyMode = Globals.CopyMode.Copy;
                else if (arg == "-l" || arg == "--link")
                    Globals.CurrentCopyMode = Globals.CopyMode.HardSoftLink;
                else if (arg == "-p" || arg == "--precision")
                {
                    if (int.TryParse(args[i+1], out int precision) && precision >= 1 && precision <= 3)
                    {
                        Globals.LocationPrecision = precision;
                        i++; // Skip next argument as it's the value
                    }
                    else
                        ConsoleOutput.ShowUsage("Invalid value for location precision (must be 1,2 or 3)");
                }
                else if (arg == "-r" || arg == "--recursive")
                {
                    Globals.ScanRecursive = true;
                    ParsedFlags.Add("Recursive");
                }
                else if (arg == "-s" || arg == "--simulate")
                    Globals.TestMode = true;
                else if (arg == "-nr" || arg == "--no-range")
                {
                    Globals.UseDateRange = false;
                    ParsedFlags.Add("No-range");
                }
                else if (arg == "-np" || arg == "--no-part" || arg == "--no-parts")
                {
                    Globals.UsePartNumbers = false;
                    ParsedFlags.Add("No-part");
                }
                else if ((arg == "-d" || arg == "--distance") && i + 1 < args.Length)
                {
                    if (double.TryParse(args[i + 1], out double distance))
                    {
                        Globals.DistanceThreshold = distance;
                        i++; // Skip next argument as it's the value
                    }
                    else
                        ConsoleOutput.ShowUsage("Invalid value for distance.");
                }
                else if ((arg == "-t" || arg == "--time") && i + 1 < args.Length)
                {
                    if (double.TryParse(args[i + 1], out double time))
                    {
                        Globals.TimeThreshold = time;
                        i++; // Skip next argument as it's the value
                    }
                    else
                        ConsoleOutput.ShowUsage("Invalid value for time.");
                }
                else if ((arg == "-f" || arg == "--format") && i + 1 < args.Length)
                {
                    if (!DateHelper.IsValidDateFormat(args[i + 1]))
                        ConsoleOutput.ShowUsage($"Invalid date format '{args[i + 1]}'");
                    Globals.DateFormat = args[i + 1];
                    i++; // Skip next argument
                }
                else if ((arg == "-a" || arg == "--append") && i + 1 < args.Length)
                {
                    if (!DateHelper.IsValidDateFormat(args[i + 1]))
                        ConsoleOutput.ShowUsage($"Invalid date format '{args[i + 1]}'");
                    Globals.AppendFormat = args[i + 1];
                    i++; // Skip next argument
                }

                else if (arg == "-h" || arg == "--help" || arg == "/?")
                    ConsoleOutput.ShowUsage();
                else if ((arg == "-g" || arg == "--geocode" || arg == "--geonames") && i + 1 < args.Length)
                {
                    Globals.GeonamesDatabase = args[i + 1];
                    i++; // Skip next argument
                    ParsedFlags.Add("Geocode");
                }
                else if ((arg == "-o" || arg == "--output") && i + 1 < args.Length)
                {
                    Globals.DestinationFolder = args[i + 1];
                    i++; // Skip next argument
                }
                else if (arg == "-nv" || arg == "--no-video" || arg == "--no-videos")
                {
                    Globals.NoVideos = true;
                    ParsedFlags.Add("No-video");
                }
                else if (arg == "-np" || arg == "--no-photo" || arg == "--no-photos")
                {
                    Globals.NoPhotos = true;
                    ParsedFlags.Add("No-photo");
                }
                else if (arg == "-xr" || arg == "--exclude-recent")
                {
                    Globals.ExcludeRecent = true;
                    ParsedFlags.Add("Exclude-recent");
                }
                else if (arg == "-df" || arg == "--date-from" && i + 1 < args.Length)
                {
                    if (DateHelper.TryParseDateArg(args[i + 1], out var parsedDate))
                        Globals.DateTakenFrom = parsedDate;
                    else
                        ConsoleOutput.ShowUsage($"Invalid date-from date format '{args[i + 1]}'.");
                    i++; // Skip next argument
                }
                else if (arg == "-dt" || arg == "--date-to" && i + 1 < args.Length)
                {
                    if (DateHelper.TryParseDateArg(args[i + 1], out var parsedDate))
                        Globals.DateTakenTo = parsedDate;
                    else
                        ConsoleOutput.ShowUsage($"Invalid date-to date format '{args[i + 1]}'.");
                    i++; // Skip next argument
                }
                else if (arg == "-pa" || arg == "--prefix-album" && i + 1 < args.Length)
                {
                    Globals.AlbumPrefix = args[i + 1];
                    i++; // Skip next argument
                }
                else if (arg == "-nc" || arg == "--no-check")
                {
                    Globals.GitHubVersionCheck = false;
                    ParsedFlags.Add("No-check");
                }
                else if (arg == "-u" || arg == "--unique")
                {
                    Globals.AvoidExistingFolders = true;
                    ParsedFlags.Add("Unique");
                }
                else if (arg == "-mp" || arg == "--max-parallel" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int procs))
                    {
                        Globals.MaxParallel = procs;
                        i++; // Skip next argument
                        ParsedFlags.Add($"Max-parallel={procs}");
                    }
                }
                else if (arg == "-ha" || arg == "--hash" || arg =="--hash-algo" || arg == "--hash-algorithm")
                {
                    Globals.DuplicateCheckMode = Hashing.TryParseHashMode(args[i+1]);
                    i++; // Skip next argument
                }
                else if (arg.StartsWith('-'))
                    ConsoleOutput.ShowUsage($"Unrecognized option '{arg}'.");

                // All other options have been exhausted, so this must be a source folder

                else if (System.IO.Directory.Exists(arg))
                {
                    if (Directory.Exists(arg))
                        Globals.SourceFolders.Add(arg);
                    else
                        ConsoleOutput.ShowUsage($"'{arg}' is not a valid source directory.");
                }
                else
                    ConsoleOutput.ShowUsage($"Unrecognized argument '{arg}'.");
            }

            // Validate the arguments
            Validate();

            // Creates the destination folder if it doesn't exist
            CreateDestinationFolder();
        }

        /// <summary>
        /// Validates the parsed command line arguments and shows usage information if any are invalid.
        /// </summary>
        private static void Validate()
        {
            // Check if both photos and videos are excluded

            if (Globals.NoPhotos && Globals.NoVideos)
                ConsoleOutput.ShowUsage("Both photos and videos have been excluded. Nothing to do.");

            // Check that at least one source folder is specified

            if (Globals.SourceFolders.Count == 0)
                ConsoleOutput.ShowUsage("No source folders specified. Please provide at least one source folder.");

            // Check that a copy mode is specified

            if (Globals.CurrentCopyMode == Globals.CopyMode.Unknown)
                ConsoleOutput.ShowUsage("No copy mode specified. Use -m, -c, or -l to specify how files should be handled.");

            // Check that every source folder exists and is not the same as the destination folder

            foreach (var folder in Globals.SourceFolders)
            {
                if (!Directory.Exists(folder))
                    ConsoleOutput.ShowUsage($"Source folder '{folder}' does not exist.");
                if (folder == Globals.DestinationFolder)
                    ConsoleOutput.ShowUsage($"Source folder '{folder}' cannot be the same as the destination folder.");
            }

            // Check that the time and distance thresholds are valid

            if (Globals.TimeThreshold < 0)
                ConsoleOutput.ShowUsage("Time threshold cannot be a negative number.");

            if (Globals.DistanceThreshold < 0)
                ConsoleOutput.ShowUsage("Distance threshold cannot be a negative number.");

            if (Globals.TimeThreshold == 0 && Globals.DistanceThreshold == 0)
                ConsoleOutput.ShowUsage("At least one of the time or distance thresholds must be greater than zero.");

            // Check that the GeoNames database file exists if specified

            if (!string.IsNullOrEmpty(Globals.GeonamesDatabase) && !File.Exists(Globals.GeonamesDatabase))
                ConsoleOutput.ShowUsage($"GeoNames data file '{Globals.GeonamesDatabase}' does not exist.");

            // Check that if the user isn't trying to group only videos by location. Since
            // videos have no location data, this is not possible.

            if (Globals.NoPhotos && Globals.TimeThreshold == 0)
                ConsoleOutput.ShowUsage("It is not possible to group videos by location.");

            // If the user has specified an append format, they must also specify a GeoNames database

            if (string.IsNullOrEmpty(Globals.GeonamesDatabase) && !string.IsNullOrEmpty(Globals.AppendFormat))
                ConsoleOutput.ShowUsage("Cannot append date format to album names without GeoNames data file.");

            // If the user has specified a date range, ensure the 'from' date is before the 'to' date

            if (Globals.DateTakenFrom != null && Globals.DateTakenTo != null &&
                Globals.DateTakenFrom >= Globals.DateTakenTo)
                ConsoleOutput.ShowUsage("The date-from date cannot be later or the same as the date-to date.");

            // If the user has specified a date range, ensure the 'from' date is not in the future

            if (Globals.DateTakenFrom != null && Globals.DateTakenFrom > DateTime.Now)
                ConsoleOutput.ShowUsage("The date-from date cannot be in the future.");

            // If the user has specified an album prefix, ensure it is valid
            if (!String.IsNullOrEmpty(Globals.AlbumPrefix))
                DateHelper.ValidateTemplate(Globals.AlbumPrefix, DateTime.Now);

            // Auto-detect the optimal number of parallel tasks if not specified by the user
            if (Globals.MaxParallel == -2)
            {
                bool isLocal = IsLocalDrive(Globals.DestinationFolder);
                Globals.MaxParallel = CalculateTasks(isLocal);
                Logger.Write($"Destination folder is {(isLocal ? "local" : "network")} drive = {Globals.MaxParallel} parallel tasks.", true);
            }
            else if (Globals.MaxParallel < 1)
            {
                Logger.Write("Invalid value for max parallel tasks. Using all available processors.", true);
                Globals.MaxParallel = -1;
            }
            else
            {
                Logger.Write($"Using user-specified {Globals.MaxParallel} parallel tasks.", true);
            }

            // Calculate the optimal number of parallel tasks if not specified by the user

            // If the user has asked to exclude recently created files, then we need to adjust
            // the date range accordingly. If no date range is specified, we set the 'to' date
            // to the recent threshold. If a date range is specified, we adjust the 'to' date
            // if it is after the recent threshold.

            if (Globals.ExcludeRecent)
            {
                DateTime recentThreshold = DateTime.Now.AddHours(-Globals.TimeThreshold);
                if (Globals.DateTakenTo == null || Globals.DateTakenTo > recentThreshold)
                {
                    Globals.DateTakenTo = recentThreshold;
                    Logger.Write($"Excluding files taken after {recentThreshold:dd MMM yyyy HH:mm:ss} due to --no-recent", true);
                }
            }
        }

        // Some functions which aren't technically command line parsing, but are used by
        // this class to prepare for processing.

        /// <summary>
        /// Creates the destination folder if it doesn't exist.
        /// </summary>
        private static void CreateDestinationFolder()
        {
            if (!Directory.Exists(Globals.DestinationFolder))
            {
                try
                {
                    Logger.Write($"Destination folder '{Globals.DestinationFolder}' does not exist. Creating it.", true);
                    Directory.CreateDirectory(Globals.DestinationFolder);
                }
                catch (Exception ex)
                {
                    ConsoleOutput.ShowUsage($"Could not create destination folder '{Globals.DestinationFolder}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Determines if the specified path is on a local drive (not a network drive).
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool IsLocalDrive(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            try
            {
                string root = Path.GetPathRoot(path) ?? throw new InvalidOperationException();
                var driveInfo = new DriveInfo(root);

                return driveInfo.DriveType != DriveType.Network;
            }
            catch
            {
                // Fallback: treat as network if we can't determine
                return false;
            }
        }

        /// <summary>
        /// Calculates the optimal number of parallel tasks based on whether the path is local or network and
        /// the number of CPU cores.
        /// </summary>
        /// <param name="isLocal"></param>
        /// <returns></returns>
        public static int CalculateTasks(bool isLocal)
        {
            int logicalCores = Environment.ProcessorCount;
            int physicalCores = Math.Max(1, logicalCores / 2); // approximate for hyper-threaded CPUs

            if (isLocal)
            {
                // Local: allow all logical cores
                return logicalCores;
            }
            else
            {
                // Network/slow I/O: limit to physical cores to avoid crashes
                return physicalCores;
            }
        }
    }
}
