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
| 3 | How bright the floor reads at night | **Dim, readable**: texture luma about 25 to 35 (TUNE at the controls), under the walls (40 to 70) and the streets (82 to 89). |
| 4 | Repetition | **2 to 4 floor variants per combination, picked per block** by a hash of the block's texture cell, the cells shifted half a tile so seams fall on street centrelines. The dressing rolls per stamp from its seed like the buildings. |
| 5 | Colliders on the dressing | **Never solid, kept low**: a structure height cap of about 4 m (TUNE), trees under the nearby eaves. |
| 6 | Texel size | **50 cm** (512² per 256 m tile). |
| 7 | Diagonal districts (`cblock2/3` paint) | **Rotated variant cells if the 45° streets form a regular grid** (measured first, A2); otherwise one design per combination with only the dressing varied. |
| 8 | "No clutter" ground with no layer beneath (no buildings) | **Open ground per block**: parks, plazas, parking lots, vacant lots, by the variant hash, with low dressing. |
| 9 | What switches it on | **`CityModels.On`**: the new ground draws exactly when the new buildings do. No new option. |
| 10 | Budget | **Shared with the buildings**: buildings, ground and dressing together at most +2.0 ms GPU and +0.5 ms CPU over the original city on the city plan's nine C5 views, distant draws at or under the original city's. The Deck keeps its one-pane default only at 60 fps or more on every view. |
| 11 | First prototype | **`cblock7`** with its own walk-ups, judged at the controls and on montages before the other combinations. |
| 12 | Character | **Mostly paved, some green**: back courts, alleys and service yards (asphalt, gravel, flagstone, fences, sheds, parked cars, ash cans), about a quarter of the free area as small gardens with trees, open blocks as parks or parking lots. |

Settled as approach, not put to the author: each combination of visible paint and standing
building set gets its own floor (the engine already splits `no_clutter` surfaces); the street band
is composited at every texture level from the original's authored `_1`/`_2` levels, so distant
streets keep their lamp points; the floor and the dressing keep clear of the original hulls grown by
their awnings and fire escapes, and of the lamp posts.

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The painted roofs are the original buildings' roofs, exposed because the new models are carved up to 3 m inside the original hulls. | The paint never fitted the template layouts. Under all eight axis-aligned texture frames the texels inside the original footprints average luma 3 to 6; 72 % to 94 % of the bright painted structure lies outside every footprint (this session's survey, A1). The new models only make the old mismatch legible. |
| 2 | One replacement texture per `cblock` texture is enough. | "No clutter" `cblock1/2/3` (and `cblock7`) paint is dressed by the `cblock4/5/6` building set beneath it (`docs/org/clutter.md:163-170`), while the clear paint carries its own set. The two sets share only 47 % to 76 % of the beneath set's footprint area, so one paint carries two layouts. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Measured from shipped data, this session** | A1, A2 (the survey numbers) | Re-run the committed script, then build on it. |
| **Direction sound, magnitude TUNE** | B12, B14, B15 (brightness, height cap, variant count, mix) | The *what* is the author's decision; the numbers are set at the controls. |
| **Leads only** | B13 (the load-time composite and the variant pick), C22 (diagonal cells) | Budget for investigation; the mechanism is proposed, not traced. |

## What the data actually ships

All from `extracted/C5` (`gamez`, `texture`, `zrdr/templates.zrd.json`), through
`analysis/city-hulls/census.py`'s loader. Luma is Rec. 601 on sRGB texels, 0 to 255.

**The ground is two coplanar layers.** `cblock1/2/3` (and `cblock7`) are drawn over `cblock4/5/6`
at the same height and the same 256 m texture repeat (`analysis/item9-depth-bias/CBLOCK-LOD.md`
§1c, §2). The `no_clutter` polygon flag picks which layer decorates and the flagged layer draws on
top (`CSVM/src/Mech3/SceneBuilder.cs:486-492`, `docs/org/clutter.md:163-170`). Flagged overlay with
no base beneath it, 7.6 % of C5's ground, carries no buildings.

**The combinations of visible paint and standing building set:**

| Visible paint | Building set | Built share of block area | Painted structure (luma ≥ 25) uncovered | Free floor luma mean, median | Free floor at luma ≤ 5 | Widest free spot |
|---|---|---|---|---|---|---|
| `cblock1` | `cblock1` (clear) | 0.27 | 0.84 | 8.6, 0 | 0.65 | 72 m |
| `cblock1` | `cblock4` (flagged) | 0.26 | 0.80 | 8.2, 0 | 0.66 | 160 m |
| `cblock2` | `cblock2` (clear) | 0.27 | 0.82 | 6.6, 0 | 0.72 | 81 m |
| `cblock2` | `cblock5` (flagged) | 0.23 | 0.72 | 5.9, 0 | 0.74 | 96 m |
| `cblock3` | `cblock3` (clear) | 0.29 | 0.80 | 5.1, 0 | 0.78 | 75 m |
| `cblock3` | `cblock6` (flagged) | 0.26 | 0.79 | 4.9, 0 | 0.78 | 74 m |
| `cblock7` | `cblock7` (clear) | 0.15 | 0.94 | 8.6, 0 | 0.65 | 149 m |
| `cblock7` | `cblock4` (flagged) | 0.29 | 0.77 | 8.8, 0 | 0.65 | 158 m |
| `cblock4` | `cblock4` (exposed base) | 0.30 | 0.71 | 28.5, 30 | 0.13 | 96 m |
| `cblock1/2/3/7` | none (flagged, no base) | 0 | 1.00 | <TODO: per paint> | | |

