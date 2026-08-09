# The original flight model, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, 2 580 480 bytes, `language x86:LE:32:default`), 2026-08-09. This supersedes
figures reconstructed from video in
[`analysis/video-flight-calibration/FINDINGS.md`](../../analysis/video-flight-calibration/FINDINGS.md)
wherever the two disagree — the executable is the authority, video calibration was a proxy.

Everything below is a description of *behaviour and constants*. No decompiler output is
reproduced; the function addresses are given so any claim can be re-checked at source.

## Provenance

The build still carries the original source path `D:\zipper\gamez\zgamegen\gg_flight.c`, and —
more usefully — a **developer "Dynamics tuner" dialog that shipped in the retail exe**. It writes
a `dynamics.txt` whose header names the entire model:

```
PitchTorque, YawTorque, RollTorque, RecMomentsInertiaX, RecMomentsInertiaY, RecMomentsInertiaZ,
ReturnRate, DampingRate, FakeDynSpeed, ThrustFactor, DragFactor, Weight, RefArea,
TopSpeed, StallSpeed, AccelTime99, AccelTime75, AccelTime50, AccelTime25,
DecelTime25, DecelTime50, DecelTime75, DecelTime99,
Climb90, Climb45, Dive45, Dive90, Turn100, Turn75, Turn50, Turn25, Turn0
```

The first 13 are the authored `dynamics` block ([vehicle.md](../formats/vehicle.md)). The
remaining 18 are **measured by running the real flight model**, not authored — which is why they
never appear in the data files.

| Address | Role |
|---|---|
| `FUN_00445090` | Dynamics tuner dialog — writes `dynamics.txt` |
| `FUN_00492040` | Measurement harness — simulates to find top/stall speed, accel & decel times, climb/dive speeds, turn rates |
| `FUN_00491820` | Per-tick integrator |
| `FUN_00490f70` | Torque accumulation, control limiters, stall flag |
| `FUN_0048fc40` | Force accumulation — thrust, drag, lift, gravity |
| `FUN_0041abd0` | Lift coefficient |
| `FUN_0041ada0` | Drag coefficient (drag polar) |
| `FUN_0041acf0` | Thrust available |
| `FUN_0041aca0` / `FUN_0041ac80` | Atmosphere / dynamic pressure |
| `FUN_00490e10` | Control authority vs speed |
| `FUN_004735b0` | `player.json` global parser (token → constant, with fallbacks) |
| `FUN_00479240` | Per-plane `dynamics` block parser |

## Units and conventions

- Internal state is **metres, seconds, radians**. Display converts with `× 2.2369363` (m/s → mph).
- **Speeds in the data files are authored in MPH** — every speed token is multiplied by `0.44704`
  as it is parsed. Angle tokens are authored in **degrees** (`× 0.01745329251994`).
- Aerodynamic intermediates are computed in **imperial** units: altitude in feet
  (`× 3.28084`), velocity in ft/s, density in slug/ft³, dynamic pressure in lb/ft².
- Forces are carried in weight units. Acceleration is `force × 9.82 / Weight`.
- Body axes: local **X = pitch axis**, **Y = yaw axis**, **Z = roll axis**; the nose points
  along **−Z**. (Consistent with the `collision` probe points in
  [vehicle.md](../formats/vehicle.md), which put the nose at −Z.)

### Gravity is exactly `nom_gravity`

Gravity is applied as a force of `(nom_gravity / 9.82) × Weight`, and the force-to-acceleration
step multiplies by `9.82 / Weight`. The two cancel, so the aircraft falls at **precisely
`nom_gravity`**. The parser's own fallback for `nom_gravity` is `9.82`; this install's
`player.json` sets **20 m/s²**. The arcade 2 g is therefore authored data, not a constant, and it
is the only gravity in the model.

## The integrator

Semi-implicit Euler, in this order each tick (`FUN_00491820`):

1. Compute linear acceleration, angular acceleration and the stall flag (`FUN_00490f70`).
2. Body angular velocity += angular acceleration.
3. Damp angular velocity — an **exponential decay** of `dt × ang_momentum_damp`, not a linear
   subtraction.
4. Transform body rates to world through the orientation matrix, scaling each axis by its
   `rec_moments_inertia` component (the reciprocal inertia — x = pitch, y = yaw, z = roll).
5. Rotate the orientation by `ω · dt`.
6. Velocity += acceleration `· dt`.
7. Position += velocity `· dt`.

The measurement harness pins `dt` to **0.01 s** (100 Hz) while it runs, so all the derived
figures in `dynamics.txt` are 100 Hz results.

⚠ **AI aircraft have a forward-speed floor.** For any aircraft that is not the player, the
velocity component along the nose axis is clamped to at least **4.4704 m/s (10 mph)** after
integration. The player is exempt.

