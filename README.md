# GroupMachine
_A cross-platform command-line tool (Windows, Linux, macOS) for grouping photos and videos into albums (folders) based on time and location changes.
It can also name these albums using real-world place names, making your collections more meaningful and easier to navigate._

## 📚 Overview

GroupMachine helps you organize large collections of photos and videos by grouping them into albums based on when and where they were taken.
It’s especially useful if you’ve downloaded images from your camera, mobile phone or cloud service (like Apple iCloud or Google Photos),
which often contain large, mixed sets from multiple locations and dates.

By default, the tool groups your photos and videos into albums - which are simply folders containing related media files. It creates a new album
whenever there’s a noticeable gap in time or distance between your shots (for example, different cities or days apart). This way, the folder structure
naturally reflects your trips, events, or outings without manual sorting.

>[!TIP]
>Once you've grouped your photos, [SideBySide](https://github.com/mrsilver76/sidebyside) can combine two portrait shots into a single landscape image, making them ready for digital frames without awkward cropping or black bars.

## 🧰 Features

- 💻 Runs on Windows 10 & 11, Linux (x64, ARM64, ARM32), and macOS (Intel & Apple Silicon)
- 🖼️ Groups downloaded photos and videos into albums based on time and location.
- 🧾 Uses photo and video metadata for the most accurate time and location (with fallback to file timestamps)
- ⏱️ Uses a configurable time gap (default: 48 hours) to define album boundaries.
- 📏 Uses a configurable distance gap (default: 50 km) to define album boundaries.
- 🗺️ Supports the GeoNames database to give album folders meaningful place names.
- 🗓️ Enables appending of extra date text to the end of album names.
- 🧠 Uses SHA hashes to detect identical files before renaming.
- 🧪 Simulation mode to preview changes without making any modifications.
- 🧮 Parallel processing speeds up hashing, metadata extraction, and file operations for large libraries.
- ✂️ Copies, moves or links files to new album folders.
- ⚙️ Automatic fallback to soft links when hard linking is not possible.
- 📘 Logs all actions, including skipped files, errors, and final summaries.

## 🧩 How does grouping work?

Grouping is based on two key factors: the time between shots and the distance between their locations. When the gap between consecutive
photos or videos exceeds either the time or distance threshold, a new album is started.

Using the default thresholds (48 hours and 50 km) means that photos/videos taken less than 2 days apart and within 50 kilometres (or 31 miles) will be grouped together.
For example, photos/videos taken during a single day trip or a weekend away will usually fall into the same album. If you then travel to a different city
a few days later, that will create a new album. This approach is designed to reflect natural breaks in your timeline, capturing major location changes or extended time gaps.

Because the grouping relies on metadata timestamps and GPS coordinates, it assumes your media files include accurate time and location information.
These assumptions generally hold true for photos and videos taken by modern smartphones and digital cameras. If the time metadata is missing or invalid, GroupMachine
can use the created or last modified timestamp of the file instead (whichever is the earliest). 

You can override these defaults using `-t` (`--time`) and `-d` (`--distance`).

>[!NOTE]
>Videos with embedded GPS data are not currently supported. Most consumer devices (including iPhones) do not store location metadata in videos, so location-based grouping only applies to photos. However, videos will still be included in albums based on time proximity - if they fall within the configured time threshold, they’ll be grouped alongside nearby photos.

## 🧭 Enhancing album names using location data

