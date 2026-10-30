# Game disc image (ISO 9660)

The Crimson Skies disc, as an `.iso` file or a raw CD image, is a game-file source read in place.
CSVM reads the volume with `CSVM/src/Extraction/IsoImage.cs`, which `GameFileSource.Open` picks
for a path ending `.iso`; a raw image (`.cue` with its `.bin`/`.img`, or CloneCD `.ccd` with its
`.img`) goes through `RawCdImage.cs` and `CueSheet.cs` to the same reader. Either is wrapped in the
cabinet layer: the disc ships each chapter folder as an LZX cabinet, described in
[cabinet.md](cabinet.md).

## Contents

- [Conceptual model](#conceptual-model)
- [Layout](#layout)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Raw CD images](#raw-cd-images)
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
  enough for the plain files above; the cabinets' long `rtextureN` names are restored by the
  cabinet layer ([cabinet.md](cabinet.md#what-the-discs-cabinets-hold)).
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

## Raw CD images

A raw image stores whole CD sectors, 2,352 bytes each, where an `.iso` stores only the 2,048
bytes of user data. A copy-protected disc such as the SafeDisc release is usually dumped this way,
because the protection's deliberately bad sectors defeat a plain 2,048-byte read. CSVM translates
the data track's sectors back to 2,048 bytes and hands that stream to the ISO 9660 reader above,
so every rule of the previous section applies unchanged.

| Track mode | Stored sector | User data at | The rest |
|---|---|---|---|
| `MODE1/2352` | 2,352 | 16 | 12-byte sync, 4-byte header (address, mode 1); after the data, EDC and ECC |
| `MODE2/2352` (form 1) | 2,352 | 24 | sync, header (mode 2), 8-byte subheader; after the data, EDC and ECC |
| `MODE2/2336` (form 1) | 2,336 | 8 | the subheader; after the data, EDC and ECC |
| `MODE1/2048`, `MODE2/2048` | 2,048 | 0 | none: the track is an ISO |

The EDC is a CRC-32 (reflected polynomial `0xD8018001`, initial value 0) over bytes 0 to 2,063 of
a mode 1 sector, stored little-endian at 2,064. CSVM does not check it; the data test reports it.

**The `.cue` sheet.** A text file of commands, one per line, keywords in any case. CSVM reads three:
`FILE "<name>" <type>` names the image file the tracks after it are in; `TRACK <nn> <mode>` opens
a track (`AUDIO` or a mode from the table); `INDEX <nn> <mm:ss:ff>` places an index at that time
in the file, counted in frames of 1/75 s, one sector each. Every other command (`REM`, `PREGAP`,
`FLAGS`, `CATALOG`, ...) is skipped. The data track is the sheet's first track: it starts at its
`INDEX 01` (frames x stored sector size into its file) and ends at the next track's first index
when that track is in the same file, else at the file's end. The file must be `BINARY`. The
`FILE` name is resolved beside the sheet without regard to case, by its relative path first and
then by its bare name, so a path written on the dumping machine (`D:\dumps\Crimson.img`) still
finds the file. A per-track dump, one `FILE` per track, reads its first track's file.

**The CloneCD `.ccd` sheet.** An INI file; the `.img` beside it, of the same name, holds every
sector of the disc from sector 0 at 2,352 bytes. CSVM reads `[Disc] DataTracksScrambled` (refused
when non-zero) and each `[TRACK n]` section's `MODE` (0 audio, 1, or 2 for form 1 at offset 24)
and `INDEX 1` (the track's first sector in the `.img`). The data track is the lowest-numbered
track; it ends at the next track's `INDEX 0`, else its `INDEX 1`, else the file's end. The
`[Entry n]` TOC sections and the `.sub` subchannel file are not read.

**Picking an image.** `RawCdImage.Descriptor` answers the sheet for a path: an existing `.cue` or
`.ccd` is its own, and an existing `.bin` or `.img` has the same-named `.cue` (then `.ccd`) beside
it, matched without regard to case. A bare `.bin` or `.img` without one is not taken, since its
sector size and mode are unknown. `GameFileSource.Open` holds the volume to the install rule as
it does an `.iso`, and answers null for an image that does not read; `RawCdImage.Read` throws
the reason.

**Refused sheets.** `InvalidDataException`, naming the sheet, the line where there is one, and the
reason: an unclosed quote; `TRACK` before any `FILE`, `INDEX` before any `TRACK`; a command short
of arguments; a track number or time that is not a number or not `mm:ss:ff` (seconds below 60,
frames below 75); no track; a first track that is audio, saying whether a data track follows; a
mode outside the table; a data file that is not `BINARY`; a first track without `INDEX 01`; a track
starting past its file's end; a `.ccd` with scrambled data, no `[TRACK n]`, or a `MODE` outside
0 to 2; a sheet over 64 KB. A missing image file is a `FileNotFoundException`.

**A data track after audio is refused.** A disc with audio first and its data later is a CD-Extra,
whose volume sits in a second session and addresses its sectors from the disc's start. Reading it
needs that session's start, which neither sheet gives reliably, so CSVM refuses it rather than
read a wrong volume. The game's disc is a single data track.

## Where the cabinet layer plugs in

`CabinetSource` wraps a source rather than changing this one. Over an inner source (this image, or
the disc's `GAME` folder as an `InstallFolderSource`) it answers `ZBD/<chapter>/...` from the
chapter's unpacked cabinet and every other path from the inner source. `GameFileSource.Open`
returns that wrapper for an `.iso`, a raw CD image and a disc folder, so callers and
`IGameFileSource` stay as they are. The cabinets are found by path through the inner source
(`ZBD/C1.CAB`); `IsoImage.Files` lists every file for a walk that needs one.

## Evidence and limits

`CSVM.Tests/IsoImageTests.cs` builds images in code: a root, `GAME` with nested folders, a
versioned name, a mixed-case name without a version, a name ending in a period, a cabinet and a
file spanning three sectors. It covers lookups, `Exists`, seeking reads, a missing file, `..`
segments, the install rule, a directory loop and five malformed images. An image written by
Windows' IMAPI2 (ISO 9660 only) of the root archives, both `.rof` files and `strings.dll` read back
byte-identical to the install. An IMAPI2 image of a file copy of the German disc (not the pressed
disc's sectors, which SafeDisc keeps from imaging tools) opens rooted at `GAME`. Through the
cabinet layer every ZBD archive in it parses, and all but the two localised sound archives match
the English install's bytes. `IsoImageDataTests` runs that check when `CSVM_TEST_ISO` names an
image (an `.iso`, or a `.cue`/`.ccd`) or `CSVM_TEST_DISC` a disc folder
([cabinet.md](cabinet.md#evidence-and-limits)).

`CSVM.Tests/RawCdImageTests.cs` stores that built image as `MODE1/2352`, `MODE2/2352`,
`MODE2/2336` and `MODE1/2048` tracks, with noise in every header and error-correction byte, and
reads each back file for file. It covers a `.ccd` in modes 1 and 2, a bare `.img` with its sheet
beside it, a `FILE` name in another case, with a foreign absolute path or in a subfolder, a
mixed-mode sheet whose data track ends at the audio, a per-track dump, audio first, audio only,
thirteen malformed cues, four malformed `.ccd` sheets and a `.ccd` without its `.img`. The data test runs when
`CSVM_TEST_CUE` names a `.cue` or `.ccd`: it lists every file from the disc root, reports the
mode 1 sectors whose sync, mode byte or EDC is wrong, and with `CSVM_TEST_CUE_COPY` naming a file
copy of the same disc, compares every file byte for byte.

A raw dump of a Crimson Skies pressing (`CRIMSON.cue`, one `MODE1/2352` track of 294,625 sectors,
volume `C_SKIES`, created 2000-08-27) reads: 112 files, 45 below `GAME`, and the install rule
holds. Its 579 sectors with a bad EDC are isolated single sectors between sectors 807 and 10,018,
in the gap after `00000001.TMP` (sector 40) and before the first file (sector 10,040): SafeDisc's
deliberately bad sectors, which no file covers. That dump is the English-language release
(`00000409` locale files, `US.PDF` with Danish, Norwegian, Finnish and Swedish manuals): 32 of the
35 non-cabinet `GAME` files it shares with the English install are byte-identical, and the three
that differ are `CRIMSON.EXE`, `CRIMSON.ICD` and `EBUEULA.DLL`. A file copy of the German release
matches it in 65 of 112 files; the root archives other than the sound ones are among them.
