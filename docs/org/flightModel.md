# The original flight model, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, 2 580 480 bytes, `language x86:LE:32:default`), 2026-08-09, extended 2026-08-14 with
ground blow, the collision impulse, and the live-versus-debug integrator correction. **This is the
authority for every flight quantity.** It superseded a set of figures reconstructed from cockpit-gauge
video, and that whole route is retired: footage cannot confirm a decode, it only ranks readings
(`docs/verification.md` DET-12). The reconstruction and the scripts behind it were deleted and are
recoverable with `git log -p`; treat any number sourced from them as withdrawn.

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
| `FUN_0041c270` | The AI brain, a sibling of the dispatch in the same tick. Writes the stick; see [aiControlLaw.md](aiControlLaw.md) |
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
debug-copy citation, and with it went the supposed player-versus-AI density divergence. **C22
(2026-08-15) mapped three more,** the ones it had to port: the player/AI guard on the airflow blend
(`0x48c520`), on the weathervane (`0x48cd3e`, the live twin of `0x4916fe`) and on the forward-speed
floor (`0x48e925`, in `FUN_0048e580`). The weathervane's is the only one this document had ever
quoted, and it quoted the debug copy's.

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

**C22 (2026-08-15) read the LIVE site and ported it.** It is in `FUN_0048e580`, not in the debug
integrator: `cmp edi, [0x71c298]` at `0x48e925` skips the block for the player, and the block itself
runs `0x48e95e`–`0x48e998`. It compares the velocity's **`m[2]` component against the negated
constant at `0x608128` (`−4.4704`)** and, when that component is greater, adds
`(−4.4704 − v·m[2]) · m[2]`. `m[2]` is `−nose`, so both signs flip and the effect is "raise `v·nose`
to 4.4704 by adding along the nose". Three properties follow from the arithmetic and are easy to get
wrong: it is **one-sided** (it never slows anything), it **adds along one axis** rather than
rescaling, so the perpendicular components survive untouched, and it is **not a floor on speed** —
it sits before the velocity is stored to `obj+0x924` and `|v|` recomputed into `obj+0x934`, so the
speed that comes out is the length of the floored vector. A plane descending at 20 m/s with its nose
on the horizon has four times the floor in SPEED and none of it along the nose, and the original
pushes it forward.

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

## A destroyed hull flies the same model

⚠ **Nothing in the flight path is gated on death.** `FUN_0048e580` (the aircraft integrator),
`FUN_0048c470` (the force and torque build) and `FUN_0048fc40` (thrust, drag, lift, gravity) do not
read `+0x91d` or `+0x91f` at any instruction: a program-wide scan finds 100+ sites reading `+0x91d`
and 39 reading `+0x91f`, and none of them lies inside those three functions. The only death test on
the movement path is the run-or-skip gate itself, `FUN_00489ea0` at `0x00489ea3` / `0x00489ead`, and
a vehicle that passes it dispatches the unmodified integrator for mode class 0 and 4. No lift term
is dropped, no drag term is added, no coefficient is swapped, and no ballistic path exists. Neither
does the death function `FUN_004b82d0` zero the throttle command or the control-surface
deflections; those are AI-written state (`FUN_0041b560`, `FUN_004209b0` slew `+0x124`) and the AI
think is what stops, so they **freeze at their last commanded values**.

Two state flags do change the force build, and death sets neither:

| Flag | Tested at | What it does | Set by |
|---|---|---|---|
| `+0x384` | `0x0048c4ba` | swaps the whole aerodynamic build for a velocity-match to `fd_speed · throttle` along the nose, the same arm any non-player over 1000 units from the player takes | `FUN_0043d640`, `FUN_004735b0`, `FUN_004aff80` |
| `+0x2dc` bit `0x2` | `0x0048fdd0` | engine out: **thrust alone** goes to zero (`local_c` at `LAB_0048fdf3`); lift and drag are untouched | `FUN_004b1690` (the systems-damage setter), `FUN_004aff80` and `FUN_004b40c0` at spawn/reset |

So "a dead engine means no lift and high drag" is refuted twice over: there is no engine-out state
on the death path, and the real engine-out bit only removes thrust. `FUN_0048ad20` is a red
herring, confirmed: it is the terrain-conform update for surface vehicles, reached from
`FUN_00489ea0`'s class 2/3/5 arms and never for an aeroplane.

