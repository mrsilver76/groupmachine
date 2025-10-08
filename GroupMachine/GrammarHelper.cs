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

namespace GroupMachine
{
    /// <summary>
    /// Provides utility methods for handling grammar-related tasks, such as pluralizing words and formatting lists with
    /// proper conjunctions.
    /// </summary>
    /// <remarks>This class is designed to assist with common grammar-related operations, such as generating
    /// pluralized strings based on a count and formatting lists into human-readable strings.</remarks>
    internal sealed class GrammarHelper
    {
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
        /// Given a list of items, sanitises them for folder names and formats them into a string with commas
        /// and "and" for the last item.
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public static string FormatListWithAnd(List<string> items)
        {
            var sanitized = items
                .Select(MediaProcessor.SanitizeForFolderName)
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
    }
}
