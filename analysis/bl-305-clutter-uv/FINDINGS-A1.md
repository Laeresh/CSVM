# A1 — C1's terpat02 UV repeat in world metres, against the 512 m grid constant

Repo-convention analysis writeup for PLAN-clutter-uv-placement item A1 (docs/PLAN-clutter-uv-placement.md), following the shape of analysis/collider-probe/FINDINGS.md. This is a permanent repo artifact the plan's checklist requires, not an ephemeral status report.

**Question.** Across C1's terpat02-textured world polygons, how many world metres does one full UV repeat of the terrain texture span, and how does that compare to the 512 m the remake currently tiles the template on (ClutterBuilder's GroundInfo, Clutter.cs:569-583)? Repeated for C5's cblock1/2/3/7 and, since the machinery is generic once built, every other registered template in all 8 chapters.

## Method

analysis/bl-305-clutter-uv/uv_repeat.py, modelled on analysis/collider-probe/probe.py: pure static analysis over extracted/<C>/gamez/{nodes,models,materials,textures}.json, no engine run. See the script's own module docstring for the full field-by-field mapping; the load-bearing parts:

- Root selection mirrors ClutterBuilder.PlaceOnWorld exactly (Clutter.cs:632-667), not WorldBuilder's render walk: world1.child_indices AND its partitions grid's referenced nodes, each walked from Transform3D.Identity independently, with no dedup between the two lists and no active-flag check at any level, because the mechanism under test is the placement code, not the render path (WORLD-15).
- Each node's own local transform (GameZ.ParseTransform, GameZ.cs:210-238) is reimplemented in full, including the Euler-angle branch, and composed onto the accumulated transform before recursing into children.
- Per-layer texture matching, not layer 0 unconditionally (the A1 trap): a polygon's materials list has an independent material_index + uv_coords per layer (GameZ.cs:454-483 — 619 install-wide polygons carry a 2nd layer, 7 a 3rd), and this script checks every layer against the target texture set. On the measured textures every match came from layer 0; no terpat02/cblock triangle needed a layer >=1 match, so the extraction's polygon shape is confirmed to expose per-layer textures.
- Triangle enumeration (fan vs strip) matches SceneBuilder.EmitPolygon / PlaceOnMesh, indexed by corner position, not vertex id, matching Clutter.cs:857-858.
- Per triangle: true 3D world-space area 0.5*|cross(v1-v0, v2-v0)| (not an XZ projection) and signed UV-space area, clamped and reported as a drop (DIAG-15) when either area falls below 1e-6. sqrt(worldArea / uvArea) is the metres-per-repeat for that triangle.
- Template resolution mirrors ClutterBuilder.FindTemplateRoot + FirstWithMesh + GroundInfo: the parentless Object3d root named by extracted/interp.json's AddClutterTemplates lines (minus C5's cblock4/5/6 exemption, BL-250), its first mesh-bearing descendant depth-first, and that mesh's layer-0 texture + XZ extent as the per-template period (confirmed: NOT a single global constant — C1's terpat02 measures 512 m, C5's cblock* measure 256 m, C2's parklot1/2 measure 32 m).

**Verify.** Two self-checks run on every invocation, both exercised (METHOD-9 — shown that the check can fail, not just that it passes):

1. A synthetic 1:1-UV triangle over a 256 m span reports exactly 256.0000 m; the same triangle with its UV span halved (perturbed) reports 512.0000 m, proving the check moves under a deliberate fault.
2. An area-reconstruction identity (sum uvArea*rate^2 == sum worldArea) is checked both on a synthetic 3-triangle set and, per texture, on the real measured totals — every one of the texture/chapter rows passed within float tolerance (diffs of 0 to 1.5e-8 against tolerances of 9.8 to 113121).

Run: `python analysis/bl-305-clutter-uv/uv_repeat.py --extracted Z:/CSVM/extracted --verbose` (this worktree has no extracted/ — it is git-ignored and worktrees don't get it — hence the explicit path to the main checkout). Exit code 0 on this run (LOG-12: the verdict is the self-checks plus every texture's own reconstruction check, all passing).

