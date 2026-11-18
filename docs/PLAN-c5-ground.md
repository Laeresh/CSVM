# C5 ground under the Enhanced city

**ACTIVE PLAN** (written 2026-11-18). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

Under the Enhanced look with 3D Scenery on, C5 (Manhattan at night) draws script-built buildings in
place of the original ones, but the ground between them is still the original's painted ground,
and beside the new buildings it no longer fits (#357). The author's report: "The ground texture
does not match the buildings. There are some roofs that are partly below the models and most of
the texture is so dark that it is just black. Perhaps some courtyard, gardens or similar inside the
free area in the blocks." This plan replaces the ground inside the blocks with a generated block
floor and low 3D dressing that fit the building layout standing on each piece of ground, keeping
the original's lit streets.

Out of scope: the streets' own content (their paint, the street lamps and glows, traffic), the
named world models, and the Faithful look and 3D Scenery off, which keep the original ground. The
plan draws one `backlog` issue, #357; it was read in this session (`gh issue view 357`, open) and
its three untraced questions were measured offline from `extracted/C5` (below). The measurements
are not yet committed (A1).

## Milestone goal

- Wherever `CityModels.On` holds, C5's ground shows no painted roof beside or under a building: the
  block interiors are a generated floor fitted to the buildings that actually stand there.
- The block floor reads at night: dim, clearly darker than the walls and the streets, but its
  paving, lawns and courtyard edges are visible.
- The free area inside the blocks holds back courts, alleys, service yards, gardens with trees,
  and on blocks without buildings parks, plazas and lots, dressed with low non-solid 3D models.
- The ground varies per block and the dressing per stamp, so the 256 m template repeat does not
  show the way the original's does.
- Buildings, ground and dressing together stay inside the city budget.

**Enhanced adds no gameplay.** No new collider, no change to the original's lots, footprints or
colliders, and nothing that looks walkable or landable where a collider stands. Faithful and 3D
Scenery off draw the original ground.

## Decisions (2026-11-18)

| # | Question | Decision |
|---|---|---|
| 1 | How much of the original ground to keep | **Keep the original's lit street band, regenerate the block interiors.** Original pixels never ship: the street band is copied from the player's own install at load time and composited with the generated floor. |
| 2 | What fills the free area | **Painted floor plus 3D dressing**: courtyard walls, fences, sheds, parked period cars, trees. |
| 3 | How bright the floor reads at night | **Dim, readable, generated at a mean texture luma of about 20 (18 to 22), with one comparison build at 30 for B15.** First set at texture 25 to 35 against the wall textures (40 to 70); A3 measured that the ground renders at 0.56 to 0.70 of its texture luma while the new walls render at a median of 10 to 13 at three of four views, so 25 to 35 would render brighter than the facades. The author chose "~20, plus a 30 variant". TUNE at the controls. |
| 4 | Repetition | **2 to 4 floor variants per combination, picked per block** by a hash of the block's texture cell, the cells shifted half a tile so seams fall on street centrelines. The dressing rolls per stamp from its seed like the buildings. |
| 5 | Colliders on the dressing | **Never solid, kept low**: a structure height cap of about 4 m (TUNE), trees under the nearby eaves. |
| 6 | Texel size | **50 cm** (512² per 256 m tile). |
| 7 | Diagonal districts (`cblock2/3` paint) | **Variants per strip.** A2 found one 45° street per tile and no cross street, so no diamond lattice exists; each 181 m strip between consecutive street centrelines picks its own variant, cell index floor(u + v − 0.5) on `cblock2` and floor(u − v − 0.5) on `cblock3`. Along a strip the floor repeats every 362 m. The author chose "Variants per strip" over one design per combination. |
| 8 | "No clutter" ground with no layer beneath (no buildings) | **Open ground per block**: parks, plazas, parking lots, vacant lots, by the variant hash, with low dressing. |
| 9 | What switches it on | **`CityModels.On`**: the new ground draws exactly when the new buildings do. No new option. |
| 10 | Budget | **Shared with the buildings**: buildings, ground and dressing together at most +2.0 ms GPU and +0.5 ms CPU over the original city on the city plan's nine C5 views, distant draws at or under the original city's. The Deck keeps its one-pane default only at 60 fps or more on every view. |
| 11 | First prototype | **`cblock7`** with its own walk-ups, judged at the controls and on montages before the other combinations. |
| 12 | Character | **Mostly paved, some green**: back courts, alleys and service yards (asphalt, gravel, flagstone, fences, sheds, parked cars, ash cans), about a quarter of the free area as small gardens with trees, open blocks as parks or parking lots. |
| 13 | What counts as street | **The whole street, kerb to kerb, lit or unlit.** The author, on A3's montages: "The yellow marks of the street don't include the unlit streets between them. They should not get objects." The street mask is geometric (kerbs, the building-free corridors), never a luma threshold, which finds only the lamp pools. No floor change and no dressing on any street. |
| 14 | Which trees (B14) | **A new deciduous city tree** (plane tree or elm style) built with the vegetation generator pipeline. No existing vegetation model is a city deciduous tree. The author chose "New deciduous model". |
| 15 | The dressing set (B14) | **Placed as built**: wall, fence, two sheds, sedan, delivery truck, ash cans, bench, the city tree. The author, on the offline montages: "Yes, place them"; grade, fog and night are tuned at B15. |
| 16 | Hedges (B14) | **A cheap box hedge** (`gd_hedge`, 18 triangles, 1.05 to 1.3 m) laid in runs along garden edges, over C1's bushes (682 and 192 triangles, the smaller bluish at night). The author chose "Cheap box hedge". |
| 17 | The `cblock7` floor layout (B12) | **Wired in as generated**; the core's patchwork of rectangles is judged in perspective under the dressing at B15. The author chose "Wire it in as is". |

Settled as approach, not put to the author: each combination of visible paint and standing
building set gets its own floor (the engine already splits `no_clutter` surfaces); the street band
is composited at every texture level from the original's authored `_1`/`_2` levels, so distant
streets keep their lamp points; the floor and the dressing keep clear of the original hulls grown by
their awnings and fire escapes, and of the lamp posts. Decision 4's variant cell is hashed on world
XZ, floor(x / 256 + 0.5) and floor(z / 256 + 0.5), never on raw UV: 1,801 distinct world tiles hold
UV (0, 0) and polygon edges cross the middle of blocks, so a UV hash would seam inside them (A2).

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The painted roofs are the original buildings' roofs, exposed because the new models are carved up to 3 m inside the original hulls. | The paint never fitted the template layouts. Under all eight axis-aligned texture frames the texels inside the original footprints average luma 3 to 6; 72 % to 94 % of the bright painted structure lies outside every footprint (this session's survey, A1). The new models only make the old mismatch legible. |
| 2 | One replacement texture per `cblock` texture is enough. | "No clutter" `cblock1/2/3` (and `cblock7`) paint is dressed by the `cblock4/5/6` building set beneath it (`docs/org/clutter.md:163-170`), while the clear paint carries its own set. The two sets share only 47 % to 76 % of the beneath set's footprint area, so one paint carries two layouts. |
| 3 | `cblock4` shows on about 3 % of its area and is a combination of its own. | A1's exact planar arrangement: every `cblock4/5/6` polygon is covered by an overlay, 0.000 km² exposed. `CBLOCK-LOD.md` §2's 336,000 m² saw only overlap inside one model. |
| 4 | Bare flagged ground is 7.6 % of C5's ground. | A per-polygon sum counts stacked overlays twice; the arrangement gives 12.96 of 180.13 km², 7.2 % (A1). |
| 6 | A texel brighter than 55 after a 16 m box filter is street. | It finds the lamp pools only; the unlit asphalt between them was counted as block floor in A1's free-floor columns and in A3's floor class (the author, Decision 13). A1's street mask is redone from the kerbs. |
| 5 | Rendered, a floor at texture luma 25 to 35 sits under the walls. | A3: the ground renders at 0.56 to 0.70 of its texture, the new walls at a median of 10 to 13 at three of four views; Decision 3 now generates at about 20. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Measured from shipped data and captures** | A1, A2, A3 | Re-run the committed scripts, then build on them. |
| **Direction sound, magnitude TUNE** | B12, B14, B15 (brightness, height cap, variant count, mix) | The *what* is the author's decision; the numbers are set at the controls. |
| **Leads only** | B13 (the load-time composite and the variant pick), C22 (the strip cells) | Budget for investigation; the mechanism is proposed, not traced. |

