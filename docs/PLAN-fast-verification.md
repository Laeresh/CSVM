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

1. ☑ Implement the explicit quick lane and exact targeted selectors
2. ☑ Remove the 31-second unit-test wall

### Wave B — Engine isolation and throughput

11. ☑ Attribute world-build time at its internal boundaries
12. ☑ Cache immutable decoded world inputs where the profile earns it
13. ☑ Isolate engine suites and run balanced shards

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

## A1 ☑ Implement the explicit quick lane and exact targeted selectors

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
exists.

**What landed.** `--run-tests=`'s value became a selector rather than a bare substring:
comma-separated terms, unioned and run in registry order, where `suite:<name>` is one exact suite,
`tier:<name>` a checked-in tier and anything else the old substring. `TestHarness.Select` is pure
over the registry and reports every term that matched nothing; `Run` refuses such a run before any
suite starts, so a typo cannot read as an empty pass. The Godot flag set is unchanged (still 133),
since the grammar rides inside the existing value.

`SuiteCatalog.QuickTier` is the checked-in engine membership, resolved through `Tier(name)`, and
`RunTests.ps1` gained `-Suite <name>[,<name>]` (exact), `-UnitFilter <expr>` (straight into
`dotnet test --filter`) and `-Quick`. Quick builds once, runs the quick unit tier
(`--filter Tier=Quick`, a `[Trait("Tier", "Quick")]` on each member class) and the quick engine
tier, skips goldens and hitch, prints its declared scope before it starts, and prints a
`not checked:` line for every omitted surface including its own partiality. A `-UnitFilter` matching
zero tests fails the units stage, the same rule the engine selector holds. `-Suite`/`-Filter` are
unioned with the engine tier, so quick plus the suite under edit is one command; `-UnitFilter`
replaces the unit tier, since the VSTest grammar can express a union itself. Every existing switch
still composes.

**Quick engine tier, and why each representative belongs** (coverage, not elapsed time; measured
warm seconds in brackets):

| Suite | The failure surface it is there to catch |
|---|---|
| `puffer-modes` [0.05] | the particle/emitter runtime end to end through a fake renderer, the one engine surface with no GPU dependency at all |
| `loadout-bind` [5.4] | every airframe model builds and every stock loadout's markers resolve: the plane-build path the whole flight half stands on |
| `weapons-fire` [0.5] | all 48 weapon defs mount and fire from a built plane, so a weapon-data or fire-control break is caught |
| `air-to-air` [1.7] | the hit chain: struck shape to data part, armour then health, the whole-vehicle kill rule and kill attribution |
| `instant-action` [2.5] | the Instant Action mission runtime and roster spawn, one of the two mission families |
| `ai-actor` [0.8] | the AI seam: an AI-piloted plane spawned into a running sim, flying orders, damageable and killable |
| `damage-stages` [2.2] | the authored `DAMAGE_SEQUENCE` ladder firing its stage effects across an HP sweep |
| `damage-hd` [3.6] | the built world's destructibles: hits destroy, swap meshes, drop colliders, and survive destroy/reset/destroy, with collision forced on |
| `effect-template-mesh` [0.06] | effect template meshes appearing at the call site and going dark on stop, the world-effects runtime's own tripwire |
| `collision-visibility` [11.4] | the only member that builds every one of the 8 chapters: a chapter that fails to build at all, and the invisible-wall class, are caught nowhere else in the tier |
| `target-selection` [0.02] | the targeting cycle and sticky selection, tree-free |
| `music-states` [0.3] | the state-driven score, the audio runtime's able-to-fail surface |
| `campaign-objectives` [0.02] | the campaign objective graph driven to both endings over a shipped mission's own script |

`emitter-lifetime` is deliberately excluded even though it is registered first: it installs the fake
emitter factory that the shared C1 world would then be cached with, and the `collision:true` rebuild
that undoes that for everyone sits far down the registry. Its registration order is untouched, so
the full run is unchanged. The unit tier is 193 tests over 14 classes covering the same idea from
the engine-free side: the CLI arg contract, the zrdr/gamez/anim/weapon readers, ballistics, plane
damage, the flight envelope, loadout and objective-graph models, campaign progression, AI target
ranking, the harness's own error screen, and the catalog/selector metadata itself.

