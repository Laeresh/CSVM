# Flight model — rewrite onto the decoded original

**ACTIVE PLAN** (written 2026-08-09). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan ports `src/Flight/FlightModel.cs` from a video-calibrated approximation onto the
original's actual flight model, decoded from `crimson.exe` and written up in
[`docs/org/flightModel.md`](org/flightModel.md). The decode supersedes video calibration wherever
the two disagree: it gives the lift law, the drag polar, the atmosphere, the control-authority
curves, the torque coupling and the stall condition as formulas with authored constants, in place
of roughly a dozen constants fitted against footage. The work is substitution plus refit — the
remake's architecture (a nose-chase model integrating on the velocity vector) turns out to be
structurally the original's, so this is not a rewrite from scratch.

**Out of scope.** AI flight (the original skips the `liftAOAs` airflow blend for AI entirely and
flies it at zero incidence) — that is M4, alongside turrets. Also out: the shipped Dynamics tuner
as a *validation harness*; reaching it in a retail build is an investigation, not a deliverable,
and this plan must stand on its own instruments.

**Assumption this plan rests on.** The decode is static analysis — read, not run. Every item that
changes flight behaviour is therefore owed a measurement against the original's footage, not just
a code-matches-decode check. Where the decode and a measurement disagree, that is a finding to
investigate (Wave D), **not** a licence to overrule the footage.

## Milestone goal

- The lift law, drag polar, thrust curve, stall condition and control-authority curves in
  `FlightModel.cs` are the original's, driven by authored constants from `player.json` /
  `vehicle.json` rather than by constants fitted to footage.
- The count of TUNE constants in `FlightModel.cs` falls substantially, and every survivor is
  either authored data or explicitly justified as absorbing a named, still-open divergence.
- The pinned measurements still hold: the 360° roll at 2.05 s, sustained pitch ≈ 33 °/s, the
  full-rudder 360° at 28.6 s, `CAP-05`'s four drag points, `CAP-01`'s 222.94 mph turn plateau, and
  the terminal dive at 355.2 mph.
- The two conflicts the decode exposes (bank-independent lift; climb thrust running opposite to
  `ClimbGravityScale`) are each resolved to a finding — a code change *or* a recorded disproof.

**This plan does not touch AI flight, turrets, or weapons.** The decode covers AI (it flies at
permanently zero incidence and gets a 10 mph forward-speed floor), and that is genuinely useful —
but it lands in M4 with the rest of the AI work, not here, because nothing in M3 flies an AI
aircraft aerodynamically.

## Decisions (2026-08-09)

| # | Question | Decision |
|---|---|---|
| 1 | Is the decode or the video calibration authority where they conflict? | **The decode, for mechanism; the footage, for magnitude.** The binary says what the shape is; only a measurement says whether we reproduced it. A decode that contradicts a measurement is a Wave D item, not an overrule. |
| 2 | Drop `wingVert` on the decode's word (lift is bank-independent)? | **No — keep it until D31 settles.** The decode and the measured knife-edge sag cannot both be right, and `wingVert` is load-bearing for a departure the suite asserts. |
| 3 | Land the aero core as separate items or one behavioural change? | **Separate items, one joint validation (B14).** Lift/drag/thrust are pinned *jointly*; each may be committed alone, but none is "verified" until B14 refits and re-measures the group. |
| 4 | Refit the pinned `*Tune` rates before or after the bank coupling? | **After C22.** Adding bank→yaw/pitch moves the turn rate the tunes were pinned against; refitting first would just be undone. |
| 5 | Remove `ClimbGravityScale` when the attitude-thrust terms land? | **No — D32 decides.** The two run in opposite directions; deleting one while adding the other changes two things at once and makes the result unattributable. |
| 6 | Where does the decode live, and is it a `docs/formats/` page? | **`docs/org/flightModel.md`** — it documents the original's *executable*, not a data format, so it sits in a new `docs/org/` area rather than `docs/formats/`. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Angle of attack in degrees *is* the load factor — 9° AOA = 9 G." | Traced the coefficient function's third argument back through its caller: the quantity passed is **already a demanded load factor in G**, derived from the airflow/velocity difference. The final formula (lift = clamped G × Weight) survives; the mechanism does not. Anyone re-reading the coefficient function in isolation will re-derive this — the argument name invites it. |
| 2 | "`liftAOAs [5, 9]` are the edges of a load-factor ramp" (the standing reading in `FlightModel.cs`). | The original takes their **cosine** and uses them as the window over which the *relative wind is blended toward the nose*. Same two numbers, entirely different mechanism. Also settles `BL-095`'s units question for these keys. |
| 3 | "The atmosphere's thin band is the operative one." | Arithmetic: the thin band puts the fallback airframe's stall at 309 mph. The dense band puts it at 75.5 mph, which is correct. The band-select threshold has only a read reference in the binary — see D33's trap. |
| 4 | "Thrust is sublinear in the throttle lever — the original's own behaviour." | **Pending, not yet dead** — the original multiplies available thrust by throttle *linearly*. `ThrottleExp = 1.236` is predicted to be an artefact of the current drag shape. B13 either kills this claim or promotes the exponent to a real divergence; do not assume the outcome. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, B11, B12, B13, B15, C21, C22, C24 | Confirm the trace against [`docs/org/flightModel.md`](org/flightModel.md), then implement. |
| **Direction sound, magnitude a judgement call** | A2, B14, C23 | The *what* is settled; the *how much* is TUNE — add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only — no mechanism yet** | A3, D31, D32, D33 | Budget for investigation; **these may end in a disproof, and that is a success.** |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The full decode is [`docs/org/flightModel.md`](org/flightModel.md) — read it before any item. It
carries the function-address table so every claim below is re-checkable at source. The facts this
plan leans on most:

- **Units.** Internal state is m/s and radians; speed tokens in the data files are authored in
  **MPH** (`× 0.44704` on load) and angle tokens in **degrees** (cosined on load). Aerodynamic
  intermediates run in imperial (ft, ft/s, slug/ft³, lb/ft²).
- **Atmosphere is a two-band step with no gradient.** Operative band: ρ = 2.2688e-3 slug/ft³,
  speed of sound 1109.5 ft/s. `q = 0.5·ρ·V_fps²`.
- **Lift** = `clamp(demanded G, −5, +9) × Weight`, capped by `(0.75 − 0.15·Mach) × q × RefArea`,
  where the demand is `lift_accel_rate · (relativeWind − velocity)` with `nom_gravity` added on
  world-up, projected onto the body X/Y plane and divided by 9.82.
- **Drag** = `q · RefArea · DragFactor · 0.73 · (0.12 + 0.8·C_L + 0.5·C_L²)`.
- **Gravity** resolves to exactly `nom_gravity` (20 m/s² in this install).
- **Control authority** is three separate speed curves: roll flat, pitch flat then fading 500 →
  600 mph, yaw a non-monotone table that is **flat 0.1 above 45 mph**.
- **Two hardcoded coupling constants**, 0.205 (bank → yaw) and 0.165 (bank → pitch), present in no
  data file.
- **Authored keys not currently read by `PlaneStats`:** `lift_accel_rate`, `liftAOAs`, `maxAOA`,
  `highGs`, `lowGs`, `turn_fade_in`/`_out`, `yaw_low_speed`/`_high_speed`/`_fade_in`/`_max`/
  `_fade_out`, `high_speed_pitch_fade`, `drag_fade_speed`.

⚠ **The extracted set under `extracted/` does not contain `player.json`** — these globals must be
read from the live install. A1 exists partly to establish that path.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Authored parameters and baseline

1. ☐ Plumb the authored flight globals into `PlaneStats`
2. ☐ Freeze a pre-change baseline across every pinned scenario
3. ☐ Settle what supplies `ThrustFactor`

### Wave B — The aero core

11. ☐ Lift as the clamped demanded-G
12. ☐ Drag as the original's polar
13. ☐ Thrust: linear throttle and the Mach/altitude curve
14. ☐ Joint refit and re-measurement of the aero group
15. ☐ Stall speed per airframe

### Wave C — Rotation and control authority

21. ☐ Yaw authority: the original's speed table
22. ☐ Bank→yaw and bank→pitch coupling
23. ☐ `return_rate` as a weathervane torque
24. ☐ Pitch high-speed fade and exponential angular damping

### Wave D — The open conflicts

31. ☐ Bank-independent lift vs the measured knife-edge sag
32. ☐ Attitude-dependent thrust vs `ClimbGravityScale`
33. ☐ The G and AOA limiters — live or inert in this install?

## Dependency and parallelism notes

**A1 blocks everything** — every downstream item reads authored constants it plumbs. A2 must land
before any behavioural change (it is the baseline those changes are measured against) and is
otherwise independent. A3 blocks B13 only.

Wave B is a chain in spirit but not in code: B11 → B12 → B13 may each be committed alone, but
**none is verified until B14**, which refits and re-measures the group. B15 is independent of the
B11–B14 chain and can land any time after A1.

Wave C is independent of Wave B — it touches `BodyRates`, not the force path — with one ordering
constraint: **C22 before any `*Tune` refit** (Decision 4). C21 → C22 → C23 is a chain; C24 is
standalone and mechanical.

