# Shore water, the wave ocean up to the coast

**ACTIVE PLAN** (written 2026-10-31). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The Enhanced wave ocean (`src/Effects/Ocean.cs`) stops short of every coast. Its shore mask counts
the authored surf ring (the `srf0001`..`srf0016` flipbook strip in C1B, the one static `srf0001`
strip in C2) as shore, so the swell and the look fade to the flat sheet from the ring's seaward
edge, and the ring is drawn over the flat water as the original draws it. In C1B that is a visible
switch from waves to a flat textured sheet with a white band. This plan makes the ocean own the
coast in the two surf-ring chapters: the ring's water counts as sea, the shore distance becomes the
exact distance to the land edge, the ring steps aside while the ocean is live (as the base sheet
already does), and the ocean shader draws the surf itself from the authored ring's extent and the
shore distance, animated on the sim clock. The swell stays at zero inside the surf zone, because
the islands have no geometry below the waterline and a trough at the coast would open a hole.

Out of scope: C1's `water1_trans1/2` coast tiles. They are opaque 256x64 gradient textures that
paint the land inside the tile, so the land's edge is a texel boundary and not a polygon edge;
replacing them, and C3's `shore*` strips and `cliff1_watertrans*` tiles with them, is a different
mechanism, filed as #270 for a later plan. A skirt below the coastline (geometry that
would let waves lap the slope) is also out; decision 2 below says why. No `backlog` issue is drawn
into this plan, so no re-verification is owed; #204 (the wake's Kelvin V) is adjacent and untouched.

## Milestone goal

- Under Enhanced graphics at the `waves` water quality, C1B's and C2's waves (texture, normals,
  whitecaps) run to the land edge; the authored surf ring is not drawn while the ocean is live.
- A procedural surf band along every ringed coast: foam dense at the land edge, breaking lines
  travelling shoreward on `csky_time`, its extent the authored ring's, its strength and pace per
  chapter in `CSVM/data/ocean_seas.json` with sliders in the ocean lab.
- No hole and no crest lifting through the land at the coast, and no 8 m staircase along the
  shore: the swell is zero inside the surf zone and the mask's shore distance is exact.
- Original graphics is byte-identical in every shader and pixel; chapters without a surf ring keep
  their look, within the shore-distance refinement the goldens re-pin.

**This plan touches no opaque coast tile.** C1's painted coast is its own problem, and the
Faithful (Original) path is never the place for any of this.

## Decisions (2026-10-31)

Settled in the research session that produced this plan; the author accepted the recommended
approach by asking for the plan. Reopen a row before Wave A if it is wrong.

