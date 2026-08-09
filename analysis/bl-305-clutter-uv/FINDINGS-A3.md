# A3 — the clutter-eligible polygon census: layers, UV coverage, coplanar overlaps

**Read-only measurement, 2026-08-09**, for [`docs/PLAN-clutter-uv-placement.md`](../../docs/PLAN-clutter-uv-placement.md)
item A3. Instrument: [`eligibility.py`](eligibility.py) in this directory; full raw run in
[`_raw_output.txt`](_raw_output.txt). Static analysis over `extracted/`, plus two live engine runs
for the reconciliation. No C# source touched.

The point is to know, before Wave B changes placement, how many polygons the original would stamp
that the remake does not and vice versa — so B14's A/B has a baseline that can be compared rather
than guessed at.

## The census

`polys` = polygons walked; `layer-0` / `layer-1+` = polygons whose texture at that layer names a
registered clutter template under the **original's** gates; `remake l0` = the same count under the
**remake's** looser gate; `coplanar (real)` = coplanar pairs where *both* members pass
`FUN_004de2c0`; `sim sprites` / `sim solids` = the instance counts a full port of the remake's
current placement produces.

| chapter | polys | layer-0 | layer-1+ | remake l0 | coplanar (real) | sim sprites | sim solids |
|---|---|---|---|---|---|---|---|
| C1 | 23,959 | 883 | 0 | 883 | 0 | 9,303 | 0 |
| C1B | 12,790 | 9 | 0 | 9 | 0 | 60 | 0 |
| C1C | 11,991 | 0 | 0 | 0 | 0 | 0 | 0 |
| C2 | 15,037 | 964 | 0 | **1,034** | 0 | 37,167 | 10,261 |
| C2B | 10,197 | 0 | 0 | 0 | 0 | 0 | 0 |
| C3 | 18,127 | 101 | 0 | 101 | 0 | 371 | 0 |
| C4 | 25,793 | 1,068 | 0 | **1,143** | 0 | 88,630 | 0 |
| C5 | 34,551 | 1,761 | 0 | **2,049** | 0 | 124,074 | 71,326 |

Zero polygons were dropped as degenerate (fewer than 3 vertex indices) in any chapter.

## Two checked zeros

**No-UV polygons: 0 in every chapter.** `FUN_004de2c0` skips a polygon whose UV array pointer at
+0x18 is null. In the shipped data that gate never fires: every polygon in the install carries a
non-null `uv_coords` and a non-empty `materials` list. This is a measured zero, not an absence of
data (LOG-2) — the script counts the field explicitly rather than inferring it from a missing key.

**Layer-1+ template matches: 0 in every chapter.** `FUN_004de190` iterates *every* texture layer and
looks a template up per layer, so a two-layer polygon whose layers both name templates would be
stamped twice. That mechanism **never fires on retail data**: no polygon's second or third layer ever
names a registered clutter template. 619 polygons install-wide carry a second layer and 7 a third,
but none of them is a clutter surface. Wave B must still iterate layers to match the original, but
no chapter exercises the multi-layer path, so it cannot be verified by an A/B and must not be
credited with any observed change.

## Static versus live: reconciled

The plan required the static census's remake-side numbers to equal a real build's, or nothing
downstream could use it (METHOD-15). The script contains a full port of `ParseTemplate`,
`PlaceOnWorld`, `PlaceOnMesh` and `PlaceOnTriangle`, and was diffed against the engine's
`clutter: N sprites … (kind ×count, …)` line from headless `--freecam` runs.

- **C1: exact.** 9,303 sprites, all five kinds matching per-kind.
- **C5: exact on solids** (71,326 = 71,326); **off by one** on two sprite kinds — `lightpole.tif`
  and `poleflare.tif` both read live 62,036 against simulated 62,037.

The C5 residual is 1 instance in 62,037 on each of two kinds, 0.0016 % of that chapter's total, and
traces to a float32-versus-float64 boundary case in the containment test — one candidate point sits
exactly on a triangle edge and falls on opposite sides of it in the two precisions. It is reported
rather than rounded away (DIAG-15) and is not a logic difference: every other kind in every other
chapter agrees exactly. **The script's verdict for C5 is a FAIL, deliberately** — the exit code does
not launder a mismatch it cannot explain away.

## Coplanar double-stamp pairs: 0 — but read why before using it

**The number B13 asked for is 0, in every chapter, unambiguously.** No coplanar pair exists whose
two members both pass the original's polygon gate.

That zero is **structural, not incidental.** Polygon flag `0x800` — the bit `FUN_004de2c0` skips on —
is the same bit already decoded in `docs/formats/gamez.md` as the **subface** mark. So the original's
own polygon gate excludes every subface polygon *before* texture matching runs, which forecloses the
double-stamp shape by construction. The original cannot double-stamp a base/subface pair because it
never considers the subface at all.

