# `templates.zrd` — the clutter decorations' per-model properties

Part of the [format documentation](README.md). Every chapter ships one
(`extracted/<chapter>/zrdr/templates.zrd.json`). It is the authored half of the clutter
system: [clutter.md](clutter.md) covers *where* decorations are stamped, this page covers
*what they become* once they are. Consumed by `CSVM/src/Mech3/ClutterTemplates.cs`.

**The single most useful fact on this page is a negative.** Five of the eleven keys the
parser accepts — `translate_uv_range`, `rotation_range`, `align_normal`, `min_slope`,
`max_slope` — are authored by **no chapter in the retail install**, and so are all three
damage blocks. They are decoded here because "unused in the shipped data" is a measurement
worth writing down, not a reason to stop parsing; but nothing in the install can exercise
them. The consequence is structural: the original's placement has **no random input that
affects a decoration's position or orientation at all**, which is why an unseeded
reimplementation of the lattice walk reproduces C1's tree positions *exactly* rather than
approximately.

## Where it sits

| | |
|---|---|
| Loaded by | `FUN_00463f40` at startup, before the templates are resolved or the world is built |
| Parsed by | `FUN_004de7d0` (per block: allocate, default, `strdup` the name) → `FUN_004deab0` (per key) |
| Stored as | one 0x88-byte block per entry, appended to a global list (`DAT_00727c44`…`DAT_00727c48`) |
| Read by | `FUN_004dd230` — matches a block to a decoration by `strcmp` on the gamez **node name** |
| Consumed by | `FUN_004dd6e0`, the stamper, at steps 3, 5, 9, 10 and 11 |
| Source file | `D:\zipper\gamez\zclass\cls_clutter.cpp` |

## Shape

A reader list of blocks, each an alternating key/value list. The retail data establishes the
parser, key by key.

```
[
  [ "node",           ["firtree1.flt"],
    "substitute",     [[9.0, "firtree1.flt"], [1.0, "firtree2.flt"]],
    "scale_range",    [0.9, 1.1],
    "far_fade_range", [[500.0, 1000.0], [1000.0, 2000.0]] ],
  …
]
```

**A block is keyed by the DECORATION MODEL, never by the template that scatters it** — and
those are different name spaces. C3 registers exactly one template, `cliff1_sandtrans`,
while its `templates.zrd` describes palms: that cliff template's ground quad carries six
`palmtree1.flt` decorations, and the palms are what the file is about. The registration
list is `AddClutterTemplates` in `interp.json` ([clutter.md](clutter.md)); this file never
mentions it. **The `.flt` suffix is part of the name** and the gamez node names carry it
too, so the match is exact (verified across all 143 blocks).

A block with no `node` key is skipped by the engine before anything is allocated.
`node`'s value list carries exactly one name in every shipped block.

## Keys

Offsets are into the 0x88-byte block, for cross-checking against a decompile.

| Key | Shape | Offsets | Default | Meaning |
|---|---|---|---|---|
| `node` | `[name]` | +0x00 | — | The decoration model this block describes. |
| `substitute` | `[[w, name], …]` | +0x08…+0x10 | empty | Weighted roll for which model this stamp actually places (step 9). |
| `scale_range` | `[min, max]` **flat** | +0x14, +0x18 | 1.0, 1.0 | Uniform per-instance scale, drawn once per stamp (step 10). |
| `translate_uv_range` | `[[uMin, vMin], [uMax, vMax]]` | +0x1c/+0x20, +0x24/+0x28 | 0 | Per-axis UV jitter added to the lattice candidate (step 5). **Unauthored.** |
| `far_fade_range` | `[[nearMin, farMin], [nearMax, farMax]]` | +0x2c/+0x30, +0x34/+0x38 | 0 | Per-instance distance fade, metres (step 11). |
| `align_normal` | **bare flag** | +0x3c | false | Align the model to the ground normal instead of rotating it (step 10). **Unauthored.** |
| `rotation_range` | `[[xMin, yMin, zMin], [xMax, yMax, zMax]]` | +0x40/+0x48/+0x50, +0x44/+0x4c/+0x54 | 0 | Random rotation per axis, **degrees in the file, radians in the block** (×π/180 at parse). **Unauthored.** |
| `max_slope` | `[degrees]` | +0x58 | −1.0 | Its **cosine** becomes the LOWER bound on the ground triangle's normal Y (step 3). **Unauthored.** |
| `min_slope` | `[degrees]` | +0x5c | +1.0 | Its **cosine** becomes the UPPER bound (step 3). **Unauthored.** |
| `OnWeaponHit` | `{health, anim, model}` | +0x60…+0x6c | absent | Response to being shot; `health` also sets the "destructible" bit at +0x60. **Unauthored.** |
| `OnCrater` | `{health, anim, model}` | +0x70…+0x78 | absent | Response to a crater. **Unauthored.** |
| `OnCollide` | `{health, anim, model}` | +0x7c…+0x84 | absent | Response to being flown into. **Unauthored.** |

