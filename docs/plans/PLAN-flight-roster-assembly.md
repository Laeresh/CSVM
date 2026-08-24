# Flight-roster assembly deepening

**COMPLETE** (2026-08-24). Archived as implementation evidence; read as history.

This behavior-preserving refactor makes `FlightRoster` own aircraft assembly, identity, membership,
later spawns, population updates, rollback, and teardown. It replaces `HumanFlightAdapter.Inputs`
and removes per-controller completion wiring from `GameSession`.

The full-tree review and 32-decision grilling on 2026-08-24 are its source; no backlog items move.
World/archive construction, pane creation, and gameplay-mode selection remain outside the roster.
The work runs in `worktree-flight-roster-assembly`, separately from flight-model parity.

**Outcome.** `FlightRoster` now owns human and AI assembly transactions, membership, identity,
target-source updates, rollback, and membership teardown through grouped dependencies and two
internal assemblers. The full gate passed with 2,003 units, 94 engine suites, and 16 unchanged
goldens; no backlog item moved.

## Milestone goal

- Humans and every AI enter through one roster transaction.
- Cohesive owner/lifetime inputs replace the mutable mega-bag and complete `SessionSpec`.
- Fixed invariants are complete at return; late populations update the roster once.
- Tests use the production seam and prove commit and rollback.
- Ordered human rigs remain readable while roster mutation and identity stay private.

**Behavior is invariant.** Optional fallbacks, deterministic order, logs, suites, and golden hashes
remain unchanged; discovered bugs become backlog items.

## Decisions (2026-08-24)

| # | Question | Decision |
|---|---|---|
| 1 | Behavior? | **Pure refactor.** |
| 2 | Scope? | **Own the complete transaction and completion wiring.** |
| 3 | Humans/AI? | **One owner, two internal paths.** |
| 4 | Start boundary? | **After world, archives, effects, and mission data exist.** |
| 5 | Late actors? | **Fixed invariants at return; populations update the roster.** |
| 6 | Tests? | **Use the production roster seam.** |
| 7 | Missing facts? | **Mandatory fails; legitimate absence is explicit.** |
| 8 | Result? | **Roster aggregate, not parallel mutable lists.** |
| 9 | Late spawns? | **All enter through the roster.** |
| 10 | Identity? | **Roster-owned.** |
| 11 | Teardown? | **Roster removes membership; session disposes shared resources.** |
| 12 | Abstract Godot? | **No; use engine suites.** |
| 13 | Failure? | **Transactional rollback.** |
| 14 | Rollback scope? | **Humans all-or-nothing; AI per aircraft.** |
| 15 | Hide rigs? | **Expose an ordered read-only human view.** |
| 16 | Build panes? | **No; adopt resolved slots.** |
| 17 | Immutable mega-context? | **Reject; group by owner and lifetime.** |
| 18 | Pass `SessionSpec`? | **No; translate resolved policy.** |
| 19 | Create modes? | **No; bind session-owned state.** |
| 20 | Starts? | **Roster owns the batch.** |
| 21 | Liveries? | **Roster-owned and deterministic.** |
| 22 | Degraded modes? | **Preserve explicit optional omissions.** |
| 23 | Success? | **Interface and locality, not line count.** |
| 24 | Landing? | **Green slices without a lasting compatibility path.** |
| 25 | One assembler? | **Separate internal human and AI assemblers.** |
| 26 | Spawn result? | **Return the configured controller.** |
| 27 | GameSession AI overloads? | **Delete; no external callers.** |
| 28 | Target plugins? | **No; one operation for one real source.** |
| 29 | Rollback implementation? | **Reverse compensations, no staging parent.** |
| 30 | Aggregate name? | **`FlightRoster`.** |
| 31 | Parity plan? | **Separate plan and worktree.** |
| 32 | Worktree outcome? | **Ready-to-land commit; merge separately.** |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | Roster already completes membership. | `GameSession.cs:1859–1869` and `:2204–2213` post-wire controllers. |
| 2 | Immutable `Inputs` is enough. | `HumanFlightAdapter.cs:528–622` mixes forty unrelated facts. |
| 3 | GameSession needs AI overloads. | Every invocation is inside `GameSession.cs`. |
| 4 | Split all GameSession. | Its job is orchestration; roster assembly is the traced leak. |
| 5 | Fake the scene tree. | No second real adapter exists. |