Wave D depends on the whole of B and C: each conflict must be measured against the *new* model,
not the current one, or the result is unattributable.

**File contention.** Every item in B, C and D edits `src/Flight/FlightModel.cs`, and A1/A3 edit
`src/Flight/PlaneStats.cs`. **Do not run any two of these in parallel worktrees.** If parallelism
is wanted, the only safe split is A2 (a measurement harness, its own files) alongside one code
item.

---

# Wave A — Authored parameters and baseline

## A1 ☐ Plumb the authored flight globals into `PlaneStats`

**Goal.** Every constant the decoded model needs is read from the original's data at load time and
exposed on `PlaneStats`, so no downstream item has to hardcode a value the game authors.

**Evidence (confidence: traced).** The `player.json` parser in the executable reads each key by
name with a fallback baked in, so both the key list and every default are known — see "What the
data actually ships" above and the parser table in
[`docs/org/flightModel.md`](org/flightModel.md). `PlaneStats.cs` currently reads none of:
`lift_accel_rate`, `liftAOAs`, `maxAOA`, `highGs`, `lowGs`, the `turn_*`/`yaw_*` curve keys,
`high_speed_pitch_fade`, `drag_fade_speed`. `FlightModel.cs` instead carries `LiftAoaLo`/`Hi`,
`MaxAoaDeg` and `LiftLoadMax` as in-code constants sourced from those same keys by hand.

**Approach.** Extend `PlaneStats` with the missing globals, following the existing
`player.Float(...)` pattern and its default-carrying shape. Mirror the original's load-time
conversions **exactly**: speeds `× 0.44704` (MPH → m/s), angles cosined at load where the original
cosines them, raw where it does not (`highGs`/`lowGs` are plain G). Do **not** change
`FlightModel.cs` in this item — plumb the values and leave them unread, so the diff is inert and
any behaviour change in a later item is unambiguously that item's.

**Model recommendation.** medium — mechanical plumbing against a known key list, but the unit
conversions are easy to get subtly wrong and are load-bearing for every later item.

**Verify.** Dump the loaded values for a Bloodhawk and compare each against the decoded fallback
and against `player.json` as shipped; a key that silently falls back to its default is the failure
mode to look for, so **assert the fallback is not being taken**. Full 8-chapter `--freecam`
regression must be byte-identical — this item changes no behaviour, and if any number moves, the
plumbing is not inert.

**⚠ Traps.** `extracted/` does **not** contain `player.json`; read the live install. The MPH
conversion is the classic error — `turn_fade_in` 10 is 10 mph, not 10 m/s, and a missed `0.44704`
puts the knee 2.2× too high and will look almost plausible. `liftAOAs` and `maxAOA` are degrees
(now confirmed — see the disproven table); `highGs`/`lowGs` are **not** converted. Do not "tidy"
`FlightModel.cs`'s existing constants while here; B11 and D33 decide their fate.

## A2 ☐ Freeze a pre-change baseline across every pinned scenario

**Goal.** A committed, re-runnable table of the current model's numbers on every scenario this plan
will move, so later items are measured against a record rather than a memory.

**Evidence (confidence: direction-sound — the scenario list is known, the tolerances are a
judgement call).** `FlightModel.cs`'s own comments name the pinned measurements and where each came
from: the 360° roll at 2.05 s, sustained pitch ≈ 33 °/s, full-rudder 360° at 28.6 s, `CAP-05`'s
four zero-thrust drag points (0.36 / 1.11 / 2.82 / 3.74 m/s² at x = 0.25 / 0.35 / 0.46 / 0.50),
`CAP-01`'s 222.94 mph sustained-turn plateau, the 355.2 mph terminal dive, the 137.9 mph
1/8-throttle equilibrium, and the flight-envelope suite's α ≤ 2.9° scenarios. The Balmoral's
knife-edge at α = 5.1° is called out as a live boundary.

**Approach.** Capture, per airframe where the scenario is per-airframe: the existing
flight-envelope suite results, `--dump-flight=player_balmoral` and the equivalent for the
Bloodhawk, and the `sustained-turn-speed` / `sustained-turn-rate` scenarios. Commit the output as
a checked-in reference table. Record the *known-divergent* values too (the 32 °/s turn rate
against the original's 18.95; the knife-edge sag settling in ~1 s against the original's 36 s
linear drift) — a plan that only baselines what is correct cannot tell improvement from
regression.

**Model recommendation.** medium, low effort — running existing instruments and tabulating.