## Headline result — C1's terpat02

| stat | value |
|---|---|
| triangles matched | 2,979 (1,617 dropped — see "degenerate triangles" below) |
| world area measured | 88,769,166 m^2 |
| min metres/repeat | 184.7 m |
| median (area-weighted) | 259.6 m |
| max metres/repeat | 461.6 m |
| remake's current constant | 512 m |
| world area below 512 m | 100.0% |
| world area at/above 512 m | 0.0% |

Area-weighted histogram (% of terpat02-textured world area, by metres/repeat):

| bucket (m) | % of area |
|---|---|
| [0, 128) | 0.0% |
| [128, 256) | 2.6% |
| [256, 384) | 97.3% |
| [384, 512) | 0.1% |
| [512, 768) and up | 0.0% |

Every measured square metre of C1's terpat02 terrain stretches its UV faster than once per 512 m — the fastest patch (184.7 m) and the slowest (461.6 m) both fall short of the constant the remake currently tiles at. Converting metres/repeat into a density ratio (original instance count is proportional to 1/repeat^2 for a fixed decoration set per template cell; remake's is proportional to 1/512^2, so ratio = (512/repeat)^2):

| region | metres/repeat | original/remake density ratio |
|---|---|---|
| fastest-stretching patch (min) | 184.7 m | 7.7x |
| typical (area-weighted median) | 259.6 m | 3.9x |
| slowest-stretching patch (max) | 461.6 m | 1.2x |

This directly supports, and quantifies, the user's report that C1's trees are thinner than the original's. Under UV-space placement the original would plant somewhere between 1.2x and 7.7x as many trees as the current fixed grid does, typically ~3.9x, and there is no region of terpat02 where the current 512 m constant is too dense — the "under-dense in some regions, over-dense in others" framing in Clutter.cs:23-25 does not hold for C1 at all; every region is under-dense.

## The 256-1280 m claim (Clutter.cs:23-25) does not survive

The class comment asserts the world's UV tiling "is wildly non-uniform on hillsides — 256..1280 m per repeat". Measured against every terrain-hillside texture this script covers (not just C1):

| chapter | texture | min (m) | max (m) |
|---|---|---|---|
| C1 | terpat02.tif | 184.7 | 461.6 |
| C1 | river1.tif | 111.1 | 155.9 |
| C1 | river2.tif | 101.3 | 151.4 |
| C1B | vegrocktop.tif | 65.4 | 162.7 |
| C2 | terpat01.tif | 148.1 | 335.9 |
| C2 | terpat04.tif | 220.3 | 330.5 |
| C3 | cliff1_sandtrans.tif | 61.8 | 108.9 |
| C4 | terpat01.tif | 196.2 | 367.9 |
| C4 | river5.tif | 85.1 | 185.8 |

