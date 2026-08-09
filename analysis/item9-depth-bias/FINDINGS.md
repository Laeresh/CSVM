# Polish-4 item 9 — conflict-local depth bias: data-only evaluation

**Read-only analysis, 2026-07-22.** No Godot was launched, nothing was built, nothing under
`CSVM/src` was modified. Every number here comes from `extracted/**` JSON plus a software
re-implementation of `SceneBuilder`'s depth maths. Scripts are in `.scratch/` (listed at the end).

Claims are labelled **[measured]** (computed from the shipped data), **[inferred]** (a
conclusion drawn from measured values plus code reading), or **[assumed]**.

---

## Verdict

**The recorded direction is VIABLE-WITH-MODIFICATION for C1B and STRUCTURALLY INCAPABLE for
C5 — because the C5 repro's mechanism has been misidentified for a fourth time.**

Three separate verdicts, in order of importance:

1. **The premise this item inherited is disproven.** "Nine coplanar World-child nodes stack
   at y = 5 in the C5 repro footprint" is another AABB reading (`verification.md` rule 9,
   for the second time on this same bug). The nine are *exactly* coplanar and *exactly*
   same-priority, and they **tile** — zero overlapping area between any pair, by two
   independent methods. **[measured]**

2. **The C5 repro's real conflict is inside one node**, between two of its own Godot
   surfaces. A per-node bias — dense, sparse, or otherwise — is one scalar per
   `MeshInstance3D` and cancels exactly there. The proposed direction cannot touch it.
   **[measured + inferred]**

3. **The C1B repro's real conflict is cross-node and is a single pair**, separated today by
   1.35× the claimed resolution floor. This is the half the dense-rank direction fixes, and
   the arithmetic budget is comfortable. **[measured]**

The two recorded poses are **two different bugs**. That is why every previous single fix
moved one and not the other.

---

## 1. The inherited premise: disproven

Node 1777 `g4683` plus 1799, 1800, 1801, 1813, 1814, 1822, 1823, 1837 — the "nine coplanar
World-child nodes". **[all measured]**