## Atmosphere

`FUN_0041aca0` is a **two-band step function — there is no altitude gradient at all**:

| Band | Density factor | ρ (slug/ft³) | Speed of sound |
|---|---|---|---|
| dense | 0.9544815 | 0.002377 × 0.9544815 = **2.2688e-3** | (0.9884208 + 1) × 558 = **1109.5 ft/s** (338.2 m/s) |
| thin | 0.057048105 | 0.002377 × 0.057048105 = **1.3560e-4** | (0.7348 + 1) × 558 = **968.0 ft/s** (295.1 m/s) |

Then:

```
q     = 0.5 · ρ · V_ft/s²          (dynamic pressure, lb/ft²)
Mach  = V_m/s / (a_ft/s · 0.3048)
```

**The dense band is the operative one.** See the stall arithmetic below: the dense band produces
a correct stall speed (~76 mph) from the code's own fallback aircraft, the thin band produces a
nonsensical 309 mph. The `flight_ceiling` of 2500 m (~8 200 ft) sits far below any plausible
switch altitude.

⚠ **Unresolved:** the altitude threshold that selects the band has **only a read reference and a
static initialiser of `0.0`** in the binary. Taken literally that would select the thin band
everywhere above sea level, which the arithmetic rules out. Either it is written at runtime by a
path static analysis does not attribute, or the comparison sense differs from the obvious reading.
Treat the dense band as universal and this as the one open question in the atmosphere.

## Lift — the wings deliver the demanded G

This is the single most important finding, and it is not recoverable from video. The model is
written **backwards from the acceleration it wants**, not forwards from a force balance.

### Step 1 — the relative wind is faked toward the nose (`liftAOAs`)

Before any force is computed, the model picks what it will treat as the oncoming airflow, keyed on
`cos α` where α is the angle between the nose and the velocity vector:

| Condition | Relative wind used |
|---|---|
| α ≤ `liftAOAs[0]` | the **true velocity vector** |
| α ≥ `liftAOAs[1]` | **speed × nose direction** — i.e. the airflow is pretended to come straight down the nose |
| between | linear blend on `cos α` between the two |

`liftAOAs` is authored in **degrees** (the parser takes its cosine; fallbacks 0.98 / 0.96 ≈
11.5° / 16.3°). So past the upper edge the game **discards the real airflow direction entirely** —
a large arcade assist that makes a hard-manoeuvring aircraft behave as though it has no sideslip
or incidence at all.

⚠ **This blend is player-only.** AI aircraft skip it and *always* use the nose-aligned wind —
AI effectively flies permanently at zero incidence.

### Step 2 — the demanded acceleration

```
demand = lift_accel_rate · (relativeWind − velocity)      [lift_accel_rate is a rate, 1/s]
demand.Y += nom_gravity                                   [world up — so lift also carries weight]
n = |demand projected onto the body X/Y plane| / 9.82      [the demanded load factor, in G]
```

`lift_accel_rate` (fallback **1.2**) is the rate at which the velocity vector is swung onto the
nose. Since the airflow difference is `≈ speed · α`, the load factor works out as
`n ≈ V · α · lift_accel_rate / g` — a centripetal acceleration expressed in G, which is
dimensionally exactly right.

### Step 3 — lift is that demand, clamped

`FUN_0041abd0`:

```
n     = clamp(n, −5, +9)                                  [a −5 G / +9 G limit]
C_L   = clamp( n · Weight / (q · RefArea), −1.8, +1.8 )
C_L   = min( C_L, 0.75 − 0.15 · Mach )                    [the aerodynamic ceiling]

Lift force  = C_L · q · RefArea   (in the demand's direction, in the body X/Y plane)
```

Below the ceiling the middle two lines cancel, and the whole model collapses to:

> **Lift force = clamp(demanded G, −5, +9) × Weight**, delivered along the demanded direction —
> capped by `(0.75 − 0.15 · Mach) × q × RefArea`.

The self-consistency check: gravity is applied as a force of `(nom_gravity / 9.82) × Weight`, and
in straight level flight at α = 0 the demand is exactly `nom_gravity`, giving a lift force of
`(nom_gravity / 9.82) × Weight`. The two cancel **exactly**. Level flight is not a tuned
equilibrium — it is an identity in the algebra.

Consequences for a reimplementation:

- **The demanded G is the control variable.** The wings simply deliver it, up to a hard 9 G and
  the aerodynamic ceiling.
- `RefArea` and the density constant do **not** set the lift slope — they only set the *ceiling*,
  i.e. where the aircraft stalls. Lift below the ceiling is independent of both.