The floor is wrong (measured minimums run as low as 61.8 m, well under the claimed 256 m floor, on every chapter) and the ceiling is wrong by a wide margin (measured maximums top out at 461.6 m — C1's own worst case — against a claimed 1280 m, more than 2.7x the real observed peak). The comment's shape (real non-uniformity across a several-hundred-metre range) is directly confirmed; its specific bracket is not, and per DIAG-8 it is re-derived here rather than inherited. No provenance for "256..1280" was ever cited in the comment, and nothing in this survey reproduces it. This is a finding for B14's class-comment rewrite, not a number to keep.

(Building-footprint templates — resblock*, filmblock*, parklot*, cblock* — cluster tightly around their own period, typically within +/-1 m: flat rectangular building lots have near-uniform UV mapping, unlike sloped terrain. Excluded from the table above because they are not "hillsides" in the sense the claim is making.)

## C5's cblock1/2/3/7 — this predicts an alignment error, not a spacing error

| template | texture | remake period | min | median | max | area below period |
|---|---|---|---|---|---|---|
| cblock1 | cblock1.tif | 256.0 m | 255.2 m | 256.0 m | 258.2 m | 0.8% |
| cblock2 | cblock2.tif | 256.0 m | 255.2 m | 256.0 m | 258.9 m | 5.9% |
| cblock3 | cblock3.tif | 256.0 m | 255.2 m | 256.0 m | 258.1 m | 6.4% |
| cblock7 | cblock7.tif | 256.0 m | 5.0 m* | 256.0 m | 256.7 m | 2.9% |

*cblock7's minimum is one sliver triangle (0.4% of its area falls under 128 m; 97.1% sits in [256, 384) with the rest of the family) — see "degenerate triangles" below; it does not change the picture.

All four cblock ground textures measure within ~1% of the 256 m the remake already tiles them at. GroundInfo derives 256 m directly from each template's own ground-quad extent, and the world polygons' own UV stretch lands almost exactly there too. A1's Approach section asked this number to decide "whether BL-305's packing is a spacing error or an alignment error" — the data says spacing is not the problem. If C5's city blocks pack edge-to-edge with visible gaps or overlaps (BL-305), the fixed period the remake already uses is the right magnitude; A2's UV-to-world orientation question (does +U consistently map to world +X, does it rotate per polygon) is where the packing defect has to live, not here.

## Degenerate triangles (DIAG-15 — reported, not silently dropped)

terpat02 dropped 1,617 of 4,596 candidate triangles (35.2%), all for zero world-space area, not zero UV area (confirmed by re-running the walk with the two drop reasons separated: world_zero=1617, uv_zero=0). Every example traced to node g16333's fan triangulation sharing a vertex position across two or more corners of the same polygon (a repeated-vertex sliver, common at terrain LOD stitches and skirt edges) — a real property of the source mesh, not a bug in the extraction or this script. river1/river2/cliff1_sandtrans/terpat01/terpat04/river5/terpat01-128/vegrocktop all show the same pattern at similar or higher rates (up to 52.8% for C4's terpat01). No polygon anywhere in this survey was dropped for a missing or degenerate UV array (dropped_no_uv=0 on every single texture measured) — the "if the extraction's polygon shape does not expose per-layer UVs, say so" trap does not apply; UVs are present and well-formed everywhere a template texture appears.

## The template-quad-vs-world-repeat ratios are not arbitrary — they cluster at 1, sqrt(2) and 2

Added by the orchestrator on review of the run above, because the per-template numbers say more
together than they do apart. `GroundInfo` derives each template's period from its own ground quad's
XZ extent; the script measures what the world's UVs actually do with that texture. Dividing one by
the other gives the linear error the remake carries per template, and its square is the density
error:

| chapter | template | quad period | world repeat (median) | ratio | density error |
|---|---|---|---|---|---|
| C1 | terpat02 | 512 m | 259.6 m | 1.97 | **3.9x too few** |
| C1 | river1 | 256 m | 128.2 m | 2.00 | **4.0x too few** |
| C1 | river2 | 256 m | 128.0 m | 2.00 | **4.0x too few** |
| C1B | rockclut | 256 m | 126.6 m | 2.02 | **4.1x too few** |
| C2 | parkpat | 512 m | 159.9 m | 3.20 | **10.2x too few** |
| C2 | filmblock1 | 128 m | 90.5 m | 1.41 | 2.0x too few |
| C2 | parklot1 | 32 m | 22.6 m | 1.42 | 2.0x too few |
| C2 | parklot2 | 32 m | 27.7 m | 1.16 | 1.3x too few |
| C3 | cliff1_sandtrans | 128 m | 90.5 m | 1.41 | 2.0x too few |
| C2 | terpat01, terpat04, terpat04-128, resblock1-6, filmblock2-5 | — | — | 1.00 | none |
| C4 | terpat01, terpat01-128, river5 | — | — | 1.00 | none |
| C5 | cblock1, cblock2, cblock3, cblock7 | 256 m | 256.0 m | 1.00 | none |

Two things fall out of this that neither the item nor the plan anticipated.

