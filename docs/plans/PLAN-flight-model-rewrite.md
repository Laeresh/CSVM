# Flight model — rewrite onto the decoded original

**COMPLETE** (2026-08-09). Written 2026-08-09; every wave landed the same day. Kept for its
evidence and its recorded dead ends — read it as history, not as live work; the open threads it
leaves are listed under "What this plan leaves open" at the end.

This plan ports `src/Flight/FlightModel.cs` from a video-calibrated approximation onto the
original's actual flight model, decoded from `crimson.exe` and written up in
[`docs/org/flightModel.md`](../org/flightModel.md). The decode supersedes video calibration wherever
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
| 5 | Remove `ClimbGravityScale` when the attitude-thrust terms land? | **Settled in D32: yes, it is retired.** Moved one at a time and measured between, as this decision required: removing the constant ALONE improves the sustained climb (276.66 → 257.74 mph against a measured 163.05), so it was absorbing the pre-B12/B13 shapes' error rather than modelling climb retention. |
| 6 | Where does the decode live, and is it a `docs/formats/` page? | **`docs/org/flightModel.md`** — it documents the original's *executable*, not a data format, so it sits in a new `docs/org/` area rather than `docs/formats/`. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Angle of attack in degrees *is* the load factor — 9° AOA = 9 G." | Traced the coefficient function's third argument back through its caller: the quantity passed is **already a demanded load factor in G**, derived from the airflow/velocity difference. The final formula (lift = clamped G × Weight) survives; the mechanism does not. Anyone re-reading the coefficient function in isolation will re-derive this — the argument name invites it. |
| 2 | "`liftAOAs [5, 9]` are the edges of a load-factor ramp" (the standing reading in `FlightModel.cs`). | The original takes their **cosine** and uses them as the window over which the *relative wind is blended toward the nose*. Same two numbers, entirely different mechanism. Also settles `BL-095`'s units question for these keys. |
| 3 | "The atmosphere's thin band is the operative one." | Arithmetic: the thin band puts the fallback airframe's stall at 309 mph. The dense band puts it at 75.5 mph, which is correct. The band-select threshold has only a read reference in the binary — see D33's trap. |
| 4 | "Thrust is sublinear in the throttle lever — the original's own behaviour." | **Dead (B13).** The original multiplies available thrust by throttle linearly, and with the drag polar read correctly (row 6) a linear lever reproduces the measured 1/8-throttle equilibrium unaided — 134.5 mph against 137.9 ± 6. `ThrottleExp` was an artefact of the drag shape, exactly as predicted, and is retired rather than promoted. |
| 6 | "The drag polar is parabolic in the delivered lift coefficient — `C_D = 0.73·(0.12 + 0.8·C_L + 0.5·C_L²)`." | **Dead (B13), and it was this plan's own reading for two items.** The polynomial's variable is **Mach**: `FUN_0041ada0`'s two operand reads are both `[esp+4]`, and the caller pushes Mach last. `C_L` is passed and never read, so the original has **no induced drag at all**. The decompiler emits `Drag(Mach, C_L)` and the polar reading is what anyone would infer from the signature plus the coefficients — only the raw bytes settle it. Any tuning done against the `C_L` version was tuning a term the original does not have. |
| 5 | "Rudder authority is flat 0.1 across the flight envelope, the pitch fade bites at 500 mph, and the G limiter engages at 5 G." | All three read the **executable's compiled fallbacks** as if they were the game's values. `BL-095` records what this install actually authors: the yaw curve *declines* 1.0 → 0.17 between 50 and 400 mph, the pitch fade is authored at 1000/1001 mph and is unreachable, and `highGs [9, 15]` puts the limiter past the ±5/9 lift clamp so it never engages. **This is the trap of the whole plan**: a fallback is evidence of intent, not of behaviour. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, B11, B12, B13, B15, C21, C22, C24 | Confirm the trace against [`docs/org/flightModel.md`](../org/flightModel.md), then implement. |
| **Direction sound, magnitude a judgement call** | A2, B14, C23 | The *what* is settled; the *how much* is TUNE — add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only — no mechanism yet** | A3, D31, D32, D33 | Budget for investigation; **these may end in a disproof, and that is a success.** |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The full decode is [`docs/org/flightModel.md`](../org/flightModel.md) — read it before any item. It
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
- **Drag** = `q · RefArea · DragFactor · 0.73 · (0.12 + 0.8·M + 0.5·M²)` — a polar in **Mach**, with
  no induced-drag term anywhere (corrected in B13; see the disproven table's row 6).
- **Thrust** = `EnginePower · RefArea · T_avail(M) · throttle`, where
  `T_avail = q_ref · 0.73·(0.12 − M/60) / (M · pow(1.33·k, 1.41·M))` and
  `q_ref = ½ρ((0.84M + 0.112)·a)²`. It **rises** with speed.
- **Gravity** resolves to exactly `nom_gravity` (20 m/s² in this install).
- **Control authority** is three separate speed curves: roll flat, pitch flat (its fade is authored
  unreachable — see below), yaw ramping to 1.0 at 50 mph then **declining to 0.17 at 400 mph**.
- **Two hardcoded coupling constants**, 0.205 (bank → yaw) and 0.165 (bank → pitch), present in no
  data file.
- **Authored keys not currently read by `PlaneStats`:** `lift_accel_rate`, `liftAOAs`, `maxAOA`,
  `highGs`, `lowGs`, `turn_fade_in`/`_out`, `yaw_low_speed`/`_high_speed`/`_fade_in`/`_max`/
  `_fade_out`, `high_speed_pitch_fade`, `drag_fade_speed`.

⚠ **The extracted set under `extracted/` does not contain `player.json`** — these globals must be
read from the live install. A1 exists partly to establish that path.

⚠ **The decode quotes the executable's compiled fallbacks; this install authors different numbers,
and in four places they change the conclusion.** `backlog.md` `BL-095` holds the authored set, and
[`docs/org/flightModel.md`](../org/flightModel.md)'s corrections table reconciles the two. The
differences that matter: the yaw curve **declines** across the envelope rather than going flat
(C21); the pitch high-speed fade is authored **unreachable** (C24); the G and AOA limiters are
authored **inert** (D33); and `lift_accel_rate` is **0.75**, not the fallback 1.2 (B11). **Build
every item from the authored values, never from the fallbacks quoted in the decode's body text.**

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

1. ☑ Plumb the authored flight globals into `PlaneStats`
2. ☑ Freeze a pre-change baseline across every pinned scenario
3. ☑ Settle what supplies `ThrustFactor`

### Wave B — The aero core

11. ☑ Lift as the clamped demanded-G
12. ☑ Drag as the original's polar
13. ☑ Thrust: linear throttle and the Mach/altitude curve
14. ☑ Joint refit and re-measurement of the aero group
15. ☑ Stall speed per airframe

### Wave C — Rotation and control authority

21. ☑ Yaw authority: the original's speed table
22. ☑ Bank→yaw and bank→pitch coupling
23. ☑ `return_rate` as a weathervane torque
24. ☑ Pitch high-speed fade and exponential angular damping — Wave C complete

### Wave D — The open conflicts

31. ☑ Bank-independent lift vs the measured knife-edge sag
32. ☑ Attitude-dependent thrust vs `ClimbGravityScale`
33. ☑ The G and AOA limiters — live or inert in this install? — **Wave D complete; plan complete**

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

## A1 ☑ Plumb the authored flight globals into `PlaneStats`

**Goal.** Every constant the decoded model needs is read from the original's data at load time and
exposed on `PlaneStats`, so no downstream item has to hardcode a value the game authors.

**Evidence (confidence: traced).** The `player.json` parser in the executable reads each key by
name with a fallback baked in, so both the key list and every default are known — see "What the
data actually ships" above and the parser table in
[`docs/org/flightModel.md`](../org/flightModel.md). `PlaneStats.cs` currently reads none of:
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

## A2 ☑ Freeze a pre-change baseline across every pinned scenario

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

## A3 ☑ Settle what supplies `ThrustFactor`

**Outcome (landed 2026-08-09).** The hypothesis is confirmed, at source: `ThrustFactor` is the
`power` column of `engines.json` for the row the def's `engine` property selects. Full chain in
[`docs/org/flightModel.md`](../org/flightModel.md#thrustfactor-is-the-engines-power-factor--resolved)
— parser writes it to def `+0x128`, copied to runtime `+0x66c`, read by the force accumulator as
`Thrust = ThrustFactor · RefArea · thrustAvailable(Mach) · throttle`. No data file authors a
thrust key (all 24 `dynamics` blocks author the same ten), and the eleven-airframe rank test
against `fd_speed` gives Spearman +1.000. **Two consequences for B13:** thrust scales with
`ref_area`, **not** `1/veh_weight` as the remake does; and `fd_speed` is very likely **not** the
level-flight equilibrium (see the caveat in the decode) — do not build B12/B13 on that assumption
without settling it. No code changed here.

**Goal.** Know where the original's per-aircraft thrust scale comes from, so B13 scales thrust from
authored data rather than from a fitted constant.

**Evidence (confidence: lead-only).** The shipped Dynamics tuner's parameter list contains
`ThrustFactor` alongside the twelve other `dynamics` values — but `ThrustFactor` is **not** among
the `dynamics` keys documented in [`docs/formats/vehicle.md`](../formats/vehicle.md). The remake
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

## B11 ☑ Lift as the clamped demanded-G

**Outcome (landed 2026-08-09).** Implemented as decoded: the airflow is blended toward the nose
across the `liftAOAs` cosine window, `lift_accel_rate·(wind − v)` plus `nom_gravity` on world-up is
projected onto the body X/Y plane, and the wings deliver `clamp(|·|/9.82, −5, +9)` G capped at
`(0.75 − 0.15·Mach)·q·RefArea / Weight` (dense band, imperial q). Gravity is now applied in full and
the level-flight identity holds exactly — `level-top-speed`, `level-speed-near-cap`, `accel-150-290`
and `eighth-throttle-speed` are unmoved to the last printed digit, and `terminal-dive` tightened
355.26 → 355.20 mph (α 0.6 → 0.0°) because the old cross-path fraction was leaking a little sink
into a dive. `AlignRate` (TUNE 4/s) is retired for the authored 0.75/s, which raises the alignment
lag in a full pull from α ≈ 8.9° to ≈ 18.4°. **Two asserted rows now fail and are left failing:**
`yaw-360` 29.17 → 23.82 s (target 28.6 ± 3) and `sustained-turn-speed` 223.03 → 88.45 mph (target
222.94 ± 5). Both are the same cause and both are B12's to resolve — the lift vector is tilted back
by α and therefore now supplies real induced drag, on top of `InducedDragCoef`, which was fitted to
absorb the whole of it; the yaw row is collateral (mean speed 298.8 → 270.6 mph raises `eff`).
`sustained-turn-rate` is unmoved across all eleven airframes (32.33 → 32.40 on the Bloodhawk), which
confirms it is C22's and not this item's. Sink improved everywhere (Bloodhawk −2.40 → −0.80 ft/s,
Balmoral 39.41 → 21.31): lift is genuinely available in bank now. ⚠ **And that is D31's conflict,
live:** a neutral-stick 90°-bank hold that used to fly the Bloodhawk into the ground (−2304 m in
35 s) now settles into a 3.6 m/s descent at the nose-sag angle, α = 0°, losing 129 m — the
knife-edge departure is gone on both airframes checked, Balmoral included. Four goldens moved
(`empty-stage`, `c1-flight`, `c1-destroy-effects`, `c1-crash` — exactly the four that fly a plane);
the manifest is updated in this commit. `CAP-05`'s four drag points are untouched by construction.

**Goal.** Lift is computed as the original computes it — a demand vector, clamped to ±5/9 G and to
an aerodynamic ceiling — instead of as a fraction of gravity's cross-path component.

**Evidence (confidence: traced).** The full chain, with the airflow blend, the demand assembly and
the clamp, is in [`docs/org/flightModel.md`](../org/flightModel.md) under "Lift — the wings deliver
the demanded G". The strongest corroboration is algebraic: gravity is applied as
`(nom_gravity / 9.82) × Weight` and level flight at zero incidence demands exactly `nom_gravity`,
so lift cancels weight **identically** rather than by tuning. The remake's `AlignRate` (TUNE, 4/s)
is the same quantity as the authored `lift_accel_rate`, which this install sets to **0.75/s**
(`BL-095`) — a factor of ~5 apart, so expect this to move the nose-chase visibly.

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

## B12 ☑ Drag as the original's polar

**Outcome (landed 2026-08-09).** Implemented as decoded: `C_D = 0.73·(0.12 + 0.8·C_L + 0.5·C_L²)`,
`Drag = q·RefArea·DragFactor·C_D` opposing velocity, with `C_L` read back out of B11's *delivered*
lift as `loadFactor·VehWeight / (q·RefArea)` (imperial q from the dense band, `veh_weight` and
`ref_area` in the data's own weight/area units; force → acceleration is `×9.82/Weight`, the same
conversion lift uses). `DragExpLow`, `DragExpHigh`, `InducedDragCoef` and `MaxAoaDeg` are gone with
their four config keys, and `Alpha` is now instrument-only — no force term reads it. **The double
count B11 left is retired**: a pull now costs speed through the `C_L` it produced and through the
lift vector's own tilt in the force sum — both of them mechanisms the original has — with no third,
fitted term stacked on top.
⚠ **The item's own primary check fails, and the failure is in the mechanism, not in a fit.**
`CAP-05`'s four zero-thrust points want 0.36/1.11/2.82/3.74 m/s²; the polar gives
**6.27/6.99/7.60/7.97** (+1641%/+530%/+169%/+113%). The reason is structural: at a fixed load factor
the polar's linear term is `q·S·0.8·C_L = 0.8·n·Weight`, i.e. **speed-independent**, so the polar has
a drag *floor* (≈4.3 m/s² on the Bloodhawk at the `n = 20/9.82 = 2.04` of level flight) where the old
power law went to zero with speed. No constant in this item can move that; it is the decoded curve.
Recorded as a decode-vs-footage conflict for B14, not refitted.
The rest of the envelope moved with the drag scale, and **that movement is B13's**: at the
Bloodhawk's fd_speed the polar asks 16.8 m/s² where the stale `ThrustConst = 107` still supplies
34.92, a factor 2.08, so every full-throttle speed settles high — `level-top-speed` 301.99 → **475.78**
(+58.4%), `level-speed-near-cap` the same, `terminal-dive` 355.26 → **528.48** (now *pinned to the
`MaxDiveSpeedFrac` backstop*, which binds for the first time), `accel-150-290` 3.75 → 2.68 s,
`eighth-throttle-speed` 137.87 → 148.46. The two rows B11 left red both moved back toward their pins
and overshot with the envelope: `sustained-turn-speed` 88.45 → **252.68** mph (target 222.94, +13.3%)
and `yaw-360` 23.82 → **47.22** s (target 28.6, and its mean speed is 453 mph — collateral again).
`sustained-turn-rate` is unmoved (32.33 → 32.34), still C22's. `roll-360`, `pitch-rate`,
`altitude-cap` and `sustained-turn-sink` are unmoved/ok.
**Diagnostic that separates the two terms** (temporary edit to `ThrustConst`, reverted; `--det`
clears `config.json`, so a config override cannot do this): at a thrust scale that lands
`level-top-speed` near 302 mph (≈52), `yaw-360` lands at ≈25 s — inside its ±3 — while
`sustained-turn-speed` falls to ≈125 mph, `terminal-dive` stays ≈510 mph and `accel-150-290` blows out
to ≈11 s. **No constant thrust scale satisfies the envelope with this polar**, and the direction of
the residuals is exactly what a propeller constant-power thrust curve (`T ∝ 1/V`, B13) fixes: more
thrust at 150 mph, much less at dive speed. That is this item's strongest signal that B13 is the
missing half, and the ten un-measured airframes agree — the Balmoral (`drag_factor` 1.7,
`ref_area` 1100) cannot fly at all on 1/weight-scaled thrust (level top speed 44 mph), and A3's
`EnginePower·RefArea` scaling is what would give it back.
**`BL-148` disposition: dissolved, and there was nothing left to delete.** The slope step at
`fd_speed` was an artefact of a curve *normalized* at `fd_speed`; the polar has no seam, no
normalization and no dependence on `fd_speed` at all, so the step needs no explanation. `backlog.md`
carries no live `BL-148` entry (the ID was retired 2026-08-04 by `CAP-06`); it survived only as the
code comment this item deleted.
**Re-playtest debt, transferred and still owed:** the old curve was 4–21× weaker below cruise than the
blend it replaced, against a user report of a throttled-back plane barely decelerating. The polar is
*much* stronger down there (the floor above), so the complaint's direction has reversed — `decel-290-150`
6.48 → 5.73 s against the original's 7.04. **This is owed to a human playtest, not to an instrument**,
and stays owed past B14.
Tests: engine 29/29 clean, goldens 4 moved (`empty-stage`, `c1-flight`, `c1-destroy-effects`,
`c1-crash` — exactly the four that fly a plane; manifest updated here, `c1-flight` eyeballed), units
696/697 with `FlightEnvelopeTests` red on `level-top-speed` as above. `cap05-drag-points.ps1` now
evaluates the polar and is the CAP-05 row's re-run command.

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

## B13 ☑ Thrust: linear throttle and the Mach/altitude curve

**Outcome (landed 2026-08-09).** **The `pow` operands were recovered**, and recovering them turned
up a second, larger error in the decode. `FUN_0041acf0` is
`thrustAvail(Mach, atm)` with Mach floored at 0.1, and the lost FPU helper call is
`MSVCRT!_CIpow` via the thunk at `0x5f7016`: **base `1.33 · atm->k`, exponent `1.41 · Mach`**
(`0x6034a4` / `0x6034a0`). Full formula, constants and addresses are in
[`docs/org/flightModel.md`](../org/flightModel.md#thrust-available--resolved-pow-operands-recovered).
⚠ **The curve RISES with speed** (+35 % from 150 to 500 mph) — the `/M` is real but `q_ref` is
taken at a reference speed that *tracks* the current speed, so the net is linear in Mach. B12's
"the residuals want a `T ∝ 1/V` curve" prediction is disproved, and the thing that actually falls
with speed is the thrust *margin*.
**The second finding, and the item's real result: the drag polar's variable is MACH, not `C_L`.**
`FUN_0041ada0` is 33 branch-free bytes with one call site; both operand reads are `disp8 = 0x04`,
i.e. `[esp+4]`, and the pushes at `0x48fd74`/`0x48fd75` put **Mach** there — `C_L` is the dead
`[esp+8]`. So `C_D = 0.73·(0.12 + 0.8·M + 0.5·M²)`, and **the original has no induced drag at
all** (every use of the `C_L` slot in `FUN_0048fc40` was enumerated). B12's polar was the same
three coefficients read against the wrong variable, which is the natural decompiler mistake here.
Corrected in this item because B13 is unmeasurable without it: the decoded thrust curve on the
`C_L` polar pins every speed against `MaxDiveSpeedFrac`.
**Taken together the aero core now closes from authored data with ZERO fitted constants.**
`ThrustConst = 107` and `ThrottleExp = 1.236` are both gone, with their config keys; thrust is
`EnginePower · RefArea · T_avail(M) · throttle`, force → accel `× 9.82 / Weight`. The
level-equilibrium solve `EnginePower·T_avail(M) = q·DragFactor·C_D(M)` is independent of ρ and `a`
and reproduces **nine of eleven** airframes' authored `fd_speed` inside 1 % (autogyro 0.94×,
Balmoral 0.71× are the outliers) — which also largely retires A3's "`fd_speed` is probably not the
equilibrium" caveat, and is a second, data-side proof that the **dense** band is the operative one
(the thin band misses by ~4×).
**The discriminating test passes: `eighth-throttle-speed` 148.46 → 134.52 mph against the measured
137.9 ± 6.** Linear throttle plus the corrected polar reproduces the part-throttle equilibrium
unaided, so **`ThrottleExp` is retired, not promoted to a divergence** — the plan's row-4 claim is
now dead.
Envelope, Bloodhawk: `level-top-speed` 475.78 → **300.46** (target 300.4) ✓, `level-speed-near-cap`
the same ✓, `yaw-360` 47.22 → **28.68** s (target 28.6) ✓ — red since B11 and closed here as
collateral of the speed, `terminal-dive` 528.48 → **336.36** mph (target 355.2 ± 6, −5.3 %) and
**`MaxDiveSpeedFrac` stops binding** (1.114 × fd), `accel-150-290` 2.68 → **3.22** s (target 3.76 ±
0.40, −14.5 %, the only asserted row still red), `sustained-turn-speed` 252.68 → **261.01** mph
(target 222.94, +17.1 %). `sustained-turn-rate` unmoved at 32.35 — still C22's. `altitude-cap`
settles at 298.7 mph instead of 469.6 (original 173.7). The Balmoral flies again: level top speed
44 → 125.9 mph. `CAP-05` is untouched by construction (zero thrust) but its numbers move with the
drag correction: 1.31/3.03/6.16/7.68 m/s² against 0.36/1.11/2.82/3.74 — the *shape* is right now
(5.9× rise over the span against the measured 10.4×, where the `C_L` polar gave 1.27×) but
everything is ≈2–3.6× too strong.
**One residual, and it is one number, not three.** `decel-290-150` 5.73 → 3.48 s (original 7.04),
`accel-150-290` fast, `CAP-05` strong — all three say the force scale is uniformly too large by
about a factor of two. The equilibrium solve is a *ratio* and is blind to exactly that factor. The
leading suspicion, **untested and recorded rather than acted on**, is the force→acceleration
divisor: `veh_weight` is authored 1900 for the Bloodhawk while every aerodynamic intermediate is
imperial, and 2.2046 is the right size. It would divide thrust and drag together and leave the
equilibrium untouched — but it also moves the lift ceiling, so it is B14's call, not this item's.
Tests: engine 29/29 clean, goldens 4 moved (`empty-stage`, `c1-flight`, `c1-destroy-effects`,
`c1-crash` — exactly the four that fly a plane; manifest updated here, `c1-flight` eyeballed),
units 696/697 with `FlightEnvelopeTests` red on `accel-150-290` alone and **left red** — nothing in
Wave B is verified until B14. Boost: the remake has none, so there was nothing to fix; the decode's
"boost replaces the lever with 1.8 and multiplies drag by 0.8" is recorded for whoever adds it.

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
— the propeller constant-power form), scaling it as A3 settled: `EnginePower · RefArea`, **not**
`EnginePower / (VehWeight/1000)`. Leave
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

## B14 ☑ Joint refit and re-measurement of the aero group

**Outcome (landed 2026-08-09).** **The refit is empty, and that is the finding: the binary settled
the force-scale question against any refit.** B13's leading suspect — a kg/lb factor of 2.2046 in
the force→acceleration divisor — is disproven at source: the whole weight chain in `crimson.exe`
is conversion-free (`veh_weight` stored `0x47ae2d`, copied `0x475c6a`, used raw by gravity
`0x48ff8e`, lift `0x41ac13` and the `×9.82/W` conversion `0x491290`; the aero speed `[obj+0x934]`
is the true `|v|`, `0x491b1b`), so the executable computes exactly what `FlightModel.cs` computes,
constant for constant. **Zero force-path code changed, zero TUNEs added.** Two more suspects died
the same way: `drag_fade_speed` and the global `drag_factor` are parsed and **read by nowhere** in
the image (dead keys — no low-speed drag fade exists), and residual/idle thrust cannot produce the
deficit (the throttle has no idle floor and slews at 0.5/s, `0x48e652` — newly decoded, recorded
unimplemented). Byte-level bonus: the player force call **zeroes altitude** (`0x491250`–`0x491284`)
before computing forces, which proves the dense band from the bytes (and exposes that the AI call
site `0x48c883` does not zero it — thin-band AI aero, recorded for M4).
Full-table dispositions ([`analysis/flight-model-baseline/POST-B14.md`](../../analysis/flight-model-baseline/POST-B14.md)):
**green, asserted** — `level-top-speed` 300.46, `level-speed-near-cap` 300.46, `roll-360` 1.98,
`pitch-rate` 33.47, `yaw-360` 28.68, `altitude-cap` 6571.95, `sustained-turn-sink` −2.65 (bound
≤ 1.85); **green, informational by design** — `eighth-throttle-speed` 134.52 (137.9 ± 6);
**conflict recorded, downgraded to informational with the record named in the row** —
`accel-150-290` 3.22 (3.76 ± 0.40) and `decel-290-150` 3.48 (7.04) and `CAP-05`'s four points
(≈2–3.6× strong): the force path is byte-verified, the deficit against the zero-thrust footage is
a near-constant ΔC_D ≈ 0.112 that no decoded mechanism produces, and **no constant can close the
set** — a ×0.5 scale that fixes the decel blows the accel to ≈6.4 s against the same footage, so a
green here could only ever be compensating errors (the CAP-05 points are themselves outputs of
FINDINGS.md's (g, C)-degenerate fit; the clip's one model-free anchor leaves the polar 1.5–1.8×
strong); **owned by a named later item, informational with the owner in the row** —
`terminal-dive` 336.36 (355.2 ± 6, the decoded ×≈1.38 dive attitude-thrust waits on D32) and
`sustained-turn-speed` 261.01 (222.94 ± 5, waits on C22) and `sustained-turn-rate` 32.35 (18.95,
C22 — moved ≤ 0.09 °/s on every airframe across the whole of Wave B, the cleanest proof the gap is
not in the force path); **re-recorded as the new model's numbers** — `zoom-climb` 1341.20 ft (936)
and `zoom-climb-min-speed` 236.06 (127.9), plus the Balmoral/all-airframes snapshots (level top
speed within 1% of `fd_speed` for the nine conventional fighters, autogyro 0.94×, Balmoral 0.71×).
`FlightEnvelopeTests` asserts **7** scenarios (was 10) and is green; the three downgrades each
carry the attribution in their probe comment. The decode doc gained "The force scale — settled",
the corrected atmosphere/band story, the throttle slew and the dead keys;
`analysis/flight-model-baseline/POST-B14.md` is the table Wave C/D measure against (A2's originals
kept). Verified: engine suites 29/29, goldens 13/13 **unmoved** (no behaviour change — the
tripwire that the item's code footprint really was tests-and-docs only), units green with the new
7-row assert count, all instruments run twice byte-identical, full 8-chapter `--freecam`
regression clean.

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

## B15 ☑ Stall speed per airframe

**Outcome (landed 2026-08-09).** `isStalled()` now compares `Speed` against `FlightModel.StallSpeed`
— the closed-form solution of `clMax(V)·q(V)·RefArea = VehWeight` (5 fixed-point passes over the
Mach-dependent ceiling, computed once per instance) — in place of `StallSpeedFrac = 0.25 × fd_speed`,
which is retired along with its config key. `StallWarnFrac`/`IsStallWarned`/`StallFraction` are
byte-for-byte untouched — the lamp still fires at a fixed 0.30 fd.
**The G-convention question the plan flagged had a traced answer, not a judgement call.** A load
factor of 1 G (bare `VehWeight`, not `nom_gravity/StandardG ≈ 2.037`) is what the decode's own
worked example requires: its fallback-aircraft sanity check (75.5 mph dense / 309 mph thin) only
reproduces under the bare-Weight read — the nom_gravity-scaled read gives 109/447 mph, numbers the
decode never quotes. 1 G is coded, not a simplification of it — see `docs/org/flightModel.md`'s
"B15 landing note".
**Eleven-airframe table** (`analysis/flight-model-baseline/POST-B14.md`'s B15 section has the
full table with commentary): bhawk 75.50→**56.51** mph (−25.1%), devastator 63.19→**55.40**
(−12.3%), fury 70.46→**54.50** (−22.7%), warhawk 50.33→**55.50** (+10.3%), autogyro
57.04→**18.53** (**−67.5%**), avenger 65.99→**56.78** (−14.0%), balmoral 44.18→**45.54** (+3.1%),
brigand 60.40→**56.96** (−5.7%), firebrand 52.01→**52.46** (+0.9%), kestrel 54.25→**57.34**
(+5.7%), peacemaker 72.70→**54.37** (−25.2%).
**Surprise: the autogyro moves most, not the Balmoral.** The plan's own evidence named the Balmoral
as most likely to move; it in fact moves least (+3.1%, "already sits at every margin" turned out to
mean the fixed fraction happened to be close, not that it was far). The autogyro's huge `ref_area`
(800) against a tiny `veh_weight` (500) — the lightest wing loading of the eleven — drops its stall
speed by two-thirds, exactly the aerodynamically sensible result a fraction of `fd_speed` (blind to
`ref_area` entirely) could never produce.
**A real decode-vs-footage conflict, recorded rather than papered over.** Evaluated against the
Bloodhawk's OWN data (not the decode's fallback numbers), the 1 G formula gives 56.5 mph against the
"Stall 0% Thrust no input" clip's measured ~76 mph nose-drop. The retired `0.25 × fd_speed` matched
that clip only because 0.25 × the Bloodhawk's fd_speed (302 mph) coincidentally sits near the
FALLBACK aircraft's own stall speed (75.5–76.3 mph) — the wing-loading coincidence this item's own
evidence section warned about, which does not extend to the Bloodhawk's real numbers. The
nom_gravity-scaled convention would land closer to the clip (80.9 mph, −1.43×) but breaks the
decode's own two-figure anchor; recorded as an open conflict (candidate for Wave D), not resolved by
picking whichever convention flatters one clip.
**Tests.** `StallWarningTests` rebuilt to fly the Bloodhawk's real dynamics (1900/330/135) instead
of the placeholder `PlaneStats()` defaults — those defaults compute a stall speed that sits almost
exactly AT the fixed 0.30 fd warn threshold (see `FlightModel.StallSpeed`'s doc), which would invert
rather than exercise the split. The rebuilt test asserts the ordering and the two-threshold mechanism
dynamically off `model.StallSpeed`, not the clip's absolute 2.64 s / 14.9 mph lead (which the new
formula no longer reproduces for the Bloodhawk — see above). `FlightEnvelopeTests` (7 asserted rows)
and the full `RunTests.ps1` are unmoved: units 697/697, engine 29/29, goldens 13/13 hash-identical —
no scenario in the pinned envelope or the golden captures exercises sub-stall flight. Every
instrument (the rebuilt `StallWarningTests`, `FlightEnvelopeTests`, and a throwaway per-airframe dump
through the real `PlaneStats.Load` path) was run twice, byte-identical both times.

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

## C21 ☑ Yaw authority: the original's speed table

**Outcome (landed 2026-08-09).** Implemented as decoded, from A1's already-plumbed globals: yaw
authority is `YawLowSpeed` (0.0625) at or below `YawFadeIn` (10 mph), ramping linearly to 1.0 at
`YawMax` (50 mph), then falling linearly to `YawHighSpeed` (0.17) at `YawFadeOut` (400 mph) and
holding there. Applied to **yaw only** — `PitchTune`/`RollTune` and their torque terms are
byte-for-byte untouched, and the interim `eff = 1.4 − clamp(Speed/fd, 0.25, 1.15)` is retired along
with `MinControlEff`/`MaxControlEff` and their two config keys, since nothing else read them.
`YawTune` is refit **1.32 → 1.33** against the pinned full-rudder 360°: **28.65 s** (target 28.6,
+0.2%, was 28.68 pre-item), the only asserted row the curve touches. As the plan's own evidence
predicted, the two curves are nearly coincident right where `YawTune` was originally fit — at the
yaw-360 probe's 290–297 mph mean speed the old `eff` gave ≈0.44 and the new curve gives ≈0.43 — so
the refit is a rounding correction, not a re-tune. **The three-speed sample the plan asked for
shows why a single-speed check would have missed real movement**: at 60 mph (just above the
Bloodhawk's stall) old `eff` sat pinned to its 1.15 ceiling while the new curve reads 0.976 (−15%);
at 336 mph (the model's own achieved terminal-dive speed) old `eff` gave 0.287 against the new
curve's 0.322 (+12%). Every other asserted row (`level-top-speed`, `roll-360`, `pitch-rate`,
`altitude-cap`, `level-speed-near-cap`) is unmoved to the last printed digit — the curve change
cannot reach them by construction — and the two rows already parked on C22
(`sustained-turn-speed`, `sustained-turn-rate`) moved ≤ 0.00 across all eleven airframes, confirming
yaw authority plays no part in the sustained-pull turn (that scenario commands pitch, not yaw).
Extended to all eleven airframes (`ZzBaselineDump`): Bloodhawk 28.65 s (was 28.68), and ten more
with no prior baseline to compare against (devastator 22.53, fury 25.85, warhawk 30.88, autogyro
13.12, avenger 23.55, balmoral 30.32, brigand 21.48, firebrand 18.97, kestrel 19.55, peacemaker
27.07) — `analysis/flight-model-baseline/POST-B14.md` carries the new column.
Tests: engine 29/29, goldens 13/13 hash-identical (freecam never instantiates a `FlightModel`, and
none of the flown-plane goldens command rudder), units 697/697 with `FlightEnvelopeTests`'s 7-row
assert count unchanged. Every instrument re-run twice, byte-identical both times: `--dump-flight`
for the Bloodhawk and the Balmoral, and `ZzBaselineDump` for all eleven. Full 8-chapter `--freecam`
regression clean, zero engine errors, all eight screenshots saved.

**Goal.** Rudder authority follows the original's authored speed curve — a decline from 1.0 at
50 mph to 0.17 at 400 mph — rather than the interim linear `eff`.

**Evidence (confidence: traced).** With this install's **authored** values (`BL-095`, and the
corrections table in [`docs/org/flightModel.md`](../org/flightModel.md)): 0.0625 up to `yaw_fade_in`
(10 mph), ramping to 1.0 at `yaw_max` (**50**), then falling linearly to `yaw_high_speed` (**0.17**)
at `yaw_fade_out` (**400**), flat beyond. At the Bloodhawk's 302 mph cruise that is ≈ 0.40. The
current `eff = 1.4 − clamp(Speed/fd, 0.25, 1.15)` is **also** a declining function of speed, so its
shape is broadly right and only the curve is wrong — a smaller change than it first appeared, and
`BL-095` already names this fade set as the thing `eff` stands in for.

**Approach.** Implement the piecewise table from A1's authored values and apply it to yaw only —
the original applies a *different* curve to each axis, and pitch/roll are handled by C24 and left
alone respectively. `YawTune` must be refit to preserve the measured full-rudder 360° at 28.6 s.

**Model recommendation.** medium — a well-specified table, with one refit against a known
measurement.

**Verify.** Full-rudder 360° at 28.6 s (the pinned measurement), plus rudder response sampled at
low speed, at cruise and near maximum dive speed — the curve declines across that whole span, so a
single-speed check cannot distinguish it from the interim `eff`.

**⚠ Traps.** ⚠ **Do not build this from the executable's fallbacks** (which give a knee at 45 mph
and a flat 0.1 — that reading was wrong, and the corrections table records why). `eff` is currently
applied to yaw *only* — do not "fix" that by extending it to the other axes; the decode confirms
the axes genuinely differ. The steady rate is what `YawTune` pins; per `BL-147`'s warning, do not
chase transient shape by moving it.

