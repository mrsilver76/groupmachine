# GroupMachine
_A cross-platform command-line tool (Windows, Linux, macOS) for grouping photos and videos into albums (folders) based on time and location changes.
It can also name these albums using real-world place names, making your collections more meaningful and easier to navigate._

## 📚 Overview

This tool helps you organize large collections of photos and videos by grouping them into albums based on when and where they were taken.
It’s especially useful if you’ve downloaded images from your camera, mobile phone or cloud service (like Apple iCloud or Google Photos),
which often contain large, mixed sets from multiple locations and dates.

By default, the tool groups your photos and videos into albums - which are simply folders containing related media files. It creates a new album
whenever there’s a noticeable gap in time or distance between your shots (for example, different cities or days apart). This way, the folder structure
naturally reflects your trips, events, or outings without manual sorting.

## 🧰 Features

- 💻 Runs on Windows 10 & 11, Linux (x64, ARM64, ARM32), and macOS (Intel & Apple Silicon)
- 🖼️ Groups downloaded photos and videos into albums based on time and location.
- 🧾 Uses photo and video metadata for the most accurate time and location (with fallback to file timestamps)
- ⏱️ Uses a configurable time gap (default: 48 hours) to define album boundaries.
- 📏 Uses a configurable distance gap (default: 50 km) to define album boundaries.
- 🗺️ Supports the GeoNames database to give album folders meaningful place names.
- 🗓️ Optionally appends extra date text to the end of album names.
- 🧠 Detects duplicates even if filenames differ (via SHA256 content hashes)
- 🧪 Simulation mode to preview changes without making any modifications.
- 🧮 Parallel processing speeds up hashing and metadata extraction for large libraries.
- ✂️ Optionally moves files or leaves source files untouched (copy mode)
- 📘 Logs all actions, including skipped files, errors, and final summaries.

## 🧩 How does grouping work?

Grouping is based on two key factors: the time between shots and the distance between their locations. When the gap between consecutive
photos or videos exceeds either the time or distance threshold, a new album is started.

Using the default thresholds (48 hours and 50 km) means that photos/videos taken less than two days apart and within 50 kilometres will be grouped together.
For example, photos/videos taken during a single day trip or a weekend away will usually fall into the same album. If you then travel to a different city
a few days later, that will create a new album. This approach is designed to reflect natural breaks in your timeline, capturing major location changes or extended time gaps.

Because the grouping relies on metadata timestamps and GPS coordinates, it assumes your media files include accurate time and location information.
These assumptions generally hold true for photos and videos downloaded from modern smartphones or cloud backups. If the metadata is missing or invalid, GroupMachine
can use the created or last modified timestamp of the file instead (whichever is the earliest). 

You can override these defaults using `-t` (`--time`) and `-d` (`--distance`).

>[!NOTE]
>Videos with embedded GPS data are not currently supported. Most consumer devices (including iPhones) do not store location metadata in videos, so location-based grouping only applies to photos. However, videos will still be included in albums based on time proximity - if they fall within the configured time threshold, they’ll be grouped alongside nearby photos.

## 🧭 Enhancing album names using location data

