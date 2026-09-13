# The texture header and the blend rule, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim below names the function it came from.

Everything here is a description of *behaviour and layout*. No decompiler output is reproduced; the
addresses are given so any claim can be re-checked at source.

**What this page settles.** Whether a sprite is drawn additively or alpha-mixed is a property of its
**texture**, carried in the texture file's own header. It is not a property of the emitter, the
`COLORS` ramp, or the sprite's brightness. The particle runtime that consumes this is
[`puffer.md`](puffer.md); the authored side is [`formats/effects.md`](../formats/effects.md).

⚠ **This page is a decode, not a proposal.** Where CSVM deliberately differs, that is listed at the
bottom rather than hidden.

## Function map

| Address | Role |
|---|---|
| `FUN_0052f7f0` | Reads the 16-byte texture header and distributes it into the image object |
| `FUN_0052fa00` | Allocates the object (`calloc(1, 0x40)` via `FUN_0052f670`), reads header then pixels |
| `FUN_0052f860` | Reads pixels, the optional alpha plane, and the local palette |
| `FUN_0052f680` | Bytes per pixel: `1 + (storageFlags & 1)` |
| `FUN_0057d5a0` | Computes `log2(width)`/`log2(height)` and the UV shift constants |
| `FUN_00530900` | `fread`s the gamez texture directory: `count` records of `0x2c` bytes |
| `FUN_00530a10` | Texture lookup by name over that directory |
| `FUN_00531cb0` | Loads one record: `record[0] = FUN_00531b60(record+0x0c /* name */, 0)` |
| `FUN_00531800` | Binds by name: the lookup above, else a new slot queued for deferred load |
| `FUN_005318a0` | Allocates a registry slot, reusing a free one before growing the array |
| `FUN_005309d0` | Queues a slot index onto the pending-load list |
| `FUN_00531d80` | Flushes that queue (`FUN_00531e50` is the level-init caller) |
| `FUN_00531b60` | The load: the archive walk, one retry, then the loose-file loaders |
| `FUN_00531900` | Walks every loaded texture source looking for one name |
| `FUN_004c59a0` | Per-polygon bind by texture ID, and the "using default" fallback |
| `FUN_00463f40` | Level init: opens `textures.zrd`, loads the `COMMON` set, sets the search path |
| `FUN_005a4210` | Immediate quad path: sets `SRCBLEND`/`DESTBLEND` from the render flags |
| `FUN_005a6160` | Deferred transparent-list flush: sorts, then sets `DESTBLEND` per polygon |
| `FUN_005a5fa0` | The transparent-list sort itself |
| `FUN_005b80a0` | Script-command dispatch, including `TextureAdditiveTransparent` |
| `FUN_005d1940` | 2D screen-quad blitter, which toggles the same bit for HUD quads |
| `FUN_00532060` | Links the `_1`/`_2` siblings into a mip chain on the registry slot |
| `FUN_00531f00` | Walks that chain N links and hands back the level's image |
| `FUN_00584ef0` | The SOFTWARE rasteriser's per-polygon level choice |
| `FUN_0057d640` | Toggles software mipmapping (`DAT_00a0ca54`, on at init) |
| `DAT_009be728` → `0x005a1840` | The hardware upload: counts the chain, creates a D3D mipmap surface |
| `FUN_0059df30` | The `MipBias` command: clamps to `[-1, 1]` into `DAT_00a10324` |
| `FUN_005a0c00` | Flushes it as `SetRenderState(46 /* MIPMAPLODBIAS */, …)` |

## The 16-byte texture header

`FUN_0052f7f0` does one `fread` of exactly `0x10` bytes and distributes it. The image object is
`0x40` bytes, zeroed by `calloc`.

| Header | Size | Object | Meaning | Extractor field |
|---|---|---|---|---|
| `0x00` | u32 (low byte used) | `+0x09` | storage flags, see below | `flags` |
| `0x04` | u16 | `+0x04` | width | `width` |
| `0x06` | u16 | `+0x06` | height | `height` |
| `0x08` | u32 (low byte used) | `+0x08` | secondary byte; no reader traced | `zero08` |
| `0x0C` | u16 | `+0x0e` | palette entry count | `palette_count` |
| `0x0E` | u16 | `+0x0c` | **render flags**, see below | `stretch` (misnamed) |

