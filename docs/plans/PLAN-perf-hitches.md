# Frame hitches — an always-on instrument, and the FPS readout on the front of it

**COMPLETE 2026-08-14** (written 2026-08-14; landed same day). All 16 checklist items are ☑ except
E13, closed ❌ disproven (waves A–E, G). Indexed in [`plans.md`](plans.md); read as history.

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
| 4 | Godot's `_Process(delta)` is the frame's wall cost. | Measured while landing B4. `delta` is post-processed (`OS.delta_smoothing`, on by default) and reads as a quantised constant: an `--det --perf --no-vsync --mute --stage=empty` run reported `delta_ms=8.333` on every one of 200 frames, and exactly `wall_ms=500.00` per 60-frame window, while a raw `Stopwatch.GetTimestamp` pair over the same frames varied 8.25–8.42 ms. `HitchMonitor` is therefore fed QPC. **A2's `max_ms`/`p95_ms` still come from `delta`** and so resolve a big hitch but not a small one. E12 must not read them as raw frame costs. |
| 5 | Under vsync, `medianMultiple × refresh_interval` always lands below the 40 ms floor, so vsync mode always falls back to the floor (HitchMonitor.cs's own shipped doc comment claimed this). | Arithmetic, at the 60 Hz cap this plan's defaults elsewhere assume: 4 × 16.67 ms = 66.7 ms, ABOVE the 40 ms floor — the RELATIVE term wins there, not the floor; only a refresh ≥ 100 Hz brings it under the floor. Found verifying B5's `--hitch-inject=`: the dev machine's *actual* vsync refresh is 120 Hz (~8.33 ms/frame, same pace as `--no-vsync` there), not the 60 Hz this plan assumes throughout — and since `HitchMonitor`'s 2000 ms grace window is wall-clock, not frame count, that same pace also meant frame 120 was only ~1000 ms in, still inside grace (PERF-12). Both a 50 ms and an 80 ms stall needed past ~frame 240 to trip. Doc comment fixed in `HitchMonitor.cs` and its `architecture.md` mirror. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A3, B5, B6, B7, E13, E14 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B4, D10, D11 | The *what* is settled; every threshold, window length and buffer depth is TUNE. Do not promote a guessed constant to fact. |
| **Leads only — no mechanism yet** | C8, C9, E12, G15, G16 | Budget for investigation. C9's site list is a hypothesis, and G15/G16 may end in a disproof of the GC theory. |

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
3. ☑ A3 — `display.vsync` config key, so the cap can come off without a rebuild

### Wave B — The instrument

4. ☑ B4 — `HitchMonitor`: rolling baseline, trigger, grace window, ring buffer, GC and counter deltas
5. ☑ B5 — `--hitch-inject=<ms>[@frame]`: a synthetic stall of known magnitude
6. ☑ B6 — The sidecar: `perf` summary line plus `.scratch/logs/<mode>-<stamp>.hitches.jsonl`
7. ☑ B7 — In-engine suite: a record fires, and carries its ring buffer, breadcrumbs and sidecar

### Wave C — Attribution

8. ☑ C8 — `PerfSample`: ambient timed leaf scopes with an explicit unattributed remainder
9. ☑ C9 — Seed the call sites (debris, spawn, pool checkout, material creation, resource load)

### Wave D — The readout

10. ☑ D10 — `PerfHud` on `F14` and `--debug-fps`: the compact tier, once for the window
11. ☑ D11 — The verbose tier and the rolling frame-time strip

### Wave E — The A/B rig

12. ☑ E12 — Hitch fields in `perf-history.jsonl` and in `-PerfCompare` output
13. ❌ E13 — A debris-burst scenario in `analysis/perf/scenarios.json` — disproven
14. ☑ E14 — `docs/verification.md`: a PERF rule on hitch comparability, plus the doc sweep

### Wave G — Diagnosis (the instrument's first customer)

15. ☑ G15 — Label a baseline and reproduce the damage-lab hitch under the instrument
16. ☑ G16 — Read the record, name the cause, mint the fix as a `backlog.md` entry

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
on purpose — 117 when this plan was written, but other work landed flags in the meantime, so
re-measure rather than trust a number written in conversation (`docs/cli.md`'s own reconciliation
line is the source of truth). This plan adds `--debug-fps` (D10) and `--hitch-inject=` (B5, landed
at 127) and removes nothing; D10 moves both counts to 128. Keep them in step in the same edit.

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

## A3 ☑ `display.vsync` config key, so the cap can come off without a rebuild

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

## B4 ☑ `HitchMonitor`: rolling baseline, trigger, grace window, ring buffer, GC and counter deltas

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

## B5 ☑ `--hitch-inject=[alloc:]<ms>[@frame]`: a synthetic stall of known magnitude

**Goal.** A stall of a stated duration happens on a stated frame, on demand, so every downstream item
has something deterministic to verify against.

**Evidence (confidence: traced).** `SessionSpec.cs` parsing plus a hook in `Launcher._Process` is all
this needs. The `@frame` form matches how `--frames=N` is already documented as a sim coordinate
rather than a wall-clock delay.

**Approach.** Parse in `SessionSpec.cs`, apply in `Launcher._Process` before `HitchMonitor` ticks.
Offer both a busy-wait and an allocation-burst form if it is cheap to do so: a busy-wait proves the
timing path, while an allocation burst is the only way to prove the GC columns move. Document in
`docs/cli.md` (this is one of the two flags this plan adds — see "Flag count" above).

**Model recommendation.** medium.

**Verify.** `--hitch-inject=50@120` did NOT reliably produce a record — the worked example above
sits right on `HitchMonitor`'s grace-window boundary (see the ⚠ Traps addendum below); landed with
`DefaultHitchInjectFrame = 300` and verified there instead. `--hitch-inject=50@300` produced exactly
one record at frame 300 (`HitchMonitor.FrameCount` space) with `frame_ms` 59.38 (the 50 ms injected
plus ~9 ms of normal frame cost, "around 50" as intended); `--hitch-inject=5` (bare, default frame)
produced none, well under the 40 ms floor even with grace long past; `--hitch-inject=alloc:50@300`
tripped once with `Gc0Delta=18` and `AllocatedBytesDelta≈860 MB` where the busy-wait form's same run
moved neither (`Gc0Delta=0`). All three confirmed via a temporary, reverted `GD.Print` in
`Launcher._Process` (`git diff` empty on it afterward) reading `HitchMonitor.Last`/`FrameCount`
directly — B6's sidecar does not exist yet to confirm through the normal path. Unit tests cover the
grammar (`SessionSpecParserTests.HitchInjectParsesMagnitudeFormAndFrame`/
`TheHitchInjectFlagReachesTheSpec`) and the new `HitchMonitor.FrameCount` invariant
(`HitchMonitorTests.FrameCountIsOneAheadOfTheNextTicksFrame`). `.\RunTests.ps1` green throughout,
goldens unchanged (no `--hitch-inject=` flag rides in any golden scenario).

**⚠ Traps.** The injector must sit outside anything `HitchMonitor` measures as attributable, or it
will be reported as its own breadcrumb and mask the thing being tested. Keep it out of `--det`'s
implied set: it is a fault injector, and a run that silently stalls is worse than one that does not
measure.

**⚠ A frame ordinal is not a wall-clock delay, and `HitchMonitor`'s grace window IS one.** This
plan's own worked example, `--hitch-inject=50@120`, was written assuming the 60 Hz cap the plan's
other defaults assume — 120 frames ≈ 2000 ms, exactly the grace window, at that pace. It does not
trip on the dev machine: vsync's own refresh there is 120 Hz, not 60 (~8.33 ms/frame, same as
`--no-vsync`), so frame 120 is only ~1000 ms into the 2000 ms grace, whatever the injected magnitude
(disproven-claim 5, PERF-12). Pick `@frame` for the machine you are actually running on — 300 was
verified clear on the dev box, with margin, at both vsync settings.

