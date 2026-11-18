# C5 ground under the Enhanced city

**Question.** How does C5's painted `cblock` ground relate to the building sets that stand on it:
which combinations of visible paint and building set exist, how much ground each covers, how
dark and how open their free floor is, whether `cblock2`'s and `cblock3`'s 45-degree streets form
a grid a rotated variant lattice could follow, and where the flagged ground with no buildings lies.
Asked by PLAN-c5-ground items A1 and A2 (issue #357).

**Instrument.** `survey.py` reuses `../city-hulls/census.py`'s loader (itself on
`../bl-305-clutter-uv/eligibility.py`) over `extracted/C5`. A building decoration's triangles are
mapped into texture UV through its template ground quad's own affine XZ to UV, as
`Clutter.ParseTemplate` projects a decoration's origin (`CSVM/src/Mech3/Clutter.cs:1251-1264`),
and rasterised at texel scale, a building crossing the tile edge drawn again on the opposite side.
A paint's streets are geometry in UV (`find_streets`): a corridor is a run of at least 10 m of
whole lines (columns, rows or 45-degree diagonals on a 1 m lattice, wrapping the tile) that no
building of any set standing on the paint touches, and its street is the kerb-to-kerb band the
paint draws inside it, the lines whose mean luma is at least half the corridor's brightest line.
Lit and unlit stretches of a street count alike; everything off the streets is block. Painted
structure is a block texel at luma 25 or more, luma is Rec. 601 on sRGB texels (0 to 255). The world census walks `world1` as `eligibility.build_transform_cache` does (each node once,
nearest LOD, `horizon`, `dzpaths` and `fvol*` skipped) and leaves the template subtrees out. Its
areas come from an exact planar arrangement of every coplanar `cblock` triangle (triangulated as
the engine does, fan or strip): each piece of ground is counted once, with the set of polygons
stacked on it, by convex clipping and no bounding boxes. `diagonal.py` reads the diagonal paints.
Both scripts take `--overlays` / `--plot` for images (`survey.py` writes a street plot per paint),
which hold original pixels and are refused inside the repo.

## Verdict

- **C5's `cblock` ground is 180.13 km², under 206.57 km² of polygons.** The difference is the
  coplanar layering: an overlay polygon over a base polygon, and a few overlay polygons stacked on
  each other.
- **Ground by visible paint and standing building set** (table below): `cblock7` with its own
  walk-ups 75.76 km² (42.1 %), `cblock1` with its towers 51.47 km² (28.6 %), the flagged `cblock1`
  over `cblock4`'s set 9.85 km², the two diagonal paints with their own and their beneath sets
  4.91 to 9.43 km² each, and 12.96 km² of flagged ground with no building set (7.2 %).
- **No `cblock4` base is exposed.** Every `cblock4`, `cblock5` and `cblock6` polygon is covered by an
  overlay, to the arrangement's precision. `cblock4` is never the visible paint, so the exposed-base
  row of the per-tile table has no ground in the world.
- **Every paint has one street layout and no alleys.** `cblock1` and `cblock7` draw a cross of two
  streets on u = 0.5 and v = 0.5, 33 to 36 m kerb to kerb; `cblock2` and `cblock3` one 45-degree
  street, 23.3 m kerb to kerb. Streets take 24 % (`cblock1`), 26 % (`cblock7`) and 13 % (`cblock2`,
  `cblock3`) of the tile, lit and unlit stretches alike. The 1 to 7 m building-free seams at the
  tile edges carry no painted street (section below).
- **The paint never fitted the buildings.** Under all eight axis-aligned texture frames the texels
  inside a template's own footprints average luma 2.9 to 6.4 on the overlay paints against a
  texture mean of 8.2 to 16.1, and 63 % to 91 % of the painted structure in the blocks lies outside
  them. The base paints show the same: inside 30.8 to 40.7 against a mean of 34.5 to 40.2, and 55 %
  to 77 % outside.
- **The area discrepancy is an instrument difference, not two readings of the world.**
  `CBLOCK-LOD.md` §2's per-texture table counts models, not placed nodes, and only models of two
  or more polygons (section below). The world census is 51.47 km² of clear `cblock1`.
