# Frame hitches — an always-on instrument, and the FPS readout on the front of it

**ACTIVE PLAN** (written 2026-08-14). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

Short freezes are visible at the controls (reproducibly when the damage lab detaches parts and they
fly away) and nothing in the repo can currently see them. This plan builds a permanent instrument
that detects a hitching frame, records what happened during it, and keeps a readout on screen, then
uses that instrument once to diagnose the known case. It also feeds the existing A/B rig so a hitch
that gets fixed cannot come back silently.

**Instrument only: no performance fix lands in this plan.** The reason is mechanical rather than a
matter of taste. The A/B protocol here is `-PerfLabel` a baseline, change one thing, `-PerfCompare`
against that label, and `-PerfCompare` hashes `CSVM.dll` specifically to catch a build being compared
against itself. A fix committed alongside the instrument that measures it has no baseline to be
compared against, because the metric did not exist before the change. The final wave therefore
produces a labelled baseline, a reproduced hitch, a read record and a **new `backlog.md` entry naming
the confirmed cause**. Fixing that cause is the next plan's or the backlog's job.

No item here is drawn from `backlog.md`, so no re-verification of existing entries was needed. The
plan's substance comes from a `/grilling` session on 2026-08-14, recorded in the Decisions table
below.

## Milestone goal

- A frame that costs far more than its neighbours is detected, logged, and written to a
  machine-readable sidecar with enough context to name a culprit, in every session, without anyone
  having asked in advance.
- The record says what the frame was doing: unaveraged CPU/GPU/physics split, count deltas, GC
  activity, the frames leading up to it, and named events from the code that ran.
- An on-screen readout (`F14`) shows current cost and the worst recent frame, with a rolling
  frame-time strip, once for the window, in every mode.
- `RunTests.ps1 -Perf` records hitch metrics per scenario, and one scenario reproduces the known
  debris case, so `-PerfCompare` can show a hitch regression the way it already shows a mean one.
- The known damage-lab hitch has a diagnosed, evidenced cause written down as a backlog item.

**No optimisation, no pooling, no GC tuning, and no allocation rewrite lands in this plan.** Those are
changes whose effect cannot be evaluated until the instrument exists and a baseline is labelled.

## Decisions (2026-08-14)

| # | Question | Decision |
|---|---|---|
| 1 | Diagnose these hitches, or build a lasting instrument? | **A permanent instrument, whose first job is the current hitches.** The reports are intermittent with no reliable repro, so an instrument you must be watching is worthless. |
| 2 | Design around a known trigger, or around not knowing? | **Around not knowing.** A repro exists (damage lab, parts detaching) and becomes a measured scenario, but the instrument is always-on and retrospective, because memory mis-attributes intermittent hitches. |
| 3 | What fires a record? | **Relative to a rolling baseline, with an absolute floor**, plus max/p95/p99 added to the existing `--perf` window. Two instruments answering two questions, both cheap. |
| 4 | Remove the 60 fps vsync cap? | **A `display.vsync` config key, with the existing `--no-vsync` flag beating it.** Not the default for play, and no runtime toggle: flipping mid-session resets the rolling baseline and poisons the log. |
| 5 | What does a record contain? | **All four layers**: unaveraged per-frame counters, GC deltas, a ring buffer of preceding frames, and event breadcrumbs. The breadcrumbs are the part that makes it last. |
| 6 | How deep does sampling go? | **Timed leaf samples with an explicit unattributed remainder. No tree.** A partially instrumented tree attributes un-instrumented time to whatever parent encloses it, and it is never fully instrumented. |
| 7 | What does the readout show, and where? | **Two tiers plus a frame-time strip, cycled on one key, drawn once for the window.** Frame rate, draw calls, node count and GC are process-wide, not per-pane. |
| 8 | How far does the map-edge retirement go? | **Key bindings only.** `TileGridOverlay`, `--debug-tilegrid`, `--map-edge-mode=` and `--map-edge-block=` all stay. `F14` is rebound to the readout. |
| 9 | Always on, or flag-gated? | **Detection always live; logging only when a frame trips.** Silent in a clean run, which is what makes it retrospective. A grace window after session build keeps startup out of it. |
| 10 | Where do records go? | **Summary line in the `perf` log category; full record to a `.scratch/logs/<mode>-<stamp>.hitches.jsonl` sidecar.** A log line is not an input to anything; a sidecar is. |
| 11 | Does it feed the A/B rig? | **Yes, plus a debris-burst scenario.** It records and never judges: no threshold gates a build. |
| 12 | Is fixing in scope? | **No. Instrument only** (see the scope paragraph above). |
| 13 | How is the instrument itself verified? | **Unit tests on the trigger math, a `--hitch-inject=` synthetic stall, and an in-engine suite.** A detector that silently never fires looks exactly like a game with no hitches. |
| 14 | What form does it land in? | **This plan**, waves A to E then G. |
| 15 | Tunable or compiled in? | **`Config` keys over `const` defaults.** Every number in this plan is a guess made during a conversation, and an absent `config.json` keeps goldens and scripted shots inert. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | Godot has a runtime equivalent of Unity's `Profiler.BeginSample`/`EndSample`. | The 4.7-stable class reference (now at `tools/godot-docs/doc/classes/`). `EngineProfiler` is *"custom profilers that are able to interact with the engine and editor debugger"* and surfaces only through `EditorDebuggerPlugin`. `Performance.AddCustomMonitor` is editor-only too and documents *"a delay of up to 1 second"*, so it cannot resolve a single frame. Build our own on `Stopwatch.GetTimestamp` pairs, the idiom `StartupProfile.cs:41` already uses. |
| 2 | `--perf` can already show a hitch. | Arithmetic. `ReportPerf` reports means over a 60-frame window (`Launcher.cs:801-843`), and its own doc comment says the averaging is deliberate *"so a single hitch doesn't read as a regression"*. One 47 ms frame among fifty-nine 16.7 ms frames moves the mean by about 0.5 ms. |
| 3 | A trigger relative to a rolling median adapts to whatever the machine is doing. | Only with vsync off. Under vsync every frame is padded up to the refresh interval, so the median sits pinned at 16.67 ms and `K × median` degenerates into a fixed threshold. Worse, the whole sub-cap range is invisible: a frame whose real work went from 4 ms to 14 ms is a 3.5× spike that vsync flattens. This is why A3 exists. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A3, B5, B6, B7, E14 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B4, D10, D11 | The *what* is settled; every threshold, window length and buffer depth is TUNE. Do not promote a guessed constant to fact. |
| **Leads only — no mechanism yet** | C8, C9, E12, E13, G15, G16 | Budget for investigation. C9's site list is a hypothesis, and G15/G16 may end in a disproof of the GC theory. |

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

