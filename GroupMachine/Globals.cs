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

using IniParser;
using IniParser.Model;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace GroupMachine
{
    internal sealed class Globals
    {
        /// <summary>
        /// Class containing metadata for a single image or video file.
        /// </summary>
        /// <summary>
        /// Metadata describing a photo or video file.
        /// </summary>
        internal sealed class ImageMetadata
        {
            /// <summary>Full path to the media file.</summary>
            public string FileName { get; set; } = string.Empty;

            /// <summary>Date and time the media was created (taken or recorded).</summary>
            public DateTime DateCreated { get; set; }

            /// <summary>Latitude of the location where the media was captured (if available).</summary>
            public double Latitude { get; set; }

            /// <summary>Longitude of the location where the media was captured (if available).</summary>
            public double Longitude { get; set; }

            /// <summary>Unique identifier for the album this media belongs to.</summary>
            public int AlbumID { get; set; }

            /// <summary>Name of the album (folder) this media belongs to.</summary>
            public string AlbumName { get; set; } = string.Empty;

            /// <summary>Part number of the album, used when splitting albums into multiple groups.</summary>
            public int Part { get; set; }

            /// <summary>Human-readable location name (via GeoNames lookup if available).</summary>
            public string? LocationName { get; set; }
        }

        /// <summary>
        /// Defines how files are copied or linked to the destination folder.
        /// </summary>
        internal enum CopyMode
        {
            /// <summary>Default value, should not be used.</summary>
            Unknown,

            /// <summary>Move files to the destination folder.</summary>
            Move,

            /// <summary>Copy files to the destination folder.</summary>
            Copy,

            /// <summary>Create hard or soft links in the destination folder.</summary>
            HardSoftLink
        }

        /// <summary>
        /// Defines the hashing algorithm used for duplicate detection.
        /// </summary>
        internal enum HashMode
        {
            /// <summary>Use CRC64, which is fast but less reliable for duplicate detection.</summary>
            CRC,
            /// <summary>Use MD5, which is a good balance of speed and reliability for duplicate detection.</summary>
            MD5,
            /// <summary>Use SHA256 (32-bit) or SHA512 (64-bit), which offers cryptographic strength but is slower.</summary>
            SHA
        }

        #region User defined settings
        /// <summary>List of source folders to scan for media.</summary>
        public static List<string> SourceFolders { get; set; } = [];

        /// <summary>Destination folder for processed media.</summary>
        public static string DestinationFolder { get; set; } = "";

        /// <summary>Current file copy mode.</summary>
        public static CopyMode CurrentCopyMode { get; set; } = CopyMode.Unknown;

        /// <summary>Maximum distance in km for grouping media into the same album.</summary>
        public static double DistanceThreshold { get; set; } = 50.0;

        /// <summary>Maximum time in hours for grouping media into the same album.</summary>
        public static double TimeThreshold { get; set; } = 48.0;

        /// <summary>If true, scans subfolders of each source folder.</summary>
        public static bool ScanRecursive { get; set; }

        /// <summary>If true, runs in test mode without making file changes.</summary>
        public static bool TestMode { get; set; }

        /// <summary>Date format used when naming albums.</summary>
        public static string DateFormat { get; set; } = "dd MMM yyyy";

        /// <summary>Path to the GeoNames database.</summary>
        public static string GeonamesDatabase { get; set; } = "";

        /// <summary>Additional formatting string appended to album names.</summary>
        public static string AppendFormat { get; set; } = "";

        /// <summary>If true, excludes videos from processing.</summary>
        public static bool NoVideos { get; set; } 

        /// <summary>If true, excludes photos from processing.</summary>
        public static bool NoPhotos { get; set; }

        /// <summary>If true, uses date ranges when naming albums.</summary>
        public static bool UseDateRange { get; set; } = true;

        /// <summary>If true, appends part numbers for albums with the same name.</summary>
        public static bool UsePartNumbers { get; set; } = true;

        /// <summary>If true, uses precise geolocation for album names.</summary>
        public static bool UsePreciseLocation { get; set; }

        /// <summary>Include only media taken on or after this date.</summary>
        public static DateTime? DateTakenFrom { get; set; }

        /// <summary>Include only media taken before this date.</summary>
        public static DateTime? DateTakenTo { get; set; }

        /// <summary>If true, excludes recently taken files from processing.</summary>
        public static bool ExcludeRecent { get; set; }

        /// <summary>Prefix for album folder names, can include date tokens.</summary>
        public static string AlbumPrefix { get; set; } = "";

        /// <summary>If true, checks for any later version on GitHub</summary>
        public static bool GitHubVersionCheck { get; set; } = true;

        /// <summary>If true, albums will not be assigned a name that conflicts with a folder that already exists</summary>
        public static bool AvoidExistingFolders { get; set; }

        /// <summary> Maximum number of parallel tasks to use when processing files. Set to 1 for single-threaded operation
        /// and 0 to allow the system to decide.</summary>
        public static int MaxParallel { get; set; } = -2;  // Use -2 as default to indicate "not set by user" 

        /// <summary>Type of hashing algorithm to use for duplicate detection.</summary>
        public static HashMode DuplicateCheckMode { get; set; } = HashMode.CRC;

        #endregion

        #region Internal settings
        /// <summary>List containing metadata for all scanned media files.</summary>
        public static List<ImageMetadata> ImageMetadataList { get; set; } = [];

        /// <summary>Version of the running application.</summary>
        public static Version ProgramVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version!;

        /// <summary>Path to the application data folder.</summary>
        public static string AppDataPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GroupMachine");

        /// <summary>GeoNames lookup instance for resolving location names.</summary>
        public static GeoNamesLookup? GeoNamesLookup { get; set; }

        /// <summary>Locks to prevent multiple threads writing to the same destination path simultaneously.</summary>
        public static readonly ConcurrentDictionary<string, object> PathLocks = new();

        /// <summary>
        /// Readable label of the media type being processed (photos, videos, or both).
        /// </summary>
        public static string MediaLabel
        {
            get
            {
                if (!NoPhotos && !NoVideos) return "photos and videos";
                if (!NoPhotos) return "photos";
                if (!NoVideos) return "videos";
                Logger.Write("Eek! Both NoPhotos and NoVideos are true.", true);
                return "unknown media";
            }
        }

        /// <summary>
        /// Readable description of the current copy mode.
        /// </summary>
        public static string CopyModeText
        {
            get
            {
                return CurrentCopyMode switch
                {
                    CopyMode.Move => "Moving",
                    CopyMode.Copy => "Copying",
                    CopyMode.HardSoftLink => "Linking",
                    _ => "Unknown"
                };
            }
        }

        /// <summary>Timestamp of the last processed media file.</summary>
        public static DateTime? LastProcessedTimestamp { get; set; }
        #endregion

        /// <summary>
        /// Loads the last processed timestamp from the settings INI file, if it exists and is valid.
        /// </summary>
        public static void GetLastProcessedTimestamp()
        {
            string iniFile = Path.Combine(Globals.AppDataPath, "settings.ini");
            if (!File.Exists(iniFile))
            {
                Globals.LastProcessedTimestamp = null;
                return;
            }

            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(iniFile);

            string value = data["LastProcessed"]["Timestamp"];

            if (!string.IsNullOrWhiteSpace(value) && DateHelper.TryParseIso8601(value, out var parsed))
            {
                Logger.Write($"Found last processed timestamp in {iniFile}: {parsed}", true);
                Globals.LastProcessedTimestamp = parsed;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Logger.Write($"Warning: Invalid timestamp format in {iniFile}: {value}");

                Globals.LastProcessedTimestamp = null;
            }
        }

        /// <summary>
        /// Saves the last processed timestamp to the settings INI file in ISO 8601 format.
        /// </summary>
        /// <param name="timestamp"></param>
        public static void SaveLastProcessedTimestamp(DateTime timestamp)
        {
            string iniFile = Path.Combine(Globals.AppDataPath, "settings.ini");
            var parser = new FileIniDataParser();

            IniData data = File.Exists(iniFile)
                ? parser.ReadFile(iniFile)
                : new IniData();

            // Always save in unambiguous ISO format
            data["LastProcessed"]["Timestamp"] = timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            parser.WriteFile(iniFile, data);
            Logger.Write($"Saved last processed timestamp to {iniFile}: {timestamp}", true);
            Globals.LastProcessedTimestamp = timestamp;
        }
    }
}
