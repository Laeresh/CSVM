# Bounce-terminated launches — give `BL-240`'s debris a real flight

**ACTIVE PLAN** (written 2026-08-02). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan delivers the launch half of `BL-240`: an `OBJECT_MOTION` that omits `RUN_TIME` and names a
`BOUNCE_SEQUENCE` gets a solved flight time, actually flies, and dispatches its bounce sequence when
it lands. `BL-240` was re-verified still-open on 2026-08-02 against both `docs/HISTORY.md` (the
`BL-236` entry files it explicitly as untouched, `HISTORY.md:10677`) and the code
(`AnimRuntime.cs:2014` still reads `run_time ?? 0f`; `:2020` still routes `ballTime <= 0f` to
`Seek(0f)`).

**The fall half is out of scope and is now `BL-245`.** The 529 bounce-terminated events are two
populations, and only ~150 of them are launches with an apex to solve. The other 379 — 335
free-falling zeppelin `gasbag1`/`crashnode1`, ~17 downward-thrown lifeboats and turret parts, 8
zero-gravity `chuteman` descents — have no parabola and carry live `water`/`lava` bounce branches
that need a struck collider to choose between. Both problems are the same ground ray, which this
plan does not build.

## Milestone goal

- A bounce-terminated launch flies its own parabola instead of being posed at rest, on all ~150
  reachable events (`sparkout3` ×125, `sparkout4` ×24, `treasure_splash` ×1).
- The named `BOUNCE_SEQUENCE` fires when the piece lands, so `OBJECT_ACTIVE_STATE partN INACTIVE`
  runs, the fireball pops, and `trailpuffer3` stops through `BL-224`'s existing `EndSustainedOn`
  path — no effects-side change.
- The instance survives the flight, so the trail is drawn *behind a flying piece* rather than at the
  tank, and the landing has something to dispatch into.
- The family gets its first engine-suite guard.

**No ray, no `water`/`lava` branch, no constant fall time.** Every one of these 150 events carries
`default` alone, so the surface table cannot be exercised here; reaching for it would mean building
`BL-245`'s prerequisite inside `BL-240`'s scope.

## Decisions (2026-08-02)

Settled in a grilling session against the census below. This table is the authority where the prose
disagrees with itself.

| # | Question | Decision |
|---|---|---|
| 1 | What ends the flight — analytic solve or a ground ray? | **Analytic return-to-launch-height**, `t = 2·v0.y / \|accel.y\|` — `SessionSpec.cs:157` builds no colliders under `--freecam` or in capture modes, so a ray would do nothing in exactly the modes goldens are shot in. |
| 2 | Does `BL-240` cover all 529 events? | **No — the ~150 upward launches only.** The 379 falls need a ground distance *and* the surface table, both from the same ray. Filed as `BL-245`. |
| 3 | The flight outlives its sequence runner. What keeps `CallSequence` reachable at landing? | **The instance stays alive while it owns a motion owing a bounce.** `SequenceRunner.cs:348-351` ends a runner the frame its last event fires, and all 150 launches *are* last. |
| 4 | How narrow is that predicate? | **Pending-bounce motions only** — not all live motions, which would pin 2,181 unbounded spins across 1,037 defs alive forever and re-open `BL-236` install-wide. |
| 5 | How is it guarded? | **An engine suite on motion + dispatch.** `BL-241`'s no-puffer harness does not block it: `BallisticMotionsLaunched` and `CallSequence` need no `PufferFactory`. |
| 6 | Is "lands at launch height" a decode? | **No — a ⚠ CHOICE.** The original tested real ground via `do_intersections`. Recorded as such in `MotionRuntime`'s doc-comment and `docs/formats/destructibles.md`. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "529 authored debris launches never move" — one uniform population (`BL-240` as originally filed) | A re-census over all 17,568 extracted defs: only 152 have an upward launch with negative gravity. 335 start from rest at `translation (0,0,0)`, 8 `chuteman` carry **gravity 0**, ~17 are thrown downward. For all 379 the analytic solve returns `t = 0` — the bug, unchanged. |
| 2 | "Pin the instance on any live motion" (the first shape of Decision 3) | `SpinMotion.cs:36` is `_runTime > 0f && _t >= _runTime`, and `AnimRuntime.cs:2059` builds spins with `run_time ?? 0f` — so an unbounded steady spin is **never** `Finished`. 2,181 of them across 1,037 def files would have become immortal instances, undoing `BL-236`'s teardown for every one owning an emitter. |
| 3 | "Returning a real duration will shift the rest of the sequence" | Measured: all 150 launches are the **last** event of their sequence (tail-length histogram: `{0: 150}`, no exceptions). `_base = _clock + duration` at `SequenceRunner.cs:202` has no successor to push. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1–A5 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | A2's landing rule | The *what* is settled (the piece must fly and land); "lands at launch height" is the ⚠ choice — record it, don't promote it to decode. |

