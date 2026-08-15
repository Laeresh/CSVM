# The original flight model, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, 2 580 480 bytes, `language x86:LE:32:default`), 2026-08-09, extended 2026-08-14 with
ground blow, the collision impulse, and the live-versus-debug integrator correction. This superseded
the figures reconstructed from video in `analysis/video-flight-calibration/FINDINGS.md`, which was
**deleted 2026-08-14** for that reason; the executable is the authority and video calibration was a
proxy. The scripts that produced it are still in that directory, and the file itself is recoverable
with `git log -p -- analysis/video-flight-calibration/FINDINGS.md`.

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
| `FUN_004897c0` | World tick: advances the clock, iterates the vehicle list |
| `FUN_00489ea0` | Vehicle-class dispatch on `obj+0x67c`; the flyable aeroplane classes are 0 and 4 |
| `FUN_0048e580` | Per-tick integrator |
| `FUN_0048c470` | Torque accumulation, control limiters, stall flag |
| `FUN_0048bdd0` | Control authority vs speed |
| `FUN_0048c220` | Ground blow, the proximity control bias |
| `FUN_0048bf60` | Ground-blow probe: the nose ray, its falloff and its axis |
| `FUN_0048d7f0` | Collision sweep and the `bounce_factor` restitution |
| `FUN_0048fc40` | Force accumulation: thrust, drag, lift, gravity |
| `FUN_0041abd0` | Lift coefficient |
| `FUN_0041ada0` | Drag coefficient (drag polar) |
| `FUN_0041acf0` | Thrust available |
| `FUN_0041aca0` / `FUN_0041ac80` | Atmosphere / dynamic pressure |
| `FUN_004c8ec0` / `FUN_004c8f70` | World segment query: terrain grid walk plus per-cell object test |
| `FUN_005388d0` / `FUN_00538880` | Two-point distance / squared distance |
| `FUN_004735b0` | `player.json` global parser (token → constant, with fallbacks) |
| `FUN_00479240` | Per-plane `dynamics` block parser |

### ⚠ There are two integrators, and this table named the wrong one until 2026-08-14

There is **one** flight model. What exists twice is the integrator, and the second copy is the
offline measurement harness described under "The measurement harness" below. Four of this table's
addresses used to name that copy:

| Debug copy | Live counterpart | What the copy drops |
|---|---|---|
| `FUN_00491820` | `FUN_0048e580` | collision (`FUN_0048d7f0`), impact (`FUN_0048d2c0`), per-part update, view smoothing |
| `FUN_00490f70` | `FUN_0048c470` | **ground blow (`FUN_0048c220`)** and seven other world-facing calls |
| `FUN_00490e10` | `FUN_0048bdd0` | the fifth output, a reverse-authority factor (below) |
| `FUN_00445090` | none | the Dynamics tuner dialog itself, which has **no callers at all** |

`FUN_0048fc40` is genuinely shared, called from `FUN_0048c470` (live) and `FUN_00490f70` (debug),
which is the whole of the connection between the two families. The debug copy reads the same
globals and computes the same curves, so **every aerodynamic constant and formula in this document
stands**; what the copy cannot show is anything it strips. Ground blow and the collision impulse
were invisible from that branch, which is why the 2026-08-09 pass found neither.

The live chain for one frame of the player's aeroplane, each call site verified:

```
FUN_004897c0  clock += dt, iterate the vehicle list
 └ FUN_00489ea0            @0x00489bb6   dispatch; obj+0x67c == 0 for a player plane
    └ FUN_0048e580         @0x00489ef3   integrator
       ├ FUN_0048c470      @0x0048e6da   aero force + control torque
       │  ├ FUN_0048bdd0                 airspeed authority ramp
       │  ├ FUN_0048fc40                 thrust, drag, lift, weight
       │  └ FUN_0048c220   @0x0048cf95   GROUND BLOW
       ├ (orientation write)             @0x0048e880-0x0048e8ae
       ├ FUN_0048d7f0      @0x0048ea17   COLLISION SWEEP + bounce_factor
       └ (position write)                @0x0048ea81
```

The other dispatch classes are not aeroplanes: class 1 (`FUN_0048ffe0`) resolves collision but runs
no aerodynamics and reads no controls, and classes 2, 3 and 5 (`FUN_0048a880`, `FUN_0048b480`) call
neither, being animated movers. The token that names each class in the data files was not found.

⚠ **The rest of this document still cites the debug copy's addresses for the shared physics** (the
integrator ordering, the weathervane at `0x4916fe`-`0x4917f0`, the throttle slew, the limiters).
Those citations are still re-checkable at those addresses and the behaviour they describe is the
behaviour the game runs, because the two copies are the same code over the same globals. They were
left alone deliberately: mapping each one to its live-copy address is a separate mechanical pass,
and inventing the mapping without tracing it would be worse than the inconsistency. When reading an
address out of this document, check which family it belongs to first. **A1 (2026-08-15) corrected
the first of them:** the Atmosphere section's "the player force path zeroes the altitude" was a
debug-copy citation, and with it went the supposed player-versus-AI density divergence.

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

**C24 landing note — the exponential form, implemented, and where it sits.** `FlightModel.Step`
now matches steps 2–3 exactly: `BodyRates += cmd · dt` (the stick, the bank coupling and the
weathervane, all three already summed into `cmd` before this line — C21/C22/C23), **then**
`BodyRates *= exp(−dt · ang_momentum_damp)` on the whole result, THIS TICK'S torque included — not
the explicit-Euler `(cmd − BodyRates·damp)·dt`, which only ever damped the rate carried over from
the previous frame and left each tick's own torque undamped until the next one. The two forms are
the same discretization to first order in `dt` per step (`exp(−x) = 1 − x + O(x²)`), but their
STEADY-STATE fixed points differ by more than that: explicit Euler's fixed point is `cmd/damp`
exactly, independent of `dt`; the exponential-form fixed point is `cmd·dt·k/(1−k)` with
`k = exp(−dt·damp)`, which is `cmd/damp · x/(eˣ−1)` for `x = dt·damp` — smaller by `≈ x/2` at
small `x`. At the remake's own physics tick (`dt = 1/60 s`) and the Bloodhawk's `ang_momentum_damp
= 5`, `x ≈ 0.083` and the predicted shortfall is `≈4.1 %`, which is what the steady-rate table
below shows landing at: `roll-360` 1.98 → 2.07 s (+4.5%), `pitch-rate` 33.54 → 32.41 °/s (−3.4%),
`yaw-360` 28.55 → 29.73 s (+4.1%) — all three inside their asserted tolerance bands, so **no `*Tune`
was refit**. This is the decoded mechanism's own bias at this `dt`, not a sign the form or the
ordering is wrong; a build at the original's own internal tick rate (unknown — see the measurement
harness's 100 Hz, which is not necessarily gameplay's own rate) would show a smaller shortfall
still, by the same formula. A large-`dt` case (`dt·damp = 5` on a released axis) shows the
qualitative point the item is about: the exponential form stays in `(0, 1)`, strictly decaying,
where the explicit-Euler factor `(1 − dt·damp) = −4` would flip the rate's sign and grow it every
tick — `CSVM.Tests/AngularDampingTests.cs` pins both the ordering (this tick's own torque is
damped, not exempted) and this divergence.

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

**The dense band is the operative one, for every aircraft, and it rests on the arithmetic rather
than on the bytes.** The comparison is `alt_ft ≤ threshold → dense` (`0x41aca4`–`0x41acc4`) against
the threshold at `0x0071bb3c`, whose shipped value is `0.0`, so a literal byte reading hands the
thin band to anything above sea level. The arithmetic rejects that reading and always has: the
dense band produces a correct ~76 mph stall from the code's own fallback aircraft where the thin
band gives a nonsensical 309 mph, and the level-equilibrium solve reproduces nine of eleven
airframes' authored `fd_speed` under the dense band while the thin band misses by ~4× (see "Drag"
and the thrust curve's note at `0x71bb3c`). Do not "fix" the band on the strength of the unwritten
threshold.

**A1 (2026-08-15): there is no player-versus-AI density divergence, and the earlier claim that the
player path pins the dense band was a debug-copy citation.** This paragraph replaces a ⚠ that read
"AI aerodynamics above sea level run on the thin band".

