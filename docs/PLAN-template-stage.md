# TemplateStage — the pool/slot/place/reveal/hide cluster leaves `AnimRuntime`

**HANDOFF DRAFT — grill before activating** (written 2026-08-06 from that day's architecture
review, candidate 2; decisions NOT yet settled). The first working session on this plan **must
start with a `/grilling` session with the user** (item A1) — the Decisions table below is empty
until then, and the checklist after A1 is provisional. When a session activates this plan, point
PROJECT_CONTEXT.md's "Current status" at it. Sibling handoffs from the same review:
[`PLAN-puffer-interface.md`](plans/PLAN-puffer-interface.md) (completed 2026-08-06); the review's
candidate 1 already landed as `FireControl` (BL-295, commit `7410cbe`), and candidate 4 as
[`PLAN-engine-free-suites.md`](plans/PLAN-engine-free-suites.md) (completed 2026-08-06).

"Where does this pooled effect draw, and when does it show and hide" is one concept spread over
~360 lines of `AnimRuntime.cs` (219 KB, the hottest file in the repo — 77 changes since
2026-07-20): 13 behaviours (`SlotOf`, `TemplateRootsFor`, `NextPooledAnchors`, `PlaceTemplateAt`/
`PlaceTemplateOn`, `ShowTemplate`, `TemplateRootsOf`, `TemplateSharedWithLiveInstance`,
`TemplateIsAt`, `HideTemplateWhenIdle`/`ReadyToHide`/`TemplateStillAnimated`/`SweepTemplateHides`,
`IndexPooledCopy`), 3 public flags (`PlaceCalledTemplates`, `ShowPlacedTemplates`,
`PooledTemplates`), 3 private fields (`_templateHidesPending`, `_poolCursor`, `_slotOfNode`), the
`PoolSlotMeta` node-meta protocol and the `PoolRecycles` counter. Six files must be read to answer
the question (`effect_pools.json` → `EffectPools.SlotsFor` → `WorldEffectsFactory.BuildEffectStage`
→ the `AnimRuntime` cluster → `NameResolver`'s `ownRootsOf` hook → `EmitterDirector`). BL-016,
BL-023, BL-061, BL-224, BL-225, BL-253 and the 2026-08-06 ghost-trail fix all landed inside this
cluster. The two entry points re-implement the same place→start→reveal→hide ritual 1,200 lines
apart (`PlayEffectAt` ~:1093–1129; the `CallAnimation` arm ~:2300–2492), and the seam already
leaks on the record — `AnimRuntime`'s architecture entry's *accepted shallow spot*:
`WorldEffectsFactory.cs:391` sets `ShowPlacedTemplates`/`PooledTemplates` AFTER sealing, working
only by accident of read order.

The proposal: extract `TemplateStage<TNode>` to `src/Mech3/Anim/`, beside `NameResolver`,
`MotionSet` and `EmitterDirector` — the three prior extractions of exactly this shape. Callers
learn ~6 members (`TakeNextSlot`, `RootsFor`, `PlaceAt`, `Reveal`, `RetireWhenIdle`, `Sweep`, plus
`Recycles`) instead of 3 flags + 13 behaviours + a meta-key protocol. **No behaviour change is
intended anywhere** — 13/13 goldens hash-identical is primary evidence per item; an item that
cannot keep them identical stops rather than repins.

## Milestone goal

- The template stage is one module with one interface; both entry points perform the ritual
  through the same implementation, so a fix (e.g. the ghost-trail class) lands once.
- The sealing leak is closed structurally: the two flags become constructor arguments, and the
  accepted-shallow-spot ⚠ is deleted from `architecture.md`, not re-documented.
- Slot arithmetic (cursor wrap, modulo fallback, `PoolRecycles`), the "which copy is mine" rule
  and the reveal/hide deferral are asserted **off-engine** in `CSVM.Tests` against a token node
  type.

**`EmitterDirector` still receives already-resolved host and anchor nodes.** The pool does not
become a third keying scheme — that property of the current design survives by construction.

## Decisions (unfilled — settle in A1's grilling session)

| # | Question | Decision |
|---|---|---|
| 1–9 | See the grilling agenda in item A1. | *(to be filled by the grilling session; this table then becomes the authority where prose disagrees)* |

## ⚠ Read this before implementing anything

| # | The claim to keep honest | What guards it |
|---|---|---|
| 1 | "This re-opens G18." | It must not. G18 (2026-08-03, recorded in `architecture.md`'s `AnimRuntime` entry) declined splitting the observation surface into per-mode interfaces; this extracts one concept all modes share — the same distinction `PLAN-name-resolver` drew. Change no mode behaviour. |
| 2 | "Decision 16 already pinned these members to `AnimRuntime`." | Partly true, and the grilling MUST address it head-on (agenda Q1): `PLAN-name-resolver`'s milestone goal states *"`SlotOf` / `TemplateRootsFor` / `NextPooledAnchors` / `PlaceTemplateAt` stay where Decision 16 of PLAN-deepening pinned them"*, and the `AnimRuntime` entry's ⚠ says they "stay here — the director is handed already-resolved host and anchor nodes". The review's reading: the *letter* pins them against moving into `EmitterDirector`; a new peer module preserves the *spirit* (the director still gets resolved nodes). That reading is a proposal, not a fact — the user decides, and the outcome gets quoted in the new `architecture.md` entries either way. |
| 3 | "`Anchors` can be called wherever resolution is needed." | No — `TemplateRootsFor` deliberately resolves through `FindAll`, never `Anchors` (census re-entry). This discipline must move into the module intact, and the `ownRootsOf` hook must remain the ONLY route into `NameResolver`'s middle tier. |
| 4 | "Line citations are current." | They were gathered 2026-08-06 against `51cdb5a` (and survive `7410cbe`, which touched only `Flight/`). `AnimRuntime.cs` is the hottest file in the repo — expect drift; re-grep every citation before editing. |
| 5 | "The two 0.25 f tolerances are one contract." | Today they are two spellings written ~700 lines apart (`TemplateIsAt`, and the `libraryCopy` distance test in the `CallAnimation` arm) that agree *by coincidence, not by contract*. Unifying them is a goal (agenda Q5) — but verify they really are the same rule before merging them; if they guard different questions, keep two named constants. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — decisions, then the stage

1. ☐ Grilling session: settle the Decisions table with the user; rewrite Waves A2+/B if the calls differ
2. ☐ `TemplateStage<TNode>`: slot arithmetic + placement + the "which copy is mine" rule, with the off-engine suite
3. ☐ Reveal/retire/sweep move in; both entry points perform the ritual through the module
4. ☐ Sealing-leak closure: the two flags become ctor args; `WorldEffectsFactory` hands the stage over sealed

### Wave B — the ride-along (if the grilling keeps it)

11. ☐ `EnsureWorldEffects`'s 6-param call-order-dependent signature folds into the stage handover (the BL-232 failure family)

## Dependency and parallelism notes

A1 blocks everything. A2 → A3 → A4 is a chain (each goldens-identical); B11 depends on A4. **File
ownership:** `CSVM/src/Mech3/Anim/TemplateStage.cs` (new), `CSVM/src/Mech3/AnimRuntime.cs`,
`CSVM/src/Session/WorldEffectsFactory.cs`, a new `CSVM.Tests/TemplateStageTests.cs`.
[`PLAN-puffer-interface.md`](plans/PLAN-puffer-interface.md) and
[`PLAN-engine-free-suites.md`](plans/PLAN-engine-free-suites.md) are both completed
(2026-08-06) — no longer a live contention.

---

# Wave A — decisions, then the stage

## A1 ☐ Grilling session — settle the decision tree with the user

**Goal.** Every question below has a user-made call recorded in the Decisions table; the checklist
is rewritten to match. No code before this lands.

**Approach.** Run `/grilling` with this agenda, one question at a time, recommendation first (the
FireControl session, 2026-08-06, is the model — its record is in commit `7410cbe`'s plan trail):

1. **The Decision 16 conflict (⚠ #2 above).** Reopen, reinterpret, or drop the extraction?
   *Recommended:* reinterpret — a peer module in `Anim/` preserves the pinned property (the
   director gets resolved nodes); quote both texts in the new entries.
2. **Scope of the move.** The 13 behaviours + flags + `PoolSlotMeta` protocol + `PoolRecycles` +
   `IndexPooledCopy`? *Recommended:* the whole cluster — a partial move leaves the concept split
   across two files, which is the current disease.
3. **Generic `TNode` or `Node3D`-typed?** *Recommended:* generic like `NameResolver<TNode>`
   (two adapters at birth: `Node3D` in engine, token type in tests); the two genuine engine
   touches (`PlaceTemplateOn`'s `GlobalTransform` write, `root.Visible`) become supplied hooks.
4. **Who constructs the stage?** *Recommended:* `WorldEffectsFactory` builds it (it stamps
   `PoolSlotMeta` today) and hands it to `AnimRuntime` sealed — the two flags as ctor args, which
   is what deletes the accepted-shallow-spot ⚠.
5. **Does `PoolSlotMeta` stay node meta?** External observability (probes, labs) vs an internal
   map. *Recommended:* keep stamping the meta, but the module becomes its only reader.
6. **The two 0.25 f tolerances** (⚠ #5). One named contract, or two named constants?
7. **`ownRootsOf` rewiring.** After the move the resolver's hook is `TemplateStage.RootsFor` —
   confirm the `FindAll`-never-`Anchors` discipline carries over verbatim (⚠ #3).
8. **Test split.** Which facts go off-engine (slot wrap, modulo fallback, recycle counting, hide
   deferral, "which copy is mine") vs stay in-engine (`effect-template-mesh`, `effects-census`
   as integration)? *Recommended:* exactly that split — never re-implement a check in both tiers.
9. **Migration + instruments.** *Recommended:* three goldens-identical commits (A2/A3/A4), with
   `effects-census` verdicts and the `PoolRecycles` counter A/B'd against HEAD per commit, plus
   the `c1-destroy-effects` golden as the pixel tripwire.

**Model recommendation.** high — judgement-heavy, user-interactive, and it rewrites this plan.

**Verify.** The Decisions table is filled, each row naming its losing option; the checklist
matches the calls.

## A2 ☐ `TemplateStage<TNode>` — slots, placement, identity, with the off-engine suite

**Goal.** The slot/placement/identity half of the cluster lives in
`src/Mech3/Anim/TemplateStage.cs`, generic over the node type, with `TemplateStageTests` asserting
cursor wrap, modulo fallback, recycle counting and the "which copy is mine" rule off-engine.
`AnimRuntime` compiles and behaves identically (13/13 goldens).

**Evidence (confidence: traced).** Member and call-site citations in the header paragraph; the
churn record is the BL list there. Precedent for the shape and for "Node-typed but never
dereferenced": `MotionSet`'s and `NameResolver`'s architecture entries.

**Approach.** Move `SlotOf` / `NextPooledAnchors` / `PlaceTemplateAt` / `PlaceTemplateOn` /
`TemplateRootsFor` / `TemplateRootsOf` / `TemplateIsAt` / `TemplateSharedWithLiveInstance` /
`IndexPooledCopy` plus `_poolCursor` / `_slotOfNode` / `PoolRecycles`, exactly as they are —
resist improving logic mid-move. Wire per A1's construction call.

**Model recommendation.** high — the hottest file in the repo; the blast radius is every effect.

**Verify.** `.\RunTests.ps1` green, goldens identical; `effects-census` verdicts and
`PoolRecycles` A/B'd identical against HEAD on a `--debug-anim` chapter run.

**⚠ Traps.** ⚠ #2/#3/#5 above. `IndexPooledCopy`'s per-copy indices are deliberately NOT shared
across pooled copies — port the cache boundaries as they are (same trap class as
`PLAN-name-resolver` ⚠ #3).

## A3 ☐ Reveal / retire / sweep — one ritual, two entry points

**Goal.** `ShowTemplate` / `HideTemplateWhenIdle` / `ReadyToHide` / `TemplateStillAnimated` /
`SweepTemplateHides` and `_templateHidesPending` move in; `PlayEffectAt` and the `CallAnimation`
arm both drive `Reveal`/`RetireWhenIdle`/`Sweep` — the in-method workaround noted in
`ShowTemplate`'s own doc ("schedules the hide to cover both entry points in one place") dies
because the module IS that one place.

**Evidence (confidence: traced).** `ShowTemplate` doc ~:3070–3077; the `Advance` retire walk
~:990; the deferral holds and their interaction with `ExternalEffectStop`.

**Approach.** Only the template steps of the 190-line `CallAnimation` arm move — the call
semantics (site resolution, death bookkeeping) stay in `AnimRuntime`, per `PLAN-name-resolver`'s
boundary. Liveness/still-animated checks become supplied predicates per A1 Q3.

**Model recommendation.** high.

**Verify.** As A2, plus `effect-template-mesh` and the stop-behaviour half of `effects-census`
("none stays lit after its stop") — the hide deferral is exactly what those pin.

**⚠ Traps.** The hide sweep interacts with `TemplateStillAnimated` across defs sharing a slot —
the BL-253 ghost-trail class. If any golden moves, stop and diagnose; do not re-pin.

## A4 ☐ Close the sealing leak

**Goal.** `PlaceCalledTemplates` / `ShowPlacedTemplates` / `PooledTemplates` are constructor
state; `WorldEffectsFactory.cs:391`'s post-seal writes are deleted; the accepted-shallow-spot ⚠
is removed from `AnimRuntime`'s architecture entry and the new `TemplateStage` entry records the
closure.

**Evidence (confidence: traced).** The ⚠ itself, quoted in the header paragraph.

**Approach.** Per A1 Q4. The factory's build order changes only in when the flags are supplied,
not in what any consumer observes.

**Model recommendation.** medium — small, but ordering-sensitive; read `WorldEffectsFactory`'s
entry first (runtime ownership is split by design there).

**Verify.** As A2. Also grep for any other reader of the three flags — the census must find none
outside the module.

# Wave B — the ride-along

## B11 ☐ `EnsureWorldEffects` stops re-taking what the session already gave it

**Goal.** The 6-param, 4-call-site, call-order-dependent `EnsureWorldEffects(gamez, worldScene,
textures, worldProgram, worldRuntime, projectiles?)` shape — the BL-232 failure family, where the
lazily-cached build's winning argument triple depends on which caller got there first — collapses
into the A4 handover.

**Evidence (confidence: direction-sound).** All four `GameSession` call sites pass the same five
session values under three local spellings (`session.Program` / `damageProgram` /
`state.CrashProgram` are one object, `GameSession.cs:659`). Re-verify at execution; `GameSession`
is the second-hottest file.

**Approach.** Decided in A1 (it may also be dropped there — it is a signature fold, not a
deepening, and only earns its place riding A4's wiring).

**Model recommendation.** medium.

**Verify.** As A2, plus the `WorldEffectsFactory` ⚠: no `GameSession`-side cache may appear —
`BuildWorldEffectsRuntime` stays private.

**⚠ Traps.** The lazy cache's call-order dependence is the bug class being removed — do not
preserve it by accident in a new form.
