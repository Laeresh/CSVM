# The original's AI control law

How `crimson.exe` turns an AI aircraft's standing order into the three stick channels and the
throttle lever. Decoded 2026-08-15 (plan item `D31`). Companion pages:
[flightModel.md](flightModel.md) (the plant those channels drive, and the player/AI divergences in
it), [../formats/ai-rosters.md](../formats/ai-rosters.md) (the roster and skill data this law reads),
[../formats/vehicle.md](../formats/vehicle.md) (the per-airframe keys).

**Nothing on this page is implemented.** `Flight/AiPilot.cs` still flies the invented placeholder
law; the port is plan item `E41`.

## Address map

| Address | Role |
|---|---|
| `FUN_004897c0` | World tick. Calls the human input handler, then the AI brain per vehicle, then the integrator |
| `FUN_00487460` | The **human** input handler. Called once per frame, on `DAT_0071c298` (the player object) only |
| `FUN_0041c270` | The AI brain root: order dispatch and class dispatch |
| `FUN_0041d9f0` | The **combat** driver (order 1): target pursuit, lay off, the evade checks |
| `FUN_0041d1f0` | The **navigation** driver (orders 0 and 2): patrol nets, danger-zone paths |
| `FUN_0041e760` | The class-4 wingman driver |
| **`FUN_0041b560`** | **The control law.** Aim point plus target velocity in, three stick channels and the throttle lever out |
| `FUN_004209b0` | The maneuver executor (mode 1), which writes the channels itself |
| `FUN_0041c080` | The autogyro steering law (class 1), a different channel set |
| `FUN_00460be0` | The quadratic intercept solver the law aims with |
| `FUN_004200d0` | The stun handler, which zeroes the channels |

## Where in the frame it runs

`FUN_004897c0` walks the vehicle list twice. The AI brain is called from the **second** walk, per
vehicle, immediately before that vehicle's integrator:

```
FUN_004897c0
 ├ FUN_00487460(DAT_0071c298)      the player's stick, once, player object only
 └ for each vehicle:
     ├ FUN_0041f810                mode selection (obj+0x358), when mode < 4
     ├ FUN_0041c270                THE AI BRAIN: writes obj+0x100 / +0x108 / +0x10c / +0x124
     └ FUN_00489ea0                the class dispatch, into FUN_0048e580 and FUN_0048c470
```

Two consequences for the port. The law runs **once per world tick, not once per sim step**, at the
same cadence as the integrator it feeds, so `AiPilot.Next`'s one-call-per-step contract already
matches. And the stick is written **before** the integrator reads it in the same frame, so there is
no one-frame lag to reproduce.

⚠ **The human handler is player-only by construction, so there is nothing to subtract.** It has
exactly one call site and that site passes the global player pointer. The AI never reaches it and
the player never reaches `FUN_0041c270`; the two input paths do not overlap anywhere.

## The channels

The control block is five raw slots followed by five copies at `+0x14`:

| Raw | Copy | Aeroplane meaning | Autogyro meaning |
|---|---|---|---|
| `+0xfc` | `+0x110` | unused | collective |
| `+0x100` | `+0x114` | **roll** | yaw |
| `+0x104` | `+0x118` | unused | elevation |
| `+0x108` | `+0x11c` | **pitch** | unused |
| `+0x10c` | `+0x120` | **yaw** | unused |
| `+0x124` | | **commanded throttle** | |

`FUN_0048c470` consumes the copies (`+0x114` roll, `+0x11c` pitch, `+0x120` yaw), which
[flightModel.md](flightModel.md) already fixed from the player side. The pairing is confirmed
independently by the stun handler, which zeroes exactly `+0x100`/`+0x108`/`+0x10c` **and**
`+0x114`/`+0x11c`/`+0x120`, the aeroplane's three channels raw and copied.

⚠ **The copy is a plain copy. `FUN_00460890` is `FLD [ESP+4]; RET`,** a two-instruction identity
that ignores its other two arguments. Every caller passes `(value, 0.5, 1.0)` as though it were a
smoother; it is not one in the retail build. Do not port a filter here.

## The dispatch tree