- They are coplanar to machine precision: over the 61 y = 5 polygons across the nine, the set
  of distinct `|n.y|` is `{1.0}` and the set of distinct `|d|` is `{5.0}`. So "coplanar" is
  **exactly** well-defined here, not a grazing-angle near-coplanarity. (That answers the
  task's question 2: a static rank is not disqualified by view-dependence.)
- **True polygon∩polygon area between them: 0.00 m².** 15 (plane, priority) buckets are
  shared by two or more of the nine; every cross-node pair in every one of them clips to zero
  (Sutherland-Hodgman, triangles). 36 priority-0 pairs and 6 priority-(−10) pairs, all
  abutting or disjoint.
- Independent cross-check by rasterisation: 8 m cells over the priority-0 y = 5 footprint —
  **114,095 covered cells, 0 covered by more than one of the nine (0.00%)**.
- Their footprints are a clean 1024 × 1024 tile grid (e.g. 1799 x[−9216,−8192] z[−3072,−2048],
  1800 x[−10240,−9216] z[−3072,−2048], 1801 x[−11264,−10240] z[−3072,−2048]).

Only `g4683` itself has an AABB that spans several tiles (x[−10240,−8192] z[−14336,−3072]),
which is where the "stack" reading came from. Its actual y = 5 outlines do not overlap them.

> **Rule 9 has now cost this bug two wrong diagnoses.** Recommend adding to
> `docs/verification.md` rule 9: *the same substitution has now produced two different wrong
> answers on the same bug — an AABB is not an overlap even when you already know that.*

**A second instrument bug worth recording** (`verification.md` §4 material): my own first pass
used each polygon's raw `vertex_indices` list as an outline. That is wrong for every
`tri_strip` polygon — a strip's index list is not a polygon and its Newell normal is
meaningless — and it manufactured a bogus plane `y = 5` for a 16-index strip box, "proving"
that node 1777 overlapped buildings 2 km away by 6.8 million m². The two superseded scripts
are kept in `.scratch/` with that header. **All numbers in this document use triangles,
triangulated exactly as `SceneBuilder.EmitPolygon` does.**

---

## 2. What actually fights, at both recorded poses

`.scratch/item9_depthprobe.py` reimplements the render path in software: `Camera3D { Fov = 50,
Far = 40000 }`, `VERTEX *= 1.0 - (depth_bias + node_bias)` in view space, `depth_bias =
clamp(priority × 2e-4, ±0.05) + min(rank,5) × 2e-6`, `node_bias = node.Index × 5e-8`, one
surface id per (node, material, priority). It reports, per pixel, the frontmost surface and
the nearest *different* surface behind it, and how close they are as a fraction of view
distance. No coplanarity assumption is involved — it just asks what the depth buffer sees.

> **⚠ It had a hole first, and the hole is instructive.** The first version *culled* triangles
> crossing the near plane instead of clipping them, which silently dropped exactly the large
> ground quads the camera is standing on. It reported "minimum relative gap 0.002, nothing is
> fighting" — a clean, precise, entirely wrong answer of the same shape as the three previous
> diagnoses. Clipping was added; every number below is post-fix.

### C5, `--campos=-9533.178,76.319,-3367.413 --lookat=-9451.281,28.148,-3398.597` **[measured]**

| separation of the two nearest surfaces | fraction of frame |
|---|---|
| < 1e-6 of view distance | 0.00% |
| < 3e-6 | **78.14%** |

All 78.14% is **one node fighting itself**:

| px | front | behind | why they separate |
|---|---|---|---|
| 144,801 (62.8%) | `g4683` / `cblock4.tif` rank 1 | `g4683` / `cblock2.tif` rank 0 | **same node**, 1 surface-rank step = **2.0e-6** |
| 35,227 (15.3%) | `g4683` / `cblock1.tif` rank 2 | `g4683` / `cblock4.tif` rank 1 | **same node**, 1 rank step = **2.0e-6** |

Same node, same priority 0, different materials → different Godot surfaces → different
`depth_bias`, identical `node_bias`. **`SurfaceRankBias` (2e-6) is the entire separation.**

Control with bias switched off: 94.80% of the frame coincident, same pairs — so the probe
demonstrably fires (`verification.md` rule 5).

### C1B, `--campos=-7698.844,48.763,-5797.924 --lookat=-7749.957,-20.093,-5849.367` **[measured]**

| separation | fraction of frame |
|---|---|
| < 1e-6 | 0.00% |
| < 3e-6 | **36.33%** |

All 36.33% is **one cross-node pair**:

| px | front | behind | why they separate |
|---|---|---|---|
| 83,714 (36.33%) | node 743 `g28169` / `wtr00000.tif` | node 716 `g28170` / `srf0001.tif` | **cross-node**, index delta 27 → `node_bias` delta **1.35e-6** = 1.35× the floor |

Both priority 0, both surface rank 0. Nothing else at that pose is within 1e-5.

### The instrument predicts the one control that was actually measured in Godot

Polish-3 measured `NodeOrderBias` 5e-8 → 2e-6 and got a result nobody could explain:
**C1B 30.95% → 2.35%, C5 35.96% → 41.69% (worse)**. Running that same change through the
probe, from the data alone: **[measured]**

| | probe, today | probe, NodeOrderBias 2e-6 | Godot, measured |
|---|---|---|---|
| C1B | 36.33% | **0.00%** | 30.95% → 2.35% |
| C5 | 78.14% | **78.14%** (untouched) | 35.96% → 41.69% |

Both halves fall straight out of the mechanism: C1B's conflict is cross-node so a 40× bigger
node step fixes it; C5's is same-node so `node_bias` cancels and cannot move it at all. This
is the first account that explains *both* halves of that control with one mechanism, and it
was derived without running anything. **[inferred]** The probe does not predict the *worse*
part (41.69%) — that needs the jitter response and any new conflicts created elsewhere, which
it does not model.

Correlation with the measured flicker rate is good but not 1:1 (C1B 36.33% predicted vs 30.95%
measured; C5 78.14% vs 35.96%). Treat the probe's number as "fraction of the frame at risk",
not as a flicker prediction.

---

## 3. The task's question 1: the rank-slot arithmetic

`node_bias` is one scalar per instance, so a "dense rank within a coplanar group" still has to
produce a **single global number per node**. Preserving the authored order (`nodes.json` DFS
order = draw order, later wins) means for every conflicting pair `a < b`, `rank(a) < rank(b)`.
That makes the conflict relation a DAG whose edges always run low index → high index, and the
number of slots required is its **longest path**, not the largest group.

Conflict = same priority, exactly coplanar in world space, non-zero true triangle∩triangle
area (> 1 m²). **[all measured]**

| chapter | built nodes | triangles | conflicting node pairs | components | largest component | **longest chain** |
|---|---|---|---|---|---|---|
| C1 | 6253 | 75,746 | 2,491 | 477 | 44 | **27** |
| C1B | 4903 | 34,804 | 1,898 | 282 | 53 | **11** |
| C1C | 4983 | 37,425 | 1,759 | 397 | 79 | **16** |
| C2 | 3925 | 49,362 | 1,960 | 380 | 56 | **16** |
| C2B | 4276 | 30,382 | 1,799 | 274 | 53 | **10** |
| C3 | 4419 | 52,400 | 1,484 | 519 | 53 | **13** |
| C4 | 7153 | 84,966 | 3,385 | 483 | 83 | **20** |
| C5 | 9363 | 121,193 | 6,567 | 681 | 128 | **28** |

Excluding the entities parked at the world origin (polish-4 item 4's `HideUnplacedEntities`
population — 12 zeppelins plus the Spruce Goose and the autogyro bus interpenetrating each
other at the map corner) the chains fall to **8 / 3 / 0 / 5 / 0 / 5 / 5 / 3**. Those
components are dominated by `structureleft`/`structureright` zeppelin panels and are an
artifact of unplaced entities, not authored layering.

**So: 200 slots budgeted, 28 required. The approach does not fail on its own arithmetic.**
**[measured]** That is the clean answer to the question asked — but see the caveat immediately
below, which is more important than the answer.

### ⚠ The "1e-6 floor" is not a safe denominator, and 2e-6 is a live counterexample

The 200-slot budget is `DepthBiasPerLevel / floor = 2e-4 / 1e-6`. **The 1e-6 figure does not
survive contact with the C5 measurement.** `SurfaceRankBias` = 2e-6 — twice the claimed floor —
is *exactly* the separation on 78% of the C5 repro frame, and that frame measures 35.96%
flicker. A 2× margin over the floor demonstrably does not stop the artifact. **[measured]**

The polish-3 bracket that produced "≈1e-6" used a **cumulative per-polygon ramp**, whose
effective per-pair delta was roughly an order of magnitude larger than its step (`g4683`'s
conflicting surfaces are ~10 polygons apart in its list). So that bracket measured where a
*cumulative* ramp starts to bite, not the per-pair step a tie-break needs. **[inferred]**

The needed per-pair step is therefore **> 2e-6 and not yet bracketed**. Slots available:

| per-pair step | slots per priority level | 28 node ranks + 5 surface ranks | fits? |
|---|---|---|---|
| 2e-6 (today; known insufficient) | 100 | 6.6e-5 | yes, but ineffective |
| 4e-6 | 50 | 1.32e-4 | yes |
| 5e-6 | 40 | 1.65e-4 | yes |
| **1e-5** | **20** | **3.30e-4** | **NO — over budget** |
| 2e-5 | 10 | 6.60e-4 | no |

**The arithmetic verdict is conditional on a number nobody has measured.** If the required
step turns out to be ≥ 1e-5, the dense-rank scheme *does* fail on its own arithmetic for C5
and C1 (chains 28 and 27) and only survives if the origin-parked pile is excluded from the
graph (chains 8 and 8, which fit at 1e-5 → 20 slots). **Bracketing the per-pair step is the
first thing anyone picking this up should do**, and it is cheap: change one constant, re-shoot
the two poses. Everything else here is downstream of it.

---

## 4. Paper test of candidate schemes

Same probe, three candidate schemes plus the validated control. **[measured — as probe
statistics, NOT as Godot flicker rates]**

| scheme | C5 frame < 3e-6 | C5 < 1e-5 | C1B < 3e-6 | C1B < 1e-5 |
|---|---|---|---|---|
| 0 today (`SurfaceRankBias` 2e-6, `node_bias` = index × 5e-8) | 78.14% | 78.14% | 36.33% | 36.33% |
| 1 `SurfaceRankBias` 2e-6 → **2e-5** | **0.00%** | **0.00%** | 36.33% | 36.33% |
| 2 dense node rank × 5e-6 | 78.14% | 78.14% | **0.00%** | 36.33% |
| 3 both | **0.00%** | **0.00%** | **0.00%** | 36.33% |
| 4 control `NodeOrderBias` 2e-6 (measured in Godot) | 78.14% | 78.14% | 0.00% | 0.00% |

- **Scheme 1 fixes C5 and does nothing for C1B.** Budget: rank cap 5 × 2e-5 = 1e-4 = half a
  priority level, so it stays inside the hierarchy.
- **Scheme 2 fixes C1B and does nothing for C5**, and only up to 3e-6 (its step is 5e-6; to
  clear 1e-5 it needs a bigger step, which the chain length may not afford — see above).
- **Neither alone covers both poses. They are two independent defects.**

---

## 5. Question 4: the recorded regressions

- **A dense conflict rank cannot invert authored layering by construction.** Every edge runs
  low node index → high node index, so the layering is a topological order of the DAG and any
  longest-path layering preserves it. C4's `g1612` (flat index 1227, a 477 m cliff,
  y ∈ [573, 1050]) and C1's `a6` (flat index 4046, the airfield tile) cannot be demoted
  relative to anything they conflict with. **[inferred, from the construction]**
