# Reading the original's cockpit gauges out of video, frame by frame

**Question.** The original's sim clock runs at the wrong speed on modern hardware, which
invalidates every stopwatch measurement the flight model was tuned against. Can the
aircraft's state be recovered from recorded cockpit footage precisely enough to re-derive
the flight constants, and can the clock error itself be measured rather than assumed?

**Answer: the state, yes — the clock, only up to one factor.** Altitude decodes to ~0.5 ft
and airspeed to ~0.3 mph per frame. What the footage measures directly is the *product*
λ·k = 1.39 ± 0.02, where k is the clock factor and λ any scale error in the altimeter's own
feet. Splitting the two needs one external fact; a spawn-point altitude read off the mission
data does it, and no amount of further flying will.

Source clips: `OriginalScreenshots/Videos/FlightModel/` (git-ignored, nine Bloodhawk
manoeuvres, cockpit view, night, 2560×720 with the game pillarboxed into x 640..1919). The
panel sits at **identical pixels in every clip**, so the first six pool into one median frame
(2,529 frames) in which the needles average away; that median is the reference everything is
differenced against. Six clips are pixel-locked; the two dives shake (±5 px), tracked
sub-pixel per dial.

## What it measured

Everything below is the **Bloodhawk**, the only airframe on video. What CSVM did with these
numbers is in `docs/HISTORY.md`; what is still open is in `backlog.md`.

**The clock: k = 1.390 ± 0.021, and the altimeter is honest (λ = 1.000 ± 0.004).** The dial
identity measures the *product* λ·k, where λ is any scale error in the altimeter's feet:

| measurement | λ·k | note |
|---|---|---|
| loop, vertical climb crossing | **1.3784–1.3846** | stable over 4 window sizes |
| loop, vertical descent crossing | **1.3701–1.3924** | stable over 4 window sizes |
| dive 2, near-vertical | 1.400–1.437 | fitted γ rotation only −5…−13 °/s ⇒ a steady dive |
| dive 1 plateau | 1.2926 ± 0.0007 | ⇒ dive 1 was **~70°, not vertical** |

`|dh/dt| ≤ V` always, so `K = |dh/dt| / V` is a hard lower bound on k needing no attitude
measurement at all, and it becomes an equality wherever the flight path is vertical — which a
loop passes through twice. Dive 1 cannot have been vertical: that would need `sin γ > 1` given
the loop. Dive 2, recorded specifically as a vertical dive, agrees with the loop to 2% in a
**different session**, so the clock was stable across both.

Splitting λ·k needs one fact from outside the flying, and four spawn-point altitudes supply it
— 559 ft (dogfight), then 1315 / 495 / 1140 ft (Stunt Flying), each against the spawn `y` in the
mission's own zrdr. Regression through the origin gives **λ = 0.9999 ± 0.0041**, residual sd
6.7 ft. The discriminator is not the residual size but its *shape*: at λ = 1 the residuals are
±8 ft, sign-random and flat in percentage terms (+1.4, +0.2, +0.6, −0.7%) — the few metres of
climb or sink expected because the aircraft spawns nose-off-level and the shot cannot be taken
on the first frame. At λ = 0.826 they are all positive, all ≈17–18%, and correlate with altitude
at +1.00: the unmistakable signature of a scale error. So the altimeter is a straight feet
conversion of a metric world, and **the original ran ~39% fast in these recordings**.

Corroboration from outside the altimeter entirely: video wall-times × k reproduce the user's
stopwatch runs from an *earlier* session — 360° roll 1.462 s → **2.05 s** (stopwatch 2 s),
rudder 360° 20.43 s → **28.6 s** (stopwatch 30 s), loop 360° 8.65 s → **12.1 s** (stopwatch 11 s
for the 90°-bank turn). The roll is the apples-to-apples one and involves no altimeter at all.

**Control rates.** Data-implied rates (`torque·recI/damp`) are pitch 44.62, yaw 22.92, roll
94.54 °/s; measured, in sim seconds at k = 1.39:

| axis | measured | note |
|---|---|---|
| roll, 360° | **2.05 s** | |
| pitch, sustained | **~33 °/s** | 30–37 across 120–280 mph |
| yaw, 360° at ~290 mph | **28.6 s** | a **verified** wings-level rudder turn (ADI bank within ±2.7°) |

**Pitch rate does not fall off with speed** — binned round the loop it is 37.9 / 33.7 / 30.7 /
36.5 °/sim-s over 120–160 / 160–200 / 200–240 / 240–280 mph, flat within the noise.

**Thrust and drag.**

