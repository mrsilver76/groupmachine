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

using System.Runtime.InteropServices;
using System.Text;

namespace GroupMachine
{
    /// <summary>
    /// Handles console output, including usage information, headers, and version checks.
    /// </summary>
    internal sealed class ConsoleOutput
    {
        /// <summary>
        /// Displays the usage information for the application, including command line options and version information.
        /// If an error message is provided, it will be displayed and the program will exit with an error status.
        /// </summary>
        /// <param name="errorMessage">Error message to display</param>
        public static void ShowUsage(string errorMessage = "")
        {
            Console.WriteLine($"Usage: {System.Diagnostics.Process.GetCurrentProcess().ProcessName} [options] -o <folder> <mode> <source> [<source> ...]\n" +
                                $"Groups photos and videos into albums (folders) based on time & location.\n");

            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version {VersionHelper.OutputVersion(Globals.ProgramVersion)}, copyright © 2025-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Gallery icons created by Freepik - Flaticon (https://www.flaticon.com/free-icons/gallery)\n" +
                                    "https://github.com/mrsilver76/groupmachine\n");

            PrintOptionsWithDescriptions();

            Console.WriteLine($"Logs are stored in {Path.Combine(Globals.AppDataPath, "Logs")}");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(-1);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Outputs the command line options and their descriptions in a formatted manner, grouped by sections.
        /// </summary>
        static void PrintOptionsWithDescriptions()
        {
            // Define sections and their options + descriptions
            var sections = new Dictionary<string, (string option, string description)[]>
            {
                ["Mandatory"] =
                [
                    ("<source>", "One or more source directories to scan for photos and videos. Use multiple directories to process more than one location."),
                    ("-o, --output <folder>", "Destination folder where generated albums will be created. The folder will be created if it does not already exist."),
                ],
                
                ["Mode (exactly one mandatory)"] =
                [
                    ("-m, --move", "Move files into the generated album folders."),
                    ("-c, --copy", "Copy files into the generated album folders."),
                    ("-l, --link", "Create hard or symbolic links into the generated album folders."),
                    ("-s, --simulate", "Run in test mode and output a report. No files will be moved, copied, or linked.")
                ],

                ["File selection"] =
                [
                    ("-r, --recursive", "Include files from subfolders when scanning input directories."),
                    ("-nv, --no-video", "Exclude video files from processing."),
                    ("-np, --no-photo", "Exclude photo files from processing."),
                    ("-df, --date-from <date>", "Only include files taken on or after this date. Supports ISO 8601 dates in the format 'yyyy-MM-dd' or 'yyyy-MM-dd HH:mm:ss'. Use \"last\" to continue from the previous run."),
                    ("-dt, --date-to <date>", "Only include files taken before this date. Supports ISO 8601 dates in the format 'yyyy-MM-dd' or 'yyyy-MM-dd HH:mm:ss'. Use \"last\" to continue from the previous run."),
                    ("-xr, --exclude-recent", "Exclude files where the date taken is within the configured time threshold from the current date.")
                ],

                ["Grouping logic"] =
                [
                    ("-d, --distance <km>", "Maximum distance between files before starting a new album. Default is 10 km. Set to 0 to disable distance-based grouping."),
                    ("-t, --time <hours>", "Maximum time between files before starting a new album. Default is 48 hours. Set to 0 to disable time-based grouping.")
                ],

                ["Album naming"] =
                [
                    ("-g, --geocode <file>", "Use the supplied GeoNames data file to generate location-based album names. If unavailable, albums will use dates only."),
                    ("-f, --format <format>", "Date format used in album names. Default is \"dd MMM yyyy\". Example: \"yyyy-MM-dd\" produces names such as \"2025-07-15\"."),
                    ("-p, --precision <num>", "Location name precision from 1 to 3. Default is 3. 1 uses broad locations such as towns, cities, and districts. 2 includes villages and local areas. 3 includes precise spot features such as landmarks."),
                    ("-a, --append", "Append the specified date format to geolocated album names. Example: \"MMMM yyyy\" appends \"July 2025\"."),
                    ("-nr, --no-range", "Disable date ranges in album names. Albums will use only the first item's date instead of showing a start and end date."),
                    ("-np, --no-part", "Disable part numbers. Multiple groups created on the same day will remain in a single album name rather than adding part suffixes."),
                    ("-pa, --prefix-album <text>", "Add a prefix to album folder names. Use / to create subfolders and placeholders such as <yyyy>, <MM>, and <dd> for date values."),
                    ("-u, --unique", "Ensure album folder names do not match existing folders by adding a numeric suffix such as (1), (2), and so on.")
                ],

                ["Miscellaneous"] =
                [
                    ("-nc, --no-check", "Do not check GitHub for newer versions of GroupMachine."),
                    ("-ha, --hash-algo <type>", "Hash algorithm used for duplicate file detection. Options are crc, md5, and sha. Default is crc."),
                    ("-mp, --max-parallel <num>", $"Maximum number of parallel copy, move, or link operations. Default is {CommandLineParser.CalculateTasks(true)} for local storage and {CommandLineParser.CalculateTasks(false)} for network storage."),
                    ("/?, -h, --help", "Display this help message and exit.")
                ]
            };

            // Determine max line width (at least 50 chars, or console width - 5)
            int maxLineWidth = Math.Max(Console.WindowWidth, 50) - 5;

            // Find max option length across all sections for consistent column width
            int firstColWidth = 0;
            foreach (var section in sections.Values)
                foreach (var (option, _) in section)
                    firstColWidth = Math.Max(firstColWidth, option.Length);
            firstColWidth += 2; // 2-character gap

            // Print each section followed by the options
            foreach (var (header, options) in sections)
            {
                // Section header
                Console.WriteLine(header + ":");

                // Each option + description
                foreach (var (option, description) in options)
                {
                    // Wrap description
                    var wrapped = WrapText(description, maxLineWidth - firstColWidth - 1); // -1 for extra indent
                    bool firstLine = true;

                    foreach (var line in wrapped)
                    {
                        if (firstLine)
                        {
                            // first line: 1-char indent + option + description
                            Console.WriteLine(" " + option.PadRight(firstColWidth) + line);
                            firstLine = false;
                        }
                        else
                        {
                            // wrapped lines: 1-char indent + firstColWidth spaces + 1 space + text
                            Console.WriteLine(new string(' ', firstColWidth + 2) + line);
                        }
                    }
                }

                Console.WriteLine(); // single newline between sections
            }

        }

        /// <summary>
        /// Given a block of text and a maximum line width, yields lines of text wrapped at word boundaries.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxWidth"></param>
        /// <returns></returns>
        private static IEnumerable<string> WrapText(string text, int maxWidth)
        {
            var words = text.Split(' ');
            var line = new StringBuilder();

            foreach (var word in words)
            {
                if (line.Length + word.Length + (line.Length > 0 ? 1 : 0) > maxWidth)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }

            if (line.Length > 0) yield return line.ToString();
        }


        /// <summary>
        /// Show the header information, including program version, copyright, source and destination folders,
        /// grouping mode, operation mode, and optional date filters.
        /// </summary>
        public static void ShowHeader()
        {
            string line = new('─', 70);

            Console.WriteLine(line);
            Console.WriteLine($"GroupMachine {VersionHelper.OutputVersion(Globals.ProgramVersion)}");
            Console.WriteLine($"Copyright © 2025-{DateTime.Now.Year} Richard Lawrence");
            Console.WriteLine("https://github.com/mrsilver76/groupmachine");
            Console.WriteLine();

            var items = new List<(string Title, string Value)>();

            // Source folders
            if (Globals.SourceFolders.Count == 1)
                items.Add(("Source", Globals.SourceFolders[0]));
            else if (Globals.SourceFolders.Count > 1)
            {
                items.Add(("Sources", Globals.SourceFolders[0]));
                foreach (var folder in Globals.SourceFolders.Skip(1))
                    items.Add(("", folder));
            }

            // Destination
            items.Add(("Destination", Globals.DestinationFolder));

            // Grouping mode
            bool timeEnabled = Globals.TimeThreshold > 0;
            bool distEnabled = Globals.DistanceThreshold > 0;

            string grouping = "";

            if (timeEnabled && distEnabled)
                grouping = $"Time ({Globals.TimeThreshold:F0} hr) or distance ({Globals.DistanceThreshold:F0} km / {Globals.DistanceThreshold * 0.621371:F2} mi)";
            else if (timeEnabled)
                grouping = $"Time ({Globals.TimeThreshold:F0} hr)";
            else if (distEnabled)
                grouping = $"Distance ({Globals.DistanceThreshold:F0} km / {Globals.DistanceThreshold * 0.621371:F2} mi)";

            if (!string.IsNullOrEmpty(grouping))
                items.Add(("Grouping", grouping));

            // Operation mode
            string mode = Globals.CurrentOperationMode switch
            {
                Globals.OperationMode.Copy => "Copy",
                Globals.OperationMode.Move => "Move",
                Globals.OperationMode.HardSoftLink => "Link",
                Globals.OperationMode.Simulation => "Simulation",
                _ => "Unknown"
            };

            items.Add(("Operation mode", mode));

            // Optional date filter
            if (Globals.DateTakenFrom.HasValue && Globals.DateTakenTo.HasValue)
                items.Add(("Date filter", $"{Globals.DateTakenFrom.Value:dd MMM yyyy HH:mm:ss} to {Globals.DateTakenTo.Value:dd MMM yyyy HH:mm:ss}"));
            else if (Globals.DateTakenFrom.HasValue)
                items.Add(("Date filter", $"After {Globals.DateTakenFrom.Value:dd MMM yyyy HH:mm:ss}"));
            else if (Globals.DateTakenTo.HasValue)
                items.Add(("Date filter", $"Before {Globals.DateTakenTo.Value:dd MMM yyyy HH:mm:ss}"));

            // Now output the items in a nicely formatted way

            int pad = items.Max(i => i.Title.Length) + 4;

            foreach (var (title, value) in items)
                Console.WriteLine($"{title.PadRight(pad)}{value}");

            Console.WriteLine(line);
            Console.WriteLine();

            LogEnvironmentInfo();

            if (DateHelper.IsAmbiguousDateFormat(Globals.DateFormat))
                Logger.Write($"Warning: The date format '{Globals.DateFormat}' may be too minimal or ambiguous for album names.");
        }

        /// <summary>
        /// Output to the logs the environment information, such as .NET version, OS and architecture.
        /// Also includes the parsed command line arguments if any were provided.
        /// </summary>
        private static void LogEnvironmentInfo()
        {
            var dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();

            var archName = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            Logger.Write($"Running {VersionHelper.OutputVersion(Globals.ProgramVersion)} on {dotnet} ({os}, {archName})", true);

            Logger.Write($"Command line: {Environment.CommandLine}", true);
        }

        /// <summary>
        /// Checks if there is a later release of the application on GitHub and notifies the user.
        /// </summary>
        public static void CheckLatestRelease()
        {
            // Skip if disabled
            if (Globals.GitHubVersionCheck == false)
                return;

            string gitRepo = "mrsilver76/groupmachine";
            var result = GitHubVersionChecker.CheckLatestRelease(Globals.ProgramVersion, gitRepo, Path.Combine(Globals.AppDataPath, "versionCheck.ini"));

            if (result.UpdateAvailable)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  ℹ️ A new version ({VersionHelper.OutputVersion(result.LatestVersion)}) is available!");
                Console.ResetColor();
                Console.WriteLine($" You are using {VersionHelper.OutputVersion(Globals.ProgramVersion)}");
                Console.WriteLine($"     Get it from https://www.github.com/{gitRepo}/");
            }
        }
    }
}