## What the data actually ships

All from `extracted/C5` (`gamez`, `texture`, `zrdr/templates.zrd.json`), through
`analysis/city-hulls/census.py`'s loader. Luma is Rec. 601 on sRGB texels, 0 to 255.

**The ground is two coplanar layers.** `cblock1/2/3` (and `cblock7`) are drawn over `cblock4/5/6`
at the same height and the same 256 m texture repeat (`analysis/item9-depth-bias/CBLOCK-LOD.md`
§1c, §2). The `no_clutter` polygon flag picks which layer decorates and the flagged layer draws on
top (`CSVM/src/Mech3/SceneBuilder.cs:486-492`, `docs/org/clutter.md:163-170`). Flagged overlay with
no base beneath it, 7.2 % of C5's ground, carries no buildings.

**The combinations of visible paint and standing building set** (`analysis/c5-ground/FINDINGS.md`;
area from an exact planar arrangement of the world's `cblock` polygons, the other columns per
256 m tile):

| Visible paint | Building set | Area (km²) | Built share of block | Painted structure (luma ≥ 25) uncovered | Free floor luma mean, median | Free floor at luma ≤ 5 | Widest free spot |
|---|---|---|---|---|---|---|---|
| `cblock7` | `cblock7` (clear) | 75.756 | 0.18 | 0.87 | 6.3, 0 | 0.71 | 149 m |
| `cblock1` | `cblock1` (clear) | 51.474 | 0.32 | 0.73 | 6.5, 0 | 0.70 | 72 m |
| `cblock1` | `cblock4` (flagged) | 9.850 | 0.31 | 0.65 | 6.0, 0 | 0.72 | 160 m |
| `cblock2` | `cblock2` (clear) | 9.425 | 0.30 | 0.80 | 6.6, 0 | 0.72 | 81 m |
| `cblock2` | `cblock5` (flagged) | 8.390 | 0.26 | 0.68 | 5.9, 0 | 0.74 | 96 m |
| `cblock3` | `cblock6` (flagged) | 6.092 | 0.28 | 0.73 | 4.6, 0 | 0.79 | 74 m |
| `cblock3` | `cblock3` (clear) | 4.914 | 0.32 | 0.75 | 4.9, 0 | 0.78 | 75 m |
| `cblock7` | `cblock4` (flagged) | 1.062 | 0.35 | 0.54 | 5.8, 0 | 0.72 | 158 m |
| `cblock2` | `cblock4` (flagged) | 0.131 | | | | | |
| `cblock1` | `cblock2` (flagged, over clear `cblock2`) | 0.066 | | | | | |
| `cblock3` or `cblock6` | both (neither flagged) | 0.008 | | | | | |
| `cblock1` | none (flagged, no base) | 3.906 | 0 | 1.00 | 6.0, 0 | 0.72 | 224 m |
| `cblock3` | none | 3.524 | 0 | 1.00 | 4.3, 0 | 0.80 | 158 m |
| `cblock2` | none | 3.091 | 0 | 1.00 | 5.9, 0 | 0.74 | 158 m |
| `cblock7` | none | 2.423 | 0 | 1.00 | 5.9, 0 | 0.72 | 220 m |
| `cblock1` | none (flagged over flagged `cblock4`) | 0.016 | | | | | |