Cross-checked against a diagnostic that ignores the subface gate, to prove the geometry pipeline
works and the zero is the flag doing its job rather than a broken script:

| chapter | raw coplanar pairs (subface gate ignored) | subface polygons involved |
|---|---|---|
| C5 | 787,546 | 449 |
| C2 | 876 | 18 |
| C4 | 69 | 33 |
| others | 0 | 0 |

The overlap is real and large. The gate is what removes it.

### ⚠ This inverts B13's rule — the dedup cannot be deleted standalone

The plan's B13 says: zero relevant coplanar pairs → delete the `seen` set. **That reasoning does not
survive this measurement**, because the zero belongs to the *original's* rule set and the `seen` set
protects the *remake's*.

`ClutterBuilder.PlaceOnMesh` (`Clutter.cs:669-699`) **never reads the subface flag at all.** The
remake's walk therefore does independently reach both members of a coplanar base/subface pair — 449
subface polygons' worth in C5 — and `PlaceOnTriangle`'s quarter-metre `seen` set (`:357-359`) is what
silently suppresses the resulting double-stamp today. Deleting the dedup on the strength of "the
original has zero pairs" would visibly double-stamp C5's city blocks: a regression that neither the
original nor the current remake exhibits.

**Recommended order for B13: add the subface (`unk3`) skip to `PlaceOnMesh` first, reproducing
`FUN_004de2c0`'s actual gate, and only then delete the `seen` set as redundant.** That is the change
the original's code justifies; deleting the dedup alone is not.

This is also visible in the census table above: the `remake l0` column exceeds `layer-0` by 70 in C2,
75 in C4 and **288 in C5**, and that delta is the subface gate the remake is missing.

## The node gate, and one undocumented flag

`FUN_004de460` gates each node on bit `2` at node+0x24, the absence of `0x4000000` at node+0x2c, and
a node type of 5 or 6.

- The `node+0x24` bit and the node-type check correspond to already-decoded fields in
  `docs/formats/gamez.md`.
- **`node+0x2c` / `0x4000000` is not currently named in `gamez.md`.** It maps to `nodes.json`'s
  `update_flags` field (confirmed against `tools/mech3ax`'s node struct layout) and is genuinely set
  in retail data — 3 nodes, all in C2 — but in every one of those cases it is redundant with the
  already-decoded `flags.active` bit. Worth a line in `gamez.md`; not worth a behaviour change.

Nodes excluded by the whole gate: 9 in C1, 3 in C2, 0 everywhere else.

## What this means for Wave B, stated plainly

**The remake's eligibility gate is looser than the original's on two independent axes** — it applies
no subface exclusion and no active/node-type check — and both push it to **over**-stamp relative to
the original.

That is the opposite sign to A1's finding, which is that the remake **under**-stamps by up to 4× in
C1 because its metric grid is coarser than the texture's UV repeat. The two are separate mechanisms
acting on different chapters, and they must not be netted against each other: C1's deficit is a
spacing error on 883 polygons that carry no subfaces at all, while C5's surplus is a gating error on
288 polygons whose spacing is already correct. A single "instance count moved in the right
direction" check would hide both.

## What this does not determine

- Whether the 288 extra C5 polygons the remake stamps are *visible*. They are coplanar with polygons
  it also stamps, so the `seen` set means most produce no extra instance today; the count is of
  eligible polygons, not of surplus instances. B13's A/B measures the instance delta.
- Whether `0x4000000` / `update_flags` means anything beyond the retail install's three C2 nodes.
- Anything about UV spacing or orientation — A1 and A2 own those.

## Files

- [`eligibility.py`](eligibility.py) — the script.
- [`_raw_output.txt`](_raw_output.txt) — the full run, including the per-chapter template lists and
  the simulated per-kind `Summary` lines, plus both reconciliation blocks.
- `CSVM/src/Mech3/Clutter.cs` — `PlaceOnMesh`/`PlaceOnWorld` (`:635-699`), `PlaceOnTriangle`'s `seen`
  set (`:357-359`), `TemplateNames`/`BuriedClutterDistricts` (`:123-199`).
- `docs/formats/gamez.md` — the subface flag; the `update_flags` gap noted above.
- `docs/verification.md` — METHOD-15, LOG-2, LOG-5, DIAG-15, WORLD-25.

---

*Authored by the orchestrator from the A3 subagent's returned summary and its committed
`_raw_output.txt`; the subagent's own Write access was refused. Every number above is from that raw
output or from the subagent's report of its two live runs.*
