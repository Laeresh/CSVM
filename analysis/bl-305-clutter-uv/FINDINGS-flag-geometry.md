# Where the unflagged `cblock*` polygons are: neither (A) nor (B)

Companion to the plan's §"The census confirms the decode" and to
[`FINDINGS-noclutter.md`](FINDINGS-noclutter.md). Instrument:
[`flag_geometry.py`](flag_geometry.py), run as

```
python analysis/bl-305-clutter-uv/flag_geometry.py --extracted Z:/CSVM/extracted
```

**The answer is (C).** The unflagged `cblock*` polygons are neither coplanar underneath the flagged
ones nor spatially disjoint from them. They are **the same single ground plane, tiled side by side**:
every one of C5's 2,049 `cblock1/2/3/7` world polygons — flagged and unflagged alike — sits at
Y ≈ 5.0, and the `no_clutter` flag varies **from tile to tile, in districts**, across one connected
16.4 × 16.4 km city.

And the headline: **the flag is nowhere near uniform. 21.6 % of the field's area is flagged; 497 of
the 775 occupied 512 m cells are less than 5 % flagged, while 168 are 90 %+ flagged. `BL-305`'s pose
sits inside one of the few solid 90 %+ blobs.** The "the original has towers where the original
would stamp nothing" contradiction is therefore very likely an artefact of comparing two different
places.

## 1. Answer (A) is dead: there is no Y separation to find — **[measured]**

(A) predicted "same XZ, a small Y below, same texture". Y first, because it needs no overlap test:

| texture | flagged Y (min–max, distinct values) | unflagged Y |
|---|---|---|
| `cblock1` | 5.000 – 5.000, **1** | 5.000 – 5.000, **1** |
| `cblock2` | 4.000 – 5.000, 2 | 5.000 – 5.000, **1** |
| `cblock3` | 3.333 – 10.000, 4 | 5.000 – 5.000, **1** |
| `cblock7` | 5.000 – 5.000, **1** | 2.500 – 5.000, 2 |

Both sets occupy one plane. The handful of off-5.0 values are all on the *flagged* side except
`cblock7`'s 2.5, i.e. the deviation runs the wrong way for an underlay, and it is a few polygons out
of hundreds.

Where a flagged polygon does have an overlapping unflagged neighbour, the **signed Y difference is
`+0.00` in every single case** — 82/82 for `cblock1`, 38/38 for `cblock2`, 31/32 for `cblock3`
(one at +0.62), 64/64 for `cblock7`. Not one partner below. (A) requires a consistent offset of one
sign; the measured offset is exactly zero.

The XZ overlap agrees. Scoring each flagged polygon against the strongest same-texture unflagged
partner (largest XZ bounding-box intersection):

| texture | bbox contact incl. edge-only | non-zero intersection | **intersection ≥ 50 % of the flagged quad** | bbox coincident to 1 cm |
|---|---|---|---|---|
| `cblock1` | 64.6 % | 12.6 % | **1.6 %** | 0.0 % |
| `cblock2` | 28.8 % | 12.1 % | **4.5 %** | 0.0 % |
| `cblock3` | 27.8 % | 20.0 % | **13.0 %** | 0.9 % |
| `cblock7` | 82.1 % | 56.4 % | **30.8 %** | 19.2 % |

The "contact" column is the trap: two city blocks laid side by side on one plane share an edge and
score as contact with **zero** intersection area. The median partner covers 0 % of the flagged quad
for `cblock1` and `cblock2`, 43 % for `cblock3`, 12 % for `cblock7`. That is a tessellation, not a
stack.

`cblock7` is again the odd one — 30.8 % substantial overlap and 19.2 % bbox-coincident, an order of
magnitude above `cblock1`. It is the one texture where something like (A) exists for a minority of
faces. **Do not average the four.**

*Instrument caveat:* the overlap test uses axis-aligned XZ **bounding boxes**, which are a superset
of the true footprint. So "no overlap" is a sound negative, and the small ≥50 % figures above are, if
anything, generous. A quad's true polygon overlap can only be smaller.

## 2. Answer (B) is also wrong: the sets are interleaved, not disjoint — **[measured]**

