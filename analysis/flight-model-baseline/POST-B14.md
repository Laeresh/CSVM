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