**Measured** (warm, `$env:CSVM_DATA_ROOT` at the primary tree): `-Quick` 39.8 s and 35.6 s against
its 60 s budget (build ~6 s, units 2.1 s / 193 tests, engine ~32 s / 13 suites); one exact engine
suite 3.7 s; one unit class 2.0 s. A deliberate assertion break in each included lane turned that
lane red and the run exited 1; `-Suite weapons` (a substring, not a suite name) failed the engine
stage naming the term, and a `-UnitFilter` matching nothing failed the units stage.

**Model recommendation.** high, because changing the verification contract has repository-wide
blast radius even though the script edits are localized.

**Verify.** Run targeted exact engine and unit examples; run `-Quick` twice warm and once after a
deliberate failure in each included lane. Confirm omitted surfaces are named, a miss selects
nothing and fails, and the warm run is ≤60 seconds. Finish with the unchanged full
`RunTests.ps1`.

**⚠ Traps.** Never infer completion from `git diff`, never let a zero-match filter pass, and never
label quick output `PASS` without adjacent `not checked:` lines for omitted coverage.

**⚠ For B13 and C23.** The full engine stage measured 296.8 s of suite time on this machine and hit
`RunTests.ps1`'s own 300 s `$EngineTimeoutSec` watchdog, which kills Godot, writes no report and
fails the stage with exit 124. The suites that ran (146 of 153) all passed. Nothing here changed the
full run's selection, so this is the pre-existing serial cost the plan exists to remove; whoever
sets the budget decides whether the watchdog moves with it.

**Verified.** Full `RunTests.ps1` on the merged Wave A tree with no other engine work on the
machine: build 7.4 s, units 2475/2475 in 18.4 s, engine 153/153 in 253.1 s with errors clean,
goldens 16/16 hash-identical in 88.0 s, hitch clean/inject as expected in 16.4 s, 383.3 s total,
exit 0. `-Quick -Suite warning-shot` ran 14/153 in 30.1 s.

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

**Verified.** The same full battery as A1's: units 2475/2475 in 18.4 s against the plan's
measured 35.4 s, everything else green, exit 0.

---

# Wave B — Engine isolation and throughput

## B11 ☑ Attribute world-build time at its internal boundaries

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

**Landed.** `TestContext.BuildWorld` installs a private `StartupProfile` as `Current` for the span
of one build, so `WorldSession.Build`'s and `SessionArchives.OpenFor`'s own `Mark`/`Record` calls —
the same boundaries a real session's `[perf] startup` line reads, never a second set invented for
the harness — land on it. `PhaseAttribution.Categorize` (Godot-free, unit-tested without the engine
in `CSVM.Tests/PhaseAttributionTests.cs`) buckets those phases into archive/decode (`gamez`,
`textures`, `anim`, `zrdr`), sound preparation (`sounds`, `prewarm`) and runtime/world construction
(`world`, `clutter`, `bind`) against `BuildWorld`'s own outer wall-clock stopwatch, so the four
figures always sum to exactly what is attributed to the suite — no second clock to drift against the
first. Disposing a world a suite built is timed the same way; the shared cache's own end-of-run
teardown is reported once, in the run's totals, never against one suite. What is left of a suite's
wall time once build and disposal are subtracted is `rest` (manual simulation plus assertion work),
floored at zero, with any stopwatch overrun reported separately (`overrunSeconds`) rather than folded
into a falsely healthy zero. The console stays one line per suite (a no-world suite prints no phase
suffix) plus one totals line; `test-report.json` (schema bumped to 2) carries the same figures per
suite and as totals, plus the run's `selector` and the executing assembly's own path/MD5.

