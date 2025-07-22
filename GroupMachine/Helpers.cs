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

using System.Text.RegularExpressions;
using static GroupMachine.Program;
using IniParser;
using IniParser.Model;

namespace GroupMachine
{
    public static class Helpers
    {
        private static readonly object _logFileLock = new();  // Lock for thread-safe logging

        /// <summary>
        /// Parses the command line arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void ParseArguments(string[] args)
        {
            if (args.Length == 0)
                DisplayUsage();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                if (arg == "-m" || arg == "--move")
                    copyFiles = false;
                else if (arg == "-r" || arg == "--recursive")
                    scanRecursive = true;
                else if (arg == "-s" || arg == "--simulate")
                    testMode = true;
                else if ((arg == "-d" || arg == "--distance") && i + 1 < args.Length)
                {
                    if (double.TryParse(args[i + 1], out double distance))
                    {
                        distanceThreshold = distance;
                        i++; // Skip next argument as it's the value
                    }
                    else
                        DisplayUsage("Invalid value for distance.");
                }
                else if ((arg == "-t" || arg == "--time") && i + 1 < args.Length)
                {
                    if (double.TryParse(args[i + 1], out double time))
                    {
                        timeThreshold = time;
                        i++; // Skip next argument as it's the value
                    }
                    else
                        DisplayUsage("Invalid value for time.");
                }
                else if ((arg == "-f" || arg == "--format") && i + 1 < args.Length)
                {
                    if (!IsValidDateFormat(args[i + 1]))
                        DisplayUsage($"Invalid date format '{args[i + 1]}'");
                    dateFormat = args[i + 1];
                    i++; // Skip next argument
                }
                else if ((arg == "-a" || arg == "--append") && i + 1 < args.Length)
                {
                    if (!IsValidDateFormat(args[i + 1]))
                        DisplayUsage($"Invalid date format '{args[i + 1]}'");
                    appendFormat = args[i + 1];
                    i++; // Skip next argument
                }

                else if (arg == "-h" || arg == "--help" || arg == "/?")
                    DisplayUsage();
                else if ((arg == "-g" || arg == "--geocode" || arg == "--geonames") && i + 1 < args.Length)
                {
                    geonamesDatabase = args[i + 1];
                    i++; // Skip next argument
                }
                else if ((arg == "-o" || arg == "--output") && i + 1 < args.Length)
                {
                    destinationFolder = args[i + 1];
                    i++; // Skip next argument
                }
                else if (arg == "-nv" || arg == "--no-video" || arg == "--no-videos")
                    noVideos = true;
                else if (arg == "-np" || arg == "--no-photo" || arg == "--no-photos")
                    noPhotos = true;
                else if (arg == "-u" || arg == "--unique-check" || arg == "--unique")
                    doHashCheck = true;
                else if (arg.StartsWith('-'))
                    DisplayUsage($"Unrecognized option '{arg}'.");

                // All other options have been exhausted, so this must be a source folder

                else if (System.IO.Directory.Exists(arg))
                {
                    if (Directory.Exists(arg))
                        sourceFolders.Add(arg);
                    else
                        DisplayUsage($"'{arg}' is not a valid source directory.");
                }
                else
                    DisplayUsage($"Unrecognized argument '{arg}'.");
            }

            // Validate the arguments

            if (noPhotos && noVideos)
                DisplayUsage("Both photos and videos have been excluded. Nothing to do.");

            if (sourceFolders.Count == 0)
                DisplayUsage("No source folders specified. Please provide at least one source folder.");

            foreach (var folder in sourceFolders)
            {
                if (!Directory.Exists(folder))
                    DisplayUsage($"Source folder '{folder}' does not exist.");
                if (folder == destinationFolder)
                    DisplayUsage($"Source folder '{folder}' cannot be the same as the destination folder.");
            }

            if (timeThreshold < 0)
                DisplayUsage("Time threshold cannot be a negative number.");

            if (distanceThreshold < 0)
                DisplayUsage("Distance threshold cannot be a negative number.");

            if (timeThreshold == 0 && distanceThreshold == 0)
                DisplayUsage("At least one of the time or distance thresholds must be greater than zero.");

            if (!string.IsNullOrEmpty(geonamesDatabase) && !File.Exists(geonamesDatabase))
                DisplayUsage($"GeoNames data file '{geonamesDatabase}' does not exist.");

            // Since videos have no location data, we cannot group them by location.
            if (noPhotos && timeThreshold == 0)
                DisplayUsage("It is not possible to group videos by location.");

            // If there is no GeoNames database, we cannot append date formats to album names since
            // we aren't using album names based on location.
            if (string.IsNullOrEmpty(geonamesDatabase) && !string.IsNullOrEmpty(appendFormat))
                DisplayUsage("Cannot append date format to album names without GeoNames data file.");

            // Validations are completed.

