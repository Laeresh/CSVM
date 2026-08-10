# The animation-sequence runtime, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), and cross-checked against the original running at
60 fps and at 120 fps, against install-wide censuses of the compiled event archives, and against
timed measurements of our own re-implementation. Every claim below names the function, the census,
or the measurement it came from.

Everything here is a description of *behaviour and constants*. No decompiler output is reproduced;
the addresses are given so any claim can be re-checked at source.

**Where the other halves live.** The authored side — the definition/sequence/event schema, the
`START_TIME` encoding, `growth_factors`, the `ACTIVATION` values, the compiled-vs-reader
divergences — is [`formats/anim-definitions.md`](../formats/anim-definitions.md). Our
implementation is `CSVM/src/Mech3/SequenceRunner.cs` (the interpreter, engine-free behind a
3-member host seam), `CSVM/src/Mech3/AnimRuntime.cs` (the host: event dispatch, condition
evaluation, instance lifetime) and `CSVM/src/Mech3/AnimDefs.cs` (the reader-form normalizer). The
headless charter that pins every rule on this page is `CSVM.Tests/SequenceRunnerTests.cs`. This
page is the original's runtime: what the engine does with an authored sequence once it is running.

⚠ **This page is a decode, not a proposal.** Where it disagrees with a footage measurement, the
decode wins and the disagreement is a note. Where CSVM deliberately differs, that is listed at the
bottom rather than hidden.

## Function map

| Address | Role |
|---|---|
| `FUN_004ecbb0` | The sequence stepper — dispatches the event at the cursor, advances it, and returns a state code. A LOOP rewind always **returns** (state 4), so the rewound pass can only begin on the FOLLOWING tick |
| `FUN_004ebfd0` | The `LOOP` handler — the u16 pass counter at `+0x30`, the increment-then-compare termination test, the `-1` infinite special case, and the 86,400 s clamp on the instance clock |
| `FUN_004ec080` | The **false-IF** branch scan — walks forward event by event and breaks on the first ELSE / ELSEIF / ENDIF, with **no depth counter** |
| `004ec5a0` | The **ELSE/ELSEIF fall-through** scan — the same walk with the narrower stop set: ENDIF only |
| `FUN_004ec6a0` | `FBFX_COLOR_FROM_TO`, dispatch slot 36 — the model for "a timed event reports STILL RUNNING until its run time is up" |
| `004e82b0` | `LIGHT_ANIMATION`, dispatch slot 5 — advances one tick's worth of the authored delta per dispatch, clamps the last tick to the remainder, and returns still-running on the same test |
| `FUN_004f7120` | `PUFFER_STATE` reader/parser — reached from a sequence's `PUFFER_STATE` event; decoded in [`org/puffer.md`](puffer.md) |

Structure offsets the runtime reads:

| Offset | Meaning |
|---|---|
| `anim+0xb0` | The **instance** clock — one clock per running definition, shared by every sequence of it |
| `seq+0x24` | The sequence's own start instant (origin `Sequence`) |
| `seq+0x28` | The previous event's completion instant (origin `Event`) |
| `seq+0x30` | The u16 LOOP pass counter |

## Three clocks, and which origin reads which

An event's `START_TIME` carries an ORIGIN and a delay, and the origin selects the clock:

- **`Animation`** — absolute against the INSTANCE clock (`anim+0xb0`). That clock starts when the
  definition starts and is never rewound; a LOOP rewinds only the sequence's own timers.
- **`Sequence`** — absolute against this sequence's start (`seq+0x24`).
- **`Event`**, and an ABSENT start alike — relative to the previous event's **completion**
  (`seq+0x28`), i.e. when it fired plus its own run time.

The `Animation`/`Sequence` split is not academic: a sequence a `CALL_SEQUENCE` started partway
through the animation runs on a sequence clock that begins at zero while the instance clock is
already running. **191 shipped events read the difference** — `ap_light_seq`'s `LightAnimation`
chain, `chuteman_drop`, and the 10 s puffer shut-off on every rocket and torpedo trail. Gated
against the called sequence's own clock instead, a `Animation 1.5` on a sequence called at t=1.0 s
fires a full second late.

⚠ **An absent start ENCODES as `Animation + 0.0` and must not be routed through the instance
clock.** The install carries ~36,000 unstamped compiled events; gating them on a clock that is
already running would fire every one of them the instant its sequence starts, whatever else was
still pending.

