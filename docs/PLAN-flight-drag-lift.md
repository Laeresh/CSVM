# Flight model — drag law, angle of attack, and induced drag

**ACTIVE PLAN** (written 2026-08-07). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This is the focused flight-model plan that `PLAN-m3-polish-6.md` and `PLAN-m3-polish-10.md` both
deferred: `BL-092` (no induced drag — a hard pull costs us no speed) and `BL-247` (the original
holds altitude at 100° of bank; our `wingVert` lift model cannot), taken together because `CAP-01`
is one clip that constrains both and because they are the same mechanism seen from the drag side
and the lift side. Both were re-verified still-open on 2026-08-07 against the record
(`git log --grep=BL-092` returns only measurement and planning commits — `916379a`, `33e3dbe`,
`355ee81` — and no landing commit) and against the code (`FlightModel.cs:116` still carries
`LowSpeedDragBlend = 0.35f`; `FlightModel.cs:316-317` still computes lift as `speedLift * wingVert`
with no pull term anywhere).

Scope grew during the 2026-08-07 grilling beyond what either entry anticipated, and the plan says so
up front: it also lands the open half of `BL-148` (the 1/8-throttle equilibrium and the
throttle→thrust curve) and the `LowSpeedDragBlend` bullet of `BL-115`, and it **refits
`ThrustConst`**, which the codebase currently documents as a measured constant. The reason is in
Decisions #3 below — `ThrustConst` is pinned only relative to the drag shape this plan replaces.
**Deliberately excluded:** the 33 vs 18.95 °/sim-s banked-turn pitch-rate gap (recorded, not fixed —
it needs a capture nobody has filmed), `BL-095`'s `player.json` decode as a whole (still blocked on
`CAP-02` for its ground-blow half; this plan consumes only the AoA block and marks that consumption
as a hypothesis under test), and `ClimbGravityScale`, which `CAP-05` proved it cannot settle.

## Milestone goal

- A hard pull costs speed: a sustained max-pull turn settles at the original's measured 222.94 mph
  against a 298.96 mph level cruise, and a full-pull zoom bleeds toward the original's 104 mph apex
  instead of arriving at 266.
- Low-speed drag matches the original's thrust-free measurement to within its fit band, instead of
  being 4–21× too strong.
- The aircraft can fly the `CAP-01` manoeuvre at all — 100° of bank held with altitude sinking at
  ~1.85 ft/sim-s, rather than falling out of the sky.
- Angle of attack exists as a modelled quantity, so lift and induced drag key on one shared
  mechanism instead of two unrelated proxies.
- `--dump-flight` carries the `CAP-01` and `CAP-05` numbers as rows, so every claim above is
  defended against regression.

**This plan does not close the banked-turn pitch-rate gap.** Once lift can hold 100° of bank we will
sweep heading at ~33 °/s against the original's measured 18.95 — 1.74× too fast. No capture we hold
explains it, and inventing a rate limiter to close it is precisely the wrong-mechanism fix that
`BL-092` trap (b) and `BL-124`'s history both warn about. It is recorded as an informational probe
row and a named capture request, and nothing else.

## Decisions (2026-08-07)

Settled by grilling before any code was written. This table is the authority where the prose below
contradicts itself.

