# Flight Model Parity

**ACTIVE PLAN** (written 2026-08-24). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's Current status names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan brings aircraft dynamics, control response, flight-adjacent animation and collision
physics to the closest reproducible match to the retail original. It schedules all fourteen open items
from `backlog.md`'s Flight model and collision physics section here: `BL-089`, `BL-120`,
`BL-271`, `BL-309`, `BL-310`, `BL-311`, `BL-381`, `BL-388`, `BL-393`, `BL-414`,
`BL-425`, `BL-437`, `BL-438`, and `BL-439`.

This session re-verified `BL-414`, `BL-437`, `BL-393`, `BL-425`, `BL-438`, and `BL-439`
against the current model, dossier and envelope probe. The remaining backlog-derived items retain
explicit re-verification steps below. Combat-AI decisions, weapons, cameras and ordinary audio are
out of scope.

## Milestone goal

- The live translational and rotational plants express every reachable decoded mechanism in the
  same place and order as the original, with deliberate product exceptions named.
- Every fitted or protective constant is decoded, removed, or recorded as an explicit CSVM
  invention with a reason and a test proving whether it binds.
- Near- and far-field AI, control-surface motion, collision response, nitro and turbulence either
  match shipped behavior or close as disproven design intent.
- A reproducible envelope report covers all eleven stock aircraft and separates executable-traced
  truth from video-derived corroboration.

**Parity does not mean blindly matching every video-derived number.** A byte-verified executable
path outranks a frame-derived measurement; conflicts remain recorded until a discriminating test
settles them.

## Decisions (2026-08-24)

| # | Question | Decision |
|---|---|---|
| 1 | Footage and a complete executable trace disagree? | **The executable trace wins**; footage does not justify a fitted multiplier. |
| 2 | Are CSVM safety nets parity behavior? | **Only as named product exceptions** whose stock-envelope reachability is tested. |
| 3 | Does the original's single-player pointer limit four-player CSVM? | **No**; widen player-only behavior to all human pilots deliberately. |
| 4 | A GDD-only feature is absent from executable and data? | **Close it as disproven/won't-do**; do not invent shipped behavior. |
| 5 | What is the boundary? | **Plant, controls, flight-adjacent animation and collision**; combat decisions, weapons and cameras stay separate. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `liftAOAs` is a load-factor ramp. | The parser cosines degrees and the live path uses it to blend relative wind toward the nose. |
| 2 | Drag is a polar in `C_L` and needs induced drag. | Raw argument order proves the variable is Mach; `C_L` is dead. |
| 3 | Knife-edge sag needs a fitted drop or weakened nose chase. | Decoded bank coupling plus weathervane produces the drift; neither lag reader carries a bank factor. |
| 4 | Pitch, yaw or roll need fitted `*Tune` multipliers. | `FUN_0048c470` contains no such factor; all defaults are 1. |
| 5 | Player and AI use different atmosphere bands. | The cited altitude zeroing belongs to the dead debug integrator; the live call is shared. |
| 6 | Spawn speed is plane-dependent. | The player spawn reads mission `PLAYER_INIT`; 120 mph belongs to a teleport cheat. |
| 7 | Every AI uses one plant at every range. | The original selects a simplified speed-hold plant beyond 1 km. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism** | A1-A4, B11-B13 | Confirm the trace, then implement or land the disproof. |
| **Direction sound, magnitude a judgement call** | C21-C23, D34 | Decode first; residual magnitude stays TUNE. |
| **Leads only** | D31-D33, E41 | Budget for investigation; each may end in a disproof. |

## What the data actually ships

- Eleven stock player airframes author weight, area, drag, control torques, reciprocal inertia,
  angular damping, return rate, rated speed and stall magnitude.
- `player.json` authors shared lift/AOA/G clamps, authority curves, gravity, ground blow and
  collision ranges. Stock values leave the pitch fade inert and the G command limiter a graze; the
  AOA window they author is reachable throughout normal manoeuvring.
- The executable supplies Mach drag, attitude thrust, bank coupling, throttle slew, atmosphere
  bands, collision impulse and the far-field AI branch.
- Nitro commands, gauges, animations, sounds and engine variants ship, but power, charge and decay
  dynamics are executable-resident.
- The current Bloodhawk report matches level speed, terminal dive, roll, sustained pitch and the
  measured C1B ceiling; climb shape, deceleration, yaw, turn and knife-edge drift remain conflicts.

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

### Wave A — Settle the translational plant

1. ☑ A1 Finish the fitted-constant audit (`BL-414`)
2. ☑ A2 Decode and port the target-velocity acceleration path (`BL-438`)
3. ☑ A3 Settle part-throttle equilibrium (`BL-439`)
4. ☑ A4 Resolve the live atmosphere band

### Wave B — Port known control and AI mechanisms

11. ☑ B11 Port the two latent control-authority terms (`BL-437`)
12. ☑ B12 Port the distant-AI simplified plant (`BL-425`)
13. ☑ B13 Reproduce control-surface motion (`BL-393`)

### Wave C — Replace invented collision behavior

21. ☑ C21 Complete collision damage and sustained-contact decoding (`BL-381`)
22. ☑ C22 Remove or justify the invented graze/stop laws (`BL-271`)
23. ☐ C23 Validate building-corner collision feel (`BL-120`)

### Wave D — Settle designed but unproven features

31. ❌ D31 Settle one-sided engine torque (`BL-309`)
32. ❌ D32 Settle roll-to-pitch coupling (`BL-310`)
33. ❌ D33 Settle ambient turbulence (`BL-311`)
34. ☑ D34 Reproduce nitro boost (`BL-089`)

### Wave E — Prove parity

41. ☐ E41 Recheck the autogyro low-speed nose-down report (`BL-388`)
42. ☐ E42 Publish the eleven-airframe parity ledger
43. ☐ E43 Enable the decoded AOA window and settle α against the original

## Dependency and parallelism notes

A1 precedes every edit to `FlightModel.cs`. A2 blocks A3 and the final envelope verdict. A4 may
run beside A2. B11-B13 are independent after A2, but B11 and B12 contend on `FlightModel.cs`.
C21 → C22 → C23 is a chain. D31 and D32 share the torque accumulator and run serially; D33 and
D34 are independent. E43 follows B11 and precedes E41 and E42, which both judge the plant with
the AOA window live; E42 follows every other item.

---

# Wave A — Settle the translational plant

## A1 ☑ Finish the fitted-constant audit (`BL-414`)

**Goal.** Decode, remove or explicitly classify every non-authored plant constant.

**Evidence (confidence: traced).** The executable retired the `*Tune` factors, stall multiplier
and knife-edge floor. The graze trio remains fitted; altitude and speed caps are CSVM safeguards.

**Approach.** Inventory every constant and `flightModel.*` override against
`docs/org/flightModel.md`; ablate each safeguard to prove whether it binds. Leave contact TUNEs to C21-C22.

**Model recommendation.** **high** — coupled physics and provenance judgement.

**Verify.** Add an able-to-fail inventory test; baseline the flight dump and run `RunTests.ps1`.

**⚠ Traps.** A config override is not necessarily fitted behavior. Do not count removed history.

**Landed.** Every constant in the plant is classified in `docs/org/flightModel.md`'s "The plant's
constant inventory", and `CSVM.Tests/FlightConstantInventoryTests` censuses the const fields and the
`flightModel.*` config block against it, so an unclassified number fails rather than arriving
quietly. Outside `Collide` the only constants that are not decoded are three named exceptions: the
2003 m altitude cap (footage, binds deliberately), the STALL lamp's 0.30 fraction (footage, a cue
that no force term reads) and `MaxDiveSpeedFrac` 1.75 (a numerical backstop, measured non-binding
with a 0.56 `fd_speed` margin on the tightest of the eleven). The `42.8 m` overshoot backstop is
removed: the altitude clamp deletes climbing velocity, which bounds overshoot to one frame of climb,
and the worst of the eleven reaches 1.46 m. Removing it leaves the eleven-airframe flight dump
byte-identical, while shrinking it to 0.05 m moves 22 lines of that dump, which is the control that
the instrument sees the clamp. The graze trio stays fitted and is deferred to `C21`/`C22`; the
`*Tune` trio is decoded-absent, held at 1 and now pinned there by the census.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 1998/1998 units,
93/93 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

