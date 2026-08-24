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
  collision ranges. Stock values leave the pitch fade and AOA/G command limiters inert.
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

11. ☐ B11 Port the two latent control-authority terms (`BL-437`)
12. ☐ B12 Port the distant-AI simplified plant (`BL-425`)
13. ☐ B13 Reproduce control-surface motion (`BL-393`)

### Wave C — Replace invented collision behavior

21. ☐ C21 Complete collision damage and sustained-contact decoding (`BL-381`)
22. ☐ C22 Remove or justify the invented graze/stop laws (`BL-271`)
23. ☐ C23 Validate building-corner collision feel (`BL-120`)

### Wave D — Settle designed but unproven features

31. ☐ D31 Settle one-sided engine torque (`BL-309`)
32. ☐ D32 Settle roll-to-pitch coupling (`BL-310`)
33. ☐ D33 Settle ambient turbulence (`BL-311`)
34. ☐ D34 Reproduce nitro boost (`BL-089`)

### Wave E — Prove parity

41. ☐ E41 Recheck the autogyro low-speed nose-down report (`BL-388`)
42. ☐ E42 Publish the eleven-airframe parity ledger

## Dependency and parallelism notes

A1 precedes every edit to `FlightModel.cs`. A2 blocks A3 and the final envelope verdict. A4 may
run beside A2. B11-B13 are independent after A2, but B11 and B12 contend on `FlightModel.cs`.
C21 → C22 → C23 is a chain. D31 and D32 share the torque accumulator and run serially; D33 and
D34 are independent. E41 follows A2/B11, and E42 follows every other item.

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

## B11 ☐ Port the two latent control-authority terms (`BL-437`)

**Goal.** Implement pitch-only high-speed fade and asymmetric AOA/G command limiting.

**Evidence (confidence: traced).** Both are decoded but inert on all stock airframes.

**Approach.** Split pitch's second stage from the shared low-speed ramp; apply the smaller limiter
only to pitch/yaw commands that increase nose/path separation.

**Model recommendation.** **high** — subtle signs and inert stock data.

**Verify.** Synthetic airframes make each term bind; stock envelope remains byte-identical.

**⚠ Traps.** Any stock-row change means the implementation or reachability reading is wrong.

## B12 ☐ Port the distant-AI simplified plant (`BL-425`)

**Goal.** Beyond 1 km, AI uses the original speed-hold plant and returns to near-field aerodynamics inside it.

**Evidence (confidence: traced).** Original far AI skips lift/drag, authority curves and bank coupling.

**Approach.** Plumb player position into the plant and select the branch at the decoded boundary.

**Model recommendation.** **high** — cross-module state plus a previously reverted prototype.

**Verify.** Spectated AI outside 1 km holds nose speed and stops bank-turning; inside is invariant.

**⚠ Traps.** This did not explain `BL-387`; do not revive that hypothesis.

## B13 ☐ Reproduce control-surface motion (`BL-393`)

**Goal.** Match six-list channel mixing, angles, smoothing and the player-only animation guard.

**Evidence (confidence: traced).** Angles and 2/s exponential smoothing are decoded; node-list population is not.

**Approach.** Decode list population, map nodes, then replace ±20°/linear 3/s behavior.

**Model recommendation.** **high** — mapping error would animate the wrong surfaces plausibly.

**Verify.** Script each axis and mixed pair; prove AI surfaces remain frozen in the original-equivalent mode.

**⚠ Traps.** Do not infer physical surface names from the six slots before decoding population.

# Wave C — Replace invented collision behavior

## C21 ☐ Complete collision damage and sustained-contact decoding (`BL-381`)

**Goal.** Consume authored armour/health ranges and reproduce vertical-speed kill and multi-tick scrapes.

**Evidence (confidence: direction-sound).** `CAP-14` shows wall-tangential sink removal and a 40%
speed loss over 0.47 s; the decoded normal impulse cannot produce either.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-381` and current collision code.>
Trace the remaining contact path, then route damage through `PlaneDamage` and port only decoded terms.

**Model recommendation.** **max** — mixed decode, lifecycle and player-facing physics.

**Verify.** Graze spends armour before health; a scripted oblique wall scrape bleeds speed across ticks; `PT-53`.

**⚠ Traps.** Do not raise `bounce_factor` or preserve old graze tuning by default.

## C22 ☐ Remove or justify the invented graze/stop laws (`BL-271`)

**Goal.** Eliminate invented kick, friction, quadratic damage, stop-speed death and embed-count death unless explicitly retained as product exceptions.

**Evidence (confidence: direction-sound).** The original's mild graze loses about 5% speed with no visible kick.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-271` and code.> Ablate each law after C21 and decode any remaining original response.

