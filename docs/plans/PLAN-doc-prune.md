# Documentation prune — architecture.md, verification.md, code comments, and the contract that grew them

**COMPLETE (2026-07-23).** All four waves landed the day the plan was written. It ran BEFORE
[`PLAN-M3-weapons.md`](PLAN-M3-weapons.md) — M3 is 44 items that will all write
documentation under whatever contract exists when they land, so the contract changed first.

Scope was settled in a grilling session on 2026-07-23. Every decision below is the user's, and
the Decisions table is the authority when this document contradicts itself elsewhere.

---

## The problem (measured 2026-07-23)

- `docs/architecture.md` is **299 KB in 96 lines** — one single-line mega-bullet per module
  (SceneBuilder 32 KB, PlaneViewer 24 KB, AnimRuntime 19 KB, FlightController 14 KB). The file
  exceeds the Read tool's token limit and a single Grep hit returns the entire line, so the
  per-module lookup CLAUDE.md mandates ("read the module's bullet before changing it") is
  structurally broken.
- `docs/verification.md` is **51.8 KB**: each rule carried by a multi-paragraph war story, the
  same incidents retold across overlapping sections (the zeppelin misdiagnosis appears three
  times).
- `CSVM/src` carries **226 provenance-marker comments across 46 files** — `(M2.5 item 5)`,
  `(2026-07-20)`, `(PLAN-anim-debugger Wave 1 A2)` — history embedded mid-sentence in otherwise
  useful comments.
