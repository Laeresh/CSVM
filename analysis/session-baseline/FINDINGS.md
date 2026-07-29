# Launch-resolution baseline

**Question.** What does each command line actually resolve to, and can that be pinned precisely
enough that a ~1,000-line refactor of `PlaneViewer`'s argument handling is provably behaviour-neutral?

**Verdict.** Yes. `--dump-session` prints every resolved launch setting (124 rows) as sorted
`key = value` text; `capture.ps1` runs 50 command lines through it into `baseline.txt`. Two captures
of the same build are **md5-identical**, and a deliberately perturbed build moves exactly the rows
that rule touches. This is the pre-refactor baseline for `docs/PLAN-sessionspec.md`.

## Why it lives here rather than in `.scratch/`

The plan drafted it into `.scratch/`. That is wrong for a baseline: `.scratch/` is swept by
`CleanScratch.ps1`, and this file has to survive until the last item of a multi-wave plan verifies
against it. The `probe_exempt.py` precedent in `../README.md` is the same mistake one step later.

`--dump-session` itself is scaffolding and gets deleted when the plan lands. The matrix and the
baseline stay: they are the record of what the launch arguments meant before the refactor, and
nothing else in the repo holds that.

## Running it

```
./analysis/session-baseline/capture.ps1               # rewrite baseline.txt in place
./analysis/session-baseline/capture.ps1 -Out new.txt  # capture elsewhere, then diff
```

~50 headless launches, about 90 s. Every row must exit 0. Read a diff row by row — a moved row is
either the change you meant or the one you did not.

## Coverage, and why this matrix

Weighted deliberately toward what the pixel goldens cannot see. Measured against
`analysis/goldens/manifest.json`: `--freecam` has 8 golden shots, `--viewer` 1, flight 1,
`--stage=empty` 1 — and **`--anim-lab`, `--stunt`, splitscreen and the menu path have none at all**.
A green `RunTests.ps1` is therefore not evidence that a launch still resolves the way it did.

| Group | Rows | What it pins |
|---|---|---|
| `menu-*` | 4 | The no-content-arg base a menu launch is patched on top of |
| `fly-*` | 9 | Flight as the default, placement routing, the deprecated aliases |
| `det-*` | 5 | The bundle implied, opted out of, and overridden constituent by constituent |
| `stunt-*` | 3 | Scenario forcing, and the explicit `--scenario=` that survives it |
| `split-*` | 3 | Player count arriving three ways, including the clamp |
| `viewer-*` | 6 | The static view, its labs, and `--viewer` beating `--fly` |
| `freecam-*` | 4 | The spectator view and the collision/damage overlays |
| `animlab-*` | 4 | The most specific mode, and `--node=` NOT forcing viewer under it |
| `stage-*` | 3 | The synthetic stage accepted, refused in viewer, and misspelled |
| `probe-*` | 5 | The probes that coerce a mode at parse time |
| paint / tex / misc | 4 | Modifiers that touch no mode |

## Instrument properties that had to be established first

Each of these was a defect in the first draft, found by reading the output rather than by reasoning:

- **A clock-derived value makes a baseline differ from itself.** The resolved master seed is drawn
  from the clock whenever nothing pins it. It now prints `<clock>` unless pinned — the reportable
  fact is *that it came from the clock*, not which number came out. (`docs/verification.md` rule 118)
- **Absolute paths make a baseline one machine's.** Paths render against `{data}` / `{repo}`.
- **The observer must not be a term of what it observes.** `--dump-session` meets the `--det`
  membership rule and the `_mode` naming rule, and obeying either destroys the instrument. It is the
  documented exception to both. (rule 117)
- **ASCII and LF only.** The text is captured by a PowerShell 5.1 harness; a single em dash
  round-trips through the console's ANSI codepage as mojibake that then reads as a diff on every row.
- **`$out` is `$Out`.** PowerShell variable names are case-insensitive, so the loop variable holding
  each run's console output silently overwrote the parameter holding the destination path. Loud
  failure here, but the same collision in a variable that is only *read* would have been silent.

## Able-to-fail control

A probe that has never reported failure is not yet an instrument. Both arms were exercised on a
deliberately perturbed build (2026-07-25), then reverted and the baseline confirmed to reproduce:

| Perturbation | Expected | Observed |
|---|---|---|
| `--stunt`'s forced scenario renamed | the `stunt-*` rows move | `world.scenario = stunt_CONTROL`, exit 0 |
| a duplicate key added to the field list | reported, and nonzero exit | `!! duplicate key: mode.fly`, `125 settings, 1 DUPLICATE KEY(S)`, **exit 1** |

After reverting both, `capture.ps1` reproduced `baseline.txt` byte for byte
(md5 `944310579BA214A0E99B801FC344098B`).

## Two latent defects the baseline exposed

Neither is fixed here — the whole point of a baseline is to record current behaviour, including its
warts, so the refactor can be shown not to have changed anything by accident. Both are for the
resolution item of `docs/PLAN-sessionspec.md`.

1. **`--dump-flight` is missing from the `_mode` "dump" chain.** Row `probe-dump-flight` resolves
   `mode.name = menu`, where every other probe resolves to `test` or `dump`. Consequence: a
   `--dump-flight` run writes its log as `menu-<timestamp>.log`. `PlaneViewer.cs` lists
   `_dumpMarkers || _dumpWeapons || _dumpLoadout || _dumpConfig` and omits `_dumpFlight` — the same
   one-term-at-a-time drift as rule 116, in a fifth copy.
2. **`--run-tests` resolves to a session that would show the launchscreen.** Row `probe-run-tests`
   has `mode.showsMenu = true` and no content arg. Harmless only because the `--run-tests` branch
   returns before the menu branch is reached: the correctness of the test harness currently rests on
   statement order in `_Ready`, which is exactly what the plan replaces with a total function.

## The equivalence gate (2026-07-29)

`compare.ps1` runs the same matrix with `--dump-session=compare`, which resolves every setting twice
in one process — once through `PlaneViewer`'s own fields, once through `SessionSpec` — and exits
nonzero on any disagreement. **50 of 50 rows agree; 5,700 field/spec value pairs compared.**

114 of the 124 settings are compared and **the report names the other 10 on every row**: the nine
derived `path.*` values (PlaneViewer's own arithmetic over `SessionPaths` and the `CSVM_DATA_ROOT`
precedence — the spec records override *values*, not resolved paths) and `tex.overrides`
(`TextureDropIn`'s name grammar, which the spec keeps as the raw request). A gate that narrows what
it checks without saying so reads exactly like one that passed.

Able-to-fail, twice, each caught on the exact row: forcing `--stunt`'s scenario to `stunt_flyingX`
failed `stunt-bare` and `stunt-2p` (`world.scenario field=stunt_flying spec=stunt_flyingX`), and
clamping the player count to 3 failed `--fly --players=4` (`plane.players field=4 spec=3`). Both
reverts were confirmed green — after a forced rebuild, because the first attempt restored a file
older than the DLL and silently re-measured the perturbation (verification rule 124).

The matrix now lives in `matrix.ps1`, dot-sourced by both scripts, so the recorder and the gate
cannot drift into testing different command lines.