| scenario | measured |
|---|---|
| level full-throttle equilibrium | 300.4 mph |
| full throttle, 150 → 290 mph | 2.685 wall s = **3.76 sim s** |
| terminal dive (γ ≈ 70°) | **355.2 ± 0.4 mph** = 1.182 × level max |
| 1/8 throttle equilibrium | **137.9 mph** (0.459 × fd) |
| 8/8 → 1/8, 290 → 150 mph | 5.03 wall s = **7.04 sim s** |
| zoom climb from 300 mph level, full pull | **+1635 ft**, bottoming at 104 mph in 10.5 sim s |
| level top speed vs altitude | flat ~300 mph from 714 m to 1909 m, then collapses |

The acceleration and the terminal dive fall out of **one** number: a max thrust acceleration of
**A ≈ 60 m/s²** reproduces the measured acceleration *and* predicts a 70.7° terminal dive of
1.175 × fd against the measured 1.182. So the drag *shape* is right and only the thrust scale
was ever wrong.

⚠ **A ≈ 60 m/s² is the transferable number; a `ThrustConst` back-derived from it is not.** That
conversion needs the plane's own stock engine power, and the Bloodhawk's `engine` is **11**
(Bloodhawk Lvl-2, 0.62) — not the level-1 row, 0.47. Reading the wrong tier inflates the
constant by 32%, and it survives every consistency check that only ever sees `A`.

**There is an altitude limit near 2065 m, mechanism unknown.** `Ceiling 2` is 43 s of repeated
attempts; five apexes:

| apex altitude | speed at apex |
|---|---|
| 2010 m | 283.3 mph |
| 2027 m | 262.9 mph |
| 2065 m | 183.1 mph |
| 2066 m | 233.7 mph |
| 2109 m (arriving at +281 ft/s) | 104.6 mph |

Apex altitude mostly trades smoothly against apex speed — the signature of a *performance*
limit, not a wall — but the 2065/2066 pair reaches the same altitude at 183 and 234 mph, which
a pure energy limit cannot do. Neither signature is clean. Level top speed is flat to 1909 m,
reads 283.5 mph near-level at 2009 m, and at 2066 m the aircraft holds level flight at only
~234 mph while still accelerating gently — so whatever happens is concentrated in
**1909–2066 m** and is invisible below it. With λ = 1 confirmed these are true altitudes, and
they are 76–83% of the data's `flight_ceiling` 2500, so **the limit is not that constant**.

**`player.json` ships a physics block almost none of which is consumed** (units unverified;
found while chasing the clock, alongside the already-used `nom_gravity 20.0` and
`stall_mag 1.25`):

```
maxAOA 46.0        liftAOAs [5,9]      lift_accel_rate 0.75
highGs [9,15]      lowGs [-6,-9]       drag_factor 1.5     drag_fade_speed 40
groundblow_elev 400   groundblow_mag 10   ai_groundblow 0.5
turn_fade_in 10    turn_fade_out 50    high_speed_pitch_fade [1000,1001]
yaw_low_speed 0.0625  yaw_high_speed 0.17  yaw_fade_in 10  yaw_max 50  yaw_fade_out 400
crash: bounce_factor 0.6, armor/health_damage_range [50,300]
autohead_turn_time 0.75  autohead_turn_max 2.86  autohead_turn_min_pitch -3.0
```

⚠ **The design document's turn-rate ladder (30/45/60/75/90 °/s) is the *velocity-vector* turn
rate, and it maps to pitch** — not a body-axis rate. Measured sustained pitch ~34 °/sim-s sits
between the ladder's 30 and 45 steps, consistent with the ladder being pitch and the Bloodhawk
not being top-of-ladder. Its shipped *roll* rate of 94.5 °/s landing on the ladder's top step is
a coincidence.

## Running it

From the repo root, in order (each writes into `.scratch/vidcal/cache/`):

```
python analysis/video-flight-calibration/checkclip.py   # GATE a new clip before decoding it
python analysis/video-flight-calibration/extract.py     # video -> panel-strip .npy cache
python analysis/video-flight-calibration/shake.py       # global panel translation per frame
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import fitdial"
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import run2;run2.main()"
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import anchor;[anchor.resolve(s) for s in ['pitch','roll','yaw','dive','accel','decel']]"
```

PTS timestamps come from the bundled ffmpeg — **required**, see the VFR trap below:

```
ffmpeg -i <clip> -vf showinfo -f null - 2>&1 | grep -oE "pts_time:[0-9.]+"
```

Needs `imageio_ffmpeg`, `numpy`, `scipy`, `pillow`, `matplotlib`. No game data is read
except the extracted dial textures (`extracted/C1/texture/{altimeter,speedometer}.png`)
and `extracted/zrdr/{vehicle,player,engines}.zrd.json`.

## How each instrument works

