# Static collider probe rewrite, and the off-by-6/11 gap

**Question.** A now-deleted probe
(`.scratch/probe_exempt.py`, swept by `CleanScratch.ps1` — `.scratch/` is gitignored, and it has
no copy anywhere including git history: `git log --all -- **/probe_exempt*` returns nothing)
reproduced the runtime world-collider count exactly in 6 of 8 chapters but predicted **6 too few**
in C4 and **11 too few** in C5 (the archived development log, 2026-07-22 entry "Polish-3 item 5", final table).
The safety conclusion wasn't in doubt (the probe's candidate list was a strict superset — nothing
it thought exempt was actually solid), but the gap itself was never explained. C22 asks for the
probe back as a committed instrument, and for that gap to be resolved one way or the other.

## Method

`probe.py` re-derives, from `extracted/<chapter>/gamez/{nodes,models,materials,textures}.json`
alone (no engine running), the exact set of colliders `WorldBuilder.Build` +
`SceneBuilder.BuildSubtree` would attach in a `--collision` run. It mirrors the current code
field for field (see the script's own docstring for the line-by-line mapping — root selection,
`SkipWorldNode`, LOD filtering, `NoCollisionNode`'s subtree-inherited exemption, and critically
`CollidersForMesh`'s per-surface-class split, which is what makes a "count collidable nodes"
probe wrong: one node's mesh can yield 0, 1, 2 or 3 colliders depending how many of
water/buildings/untagged its own polygons touch). Deliberately does not model `ClutterBuilder` —
see the docstring for why that's provably out of scope, not an oversight.

**Ground rule check:** every value below is read from `extracted/`, not assumed. `--freecam
--chapter=<X> --collision` was run for all 8 chapters via `RunProbe.ps1` (SHELL-10) and each
`.scratch/logs/probe-*.out`'s `loaded chapter <X> world + freecam: N gamez nodes, M mesh
instances, C colliders, ...` line (`GameSession.LogBuildSummary`) is the ground truth WORLD-9
requires — `--collision` is exactly the flag that makes freecam build world colliders at all
(`SessionSpec.BuildsCollision`), the trap this item's plan entry calls out explicitly.

## Result (this build, 2026-08-04)

| chapter | probe predicted | runtime (`--freecam --collision`) | match |
|---|---|---|---|
| C1  | 1991 | 1991 | ✅ |
| C1B | 1046 | 1046 | ✅ |
| C1C | 1324 | 1324 | ✅ |
| C2  | 1239 | 1239 | ✅ |
| C2B |  957 |  957 | ✅ |
| C3  | 1730 | 1730 | ✅ |
| C4  | 1852 | 1852 | ✅ |
| C5  | 3569 | 3569 | ✅ |

**8 of 8 exact matches.** The C4/C5 gap reported 2026-07-22 does not reproduce on the current
build — **measured gone**, the Verify step's explicit alternative to reproduce-and-explain.

## Why the gap closed (investigated, not fully attributed — and not required to be)

The 2026-07-22 measurement was taken on a materially different collision pipeline: its own final
counts (C4 2545, C5 4253) predate `597101f` ("water/buildings colliders split per surface class,
not per whole mesh" — the `CollidersForMesh` multi-bucket system this probe now models directly
replaced an area-weighted single-vote-per-mesh classifier), `85c67b2` (`intersect_surface`
honoured — confirmed by the retired `analysis/intersect-surface/FINDINGS.md`'s own before/after
table (`git show analysis-archive:` that path), whose
*prior* numbers, C4 2638 / C5 4583, are themselves higher than the 2026-07-22 finals, so at least
one more collision-affecting change landed between the two), `7c82b80` (marker gizmos no longer
build a collider), and this plan's own Wave A (`active`-flag honouring, the depth-bias conflict
rank, the second material pass). Any of these could move a per-chapter total by single digits.

One candidate was checked directly and ruled out by count: `marker_check.py` walks the same
collidable subtree and counts marker-gizmo nodes (`GameZ.IsMarkerGizmo`) that reach a still-
collidable point in the walk — i.e. nodes `7c82b80` (2026-08-01, "Level-editor marker gizmos no
longer render") stopped colliding. That's **3** in C4 (`p2_lightmarker`/`p3_lightmarker`/
`p4_lightmarker`) and **1** in C5 (`exhaust1`) — real, but neither matches 6 or 11, so marker
gizmos are at most a partial contributor, not the whole explanation.

Naming the single exact commit is deliberately not chased further: DIAG-6 (correlation is not a
mechanism) cuts against picking one of several plausible contemporaneous changes without
isolating it, and the plan's own Verify step accepts "measured gone" as a complete outcome — the
question this item asks is "does today's probe explain today's runtime," and it does, exactly,
on every chapter.

## Reproduce

```
python analysis/collider-probe/probe.py            # predicted vs nothing; compare by hand
python analysis/collider-probe/probe.py --verbose   # + C4/C5 contributing-root breakdown
python analysis/collider-probe/marker_check.py      # the marker-gizmo cross-check above
```

Runtime side, one chapter: `.\RunProbe.ps1 --freecam --chapter=C4 --collision
--screenshot=.scratch\c4.png`, then read the `loaded chapter C4 world + freecam: ... colliders`
line from the printed `.out` log path.
