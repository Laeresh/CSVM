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
divergences — is [`formats/anim-definitions.md`](../formats/anim-definitions.md), which states the
mechanisms this page decoded but does not repeat the decode: every address, struct offset and
handler trace for the animation runtime belongs here. Our
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
| `FUN_004ebfa0` | The sequence reset — rewind and re-arm: `state ← +0x21`, `event ptr ← +0x38`, both timers to 0 |
| `FUN_004ebfd0` | The `LOOP` handler — the u16 pass counter at `+0x30`, the increment-then-compare termination test, the `-1` infinite special case, and the 86,400 s clamp on the instance clock |
| `DAT_00727de0` | The event dispatch table, 47 slots, index 1–0x2f, populated by `FUN_004ee1a0` |
| `FUN_004ec080` | The `IF`/`ELSEIF` condition evaluator, and the **false-IF** branch scan — walks forward event by event and breaks on the first ELSE / ELSEIF / ENDIF, with **no depth counter** |
| `004ec5a0` | The **ELSE/ELSEIF fall-through** scan — the same walk with the narrower stop set: ENDIF only |
| `004ec5d0` | `ENDIF` — a bare `MOV EAX,2 / RET`, i.e. a no-op |
| `004eb570` / `004eb610` | `CALL_SEQUENCE` / `STOP_SEQUENCE` |
| `004ecedc`–`004ecf53` | The per-instance **tick walk** — one ascending pass over the sequence array, stepping each slot at most once. The containing function is undefined in the database; the loop block is the citable unit |
| `FUN_00516820` | The reader-side condition parser — one branch per keyword, writing the condition flag word |
| `FUN_004ec6a0` | `FBFX_COLOR_FROM_TO`, dispatch slot 36 — the model for "a timed event reports STILL RUNNING until its run time is up" |
| `004e82b0` | `LIGHT_ANIMATION`, dispatch slot 5 — advances one tick's worth of the authored delta per dispatch, clamps the last tick to the remainder, and returns still-running on the same test |
| `FUN_004f7120` | `PUFFER_STATE` reader/parser — reached from a sequence's `PUFFER_STATE` event; decoded in [`org/puffer.md`](puffer.md) |

The interpreter was compiled from `D:\zipper\gamez\zEffect\zeff_ani*.c`; the image base is
`0x400000`.

**The live sequence struct is mech3ax's `SeqDefInfoC`** (64 bytes), mutated in place by the stepper
on every tick. These are the offsets the runtime reads:

| Offset | Meaning |
|---|---|
| `anim+0xb0` | The **instance** clock — one clock per running definition, shared by every sequence of it |
| `anim+0xcc` | Base of the definition's **sequence array** — `SeqDefInfoC` records, stride `0x40` |
| `anim+0xd8` | Sequence **count**, one byte; the loop bound for every walk of the array |
| `seq+0x20` | Current state |
| `seq+0x21` | Reset state (`SeqDefState`: `Initial`=0, `OnCall`=3) |
| `seq+0x24` | The sequence's own start instant (origin `Sequence`) |
| `seq+0x28` | The previous event's completion instant (origin `Event`) |
| `seq+0x2c` | Accumulated loop time |
| `seq+0x30` | The u16 LOOP pass counter |
| `seq+0x34` / `+0x38` / `+0x3c` | Current event pointer / start pointer / byte size |

⚠ mech3ax's own field-name guesses at offsets 36/40/44 (`loop_time` / `event_time` / `seq_time`)
are mis-ordered against this layout — compare `+0x24`/`+0x28`/`+0x2c` above against offsets 36/40/44
decimal. They are not load-bearing for round-tripping (nothing reads a compiled def by field name),
so the fork's names were left alone; this paragraph is the correction, and renaming the fork's
struct field is explicitly out of scope for this record (it is a round-trip-test surface and belongs
in its own change).

## The handler state machine

Every handler returns one of four values, and the stepper writes the result into the state byte at
`+0x20`:

| Return | Meaning |
|---|---|
| **2** | event complete — advance to the next event |
| **1** | still running — re-dispatch the SAME event next tick |
| **4** | sequence rewound by `LOOP` — re-gate from the top of the sequence, and yield the rest of this tick |
| *(state)* **3** | parked, awaiting a `CALL_SEQUENCE` (not a return value — a resting state) |

