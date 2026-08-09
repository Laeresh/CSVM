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

There is also a **boost state**: it **replaces** the throttle multiplier with a flat **1.8** (not a
multiply — `mov [ebp+8], 1.8f` on the boost branch at `0x48fcb6`, where the normal branch loads the
throttle) and multiplies the drag coefficient by **0.8**. So boost is "throttle pinned to 180 %",
and boosting at part throttle is identical to boosting at full throttle.

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

## `ThrustFactor` is the engine's power factor — resolved

The tuner's header lists **13** authored fields but
[vehicle.md](../formats/vehicle.md) documents only **12** `dynamics` keys. The extra one is
`ThrustFactor`, and it is **not** an authored key: it is the `power` column of `engines.json`, for
the row the def's `engine` property selects. Traced end to end.

**1 — the assembly.** In the force accumulator (`FUN_0048fc40`), thrust force is

```
mult   = boost ? 1.8 : throttle                     ; [obj+0x128], boost flag [obj+0x947]
avail  = thrustAvailable(Mach) · mult               ; FUN_0041acf0, call at 0x48fce2
avail *= attitude terms (0.24 / 0.13)               ; the arcade term above
if engine destroyed: avail = 0                      ; flag [obj+0x2dc]
Thrust = ThrustFactor · RefArea · avail             ; 0x48fde1: fld [obj+0x66c] · [obj+0x678]
```

⚠ **Thrust scales with `ref_area`, not with `1/veh_weight`.** That is what makes the thrust/drag
balance dimensionally consistent — drag is `q · RefArea · DragFactor · C_D`, so `RefArea` cancels
out of the equilibrium and the top speed depends only on `ThrustFactor / DragFactor` (and weight
through `C_L`). The remake's `EnginePower × ThrustConst / (VehWeight/1000)` divides by the wrong
quantity; B13 owns the fix.

**2 — the struct slot.** The plane *definition* struct carries the whole `dynamics` block at
`+0x100 … +0x134`, and the runtime flight object mirrors it at `+0x644 … +0x678`:

| Def | Runtime | Tuner column | Parser token |
|---|---|---|---|
| `+0x100` | `+0x644` | `RollTorque` | `roll_torque` |
| `+0x104` | `+0x648` | `PitchTorque` | `pitch_torque` |
| `+0x108` | `+0x64c` | `YawTorque` | `rudder_torque` |
| `+0x10c` | — | — | `level_off_rate` (accepted, **never authored**) |
| `+0x110` | `+0x654` | `ReturnRate` | `return_rate` |
| `+0x114` | `+0x658` | `DampingRate` | `ang_momentum_damp` |
| `+0x118`/`+0x11c`/`+0x120` | `+0x65c`/`+0x660`/`+0x664` | `RecMomentsInertiaX/Y/Z` | `rec_moments_inertia` |
| `+0x124` | `+0x668` | `FakeDynSpeed` | `fd_speed` |
| **`+0x128`** | **`+0x66c`** | **`ThrustFactor`** | **none — written from `engines.json`** |
| `+0x12c` | `+0x670` | `DragFactor` | `drag_factor` |
| `+0x130` | `+0x674` | `Weight` | `veh_weight` |
| `+0x134` | `+0x678` | `RefArea` | `ref_area` |

The def→runtime copy is the straight-line block at `0x475c40`–`0x475c76`, which fixes the mapping
without ambiguity. The tuner (`FUN_00445090`) writes its 13 dialog floats back to `+0x644 … +0x678`
in exactly this order, which is how the header names line up with the slots.

**3 — who writes `+0x128`.** The vehicle-def parser (`FUN_00479240`) handles the def-level `engine`
key at `0x47acc0`: it looks the id (or name) up in the `engines.zrd` table — loaded by `0x449040`
into 0x18-byte records, searched by id at `0x449140` and by name at `0x449160` — reads the record's
`+0x14` float through the accessor `FUN_004861f0` (`fld [ecx+0x14]; ret`), and stores it with
`fstp [def+0x128]` at `0x47ad01`. Record `+0x14` is the third JSON column and defaults to `1.0`
(`0x4490a4`), i.e. **`power`**. The hangar's engine-swap path does the same thing directly to the
live object: `fld [rec+0x14]; fstp [player+0x66c]` at `0x43e97b`. Nothing else ever writes the
slot except an AI spread — `FUN_00477280` jitters both `fd_speed` and `ThrustFactor` by a small
random `1 ± ε` for non-player aircraft.