- **C1C is inert:** 0 conflicting node pairs once the origin-parked pile is excluded.
  **[measured]**
- **The `fvol*` caution is real and bigger than stated:** C1 has 66 World children, 10 of which
  carry a mesh — and **9 of those 10 are `fvol1..fvol9` fog volumes**. `a6` is the only real
  one. Any World-child heuristic is operating on a set that is 90% fog volume. **[measured]**
- **Approximate winner-inversion counts** over cross-node conflicting pairs (does the higher
  node index still end up in front?). Approximate because it uses each node's max surface rank
  at that priority rather than the specific conflicting surface: **[measured, approximate]**

| chapter | cross-node pairs | inverted today | inverted with `SurfaceRankBias` 2e-5 | inverted with dense rank × 5e-6 |
|---|---|---|---|---|
| C1 | 2491 | 393 | 456 | **64** |
| C1B | 1898 | 58 | 62 | **0** |
| C2 | 1960 | 191 | 352 | **8** |
| C5 | 6567 | 261 | 293 | **69** |

  Two readings. (a) **Today already gets 2–16% of cross-node conflicting pairs the wrong way
  round**, because within-mesh surface rank can out-bid the cross-node term. (b) Scheme 1
  makes that worse, scheme 2 makes it markedly better. If both land, scheme 2 should land
  first or they should be sized together.

