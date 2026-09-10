# Craters, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function it came from, so any of
them can be re-checked at source. No decompiler output is reproduced.

**Where the other halves live.** The authored side, meaning which weapons carry `CRATER` and what
this install ships in the block, is [`formats/weapons.md`](../formats/weapons.md). The detonation
that reaches this subsystem, including the flag word the gate reads and the animation suppression a
successful carve causes, is [`ordnanceTypes.md`](ordnanceTypes.md) "Detonation: the impact, then the
splash". The decorations a crater destroys are [`clutter.md`](clutter.md). Our implementation is
`WeaponDef.Crater`, a parsed boolean the weapon lab and the probes print as a label; CSVM builds no
part of the subsystem below.

**Scope.** This page covers the **engine-wide crater template**, the **weapon sub-block** that
overrides it, **what one carve actually builds**, the **three failure paths**, and the
**bookkeeping** that decides how many craters a mission can hold.

## Function map

| Address | Role |
|---|---|
| `FUN_004e5590` | `zdec_init.cpp`: reads `declient.zrd`, fills the crater and quicksand templates and the texture table |
| `FUN_004e4870` | copies the crater template into a caller's request struct |
| `FUN_005ac690` | the weapon's carve request: fills a template copy from the hit and the weapon, then asks for the carve |
| `FUN_004e4a10` | the entry point: an optional veto hook, then the instancer |
| `FUN_004e4890` | `zdec_crater.cpp`: the instancer, and the owner of the three failure strings |
| `FUN_004e4e00` | allocates the crater instance from the request |
| `FUN_004e4a40` | **build**: locates the terrain cell, clamps the centre, lays the rim ring, rejects overlaps |
| `FUN_004e4ea0` | **clip**: clips the rim ring against the terrain (`FUN_005272e0`) |
| `FUN_004e4f00` | **tesselate**: emits the bowl's polygons and names the scene node |
| `FUN_004e4830` | frees one instance (its point array and its mesh builder) |
| `FUN_004df560` | destroys the decorations inside the crater |
| `FUN_00526f10` | commits the built geometry into the terrain cells and bumps each cell's carve counter |
| `FUN_004e5e20` | appends the carve to the record list |
| `FUN_004e5d30` | removes every carve from the world and zeroes the per-cell counters |
| `FUN_004e5b40` | teardown: empties the record list and frees the texture table |
| `FUN_005ad630` | `zwep_ini.c`: the `.zrd` weapon dispatcher, which parses `CRATER` and `MAX_CRATER_RADIUS` |

## Two blocks are called `CRATER`, and only one of them is the weapon's

`FUN_004e5590` reads a block named `CRATER`, and so does `FUN_005ad630`, but they are different
blocks in different files. `FUN_004e5590` reads `declient.zrd`, an engine-wide configuration file,
and its `CRATER` block sets the defaults every crater in the game starts from. `FUN_005ad630` reads
the `CRATER` sub-block of one weapon entry in `weapons.zrd`, whose only job is to move three of
those defaults for that weapon.