Derived, not from the file: `+0x00` is `width * height` (`FUN_0052f700`), and `+0x0a`/`+0x0b` are
`log2(width)`/`log2(height)`, computed by `FUN_0057d5a0` along with the UV shift constants at
`+0x24`/`+0x28`/`+0x2c`.

⚠ **`+0x09` and `+0x0c` are two different fields and both look like "flags".** The blend bit is in
`+0x0c`. Reading bit 2 of `+0x09` instead gives an unrelated answer.

### The storage flags byte (header `0x00` → object `+0x09`)

| Bit | Meaning | Evidence |
|---|---|---|
| `0x01` | 2 bytes per pixel (16-bit rather than 8-bit paletted) | `FUN_0052f680` returns `1 + (flags & 1)`, and that is the multiplier in `FUN_0052f6b0`'s buffer size |
| `0x02` | the texture has an alpha channel; the hardware draw routes a polygon carrying it through the sorted transparent queue, and the software draw alone skips its lighting | `FUN_00554550` tests it for sorting, blending and the shadow and lightmap passes only; `FUN_005524d0` skips the per-vertex light evaluation on it, see [`vertexLighting.md`](vertexLighting.md) |
| `0x04` | the texture has none; complementary to `0x02` in every shipped header | read off all eight `texture.zbd` directories against the extractor's own `alpha` field |
| `0x08` | a separate alpha plane follows the pixel data | `FUN_0052f860` reads `width*height` further bytes into `+0x14` only when set |
| `0x10` | use the global palette, skip the local one | `FUN_0052f860` skips the local palette read when set |
| `0x20` | image loaded (runtime marker) | set by `FUN_0052fa00` after the pixel read |
| `0x40` | alpha plane loaded (runtime marker) | set by `FUN_0052f860` |
| `0x80` | palette loaded (runtime marker) | set by `FUN_0052f860` |

The extractor's `alpha` enum (`None`/`Simple`/`Full`) is derived from bits `0x02`/`0x04`, and the
third bit of the set, `0x08`, is what separates `Full` from `Simple`. A no-alpha texture ships
`0xa5`, a full-alpha one `0xab`, a simple-alpha one `0xa3`.

⚠ **Bit `0x02` is not only a storage property.** On the hardware draw, the one every retail capture
shows, it routes a textured polygon through the sorted transparent queue with blending on and
z-write off, and keeps it out of the shadow and lightmap passes; it never touches the polygon's
colour. Only the software draw reads it as a lighting exemption. Both are in
[`vertexLighting.md`](vertexLighting.md).

## The render-flags word (header `0x0E` → object `+0x0c`)

This is the field the extractor currently calls `stretch` and spells as an enum. It is a bitfield.

### Bit 2 (`0x04`) is the additive-transparent flag

`FUN_005a4210`, the immediate quad path, is the explicit form. Reading the bit off the texture
object it computes both blend factors:

```
destblend = (~flags & 4) | 2              -> set: 2 (D3DBLEND_ONE)   clear: 6 (INVSRCALPHA)
srcblend  = (-(flag) & 0xFFFFFFFD) + 5    -> set: 2 (D3DBLEND_ONE)   clear: 5 (SRCALPHA)
```

So the bit set means `ONE, ONE` (pure additive) and clear means `SRCALPHA, INVSRCALPHA` (standard
alpha mix). `ALPHABLENDENABLE` (`D3DRS` `0x1b`) is set separately from the material's alpha op.

Most effect sprites do not take that path. A quad whose vertex alpha is below 1.0 is diverted to the
deferred, depth-sorted transparent list (`LAB_005a4580` at `005a45ac`–`005a45bb`). The flag is
captured into the queued polygon's bit 31 at enqueue time (`TEST byte ptr [EAX + 0xc], 0x4` at
`005a4a84`, and the matching test in `FUN_005a4b70`), and the list is flushed by `FUN_005a6160`,
which sets **only** `DESTBLEND` per polygon from that bit (`((~poly[1] >> 31) << 2) | 2`, so 2 or 6)
and restores 6 at the end. It never sets `SRCBLEND`.

⚠ **Additive therefore differs between the two paths.** Immediate is `ONE, ONE`; the sorted
transparent pass is `SRCALPHA, ONE`, because `SRCBLEND` is left at whatever the last immediate draw
set. Particle sprites take the sorted path, so `SRCALPHA, ONE` is the one that matters for effects.

Two further writers of the same bit, neither of them the data path:

- **`TextureAdditiveTransparent <texture>`**, a script command (string at `0x0063e40c`, handled in
  `FUN_005b80a0` at `005bc10d`, setting the bit at `005bc14e` after a name lookup through
  `FUN_00530a10`). **Unused in retail.** Scanning every file in the install finds the keyword only
  inside `crimson.exe` itself; the whole `TextureAdd*` command family is absent from shipped data,
  and there is no `data\` tree (the search path at `0x00625b94` points at a dev-only location).
  Controls for that scan: `CycleTextureSetMap` and `SetTextureDirectory` are both found in
  `ZBD\interp.zbd`, so the method does find shipped command names.
- **`FUN_005d1940` case 2**, the 2D screen-quad blitter, which sets the bit for one HUD draw mode
  and clears it again in its sibling cases (`005d19e2` sets, `005d19ec` clears). This explains why
  the HUD hilite and indicator textures appear in the census below.

### The other bits, named by data correlation only

⚠ **Bits 0, 1 and 3 are not traced in the executable.** The names below come from which textures
carry them, and from the extractor's existing spelling. Treat them as provisional.

| Bit | Provisional name | C1 textures carrying it |
|---|---|---|
| `0x01` | `STRETCH_U` | `cliff01_trans1/2`, `compassticks2`, `dougfirtree1`, `firtree1/2`, `river2`, `sky1`, `terpat02_trans2` (plus everything in `Both`) |
| `0x02` | `STRETCH_V` | `tracer1`, `water1_trans1` (plus everything in `Both`) |
| `0x08` | `UNK3` | `cloud1`, `cloud2`, `rotorblur`, and nothing else |

Values observed across the install: 0, 1, 2, 3, 4, 7, 8. `Both` (3) is `lkshad2/3/5/6`,
`nitroprop`, `splash01/02/03`, `splashbase`, `tether_field`, which is consistent with a UV wrap or
stretch pair.

## The additive census

Textures with bit 2 set, in full. **C1: 31 of 881. C3: 26 of 732.**

| Group | C1 |
|---|---|
| Fire flipbook | `fire101` … `fire112` (raw value 7, so additive plus both stretch bits) |
| Lens flares | `bigflare01`, `bigflare02`, `lflare1` … `lflare4`, `light_flare`, `light_flare_r` |
| Impact rings | `ring_ap`, `ring_he`, `ring_sonic` |
| HUD | `bluehilite`, `blueindicator`, `redhilite`, `redindicator`, `yellowhilite`, `yellowindicator`, `lowalt`, `stall` |

Nothing else in 881 textures carries it, and every member is a glow or emissive element. C3 is the
same set minus `bigflare01/02` and the impact rings. That correlation is what identifies the bit;
it is not an inference from the extractor's field name.

**Sprites that do NOT carry it**, which is every sprite any puffer names: `fire_f01`, `fire_f02`,
`fire_f06`, `smoke101`, `smoke102`, `smoke103`, `exp_yel01`, `thickblksmoke`. All alpha-mixed.

## What this means for particles

Blend is looked up from the texture bound at draw time. A puffer picks its texture per particle
(`TEXTURES` pool) and per frame (`TEXTURE_SEQUENCE`), so **blend is a per-particle, per-frame
property, not a per-emitter one**. A flipbook that crosses from a flagged frame to an unflagged one
changes blend mid-life.

The engine's rule, whole:

> A particle is drawn additively if and only if bit 2 of its current texture's render-flags word is
> set. Otherwise it is alpha-mixed. The `COLORS` ramp and the sprite's brightness play no part.

## The transparent list is depth-sorted

`FUN_005a6160` fills an index array in reverse submission order (`005a61f5`–`005a620b`, writing
`count - 1 - i`), calls `FUN_005a5fa0` to sort it, then draws in the sorted order. The queued
records are `0x1c` bytes: the texture object at `+0x08`, the quad's smallest reciprocal depth at
`+0x10` (computed at enqueue, `005a4b42`–`005a4b54`), and the sort key written into `+0x14`.

The sort runs three passes, and their **precedence** is what the rule turns on:

| Pass | Comparator | Field | Effect |
|---|---|---|---|
| 1 | `LAB_005a5f00` | `+0x14` | `key[b] - key[a]`, so descending: **farthest first** |
| 2 | `LAB_005a5f30` | `+0x08` | each equal-KEY run re-sorted by texture pointer, ascending |
| 3 | `LAB_005a5f60` | `+0x10` | each equal-key-and-texture run re-sorted by `minRHW` bits, ascending |

The key is built at `005a5fc2`–`005a6003`. `minRHW` is compared against `0.0` (`0x006032c8`), and a
quad at or behind the eye takes the sentinel `0x3e7` = **999** (`005a5ff2`); otherwise the key is
`ftol(0.01 / minRHW)` with the `0.01` at `0x006032d8`. Since `minRHW` is the reciprocal of the
quad's FARTHEST vertex depth, that is `trunc(0.01 × depth)`: **an integer bucket 100 units wide**,
and the sentinel is the bucket a polygon 99,900 units out would occupy, well past any level's far
clip. Pass 3's integer subtraction of two positive IEEE-754 floats is a valid float compare, so
within one bucket and one texture the order is exact depth, farthest first. Setting `DAT_009be6ec`
(written by the one-line setter at `005a5f90`) skips passes 1 and 2 and leaves pass 3 alone.

So the original **does** sort its transparent polygons back to front, across the whole frame rather
than within one emitter, and it does not draw them in submission order. Two consequences:

- **There is no notion of emitter or object.** A gamez alpha polygon and a particle sprite are
  interleaved by the same key, so "order between emitters" and "order against world transparent
  geometry" are the same question as "order within an emitter".
- ⚠ **Pass 2 outranks exact depth, so inside one 100-unit bucket TEXTURE decides.** Two overlapping
  sprites of different textures at nearly the same distance are ordered by texture pointer, which
  is heap-allocation order and not readable from a static binary. That pass exists to hold texture
  binds down (its own equal-key grouping), and it is the one part of the rule a remake drawing a
  packed atlas has nothing to buy with: one texture per emitter makes every particle share pass 2's
  key, leaving passes 1 and 3, which are both depth, farthest first.

## The two rasteriser dispatch entries

`FUN_0054e6e0` ends in `FUN_0057c5c0(pos, radius, texture, alpha, hasColourRamp)`, and on the
hardware path that fifth argument picks between two entries installed by `FUN_005a8c00`. They differ
in exactly two things, and **neither of them is blend**.

| | `DAT_009be78c` → `LAB_005a4580` (no ramp) | `DAT_009be790` → `FUN_005a4b70` (ramp) |
|---|---|---|
| `SHADEMODE` (`D3DRS` 9) | `1` = FLAT (`005a4727`) | `2` = GOURAUD |
| Vertex diffuse | forced to white: `0xFFFFFFFF` immediate (`005a4642`, `005a468f`), or `(alpha*255) << 24 \| 0x00FFFFFF` deferred (`005a48ed`–`005a4930`, `005a4a12`–`005a4b19`) | copied through untouched, preserving the ramp colour |
| `SRCBLEND` / `DESTBLEND` | never touched | never touched |

The ramp-less white and the `(1 − ageFrac)` alpha envelope are written earlier, by `FUN_0054e6e0`
itself; the ramp-less entry then re-applies white on top.

## Name resolution: one global registry, no chapter scoping

The engine keeps a single flat texture registry, the array at `DAT_0072c17c`: stride `0x2c`, live
count in `DAT_0072c178`, a hard cap of `0x1000` entries, the name at entry `+0x0c` and an in-use
field at entry `+0x20`. `FUN_00530a10` is a linear name scan over it, and it has no notion of which
chapter contributed an entry. **There is no per-chapter texture scope, so there is also nothing to
fall back from**: a name resolves if any loaded source supplied it and fails if none did.

Level init (`FUN_00463f40`) fills that registry from three places. It opens `textures.zrd`, loads
the `COMMON` set at a detail level derived from a driver query (falling back to a `DEFAULT` set),
and sets a loose-file search path of `..\data\common\textures;..\data\common\effects\textures`,
which is a dev-only location absent from a retail install.

A miss does not fail immediately. `FUN_00531800` allocates a slot (`FUN_005318a0`), copies the name
in, marks it state 2 and queues the index (`FUN_005309d0`, onto the 20-entry pending list at
`DAT_00758204` counted by `DAT_0075822c`). `FUN_00531d80` flushes the queue, and level init calls it
twice through `FUN_00531e50`, once after the texture-load stage and once after `effects.zrd`. Each
queued slot then goes to `FUN_00531cb0`, which resolves in this order:

1. `FUN_00531b60`, which calls `FUN_00531900` (a walk over every loaded texture source), retries
   once with `DAT_00758234` toggled, then tries the two loose-file loaders `FUN_00534cf0` and
   `FUN_00534060`.
2. An optional secondary resolver at `DAT_00758250`. ⚠ **Never installed in retail**: its only
   cross-reference in the binary is the read in `FUN_00531cb0` itself, so the pointer is null and
   the branch is dead.
3. The terminal fallback, `&DAT_006351f0`.

### `DEFAULT_TEXTURE`, the built-in fallback image

`DAT_006351f0` is a static image descriptor compiled into the executable: `08 00 08 00` at `+0x04`
is its 8x8 size, and `+0x10` points at 128 bytes of 16-bit pixels at `0x00635170`. Its registry
entry is `PTR_DAT_00635230`, carrying the name `DEFAULT_TEXTURE` at the usual `+0x0c`. The pixels
are a red/green checkerboard, `0xF800` and `0x03E0` in alternating pairs.

The per-polygon binding site is `FUN_004c59a0` (`gg_load`, line `0x27b`). It scans the model's
texture table for the polygon's texture ID, and on no match, or when the bind returns zero, it logs
`Failed to find texture ID (%d) using default` and binds the texture literally named `default`. No
archive in the install ships that name either, so it lands on the same checkerboard.

### What that means for a referenced-but-absent texture

The install ships `cloud1`/`cloud2` in seven of the eight chapters' `texture.zbd`. C3 ships neither,
although its skydome (node `g1155`, model 492) names both on two of its 25 polygons. No root-level
archive supplies them: `rimage.zbd`, the shared image pool, has neither, and the only occurrences
outside a chapter archive are three copies of a puffer definition in `zrdr.zbd` listing
`TEXTURES cloud1 cloud2 smoke101 …`, which references the names rather than supplying pixels.

⚠ **The decode and the screen disagree here, and the screen wins.** The chain above says C3's two
cloud polygons should draw the red/green checkerboard. At the controls the original shows nothing
below the aircraft in C3, so something upstream of the bind is dropping those polygons and the
fallback never reaches the screen. The route by which that happens is not decoded. What is settled
is the part that matters for the remake: **there is no shared pool the names could have come from**,
so a cross-chapter lookup would be inventing engine behaviour that does not exist.

The field is currently read as `stretch: u16` and spelled as an enum, which loses the bit structure
and hides the additive flag behind an `Unk` name. Suggested:

```
render_flags: u16     // was: stretch
  bit 0  STRETCH_U             (provisional, not traced)
  bit 1  STRETCH_V             (provisional, not traced)
  bit 2  ADDITIVE_TRANSPARENT  (traced: FUN_005a4210, FUN_005a6160)
  bit 3  UNK3                  (provisional, not traced)