### Wave A — Groundwork (measurable ground before anything is built on it)

1. ☑ A1 — Unbind the three map-edge debug keys, freeing `F14`
2. ☑ A2 — `ReportPerf` reports max and p95 next to its means (no p99 — it would equal max by
   construction at the 60-frame window; see the item's ⚠ Traps)
3. ☐ A3 — `display.vsync` config key, so the cap can come off without a rebuild

### Wave B — The instrument

4. ☐ B4 — `HitchMonitor`: rolling baseline, trigger, grace window, ring buffer, GC and counter deltas
5. ☐ B5 — `--hitch-inject=<ms>[@frame]`: a synthetic stall of known magnitude
6. ☐ B6 — The sidecar: `perf` summary line plus `.scratch/logs/<mode>-<stamp>.hitches.jsonl`
7. ☐ B7 — In-engine suite: a record fires, and carries its ring buffer, breadcrumbs and sidecar

### Wave C — Attribution

8. ☐ C8 — `PerfSample`: ambient timed leaf scopes with an explicit unattributed remainder
9. ☐ C9 — Seed the call sites (debris, spawn, pool checkout, material creation, resource load)

### Wave D — The readout

10. ☐ D10 — `PerfHud` on `F14` and `--debug-fps`: the compact tier, once for the window
11. ☐ D11 — The verbose tier and the rolling frame-time strip

### Wave E — The A/B rig

12. ☐ E12 — Hitch fields in `perf-history.jsonl` and in `-PerfCompare` output
13. ☐ E13 — A debris-burst scenario in `analysis/perf/scenarios.json`
14. ☐ E14 — `docs/verification.md`: a PERF rule on hitch comparability, plus the doc sweep

### Wave G — Diagnosis (the instrument's first customer)

15. ☐ G15 — Label a baseline and reproduce the damage-lab hitch under the instrument
16. ☐ G16 — Read the record, name the cause, mint the fix as a `backlog.md` entry

## Dependency and parallelism notes

Wave A is independent of everything and its three items are independent of each other, so all three
can run in parallel. A2 and A3 should land before any measurement is taken, because they change what
a baseline means.

B4 blocks the rest of Wave B and everything after it. B5 lands early on purpose: every later item is
verified with a hitch of known magnitude on demand, and neither B6 nor B7 has a trustworthy check
without it. B6 → B7 is a chain.

C8 blocks C9. D10 blocks D11.

**C before D is a deliberate ordering, not an accident.** A compelling readout with no attribution
behind it invites exactly the guess-the-culprit habit this instrument is meant to replace.

E12 and E13 need Wave B landed but not Wave D. E14 can be written any time after A2 and A3, and is
the natural place to record the vsync arithmetic from disproven-claim 3.

Wave G needs everything except D11.

**File contention.** A1, A2, A3, B4, B5, B6 and D10 all touch `Launcher.cs` or `SessionSpec.cs`. Do
not run those in parallel worktrees. C9 fans out across `src/Flight/`, `src/Mech3/` and
`src/Session/` and is the one item that touches modules it does not own; it should run alone.

**The wave letter F is skipped on purpose.** This plan rebinds `F14` and retires `F15`/`F16`, so
item ids `F14`–`F16` would read as key names in a plan that discusses both constantly.

**Flag count.** The parser's accepted-flag count and `docs/cli.md`'s flag-index count are kept equal
on purpose (117 today). This plan adds `--debug-fps` (D10) and `--hitch-inject=` (B5) and removes
nothing, so both move to 119. Keep them in step in the same edit.

---

# Wave A — Groundwork

## A1 ☑ Unbind the three map-edge debug keys, freeing `F14`

**Goal.** `F14`, `F15` and `F16` do nothing. The tile-grid overlay is still reachable via
`--debug-tilegrid`, and block depth and fold mode are still settable via `--map-edge-block=` and
`--map-edge-mode=`. `F14` is then free for D10.

**Evidence (confidence: traced).** The map-edge fold question was settled at the controls on
2026-08-08 (`docs/architecture.md` on `src/Mech3/MapEdgeExtender.cs`: repeats rather than mirrors,
matching on C1/C2/C4/C5 with no seam gaps), so the interactive stepping keys have no remaining job.
The key handling and the overlay live across `CSVM/src/Session/GameSession.cs`,
`CSVM/src/UI/TileGridOverlay.cs`, `CSVM/src/Mech3/MapEdgeExtender.cs`,
`CSVM/src/Mech3/WorldBuilder.cs`, `CSVM/src/Mech3/SceneBuilder.cs` and `CSVM/src/SessionSpec.cs`.

**Approach.** Remove only the `F14`/`F15`/`F16` input handling and the rows in
`docs/controls.md`. **Do not touch `TileGridOverlay.cs`, `--debug-tilegrid`, `--map-edge-mode=` or
`--map-edge-block=`.** Those flags cost a parse and a field to keep, and they are the only remaining
way to re-open a question that was originally decided from a video reading that turned out to be
wrong (the `CAP-17` post-mortem in `analysis/video-flight-calibration/FINDINGS.md`). Keeping them is
cheap insurance in a place this project has already been burned. Note in the `TileGridOverlay`
architecture entry that the overlay is now flag-only, since the repo's usual convention is that every
overlay has both a key and a scripted twin.

**Model recommendation.** medium, low effort. Mechanical deletion with a clearly drawn boundary.

**Verify.** `.\RunTests.ps1` green, golden hashes unchanged (nothing that draws is being removed).
`--debug-tilegrid` still opens the overlay; `--map-edge-block=3` and `--map-edge-mode=mirror` still
take effect. `MapEdgeFoldTests` still passes.

**⚠ Traps.** The temptation is to delete the overlay too, since nothing binds a key to it any more.
Decision 8 says no. Also: `F14`/`F15`/`F16` appear 13 times in `docs/HISTORY.md`, which is **frozen
and never edited** — those references stay as they are and describe the past correctly.

## A2 ☐ `ReportPerf` reports max, p95 and p99 next to its means

**Goal.** Every existing `--perf` window line, and therefore every record already being written to
`perf-history.jsonl`, carries the worst frame in the window and two percentiles alongside the means
it reports today.

**Evidence (confidence: traced).** `Launcher.cs:801-843` accumulates sums over
`PerfWindowFrames = 60` and divides. Adding a max and two percentiles needs the same per-frame values
that are already being read, held in a fixed 60-slot buffer. `ReportPerf`'s own comment explains why
it averages, and that reasoning stays correct for the mean terms; the percentiles are additional, not
a replacement.

**Approach.** Keep a preallocated `double[PerfWindowFrames]` of the frame's wall cost, compute
`max`/`p95`/`p99` at window close, append to the existing single interpolated log line (the comment
at `Launcher.cs:824` explains why it must stay one interpolated string). Adding fields at the end of
the line keeps existing parsers working, but confirm rather than assume: check whatever in
`analysis/perf/` reads the line before landing.

**Model recommendation.** medium. Small and local, but it changes a line other tooling parses.

**Verify.** `.\RunTests.ps1 -Perf -PerfFilter <one scenario>` writes a record carrying the new fields
and the existing fields unchanged. Run `--perf --no-vsync --hitch-inject=` once B5 exists to confirm
`max` actually moves when a known stall is injected: **an unchanged number is not evidence until you
have seen it able to fail.**

**⚠ Traps.** p99 over a 60-sample window is the single worst sample, so p99 and max will be identical
by construction. Either widen the percentile window beyond the report window or report only `max` and
`p95` and say why. Do not ship a p99 column that is a duplicate of `max` by definition.

## A3 ☐ `display.vsync` config key, so the cap can come off without a rebuild

**Goal.** Vsync can be turned off for an ordinary interactive session by editing the git-ignored
`res://config.json`, with no rebuild and no flag on the command line. `--no-vsync` still wins over
the config value.

**Evidence (confidence: traced).** `--no-vsync` already exists and does exactly the right thing
(`SessionSpec.cs:887`, `Launcher.cs:281-286`: `VSyncMode.Disabled` plus `Engine.MaxFps = 0`), but
`Launcher.cs:276` calls it *"a measurement flag, not a display one"* and neither `RunGame.ps1` nor
`RunDev.ps1` passes it. `audio.volume` is the existing precedent for a config key that a flag beats
(`docs/architecture.md` on `src/Utils/Config.cs`).

**Approach.** `Config.GetBool("display.vsync", true)` read at the same site in `_Ready`, with
`_spec.NoVsync` beating it. Log the resolved state either way, so a session's log always says which
mode it ran in (E12 and E14 depend on that being recoverable after the fact).

**Model recommendation.** medium, low effort.

**Verify.** `--dump-config` lists `display.vsync`. No `config.json` leaves behaviour byte-identical,
so goldens are untouched. With `display.vsync: false` in the file, the `vsync off` log line appears;
adding `--no-vsync` on top changes nothing; `display.vsync: true` plus `--no-vsync` still turns it
off.

**⚠ Traps.** This key must never turn on the D10 readout by any path. The readout is nondeterministic
and the goldens are pinned by raw-pixel md5, so a config key that draws pixels would break all 11
permanently. Vsync state changes timing only, never what is drawn.

---

# Wave B — The instrument

## B4 ☐ `HitchMonitor`: rolling baseline, trigger, grace window, ring buffer, GC and counter deltas

**Goal.** Any frame costing far more than its recent neighbours is detected as it happens, and a
record is assembled describing it: unaveraged `script`/`render_cpu`/`gpu`/`physics`, deltas in
`draws`/`prims`/`nodes`/`mem`, `GC.CollectionCount` per generation plus allocated bytes, and the
preceding N frames. Nothing is logged yet (that is B6).

**Evidence (confidence: direction-sound; every constant is a guess).** The mechanism is settled: the
monitors are the ones `ReportPerf` already reads, and `Stopwatch.GetTimestamp` is the repo's
established timing primitive (`StartupProfile.cs:41`, *"two QPC reads per phase"*). The **numbers are
not evidenced by anything**: the median multiple, the floor, the baseline window, the ring depth and
the grace period are all values named in conversation on 2026-08-14. They are TUNE.

**Approach.** New `CSVM/src/Utils/HitchMonitor.cs`, ticked from `Launcher._Process` where
`ReportPerf` is called, so it lives alongside the other session-wide services and works at the
launchscreen, in `--viewer` and in `--freecam` as well as in flight. Trigger:
`frame_ms > max(medianMultiple × rolling_median, floorMs)`. All constants via `Config` getters over
`const` defaults, keyed `hitchMonitor.*` (`medianMultiple`, `floorMs`, `baselineFrames`,
`ringFrames`, `graceMs`), so they self-register into `--dump-config` and `ReportOrphans` catches
typos. Everything preallocated: a fixed struct array for the ring, a fixed array for the baseline
window, no allocation on any frame including a hitching one. Stay on wall time, following the
existing precedent at `Launcher.cs:582` (an instrument that freezes with the thing it measures
reports nothing), including under `--det`.

Unit-test the pure math in `CSVM.Tests` (rolling median, the trigger expression, grace-window
suppression, ring wraparound), per the stated split: anything reaching `GD.*` or a live `Node`
belongs in `src/Testing/` instead.

**Model recommendation.** high. This is the item everything else hangs off, and getting the baseline
or the wraparound subtly wrong produces an instrument that looks alive and sees nothing.

**Verify.** Unit tests cover the trigger at, just above and just below the boundary in both the
relative and floor regimes. End-to-end verification waits on B5, and that is the real check: a
detector that never fires is indistinguishable from a game with no hitches.

**⚠ Traps.** The rolling median must be a true median, not a mean, or one hitch raises the baseline
and hides the next. Under vsync the median is pinned at the refresh interval (disproven-claim 3), so
the floor is what actually fires and that is expected, not a bug. `GC.GetTotalAllocatedBytes(false)`
is the cheap imprecise overload and is the right one here; the precise one is not free. The grace
window exists because startup legitimately hitches and `StartupProfile` already covers that ground.

## B5 ☐ `--hitch-inject=<ms>[@frame]`: a synthetic stall of known magnitude

**Goal.** A stall of a stated duration happens on a stated frame, on demand, so every downstream item
has something deterministic to verify against.

**Evidence (confidence: traced).** `SessionSpec.cs` parsing plus a hook in `Launcher._Process` is all
this needs. The `@frame` form matches how `--frames=N` is already documented as a sim coordinate
rather than a wall-clock delay.

**Approach.** Parse in `SessionSpec.cs`, apply in `Launcher._Process` before `HitchMonitor` ticks.
Offer both a busy-wait and an allocation-burst form if it is cheap to do so: a busy-wait proves the
timing path, while an allocation burst is the only way to prove the GC columns move. Document in
`docs/cli.md` (this is one of the two flags moving the count to 119).

**Model recommendation.** medium.

**Verify.** `--hitch-inject=50@120` produces exactly one record at sim frame 120 with `frame_ms`
around 50. `--hitch-inject=5` produces none under the default floor. The allocation form moves
`GC.CollectionCount(0)`.

**⚠ Traps.** The injector must sit outside anything `HitchMonitor` measures as attributable, or it
will be reported as its own breadcrumb and mask the thing being tested. Keep it out of `--det`'s
implied set: it is a fault injector, and a run that silently stalls is worse than one that does not
measure.

## B6 ☐ The sidecar: `perf` summary line plus `.scratch/logs/<mode>-<stamp>.hitches.jsonl`

**Goal.** A tripped frame leaves one human-readable line in the `perf` log category and one complete
JSON record in a sidecar sharing the log's `<mode>-<stamp>` stem.

**Evidence (confidence: traced).** Every run already writes all categories to
`.scratch/logs/<mode>-<stamp>.log` regardless of `--log=`. Machine-readable sidecars beside a
human-readable summary is the established pattern here: `.scratch/test-report.json`,
`.scratch/tex_census.json`, `perf-history.jsonl`. Sharing the stem means `CleanScratch.ps1` sweeps
both with no new rule.

**Approach.** Buffer records in a preallocated ring and flush on session teardown or every few
seconds, **never inline in the hitching frame**: JSON serialisation allocates, and doing it
immediately after a hitch is the worst possible moment. The `perf` line stays a single interpolated
string for the same culture reason documented at `Launcher.cs:824`.

**Model recommendation.** medium.

**Verify.** `--hitch-inject=50@120` writes exactly one sidecar record whose ring buffer holds the
preceding frames and whose counters match the log line. A clean run writes **no** hitch lines and
either no sidecar or an empty one. Confirm the sidecar is swept by `CleanScratch.ps1`.

**⚠ Traps.** Do not use PowerShell to read or write the sidecar: PowerShell 5.1 corrupts UTF-8
silently, and `.scratch/` is git-ignored so the pre-commit encoding tripwire cannot see it. Write it
from C# with an explicit UTF-8 encoding. A flush on teardown must survive a crash-exit path, or the
one run that crashed is the one with no record. <TODO: decide whether an abnormal exit is expected to
retain records, and if so how.>

## B7 ☐ In-engine suite: a record fires, and carries its ring buffer, breadcrumbs and sidecar

**Goal.** `RunTests.ps1` fails if the hitch detector stops detecting.

**Evidence (confidence: traced).** `src/Testing/` plus `--run-tests[=filter]` is the existing harness
and already exits nonzero on failure; `Suites.cs` is where the assertions live.

**Approach.** A suite that runs with `--hitch-inject=`, then asserts: exactly one record, `frame_ms`
within tolerance of the injected duration, the ring buffer populated to depth, at least one
breadcrumb present once C9 has landed, and the sidecar written and parseable. Add the suite to
whatever `RunTests.ps1` already drives.

**Model recommendation.** medium.

**Verify.** `.\RunTests.ps1` green. Then deliberately break the trigger locally and confirm the suite
goes red: a test that has never been seen to fail is not evidence.

**⚠ Traps.** The harness completes inside one `_Ready` call and never yields a frame (this is why
goldens are a separate pass, per `RunTests.ps1:21-24`). A hitch is by definition a per-frame
phenomenon, so this suite cannot be a plain `--run-tests` assertion unless it is fed synthesised
frame data. <TODO: settle whether this lands as a `--run-tests` suite over synthetic input, or as a
separate frame-yielding probe pass in the shape of the golden stage.> This is the item most likely to
need its approach revised on contact.

---

# Wave C — Attribution

## C8 ☐ `PerfSample`: ambient timed leaf scopes with an explicit unattributed remainder

**Goal.** Any code path can declare that it ran and how long it took, without knowing anything about
the monitor, and a hitch record lists the named work in that frame plus how much time was **not**
attributed to anything.

**Evidence (confidence: lead-only).** The pattern is proven here: `StartupProfile` is described as
*"ambient statics over `Current`, so the shared build code records blind"*. What is not measured is
the per-call overhead in this codebase; a QPC read is tens of nanoseconds in general, but that is an
assumption until C9 puts scopes on real paths.

**Approach.** `CSVM/src/Utils/PerfSample.cs`. Sites are a fixed enum, never a string built per call,
so nothing allocates. `using (PerfSample.Scope(Site.DebrisSpawn))` accumulates into a preallocated
per-frame array indexed by site, reset each frame and snapshotted into a record when one fires. Never
touch a Godot `Array` or `Variant` (boxing). **Flat leaves only, no nesting**, and the record always
carries an explicit remainder term.

**Model recommendation.** high. The API shape decides whether this survives contact with future
features or rots.

**Verify.** Unit tests in `CSVM.Tests` on the accumulate/reset/snapshot cycle and the remainder
arithmetic. Measure the overhead of a scope directly and record the number in the module's
architecture entry. <TODO: name the measurement, most likely a tight loop under `--perf --no-vsync`
compared against the same loop with scopes compiled out.>

**⚠ Traps.** This repo has already paid for the nesting lesson once. `StartupProfile`'s standing
warning reads *"keep every phase a LEAF or the sum silently double-counts; `rest` = real
uninstrumented work, not an error term."* The same failure recurs per-frame: a partially instrumented
tree attributes un-instrumented time to whatever parent encloses it, and it is never fully
instrumented, because the next feature to land will not add its scope. Flat leaves plus a visible
remainder cannot lie that way. State in the module doc that scopes are **coarse-grained only**: a
debris burst, not one chunk; a spawn, not one node.

## C9 ☐ Seed the call sites

**Goal.** The paths most likely to cost a frame declare themselves, so a record names work rather
than just counting nodes.

**Evidence (confidence: lead-only).** The site list is a hypothesis, not a finding. It rests on the
general shape of a .NET game hitch (allocation burst, first-draw shader compilation, synchronous
load) plus the observation that the reproducible case coincides with objects coming into existence.
Grounding so far: effects are already pre-pooled (`EffectPools`, per-root and per-player scaled), so
runtime effect allocation was designed against; there are 43 runtime `new ShaderMaterial` /
`ResourceLoader.Load` / `GD.Load` sites across 14 files; and `CSVM.csproj` carries **no GC
configuration at all**, so the process runs .NET 8 defaults (Workstation, background GC), which is
already the low-pause configuration rather than a misconfiguration to be fixed.

**Approach.** Seed: debris and part detach (the reproducible case), AI aircraft spawn, effect-pool
checkout and pool exhaustion, runtime `ShaderMaterial` creation, synchronous resource and audio
loads. Coarse granularity per C8's rule.

**Model recommendation.** medium. Mechanical fan-out across modules, but each site needs a judgement
about granularity.

**Verify.** Reproduce the damage-lab case and confirm at least one breadcrumb appears in the record.
<TODO: confirm each seeded site is actually reached in the sessions being measured; a scope on a dead
path is worse than none because its absence reads as evidence.>

**⚠ Traps.** This item touches modules it does not own, so read each module's `docs/architecture.md`
entry before editing. Run it alone, never in a parallel worktree alongside another item. The failure
mode to avoid is a scope inside a per-projectile or per-particle loop: that turns tens of nanoseconds
into a real cost and produces an instrument that changes what it measures.

---

# Wave D — The readout

## D10 ☐ `PerfHud` on `F14` and `--debug-fps`: the compact tier, once for the window

**Goal.** `F14` cycles a readout showing frames per second, current frame cost, and the worst frame
in the last few seconds. It is off by default, drawn once for the window rather than per splitscreen
pane, and works at the launchscreen and in every mode.

**Evidence (confidence: direction-sound; legibility is unverified).** Frame rate, draw calls, node
count and GC are process-wide, so a per-pane copy would show four identical readouts at four times
the layout cost. Hosting on `Launcher` rather than `GameSession` follows `ReportPerf`, and gives the
launchscreen, `--viewer` and `--freecam` for free. Whether the compact tier is legible in a 4-player
pane is not something a screenshot settles, hence the playtest item below.

**Approach.** `CSVM/src/UI/PerfHud.cs`, alongside `NodeLabels`, `MarkerOverlay` and
`TileGridOverlay`. Size through `HudMetrics.Scale`, but note that `HudMetrics`' damping is per-pane
and this control is not, so it takes the window ratio rather than `PaneFactor`. Cycle
off → compact → full → off. Add `--debug-fps[=compact|full]` as the scripted twin (the second of the
two flags moving the count to 119), and rows in `docs/controls.md`.

The worst-frame term is the one that matters: it spikes and decays, so a hitch you felt leaves
readable evidence a second later, which an instantaneous counter does not.

**Model recommendation.** medium.

**Verify.** `.\RunTests.ps1` green with **golden hashes unchanged**. If a golden moves, the overlay is
drawing when it should not be, and that is a bug, not a re-pin. Confirm the readout appears in
`--viewer`, `--freecam` and at the launchscreen, and that `--hitch-inject=50` visibly moves the
worst-frame term. <TODO: mint a `PT-` item via `.\New-ItemId.ps1 -Kind PT` for at-the-controls
legibility in a 4-player pane, and add it to `playtest.md`.>

**⚠ Traps.** **Off by default, and reachable only by key or flag, never by a config key.** The
readout is nondeterministic by nature and the 11 goldens are pinned by raw-pixel md5, so anything
that could switch it on implicitly breaks all of them permanently. `RunGame.ps1` may pass
`--debug-fps` if you want it on during ordinary play; goldens do not go through `RunGame.ps1`, so
that path is safe.

## D11 ☐ The verbose tier and the rolling frame-time strip

**Goal.** The second tier adds the per-frame cost split, count and memory terms, GC counts by
generation, and the last few breadcrumbs, plus a rolling bar graph of recent frame times where a
hitch is visually unmistakable and its run-up is visible.

**Evidence (confidence: direction-sound).** The strip is the highest-value display for this problem,
because a spike is obvious at a glance in a way a number is not. Nothing in `src/UI` draws a graph
today, so there is no pattern to copy.

**Approach.** `_Draw` over `HitchMonitor`'s existing ring buffer: no new data collection, just a
second view of what B4 already keeps. Span in frames, via a `Config` key. Mark the trigger threshold
as a line on the strip so the relationship between what is drawn and what fires a record is visible.

**Model recommendation.** medium.

**Verify.** `--hitch-inject=50@120` puts a visible spike on the strip at the right position.
Screenshot it into `.scratch/` for the record. Goldens unchanged (still off by default).

**⚠ Traps.** The strip must read from the existing ring buffer rather than keeping its own history,
or the display and the records can disagree about the same frame. Redrawing 120 bars every frame is
itself work: measure it with `--perf` and confirm the readout is not what is causing the next hitch.

---

# Wave E — The A/B rig

## E12 ☐ Hitch fields in `perf-history.jsonl` and in `-PerfCompare` output

**Goal.** Every `-Perf` run records hitch count, worst frame and p95 per scenario, and `-PerfCompare`
prints their ratios beside the existing terms.

**Evidence (confidence: lead-only).** A2 makes `ReportPerf` emit the values, so this is plumbing
rather than new measurement. What is unknown is how noisy a hitch count is across repeated identical
runs, which decides whether the median over kept launches means anything.

**Approach.** Extend the scenario record in `RunTests.ps1` and whatever assembles it. **Carry the
vsync mode in the record**, because hitch counts are not comparable across modes (E14). Print the
ratio in `-PerfCompare` with the same "this is awareness, not a verdict" framing the existing
awareness metrics already carry.

**Model recommendation.** medium.

**Verify.** Two labelled runs of the same unchanged build produce hitch fields, and `-PerfCompare`
pairs them. `-PerfCompare` still catches identical `CSVM.dll` hashes as a noise floor rather than an
A/B (METHOD-6). <TODO: measure the run-to-run spread of hitch count on an unchanged build, and record
it as the noise floor; without that number the field cannot be read at all.>

**⚠ Traps.** **It records; it never judges.** No threshold gates a build. `docs/verification.md`
PERF-5 and METHOD-3 hold that a verdict comes from a paired A/B and never from a committed number,
because machine drift makes a fixed threshold lie. Hitch count is noisier than any mean term here, so
it is the last metric that should ever gate anything.

## E13 ☐ A debris-burst scenario in `analysis/perf/scenarios.json`

**Goal.** The known hitch is under continuous measurement, so a fix cannot silently regress later.

**Evidence (confidence: lead-only).** The user reproduces the hitch interactively with the damage lab
sliders. Whether it reproduces under `--det --no-vsync --mute` at a fixed sim-frame count, driven by
`--damage=` rather than by hand, is unproven and is the first thing this item must establish.

**Approach.** Follow the existing scenario shape: `--det --perf --no-vsync --mute` plus
`--frames=N --screenshot=`, which is what ends the run. Drive the damage state from the command line
(`--damage=`) so no human is at the controls. Scenario prose in the manifest describes what the shot
covers today, per the golden-manifest hook's rules.

**Model recommendation.** medium.

**Verify.** The scenario runs to exactly N sim frames (the printed `sim_frame=` proves it) and
produces a nonzero hitch count on the current build. **If it produces zero, this item has failed and
must say so**, rather than being tuned until it reports something.

**⚠ Traps.** A scripted damage change may apply in one frame where a human moving a slider spreads it
over many, which could produce a bigger hitch than the real one or none at all. Warmup launches are
discarded for a reason (PERF-7): a cold file cache reshapes a startup profile rather than scaling it,
and it will also manufacture hitches that have nothing to do with the case being measured.

## E14 ☐ `docs/verification.md`: a PERF rule on hitch comparability, plus the doc sweep

**Goal.** The next person to read a hitch count knows what it can and cannot be compared against.

**Evidence (confidence: traced).** Vsync off changes frames per wall second, therefore allocations
per wall second, therefore hitch frequency. Per-frame cost is comparable across modes; frequency is
not. Separately, under vsync the rolling median is pinned at the refresh interval, so the relative
trigger degenerates to the floor.

**Approach.** One new `PERF` rule in the established shape: a bold one-or-two-line imperative plus at
most one sentence of measured evidence, no narrative. Sweep the docs this plan has changed:
`docs/cli.md` (two new flags, index count to 119), `docs/controls.md` (`F14` rebound, `F15`/`F16`
removed), `docs/architecture.md` (entries for `HitchMonitor`, `PerfSample`, `PerfHud`; amend
`TileGridOverlay` to note it is flag-only), `docs/tooling.md` (the new perf-stage fields).

**Model recommendation.** medium.

**Verify.** The parser's accepted-flag count and `cli.md`'s index count are both 119. Each new module
has an architecture entry within the length budget and at most three `⚠` lines.

**⚠ Traps.** `docs/architecture.md` is about 110 KB: read only the specific `##` entry being edited,
never the whole file. `PROJECT_CONTEXT.md`'s "Current status" gets its pointer swapped, never a
description of what landed, and must not be longer after the edit than before it.

---

# Wave G — Diagnosis

## G15 ☐ Label a baseline and reproduce the damage-lab hitch under the instrument

**Goal.** A labelled `perf-history.jsonl` baseline exists for the current build, and the reproducible
hitch has been captured with a full record.

**Evidence (confidence: lead-only).** This wave is the investigation, so it has no findings yet by
construction.

**Approach.** `.\RunTests.ps1 -Perf -PerfLabel <baseline>` on the landed instrument. Then reproduce
interactively with the damage lab and capture the sidecar, and separately via E13's scripted
scenario. Keep both: the interactive one is the phenomenon as experienced, the scripted one is what
future A/Bs will actually compare.

**Model recommendation.** medium. Execution is mechanical; the judgement is in G16.

**Verify.** A labelled record exists for the current `CSVM.dll` hash. At least one sidecar record
exists for the damage-lab case, with breadcrumbs populated.

**⚠ Traps.** Interactive and scripted runs differ in vsync mode, so their hitch counts are not
comparable (E14). Take the baseline **before** touching anything else, or there is nothing to compare
a later fix against, which is the whole reason this plan lands no fix.

## G16 ☐ Read the record, name the cause, mint the fix as a `backlog.md` entry

**Goal.** The damage-lab hitch has a stated, evidenced cause, and the fix exists as a `backlog.md`
item with its traps recorded. No fix is implemented.

**Evidence (confidence: lead-only).** The leading hypothesis is that a burst of object creation
(debris chunks, their materials, possibly first-draw shader compilation) triggers either the
allocation cost directly or the GC pause that follows it. `CSVM.csproj` carries no GC configuration,
so the process is on .NET 8 Workstation background GC, which is already the low-pause default: if GC
turns out to be the cause, **there is no setting to flip**, and the fix is allocating less. This
hypothesis may be wrong, and a disproof that lands no code is a success here.

**Approach.** Read the record: which term dominates the frame (script, render CPU, GPU), which
breadcrumbs are present, whether a generation-2 collection lands on the same frame, what the run-up
in the ring buffer looks like. Mint the item with `.\New-ItemId.ps1 -Kind BL` and write it with a
`⚠ Traps` section naming what was ruled out, so the next session does not re-chase it.

**Model recommendation.** high. This is judgement over evidence, and the failure mode is a
confident wrong attribution.

**Verify.** The backlog entry names a specific mechanism with the record that supports it, and states
what was ruled out. If the record is inconclusive, **say so and name what the instrument would need
to settle it** rather than picking the most plausible suspect.

**⚠ Traps.** The reflex will be to fix it immediately, and decision 12 says no: the baseline from G15
is what makes the fix provable, and a fix landed in the same breath as the diagnosis cannot be
A/B'd. Correlation between a breadcrumb and a hitch is not causation: the same frame that spawns
debris also draws them, and the CPU/GPU split is what separates those two.
