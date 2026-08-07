# Gun-wobble shake: what `fire_bullet`'s `magnitude_factor` multiplies

**Question** (`BL-266`): the authored `fire_bullet` oscillator (`shakes.zrd.json`: frequency 15,
damp 12.5, sawtooth, `magnitude_factor` 7e-5) scales *some* per-shot quantity — caliber, damage,
or muzzle velocity. Which one, and in what units?

**Answer: caliber, in radians of roll.** `magnitude = 7e-5 × CALIBER` — for the footage's 40-cal
slugs, 2.80e-3 rad (0.160°), against a measured roll oscillation of 2.8e-3 rad RMS / ~4.0e-3 rad
peak. The other candidates miss by an order of magnitude in each direction: damage (4.5) →
3.15e-4 rad, ~9× under; velocity (900) → 6.3e-2 rad, ~16× over. The candidate separation is so
wide that the peak-vs-RMS ambiguity (×√2–2) cannot flip the verdict — this is a measured
discrimination, not a one-coincidence match.

## Source clip

`OriginalScreenshots/Videos/Gun Wobble and animation.mp4` (12.1 s, 30.24 fps, 2560×720 with the
game pillarboxed at x 640..1919, same geometry as the FlightModel clips). Dead-astern chase view
of the red Bloodhawk in level flight; HUD shows "GUNS 40 SLUG", counter 2371→2325. Guns fire
t≈3.7–8.2 s (frames 113–247), bracketed by clean idle stretches.

**The dead-astern view is what makes the roll measurement calibration-free:** the roll axis
points at the camera, so image-plane rotation *is* world roll angle — no FOV, distance, or
projection model enters.

## Method

- `probe.py` — metadata, luminance trace, 1 fps contact sheet.
- `measure.py` — red-dominance-mask centroid/axis + ammo-counter redraw detection. The firing
  window comes from the counter ROI's frame-difference energy (sustained redraws f113–f247; the
  isolated single-frame blips every ~29 frames are a periodic redraw artifact, not fire).
  ⚠ The red-mask centroid is **not** a usable motion instrument here — it reported 10.5 px/frame
  jumps at ~6.5 Hz that patch registration refutes; mask flicker at the marginal dark-red wing
  pixels manufactures the signal.
- `track.py` / `wings.py` — frame-to-frame windowed phase correlation (Hann only, no binary
  mask — per `docs/verification.md`) of fuselage, tail, and both wing patches; parabolic
  sub-pixel peak.
- `wep40.py` — the candidate quantities for `wep_40` from `extracted/zrdr/weapons.zrd.json`.

## Measurements

| patch, axis | fire RMS (px/frame) | idle RMS | ratio |
|---|---|---|---|
| fuselage dx | 0.277 | 0.010 | ×28 |
| fuselage dy | 0.256 | 0.019 | ×13 |
| left wing dy | 1.118 | 0.192 | ×5.8 |
| right wing dy | 1.199 | 0.082 | ×15 |
| tail dx/dy | 0.52/0.54 | 0.07/0.21 | ×8/×2.5 |

Left-vs-right wing dy correlation in the fire window: **−0.86** — the wings move in opposition:
the wobble is **roll-dominated**, with a small co-moving lateral component (dx +0.61). Wing
patches sit ±205 px from the roll centre, so 1.16 px/frame RMS ⇒ roll step 5.7e-3 rad/frame.
A ~15 Hz tone sampled at 30.24 fps sits at Nyquist (step ≈ 2× displacement), giving displacement
≈ 2.8e-3 rad RMS, ~4.0e-3 rad peak.

Idle floor: the chase plane is pinned to ~0.01 px/frame — level-flight `high_speed` rattle is
invisible at this speed, consistent with its `min_speed` gate / magnitude quotient keeping cruise
quiet.

## Engine A/B (`engine_check.py`)

The same instrument run over the engine's own frames (`RunProbe --stage=empty
--plane=player_bhawk --fire --shots=24`, dead-astern chase, vs a no-fire baseline) after the
`PlaneShake` wiring landed:

| patch, axis | fire RMS (px/frame) | idle RMS | ratio |
|---|---|---|---|
| left wing dy | 0.032 | 0.004 | ×8.9 |
| right wing dy | 0.049 | 0.003 | ×17.9 |

Left-vs-right wing dy correlation **−0.96** — the same roll signature as the original.
Per-frame amplitude reads ~2–4× under the original clip's after normalising lever arm and
frame rate; known contributors: the engine kicks at the authored `FIRE_RATE` 8/s while the
original clip's counter ran ~12–13 rounds/s (higher envelope refresh), and the 60 fps
render-interpolated pose smooths a near-Nyquist buzz more than the original's 30 fps capture.
Whether the residual matters is a feel call — `PT-44` judges it at the controls against
`Gun Wobble and animation.mp4`.

## Caveats

- One plane (Bloodhawk), one gun (40-cal slug): plane-model/weight factors are unmeasured. A
  second caliber on the same plane, and the same gun on a light vs heavy plane, would confirm the
  pure-caliber law (owed capture, see `playtest.md`).
- The fire-window spectrum peaks near 10.4 Hz, not 15: a damped sawtooth re-excited per shot
  (~12–13 rounds/s from the counter) and sampled at 30 fps does not yield a clean oscillator
  line. Frequency comes authored regardless; amplitude was the question.
- Counter-derived fire rate (~12–13 rounds/s) vs authored `FIRE_RATE` 8.0 is unreconciled
  (two guns at 8/s would be 16/s); not needed for this decode.