- The save / store-`0` / restore of `[obj+0x208]` at `0x491250`–`0x491284` is inside
  `FUN_00490f70`, which is the **debug copy** of the force function (see "There are two
  integrators"). Its only caller is `FUN_00491820` (`0x49184c`), whose only callers are
  `FUN_00492040`, `FUN_00491c60` and `FUN_00491d90`; the latter two are called only by
  `FUN_00492040`, and `FUN_00492040` returns immediately unless `DAT_0071c78a` is set. That is the
  Dynamics-tuner display flag, written only by the two dialog handlers `FUN_00443750` and
  `FUN_00445090`, and `0` in the shipped image. The zeroing is the measurement harness pinning
  itself to sea level for its report. **It never runs during flight, for the player or anyone
  else.**
- The live force path is shared: `FUN_004897c0` → `FUN_00489ea0` → `FUN_0048e580` →
  `FUN_0048c470` → `FUN_0048fc40` (`0x48c883`) → `FUN_0041aca0` (`0x48fc70`), and
  `FUN_0048fc40` passes `[obj+0x208] · 3.28084` (`0x48fc51`) with no zeroing anywhere in
  `FUN_0048c470` or `FUN_0048e580` (a program-wide instruction sweep for `+0x208` accesses finds
  none in either function). The player reaches this path exactly as an AI aircraft does
  (`FUN_004897c0` dispatches the player object through `FUN_00489ea0` like every other vehicle),
  and the class dispatch is on the vehicle class `obj+0x67c`, never on the player pointer. So
  whatever band the atmosphere selects, the player and the AI select the same one.
- `[obj+0x208]` is world altitude in metres, positive up: gravity is subtracted from world
  component 1 (`FUN_0048fc40`), the respawn writes `900.0` there or the terrain height when that is
  higher (`FUN_004969b0`, `0x496b75`), and `FUN_004704b0` pushes the object to terrain + 1 m when it
  is below the terrain.

**The write-xref sweep on the band threshold `DAT_0071bb3c`, stated in full so the search is
visible.** Cross-references to `0x0071bb3c`: **one**, the read at `0x41aca4` (`FCOMP`). Writes:
**none, the empty case.** A byte search for the little-endian address `3c bb 71 00` across the
whole image returns exactly one hit, `0x41aca6`, the displacement inside that same instruction, so
no pointer table and no parser store can name it, and the search demonstrably
covers `.data` (the same search for `dynamics` finds the parser token at `0x627f88`). Its
neighbours are ordinary separate globals reached by absolute address, not a struct some base
pointer could walk into: `0x71bb40` is written directly at `0x47f3e2`, and `0x71bb44`–`0x71bb60`
are read by `FUN_0047f1f0` with no writer at all. `0x0071bb3c` is therefore `0.0f` for the whole life of the process, and this question
cannot be reopened by a further static pass, only by reading the live process.

⚠ **The literal reading and the arithmetic still disagree, and that conflict is now a
shared-path question, not an AI one.** Taken at face value the live path gives every airborne
aircraft the thin band, which no airframe could fly (the authored `ref_area`/`veh_weight` pairs sit
at ~10 lb/ft² wing loading, tuned for the dense band; under the thin band the aerodynamic ceiling
`(0.75 − 0.15·M)·q·RefArea` cannot deliver 1 G at any speed those airframes reach). The remake runs
the dense band for both, which is what the arithmetic supports; what would settle the residue is a
live read of `0x0071bb3c` and of `[obj+0x208]` in a running process, not another static sweep.

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

## The two stall cues — a lamp and a nose-drop, on two unrelated thresholds

Both cues are measured on the same margin (`Speed / fd_speed`), and their thresholds are
**deliberately different numbers**, neither of them a tuning constant:

- **The nose-drop** fires below the airframe's own computed stall speed — the decoded
  `clMax · q · RefArea = Weight` solve above (B15). **Decoded.**
- **The STALL lamp** lights at a fixed **0.30 × `fd_speed`** — 0.2989–0.2996 across four
  original-game clips. **Measured off footage**, and deliberately *not* re-derived from the computed
  stall speed: the split is unrelated to the nose-drop mechanism, so the lamp stays a fraction.

In the original's "Stall 0% Thrust no input" clip the lamp led the Bloodhawk's own break by
**2.64 sim s / 14.9 mph**. `CSVM.Tests/StallWarningTests.cs` asserts the ORDERING (warn leads
stall) and the MECHANISM (two independent thresholds on one margin), **not** that absolute lead —
the Bloodhawk's computed 56.5 mph stall does not reproduce the clip's ~76 mph nose-drop, and that
is the recorded decode-vs-footage conflict above rather than something a test papers over.

⚠ **Do not fold the two thresholds together**, and do not move the lamp onto the computed stall
speed to tidy the split away — they are unrelated by measurement, not by oversight.
⚠ **Do not exercise the split on the executable's fallback aircraft.** The fallback airframe's own
computed stall (75.5 mph, i.e. ≈0.30 of its own reference speed) sits almost exactly AT the warn
threshold, which *inverts* the split instead of testing it. The tests fly the Bloodhawk's real
1900 / 330.

**The lamp's blink is a rate ramp, measured (`CAP-06`).** Half-period **643 ms at 0.30 fd** and
**296 ms at 0.15 fd**, shortening monotonically with stall depth and **held** below 0.15 rather
than extrapolated into a strobe. Every figure is in **sim** seconds: the wall→sim conversion is
**k = 1.390**, so one original game frame (33.37 ms wall) is **46.4 ms sim** — the resolution the
lamp was measured at, and therefore the tolerance every period assertion gets.
⚠ **A wall-clock implementation lands ≈39 % short of every dwell.** Same conversion and the same
trap as the chase camera's throttle transient ([camparam.md](../formats/camparam.md)).

**The nose-drop's own rate is a remake TUNE, not a decode.** `StallNoseRate` (1 rad/s toward
world-down at full stall depth, scaled by the authored `stall_mag`) is chosen so the deep-stall
rate exceeds full-elevator authority (~0.58 rad/s steady) and the drop is decisive until airspeed
recovers; while stalled the nose additionally cannot be raised over the horizon at any bank
(user-observed behaviour of the original). Nothing in the executable has been traced to either.

## Control authority vs speed

`FUN_0048bdd0` derives three independent scalars from airspeed alone, reading nine globals at
`0x0071c3f8`-`0x0071c418`. (The debug copy `FUN_00490e10`, which this section used to name, reads
the same nine and computes the same curves.) Fallback values shown as authored (MPH):

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

- **Reverse-authority factor** (decoded 2026-08-14, UNIMPLEMENTED and unowned). `FUN_0048bdd0` has a
  fifth output the debug copy lacks: above `yaw_max` (`0x0071c414`, authored **50 mph**) it returns
  `max(yawAuthority, 0.2)`, and **1.0** at or below it. `FUN_0048c470` uses it to soften control
  forces that oppose the current velocity vector. Because `yaw_max` is the speed at which the yaw
  curve peaks, this engages across the whole of normal flight: it tracks the declining yaw curve
  from 1.0 at 50 mph down to the **0.2** floor, which the curve reaches at ≈345 mph. At the
  Bloodhawk's 302 mph cruise it is ≈0.40. Nothing in `FlightModel` carries it.
  ⚠ Which axes it reaches, and what "opposing the velocity vector" is tested against, were not
  traced. Do not implement it from this paragraph without reading `FUN_0048c470`'s use of the fifth
  output.

### The low-speed ramp is decoded and UNIMPLEMENTED (`BL-330`)

The base ramp `f` is the one part of `FUN_0048bdd0` the remake does not carry: `FlightModel.Step`
applies the authored yaw curve and **no speed term at all** to pitch or roll, so both hold full
authority down to zero airspeed. The original fades them to **0 at `turn_fade_in`** (10 mph),
reaching full only at `turn_fade_out` (fallback 40, **authored 50**). Traced, corroborated from the
controls, and open as `BL-330`.

⚠ **"Roll never fades" above means never with HIGH speed.** Read as "roll authority is
speed-independent" it becomes the misreading that had `turn_fade_in`/`turn_fade_out` filed as a
candidate *bank* effect through four consecutive items — see the CORRECTION at the end of
"Bank-independent lift vs the measured knife-edge sag". The ramp is keyed on airspeed alone and is
saturated across the whole regime in which that 1.6× gap appears.

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

⚠ **`rec_moments_inertia` is applied downstream, not here.** `FUN_00490f70` accumulates a plain
`torque · input · dt · authority` world-frame vector; the integrator then rotates it into body
axes, scales each by `rec_moments_inertia` (`0x4918b9`–`0x491932`) and rotates it back. The
formula above folds the two steps together, which is exact — but it matters for anything else
that enters the same accumulator, because that too is scaled by the reciprocal inertia and damped
by `ang_momentum_damp`. The bank coupling below is exactly such a term.

## Bank coupling — resolved, including the inverted case

`FUN_00490f70` adds two more contributions to the same angular accumulator the stick commands
feed, after the three torque blocks and before the player-only weathervane (`0x491621` and
`0x49167b`). Both key off the **orientation matrix at `+0x180`**, whose rows are the body axes in
world coordinates — row 0 = starboard (pitch axis), row 1 = the aircraft's own up (yaw axis),
row 2 = −nose (roll axis) — and both are multiplied by `dt` (`0x71c56c`), so each is an angular
**rate** contribution, i.e. an acceleration once the `dt` is divided back out:

```
bank   = m[0][1]                       ; starboard · worldUp  =  sin(bank), + = banked left
wingUp = m[1][1]                       ; bodyUp    · worldUp  =  cos(bank)

ω += 0.205 · bank                        · dt · m[1]          ; yaw  axis, SIGNED by bank
ω += ( 0.165 · |bank|
     + (wingUp < 0 ? −0.205 · wingUp : 0) ) · dt · m[0]        ; pitch axis, always nose-UP
```

⚠ **The inverted term is the SAME 0.205 constant applied to the PITCH axis** — not a third
number, and not an addition to the yaw term. `0x49167b` builds `|bank|·0.165·dt`, compares
`m[1][1]` against `0.0` (`0x6032c8`), and on the `wingUp < 0` branch subtracts
`0.205 · dt · wingUp` — a subtraction of a negative, so it *adds*. It therefore peaks
wings-level **inverted**, where the `0.165` bank term is identically zero: an aeroplane on its
back is pulled toward the ground rather than left to fly hands-off. Both terms vanish exactly at
wings-level upright, so level cruise is untouched by construction.

Consequences worth stating, because each is easy to get backwards:

- The **yaw** term is what drops the nose in a steep bank. At 90° of bank the body up axis is
  horizontal, so a yaw rate is a rotation in the *vertical* plane — this is the fall, and it has
  no equilibrium (nothing opposes it as the nose falls).
- The **pitch** term is what makes the turn. At 90° of bank the body pitch axis is vertical, so a
  nose-up rate is a pure heading change, in the direction of bank.
- Neither is gated by the AOA/G limiters or by any authority curve; they are added raw.

The two constants are **`.data`, not `.rdata`, because the retail build ships a developer console
that writes them**: `fall_off` (`0x622e94`) stores to `0x6289f8` at `0x43e299` and `bank_off`
(`0x622ea0`) stores to `0x6289fc` at `0x43e2dc`, in the same command table as `hide_plane`,
`kill` and `revive`. So they are tunable at runtime but authored nowhere — no data file reaches
them, and 0.205/0.165 are what every shipped flight uses. The whole block is inlined a second
time at `0x48cc61`–`0x48ccf4` on the AI flight path, guarded by a byte flag; identical
arithmetic, recorded for M4.