## C22 ☑ Bank→yaw and bank→pitch coupling

**Outcome (landed 2026-08-09).** Implemented as decoded, and the inverted case — which
[`docs/org/flightModel.md`](../org/flightModel.md) carried only as "an extra contribution when
inverted" — is **re-derived from the bytes and now recorded exactly**. Both terms are added to the
same angular accumulator the stick commands feed (`FUN_00490f70`, `0x491621` / `0x49167b`), keyed
off the orientation matrix at `+0x180`:
`ω += 0.205·(starboard·up)·dt` on the **yaw** axis, signed by bank; and
`ω += (0.165·|starboard·up| + [bodyUp·up < 0] · −0.205·(bodyUp·up))·dt` on the **pitch** axis,
always nose-up. ⚠ **The inverted term reuses the 0.205 constant on the PITCH axis** — not a third
number and not more yaw — and it peaks wings-level inverted, where the 0.165 bank term is
identically zero. Each carries the axis' `RecInertia` (which the original applies downstream, in
the integrator, not in the accumulator — newly recorded) but **not** the remake's `*Tune`: those
calibrate stick authority against video and are ours. The ×`*Tune` alternative was measured rather
than argued (settled turn 255.6 → 257.4 mph, sink 1.66 → 2.03 ft/s, i.e. past the measured sink
bound), so the literal read is also the one the measurements prefer. **No `*Tune` was refit**, so
Decision 4's carve-out is unspent and still available to C23/C24.
**Bonus decode: the constants live in `.data`, not `.rdata`, because the retail build ships a
developer console that writes them** — `fall_off` → 0.205 (`0x43e299`) and `bank_off` → 0.165
(`0x43e2dc`), in the same command table as `hide_plane`/`kill`/`revive`. Runtime-tunable, authored
nowhere; the plan's "present in no data file" is exactly right and now has a reason.
⚠ **The item's own primary check fails, and it fails in the direction that kills the hypothesis
this item was written on.** `sustained-turn-rate` moves 32.35 → **34.71 °/s** against the
original's 18.95 — *away* from it — and rises on ten of the eleven airframes (peacemaker
29.98 → 32.25, fury 27.12 → 29.09, warhawk 14.97 → 15.68…). Both coupling terms *add* heading rate
in the direction of bank, by construction; there is no sign or scale of them that subtracts one. So
**the row is NOT promoted to asserting — it stays informational**, and the plan's standing
attribution of the turn-rate gap to this coupling is disproved at the mechanism rather than at the
magnitude. `BL-095`'s `turn_fade_in`/`turn_fade_out`/`highGs` are again the only authored fields
shaped like the original's 1.6×-slower banked pull, and they now carry the whole of it.
`sustained-turn-speed` moves 261.01 → **255.64 mph** against 222.94 (+17.1% → +14.7%) — a real but
small step toward the pin, on nine of eleven airframes; it also stays informational, since the row
it was parked behind is still open. `sustained-turn-sink` — the one **asserted** row the coupling
can reach — moves −2.65 → **1.66 ft/s** against its ≤ 1.85 upper bound and stays green: the turn
stops coming out *climbing*, which the row's own comment called a divergence, and lands just inside
the original's measured sink rather than crossing it. (This is the row the ×`*Tune` variant broke
at 2.03, and the only place the two variants are distinguishable by a verdict.)
**What did move, cleanly: the knife-edge, and it is a shape change rather than a number change.**
A neutral-stick 90° bank held 35 s used to pin the nose at the bounded −4.01° sag and settle
(−316 m, sink flat at 9.5 m/s from ~10 s); it now drifts **linearly at ≈1.08 °/s to −41.8° with no
equilibrium** (−1993 m). That is the shape `BL-247` measured on the original (0.69–0.89 °/s to
−27° over 36 s, still steepening) and that `KnifeNoseSag`'s bound was explicitly recorded as unable
to produce. The drift is now ≈1.2–1.6× too *fast* — a magnitude question where it was a mechanism
question. **Recorded for `D31`, not acted on**; `KnifeNoseSag`/`KnifeNoseRate` are untouched, and
D31 now has a live candidate for retiring the bound outright. Inverted level flight changed the
same way and for the same term: it used to hold altitude exactly forever, and now sinks (nose
−16.9° at 10 s), which is the arcade cheat working.
**Invariants held, and they are the strongest evidence the term is right.** Both coupling terms
vanish exactly at wings-level upright, so every wings-level scenario is unmoved **to the last
printed digit on all eleven airframes**: `level-top-speed`, `level-speed-near-cap`, `terminal-dive`,
`roll-360`, `pitch-rate`, `yaw-360`, `altitude-cap`, `accel-150-290`, `decel-290-150`,
`eighth-throttle-speed`. Roll is untouched on all three counts — the coupling feeds two axes, not
three (asserted), the full-aileron steady roll rate is 200.41 °/s before and after, and `roll-360`
is 1.98 s before and after. `zoom-climb` moves slightly (1341.20 → 1321.51 ft) because a held full
pull loops past vertical and inverted, which is legitimately in scope.
⚠ **One anomaly, recorded not chased: the autogyro's turn rate falls 33.44 → 13.04 °/s** while its
settled bank (75.9°) and speed (213.4 → 213.2 mph) are unchanged and every other airframe rises.
A turn rate at fixed bank and speed cannot fall by 61% for a physical reason, and the row is a
heading integral (`swept` 532° → 208°) — so this reads as the unfolding instrument meeting a
near-vertical flight path on the airframe most able to reach one, not as a model result. It is the
one number in the C22 table that should not be built on.
Verified per [`docs/verification.md`](../verification.md): `RunTests.ps1` green — units **702/702**
(697 + the 5 new coupling tests), engine 29/29, goldens 13/13. **One golden moved, `c1-flight`, and
which one is the evidence (GOLD-5/GOLD-1):** it is the only golden whose `--hold` carries a roll
input (`0.2,0.1,0,1`), so it is the only one that banks — `empty-stage`, `c1-destroy-effects` and
`c1-crash` all fly wings-level and are hash-identical. Manifest updated here, shot eyeballed.
Before/after was taken as a same-build A/B with both constants set to 0 (METHOD-9/10/15): the
zeroed control reproduces the post-C21 table exactly (261.01 / −2.65 / 32.35 / 1341.20), which is
what makes every movement above attributable to this item alone; `git diff` proves the temporary
edit restored (METHOD-17). Every cited instrument run twice, byte-identical:
`--dump-flight` for the Bloodhawk and the Balmoral and `ZzBaselineDump` for all eleven. Full
8-chapter `--freecam` regression clean, zero engine errors, all eight screenshots saved.

