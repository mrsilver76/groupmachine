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

namespace GroupMachine
{
    /// <summary>
    /// Represents the main entry point for the application, orchestrating the initialization, configuration, and
    /// execution of the media processing workflow.
    /// </summary>
    /// <remarks>This class is responsible for initializing application-wide settings, parsing command-line
    /// arguments, and coordinating the sequence of operations required to process media files. It performs tasks such
    /// as loading configuration data, scanning media folders, building albums, and organizing media into album folders.
    /// Additionally, it handles optional location-based enrichment if a GeoNames database is provided. <para> The
    /// application exits with a non-zero status code if critical operations, such as creating album folders, fail.
    /// </para></remarks>
    internal sealed class Program
    {
        /// <summary>
        /// Main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Initialize logging and parse command line arguments
            Logger.Initialise(Path.Combine(Globals.AppDataPath, "Logs"));
            Logger.Write("GroupMachine started.", true);

            CommandLineParser.ParseArguments(args);

            // Display a header with some setting information
            ConsoleOutput.ShowHeader(args);

            // Get the timestamp of the last processed file
            Globals.GetLastProcessedTimestamp();

            // Load geonames database if specified. Note, you cannot call this after
            // scanning the media as location enrichment is done during the scan.
            if (!string.IsNullOrEmpty(Globals.GeonamesDatabase) && File.Exists(Globals.GeonamesDatabase))
            {
                Globals.GeoNamesLookup = new GeoNamesLookup();
                Globals.GeoNamesLookup.LoadFromFile(Globals.GeonamesDatabase);
            }
            else
            {
                Globals.GeoNamesLookup = null;
                Logger.Write("No GeoNames database found; location-based album naming disabled.", true);
            }

            // Start processing photo and vides
            MediaScanner.ScanFolders();

            // Impute missing/invalid location data
            MediaMetadataExtractor.ImputateMissingLocationData();

            // Build the albums based on the scanned media
            AlbumManager.BuildAlbums();

            // Create the album folders based on the unique album names
            if (!MediaProcessor.CreateAlbumFolders())
            {
                Logger.Write("Failed to create one or more album folders. Exiting.");
                Environment.Exit(1);
            }

            // Move or copy the items to their respective album folders
            MediaProcessor.ProcessMedia();

            // Report that we're finished
            Logger.Write("GroupMachine finished.");

            // Check for updates
            ConsoleOutput.CheckLatestRelease();
            Environment.Exit(0);
        }
    }
}