**C22 landing note — what implementing it settled, and what it did not.** Landed in
`FlightModel.cs` as an addition to the `BodyRates` command, carrying each axis' `RecInertia`
(which the original applies downstream, above) but **not** the remake's `*Tune`, since those
calibrate stick authority against video and are ours. Measured on the Bloodhawk: every
wings-level scenario is unmoved to the last printed digit across all eleven airframes
(`level-top-speed`, `terminal-dive`, `roll-360`, `pitch-rate`, `yaw-360`, `altitude-cap`,
`accel`/`decel`, `eighth-throttle`) — the coupling cannot reach them, which is the cleanest
possible confirmation of the "vanishes at zero bank" property.
⚠ **It does NOT close the sustained-turn-rate gap, and it moves it the wrong way**: 32.35 →
34.71 °/s against the original's 18.95, and up on ten of eleven airframes. Both terms *add*
heading rate in the direction of bank, so the plan's expectation that this item would explain the
original's 1.6×-slower banked turn is disproved at the mechanism. Whatever makes the original
turn slowly when banked is still missing — and as of 2026-08-09 **no authored field is a candidate
for it**: `highGs` is measured inert (D33), and `turn_fade_*` is this document's own base ramp,
keyed on airspeed alone and saturated at 1 above 50 mph (see "Control authority vs speed" and the
correction note at the end of this section).
The one place it clearly improves fidelity is the **knife-edge**: a neutral-stick 90° bank held
for 35 s used to pin the nose at the bounded −4.01° sag and settle (−316 m); it now drifts
linearly at ≈1.08 °/s to −41.8° with no equilibrium (−1993 m), which is the *shape*
`BL-247` measured on the original (0.69–0.89 °/s to −27° over 36 s) and could not previously be
produced at all. Recorded for `D31`, not acted on — the drift is now ≈1.2–1.6× too fast, which is
a magnitude question where it used to be a mechanism question.

## Weathervane centring — resolved, and the summary line above was wrong

`return_rate` is the last contribution `FUN_00490f70` makes to the angular accumulator, at
`0x4916fe`–`0x4917f0`, immediately after the bank coupling. It is guarded twice:

```
cmp esi, [0x71c298]        ; 0x4916fe — the PLAYER object. AI skips the whole block.
fld [ebp-0x10]; fcomp 0    ; 0x49170a — speed ([obj+0x934], the true |v|) must be > 0
```

Then, in full:

```
n   = −m[2]                                  ; 0x49171e — the three floats at +0x198, sign bit
                                             ;   XORed. m[2] is −nose (thrust uses −m[2] too,
                                             ;   0x48fe91), so n IS THE NOSE.
v̂   = velocity · (1/speed)                   ; 0x491754 — 1/|v| times the vtable's velocity getter
q   = shortestArc(n, v̂)                      ; 0x53fd40 — half-vector quaternion:
                                             ;   ĥ = normalize(n + v̂); q.w = n·ĥ; q.v = n×ĥ
                                             ;   (degenerate n ≈ −v̂ → q.w = 0, q.v = normalize(n))
r   = (atan2(|q.v|, q.w) / |q.v|) · q.v      ; 0x53fca0 — quaternion log: (θ/2)·unit(n × v̂)
ω  += return_rate · dt · r                   ; 0x49179f — [obj+0x654] = ReturnRate, × dt (0x71c56c)
```

so, with α the angle between the nose and the flight path:

```
ω += return_rate · (α/2) · unit(nose × v̂)
```

⚠ **Three corrections to what this document said before.**

1. **The axis is `cross(nose, v̂)`, not `cross(−nose, v̂)`.** The old one-liner transcribed the
   code's `−m[2]` as "−nose", but `m[2]` is itself −nose — the same negation the thrust term
   applies to point along the nose. The sign matters completely: `cross(−nose, v̂)` drives the
   nose *away* from the flight path and is divergent.
2. **The angle is HALVED.** `0x53fca0` is a quaternion→rotation-vector helper and `atan2(|q.v|,
   q.w)` is the half angle; nothing doubles it back. So the effective spring rate is
   `return_rate/2`, not `return_rate`.
3. **It is proportional to the misalignment, and it enters the same accumulator as everything
   else** — so it carries `rec_moments_inertia` downstream and is damped by `ang_momentum_damp`.
   Torque ∝ displacement plus damping ∝ rate is a **spring-damper**: second order, where folding
   `return_rate` into the damping coefficient (what the remake did) is first order.

The axis is perpendicular to the nose by construction, so the term has **no roll component at any
attitude** — a weathervane cannot bank an aeroplane.

**C23 landing note — what implementing it settled, and what it did not.**

- **`BL-147` does not close.** The square-wave pitch-cadence sweep run through our own build (same
  input, same estimator, so no transfer function is assumed on either side) falls **19.6×** between
  the 1300 ms and 570 ms cadences before the change and **23.3×** after, against the original's
  **42×**. Right direction, about a sixth of the gap. A second-order response is part of the answer
  and demonstrably not the whole of it.
- ⚠ **And the sweep corrects `BL-147`'s own arithmetic.** Its "42× is 3.5× steeper than any single
  first-order lag permits" reasoning assumed the chain is *double integration + one lag*. The
  remake's is not, and never was: the flight path chases the nose through a **second** first-order
  lag (`lift_accel_rate`, τ = 1.33 s), so the pre-C23 build already rolled off 19.6× — 1.65× past
  that "ceiling" — with `return_rate` still folded into the damping. The 3.5× figure is therefore
  not a measurement of *our* build's deficit, and the amplitude-for-amplitude comparison above
  replaces it.
- **It reaches two of the three pinned steady rates, and that is not a sign error.** A sustained
  full-stick manoeuvre holds a real misalignment — α ≈ 18° in a full pull, β ≈ 8° on full rudder —
  so the restoring torque opposes the stick there. Un-refit, `pitch-rate` fell 33.47 → 28.35 °/s
  and `yaw-360` rose 28.65 → 34.43 s. `PitchTune` 0.75 → **0.89** and `YawTune` 1.33 → **1.57**
  re-pin them (33.54 °/s, 28.55 s). `RollTune` is untouched: the torque provably cannot reach roll,
  and `roll-360` is 1.98 s before and after on all eleven airframes.
- **It accounts for a little over half of `PitchTune`'s existence.** The un-tuned formula
  `pitch_torque · rec_inertia / ang_momentum_damp` gives the Bloodhawk 44.6 °/s against a measured
  33; `PitchTune` 0.75 was the whole of that gap, and 0.89 is the part the weathervane does not
  explain. It is a smaller fudge than it was, not a retired one.
- **It steepens the knife-edge sag rather than opposing it.** In a sagging knife-edge the flight
  path is *below* the nose, so the weathervane pulls the nose down onto it: the 35 s neutral-stick
  90° hold goes −41.80° → **−44.61°** (−1991 → −2124 m), i.e. ≈1.17 °/s against `BL-247`'s measured
  0.69–0.89. Recorded for `D31`; no knife constant was touched.

## Bank-independent lift vs the measured knife-edge sag — reconciled (D31)

The conflict as it stood: the decoded lift demand is projected onto the body **X/Y plane** and its
magnitude is that projection's length, so at 90° of bank the body X axis is vertical and the wings
still carry full weight — the decode says lift does not depend on bank. Against that, the original's
own footage (`CAP-05`, two knife-edge takes at 143 and 300 mph) shows it sagging and eventually
spiralling in. **Both are true, and neither term is where the other one thought it was.**

**The sag is in the NOSE, not in the lift.** At 90° of bank the body yaw axis is horizontal, so the
bank→yaw coupling's `0.205` *is* a nose-sag rate; the weathervane then pulls the nose further down
onto the falling flight path. Neither term is bank-dependent lift and neither was written for the
knife-edge — they are the original's own constants, landed in C22 and C23 for other reasons, and
between them they reproduce the footage's shape: a drift that never finds an equilibrium.

**So the remake's own bounded nose-sag term is retired, and its removal IMPROVES the onset it was
fitted to.** `KnifeNoseSag` (0.07 rad) and `KnifeNoseRate` (0.2 rad/s) were a bounded ≈4° step at a
capped rate, added when nothing else dropped the nose in a knife-edge. Measured against the 143 mph
take, with the throttle trimmed for level flight at the entry speed:

| | original | with the sag term | without it |
|---|---:|---:|---:|
| nose at +3 s | −4.9° | −7.28° | **−4.94°** |
| sink at +3 s (ft/s) | 0.5 | 12.7 | **5.7** |
| nose drift 3→36 s (°/s) | 0.69 | 1.05 | 1.09 |
| altitude lost in 36 s (m) | 540 (in 38.9 s) | 1187 | **1087** |

It was also never really a knife-edge term: it keyed on `1 − |bodyUp·up|`, which is 0.29 at a 45°
nose-up attitude with the wings dead level, so it fought every pull at up to 11.5 °/s. Removing it
moves `zoom-climb` toward its measured 936 ft on **all eleven** airframes (Bloodhawk 1396 → 1338 ft,
Balmoral 3935 → 1791) and lets the Balmoral reach the altitude cap at all (4471 → 6572 ft).

**`wingVert` stands, in its one surviving use, and the decode does not contradict that.** Lift has
not read it since B11. Its only remaining reader is the rate at which the flight path chases the
nose — an explicit kinematic slerp that is the *remake's* arcade handling and has no counterpart in
the original's force path, so "lift is bank-independent" says nothing about it. What does speak to
it is the footage: the original holds its nose 4.8° → 8.3° **below** its flight path across the
36 s, a gap that grows. Ours runs 2.9° → 1.2°; with `wingVert` retired (chase floor 1.0) it
collapses to 1.9° → 0.5° and the 36 s altitude loss rises 1087 → 1334 m. Every knife-edge
observable moves the wrong way without it. Lowering the floor instead of removing it moves every
row toward the footage (at 0.10: gap 3.5° → 2.1°, drift 0.96 °/s, 874 m) and still cannot reach it
— and it walks the knife-edge α up to 5.36°, past `liftAOAs[0] = 5°`, where the airflow blend
starts engaging in a knife-edge. Left at 0.35; `KnifeEdgeTests` pins the α margin on all eleven.

**What is still open, and it is one number, not four.** The whole banked rotation runs ≈1.6× fast:
nose drift 1.09 °/s against a measured 0.69–0.89, heading 1.7 °/s against 0.68–1.13 — the same
≈1.6× by which `sustained-turn-rate` exceeds the original's banked pull (32.8 against 18.95). Two
independent manoeuvres, two different body axes, one ratio. Nothing was tuned to close it here.

⚠ **Do not reintroduce a nose-sag term to deepen the knife-edge.** The decoded bank→yaw coupling
already drops the nose there, and the weathervane then pulls it onto the falling path; a second
nose-down term double-counts what is already present and re-creates the wings-level leak above (the
retired term rotated the nose down at up to 11.5 °/s in a plain 45° pull).
⚠ **Do not lower `KnifeAlignFloor` to close the remaining gap either.** Lowering it moves every row
toward the footage and still cannot reach it, because the remaining error is in the ROTATION rate —
the ≈1.6× above — and retuning the chase constant would hide a rotation error inside it. It also
runs into a real boundary at 0.10, where the knife-edge α reaches 5.36° and crosses
`liftAOAs[0] = 5°`.

