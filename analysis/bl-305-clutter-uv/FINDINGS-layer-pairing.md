# The user's rule holds: C5's city is two coplanar layers and `no_clutter` selects between them

Companion to [`FINDINGS-flag-geometry.md`](FINDINGS-flag-geometry.md) and
[`FINDINGS-noclutter.md`](FINDINGS-noclutter.md), and to the plan's
§"⚠ At the controls, 2026-08-10". Instrument: [`layer_pairing.py`](layer_pairing.py), run as

```
python analysis/bl-305-clutter-uv/layer_pairing.py --extracted Z:/CSVM/extracted
```

**Does the data support the user's rule — "on the red areas cblock4, 5 and 6 are active, and on
green cblock1, 2, 3 and 7"? YES.** Not weakly, and not by averaging: a `cblock1/2/3/7` overlay
polygon that carries `no_clutter` is **1,037× more likely** to have an unflagged `cblock4/5/6`
polygon coplanar with it than one that does not, and the effect is present in all four overlay
textures with no exception in either direction.

## The contingency table

Every `cblock1/2/3/7` world polygon in C5, split by its `no_clutter` flag and by whether an
**unflagged `cblock4/5/6`** polygon lies coplanar with it. Cell values are **exact XZ
polygon-intersection area** — the covered area *is* the cell, so the four cells partition the
overlay area with no threshold applied at all.

| | base `cblock4/5/6` beneath | no base beneath | row total |
|---|---|---|---|
| **overlay FLAGGED `no_clutter`** | **25,524,629 m² (64.9 %)** | 13,831,347 m² (35.1 %) | 39,355,976 m² |
| **overlay CLEAR** | **7,517 m² (0.02 %)** | 141,652,679 m² (99.98 %) | 141,660,196 m² |
| column total | 25,532,146 m² | 155,484,026 m² | 181,016,172 m² |

The same table as polygon counts, at a ≥ 50 % coverage threshold:

| | base beneath | no base | row total | % with base |
|---|---|---|---|---|
| overlay FLAGGED | 178 | 274 | 452 | **39.4 %** |
| overlay CLEAR | **1** | 1,596 | 1,597 | **0.1 %** |

Odds ratio **1,036.8×**. One single clear overlay polygon in the whole chapter has a base under it
(a `cblock3` quad, 7,517 m², 0.05 % of the clear `cblock3` area).

Two facts fall out of the column totals that are worth stating on their own:

- **Every square metre of C5's `cblock4/5/6` base layer lies under a `cblock1/2/3/7` overlay** —
  exposed base area is **0 m² of 25,532,146 m²**. The base is never the visible ground anywhere.
- **99.97 % of that base sits under a FLAGGED overlay.** The base layer and the `no_clutter`
  flag are, to three significant figures, the same region of the map.

So the mechanism the hypothesis proposes is exactly the shape of the data: the flag marks where the
original's walk should *skip the overlay and stamp the layer underneath instead*. Where the overlay
is clear there is nothing underneath to fall through to, so the overlay's own templates stamp.

## Per overlay texture — it holds in all four, with one that is weaker

| texture | FLAGGED: area with base | CLEAR: area with base | FLAGGED n≥50 % | CLEAR n≥50 % |
|---|---|---|---|---|
| `cblock1` | 9,849,572 / 13,903,311 = **70.8 %** | 0 / 51,474,101 = **0.0 %** | 59/127 = 46.5 % | 0/255 = 0.0 % |
| `cblock2` | 8,521,414 / 11,612,436 = **73.4 %** | 0 / 9,490,325 = **0.0 %** | 65/132 = 49.2 % | 0/92 = 0.0 % |
| `cblock3` | 6,091,631 / 9,750,447 = **62.5 %** | 7,517 / 4,921,666 = **0.2 %** | 46/115 = 40.0 % | 1/52 = 1.9 % |
| `cblock7` | 1,062,012 / 4,089,784 = **26.0 %** | 0 / 75,774,105 = **0.0 %** | 8/78 = 10.3 % | 0/1198 = 0.0 % |

**`cblock7` is where it is weakest, and it does not break the rule — it weakens one half of it.**
The *clear* half is perfect for `cblock7`: zero of its 1,198 clear polygons and zero of its
75.8 million m² of clear area have a base beneath. The *flagged* half is where it thins: only 26 %
of `cblock7`'s flagged area has a base, against 62–73 % for `cblock1/2/3`. So on `cblock7`'s
flagged ground the original mostly stamps **nothing at all** rather than the low-rise district.
That is consistent with the user's report — `cblock7` is in their *green* group, and green is the
half `cblock7` satisfies exactly.

