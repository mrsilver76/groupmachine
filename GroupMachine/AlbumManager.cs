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

namespace GroupMachine
{
    internal sealed class AlbumManager
    {
		/// <summary>
		/// Builds albums from the media metadata list by sorting the media, assigning album IDs,
		/// assigning base album names, and applying part numbers if required.
		/// </summary>
		public static void BuildAlbums()
        {
            // We need to sort the media by date first
            SortMediaByDate();
            // Next, assign album IDs based on the time and distance thresholds
            AssignAlbumIDs();
            // Now we can group the items by album ID and assign base album names
            AssignBaseAlbumNames();
            // Finally, if required, assign part numbers and apply them to the album names
            AssignAndApplyPartNumbers();
		}

		/// <summary>
		/// Assign a unique album ID to each media item. The album ID is incremented when the time or
        /// distance threshold is exceeded between consecutive items.
		/// </summary>
		private static void AssignAlbumIDs()
        {
            Logger.Write($"Allocating {Globals.MediaLabel} to albums...");

            int currentAlbumID = 1;
            Globals.ImageMetadataList[0].AlbumID = currentAlbumID;

            for (int i = 1; i < Globals.ImageMetadataList.Count; i++)
            {
                var prev = Globals.ImageMetadataList[i - 1];
                var curr = Globals.ImageMetadataList[i];

                bool thresholdExceeded = false;
                List<string> reasons = [];

                // Check the time difference between the two items
                if (Globals.TimeThreshold > 0)
                {
                    TimeSpan timeDiff = curr.DateCreated - prev.DateCreated;
                    if (timeDiff.TotalHours >= Globals.TimeThreshold)
                    {
                        thresholdExceeded = true;
                        reasons.Add($"time ({timeDiff.TotalHours:F2} hrs)");
                    }
                }

                // Check if either item has a valid location. If not, we won't check the distance
                bool hasPrevLocation = prev.Latitude != 0 && prev.Longitude != 0;
                bool hasCurrLocation = curr.Latitude != 0 && curr.Longitude != 0;

                // Check the distance between the two items (this will usually be photos)
                if (Globals.DistanceThreshold > 0 && hasCurrLocation && hasPrevLocation)
                {
                    double distance = GeoUtils.Haversine(curr.Latitude, curr.Longitude, prev.Latitude, prev.Longitude);
                    if (distance >= Globals.DistanceThreshold)
                    {
                        thresholdExceeded = true;
                        reasons.Add($"distance ({distance:F2} km)");
                    }
                }

                // If either threshold is exceeded, log the reason and increment the album ID
                if (thresholdExceeded)
                {
                    var prevName = Path.GetFileName(prev.FileName);
                    var currName = Path.GetFileName(curr.FileName);
                    Logger.Write($"Album break: {string.Join(" and ", reasons)} between {prevName} → {currName}", true);
                    currentAlbumID++;
                }
                curr.AlbumID = currentAlbumID;
            }
        }

