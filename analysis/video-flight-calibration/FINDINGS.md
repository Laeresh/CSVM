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
| level full-throttle equilibrium | 300.4 mph (re-measured 298.96 ± 0.20 in the 08-03 session) |
| **sustained max-pull turn, full throttle** | **222.94 ± 1.77 mph**, held 15.9 sim s at 100° bank |
| full throttle, 150 → 290 mph | 2.685 wall s = **3.76 sim s** |
| terminal dive (γ ≈ 70°) | **355.2 ± 0.4 mph** = 1.182 × level max |
| 1/8 throttle equilibrium | **137.9 mph** (0.459 × fd) |
| 8/8 → 1/8, 290 → 150 mph | 5.03 wall s = **7.04 sim s** |
| zoom climb from 300 mph level, full pull | **+1635 ft**, bottoming at 104 mph in 10.5 sim s |
| level top speed vs altitude | flat ~300 mph from 714 m to **1988 m**, i.e. right up to the cap |

The acceleration and the terminal dive fall out of **one** number: a max thrust acceleration of
**A ≈ 60 m/s²** reproduces the measured acceleration *and* predicts a 70.7° terminal dive of
1.175 × fd against the measured 1.182. So the drag *shape* is right and only the thrust scale
was ever wrong.

⚠ **A ≈ 60 m/s² is the transferable number; a `ThrustConst` back-derived from it is not.** That
conversion needs the plane's own stock engine power, and the Bloodhawk's `engine` is **11**
(Bloodhawk Lvl-2, 0.62) — not the level-1 row, 0.47. Reading the wrong tier inflates the
constant by 32%, and it survives every consistency check that only ever sees `A`.

**The altitude limit is a hard clamp on altitude, measured 2026-08-03 from `CAP-03`.** Four
16:9 clips, Bloodhawk, C1B IA1, all gating rigid. Level full-throttle equilibrium is **299.71 ±
0.32 mph at 5492 ft**, **299.80 ± 0.56 at 6001 ft** and **300.00 ± 0.52 at 6520 ft** — no fade at
all below the cap. In `CAP-03 Stall at max Alt.mp4` the aircraft cruises level at **6570.4 ± 1.04
ft / 297.3 mph**, and pulling ~22° nose-up (ADI sin θ −0.13 → +0.24, against −0.135 at level)
gains **no altitude whatever**: 6571.6 ± 0.39 ft over the last 5 s while airspeed decays at 13.0
mph/sim-s to a fresh equilibrium of **173.74 ± 0.60 mph** with the stall window lit. Sub-foot
altitude at 22° AoA is a clamp, not an energy limit, and the "auto stall" is its consequence.
Earlier zoom attempts in the same clip overshoot ballistically to **6712 ft (2046 m)** and sag
back — which is what the five-apex reading below was seeing. Resting cap **6571.6 ft = 2003 m**;
whether it is global or per mission is untested (one mission flown).

The older reading, kept because its data is real and its trap still bites — `Ceiling 2` is 43 s of
repeated attempts; five apexes:

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
⚠ Every apex above ~2003 m in that list is now read as ballistic overshoot of the clamp, so the
"performance limit" signature was an artifact of measuring a clamp with zoom climbs.

**Knife-edge is a departure, not an equilibrium, and the stall break is gentle — measured
2026-08-04 from `CAP-05`.** Four 16:9 Bloodhawk clips, all gating rigid (dx corr +0.97 to +1.00).
Two knife-edge takes at very different speeds agree, which is what makes the shape trustworthy:

| clip | onset | at onset+3 s | +12 s | +24 s | +36 s |
|---|---|---|---|---|---|
| `CAP-05 2 Knife Edge` (143 mph) | bank +85° | nose −4.9°, sink 0.5 ft/s | −12.0°, 24 ft/s | −20.0°, 60 ft/s | −27.0°, 93 ft/s |
| `CAP-05 Knife Edge` (300 mph) | bank +90° | nose −7.3°, sink 13 ft/s | −15.0°, 72 ft/s | — | — |

The nose takes an immediate **≈4° step** at the roll-in (fitted intercepts −3.4° and −4.2°) and then
keeps sagging **linearly at 0.69 and 0.89 °/sim-s** (0.012–0.016 rad/sim-s) for as long as the clip
runs — it never finds a bound. The flight path follows about **5–8° behind the nose**, that lag
growing slowly (−4.8° at +3 s, −7.2° at +24 s, −8.3° at +36 s). The long take loses **1772 ft
(540 m) in 38.9 sim s** and is still steepening when the clip ends. Bank drifts +104° → +94° over
the same span.

