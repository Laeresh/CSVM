# Chapter cabinets (Microsoft CAB with LZX)

The Crimson Skies disc ships each chapter folder of an install (`ZBD/C1/`, `ZBD/C1B/`, ...) as one
Microsoft cabinet in `GAME/ZBD` (`C1.CAB`, `C1B.CAB`, ...). CSVM reads them in place:
`CSVM/src/Extraction/Cabinet.cs` reads the cabinet tables, `LzxDecoder.cs` decompresses a folder,
and `CabinetSource.cs` answers the chapter's paths from the cabinet under the names an install
uses. The disc around the cabinets is [disc-image.md](disc-image.md).

## Contents

- [Conceptual model](#conceptual-model)
- [Cabinet layout](#cabinet-layout)
- [LZX](#lzx)
- [What the disc's cabinets hold](#what-the-discs-cabinets-hold)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

A cabinet is a header, a table of folders, a table of files and the folders' data. A folder is
one compressed stream; each file is a byte range of its folder's unpacked output. The output is
cut into data blocks of 32,768 bytes (the last one shorter), each stored with its compressed size.
An LZX folder's blocks form one bitstream whose window spans block boundaries, so a folder unpacks
from its start, as a whole.

`CabinetSource` wraps another game-file source (the `IsoImage`, or the disc's `GAME` folder). A
path below `ZBD/<chapter>/` is the cabinet's when `ZBD/<chapter>.CAB` exists in the inner source,
and every other path goes to the inner source. `GameFileSource.Open` returns this wrapper for an
`.iso` and for a disc folder (`InstallLocator.Check` gives `Disc`).

## Cabinet layout

All integers are little-endian. The header (`CFHEADER`):

| Offset | Size | Field |
|---|---|---|
| `0x00` | 4 | signature `MSCF` |
| `0x10` | 4 | offset of the first file entry |
| `0x18` | 1 + 1 | version minor, major (1.3) |
| `0x1A` | 2 | folder count |
| `0x1C` | 2 | file count |
| `0x1E` | 2 | flags: `0x1` previous cabinet, `0x2` next cabinet, `0x4` reserve fields present |
| `0x20` | 2 + 2 | set id, index in the set |
| `0x24` | 2 + 1 + 1 | when flag `0x4`: header, folder and data-block reserve sizes, then the header reserve |

Each folder entry (`CFFOLDER`), from `0x24` (after any reserve): offset of its first data block
(4), data block count (2), compression type (2), then the folder reserve. The type's low four bits
are 0 none, 1 MSZIP, 2 Quantum, 3 LZX; for LZX, bits 8 to 12 are the window size in bits.

Each file entry (`CFFILE`): unpacked size (4), offset in the folder's output (4), folder index
(2, values from `0xFFFD` mark a file continued across cabinets), DOS date and time (2 + 2),
attributes (2, `0x80` means the name is UTF-8), then the name, NUL-terminated, `\`-separated.

Each data block (`CFDATA`): checksum (4), compressed size (2), unpacked size (2), the data-block
reserve, then the compressed bytes.

## LZX

**Bitstream.** The folder's data blocks are joined into one input. It is read as 16-bit
little-endian words, each consumed from its high bit. Huffman codes are canonical (shorter codes
first, then by symbol), at most 16 bits.

**Stream header.** One bit: when set, E8 translation is on and 32 bits (two words' worth, high half
first) give the translation size.

**Blocks.** Each block starts with its type (3 bits: 1 verbatim, 2 aligned offset, 3 uncompressed)
and its unpacked length (24 bits). A block may span frames.

- *Verbatim:* the main tree (256 literals plus 8 per position slot) and the length tree (249
  symbols) follow, each length coded through a 20-symbol pretree as a change from the previous
  block's length for that symbol. The main tree is read in two runs, the literals then the rest,
  each with its own pretree. A tree's lengths start at zero for the stream and carry between blocks.
- *Aligned offset:* as verbatim, preceded by eight 3-bit lengths of the aligned tree.
- *Uncompressed:* 1 to 16 bits of padding reach a word boundary (16 when already aligned), then R0,
  R1 and R2 as raw 32-bit values, then the bytes. An odd length is followed by one pad byte.

**Pretree codes.** 0 to 16: length = (previous - code) mod 17. 17: 4 bits + 4 zeros. 18: 5 bits +
20 zeros. 19: 1 bit + 4 copies of the length the next pretree code gives.

**Matches.** A main symbol of 256 or more is a match: its low three bits are the length header
(7 adds a length-tree symbol), plus 2; the rest is the position slot. Slots 0, 1 and 2 reuse R0,
R1 or R2 (1 and 2 swap with R0). A higher slot's offset is its base plus its extra bits minus 2,
and shifts R0 to R1 to R2. Slot *n* has `min((n - 2) / 2, 17)` extra bits from slot 4; the base
is the running sum of `2^extra`. In an aligned block a slot with 3 or more extra bits reads all but
the low three as plain bits and the low three through the aligned tree. A window of 2^15 to 2^21
bytes has 30, 32, 34, 36, 38, 42 or 50 slots.

**Frames.** Every 32,768 output bytes the input drops the rest of its current word. No match
crosses a frame end or a block end.

**E8 translation.** After decoding, in each frame of the first 32,768, every `0xE8` byte more than
10 bytes before the frame's end takes the next 4 bytes as a signed absolute operand *a*. At output
position *p* of the `0xE8`, when `-p <= a < size`, the operand becomes `a - p` (for `a >= 0`) or
`a + size`, and the scan resumes after it. The window holds the untranslated bytes, which is why
the translation runs over the finished output.

## What the disc's cabinets hold

The German disc's eight cabinets (10 to 16 MB each) each hold one LZX folder with a 2^21-byte
window, no reserve fields and no cabinet set, and have E8 translation on. Unpacked, a chapter is
40 to 57 MB (396 MB for all eight). Members are stored under `GAME\ZBD\<chapter>\`, rooted at the
disc: `cam_anim.zbd`, `gamez.zbd`, `texture.zbd`, `zrdr.zbd`, and `mis_anim.zbd` plus `zrdr.zbd`
per mission folder.

**The texture tiers are stored under 8.3 aliases** (`RTEXTU~1.ZBD` to `RTEXTU~4.ZBD`, and a hashed
one such as `RT60AC~1.ZBD`). Paired in cabinet order with the chapter's tier names
(`rtexture2`, `4`, `6`, `8` and the chapter's top tier, `ZbdManifest`) sorted ordinally, each alias
gets its install name: C2B's `RTEXTU~1` to `~4` are `rtexture2` to `rtexture8` and `RT60AC~1` is
`rtexture9`; C1's first alias is `rtexture15`, which sorts before `rtexture2`. An alias is a name
holding `~` that starts with `RT`.

## Reader rules and edge cases

- **Lazy, in memory, per session.** A chapter's file table is read on the first lookup into it, so
  `Exists` never decompresses. The folder unpacks into one array on the first `Open`, and only the
  chapter opened last keeps it (a launch visiting all eight would hold about 400 MB); each file is
  a read-only, seekable `MemoryStream` over its range.
- **Refused cabinets.** `InvalidDataException` for a missing `MSCF` signature, a major version
  other than 1, a cabinet in a multi-cabinet set, a file in a continued or absent folder, a name over
  256 bytes, a data block running past the file, a non-final block not unpacking to 32,768 bytes, a
  member not below `GAME\ZBD\<chapter>\`, or an alias count other than the chapter's tier count.
- **Compression types.** Stored and LZX folders unpack; MSZIP and Quantum throw, since no game
  cabinet uses them. Data-block checksums are not verified.
- **Refused LZX.** A block type of 0 or above 3, a zero-length block, over-subscribed code lengths,
  a code the table lacks, a length run past its tree, a match crossing its block or frame or
  reaching before the output's start or past the window, and input consumed past its end all throw
  `InvalidDataException`, which the cabinet rethrows naming the cabinet and folder.
- **Repeated members.** A second member with a path already taken is ignored.

## Evidence and limits

- **Byte-identical to the English install.** Through `CabinetSource`, over both
  `Z:\CSVM\tmp\CrimsonSkies-DE.iso` and its source folder, all 176 chapter archives (renamed tiers
  included) match the English install's bytes, and all 184 `ZbdManifest` archives parse with their
  readers. Only `soundsh.zbd` and `soundsl.zbd` (plain files, localised) differ.
  `IsoImageDataTests` reports this, under `CSVM_TEST_ISO` and `CSVM_TEST_DISC`.
- **Unpack time** per chapter, measured by that test on the development machine: Release 140 to
  250 ms (up to 460 ms for a run's first chapters, cold), Debug 430 to 1,080 ms. A session pays
  one unpack per chapter it reads.
- **CI-safe tests.** `CSVM.Tests/CabinetTests.cs` embeds three self-made cabinets written by
  Windows' `makecab` (LZX with two data blocks, E8 translation and aliased tiers; stored; MSZIP),
  and hand-built uncompressed LZX blocks of odd length.
- **Sources.** The decoder is written from Microsoft's published cabinet (`[MS-CAB]`) and LZX
  format specifications. The alias pairing is the rule `unzbd cs disc` applied; the byte
  comparison above confirms it for every chapter.
