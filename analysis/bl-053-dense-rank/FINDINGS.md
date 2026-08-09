# BL-053 — the dense cross-node conflict rank

**2026-08-04.** Re-measurement, sizing and verification of the cross-node draw-order tie-break,
replacing `node_bias = node.Index × NodeOrderBias`. Supersedes the numbers in
`analysis/item9-depth-bias/FINDINGS.md` §5–§6 for today's build (see §1 for why they moved).

Claims are labelled **[measured]** (computed from the shipped data or captured in Godot),
**[inferred]** (measured values plus code reading), or **[assumed]**.

---

## Verdict

**Landed.** The tie-break is now one slot per conflicting *layer* instead of one per node.
Two independent measurements settle the sizing, and they agree:

1. **Arithmetic.** The step must exceed `SurfaceRankCap × SurfaceRankBias` = **1e-5**, or a
   within-mesh surface rank of 5 still out-bids a one-slot cross-node step — which is the whole
   defect. It must also leave `SubfaceBias` (1e-4) inside one priority level, which caps it at
   **1.29e-5** given the measured worst chain of 7. **[measured]**
2. **Capture.** A millimetre-jitter capture at the recorded C1B pose brackets the separation that
   actually stops a coplanar fight between **5e-6 (fails)** and **1.2e-5 (clean)**. **[measured]**

`ConflictRankBias = 1.2e-5` is the only value satisfying both, and it is not a compromise: the
capture at 1.2e-5 is pixel-for-pixel as stable as a 10× larger control.

**The FINDINGS proposal of 5e-6 would not have worked.** item9 §6 proposed `28 × 5e-6`; measured
here, 5e-6 leaves 1,774 pixels swapping winner at the very pose it was proposed for, and leaves
126 conflicting pairs still resolving the wrong way round install-wide. **[measured]**

---

## 1. The inherited numbers moved, and one reason is an instrument defect

`analysis/item9-depth-bias/` reports C1 2,491 / C5 6,567 conflicting node pairs and "2–16 % of
cross-node conflicting pairs resolve the wrong way round". Three things changed since:

- **`item9_lib.built_nodes` never drops far-LOD levels.** It reads `range_near` / `range_min`;
  the extraction ships `data.Lod.range.min`, so the lookup returns `None` and every LOD level is
  kept — including the ones `SceneBuilder.BuildSubtree` refuses to build. Re-measured with the
  correct field, C1 falls 2,486 → 2,129 pairs and C5 6,551 → 5,812. **[measured]** The scripts in
  that directory are left as the record of what was run then; `dense_lib.py` here has the fix.
- **The subface fix landed** (2026-08-01). A base/subface pair is separated by half a priority
  level and is no longer a conflict, so it leaves the graph.
- **A1 landed** (2026-08-04): a root shipped `active: false` is not built, so it leaves too.

The 2–16 % figure was also *approximate by construction* — item9 §5 used each node's **maximum**
surface rank rather than the rank of the surface actually in conflict. Measured exactly, over the
surfaces that actually overlap and excluding the hidden origin-parked pile:

| chapter | visible cross-node coplanar pairs | of which conflicts | **wrong way round today** | after |
|---|---|---|---|---|
| C1 | 762 | 513 | **204 (40 %)** | 0 |
| C1B | 31 | 16 | **6 (38 %)** | 0 |
| C1C | 0 | 0 | 0 | 0 |
| C2 | 80 | 69 | **10 (14 %)** | 0 |
| C2B | 0 | 0 | 0 | 0 |
| C3 | 1,265 | 513 | **119 (23 %)** | 0 |
| C4 | 438 | 354 | **114 (32 %)** | 0 |
| C5 | 485 | 453 | **213 (47 %)** | 0 |
| **total** | **3,061** | **1,918** | **666 (35 %)** | **0** |

**[measured, `hierarchy.py`]** So the defect is bigger than recorded — 35 % of contested pairs
install-wide and up to 47 % in a chapter, not 2–16 %.

## 2. The "accepted corner case" does not bite — and that is worth knowing

`docs/architecture.md` recorded the tie-break's span as an accepted limit: "a prio-0 node >~4000
indices later can out-bias a prio-1 overlay; no such pair observed in C1". Measured across all
eight chapters over **1,143 hierarchy pairs** (cross-node, coplanar, overlapping, *differing* in
priority or subface): **zero resolve against the authored layering, today or after the change.**
**[measured]** The span is real arithmetic, but the pairs it could invert do not exist in this
data — the visible defect is entirely *within* one priority level. The note is retired as a
measurement, not as an assumption.

## 3. Sizing: two constraints that nearly exclude each other

```
dominance   step >  SurfaceRankCap × SurfaceRankBias                      = 1.0e-5
budget      maxRank × step + SurfaceRankCap × SurfaceRankBias < SubfaceBias = 1.0e-4
```

