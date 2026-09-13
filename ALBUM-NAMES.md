# How are album names created?

By default, GroupMachine names albums using the date range of the photos and videos they contain.

For example

`4 Apr 2025 - 7 Apr 2025`

You can optionally provide a free [GeoNames](https://www.geonames.org/) database to create more useful names based on where the content was taken. An album might then be named

`Le Marais, Montmartre and Latin Quarter`

This is particularly useful for holidays and days out, where the places you visited are often more useful than the dates alone.

## Using GeoNames

GeoNames is a database of geographical features and place names. GroupMachine uses the GPS coordinates in your photos and videos to find suitable nearby places in the database.

It does not simply choose the closest place. Instead, it searches through several distance ranges, giving preference to specific and recognisable locations before falling back to broader geographic areas.

| Distance     | Location types prioritised                                 | Example results                                         |
| ------------ | ---------------------------------------------------------- | ------------------------------------------------------- |
| Up to 100 m  | Landmarks and notable places                               | *Eiffel Tower, Tower Bridge, Hyde Park*                 |
| Up to 1 km   | Neighbourhoods and smaller local areas                     | *Montmartre, Le Marais, Greenwich*                      |
| Up to 10 km  | Populated places                                           | *Paris, Bath, Bristol*                                  |
| Up to 100 km | Larger geographic features and selected fallback locations | *Somerset, Provence-Alpes-Côte d’Azur, English Channel* |

The precision level controls how far through these levels GroupMachine will search. The default setting, `--precision 3`, considers all of them, starting with the most specific locations and falling back to broader areas when necessary.

Selected geographic features such as seas, gulfs and straits can also be used as a final fallback when photos or videos were taken on water and no suitable land based location is available.

This approach helps avoid album names based on obscure database entries that happen to be nearby, while still allowing a recognisable landmark to be used when one is genuinely close.

## Choosing the album name

An album can contain content from several places. Rather than choosing just one location, GroupMachine can include up to four place names in the album title.

Places are considered in the order they first appear in the album. If more than four suitable places are found, less significant locations are dropped so that the name remains manageable.

For example, a four day trip might produce

`Le Marais, Montmartre and Latin Quarter`

rather than

`4 Apr 2025 - 7 Apr 2025`

The places therefore appear in roughly the same order as you visited them.

## Adding dates to album names

If you regularly visit the same places, different albums could otherwise end up with the same name.

You can use `-a` (`--append`) to add a date to the end of the album name. For example

```text
--append "MMMM yyyy"
```

would produce

`Le Marais, Montmartre and Latin Quarter (April 2025)`

This is useful when you want to keep albums from different trips separate while still giving them meaningful names.

## Missing GPS information

GroupMachine can sometimes infer the location of content that has missing or invalid GPS information. It does this by using nearby content taken close in time where the location is known.

For example, if several photos were taken during a visit to the same place and one photo has no GPS information, GroupMachine can use the surrounding photos to estimate its location.

This can help both with grouping and with album naming. The inferred location is only an estimate, however, and may not reflect movement between photos.

## Setting up GeoNames

To use location based album names, download a GeoNames dataset from the [GeoNames download page](https://download.geonames.org/export/dump/).

For worldwide coverage, use `allCountries.zip`. Alternatively, you can download the dataset for a specific country.

Unzip the downloaded archive and provide the resulting `.txt` file to GroupMachine using `-g` (`--geocode`).

For example

```text
-g allCountries.txt
```

The GeoNames database is optional. Without it, GroupMachine will continue to create albums using date ranges.