The ground totals 180.13 km² under 206.57 km² of polygons. No `cblock4/5/6` base is exposed.
"Street" is the kerb-to-kerb band inside every building-free corridor of at least 10 m across the
tile (Decision 13, `survey.py`'s `find_streets`); blocks are everything else, the 2 to 5 m verge
included. The square paints draw a full cross: `cblock1` 33 m wide on u and v = 0.50, `cblock7` 35
to 36 m on u = 0.500 and v = 0.494 (half-widths 0.0645 and 0.068 to 0.070 of a tile). `cblock2` and
`cblock3` draw one 23.3 m street on u + v and u − v = 0.5 + k (half-width 0.0645 in that
coordinate). The 1 to 7 m building-free seams along the tile edges carry no kerb, band or lamp and
are block. Streets average texture luma 34 to 46 under this mask; the original building textures 40 to 70
(`analysis/city-hulls/FINDINGS.md`, Hulls table). `cblock7` is `cblock1`'s art at 128²
(r = 0.983, `CBLOCK-LOD.md` §1a) under small walk-ups, 42 % of the city's ground. Of it, 0.427 km²
is flagged `cblock7` over clear `cblock7` laid in a 45 m or an off-axis 128 m UV frame, whose
buildings stamp in that frame (inferred, not watched).

**The diagonal paints** (`analysis/c5-ground/diagonal.py`): `cblock2` and `cblock3` carry one lit
45-degree street per tile, 22.6 m between kerbs, centrelines on u + v = 0.5 + k and u − v = 0.5 + k,
181.0 m apart. No street crosses them and no footprint of either set touches a centreline.

**Rendered** (`analysis/c5-ground/DARKNESS.md`): the ground renders at 0.56 of its texture luma at
16, 0.66 at 32 and 0.70 from 64 up, at every view to 1000 m; the zone's black fog (1500 to 2250 m)
darkens it beyond. Rendered medians at the C5 cost views: walls 10 to 13 (27 over `cblock7`), roofs
9 to 19, streets 53 to 72, today's floor 0 to 2.7.

**Footprints map to the paint through the template quad.** A decoration's texture UV is its origin
projected through the template ground quad (`CSVM/src/Mech3/Clutter.cs:1251-1264`), the quad frame
is +U = +X, +V = +Z, and 93.6 % to 100 % of every `cblock` texture's ground area is laid in that
same frame at 255 to 259 m per repeat, so a design made in a template's texture space lands under
its buildings everywhere.

**The paint's own levels.** `cblock1_1` (128²) averages luma 4.41 and `cblock1_2` (64²) 6.77,
against a box filter's 15.6: hand-authored, darker, the lamp points kept (`CBLOCK-LOD.md` §1b).
`TextureArchive` installs them (`--mips=authored`).

`CBLOCK-LOD.md` §2's 32.4 km² of clear `cblock1` counts models of two or more polygons once each;
the world holds 51.47 km² (A1).

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, survey

1. ☑ Commit the #357 ground survey as `analysis/c5-ground/`
2. ☑ Measure the diagonal districts' street grid and the bare flagged ground
3. ☑ Measure the ground's darkness in a rendered C5 frame

### Wave B, the `cblock7` prototype

11. ☑ Export the footprint and street data the generator needs
12. ☑ Generate the block floor variants for `cblock7`
13. ☑ Composite and select the new ground in the engine
14. ◐ Build and stamp the low block dressing (models landed, stamping open)
15. ☐ Judge the prototype at the controls

### Wave C, the whole city

21. ☐ The square districts' other combinations
22. ☐ The diagonal districts
23. ☐ Open ground on the bare flagged blocks
24. ☐ Measure the cost on the PC and the Deck
25. ☐ Checks: suite and goldens

## Dependency and parallelism notes

A1 blocks everything. A2 blocks C22 and C23; A3 sets B12's brightness target. B11 → B12 → B13 is a
chain; B14 needs B11 and can run beside B12 and B13 (B14 owns the dressing models and their
stamping, B12 and B13 own the floor). B15 needs B12 to B14. Wave C starts after B15's verdict; C21,
C22 and C23 all extend the generator, so they run in series, never in parallel worktrees. C24 and
C25 come last.

---

# Wave A, survey

## A1 ☑ Commit the #357 ground survey as `analysis/c5-ground/`

**Landed.** `analysis/c5-ground/survey.py` and `FINDINGS.md`. The survey maps each building
decoration into texture UV through its template quad (`Clutter.cs:1251-1264`), wrapping a
footprint across the tile edge, and computes an exact planar arrangement of every coplanar `cblock`
triangle reached from `world1`. The table in "What the data actually ships" is its output. The
paint never fitted the buildings: under all eight frames the texels inside a template's own
footprints average luma 2.9 to 6.4, and 67 % to 93 % of the painted structure lies outside them.
`CBLOCK-LOD.md` §2's 32.4 km² comes from a probe that skips single-polygon models and counts each
model once; the world holds 51.47 km² of clear `cblock1`. No `cblock4` base is exposed, and bare
flagged ground is 7.2 %. The street mask is geometric (Decision 13): the first survey's luma rule
found only the lamp pools, and the author checked the redone mask's plots ("Yes, use it").

**Verified.** The script reproduces every number of the plan's first table, the eight-frame test
and the UV-frame census, and §2's 32,435,893 and 11,281,871 m² and its 196-polygon bimodal coverage
under that probe's restrictions (confidence: traced, from shipped data).

**Original approach (kept for reference).**

**Goal.** The measurements in "What the data actually ships" are reproducible from a committed
script and a `FINDINGS.md`, with the `cblock1` area discrepancy settled.

**Evidence (confidence: direction-sound).** Measured this session by a scratch script outside the
repo: footprints from each template's decorations (`census.Chapter.geometry`), mapped into texture
UV through the ground quad's own affine XZ→UV, rasterised at texel scale; the eight-frame test; the
world UV-frame census over every `cblock` polygon reached from `world1`.

**Approach.** Move the scratch script into `analysis/c5-ground/` beside `analysis/city-hulls/`,
reusing its loader. Write `FINDINGS.md` in the analysis pages' form (question, instrument, verdict,
caveats). Overlay images hold original pixels and stay out of the repo, as `render.py`'s do. Find
why the walk sums `cblock1` clear ground to 51.5 km² against `CBLOCK-LOD.md`'s 32.4 km² (map-edge
copies or a doubled partition root are the first suspects) and quote areas once it agrees.