- The `±1.8` clamp on `C_L` binds only when `q · RefArea` is small relative to weight, where the
  compressibility ceiling is already lower. In practice `0.75 − 0.15·Mach` is the operative limit.
- The delivered `C_L` is what feeds the drag polar, so a hard pull pays induced drag ∝ `C_L²`
  automatically. Induced drag is not a separate term in this model — it falls out.

## Drag

`FUN_0041ada0` is a parabolic polar in the already-capped `C_L`:

```
C_D  = 0.73 · (0.12 + 0.8·C_L + 0.5·C_L²)
     = 0.0876 + 0.584·C_L + 0.365·C_L²

Drag = q · RefArea · DragFactor · C_D        (opposing the velocity vector)
```

`DragFactor` is the per-aircraft tuning multiplier from the `dynamics` block. Note the parasite
term `0.0876` is fixed for every aircraft — airframes differ only through `DragFactor` and
`RefArea`.

## Stall

The stall flag is `1 − L(at 9° AOA) / Weight`; the aircraft is stalled once maximum available
lift can no longer carry its weight. The harness locates stall speed by stepping speed upward in
**0.044704 m/s (exactly 0.1 mph)** increments until the flag flips.

Solving the cap against weight gives a closed form:

```
V_stall = sqrt( 2 · Weight / (0.75 · ρ · RefArea) )
```

(the `0.15 · Mach` term is negligible at stall speed).

**Sanity check** with the parser's own fallback aircraft (`veh_weight` 3500, `ref_area` 335):

```
q_stall = 3500 / (0.75 · 335)      = 13.93 lb/ft²
V       = sqrt(2 · 13.93 / 2.2688e-3) = 110.8 ft/s = 33.8 m/s = 75.5 mph
```

A correct stall speed for these aircraft — and it only comes out right in the dense atmosphere
band, which is what settles that question.

`stall_mag` (fallback **0.45**) is a separate quantity, the magnitude of the stall departure; it
is not part of the lift calculation.

## Control authority vs speed

`FUN_00490e10` derives three independent scalars from airspeed alone. Fallback values shown as
authored (MPH):

- **Base ramp** `f` — 0 below `turn_fade_in` (**10**), rising linearly to 1 at `turn_fade_out`
  (**40**), then held at 1.
- **Roll authority** = `f`. **No high-speed fade** — roll never degrades with speed.
- **Pitch authority** = `f`, then faded by `high_speed_pitch_fade`: full until **500**, falling
  linearly to **zero at 600 mph**.
- **Yaw authority** — piecewise, and deliberately **not monotone**:

  | Speed (mph) | Authority |
  |---|---|
  | ≤ `yaw_fade_in` (10) | `yaw_low_speed` = **0.05** |
  | 10 → `yaw_max` (22.5) | ramps 0.05 → **1.0** |
  | 22.5 → `yaw_fade_out` (45) | ramps 1.0 → `yaw_high_speed` = **0.1** |
  | ≥ 45 | **0.1** |

  The rudder is at full authority only in a 22.5–45 mph window and sits at **10 %** for all of
  normal flight. It is a ground-handling and low-speed control, not a flight control.

## Torques and the limiters

Per axis, per tick:

```
Δω_axis = axis_torque · input · dt · authority_axis · rec_moments_inertia_axis
```

using `pitch_torque` / `rudder_torque` / `roll_torque` with the pitch / yaw / roll authority
scalars respectively.

Two further limiters multiply into **pitch and yaw only**, and — importantly — they apply **only
when the commanded torque opposes the current rotation** (the sign test is on the command versus
the existing angular momentum about that axis):

- **AOA limiter** — `(cos AOA − cos maxAOA) / (1 − cos maxAOA)`, reaching **0 at `maxAOA`**.
  The fallback `maxAOA` cosine is **0.85** (≈ 31.8°).
- **G limiter** — above `highGs[0]` (**5 G**) authority falls linearly to **0 at `highGs[1]`
  (9 G)**; mirrored below `lowGs` (**−5 → −9 G**).

The smaller of the two is used. Note that because the limiter gates *opposing* input, it damps
recovery from a departure rather than entry into one.

`return_rate` is a separate centring torque, described below.

## The three arcade terms

None of these has an aerodynamic justification, and all three distort any model fitted from
observed video.

1. **Thrust varies with nose attitude.** Available thrust is scaled by
   `(1 + 0.24 · noseUpComponent)`, and *additionally* by `(1 + 0.13 · noseUpComponent)` when the
   nose is up. Climbing loses thrust and diving gains it — **on top of** the real gravity term,
   not instead of it. Two separate coefficients, one of them one-sided.
