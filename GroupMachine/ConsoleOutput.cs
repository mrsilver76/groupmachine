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

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static GroupMachine.Globals;

namespace GroupMachine
{
    internal sealed class ConsoleOutput
    {
        /// <summary>
        /// Displays the usage information for the application, including command line options and version information.
        /// If an error message is provided, it will be displayed and the program will exit with an error status.
        /// </summary>
        /// <param name="errorMessage">Error message to display</param>
        public static void ShowUsage(string errorMessage = "")
        {
            Console.WriteLine($"Usage: {System.Diagnostics.Process.GetCurrentProcess().ProcessName} [options] -o <dest> -m|-c|-l <src> [<src> ...]\n" +
                                $"Groups photos and videos into albums (folders) based on time & location.\n");

            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version {VersionHelper.OutputVersion(Globals.ProgramVersion)}, copyright © 2025-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Gallery icons created by Freepik - Flaticon (https://www.flaticon.com/free-icons/gallery)\n");

            Console.WriteLine(  "Options:\n" +
                                "   -o, --output <folder>       Destination folder for albums (required).\n" +
                                "   -m, --move                  Move files (one of -m/-c/-l required)\n" +
                                "   -c, --copy                  Copy files (one of -m/-c/-l required)\n" +
                                "   -l, --link                  Hard/soft link (one of -m/-c/-l required)\n\n" +
                                "  File selection:\n" +
                                "   -r, --recursive             Include subfolders.\n" +
                                "   -nv, --no-video             Exclude videos.\n" +
                                "   -np, --no-photo             Exclude photos.\n" +
                                "   -df, --date-from <date>     Include files after this date.\n" +
                                "                                ISO 8601: yyyy-mm-dd or yyyy-mm-dd hh:mm:ss\n" +
                                "                                Use \"last\" to continue from previous file.\n" +
                                "   -dt, --date-to <date>       Include files on or before this date.\n" +
                                "                                ISO 8601: yyyy-mm-dd or yyyy-mm-dd hh:mm:ss\n" +
                                "                                Use \"last\" to continue from previous file.\n" +
                                "   -xr, --exclude-recent       Exclude files within the -t threshold from now.\n\n" +
                                "  Grouping logic:\n" +
                                "   -d, --distance              Distance in km (default: 50). 0 disables.\n" +
                                "   -t, --time                  Time in hours (default: 48). 0 disables.\n\n" +
                                "  Album naming:\n" +
                                "   -g, --geocode <file>        GeoNames data file; if missing, dates used.\n" +
                                "   -f, --format                Album date format (default: dd MMM yyyy).\n" +
                                "                                Example: \"yyyy-MM-dd\" → 2025-07-15\n" +
                                "   -p, --precise               Use precise location names (stations, parks).\n" +
                                "   -a, --append                Append date format to geolocated albums.\n" +
                                "                                Example: \"MMMM YYYY\" → July 2025\n" +
                                "   -nr, --no-range             Disable date ranges; album names will use only\n" +
                                "   -nr, --no-range              the first item's date.\n" +
                                "   -np, --no-part              Don't use part numbers; multiple groups on the\n" +
                                "                                same day stay in one album.\n" +
                                "   -pa, --prefix-album <text>  Prefix for album folder names. Use / for\n" +
                                "                                subfolders and <yyyy>, <MM>, <dd> etc. for date.\n" +
                                "   -u, --unique                Ensure album names do not match existing folders\n" +
                                "                                (adds (1), (2), ... if needed)\n\n" +
                                "  Others:\n" +
                                "   -nc, --no-check            Don't check GitHub for later versions.\n" +
                                "   -s, --simulate             Test mode; no files moved, copied, or linked.\n" +
                                "   /?, -h, --help             Show this help.\n\n" +
                                $"Logs are stored in {Path.Combine(Globals.AppDataPath, "Logs")}");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(-1);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Displays the header information for the application, including version, copyright, and grouping mode.
        /// </summary>
        /// 
        public static void ShowHeader(string[] args)
        {
            Console.WriteLine(new string('-', 70));
            WriteLeftRight(
                $"\x1b[1;33mGroupMachine v{VersionHelper.OutputVersion(Globals.ProgramVersion)}\x1b[0m",
                $"Copyright © 2025-{DateTime.Now.Year} Richard Lawrence"
            );
            Console.WriteLine("\x1b[3mGroups photos and videos into albums based on time & location.\x1b[0m");
            WriteLeftRight("GNU GPL v2 or later", "https://github.com/mrsilver76/groupmachine");
            Console.WriteLine(new string('-', 70));

            // Prepare titles + content
            var items = new List<(string Title, string Value)>();

            // Source folders
            if (Globals.SourceFolders.Count == 1)
                items.Add(("Source:", Globals.SourceFolders[0]));
            else if (Globals.SourceFolders.Count > 1)
            {
                items.Add(("Sources:", Globals.SourceFolders[0]));
                foreach (var folder in Globals.SourceFolders.Skip(1))
                    items.Add(("", folder)); // continuation lines
            }

            // Grouping mode
            bool timeEnabled = Globals.TimeThreshold > 0;
            bool distEnabled = Globals.DistanceThreshold > 0;
            if (timeEnabled && distEnabled)
                items.Add(("Grouping mode:",
                    $"Time ({Globals.TimeThreshold:F2} hr) or distance ({Globals.DistanceThreshold:F1} km/{Globals.DistanceThreshold * 0.621371:F2} mi)"));
            else if (timeEnabled)
                items.Add(("Grouping mode:", $"Time ({Globals.TimeThreshold:F2} hr)"));
            else if (distEnabled)
                items.Add(("Grouping mode:", $"Distance ({Globals.DistanceThreshold:F1} km/{Globals.DistanceThreshold * 0.621371:F2} mi)"));

            // Date filter
            if (Globals.DateTakenFrom.HasValue && Globals.DateTakenTo.HasValue)
                items.Add(("Date filter:",
                    $"{Globals.DateTakenFrom.Value:dd MMM yyyy HH:mm:ss} to {Globals.DateTakenTo.Value:dd MMM yyyy HH:mm:ss}"));
            else if (Globals.DateTakenFrom.HasValue)
                items.Add(("Date filter:", $"After {Globals.DateTakenFrom.Value:dd MMM yyyy HH:mm:ss}"));
            else if (Globals.DateTakenTo.HasValue)
                items.Add(("Date filter:", $"Before {Globals.DateTakenTo.Value:dd MMM yyyy HH:mm:ss}"));

            // Album format
            string sampleAlbumName;
            if (!string.IsNullOrEmpty(Globals.GeonamesDatabase))
                sampleAlbumName = "Paris, Le Marais, and Versailles";
            else if (Globals.UseDateRange)
                sampleAlbumName = DateTime.Now.ToString(Globals.DateFormat, CultureInfo.CurrentCulture) + " - " +
                    DateTime.Now.AddDays(3).ToString(Globals.DateFormat, CultureInfo.CurrentCulture);
            else
                sampleAlbumName = DateTime.Now.ToString(Globals.DateFormat, CultureInfo.CurrentCulture);

            if (!string.IsNullOrEmpty(Globals.AlbumPrefix))
                sampleAlbumName = DateHelper.ApplyTemplate(Globals.AlbumPrefix, DateTime.Now, sampleAlbumName);
            if (!string.IsNullOrEmpty(Globals.AppendFormat))
                sampleAlbumName += $" ({DateTime.Now.ToString(Globals.AppendFormat, CultureInfo.CurrentCulture)})";

            items.Add(("Album preview:", $"\u001b[3m\"{sampleAlbumName}\"\u001b[0m"));

            // Copy mode
            string copyMode = Globals.TestMode ? "Simulation (no files copied, moved or linked)"
                : Globals.CurrentCopyMode == CopyMode.Copy ? "Copy"
                : Globals.CurrentCopyMode == CopyMode.Move ? "Move"
                : Globals.CurrentCopyMode == CopyMode.HardSoftLink ? "Link"
                : "Unknown";
            items.Add(("Copy mode:", copyMode));

            // Flags
            if (CommandLineParser.ParsedFlags.Count > 0)
                items.Add(("Other flags:", string.Join(", ", CommandLineParser.ParsedFlags)));

            // Geonames database location
            if (!string.IsNullOrEmpty(Globals.GeonamesDatabase))
                items.Add(("GeoNames dbase:", Globals.GeonamesDatabase));

            // Destination
            items.Add(("Destination:", Globals.DestinationFolder));

            // Find longest title length
            int pad = items.Max(i => i.Title.Length) + 2;

            // Print everything
            foreach (var (title, value) in items)
                Console.WriteLine($"{title.PadRight(pad)}{value}");

            Console.WriteLine(new string('-', 70));
            Console.WriteLine();

            // Log details
            Logger.Write("GroupMachine started.");
            LogEnvironmentInfo(args);

            // Warn if date format may be ambiguous
            if (DateHelper.IsAmbiguousDateFormat(Globals.DateFormat))
                Logger.Write($"Warning: The date format '{Globals.DateFormat}' may be too minimal or ambiguous for album names.");
        }

        /// <summary>
        /// Writes two strings, one aligned to the left and the other to the right, within a specified total width.
        /// </summary>
        /// <remarks>If the combined visible length of the <paramref name="left"/> and <paramref
        /// name="right"/> strings  exceeds the <paramref name="totalWidth"/>, the strings are written directly next to
        /// each other  with a single space in between. ANSI escape sequences (e.g., for text formatting) are ignored 
        /// when calculating the visible length of the strings.</remarks>
        /// <param name="left">The string to be displayed on the left side of the output.</param>
        /// <param name="right">The string to be displayed on the right side of the output.</param>
        /// <param name="totalWidth">The total width of the output, including both strings and any padding between them.  Defaults to 70 if not
        /// specified.</param>
        public static void WriteLeftRight(string left, string right, int totalWidth = 70)
        {
            // Regex to remove ANSI escape sequences
            string ansiRegex = @"\x1B\[[0-9;]*m";

            int visibleLeftLength = Regex.Replace(left, ansiRegex, "").Length;
            int visibleRightLength = Regex.Replace(right, ansiRegex, "").Length;

            if (visibleLeftLength + visibleRightLength >= totalWidth)
                Console.WriteLine(left + " " + right);
            else
                Console.WriteLine(left + new string(' ', totalWidth - visibleLeftLength - visibleRightLength) + right);
        }

        /// <summary>
        /// Output to the logs the environment information, such as .NET version, OS and architecture.
        /// Also includes the parsed command line arguments if any were provided.
        /// </summary>
        /// <param name="args"></param>
        private static void LogEnvironmentInfo(string[] args)
        {
            var dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();

            var archName = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            Logger.Write($"Running {VersionHelper.OutputVersion(Globals.ProgramVersion)} on {dotnet} ({os}, {archName})", true);

            if (args.Length > 0)
                Logger.Write($"Parsed arguments: {string.Join(" ", args)}", true);
        }

        /// <summary>
        /// Checks if there is a later release of the application on GitHub and notifies the user.
        /// </summary>
        public static void CheckLatestRelease()
        {
            // Skip if disabled
            if (Globals.GitHubVersionCheck == false)
                return;

            var result = GitHubVersionChecker.CheckLatestRelease(Globals.ProgramVersion, "mrsilver76/groupmachine", Path.Combine(Globals.AppDataPath, "versionCheck.ini"));

            if (result.UpdateAvailable)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  ℹ️ A new version ({result.LatestVersion}) is available!");
                Console.ResetColor();
                Console.WriteLine($" You are using {result.CurrentVersion}");
                Console.WriteLine($"     Get it from https://www.github.com/{result.Repo}/");
            }
        }
    }
}
