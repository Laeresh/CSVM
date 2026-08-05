# Effect catalogue — one record of what is a playable effect, and what it needs staged

**COMPLETE** (written 2026-08-05 from that day's architecture review, candidate 1; decisions settled
in the same day's grilling session; completed the same day). Archived under `docs/plans/`; all three
items landed, the middle one as **Decision 1's documented fallback** — which turned out to be the
find of the plan rather than a detour. `A1` moved the names into `EffectCatalogue`; `B2` built
`StageRootsFor` and proved it against the hand tables on all 8 chapters at both binds, where derived
≠ hand table three ways: two are knowledge the tables encoded and the closure cannot see (now
`CallSuppliedAnchors`/`AirframeScopedAnchors`), and one is a genuine miss — `ballflare.flt` and
`apassengers` are roots the bound defs anchor on that **nothing stages**, so the torpedo explosion's
flare and the crash's passenger removal play nothing at all (`BL-262`). `B3` then deleted both hand
tables anyway: each bind stages the derivation **minus those two named gaps**, which reproduces the
shipped set exactly, and an anchor that resolves nowhere now fails the build naming the definition
and the node instead of playing nothing silently.

**Left open, deliberately: `BL-262`.** Staging either gap is a behaviour change with goldens to
re-pin, and this plan's rule was that an item which cannot keep 13/13 hash-identical stops rather
than repins. The gap lists in `EffectCatalogue` are that item's authoritative marker — deleting a
name there is now the whole fix — and `effects-census` fails in both directions if one rots.

One authored effect is currently five string tables, one JSON file, and string literals in three
caller modules, with nothing linking them: `WorldEffectsFactory.EffectAnimNames` (33 names,
`WorldEffectsFactory.cs:32-53`), `EffectStageRoots` (38 roots, `:102-118`), `EffectTemplateRoots`
(the crash rig's 11, `:78-85`), `PlaneDamageEffectAnims` (`:92-93`), `effect_pools.json`
(`EffectPools`), plus `GrazeReaction`'s three-way switch (`FlightController.cs:1757-1762`) and
`ImpactOutcome`'s gunhit-name formula. Both root tables are the *offline output* of
`analysis/effect-anchor-roots/`, and both carry the same comment admitting the failure mode: **"a
root left out leaves every def anchored on it unanchored, so it plays nothing at all."** The
`effects-census` suite (landed 2026-08-05, `4e4db08`) guards the *current* 33 names — but a newly
added effect with a missing root passes it (its row resolves, the tallies don't move), so the
silent-miss window is still open at the exact moment it matters: when someone adds an effect.

This plan turns that correspondence into a module: `EffectCatalogue` owns the names and derives the
staged-root set from the bound defs at build time, failing loudly where today's build plays nothing
silently. **No behaviour change is intended anywhere in this plan** — 13/13 goldens staying
hash-identical is primary evidence for every item, and an item that cannot keep them identical
stops rather than repins.

## Milestone goal

- Adding an effect is one edit: its name into `EffectCatalogue`, its pool size into
  `effect_pools.json`. The staged-root set derives from the bound defs; a def whose anchor cannot
  be staged fails the build with a structured error instead of playing nothing.
- Both hand root-tables (`EffectStageRoots`, `EffectTemplateRoots`) are deleted, proven equal to
  the derivation first by a tripwire across all 8 chapters.
- Every producer of effect names — `ImpactOutcome`'s gunhit formula, the graze switch (now
  `TouchdownFor`), `PlaneDamageEffectAnims` — has a unit tripwire asserting its whole producible
  range is in the catalogue.

**The sealed builder and the pool stay exactly as they are.** `BuildWorldEffectsRuntime` stays
private behind `EnsureWorldEffects` (`BL-232` — the catalogue changes *what* is staged, never *who
may build*), and pool sizes stay in `effect_pools.json` per the "INVENTED, not decoded" ⚠
(`WorldEffectsFactory.cs:65-69`) — the catalogue references that file, never absorbs it.

## Decisions (2026-08-05)

Settled in the grilling session. This table is the authority where prose disagrees.

| # | Question | Decision |
|---|---|---|
| 1 | Does the staged-root table die, or get guarded? | **Die — the root set derives from the bound defs at build, with an equality tripwire as the transition proof.** Curation stays in `EffectAnimNames` (every exclusion — `b_steamtrail`, `random_gun_impact`, the LOCAL_CHOREOGRAPHY fail-closed set — lives at the *names* level); the mechanical half (names → anchor-root closure) is computed at build the way `analysis/effect-anchor-roots/` computed it offline, failing loudly on an anchor that resolves nowhere. The migration commit asserts derived == hand table; if a mismatch surfaces a subtlety the analysis knew, **stop at the guarded-table shape and document the mismatch** — that fallback is a recorded success, not a failure. Losing option: keep the hand table + bind-time cross-validation only — safer, but the analysis re-derivation step survives and the table can drift stale in the other direction. |
| 2 | Typed catalogue entries through the play seams, or strings? | **Strings stay at the seams; the catalogue closes drift with producer-range tripwires.** Every shipped failure in this family was a root-omission or lifecycle bug, never a name typo, and typed entries would thread a Session type through the deliberately engine-free `ImpactOutcome` plus four delegate signatures. Instead: a unit tripwire per producer (ImpactOutcome's outputs across 48 weapons × surfaces — one more check in the existing B5 suite; the graze trio; `PlaneDamageEffectAnims`) asserting range ⊆ catalogue. Refinement taken: the graze switch becomes a pure `TouchdownFor(SurfaceClass)` living beside the names it must match. Losing option: catalogue entries in `EffectSink`/`GrazeEffectSink`/`ExternalEffect` — guarantees the same property at much higher churn. |
| 3 | World-effects side only, or both closures? | **Both.** The derivation is one function — *names + gamez → staged roots, or fail* — and the crash-rig bind (`player_crash_dirt`/`_water` + the four `PlaneDamageEffectAnims`) calls the same one, killing `EffectTemplateRoots` too. The uniform three-way rule: an anchor resolving to a parentless gamez root is staged; an anchor resolving within the bind's existing scope (crash scaffold `player`/`healthy`/`destroyed`/pieces, the plane's `pdpN` panels) is already satisfied; an anchor resolving *nowhere* fails the build. `CrashAnchorNodes` explicitly stays — a hand-built scaffold (the crash def's target set), not a closure output. Losing option: world-only — leaves one hand table alive, so the "which list do I update" confusion survives the refactor aimed at it. |
| 4 | Module shape | **`Session/EffectCatalogue.cs`**, static, owning: `EffectAnimNames` (with its curation comments — the exclusions are the load-bearing knowledge), the crash-side name sets, `TouchdownFor(SurfaceClass)`, and `StageRootsFor(program, names, resolveRoot)` returning the closure under Decision 3's rule with a structured error. `WorldEffectsFactory` becomes a pure consumer, keeping everything it owns that isn't naming: the sealed builder, `EffectPools`, `EffectRuntimeTtl`, `CrashAnchorNodes`, `BuildCrashAnchorSet`. Tests split by tier: producer-range tripwires + `TouchdownFor` engine-free in `CSVM.Tests`; the derivation-equality tripwire needs a bound program + gamez, so it runs as checks inside `effects-census` (world side) and the crash-rig path. `EffectStageRootNames` survives as a forwarding property only for `EffectPoolsTests`' json cross-check, now against the derived set. The term "catalogue" enters the vocabulary via the module's own `docs/architecture.md` entry: *the record of which authored anims are playable effects, and what their defs need staged.* |

## ⚠ Read this before implementing anything

| # | The claim to keep honest | What guards it |
|---|---|---|
| 1 | "The derivation is exactly the analysis output." | Unproven until B2's tripwire passes **all 8 chapters**. The hand tables were checked "all 8" offline (`WorldEffectsFactory.cs:101`, `:75-77`); the tripwire must reach the same coverage before B3 deletes anything. A mismatch is Decision 1's documented fallback, not a bug to force through. |
| 2 | "`effects-census` covers this." | Only for the current 33 names — its golden tallies (30/17) don't move when a new *rootless* effect is added. That is why the derivation must fail at build, not rely on the suite. |
| 3 | "The BL-061 one-instance-per-def caveat and the `graze.siteAtContact` A/B are resolved by this refactor." | They are not — both survive as data. `FlightController.cs:1747-1748`'s ⚠ and the Config-keyed site A/B (user judgement 2026-08-01) must read the same after every item. |

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
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
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

### Wave A — the module, engine-free half

1. ☑ Extract `EffectCatalogue`: names, crash name sets, `TouchdownFor`, producer-range tripwires

### Wave B — the derivation, and the tables die

2. ☑ `StageRootsFor` + the equality tripwire at both binds, proven across all 8 chapters —
   **landed as Decision 1's guarded-table fallback**, see B2's Outcome
3. ☑ The hand root-tables die; the derivation becomes the stage's source — **the derived set minus
   the named `BL-262` gaps**, see B3's Outcome

## Dependency and parallelism notes

Linear: A1 → B2 → B3; no intra-plan parallelism. **File ownership of this whole plan:**
`CSVM/src/Session/EffectCatalogue.cs` (new), `WorldEffectsFactory.cs`, `FlightController.cs`
(GrazeReaction only), `CSVM.Tests/` (ImpactOutcome/EffectPools test files), `Suites.cs`
(effects-census checks only). It contends with `PLAN-name-resolver` on **nothing** — that plan owns
`AnimRuntime.cs`/`Anim/` and adds no `Suites.cs` checks — so the two may run as parallel worktree
sessions.

**⚠ Baseline sensitivity to the active plan's D9 (`PLAN-m3-polish-7`, `BL-228`
`WAIT_FOR_COMPLETION`).** D9 expects goldens to move, and its 3,731 flagged events are all
`OnCall`/`WeaponHit` — largely the effect defs `effects-census` sweeps. A caller held until its
callee completes changes what happens inside the census's 30-tick window, so the 30/17 tallies
(and possibly `effect-template-mesh`'s timing assertions) may legitimately move when D9 lands.
Every "tallies unmoved" verify line in this plan means *unmoved against the HEAD this plan starts
from* — if D9 landed in between, re-measure the tallies first and treat a moved pinned number as
D9's doing to confirm, not this plan's to absorb silently. D10 (`BL-239`) is disjoint (owns
`Projectile.cs` + damage-suite rows; this plan's `Suites.cs` edits stay in the effects-census
region) — avoid running the two in the same worktree, nothing more.

---

# Wave A — the module, engine-free half

## A1 ☑ Extract `EffectCatalogue`: names, crash name sets, `TouchdownFor`, producer-range tripwires

**Goal.** The names and their producers' obligations live in one module; a name typo or a producer
emitting an uncatalogued name is a red unit test, engine-free.

**Evidence (confidence: traced).** `EffectAnimNames` + curation comments `WorldEffectsFactory.cs:26-53`;
`PlaneDamageEffectAnims` `:87-93`; the graze switch `FlightController.cs:1749-1766` (its ⚠ block
`:1744-1748` must survive verbatim); the gunhit formula in `Flight/ImpactOutcome.cs` with its
existing 48-weapon unit suite (`CSVM.Tests/ImpactOutcomeTests.cs`, the B5 battery — the range
tripwire is one more check there, not a new harness).

**Approach.** New `Session/EffectCatalogue.cs` (Decision 4). Move the name tables verbatim with
their comments; add `TouchdownFor(SurfaceClass)` and route `GrazeReaction` through it; leave every
delegate signature untouched (Decision 2). Add the three range tripwires. `WorldEffectsFactory`
consumes the catalogue's names.

**Model recommendation.** medium — mechanical code motion with a settled shape; the judgement was
spent in the grilling.

**Verify.** Full `.\RunTests.ps1` (goldens identical — this item is pure motion); the new unit
checks seen failing once each (e.g. drop a name from the catalogue, watch the producer tripwire go
red, restore).

**⚠ Traps.** `ImpactOutcome` stays engine-free and must not reference Session types — the tripwire
lives in the *test* project, which may reference both. The curation comments are the value: move
them, don't summarize them.

# Wave B — the derivation, and the tables die

## B2 ☑ `StageRootsFor` + the equality tripwire at both binds, proven across all 8 chapters

**Goal.** The catalogue computes the anchor-root closure of its names against a bound program and
gamez, under Decision 3's three-way rule; a tripwire asserts derived == hand table at the world
bind and the crash bind, and has been run against every chapter.

**Evidence (confidence: direction traced, derivation feasibility to confirm).** The hand tables are
the offline derivation's output (`analysis/effect-anchor-roots/`, cited at
`WorldEffectsFactory.cs:74-77`, `:95-101`). The closure machinery exists at runtime —
`AnimProgram.Subset(names)` is exactly the closure walk (`WorldEffectsFactory.cs:402`), and def
anchors are enumerable from `AnimDefinition`. **Confirm before building:** that the def's anchor
*names* (not resolved nodes) are recoverable per def in the subset — read `analysis/effect-anchor-roots/`'s
script first to mirror its exact rule.

**Approach.** `EffectCatalogue.StageRootsFor(program, names, resolveRoot)` where `resolveRoot` is a
caller-supplied lookup (world: parentless gamez root by name; crash: same, else
already-in-scope check). Wire the equality check as a suite condition inside `effects-census`
(world) and beside the crash-rig bind path. For the 8-chapter proof, script
`RunProbe.ps1 --run-tests=effects-census --chapter=<X>` per chapter (or a one-off `--dump-`-style
sweep) and record the result in the landing's HISTORY entry.

**Model recommendation.** high — the derivation rule has edge cases (Decision 3's three-way rule,
the analysis' filters) and a wrong reading here deletes the wrong table in B3.

**Verify.** Tripwire green on all 8 chapters at both binds; goldens identical; the tripwire seen
failing once (remove a root from the hand table copy, watch equality break).

**⚠ Traps.** A mismatch is Decision 1's fallback, not an obstacle: stop, keep the hand table +
validation, record why. The crash bind is per-plane (wreck subtrees vary) — run its check on at
least two airframes. Do not touch `EnsureWorldEffects`' seal or signature.

**Outcome (2026-08-05) — Decision 1's fallback, and it earned its keep.** `StageRootsFor` landed and
the tripwire is green on all 8 chapters at both binds (crash on `player_bhawk` + `player_pfighter`),
but **derived ≠ hand table**, three ways, and all three are knowledge the tables encode and the
closure cannot:

| # | Divergence | Verdict |
|---|---|---|
| 1 | `zep_can_dstry1.flt` (world) — `dblcannon_flying_parts`' own NAME | **Derivation corrected.** Every call reaching it carries an `AT_NODE` onto a zeppelin wreck whose subtree already has the `part1..8` it flings; C2's gamez has no node of the name at all, so a strict "resolves nowhere → fail" would break that one chapter. Curated out in `EffectCatalogue.CallSuppliedAnchors`. |
| 2 | `player_pfighter` (crash) — `plane_reset`'s anchor | **Derivation corrected.** Authored against the Devastator's own model root; on the other ten airframes the rig's scope has nothing of the name and the def is inert. No chapter's gamez carries the node, so there is no template either way. `EffectCatalogue.AirframeScopedAnchors`. |
| 3 | `ballflare.flt` (world) and `apassengers` (crash) | **The tables are wrong, and this is the find.** Both are single parentless roots in all 8 chapters that the bound defs anchor on with no `AT_NODE` to supply them — so both effects play nothing today. Staging them is a behaviour change B2 must not make; named as KNOWN gaps in `WorldEffectsFactory.WorldStageRootGaps`/`CrashTemplateRootGaps` and filed as `BL-262`. |

`ballflare.flt` is why the fallback was the right call: `analysis/effect-anchor-roots/anchor_roots.py`
keys definitions by `ANIMATION_NAME` and that definition declares only a `NAME`, so the offline
instrument never reached it — the derivation running against the *bound* program found a silent miss
its own source could not see (INSTR-11's shape again).

**What this means for B3.** The tables cannot simply die: deleting them deletes rows 1–3's curation
with them. B3's shape is now (a) close `BL-262` (stage both roots, re-pin whatever moves), then
(b) delete the tables, with rows 1 and 2 surviving as catalogue curation beside `EffectAnimNames`'
exclusions. The derived set is returned **sorted**, so `_pools.DepthFor`/`SlotsFor` iterating it will
not see the hand tables' authored order — B3's own ⚠ about slot assignment applies.

## B3 ☑ The hand root-tables die; the derivation becomes the stage's source

**Goal.** `EffectStageRoots` and `EffectTemplateRoots` are deleted; `WorldEffectsFactory` stages
what `StageRootsFor` returns; an unresolvable anchor fails the build with a structured error naming
the def and the anchor.

**Evidence (confidence: traced, gated on B2).** Staging sites: `BuildWorldEffectsRuntime`
(`WorldEffectsFactory.cs:359-386`, per-slot `BuildEffectStage` calls) and the crash-rig build
(`:249+`). `EffectStageRootNames` consumers: `EffectPoolsTests` (re-point at the derived set) and
the `effects-census` suite's staging helper in `Suites.cs`.

**Model recommendation.** medium — mechanical once B2's equality held.

**Verify.** Full `.\RunTests.ps1`, goldens identical, `effects-census` tallies unmoved (30/17);
the world-effects boot line ("N/N effect template(s) staged") reports the same counts as HEAD on a
`--effects-test` run; the structured error seen once (bind with a name whose anchor can't stage).

**⚠ Traps.** The pool-depth math (`_pools.DepthFor`/`SlotsFor` over the root list,
`WorldEffectsFactory.cs:372-382`) now iterates the derived set — order may differ from the hand
table; make staging order-insensitive or sort, because slot assignment must not shift (that would
move `c5-city-night`, the golden `BL-225`'s trap named). Docs sweep in the same turn:
`architecture.md` entries for `EffectCatalogue` (new, with the vocabulary line) and
`WorldEffectsFactory` (tables gone), HISTORY entry citing the 8-chapter proof.

**Outcome (2026-08-05) — landed as written, with B2's gap sets doing the work.** Both tables are
deleted. `EffectCatalogue.WorldStageRoots`/`CrashStageRoots` are the two bind-facing derivations —
the closure of the names each bind is about to bind, **minus** `WorldStageRootGaps`/
`CrashTemplateRootGaps` (moved into the catalogue, where they are now `BL-262`'s authoritative
marker) — and that subtraction reproduces the shipped staged set exactly, which is what B2's
tripwire had already proven. `WorldEffectsFactory` owns no root list at all: `BuildEffectStage`
takes what it is handed, `EffectStageRootNames(program, gamez)` and `CrashStageRootNames(program,
gamez, rigScope)` are thin forwards, and `WorldStageRootDrift`/`CrashStageRootDrift` (with their
`Drift` helper) are gone with the tables they compared against.

**The ordering trap: no shift, and the goldens are the proof.** Slot assignment turns out not to be
a function of list order at all — `AnimRuntime` reads a call's slot off the `PoolSlotMeta` of the
container above the anchor (`SlotOf`), and `NextPooledAnchors`/`TemplateRootsFor` pick by *slot
number*, never by index into the root list. The list order only decides sibling order inside one
`pool<N>` container, and every staged root name is distinct. So the sorted derived order was taken
as-is; the world build's own numbers are unchanged (`147/147` templates over `8` pool slots,
`33` names bound) and all 13 goldens — `c5-city-night`, `c1-crash`, `c1-destroy-effects`,
`c1-flight` included — stayed hash-identical. The only visible difference is cosmetic: the boot
line's size-group listing now reads `1× dum_gunhit/gunhit/mag_gunhit` instead of the authored
`1× gunhit/dum_gunhit/mag_gunhit`.

**One real find while wiring the crash bind.** Deriving the rig's roots needs the crash scaffold in
scope: `player` is the crash defs' own anchor and exists in no chapter's gamez, so the first
attempt (scope = the bare controller) reported the whole rig unanchorable and failed the three
plane-bearing goldens outright — the structured error working exactly as intended, on the wiring
rather than on the data. `BuildFlightCrashRuntime` now parents its `player` crash root **before**
deriving; both subtrees' child order is untouched (templates still before the wreck, crash root
still between the plane model and the runtime). The anim lab does the same trick in reverse — its
crash-anchor set is built first and parented after the templates.

`EffectPoolsTests`' two root-list checks are re-pointed at the derived set and are now
`ExtractedDataFact`s (they load C1's program + gamez and call the same forward), because there is no
hand table left to read without a bound chapter; they skip rather than pass on a checkout without
`extracted/`. `effects-census` stages the derivation instead of a table and asserts what still has
chapter-dependent content: every derived root builds, `effect_pools.json` sizes only roots this bind
stages, and `BL-262`'s gaps are still both asked-for and held back — the last one failing in **both**
directions, so the marker cannot rot into "the derivation stopped asking".