By default, album folders are named using date ranges reflecting when the photos or videos were taken. However, you can improve folder naming by using
the [GeoNames](https://www.geonames.org/) database, which links GPS coordinates to nearby place names.

If you provide the GeoNames data, the tool will look up the nearest populated place for each group of media files and include that location in the album name.
This can make your albums more meaningful and easier to browse - for example, “_Paris, Le Marais and Versailles_” instead of just “_5 Apr 2025 - 6 Apr 2025_”.
You can also choose to append date information (using `-a` or `--append`) to locations you visit frequently to differentiate those albums. For instance, using `--append MMMM yyyy` would label your album as “_Paris, Le Marais and Versailles (April 2025)_”

To use this feature, you’ll need to download one of the [GeoNames database files](https://www.geonames.org/datasources/), which are quite large and may take
some time to process, especially the comprehensive `allCountries.txt` dataset. The location names are based on popular or significant nearby places, so while
usually accurate, they might not always perfectly match your exact photo spots.

To use the GeoNames data, you need to manually decompress the `.zip` file and use `-g` (`--geocode`) with the full path and filename of the resulting `.txt` file.

>[!TIP]
>If your photos span multiple countries, consider using the full `allCountries.txt` dataset for best results. It can take several minutes to load, but ensures accurate location names across borders.

## 📦 Download

Get the latest version from https://github.com/mrsilver76/groupmachine/releases.

Each release includes the following files (`x.x.x` denotes the version number):

|Platform|Download|
|:--------|:-----------|
|Microsoft Windows 10 & 11|`GroupMachine-x.x.x-win-x64.exe` ✅ **Most users should choose this**|
|Linux (64-bit Intel/AMD)|`GroupMachine-x.x.x-linux-x64`|
|Linux (64-bit ARM), e.g. Pi 4 and newer|`GroupMachine-x.x.x-linux-arm64`|
|Linux (32-bit ARM), e.g. Pi 3 and older|`GroupMachine-x.x.x-linux-arm`|
|Synology DSM|`GroupMachine-x.x.x-linux-x64` 🐳 Run via Docker / Container Manager|
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

### Synology DSM users

- Only Plus-series models (e.g. DS918+, DS920+) support Docker/Container Manager. Value and J-series models cannot run GroupMachine this way.
- For DSM 7.2+, use Container Manager; older versions use the Docker package.
- Install the [.NET 8.0 runtime](https://learn.microsoft.com/en-gb/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website) inside the container or use a [.NET container image](https://learn.microsoft.com/en-gb/dotnet/core/docker/introduction#net-images).
- ⚠️ Do not install the SDK, ASP.NET Core Runtime, or Desktop Runtime.
- Use the `GroupMachine-x.x.x-linux-x64` binary inside the container.
- Mount your playlist folder into the container with read access and ensure network access to Plex.

### Platform testing notes

* Tested extensively: Windows 11  
* Tested moderately: Linux (64-bit ARM, Raspberry Pi 5 only)  
* Not tested: Windows 10, Linux (x64), Linux (32-bit ARM), Synology DSM (via Container Manager), macOS (x64 & Apple Silicon)

>[!NOTE]
>Docker, Synology DSM, and macOS environments have not been tested, and no platform-specific guidance is available as these setups are outside the developer’s experience. While GroupMachine should work fine on them, support will be limited to questions directly related to the tool itself.

## 💻 Command line options

GroupMachine is a command-line tool. Run it from a terminal or command prompt, supplying all options and arguments directly on the command line. Logs with detailed information are also written and you can find the log file location using `--help` (`-h`).

```
GroupMachine [options] -o <destination folder> <source folder> [<source folder> ...]
```

### Mandatory arguments

- **`-o <folder>`, `--output <folder>`**   
  Specifies the destination folder for grouped albums. If the folder does not exist, it will be created automatically.

- **`<source folder> [<source folder> ...]`**   
  One or more folders containing the photos and videos to be grouped.

### Optional arguments

- **`-c`, `--copy`**   
  Copy files from the source folder to the destination folder instead of moving them.

- **`-d <number>`, `--distance <number>`**   
  Distance threshold in kilometers. If two consecutive photos or videos are taken more than this distance apart, a new album is started. Set to `0` to disable distance-based grouping.

- **`-t <number>`, `--time <number>`**   
  Time threshold in hours. If two consecutive photos or videos are taken more than this many hours apart, a new album is started. Set to `0` to disable time-based grouping.

- **`-g <file>`, `--geocode <file>`**   
  Full path to a [GeoNames database file](https://www.geonames.org/datasources/) in `.txt` format. Providing this file enables automatic renaming of albums based on location data.

>[!TIP]
>Store the [GeoNames database file](https://www.geonames.org/datasources/) on your computer’s SSD to speed up loading. Using an HDD or network share will cause significant delays.

- **`-f <format>`, `--format <format>`** 
  Date format used for album folder names. This follows the [.NET DateTime format syntax](#datetime-format-syntax). The default is `dd MMM yyyy` (e.g., `20 Jul 2025`). Used when no GeoNames data is provided or no location can be determined.

- **`-a <format>`, `--append <format>`**  
  Date format to append to album names. Useful to distinguish multiple visits to the same location. Also uses the [.NET DateTime format syntax](#datetime-format-syntax).

- **`-r`, `--recursive`**  
  Recursively scan all subfolders within the specified source folders.

- **`-s`, `--simulate`**  
  Runs all processing steps but does not copy or move any files. Ideal for testing and previewing changes.

- **`-np`, `--no-photos`**  
  Exclude photos (`.jpg`, `.jpeg`) from scanning.

- **`-nv`, `--no-videos`**  
  Exclude videos (`.mp4`, `.mov`) from scanning.

- **`-nh`, `--no-hash-check`**  
  Skip checking for duplicate files based on content hashes in the destination folder.

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

## 💡 Possible future enhancements

These features are currently under consideration and may or may not be implemented. There is no commitment to deliver them, and no timeline has been established for their development. They represent exploratory ideas intended to improve the tool's functionality and usability.

- [ ] Seralising the GeoNames database file into a binary file for much faster subsequent loads.
- [ ] Support for embedded location data within `.mp4` and `.mov` video files. This requires sample videos that implement this capability.

If you're particularly enthusiastic about any of these potential features or have ideas of your own, you’re encouraged to raise a [feature request](https://github.com/mrsilver76/groupmachine/issues).

## 📝 Attribution

- Gallery icons by Freepik - Flaticon (https://www.flaticon.com/free-icons/gallery)
- Apple and iCloud are trademarks of Apple Inc., registered in the U.S. and other countries. This tool is not affiliated with or endorsed by Apple Inc.
- Google and Google Photos are trademarks of Google LLC. This tool is not affiliated with or endorsed by Google LLC.
- .NET is a trademark of Microsoft Corporation. This tool is developed using the .NET platform but is not affiliated with or endorsed by Microsoft.
- GeoNames is a project of Unxos GmbH. This tool is not affiliated with or endorsed by Unxos GmbH.

## 🕰️ Version history

### 1.0.0 (xx xx 2025)
- 🏁 Initial release. Declared as stable.