| Confidence | Items | Meaning |
|---|---|---|
| **Traced** | A1–D6 | Confirm the cited paths, then implement. |
| **Direction sound** | E7 | Suite placement follows catalog locality. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from `backlog.md` (not marked FIXED there). New decodes
  land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☑ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — pin the transaction
1. ☑ Characterize roster commit, failure, identity, and optional fallbacks
2. ☑ Replace the mega-bag with cohesive roster dependencies and policy

### Wave B — roster-owned humans
3. ☑ Make `FlightRoster` own human membership and completion wiring

### Wave C — one AI entry path
4. ☑ Extract the internal AI assembler and migrate every late spawn

### Wave D — population and lifetime
5. ☑ Move zeppelin target population behind the roster
6. ☑ Add rollback and roster-owned membership teardown

### Wave E — close the old surface
7. ☑ Delete obsolete paths, update architecture, and verify

## Dependency and parallelism notes

Items run in listed order; no parallelism. A2–D6 share the roster/session construction seam.

---

# Wave A — pin the transaction

## A1 ☑ Characterize roster commit, failure, identity, and optional fallbacks

**Landed.** The `flight-roster-transaction` engine suite forces late human and AI failures, proves
external bindings plus paint/pilot/RNG state are restored, then proves ordered human and AI commit,
optional smoke omission, identity reuse, membership teardown, and session-owned node cleanup.

**Goal.** An engine suite observes complete humans, one late AI, stable identity/order, explicit
optional omissions, and no partial membership after a forced failure.

**Evidence (confidence: traced).** Tests reconstruct `HumanFlightAdapter.Inputs` at
`Testing/InstantActionSuites.cs:114–130` and other sites. Production enters through
`FlightRoster.BuildPlayers` and `SpawnAi`, but no suite treats the transaction as its surface.

**Approach.** Add focused roster assertions in the in-engine session suite family. Record successful
outcomes and an able-to-fail completion invariant; do not invent an off-engine Godot adapter.

**Model recommendation.** high — distinguish construction behavior from incidental scene state.

**Verify.** Run the focused suite through `RunProbe.ps1`, prove its control fails, then build.

**⚠ Traps.** Helper-fragment tests miss ordering. Set `CSVM_DATA_ROOT=Z:\CSVM` in this worktree
or engine suites and goldens silently skip (LOG-17).

## A2 ☑ Replace the mega-bag with cohesive roster dependencies and policy

**Landed.** `FlightRosterPolicy`, `AircraftAssemblyResources`, `FlightWorldBindings`, and
`HumanRosterBindings` replaced the mutable `Inputs` bag and whole-`SessionSpec` handoff.

**Goal.** No caller constructs `HumanFlightAdapter.Inputs` or passes complete `SessionSpec`;
mandatory and optional dependencies are structurally distinct.

**Evidence (confidence: traced).** `Inputs` spans aircraft data, input/menu, modes, world,
archives, effects, and debug policy at `HumanFlightAdapter.cs:528–622`, populated in one mutable
initializer at `GameSession.cs:1805–1854`.

**Approach.** Introduce cohesive values grouped by owner/lifetime and narrow resolved policy. Reuse
deep owners instead of copying state. Migrate constructor and tests together.

**Model recommendation.** high — fan-out and nullability semantics make this judgement-heavy.

**Verify.** Build and focused suites; grep proves both broad inputs are gone.

**⚠ Traps.** An immutable forty-field context is rejected. Null and empty pad bindings differ;
optional sound, HUD, and effects omissions stay behavior-identical.

# Wave B — roster-owned humans

## B3 ☑ Make `FlightRoster` own human membership and completion wiring

**Landed.** The roster commits the ordered human view only after the batch succeeds and binds
smoke, pause, and target population before returning.

**Goal.** The roster adopts pane slots, builds humans atomically in deterministic order, retains a
read-only view, and completes fixed controller wiring before return.

**Evidence (confidence: traced).** `GameSession` owns mutable `_rigs` at `GameSession.cs:71`
and post-build loops at `:1859–1869`, while many legitimate consumers read ordered rigs.

**Approach.** Move membership mutation into `FlightRoster`; expose a read-only view. Keep pane
creation in `GameSession`, start selection in the roster, and human detail in an internal assembler.