| # | Question | Decision |
|---|---|---|
| 1 | `BL-092` alone, or jointly with `BL-247`? | **Joint** — the fit target for `BL-092` is the `CAP-01` plateau, which is unflyable until `BL-247`'s lift can hold the bank; and `BL-247` owns the knife-edge terms that share the same `1 − wingVert` quantity |
| 2 | Base drag-law rewrite before or after the induced term? | **Before** — the `+0.380 A` figure is quoted against the *current* blend; fitting induced drag on a base curve we have measured to be wrong bakes in the error |
| 3 | Is `ThrustConst` 184 (`A` = 60 m/s²) fixed? | **No — refit it** with the drag shape. It is pinned by `accel-150-290` and `terminal-dive` *agreeing*, which they do only given the current drag shape. At `A` = 60 no drag shape satisfies all three measurements |
| 4 | Drag-curve form | **Fit `(A, p_lo, p_hi)` numerically against the real integrator.** Single exponent if it fits all three; **piecewise split at fd** if it does not — the seam sits at the normalisation point where `D(fd) = A` is already pinned |
| 5 | Thrust linear in throttle? | **No — `throttle^k`**, `k` ≈ 1.46, determined (not guessed) from the measured 137.9 mph 1/8-throttle equilibrium once `p_lo` is known. Full throttle is `1^k = 1`, so no asserted row moves |
| 6 | What does induced drag key on? | **Angle of attack**, α = angle(nose, `VelocityDir`) — the one quantity that serves both entries, matches the authored `player.json` block, and separates `CAP-01` from `CAP-05` without being a function of bank |
| 7 | How deep does the α rework go? | **Re-key the existing terms**, don't rebuild lift as a perpendicular force. Keeps every calibrated steady rate and the whole asserted suite intact |
| 8 | Induced-drag functional form | **`D_i = A · C_i · sin²α`**, clamped at `maxAOA` 46°. Textbook shape; doesn't saturate where `liftFrac` does; vanishes exactly at α = 0 so cruise and the terminal dive are untouched by construction |
| 9 | The 33 vs 18.95 °/s banked-turn gap | **Fit drag to the measured plateau, record the rate gap as open.** Speed and turn rate are separable — the plateau is an along-path force balance we can match honestly |
| 10 | If the joint fit misses a tolerance? | **Go piecewise** (Decision 4's fallback) rather than widen a tolerance or drop a measurement. Record which shape the data chose |
| 11 | Probe policy | Assert what is measured; leave known-broken things informational and *visible*. Promote `eighth-throttle-speed` and `decel-290-150` to asserted — their "informational because the throttle curve is undecoded" rationale stops being true |

## ⚠ Read this before implementing anything

Four claims that are written down in `backlog.md`, `FINDINGS.md` or the code and are **wrong**.
They died to arithmetic during the 2026-08-07 grilling, before any code was touched. This is the map
of the minefield, not blame — each one is individually plausible and each one is load-bearing.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "`CAP-05` is an independent confirmation of `BL-092`'s `x^2.67`" (`backlog.md` `BL-115`, `BL-148`, `FINDINGS.md`) | `x^2.67` at the four probe points gives **1.48 / 3.64 / 7.55 / 9.43** m/s² against measured **0.36 / 1.11 / 2.82 / 3.74** — still 2.5–4× too much. Solving each point for its own exponent gives p = 3.69 / 3.80 / 3.94 / 4.00. `CAP-05` corroborates "much steeper than quadratic"; it does not corroborate 2.67 |
| 2 | `x^2.67` is a measurement of the drag shape (`BL-148`) | It is an artifact of assuming thrust is linear in throttle. `0.459^2.67 = 0.125` **exactly** — 2.67 is by construction the exponent that makes 1/8 throttle produce 1/8 thrust. `BL-148`'s own CAP-05-derived thrust(1/8) band of 2.8–4.5 m/s² already implies p ≈ 3.7–4.0 while its prose still says 2.67 |
| 3 | A single power law can replace the blend | `terminal-dive` is an **asserted** row: `D/A = 1 + g·sinγ/A = 1.315` at x = 1.182. `x^3.9` solves to x = 1.072 → **322 mph** against 355.2 ± 6. Solving the dive for its own exponent gives p ≈ 1.64. The two ends of the curve demand incompatible exponents at fixed `A` |
| 4 | `ThrustConst` 184 is independently measured ("THE measurement that sets `ThrustConst`", `FINDINGS.md`; `FlightModel.cs:40-49`) | It is pinned by `accel-150-290` and `terminal-dive` agreeing — *given the current drag shape*. Integrating `dv/dt = A(1 − x^p)` with `p_lo` = 3.9 gives **~2.0 s** against 3.76 ± 0.40, and still ~2.1 s at the favourable end of `CAP-05`'s band. Three measurements, no solution at `A` = 60. Freeing `A` reconciles them near 35–40 m/s² |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2 | Three independent measurements of the original, one of them thrust-free. Confirm the fit reproduces them, then land it. |
| **Direction sound, magnitude a judgement call** | C21, C22 | `CAP-01` pins the induced-drag magnitude at one point only; both segments sit at the same load factor, so the *exponent* is our choice (Decision 8) and `C_i` is a fitted constant, not a measured one. |
| **Leads only — no mechanism yet** | B11, B12 | `liftAOAs [5,9]` as a lift ramp in α is an *inference*, not a decode. Budget for it being wrong; a disproof here is a success. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. This plan runs in
`.claude/worktrees/bl092-bl247` on branch `worktree-bl092-bl247`.

## What the data actually ships

**The three measurements that over-determine the drag law.** All Bloodhawk, all from
`analysis/video-flight-calibration/`, with `A` = 60 m/s² and `g` = `nom_gravity` 20.0 as the model
currently stands:

| measurement | source | value | what it constrains |
|---|---|---|---|
| low-speed drag, thrust-free | `CAP-05 Stall 0% Thrust no input` | `D` = **0.36 / 1.11 / 2.82 / 3.74** m/s² at x = 0.25 / 0.35 / 0.46 / 0.50 (absolute, not relative to `A`) | the curve below cruise, with **no thrust term assumed** — the strongest evidence in the set |
| level acceleration | `FINDINGS.md` | 150 → 290 mph in **3.76 s** | `A` together with the curve below cruise |
| terminal dive | `FINDINGS.md` | **355.2 ± 0.4 mph** = 1.182 × fd at γ = 70.7° | the curve above cruise |
| 1/8-throttle equilibrium | `FINDINGS.md` | **137.9 mph** = 0.459 × fd | the throttle→thrust curve, once the drag curve is known |
| level full-throttle equilibrium | `FINDINGS.md` | 300.4 mph (298.96 ± 0.20 re-measured 08-03) | **nothing** — it is fd_speed by construction for any drag shape (`BL-148` trap (b)) |

**`CAP-01`, the induced-drag clip.** Full throttle, stick full back throughout (pilot-confirmed),
Bloodhawk, ~3,200 ft. Holds **+100 ± 4°** of bank for **15.9 sim s** and sweeps **449.8°** of
heading — a true sustained equilibrium.

| segment | speed | dV/dt | altitude |
|---|---|---|---|
| cruise, pre-pull (7.6 sim s) | **298.96 ± 0.20 mph** | +0.06 mph/sim-s | +5.7 ft/sim-s |
| bleed-in (5.6 sim s) | 237.2 mph mean | **−7.50 mph/sim-s** | +6.6 ft/sim-s |
| **sustained turn (15.9 sim s)** | **222.94 ± 1.77 mph** | −0.35 mph/sim-s | −1.85 ft/sim-s |

Turn geometry: **18.95 °/sim-s** (fit residual sd 0.39°) at 222.9 mph, i.e. `V·ω` = 32.96 m/s²
lateral = 1.65 × `nom_gravity`.

**The unconsumed `player.json` AoA block** (`BL-095`, `BL-092` trap (a), `BL-247`): `maxAOA` **46.0**,
`liftAOAs` **[5, 9]**, `lift_accel_rate` **0.75**, `highGs` **[9, 15]**, `lowGs` **[−6, −9]**. Units
unverified. This plan consumes `maxAOA` as a clamp and `liftAOAs` as a lift ramp in α, and treats
both as hypotheses under test (see B12 traps).

**Why α separates the two clips — the observation this plan is built on.** In our model α settles
where the path-chase cancels the pitch rate: `α_ss = ω_pitch / align`. With `AlignRate` = 4 /s
(`FlightModel.cs:50`) and the calibrated 33 °/s sustained pitch rate, a wings-level full pull gives
**α_ss ≈ 8.25°** — inside `liftAOAs [5,9]`. `CAP-05`'s knife-edge measured the path lagging the nose
by **4.8° / 7.2° / 8.3°**, spanning the same band, while the aircraft falls out of the sky at
0.68–1.13 °/sim-s of heading. `CAP-01` at full back-stick works out to **α ≈ 18°**, past the ramp,
holding altitude at 18.95 °/sim-s. That is the 17–28× separation the `CAP-05`/`CAP-01` pairing
demands, produced by a quantity with **no bank term in it** — which is the shape `BL-247` trap (a)
says the answer must have.

**What the model looks like today.** `FlightModel.cs` has no angle-of-attack state. Rotation is
kinematic (`BodyRates` → `Attitude`, lines 200-235); the flight path separately *chases* the nose via
`align` (line 349), so the nose/path angle is an emergent lag that is never named. Lift is
`speedLift * wingVert` (lines 314-317) — speed × bank, with no pull term — and it only cancels
gravity's cross-path component (line 337); it does not turn the path. Drag is
`_maxThrustAccel * Lerp(x², x, 0.35)` (line 330), a function of speed alone.

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

### Wave A — Base drag and thrust

1. ☑ Fit `(A, p_lo, p_hi, k)` numerically against all four measurements
2. ☑ Land the fitted constants and the drag/thrust probe rows

### Wave B — Angle of attack and lift (`BL-247`)

11. ☑ Introduce α as a modelled quantity
12. ☑ Re-key lift on `f(α)`; revisit the knife-edge terms
13. ☐ Sustained-turn probe rows

### Wave C — Induced drag (`BL-092`)

21. ☐ The `sin²α` induced-drag term, `C_i` fitted to the `CAP-01` plateau
22. ☐ Assert the plateau; record the turn-rate gap

### Wave D — Records

31. ☐ `backlog.md`, `PROJECT_CONTEXT.md` and docs

## Dependency and parallelism notes

Strictly linear — every wave depends on the one before, and no two items may run in parallel
worktrees because **every code item edits `FlightModel.cs`**.

A1 blocks everything: until `A` and the drag exponents are refitted, every downstream number is
quoted against a base curve we have measured to be wrong (Decision 2). A2 must land before B11 —
`α_ss = ω_pitch / align` is what B11 relies on, and A2 can move nothing that affects it, so B11's
premise wants to be re-checked against a landed, green suite rather than the current one.
B12 blocks C21 outright: `C_i` is fitted to the sustained-turn plateau, which is **unflyable** until
lift can hold 100° of bank. C21 → C22 is a chain (you cannot assert a row before its constant is
fitted). D31 last, because `backlog.md` records which shape the data actually chose.

File contention: A2, B11, B12, C21 all edit `FlightModel.cs`; A2, B13, C22 all edit
`CSVM/src/Testing/Probes.cs`. One agent, in order.

---

# Wave A — Base drag and thrust

## A1 ☑ Fit `(A, p_lo, p_hi, k)` numerically against all four measurements

**Landed** 2026-08-07. `ThrustConst` **184 → 107** (A = 60.04 → 34.92 m/s² on the Bloodhawk),
`LowSpeedDragBlend` 0.35 replaced by `DragExpLow` **3.278** / `DragExpHigh` **2.663**, and thrust
made sublinear with `ThrottleExp` **1.236**.

**Verified** against the real `FlightModel.Step` integrator via `Probes.FlightEnvelope`, on all
eleven airframes. Five independent measurements of the original now reproduce:

| scenario | before | after | original |
|---|---|---|---|
| `accel-150-290` | 3.73 s | **3.75 s** | 3.76 ± 0.40 |
| `terminal-dive` | 355.92 mph | **355.37 mph** | 355.20 ± 6 |
| `eighth-throttle-speed` | 85.96 mph | **137.87 mph** | 137.90 |
| `decel-290-150` (zero throttle) | 1.86 s | **6.48 s** | 7.04 ± 1.0 |
| `CAP-05` drag ×4 | 4–21× high | **within 4%** | 0.36/1.11/2.82/3.74 m/s² |

All eight previously-asserted rows stayed green and unmoved. Piecewise was **necessary and the data
chose it**: a single exponent puts the terminal dive at 344.5 mph against 355.2 ± 6, outside by 1.8×
the tolerance.

**Three findings that were not what the item went looking for.**

1. **The 1/8-throttle equilibrium is airframe-independent, and now falls out rather than being
   fitted.** Every one of the eleven planes settles at exactly **0.457 × fd with a settled path of
   0.0°** — a true level equilibrium — against the original's measured 0.459. Before, they scattered
   0.271–0.347 and all *sank*. The fraction is `(throttle^k)^(1/p_lo)`, which depends only on the
   exponents and not on `A`, so the airframe-independence is structural.
2. **`decel-290-150` was flying the wrong throttle setting.** The probe modelled it at 1/8; the clip
   holds 8/8, cuts to **0/8**, and touches nothing else in level flight (pilot-confirmed
   2026-08-07). At 1/8 the model reads 12.10 s against the 7.04 target and *no* power law can fix
   it — 150 mph sits 8% above the 1/8 equilibrium, so the approach is asymptotic and the required
   e-folding rate is unreachable at any exponent. That looked like a sixth contradictory
   measurement and was not one. Corrected to zero throttle, it is a pure drag probe and lands at
   6.48 s. **The 7.04 s measurement was right; our scenario was wrong.**
3. **`MaxDiveSpeedFrac` did not need raising** — the opposite of what was predicted. The fitted
   `p_hi` = 2.663 is steeper than the ~1.64 the hand-estimate assumed, so every airframe's terminal
   moved *down*: the Balmoral went 1.678 → **1.590 fd**, further clear of the 1.75 backstop.

**⚠ Deviation from this item as written.** A1 said it would produce numbers without touching
`FlightModel.cs`, by sweeping `Config` keys. That is not possible: `Config.Load` resolves through
`ProjectSettings.GlobalizePath`, a Godot native call unavailable in the headless test host, and
there is no programmatic setter. A `Config.SetOverride` seam was considered and **rejected by the
user** — `Config.cs:64-68` warns that overrides make a capture a function of uncommitted state, and
the repo has no test that overrides config at all. Fitting was done instead by editing the in-code
constants and re-running the real probe suite, with closed-form algebra
(`scratchpad/startvals.py`, not committed — game-data-free but throwaway) only to seed the search.
The trap the item raised still held: the seed values were re-checked against the integrator and the
integrator is what the numbers above come from.

**Original approach (kept for reference).**

**Goal.** A defensible set of numbers for maximum thrust acceleration, the drag exponent(s) and the
throttle→thrust exponent, produced by a fit that reproduces `CAP-05`'s four absolute drag points,
`accel-150-290` = 3.76 s, `terminal-dive` = 355.2 mph, and the 137.9 mph 1/8-throttle equilibrium
simultaneously — plus a written record of whether one exponent sufficed or the piecewise split was
needed.

**Evidence (confidence: traced).** All four measurements are tabulated in "What the data actually
ships" above, each cited to `FINDINGS.md` or the `CAP-05` decode. The contradiction at `A` = 60 is
⚠-table rows 3 and 4, and both were derived by hand-integrating `dv/dt = A(1 − x^p)` over the
acceleration window. **The hand method was calibrated first**: run against the *current* blend it
returns 4.00 s where the passing suite asserts 3.76, i.e. it reads ~6% high — which is what makes it
trustworthy enough to find a 2× discrepancy and *not* trustworthy enough to set a constant. Hence a
numerical fit. Hand-solved starting points, for sanity-checking the fitter's output only:
`A` ≈ 35 with `p_lo` ≈ 3.23 satisfies `CAP-05`-central + acceleration; `A` ≈ 38 with `p_lo` ≈ 2.76
satisfies the favourable end of `CAP-05`'s band; `p_hi` ≈ 2.4–2.6 follows from the dive at those
`A`; `k` = ln(D(0.459)/A) / ln(0.125) ≈ 1.46.

**Approach.** Write the fitter as a probe or test that drives the **real `FlightModel.Step`
integrator** — not a reimplementation of the force law, which would be free to be right about a
model we don't ship. Parameterise `_maxThrustAccel`, the drag exponent(s) and the throttle exponent
through `Config` keys so the fitter can sweep them without a recompile (they are being added as
config-backed constants in A2 anyway). Objective: normalised residuals against the four
measurements, each weighted by its own stated tolerance. Try the single exponent first and only
admit `p_hi` if the single-exponent residual puts any asserted row outside its band (Decision 10).
Do **not** touch `FlightModel.cs`'s constants in this item — A1 produces numbers and a record; A2
lands them.

**Model recommendation.** high — the whole plan rests on this fit being right, and the failure mode
(a fit that confirms whatever it was fed) is the specific trap `FINDINGS.md` documents twice.

**Verify.** The fitter's own residuals, plus a re-run of `--dump-flight` at the fitted values showing
`level-top-speed`, `accel-150-290`, `terminal-dive`, `roll-360`, `pitch-rate`, `yaw-360`,
`altitude-cap` and `level-speed-near-cap` all still inside tolerance. Take a **baseline
`--dump-flight` for every airframe before changing anything** — `ThrustConst` scales all eleven
planes together, so this is a global change and an unchanged number elsewhere is not evidence unless
you have seen it able to move.

**⚠ Traps.**
- **The degeneracy trap, restated.** `FINDINGS.md` warns twice about fits that confirm whatever they
  are fed (the `g`/`C` valley, the stall-lamp needle). A four-parameter fit against four
  measurements can land anywhere. The defence is that the constraints are *sequential, not
  simultaneous*: `CAP-05` fixes `p_lo` with no thrust term in it at all, the dive fixes `p_hi`, and
  only then does the 1/8-throttle equilibrium fix `k`. If the fitter has to trade `p_lo` against `k`
  to converge, something is wrong — stop and say so.
- **`CAP-05`'s drag values are absolute (m/s²), not fractions of `A`.** Fitting them as fractions
  will silently succeed and be wrong, because `A` is one of the free parameters.
- **`level-top-speed` cannot validate anything here.** It is fd_speed by construction for any drag
  shape and any `A` (`BL-148` trap (b)). A green row there means nothing; it is in the suite
  precisely because a thrust change could break the *construction* silently.
- **Don't solve `ThrustConst` from the level-1 engine row.** `FINDINGS.md`: the Bloodhawk's `engine`
  is 11 (Lvl-2, 0.62); using the level-1 row (0.47) inflates the constant by 32% and survives every
  check that only ever sees `A`.

## A2 ☑ Land the fitted constants and the drag/thrust probe rows

**Landed** 2026-08-07 with A1 (the two are one commit — the Config seam's rejection collapsed the
produce-numbers/land-numbers split that separated them).

**Verified.** `.\RunTests.ps1`: build clean, **616/616** units, **26/26** engine suites, engine
errors clean. Goldens: **4 of 13 moved**, rebaselined — `empty-stage`, `c1-flight`,
`c1-destroy-effects`, `c1-crash`. Those are exactly the four shots that fly a plane on a `--hold`
path; the nine freecam/viewer/static shots are byte-identical. Two of the four hold **part
throttle** (0.6 and 0.5), which is where `ThrottleExp` bites hardest. Confirmed able-to-fail and
deterministic before rebaselining: all 13 pass at the branch point `c1861f3`, and two independent
runs with the change produced identical new hashes. Both flight images inspected — level, stable,
gauges and HUD rendering; not a plane falling out of the sky.

**Deviations from this item as written.**
- `decel-290-150` was **not** promoted to asserted. Its stated reason for being informational (the
  throttle curve is undecoded) did expire, but the row was found to be flying the wrong throttle
  setting (A1 finding 2) and the user's call was to keep it informational pending the playtest.
  `eighth-throttle-speed` was likewise left informational for the same reason.
- The four `CAP-05` drag points were **not** added as probe rows. They were used as fit targets and
  are reproduced within 4%, but they are not yet defended against regression. **Carried to B13.**
- `MaxDiveSpeedFrac` untouched (A1 finding 3), but its comment was corrected — it cited the
  Balmoral at 1.678 and A = 13, both now stale.
- `Probes.cs:812` carried a **second hardcoded copy** of the `thrustConst` default (184) that the
  model change did not reach, so the dump header mis-reported max thrust accel while the model
  itself was correct. Fixed, with a comment naming it as a duplicate.

**⚠ Owed, not done.** The playtest. The user flew this build and reports it **"feels like
coasting"** — which is precisely the `BL-148` trap (c) complaint that `LowSpeedDragBlend` 0.35
existed to answer, and this commit removes that guard. Their hypothesis: the original may have
something like airbrakes (not visible) when slowing. The zero-throttle correction removes the need
for that explanation at 0/8 — 6.48 s against 7.04 — but says nothing about part throttle, where the
model still takes 12.10 s to reach 150 mph at 1/8. **This is unresolved and blocks nothing in Wave
B/C, but it must not be forgotten at D31.**

**`CAP-31` opened 2026-08-07** to settle it (`playtest.md` §0): 8/8 → **1/8** level, held until the
speed stops falling, speedo and altimeter in frame. The 1/8 case has never been filmed — `CAP-05`
covers 0/8 only. Prediction recorded there before the capture so it can fail: **12.1 sim s** to
150 mph, settling at **137.9**. If the original gets there markedly faster while still settling near
137.9, no power law fits and the user's unmodelled-drag ("airbrake-like on throttle chop")
hypothesis becomes live.

**Original approach (kept for reference).**

**Goal.** `FlightModel.cs` carries the fitted drag curve and a sublinear throttle→thrust curve; the
suite asserts the `CAP-05` low-speed drag points and the two 1/8-throttle rows; every previously
asserted row is still green.

**Evidence (confidence: traced).** A1's output.

**Approach.** In `FlightModel.cs`: replace `LowSpeedDragBlend` and its `Lerp(x², x, 0.35)` (lines
116-120, 329-330) with the fitted exponent form; change `Throttle * _maxThrustAccel` (line 334) to
`Pow(Throttle, k) * _maxThrustAccel`; update `ThrustConst` (line 49). Every new constant gets a
`Config.GetFloat` read in the block at lines 175-193 — read unconditionally, per the comment there,
so `--dump-config`'s template and the orphan check stay honest. Raise `MaxDiveSpeedFrac` (line 64) if
and only if the fit makes it bind on a low-thrust airframe; it is declared a numerical backstop, not
a terminal speed, so that is a correction rather than a retune. In `Probes.cs`: add the four
`CAP-05` drag points as asserted rows (zero throttle, absolute m/s²), and flip
`eighth-throttle-speed` and `decel-290-150` from `info: true` to asserted (`Probes.cs:903-918`),
rewriting the comment above them — their stated reason for being informational is that the
throttle→thrust curve is undecoded, which this item ends.

**Model recommendation.** high — small diff, large blast radius: it moves a constant every airframe
and half the suite depends on.

**Verify.** `--dump-flight` green on every asserted row, for **all eleven airframes** against the
A1 baseline. Then the full 8-chapter `--freecam --chapter=<X>` regression. `RunTests.ps1`.

**⚠ Traps.**
- **`BL-148` trap (c) is the binding constraint here, not a footnote.** `LowSpeedDragBlend` 0.35
  exists to answer a user report that a throttled-back plane barely decelerated. This item cuts
  low-speed drag by a large factor — *exactly the direction of that complaint*. The fix must come
  from the **shape** (drag still biting approaching fd), and the report must be re-flown before
  anything closes. It stays on the owed-playtest list either way.
- **The terminal dive is emergent, not clamped** (`FlightModel.cs:64-73`). If the fitted curve makes
  `MaxDiveSpeedFrac` bind, the dive row stops measuring the drag law and starts measuring the
  backstop — and it will still look green. Check the settled `x` in the row's detail string, not
  just the verdict.
- Adding config keys without the unconditional read breaks `--dump-config`'s orphan/missing check.

# Wave B — Angle of attack and lift (`BL-247`)

## B11 ☑ Introduce α as a modelled quantity

**Landed** 2026-08-07. `FlightModel.Alpha` (deg) = angle(nose, `VelocityDir`), computed once per
step right after `var nose = -Attitude.Z`, before liftFrac/drag/gravity are computed — a new,
separate read from the existing `pathDot` guard further down (that one re-reads the dot product
*after* the translation update, to condition `Slerp`'s axis for align; α reads the frame-start
value so a later lift/drag re-key would see one consistent number). Reported in every
`--dump-flight` row's detail string, all eleven airframes.

**Verified.** `.\RunTests.ps1`: build clean, **616/616** units, **26/26** engine suites, engine
errors clean, **13/13** goldens hash-identical (this item touches no rendered state). The flight
suite (`FlightEnvelopeTests`) stayed green with all eight asserted rows unmoved — α is observed-only,
nothing consumes it yet.

**The central inference, tested as B11 asked.** Only two of the plan's four reference manoeuvres are
reachable at this stage — knife-edge and the `CAP-01` sustained turn both need lift re-keyed (B12)
and a probe row (B13) before they're flyable at all. Of the two that are:

| manoeuvre | scenario used | predicted | measured (Bloodhawk) |
|---|---|---|---|
| cruise (α → 0 safety) | `level-top-speed`, `accel-150-290`, etc. | ≈0° | **0.0°**, every level/near-level row |
| wings-level full pull | `pitch-rate` (full elevator, level, 120/200/280 mph) | α_ss ≈ 8.25° (`α_ss = ω_pitch / align`) | **9.7°** at all three speeds |
| wings-level full pull (sustained loop) | `zoom-climb` (full pull from 300 mph, α at min speed) | ≈8° | **12.5°** |

`pitch-rate` is the more faithful test of the equilibrium argument — short enough (2 s) that the
loop hasn't carried the plane far from the wings-level, near-constant-speed regime the formula
assumes. It lands **9.7°**, 1.45° above the 8.25° prediction and just outside the authored
`liftAOAs [5,9]` upper edge — close enough to call the inference **supported, not disproven**, but
not a clean hit either. `zoom-climb`'s 12.5° reads higher still; it is a longer, genuine loop (the
comment above it already notes a held full pull becomes one), so as the plane pitches through
inverted and back, `eff`, gravity's along-path share and speed all move away from the level-cruise
values the `α_ss` formula assumed, which plausibly accounts for the gap. Across all eleven
airframes the same shape holds — every plane's `pitch-rate` α sits within a couple of degrees of
its own `α_ss` prediction from its own pitch rate, and every plane's `zoom-climb` α reads higher
(`player_balmoral`, the slowest/heaviest, most extremely: 2.5° at `pitch-rate` vs 25.1° at
`zoom-climb`, because its zoom-climb loop takes 30 s and stalls near the top rather than settling).
**Not a disproof** — B12/C21 can proceed — but the margin is real and not free; C21's `C_i` fit
(which only ever sees `CAP-01`'s bank, not this straight-pull reading) is the next place this could
still go wrong, and the traps below carry forward accordingly.

**Evidence (confidence: lead-only for the interpretation; traced for the quantity).** The quantity
itself was already implicit — `pathDot = nose.Dot(VelocityDir)`, computed just before the align
slerp. What was a *lead* is the claim that our α lands in the authored `liftAOAs [5,9]` band; that
rested on `α_ss = ω_pitch / align` with `AlignRate` = 4, an equilibrium argument this item has now
checked against the real integrator (above) rather than by hand.

**Original approach (kept for reference).**

**Goal.** α = angle(nose, `VelocityDir`) is computed once per step, available to lift and drag, and
observable in `--dump-flight` — with its settled values in the four reference manoeuvres measured
and written down.

**Approach.** Hoist α out of the existing `pathDot` computation to just after `var nose = -Attitude.Z`
(line 309), so lift, drag and `align` all read one consistent value for the frame. Keep the existing
near-parallel guard at line 357 — it exists because Godot's `Slerp` throws on a sub-degree angle and
aborted the whole physics frame (a plane holding straight and level simply stopped flying). Add α to
the `--dump-flight` detail strings for the existing manoeuvre rows so B12's premise can be checked
rather than assumed.

**Model recommendation.** medium — mechanical, but in a file where a thrown exception kills the
physics frame silently.

**Verify.** `--dump-flight` reports α; the wings-level full pull should read ≈8° and the knife-edge
rows ≈5–8° if the premise holds. **This is the item where the plan's central inference gets tested.**
No asserted row may move — α is observed-only at this stage.

**⚠ Traps.**
- **α is an emergent lag in this model, not an aerodynamic state.** It exists because the path chases
  the nose with a finite rate. Do not describe it in comments as though we model aerodynamic
  incidence; we model a lag that behaves like one.
- **If the measured α does not land near the authored band, say so and stop.** The whole B/C
  mechanism rests on `liftAOAs [5,9]` being a ramp in this quantity. A disproof here is a successful
  outcome and should be written up, not engineered around.
- Near-parallel nose and path is the normal cruise state; anything reading α must be safe at α → 0.

## B12 ☑ Re-key lift on `f(α)`; revisit the knife-edge terms

**Landed** 2026-08-07. `liftFrac = speedLift * wingVert` became
`min(1, speedLift * wingVert * n(α))`, with `n` a load factor ramping 1 g → `LiftLoadMax` **9**
over `LiftAoaLo/Hi` **5°/9°**. All three are config-backed.

**The shape the item asked for is not the shape that works, and the arithmetic says why.**
B12 as written says "replace `liftFrac = speedLift * wingVert` with `speedLift * f(α)`" — i.e. drop
the bank carrier. That cannot be right, and it dies in one line: **level cruise and knife-edge are
the same α**. Both fly a horizontal path at neutral stick, so both sit at α ≈ 0 and gravity's
cross-path component is the full `g` in both. A term keyed on α *alone* must give them the same
answer, and one of them has to hold altitude while the other falls out of the sky. α cannot be the
whole carrier — it can only be a *second* factor. So the landed form keeps `wingVert` (trap (a)
satisfied literally: it is not flattened) and multiplies it by a load factor read off α. That is
also the textbook reading of `CAP-01`: holding altitude at 100° of bank needs
`1/|cos 100°|` = **5.8 g**, and `player.json` ships `highGs [9, 15]`, so the manoeuvre is inside
the airframe's authored envelope rather than requiring lift to stop depending on bank.

**Verified.** `.\RunTests.ps1`: build clean, **616/616** units, **26/26** engine suites, engine
errors clean, **13/13 goldens hash-identical** — including the four flown `--hold` shots, which
A2 moved. 8-chapter `--freecam` regression: all eight built, rendered and exited clean, zero errors.
A/B against `LiftLoadMax = 1`, which reproduces the old model exactly (METHOD-9's able-to-fail
control), swept over **all eleven airframes** through the real `FlightModel.Step` integrator:

| | before | after | original |
|---|---|---|---|
| sustained max-pull turn, sink (Bloodhawk) | 18.29 ft/sim-s | **−3.03** (a 3 ft/s climb) | 1.85 |
| same, settled speed | 301.47 mph | **299.26** | 222.94 |
| knife-edge, 35 s altitude lost | 539.98 m | **539.98** (bit-identical) | 540 |
| all eight asserted envelope rows | green | **green, unmoved** | — |

Every airframe's turn sink falls by 10–19 ft/sim-s (worst remaining: Balmoral 40.6, Warhawk 18.3 —
both are slow enough that their turn α sits *inside* the ramp rather than past it, so they collect
only part of the load factor). The knife-edge is **bit-identical on ten of eleven**.