By default, album folders are named using date ranges reflecting when the photos or videos were taken. You can improve folder naming by using
the [GeoNames](https://www.geonames.org/) database, which maps GPS coordinates to nearby place names.

If you provide the GeoNames data, GroupMachine will look up the nearest populated place for each photo or video. It then selects up to four place names for the album title. These are chosen in the order they first appear in the group, with less frequent locations dropped if more than four are found. The result is a name that prioritises the most representative places, preserving the order of your journey.

For example, an album might be named "_Paris, Le Marais, and Versailles_" instead of just "_5 Apr 2025 - 6 Apr 2025_".

If you frequently visit the same places, you can avoid album name collisions by appending a date to each name using `-a` or `--append`. For instance, using `--append "MMMM yyyy"` would label your album as "_Paris, Le Marais and Versailles (April 2025)_"

To use this feature, download a GeoNames database file from [here](https://www.geonames.org/export/). You can choose `allCountries.zip` for global data or the `.zip` file for a specific country. A list of supported countries and datasets is available [here](https://www.geonames.org/datasources/). You'll then need to manually decompress the `.zip` file and pass the path and filename of the extracted `.txt` file to the program using `-g` or `--geocode`.

Location selection avoids overly narrow names. GroupMachine prioritises general place names (e.g. _"Paris"_) over exact landmarks (e.g. _"Eiffel Tower"_), giving you cleaner and more useful album names. You can override this by using the `-p` (`--precise`) option to include well-known landmarks.

>[!TIP]
>If your photos span multiple countries, consider using the full `allCountries.txt` dataset for best results. It takes longer to load but ensures accurate results across borders.

## 📦 Download

Get the latest version from https://github.com/mrsilver76/groupmachine/releases.

Each release includes the following files (`x.x.x` denotes the version number):

|Platform|Download|
|:--------|:-----------|
|Microsoft Windows 10 & 11|`GroupMachine-x.x.x-win-x64.exe` ✅ **Most users should choose this**|
|Linux (64-bit Intel/AMD)|`GroupMachine-x.x.x-linux-x64`|
|Linux (64-bit ARM), e.g. Pi 4 and newer|`GroupMachine-x.x.x-linux-arm64`|
|Linux (32-bit ARM), e.g. Pi 3 and older|`GroupMachine-x.x.x-linux-arm`|
|Docker, e.g. Synology NAS|`GroupMachine-x.x.x-linux-x64`|
|macOS (Apple Silicon)|`GroupMachine-x.x.x-osx-arm64`|
|macOS (Intel)|`GroupMachine-x.x.x-osx-x64`|
|Other/Developers|Source code (zip / tar.gz)|

> [!TIP]
> There is no installer for native platforms. Just download the appropriate file and run it from the command line. If you're using Docker (e.g. on Synology), setup will differ - see notes below.

### Linux/macOS users

- Download the appropriate binary for your platform (see table above).
- Install the [.NET 8.0 runtime](https://learn.microsoft.com/en-gb/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website).
- ⚠️ Do not install the SDK, ASP.NET Core Runtime, or Desktop Runtime.
- Make the downloaded file executable: `chmod +x GroupMachine-x.x.x-<your-platform>`

### Docker users

- Install the [.NET 8.0 runtime](https://learn.microsoft.com/en-gb/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website) inside the container or use a [.NET container image](https://learn.microsoft.com/en-gb/dotnet/core/docker/introduction#net-images).
- ⚠️ Do not install the SDK, ASP.NET Core Runtime, or Desktop Runtime.
- Use the `GroupMachine-x.x.x-linux-x64` binary inside the container.
- Mount your photo folders into the container with appropriate read and write access.

### Platform testing notes

* Tested extensively: Windows 11  
* Tested moderately: Linux (64-bit ARM, Raspberry Pi 5 only)  
* Not tested: Windows 10, Linux (x64), Linux (32-bit ARM), Docker, macOS (x64 & Apple Silicon)

>[!NOTE]
>Docker and macOS environments have not been tested, and no platform-specific guidance is available as these setups are outside the developer’s experience. While GroupMachine should work fine on them, support will be limited to questions directly related to the tool itself.

## 🚀 Quick start guide

This is the simplest way to use GroupMachine. It scans `d:\Photos` (and all sub-folders) for photo/video content and moves it into dated subfolders within `e:\My Album`. It uses the default thresholds (48 hours and 50 km) and default date format (eg `20 Jul 2025`).

```
GroupMachine "d:\Photos" -m -r -o "e:\My Album"

GroupMachine "d:\Photos" --move --recursive --output "e:\My Album"
```

This is a more complicated example that uses the GeoNames database (and the `allCountries.txt`) database file for naming folders and appends the four-digit year onto the album name. Files are copied instead of moved.

```
GroupMachine "d:\Photos" -o "e:\My Album" -r -g c:\temp\allCountries.txt -a "YYYY" -c

GroupMachine "d:\Photos" --output "e:\My Album" --recursive --geocode "c:\temp\allCountries.txt" --append "YYYY" --copy
```

This example shows how to change the thresholds (to 24 hours and 10 km), the date format of the folder names (to ISO-8601 format) and to skip looking for videos.

```
GroupMachine "d:\Photos" -r -o "e:\My Album" -t 24 -d 10 -f "yyyy-MM-dd" -nv -c

GroupMachine "d:\Photos" --recursive --output "e:\My Album" --time 24 --distance 10 --format "yyyy-MM-dd" --no-videos --copy

```

>[!TIP]
>Use `-s` (`--simulate`) to preview how your albums will be grouped - no files are moved, copied or linked, so it’s a safe way to fine-tune all your settings before committing. To better understand what’s happening during processing, check the log file (the location is shown when you run `-h` or `--help`).

## 💻 Command line options

GroupMachine is a command-line tool. Run it from a terminal or command prompt, supplying all options and arguments directly on the command line. Logs with detailed information are also written and you can find the log file location using `--help` (`-h`).

```
GroupMachine [options] -o <destination folder> <source folder> [<source folder> ...]
```

### Required

- **`-o <folder>`, `--output <folder>`**   
  Specifies the destination folder for grouped albums. If the folder does not exist, it will be created automatically.

- **`<source folder> [<source folder> ...]`**   
  One or more folders containing the photos and videos to be grouped.

#### File copy modes

One of the following file copy modes must be specified:

- **`-c`, `--copy`**   
  Copy files from the source folder to the destination folder.

- **`-m`, `--move`**   
  Move files from the source folder to the destination folder.

- **`-l`, `--link`**   
  Link files from the source folder to the destination folder. This avoids duplicating data by creating a reference to the original file instead of copying it.

  When used, GroupMachine first attempts to create a [hard link](https://en.wikipedia.org/wiki/Hard_link), which behaves like a real file and doesn’t depend on the original path. If hard linking fails (e.g. across different drives), it falls back to creating a [soft link](https://en.wikipedia.org/wiki/Symbolic_link) (symbolic link), which points to the source file’s path and breaks if that file is moved or deleted.

>[!NOTE]
>Files are never overwritten. If a file with the same name already exists in the destination, it is compared by content. If the files are not binary identical, a number is appended to the new file (e.g., `IMG_1234 (2).jpg`) to preserve both versions.

### File selection

- **`-r`, `--recursive`**  
  Recursively scan all subfolders within the specified source folders.

- **`-nv`, `--no-videos`**  
  Exclude videos (`.mp4`, `.mov`) from scanning.
  
- **`-np`, `--no-photos`**  
  Exclude photos (`.jpg`, `.jpeg`) from scanning.

### Grouping logic

- **`-d <number>`, `--distance <number>`**   
  Distance threshold in kilometers. If two consecutive photos or videos are taken more than this distance apart, a new album is started. Set to `0` to disable distance-based grouping. If not supplied, the default is 50 km.

- **`-t <number>`, `--time <number>`**   
  Time threshold in hours. If two consecutive photos or videos are taken more than this many hours apart, a new album is started. Set to `0` to disable time-based grouping. If not supplied, the default is 48 hours.

### Album naming

- **`-g <file>`, `--geocode <file>`**   
  Full path to a [GeoNames database file](https://www.geonames.org/export/) in `.txt` format. Providing this file enables automatic renaming of albums based on location data. You will need to manually decompress the `.zip` file provided by the GeoNames website before you can use it with GroupMachine.

>[!TIP]
>For best performance, store the [GeoNames database file](https://www.geonames.org/export/) on a local SSD. Loading it from a network share, USB drive, or HDD can be much slower.

- **`-f <format>`, `--format <format>`**   
  Date format used for album folder names that use dates. This follows the [.NET DateTime format syntax](#datetime-format-syntax). The default is `dd MMM yyyy` (e.g., _"20 Jul 2025"_). Used when no GeoNames data is provided or no location can be determined.

- **`-p`, `--precise`**   
  Use more specific named locations in album titles (e.g. _"Eiffel Tower"_ instead of _"Paris"_).

  By default, GroupMachine avoids GeoNames entries from the [spot ("S") feature class](https://www.geonames.org/export/codes.html#:~:text=S%20spot%2C%20building%2C%20farm), as they can produce overly specific or inconsistent album names. Enabling this option allows use of select named places typically relevant to tourist photography.

  Only the following types of places are included:

  - **Cultural landmarks:** castles, monuments, palaces, temples, mosques, churches, theatres, opera houses
  - **Historic and archaeological sites:** ruins, tombs, pyramids, historical or archaeological sites
  - **Recognisable structures:** towers, arches, caves, lighthouses, piers, quays, squares, gardens
  - **Public institutions and attractions:** museums, zoos, famous universities, public libraries, stadiums
  - **Leisure and resort areas:** marinas, resorts, golf courses, spas
  - **Religious or spiritual locations:** missions, shrines

  The list is fixed and cannot be changed.

- **`-a <format>`, `--append <format>`**  
  Date format to append to album folder names that use locations. Useful to distinguish multiple visits to the same location. Also uses the [.NET DateTime format syntax](#datetime-format-syntax).

 - **`-nr`, `--no-range`**  
  Don’t use a date range in album titles. By default, GroupMachine adds a date range (e.g. _"12 June 2025 – 14 June 2025"_, using the format defined by `-f`, `--format`) when an album spans multiple days. With this option enabled, album names always use the date of the first item in the group, even if later items fall on different days.

- **`-np`, `--no-part`**  
  Don’t add part numbers to album titles. Normally, if multiple albums form on the same day, date range or in the same location (e.g. due to the distance threshold being exceeded), GroupMachine appends part numbers to distinguish them (e.g. _"Paris (part 2)"_). With this option enabled, part numbers are omitted and such groups are merged into a single album.

>[!TIP]
>If you regularly visit the same locations at different times of the year and want to keep those visits separate, consider appending the date, month, or year to album names using the `-a` (`--append`) option.

### Others

- **`-s`, `--simulate`**  
  Runs all processing steps but does not copy or move any files. Ideal for testing and previewing changes.
  
- **`/?`. `-h`, `--help`**  
  Displays the full help text with all available options, credits and the location of the log files.

### DateTime format syntax

The `-f` (`--format`) and `-a` (`--append`) options accept date formats using the .NET DateTime format syntax, allowing you to customize how dates appear in album names. Below is a list of commonly used date formats for your reference:

|Format String|Description|Example Output|
|-------------|-----------|--------------|
|`dd MMM yyyy`|Day (two digits) Month abbrev. Year (four digits)|`20 Jul 2025`|
|`dd MMMM yyyy`|Day (two digits) Full month name Year (four digits)|`20 July 2025`|
|`MM/dd/yyyy`|Month/day/year (common US format)|`07/20/2025`|
|`dd/MM/yyyy`|Day/month/year (common UK format)|`20/07/2025`|
|`yyyy-MM-dd`|ISO 8601 format (sortable)|`2025-07-20`|
|`MMMM yyyy`|Full month name and year|`July 2025`|
|`MMM yyyy`|Abbreviated month name and year|`Jul 2025`|
|`yyyy`|Year only|`2025`|
|`dd-MM-yyyy`|Day-month-year with dashes|`20-07-2025`|

For more detailed information, please refer to [this page](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#:~:text=The%20following%20table%20describes%20the%20custom%20date%20and%20time%20format%20specifiers%20and%20displays%20a%20result%20string%20produced%20by%20each%20format%20specifier.) authored by Microsoft.

## 🛟 Questions/problems?

Please raise an issue at https://github.com/mrsilver76/groupmachine/issues.

## 💡 Future development: open but unplanned

GroupMachine currently meets the needs it was designed for, and no major new features are planned at this time. However, the project remains open to community suggestions and improvements. If you have ideas or see ways to enhance the tool, please feel free to submit a [feature request](https://github.com/mrsilver76/groupmachine/issues).

## 📝 Attribution

- Gallery icons by Freepik - Flaticon (https://www.flaticon.com/free-icons/gallery)
- Apple and iCloud are trademarks of Apple Inc., registered in the U.S. and other countries. This tool is not affiliated with or endorsed by Apple Inc.
- Google and Google Photos are trademarks of Google LLC. This tool is not affiliated with or endorsed by Google LLC.
- .NET is a trademark of Microsoft Corporation. This tool is developed using the .NET platform but is not affiliated with or endorsed by Microsoft.
- GeoNames is a project of Unxos GmbH. This tool is not affiliated with or endorsed by Unxos GmbH.

## 🕰️ Version history

### 1.0.0 (08 August 2025)

- 🏁 Declared as the first stable release.
- Enforced use of `-c` (`--copy`), `-m` (`--move`), or `-l` (`--link`) to specify the copy mode.
- Added `-l` (`--link`) option for hard linking, falling back to soft links on failure.
- Added `-p` (`--precise`) to enable precise location names (e.g. stations, parks, landmarks, etc.) in album titles.
- Added `-nr` (`--no-range`) to show only the first date in folder names that span multiple days.
- Added `-np` (`--no-part`) to suppress part number suffixes.
- Changed album title logic: dropped popularity sorting; locations now kept in time order with the least-used removed.
- Updated `-s` (`--simulate)` to show the destination folder structure.
- Refactored the _"(part x)"_ numbering logic to ignore existing folders on disk, relying on date suffixes for uniqueness.
- Switched to SHA512 for identical file checks on 64-bit processors, 32-bit processors continue to use SHA256.
- Removed `-u` (`--unique`) check due to poor performance and limited value.
- Logger now includes OS details to assist with debugging.
- Re-ordered command-line arguments and grouped them into logical sections.
- Cleaned up various pieces of code (analyzer suggestions regarding naming, simplifications, and style)

### 0.9.0 (22 July 2025)
- Initial release.