## What the data actually ships

Censused 2026-08-02 over `extracted/**/*.json` (17,568 files), reproducing `BL-240`'s own headline
counts exactly: **7,458** `ObjectMotion` events, **733** carrying a `bounce_sequence`, **529** of
those with no `RUN_TIME` (217 def files).

The 529 split by launch shape:

| shape | count | example |
|---|---|---|
| upward launch, `translation_range` elev +60…+85°, gravity < 0 | **152** (150 in executed `sequences`) | `part3`/`part4` — `refuel`, `g_tower1`, `ftank01`, `m_build01`, `u_camp1` |
| free-fall from rest, `translation (0,0,0)`, gravity −9.8 | 335 | `gasbag1` (`multiplayer1zep`), `crashnode1` (`cargozep1_crash`) |
| thrown downward, elev −70…−90° | ~17 | `lifesaver11`'s `lifeboat`, `b_turret1`'s parts |
| `translation (0,−3,0)`, **gravity 0** | 8 | `chuteman` → `deactivate_chuteman` |

What makes the 152 tractable, all measured:

- **150 of 150 carry only the `default` branch.** Zero `water`, zero `lava`, zero `bounce_sound`.
- **150 of 150 name a sequence that exists in the same def** — `CallSequence` resolves locally, no
  cross-def lookup.
- **150 of 150 are the last event of their sequence.**
- Solved flight times: **median 3.81 s**, p10 1.43, p90 4.32, max 4.89, 13 under 0.5 s, none over
  8 s — against `part1`/`part2`'s *authored* `RUN_TIME` of 5.0 and 3.5 in the same def.

The canonical worked example is
`extracted/C1/cam_anim/refuel1-refuel1-healthy.json`, the clean A/B inside a single def:

```
seq0 node=part1  run_time=5.0   bounce=None
seq1 node=part2  run_time=3.5   bounce=None
seq2 node=part3  run_time=None  bounce={'default': 'sparkout3'}
seq4 node=part4  run_time=None  bounce={'default': 'sparkout4'}
```

**Blast radius, measured.** `MotionRuntime.Create` is already called unconditionally *before* the
`ballTime <= 0f` branch (`AnimRuntime.cs:2017`), so the seeded `_rng` draw count does not change and
`--det` captures stay byte-identical outside the affected defs. The only kill-bearing golden,
`c1-destroy-effects` (`--destroy=radiotwr.flt`, `analysis/goldens/manifest.json`), targets a def
carrying no `bounce_sequence` at all.

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

### Wave A — the launch

1. ☑ Baseline probe: confirm the branch these events actually take
2. ☑ Solve the flight time in `MotionRuntime`
3. ☑ Dispatch `BOUNCE_SEQUENCE` when the motion finishes
4. ☑ Keep the instance alive while a bounce is pending
5. ☐ Engine suite: motion launched, time in band, `sparkoutN` fired
6. ☐ Record the choice and close `BL-240`

## Dependency and parallelism notes

Items run in listed order; no parallelism. A1 gates everything — if the probe does not show the
deferred count, the diagnosis is wrong and A2–A6 do not apply. A2 → A3 → A4 is a hard chain: the
dispatch needs a solved time to fire at, and the pin exists only to make the dispatch land. A5
cannot pass before A4. File contention is total — A2/A3 both edit `MotionRuntime.cs`, A3/A4 both
edit `AnimRuntime.cs` — so none of these may run in parallel worktrees.