**Model recommendation.** high — construction order and UI bindings have broad blast radius.

**Verify.** Roster, splitscreen, and Instant Action suites; stable starts, liveries, and summary.

**⚠ Traps.** Do not absorb `SplitScreen`, modes, weather, or archives. Avoid pass-through methods.

# Wave C — one AI entry path

## C4 ☑ Extract the internal AI assembler and migrate every late spawn

**Landed.** `AiFlightAssembler` owns pilot and aircraft detail; command-line, mission, wave, and
generator producers now pass `AiSpawn` directly to the roster. The GameSession overloads are gone.

**Goal.** Mission AI, generators, scripts, and waves share roster identity and one atomic AI path;
`GameSession.SpawnAiAircraft` is gone.

**Evidence (confidence: traced).** AI assembly is `FlightRoster.cs:59–163`; every forwarding
overload and call is inside `GameSession.cs:320–358,2100,2150–2157,2251`.

**Approach.** Put detailed AI construction in an internal assembler invoked by the roster. Translate
callbacks to `AiSpawn`; retain the completed controller result and per-aircraft isolation.

**Model recommendation.** high — effects, loadout degradation, and generators must stay equivalent.

**Verify.** AI actor/damage/generator/Instant Action suites plus one-entry-path census.

**⚠ Traps.** Loadout bind failure still warns and produces an unarmed AI. Never roll back a running
roster because one optional AI spawn failed.

# Wave D — population and lifetime

## D5 ☑ Move zeppelin target population behind the roster

**Landed.** `SetTargetSubParts` applies the one zeppelin source to current humans and every later AI
without exposing per-controller wiring to GameSession.

**Goal.** GameSession announces one live zeppelin source; the roster applies it to current and future
human aircraft without a generalized plugin system.

**Evidence (confidence: traced).** `GameSession.cs:2204–2213` loops every rig after zeppelin build;
no second subpart provider exists.

**Approach.** Add one roster population operation and remove the GameSession controller loop.

**Model recommendation.** medium — narrow fan-out after roster ownership exists.

**Verify.** Targeting and zeppelin suites for every pane and the pre-provider state.

**⚠ Traps.** Do not build a registry framework for one provider.

## D6 ☑ Add rollback and roster-owned membership teardown

**Landed.** Human builds roll back as a batch, AI builds roll back per aircraft without consuming
identity, and session exit releases roster membership/bindings without disposing shared resources.

**Goal.** Failed initial assembly removes every node/registration from the attempt; member removal
clears roster registrations without disposing shared session resources.

**Evidence (confidence: traced).** Construction mutates the tree and pools incrementally in
`HumanFlightAdapter.cs:431–510` and `FlightRoster.cs:59–163` with no transaction owner.

**Approach.** Record reverse-order compensations while assembling, then commit or unwind. Centralize
member removal while leaving archives, world runtime, and projectile disposal in GameSession.

**Model recommendation.** high — Godot timing and registration rollback hide state.

**Verify.** The A1 failure control proves zero surviving nodes/registrations; run respawn/targeting.

**⚠ Traps.** No staging parent. `QueueFree` is deferred; distinguish queued nodes from membership.

# Wave E — close the old surface

## E7 ☑ Delete obsolete paths, update architecture, and verify

**Verified.** No live reference remains to `HumanFlightAdapter.Inputs` or
`GameSession.SpawnAiAircraft`; the final `RunTests.ps1` pass reported 2,003/2,003 units, 94/94
engine suites with clean errors, and 16/16 hash-identical goldens.

**Goal.** Only the roster mutates membership; bags, overloads, post-wiring loops, and transition
paths are gone, and docs state final ownership.

**Evidence (confidence: direction-sound).** The full-tree review and completed controller deepening
plan isolate this session seam; reopening large modules by size is out of scope.

**Approach.** Remove transition code, tighten visibility, update the three architecture entries and
PROJECT_CONTEXT status, archive the completed plan, and leave parity/backlog untouched.

**Model recommendation.** high — final review must catch any alternate construction path.

**Verify.** Set `CSVM_DATA_ROOT=Z:\CSVM`; run `RunTests.ps1` and confirm non-zero suite/golden
counts, all hashes unchanged. Run comment caps and review the branch against plan and standards.

**⚠ Traps.** Worktree exit 0 without engine/golden counts is not verification (LOG-17). Repin no golden.
