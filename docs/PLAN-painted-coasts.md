# Painted coasts: the wave ocean up to the shorelines painted into tiles and strips

**ACTIVE PLAN** (written 2026-11-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan delivers issue #270. Under Enhanced graphics at the `waves` water quality, the wave ocean
stops short of every coast whose shoreline the authors painted into a flat texture at y = 0 rather
than modelled as a polygon edge: C1's `water1_trans1`/`water1_trans2` tiles, C3's `shore1` and
`shore_trans` strips and flat `cliff1_watertrans*` polygons, C2's `beach1` strip, and the
`cliff01_trans2` rock edge that C1 draws as an overlay over its tiles and C2 lays as a decal over its
sheet at the foot of its cliffs (the author's report of C2 coast tiles like C1's, measured in A2; #270
does not list them). The plan teaches the shore mask to read the coastline out of those textures, so the waves run to the painted waterline
and the tiles' water side steps aside for them. It builds on what PLAN-shore-water landed: the surf
kind and plane (its A1), the segment-based shore distance (A2), the surf coverage in the material
(A3), the ring's ocean hide (B11) and the procedural surf (C22).

#270 was re-verified still open in this session against the issue (state OPEN, its blocker
PLAN-shore-water merged at 4009bd4c0) and against the code: the mask still reads painted-coast
triangles as `Kind.Edge` or solid (`CSVM/src/Effects/OceanMask.cs:496-586`). #270's thread records
that the blocker landed, and its `blocked` label is dropped.