---

## 6. Two findings not asked for, worth recording

- **`node_bias = node.Index × 5e-8` already spans 1.22–2.86 priority levels per chapter**
  (C1 1.77, C1B 1.40, C1C 1.41, C2 1.24, C2B 1.22, C3 1.35, C4 2.07, **C5 2.86**).
  **[measured]** `docs/architecture.md` records this as an accepted limit phrased as "a prio-0
  node >~4000 indices later can out-bias a prio-1 overlay; no such pair observed in C1" — the
  real span is up to nearly three levels, and it is not a corner case, it is the normal state
  of the chapter. A dense rank fixes this as a side effect (28 × 5e-6 = 1.4e-4 = 0.7 levels).
- **`SurfaceRankCap` = 5 produces genuine zero-separation pairs.** 0.0–4.1% of built meshes per
  chapter have more than 6 (material, priority) groups, so ranks 5+ collapse onto one bias; the
  worst mesh has **32** groups (C4). In C5 that yields 4 measured coplanar overlapping pairs
  with *exactly zero* separation, totalling 774,152 m² — `g4642` (`cblock3` vs `cblock6`,
  598,016 m²; `cblock1` vs `cblock4`, 102,400 m²), `g4622`, `g4674`. **[measured]** None of
  them is at the recorded repro pose, so this is a latent second-order issue, not the reported
  bug — but it is a real hole in the tie-break and it is invisible to any node-level scheme.

---

## 7. The fidelity question that must be settled before either fix lands

> **Settled 2026-08-09 — no defect, no change.** Watched at the controls: the original draws
> the *shoreline over the water*, and our own shore/water boundary was judged correct in play
> regardless. The measurement below stands as written; what it predicted — a picture getting
> worse while the flicker metric improved — did not happen. So the depth order at this one pose
> is not what the eye reads at a shoreline, and it is **not** grounds to re-tune the rank
> (`BL-251`, retired — `git log --grep=BL-251`). The rest of this section is kept because the
> pose, the pair and the reasoning are still the record of how it was bracketed.

`verification.md` rule 8 and rule 4 both apply, and this is the trap most likely to waste the
next session:

