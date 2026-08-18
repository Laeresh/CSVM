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
⚠ The 2.80e-3 rad is a *rendered* measurement of the clip; it is **not** the oscillator's kick
amplitude (a kick of that size renders ~0.28× itself at 8/s). It discriminates `7e-5 × CALIBER`
from competing laws, but reading it as the engine's intended per-kick roll is the conflation
`BL-266(a)` tracks — see the amplitude sections below.

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
**Per-frame amplitude reads ~4–5× UNDER what the wired law should render** (see the
analysis section below), and the two attributions the table once floated are both TESTED
NEGATIVE — the residual is **not** a rate shortfall and **not** a pose-interpolation loss:
- *Rate:* the original fires ONE round per fire-tick at the authored `FIRE_RATE` (8.0 for
  `wep_40`; `docs/org/weaponFire.md`); the clip's "12–13/s" was a redraw-window artifact.
  The engine probe already fired at 8/s, so the rate was never a confounder.
- *Render interpolation:* the shake pivot is a child of the `FlightController`, its roll
  written once per 60 Hz physics tick (`FlightController.cs:1302-1303`), and the plane's own
  manual pose interpolation (`_renderPose`, `:1388`) does **not** touch the pivot. Godot auto
  physics interpolation is OFF (`CSVM/project.godot`, 35 lines, no `physics_interpolation`
  entry; defaults OFF). A 15 Hz sawtooth at 60 Hz sampling is ~3.5× above Nyquist, so the pivot
  renders *stepped* — there is no smoothing and no amplitude loss from the rendering chain.

## Where the shortfall actually comes from: a law-derivation conflation

Model the engine's own oscillator (validated against `PlaneShakeTests`'s single-kick bound:
one kick of envelope `E` peaks at `0.435·E`, exactly the test's `0.25–1.001` window). At the
real fire rate the sustained rendered roll is **`Amp × sawtooth(phase) × envelope`**, and because
the sawtooth is rarely at ±1 and the envelope decays between kicks, the *rendered* RMS is only
**~0.28× the kick amplitude** (`magnitude_factor × caliber`) — phase-averaged steady state, std
±0.009 across start phases:

| render | fire/s | rendered RMS (rad) | ×kick | rendered peak (rad) | ×kick |
|---|---|---|---|---|---|
| 60 fps | **8.0** (engine's real config) | 7.9e-4 | **0.284** | 2.27e-3 | 0.81 |
| 60 fps | 12.5 (a higher fire rate, for scale) | 1.0e-3 | 0.36 | 2.27e-3 | 0.81 |
| 30 fps | 8.0 | 7.9e-4 | 0.284 | 2.27e-3 | 0.81 |

The corresponding per-frame roll STEP (what patch registration measures) at 8/s is
~9.5e-4 rad/frame ⇒ **~0.20 px/frame of wing motion** at the ±205 px lever.

The wire's `7e-5 × caliber` law (= kick amplitude **2.80e-3 rad**) was derived by setting it
EQUAL to the clip's *rendered* RMS (**2.80e-3 rad**). But those are different physical
quantities — rendered ≈ 0.28× kick at 8/s. So the engine never renders near the law's literal
value; it renders ~0.28× of it. To render the law's own number (2.80e-3) as RMS at 8/s the kick
would need to be 2.80e-3/0.284 =
**9.9e-3 rad** (magnitude_factor ≈ **2.5e-4**, ~3.5× the current 7e-5). If the fidelity target is
the clip's rendered 2.8e-3 RMS the needed factor is 2.8e-3/7.9e-4 ≈ **3.5×** — a **decode
correction** implied by the law's own geometry; it does not depend on the clip, which is only
the later judgment of how the result should look against the original.

## The decoded engine-render (no clip, no measurement)

Everything the engine renders is determined by decoded/authored constants and the oscillator's
own math — no pixel registration, no engine run, no clip:

| step in the chain | value | source |
|---|---|---|
| kick per shot `7e-5 × 40` | 2.80e-3 rad | authored `magnitude_factor` × `caliber` |
| rendered roll RMS @8/s | **~8e-4 rad** | oscillator: `Amp·sawtooth·envelope` |
| rendered roll peak @8/s | 2.27e-3 rad | (above) |
| rendered per-frame \|step\| | ~9.5e-4 rad/frame | roll sampled once per 60 Hz tick |
| ⇒ wing dy @ ±205 px lever | **~0.20 px/frame** | (above) × lever |

So, clip aside, the law renders **~0.28× its own literal number** (RMS/kick = 0.284), i.e.
**~0.20 px/frame** of wing motion under steady 8/s fire. That reduction is entirely oscillator
geometry (sawtooth duty × damp decay between kicks); the render chain adds nothing.

⚠ **Neither the engine A/B nor the clip is consistent with the one model, and they disagree in
opposite directions** — so the table's "2–4× under" is not a clean pipeline-loss claim:

| measurement | per-frame step | vs model@8/s |
|---|---|---|
| clip raw (`track.py`) | 5.7e-3 rad/fr | ~6.5× **high** (30 fps capture; its own "Nyquist ≈ 2×"
  correction was applied to a damped re-excited sawtooth, not a clean tone) |
| engine probe | 1.6–2.4e-4 rad/fr | ~4–5× **low** (fire signal 0.032–0.049 px sits barely above
  the 0.003–0.004 px idle floor; asymmetric wings ⇒ registration noise) |
| model (this law) | ~9.5e-4 rad/fr | reference |

The upshot: the shortfall is **not** a render-pipeline loss and **not** a wrong `magnitude_factor`
hiding a bug — the constant is the product of a documented conflation (rendered RMS typed as kick
amplitude). The clip is NOT needed to answer "does the engine render the law?" — the decoded
engine-render is ~0.20 px/frame (~0.28× kick), settled above. All the clip enters is the
separate fidelity question of how the original should look against that; the clip's own
2.8e-3 figure reads ~6.5× over the decoded model, which is a flag on the OLD clip measurement
(Nyquist-ish correction applied to a damped re-excited sawtooth), not on the law. `BL-266(a)`
carries it; `magnitude_factor` must not change until the fidelity target (matching the clip
look) is decided.

## Caveats

- One plane (Bloodhawk), one gun (40-cal slug): plane-model/weight factors are unmeasured. A
  second caliber on the same plane, and the same gun on a light vs heavy plane, would confirm the
  pure-caliber law (owed capture, see `playtest.md`).
- The fire-window spectrum peaks near 10.4 Hz, not 15: a damped sawtooth re-excited per shot
  and sampled at 30 fps does not yield a clean oscillator line. Frequency comes authored
  regardless; amplitude was the question.
- The counter-derived "12–13 rounds/s" is a **redraw-window artifact, not the true rate**:
  `crimson.exe` fires ONE round per fire-tick at the authored `FIRE_RATE` 8.0
  (`docs/org/weaponFire.md`); the counter dropped 46 rounds at 8/s (~5.75 s) while the motion
  window only captured ~4.5 s, inflating the per-second reading. The two-guns-at-16/s guess is
  ruled out — one gun group, one round per tick.
- **The `magnitude` law is a rendered quantity, not a kick amplitude** — see the conflation
  section above: `7e-5 × caliber` was matched to the clip's rendered RMS, but a kick of that
  size renders ~0.28× of itself at 8/s. The decoded engine-render is ~0.20 px/frame regardless
  of the clip; the clip is only the eventual fidelity target. The clip's own ~6.5×-over-model
  reading is a flag on that old clip measurement, not on the law.
