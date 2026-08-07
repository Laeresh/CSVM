# TemplateStage — the pool/slot/place/reveal/hide cluster leaves `AnimRuntime`

**COMPLETE** (2026-08-07). Written 2026-08-06 from that day's architecture review,
candidate 2; the A1 grilling session ran 2026-08-06 and filled the Decisions table below, which
is the authority where prose disagrees. Wave A landed A1–A4; B11 was closed ❌ no-go at its
Decision 9 gate — its reasoning is in its own section, and the dead end is recorded in
`architecture.md`'s `WorldEffectsFactory` entry so it is not re-chased. Sibling handoffs from the
review: [`PLAN-puffer-interface.md`](PLAN-puffer-interface.md) (completed 2026-08-06); the review's
candidate 1 already landed as `FireControl` (BL-295, commit `7410cbe`), and candidate 4 as
[`PLAN-engine-free-suites.md`](PLAN-engine-free-suites.md) (completed 2026-08-06).

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
`WorldEffectsFactory.cs:391` (drifted to `:441–442`) sets `ShowPlacedTemplates`/`PooledTemplates`
AFTER sealing, working only by accident of read order. *(Closed by A4, 2026-08-07 — the flags are
sealed stage constructor state and the ⚠ is deleted from `architecture.md`.)*

The proposal: extract `TemplateStage<TNode>` to `src/Mech3/Anim/`, beside `NameResolver`,
`MotionSet` and `EmitterDirector` — the three prior extractions of exactly this shape. Callers
learn ~6 members (`TakeNextSlot`, `RootsFor`, `PlaceAt`, `Reveal`, `RetireWhenIdle`, `Sweep`, plus
`Recycles`) instead of 3 flags + 13 behaviours + a meta-key protocol. **No behaviour change is
intended anywhere** — 13/13 goldens hash-identical is primary evidence per item; an item that
cannot keep them identical stops rather than repins.

## Milestone goal

- The template stage is one module with one interface; both entry points perform the ritual
  through the same implementation, so a fix (e.g. the ghost-trail class) lands once.
- The sealing leak is closed structurally: the flags become constructor arguments (all three, per
  Decision 4), and the accepted-shallow-spot ⚠ is deleted from `architecture.md`, not re-documented.
  ☑ A4.
- Slot arithmetic (cursor wrap, modulo fallback, `PoolRecycles`), the "which copy is mine" rule
  and the reveal/hide deferral are asserted **off-engine** in `CSVM.Tests` against a token node
  type.

**`EmitterDirector` still receives already-resolved host and anchor nodes.** The pool does not
become a third keying scheme — that property of the current design survives by construction.

## Decisions (settled 2026-08-06; authority where prose disagrees)