The start-time gate is evaluated only when the stepper is in state 0 (freshly advanced past a
completed event) or state 4 (freshly rewound), i.e. only once the previous event has actually
reported completion, never mid-event. An `ON_CALL` sequence that runs off the end of its event list
is rewound and re-parked at state 3, ready to be called again; an `Initial` sequence instead stops
at state 2 and does not re-arm.

## The opcode table

The 47-slot dispatch table at `DAT_00727de0` (populated by `FUN_004ee1a0`, read directly rather
than reconstructed from behaviour) matches mech3ax's `EventType` enum exactly, slot for slot,
**including the null slot 29** (`FUN_004ee1a0` writes `_DAT_00727e54 = 0`), which mech3ax also marks
as a gap. That agreement is an independent confirmation the fork's event numbering is correct, not
an assumption inherited from it. The exe additionally has real handlers at slots 38, 43, 44 and 45
(`LAB_004ecb70`, `LAB_004eac40`, `FUN_004eac90`, `FUN_004eadd0` respectively) that mech3ax leaves
undecoded; no shipped def uses any of the four, so nothing downstream needs them yet.

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

## The condition flag word: fourteen kinds, ten authored

A compiled condition record carries a bitmask at `+0xc`, a node index at `+0x10`, and one or two
values at `+0x14`/`+0x18`. The bit for each token is written by the reader parser `FUN_00516820`,
one branch per keyword, so this mapping is read off the parser rather than inferred from the
evaluator's branch order:

| Bit | Token | Evaluated as (`FUN_004ec080`) |
|---|---|---|
| `0x1` | `RANDOM_WEIGHT` | `table[i] <= value`, see the ring below |
| `0x2` | `PLAYER_RANGE` | `dist²(anchor, player) <= value` (`FUN_004ec540`) |
| `0x4` | `ANIMATION_LOD` | `value <= DAT_00727fc8` (the detail setting) |
| `0x8` | `PLAYER_UNDERCOVER` | upward 50 m probe from the player (`FUN_004ec410`) |
| `0x10` | `NODE_UNDERCOVER` | the same probe from the node |
| `0x20` | `HW_RENDER` | `DAT_009be708` |
| `0x40` | `PLAYER_1ST_PERSON` | `DAT_009fd17c` |
| `0x80` | `PLAYER_BELOW_ALT` | `playerY < value` |
| `0x100` | `NODE_BELOW_ALT` | `nodeY < value` |
| `0x200` | `PLAYER_LINED_UP` | `θ² <= value`, see below |
| `0x400` | `PLAYER_SPEED` | `min <= playerSpeed <= max` |
| `0x800` | `ANIM_HEALTH` | `health <= value` |
| `0x1000` | `ANIM_HEALTH` (two-value) | `min <= health <= max` |
| `0x2000` | `NODE_ACTIVE` | node flag `0x4` at `+0x24` |

**Four of the fourteen are never authored**: `PLAYER_UNDERCOVER`, `PLAYER_BELOW_ALT`,
`PLAYER_LINED_UP` and `PLAYER_SPEED` are engine features the level data never used, which is why
the condition census in [`formats/anim-definitions.md`](../formats/anim-definitions.md) finds ten
kinds and not fourteen. Swept 2026-08-13 over the whole extraction: **0 occurrences** of any of the
four, in either the reader spelling or the compiled one, and 0 for the `PLAYER_NEAR_GROUND` alias
the parser also accepts, against **1,097 occurrences of `PLAYER_RANGE`** on the same sweep as a
control. The reader tokens are the parser's own literal keywords and the reader sources are what
the compiled archives are built from, so that half is a direct test rather than an inference.
Nothing here is a gap in CSVM: there is no shipped condition to evaluate.

