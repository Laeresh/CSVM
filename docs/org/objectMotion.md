# `OBJECT_MOTION`, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`) during
[`PLAN-object-motion-decode.md`](../PLAN-object-motion-decode.md). The authored record is
documented separately in [`formats/anim-definitions.md`](../formats/anim-definitions.md) and
[`formats/destructibles.md`](../formats/destructibles.md). Every executable claim below names
the function it came from; claims marked **measured off footage** are observations, not decodes.

Everything here describes behaviour and constants. No decompiler output or C-like transcription is
reproduced.

**Where the other half lives.** CSVM's implementation is
`CSVM/src/Mech3/Anim/MotionRuntime.cs`; its architecture entry carries the engine-side constraints.
This page owns the original runtime decode. The destructibles format page keeps the authored,
destructible's-eye view and points here for execution semantics.

⚠ **The executable decode wins over a footage-derived magnitude fit.** The old `0.65` speed
factor was a judged look; the approximately `0.58` frame-comparison estimate was measured off
footage. Neither contests the executable's decoded launch direction or magnitude.

## Function map

| Address | Role |
|---|---|
| `FUN_004e8fa0` | Per-frame `OBJECT_MOTION` update: launch, acceleration, gravity, contact, bounce and termination |
| `FUN_00508590` | `OBJECT_MOTION` parser; sets the flag word bits while recognising each token |
| `FUN_004e9e30` | Default contact tier; asks the world for the point-column candidates |
| `FUN_004c76e0` | Vertical column query used by the default contact tier |
| `FUN_004c8ec0` | Full geometry-sweep contact tier selected by `DO_INTERSECTIONS` |
| `FUN_0053c6c0` | Azimuth sine/cosine helper used by the range launch |

## The flag word

**Decoded from the executable.** The parser stores the motion flags at `motion+0xc`. The relevant
gravity flags are `GRAVITY` (`0x1`), `GRAVITY COMPLEX` (`0x2000`), `GRAVITY NO_ALTITUDE`
(`0x4000`) and `GRAVITY DO_INTERSECTIONS` (`0x8000`). `COMPLEX` suppresses the ordinary gravity
add and selects the world-down fold; `NO_ALTITUDE` opts out of the default column; and
`DO_INTERSECTIONS` selects the geometry sweep. The sweep takes precedence over the column opt-out.

**Decoded from the executable.** The `GRAVITY` block also accepts `DEFAULT` and `LOCAL <value>`.
They select the gravity number but set no mode bit, so the compiled record cannot distinguish them.
The extractor's three gravity booleans are therefore the three that survive compilation.

**Verified against extracted JSON.** Across the eight chapter corpora, the gravity cross-tab is:

| `complex` | `no_altitude` | `do_intersections` | events |
|---:|---:|---:|---:|
| false | false | false | 1,363 |
| true | false | false | 88 |
| true | false | true | 166 |
| false | true | false | 8 |

The 8 `NO_ALTITUDE` records are `gunshell`, one per chapter. The 1,363 + 88 bodies use the
default column; the 166 sweep bodies are the strict `DO_INTERSECTIONS` subset.

## Launch and acceleration

**Decoded from the executable.** `TRANSLATION_RANGE` is a polar launch: `xz` is azimuth in
degrees, `y` is elevation in degrees, and `initial` is speed in metres per second. Elevation is
linear: the vertical direction component is `elevation / 90`, while the horizontal component is
the L1 remainder `1 − |elevation| / 90`. The direction is consequently not unit length: at 45°
its magnitude is 0.707. Only azimuth is passed through the sine/cosine helper. The bearing assigned
to azimuth zero is an engine choice, not settled by the data.

**Verified against extracted JSON.** The corpus contains 1,217 range launches across 613 distinct
shapes. `xz` spans −170…359°, all but one `y` lies in −90…90°, and `initial` spans −45…95 m/s.
The canonical `m_build03` record has azimuth 35…55°, elevation 60…70°, speed 28…37 m/s, zero
delta, gravity −10 m/s² and `RUN_TIME` 5.0 s.