---

# Wave A — the launch

## A1 ☑ Baseline probe: confirm the branch these events actually take

**Verified (2026-08-03).** The diagnosis holds — but **not** by the probe `BL-240` specified, which
cannot work. Run:
`.\RunProbe.ps1 --freecam --chapter=C1 --destroy=refuel --debug-anim …` after
`dotnet build CSVM/CSVM.sln`. Five tanks died (`damage: -21 on refuel1..5 HP 20→0 DESTROYED — death
sequence run`).

What settles it is the per-frame motion log, not the counter:

- `part1` emits `anim/debug: part1 at (-6412,1, 55,6, -3375,1)` … at **changing** positions across
  seconds, then `anim: host 'part1' deactivated — emitter stopped`. It flies.
- `part3` emits **five** `anim: retarget 'large_fireball' onto 'part3' [caller refuel1…5]` lines —
  one per tank. That `CallAnimation` is the 2nd event of the same unnamed sequence whose 4th event is
  the `ObjectMotion(part3)`, so the sequence demonstrably runs and reaches the motion.
- `part3` never appears in the motion log; `part4` appears zero times (its sequence carries no
  `CallAnimation` to leave a trace).

Sequence reached, motion absent ⇒ the `ballTime <= 0f` → `Seek(0f)` branch, confirmed.