⚠ **CORRECTION (2026-08-09): the authored candidates are exhausted, and this document said
otherwise for four items running.** C22, C23, D31 and D33 each parked this gap on "`BL-095`'s
unconsumed `turn_fade_in`/`turn_fade_out`/`highGs` are the only authored fields shaped like it".
Both halves are now false. `highGs` is measured inert on every airframe (D33). And `turn_fade_*` is
**already decoded in this very document** — "Control authority vs speed" above: `FUN_00490e10`'s
base ramp is a function of **airspeed alone**, 0 at `turn_fade_in` (10) rising to 1 at
`turn_fade_out` (50 authored), **held at 1 above that**, scaling roll and pitch authority with no
bank or load-factor term anywhere in it. The banked turn settles at 222–260 mph and the knife-edge
takes are at 143 and 300, so the ramp is saturated across the whole regime where the 1.6× appears
and cannot be its cause. The ramp is a real unimplemented low-speed behaviour (`BL-330`) — it is
simply not this. **What remains is not a decode question.** Recorded rather than quietly
re-pointed: a gap that has been attributed to the same three fields four times is exactly the kind
of inherited claim that stops being re-checked.

⚠ **UPDATE (2026-08-15): the "not internally consistent with a coordinated level turn" half of that
paragraph was itself wrong, and `CAP-33` is what corrected it.** The apparent inconsistency was
`CAP-01`'s 58.7° implied bank (`V·ω` = 32.96 m/s² against `nom_gravity`) set against the ~100° its
ADI sky-centroid reads. `CAP-33` was flown with the pilot **holding a known 60–70° of bank** and
reporting it at the controls, which makes the bank independent of any instrument: over that turn the
ADI centroid reads a mean **105.1°** (range 79.5–125.4°) while `V·ω / nom_gravity` reads **62.2°**
(range 56.0–69.9°), both over the turn's 23 one-second bins. The ADI reading tracks the pitch cycle,
not the turn — binned per second, `r(ADI roll, climb rate) = +0.886` against
`r(ADI roll, heading rate) = −0.091`. So the ADI shows airframe attitude, which in a high-α pull is
tens of degrees away from the bank of the turn, and the original **is** flying coordinated closely
enough for the level-turn relation to recover the flown bank. `CAP-01`'s 58.7° was the good number;
its 100° should not be quoted as a bank.
⚠ The bank in `atan(V·ω / g)` takes **`nom_gravity` (20 m/s²)**, not 9.81; Earth gravity gives 73.4°
for `CAP-01` and breaks every comparison above. Full record: `git log --grep=BL-307` (the entry
itself is closed and deleted).

## The three arcade terms

None of these has an aerodynamic justification, and all three distort any model fitted from
observed video.

1. **Thrust varies with nose attitude.** Available thrust is scaled by
   `(1 + 0.24 · a)`, and *additionally* by `(1 + 0.13 · a)` when `a ≤ 0`, where `a` is the
   **world-up component of the body Z axis** — and the nose is **−Z**, so `a` is negative in a
   climb. Climbing loses thrust (0.6612× straight up) and diving gains it (1.24× straight down),
   **on top of** the real gravity term, not instead of it. Two separate coefficients, one of them
   one-sided. Fully recovered under "Attitude thrust — resolved, and the climb it settles" below.
2. **Bank coupling — hardcoded, not data-driven.** Constants **0.205** and **0.165** (at
   `0x6289f8` and `0x6289fc`) convert bank directly into yaw and pitch rate. This is the
   coordinated-turn cheat that makes banking turn the aircraft. Fully recovered under
   "Bank coupling — resolved" above, including the inverted case.
3. **Weathervane centring.** `return_rate` applies a torque along `cross(nose, v̂)` — HALF the
   misalignment angle — pulling the nose onto the velocity vector. **Player aircraft only** — AI
   does not get it. Fully recovered under "Weathervane centring — resolved" above, where the
   `−nose` this line used to read is corrected.

There is also a **boost state**: it **replaces** the throttle multiplier with a flat **1.8** (not a
multiply — `mov [ebp+8], 1.8f` on the boost branch at `0x48fcb6`, where the normal branch loads the
throttle) and multiplies the drag coefficient by **0.8**. So boost is "throttle pinned to 180 %",
and boosting at part throttle is identical to boosting at full throttle.

## Attitude thrust — resolved, and the climb it settles

The two coefficients sit between the throttle multiply and the `ThrustFactor · RefArea` scaling, in
the force accumulator `FUN_0048fc40`. The whole block is eight instructions:

```
0048fce2  call 0x41acf0            ; thrustAvail(mach, atm)
0048fce7  fmul [ebp+8]             ; * throttle (or the boost 1.8)
0048fcea  fld  [esi+0x19c]         ; a = orientation row 2 . Y
0048fcf3  fcomp [0x6032c8]         ; compare a with 0.0, popping it
0048fcfe  je   0x48fd14            ; ZF set = neither C0 nor C3 = a > 0  -> SKIP
0048fd00  fld  [0x6080d8]          ; 0.13
0048fd06  fmul [esi+0x19c]
0048fd0c  fadd [0x6032dc]          ; 1 + 0.13*a
0048fd12  fmulp st(1)              ; avail *= that                       (only when a <= 0)
0048fd14  fld  [0x6080dc]          ; 0.24
0048fd1d  fmul [esi+0x19c]
0048fd30  fadd [0x6032dc]          ; 1 + 0.24*a
0048fd38  fmul st(1)               ; avail * that -> [ebp-8]
```

**The sign is settled from the bytes, not from the coefficient names.** `[esi+0x19c]` is the Y
component of **row 2** of the orientation matrix at `+0x180`, and row 2 is **−nose** — proved at the
point of use rather than assumed: at `0x48fe91` the thrust magnitude is `fchs`'d and *then*
multiplied by that same row (`0x198`/`0x19c`/`0x1a0`) before being added to the force vector, so
`force = −magnitude · row2 = +magnitude · nose`. Therefore `a = −nose.Y`: **negative climbing**, and
both branches reduce thrust there. A vertical climb keeps `(1 − 0.24)(1 − 0.13) = 0.6612`; a
vertical dive gets `1 + 0.24 = 1.24`, the one-sided term not applying. Nothing clamps `a`; it is a
unit-vector component already.

**Gravity is NOT attitude-scaled anywhere.** `0x48ff85`–`0x48ff9d` is the whole of it —
`[obj+0xc4] / 9.82 × [obj+0x674]` subtracted from `force.Y`, with no attitude read in the block.
This matters because the game's own design document describes gravity as pitch-scaled and *reduced
on upward pitch* so climbs stay flyable. The shipped executable does the opposite thing in a
different term: it penalises the climb through thrust. **The GDD is design intent; the binary is
behaviour, and they disagree in sign here.**

### The sustained climb — what this cost, and what it did not close

The remake carried a fitted `ClimbGravityScale = 0.6` that spared a climbing aircraft, on the
reading that the original held speed in a climb better than plain energy exchange predicts. It runs
opposite to the decoded terms, so only a measurement could separate them.

The measurement is the original's own sustained full-throttle climb
(`OriginalScreenshots/Videos/Climp 90° 100% Thrust.mp4`, decoded to
`videodata/.../Climp 90° 100% Thrust.mp4.csv`, clip key `climb90`): entry **298.9 mph**, the flight
path settles at **56.3 ± 3.2°**, speed falls to a minimum of **152.4 mph at +6.5 s** and then
**recovers** onto a plateau — 163.05 mph across 12–18 s, still creeping to a flat **167.0 ± 0.5 mph**
by +36 s — climbing ≈12,000 fpm from 900 to 6,300 ft. It leaves that state at ≈6,600 ft, which is
the altitude ceiling (`CAP-03`) and not the climb.

All four arrangements, same build, same probe (`Probes.SustainedClimb`), plateau over 12–18 s
against the footage's **163.05**:

| arrangement | plateau | vs footage |
|---|---:|---:|
| `ClimbGravityScale` only (the pre-change model) | 276.66 | +69.7 % |
| neither mechanism | 257.74 | +58.1 % |
| both mechanisms | 232.20 | +42.4 % |
| attitude terms only (landed) | **204.04** | **+25.1 %** |

**The fitted constant makes the climb worse, and it makes it worse on its own** — removing it alone
moves 276.66 → 257.74 with no attitude term anywhere near it. That is what "it was absorbing the old
drag and thrust shapes' error" looks like from outside: B12/B13 replaced those shapes, and what the
constant was compensating went with them.

⚠ **The residual is real and is recorded, not tuned.** 204 against 163 is +25 %, and the *shape*
differs too: the original undershoots its own plateau by 9 % and climbs back out of it, where the
model decays monotonically. The along-path balance at the footage's own plateau needs a thrust
factor of **0.563**, and no attitude in the decoded formula reaches that — 0.6612 is its floor. But
the probe holds α = 0 (attitude set to the path), and the clip is a **90° pull**: at a 90° nose with
the measured 56° path the same decoded force path balances to **−3.3 %**, because the attitude scale
bottoms out *and* the nose-to-path cosine takes another 18 %. The clip's ADI saturates above ≈+30°,
so its nose angle is **not readable** and this cannot be settled from this capture — it is exactly
what `CAP-20` (a shallow, held climb with a readable ADI) was filed to answer. Do not close the gap
by moving 0.24/0.13; they are the binary's, and the dive side of the same scale lands
`terminal-dive` at 356.0 mph against a measured 355.2 ± 6 with nothing fitted.

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

⚠ **This does not reopen the band question.** `0x71bb3c` sits in `.data` with a single read
reference (the `fcomp` itself) and no writer anywhere in the image — swept exhaustively under A1,
see the Atmosphere section — so a byte-level reading says "threshold 0, thin band always". The
**dense band is established by
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
reference speed** — `speed/fd_speed` for gauge and effect fractions (`0x4b1e54`), a far-field
cruise speed `fd_speed · throttle` (`0x48c593`, A2 below; this used to read "an AI target speed",
which it is not), and a speed clamp (`0x46aaf4`) — never as a solved
equilibrium. **Do not treat `fd_speed` as the original's top speed in B12/B13 without settling
this.**