| texture | flagged bbox / centroid | unflagged bbox / centroid |
|---|---|---|
| `cblock1` | X[−13177, −2048] Z[−16384, 0] · (−7856, 5.0, −9207) | X[−16384, −2048] Z[−16384, −256] · (−8007, 5.0, −10093) |
| `cblock2` | X[−13150, −1536] Z[−15360, −1024] · (−7694, 5.0, −7303) | X[−15616, −1536] Z[−16384, −1472] · (−7918, 5.0, −10251) |
| `cblock3` | X[−13187, −2048] Z[−14592, −1792] · (−8185, 5.0, −8643) | X[−13312, −1536] Z[−16128, −256] · (−6370, 5.0, −9739) |
| `cblock7` | X[−10240, −1536] Z[−12288, −1152] · (−4097, 5.0, −6486) | X[−16384, 0] Z[−16384, 0] · (−7752, 5.0, −7236) |

No pair of set bounding boxes is disjoint, and the centroids sit within ~3 km of each other in a
16 km field. Treated as one field, the flagged and unflagged `cblock` polygons form **a single
8-connected district** spanning the whole X[−16384, 0] Z[−16384, 0] map.

## 3. The map: the flag is a district property — **[measured]**

512 m cells, each polygon's XZ area distributed across the cells its footprint covers. Character =
**flagged** share of that cell's `cblock` area; `@` is the pose's cell.

```
        210987654321098765432109876543210      '#' >=90%  '+' 60-90%  'o' 30-60%
  z= -16384 ........    ..........#             '-' 5-30%  '.' <5%    ' ' none
  z= -15872 ........    ..........#
  z= -15360 .......+   #..........#
  z= -14848 .......+   #..........#
  z= -14336 .......o+###..o.......#
  z= -13824 .......o####..o.......##
  z= -13312 ......o#####..-.......###  #-...
  z= -12800 ......o#####.........#### ###...
  z= -12288 ......+#####..o-....--..#####...
  z= -11776 ......+#####..-+....--..#####...
  z= -11264 ......+-# ....----o-o-..#####...
  z= -10752 ......o## ....----+.--..#####...
  z= -10240 .......o# ##..--o.o-o...## ##...
  z=  -9728 ........  ##...o-o--o-..####o...
  z=  -9216 .......    #...o.-.o..### oo....
  z=  -8704 ......     #..-o.-.o..##   .....
  z=  -8192 ......     ....o......##    ....
  z=  -7680 .....     ....#.......      ....
  z=  -7168 .....     ##.-..........   .....
  z=  -6656 ....      ##..-..........  .....
  z=  -6144          #.o..........##+ -.....
  z=  -5632          #..--........####+.....
  z=  -5120          #.-....##########+.....
  z=  -4608         ##-.....######   #+.....
  z=  -4096        #####.-####     ###-.....
  z=  -3584        ######@####  #+###oo.....
  z=  -3072         ########    .-##-.--....
  z=  -2560           ####o+#o  +.-+---.....
  z=  -2048           o#+o.oo-..-...........
  z=  -1536           +oooooo...............
  z=  -1024           o.....................
  z=   -512           o.....................
```

- **775 occupied cells. Flagged area 38,960,572 m² = 21.6 % of 180,570,997 m².**
- Cells by flagged share: **≥90 % → 168 · 60–90 % → 18 · 30–60 % → 42 · 5–30 % → 50 · <5 % → 497.**
  A strongly bimodal distribution: a cell is usually either almost entirely flagged or almost
  entirely not. **The `no_clutter` attribute was authored per district, not per face.**
- **`BL-305`'s pose (−9490, −3300) is at 42 % across X and 80 % across Z of the field, in cell
  (−19, −7) — the middle of the solid `#` blob that runs roughly X[−12800, −5000] Z[−5600, −2500].**
  It is one of the largest fully-flagged regions on the map, and it is not the geometric centre of
  the city.
- Large, entirely unstamped-in-our-build districts exist well within a short flight of it — e.g. the
  cell centred (−8448, −7424) is <5 % flagged and 4.3 km away; the whole band z ≈ −6000 … −12000,
  x ≈ −8000 … −3000 reads `.` and `-`.

