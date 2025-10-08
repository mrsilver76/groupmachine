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

using System.Globalization;
using System.Text.RegularExpressions;

namespace GroupMachine
{
    /// <summary>
    /// Provides date parsing and template application functionality.
    /// </summary>
    internal sealed class DateHelper
    {
        static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
         "CON", "PRN", "AUX", "NUL",
         "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
		private static readonly char[] PathSeparator = ['/'];

        /// <summary>
        /// Tries to parse a date string in ISO 8601 format.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryParseIso8601(string input, out DateTime result)
        {
            string[] formats = [ "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-ddTHH:mm:ss" ];

            return DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        /// <summary>
        /// Parses a command-line date argument, handling "last" and ISO 8601 dates.
        /// </summary>
        /// <param name="input">The string from args.</param>
        /// <param name="result">The parsed DateTime, or null.</param>
        /// <returns>True if input is valid ("last" or ISO date); false if invalid format.</returns>
        public static bool TryParseDateArg(string input, out DateTime? result)
        {
            if (string.Equals(input, "last", StringComparison.OrdinalIgnoreCase))
            {
                // Use global Options.LastProcessedTimestamp if defined
                result = Globals.LastProcessedTimestamp;
                return true; // "last" is always considered valid even if timestamp is null
            }

            if (TryParseIso8601(input, out var parsed))
            {
                result = parsed;
                return true;
            }

            // Invalid format
            result = null;
            return false;
        }

        /// <summary>
        /// Checks if the provided date format is valid.
        /// </summary>
        /// <param name="format"></param>
        /// <returns>True if the date format is valid</returns>
        public static bool IsValidDateFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return false;

            try
            {
                _ = DateTime.Today.ToString(format, CultureInfo.CurrentCulture);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the provided date format is ambiguous or too minimal.
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public static bool IsAmbiguousDateFormat(string format)
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
        /// Validates that the template is syntactically and semantically safe.
        /// Throws <see cref="FormatException"/> if invalid.
        /// </summary>
        public static void ValidateTemplate(string template, DateTime sampleDate)
        {
            if (string.IsNullOrWhiteSpace(template))
                ConsoleOutput.ShowUsage("Template cannot be empty.");

            // Split into segments using allowed separators
            var segments = SplitSegments(template);

            foreach (var segment in segments)
            {
                // Expand date placeholders
                string expanded = ExpandDatePlaceholders(segment, sampleDate);

                // Check invalid characters
                if (expanded.IndexOfAny(InvalidFileNameChars) >= 0)
                    ConsoleOutput.ShowUsage($"Template segment '{segment}' expands to '{expanded}' which contains invalid characters.");

                // Check reserved names
                if (ReservedNames.Contains(expanded))
                    ConsoleOutput.ShowUsage($"Template segment '{segment}' expands to reserved name '{expanded}'.");
            }
        }

        /// <summary>
        /// Applies the template to a folder name, producing a safe relative path.
        /// Assumes <see cref="ValidateTemplate"/> has already been called.
        /// </summary>
        public static string ApplyTemplate(string template, DateTime date, string folderName)
        {
			// Split template into segments by folder separator
            var segments = template.Split(PathSeparator, StringSplitOptions.None)
					   .Select(s => ExpandDatePlaceholders(s, date))
					   .ToList();

            // Append the folder name to the last segment
            if (segments.Count == 0)
                segments.Add(folderName);
            else
                segments[^1] += folderName;

            // Combine into a proper path
            string output = Path.Combine([.. segments]);

            Logger.Write($"Applied '{template}' with {date:yyyy-MM-dd} and folder '{folderName}' -> '{output}'", true);
            return output;
        }

        /// <summary>
        /// Given a template string, splits it into segments based on path separators.
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        private static List<string> SplitSegments(string template)
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            char[] splitChars = isWindows ? ['/', '\\'] : ['/'];

            return [.. template.Split(splitChars, StringSplitOptions.None).Where(s => s.Length > 0)];
        }

        /// <summary>
        /// Given a segment, expands any .NET date format placeholders (marked by &lt; and &gt;)
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        private static string ExpandDatePlaceholders(string segment, DateTime date)
        {
            return Regex.Replace(segment, @"<([^>]+)>", m =>
            {
                try
                {
                    return date.ToString(m.Groups[1].Value, CultureInfo.CurrentCulture);
                }
                catch (FormatException ex)
                {
                    // This will cause the program to exit immediately which could be an
                    // issue if this occurs during processing rather than startup. However
                    // since we also check at startup, this should be rare.
                    ConsoleOutput.ShowUsage($"Invalid date format '{m.Groups[1].Value}': {ex}");
                    return string.Empty; // Unreachable but satisfies compiler
                }
            });
        }
    }
}