**The entry's stated probe is unrunnable, and `BL-240`'s trap note must be corrected.**
`Count("ObjectMotion(bounce_sequence deferred)")` lands in `_unhandled`, which is flushed only by
`ReportUnhandled()` — called once, inside the bootstrap census, immediately before
`_censusOpen = false` (`AnimRuntime.cs:1576-1577`). `--destroy` fires *after* the census closes, so
that counter can never print for a kill, whatever the branch. This is **LOG-16** ("a census printed
at the end of setup cannot report a runtime miss"), already on the books. The five-versus-ten
question the item raised is therefore moot: the answer is zero either way, and zero means nothing.

Also landed: **SHELL-12** in `docs/verification.md`. `--frames=N` is `ScreenshotFrames`
(`SessionSpec.cs:586`) and terminates a run only with `--screenshot`; passed alone it is silently
inert. That mistake left a `--debug-anim` run spinning for six hours and 45 MB.

**Baseline for A2–A5:** per kill, `part1`/`part2` move and deactivate; `part3`/`part4` never move;
`trailpuffer3` present. Post-A2 the same run must show `part3`/`part4` in the motion log at changing
positions.

### Original approach (kept for reference)

**Goal.** Establish, before any code, that a `--destroy=refuel` run reaches the `ballTime <= 0f`
branch on exactly the events the census says it should — and capture the baseline the later items
are measured against.

**Evidence (confidence: traced).** `AnimRuntime.cs:2034-2035` counts
`ObjectMotion(bounce_sequence deferred)` whenever a ballistic event carries a `bounce_sequence`.
`refuel1` has two such events (`part3`, `part4`) and a `--destroy=refuel` run kills five tanks, so
the count should read ten, not five — `BL-240`'s own trap note says "five", written before the
per-tank multiplier was checked. Settle which is right here; the number is the baseline, and a wrong
baseline is worse than none.

**Approach.** Build first — `dotnet build CSVM/CSVM.sln` — then
`.\RunProbe.ps1 --chapter=C1 --destroy=refuel --debug-anim`. Record the deferred count, the
`BallisticMotionsLaunched` value, and the emitter census at 1 s / 5 s / 10 s. Change nothing.

**Model recommendation.** medium, low effort — mechanical measurement against a stated expectation.

**Verify.** The instrument must be seen firing. A deferred count of zero is not "already fixed"; it
means the events take a different branch and the diagnosis is wrong — stop and re-diagnose rather
than proceeding.

**⚠ Traps.** `RunProbe.ps1` does **not** build (`BL-241`). A probe whose conclusion rests on output
NOT appearing must show the instrument firing somewhere first — this exact omission once turned a
missing instrument into a fabricated measurement.

## A2 ☑ Solve the flight time in `MotionRuntime`

**Landed (2026-08-03).** `MotionRuntime.FlightToLaunchHeight(v0y, ay)` returns `2·v0y / -ay` and 0
for anything with no apex; `Create` calls it when `data.Num("run_time") is null &&
data.Has("bounce_sequence")`, so an *authored* 0 still means zero. `AnimRuntime.cs:2020-2036` now
reads `motion.RunTime` back per body instead of trusting the authored value, and reports the longest
as the event's duration.

**One thing the plan did not foresee, and it mattered.** Three channels were already divided by
`rtSafe` *before* the solve could run — the `translation`/`translation_range` velocity ramps and,
critically, `_tumbleRate = fwdTotal / rtSafe` (`MotionRuntime.cs:177`). At `rtSafe == 0` all three
collapse to zero, so a naive solve would have made the debris fly without its authored tumble. The
launch is now collected into a `rampTotal` local and everything run-time-denominated is folded in
*after* the flight time settles. Confirmed in the arc: `part4` logs
`(-6466,1, 23,8, -3433,4) rot (5,8,…)` → `(-6481,2, 15,7, -3452,3) rot (8,7,…)` — descending past
apex, travelling in x/z, and turning.

**Verified — with an able-to-fail control.** `--freecam --chapter=C1 --destroy=refuel --debug-anim`
over five tank kills: **20** ballistic launches with the solve on, **10** with it disabled behind a
temporary `false &&` and rebuilt. Exactly +2 per tank (`part3`, `part4`), and the number is one that
can move. `.\RunTests.ps1` **PASS**: 352 units, 17/17 engine suites, engine errors clean, **13/13
goldens hash-identical** — `c1-destroy-effects` included, confirming the predicted zero blast
radius. Eight-chapter `--freecam` sweep (C1, C1B, C1C, C2, C2B, C3, C4, C5): every one exit 0, zero
errors; the goldens are the stronger unchanged-geometry evidence, being pixel-exact.

**Instrument fixed on the way (`LOG-5`).** `LogMotions` prints at most 12 motions
(`AnimRuntime.cs:3299`) and five tank kills put **34** in flight at once, so `part3` sat past the cap
and the truncated list read exactly like "it never launched" — which I briefly believed. The
per-second line now leads with `N live motion(s), M ballistic launch(es) so far`; the cumulative
counter is uncapped and is the only headless answer to whether a launch happened. `part3`'s own
position samples are still behind the cap; the counter A/B is what carries it.

**Not done here:** the bounce sequence still does not fire (A3), and the instance still dies at t=0
(A4), so `sparkout3`/`sparkout4` remain unreached and `trailpuffer3` still stops on `BL-236`'s
instance cap rather than at the landing.

### Original approach (kept for reference)

**Goal.** A bounce-terminated launch reports a real duration instead of 0, and its piece leaves the
wreck on its own parabola.

**Evidence (confidence: traced; the landing rule is direction-sound).** `AnimRuntime.cs:2014` reads
`ev.Data.Num("run_time") ?? 0f`. `MotionRuntime` already integrates
`origin(t) = held + v0·t + ½·accel·t²` with gravity folded into `accel` (`MotionRuntime.cs:69-70`),
so the return-to-launch-height root is `t = 2·v0.y / |accel.y|` with no new physics. Median 3.81 s
against sibling authored run times of 5.0 and 3.5 corroborates the magnitude.

**Approach.** The solve must live inside `MotionRuntime.Create` — the launch velocity is drawn there
from `rt._rng` (`MotionRuntime.cs:109-110`), so `AnimRuntime` cannot compute it without duplicating
the draw and desynchronising the seeded stream. `Create` resolves `v0`/`accel`, solves the root,
assigns `_runTime`, and exposes it; `AnimRuntime.cs:2014-2036` reads it back for both the
`ballTime <= 0f` decision and the `duration` it returns. Guard the solve to `v0.y > 0 && accel.y < 0`
and leave every other shape on today's `Seek(0f)` path untouched — that is `BL-245`.

**Model recommendation.** high — it carries the one epistemic choice in the plan and sets `_runTime`,
which the scale ramp's `u = t/_runTime` normalisation also reads (`MotionRuntime.cs:204`).

**Verify.** Re-run A1's probe: the deferred count is unchanged (A3 has not landed),
`BallisticMotionsLaunched` rises by 2 per tank, and `part3`/`part4` visibly leave the tank. Then the
full 8-chapter `--freecam` regression, zero errors, unchanged mesh/node counts.

**⚠ Traps.** **Absent `RUN_TIME` is not `RUN_TIME 0`** — do not default to a constant the way
`Projectile.cs:1626` gives its debris 2 s. Use the per-instance *drawn* `v0`, not the range midpoint:
the midpoint would make individual pieces land early or sink. `_runTime` also drives the scale ramp
— check the 45 SCALE events are unaffected.

## A3 ☑ Dispatch `BOUNCE_SEQUENCE` when the motion finishes

**Landed (2026-08-03).** `MotionRuntime.PendingBounce` carries `bounce_sequence.default`, armed only
inside the solved-flight block — so a body that owes a landing is exactly a body whose flight this
code decided. `TickMotions` removes the finished body *first* (the sequence it triggers may add
motions of its own) and then calls `CallSequence(Owner.Def, Owner.Anchor, bounce)`.

**Scope call the item left implicit, settled by census.** 204 events carry a bounce *and* an
authored `RUN_TIME`. Those do **not** arm: **102 of them name a live `water` branch** (`p1grndhit`,
`p1hit`, `bounce_effects` and their wet twins), and choosing a branch needs the struck collider
`BL-245` will cast for. Firing `default` at them would be wrong roughly half the time. They keep
reporting as deferred; only the solved launches are handled.

**The unhandled counter now means what it says.** `Count("ObjectMotion(bounce_sequence deferred)")`
fires only when nothing armed. A bounce this event armed *is* acted on, and filing it as unhandled
would report a working feature as a missing one — the same rule the retarget tallies already follow
(`AnimRuntime.cs:518-520`).

**Verified.** `--freecam --chapter=C1 --destroy=refuel --debug-anim`, five tank kills:

- **10 landings** — `'part3' landed — bounce sequence 'sparkout3'` ×5 and `'part4' landed — bounce
  sequence 'sparkout4'` ×5. (`part3` lands, incidentally re-confirming that A2's "part3 doesn't
  move" reading was the 12-entry log cap and nothing else.)
- **`anim: host 'part3' deactivated — emitter stopped` ×5, which is new** — pre-A3 only `part1` and
  `part2` ever deactivated. That is `sparkout3` genuinely running: its `OBJECT_ACTIVE_STATE part3
  INACTIVE` fired and stopped `trailpuffer3` through `BL-224`'s `EndSustainedOn`, with no
  effects-side change, exactly as the item predicted.
- The closing census is **6 ambient emitters, no `trailpuffer*` at all**.
- `part4` shows no emitter stop, correctly — only `part1`/`part2`/`part3` carry a `trailpufferN`.

`.\RunTests.ps1` **PASS**: 352 units, 17/17 engine suites (including `stop-sequence`, the
dispatch-side suite), engine errors clean, **13/13 goldens hash-identical**.

**⚠ This does not prove A4 unnecessary — and `refuel` cannot.** `refuel`'s sibling `seq0` runs
`part1` for an authored 5.0 s, and the two landings fall at ~4.3 s and ~4.9 s, so the instance is
still alive when they arrive, by a margin of tenths of a second. A def whose bounce launch outlives
every sibling sequence would hit `CallSequence`'s silent no-instance return
(`AnimRuntime.cs:3029-3030`) and dispatch nothing. A4's verify must pick such a def; `refuel` is
structurally unable to show the gap.

### Original approach (kept for reference)

**Goal.** The piece landing runs its `sparkoutN`, which deactivates the node and pops the fireball —
and stops the trail through the path that already exists.

**Evidence (confidence: traced).** `CallSequence` (`AnimRuntime.cs:3027`) already does the whole job
once something calls it, and all 150 events name a sequence present in the same def. `AddMotion`
stamps `motion.Owner = (def, anchor)` (`:3318`), and `TickMotions` (`:3327`) is where a finished
motion is swept — so the sweep has both halves of `CallSequence`'s signature in hand.

**Approach.** Carry the bounce name on `MotionRuntime` as `PendingBounce`, set in `Create` from
`bounce_sequence.default`. In `TickMotions`, when a motion reports `Finished`, dispatch the pending
bounce before removing it. Replace the `Count("ObjectMotion(bounce_sequence deferred)")` at `:2035`
with a counter that distinguishes dispatched-on-landing from still-deferred (the `BL-245` falls), so
the probe stays meaningful after this lands.

**Model recommendation.** medium — mechanical once A2 is in, but it touches the per-frame sweep.

**Verify.** The probe shows `sparkout3`/`sparkout4` firing ~3.8 s after the kill, `part3` going
inactive, and `trailpuffer3` leaving the emitter census at that moment rather than at `BL-236`'s 5 s
instance cap.

**⚠ Traps.** Only `default` exists on these 150 — do not build the `water`/`lava` selection here, it
needs the struck collider and belongs to `BL-245`. `CallSequence` silently returns when no instance
is live (`:3029-3030`); until A4 lands this will therefore *appear* to work on `refuel` (whose
sibling `seq0` holds the instance open to 5.0 s) and fail elsewhere. Do not conclude from `refuel`
alone that A4 is unnecessary.

## A4 ☑ Keep the instance alive while a bounce is pending

**Landed (2026-08-03).** `AnimRuntime.Retirable(inst)` is `inst.Finished &&
!HasPendingBounceFor(inst.Def, inst.Anchor)`, and it replaces the bare `inst.Finished` test at
**both** retirement sites — the per-frame instance walk *and* the t=0 finish at instance start,
which the item did not mention but which a launch armed at t=0 in a single-event sequence would
reach. `_motions` already carries `Owner`, so the predicate is a scan.

**A static model said this item was unnecessary; the measurement disagreed.** Modelling each def's
sibling-sequence timeline against its solved flight put **zero** of the 150 launches landing after
their instance ended (tightest margin −1.19 s, `tbase*`). That model was wrong in both directions —
it counted ON_CALL sequences that may never run, and treated a `Loop` as infinite even where a
`StopSequence` halts it. A direct probe found the gap on the first sweep:

```
m_build     landings=14  no-instance=1     <- before
m_build     landings=14  no-instance=0     <- after
refuel      landings=10  no-instance=0     (before and after — see A3)
pass_plane  landings=8   no-instance=0
```

**One instance in seven, and which one is a coin toss.** Each launch draws speed and elevation from
`translation_range`, so flight time varies per instance; a longer draw outlives the sibling holding
the instance open. This was never going to be a reproducible failure — it is an intermittent,
seed-dependent lost bounce, which is exactly the kind that survives a casual look.

**The miss is now counted, not silent.** `TickMotions` checks `InstanceOf` before dispatching and
files `ObjectMotion(bounce landed after its instance ended)` when there is nothing to dispatch into,
with a `--debug-anim` line saying so. That counter is what made the 1-in-7 visible, and it is the
control proving the fix can fail.

**Verified — including the negative the item demanded.** Emitter census for the `BL-236` regression
set: **C3 = 11, C4 = 15, C5 = 36 active puffers**, matching `docs/HISTORY.md`'s recorded post-`BL-234`
counts exactly, so no spin-bearing instance was pinned by the narrow predicate. `.\RunTests.ps1`
**PASS**: 352 units, 17/17 engine suites, engine errors clean, **13/13 goldens hash-identical**.

`SequenceRunner.cs`'s two comments now say instance end is the exception to "resources outlive their
sequence" — on `Halt()` and on `AnimInstance.Finished`, which is explicitly no longer the retirement
test on its own.

### Original approach (kept for reference)

**Goal.** The landing always has a live instance to dispatch into, on every def — not just the ones
where a sibling sequence happens to run long enough.

**Evidence (confidence: traced).** `SequenceRunner.cs:348-351` sets `_done = true` the frame
`_pc >= _seq.Events.Count`, regardless of the duration the last event returned; `AnimInstance.Advance`
then removes the runner (`:65-66`) and `Finished => Runners.Count == 0` (`:58`) goes true. Every one
of the 150 launches is its sequence's last event, so the instance dies at t=0 while the flight has
~3.8 s to run.

**Approach.** `AnimInstance.Finished` gains a second condition: no live motion owned by
`(Def, Anchor)` still owes a bounce. `_motions` already carries `Owner`, so this is a scan, not new
bookkeeping. Amend `SequenceRunner.cs:179`'s "resources outlive their sequence" comment to record
that instance end is now the exception, and say why.

**Model recommendation.** high — this is the item with real blast radius; the predicate's width is
the difference between fixing 150 events and re-opening `BL-236` across 1,037 defs.

**Verify.** Probe a def *without* a long sibling sequence and confirm the bounce still fires. Then
confirm the negative: the emitter census for the `BL-236` regression set (`c3-island`, `c4-snow`,
`c5-city-night`) is unchanged, proving no spin-bearing instance was pinned. Re-run all 13 goldens.

**⚠ Traps.** **Do not pin on any live motion** — `SpinMotion.cs:36` never reports `Finished` for the
2,181 unbounded spins, and `AnimRuntime.cs:2067`'s idempotent re-assertion keeps them in `_motions`
indefinitely. Pin on a pending bounce alone, which self-expires because `MotionRuntime` always
finishes.

## A5 ☐ Engine suite: motion launched, time in band, `sparkoutN` fired

**Goal.** The first regression guard this bug family has ever had.

**Evidence (confidence: traced).** `BL-241` records that `TestHarness.BuildWorld` builds no emitters,
so emitter lifetime cannot be asserted — but this fix's assertions are motion and dispatch.
`BallisticMotionsLaunched` is already a counter on `AnimRuntime`, and the bounce firing is a
`CallSequence`. Neither reads the `PufferFactory`, so `BL-241` is not a blocker and does not need to
land first.

**Approach.** Kill a `refuel*` tank in the harness via `DamageAt` (the route `BL-241` names). Assert
`BallisticMotionsLaunched` rises by 2, the solved time for `part3` falls in a band consistent with
the census rather than an exact value, and `sparkout3`/`sparkout4` dispatch before the instance ends.
Seed `_rng` so the band is deterministic.

**Model recommendation.** medium — a suite against settled behaviour.

**Verify.** `.\RunTests.ps1` full pass. Then break A2 deliberately and confirm the suite fails — an
unchanged green is not evidence unless it has been seen able to fail.

**⚠ Traps.** Assert a band, not an exact time: the launch is a random draw within
`translation_range`. Do not extend this suite to the emitter census — that needs `BL-241`'s
`TexturesOutliveBuild` change and is a separate item.

## A6 ☐ Record the choice and close `BL-240`

**Goal.** The next cold reader finds the landing rule marked as a choice, with the ray named as what
replaces it — and `backlog.md` no longer carries a solved item.

**Approach.** Add the ⚠ note to `MotionRuntime`'s doc-comment beside the existing TUNE caveats;
extend `docs/formats/destructibles.md:179/239`, which already say ground-rest is deferred, to state
what now happens instead and for which subset. Dated `docs/HISTORY.md` entry carrying the census, the
150/379 split, and the three disproven claims above. Then `/close-backlog-item BL-240` — deleting the
entry, not marking it fixed — leaving `BL-245` open and cross-referenced.

**Model recommendation.** medium — prose, but it is the load-bearing record of a choice.

**Verify.** Grep that no doc still says a bounce-terminated launch is wholly deferred without naming
the 150/379 split.

**⚠ Traps.** Do not let the HISTORY entry restate "529 debris launches never move" — that premise is
disproven and this plan is where it dies. `BL-245` must survive `BL-240`'s closure.