**The offset belongs to the event that CARRIES it, not to its successor.** Reading the stamp off
the event just fired and applying it to the next one shifts every sequence in the install by one
slot: a timestamped event fires one slot early and its unstamped partner one slot late.

### The SWAP-pair stamping rule

C1's `bowl` sign is the clean demonstration and the measurement that settled it. Its compiled
sequence is nine strict `des_on`/`des_off` `OBJECT_ACTIVE_STATE` pairs plus an infinite `Loop`, and
**only the FIRST of each pair carries a timestamp** — the second fires immediately after its
stamped predecessor, so the pair lands together and the sign flashes. Read with the offsets shifted
by one slot, every pair splits: both variants are lit at t=0 and then **nothing at all** for each
gap. Measured face-on at the sign: **38.0 % of frames completely blank** under the shifted reading,
**0 %** under the correct one — the sign disables and re-enables itself instead of flashing.

Control-flow events (LOOP/IF/ELSEIF/…) take no time and so do not advance the previous-completion
instant, but they ARE gated on their own offset. That is what gives the bowl sign's trailing
`Loop {Event 1.2}` its inter-cycle pause: a LOOP branch that hard-resets the gate to zero discards
the authored pause and the sign cycles without one.

## The LOOP

`FUN_004ebfd0` does three things, in this order: increment the u16 pass counter at `+0x30`;
terminate the sequence if the counter now **equals** the authored count; otherwise rewind the
cursor to the top of the sequence. The count is re-read off the event struct on every visit rather
than cached. An authored **`-1` is special-cased infinite** regardless of the counter.

**An authored `0` is infinite as a CONSEQUENCE of this shape, not a case of its own.** The counter
starts at 0 and is incremented *before* the compare, so after the first visit it only ever grows
and can never equal 0 again. Surveyed across the whole install, **26 ground-vehicle route
animations ship `Count 0`** — C1's police, mafia, black_car and truck traffic, C2's and C3/M02's
studebakers, every one of them the LAST event of its sequence — and the original drives all 26
continuously for exactly that reason. Reading 0 as "stop immediately" makes each car drive its
route once and freeze. No normalisation of the value is needed to say so.

⚠ A real u16 wrap would re-hit 0 at pass 65,536. That wrap is not reproduced: it is unreachable
within a session, and `-1` already covers the deliberately-infinite case.

⚠ `FUN_004ebfd0` clamps the instance clock at 86,400 s (one day). Not reproduced — a session never
gets there.

### There is no free first pass

Traced against `FUN_004ecbb0`: a rewind always **returns** (state 4), so the next pass can only
start on the following tick. Pass 1 costs a tick exactly like every other pass, and 200 passes cost
200 ticks. A down-counting reading spends `authored + 1` passes; measured on the `LOOP 200` fixture
at a 1/60 s step that reads **3.3499975 s** against the correct **3.3333309 s** — a full animation
frame long, not a compensating error that happened to land on the right total.

## A LOOP count is a count of authored ANIMATION FRAMES, at 60 Hz of SIM time

`ref_fueltanks`' `fire_n_smoke` is `[PUFFER_STATE, LOOP 200]` and burns **~3 s** in the original:
200/3 ≈ 60. The same burn was then timed against the original **at 60 fps and at 120 fps and took
the SAME time at both**. Per-rendered-frame ticking would have halved it at 120, so the original's
sequence tick is decoupled from rendering and every untimed count is a real authored **duration**:
one authored animation frame is **1/60 s**, and a `LOOP n` over an instantaneous body is a timer of
`n × 1/60` seconds.

⚠ **The animation frame is its own constant, not the simulation step**, though the two are equal
today. "The rate the original's artists counted frames at" and "the rate we step the simulation at"
are independent facts; re-stepping the sim at 1/120 for physics reasons must not halve every
authored animation timer. (Their equality today is also what keeps deterministic captures
byte-identical — exactly one pass per fixed step.)

## The rollover must carry its remainder, never reset the clock to zero

When a loop rolls over it rewinds the sequence clock. **The overshoot past the gate it just
satisfied has to be carried into the next iteration** — `clock − due` — rather than dropped.
Dropping it rounds every iteration up to the next whole step, forever, and the failure has two
distinct faces:

- **Instantaneous iterations** quantise to the RENDER rate instead of the authored 60 Hz. A 144 Hz
  client needs three steps to accumulate 1/60 s, so every authored timer runs at 48 Hz; a 240 Hz
  client runs them 4× fast; a 30 Hz client at half speed.
- **Timed iterations** — a LOOP carrying its own authored period — round that period up to a whole
  step. Measured over the install's **599 timed loops**: a 0.02 s period costs 2 steps instead of
  1.2 at 60 Hz, so the **126 loops carrying it** (`patrolboat`, `ptboat*`, `ftank_boom*`,
  `m_build0*`, `pass_plane0*`, `sub_destruction`, `balloont_die*`, `refuel*`) run at **60 % speed**.
  C3/M05's `ww_balmoral1/2/3` (`LOOP 1000` at a 0.01 s period, authored 10 s) take **16.7 s** at
  60 Hz, 12.5 s at 240 Hz, and only reach 10.0 s at 600 Hz.

**Even periods that divide 1/60 exactly in real arithmetic pay one extra step per iteration**, and
this is the detail that makes the carry non-negotiable: sixty float32 additions of 1/60 land just
*under* 1.0, and the gate is `>=`. Without the carry that is 61 steps per second, forever, on every
route loop — a car running 1.7 % slow for the whole session.

Carried, the residual is bounded by **one step**, never by the count: it cannot compound over
hundreds of iterations, which is the property the long-horizon fixtures (60 s at periods of 0.02 s,
1.0 s and 2.5 s) exist to pin. A carry applied *twice* is the other failure direction, and runs
fast without bound.

## A loop that carries its own period is TIMED DATA, not a tick counter

The animation-frame pacing above applies only to an iteration that scheduled **no** time. The test
is "did the DATA schedule time?", and an authored offset counts **even when the clock has already
run past it** — because an absolute (`Animation`/`Sequence`) period shorter than one step is
already behind the clock by the time it is gated. Testing "is the gate in the future?" instead
makes it a question about the STEP: `ww_balmoral1/2/3`'s 0.01 s period is below the 0.0167 s
animation frame, reads as instantaneous, collects the frame floor meant for untimed poll loops, and
the three of them run 16.7 s instead of their authored 10 s — reached by a route the floor is
explicitly not supposed to reach.

An authored **0** still means "immediately after the previous event", so the untimed poll idiom
keeps its pacing and its guard.

**A loop whose body takes time must start its next iteration at once**, not yield: it is already
waiting on its gate. With a 1.0 s motion, the loop rolls over on the step the clock reaches 1.0 s
and the motion re-fires within that same step.

## The double-poll guard

A loop over purely instantaneous events is the data's "keep this animation alive" idiom. Two shapes
ship: C1's waterfall, `[PUFFER_STATE ×3, LOOP{-1}]`, whose emitters then run on their own
`TIME_INTERVAL`; and the poll, `If … CallAnimation; Endif; LOOP{-1}`.

Such an iteration is paced to **one pass per authored animation frame**, and the pacing test must
be "did this iteration schedule any time?" — **not** "is the clock zero". The rollover resets the
clock, so a clock-zero test reads the next step's `dt` at that point and lets the instantaneous
body run a **second** time before yielding. Measured: every poll loop in the chapter costs double.

