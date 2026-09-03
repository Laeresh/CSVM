# A2 — the template ground quad's UV parameterisation, and the UV→world orientation

**Read-only measurement, 2026-08-09**, for PLAN-clutter-uv-placement
A2. Instrument: [`uv_orient.py`](uv_orient.py) in this directory, over `extracted/` only — no
engine run, no C# source touched. This is the verification
the retired `analysis/bl-058-clutter-doubling/FINDINGS.md:121-127`
(`git show analysis-archive:analysis/bl-058-clutter-doubling/FINDINGS.md`)
named as the reason `match_footprints.py` was inconclusive.

Reproduce (from the repo root; from a worktree add `--extracted Z:/CSVM/extracted`):

    python analysis/bl-305-clutter-uv/uv_orient.py --samples 3

## The two answers

**(i) Every template ground quad spans exactly 0..1 in both UV axes — all 32 of them, install-wide.**
Checked against every `AddClutterTemplates` name in every chapter's boot script (38 names, 6 of
which no chapter's gamez carries — retail-data-normal, see `Clutter.cs:203-208`). Not one quad
spans 0..2 or anything else, so `FUN_004dd230`'s `fmod` wrap folds nothing and *does* mean
"fractional position across the quad". Trap (b) of the plan's A2 section is closed: it does not
bite. **But the remake's current stand-in for that fraction is wrong on four of the 32** — see
"What the remake gets wrong" below.

**(ii) A world polygon's UV axes are axis-aligned but *not* consistently oriented — there are eight
frames in the data and no discoverable rule picks between them, and that does not matter, because
the original stamps in texture space.** For C1's `terpat02` the dominant frame is +U → world **+Z**,
+V → world **−X** (69.6 % of 2,979 triangles) — a 90° rotation away from the template quad's own
+U → +X, +V → +Z — with eight other frames making up the rest and 5.7 % off-axis entirely.
C5's `cblock1` is the opposite extreme: 97.7 % identity, all flat, all one handedness. Handedness
itself is split almost evenly in C1 (1,441 mirrored / 1,538 not), so a *mirrored* UV frame is
ordinary shipped data, not a corruption. See "Is there a rule?" for why B12 is unaffected.

## The UV convention, established rather than assumed

`docs/formats/gamez.md:9` states the extraction is Godot's frame exactly — right-handed Y-up, nose
at −Z, "no mirroring, no UV V-flip". Both consumers agree with that: `SceneBuilder.EmitTriangle`
(`CSVM/src/Mech3/SceneBuilder.cs:705-706`) and `ClutterBuilder.BuildSpriteMesh`
(`CSVM/src/Mech3/Clutter.cs:857-858`) both call `SetUV(uvs[corner])` with no sign change anywhere in
the path, and `GameZ.cs:466-471` reads `{u, v}` straight through. Godot and Direct3D 7 also share the
V-down / top-left texture origin, so there is no flip to insert on either side. **The extraction is
not flipped, and the remake does not flip it.**

That is a chain of claims, so the script tests the *instrument* for a flip rather than trusting it
(METHOD-9): it prints each ground quad's raw corner→UV pairs so a reader can see which corner
carries (0,0), and its self-check feeds a synthetic quad three ways — plain, U/V swapped, and
V-flipped — and requires the three to report different answers. If the script were blind to the UVs,
the swapped case would come back identical to the plain one.

```
A2 self-check (METHOD-9: the instrument must be able to fail)
  synthetic 1:1 quad      : |A|=256.000 |B|=256.000  headU=0.0 headV=90.0  -> OK
  same quad, U/V swapped  : headU=90.0 headV=-0.0  -> OK (must differ from above)
  same quad, V flipped    : +V world dir (-0.000, -0.000, -256.000) -> OK (must point -Z)
  degenerate UV triangle  : OK (must be refused)
  fmod wrap [0,1)         : OK
```