**Dial geometry is fitted, not guessed** (`fitdial.py`). The panel is a 3D model under a
fixed camera, so every flat dial reaches the screen through a homography; over ~100 px an
affine is indistinguishable from it. Fitting the *original's own dial texture* onto the
pooled median frame by weighted NCC (weight `1/(1+std)`, so needle-swept pixels do not
vote) reaches NCC 0.978/0.981 and lands the dial in its texture's own frame, where the
face art defines the scale exactly. The altimeter and speedometer fits come out
mirror-symmetric in shear (+6.54 / −6.67 px), which is what symmetric placement about the
screen centre must give — a free check that the fit found real geometry.

**Value scales come from the textures' tick rings, not from assumption.** Polar-unwrapping
`speedometer.png` and `altimeter.png` puts minor ticks 7.2° apart on both faces, with
majors every 36°. So the speedometer is exactly **0.72°/mph** and the altimeter **36° per
numeral** — 360° per 1,000 ft on the long needle, per 10,000 ft on the short one. These
match the constants `GaugeCluster` already uses.

**Needles are read as ridges in a high-passed angular profile** (`reader2.py`). Each frame
is illumination-matched to the pooled median inside the dial (robust gain/offset) before
differencing, because night-scene panel lighting swings with attitude. The angular profile
is then high-passed over ±25°: a needle is a ~7° ridge, while a lit `LOW ALT` or `STALL`
window is a broad bright blob sitting in the same radial band, and without the high-pass
the blob wins.