**Bonus, and load-bearing for B13:** `[obj+0x124]` and `[obj+0x128]` are the **commanded** and
**current throttle** — `FUN_0048e585` rate-limits the current toward the commanded and burns fuel
at `[obj+0x134] -= dt · throttle · 5`, and `FUN_00491820` snaps them together. The current throttle
enters thrust as a **plain multiply** at `0x48fce7`. That is the linear-throttle claim, confirmed
at source in this item rather than inferred.

### A2 (2026-08-15): throttle is a lever for the AI too, and `fd_speed · throttle` belongs to a far-field model

**The throttle interface has no AI branch.** `FUN_0048fc40` reads the current throttle
`[obj+0x128]` and multiplies the Mach thrust curve by it (`0x48fcc6` loads it, `0x48fce7`
multiplies), with no player compare anywhere in the chain; boost (`[obj+0x947]`) replaces the lever
with a flat `1.8` and scales drag by `0.8` (`0x48fcb6`–`0x48fcbd`). The commanded/current pair
`[obj+0x124]`/`[obj+0x128]` and the 0.5/s slew below are likewise unbranched, in the **live**
integrator `FUN_0048e580` (`0x48e645`–`0x48e6bd`), so an AI aircraft's throttle moves under exactly
the same rate limit as the player's. The player's commanded value is written by the input handler
`FUN_00487460`; AI commanded values are written at spawn/placement (`FUN_0047f1f0` writes `0.4` at
`0x47f28b` and `1.0` at `0x47f3fb`). **`AiPilot.Throttle` as a 0..1 lever is the right shape; the
original has no AI speed-setpoint interface.**

**What `fd_speed · throttle` at `0x48c593` actually is: the far-field aircraft model.**
`FUN_0048c470` opens with a guard that skips the whole aerodynamic path when the aircraft is flagged
crashed (`[obj+0x384]`), **or** when it is not the player and its **horizontal** distance from the
player exceeds 1000 m (`FUN_00538920` returns `Δx² + Δz²`, compared against `1e6`). In that
branch the aircraft's velocity is driven toward the nose axis at `fd_speed · throttle`
(`0x48c593`–`0x48c5a0`), plus a flat **5 m/s** for anything that is not the player
(`0x48c5ae`, `[0x6036bc] = 5.0`), and the function's linear-acceleration output is set to
`target − current` velocity rather than to a force. So distant traffic cruises along its nose at a
speed the data sets, with no lift, drag, thrust, ground blow or weathervane computed at all.

This is a level-of-detail model, not the AI's control interface: it is keyed on distance from the
player and applies to the player's own aircraft only when it is crashed. Nothing in the remake
implements it, and nothing needs to: it is invisible inside 1 km, which is where every AI aircraft
we simulate and score sits. **Decoded, unimplemented, and deliberately unowned.**

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

⚠ **The tuner shipped in the retail executable, but the dialog is dead code.** Resolved 2026-08-14:
`FUN_00445090`, the dialog that writes `dynamics.txt`, has **no callers anywhere in the image**. The
harness itself is still reachable, from `FUN_004897c0`'s tail via `FUN_00493dc0`, gated on the debug
flags `DAT_0071c78a` and `DAT_00628a00` being set with `DAT_0071c788`, `DAT_0071c789` and
`DAT_00654120` clear; it reports through `sprintf` plus `FUN_00458770` ("Top speed = %5.1f MPH",
`0x00628a6c`) rather than to a file. So the 18 figures are obtainable by flipping flags in a
debugger, not by finding a menu.

**What the harness does to the world, and why it found no ground blow.** It saves the aircraft's
orientation, position, throttle, velocity, angular rates and all six stick inputs; **overrides the
global timestep to a fixed 0.01 s**; zeroes the stick and resets position and orientation each
iteration; steps its own stripped integrator (`FUN_00491820`) in closed loops of up to 10 000 and
30 000 iterations; then restores everything. That stripped integrator is the second copy documented
under "There are two integrators" above, and it has collision and ground blow removed.

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
| `drag_factor` (global) | 3.0 | **1.5** | Dead in the executable either way (see below) |
| `stall_mag` | 0.45 | **1.25** | |
| `groundblow_elev` | 100 m | **400 m** | A four-times-longer nose ray, and a four-times-flatter falloff |
| `groundblow_mag` | 1.5 | **10** | 11× amplification of an away-from-surface command at contact, against 2.5× |
| `ai_groundblow` | 0.9 | **0.5** | The AI's fixed push is 5.0·S authored, against 1.35·S under both fallbacks |
| `bounce_factor` | 0.8 | **0.6** | The ceiling on effective normal restitution |

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

**C24 landing note — confirmed unreachable for all eleven player airframes, and nothing was
implemented.** `MaxDiveSpeedFrac` (1.75 × `fd_speed`) is the model's own hard numerical ceiling on
`Speed` — never redirected, never faded, a plain clamp applied every tick — so it is the most
generous "could this airframe ever reach the fade" test available, more generous than any
aerodynamically-settled terminal dive (itself well below it on every airframe measured, per
`POST-B14.md`). `1.75 × fd_speed` for all eleven, against `high_speed_pitch_fade`'s authored
[1000, 1001] mph window:

| Airframe | fd_speed (mph) | 1.75 × fd_speed (mph) | measured terminal dive (mph) | reaches 1000 mph? |
|---|---:|---:|---:|---|
| bhawk (Bloodhawk) | 302.0 | 528.5 | 336.36 | no |
| devastator | 252.8 | 442.4 | 280.68 | no |
| fury | 281.9 | 493.3 | 314.65 | no |
| warhawk | 201.3 | 352.3 | 217.95 | no |
| autogyro | 228.2 | 399.4 | 221.02 | no |
| avenger | 264.0 | 462.0 | 293.55 | no |
| balmoral | 176.7 | 309.2 | 149.49 | no |
| brigand | 241.6 | 422.8 | 268.85 | no |
| firebrand | 208.0 | 364.0 | 222.39 | no |
| kestrel | 217.0 | 379.8 | 236.27 | no |
| peacemaker | 290.8 | 508.9 | 324.77 | no |

The Bloodhawk's own hard ceiling (528.5 mph) is the highest of the eleven and sits at little over
**half** of `high_speed_pitch_fade[0]` (1000 mph) — every other airframe's ceiling is lower still.
**Nothing was implemented.** Untestable code on a threshold no capture of the original could ever
exercise is exactly the invented content this project's ground rules forbid; `backlog.md`'s `BL-095`
and this document now both carry the closed finding so a future session reading the decode does not
mistake the fade for a missing feature.

**Corrected — both the G and AOA limiters are inert.** The lift clamp is a hard ±5/9 G, while
`highGs` begins at 9 G and `lowGs` at −6 G. Neither limiter can engage before lift is already
capped, so no authored configuration in this install reaches them.

**D33 landing note — measured on all eleven airframes, and nothing was implemented.** The
structural argument above is real but it is not what settles this: the delivered load factor is
clamped at +9/−5 G, which is *exactly* where `highGs`'s ramp starts, so the argument only ever
proves the reduction is zero at the boundary. What settles it is the measurement. The remake's
`FlightModel.LoadFactorDemand` reports the lift demand's body X/Y projection **before** both
clamps — the most generous available reading of "the G this aircraft is pulling" — and
`CSVM.Tests/ControlLimiterTests.cs` flies five max-performance manoeuvres per airframe (the pull at
`fd_speed` and entered at 1.5 × `fd_speed`, the same pull banked, a full forward push, full rudder;
600 steps at 1/60 s each, more than a full loop) and takes the peak of each:

| Airframe | peak demanded G | `highGs[0]` | margin | peak α | `maxAOA` | margin |
|---|---:|---:|---:|---:|---:|---:|
| bhawk (Bloodhawk) | **5.01** | 9.0 | 3.99 | **25.6°** | 46.0° | 20.4° |
| devastator (`pfighter`) | 3.83 | 9.0 | 5.17 | 18.7° | 46.0° | 27.3° |
| fury | 4.43 | 9.0 | 4.57 | 22.1° | 46.0° | 23.9° |
| warhawk | 2.56 | 9.0 | 6.44 | 10.6° | 46.0° | 35.4° |
| autogyro | 3.17 | 9.0 | 5.83 | 12.4° | 46.0° | 33.6° |
| avenger | 4.07 | 9.0 | 4.93 | 20.3° | 46.0° | 25.7° |
| balmoral | 2.13 | 9.0 | 6.87 | 8.9° | 46.0° | 37.1° |
| brigand | 3.45 | 9.0 | 5.55 | 15.6° | 46.0° | 30.4° |
| firebrand (`fbrand`) | 2.57 | 9.0 | 6.43 | 10.5° | 46.0° | 35.5° |
| kestrel | 2.91 | 9.0 | 6.09 | 12.4° | 46.0° | 33.6° |
| peacemaker | 4.73 | 9.0 | 4.27 | 24.1° | 46.0° | 21.9° |

The G peak is a full-forward **push** at 1.5 × `fd_speed` on the six fastest airframes and a pull on
the rest; the α peak is the pull at 1.5 × `fd_speed` on ten of eleven. The suite's own instruments
agree from the other side: the sustained pitch-rate row reports α = 20.2/20.5/20.6° at
120/200/280 mph, `zoom-climb` 23.3° at its minimum speed, the sustained turn 23.0°, and D31's
knife-edge probe peaks at 0.71–4.29°. **The negative side is unreachable twice over:** `lowGs [−6,
−9]` sits past the −5 G clamp, *and* the demand is the LENGTH of a projected vector, so it is never
negative in this model at all.

⚠ **The margin against the executable's own fallbacks is one hundredth of a G.** The Bloodhawk's
5.01 G peak is 0.2 % **past** the compiled fallback `highGs[0] = 5` — under the fallbacks the
limiter would engage, but a fraction of a percent into a 4 G-wide ramp. What puts the mechanism out
of reach is the **authored 9**, not the model's inability to pull hard. This is the cleanest example
in the whole decode of why a fallback is evidence of intent and not of behaviour.