**Model recommendation.** <TODO: not settled in the grilling.>

**Verify.** The script reproduces every number in the combinations table; the area total agrees
with `CBLOCK-LOD.md` §2 or the difference is explained.

**⚠ Traps.** Census by template root, never by `cbNNa` name range (`docs/formats/clutter.md:152-155`).
The overlays and any contact sheet hold original pixels.

## A2 ☑ Measure the diagonal districts' street grid and the bare flagged ground

**Landed.** `analysis/c5-ground/diagonal.py`, and the bare-ground section of `FINDINGS.md`.
`cblock2`'s kerbs lie on texel diagonals 111 and 143 (u + v family), `cblock3`'s on 112 and 144
(u − v family), each lit on 81 % to 100 % of every quarter of the tile, which pins them to 45° within
0.22°. One street per tile, 22.6 m wide, centrelines 181.0 m apart; the best-lit crossing diagonal
is lit on only 0.51 and 0.44 of its length, and every crossing diagonal runs through a building of
the flagged set. So no diamond lattice exists and the stripe lattice does (Decision 7). The bare
flagged ground is 12.96 km² in 55 to 68 polygons per paint, its free floor as dark as its paint
(mean 4.6 to 7.9, median 0); its largest clusters are listed in `FINDINGS.md`.

**Verified.** The stripe plots (`diagonal.py --plot`, outside the repo) show the centrelines clear
of both sets' footprints on 3 × 3 tiles (confidence: traced for the measurements; the stripe lattice
is direction-sound until B13 draws it).

