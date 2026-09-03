# How does grouping work?

GroupMachine is designed around the way people typically take photos and videos. A typical outing might involve taking a number of photos in one place, then moving on and taking more photos somewhere else. GroupMachine uses the time and location of the content to recognise these natural breaks and divide it into albums.

## Time and distance

Grouping is based on two factors

* The time between pieces of content
* The distance between their locations

GroupMachine processes the content in chronological order. Content is kept in the same group while it remains within the configured time and distance limits. When either limit is exceeded, a new group is started.

The default limits are **48 hours** and **10 km**. This means that content taken less than two days apart and within 10 km will normally belong to the same group.

For example, photos taken during a day trip or weekend away will usually form one album. If you then travel to another city a few days later, the new content will normally start a new album.

The limits can be changed using `-t` (`--time`) and `-d` (`--distance`).

> [!TIP]
> In countries where towns and landmarks are more widely spaced, increasing the distance limit may produce better results, particularly when using GeoNames to create album names. This can be useful in places such as the United States, Canada, Australia and New Zealand.

## Missing date and location information

GroupMachine normally uses the date, time and GPS coordinates stored in each photo or video. Modern smartphones and digital cameras generally provide this information automatically.

When information is missing or invalid, GroupMachine can use information from other content taken around the same time to fill the gap. This is called *imputation*.

For example, if several photos were taken at the same location and one of them has no GPS information, GroupMachine can use the location of the surrounding photos to estimate where it was taken.

If no usable date or time is available in the media metadata, GroupMachine falls back to the file's created and modified timestamps, using whichever is earliest.

These estimates are intended to keep related content together. They cannot account for movement that happened between two photos, so an inferred location may not always represent the exact place where a particular photo or video was taken.

## How albums are formed

Once the content has been sorted by date and time, GroupMachine works through it chronologically and looks for breaks in the sequence.

A new album is created when the content moves beyond either the configured time or distance limit. This allows a single trip to be split naturally into separate albums when there is a significant change in either when or where the content was taken.

For example, imagine a holiday where you spend two days in London, travel to Bath, spend a day there, and then return to London. The changes in location and the time spent between them can result in separate albums rather than putting the entire holiday into one large folder.

The exact result depends on the timestamps and locations available in the media and the configured grouping limits.

## Choosing the right distance

The default distance of 10 km works well for many situations, but there is no single distance that is ideal everywhere.

In densely populated areas, 10 km can cover several distinct towns or attractions. A smaller distance may therefore produce more useful albums.

In areas where places are more widely spread, 10 km may be too restrictive. Increasing the distance can help keep content from the same trip together, particularly when the album names are being generated from geographic data.

The time and distance limits can be adjusted independently, so you can choose the behaviour that best matches how your photos and videos were taken.