⚠ **If either threshold ever comes into reach, the asymmetry is the thing to get right.** The
original gates **only input that opposes the current rotation** (the sign test is on the command
versus the existing angular momentum about that axis), so the limiter damps *recovery* from a
departure, not entry into one. A limiter that scales all input instead is backwards and will read
as sluggish controls. `ControlLimiterTests` asserts each airframe's peaks against **its own loaded**
`highGs`/`lowGs`/`maxAOA`, so a data edit or a per-plane override that brings either into reach
fails the suite rather than passing silently — which is the condition under which the code above is
owed.

**Two of the parsed globals are dead in the executable.** The global `drag_factor` (→ `0x71c44c`,
fallback 3.0) and `drag_fade_speed` (→ `0x71c450`, parsed × 0.44704, fallback 40 mph) are written
by the `player.json` parser at `0x4744c0`/`0x4744f0` and **read by nothing anywhere in the image**
— a full dword-reference scan finds only the parser's own two writes for each. There is no global
drag multiplier and no low-speed drag fade; the per-plane `drag_factor` is the only drag scale.
Checked while hunting the CAP-05 deficit (a fade below ~300 mph would have produced exactly its
shape); the hunt is what proved the keys dead.

## The resting altitude cap — measured, and traced to ONE mission

The original's flight has a ceiling: the sustained climb above leaves its plateau at ≈6,600 ft
(`CAP-03`). The remake carries it as a hard clamp at **2003 m**, with a **42.8 m (~140 ft)**
overshoot backstop above that. Both numbers are **footage measurements** (C1B IA1, Bloodhawk) —
nothing in `crimson.exe` has been traced to either, so this section is measurement, not decode.

**It is a clamp on ALTITUDE, not an energy limit**, and the footage is what says so: the level
full-throttle equilibrium is flat to ±0.3 mph right up to 15 m under the line, and holding a 22°
nose-up pull against it gains no altitude at all (sub-foot over the clip's last 5 s) while airspeed
bleeds instead. The mechanism therefore deletes the frame's climbing velocity outright rather than
fading thrust, lift or drag toward the ceiling. Whatever that bleed then runs into — the stall
thresholds above — is a consequence of the clamp, not a second mechanism built beside it.

The overshoot figure is a backstop only. The footage's zoom entries coast past the resting cap on
pre-existing momentum before the clamp ever catches them, so it needs only to be at least as
generous as the measured **6712 ft** apex; it bounds a runaway frame, it does not shape the
overshoot.

⚠ **Traced to ONE mission.** Do not assume the cap is global, per-chapter/zone, or per-aircraft
until another mission's footage says otherwise.

⚠ **The dive-speed cap is the same kind of object and must not bind.** `MaxDiveSpeedFrac`
(1.75 × `fd_speed`) is a numerical backstop against a loop energy pump or a `dt` spike, not a
terminal speed — a cap that binds replaces a measured terminal with a guess. It does not bind: the
Bloodhawk's full-throttle 71° dive terminates at **1.11 × `fd_speed`** on the aerodynamics alone.
It is also the ceiling the `high_speed_pitch_fade` unreachability table below is computed from,
precisely because it is the most generous "could this airframe ever get there" test available.

## Ground blow — a control bias, not a force (`FUN_0048bf60`, `FUN_0048c220`)

Decoded 2026-08-14. All three keys are **raw scalars**: plain dword stores, no `× 0.44704`, no
cosine.

| Key | Global | Parser store | Fallback | **Authored** |
|---|---|---|---|---|
| `groundblow_elev` | `0x0071c420` | `0x00474320` | 100.0 | **400** |
| `groundblow_mag` | `0x0071c424` | `0x0047434a` | 1.5 | **10** |
| `ai_groundblow` | `0x0071c428` | `0x00474373` | 0.9 | **0.5** |

⚠ **`groundblow_elev` is a length in METRES, and it is not a threshold.** It is the length of a ray,
not an altitude, not a range that anything is compared against, and not feet. World units are metres
(see "Units and conventions"; the identification is positive, from `nom_gravity`'s 9.82 reference
divisor at `0x006080d4`, not from the absence of a conversion).

**The probe.** `FUN_0048bf60` casts a ray from the aircraft origin along the **nose**, of length
`groundblow_elev` metres, into the world segment query `FUN_004c8ec0` at `0x0048c01f`. The direction
is `−(obj+0x198)`, and `obj+0x198` is the +Z body axis, so this is `−Z`, matching the nose convention
above. A hit qualifies only if the surface faces back toward the aircraft (`dot(b, n) > 0` strictly,
`0x0048c0ff`). One threshold, no hysteresis, no latch. The same value is the falloff's denominator,
so raising it both lengthens the ray and flattens the ramp.

With `n` the hit normal, `b` the backward body axis, `d` the straight-line distance to the hit point
(`FUN_005388d0`), `c = dot(b, n)`:

```
S = sqrt(c) · (elev − d) / elev     proximity: 1 at contact, falling to 0 at the ray's end
A = normalize(n × b)                unit axis rotating the nose away from the surface
V = A · S                           the scaled axis, which is what the probe hands back
```

⚠ **The probe returns `S` and writes `V = A · S`, not `A`** (corrected 2026-08-15). Its
out-parameter takes the normalised cross product multiplied by `S` (`FUN_00422690`, then the three
scalings before the adds), and `S` itself is the return value. The caller uses that one scaled
vector **twice**, once in the dot and once in the add, which is where the second power of `S` in the
player law comes from. The out-parameter is zero-initialised from `DAT_0075d1b8`, a zero vector, and
is written only on a qualifying hit, so a miss, a `c ≤ 0` rejection and the vehicle-filter abandon
all leave a zero vector and contribute nothing on either path.

**Where it goes, which is the whole question.** `FUN_0048c220` writes into the same accumulator the
three stick channels were summed into one call earlier in `FUN_0048c470`; `FUN_0048e580` then adds
that accumulator to `obj+0x160`, the persistent angular-velocity state. The linear acceleration is a
**different argument** of `FUN_0048c470`, reaching velocity separately. Ground blow is therefore a
bias on **control response**, not an applied force, which is what the GDD's §4.1.7 describes and what
`CAP-02` inferred.

Player path (`0x0048c30f`), with `p = dot(accum, V)`:

```
p ≥ 0 (commanding away):   accum += V · |p| · groundblow_mag
p < 0 (commanding into):   accum += V · 0.05·|p| · groundblow_mag,  and S is zeroed
```

Both push along `+A`, away from the surface. Written against the unscaled command component
`u = dot(accum, A)`, the same two lines are `u → u · (1 + mag·S²)` and
`u → u · (1 − 0.05·mag·S²)`, since `p = S·u` and the add carries a further `S`. Three consequences,
each matching a design claim:

- **It cannot overpower the stick.** An into-obstacle command is scaled by `1 − 0.5·S²` at the
  authored 10, so it is exactly halved at contact, cut less than that further out, and never
  reversed (reversal would need a `groundblow_mag` above 20).
- **It cannot save a head-on.** As the approach becomes perpendicular, `n → b`, so `n × b → 0` and
  the whole term vanishes (`FUN_00422690` leaves a zero vector untouched).
- **Commanding away is amplified** by `1 + 10·S²`, i.e. 11× at contact with the authored 10.

**A second, smaller effect.** After the accumulator write, the velocity *direction* is steered
exponentially toward the nose at `DAT_00622bbc · S` per second (`FUN_00460700`, speed preserved,
writing `obj+0x924`/`obj+0x934`). `DAT_00622bbc` is **2.0** and its only writer is the debug console
command `gbc` (string `0x00622c84`, handler `0x0043db9d`), so it is not a data key. It is suppressed
on the player path whenever the pilot is commanding into the obstacle, because `S` is zeroed there.

**The AI path is a different law, not a scaled one** (`0x0048c317`):

```
accum += V · (ai_groundblow · groundblow_mag)           = A · S · 5.0 as authored
```

It is independent of the AI's own command (a fixed push, where the player's is proportional to what
the pilot asked for), **linear** in proximity rather than quadratic, and **not multiplied by `dt`**
anywhere in the chain, so it is frame-rate dependent. Both the factor and `S` are cut to **0.15**
while the clock is inside `obj+0xB4` (below).

**Gates on the whole effect.** `obj[0xd6] != 4`; for non-player objects the clock must be past
`obj+0xAC`; and `FUN_0048c470` skips the call entirely when the player is flagged crashed
(`obj[0xe1] != 0`).

`obj+0x358` (dword `0xd6`) is the **AI mode enum**, read off the debug HUD's jump table at
`0x0041d1b8` in `FUN_0041c470` and the string each target pushes:

| Value | Meaning |
|---|---|
| 0 | patrol / evade / pursue / lay off, chosen by `obj+0x948`, `obj+0xBA`, `obj+0x2F0` |
| 1 | evasive maneuver |
| 2 | approaching danger zone |
| 3 | avoid crash |
| **4** | **stunned** |
| 5 | navigating danger zone |

State 4 is set by `FUN_004200d0` ("Stunned for %f seconds based on stun recovery", `0x00620518`),
which zeroes the control inputs and sets `obj+0xC0 = clock + duration`; the related tokens are
`stun_recovery`, `stun_recovery_interval` and the smokescreen weapon's `smokescreen_stun_*`, so it
is a weapon effect. A stunned AI therefore has its controls zeroed **and** its ground blow
suppressed, so it flies into terrain. That reads as deliberate.

**Emitters.** `FUN_004c8f70` walks the terrain grid and tests, per cell, the terrain geometry and
every scene node whose flag word at `node+0x24` carries both bits `0x4` and `0x10`, which are
`ACTIVE` and `INTERSECT_SURFACE` (`tools/mech3ax/crates/api-types/src/gamez/nodes.rs`);
`FUN_004c8ec0` keeps the nearest hit. Terrain and ordinary scenery always qualify.