**Three findings the item did not go looking for.**

1. **The ramp is inert by construction, and that was measured, not assumed.** Knife-edge at neutral
   stick settles at α = **3.2°** and every scenario the suite asserts sits at α ≤ **2.9°** — all
   below `liftAOAs`' 5° edge — while the sustained pull settles at α = 6–14°. That is why nothing
   moved: the term reads exactly zero everywhere the calibration lives. **The authored `[5, 9]`
   survives as a hypothesis**; it did not have to move, so B12's unit-scepticism trap was not
   triggered and nothing goes back to `BL-095`.
2. **The Balmoral has already spent that margin.** It knife-edges at α = **5.1°**, 0.1° *inside* the
   ramp, and gets back 2.2% of its 35-second altitude loss (466.6 → 456.5 m). Left as is — it is
   small, the Balmoral has no measured original, and moving an authored constant to protect one
   airframe is the fudge the trap warns about — but 5° is now a live boundary, recorded in the code.
3. **`CAP-01`'s own numbers are not those of a coordinated level turn, and this is worth knowing
   before C21.** 18.95 °/sim-s at 222.94 mph is `V·ω` = 32.96 m/s² lateral, which for a level turn
   implies a bank of `atan(32.96/20)` = **58.7°** — not the 100° the ADI centroid reads. Our model
   is self-consistent where the original is not: it settles at 75.4 m/s² lateral and **75.1°** of
   bank, exactly `atan(75.4/20)`. So either the ADI sky-centroid reading is not bank (`BL-247`
   trap (b) already trusts it only to ±4°) or the original is not flying coordinated. This does not
   change what B12 landed — the lift deficit was real at *any* steep bank, and it is gone — but it
   means "holds 100° of bank" should not be turned into an assertion at face value in B13.

