# Gun-wobble shake: what `fire_bullet`'s `magnitude_factor` multiplies

**Question** (`BL-266`): the authored `fire_bullet` oscillator (`shakes.zrd.json`: frequency 15,
damp 12.5, sawtooth, `magnitude_factor` 7e-5) scales *some* per-shot quantity — caliber, damage,
or muzzle velocity. Which one, and in what units?

**Answer: caliber, in radians of roll.** `magnitude = 7e-5 × CALIBER` — for the footage's 40-cal
slugs, 2.80e-3 rad (0.160°), against a measured roll oscillation of 2.8e-3 rad RMS / ~4.0e-3 rad
peak. The other candidates miss by an order of magnitude in each direction: damage (4.5) →
3.15e-4 rad, ~9× under; velocity (900) → 6.3e-2 rad, ~16× over. The candidate separation is so
wide that the peak-vs-RMS ambiguity (×√2–2) cannot flip the verdict.

**The `× CALIBER` law is now edge-traced in `crimson.exe`, not just clip-fitted.** The consume
site for `fire_bullet.magnitude_factor` is the plane per-tick firing-feedback loop `FUN_004b6820`
(disasm at `0x004b6e20`): it `FILD`s the weapon-ext `CALIBER` (signed integer at `weapon+0x210`
→ `+0x10`, built by `FUN_004ba6f0`) and `FMUL`s it against `*(camera + 0x3c)` = `magnitude_factor`
(loaded raw into the camera object by the `shakes.zrd` parser `FUN_0042bc10`; camera =
`DAT_0064ef78`). The product is the per-shot shake input = `7e-5 × 40 = 2.80e-3`. `FILD`
(integer load) is decisive: it is CALIBER (integer 40), not damage (4.5 float) nor velocity (900).
Chain: `shakes.zrd → camera+0x3c → (fire tick) CALIBER(weapon_ext+0x10) × it → FUN_0042c070 →
FUN_0042be10` (camera shake kick, see below).
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

⚠ **This model is the remake's `PlaneShake.cs` oscillator** (that is why it validates against
`PlaneShakeTests`). The binary trace shows the ORIGINAL feeds `2.80e-3` through a *camera-shake
component* first, and the per-shot kick law is now closed-form and binary-derived:
`FUN_0042be10(this=camera+0x18)` scales by `this[1]` = **2.0** (camera-ctor `FUN_0042bab0`, dword
7) and the waveform factor, whose selector `*this == 0` takes the **`6.2832`** (sine) branch —
**not** the `4.0` sawtooth branch — then kicks the three accumulators:
`fVar1 = mag × 2.0 × 6.2832 = 3.518e-2`, `Δroll/pitch = (rand01−0.5) × fVar1 × 1.2`,
`Δyaw = (rand01−0.5) × fVar1 × 2.5`. For wep40 (`mag = 2.80e-3`): **Δroll is uniform in
±2.11e-2 rad per shot**, Δyaw in ±4.40e-2. So the original's per-shot kick to its roll oscillator
is `(rand01−0.5) × 2.80e-3 × 2.0 × 6.2832 × 1.2` — an order of magnitude larger than the raw law.
⚠ `FUN_0042be10` only *kicks* the accumulator at `camera+0x24/+0x28/+0x2c` (random-delta walk,
confirmed: it returns right after the three adds, with no decay/oscillation/render); the visible
wobble is a separate camera function. **That consumer has now been static-traced to a negative**
(see below). The two gangs of scalars (remake `PlaneShake` gain vs original
camera-shake-component gain ×12.566) are **not reconciled**; the 0.284/0.20-px figure is the
remake's render of a `2.80e-3` kick, and comparing it to the original demands the original's true
kick. Decode persisted: `.scratch/magnitude-factor-binary-decode.txt`.

The remaining static trace — **the `camera+0x24` consumer** — is now conclusively *not* in the
camera update/render path: mode dispatcher (`FUN_0042c5c0`), all seven mode drivers
(`FUN_0042c7f0/0042ca50/0042cb70/0042ce00/0042d980/0042cf10/0042db40`), the post-mode driver
`FUN_0042ba70`, transform-apply `FUN_0042c670`, and the z-class orientation getters/setters
(`FUN_004d2890/004d2490/004d2680`) were each disassembled and **none read `camera+0x24`**. The
committed camera orientation is the `camera+0x0c..+0x14` triplet, which the traced path never
folds `camera+0x24` into (the writer, `FUN_0042c070`, is confirmed `this = camera + index*0x2c +
0x18`, so index-0 accumulators are exactly `camera+0x24/+0x28/+0x2c`). Byte-pattern sweeps for
`fld [reg+0x24/+0x28/+0x2c]` find **no hit in the 0x0042 camera region** (only non-camera
subsystems at `0x0041c4xx`/`0x004269xx`/`0x0053xxx`).

So the random-walk accumulators have no traced reader in the camera update path. Either a
render-layer function (non-`0x0042`; SIB-form loads were not swept exhaustively) reads them, or
they are effectively **dead/near-unused** in this build — the remake's actual gun-shake
(`PlaneShake.cs`: deterministic damped sawtooth, no RNG) is a *different mechanism* from the
original's random-walk camera accumulator. This strengthens the structural-mismatch view: the ~40×
per-shot-kick gap is a mechanism difference (original random-walk accumulator vs remake damped
sawtooth), not a render-pipeline gain loss.

## High-speed (`high_speed`) shares the SAME random-walk accumulator as the gun