**What falls, then, is the anim.** The dead hull holds altitude and travels, for the three seconds
until `Callback 15` releases it; `randomdestseq`'s `ObjectMotion` (gravity −9.8, `impact_force`) is
what flies it down (`docs/org/vehicleDamage.md`, "A dead aircraft keeps flying itself until
`Callback 15`").

**The commands freeze; they are not neutralised.** What death stops is the AI think and the weapon
loop, so nothing writes the command vector and the integrator keeps reading its last value.
`FlightController.StepWreckFall` steps the model with `_lastInput` for that reason. A default
`FlightInput` there would fly the wreck on zero throttle and neutral surfaces, which is an
invention: the original has no neutralising step on the death path.

⚠ **Do not tune this against the recordings' downrange.** Freezing measures 323 m downrange and
−8 m of altitude on a headless kill, where neutralising measured 175 m and +1 m, and 175 m is the
figure a reference recording gave (`docs/plans/PLAN-ai-damage-and-engine-audio.md`, D21). That agreement
is not evidence for neutralising. It is a footage-derived distance, the class of measurement that
has failed here repeatedly and may not contest a decode, and the magnitude under freezing is a
function of **our** AI's last throttle rather than the original's, so neither number tests the
mechanism. If the downrange reads wrong at the controls, the open question is what throttle an AI
carries into its death (`BL-414`), not whether to reinstate a neutraliser the original never had.

⚠ **Decoded for an AI, assumed for a human.** What `FUN_004897c0` skips on death is the AI think
(`FUN_0041f810`/`FUN_0041c270`) and the weapon loop, so an AI's commands demonstrably stop being
written. Whether the original also stops reading a HUMAN's stick on death is not decoded, and
`StepWreckFall` serves both, so a dead player's hull flies the last stick position its pilot held.
The stakes are lower than they look, since `player-player` breaks the hull into four separately
flown pieces rather than falling as one, but a player killed holding full deflection is the case to
watch.

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

**C22 (2026-08-15) located the LIVE guard and ported it.** In `FUN_0048c470`, `cmp esi, ecx` at
`0x48c520` (the player object loaded into `ecx` at `0x48c502`) jumps to `0x48c6e9` for anything that
is not the player, and `0x48c6e9`–`0x48c70a` builds `−speed · m[2]` — `m[2]` being `−nose`, that is
`speed · nose` — with no reference to the window constants `_DAT_0071c430`/`_DAT_0071c434` the
player branch reads. So it really is a skip of the whole block, not a saturated blend.
⚠ **The sign of the resulting difference is not fixed, and "the AI pulls harder" is wrong.** The
demand is `lift_accel_rate · (relativeWind − velocity)` **plus weight**, so at a climbing flight path
the fully-nose-aligned swing points down and partially cancels the weight term: measured on the
Bloodhawk at α = 8° with the path above the nose, the AI's demanded load factor is **0.26 G against
the player's 2.04 G**. Past `liftAOAs[1]` the two agree exactly, because there the player is
nose-aligned as well.

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
divisor — is **disproven at source**: `veh_weight` runs raw end to end. The decode stands and the
residual is not a missing term: the footage figures it disagrees with are frame-derived and cannot
refute a decode (`docs/verification.md` DET-12).

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

**That settles the CAP-05 residual as a property of the footage, not a units bug in the model.**
The decode is authoritative here (`docs/verification.md` DET-12); the deficit is recorded because
its structure is interesting, not because a constant is owed. No constant can close it anyway:

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
warned not to read as validation, and it does not extend to the Bloodhawk's real numbers. **The
computed stall stands; the clip's ~76 mph does not contest it** — a frame-derived speed ranks
readings, it does not confirm or refute a decode (`docs/verification.md` DET-12). Not resolved by
switching G-conventions to fit one clip.

## The two stall cues — a lamp and a nose-drop, on two unrelated thresholds

Both cues are measured on the same margin (`Speed / fd_speed`), and their thresholds are
**deliberately different numbers**, neither of them a tuning constant:

- **The nose-drop** fires below the airframe's own computed stall speed — the decoded
  `clMax · q · RefArea = Weight` solve above. **Decoded.**
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

### The nose-drop's rate is `stall_mag`, and it is a TORQUE

The original's nose-drop is the last block of `FUN_0048c470`, behind the player-only guard. The stall
flag is written to the out-parameter as `1 − L(9°)/Weight`, and where it is positive the block builds
`axis = nose × worldUp` (`DAT_006379c0` reads `(0, 1, 0)`, confirmed from memory), cancels whatever
accumulated torque already opposes that axis, and adds along it:

```
Δ(torque accumulator) += axis · (stall_mag · stallFlag · dt)      [dt multiply at 0x48d11b]
```

`stall_mag` is `_DAT_0071c41c`, authored from the `stall_mag` token (string at `0x627854`, parsed at
`0x4742de`-`0x4742fe`), compiled fallback **0.45** (the immediate `0x3ee66666` stored at `0x4742fe`,
on the branch the parser takes only when the token is ABSENT). There is **no second rate constant**:
the one scalar is the whole of it. It is also a **single global, not a per-airframe field**: the one
write pair is in the player-globals parser `FUN_004735b0` and the force path reads the global
directly at `0x48d10d`, so no plane record can carry its own.

⚠ **This install authors 1.25, so the fallback is the wrong number to reason with** (corrections
table below). Every figure here is the authored value's.

Three consequences, and they are what separate this from the remake's version:

- **It enters the torque accumulator, not the attitude.** So it is scaled by `rec_moments_inertia`
  and damped by `ang_momentum_damp` downstream, exactly as "Torques and the limiters" warns for
  anything else entering that accumulator. On the Bloodhawk (`rec_moments_inertia.x` 1.18,
  `ang_momentum_damp` 5.0) full stall depth settles at `1.25 · 1.18 / 5.0` ≈ **0.295 rad/s** — under
  the fallback it would be `0.45 · 1.18 / 5.0` ≈ 0.106, and either way an order of magnitude under a
  direct rotation at the scalar itself.
- **The cancellation is what holds the nose down, not the magnitude.** Full-elevator pitch authority
  on the same airframe is ≈0.585 rad/s (`pitch-rate` 33.54 °/s), well over the deepest stall torque,
  so a straight contest would go to the elevator. There is no contest: the `FCOMP` at `0x48d0ce`
  tests the accumulated torque against the axis and, where it opposes, the projection
  `−(t·a)/(a·a)` removes that component **entirely** before the drop is added. A pull along the drop
  axis is deleted, not outvoted, so the nose cannot be raised while the flag is positive. That is
  the original's own version of "no raising the nose over the horizon in a stall", and it needs no
  separate cap, floor or minimum rate on top of it.
- **The axis is not normalised**, so the rate carries a `cos(nose elevation)` factor and falls away
  as the nose leaves the horizontal. The nose is pushed down about a horizontal axis; it does not
  chase world-down, and a bounded nose-down attitude is an equilibrium between this torque and the
  restoring terms rather than the end of a chase.
- **The depth measure is the lift-versus-weight flag**, `1 − L(9°)/Weight`, not a speed ratio.

**This is what the remake flies.** `FlightModel.StallNoseTorque` builds the term above and folds it
into the same accumulator the stick, the bank coupling and the weathervane feed, with the sign test
and the projection intact; `FlightModel.StallFlag` is the `1 − L(9°)/Weight` depth, which is the
already-computed lift cap subtracted from one. The `StallNoseRate` multiplier, the attitude rotation
it scaled and the hand-rolled over-the-horizon cap that used to follow it are all retired: the first
was a no-op on `stall_mag`, and the cancellation supersedes the third. `StallNoseDropTests` pins the
closed form, the equilibrium and the cancelled pull.

## `lift_accel_rate` is a lag toward a target velocity

The nose-chase is real and authored: `lift_accel_rate` (string `0x6278b4`, global `_DAT_0071c448`,
compiled fallback `0x3f99999a` = **1.2**, this install authoring **0.75**) has two readers,
`0x48c746` in the force build `FUN_0048c470` and `0x49112a` in `FUN_00490f70`, and both are the same
block:

```
a  = lift_accel_rate · (targetVelocity − velocity)     [0x48c70d-0x48c776]
a.y += gravity                                         [0x48c77b]
a  = a · orientation                                   [into body axes, 0x48c78a…]
```

`FUN_0048c470` splits at `0x48c522` on whether the object is the player (`ESI` against
`_DAT_0071c298`) and the two sides differ only in how they build `targetVelocity` — the player
accumulating `scalar × direction` through three virtual calls (`0x48c6b6`–`0x48c6e7`), everything
else taking `−speed × nose` (`0x48c6e9`–`0x48c704`). They rejoin at `0x48c70a`, so the lag itself is
unconditional.

⚠ **Nothing at either reader touches bank or wing verticality.** The remake's `KnifeAlignFloor`
weakened this chase by `|bodyUp·up|`; the binary has no such factor, so the constant and its
`wingVert` input are retired and the chase runs at the authored rate alone. That the knife-edge
nose–path gap then reads smaller than the footage's is a decode-versus-footage conflict of the same
class as `yaw-360` and `decel-290-150`, recorded rather than tuned away.

⚠ **The remake spends this vector differently, and that is undecoded work, not a landed match.**
`FlightModel.Step` uses `(relativeWind − velocity) · LiftAccelRate` with gravity on Y as the lift
DEMAND — deriving a load factor from it, clamping it, projecting it on the wing plane and adding
thrust, drag and gravity separately — where the original uses the same shape AS the acceleration.
`BL-438` owns reconciling the two, and the three target-velocity contributions above are the first
thing it needs.

## The keyboard stick is an accumulator, not a switch (`FUN_00487460`)

The player input handler runs once per frame from the tick function `FUN_004897c0`, which sets the
flight model's `dt` (`DAT_0071c56c`) from the same frame delta (`DAT_009ad744`) the handler ramps
with — input and flight share one clock. Each of the three stick axes is a stored deflection the
keys move, not a flag the keys set:

```
axis = 0                                     ; if the key is released, or the command opposes
                                             ;   the current sign — pitch: 0x48786a
axis += dt · dir · 2.5                       ; while a key is held — pitch: 0x487880-0x48788f,
                                             ;   the 2.5 at 0x006040a8
axis = clamp(axis, -1, +1)
```

Pitch is `obj+0x108`, roll `obj+0x100`, yaw `obj+0x10c`, all three the identical block (roll at
`0x48794c`, yaw at `0x487a18`); the throttle follows at `0x487a54`/`0x487a7d` with the same shape
and its own `±0.5·dt`. The three are then copied to the slots the force path reads — `obj+0x114`
roll, `obj+0x11c` pitch, `obj+0x120` yaw — through `FUN_00460890`, **which is an identity stub**:
its `0.5`/`1.0` arguments are dead, so no expo curve exists on the way out.

**The rule, and its asymmetry.** A held key takes **0.4 sim s** to reach full deflection; a released
or reversed key drops the axis to centre in a single frame. Gradual on, instant off. An analogue
axis bypasses the ramp entirely — the joystick path assigns its scaled value to the same slot and
suppresses that frame's zero-snap, so a stick's deflection is absolute where a key's is accumulated.

**What it settles.** A tap never reaches the deflection its key nominally commands: 115 ms of press
is `2.5 × 0.115` = **0.29** of full travel. The original's fast pitch cadences therefore fly a
much smaller stick than its slow ones, which is roll-off produced in the input stage before any
aerodynamics are involved — see the landing note below.

**Landing note — it closes about a third of the residual roll-off, and a 1.57× gap survives it.**
Ported as `StickRamp`, applied to the keyboard axes in `FlightController.ReadKeyboard` (the
gamepad's analogue axes add on top, unramped, matching the joystick path). Driving `ZzCadenceSweep`
through the ramp moves the 1300 → 570 ms roll-off from **20.5× to 26.8×** against the original's
**42×**, so the deficit falls from 2.05× to **1.57×**. That remainder is outside the ±20% amplitude
systematics and the ±12% spread in the clips' mean airspeed, so it is a real difference and not
measurement slack; the pitch transient nonetheless reads right at the controls, which is why no
constant is chased for it. Two candidates have never been examined: the `liftAOAs` airflow blend
under a rapidly reversing demand, and the possibility that the original's 570 ms point
(a 4.9× drop from 700 ms over a 1.23 frequency ratio) is a resonance rather than a point on a smooth
roll-off, which no monotone transfer function produces and which the corpus cannot separate from
noise at 0.63 ± 0.13 ft.
⚠ **Quote the sweep's WALL reading, not its sim reading.** The macro drove the keys in wall
milliseconds, so the period the game saw is that × 1.390 (`docs/verification.md` DET-11); the sim
column answers a question nobody flew. It used to be defensible to quote either, because with a
square wave on both sides the ratio barely moved between them — a rate limit destroys that, since
2.5/s is an absolute timescale that does not rescale with the cadence. The sim column reads 36.4×
for the same run.
⚠ **The 23.3× the C23 note above quotes is neither today's baseline nor the right column** — the
unramped model reads 20.5× on the current build, and ratios are comparable only within one run.

## Control authority vs speed

`FUN_0048bdd0` derives three independent scalars from airspeed alone, reading nine globals at
`0x0071c3f8`-`0x0071c418`. (The debug copy `FUN_00490e10`, which this section used to name, reads
the same nine and computes the same curves.) Fallback values shown as authored (MPH):

- **Base ramp** `f` — 0 below `turn_fade_in` (**10**), rising linearly to 1 at `turn_fade_out`
  (**40**), then held at 1.
- **Roll authority** = `f`. **No high-speed fade** — roll never degrades with speed.
- **Pitch authority** = `f`, then faded by `high_speed_pitch_fade`: full until **500**, falling
  linearly to **zero at 600 mph**. The fallbacks are the immediates at `0x474179` / `0x474183`, and
  the authored path parses the `high_speed_pitch_fade` token (string at `0x6277f8`) at
  `0x4741f2`-`0x474231`, each field converted from MPH.
  ⚠ **Pitch and roll do NOT share one curve, though the remake's `RollPitchAuthorityAt` treats them
  as if they did.** The second stage is pitch-only. It is invisible on the fallback numbers, since
  500 mph is above every airframe's `fd_speed`, which is also why the video reads pitch rate as flat
  with speed. An install that authors a lower `high_speed_pitch_fade` would bind it, and ours would
  not follow.
- **Yaw authority** — piecewise, and deliberately **not monotone**:

  | Speed (mph) | Authority |
  |---|---|
  | ≤ `yaw_fade_in` (10) | `yaw_low_speed` = **0.05** |
  | 10 → `yaw_max` (22.5) | ramps 0.05 → **1.0** |
  | 22.5 → `yaw_fade_out` (45) | ramps 1.0 → `yaw_high_speed` = **0.1** |
  | ≥ 45 | **0.1** |

  The rudder is at full authority only in a 22.5–45 mph window and sits at **10 %** for all of
  normal flight. It is a ground-handling and low-speed control, not a flight control.

- **Reverse-authority factor** (decoded 2026-08-14; its consumer traced 2026-08-15).
  `FUN_0048bdd0` has a fifth output the debug copy lacks: above `yaw_max` (`0x0071c414`, authored
  **50 mph**) it returns `max(yawAuthority, 0.2)`, and **1.0** at or below it
  (`0x48bf16`–`0x48bf58`; the 0.2 is the immediate at `0x6034fc`). Because `yaw_max` is the speed at
  which the yaw curve peaks, it engages across the whole of normal flight: it tracks the declining
  yaw curve from 1.0 at 50 mph down to the **0.2** floor, which the curve reaches at ≈345 mph. At
  the Bloodhawk's 302 mph cruise it is ≈0.40.
  ⚠ **It is not a force term, and the "softens control forces that oppose the current velocity
  vector" this entry used to carry was a misattribution.** `FUN_0048c470` takes it as an
  out-parameter, writes it once and never reads it again; its own opposing-command softening is a
  DIFFERENT, local quantity (see "Torques and the limiters" below). The factor is passed out to
  `FUN_0048c470`'s only caller, `FUN_0048e580`, whose single use of it is at
  `0x48ec0b`–`0x48ec1d`: `rudderAngle = factor · yawInput · −0.61086524` rad (**−35°**, the
  immediate at `0x608120`), exponentially smoothed toward that target at 2/s by `FUN_00460490` into
  the angle slots `obj+0x634`/`+0x638`, which `FUN_004b2fe0` applies as a node rotation to the two
  rudder node lists at `obj+0xa04`/`+0xa14`. **It scales the VISIBLE rudder deflection, on the
  player's aircraft only, and touches no torque.** Ported at that site (`ControlSurfaceAnimator`),
  not in the force path.

### The original's control-surface animation, decoded in passing

The same block in `FUN_0048e580` (`0x48eaf0`–`0x48ec92`, inside a `piVar3 == DAT_0071c298`
player-only guard) drives **six** angle slots off three stick channels — `obj+0x100` (`a`),
`obj+0x108` (`b`) and `obj+0x10c` (yaw, the one the reverse-authority factor scales). Each slot is
smoothed exponentially toward its target at 2/s by `FUN_00460490`, and `FUN_004b2f00` /
`FUN_004b2f70` / `FUN_004b2fe0` apply the six as node rotations over six node lists:

| Slot | Node list | Target angle | Clamp |
|---|---|---|---|
| `+0x63c` / `+0x640` | `+0x9c4` / `+0x9d4` | `−0.5·a` / `+0.5·a` | ±0.5 rad (28.6°) |
| `+0x62c` / `+0x630` | `+0x9e4` / `+0x9f4` | `−0.6·b − 0.18·a` / `−0.6·b + 0.18·a` | ±0.6 rad (34.4°) |
| `+0x634` / `+0x638` | `+0xa04` / `+0xa14` | `−0.61086524 · yaw · reverseAuthority` | none (−35° at full) |

Two things fall out of this that are worth having even though the surfaces themselves are cosmetic.
The second pair MIXES two channels — a common-mode `b` term with a differential `a` term — so those
surfaces are not driven by one axis each. And the whole block sits behind the player guard, so **the
original's AI aircraft fly with frozen control surfaces**; ours deflect on every aircraft, a
deliberate divergence rather than an unported guard.
⚠ Which physical surface each node list holds is NOT decoded — only `FUN_004d1a30`'s rotation axis
per list is visible here, and the `a`/`b` channels were not traced back to the pitch and roll
inputs. Do not map this table onto aileron/elevator names without reading the list population.
NOT ported beyond the reverse-authority scale: our `ControlSurfaceAnimator` classifies four surface
kinds rather than six node lists, and its ±20° angles were validated by eye (`backlog.md`
`BL-393`).

### The low-speed ramp — implemented 2026-08-15 (closing `BL-330`)

The base ramp `f` was the one part of `FUN_0048bdd0` the remake did not carry: `FlightModel.Step`
applied the authored yaw curve and **no speed term at all** to pitch or roll, so both held full
authority down to zero airspeed. It is now `FlightModel.RollPitchAuthorityAt`, on the authored
`turn_fade_in` 10 / `turn_fade_out` 50 mph (the executable's fallback `turn_fade_out` is 40), a
scalar on the pitch and roll components of the stick command.

Three properties of the port, all read off `FUN_0048bdd0` and `FUN_0048c470` rather than assumed:

- **It scales the STICK COMMAND only.** `FUN_0048c470` multiplies the three authority scalars inside
  its three per-axis input blocks (`obj+0x114` roll, `obj+0x11c` pitch, `obj+0x120` yaw) and nowhere
  else, so the bank coupling, the weathervane and the ground blow enter the same accumulator at full
  strength. A slow aeroplane loses its controls and keeps the coupling.
- **The boundary is exclusive at the bottom.** `0x48bdd4` tests `speed > turn_fade_in`, so authority
  is exactly 0 *at* 10 mph, not merely small.
- **Pitch and roll take the SAME scalar.** `0x48be20` writes it to the pitch output and `0x48be6c`
  to the roll output; the pitch output is then multiplied by `high_speed_pitch_fade`
  (`0x48be22`–`0x48be68`), which this install authors at [1000, 1001] mph and is unreachable.

Where 50 mph falls decides how visible this is, and it differs by airframe: nine of the eleven stall
at 52–57 mph, i.e. *above* the ramp's top, so for them the fade bites only once already stalling;
the Balmoral (45.5) reaches its stall at ≈89 % authority, and the autogyro (18.5) flies a long way
inside the ramp and stalls at roughly **21 %** of roll and pitch authority.

⚠ **"Roll never fades" above means never with HIGH speed.** Read as "roll authority is
speed-independent" it becomes the misreading that had `turn_fade_in`/`turn_fade_out` filed as a
candidate *bank* effect through four consecutive items — see the CORRECTION at the end of
"Bank-independent lift vs the measured knife-edge sag". The ramp is keyed on airspeed alone and is
saturated across the whole regime in which the footage reads a 1.6×-slower banked rotation.

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

**CORRECTION (from `FUN_0048c470` directly).** The sign test is **not** against the existing
angular momentum. `FUN_0048c470` builds `unit(nose × v̂)` — the same closing axis the weathervane
uses — and compares the sign of the commanded torque against the sign of that axis' component on the
axis being commanded (`0x48ca7a` onward, the pitch and yaw blocks only; roll has no such test). So
what is softened is a command that swings the nose FURTHER off the flight path. The combined scalar
is also computed once and shared by both axes, and the AOA half of it is literally
`(cos α − maxAOACos) / (1 − maxAOACos)` on that same α, which is why it reads as a limiter and an
alignment window at once. None of this changes the unreachability finding — both halves are authored
out of reach on all eleven airframes (`ControlLimiterTests`) — and nothing is implemented.
⚠ This is also the quantity the reverse-authority factor was wrongly identified with; see that
bullet above.

`return_rate` is a separate centring torque, described below.

⚠ **`rec_moments_inertia` is applied downstream, not here.** `FUN_00490f70` accumulates a plain
`torque · input · dt · authority` world-frame vector; the integrator then rotates it into body
axes, scales each by `rec_moments_inertia` (`0x4918b9`–`0x491932`) and rotates it back. The
formula above folds the two steps together, which is exact — but it matters for anything else
that enters the same accumulator, because that too is scaled by the reciprocal inertia and damped
by `ang_momentum_damp`. The bank coupling below is exactly such a term.

Both are per-airframe fields of the plane record, parsed in `FUN_00479240`: `ang_momentum_damp`
(token at `0x627fdc`) into `+0x114` at `0x47add3`, and `rec_moments_inertia` (token at `0x628020`)
into `+0x118`/`+0x11c`/`+0x120` from list elements 0/1/2 at `0x47ae69`/`0x47ae75`/`0x47ae81` —
pitch, yaw, roll in that order. ⚠ **Neither has a compiled fallback**: each is a single store on
the token-present branch with no else, unlike `stall_mag`'s. `PlaneStats`' 5.0 and (0.8, 0.6, 1.3)
are therefore not the executable's defaults for these two, and an airframe that authored neither
would fly on whatever the record was initialised to; in practice every airframe authors both.

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
⚠ **It does NOT close the sustained-turn-rate difference, and it moves it the wrong way**: 32.35 →
34.71 °/s against the 18.95 measured off `CAP-01`, and up on ten of eleven airframes. Both terms
*add* heading rate in the direction of bank, so the plan's expectation that this item would explain
the footage's slower banked turn is disproved at the mechanism. **No authored field is a candidate
for the remaining difference either**: `highGs` is measured inert, and `turn_fade_*` is this
document's own base ramp, keyed on airspeed alone and saturated at 1 above 50 mph (see "Control
authority vs speed" and the correction note at the end of this section). The rotation path is
decoded; what disagrees with it is a frame measurement, which does not outrank one.
The one place it clearly improves fidelity is the **knife-edge**: a neutral-stick 90° bank held
for 35 s used to pin the nose at the bounded −4.01° sag and settle (−316 m); it now drifts
linearly at ≈1.08 °/s to −41.8° with no equilibrium (−1993 m), which is the *shape*
`BL-247` measured on the original (0.69–0.89 °/s to −27° over 36 s) and could not previously be
produced at all. Recorded for `D31`, not acted on — the drift is ≈1.2–1.6× the frame-measured rate,
which is a magnitude difference against video where it used to be a missing mechanism.

## Weathervane centring — resolved, and the summary line above was wrong

`return_rate` is the last contribution `FUN_00490f70` makes to the angular accumulator, at
`0x4916fe`–`0x4917f0`, immediately after the bank coupling. It is guarded twice:

```
cmp esi, [0x71c298]        ; 0x4916fe — the PLAYER object. AI skips the whole block.
fld [ebp-0x10]; fcomp 0    ; 0x49170a — speed ([obj+0x934], the true |v|) must be > 0
```

**C22 (2026-08-15): the LIVE copy of this guard is `cmp esi, [0x71c298]` at `0x48cd3e` in
`FUN_0048c470`**, jumping past the whole block to `0x48ce45`. It is guarded three times there rather
than twice — player, `[obj+0x384]` (the crashed flag) clear at `0x48cd4a`, and speed > 0 — and the
block is `0x48cd3e`–`0x48ce45`, reading `return_rate` from `[obj+0x654]`. The addresses in the
listing above are the debug copy's and remain re-checkable there; this is the one the game runs, and
it is the guard `FlightModel.UsesAiForcePath` now stands for.

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

- **It moves the pitch transient the right way and does not account for all of it.** The square-wave
  pitch-cadence sweep run through our own build (same input, same estimator, so no transfer function
  is assumed on either side) falls **19.6×** between the 1300 ms and 570 ms cadences before the
  change and **23.3×** after, against the original's **42×** — about a sixth of the gap, on the sim
  reading the sweep then quoted. A second-order response is part of the answer and demonstrably not
  the whole of it; the stick ramp is most of the rest.
- ⚠ **The sweep also refutes a "3.5× steeper than any single first-order lag permits" reading of
  the original's roll-off.** That reasoning assumed the chain is *double integration + one lag*. The
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

## Bank-independent lift vs the measured knife-edge sag — reconciled

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

The whole measured trajectory, from both takes at 143 and 300 mph — they agree to ~13%, so the
knife-edge is driven by time-since-roll-in rather than by airspeed:

| time since roll-in | nose | path | sink |
|---|---|---|---|
| 0–3 s | −4° step, then drifting | ≈0° | **0.5 ft/sim-s — genuinely holds altitude** |
| +12 s | −12.0° | −6.0° | 24 ft/sim-s |
| +24 s | −20.0° | −12.8° | 60 ft/sim-s |
| +36 s | −27.0° | −18.7° | 93 ft/sim-s, still steepening |

The sag is an immediate ≈4° step followed by an unbounded drift of 0.69–0.89 °/sim-s, which is why
the bounded pair is retired rather than retuned: a bounded term cannot produce a drift that never
settles.

⚠ **Do not back an absolute align rate out of this table**, for the retired `KnifeAlignFloor` or for
anything replacing it. The observable here is the path lagging the nose (4.8° at +3 s, 7.2° at +24 s, 8.3° at +36 s), and `CAP-05` cannot separate
that lag from gravity pulling the path down over the same interval. What the A/B above uses is only
the *direction* each row moves under a change on one build, which the confound cannot reverse:
gravity pulling the path down can only shrink the gap, so the inferred chase is an upper bound
either way.

It was also never really a knife-edge term: it keyed on `1 − |bodyUp·up|`, which is 0.29 at a 45°
nose-up attitude with the wings dead level, so it fought every pull at up to 11.5 °/s. Removing it
moves `zoom-climb` toward its measured 936 ft on **all eleven** airframes (Bloodhawk 1396 → 1338 ft,
Balmoral 3935 → 1791) and lets the Balmoral reach the altitude cap at all (4471 → 6572 ft).

**⚠ RETIRED (2026-08-24): `wingVert` is gone, and the reading below is superseded.** It survived on
the argument that the chase rate had no counterpart in the original's force path, so only footage
could speak to it. That argument was wrong: the chase IS in the force path — `lift_accel_rate`,
decoded above — and neither of its two readers scales by bank or verticality. The constant and its
input are removed, and the footage comparison in this paragraph is kept only as the record of what
was believed. Its own numbers: the original holds its nose 4.8° → 8.3°
**below** its flight path across the
36 s, a gap that grows. Ours runs 2.9° → 1.2°; with `wingVert` retired (chase floor 1.0) it
collapses to 1.9° → 0.5° and the 36 s altitude loss rises 1087 → 1334 m. Every knife-edge
observable moves the wrong way without it. Lowering the floor instead of removing it moves every
row toward the footage (at 0.10: gap 3.5° → 2.1°, drift 0.96 °/s, 874 m) and still cannot reach it
— and it walks the knife-edge α up to 5.36°, past `liftAOAs[0] = 5°`, where the airflow blend
starts engaging in a knife-edge.

**What the chase now reads at, with the constant gone.** The knife-edge α peak falls rather than
rises — Bloodhawk 4.28° → 2.59° at the 143 mph entry — so the `liftAOAs[0]` boundary that argued
against lowering the floor is further away than it ever was, not nearer. Drift holds at 1.19–1.21 °/s
and the last-third share at 0.20–0.21, so the knife-edge still never settles; `KnifeEdgeTests` pins
both on all eleven. The nose–path gap does shrink, which is the footage difference this section used
to weigh, and it is now recorded as a conflict rather than closed.

**The banked rotation runs ≈1.6× the footage rate, and that is a note, not a gap.** Nose drift
1.09 °/s against a frame-measured 0.69–0.89, heading 1.7 °/s against 0.68–1.13, and
`sustained-turn-rate` 32.8 against `CAP-01`'s 18.95 — two independent manoeuvres, two different
body axes, one ratio. Every number on the original's side of that comparison is frame-measured off
video; the rotation that produces our side is decoded from the force path (the torques, the
limiters, the bank coupling, the weathervane and the airspeed authority ramp all sit in this
document). A decode is not corrected by a footage measurement, and a single ratio across two
manoeuvres and two axes is the signature of a common factor in how the footage was read rather than
of a force term that would have to reach both. Nothing is tuned to close it.

⚠ **Do not reintroduce a nose-sag term to deepen the knife-edge.** The decoded bank→yaw coupling
already drops the nose there, and the weathervane then pulls it onto the falling path; a second
nose-down term double-counts what is already present and re-creates the wings-level leak above (the
retired term rotated the nose down at up to 11.5 °/s in a plain 45° pull).
⚠ **Do not reintroduce a scale on the nose-chase to close the footage difference either.**
`KnifeAlignFloor` was exactly that and it is retired: the decode's two `lift_accel_rate` readers
carry no such factor. What separates the two sides is the ROTATION rate — the ≈1.6× above — and a
scale on the chase constant would hide a rotation rate inside it. The retired constant also ran into
a real boundary at 0.10, where the knife-edge α reached 5.36° and crossed
`liftAOAs[0] = 5°`.

⚠ **CORRECTION (2026-08-09): the authored candidates are exhausted, and this document said
otherwise for four items running.** C22, C23, D31 and D33 each parked this gap on "the unconsumed
`turn_fade_in`/`turn_fade_out`/`highGs` are the only authored fields shaped like it".
Both halves are now false. `highGs` is measured inert on every airframe. And `turn_fade_*` is
**already decoded in this very document** — "Control authority vs speed" above: `FUN_00490e10`'s
base ramp is a function of **airspeed alone**, 0 at `turn_fade_in` (10) rising to 1 at
`turn_fade_out` (50 authored), **held at 1 above that**, scaling roll and pitch authority with no
bank or load-factor term anywhere in it. The banked turn settles at 222–260 mph and the knife-edge
takes are at 143 and 300, so the ramp is saturated across the whole regime where the 1.6× appears
and cannot be its cause. The ramp is a real low-speed behaviour (`BL-330`, implemented 2026-08-15 by
C24) — it is simply not this, and landing it moved no row of the envelope suite. **What is left over
is not a decode question**: the rotation path is decoded, and the only thing on the other side of the
comparison is frame-measured video. Recorded rather than quietly re-pointed: a difference that was
attributed to the same three fields four times is exactly the kind of inherited claim that stops
being re-checked.

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
   does not get it (live guard `0x48cd3e`, ported in `C22`). Fully recovered under "Weathervane
   centring — resolved" above, where the `−nose` this line used to read is corrected.

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
so its nose angle is **not readable**. ⚠ **No capture can settle this and none is owed** — a
readable ADI would still only give a frame-derived angle, which cannot confirm a decode
(`docs/verification.md` DET-12). The open question is what α the original's climb path holds, and it
is answered in the force path above. Do not close the gap
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
slot except the per-spawn AI spread below, which multiplies it by a random `1 ± 5 %`.

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
which it is not), and an **initial** velocity for a spawning AI aircraft (`0x46aaf4`, the
mission-event "move object to node" action; ⚠ this used to read "a speed clamp", which it is not,
and it is the same `min(plane_speed_max, fd_speed)` rule the vehicle factory applies, item 13
above) — never as a solved equilibrium. **Do not treat `fd_speed` as the original's top speed in
B12/B13 without settling this.**

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
2 s of tapering thrust; slamming open takes the same. `FlightController` now applies the live-lever
slew to AI commands as well as keyboard commands, so a carrier release's 0.1 seed survives the AI's
first desired-full-throttle update.

## The per-spawn jitter — eleven slots, non-player aircraft only (IMPLEMENTED)

At the tail of the spawn/reset function `FUN_00476250` (`0x477340`–`0x4773f0`) sits a block that
gives every non-player aircraft its own slightly-different airframe. It runs only when all three
of these hold:

- the object's own name (`obj+0xc`) is not `"player"` — a 7-byte compare at `0x477326`;
- `FUN_00440ad0()` returns 0. That function is `return *DAT_0064f750`, the first dword of the
  **`"Network"` subsystem object** looked up at `0x44023d` and stored at `0x440247`
  (`docs/org/aim-assist.md` names the same flag) — i.e. **not a network game**;
- the vehicle class `obj+0x67c` is **0 or 1**. That field is the def's `mode` key, resolved by the
  parser's own string table at `0x47afc0`–`0x47b081` (`0x62808c` onward): **`jet` = 0, `heli` = 1,
  `tank` = 2, `ship` = 3, `wingman` = 4, `plane` = 5**, and it is copied to the runtime object at
  `0x475abf` from def `+0xa4`. So the jitter reaches aeroplanes and autogyros and skips ships,
  ground vehicles — and **the shipped `w*` wingman family**, which authors `mode wingman` (class 4)
  and so flies unjittered even though the dispatch flies it down the same aeroplane arm as class 0.
  Everything else in the shipped file inherits `basic_airplane`'s `jet`; only `basic_airplane`,
  `patrolboat`, `t_truck` and the eleven `w*`/`wingman`/`bswingman` defs author `mode` at all.

Then, for each of **eleven** runtime slots in this order, it draws `rand()` and multiplies the slot
in place by `((2r − 1) · 0.05 + 1)` where `r = rand() · 3.051851e-05` (`1/32768`): an independent
uniform **1 ± 5 %** per slot, eleven separate draws.

| # | Runtime | Def | What it is |
|---|---|---|---|
| 1 | `+0x2cc` → mirrored to `+0x2d0` | — | whole-vehicle health max (and its "current" mirror) |
| 2 | `+0x2c4` → mirrored to `+0x2c8` | — | whole-vehicle armour max (and its "current" mirror) |
| 3 | `+0x668` | `+0x124` | `fd_speed` |
| 4 | `+0x66c` | `+0x128` | `ThrustFactor` (the stock engine's `power`) |
| 5 | `+0x670` | `+0x12c` | `drag_factor` |
| 6 | `+0x648` | `+0x104` | `pitch_torque` |
| 7 | `+0x644` | `+0x100` | `roll_torque` |
| 8 | `+0x680` | `+0xe8` | `rates[0]` |
| 9 | `+0x684` | `+0xec` | `rates[1]` |
| 10 | `+0x688` | `+0xf8` | `turns[0]` |
| 11 | `+0x68c` | `+0xfc` | `turns[1]` |

⚠ **`veh_weight` (`+0x674`) and `ref_area` (`+0x678`) are NOT in the list**, which is why nothing
computed once from that pair (stall speed) can go stale behind the jitter.

⚠ **The per-part damage pools are not touched either** — only the whole-vehicle pair is, and it is
the pair the kill test reads (`docs/org/vehicleDamage.md`).

**Slots 8–11 are inert on an aeroplane.** `rates` and `turns` are the surface-driving integrator's
acceleration and steering rates with their clamps: `FUN_0048f7d0` accelerates toward the throttle
input at `rates[0]` and clamps to `+0x178` (seeded from `rates[1]`), `FUN_0048f720` does the same
for steering at `turns[0]` against `+0x17c`. Both are reached only from the class-2 (`FUN_0048a880`)
and class-3/5 (`FUN_0048b480`) arms of the class dispatch `FUN_00489ea0`, plus the class-1 autogyro
arm `FUN_0048ffe0` which reads `+0x680`/`+0x684`/`+0x688` directly. The aeroplane arm
`FUN_0048e580` only copies `+0x684` into `+0x178` at `0x48e8bc` and never reads it back. The keys
are nonetheless authored on `basic_airplane` (`rates` 10/42, `turns` 4.6/6.5) and so every aircraft
carries them; the shipped data authors them on exactly three defs — `basic_airplane`, `patrolboat`
and `t_truck`.

**In the remake:** `PlaneStats.WithAiSpawnJitter`, applied at `FlightRoster.SpawnAi` to a
COPY of the session's shared per-airframe stats. The name test becomes `IsHumanPiloted` (C21's
recorded divergence, since this engine flies up to four humans); the network gate holds trivially
(no network play) and the class gate holds by construction (every airframe it can fly is class 0).
Seven of the eleven have a field to move; slots 8–11 have no consumer here, exactly as they have
none on the original's aeroplane arm. The draw is keyed by the aircraft's spawn ordinal off
`Rng.Spawn`, not taken from a shared stream, so a `--det` replay reproduces it and it cannot shift
any other subsystem's sequence.

⚠ **The class-4 wingman exemption has no analogue here and is a recorded divergence.** This engine
flies every aircraft, hostile or wingman, off a *player* def (`PlaneStats.Load` requires
`player_airplane` in the `kind_of` chain), and every one of those resolves `mode jet` — so the class
gate holds trivially and an Instant Action wingman is jittered where the original's own `w*`
wingman would not be. Selecting on team instead would be inventing a mapping the data does not
carry.

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
numbers, and in four places the difference changes the conclusion.** The authored set is the table
below, read back out of `extracted/zrdr/player.zrd.json` and pinned by
`CSVM.Tests/PlaneStatsFlightGlobalsTests`; read it as the operative one, and treat the fallbacks as
evidence of intent only.

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

**Implemented.** `FlightModel.YawAuthorityAt` is this table, built from A1's already-plumbed
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
exercise is exactly the invented content this project's ground rules forbid; this document and
`CSVM.Tests/ControlLimiterTests` (which fails if a data edit brings one into reach) carry the closed
finding, so a future session reading the decode does not mistake the fade for a missing feature.

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
anywhere in the chain, so it is frame-rate dependent. Both the factor and `S` are cut by a further
**×0.15** (5.0 → 0.75, not down TO 0.15 — confirmed by decompile) after the `obj+0xAC`
collision/ground-blow gate has expired but while the clock is inside `obj+0xB4` (below). Together
these make a carrier drop: 1.5 s with no AI ground blow, then 1.0 s at ×0.15, then full strength.
⚠ **This window IS reachable in the remake.** A zeppelin's fighter-drop launch
(`AiGeneratorRuntime` → `FlightRoster.SpawnAi`, the "Zeppelins" section below) is this engine's
carrier drop, and a freshly-dropped fighter flies the same `UsesAiForcePath` plant this law reads.
The port carries this timer on each carrier-released aircraft, so the ×0.15 cut applies for its
full 2.5 s window.

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

**What reads this enum, and what writes the stick, is decoded on its own page.** See
[aiControlLaw.md](aiControlLaw.md) (2026-08-15): the AI brain `FUN_0041c270` runs from the
world tick just before this vehicle's integrator, dispatches on the order at `obj+0x2f0` and this
mode, and reaches the stick through `FUN_0041b560`. `obj+0x948` is the target pointer rather than a
flag, and `obj+0xBA` is the evade flag the damage handler sets.

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
flag by identity. The follower itself is the next section.

**Zeppelins: yes, and by the default rather than by a zeppelin rule.** `extracted/zrdr/vehicle.zrd.json`
names no zeppelin, blimp or airship type, so a zeppelin is never a spawned registry vehicle in this
install. IA1's is the C1 gamez scene node `multiplayer1zep` (`extracted/C1/gamez/nodes.json`), driven
by `mis_anim` animations, with `update_flags` 1 and both `active` and `intersect_surface` set. It
never reaches the registry filter at all, and repels the player exactly as terrain does. The GDD's
naming of zeppelins describes the outcome, not a mechanism.

⚠ **The vehicle filter is player-only**, since it sits inside the `param_1 == DAT_0071c298` test. On
the AI path nothing is filtered and every hit the sweep returns repels, other aircraft in ordinary
flight included.

**Implemented 2026-08-15** (`BL-359` player path; `C23` AI path; `git log --grep=BL-359`,
`git log --grep=C23`). `FlightController.ProbeGroundBlow` casts the ray — the SAME cast and falloff
for both paths — and `FlightModel.GroundBlowTerm` branches the response on `UsesAiForcePath`, added
to the command accumulator after the bank coupling and the weathervane and before
`BodyRates += cmd * dt`, which is this function's own ordering. Things the port does differently on
purpose: the emitter filter is the probe's `CollisionLayers.World` mask rather than a registry lookup
(only aircraft bodies carry the Aircraft layer, so terrain, scenery and the zeppelin repel and
aeroplanes do not, which is the same set the filter above produces for both paths — this engine has
no scripted-path vehicle carrying the original's vehicle-filter mark, so there is nothing for the
AI's "unfiltered" sweep to disagree with), and the second effect is folded into the model's existing
nose-chase as `align + 2·S` (player) or `align + 2·S` un-suppressed (AI) — exact rather than
approximate, since two exponential steers toward the same target compose. `CSVM.Tests`'
`GroundBlowTests` pins both laws, including the player's `S²` power against the AI's linear `S`, the
AI's independence from command sign, and the body-frame conversion.
⚠ **`ai_groundblow` alone is not the AI factor.** `FlightModel.GroundBlowTerm`'s response is
`ai_groundblow · groundblow_mag` (5.0 authored), matching the correction above; the AI probe is
additionally gated on `AiModeMachine.Mode != AiMode.Stunned` (`0x0048c317`'s own mode check,
flightModel.md above), reproducing "a stunned AI flies into terrain". Carrier releases now carry
both timers: the 2.5 s post-drop ×0.15 window and the per-object `obj+0xAC` collision grace. The
carrier grace also suppresses AI ground blow; its probe and both output terms begin only after it.

## The scripted-path follower (`FUN_0048a110`), the second movement law

A placed vehicle can be a puppet on an authored waypoint list rather than a simulated aeroplane. The
aircraft update dispatcher `FUN_00489ea0` branches on `obj+0xcc` **before** it reaches any flight
law: non-zero and the object is driven by the path follower `FUN_0048a110`, zero and it runs the
movement law selected by `obj+0x67C` (`0`/`4` being `FUN_0048e580`, the flight integrator). The two
are exclusive, so nothing in the follower is a steering input to the flight model, and it is not AI
behaviour either.

**The lifecycle.** The spawner `FUN_0047c210` sets `+0xcc = 1` when the spawn record carries a path
(`0x0047c568`) together with a freeze flag `+0xd4 = 1` (`0x0047c57e`), so the vehicle sits motionless
at its first waypoint until a mission goal releases it (`FUN_0046a2b0`, `0x0046a2c3`, reached from
the goal-action runtime `FUN_0046a490`). That same runtime can attach a path at any time with
`FUN_004940d0` (`0x0049427c`), which sets `+0xcc = 1` and `+0xd4 = 0` so the vehicle starts moving at
once. Only `FUN_0048a110` clears `+0xcc` (`0x0048a863`), and only on the final leg.
⚠ **The freeze flag `+0xd4` is a separate flag from the path flag `+0xcc`.** A design folding the two
into one boolean cannot express "placed and waiting", which is the state most authored path vehicles
spend most of a mission in.
⚠ `FUN_004afd00` gates AI radio chatter on `+0xcc`, so a path-driven vehicle is silent. Do not model
the movement and leave the voice on.

**The law**, per tick, with `dt` = `DAT_009ad744`:

    target   = next waypoint, y raised by the vehicle type's ride height at type+0x218
               (a flat 0.2 m for movement classes other than 0/4)
    heading += clamp(headingError / 60°, ±1) · dt        radians, so ≥60° of error gives 1 rad/s
    speed    = 17.8816 m/s, which is exactly 40 mph      held until the final leg
    forward  = speed · (1 − |clamped heading error|)     it barely advances while turning hard
    advance the leg when dot(target − pos, legDir) ≤ 5.0

On the **final** leg the steering target is replaced by a point **300 m** along the leg direction,
its y gains `(speed/110mph − 0.4) · 83.3` once speed passes 0.4 of 110 mph (44 mph), and the speed
term becomes `speed += 4.0302024 · dt` instead of the fixed 40 mph. Reaching that leg's own waypoint
clears the path flag, which is the handoff to the flight model.

The constants: `17.8816` is 40 mph exactly, `0.020335784` is 1/110 mph and `0.95492965` is 3/π, the
60° heading-error normaliser. ⚠ **`4.0302024` and the `83.3` climb gain were read but not
identified.** They are used as read, not as tuning knobs, and nothing may retune them; what is open
is which authored quantity they come from.

**The path source.** The roster's `taxiPath` slot ([formats/ai-rosters.md](../formats/ai-rosters.md))
names a path (`pp1`); the chapter's gamez carries it as the transform-only subtree `pp1_aipath`,
whose `pp1_aipN` children are the waypoints in ordinal order. Ten vehicles carry one, in three
missions: C1/M04 (`blakepeace_2_3`…`_6` on `pp1`…`pp4`), C2/M02 (five) and C5/M01 (one). C1/M04's
four sit on an airfield runway and its `objectives.zrd` releases them with `START_TAXI` two to three
seconds apart, which is a flight taking off one aeroplane at a time. Fixed taxi speed, a ground-height
offset, a final-leg acceleration with a climb-out and a handoff to the flight model read as the
runway takeoff run, though `FUN_004940d0` shows a goal can attach a path for any purpose. Instant
Action places no vehicle on a path (`ia.zrd.json`'s `dzpath1`–`dzpath5` are danger-zone gates), so no
golden can see it.

**What CSVM ports of this.** `Flight/PathFollower.cs` is the law with every constant above,
`Mech3/ScriptedPath.cs` the route, and `Session/ScriptedPathVehicles.cs` the lifecycle, released by
`CampaignDirector`'s `START_TAXI`. Pinned by the `scripted-path` suite over C1's real `pp1`.
Two things the decode does not pin, chosen here rather than found: the altitude between waypoints
(the follower is taken to the target's height over the horizontal distance still to run), and the
leg-advance test, which is measured against the leg's own waypoint on every leg including the last,
so the 300 m point steers and does not also delay the handoff.
⚠ **The ride height for movement classes 0 and 4 is not identified.** Those are the aircraft classes,
so it is the one every shipped path vehicle needs, and `vehicle.json` has no field traced to
`type+0x218`. The port leaves it at zero and says so rather than reusing the 0.2 m the other classes
take.
⚠ **Nothing spawns the `aiv` roster yet**, so no session places a vehicle on a path and the registry
is empty at run time; `START_TAXI` reports itself unconsumed until a roster spawner calls
`ScriptedPathVehicles.Place`. Ground blow's own emitter test reads `+0xcc`, so a spawned vehicle put
on a path would stop repelling the player the moment it completes the path; ground blow shipped
without the registry filter (its player probe simply excludes aircraft), so whether a path-driven
vehicle needs to become an emitter in this build is open, and only becomes answerable once one exists.

## Collision response and `bounce_factor` (`FUN_0048d7f0`)

Decoded 2026-08-14, **impulse implemented 2026-08-15** (retiring `BL-172`):
`FlightModel.BounceNormalSpeed` is the law and `FlightModel.Collide` the site, gated on
`IsHumanPiloted` and not-already-crashed. The sweep, the placement and the two timers below are
NOT ported — this engine has its own collision sweep, and what C25 bound is the impulse alone.

`bounce_factor` lives in `player.json`'s `crash` block, is a **raw scalar**, and
lands in global `0x0071c35c` from the parser store at `0x00473c38`. Its default is pre-set at
`0x00473bb5` *before* the block is looked up, so an absent `crash` block leaves the fallback
standing. Fallback **0.8**, this install authors **0.6**.

`FUN_0048d7f0` sweeps the aircraft's contact spheres (`obj[0x1a9]..obj[0x1aa]`, stride `0x24`)
through the world and resolves the **single deepest** contact. On a contact frame it **replaces** the
frame's translation with a placement at the contact point plus a fixed **0.03** along the normal,
rather than moving by `v·dt`. One resolution per aircraft per sweep, no sub-stepping.

⚠ **An object sweeps only every OTHER frame, on a random per-object phase.** The sentence above
said "per tick", which is wrong. `FUN_0048d7f0` returns immediately unless
`((obj[0x1af] ^ DAT_009be6f8) & 1) == 1`, that is unless the object's own phase byte at `obj+0x6BC`
matches the parity of the global frame counter. The phase is seeded from `rand()` in the entity
constructor `FUN_004aff80`, so it differs per object and per run. On a skipped frame the sweep does
not simply do nothing: it accumulates that frame's translation into `obj+0x6B0`…`obj+0x6B8` and
returns severity `0.0`, and the next sweep that does run applies the accumulated motion. This halves
the collision rate and is why two aircraft in the same contact do not necessarily resolve on the
same frame. **Not ported**: our sweep runs every sim step, which resolves a contact sooner than the
original would but never differently.

⚠ **Only the player bounces.** The impulse branch is entered only when `obj == DAT_0071c298` and the
player is not already crashed. AI aircraft get position correction and an impact cosine, and no
impulse at all. Ported as `IsHumanPiloted && !crashed`, the same widening of a single-global-
player-pointer guard to every human-piloted aircraft that `C21` recorded for the force path; pinned
by the `graze-bounce` suite, which flies a player rig and an AI rig down the same trajectory into the
same floor and measures `e = 0.56` against `0.00`.

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

⚠ **The partition runs the opposite way round to the summary sentence this decode has been quoted
with ("a short lever arm rebounds at up to 0.6 while a wingtip throws most of the impact into
rotation"). Corrected 2026-08-15 while implementing it.** `Δω = (r × J)/|r|²` has magnitude
`|I⁻¹|·|J|·sinθ/|r|`, which **falls as 1/|r|**: the `/|r|²` is a point-mass moment of inertia, not a
lever. So `A` shrinks as the arm lengthens and `f_lin = L/(L+A)` rises toward 1 — a wingtip rebounds
HARDER than a contact near the centre, and nothing here converts a wingtip strike into spin. With
`I⁻¹ ≈ 1.1` it reads `f_lin = 2.25/(2.25 + 1.1·sinθ/|r|)`: ≈0.91 at a 5 m arm, ≈0.67 at 1 m, exactly
1 when `r ∥ n` (a contact directly under the centre of mass, where `r × J` vanishes). The formulas
above are unchanged; only their reading was wrong.

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
on flat ground against 0.06–0.18 on vertical faces, `BL-172`) must therefore never be implemented as
a per-surface coefficient.

⚠ **The lever-arm partition does not produce that split either, and the claim that it does was
withdrawn 2026-08-15 on the arithmetic above.** With `A ∝ 1/|r|`, `f_lin` is ≈0.9 in both
orientations at any contact geometry an airframe actually presents, so the two rebound at nearly the
same coefficient: the `graze-bounce` suite flies both and measures `e = 0.56` on flat ground against
`e = 0.59` on a vertical face. What differs on a wall is the AXIS — the rebound is horizontal, so an
altimeter reads nothing across the contact (measured `vy 0.00 → 0.00` there) — and that, not a
coefficient, is what a vertical-face clip shows. The flat-ground magnitude above `bounce_factor`
still comes from the doubled rotational term, which `bounce_factor` cannot produce.

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
  (`0x0048d383`, `0x0048d395`), the same branch that cuts that impact's damage terms to 20 %
  ("Collision damage" below).
- **`obj+0xB4`, a post-drop settling window.** Clock + **2.5**, written on the spawn paths only.
  `FUN_00452450` gives the context: the entity is repositioned, yawed to −π/2, and given the launch
  velocity minus 22.352 m/s vertically, which is a drop from a carrier at 50 mph. `AiGeneratorRuntime`
  samples the host's live velocity each sim step and seeds this vector after the released aircraft's
  respawn, so the generic spawn speed cannot overwrite it. The same routine seeds both throttle fields
  to **0.1**, which `FlightController` carries through the release rather than its ordinary spawn throttle.
  The first 1.5 s overlaps `+0xAC`, so ground blow is completely absent; only the final second runs
  at 15 %.

Neither is a damage-invulnerability timer; `obj+0xAC` disables collision itself.

## Collision damage (`FUN_0048d2c0`)

The section above is the response; this is the damage. `FUN_0048d7f0` returns an impact severity and
`FUN_0048d2c0` turns it into a damage pair. **Ported**, as `Flight/CollisionDamage.cs` with the
authored ranges read in `PlaneStats` and the contact resolved in `AircraftContactResolver`;
the struck party's damage runs through `AnimRuntime.CollideDamageAt` and the striker's through
`PlaneDamage.Apply`. Two parts of the section below are knowingly not ported and say so where they
appear: the object's own `+0xbc` damage reduction, and the every-other-frame sweep parity.

**The severity is a cosine.** `FUN_0048d7f0` normalises the velocity through `FUN_00422690` (at
`0x0048df8b` on the player path, `0x0048dba0` / `0x0048dbba` otherwise) **before** dotting it with
the contact normal, and returns `s = -(v̂ · n̂)`, dimensionless and in `(0, 1]`. Nothing registers
unless `s > 0` (`0x006032c8`).

```
armorDmg = healthDmg = max(300 · s³, 50)          cube at 0x0048d4c1
                                                   scale  0x0071c34c / 0x0071c354
                                                   floor  0x0071c350 / 0x0071c358
if the striker is not the player and it hit an aeroplane:  both × 0.2
```

⚠ **There is no airspeed term anywhere in it.** A 400 mph belly-flop and a 90 mph belly-flop at the
same attitude deal identical damage. The whole law is a function of the angle of incidence, which is
why a remake that scales collision damage by closing speed cannot be made to match by retuning a
constant. The floor dominates below `s ≈ 0.550`, so any contact shallower than about 33° off the
surface deals a flat 50.

The four numbers are **data, not constants**: they are `player.json`'s `crash` block, the same block
`bounce_factor` comes from, parsed by `FUN_004735b0` under keys `armor_damage_range` (`0x006275a0`)
and `health_damage_range` (`0x006275b4`), with element 0 landing in the floor slot and element 1 in
the scale slot. Compiled fallbacks are `[15, 200]` (`0x00473b8d`, `0x00473b97`, `0x00473ba1`,
`0x00473bab`); this install authors `[50, 300]` for both, so the fallbacks never stand. This is the
same authored-vs-fallback split the "Authored values vs the executable's fallbacks" section covers.

**A collision is delivered as a weapon hit.** `FUN_0048d2c0` looks up the weapon record named
`wep_24` (string `0x00628a14`, by-name scan `FUN_005abfd0`, stride `0x214`) and hands the pair to
the ordinary applier `FUN_005abcf0` at `0x0048d55b`, the same one a rocket uses. The call is gated
only on the sweep having resolved a node (`contact+0x24 != 0`): there is **no** test on object
class, on a destructible's `ACTIVATION`, or on whether the striker survives. Two consequences
follow, and both are structural rather than incidental. A rammed object dies through its ordinary
`DAMAGE_SEQUENCE` thresholds, so collision damage is severity-scaled rather than an instant kill.
And any gate that sits downstream of the applier catches collisions for free, which is exactly how
the gasbag exemption works: the gasbag damage handler `FUN_004c0640` tests `DAMAGES_ZEPPELIN`
(`weaponRecord+0x210`, flags `& 0x1000`, `TEST AH,0x10` at `0x004c0665`) before subtracting
anything, `wep_24` does not carry the flag, and so ramming a gasbag deals it nothing while still
killing the plane. The zeppelin cannon and turret handler `FUN_004c0880` has no such test and
subtracts at `0x004c090b`, so every zeppelin part except the gasbags is rammable.

**Both parties are damaged, by the same law.** The struck party takes the pair on the `wep_24` path
above; for an aircraft target that runs `FUN_004b9750` → `FUN_004b9770` → `FUN_004b9b30` →
`FUN_004b9bc0` and lands on the identical primitives the striker applies to itself. The striker then
takes the same pair: per-part first where the airframe has a part list (`FUN_004b3950` picks the
struck node's own part, else the part nearest the contact; `FUN_004b3bf0` at `0x0048d724`), then the
remainder in aggregate (`FUN_004b8070` at `0x0048d783`). The armour-to-health split is
`FUN_004b7f80`: with `f = min(1, armor / armorDmg)`, armour drops by `armorDmg` floored at zero and
health drops by `(1 - f) · healthDmg`, so a **fully-armoured contact costs no health at all**.
Survival is then `health > 0` (`obj+0x2d0`, tested `0x0048d78b`).

⚠ **An invulnerable striker still destroys what it hits.** The `obj+0x920` early-out at `0x0048d563`
sits *after* the struck object has been damaged, so invulnerability protects the rammer only.

⚠ **The player is deliberately asymmetric, in three ways that travel together.** A player-owned
striker jumps past the entity-detection block at `0x0048d2ed` entirely. So the player never takes the
0.2 cut that scales an AI's ram to a fifth (`0x0048d51a`, `0x0048d526`); the player writes no
`obj+0xAC` grace clock, so the contact can re-resolve on following frames while the two aircraft are
still overlapped; and the player is never subject to `local_11`, the flag that forces destruction
regardless of remaining health (`0x0048d79e`). `local_11` is set for a non-player striker that did
*not* resolve an aeroplane, so an AI that rams terrain or a building always dies, while an AI that
rams another aircraft survives if health remains. Entity detection requires node flag `0x40000000`
and a dispatch class at `+0x67c` of 0 or 4, the two aeroplane classes (`0x0048d360`). This is the
same single-global-player-pointer guard the bounce impulse uses, and widening it to every
human-piloted aircraft is the same decision `C21` recorded for the force path.

**What a rammed destructible actually takes.** The handler is `0x004e7220`, registered per
animation record by `FUN_005230d0`, the animation-definition loader (`D:\zipper\gamez\zEffect\zeff_ani…`,
`0x0062ee28`), which is the right place because a destructible IS an animation definition
([`../formats/destructibles.md`](../formats/destructibles.md)). Records live in the array at
`DAT_009fd14c`, count `DAT_009fd14a`, stride `0x110`, and the registration is keyed on the record's
`+0xa1` byte: 0 registers slot 0 only, 1 registers slot 1 only (`FUN_004e7150`), 2 registers both.
`FUN_005abcf0` dispatches through **slot 0**, and `0x004e7220` re-checks the byte itself, accepting
`+0xa1 ∈ {0, 2}` and refusing everything else (`0x004e7234`). That byte is the `ACTIVATION` enum,
with `WeaponHit` 0 and `WeaponOrCollideHit` 2, so a ram damages **both** kinds and the enum decides
only the plane's fate.

With `+0xb4` the max health, `+0xb8` the current pool and `+0xbc` a per-object damage reduction:

```
net = healthDmg - animRecord[+0xbc]      subtracted, and written BACK into the pair (0x004e7259)
if net > 0:  animRecord[+0xb8] -= net
             re-evaluate DAMAGE_SEQUENCE (FUN_004e71e0)
             dead at +0xb8 <= 0 -> death sequence FUN_004ed730
```

⚠ **A destructible consumes the health term only.** `armorDmg` is never read on this path, and
`healthDmg` is reduced by the object's own `+0xbc` before anything is subtracted. **`+0xbc` is not
ported**: our destructible data carries no equivalent field, and the law reproduces the original's
observed graze and ram outcomes on C1's 60 HP buildings without it, so it is zero or near zero on
those defs. Every non-aircraft
handler behaves this way (the two zeppelin handlers above, and clutter's `0x004df420` from
`FUN_004deab0`, `D:\zipper\gamez\zclass\cls_clutter.cpp`); only the aircraft handler `0x004b9750`
consumes both through the armour split.

⚠ **The `+0x400` gate refuses damage when the bit is SET, not when it is clear.** `0x004e7227` tests
`weaponRecord+0x74 & 0x400` and jumps *into* the damage body when it is zero (`JZ` at `0x004e722a`
over the `XOR EAX,EAX; RET` at `0x004e722c`). Bit `0x400` is the `FREEZE` impact type, set only by
that keyword in the weapon parser `FUN_005ad630`, paired with `0x40` `HEAT` and `0x1000` `DESIGNATE`.
No shipped weapon carries `FREEZE`, and `wep_24` in particular does not, so collisions do reach
destructibles. Read the other way round this would say collisions damage nothing, so the direction
matters.

⚠ **A ram borrows the HE rocket's record, so it borrows its impact effects.** `wep_24` is
`MSG_WEAP_HEXPLOSIVE_ROCKET`, `NAME "BOOM"`. Its authored `ARMOR_DAMAGE 100` / `HEALTH_DAMAGE 100`
are NOT used (`FUN_005abcf0` forwards the caller's pair, which `FUN_0048d2c0` filled with the
severity law above), but its `IMPACT` block still fires: ramming a building plays `large_fireball`,
ramming water plays `bsplsh.flt`. A remake that models the damage without the effect will look
wrong on contact.

**What the sweep can hit.** The sweep removes the object's own node (`gwNodeSetActive`,
`FUN_004cca30`, bit `0x4` of `node+0x24`) and then queries the world through `FUN_004ca320`, or
`FUN_004c8f70` for a single probe. That query has two halves: a terrain-grid walk over the cells the
swept AABB covers, and then an unconditional loop over the world root's DIRECT CHILD list
(`root+0x5c`, count `root+0x56`) carrying **no spatial test and no class test**, admitting a node on
bits `0x4` and `0x10` plus the layer filter `FUN_0056c430` and recursing into its children through
`FUN_004cad00`. `FUN_004dae80` decides which of those two lists an object registers into by whether
its cell range spans more than one cell, so a large moving object lives in the root child list and is
descended into on every sweep.

This is why a zeppelin's sub-parts are rammable, which they are here too: a cannon mount takes ram
damage and a gasbag takes none, the exemption holding because a ram carries no `DAMAGES_ZEPPELIN`
ordnance. They are ordinary descendants of the zeppelin root
in the hierarchy the recursion walks; `gwNodeNew` stamps every node `node+0x24 = 0x0108001C` and
`node+0x30 = 0xFF` at birth, so both predicate bits and the layer filter pass by default, and neither
the zeppelin parser nor its two part-registration helpers clears them on a part. There is no
"static world geometry only" filter anywhere on the path.

**Open:** what writes dispatch class 1 to `entity+0x67c`. Class 1 runs `FUN_0048ffe0`, which sweeps
at `0x00490441` but never calls `FUN_0048d2c0`, so class-1 objects take position correction and no
collision damage at all; every write to that field found so far stores 0.

## The `*Tune` rates — none of the three exists in the executable

The remake's per-axis control-rate calibration. Steady rate is
`torque · rec_moments_inertia · Tune / ang_momentum_damp` (× the yaw authority curve on yaw), and a
full 360° takes ≈ `1/damp` of spin-up plus `2π/rate`.

⚠ **No axis carries a calibration factor.** Read out of the LIVE force function `FUN_0048c470`, the
three stick torques are built in three guarded blocks and each is exactly four factors, with nothing
else multiplying in:

| Axis | Guard | Chain | `dt` multiply |
|---|---|---|---|
| Roll | `obj+0x114` ≠ 0 | `roll_torque [+0x644] · stick [+0x114] · rollAuthority · dt` | `0x48caa2` |
| Pitch | `obj+0x11c` ≠ 0 | `pitch_torque [+0x648] · stick [+0x11c] · pitchAuthority · dt` | `0x48cb19` |
| Yaw | `obj+0x120` ≠ 0 | `rudder_torque [+0x64c] · stick [+0x120] · yawAuthority · dt` | `0x48cbd5` |

The roll chain is visible instruction by instruction at `0x48ca8d` (`FLD [ESI+0x644]`), `0x48ca96`
(`FMUL [ESI+0x114]`), `0x48ca9f` (`FMUL [EBP-0x18]`, the authority scalar) and `0x48caa2`; the pitch
chain the same way at `0x48cb07` and `0x48cb19`. `rec_moments_inertia` and `ang_momentum_damp` enter
downstream in the integrator, as "Torques and the limiters" describes. Pitch and yaw differ from roll
only in which authority scalar `FUN_0048bdd0` hands them and in the opposing-command test, neither of
which is a constant factor.

So the authored numbers are the whole of it on every axis. The Bloodhawk's `roll_torque 7.5` and
`rec_moments_inertia.z 1.10` against `ang_momentum_damp 5.0` give **90.7 °/s** and a 360° roll in
**4.17 s**.

| Constant | Value | Standing |
|---|---:|---|
| `PitchTune` | was **0.89** | fitted to stopwatch timings and cockpit-gauge video, sustained pitch ≈33 °/s. **No counterpart in the binary**, now 1: pitch-rate moves 33.54 → **35.26 °/s** against the original's 33.00, still inside the ±3 band |
| `YawTune` | was **1.57** | pinned against the authored yaw curve, full-rudder 360° 28.6 s. **No counterpart in the binary**, now 1: yaw-360 moves 28.55 → **49.12 s** against the original's 28.60, and the row is now informational |
| `RollTune` | **1.0** | already retired on this evidence; a 2.12 that used to sit here is gone |

The 2.12 existed to reach a **2.05 s** roll timed off footage, which is 2.12× what the executable's
own arithmetic produces. A decode is not contested with a stopwatch reading, so the multiplier went
rather than the decode. The pitch and yaw multipliers are the same class of fit against the same
class of evidence, and the same disposition applies. Restoring any of the three needs a mechanism
traced in the binary.

⚠ **Pitch survives its own measurement; yaw does not.** Pitch rate is NOT proportional to its
multiplier, because the weathervane torque enters the same accumulator carrying no `*Tune` and grows
with the resulting incidence: 0.89 → 1 moves the rate by 5%, not by 12%. At 35.26 °/s the row is
still green against 33.00 ± 3, and the video it was fitted to reads 37.9 / 33.7 / 30.7 / 36.5 °/s
binned round a loop, so 0.89 was fitting noise inside its own spread.

Yaw is the row the decode breaks, at 49.12 s against a 28.60 s target. What it moves toward is the
rudder this page already describes: a ground-handling control held at 10% authority for all of
normal flight, which a 28.6 s full-rudder 360° never fitted.

⚠ **`yaw-360` is now INFORMATIONAL, and the 28.6 s was NOT rewritten.** The footage figure stays
recorded exactly as measured, but it does not gate anything: it disagrees with the decode, and a
frame-derived duration cannot refute one (`docs/verification.md` DET-12). The row joins
`accel-150-290` and `decel-290-150` as informational rather than as a target refitted to whatever
the model now produces. `FlightEnvelopeTests.FlightScenarios` drops 7 → 6 to make the demotion loud,
which is what that constant is for. The slow rudder was read at the controls and accepted before the
row moved. Nothing is owed here — do not restore a multiplier to chase the 28.6 s.

⚠ **One golden moved with this: `c1-flight`, re-pinned.** It flies `--hold=0.2,0.1,0,1` — held pitch
and roll, zero rudder — so it moved on the pitch change alone, and the other fifteen shots are
untouched, which is what confines the change to the control rates. The capture was inspected before
the re-pin: same scene, same HUD, a different flown attitude.

⚠ **This halves the roll rate of every airframe, the player's included**, since the other ten move
with the Bloodhawk as they always have. It is a decode correction and not a feel change, so the
at-the-controls read of it belongs in a playtest rather than in a re-tune.

**The video also closed an open question: the original's pitch rate does NOT fall off with speed.**
Binned round a loop it reads 37.9 / 33.7 / 30.7 / 36.5 °/s over 120–280 mph — flat within the
noise — so speed-independent pitch is right, and this is the measurement behind "pitch carries no
high-speed fade" above.

The refits are **Bloodhawk-pinned**, as they always were; the other ten airframes have no measured
target of their own and simply move with them.

⚠ **Do not chase the pitch transient's residual roll-off through these constants.** They set the
STEADY rate, which matches; a transient chased through them breaks the thing that currently works.
The square-wave cadence sweep is the measurement that belongs to that gap — see the stick-ramp
section's landing note.

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
- **`StickRampTests`** — `FUN_00487460`, the three per-axis blocks. Pins the RATE (0.4 s of held key
  is full deflection, and half that time is half deflected, so a linear ramp cannot be swapped for an
  exponential approach) and, twice over, the ASYMMETRY: release centres the axis in one frame, and a
  reversal ramps from centre rather than counting down from the old deflection. ⚠ A symmetric ramp
  agrees on a held key and differs on every release and every reversal — which is the whole of the
  cadence roll-off, so the held-key case alone would pin nothing that matters.
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
  bias in every pull at any bank — the "knife-at-zero-bank leak". Also pinned: the
  knife-edge never settles on any of the eleven (a bounded sag puts almost none of its total in the
  last third of a 36 s hold, a genuine drift about a third), and α stays inside `liftAOAs[0]` on
  all eleven — peak **0.71–2.59°** against the authored 5°. A fourth assertion, that the nose stays
  well below the path, is retired with `wingVert`: its bound was a footage anchor written to catch
  the chase getting faster, which is what the decode requires. That last one **replaced a lost prose
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
  `sustained-turn-sink` (both riding the banked turn-rate difference against the footage, which C22
  was expected to close and demonstrably does not). `terminal-dive` came BACK from that list when the attitude-thrust terms
  landed: the count went 7 → 6 → 7, and a demotion is never the quiet way to make a run green.

## What this changes for the remake

Checked against [`src/Flight/FlightModel.cs`](../../CSVM/src/Flight/FlightModel.cs) and
[`src/Flight/PlaneStats.cs`](../../CSVM/src/Flight/PlaneStats.cs), the items most likely to differ:

1. **Lift is the demanded G, delivered.** The remake's `liftFrac` cancels a *share* of gravity's
   cross-path component; the original builds a demand vector, clamps it to ±5/9 G and applies it.
   The remake's `AlignRate` (TUNE, 4/s) is the same quantity as the original's authored
   `lift_accel_rate` (fallback 1.2/s) — it should be read from `player.json`, not tuned.
2. **`liftAOAs` is an airflow blend, not a load-factor ramp.** The remake reads `[5, 9]` as the
   edges of a G ramp; the original uses them as the window over which the relative wind is faked
   toward the nose. Same numbers, different mechanism. This also **settles the units question for
   these keys**: the parser takes their cosine, so `liftAOAs` and `maxAOA` are confirmed **degrees**,
   while `highGs`/`lowGs` are stored raw and are plain **G**.
3. **The two hardcoded bank constants (0.205, 0.165).** These are in no data file — they are
   developer-console variables — so no amount of data extraction would have surfaced them. Landed
   in `C22`; see "Bank coupling — resolved". ⚠ They make the banked turn **faster**, not slower,
   so they do not account for the footage reading a slower banked turn than ours.
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
: peak demand 2.13–5.01 G against `highGs[0] = 9`, peak α 8.9–25.6° against
   `maxAOA = 46°`, on all eleven airframes — see the D33 landing note above.
10. **Thrust scales with `ref_area`, not `1/veh_weight`.** The remake divides engine power by
    weight; the original multiplies it by reference area, which is what makes `RefArea` cancel
    against drag. See `ThrustFactor` above.
11. **Drag is a polar in Mach with no induced term.** Any model that makes drag rise with the pull
    is adding a mechanism the original does not have. See Drag above. The remake briefly did:
    a `sin²α` term (`InducedDragCoef` 10.75) was fitted on 2026-08-07, two
    days before this decode, and removed with no successor when
    the decoded Mach polar replaced the fitted drag law. `FlightModel.cs` now carries the same
    no-induced-drag statement in its own comments.
12. **The throttle lever slews at 0.5/s with no idle floor** (2 s full-to-idle). `FlightController`
    applies it to both keyboard and AI desired-throttle commands; carrier releases start both fields
    at 0.1, then ramp if the AI immediately requests full power.
13. **Spawn speed is the mission's own, and it is not plane-dependent.** ⚠ This item previously
    read "the original's is plane-dependent"; that is false at source, and the remake now reads
    the authored value. The player spawn routine `FUN_0047f1f0` takes its speed from the
    mission's own `PLAYER_INIT[4] × 0.1` (18 m/s in 48 of 51 records) and touches no per-aircraft
    data at all: no reference to `fd_speed` (object `+0x668`) or to the def pointer appears in any
    of its 349 instructions. The remake's 53.6 m/s traces to `−53.6448` at `0060803c`, read only
    by the cheat dispatcher `FUN_0047e080`'s case `0x3b7` (teleport player to camera), which is
    120.000 mph exactly. Spawn throttle is `PLAYER_INIT[3]` (0.8 in 49 of 51 records), replacing
    the remake's 0.5. Full decode, including the mode-3 Instant Action branch and the reset paths:
    [../formats/spawns.md](../formats/spawns.md), "Story mission spawns".
    ⚠ **The spawn now sits inside the force-scale conflict recorded above.** A start at 18 m/s is
    well below cruise, where the polar reads 2–3.6× too strong against `CAP-05`, and the aircraft
    accelerates through its own computed stall speed at about 4.4 G rather than dropping. The
    climb-out reads right at the controls, so this is a dependency to know about rather than a
    defect: a change to the sub-cruise force path moves the feel of every mission's first seconds.
    The spawn speed is authored data and is not the knob to compensate with.
    The per-airframe spawn rule that does exist belongs to **AI** aircraft: the vehicle factory
    `FUN_0047c210` gives a pathless aircraft `min(plane_speed_max, fd_speed)` along its nose
    (comparison at `0047d84f`) and gives one with an authored waypoint path zero velocity and
    throttle 1.0 (`0047d82d`). `plane_speed` is a two-value authored tag at def `+0x1e4`/`+0x1e8`,
    each scaled by `0.44704` on parse (`00607b2c`), which the AI speed controller then uses as the
    lower and upper clamps.

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
- **Read directly from the executable, 2026-08-15:** that the atmosphere call is on the
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
- **Read directly from the executable, 2026-08-15:** the per-spawn jitter's full eleven-slot list,
  its three gates and the `mode`→`obj+0x67c` class table (`jet`/`heli`/`tank`/`ship`/`wingman`/
  `plane` = 0/1/2/3/4/5), which also settles what was recorded below as an undetermined data token.
- **Not determined:** which registry entities keep the `+0xcc` emitter flag set permanently, so the
  GDD's naming of zeppelins as ground-blow emitters is neither confirmed nor refuted; which axes the
  reverse-authority factor reaches.