**The vehicle filter, and what it excludes** (decoded 2026-08-15). Inside the player-only branch
guarded by `param_1 == DAT_0071c298` at `0x0048c047`, a hit node carrying bit `0x40000000` at
`node+0x28` is resolved through the vehicle registry at `0x0071dab8` (`FUN_004afee0`, matched on the
entity's node pointer at `+0xc`, walking up the parent chain on a miss). If an entity is found and
its byte at `+0xcc` is **zero**, the whole term is abandoned and the function returns 0
(`0x0048c0ac`). Everything else falls through and repels: an unmarked node, a marked node with no
registry entity above it, and a marked node whose entity carries a non-zero `+0xcc`.

⚠ **The `0x40000000` mark is applied at spawn, never authored.** Its only setter is `FUN_004848f0`
(`0x0048490c`), which ORs it into the node and its whole child subtree, and its only external caller
is the vehicle spawner `FUN_0047c210`. No node in the shipped world data carries it: all 7,064 nodes
in `extracted/C1/gamez/nodes.json` have `update_flags` 0 or 1.

**The byte at `+0xcc` is a mode flag, not a transient. It means "this vehicle is following a scripted
path instead of being flight-simulated".** The update dispatcher `FUN_00489ea0` reads it first and
calls the path follower `FUN_0048a110` **instead of** the movement law selected by `obj+0x67C`
(`0`/`4` being `FUN_0048e580`, the flight integrator that consumes ground blow). Its writers:

| Address | Function | Effect |
|---|---|---|
| `0x004b005e` | `FUN_004aff80`, the constructor | 0, so the default is not an emitter |
| `0x0047c568` | `FUN_0047c210`, the spawner | 1 when the spawn record carries a path, with `+0xd4 = 1` at `0x0047c57e` |
| `0x00452275` | `FUN_00451bf0` | 1, path taken from the placement record's `+0x24`, leaving `+0xd4` alone |
| `0x0049427c` | `FUN_004940d0` | 1 with `+0xd4 = 0`, called from the mission-goal runtime `FUN_0046a490` |
| `0x0048a863` | `FUN_0048a110` | 0, the only clear, and only on reaching the **last** waypoint within 5.0 m |

A non-zero `+0xd4` makes `FUN_0048a110` return on its first line, so the vehicle neither moves nor
clears `+0xcc`; the release is `FUN_0046a2b0` (`0x0046a2c3`), a mission-goal action. A vehicle
spawned with a path is therefore a frozen emitter from placement until a goal releases it, and stops
being one the moment it completes the path and drops into the flight model. No vehicle type holds the
flag by identity. The follower itself is written up under `BL-361`.

**Zeppelins: yes, and by the default rather than by a zeppelin rule.** `extracted/zrdr/vehicle.zrd.json`
names no zeppelin, blimp or airship type, so a zeppelin is never a spawned registry vehicle in this
install. IA1's is the C1 gamez scene node `multiplayer1zep` (`extracted/C1/gamez/nodes.json`), driven
by `mis_anim` animations, with `update_flags` 1 and both `active` and `intersect_surface` set. It
never reaches the registry filter at all, and repels the player exactly as terrain does. The GDD's
naming of zeppelins describes the outcome, not a mechanism.

⚠ **The vehicle filter is player-only**, since it sits inside the `param_1 == DAT_0071c298` test. On
the AI path nothing is filtered and every hit the sweep returns repels, other aircraft in ordinary
flight included.

**Implemented 2026-08-15** (`BL-359`, closed; `git log --grep=BL-359`), player path only.
`FlightController.ProbeGroundBlow` casts the ray and `FlightModel.GroundBlowTerm` applies the law,
added to the command accumulator after the bank coupling and the weathervane and before
`BodyRates += cmd * dt`, which is this function's own ordering. Two things the port does differently
on purpose: the emitter filter is the probe's `CollisionLayers.World` mask rather than a registry
lookup (only aircraft bodies carry the Aircraft layer, so terrain, scenery and the zeppelin repel
and aeroplanes do not, which is the same set the filter above produces), and the second effect is
folded into the model's existing nose-chase as `align + 2·S` — exact rather than approximate, since
two exponential steers toward the same target compose. `CSVM.Tests`' `GroundBlowTests` pins the law,
including the `S²` power and the body-frame conversion. **The AI law is not built.**

## Collision response and `bounce_factor` (`FUN_0048d7f0`)

Decoded 2026-08-14. `bounce_factor` lives in `player.json`'s `crash` block, is a **raw scalar**, and
lands in global `0x0071c35c` from the parser store at `0x00473c38`. Its default is pre-set at
`0x00473bb5` *before* the block is looked up, so an absent `crash` block leaves the fallback
standing. Fallback **0.8**, this install authors **0.6**.

`FUN_0048d7f0` sweeps the aircraft's contact spheres (`obj[0x1a9]..obj[0x1aa]`, stride `0x24`)
through the world and resolves the **single deepest** contact. On a contact frame it **replaces** the
frame's translation with a placement at the contact point plus a fixed **0.03** along the normal,
rather than moving by `v·dt`. One resolution per aircraft per tick, no sub-stepping.

⚠ **Only the player bounces.** The impulse branch is entered only when `obj == DAT_0071c298` and the
player is not already crashed. AI aircraft get position correction and an impact cosine, and no
impulse at all.

With `r` the contact point minus `obj+0x204`, `ω` the body rates at `obj+0x16c`, and
`I⁻¹ = (obj[0x197], obj[0x198], obj[0x199])`:

```
vp    = v + 2·(ω × r)                        contact-point velocity, rotational term DOUBLED
J     = −(n · vp) · n                        normal only; no tangential or friction term
Δω    = R · I⁻¹ · Rᵀ · [ (r × J) / |r|² ]    zero vector if |r|² == 0
L     = 2.25 · |J|   (literal at 0x00608108, hardcoded, no data origin)
A     = |Δω|
f_lin = L/(L+A) ,  f_ang = A/(L+A)           L == 0 → 0/1 ;  A == 0 → 1/0

v          += J  · (1 + f_lin · bounce_factor)                    0x0048e429
obj+0x160  += Δω · (1 + f_ang · bounce_factor) · 0.5              0x0048e4bc
```

The `0.5` is the shared literal at `0x006032e0`, also hardcoded. The angular impulse goes into
`obj+0x160`, the same accumulator the stick and ground blow write to.

Effective normal restitution for a non-rotating contact is **`f_lin · bounce_factor`**, bounded by
`[0, 0.6]` as authored.

⚠ **The impulse is not a rigid-body impulse, and that defect is the mechanism behind `CAP-14`'s
split.** It is computed from the *contact point's* velocity, with the rotational term doubled, then
applied in full to the *centre of mass* with no reaction term removing the rotational share. Taking
the normal component with `k = f_lin · bounce_factor`:

> `n·v_after = −k·(n·v) − (1 + k)·2·n·(ω × r)`

The first term is the bounded restitution. The second is unbounded and is not restitution at all:
whenever the contact point closes faster than the centre of mass, which is the normal case for a
belly or nose contact carrying any nose-down pitch rate, the aircraft leaves the surface faster than
`bounce_factor` permits.

⚠ **There is no surface dependence anywhere in the code.** No test on the normal's verticality, no
per-surface-type table, no material lookup, no friction term. `CAP-14`'s measured split (0.75–0.86
on flat ground against 0.06–0.18 on vertical faces, `BL-172`) is reproduced by the geometry alone: on
flat ground `r` is long and roughly horizontal against a vertical `n`, so `n·(ω × r)` is large and
negative while `f_lin` stays high; against a wall `r × J` is large, `f_ang` dominates, `f_lin → 0`,
and the impact converts to spin instead of rebound. The direction of that split is confirmed; the
flat-ground magnitude comes from the doubled rotational term, not from `bounce_factor`, which cannot
produce it.

**Ruled out as sources of the excess, each traced:** multiple resolutions per frame (one per aircraft
per tick, `FUN_004897c0`'s head); successive-frame stacking (once the contact velocity is outgoing
`J` points back *into* the surface, so repeats damp rather than compound); a separate ground-support,
landing or gear path (a whole-image scan for float stores to `[reg+0x160]` returns exactly one site,
inside `FUN_0048d7f0` itself; the `Landing` classes at `0x00625790` are mission landing-zone volumes
and `touchdown_` is only a name prefix); and gravity ordering (the resolver is the **last writer of
velocity in the frame**, so lift and weight cannot add to the rebound within the contact frame). The
0.03 push-out is real but is a position placement, worth roughly 0.9 m/s of spurious upward velocity
in a single inter-frame interval at 30 fps, and it does not accumulate.

**Two per-object timers**, both absolute seconds against the game clock at `0x0071c470`:

- **`obj+0xAC`, a collision grace window.** `FUN_0048d7f0` returns immediately while the clock is
  inside it (`0x0048d7fd`), so the object has **no collision at all**, and the AI's ground blow is
  skipped too. Set to clock + **1.5** at spawn (**5.0** on the alternate placement path,
  `0x0045243c`), and to clock + **1.0** on **both** parties after an entity-versus-entity impact
  (`0x0048d383`, `0x0048d395`), the same branch that cuts that impact's damage terms to 20 %.
- **`obj+0xB4`, a post-drop settling window.** Clock + **2.5**, written on the spawn paths only.
  `FUN_00452450` gives the context: the entity is repositioned, yawed to −π/2, and given the launch
  velocity minus 22.352 m/s vertically, which is a drop from a carrier at 50 mph. Inside it, an AI's
  ground blow runs at 15 %.

Neither is a damage-invulnerability timer; `obj+0xAC` disables collision itself.

## The three `*Tune` rates — what they are pinned to

The remake's per-axis control-rate calibration. Steady rate is
`torque · rec_moments_inertia · Tune / ang_momentum_damp` (× the yaw authority curve on yaw), and a
full 360° takes ≈ `1/damp` of spin-up plus `2π/rate`. Fitted to stopwatch timings of the original
and then confirmed against cockpit-gauge video of it — **360° roll 2.05 s, sustained pitch ≈33 °/s,
full-rudder 360° 28.6 s** — all three within a few percent of what the values already gave.

