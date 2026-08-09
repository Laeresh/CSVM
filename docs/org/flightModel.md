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
- Forces are carried in weight units. Acceleration is `force × 9.82 / Weight` (`0x491290`), with
  `Weight` the **raw authored `veh_weight`** — no unit conversion anywhere in the weight chain; see
  "The force scale — settled" below.
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

**The dense band is the operative one for the player, and that is now proven from the bytes, not
just the arithmetic.** The player force path **zeroes the altitude before computing forces**: at
`0x491250`–`0x491284` the caller saves `[obj+0x208]`, stores `0`, calls the force accumulator and
restores it. With the band threshold statically `0.0` and the comparison `alt ≤ threshold →
dense` (`0x41aca4`–`0x41acc4`), an altitude pinned to 0 selects the dense band **always** — the
threshold's unwritten value is irrelevant to player flight because the altitude never reaches the
comparison. The stall arithmetic below independently agrees (the dense band produces a correct
~76 mph stall from the code's own fallback aircraft, the thin band a nonsensical 309 mph).

⚠ **The thin band is reachable on the OTHER call path.** The force accumulator's second call site
(`0x48c883`, inside the AI-side flight function that also reads `fd_speed·throttle` as the AI
target speed) does **not** zero `[obj+0x208]` first — so with the threshold read literally as 0.0,
AI aerodynamics above sea level run on the thin band (ρ 16.7× lower, i.e. near-zero aero forces on
top of AI's nose-aligned wind and 10 mph speed floor). Static reading, not runtime-verified;
recorded for M4's AI flight work, and no longer an open question for the player model.

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
- The delivered `C_L` is passed to the drag routine and **never read there** (see Drag). This model
  has no induced drag at all: a pull costs speed only through the lift vector's own tilt.

## Drag — the polar is in MACH, not `C_L`

`FUN_0041ada0` is 33 bytes, no branches, and has exactly **one** call site in the whole `.text`
(`0x48fd76`):

```
0041ada0  fld   dword [esp+4]      ; x  <-- arg1
0041ada4  fmul  dword [0x6032e0]   ; * 0.5
0041adaa  fadd  dword [0x6034b0]   ; + 0.8
0041adb0  fmul  dword [esp+4]      ; * x  <-- arg1 AGAIN (disp8 = 04, not 08)
0041adb4  fadd  dword [0x603490]   ; + 0.12
0041adba  fmul  dword [0x603474]   ; * 0.73
0041adc0  ret
```

```
C_D  = 0.73 · (0.12 + 0.8·M + 0.5·M²)        M = Mach
     = 0.0876 + 0.584·M + 0.365·M²

Drag = q · RefArea · DragFactor · C_D · k    (opposing the velocity vector; k = 0.8 when boosting)
```

⚠ **This corrects an earlier reading of this page that had the same three coefficients as a polar
in `C_L`.** The caller computes `C_L` at `0x48fd3f`, pushes it at `0x48fd74`, and cleans 8 bytes —
so a decompiler emits `Drag(Mach, C_L)` and the polar reading is the natural mistake. But the
pushes are `push eax` (`C_L`) then `push ecx` (Mach), and the **last** push is the lowest address,
so **Mach is `[esp+4]`** and `C_L` is the dead `[esp+8]`. Both `fld`/`fmul` in the function use
`disp8 = 0x04`. The `C_L` argument is a stale prototype, nothing more.

The difference is not cosmetic: under the `C_L` reading drag rises with commanded G and has a
speed-independent floor at fixed load factor; under the correct one drag is **independent of G**
and rises with speed only. Anything calibrated against the `C_L` version was calibrated against a
term the original does not have.

**No induced-drag term exists anywhere in `FUN_0048fc40`.** Every use of the `C_L` slot was
enumerated: the lift-force product, an optional load-factor out-param, and the dead push. There is
no `C_L²`, AOA-keyed or load-factor-keyed contribution to drag.

`DragFactor` is the per-aircraft tuning multiplier from the `dynamics` block. The parasite term
`0.0876` is fixed for every aircraft — airframes differ only through `DragFactor` and `RefArea`.

**Cross-check that settles it independently of the bytes.** With this polar, the thrust curve above
and `ThrustFactor = EnginePower` scaled by `RefArea`, the full-throttle level equilibrium
`EnginePower · T_avail(M) = q · DragFactor · C_D(M)` reproduces each airframe's **authored
`fd_speed`** with nothing fitted:

| Airframe | `fd_speed` | solved | ratio |
|---|---:|---:|---:|
| `pbloodhawk` | 302.0 mph | 300.5 | 0.995 |
| `ppeacemaker` | 290.8 | 290.0 | 0.997 |
| `pfury` | 281.9 | 281.0 | 0.997 |
| `pavenger` | 264.0 | 261.4 | 0.990 |
| `pdevastator` | 252.8 | 251.2 | 0.994 |
| `pbrigand` | 241.6 | 240.0 | 0.993 |
| `pautogyro` | 228.2 | 214.9 | 0.942 |
| `pkestrel` | 217.0 | 215.3 | 0.992 |
| `pfirebrand` | 208.0 | 206.7 | 0.994 |
| `pwarhawk` | 201.3 | 200.9 | 0.998 |
| `pbalmoral` | 176.7 | 125.9 | **0.712** |

Nine of eleven land inside 1 %. The autogyro and the Balmoral are the outliers, and they are the
two airframes that are not conventional fighters. Under the `C_L` reading the same solve is not
even close.

⚠ **The absolute scale against `CAP-05` does not close, and the force path is now verified
byte-complete — see "The force scale — settled" below.** The four zero-thrust decelerations
(0.36/1.11/2.82/3.74 m/s² at `V/fd_speed` = 0.25/0.35/0.46/0.50) come out **≈2–3.6× too strong**
against this curve. The earlier suspicion — a kg/lb mix-up of 2.2046 in the force→acceleration
divisor — is **disproven at source**: `veh_weight` runs raw end to end. The residual stands as a
recorded decode-vs-footage conflict, not as a missing term.

## The force scale — settled (no conversion exists; the conflict is real)

The B13 residual read "everything ≈2× too strong, invisible to the level-equilibrium ratio", and
the suspect was the force→acceleration divisor. **Re-read from the raw bytes, the whole weight
chain is conversion-free**, so there is no missing factor to implement:

| Address | What it shows |
|---|---|
| `0x47ae2d` | parser stores the `veh_weight` token (`0x628008`) to def `+0x130` with a plain `mov` — no FPU op, no 0.44704-style conversion (contrast `drag_fade_speed`, converted two instructions earlier) |
| `0x475c6a` | def `+0x130` → runtime `+0x674`, plain `mov` |
| `0x48ff8e` | gravity force = `nom_gravity / 9.82 × [obj+0x674]` — weight raw |
| `0x41ac13` | lift: `C_L = n · [obj+0x674] / (q·S)` — weight raw |
| `0x48fd63` | the optional delivered-load out-param divides by `[obj+0x674]` raw |
| `0x491290` | force → acceleration: `× 9.82 / [obj+0x674]` — weight raw (`9.82` at `0x6080d4`) |

The only other writers of `+0x674` are the Dynamics tuner dialog's write-backs (`0x443992`,
`0x443a5a`, `0x445146`) — debug paths, not conversions. `[obj+0x934]`, the speed every aerodynamic
term keys on, is `sqrt(vel·vel)` stored by the integrator (`0x491b1b`), i.e. the true airspeed.
So the executable computes **exactly** what `FlightModel.cs` computes, constant for constant, and
the remake's force path needs no change.

**That promotes the CAP-05 residual from "suspected units bug" to a real decode-vs-footage
conflict**, and the conflict is overdetermined — no constant can close it:

- The deficit has clean structure: measured C_D at the four points is the polar **minus a constant
  ≈ 0.112** (deficit ∝ q; −0.1085/−0.1118/−0.1122/−0.1122 across x = 0.25…0.50). Nothing decoded
  produces a constant-C_D forward force: thrust available is flat-ish in Mach there (so residual
  lever or idle thrust is ruled out — the implied lever would have to fall 8.6% → 2% *within one
  clip*), and gravity-payback from a descent would be a constant force, not ∝ V².
- Scaling thrust+drag together (the only scale the level equilibrium tolerates) moves
  `accel-150-290` the wrong way: a ×0.5 that fixes `decel-290-150` (3.48 s → ≈7 s) blows the accel
  row out to ≈6.4 s against the same footage's 3.76 ± 0.40. The footage itself rejects every
  uniform force scale.
- The CAP-05 points are **fit outputs, not raw readings**: `FINDINGS.md` records the (g, C)
  degeneracy of the extraction and the +7° AoA of its descent leg. The one model-free anchor
  (6.24 m/s² lost at 152.6 mph in a +5° climb, engine off) still leaves the polar ≈1.5–1.8× strong
  once full `nom_gravity` is charged to the climb — smaller than the fit table's 2.05×, but real.

**Disposition (2026-08-09):** recorded, not fitted. The polar's coefficients are the binary's; the
zero-thrust footage disagrees below cruise by about half; whichever of the two is wrong, it is not
a constant in this model. The most likely reconciliations — the clip's γ accounting inside the
degenerate fit, or a mechanism outside the per-tick force path — are for a future re-decode of the
clip, not for the flight model.

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

**B15 landing note — the G convention, and a real footage conflict.** `Weight` above is the BARE
authored `veh_weight` — a load factor of exactly 1 G — not `nom_gravity / StandardG ≈ 2.037`, the
load factor level flight itself needs to cancel this install's arcade gravity (B11's identity: a
demand of exactly `nom_gravity` cancels weight at zero incidence, and `nom_gravity` here is
20 m/s² against `StandardG` 9.82). The two conventions differ by
`√(nom_gravity/StandardG) ≈ 1.43×`. This page's own sanity check settles which one is coded: 75.5
and 309 mph (dense/thin bands) both reproduce ONLY under the bare-Weight read; the
nom_gravity-scaled read gives 109/447 mph instead, numbers this page never quotes. So 1 G is what
`FUN_00490f70` actually compares against, not a simplification of it.
Evaluated against a REAL airframe rather than the fallback numbers, this surfaces a residual the
fallback coincidence hides: the Bloodhawk's own data (`veh_weight` 1900, `ref_area` 330) computes a
56.5 mph stall, against the "Stall 0% Thrust no input" clip's measured ~76 mph nose-drop
(`src/Flight/FlightModel.cs`'s stall-threshold comment). The remake's retired `StallSpeedFrac = 0.25`
matched that clip only because 0.25 × the BLOODHAWK's fd_speed (302 mph) happens to sit close to
the FALLBACK aircraft's own stall speed (75.5–76.3 mph) — the wing-loading coincidence this plan
warned not to read as validation, and it does not extend to the Bloodhawk's real numbers. Recorded
as an open decode-vs-footage conflict (`analysis/flight-model-baseline/POST-B14.md`'s B15 section
has the full eleven-airframe table), not resolved by switching G-conventions to fit one clip.

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

## Thrust available — resolved, `pow` operands recovered

`FUN_0041acf0` is `__cdecl float thrustAvail(float mach, Atmos *atm)` — two stack args, result in
`st(0)`, caller cleans (`add esp, 8` at `0x48fcf0`). Mach is floored at **0.1** and the clamp is
written back into the argument slot (`0x41ad02`), so every use below is of the floored value:

```
V_ref = (0.84·M + 0.112) · a                       ; a = atm->a, ft/s
q_ref = 0.5 · ρ · V_ref²                           ; FUN_0041ac80(atm, V_ref)
T     = q_ref · 0.73 · (0.12 − M/60) / ( M · pow(1.33 · atm->k, 1.41 · M) )
```

**The `pow` operands are now recovered from the raw bytes** — the decompiler had lost them behind
`call 0x5f7016`, a one-instruction thunk `jmp dword ptr [0xa20358]` that the import table resolves
to `MSVCRT!_CIpow` (base in `st(1)`, exponent in `st(0)`, pushed in that order at `0x41ad0f` /
`0x41ad18`):

- **base** = `atm->k · 1.33` — a constant per atmosphere state, **1.3146** on the dense band
- **exponent** = `Mach · 1.41`

Every float immediate, all `.rdata`:

| Address | Value | Role |
|---|---|---|
| `0x6034a8` | 0.1 | Mach floor |
| `0x6034a4` | 1.33 | × `atm->k` → `pow` base |
| `0x6034a0` | 1.41 | × Mach → `pow` exponent |
| `0x60349c` | 0.84 | `V_ref` slope |
| `0x603498` | 0.112 | `V_ref` intercept, in Mach |
| `0x603494` | 1/60 | Mach trim on the parasite term |
| `0x603490` | 0.12 | parasite coefficient |
| `0x603474` | 0.73 | the same drag scale the polar uses |

⚠ **The curve RISES with speed — it is not `T = P/V`.** The `/M` is real, but the numerator is `q`
at a reference speed that **tracks the current speed** (`V_ref = (0.84M + 0.112)·a`) rather than
sitting at a fixed design point, so `q_ref ∝ M²` and the net is roughly *linear* in Mach: +35 %
between 150 and 500 mph on the dense band. Read it as "the parasite drag force the airframe would
feel at `V_ref`, divided by Mach". What falls with speed in this model is the thrust **margin**,
because drag grows as `M²` where thrust grows as `M`. Anyone re-deriving this from the `/M` alone
will predict a falling curve and be wrong.

The atmosphere initialiser `FUN_0041aca0(alt_ft, atm)` — called at `0x48fc70` with
`alt_ft = position.y · 3.28084` — is what supplies `atm->k`:

```
if (alt_ft <= *(float*)0x71bb3c) { r = 0.9544815;  k = 0.98842078; }   ; dense
else                             { r = 0.05704810; k = 0.73480000; }   ; thin
atm->a   = (k + 1.0) · 558.0        ; 1109.54 ft/s dense, 968.02 thin
atm->rho = r · 0.002377             ; 2.2688e-3 dense, 1.35603e-4 thin
```

⚠ **This does not reopen the band question.** `0x71bb3c` is BSS with a single read reference (the
`fcomp` itself) and no writer anywhere in `.text`, exactly as recorded under the G/AOA limiters —
so a byte-level reading says "threshold 0, thin band always". The **dense band is established by
arithmetic, not by that flag** (the thin band puts the fallback airframe's stall at 309 mph), and
it is corroborated here: with the dense band's `k`, the decoded thrust curve and the decoded drag
polar put the Bloodhawk's full-throttle level equilibrium at **300.5 mph** against its authored
`fd_speed` of 302.0 and its measured 300.4, with no fitted constant anywhere. The thin band would
miss by a factor of ~4. Do not "fix" the band on the strength of the unwritten flag.

**This also settles the `fd_speed` caveat below, in `fd_speed`'s favour** for the Bloodhawk: with
`ThrustFactor = EnginePower` and thrust scaled by `RefArea`, the level equilibrium lands on
`fd_speed` to 0.2 %. The equilibrium is independent of ρ and `a` entirely — both cancel — and
reduces to `EnginePower/DragFactor`:

```
EnginePower · (0.84M + 0.112)² · (0.12 − M/60) / (M³ · pow(1.33k, 1.41M))
    = DragFactor · (0.12 + 0.8M + 0.5M²)
```

The Balmoral still misses (it solves at ≈ 0.71 × its own `fd_speed`), so the caveat stands for the
rest of the set — but the "`fd_speed` is a pure normalising reference" reading is now the weaker
one.

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

⚠ **A caveat, now largely retired — read the Drag section's solve table first.** With the drag
polar corrected to Mach and the thrust `pow` recovered, the absolute test below *does* close: nine
of eleven airframes solve to within 1 % of their own `fd_speed`. What follows is the state of the
question before those two corrections, kept because the Balmoral (0.71×) and the autogyro (0.94×)
still miss and the residual is unexplained.

The table above is a *rank* test, and it is
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
at `[obj+0x134] -= dt · throttle · 5`, and `FUN_00491820` snaps them together. The current throttle
enters thrust as a **plain multiply** at `0x48fce7`. That is the linear-throttle claim, confirmed
at source in this item rather than inferred.

**The throttle slews at 0.5/s, with no idle floor** (`0x48e652`/`0x48e698`: current ±= `0.5 · dt`
toward commanded, snapping exactly onto it when the step crosses). Cutting from full to zero takes
2 s of tapering thrust; slamming open takes the same. The remake applies the lever instantly —
**decoded, unimplemented**, and the one mechanism that could contaminate the first seconds of any
throttle-step measurement taken from footage (it is far too fast to explain the CAP-05 deficit,
whose implied residual lever would have to persist for ~28 s).

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

**Implemented (C21).** `FlightModel.YawAuthorityAt` is this table, built from A1's already-plumbed
`PlaneStats` fields, applied to yaw only. The refit `YawTune` needed was tiny (1.32 → 1.33) — at the
290 mph cruise the yaw-360 measurement was fit against, the two curves are within a point of each
other (`eff` ≈ 0.44, the new curve ≈ 0.43), which is exactly the "shape was right" finding above.
The two curves diverge sharply away from cruise: at 60 mph (just above stall) `eff` was pinned to
its 1.15 ceiling against the new curve's 0.98; at 336 mph (the model's own achieved terminal-dive
speed) `eff` gave 0.29 against the new curve's 0.32.

**Corrected — the pitch high-speed fade never fires.** Authored at 1000/1001 mph against a maximum
attainable dive speed of ~528 mph, it cannot engage. It is real code on a threshold this game never
reaches.

**Corrected — both the G and AOA limiters are inert.** The lift clamp is a hard ±5/9 G, while
`highGs` begins at 9 G and `lowGs` at −6 G. Neither limiter can engage before lift is already
capped, so no authored configuration in this install reaches them.

**Two of the parsed globals are dead in the executable.** The global `drag_factor` (→ `0x71c44c`,
fallback 3.0) and `drag_fade_speed` (→ `0x71c450`, parsed × 0.44704, fallback 40 mph) are written
by the `player.json` parser at `0x4744c0`/`0x4744f0` and **read by nothing anywhere in the image**
— a full dword-reference scan finds only the parser's own two writes for each. There is no global
drag multiplier and no low-speed drag fade; the per-plane `drag_factor` is the only drag scale.
Checked while hunting the CAP-05 deficit (a fade below ~300 mph would have produced exactly its
shape); the hunt is what proved the keys dead.

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
11. **Drag is a polar in Mach with no induced term.** Any model that makes drag rise with the pull
    is adding a mechanism the original does not have. See Drag above.
12. **The throttle lever slews at 0.5/s with no idle floor** (2 s full-to-idle); the remake applies
    it instantly. Decoded, unimplemented — a feel/transient gap, not a steady-state one, and the
    one mechanism that could contaminate the first seconds of any throttle-step footage.

## Confidence

- **Read directly from the executable:** all constants and formulas above, except as marked. This
  now includes the `ThrustFactor` chain (parser → def `+0x128` → runtime `+0x66c` → force
  assembly), the linear throttle multiply and its 0.5/s slew, the thrust `pow` operands
  (`MSVCRT!_CIpow`, base `1.33·atm->k`, exponent `1.41·M`), the drag polar's variable being
  **Mach** — the last read off the raw bytes rather than out of a decompiler, which is what
  corrected it — the conversion-free weight chain ("The force scale — settled"), and the player
  path's altitude-zeroing that pins the dense band.
- **Inferred, and marked ⚠ in place:** the thin band on the AI call path (static reading of an
  unwritten threshold, not runtime-verified).
- **Recorded conflict, not an open decode question:** the absolute force scale below cruise —
  `CAP-05`'s zero-thrust points read the polar ≈2–3.6× too strong (a constant-ΔC_D deficit), the
  weight chain is byte-verified conversion-free, and the footage's own accel row rejects any
  uniform rescale. See "The force scale — settled".
- **Verified by arithmetic:** the dense atmosphere band, via the stall-speed check against the
  parser's fallback aircraft **and** via the level-equilibrium solve (the thin band misses by ~4×)
  — now also proven from the bytes for the player path; `ThrustFactor` = engine power, via a
  perfect rank correlation with `fd_speed` across all eleven player airframes and an absolute
  solve landing nine of eleven inside 1 %.
- **Untested:** whether the shipped Dynamics tuner is reachable in a retail build.