**Original approach (kept for reference).**

**Goal.** Decision 7 can be applied: whether `cblock2` and `cblock3`'s 45° streets form a regular
grid in texture space, at what spacing and offset, and the free-floor numbers for the flagged ground
with no base.

**Evidence (confidence: lead-only).** The `cblock2` overlay shows diagonal streets crossing the tile
edges; regularity is not measured. The bare flagged ground is 7.6 % of C5's ground
(`docs/org/clutter.md:169-170`).

**Approach.** Fit the street mask of `cblock2` and `cblock3` against a rotated lattice; record
spacing and phase. For the bare ground, list its polygons per paint and their areas.

**Model recommendation.** <TODO>

**Verify.** <TODO: the lattice fit's residual, and a plot of the rotated cells over the street mask.>

**⚠ Traps.** A diagonal block straddles the 256 m tile edge, so any per-tile statistic must wrap the
tile (the survey's distance transform tiles 3 × 3).

## A3 ☑ Measure the ground's darkness in a rendered C5 frame

**Landed.** `analysis/c5-ground/darkness.ps1`, `darkness_measure.py` and `DARKNESS.md`. 14
`--freecam --det --fov=60` captures per view at `measure.ps1`'s `C5-cb7`, `C5-cb1`, `C5-alt` and
`C5-street`, classified with existing flags only: ground by flattening every `cblock` texture to two
greys (`--tex-override`), street and floor by reading each ground pixel back through its own
transfer curve, buildings against a `--no-clutter` twin, roofs by their gain under a zenith sun
(`--debug-lighting`). The ground renders at 0.56 of its texture luma at 16 and 0.70 from 64 up, the
same at every view to 1000 m, because it takes no Godot light. The new walls render at a median of
10 to 13 at three views and 27 over `cblock7`, roofs at 9 to 19, streets at 53 to 72. A floor mean
of texture 20 renders at about 12 (Decision 3).

**Verified.** A repeated capture is byte-identical (METHOD-2); the read-back floor at `C5-cb7` near,
8.7, matches the survey's 8.6; Faithful renders the ground at exactly its texel byte (SHOT-46).
Confidence: measured for the transfer, TUNE for the target. Not covered: no view stands under the
western district's fog volumes.

**Original approach (kept for reference).**

**Goal.** Decision 3's texture target (luma 25 to 35) has a rendered counterpart: the block floor,
the walls and the streets measured on today's Enhanced city at fixed C5 views.

**Evidence (confidence: lead-only).** Only texture luma is measured. The `cblock` ground's vertex
colours are all 255 and the tower skins' 181 to 221 mean (`docs/org/vertexLighting.md:340-344`),
so a rendered ratio will differ from the texture ratio.

**Approach.** <TODO: which views (the city plan's nine C5 views are the obvious set), and the
instrument, read against `docs/verification.md` first.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** Look judgements are the author's: a luminance number informs B15, it does not decide it.

# Wave B, the `cblock7` prototype

## B11 ☑ Export the footprint and street data the generator needs

**Landed.** `analysis/c5-ground/export_layout.py` writes `CSVM/data/city_models/C5/ground_layout.json`
(41 KB, derived geometry only). Per template: its lots, each the union of every model its
decorations' substitute rolls can pick with their `det` halves and further meshes, written as
convex hulls in texture UV (grown 0.5 m, holes filled) with the lot's top height `h` over every
roll; and its lamp and glow points. Per paint: its streets (family, centreline, half-width), its
variant cell (origin snapped to 0.5), and the building sets standing on it with their areas. A
lot's origin is wrapped into [0, 1) and its polygons are not, so a consumer draws each polygon
again shifted by ±1 in u and v.

**Verified.** Rasterised with the ±1 shifts, the exported footprints cover 1.00000 of `survey.py`'s
placed footprints on all seven templates within 0.25 m (area 1.01 to 1.25 times the placed set,
because every rolled substitute and `det` half is included). Every main set has 0.000 of its
footprint in its paint's streets except `cblock7` (0.003) and `cblock4` on `cblock7` paint
(0.001), which count as covered by a building. A second run is byte-identical. Confidence: traced.

**Original approach (kept for reference).**

**Goal.** The ground generator reads committed, derived data only: per template, each building
lot's footprint polygon in texture UV (grown by its awnings and fire escapes), the lamp post
points, and the street mask's outline. No game pixels.

**Evidence (confidence: direction-sound).** The city generator already reads no game file
(`docs/tooling.md:848-852`); the hulls it uses are measured by `analysis/city-hulls/tiers.py`. The
UV projection is `Clutter.cs:1251-1264`.

**Approach.** <TODO: format and location, next to `CSVM/data/city_models/` or inside `c5.py`'s
data.>

**Model recommendation.** <TODO>

**Verify.** <TODO: the exported footprints rasterised over the original texture match A1's overlay.>

**⚠ Traps.** The street mask is A1's geometric one (Decision 13), never a luma threshold: a luma
rule finds the lamp pools and misses the unlit asphalt between them. A substitute (`b`, `c`, `d`) can differ in shape from its `a` model in 29 of 39 pairs
(`analysis/city-hulls/FINDINGS.md`); the footprint must be the union over everything a lot can roll.
The `cblock2/3/5/6` buildings stand turned 45° in their own frames.

## B12 ☑ Generate the block floor variants for `cblock7`

**Landed.** `CSVM/data/city_models/c5_ground.py` reads only `ground_layout.json` and writes
`CSVM/data/city_models/C5/ground/`: 4 variants of `cblock7` paint with its own walk-ups and 2 of
`cblock7` paint with `cblock4`'s set, each 512 × 512 sRGB, one tile in the template's UV, seamless,
named `<paint>-<set>-<n>.png.bin` (every image under `data/` is `.bin`, so Godot does not import it
and the export ships its bytes); `index.json` lists the file names, which the engine reads instead
of building them. Beside each variant a layout JSON holds its surface cells (kind and region),
fence and wall lines and paths, for the dressing stamper. A block: a 3 m sidewalk verge, a ring of
lots 28 to 40 m deep (each building on a lot of its own width with an apron and a yard behind,
vacant lots, lot-line fences, rear walls), a core of back-yard strips, service yards, courts and
open lots, a 5 m service alley where no building blocks it, 25 % to 26 % green. Free floor mean
texture luma 20.0, p5 to p95 15.3 to 24.9. `--luma 30 --out <dir>` writes the comparison set,
kept outside the repo.

**Verified.** A rerun of both levels is byte-identical; the generator asserts its rolled-frame free
mask matches the image-frame mask on more than 99.9 % of texels; 0.1 % to 0.7 % of the free floor
is in no layout cell (slivers, paved, no dressing). The author saw the 3 × 3 montages (Decision 17).
Confidence: direction-sound, magnitudes TUNE.

**Original approach (kept for reference).**

**Goal.** 2 to 4 block floor variants at 512² for `cblock7` with its walk-ups, mostly paved with
some green (Decision 12), at a mean texture luma of about 20 with one comparison set at 30
(Decision 3), plus the mask that says where the original's street band shows through.

**Evidence (confidence: direction-sound, magnitudes TUNE).** `cblock7` is 15 % built with 94 % of
its painted structure exposed and its widest free spot 149 m.

**Approach.** A generator script beside `CSVM/data/city_models/c5.py` and `city_atlas.py`, following
their pattern, reading only `ground_layout.json`. The floor's materials and garden layout follow
Decision 12 and are shown to the author on montages before B13 wires them; B15 judges them flown.

**The contract with B13.** The generator writes, per combination of visible paint and standing set
(`ground_layout.json`'s `paints.<paint>.sets`, `none` included), N variants as
`CSVM/data/city_models/C5/ground/<paint>-<set>-<n>.png`: 512 × 512, sRGB RGB, covering one 256 m
tile in the template's own UV (+U right, +V down the image), seamless across the tile edges. Texels
in a street band or under a lot's footprint are don't-care (the engine draws the original there
and a building stands on the rest). `ground/index.json` lists each combination's variant count.
The level-30 comparison set of Decision 3 is written outside the repo, for B15 only.

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The street mask must stop at the street edge the paint has, not the building lot, or a
lamp's painted glow is cut off. A variant seam lies on a street centreline (Decision 4), so the
floor at the half-tile cell edge must be street, never block.

## B13 ☑ Composite and select the new ground in the engine

**Landed.** `CSVM/src/Mech3/CityGround.cs` reads `ground_layout.json` and the floor folder's
`index.json` into one mipmapped texture array per paint, and gives each painted polygon reached
from `world1` its building set: clear paint its own; flagged paint the coplanar clear layer under
the centre of its largest triangle (within 0.5 m; coverage is all or nothing per polygon, A1), the
rule `Clutter` and `VegetationGround` use for which layer decorates; otherwise `none`. It keeps the
paint off the `U+X V+Z` 256 m frame, where two placements of one mesh disagree, for a set whose
`on_street` share exceeds 0.01, and for a combination with no image, naming the missing ones once in
the `city ground:` log line. `SceneBuilder` writes each paint polygon's first layer and variant
count into UV2 (zero keeps the paint) rather than splitting surface groups, which would move ranks
and depth bias; the material cache keys gain `Floor`; an Enhanced-only arm under shader key bit
2^29 draws the original paint (with its authored levels) inside the analytic street bands,
antialiased over a pixel, and a floor layer elsewhere, hashed on the world-XZ cell. The composite
runs per fragment, so no original pixel is copied. `WorldMerge` carries UV2. The global
`csky_city_ground` follows `CityModels.On` through `VegetationStage.Follow`. `--city-ground=<folder>`
reads another floor set. Load: 88 ms on C5 with six variants (census: 1,200 polygons floored, 31
off-frame, 13 with disagreeing placements, 756 kept).

**Verified.** `CityGroundTests` (set resolution on a hand-built world, the two-placement conflict,
the frame test, both parses) and the `city-ground` engine suite (UV2, the array and uniforms, no
other texture floored, the missing combination, live moves between Off/Near and Faithful/Enhanced,
Faithful's shader text free of the arm) pass beside `city-buildings`; `-Quick` passes. At `C5-cb7`
placeholder floors in two colours change on the street centrelines with the streets kept; 3D
Scenery off and Faithful frames are byte-identical with and without floors. The merged tree's run
is in the landing commit. Confidence: traced for the selection and the identity; the look is B15's.

**Original approach (kept for reference).**

**Goal.** Wherever `CityModels.On` holds, `cblock7` surfaces draw the street band from the
install's own texture and the generated floor elsewhere, a variant picked per block; Faithful and 3D
Scenery off are unchanged.

**Evidence (confidence: lead-only).** Surfaces are already grouped by `(material, priority,
no_clutter, sidedness, pass)` (`SceneBuilder.cs:2041-2059`), so flagged and clear ground can take
different materials. The authored levels are installed by `TextureArchive`.

**Approach.** Composite at load per texture level, the street band from the original's own level
(`cblock7`, `cblock7_1`) and the floor from the generated image's mip, into a texture array of
variants; the ground shader picks the layer by a hash of the half-tile cell on world XZ,
floor(x / 256 + 0.5) and floor(z / 256 + 0.5), never on raw UV (A2). The street test is analytic from
`ground_layout.json`'s streets (|c − centre − k| ≤ half), so its edge is sharp at every distance and
no mask texture exists. The combination per polygon: clear paint takes its own set; flagged paint
over a decorating base takes the base's set; flagged paint with none takes `none`. Reads the
contract in B12. <TODO: how the engine knows a flagged polygon's base set (the clutter pass knows
which polygons decorate; A1 found the coverage bimodal: 196 polygons wholly over a base, 254 over
none, 2 partly), where the composite runs, how the shader variant joins the material cache key, a
dev flag that points the floor at another folder for B15's level-30 comparison, and the load
cost.>

**Model recommendation.** <TODO>

**Verify.** <TODO: the full 8-chapter `--freecam` regression with unchanged counts outside C5, and a
C5 capture at a `cblock7` view with the city on and off.>

**⚠ Traps.** The layout assumes the `U+X V+Z` 256 m frame: ground laid in another frame (0.427 km²
of clear `cblock7` at 45 m or off-axis, 3.8 % of flagged `cblock1` turned) keeps the original
paint. So do the two minor combinations whose buildings stand across their paint's street
(`cblock2`'s set under turned `cblock1`, 0.066 km², and `cblock4`'s set on `cblock2` paint,
0.131 km²). Never drop a component of the material cache key (`SceneBuilder.cs:561-566`). The
`no_clutter` bias stays at half a level (`SceneBuilder.cs:486-492`). Original pixels are composited
at runtime and never written to the repo, a cache or a release.

## B14 ◐ Build and stamp the low block dressing

**Landed, the models.** `CSVM/data/city_models/c5_ground_dressing.py` builds `gd_wall`, `gd_fence`,
`gd_shed`, `gd_sedan`, `gd_truck`, `gd_ashcans`, `gd_bench` and `gd_hedge` in `C5/` through the
city pipeline (near and far nodes, 14 to 50 triangles near, 4 to 20 far, every structure under the
4 m cap, asserted), and `CSVM/data/vegetation_models/citytree.py` the city tree in
`vegetation_models/C5/` (178 triangles near, 24 far, 9.6 m). Pivots stand at the footprint centre on
the ground with the long axis on X; wall, fence and hedge are runs (8 m, 8 m, 3 m) laid end to end
or stretched by up to a quarter; the fence's boards sit at z = 0 with its posts on −Z. The seed
(`INSTANCE_CUSTOM.w`) picks the near variant and varies grade and tint, and picks the truck's
poster. `analysis/city-hulls/margin.py` and `corners.py` skip the `gd_` files.

**Open, the stamping.** The `gd_` files load through `CityModels.Load` but need a C5 kind list (the
engine loads by key); the tree needs a `VegetationModels` registration and a grade reference.
Placement reads B12's layout JSON per variant: fences and walls on its lines, hedges on garden
edges, trees in gardens and lawns with about 8 m clear overhead, sheds, cars and bins in yards,
service yards and the alley, benches in gardens; nothing on a street, under a lot, or in a
no-cell sliver. Bench and ash cans fade early rather than drawing their far nodes.

**Original approach (kept for reference).**

**Goal.** Low, non-solid dressing (courtyard walls, fences, sheds, parked period cars, ash cans,
trees) stands in the free area of `cblock7`'s blocks, rolled per stamp from its seed.

**Evidence (confidence: lead-only).** The Enhanced vegetation models are drawn on card stamps and
never solid (`docs/architecture/Mech3.md`, the `VegetationModels` entry); the city's `det`
dressing fades at 75 to 250 m (`analysis/city-hulls/FINDINGS.md`).

**Approach.** <TODO: whether the dressing is a new template-like stamp list in texture space or
part of the generated layout; reuse of the vegetation models for trees; fade distances.>

**Model recommendation.** <TODO>

**Verify.** <TODO: no dressing inside a hull grown by its awnings; no collider added (the collision
shape count stays 123 shapes, 4,224 triangles).>

**⚠ Traps.** Height cap about 4 m (TUNE, Decision 5). The dressing must not suggest a landable spot
where a collider stands. The distant draw count must not rise (Decision 10).

## B15 ☐ Judge the prototype at the controls

**Goal.** The author's verdict on the `cblock7` prototype: brightness, variant count, the
paved/green mix, the height cap.

**Evidence (confidence: n/a).** Look judgements are the author's.

**Approach.** Montages of fixed views first, then a flight; ask the look questions one at a time
with the question dialog after the flight, and record the answers in the Decisions table.

**Model recommendation.** <TODO>

**Verify.** The author's answers, recorded.

**⚠ Traps.** Never park a visible feature on a luminance distance alone.

# Wave C, the whole city

## C21 ☐ The square districts' other combinations

**Goal.** `cblock1` (clear, and flagged with `cblock4`'s set) and `cblock7` flagged with
`cblock4`'s set draw the generated ground. No `cblock4` base is exposed (A1), so it needs no floor.

**Evidence (confidence: direction-sound).** The combinations table. The 0.427 km² of clear
`cblock7` in a 45 m or off-axis frame stamps its buildings in that frame (inferred); check what the
generated floor does there before calling the district done.

**Approach.** Extend B11 to B14 per combination. <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** A flagged paint carries the beneath set's layout, never its own (⚠ table, row 2).

## C22 ☐ The diagonal districts

**Goal.** `cblock2` and `cblock3`, each with both building sets, draw the generated ground, a
variant per 181 m strip (Decision 7). The small combinations go here too: `cblock2` paint with
`cblock4`'s set (0.131 km²) and flagged `cblock1` over clear `cblock2` (0.066 km²).

**Evidence (confidence: direction-sound).** A2: one street family per paint, centrelines
u ± v = 0.5 + k, the strip index floor(u ± v − 0.5) repeats with the tile. 0.008 km² of
`cblock3`/`cblock6` coplanar with neither flagged has no rule; leave it to whichever layer wins today.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The buildings here stand turned 45° in their own frames (`docs/tooling.md:850-852`).

## C23 ☐ Open ground on the bare flagged blocks

**Goal.** Flagged ground with no base beneath shows parks, plazas, parking lots and vacant lots
(Decision 8), no building-like shape.

**Evidence (confidence: direction-sound).** 12.96 km², 7.2 % of C5's ground, in 55 to 68 polygons
per paint; its clusters are in `analysis/c5-ground/FINDINGS.md` (A2).

**Approach.** The generator with an empty footprint set. <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** No collider stands there, so nothing drawn may look like one.

## C24 ☐ Measure the cost on the PC and the Deck

**Goal.** Decision 10's budget holds for buildings, ground and dressing together.

**Evidence (confidence: n/a).** The buildings alone: at most +0.58 ms GPU and +0.153 ms CPU over
nine C5 views on the RTX 5080, every Deck view at 71 fps or more at one pane (the city plan's
closing commit, `git log --grep=PLAN-city-buildings`).

**Approach.** The city plan's instrument and views. <TODO: name the script.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The texture memory of the variant arrays counts on the Deck.

## C25 ☐ Checks: suite and goldens

**Goal.** The ground is pinned by a suite and goldens like the buildings (`city-buildings` suite,
goldens `c5-city-night-enhanced` and `c5-city-towers-enhanced`).

**Evidence (confidence: n/a).**

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** The complete `.\scripts\RunTests.ps1`.

**⚠ Traps.** Tests never read modding folders; a golden must not move under external GPU load.
