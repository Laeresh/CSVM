# Anim-debugger verification (PLAN-anim-debugger Waves 1–3)

**Question.** Do the anim-debugger changes leave the game byte-identical, and does the
`--anim-lab` mode reproduce live playback deterministically?

**Verdict (2026-07-23, Wave 3 accepted).** Yes on both: game inert (plane-viewer screenshot md5
and the full C1/C5 boot censuses byte-identical HEAD-vs-after), lab playback matches the live
world pose-per-sim-second, and same-seed scripted runs are byte-identical with both controls
able to fail. Full narratives live in `docs/HISTORY.md` (the three dated "Animation debugger
Wave …" entries); this directory keeps the **instrument** (`verify.ps1`), the **baselines**
(`baseline_c1.census`, `baseline_c5.census`, produced from `main` @ Wave 3), and the numbers
below. Probe outputs (PNGs, logs) are ephemeral `.scratch/` content and are not kept — rendered
frames are game-derived and never enter version control.

## Recorded numbers (measured, 2026-07-23, this machine, 1280×720 default window)

- **Static plane-viewer md5** (`--viewer --plane=player_bhawk --no-pads --mute --screenshot`):
  `F1290254F2DDA1E3B8A9BCEE867D6C3D` — identical HEAD-vs-after for Waves 1 A2/A3 and 3.
  ⚠ Machine- and window-size-dependent: only compare values from the same machine and session;
  the durable claim is "unchanged across a stash A/B", not the constant itself.
- **C1/C5 boot censuses**: the two `baseline_*.census` files here (16 + 15 lines, `N ms`
  normalised to `X ms`). Byte-identical HEAD-vs-after in every wave. C1: 814 defs, 616
  ON_STARTUP + 5 startanims, 615 instances, 39 motions, 4132 state ops.
- **Quiet stage** (`--anim-lab --chapter=C1`): `0 ON_STARTUP + 0 start anims running, 0 live
  instance(s), 0 live motion(s)` with 3511 state ops applied, mission setup 29 nodes, safety
  net 31 subtrees, 0 colliders (by design, `Collision=false`).
- **Lab vs live** (`--play-anim=train_on_track` fixed clock vs `--freecam` wall clock,
  `--debug-anim` caboose pose per logged sim-second):

  | s | lab (fixed dt) | freecam (live) |
  |---|---|---|
  | 1 | (-6956.0, 128.0, -5427.0) rot -22.6° | (-6956.1, 128.0, -5426.7) rot -22.6° |
  | 4 | (-6908.7, 128.0, -5530.5) rot -30.3° | (-6909.6, 128.0, -5529.1) rot -30.1° |
  | 7 | (-6852.5, 128.0, -5609.5) rot -47.0° | (-6854.4, 128.0, -5607.4) rot -46.7° |
  | 10 | (-6779.6, 128.0, -5673.6) rot -50.7° | (-6783.1, 128.0, -5670.9) rot -50.6° |

  Same track, same ~35 units/s, rotations within 0.4°; the growing ~0.3 m/s offset is the
  freecam side's wall-clock 1 Hz sampler, not a divergence.
- **Determinism** (`--play-anim=mp_hangar3_open --seed=42 --frames=90 --shots=3 --jitter=0`):
  two runs → all three frames byte-identical (`E34789C5…`, `3A854EC8…`, `410C6C2E…` both
  times); the `--frames=30` control differs on every frame.
- **Seed** (`--chapter=C1 --mission=M05 --play-anim=random_prop`, RandomWeight verdicts):
  seeds 1/2/4 → `false TRUE false`; seeds 3/5/6 → `false` (a different branch). Same-seed
  reruns identical. With 3 rolls at weight 0.125 seed collisions are common — probe several
  seeds before concluding "seed does nothing".

## Instrument bugs hit on the way (the part worth re-reading)

- **The 2× clock the pose comparison could not see (measured).** The lab world ran at exactly
  2× — the runtime's own `_Process` ticked wall time on top of the lab's fixed steps, because
  Godot re-enables processing at READY and undid a too-early `SetProcess(false)`. The
  lab-vs-live pose table above *still matched perfectly* while the bug was live: both sides are
  sampled per accumulated sim-second, so pose-at-logged-second-N is a pure function of sim time
  and a clock-rate error cancels out. What caught it was the external denominator — 20 logged
  sim-seconds in a 610-frame (~10 s) run vs the freecam control's 10 — and its visible symptom
  was same-seed bursts differing by ~5k pixels (a quarter-step of door motion: wall-dt
  variance). Now `verify.ps1` check 3 and a rule in `docs/verification.md` §4; the fix is
  `AnimRuntime.ManualAdvance`.
- **`LogMotions`' `Take(12)` cap (known, re-confirmed).** The freecam side of the train
  comparison needs the cap raised in a throwaway build — the train's motions register during
  pass 3, after ~35 ON_STARTUP motions. The lab side does not (only the played def's motions
  are live). Raise it, measure, revert.
- **`--screenshot=` relative paths resolve against the Godot project dir (`CSVM/`), not the
  shell's cwd (measured)** — and `SavePng` into a missing directory fails while the
  "screenshot saved" line prints anyway (the save's error code is unchecked). Use absolute
  paths, or `../.scratch/...` from the project dir.
- **First `--debug-anim` sample after Play can read `HIDDEN` (measured, not a bug).** Entities
  the unplaced-entity watch switched off (C1's four train cars, parked at the origin) come back
  through the 1 Hz `RestorePlacedEntities` poll once the played def moves them.