### Per texture, the districts differ

| texture | occupied cells | flagged share of area | districts (8-connected) | note |
|---|---|---|---|---|
| `cblock1` | 347 | 21.3 % | 8 | main district 320 cells, 21.5 % flagged, contains the pose |
| `cblock2` | 179 | 54.9 % | 24 | pose district 40 cells, 66.9 % flagged; a 9-cell 0 %-flagged district at X[−7168, −5120] Z[−16384, −14848] |
| `cblock3` | 116 | 65.6 % | 20 | largest district 27 cells at X[−6144, −2048] Z[−14336, −9216], 74.8 % flagged; a 10-cell district at X[−6144, −3584] Z[−7168, −5632] is only 4.2 % flagged |
| `cblock7` | 329 | **5.1 %** | 3 | **a 123-cell district, X[−16384, −12288] Z[−16384, −6144], 30.4 million m², 0.0 % flagged** |

`cblock7`'s western district is the single biggest fact here: 30 km² of clutter-eligible ground on
which the original's walk is not blocked by anything, nowhere near the pose. `cblock2` and `cblock3`
are majority-flagged textures; `cblock1` and `cblock7` are minority-flagged. The four do not behave
alike and a conclusion from any one of them would have been wrong.

## 4. What is actually at `BL-305`'s pose — **[measured]**

Everything of these textures within 600 m of (−9490, −3300):

```
    dist  flag  texture   node                y_mean    xzArea  centroid
       0  F     cblock1   g4683[1777]#6          5.0     32768  (-9515, -3413)
       0  F     cblock1   g4683[1777]#7          5.0    360448  (-9888, -3328)
      18  F     cblock2   g4683[1777]#0          5.0    131072  (-9344, -3328)
     228  F     cblock1   g4697[1800]#4          5.0    307200  (-9856, -2872)
     229  F     cblock2   g4697[1800]#2          5.0     69632  (-9344, -2936)
     274  F     cblock2   g4684[1813]#4          5.0    655360  (-8768, -3456)
     284  F     cblock1   g4683[1777]#2          5.0     16384  (-9536, -3648)
     285  .     cblock1   g4683[1777]#5          5.0     81920  (-9344, -3744)
     305  .     cblock1   g4683[1777]#4          5.0    204800  (-9920, -3744)
     356  F     cblock1   g4698[1799]#3          5.0    110592  (-8832, -3000)
     395  F     cblock2   g4684[1813]#3          5.0    262144  (-8875, -3925)
     412  .     cblock1   g4683[1777]#3          5.0    221184  (-9632, -3904)
```

Twelve polygons: **9 flagged, 3 unflagged, all at Y = 5.0, no `cblock3` and no `cblock7` at all.**
The pose stands directly on two flagged `cblock1` quads. The nearest unflagged quad is **285 m
away** — and at 230 m altitude looking straight down, that is outside the frame. So the debug
recolour's "100 % of the visible ground is flagged" and the census's "102 flagged / 256 unflagged
`cblock1` polygons" are both correct and not in tension: **the unflagged ones are real, nearby, and
simply out of shot.**

Note also that `g4683[1777]` owns both flagged and unflagged `cblock1` quads. The flag is not a
whole-node property either.

## 5. Was the comparison ever the same place? — **[inferred]**

`playtest/CAP-22/README.md` claims only a **scale** match for
`ours-nadir-230-scale-matched.png`; position was never matched or asserted, and the original's stills
come from a nadir chase-cam wherever the mission path took the aircraft. Section 3 shows the flagged
fraction swinging from 0 % to 100 % over a few hundred metres. **Two nadir frames a kilometre apart
in C5 can legitimately disagree about whether the ground below is clutter-bearing**, so the observed
contradiction is fully explained by a position mismatch without anything in the decode being wrong.

This is not proof that the frames differ — nobody has located the original's frame in world
coordinates. It is a demonstration that the contradiction **does not require a bug**. Locating the
original's nadir still on this map is now the cheapest way to close the question; the map gives it a
target, since the original's frame must land on a `.`/`-` cell if its towers are clutter.

## 6. Two cheap extras — **[measured]**

**Layer structure differs, sharply.** Flagged polygons carry a second material layer far more often
than unflagged ones:

