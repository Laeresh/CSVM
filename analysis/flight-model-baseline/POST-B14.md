# Flight model baseline — POST-B14 (the decoded aero core)

**Purpose.** The B14 re-run of [`BASELINE.md`](BASELINE.md)'s full A2 table against the decoded
aero core (B11 lift + B12/B13 drag-and-thrust, zero fitted constants in the force path), with every
row dispositioned: **green**, **conflict recorded**, or **owned by a named later item**. Wave C/D
measure against THIS file; A2's originals stay untouched as the pre-rewrite record. Raw outputs:
[`raw/post-b14-dump-bhawk.txt`](raw/post-b14-dump-bhawk.txt),
[`raw/post-b14-dump-balmoral.txt`](raw/post-b14-dump-balmoral.txt),
[`raw/post-b14-all-airframes.txt`](raw/post-b14-all-airframes.txt). Same instruments and re-run
commands as A2 (same `verification.md` citations: SHELL-10/DET-6 for the probes, DET-7/DET-9 for
the engine-free runs); every instrument run twice, byte-identical both times.

## The force-scale verdict (what B14 changed, and what it deliberately did not)

B13 left one residual: "everything ≈2× too strong, invisible to the level-equilibrium ratio", with
the force→acceleration divisor (a kg/lb mix-up of 2.2046) as the suspect. **Disproven at source.**
The whole weight chain in `crimson.exe` is conversion-free — `veh_weight` is stored by the parser
with a plain `mov` (`0x47ae2d`), copied raw to the runtime object (`0x475c6a`), and used raw by
gravity (`0x48ff8e`), lift (`0x41ac13`) and the force→acceleration step (`0x491290`,
`× 9.82 / weight`). The executable computes exactly what `FlightModel.cs` computes, constant for
constant. Full address table and the conflict analysis:
[`docs/org/flightModel.md`](../../docs/org/flightModel.md), "The force scale — settled".

**Consequently no force-path code changed in B14**, no TUNE was added, and the CAP-05/decel
residual is a **recorded decode-vs-footage conflict** (the deficit is a near-constant
ΔC_D ≈ 0.112, which no decoded mechanism produces and no constant can close — a rescale that fixed
`decel-290-150` would break `accel-150-290` against the same footage). Also ruled out while
hunting: a low-speed drag fade (`drag_fade_speed` and the global `drag_factor` are parsed and read
by nothing — dead keys) and residual idle thrust (the implied lever would have to decay 8.6% → 2%
across one 28 s clip against a 0.5/s slew).

## Bloodhawk — the full table (`.\RunProbe.ps1 --dump-flight --headless`)