**Goal.** Banking turns the aircraft through the original's coupling terms, not solely through the
flight path chasing the nose.

**Evidence (confidence: traced).** Two constants hardcoded in the executable and present in no data
file: bank contributes to yaw at 0.205 and to pitch at 0.165 (with an extra contribution when
inverted), both scaled by the bank component and applied per tick as angular rate. The remake has
**no** bank-to-turn coupling at all. This is the leading candidate for the known turn-rate error —
the remake sweeps heading at ~32 °/s where the original sweeps 18.95.

**Approach.** Add both terms to the `BodyRates` accumulation. ⚠ **The refit this item was written to
expect no longer has a subject:** `InducedDragCoef` is gone (B12) and it has no successor — B13
showed the original's drag has **no induced term at all**, so nothing in the force path absorbs the
turn-rate error any more. Whatever moves the turn plateau after this lands must be attributed
somewhere else; do not go looking for a pull-cost constant to retune.

**Model recommendation.** high — small in code, but it changes the mechanism by which the aircraft
turns, and it invalidates a constant fitted on top of the old behaviour.

**Verify.** `sustained-turn-rate` against the original's measured 18.95 °/s — currently
informational precisely because of this gap, and the item that should promote it to asserting. Then
`sustained-turn-speed`'s 222.94 mph plateau, which the refit must preserve.