**Altitude uses the long needle only, anchored once** (`anchor.py`). The long needle gives
altitude mod 1,000 ft continuously; the remaining unknown is which 1,000 ft band the clip
starts in — ten candidates, because the band must leave the long needle's own reading
intact. Scoring all ten against the short needle's angular profile across every frame at
once resolved every clip with a ≥1.97× margin. Re-picking the band frame by frame
instead produces 1,000 ft jumps (the short needle is fat, sits in a thin annulus and gets
crossed by the long needle's shaft, so single frames are unreliable while hundreds are not).

**Attitude, when the dials cannot give it.** The artificial horizon is a gyro ball, so its
painted horizon is a great circle and the *area* fraction either side of it is a known
function of the cut distance — robust to the ball's coarse tessellation and to the fixed
aircraft symbol painted over it (`adi.py`). Because `sin(180° − θ) = sin(θ)`, vertical
speed needs only the sky fraction and the bank state never has to be untangled. The
centroid *direction* of the sky region gives bank, which is what measured the 360° roll.

## Verified precision

| quantity | precision |
|---|---|
| altitude, clean clips | 0.10–0.51 ft (sd of the second difference) |
| altitude, shaking dive clip | 5.6 ft |
| airspeed | 0.05–0.96 mph |
| dial registration | sub-pixel (fit residual invisible in a ×4 overlay) |
| altitude over a 5 s straight-line fit | residual sd 6.7 ft on the dive, 2.0 ft on the loop |

The decoded needle angle drawn back over the frame lands on the needle, and the derived
0/100/200/300 rays land on the major ticks — the end-to-end check that matters.

## ⚠ Traps this measurement walked into

**The capture is variable-frame-rate. Use PTS, never `1/fps`.** The clips report ~30.1 fps
but the real frame intervals are 33.3 ms mostly, with 16.7 ms and 50.0 ms intervals mixed
in. Cumulative time agrees with the nominal rate, so *durations* survive a uniform time
axis, but individual frame intervals are wrong by up to ±50% and any frame-to-frame
derivative inherits that.

**Never multiply both images by the same binary mask inside a phase correlation.** Masking
the dial neighbourhood to "pixels that hold still" put a spurious peak at zero lag: the
mask pattern correlates with itself and outvotes the weak underlying signal. The first pass
therefore reported *no shake at all* on the one clip that visibly shakes, and altitude came
out with 1,613 ft of second-difference noise. Gradient-based (Lucas–Kanade) registration
with a gain/offset term, weighting pixels by stillness × gradient, found the ±5.8 px shake
and dropped the noise to 5.6 ft — a 300× improvement. A masked phase correlation *looks*
like it worked: it returns a confident sub-pixel answer of zero.

**Two-region shift agreement is not a rotation test unless both regions have texture.** The
right-hand panel patch disagreed with the left by up to 4.8 px in y, which reads as panel
rotation — but its correlation peak was 0.07 against the left's 0.30. It was a tracking
failure on a dark, featureless region, not rotation.

**A peak found by differentiating a smoothed signal is a smoothing artifact.** Estimating
the clock factor from `max |dh/dt| / V` gave 1.47 at a 11-frame Savitzky–Golay window and
1.38 at 31 frames, and a parabola fitted to the peak jumped between 1.36 and 1.51. Fitting
a low-order polynomial to *altitude* over a window and taking the extremum of its
derivative gave 1.363–1.377 across every window size and degree tried. Altitude is the
precise quantity; differentiate it as little as possible.

**Know which unknown a test actually constrains, and how hard it leans on your model.**
Reconciling level acceleration against terminal dive speed looks like it measures the clock.
It does not — it measures the *altimeter scale* λ, and only given an assumed drag law. Worse,
that dependence is strong: our quadratic/linear blend yields λ ≈ 1.00, pure quadratic 0.92,
**pure cubic 0.83**. Quoting a single number from it without the drag-law sweep hides a 20%
fork in every downstream rate.

**A "ballistic" reference point is only ballistic if nothing else is acting on it.** The
zoom-stall apex was used to split λ from k via `nom_gravity`, and it fitted a parabola to
0.22 ft — convincingly free-flight. A later, longer clip then showed the aircraft cannot
exceed ~2066 m, and that apex sat at 2109 m: it was *above* the limit, so whatever enforces
the limit was pushing on it the whole time. The clean parabola was not evidence of absence.
Before trusting a ballistic segment, establish independently that the aircraft was in free
flight there.

**Do not read a mechanism off two hand-picked points.** Two climb attempts reached the same
altitude (2065 and 2066 m) at very different speeds (183 and 234 mph), which reads as a hard
wall. Across all five apexes in the clip, altitude instead trades smoothly against speed —
the signature of a performance limit. Both patterns are in the same data; whichever two
points you quote decides the answer.

**Check a consistency test for degeneracy before believing it.** Reconciling the measured
level acceleration with the measured terminal dive speed through the flight model's own
drag shape agrees to 0.6% — and does so for *every* clock factor, residual constant to five
decimals. The thrust scale needed goes as `1/k` and the dive angle's sine also goes as
`1/k`, so the ratio that sets the terminal speed is clock-independent by construction. The
test validates the drag *shape* and says nothing whatever about the clock, though it
presents as a sharp confirmation of whatever clock value you fed it.

**The pooled median is only usable because the panel is pixel-locked across clips.** Phase
correlation of all six clips' reference frames gives dx = dy = 0, so 2,529 frames pool into
one median in which the needles vanish. Re-check this before adding footage from another
session, seat position or resolution — the whole pipeline rests on it.

**Auto head turn invalidates a clip; check for it before decoding.** Two later takes came in
with the original's auto head turn enabled, which rotates the camera rather than translating
it, so the panel's *perspective* changes: the altimeter and speedometer shift by equal
amounts vertically but **opposite** amounts horizontally (ALT +4 px while MPH −4 px), since
their foreshortening moves in opposite directions. Registration is translation-only per dial,
so this cannot be corrected without solving a per-frame homography. **The cheap test is
exactly the diagnostic above** — register the two dials independently against the pooled
median and difference their offsets. Equal offsets mean translation and the clip is usable;
equal-in-y-but-opposite-in-x means head turn and the clip must be re-recorded. Correlation
peaks also collapse (~0.70 → 0.03), so low confidence alone is a warning. The governing
constants are `player.json`'s `autohead_turn_time` / `autohead_turn_max` /
`autohead_turn_min_pitch`.

## What the ADI cannot do

It saturates before vertical: sky fraction runs 0.05–0.73 over a full loop while the
physics-derived `k·sin γ` runs ±1.4. It also shows hysteresis against vertical speed
(expected — the ball shows pitch attitude, the vertical speed follows the flight path, and
angle of attack separates them at low speed). So the ADI gives bank well and pitch only in
its unsaturated middle; it cannot measure the vertical crossings of a loop.

## Capture spec, for any further recording

- **Cockpit view, clear air, fixed throttle, and name the aircraft in the filename.**
- **Auto head turn must be OFF** — it invalidates a clip outright, see the trap above.
  `checkclip.py` gates a clip on exactly that test before anything is decoded.
- **Prefer a constant-frame-rate recording** if the recorder offers one; the pipeline reads real
  PTS either way.
- **Highest bitrate available.** Needle legibility was never the limiting factor even at the
  effective 1280×720 — the dials are only ~106 px across and still decode to half a foot.
- **Hold a manoeuvre to its steady state.** Every number above that pinned a constant came from
  a plateau; the transients only ever bounded things.

| # | Manoeuvre | Status |
|---|---|---|
| 1 | Vertical dive to terminal | ✅ `Dive 2` is vertical; `Dive` is ~70° but holds a 5 s plateau |
| 2 | Full loop from level | ✅ this is what pinned the clock |
| 3 | Sustained level turn, max pull | ❌ owed — the yaw clip is a wings-level *rudder* turn, so a banked max-pull turn is still missing (and it is the one that would measure induced drag) |
| 4 | 360° aileron roll | ✅ |
| 5 | Low pass along a canyon wall | ❌ owed — the only source for ground blow |
| 6 | Level top speed at 5500 / 6000 / 6500 / 6800 ft | ❌ owed — settles what enforces the ~2065 m limit |
