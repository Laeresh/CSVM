# Flight model baseline — PLAN-flight-model-rewrite A2

**Purpose.** A committed, re-runnable snapshot of the CURRENT model's numbers (pre-rewrite, i.e.
before any of Wave B/C/D lands) on every scenario the rewrite plan will move. Later items diff
their own re-run of these same commands against this table, not against memory. Nothing here
changed `src/Flight/FlightModel.cs` or `src/Flight/PlaneStats.cs` — every number below comes from
running the *existing* instruments and, for the one scenario with no instrument, evaluating the
model's own committed constants by hand.

Raw outputs live in [`raw/`](raw/); the drag-point script is
[`cap05-drag-points.ps1`](cap05-drag-points.ps1).

**B14 re-ran this whole table against the decoded aero core and dispositioned every row —
[`POST-B14.md`](POST-B14.md) is the record Wave C/D measure against.** This file stays as the
pre-rewrite snapshot.

## Instruments used, and which verification.md rule bites

| Instrument | Command | What it's good for | verification.md rule |
|---|---|---|---|
| `--dump-flight[=plane]` | `.\RunProbe.ps1 --dump-flight --headless` (Bloodhawk) / `.\RunProbe.ps1 --dump-flight=player_balmoral --headless` | The six measured-and-asserted rows plus the informational ones, for one airframe, printed beside the video-decoded original | **SHELL-10** (launch through `RunProbe.ps1`, never the exe directly); the run is scripted so **DET-6** applies (`--det` implied) — confirmed bit-identical over two runs |
| `CSVM.Tests.FlightEnvelopeTests` | `dotnet test CSVM.Tests --filter FullyQualifiedName~FlightEnvelopeTests` | Asserts the scenarios that carry a measured target still pass, and that the count itself (`FlightScenarios`, 10 at A2 — B14 downgraded three recorded conflicts to informational, live count 7, see [`POST-B14.md`](POST-B14.md)) hasn't silently shrunk | **DET-7/DET-9** — engine-free, no clock, no Godot; a pure function of committed data |
| `CSVM.Tests.ZzBaselineDump.DumpEveryAirframe` | `$env:CSVM_DUMP_OUT=<path>; dotnet test CSVM.Tests --filter FullyQualifiedName~ZzBaselineDump` | Same report as `--dump-flight`, for **all 11 player airframes** in one pass — this is the per-airframe capture the item asks for beyond the Bloodhawk/Balmoral pair | Same as above. This test is a **leftover THROWAWAY from the already-landed `PLAN-flight-drag-lift`** (its own doc comment says "delete before the plan lands" — it wasn't). A2 did not delete it: it is exactly the instrument this item needs, and deleting a working, in-tree instrument to satisfy someone else's cleanup note would be the wrong trade here. Flagged for whoever eventually does that cleanup. |
| CAP-05 drag-point formula | `.\analysis\flight-model-baseline\cap05-drag-points.ps1` | The one scenario with **no runnable probe row** — evaluates `dragAccel(x) = maxThrustAccel * x^DragExpLow` directly from the constants in `FlightModel.cs`, since CAP-05's four points are the raw video measurements those constants were fitted against, not a scenario the suite steps | **DET-9** — pure arithmetic on committed constants, no clock/path/machine-state dependency; re-run twice, byte-identical |

**Doc correction found while locating instruments (not part of A2's scope to fix, recorded so the
next reader isn't misled).** `docs/cli.md` and `docs/architecture.md` both describe
`flight-envelope` as a registered `--run-tests` suite among "the fourteen suites" / "the 26
registered suites". It is not: `src/Testing/Suites.cs`'s `Register()` has no `flight-envelope`
entry (confirmed by grep and by `--run-tests=flight-envelope` reporting an unmatched-filter error).
The check now lives engine-free in `CSVM.Tests/FlightEnvelopeTests.cs` (`dotnet test`), consistent
with `docs/architecture.md`'s own `src/Testing/TestHarness.cs` rule — "only checks that need a live
Godot belong here; anything that runs without the engine goes in `CSVM.Tests`". The `--dump-flight`
flag itself is real and unaffected; only the *suite name* in the two docs is stale.

## Bloodhawk — the six pinned + four informational rows (`--dump-flight`)

Source: [`raw/dump-bhawk.txt`](raw/dump-bhawk.txt). Re-run: `.\RunProbe.ps1 --dump-flight --headless`
(writes `.scratch/flight_dump.txt`).

| scenario | unit | model | original | err | verdict | what would move it |
|---|---|---:|---:|---:|---|---|
| level-top-speed | mph | 301.99 | 300.40 | +0.5% | ok | `ThrustConst`/`DragExpHigh` at x=1 (B13) |
| accel-150-290 | s | 3.75 | 3.76 | -0.3% | ok | `ThrustConst` (B13) |
| terminal-dive | mph | 355.26 | 355.20 | +0.0% | ok | `DragExpHigh` (B12) |
| roll-360 | s | 1.98 | 2.05 | -3.3% | ok | `RollTorque`/`RollTune` (untouched by this plan) |
| pitch-rate | deg/s | 33.47 | 33.00 | +1.4% | ok | `PitchTune` (C24 refit only) |
| yaw-360 | s | 29.17 | 28.60 | +2.0% | ok | `YawTune`/yaw authority curve — **C21 refits this** |
| altitude-cap | ft | 6572.09 | 6571.60 | +0.0% | ok | `AltitudeCapM` (not in this plan) |
| level-speed-near-cap | mph | 301.99 | 300.40 | +0.5% | ok | same as level-top-speed |
| sustained-turn-speed | mph | 223.03 | 222.94 | +0.0% | ok | `InducedDragCoef` (B12 successor) — **must be re-checked after C22** per Decision 4 |
| sustained-turn-sink | ft/s | -2.40 | <= 1.85 (upper bound) | - | ok | lift keying (B11) |
| sustained-turn-rate | deg/s | **32.33** | **18.95** | **+70.6%** | (not asserted) | **KNOWN-DIVERGENT — see below** |
| eighth-throttle-speed | mph | 137.87 | 137.90 | ~0% | (not asserted) | `ThrottleExp` — **B13 either kills or promotes this** |
| decel-290-150 | s | 6.48 | 7.04 | -7.9% | (not asserted) | `DragExpLow` (B12) |
| zoom-climb | ft | 1282.25 | 936.00 | +37.0% | (not asserted) | induced drag / lift keying at high alpha (B11/B12) |
| zoom-climb-min-speed | mph | 205.26 | 127.90 | +60.5% | (not asserted) | same |

Every "ok" row is a row that would fail if the term it depends on moved the wrong way — the
`(not asserted)` rows are open questions the plan's own docs already name as such
(`docs/org/flightModel.md`'s corrections table; `FlightEnvelopeTests`'s own doc comment), not
silently-passing decoration.

## Balmoral — per-airframe check (`--dump-flight=player_balmoral`)

Source: [`raw/dump-balmoral.txt`](raw/dump-balmoral.txt). No measured original exists for this
airframe (Bloodhawk is the only one on video), so every row reads `(not asserted)`; it exists so a
later item's per-airframe check has a "before" to diff against. Headline numbers: `fd_speed` 79 m/s
(176.7 mph), weight 4125 kg, engine power 0.30, max thrust accel 7.78 m/s^2. `altitude-cap` settles
at 5559.18 ft (below the 6571 ft the faster airframes reach — the Balmoral can't out-climb its own
drag to reach the same altitude before the pull bleeds it back to `alpha = 0.0 deg`, unlike every
other airframe's `alpha = 2.2 deg` at that row). `zoom-climb-min-speed` bottoms at 20.94 mph — this
airframe is already near-stalled at the top of its own zoom, which is why it is "the airframe that
already sits at every margin" (B15's own text) and the one D31 must check specifically.

## All 11 player airframes (`ZzBaselineDump`)

Source: [`raw/all-airframes.txt`](raw/all-airframes.txt). Re-run:

```powershell
$env:CSVM_DATA_ROOT = 'Z:\CSVM'; $env:CSVM_EXTRACTED = 'Z:\CSVM\extracted'
$env:CSVM_DUMP_OUT = 'C:\path\to\output.txt'
dotnet test CSVM.Tests --filter "FullyQualifiedName~ZzBaselineDump"
```

Only the Bloodhawk carries a measured `original` column; the other ten report every row
`(not asserted)`, same as the Balmoral case above — this file is the "before" snapshot B14 diffs
against once the aero core changes, not an assertion. Node names, `fd_speed` and `sustained-turn-rate`
error ratio for all eleven (heaviest divergence first):

| node | fd_speed (mph) | sustained-turn-rate model | x original (18.95 deg/s) |
|---|---:|---:|---:|
| player_autogyro | 228.2 | 33.42 | 1.76x |
| player_bhawk (Bloodhawk) | 302.0 | 32.33 | 1.71x |
| player_peacemaker | 290.8 | 29.96 | 1.58x |
| player_fury | 281.9 | 27.12 | 1.43x |
| player_avenger | 264.0 | 24.63 | 1.30x |
| player_pfighter (Devastator) | 252.8 | 22.94 | 1.21x |
| player_brigand | 241.6 | 18.57 | 0.98x |
| player_kestrel | 217.0 | 16.32 | 0.86x |
| player_fbrand (Firebrand) | 208.0 | 15.56 | 0.82x |
| player_warhawk | 201.3 | 15.04 | 0.79x |
| player_balmoral | 176.7 | 9.57 | 0.51x |

**This is the shape C22 (bank->yaw/pitch coupling) is landing to fix.** The error scales with
fd_speed / lateral-acceleration, not a fixed offset — faster, lighter airframes over-turn more —
which is consistent with the plan's own diagnosis ("bank/load-factor effect, not a pitch authority
error") and gives C22 an eleven-airframe check instead of a one-airframe one.

## CAP-05's four zero-thrust drag points — no scenario row exists for this

There is no `--dump-flight` row that isolates zero-thrust deceleration (the closest live scenario,
`decel-290-150`, cuts throttle to zero but only reports two speeds far apart, not the four `x`
values CAP-05 was read at). CAP-05's numbers are the *inputs* `DragExpLow`/`ThrustConst` were fitted
against, not something the current instruments re-derive independently. Re-run:
`.\analysis\flight-model-baseline\cap05-drag-points.ps1`. Output (`raw/cap05-drag-points.txt`):

| x = V/fd_speed | model (m/s^2) | measured (m/s^2) | err |
|---:|---:|---:|---:|
| 0.25 | 0.3711 | 0.36 | +3.1% |
| 0.35 | 1.1182 | 1.11 | +0.7% |
| 0.46 | 2.7390 | 2.82 | -2.9% |
| 0.50 | 3.6000 | 3.74 | -3.7% |

All four land within 4%, matching `FlightModel.cs`'s own `DragExpLow` comment. **What would move
this row:** `DragExpLow` or `ThrustConst` (both retired by B12/B13) — a change to either must be
re-checked against these four numbers before it can claim to preserve CAP-05.

⚠ **The script no longer reproduces the table above** (the numbers stay valid as the A2 "before", and
`raw/cap05-drag-points.txt` still holds their raw output). B12 replaced the power law with the
original's drag polar and rewrote `cap05-drag-points.ps1` to evaluate *that* — the four points are
now an independent check of an authored curve rather than a re-reading of the fit they produced.
B13 then corrected the polar's variable from `C_L` to **Mach** (the original passes its lift
coefficient to the drag routine and never reads it), which is what the script evaluates today:

| x | model (m/s²) | measured | err |
|---:|---:|---:|---:|
| 0.25 | 1.3124 | 0.36 | +264.5% |
| 0.35 | 3.0333 | 1.11 | +173.3% |
| 0.46 | 6.1557 | 2.82 | +118.3% |
| 0.50 | 7.6786 | 3.74 | +105.3% |

The *shape* is now the measured one — a 5.9× rise across the span against the measured 10.4×, where
B12's `C_L` polar rose only 1.27× and had a speed-independent floor — but everything is ≈2–3.6×
too strong. B14 settled the suspect: the force→acceleration divisor carries **no** unit conversion
in the binary (`docs/org/flightModel.md`, "The force scale — settled"), so this is a recorded
decode-vs-footage conflict, dispositioned in [`POST-B14.md`](POST-B14.md), not an open scale
question.

## Knife-edge alpha — the Balmoral's 0.1-degree-inside-the-ramp margin

**No committed, re-runnable instrument produces this number.** `FlightModel.cs`'s own comments
(around `LiftAoaLo`) and `docs/plans/PLAN-flight-drag-lift.md`'s B12 landing notes state, as an
already-established fact about the current code:

> The Balmoral knife-edges at **alpha = 5.1 deg**, 0.1 deg *inside* the `[5, 9]` `liftAOAs` ramp,
> and gets back 2.2% of its 35-second altitude loss (466.6 -> 456.5 m). [...] 5 deg is now a live
> boundary, recorded in the code.

and, generically (same source, no airframe named, i.e. the Bloodhawk):

> Knife-edge at neutral stick settles at alpha = **3.2 deg**, and every scenario the suite asserts
> sits at alpha <= **2.9 deg**.

**I could not reproduce either figure from a scenario I wrote myself**, and I am recording that as
a finding rather than silently substituting my own number for the documented one. I tried the
direct read of "bank set at entry (90 deg), neutral stick, held": `PlaneStats.Load` + `new
FlightModel(stats)` + `Reset(Vector3.Zero, Basis.Identity.Rotated(Vector3.Forward, 90deg), fd_speed,
throttle=1)`, stepped 35 sim-seconds at 60 Hz. That settles the Bloodhawk at **alpha = 5.00 deg**
(not 3.2) and the Balmoral at **alpha = 8.49 deg** (not 5.1) — both airframes settle noticeably
higher than the documented figures, and the Bloodhawk's result landing exactly on `LiftAoaLo` (5.0)
rather than under it suggests my entry speed or throttle differs from whatever the original
investigation used (full throttle driving the aircraft toward `fd_speed` may not be how that probe
was set up — the documented figures may have used a trimmed cruise entry instead). **This is a real
gap, not a rounding difference**, and per verification.md METHOD-1 / DIAG-1 I am not guessing
further at the recipe under this item's budget: D31 is the plan item that owns resolving the
knife-edge mechanism, and it should either recover the original probe's exact parameters or build a
new one and re-derive these two numbers *as part of that item*, not inherit an unverified script
from A2. Until then, **the baseline for these two rows is the text quoted above, cited to its
source**, not a value from a script.

## Known-divergent rows (recorded so a later "it's still off" reads as informational, not a surprise)

1. **Sustained-turn-rate: 32.33 deg/s (Bloodhawk) vs the original's 18.95 deg/s, +70.6%.** Captured
   live above via `--dump-flight`, printed `(not asserted)` by `FlightEnvelopeTests` on purpose (its
   own doc comment: informational rows "record open questions and must not fail a build"). **What
   would move it:** C22 (bank->yaw/pitch coupling) is the plan's own leading candidate.
2. **Knife-edge sag settles in ~1 sim-s vs the original's 36 sim-s linear drift with no
   equilibrium.** Not independently re-run here (see the gap recorded above); quoted from
   `FlightModel.cs`'s `KnifeNoseSag` comment as the current, documented behaviour: "Ours instead
   settles inside a second at -4 deg nose / -10 deg path / 19.4 m/s" against the original's "-27 deg
   nose / -18.7 deg path / 28 m/s sink by +36 s and still steepening." **What would move it:** D31
   explicitly (the item is titled for this exact conflict); the code comment already forbids closing
   the gap by retuning `KnifeNoseSag`'s bound.

## Determinism

Every instrument above was run twice and diffed byte-for-byte before being accepted:

- `--dump-flight` (Bloodhawk) and `--dump-flight=player_balmoral`: two `RunProbe.ps1` runs,
  `Compare-Object` on the two `.scratch/flight_dump.txt` copies — **empty diff**.
- `ZzBaselineDump` (all 11 airframes): two `dotnet test` runs to two `CSVM_DUMP_OUT` paths,
  `Compare-Object` — **empty diff**.
- `cap05-drag-points.ps1`: two runs, `Compare-Object` — **empty diff** (expected; it is pure
  arithmetic with no clock or random input, DET-9).
- `FlightEnvelopeTests`: two `dotnet test` runs, both pass, 1/1, with the same 10-scenario assert
  count — no non-determinism observed.

No row in this baseline is non-deterministic; there is nothing here to extend verification.md's
"Known non-deterministic surfaces" section over.

## Scenarios named in the item that this baseline could NOT capture, and why

- **Knife-edge alpha (both airframes)** — see the dedicated section above: recorded from documented
  source text, not from a script, because no committed instrument exists and my own attempt did not
  reproduce the documented figures. This is the one row in this baseline that is decoration-shaped
  until D31 gives it a real re-run command — flagged, not hidden.

Everything else the item named — the three steady rates, `CAP-05`'s four points, `CAP-01`'s
plateau, the terminal dive, the 1/8-throttle equilibrium, the flight-envelope alpha <= 2.9 deg
scenarios, and both known-divergent rows — is captured above with a re-run command.