**First, C1 is the worst chapter in the game for this, and by a clean factor of 4.** Every one of its
templates — the forest and both rivers — is off by exactly 2 in linear terms, and C1B's `rockclut`
with them. Meanwhile C2's and C4's terrain templates and all of C5's city blocks are correct to
within 1%. The user's report that C1's trees are thin, and that C5's problem looks like packing
rather than sparsity, matches the measured data chapter for chapter. The remake's error is not
uniform across the game and cannot be fixed by changing one constant.

**Second, the ratios are suspiciously exact.** They are not scattered: they sit at 1.00, 1.41, 2.00
and one 3.20. That is a structural mismatch, not measurement noise, and the shape of it is a lead for
A2's orientation question.

> ### ⚠ Corrected by A2 — both readings offered here were wrong
>
> This section originally proposed two explanations for the clustering: that 1.41 = sqrt(2) meant a
> **45-degree-rotated UV mapping**, and that the exact-2 cases were **quads authored across two
> texture repeats**. A2 measured the UV coordinates themselves and disproved both
> ([`FINDINGS-A2.md`](FINDINGS-A2.md), "A1's two candidate readings, both settled here"). Neither
> guess is a fact and neither should be carried forward.
>
> - **There is no 45-degree mapping anywhere in the install.** Every bearing on the sqrt(2)
>   templates is 0 or 90 degrees. Those three templates are three of the **four non-square quads**,
>   and their world tiling matches the quad *per axis, exactly*: `cliff1_sandtrans` 128x64 quad ->
>   world 128.00/64.00; `filmblock1` 64x128 -> 63.99/128.00; `parklot1` 16x32 -> 16.00/32.00. The
>   sqrt(2) is an artifact **of this table's own method**: `Period` is `max(extX, extZ)` while the
>   measured rate is effectively a geometric mean `sqrt(extX*extZ)`, and on a 2:1 quad their ratio is
>   exactly sqrt(2). It measured the statistic, not the data.
> - **`parklot2` (32x16 quad -> world 32.00/16.00) is the fourth instance and is missing from the
>   table above.** Its 1.16 ratio is this same artifact seen through a partly-degenerate world
>   sample, not a distinct case.
> - **The exact-2 cases are not double-span quads.** C1's `terpat02` spans exactly 0..1 (A2 checked
>   all 32 templates; not one spans anything else), so `FUN_004dd230`'s `fmod` wrap folds nothing.
>   The factor 2 is a genuine **world-vs-template scale mismatch** — the terrain is painted with the
>   same texture at half the scale the template quad uses.
>
> **What survives, and it is the part that matters:** the factor-2 templates are C1's, the density
> loss they imply is real, and C1 is exactly where the forest is reported too sparse. The headline
> result of this item is unaffected. What died is only the mechanism this section guessed at.

## What this does NOT determine (per A1's own scope, and to head off overreading)

- Not the density ratio by itself. The remake's seen dedup and MinSlopeCos cull (B13) remove instances the UV figure above says nothing about; A3's coplanar/subface census and B13's slope-cull removal are what turn this into an actual before/after instance count.
- Not the UV-to-world orientation. This script measures a scalar rate per triangle; it does not establish whether the ground quad's +U axis maps to world +X consistently, rotates per polygon, or flips — that is A2, and B12 is written against A2's answer, not this one.
- Not a C1 original-vs-remake capture comparison. The user's "C1 trees are thinner" report has no capture behind it yet (B14's own Evidence table says the same); this item supports the report with a mechanism and a number, it does not verify it against original footage.
- cblock7's poleflare/lightpole and other non-ground-quad decorations are untouched here — this measures only the terrain polygons a template's ground texture matches, not the template's own decoration count or type.

## Full script output (verbatim, 2026-08-09 run)