The frame floor is a **floor, not an addition**: an authored offset still wins whenever it is the
longer wait (the bowl sign's 1.2 s pause). And a frame gate is not the body scheduling time — count
it as such and the NEXT pass reads as timed and spins ungated.

## The nested IF scan, and the branch stack

The engine walks event by event and breaks on the **first** byte in its stop set, with **no depth
counter**. The two stop sets are different, and must stay separate:

| Jump | Address | Stops at |
|---|---|---|
| A **false** IF/ELSEIF condition | `FUN_004ec080` | ELSE, ELSEIF or ENDIF — whichever comes first |
| The fall-through off a **taken** branch | `004ec5a0` | ENDIF only |

**This is observable, not academic.** 48 shipped sequences nest — every chapter's
`gunhit-*slug_gunhit` and `mag_gunhit-*`, played on every gun impact — and all 48 have one shape:

```
If lod / If range / If weight … Elseif weight … Else Endif / Else Endif / Endif
```

A false OUTER condition lands on the INNER chain's `Elseif` and **re-tests it**, so the impact
light still fires on its 20 % random roll with the LOD gate and the 1 km range gate both failed. A
depth-aware scan skips the whole chain and fires nothing. That reads like a compiler bug in the
original, and it is what the original does.

The original carries **no per-chain "has a branch run yet" state**: it distinguishes a
fall-through from a candidate by ARRIVAL. A branch marker the stepper walked into is dispatched (it
is a fall-through, and jumps to the ENDIF); one that a failed condition's scan landed on is the
next candidate to test. Our runner stands that up with an explicit stack of one flag per open IF,
which is a stand-in for the arrival distinction rather than a model of it.

⚠ **The stand-in has one visible residual.** It needs a chain whose inner IF closes BEFORE the
outer chain's next branch marker: there the depth-free scan pops the one open frame on the inner
ENDIF, the chain behind it reads as unopened, and its ELSE body can run in addition to a taken
ELSEIF. **No shipped definition has that shape** — all 48 nesting sequences close inner-first,
where the two readings agree.

A malformed chain (an ELSE with no open IF) reads as "not taken" and its writes are dropped, so bad
data degrades to **running** the branch rather than faulting — the safe direction, since a skipped
branch poses objects wrongly.

## CALL_SEQUENCE and STOP_SEQUENCE: a sequence is a single instance

The original keeps a sequence's execution state **inside the definition's own sequence array**, so
a sequence cannot run two copies of itself, and both calls are tiny state writes.

**CALL_SEQUENCE** resolves the name to an index in the definition's OWN array and does exactly one
thing: `if (state == parked) state = running;`. Two consequences, both load-bearing in the shipped
data:

- A call into a sequence that is **already running** is a silent no-op. **77 definitions call one
  sequence from more than one site** (`sonic_ground_effect` calls `sonic_light_seq` twice, `player`
  calls `destroy_craft` three times); each pair must produce ONE run of the body.
- A call naming a sequence whose authored activation is **not `ON_CALL`** is also a no-op — such a
  sequence is never parked, because it runs with the animation and then stops done. **6 definitions
  do this** (`reflight1..6 → refinery_light_seq`).

⚠ The call reports whether the definition **HAS** that sequence, not whether anything started. A
no-op call must still report found, or a `CALL_ANIMATION` fallback fires on a name that was there.

**STOP_SEQUENCE** resolves the name the same way and then unconditionally marks that sequence
DONE. There is no start-if-not-running path anywhere in it. Since a call can only start from
PARKED, **stopping a parked ON_CALL sequence is a DISABLE**: it stays un-callable until the whole
definition resets. **16 shipped definitions hit exactly that** — `flame_ball_01`/`flame_ball_02` →
`stop_p1trail`, in every chapter, inside the HE explosion's call chain — so the teardown those
definitions name simply never runs.

A stop also reaches the caller's OWN runner, which is the data's break-out-of-my-own-IF-chain
idiom (`test_player` ×33, `setprop` ×8), and it takes effect within the same tick rather than at
the next one.

⚠ Identity for "is this sequence running" is the sequence OBJECT, not its name — the empty name is
not unique (`he_ground_effect` ships two unnamed sequences), and a name-keyed test collapses them
into one and loses half the burst.

⚠ Halting a runner does not touch what it already launched. Motions and puffers have authored
lifetimes and outlive the sequence that started them.

## WAIT_FOR_COMPLETION gates the NEXT event, not the runner's lifetime

A `CALL_ANIMATION` carrying the flag holds its sequence while the callee it actually reached is
still running. The hold is a **predicate**, not a duration — the callee's own length is not knowable
at the call (its sequences can call further sequences, and an SI script's run time lives in a
separate archive) — and it must test the **exact instances this call reached**, since a pooled
template copy is chosen at dispatch and asking again by name would take another pool slot.

**The scope rule is the data's.** Censused over both front-ends: of the **2,999 flagged calls the
runtime can reach, 2,770 are the LAST event of their block, and 2,770 of those name a callee that
never terminates** — the `sputter_fire` / `sputter_black_smoke` / `sputter_fire_smoke` /
`gen_drop_ladder` `LOOP{-1}` idiom. Not one flagged call with an event behind it names a
never-terminating callee, in either front-end. So a runner-lifetime reading wedges 2,770 authored
sequences open for the session, while a next-event reading is consistent with the whole install
with zero exceptions.

- A callee whose whole choreography fires at t=0 never becomes a live instance, so there is nothing
  to hold on and the caller advances in the same pass. 16 of the census's effective holds are that
  shape.
- **The wait REPLACES the call's duration**: a trailing `Event + t` offset is measured from the
  callee's END, not from when the call fired. Measured from the call, the offset is already behind
  the clock at release and collapses to zero, losing the authored pause.
- The hold is real elapsed time, so an enclosing LOOP must **not** read that iteration as
  instantaneous and charge it the animation-frame floor.
- A hold inside a poll loop stops the whole loop, not merely the next event — otherwise five
  seconds of the poll idiom re-fire its body ~300 times behind the hold.

The authored case is `player_crash_water`/`destroy_crash`: a flagged call of `plane_big_splash`,
then `large_steam_spray`. Without the hold both retarget on the same tick, though the splash's own
choreography runs 3.0 s.

⚠ `0` and `null` are different authored states for this flag (**3,639** vs **53,019** events), and
only a set flag holds.

## Timed events, and what a handler reports back

The stepper needs each dispatched event's run time to place the next one. Two handlers traced (D31)
establish the pattern:

- `LIGHT_ANIMATION` (`004e82b0`, dispatch slot 5) advances the light by one tick's worth of the
  authored delta per dispatch, clamps the last tick to the remainder, and returns **still running**
  until the run time is up.
- `FBFX_COLOR_FROM_TO` (`FUN_004ec6a0`, dispatch slot 36) interpolates RGBA linearly from `from`
  toward `to` over the run time, clamps each channel to 0..1, packs RGB into one frame-buffer pixel
  value and hands it plus the alpha — **separately, as a blend weight, not a premultiplied
  component** — to the single global frame-buffer-effect object. It returns still running on the
  same test.

The consequence for the sequence is the same in both cases: **the ramp is the event's duration and
the chain waits for it.** Reporting 0 collapses `he_light_seq`'s authored 0.41 s flicker — seven
ramps — into a single frame, each tween overwriting the previous, and `he_ground_effect`'s six-step
1.2 s white↔violet wash into one instant.

## Sequences of one definition run CONCURRENTLY

A definition instance is an anchor plus one runner per active sequence. The C1 train drives its
four cars from four sibling sequences, each with its own SI script and its own loop, all against
the one shared instance clock. Each runner retires the moment its own cursor runs off the end.

The train is also what pins the absent-start reading: its sequences are
`[ObjectMotionSiScript, LOOP{-1}]` with no start offsets anywhere, and only "as soon as the
previous event COMPLETES" turns that into the surveyed **~327 s track loop** instead of a
zero-length infinite loop.

⚠ **"No runner is still executing" is not on its own the test for retiring an instance.** A motion
that still owes a `BOUNCE_SEQUENCE` holds its instance open: such a launch is the last event of its
sequence, and its runner ends the moment the piece leaves the ground, while the landing has still to
dispatch into a sequence.

## Where CSVM deliberately differs

Everything here is a known, deliberate divergence — not a gap waiting to be closed.

| Divergence | Why |
|---|---|
| **The `STOP_SEQUENCE` DISABLE is not persisted** | A halt is not remembered, so a later `CALL_SEQUENCE` on the same name starts the sequence again, where the original's DONE state refuses it until the definition resets. **123 definitions name one sequence in both a call and a stop** (mostly `flame_light_seq`), but whether any reaches the stop BEFORE the call at run time is a control-flow question a static census cannot answer — persisting the flag would move all 123 on a divergence none of them is known to observe |
| **The branch-taken stack** stands in for the original's arrival distinction | See the residual above: it needs a nesting shape no shipped definition has |
| **A 256-dispatch-per-tick guard** | Bounds a zero-length sequence within one tick; the remainder defers to the next. The original has no such cap |
| **The u16 pass-counter wrap** at 65,536, not reproduced | Unreachable within a session, and `-1` already spells "infinite" |
| **The 86,400 s instance-clock clamp** (`FUN_004ebfd0`), not reproduced | A session never reaches a day |
| **`LIGHT_ANIMATION` ramps asynchronously** rather than one delta-tick per dispatch | Same picture only because the sequence is also held for the run time; the tick-by-tick advance is not reproduced |
| **The animation frame is its own constant**, not the simulation step | They are equal today, but they are independent facts — see the trap above |