```

The existing enum values map as: `None` = 0, `Horizontal` = 1, `Vertical` = 2, `Both` = 3,
`Unk4` = additive alone, `Unk7` = additive plus both stretch bits, `Unk8` = bit 3 alone.

Nothing else in the header needs renaming; `flags`, `width`, `height`, `palette_count` and `zero08`
all match the engine's use.

## The mip chain, and what selects a level

The `_1`/`_2` siblings that no material names ([formats/gamez.md](../formats/gamez.md)) are a mip
chain the texture system builds itself. `FUN_00532060` runs on every load
(`FUN_00531cb0` calls it right after the pixels arrive): it copies the base name, appends `_1`,
and loops, linking each level it resolves onto the previous slot's `+0x28` and incrementing the
last character, so `_1` becomes `_2` and so on until a name misses. Each level's image also gets
`+0x1c` = base width / level width, the 2, 4, … scale. A slot already in use as a texture in its own
right is refused with *"Mip file %s used externally. Mip chain broken."*, which is the engine
saying these names are levels and not surfaces.

**The two rasterisers then choose a level by different rules.**

The software rasteriser chooses per polygon. `FUN_00584ef0` takes the polygon's widest projected
texel span, rounds it, halves it, and asks `FUN_00531f00` for that many links down the chain. It is
gated on `DAT_00a0ca54`, which the software init `FUN_0057ced0` sets to 1 and a debug toggle
(`FUN_0057d640`, the *"MIP Mapping Enabled/Disabled"* console line) flips.

The hardware draw hands the whole chain to Direct3D. `DAT_009be728` (`0x005a1840`, installed by
`FUN_005a8c00`) walks `+0x28` to count the levels and, when there is at least one, sets
`dwFlags |= DDSD_MIPMAPCOUNT` (`0x005a1ca0`), `ddsCaps.dwCaps |= DDSCAPS_MIPMAP | DDSCAPS_COMPLEX`
(`0x005a1caa`) and `dwMipMapCount` = 1 + the chain length (`0x005a1cb8`) before `CreateSurface`.
So the authored levels **are** the D3D7 mip chain, and which one a pixel reads is the card's own
decision, offset by one engine knob.

### The knob is `MipBias`, and only C5 sets it

`MipBias <f>` is a boot-script command (`FUN_005b80a0` at `0x005ba8a4`). It applies on the hardware
path only, clamps its argument into `[-1, 1]` (`FUN_0059df30`) and is flushed as
`SetRenderState(46, …)`, `D3DRENDERSTATE_MIPMAPLODBIAS`, by `FUN_005a0c00`. The device default is
0 (`FUN_005a8800` at `0x005a8857`). It is one global state for the whole device, not a per-texture
setting, and nothing resets it between chapters.

Five scripts in the install carry the command. Four are `support\c{1,2,3,4}\load.gw` at `-1.0`, and
`load.gw` is the data-compile path: `support\main.gw` sources it only in the `ifndef USEZBD` arm,
where it loads `%CAMPAIGN_DIR%\terrain\*.flt`, and a retail install ships no `data\` tree and no
`.flt` at all. The fifth is `support\c5\adjust.gw` at **`-0.8`**, and `adjust.gw` is sourced
unconditionally. So a retail run applies exactly one mip bias, C5's, and every other chapter draws
at the device default.

A negative bias selects a sharper level: each transition radius moves out by `2^0.8` = 1.741. The
C5 ground is 256² texels over a 256 m tile, one texel per metre
(`analysis/item9-depth-bias/CBLOCK-LOD.md`), and the original's external views are 60° horizontal
at 4:3 ([cameraViews.md](cameraViews.md)), a 23.41° vertical half-angle, so at 640×480 a face-on
surface subtends 554 px per metre-at-one-metre. Level 1 is therefore first fully read at
`2^1.8` texels per pixel, 1930 m out, where C5's `ZONE1` fog is already 57 % of the way to its pure
black and the far clip stands 570 m beyond ([weather.md](weather.md)). At a grazing angle the
density rises and the radius shrinks, by the same factor in both the biased and unbiased case.

## Where CSVM differs today

`TextureArchive.RenderFlags` carries the word off each archive's own extraction manifest (the
`stretch` field) and `IsAdditive` tests bit 2; a name the archive cannot resolve, and a PNG-only
tree with no manifest, read as 0 and therefore alpha-mix, which is the engine's own fallback.
`Puffer.Create` asks it once per atlas column, so the verdict is per particle and per flipbook
frame; `MultiMeshEmitterRenderer` draws a column set that spans both blends as two MultiMeshes and
routes each particle by the column it is showing. Godot's `blend_add` is `SRC_ALPHA, ONE`, which is
the sorted transparent pass's additive and so the one that applies to particles.

**No shipped puffer sprite carries the bit**, so every puffer in the install alpha-mixes and the
split path is reachable only by a frame list nothing authors. The flagged textures are consumed by
mesh polygons and the HUD instead: the `fire101`…`fire112` flipbook, the lens flares, the impact
rings and the HUD hilites. Those draw through `SceneBuilder`, which does not read the word yet.

⚠ **The word is per archive, not per name.** `bigflare01`, `ring_he` and `beflare5` are flagged in
some chapters and not in others, so an install-wide name table would answer wrongly for whichever
chapter it was not built from.

`TextureArchive.LastAlphaIsSoft` calls a texture's alpha **soft** when fewer than 45% of its ink
texels (alpha >= 32) are truly opaque (alpha >= 200), measured install-wide in
`analysis/alpha-classification/`. Soft ink must not scissor: a 1-bit cut at 0.5 both erases
sub-threshold ink and solidifies the partial alpha above it, so only essentially-binary ink
(fences, trees) survives the cut. `SoftAlphaCoastline` names the five waterline sheets the ratio
misreads, whose solid dry-land half outvotes the feathered ramp that is the point of the texture;
the inland transition sheets measure 0.81 to 0.93 binary and are genuine cutouts. This is a
different question from `LastAlphaClass`, the extractor's own `None`/`Simple`/`Full` field, which
is the header bit itself and the only reader that sees the `Simple` textures; no lighting decision
keys on it, since the original's hardware draw has none ([vertexLighting.md](vertexLighting.md)).

`TextureArchive` carries two absent-name sets rather than one, because the retail data lacks
textures for two different reasons. `KnownAbsentFromGameData` (`pir_spinner`, `barngrill`) draws a
neutral gray card; `AbsentAndUndrawn` (`cloud1`, `cloud2`) drops the polygon, which is what C3's
skydome needs. ⚠ Membership of the second set is not enough on its own: `IsAbsentAndUndrawn` also
requires the lookup to fail, so the seven chapters that do ship the pair keep drawing it.
`SceneBuilder.UndrawnPolygonCount` reports 2 for a C3 world build and 0 for every other chapter,
which is the tripwire if that ever stops being true.

The alpha-weighted luminance of the sprite a particle dies on decides one thing only, and it is not
blend: the soft-particle depth fade, a render nicety with no counterpart in the original
([puffer.md](puffer.md)).

### The depth order, and where ours stops being the original's

The order is ported in two halves that meet at the draw call.

Within an emitter, `Puffer._Process` buffers the frame's finished draw payloads and writes them to
the renderer sorted on view depth against pane 0's camera, farthest first, which is the rule's
passes 1 and 3 collapsed (the atlas makes pass 2's key uniform). The behind-eye sentinel is
reproduced as a 99,900 m depth, and is unreachable while the near cull is on, since that discards
an at-or-behind-eye particle before the sort sees it. The sort is `Array.Sort`, unstable, matching
pass 3's own exposure on two equal depths.

Between emitters, and against the world's transparent geometry, the sort is **Godot's**: its
transparent pass orders objects back to front per camera, on each instance's AABB centre. That is
per-pane and therefore right in splitscreen, and it is measured rather than assumed: switching
`MultiMeshEmitterRenderer`'s lists to node-origin sorting moves 8 of the 18 pinned goldens,
including three no particle-order change touches, because a trail or sustain emitter parks its own
node at the world origin.

Two divergences remain, both of granularity rather than direction:

- **Godot sorts objects, the original sorts polygons.** Two emitters whose clouds interpenetrate
  draw one wholly before the other, where the original would interleave their sprites. The same
  holds for a particle cloud against an alpha-blended gamez surface.
- **One order serves every pane.** The instance buffer is written once per frame, so the
  within-emitter order is pane 0's. This is the same seam as the shared alpha
  ([puffer.md](puffer.md), "One alpha per particle across the panes") and has the same answer:
  identical to the original wherever there is one viewer, which is every capture, freecam shot and
  single-player session. A per-pane order would take one `MultiMesh` per pane.

`TextureArchive.MipBias` reads the chapter's `adjust.gw` line and `Launcher` writes it to the
`csky_mip_bias` global, so C5 samples at -0.8 and every other chapter at 0, which is why only the
C5 golden moves when the reader is wired up. ⚠ Read `adjust.gw` and not `load.gw`: the
compile-path -1.0 above never runs at retail, and taking it would bias four chapters the original
leaves alone.

The original's is one device render state, so every mip-mapped arm here reads that global through
the one `csky_sample_albedo` in `shaders/csky_mip_bias.gdshaderinc`: the world mesh, the
camera-facing billboards, the cylindrical facades, the templates clutter and the mesh lab's
diagnostic twin. The `chapter-census` suite fails any arm that samples around it. The ambient cloud
field (`Effects/FogVolumeClutter.cs`) is the one sampler still outside that routing.

Three divergences remain on the selection itself, and the first two widen the band the authored
level 1 draws. The world sampler is `filter_linear_mipmap_anisotropic`, which the original's D3D7
hardware had no equivalent of, so a grazing ground surface holds level 0 much further out here and
crosses into level 1 as a ring rather than a gradient. The camera far plane is the remake's,
not the zone's `CLIP_RANGES` far ([weather.md](weather.md)), so the ring has room to sit past
where the original's world ends. And the sprite arms sit at level 0 over most of a C5 framing, so
the bias moves no pixel on them there; forcing the global to +4 leaves the pinned C5 shot
byte-identical while it moves 63% of the frame through the world arm.
