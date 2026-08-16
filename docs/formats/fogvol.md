# Fog volumes: `fogvol.zrd` + the gamez `fvol*` boxes

Part of the [format documentation](README.md). Covers the chapter-scope reader that fills the
world's invisible fog volumes with cloud sprites — the original's ambient cloud field.
Consumed by `CSVM/src/Mech3/FogVolumes.cs` (reader + volume census + the in-volume whiteout rule,
`FogVolumeWhiteout`), `CSVM/src/Effects/FogVolumeClutter.cs` (the scatter and the render) and
`CSVM/src/Session/WeatherRig.cs` (the whiteout overlay). Which key reaches which consumer:
[Consumed by the remake](#consumed-by-the-remake).

**The format is two halves that only mean something together.** The reader says *what* to
scatter and *how densely*; the gamez says *where*. Neither alone tells you a chapter has clouds:
three chapters ship a reader file and no volumes, and their reader file names a template their
gamez does not carry.


## Contents

- [Reader data](#reader-data)
- [GameZ volume nodes](#gamez-volume-nodes)
- [What the engine does with the volumes](#what-the-engine-does-with-the-volumes)
- [Map-edge continuation](#map-edge-continuation)
- [The sprite templates](#the-sprite-templates)
- [What is decoded and what is inferred](#what-is-decoded-and-what-is-inferred)
- [Consumed by the remake](#consumed-by-the-remake)
- [Visible consequences to know about](#visible-consequences-to-know-about)
## Reader data

One chapter-scope reader file, root = an [alternating key/list dict](README.md#shared-conventions-zrdr-readers).

| Key | Value | Meaning |
|---|---|---|
| `fog_zone` | `[int]` | **A bool**: non-zero arms the engine's in-volume whiteout + `ZONE3` camera state — see [the decompile section](#what-the-engine-does-with-the-volumes). Consumed by the reader and weather runtime |
| `distance` | `[float]` | The scatter's mean spacing, metres — an areal density, not a lattice phase. Engine default **206.25** |
| `fog_fade_dist` | `[float]` | *(C5 only, **16**)* whiteout approach ramp, metres before the volume wall. Engine default **400**. Consumed (`C21`) |
| `interior_fog_fade_dist` | `[float]` | *(C5 only, **16**)* whiteout decay depth inside the volume. Engine default **20**. Consumed (`C21`) |
| `fog_color` | `[r,g,b]` | *(C5 only, **[16,16,16]** — near black)* the whiteout's colour, integer 0–255. Engine default: the mission's `CLOUD_COVER` `TOP_COLOR`. Consumed (`C21`) |
| `clutter` | `[block, …]` | The scatter table — one or more blocks, each an alternating dict |

A `clutter` block:

| Key | Value | Meaning |
|---|---|---|
| `weight` | `[float]` | This block's weight among the table's alternatives |
| `nodes` | `[[weight, name], …]` | The gamez clutter-template roots this block may place, with their own weights |
| `far_fade_range` | `[[a,b],[c,d]]` | **Two** fade bands (metres, start → gone) — the same pairing `templates.zrd` uses for ground clutter, i.e. per detail level |
| `perp_dist_range` | `[min,max]` | Offset perpendicular to the volume's horizontal plane (vertical metres) |
| `perturb_dist_range` | `[min,max]` | Displacement from the drawn point within that plane |
| `scale_range` | `[min,max]` | Multiplier on the template sprite's own authored size |

⚠ **C1B, C2 and C3 ship a degenerate copy**: no `fog_zone`, no `clutter` key — the block sits in
the root list with nothing in front of it, which `ZrdrDict.FromAlternating` drops as a stray
value. `FogVolumeSpec.Parse` reads it anyway, on purpose: those three name a template
(`cloudsprite`, no digit) that **exists in no chapter's gamez**, so reading the block is what
lets "this chapter renders no clouds" be a proven lookup failure rather than a parser choice.
Their ranges are degenerate too (`perp_dist_range [151.25, 151.25]`), which is the second sign
the file is vestigial.

## GameZ volume nodes

Every chapter that scatters anything carries `fvol1`…`fvol34`: parentless-in-effect volumes
directly under the `World` node, each with its own model of 8–56 vertices, invisible (the world
walk has always skipped them — `WorldBuilder.SkipWorldNode`, now via `IsFogVolumeNode`, which the
volume census shares so the two sets cannot drift). **Their mesh is the volume** — not its
bounding box, which is the same thing only for C1/C2B/C4 (see the footprint entry below).

The correlation with half 1 is exact and is the whole decode:

| Chapter | `fvol*` | volume altitude band (m) | `distance` | `fog_zone` | templates named | sprites placed |
|---|---|---|---|---|---|---|
| C1 | 9 | 970 – 1090.5 | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,025 |
| C1B | **0** | — | 206.25 | *(absent)* | `cloudsprite` — **absent from every gamez** | 0 |
| C1C | 21 | 970.7 – 1091.3, **+ 12 frusta reaching 1390–1688** | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,569 |
| C2 | **0** | — | 206.25 | *(absent)* | `cloudsprite` — absent | 0 |
| C2B | 9 | 970 – 1090.5 | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,025 |
| C3 | **0** | — | 206.25 | *(absent)* | `cloudsprite` — absent | 0 |
| C4 | 9 | 1060 – 1180.5 | 130 | 0 | `cloudsprite1`, `cloudsprite2` | 9,025 |
| C5 | 17 | −463 – 183 | 80 | 1 | `cloudsprite1`, `cloudsprite2` | 16,170 |

C1/C1C/C2B/C4's `fvol1`–`fvol9` tile the whole 12,288 m map as a single flat slab ~120 m thick —
and they are an **exact 3 × 3 partition of the `World` node's own `area`** (x and z each split at
−10240 and −2048 over [−12288, 0]), so the field's footprint *is* the base map, to the metre, with
no interior seam. The authored field DOES end at the base map's outer rim — "The map-edge
continuation (`A5`)" below continues it past there, engine-side, for these four chapters only.
**C1C additionally stacks twelve smaller volumes on top of that footprint**, reaching 1,688 m —
authored build-ups over particular places, and the reason the scatter fills *each* volume rather
than taking the first one that contains a cell. C5's are not a slab at all: seventeen
low strips at −463…183 m following the streets, which is why its clouds read as ground-level
night haze between the skyscrapers rather than as an overcast.

⚠ **The C1C and C5 counts moved when the fill went from the bounding box to the authored shape**
(C1C 11,368 → 9,569, C5 19,356 → 16,170; C1/C2B/C4 unchanged at 9,025 because their volumes are
boxes). The spacing did not: the surplus was outside the authored footprint, and removing it is
what makes the density authored rather than an artifact of measuring a rotated shape squarely.

⚠ **A fog volume is not the `CLOUD_COVER` band.** C1's floor (970) happens to equal its
`weather.json` `BOTTOM`, but C1C (band 1055–1110 vs volume 971–1091), C4 (1000–1100 vs
1060–1181) and C5 (9950–10150 vs −463–183) all disagree. The volumes are their own authored
geometry; do not re-derive them from the weather file.

## What the engine does with the volumes

Decompiled from `crimson.exe` (Ghidra; loader `FUN_0044e010`, volume evaluator `FUN_0044e6f0`,
per-frame consumer `FUN_0042ee40` — the same frame update that runs the `CLOUD_COVER` whiteout,
[weather.md](weather.md)). What the reader's keys actually drive:

- **The loader hides every world node whose name starts with `fvol`** (a 4-char prefix match)
  and wraps each in a volume record (transform + bounds) — the same split this page decodes as
  "the mesh is the volume, invisible".
- **`fog_zone` is a bool arm-switch, not an index.** The loader stores `value != 0`; when set,
  every frame evaluates the camera against all volumes: approaching a wall, whiteout opacity
  ramps up linearly over the last `fog_fade_dist` metres; inside, it decays from full at the
  wall over `interior_fog_fade_dist` (the volume is a *transition* whiteout — ~20 m in it hands
  off); multiple volumes union as `a + b − a·b`. Being inside any volume flips the camera to
  **state 3 = `ZONE3`**, whose fog then carries the interior look. The install agrees to the
  letter: C5 is the only chapter authoring `fog_zone 1` — and the only one authoring a `ZONE3`
  (50–250 m fog) and its own `fog_color`/fade dists; C1's `fog_zone 0` leaves its nine deck
  volumes as scatter containers only, with no interior state, and C1 authors no `ZONE3`.
- **The whole load runs under a FIXED seed:** `srand(0x9b3a9ce2)` at entry, restored to
  `srand(time())` at exit — so every `rand()` the original's scatter draws is the same sequence
  every launch. The original's field is deterministic by construction (the remake's own seeded
  `Rng` reproduces the *property*, not the sequence — matching the original's exact placements
  would additionally need its scatter loop decoded, which this pass did not do).
- **The engine's defaults equal the "vestigial" C1B/C2/C3 values.** Absent keys default to
  `distance` 206.25, `far_fade_range` [2500,3500]×2, `perturb_dist_range` [82.5, 82.5],
  `perp_dist_range` [151.25, 151.25], `scale_range` [0.85, 1.15] — the degenerate copies simply
  restate the hardcoded defaults, a third sign those files are boilerplate rather than authored.

## Map-edge continuation

⚠ **Everything above this section is what `fogvol.zrd` + the gamez author. This section is not
that.** The original's field reads as everywhere, past the map the way `cloudparent` does NOT
(`CAP-12`'s own caveat: `cloudparent` stops at the map edge; extending it would invent content —
the two populations' vocabulary is `BL-325`'s note). Neither
`fogvol.zrd` nor the gamez says anything about content past `World.area` — there is nothing to
decode here, only a deliberate engine-side match to the terrain's own continuation
(`MapEdgeExtender.cs`, docs/architecture.md), landed as `A5` (user playtest, : "it is
only over the basemap. in the original its everywhere").

**What extends, decided from data (`FogVolumeSpec.FindMapSpanningSlab`):** only the volumes that
are (1) axis-aligned boxes, (2) top-anchored by A3's own rule, and (3), as a set, exactly tile
their own combined bounding rectangle with no gap or overlap — re-checking A1's "exact 3×3
partition of `World.area`" finding from the data rather than assuming it, so nothing keys off a
chapter name or an `fvol1..9` numbering convention. C1/C2B/C4's nine slab pieces and C1C's own
map-spanning `fvol1`–`fvol9` pass; C1C's twelve build-up frusta and C5's seventeen street strips
both fail test (2) — the shortest build-up is 299.7 m (ratio 2.27 against the 1.5× cut) and the
shallowest strip is 646 m (ratio 9.23) — so neither is ever extended, verified at the render
(their counts are bit-identical to A3/A6/A7's own, below).

**How far.** `MapEdgeExtender`'s rolling terrain window covers `Rings` (5) tiles of 1,024 m past
whatever cell the camera occupies — **5,120 m** — but a full precomputed ring to that radius would
place an estimated **~21,250** extra sprites for C1/C2B/C4 (≈2.35× the 9,025-sprite base field,
extrapolated from the ring-area ratio at the measured radius below), past a sane budget for a
structure that is built once and kept for the process's whole lifetime. Every kind's own shader
already collapses a sprite past its authored `far_fade.y` to a degenerate quad (`FogVolumeClutter`
class remarks), so a ring wider than the LARGEST authored `far_fade.y` buys zero visible pixels
from anywhere a camera can stand — bounding the extension there is lossless, not a cut corner.
Both `cloudsprite1`/`cloudsprite2` carry the same **3,500 m** in every shipped deck chapter, so
today this is one radius per chapter, read from the data rather than hardcoded.

**Density, Y band, and determinism.** Identical `distance` × `distance` cell tiling as the interior
draw (outermost cell of each axis takes the remainder, same as the interior loop), same weighted
kind draw, same `perturb_dist_range`/`scale_range`. Y is the slab's own constant top (every
qualifying piece's `box.End.Y`, checked equal by `FindMapSpanningSlab`) plus `perp_dist_range` —
A3's top-anchor rule inherited, not a second vertical rule invented. Each extension cell draws off
its OWN generator, `Rng.NewSystemRandom(Rng.Clouds, gx, gz)` (`Utils/Rng.cs`) — a hash of the
master seed, the subsystem and the cell's own coordinates, not the interior's shared sequential
stream, so the extension is stable under `--det` regardless of how many cells the far-fade bound
admits or what order they build in, and the interior draw's own realization is untouched (base
counts below are bit-identical to A3/A6/A7's own).

**Measured, all 8 chapters** (`--freecam --chapter=<X> --det`, `fogvol clouds:` log line):

| chapter | base (authored) | extension (engine-side) | total | extension/base |
|---|---|---|---|---|
| C1 | 9,025 | 13,176 | 22,201 | 1.46× |
| C1B | 0 | 0 | 0 | — |
| C1C | 9,572 | 13,176 | 22,748 | 1.38× |
| C2 | 0 | 0 | 0 | — |
| C2B | 9,025 | 13,176 | 22,201 | 1.46× |
| C3 | 0 | 0 | 0 | — |
| C4 | 9,025 | 13,176 | 22,201 | 1.46× |
| C5 | 16,170 | 0 | 16,170 | — |

C5's strips and C1C's build-ups are unmoved (their own counts are exactly A3/A6/A7's), and the
three deckless chapters stay at zero — the extension is inert exactly where the data says it must
be, and additive only where a map-spanning slab exists.

**Verified at the render.** A straight-down-the-seam pair at the west map rim
(`.scratch/a5/before-west-along-override.png` / `after-west-along-override.png`,
`--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00 --no-fog`): before, a hard
vertical line where dense green field meets bare white void; after, the same frame is green edge
to edge — no seam. The same pair at the map's NW corner and from 3,000 m past the west rim looking
back both show the identical before/after change (`.scratch/a5/*-corner-outward-override.png`,
`*-outside-lookback-override.png`) — the void the "before" build shows past the rim is filled, not
just thinned. `--det` twice at an identical pose: `pixmd5=f49caae4b1c6c1f62bdde1af8229faa6` both
runs.

## The sprite templates

`cloudsprite1`/`cloudsprite2` are ordinary [clutter template roots](clutter.md) — parentless
`Object3d` nodes resolved by name (`ClutterBuilder.FindTemplateRoot`, shared with the trees) with
one child carrying the card. The card is a single 4-vertex, 1-polygon tri-strip quad,
`model_type: Facade` + `facade_mode: SphericalY` (full camera-facing, not the trees' upright
`CylindricalY`), skinned `cloud1.tif` / `cloud2.tif`, vertex colours 240/240/240, centred on its
own quad centre to within 3 mm.

⚠ **We render those cards at vertex colour 225, not the authored 240** — a marked TUNE
(`FogVolumeClutter.CardVertexColorTune`, the user's verdict at the controls),
applied to RGB only and never to alpha. The authored value is what this page says it is and is
unchanged as a decode; what the TUNE fixes is a rendered-brightness gap with **no decoded
mechanism**. `236.65 × 240/255 = 222.7` is what the naive reading gives and it is what we shipped;
**nothing in five independent original above-band frames renders at 222.7**, and the original's
saturated plateau measures **208.88** (`t124`, 1208 m) and **209.16** (`t59`, 1219 m) in a
near-field, fog-free patch, with whole-frame `p99` topping out at 213–216. `236.65 × 225/255 =
208.8` lands there. C23 refuted the four mechanisms that could have explained it as data: (1) no
`cloudsprite` `OBJECT_OPACITY_STATE` exists in any chapter's `zrdr` — C1's `clouds.zrd` names only
`cloudparent`, and no other chapter ships one; (2) `WorldLight` on C1's cards would put them at
178.6, *below* the original's own 204.9–213.3 at the matching altitude; (3) `fog: true` is
contradicted three ways (the same reader's tree templates and the world's placed cloud facades
both author it explicitly, and `B16` verified the flag is honoured); (4) carrying the field up
with the relocated deck is refuted by CAP-12's own altimetry. **A mechanism, if one is ever
decoded, replaces this constant — it does not stack with it.** The TUNE is `fvol` cards only: the
placed `cloudparent` facades keep vertex colour 255 and their range-gated `0.6` opacity.

| Chapter | card size | `lighting` | `fog` |
|---|---|---|---|
| C1, C4 | 132.3 × 132.3 m | `false` | `false` |
| C1C, C2B | 132.3 × 132.3 m | `true` | `false` |
| C5 | 70.0 × 70.0 m | `true` | `false` |

⚠ Every card is authored `fog: false` — the sprites are exempt from the mission distance fog and
carry `far_fade_range` instead. That is a deliberate reversal of what `CloudPuffs` did (it fogged
its puffs); the fade band replaces the fog wall.

⚠ **The exemption is the CARDS' alone — do not extend it to the rest of the overcast**. The 144
cloud-deck tiles author `fog: true` in all four deck chapters (144/144, C1/C1C/C2B `cloudlayer.tif`
@ 960, C4 `Sky1.tif` @ 1050), and so do all 626/1056/1453 `cloudparent` facades in C1/C1C/C4. The
surface that genuinely never fogs below the deck is the horizon **dome** — every horizon model in
every chapter is `fog: false` — which is what actually explains the original's ceiling texture
surviving to the horizon line (census and consequences in [`weather.md`](weather.md)'s deck-census section).

## What is decoded and what is inferred

**Decoded, traced to data:** which templates and their weights; the volumes and their **authored
shapes**, not merely their extents; the per-sprite scale range; the draw distances; the card
geometry, texture, billboard mode and render flags; and the three-chapter split, which four
independent absences agree on (no `fvol*`, no `cloudsprite*` template, no `clutter` key,
degenerate ranges).

**Inferred, and marked as such:**

- **The card's RENDERED brightness — vertex colour 240 scaled to 225 (`CardVertexColorTune`).**
  The authored 240 is decoded and unchanged; the scale is a TUNE calibrated to the original's
  measured 209 plateau, with no mechanism behind it and four candidates refuted. Full statement
  under [The sprite templates](#the-sprite-templates) above. ⚠ The most likely *next* mechanism is
  not on this list at all: whether `lighting: true` on a `Facade` cloud card is a `WorldLight` gate
  (it is what puts C1C/C2B's own two cloud populations 60–70 units apart in one frame).
- **`distance` is the scatter's mean spacing — an areal density, not a lattice period.** Each
  volume is cut into `distance` × `distance` cells anchored on the world origin and each cell gets
  **one placement drawn uniformly inside it**, with `perturb_dist_range` applied on top. The
  density is what the number is for and it is corroborated: C1's 9,025 placements over the
  12,288 m map are a mean spacing of **129.3 m** against the authored 130, and a mean card area of
  132.3² × E[scale²] gives **1.6 sprite-areas of cover** per unit of layer — an overcast one sprite
  deep. Cells rather than N uniform draws over the whole footprint, because a Poisson field at this
  density opens holes and an overcast has to read as continuous.
  ☑ **Landed `A2`,, and confirmed at the render.** The previous reading — that the
  number was a *grid phase*, with `perturb_dist_range` as jitter on it — was contradicted by
  `PT-42` + `CAP-12` (ours combed on the 130 m lattice at grazing angles; the original shows none
  at any angle) and is now gone from the code. Mean spacing is invariant under the randomisation,
  so the density corroboration above only ever supported the *spacing*, never the *regularity*.
  The A2 A/B that settles it is a straight-down pair over C5's harbour, where the sprites read
  against dark water: before, blobs in aligned rows and columns at a fixed pitch; after, an
  irregular scatter with clumps and gaps at the same count.
  **Unbounded camera-tiling — the other candidate correction — is *falsified*, not merely
  unsupported**: the reader carries **no altitude field at all**, so the measured deck heights can
  only come from the `fvol` geometry, and a mechanism that reads a volume's Y bounds while
  discarding its X/Z bounds is two mechanisms; C5's seventeen polygonal street prisms (38 % of its
  map) and C1C's twelve rotated, tapering build-up frusta are inexpressible in a camera-centred
  field. **And its `templates.zrd` premise is wrong on the data:** that file carries only
  `node` / `substitute` / `scale_range` / `far_fade_range` — **no `distance`, no
  `perturb_dist_range`** — its period is the template's gamez ground-quad size and its decoration
  offsets are authored one-by-one, with no randomness anywhere. The genuinely shared keys are
  `scale_range`, `far_fade_range` and weighted substitution.
  ⚠ `PT-42`(b)'s "world-locked and tiled, no volume edge anywhere over the base map" is **not**
  evidence against the bounded reading — the nine slab volumes ARE the map (see half 2), so the
  two readings can only differ within `far_fade_range.y` (3500 m) of the map boundary and the
  footage never samples that.
- **`perp_dist_range` is vertical.** "Perpendicular" to the volume's horizontal plane. The
  asymmetry supports it: `cloudsprite1` gets `[-5, 5]` and `cloudsprite2` `[-5, 10]`, so one kind
  floats slightly higher — which is a reading a horizontal offset makes no sense of.
- **`far_fade_range[1]` is what to draw at.** The pair is per detail level (`templates.zrd`
  authors it the same way, e.g. `firtree1` `[[500,1000],[1000,2000]]`); the remake has no
  reduced-detail mode, so it takes the farther band. Both are read and kept.
- **The vertical spread is TOP-ANCHORED for sheet-thin volumes, UNIFORM for tall ones — the anchor
  IS per-volume-shape, settled `A3` and verified at the render.** C4's clear air at
  1135 m (`CAP-12` C4 take) falsified a uniform fill: 132.3 m cards
  drawn uniformly across the 1060–1180.5 volume would hang to ~956 m, leaving no gap, but the clip
  shows *clear sky*, puff bases well above the 1050 m deck sheet. `A1` decided the shape (centre
  Y = volume top + `perp_dist_range`) and `A3` decided WHICH volumes it applies to, since a
  map-spanning slab and a 597 m build-up tower are not the same kind of shape.
  ☑ **Rule, as implemented (`FogVolumeClutter.Scatter`):** a volume whose own AABB height is at
  most `TopAnchorHeightFactor` (1.5×) the chapter's card size draws every cell's Y at the volume's
  own top (`box.End.Y`) before containment, then adds `perp_dist_range` after — same order as
  every other placement, so the ordering trap below still applies. A taller volume keeps the
  original full-height uniform draw untouched. **Marked inference — the factor is a judgement
  call, not authored data**, chosen from a clean gap in the volumes' own measured thickness
  (`extracted/{C1,C1C,C2B,C4,C5}/gamez/nodes.json`): C1/C2B/C4's nine slabs and C1C's own
  map-spanning `fvol1`–`fvol9` are **120.5–120.6 m** thick against a 132.3 m card (ratio 0.91);
  C1C's twelve build-up frusta start at **299.7 m** (ratio 2.27, the shortest of them) and reach
  596.8 m; C5's seventeen street strips are **646 m** (ratio 9.23 against their 70 m card). 1.5×
  card height (198.5 m for the 132.3 m chapters) sits in that gap with margin on both sides —
  39 % under the tallest slab, 51 % under the shortest build-up — so no shipped volume is a close
  call.
  - **Why sampling AT the top (not inventing a band) still respects a sloped or tapered top:**
    `Contains` already runs the exact face test (`A2`), so for a volume whose top isn't a simple
    flat plane the (x, box.End.Y, z) point drawn in a cell is rejected exactly when that XZ falls
    outside the true top footprint at that height. No separate per-column top lookup was needed;
    the geometry the containment test already reads does the work.
  - **C1's band is still a degenerate instrument on its own** (`CLOUD_COVER` `TOP 1124 / BOTTOM
    970 / THICKNESS 30` predicts full white 1000–1094; the slab predicts a 1090.55 m top; the
    measurement is 1003–1085 m and both fit) — **C4's 1135 m clear-air frame remains the clean
    discriminator**, and the render now reproduces it (below).
  - ⚠ **The deck mesh is the slab's floor, authored: every deck chapter puts its `CloudDeck`
    tiles ~10 m BELOW its `fvol1`–`fvol9` slab floor** (`A6`,, from each gamez's
    `model_bbox`). The invariant, all four:

    | chapter | deck tiles | `fvol1`–`fvol9` floor | gap | `CLOUD_COVER` centre |
    |---|---|---|---|---|
    | C1 | 960.0 | 970.00 | 10.00 | 1047.0 |
    | C1C | 960.0 | 970.73 | 10.73 | 1082.5 |
    | C2B | 960.0 | 970.00 | 10.00 | 1024.0 |
    | C4 | 1050.0 | 1060.00 | 10.00 | **1050.0** |

    So the two populations are ONE sheet in the data: mesh underneath, sprite field on top of it,
    and top-anchoring only makes sense read against the mesh at its authored altitude. C4's centre
    lands on its authored deck exactly, which is the corroboration and also why C4 never showed
    the `A6` defect.

    ⚠ **The authored altitude is where the data puts the sheet; it is NOT where the deck mesh is
    rendered** (`A7`). The original's deck is engine trickery — below the
    `CLOUD_COVER` centre a ceiling carried 400 m above the camera, above it a world-fixed floor at
    the centre — so `WeatherRig.Tick` places it at neither chapter's authored 960/1050. What the
    table above still decides is the **relationship** the scatter is read against (mesh 10 m under
    the slab floor, one sheet) and the fact that `A6`'s band-centre pin was wrong *in the
    below-band regime*, where it buried the deck inside the field. Above the band that same pin is
    what the original does, and A7 restores it there.
  - **Verified at the render, `--pos`/`--tex-override` probes in `.scratch/a3/`:**
    - **C1 river pose** (`x -7325 y 934 z -3829` — ⚠ **not** the twin's altitude: `A7` re-read
      `Screenshots/C1 IA1 Fog river.png`'s overlay in and it says **`y 192`**, which the
      original's ALT gauge corroborates at ~700–750 ft. This probe is a self-consistent
      before/after pair at 934 m and its conclusion stands; a matched-pose A/B against the
      original needs 192): before, discrete cauliflower lumps hang below the deck sheet with a
      hard lower boundary (`before-river-pose.png`); after, the sheet's underside reads clean with
      the cloud band sitting well above it (`after-river-pose.png`).
      ⚠ **`A3`'s `--tex-override` numbers at this pose were misread, and `A6` them — do not re-cite them as written.** ~~looking straight up shows **zero** sprite pixels
      (`after-river-override-up.png`); levelled and tilted up, the coloured field's lower edge sits
      well clear of a flat gray band (the deck mesh at y=960) with no green intrusion at all
      (`after-river-override-level.png`)~~. Both readings measured something else. (a) A
      `cloudsprite` card is a `Facade`/`SphericalY` billboard, so from *directly* below it is
      **edge-on** — "zero green looking straight up" is a fact about billboard orientation and
      carries no altitude information at all. (b) The deck was NOT at 960 m at the time: `WeatherRig.Tick`
      re-pinned it to the `CLOUD_COVER` band centre, **1047 m**, and the big pale surface filling
      `after-river-override-level.png` IS that deck — the green fringe is drawn *over* it, i.e.
      below it. The "flat gray band" A3 identified as the deck is the dome seen under the deck's
      far edge. Re-shot with the deck itself flattened (`--tex-override=cloudlayer.tif=ff0000`,
      `.scratch/a6/`): **85,507 green pixels over red before, 0 after** the `A6` fix.
    - **C4 clear-air pose** (`x -4974 y 1135 z -3861`, the `csvm-c4-1135m.png` altitude): a
      level `--tex-override` sweep shows patchy, non-solid coverage at 1135 m — consistent with
      the predicted card-bottom band topping out at 1127.7 m, 7 m below this altitude — against
      **fully solid** coverage from the same pose 85 m lower at 1050 m, inside the predicted
      986–1128 m band (`after-c4-1135m-override-level.png` vs `after-c4-1050m-override-level.png`).
    - **C1C build-up** (`fvol10`, pos `-5416,1350,-9737` looking at its centroid) and **C5 street
      pass** (C5's `fvol10` strip, pos `-2868,50,-1792`): both **unchanged pixel-for-pixel in
      character** before/after — the tower stays a solid tapering mass, the street-level haze
      between skyscrapers stays put — because both volumes measure far taller than
      `TopAnchorHeightFactor` × their card and keep the old uniform draw.
    - **Sky→tops transition depth (the `A2`-amended instrument) does NOT move materially at any
      of the four poses tested** (pinned above-deck 46/13→46/7, grazing-tops-1208 60/47.5→55/48,
      above-deck-1527 25/25→25/25, high-above-1698 24/24→24/24 — band/edge px). This is a genuine
      finding, not a forced non-result: a billboard card is itself 132.3 m across, so even under
      the old uniform-in-120 m draw there was already a card near the volume's ceiling almost
      everywhere by sheer density, and the new tight top-anchored band doesn't change that — the
      instrument reads how ragged the *silhouette* of the nearest cards is at a shallow viewing
      angle, which top-anchoring does not touch. **The remaining gap to the original's 91–103 px
      is therefore NOT primarily a vertical-placement problem**; it is left as a finding for a
      future item (candidates: per-card alpha softness/scale distribution, or a video-compression
      artifact in the `CAP-12` capture — untraced, not investigated here).
  - **Counts:** C1/C2B/C4 stay exactly 9,025 (their slabs are axis-aligned boxes, so top-anchoring
    accepts the same 100 % of cells as uniform did — only the kind/scale/perturb realization
    shifts, since sampling Y at a fixed height instead of drawing it skips one RNG draw per
    top-anchored cell and re-aligns every later draw in the shared stream). C5 stays exactly
    16,170 (its strips are all classified uniform, untouched). **C1C moves 9,569 → 9,572** (+3,
    0.03 %): its nine slab pieces (including `fvol9`) are now top-anchored, and because that
    volume-order precedes the twelve build-ups in the shared RNG stream, the build-ups' own
    (unchanged-logic) containment draws land on different random numbers than before — an
    expected consequence of one shared seeded stream, not a second correction to the build-up
    rule itself.
- ~~**The volume is its axis-aligned bounding box.**~~ — **`A1`/`A2`,. The
  volume is the authored mesh**, and the two agree only for C1/C2B/C4.
  `FogVolumeSpec.VolumesOf` now carries each volume's face planes beside its bounds, and
  `FogVolumeBox.Contains` is a half-space test over them — which is **exact, not an approximation,
  because every one of the 65 shipped `fvol*` volumes is convex** (verified across all eight
  chapters: slabs, frusta and street prisms alike). The scatter still walks cells over the bounds
  and draws inside the cell; a draw that lands outside the authored shape simply places nothing,
  which is what keeps the spacing authored instead of crowding the surplus inward.
  - C1/C2B/C4's slabs are boxes (hull/AABB = 1.000): **9,025 unchanged**, and no C1 pixel of the
    milestone's two reference stills moves for this reason.
  - **C1C's twelve build-ups are rotated *frusta*.** Footprints 2048 × 704 m (also 974 × 335,
    1864 × 641, 854 × 294), the same twelve shapes cut into `fvol9`'s own top face as coplanar
    polygons; `fvol10` is 854 × 294 m at its 1091.28 m base and ~464 × 160 m at its 1688.05 m top.
    Base footprint = 0.383 of the bounding box, but the taper makes the **volume** fraction 0.235,
    so C1C went **11,368 → 9,569** under the (then still uniform-in-Y) draw at A2 — not the
    ~9,922 a footprint-only estimate predicts. **It moved again at `A3`, to 9,572**, once the nine
    slab pieces switched to top-anchored — see the vertical-spread entry above for why (an RNG
    stream-order effect on the build-ups, not a second correction to this frustum-taper rule).
  - **C5's seventeen strips are polygonal prisms**, three of them with a ramped top; volume
    fractions 0.558–1.000, and only `fvol1`/`fvol3` are boxes. **19,356 → 16,170**, the surplus
    having sat off the streets.
  - Because a frustum's faces slope, the test narrows with height by itself — nothing about it is
    a footprint taken at one altitude, and no vertical rule has to be taught about it.

**Undecoded / not implemented:**

- ~~**`fog_zone`, and C5's `fog_color` / `fog_fade_dist` / `interior_fog_fade_dist`** are read and
  reported and nothing in the remake consumes them yet.~~ **Struck (`C21`): all four keys
  are now CONSUMED — see [Consumed by the remake](#consumed-by-the-remake) below.**
- ⚠ **`fog_zone` is not the sky/fog zone selector** — and as of it is no longer
  unidentified: the decompile (section above) shows it is a **bool** arming the in-volume
  whiteout and the `ZONE3` camera state. The old record stands as history: `docs/HISTORY.md`
  claimed "no chapter's copy has a `fog_zone` key" — wrong, five do — and the
  `zone_id` mismatches that blocked the "selector" reading (C1's 0 against `zone_id: 2`) were
  never a contradiction, because the value was never an index. `BL-277`'s geometry rule stands
  as landed.
- **The engine's exact cell phase** — the remake anchors the cells on the world origin. Under the
  density reading this is a far weaker choice than it was under the grid reading (a phase shift
  moves which cell a placement is drawn in, not where the placements line up), but it is still not
  read from data. Same unknown as [clutter.md](clutter.md)'s template alignment.
- **The map-edge continuation's radius is a TUNE, like `A3`'s `TopAnchorHeightFactor`.** `A5`
  bounds the extension to the largest authored `far_fade.y` (3,500 m, every shipped deck chapter)
  rather than to `MapEdgeExtender`'s own reach (5,120 m) — a budget decision matched to the
  render's own fade shader, not a value `fogvol.zrd` or the gamez names. See the section above.

## Consumed by the remake

| key | who reads it | since |
|---|---|---|
| `distance`, `clutter` (all sub-keys) | `Effects/FogVolumeClutter` — the scatter | `A1`–`A5` |
| the gamez `fvol*` shapes | `FogVolumeSpec.VolumesOf` → the scatter, `MapEdgeExtender`, the camera-state test | `A1`/`A2` |
| **`fog_zone`** | `FogVolumeSpec.FogZoneArmed` → `WeatherState.CameraWeatherState`'s state-3 gate | |
| **`fog_fade_dist`, `interior_fog_fade_dist`, `fog_color`** | `Mech3.FogVolumeWhiteout` → `Session/WeatherRig.Tick`'s whiteout overlay | |

**`C21` landed the in-volume whiteout exactly as the section above decodes it.** Per
frame, per rig, when the chapter arms `fog_zone` (C5 alone), every volume's own density is taken
from one signed distance (`FogVolumeBox.SignedDistance` — outside-positive, inside-negative) through
the two decompiled ramps, and the volumes union as `a + b − a·b`:

- **outside**, the density rises linearly from 0 at `fog_fade_dist` metres to 1 AT the wall, over
  the true Euclidean distance to the authored convex hull (`FogVolumeBox.ExteriorDistance` — the
  face planes alone understate it by up to 42 % near a corner);
- **inside**, it DECAYS from 1 at the wall to 0 at `interior_fog_fade_dist` metres deep. ⚠ Read
  that next to `C22`, not on its own: the volume is a transition CURTAIN and `ZONE3`'s own 50–250 m
  fog is what carries the interior look. Inverting it would be wrong.

The result is blended onto the same full-screen overlay the `CLOUD_COVER` band whiteout uses (one
camera-space density per frame, never per-volume fog meshes — the original's own mechanism). The
two sources union with the same `a + b − a·b`, with the volume curtain composited OVER the band;
**they never coexist in shipped data** — C5 is the only chapter arming `fog_zone` and its band sits
at 9950–10150 m, ~9.8 km above its highest street strip.

⚠ **C5's "whiteout" is very nearly a BLACKOUT.** Its authored `fog_color` is `[16,16,16]` — the
same 16 its `ZONE3` `FOG_COLOR` carries, so the hand-off to `C22` is colour-continuous — and its
two fade distances are **16 m each**, not the engine defaults (400/20). At flight speed that
approach ramp is roughly one frame; the curtain is a hard edge by authoring, not by our
implementation.

Measured at the render (`.scratch/c21/`, `--freecam --chapter=C5 --det --no-zone-cull`, a vertical
approach at `(-2000, y, -1792)` onto a street strip whose top is 183 m; "before" = the same build
with the volume term forced to 0, restored and rebuilt after):

| pose | density | frame mean before → after | predicted | frame sd |
|---|---|---|---|---|
| `y 210` (27 m above, past the ramp) | 0.000 | 17.76 → 17.76, **byte-identical** | 17.76 | 18.38 → 18.38 |
| `y 191` (8 m above = half the ramp) | 0.500 | 18.53 → **17.29** | 17.26 | 18.84 → **9.54** |
| `y 182` (1 m inside the wall) | 0.937 | 17.85 → **16.06** | 16.12 | 5.08 → **0.33** |

The predictions are the sRGB-byte composite `(1−a)·frame + a·16`; a LINEAR-space composite predicts
18.22 at the half pose against the measured 17.29, so the overlay demonstrably blends in the
framebuffer's own space — which is what a `ColorRect` on a `CanvasLayer` does, and why this colour
is NOT linearised the way the fog globals are. The interior frame's sd of **0.33** is the
"near-full whiteout in `fog_color`" claim measured: at 1 m in, the frame is flat 16.

A C1 river pose (`fog_zone` 0) is **byte-identical** across the same A/B, and all 13 goldens are
hash-identical — including `c5-city-night`, whose camera stands **1,797.7 m** from the nearest C5
volume, 112× the 16 m ramp (predicted before the run, pinned in
`CSVM.Tests/FogVolumeWhiteoutTests.cs`).

## Visible consequences to know about

- **The 130 m comb is not authored.** It was ours: a 10–20 m perturbation on a 130 m *grid* is
  ±15 % jitter, and it combed at grazing angles where the original (`CAP-12` t=44/59/97/124,
  t=29.2) shows soft continuous mottling at every angle. The grid is gone; do not re-derive one
  from `distance`.
- **The sky→tops transition-depth gap survived BOTH the horizontal fix (`A2`) and the vertical
  one (`A3`) — it is evidence for neither scatter axis.** `A2` measured 0–0.5 px of movement from
  randomising the horizontal placement (13 → 13.5 px at the pinned above-deck pose); `A3`'s
  top-anchoring moved the same four poses by 0–5 px, still nowhere near the original's 91–103 px
  (see the vertical-spread entry above for the per-pose numbers). A 132.3 m card is nearly as tall
  as the whole slab is thick, so even the OLD full-height-uniform draw already put a card near the
  volume's ceiling almost everywhere by sheer density — concentrating the draw into a tighter top
  band didn't change that. **Do not read this shallow transition as evidence about either the
  horizontal scatter or the Y-distribution rule** — whatever produces the original's soft band is
  still unidentified (candidates: per-card alpha falloff/scale distribution, or a capture artifact
  in the `CAP-12` video) and is a lead for a future item, not something either wave should keep
  chasing with placement changes.
- **Volume walls are not sprite clips.** `perturb_dist_range` is applied after containment, so a
  card's centre can sit up to `perturb_dist_range.y` outside its own volume's wall. That is what a
  perturbation means; the volume bounds where the field is placed, not where each sprite may hang.
- **The field no longer ends at the base map's rim, for the four map-spanning-slab chapters.**
  `A5`'s engine-side continuation (its own section above) fills the same void this document used to
  describe as "no edge anywhere a player can reach it" — that phrase described the ABSENCE of
  interior seams between the nine slab pieces, which still holds; the map's OUTER rim used to be a
  real edge, and now is not, for C1/C1C/C2B/C4 only.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.