⚠ **The decisive comparison is against `CAP-01` at the same bank.** `CAP-01` holds +100° of bank at
full back-stick, keeps altitude to 1.85 ft/sim-s of sink, and sweeps **18.95 °/sim-s** of heading.
`CAP-05` sits at +94…+104° at near-neutral stick and instead falls out of the sky while turning only
**0.68 and 1.13 °/sim-s** — 17–28× slower, tracked off the compass tape (peak median 0.998, and the
tape's own `S`→`SE`→`E` labels confirm ~24° between the 7 s and 31 s stills). Same bank, opposite
outcome: **whatever carries the original's banked turn is a function of pull/AoA, not of bank.**

**The stall break (`CAP-05 Stall 0% Thrust no input`).** Zero thrust, no input, wings level — the
compass turns **0.0°** over the whole 24.8 sim s, so there is no wing drop and no departure. The
nose sits rock-steady at **+4.2 ± 0.1°** through the entire deceleration and only begins to fall at
**76 mph = 0.25 fd**, reaching a minimum speed of **69.8 mph = 0.232 fd**. It then drops at
**3.38 °/sim-s (0.059 rad/sim-s)** from +4.1° to −21.2°, and *stops* at about **−22°** once speed
rebuilds past 0.40 fd — it does not chase world-down.

**Low-speed drag is 4–6× weaker than a `lerp(x², x, 0.35)` blend at `A` = 60 m/s².** The same
zero-thrust clip is the cleanest drag probe in the whole set — no thrust term to assume. Fitting
`dV/dt = −D(V) − g·sin γ·(C if climbing)` over its +5° climb and −15° dive:

| x = V/fd | D measured (m/s²) | our blend | ratio |
|---|---|---|---|
| 0.25 (75 mph) | 0.36 | 7.69 | 21× |
| 0.35 (105 mph) | 1.11 | 12.13 | 11× |
| 0.46 (138 mph) | 2.82 | 17.91 | 6.4× |
| 0.50 (150 mph) | 3.74 | 20.25 | 5.4× |

The *ratio* is robust: across every `(g, C)` pair the fit tolerates, `D(x = 0.5)` lands in
3.8–5.6 m/s² against our 20.25. The absolute deceleration is the model-free version — at 152.6 mph
in a +5° climb with the engine off the original loses **6.24 m/s²**, where our curve would take
21.6. This corroborates `BL-092`'s independent `x^2.67` from the 1/8-throttle equilibrium, by a
route with no thrust in it at all.

⚠ **The clip cannot split `g` from the climb-gravity scale `C`, and the fit that looks like it can
is degenerate.** A free 4-parameter fit runs `C` to its bound; holding `g` and refitting gives rms
0.218–0.280 m/s² flat over `g` = 17…25 m/s², so the residual chooses nothing. Along that valley
`C` = 1.18 at `g` = 17, **0.59 at `g` = 20**, 0.00 at `g` = 25. That `nom_gravity` 20.0 lands on
`C` ≈ 0.6, our own `ClimbGravityScale`, is a *consistency and not a measurement* — this is exactly
the degeneracy trap below. The descent leg also flies at ~+7° AoA against the climb leg's ~0°, so
any AoA-dependent drag is being absorbed into `g`/`C` as well.

**The stall warning is a blink-RATE ramp on a second, higher threshold — measured 2026-08-04 from
`CAP-06` plus the two `CAP-05` stall clips.** The `STALL` plate is the red window above the
speedometer hub, **game x 892–918, y 548–558**. It is read in *colour* from the video, not from the
luma cache, and the box is kept to the plate's left two-thirds:

⚠ **The first automated hunt for it found the NEEDLE instead.** Locating the lamp as "the pixels
that brighten when slow" lands on the speedometer needle sweeping into the low-speed part of the
dial — it produces a convincing monotone "ramp" that then *peaks and falls away* as the needle
sweeps past. This is the same needle-vs-window confusion `reader2.py` high-passes away, met from the
other side. Confirm any lamp box against a zoomed still before believing a waveform off it.

| what | measurement |
|---|---|
| threshold, four clips | **0.2989 / 0.2992 / 0.2994 / 0.2996 fd** — i.e. 0.30 fd |
| lit / unlit plate red | **211.0 ± 0.2** / **41.7 ± 0.2**, identical at every speed |
| duty cycle | **0.50** |
| half-period at threshold | 13.9 frames = 462 ms wall = **643 ms sim** |
| half-period at 0.15 fd | 6.4 frames = 213 ms wall = **296 ms sim** |
| hysteresis | none — on at 89.9/90.0 mph decelerating, 90.0/89.9 accelerating |

**Brightness is binary; only the rate ramps.** The two levels never take an intermediate value in any
of 105 pooled dwells spanning 43–90 mph. **Every dwell is an integer number of 33.37 ms game frames**
(lattice residual ≤ 8 ms), so the lamp toggles on a frame counter. Half-period ≈ `5.9·V(mph) − 62` ms
wall, or `5.1·V` through the origin — residual 36 ms, one frame, so those two forms are not
separable here and neither extrapolates below ~43 mph. The rate tracks *speed*, not time-since-onset:
in `CAP-06.mp4` the speed dips to 65 mph and recovers, and the blink rate falls and rises again with
it.

⚠ **The warning threshold and the stall itself are different numbers.** The lamp lights at 0.30 fd;
the nose does not drop until **0.25 fd**. Measured inside a single clip — in `CAP-05 Stall 0% Thrust
no input` the lamp lights at 7.96 sim s / 89.9 mph with the nose still held at +4.3°, and the break
comes at 10.60 sim s / 75.0 mph, so the warning **leads the stall by 2.64 sim s and 14.9 mph**. Any
model driving both cues off one threshold is wrong by construction.

**The weapon-gauge arrow sweeps at a constant 168.7 ± 1.6 °/sim-s, and takes the shortest way round
— measured 2026-08-04 from `CAP-18`.** Both weapon gauges, one rate: fitting only the interior of
each sweep (dropping 2 frames at each end, where a run detector keeps barely-moving frames) gives
**235.6 ± 1.8 °/wall-s** on the gun gauge and **233.7 ± 2.2** on the hardpoint gauge, 0.8% apart, so
**234.5 ± 2.3 °/wall-s = 168.7 ± 1.6 °/sim-s**. Each move also carries ~**70 ms wall of ramp**,
consistent across 90°/135°/180° moves — which is why an end-to-end fit reads lower (218–226) the more
end frames it includes. The interior is straight to **1.1°** over traverses of 90–180°: a
constant-rate traverse with about two frames of ease at each end, not a smoothstep.

| gauge | slots | end-to-end move times |
|---|---|---|
| guns | **4 at 90°** (green at 12 and 9 o'clock, red at 3 and 6) | 90° = 455 ± 15 ms wall = **633 ms sim** |
| hardpoints | **8 at 45°** | 135° = 634 ms wall = 881 ms sim; 180° = 856 ms wall = 1190 ms sim |

The slot counts are measured, not assumed: the hardpoint gauge's three resting angles (359.50°,
180.17°, 314.36°) fit a 45° lattice to **0.64° max / 0.44° mean**, against 14.4° for a 6-slot ring,
35.8° for 5 and 44.4° for 4.

**The needle routes by shortest way, not by walking the indices.** Numbering slots as the game does
— 0 at the top, increasing counterclockwise — the three rests are slots **0** (359.50°), **4**
(180.17°) and **1** (314.36°), and the clip repeats the cycle **0 → 4 → 1 → 0** four times. Two of
its three moves discriminate: 4 → 1 sweeps **+134.1°** clockwise where walking the indices the other
way round the ring would sweep 225°, and 1 → 0 sweeps **+45.2°** against 315°. ⚠ At the exact
**180°** antipode (0 → 4) it goes **counterclockwise**,
consistently over all four repetitions; that needs no special case, because the standard
shortest-path wrap `delta = ((target − current + 180) mod 360) − 180` returns −180 at exactly +180.
The ammo readout does *not* animate with the arrow — `40 SLUG`/`2400` → `30 SLUG`/`2800` flips on one
frame at the *start* of the sweep, so the value snaps and only the pointer tweens (`BL-184`).

⚠ **A dial fit that does not lock is worse than no fit, and this one nearly published a wrong
answer.** The hardpoint gauge first fitted at NCC **0.10** with its centre 9.5 px out, purely because
the start point it was given was 9.5 px out and its radius 5.6 px small — `fitdial` has no basin to
climb from there, exactly as its own docstring warns. On that centre the 8-slot lattice did not
appear at all (residuals 15–21° to every candidate ring) and the moves read −171°/+145°, which was
initially written up as "the hardpoint gauge is unmeasurable". It is not: re-seeded from a centre
measured two independent ways — the left column's x from the altimeter fit plus the right column's
row spacing (gungauge → speedometer, 92.85 px), and separately a pivot solved from the arrow's own
sweep lines — the fit locks at **NCC 0.7684** and lands **0.05 px** from that measurement. `hud.py`
now carries an `NCC_FLOOR` and `fitdial` stores each NCC beside its affine, so `load_affine` refuses
an unlocked fit instead of handing back a plausible-looking wrong centre.

**The chase camera's distance moves with speed AND with acceleration — measured 2026-08-04 from
`CAP-21`.** Four Bloodhawk chase takes driven by `analysis/capture-rigs/ThrottleSweep.ahk`: two 5 s
staircases 0/8↔8/8 and two single-jump runs, so the same speed range is traversed with completely
different throttle histories. With the camera untouched and the FOV fixed, the aircraft's **span in
frame** is 1/distance, and the speedometer in the same frame is the abscissa (`chasesize.py` keys
the airframe's crimson; `run2chase.py` supplies the speed). All four clips register to the chase
pooled median at dx = dy = 0 (peaks 0.73–0.80) and the two dials fit at NCC 0.990/0.990.

| what | measurement |
|---|---|
| span at plateau, 118.0 mph | **454.00 ± 0.23 px** |
| span at plateau, 296–299 mph | **436.00 / 436.50 / 435.50 / 437.50 px** — the four takes, 0.46% apart |
| speed term | `d(V)/d(0) = 1 + 5.65e-4·V(m/s)`; **+4.13%** over 118 → 299 mph |
| implied `dist_factor` (Bloodhawk `dist` 18.5) | **0.0105** fitted, 0.00945 from the two extreme plateaux — shipped value **0.01** |
| acceleration term | **+0.28% of `d` per (mph/sim-s)** = 0.105 units per (m/s²), corr −0.79…−0.85 in all four clips |
| peak excursion | **+15.2%** at +38 mph/sim-s; **−6.9%** at −33 mph/sim-s — ~4× the speed term, and far faster |
| relaxation once acceleration stops | **τ = 1.11 wall s = 1.55 sim s**, i.e. **0.65 /sim-s** |

So the shipped `dist_factor` 0.01 is confirmed to 5%, and **the game's internal speed unit is
metres per sim second** — the same metric world the altimeter established.

⚠ **The steady term and a camera position lag are the same shape, and only the magnitude separates
them.** A first-order world-space follower `ẋ_cam = c(x_target − x_cam)` leaves a steady lag `V/c`,
also linear in `V`. Fitting the observed coefficient as a lag needs **c = 96 /s**, which is no
constant in `camparam.zrd.json`; fitting it as `dist_factor` lands on a shipped 0.01. That also
**excludes `pos_catch_up` 2.0 as a world-space position lag** — at c = 2 the camera would trail
66 m at 300 mph, 3.6× the whole chase radius, and the footage shows nothing of the kind. The
original must smooth the *offset*, not the world position.

⚠ **The clip-to-clip systematic is ~1%, and it is the floor under every number here.** Two plateaux
that should agree do not: 118.0 mph reads 454.00 px and 134.9 mph reads 458.50 — bigger at the
higher speed. The span/height aspect differs 3% between those takes, so the viewing angle onto the
wing differed and the red-key edge moved with it. Within one take the plateau sd is 0.2–0.8 px
(0.05–0.18%), so the *shape* of a curve is far better determined than any cross-clip offset.

## Two HUDs, one pipeline

The original draws the instruments two ways and they are different measurement problems. `hud.py`
holds both layouts and every stage takes a HUD; **cockpit keeps the bare cache names, so every
artifact and published number above stays valid and reproducible without a re-run.**

| | cockpit | chase |
|---|---|---|
| where | one strip across the bottom | two screen-edge columns + top-centre compass |
| what it is | dials painted on a 3-D panel under a fixed camera | screen-space 2-D sprites |
| geometry | reaches the screen through a homography — real shear, mirror-symmetric (−6.54/+6.67 px) | **zero shear** (u^v angle 89.88°/89.96°, rotation 0.01°) |
| can it move | yes — screen shake translates it; auto head turn *rotates* it and invalidates the clip | no: pinned by construction, so `checkclip`'s head-turn gate has nothing to test |
| reference frame | median of 6 pixel-locked clips (2,529 frames) — needles average away | median of the chase clips (1,224 frames) — the HUD is the only thing holding still, so the **world** smears away instead |

Dial radius comes out **47.1 px on both** (chase altimeter 47.17, speedometer 47.15 against the
cockpit's 46), i.e. the same art at the same size, just placed differently. Chase NCC: altimeter
**0.9900**, speedometer **0.9898** — both clear the 0.98 bar the cockpit set; gun gauge 0.7360 (the
readout boxes are painted over the face and are not in the texture, and its geometry is confirmed
independently by the sweep pivot to 1.8 px); rockets **0.7684** (same reason, and only after being
re-seeded — see the unlocked-fit trap above); ADI 0.5753 (a gyro ball, as on the cockpit).

Two chase clips of *different capture geometry* — `CAP-18` at 2560×1440 and `CAP-10 3 3rd Person` at
2560×720 — both register to the chase median at **dx = dy = 0** (peaks 0.94 and 0.73), which is the
same pixel-lock property the cockpit set has and the thing that makes one reference frame usable
across sessions.

⚠ **A dial's role name is not its texture name.** The rockets gauge draws from `missilegauge.png`;
assuming role == filename is what made the first chase fit crash rather than mis-fit. `hud.py`
carries the texture per dial.

⚠ **Difference against a neighbouring frame, not the clip median, when a readout can change.** The
first arrow-pivot solve came out 10 px sideways: on a switch the readout text changes, so a
median-difference leaves a bright *horizontal* residual across the face for as long as the new value
is shown, and horizontal residual fits horizontal lines. Two frames a few apart carry the same text,
so it cancels and only the arrow moves — that version agrees with the texture fit to 1.8 px.

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

`CAP-02` ran to 14 takes and found **no measurable amplification near terrain on either axis** —
path-normal acceleration 0.91–1.03× free air, ADI body pitch rate 0.91–1.10× — including a
controlled pair flying the same held full-deflection loop at 1,113 ft and at 90 ft. An earlier pass
here claimed "1.95× max pull"; that was **wrong** (a borrowed yardstick) — the retraction and the
one outstanding anomaly are on `BL-095`. Do not re-derive any of it from the clips.

⚠ **The ADI saturates above roughly +25° nose-up** — sky fraction pins at 0.729–0.730, identically
in every `CAP-02` batch-4 clip. Only the first ~0.7 s of a hard pull is readable for pitch rate, so
design the comparison to live in that window (a pull *from level flight* does; a pull out of a dive
does not). This is the same ceiling `playtest.md`'s `CAP-20` row already records.

⚠ **`BL-109`'s 32.96 / 34.02 m/sim-s² is SPEED-SPECIFIC. Never reuse it as a general max-pull
yardstick.** Lift ∝ V², and at ~300 mph this same aircraft pulls **72.6 m/sim-s²** in free air —
2.17× that figure with nothing anywhere near it (`CAP-02 Run3 5`, 28 s at 2,634–3,939 ft over open
water). Comparing a fast manoeuvre against the slower loop is what manufactured a phantom "×2".
Film a free-air control in the same session, at the same speed, and compare inside a **matched
speed band** — and note `a_n/V²` is *not* a safe substitute: it is proportional to `C_L` in
principle but blows up as V falls, reading a spurious 4.07× at 80 mph on `CAP-02 Run3 1`.

⚠ **Range, not altitude — and a shallow dive is what separates them.** Along-path range to a level
surface is `alt / sin|γ|`, so in a −43° dive it is 1.47× the altitude and in a vertical dive the two
are identical. Only a *shallow* pass can tell a range trigger from a height trigger, and only over
**water** is the surface flat and at a known 0 ft. Both `CAP-02` batches' other dives are
near-vertical and cannot distinguish them at all.

⚠ **Path-normal acceleration without differentiating γ.** Every attempt that went through
`ω = dγ/dt` failed on these clips, because γ saturates at ±90° in a steep dive and the unpinning
frames throw the ±60 °/s artifacts recorded above. Use instead, from `h" = V' sin γ + a_n cos γ`:

```
a_n = (h" - V' sin γ) / cos γ           sin γ = climb / V     (a ratio, not a derivative)
```

`h"` by local *quadratic* fit on the PTS axis (one fit, not two chained first-derivatives), `V'` by
local linear fit, and gate on `|sin γ| < 0.90` — the estimator only dies where `cos γ → 0`. On
`CAP-02 Up Down` this reads 0% gated across the whole recovery and the peak moves by less than
±15% across smoothing windows from 0.30 s to 0.90 s.

⚠ **A clip that ends in a crash fails `checkclip` on its last second.** The gate is deliberately
all-or-nothing (a head turn invalidates everything after it), so it returns REJECT for the whole
clip. Re-run the same registration **in windows** to find where rigidity is actually lost, and
decode only the prefix — `CAP-02 pull up to cras` is clean to t = 10.5 s of 12.8 s. Do not skip the
windowed check and decode a rejected clip anyway; and note a REJECT's "consistent with auto head
turn" wording names one cause among several — `CAP-02 C4 Canyon 2` shears from **damage wobble**.

⚠ **The design document's turn-rate ladder (30/45/60/75/90 °/s) is the *velocity-vector* turn
rate, and it maps to pitch** — not a body-axis rate. Measured sustained pitch ~34 °/sim-s sits
between the ladder's 30 and 45 steps, consistent with the ladder being pitch and the Bloodhawk
not being top-of-ladder. Its shipped *roll* rate of 94.5 °/s landing on the ladder's top step is
a coincidence.

## Running it

From the repo root, in order (each writes into `.scratch/vidcal/cache/`). `.scratch` is swept
by `CleanScratch.ps1`, so this is the cold-start order — **`extract` and `pool` first**, since
everything after them differences against the pooled median:

A clip flown in the **chase** view names its HUD in `extract.py`'s `CLIPS` and then runs the same
order with `--hud=chase` on the pooling and fitting stages:

```
python analysis/video-flight-calibration/extract.py cap18 cap10chase cap10dive
python analysis/video-flight-calibration/pool.py --hud=chase
python analysis/video-flight-calibration/fitdial.py --hud=chase
python analysis/video-flight-calibration/ammoarrow.py cap18 chase   # selector arrow
python analysis/video-flight-calibration/run2chase.py cap10chase cap10dive   # alt + mph, on PTS
python analysis/video-flight-calibration/enginepitch.py             # pair alt/mph to CAP-10 audio
python analysis/video-flight-calibration/dutysweep.py               # scripted-rig takes (F13/F14)
python analysis/video-flight-calibration/recovery.py                # the dive-recovery take
python analysis/video-flight-calibration/chasesize.py               # apparent size -> chase distance
```

`chasesize.py` is the one chase stage that does **not** read the cached panel strip: it needs the
full colour frame, because the measurement is the aircraft itself rather than an instrument. It
still normalises to canonical game px, so its numbers are comparable across capture geometries.

⚠ **The chase decode does not use `LKRegistrar`, and must not.** The chase HUD is a screen-space
sprite pinned by construction — `pool.py` registers every chase clip at dx = dy = 0 — so there is no
shake to solve, and its dials sit hard against the screen edge, where the registrar's padded ROI
runs to negative indices and `np.gradient` dies on the empty slice. `run2chase.py` therefore passes
(0, 0) directly. This is why the chase path had dial *fits* long before it had needle *readings*.

⚠ **The chase HUD carries no pitch attitude.** Its green bottom-left instrument looks like an ADI
but is a **roll-only plan-view indicator** — a fixed top-down aircraft silhouette against a rotating
ring. Its green-fill fraction is flat to 4% across a fully vertical dive, so it cannot be read for
pitch, and the cockpit `adi.py` crop landing on it returns a constant. The only pitch proxy available
on a chase clip is the flight-path angle γ = asin(climb/v) from the altimeter and speedometer — and
γ **saturates at ±90°** in a genuinely vertical dive (descent rate reaches airspeed), so `dγ/dt`
there is a clipping artifact, not a pitch rate. Drop those frames before using it
(`recovery.py` gates on `|climb/v| < 0.98`).

⚠ **`Dial` and `LKRegistrar` take `sfx=""`** to choose the pooled reference (`""` cockpit,
`"_chase"` chase). The default is the cockpit pool, so every cockpit caller and every published
cockpit number above stays valid without a re-run — the same convention `pool.py` and `fitdial.py`
already use for their cache names.

The cockpit path is unchanged:

```
python analysis/video-flight-calibration/extract.py     # video -> panel-strip .npy cache
python analysis/video-flight-calibration/pool.py        # pooled median + std (2,529 frames)
python analysis/video-flight-calibration/checkclip.py   # GATE a new clip before decoding it
python analysis/video-flight-calibration/shake.py       # global panel translation per frame
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import fitdial"
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import run2;run2.main()"
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import anchor;[anchor.resolve(s) for s in ['pitch','roll','yaw','dive','accel','decel','cap01']]"
python analysis/video-flight-calibration/compass.py cap01   # heading, where a clip turns
```

PTS timestamps come from the bundled ffmpeg — **required**, see the VFR trap below:

```
ffmpeg -i <clip> -vf showinfo -f null - 2>&1 | grep -oE "pts_time:[0-9.]+"
```

Needs `imageio_ffmpeg`, `numpy`, `scipy`, `pillow`, `matplotlib`. No game data is read
except the extracted dial textures (`extracted/C1/texture/{altimeter,speedometer}.png`)
and `extracted/zrdr/{vehicle,player,engines}.zrd.json`.

**Running it from a git worktree** (added 2026-08-07 for `CAP-02`). Both inputs above are
git-ignored, so a worktree has *neither* `OriginalScreenshots/` nor `extracted/`. Two env vars
point the pipeline at the main checkout's copies:

```
$env:CSVM_VIDEOS    = "Z:\CSVM\OriginalScreenshots\Videos"   # extract.py
$env:CSVM_EXTRACTED = "Z:\CSVM\extracted"                    # fitdial.py
```

⚠ **Never junction the media in instead** — PowerShell 5.1's recursive delete follows a junction
into its *target*, so a link left in a worktree turns a later sweep into a deletion of
irreplaceable original-game footage (`CLAUDE.md`).

**A third capture geometry: 1920×1080** (`CAP-02`). 1080p is 16:9 but reaches canonical 1280×720
at **1.5×**, which the block-mean in `extract.py` cannot do — it takes an integer factor and raises
on anything else rather than guessing. Those clips are therefore pre-resampled **once** with
ffmpeg/lanczos into `playtest/CAP-02/` and enter through the new `LAYOUTS[(1280, 720)] = (0, 0, 1)`;
a single high-quality resample beats a two-step. It registers: the cockpit pool rebuilt to the same
2,529 frames, `fitdial` returned altimeter NCC **0.9787** / speedometer **0.9808** against the
published 0.978/0.981, and all four 1080p cockpit takes landed at **dx = dy = 0** (peaks 0.61–0.68).
On the chase side MPH / gun / rockets also land at dx = dy = 0 (peaks 0.48–0.82) — so the
screen-space HUD does scale with render resolution, which was not obvious in advance. The chase ALT
and ADI cannot be checked this way at all: their ROI sits hard against the screen edge and the
padded search window runs off the array, returning the window edge with peak 0.000. That is the
gate's limit, not the clip's.

⚠ **`run2chase.pts()` silently returned an EMPTY time axis for a staged clip** (fixed 2026-08-07).
`extract()` has always accepted a repo-relative `playtest/<ID>/…` path; `pts()` did not, and hard-
prefixed `OriginalScreenshots/Videos`. The ffmpeg call then failed, the list comprehension yielded
`[]`, and **nothing raised** — `alt`/`mph` decoded and printed healthy statistics while `_pts.npy`
was zero-length, so the failure only surfaced downstream as an empty analysis window. Both helpers
now share the same fallback.

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

**Heading comes off the compass tape, and the tape calibrates itself** (added for `CAP-01`,
2026-08-03). The tape is a scrolling strip, so per-frame sub-pixel 1-D cross-correlation of a
±45 px window about its centre tracks it (correlation median 0.973), and the cumulative scroll is
heading. Its degrees-per-pixel needs no assumption: resampling the tracked central ±12 px into a
tape-fixed coordinate stitches a **panorama** of the whole strip, and that panorama is periodic in
360°. Two independent rulers on it agree — autocorrelation of the label band peaks at
**537.7 px** (corr 0.907) ⇒ 1.4937 px/deg, and a lattice fit to the major ticks gives **22.343 px**
per major, which is 24.07 majors per revolution ⇒ at exactly 24, 1.4896 px/deg. The two bracket
**1.500 px/deg** to 0.3%, and put the majors **15°** apart, in canonical 1280×720 game coords.
`compass.py` runs both. The end-to-end check: `CAP-01`'s
675.14 px of travel is 450.09°, and the panorama's own label sequence reads
E·NE·N·NW·W·SW·S·SE·E·NE·N — 10 × 45° = **450°**, agreeing to 0.02%.

⚠ **The compass reads the nose, not the flight path.** It is a heading tape, so in any manoeuvre
with angle of attack it is not the velocity-vector turn rate — which is the quantity the design
document's turn-rate ladder refers to.

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

**Capture aspect ratio does not have to match, but it must be declared.** The 2026-07 set is
32:9 (2560×720) with the game pillarboxed into x 640..1919; the 2026-08-03 CAP set is 16:9
(2560×1440) with the game filling the frame. The game renders 16:9 either way, so the second is
exactly 2× the first — `extract.py`'s `LAYOUTS` table block-mean downscales it into the same
canonical 1280×720 game coords, after which the panel lands on **the same pixels**: every clip
of both sessions phase-correlates to the pooled median at dx = dy = 0 (peaks 0.75–0.88). So the
ROI, the fitted dial affines and the median itself all carry over, and nothing downstream of
`extract.py` knows the capture geometry. An unlisted resolution raises rather than guessing an
origin — a wrong origin decodes silently.

**A level clip barely constrains the altimeter's 1,000 ft band.** `anchor.py` resolved the six
manoeuvre clips with a ≥1.97× margin, but `CAP-01` — level throughout, 87 ft of total altitude
range — came back **k=3 at only 1.39×**, with k=2 scoring 0.72. The short needle hardly moves, so
there is almost nothing for the scoring to bite on. It did not matter there, because every result
in that clip depends on *changes* in altitude and not its absolute value; but do not quote an
absolute altitude off a level clip without saying the band is soft. The margin is printed — read it.

**A big shake trips a bare `|dx|` threshold; only the sign separates it from head turn.**
`checkclip`'s original rule rejected any clip whose two dials disagreed in dx by more than 2 px,
which fails on a hard dive: `CAP-10 2` shears 3 px and is perfectly decodable. Head turn
foreshortens the dials in *opposite* directions (dx anti-correlates) while shake translates the
whole panel (dx correlates at +0.95 on that clip). The correlation is now the discriminator and
the spread only a trigger for computing it.

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
| 3 | Sustained level turn, max pull | ✅ **decoded 2026-08-03** from `CAP-01.mp4` — 222.9 mph sustained against 299.0 level, at 18.95 °/sim-s and 100° bank (`BL-092`, `BL-247`) |
| 4 | 360° aileron roll | ✅ |
| 5 | Low pass along a canyon wall | ❌ owed — the only source for ground blow |
| 7 | Sustained knife-edge | ✅ **decoded 2026-08-04** from the two `CAP-05` knife clips — a 4° nose step then an unbounded 0.7–0.9 °/sim-s sag, 540 m lost in 38.9 sim s, turning only 0.7–1.1 °/sim-s at 100° bank (`BL-247`; closed `BL-124`) |
| 8 | Stall entry and recovery, engine off | ✅ **decoded 2026-08-04** from `CAP-05 Stall 0% Thrust no input` — break at 0.25 fd, nose drop 3.4 °/sim-s to a −22° floor, and the low-speed drag curve (`BL-115`, `BL-092`) |
| 6 | Level top speed at 5500 / 6000 / 6500 ft, plus the cap | ✅ **decoded 2026-08-03** from the four `CAP-03` clips — flat 300 mph to 1988 m, then a hard altitude clamp at 2003 m (`BL-094`). 6800 ft is unreachable: the aircraft cannot be flown above the clamp |
| 9 | Level runs at 1/4 and 1/2 throttle, held to equilibrium | ❌ owed — the thrust-vs-throttle curve. `CAP-05`'s 50%-throttle clip cannot serve: its ADI saturates in the climb, so the nose angle (and with it the along-path thrust) is unreadable |
| 10 | Approach to stall with the speedometer readable | ✅ **decoded 2026-08-04** from the two `CAP-06` clips (+ both `CAP-05` stalls) — warning is binary in brightness, ramped in blink rate, threshold 0.30 fd against the stall's own 0.25 (`BL-148`) |
