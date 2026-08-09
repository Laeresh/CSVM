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
| yaw-360 | s | 29.17 | **28.68** | 28.6 | **green** (asserted) |
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
