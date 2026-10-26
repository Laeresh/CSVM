# ZBD named-entry archive

The container `zrdr.zbd` (root, chapter and mission) and the two sound archives `soundsh.zbd` and
`soundsl.zbd` share: whole files stored back to back, with a table naming them at the end. CSVM
reads it with `CSVM/src/Extraction/ZbdArchive.cs`. What an entry holds is its family's page:
[zrdr.md](zrdr.md) for the `.zrd` reader files, [sounds.md](sounds.md) for the `.wav` sounds.

## Contents

- [Conceptual model](#conceptual-model)
- [Layout](#layout)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

An archive is a folder flattened into one file: no compression, no directories, no alignment. The
other `.zbd` files of an install (`interp`, `gamez`, `planes`, `texture`, `rtextureN`, `rimage`,
`cam_anim`, `mis_anim`) are formats of their own and do not end in this table; the texture ones
are [texture-archive.md](texture-archive.md). The saved-game
files use the same 148-byte entry and two-word tail ([saved-games.md](saved-games.md)).

## Layout

Read it back to front.

| Region | Contents |
|---|---|
| `0x00` .. *T* | entry payloads, in table order, with no gap between them |
| *T* .. *T* + 148 x *N* | the table, *N* entries of 148 bytes |
| last 8 bytes | the version `1`, then the entry count *N* |

The table starts at *T* = file size - 8 - 148 x *N*. Each entry:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 4 | payload offset from the start of the file |
| `0x04` | 4 | payload length in bytes |
| `0x08` | 64 | entry name, ASCII, NUL-terminated (`aiv.zrd`) |
| `0x48` | 4 | a flags word: `2` or `62` in most entries, uninitialised bytes in the rest |
| `0x4C` | 64 | a copy of the name in every shipped entry |
| `0x8C` | 8 | a Windows `FILETIME`; every shipped entry's falls on 26 August 2000 |

All values are little-endian. Nothing after the name is read by CSVM.

## Reader rules and edge cases

- **There is no checksum.** Version 1 carries none, so the reader checks the table instead: the
  first payload starts at 0, each next one where the last ended, and none runs into the table.
  In every shipped archive the last payload ends exactly at *T*.
- **Names repeat.** `soundsh.zbd` and `soundsl.zbd` each hold 44 entries whose name an earlier
  entry already has, plus one pair differing only by case; the root `zrdr.zbd` holds `player.zrd`
  twice.
  The reader keeps every entry in table order under its stored name. A lookup by name has to pick
  one of them; today's extraction wrote a repeat as `stem-N.ext`, counting from 1.
- **Case.** Names are matched as stored. The archive's own path in an install is matched without
  regard to case (`InstallLocator.ResolveFile`).
- **Size.** `soundsh.zbd` is about 128 MB, so the reader keeps the file open and reads one payload
  at a time rather than loading it whole.

## Evidence and limits

`CSVM.Tests/ZbdArchiveTests.cs` reads all 64 archives of this layout in `ZbdManifest.Expected`
through the install: every table tiles to *T*, every entry name matches today's extraction, and
every sound payload's CRC32 matches the one its zip records. The flags, comment and `FILETIME`
values come from a census of all 6,334 entries of those 64 archives. The English install is the
only one checked.
