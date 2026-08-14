# `gunshell`/`muzzle_burst` node shape across all 8 chapters (BL-140)

**Question.** `docs/formats/weapon-effects.md` listed `gunshell` and `muzzle_burst` as
prototype roots "confirmed in C1" alongside genuinely-meshed roots (`slug.flt`,
`splash1.flt`, …). `backlog.md`'s original `BL-140` evidence, traced only in C1, read
`gunshell`'s node as `model_index: -1` with "zero real children" — the same shape as a
documented empty template root — implying **neither name resolves to any visible mesh
anywhere in its subtree**. Does that shape hold in the other 7 chapters, and is "zero
real children" actually correct?

**Instrument.** `check_meshless_roots.py` — reads each chapter's `extracted/<chapter>/gamez/nodes.json`
(a flat list, array index = node index; `child_indices` are indices into the same list),
finds the node named `gunshell` / `muzzle_burst`, and reports the root's own
`model_index` plus each direct child's name and `model_index`. Read-only, no game data
embedded (per `analysis/README.md`).

**Result — identical shape in all 8 chapters (`C1`, `C1B`, `C1C`, `C2`, `C2B`, `C3`, `C4`, `C5`):**

- `muzzle_burst`: root `model_index: -1`, exactly **one** child, named `dummy`,
  `model_index: -1`. The root and its whole subtree are genuinely meshless — the name
  resolves to a node, and neither it nor anything under it has geometry to show.
- `gunshell`: root `model_index: -1`, exactly **one** child, named `g1`,
  **`model_index: 60`** (same model index in every chapter) — `models.json` model 60
  has 10 vertices / 7 polygons, a real (if tiny) mesh. The *root* node carries no mesh,
  but its subtree is **not** meshless: the single child does.

**This corrects the original `BL-140` evidence, not just extends it to 7 more chapters.**
"Zero real children" was wrong for both names — each root has exactly one child, and for
`gunshell` that child carries a real mesh. The `gunshell` name resolving to a meshless
*root* does not mean "the data gives no mesh" (the phrasing `BL-141`'s fix-shape note
used) — a mesh exists one level down, structurally parented under `gunshell` itself
(`nodes.json`'s own `parent_indices` on the `g1` child names the `gunshell` root as its
only parent).

**Incidental finding — `BL-141`'s traced node numbering doesn't match raw `nodes.json`
indices.** `BL-141` names the mesh-bearing node "205 (`g1`)" with a parent chain
"`rabbit_blur` (203) → `g11` (200) → `rabbit_blur` (198)". Applying a uniform −1 offset
(the same "anim-def ptr is +1 from the raw list index" convention noted in `BL-137`)
maps those onto raw indices 204/202/199/197 — which *are* `g1`/`rabbit_blur`/`g11`/
`rabbit_blur` by name. But raw node 204's own `parent_indices` field is `[203]` only,
and raw 203 is `gunshell`, not `rabbit_blur` — so `BL-141`'s "parent chain" reads as
list-adjacency near the target node, not the JSON's actual parent pointers, and its
"no relation to `gunshell` by … parentage" conclusion does not hold: node 204 (`g1`,
model 60) *is* `gunshell`'s only child by the data's own `parent_indices`. Flagged in
`backlog.md`'s `BL-141` entry; not re-investigated further here — out of `BL-140`'s scope.
(`BL-141` was closed on this correction, 2026-08-14: `shell1`/`shell2` are model 60's own
materials, so the casing's skin, and `rabbit_blur` is the tracer streak child decoded in
`docs/org/tracers.md`. See `git log --grep=BL-141`.)

**Confidence:** measured — every number above read directly from each chapter's
`nodes.json` / `models.json`, not inferred.
