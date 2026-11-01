# Painted coasts: what the data ships

The measurements PLAN-painted-coasts Wave A rests on: the coasts whose shoreline is painted into a
flat texture at y = 0 in C1, C2 and C3. The scripts in this folder take the unpacked gamez directory
(and, where pixels are read, the chapter's texture PNG directory) as arguments and follow the
`analysis/shore-water/` pattern. Extraction coordinates are the game's world coordinates with no axis
change (`GameZ.ParseVec3`, `CSVM/src/Mech3/GameZ.cs:295`). Node rotations compose in YXZ order or come
from the stored matrix (`GameZ.ParseTransform`, `GameZ.cs:263`), not the Rz·Ry·Rx order of the
shore-water scripts; a script that meets a rotated node must use the engine's order.

## A1, what lies under C3's strips and C2's beach

Method: `python -I analysis/painted-coasts/a1_strips.py <gamez dir> <texture dir> <strip texture ...>`,
run as `... extracted/C3/gamez extracted/C3/texture shore1.tif shore_trans.tif` and
`... extracted/C2/gamez extracted/C2/texture beach1.tif`. Node transforms compose as the engine's
`GameZ.ParseTransform` does; no node carrying a strip is rotated (26 nodes in C3, 17 in C2). Each
strip triangle is sampled on a barycentric grid about 6 m apart (65,435 points under `shore1`, 7,294
under `shore_trans`, 110,692 under `beach1`), recording every other surface whose XZ footprint covers
the point and its height there; sky polygons and the world floor (below -1000 m) are excluded. The
sampler first finds each of 20 strips at its own height under every one of its own samples.

### Census

| | C3 `shore1` | C3 `shore_trans` | C2 `beach1` |
|---|---|---|---|
| Polygons | 487 | 55 | 58 |
| y | 0.000 at every vertex | 0.000 | 0.000 |
| Across the alpha axis | U: 26.8 to 115.0 m, median 64.6 | U: 17.3 to 116.3 m, median 52.3 | V: 116.7 to 333.0 m, median 188.5 |
| Along the coast | | | U: 89.1 to 588.9 m |
| Edges to the nearest land vertex within 1 m of y = 0 (median) | U = 0 edge 37.3 m, U = 1 edge 30.3 m | 21.3 m, 8.6 m | V = 0 edge 65.0 m, V = 1 edge 64.7 m |

### The textures across the strip

- `shore1` (128x256) and `shore_trans` (128x128) carry alpha along U, transparent at both edges and
  opaque in the middle. U = 0 is the water side, told apart only by its painted dark blue-grey; U = 1
  is the sand side. Mean alpha passes 0.5 at U = 0.188 (`shore_trans` 0.195) and falls below it again
  at U = 0.828 (0.836). The water-side crossing is the painted waterline.
- `beach1` (256x128) carries alpha along V, transparent at both edges. V = 0 is the water side. Alpha
  passes 0.5 at V = 0.125 and falls below it at 0.852.

### What lies under the strips

No land lies below sea level under any strip. Below the strips there is only the base sheet
`wtr00000` at y = 0 and flat land at y = 0 (C3: `cliff1_sandtrans`, `sand128`, `cliff1_watertrans*`;
C2: `terpat04`, `terpat04-128`, `terpat01`). Above them there are objects 10 m or more up and, in C2,
`terpat04` up to 1.6 m.

| Under the strip | `shore1` | `shore_trans` | `beach1` |
|---|---|---|---|
| base sheet at 0 only | 44.0 % | 58.4 % | 44.9 % |
| land at 0 and base sheet at 0 | 39.5 % | 25.0 % | 5.1 % |
| land at 0 only | 5.0 % | 14.2 % | 42.8 % |
| either of the above, with an object above | 11.3 % | 2.5 % | 0.9 % |
| world floor only, or nothing | 0.2 % | 0 % | 6.3 % |

Across the strip, the land mesh's sea-level edge runs through the middle:

| Texture coordinate | `shore1` land at 0 | `shore_trans` land at 0 | `beach1` land at 0 |
|---|---|---|---|
| 0 to 0.125 | 12 % | 0 % | 0 % |
| 0.125 to 0.25 | 12 % | 1 % | 2 % |
| 0.25 to 0.375 | 14 % | 4 % | 11 % |
| 0.375 to 0.5 | 29 % | 17 % | 27 % |
| 0.5 to 0.625 | 69 % | 59 % | 71 % |
| 0.625 to 0.75 | 92 % | 87 % | 85 % |
| 0.75 to 1 | 91 to 93 % | 96 to 97 % | 95 to 98 % |

The rest of each row is the base sheet alone; in `beach1`'s 0.5 to 0.75 band, 14 to 22 % of samples
have nothing under them. The land meets the strip near its centre line (U or V about 0.5), while the
painted waterline lies at U 0.19 or V 0.125. Between the two, about a third of the strip's width
(roughly 20 m of a 65 m `shore1` strip, 70 m of a 190 m `beach1` strip), the painted sand lies over
the base sheet alone. Under `shore_trans`, some of the land at y = 0 is the cliff tiles of A3.

The picture holds along every coast run (13 runs of 18 to 81 strips in C3, 4 runs of 1 to 32 in C2,
the lowest surface under each at 0.00 m). C2's runs at (-7142, -3653) and (-9938, -6962) have holes
in both the sheet and the land on the beach's land side.

The land under the strips is therefore not below sea level: the strip's land side takes the solid
write alone, with no ramp lift, in both chapters.

### Poses

1280x720, 150 m up, 150 m out on the strip's water side; each checked with a `--freecam --det`
screenshot whose frame centre fell on the strip:

- C3: `'--pos=-13273,150,-8665' '--lookat=-13401,0,-8743'` (a run of 76 strips) and
  `'--pos=-10847,150,-8691' '--lookat=-10968,0,-8780'` (60).
- C2: `'--pos=-3591,150,-2455' '--lookat=-3662,0,-2587'` (32) and
  `'--pos=-10133,150,-6870' '--lookat=-10027,0,-6976'` (15). In the second, the coplanar `terpat04`
  land draws over the beach's sand half with a hard straight edge.

## A2, C1's and C2's coast tiles

Method: `python -I analysis/painted-coasts/a2_tiles.py <gamez dir> <texture dir> [profile.png]`, run
over C1 and C2. The script splits strip polygons into triangles (skipping degenerate joins), keeps the
flat ones, fits the UV-to-world map per triangle, reads each profile from the authored PNG, and
measures each tile vertex's distance to the land's sea-level edges (|y| ≤ 0.25, plus the cuts where
land triangles cross y = 0) and to the base sheet's edges. No node carrying a tile is rotated or
translated in either chapter.

### C1

| | `water1_trans1` | `water1_trans2` |
|---|---|---|
| Texture | 256x64 RGB, opaque, the ramp along U | 256x256 RGB, opaque, two ramps split by a seam on the diagonal u + v = 1 |
| Polygons | 129 (84 fans, 45 strips), 182 triangles, 24 nodes, priority 0 | 243 (194 fans, 49 strips), 350 triangles, 24 nodes, priority 0 |
| Height | y = 0 at every vertex | y = 0 at every vertex |
| Area | 1.425 km² | 0.952 km² |
| Second material | `cliff01_trans2` on 43 polygons (all 92 triangles at u 0.75 to 1) | `cliff01_trans2` on 64 polygons, `cliff01_trans1` on 1 (168 of the 172 triangles at p 0 to 0.25) |
| Coast axis and scale | U, a median 128 m per U (a texel is 0.5 m); V along the coast at 32 m per V | p = frac(frac(u) + frac(v)), land at p = 0 and water at p = 1, a median 90.5 m per unit (128/√2) |
| Width across the coast | 128 m: fans cover u 0 to 0.75 (96 m), the strips with the rock layer u 0.75 to 1 (32 m) | 90.5 m a ramp: fans cover p 0.25 to 1 (68 m), the strips with the rock layer p 0 to 0.25 (22.6 m) |

The profiles (column means for `trans1`, means along each diagonal for `trans2`, both halves alike):
from the water end, which matches the `water1` sheet's mean texel (30.2, 58.6, 67.6) to within 0.4 to
2.2 RGB, the colour first brightens to a light teal (about 30, 70, 74 near u = 0.31 in `trans1`), then
turns to a dark grass green, about (30, 52, 24). The green is 8.6 to 12.8 from `land_grs` and 67.5 to
74.3 from `cliff01`, the land the tiles actually meet, so a crossing measured on the sheet-to-`cliff01`
axis never reaches its midpoint. Measured against the profile's own two ends there is no step: the
10 % to 90 % rise spans 72 to 81 texels.

| Profile | 10 % (colour leaves the water) | 50 % crossing | 90 % | Fade 10 % to 90 % |
|---|---|---|---|---|
| `water1_trans1` (U) | u = 0.498, 63.5 m from the sheet edge | u = 0.655, 83.6 m from the sheet and 44.4 m from the cliff foot | u = 0.814 | 0.316, 40.5 m |
| `water1_trans2` upper half | p = 0.504, 44.5 m | p = 0.348, 58.7 m and 31.5 m | p = 0.188 | 0.316, 28.6 m |
| `water1_trans2` lower half | p = 0.508, 44.2 m | p = 0.370, 56.7 m and 33.5 m | p = 0.227 | 0.281, 25.5 m |

The rock layer drawn over the landward strip, `cliff01_trans2`, is opaque at the cliff foot and passes
alpha 0.5 at u = 0.846 (`trans1`, 19.7 m from the cliff foot) and p = 0.154 (`trans2`, 13.9 m). It is
the rock edge drawn on screen, about 25 m landward of the base colour's 50 % crossing.

Edges: all 81 `trans1` vertices at u = 1 and all 145 `trans2` vertices at p = 0 lie 0.0 m from
sea-level `cliff01` land, from which 101 `cliff01` polygons rise to 3 to 132 m. All 70 `trans1`
vertices at u = 0 lie on a sheet edge; for `trans2`, 94 to 96 % of the p = 1 vertices do. The tiles
and the sheet do not overlap (0 of 182 and 1 of 350 tile centroids lie over a sheet triangle).

### C2

C2 has no opaque gradient tile. Its painted water-to-land textures on flat polygons at y = 0 within
1 m of a `wtr` edge are `beach1` (A1) and `cliff01_trans2`. The others ruled out: `cliff01_trans1`
only layers 34 rising `terpat01` cliff slopes (14.7 to 121 m); `terpat01_trans1/2` are grass fringes
over `terpat04` land; `resblock_trans1..4` sit in model prototypes about 2980 m from any sheet; and
`terpat04`, `terpat04-128` and `terpat01` are opaque dirt and grass whose row and column means stay
96 RGB or more from the `wtr00000` mean.

`cliff01_trans2` in C2 is a 64x256 RGBA rock edge: 29 polygons (28 strips, 1 fan), priority 1, in 9
nodes, 0.213 km², 27 of them flat at y = 0 (58 triangles) and 2 climbing to 8.0 m. U runs across the
coast at a median 30.6 m (20.0 to 44.6), V along it at 256 m per V. Alpha is about 0 at u = 0, the
water side, and opaque from u ≈ 0.75 to 1; each texel row crosses 0.5 at its own U, between 0.1 and
0.7, so the edge is ragged. The mean crossing is u = 0.398 (12.0 m from the water edge, 18.6 m from
the cliff foot); the 10 % point is u = 0.195 and the fade from 10 % to 90 % is u 0.195 to 0.617,
12.9 m. The land end matches `cliff01` to 0.9 RGB.

The `wtr` sheet does not stop at the strip: all 58 strip centroids lie over a `wtr` triangle and the
u = 1 vertices sit on sheet edges at the cliff foot, so the strip is a decal over the sheet, and the
mask reads its footprint as solid out to its u = 0 edge. The rocks the author sees above the water are
31 `cliff01` polygons rising from the strips' landward vertices to 63.7 to 111.1 m (median 96); no
other polygon stands over the sheet or the strips within 150 m.

### Poses

Not yet seen in a frame (freecam's vertical field of view is 46.8°):

- C1 `water1_trans1`, near the `c1-lake-enhanced` golden: `'--pos=-7926,150,-3173' '--lookat=-7846,0,-3323'`
- C1 `water1_trans2`: `'--pos=-1823,150,-3208' '--lookat=-1690,0,-3313'`
- C2 `cliff01_trans2`: `'--pos=-9089,100,-3989' '--lookat=-8998,0,-4049'` and
  `'--pos=-9986,100,-5119' '--lookat=-9889,0,-5170'`

## A3, C3's cliff tiles

Method: `python -I analysis/painted-coasts/a3_cliffs.py <C3 gamez dir> <C3 texture dir>`. The water
reference is `wtr00000`, C3's base sheet (material 220). The materials are 232 (`cliff1_watertrans`),
222 (`cliff1_watertrans2`) and 227 (`cliff1_watertrans3`); no polygon carries them in a later material
pass, and no node carrying them is rotated.

### Flat and climbing

The test is the mask's sea-level test per triangle, minY > -0.5 and maxY < 0.5
(`CSVM/src/Effects/OceanMask.cs:520`).

| Texture | Polygons | Flat | Not flat | Triangles (flat) | Flat area | v = 1 vertex to the v = 0 edge (min, p10, median, p90, max) | Metres per unit v (median) |
|---|---|---|---|---|---|---|---|
| `cliff1_watertrans` | 45 | 44 | 1, at y 40.91 to 81.07 m | 166 (153) | 245,448 m² | 29, 64, 108, 149, 209 m | 114 |
| `cliff1_watertrans2` | 177 | 177 | 0 | 619 (619) | 1,685,971 m² | 57, 88, 127, 144, 206 m | 118 |
| `cliff1_watertrans3` | 36 | 36 | 0 | 53 (53) | 162,586 m² | 29, 42, 128, 181, 202 m | 128 |

258 polygons in all, 257 flat. The one that is not flat is a 15-vertex `cliff1_watertrans` polygon
centred at x -4404, z -4803, starting 40.9 m above the sea; it never touches the water. Every vertex of
a flat polygon has |y| < 0.0001, so the mask's ±0.5 m test separates the two sets with a 40 m margin.
No polygon mixes flat and climbing triangles.

### The coast axis

V runs across the coast: every v = 0 vertex of a flat polygon lies within 0.5 m of a land vertex at
sea level, and every v = 1 vertex within 0.5 m of an edge of the sea-level base sheet. U runs along
the coast, tiling 0 to 2.5 on `cliff1_watertrans` and 0 to 1 on the other two. Image row 0 is v = 0:
land (forest and rock) is painted at the top, water at the bottom.

### The colour profile along v

All three textures are 128x128 RGB with no alpha. `wtr00000`'s mean texel is RGB 82.0, 124.0, 122.2,
and its own texels lie within 13.1 of it. The profile is each row's mean; d is its RGB distance from
the sheet's mean texel. The painted water is not the sheet's colour: each tile's water end sits 30 to
33 from it. Neither profile has a step; d rises steadily from v ≈ 0.85 to v ≈ 0.1.

| Texture | Water end d | Land end d | Crossing v, midpoint of the profile's own ends | 10 % point (colour leaves the painted water) | Fade 10 % to 90 % |
|---|---|---|---|---|---|
| `cliff1_watertrans` | 33.3 | 86.1 | 0.332, about 38 m from the land edge | v 0.777, about 25 m in from the sheet | v 0.777 to 0.121, 0.656 in v, about 75 m |
| `cliff1_watertrans2` | 33.2 | 99.2 | 0.363, about 43 m from the land edge | v 0.762 | v 0.762 to 0.152, 0.609 in v, about 72 m |

A threshold taken from the sheet's mean texel instead (half the land end's distance) sits only 10 to
17 above the painted water's own distance and fires on texel noise at v ≈ 0.97 to 1.0, so the
profile's own ends are the reference. The midpoint crossing lies inside the painted rocks.

`cliff1_watertrans3` is a corner tile with no coast profile along v: its water is a wedge around
u = 0, v = 1, and the rest is rock, forest and sand. Only 7 % of its texels are water-coloured, and its
26 vertices that meet only the base sheet all sit at u = 0, v = 1. A constant-v iso-line misplaces its
coast.

### Overlays

None of the 258 `cliff1_watertrans*` polygons carries a second material pass (45, 177 and 36 checked,
0 with an overlay), so C3 has no counterpart to C1's `cliff01_trans2` rock edge: the shore crossing on
these tiles is the opaque colour profile alone. In the whole chapter 16 of 16,087 polygons have two
passes, none on the coast.

### The edges

- Seaward (v = 1) on the base sheet: 122 of 122, 491 of 491 and 39 of 39 vertices, each within 0.5 m
  of an edge of the sea-level `wtr` sheet.
- Landward (v = 0) on a land vertex at sea level: 119 of 119, 480 of 480 and 55 of 55, each within
  0.5 m. The land met is the cliff terrain: `cliff1` for `cliff1_watertrans`, mostly `forest1-128` for
  `cliff1_watertrans2`, `sand128` and `forest1-128` for `cliff1_watertrans3`. The tallest land polygon
  at those vertices rises a median of 84, 90 and 42 m, so the landward edge is the cliff's foot.

### Poses

Over `cliff1_watertrans2` near the `c3-island` golden, 250 m seaward of the polygons' centroid and
180 m up (framing assumed from a 60 degree vertical field of view, not yet seen in a frame):

- `'--pos=-5152,180,-2649' '--lookat=-5325,0,-2830'`
- `'--pos=-4438,180,-3355' '--lookat=-4670,0,-3449'`