**Which base pairs with which overlay is strict**, and it reproduces `CBLOCK-LOD.md`'s 1↔4, 2↔5,
3↔6 pairing from the other direction: of the flagged overlays that found a partner, `cblock1` drew
`cblock4` 59 times (and `cblock5`/`cblock6` twice each), `cblock2` drew `cblock5` 64 times and
`cblock4` once, `cblock3` drew `cblock6` 46/46, and `cblock7` drew `cblock4` 8/8.

**The two layers are coincident, not stacked.** The signed Y difference (base minus overlay) is
`+0.000` for **all 183** paired polygons — min, median and max alike. Nothing is "underneath" in the
vertical sense; both layers sit at exactly Y = 5.0 and the render order is decided by `SubfaceBias`.
That does not weaken the reading — the clutter walk iterates polygons, not depth — but "the base
beneath" is a description of draw order, not of geometry, and the write-up should not imply a gap.

**Coverage is perfectly bimodal.** For flagged overlays the coverage percentiles are
p25 = 0 %, p50 = 0 %, p75 = 100 %, p95 = 100 % in `cblock1/2/3`: a flagged quad either has a base
covering it exactly or has none at all. So the count and area figures differ (39.4 % vs 64.9 %)
only because the paired quads are the larger ones, not because anything is partly covered.

## Where this leaves `FINDINGS-flag-geometry.md`

