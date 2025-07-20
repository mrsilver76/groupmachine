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
- 🧾 Reads photo and video metadata to ensure the most accurate time and location (with fallback to file timestamps)
- ⏱️ Uses a configurable time gap (default: 48 hours) to define album boundaries.
- 📏 Uses a configurable distance gap (default: 50 km) to define album boundaries.
- 🗺️ Supports the GeoNames database to give album folders meaningful place names.
- 🗓️ Optionally appends extra date text to the end of album names.
- 🧠 Detects duplicates even if filenames differ (via SHA256 content hashes)
- 🧪 Simulation mode to preview changes without making any modifications.
- 🧮 Parallel processing speeds up hashing and metadata extraction for large libraries.
- ✂️ Optionally moves files or leaves source files untouched (copy mode)
- 📘 Logs all actions, including skipped files, errors, and final summaries.

## 🧩 How Does Grouping Work?

Grouping is based on two key factors: the time between shots and the distance between their locations. When the gap between consecutive
photos or videos exceeds either the time or distance threshold, a new album is started.

Using the default thresholds (48 hours and 50 km) means that photos taken less than two days apart and within 50 kilometres will be grouped together.
For example, pictures taken during a single day trip or a weekend away will usually fall into the same album. If you then travel to a different city
a few days later, that will create a new album. This approach is designed to reflect natural breaks in your photo timeline, capturing major location changes or extended time gaps.

Because the grouping relies on metadata timestamps and GPS coordinates, it assumes your media files include accurate time and location information.
These assumptions generally hold true for photos and videos downloaded from modern smartphones or cloud backups. If the metadata is missing or invalid, GroupMachine
can use the created or last modified timestamp of the file instead (whichever is the earliest). 

You can override these defaults using `-t` (`--time`) and `-d` (`--distance`).

## 🧭 Enhancing Album Names Using Location Data

By default, album folders are named using date ranges reflecting when the photos or videos were taken. However, you can improve folder naming by using
the [GeoNames](https://www.geonames.org/) database, which links GPS coordinates to nearby place names.

If you provide the GeoNames data, the tool will look up the nearest populated place for each group of media files and include that location in the album name.
This can make your albums more meaningful and easier to browse - for example, “_Paris, Le Marais and Versailles_” instead of just “_5 Apr 2025 - 6 Apr 2025_”.
You can also choose to append date information (using `-a` or `--append`) to locations you visit frequently to differentiate those albums.

To use this feature, you’ll need to download one of the [GeoNames database files](https://www.geonames.org/datasources/), which are quite large and may take
some time to process, especially the comprehensive `allCountries` dataset. The location names are based on popular or significant nearby places, so while
usually accurate, they might not always perfectly match your exact photo spots.

To use the GeoNames data, you need to manually unzip the file and use `-g` (`--geocode`) with the full path and filename of the resulting `txt` file.

>[!TIP]
>If your photos span multiple countries, consider using the full `allCountries.txt` GeoNames dataset for best results. It can take several minutes to load, but ensures accurate location names across borders.

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