        /// <summary>
        /// Assigns base album names to items by grouping them based on their album ID.
        /// </summary>
        /// <remarks>This method groups items by their album ID and assigns a base album name to each
        /// image in the group. The album name is determined depending on the presence of location names:
        /// - If no location names are available, the album name is based on the date range of the items in the group.
        /// - If location names are available, the album name is generated based on the most common locations in the group.
        /// </remarks>
        private static void AssignBaseAlbumNames()
        {
            Logger.Write($"Grouping {Globals.MediaLabel} by album...");

            var groups = Globals.ImageMetadataList.GroupBy(img => img.AlbumID);

            Logger.Write("Determining album names...");
            foreach (var group in groups)
            {
                var items = group.ToList();

                var firstDate = items.First().DateCreated.Date;
                var lastDate = items.Last().DateCreated.Date;
                string albumName;

                // If no geonames DB or no location names, fallback to old date range logic
                if (Globals.GeoNamesLookup == null || !items.Any(p => !string.IsNullOrEmpty(p.LocationName)))
                {
                    // Start with the first date as the album name
                    albumName = firstDate.ToString(Globals.DateFormat, CultureInfo.CurrentCulture);

                    // If the first and last dates are different, append the last date to the album name
                    // to make a range - but only do this if Options.UseDateRange is true
                    if (firstDate != lastDate && Globals.UseDateRange)
                        albumName += $" - {lastDate.ToString(Globals.DateFormat, CultureInfo.CurrentCulture)}";
                }
                else
                {
                    // Location names present, generate name based on locations
                    albumName = MediaMetadataExtractor.GenerateAlbumNameFromLocations(items);
                }

                // Apply prefix template, if one has been specified
                if (!string.IsNullOrEmpty(Globals.AlbumPrefix))
                    albumName = DateHelper.ApplyTemplate(Globals.AlbumPrefix, firstDate, albumName);

                // Ensure album name is unique on disk if requested
                if (Globals.AvoidExistingFolders)
                {
                    string basePath = Path.Combine(Globals.DestinationFolder, albumName);
                    string uniqueAlbumName = albumName;
                    int suffix = 1;

                    // Check if a folder with this name already exists, and if so, append a numeric
                    // suffix and try again until we find a unique name
                    while (Directory.Exists(Path.Combine(Globals.DestinationFolder, uniqueAlbumName)))
                    {
                        uniqueAlbumName = $"{albumName} ({suffix})";
                        suffix++;
                    }

                    // If the album name needs adjusting, log this and then adjust it
                    if (uniqueAlbumName != albumName)
                    {
                        Logger.Write($"Adjusted '{albumName}' to '{uniqueAlbumName}' to avoid clash", true);
                        albumName = uniqueAlbumName;
                    }
                }

                // Now assign the album name to each item in the group
                foreach (var img in items)
                    img.AlbumName = albumName;
            }
        }

        /// <summary>
        /// Assigns and applies part numbers to items in the image metadata list based on album identifiers and names.
        /// </summary>
        /// <remarks>This method iterates through the image metadata list and assigns a sequential part
        /// number to any items where the album name is the same but the album ID is different. An example of
        /// this would be photos taken on the same day but where the distance threshold has been exceeded. Once
        /// completed, the album names are updated to reflect the fact</remarks>
        private static void AssignAndApplyPartNumbers()
        {
            if (!Globals.UsePartNumbers)
                return;

            Logger.Write("Assigning part numbers and finalising album names...");

            if (Globals.ImageMetadataList.Count == 0)
                return; // We shouldn't have got this far without metadata, but just in case

            Globals.ImageMetadataList[0].Part = 1;
            int part = 1;

            // Assign the part numbers based on AlbumID and AlbumName

            for (int i = 1; i < Globals.ImageMetadataList.Count; i++)
            {
                var prev = Globals.ImageMetadataList[i - 1];
                var curr = Globals.ImageMetadataList[i];

                if (curr.AlbumID != prev.AlbumID)
                {
                    // If the album IDs are different, but the album names are the same,
                    // then this is a different part of the same album
                    part = (curr.AlbumName == prev.AlbumName) ? part + 1 : 1;
                }

                curr.Part = part;
            }

            // Now append "(part n)" to the album names where applicable. We don't
            // want to append "(part 1)", as that is the default album name.

            foreach (var img in Globals.ImageMetadataList)
                if (img.Part >= 2)
                    img.AlbumName += $" (part {img.Part})";
        }

        /// <summary>
        /// Sorts the media by the date the photo or video was taken or created.
        /// </summary>
        private static void SortMediaByDate()
        {
            Logger.Write($"Sorting {Globals.MediaLabel} by date taken...");
            Globals.ImageMetadataList.Sort((x, y) => x.DateCreated.CompareTo(y.DateCreated));
        }
    }
}