Out of scope, each a separate question: a surf band along a quay or any coast with no authored ring,
which needs a fallback width from the shore distance (the C2 comment on #270 names it); the decals
lying on the water, C2's `eiffel2`, which mark their texels solid and belong to #272; and C5, C2B and
C1C, whose land already meets the base sheet, and C4, which has no sea at y = 0.

## Milestone goal

- In C1, C2 and C3 the wave ocean runs to the waterline painted into each coast texture, with no flat
  sheet between the waves and the painted sand or cliff base.
- The shore distance is exact to that painted waterline, so the swell and look ramps and the
  procedural surf measure from it.
- The land half of every painted tile and strip keeps drawing; only its water side steps aside.
- The Original water path is byte-identical, and every Original golden holds.

**The coast textures stay land or water exactly as `ClassifySurface` calls them today.** The mask gets
its own predicate, because the colliders and the tile census read that classification too.

## Decisions (2026-11-01)

| # | Question | Decision |
|---|---|---|
| 1 | Where each texture's shore U is computed | **In `TextureArchive` at runtime, from the authored pixels, cached and passed into the bake from `Ocean.Create`**, on the soft-alpha check's precedent; no extraction-time sidecar exists for pixel-derived values |
| 2 | How B12 splits a triangle into sea and land | **Per texel, from the interpolated U against the shore U**, reusing the surf case's raster; no triangle clipper |
| 3 | Golden coverage of the painted coasts | **Three new Enhanced `--water-quality=waves` goldens, one per chapter** (a C1 coast tile, C2's beach, a C3 shore strip), pinned in D31 after C22's verdict |
| 4 | Whether C22 re-judges C3's ramps | **Yes**: a montage of today's ramps beside C1B's and C2's, the author picks, the value lands in `ocean_seas.json` |
| 5 | Whether C22 re-judges C1's look ramp | **Yes**, the same montage: `look_from` 55 beside 0; #270 does not name it, but it may hide the tile gap as C3's ramps do |
| 6 | Where the waves stop on a texture whose colour fades with no step | **C1: at the rock overlay's edge**, `cliff01_trans2`'s alpha 0.5, so `water1_trans*` is sea up to the overlay; **C3's cliff tiles: where the colour first leaves the painted water** (the 10 % point, v 0.777 and 0.762). Alpha strips (`shore1`, `shore_trans`, `beach1`, `cliff01_trans2`) stop at alpha 0.5 on their water side. Chosen from a montage of the candidate lines on the textures |
| 7 | C3's corner tile `cliff1_watertrans3`, whose water is a wedge no one-axis crossing places | **Left land, as today**: 36 polygons, 0.16 km², 7 % water texels; no two-dimensional water map |
| 8 | Whether the water washes over the painted beach and rocks | **Yes, in this plan, with spray at the cliffs** (C23, C24). The swash is drawn opaque by the coast textures' own shader, not by a transparent ocean: a transparent ocean loses the depth write SSR and TAA read, and sorts against the alpha-blended strips |
| 9 | How the coast textures give way to the waves | **By their own texture coordinate, not by the mask.** The mask still drives the waves. The alpha strips and C1's rock overlay take no hide and keep their own soft alpha over the waves, except that `beach1`'s alpha also fades past its crossing, thinning the grey haze its water side lays over the waves. C3's opaque cliff tiles fade out along their own UV by a dithered discard that TAA smooths, across the whole stretch from the crossing to their water edge. C1's sea tiles keep the base sheet's mask hide: discarded whole they open holes in the overlay's gaps, where the mask calls the ground land. The mask-driven hide on every coast had cut the painted fringe to sawteeth and staircases (C3's strip and cliffs, C2's decal); chosen from the C21 montages |
| 10 | C3's `shore2`, a flat blended decal of painted shallows and foam on the sheet, which the mask read as solid (a calm band 24 to 51 m off the coasts, 1,033 flat triangles in 15 runs) | **Sea in the mask, and under Enhanced with waves the decal fades out**, so the procedural surf replaces the painted shallows; its 2 rising triangles stay solid |
| 11 | The cliff tiles' fixed 4x4 dither reads as a lattice in motion | **A dither that changes per frame**, which TAA averages into a smooth fade; the tiles stay opaque |
| 12 | C3's near-shore ramps (decision 4) | **C1B's values: swell 8 to 48 m, look 0 to 24 m**, after tracing and fixing the soft dark patch the swell change showed 40 to 60 m off the strip pose |
| 13 | C1's look ramp (decision 5) | **`look_from` 0**, `look_full` kept at 143 |
| 14 | The ocean's 8 m mask-texel sawtooth where it meets the sheet at C2's beach | **Sea under the alpha strips' opaque part**: the mask keeps sea landward of the painted waterline out to where the strip turns opaque, so the ocean's texel edge lies under the strip's own sand; the shore distance still measures from the painted waterline |
| 15 | What replaces `shore2`'s painted surf band at C3's cliffs once the decal fades (C3's sea had no procedural surf) | **C3 takes procedural surf**: `surf_wash` 0.7 and `surf_lines` 0.6, C1B's and C2's values, judged on a montage before landing |
| 16 | The surf coverage's texel edge at the waterline, which kept C2's sawtooth after decision 14 | **The coverage fills the alpha strips' translucent sand**, saturated out to the opaque point, so the foam's edge lies under the strip's opaque part as the ocean's edge does |
| 17 | How far the swash runs up the painted sand and rock | **12 m** (`OceanShader.SwashReach`), picked from contact sheets over a breaker cycle and reach strips at 6, 12 and 20 m |
| 18 | C1, whose sea had no surf and so no breakers to time a swash to | **C1 takes procedural surf**, `surf_wash` 0.7 and `surf_lines` 0.6, so breakers and swash reach its cliff foot, judged on a montage before landing |
| 19 | How the cliff spray is drawn | **The original's water-splash sprites** (`hg_splasher`'s `splasher` puffer, retuned from an impact to a breaker), picked from contact sheets over a breaker cycle beside a camera-facing shader sheet, which stays opt-in as `--coast-spray=sheet` |
| 20 | C3's cliffs, which stand 50 to 130 m behind the painted waterline | **No spray in C3**, off for the whole chapter in its sea (`"spray": 0` in `ocean_seas.json`): at its cliff feet the spray landed in the forest, far from the surf line |
| 21 | Whether quay walls and bare rock off the painted coasts take spray | **Cliffs only.** The bare sites spray only with `--coast-spray=…,quays` |
| 22 | The base sheet's tint at corners hidden under the tiles the ocean replaces, which spread straight-edged wedges into the visible sea | **The sheet corners under C1's sea tiles take the open sea's mean tint** (`OceanMaskRaster.TintUnder`). This departs from Original's authored inlet darkening, because the ocean shows what the tiles covered. C3's cliff tiles stay out: C3's bright tint ring runs on under the alpha strips, and moving only the cliff tiles' corners darkens the near-shore sea and notches it where they meet the strips (C25, rendered in D31) |
| 23 | How C3's opaque cliff tiles give way to the ocean | **Blended by alpha over 25 m seaward of the painted waterline**, measured by world distance from the inland image, as C2's beach blends, not dithered and not along each polygon's V; picked over 12 m and over the per-frame dither (C26) |
| 24 | Where the swash ends on C3's cliff tiles | **It fades out over 4 m past the coastline**, since the tile still draws near-opaque there and a stop on the line showed one triangle's straight waterline; a carry as long as the 25 m blend filled the mask's sea pockets inside the tiles with foam (C26) |

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | B12, B13, C21, D31 | The mask's gaps, the hide's gate and the golden coverage are read from the code and manifest; confirm the line numbers, then implement. |
| **Direction sound, magnitude a judgement call** | B11 | The crossings are measured (Wave A); the rule that finds them in the pixels must reproduce those figures. |
| **Leads only, no mechanism yet** | C22, C23, C24 | The author's look calls; C23 and C24 are new effects whose mechanism is chosen in the item. |

Wave A corrected #270 in five places; the data table below carries the corrections, and
`analysis/painted-coasts/FINDINGS.md` holds the measurements:

| # | The claim | What the data shows |
|---|---|---|
| 1 | C3's strips run from the land's edge out to sea, both edges 30 to 37 m from land | Flat land at y = 0 lies coplanar under each strip's sand half; its edge runs near the strip's centre line, and the painted waterline sits a third of the strip seaward of it (A1) |
| 2 | `shore1`'s alpha is transparent on the water side, then opaque sand | It is transparent at both edges; the water side is U = 0, told apart by its painted colour (A1) |
| 3 | 45 `cliff1_watertrans*` polygons climb the cliff face | One polygon does, at 41 to 81 m, and never meets the water; 257 of 258 are flat (A3) |
| 4 | C1's tiles are 256x64 gradients along U | `water1_trans2` is 256x256 with a diagonal seam, and both tiles carry the `cliff01_trans2` rock overlay over their landward strip (A2) |
| 5 | C2's coast is `beach1` alone | C2 also lays `cliff01_trans2` as a 30 m decal over its sheet at the cliff foot (A2) |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy. This plan runs in
`.claude/worktrees/painted-coasts` on branch `painted-coasts`.

## What the data actually ships

Measured in #270 and Wave A from the extracted gamez (unzbd output, node transforms applied); the
scripts and the full figures are in `analysis/painted-coasts/`, and
`analysis/shore-water/FINDINGS.md` holds the surf-ring survey this extends.

| Chapter | Texture | Geometry | Texture | Mask today |
|---|---|---|---|---|
| C1 | `water1_trans1` (129 polygons), `water1_trans2` (243) | flat tiles at y = 0, seaward edge on the `water1` sheet, landward edge on `cliff01` land; 128 m and 90.5 m across (A2) | `trans1` 256x64 RGB along U, `trans2` 256x256 RGB along the diagonal u + v; opaque, from the sheet's colour to a grass green with no step (A2) | `Kind.Edge` (water that is not the base sheet): shore, so the waves stop at the tile's seaward edge |
| C1 | `cliff01_trans2`, an overlay pass on 43 `trans1` and 64 `trans2` polygons | the tiles' landward strip, u 0.75 to 1 (32 m) or p 0 to 0.25 (22.6 m) | 256x64 RGBA rock edge, opaque at the cliff foot, alpha 0.5 at 19.7 m (`trans1`) and 13.9 m (`trans2`) from it (A2) | land over the tile, so solid |
| C2 | `cliff01_trans2` | 27 flat polygons at y = 0 (2 more climb to 8 m), 20 to 45 m across, a decal over the `wtr` sheet, which runs under it to the cliff foot; 31 `cliff01` rock polygons rise from its landward edge to 64 to 111 m (A2) | 64x256 RGBA along U, transparent at u = 0 (the water side), a ragged rock edge, alpha 0.5 at u = 0.398 (A2) | land, so solid out to its seaward edge |
| C2 | `beach1` | 58 flat polygons at y = 0, 89 to 589 m along the coast and 117 to 333 m across; flat `terpat` land at y = 0 lies under its sand half, its edge near the strip's centre line (A1) | 256x128 RGBA: painted surf foam along the water edge, wet sand, then dry sand and rocks, the alpha fading out at both long edges; the alpha runs along V | land, so the waves stop at the strip's seaward edge, 65 m or more from the painted waterline |
| C3 | `shore1` (487 polygons), `shore_trans` (55) | flat strips at y = 0, 17 to 116 m across (median 65 m); flat land at y = 0 lies under their sand half and its sea-level edge runs near their centre line, with only the base sheet under the water side (A1) | 128x256 and 128x128 RGBA along U, transparent at both edges: the painted waterline at U = 0.19 on the water side (U = 0), a foam band, then sand (A1) | land, so the waves stop at the strip's seaward edge and the strip draws over the flat sheet |
| C3 | `cliff1_watertrans`, `cliff1_watertrans2`, `cliff1_watertrans3` | 258 polygons, 257 flat at y = 0 against the cliffs (p10 to p90 64 to 149 m across), their landward edge on the cliff's foot and seaward edge on the base sheet; one `cliff1_watertrans` polygon sits at 41 to 81 m and never meets the water (A3) | 128x128 RGB, opaque, water painted into the cliff base along V, 30 to 33 RGB from the base sheet's colour, fading over about 70 m with no step; `cliff1_watertrans3` is a corner tile with a water wedge (A3) | land |
| C3 | `shore2` | 1,035 triangles, 1,033 flat on the sheet just seaward of the painted foam, 2 rising (C22) | RGBA painted shallows and foam, alpha fading at both U edges | solid, a calm band 24 to 51 m off the coasts; sea under decision 10 |

C2's authored `srf0001` ring (86 polygons) covers only the sea stack. C2's quays meet the `wtr` sheet
with the 1.03 m harbour ramp, nothing painted, so the waves already run to the quay walls.

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

### Wave A, measure first

1. ☑ What lies under C3's sand strips (#270)
2. ☑ C1's and C2's coast tiles: world width, shore U, and whether the land meets their landward edge (#270)
3. ☑ Which of C3's `cliff1_watertrans*` polygons are flat (#270)

### Wave B, the mask reads the painted coast

11. ☑ A shore U per coast texture, read from the texture's own profile at bake time (#270)
12. ☑ A painted-coast kind in the mask: sea on the water side of the iso-line with surf coverage, solid on the land side (#270)
13. ☑ The iso-line is a coastline segment for the exact shore distance (#270)

### Wave C, the coast steps aside

21. ☑ The painted tiles and strips give way to the waves by their own texture coordinate, C1's sea tiles by the mask (#270)
22. ☑ The look at the painted coasts in C1, C2 and C3, judged from montages, at the controls in the closing batch (#270)
23. ☑ Swash: each breaker runs up the painted sand and flat rocks as a wet film and foam, drawn by the coast textures' own shader (#270)
24. ☑ Spray where the breakers meet the cliffs rising from the painted coasts (#270)
25. ☑ The new ocean replaces the old water look completely under Enhanced at waves (#270)
26. ☑ C3's cliff tiles blend out by world distance from the waterline, as the beach does (#270)

### Wave D, landing

31. ☑ Goldens re-pinned where the Enhanced look moves, the battery and perf green on the merged tree (#270)

## Dependency and parallelism notes

A1, A2 and A3 are read-only measurements and can run in parallel. B11 needs A2 for the opaque-tile
case and A1 for the strips. B12 needs B11 and A3 (which polygons are candidates). B13 needs B12's
per-triangle iso-line. C21 needs B12, since the hide reads the mask's sea channel under the tile. C22
needs B13 and C21. C23 needs C22 (the waterline it washes over is settled first) and edits the same
shader generator as C21, so it follows C21 and never runs beside it. C24 needs B13's coastline and
C23's shared breaker timing. D31 is last, pinning the goldens over C23's and C24's verdicts. File contention: B12 and B13 both edit `CSVM/src/Effects/OceanMask.cs`
and `OceanMaskRaster.cs`, and C21 edits `CSVM/src/Mech3/SceneBuilder.cs`'s ocean-material selection;
never run B12, B13 and C21 in parallel worktrees.

---

# Wave A, measure first

## A1 ☑ What lies under C3's sand strips

**Landed.** `analysis/painted-coasts/a1_strips.py` and FINDINGS.md's A1 section. No land lies below
sea level under any strip in C3 or C2: under each strip there is only the base sheet and flat land at
y = 0, the land's edge near the strip's centre line, and the painted waterline (alpha 0.5 on the water
side, U 0.188 for `shore1`, 0.195 for `shore_trans`, V 0.125 for `beach1`) about a third of the strip
seaward of it. The strip's land side takes the solid write alone. Two poses per chapter recorded.

**Verified.** The script reproduces #270's census (487 and 55 polygons, 17 to 116 m across, `shore1`'s
edges 30 to 37 m from land) and finds 20 strips at their own height under their own samples; the
orchestrator re-ran it on C3 with the same figures. Four `--freecam --det` screenshots put the frame
centre on the strip at each pose.

**Original approach (kept for reference).**

**Goal.** It is known whether the land mesh under each C3 `shore1`/`shore_trans` strip lies below sea
level, at sea level, or is absent, so B12 knows whether the strip's opaque sand is the only land there.

**Evidence (confidence: lead-only).** #270: the strips' edges sit 30 to 37 m from the nearest land
vertex at sea level, so the land mesh does not meet the strip at its inner edge. If the land under the
strip is below sea level, the sand part needs the height treatment a ramp gets; if it is flat water,
the strip's opaque sand is the only land there.

**Approach.** Extend the `analysis/shore-water/` scripts (`coast_detail.py`, `seabed.py`) over C3's
gamez: for each strip triangle, sample the land surfaces under its footprint and report their heights.
Record the result in `analysis/shore-water/FINDINGS.md` or a new `analysis/painted-coasts/FINDINGS.md`,
with two mask-dump poses over the strips. Run the same pass over C2's `beach1` (58 polygons, #270's C2
comment) and record two poses there too.

**Model recommendation.** medium, low effort: a scripted measurement on the existing pattern.

**Verify.** The script first reproduces #270's census for C3 (487 `shore1` and 55 `shore_trans`
polygons, edges 30 to 37 m from the nearest land vertex at sea level) before any new value is read
from it (verification.md METHOD-9). The heights under the strips are then cross-checked at two strips
in a `--freecam --chapter=C3` screenshot.

**⚠ Traps.** The extracted gamez carries node transforms the script must apply, as the
`analysis/shore-water/` scripts do; an untransformed mesh reads a strip in the wrong place.

## A2 ☑ C1's and C2's coast tiles: world width, shore U, and whether the land meets their landward edge

**Landed.** `analysis/painted-coasts/a2_tiles.py` and FINDINGS.md's A2 section. C1's tiles meet the
sheet at their seaward edge and `cliff01` land at their landward edge; their colour fades with no step
from the sheet's colour to a grass green that is not the land they meet; `water1_trans2` is 256x256
with its ramp along the diagonal; and both carry `cliff01_trans2`, a rock edge, as an overlay pass
over their landward strip, passing alpha 0.5 at 19.7 m (`trans1`) and 13.9 m (`trans2`) from the cliff
foot. C2 has no opaque gradient tile: its coast tiles are `cliff01_trans2` laid as a 30 m decal over
the `wtr` sheet, alpha 0.5 at u = 0.398, with `cliff01` rock polygons rising from their landward edge.
Two poses per chapter recorded, not yet seen in a frame.

**Verified.** The water end of every C1 profile matches the `water1` sheet's mean texel to within 2.2
RGB; the orchestrator re-ran the script on C1 and confirmed the polygon counts and overlay passes, and
read `water1_trans2.png` as 256x256 and C2's `cliff01_trans2.png` as 64x256 RGBA.

**Original approach (kept for reference).**

**Goal.** The world width of C1's `water1_trans1`/`water1_trans2` tiles, the U at which their colour
gradient leaves the water's blue, and whether the land polygons meet each tile at its landward edge
are measured. The same is measured for C2's coast tiles, which the author reports are like C1's with
some rocks above the water; A2 names their textures and separates the rocks from the flat tiles.

**Evidence (confidence: lead-only).** #270: the surf ring's inner edge coincides with the land to
0.0 m, but the tiles were not measured. The tiles are 256x64 RGB, opaque, a gradient along U
(`analysis/shore-water/FINDINGS.md:39`).

**Approach.** Same script pattern as A1, over C1's gamez and the two textures' pixels: a
one-dimensional colour profile along U, the tiles' UV-to-world scale, and the gap from each tile's
landward edge to the nearest land edge at sea level.

**Model recommendation.** medium, low effort: a scripted measurement on the existing pattern.

**Verify.** The tile profile's water end matches the `water1` sheet's own texels, and its land end
matches the land texture the tile meets; the crossing U is recorded with the profile plot in the
findings.

**⚠ Traps.** The tiles have no alpha, so the crossing is a colour threshold, not an alpha of 0.5. A2
picks the threshold from the profile itself (the midpoint of the water-to-land colour distance,
unless the profile shows a sharper step) and records it; B11 applies the same rule.

## A3 ☑ Which of C3's `cliff1_watertrans*` polygons are flat

**Landed.** `analysis/painted-coasts/a3_cliffs.py` and FINDINGS.md's A3 section. 257 of the 258
polygons are flat at y = 0; the one that is not sits at 41 to 81 m and never meets the water, so the
mask's own per-triangle sea-level test separates them with a 40 m margin. V runs across the coast,
land at v = 0 on the cliff's foot and the sheet at v = 1. The colour leaves the painted water at
v 0.777 (`cliff1_watertrans`) and 0.762 (`cliff1_watertrans2`). `cliff1_watertrans3` is a corner tile
whose water is a wedge. No polygon carries an overlay pass. Two poses recorded, not yet seen in a frame.

**Verified.** Every v = 0 vertex lies within 0.5 m of a sea-level land vertex and every v = 1 vertex of
a sheet edge; the orchestrator re-ran the script with the same counts and crossings.

**Original approach (kept for reference).**

**Goal.** The flat `cliff1_watertrans*` polygons at y = 0 (the mask's candidates) are separated from
those that climb the cliff face (land).

**Evidence (confidence: lead-only).** #270: 258 flat polygons at y = 0, 90 to 130 m across, their
landward edge on the land vertices; 45 more rise up the cliff face to 81 m.

**Approach.** Same script pattern; classify each polygon by its height span and confirm the counts.
The same height test is what B12's predicate will apply at bake time, so record the threshold.

**Model recommendation.** medium, low effort: a scripted measurement on the existing pattern.

**Verify.** The split reproduces #270's 258 flat and 45 climbing polygons under the mask's own
sea-level test (`minY > -0.5 && maxY < 0.5`, `CSVM/src/Effects/OceanMask.cs:520`).

**⚠ Traps.** A polygon whose foot sits at y = 0 but climbs the cliff is land; only the whole-triangle
sea-level test makes a candidate.

# Wave B, the mask reads the painted coast

## B11 ☐ A shore U per coast texture, read from the texture's own profile at bake time

**Landed.** `CoastProfile` (`CSVM/src/Mech3/CoastProfile.cs`), an engine-free reduction of a texture's
authored pixels to its coast axis, water end, crossing and fade. `TextureArchive.CoastProfileOf` caches
it per texture and base sheet, reading the archive's own record (the pixels `FindImage(authored: true)`
wraps), so an HD pack cannot move it. The painted coasts are the name list `PaintedCoastline` (`beach1`,
`cliff01_trans2`, `cliff1_watertrans`, `cliff1_watertrans2`, `shore1`, `shore_trans`); C1's
`water1_trans*` are `IsPaintedSeaTile`; `cliff1_watertrans3` and `shore1_end` (only on C4's raised lake,
517 to 709 m) are not listed. `Ocean.Create` passes the archive into `OceanMask.Bake`, which fills
`CoastProfiles` once the base sheet is chosen and logs each profile once.

**Verified.** Each chapter's bake under Enhanced at `waves` logs, against the table below:
C1 `cliff01_trans2` V, water 1, 0.602 (A2's row reading is 0.617, inside one 1/64 texel); C2 `beach1`
V, 0, 0.121; C2 `cliff01_trans2` U, 0, 0.398, fade 0.422; C3 `cliff1_watertrans` V, 1, 0.781, fade
0.658; `cliff1_watertrans2` V, 1, 0.768, fade 0.613; `shore1` U, 0, 0.184; `shore_trans` U, 0, 0.195.
`CoastProfileTests` pins five synthetic cases and the seven real textures through `[GameFilesTheory]`
rows that ran against the install. The complete battery runs on the merged tree at the wave's end.

**Original approach (kept for reference).**

**Goal.** Each painted-coast texture carries the coordinate, along whichever UV axis runs across the
coast (U for C3's strips, V for C2's `beach1` and C3's cliff tiles), at which it crosses from water
to land, and the width of its painted foam fade, read from its pixels and never typed in.

**Evidence (confidence: direction-sound).** Wave A measured every crossing (FINDINGS.md), and
decisions 6 and 7 set which one is the coast:

| Texture | Axis, water side | Crossing the profile must reproduce |
|---|---|---|
| `shore1`, `shore_trans` (C3) | U, water at U = 0 | alpha 0.5 at U 0.188, 0.195 (the water-side crossing of two) |
| `beach1` (C2) | V, water at V = 0 | alpha 0.5 at V 0.125 (the water-side crossing of two) |
| `cliff01_trans2` (C2 decal, C1 overlay) | U in C2's 64x256; the C1 copy is 256x64 | alpha 0.5 at u 0.398 in C2; in C1 the overlay crosses at the tile's u 0.846 and p 0.154 |
| `cliff1_watertrans`, `cliff1_watertrans2` (C3) | V, water at v = 1 | the 10 % point of the colour profile normalised to its own ends, v 0.777, 0.762 |
| `water1_trans1`, `water1_trans2` (C1) | none | no crossing: the tile is sea, and its coast is the overlay's |
| `cliff1_watertrans3` (C3) | none | not a painted coast: left land |

**Approach.** `TextureArchive` (`CSVM/src/Mech3/TextureArchive.cs`) computes a coast profile at runtime
from the decoded authored image (`FindImage(name, authored: true)`, `:1073`) the first time it is asked
for, and caches it as it caches the soft-alpha check (`AlphaIsSoft`, `:1139`, cached in `_alphaInfo`)
(decision 1). `Ocean.Create` already holds the archive (`CSVM/src/Effects/Ocean.cs:113`) and passes it
into `OceanMask.Bake`, which today sees only the `SceneBuilder`. The profile is one-dimensional: the
axis is the one along which the texture varies, the water side is read from the pixels (for alpha, the
transparent end whose colour is nearer the chapter's base sheet; for an opaque ramp, the end nearer
it), and the crossing follows the table's rule. Which textures are painted coasts is a name list beside
`SoftAlphaCoastline` (`TextureArchive.cs:681-688`), as `ClassifySurface` matches by name; the values are
never typed in.

**Model recommendation.** medium: a pixel reduction on an existing cache pattern.

**Verify.** A unit test over synthetic images (a two-edged alpha strip, a one-edged alpha strip and an
opaque ramp) pins the axis, the water side, the crossing and the fade width. Each real texture's
profile is logged once at bake and matches the table to within one texel; the figures go in the
commit.

**⚠ Traps.**
- `shore1`'s alpha runs the opposite way to the surf ring's: transparent on the water side, opaque
  sand on the land side. `beach1`'s runs along V and fades at both long edges.
- `IsSurfTexture` (`CSVM/src/Mech3/SceneBuilder.cs:907`) matches `srf*` only, and the ring's coverage
  is `1 - u` (`CSVM/src/Effects/OceanMask.cs:585-586`). Neither holds for these textures.
- Read the authored pixels, never the HD replacement: an HD pack repaints the texture, and the mask
  must not move with a modding folder.
- A threshold taken from the base sheet's mean texel fires on texel noise for the cliff tiles, whose
  painted water sits 33 RGB from the sheet (A3); normalise to the profile's own ends.
- `water1_trans2`'s ramp runs along the diagonal; it needs no profile under decision 6, so do not
  build a diagonal reader for it.
- `SoftAlphaCoastline` also names `shore1_end`, which Wave A did not meet in any census; check whether
  it lies flat at y = 0 anywhere before leaving it out of the list.

## B12 ☐ A painted-coast kind in the mask

**Landed.** `Kind.Coast` in `OceanMaskRaster`, chosen by the mask's own predicate (the B11 name list
and `IsPaintedSeaTile`) on triangles passing the sea-level test, now the shared constant
`OceanMaskRaster.SeaLevelBand`. Once the base sheet and profiles are known, each corner stores how far
seaward of its profile's crossing it lies, in fade widths; sea tiles store 2 (sea, no coverage). The
raster interpolates it per texel: above 0 is water with coverage 1 at the crossing falling to 0 one
fade seaward, the surf ring's meaning; at or below 0 is land. Triangles only mark per-texel coast
arrays (land beats water, highest coverage wins) and each band resolves its own rows afterward, so
order and band count cannot change a texel. The water side is sea over coplanar land only: a solid
rising off the water keeps its texels and seed (`StandSolid`, `StandSeed`). The land side is solid
over the tile or sheet beneath. `coast_tris=` joins the `ocean: built grid` line, and the surf plane
now carries the painted coasts' coverage, so C1's and C3's waves calm and wash at them already. No
coastline segment comes from the iso-line yet (B13) and no shader changed (C21).

**Verified.** `dotnet build` 0 warnings; `dotnet test` 7960 passed, 0 failed, 6 skipped;
`OceanMaskRasterTests` covers the kind in the whole-image oracle at 1, 5 and 12 bands, plus four tests
in both triangle orders (sea to the waterline, sea over the land beneath a strip, an overlay's land
over its sea tile, a rising solid on a strip's water keeping its texels, which fails without the
narrowing). Mask dumps under `--freecam --det --graphics=enhanced --water-quality=waves` at two poses
per chapter put the sea's edge on the painted waterline: C1 at the rock overlay's edge, C2 on
`beach1`'s foam band and the decal's mean rock edge, C3 on `shore1`'s seaward foam fringe and the cliff
tiles' 10 % point. C1's 376 `cliff01_trans2` triangles are 262 over the tiles and 114 zero-area strip
joins the raster drops. `coast_tris` C1 1022, C2 121, C3 1315. The complete battery runs on the merged
tree at the wave's end.

**Original approach (kept for reference).**

**Goal.** In the mask, texels on the water side of each painted-coast triangle's iso-line are open sea
(`Sea = 1`) with a surf coverage measured across the texture's fade, and texels on the land side stay
shore, so no wave reaches the painted sand.

**Evidence (confidence: traced).**
- The mask classifies by `SceneBuilder.ClassifySurface` (`CSVM/src/Mech3/SceneBuilder.cs:872`, read at
  `CSVM/src/Effects/OceanMask.cs:496`), which calls `shore*`, `beach*` and `cliff1_watertrans*` land and
  `water1_trans*` water that is not the base sheet.
- Water at sea level that is not base or surf becomes `Kind.Edge` (`OceanMask.cs:566-572`), which the
  raster writes as `Sea = 2` (`CSVM/src/Effects/OceanMaskRaster.cs:341-344`). Non-water near sea level
  becomes `Kind.Solid` (`OceanMask.cs:573-577`).
- UVs are loaded only for base and surf triangles (`OceanMask.cs:505-506`).
- `Kind` has four members (`OceanMaskRaster.cs:57-63`); the surf case writes sea and the interpolated
  coverage (`OceanMaskRaster.cs:345-352`).

**Approach.** Give the mask its own predicate for the painted-coast textures, separate from
`ClassifySurface`. Load their UVs. Add a kind whose raster case splits each triangle along the iso-line
of B11's shore U per texel (decision 2): each texel interpolates the triangle's U from the barycentric
weights the raster already computes, as the surf case does (`OceanMaskRaster.cs:345-352`). A texel on
the water side is written as the surf case writes it, with the coverage measured across B11's fade. A
texel on the land side is written as a solid (`Sea = 0`). Candidate polygons are those A3's height
test calls flat. A1 finds no land below sea level under any strip, so the land side takes the solid
write alone, with no ramp lift. `water1_trans*` triangles are sea throughout, with no coverage of their
own (decision 6). A painted-coast texel decides its texel over every other kind there: its water side
is sea over coplanar land (A1's strips), and its land side is solid over the tile or sheet beneath it
(C1's overlay on `water1_trans*`, C2's `cliff01_trans2` decal on the `wtr` sheet). Where two painted
coasts overlap (`shore_trans` over the cliff tiles, the overlay over its tile), the land side wins.
Overlay passes are their own surfaces with their own UVs (`CSVM/src/Mech3/SceneBuilder.cs:433-438`,
`:1043-1047`), so the mask reads `cliff01_trans2` as it reads any other painted coast.

**Model recommendation.** high: a new mask kind across the banded raster, the dump and the counters.

**Verify.** A unit test in `CSVM.Tests/OceanMaskRasterTests.cs` on the pattern of
`TheSurfRingIsSeaMeasuredFromTheLand`: a synthetic strip of the new kind beside a base triangle,
asserting sea and coverage on the water side of the iso-line, shore on the land side, and
band-count independence (`EveryBandCountMatchesTheWholeImagePass` extended to the new kind). In the
engine, `--dump-ocean-mask=<abs path>.png` paired with `--screenshot=` (verification.md SHOT-10) in C1,
C2 and C3: the R channel ends on the painted waterline inside each tile and strip, and the `.surf.png`
carries the fade. The build log's `ocean: built grid` line (`CSVM/src/Effects/Ocean.cs:169`) shows the
new kind's triangle count. The dump poses are two per chapter, recorded by A1 (C3 strips), A2 (C1
tiles) and A3 (C3 cliffs); A1 also records C2's two `beach1` poses with the same script.

**⚠ Traps.**
- Do not change `ClassifySurface`: the colliders and the tile census read it.
- A painted tile's mask under its land half must read `Sea = 0`, or C21's hide discards land. `Kind.Edge`
  is not that: it writes `Sea = 2` (`OceanMaskRaster.cs:342`), which encodes as R = 255 (`:451`), and the
  hide reads R alone. C1's tiles are `Kind.Edge` today, so their land half must move to the solid write.
- Flat land at y = 0 lies coplanar under the strips (A1): under `shore1`'s painted water (U below
  0.25) 12 % of samples have land beneath them, and under `shore_trans` some of that land is A3's
  cliff tiles. That land is a solid in the mask and its edges are coastline segments today, so on the
  water side of the painted waterline the strip's sea has to win over the land under it, or the waves
  stop at the land mesh's edge near the strip's centre line instead of at the painted waterline.
- `shore1`'s and `shore_trans`'s alpha crosses 0.5 twice, at both U edges; the water side is U = 0,
  told apart by its painted colour (A1). B11 takes the crossing on that side.
- The precedence cannot rest on triangle order: the raster's bands and the parallel pass must give the
  same texel whatever order the surfaces arrive in (`EveryBandCountMatchesTheWholeImagePass`).
- Moving-hull removal decrements the per-kind counters (`OceanMask.cs:650-656`); a new kind needs its
  counter there too.

## B13 ☐ The iso-line is a coastline segment for the exact shore distance

**Landed.** `OceanMaskRaster.Coastline` adds each `Kind.Coast` triangle's waterline, where its stored
seaward value crosses 0, as a segment (sea tiles never cross 0 and add none). A flat solid's coast
edges keep only the parts that lie on no painted coast's water side, found through a 128 m bucket
index, with the raster's own barycentric tolerance; the covered stretches are sorted and merged, so
coast order cannot change a piece, and the coastline is built once for all bands. A rising solid on
painted water keeps its foot. The same drop applies under C1's sea tiles, where flat land edges had cut
the distance to 1.3 m some 30 m out.

**Verified.** `dotnet test` 7963 passed, 0 failed, 6 skipped; new tests pin the exact distance to a
diagonal strip's waterline (30° and 45°, within 0.5 m) and that coplanar land under a strip's water
adds no coastline while a rock's foot does, in both triangle orders; the whole-image oracle includes
both at 1, 5 and 12 bands, and fails with the drop switched off. Mask dumps before and after at the
FINDINGS.md poses: R byte-identical; G shorter at 51,022 / 40,743 / 43,552 sea texels (C1 / C2 / C3,
median 4.4 to 5 m) and longer at 150 / 0 / 7,807 where dropped land edges had stood; G grows from the
painted waterline at every pose, and the vertical banding on C3's strips is gone. The distance pass,
two runs each side on a noisy machine: C1 no change beyond noise, C2 +10 ms within its spread, C3
about +24 ms against a 10 to 13 ms spread.

**Original approach (kept for reference).**

**Goal.** The shore distance in front of a painted coast is exact to the painted waterline, as it is to
a land polygon's edge since PLAN-shore-water A2.

**Evidence (confidence: traced).** `OceanMaskRaster.Coastline` (`CSVM/src/Effects/OceanMaskRaster.cs:463-484`)
takes segments only from `Kind.Solid` triangles: their coast edges and their cut through y = 0.
`Exact` (`OceanMaskRaster.cs:498`) lowers each open-sea texel to its distance from those segments.

**Approach.** Add each painted-coast triangle's iso-line, a straight segment in world XZ, to the
coastline list.

**Model recommendation.** medium: one more segment source on an existing pass.

**Verify.** A unit test on the pattern of `ADiagonalCoastReadsTheExactDistance`
(`CSVM.Tests/OceanMaskRasterTests.cs:96`): a texel in front of a synthetic strip reads its exact
distance to the iso-line. The bake time per chapter is read from the `distance=` field of the
`ocean: built grid` line (`CSVM/src/Effects/Ocean.cs:169`, phases at `OceanMask.cs:313-314`) in C1, C2
and C3, before and after, two runs each side (verification.md PERF-9).

**⚠ Traps.**
- Coplanar land under a strip's water side (A1: 12 % of `shore1`'s painted-water samples) is a solid
  whose edges `Coastline` adds today. Left in, those segments lie seaward of the painted waterline and
  shorten the shore distance there; a solid edge inside a painted coast's water side is not coastline.
- PLAN-shore-water's closing commit records C1's bake distance pass tripling to 73 ms from
its many low harbour edges. Every new segment adds to that pass, so measure the bake time per chapter
before and after.

# Wave C, the coast steps aside

## C21 ☐ The painted tiles and strips carry the ocean hide

**Landed.** Decision 9, after a first build gave every painted coast the mask-driven hide and cut the
painted fringe to sawteeth and staircases. `SceneBuilder.IsPaintedCoastTexture` sets a painted-coast
flag (its own key bit, beside `oceanBase` and `oceanSurf`, never through `waterLit`, so sand and rock
stay matte) that C23 can reuse. What a coast does follows from flags already in its key: C1's sea tiles
take the base sheet's mask hide; C3's opaque cliff tiles fade out along their own UV past their
crossing by a dithered discard (`csky_clutter_dither_keep`), across the whole stretch to their water
edge (`CoastFadeShare` 1.0, the author's pick); `beach1`'s alpha fades the same way, thinning its grey
haze over the waves; `shore1`, `shore_trans` and `cliff01_trans2` (the C2 decal and the C1 overlay) keep
their authored alpha. The fade parameters (`uniform vec4 coast_fade`: axis, water end, crossing,
width) come from the mask's own profiles through `SceneBuilder.FadeCoasts`, called by `Ocean.Create`
after the bake, so the tiles and the waves place the same crossing. All of it runs only under Enhanced
with the ocean on and keeps the FlatSeaEye exemption; the Original text is byte-identical.

**Verified.** `dotnet build` 0 warnings; `dotnet test` 7968 passed, 0 failed, 6 skipped, with unit
tests for the ramp's dither, the strip's alpha fade, the scissored strip taking none, the sea tile's
Original text and the fade parameters from the profile. In-engine `graphics-coast-hide` (new, C1, C2
and C3) and `graphics-ocean-switch` pass, engine errors clean: C1 17 coast materials, 13/13 sea tiles
hiding, 4/4 overlay kept; C2 14/14 `beach1` faded, 6/6 `cliff01_trans2` kept; C3 14/14 cliff tiles
faded, 18/18 strips kept; every land coast matte; no ocean at flat water; under Original no coast reads
the ocean. Goldens: 36 held; `c1-lake-enhanced` (3b69049394b51460a410fe50a7351116) and
`c1-rocket-hit-enhanced` (9f238982e54cde130e8ff794b77e2f0a) moved, pixel-identical on re-render, not
re-pinned until D31. Montages at the six poses went to the author: C2's strip, beach and decal and C3's
strip draw as before C21 or better, C3's cliff tiles give way along their gradient, C1's overlay rock
lies over the waves. Owed to C22: the dither and the beach fade over frames (TAA, flicker), and the
calm band at C1 and C3's cliffs.

**Original approach (kept for reference).**

**Goal.** Under Enhanced at `waves`, the water side of every painted tile and strip steps aside for the
waves, and the land side keeps drawing.

**Evidence (confidence: traced).** The surf ring got the ocean hide in PLAN-shore-water B11; the
material selection that gives it is at `CSVM/src/Mech3/SceneBuilder.cs:2250-2251`, gated on
`IsOceanBaseTexture` and `IsSurfTexture`. #270: the hide discards only where the mask says sea. Every
painted-coast texture already goes through the same generator, `BiasShaderCode`, and only the
`oceanBase`/`oceanSurf` flags keep the hide off them (`SceneBuilder.cs:2355-2357`, `:2410`).

**Approach.** Add a painted-coast flag beside `oceanBase` and `oceanSurf`, set from B12's predicate,
with its own shader-key bit, and widen the hide gate to it without passing through `waterLit`. The
hide code itself (`SceneBuilder.cs:2475-2479`, `:2551`, `:2599-2600`) is reused unchanged.

**Model recommendation.** high: it edits the shared world shader generator, whose key bits and gates
reach every world material.

**Verify.** The Original shader text stays byte-identical (the Original goldens hash-identical, as in
PLAN-shore-water). At the B12 dump poses, an Enhanced `--water-quality=waves` screenshot shows the
waves through the tile's water side and the painted sand or cliff base intact on its land side. The
in-engine suite that holds the hide's constants (`CSVM/src/Testing/GraphicsSwitchSuites.cs:55`,
`HideCall`) covers the new materials.

**⚠ Traps.**
- The hide must follow the mask's sea channel texel by texel; a per-polygon hide discards the land
  half of an opaque tile.
- The hide samples only the mask's R channel at the fragment's world XZ, between y = -0.25 and 4.0
  (`CSVM/shaders/csky_ocean.gdshaderinc:48-62`). It never reads the shore distance or the surf plane.
- Do not gate the new hide on `waterLit` for the land textures: `waterLit` also selects the glossy
  water arm (`CSVM/src/Mech3/SceneBuilder.cs:2646`) and the water specular in the shadow light
  (`:2698`). The gate at `:2410` needs its own flag and shader-key bit, beside the base sheet's and the
  ring's (`:2369-2376`).
- `shore1`, `shore_trans` and `beach1` are alpha-blended (forced soft by `SoftAlphaCoastline`,
  `CSVM/src/Mech3/TextureArchive.cs:681-688`); C1's tiles and the cliff tiles are opaque. The hide runs
  before the albedo sample, so it applies to either.

## C22 ☐ The look at the painted coasts, judged at the controls

**Landed.** Decisions 10 to 16, from three rounds of montages. `shore2`'s flat triangles are open sea in
the mask (`TextureArchive.IsPaintedShallows`, `OceanMask.MarksSolid`), its 2 rising triangles solid,
and under Enhanced with the ocean on the decal fades out; C3's solid triangles fell from 4721 to 3688
and the calm band off its coasts is gone. The cliff tiles dither by `csky_clutter_dither_keep_frame`,
a threshold that shifts every frame on the shader clock, deterministic under `--det`. C3's sea takes
C1B's ramps (swell 8 to 48 m, look 0 to 24 m) and procedural surf (`surf_wash` 0.7, `surf_lines` 0.6);
C1's look ramp starts at 0. The soft dark patch the swell change showed was the baked terrain shadow
treating the flat sea sheet as ground, so deep troughs read as shaded: `TerrainShadow.Triangles` now
skips water and `shore2`. `CoastProfile.Opaque` (the crossing to the alpha's 90 % point: `shore1` 0.130,
`beach1` 0.080, `cliff01_trans2` 0.216) keeps the mask sea, with zero shore distance and saturated surf
coverage, under the alpha strips' translucent sand, so the ocean's and the foam's texel edges lie under
the strip's opaque part; the coastline and the coverage seaward of the waterline are unchanged.

**Verified.** `dotnet build` 0 warnings; `dotnet test` 7972 passed, 0 failed, 6 skipped, with unit tests
for `shore2` (flat is sea, rising solid), the translucent sand (sea, zero distance, full coverage,
order-free), the shallows' fade and the per-frame dither averaging to its alpha. `graphics-coast-hide`
(now with `shore2`) and `graphics-ocean-switch` pass, engine errors clean. Montages at every pose went
to the author, who approved: procedural foam along C3's strips and cliffs, the Enhanced-only dark rim
at the cliff tiles (6 levels below both sides, absent in Original) turned into a foam peak, C2's
sawtooth gone, C1's calm band and its diagonal edge gone, the C3 patch gone at frames 120 and 180.
Still camera over a breaker cycle: no lattice, no flicker; under a random `--jitter` burst the
per-frame pattern shows frame by frame, owed to the closing at-the-controls batch. Five Enhanced
goldens moved, each pixel-identical on re-render, attributed by toggling each change: `c1-lake-enhanced`
and `c1-rocket-hit-enhanced` to the terrain-shadow fix, C1's `look_from` 0 and the sand extension;
`c1-cockpit-enhanced`, `c1-cockpit-xr-sim-enhanced` (at most 1 level per pixel) and `c1b-ocean-enhanced`
to the terrain-shadow fix alone, the last losing about 145,000 pixels of self-shadow blotches on the
sea. Re-pinned in D31.

**Goal.** The author judges the waves meeting the painted foam in C1, C2 and C3.

**Evidence (confidence: lead-only).** #270: C3's entry in `CSVM/data/ocean_seas.json` already pulls
`swell_from` to 6 m and `look_from` to 8 m, which hides the gap's width at the controls.

**Approach.** Montages at each chapter's coast, then the author's verdict. C3's montage shows its
ramps as they are (`swell_from` 6, `look_from` 8) beside the ramps C1B's and C2's surf seas use
(`swell_from` 8, `look_from` 0); the author picks, and the value lands in
`CSVM/data/ocean_seas.json` with the verdict quoted in the commit (decision 4). C1's montage does the
same for its look ramp (`look_from` 55, `look_full` 143) against `look_from` 0 (decision 5).

**Model recommendation.** medium: montages and the author's verdict.

**Verify.** The author's verdict at the controls, after montages at each coast (verification.md
SHOT-49: a line called an Enhanced defect is thresholded against the same pixels in Original first).

**⚠ Traps.**
- Judge the gap itself in the mask dump, not from the current look.
- In C3 a band of texels reading sea with a zero shore distance sits just seaward of some coasts:
  about 30 m out at the cliff pose near x -5325, z -2660, and at a strip near x -11780, z -8452. They
  are solids outside every painted-coast triangle, so the shore distance rises from the waterline and
  falls back toward them, and the waves calm there. Name their source (flat land on the sheet, a
  rising solid) from the mask dump and the scene before judging the look.

## C23 ☐ Swash: each breaker runs up the painted sand and flat rocks

**Landed.** The raster's `Inland` pass writes an R8 texture of each texel's exact signed distance to
the nearest sea-facing coastline segment (one with open sea within 8 m, so painted land edges under a
beach draw no rings), capped at `InlandReach` 32 m: 128 on the line, 255 at 32 m landward, 0 at 32 m
seaward; land, solids and a strip's translucent sand read landward. It is a per-texel minimum per band,
order-free, uploaded as the global `csky_ocean_inland` and dumped as `<name>.inland.png`. The breakers'
phase, `csky_surf_arrive`, moved with `sea_hash` and `foam_noise` into `csky_ocean.gdshaderinc`; the
ocean calls it with its own literal rate, and the coast shaders read the rate, whether the sea breaks
surf, the reach and `InlandReach` from the global `csky_ocean_surf`, set by `Ocean` from
`OceanShader.SurfGlobal`. `csky_ocean_swash` darkens the albedo toward wet sand and lays a foam film
whose front climbs for 0.3 of a cycle from the line's arrival and drains for the rest, within ±0.5 m of
sea level, on every profiled coast (`coast_swash`), not on C1's sea tiles or `shore2`. Reach 12 m, the
author's pick (decision 17); C1 takes procedural surf so its overlay rock gets breakers and swash
(decision 18). Enhanced with the ocean on only, FlatSeaEye exempt, the Original text byte-identical.

**Verified.** `dotnet test` 7978 passed, 0 failed, 6 skipped, with tests for the signed inland
distance (a 30° and 45° strip within 0.5 m, the whole-image oracle at 1, 5 and 12 bands), the shared
phase (the include with the ocean's rate substituted equals the old line), the whole-per-wrap drift and
the coast shader text under Enhanced, Original and flat. `graphics-coast-hide` and
`graphics-ocean-switch` pass, engine errors clean, the suite reading the live inland image (16,284 /
36,657 / 106,761 texels within reach in C1 / C2 / C3). `c1b-ocean-enhanced` held C22's hash
36084ddde2162f8cef2fa6b57f6b1ee1: the moved phase leaves the ocean bit-identical. C1's surf moved
`c1-lake-enhanced` (a99b427fc62be3a4d9c687ff419e6538) and `c1-rocket-hit-enhanced`
(c6ebf47069d23d98c88eda492e1db4d0), each matching C22's frame with C1's surf removed. Contact sheets over
a breaker cycle at C2's beach, C3's strip and cliff, and C1's cove, and the reach strips, went to the
author, who approved. Bake, two runs: `inland=` C1 6.6/6.7, C2 9.1/7.5, C3 50.8/51.4 ms (C3's cost is
the per-texel loop over its 2048x2048 mask under build contention; one quiet run read 17 ms); bake C1
182/185, C2 179/173, C3 322/329 ms.

**Goal.** Under Enhanced at `waves`, each breaker that reaches a painted coast runs on up the painted
sand and flat painted rocks as a thin foam film over darkened, wet sand, then drains back before the
next, in time with the surf's breaking lines. The waves' geometry still ends at the coastline.

**Evidence (confidence: lead-only).**
- The breakers' timing is the `arrive` phase in the ocean fragment (`CSVM/src/Effects/OceanShader.cs:361-366`),
  a phase on the sim clock with a still noise bend, and the wash surges as each line reaches the land
  (`SurfSurge`, `SurfSurgeLag`, `:216-219`, `:377`). It lives in the generated ocean shader, not in
  the shared include.
- The coast textures' shader already reads the mask through `csky_ocean.gdshaderinc` once C21 gives
  them the hide; the include exposes the mask's sea channel by world XZ (`csky_ocean_sea`, `:18`).
- The mask encodes a shore distance only for open sea; land texels read 0
  (`CSVM/src/Effects/OceanMaskRaster.cs:454`), so nothing today says how far inland a land texel is.

**Approach.** Three parts.
1. The mask measures an inland distance from the coastline segments for land texels within a swash
   reach, in a channel or texture of its own, beside the shore distance B13 makes exact.
2. The breakers' phase moves from the generated ocean shader into the shared include, so the ocean and
   the coast textures read one function and the swash arrives with the line that made it.
3. In the coast textures' shader (C21's painted-coast flag), on the land side of the coastline, the
   swash darkens the albedo toward wet sand and lays a foam film whose reach rises and falls with the
   phase. It stays opaque.
<TODO: the swash reach and the wet tint come from the author's montage verdict, not a guess>.

**Model recommendation.** high: a new mask channel, a moved shader function both shaders share, and a
look the author judges.

**Verify.** The Original shader text stays byte-identical. At the B12 poses, a `--shots` sequence over
one breaker cycle shows the film running up and draining (verification.md INSTR-81: an effect living
over frames is never judged from a still; SHOT-12: frame the time-driven surface, then perturb time).
The ocean's own surf is unchanged by the move, its golden `c1b-ocean-enhanced` hash-identical. The
author's verdict at the controls.

**⚠ Traps.**
- Not a transparent ocean (decision 8).
- Moving the phase must leave the ocean shader's output bit-identical: `c1b-ocean-enhanced` holding is
  the check.
- Every rate stays whole per the 3600 s clock wrap, as PLAN-shore-water C23 required of the surf's.
- The swash must not draw over solids above the water, a pier or a hull; only painted-coast
  textures at sea level take it.

## C24 ☑ Spray where the breakers meet the cliffs

**Verified.** The author picked the sprites from contact sheets over one breaker cycle at the C1 cove, beside the shader sheet and spray off, and ruled C3 off and quays out from the C3 and C2 city-quay sheets (decisions 19 to 21). `graphics-coast-hide` holds C1 1,827 and C2 755 active cliff sites, none in C3, none under Original or at flat water; full `dotnet test` 7,982 passed, 0 failed; `RunTests.ps1 -Suite graphics-coast-hide,graphics-ocean-switch` green with engine errors clean. c1-lake-enhanced (to 2c0ac6a0) and c1-rocket-hit-enhanced (to 308e72c2) move by the C1 spray alone and return to their C23 hashes under `--coast-spray=off`; they wait for D31. The spray in flight goes into the closing at-the-controls batch.

**Landed.** The raster picks spray sites (`OceanMaskRaster.SpraySites`): points 12 m apart (TUNE) along
the foot, under `SolidTop`, of every solid face rising 6 m or more at over 45°. A cliff's foot stands
within 2 m of a painted coast with a water side and within 160 m (`ShoreReach`) of a sea-facing painted
waterline; a bare foot (a quay, rock off every painted coast) has open sea a texel out from its face.
Sorted, then thinned, so no triangle order moves a site. Cliff and bare sites: C1 1,827 and 31, C2 755
and 750, C3 201 and 769. C1's and C2's cliffs stand 8 to 24 m behind their waterline, C3's 50 to 130 m.
`Effects/CoastSpray`, a child of the ocean, fires each site when the surf's phase starts a cycle there
(`OceanShader.SurfArrive`, `csky_surf_arrive` in C#): the original's `splasher` sprites, retuned TUNE
(8 a batch, 0.3 s, 16 m/s, 1 to 2.4 s life), from a pool of 24 on their own `Rng.CoastSpray` stream
within 1,200 m of a pane (decision 19). `--coast-spray=sheet` draws a camera-facing shader sheet on the
same phase instead, `off` none. Cliffs only by default; `,quays` adds the bare sites (decision 21). The
sea's `spray` field turns it off by chapter, 0 in C3 (decision 20). Enhanced at waves only, while the sea
breaks surf; Original builds none. Cost, `gpu_ms` medians A/B after unchanged pairs: sprites +0.00 to
+0.09 ms, the sheet +0.00 to +0.02 ms at C1's cove, harbour and a pose inside the spray.

**Goal.** Under Enhanced at `waves`, where a painted coast ends at a cliff (C1's and C2's `cliff01`
rock faces, C3's `cliff1` at the cliff tiles' landward edge), each breaker that reaches the cliff
throws spray up the face and falls back, in time with the swash.

**Evidence (confidence: lead-only).**
- The cliffs rise from the painted coasts' landward edge (A2: 101 `cliff01` polygons in C1 to 3 to
  132 m, 31 in C2 to 64 to 111 m; A3: `cliff1` with a median 84 to 90 m at the cliff tiles).
- The original game ships water-splash ring and spray effects, played from crash sequences and leveled
  to world axes (`EffectCatalogue.CrashSurfaceLevelAnimNames`, applied at
  `CSVM/src/Session/World/WorldEffectsFactory.cs:883-887`), drawn by the puffer emitters
  (`PufferEmitterFactory`).
- B13's coastline segments locate every coast edge in world XZ, and C23 puts the breakers' phase in
  the shared include.

**Approach.** Pick the coastline segments whose solid rises steeply above the water, and drive spray
along them on the breakers' phase. Two candidate mechanisms, chosen in the item from a montage:
the original's own water-splash spray sprites, emitted along those segments and timed to the phase;
or a camera-facing spray sheet in a shader at the cliff foot. The author picked the sprites, no C3 and
no quays (decisions 19 to 21).

**Model recommendation.** high: a new effect placed from the bake's data, with a performance budget and
a look the author judges.

**Verify.** A `--shots` sequence at the C2 and C1 poses over one breaker cycle, judged by the author;
perf `gpu_ms` at the busiest cliff pose against the baseline (verification.md PERF-9: two unchanged
pairs for noise, then A/B back to back). Original mode draws no spray.

**⚠ Traps.**
- Spray placed per segment multiplies with C1's many short harbour edges; budget it before tuning it.
- The original's splash defs are authored for an impact, not a cycle; reuse their sprites, not their
  one-shot timing.

## C25 ☑ The new ocean replaces the old water look completely under Enhanced at waves

**Verified.** The author held C25 on the C1 coast colour and passed it once the straight-edged wedges were traced to the sheet tint under C1's sea tiles and gone in the before/after at the author's pose and a sea-tile pose (decision 22); the switch montage at the author's pose rules out sun shadow, the sea tiles drawing, the look band, TerrainShadow, `sheet` and SSR. The C2 inland swash fringe at the author's C2 pose is gone, and Original renders pixel-identical at the author's C1 pose. The C3 extension of the tint rule was weighed from the tint data alone and left out: C3's bright ring runs on under the alpha strips, which showed it in Original, and moving only the cliff tiles' corners cut straight notches. `dotnet test` 7,980 passed, 0 failed; `RunTests.ps1 -Suite graphics-coast-hide,graphics-ocean-switch` green with engine errors clean. Owed: a render of C3 with the tint forced white beside the current one, and the golden rerun naming each hash the tint fix moves, both blocked when the machine stopped creating GPU devices; they go to D31.

**Source.** The author, flying C1 under Enhanced at waves: "disable the repeating water texture,
because it does not look good", and then "can we replace it with the new ocean completely now that
we have waves that go up to the shore". On C2: "there is a second waterline behind the beach where
the vegetation is starting. Can we remove that."

**Goal.** Under Enhanced at `waves` the old water look goes: the ocean draws no tiled sample of the
base texture at any distance, its colour is the texture's mean times the sea's tint, and no water
look the ocean does not draw shows behind a painted beach. What old water still draws is listed per
chapter, with why the ocean cannot take it.

**Evidence (confidence: traced).**
- The ocean blended the base texture, tiled every `tile_m` (32 m in C1), wherever it shaded as the
  flat sheet: `col = mix(tex_mean, csky_sample_albedo(albedo_tex, p / tile_m).rgb, sheet) * tint`, with
  `sheet = max(1 - look, smoothstep(0, 0.25, fog_amt))`. PLAN-enhanced-ocean C25 added it so the ocean
  matched a coplanar coast tile at the shore and the sheet in the fog band; C22's `look_from` 0 ran
  that shore band to C1's cliffs.
- The coplanar tiles it matched are gone: no chapter's mask holds a `Kind.Edge` texel at the sea
  except two triangles at the foot of C3's raised-lake chute (`edge_tris` C1 0, C1B 0, C1C 0, C2 0,
  C2B 0, C3 2, C5 0). C1's sea tiles take the mask hide; the painted strips keep their own alpha over
  the waves.
- At the author's C1 pose (5120x1440) the texture drew a fine, low-contrast grain in the 143 m look
  band along the cliffs: forced to its mean, 13,300 pixels move by more than 4 levels, none by more
  than 16, all inside that band. The pale flecks across the open sea stay with the texture gone; by
  their shape they are whitecaps, which this item leaves alone (not toggled).
- C2's second waterline is the swash: with the sea's surf off (`--debug-ocean=surf_wash:0,surf_lines:0`)
  its fringe goes, and the grey band behind the dry sand stays, as it does under Original and Enhanced
  flat. That band is `beach1`'s own painted rocks fading out over the land. The fringe traced inland
  edges of the flat `terpat` land at y = 0: `OceanMaskRaster.SeaFacing` kept a whole segment when any
  point of it lay within a texel edge of open sea, so a 500 m land edge touching the harbour channel at
  one end carried the swash along its whole length, up to 110 m inland. The same rule removes a
  smaller inland swash at a C3 strip (948 pixels at the C3 pose).
- The large straight-edged dark shapes on C1's sea along the cliffs (a wedge in the left inlet, a
  dark area in the right bay) are the base sheet's baked vertex colour, which the ocean multiplies
  in as its tint. Switched one at a time at the author's pose, only the tint forced white removes
  them. Sun shadows off removes a different set, the cliffs' own shadow strips. TerrainShadow, SSR,
  the sea tiles (drawn magenta, they show only slivers at the cliff foot), `sheet` and the look
  factor (0 or 1) leave the shapes in place. Original shows the same wedges. The tint mask falls
  to about G 130 in the inlets, against 255 in the open sea: the sheet's corners there lie under C1's opaque sea tiles,
  and the colour interpolates from them across the sheet's large triangles into the open sea.
- The other chapters, by the sheet corners under each painted coast, against the corners under none
  (mean of R, G and B): C1B, C1C, C2B and C5 have no painted coast, so nothing changes. C2's corners lie
  only under `beach1` (120, all 255, against an open median of 128) and the `cliff01_trans2` decal (121,
  median 94); both are alpha strips that kept their soft alpha in Original, so those corners showed
  through, and neither is replaced. C3's opaque cliff tiles cover 728 corners (`cliff1_watertrans` 160,
  `cliff1_watertrans2` 568, median 255) against an open median of 185, and its alpha strips cover 422
  more, also at 255. The bright band is one authored ring along every C3 coast, visible through the
  strips in Original. Giving the cliff tiles' corners the mean moves 187,184 sea texels of the tint by
  more than 4 levels, and in the tint image cuts straight-edged notches where the cliff tiles meet the
  strips. That is the shape this rule exists to remove.
- A render confirms the current rule: at the author's C3 pose and the first A3 cliff pose,
  rendered current, tint forced white and with the cliff tiles' corners taking the mean,
  C3's sea shows no straight-edged tint shape today, while the
  extended rule darkens the near-shore sea over 17 to 18 % of each frame and draws a stepped,
  notched edge where the cliff tiles' corners meet the strips', seen through the strips' water side
  at the A3 pose (D31).

**Approach.**
1. The ocean's colour is `tex_mean * tint` everywhere. `sheet` keeps the normals' flattening, the
   foam's fade and the roughness: the sea's look ramp still fades the waves' look in from the shore,
   and the fog still calms the far sea. `tile_m` and the mip-bias include leave the shader.
2. `SeaFacing` keeps only the half-texel pieces of each segment with open sea within a texel edge, so
   the swash runs only where the sea meets the land.
3. `OceanMaskRaster.TintUnder` gives every base-sheet corner under a painted sea tile (its edge
   included) the sheet's mean tint before the raster, so the ocean over a sea-tile texel and the open
   sea beyond it take the open sea's tint (decision 22). Under Original and flat water no ocean or
   mask is built.

**Old water still drawn under Enhanced at waves.**

| Chapter | What | Why the ocean does not draw it |
|---|---|---|
| C1 | `river1`, `river2` (the inland river), `falls`, `falls1`, `falls_edge` (the waterfall) | Above sea level, and not classed as water; the ocean is one surface at y = 0 |
| C1B, C1C, C2B | none | |
| C2 | the harbour ramp's 3 base triangles to 1.03 m | Drawn: the ocean rises over them with the ramp lift |
| C3 | 31 base-texture triangles of the raised lake and its chute, -6 to 13.17 m, 2 of them `Kind.Edge` at its foot; `falls-128`; `cliff1_watertrans3`'s painted water wedge (decision 7) | The lake stands 13 m above the sea; a second ocean at its own height is a separate item |
| C5 | the 12 ramp triangles to 3.03 m under the steinmann ship | Drawn: the ramp lift |
| all | the flat sheet a spyglass disc sees (`SceneBuilder.FlatSeaEye`) | By design |

The mask's rect covers every base-sheet polygon (C1 to 12.3 km, C3 and C5 to 16.4 km), and the grid
reaches 45 km, so no base sheet lies past the ocean's reach.

**Verify.** `OceanShaderTests` pin the colour line and the single texture fetch; the default text's
hashes and fixture move with it. `OceanMaskRasterTests` hold an inland edge touching the sea at its
ends to no swash halfway along, the whole-image oracle follows the per-piece rule, and a sheet corner
under a sea tile (or on its edge) takes the mean while one off it keeps its own. Before | after
montages at the author's pose, six chapters and a far foggy view, and at C2's beach.

**⚠ Traps.**
- Do not drop `sheet`: it still drives the normals, the foam and the roughness at the shore and in the
  fog. Only the colour stops reading it.
- The inland image's segment test is per piece, not per segment; the shore distance's segments are
  unchanged.

## C26 ☑ C3's cliff tiles blend out by world distance from the waterline

**Verified.** The author picked the 25 m blend over the dither and over 12 m at the author's pose and the A3 cliff poses, and the 4 m swash carry from a before/after zoom where the foam had stopped on a straight line (decisions 23 and 24). Eight consecutive frames at the author's pose show no flicker: the mean frame-to-frame change in the fade region is 0.26 to 0.68 levels, against 0.69 to 0.84 under the dither. C1's cove and C2's beach render pixel-identical. `graphics-coast-hide` reads C3's ramps 14/14 blending under Enhanced and 14/14 opaque under Original; RunTests' unit run 7,979 passed, 0 failed; `RunTests.ps1 -Suite graphics-coast-hide,graphics-ocean-switch` green with engine errors clean. No golden is in C3 at waves, so none moves by this item. The fade in flight goes into the closing at-the-controls batch.

**Landed.** `cliff1_watertrans` and `cliff1_watertrans2` blend out by their world distance seaward of
the painted waterline, read from the inland image: alpha 1 on the land side, a smoothstep to 0 at
`SceneBuilder.CoastRampMetres` (25 m, TUNE, the whole crossing-to-water-edge stretch at
`cliff1_watertrans`'s median scale; 12 m rendered beside it for the author). Under Enhanced the ramp's
text writes that alpha, so the tile draws in the transparent pass with `depth_prepass_alpha` at
`CoastRampRenderPriority`, one below the ground layer; `FadeCoasts` sets `coast_reach` on each ramp
the mask profiled. The per-frame dither `csky_clutter_dither_keep_frame` and its test are removed;
`csky_clutter_dither_keep` stays for the clutter, ground cover, canopy and race ghost. `beach1` keeps
its UV fade; the same world-distance fade on it is rendered beside it at the A1 beach pose for the
author's call. The swash on the ramps no longer stops along the coastline: at the author's pose the
foam had ended on a straight line where the inland image crosses 0, the waterline segment of a
`cliff1_watertrans2` triangle (node `g28615`, model 782), with the ramp still near opaque past it, so
nothing met the foam. The ramps call `csky_ocean_swash_on`, which holds the film's value at the line
and fades it out over `SceneBuilder.CoastSwashCarry` (4 m, TUNE) seaward; a carry as long as the blend
filled the small sea pockets the mask reads inside the cliff tiles with foam. The strips keep
`csky_ocean_swash`, which ends at the line as before, and C1's cove and C2's beach render
pixel-identical. The swash still runs on the tiles' land side.
Under Original the tiles' text writes no alpha and is unchanged; at flat water the gate keeps alpha 1,
but the tile still draws in the transparent pass, so the frame is not pixel-identical to the opaque
tile: at the author's pose 0.8 % of the pixels, all on the tiles, move by more than 4 levels (at most
31), with no visible change.

**Source.** The author, flying C3 close to the cliff tiles (freecam at x -4652, y 52, z -3426): the
dithered fade is distracting, it reads as noise, and it jumps at polygon edges in straight lines. The
author chose to fade the cliff tiles the way C2's beach fades: blended by alpha, not dithered, and
measured by world-space distance from the painted waterline, not by UV, so the fade is continuous
across polygons.

**Goal.** Under Enhanced with the ocean on, `cliff1_watertrans` and `cliff1_watertrans2` are opaque on
the land side of the painted waterline and blend smoothly to nothing seaward of it, with no dither and
no step where two polygons meet.

**Evidence (confidence: traced).** The straight cuts come from decision 9's fade following each
polygon's own V: the cliff polygons stretch V from about 60 m to over 800 m per unit (A3), so
neighbours fade over different widths and meet at a step along their shared edge. C23's inland image
(`csky_ocean_inland`, 8 m texels, bilinear) already holds each texel's exact signed distance to the
nearest sea-facing coastline segment, which for the cliff tiles is their profile's crossing (B13).

**Approach.** The ramp's Enhanced text writes `ALPHA = smoothstep(-coast_reach, 0.0, d)`, d the inland
image's metres at the fragment, behind the same gate as before (`csky_ocean_on`, `coast_reach > 0`, the
FlatSeaEye exemption). Writing an alpha moves the tile into the transparent pass, so it takes
`depth_prepass_alpha` (its land side still writes depth and normals for the screen-space passes) and a
render priority one below the ground layer, so every strip and decal laid over it composites on top as
it did over the opaque tile. `coast_reach` (`SceneBuilder.CoastRampMetres`, TUNE) is set by
`FadeCoasts` on each profiled ramp. The per-frame dither, `csky_clutter_dither_keep_frame`, had no
other user and is removed. The Original text writes no alpha and is unchanged.