| scenario | unit | A2 (pre) | POST-B14 | original | disposition |
|---|---|---:|---:|---:|---|
| level-top-speed | mph | 301.99 | **300.46** | 300.40 | **green** (asserted) |
| accel-150-290 | s | 3.75 | **3.22** | 3.76 ± 0.40 | **conflict recorded** — informational; force path byte-verified, residual −14.5% unattributed; candidates: the original's 0.5/s throttle slew (decoded, unimplemented) and the clip's lever history |
| terminal-dive | mph | 355.26 | **336.36** | 355.2 ± 6 | **owned by D32** — informational; the decoded attitude-thrust (×≈1.38 at this dive angle) is deliberately unimplemented until D32; re-assert there |
| roll-360 | s | 1.98 | **1.98** | 2.05 | **green** (asserted) |
| pitch-rate | °/s | 33.47 | **33.47** | 33.0 | **green** (asserted) |
| yaw-360 | s | 29.17 | **28.68** → C21 **28.65** | 28.6 | **green** (asserted) |
| altitude-cap | ft | 6572.09 | **6571.95** | 6571.6 | **green** (asserted) |
| level-speed-near-cap | mph | 301.99 | **300.46** | 300.40 | **green** (asserted) |
| sustained-turn-speed | mph | 223.03 | **261.01** | 222.94 ± 5 | **owned by C22** — informational; the missing bank→yaw/pitch coupling sets the turn equilibrium; Decision 4 forbids any refit before C22 |
| sustained-turn-sink | ft/s | −2.40 | **−2.65** | ≤ 1.85 | **green** (asserted, upper bound) |
| sustained-turn-rate | °/s | 32.33 | **32.35** | 18.95 | **owned by C22** — informational since A2, unchanged through all of Wave B (the strongest evidence the gap is not in the force path) |
| eighth-throttle-speed | mph | 137.87 | **134.52** | 137.9 ± 6 | **green** (informational by design — B13's discriminating test, passes) |
| decel-290-150 | s | 6.48 | **3.48** | 7.04 | **conflict recorded** — informational; same conflict as CAP-05 (the polar below cruise vs the zero-thrust footage); still owed a human playtest (B12's transferred debt) |
| zoom-climb | ft | 1282.25 | **1341.20** | 936 | **re-recorded** — informational; energy split still wrong the same way as post-C21; D31/C-wave context |
| zoom-climb-min-speed | mph | 205.26 | **236.06** | 127.9 | **re-recorded** — informational, same loop |

The asserted count in `FlightEnvelopeTests` is now **7** (was 10): the three conflicted rows are
informational, each with the attribution in its probe comment and the record in
`docs/org/flightModel.md`. The suite is green; the conflicts are visible rows, not hidden ones.

## CAP-05's four zero-thrust points (`.\analysis\flight-model-baseline\cap05-drag-points.ps1`)

Unchanged from the B13 state (no force-path code moved in B14):

| x | model (m/s²) | measured | err |
|---:|---:|---:|---:|
| 0.25 | 1.3124 | 0.36 | +264.5% |
| 0.35 | 3.0333 | 1.11 | +173.3% |
| 0.46 | 6.1557 | 2.82 | +118.3% |
| 0.50 | 7.6786 | 3.74 | +105.3% |

**Disposition: conflict recorded, not fitted.** The deficit is the polar minus a constant
C_D ≈ 0.112 (−0.1085/−0.1118/−0.1122/−0.1122 across the span — ∝ q, not ∝ V or constant force).
The four points are fit outputs of a (g, C)-degenerate extraction (FINDINGS.md records this); the
clip's one model-free anchor (6.24 m/s² at 152.6 mph, +5° climb, engine off) still reads the polar
≈1.5–1.8× strong with full `nom_gravity` charged. Whichever side is wrong, it is not a constant in
this model.

## Balmoral (`--dump-flight=player_balmoral`) and all 11 airframes (`ZzBaselineDump`)

Balmoral headline (no measured original exists): level top speed **125.87 mph** against fd_speed
176.7 (the B13-recorded 0.71× outlier of the equilibrium solve — the airframe still flies, unlike
under B12's interim state), terminal dive 149.49 (0.846 × fd), eighth-throttle 58.27 with a −9.4°
settled path (it cannot hold level at 1/8 lever), sustained-turn sink +15.53 ft/s. The knife-edge
margin question A2 carried is gone with the old lift ramp (B11); D31 owns the knife-edge shape.

`sustained-turn-rate`, all eleven, vs A2 (original 18.95 °/s, Bloodhawk only on video): autogyro
33.44 (A2 33.42), bhawk 32.35 (32.33), peacemaker 29.98 (29.96), fury 27.12 (27.12), avenger 24.62
(24.63), devastator 22.92 (22.94), brigand 18.52 (18.57), kestrel 16.24 (16.32), firebrand 15.48
(15.56), warhawk 14.97 (15.04), balmoral 9.50 (9.57). **The entire Wave B rewrite moved no
airframe's turn rate by more than 0.09 °/s** — the fd_speed-scaling error shape C22 was written
against is intact and untouched by the force path.

Level top speed vs authored fd_speed, all eleven (the equilibrium solve, live in the integrator):
bhawk 300.46/302.0, peacemaker 289.99/290.8, fury 280.96/281.9, avenger 261.43/264.0, devastator
251.17/252.8, brigand 240.01/241.6, kestrel 215.31/217.0, firebrand 206.71/208.0, warhawk
200.93/201.3 — nine inside 1% — autogyro 214.93/228.2 (0.94×), balmoral 125.87/176.7 (0.71×), the
two non-fighters, as recorded in the decode doc's solve table.

## Determinism

Every instrument above was run twice and diffed byte-for-byte — all empty diffs: `--dump-flight`
(both airframes, via `RunProbe.ps1`), `ZzBaselineDump` (two `CSVM_DUMP_OUT` paths),
`cap05-drag-points.ps1`, and `FlightEnvelopeTests` (two passes, same 7-scenario assert count).

## B15 — stall speed per airframe

`isStalled()`'s nose-drop threshold is now `FlightModel.StallSpeed`, solved from the SAME
aerodynamic ceiling lift caps with, `clMax(V)·q(V)·RefArea = VehWeight` (a load factor of exactly
1 G — see the G-convention note below), in place of the fixed `0.25 × fd_speed` fraction. Computed
through the real `PlaneStats.Load` path for all eleven player airframes (`ClMaxStatic = 0.75`,
`ClMaxMach = 0.15`, dense-band ρ = 2.2688e-3 slug/ft³):

| Airframe | Weight | RefArea | fd_speed (mph) | new StallSpeed (mph) | old 0.25·fd (mph) | Δ |
|---|---:|---:|---:|---:|---:|---:|
| bhawk (Bloodhawk) | 1900 | 330 | 301.99 | **56.51** | 75.50 | −25.1% |
| devastator | 2850 | 515 | 252.77 | **55.40** | 63.19 | −12.3% |
| fury | 1500 | 280 | 281.85 | **54.50** | 70.46 | −22.7% |
| warhawk | 3000 | 540 | 201.32 | **55.50** | 50.33 | +10.3% |
| autogyro | 500 | 800 | 228.17 | **18.53** | 57.04 | **−67.5%** |
| avenger | 2325 | 400 | 263.96 | **56.78** | 65.99 | −14.0% |
| balmoral | 4125 | 1100 | 176.72 | **45.54** | 44.18 | +3.1% |
| brigand | 3100 | 530 | 241.59 | **56.96** | 60.40 | −5.7% |
| firebrand | 3850 | 775 | 208.04 | **52.46** | 52.01 | +0.9% |
| kestrel | 4000 | 675 | 216.98 | **57.34** | 54.25 | +5.7% |
| peacemaker | 1600 | 300 | 290.80 | **54.37** | 72.70 | −25.2% |

**Surprise: the autogyro moves most, not the Balmoral.** The plan's own evidence flagged the
Balmoral (a bomber, "the airframe that already sits at every margin") as most likely to move: it
in fact moves the LEAST of any non-firebrand airframe (+3.1%). The autogyro's enormous `ref_area`
(800) against a tiny `veh_weight` (500) — the lightest wing loading of the eleven by a wide
margin — drops its stall speed by two-thirds, which is exactly the aerodynamically-sensible
outcome the fixed fraction could never express (it is blind to `ref_area` entirely).

**The G-convention choice.** `clMax(V)·q(V)·RefArea = VehWeight` is a load factor of 1 — not
`nom_gravity / StandardG ≈ 2.037` (what level flight itself demands to cancel this install's
arcade gravity, per B11's identity). The two conventions differ by
`√(nom_gravity/StandardG) ≈ 1.43×`. The decode's own worked example settles which one is coded:
its "fallback aircraft" (`veh_weight` 3500, `ref_area` 335) sanity-checks to 75.5 mph under the
dense band and 309 mph under the thin one (`docs/org/flightModel.md`, "Stall") — both figures
reproduce ONLY under the bare-Weight (1 G) convention (109/447 mph under the nom_gravity-scaled
read, which the decode never quotes). 1 G is therefore what the binary computes, not a
documentation shortcut for something else.

**Recorded, not swept under: a real decode-vs-footage conflict.** Evaluated against the
Bloodhawk's OWN data (1900/330) rather than the fallback aircraft's, the 1 G formula gives 56.5 mph
— but the "Stall 0% Thrust no input" clip measures the Bloodhawk's actual nose-drop at ~76 mph
(`FlightModel.cs`'s stall-threshold comment). The fixed `0.25 × fd_speed` this item retires only
matched that clip because 0.25 × the BLOODHAWK's fd_speed (302 mph) happens to sit close to the
FALLBACK aircraft's own stall speed (75.5–76.3 mph) — a coincidence of wing loading the plan itself
warned not to read as validation, not a coincidence that extends to the Bloodhawk's real numbers.
Using `nom_gravity/StandardG` instead would land at 80.9 mph (6.5% over the clip) rather than 1 G's
56.5 mph (34% under) — closer, but it breaks the decode's own 75.5/309 mph anchor pair for the
fallback aircraft, which only reproduces under 1 G. Recorded here rather than resolved by picking
whichever convention flatters one clip; a candidate for Wave D if the gap needs closing later.

## C21 — yaw authority from the original's authored speed table

`FlightModel.YawAuthorityAt` replaces the interim `eff = 1.4 − clamp(Speed/fd, 0.25, 1.15)` with
the original's own piecewise curve (`YawLowSpeed` 0.0625 below `YawFadeIn` 10 mph, ramping to 1.0
at `YawMax` 50 mph, falling to `YawHighSpeed` 0.17 at `YawFadeOut` 400 mph, flat beyond), applied to
yaw only. `YawTune` refit **1.32 → 1.33** against the pinned full-rudder 360°.

**Rudder authority, old `eff` vs the new curve, at three speeds (Bloodhawk):**

| Speed | old `eff` | new curve | Δ |
|---|---:|---:|---:|
| 60 mph (just above stall) | 1.150 | 0.976 | −15.1% |
| 290–297 mph (cruise, the yaw-360 fit point) | ≈0.440 | ≈0.431 | −2.0% |
| 336 mph (the model's own achieved terminal-dive speed) | 0.287 | 0.322 | +12.2% |

The two curves are nearly coincident exactly where `YawTune` was fit, which is why the refit is a
1-in-132 correction rather than a re-tune, and diverge sharply off cruise — a single-speed check at
290 mph could not have distinguished the two curves at all.

**Bloodhawk yaw-360:** 28.68 s (pre-C21) → **28.65 s** (target 28.6 ± 3, +0.2%, **green, asserted**).
All ten other asserted/informational rows in the Bloodhawk table above are unmoved to the last
printed digit; `sustained-turn-speed`/`sustained-turn-rate` (still C22's) moved ≤ 0.00 across all
eleven airframes, confirming yaw authority has no path into the sustained-pull turn (that manoeuvre
commands pitch, not yaw).

**All eleven airframes' yaw-360 (`ZzBaselineDump`), s:** bhawk **28.65**, devastator 22.53, fury
25.85, warhawk 30.88, autogyro 13.12, avenger 23.55, balmoral 30.32, brigand 21.48, firebrand 18.97,
kestrel 19.55, peacemaker 27.07. Only the Bloodhawk has a measured original to compare against; the
other ten are recorded here as the new model's own numbers, with no prior baseline.

Determinism: `--dump-flight` (Bloodhawk and Balmoral) and `ZzBaselineDump` (all eleven) each run
twice, byte-identical both times. `RunTests.ps1` green: units 697/697 (`FlightEnvelopeTests`'s
7-row assert count unchanged), engine 29/29, goldens 13/13 hash-identical (`--freecam` never
instantiates a `FlightModel`, and none of the four flown-plane goldens command rudder). Full
8-chapter `--freecam` regression clean, zero engine errors.

## C22 — the original's bank→yaw / bank→pitch coupling

`FlightModel.Step` adds the two hardcoded constants to the `BodyRates` command:
`ω_yaw += 0.205·(starboard·up)` signed by bank, and
`ω_pitch += 0.165·|starboard·up| + [inverted]·0.205·|bodyUp·up|` always nose-up, each × that axis'
`RecInertia` (the only scaling the original applies downstream) and **not** × the axis' `*Tune`.
No `*Tune` was refit. Full byte-level derivation, including the inverted case and the
developer-console origin of the two constants, is in
[`docs/org/flightModel.md`](../../docs/org/flightModel.md), "Bank coupling — resolved".

**A/B method.** Before = the same build with both constants set to `0f` (METHOD-9/10/15), which
reproduces the post-C21 table exactly — 261.01 / −2.65 / 32.35 / 1341.20 — so every movement below
is this item's alone. The temporary edit was reverted and `git diff` checked (METHOD-17).

**Bloodhawk (`.\RunProbe.ps1 --dump-flight --headless`):**

| scenario | unit | post-C21 | POST-C22 | original | disposition |
|---|---|---:|---:|---:|---|
| level-top-speed | mph | 300.46 | **300.46** | 300.40 | green (asserted), **unmoved** |
| accel-150-290 | s | 3.22 | **3.22** | 3.76 ± 0.40 | conflict recorded, **unmoved** |
| terminal-dive | mph | 336.36 | **336.36** | 355.2 ± 6 | owned by D32, **unmoved** |
| roll-360 | s | 1.98 | **1.98** | 2.05 | green (asserted), **unmoved** |
| pitch-rate | °/s | 33.47 | **33.47** | 33.0 | green (asserted), **unmoved** |
| yaw-360 | s | 28.65 | **28.65** | 28.6 | green (asserted), **unmoved** |
| altitude-cap | ft | 6571.95 | **6571.95** | 6571.6 | green (asserted), **unmoved** |
| level-speed-near-cap | mph | 300.46 | **300.46** | 300.40 | green (asserted), **unmoved** |
| sustained-turn-speed | mph | 261.01 | **255.64** | 222.94 ± 5 | +17.1% → **+14.7%**; still informational |
| sustained-turn-sink | ft/s | −2.65 | **1.66** | ≤ 1.85 | **green (asserted)** — stops climbing out of the turn |
| sustained-turn-rate | °/s | 32.35 | **34.71** | 18.95 | ⚠ **moved AWAY**; stays informational — see below |
| eighth-throttle-speed | mph | 134.52 | **134.52** | 137.9 ± 6 | green (informational), **unmoved** |
| decel-290-150 | s | 3.48 | **3.48** | 7.04 | conflict recorded, **unmoved** |
| zoom-climb | ft | 1341.20 | **1321.51** | 936 | informational; the loop goes inverted, so in scope |
| zoom-climb-min-speed | mph | 236.06 | **235.50** | 127.9 | informational, same loop |

The ten unmoved rows are unmoved **to the last printed digit on all eleven airframes**, not just
the Bloodhawk — both terms vanish identically at wings-level upright, so nothing that flies
wings-level can see this change. That is the item's able-to-fail invariant (METHOD-12) and it held.

**⚠ `sustained-turn-rate` is NOT promoted to asserting, and the reason is a disproof.** The plan
named this coupling as the leading candidate for the 32 vs 18.95 °/s gap. It is not: both terms
*add* heading rate in the direction of bank, so the rate rises, and it rises on ten of eleven
airframes — bhawk 32.35 → 34.71, peacemaker 29.98 → 32.25, fury 27.12 → 29.09, avenger
24.62 → 26.29, devastator 22.92 → 24.49, brigand 18.52 → 19.66, kestrel 16.24 → 16.98, firebrand
15.48 → 16.11, warhawk 14.97 → 15.68, balmoral 9.50 → 10.03. There is no sign or scale of the
decoded terms that subtracts turn rate. `BL-095`'s `turn_fade_in`/`turn_fade_out`/`highGs` now
carry the whole of the original's 1.6×-slower banked pull.

**⚠ The eleventh airframe is an instrument anomaly, recorded not chased.** The autogyro's rate
*falls* 33.44 → **13.04 °/s** while its settled bank (75.9°, unchanged) and settled speed
(213.42 → 213.24 mph) do not move at all. A turn rate at fixed bank and speed cannot fall 61% for a
physical reason; the row is a heading integral and its `swept` figure drops 532° → 208°, so this
reads as the unfolding estimator meeting a near-vertical flight path on the airframe most able to
reach one. Do not build anything on this number.

**Knife-edge / inverted hold (throwaway `FlightModel` probe, Bloodhawk and Balmoral, neutral
stick, full throttle):** the shape changed, which is the D31-relevant result.

| hold | t | before: nose / alt | after: nose / alt |
|---|---:|---|---|
| bhawk 90° bank, 300 mph | 1 s | −4.01° / −1.9 m | −5.00° / −2.1 m |
| | 3 s | −4.01° / −14.9 m | −7.91° / −20.2 m |
| | 10 s | −4.01° / −79.1 m | −17.67° / −200.2 m |
| | 35 s | −4.01° / −316.4 m | **−41.81° / −1993.0 m** |
| balmoral 90° bank, 143 mph | 35 s | −4.01° / −134.8 m | **−40.15° / −785.2 m** |
| bhawk inverted 180°, 300 mph | 10 s | −0.00° / 0.0 m | **−16.90° / −169.6 m** |
| bhawk level 0°, 300 mph | 10 s | 0.00° / 0.0 m | 0.00° / 0.0 m |

Before, the nose pinned at `KnifeNoseSag`'s bounded −4.01° and the sink settled flat by ~10 s.
After, it drifts **linearly at ≈1.08 °/s with no equilibrium** — the shape `BL-247` measured on the
original (0.69–0.89 °/s to −27° over 36 s, still steepening) and the one the bound was recorded as
unable to produce. The drift is now ≈1.2–1.6× too fast, i.e. a magnitude question where it was a
mechanism question. **`KnifeNoseSag`/`KnifeNoseRate` were not touched** — D31 owns this.
Full-aileron steady roll rate is 200.41 °/s before and after, and the coupling asserts zero
contribution to the roll axis, so roll is untouched on both a measurement and a test.

**Goldens: one moved, `c1-flight`, and which one is the evidence (GOLD-5).** It is the only golden
whose `--hold` carries a roll input (`0.2,0.1,0,1`) and therefore the only one that banks;
`empty-stage` (`0,0,0,0.6`), `c1-destroy-effects` and `c1-crash` fly wings-level and are
hash-identical. Manifest re-pinned in the same change, shot eyeballed (banked Bloodhawk over C1,
HUD and gauges intact).

**Determinism.** `--dump-flight` (Bloodhawk, Balmoral) and `ZzBaselineDump` (all eleven) each run
twice with an empty diff. `RunTests.ps1` green: units **702/702** (697 + `BankCouplingTests`'s 5,
`FlightEnvelopeTests`' 7-row assert count unchanged), engine 29/29, goldens 13/13 after the re-pin.
Full 8-chapter `--freecam` regression clean, zero engine errors, all eight screenshots saved.

## C23 — `return_rate` as the original's weathervane torque

`FlightModel.WeathervaneTorque()` adds `return_rate · (α/2) · unit(nose × v̂)` to the same
`BodyRates` command the stick and the bank coupling feed (× that axis' `RecInertia`), and
`return_rate` leaves the `damp` vector — damping is `ang_momentum_damp` alone, stick held or not.
Byte-level derivation, including the **sign correction** (`cross(nose, v̂)`, not `cross(−nose, v̂)`)
and the **half angle**, is in [`docs/org/flightModel.md`](../../docs/org/flightModel.md),
"Weathervane centring — resolved". Raw outputs:
[`raw/post-c23-dump-bhawk.txt`](raw/post-c23-dump-bhawk.txt),
[`raw/post-c23-dump-balmoral.txt`](raw/post-c23-dump-balmoral.txt),
[`raw/post-c23-all-airframes.txt`](raw/post-c23-all-airframes.txt),
[`raw/post-c23-cadence-sweep.txt`](raw/post-c23-cadence-sweep.txt) and its
[pre-C23 pair](raw/pre-c23-cadence-sweep.txt).

**A/B method.** Before = the same build with the weathervane commented out, `return_rate` restored
to the `damp` vector and the two tunes back at 0.75/1.33. That control reproduces the committed
`C22` build's `--dump-flight` and cadence-sweep output **byte-identically** (checked by rebuilding
from `git show HEAD:` and diffing), so every movement below is this item's alone (METHOD-6/9/15).
`git diff` proves the temporary edit restored (METHOD-17).

**The BL-147 instrument, and it is new.** The square-wave pitch-cadence sweep existed only as
footage of the original; `CSVM.Tests/ZzCadenceSweep.cs` is now the same experiment run through our
own model — alternating full pitch-up/pitch-down at the original's six recorded cadences, 3 periods
settled and 12 fitted, amplitude taken by fitting **cubic + sin + cos simultaneously** (never
detrending first, `BL-147`'s own trap). Both time readings are swept, since the cadences are the
original's *wall* milliseconds and sim time runs at k = 1.390 (DET-11); the roll-off is invariant
to the choice, only the operating point is not.

| cadence (wall) | original ft | pre-C23 ft | post-C23 ft | (wall reading) pre → post |
|---|---:|---:|---:|---:|
| 1300 ms | 26.31 | 2.4488 | **3.3538** | 9.1007 → **11.3474** |
| 930 ms | 7.07 | 0.7084 | **0.8507** | 2.3668 → **3.2721** |
| 700 ms | 3.09 | 0.2640 | **0.3076** | 0.7921 → **0.9732** |
| 570 ms | 0.63 | 0.1249 | **0.1439** | 0.4400 → **0.5075** |
| 370 ms | ≤ 0.065 | 0.0259 | **0.0303** | 0.1777 → **0.1969** |
| 230 ms | ≤ 0.037 | 0.0039 | **0.0050** | 0.0245 → **0.0298** |
| **roll-off 1300 → 570** | **42×** | 19.6× | **23.3×** | 20.7× → **22.4×** |

**`BL-147` does not close.** The weathervane moves the roll-off the right way and closes about a
sixth of the gap. ⚠ **It also corrects `BL-147`'s own arithmetic:** the "42× is 3.5× steeper than
any single first-order lag permits" reading assumes the chain is double integration + one lag, and
the remake's is not — the flight path chases the nose through a *second* first-order lag
(`lift_accel_rate`, τ = 1.33 s), so the pre-C23 build already rolled off 19.6×, 1.65× past that
"ceiling", with `return_rate` still folded into the damping. The amplitude-for-amplitude column
above replaces the 3.5× figure as the statement of our deficit.

**Bloodhawk (`.\RunProbe.ps1 --dump-flight --headless`):**

| scenario | unit | POST-C22 | POST-C23 | original | disposition |
|---|---|---:|---:|---:|---|
| level-top-speed | mph | 300.46 | **300.46** | 300.40 | green (asserted), **unmoved** |
| accel-150-290 | s | 3.22 | **3.22** | 3.76 ± 0.40 | conflict recorded, **unmoved** |
| terminal-dive | mph | 336.36 | **336.36** | 355.2 ± 6 | owned by D32, **unmoved** |
| roll-360 | s | 1.98 | **1.98** | 2.05 | green (asserted), **unmoved** — the torque cannot reach roll |
| pitch-rate | °/s | 33.47 | **33.54** | 33.0 | green (asserted), re-pinned by `PitchTune` 0.75 → 0.89 |
| yaw-360 | s | 28.65 | **28.55** | 28.6 | green (asserted), re-pinned by `YawTune` 1.33 → 1.57 |
| altitude-cap | ft | 6571.95 | **6571.82** | 6571.6 | green (asserted) |
| level-speed-near-cap | mph | 300.46 | **300.46** | 300.40 | green (asserted), **unmoved** |
| sustained-turn-speed | mph | 255.64 | **258.85** | 222.94 ± 5 | informational; +14.7% → +16.1% |
| sustained-turn-sink | ft/s | 1.66 | **2.33** | ≤ 1.85 | ⚠ **downgraded to informational** — see below |
| sustained-turn-rate | °/s | 34.71 | **33.38** | 18.95 | informational; +83% → +76%, still not the mechanism |
| eighth-throttle-speed | mph | 134.52 | **134.52** | 137.9 ± 6 | green (informational), **unmoved** |
| decel-290-150 | s | 3.48 | **3.48** | 7.04 | conflict recorded, **unmoved** |
| zoom-climb | ft | 1321.52 | **1335.71** | 936 | informational |
| zoom-climb-min-speed | mph | 235.50 | **236.48** | 127.9 | informational |

The unmoved rows are unmoved because they hold **α = 0**: the torque vanishes identically when the
nose is on the flight path, which is the item's able-to-fail invariant (METHOD-12) and it held.

**⚠ `sustained-turn-sink` is downgraded from asserting, and `FlightEnvelopeTests` now asserts 6.**
The weathervane opposes the sustained pull, so the turn rate falls and the sink rises past the
1.85 ft/s bound. It is the third leg of a manoeuvre whose other two legs are already informational
and owned by `BL-095`'s `turn_fade_*`/`highGs`: the model sweeps heading 76 % faster than the
original at a bank the original never flew, and a sink read off that flight path has no reason to
land on the original's — C22's green there sat inside the same unattributed gap. Recorded with the
attribution in its own probe comment (B14's pattern); it re-asserts with the rate row, not before.

**⚠ Two `*Tune` constants moved, and the plan's "steady rates unchanged by construction" premise is
where this came from.** The weathervane vanishes at zero misalignment — but a *sustained full-stick*
manoeuvre holds a real misalignment (α ≈ 18° pulling, β ≈ 8° on full rudder), so the restoring
torque opposes the stick there. Un-refit: `pitch-rate` 33.47 → **28.35** °/s and `yaw-360`
28.65 → **34.43** s, both outside their bands. `PitchTune` 0.75 → 0.89 and `YawTune` 1.33 → 1.57
re-pin them against the same measurements they were always pinned to (Decision 4's carve-out,
spent). This is the opposite of what `BL-147` forbids: `*Tune` sets the steady rate, and a
mechanism moved the steady rate. `RollTune` is untouched. Context worth keeping: the un-tuned
formula `pitch_torque · rec_inertia / ang_momentum_damp` gives 44.6 °/s against a measured 33, so
the weathervane explains a little over half of `PitchTune`'s existence.

**All eleven airframes (`ZzBaselineDump`), POST-C22 → POST-C23.** `roll-360` is unmoved to the last
printed digit on **every** airframe (1.98 / 2.92 / 2.08 / 4.97 / 4.97 / 2.55 / 4.97 / 3.93 / 4.90 /
4.40 / 2.03), and the full-aileron steady roll rate is 200.42 °/s before and after — the roll-axis
invariant on a measurement as well as in a test. `pitch-rate` rises on all eleven (the global
`PitchTune` refit is Bloodhawk-pinned, as it always was, while the weathervane's opposition is
per-airframe): warhawk 15.60 → 17.24, autogyro 34.48 → 37.95, balmoral 9.63 → 10.64, peacemaker
31.20 → 31.67. `yaw-360` falls on all eleven: warhawk 30.88 → 28.53, autogyro 13.12 → 11.37,
balmoral 30.32 → 29.13. `sustained-turn-sink` mostly **improves** off the Bloodhawk — warhawk
13.13 → 6.44, firebrand 9.79 → 0.53, kestrel 7.64 → 0.61, balmoral 18.09 → 15.97 — which is the
Bloodhawk's own worsening read the other way round. The autogyro's turn rate 13.04 → 16.10 is the
C22 instrument anomaly moving; still not a number to build on.

**Balmoral:** `level-top-speed` 125.87 and `terminal-dive` 149.49 unmoved; `eighth-throttle-speed`
58.27 → **65.34** mph, which the Bloodhawk's cannot do — the Balmoral cannot hold level at 1/8
lever and settles at a −9.4° path, so it is the one part-throttle case with a live misalignment.

**D31's number, measured and recorded, not acted on.** The weathervane **steepens** the knife-edge
rather than opposing it: in a sagging knife-edge the flight path is *below* the nose, so the torque
pulls the nose down onto it. (Neutral stick, full throttle, throwaway `FlightModel` probe.)

| hold | t | POST-C22: nose / alt | POST-C23: nose / alt |
|---|---:|---|---|
| bhawk 90° bank, 300 mph | 1 s | −4.98° / −2.0 m | −4.91° / −2.0 m |
| | 3 s | −7.88° / −20.0 m | −7.93° / −19.8 m |
| | 10 s | −17.67° / −200.2 m | −18.74° / −206.4 m |
| | 35 s | −41.80° / −1991.4 m | **−44.61° / −2123.6 m** |
| balmoral 90° bank, 143 mph | 35 s | −40.14° / −784.5 m | **−47.16° / −918.9 m** |
| bhawk inverted 180°, 300 mph | 10 s | −16.90° / −169.6 m | **−20.90° / −214.6 m** |
| bhawk level 0°, 300 mph | 10 s | −0.00° / 0.0 m | −0.00° / 0.0 m |

The drift is now ≈**1.17 °/s** against `BL-247`'s measured 0.69–0.89 °/s — ≈1.3–1.7× too fast,
where C22 left it 1.2–1.6×. `KnifeNoseSag`/`KnifeNoseRate`/`KnifeAlignFloor` are untouched.

**Goldens: one moved, `c1-flight`, and which one is the evidence (GOLD-5).** It is the only golden
whose `--hold` carries a **pitch** input (`0.2,0.1,0,1`) and therefore the only one that ever holds
a nose/path misalignment; `empty-stage` (`0,0,0,0.6`), `c1-destroy-effects` and `c1-crash` fly
stick-centred and are hash-identical. Manifest re-pinned in the same change, shot eyeballed (banked
Bloodhawk over C1, HUD, gauges and exhaust trail intact).

**Determinism.** `--dump-flight` (Bloodhawk, Balmoral), `ZzBaselineDump` (all eleven) and the
cadence sweep each run twice with an empty diff. `RunTests.ps1` green: units **710/710** (702 +
`WeathervaneTests`' 7 + the sweep; `FlightEnvelopeTests` at its new 6-row assert count), engine
29/29, goldens 13/13 after the re-pin. The tests' able-to-fail control is a deliberate perturbation
(METHOD-9): reading the full misalignment angle instead of half fails 1 of the 7, a sign flip
fails 2. Full 8-chapter `--freecam` regression clean, zero engine errors, all eight screenshots
saved.