- **Root cause:** CLAUDE.md's routing rules *instruct* every session to write narratives into
  these files ("implementation detail, diagnosis narratives, verified gotchas → the module's
  bullet"), and unlike CLAUDE.md itself — which stopped growing only when
  [`PLAN-docs-cleanup`](PLAN-docs-cleanup.md) gave it a budget and shape rule — they
  never got a shape rule. Pruning without changing the contract means regrowth.

## Decisions (2026-07-23)

| # | Question | Decision |
|---|---|---|
| 1 | Which harm are we fixing? | All three — retrieval cost, trust, maintenance — so shape, content, **and** the CLAUDE.md contract change together. |
| 2 | What survives in architecture.md? | Terse purpose/behavior + still-binding constraints and deliberate-design markers as ⚠ one-liners. All narrative, dates, plan-item refs, and diagnosis stories: deleted. |
| 3 | Entry shape? | One `## src/...` heading per module (greppable, offset-readable); body ≤ ~8 lines (~12 for the heaviest): 1–2 sentences of purpose beyond CLAUDE.md's index line, then the ⚠ one-liners. |
| 4 | Where does cut content go? | Deleted outright. Git history and `docs/HISTORY.md` are the archive — no dump-append, no frozen-copy file. |
| 5 | Format knowledge inside module entries? | Generalized migration, every module: check against its `docs/formats/` page — missing → merge there (becomes CC-BY), already present → delete. Implementation detail never crosses into `docs/formats/` (licensing boundary). |
| 6 | verification.md treatment? | Keep every still-binding rule; compress each to a 1–2-line imperative + ≤1 sentence of hard project evidence (the number, the exact-bits fact). No narratives, retellings, or corrections-of-corrections — final understanding only. Merge the rules list and the themed sections; the what-can't-self-verify, non-determinism, and checklist sections stay as short factual lists. |
| 7 | Code comments? | Retroactive **scoped** sweep now: strip provenance markers and reviewer-directed asides, keep the what/why substance, do NOT rewrite comments wholesale. Convention added to CLAUDE.md. |
| 8 | HISTORY.md (509 KB)? | Existing content untouched — it is the safety net for every deletion above. Future entries capped at a few lines: what landed, how verified, outcome. |
| 9 | Enforcement? | **Shape rules only, no byte budgets** — module count will grow and needs room; the searchable per-module shape keeps the context cost of one lookup small regardless of total file size. |

---

## Wave A — architecture.md rewrite (299 KB → est. 40–50 KB)

All 96 module entries to the new shape. Target form:

```markdown
## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order
as depth bias (priority × surface rank × node index → polygon offset).
⚠ Instance-uniform block is an ORDERING CONTRACT — every shader on one
  instance must declare the same block (csky_instance_uniforms).
⚠ A shader with NO instance uniform must not take the preamble
  (16-vec4 per-instance buffer cost).
⚠ Hybrid by design: the 128 blend/scroll/clamp variants stay generated in C#;
  .gdshaderinc holds only the shared blocks.
```

Process (chunk the 96 entries into ~6 groups, one subagent each — the source is only readable
in slices anyway):

1. Per entry: extract purpose (1–2 sentences), extract every still-binding constraint /
   deliberate-design marker as a ⚠ line, drop everything else.
2. **Spot-verify kept ⚠ lines:** every symbol/name a kept constraint cites must grep in
   `CSVM/src` or `CSVM/shaders/`; a constraint referencing removed code is dropped, not kept.
3. **Format-content check:** format knowledge found in an entry is diffed against its
   `docs/formats/` page — already there → delete; missing → merge into that page in the same
   change. Known spillover candidates: `GameZ.cs`→`gamez.md`, `CompiledAnim.cs`/`AnimDefs.cs`→
   `anim-definitions.md`, `PatternLibrary.cs`→`paint.md`, `WavFile.cs`/`SoundDefs.cs`→
   `sounds.md`, `Clutter.cs`→`clutter.md`, `Zrdr.cs`→`zrdr.md`. Only format facts move
   (CC-BY side); implementation detail stays behind and compresses or dies.
4. Delete the retired `CrashBreakup`/`CrashChoreography` entry — the
   `bespoke-crash-animation` branch and `backlog.md` hold it.
5. Rewrite the file preamble: what the file is, the entry shape rule, "narratives live in
   HISTORY.md/git".

**Acceptance:** 96 `## src/` headings; `Grep -A 12` on any module returns that entry and
nothing else; a full-file Read succeeds (< 25 k tokens); every kept ⚠ symbol greps in the
code; no dates or plan/item refs remain in the file.

## Wave B — verification.md rewrite (51.8 KB → est. 12–16 KB)

One flat numbered rule list; each rule = bold imperative (1–2 lines) + at most one sentence of
project evidence ("noise floors measured here: 1 px to 13,346 px"). Collapse the triple-told
C5/zeppelin incidents to their final understanding, one rule each. Keep "what this project
cannot verify itself", "known non-deterministic surfaces", and the standing checklist as short
factual lists. Keep the cross-pointer to module ⚠ lines in architecture.md.

**Acceptance:** ~12–16 KB; every rule ≤ 4 lines total; no incident appears twice; no
superseded diagnosis is described except as its one-line final rule.

## Wave C — comment sweep over CSVM/src (46 files, 226 sites)

Strip provenance markers — `(2026-07-XX)`, `(M2.x item N)`, `(polish-N item N)`,
`(PLAN-* Wave N)`, `(Run-2 item N)` — and reviewer-directed asides; re-flow each sentence so
it stands alone. Keep all what/why substance and ⚠-style warnings. Hotspots first:
`PlaneViewer.cs` (52), `StuntMission.cs` (20), `FlightController.cs` (20), `Clutter.cs` (17),
`SceneBuilder.cs` (13), `MapEdgeExtender.cs` (8); then the long tail.

**Acceptance:** the marker grep
`// .*(20\d\d-\d\d-\d\d|M2[.-]|polish|item \d|Run-2|Wave \d|PLAN-)` returns 0 hits in
`CSVM/src`; `git diff` is comment-only; `dotnet build CSVM/CSVM.sln` passes.

## Wave D — the CLAUDE.md contract rewrite (the regrowth fix)

In "Keep this file + `docs/` up to date":

- Architecture routing rule becomes: *module purpose + still-binding constraints as ⚠
  one-liners → the module's `##` entry in `docs/architecture.md` (≤ ~8 lines/module);
  diagnosis narratives go ONLY to a short HISTORY.md entry.*
- verification.md rule gains the shape: *rule + one evidence sentence, no narrative.*
- HISTORY.md rule gains the cap: *a few lines — what landed, how verified, outcome.*
- Coding conventions gains: *Comments state what and why, briefly — never provenance (dates,
  plan/milestone/item refs), never history, never instructions to a reviewer.*

**Acceptance:** the routing rules match Decisions 2–8; CLAUDE.md stays under its 35 KB budget.

---

## Sequencing and commits

Waves are independent except D, which lands last (the contract should describe the docs as
they now are). One commit per wave, straight to `main` when the user asks — each individually
revertable. When all four land: move this file to `docs/plans/`, add its row to
`plans.md`, append the HISTORY.md entry (a few lines, per its own new rule), and hand the
active-plan slot to [`PLAN-M3-weapons.md`](PLAN-M3-weapons.md) Wave A.