## A2 ☑ Decode and port the target-velocity acceleration path (`BL-438`)

**Goal.** Spend `lift_accel_rate` exactly as the original does and recover its climb shape without tuning.

**Evidence (confidence: traced).** The original uses the lag vector as acceleration. CSVM derives a
load demand, adds separate forces, then chases the nose again. Three virtual calls remain undecoded.

**Approach.** Decode `0x48c6b6-0x48c6e7` first; document units, then replace `Step` only if
the trace proves the current composition is not algebraically equivalent. Keep an A/B seam.

**Model recommendation.** **max** — reverse engineering plus the highest-blast-radius edit.

**Verify.** A/B the full dump, especially climb 204.04 vs 163.05 mph and its missing undershoot;
sweep all eleven planes and run the complete suite.

**⚠ Traps.** Do not tune drag, gravity or the lag rate first. Do not restore pitch-scaled gravity.

**Landed.** The `0x48c6b6`–`0x48c6e7` range is decoded: the three virtual calls are one virtual
velocity getter (vtable slot `+0x4`, a world-velocity float3 pointer, m/s) fetched once per
component of the `liftAOAs` blend, so the player's `targetVelocity` is exactly the relative wind
`Step` already builds. The evidence line's core claim is DISPROVEN for the live near-field path:
`FUN_0048c470` spends the lag as the clamped lift DEMAND through `FUN_0041abd0`/`FUN_0048fc40`
(thrust, Mach drag, weight, force × 9.82/weight), and `FUN_0048e580` integrates `velocity += a·dt`
with no kinematic rotation onto the nose; only the far-field branch (crashed, or beyond 1 km,
`B12`'s plant) spends the raw lag as acceleration. The one real non-equivalence ran the other way:
`Step`'s extra exponential `VelocityDir` chase at `lift_accel_rate`, which doubles the unclamped
swing (`ω = rate·α` on both paths) and bypasses the G clamps. It is retired behind the A/B seam
`NoseChaseFactor` 0 (decoded-absent, config `flightModel.noseChaseFactor`, 1 restores the old
composition byte-identically). A/B per `METHOD-6`/`METHOD-9`/`METHOD-15`: both dumps from this
tree, and the seam at 1 reproduces the pre-change dump SHA256-identically while 0 moves 339 of
891 lines, so the instrument sees the change in both directions. The moved lines are all in
saturated/transient rows: Bloodhawk `pitch-rate` 35.87 → 32.43 °/s (read 33.00), knife-edge drift
1.19–1.21 → 0.86–1.08 °/s (filmed 0.69–0.89), `zoom-climb-min-speed` 176.7 → 117.9 mph (read
127.9), and the `ZzCadenceSweep` roll-off 26.8× → 42.0× against the original's 42×, closing that
standing 1.57× gap. The climb plateau did NOT move (204.03 mph, α = 0 is a fixed point of both
compositions), so the climb residual is not owned by this path; `A3`/`A4` own what remains. The
negative-`C_L` ceiling asymmetry in `FUN_0041abd0` (one-sided `min`, flat −1.8 floor) is recorded
in the dossier as an open difference. Dossier, inventory (+`NoseChaseFactor` row, config surface
7 → 8 keys), census and `KnifeEdgeTests`' α-window pin updated; `BL-438` deleted from `backlog.md`.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 1998/1998 units,
93/93 engine suites with engine errors clean. Six flight-involved goldens moved as the transient
predicts and were re-pinned after the user reviewed the old-vs-new montage; a subsequent check
pass reads 16/16 hash-identical against the new pins, exit 0.

## A3 ☑ Settle part-throttle equilibrium (`BL-439`)

**Goal.** Give each throttle setting a decoded or A2-derived equilibrium target.

**Evidence (confidence: traced).** Lever and 0.5/s slew are linear, but equilibrium remains
unresolved while A2's target-velocity contributions are unknown.

**Approach.** After A2, trace every throttle-dependent contribution and derive the curve analytically.

**Model recommendation.** **high** — bounded decode and arithmetic after A2.

**Verify.** Sweep throttle 0→1 for all airframes; assert monotonicity and decoded points with a failing perturbation.

**⚠ Traps.** An asserted probe target is not evidence. Do not substitute a new footage estimate.

**Landed.** The lever enters the live force path exactly once, as `avail *= lever` on the thrust
curve at `0x48fce7`; a census of every read of the current throttle `[obj+0x128]` in the live chain
returns four other sites and none of them is a force (the far-field cruise target `0x48c5a0`, the
effect-list delta `0x48e59b`, the fuel burn `0x48e603` and the 0.5/s slew `0x48e63f`–`0x48e6c3`).
Drag is reached only through the boost flag, which replaces the lever with a flat 1.8 rather than
scaling it. With one term scaled the level balance solves in closed form:
`lever(M) = DragFactor · M³ · (0.12 + 0.8M + 0.5M²) · (1.33k)^(1.41M) / (ThrustFactor ·
(0.84M + 0.112)² · (0.12 − M/60))`, losing one power of `M` below the thrust curve's 0.1 Mach floor
(which is written into `thrustAvail`'s own argument copy, so drag keeps the true Mach). Reference
area, air density, the speed of sound and the shared 0.73 all cancel, so the curve is one curve in
`ThrustFactor / DragFactor`, strictly increasing, and the full-throttle end reproduces the Drag
section's published `fd_speed` solve. The eleven-airframe table at 1/8, 1/4, 1/2, 3/4 and full is in
the dossier's new "Part-throttle equilibrium" section. **The decode agrees with the shipped plant,
so no code changed**: the dump's `level-top-speed` and `eighth-throttle-speed` rows sit within
0.05 mph of the solve on all eleven except the Balmoral's 1/8, which is the one stock lever whose
level solution (55.2 mph) falls below the speed at which its wings can still carry `nom_gravity`
(65.2 mph), where the plant descends instead and no decoded target exists. The climb residual is
re-attributed rather than closed: the lever is a linear multiply, so at the filmed clip's full
throttle it is a factor of exactly 1 and no throttle law of any shape can move a full-throttle
climb. With `A4` settling the band dense, the only owner left is the α the original's climb path
holds, whose arithmetic (0.550 available against 0.567 needed at a 90° nose and a 56.3° path) the
climb section already carries. `PartThrottleEquilibriumTests` flies all eleven airframes at eight
lever positions from above and below against the independently solved curve, and pins monotonicity,
the ratio-only dependence, the Mach floor's shape and the Balmoral's floor case, with a 5 % lever
error as the `METHOD-9` control. `BL-439` deleted from `backlog.md`; its `ThrustConst` warning was
already moot, that constant having gone with the fitted thrust scale.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 2019/2019 units,
93/93 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

## A4 ☑ Resolve the live atmosphere band

**Goal.** Determine which atmosphere band the retail process selects in flight.

**Evidence (confidence: traced).** Static bytes imply the unusable thin band above sea level; stall
and equilibrium arithmetic require the dense band.

**Approach.** Read `0x0071bb3c`, aircraft altitude and atmosphere outputs in a live retail process
at sea level and airborne; document the observation before changing code.

**Model recommendation.** **high** — narrow work requiring trustworthy live-process instrumentation.

**Verify.** Repeat live reads at two altitudes, then reproduce the selected band's arithmetic in a test.

**⚠ Traps.** Another static xref sweep cannot settle this. Prove binary and address (`METHOD-6`).

**Landed.** A live retail process holds **6561.6796875** at `0x0071bb3c`, which is 2000 m in
feet, so the dense band covers the whole envelope below 2000 m and the thin band is that ceiling's
regime. The slot is a BSS variable, not a shipped `0.0`, and its writer is `FUN_00463640` storing
through a base register at `0x46368b`, which is why an address xref sweep found nothing. The
airborne half is observed at the controls: a passive 10 Hz `ReadProcessMemory` sample through
menu, mission load and flight (3655 vehicle samples, 854 to 6936 ft) reads the threshold constant
throughout, the dense outputs (`a` 1109.54 ft/s, `rho` 2.2688e-3, `k` 0.98842) on every sample at
or below the line and the thin outputs (968.02, 1.356e-4, 0.7348) on every sample above it, with
130 clean transitions as the aircraft crossed the boundary in both directions; the nine
stragglers sit within 1.3 ft of the threshold, which is the skew between the altitude and
atmosphere reads. Band selection is `alt <= 2000 m` selects dense, live, both ways. The band
step function, its 2000 m boundary and the discriminating stall case are asserted in
`CSVM.Tests/AtmosphereBandTests.cs`. CSVM's dense-band constants match the flyable envelope;
no code change. The 2003 m `AltitudeCapM` reading of the same ceiling is a recorded lead, not
acted on here.

**Verified.** Full `RunTests.ps1` battery on the merged lane tree: build clean, 2008/2008 units,
93/93 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

# Wave B — Port known control and AI mechanisms

## B11 ☑ Port the two latent control-authority terms (`BL-437`)

**Goal.** Implement pitch-only high-speed fade and asymmetric AOA/G command limiting.

**Evidence (confidence: traced).** Both are decoded but inert on all stock airframes.

**Approach.** Split pitch's second stage from the shared low-speed ramp; apply the smaller limiter
only to pitch/yaw commands that increase nose/path separation.

**Model recommendation.** **high** — subtle signs and inert stock data.

**Verify.** Synthetic airframes make each term bind; stock envelope remains byte-identical.

**⚠ Traps.** Any stock-row change means the implementation or reachability reading is wrong.

**Landed.** Both terms are confirmed against the binary and implemented. The pitch fade is
`FUN_0048bdd0`'s second stage on the pitch output alone (`0x48be22`–`0x48be68`, the globals at
`0x0071c400`/`0x0071c404`), and it is now `FlightModel.PitchAuthorityAt`, the base ramp
(`RollAuthorityAt`, which is the whole of roll's curve) multiplied by it. The limiter is one scalar computed once per tick in `FUN_0048c470` and shared by pitch and
yaw: `min` of an AOA window `(cos α − cos maxAOA) / (1 − cos maxAOA)` (`0x48c9f4`–`0x48ca18`,
`0x0071c42c` confirmed as `FCOS` of the authored degrees at `0x4743a3`) and a ramp on the
**delivered, signed** body-up load factor over `highGs` (`0x48ca1e`–`0x48ca3a`) or `lowGs`
(`0x48ca45`–`0x48ca61`), the min at `0x48ca69`. **The sign rule is the opposite of what the dossier
concluded.** `FUN_0053fd40` at `0x48c9ae` builds the quaternion taking the nose onto `v̂`, the same
closing axis the weathervane uses, and the raw sign bits of the command and of that axis' component
are compared (pitch at `0x48cb52`); opposite signs soften. So it damps ENTRY into a departure and
leaves recovery free, not the reverse. Roll carries no test; a fourth consumer, the never-authored
`level_off_rate` torque at `0x48cedc`, is dead and unported.
**The reachability reading was wrong, and implementing the terms is what showed it.** Ablated one
half at a time against the eleven-airframe dump: the pitch fade and the G ramp leave it
SHA256-identical (`7BF4C7AE…`), the AOA window alone moves 286 of its 891 lines (`D2D682D8…`). The
G ramp's negative side is a graze rather than unreachable — the Bloodhawk reaches −6.23 G and the
Peacemaker −6.08 against the authored −6, 7.7 % and 2.7 % into a 3 G ramp — because the quantity is
signed, which the old "the demand is a length" argument was not measuring. The AOA half is not a
threshold at all: it is below 1 at every non-zero α, so at this plant's 32–38° it halves the
elevator, and at 1 it moves `pitch-rate` 32.43 → 22.51 °/s against a filmed 33.00 while moving
`sustained-turn-rate` 34.52 → 25.83 toward its own 18.95. It is therefore implemented and **held
off** by `AoaLimiterFactor` 0 (config `flightModel.aoaLimiterFactor`, 1 spends it), a recorded
divergence rather than a decode doubt: whether this plant's α is the α the original holds is a
whole-envelope question that belongs to `E42`. `LatentControlAuthorityTests` flies synthetic
airframes that author each term into reach and pins every shape and both sign directions, with a
stock-side control that pitch and roll authority are the identical number at every speed up to
`MaxDiveSpeedFrac × fd_speed` on all eleven; `ControlLimiterTests` now measures
`FlightModel.BodyUpLoadFactor` and pins the graze as a bounded fraction with a halved-`lowGs`
control. Dossier, inventory (+`AoaLimiterFactorDefault`, config surface 8 → 9 keys), census and
architecture updated; `BL-437` deleted from `backlog.md`.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 2052/2052 units,
93/93 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

## B12 ☑ Port the distant-AI simplified plant (`BL-425`)

**Goal.** Beyond 1 km, AI uses the original speed-hold plant and returns to near-field aerodynamics inside it.

**Evidence (confidence: traced).** Original far AI skips lift/drag, authority curves and bank coupling.

**Approach.** Plumb player position into the plant and select the branch at the decoded boundary.

**Model recommendation.** **high** — cross-module state plus a previously reverted prototype.

**Verify.** Spectated AI outside 1 km holds nose speed and stops bank-turning; inside is invariant.

**⚠ Traps.** This did not explain `BL-387`; do not revive that hypothesis.

**Landed.** The branch is confirmed against the binary and ported whole. `FUN_0048c470`'s opening
test takes the far branch when the crashed flag `[obj+0x384]` is set (`0x48c4ba`) or when the
aircraft is not the player and `FUN_00538920`'s **horizontal** squared separation `Δx² + Δz²`
exceeds the `1e6` m² at `0x00607a18` (`0x48c4e9`–`0x48c4fc`), a strict `>` at **1000 m** with no
hysteresis and no timer, so the branch is re-decided every frame in both directions. The far plant
is `target = nose · (throttle · fd_speed + 5)` and `a = target − velocity`
(`0x48c593`–`0x48c603`, the 5 at `0x006036bc` for anything that is not the player), written to the
same linear-acceleration output the near path fills, so the rate is exactly **1/s** and no
`lift_accel_rate` enters it. What it skips is fixed by two tests of one flag: the whole force build
including gravity (the jump at `0x48c643` past `0x48c648`–`0x48c8c3`), the three authority factors
and the reverse-authority factor, forced to 1 rather than computed (`0x48c8e5`, so `FUN_0048bdd0`
never runs and `B11`'s pitch fade cannot bite), the opposing-command limiter's scalar, forced to 1
past `0x48c93c`–`0x48ca79` (so neither `B11`'s AOA window nor its G ramp reaches a distant
aircraft), and the bank coupling (`0x48cc56`, past `0x48cc61`–`0x48cd3d`). **What still runs is as
decoded as what does not**: the three stick torques accumulate normally at authority 1, and the
ground blow `FUN_0048c220` is called at `0x48cf95` for every aircraft but a crashed player, which
corrects the dossier's "no ground blow or weathervane at all". The weathervane at `0x48cd6c` is
player-only anyway, and the arm a non-player takes instead is the dead `level_off_rate` auto-level.
A second dossier error is corrected with it: `0x48c520` is not part of the far-field test but the
`liftAOAs` wind-blend player compare, so it stands as `docs/architecture.md`'s evidence for the AI
force path. In code, `FlightModel.FarFieldPlant` is re-decided each `Step` from
`FlightInput.NearestHumanDistSqM`, which `FlightController` fills from the session's
`PlayerPositions` snapshot (the wreck fall re-reads it too, since the original re-tests the range
whatever is flying the hull). **Distance is measured to the NEAREST human pilot**, this plan's
Decision 3 and the single deliberate difference from the decode; the crashed-flag arm is not ported,
since CSVM's wreck already flies the near plant and that flag's writers are undecoded. The reverted
prototype was built to test whether this explains `BL-387` and measured that it does not (mean bank
66° against 64°); it was reverted because nothing reached it without session plumbing. This change
wires that plumbing, so the branch is reachable, and it lands on faithfulness alone with no bug
riding on it. `FarFieldPlantTests` pins each skipped term alone against a near-field control in the
identical state, plus the strict boundary, the held speed from both sides, the 1/s rate on an
airframe whose `lift_accel_rate` is 4, and two controls: the ground blow still runs far-field, and
an AI at 999 m integrates identically to one standing on the human. The `ai-far-field-plant` engine
suite owns the session plumbing (horizontal-only range, nearest-of-several humans, an unbound seam
staying near-field, the branch watched selecting in both directions). Inventory (+`FarFieldRangeM`,
+`FarFieldAiSpeedBonus`, config surface unchanged at 9 keys), census, dossier and architecture
updated; `BL-425` deleted from `backlog.md`. Proof of invariance: the eleven-airframe dump is
SHA256-identical to `B11`'s `7BF4C7AE…`, which it must be by construction (that dump is single-plane
and player-path, so the branch is unreachable in it), and the instrument is live on this tree, since
a 2 % `DragPolarQuad` perturbation moves 716 of its lines and restoring it returns the hash exactly.
No golden can move: no shot spectates an AI beyond 1 km, the two AI shots (`c1-targeting-hud`,
`c1-ai-wreck`) spawn their AI 250 m ahead of the player on its own heading and run 1.5 s and 4.7 s,
so the separation cannot approach the boundary. That is reasoned from the manifest, not measured
here.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 2079/2079 units,
94/94 engine suites (the new `ai-far-field-plant` included) with engine errors clean,
16/16 goldens hash-identical, exit 0.

## B13 ☑ Reproduce control-surface motion (`BL-393`)

**Goal.** Match six-list channel mixing, angles, smoothing and the player-only animation guard.

**Evidence (confidence: traced).** Angles and 2/s exponential smoothing are decoded; node-list population is not.

**Approach.** Decode list population, map nodes, then replace ±20°/linear 3/s behavior.

**Model recommendation.** **high** — mapping error would animate the wrong surfaces plausibly.

**Verify.** Script each axis and mixed pair; prove AI surfaces remain frozen in the original-equivalent mode.

**⚠ Traps.** Do not infer physical surface names from the six slots before decoding population.

**Landed.** The node-list population is decoded, and it settles the mapping the item was blocked
on: `FUN_004b27e0`, `FUN_004b2a40` and `FUN_004b2ca0` fill the six vectors at `obj+0x9c0` through
`+0xa10` with two `sprintf` loops each over the six format strings at `0x62aee8`–`0x62af2c`
(`l_aileron%d`, `r_aileron%d`, `l_elevator%d`, `r_elevator%d`, `l_rudder%d`, `r_rudder%d`), from
index 1 until a name lookup misses. The string order is the slot order, and nothing else writes
those vectors. The channels resolve the same way: `obj+0x100`, `+0x108` and `+0x10c` are written
one-to-one alongside `+0x114` (roll), `+0x11c` (pitch) and `+0x120` (yaw) at
`0x4922b7`–`0x4922d5` and `0x492b36`–`0x492b7e`, and the joystick read at `0x487559`–`0x487653`
takes them from axes X, Y and 5. So the mixed pair is the ELEVATORS carrying a differential roll
term at 36 % of the aileron gain, not an unknown surface: `l_elevator` is
`clamp(−0.6·pitch − 0.18·roll, ±0.6)` and `r_elevator` the same with the roll term added. The
smoothing helper `FUN_00460490` is `slot = target + (slot − target)·exp(−rate·dt)` with
`FUN_00460410` the `exp(−x)` (cubic Taylor below 0.1), so 2/s is an exact 0.5 s time constant, not
a per-frame fraction. The three appliers at `0x48eca9`–`0x48ecb7` run OUTSIDE the player guard,
which is how an AI aircraft poses surfaces from slots nothing ever writes. CSVM's ±20° linear
3 units/s behaviour is replaced: `ControlSurfaceMix` (new, engine-free) holds the five distinct
slots, the clamps and the exponential, `ControlSurfaceAnimator` keeps only the node side, and
`ControlSurfaces.Kind` splits the elevator per side because the differential needs it. The guard is
`FlightController.IsHumanPiloted`, this plan's Decision 3, so AI surfaces freeze and every human
pilot animates; no config key was invented and no plant constant moved, so the constant inventory
and its config census are untouched. `ControlSurfaceMixTests` scripts each axis alone, the mixed
pair on both sides of the clamp, the reverse-authority scaling, the exponential shape against a
`3·dt` frame rate, and the frozen-AI case with the guard-open run as its `METHOD-9` control.
`BL-393` deleted from `backlog.md`; the decode with addresses is in
`docs/org/flightModel.md`, "The original's control-surface animation". One named product
exception, decided by the user: CSVM's node matching also accepts `l_rudder_rotate` and the
digitless `l_elevator`, which the original's `%d` lookups miss, so the Fury's rudder animates in
CSVM where the original flies it frozen; the exception is recorded beside the population decode
in the dossier.

**Verified.** Full `RunTests.ps1` battery on the merged lane tree: build clean, 2059/2059 units,
93/93 engine suites with engine errors clean. One golden moved, `c1-flight`, the only shot with a
held stick input; the pair was reviewed by the user and re-pinned, and a check pass reads 16/16
hash-identical against the new pin, exit 0.

# Wave C — Replace invented collision behavior

## C21 ☑ Complete collision damage and sustained-contact decoding (`BL-381`)

**Goal.** Consume authored armour/health ranges and reproduce vertical-speed kill and multi-tick scrapes.

**Evidence (confidence: direction-sound).** `CAP-14` shows wall-tangential sink removal and a 40%
speed loss over 0.47 s; the decoded normal impulse cannot produce either.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-381` and current collision code.>
Trace the remaining contact path, then route damage through `PlaneDamage` and port only decoded terms.

**Model recommendation.** **max** — mixed decode, lifecycle and player-facing physics.

**Verify.** Graze spends armour before health; a scripted oblique wall scrape bleeds speed across ticks; `PT-53`.

**⚠ Traps.** Do not raise `bounce_factor` or preserve old graze tuning by default.

**Landed.** The re-verify found the item's first bullet already built: `BL-302`/`BL-402` routed
collision damage through the decoded law (`FUN_0048d2c0`, `Flight/CollisionDamage.cs`) with the
authored `armor_damage_range`/`health_damage_range` consumed and the pair spent armour-first
through `PlaneDamage.Apply`, so the backlog text was stale on that point. What was still open is
now decoded, and the answer is a DISPROOF of any missing velocity term: the whole contact path is
traced end to end (`FUN_0048e580` integrator → `FUN_0048d7f0` sweep → `FUN_0048d2c0` damage) and
the executable's only velocity edit on contact is the known normal impulse. What `CAP-14` filmed
is position-derived: `FUN_0048d7f0` REWRITES the frame's translation in place
(`0x48e065`–`0x48e0bb` player, `0x48db30`–`0x48db7c` non-player), landing the struck sphere
exactly at its contact point plus 0.03 m along the normal for the player alone (`0x006080c4`; a
non-player gets no offset, a crashed player no placement at all, `0x48dfbe`). That placement
cancels the whole frame's motion against the contact point, which is the filmed wall "sink
removal", and a sustained scrape is the placement plus the re-spent closing component iterated,
with damage repeating only while the severity cosine stays positive. Two decode corrections
landed with it: the partition's angular share is the inertia-MULTIPLIED momentum vector (the
`FDIV`s by `rec_moments_inertia` at `0x48e2e9`/`0x48e2fb`/`0x48e307`; the dossier's `I⁻¹` reading
and the port's `×RecInertia` were both inverted), and the angular deposit's inertia weighting
cancels through the accumulator round-trip, so the net body-rate kick is
`(r×J)/|r|² · (1 + f_ang·bounce_factor) · 0.5` with no inertia factor. In code `Collide` is now
the decoded response whole: placement (0.03 human / exact stop AI), the human-only normal impulse,
and the decoded angular kick via the shared `BounceImpulse`; the fitted graze trio
(`GrazeFriction`/`GrazeKick`/`GrazePushOut`) is retired, `BounceLeverScale` reclassified decoded,
and `ContactPushOut` 0.03 plus `BounceAngularHalf` 0.5 join the inventory as decoded rows (census
updated, config surface unchanged at 9 keys). The invented laws left for `C22`'s ablation are
named in the re-scoped `BL-271`: `CrashSpeed`, `GrazeStopSpeed`, the un-embed death and the 0.3 s
`DamageCooldown` (standing in for the unported every-other-frame sweep parity). Tests:
`CollideResponseTests` pins both placements, the tangential exactness a friction term of any size
fails, the decoded kick's shape and sign, the multi-tick scrape's exact per-tick bleed
(`speed' = speed·√(cos²θ + (bf·sinθ)²)`, 24 % over ten ticks at 20°) and its no-re-steer control
(METHOD-9); `AircraftContactResolverTests` adds the armour-before-health graze through a real
ledger with the armour-exhausted control; `BounceRestitutionTests`' partition pins move to the
corrected shares (12.8/16.7). The eleven-airframe dump cannot see this change (no collision
scenario, `Step` untouched); the instruments are those unit pins plus the `graze-bounce` engine
suite, whose measured `e` values shift a point or two with the partition correction. The
`c1-crash`/`c1-debris-rest` goldens may move if their scripted crashes include a survivable
contact before the fatal one; they are NOT re-pinned here. `BL-381` deleted from `backlog.md`;
`BL-121`/`BL-271`/`PT-53` cross-references updated to the remainder. `PT-53`'s at-the-controls
half rides `C23` unchanged.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 2082/2082 units,
94/94 engine suites with engine errors clean, 16/16 goldens hash-identical (the scripted crash
shots contain no survivable contact, so the placement change never fires in them), exit 0.

## C22 ☑ Remove or justify the invented graze/stop laws (`BL-271`)

**Goal.** Eliminate invented kick, friction, quadratic damage, stop-speed death and embed-count death unless explicitly retained as product exceptions.

**Evidence (confidence: direction-sound).** The original's mild graze loses about 5% speed with no visible kick.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-271` and code.> Ablate each law after C21 and decode any remaining original response.

**Model recommendation.** **high** — ablation and feel judgement after the mechanism lands.

**Verify.** Synthetic contacts separate each law; at-controls belly-slide and corner cases cannot collect endless zero-damage kisses.

**⚠ Traps.** The three graze constants were co-tuned against pre-restitution behavior; never tune one alone.
That warning is historical: `C21` retired the trio against the decoded response, and nothing it named
is still in the code.

**Landed.** Three of the four laws are gone and the fourth is a recorded product exception. The
decode that settles them is the destruction call at `0x48d7cc`: its only inputs are `local_11` and
`health > 0` (`0x48d78b`), and the whole contact path reads no speed, no vertical speed, no slide
and no overlap, so **no counterpart exists for a speed threshold, a stop rule or an embed rule**.
`CrashSpeed` 25 is removed as already inert: it gated a log line inside the no-damage-data arm,
which crashes at any speed, and that arm is unreachable on shipped content, which is now a
measurement rather than an assumption (`NoStockAirframeFliesWithoutADamageLedger`: eleven player
loads author four zones each, eleven AI loads an armour/health pair). `GrazeStopSpeed` 12 is removed
with the regression it guarded proven unreachable on the decoded response: a contact that closes at
all costs at least the authored 50 floor, the pair re-spends for as long as the scrape closes, and
the pool is bounded, so a slide exhausts its ledger and dies by the decoded health rule (five
contacts on the suite's one-zone plane) while keeping its tangential speed, since the impulse edits
the normal component alone. The METHOD-9 control beside it spends nothing and slides for 200
contacts without a fate, which is the "endless zero-damage kisses" report the rule was invented for.
`DamageCooldown` 0.3 s is replaced by the decoded gating rather than kept: the original spends the
pair on every frame its sweep resolves, gated only on a positive severity (`0x48ed79` guarding
`0x48ed8b`), so the cadence is one pair per two frames and the wall-clock stand-in was making a
scrape about nine times cheaper than the decode allows. The sweep parity itself is still not ported
(`C21`'s every-step sweep stands), so its one behavioural consequence rides
`ContactConditions.OnSweepParity`, flipped once per sim step by `FlightController`. The un-embed
loop's destruction after three failed pushes is KEPT, as a named product exception with its reason:
the original places one sphere exactly at its contact point and cannot leave an airframe inside
geometry, while a swept multi-box airframe can, and without a terminal case that aircraft has no
rule to end it. It is bound by `AnEmbeddedPlaneIsPushedOutThreeTimesThenExplodes`. `ContactResponse`
loses its speed field with the ground stop, since nothing else read it. Inventory and census updated
together, and both now cover the contact rules: `CollisionDamage` and `AircraftContactResolver` join
`FlightConstantInventoryTests`' reflected types with five new rows (the three decoded grace/cut
constants, `EmbedPushOut` 0.3 and `EmbedTries` 3 as exceptions), so a fitted contact number cannot
come back where the plant's census cannot see it. Config surface unchanged at 9 keys. Goldens cannot
move: `c1-crash` is `--crash=5`, whose aircraft is already crashed and never runs the resolver, and
`c1-debris-rest` is a freecam object shot with no aircraft contact at all. `BL-271` deleted from
`backlog.md`; `BL-121` re-tagged to `C23` alone and `PT-53`'s TUNE note rewritten.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 2084/2084 units,
94/94 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

## C23 ☐ Validate building-corner collision feel (`BL-120`)

**Goal.** Confirm the settled collision plant feels like the original at building corners.

**Evidence (confidence: lead-only).** The backlog records an owed at-controls comparison, not a mechanism.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-120` and `playtest.md`.> Run matched original/CSVM corner approaches after C21-C22. The same sitting covers `BL-121`'s
owed graze-feel check (kick, friction, stop behavior as settled by C21-C22), so that item needs no
separate flight; its breakup-scatter remainder stays in the backlog.

**Model recommendation.** **medium** — bounded playtest and evidence recording.

**Verify.** Record speed, attitude, damage and outcome across mild, hard and fatal corner strikes.

**⚠ Traps.** Feel cannot override a decoded impulse; it can expose missing lifecycle behavior.

**Defect found at the controls.** After a first shallow graze, steeper contacts with the same
building cost nothing at all and only a head-on crashed the airframe. The session log shows the
mechanism: three single-step contacts (graze reactions at 94.3 s, 111.0 s and 115.5 s, each taking
30 to 45 m/s off the airspeed) with no spend line between them, while the contacts that did spend
each charged the 50 floor. The root cause is a port error in how `C22` carried the sweep parity:
the sweep ran every step and the parity gated the SPEND, so a contact resolved on a non-spending
step still got the placement and the impulse (0.03 m off the surface, the normal velocity removed)
and bounced away for free, and a scrape whose re-contacts landed on those steps never spent once.
The original gates the SWEEP on the parity and spends on every contact it resolves, with the
skipped frame's motion carried into the next sweep. The fix is that decode: `SweepCadence` puts the
every-other-step cadence on `FlightController`'s sweep with the carried origin, and
`AircraftContactResolver` spends on every contact it is handed (`ContactConditions.OnSweepParity`
is gone). A second finding from the same log: two mid-flight respawns (R, or pad Y) at 59 s and
60.5 s swept from the pre-respawn pose to the spawn point 2.6 km away, struck the building in
between, and the placement hauled the repaired airframe back to the wall with a fresh 50-point
dent, which is why the ledger read full again between grazes; the sweep origin is now read after
the input, so a respawn sweeps from the spawn. `SweepCadenceTests` pins the cadence, the
shallow-then-steeper sequence (hull 80, 60, 40), a sustained scrape dying in three spends, and the
spend-gated control that locks onto the free steps. The dossier's "What the parity is ported as"
is corrected as a port error, not a decode error. Owed the re-fly: corner and scrape contacts
must now cost the 50 floor on every resolved contact, one per two sim steps in a sustained scrape.

# Wave D — Settle designed but unproven features

## D31 ❌ Settle one-sided engine torque (`BL-309`)

**Goal.** Prove whether the shipped game assists turns in one engine-torque direction and port it only if present.

**Evidence (confidence: lead-only).** The GDD specifies assistance; current code has none and existing footage is non-discriminating.

**Approach.** Trace `FUN_0048c470` for a throttle/direction term; obtain matched-direction captures only if the trace remains ambiguous.

**Model recommendation.** **max** — absence proof across a decoded accumulator is demanding.

**Verify.** Matched-speed rolls and rudder turns both directions; a port must assist one side and never penalize the other.

**⚠ Traps.** Single-direction video rates may already contain the effect and cannot size it.

**Landed.** DISPROVEN: the shipped executable carries no engine torque, and `BL-309` closes as
unshipped design intent per Decision 4. The re-verify found the item still open (`git log
--grep=BL-309` returns only its minting and a renumber). The proof is a complete enumeration of the
writes to the angular accumulator, the third argument of `FUN_0048c470` (`[EBP+0x10]`, which
`FUN_0048e580` adds to `obj+0x160` at `0x48e6ef`), now the dossier's "Engine torque" table: the
zero-vector initialisation (`0x48c4a6`), the three stick torques (`0x48cae2`/`0x48cb98`/`0x48cc4e`,
each odd in its stick and along a body axis), the two bank-coupling rows (`0x48ccb3` odd in bank on
the yaw axis, `0x48cd36` even in bank on the pitch axis), the player-only weathervane (`0x48ce3d`,
axis `nose × v̂`), the never-authored `level_off_rate` arm (`0x48cf76`, axis `m[1] × up`), the
ground blow (`FUN_0048c220` at `0x48cf95`, axis from the struck surface, on both the player and the
AI law) and the player-only stall nose-drop (`0x48d158`, axis `nose × down`). Every term is a
product of state-derived vectors; none carries a constant vector, a constant-signed scalar on a
body axis, or the throttle. The far-field arm (`0x48c593`–`0x48c603`) writes only the linear
output, so the AI plant inherits the same set minus what `B12` records it skipping. Downstream is
as blind: the integrator damps by `ang_momentum_damp` and scales by the reciprocal inertias, and
the only other writers of `obj+0x160` in the program are the decoded collision deposit
(`0x48e4d3`), two reset loops (`FUN_00491c60`, `FUN_00491d90`) and the dead debug integrator
family. The throttle `[obj+0x128]` is read once in `FUN_0048c470`, at `0x48c5a0` for the far-field
cruise target, which agrees with `A3`'s census from the force side. No engine record, propeller
direction or handedness constant enters any row, so nothing remains to classify. No code changed;
the remake's rotational plant already has the decoded symmetry, and
`CSVM.Tests/EngineTorqueAbsenceTests.cs` pins it so the GDD term is never re-chased: matched
full-stick rolls and rudder turns in both directions at three throttles on the Bloodhawk's real
inertias and bank coupling, a centred-stick throttle sweep that stays still, an idle against
full-throttle comparison with speed and flight path held (the free-path form differs by 2 mrad/s
in pitch and yaw through the weathervane reading a thrust-moved `v̂`, a translational coupling and
not a torque), and a `METHOD-9` control showing a 0.28 % one-sided assist
(a hundredth of the footage pair's disputed 28 %) fails the roll pin. No capture was requested:
the trace is unambiguous, and per `DET-11`/`DET-12` a directional rate read off footage could
not outrank it anyway. Dossier and `docs/architecture.md`'s `FlightModel.cs` entry updated;
`BL-309` deleted from `backlog.md`.

**Verified.** Full `RunTests.ps1` battery on the merged lane tree: build clean, 2093/2093 units,
94/94 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

## D32 ❌ Settle roll-to-pitch coupling (`BL-310`)

**Goal.** Determine whether aileron input adds the GDD's small nose-down term in the retail build.

**Evidence (confidence: lead-only).** Design intent names it; no current mechanism is known.

**Approach.** Search the live torque accumulator for pitch written from roll input; close as won't-do if absent.

**Model recommendation.** **high** — bounded binary trace with a valid disproof outcome.

**Verify.** If present, a one-step unit isolates cross-axis torque; if absent, document the complete writer search.

**⚠ Traps.** Altitude or ADI movement during a roll cannot separate coupling from geometry.

**Landed.** The re-verify found `BL-310` open, and the complete search DISPROVES the GDD's term.
The search ran from the command outward rather than from the accumulator inward, which is the
independent half of `D31`'s writer table: every program-wide read of the roll command, in both of
its slots (the shaped channel `[obj+0x100]` and the clamped stick `[obj+0x114]`), is enumerated and
classified, and no read reaches the pitch axis. The live path spends the roll stick exactly twice,
as the roll torque along the `m[2]` row at `0x48ca7a`–`0x48cae2` and as a compare against `0.0`
gating the never-authored `level_off_rate` auto-level at `0x48ce53`. **The command builders are
axis-wise on both paths**, which is the first place a coupling could have lived: each stick is
`FUN_00460890` of its own channel in one run of three calls, the player's at
`0x487dab`/`0x487dc7`/`0x487de3` and the AI's in the identical shape at
`0x41c02f`/`0x41c04b`/`0x41c067` and `0x420fd8`/`0x420ff4`/`0x421010`, so the pitch stick `+0x11c`
takes the pitch channel `+0x108` alone whoever is flying. **`B13`'s elevator mixing is measured to
be animation, not assumed to be**: the six targets `FUN_0048e580` builds at `0x48eaf0`–`0x48ec8b`
land in `+0x62c` through `+0x640`, and the only reads of those six slots in the whole program are
the three node appliers `FUN_004b2f00`/`FUN_004b2f70`/`FUN_004b2fe0`, with the constructors that
zero them as the only other writers. The remaining consumers are the other motion models
(`FUN_00489ea0` switches on `[obj+0x67c]`, copied from the vehicle record at `0x475abf`), and in
all of them the roll command becomes a heading rate while the pitch angle is either untouched or
taken from the velocity vector. Two decode hazards are recorded with the finding: `[+0x114]` is
`ang_momentum_damp` on the plane RECORD and part of a 4×4 matrix in `FUN_004d3010`, so an offset
sweep that does not separate the structures reports coupling that is not there; and the bank
coupling's pitch term at `0x48cd36` takes `m[0].y`/`m[1].y`, so it produces pitch during a roll
with no stick term at all, which is why filmed altitude or ADI movement cannot settle this.
**No code changed.** `RollToPitchCouplingTests` flies a held roll stick at three magnitudes
wings-level and on the flight path, so the bank coupling and the weathervane read one unchanging
state, and pins `BodyRates.X` at exactly zero and identical to a centred-stick run while the roll
rate stays live; the `METHOD-9` control injects a nose-over at a fiftieth of `pitch_torque` and
reads it back off the accumulator, and a fourth test holds the roll-driven elevator deflection
beside the quiet pitch axis so the animation and the plant cannot be confused. The consumer list
with addresses is in `docs/org/flightModel.md`, "Roll to pitch"; `BL-310` deleted from
`backlog.md`. Closed per this plan's Decision 4 as unshipped design intent.

**Verified.** Full `RunTests.ps1` battery on the merged lane tree (main merged in the same
step): build clean, 2132/2132 units, 94/94 engine suites with engine errors clean, 16/16 goldens
hash-identical, exit 0.

## D33 ❌ Settle ambient turbulence (`BL-311`)

**Goal.** Reproduce subtle visual-only jostling if the retail game ships it; otherwise record it as unshipped intent.

**Evidence (confidence: lead-only).** The GDD names zero-performance turbulence; `PlaneShake` has a compatible visual-only seam.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-311`, shake data and code.> Search authored shake sources and runtime calls before adding any oscillator.

**Model recommendation.** **high** — data census plus binary absence/presence decision.

**Verify.** Isolate camera/plane motion at steady flight; speed and heading remain invariant.

**⚠ Traps.** If no data/mechanism exists, magnitude and cadence are TUNE and not parity facts.

**Landed.** **Nothing ships, and both halves of the search are complete, so this closes as
unshipped design intent under Decision 4 with no code added.** The re-verify found the item
untouched since it was minted, and settling it decoded the whole shake machinery rather than one
call. The camera object `DAT_0064ef78` carries **seven** oscillator blocks of eleven dwords each
from `camera+0x18`, the constructor `FUN_0042bab0` fills all seven with the same defaults
(frequency 2.0, damp 4.5, sawtooth 0, zero accumulators, zero magnitude), and the block index is
the parser `FUN_0042bc10`'s own source order, which the magnitude-term offsets confirm by landing
every authored magnitude at its block's `[9]`. **Block 5 is `turbulence`**, not the "impact block"
the dossier called it: the parser looks up the key `turbulence` at `0x00621638` (referenced once in
the whole binary, from `0x42bd93`), hands `camera+0xf4` to the shared law reader `FUN_0042bba0`,
and stops. That reader takes `frequency`, `damp` and `sawtooth` only, so alone among the seven the
turbulence block has **no magnitude field to parse at all**, and `camera+0x118`, the slot a
magnitude would occupy, is written by no instruction in the executable. The caller sweep is
exhaustive and every arm is non-ambient: `FUN_0042c070` is a one-line forwarder to `FUN_0042be10`,
which has no other caller, and `FUN_0042c070` has exactly five xrefs, all per-event except one
gated per-frame source. They are `FUN_004b6820` at `0x4b6e38` (block 0, one gun round fired),
`FUN_004b9bc0` at `0x4b9d26` (blocks 1/2/3 selected in `EBX`, one gun round, rocket or nearby
detonation taken), `FUN_0048c470` at `0x48d1bc` (block 4, per frame but only above the authored
`min_speed` gate, which is rated max speed), `FUN_0048d2c0` at `0x48d409` (block 5, one collision
contact, so `C21`'s contact shake is what actually occupies the turbulence slot) and
`FUN_004b2131` at `0x4b21ce` (block 6, nitro engaged, player only, magnitude the authored
`nitro.magnitude` at `camera+0x144`). The only other writer of any accumulator is the integrator
`FUN_0042bec0`, whose sole caller is the render consumer `FUN_0042c0e0`, and no function outside
the shake module stores a float at the block offsets. Steady flight kicks nothing.
The data census is the second, independent negative: `extracted/zrdr/shakes.zrd.json` is the only
shake-oscillator file in the tree with no per-campaign, per-mission or per-airframe override, and
it authors six sources with no `turbulence` among them, so block 5 also keeps its default law. A
sweep of all 61010 extracted files finds no field or token containing `turbulen`, `jostl`,
`buffet`, `gust`, `wobble`, `vibrat`, `jitter` or `thermal`, and no shake source with an idle,
cruise or always-on activation. The near neighbours are each something else: `player.zrd.json`'s
`rattle` is the `snd_planeshake` volume and pitch envelope and carries no motion, the mission
`weather.zrd.json` `WIND` block is a particle field identical across all 53 mission copies and
reached only by `WIND_FACTOR` on dust, smoke, steam and spray emitters, `damage_shakes.zrd`'s
`ON_CALL` defs are finite damage reactions, and every `ambient` hit in the tree is lighting. The
rule that bites is `docs/verification.md` `SRC-3` (design documents give intent, retail evidence
decides shipped details); `SRC-7`'s dual applies to the parser as well, since a key the executable
reads is not a feature until something drives what it fills. The decode, the seven-block table
with every kicker, and the disproof are in `docs/org/shakes.md`; `docs/formats/shakes.md` gains
the pointer, and `BL-311` is deleted from `backlog.md`. Two corrections land with the block map:
the block layout is `[3]/[4]/[5]` velocity and `[6]/[7]/[8]` position, the reverse of what the
dossier said, and the `explosion` source's magnitude term never fills, because the parser reads
the key `max_magnitude` (`0x6215fc`) into `+0xc0` while the data authors `magnitude_factor` there.
That last one and the `camera+0x1c` default-versus-authored frequency reading belong to `BL-266`
and are recorded, not acted on, here.

**Verified.** No source file changed, so the lane's source tree is the one C22's full
`RunTests.ps1` battery passed (build clean, 2084/2084 units, 94/94 engine suites, 16/16 goldens
hash-identical, exit 0).

## D34 ☑ Reproduce nitro boost (`BL-089`)

**Goal.** Implement the original boost, charge, decay, cap, HUD, animation and audio lifecycle.

**Evidence (confidence: direction-sound).** Commands, gauges, defs, sounds and nitro engine variants ship; numeric dynamics do not exist in extracted data.

**Approach.** Trace executable-resident dynamics, then wire existing authored assets and engine eligibility.

**Model recommendation.** **max** — cross-cutting reverse engineering and gameplay state.

**Verify.** Compare activation, acceleration, depletion, recharge, cap and stop behavior on nitro and plain engines.

**⚠ Traps.** Do not hand-balance before decoding; engine choice grants nitro, not every aircraft unconditionally.

**Landed.** The re-verify found the item untouched (`git log --grep=BL-089` returns nothing, and
no code read `ShakeDefs.Nitro`, `EngineDrive.Boosting`, `Maneuver.Nitro` or `PropParts.Kind.Nitro`).
The executable-resident dynamics are decoded whole, no balance pass was needed, and every number
has an address in the dossier's new "Nitro" section. The tank is four constructor constants
(`FUN_004aff80`, `0x4b02c4`–`0x4b02e9`): cap 30, spawn charge 30, burn 4/s, refill 1/s, and the
refill is unconditional (`0x49f832`–`0x49f83e`), so a burn nets 3/s and lasts **9.5 s** from full
to the 5 % cutoff (`0x6034d8`), after which the refill to the **99 %** engage line (`0x6080a8`)
takes 28.2 s. Activation is a one-shot: the human handler (`0x487e91`–`0x487efc`, command `0x12`)
engages on a held key only from 99 %, re-asserts the flag until the cutoff, and offers no way to
stop a burn. The state machine `FUN_004b2110` refuses an engine-out aircraft (`[obj+0x2dc] & 2`),
plays the `nitro_boost` def and, on release after a 1.0 s minimum (`def+0x188`), stops it and plays
`nitro_decay` with a completion callback, refuses a re-engage while either def is alive, keys the
`snd_nitro` loop (`def+0x184`) while the boost def lives, and kicks shake block 6 and the
`NitroStart` force-feedback effect for the player alone. Eligibility is `[obj+0x946]`: the hangar's
engine ids 3–5 (`0x47d4f0`), the roster block's `nitro` slot for an AI (`0x475c9a`, three shipped
blocks author it). The AI engages once at the start of a nitro-flagged maneuver (`0x420928`, the
library's one flagged entry is `nitro_evade`, unselectable without the injector), is released every
frame outside one (`0x4899d1`) and has no 99 % line. The gauge (`FUN_004568c0`) chases −216° on the
boost needle at 3/s and `(1 − charge/cap) · 216°` on the charge needle at 1.5/s, and is shown only
with the injector; `MSG_HUD_NITRO` is a debug-flag readout. The boost's own force couplings were
already decoded (lever replaced by 1.8, drag × 0.8) and the far-field cruise target reads the
lever, not the flag, so a distant AI's boost changes nothing there, which is the decode.
In code: `Flight/NitroSystem.cs` is the state machine, engine-free, with every constant censused
(`NitroSystem` joins the inventory's reflected types, eight new rows including
`FlightModel.BoostLever`/`BoostDragFactor`, config surface unchanged at 9 keys);
`FlightInput.Boost` reaches the two couplings in `Step`; `FlightController.AdvanceNitro` runs the
human arm (`N` / pad X, `docs/controls.md`) or the AI arm off `AiModeMachine.Executor`'s maneuver
flag, then the tank, then the edges: `PlaneShake.NitroEngaged` (block 6, human pilots only, plan
Decision 3), the `nitro_boost`/`nitro_decay` defs through the crash rig runtime
(`EffectCatalogue.NitroAnims` stages them), the `snd_nitro` loop in `FlightAudio`, and
`EngineDrive.Boosting` for the already-decoded 1.17/1.25 engine-note pins. `PlaneBuilder` now
builds `nitropropN` hidden for the def to reveal. The injector is `CustomPlaneBuild.HasNitrous`
for a human and `AiSpawn.Nitro` (read by `AiSkills.RosterNitro`, slot 34) for an AI; `GaugeCluster`
draws the `nitrogauge` subtree with both needles on the decoded exponential. Tests:
`NitroSystemTests` (activation, the 3/s net burn and 9.5 s, the 28.2 s re-arm with the 98 %/99 %
control, the no-stop rule, the injector and engine-out gates, the AI arm's missing line, the decay
lockout and the 1 s minimum, plus the lever-replacement and drag-ratio pins on the Bloodhawk and
the plain-engine bit-identity control), `NitroGaugeNeedleTests`, `PlaneShakeTests`' nitro kick and
`AiSkillsTests`' slot reader. The eleven-airframe dump is SHA256-identical to `B11`'s
`7BF4C7AE…` (nitro is off in every scenario), and no golden can move: no pinned shot fits a
nitrous engine, so the dial never draws and the flag never sets. Deferred, with reasons: the
`medium_aishake` def the original also plays on an AI engage (`FUN_00473430(1)`) and the AI's
positional `snd_nitro` (a 0.1 s blip plus one second after the maneuver) are not wired; the
session's mission spawner does not yet read roster blocks, so `AiSpawn.Nitro` has no live producer
until it does; the decay lockout runs on the def's authored 1.0 s rather than a runtime callback,
which the runtime does not offer. `BL-089` deleted from `backlog.md`.

**Verified.** Full `RunTests.ps1` battery on the lane tree: build clean, 2113/2113 units,
94/94 engine suites with engine errors clean, 16/16 goldens hash-identical, exit 0.

# Wave E — Prove parity

## E41 ☐ Recheck the autogyro low-speed nose-down report (`BL-388`)

**Goal.** Decide whether the autogyro's softer low-speed nose-down is a real residual after the settled plant.

**Evidence (confidence: lead-only).** One uncertain playtest report names softness; the authored low-speed authority ramp is a competing explanation.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-388` and `playtest.md`.> After A2/B11, replay matched autogyro stalls and separate AI recovery commands from plant authority.

**Model recommendation.** **high** — coupled control-law/plant diagnosis from weak evidence.

**Verify.** Side-by-side original and CSVM entries at matched speed and attitude, with recovery-arm state logged.

**⚠ Traps.** The recovery arm keys on the backward axis: nose-up-and-slow, not nose-down.

## E42 ☐ Publish the eleven-airframe parity ledger

**Goal.** End with one reproducible table classifying every mechanism and envelope row as matched, intentional exception, recorded executable-vs-video conflict, or unsupported.

**Evidence (confidence: traced).** Today only the Bloodhawk has video targets; mechanism tests cover decoded laws but not every combined airframe envelope.

**Approach.** Extend `--dump-flight` to all stock aircraft, add reachability margins and branch coverage, and update `docs/org/flightModel.md` plus architecture/CLI docs.

**Model recommendation.** **high** — broad reconciliation and evidence-quality judgement.

**Verify.** Run `RunTests.ps1`, all eleven deterministic dumps, eight chapter regressions and every owed parity playtest; perturb each ledger class's instrument once.

**⚠ Traps.** Passing mechanism tests proves the port is internally consistent, not that every original-game observation agrees. Preserve conflicts verbatim.

## E43 ☐ Enable the decoded AOA window and settle α against the original

**Goal.** Ship the opposing-command limiter's AOA window at its decoded strength (user decision,
plan Decision 1 applied strictly), then determine whether the α CSVM's plant holds in a full pull is
the α the original holds, so the window's effect on the pitch rate is judged against the right
plant rather than tuned away.

**Evidence (confidence: traced for the window, lead-only for α).** `B11` decoded the window
(`(cos α − cos maxAOA)/(1 − cos maxAOA)`, `0x48c9f4`–`0x48ca18`) and measured it non-inert: at
this plant's 32–38° full-pull α it roughly halves the elevator, moving the Bloodhawk pitch rate
32.43 → 22.51 °/s against the filmed 33.00, the sustained turn 34.5 → 25.8 °/s toward its 18.95,
and the zoom climb 821 → 1280 ft away from its 936. The original reaches 33 °/s with this window
live, which is evidence about α, not about the window: `A2`'s lag reader at 0.75/s structurally
implies tens of degrees of nose–path separation at 33 °/s, and `B11`'s limiter reads the previous
tick's lift.

**Approach.** Two halves in order. (1) Set `AoaLimiterFactor` to 1 (inventory row and census
updated, the seam kept), A/B the eleven-airframe dump and record every moved row in the dossier
as a decoded change; the six flight goldens re-pin only after the user has reviewed the montage.
(2) Decode what bounds α in the original in a full pull: the `liftAOAs` blend thresholds, the AOA
clamp in `FUN_0041abd0`, the 8 ft/s low-speed lift gate, and whether the limiter reads the same
tick's lift or the previous tick's; then replay the pull on both plants and attribute the residual
to a named term or record it as a conflict.

**Model recommendation.** **max** — the plant's highest-coupling question, with a FAIL row at
stake and every earlier disproof as a constraint.

**Verify.** With the window live, `LatentControlAuthorityTests` still passes; the dump's moved
rows are each attributed; a pull-to-limit scenario on the Bloodhawk logs α, the window factor and
the pitch rate per step, and either matches 33 °/s within the footage rule's error or records the
residual as a conflict with the term it belongs to.

**⚠ Traps.** Do not close the gap by weakening the window, the lag rate or `maxAOA`; each is
decoded. A pitch-rate match bought by a fitted factor fails Decision 1. Footage rates carry
`DET-11`/`DET-12` error; state the band before declaring a match or a conflict.
