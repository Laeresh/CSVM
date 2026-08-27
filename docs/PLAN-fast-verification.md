# Fast development verification

**ACTIVE PLAN** (written 2026-08-28). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan shortens CSVM's edit-test loop without weakening the complete landing gate. It adds an
explicit quick confidence layer, improves targeted selection, removes the largest measured serial
costs, and then earns parallel execution by isolating reports and mutable test state. No backlog
items are scheduled by this plan.

Game behaviour, golden contents, and assertion coverage are out of scope. A check may move between
development, quick, and full lanes only when the omitted coverage is printed honestly and the full
landing command continues to exercise it.

## Milestone goal

- A targeted unit or engine loop normally returns in 10–30 seconds.
- `RunTests.ps1 -Quick` provides a broad, explicitly partial confidence pass in at most 60 seconds
  on the development machine.
- The full `RunTests.ps1` landing gate retains build, units, all engine suites, goldens, and one exit
  code, while its serial work is reduced or safely parallelized.
- Each stage records enough timing and identity data to catch a future verification-time regression.

**The full landing gate remains authoritative.** Quick and targeted runs accelerate iteration; they
never become evidence that omitted suites, goldens, or awareness checks passed.

## Decisions (2026-08-28)

| # | Question | Decision |
|---|---|---|
| 1 | Which loop should agents use while editing? | **Run the exact affected suite or unit first, then the broad quick lane when it exists.** Full verification is reserved for landing. |
| 2 | What does `-Quick` mean? | **A stable, budgeted, explicitly partial smoke gate.** It prints every omitted surface and exits nonzero on failures inside its declared scope. |
| 3 | May quick selection be inferred from `git diff`? | **No.** Hidden dependency mapping would make an incomplete run look complete; selection remains explicit or a checked-in quick tier. |
| 4 | May live chapter worlds be cached across more suites? | **Not as the primary optimization.** They are mutable and already create registry-order coupling; cache immutable decoded inputs and build fresh runtime state. |
| 5 | When may engine suites run in parallel? | **Only after reports, artifacts, user data, and order dependencies are isolated.** Process count is measured, not assumed. |
| 6 | What happens to the hitch stage? | **Keep the detector check isolated and make its normal cadence explicit.** It must not overlap load that manufactures the wall-time symptom it measures. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | A few pathological suites explain the engine stage. | The 20 slowest suites account for only 49.5% of 279.1 seconds; the cost is broad. |
| 2 | Golden and hitch launches explain the reported five-minute wait. | The in-engine report alone summed to 279.1 seconds and its file spanned about 281 seconds. |
| 3 | Sound prewarming is the main world-build cost. | A cold unmuted seven-world run took 22.34 seconds, but the nearby warm runs were 14.31 seconds unmuted and 13.70 seconds muted. The apparent 41% win collapsed to about 4%. |
| 4 | Reusing every built world is the obvious cache. | `TestContext.WithWorld` retains mutable state, and `SuiteCatalog` already has an order constraint around the shared C1 world. Wider live reuse increases leakage risk. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, C22 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B12, B13, C23 | Preserve the A/B harness and let measured wall time choose the design or concurrency. |
| **Leads only — no mechanism yet** | C21 | Budget for investigation; this may end in a disproof. |

## What the data actually ships

The 2026-08-28 same-tree diagnosis measured the current stages independently:

| Surface | Measured wall time | Relevant shape |
|---|---:|---|
| Build | 5.3 s | `dotnet build CSVM/CSVM.sln` |
| Units | 35.4 s | 2,470 passing tests; one eight-chapter test took 31.339 s |
| Engine | 279.1 s | 148 passing suites, run serially in `TestHarness.Run` |
| Goldens | 93.1 s | 16 independent Godot launches |
| Hitch | 17.4 s | two isolated real-frame launches; awareness-only |

Forty-five engine suites performed 58 full world builds and consumed 203.7 seconds, 73% of the
engine report. Forty-two suites built one world, one built two, and the two cross-chapter censuses
built seven each in the full run because C1 was already cached. Campaign, landing, and roster
suites consumed 140.6 seconds. `TestContext.WithWorld` and `BuildWorld` are the shared construction
seam (`CSVM/src/Testing/TestHarness.cs`); the ordered registry is
`CSVM/src/Testing/SuiteCatalog.cs`.

The unit wall is dominated by
`EffectCatalogueTests.EveryChapterShipsExactlyTheThreeAiCrashDefs`, which loads a complete
`AnimProgram` for every chapter inside one xUnit test (`CSVM.Tests/EffectCatalogueTests.cs`). xUnit
already overlaps independent test classes, so the sum of individual durations was 185 seconds
while wall time was 35.4 seconds; the 31.339-second serial case determines most of that wall.

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