`FUN_0041c270`, gated on `obj+0xcc == 0` (not path-following, per
[flightModel.md](flightModel.md)'s scripted-path finding):

```
if obj+0x948 == 0 and obj+0x2f0 == 1:        no target while under a combat order
    obj+0x2f0 = obj+0x2f4                    revert to the default order
    obj+0x300 = clock + obj+0x308            and start its timer
if class in {0,4,1} and obj+0x358 < 3: FUN_0041afd0     target selection
if class == 4:  FUN_0041e760                 wingman
else switch obj+0x2f0:
    0, 2 -> FUN_0041d1f0                     navigation
    1    -> FUN_0041d9f0                     combat
    else -> nothing
```

The three fields `D31` was told to account for:

| Field | What it is |
|---|---|
| `obj+0x948` | The **current target**, a `Target*` (RTTI `TargetVehicle` when it is a vehicle). Not a flag. Null means no target, which reverts the order above and short-circuits the tail of the brain |
| `obj+0x2F0` | The **current order**, selecting the driver: 0 and 2 navigate, 1 fights. `obj+0x2f4` holds the default it reverts to, `obj+0x300`/`+0x308` its timer |
| `obj+0xBA` | The **evade flag**. Set to 1 by the damage handler `FUN_004b9bc0` when the steady-hand test fails ("Absorbed %f damage; steady hand test failed. Evading.", `0x62b1e8`), cleared in `FUN_0041d9f0` (`0x41deaf`) once the pursuer's nose alignment on this aircraft drops below 0.85. While set it suppresses the lay-off branch and the voice callouts |

`obj+0x358` is the mode enum already documented in [flightModel.md](flightModel.md). Both drivers
switch on it identically for a class-0 aeroplane: mode 0 steers, mode 1 hands off to the maneuver
executor `FUN_004209b0`, mode 2 to `FUN_004216e0`, mode 3 (avoid crash) aims at a point 1000 m
directly above the aircraft and runs the law in its emergency form.

## `FUN_0041b560`, the law

Six arguments: the object, an aim point, the aim point's velocity, an eight-float parameter table,
an `emergency` byte and a `gunLead` byte. Everything below is skipped when the aim point coincides
with the aircraft.

**1. Clamp the aim altitude.** Above `obj+0x314` it is pulled down to that ceiling and any upward
component of the aim velocity is zeroed; below the global floor `DAT_0071c3f0` the mirror.

**2. Desired speed.** With `vt` the aim velocity's magnitude, `along` the component of the
separation along it and `cross` the component across it:

```
want = vt + along · params[4] + cross · params[5]        (want = 80.4672 when vt == 0)
```

Then, when `emergency` is clear: `want` is held within 26.8224 m/s (60 mph) of `vt`, capped at
`fd_speed · params[1]`, floored at 22.352 m/s (50 mph), and finally clamped to the pair at
`def+0x1e4`/`def+0x1e8`.

⚠ **That last pair is `0.0` and `111.76 m/s` (250 mph) on every airframe.** No parser token writes
either slot; the def initialiser sets them (`FUN_00478a00`, `0x478d47` and `0x478d52`) and the
`kind_of` copy propagates them unchanged. So **the AI's desired speed is capped at 250 mph
regardless of airframe**, and on the faster fighters that ceiling binds well before
`fd_speed · params[1]` does: a Bloodhawk chasing at `params[1] = 1.3` would ask for 393 mph and
gets 250. Recovered 2026-08-15 while landing `E41`; D31 left it named as unresolved.

When `emergency` is set, `want` is 22.352 m/s, and with the nose at or below the horizon
(`noseY >= 0`) the law **writes the aircraft's own state**: it adds `dt · noseY · 4.0` to the
altitude and, if the vertical velocity is below `-22.352 · noseY`, eases it toward that value at
rate 0.5 and rewrites the velocity vector and speed at `+0x924`…`+0x934`. Both terms lift a diving
aeroplane, which is what makes this a position and velocity cheat during crash recovery rather than
a force. Nose above the horizon instead raises `want` to `22.352 · (1 - noseY)`, more speed for the
climb.

**3. Aim.** `FUN_00460be0` solves the intercept quadratic and returns a unit direction, with bit 1
set when a second root exists. With `gunLead` clear it is solved at `want` against the aim
velocity; with `gunLead` set it is solved at **860.0 m/s** against the velocity *relative* to this
aircraft, which is a firing solution rather than a fly-to solution. No solution falls back to the
straight-line direction. When two roots exist and `DAT_0064ee4c` is clear, the law takes whichever
root has the larger dot product with the current nose, that is the one needing less turning.

**4. Body frame.** The aim direction is rotated by the 3x3 at `obj+0x180` into `bx` (right), `by`
(up), `bz`.

⚠ **`bz` is BACKWARD, and it is positive BEHIND the aircraft.** The third row of that basis
(`obj+0x198`…`obj+0x1a0`) is the backward axis: `FUN_00476250` builds the vehicle's forward vector
by loading those three and negating them (`FLD [ESI+0x198]; FCHS; FSTP [ESI+0x1e0]` at
`0x00476339`), and the escort law's station offsets read the same row the same way. Everything the
law does with `bz` and with `noseY` (`obj+0x19c`, that row's Y, so **positive nose DOWN**) hangs on
this sign, and reading it as "forward" inverts step 6, step 7 and step 8 together. Two consequences
make the sign legible without the disassembly at all: step 8 is a recovery only if it fires nose
UP, and step 2's emergency arm only lifts the aeroplane if it fires nose DOWN.

**5. Throttle** (`obj+0x124`, the commanded lever, which the integrator then rate-limits like the
player's):

```
if crashed or squared distance to the player > 4e6 (2000 m):
    throttle = want / fd_speed                    open loop
else:
    throttle += dt · 0.35 if want > speed else -dt · 0.35
throttle = clamp(throttle, params[0], params[1])
```

**6. Stick.** With `h` the horizontal magnitude of `(bx, by)`, renormalised to 1.0 when the target
is **behind** (`bz > 0`), pitch and yaw start at zero and:

```
if h > def+0x260 (rudder_tol) or |bx| <= |by|:       the ordinary branch
    if by < 0:  bx = -bx  when emergency or h < params[6],  else bx = sign(bx)
    roll = -bx
    if |bx| < params[2]:  pitch = by
else:                                                the small-error branch
    if bx < 0:  by = -by
    roll = by
    if |by| < params[2]:  yaw = -bx
```

⚠ **Read the branch test together with the renormalisation above it, or it reads backwards.**
Because `h` is forced to exactly 1.0 whenever the target is **behind**, `h > rudder_tol` (0.2 by
default) is true for everything astern, so anything behind the aircraft takes the bank branch at
full authority. That is the reversal: throw the error's magnitude away, keep its direction, and
commit the stick to bringing the target round.

An aim point **ahead** keeps its true magnitude, and that is what the second branch is for. `h` is
then the real horizontal aim error, so a genuine turn still clears `rudder_tol` and banks, while an
error under 0.2 with the lateral component dominating falls through to the rudder instead. **This is
the branch a patrolling or pursuing aircraft flying at something in front of it normally sits in**,
and it is where the original's own AI was sampled under a debugger: a small lateral error on the
rudder, a roll command in the thousandths, wings level.

⚠ **`rudder_tol` reads the opposite way round to what its position in the test suggests.** Pinned by
`CSVM.Tests/AiControlLawTests`. Clearing the threshold selects the BANK branch, so a **higher**
`rudder_tol` yields **more** rudder, not less:

| `rudder_tol` | Aim point ahead (`h` = the true error) | Aim point behind (`h` = 1) |
|---|---|---|
| **0.2**, the def default | banks once the horizontal error passes 0.2, roughly 12 degrees off the nose; under that, a lateral-dominant error goes on the rudder | `1 > 0.2`, so always the bank branch |
| **1.0**, authored by `autogyro` and `balmoral` | `1 > 1` is false at the very most, so a lateral-dominant error ALWAYS goes on the RUDDER | `1 > 1` is false, so rudder whenever the lateral error dominates |

So the key is what its name says, a tolerance on how much horizontal aim error justifies banking
rather than ruddering, and the two defs that raise it to 1.0 are **rudder-steered aircraft**. The
`autogyro` is class 1 and never reaches this law, which leaves the **`balmoral` as the one
aeroplane in the game that turns onto a target ahead with rudder instead of bank.**

**7. Wings level.** When `emergency` is clear, both `|bx| < params[2]` and `|by| < params[3]`, and
the aircraft is not near vertical (`|noseY| < 0.9`), the roll command is overwritten with `0.2 ·` a
levelling term read off the right-wing vector's vertical component `obj+0x184`, sign-flipped when
inverted.

⚠ **This is the STRAIGHT-AHEAD case, and the renormalisation is again why.** Whenever the aim point
is behind, `(bx, by)` is scaled to unit length, so at least one of them is at least 0.707 and the
pair can never both sit under 0.06. Both small therefore forces `bz <= 0`: the aim point is in
front, and within about 3 degrees of the nose. What the rule actually says is "I am pointed at the
thing I want, so stop steering and roll the wings level", which is the ordinary end state of
flying a leg. Note the edge case one step earlier, which is the mirror of it: an aim point EXACTLY
astern gives `h == 0` before renormalisation, and the law sets `by = 1`, full elevator, pulling up
and over into the reversal.

**8. Low-speed recovery.** Nose more than 0.5 **above** the horizon (`noseY < -0.5`, the backward
axis half a unit down) and speed under 26.8224 m/s (60 mph) forces pitch to `-1.0` upright or
`+1.0` inverted, pushing the nose down either way up, and the throttle to `params[1]`. Slow and
nose high is a stall, and unloading with full power is the recovery from it.

**9. Scale, clamp, ease off.**

```
roll  *= obj+0x8c4   (emergency: +0x8dc)   then clamp to ±obj+0x8d0  (+0x8e8)
pitch *= obj+0x8c8   (emergency: +0x8e0)   then clamp to ±obj+0x8d4  (+0x8ec)
yaw   *= obj+0x8cc   (emergency: +0x8e4)   then clamp to ±obj+0x8d8  (+0x8f0)
```

Every scale takes `+0.5` and every limit `+0.25` while `DAT_0064ee4c` is set. Then, when
`emergency` is clear, all three are multiplied by `obj+0x974`, itself `+0.08` while `DAT_0064ee4c`
is set and cut to a tenth while `clock < obj+0xb4`. Finally the three are copied to
`+0x114`/`+0x11c`/`+0x120` through the identity stub.

⚠ **No stick channel carries a rate term.** The law reads like a controller that ought to have one,
which is why the absence is recorded here rather than left to be inferred. Every step from the body
frame to `+0x114` is memoryless: the branch, the scale, the clamp and the skill factor all read this
frame's aim error and nothing else. No channel is filtered against its previous value, none reads a
body rate, and no `dt` enters any of the three. The throttle at `obj+0x49` is the one filtered
quantity in the function (`±dt · 0.35`), and the identity stub means the copy out adds no slew
either. So **behind** the aircraft the roll command is a relay on the SIGN of the lateral aim error:
the renormalisation in step 6 writes back into the same `bx` the bank branch outputs, the magnitude
is gone, and the shipped 3.5 scale against a limit of 1.0 saturates anything past about 0.29. That
is deliberate and it is confined to the reversal. **Ahead**, the magnitude survives, and the 3.5
scale is then a proportional gain on a real error rather than a relay on a sign.

⚠ **`BL-387` was this sign read backwards in the port, not a missing damping term.** With the
renormalisation applied to targets in FRONT, a tiny lateral error on a straight leg was blown up to
full scale every frame and the bank sawed to about 90°; with it applied astern, as here, the same
flight commands nothing at all. There is no rate term to go looking for, and the search for one
should not restart here.

### The parameter tables

Four eight-float tables in `.rdata`, read back from the image:

| Table | `[0]` throttle min | `[1]` speed cap and throttle max | `[2]` | `[3]` | `[4]` | `[5]` | `[6]` | Used by |
|---|---|---|---|---|---|---|---|---|
| `0x61fb08` | 0.3 | 1.3 | 0.08 | 0.01 | 0.2 | 0.025 | 0.9 | combat, engaged |
| `0x61fb28` | 0.4 | 1.5 | 0.06 | 0.06 | 0.15 | 0.025 | 0.35 | the class-4 wingman |
| `0x61fb48` | 0.6 | 1.3 | 0.06 | 0.06 | 0.15 | 0.025 | 0.35 | avoid crash (with `emergency` set) |
| `0x61fb68` | 0.8 | 1.1 | 0.06 | 0.06 | 0.15 | 0.025 | 0.35 | patrol, and combat while breaking off |

`[7]` is 0.0 in all four and never read. `[1]` does double duty as the multiple of `fd_speed` that
caps the desired speed and as the throttle ceiling, so a table with `[1] = 1.3` both allows a faster
target chase and a throttle above 1.0.

### `DAT_0064ee4c` and `DAT_0064ee4d`

`DAT_0064ee4c` is set immediately before the combat driver's call to the law and cleared
immediately after, so it is an argument passed through a global rather than a persistent state. It
is set for a **normal engagement** (wider throttle band, boosted gains and limits, and the
second-root selection in step 3 suppressed) and clear while **breaking off**, which instead flies
the patrol table.

`DAT_0064ee4d` is cleared at the top of `FUN_004897c0` and set by whichever AI takes the break-off
branch, which is a condition of entering it. **At most one AI per frame can break off.** That is
the engine-side shape of the `lay off` mode the mode vocabulary already names.

### The pursuit standoff

`FUN_0041d9f0` does not aim at the target itself. It offsets the aim point behind the target along
the target's own facing by a standoff that ramps with the target's speed:

```
standoff = 106.68                                       when speed <= 20.576 m/s
         = 106.68 + (speed - 20.576) · 1.8516719        between
         = 259.08                                       when speed >= 102.880005 m/s
```

That is 350 ft rising to 850 ft over 46 to 230 mph. Inside 400 m, an overshoot test compares
closing speed against 0.8 of own speed and 0.8 of the target's, and either swings the aim point
500 m off to reposition or biases the aim vertically by up to 0.3 to bleed the overshoot.

## The other channel writers

Every write to `+0x100`, `+0x108` and `+0x10c` in the image, by function. The set is closed.

| Function | What it writes, and when |
|---|---|
| `FUN_0041b560` | The law above. All three, plus the throttle |
| `FUN_004209b0` | The **maneuver executor**, mode 1. Steps a program at `obj+0x9a4`…`+0x9a8` (stride 0x20), index `+0x9b0`, step deadline `+0x89c`; drives the three channels toward the step's attitude, or copies the step's own three values verbatim when it carries them. Applies the **same** `+0x8c4`…`+0x8d8` scales and limits and the same `+0x974` factor, so the maneuver library and the law share one output stage. Its throttle is bang-bang toward 80.4672 m/s against the patrol table's 0.8/1.1. Exhausting the program sets mode back to 0 |
| `FUN_0041c080` | The **autogyro** law, class 1. A different channel set (`+0xfc`, `+0x100`, `+0x104`): heading error scaled by `3/pi` and clamped to ±1, so full deflection at 60° of error, plus a `fpatan` elevation angle |
| `FUN_0041d1f0` | The class-3/5 surface-vehicle branch, the same heading-error law written straight into `+0xfc`/`+0x100` |
| `FUN_0041d9f0` | Its class-1 branch, a hover-style law writing `+0xfc`/`+0x100` |
| `FUN_004200d0` | The stun handler. Zeroes all three raw and all three copied |
| `FUN_00487460` | The human handler, player object only (above) |
| `FUN_00492040` | The measurement harness, already documented in [flightModel.md](flightModel.md): it saves, zeroes and restores the stick around its own runs |

`FUN_0041e760` (wingman) and `FUN_004216e0` (danger zone) write no channel themselves; both reach
the stick only through `FUN_0041b560`.

## Where the gains come from

The six per-axis values resolve at spawn, in `FUN_0047c210` (`0x47d1e9`–`0x47d263`), one slot at a
time:

```
if roster slot != -1.0:  runtime = roster slot
else:                    runtime = def slot
```

⚠ **The roster block is in pitch/roll/yaw order and the def and runtime blocks are in
roll/pitch/yaw order.** The spawner is the transposition, and it is explicit in the code: roster
`+0xa8` goes to runtime `+0x8c8` with def fallback `+0x268` (pitch), roster `+0xac` to `+0x8c4`
with fallback `+0x264` (roll), roster `+0xb0` to `+0x8cc` with fallback `+0x26c` (yaw). Both
orderings on [ai-rosters.md](../formats/ai-rosters.md) and in the parser are correct; they are
different files. A port that assumes one order throughout will swap the pitch and roll gains.

| Axis | Roster slot | Def offset | Parser token | Runtime |
|---|---|---|---|---|
| roll scale | 45 (`sclr`) | `+0x264` | `ai_input_scale_roll` | `+0x8c4` |
| pitch scale | 44 (`sclp`) | `+0x268` | `ai_input_scale_pitch` | `+0x8c8` |
| yaw scale | 46 (`scly`) | `+0x26c` | `ai_input_scale_yaw` | `+0x8cc` |
| roll limit | 48 (`limr`) | `+0x270` | `ai_input_limit_roll` | `+0x8d0` |
| pitch limit | 47 (`limp`) | `+0x274` | `ai_input_limit_pitch` | `+0x8d4` |
| yaw limit | 49 (`limy`) | `+0x278` | `ai_input_limit_yaw` | `+0x8d8` |

The emergency set repeats the same six at `+0x18` further on, `+0x8dc`…`+0x8f0`, and `emergency` is
set only by the avoid-crash arm and one of the wingman driver's two calls.

**What the shipped data authors.** Nothing on the roster side: all twelve slots read `-1.0`, on
every block of every mission checked (59 blocks across 6 missions, and `ai-rosters.md`'s own census
puts the whole column at one constant). On the def side, only `ai_input_limit_pitch` (11 defs, 0.79
to 0.91) and one `ai_input_limit_yaw` (0.79, on `firebrand`'s neighbour in the file). Everything
else inherits down the `kind_of` chain, which `FUN_00477b70` copies as one six-slot block
(`0x478942`–`0x47897e`), and terminates at the def initialiser's compiled defaults.

**The compiled defaults**, read from `FUN_00478a00` (`0x478dd6`–`0x478e28`). Recovered 2026-08-15
while landing `E41`; D31 left this open:

| Def slot | Default | Set at |
|---|---|---|
| `+0x260` `rudder_tol` | **0.2** | `0x478dd6` |
| `+0x264`/`+0x268`/`+0x26c` scales, roll/pitch/yaw | **3.5** each | `0x478de0`–`0x478dec` |
| `+0x270`/`+0x274`/`+0x278` limits, roll/pitch/yaw | **1.0** each | `0x478df2`–`0x478dfe` |
| `+0x27c`/`+0x280`/`+0x284` emergency scales | **3.5** each | `0x478e04`–`0x478e10` |
| `+0x288`/`+0x28c`/`+0x290` emergency limits | **1.0** each | `0x478e16`–`0x478e22` |

⚠ **The emergency block's defaults are identical to the normal block's, and nothing in the shipped
data authors any emergency slot.** So on every shipped airframe the `emergency` flag changes *which
behaviours run* inside the law (the 22.352 m/s target, the climb assist, the suppressed wings-level
rule and the suppressed skill multiplier) and **not the gains**. A port may share one gain pair
between the two arms and still be exact against this install.

So the effective per-axis stage on a stock airframe is **scale 3.5, limit 1.0** on all three axes,
with pitch limited to 0.79–0.91 instead of 1.0 on the eleven defs that say so. A scale of 3.5
against a limit of 1.0 means the law saturates its own output for any aim error over about 0.29 in
the body frame: the stage is a **near-bang-bang** one, not a proportional one, for all but small
errors.

## The skill scalar, and how a 1 to 9 rating interpolates

`obj+0x974` is the flat multiplier on all three channels. It is `sixth_sense_factor`, interpolated
at spawn (`0x47d0e7`–`0x47d101`) from the `[value@1, value@9]` pair `ai_skill_parameters` supplies,
alongside `obj+0x970` for `sixth_sense_chance` (`0x47d0c1`–`0x47d0de`), which is the rand() test in
the combat driver's evaded check, and `obj+0x978`, the stun duration the stun handler prints.

⚠ **The interpolation is not what [ai-rosters.md](../formats/ai-rosters.md) assumed.** The engine
computes

```
value = lo + (hi - lo) · rating · 0.11111112        (0x608028, exactly 1/9)
```

so the endpoints sit at rating **0 and 9**, not 1 and 9. A rating of 9 gives `hi` exactly, but a
rating of 1 gives `lo + (hi - lo)/9`, not `lo`. That page's stated working assumption ("linear
interpolation over the 1-9 scale") is the right shape with the wrong origin, and `CSVM`'s
`AiSkills` reader implements the assumption rather than this. Correcting it is `E42`'s business,
not this page's.

This also fixes what `sixth_sense_factor` does, which that page could only describe as "the ease-off
factor applied while being pursued". It is a **flat multiplier on the AI's three stick channels,
applied every frame on the non-emergency path**, not a speed or a throttle term and not conditional
on being pursued.

## What this page does not settle

Named so they are not mistaken for decoded:

- **`obj+0xb4`**, the deadline that cuts the skill scalar to a tenth while the clock is short of it.
- **`obj+0x314`**, the aim-altitude ceiling, and the global floor `DAT_0071c3f0`.
- **`FUN_004216e0`**, the mode-2 danger-zone driver, characterised only as far as "writes no channel
  directly". Danger zones are outside the current plan's scope.
- **`FUN_0041e760`**, the class-4 wingman driver, traced only to its two calls into the law with
  table `0x61fb28`. Mission-layer wingman behaviour is out of scope by the plan's own statement.