**Verify.** Re-run the capture twice and confirm the table reproduces; a baseline that is not
deterministic is not a baseline. Per `docs/verification.md`, note which instruments are known to
mislead here and cite the rule beside the affected row.

**⚠ Traps.** "An unchanged number is not evidence unless you've seen it able to fail" — for each
row, note what *would* move it, or the row is decoration. The knife-edge margin is thin: the
Balmoral sits 0.1° inside the lift ramp, so record its knife-edge α to more precision than feels
necessary.

## A3 ☐ Settle what supplies `ThrustFactor`

**Goal.** Know where the original's per-aircraft thrust scale comes from, so B13 scales thrust from
authored data rather than from a fitted constant.

**Evidence (confidence: lead-only).** The shipped Dynamics tuner's parameter list contains
`ThrustFactor` alongside the twelve other `dynamics` values — but `ThrustFactor` is **not** among
the `dynamics` keys documented in [`docs/formats/vehicle.md`](formats/vehicle.md). The remake
instead scales thrust by `EnginePower` (from `engines.json`) times the fitted `ThrustConst = 107`.
The working hypothesis is that engine power *is* the thrust factor, which would explain why the
current arrangement works at all. Unproven either way.

**Approach.** Census the shipped `vehicle.json` for any thrust-like key on the `dynamics` block or
its inheritance chain; if absent, check whether the executable's per-plane thrust field is
populated from `engines.json` at load. This may end in "the tuner exposes a field the shipped data
does not author, and engine power fills its role" — **that is a valid outcome**, and should be
recorded in `docs/org/flightModel.md` and `docs/formats/vehicle.md` rather than forced into a code
change.

**Model recommendation.** high — an open-ended decode question where the likely answer is a
negative result that has to be argued, not just observed.

**Verify.** Whatever the answer, it must account for all eleven player airframes, not one. If the
conclusion is "engine power is the thrust factor", show that the resulting per-airframe thrust
ordering matches the aircraft's published relative performance.

**⚠ Traps.** `PROJECT_CONTEXT.md` and `vehicle.md` warn that the Bloodhawk's stock engine is id 11
(power 0.62), **not** the level-1 row — a mistake that back-derives a constant ~32 % too large and
has already been made once here. Do not solve any thrust constant from the level-1 engine.

# Wave B — The aero core

## B11 ☐ Lift as the clamped demanded-G

**Goal.** Lift is computed as the original computes it — a demand vector, clamped to ±5/9 G and to
an aerodynamic ceiling — instead of as a fraction of gravity's cross-path component.

**Evidence (confidence: traced).** The full chain, with the airflow blend, the demand assembly and
the clamp, is in [`docs/org/flightModel.md`](org/flightModel.md) under "Lift — the wings deliver
the demanded G". The strongest corroboration is algebraic: gravity is applied as
`(nom_gravity / 9.82) × Weight` and level flight at zero incidence demands exactly `nom_gravity`,
so lift cancels weight **identically** rather than by tuning. The remake's `AlignRate` (TUNE, 4/s)
is the same quantity as the authored `lift_accel_rate` (fallback 1.2/s).

**Approach.** Replace `liftFrac` and its inputs with the demand construction: blend the relative
wind toward the nose across the `liftAOAs` window, form
`lift_accel_rate · (relativeWind − velocity)` with `nom_gravity` added on world-up, project onto
the body X/Y plane, divide by 9.82 for the load factor, clamp to `[−5, +9]`, then cap the resulting
force at `(0.75 − 0.15·Mach) · q · RefArea`. Drive `AlignRate` from the authored
`lift_accel_rate`. This retires `LiftSpeedFrac`, `LiftAoaLo`/`Hi` and `LiftLoadMax`.
**Do not remove `wingVert`** — Decision 2; D31 owns that question.

**Model recommendation.** high — the largest behavioural change in the plan, coupled to drag and
thrust, and it rewrites the term every other flight measurement is sensitive to.

**Verify.** Against A2's baseline: the flight-envelope suite, `CAP-01`'s 222.94 mph plateau, and
the knife-edge scenarios per airframe. Expect movement — the point is that it moves *toward* the
measurements. Nothing here is "verified" until B14.

**⚠ Traps.** The disproven table's row 1 is this item's minefield: the clamp is on a **load
factor**, not an angle, and re-deriving it as degrees will produce a model that looks right at
small inputs and diverges at the limits. `Alpha` stays an emergent lag, and the existing comment
warning against describing it as modelled incidence stays true. The Balmoral knife-edges 0.1°
inside the old lift ramp — whatever replaces that ramp must be checked against the Balmoral
specifically, not the Bloodhawk.