**4 — no thrust key exists in the data.** The vehicle parser's token table
(`0x627f94`–`0x628020`) contains eleven `dynamics` tokens: `pitch_torque`, `roll_torque`,
`rudder_torque`, `level_off_rate`, `return_rate`, `ang_momentum_damp`, `fd_speed`, `drag_factor`,
`veh_weight`, `ref_area`, `rec_moments_inertia`. There is no thrust token, and the literal
`ThrustFactor` occurs exactly once in the whole 2.5 MB image — inside the tuner's CSV header.
A census of the shipped `vehicle.json` agrees: **24 defs carry a `dynamics` block, every one of
them authors the same ten keys, and none authors an eleventh.** (`level_off_rate` is the mirror
case — a token the parser accepts that the shipped data never uses.) So there is no inheritance
chain to walk: `kind_of` never has to supply a thrust value because no def has one.

**5 — the data agrees, across all eleven airframes.** Independent of the binary: at a common speed
`RefArea` cancels and the level-flight equilibrium is set by `ThrustFactor / (DragFactor · C_D)`.
Ranking the eleven player airframes by that index against their authored `fd_speed` gives a
**Spearman ρ of exactly +1.000** when `ThrustFactor` is the stock engine's power. The two
alternatives fail: a uniform `ThrustFactor` of 1 gives ρ = +0.90, and one proportional to weight
gives ρ = +0.77.

| Airframe | Stock engine (id) | `power` | `drag_factor` | `fd_speed` | index `p/(df·C_D)` | null index |
|---|---|---|---|---|---|---|
| `pbloodhawk` | Bloodhawk Lvl-2 (11) | 0.62 | 0.37 | 302.0 mph | 15.21 | 24.53 |
| `ppeacemaker` | Peacemaker Lvl-2 (14) | 0.58 | 0.38 | 290.8 | 14.07 | 24.26 |
| `pfury` | Fury Lvl-2 (17) | 0.59 | 0.42 | 281.9 | 12.94 | 21.93 |
| `pavenger` | Hellhound Lvl-2 (20) | 0.64 | 0.55 | 264.0 | 10.54 | 16.47 |
| `pdevastator` | Devastator Lvl-2 (23) | 0.65 | 0.62 | 252.8 | 9.59 | 14.76 |
| `pbrigand` | Brigand Lvl-2 (26) | 0.68 | 0.73 | 241.6 | 8.43 | 12.39 |
| `pautogyro` | Hoplite gyro Lvl-2 (38) | 0.35 | 0.50 | 228.2 | 7.78 | **22.22** |
| `pkestrel` | Kestrel Lvl-2 (29) | 0.90 | 1.28 | 217.0 | 6.34 | 7.05 |
| `pfirebrand` | Firebrand Lvl-2 (32) | 1.00 | 1.58 | 208.0 | 5.91 | 5.91 |
| `pwarhawk` | Warhawk Lvl-2 (35) | 1.00 | 1.70 | 201.3 | 5.38 | 5.38 |
| `pbalmoral` | Balmoral bomber Lvl-2 (41) | 0.30 | 1.70 | 176.7 | 1.73 | 5.76 |

The **Hoplite autogyro is the discriminating case**: it has the fourth-lowest `drag_factor` in the
set but the fifth-*lowest* `fd_speed`, so drag alone ranks it third-fastest when it is authored
seventh. Only its engine — 0.35, the second-weakest in the game — puts it where the data says it
belongs. The Balmoral is the second discriminator at the other end.

Note that **every player airframe's stock engine is its Lvl-2 row**, not Lvl-1: ids 11, 14, 17, 20,
23, 26, 29, 32, 35, 38, 41. Solving any constant from a Lvl-1 row inflates it by ~30 %.

⚠ **A caveat this item turned up, owed to B12/B13.** The table above is a *rank* test, and it is
clean. The absolute test is not: assuming `fd_speed` is the full-throttle level equilibrium and
solving `power · T_avail(Mach) = q · DragFactor · C_D` at each airframe's own `fd_speed` should
give eleven samples of one smooth curve, and it does not — a log-log fit leaves ±18 % residuals
across most of the set and the Balmoral misses by **+60 %**. Either the unrecovered `pow` term has
strong curvature, or `fd_speed` is not the equilibrium. The second reading has support: the tuner
calls the field **`FakeDynSpeed`** and *measures* `TopSpeed` separately (if `fd_speed` were the top
speed there would be nothing to measure), and at runtime `fd_speed` is used as a **normalising
reference speed** — `speed/fd_speed` for gauge and effect fractions (`0x4b1e54`), an AI target
speed `fd_speed · throttle` (`0x48c593`), and a speed clamp (`0x46aaf4`) — never as a solved
equilibrium. **Do not treat `fd_speed` as the original's top speed in B12/B13 without settling
this.**