**At the C1B pose, `wtr00000.tif` (water, node 743) currently renders IN FRONT of
`srf0001.tif` (surf/shoreline, node 716) — and every scheme tested keeps it there**, because
all of them preserve node index order and 743 > 716. **[measured]** So a bigger separation
does not change *which* surface wins; it makes the current winner win more decisively. If the
original draws the surf over the water, the flicker metric will improve while the picture gets
*worse* — the exact failure mode rule 4 describes, and the same shape as the C3 beach case
(rule 8), where the water was winning against the beach and the correct-looking measurement
licensed the wrong fix.

**This needs a reference capture of C1B at that pose before any bias change is judged.** The
data cannot settle it: "later node wins" is the documented rule and it says water.

C5 has no equivalent ambiguity — its contested pairs are `cblock*` ground variants inside one
mesh, and the within-mesh rule ("later polygons drew over earlier ones") is the same one the
rank already implements.

---

## 8. Recommendation

1. **Correct the record first.** The nine-node premise is disproven; the mechanism is
   (a) same-node cross-surface in C5 and (b) a single cross-node pair in C1B. Update the
   item-9 evidence section, `docs/architecture.md`'s `SceneBuilder` bullet, and add the
   AABB-twice note to `verification.md` rule 9. **This is the durable output of this session**
   — it is the fourth wrong mechanism retired, and it comes with an instrument that predicted
   a previously-unexplained measured control.
2. **Bracket the per-pair step.** One constant, two poses. Everything else is conditional on it.
3. **Then, if it lands, land it as two changes, not one.** `SurfaceRankBias` for C5;
   dense conflict rank for C1B. Sized together against the 2e-4 budget, dense rank first.
4. **Do not raise `NodeOrderBias`** — reconfirmed, now with the mechanism: it cannot reach
   C5's conflict at all.

---

## Scripts (all in `.scratch/`, read-only, no Godot)

| file | what it does |
|---|---|
| `item9_lib.py` | loader: reproduces `GameZ` transform parsing, `WorldBuilder`'s walk roots, `SceneBuilder`'s triangulation; exact polygon clipping |
| `item9_conflicts.py` | the conflict relation + components + longest chain, all 8 chapters (`--drop-parked` to exclude the origin pile) |
| `item9_repro.py`, `item9_repro2.py`, `item9_q3b_outlines.py` | the C5 repro footprint: the nine nodes, their outlines, the raster cross-check |
| `item9_withinmesh.py` | within-mesh cross-surface conflicts and the `SurfaceRankCap` collapse |
| `item9_depthprobe.py` | the software depth probe (near-plane clipping included) |
| `item9_whatif.py` | paper test of the candidate schemes + the validated control |
| `item9_budget.py` | surface-group counts, slot arithmetic, regression-guard checks |
| `item9_q1_groups.py`, `item9_q1_dag.py` | **superseded** — polygon-outline versions, kept as the record of the `tri_strip` instrument bug |

---

## 2026-08-01 — BL-052 re-measure: the cap still has small, real non-subface survivors

**Method.** `item9_withinmesh.py` now mirrors the current `SceneBuilder` surface key
`(material, priority, subface)` and reads the extracted polygon flag from `flags.unk3` (absent =
false). It triangulates the same way as `SceneBuilder`, clips true coplanar triangle overlap, and
counts only different-surface pairs whose two ranks both collapse to `SurfaceRankCap = 5`.
Pairs containing a subface are reported separately and excluded: `SubfaceBias` is `1e-4`, fifty
rank steps, after the capped rank.

**Result.** Across all eight extracted chapters, **36** cap-collapsed, non-subface pairs remain:
**13,640 m²** in **12 nodes**. C1 has 10 pairs / 2,795 m² (chiefly `a8` road/ground plus
`a6` and six sign faces); C2 has 25 / 10,837 m² (`g36347`, `g36353`, `g36360` building faces);
C5 has 1 / 8 m² (`g4674`, `cblock5.tif` / `cement1.tif`). C1B, C1C, C2B, C3, and C4 have none.

**Control.** The formerly cited C5 `cblock*` pairs are no longer survivors: 124 subface-containing
pairs, 24,122,439 m², are reported as separated. The previous 774,152 m² group is therefore not
reused as risk evidence. A deterministic fog-off freecam capture was taken at C2 `g36347`
(`.scratch/bl052/c2-g36347.png`); this census alone does not establish a visible defect, so no cap
change is proposed.

**Closure (2026-08-01).** The user confirmed the dominant C1 survivor, `a8`, visibly z-fights in
the original game. With no demonstrated regression among the C2/C5 remainder, `BL-052` is closed:
the residual is retained as measurement evidence, not a mandate to change `SurfaceRankCap`.