## B6 ☑ The sidecar: `perf` summary line plus `.scratch/logs/<mode>-<stamp>.hitches.jsonl`

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

**Verify.** `--hitch-inject=50@300` (B5's actual default frame — `@120` sits inside the grace window
on this machine, disproven-claim 5) wrote exactly one sidecar record: `HitchSidecar.cs` opened
`.scratch/logs/fly-<stamp>.hitches.jsonl` alongside `Log`'s own `.log`, and a single JSON line
appeared there with a 120-entry ring ending in the hitching frame itself, every field matching the
`[perf] hitch frame=300 …` line byte for byte after unit conversion (`mem_bytes` 119464361 ↔
`mem_mb` 113.93, `allocated_bytes_delta` 16400 ↔ `alloc_delta_mb` 0.02). A clean `--frames=180` run
(no `--hitch-inject=`) wrote no `[perf] hitch` line and left the sidecar at 3 bytes — the UTF-8 BOM
alone, since the file opens unconditionally at session start. `CleanScratch.ps1 -WhatIf` listed both
generated `.hitches.jsonl` files for deletion alongside their `.log`s, no new rule needed. New tests:
`CSVM.Tests/HitchSidecarTests.cs` (queue-copy-not-reference, the flush-interval gate, drop-oldest
overflow, JSON culture-invariance) — `.\RunTests.ps1` green throughout (build, 1198 units, 53 engine
suites, 14 goldens unchanged).

**⚠ Traps.** Do not use PowerShell to read or write the sidecar: PowerShell 5.1 corrupts UTF-8
silently, and `.scratch/` is git-ignored so the pre-commit encoding tripwire cannot see it. Written
from C# with `Log.cs`'s own recipe: UTF-8 WITH a BOM (not just "explicit UTF-8" — a BOM-less file is
what PowerShell 5.1 misreads) and `AutoFlush`, opened once for the process's whole life rather than
per-flush. **The TODO on crash-exit survival is resolved, not deferred**: a record survives a crash
from the moment it is FLUSHED (`AutoFlush` puts it on disk immediately), not from the moment it
TRIPPED, so `hitchSidecar.flushSeconds` (default 3 s) is the loss bound on a `Stop-Process -Force`/
crash — at most the queued-but-unflushed tail. `Launcher.LaunchSession`/`ReturnToMenu`/`_ExitTree`
all flush before `HitchMonitor.Rearm`, so only an actual kill/crash can lose anything, never an
ordinary quit or relaunch. A signal handler racing the crash it is meant to survive was considered
and rejected as more mechanism than the bound it would buy.