**Bonus, and load-bearing for B13:** `[obj+0x124]` and `[obj+0x128]` are the **commanded** and
**current throttle** — `FUN_0048e585` rate-limits the current toward the commanded and burns fuel
at `[obj+0x134] -= dt · throttle · k`, and `FUN_00491820` snaps them together. The current throttle
enters thrust as a **plain multiply** at `0x48fce7`. That is the linear-throttle claim, confirmed
at source in this item rather than inferred.

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

## ⚠ Authored values vs the executable's fallbacks

**Everything above quotes the fallbacks compiled into `crimson.exe`. This install authors different
numbers, and in four places the difference changes the conclusion.** The authored set is recorded in
`backlog.md` `BL-095`; read it as the operative one, and treat the fallbacks as evidence of intent
only.

| Key | Fallback | **Authored** | Why it matters |
|---|---|---|---|
| `yaw_fade_out` | 45 mph | **400 mph** | The yaw curve is **not** flat across the envelope — see below |
| `yaw_max` | 22.5 mph | **50 mph** | |
| `yaw_low_speed` / `yaw_high_speed` | 0.05 / 0.1 | **0.0625 / 0.17** | |
| `high_speed_pitch_fade` | [500, 600] mph | **[1000, 1001] mph** | The pitch fade is **unreachable** — inert |
| `highGs` / `lowGs` | [5, 9] / [−5, −9] | **[9, 15] / [−6, −9]** | Both limiters sit at or past the lift clamp — inert |
| `lift_accel_rate` | 1.2 | **0.75** | |
| `turn_fade_in` / `_out` | 10 / 40 mph | **10 / 50 mph** | |
| `maxAOA` | 31.8° | **46.0°** | |
| `drag_factor` (global) | 3.0 | **1.5** | |
| `stall_mag` | 0.45 | **1.25** | |

**Corrected — the yaw curve declines, it does not go flat.** With the authored numbers, rudder
authority is 0.0625 up to 10 mph, ramps to **1.0 at 50 mph**, then falls linearly to **0.17 at
400 mph** and holds. At the Bloodhawk's 302 mph cruise that is ≈ **0.40**, and it varies across the
whole flight envelope. The earlier "flat 0.1 above 45 mph" reading was an artefact of the fallbacks.
This substantially rehabilitates the remake's `eff = 1.4 − clamp(v/fd, 0.25, 1.15)`, which is also a
declining function of speed — the *shape* was right, only the curve is wrong.

**Corrected — the pitch high-speed fade never fires.** Authored at 1000/1001 mph against a maximum
attainable dive speed of ~528 mph, it cannot engage. It is real code on a threshold this game never
reaches.

**Corrected — both the G and AOA limiters are inert.** The lift clamp is a hard ±5/9 G, while
`highGs` begins at 9 G and `lowGs` at −6 G. Neither limiter can engage before lift is already
capped, so no authored configuration in this install reaches them.

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
10. **Thrust scales with `ref_area`, not `1/veh_weight`.** The remake divides engine power by
    weight; the original multiplies it by reference area, which is what makes `RefArea` cancel
    against drag. See `ThrustFactor` above.

## Confidence

- **Read directly from the executable:** all constants and formulas above, except as marked. This
  now includes the `ThrustFactor` chain (parser → def `+0x128` → runtime `+0x66c` → force
  assembly) and the linear throttle multiply.
- **Inferred, and marked ⚠ in place:** the atmosphere band threshold; the thrust `pow` exponent;
  whether `fd_speed` is a level-flight equilibrium at all (the evidence now says probably not).
- **Verified by arithmetic:** the dense atmosphere band, via the stall-speed check against the
  parser's fallback aircraft; `ThrustFactor` = engine power, via a perfect rank correlation with
  `fd_speed` across all eleven player airframes.
- **Untested:** whether the shipped Dynamics tuner is reachable in a retail build.