## B12 ☐ Drag as the original's polar

**Goal.** Drag is the original's parabolic polar in the delivered lift coefficient, so induced drag
falls out of the model instead of being a separately fitted term.

**Evidence (confidence: traced).** `C_D = 0.73 · (0.12 + 0.8·C_L + 0.5·C_L²)`, applied as
`q · RefArea · DragFactor · C_D` opposing velocity. The parasite term is identical for every
aircraft — airframes differ only through `DragFactor` and `RefArea`, both authored.

**Approach.** Replace the `DragExpLow`/`DragExpHigh` power law **and** the `InducedDragCoef`
`sin²α` term with the polar, taking `C_L` from B11's delivered lift. Retires `DragExpLow`,
`DragExpHigh`, `InducedDragCoef` and `MaxAoaDeg`'s drag role — four fitted constants for two
authored ones.

**Model recommendation.** high — small in code, but it is one of the three jointly-pinned terms and
touches every speed measurement in the suite.

**Verify.** `CAP-05`'s four zero-thrust drag points are the cleanest probe in the set (no thrust
term at all) and are the primary check. Then the terminal dive at 355.2 ± 6 mph and the
`sustained-turn-speed` plateau. Take B12 to B14 before calling it.

**⚠ Traps.** The existing comment is emphatic that these three constants are pinned *jointly* and
that moving one without refitting the others silently breaks whichever measurement you were not
watching — that warning applies to *replacing* them too. `BL-148` (the unexplained slope step at
`fd_speed`, currently modelled as two exponents) may simply dissolve here: the polar is not a
power law in speed at all, so check whether the seam still needs explaining before carrying
`BL-148` forward. The old curve was 4–21× weaker below cruise than what it replaced and is owed a
re-playtest against the original deceleration complaint — that debt transfers to this item.

## B13 ☐ Thrust: linear throttle and the Mach/altitude curve

**Goal.** Thrust responds to the lever as the original's does — linearly — with the Mach/altitude
curve behind it, so part-throttle behaviour is right for a reason rather than by exponent.

**Evidence (confidence: traced for the linearity; the curve's exponent is not).** The original
multiplies available thrust by throttle directly. The remake's `ThrottleExp = 1.236` was solved
from the measured 137.9 mph 1/8-throttle equilibrium *given the current drag shape*; with the polar
(B12), low-speed drag rises as `W²/(qS)` and a linear lever is predicted to reproduce that
equilibrium unaided. ⚠ The thrust-available curve's `pow` operands were **not** recovered from the
binary — see the trap.

**Approach.** Make throttle a linear multiplier and drop `ThrottleExp`. Implement the
thrust-available curve's known shape (parasite term with a linear Mach correction, divided by Mach
— the propeller constant-power form), scaling by whatever A3 settles as the thrust factor. Leave
the attitude-dependent terms (0.24 / 0.13) **out of this item** — they belong to D32, which owns
the conflict with `ClimbGravityScale`.

**Model recommendation.** high — the unrecovered exponent means this item has to reason about what
is and is not determined by the data.

**Verify.** The 1/8-throttle equilibrium at 137.9 mph is the discriminating test and the reason
this item exists: full throttle is `1^k = 1` for any exponent, so **only part-throttle scenarios
can see this change**. Also re-run the level acceleration (150 → 290 mph in 3.76 s).

**⚠ Traps.** If linear throttle plus the polar does *not* reproduce 137.9 mph, the honest outcome
is to record `ThrottleExp` as a real, named divergence — **not** to reintroduce it silently as a
fudge. The missing `pow` operands mean absolute top speeds cannot be predicted from first
principles; if the curve cannot be closed, scale to the measured `TopSpeed` and say so in the code
comment. Do not solve any thrust constant from the level-1 engine row (see A3).

## B14 ☐ Joint refit and re-measurement of the aero group

**Goal.** The B11–B13 group is validated *together* against every pinned measurement, and any
surviving fitted constant is named, justified and added to the TUNE list.

**Evidence (confidence: direction-sound — the measurements are pinned; which constant absorbs a
residual is a judgement call).** The three terms have no closed form between them (the couplings
run drag → speed → bank → wing verticality → alignment → incidence → drag), which is why the
existing `InducedDragCoef` was swept against the real integrator rather than derived. The same will
be true of whatever residual survives here.

**Approach.** Re-run A2's full baseline table against the new model. For each row that misses,
attribute the miss to a specific term before touching anything. Where a residual genuinely cannot
be attributed, prefer leaving it visible as a named TUNE over distributing it across three
constants until the table goes green.