**Verify.** Unit tests on the ramp's text under Enhanced and Original, and the ramp read through the
inland bytes against the exact distance; `graphics-coast-hide` reads the cliff tiles blended under
Enhanced with waves and opaque under Original and at flat; montages at the author's pose, the A3
cliff poses and C2's beach, two widths, and frames over time at the author's pose.

**⚠ Traps.**
- The inland image saturates at `OceanMaskRaster.InlandReach` (32 m), so the fade cannot be wider.
- A blended surface that writes no depth would let a coplanar strip drawn before it vanish under it;
  the priority below the ground layer and the depth prepass keep the overlap order. At the author's
  pose, the top view and a `shore_trans` overlap the ramp at the ground layer's own priority rendered
  pixel-identical, so the lower priority guarantees the order rather than fixing a seen artefact.
- A fixed width in metres can outrun a narrow tile: where a polygon's water edge lies nearer the
  waterline than `CoastRampMetres` (A3's p10 widths put it near 14 m on `cliff1_watertrans` and 20 m
  on `cliff1_watertrans2`), the tile ends before its alpha reaches 0. No such edge showed at the
  poses checked; a wider width makes it likelier.

# Wave D, landing

## D31 ☑ Goldens re-pinned, the battery and perf green on the merged tree

**Verified.** The author saw the three new frames and the C3 tint montage, and ruled that `c2-city` stays pinned at c530a830 with its pacing defect filed as #282, not re-pinned. The complete battery with `-Perf` on the tree merged with main is green but for `c2-city`, and a goldens-only rerun on the re-pinned manifest holds 40 of 41. Perf holds: no scene stays above its baseline on an unchanged rerun (PERF-7), and the perf scenes run under Original, which this plan leaves byte-identical.