| Constant | Value | Note |
|---|---:|---|
| `PitchTune` | **0.89** | 0.75 before C23; the weathervane explains a little over half of what it used to absorb |
| `YawTune` | **1.57** | 1.33 before C23 (and 1.32 before C21's authored yaw curve) |
| `RollTune` | **2.12** | untouched by C21/C22/C23 — the weathervane's torque is ⊥ the nose and provably cannot reach roll |

**The video also closed an open question: the original's pitch rate does NOT fall off with speed.**
Binned round a loop it reads 37.9 / 33.7 / 30.7 / 36.5 °/s over 120–280 mph — flat within the
noise — so speed-independent pitch is right, and this is the measurement behind "pitch carries no
high-speed fade" above.

The refits are **Bloodhawk-pinned**, as they always were; the other ten airframes have no measured
target of their own and simply move with them.

⚠ **Do not chase `BL-147`'s transient gap through these constants.** They set the STEADY rate,
which matches; a transient chased through them breaks the thing that currently works. The
square-wave cadence sweep is the measurement that belongs to that gap — see the C23 landing note.

## What the test suite pins, and why each test can fail

Every decoded mechanism above has an able-to-fail assertion behind it, and several of those tests
encode a specific wrong reading rather than merely re-stating the right one. Recorded here because
the tests, not the prose, are what stops a mechanism being quietly re-derived.

- **`AngularDampingTests`** — `FUN_00491820` steps 2–3. Pins the ORDERING (this tick's own torque
  is damped too, not exempted until the next tick: a decay-then-add implementation reads the same
  two lines and fails here) and the FORM. ⚠ The two forms agree to first order in `dt`, so a small
  timestep cannot separate them; the case that can is `dt · damp = 5`, past the linear form's
  stability edge at **`dt · damp = 2`**, where `(1 − dt·damp) = −4` flips the rate's sign and grows
  it every tick while `exp(−dt·damp)` stays in `(0, 1)` and only decays.
- **`WeathervaneTests`** — `FUN_00490f70`, `0x4916fe`–`0x4917f0`. Reads the torque directly, or the
  body rates after ONE step from rest where the arithmetic is closed form, so a sign flip, a missing
  halving or a leak into roll fails exactly instead of being absorbed a hundred frames later. Pins:
  it VANISHES on the flight path (exactly zero, which is what leaves level cruise untouched by
  construction rather than by scale); the sign CLOSES the misalignment (a reversed weathervane is
  divergent and still looks plausible in one frame); the magnitude is `return_rate × HALF` the
  angle; the roll component is identically zero at every bank. ⚠ And it pins that a released axis
  decays at **`ang_momentum_damp` ALONE** — folding `return_rate` back into the damping coefficient
  is the first-order lag this replaced, and the failure message prints that number alongside.
- **`KnifeEdgeTests`** — the retired sag term's own footprint is asserted gone: wings level, nose
  45° up, path on the nose, stick centred, every torque in the model is identically zero, so one
  step must not rotate the attitude at all. The bounded term keyed on `1 − |bodyUp·up|`, which is
  0.29 in exactly that attitude, so it rotated the nose down at up to **11.5 °/s** — a nose-down
  bias in every pull at any bank, filed as `BL-115`'s "knife-at-zero-bank leak". Also pinned: the
  knife-edge never settles on any of the eleven (a bounded sag puts almost none of its total in the
  last third of a 36 s hold, a genuine drift about a third), the nose stays well below the path
  (retiring `wingVert` makes the chase faster and fails it), and α stays inside `liftAOAs[0]` on
  all eleven — peak **0.71–4.29°** against the authored 5°. That last one **replaced a lost prose
  figure** ("the Balmoral knife-edges at α = 5.1°, 0.1° inside the ramp") that no instrument could
  reproduce: the Balmoral peaks at 1.77°, and the tightest airframe is the **Bloodhawk** at 4.29°,
  ≈0.71° clear. The probe recipe lives in `Probes.KnifeEdge` — it was lost once as prose and is
  code now precisely so that it cannot be again.
- **`AttitudeThrustTests`** — the 0.24 / 0.13 coefficients, and the SIGN read out of the integrator
  rather than off the formula's argument name: throttle touches only the thrust term, so
  differencing a full-throttle step against a zero-throttle step from an identical state isolates it
  to the last bit. ⚠ This is the one place in the force path where a dropped sign produces flight
  that still looks entirely plausible — it merely swaps climb for dive. The four-arrangement climb
  table above is asserted as a BOUND that separates the four, not one the shipped arrangement merely
  passes, and the probe fails the run outright if the altitude clamp binds (a clamped run measures
  the clamp, not the climb).
- **`ControlLimiterTests`** — the two limiters are decoded, authored out of reach, and deliberately
  NOT implemented; these tests are what keeps that decision honest, because they fail the moment a
  data edit, a per-plane override or a model change brings either threshold into reach. ⚠ The
  disproof carries its own able-to-fail control (`METHOD-9`): halving both authored thresholds must
  make both checks fail, otherwise the manoeuvres have gone too gentle to trip anything and the
  disproof has stopped measuring a margin.
- **`FlightEnvelopeTests`** — the Bloodhawk's flown envelope against cockpit-gauge video, as golden
  numbers ("150 → 290 mph in 3.76 s" is an invariant of a fixed artifact). The count of asserted
  scenarios is **pinned at 7** so that silently demoting one to informational cannot read as a green
  run. Three informational rows are recorded CONFLICTS rather than open questions —
  `accel-150-290` (footage vs the byte-verified force path), `sustained-turn-speed` and
  `sustained-turn-sink` (both riding the unattributed turn-rate gap C22 was expected to close and
  demonstrably does not). `terminal-dive` came BACK from that list when the attitude-thrust terms
  landed (D32): the count went 7 → 6 → 7, and a demotion is never the quiet way to make a run green.

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
3. **The two hardcoded bank constants (0.205, 0.165).** These are in no data file — they are
   developer-console variables — so no amount of data extraction would have surfaced them. Landed
   in `C22`; see "Bank coupling — resolved". ⚠ They make the banked turn **faster**, not slower,
   so they are not the explanation of the original's 1.6×-slower banked turn.
4. **The attitude-dependent thrust (0.24 / 0.13).** A video fit would absorb these into gravity
   or drag and then fail in the opposite manoeuvre.
5. **Rudder at 10 % authority in flight**, full only between 22.5 and 45 mph.
6. **Pitch authority reaching zero at 600 mph**, roll never fading.
7. **Stall speed is per-airframe** — `sqrt(2W / (0.75 ρ S))` — not a fixed fraction of `fd_speed`.
8. **`return_rate` is a weathervane torque** toward the velocity vector, not extra axis damping on
   a released stick. Landed in `C23`; see "Weathervane centring — resolved". ⚠ It reaches every
   sustained full-stick manoeuvre, because those hold a real nose/path misalignment — it is not a
   released-stick-only term in any sense.
9. **The G/AOA limiters gating only opposing input** — a subtle asymmetry that changes departure
   and recovery behaviour, not steady turns. ⚠ **Authored inert and deliberately NOT implemented**
   (`D33`): peak demand 2.13–5.01 G against `highGs[0] = 9`, peak α 8.9–25.6° against
   `maxAOA = 46°`, on all eleven airframes — see the D33 landing note above.
10. **Thrust scales with `ref_area`, not `1/veh_weight`.** The remake divides engine power by
    weight; the original multiplies it by reference area, which is what makes `RefArea` cancel
    against drag. See `ThrustFactor` above.
11. **Drag is a polar in Mach with no induced term.** Any model that makes drag rise with the pull
    is adding a mechanism the original does not have. See Drag above. The remake briefly did:
    `PLAN-flight-drag-lift` C21 fitted a `sin²α` term (`InducedDragCoef` 10.75) on 2026-08-07, two
    days before this decode, and `PLAN-flight-model-rewrite` B12 removed it with no successor when
    the decoded Mach polar replaced the fitted drag law. `FlightModel.cs` now carries the same
    no-induced-drag statement in its own comments.
12. **The throttle lever slews at 0.5/s with no idle floor** (2 s full-to-idle); the remake applies
    it instantly. Decoded, unimplemented — a feel/transient gap, not a steady-state one, and the
    one mechanism that could contaminate the first seconds of any throttle-step footage.

## Confidence

- **Read directly from the executable:** all constants and formulas above, except as marked. This
  now includes the `ThrustFactor` chain (parser → def `+0x128` → runtime `+0x66c` → force
  assembly), the linear throttle multiply and its 0.5/s slew, the thrust `pow` operands
  (`MSVCRT!_CIpow`, base `1.33·atm->k`, exponent `1.41·M`), the drag polar's variable being
  **Mach** — the last read off the raw bytes rather than out of a decompiler, which is what
  corrected it — the conversion-free weight chain ("The force scale — settled"), and the
  weathervane's axis, its half-angle and
  its player-only gate ("Weathervane centring — resolved", which corrects a sign this document
  previously carried).
- **Read directly from the executable, 2026-08-15 (A1/A2):** that the atmosphere call is on the
  shared live path with no player/AI branch and no altitude zeroing, that the altitude-zeroing site
  belongs to the debug copy behind a dialog flag, that the band threshold `0x71bb3c` has one read
  reference and no writer in the whole image, that the throttle lever and its slew are unbranched,
  and the far-field cruise model behind `fd_speed · throttle`.
- **Recorded conflict, not an open decode question:** the band threshold reads `0.0`, which would
  put every airborne aircraft on the thin band, against an authored data set and a stall/equilibrium
  arithmetic that only work on the dense band. Shared by player and AI; settleable only in a live
  process.
- **Recorded conflict, not an open decode question:** the absolute force scale below cruise —
  `CAP-05`'s zero-thrust points read the polar ≈2–3.6× too strong (a constant-ΔC_D deficit), the
  weight chain is byte-verified conversion-free, and the footage's own accel row rejects any
  uniform rescale. See "The force scale — settled".
- **Verified by arithmetic:** the dense atmosphere band, via the stall-speed check against the
  parser's fallback aircraft **and** via the level-equilibrium solve (the thin band misses by ~4×)
  — now also proven from the bytes for the player path; `ThrustFactor` = engine power, via a
  perfect rank correlation with `fd_speed` across all eleven player airframes and an absolute
  solve landing nine of eleven inside 1 %.
- **Resolved 2026-08-14 (was "untested"):** the shipped Dynamics tuner **dialog** is dead code, with
  no callers; the measurement harness behind it is reachable from the world tick behind five debug
  flags and prints rather than writing `dynamics.txt`.
- **Read directly from the executable, 2026-08-14:** the ground-blow probe, its falloff, its axis and
  both application paths; the `bounce_factor` impulse and its doubled rotational term; the two
  per-object timers; the AI mode enum; the live-versus-debug integrator split. World units are
  metres, identified positively from `nom_gravity`'s 9.82 reference divisor rather than from the
  absence of a conversion.
- **Not determined:** which registry entities keep the `+0xcc` emitter flag set permanently, so the
  GDD's naming of zeppelins as ground-blow emitters is neither confirmed nor refuted; the data-file
  token naming each `obj+0x67c` vehicle class; which axes the reverse-authority factor reaches.