**⚠ Traps.** These constants are in **no data file** — do not go looking for them in `player.json`
and conclude they are absent from the game. Decision 4: no `*Tune` refit until this lands, or the
refit is wasted.

## C23 ☑ `return_rate` as a weathervane torque

**Outcome (landed 2026-08-09).** Implemented as decoded, and the decode had to be re-derived from
the bytes because the write-up was one line and **wrong in sign**. `FUN_00490f70`'s last
contribution (`0x4916fe`–`0x4917f0`, guarded by `cmp esi, [0x71c298]` — the player object — and by
`speed > 0`) builds the shortest-arc quaternion from the nose onto the unit velocity (`0x53fd40`:
half-vector construction, `w = n·ĥ`, `v = n×ĥ`), converts it through a quaternion-log helper
(`0x53fca0`: `atan2(|q.v|, q.w) · unit(q.v)`) and adds `return_rate · dt ·` that to the same
accumulator the stick and the bank coupling feed. So `ω += return_rate · (α/2) · unit(nose × v̂)`.
⚠ **Three corrections, all recorded in [`docs/org/flightModel.md`](../org/flightModel.md)'s new
"Weathervane centring — resolved":** the axis is `cross(nose, v̂)`, **not** `cross(−nose, v̂)` (the
code's `−m[2]` *is* the nose — the same negation the thrust term applies — and the opposite sign is
divergent); the angle is **halved**, so the spring rate is `return_rate/2`; and the roll component
is identically zero at every attitude, because the axis is ⊥ the nose. `return_rate` leaves the
`damp` vector entirely — damping is `ang_momentum_damp` alone, held stick or not.
**`BL-147` does not close, and the discriminating instrument had to be built** — the sweep existed
only as footage of the original, never as a probe of our build (`CSVM.Tests/ZzCadenceSweep.cs` now
is one: the same alternating square wave, the same simultaneous cubic+sin+cos estimator, run at
both the sim and wall readings of the cadences per DET-11). Amplitude-for-amplitude, so no transfer
function is assumed on either side: the 1300 → 570 ms roll-off is **19.6× before → 23.3× after**,
against the original's **42×**. Right direction, ≈ a sixth of the gap. A second-order response is
part of the answer and demonstrably not the whole of it.
⚠ **And the sweep corrects `BL-147`'s own arithmetic, which is the item's second finding.** Its
"42× is 3.5× steeper than any single first-order lag permits" rests on the chain being *double
integration + one lag*. Ours never was: the flight path chases the nose through a **second**
first-order lag (`lift_accel_rate`, τ = 1.33 s), so the pre-C23 build already rolled off 19.6× —
1.65× past that ceiling — with `return_rate` still in the damping coefficient. The 3.5× figure
is not a measurement of our build's deficit, and `backlog.md` now carries the same-input
comparison in its place.
⚠ **The plan's "the steady rates must be unchanged, by construction" trap rests on a false
premise, and this is where a `*Tune` moved.** The weathervane vanishes at zero misalignment — every
wings-level, α = 0 scenario is unmoved to the last printed digit (`level-top-speed`,
`level-speed-near-cap`, `terminal-dive`, `accel-150-290`, `decel-290-150`, `eighth-throttle-speed`)
— but a *sustained full-stick* manoeuvre holds a real misalignment by construction (α ≈ 18° in a
full pull, β ≈ 8° on full rudder), so the restoring torque opposes the stick there. Un-refit,
`pitch-rate` fell 33.47 → 28.35 °/s and `yaw-360` rose 28.65 → 34.43 s, both outside their bands.
**`PitchTune` 0.75 → 0.89 and `YawTune` 1.33 → 1.57 re-pin them: 33.54 °/s (33.0 ± 3) and 28.55 s
(28.6 ± 3).** That is Decision 4's carve-out, spent, and it is the *opposite* of what `BL-147`
forbids — `*Tune` sets the steady rate, and this re-pins a steady rate a new mechanism moved rather
than chasing a transient through it. `RollTune` is untouched and `roll-360` is 1.98 s before and
after **on all eleven airframes**, which is the roll-axis invariant proved on a measurement as well
as in a test. Bonus: the un-tuned formula gives the Bloodhawk 44.6 °/s against a measured 33, so
the weathervane accounts for a little over half of `PitchTune`'s existence — a smaller fudge, not a
retired one.
⚠ **One asserted row is downgraded, not tuned away.** `sustained-turn-sink` 1.66 → **2.33 ft/s**
against its ≤ 1.85 bound. It is the third leg of a manoeuvre whose other two legs are already
informational and owned by `BL-095`'s `turn_fade_*`/`highGs`: we sweep heading 76 % faster than the
original at a bank it never flew, and a sink read off that flight path has no reason to land on the
original's — C22's green there sat inside the same unattributed gap. Downgraded to informational
with the attribution in its probe comment (B14's pattern), re-asserting with the rate row;
`FlightEnvelopeTests` now asserts **6**. `sustained-turn-rate` 34.71 → **33.38 °/s** (still 18.95
away) and `sustained-turn-speed` 255.64 → **258.85 mph**.
**D31's number, measured and recorded, not acted on:** the weathervane *steepens* the knife-edge
rather than opposing it — in a sagging knife-edge the path is below the nose, so the torque pulls
the nose down onto it. The 35 s neutral-stick 90° hold goes −41.80° / −1991 m → **−44.61° /
−2124 m**, i.e. ≈**1.17 °/s** against `BL-247`'s measured 0.69–0.89 (≈1.3–1.7× too fast, was
1.2–1.6×); the Balmoral's 35 s hold −40.14° → −47.16°, and inverted level −16.90° → −20.90° at
10 s. `KnifeNoseSag`/`KnifeNoseRate`/`KnifeAlignFloor` are byte-for-byte untouched. The ten
unmeasured airframes' `sustained-turn-sink` mostly *improves* (warhawk 13.13 → 6.44, firebrand
9.79 → 0.53, kestrel 7.64 → 0.61) while the Bloodhawk's worsens.
Verified per [`docs/verification.md`](../verification.md): `RunTests.ps1` green — units **710/710**
(702 + `WeathervaneTests`' 7 + the sweep), engine 29/29, goldens 13/13 after one re-pin.
**One golden moved, `c1-flight`, and which one is the evidence (GOLD-5/GOLD-1):** it is the only
golden whose `--hold` carries a pitch input (`0.2,0.1,0,1`), so it is the only one that ever holds
a nose/path misalignment — `empty-stage` (`0,0,0,0.6`), `c1-destroy-effects` and `c1-crash` fly
stick-centred and are hash-identical. Manifest re-pinned here, shot eyeballed. Before/after was
taken as a same-build A/B (the weathervane commented out, `return_rate` back in `damp`, the two
tunes restored) which reproduces the committed `C22` build's output **byte-identically** on both
instruments (METHOD-6/9/10/15); the temporary edit is reverted and `git diff` proves it (METHOD-17).
The able-to-fail control for the tests is a deliberate perturbation (METHOD-9): the full-angle read
fails 1 of the 7, a sign flip fails 2. Every cited instrument run twice, byte-identical:
`--dump-flight` for the Bloodhawk and the Balmoral, `ZzBaselineDump` for all eleven, and the
cadence sweep. Full 8-chapter `--freecam` regression clean, zero engine errors, all eight
screenshots saved.

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

## C24 ☑ Exponential angular damping (and the inert pitch fade, recorded not built)

**Outcome (landed 2026-08-09, Wave C complete).** `FlightModel.Step`'s integrator line is now
`BodyRates += cmd · dt; BodyRates *= Mathf.Exp(-dt * s.AngMomentumDamp);` — the decode's own order
(`FUN_00491820` steps 2–3): accumulate this tick's torque (stick + C22's bank coupling + C23's
weathervane, all three already summed into `cmd`) onto `BodyRates` FIRST, then decay the WHOLE
result, this tick's own contribution included, by `exp(−dt·ang_momentum_damp)` — not the
explicit-Euler `(cmd − BodyRates·damp)·dt` it replaces, which only ever damped the rate carried
over from the previous frame. `docs/org/flightModel.md`'s "The integrator" already had the order
right; no re-derivation from the binary was needed, only confirmation.
**The two forms agree to first order per step but not at steady state, and the gap is real, not
noise:** explicit Euler's fixed point is `cmd/damp` exactly (dt-independent); the exponential
form's is `cmd/damp · x/(eˣ−1)` for `x = dt·damp`, smaller by `≈x/2` at small `x`. At this engine's
own `dt = 1/60 s` and the Bloodhawk's `ang_momentum_damp = 5` (`x ≈ 0.083`), the predicted
shortfall is `≈4.1 %`, and the measured steady rates land almost exactly there: `roll-360`
1.98 → **2.07 s** (+4.5%, target 2.05 ± tolerance), `pitch-rate` 33.54 → **32.41 °/s** (−3.4%,
target 33.0), `yaw-360` 28.55 → **29.73 s** (+4.1%, target 28.6 ± 3) — all three still inside
`FlightEnvelopeTests`' asserted tolerance, so **Decision 4's carve-out was not spent again; no
`*Tune` was refit.** This is the decoded discretization's own bias at this engine's tick rate, not
a sign the form or the ordering is wrong — a mechanical consequence of the recovered mechanism,
not a free parameter. `level-top-speed`/`level-speed-near-cap`/`altitude-cap` (the translation
path) are unmoved, confirming the change is contained to rotation.
**The large-timestep divergence, measured:** at `dt·damp = 5` (a released axis, `dt = 1 s`) the
exponential factor stays at `exp(−5) ≈ 0.0067` — positive, bounded, strictly decaying — where the
retired linear factor `(1 − dt·damp) = −4` would have flipped the rate's sign and quadrupled its
magnitude every tick. `CSVM.Tests/AngularDampingTests.cs` (3 new tests) pins both this and the
ordering: one test asserts this tick's own torque is itself subject to the tick's decay (not
exempted, the mistake a decay-then-add reading would make), which a build reading the two lines in
the wrong order fails.
**The pitch fade: confirmed unreachable for all eleven player airframes, and nothing was
implemented.** `MaxDiveSpeedFrac` (1.75 × `fd_speed`) is the model's own hard, unconditional
ceiling on `Speed` — the most generous "could this ever reach it" test available, more generous
than any airframe's aerodynamically-settled terminal dive. The highest of the eleven (the
Bloodhawk, 528.5 mph) sits at little over half of `high_speed_pitch_fade[0]`'s authored 1000 mph;
the other ten are lower still (309–509 mph). Full table in
[`docs/org/flightModel.md`](../org/flightModel.md#the-integrator) and
[`POST-B14.md`](../../analysis/flight-model-baseline/POST-B14.md)'s C24 section. No code was written
for the fade — untestable code on a threshold no capture could ever exercise is the invented
content this project's ground rules forbid — and `backlog.md`'s `BL-095` now records the finding as
closed rather than open.
**The cadence sweep, reported honestly and NOT claimed for `BL-147`:** the roll-off (1300→570 ms)
moved from 23.3× to **22.6×** (sim reading) and 22.4× to **22.4×** (wall reading, unmoved to the
printed digit) — against the original's 42×. The sim reading moved slightly AWAY from the target,
not toward it; the exponential form does not produce a steeper rolloff (as the item's own evidence
predicted), and this item claims none of `BL-147`'s gap.
**Verified per `docs/verification.md`:** `RunTests.ps1` green — units **713/713** (710 + 3 new
`AngularDampingTests`), engine 29/29, goldens 13/13 after one re-pin. **One golden moved,
`c1-flight`, same pattern as C22/C23 (GOLD-5):** it is the only golden whose `--hold` carries a
pitch+roll input (`0.2,0.1,0,1`), so it is the only one that ever holds a body rate long enough to
feel the ~4% steady-rate shift; `empty-stage`, `c1-destroy-effects` and `c1-crash` are
hash-identical. Manifest updated here, shot eyeballed (banked Bloodhawk over C1, HUD/gauges/exhaust
trail intact). Every cited instrument run twice, byte-identical: `--dump-flight` (Bloodhawk and
Balmoral), `ZzBaselineDump` (all eleven airframes) and `ZzCadenceSweep`. Full 8-chapter `--freecam`
regression clean, zero engine errors, all eight screenshots saved. **Wave C is complete.**

**Goal.** Angular damping decays exponentially rather than linearly; the original's high-speed pitch
fade is documented as unreachable in this install and deliberately **not** implemented.

**Evidence (confidence: traced).** Damping in the original applies the torque and then decays the
rate by `exp(−dt · damp)`; the remake's `(cmd − rates·damp)·dt` is the explicit-Euler approximation
of the same thing, agreeing to first order. Separately, pitch authority fades between
`high_speed_pitch_fade[0]` and `[1]` — but this install **authors [1000, 1001] mph** (`BL-095`)
against a maximum attainable dive of ~528 mph, so the fade **cannot engage**. The executable's
[500, 600] fallbacks, on which this item was originally written, are not what the game ships.

**Approach.** Switch damping to the exponential form. For the pitch fade: implement nothing, and
record the finding in `docs/org/flightModel.md` and the module's `docs/architecture.md` entry — a
future session reading the decode will otherwise see a missing feature and "fix" it.

**Model recommendation.** medium, low effort — one small, well-specified change plus a note.

**Verify.** Confirm the steady rates are unchanged and check behaviour at a deliberately large
timestep, where the two forms diverge. Show the pitch fade is unreachable for **all eleven**
airframes (each has its own `fd_speed`, so the maximum dive speed differs), not just the Bloodhawk.

**⚠ Traps.** The damping change is **not** the answer to `BL-147` — the two forms agree to first
order and the exponential does not produce a steeper rolloff. C23 owns that question; do not let
this item claim it. ⚠ **Do not implement the pitch fade "for completeness"** — untestable code on
an unreachable threshold is exactly the kind of invented content this project's ground rules warn
against, and there is no capture that could ever verify it.

# Wave D — The open conflicts

## D31 ☑ Bank-independent lift vs the measured knife-edge sag

**Outcome (landed 2026-08-09).** **Both sources were right about different terms, and the term that
was wrong is one neither of them owns.** The decode's bank-independent lift is already implemented
(B11) and stands: at 90° of bank the body X/Y projection still carries full weight. The footage's
sag is real too — and it is in the **nose**, exactly as the plan's leading candidate said. At 90° of
bank the body yaw axis is horizontal, so C22's `0.205` bank→yaw *is* a nose-sag rate and C23's
weathervane deepens it. Neither was written for the knife-edge; between them they produce the
original's own shape, a drift that never finds an equilibrium, on **all eleven** airframes
(0.48–1.42 °/s, last-third share 0.23–0.30).
⚠ **So the thing that had to go was the remake's own bounded sag, and removing it IMPROVES the very
onset it was fitted to** — the opposite of what the standing trap warned. `KnifeNoseSag` (0.07 rad)
and `KnifeNoseRate` (0.2 rad/s) are retired with their two config keys and the whole `!stalled` sag
block. Bloodhawk, 143 mph take, throttle trimmed level at entry: nose at +3 s **−4.94°** against a
measured **−4.9°** (it read −7.28° with the bounded step stacked on top), sink at +3 s 12.7 → **5.7**
ft/s against a measured 0.5, 36 s altitude 1187 → **1087** m against a measured 540 (in 38.9 s). The
trap's premise — "raising the bound destroys the first 3 s" — was right about the bound and wrong
about the term: the first 3 s is where deleting it helps most. It was also never a knife-edge term
at all. Keyed on `1 − |bodyUp·up|`, it fought every wings-level pull at up to 11.5 °/s — `BL-115`'s
suspected "knife-at-zero-bank leak", now confirmed and pinned closed by a test — which is why
`zoom-climb` moves toward its measured 936 ft on **all eleven** airframes (Bloodhawk 1396 → 1338,
Balmoral 3935 → 1791) and the Balmoral reaches the altitude cap at all (4471 → **6572** ft).
⚠ **`wingVert` stands, and Decision 2 is satisfied by evidence rather than by either source.** Lift
has not read it since B11; its one surviving reader is the nose-chase floor `KnifeAlignFloor`, an
explicit kinematic slerp that is the *remake's* arcade handling and has **no counterpart in the
original's force path** — so "lift is bank-independent" is simply silent about it. The footage is
not: the original holds its nose 4.8° → 8.3° **below** its flight path across the 36 s, a gap that
GROWS. Ours runs 2.9° → 1.2°; with `wingVert` retired (chase floor 1.0, the decode's reading applied
where it does not belong) it collapses to 1.9° → 0.5°, the drift rises 1.09 → 1.19 °/s and the 36 s
loss rises 1087 → **1334** m. Every knife-edge observable moves the wrong way without it. Lowering
the floor instead of removing it moves every row toward the footage (0.10: gap 3.5° → 2.1°, drift
0.96, 874 m) and still cannot reach it, while walking the knife-edge α to **5.36°**, past
`liftAOAs[0] = 5°`, where the airflow blend would start engaging in a knife-edge. **Left at 0.35 —
no `*Tune` and no TUNE constant moved**, so Decision 4's default holds.
**The lost α probe is rebuilt as code, not prose.** `Probes.KnifeEdge` carries the recipe A2
recorded as unrecoverable: 90° bank at entry then FREE, nose on the horizon, path along the nose,
stick neutral, throttle **bisected to a level-flight trim at the entry speed**, held 36 sim s,
sampled at the original's own +1/+3/+12/+24/+36 s, reporting nose, path, the nose−path gap, sink,
Δalt, α, bank, heading rate and speed. It rides `--dump-flight` and `ZzBaselineDump`, so all eleven
airframes carry it and it cannot be lost again. ⚠ **The trim is the finding, not a detail** — held
at full throttle the 143 mph take is past 290 mph in three seconds and reports the 300 mph take's
numbers under the wrong label; that is now `verification.md`'s **METHOD-21**. It also retires the
stale "Balmoral knife-edges at α = 5.1°, 0.1° inside the ramp" figure — and it retires it in the
opposite direction to the one the old text implied. `liftAOAs` is a `player.json` **global**, so the
5° edge is the same for all eleven; the peaks run **0.71–4.29°**, the Balmoral is the *roomiest* at
1.77° (≈3.2° clear, not 0.1°), and the **tightest is the Bloodhawk** at 4.29°, ≈0.71° clear. So the
margin is real on every airframe but thinner than the fleet-wide figure suggests, and it is the
Bloodhawk — not the bomber — that would cross first. `KnifeEdgeTests` asserts it per airframe
against each one's own loaded edge, so a future per-plane `liftAOAs` override cannot slip past it.
⚠ **One row worsens and is left worsened, attributed.** `sustained-turn-sink` 1.07 → **8.86** ft/s
(bound ≤ 1.85): the retired term had been holding the nose down through the whole max-pull turn, so
the turn now settles at 88.8° of bank rather than 74.9° and actually pulls. It is the third leg of a
manoeuvre whose other two legs are informational and owned by `BL-095`'s `turn_fade_*`/`highGs` —
we sweep heading 73% faster than the original at a bank it never flew — and C23 already demoted it
for exactly that reason; it re-asserts with the rate row, not before. On the other ten airframes the
same change moves it the OTHER way (warhawk 8.96 → −15.54, balmoral 17.57 → −4.17, firebrand
1.31 → −12.48; negative = climbing), which is itself evidence it is riding the turn gap.
`sustained-turn-rate` 32.24 → 32.80 and `sustained-turn-speed` 261.82 → 258.41, both informational.
**Still open, and it is now ONE number rather than a mechanism question.** The whole banked rotation
runs ≈1.6× fast: knife-edge nose drift 1.09 °/s against 0.69–0.89, knife-edge heading 1.68 °/s
against 0.68–1.13, and `sustained-turn-rate` 32.80 against 18.95. Two manoeuvres, two different
body axes, one ratio — and `BL-095`'s unconsumed `turn_fade_in`/`turn_fade_out`/`highGs` remain the
only authored fields shaped like it. Nothing was tuned to close it here.
Verified per [`docs/verification.md`](../verification.md): `RunTests.ps1` green — units **717/717**
(713 + `KnifeEdgeTests`' 4), engine 29/29, goldens 13/13 after one re-pin, `FlightEnvelopeTests`
still asserting **6** (no scenario demoted). **One golden moved, `c1-flight`, same pattern as
C22/C23/C24 (GOLD-5/GOLD-1):** the only golden whose `--hold` carries pitch and roll
(`0.2,0.1,0,1`), so the only one whose attitude the retired term could touch; `empty-stage`,
`c1-destroy-effects` and `c1-crash` are hash-identical. Manifest re-pinned here, shot eyeballed.
Before/after is a same-build A/B against `HEAD`'s `FlightModel.cs` (METHOD-6/15) with `git diff`
proving every temporary edit restored (METHOD-17), and every α = 0 wings-level row is unmoved to
the last printed digit (DIAG-10). Two able-to-fail controls, demonstrated not argued (METHOD-9):
the pre-change build fails the wings-level-pull test, and the bank-independent chase fails the
nose-below-path test. Every cited instrument run twice, byte-identical: `--dump-flight` for the
Bloodhawk and the Balmoral, `ZzBaselineDump` for all eleven. Full 8-chapter `--freecam` regression
clean, zero engine errors, all eight screenshots saved. Full tables:
[`analysis/flight-model-baseline/POST-B14.md`](../../analysis/flight-model-baseline/POST-B14.md)'s D31
section.

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

## D32 ☑ Attitude-dependent thrust vs `ClimbGravityScale`

**Outcome (landed 2026-08-09).** **The decode wins outright, and the fitted constant was not merely
redundant — it was making the very manoeuvre it was invented for WORSE.** The discriminating
instrument existed all along and had never been decoded: `Climp 90° 100% Thrust.mp4`, a
full-throttle climb the original holds for forty seconds. Registered as clip key `climb90` and
decoded, it gives entry **298.9 mph**, a flight path settling at **56.3 ± 3.2°**, speed falling to
**152.4 mph at +6.5 s** and then **recovering** — 163.05 mph over 12–18 s, creeping to a flat
**167.0 ± 0.5** by +36 s — climbing ≈12,000 fpm, and leaving that state only at ≈6,600 ft, which is
`CAP-03`'s ceiling and not the climb. `Probes.SustainedClimb` is that manoeuvre as code, riding
`--dump-flight` and `ZzBaselineDump` so it cannot be lost.
**One mechanism at a time, same build (Decision 5), plateau against the footage's own 163.05:**
`ClimbGravityScale` alone (the pre-change model) **276.66**; neither **257.74**; both **232.20**;
**attitude terms alone 204.04**. Removing the constant on its own moves 276.66 → 257.74 with no
attitude term in sight — so on the post-B14 drag and thrust shapes it is not climb retention at all,
and the plan's leading hypothesis (it was absorbing B12/B13's predecessors' error) is confirmed by
its own ablation. `ClimbGravityScale` is **retired** with its config key, and gravity is now full
strength in every attitude — which the binary corroborates: the whole gravity block
(`0x48ff85`–`0x48ff9d`) is `nom_gravity/9.82 × Weight` with no attitude read anywhere near it. ⚠ The
game's own GDD §4.1.1 describes gravity as pitch-scaled and *reduced* on upward pitch; the shipped
executable does the opposite, in a different term. Design intent and behaviour disagree in sign
here, and that is now recorded rather than reconciled.
**The sign is proved from the bytes, not from a coefficient name.** The terms scale available thrust
by `(1 + 0.24a)·(a ≤ 0 ? 1 + 0.13a : 1)` where `a = [obj+0x19c]` is the **Y of orientation row 2**,
and row 2 is **−nose** — established at the point of use, where the thrust magnitude is `fchs`'d
before being multiplied by that same row (`0x48fe91`–`0x48feb8`), so the force lands along `+nose`.
So `a < 0` climbing, both branches bite there, and a vertical climb keeps 0.6612 against a vertical
dive's 1.24. `AttitudeThrustTests` reads the term back out of the integrator (full throttle minus
zero throttle at an identical state, which isolates thrust exactly) and **fails under the flip** —
demonstrated, not argued.
**`terminal-dive` is PROMOTED back to asserting**: 336.36 → **356.00** mph against 355.2 ± 6
(−5.3 % → +0.2 %), and it is entirely the attitude terms — the dive is the side of the scale that
ADDS thrust and the one attitude the retired constant never touched. `FlightEnvelopeTests` asserts
**7** again (7 → 6 at B14, 6 → 7 here); nothing was demoted to make room.
`zoom-climb` **1338.33 → 1060.33 ft** against 936 (+43.0 % → +13.3 %) and moves toward it on **ten of
eleven** airframes; the autogyro **crosses** (968.93 → 845.26, +3.5 % → −9.7 %), the same outlier
B15 flagged, reported rather than smoothed. `zoom-climb-min-speed` 237.03 → 165.96 (127.9).
Every α = 0 wings-level row — `level-top-speed`, `level-speed-near-cap`, `accel-150-290`,
`decel-290-150`, `eighth-throttle-speed`, `yaw-360`, `roll-360` — is unmoved to the last printed
digit (DIAG-10): the scale is exactly 1 with the nose on the horizon. `pitch-rate` 31.87 → 31.59
(green), `sustained-turn-speed` 258.41 → 255.61 and `sustained-turn-rate` 32.80 → 32.83 (both
informational, still `BL-095`'s).
⚠ **One row worsens and is left worsened, attributed.** `sustained-turn-sink` 8.86 → **10.31** ft/s
(bound ≤ 1.85): the max-pull turn settles nose-high at ≈89° of bank, so the attitude scale takes
thrust off it. It is the third leg of the same manoeuvre whose other two legs are informational and
owned by `BL-095` — we sweep heading 73 % faster than the original at a bank it never flew — and it
re-asserts with the rate row, as C23 and D31 both recorded.
⚠ **The decode turns out INCOMPLETE for the climb, and that is recorded rather than patched.** The
plateau is still **+25 %** (204.04 against 163.05) and the shape differs: the original undershoots
its own plateau by 9 % and climbs back out of it, where the model decays monotonically. The
along-path balance at the footage's plateau needs a thrust factor of **0.5632** and the decoded
formula's floor is 0.6612, so the 0.24/0.13 terms cannot be the missing 21 % at any attitude. The
leading candidate is the probe's **α**: it holds α = 0 (attitude on the path) while the clip is a
**90° pull**, and at a 90° nose with the measured 56° path the same decoded force path balances to
**−3.3 %**. The clip's ADI saturates above ≈+30°, so its nose angle is **not readable** and this
capture cannot settle it — which is exactly what `CAP-20` was filed for. Nothing was tuned.
**No `*Tune` and no TUNE constant moved** (Decision 4's default holds); `ClimbGravityScale` leaves
`BL-115`'s TUNE list, which is now `StallNoseRate` and `KnifeAlignFloor`.
Verified per [`docs/verification.md`](../verification.md): `RunTests.ps1` green — units **721/721**
(717 + `AttitudeThrustTests`' 4), engine 29/29, goldens 13/13 after two re-pins,
`FlightEnvelopeTests` asserting **7**. **Two goldens moved, `c1-flight` and `c1-destroy-effects`
(GOLD-5/GOLD-1), and the pattern IS the evidence:** they are the only two whose aircraft is not
nose-level — one holds pitch, the other spawns nose-down 5.7° — while `empty-stage` and `c1-crash`
fly level-attitude holds and are hash-identical, which is the DIAG-10 argument again in pixels.
Manifest re-pinned here, both shots eyeballed, and the `c1-destroy-effects` re-render reproduces its
new hash exactly. Before/after is a same-build A/B (METHOD-6/15) whose pre-change configuration
reproduces the committed POST-D31 numbers to the last digit (METHOD-8), with `git diff` proving
every temporary edit restored (METHOD-17). Every cited instrument run twice, byte-identical:
`--dump-flight` (Bloodhawk, Balmoral) and `ZzBaselineDump` (all eleven). Full 8-chapter `--freecam`
regression clean. Full tables:
[`analysis/flight-model-baseline/POST-B14.md`](../../analysis/flight-model-baseline/POST-B14.md)'s D32
section.

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

## D33 ☑ The G and AOA limiters — live or inert in this install?

**Outcome (landed 2026-08-09, Wave D complete, plan complete).** **Disproven on all eleven
airframes, and no limiter was written.** The G limiter's ramp begins at the authored
`highGs[0] = 9 G`; the hardest manoeuvres this model can fly demand **2.13–5.01 G**, so the
reduction it would apply is identically zero everywhere, on every airframe, with margins of
**3.99–6.87 G**. The AOA limiter reaches zero at the authored `maxAOA = 46°`; the same manoeuvres
peak at **8.9–25.6° α**, margins of **20.4–37.1°**. Both figures are measured through the real
model rather than argued: `FlightModel.LoadFactorDemand` is the lift demand's body X/Y projection
read **before** both lift clamps — the most generous reading of "the G this aircraft is pulling",
and higher than anything the wings actually deliver — and α is the same emergent alignment lag the
instruments already report. The suite's own rows agree from the other side: the sustained
pitch-rate row 20.2/20.5/20.6°, `zoom-climb` 23.3° at its minimum speed, the sustained turn 23.0°,
D31's knife-edge peaks 0.71–4.29°. The negative side is unreachable twice over: `lowGs [−6, −9]`
sits past the −5 G lift clamp, **and** the demand is the LENGTH of a projected vector, so it is
never negative in this model at all.
⚠ **The G margin is thinner than the authored numbers make it look, and that is the item's own
finding.** The Bloodhawk's peak demand is **5.01 G** — within **0.2 %** of the executable's
compiled fallback `highGs[0] = 5`. Under the fallbacks the limiter would engage, but only barely:
a hundredth of a G into a 4 G-wide ramp, i.e. a fraction of a percent of authority. So the plan's
"the fallbacks, where the limiter clearly would bite" is true only in the sense that it would
*start* to; what puts the mechanism firmly out of reach is the **authored 9**, and the difference
between "0.2 % past a threshold" and "44 % short of one" is exactly why this had to be measured on
all eleven airframes rather than asserted from two constants.
⚠ **One consequence lands outside this item: `highGs` is out of the banked-turn gap.** C22, C23 and
D31 each parked the ≈1.6–1.7× fast banked rotation on "`BL-095`'s `turn_fade_in`/`turn_fade_out`/
`highGs` are the only authored fields shaped like it". `highGs` cannot be it — it is inert on every
airframe by the arithmetic above, and a limiter that never fires cannot slow a turn.
⚠ **Post-plan correction (2026-08-09, same day): `turn_fade_*` is out too, so the gap has NO
authored candidate.** Checked in response to a question about whether decoding could still answer
it, and the answer was already in this project's own decode: `FUN_00490e10`'s base ramp keys off
**airspeed alone** (0 at `turn_fade_in` 10, 1 at `turn_fade_out` 50, flat above) and scales roll and
pitch authority with no bank or load-factor term. It is identically 1 across the 222–260 mph the
banked turn settles at, so it cannot be the 1.6×. C22, C23, D31 and D33 all repeated "the only
authored fields shaped like it" without re-reading the function they were pointing at — an inherited
claim four items deep. Corrected in [`docs/org/flightModel.md`](../org/flightModel.md),
`backlog.md`'s `BL-095` and `Probes.cs`; the ramp itself is a real unimplemented low-speed behaviour
and is minted as `BL-330`. The gap is now a **capture** question (`CAP-33`), since the measurement
is not internally consistent with a coordinated level turn.
**Nothing was implemented, following C24's precedent.** The asymmetry the trap warns about — the
original gates only input that *opposes* the current rotation, so the limiter damps recovery from a
departure rather than entry into one — is recorded in
[`docs/org/flightModel.md`](../org/flightModel.md) with both tables, so a future session reading the
decode sees a mechanism deliberately left unbuilt rather than a missing feature. The atmosphere
band-select flag was **not** touched (its runtime value is still unknown and the dense band is
still established by arithmetic), no `*Tune` and no TUNE constant moved, and Decision 4's default
holds.
**The one line of code is instrumentation, not mechanism.** `FlightModel.LoadFactorDemand` is a
public field assigned beside the existing clamp and read by no force term — the same shape as
`Alpha`. Everything else is tests and docs, and the tripwire says so: `--dump-flight` (Bloodhawk
and Balmoral) and `ZzBaselineDump` (all eleven airframes) reproduce D32's committed raw files
**byte-identically**, and goldens are **13/13 hash-identical** with no re-pin — the first item in
Waves B–D to move no golden at all.
**The disproof is pinned as a test, not as prose:** `CSVM.Tests/ControlLimiterTests.cs` (5 tests)
flies five max-performance manoeuvres per airframe — the pull at cruise and entered at 1.5 ×
`fd_speed`, the same pull banked, a full forward push, full rudder — and asserts the measured peaks
against **each airframe's own loaded** `HighGStart` / `LowGStart` / `MaxAoaCos`, so a data edit or a
per-plane override that brings either threshold into reach fails the suite instead of passing
silently. The able-to-fail control is demonstrated rather than argued (METHOD-9): halving both
authored thresholds — the stand-in for exactly such an edit — fails both checks, so each is
measuring a margin and not asserting an unreachable constant.
Verified per [`docs/verification.md`](../verification.md): `RunTests.ps1` green — units **726/726**
(721 + `ControlLimiterTests`' 5), engine 29/29, goldens **13/13 unmoved**, `FlightEnvelopeTests`
still asserting **7** (an analysis item moves none). Every cited instrument run twice,
byte-identical: `--dump-flight` for the Bloodhawk and the Balmoral, `ZzBaselineDump` for all
eleven, and the per-airframe limiter table itself. Full 8-chapter `--freecam` regression clean,
zero engine errors, all eight screenshots saved. Full tables:
[`analysis/flight-model-baseline/POST-B14.md`](../../analysis/flight-model-baseline/POST-B14.md)'s D33
section.

**Goal.** Determine whether the original's G and AOA control limiters ever engage with this
install's authored values, and implement them only if they do.

**Evidence (confidence: direction-sound — the disproof is essentially already made, and this item
exists to confirm and record it).** The original reduces pitch and yaw authority above `highGs[0]`,
reaching zero at `highGs[1]`, and separately reduces it toward zero at `maxAOA` — both gating **only
input that opposes the current rotation**. But the lift clamp is a hard ±5/9 G, and this install
authors `highGs [9, 15]` and `lowGs [−6, −9]` (`BL-095`): **both limiters begin at or past the point
where lift is already capped, so neither can engage.** The executable's `[5, 9]` / `[−5, −9]`
fallbacks, where the limiter clearly would bite, are not what the game ships. `maxAOA` is authored
at 46°, well beyond any α the suite's scenarios reach.

**Approach.** Confirm the arithmetic against A1's loaded values for all eleven airframes, then
**record the disproof and implement nothing** — that is the expected and successful outcome. Write
it into `docs/org/flightModel.md` so the mechanism is documented as present-but-unreachable rather
than missing. Only if some airframe's numbers reach the threshold does any code follow.

**Model recommendation.** high — mostly analysis, and the valuable answer is a negative one that has
to be argued from the interaction of two clamps rather than observed.

**Verify.** If implemented: a maximum-G pull and a high-AOA departure, checking that authority
falls only against the rotation and not with it. If disproven: show the threshold is unreachable
for all eleven airframes, not just one.

**⚠ Traps.** The asymmetry is the whole subtlety — a limiter that reduces *all* input rather than
opposing input will damp entry into a manoeuvre instead of recovery from it, which is backwards and
will feel like sluggish controls. Related, and **not** to be resolved by guessing: the atmosphere
band-select threshold has only a read reference and a static zero in the binary, so its runtime
value is unknown; the dense band is established by arithmetic, not by reading the flag. Do not
"fix" that flag on the strength of this item.

---

# What this plan leaves open

Every item landed; these are the threads it deliberately did **not** close, each already carried by
a live `backlog.md` entry or a filed capture. Nothing here is a regression — it is the honest
residue of measuring a decode against footage.

- **The zero-thrust drag deficit (`CAP-05`, `decel-290-150`, `accel-150-290`).** The force path is
  byte-verified against the executable (B14) and the footage still disagrees by a near-constant
  ΔC_D ≈ 0.112 that no decoded mechanism produces; no constant closes the set. All three rows are
  informational with the record named in the probe comment. B12's re-playtest debt — the polar is
  much stronger below cruise than what it replaced — is owed to a **human at the controls**, not to
  an instrument.
- **The banked-turn rate: ≈1.6–1.7× fast, one number across two manoeuvres.**
  `sustained-turn-rate` 32.83 against 18.95 °/s, and the knife-edge nose drift and heading rate
  carry the same ratio (D31). C22 disproved the bank coupling as its cause, and D33 removes
  `highGs`; **`turn_fade_in` / `turn_fade_out` are what is left**. `sustained-turn-speed` and
  `sustained-turn-sink` are the same manoeuvre's other two legs and re-assert with the rate row.
- **The sustained climb is +25 % (204.04 against 163.05 mph) and the wrong shape** — the original
  undershoots its plateau and climbs back out of it, the model decays monotonically to it. The
  leading candidate is the probe's α against a 90° pull whose nose angle the clip's saturated ADI
  cannot read; `CAP-20` was filed to answer exactly that (D32).
- **Stall speed against the footage (B15).** The decoded 1 G formula gives the Bloodhawk 56.5 mph
  against the clip's ≈76 mph nose-drop; the `nom_gravity`-scaled convention lands closer but breaks
  the decode's own two-figure anchor. Recorded as a conflict rather than resolved by picking the
  convention that flatters one clip.
- **Decoded but unimplemented, on purpose.** The throttle lever's 0.5/s slew with no idle floor
  (B14) — a feel/transient gap; boost (lever 1.8, drag ×0.8), which the remake does not have at all
  (B13); the AI flight path's thin-band aero and zero-incidence flight, which belongs to M4 (B14).
- **Present-but-unreachable, built nowhere and pinned by a test.** The high-speed pitch fade (C24)
  and the G/AOA limiters (D33). Both are real code in the original on thresholds this install
  authors out of reach; both are documented in `docs/org/flightModel.md` so they are not
  re-discovered as missing features.
- **`TUNE` survivors in `FlightModel.cs`: `StallNoseRate` and `KnifeAlignFloor`** — the whole list
  after `ClimbGravityScale` retired (D32) and `KnifeNoseSag`/`KnifeNoseRate` retired (D31).
  `KnifeAlignFloor` is kept on a measurement the decode is silent about, not on a fit.
- **Owed at the controls:** `PT-28`, `PT-41`, `PT-43`, `PT-45` ([`playtest.md`](../../playtest.md)) —
  the flight model has been rebuilt on decoded mechanisms and has not yet been flown by a human
  across a whole session.