(2026-08-19.) The `camera+0x24` negative above is the **fire** component only. The `high_speed`
oscillator writes a **different** component block, through the **identical** write function
`FUN_0042be10` — so the two sources are the same random-walk mechanism, differing only by
component index and magnitude law.

The consumer is in `FUN_0048c470` (the per-frame player-plane updater; sole caller
`FUN_0048e580`, the tick velocity/position integrator that also computes `plane[0x24d]` =
SQRT of squared velocity). Inside the `plane == player` (`DAT_0071c298`) branch:

```c
min = *(camera+0xec) * plane[0x19a];      // min_speed (authored 1.0) × rated-max scale
if (min < plane[0x24d]) {                  // overspeed gate: current speed > min_speed×max
    mag = (plane[0x24d] − min)
        / (*(camera+0xf0) * plane[0x19a]); // (speedRatio − min_speed)/magnitude_quotient (70.0)
    FUN_0042c070(4, mag);                  // SAME random-walk accumulator as the gun
}
```

- `camera+0xec` = `high_speed.min_speed`, `camera+0xf0` = `high_speed.magnitude_quotient`
  (both loaded raw by the `shakes.zrd` parser `FUN_0042bc10`, camera = `DAT_0064ef78`).
- `FUN_0042c070(4, mag)` is the exact dispatcher the fire path uses, with component index 4:
  `this = camera + 4*0x2c + 0x18 = camera + 0xc8` (block layout from the camera ctor
  `FUN_0042bab0`: `[0]=0` waveform → the `6.2832` sine branch, `[1]=2.0` gain).
- `FUN_0042be10(camera+0xc8, mag)` then kicks the **block-4 accumulators at
  `camera+0xd4/+0xd8/+0xdc`** (roll/pitch ×1.2, yaw ×2.5) — the same per-axis random walk as fire,
  on all **three** axes (the user-visible high-speed wobble is multi-axis, matching observation,
  not roll-only).
- Fire (`FUN_0042c070(0, …)`) and high_speed (`FUN_0042c070(4, …)`) are thus the **same algorithm**;
  the differences are only the component block (0 vs 4) and the magnitude law (`magnitude_factor×
  CALIBER` per shot vs `(speedRatio−min_speed)/magnitude_quotient` per frame).

**Consequences:**

1. **The original's `high_speed` is a random-walk accumulator, NOT the remake's damped sawtooth.**
   The remake's `PlaneShake._speed` path (deterministic `Target → Amp → sawtooth`) is a *different
   mechanism* from the original — this is the root cause of `BL-266(d)`'s "overspeed rattle ~6×
   muted." The fidelity fix is to drive `_speed` through a second `RandomWalkKick` accumulator (the
   `_fire` clone), fed by the already-correct excess-over-gate `SetSpeedRatio` law.
2. **High-speed re-kicks every frame** (the updater runs each physics tick) while fire kicks once
   per shot at 8/s, so the visible dive walk is sustained accumulation — why terminal-dive wobble
   "looks about the same" regardless of overspeed depth and reads at least as strong as the guns.
3. **The `camera+0x24` negative does NOT generalise to high_speed.** The high_speed accumulator
   lives at `camera+0xd4/+0xd8/+0xdc`, a different address than the searched `camera+0x24`. Since
   high-speed visibly wobbles in the original clips through this exact `FUN_0042be10` writer, the
   mechanism is demonstrably live — the earlier "near-dead" reading for `camera+0x24` is at minimum
   unproven for the shared mechanism and the high_speed block's reader was never searched. (The
   decoded high_speed accumulator offsets are recorded here so a future sweep can look for the
   `camera+0xd4/+0xd8/+0xdc` reader.)

No live instrument is required — every step above is a static trace from the open `crimson.exe`
(project `CSVMCrimsonExe`): disasm of `FUN_0042c070` (index→block address math), `FUN_0048c470`
(the high_speed consumer), `FUN_0048e580` (per-frame caller), and the camera ctor `FUN_0042bab0`
(block initialisation).

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
- **The `7e-5 × caliber` multiplicate is confirmed, but its downstream gain is not yet
  reconciled.** The binary leaves the law intact but routes `2.80e-3` through the camera-shake
  component gain (×2.0) and waveform factor (×6.2832, the `*this==0` sine branch) in
  `FUN_0042be10`, giving a closed-form per-shot `Δroll` uniform in ±2.11e-2 rad (see the
  amplitude section). The remake's `PlaneShake.cs` (whose render this doc models) applies a
  different gain, so remake-vs-original amplitude equality is an open question. The `camera+0x24`
  consumer (decay/oscillator that turns the random walk into wobble) is **not in the traced camera
  update/render path** — all mode drivers, the transform-apply, and the orientation getter/setters
  are ruled out, and byte-pattern sweeps find no `fld [camera-region+0x24]` reader. ⚠ **That
  negative is the fire block only. The new `high_speed` trace (see the section above) proves the
  shared `FUN_0042be10` writer is LIVE** — the original's `high_speed` drives the identical random-walk
  accumulator (component index 4, at `camera+0xd4/+0xd8/+0xdc`, not the searched `camera+0x24`) and
  visibly wobbles in the clips, so the mechanism is demonstrably not dead; the `camera+0x24` negative
  does not generalise and the high_speed block's reader was simply never searched. Either way the
  original's shake is a random-walk accumulator (both sources), while the remake's `PlaneShake.cs`
  `_speed` path (deterministic damped sawtooth, no RNG) is a *different mechanism* — that structural
  mismatch is the root of `BL-266(d)`'s muted dive. No live instrument is needed for any of this —
  every step is a static trace.