"Street" is texels whose 16 m box-filtered luma exceeds 55; blocks are everything else. Streets
average luma 82 to 89; the original building textures 40 to 70
(`analysis/city-hulls/FINDINGS.md`, Hulls table). `cblock7` is `cblock1`'s art at 128²
(r = 0.983, `CBLOCK-LOD.md` §1a) under small walk-ups, the largest district at 75.8 km².

**Footprints map to the paint through the template quad.** A decoration's texture UV is its origin
projected through the template ground quad (`CSVM/src/Mech3/Clutter.cs:1251-1264`), the quad frame
is +U = +X, +V = +Z, and 93.6 % to 100 % of every `cblock` texture's ground area is laid in that
same frame at 255 to 259 m per repeat, so a design made in a template's texture space lands under
its buildings everywhere.

**The paint's own levels.** `cblock1_1` (128²) averages luma 4.41 and `cblock1_2` (64²) 6.77,
against a box filter's 15.6: hand-authored, darker, the lamp points kept (`CBLOCK-LOD.md` §1b).
`TextureArchive` installs them (`--mips=authored`).

Caveat: this session's world walk summed `cblock1` clear ground to 51.5 km² where
`CBLOCK-LOD.md` §2 has 32.4 km²; only shares are quoted until A1 settles which is right.

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

1. ☐ Commit the #357 ground survey as `analysis/c5-ground/`
2. ☐ Measure the diagonal districts' street grid and the bare flagged ground
3. ☐ Measure the ground's darkness in a rendered C5 frame

### Wave B, the `cblock7` prototype

11. ☐ Export the footprint and street data the generator needs
12. ☐ Generate the block floor variants for `cblock7`
13. ☐ Composite and select the new ground in the engine
14. ☐ Build and stamp the low block dressing
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

## A1 ☐ Commit the #357 ground survey as `analysis/c5-ground/`

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

## A2 ☐ Measure the diagonal districts' street grid and the bare flagged ground

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

## A3 ☐ Measure the ground's darkness in a rendered C5 frame

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

## B11 ☐ Export the footprint and street data the generator needs

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

**⚠ Traps.** A substitute (`b`, `c`, `d`) can differ in shape from its `a` model in 29 of 39 pairs
(`analysis/city-hulls/FINDINGS.md`); the footprint must be the union over everything a lot can roll.
The `cblock2/3/5/6` buildings stand turned 45° in their own frames.

## B12 ☐ Generate the block floor variants for `cblock7`

**Goal.** 2 to 4 block floor variants at 512² for `cblock7` with its walk-ups, mostly paved with
some green (Decision 12), dim and readable (Decision 3), plus the mask that says where the
original's street band shows through.

**Evidence (confidence: direction-sound, magnitudes TUNE).** `cblock7` is 15 % built with 94 % of
its painted structure exposed and its widest free spot 149 m.

**Approach.** A generator script beside `CSVM/data/city_models/c5.py` and `city_atlas.py`, following
their pattern. <TODO: the floor's material vocabulary and how gardens are laid out.>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The street mask must stop at the street edge the paint has, not the building lot, or a
lamp's painted glow is cut off. A variant seam lies on a street centreline (Decision 4), so the
floor at the half-tile cell edge must be street, never block.

## B13 ☐ Composite and select the new ground in the engine

**Goal.** Wherever `CityModels.On` holds, `cblock7` surfaces draw the street band from the
install's own texture and the generated floor elsewhere, a variant picked per block; Faithful and 3D
Scenery off are unchanged.

**Evidence (confidence: lead-only).** Surfaces are already grouped by `(material, priority,
no_clutter, sidedness, pass)` (`SceneBuilder.cs:2041-2059`), so flagged and clear ground can take
different materials. The authored levels are installed by `TextureArchive`.

**Approach.** Composite at load per texture level, the street band from the original's own level
(`cblock7`, `cblock7_1`) and the floor from the generated image's mip, into a texture array of
variants; the ground shader picks the layer by a hash of the half-tile-shifted texture cell.
<TODO: where the composite runs, how the shader variant joins the material cache key, and what it
costs at load.>

**Model recommendation.** <TODO>

**Verify.** <TODO: the full 8-chapter `--freecam` regression with unchanged counts outside C5, and a
C5 capture at a `cblock7` view with the city on and off.>

**⚠ Traps.** Never drop a component of the material cache key (`SceneBuilder.cs:561-566`). The
`no_clutter` bias stays at half a level (`SceneBuilder.cs:486-492`). Original pixels are composited
at runtime and never written to the repo, a cache or a release.

## B14 ☐ Build and stamp the low block dressing

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

**Goal.** `cblock1` (clear, and flagged with `cblock4`'s set), `cblock7` flagged with `cblock4`'s
set, and the exposed `cblock4` draw the generated ground.

**Evidence (confidence: direction-sound).** The combinations table.

**Approach.** Extend B11 to B14 per combination. <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** A flagged paint carries the beneath set's layout, never its own (⚠ table, row 2).

## C22 ☐ The diagonal districts

**Goal.** `cblock2` and `cblock3`, each with both building sets, draw the generated ground, varied
as A2 allows (Decision 7).

**Evidence (confidence: lead-only).** Depends on A2.

**Approach.** <TODO>

**Model recommendation.** <TODO>

**Verify.** <TODO>

**⚠ Traps.** The buildings here stand turned 45° in their own frames (`docs/tooling.md:850-852`).

## C23 ☐ Open ground on the bare flagged blocks

**Goal.** Flagged ground with no base beneath shows parks, plazas, parking lots and vacant lots
(Decision 8), no building-like shape.

**Evidence (confidence: direction-sound).** 7.6 % of C5's ground (`docs/org/clutter.md:169-170`).

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