The budget constraint is the strict one: keeping the whole tie-break under `SubfaceBias` is what
keeps *subface + tie-break* inside one authored priority level, which is the property
`SubfaceBias`'s own comment exists to protect.

`maxRank` is the longest index-ordered chain in the conflict DAG, and it is dominated by the
origin-parked entity pile — one interpenetrating heap of unplaced vehicles at the map corner:

| | C1 | C1B | C1C | C2 | C2B | C3 | C4 | C5 |
|---|---|---|---|---|---|---|---|---|
| longest chain, all nodes | 27 | 10 | 16 | 16 | 10 | 13 | 20 | **28** |
| longest chain, parked pile excluded | **8** | 3 | 1 | 4 | 1 | 5 | 5 | 3 |

**[measured, `sweep.py`]** At 28 there is no admissible step at all: `28 × 1e-5` alone is 1.4× a
whole priority level. At 8 (max rank 7) the window is `1.0e-5 < step ≤ 1.29e-5`.

**Excluding the parked pile is sound, not convenient.** `WorldBuilder.HideUnplacedEntities`
switches every one of those entities off at bootstrap, so nothing in the heap is on screen to
fight; anything the mission subsequently *moves* is restored, by which point it is no longer in
the heap. **[inferred from the code + the measured populations]**

## 4. The bracket — what separation actually stops a fight

item9 §3 flagged that the "1e-6 resolution floor" the slot budget divided by was disproven and
that the real per-pair step was **unbracketed**. It is bracketed here.

**Instrument (`flipcount.py`).** A coplanar pair the depth buffer cannot separate does not merely
look wrong once — its winner is *unstable*. The two contested textures are flattened to loud
colours (`--tex-override=wtr00000=ff0000 --tex-override=srf0001=00ff00 --no-fog`), the same pose
is captured three times with the camera moved **1 mm**, and pixels that swap winner are counted.
The blend ramp between the two is excluded by requiring one channel to dominate, so a smoothly
shifting edge is not counted.

Pose: C1B `--pos=-7698.844,48.763,-5797.900 --lookat=-7749.957,-20.093,-5849.467`, the cross-node
pair item9 §2 recorded (node 743 `wtr00000` vs node 716 `srf0001`, index delta 27). Each row is a
build with `NodeOrderBias` rescaled so that pair receives exactly the stated separation:

| pair separation | pixels swapping winner @1 mm | @2 mm |
|---|---|---|
| 1.35e-6 (today) | **16,260** (1.76 % of frame) | 240 |
| 5e-6 (item9's proposed dense step) | **1,774** | 1 |
| **1.2e-5** | **0** | 1 |
| 1.2e-4 (able-to-fail control, 10×) | 0 | 1 |

**[measured]** The control is what makes the 1.2e-5 row meaningful (METHOD-9): a 10× larger
separation does not do better, so 1.2e-5 is not merely "not yet failing". The residual single
pixel and ~140 pixels entering/leaving classification are the blend ramp and are identical in the
control. Temporary source edits were reverted and proved reverted with `git diff` (METHOD-17).

## 5. What landed

`CSVM/src/Mech3/ConflictRank.cs` (new) computes the graph; `WorldBuilder.RankConflicts` walks the
world exactly as the build does and hands the map to `SceneBuilder.ConflictRanks`;
`SceneBuilder.NodeBiasOf` is now the single source of every `node_bias` — world nodes, clutter
decorations (`Clutter.NodeBiasOf`) and map-edge tiles (via `KindExport.NodeBias`).

A build with **no** conflict map (an aircraft, `--node=`, the viewer's plane) keeps
`node.Index × NodeOrderBias`. That is deliberate: those builds have no cross-node conflict graph,
and it is what makes the two non-world goldens byte-identical below.

**Cost, measured in-engine:** 11–45 ms per world build (C5 45 ms against a 2,437 ms world load —
1.8 %). **[measured]**

| | C1 | C1B | C1C | C2 | C2B | C3 | C4 | C5 |
|---|---|---|---|---|---|---|---|---|
| conflicting node pairs | 351 | 13 | 0 | 61 | 0 | 337 | 161 | 326 |
| ranks used | 8 | 3 | 1 | 4 | 1 | 5 | 5 | 3 |
| ms | 32 | 14 | 11 | 17 | 11 | 32 | 32 | 45 |

### Engine vs instrument: the rank depth agrees exactly, the pair counts do not

Ranks used are identical to the instrument's in all eight chapters (8/3/1/4/1/5/5/3). Pair counts
differ by up to 25 % (C4 161 vs 215), in both directions. Three known causes, all understood:

- **The engine culls degenerate triangles**, which a `tri_strip` carries many of; the instrument
  counts them. C1: 34,562 engine triangles vs 40,258. **[measured]**
- **The engine works in float, the instrument in double.** A world coordinate reaches ~1e4, where
  a float has about the plane tolerance's worth of precision left. This is not the engine being
  less correct: **the float geometry is what the GPU renders**, so the engine's own view of "are
  these coplanar" is the one that decides whether they fight.
- **The engine pairs each plane bucket against the next offset up**, recovering pairs a
  quantisation boundary would split (294 → 326 in C5). The instrument does not.

The node sets themselves match exactly where checked (C1: 3,376 built nodes and 18 parked roots,
both implementations). **[measured]**

## 6. Verification

- **`RunTests.ps1`**: build, 414 units, 22 in-engine suites, engine errors clean.
- **Goldens: 11 of 13 moved and were rebaselined; `viewer-bhawk` and `empty-stage` are
  hash-identical** — the two shots with no world build, exactly as predicted (METHOD-12). The
  pre-change build was re-run from a `git stash` and reproduced all 13 original hashes, which is
  what makes the A/B attributable (METHOD-6/8). Pixel diffs of the 11: 0.05 %–1.73 % of frame,
  scattered speckle at contested surfaces, nothing structural. **[measured]**
- **8-chapter `--freecam` regression: 0 errors**, and the build composition is invariant — C1B
  before and after both report `5603 gamez nodes, 3095 mesh instances, 614 uv-clamped surfaces`.
  **[measured]**
- **Targeted A/B captures** (`git stash` A/B, same pose, `--no-fog`):
  - **C1B water/shoreline**: 16,260 → **0** pixels swapping winner under the 1 mm jitter.
  - **C1 `a5`/`lkshb6`** (`--pos=-5331.2,378.0,-5008.4`): 27,773 px changed — the lake-shadow
    decal is no longer clipped by the tile's own rank-5 grass surface; the shadow reads as a
    complete soft blob instead of one with a hard straight bite out of it.
  - **C5 `g636`/`lrdrp2`** (`--pos=-3505.3,289.9,-3421.7`): 6,438 px — the warehouse roof's
    mottled patches resolve.
  - **C3 `g28614`/`g29108`**, the largest inverted pair by area (49,152 m²): only **146 px**
    changed. Worth recording as a caution — **the pair census bounds what CAN fight, it does not
    count visible defects**; that pair is occluded at every angle tried.

## 7. What this does NOT establish

**That "the later node wins" is what the original shows.** Every scheme here preserves node index
order, which is the documented rule (`nodes.json` is a DFS serialization = draw order) — but at
the C1B pose it means water keeps drawing over the shoreline, more decisively than before. The
data cannot settle it; only the original could.

**Settled 2026-08-09 at the controls, and the picture is fine** (`BL-251`, retired — `git log
--grep=BL-251`). The original draws the *shoreline over the water*, and our build's shore/water
boundary was judged correct in play all the same. So this section's warning is discharged: what
it feared was a visible defect, and there is none. What stays unexplained is narrower — the
single-pose measurement below says water wins at `-7700,49,-5798`, yet nothing wrong is visible
at the shore. Treat the depth ordering at that one pose as unrepresentative of what the eye
reads at a shoreline, not as a licence to re-tune the rank.

## 8. One-off observed, unrelated to this change

A `--freecam --chapter=C1 --screenshot` run on the **unmodified** build exited `0xC0000005` —
*after* writing its PNG — in the .NET finalizer during shutdown:

```
ERROR: Leaked unsafe reference
Fatal error. 0xC0000005
   at Godot.NativeInterop.NativeFuncs.godotsharp_internal_object_get_associated_gchandle(IntPtr)
   at Godot.GodotObject.Dispose(Boolean) / Finalize() / System.GC.RunFinalizers()
```

Not reproduced on retry (the retry's PNG is byte-identical to the crashed run's). Recorded here
because `BL-039` (C21 in this plan) is about exactly this class of evidence going missing: this
one is a **teardown** crash with the artifact already on disk, whereas `BL-039`'s run exited 1
with no PNG — so it is a lead for that item, not the same failure.

---

## Scripts

All read-only, no Godot except where stated. Run from anywhere; they locate `extracted/`.

| file | what it does |
|---|---|
| `dense_lib.py` | the loader: today's build walk (active flag, skip names, nearest LOD, marker gizmos), today's surface key, the coplanar-overlap relation, the dense-rank layering |
| `census.py` | per-chapter conflict pairs, chain length, wrong-way-round count today vs a dense rank |
| `sweep.py` | the sizing sweep: chain length with and without the parked pile, inversions at six candidate steps, budget arithmetic |
| `hierarchy.py` | the load-bearing census: conflict pairs AND hierarchy pairs, wrong-way counts today vs dense |
| `worst.py` | the largest inverted pairs with a camera pose aimed at the **overlap** centroid (a node's bbox centre is a kilometre off) |
| `flipcount.py` | the z-fight instrument: pixels that swap winner between two captures (needs Pillow, reads PNGs) |
