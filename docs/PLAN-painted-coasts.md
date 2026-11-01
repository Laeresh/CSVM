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

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | B12, B13, C21, D31 | The mask's gaps, the hide's gate and the golden coverage are read from the code and manifest; confirm the line numbers, then implement. |
| **Direction sound, magnitude a judgement call** | B11 | The crossings are measured (Wave A); the rule that finds them in the pixels must reproduce those figures. |
| **Leads only, no mechanism yet** | C22 | The author's look call. |

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
| C3 | `shore2` | the beach slope itself, rising to 7.6 m | RGBA sand and foam | land, correctly |

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

11. ☐ A shore U per coast texture, read from the texture's own profile at bake time (#270)
12. ☐ A painted-coast kind in the mask: sea on the water side of the iso-line with surf coverage, shore on the land side (#270)
13. ☐ The iso-line is a coastline segment for the exact shore distance (#270)

### Wave C, the coast steps aside

21. ☐ The painted tiles and strips carry the ocean hide, discarding only where the mask says sea (#270)
22. ☐ The look at the painted coasts in C1, C2 and C3, judged at the controls (#270)

### Wave D, landing

31. ☐ Goldens re-pinned where the Enhanced look moves, the battery and perf green on the merged tree (#270)

## Dependency and parallelism notes

A1, A2 and A3 are read-only measurements and can run in parallel. B11 needs A2 for the opaque-tile
case and A1 for the strips. B12 needs B11 and A3 (which polygons are candidates). B13 needs B12's
per-triangle iso-line. C21 needs B12, since the hide reads the mask's sea channel under the tile. C22
needs B13 and C21. D31 is last. File contention: B12 and B13 both edit `CSVM/src/Effects/OceanMask.cs`
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

**⚠ Traps.** Judge the gap itself in the mask dump, not from the current look.

# Wave D, landing

## D31 ☐ Goldens re-pinned, the battery and perf green on the merged tree

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
with its `exercises` field stating what it shows. Pin them after C22's verdict. Then
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