**Measured** (`$env:CSVM_DATA_ROOT="Z:\CSVM"`, warm, same tree as A1/A2's own verified numbers).
Same-build spread, two runs each of the two seven-world censuses in isolation:

| Suite | Run 1 | Run 2 | Spread |
|---|---:|---:|---:|
| `collision-visibility` | 14.34s (build 13.67s) | 14.48s (build 13.82s) | ~1% |
| `destructible-census` | 12.20s (build 11.84s) | 12.12s (build 11.76s) | <1% |

The full engine catalog (`.\RunTests.ps1 -SkipUnits -SkipGoldens -SkipHitch`, one run, 153/153
passed in 252.9s, matching A1's own 253.1s verified figure) attributed every suite's wall time:

| Phase | Seconds | % of engine wall (247.84s suite total) |
|---|---:|---:|
| Archive/decode (`gamez`+`textures`+`anim`+`zrdr`) | 23.39 | 9.4% |
| Sound preparation (`sounds`+`prewarm`) | 16.15 | 6.5% |
| Runtime/world construction (`world`+`clutter`+`bind`) | 63.19 | 25.5% |
| Unattributed build overhead | 5.95 | 2.4% |
| **Total inside `BuildWorld`** | **108.68** | **43.9%** |
| Disposing a suite-built world | 2.79 | 1.1% |
| Shared-cache teardown (once, end of run) | 0.05 | 0.02% |
| Manual simulation + assertion work (`rest`) | 136.37 | 55.0% |

45 suites made 58 world builds (both figures machine-checked against the report, matching the
plan's own count). Top suites by build time: `collision-visibility` 10.58s (7 worlds — one fewer
than standalone, since C1 was already cached), `destructible-census` 9.10s (7 worlds, same reason),
`campaign-persistence` 3.79s (2 worlds), `cutscene-letterbox` 2.68s, `self-ref-launch` 2.56s,
`emitter-lifetime` 2.36s, `zeppelin-identity` 2.36s, `campaign-cutscene` 2.27s, `fog-state` 2.26s,
`mission-radio` 2.26s — the remaining 35 single-world suites each spend 1.3–2.2s inside `BuildWorld`.

**⚠ dead claim 1 revised, not the same shape.** `PLAN-fast-verification.md`'s own dead-claim table
already showed "a few pathological suites" was wrong; this data shows the ⚠ table's 203.7-second
figure was itself a suite-wall-time proxy for world-build cost, not a measurement of `BuildWorld`
itself — the actual time inside `BuildWorld` across all 58 builds is 108.68 seconds (43.9% of the
suite wall those 45 suites consume), and more than half of what was attributed to "world build" is
manual simulation and assertion work running against an already-built world. A cache that reused
every immutable decode input has a ceiling of 23.39 seconds across the whole catalog (9.4% of engine
wall), not 73%: this is the number B12 measures its own win against, not the earlier estimate.

**Injected-delay check.** A temporary `Thread.Sleep(500)` placed inside `SessionArchives.OpenFor`'s
`textures` phase (restored afterward, confirmed by `git diff`, rebuild forced) moved `damage-hd`'s
`archiveDecodeSeconds` from 0.84s to 1.38s (+0.54s, matching the injected span) while `soundPrep`,
`runtimeConstruction` and disposal stayed within their own run-to-run noise band — the delay landed
in the intended category and nowhere else.

**Closure.** `phaseTotals.overrunSeconds` was 0.00s on the full 153-suite run: `buildSeconds +
disposalSeconds` never exceeded the summed suite wall time on any suite, so the four phase totals
plus the run's totals close over the measured wall time exactly (`PhaseAttribution.Rest`/`Overrun`,
proved in `CSVM.Tests/PhaseAttributionTests.cs` with synthetic inputs including a deliberately
overrunning case). No numeric tolerance was needed: the identity is exact by construction, since
`rest` is `wall − build − disposal` measured from the same suite-wall stopwatch `TestHarness.Run`
already keeps, not a second independently-collected figure.

**Verified.** Full `RunTests.ps1` on the B11+B12 tree with nothing else on the test desktop:
units 2490/2490 in 17.2 s, engine 153/153 in 235.4 s with errors clean and the totals line
reading build 89.2 s (decode 5.4 s, sound 16.0 s, rt 63.6 s, other 4.1 s) over 58 worlds,
goldens 16/16 hash-identical in 87.8 s, hitch clean, 364.0 s total, exit 0.

## B12 ☑ Cache immutable decoded world inputs where the profile earns it

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

**The per-phase split B11 left open.** A temporary per-build log line over `StartupProfile.Phases`
(reverted, confirmed by `git diff`) split the full catalog's 58 builds by raw phase name. The
23.46 s of archive/decode is two phases and only two:

| Raw phase | Seconds | What it is | Distinct keys in the catalog |
|---|---:|---|---:|
| `gamez` | 11.32 | `GameZ.Load` (`SessionArchives.OpenFor`) | 8 chapters |
| `anim` | 11.72 | `AnimProgram.Load` (`WorldSession.Build`) | 16 chapter+mission pairs |
| `zrdr` | 0.38 | `MissionSetup.Load` + the sound def/group load | — |
| `textures` | 0.04 | `new TextureArchive` — the constructor only indexes the zip | — |

By chapter (decode seconds / builds): C1 8.50/19, C3 9.21/23, C2 1.42/4, C5 1.36/3, C4 1.31/3,
C1C 0.59/2, C1B 0.58/2, C2B 0.48/2. The catalog is a C1+C3 workload; the other six chapters are
2.7 s of decode between them. Two-thirds of the remaining build cost is runtime construction
(`world` 37.34 s, `bind` 22.23 s, `clutter` 2.89 s), which is per-world by nature and out of scope
here. Charging each distinct key one decode and every repeat nothing predicted a 18.23 s ceiling,
9.65 s of it `gamez` and 8.58 s `anim`.

**What is immutable, and what is not.** `TextureArchive` and `SoundArchive` are disposable and
lifetime-bound to a build (`ArchiveIntent`), so neither is cached; nothing scene-side, runtime-side
or world-side is either. Of the two phases that matter:

- **`AnimProgram` — immutable after load, and now read-only by type.** Nothing in `CSVM/src` or
  `CSVM.Tests` writes a program, an `AnimDefinition`, a sequence or an event after `Load` returns;
  every write is inside a parser on an object it just built. Per-play state lives in
  `AnimRuntime`-owned tables keyed BY the def (`_invalidated`, `_everStarted`, `_inputGoverned`,
  `_washGates`, `_checkoutClosure`), never ON it — which is why one program can already be bound by
  a world runtime and several `Subset` stages at once. The `List` fields A2 warned about are the
  real hazard, so `Defs`, `StartAnims` and `MissionLibrarySkipped` became `IReadOnlyList` over
  private backing lists; that cost exactly one call site (`LandingApproachSuites.cs`, which used
  `List.Contains`).
- **`GameZ` — immutable in practice, not by type, with one in-place writer.**
  `EffectCycles.Apply` overwrites `CycleTextures`/`CycleSpeed`/`CycleLooping` on the materials it
  resolves. It is safe to share through because it `Clear()`s before writing and its source is the
  install-wide `effects.zrd`, so a second application over a shared instance writes the same values.
  `MissionSetup.BindPartitions` was the other suspect and does not touch the gamez: the resolved
  node indices land in the per-mission `MissionSetup`, so mission differences cannot leak through a
  shared chapter document. `WorldBuilder`, `SceneBuilder`, `ClutterBuilder`, `AircraftStage`,
  `GameSession` and every `TestWorld.Gamez` consumer hold it `readonly` and only read. Its own lazy
  memo fields (`_parent`, `_placed`, `_markerGizmo`) are unsynchronised, which is why the contract
  is single-threaded rather than merely read-only.

**Landed.** `CSVM/src/Mech3/DecodeCache.cs` is an instance-scoped store keyed by the absolute paths
a decode reads: `Gamez(path)` and `Anim(shared, chapterZrdr, missionZrdr, chapterAnim, missionAnim)`.
The paths are the whole key because they already carry data root, chapter and mission, and the
options that do not change what is decoded are deliberately absent from it — collision reaches
`WorldBuilder` after the decode, mute gates only the sound archive (never cached), and the emitter
factory and prewarm list act on the built runtime. It is opt-in: `SessionArchives.OpenFor` takes an
optional `decode` and `WorldSession.Options.Decode` defaults to null, so a game session retains no
chapter it has left and only the harness holds an instance (one per run, covering
`GameZ.Load` for the chapter and for `AircraftStage`'s planes archive, plus `AnimProgram.Load`).
`test-report.json` and the totals line carry `decodeCacheHits`/`decodeCacheMisses`, so a run shows
the cache hit rather than only that the wall time moved.

**Measured** (`$env:CSVM_DATA_ROOT="Z:\CSVM"`, warm, full catalog
`.\RunTests.ps1 -SkipUnits -SkipGoldens -SkipHitch`, one run per row, no other engine work on the
machine). Rows 2 and 4 are the A/B pair: same tree, the cache switched off at its two call sites
and back on, so the only variable is the cache.

| Run | `binary.md5` | Suite wall | Build | Decode | Hits/misses | Engine stage | Verdict |
|---|---|---:|---:|---:|---|---:|---|
| Baseline, cache absent | `c1c98578bc4741cb7b444d2f10fd04b7` | 245.27 s | 107.83 s | 23.46 s | — | 251.0 s | 153/153, errors clean |
| Adjacent baseline, cache off | `e88a63c4bdd6e562ff1234ff945e81dd` | 243.49 s | 107.09 s | 23.36 s | 0/0 | 248.7 s | 153/153, errors clean |
| Cache on | `8d5f9d3e70c901d7df095d4e95c82cd6` | 227.88 s | 89.16 s | 5.26 s | 102/25 | 234.7 s | 153/153, errors clean |
| Cache on, shipping shape | `9f1cac8af741dc4a8fe7d8ee61ffe8e8` | 227.82 s | 88.89 s | 5.23 s | 102/25 | 234.8 s | 153/153, errors clean |

Four distinct binaries, four distinct hashes (METHOD-6). Same-build spread is 0.7 % across the two
cache-off runs and 0.03 % across the two cache-on runs, against B11's ~1 % figure. The decode phase
falls 23.36 → 5.23 s, a drop of 18.13 s against the 18.23 s predicted from the key counts, so the
mechanism is confirmed and not merely correlated (PERF-3/PERF-5). Suite wall falls 15.67 s (6.4 %)
and the engine stage 13.9 s (5.6 %); the gap between 18.13 s of decode and 15.67 s of wall is
`rest` reading 133.69 s against 136.20 s, inside its own 133.7–136.4 s band across the four runs.
25 misses is exactly the distinct-key count the split predicted: 8 chapter gamez + 16 programs + 1
shared aircraft archive. The complete `.\RunTests.ps1` on the shipping binary
(`19b91809d089fa5e4c9ecb537e4d4dc0`) is green end to end: build 0.8 s, units 2490/2490 in 17.8 s,
engine 153/153 in 235.2 s with errors clean and the same 102/25, goldens 16/16 hash-identical in
87.9 s (`manifest.json` unmodified in the working tree, GOLD-9), hitch clean/inject as expected in
16.5 s, 358.2 s total against A1's verified 383.3 s, exit 0.

**Key discrimination.** `CSVM.Tests/DecodeCacheTests.cs` proves hit/miss identity directly: a
repeated key returns the same instance and counts a hit, and a changed data root, chapter or
mission each returns a different instance and counts a miss (the anim half needs no install, since
`AnimProgram.Load` tolerates absent paths, which is what lets data root be varied at all). The
`gamez` half runs against the install and separates C1 from C3. Collision and mute are proved
absent from the key from the other direction, since they must NOT miss: `collision-visibility`
builds all eight chapters with collision forced on after `destructible-census` built them without
it, and its decode reads 0.04 s (8 hits, 0 misses) while both suites stay green; mute never reaches
a cached call at all, because it gates only `sounds`/`zrdr`.

**⚠ A wrong key does not necessarily go red in the engine catalog.** Keying `Gamez` on the file
name alone (every chapter's is `gamez.zip`) was the able-to-fail control. It failed the identity
unit test at once (`Assert.NotSame() Failure: Values are the same instance`), but
`collision-visibility` — the one suite that builds all eight chapters — still reported PASS in
17.29 s with `decode_hits=7`, having built C1 eight times: its assertion holds vacuously on a world
it was not meant to be looking at. The identity test is the guard on the key, not the catalog.

**⚠ For B13.** Sharding erodes this win, because a shard sees fewer repeats of its own chapters.
Round-robining the same 58 builds over N processes and recharging each process's first use of a key
gives 18.23 s saved at 1 shard, 14.44 s at 2, 12.52 s at 3 and 12.37 s at 4 — and at 4 shards that
12.37 s is spread across four processes, so about 3 s comes off the critical path. Weighting shards
by measured suite wall matters more than the cache does.

**⚠ For B13: the balancer's weights.** From the cache-on report, 227.82 s of suite wall over 153
suites; greedy longest-first division gives a max shard of 122.6 s at 2, 81.8 s at 3, 61.3 s at 4
and 40.9 s at 6, each within 0.1 s of the arithmetic floor, so no single suite is the pacer at any
useful shard count and membership can be chosen freely. `rest` (manual simulation plus assertions)
is 136.20 s, 59.8 % of the wall, and its top 20 suites are 55.6 % of it — the campaign and landing
families, which fly aircraft for seconds at a time:

| Suite | Wall | Build | `rest` |
|---|---:|---:|---:|
| `campaign-roster` | 9.86 s | 2.15 s | 7.68 s |
| `campaign-bomber-formation` | 8.12 s | 1.72 s | 6.38 s |
| `wingman-station` | 5.62 s | 0.00 s | 5.62 s |
| `player-destroy-choreography` | 4.69 s | 0.00 s | 4.69 s |
| `landings-wingwalk-gate` | 6.81 s | 2.17 s | 4.63 s |
| `loadout-bind` | 4.59 s | 0.00 s | 4.59 s |
| `campaign-cutscene-skip` | 6.43 s | 2.18 s | 4.23 s |
| `campaign-set-ai-net` | 5.91 s | 1.68 s | 4.22 s |
| `campaign-squad-wakeup` | 5.71 s | 1.69 s | 4.01 s |
| `campaign-airframe-swap` | 6.19 s | 2.17 s | 4.01 s |

The two seven-world censuses (`collision-visibility` 11.09 s, `destructible-census` 9.38 s) are the
longest suites by wall but are almost all build, and they are also the pair whose ordering the cache
now rewards: whichever runs second pays 0.04 s of decode instead of 3.44 s. Putting them in the same
shard is worth about 3 s; splitting them costs it. `emitter-lifetime` must still run before the
shared C1 world is cached, unchanged by this item.

**Verified.** The same full battery as B11's: engine 235.4 s against 253.1 s on the Wave A
tree, decode 5.4 s over 58 worlds, everything green, exit 0.

## B13 ☑ Isolate engine suites and run balanced shards

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

**Order-dependency audit.** The catalog carried exactly one encoded order dependency and it is now
gone. `emitter-lifetime` had to be registered first because it installed a fake `IEmitterFactory`
and then built the shared C1 world through `WithWorld`, so the fake rode into every later
same-chapter suite until `damage-hd`'s `collision:true` rebuild undid it far down the registry. It
now calls `TestContext.WithPrivateWorld`, which never reads the shared cache and never writes to it
and frees the world when the body returns, so the fake cannot leave the suite. Registration order is
presentation only from here; `SuiteCatalog`'s and `docs/architecture.md`'s notes say so.

The audit for anything else a suite leaves in the shared host ran from both sides. By inspection,
the three mutable build knobs on `TestContext` are the only shared-state seam a suite writes:
`EmitterFactory` (one setter, now on a private world), `ExtraPrewarmSoundNames` (five setters, none
of which reset it) and `CutsceneRoots` (eight setters, each resetting in a `finally`). Every one of
those knob-setting suites builds through the four-argument `WithWorld(chapter, collision, mission,
body)`, whose mission override is never cached, so none of them could put a knob-shaped world into
the shared cache; the residue was `ExtraPrewarmSoundNames` reaching whatever world was built next.
That whole class is closed rather than argued about: `TestContext.ResetForSuite` now clears all
three knobs alongside the phase attribution, once per suite in `Run`, so a suite's inputs no longer
depend on which suite preceded it. By measurement, ten full-catalog runs over five different
partitions (serial, 2, 3, 4 and 6 shards, twice each) produced byte-identical 153-row name-and-
verdict sets, which is the check that would catch a leaked named node making a later same-named
spawn come out renamed. An eleventh partition, 8 shards, agreed too.

The one cost is that `emitter-lifetime`'s C1 world is no longer the shared one, so the first later
C1 suite in each process pays a world build it used to inherit (one ~1.5–2.5 s build per process).

**Isolation, per artifact.** Every engine launch writes under `.scratch/engine/owner-<pid>/`,
which is what makes a run's outputs unmistakable for another run's:

| Artifact | How it is isolated |
|---|---|
| engine log | `--log-file .scratch/engine/owner-<pid>/shard<i>of<n>.log`, per run and per shard |
| stdout / stderr | `Invoke-Godot` already derives `.out`/`.err` from the `--log-file` path |
| `test-report.json` | the harness writes it into `TestContext.ScratchDir`, which a sharded run points at `shard<i>of<n>/` **beside its own engine log** — so neither a sibling shard nor a concurrent run can reach it |
| per-suite artifacts (`WriteArtifact`) | the same `ScratchDir`, so they move with the report |
| stray-Godot cleanup | `Stop-StrayGodots` is owner-scoped: a `--run-tests` Godot whose owning PowerShell is gone is a stray and is killed, one whose owner is alive belongs to another run and is reported and spared (SHELL-2). A shard cannot kill a sibling, and a live sibling run cannot be killed by a starting one |
| watchdog | per launch, not per stage: one shard timing out fails itself with its evidence kept and the others still report |
| hidden desktop | `HiddenDesktop.ps1` gained `Start`/`Wait` beside `Run`, so several launches are in flight on the one desktop at once. Measured with 2, 3, 4, 6 and 8 concurrent Godots, all green: a desktop is a namespace for windows, not a serializing resource |
| testing profile / user data | **not isolated, deliberately.** `campaign-loop` keeps its store under `user://Testing/campaign-loop/Profiles` precisely because it proves persistence ACROSS processes, which `.scratch/` isolation would defeat. Only one shard runs it, so a single run is safe; two concurrent runs are not (below) |

**Membership.** `--run-tests=`'s value gained one term that divides rather than selects:
`shard:<index>/<count>`, parsed out by `SuiteShards.Parse` and applied by `SuiteShards.Plan` after
the selector's own miss checks, so an empty shard is a legitimate division of a small selection
while an empty selector is still a typo. The weights are checked in at
`analysis/engine-suite-weights.json`, written from a warm serial report; `Plan` is longest-unit-
first onto the lightest shard with ties broken on registry position, so one tree divides the same
way every run. The censuses-together constraint lives in that file's `groups`
(`collision-visibility` + `destructible-census`), worth about 3 s of decode. A suite the file does
not name is charged the default weight and printed as a `not checked:` line rather than silently
skewing the balance. Shard count is `-Shards <n>`; 0 (the default) means the measured default for a
full run and 1 whenever `-Suite`/`-Filter`/`-Quick` names a selection.

**Aggregation.** `Merge-EngineShards` in `RunTests.ps1` folds the shard reports into one verdict.
Counts sum; failed names sort back into registry order through each suite row's new `index` field;
the error allowlist's caps are re-checked against the **summed** counts, since N processes each
under the cap can still sum past it; every shard must report the same `CSVM.dll` MD5; and the
shards' suite counts must add up to the `selectedTotal` each of them reports, with a name appearing
in two shards its own failure. A shard exiting 0 with no report FAILS the stage. `ReportSchema` is
bumped to 3: the `shard` block and the per-suite `index` are new, and a sharded run's report is no
longer at `.scratch/test-report.json`.

**Measured** (`$env:CSVM_DATA_ROOT="Z:\CSVM"`, warm, `.\RunTests.ps1 -Shards N -SkipUnits
-SkipGoldens -SkipHitch`, one binary throughout: `d2e099fe0975c993bff005da5efa84f8`, two runs per
row, nothing else on the test desktop):

| Shards | Engine stage A | B | Max shard A/B | % of the arithmetic floor | Suite set equal | Verdicts equal |
|---:|---:|---:|---:|---:|---|---|
| 1 (serial) | 224.6 s | 224.2 s | — | — | reference | reference |
| 2 | 120.4 s | 120.3 s | 117.2 / 117.1 s | 93 % | yes | yes |
| 3 | 86.7 s | 86.3 s | 83.5 / 83.0 s | 87 % | yes | yes |
| 4 | 66.4 s | 66.3 s | 63.1 / 63.2 s | 87 % | yes | yes |
| 6 | 49.8 s | 50.0 s | 47.0 / 47.2 s | 77 % | yes | yes |

Same-build spread is ≤ 0.2 % at every row, against B11's ~1 % figure, so the differences between
rows are the shard count and nothing else. The floor column is the measured 218.6 s of suite wall
divided by the shard count, against the slowest shard actually measured. A single 8-shard run on
the preceding binary read 45.0 s with a 42.3 s slowest shard, 65 % of its floor: past 6 the balance
is bounded by individual suites rather than by the division. "Suite set equal" and "verdicts equal"
are machine-checked, not eyeballed: all ten runs above produced byte-identical sorted
`index name status` sets of 153 rows against the serial reference, and every run reported errors
clean and the same binary hash. At the chosen default the four shards measured 63.95 / 62.54 /
62.93 / 61.53 s of suite wall, a 3.8 % spread.

**Chosen default: 4.** It meets the item's 90–120 s goal with margin (66 s, 3.4x serial) at 87 % of
its arithmetic floor, while the marginal 16.5 s from 4 to 6 costs ten points of balance efficiency
and the next step costs twelve more. It leaves half of this machine's 16 logical processors free,
which matters because C21 may yet want concurrent golden launches. Each shard's ~63 s also sits
about five times under the per-launch 300 s watchdog, so a slower machine has room before a shard
starts timing out. 6 measured green twice and is one flag away (`-Shards 6`) for anyone who wants
the extra 16 s; serial stays selectable as `-Shards 1`, which is what C23's A/B compares against.

**Injected failures.** A temporary `ctx.Check(false, …)` was placed in one suite of each of the
four shards (reverted afterward, confirmed by `git diff` and a grep for the marker). Every shard
reported exactly its own suite and nothing else — `warning-shot` (index 10) in shard 1,
`engine-note` (25) in shard 2, `launch-velocity-decay` (13) in shard 3, `motor-acceleration` (19) in
shard 4 — and the stage row named all four in registry order, `149 passed, 4 failed`, exit 1.

**The two able-to-fail controls on the merge.** Pointing one shard's expected report path at a
directory that does not exist, so the shard exits 0 having written its report elsewhere, failed the
stage three ways at once: `s2: Godot exited 0 with no report`, `the shards covered 1 suite(s) of the
selection's 5`, and `1 of 2 shard(s) wrote no readable report`, exit 1. Temporarily dropping
`$EngineTimeoutSec` to 9 s with `collision-visibility` in one shard failed that shard alone
(`s1: timed out after 9s (exit 124); partial log at …`) while the other completed and reported both
its suites, exit 1. Both edits were restored and the restore confirmed by `git diff`.

**⚠ Two concurrent `RunTests.ps1` invocations isolate everything except a suite's own `user://`
store.** Two `-Shards 4 -SkipUnits -SkipGoldens -SkipHitch` runs started three seconds apart: the
first passed 153/153, the second failed exactly one suite, `campaign-loop`, on `an earlier
process's flown mission is still recorded in the profile file expected=1 actual=0`. Nothing else
collided — logs, reports and artifacts stayed apart, and the second run's stray sweep printed the
first run's four shards as `another Godot is live on this tree, left alone` instead of killing
them. The remaining collision is the one artifact deliberately outside `.scratch/`, so it is a
property of that suite's subject rather than a gap in the sharding. The goldens, hitch and perf
stages still identify their strays by output path alone and are not concurrency-safe either; this
is recorded on `docs/verification.md`'s LOG-13.

**Kept working.** `-Suite warning-shot` 1.4 s, `-Filter puffer` 3.9 s (5 suites), a selector miss
still fails the stage naming the term, and `-Quick` 30.5 s (219 unit tests, 13 engine suites). All
three stay single-process, which is faster for a handful of suites than paying N process startups.
The hitch stage is untouched and never runs as shard work.

**Verified.** Full `RunTests.ps1` at the default four shards with nothing else on the test
desktop: units 2506/2506 in 17.2 s, engine 153/153 in 66.5 s (slowest shard 63.7 s, errors
clean), goldens 16/16 hash-identical in 87.2 s, hitch clean, 193.5 s total against 383.3 s at
the start of the plan, exit 0.

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