## B7 ☑ In-engine suite: a record fires, and carries its ring buffer, breadcrumbs and sidecar

**Goal.** `RunTests.ps1` fails if the hitch detector stops detecting.

**Evidence (confidence: traced).** `src/Testing/` plus `--run-tests[=filter]` is the existing harness
and already exits nonzero on failure; `Suites.cs` is where the assertions live.

**Approach — settled, not as first written.** `HitchMonitor` only trips on a real rendered frame
measured over wall time (`Launcher._Process`), and `--run-tests` runs every suite to completion
inside one `_Ready` call without ever yielding a frame — the same reason goldens is a scripted pass
rather than a suite. So this is **not** a `Suites.cs` entry: it is a new `hitch` stage in
`RunTests.ps1`, in the goldens/perf shape — two scripted Godot launches, each read back through its
own `--log-file` plus the `.hitches.jsonl` sidecar B6 writes. A clean `--frames=180` launch must
stay silent (decision 13: silent-when-clean is the whole point, and a detector that fires on
nothing would read as one that fires on everything with nobody the wiser). A
`--hitch-inject=50@300 --frames=310` launch (B5's own verified-safe pair — `@120` sits inside the
grace window on this machine, disproven-claim 5) must trip exactly once, on frame 300, with a full
120-entry ring, and a sidecar record whose `frame`/`frame_ms` match the printed `[perf] hitch …`
line. Breadcrumbs are C9's, not yet landed — the assertion list below is everything B4/B5/B6 already
give this suite something to check; C9 extends it, it does not need to re-architect it.

**Model recommendation.** medium.