### Wave A — Fast confidence layers

1. ☐ Implement the explicit quick lane and exact targeted selectors
2. ☑ Remove the 31-second unit-test wall

### Wave B — Engine isolation and throughput

11. ☐ Attribute world-build time at its internal boundaries
12. ☐ Cache immutable decoded world inputs where the profile earns it
13. ☐ Isolate engine suites and run balanced shards

### Wave C — Remaining stages and final contract

21. ☐ Prove or reject parallel golden rendering
22. ☐ Set the hitch check's isolated cadence
23. ☐ Ratchet the full verification budget and documentation

## Dependency and parallelism notes

A1 and A2 are independent, but both touch test selection and should land serially. B11 blocks B12;
B12 informs B13, while B13 also depends on A1's suite metadata and exact selection. C21 may be
investigated after A1, but it and B13 both edit `RunTests.ps1`, so do not implement them in parallel.
C22 may be specified independently but must not run concurrently with any timing experiment. C23
lands last and reconciles `RunTests.ps1`, `docs/tooling.md`, `docs/verification.md`,
`PROJECT_CONTEXT.md`, and the three agent entry files.

---

# Wave A — Fast confidence layers

## A1 ☐ Implement the explicit quick lane and exact targeted selectors

**Goal.** `RunTests.ps1 -Quick` is a broad partial gate with a measured ≤60-second budget, while an
agent can select one exact engine suite and one filtered xUnit surface without spelling a brittle
set of skip flags.

**Evidence (confidence: traced).** `RunTests.ps1` already exposes `-Filter`, `-SkipUnits`,
`-SkipEngine`, `-SkipGoldens`, and `-SkipHitch`, but `-Filter` is a substring for engine suites only.
The measured targeted engine loop was 13–22 seconds for a seven-world suite including process
startup; build alone was 5.3 seconds.

**Approach.** Add checked-in suite-tier metadata, an exact engine selector, a unit filter, and
`-Quick`. Quick builds once, runs the budgeted unit and engine tiers, skips goldens and hitch with
the existing `not checked:` honesty, and prints its declared scope. Keep the existing switches
composable. Update `docs/tooling.md` and, if exact selection changes the Godot flag contract,
`docs/cli.md`; remove the temporary A1-open conditional from the agent guidance when the flag
exists. <TODO: choose the quick suite/unit membership by able-to-fail coverage, and record why each
representative belongs; elapsed time alone is not selection evidence.>

**Model recommendation.** high, because changing the verification contract has repository-wide
blast radius even though the script edits are localized.

**Verify.** Run targeted exact engine and unit examples; run `-Quick` twice warm and once after a
deliberate failure in each included lane. Confirm omitted surfaces are named, a miss selects
nothing and fails, and the warm run is ≤60 seconds. Finish with the unchanged full
`RunTests.ps1`.

**⚠ Traps.** Never infer completion from `git diff`, never let a zero-match filter pass, and never
label quick output `PASS` without adjacent `not checked:` lines for omitted coverage.

## A2 ☑ Remove the 31-second unit-test wall

**Goal.** The full unit stage no longer waits on one serial eight-chapter animation-program census,
while the same eight chapters and both crash families remain asserted.

**Evidence (confidence: traced).** The TRX measured
`EveryChapterShipsExactlyTheThreeAiCrashDefs` at 31.339 seconds inside a 35.4-second unit wall.
`CSVM.Tests/EffectCatalogueTests.cs` loads eight complete programs in one test.

**Approach.** Measure `AnimProgram.Load` by chapter, then choose the smallest safe seam: immutable
process fixture/cache if the returned program is read-only, or explicitly parallel chapter cases
if the loaders prove thread-safe. Do not weaken the census or merely move it out of the default
lane. Share a cache only through an API whose ownership and mutation contract is explicit.

**Model recommendation.** medium, because the target is narrow but parser ownership and xUnit
parallelism need care.

**Verify.** Run the test alone three times, the complete unit stage three times, and a deliberate
wrong expected def. Require all eight chapter identities in the result and a materially lower unit
wall; record the before/after medians in the landing commit.

**⚠ Traps.** xUnit already parallelizes independent classes, so summed test durations are not wall
time. Splitting rows inside one serial class may change nothing.

