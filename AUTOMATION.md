# Automating GroupMachine

GroupMachine can be run manually against any folder of photos and videos whenever you want to organise them. For larger or continuously growing libraries, however, it can also form part of an automated workflow.

A typical workflow is

1. Download new photos and videos from a cloud service.
2. Run GroupMachine against the downloaded library.
3. Group the new media into albums.
4. Optionally make the resulting folders available to another photo management application.

GroupMachine does not perform the downloading or scheduling itself. Instead, it is designed to work alongside tools that handle those parts of the workflow.

## Incremental processing

For an automated workflow, use `-df last` (`--date-from last`).

This tells GroupMachine to process media from the date recorded by the previous run onwards, allowing subsequent runs to process new content without repeatedly processing the entire library.

The last processed date is stored in GroupMachine's `settings.ini` file. The location of this file is shown when you run `-h` (`--help`). It is normally located in the parent folder of the log files.

This also means that the `settings.ini` file should be retained between runs. If it is deleted or reset, GroupMachine will no longer have the previous run's date available for incremental processing.

## Allowing recent photos to settle

When photos are downloaded regularly, the most recent files may not yet represent a complete event or trip. Running GroupMachine immediately can therefore result in an album being created before all the related photos have been downloaded.

Use `-xr` (`--exclude-recent`) to exclude very recent media from processing.

For example, a daily scheduled job could download new photos each night and then run GroupMachine with `-df last -xr`. Photos from the most recent period are left until a later run, giving additional photos time to arrive before the album is created.

The exact delay should depend on how frequently new photos are downloaded and how long it is reasonable to wait before creating an album.

## Organising the resulting albums

Automated workflows work best when album names and folders are predictable.

Use `-pa` (`--prefix-album`) to organise albums into a folder hierarchy. The prefix can contain date placeholders and literal text. For example, `-pa "<yyyy>/<MM>-<dd> "` produces a structure such as

```text
2026/
    08-14 London/
    08-21 Cornwall/
    08-29 London/
```

You can also use `-a` (`--append`) to add date elements to the album name itself. This can be useful when the same location is visited more than once, as the date information helps keep otherwise similar albums separate.

The exact naming options you use will depend on how the resulting folders are going to be consumed by other software.

## Avoiding duplicate copies

If the downloaded files are already being retained as the master copy, consider using `-l` (`--link`) instead of copying or moving the files.

This allows GroupMachine to create the album structure without creating another complete copy of the media. The original downloaded files remain in place while the albums reference them through links.

This can substantially reduce the amount of storage required for large photo libraries.

## Location based album names

If you use GroupMachine's location based album naming, make sure the GeoNames database is available and kept reasonably up to date.

GroupMachine uses the database to turn GPS coordinates extracted from the media into place names. Updating the database periodically can therefore improve the place names available for newly processed photos.

See the location based album naming documentation for details on configuring GeoNames and controlling the precision used when selecting place names.

## Cloud download tools

The first part of an automated workflow needs a separate tool to download media from the cloud service.

For example, [iCloud Photos Downloader](https://github.com/icloud-photos-downloader/icloud_photos_downloader) can download photos and videos from iCloud. Similar tools are available for other cloud photo services.

The downloader should ideally be configured so that it can be run repeatedly and only downloads media that has not already been downloaded. GroupMachine can then perform the separate task of organising that growing local library.

## Using GroupMachine with photo management software

GroupMachine creates a conventional folder based album structure, which means the resulting folders can also be useful to other photo management applications.

[Immich](https://immich.app/) users can use [Immich Folder Album Creator](https://github.com/Salvoxia/immich-folder-album-creator) to turn folders created by GroupMachine into albums within Immich.

Other applications such as [Picasa](https://www.majorgeeks.com/files/details/picasa_photo_organizer.html), [PhotoPrism](https://www.photoprism.app/), [LibrePhotos](https://github.com/LibrePhotos/librephotos), [Photoview](https://photoview.github.io/) and [digiKam](https://www.digikam.org/) can work with folder based photo libraries and can therefore make use of the organisation created by GroupMachine.

[Synology Photos](https://www.synology.com/en-global/dsm/feature/photos) does not currently provide a way to automatically create albums from newly added folders or files. There is, however, a workaround that can be used as part of an automated workflow. A script can write metadata tags to the media based on the GroupMachine folder name. Synology Photos can then use those tags to create albums, allowing the album names generated by GroupMachine to be carried through into Synology Photos. This requires an additional processing step and is less direct than the approaches above. See [here](https://github.com/mrsilver76/groupmachine/issues/6#issuecomment-3621052522) for details.

## Example scheduled workflow

A simple automated workflow could run once a day or once a week.

```text
Cloud photo service
        ↓
Download new photos and videos
        ↓
Local photo library
        ↓
GroupMachine
    -df last
    -xr
    -pa "<yyyy>/"
        ↓
Organised album folders
        ↓
Optional photo management software
```

For example

```bash
groupmachine <source> -o <output> -df last -xr -pa "<yyyy>/"
```

The downloader and GroupMachine can then be run from a scheduled task, cron job, shell script or other automation system.

The important part is that the same GroupMachine configuration and `settings.ini` are retained between runs so that incremental processing can continue from the previous run.

## Important considerations

**Run downloads before GroupMachine.** GroupMachine can only organise files that are already available locally.

**Keep the GroupMachine state.** The `settings.ini` file contains information required for incremental processing.

**Allow enough time for recent media.** Use `-xr` when downloads can arrive over several days or when a single event is likely to be spread across multiple download runs.

**Test the workflow manually first.** Before scheduling an automated job, run the complete workflow against a representative copy of the library and check the resulting album names and folder structure.

**Be careful when changing source or output locations.** An automated job should consistently use the same locations and configuration. Changing these between runs can produce results that differ from a normal incremental run.

**Consider what happens when automation fails.** The downloader and GroupMachine are separate steps. A script should ideally only proceed to GroupMachine when the download step has completed successfully.