**Verify.** `.\RunTests.ps1` green — 173.7s total (build 1.7s, units 12.6s/1198 passed, engine
69.5s/53 suites clean, goldens 74.0s/14 shots hash-identical, the new `hitch` stage 15.9s:
`clean: 0 hitch line(s); inject: 1 hitch line(s), frame_ms=62.52`). Then
deliberately broke the trigger locally (`HitchMonitor.Tick`'s `tripped` forced `false`) and reran —
`FAIL hitch … inject: expected exactly 1 hitch line, saw 0` — confirming the stage is actually able
to fail before reverting the change (`git diff` empty on it afterward): a test that has never been
seen to fail is not evidence.

**⚠ Traps — the one this item hit, for the next stage that launches Godot from a PowerShell script.**
`--frames=N` alone never quits the process: the only quit is `CaptureDirector`'s, gated on a pending
`--screenshot=` (`CaptureDirector.cs:126`). Landing this stage without `--screenshot=` (as first
written) launched Godot into an unbounded run that outlived the calling shell — the frame counter in
its own log climbed past 200,000 with nothing to stop it. Every scripted launch in `RunTests.ps1`
now pairs `--frames=` with `--screenshot=`, same as goldens/perf; a launch that doesn't care what
gets drawn can point it at a throwaway PNG in the same scratch dir.

Also: `[System.IO.Path]::ChangeExtension($path, $null)` in PowerShell is **not** the same call as
`Path.ChangeExtension(logPath, null)` in C# — `HitchSidecar.cs`'s own derivation. PowerShell coerces
`$null` to `""` on the way into a `[string]` parameter, and `ChangeExtension` treats an empty-string
extension as "replace with a bare dot", leaving a double dot (`…stamp..hitches.jsonl`) rather than
the single one the C# side writes. Pass the compound extension directly instead —
`ChangeExtension($path, "hitches.jsonl")` — which sidesteps the coercion rather than fighting it.

---

# Wave C — Attribution

## C8 ☑ `PerfSample`: ambient timed leaf scopes with an explicit unattributed remainder

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

**Verify.** `.\RunTests.ps1` green — 159.1s total (build 1.5s, units 11.1s/1214 passed, engine
63.1s/53 suites clean, goldens 68.4s/14 shots hash-identical, hitch 15.0s: `clean: 0 hitch line(s);
inject: 1 hitch line(s), frame_ms=62.39`). 16 new units in `PerfSampleTests` plus one in
`HitchSidecarTests` cover the accumulate/freeze/snapshot cycle, the remainder arithmetic, nesting
suppression, `Reset`, the site vocabulary, and the attribution reaching the sidecar's JSON as a copy.

The overhead measurement is **a tight loop in the unit host, not the `--perf --no-vsync` A/B this
plan first sketched**: 200 000 open+close pairs timed directly answers "what does one scope cost"
to the nanosecond, where a frame-loop A/B would have to resolve ~60 ns against a frame that varies
by ±0.1 ms and would mostly measure the machine. **60 ns per scope** (58.8 / 59.3 / 62.7 over three
runs) and **exactly 0 bytes allocated** over 10 000 scopes, both asserted — the allocation figure
exactly, the timing against a 2 µs ceiling that catches a lock or a lookup appearing on the path
without failing on a loaded machine. Recorded in the `architecture.md` entry.

The chain was also proved end to end **in-engine**, since no call site exists until C9 and a
mechanism only unit-tested would land as plausible rather than working: `InjectHitch`'s stall was
temporarily wrapped in a scope and the hitch stage rerun, giving
`samples=debris_spawn:1x55.91 attributed_ms=55.91 unattributed_ms=11.69 sample_violations=0` on a
67.60 ms frame — the scope's own time named, the rest visible as remainder, the sum exact. The wrap
was then reverted (`git diff` clean on `Launcher.cs` apart from the wiring), because B5's rule is
that the injected fault must read as unattributed time.

**⚠ Traps.** This repo has already paid for the nesting lesson once. `StartupProfile`'s standing
warning reads *"keep every phase a LEAF or the sum silently double-counts; `rest` = real
uninstrumented work, not an error term."* The same failure recurs per-frame: a partially instrumented
tree attributes un-instrumented time to whatever parent encloses it, and it is never fully
instrumented, because the next feature to land will not add its scope. Flat leaves plus a visible
remainder cannot lie that way. State in the module doc that scopes are **coarse-grained only**: a
debris burst, not one chunk; a spawn, not one node.

**⚠ What landing it added to that.** Nesting is *enforced*, not just documented: a scope opened
inside another measures nothing and is counted in the record's `sample_violations`, so
`Σ(sites) + unattributed = frame_ms` holds on every frame including a wrong one, and the violation
is visible rather than being a quietly doubled total. The remainder is deliberately **not clamped** —
negative means a scope spanned the frame boundary, which is a defect to see. `EndFrame()` is called
on the same line as `Launcher`'s QPC stamp, which is what makes "the scopes" and "the frame_ms they
ran inside" the same span; the session node processes at priority -1000, one notch ahead of the
launcher, so its work is already in. And `HitchMonitor.Fill` takes the snapshot itself rather than
leaving it to the caller: the one ambient read in a class that otherwise has everything handed in,
so a record can never carry a stale frame's attribution.

## C9 ☑ Seed the call sites

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
loads. Coarse granularity per C8's rule. **The vocabulary already exists**: C8 landed `PerfSite`
with exactly those eight values (`DebrisSpawn` · `PartDetach` · `AiSpawn` · `EffectCheckout` ·
`EffectPoolMiss` · `MaterialCreate` · `ResourceLoad` · `AudioLoad`), so this item is call sites
alone unless a site turns out to be missing — a site nothing calls is simply absent from a record,
never a zero row.

**Model recommendation.** medium. Mechanical fan-out across modules, but each site needs a judgement
about granularity.

**Verify.** All eight sites landed: `DebrisSpawn` (`AnimRuntime.RunDeathSequence`'s try body — the
same span `_deathCallDepth` already brackets as "the whole burst"), `PartDetach`
(`FlightController.Crash`'s two `CrashRuntime.Play` calls), `AiSpawn`
(`AiAircraftSpawner.Spawn`'s whole build), `EffectCheckout` (`AnimRuntime.PlayEffectAt`'s
per-def loop), `EffectPoolMiss` (`EmitterDirector.Assert`'s miss branch), `MaterialCreate`
(`EmitterRenderer.Attach`), `ResourceLoad` (`TextureArchive.FindImage`) and `AudioLoad`
(`WorldSounds.Spawn` and `WorldSounds.Create`'s decode-on-miss blocks, both reached — the one-shot
and the looping `SOUND_NODE` halves).

Reached live, confirmed two ways. First, `.\RunTests.ps1` stayed green throughout (build, 1214
units, 53 engine suites, 14 goldens hash-identical, hitch stage clean:0/inject:1) — goldens
unchanged confirms nothing here draws. Second, a temporary `GD.Print` after `PerfSample.EndFrame()`
in `Launcher._Process` (reverted, `git diff` empty on `Launcher.cs`/`HitchMonitor.cs` afterward)
read every closed frame's attribution on two real, non-injected scenarios: `--freecam --chapter=C1
--destroy=m_build03 …` (a world destructible dying) surfaced `effect_pool_miss` and
`effect_checkout` on later frames as the death's choreography continued; `--chapter=C1
--plane=player_bhawk --crash=5 …` (a player crash) surfaced `part_detach` directly on the frame the
crash rig fires (56.5 ms of a 67.4 ms frame — real hitch-sized cost, not a rounding artifact) plus
`effect_pool_miss` on the frames either side. To get a REAL (not `--hitch-inject=`) sidecar record
rather than just the console read — both frames were too early for `HitchMonitor`'s 2000 ms grace
window (PERF-12) — `HitchMonitor.Tick`'s grace check was temporarily dropped (`bool tripped =
frameMs > ThresholdMs;`, reverted, `git diff` empty on `HitchMonitor.cs` afterward) and the crash
scenario rerun: `.hitches.jsonl` carries `{"samples":[{"site":"part_detach","ms":56.524,"calls":1}],
"attributed_ms":56.524,"unattributed_ms":10.836,...}` and a sibling record with
`{"site":"effect_pool_miss","ms":100.177,"calls":7}` — `Σ(sites)+unattributed=frame_ms` holds on
both, exactly as C8 requires. `DebrisSpawn` itself did not fire in either live run: `--destroy=`
kills at session build (`GameSession.ApplyDestroyOverride`, confirmed in `docs/cli.md`), and
`Launcher.LaunchSession` calls `PerfSample.Reset()` right after the build specifically because "the
build's own scopes... belong to no frame" (C8's own comment) — so a build-time death's attribution
is deliberately discarded before frame 1 ever reads it. This is existing, correct C8 behaviour, not
a defect: the plan's actual reproducible case is the *interactive* damage lab, mid-session, which
reaches `RunDeathSequence` the same way `--crash=5` reaches `Crash()` (same `AnimRuntime` event
machinery) — `PartDetach` firing live is the closest available proof that `DebrisSpawn` will too,
since no scripted CLI trigger kills a world destructible after session build exists today (a gap
worth a future backlog item if G15/G16 need one).

**Evidence correction.** The "43 runtime `new ShaderMaterial` / `ResourceLoader.Load` / `GD.Load`
sites across 14 files" figure in this item's own Evidence above does not match the tree: a full grep
of `CSVM/src` found zero `ResourceLoader.Load(`/`GD.Load(` calls anywhere — this codebase reads its
own archive formats (`TextureArchive`, `SoundArchive`) rather than Godot's resource loader, and the
`new *Material(` count is single digits, not 43, once load-time builds (`SceneBuilder`, `Clutter`,
`FogVolumeClutter`, `Precipitation`, the `--anim-lab` mesh lab) are excluded as out of scope for a
frame-path site. The real load/decode surface for `ResourceLoad`/`AudioLoad` is the custom archive
readers seeded above. Left as a correction here per this plan's own rule that a lead is verified,
not assumed — the original number was a conversation guess, not a grep result.

**⚠ Traps.** This item touches modules it does not own, so read each module's `docs/architecture.md`
entry before editing. Run it alone, never in a parallel worktree alongside another item. The failure
mode named in the plan — a scope inside a per-projectile or per-particle loop — did not arise: every
seeded site is a whole burst/spawn/checkout/build/decode, never a per-item iteration.

**⚠ What landing it found that the plan did not anticipate: routine cross-site nesting.** C8's own
warning is that nesting is *"a defect to be found and removed, not a shape this supports"* — but
several of C9's eight sites naturally call into each other on the SAME reproducible case this plan
cares about: `RunDeathSequence`/`Crash` reach `PlayEffectAt` (`EffectCheckout`) and `WorldSounds`
(`AudioLoad`) through the same event dispatch; `EmitterDirector.Assert`'s miss branch always reaches
`EmitterRenderer.Attach` (`MaterialCreate`) through `Puffer.Create`; `AiAircraftSpawner.Spawn`
reaches `TextureArchive.FindImage` (`ResourceLoad`) through the plane's own decal paint. This is not
a bug in the seeding: `PerfSample._open` is one flag for the whole process (not a per-site stack),
so the OUTER call wins and every nested attempt is suppressed and counted in `sample_violations`,
exactly the mechanism C8 built for this. The live `--crash=5` record above shows it directly —
`part_detach`'s frame carries `sample_violations: 6`, meaning six nested attempts (effect checkouts,
pool misses, material creates, audio decodes, all real work the crash choreography does) folded
into `part_detach`'s own 56.5 ms rather than appearing as their own line items. A record with a high
violation count on a dominant site is therefore a **normal, expected** shape for a compound event,
not a sign the instrument mis-fired — G16 should read a nonzero `sample_violations` as "more
happened here than the named sites show," not as noise.

---

# Wave D — The readout

## D10 ☑ `PerfHud` on `F14` and `--debug-fps`: the compact tier, once for the window

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
off → compact → full → off. Add `--debug-fps[=compact|full]` as the scripted twin (the second of this
plan's two flags — see "Flag count" above for the current number), and rows in `docs/controls.md`.

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

## D11 ☑ The verbose tier and the rolling frame-time strip

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

## E12 ☑ Hitch fields in `perf-history.jsonl` and in `-PerfCompare` output

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

**Verify.** `hitch_count` rides inside the existing per-scenario `metrics` object (median over the
same kept-launch population as every other metric there), so it flows through `-PerfCompare`'s
existing ratio loop for free rather than needing a new section; `vsync` (`"off"`/`"on"`, read back
per launch from its own `[perf] vsync …` line) lands as a new top-level field beside `gpu`.
`RunTests.ps1 -Perf -PerfFilter empty-stage -PerfIterations 2 -PerfLabel e12-base`, then
`-PerfLabel e12-change -PerfCompare e12-base` (same unchanged build, `dll_md5` identical both times):
both records carried `"vsync":"off"` and `"metrics":{…,"hitch_count":0}`, and the printed A/B showed
`hitch_count 0 -> 0 x n/a (awareness only)` plus `SAME BINARY: …noise floor, not an A/B (METHOD-6)` —
the existing same-build guard is untouched by this item's additions. `max_ms`/`p95_ms` (A2) also now
carry a `why` line, closing a gap A2 itself left open (they rode into `metrics` automatically the
whole time — the generic `[perf] window` parser already reads any numeric field — but were never
added to the manifest's `awarenessMetrics`, so they printed with no reason attached).

Then, proving the count can move at all (a check that has never been seen to move is not evidence,
METHOD-9): `analysis/perf/scenarios.json`'s `empty-stage` args temporarily carried
`--hitch-inject=50@280` (reverted after, `git diff` clean on the manifest) and
`-PerfFilter empty-stage -PerfIterations 1` reran. The launch's log carried exactly one
`[perf] hitch frame=280 frame_ms=60.77 …` line, and the history record read
`"metrics":{…,"max_ms":8.33,"p95_ms":8.33,"hitch_count":1}`. **`hitch_count` is the metric that
actually survives a lone hitch, and this run proves why the other two do not**: the `[perf] window
sim_frame=300 …` line covering frame 280 read `max_ms=48.96` on its own (moved, and — per A2's own
disproven-claim 4 — a `delta`-smoothed 48.96 against `HitchMonitor`'s raw-QPC 60.77 for the identical
frame, the exact gap that claim warned E12 not to read as a raw cost), but the scenario's *reported*
`max_ms` stayed `8.33`: the median over the 4 kept windows (`8.33, 8.33, 8.33, 48.96`) is dragged back
to `8.33` by the three clean windows outvoting the one that saw the stall. A single-frame hitch is
structurally invisible to a median-of-per-window-maxima; `hitch_count` is not built the same way and
caught it. The run-to-run spread of hitch count on an unchanged build remains unmeasured — it needs
many repeated runs on a quiet machine to characterise, which is beyond what this landing session can
do, and is future work rather than a blocker for the field existing: a `0 -> 0` same-build ratio has
been observed (above) but a single pair is not a noise band (METHOD-2, METHOD-3), so read `hitch_count`
qualitatively (it moved, or it did not) until that measurement exists, same as every other TUNE
constant this plan has landed undocumented-in-`backlog.md` by design (Wave G is where this plan's
diagnosis work, and any backlog entry it needs, lands).

**⚠ Traps.** **It records; it never judges.** No threshold gates a build. `docs/verification.md`
PERF-5 and METHOD-3 hold that a verdict comes from a paired A/B and never from a committed number,
because machine drift makes a fixed threshold lie. Hitch count is noisier than any mean term here, so
it is the last metric that should ever gate anything — confirmed structurally rather than just
argued: `verdictMetrics` in the manifest was left untouched, so `hitch_count` cannot flag a row even
if `-PerfCompare` is misread; it only ever prints under the `(awareness only)` note.

## E13 ❌ A debris-burst scenario in `analysis/perf/scenarios.json` — disproven: the burst and the reset that would let it be seen are the same line of code, on opposite sides

**Closed as a disproof — no scenario was added, and none should be: it would report `hitch_count: 0`
forever, indistinguishable from "measured clean" for a case the instrument structurally cannot see.**
The item's own Verify said so in advance: *"If it produces zero, this item has failed and must say
so, rather than being tuned until it reports something."* It produced zero, twice, at two different
burst sizes, and the reason is not noise — it is `Launcher.LaunchSession`'s own ordering.

**The mechanism.** `LaunchSession` (`Launcher.cs:714-751`) builds the whole session tree via
`_session.StartSession()`, then immediately calls `_hitchMonitor.Rearm()` (`Launcher.cs:743-744`) —
by design, per the method's own comment: *"A build stalls the frame loop for as long as it takes...
the hitch monitor starts its grace window and drops its baseline here rather than reporting the build
as the session's first hitch."* `DamageLab`'s `--damage=` preset is applied inside that same build,
synchronously in its `_Ready()` (`GameSession.cs:1387` for the parked-viewer host, `:1807` for the
flown one), which calls `Reapply` → `_target.Apply` (`DamageLab.cs:201-217, 445-471`) and fires every
crossed panel's `gimmeflakes`/`yellow_sparks_follow`/`small_fireball_follow`/fire-trail cascade before
`StartSession()` returns. So the entire debris burst is always finished before `Rearm()` even runs,
and grace (2000 ms wall-clock, TUNE) counts from that same `Rearm()` — the burst and the reset that
would let it register are ordered by the same two lines, with the burst permanently on the wrong side.
No `--damage=` value changes this: there is no CLI-reachable way to delay the preset past session
build, and building one would be a fix to the damage lab, not an item this instrument-only plan owns.

**Verified against two real launches, not assumed.** Both against `pbloodhawk` (`nodename
player_bhawk`), thresholds read from the actual extraction, not the doc's example prose
(`extracted/zrdr/vehicle.zrd.json:1025-1068`: leftwing crosses `pdpanel5`@0.5/`pdpanel4`@0.3/
`pdpanel3`@0.15, all three at once below 0.15):
- `--viewer --plane=player_bhawk --damage=leftwing:0.05 --det --mute --perf --no-vsync --frames=180`:
  the first window (`sim_frame=60`, `wall_ms=699.91`) reads `max_ms=150.00` against an 8.33 ms
  baseline — a real, 18x single-frame spike, visible exactly where A2's own field should show it —
  and **zero** `[perf] hitch` lines anywhere in the log. 700 ms is a third of the 2000 ms grace.
- Maximised the burst — all four parts (`nose`/`tail`/`leftwing`/`rightwing`) driven to 0.05, every
  `pdpanelN` on the airframe plus both def-level `player_smoketrail`(0.10)/`player_fuelleak`(0.85)
  entries crossed at once — over `--frames=600` (10 windows): identical first-window `max_ms=150.00`,
  and every one of the other nine windows reads the clean baseline exactly (`max_ms=8.33`). Making the
  burst four times bigger changed neither its timing nor its duration enough to matter, and it leaves
  nothing behind to trip a later window — a one-time construction cost, not a recurring one.

**What this settles.** E12's `max_ms` genuinely sees this spike; `hitch_count` genuinely cannot, and
not from an undertuned threshold — the event and the counter's own arming are mutually exclusive by
construction. A future fix to the real interactive hitch (Wave G) will need a live in-flight hit, not
a `--damage=` preset, if it wants HitchMonitor to see it happen; this item's job was to establish that
distinction, not to work around it.

### Original approach (kept for reference)

## E13 (original) A debris-burst scenario in `analysis/perf/scenarios.json`

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

## E14 ☑ `docs/verification.md`: a PERF rule on hitch comparability, plus the doc sweep

**Goal.** The next person to read a hitch count knows what it can and cannot be compared against.

**Evidence (confidence: traced).** Vsync off changes frames per wall second, therefore allocations
per wall second, therefore hitch frequency. Per-frame cost is comparable across modes; frequency is
not. Separately, under vsync the rolling median is pinned at the refresh interval, so the relative
trigger degenerates into a FIXED threshold — which of it or the floor actually fires then depends on
the refresh rate, not on the defaults alone (B5 found this the hard way, PERF-12/HitchMonitor.cs):
at the 60 Hz cap this plan's own defaults assume elsewhere, that fixed threshold (66.7 ms) is ABOVE
the 40 ms floor, so the relative term wins there, not the floor as originally believed.

**Approach.** One new `PERF` rule in the established shape: a bold one-or-two-line imperative plus at
most one sentence of measured evidence, no narrative. Sweep the docs this plan has changed:
`docs/cli.md` (two new flags — re-measure the index count rather than trust a number written in
conversation, see "Flag count" above), `docs/controls.md` (`F14` rebound, `F15`/`F16`
removed), `docs/architecture.md` (entries for `HitchMonitor`, `PerfSample`, `PerfHud`; amend
`TileGridOverlay` to note it is flag-only), `docs/tooling.md` (the new perf-stage fields).

**Model recommendation.** medium.

**Verify.** New rule landed as `PERF-13` (`docs/verification.md`), covering both halves of the
Evidence above in one bold imperative plus one evidence sentence, in the established shape:
comparability is scoped to same-vsync-mode runs, and the vsync-pins-the-median mechanism (with its
66.7 ms vs. 40 ms arithmetic) is cross-referenced to `PERF-12` rather than restated. `docs/tooling.md`
cited `PERF-12` for the comparability claim (line 239, landed with E12 before `PERF-13` existed) —
swapped to the new, on-point rule.

The doc sweep found three of its five targets already correct from earlier items landing their own
doc updates in-commit (this plan's Ground rules require that): `docs/cli.md`'s flag-index count
(re-measured independently rather than trusted — `Grep "^- .--" docs/cli.md` gives 129 bullet lines,
128 unique index entries in the "Flag index" block, and extracting every `arg == "--x"` /
`arg.StartsWith("--x=")` name from `SessionSpec.cs`'s parse chain gives 128 unique flags too; all
three agree, no edit needed); `docs/controls.md` (F14 already reads the readout row, no F15/F16 rows
remain — landed with A1/D10); `docs/architecture.md`'s `TileGridOverlay` index line (already reads
"flag-only" with the A1 cross-reference).

The other two needed work. `docs/architecture.md`'s `HitchMonitor.cs` and `PerfSample.cs` entries
had grown to four `⚠` lines each as B6/C9/D11/E13 landed content into them one item at a time, over
this item's own three-line budget — merged `HitchMonitor.cs`'s short `Stopwatch`-vs-`delta` bullet
into its TUNE-defaults bullet, and `PerfSample.cs`'s short main-thread-only bullet into its
coarse-granularity bullet, losing no sentence, both now at three. `docs/tooling.md` already carried
the perf-stage field documentation (`hitch_count`, `vsync`, the `hitch` stage) from E12/B7 landing
in-commit; only the `PERF-12`→`PERF-13` citation needed changing.

**⚠ Traps.** `docs/architecture.md` is about 110 KB: read only the specific `##` entry being edited,
never the whole file. `PROJECT_CONTEXT.md`'s "Current status" gets its pointer swapped, never a
description of what landed, and must not be longer after the edit than before it.

---

# Wave G — Diagnosis

## G15 ☑ Label a baseline and reproduce the damage-lab hitch under the instrument

**Goal.** A labelled `perf-history.jsonl` baseline exists for the current build, and the reproducible
hitch has been captured with a full record.

**Evidence (confidence: lead-only).** This wave is the investigation, so it has no findings yet by
construction.

**Approach — reconciled against E13, not as first written.** The original Approach's second half
("separately via E13's scripted scenario") named a scenario that does not exist: E13 disproved the
only CLI path to the aircraft `DamageLab`'s burst (`--damage=`, applied inside
`Launcher.LaunchSession`'s build, always finished before `Rearm()` starts the grace window) and found
no CLI-reachable way to delay it. Reading `DamageLab.cs` directly closes the other half too:
`Reapply()` fires only from `HSlider.ValueChanged` (a hand drag) or that same build-time preset —
nothing calls it post-`Rearm()`. **The literal aircraft-DamageLab burst has no scripted repro today**,
only an interactive one (a human dragging a slider after grace has cleared), which this item did not
attempt (see Verify — the user chose the scripted proxy below over sitting at the controls).
`.\RunTests.ps1 -Perf -PerfLabel g15-baseline` labelled the baseline. For the hitch itself, `--crash=`
stood in as the live, scriptable proxy C9 already called "the closest available proof": a real,
post-`Rearm()` `FlightController.Crash()` that detaches parts and throws debris, picked past
`HitchMonitor`'s grace window the same way B5/E13 tuned `--hitch-inject=`/`--damage=` (PERF-12):
frame 300 at this machine's ~8.33 ms/frame pace lands ~2500 ms in, ~500 ms past the 2000 ms grace.

**Model recommendation.** medium. Execution is mechanical; the judgement is in G16.

**Verify.** `.\RunTests.ps1 -Perf -PerfLabel g15-baseline -SkipUnits -SkipEngine -SkipGoldens` — PASS,
119.6 s total, `dll_md5=9dec2111b119d6798600d432aa6d52a0` (commit `1dfe02f`, `dirty=False`) — 5
scenario records landed in `perf-history.jsonl`, all labelled `g15-baseline`, all `hitch_count=0` (a
clean instrument on an unmodified build, not yet the reproduction).
`.\RunProbe.ps1 --fly --chapter=C1 --plane=player_bhawk --crash=300 --no-vsync --perf --frames=320
--screenshot=.scratch\g15-crash-proxy.png` tripped twice, both well clear of grace: frame 300
`frame_ms=48.43` (threshold 40.00, `samples=part_detach:1x33.01`) and frame 301 `frame_ms=62.11`
(`samples=effect_pool_miss:7x54.36`). The sidecar `.scratch/logs/fly-20260814-203733.hitches.jsonl`
carries both full records — a 120-entry ring each, breadcrumbs populated, `sample_violations` 6 and 7
respectively (C9's compound-event shape, not a mis-fire).

**⚠ Traps.** Interactive and scripted runs differ in vsync mode, so their hitch counts are not
comparable (E14) — moot here, since this item landed only the scripted half; an interactive
`DamageLab` capture, if taken later, is a separate, non-comparable data point, not a replacement for
one. Take the baseline **before** touching anything else, or there is nothing to compare a later fix
against, which is the whole reason this plan lands no fix. **The reconciliation is itself a finding to
carry into G16**: `part_detach`/`effect_pool_miss` from a scripted plane crash are evidence for the
debris/effects-cascade hypothesis in general, not for the aircraft `DamageLab` specifically — read
them as the closest available proxy, not as a capture of the literal reported phenomenon.

## G16 ☑ Read the record, name the cause, mint the fix as a `backlog.md` entry

**Goal.** The damage-lab hitch has a stated, evidenced cause, and the fix exists as a `backlog.md`
item with its traps recorded. No fix is implemented.

**Evidence (confidence: lead-only).** The leading hypothesis is that a burst of object creation
(debris chunks, their materials, possibly first-draw shader compilation) triggers either the
allocation cost directly or the GC pause that follows it. `CSVM.csproj` carries no GC configuration,
so the process is on .NET 8 Workstation background GC, which is already the low-pause default: if GC
turns out to be the cause, **there is no setting to flip**, and the fix is allocating less. This
hypothesis may be wrong, and a disproof that lands no code is a success here.

**Approach — as written, plus one confirmatory trace.** G15's own two sidecar records already settle
the allocation/GC half of the leading hypothesis: `gc0_delta`/`gc1_delta`/`gc2_delta` are **zero on
both hitching frames** and `allocated_bytes_delta` is 300-350 KB, three orders of magnitude under the
~860 MB burst B5 needed to move those columns at all. What actually dominates is `attributed_ms`
(`part_detach`/`effect_pool_miss`), so a temporary, reverted `GD.Print` in `EmitterDirector.Assert`'s
miss branch (`git diff` empty afterward) named exactly which effects it built during the same
`--crash=300` run, settling "first-use construction" against "pool exhaustion" with real names
rather than a guess.

**Model recommendation.** high. This is judgement over evidence, and the failure mode is a
confident wrong attribution.

**Verify.** `BL-355` minted (`.\New-ItemId.ps1 -Kind BL`) and written with the traced mechanism: ten
distinct first-time `EmitterDirector.Assert` misses (`lgpuffer` x3, `spurtpuffer1`-`5`,
`fierypuffer`, `trailpuffer2`) each synchronously building a `Puffer` + shader material + particle
system in the crash's own dispatch frame. GC, allocation volume and GPU/render are all ruled out
with the record's own numbers; pool-size exhaustion is ruled out separately (`large_firetrail` sized
6, only 3 pieces in flight, no `PoolRecycles` wrap). Its `⚠ Traps` names what the record cannot yet
answer — whether the cost recurs on a second crash/damage event in the same session — as an open
question with a concrete next step, per this Verify's own instruction, rather than a guessed
suspect.

**⚠ Traps.** The reflex will be to fix it immediately, and decision 12 says no: the baseline from G15
is what makes the fix provable, and a fix landed in the same breath as the diagnosis cannot be
A/B'd. Correlation between a breadcrumb and a hitch is not causation: the same frame that spawns
debris also draws them, and the CPU/GPU split is what separates those two — confirmed here, not just
argued: `render_cpu_ms`/`gpu_ms` sat at their normal ~0.5/0.2 ms on both hitching frames, so the cost
is CPU-script-side, matching the `PerfSample` attribution rather than contradicting it.
