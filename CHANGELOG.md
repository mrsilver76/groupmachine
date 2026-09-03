# Version history

## 1.5.1 (xx September 2027)
- Fixed a bug that caused GroupMachine to crash when trying to move (using `-m` or `--move`) content into a folder.
- Updated the documentation to put grouping, album naming, automation and changelog into separate files.
- Updated the documentation to describe the steps that GroupMachine takes when running.
- Fixed minor errors within the documentation.

## 1.5.0 (20 July 2027)

- Optimised GeoNames location loading by filtering unnecessary data before processing. This has dramatically reduced memory usage and improved performance, especially for large photo and video collections.
- Significantly improved album naming by changing the GeoNames lookup logic to introduce a clearer prioritisation order. Location searches now use four distance tiers that better reflect how people typically name places - prioritising spot features within 100m, local features within 1km, populated places within 10km, and finally the nearest appropriate location within 100km.
- Added support for selected hydrographic features such as seas, gulfs and straits as part of the final fallback, ensuring support for photos and videos taken on water.
- Changed the default precision level to 3 (detailed) to include spot features by default.
- Added support for processing HEIC, HEIF and AVIF images.
- Improved album date accuracy by checking additional EXIF and QuickTime metadata fields.
- Upgraded MetadataExtractor package from 2.8.1 to 2.9.3.
- Fixed a bug that incorrectly prevented videos from being grouped by location.
- Fixed a bug that incorrectly quoted command line options in logs.
- Fixed bug that prevented GeoNames from being used for video content only.
- Updated `-s` (`--simulate`) to display significantly more useful information in the terminal instead of only writing details to logs.
- Cleaned up a lot of formatting to the console.
- Updated `-h` (`--help`) output with more descriptive command information and improved formatting to respect terminal width.
- Significantly sped up file size scanning by running operations in parallel.
- Cleaned up code and fixed compiler warnings and recommendations.
- Updated documentation, especially around the Quick Start section and location naming behaviour.

## 1.4.0 (21 March 2026)
- Added support for extracting GPS metadata from videos - handles XMP, DMS, decimal and ISO 6709 formats commonly used by both iOS and Android.
- Improved progress bar accuracy - now calculated using bytes processed rather than files processed.
- Tidied up formatting of progress bar for better terminal compatibility.
- Updated default distance to 10km and default precision level to 2 for better album naming.
- Added checks to ensure invalid GPS data is not considered.
- Fixed a bug which meant that photos and videos were still scanned even if `-nv` and/or `-np` were used.
- Updated the publish script to use `--no-self-contained` as identified by dotnet/sdk#51888.
- Updated documentation and copyright.

## 1.3.0 (08 October 2025)
- Replaced `-p` (`--precise`) with new `-p` (`--precision`) to support three levels of album naming detail.
- Added progress bar for long running tasks; displays percentage complete and estimated time left.
- Updated GPL copyright version in comments to correctly reflect GNU GPL v2 (or later).
- Updated documentation.

## 1.2.0 (22 September 2025)
- Improved grouping by filling missing/invalid GPS data (*imputing*) with locations inferred from photos taken close in time.
- Moved content sorting by date earlier in the process to support imputing and improve debugging with logs.
- Added automatic detection of a safe number of parallel tasks based on CPU and storage type to prevent `SEHException` crashes on network drives.
- Added `-mp` (`--max-parallel`) option to allow users to override the default number of parallel copy, move, or link operations.
- Default hashing switched to CRC64-ECMA-FAST (64 KiB prefix) for much faster performance; accidental collisions remain rare.
- Added `-ha` (`--hash-algo`) to override the hashing algorithm with MD5 or SHA512 (SHA256 on 32-bit systems).
- Added file size comparison before hashing to further improve duplicate-checking speed.
- Fixed a bug where the last processed timestamp would be incorrectly updated to an earlier date.
- Tidied up logging and removed superfluous entries.
- Fixed a bug where the version checker formatted version numbers using .NET conventions instead of semantic versioning.
- Updated publishing powershell script to avoid hanging after first build has been completed. 

## 1.1.0 (12 September 2025)

- Added `-df` (`--date-from`) and `-dt` (`--date-to`) to define the photo date range.
- Added support for using `last` with both `-df` and `-dt`, allowing resuming of previous runs.
- Added `-xr` (`--exclude-recent`) to skip photos that would appear in future albums (using `-t` threshold).
- Added `-p` (`--prepend`) to prefix folder names; supports date formats and folder creation.
- Added `-nc` (`--no-check`) to disable GitHub version checking.
- Files with no location data are counted to highlight potential issues.
- New header displays all key configuration information.
- Added `-u` (`--unique`) to prevent existing album folders from being re-used when album names clash.
- Improved logger performance by keeping files open instead of repeatedly opening/closing.
- Split utility functions into static classes for clearer structure.
- Resolved all .NET code analysis warnings to standardize style and tidy the codebase.
- Added documentation on how GroupMachine can be used in an automated workflow.

## 1.0.0 (08 August 2025)

- 🏁 Declared as the first stable release.
- Enforced use of `-c` (`--copy`), `-m` (`--move`), or `-l` (`--link`) to specify the copy mode.
- Added `-l` (`--link`) option for hard linking, falling back to soft links on failure.
- Added `-p` (`--precise`) to enable precise location names (e.g. stations, parks, landmarks, etc.) in album titles.
- Added `-nr` (`--no-range`) to show only the first date in folder names that span multiple days.
- Added `-np` (`--no-part`) to suppress part number suffixes.
- Changed album title logic: dropped popularity sorting; locations now kept in time order with the least-used removed.
- Updated `-s` (`--simulate`) to show the destination folder structure.
- Refactored the _"(part x)"_ numbering logic to ignore existing folders on disk, relying on date suffixes for uniqueness.
- Switched to SHA512 for identical file checks on 64-bit processors, 32-bit processors continue to use SHA256.
- Removed `-u` (`--unique`) check due to poor performance and limited value.
- Logger now includes OS details to assist with debugging.
- Re-ordered command-line arguments and grouped them into logical sections.
- Cleaned up various pieces of code (analyzer suggestions regarding naming, simplifications, and style)

## 0.9.0 (22 July 2025)
- Initial release.