⚠ **`PLAYER_LINED_UP` owns the `* 4.0` that looked like a `PLAYER_RANGE` scale factor.** The
evaluator's `local_18 * 4.0 <= value` sits in the `0x200` branch, and that branch is angular, not
metric: `FUN_004cf380`/`FUN_0053df30` take the node's world matrix to Euler angles, `FUN_0053f610`
builds a quaternion from them, `FUN_0053f9b0` multiplies it against the player quaternion at
`DAT_009fd190` with conjugation, and `FUN_0053fca0` is the quaternion log map, whose `atan2(|v|, w)`
is the **half**-angle. So `|out|² * 4.0` is θ², compared against a threshold the parser stores as
`(degrees × 0.017453292)²`. Both sides are squared radians and the 4 is the half-angle cancelling,
with nothing left over. `PLAYER_RANGE` is the separate `0x2` branch and carries no such factor: the
parser squares its metres argument into `+0x14` (independently confirming the reader-270 ↔
compiled-72900 relation in the format doc's condition table) and the evaluator compares
`dist² <= m²`, which is what CSVM implements. No gate radius is scaled.

Two details of the `0x2` branch, decoded 2026-08-15 (`BL-313`). The distance is measured from the
animation instance's **anchor** node at `+0x6c` (`004ec108`); the condition's own node-index field
at `+0x10` is ignored here, unlike the `NODE_*` branches. And the test is one-shot: it runs when the
stepper dispatches the `IF`/`ELSEIF`, not per frame, and re-runs only if a `LOOP` rewinds the cursor
back over it. ⚠ `FUN_004ec540` returns **0.0** when the node lookup fails, so a failed lookup reads
as "in range" rather than out of it.

**`RANDOM_WEIGHT` reads a 200-entry ring, and that ring is not reproducible.** Each evaluation reads
`table[DAT_0072836c]` and advances that index modulo 200, with the index global across every
definition in the world. The table at `DAT_009fce20` is not compiled data: `FUN_004ee380` fills it at
anim-system init with 200 calls to `rand() * 3.051851e-05` (1/32767, MSVC's `RAND_MAX`), and the
stream it draws from is reseeded `srand(time(NULL))` on ordinary startup and level-load paths, so
two runs of the original produce two different tables. There is no fixed sequence to match, and
CSVM's session-seeded `_rng` is the correct-shape answer rather than a divergence. Disproven in
full, with the two `srand` call sites, in `analysis/anim-interpreter-decode/FINDINGS.md`; the
comparison sense that came out of it, `draw <= weight` inclusive at both ends, is what CSVM runs.

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

## The tick walk is ascending, and that is what decides same-tick dispatch

A running definition advances its sequences in **one forward pass over the array**, not over a list
of live runners. The walk reads the base from `anim+0xcc` and the count from the byte at `anim+0xd8`,
and steps by the `0x40` record stride (`004ecedc`–`004ecf53`, increment at `004ecf4d`). Each
iteration re-reads that slot's state byte at `seq+0x20` and calls the stepper only when the state is
**0 or 1**; parked (3) and done (2) are skipped. The global tick delta `DAT_009fd1a8` is reloaded
from `DAT_009ad744` at the top of **every** iteration, so each slot the pass reaches is advanced by
the full frame delta.

Two properties follow from the pass being a forward index walk, and both matter more than they look.

**A called sequence runs in the same tick if and only if its index is higher than the caller's.**
`CALL_SEQUENCE` writes state 0 into the callee's own slot (`004eb5fa`–`004eb605`, addressing it as
`base + index*0x40`), and the walk has either passed that index already or has not. A call
*forward* in the array lands on a slot the cursor has yet to reach, so the callee's first event
fires in the same tick as the call. A call *backward*, or a sequence calling itself, lands behind
the cursor and waits for the next tick. The index is the sequence's position in the definition's own
array, which is authored declaration order, so the authoring decides the timing.

The data authors overwhelmingly call forward. Of the 22,173 compiled CALL edges the walk sees,
**22,057 (99.48 %) target a higher index** and so dispatch in the same tick; 116 target a lower one
and defer. 398 of the 403 definitions carrying a call have at least one forward edge, and exactly
one is backward-only (census: `analysis/bl-135-callsequence-lag/call-index-order.ps1`). Among the
backward minority are `police_car`'s `start_walkin` → `siren_police`, which the original really does
start a tick late, and the return hop of C2/M02's `marypickford` ring, which is what stops that ring
resolving inside one tick.

**No sequence is stepped twice in one pass, so the same-tick chain needs no cap.** A chain of
forward calls is bounded by the array count, and a ring cannot spin at all: whichever way round it
goes, one of its hops necessarily targets an index the cursor has passed. The engine gets this for
free from the walk's shape rather than from a guard.

⚠ The name resolution is **memoized into the event**, not recomputed. `CALL_SEQUENCE` scans the
array comparing the record's name at `seq+0` and stores the resulting index in the event payload at
`+0x2c` (`004eb5db`), reusing it on every later dispatch of that same event. `STOP_SEQUENCE`
(`004eb610`) resolves identically. The memo is per authored event, so it is stable for the life of
the mission.

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

The stepper needs each dispatched event's run time to place the next one. Two handlers traced
establish the pattern:

- `LIGHT_ANIMATION` (`004e82b0`, dispatch slot 5) advances the light by one tick's worth of the
  authored delta per dispatch, clamps the last tick to the remainder, and returns **still running**
  until the run time is up.
- `FBFX_COLOR_FROM_TO` (`FUN_004ec6a0`, dispatch slot 36) interpolates RGBA linearly from `from`
  toward `to` over the run time, clamps each channel to 0..1, packs RGB into one frame-buffer pixel
  value and hands it plus the alpha — **separately, as a blend weight, not a premultiplied
  component** — to the single global frame-buffer-effect object. It returns still running on the
  same test. Full decode below.

The consequence for the sequence is the same in both cases: **the ramp is the event's duration and
the chain waits for it.** Reporting 0 collapses `he_light_seq`'s authored 0.41 s flicker — seven
ramps — into a single frame, each tween overwriting the previous, and `he_ground_effect`'s six-step
1.2 s white↔violet wash into one instant.

## FBFX_COLOR_FROM_TO is a full-screen wash

`FUN_004ec6a0`, dispatch slot 36, read in full (decompiled and disassembled). **152 events ship**,
in four definitions and nowhere else: `he_ground_effect`, `ap_ground_effect` and `flak_effect`
(6 events each × 8 chapters = 144) plus the intro cutscene's `gi_scene1` (1 × 8). Each of the three
ordnance definitions carries the same shape — a sibling `Initial` sequence doing
`If PlayerRange … → CallSequence frame_buffer_effects1 → Endif`, and an `OnCall`
`frame_buffer_effects1` holding the chain.

**The event struct**, from the handler's own offsets (`ECX` is the event, `EDX` the live sequence):

| Offset | Field |
|---|---|
| `+0x0c` / `+0x10` / `+0x14` | red `from` / `to` / **delta** |
| `+0x18` / `+0x1c` / `+0x20` | green `from` / `to` / delta |
| `+0x24` / `+0x28` / `+0x2c` | blue `from` / `to` / delta |
| `+0x30` / `+0x34` / `+0x38` | alpha `from` / `to` / delta |
| `+0x3c` | `run_time` |

**What it does, per tick.** With `t` = the sequence's **event timer** (`seq+0x28`) clamped to
`run_time`, each channel is `from + t * delta` — the delta is the compiled
`(to - from) / run_time`, so the interpolation is **linear in RGBA**. Once the event timer reaches
`run_time` the value snaps to `to` outright and the handler returns **2** (complete); before that it
returns **1** (still running, re-dispatch me next tick). Each channel is then clamped to `0…1`; RGB
is scaled by 255 and packed into one frame-buffer pixel through the same mask/shift globals
(`DAT_009c67fc`/`6800`/`6804`/`680c`) the weather particles' `COLOR` uses, and the alpha is passed
**separately, as a scalar** — not premultiplied into the pixel. Colour plus a scalar weight is an
alpha blend over the picture (`dst = dst·(1-a) + colour·a`); it cannot be a multiply or a screen,
and the data agrees — `he_ground_effect`'s first step is white at α 0.3, which a multiply would
render invisible. *(The blend state itself sits behind a virtual on the renderable and was not
traced; the colour+weight pair and the white-step argument are the evidence.)*

**One global state, last writer wins.** The pair goes to a single process-wide object
(`FUN_005ca1f0` → `FUN_005ca0e0`, `this` hard-coded to `0x9c8a98`): packed colour at `+0x60`,
alpha at `+0x68`, then a virtual call that arms the effect **for the current frame only**
(`FUN_005c54a0` — sets the live bit and clears the persistent one). Nothing else in the exe writes
those fields. So two bursts overlapping do not composite: the second simply overwrites the first,
and when the last event completes nothing re-arms the object and the wash is gone on the next frame
rather than holding its `to` colour.

**`alpha_delta` in the extraction is a round-trip artefact, not a parameter.** mech3ax recomputes
every delta as `(to - from) / run_time` and emits `alpha_delta` only when the file's stored value
disagrees bit-for-bit. All 8 non-null values in this install are `flak_effect`'s `-0.99999994`
against a computed `-1.0` — one ulp, about 2e-8 of alpha across the whole 0.3 s ramp, orders below
one 8-bit level. CSVM does not read it.

**How CSVM plays it.** `AnimRuntime`'s `FbfxColorFromTo` case pushes `(from, to, run_time)` to a
session-level sink and reports `run_time` as the event's **duration**, which is the CSVM equivalent
of the original's "return 1 until done": the sequence runner gates the next event on it, so
`he_ground_effect`'s six steps space out over their authored 1.2 s instead of collapsing into one
instant. The sink is `UI.ScreenFlash` (`docs/architecture.md`) — one ramp at a time, replaced
outright by a later event, painted into every rendered view. Asserted by the `fbfx-flash`
`--run-tests` suite.

## The last four unhandled kinds — decoded and triaged, none built

`Callback` (slot 35, `004ec5e0`), `ObjectCycleTexture` (slot 17, `004eabd0`), `ObjectDeleteChild`
(slot 16, `004eab90`) and `CameraState` (slot 20, `004e85c0`) are the whole of what the census in
`analysis/anim-interpreter-decode/FINDINGS.md` counts as shipped-but-unhandled. All four were
missing from Ghidra's function list (reached only through the dispatch table, like `LOOP`) and were
recovered by forcing a function at each dispatch address, then decompiled in full. None gets a
handler: for each, either CSVM has no consumer of what the exe does, or the def(s) that carry it are
never reached by anything CSVM plays. A def-census over all 3,015 compiled defs (ad hoc, same method
as `anim_census.py`) located every occurrence of all four kinds to check reachability, not just count
them.

- **`Callback`** (288 events, 120 defs — e.g. `player-player.json`'s `destroy_craft` sequence,
  `value: 15`/`16`). The handler calls the anim instance's own registered native function pointer
  (`anim+0x74`) with a per-event code (`anim+0x78`, the event's own `+0xc`) if one is registered —
  pure `has_callbacks`-gated mission-scripting plumbing, notifying a host that installed a callback.
  CSVM's `AnimRuntime` never installs one; there is no consumer to notify.
- **`ObjectCycleTexture`** (96 events, 96 defs — exactly the player's own
  `<part>_damage_{green,yellow,red}` cockpit indicator lights for `leftwing`/`rightwing`/`nose`/
  `tail`, one set per chapter, nothing else). The handler resets an object's per-mesh texture-cycle
  list to frame 0 (`FUN_005642a0`) then jumps straight to a specific frame (`FUN_00564410`, index
  from the event's `+0x12`) — i.e. "snap this object's cycling texture to state N", not "start a
  cycle". `docs/architecture.md`'s `Flight/DamageVisuals.cs` entry already records these same defs as
  deliberately unwired: CSVM has no first-person cockpit to show the indicator on, and
  `GaugeCluster.OnPartDamage` covers the same information a different way. The decode confirms it is
  the same mechanism, not a second consumer — nothing changes.
- **`ObjectDeleteChild`** (48 events, 40 defs). The handler unconditionally detaches a named child
  from a named parent (`FUN_004cd6d0`, dispatched by the child's own node type) — a pure scene-graph
  reparent, no visibility or transform change of its own. Every shipped use is one of two shapes:
  - `camera1-generic_intro.json`'s `check_warhawk`/`start_script` sequences detach `camera1` and
    `player` from `world1` — cutscene camera rigging, `OnCall` and never reached by anything CSVM
    plays (`camera1`/`player`/`cpilot` are cutscene machinery for cutscenes this project does not
    have), and `apassengers-rem_pas.json`'s
    `remove_passenger` detaches `apassengers` from `pass_st` — `pass_st` is not a gamez node in any
    chapter (confirmed earlier, `docs/HISTORY.md`), so the parent can never resolve even if a handler
    were written.
  - `player-cpeject1/2/cpejectstop.json` detach `cpilot` from `pilot_pos`. These ARE reached — they
    are called from the player's own `destroy_it` crash sequence
    — but in all three files the delete is the first of exactly two events, and the second is
    `ObjectActiveState(cpilot, false)`: `cpilot` is hidden immediately after, whether or not it was
    ever detached. Reached, and still a no-op to build: CSVM already renders the correct (invisible)
    outcome without it.
- **`CameraState`** (8 events, 8 defs — one `player-gi_1stperson.json` each). The handler writes a
  camera node's clip near/far, LOD multiplier, FOV and zoom fields, each gated by its own bit in the
  event's flags byte. `gi_1stperson` is `OnCall` and its only caller anywhere in the install is
  `camera1-generic_intro.json`'s `check_warhawk` sequence — the same unreached intro-cutscene
  machinery as `ObjectDeleteChild` above. CSVM has no scripted first-person camera to configure
  either (`PlayerFirstPerson` reads `false` — no cockpit view — see the format doc's own condition
  table).

`AnimRuntime`'s `default:` case keeps counting all four by name (`Count(ev.Kind)`) exactly as
before — this record is what makes that report legible, not a code change.

## What this record could not confirm

`FUN_004ecbb0` and `FUN_004ebfa0` were read in full and match the state machine and struct offsets
above exactly, including the three `START_TIME` origin codes (1/2/3 → `anim+0xb0` / `seq+0x24` /
`seq+0x28`) and the reset writing `state ← +0x21`, `event ptr ← +0x38`, both timers to 0. `LOOP`
(`004ebfd0`) has no Ghidra-recognised function boundary — it is reached only through the dispatch
table — but was disassembled directly: it folds the sequence timer into `+0x2c`, does
`INC word ptr [+0x30]`, terminates on `counter == authored` (`-1` special-cased infinite) or, in its
second form (`flags & 2`, `LOOP_RUN_TIME`), on the accumulated time reaching the authored float,
then calls the reset routine and returns 4. **The exe has that second form; nothing shipped builds
it** — of 3,015 compiled defs' 1,018 `Loop` events, all 1,018 carry `Count` and none carries
`RunTime`. Disproven, not merely unimplemented: see the census in
`analysis/anim-interpreter-decode/FINDINGS.md`.

The struct offsets above are otherwise mech3ax's `SeqDefInfoC` layout, taken as given rather than
independently re-derived field-by-field; only the offsets the stepper and `LOOP` actually touch have
been seen in code.

`WAIT_FOR_COMPLETION`'s hold is confirmed only at the general handler-return-value contract, not by
reading a dedicated address, which is a weaker kind of confirmation than the rest of this page. The
animation-frame tick keeps a hypothesis the decode cannot rule out (`min(render rate, 60)`) alongside
its confirmation.

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
| **Live runners in an append-ordered list, walked descending**, where the original holds a fixed slot per sequence and walks it ascending | Our `AnimInstance.Runners` has no index to compare, so a `CALL_SEQUENCE` always lands past the cursor and every called sequence starts one tick late, where the original starts it in the same tick whenever the callee is declared after the caller. `BL-135` holds the measurement of what closing this costs |
| **The u16 pass-counter wrap** at 65,536, not reproduced | Unreachable within a session, and `-1` already spells "infinite" |
| **The 86,400 s instance-clock clamp** (`FUN_004ebfd0`), not reproduced | A session never reaches a day |
| **`LIGHT_ANIMATION` ramps asynchronously** rather than one delta-tick per dispatch | Same picture only because the sequence is also held for the run time; the tick-by-tick advance is not reproduced |
| **The animation frame is its own constant**, not the simulation step | They are equal today, but they are independent facts — see the trap above |
