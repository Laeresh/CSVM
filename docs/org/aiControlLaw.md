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

When `emergency` is set, `want` is 22.352 m/s, and with the nose above the horizon the law
**writes the aircraft's own state**: it adds `dt · noseY · 4.0` to the altitude and, if the vertical
velocity is below `-22.352 · noseY`, eases it toward that value at rate 0.5 and rewrites the
velocity vector and speed at `+0x924`…`+0x934`. Nose below the horizon instead raises `want` to
`22.352 · (1 - noseY)`. This is a position and velocity cheat during crash recovery, not a force.

**3. Aim.** `FUN_00460be0` solves the intercept quadratic and returns a unit direction, with bit 1
set when a second root exists. With `gunLead` clear it is solved at `want` against the aim
velocity; with `gunLead` set it is solved at **860.0 m/s** against the velocity *relative* to this
aircraft, which is a firing solution rather than a fly-to solution. No solution falls back to the
straight-line direction. When two roots exist and `DAT_0064ee4c` is clear, the law takes whichever
root has the larger dot product with the current nose, that is the one needing less turning.

**4. Body frame.** The aim direction is rotated by the 3x3 at `obj+0x180` into `bx` (right), `by`
(up), `bz` (forward).

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
is ahead (`bz > 0`), pitch and yaw start at zero and:

```
if h > def+0x260 (rudder_tol) or |bx| <= |by|:       vertical-dominant
    if by < 0:  bx = -bx  when emergency or h < params[6],  else bx = sign(bx)
    roll = -bx
    if |bx| < params[2]:  pitch = by
else:                                                lateral-dominant
    if bx < 0:  by = -by
    roll = by
    if |by| < params[2]:  yaw = -bx
```

Both branches command **roll** first. The lateral branch is bank-to-turn: it banks so the lift
vector points at the target and lets the plant's own bank coupling do the turning, adding rudder
only once the bank command is small. The vertical branch levels the wings laterally and pulls.

⚠ **`rudder_tol` is inert at the shipped value.** It is authored as `1.0` on the two defs that
carry it, and `h` is at most 1.0 by construction, so `h > rudder_tol` never holds and the branch is
chosen by `|bx|` against `|by|` alone. This is the same class of finding as
[flightModel.md](flightModel.md)'s unreachable `high_speed_pitch_fade`: real code on a threshold
the shipped data never reaches.

**7. Wings level.** When `emergency` is clear, the aim is nearly straight ahead
(`|bx| < params[2]` and `|by| < params[3]`) and the aircraft is not near vertical
(`|noseY| < 0.9`), the roll command is overwritten with `0.2 ·` a levelling term read off the
right-wing vector's vertical component `obj+0x184`, sign-flipped when inverted.

**8. Low-speed recovery.** Nose more than 0.5 below the horizon and speed under 26.8224 m/s
(60 mph) forces pitch to `-1.0` upright or `+1.0` inverted (pull toward level either way) and the
throttle to `params[1]`.

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

**What the shipped data authors.** Nothing on the roster side: all twelve slots read `-1.0`. On the
def side, only `ai_input_limit_pitch` (11 defs, 0.79 to 0.91) and one `ai_input_limit_yaw` (0.79).
Everything else inherits down the `kind_of` chain, which `FUN_00477b70` copies as one six-slot block
(`0x478942`–`0x47897e`). ⚠ **The compiled default the chain terminates at has not been read**, and
`E41` needs it: an unauthored scale of 0 would leave an AI with no stick at all, so the default is
not zero and must be recovered before the port fixes numbers.

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

- **The compiled default of the `ai_input_*` def slots** (above). The next step is reading the def
  constructor's initialisation of `+0x264`…`+0x278`.
- **`def+0x1e4`/`+0x1e8`**, the final speed clamp in step 2. The vehicle def parser writes neither,
  so they arrive from somewhere else in the def's construction.
- **`obj+0xb4`**, the deadline that cuts the skill scalar to a tenth while the clock is short of it.
- **`obj+0x314`**, the aim-altitude ceiling, and the global floor `DAT_0071c3f0`.
- **`FUN_004216e0`**, the mode-2 danger-zone driver, characterised only as far as "writes no channel
  directly". Danger zones are outside the current plan's scope.
- **`FUN_0041e760`**, the class-4 wingman driver, traced only to its two calls into the law with
  table `0x61fb28`. Mission-layer wingman behaviour is out of scope by the plan's own statement.