Defaults are `FUN_004de7d0`'s own initialiser, not conventions: scale 1.0/1.0, slope bounds
−1.0/+1.0, `align_normal` false, everything else zeroed.

### ⚠ The nested pairs are grouped by BOUND, not by band

`translate_uv_range`, `far_fade_range` and `rotation_range` are all pairs-of-lists, and the
first list is the **minima of every quantity**, the second the **maxima** — not "the first
quantity" and "the second". `FUN_004dd6e0` proves it: the near fade distance is lerped
between `kind+0x2c` and `+0x30`, which the parser filled from the two pairs' *first*
components, and the far distance between `+0x34` and `+0x38`, their *second*.

So C1's `firtree2`, authored `[[300,600],[1000,2000]]`, fades starting somewhere in
300–1000 m and gone somewhere in 600–2000 m. Reading the pairs as two bands would give
300–600 and 1000–2000 — a different rule that happens to agree on the many kinds whose
numbers are chained (`[[200,300],[300,350]]`), which is exactly what makes the mistake
survivable long enough to matter.

`scale_range` is **flat** (`[min, max]`) and must not be run through the same helper.

**Both fade distances come from one `rand()` draw**, so near and far are perfectly
correlated per instance rather than rolled independently. A zero `farMax` is the engine's
"never fades" sentinel — which is also what a decoration with no block at all gets.

### ⚠ The slope keys invert

Cosine decreases with angle, so `min_slope` (the minimum slope angle) becomes the
**maximum** admissible normal Y, and `max_slope` the minimum. `FUN_004dd6e0` step 3 clamps
the triangle's plane normal Y to [−1, 1] and rejects the triangle when it falls outside
`[cos(max_slope), cos(min_slope)]`. Naming a field after the key it came from is how this
gets implemented backwards; `ClutterKindProps` names them `NormalYMin`/`NormalYMax`, after
the thing they bound.

The defaults are ±1.0 — **no cull** — and no chapter authors either key, so no clutter in
the install is ever slope-culled. The remake's old `MinSlopeCos = 0.25f` had no counterpart
here and never fired anyway (the steepest clutter-eligible triangle in the install is C1's
at 0.4598).

### ⚠ `substitute` weights are relative, and the file never normalises them