- **The diagonal paints carry one street family each and no grid.** `cblock2` has one lit 45-degree
  street per tile on the centreline u + v = 0.5 + k, `cblock3` one on u − v = 0.5 + k, 22.6 m
  between kerbs and 181.0 m between centrelines. No street crosses them. A diamond lattice with
  edges on streets does not exist; a stripe lattice does (section below).
- **The bare flagged ground is scattered.** It is 12.96 km² in 55 to 68 polygons per paint, in
  clusters of 0.04 to 1.3 km², and its free floor is as dark as the rest of its paint.

## Ground by visible paint and building set

A flagged (`no_clutter`) polygon draws on top of the coplanar polygon beneath it, and only
unflagged polygons decorate (`docs/org/clutter.md`, "Which polygons get dressed"). So flagged
`cblock1/2/3/7` over a base shows its own paint and carries the base's buildings, unflagged overlay
carries its own, and flagged overlay with nothing beneath carries none. Area is from the world
arrangement; every other column is per 256 m tile of the paint under the set's footprints.

| Visible paint | Building set | Area (km²) | Built share of block | Structure uncovered | Free floor luma mean, median | Free floor at luma ≤ 5 | Widest free spot |
|---|---|---|---|---|---|---|---|
| `cblock1` | `cblock1` (clear) | 51.474 | 0.32 | 0.73 | 6.5, 0 | 0.70 | 72 m |
| `cblock1` | `cblock4` (flagged) | 9.850 | 0.31 | 0.65 | 6.0, 0 | 0.72 | 160 m |
| `cblock1` | `cblock2` (flagged, over clear `cblock2`) | 0.066 | | | | | |
| `cblock2` | `cblock2` (clear) | 9.425 | 0.30 | 0.80 | 6.6, 0 | 0.72 | 81 m |
| `cblock2` | `cblock5` (flagged) | 8.390 | 0.26 | 0.68 | 5.9, 0 | 0.74 | 96 m |
| `cblock2` | `cblock4` (flagged) | 0.131 | | | | | |
| `cblock3` | `cblock3` (clear) | 4.914 | 0.32 | 0.75 | 4.9, 0 | 0.78 | 75 m |
| `cblock3` | `cblock6` (flagged) | 6.092 | 0.28 | 0.73 | 4.6, 0 | 0.79 | 74 m |
| `cblock3` or `cblock6` | both (neither flagged) | 0.008 | | | | | |
| `cblock7` | `cblock7` (clear) | 75.756 | 0.18 | 0.87 | 6.3, 0 | 0.71 | 149 m |
| `cblock7` | `cblock4` (flagged) | 1.062 | 0.35 | 0.54 | 5.8, 0 | 0.72 | 158 m |
| `cblock4` | `cblock4` (exposed base) | 0.000 | 0.38 | 0.58 | 32.8, 29 | 0.15 | 154 m |
| `cblock1` | none (flagged, no base) | 3.906 | 0 | 1.00 | 6.0, 0 | 0.72 | 224 m |
| `cblock2` | none (flagged, no base) | 3.091 | 0 | 1.00 | 5.9, 0 | 0.74 | 158 m |
| `cblock3` | none (flagged, no base) | 3.524 | 0 | 1.00 | 4.4, 0 | 0.80 | 158 m |
| `cblock7` | none (flagged, no base) | 2.423 | 0 | 1.00 | 6.0, 0 | 0.72 | 220 m |
| `cblock1` | none (flagged over flagged `cblock4`) | 0.016 | | | | | |

Streets average luma 46 (`cblock1`), 35 (`cblock2`), 34 (`cblock3`), 45 (`cblock7`) and 55
(`cblock4`) and take 24 %, 13 %, 13 %, 26 % and 27 % of the tile. On a diagonal paint the widest
free spot with no buildings, 158 m, is the strip between two streets: 181.0 m less 23.3 m.

Of the 75.756 km² of `cblock7` with its own set, 0.427 km² is flagged `cblock7` over a clear
`cblock7` polygon laid in another UV frame (`U-X V-Z` at 45 m per repeat, 0.297 km², and an
off-axis frame at 128 m, 0.130 km²), whose buildings stamp in that frame. The flagged-overlay
coverage is bimodal as `CBLOCK-LOD.md` §2 found: 196 flagged polygons (26.330 km²) are wholly over
a decorating layer, 254 (12.894 km²) have none, and 2 (0.131 km²) are partly over one.

