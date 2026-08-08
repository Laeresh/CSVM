# Fog volumes: `fogvol.zrd` + the gamez `fvol*` boxes

Part of the [format documentation](README.md). Covers the chapter-scope reader that fills the
world's invisible fog volumes with cloud sprites — the original's ambient cloud field.
Consumed by `CSVM/src/Mech3/FogVolumes.cs` (reader + volume census) and
`CSVM/src/Effects/FogVolumeClutter.cs` (the scatter and the render).

**The format is two halves that only mean something together.** The reader says *what* to
scatter and *how densely*; the gamez says *where*. Neither alone tells you a chapter has clouds:
three chapters ship a reader file and no volumes, and their reader file names a template their
gamez does not carry.

## Half 1 — `extracted/<chapter>/zrdr/fogvol.zrd.json`

One chapter-scope reader file, root = an [alternating key/list dict](README.md#shared-conventions-zrdr-readers).

| Key | Value | Meaning |
|---|---|---|
| `fog_zone` | `[int]` | Which fog zone the volumes belong to. **Read, not consumed** — see the open question below |
| `distance` | `[float]` | The scatter's mean spacing, metres — an areal density, not a lattice phase |
| `fog_fade_dist` | `[float]` | *(C5 only)* the volume's own fog fade distance |
| `interior_fog_fade_dist` | `[float]` | *(C5 only)* the same, from inside |
| `fog_color` | `[r,g,b]` | *(C5 only)* the volume's interior fog colour, integer 0–255 |
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

## Half 2 — the gamez `fvol*` nodes

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
no interior seam and no edge anywhere a player can reach it.
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

## The sprite templates

`cloudsprite1`/`cloudsprite2` are ordinary [clutter template roots](clutter.md) — parentless
`Object3d` nodes resolved by name (`ClutterBuilder.FindTemplateRoot`, shared with the trees) with
one child carrying the card. The card is a single 4-vertex, 1-polygon tri-strip quad,
`model_type: Facade` + `facade_mode: SphericalY` (full camera-facing, not the trees' upright
`CylindricalY`), skinned `cloud1.tif` / `cloud2.tif`, vertex colours 240/240/240, centred on its
own quad centre to within 3 mm.

| Chapter | card size | `lighting` | `fog` |
|---|---|---|---|
| C1, C4 | 132.3 × 132.3 m | `false` | `false` |
| C1C, C2B | 132.3 × 132.3 m | `true` | `false` |
| C5 | 70.0 × 70.0 m | `true` | `false` |

⚠ Every card is authored `fog: false` — the sprites are exempt from the mission distance fog and
carry `far_fade_range` instead. That is a deliberate reversal of what `CloudPuffs` did (it fogged
its puffs); the fade band replaces the fog wall.

## What is decoded and what is inferred

**Decoded, traced to data:** which templates and their weights; the volumes and their **authored
shapes**, not merely their extents; the per-sprite scale range; the draw distances; the card
geometry, texture, billboard mode and render flags; and the three-chapter split, which four
independent absences agree on (no `fvol*`, no `cloudsprite*` template, no `clutter` key,
degenerate ranges).

**Inferred, and marked as such:**

- **`distance` is the scatter's mean spacing — an areal density, not a lattice period.** Each
  volume is cut into `distance` × `distance` cells anchored on the world origin and each cell gets
  **one placement drawn uniformly inside it**, with `perturb_dist_range` applied on top. The
  density is what the number is for and it is corroborated: C1's 9,025 placements over the
  12,288 m map are a mean spacing of **129.3 m** against the authored 130, and a mean card area of
  132.3² × E[scale²] gives **1.6 sprite-areas of cover** per unit of layer — an overcast one sprite
  deep. Cells rather than N uniform draws over the whole footprint, because a Poisson field at this
  density opens holes and an overcast has to read as continuous.
  ☑ **Landed `A2`, 2026-08-08, and confirmed at the render.** The previous reading — that the
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
  footage never samples that. Full reasoning and the per-still evidence:
  `docs/PLAN-overcast-match.md` § `A1`/`A2`.
- **`perp_dist_range` is vertical.** "Perpendicular" to the volume's horizontal plane. The
  asymmetry supports it: `cloudsprite1` gets `[-5, 5]` and `cloudsprite2` `[-5, 10]`, so one kind
  floats slightly higher — which is a reading a horizontal offset makes no sense of.
- **`far_fade_range[1]` is what to draw at.** The pair is per detail level (`templates.zrd`
  authors it the same way, e.g. `firtree1` `[[500,1000],[1000,2000]]`); the remake has no
  reduced-detail mode, so it takes the farther band. Both are read and kept.
- **The vertical spread inside a volume is uniform.** The box is a volume, so the field fills it;
  `perp_dist_range` is applied on top.
  ⚠ **Under challenge from footage (2026-08-07, `CAP-12` C4 take — see `BL-118`):** C4's clear
  air shows the plane in *clear sky at 1135 m*, inside the 1060–1180.5 volume, with puff bases
  well above the 1050 m deck sheet — a gap uniform fill cannot produce (132.3 m cards would hang
  to ~956 m). A top-anchored scatter (centres near the volume top + `perp_dist_range`) fits both
  the C4 gap and C1's measured whiteout onset at 1003 m. Not yet implemented; the correction
  belongs here, not in a tuning constant.
  ☑ **DECIDED 2026-08-08 (plan item `A1`), pending render verification `A2`–`A4`: top-anchored —
  centre Y = volume top + `perp_dist_range`.** C1 then predicts card bottoms 986.3–1037.7 m
  (measured wisps 982, obscuration from 1003) and C4 1076.3–1127.7 m (measured clear air at
  1135 m). ⚠ **C4's frame is the only clean discriminator; C1's band is degenerate** and must not
  be cited alone — `CLOUD_COVER` `TOP 1124 / BOTTOM 970 / THICKNESS 30` predicts full white
  1000–1094 and the slab predicts a 1090.55 m top, against a measurement of 1003–1085, so both
  fit. ⚠ Open for `A3`: the rule is only tested on ~one-card-thick slabs. C5's 646 m strips would
  put every sprite in a sheet at 125–246 m and empty the streets below, and C1C's 300–597 m
  build-ups would be capped and hollow. If a frame shows either, the anchor is per-volume-shape.
- ~~**The volume is its axis-aligned bounding box.**~~ — **corrected `A1`/`A2`, 2026-08-08. The
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
    so C1C goes **11,368 → 9,569** under today's uniform-in-Y draw — not the ~9,922 a
    footprint-only estimate predicts. The number will move again when the vertical rule changes,
    and that is the containment test composing with it rather than a second correction.
  - **C5's seventeen strips are polygonal prisms**, three of them with a ramped top; volume
    fractions 0.558–1.000, and only `fvol1`/`fvol3` are boxes. **19,356 → 16,170**, the surplus
    having sat off the streets.
  - Because a frustum's faces slope, the test narrows with height by itself — nothing about it is
    a footprint taken at one altitude, and no vertical rule has to be taught about it.

**Undecoded / not implemented:**

- **`fog_zone`, and C5's `fog_color` / `fog_fade_dist` / `interior_fog_fade_dist`** are read and
  reported and nothing consumes them. Rendering a volume's *interior fog* is a separate feature
  from its clutter.
- ⚠ **`fog_zone` is not the sky/fog zone selector.** `docs/HISTORY.md` (2026-08-05) records the
  negative "no chapter's copy has a `fog_zone` key" — **that is wrong**: five chapters do. But
  the values do not select a weather zone either: C1's is 0 while its `fvol` nodes carry
  `zone_id: 2` and its weather file names `ZONE1`/`ZONE2`, and C5's is 1 against `zone_id` on its
  own volumes. `BL-277`'s geometry rule stands as landed; treat `fog_zone` as an index into
  something still unidentified, and do not re-open the zone decode on it.
- **The engine's exact cell phase** — the remake anchors the cells on the world origin. Under the
  density reading this is a far weaker choice than it was under the grid reading (a phase shift
  moves which cell a placement is drawn in, not where the placements line up), but it is still not
  read from data. Same unknown as [clutter.md](clutter.md)'s template alignment.

## Visible consequences to know about

- **The 130 m comb is not authored.** It was ours: a 10–20 m perturbation on a 130 m *grid* is
  ±15 % jitter, and it combed at grazing angles where the original (`CAP-12` t=44/59/97/124,
  t=29.2) shows soft continuous mottling at every angle. The grid is gone; do not re-derive one
  from `distance`.
- **The sky→tops boundary is a VERTICAL question, not a horizontal one.** Measured with the
  10–90 % transition-depth instrument (`docs/PLAN-overcast-match.md` § `A2`), randomising the
  horizontal placement moved our grazing frames by 0–0.5 px — 13 → 13.5 px at the pinned
  above-deck pose against the original's 91–103 px. The depth is set by how ragged the field's
  TOP is, which is the vertical rule's business. Do not read a shallow transition as evidence
  about the horizontal scatter.
- **Volume walls are not sprite clips.** `perturb_dist_range` is applied after containment, so a
  card's centre can sit up to `perturb_dist_range.y` outside its own volume's wall. That is what a
  perturbation means; the volume bounds where the field is placed, not where each sprite may hang.