**Model recommendation.** **high** — ablation and feel judgement after the mechanism lands.

**Verify.** Synthetic contacts separate each law; at-controls belly-slide and corner cases cannot collect endless zero-damage kisses.

**⚠ Traps.** The three graze constants were co-tuned against pre-restitution behavior; never tune one alone.

## C23 ☐ Validate building-corner collision feel (`BL-120`)

**Goal.** Confirm the settled collision plant feels like the original at building corners.

**Evidence (confidence: lead-only).** The backlog records an owed at-controls comparison, not a mechanism.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-120` and `playtest.md`.> Run matched original/CSVM corner approaches after C21-C22. The same sitting covers `BL-121`'s
owed graze-feel check (kick, friction, stop behavior as settled by C21-C22), so that item needs no
separate flight; its breakup-scatter remainder stays in the backlog.

**Model recommendation.** **medium** — bounded playtest and evidence recording.

**Verify.** Record speed, attitude, damage and outcome across mild, hard and fatal corner strikes.

**⚠ Traps.** Feel cannot override a decoded impulse; it can expose missing lifecycle behavior.

# Wave D — Settle designed but unproven features

## D31 ☐ Settle one-sided engine torque (`BL-309`)

**Goal.** Prove whether the shipped game assists turns in one engine-torque direction and port it only if present.

**Evidence (confidence: lead-only).** The GDD specifies assistance; current code has none and existing footage is non-discriminating.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-309` and code.> Trace `FUN_0048c470` for a throttle/direction term; obtain matched-direction captures only if the trace remains ambiguous.

**Model recommendation.** **max** — absence proof across a decoded accumulator is demanding.

**Verify.** Matched-speed rolls and rudder turns both directions; a port must assist one side and never penalize the other.

**⚠ Traps.** Single-direction video rates may already contain the effect and cannot size it.

## D32 ☐ Settle roll-to-pitch coupling (`BL-310`)

**Goal.** Determine whether aileron input adds the GDD's small nose-down term in the retail build.

**Evidence (confidence: lead-only).** Design intent names it; no current mechanism is known.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-310` and code.> Search the live torque accumulator for pitch written from roll input; close as won't-do if absent.

**Model recommendation.** **high** — bounded binary trace with a valid disproof outcome.

**Verify.** If present, a one-step unit isolates cross-axis torque; if absent, document the complete writer search.

**⚠ Traps.** Altitude or ADI movement during a roll cannot separate coupling from geometry.

## D33 ☐ Settle ambient turbulence (`BL-311`)

**Goal.** Reproduce subtle visual-only jostling if the retail game ships it; otherwise record it as unshipped intent.

**Evidence (confidence: lead-only).** The GDD names zero-performance turbulence; `PlaneShake` has a compatible visual-only seam.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-311`, shake data and code.> Search authored shake sources and runtime calls before adding any oscillator.

**Model recommendation.** **high** — data census plus binary absence/presence decision.

**Verify.** Isolate camera/plane motion at steady flight; speed and heading remain invariant.

**⚠ Traps.** If no data/mechanism exists, magnitude and cadence are TUNE and not parity facts.

## D34 ☐ Reproduce nitro boost (`BL-089`)

**Goal.** Implement the original boost, charge, decay, cap, HUD, animation and audio lifecycle.

**Evidence (confidence: direction-sound).** Commands, gauges, defs, sounds and nitro engine variants ship; numeric dynamics do not exist in extracted data.

**Approach.** <TODO: re-verify still-open against `git log --grep=BL-089` and current input/HUD code.> Trace executable-resident dynamics, then wire existing authored assets and engine eligibility.

**Model recommendation.** **max** — cross-cutting reverse engineering and gameplay state.

**Verify.** Compare activation, acceleration, depletion, recharge, cap and stop behavior on nitro and plain engines.

**⚠ Traps.** Do not hand-balance before decoding; engine choice grants nitro, not every aircraft unconditionally.

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