**Model recommendation.** max — this is the judgement call the whole wave funnels into, with the
highest blast radius and the least mechanical answer.

**Verify.** Every row of A2's table, plus the full 8-chapter `--freecam` regression. Per
`docs/verification.md`, cite the rule that bites for each instrument used.

**⚠ Traps.** The failure mode is a green table achieved by three compensating errors — the exact
trap the current model fell into, and the reason `ThrottleExp` exists. If a row can only be met by
moving a constant the decode says is authored, **stop and record the conflict** rather than
overriding authored data with a fit.

## B15 ☐ Stall speed per airframe

**Goal.** Stall speed is derived from the aircraft's own weight and reference area, not from a
fixed fraction of `fd_speed`.

**Evidence (confidence: traced).** The stall condition is that maximum available lift can no longer
carry weight, giving `V_stall = sqrt(2·Weight / (0.75·ρ·RefArea))`. For the fallback airframe this
yields 33.8 m/s against the current `StallSpeedFrac = 0.25 × 135 = 33.75` — near-identical, which
is why the current model has not visibly failed. Different wing loadings will diverge.

**Approach.** Replace `StallSpeedFrac`'s role in `isStalled()` with the computed speed. Keep
`StallWarnFrac` separate — the two thresholds are measured as genuinely different numbers (the lamp
leads the break by 2.64 s / 14.9 mph, confirmed inside a single clip), and nothing in the decode
touches the lamp.

**Model recommendation.** medium — a contained formula swap with a clear per-airframe check.

**Verify.** Compute the new stall speed for all eleven airframes and compare against
`0.25 × fd_speed` per aircraft; the Balmoral (a bomber, and the airframe that already sits at
every margin) is the one most likely to move. Then re-run the stall scenarios.

**⚠ Traps.** Do not collapse the two stall thresholds — a model driving both cues off one number is
wrong by construction, and that is already recorded. The near-perfect agreement for the fallback
airframe is a coincidence of that aircraft's wing loading and must not be read as validation of the
0.25 fraction in general.

# Wave C — Rotation and control authority

## C21 ☐ Yaw authority: the original's speed table

**Goal.** Rudder authority follows the original's speed curve — effectively flat across the whole
flight envelope — rather than sweeping with airspeed.

**Evidence (confidence: traced).** The original's yaw curve is `yaw_low_speed` (0.05) up to
`yaw_fade_in` (10 mph), ramping to 1.0 at `yaw_max` (22.5), falling to `yaw_high_speed` (0.1) at
`yaw_fade_out` (45), and **flat 0.1 above that**. With `fd_speed` at 302 mph, the 45 mph knee sits
at 0.149 fd, so all of normal flight is on the flat. The current `eff = 1.4 − clamp(Speed/fd, 0.25,
1.15)` instead sweeps 0.25 → 1.15 across the envelope; its own comment already says "Still not same
as original".

**Approach.** Implement the piecewise table from A1's authored values and apply it to yaw only —
the original applies a *different* curve to each axis, and pitch/roll are handled by C24 and left
alone respectively. `YawTune` must be refit to preserve the measured full-rudder 360° at 28.6 s.

**Model recommendation.** medium — a well-specified table, with one refit against a known
measurement.

**Verify.** Full-rudder 360° at 28.6 s (the pinned measurement), plus rudder response sampled at
low speed and at cruise to confirm the curve is flat where the decode says it is.

**⚠ Traps.** `eff` is currently applied to yaw *only* — do not "fix" that by extending it to the
other axes; the decode confirms the axes genuinely differ. The steady rate is what `YawTune` pins;
per `BL-147`'s warning, do not chase transient shape by moving it.

## C22 ☐ Bank→yaw and bank→pitch coupling

**Goal.** Banking turns the aircraft through the original's coupling terms, not solely through the
flight path chasing the nose.

**Evidence (confidence: traced).** Two constants hardcoded in the executable and present in no data
file: bank contributes to yaw at 0.205 and to pitch at 0.165 (with an extra contribution when
inverted), both scaled by the bank component and applied per tick as angular rate. The remake has
**no** bank-to-turn coupling at all. This is the leading candidate for the known turn-rate error —
the remake sweeps heading at ~32 °/s where the original sweeps 18.95.

**Approach.** Add both terms to the `BodyRates` accumulation. Then refit `InducedDragCoef` (or its
B12 successor) — the existing comment explicitly predicts this: the current value absorbs the
turn-rate error and "close the rate gap and this must be refitted".

**Model recommendation.** high — small in code, but it changes the mechanism by which the aircraft
turns, and it invalidates a constant fitted on top of the old behaviour.