2. **Bank-to-yaw coupling — hardcoded, not data-driven.** Constants **0.205** and **0.165** (at
   `0x6289f8` and `0x6289fc`, immediates in the executable, absent from every data file) convert
   bank angle directly into yaw rate, with an extra contribution when inverted. This is the
   coordinated-turn cheat that makes banking turn the aircraft, and it is why turn rate tracks
   bank angle far more tightly than a real force balance would give.
3. **Weathervane centring.** `return_rate` applies a torque along `cross(−nose, v̂)`, pulling the
   nose onto the velocity vector. **Player aircraft only** — AI does not get it.

There is also a **boost state** multiplying thrust by **1.8** and drag by **0.8**.

## Thrust available — partially resolved

`FUN_0041acf0`, with Mach floored at 0.1:

```
T ∝ [ (0.12 − Mach/60) · 0.73 · q_ref ] / ( Mach · pow(…) )
```

evaluated at a reference speed of `(0.84 · Mach + 0.112) · a`. The leading bracket is the
parasite-drag term `0.12 · 0.73` carrying a linear Mach correction, and the division by Mach is
the propeller constant-power form (`T = P / V`).

⚠ **The `pow` operands were not recovered** — the decompiler lost them behind an FPU helper call.
Closing this needs a read of the raw instructions at that call site. Until then the *shape* of
the thrust curve is known but its exponent is not, so absolute top speeds cannot be predicted
from first principles. The measured `TopSpeed` in `dynamics.txt` is the way around this.

## The measurement harness — a validation route

`FUN_00492040` is worth understanding on its own, because it defines what the original considered
its own performance envelope:

- **Stall speed** — step up from rest in 0.1 mph increments until lift carries weight.
- **Accel times** — from stall speed at full throttle, time to 25 / 50 / 75 / 99 % of the span
  between stall and top speed.
- **Decel times** — from top speed at zero throttle, the mirror.
- **Climb / dive speeds** — steady-state speed held at pitch angles of **+90°, +45°, −45°, −90°**
  (the angles appear as the immediates π/2 and π/4).
- **Turn rates** — maximum sustained yaw rate at **100 / 75 / 50 / 25 %** of top speed and at
  stall speed, reported in deg/s.

⚠ **The tuner shipped in the retail executable.** If the dialog can be reached, it dumps all 18
measured figures per aircraft as ground truth — which would validate a reimplementation in one
pass rather than by flying it. Whether it is reachable in a retail build is untested; the entry
point is gated on several flags.

## What this changes for the remake

Checked against [`src/Flight/FlightModel.cs`](../../CSVM/src/Flight/FlightModel.cs) and
[`src/Flight/PlaneStats.cs`](../../CSVM/src/Flight/PlaneStats.cs), the items most likely to differ:

1. **Lift is the demanded G, delivered.** The remake's `liftFrac` cancels a *share* of gravity's
   cross-path component; the original builds a demand vector, clamps it to ±5/9 G and applies it.
   The remake's `AlignRate` (TUNE, 4/s) is the same quantity as the original's authored
   `lift_accel_rate` (fallback 1.2/s) — it should be read from `player.json`, not tuned.
2. **`liftAOAs` is an airflow blend, not a load-factor ramp.** The remake reads `[5, 9]` as the
   edges of a G ramp; the original uses them as the window over which the relative wind is faked
   toward the nose. Same numbers, different mechanism. This also **settles `BL-095` for these
   keys**: the parser takes their cosine, so `liftAOAs` and `maxAOA` are confirmed **degrees**,
   while `highGs`/`lowGs` are stored raw and are plain **G**.
3. **The two hardcoded bank-to-yaw constants (0.205, 0.165).** These are in no data file, so no
   amount of data extraction would have surfaced them. The remake has no bank-to-turn coupling at
   all — it turns purely by the flight path chasing the nose.
4. **The attitude-dependent thrust (0.24 / 0.13).** A video fit would absorb these into gravity
   or drag and then fail in the opposite manoeuvre.
5. **Rudder at 10 % authority in flight**, full only between 22.5 and 45 mph.
6. **Pitch authority reaching zero at 600 mph**, roll never fading.
7. **Stall speed is per-airframe** — `sqrt(2W / (0.75 ρ S))` — not a fixed fraction of `fd_speed`.
8. **`return_rate` is a weathervane torque** toward the velocity vector, not extra axis damping on
   a released stick.
9. **The G/AOA limiters gating only opposing input** — a subtle asymmetry that changes departure
   and recovery behaviour, not steady turns.

## Confidence

- **Read directly from the executable:** all constants and formulas above, except as marked.
- **Inferred, and marked ⚠ in place:** the atmosphere band threshold; the thrust `pow` exponent.
- **Verified by arithmetic:** the dense atmosphere band, via the stall-speed check against the
  parser's fallback aircraft.
- **Untested:** whether the shipped Dynamics tuner is reachable in a retail build.