**Landed.** Three Enhanced `--water-quality=waves` goldens pin the painted coasts at frame 120:
`c1-coast-enhanced` (`water1_trans1`'s pose in C1's waterfall cove: the sea tiles stepping aside,
breakers and swash on the rock overlay at the cliff foot, the spray puffs), `c2-beach-enhanced`
(`beach1`'s first A1 pose: the strip's alpha fade over the waves, the surf and the swash on the sand)
and `c3-coast-enhanced` (the author's C3 pose: a strip and a cliff tile, the 25 m blend and the
swash). Each can fail (GOLD-18, METHOD-9): `--coast-spray=off` moves 2.22 % of the C1 frame by more
than 4 levels, surf off (`--debug-ocean=surf_wash:0,surf_lines:0`) moves 5.69, 29.05 and 8.84 % of
the C1, C2 and C3 frames, and `CoastRampMetres` at 12 m moves 13.22 % of the C3 frame. Each rendered
one hash in three launches, one of them beside the battery's engine stage. Frame 120 against 121
differs on 22.45, 19.51 and 24.26 % of the pixels.

Five Enhanced goldens are re-pinned, each pixel-identical on the battery's re-render:

| Golden | Old | New | Cause |
|---|---|---|---|
| `c1-lake-enhanced` | 9bff994b | 16ed21c4 | C22's terrain-shadow fix, look ramp and sand extension, C23's C1 surf, C24's spray (off: 1.16 % of the frame), C25's colour and `TintUnder` (off: 14.07 %, at most 26 levels, the cove's sea) |
| `c1-rocket-hit-enhanced` | 1e12fb46 | f7e36dca | The same chain; spray and the tint each move it by at most 3 and 1 levels |
| `c1b-ocean-enhanced` | f9b336f4 | db71ea00 | C22's terrain-shadow fix and C25's colour, C25's recorded hash; the tint leaves it |
| `c1-cockpit-enhanced` | df5b6ee7 | 3e6f399b | C22's terrain-shadow fix and the later sea changes (C23's surf, C25's colour), not split further; spray and tint leave it |
| `c1-cockpit-xr-sim-enhanced` | 6279deb5 | dc28876c | As `c1-cockpit-enhanced` |

The two cockpit pins are main's #272 re-pins, so C22's recorded 926b90ad and 16a31671, measured
before #272 merged in, no longer apply. Main's polygon patches move none of these: with C1's entries
emptied the four C1 shots render the same hashes, and no Original golden moved. `c2-city` moved
(c530a830 to 4c212470, 116 pixels: a sailboat and a car absent) and is not re-pinned: its frame
depends on wall-clock pacing. `GameSession`'s unplaced-entity recheck runs on wall time once a
second, and on this machine the 120 sim frames now finish before it first fires, so the 17 route
vehicles and boats stay switched off; the same render at 095789b2d gives 4c212470, and Godot's
`--frame-delay 20` brings back the restore and c530a830.

The complete battery with `-Perf`: units 8,144 passed, 0 failed, 6 skipped; engine 641 passed, 0
failed, 2 skipped, engine errors clean; goldens 41, 35 held and 6 moved, every Original golden but
`c2-city` held; perf 8/8. A goldens-only rerun on the re-pinned manifest holds 40 of 41, `c2-city`
the one move. Hitch is opt-in and ran beside the perf rerun: its inject check saw no hitch
line, an awareness item that does not gate. `gpu_ms` medians (Original), first run and an unchanged
rerun, against the starting tree's baseline:

| Scene | Baseline | Run | Rerun |
|---|---|---|---|
| empty-stage | 0.13 | 0.1 | 0.1 |
| c1-flight | 0.725 | 0.2 | 0.215 |
| c2b-water | 0.155 | 0.1 | 0.09 |
| c4-terrain | 1.105 | 0.25 | 0.26 |
| c2m02-hollywood | 0.94 | 0.325 | 0.295 |
| c5-city | 1.7 | 1.675 | 1.67 |
| c1-cockpit-xr-sim | 3.075 | 3.15 | 3.07 |
| c5-city-xr-sim | 6.62 | 6.63 | 6.615 |

Two scenes read over the baseline on the first run, `c1-cockpit-xr-sim` by 0.075 ms and
`c5-city-xr-sim` by 0.01 ms, and neither reproduces on the rerun (PERF-7). The perf scenes run under
Original, which this plan leaves byte-identical.

**Goal.** Three new Enhanced `--water-quality=waves` goldens pin the finished painted coasts, one per
chapter (decision 3); the Enhanced goldens that move are re-pinned; the complete battery and perf are
green on the tree merged with main.

**Evidence (confidence: traced).** No Enhanced golden covers a painted coast today
(`analysis/goldens/manifest.json`). `c1b-ocean-enhanced` (`:275`) is the only `--water-quality=waves`
shot and frames C1B's surf ring. `c1-lake-enhanced` (`:149`) and `c1-rocket-hit-enhanced` (`:170`)
catch C1's coast tiles only in passing, at the default water quality. C2 and C3 have no Enhanced
golden. The Original coast shots are `c3-island` (`:44`), `c1b-night-sea` (`:23`), `c2-city` and
`c1-waterfall` (`:16`).

**Approach.** Add three shots in `analysis/goldens/manifest.json` at the B12 dump poses: a C1 coast
tile, C2's `beach1` and a C3 `shore1` strip, each `--det --graphics=enhanced --water-quality=waves`,
with its `exercises` field stating what it shows. Pin them after C22's, C23's and C24's verdicts;
the swash and spray move with time, so each shot's frame count is part of its pin (GOLD-4). Then
`.\scripts\RunTests.ps1` with `-Perf` on the merged tree; re-pin only the existing goldens whose move
is the intended change.

**Model recommendation.** medium: the battery and a re-pin on known rules.

**Verify.** Every Original golden hash-identical, the moved Enhanced goldens named in the commit with
what each now shows (verification.md GOLD-1, GOLD-3, GOLD-5), perf gpu_ms at or under the baseline.
The baseline is a `-Perf` run of the complete battery on the plan's starting tree, taken before B12
lands.

**⚠ Traps.**
- A golden that flips under load is a determinism defect, not a re-pin.
- Frame each new shot so the coast owns enough of the frame to move the hash (verification.md
  SHOT-29), and prove it can fail by perturbing the hide (GOLD-18, METHOD-9).