## The streets

Each street is a band in UV: the lines of one family whose coordinate lies within the half-width
of the centreline, repeating every whole UV (`+ k`). Family u is the street running along V
(u = const), v the one running along U, u + v and u − v the diagonals. Kerb to kerb is the band;
the corridor is the run of lines no building of either standing set touches, so the strip between
the two is building-free verge on the block side of the kerb. `survey.py` prints these figures
and `find_streets` returns them.

| Paint | Sets read | Street | Centreline ± half-width (UV) | Kerb to kerb | Corridor | Mean luma |
|---|---|---|---|---|---|---|
| `cblock1` | `cblock1`, `cblock4` | u | 0.5020 ± 0.0645 | 33.0 m | 37.0 m | 47 |
| `cblock1` | | v | 0.4980 ± 0.0645 | 33.0 m | 39.0 m | 49 |
| `cblock2` | `cblock2`, `cblock5` | u + v | 0.5000 ± 0.0645 | 23.3 m | 33.2 m | 35 |
| `cblock3` | `cblock3`, `cblock6` | u − v | 0.5000 ± 0.0645 | 23.3 m | 33.9 m | 34 |
| `cblock7` | `cblock7`, `cblock4` | u | 0.5000 ± 0.0703 | 36.0 m | 37.0 m | 46 |
| `cblock7` | | v | 0.4941 ± 0.0684 | 35.0 m | 39.0 m | 48 |
| `cblock4` | `cblock4`, `cblock1`, `cblock7` | u | 0.4980 ± 0.0723 | 37.0 m | 37.0 m | 58 |
| `cblock4` | | v | 0.5020 ± 0.0762 | 39.0 m | 39.0 m | 58 |
| `cblock5` | `cblock5`, `cblock2` | u + v | 0.5039 ± 0.0918 | 33.2 m | 33.2 m | 47 |
| `cblock6` | `cblock6`, `cblock3` | u − v | 0.4941 ± 0.0938 | 33.9 m | 33.9 m | 47 |

A diagonal half-width is in its family's coordinate; across the street it is that times
256 m × √2. The diagonal bands hold the kerb lines `diagonal.py` finds, 22.6 m apart centre to
centre. On the base paints, which are never visible, the band is the whole corridor: their paint
is daylit and shows no kerb step inside it.

What is not counted as street, and why:

- **Tile-edge seams.** Every paint has building-free lines along its tile edges that cross the
  whole tile: `cblock1`, `cblock4` and `cblock7` 2 m along u = 0 and 5 m along v = 0, `cblock2`
  and `cblock5` 5 m and 4 m, `cblock3` and `cblock6` 1 m and 7 m. The paint runs straight across
  them with no kerb, band or lamp, so they are the seam between one template's lots and the next
  repeat's, and stay block. The 10 m corridor floor separates them from every street.
- **No unlit street and no alley.** No other full-length line of any family is free of both sets.
  On the diagonal paints the crossing family has at most 3 building-free diagonals for one set alone
  and none for both.
- **Minor sets.** The 0.131 km² of `cblock4`'s set on `cblock2` paint and the 0.066 km² of
  `cblock2`'s set under rotated `cblock1` are left out of the corridors; their buildings stand
  across the paint's street.

## The area discrepancy

The world walk sums clear `cblock1` to 51.47 km²; `CBLOCK-LOD.md` §2 has 32,435,893 m². Both are
right about what they count. §2's table comes from `item9-depth-bias/cblock_probe4.py`, whose
question was coplanar pairs inside one model, so it:

