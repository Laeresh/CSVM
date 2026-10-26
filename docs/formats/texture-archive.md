# Texture archives

The container of every chapter's `texture.zbd` and its five `rtextureN.zbd` quality tiers, and of
the root `rimage.zbd` (menu, briefing and HUD art): a table of names, then each texture's header and
pixels back to back. CSVM reads it with `CSVM/src/Mech3/TextureZbd.cs`. What the header's two flag
words mean to the original's renderer is in [org/textures.md](../org/textures.md); this page is the
file layout and the decode to 8-bit RGB(A).

## Contents

- [Conceptual model](#conceptual-model)
- [Layout](#layout)
- [Pixel decode](#pixel-decode)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

An archive is a flat list of named textures, each either 16-bit RGB565 or 8-bit indices into a
palette of RGB565 colours, with an optional 8-bit alpha plane. It is not the named-entry container
of [zbd-archive.md](zbd-archive.md). The `rtextureN` tiers hold the same names as their chapter's
`texture.zbd`, downscaled ([README.md](README.md#extraction-map)).

## Layout

All values are little-endian.

| Region | Contents |
|---|---|
| `0x00` | file header, 24 bytes |
| `0x18` | *N* texture entries of 40 bytes |
| then | *P* global palettes of 512 bytes (256 RGB565 colours) |
| then | each texture's record, in entry order, with no gap; the last ends at the end of the file |

File header:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 4 | zero |
| `0x04` | 4 | `1` |
| `0x08` | 4 | global palette count *P*, signed |
| `0x0C` | 4 | texture count *N*, above zero |
| `0x10` | 8 | zero |

Texture entry:

| Offset | Size | Field |
|---|---|---|
| `0x00` | 32 | name, ASCII, NUL-terminated and NUL-padded (`sky1`, no extension) |
| `0x20` | 4 | offset of the texture's record from the start of the file |
| `0x24` | 4 | global palette index, or `-1` for none |

Texture record: the 16-byte header of [org/textures.md](../org/textures.md#the-16-byte-texture-header)
(storage flags, width, height, a zero word, palette count, render-flags word), then its data:

| Palette count | Data, in order |
|---|---|
| 0 | width x height RGB565 texels; then, for the `Full` class, width x height alpha bytes |
| 1 to 256 | width x height palette indices; for `Full`, the alpha plane; then, without a global palette, the local palette of *count* RGB565 colours |

## Pixel decode

- **Colour.** Each RGB565 channel widens to 8 bits by rounding to nearest: `round(v x 255 / 31)`
  for red and blue, `round(v x 255 / 63)` for green, halves rounding up. A paletted texel takes its
  palette entry, widened the same way; a global palette contributes its first *count* colours.
- **Alpha class**, from the storage flags byte alone (bits `0x02`, `0x04`, `0x08`), never from the
  pixels: `0x04` is `None`; otherwise `0x02` must be set, and `0x08` makes it `Full`, its absence
  `Simple`.
- **Alpha values.** `None` has no alpha channel. `Full` takes the alpha plane byte. A 16-bit
  `Simple` texture gets an alpha channel that is 0 where the texel is `0x0000` and 255 elsewhere.
  ⚠ A paletted `Simple` texture gets **no** alpha channel: its data names no clear index, so it
  decodes as opaque RGB while its class still reads `Simple`.
- **Names.** A name already taken earlier in the same archive (compared with case) is renamed
  `stem-N.suffix`, split at the last period, or `name-N` without one, with the smallest `N` from 1
  not yet taken; a rename is itself taken. That is the collision rule of
  [zbd-archive.md](zbd-archive.md#reader-rules-and-edge-cases). The record keeps the stored name
  beside its rename.

## Reader rules and edge cases

- **Tiling is the integrity check.** There is no checksum. Each record must start at its entry's
  offset, which is where the previous one ended, and the last must end at the end of the file. A
  palette index beyond the palette count is refused.
- **Storage flags are checked, not trusted.** Bit `0x01` is set on every texture, paletted ones
  included, so it does not say whether the data is 16-bit; the palette count does. Bit `0x10` must
  agree with the entry's palette index. The runtime markers `0x20`, `0x40` and `0x80` are ignored.
- **The render-flags word is kept raw.** The extraction spells it as an enum whose `Vertical` is 1
  and `Horizontal` is 2.

## Evidence and limits

`CSVM.Tests/TextureZbdTests.cs` decodes all 49 archives of `ZbdManifest.Expected` from the install
and compares every texture with today's extraction: the manifest's name, rename, alpha class,
size and render-flags word, and every PNG pixel, with its colour type deciding RGB or RGBA. The
census behind the rules above covers all 37,004 textures of the English install:

| Shape | Textures |
|---|---|
| 16-bit, storage flags `0x05` / `0x0b` / `0x03` (`None` / `Full` / `Simple`) | 15,399 / 15,343 / 137 |
| paletted with a local palette, `0xa5` / `0xab` / `0xa3` | 3,089 / 3,014 / 22 |
| paletted with a global palette | 0 |

No archive ships a global palette, every paletted texture is in a `texture.zbd`, and no name
repeats within an archive, with or without regard to case, so the rename rule and the global
palette path are exercised only by the hand-encoded archives of the unit tests. Render-flags words
seen: 0, 1, 2, 3, 4, 7 and 8.
