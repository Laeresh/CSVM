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
- **The vertical spread is TOP-ANCHORED for sheet-thin volumes, UNIFORM for tall ones — the anchor
  IS per-volume-shape, settled `A3` 2026-08-08 and verified at the render.** C4's clear air at
  1135 m (`CAP-12` C4 take, 2026-08-07 — see `BL-118`) falsified a uniform fill: 132.3 m cards
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
    tiles ~10 m BELOW its `fvol1`–`fvol9` slab floor** (`A6`, 2026-08-08, from each gamez's
    `model_bbox`). The invariant, all four:

    | chapter | deck tiles | `fvol1`–`fvol9` floor | gap | `CLOUD_COVER` centre |
    |---|---|---|---|---|
    | C1 | 960.0 | 970.00 | 10.00 | 1047.0 |
    | C1C | 960.0 | 970.73 | 10.73 | 1082.5 |
    | C2B | 960.0 | 970.00 | 10.00 | 1024.0 |
    | C4 | 1050.0 | 1060.00 | 10.00 | **1050.0** |

    So the two populations are ONE sheet in the data: mesh underneath, sprite field on top of it,
    and top-anchoring only makes sense read against the mesh at its authored altitude. Whatever
    renders the deck must leave that altitude alone — `WeatherRig.Tick` re-pinned it to the
    `CLOUD_COVER` centre until `A6` and thereby lifted it 64–122 m into the middle of the field in
    three chapters of four. C4's centre lands on its authored deck exactly, which is the
    corroboration and also why C4 never showed the defect.
  - **Verified at the render, `--pos`/`--tex-override` probes in `.scratch/a3/`:**
    - **C1 river pose** (`x -7325 y 934 z -3829`, matching `Screenshots/C1 IA1 Fog river.png`'s
      pinned altitude): before, discrete cauliflower lumps hang below the deck sheet with a
      hard lower boundary (`before-river-pose.png`); after, the sheet's underside reads clean with
      the cloud band sitting well above it (`after-river-pose.png`).
      ⚠ **`A3`'s `--tex-override` numbers at this pose were misread, and `A6` (2026-08-08) corrected
      them — do not re-cite them as written.** ~~looking straight up shows **zero** sprite pixels
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