- **skips every model with fewer than two polygons** (`if len(polys) < 2: continue`). Single-polygon
  models hold 19.10 km² of clear `cblock1` (and 1.11 km² of clear `cblock2`). The models with two or
  more polygons hold 32.436 km², §2's figure to the square metre; flagged `cblock1` reproduces the
  same way (11.282 km² against §2's 11,281,871 m²);
- **counts each model once**, while 9 models are placed by more than one node: the second and later
  copies add 2.03 km² of flagged `cblock1`, 0.66 km² of flagged `cblock2` and 0.92 km² of flagged
  `cblock3`;
- **includes the seven template ground quads** (65,536 m² each), which the world does not place;
- **sees only overlap inside one model**, which is why §2 has `cblock4` 97.0 % covered and about
  336,000 m² exposed: flagged `cblock7` in other nodes covers the rest.

No node is reached twice: the transform cache visits each node once, every `cblock` model's nodes
are reached, and the duplicates that exist are two nodes placing one model in one place (nodes 3665
and 3907, `g102`, flagged `cblock3`), which the arrangement counts once. LOD levels play no part:
no `cblock` polygon sits below a far LOD level. The scratch walk's flagged `cblock3` of 9.99 km²
fanned three triangle-strip polygons; triangulated as strips it is 9.75 km².

`docs/org/clutter.md`'s 7.6 % for the bare flagged ground is the figure a per-polygon sum gives
(13.83 of 181.02 km²), which counts stacked overlay polygons twice. The arrangement gives 12.96 of
180.13 km², 7.2 %.

## UV frames and the variant cell

| Texture | Clear (km²) | Flagged (km²) | `U+X V+Z` at 255 to 259 m |
|---|---|---|---|
| `cblock1` | 51.47 | 13.90 | 100 % clear; 96.3 % flagged (3.8 % turned 90 or 180 degrees) |
| `cblock2` | 9.49 | 11.61 | 100 %, 100 % |
| `cblock3` | 4.92 | 9.75 | 100 %, 100 % |
| `cblock4` | 11.04 | 0.02 | 100 % |
| `cblock5` | 8.39 | | 100 % |
| `cblock6` | 6.10 | | 100 % |
| `cblock7` | 75.77 | 4.09 | 99.4 % clear (0.4 % at 45 m, 0.2 % off-axis at 128 m); 99.9 % flagged |

Of the polygons in the `U+X V+Z` 256 m frame, 99.53 % by area put whole UVs at world multiples of
256 m. Their UV origins differ: 1,801 distinct world tiles hold UV (0, 0). Polygon edges therefore
lie on whole UVs, and in the square districts the lit street runs across the tile middle (v about
0.5 in `cblock1` and `cblock7`), so a polygon edge crosses the middle of a block. A variant hash
on raw UV would change at every such edge; a hash on world XZ, floor(x / 256 + 0.5) and
floor(z / 256 + 0.5), gives the half-tile cells the plan's Decision 4 describes on all of that
ground.

## The diagonal streets

`diagonal.py`, per diagonal paint. A diagonal of a square texture wraps onto itself, so every
per-diagonal number wraps the tile.

| | `cblock2` | `cblock3` |
|---|---|---|
| Street family | u + v = const | u − v = const |
| Kerb diagonals (lit ≥ 90 % end to end) | 111, 143 | 112, 144 |
| Kerb lit share per quarter of the rows | 0.98, 1.00, 0.81, 1.00; 1.00, 0.89, 1.00, 1.00 | 0.92, 0.84, 1.00, 1.00; 1.00, 1.00, 0.94, 0.81 |
| Free line fit per kerb | slope −0.9935, −0.9943 (44.81, 44.84 degrees), RMS 1.78, 1.96 texels | +0.9958, +0.9936 (44.88, 44.82 degrees), RMS 1.94, 1.70 texels |
| Street width between kerbs | 22.6 m | 22.6 m |
| Centreline | u + v = 0.5000 + k | u − v = 0.5000 + k |
| Streets per tile, spacing | 1, 181.0 m | 1, 181.0 m |
| Most lit diagonal of the crossing family | 0.51 of its length | 0.44 of its length |
| Centreline under a building, clear set / flagged set | 0.000 / 0.000 | 0.000 / 0.000 |
| Crossing diagonals no building touches, clear set | 3 of 256 (53 to 55) | 1 of 256 (249) |
| Crossing diagonals no building touches, flagged set | 0 of 256 | 0 of 256 |
| Least-covered crossing diagonal, both sets | 0.090 of its length | 0.090 of its length |

A kerb stays on one texel diagonal in every quarter of the tile, which pins it to 45 degrees within
atan(1/256), 0.22 degrees; the free fit's spread is its pick landing on neighbouring paint. A lamp
glow sits on the street once per tile.

**Lattice verdict.** A rotated lattice whose cell edges all lie on street centrelines needs two
street families; each diagonal paint has one, so no diamond lattice exists. Every crossing
diagonal runs through a building of the flagged set, so no cross cut avoids the buildings either.
A stripe lattice does exist: cells are the strips between consecutive centrelines, 181.0 m wide,
edges on the centrelines, which no footprint of either set touches. In UV the cell index is
floor(u + v − 0.5) for `cblock2` and floor(u − v − 0.5) for `cblock3`; a whole-tile shift moves it
by a whole number, so it repeats with the 256 m tile and a block crossing a tile edge keeps its
cell. On world XZ in the `U+X V+Z` frame with zero phase this is floor((x + z) / 256 − 0.5) and
floor((x − z) / 256 − 0.5). Along a strip the cell does not change, so the template still repeats
every 362 m (256 m times √2) along the street.

## The bare flagged ground

Flagged overlay with no decorating layer beneath it carries no buildings. As laid on the ground
(each texel weighted by how often the bare polygons show it) its free floor reads within 0.1 of
the per-tile numbers.

| Paint | Area (km²) | Polygons | Free floor luma mean, median (as laid) | At luma ≤ 5 | Widest free spot (tile) |
|---|---|---|---|---|---|
| `cblock1` | 3.906 | 66 | 6.0, 0 | 0.72 | 224 m |
| `cblock2` | 3.091 | 67 | 5.9, 0 | 0.74 | 158 m |
| `cblock3` | 3.524 | 68 | 4.3, 0 | 0.80 | 158 m |
| `cblock7` | 2.423 | 55 | 5.9, 0 | 0.72 | 220 m |

Where it lies, as the largest clusters of touching polygons (world XZ, metres):

| Paint | Cluster bounds (x; z) | Area (km²) |
|---|---|---|
| `cblock1` | −5632 to −4608; −13056 to −12288 | 0.393 |
| `cblock1` | −7168 to −6656; −11008 to −10240 | 0.328 |
| `cblock1` | −9216 to −8704; −14080 to −13568 | 0.262 |
| `cblock2` | −11152 to −10240; −2192 to −1024 | 0.401 |
| `cblock2` | −13097 to −12491; −13312 to −12032 | 0.322 |
| `cblock2` | −3472 to −2643; −5661 to −4864 | 0.225 |
| `cblock3` | −5120 to −3072; −4352 to −2104 | 1.325 |
| `cblock3` | −12853 to −11933; −14592 to −13312 | 0.437 |
| `cblock3` | −13187 to −12491; −10752 to −10240 | 0.172 |
| `cblock7` | −4096 to −2490; −5120 to −2167 | 1.053 |
| `cblock7` | −2048 to −1536; −12288 to −9290 | 0.769 |
| `cblock7` | −10240 to −7787; −2048 to −1229 | 0.563 |

The rest are clusters of one to eight polygons, most of one 256 m tile or less.

## Caveats (measured / inferred / assumed)

- Measured: every area, the per-tile numbers, the eight-frame test, the UV frames and phases, the
  kerbs and the footprint tests, all from shipped data.
- Measured, reproduced: §2's 32,435,893 and 11,281,871 m² and its bimodal 196-polygon,
  26,330,410 m² coverage, as the restrictions above select them.
- Inferred: the kerb-to-kerb band, read as the corridor lines at half the brightest line's mean
  luma or more, and checked on the street plots; and that the tile-edge seams are lot seams, read
  from the paint at the tile corners.
- Inferred: which layer is visible and which decorates, from `docs/org/clutter.md`'s `no_clutter`
  rule. Two coplanar unflagged polygons of different paint (0.008 km²) have no rule here.
- Inferred: that the 0.427 km² of clear `cblock7` in the 45 m and 128 m frames stamps its set in
  that frame; neither the original nor the engine was watched doing it.
- Assumed: the footprint is a building decoration's whole top-down shape without its `det` half,
  as the per-tile table has always used; B11's grown hull is larger.
- The per-tile free-floor numbers weight every texel of the tile alike. For the bare ground the
  as-laid weighting agrees within 0.1; for the other combinations it was not computed.
