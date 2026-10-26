# Game disc image (ISO 9660)

The Crimson Skies disc, as an `.iso` file, is a game-file source read in place. CSVM reads it with
`CSVM/src/Extraction/IsoImage.cs`, which `GameFileSource.Open` picks for a path ending `.iso`. The
disc ships each chapter folder as an LZX cabinet; a cabinet's contents are not part of this page.

## Contents

- [Conceptual model](#conceptual-model)
- [Layout](#layout)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Where the cabinet layer plugs in](#where-the-cabinet-layer-plugs-in)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

The disc's `GAME` folder is an install, except that `GAME/ZBD` holds each chapter as one cabinet
(`C1.CAB`, `C1B.CAB`, ...) where an install holds a chapter folder (`ZBD/C1/`). The root archives
(`interp.zbd`, `planes.zbd`, `rimage.zbd`, `soundsh.zbd`, `soundsl.zbd`, `zrdr.zbd`), `strings.dll`
and `GOSDATA/ASSETS` sit on the disc as plain files. The source is rooted at `GAME`, so a path is
spelled as an install spells it (`ZBD/zrdr.zbd`); an image with no `GAME` folder, such as an image
of an install, is rooted at its own root.

## Layout

ISO 9660 counts in logical sectors of 2,048 bytes. Sectors 0 to 15 are the system area, unused
here. Volume descriptors follow from sector 16, one per sector, each starting with a type byte and
`CD001`; the set ends at type 255. The primary volume descriptor is type 1.

| Primary descriptor offset | Size | Field |
|---|---|---|
| `0x80` (128) | 2 + 2 | logical block size, little- then big-endian |
| `0x9C` (156) | 34 | the root directory's record |

A directory's extent is a run of records, none crossing a sector boundary; a zero length byte pads
to the next sector. Each record:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 1 | record length |
| `0x01` | 1 | extended attribute record length, in blocks |
| `0x02` | 4 + 4 | extent's first block, little- then big-endian |
| `0x0A` | 4 + 4 | data length in bytes, little- then big-endian |
| `0x19` | 1 | flags: `0x02` directory, `0x04` associated file, `0x80` continues in a further extent |
| `0x20` | 1 | name length *n* |
| `0x21` | *n* | name, then a pad byte when *n* is even |

A file's bytes start at (first block + extended attribute length) x block size. CSVM reads the
little-endian halves only.

## Reader rules and edge cases

- **Names.** Only the primary volume's names are read; a Joliet or Rock Ridge name is not. A name
  loses its `;1` version and then a trailing period (`NOEXT.;1` is `NOEXT`). The 8.3 names are
  enough for the plain files above; the cabinets' long `rtextureN` names are the cabinet layer's.
- **Case.** Paths split on either slash and match without regard to case, as an install folder's
  do, because the disc spells names in upper case and the engine in lower.
- **Records 0 and 1** (the directory itself and its parent) are skipped, as are associated files.
  A path's `.` or `..` segment matches no stored name, so no lookup leaves the image.
- **Repeats.** A second record with a name already taken in its directory is ignored.
- **Refused images.** `IsoImage.Read` throws `InvalidDataException` when no primary descriptor is
  found in the first 32 descriptor sectors, the block size is not 512, 1,024 or 2,048, a record is
  shorter than 34 bytes or overruns its directory, an extent runs past the end of the file, a
  directory claims more than 1 MB, or a file spans several extents. `GameFileSource.Open` answers
  null for such an image, as for a folder that is not an install.
- **Loops.** A directory whose extent was already walked is skipped, so a corrupt record pointing
  at an ancestor cannot loop.
- **The install rule.** `GameFileSource.Open` takes an image only when files lie below `ZBD` and
  `GOSDATA/ASSETS`, the image's form of `InstallLocator.IsInstall`.
- **Reading.** Opening walks the tree once and keeps each file's offset and length. Each
  `Open(path)` returns a seekable window over its own handle on the image, so a 128 MB sound
  archive is never loaded whole and two readers never share a file position.

## Where the cabinet layer plugs in

The cabinet decoder wraps a source rather than changing this one. A cabinet source over an inner
source (this image, or the disc's `GAME` folder as an `InstallFolderSource`) answers
`ZBD/<chapter>/...` from the chapter's unpacked cabinet and every other path from the inner
source. `GameFileSource.Open` then returns that wrapper for an `.iso` and for a disc folder, so
callers and `IGameFileSource` stay as they are. The cabinets are found by path through the inner
source (`ZBD/C1.CAB`); `IsoImage.Files` lists every file for a walk that needs one.

## Evidence and limits

`CSVM.Tests/IsoImageTests.cs` builds images in code: a root, `GAME` with nested folders, a
versioned name, a mixed-case name without a version, a name ending in a period, a cabinet and a
file spanning three sectors. It covers lookups, `Exists`, seeking reads, a missing file, `..`
segments, the install rule, a directory loop and five malformed images. An image written by
Windows' IMAPI2 (ISO 9660 only) of the root archives, both `.rof` files and `strings.dll` read back
byte-identical to the install. No image of the original disc has been read yet; set
`CSVM_TEST_ISO` to one for the data test, which checks the root archives are present, that `ZBD`
holds a `.CAB`, and that every non-cabinet file both hold matches the install's bytes. The image
and the install must be the same release for that comparison.