**Verify.** `sustained-turn-rate` against the original's measured 18.95 °/s — currently
informational precisely because of this gap, and the item that should promote it to asserting. Then
`sustained-turn-speed`'s 222.94 mph plateau, which the refit must preserve.

**⚠ Traps.** These constants are in **no data file** — do not go looking for them in `player.json`
and conclude they are absent from the game. Decision 4: no `*Tune` refit until this lands, or the
refit is wasted. Landing this without refitting the induced-drag term will move the turn plateau,
and it will look like B12 broke.

## C23 ☐ `return_rate` as a weathervane torque

**Goal.** `return_rate` restores the nose toward the velocity vector, as the original does, instead
of acting as extra axis damping when a stick is centred.

**Evidence (confidence: direction-sound).** The original applies `return_rate` as a torque along
`cross(−nose, velocity)` — a restoring torque proportional to displacement, i.e. a spring, giving
second-order response. The remake folds it into the damping coefficient on each axis, giving a
first-order lag. ⚠ **Hypothesis worth testing here:** `BL-147` reports the original's pitch
transient rolling off ~3.5× steeper than the ceiling of a single first-order lag, and a
spring-damper rolls off exactly twice as steep in dB terms. Mechanism and symptom match; that is a
lead, not a finding.

**Approach.** Remove `return_rate` from the `damp` vector and add the weathervane torque. Note the
original applies it to **player aircraft only**.

**Model recommendation.** high — it changes the order of the rotational response, and the payoff
(closing `BL-147`) depends on getting the shape right rather than the magnitude.

**Verify.** The square-wave pitch-cadence sweep that `BL-147` was raised from is the discriminating
instrument — a first-order lag cannot produce the measured rolloff, so this either closes it or
demonstrates the mechanism is something else. The three steady rates (roll 2.05 s, pitch ≈ 33 °/s,
rudder 28.6 s) must be **unchanged**: this is a transient-shape change, not a steady-rate one.

**⚠ Traps.** If the steady rates move, the torque has been added with the wrong sign or scale — the
weathervane should vanish at zero misalignment, so cruise must be untouched *by construction*, not
by tuning. `BL-147` explicitly warns against "fixing" the transient by moving the `*Tune`
constants; that warning stands, and this item is the alternative it was waiting for.

## C24 ☐ Pitch high-speed fade and exponential angular damping

**Goal.** Two small mechanical corrections: pitch authority fades at high speed as the original's
does, and angular damping decays exponentially rather than linearly.

**Evidence (confidence: traced).** Pitch authority is flat until `high_speed_pitch_fade[0]`
(500 mph) and falls linearly to zero at `[1]` (600 mph) — reachable, since `MaxDiveSpeedFrac`
1.75 × 302 = 528 mph. Damping in the original applies the torque and then decays the rate by
`exp(−dt · damp)`; the remake's `(cmd − rates·damp)·dt` is the explicit-Euler approximation of the
same thing, agreeing to first order.

**Approach.** Add the pitch fade from A1's authored values. Switch damping to the exponential form.
Keep the two changes in one item because both are contained and neither is independently
observable at normal speeds.

**Model recommendation.** medium, low effort — both are small, well-specified, and low-risk.

**Verify.** Pitch response sampled in a terminal dive above 500 mph (the only place the fade is
observable) against normal-speed pitch response, which must not move. For the damping change,
confirm the steady rates are unchanged and check behaviour at a deliberately large timestep, where
the two forms diverge.

**⚠ Traps.** The damping change is **not** the answer to `BL-147` — the two forms agree to first
order and the exponential does not produce a steeper rolloff. C23 owns that question; do not let
this item claim it. The pitch fade is invisible in every normal-flight scenario, so an unchanged
suite proves nothing here — construct a case that can actually fail.

# Wave D — The open conflicts

## D31 ☐ Bank-independent lift vs the measured knife-edge sag

**Goal.** Resolve whether the original's lift is genuinely bank-independent, and decide the fate of
`wingVert` on evidence rather than on either source alone.

**Evidence (confidence: lead-only — two sources in direct conflict).** The decoded lift demand is
projected onto the body X/Y **plane**, and its magnitude is the length of that projection, so at
90° bank the body X axis is vertical and full lift remains available — the decode says lift does
not depend on bank. Against that, the footage shows the original's knife-edge sagging and
eventually spiralling in (`BL-247`: a linear drift of 0.69–0.89 °/s over 36 s with no equilibrium),
and `wingVert` is load-bearing for a departure the flight-envelope suite asserts. Both cannot be
right; neither has been shown wrong.