It is not contradicted. That document compared flagged against unflagged **within one texture** and
correctly found a side-by-side tiling with dY = 0 — the same-texture question has no stacking in it.
The pairing is entirely **cross-texture**, and nothing in that measurement could see it. Its
closing caveat ("true polygon-vs-polygon overlap — the instrument uses XZ bounding boxes, so the
≥ 50 % overlap figures are upper bounds") is discharged here: this instrument clips
triangle-against-triangle and uses bounding boxes only as a broad-phase filter.

## The map: which district would stamp under the original's rule

512 m cells over the whole `cblock` field, area distributed across the cells each polygon's
footprint covers — the same accounting `flag_geometry.py` uses, so the two maps overlay directly.

```
        210987654321098765432109876543210
  z= -16384 TTTTTTTT    TTTTTTTTTTL          'T' overlay CLEAR      -> cblock1/2/3/7 towers
  z= -15872 TTTTTTTT    TTTTTTTTTTL          'L' FLAGGED + base     -> cblock4/5/6 low-rise
  z= -15360 TTTTTTTx   LTTTTTTTTTTL          'x' FLAGGED, no base   -> nothing stamps
  z= -14848 TTTTTTTx   LTTTTTTTTTTL          'b' base, no overlay   -> (never occurs)
  z= -14336 TTTTTTTtxLLLTTxTTTTTTTL          lower case = winner holds <60% of the cell
  z= -13824 TTTTTTTxLLLLTTxTTTTTTTLL         '@' = BL-305's pose cell
  z= -13312 TTTTTTtLLLLLTTTTTTTTTTxLL  xTTTT
  z= -12800 TTTTTTxLLLLLTTTTTTTTTxLLL LLxTTT
  z= -12288 TTTTTTxLLLLLTTxTTTTTTTTTLLLLlTTT
  z= -11776 TTTTTTxLLLLLTTTxTTTTTTTTLLLLlTTT
  z= -11264 TTTTTTxTL TTTTTTTTtTxTTTLLLLlTTT
  z= -10752 TTTTTTtLL TTTTTTTTxTTTTTLLLLlTTT
  z= -10240 TTTTTTTxL LLTTTTtTtTtTTTLL LxTTT
  z=  -9728 TTTTTTTT  LLTTTxTtTTTTTTLLLltTTT
  z=  -9216 TTTTTTT    LTTTxTTTxTTLLL tTTTTT
  z=  -8704 TTTTTT     LTTTtTTTxTTLL   TTTTT
  z=  -8192 TTTTTT     TTTTTTTTTTTLL    TTTT
  z=  -7680 TTTTT     TTTTxTTTTTTT      TTTT
  z=  -7168 TTTTT     LLTTTTTTTTTTTT   TTTTT
  z=  -6656 TTTT      LLTTTTTTTTTTTTT  TTTTT
  z=  -6144          LTtTTTTTTTTTTLLl TTTTTT
  z=  -5632          LTTTTTTTTTTTTLLLxxTTTTT
  z=  -5120          LTTTTTTLLLLLLLLLxxTTTTT
  z=  -4608         LLTTTTTTLLLLLL   xxTTTTT
  z=  -4096        LLlLLTTLLLL     xxxTTTTTT
  z=  -3584        LLLLLL@LLLL  xxxxxTTTTTTT
  z=  -3072         LLLLLLLL    TTxxTTTTTTTT
  z=  -2560           LLxxtxxt  xTtxTTTTTTTT
  z=  -2048           TxxlTTTTTTTTTTTTTTTTTT
  z=  -1536           xtttTTTTTTTTTTTTTTTTTT
  z=  -1024           TTTTTTTTTTTTTTTTTTTTTT
  z=   -512           TTTTTTTTTTTTTTTTTTTTTT
```

- **By area: towers 141,660,196 m² (78.3 %) · low-rise 25,524,629 m² (14.1 %) · nothing
  13,831,347 m² (7.6 %) · bare base 0 m².**
- **By cell: T = 577, L = 148, x = 50** of 775 occupied cells.
- **`BL-305`'s pose cell (−19, −7) is 100 % `L`**: T = 0, L = 283,989 m², x = 0. Under the
  original's rule that pose stamps **the `cblock4/5/6` low-rise district and only that**. Our build
  stamps `cblock1/2/3` towers there, because it ignores the flag *and* exempts `cblock4/5/6` via
  `ClutterBuilder.BuriedClutterDistricts`. Too big and too dense is exactly the predicted symptom.

**Shape against `flag_geometry.py`'s flag map:** identical support, split in two. Every `#`/`+` cell
of that map is an `L` or an `x` here and every `.` cell is a `T` — as it must be, since `L ∪ x` is
the flagged set. The new information is the split: the big flagged blob at X[−12800, −5000]
Z[−5600, −2500] that contains the pose is almost entirely **`L`** (the low-rise district stamps
there), whereas the flagged patches at X ≈ −8000…−6500, Z ≈ −5600…−2000 and the scattered flagged
cells inside the western `cblock7` field are **`x`** — flagged with no base, so nothing stamps.
The 13.8 M m² of `x` is spread across all four textures (`cblock1` 4.05 M, `cblock3` 3.66 M,
`cblock2` 3.09 M, `cblock7` 3.03 M), not concentrated in one.

## The bridge

**Located, from the geometry, unambiguously.** C5 has a node literally named **`brooklynbridge`
(index 2299)**, reachable from the `world1` walk, spanning
**X[−10148, −10076] Y[5.0, 122.6] Z[−3700, −1423]** — a 2,277 m north–south span at X ≈ −10112,
rising to 122.6 m. Three `pedbridge1` footbridges also exist, at 268–330 m altitude, far from the
city ground.

`pier` did **not** locate it and would have misled: all 341 `pier`-textured world polygons have
**zero plan area** — they are vertical faces. `pier` is C5's quay/seawall texture, not bridge
decking.

North is **−Z** in this project (`docs/architecture.md`, "North = −Z is confirmed"), so
"north of the bridge" is the −Z side. What the rule predicts there, probed at 700 m radius:

| probe | what the rule says |
|---|---|
| at the bridge (−10112, −2562) | mixed, majority **`L`** — 8 flagged quads at 100 % base coverage (`cblock1/2/3` over `cblock4/5/6` in nodes `g4696`, `g4697`, `g4682`, `g4711`), 6 flagged with no base |
| **1000 m north (−10112, −3562)** | **`L`, overwhelmingly.** The four largest quads present (`g4683#7` 360 k m², `g4682#1` 524 k m², `g4682#0` 393 k m², `g4697#4` 307 k m²) are all flagged at 100 % base coverage. Five clear `cblock1` quads sit in the same window. |
| 2000 m north (−10112, −4562) | flips back to **`T`** — six clear `cblock1` quads, 917 k m² the largest; only two flagged quads, one with a base |
| 1000 m south (−10112, −1562) | `cblock7`/`cblock2` country: clear quads stamp towers, flagged ones stamp **nothing** (no base under any of them) |
| 1000 m west (−11112, −2562) | **entirely `L`** — every flagged quad in the window is at 100 % base coverage |
| 1000 m east (−9112, −2562) | mixed `L` and `x` |

**So the rule predicts the low-rise `cblock4/5/6` district immediately north of the bridge, out to
roughly 1.5 km, and towers again beyond that.** That is the user's report. It is a prediction from
static data at a located landmark, not a matched frame — nobody has yet put a camera there and
compared.

## Method, and what it cannot say

**The walk** mirrors `ClutterBuilder.PlaceOnWorld` field for field and is lifted from
[`flag_geometry.py`](flag_geometry.py): `world1`'s `child_indices` plus its partition grid's
referenced node indices, each subtree from identity; `WorldBuilder.SkipWorldNode`
(`horizon`/`dzpaths`/`fvol*`) and non-nearest `Lod` levels skipped, subtree included; the `active`
flag not checked; each node's local transform composed before recursing; **world space** throughout
(WORLD-15). `ClutterBuilder.BuriedClutterDistricts` is **not** applied — the question is what the
*original* would do, and the original registers all seven templates.

**True polygon overlap, not bounding boxes.** Every polygon is triangulated exactly as
`SceneBuilder.EmitPolygon` triangulates it (fan, or strip when `tri_strip` is set — a strip's raw
index list is not an outline), projected to XZ, and clipped triangle-against-triangle with
Sutherland–Hodgman. Bounding boxes are used only as a broad-phase filter. Multiple bases covering
one overlay are capped at the overlay's own area, so nothing double-counts.

**Self-checks (METHOD-9), all passing before any measurement ran.** Seven cases, four of which are
able-to-fail controls:

| case | required | got |
|---|---|---|
| coincident overlay/base, base 1 m below | coverage 1.0, dY = −1 | 1.0000, −1.000 |
| deliberately disjoint pair | no partner | coverage 0, partners 0 |
| edge-touching pair (blocks side by side) | zero | 0.000000 |
| **AABB-coincident, polygon-disjoint** (two opposite-corner triangles of one square) | **near zero** | **0.000000 — an AABB test would report 1.0 here** |
| base covering exactly half | 0.5 | 0.500000 |
| 45° diamond inscribed in the overlay | 0.5 | 0.500000 |
| two abutting bases, no double count | 1.0 | 1.000000 |

The fourth is the one that matters: it is precisely the case where an AABB instrument confirms the
hypothesis out of a tiling. `flag_geometry.py` flagged its own AABB use as a superset; that
weakness is removed rather than inherited.

**Diagnostics (DIAG-15).** 2,214 `cblock1`–`cblock7` polygons visited, 2,214 records (2,049 overlay
of which 452 flagged, 165 base of which 1 flagged). Dropped: 0 with < 3 vertices, 0 with an
out-of-range vertex index, 0 non-finite. 0 reachable twice. 0 matched a target texture on two
material layers. **5 triangle-strip polygons**, triangulated as strips. **22 degenerate triangles**
(zero XZ area) dropped from footprints, and **7 polygons whose total XZ area is zero** — vertical
`cblock` faces, kept in the run and reported rather than dropped; they cannot overlap anything in
plan projection by construction. **1 base polygon carries `no_clutter` itself** (`cblock4`
`g4675[1820]#9`, 16,384 m², Y = 5.0) and is excluded from the "unflagged base beneath" test, as the
question requires.

**What this cannot determine.**

- **That the original actually stamps `cblock4/5/6` there.** This measures geometry and flags. It
  shows the original's rule *has something to fall through to* exactly where the user says the
  low-rise buildings are, and *nothing* where they say the towers are. The final word is a matched
  frame against the recordings, or a `--clutter-templates=cblock4,cblock5,cblock6` render at the
  bridge compared with the user's footage.
- **What a `cb12a`+ instance looks like relative to a `cb00a`+ one.** "Smaller" is the user's
  observation; no height comparison is made here.
- **The 35 % of flagged overlay area with no base (13.8 M m², the `x` cells).** Under this rule the
  original stamps nothing there. Whether that is right, or whether some third mechanism fills it,
  is untested — and it is 7.6 % of C5's ground.
- **The exact northern extent of the low-rise district.** The 1 km / 2 km probes bracket the flip
  between roughly 0.9 and 1.9 km north of the bridge's centreline; nothing finer was measured.

---

*Authored by the layer-pairing subagent; saved to disk by the orchestrator, verbatim. The
subagent's own Write access was refused — the fifth time this session.*