`FUN_004deab0` sums the list and stores each entry as `w / total`; the stamper then walks
that list subtracting from one uniform draw. So C1's `firtree1` at `[[9, firtree1], [1,
firtree2]]` is **90 % / 10 %**, not "nine of something". Weight sums in the shipped data run
8.5 to 20, and the extreme case is C5's `hotelsign0`, which weights *itself* 0.1 against two
alternatives at 5.0: it is replaced 99 % of the time.

Every shipped list names its own model as one of the alternatives — that is how "usually
stays itself" is expressed. ⚠ **The roll rewrites the model and nothing else: properties
stay those of the SOURCE block** — the one the template authored, not the one it became.
See the trap at the bottom of this page, which works the case through.

A target the engine cannot resolve logs `%s: cannot find clutter substitution node,
interpreting it as nothing.` and is stored as a null model: it keeps its share of the roll
and **places nothing** when drawn. Two C5 targets (`cb05det02.flt`, `cb06det03.flt`) have no
block of their own, which is legal — a missing block means all defaults, never "do not
place".

## What the eight shipped files actually author

Every number below is measured off the retail install and pinned per chapter
in `CSVM.Tests/ClutterTemplatesTests.cs`.

| chapter | bytes | blocks | distinct models | `scale_range` | `far_fade_range` | `substitute` | the other eight keys |
|---|---|---|---|---|---|---|---|
| C1 | 1,657 | 5 | 5 | 5 | 5 | 3 | 0 |
| C1B | 795 | 3 | 3 | 3 | 3 | 0 | 0 |
| C1C | 4 | 0 | 0 | 0 | 0 | 0 | 0 |
| C2 | 12,250 | 50 | 50 | 50 | 50 | 2 | 0 |
| C2B | 4 | 0 | 0 | 0 | 0 | 0 | 0 |
| C3 | 992 | 3 | 3 | 3 | 3 | 1 | 0 |
| C4 | 1,148 | 4 | 4 | 4 | 4 | 1 | 0 |
| C5 | 22,956 | 78 | **77** | 78 | 78 | 34 | 0 |
| **total** | | **143** | — (names repeat across chapters) | **143** | **143** | **41** | **0** |

Only three keys are ever authored, and every block authors exactly `node` +
`scale_range` + `far_fade_range`, with or without `substitute` — 102 blocks of the first
shape and 41 of the second, install-wide. There is no other combination.

**The empty files are literally `null`.** C1C and C2B ship four bytes; mech3ax renders a
reader with no entries that way. That is a chapter authoring nothing, which is a different
fact from a chapter having no such file, and a reader that collapses the two loses it —
`Zrdr.LoadFileOrEmpty` exists for exactly this. C1C registers no templates at all, and
C2B's boot script registers six templates its own gamez ships none of (the engine logs
`ClutterLoadTemplates(): cannot find node for template %s` six times); so both chapters
place no clutter, and an empty properties file is the consistent outcome, not an anomaly.

**Ranges, for the units check** (WORLD-23): `scale_range` spans 0.5–3.0 across the install
and every pair runs low→high — multipliers, plainly, not radians or metres. Twelve distinct
pairs exist; C1's `firtree1` is 0.9–1.1 while its `firtree2`, `dougfirtree1` and bushes are
0.9–1.5, and C2's `spruce` is the widest at 1.0–3.0. `far_fade_range` spans 50–2000 m over
27 distinct quads, and all 143 satisfy near ≤ far on both bounds — the corroboration that
the min-pair/max-pair grouping above is the right reading. The commonest quads are C5's city
blocks at `[[200,300],[300,350]]` (30 kinds) and `[[300,400],[350,450]]` (27).

### The extra blocks are substitution targets

Cross-checking every block against the decorations its chapter's registered templates
actually carry:

| chapter | blocks with no decoration of their own | why |
|---|---|---|
| C1, C1B, C2 | none | every block is a placed decoration |
| C3 | `palmtree2.flt`, `palmtree3.flt` | `palmtree1` rolls three ways evenly |
| C4 | `firtree2.flt` | `firtree1` rolls 9:5 to it |
| C5 | 43 of 78 | the `b`/`c`/`d` block variants, the `det02`/`det03` details, and all three `hotelsign*` |

**No chapter has the converse** — every decoration that is actually placed has a block. So
the file is complete over what it dresses, and its surplus is exactly the set of models
reachable only by substitution.

⚠ **Those surplus blocks are inert.** Since a substituted stamp keeps its source block's
properties, a block belonging to a model that is only ever *arrived at* by a roll is never
read — the authoring is complete rather than load-bearing. It becomes load-bearing only for a
model that is ALSO placed directly somewhere, which is the ordinary case for the names that
appear in both columns. This is why the two C5 targets with no block of their own
(`cb05det02.flt`, `cb06det03.flt`) cost nothing: there was nothing to read either way.

### C5 ships one duplicate, and the first block wins

`cb05det01.flt` has two blocks: the first carries a 50/50 substitute to `cb05det02.flt`,
the second carries only `scale_range` and `far_fade_range`. The engine's lookup
(`FUN_004dd230`) is a linear scan of the load order that stops at the first `strcmp` match
and the loader appends, so **the first block is the one that is used** — the substitute
survives, and the second block is unreachable. It is the only duplicate in the install.

## What the remake reads, and what it does with it

`ClutterTemplateSpec.Load` / `.Parse` read every key on this page, including the eight no
chapter authors — the negative is only a measurement if the reader would have seen them.
`ClutterKindProps` holds one block; `Find` resolves a model name the way the engine does
(first block wins); `Census()` prints the per-key counts the table above pins.

`ClutterBuilder` consumes `substitute` and `scale_range` (C22): a stamp rolls its model against
the kind's table and takes a uniform scale from its range. `far_fade_range` is read and **not
applied** — deferred to `BL-337` (C23, 2026-08-10). It is a rendering-side feature, not a
placement one: the runtime's `CameraSetClutterFadeScaleSq` (`0x0063f5bc`) writes one global that
scales *every* type-5 scene node's LOD/distance fade, defaulted by the graphics detail level
(×1/×2/×3, `FUN_00440750`) and overridable per mission — so the authored metres in this file are a
base distance, not a literal one, and implementing the fade needs the detail-scale system alongside
the shader path. `rotation_range`, `align_normal` and `translate_uv_range` are unauthored
everywhere in the install (censused under B14/C21) and so need no decision beyond "document,
don't build" — recorded above. The five unauthored keys are read and, being unauthored, do
nothing.

⚠ **The draws come off a FIXED seed, not the session's.** The original wraps its whole world build
in `srand(0x8EA91836)` … `srand(time(0))` (`FUN_004df1d0`), so a chapter's forest is the same
forest on every launch — variety across launches is a property it deliberately does not have here.
Matching the original's *stream* is not achievable and not worth chasing (different PRNG,
traversal and draw count), but being fixed is.

⚠ **A substituted stamp keeps the SOURCE kind's properties.** The stamper holds the decoration
entry's own kind block in `fVar4` throughout `FUN_004dd6e0`, and the roll rewrites only the model
pointer — so scale, fade and the slope gate all come from the model the template authored, not from
the one it became. C1's `firtree1` rolls 1-in-10 to `firtree2` and those trees are scaled by
firtree1's 0.9–1.1, while the `firtree2` the templates place directly are scaled by its own
0.9–1.5.

One nuance for whoever implements step 10's rotation: the engine draws three `rand()` values for
it **even when `rotation_range` is absent**, so the key is inert in its effect, not skipped in the
stream. That only matters to somebody trying to match the original's draw order, which the
paragraph above says not to attempt.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