⚠ **The weapon flag is `+0x74` bit `0x10`, not bit `0x2000`.** Bit `0x2000` is written by the key
parsed immediately before `CRATER` in the same dispatcher, `CATCHES_FIRE`
(`0x005ada81`; see [`formats/anim-definitions.md`](../formats/anim-definitions.md), "The behaviours
are dead data"). The `CRATER` branch begins at `0x005ada98`, sets bit `0x10` when the block is
present and clears it when it is absent. Both bits live in the `.zrd` dispatcher's flag word at
weapon `+0x74`, which is a different bit space from the extension struct's flags
([`ordnanceTypes.md`](ordnanceTypes.md), "A second flags word").

## The engine-wide template

`FUN_004e5590` opens `declient.zrd`, and if the file is missing it logs "Failed to read %s, using
defaults" and keeps the constants compiled into the function. The file exists, once per chapter, in
each chapter's `zrdr.zbd` and not in the top-level one, and **all eight chapters ship identical
numbers**, so the template is effectively a global. The template it fills is a 40-byte struct at
`DAT_00727cb0`; the quicksand template beside it at `DAT_00727cfc` has the same layout.

| Offset | Field | Key | Compiled fallback | Crater, as shipped | Quicksand, as shipped |
|---|---|---|---|---|---|
| `+0x00` | flags | none | `0x100c` / `0x1008` | `0x100c` | `0x1008` |
| `+0x04` | rim vertex count | `POINTS` | 7 | 7 | 7 |
| `+0x08` | struck material | none, the caller fills it | 0 | | |
| `+0x0c` | slope | `SLOPE` | 0.0 / 40.0 | 30.0 | 40.0 |
| `+0x10` | depth | `DEPTH` | 4.0 | 3.0 | 5.0 |
| `+0x14` | radius | `RADIUS` | 20.0 | 20.0 | 20.0 |
| `+0x18`…`+0x20` | centre x, y, z | none, the caller fills it | | | |
| `+0x24` | filled from the impact caller's fourth argument | none | | | |

The same block also carries `DEFAULT_TEXTURE`, `DEFAULT_ANIM` and a `TEXTURE_ANIM` list. Those build
a separate table at `DAT_00727cac` (`DAT_00727ca8` rows of 12 bytes: the ground material to match,
the texture the carve is skinned with, the effect the carve plays). Row 0 is the default pair, and
each `TEXTURE_ANIM` pair adds a row keyed on a ground material. `FUN_004e4e00` picks the row whose
key equals the request's `+0x08`, falling back to row 0, so **which texture and which effect a
crater shows is chosen by the ground that was hit**, not by the weapon.

⚠ **The shipped file spells the effect key `DEFAULT_EFFECT` and the engine reads `DEFAULT_ANIM`.**
The block authors `DEFAULT_EFFECT crater_explosion`, which `FUN_004e5590` never looks up, so row 0's
effect slot stays null and `FUN_004e4890`'s spawn is skipped on its own null test. **No crater in
the original plays an effect of its own**, and the name is dead twice over: `crater_explosion`
appears in no archive but the `declient.zrd` blocks themselves, so even a corrected key would
resolve to nothing. `DEFAULT_TEXTURE default` **is** read and does skin the carve. The file also
authors `TEXTURE_PARMS [1.0, 1.0]`, which the engine does not read, and no `TEXTURE_ANIM`, so the
table is one row and the ground material never selects anything.

⚠ **`SLOPE` has no reader on the crater path.** It is parsed into `+0x0c` and none of
`FUN_004e4890`, `FUN_004e4e00`, `FUN_004e4a40`, `FUN_004e4ea0` or `FUN_004e4f00` touches that slot.

## What the weapon's `CRATER` sub-block authors

The parse at `0x005adab0` does three things. It sets `+0x74` bit `0x10`; it copies the engine
template **twice**, into weapon `+0x194` and weapon `+0x1bc` (`FUN_004e4870`, 40 bytes each, so the
two copies are adjacent); and it then reads three keys, each a two-element list, out of the block:

| Key | Form | Minimum stored at | Span stored at |
|---|---|---|---|
| `points` | two ints | `+0x198` | `+0x1c0` |
| `radius` | two floats | `+0x1a8` | `+0x1d0` |
| `depth` | two floats | `+0x1a4` | `+0x1cc` |

Both destinations are seeded with the template's own value before the read, so an absent key leaves
minimum and maximum equal. The parse then stores element 0 as the minimum and `element 1 − element 0`
as the span. The first copy therefore ends up holding the three minima in their template slots, and
the second copy holds the three spans in the matching slots and nothing else of use.

`FUN_005ac690` spends them at detonation. It copies the **engine** template into a fresh request
(not the weapon's copy), then overrides each of the three fields only when its span is non-zero:

- `points` = `+0x198` + truncate(uniform draw × `+0x1c0`), gated on `+0x1c0 != 0`
- `depth` = `+0x1a4` + uniform draw × `+0x1cc`, gated on `+0x1cc > 0`
- `radius` = `+0x1a8` + uniform draw × `+0x1d0`, gated on `+0x1d0 > 0`

The uniform draw is `rand()` scaled by 1/32768, so the range is `[minimum, minimum + span)`.

⚠ **A zero span discards the authored minimum as well.** Because the request starts as a copy of the
engine template rather than of weapon `+0x194`, a weapon that authored `radius [10, 10]` would carve
at the engine's 20.0, not at 10.0. Only a genuine range reaches the request at all.

## The six carriers author nothing inside the block

Every one of `wep_04`, `wep_12`, `wep_25`, `wep_26`, `wep_27` and `wep_28` spells the key as
`CRATER [0]`: a bare scalar, with no `points`, `radius` or `depth` beneath it. All three spans are
therefore zero, all three overrides are skipped, and **every crater in the shipped game is the same
crater**: a 7-vertex rim, 20.0 radius, 3.0 depth. The randomisation exists and never fires.

## `MAX_CRATER_RADIUS` bounds nothing

It is a file-level key of `weapons.zrd`, not a weapon key. `FUN_005ad630` reads it at `0x005ad775`
into the global `DAT_00a1e170`, whose default `FUN_005ad4e0` sets to 30.0. **Nothing in the binary
reads that global.** An instruction sweep over the whole program finds exactly two references to the
address, the default store and the parse store, and no load. The install authors no
`MAX_CRATER_RADIUS` either, so the key is inert twice over: unauthored, and unread if it were
authored.

## Making one crater

`FUN_005ac690` runs from the direct-impact handler `FUN_005ac7a0` at two sites, `0x005ac83c` for the
struck surface and `0x005ac8bb` for the terrain record stashed in `DAT_00a1e17c`, both under
`+0x74` bit `0x10` and both skipped when the impact hook's suppression mask carries bit 2. It fills
a request (position from the hit record's `+0x0c`…`+0x14`, material from `+0x20`, the three sizes as
above) and hands it to `FUN_004e4a10`, which first offers the request to an optional veto callback
at `DAT_00727ca4`. **No code in the binary installs that callback**, so the veto never fires.

`FUN_004e4890` then runs the three stages, with a geometry tolerance global saved, set to 0.005 for
the duration and restored afterwards (`FUN_005600c0` / `FUN_005600d0`).

**Build (`FUN_004e4a40`).** Allocates the instance (`FUN_004e4e00`: an 80-byte record holding the
request at `+0x04`…`+0x2b`, a `points`-long vertex array at `+0x2c`, a mesh builder at `+0x44` and
the chosen texture row at `+0x4c`), queries the terrain for the cell under the centre
(`FUN_004da3b0`, then `FUN_004e6fc0`), clamps the centre inward so the whole disc stays inside the
cell's extents (nudging by 1.0 past the edge), and lays `points` vertices evenly around a circle of
`radius` at the impact height. It records the ring's 2D bounding box at `+0x30`…`+0x3c`, then walks
the cell's object list looking for overlaps (below).

**Clip (`FUN_004e4ea0`).** Hands the ring, the cell and the mesh builder to `FUN_005272e0`, which
clips the disc against the terrain's own polygons; on success the instance's vertex array and count
are replaced by the clipped boundary, so the rim follows the real ground rather than a flat circle.

**Tesselate (`FUN_004e4f00`).** Creates a scene node, names it `ZDEC_FEATURE`, and stores the
instance pointer in the node's `+0x40`. It then lowers the centre by `depth`, builds a mid ring
halfway between each rim vertex and the centre and lowers that by `depth` too, and puts the apex a
further `depth` below, so **the bowl is two rings deep and its floor sits `2 × depth` below the
impact**. It emits one quad per rim segment (rim pair to mid pair) and one triangle per segment (mid
pair to apex), skinned with the texture row's material and lit from the terrain where a lighting
probe succeeds and from a flat 0.8 where it does not.

On success `FUN_004e4890` appends the carve to the record list, commits the geometry into the
terrain cells (`FUN_004e5cd0`, then `FUN_00526f10`), would play the texture row's effect at the
crater centre through `FUN_004edc10` if the row carried one, destroys the decorations inside the
radius, and frees the instance; the geometry itself now belongs to the terrain.

## What a crater destroys

`FUN_004df560` takes the crater centre and radius, walks every terrain cell the bounding square
covers, and for each decoration whose origin lies within the radius (a squared-distance test,
`FUN_00538920`) sets bits `0x1` and `0x4` in the instance's word at `+0x48`. Those are the same bits
the clutter weapon-hit hook at `LAB_004df420` sets when a decoration's health runs out: `0x1` marks
it destroyed and `0x4` marks the cell's decoration set dirty. So a crater flattens the trees and
scenery inside it outright, with **no health test, no anim and no model swap**: `FUN_004df560` reads
no template field at all, which is why the `OnCrater` health/anim/model triple that `FUN_004deab0`
parses out of a `templates.zrd` chapter is not consumed on this path. No chapter authors `OnCrater`
either ([`clutter.md`](clutter.md)), so the block is dead twice over.

## The three failure paths

Each stage has its own message, logged through `FUN_00415330` with `zdec_crater.cpp` and a line
number, and each returns −1 to `FUN_004e4a10`.

| Message | Line | Cause | Cleanup |
|---|---|---|---|
| "Failed to instance crater: Build Failed" | 0x95 | `FUN_004e4a40` returned 0: no terrain object, the ground query missed, the cell's carve counter has reached the cap, or the footprint overlaps an existing carve | `FUN_004e4a40` freed the instance itself |
| "Failed to instance crater: Clip Failed" | 0xcd | `FUN_005272e0` produced fewer than one polygon, or the clipped result has no vertices | the instance is freed |
| "Failed to instance crater: Tesselation Failed" | 0xdc | the mesh builder could not be opened on the cell (`FUN_00527040` returned 0) | the instance is freed |

`FUN_005ac690` returns true only when the carve succeeded, and `FUN_005ac7a0` ANDs that into its
animation-suppression flag. So **a refused crater is invisible to the player twice over**: no
terrain changes, and the weapon's `ANIMATION` and `SURFACE_ANIMATION` slots play normally instead of
being suppressed. A successful crater suppresses both unless the weapon authors `ANIMATION_ALWAYS`,
which nothing in this install does ([`ordnanceTypes.md`](ordnanceTypes.md), "Half one, the direct
impact"). `zdec_qsand.cpp` carries the same three messages for `QUICK_SAND`, whose weapon flag
`+0x74` bit `0x40000` no entry sets.

## Persistent, capped per cell, and never overlapping

A crater is **permanent for the mission**. Nothing ages it out, nothing recycles it, and there is no
fixed pool: the carve is merged into the terrain's own geometry and the record of it is appended to
a `std::vector` at `DAT_00727db4`…`DAT_00727dbc` that grows without a bound. Three separate
mechanisms hold the count down instead.

- **A per-cell cap.** Each successful carve increments a byte at terrain cell `+0x39`, once per cell
  it touched (`FUN_00526f10`). `FUN_004e4a40` refuses the build when that byte has reached the cap
  held on the terrain object at `world + 0x38`, byte `+0x5c`. Where that cap's value comes from was
  not traced.
- **No two craters may overlap.** Every carve leaves its `ZDEC_FEATURE` node in the cell with a
  back-pointer to its instance. `FUN_004e4a40` walks the cell's objects, and for each one named
  `ZDEC_FEATURE` compares the new ring's 2D bounding box against the recorded one, expanded by 5
  units on all four sides. Any overlap is a Build Failure. So repeatedly bombing the same spot
  produces one crater and then nothing, and craters cannot stack or interpenetrate.
- **Teardown.** `FUN_004e5b40` empties the record list (`FUN_004e60c0`) and frees the texture table.

The record list is also serialised. `FUN_004e5590` registers a writer and a reader under the name
`zDEClient` (`FUN_005bfce0`, into the archive registry `FUN_005c0100` builds). The writer walks the
list and emits one 52-byte entry per carve, named `Crater%d` for a crater and `QSand%d` for
quicksand; the reader replays each entry through `FUN_004e4890` with the effect argument zero, so a
restored crater is re-carved without replaying its animation. The reader's other arm, taken when the
registry's reset flag is set, calls `FUN_004e5d30`, which detaches every live carve's mesh, zeroes
the per-cell counters and deletes every `ZDEC_FEATURE` node in the world, then empties the list.

## The debug key

`FUN_00443310`, the debug-key handler, carries a case that copies the engine template, takes the
world position under the cursor and calls `FUN_004e4a10` directly, and another that does the same
for quicksand through `FUN_004e3bf0`. That path takes no weapon and so always carves at the engine
defaults.

## Evidence & limits

- `FUN_004e5590`, `FUN_004e4870`, `FUN_004e4890`, `FUN_004e4a10`, `FUN_004e4a40`, `FUN_004e4e00`,
  `FUN_004e4ea0`, `FUN_004e4f00`, `FUN_004e4830`, `FUN_004e5b40`, `FUN_004e5cd0`, `FUN_004e5d30`,
  `FUN_004e5e20`, `FUN_004e60c0`, `FUN_004e5ac0`, `FUN_004df560`, `FUN_00526f10`, `FUN_00526230`,
  `FUN_005ac690` and `FUN_004deab0` were read in full.
- The `CRATER` parse at `0x005ada98`…`0x005adb69` and the `MAX_CRATER_RADIUS` read at `0x005ad775`
  were read from `FUN_005ad630`'s disassembly rather than its decompilation, because the dispatcher
  is too large to decompile usefully and the min/span arithmetic is clearer in the instructions.
  `CATCHES_FIRE` at `0x005ada81` was identified the same way and agrees with
  [`formats/anim-definitions.md`](../formats/anim-definitions.md)'s independent reading of the same
  bit.
- "Nothing reads `MAX_CRATER_RADIUS`" rests on an instruction sweep of the whole program for the
  address `0x00a1e170`: two hits, both stores.
- The `points` draw's scaling is inferred. The decompiler renders that branch as `rand()` followed
  by a bare `ftol()` with the multiply dropped, while it renders the `depth` and `radius` draws in
  full as `rand() × 1/32768 × span + minimum`. The `points` branch is read here as the integer form
  of the same expression, which is what its operands allow and what the two decoded siblings do.
- The three failure paths' cleanup was read from `FUN_004e4890`'s disassembly. The decompiler drops
  the argument to the Clip-Failed cleanup call and makes it look like a leak; the instruction at
  `0x004e4915` pushes the instance, so it is not one.
- `FUN_005272e0` (the clip), `FUN_00527040` and `FUN_00526590` (the mesh builder and its polygon
  emitter), `FUN_00526350` (the lighting probe) and `FUN_004da3b0` / `FUN_004da430` (the terrain
  cell queries) were not opened; their roles are inferred from their arguments and from the
  arithmetic around the calls.
- Where the per-cell carve cap at terrain `+0x5c` gets its value was not traced. That it *is* the
  cap rests on the comparison in `FUN_004e4a40`, on `FUN_00526f10` incrementing the counter it is
  compared against, and on `FUN_004e5d30` zeroing that counter when it removes every carve.
- The clutter flag bits `0x1` and `0x4` were named from `LAB_004df420`, which sets the same two on
  a decoration whose health reaches zero. What reads them was not traced.
- The shipped `declient.zrd` numbers were read straight out of the retail `zrdr.zbd` of all eight
  chapters, by locating the key strings in the archive and decoding the reader node that follows
  each one. The project's extraction covers the top-level `zrdr.zbd` only, which does not carry the
  file, so `extracted/zrdr/` will not show it.
- The `DEFAULT_EFFECT` / `DEFAULT_ANIM` mismatch rests on both sides being read directly: the four
  key strings `FUN_004e5590` looks up sit at `0x0062eb90`…`0x0062ebd8`, and the block in the archive
  spells `DEFAULT_TEXTURE`, `DEFAULT_EFFECT`, `TEXTURE_PARMS`, `SLOPE`, `RADIUS`, `DEPTH` and
  `POINTS` and nothing else. That `crater_explosion` is defined nowhere rests on a byte scan of
  every `.zbd` in the install, which finds the name only inside the eight `declient.zrd` blocks.