**⚠ The knife-edge constants were NOT replaced, and that is a result, not an omission.** B12
inherited from `BL-247` the expectation that `KnifeNoseSag` / `KnifeNoseRate` / `KnifeAlignFloor`
must be replaced "at the same time" as the lift keying, because `knife = 1 − wingVert` is the same
quantity. They were left untouched because the re-key does not displace `wingVert` — it multiplies
it — so the three terms still run on exactly the quantity they were measured against, and the
knife-edge behaviour is bit-identical on ten of eleven airframes. `KnifeNoseSag`'s known divergence
(the original's sag is unbounded; ours settles) is untouched and still open.

**⚠ Owed, not done.** A cockpit A/B of the banked turn. The manoeuvre now *works*, but "works" here
means an instrument reading — nobody has flown it. Rides `BL-247`'s `Owed-playtest`.

**A probe-design warning for B13, paid for in this item.** A sustained-turn probe that *forces* the
bank with a roll input measured as `atan2(−X.Y, Y.Y)` is worthless: that angle stops meaning bank
the moment the nose leaves the horizontal, and the controller drove targets of 60°, 75° and 100° all
into the same 100°-bank, −75°-nose spiral at terminal speed — **identically with and without this
change**, which is what identified it as an instrument artifact rather than a model finding. The
free-roll probe (bank set at entry, no roll input, full back stick) settles honestly at nose 0.3° /
path 0.4° and is the one that measures anything.

**Original approach (kept for reference).**

**Goal.** The aircraft holds 100° of bank at full back-stick with altitude sinking ~1.85 ft/sim-s,
while knife-edge at near-neutral stick still departs — from one keying, with no bank term.

**Evidence (confidence: lead-only).** The `CAP-01`/`CAP-05` pairing in "What the data actually
ships": same bank by the same ADI measure, 17–28× different heading rate, opposite altitude
outcomes. A lift term keyed on bank alone must give these two the same answer, so bank cannot be the
carrier. `player.json` ships `liftAOAs [5,9]` and `maxAOA 46`; `BL-247` trap (a) names pull/AoA as
the obvious candidate.

**Approach.** Replace `liftFrac = speedLift * wingVert` (`FlightModel.cs:317`) with
`speedLift * f(α)`, `f` a ramp over `liftAOAs`. Then re-examine the three knife-edge terms that
`BL-247` inherited from `BL-124` — `KnifeNoseSag` 0.07, `KnifeNoseRate` 0.2, `KnifeAlignFloor` 0.35
— because `knife = 1 − |up·Y|` is `1 − wingVert`, the same quantity being re-keyed, and `BL-247`
says whatever replaces the lift keying must replace these at the same time.

**Model recommendation.** high — this is the item that can silently break knife-edge, the stall
interaction and the altitude cap at once.

**Verify.** New `sustained-turn` probe (B13) shows the bank held with sink ≈1.85 ft/sim-s. The
knife-edge behaviour must still *depart* (`CAP-05`: nose sags linearly at 0.69–0.89 °/sim-s with no
equilibrium, reaching −27° nose / −18.7° path by +36 s, 540 m lost). Full asserted suite green;
8-chapter regression.

**⚠ Traps.**
- **Do not "fix" this by flattening `wingVert`** (`BL-247` trap (a)). The same quantity drives the
  knife-edge nose-sag, whose presence and direction are proven by scripted test; a bank-independent
  lift term reproduces the turn and breaks that.
- **`KnifeNoseSag`'s magnitude is measured but its bound is wrong** (`FlightModel.cs:92-107`). The
  original's sag has *no equilibrium* — it spirals in. Do not retune the constant to close the gap;
  a bounded sag cannot produce a 36-second linear drift. Total altitude lost is nearly identical
  either way (540 m vs 634 m), so **a feel A/B on sink alone would pass a wrong model**.
- **The stall block owns the nose outright below stall speed** and the knife-edge term is gated on
  `!stalled` deliberately — that gate is what makes the interaction provably empty rather than
  merely benign (`FlightModel.cs:274-283`). Keep it.
- `maxAOA` 46 and `liftAOAs` are in **unverified units** (`BL-095`). Deriving a ramp from them is a
  hypothesis; if the fitted ramp needs values far from [5, 9], that is evidence the units are not
  degrees and belongs in `BL-095`, not a fudge here.

## B13 ☐ Sustained-turn probe rows

**Goal.** The `CAP-01` manoeuvre is a probe row, so B12 and C21 are both measurable and defended.

**Evidence (confidence: traced).** The `CAP-01` table above.

**Approach.** In `Probes.cs`, add a sustained-turn probe: full throttle, roll to 100° bank, hold full
back-stick, run to equilibrium. Three rows — settled speed (informational until C22 fits its
constant), sink rate (**assert** ≤ 1.85 ft/sim-s, `BL-247`'s target), heading rate (**informational**,
original 18.95 °/sim-s, detail string naming the 1.74× gap explicitly). Follow the existing `Row(…)`
shape and the `bhawk` guard — the Bloodhawk is the only airframe on video.

**Model recommendation.** medium.

**Verify.** The rows appear in `--dump-flight`; the sink row fails before B12 and passes after
(an unchanged number is not evidence unless you have seen it able to fail — check this deliberately).

**⚠ Traps.**
- The probe must reach a **sustained equilibrium**, not a transient: `CAP-01` is 15.9 sim s and
  449.8° of heading. A probe that samples too early measures the bleed-in, which is a different
  number (0.369 A vs 0.380 A) that happens to look plausible.
- Roll-in geometry matters: the original is at +100°, *past* vertical, read from the ADI sky-region
  centroid and trusted to ±4° (`BL-247` trap (b)).

# Wave C — Induced drag (`BL-092`)

## C21 ☐ The `sin²α` induced-drag term, `C_i` fitted to the `CAP-01` plateau

**Goal.** A sustained max-pull turn settles at 222.94 mph against a 298.96 mph level cruise — a hard
pull costs ~25% of top speed, held indefinitely.

**Evidence (confidence: direction-sound, magnitude a judgement call).** `CAP-01`'s plateau, and the
bleed-in transient reaching the same figure by an independent route at a different speed (0.369 A vs
0.380 A, 2.9% apart) — which is what makes the magnitude credible even though the shape is not
measured.

**Approach.** Add `dragAccel += A * C_i * sin²(min(α, maxAOA))` at `FlightModel.cs:330`. Config-backed
constant, TUNE comment. Fit `C_i` against the B13 sustained-turn row.

**Model recommendation.** high — the constant is a judgement call resting on a chain of prior fits.

**Verify.** `sustained-turn` speed lands at 222.94 ± 5. `zoom-climb` min speed should move toward the
original's 104 mph from our 266. Full asserted suite green — **especially `pitch-rate`,
`terminal-dive` and `level-top-speed`**. 8-chapter regression.

**⚠ Traps.**
- **`C_i` absorbs our turn-rate error and is therefore not transferable.** We turn at ~33 °/s where
  the original turns at 18.95, so our α in this manoeuvre is higher and a *smaller* `C_i` reaches the
  same settled speed. If the rate gap is ever closed, `C_i` must be refitted. **The comment beside
  the constant must say this.**
- **Trap (b): it must not slow the sustained pitch RATE.** Structurally safe by design — pitch torque
  (line 201) has no speed term and `eff` applies to yaw only — but `pitch-rate` is asserted at three
  speeds precisely to catch anything leaking in, so treat a green row as the check it is.
- **The exponent is unmeasured** (`BL-092` trap (c)). Both `CAP-01` segments sit at essentially the
  same load factor (`V·ω` 32.96 vs 34.02), so terms in `n`, `n²` or `ω²` fit equally. Decision 8
  picks `sin²α` on physical grounds, not evidential ones — say so in the comment.
- **Thrust vectoring is already handled** — thrust acts along the nose (line 334), drag along the
  path (line 335), so the `1 − cos α` loss falls out of the vector sum. The induced term supplies
  the deficit **minus** what vectoring already gives at our settled α. Adding the full deficit as
  drag double-counts it.
- **The deficit is drag-law-shape dependent** (`BL-092` trap (d)), and A1 has changed the shape.
  Recompute the target against the *landed* base curve; the entry's `+0.380 A` is quoted against the
  old blend and is stale.

## C22 ☐ Assert the plateau; record the turn-rate gap

**Goal.** Everything this plan fixed is defended by an assertion; everything it knowingly did not fix
is visible as an informational row with the capture that would settle it named.

**Approach.** Flip the sustained-turn speed row to asserted (222.94 ± 5). Add `zoom-climb` min speed
as an informational row (original 104 mph) — informational because its stick history is unknown
(`Probes.cs:923-924`: a held full pull is a loop, and the same session's loop passed 180° in ~6 s, so
10.5 s to apex was some other input) *and* because the turn-rate gap is open. Rewrite the
`zoom-climb` comment, which currently says "we model no induced drag at all".

**Model recommendation.** medium.

**Verify.** `--dump-flight` green; `RunTests.ps1`; 8-chapter regression.

# Wave D — Records

## D31 ☐ `backlog.md`, `PROJECT_CONTEXT.md` and docs

**Goal.** The record says what the data chose, including the four claims this plan disproved.

**Approach.** Delete `BL-092` and `BL-247` from `backlog.md` (deleted, not marked FIXED — Ground
rules), with the closing record in the commit message since `docs/HISTORY.md` is frozen (2026-08-06).
Update `BL-115` (its `LowSpeedDragBlend` bullet lands here) and `BL-148` (its open half lands here);
correct the `x^2.67` claim wherever it appears — `backlog.md` `BL-115`/`BL-148`,
`analysis/video-flight-calibration/FINDINGS.md:169`. Refresh `PROJECT_CONTEXT.md`'s "Current status"
and `docs/architecture.md`'s `FlightModel` entry. Add the part-deflected-pull and banked-turn
captures to `playtest.md` as new `CAP-` IDs; keep the `Owed-playtest` obligations from `BL-092`,
`BL-247`, `BL-115` and `BL-148` trap (c) alive on whatever entries survive.

**Model recommendation.** medium.

**Verify.** The duplicate-ID commit hook passes; the encoding tripwire passes; `git log --grep` finds
the closing record.

**⚠ Traps.**
- **Nothing here closes an `Owed-playtest`.** `BL-092`, `BL-247` and `BL-115` all owe a cockpit A/B,
  and `BL-148` trap (c) specifically owes the throttled-back-deceleration report. Low-speed drag
  drops by a large factor in A2 — the direction of the original complaint.
- Use the `/close-backlog-item` skill rather than hand-editing; it knows the ID-retirement and
  caveat-striking steps.
- ⚠ Never `Get-Content`/`Set-Content` these files from PowerShell without `-Encoding utf8` on both
  ends — see CLAUDE.md. Edit with the Read/Edit/Write tools.