| # | Question | Decision |
|---|---|---|
| 1 | Replace C1's `water1_trans` gradient tiles too? | **No, out of scope.** The land is painted inside the opaque texture; the mask cannot see where it starts. Second plan. |
| 2 | Let the waves lap the shore, or hold them flat there? | **Hold the swell at zero inside the surf zone.** No land geometry lies below y = 0 at the coast (the only thing under the ring's seaward edge is a floor at -11.7 km), so a trough would open a hole. A skirt extruded from the coastline polyline is a possible later item, not this plan. |
| 3 | Draw the surf as a shader on the ring's own polygons, or in the ocean shader with the ring hidden? | **In the ocean shader, ring hidden.** A coplanar overlay cannot follow displaced water, and one owner per texel is the rule the base-sheet hide already follows. |
| 4 | Shore distance from the 8 m chamfer, or exact? | **Exact distance to the coastline polyline** (the sea-level edges of the land triangles), chamfer kept only for solids with no sea-level edge (ships, piers). A foam band drawn from the chamfer would show the texel staircase along the shore, as #250's sliver did. |
| 5 | Which non-base water becomes sea in the mask? | **Only the surf ring (`srf*` at sea level).** Opaque sea-level water (C1's tiles) stays shore, so C1 does not change. |
| 6 | Surf width from a tune value or from the data? | **From the authored ring.** The ring's U runs from 0 at the land edge to 1 at its seaward edge (the texture's alpha gradient), so `1 - u` rasterised into the mask is the authored surf coverage, 13 to 92 m wide in C1B and 6 to 21 m in C2. Strength and pace are the tune. |

## What the data actually ships

Measured by `analysis/shore-water/` over `extracted/C1B/gamez.zip` and `extracted/C2/gamez.zip`
(unzbd output, the model diffs' oracle), and read off the engine's mask bake.

| | C1B | C2 |
|---|---|---|
| Surf ring polygons (material `srf0001`) | 375 (373 triangles, 2 quads), priority 1 over the sheet's 0 | 86 |
| Ring y | 0.000 everywhere | 0.000 |
| Ring width, land edge to seaward edge | min 12.7, p10 31.5, median 43.8, p90 66.3, max 91.5 m | min 5.9, median 10.0, max 20.8 m |
| Ring inner edge (u = 0) to the nearest land vertex at sea level | 0.0 m median, 0.0 m at the 90th percentile | same construction |
| Ring UVs | u 0..1 across the band, v 0..3.9 along the coast | same |
| Ring vertex colours | white (255) and grey (97) vertices on both sides of the band, so the authors dimmed parts of the ring, not one edge | same two values |
| Ring texture | `srf0001`..`srf0016`, 32x64 RGBA, alpha a gradient along U (opaque at u = 0, clear at u = 1), 9 fps material cycle | one static `srf0001` |
| Sea sheet | `wtr00000`..`wtr00015`, 64x64 RGB, 695 polygons of 512 m at y = 0, 10 fps cycle | `wtr*` likewise |
| Geometry below y = 0 under the ring's seaward edge | none but node `g1165` at -11,680 m (the world floor) | not checked |
| Mask bake (`[world] ocean: built grid`, `--freecam --det`) | 1536x1536 texels of 8 m from (-12288, -12288), base_tris 1360, edge_tris 377 (surf_tris 377 since A1), solid_tris 723, bake 90 ms (walk 15, raster 36, distance 15, upload 6) | not read |

Engine facts the items lean on:

- `OceanMask.Read` (`CSVM/src/Effects/OceanMask.cs:469-556`) marks the ring `Kind.Edge` (line
  535-541: water that is not base, at sea level). `OceanMaskRaster.Raster` writes an Edge texel as
  `Sea = 2` (`OceanMaskRaster.cs:315-318`), so the ocean draws under the ring (R = 255,
  `Encode` line 407), but `ShoreDistance` seeds only `Sea == 1` texels (line 426), so the shore
  distance G is 0 under the ring and the ring's seaward edge is where the ramps start.
- The ocean shader fades the swell with `shore_swell(G)` over `SwellFrom..SwellFull` (24..160 m)
  and the look with `shore_look(G)` over `LookFrom..LookFull` (12..64 m)
  (`OceanShader.cs:379-386`, vertex use 702-706, fragment 756 and 814; `SeaState.cs:104-117`).
  At `sheet = 1` the fragment is the flat sheet's own texture (`OceanShader.cs:811-823`), so the
  current coast is textured flat water under the ring.
- The base sheet steps aside through `csky_ocean_hides_sea` (`CSVM/shaders/csky_ocean.gdshaderinc:47-61`),
  compiled into the sheet's shader when `oceanHide = waterLit && oceanBase`
  (`SceneBuilder.cs:2400`, include at 2467, discard at 2590). `waterLit` exists only in the
  Enhanced text, so Original's shader text cannot move. The ring's material is not `oceanBase`
  (`SceneBuilder.IsOceanBaseTexture`, line 895-902), so it is drawn today.
- Zone bytes: an Edge texel takes `AnyZone` (every grid draws there); a land texel takes the group
  of the sea beside it (`OceanMaskRaster.cs:236-246`, the #250 fix).
- `csky_time` wraps at 3600 s (`csky_time.gdshaderinc:10-13`); `OceanShader` rounds every swell
  omega, detail drift and patch drift to whole cycles per wrap.

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

### Wave A, the mask

1. ☑ A surf kind in the mask: the ring's water is sea with an authored surf coverage, opaque coast tiles stay shore
2. ☑ Exact shore distance to the coastline polyline, chamfer only for solids without a sea-level edge
3. ☐ The surf coverage reaches the ocean material and the mask dump

### Wave B, the ring steps aside

11. ☑ The surf ring's shader carries the ocean hide, as the base sheet does

### Wave C, the shader

21. ☐ The ramps measure from the land: swell zero inside the surf zone, look to the coast, per-chapter tune
22. ☐ Procedural surf in the ocean fragment: dense wash at the land edge, breaking lines travelling shoreward

### Wave D, landing

31. ☐ Goldens re-pinned, the look judged at the controls in C1B and C2, perf and the full battery

## Dependency and parallelism notes

A1 → A2 → A3 is a chain in `OceanMaskRaster.cs` and `OceanMask.cs`; never run two of them in
parallel. A1 also adds the surf-texture predicate beside `IsOceanBaseTexture` in `SceneBuilder.cs`,
which B11 reads, so B11 starts after A1 and then runs in parallel with A2 and A3 (B11 owns
`SceneBuilder.cs` and `GraphicsSwitchSuites.cs`; A2 and A3 own `src/Effects/`). C21 and C22 need
A3 and B11 landed (the look cannot be judged with the ring still drawn) and both edit
`OceanShader.cs` and `SeaState.cs`, so they run in order. D31 is last.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

---

# Wave A, the mask

## A1 ☑ A surf kind in the mask: the ring's water is sea with an authored surf coverage, opaque coast tiles stay shore

**Landed.** `SceneBuilder.IsSurfTexture(string)` (`CSVM/src/Mech3/SceneBuilder.cs`, beside
`IsOceanBaseTexture`) matches the `srf` prefix case-blind, with or without an extension, as
`IsOceanBaseTexture` matches `wtr`; only `srf0001`..`srf0016` exist in the extracted data, all
surf. `OceanMaskRaster.Kind.Surf` (`CSVM/src/Effects/OceanMaskRaster.cs`): `OceanMask.Read`
(`CSVM/src/Effects/OceanMask.cs`) marks a sea-level water triangle with a surf texture `Surf`
ahead of the `Edge` branch and carries `1 - u` per corner in the new `Tri.Surf` (a `Vector3`,
corners A, B, C). The raster gives a Surf texel `Sea = 1` (set only where no water marked it, so an
`Edge` texel stays shore whatever the triangle order), `Zone = AnyZone`, no tint, and the
barycentric coverage through `Channel` into `Grid.Surf`, the highest over overlapping triangles;
`OceanMaskRaster.Result.Surf` returns that plane (R8, `Width * Height`, 0 off the ring). Nothing
uploads it yet (A3). `OceanMask.SurfTriangles` counts the ring apart from `EdgeTriangles`, in
`DropMovingHulls` too, and the `[world] ocean: built grid` line prints `surf_tris=`
(`CSVM/src/Effects/Ocean.cs`). Tests: `OceanBaseTextureTests` pins `srf0001.tif`, `SRF0016.TIF`
and `srf0001` surf and `water1_trans1/2`, `wtr00003`, `water1`, `wakefront1`, `watersquirt` not;
`OceanMaskRasterTests.TheSurfRingIsSeaMeasuredFromTheLand` puts a 64 m ring between a solid and
the sheet and holds G to the distance from the land texel, the surf byte to `1 - x / 64`, and the
zone to `AnyZone`; `EveryBandCountMatchesTheWholeImagePass` adds 200 surf pieces to its scene and
compares the surf plane at every band count. No existing test changed meaning. In C1B the bake now
logs `edge_tris=0 surf_tris=377` (377 `edge_tris` before), in C1 `edge_tris=646 surf_tris=0`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A texel under the surf ring is open sea to the shore-distance pass and carries the
authored surf coverage (`1 - u`, 1 at the land edge, 0 at the ring's seaward edge). A texel under
C1's opaque `water1_trans` tiles is shore, as today.

**Evidence (confidence: traced).** `OceanMask.Read` classifies non-base water at sea level as
`Kind.Edge` (`CSVM/src/Effects/OceanMask.cs:535-541`); the raster writes it as `Sea = 2` with
`Zone = AnyZone` (`OceanMaskRaster.cs:315-318`) and the distance pass seeds only `Sea == 1`
(`OceanMaskRaster.cs:426`), which is why G is 0 under the ring. The ring's inner edge sits on the
land edge (0.0 m median, `analysis/shore-water/FINDINGS.md`), and its U runs 0 at the land edge to 1
at the seaward edge, the direction of the texture's alpha gradient. `Read` already carries UVs for
base triangles (line 480-481) and the raster interpolates barycentric weights per texel for the tint
(line 310-313), the same machinery the coverage needs.

**Approach.** Add `SceneBuilder.IsSurfTexture(string)` beside `IsOceanBaseTexture`
(`SceneBuilder.cs:895`), matching the `srf` stem exactly as that method matches `water1`, with a
test in `CSVM.Tests/OceanBaseTextureTests.cs`'s style that pins `srf0001.tif` surf and
`water1_trans1.tif`, `wakefront1`, `watersquirt` not. In `OceanMask.Read`, a sea-level water triangle
whose texture is surf becomes a new `Kind.Surf`; `Kind.Edge` keeps the opaque tiles. In
`OceanMaskRaster.Raster`, a Surf texel sets `Sea = 1` (so the distance pass treats it as open), keeps
`Zone = AnyZone`, and writes the interpolated `1 - u` into a new surf byte array (max over
overlapping triangles); `Encode` emits it as an R8 plane beside the mask. Count Surf triangles
separately from `EdgeTriangles` in the build log line (`Ocean.cs:168`) and in `DropMovingHulls`'s
bookkeeping (`OceanMask.cs:606-621`). Tint: a Surf texel is not tinted (the sheet under it is, and
that texel keeps the sheet's tint through the existing off-sheet mean, line 118 of
`OceanMaskRaster.cs`'s entry). Unit test in `OceanMaskRasterTests.cs`: a surf triangle between a
land triangle and a base triangle gives a sea texel whose distance is measured to the land and whose
surf byte follows u.

**Model recommendation.** medium. Mechanical once the kind is named; the two predicates and the
byte plumbing are the whole item.

**Verify.** `dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build --filter "FullyQualifiedName~OceanMaskRaster|FullyQualifiedName~OceanBaseTexture"`,
then `.\scripts\RunProbe.ps1` with `--freecam --chapter=C1B --graphics=enhanced --water-quality=waves --det --dump-ocean-mask=.scratch/shore-water/c1b-mask.png --screenshot=...`
and read the dump: the ring's texels now carry a non-zero G rising from the land, and the surf plane
shows the ring; the C1 dump (`--chapter=C1`) is unchanged against a baseline taken before the change
(`docs/verification.md`: an unchanged image is evidence only once the instrument has been seen to
change, so dump C1B first).

**⚠ Traps.** `water1_trans1/2` must stay shore: `OceanBaseTextureTests` already pins them not base;
the new test pins them not surf. `Kind.Edge` texels set `AnyZone` so every grid draws there (the
#250 fix at `OceanMaskRaster.cs:236-246` reads `SeamZone` and `AnyZone`); keep that for Surf, or
C5-style zoned seas cut the band. The ring's vertex colours are not alpha and do not encode the
band's fade (white and grey appear on both edges); the texture alpha along U does.

## A2 ☑ Exact shore distance to the coastline polyline, chamfer only for solids without a sea-level edge

**Landed.** All in `CSVM/src/Effects/OceanMaskRaster.cs`; `OceanMask.Read` is unchanged.
`Coastline(Tri[])` collects the coastline from the triangles `Run` already receives: every edge of
a `Kind.Solid` triangle whose two corners lie within `CoastBand` (0.25 m) of y = 0, as XZ segments.
In each band, after its chamfer, `Exact` writes every segment's distance (`Segment`, now internal
for the tests' oracle) into each open-sea texel of the band's rows within `ShoreReach` of the
segment's box, keeping the nearer of that and the chamfer, so a hull beside the coast keeps its
calm. The segments are not bucketed: a bucket ring search writes the same bytes at about four times
the splat's `distance=` in the Debug probe, its work piling into the few bands that hold the
islands. The chamfer seeds (`Grid.Seed`, replacing the plain solid test) are the coast tiles
(`Sea = 2`), the texels nothing covers, every texel centre a solid covers, and the corner texels of
a hull, a solid with no corner within 0.25 m of y = 0 (`IsCoast`). A coast solid's corner texel is
solid (G = 0) but does not seed, since its centre can lie in the sea; that corner is what made the
staircase. A land centre behind a coastline is never nearer than the coastline, so a coast with
sea-level edges reads the exact distance, and a land edge standing off sea level (C1B's strip near
(-7730, -5745) has a north edge with no corner on y = 0) keeps the chamfer instead of reading the
distance across the land. Tests in `CSVM.Tests/OceanMaskRasterTests.cs`:
`TheShoreChannelIsTheDistanceFromASolidCorner` became `AHullTakesTheTexelDistance` (its sliver now
crosses y = 0 with no corner on it, same bytes); `ADiagonalCoastReadsTheExactDistance` (30 and 45
degree coasts off the lattice: every sea texel within half a metre of the line distance, and at
45 degrees each texel equal to its diagonal neighbour); `BesideACoastTheNearerShoreWins`;
`ALandEdgeOffSeaLevelTakesTheTexelDistance`; `EveryBandCountMatchesTheWholeImagePass` adds 400
hulls and one-corner solids to its scene, and its oracle splats every segment over the whole image
beside the single-pass chamfer. `TheSurfRingIsSeaMeasuredFromTheLand` now expects G = x (the
distance to the land's sea-level edge at x = 0) where it expected x + 4 (the land texel's centre).
C1B: R and the tint byte-identical, G moves at 31,862 sea texels (28,554 down, median change
3.8 m), a straight 20 degree shore 12 m out reads 12.07 m with sd 0.12 m where the chamfer read
13.6 to 17.4 m; C1: R and tint identical, G moves at 1,035 texels, all within x -6632..-6208,
z -3422..-3094, by at most 8.2 m. `bake=100 (distance=24.1)` in C1B,
`bake=102 (distance=20.9)` in C1.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The mask's G channel is the Euclidean distance from the texel centre to the nearest
coastline segment, so a foam band or a ramp drawn from it follows a straight or curved shore
without an 8 m staircase.

**Evidence (confidence: traced).** `ShoreDistance` is a two-pass chamfer over texel centres
(`OceanMaskRaster.cs:417-461`) with `Corner` marking a texel solid when any triangle corner lands
in it (line 389-393), so the distance to a diagonal shore is off by up to half a texel diagonal
(5.7 m) and steps per texel; #250's shoreline sliver was this staircase made visible. The land
edge is a polyline: 13,406 land vertices in C1B sit within 0.25 m of y = 0, and the ring's inner
edge coincides with them (`analysis/shore-water/FINDINGS.md`). Linear filtering of a true distance
field reproduces a straight edge exactly and a curve to within texel²/(8R).

**Approach.** In `OceanMask.Read`, collect the edges of `Kind.Solid` triangles whose both endpoints
lie within ±0.25 m of y = 0 as coastline segments (world XZ). In `OceanMaskRaster`, bucket the
segments on the 8 m grid and, for every sea texel the chamfer puts within `ShoreReach + Cell`,
replace its distance with the exact minimum over segments within that radius (search rings
outward until the ring's inner radius exceeds the best distance). A texel whose nearest shore has
no segment within `ShoreReach` (a ship's hull, a pier post) keeps the chamfer value. Keep the row
bands and the halo (`Halo`, line 34-36): the exact pass is per texel and band-independent by
construction. Interior edges of land lying flat at y = 0 (a quay) add segments inside the land,
which no sea texel is nearest to, so they are harmless. Tests: `TheShoreChannelIsTheDistanceFromASolidCorner`
(`OceanMaskRasterTests.cs:74`) changes meaning; replace it with a diagonal coast whose every sea
texel is within 0.5 m of the analytic line distance, keep `EveryBandCountMatchesTheWholeImagePass`
green, and add a hull case that still takes the chamfer.

**Model recommendation.** high. The geometry has to be right at corners, seams and band edges, and
every Enhanced sea shot reads it.

**Verify.** The unit tests above; then the C1B and C1 mask dumps against the A1 baselines, G now
smooth along the diagonal shores (a line profile across the dump, not a hash). The C1B freecam bake
line reads `bake=90 (... distance=14.9 ...)` today; with the exact pass the bake stays under about
300 ms and the `distance=` figure under about 150 ms on the user's rig.

**⚠ Traps.** The chamfer's distance is a texel-centre distance and the exact one is an edge
distance, so they do not agree at the texel right beside the coast; do not mix them per texel, pick
one per texel by whether a segment is within reach. Ships that are not movers cross y = 0 with
faces, not edges, so they produce no segments; the fallback covers them. The pass runs on the
thread pool in bands (`OceanMaskRaster.cs:395-415`); a per-band segment bucket must see segments
within `ShoreReach` of the band, not only inside it. The quantisation stays 160 m / 255 = 0.63 m,
enough for a band that starts 4 m out.

## A3 ☐ The surf coverage reaches the ocean material and the mask dump

**Goal.** The ocean's shader can read the authored surf coverage at a world XZ, and
`--dump-ocean-mask=` writes it beside the mask and the tint.

**Evidence (confidence: traced).** `OceanMask.BakeNew` uploads the mask (RG8), the tint (RGB8), the
zone (R8) and the lift (R8) as textures (`OceanMask.cs:290-300`) and `Ocean.Create` binds them as
`mask_tex`, `tint_tex`, `zone_tex`, `lift_tex` with one `mask_rect` (`Ocean.cs:134-142`); the dump
writes mask and tint (`Ocean.cs:190-199`). The include reads only the mask's R through `texelFetch`
(`csky_ocean.gdshaderinc:17-28`), so the mask's format is pinned by the hide and the suites.

**Approach.** A separate R8 `surf_tex` (one zero texel when the world has no surf triangles, as
`LiftTexture` does), bound beside `lift_tex`, sampled with the same `mask_rect` and linear filter,
written by the dump as `<name>.surf.png`. Leave the RG8 mask and the include untouched. A
`SurfAt(Vector3)` reader filtered as the shader samples, for the suites, in `LiftAt`'s shape
(`OceanMask.cs:226-235`).

**Model recommendation.** medium, low effort. Plumbing in the shape the lift already has.

**Verify.** `.\scripts\RunTests.ps1 -Suite graphics-ocean-switch -SkipUnits -SkipGoldens` still
green (it holds the include's literals and the hide call); the C1B dump's `.surf.png` shows the
ring at its authored widths; a chapter without a ring (`--chapter=C3`) logs a 1x1 surf texture.

**⚠ Traps.** `--dump-ocean-mask` keeps the images alive after upload (`callerKeeps: true`,
`OceanMask.cs:292-294`); the surf image needs the same or the dump reads freed memory. The suites
read `OceanMask.Live` because the server's global getter errors outside the editor
(`OceanMask.cs:59-61`); a new reader goes on the instance, not on a global.

# Wave B, the ring steps aside

## B11 ☑ The surf ring's shader carries the ocean hide, as the base sheet does

**Landed.** `SceneBuilder` (`CSVM/src/Mech3/SceneBuilder.cs`) passes an `oceanSurf` flag
(`water && IsSurfTexture(texName)`) from the textured-material path through `BiasMaterial`,
`GetBiasShader` (key bit 33554432) and `BiasShaderCode`, where
`oceanHide = waterLit && (oceanBase || oceanSurf)`, so the ring's blended shader takes the base
sheet's `csky_ocean.gdshaderinc` include, `v_ocean_pos` varying and `!FlatSeaEye`-guarded discard
with no new shader text. `waterLit` exists only under Enhanced, so the Original text is unchanged.
`graphics-ocean-switch` (`CSVM/src/Testing/GraphicsSwitchSuites.cs`) counts the `IsSurfTexture`
materials in `Sea(Rig)` beside the base sheet and checks that every one carries
`csky_ocean_hides_sea(` under Enhanced and none under Original, fresh, switched and round-tripped;
C1B builds 5 ring materials, 5 of 5 hiding in Enhanced and 0 of 5 in Original. With the ocean live
the white ring no longer draws in C1B or C2 and the flat sheet's water shows under it; with
`--no-ocean` and under Original graphics the frame is byte-identical.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** While the wave ocean is live, the `srf*` ring draws nowhere the ocean draws; with the ocean
off, in Original graphics, and on the spyglass disc it draws exactly as today.

**Evidence (confidence: traced).** The sheet's hide is `oceanHide = waterLit && oceanBase`
(`SceneBuilder.cs:2400`), adding the include and a varying at 2467-2468 and the discard at 2590,
guarded by `!FlatSeaEye`. `waterLit` is Enhanced-only (`worldLit = GraphicsMode.Enhanced && ...`,
line 2394), so Original's text cannot change. `oceanBase` is part of the shader key and the
generator's arguments (line 2371-2374, 2385-2389). The hide clears only where the mask's filtered
coverage is at or above `SeaThreshold` (`csky_ocean.gdshaderinc:54`), which A1 makes true under the
ring. `TextureCycler` swaps the ring's `albedo_tex` each frame (`TextureCycler.cs:89`) and does not
care what the shader does with it.

**Approach.** Thread an `oceanSurf` flag (true for `IsSurfTexture`, from A1) through the key and
`BiasShaderCode` beside `oceanBase`, and let `oceanHide = waterLit && (oceanBase || oceanSurf)`. No
new shader text: the ring's blended arm takes the same include, varying and discard. Update
`GraphicsSwitchSuites`' counts of shaders carrying the hide call (`GraphicsSwitchSuites.cs:3177`,
3328) to include the ring's.

**Model recommendation.** medium. One flag through an existing seam; the suite counts are the work.

**Verify.** `.\scripts\RunTests.ps1 -Suite graphics-ocean-switch -SkipUnits -SkipGoldens`; the
golden pose `--freecam --chapter=C1B --pos=-5900,120,-8350 --direction=0,-0.3,1 --det --mute --graphics=enhanced --water-quality=waves`
through `RunProbe.ps1` with and without `--no-ocean`: with the ocean the white rings are gone
(flat water under them until C22), without it they are pixel-identical to the baseline; the same
pose with `--graphics=original` is byte-identical to its baseline hash (`c1b-night-sea` in the
manifest covers the Original pose).

**⚠ Traps.** The key must carry the flag or `BiasShaders[key]` (`SceneBuilder.cs:2369-2375`) hands
the ring a sheet shader or the sheet a ring shader. A first switch to Enhanced rewrites shaders in
place (`ShaderTwins.Rewrite`, docs/architecture/Mech3.md, LOG-26), so the suite must switch, not
only build. The ring's material is `BlendSurface`, so `WorldMerge` never merges it
(`IsStaticOpaque`, line 1018-1021); nothing to re-split.

# Wave C, the shader

## C21 ☐ The ramps measure from the land: swell zero inside the surf zone, look to the coast, per-chapter tune

**Goal.** In C1B and C2 the waves' texture, normals and whitecaps run to the land edge; the swell's
height is zero where the authored surf coverage is full and rises with distance from the land, so no
crest lifts through the land and no trough opens a hole.

**Evidence (confidence: direction-sound).** With A1 the shore distance under the ring is measured
from the land, so the current ramps (`SwellFrom` 24 m .. `SwellFull` 160 m, `LookFrom` 12 m ..
`LookFull` 64 m, `SeaState.cs:104-117`) move the fade inward by one ring width but leave it as a
fade from flat; the values are TUNE. No land geometry lies below y = 0 at the coast
(`analysis/shore-water/FINDINGS.md`), which is the measurement behind holding the swell at zero
there. The authored ring is 6 to 92 m wide depending on the coast, so a fixed metre value cannot
match it and the surf coverage can.

**Approach.** In `OceanShader`'s vertex stage (line 702-706), `amp = shore_swell(G) * (1 - surf) * ship_calm * wave_scale`
with `surf` the A3 texture read as the lift is (`textureLod(surf_tex, ...)`); the wake term stays on
`shore_swell` alone. In the fragment, `look` stays `shore_look(G)`, so the normals and foam reach the
coast at `LookFrom`. Per-chapter entries in `ocean_seas.json` for `C1B` and `C2` set `swell_from`,
`swell_full`, `look_from` and `look_full` to the values the author settles at the lab; the defaults
and every other chapter's entry stay as they are, so C1 (whose tiles remain shore) does not move
beyond A2's distance refinement. Keep `Clamped`'s `LookFrom >= Cell` floor (`SeaState.cs`, Fields
line 49-50): C1's opaque tiles still need the one-texel seam it protects.

**Model recommendation.** medium. The code is a multiply; the values are the author's at the lab.

**Verify.** The ocean lab (Shift+F1 in `--freecam --chapter=C1B`) with the sliders, Save writes the
entry; then the golden pose above and a close shore pose
`<TODO: pick a low pose 30 m off an island shore in C1B and one at C2's ring; the author judges the look from a montage>`.
`OceanShaderTests`' byte-for-byte fixture (`CSVM.Tests/fixtures/ocean/default_l0_zoned.gdshader.txt`)
is regenerated once for the new text, with the default sea still producing it.

**⚠ Traps.** Every ramp value is TUNE, not fact: file disagreements as `type:tuning`, do not argue
them in code comments. `Clamped` holds `SwellFrom <= SwellFull - RampGap` and the same for the look
(`SeaState.cs:202-205`); a chapter entry outside range is clamped with a `world` warning, not an
error. The wake's calm zone (`ship_calm`) and the harbour ramps (`lift_tex`, C2's quays at 1.03 m)
are unchanged by this item; a C2 quay that floods under a crest is a ramp question, not a surf one.

## C22 ☐ Procedural surf in the ocean fragment: dense wash at the land edge, breaking lines travelling shoreward

**Goal.** Along every ringed coast the ocean draws its own surf: foam that is dense against the
land, thins across the authored ring's width, and carries breaking lines that travel toward the
shore on the sim clock, in the ocean's foam colour and roughness, so the waves and the surf read
as one water.

**Evidence (confidence: lead-only).** The fragment already has the pieces: `foam_noise`,
`foam_holes` and the footprint fades (`OceanShader.cs:485-516`, 836-848), the whitecap mix at
line 850-852 and the roughness mix at 858; the surf is one more term into `foam_mix`. What the band
should look like is the author's call at the controls; the original's ring (white at the land edge
fading seaward, 9 fps shimmer, `analysis/shore-water/FINDINGS.md`) is the reference for its extent,
not its look.

**Approach.** In `fragment()`, read `surf` (A3) and `G`; `wash = surf * smoothstep(..)` for the
dense foam at the land edge; `lines = smoothstep(a, b, sin(G * k - csky_time * w + noise))` for the
breakers, with `k` and `w` from new `SeaState` fields (`surf_strength`, `surf_spacing_m`,
`surf_speed_mps`, group "Surf", lab sliders, file keys) and `w` rounded to whole cycles per 3600 s
wrap as every drift is (`FoamPatchTerms`, `OceanShader.cs:266-275`). Modulate both by `foam_noise`
at the patch scale and by `tint_tex` as the water is, gate the fine holes by the footprint fades so a
distant shore does not shimmer, and fold the result into `foam_mix` before the colour and roughness
mixes (line 851-858). A chapter whose surf texture is the zero texel adds nothing, so C1, C1C, C2B,
C3 and C5 draw no new foam. Emit the surf block only when the sea's `SurfStrength > 0`, so the
default shader text is the current one plus the uniform reads.

**Model recommendation.** high. Look work with a determinism constraint; the author judges it.

**Verify.** `OceanShaderTests` regenerated once; the C1B golden pose and the close shore poses from
C21, as a montage of frames 120, 180 and 240 for the author
(`<TODO: the author's look verdict is the acceptance; ask it with the montage, quoting any complaint verbatim>`);
`--det` runs twice hash-identical (`docs/verification.md`, goldens must not move under load).

**⚠ Traps.** Every rate is whole cycles per `csky_time` wrap or the shore flashes at the hour
(`csky_time.gdshaderinc:10-13`). The foam colour `vec3(0.6, 0.65, 0.68)` is lit by the terrain
`light()` (`SceneBuilder.TerrainShadowLight`, `OceanShader.cs:861-862`), so night chapters dim it
as the ring's vertex greys did; do not brighten it for C1B alone. The fog hands the fragment back
to the sheet look (`FogSheetAt`, line 814); surf past that is wasted work and a pop.

# Wave D, landing

## D31 ☐ Goldens re-pinned, the look judged at the controls in C1B and C2, perf and the full battery

**Goal.** Every pinned Enhanced shot over a sea is re-rendered, the moves are named, the look passes
at the controls, and the complete battery is green on the merged tree.

**Evidence (confidence: traced).** The Enhanced goldens over sea chapters are `c1b-ocean-enhanced`
(the only one with waves), `c1-lake-enhanced`, `c5-city-night-enhanced`, `c1-cloud-deck-enhanced`,
`c1-rocket-hit-enhanced`, `c1-cockpit-enhanced` and `c1-cockpit-xr-sim-enhanced`
(`analysis/goldens/manifest.json`). A2 changes G on every shore, so `c1-lake-enhanced` and the C5
shot may move by the distance refinement; `c1b-ocean-enhanced` moves by design.

**Approach.** Run the goldens stage, read the ledger (`GoldenLedger.ps1`), and for each move write
in the landing commit what moved and why (surf, or the distance refinement). Re-pin after the
author's verdict on the C1B and C2 looks, then the complete `.\scripts\RunTests.ps1` with `-Perf`,
since the fragment grew.

**Model recommendation.** medium. Procedure, plus a report the author reads away from the repo.

**Verify.** `.\scripts\RunTests.ps1 -WaitQuiet` green or waived per suite with an owner; `-Perf`
within the A/B noise against `perf-history.jsonl`; the Original goldens (`c1b-night-sea` and every
non-Enhanced shot) unchanged, which is the proof Original is byte-identical.

**⚠ Traps.** A hash that flips between two `--det` runs is a determinism defect, not a tune
question. An unchanged Original hash is evidence only because the Enhanced hashes were seen to
move in the same run.