A note on the one link I could **not** close: that mech3ax's `uv_coords` are byte-identical to the
array `FUN_004de2c0` reads at polygon+0x18 is `gamez.md`'s claim, not something re-derived here. It
is strongly corroborated (the template quads come out as exact 0/1 corners, the C5 city UVs come out
as exact integers and 1/256ths, and the world's tiling reconstructs sensible 128/256/512 m periods),
but it is inherited evidence.

## (i) in full: the census

`extX`/`extZ` are the quad's local X and Z extents; `period` is what `ClutterBuilder.GroundInfo`
(`Clutter.cs:569-583`) reduces them to; `quad frame` names where the quad's own +U and +V point in
its local space; the last three columns are the decoration count, the worst disagreement between the
wrapped quad UV and the remake's `(origin − min corner) / period`, and the number of decorations that
fail to project onto the quad at all.

```
  ch    template           ground texture            extX    extZ  period  U span / V span           quad frame  decos  max|wrapUV-remake|  notproj
  C1    terpat02           terpat02.tif             512.0   512.0   512.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     28   1.1e-16  0
  C1    river1             river1.tif               256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     10   0.0e+00  0
  C1    river2             river2.tif               256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      9   0.0e+00  0
  C1B   rockclut           vegrocktop.tif           256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     31   1.1e-16  0
  C1C   (registers no clutter templates)
  C2    terpat01           terpat01.tif             256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     23   5.6e-17  0
  C2    terpat04           terpat04.tif             256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     28   1.1e-16  0
  C2    terpat04-128       terpat04-128.tif         128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     14   1.1e-16  0
  C2    filmblock1         filmlot1.tif              64.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      3   2.6e-01  0
  C2    filmblock2         filmlot2.tif             128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      2   5.6e-17  0
  C2    filmblock3         filmlot3.tif             128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      2   0.0e+00  0
  C2    filmblock4         filmlot4.tif             128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      1   5.6e-17  0
  C2    filmblock5         filmlot10.tif            128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      3   0.0e+00  0
  C2    parklot1           parklot1.tif              16.0    32.0    32.0  0.000..1.000 / 0.000..1.000  U=-X V=-Z      7   7.4e-01  0
  C2    parklot2           parklot2.tif              32.0    16.0    32.0  0.000..1.000 / 0.000..1.000  U=-X V=-Z      7   7.2e-01  0
  C2    parkpat            parkfront.tif            512.0   512.0   512.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     10   5.6e-17  0
  C2    resblock6          resblock_trans4.tif      128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      9   1.1e-16  0
  C2    resblock5          resblock_trans3.tif      128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      9   1.1e-16  0
  C2    resblock4          resblock_trans2.tif      128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     12   1.1e-16  0
  C2    resblock3          resblock_trans1.tif      128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     10   1.1e-16  0
  C2    resblock2          resblock2.tif            128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     23   5.6e-17  0
  C2    resblock1          resblock1.tif            128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     22   1.1e-16  0
  C2B   terpat01             (no parentless Object3d by that name -- retail-data-normal, see Clutter.cs:203-208)
  C2B   terpat03             (no parentless Object3d by that name -- retail-data-normal, see Clutter.cs:203-208)
  C2B   terpat04             (no parentless Object3d by that name -- retail-data-normal, see Clutter.cs:203-208)
  C2B   resblock2            (no parentless Object3d by that name -- retail-data-normal, see Clutter.cs:203-208)
  C2B   filmblock1           (no parentless Object3d by that name -- retail-data-normal, see Clutter.cs:203-208)
  C2B   filmblock2           (no parentless Object3d by that name -- retail-data-normal, see Clutter.cs:203-208)
  C3    cliff1_sandtrans   cliff1_sandtrans.tif     128.0    64.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z      6   3.0e-01  0
  C4    terpat01           terpat01.tif             256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     43   1.1e-16  0
  C4    terpat01-128       terpat01-128.tif         128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     32   1.1e-16  0
  C4    river5             river5.tif               128.0   128.0   128.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     31   1.1e-16  0
  C5    cblock1            cblock1.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     96   1.1e-16  0
  C5    cblock2            cblock2.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     64   5.6e-17  0
  C5    cblock3            cblock3.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     93   1.1e-16  0
  C5    cblock4            cblock4.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     41   1.1e-16  0
  C5    cblock5            cblock5.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     63   1.1e-16  0
  C5    cblock6            cblock6.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     91   1.1e-16  0
  C5    cblock7            cblock7.tif              256.0   256.0   256.0  0.000..1.000 / 0.000..1.000  U=+X V=+Z     49   1.1e-16  0
  38 registered template names, 6 of them absent from their chapter's gamez
  every resolved quad spans exactly 0..1 in both axes: YES
  NON-SQUARE quads (4) -- GroundInfo's scalar Period cannot describe these: C2/filmblock1 (64x128), C2/parklot1 (16x32), C2/parklot2 (32x16), C3/cliff1_sandtrans (128x64)
  MIRRORED quads (2) -- their UVs run opposite to world +X/+Z: C2/parklot1 (U=-X V=-Z), C2/parklot2 (U=-X V=-Z)
  worst disagreement between the wrapped quad UV and the remake's (origin - min)/period: 0.736  (0 would mean the remake's rule is a relabelling)
```

Every quad is flat (Y extent 0.000 on all 32), normal +Y, one polygon, four corners. The plan's three
named templates in full:

```
C1 / terpat02  (root node 5863, ground node 5886, model 1160)
  ground texture      : terpat02.tif
  local extent X/Y/Z  : 512.000 / 0.000 / 512.000   GroundInfo.Period = 512.000
  quad corners (local vertex -> uv):
    [0] (256.000, 0.000, 256.000)    -> u=1.000000 v=1.000000
    [1] (256.000, 0.000, -256.000)   -> u=1.000000 v=0.000000
    [2] (-256.000, 0.000, -256.000)  -> u=0.000000 v=0.000000
    [3] (-256.000, 0.000, 256.000)   -> u=0.000000 v=1.000000
  U span 0.000000 .. 1.000000   V span 0.000000 .. 1.000000
  +U is world (1.000, -0.000, -0.000) (512.000 m per unit U, bearing -0.0 deg from +X toward +Z)
  +V is world (-0.000, -0.000, 1.000) (512.000 m per unit V, bearing 90.0 deg)
  quad normal         : (0.000, 1.000, 0.000)
  decorations         : 28

C5 / cblock1  (root node 1759, ground node 1732, model 996)
  ground texture      : cblock1.tif
  local extent X/Y/Z  : 256.000 / 0.000 / 256.000   GroundInfo.Period = 256.000
  quad corners (local vertex -> uv):
    [0] (-128.000, 0.000, 128.000)   -> u=0.000000 v=1.000000
    [1] (128.000, 0.000, 128.000)    -> u=1.000000 v=1.000000
    [2] (128.000, 0.000, -128.000)   -> u=1.000000 v=0.000000
    [3] (-128.000, 0.000, -128.000)  -> u=0.000000 v=0.000000
  U span 0.000000 .. 1.000000   V span 0.000000 .. 1.000000
  +U is world (1.000, 0.000, -0.000) (256.000 m per unit U, bearing -0.0 deg from +X toward +Z)
  +V is world (-0.000, -0.000, 1.000) (256.000 m per unit V, bearing 90.0 deg)
  decorations         : 96

C2 / resblock1  (root node 680, ground node 681, model 685)
  ground texture      : resblock1.tif
  local extent X/Y/Z  : 128.000 / 0.000 / 128.000   GroundInfo.Period = 128.000
  quad corners (local vertex -> uv):
    [0] (64.000, 0.000, 64.000)      -> u=1.000000 v=1.000000
    [1] (64.000, 0.000, -64.000)     -> u=1.000000 v=0.000000
    [2] (-64.000, 0.000, -64.000)    -> u=0.000000 v=0.000000
    [3] (-64.000, 0.000, 64.000)     -> u=0.000000 v=1.000000
  U span 0.000000 .. 1.000000   V span 0.000000 .. 1.000000
  +U is world (1.000, -0.000, -0.000) (128.000 m per unit U, bearing -0.0 deg from +X toward +Z)
  +V is world (-0.000, -0.000, 1.000) (128.000 m per unit V, bearing 90.0 deg)
  decorations         : 22
```

Note the corner *order* differs between `terpat02` and `cblock1` (the winding starts at a different
corner and runs the other way) while the parameterisation is the same. Anything that keys off "corner
0" rather than off the UVs will read these two quads inconsistently.

### What the remake gets wrong today — for B11

`ClutterBuilder.ParseTemplate` (`Clutter.cs:498-502`) stores `(origin − quad min corner)` in metres
and `PlaceOnTriangle` (`:348`) divides by the scalar `GroundInfo.Period` = `max(extentX, extentZ)`
(`:581`). On **28 of the 32** templates that is an exact relabelling of the quad UV — the census's
`max|wrapUV − remake|` column is at float noise, 1.1e-16, for every one of them. On the other four it
is not:

| template | why | worst error |
|---|---|---|
| C2 `filmblock1` | quad is 64 × 128 m; `Period` = 128 applies the long side's scale to U as well, so U comes out half what it should | 0.26 UV |
| C3 `cliff1_sandtrans` | quad is 128 × 64 m; same, on V | 0.30 UV |
| C2 `parklot1` | quad is 16 × 32 m **and** its UVs are mirrored — u = 0 at max X, v = 0 at max Z | 0.74 UV |
| C2 `parklot2` | quad is 32 × 16 m, same mirroring | 0.72 UV |

Worked in one line for `parklot1`: `c_studebaker2` at local x = −3.651 carries quad UV
u = (8 − x)/16 = 0.728, and the remake computes (x + 8)/32 = 0.136. Mirrored *and* half-scaled.

So B11's rewrite is not a pure refactor — it moves C2's parked Studebakers and film-lot buildings and
C3's palms. That is four templates out of 32, all small, and the change is a *correction*; it just
must not be mistaken for a regression when B11's "behaviour-preserving" golden is taken. **B11 should
replace `Period` with the quad's own UV→local affine map (two axes with signs, not one scalar)** — the
mirrored quads need the sign, and nothing needs `Period` afterwards.

Also for B11: **not one decoration in the whole install fails to project onto its quad** (the
`notproj` column is 0 everywhere), so `FUN_004dd230`'s `template %s clutter %s does not project to
polygon.` error path is never taken by retail data. It should still be implemented as a skip-and-log,
per the plan, but it is not a case any chapter exercises.

## (ii) in full: the world side

Walk mirrors `PlaceOnWorld` (`Clutter.cs:635-667`): `world1`'s `child_indices` then its
spatial-partition grid's referenced nodes, accumulated local transforms, `WorldBuilder.SkipWorldNode`
(`horizon` / `dzpaths` / `fvol*`) and non-nearest-`Lod` skipped. Every texture layer is examined, not
just layer 0 (`FUN_004de190` iterates them all). Exclusions, per LOG-5:

```
C1: 3234 triangle(s) kept; dropped 0 with no UVs on the matching pass, 1655 zero-area in world (fan artifacts), 0 degenerate in UV
C5: 3253 triangle(s) kept; dropped 0 with no UVs on the matching pass,    2 zero-area in world (fan artifacts), 0 degenerate in UV
C2: 1866 triangle(s) kept; dropped 0 with no UVs on the matching pass,  920 zero-area in world (fan artifacts), 0 degenerate in UV
```

The zero-area drops are fan-triangulation artifacts of n-gons with repeated or collinear corners —
they carry a nonzero UV span, so their affine map is finite but meaningless (both axes collapse onto
one line). Leaving them in inflated C1's metres-per-U maximum from 561 m to 32,768 m, which is how
they were noticed. **B12 must skip them too** — a zero-area *world* triangle with a nonzero *UV* area
passes a UV-area test, so the UV-degeneracy guard the plan already asks for is not enough on its own.

The "UV frame" tables below name where each triangle's +U and +V point, against the template ground
quad's own frame (+U = +X, +V = +Z), with a 5° tolerance; "off-axis" is everything else.

```
-- C1 / terpat02  (texture terpat02.tif): 2979 world triangles
  all                    n=2979
      +U bearing  min/med/max  -180.00    90.00   180.00 deg   metres/U   134.47   258.90   560.83
      +V bearing  min/med/max  -180.00    -0.00   180.00 deg   metres/V   125.21   258.22   541.79
      handedness  {-1: 1441, 1: 1538} (+1 = same as the template quad's, -1 = mirrored)
      UV frame (vs the template quad's U=+X V=+Z):
        U=+Z V=-X          2073   69.6%
        U=+Z V=+X           242    8.1%
        U=+X V=+Z           222    7.5%
        (off-axis)          169    5.7%
        U=-X V=-Z           122    4.1%
        U=-X V=+Z            79    2.7%
        U=-Z V=+X            40    1.3%
        U=+X V=-Z            17    0.6%
        U=-Z V=-X            15    0.5%
  flat (|Ny|>0.999)      n=125     U=+Z V=-X 73.6%, U=+X V=+Z 12.0%, U=-Z V=+X 7.2%, off-axis 3.2%, ...
  gentle (0.9-0.999)     n=2462    U=+Z V=-X 71.9%, U=+Z V=+X 8.4%, U=+X V=+Z 7.3%, off-axis 4.7%, ...
  sloped (|Ny|<=0.9)     n=392     U=+Z V=-X 53.6%, off-axis 12.5%, U=-X V=-Z 9.4%, U=+Z V=+X 8.7%, ...

-- C1 / river1  (texture river1.tif): 255 world triangles
        U=-Z V=+X            83   32.5%
        U=-X V=-Z            67   26.3%
        U=+Z V=-X            37   14.5%
        U=+X V=+Z            36   14.1%
        U=+X V=-Z            18    7.1%
        (off-axis)            8    3.1%
        U=-Z V=-X             6    2.4%

-- C5 / cblock1  (texture cblock1.tif): 709 world triangles   [all flat, |Ny| > 0.999]
      handedness  {1: 709}
        U=+X V=+Z           693   97.7%
        U=+Z V=-X             8    1.1%
        U=-Z V=+X             6    0.8%
        U=-X V=-Z             2    0.3%
      metres/U min/med/max 254.37 / 256.00 / 260.43   metres/V 255.07 / 256.00 / 256.76

-- C5 / cblock7  (texture cblock7.tif): 2544 world triangles
      handedness  {1: 2537, -1: 7}
        U=+X V=+Z          2504   98.4%
        (off-axis)           22    0.9%
        U=-X V=-Z            18    0.7%

-- C2 / resblock1  (texture resblock1.tif): 100 world triangles   [all flat]
        U=+X V=+Z            98   98.0%
        U=-X V=+Z             2    2.0%

-- C2 / terpat01  (texture terpat01.tif): 1766 world triangles
      handedness  {1: 912, -1: 854}
        U=+X V=+Z          1241   70.3%
        U=-X V=-Z           120    6.8%
        U=-Z V=+X           112    6.3%
        U=+Z V=-X           102    5.8%
        U=-X V=+Z            79    4.5%
        U=+X V=-Z            43    2.4%
        U=-Z V=-X            26    1.5%
        (off-axis)           22    1.2%
        U=+Z V=+X            21    1.2%
  flat (|Ny|>0.999)      n=94      U=+X V=+Z 79.8%
  sloped (|Ny|<=0.9)     n=123     U=+X V=+Z 41.5%, U=-X V=-Z 13.0%, U=+Z V=-X 11.4%, off-axis 9.8%

-- C3 / cliff1_sandtrans  (texture cliff1_sandtrans.tif): 319 world triangles
      metres/U min/med/max  82.90 / 128.00 / 178.74   metres/V  39.92 /  64.00 /  93.01
      handedness  {1: 151, -1: 168}
      +U bearing modes (deg x count): 90 x54, 0 x50, -90 x49, 180 x34, -180 x17, 99 x5

-- C2 / filmblock1 (filmlot1.tif): 11 tris   metres/U 63.99/64.00/64.00   metres/V 128.00 flat   all bearings 0 / 90
-- C2 / parklot1   (parklot1.tif): 40 tris   metres/U 16.00              metres/V 24.00/32.00/32.07  all bearings 0 / 90
-- C2 / parklot2   (parklot2.tif): 12 tris   metres/U 32.00              metres/V 16.00/16.00/24.09  all bearings 0 / 90
-- C2 / parkpat    (parkfront.tif): 4 tris   metres/U 128.00/160.00/160.00  metres/V 128.00/159.84/159.84
```

METHOD-11 is honoured: the flat / gentle / sloped split is reported for every template, and it
changes the answer. On C1 `terpat02` a flat-only sample would have said "+U is +Z, 73.6 %"; the
sloped population says the same frame at only 53.6 % with 12.5 % off-axis. Concluding "consistent"
from either alone would have been wrong. The C5 street grid, sampled deliberately as the plan asks,
is the case that looks consistent — and it is the exception, not the rule.

### Is there a rule?

**No rule that predicts the frame from the geometry, and none is needed.** Three observations:

1. **The frame is a property of the authored texture mapping, not of the world axes.** It is
   axis-aligned in ~95 % of cases but the choice among the eight axis-aligned frames varies per
   polygon, and it varies *within a single node's mesh*, not just between nodes. C1's terrain was
   painted with `terpat02` rotated 90° over most of its area; C5's street grid was not rotated at all.
2. **Handedness is not a corruption.** C1 `terpat02` is 48 % mirrored, C2 `terpat01` 48 %, C5
   `cblock1` 0 %. A mirrored UV frame is ordinary shipped data. The remake's world renderer already
   draws all of it correctly, which is the corroboration that it is not an extraction artifact.
3. **`FUN_004dd6e0` never asks the question.** Steps 4–7 build the UV bounding box, walk the integer
   lattice, test containment **in UV space**, and recover XYZ through the per-triangle affine map.
   Nothing in that path references a world axis. The clutter therefore rotates, mirrors and stretches
   *with the ground texture*, which is exactly the intent: the decorations were authored against the
   painted texture, so a building sits in its painted block wherever that block lands.

That is why the earlier `match_footprints.py` attempt was inconclusive and why it could not have been
otherwise: it compared decoration positions to the painted texture in *world* metres, and the world
frame is not the frame the positions live in. The mapping was never missing — it is per triangle, and
there are 2,979 of them for `terpat02` alone.

The one thing that is *not* discoverable and that Wave B must therefore not try to use: **there is no
global "the clutter grid starts here" origin.** The remake's `gx * period` world grid
(`Clutter.cs:339-348`) is a fiction with no counterpart in the original.

### A1's two candidate readings, both settled here

A1 measured a per-template ratio between the quad's `Period` and the world's metres-per-UV-repeat and
found it clusters at 1.00, 1.41 and 2.00. Both explanations it offered are disproven by the UV
coordinates themselves:

- **"sqrt(2) is a 45-degree-rotated UV mapping."** No. The three sqrt(2) templates are exactly three
  of the four **non-square** quads, and their world tiling matches the quad **per axis, exactly**:
  `cliff1_sandtrans` quad 128 × 64 → world 128.00 / 64.00; `filmblock1` 64 × 128 → 63.99 / 128.00;
  `parklot1` 16 × 32 → 16.00 / 32.00. Every bearing in all three is 0° or 90°; there is no 45°
  mapping anywhere in the install. The sqrt(2) is `Period` (= max extent) over the geometric mean
  `sqrt(extX·extZ)` on a 2:1 quad — an artifact of comparing a geometric-mean statistic against a
  max-extent scalar. **`parklot2` (32 × 16 → world 32.00 / 16.00) is the fourth instance and is
  missing from A1's list.**
- **"The exact-2 cases are quads authored across two texture repeats."** No. C1 `terpat02` spans
  exactly 0..1 (see the census), and its world triangles tile at median 258.90 m/U and 258.22 m/V
  against a 512 m quad. The factor 2 is a genuine **world-vs-template scale mismatch** — the same
  texture is laid on the terrain at half the scale the template quad uses — and it does not touch the
  `fmod` wrap at all. `river1` (256 m quad, world 131.94 m median) and `rockclut` are the same shape.
  C2 `parkpat`'s 3.20 is likewise real (512 m quad, world 160.00 / 159.84), but only 4 world triangles
  carry `parkfront.tif`, so treat that figure as thin.

This matters for A1's own conclusion: the factor-2 templates are C1's, which is precisely where the
user reports the forest is too sparse. It is a real density term, not an artifact.

## Worked example (METHOD-1)

One decoration, one named world triangle, computed by hand and checked against the script. This is
B12's test case.

**The decoration.** C1's `terpat02` root is node 5863 (parentless `Object3d`); its ground node is
5886, mesh 1160. The ground node's first child is node **5909, `firtree1.flt`**, local translation
**(106.86228, 0.0, 66.779945)**.

The quad spans x ∈ [−256, 256] with u ∈ [0, 1] and z ∈ [−256, 256] with v ∈ [0, 1], so projecting the
decoration down the quad normal (+Y) and reading the interpolated UV gives

    u = (106.86228 + 256) / 512 = 0.708715
    v = ( 66.779945 + 256) / 512 = 0.630430

Both are already in [0, 1), so `FUN_004dd230`'s `fmod` wrap leaves them alone. The script's
plane-projection code, which does not know about the shortcut, returns the same numbers.

**The world triangle.** C1 node **2911 `g777`, model 953, polygon 3, triangle 5**
(**⚠ corrected by B12: this polygon is a `tri_strip`, not a fan** — its corner triple is a strip
triple. The vertices, UVs, affine map and result below are all correct; only the original label was
wrong. B12 pinned the distinction in a unit test, because corners 3 and 4 of this polygon share
vertex 5 with *different* UVs, which is what makes corner-indexing rather than vertex-indexing
load-bearing), texture layer 0, texture `terpat02.tif`:

| corner | world position | uv |
|---|---|---|
| v0 | (−9472.000, 128.000, −3328.000) | (1.000, 1.000) |
| v1 | (−9216.000, 128.000, −3584.000) | (0.000, 0.000) |
| v2 | (−9408.000, 128.000, −3328.000) | (1.000, 0.750) |

Flat (normal +Y), so the arithmetic is checkable on paper.

**Step 7's affine map, by hand.** From v0 to v2: Δuv = (0, −0.25), Δp = (64, 0, 0), so
B·(−0.25) = (64, 0, 0) and **B = (−256, 0, 0)** — one unit of V is 256 m toward world −X. From v0 to
v1: Δuv = (−1, −1), Δp = (256, 0, −256), so −A − B = (256, 0, −256), giving
A = −(256, 0, −256) − (256, 0, 0) = **(0, 0, 256)** — one unit of U is 256 m toward world +Z. This
triangle is in the `U=+Z V=-X` frame, C1's dominant one: rotated 90° from the template quad.

**Steps 4–6, the lattice.** UV bounding box u ∈ [0, 1], v ∈ [0, 1]; floored, `uInt ∈ {0, 1}` and
`vInt ∈ {0, 1}`, so four candidate cells. Candidate UVs are `(uInt + 0.708715, vInt + 0.630430)`:
(0.709, 0.630), (0.709, 1.630), (1.709, 0.630), (1.709, 1.630). Only the first is inside the triangle
by the UV-space edge test — the other three are outside the unit square this triangle occupies.

**Step 7, the position.**

    P = v0 + A·(0.708715 − 1) + B·(0.630430 − 1)
      = (−9472, 128, −3328) + (0, 0, 256)·(−0.291285) + (−256, 0, 0)·(−0.369570)
      = (−9472 + 94.610, 128, −3328 − 74.569)
      = (−9377.390, 128.000, −3402.569)

Script output, verbatim:

```
WORKED EXAMPLE (METHOD-1): one decoration, one named world triangle
  decoration      : node[5909] 'firtree1.flt' of C1/terpat02
  local position  : (106.862, 0.000, 66.780)
  quad UV at it   : (0.708715, 0.630430)  -> fmod-wrapped (0.708715, 0.630430)
  by hand         : u = (x + 256) / 512 = 0.708715 ; v = (z + 256) / 512 = 0.630430
  agreement       : OK

  world triangle  : node[2911] 'g777' model 953 poly 3 tri 5 pass 0
    v0 (-9472.000, 128.000, -3328.000)   uv (1.000000, 1.000000)
    v1 (-9216.000, 128.000, -3584.000)   uv (0.000000, 0.000000)
    v2 (-9408.000, 128.000, -3328.000)   uv (1.000000, 0.750000)
  UV bbox         : u 0.000000..1.000000  v 0.000000..1.000000
  floored lattice : uInt 0..1  vInt 0..1  (FUN_004dd6e0 step 4)
  affine map      : P(u,v) = p0 + A*(u-u0) + B*(v-v0)
    A (per +1 U)  : (0.000, 0.000, 256.000)  |256.000 m|
    B (per +1 V)  : (-256.000, -0.000, 0.000)  |256.000 m|
  lattice cells hit by this decoration's (u,v): 1
    candidate UV (0.708715, 0.630430)
      = p0 (-9472.000, 128.000, -3328.000) + A * -0.291285 + B * -0.369570
      -> world (-9377.390, 128.000, -3402.569)   affine-vs-barycentric 1.82e-12 m
  affine map agrees with barycentric interpolation: OK
```

The hand computation and the script agree to the printed digits. The script additionally recovers the
same point by barycentric interpolation of the three world vertices — an independent route — and the
two agree to 1.8e-12 m, which is the check that the affine map is not merely self-consistent.

Note what this triangle also shows about **step 7 versus the remake's barycentric height**
(`Clutter.cs:360`): on a planar triangle the two are the same thing, so B12 can use the affine map for
Y as well, as the plan prefers. They can only diverge if a triangle is non-planar, which a triangle
cannot be.

## What I could not determine

- **That the extraction's `uv_coords` are the array the original's `FUN_004de2c0` reads.** Inherited
  from `docs/formats/gamez.md:9`, corroborated by the exactness of the values, not re-derived. If it
  were wrong, everything above is self-consistent and wrong together — but the remake already renders
  the world's textures correctly from these same arrays, which is a strong constraint.
- **Why C1's terrain is painted with `terpat02` rotated 90°** over most of its area while C5's city is
  not. It is a fact of the authored data; no rule explains it and none is needed.
- **Whether the four mis-parameterised templates (`filmblock1`, `cliff1_sandtrans`, `parklot1/2`)
  visibly misplace anything today.** They are C2's Studebakers and film-lot buildings and C3's palms;
  measuring the visual difference needs a build, which A2 does not do. B11's A/B will show it.
- **`far_fade_range` / `translate_uv_range` interaction with the lattice** — out of scope (C23).

## Verdict for Wave B

**Not blocked. B11 and B12 can be written against this.** The template side is fully determined (0..1,
per-axis, with a sign), and the world side needs no consistency because the stamp is in UV space. The
two things Wave B must take from this document rather than from `Clutter.cs`:

1. `GroundInfo`'s scalar `Period` is not sufficient — four templates need the quad's two-axis signed
   UV→local map, and after B11 nothing needs `Period` at all.
2. A zero-area *world* triangle can carry a nonzero *UV* area; skip on both tests, and count what you
   skipped.

---

*Authored by the A2 subagent; saved to disk by the orchestrator, verbatim apart from decoding the
HTML entities its transport introduced into the code blocks. The subagent's own Write access was
refused mid-run.*