**Approach.** Reconcile, do not pick. Candidate explanations to test in order: the sag originates in
the **nose** rather than in lift (the weathervane torque from C23 plus the absence of any
roll-hold), rather than in a bank-dependent lift term; or the `liftAOAs` airflow blend behaves
differently at sustained high bank than the static read suggests. Measure the new post-C23 model's
knife-edge against the footage before changing any lift term.

**Model recommendation.** max — a contested-evidence question where the likely resolution is
subtle and the wrong call silently breaks a departure the suite depends on.

**Verify.** The original's knife-edge takes at 143 and 300 mph, against the new model, per
airframe. The discriminating signature is the *shape*: the original drifts linearly for 36 s with
no equilibrium, where a bounded sag settles within a second.

**⚠ Traps.** `KnifeNoseSag`'s magnitude is measured (≈4° immediate step) but its **bound is known
wrong**, and the existing comment warns explicitly against retuning it to close the gap — raising
the bound destroys the first 3 s, where the original holds altitude and the remake does not. The
Balmoral knife-edges 0.1° inside the old lift ramp: any change here must be checked against it.
Removing `wingVert` on the decode's word alone is exactly what Decision 2 forbids.

## D32 ☐ Attitude-dependent thrust vs `ClimbGravityScale`

**Goal.** Establish which mechanism the original actually uses for climb behaviour, and land one of
them rather than both.

**Evidence (confidence: lead-only — the decode is traced, its reconciliation with the measurement
is not).** The original scales thrust by nose attitude: down to ~0.66× when climbing vertically
(two terms, 0.24 and a one-sided 0.13) and up to 1.24× when diving. That **penalises** a climb.
`ClimbGravityScale = 0.6` does the opposite — it spares a climbing aircraft, and was introduced
because the original was measured as holding speed better in a sustained climb than plain energy
exchange predicts. The two run in opposite directions.

**Approach.** Land this **after** B14, not before: the current `ClimbGravityScale` was fitted on top
of the old drag and thrust shapes, and the leading hypothesis is that it is absorbing their error
rather than modelling climb retention. Add the attitude terms, remove `ClimbGravityScale`, and
measure the sustained climb against the footage. If climb retention survives without it, the
conflict was an artefact; if it does not, the decode is incomplete and that must be recorded.

**Model recommendation.** high — a genuine contradiction between a traced mechanism and a
measurement, where the resolution is probably "the fitted constant was absorbing something else"
but must be shown rather than assumed.

**Verify.** A sustained full-throttle climb, speed against time, versus the original's footage —
the measurement `ClimbGravityScale` was introduced to satisfy. Both constants removed and the
attitude terms in place is the target state; anything less must be justified.

**⚠ Traps.** Do not change both at once and read the net result — Decision 5. Sign errors here are
easy and self-consistent-looking: the decoded terms key off the world-up component of the body Z
axis, and the nose points along **−Z**, so a dropped sign inverts climb and dive and will still
produce plausible-looking flight.

## D33 ☐ The G and AOA limiters — live or inert in this install?

**Goal.** Determine whether the original's G and AOA control limiters ever engage with this
install's authored values, and implement them only if they do.

**Evidence (confidence: lead-only).** The original reduces pitch and yaw authority above
`highGs[0]`, reaching zero at `highGs[1]`, and separately reduces it toward zero at `maxAOA` — both
gating **only input that opposes the current rotation**. But the lift clamp is a hard ±5/9 G, and
this install is reported as authoring `highGs [9, 15]`, so the limiter may begin exactly where lift
is already capped and never bite. The executable's own fallbacks are `[5, 9]`, where it clearly
would.

**Approach.** Read the authored values via A1 and compute whether the limiter's threshold is
reachable given the lift clamp. If it is unreachable, **record the disproof and implement nothing**
— that is the successful outcome. If it is reachable, implement both limiters with the
opposing-input gate.

**Model recommendation.** high — mostly analysis, and the valuable answer is likely a negative one
that has to be argued from the interaction of two clamps.

**Verify.** If implemented: a maximum-G pull and a high-AOA departure, checking that authority
falls only against the rotation and not with it. If disproven: show the threshold is unreachable
for all eleven airframes, not just one.

**⚠ Traps.** The asymmetry is the whole subtlety — a limiter that reduces *all* input rather than
opposing input will damp entry into a manoeuvre instead of recovery from it, which is backwards and
will feel like sluggish controls. Related, and **not** to be resolved by guessing: the atmosphere
band-select threshold has only a read reference and a static zero in the binary, so its runtime
value is unknown; the dense band is established by arithmetic, not by reading the flag. Do not
"fix" that flag on the strength of this item.