| # | Question | Decision |
|---|---|---|
| 1 | The Decision 16 conflict (⚠ #2) | **Reinterpret.** The pin's letter blocked moving slot arithmetic into `EmitterDirector`; its spirit is the property "the director is handed already-resolved host and anchor nodes; the pool is not a third keying scheme". A peer module in `Anim/` preserves that property. Both prior texts (PLAN-deepening D16, PLAN-name-resolver's milestone goal) get quoted in the new `architecture.md` entries with this reinterpretation stated. *Losing options:* reopen formally (heavier, no interpretive reading); drop the extraction (keeps the 1,200-line duplication and the sealing leak). |
| 2 | Scope of the move | **The whole cluster PLUS the BL-288 caller-slot trio** the draft's 13-member list omitted (`AssignCallerSlot`/`AssignedCallerSlot` + `_callerSlots`/`_callerSlotCursor`, ~:3038–3077 — consulted by `TemplateRootsFor`/`TemplateRootsOf`, so load-bearing for slot choice). `IndexPooledCopy` moves as the stage's staging entry with `IndexWorld`/`ApplyResetStatesWithin` as supplied runtime hooks; `ResolveLibraryRoot` stays a delegate outside (a *provider of* pooled copies, not slot/place/reveal logic). *Losing options:* the draft's 13 only (splits slot choice across two files); slots/placement only (half the concept moves). |
| 3 | Generic `TNode` or `Node3D`-typed? | **Generic `TNode`**, `NameResolver<TNode>`'s shape. Honest hook count is ~8, not the draft's 2: identity comparer, parent/slot-meta walk, place-writer (`GlobalTransform`+`TopLevel`), visibility-writer, position-reader, and `isValid`/`isLive`/still-animated predicates — each one line in the engine adapter. The off-engine suite is the milestone's point; `MotionSet`'s typed-but-not-dereferenced precedent records exactly what that choice costs (its off-engine fake collapses). *Losing options:* `Node3D` + static arithmetic core (re-splits the concept); plain `Node3D`-typed (off-engine goal dropped). |
| 4 | Who constructs the stage? | **`WorldEffectsFactory` builds it sealed and passes it into `ForEffects`/`ForCrashRig` as an argument**; the three flags become the stage's ctor state; plain `new AnimRuntime` gets an inert default stage (all-off — zero change for the ambient world and the lab/test construction sites). The flag properties leave `AnimRuntime` entirely, so the accepted-shallow-spot leak becomes *unexpressible*, not just closed. ⚠ The leak's cite drifted: it now sits at `WorldEffectsFactory.cs:441–442`, not `:391`. *Losing options:* runtime builds + factory configures (renames the leak); stage as a `Bind` argument (two-phase birth with an implicit ordering contract). |
| 5 | Does `PoolSlotMeta` stay node meta? | **Yes — keep stamping it; the stage becomes its only reader** (the engine adapter's slot hook is today's ancestry walk). Verified: written by `WorldEffectsFactory` (2 sites) + `Suites.cs` fake pools (3 sites), read only by `SlotOf`; its value is scene-dump observability plus the suites' existing idiom, both preserved at zero cost. *Losing options:* internal container registry (blinds scene dumps, new suite API); stamp + registry both (two sources of truth). |
| 6 | The two 0.25 f tolerances (⚠ #5) | **One named contract.** Verified per the ⚠ before merging: `TemplateIsAt` (~:3238) and the `CallAnimation` `movedAway` test (~:2484) ask the identical question — "has the call site moved from where this copy sits" — with the same threshold and purpose, differing only in root resolution (pooled roots vs a library copy that `TemplateRootsFor` cannot see). One `TemplateStage` constant (0.25 f m² ≡ 0.5 m), rationale in its doc. *Losing option:* two named constants — preserves a coincidence as a distinction the code doesn't have. |
| 7 | `ownRootsOf` rewiring (⚠ #3) | **Confirmed.** The resolver's hook becomes `TemplateStage.RootsFor`; the stage's `findAll` is a **late-bound delegate wired at the runtime handover** (the factory builds the stage before any resolver exists — both directions are delegates). The discipline moves onto the stage's doc verbatim, WITH the real asymmetry spelled out: per-*call* paths (`NextPooledAnchors`) may use `Anchors` (census records once per def identity), per-*event* paths must use `FindAll` only. `NextPooledAnchors` keeps its `Anchors` call unchanged. *Losing option:* handing the stage the whole resolver — makes the discipline advisory instead of structural. |
| 8 | Test split | **Arithmetic off-engine, integration in-engine, nothing re-implemented across tiers.** New `TemplateStageTests` (token adapter): cursor wrap, modulo fallback, `PoolRecycles` counting (both wrap flavours), caller-slot stickiness, hide-deferral holds + sweep drain. `effect-template-mesh` / `effects-census` / `damage-template-pool` stay in `Suites.cs` unchanged as the integration tier (they assert scene/visibility consequences a token type cannot honestly fake). *Losing options:* also porting suites down (MotionSet's lesson); in-engine only (abandons Q3's reason for generics). |
| 9 | Migration + instruments | **Three goldens-identical commits (A2→A3→A4), each verified by the FULL 13-golden `.\RunTests.ps1` sweep** — not just `c1-destroy-effects`; the puffer A3 migration proved the moving golden is the unexpected one (`c1-flight`). Per commit: `effects-census` verdicts + `PoolRecycles` A/B'd identical against HEAD on a `--debug-anim` chapter run. **Zero tolerated deltas** — this is a pure extraction with no accepted behavioural change anywhere; any golden moving is stop-and-diagnose, never re-pin. B11 stays in the plan, decided after A4 lands. *Losing options:* one big commit (loses bisectability in the hottest file); dropping B11 today. |

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

1. ☑ Grilling session: Decisions table settled 2026-08-06; all nine recommendations accepted (two with substantive findings: the caller-slot trio joins the move scope, and the honest hook count is ~8)
2. ☑ `TemplateStage<TNode>` landed: slots/placement/identity + the BL-288 caller-slot claim moved (10 members, 5 fields), generic with 7 ctor hooks + 9 `Wire`d runtime hooks; `TemplateStageTests` (15 facts) asserts wrap/modulo/recycles/stickiness/placement/tolerance off-engine; 566 units + 24 suites + 13/13 goldens identical, and the two `--debug-anim` A/B scenarios (destroy + crash, frame 120) pixel-identical AND anim-log-identical to HEAD
3. ☑ Reveal/retire/sweep landed: `Reveal`/`RetireWhenIdle`/`Sweep` + the pending-hides list moved in (the still-animated hold stays in `AnimRuntime` as the supplied predicate, Decision 3's hook list), both entry points drive the one module, `ShowPlacedTemplates` forwards to `Shown`, Decision 6's second literal folded onto `MoveToleranceSq`; 6 new off-engine facts (572 units) + 24 suites + 13/13 goldens identical, both `--debug-anim` A/B scenarios pixel- and anim-log-identical to HEAD
4. ☑ Sealing-leak closure landed: the three flags are sealed `TemplateStage` ctor state (`Pooled`/`Shown`/`Places`, get-only), `ForEffects`/`ForCrashRig` take the stage as their first argument, `new AnimRuntime()` takes the inert all-off default and `AnimRuntime.NewTemplateStage` is the one Godot adapter; the anim lab's post-`Bind` write became `WorldSession.Options.PlacesCalledTemplates`. Census: no reader or writer of the three flags outside the module (`AnimRuntime` reads `Places` at one site). The accepted-shallow-spot ⚠ is deleted from `architecture.md`, not re-documented; 572 units + 24 suites + 13/13 goldens identical, both `--debug-anim` A/B scenarios pixel- and anim-log-identical to HEAD

### Wave B — the ride-along (kept; decided after A4 lands)

11. ❌ `EnsureWorldEffects`'s 6-param signature fold — **no-go at the Decision 9 gate** (2026-08-07): A4's handover builds the stage from `_spec` alone, so there is no wiring to ride; the call-order dependence is in the signature only (all four sites pass one and the same five objects); and every fold shape either re-expresses the ordering contract or restructures session construction. No code landed

## Dependency and parallelism notes

A1 blocks everything. A2 → A3 → A4 is a chain (each goldens-identical); B11 depends on A4. **File
ownership:** `CSVM/src/Mech3/Anim/TemplateStage.cs` (new), `CSVM/src/Mech3/AnimRuntime.cs`,
`CSVM/src/Session/WorldEffectsFactory.cs`, a new `CSVM.Tests/TemplateStageTests.cs`.
[`PLAN-puffer-interface.md`](PLAN-puffer-interface.md) and
[`PLAN-engine-free-suites.md`](PLAN-engine-free-suites.md) are both completed
(2026-08-06) — no longer a live contention.

---

# Wave A — decisions, then the stage

## A1 ☑ Grilling session — settled 2026-08-06

All nine questions put to the user one at a time, recommendation first; every recommendation was
accepted. Two carried substantive findings the code-read forced: the draft's 13-member list was
incomplete — the **BL-288 caller-slot trio** (`AssignCallerSlot`/`AssignedCallerSlot` + two
fields) is consulted by `TemplateRootsFor`/`TemplateRootsOf` and joins the move (Q2) — and the
draft's "two supplied hooks" undercounted: generic `TNode` honestly needs ~8 (Q3), each one line
in the engine adapter. Verifications performed during the session: the sealing-leak cite drifted
`:391` → `:441–442` (⚠ #4 in action); `PoolSlotMeta` has no reader outside `SlotOf` and no
probe/lab keys on it (Q5); the two 0.25 f tolerances are one rule asked through two root
resolutions, so ⚠ #5's precondition for merging them is met (Q6). Q9 tightened the draft's
instrument from "the `c1-destroy-effects` golden" to the FULL 13-golden sweep — the puffer A3
migration's lesson (the moving golden was `c1-flight`, the unexpected one). The Decisions table
above holds each call with its losing options.

## A2 ☑ `TemplateStage<TNode>` — slots, placement, identity, with the off-engine suite

**Goal.** The slot/placement/identity half of the cluster lives in
`src/Mech3/Anim/TemplateStage.cs`, generic over the node type, with `TemplateStageTests` asserting
cursor wrap, modulo fallback, recycle counting and the "which copy is mine" rule off-engine.
`AnimRuntime` compiles and behaves identically (13/13 goldens).

**Evidence (confidence: traced).** Member and call-site citations in the header paragraph; the
churn record is the BL list there. Precedent for the shape and for "Node-typed but never
dereferenced": `MotionSet`'s and `NameResolver`'s architecture entries.

**Approach.** Move `SlotOf` / `NextPooledAnchors` / `PlaceTemplateAt` / `PlaceTemplateOn` /
`TemplateRootsFor` / `TemplateRootsOf` / `TemplateIsAt` / `TemplateSharedWithLiveInstance` /
`IndexPooledCopy` plus `_poolCursor` / `_slotOfNode` / `PoolRecycles` **and the caller-slot trio
`AssignCallerSlot` / `AssignedCallerSlot` + `_callerSlots` / `_callerSlotCursor` (Decision 2)**,
exactly as they are — resist improving logic mid-move. `IndexPooledCopy`'s body stays two calls
into runtime services (`IndexWorld`, `ApplyResetStatesWithin`) supplied as hooks;
`ResolveLibraryRoot` stays outside. Wire per Decision 4's construction call, `findAll` late-bound
at the handover (Decision 7).

**Model recommendation.** high — the hottest file in the repo; the blast radius is every effect.

**Verify.** `.\RunTests.ps1` green, goldens identical; `effects-census` verdicts and
`PoolRecycles` A/B'd identical against HEAD on a `--debug-anim` chapter run.

**⚠ Traps.** ⚠ #2/#3/#5 above. `IndexPooledCopy`'s per-copy indices are deliberately NOT shared
across pooled copies — port the cache boundaries as they are (same trap class as
`PLAN-name-resolver` ⚠ #3).

## A3 ☑ Reveal / retire / sweep — one ritual, two entry points

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

## A4 ☑ Close the sealing leak — landed 2026-08-07

**Goal.** `PlaceCalledTemplates` / `ShowPlacedTemplates` / `PooledTemplates` are constructor
state; `WorldEffectsFactory.cs:441–442`'s post-seal writes are deleted (the cite drifted from
`:391`, ⚠ #4 in action — as did the crash rig's own `:312`, which this item found and deleted
too); the accepted-shallow-spot ⚠
is removed from `AnimRuntime`'s architecture entry and the new `TemplateStage` entry records the
closure.

**What landed.** The three flags are `TemplateStage<TNode>` constructor state, get-only
(`Pooled`/`Shown`/`Places`) with no setter anywhere; `AnimRuntime` gained
`NewTemplateStage(pooled, shown, placesCalled, debugMotions)` — the one place the Godot adapter
hooks are spelled — plus an `AnimRuntime(TemplateStage<Node3D>)` ctor, with the parameterless one
delegating to an inert all-off stage. `ForEffects`/`ForCrashRig` take the stage as their first
argument and bake three role flags instead of four. The one caller the draft's construction census
missed is the anim lab: `GameSession` wrote `PlaceCalledTemplates = true` onto the WORLD runtime
after `Bind`, which became `WorldSession.Options.PlacesCalledTemplates = _spec.AnimLab` beside the
existing `AutoStart = !_spec.AnimLab` — behaviourally identical because a quiet-stage bootstrap
dispatches only RESET_STATEs, which are `instant` and never reach the relocation test. The
off-engine suite's harness takes the role as constructor parameters rather than flipping `Pooled`
mid-test, which is the production shape.

**Evidence (confidence: traced).** The ⚠ itself, quoted in the header paragraph.

**Approach.** Per A1 Q4. The factory's build order changes only in when the flags are supplied,
not in what any consumer observes.

**Model recommendation.** medium — small, but ordering-sensitive; read `WorldEffectsFactory`'s
entry first (runtime ownership is split by design there).

**Verify.** As A2. Also grep for any other reader of the three flags — the census must find none
outside the module.

# Wave B — the ride-along

## B11 ❌ `EnsureWorldEffects` stops re-taking what the session already gave it — no-go 2026-08-07

**The call (Decision 9's gate).** No-go; no code landed. Three findings, each independently
sufficient, all re-verified against the tree at `79945b4` rather than taken from the citations
below (⚠ #4):

1. **The ride does not exist.** Decision 9 kept B11 alive *only* because it rides A4's wiring.
   It does not: A4's handover (`WorldEffectsFactory.cs:444–449`) builds the stage from `_spec`
   alone — `_spec.DebugAnim`, plus `_spec.Players` for the slot depth — and not one of
   `EnsureWorldEffects`' four world parameters reaches it. A4 changed `ForEffects`' argument list;
   `EnsureWorldEffects` and its four call sites are untouched by it. The item's sole stated reason
   for being in this plan is false at execution.
2. **The bug the signature suggests is not in the program.** The Evidence's claim is confirmed and
   is *stronger* than it reads: `GameSession.cs:665–667` assigns `state.CrashProgram =
   session.Program`, `state.WorldScene = session.Builder.Scene`, `state.WorldRuntime =
   session.Runtime`, and the damage lab's locals (`:755–757`) are copies of those same references
   taken after that line. All four sites (`:719`, `:761`, `:1340`, `:1564`) therefore pass one and
   the same five objects. "The winning argument triple depends on which caller got there first" is
   true of the signature and false of the code — whoever wins, wins with identical arguments. The
   `BL-232` defect was the *cache-population* hole, and `PLAN-deepening` F17 closed it by making
   `BuildWorldEffectsRuntime` private. There is no live defect here to remove.
3. **Every fold shape costs more than it buys.** The factory is constructed at `GameSession.cs:249`,
   ~410 lines before those values exist. So the params can only move onto it as (a) a
   `Bind`/`Attach` two-phase birth — literally Decision 4's rejected losing option, and it
   re-expresses the ordering dependence this item exists to delete, in a worse form: the damage
   lab's site is a **lambda** (`:761`) fired on the first damage action, so "did anyone attach
   before the first demand" becomes a live implicit contract whose failure is a null-ref rather
   than four identical objects; or (b) moving the factory's construction after the world build,
   which forks it across the world / empty-stage / plane-only paths and breaks the "constructed
   once per session, same lifetime as `LiveryResolver`/`SpawnPicker`" invariant the teardown story
   hangs on. Both are session-construction restructuring in the second-hottest file, under this
   plan's zero-delta rule, for a signature that would read shorter and behave identically.

**What survives the no-go, for whoever asks again.** The interesting version is not this item: the
crash rig re-takes the *same* four objects a second time through `FlightRigAssembler.Inputs`
(`GameSession.cs:1402–1409` → `FlightRigAssembler.cs:348–349`), so a factory that owned the
world's build inputs would shorten two signatures, not one. That is a construction-order
deepening of `WorldEffectsFactory`, needs its own grilling, and hits the same `:249`-vs-`:665`
wall — it is not a ride-along on a template-stage plan. Recorded in `architecture.md`'s
`WorldEffectsFactory` entry as the dead end, per the ground rules.

**Goal (as proposed).** The 6-param, 4-call-site, call-order-dependent `EnsureWorldEffects(gamez, worldScene,
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