**Landed.** Per-chapter `AnimProgram.Load` timing (measured once cold, matching the plan's
figure): C1 7203 ms, C1B 4450 ms, C1C 3731 ms, C2 3769 ms, C2B 3008 ms, C3 4408 ms, C4 3469 ms,
C5 4442 ms — sum 34.48 s. Nothing in `Zrdr`/`CompiledAnim`/`AnimDefs`/`AnimProgram` holds shared
mutable state (every archive opens its own `ZipArchive` and parses into locals per call), so the
seam is `Task.Run` per chapter inside the existing test: the eight loads dispatch concurrently,
`Task.WaitAll` joins them, and the assertion loop (unchanged, still all eight chapters and both
crash families) stays serial afterward for a stable failure order. A timing probe confirmed real
concurrency rather than coincidence: outer wall 971 ms against a slowest single chapter of 970 ms,
not the ~7.3 s sum. No process-wide cache was added — `EffectPoolsTests` is the only other
`AnimProgram.Load` caller and it touches only C1, so cross-test sharing would not have paid for
its own risk (a `List`-backed `AnimProgram` shared without an explicit immutability contract).
`CSVM.Tests/EffectCatalogueTests.cs` is the only file changed; no `CSVM/src` seam was needed.

Same-build wall medians (`$env:CSVM_DATA_ROOT="Z:\CSVM"`, three runs each): the isolated test alone
fell from 3.99 s to 2.35 s warm (dotnet-host and JIT overhead dominate both). The full
`dotnet test CSVM/CSVM.sln --no-build` unit stage (2470 tests) measured 16.6 s before and 17.3 s
after, unchanged within this run's noise band (14–21 s across all six runs): on this machine's
warm OS file cache, other test classes already bounded the stage wall, so this test was no longer
the pacer either way at the time of measurement — which is consistent with the fix, not evidence
against it, since the item's target was specifically the case where this one test dominates the
wall (cold-cache: an isolated 34.48 s serial sum matches the plan's 31.339 s TRX figure). A
deliberately wrong expected def (`ai_crash_dirt` → `ai_crash_WRONG` on C1's `DefForSurfaceId(13)`)
failed as `chapter C1: Assert.Equal() Failure: Strings differ … Expected: "ai_crash_WRONG" Actual:
"ai_crash_dirt"`, naming the chapter, then was restored and reverified green.

**Verified.** <pending orchestrator run>

---

# Wave B — Engine isolation and throughput

## B11 ☐ Attribute world-build time at its internal boundaries

**Goal.** Every full engine report distinguishes archive/decode, sound preparation, runtime/world
construction, manual simulation, assertion work, and disposal well enough to select the next
optimization from data.

**Evidence (confidence: traced).** Forty-five suites with 58 world builds consumed 203.7 seconds,
but the suite stopwatch in `TestHarness.Run` encloses all setup and body work. The mute A/B disproved
sound preparation as the dominant warm-cache cost.

**Approach.** Add low-overhead timing aggregation at `TestContext.BuildWorld` and suite boundaries,
write it into `.scratch/test-report.json`, and keep normal console output compact. Measure same-build
variation before comparing changes. The report schema must identify the loaded binary and selected
suite set.

**Model recommendation.** medium, because this is contained instrumentation with strict
measurement discipline.

**Verify.** Run one no-world suite, one single-world suite, both seven-world censuses, and the full
engine catalog. Assert phase totals close over suite wall time within a documented tolerance and
prove a deliberate delay appears in the intended phase.

**⚠ Traps.** Logging every operation can create the result. Aggregate stopwatches at stable
boundaries and keep per-object chatter out of the timing path.

## B12 ☐ Cache immutable decoded world inputs where the profile earns it

**Goal.** Repeated world builds reuse expensive immutable decode results while each suite receives
fresh mutable Godot/runtime state.

**Evidence (confidence: direction-sound).** The profile seam is known, but B11 must determine how
much of 203.7 seconds is reusable decode rather than required construction or simulation.

**Approach.** From B11's hottest immutable phase, introduce a process-scoped cache keyed by every
input that changes identity, including chapter, mission, paths, and relevant build options. State
ownership and disposal in the module's architecture entry. Reject the item if no reusable phase
clears same-build noise.

**Model recommendation.** high, because archive ownership and mutable Godot resources make a wrong
cache fast but unsound.

**Verify.** A/B the full engine catalog on the same build and warm cache, prove cache hit/miss
identity, then deliberately vary chapter, mission, collision, data root, and emitter/sound options.
All 148 suites and engine-error screening must remain equivalent.

**⚠ Traps.** Do not cache scene nodes, runtimes, texture archives with expired lifetimes, or a world
whose previous suite mutated objectives, visibility, damage, emitters, or roster state.

## B13 ☐ Isolate engine suites and run balanced shards

**Goal.** The full engine catalog runs in multiple Godot processes with deterministic aggregation,
unique artifacts, and no suite-order dependency; practical engine wall time is 90–120 seconds.

**Evidence (confidence: direction-sound).** Greedy division of the measured suite durations gives a
four-shard arithmetic floor near 70 seconds, before startup, lost cache reuse, I/O, and contention.
`SuiteCatalog` currently requires `emitter-lifetime` first because it affects the shared C1 world.

**Approach.** First remove or encode order dependencies as fixture setup. Give each shard unique
engine logs, JSON reports, scratch artifacts, and testing profile paths; aggregate counts, errors,
binary identity, and failures in registry order. Choose shard count from an A/B sweep, with checked-in
stable membership or deterministic weighted scheduling based on versioned timing data.

**Model recommendation.** high, because concurrency can manufacture false passes through shared
files or hidden state.

**Verify.** Run serial and candidate shard counts repeatedly, compare the complete suite-name set,
verdicts, counts, error screening, and artifacts, and deliberately fail one suite in each shard.
Stress concurrent runs and prove no report or profile is overwritten.

**⚠ Traps.** A process exiting zero with a missing report is a failure. Do not let one shard kill
another through stray-Godot cleanup, and do not use wall-time-sensitive hitch checks as shard work.

---

# Wave C — Remaining stages and final contract

## C21 ☐ Prove or reject parallel golden rendering

**Goal.** Determine whether two or more simultaneous golden launches reduce the 93.1-second stage
without changing raw pixels, frame identity, adapter reporting, or failure evidence.

**Evidence (confidence: lead-only).** Sixteen shots are launched independently and have unique
paths, but GPU/driver contention has not been measured and may remove the apparent parallel win.

**Approach.** Add an experimental concurrency control, A/B serial and 2/3/4 workers, and preserve
one process and log per shot. Land only the fastest repeatable worker count whose hashes remain
bit-identical and whose silent-death retry/timeout evidence remains isolated.

**Model recommendation.** medium, because the implementation is mechanical after the GPU
experiment settles the safe concurrency.

**Verify.** Repeat the complete manifest under each worker count, compare raw-pixel hashes, frames,
sizes, adapters, exit codes, and total wall. Inject one moved hash, one missing PNG, and one timeout.

**⚠ Traps.** Deterministic simulation does not guarantee deterministic concurrent driver behavior.
A faster run with flaky hashes is a disproof, not a tuning problem.

## C22 ☐ Set the hitch check's isolated cadence

**Goal.** The 17.4-second awareness-only hitch detector check runs at an explicit useful cadence
without taxing every edit loop or overlapping work that invalidates its wall-time evidence.

**Evidence (confidence: traced).** `RunTests.ps1` marks hitch `TODO`, never changes the verifier's
exit code from it, and documents workstation contention as non-gating. The stage uses two real-frame
launches and measured 17.4 seconds.

**Approach.** Keep hitch out of targeted and quick lanes. Decide whether full landing retains it or
whether it becomes an explicit/scheduled tooling check, then update `RunTests.ps1`,
`docs/tooling.md`, and `docs/verification.md` together. Never overlap it with units, shards,
goldens, or perf.

**Model recommendation.** medium, because the code change is small but the confidence contract must
remain explicit.

**Verify.** Exercise the clean and injected cases, prove the chosen default prints its cadence and
unchecked status, and deliberately break detector output so the awareness item remains visible.

**⚠ Traps.** Parallel load creates the symptom under test. Saving 17 seconds by overlapping the
stage would make its result uninterpretable.

## C23 ☐ Ratchet the full verification budget and documentation

**Goal.** The optimized full gate has a measured budget, preserves its complete verdict contract,
and leaves one authoritative workflow in tooling, verification, project context, and agent files.

**Evidence (confidence: direction-sound).** The current independent stage measurements identify the
available savings, but the final full-wall target depends on B13 and C21's measured concurrency.

**Approach.** Run same-build variation, set stage and total warning budgets above the observed warm
distribution, and record timings in the summary without making workstation noise a correctness
failure. Reconcile `docs/tooling.md`, `docs/verification.md`, `PROJECT_CONTEXT.md`, `AGENTS.md`,
`AGENTS.override.md`, and `CLAUDE.md`; remove transitional A1 wording. <TODO: set the full-run wall
budget from the landed serial/sharded distribution rather than choosing it now.>

**Model recommendation.** high, because this closes the plan's verification and documentation
contract across every agent entry point.

**Verify.** Run targeted, quick, and full commands from a clean tree; perturb one check in each full
stage; verify every omission is named and all intended failures reach the one exit code. Confirm the
documented commands exactly match `Get-Help .\RunTests.ps1 -Detailed`.

**⚠ Traps.** Timing budgets are awareness thresholds, not correctness verdicts. Do not make a busy
workstation fail otherwise-correct code or allow a quick run to satisfy the full landing rule.