| texture | flagged 2-layer | unflagged 2-layer |
|---|---|---|
| `cblock1` | 13 / 127 = 10.2 % | 0 / 255 = 0.0 % |
| `cblock2` | 25 / 132 = 18.9 % | 0 / 92 = 0.0 % |
| `cblock3` | 24 / 115 = 20.9 % | 0 / 52 = 0.0 % |
| `cblock7` | **51 / 78 = 65.4 %** | 15 / 1198 = 1.3 % |

The matching `cblock*` texture is at layer position 0 in **all 2,049** records, so the second layer
is always an overlay pass on top of the block texture, never the block itself. This is a genuine
structural correlate of the flag — the flagged set is where the artists put decals.

**`priority` is enriched in the flagged set but is not the same field.** Only two values occur on
`cblock*` polygons, `0` and `-10`:

| texture | flagged `priority = -10` | unflagged `priority = -10` |
|---|---|---|
| `cblock1` | 28 / 127 = 22 % | 1 / 255 = 0.4 % |
| `cblock2` | 59 / 132 = 45 % | 0 / 92 = 0 % |
| `cblock3` | 34 / 115 = 30 % | 3 / 52 = 5.8 % |
| `cblock7` | 34 / 78 = 44 % | 48 / 1198 = 4.0 % |

Consistent with the hypothesis that subface lives in `priority`: `priority = -10` is 5–100× more
common on flagged faces, yet it is a **minority** of them and appears on unflagged faces too. So
`unk3` and `priority` are correlated but distinct — which is exactly what "two different things this
project has been conflating" predicts. Whether −10 is the `GameGenSetSubfacePriorityOffset`
accumulation is not testable from this data.

## 7. Method and what it cannot say

The walk mirrors `ClutterBuilder.PlaceOnWorld` (`Clutter.cs:635-667`) field for field, lifted from
[`uv_repeat.py`](uv_repeat.py): `world1`'s `child_indices` plus its partition grid's referenced
nodes, each from identity; `SkipWorldNode` (`horizon`/`dzpaths`/`fvol*`) and non-nearest `Lod`
levels skipped, subtree included; each node's local transform composed before recursing; the
`active` flag not checked, because `PlaceOnWorld` does not check it. Positions are **world space**
(WORLD-15) — the whole question is positional and a local-space answer would have been plausible and
wrong. Texture is read **per material layer**, not layer 0 unconditionally.

**Diagnostics (DIAG-15).** 2,049 target-texture polygons visited, 2,049 records (no polygon matched
on two layers at once). Dropped: 0 with <3 vertices, 0 with an out-of-range vertex index, 0
non-finite. 0 polygons were reachable twice through the two root lists. **7 polygons have zero XZ
area** — vertical `cblock*` faces, projecting to a line from above; they are kept in the overlap test
(they still have a real XZ bounding box) and counted here rather than dropped.

**Self-checks (METHOD-9), all passing before any measurement ran:** a synthetic coplanar pair with a
known −1.0 m offset is detected with `dY = −1.000` and an intersection area of 10,000 m²; a
deliberately disjoint pair is reported disjoint; a pair 1 m beyond the margin is reported disjoint;
an edge-touching pair is reported as zero-area contact rather than as an overlap. Without the last
two the instrument would have manufactured answer (A) out of a tiling — which, given that "bbox
contact" reached 82 % for `cblock7`, it very nearly did.

**What this cannot determine.**

- Where the original's nadir still actually is in world coordinates. Everything in §5 is a
  demonstration that the contradiction is *dissolvable*, not that it is dissolved.
- Whether the towers at the original's frame are clutter at all. This measures ground polygons and
  their flags; it says nothing about what template got stamped or what a `cb**a` instance looks like.
- True polygon-vs-polygon overlap. The instrument uses XZ bounding boxes, so the ≥50 % overlap
  figures are upper bounds.
- Whether `priority = -10` is the subface concept. Correlation only.

---

*Authored by the flag-geometry subagent; saved to disk by the orchestrator, verbatim apart from
decoding the HTML entities its transport introduced. The subagent's own Write access was refused —
the fourth time in this session.*