```
== self-checks (METHOD-9) ==
  synthetic 256 m / unit-UV triangle -> 256.0000 m (OK)
  perturbed (half-UV-span) triangle -> 512.0000 m (OK, check can fail)
  area-reconstruction identity over 3 synthetic triangles -> diff=2.27e-13 (OK)

== C1 ==
  registered templates (3):
    terpat02: root=[5863] ground=[5886] texture=terpat02.tif period=512.0
    river1: root=[2748] ground=[5427] texture=river1.tif period=256.0
    river2: root=[2712] ground=[2714] texture=river2.tif period=256.0
  river1 -> river1.tif (remake period 256.0 m):
    triangles=255 dropped_no_uv=0 dropped_degenerate=38
    world area total=665348.8 m^2  uv area total=39.9716
    min=111.1 m  median(area-wtd)=128.2 m  max=155.9 m
    area fraction below 256.0 m: 100.0%   at/above: 0.0%
    self-check: overall_rate=129.018 m -> OK (diff=1.16415e-10, tol=665.349)
  river2 -> river2.tif (remake period 256.0 m):
    triangles=594 dropped_no_uv=0 dropped_degenerate=118
    world area total=1443434.9 m^2  uv area total=86.4606
    min=101.3 m  median(area-wtd)=128.0 m  max=151.4 m
    area fraction below 256.0 m: 100.0%   at/above: 0.0%
    self-check: overall_rate=129.208 m -> OK (diff=2.32831e-10, tol=1443.43)
  terpat02 -> terpat02.tif (remake period 512.0 m):
    triangles=2979 dropped_no_uv=0 dropped_degenerate=1617
    world area total=88769165.6 m^2  uv area total=1293.1323
    min=184.7 m  median(area-wtd)=259.6 m  max=461.6 m
    area fraction below 512.0 m: 100.0%   at/above: 0.0%
    histogram: [0,128)=0.0% [128,256)=2.6% [256,384)=97.3% [384,512)=0.1% [512,768)=0.0% [768,1024)=0.0% [1024,1280)=0.0% [1280,inf)=0.0%
    self-check: overall_rate=262.005 m -> OK (diff=1.49012e-08, tol=88769.2)

== C1B ==
  rockclut -> vegrocktop.tif (remake period 256.0 m):
    triangles=20 dropped_no_uv=0 dropped_degenerate=6
    world area total=138930.6 m^2  uv area total=11.0000
    min=65.4 m  median(area-wtd)=126.6 m  max=162.7 m
    area fraction below 256.0 m: 100.0%   at/above: 0.0%
    self-check: overall_rate=112.384 m -> OK (diff=0, tol=138.931)

== C2 ==
  registered templates (17): terpat01, terpat04, terpat04-128, filmblock1-5, parklot1, parklot2,
  parkpat, resblock1-6 (roots/grounds/textures/periods all resolved, see script --verbose output)
  filmblock1 -> filmlot1.tif (period 128.0 m): triangles=11 min=90.5 median=90.5 max=90.5 below=100.0% -> OK
  filmblock2 -> filmlot2.tif (period 128.0 m): triangles=18 min=119.3 median=128.0 max=128.4 below=64.4% -> OK
  filmblock3 -> filmlot3.tif (period 128.0 m): triangles=12 min=127.8 median=128.0 max=128.4 below=11.9% -> OK
  filmblock4 -> filmlot4.tif (period 128.0 m): triangles=6 min=128.0 median=128.0 max=128.1 below=0.0% -> OK
  filmblock5 -> filmlot10.tif (period 128.0 m): triangles=14 min=127.9 median=128.0 max=128.2 below=31.6% -> OK
  parklot1 -> parklot1.tif (period 32.0 m): triangles=40 min=19.6 median=22.6 max=22.7 below=100.0% -> OK
  parklot2 -> parklot2.tif (period 32.0 m): triangles=12 min=22.6 median=27.7 max=27.8 below=100.0% -> OK
  parkpat -> parkfront.tif (period 512.0 m): triangles=4 min=128.0 median=159.9 max=159.9 below=100.0% -> OK
  resblock1 -> resblock1.tif (period 128.0 m): triangles=100 min=127.8 median=128.0 max=128.0 below=0.0% -> OK
  resblock2 -> resblock2.tif (period 128.0 m): triangles=68 min=128.0 median=128.0 max=128.3 below=0.0% -> OK
  resblock3 -> resblock_trans1.tif (period 128.0 m): triangles=39 min=128.0 median=128.0 max=128.1 below=0.0% -> OK
  resblock4 -> resblock_trans2.tif (period 128.0 m): triangles=43 min=128.0 median=128.0 max=128.1 below=0.0% -> OK
  resblock5 -> resblock_trans3.tif (period 128.0 m): triangles=18 min=128.0 median=128.0 max=128.0 below=0.0% -> OK
  resblock6 -> resblock_trans4.tif (period 128.0 m): triangles=34 min=128.0 median=128.0 max=128.0 below=1.1% -> OK
  terpat01 -> terpat01.tif (period 256.0 m): triangles=1766 dropped_degenerate=920 min=148.1 median=259.6 max=335.9 below=0.5% -> OK
  terpat04 -> terpat04.tif (period 256.0 m): triangles=811 dropped_degenerate=192 min=220.3 median=256.0 max=330.5 below=3.0% -> OK
  terpat04-128 -> terpat04-128.tif (period 128.0 m): triangles=94 dropped_degenerate=21 min=127.7 median=128.0 max=143.8 below=1.9% -> OK

== C2B ==
  registered templates (6): terpat01, terpat03, terpat04, resblock2, filmblock1, filmblock2 —
  every one reports "no template root in gamez" (retail-data-normal per Clutter.cs's
  FindTemplateRoot doc: C2B's boot script registers templates its own gamez does not carry).

== C3 ==
  cliff1_sandtrans -> cliff1_sandtrans.tif (period 128.0 m):
    triangles=319 dropped_degenerate=100 min=61.8 median=90.5 max=108.9 below=100.0% -> OK

== C4 ==
  river5 -> river5.tif (period 128.0 m): triangles=109 dropped_degenerate=50 min=85.1 median=128.0 max=185.8 below=51.7% -> OK
  terpat01 -> terpat01.tif (period 256.0 m): triangles=3748 dropped_degenerate=1981 min=196.2 median=258.9 max=367.9 below=0.6% -> OK
  terpat01-128 -> terpat01-128.tif (period 128.0 m): triangles=789 dropped_degenerate=427 min=85.9 median=130.0 max=185.2 below=4.0% -> OK

== C5 ==
  cblock1 -> cblock1.tif (period 256.0 m): triangles=709 min=255.2 median=256.0 max=258.2 below=0.8% -> OK
  cblock2 -> cblock2.tif (period 256.0 m): triangles=412 min=255.2 median=256.0 max=258.9 below=5.9% -> OK
  cblock3 -> cblock3.tif (period 256.0 m): triangles=306 dropped_degenerate=6 min=255.2 median=256.0 max=258.1 below=6.4% -> OK
  cblock7 -> cblock7.tif (period 256.0 m): triangles=2544 dropped_degenerate=2 min=5.0 median=256.0 max=256.7 below=2.9% -> OK
```

Exit code: 0. Reproduce with `python analysis/bl-305-clutter-uv/uv_repeat.py --extracted Z:/CSVM/extracted --verbose` for the full unabridged per-chapter listing (this file's C2/C3/C4/C5 blocks are condensed to one line per texture for length; every number above is copied verbatim from that run, only the line layout is compressed).

## Files

- analysis/bl-305-clutter-uv/uv_repeat.py — the script.
- CSVM/src/Mech3/Clutter.cs — GroundInfo (:569-583, the per-template period this compares against), PlaceOnWorld/PlaceOnMesh (:632-699, the walk this script mirrors), FindTemplateRoot (:210-222), TemplateNames/BuriedClutterDistricts (:123-199).
- CSVM/src/Mech3/GameZ.cs — ParseTransform (:210-238), the polygon multi-layer materials parse (:454-483).
- extracted/interp.json — AddClutterTemplates census.
- docs/verification.md — WORLD-15, DIAG-8, DIAG-15, METHOD-9 (the rules this method leans on).