            // Set the media label based on the file types being processed
            if (!noPhotos && !noVideos)
                mediaLabel = "photos and videos";
            else if (!noPhotos)
                mediaLabel = "photos";
            else if (!noVideos)
                mediaLabel = "videos";
            else  // Should never happen
            {
                Logger("Eek! Both noPhotos and noVideos are true.", true);
                mediaLabel = "unknown media";
            }

            // Create the destination folder if it doesn't exist
            if (!Directory.Exists(destinationFolder))
            {
                try
                {
                    Logger($"Destination folder '{destinationFolder}' does not exist. Creating it.", true);
                    Directory.CreateDirectory(destinationFolder);
                }
                catch (Exception ex)
                {
                    DisplayUsage($"Could not create destination folder '{destinationFolder}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Displays help information.
        /// </summary>
        public static void DisplayUsage(string errorMessage = "")
        {
            Console.WriteLine($"Usage: {System.Diagnostics.Process.GetCurrentProcess().ProcessName} [options] -o <destination folder> <source folder> [<source folder> ...]\n" +
                                $"Groups photos and videos into albums (folders) based on time & location changes.\n");

            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version {OutputVersion(version)}, copyright © 2025-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Gallery icons created by Freepik - Flaticon (https://www.flaticon.com/free-icons/gallery)\n");

            Console.WriteLine(  "Options:\n" +
                                "  Required:\n" +
                                "    -o, --output <folder>   Destination folder for grouped albums.\n" +
                                "  File selection:\n" +
                                "    -r, --recursive         Include all sub-folders when scanning.\n" +
                                "    -nv, --no-video         Exclude videos from processing.\n" +
                                "    -np, --no-photo         Exclude photos from processing.\n" +
                                "  Grouping logic:\n" +
                                "    -d, --distance          Distance threshold in kilometers (default: 2.0).\n" +
                                "                            Use 0 to disable distance grouping.\n" +
                                "    -t, --time              Time threshold in hours (default: 48.0).\n" +
                                "                            Use 0 to disable time grouping.\n" +
                                "  Album naming:\n" +
                                "    -g, --geocode <file>    Location of the GeoNames data file.\n" +
                                "                            If not specified, album names will use dates.\n" +
                                "    -f, --format            Album folder date format (default: dd MMM yyyy)\n" +
                                "                            Example: \"yyyy-MM-dd\" → 2025-07-15\n" +
                                "    -a, --append            Date format appended to geolocated album names.\n" +
                                "                            Example: \"MMMM YYYY\" → July 2025\n" +
                                "  Duplicate handling:\n" +
                                "    -u, --unique-check      Check for duplicate files in source and destination.\n" +
                                "                            Note: Comparison is based on file content (SHA-256\n" +
                                "                            hashes) and not file names or timestamps.\n" +
                                "  Execution mode:\n" +
                                "    -m, --move              Move files instead of copying them.\n" +
                                "    -s, --simulate          Show actions only, don't move or copy files.\n" +
                                "  Help:\n" +
                                "    /?, -h, --help          Display this help screen.\n\n" +
                               $"Logs are stored in {Path.Combine(appDataPath, "Logs")}");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(-1);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Checks if the provided date format is valid.
        /// </summary>
        /// <param name="format"></param>
        /// <returns>True if the date format is valid</returns>
        static bool IsValidDateFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return false;

            try
            {
                _ = DateTime.Today.ToString(format);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Defines the location for logs and deletes any old log files
        /// </summary>
        public static void InitialiseLogger()
        {
            // Set the path for the application data folder
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GroupMachine");

            // Set the log folder path to be inside the application data folder
            string logFolderPath = Path.Combine(appDataPath, "Logs");

            // Create the folder if it doesn't exist
            Directory.CreateDirectory(logFolderPath);

            // Delete log files older than 14 days
            var logFiles = Directory.GetFiles(logFolderPath, "*.log");
            foreach (var file in logFiles)
            {
                DateTime lastModified = File.GetLastWriteTime(file);
                if ((DateTime.Now - lastModified).TotalDays > 14)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger($"Error deleting log file {file}: {ex.Message}", true);
                    }
                }
            }
        }

        /// <summary>
        /// Writes a message to the log file for debugging.
        /// </summary>
        /// <param name="message">Message to output</param>
        /// <param name="verbose">Verbose output, only for the logs</param>

        public static void Logger(string message, bool verbose = false)
        {
            // Define the path and filename for this log
            string logFile = DateTime.Now.ToString("yyyy-MM-dd");
            logFile = Path.Combine(appDataPath, "Logs", $"log-{logFile}.log");

            // Define the timestamp
            string tsTime = DateTime.Now.ToString("HH:mm:ss");
            string tsDate = DateTime.Now.ToString("yyyy-MM-dd");

            // Write to file
            lock (_logFileLock)
                File.AppendAllText(logFile, $"[{tsDate} {tsTime}] {message}{Environment.NewLine}");

            // If this isn't verbose output for the logfiles, then output to the console
            if (!verbose)
                Console.WriteLine($"[{tsTime}] {message}");
        }

        /// <summary>
        /// Checks if there is a later release of the application on GitHub and notifies the user.
        /// </summary>
        public static void CheckLatestRelease()
        {
            string gitHubRepo = "mrsilver76/groupmachine";
            string iniPath = Path.Combine(appDataPath, "versionCheck.ini");

            var parser = new FileIniDataParser();
            IniData ini = File.Exists(iniPath) ? parser.ReadFile(iniPath) : new IniData();

            if (NeedsCheck(ini, out Version? cachedVersion))
            {
                var latest = TryFetchLatestVersion(gitHubRepo);
                if (latest != null)
                {
                    ini["Version"]["LatestReleaseChecked"] = latest.Value.Timestamp;

                    if (!string.IsNullOrEmpty(latest.Value.Version))
                    {
                        ini["Version"]["LatestReleaseVersion"] = latest.Value.Version;
                        cachedVersion = ParseSemanticVersion(latest.Value.Version);
                    }

                    parser.WriteFile(iniPath, ini); // Always write if we got any response at all
                }
            }

            if (cachedVersion != null && cachedVersion > version)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($" ℹ️ A new version ({OutputVersion(cachedVersion)}) is available!");
                Console.ResetColor();
                Console.WriteLine($" You are using {OutputVersion(version)}");
                Console.WriteLine($"    Get it from https://www.github.com/{gitHubRepo}/");
            }
        }

        /// <summary>
        /// Takes a semantic version string in the format "major.minor.revision" and returns a Version object in
        /// the format "major.minor.0.revision"
        /// </summary>
        /// <param name="versionString"></param>
        /// <returns></returns>
        public static Version? ParseSemanticVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return null;

            var parts = versionString.Split('.');
            if (parts.Length != 3)
                return null;

            if (int.TryParse(parts[0], out int major) &&
                int.TryParse(parts[1], out int minor) &&
                int.TryParse(parts[2], out int revision))
            {
                try
                {
                    return new Version(major, minor, 0, revision);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Compares the last checked date and version in the INI file to determine if a check is needed.
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="cachedVersion"></param>
        /// <returns></returns>
        private static bool NeedsCheck(IniData ini, out Version? cachedVersion)
        {
            cachedVersion = null;

            string dateStr = ini["Version"]["LatestReleaseChecked"];
            string versionStr = ini["Version"]["LatestReleaseVersion"];

            bool hasTimestamp = DateTime.TryParse(dateStr, out DateTime lastChecked);
            bool isExpired = !hasTimestamp || (DateTime.UtcNow - lastChecked.ToUniversalTime()).TotalDays >= 7;

            cachedVersion = ParseSemanticVersion(versionStr);

            return isExpired;
        }

        /// <summary>
        /// Fetches the latest version from the GitHub repo by looking at the releases/latest page.
        /// </summary>
        /// <param name="repo">The name of the repo</param>
        /// <returns>Version and today's date and time</returns>
        private static (string? Version, string Timestamp)? TryFetchLatestVersion(string repo)
        {
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var client = new HttpClient();

            string ua = repo.Replace('/', '.') + "/" + OutputVersion(version);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);

            try
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (!response.IsSuccessStatusCode)
                {
                    // Received response, but it's a client or server error (e.g., 404, 500)
                    return (null, timestamp);  // Still update "last checked"
                }

                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var match = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success)
                {
                    return (null, timestamp);  // Response body not as expected
                }

                string version = match.Groups[1].Value.TrimStart('v', 'V');
                return (version, timestamp);
            }
            catch
            {
                // This means we truly couldn't reach GitHub at all
                return null;
            }
        }

        /// <summary>
        /// Pluralises a string based on the number provided.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="singular"></param>
        /// <param name="plural"></param>
        /// <returns></returns>
        public static string Pluralise(int number, string singular, string plural)
        {
            return number == 1 ? $"{number} {singular}" : $"{number:N0} {plural}";
        }

        /// <summary>
        /// Given a .NET Version object, outputs the version in a semantic version format.
        /// If the build number is greater than 0, it appends `-preX` to the version string.
        /// </summary>
        /// <returns></returns>
        public static string OutputVersion(Version? netVersion)
        {
            if (netVersion == null)
                return "0.0.0";

            // Use major.minor.revision from version, defaulting patch to 0 if missing
            int major = netVersion.Major;
            int minor = netVersion.Minor;
            int revision = netVersion.Revision >= 0 ? netVersion.Revision : 0;

            // Build the base semantic version string
            string result = $"{major}.{minor}.{revision}";

            // Append `-preX` if build is greater than 0
            if (netVersion.Build > 0)
                result += $"-pre{netVersion.Build}";

            return result;
        }
    }
}