**Decoded from the executable.** `TRANSLATION` uses the authored vector form unchanged. In both
forms, `delta` contributes a constant acceleration along the launch direction; it is not a speed
ramp divided by `RUN_TIME`. Gravity is folded into the acceleration, and `COMPLEX` converts
world-down gravity into the object's parent frame before that fold.

## Contact

**Decoded from the executable.** A gravity-bearing body normally uses the column tier. The query
looks at the next X/Z and returns candidate altitude-surface records in the vertical column ending
at the next point, within 10 m. Ordinary solid walls and roofs are not candidates. Plain gravity
checks the column on descending parent-frame steps; `COMPLEX` widens admission to every step because
world-down gravity can have positive local Y. `NO_ALTITUDE` suppresses this tier.

**Decoded from the executable.** `DO_INTERSECTIONS` uses the full geometry sweep instead. The sweep
tests the trajectory against the same world database, so a wall or rooftop can be the struck
surface. The struck surface's type selects the bounce branch: the caller maps the relevant surface
types to the default, water or lava branch indices. The extracted install carries no live lava
branch; that is dead authored data.

**Decoded from the executable.** On contact, a moving body's pose is held half of the incoming step
clear of the surface; below the asymmetric 0.1 m/s horizontal / 0.5 m/s vertical thresholds it is
placed exactly on the surface. Velocity itself is **not reflected**: every component keeps its sign
and is multiplied by 0.2. The motion continues only while incoming speed squared is at least
acceleration squared; otherwise it ends exactly on the struck surface.

## Time and termination

**Decoded from the executable.** An authored `RUN_TIME` is a ceiling, not a flight duration. When
the next step would overshoot it, the final step is shortened so the body ends exactly at the
authored time. A body without `RUN_TIME` uses a watchdog: 15 seconds on the column path and 35
seconds on the sweep path. A predicted upward arc can schedule a following sequence event, but it
does not terminate the body.

**Measured off footage.** The earlier `m_build03` symptom was six of nine pieces cut while still
climbing at roughly 67–72% of the authored arc. This observation remains; its old launch-magnitude
and ground-contact explanations do not.

## Retired readings

These are kept so the dead ends are not re-derived:

| Retired reading | Cause of death |
|---|---|
| `translation_range` was a spherical unit-vector launch | The update's linear elevation path is decoded from `FUN_004e8fa0`; the extracted angle bands and canonical JSON agree with it. |
| `delta` was a ramp divided by `RUN_TIME` | The update adds it as constant acceleration along the launch direction. |
| Only `DO_INTERSECTIONS` bodies were collision-tested | `FUN_004e9e30`/`FUN_004c76e0` is the default column tier; `DO_INTERSECTIONS` upgrades it to the sweep. |
| `NO_ALTITUDE` was a second default terrain mode | The parser and update make it an opt-out from the default column. |
| The apex solve was the landing mechanism | The decode uses the contact tiers for landing; the apex is only sequence timing, and watchdogs terminate untimed bodies. |
| `RUN_TIME` was the flight duration | The executable shortens the final step at the ceiling; untimed bodies use the 15 s / 35 s watchdogs. |
| `DebrisTune.LaunchScale = 0.65` was part of the decode | **Measured off footage:** 0.65 was a judged look, while the 0.745–0.81 launch reduction across the canonical band is **decoded from the executable**. The tune is retired. |

**Measured off footage.** `PT-46` (d)'s observation that some pieces pass through or disappear is
kept as a real observation; the mechanism is reattributed to the decoded contact and termination
rules rather than deleted.

## Deliberate CSVM differences

The remake's defensive eight-rebound ceiling and its contact-arming epsilon are implementation
choices, not executable constants. The launch azimuth's world bearing is also intentionally left
as the engine's current choice because the data fixes relative spacing, not compass orientation.
