# Screen-shake and wobble behaviour, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), over 2026-08-18 / 2026-08-19, settling the
`BL-266` gun-wobble and overspeed-rattle half of the fire-rate backlog. Every claim names the
function or address it came from. No decompiler output is reproduced; the addresses are given so
any claim can be re-checked at source.

**Where the neighbours live.** The *authored* shake content, the six oscillator-source blocks of
`shakes.json` and the `ON_CALL` damage-shake animations of `damage_shakes.json`, is the shared
zrdr reader [`../formats/shakes.md`](../formats/shakes.md). This page is what the original does
with those laws: the per-shot/per-frame magnitudes, the accumulator they feed, the consumer that
rocks the plane, the camera attachment, and the port's fidelity gap.

## Two oscillators, one mechanism

In 3rd-person views of the original the **plane itself** wobbles against the world; cockpit/nose
views read as camera shake. One mechanism explains both, rock the plane node, and any
plane-mounted camera inherits the motion, mirroring how the `damage_shakes` defs rock the plane's
`healthy` node (there the camera gets its own authored half because the chase camera is not
rigidly attached). The engine implements exactly this: `PlaneShake` rolls a pivot the plane model
hangs under; physics and the camera never see it.

**Every shake source is the same 3-axis random-walk accumulator.** `fire_bullet` (per shot),
`high_speed` (per frame) and `nitro` (per engage) all run through `FUN_0042be10`, a random walk
that kicks three camera-relative block accumulators. Its step is built from the block's own
authored law, not from a fixed gain (`0042be10`–`0042be60`): `W = 6.2832` when the block's
`sawtooth` word is clear and `4.0` when it is set (`0x006040b0` / `0x00603514`), then
`step = magnitude × frequency × W` and `Δroll/pitch = (rand01−0.5)·step·1.2`,
`Δyaw = (rand01−0.5)·step·2.5` (`0x006040ac`, `0x006040a8`). The three sources differ only in
component block, magnitude law, and cadence:

| | `fire_bullet` | `high_speed` | `nitro` |
|---|---|---|---|
| component block | 0 (`camera+0x24/+0x28/+0x2c` roll/pitch/yaw) | 4 (`camera+0xd4/+0xd8/+0xdc`) | 6 (`camera+0x12c/+0x130/+0x134`) |
| magnitude law | `magnitude_factor × CALIBER` per shot | `(speedRatio − min_speed)/magnitude_quotient` per frame | the authored `magnitude` 0.05 |
| cadence | once per shot at 8/s | every frame while over the gate | once per engage |
| step per unit magnitude | ±36 (freq 15, sawtooth) | ±36 (freq 15, sawtooth) | ±9.6 (freq 4, sawtooth) |

⚠ **The kicked triple is a VELOCITY, and the authored `frequency` is inside the step.** Neither is
what an envelope reading expects: the rendered angle is the position the integrator below builds
out of that velocity, a fraction of it, and a source's step scales with its own authored rate. A
reading that takes `camera+0x1c`'s `2.0` for a gain is reading the constructor default over the
top of the authored `fire_bullet` frequency 15.0 (the ⚠ at the end of the block table).

## Where the wobble STATE lives vs. where it displaces, and the CONSUMER

The random-walk accumulators are written **onto the camera object**, not the plane,
`FUN_0048c470` (the per-frame player updater) reads `camera+0xec`/`+0xf0` for `high_speed` when
its `min_speed` gate trips and calls `FUN_0042c070(4, mag)`; the gun path calls
`FUN_0042c070(block, mag)` the same way. `FUN_0042c070` kicks `camera`-relative component blocks,
one per authored source, indexed in the parser's own order (the table in "The seven component
blocks" below). Each block's `[3]/[4]/[5]` (roll/pitch/yaw) are the **velocities** the kick adds
to; `[6]/[7]/[8]` are the **positions** that integrate from them and that the consumer sums.

The render consumer is **`FUN_0042c0e0`**: it walks the camera's **seven** component blocks
(0xb dwords = 0x2c bytes apart), runs the per-block integrator `FUN_0042bec0`, **sums all
blocks' positions** `[6]/[7]/[8]` (the walk starts at `camera+0x30`, which is block 0's `[6]`, and
strides `0xb` dwords), transforms the total through the quaternion helpers
(`FUN_0053fbf0/f850/fa40/df30`) and applies it to the **plane node** `DAT_0071c304` via
`FUN_004d1a30`. Its only caller, the per-view render handler **`FUN_0042e5e0`**, calls it **first,
unconditionally, every frame, with no branch on the live mode byte `camera+0x14c`**. The mode
byte is used elsewhere only for FOV (mode 6→80°, `FUN_0042b660`), head-lock (mode 7), cockpit-
interior draw, and hiding scene nodes, **never to scale the wobble**. So **there is no per-view
dampening**: cockpit(6)/nose(7) inherit the full undampened wobble 1:1 from the plane node.

  *The "dampening" is per-**source**, not per-**view**: `FUN_0042bec0` integrates each block on
  its own authored law, at a fixed `1/150` s substep (`0x3bda740e` at `0042beda`) with the last
  substep of a frame cut short, and the position it builds is what renders. Identical for every
  camera view. The two laws are these:*

- **A block with `sawtooth` clear is a damped spring** (`0042bf20`–`0042bf90`):
  `v -= (damp·v + (2π·freq)²·x)·h` per substep, `freq` and `damp` the block's `[1]` and `[2]`.
  A kick therefore rings down at the authored rate, which is the reading `PlaneShake`'s envelope
  sources approximate.
- **A block with `sawtooth` set is a ramp with a reversal test** (`0042bfd8`–`0042c000`): when
  position and velocity share a sign and `|v| < |x|·freq·4.0`, velocity is replaced outright by
  `v = -4.0·freq·e^(-damp/(2·freq))·x` (the exponential is `FUN_00460410` called at `0042c000`,
  a Taylor series under `0.1` and the table in `FUN_0053e2e0` above it; constants `0.5` at
  `0x006032e0` and `-4.0` at `0x006040b4`). Otherwise velocity is left alone. Either way
  `x += v·h` closes the substep at `0042c025`.

  *The reversal makes a sawtooth block coast in a straight line until it has travelled far enough,
  then snap to the opposite heading at a speed proportional to how far out it is, so an untouched
  block draws a decaying triangle wave and a block re-kicked every frame wanders. Both of the
  sources this page owns, `high_speed` and `nitro`, author `sawtooth 1`, and so does
  `fire_bullet`.*

  *`camera+0x24` is therefore **not** a cockpit dampener, it is block 0's roll accumulator
  `[3]` (base `camera+0x18`). The earlier "static-trace NEGATIVE" on it (`BL-266`, `7f8881a0`)
  is now a POSITIVE: the reader is `FUN_0042c0e0`, which read it indirectly via the 7-block walk
  (a register-relative float add, not a direct `camera+0x24` load), that indirection is why the
  original field search missed it.*

Because the first-person placement `FUN_0042d980` (modes 6/7, [`cameraViews.md`](cameraViews.md)) reads
the plane's basis directly and adds **no** wobble of its own at attachment, a plane-mounted camera
inherits the rocked rotation automatically, that is why cockpit/nose read as camera shake and the
two cockpit views need no separate handling.

## The seven component blocks and every kicker

The camera object `DAT_0064ef78` is an `operator_new(0x158)` allocation constructed by
`FUN_0042bab0`, and it carries seven identical oscillator blocks starting at `camera+0x18`, each
eleven dwords (`0x2c` bytes): `[0]` sawtooth, `[1]` frequency, `[2]` damp, `[3]/[4]/[5]` velocity,
`[6]/[7]/[8]` position, `[9]` and `[10]` the block's one or two magnitude terms. The constructor's
seven-iteration loop fills every block with the same defaults, frequency `2.0`, damp `4.5`,
sawtooth 0, zero accumulators and a zero first magnitude term. `FUN_0042bc10` then reads
`shakes.zrd` and overwrites, per source, only the fields that file authors.

Block index is the parser's own source order, and the magnitude-term offsets pin it: every
authored magnitude lands at its block's `[9]` (and `[10]` for the two-term sources).

| block | base | source key | magnitude terms | kicked by | when |
|---|---|---|---|---|---|
| 0 | `camera+0x18` | `fire_bullet` | `magnitude_factor` `+0x3c` | `FUN_004b6820` at `0x4b6e38` | one gun round fired |
| 1 | `+0x44` | `bullet_impact` | `magnitude_factor` `+0x68` | `FUN_004b9bc0` at `0x4b9d26`, index 1 | one gun round taken |
| 2 | `+0x70` | `missile_impact` | `magnitude_factor` `+0x94`, `he_factor` `+0x98` | the same site, index 2 | one rocket taken |
| 3 | `+0x9c` | `explosion` | `max_magnitude` `+0xc0` | the same site, index 3 | one nearby detonation |
| 4 | `+0xc8` | `high_speed` | `min_speed` `+0xec`, `magnitude_quotient` `+0xf0` | `FUN_0048c470` at `0x48d1bc` | every frame over the gate |
| 5 | `+0xf4` | `turbulence` | none parsed; `+0x118` is never written | `FUN_0048d2c0` at `0x48d409` | one collision contact (`PlaneShake.ContactHit`, every human pilot) |
| 6 | `+0x120` | `nitro` | `magnitude` `+0x144` | `FUN_004b2131` at `0x4b21ce` | nitro engaged, player only (`PlaneShake.NitroEngaged`, every human pilot) |

**Every kicker carries an AI twin, and that is what the `damage_shakes` `*_aishake` defs are
for.** `FUN_00473430(this, index)` plays one of `_DAT_0071c2f4`/`+4`/`+8` (`small`, `medium`,
`large_aishake`, resolved by name at startup in `FUN_004735b0` at `0x473911`) on the vehicle's own
node `[obj+0xc]`, refuses when the vehicle IS the player (`DAT_0071c298`) and when a shake instance
is already alive in `[obj+0x6ec]`, and clears that handle from the instance's completion callback,
so one aircraft rocks to one def at a time. Its five call sites are the same five functions as the
table above, each standing immediately before that block's player arm: `0x4b6e13` index 0 (a round
fired), `0x4b9d0e` index 0 (a round taken), `0x48d204` index 1 (past `fd_speed` × `0x6040ac`, the
overspeed arm), `0x48d3bf` index 2 (a collision contact) and `0x4b21b2` index 1 (a nitro engage).
So the split is by who is flying rather than by event: a person gets the camera block, everyone
else rocks the aeroplane. CSVM wires the nitro engage (`FlightController.AdvanceNitro`,
`EffectCatalogue.AiShakeAnim`); the other four are decoded and not wired.

That table is the complete kicker list. `FUN_0042c070` is a one-line forwarder to `FUN_0042be10`,
`FUN_0042be10` has no other caller, and the five call sites above are every xref to
`FUN_0042c070`. The only other writer of any block's accumulators is the integrator
`FUN_0042bec0`, whose sole caller is the render consumer `FUN_0042c0e0`. No function outside the
shake module stores a float at the block offsets.

⚠ **The `explosion` block's magnitude term never fills.** The parser reads the key
`max_magnitude` (`0x6215fc`) into `+0xc0`, and `shakes.zrd` authors `magnitude_factor` for that
source instead, which no reader looks for. The slot therefore keeps the constructor's zero and
the original's explosion shake has magnitude zero. The three impact sources are `BL-266(b)`'s
subject and the correction belongs there.

⚠ **`camera+0x1c` holding `2.0` is the constructor default, not the live value.** The parse
overwrites it with the authored `fire_bullet` frequency, and the same applies to every block the
data authors. Any gain read off the defaults is a reading of an uninitialised camera;
`BL-266(a)` owns the gun-buzz law that depends on it.

## Ambient turbulence does not ship

The design intent (a subtle, continuous jostle of the player's plane in steady flight, with zero
effect on speed, heading or performance) is **not built in the retail game**. It is closed as
unshipped intent, on two independent negatives.

**The executable parses a `turbulence` source and nothing drives it.** `FUN_0042bc10` looks up the
key `turbulence` (`0x00621638`, referenced exactly once in the whole binary, from `0x42bd93`) and
passes block 5 at `camera+0xf4` to the shared law reader `FUN_0042bba0`. That reader takes
`frequency`, `damp` and `sawtooth` and nothing else, so unlike all six of its neighbours the
turbulence block has **no magnitude field at all** to parse: there is no key whose value would say
how hard an ambient jostle rocks the plane, and `camera+0x118`, the slot a magnitude would occupy,
is written by no instruction in the executable. Block 5's only kicker is the contact path
`FUN_0048d2c0`, which computes its own per-collision magnitude ("Block 5, the per-contact kick"
below), so the slot the design named is in service as the collision oscillator. There is no
per-frame, ungated caller of `FUN_0042c070` on any block: of the five call sites, four are per-event (a round fired, damage taken, a contact, a
nitro engage) and the fifth, `high_speed`, runs per frame but only above its authored `min_speed`
gate, which is rated max speed. Steady flight kicks nothing.

**The data authors no such source.** `extracted/zrdr/shakes.zrd.json` is the only shake-oscillator
file in the extracted tree, there is no per-campaign, per-mission or per-airframe override of it,
and it authors six blocks: `fire_bullet`, `bullet_impact`, `missile_impact`, `explosion`,
`high_speed` and `nitro`. `turbulence` is not among them, so block 5 also keeps the constructor's
default law. A census of all 61010 extracted files finds no field or token containing `turbulen`,
`jostl`, `buffet`, `gust`, `wobble`, `vibrat`, `jitter` or `thermal` anywhere, and no shake source
with an idle, cruise or always-on activation. The nearest neighbours are all something else:
`player.zrd.json`'s `rattle` is the speed-keyed volume and pitch envelope for the `snd_planeshake`
sound and carries no motion; the mission `weather.zrd.json` `WIND` block
(`STATIC_VELOCITY`, `RANDOM_MAX_SPEED`, `RANDOM_ACCEL`, `RANDOM_ANG_VEL`) is a particle field,
identical in all 53 mission copies and consumed only by `WIND_FACTOR` on dust, smoke, steam and
spray emitters, with no plane or player file referencing it; `damage_shakes.zrd`'s `ON_CALL`
animations are finite three-loop damage reactions; and every `ambient` hit in the tree is lighting.

So magnitude and cadence for an ambient jostle have no authored or executable source, which under
this project's rules (`docs/verification.md` `SRC-3`, design documents give intent and retail
evidence decides shipped details) makes any oscillator added here invented content rather than
parity. `PlaneShake` gains no ambient source.

## Block 5, the per-contact kick, `min(speed × severity × 0.03, 0.15)`

Block 5 is the source the data never authors, so it runs on the constructor's law alone: frequency
`2.0`, damp `4.5`, sawtooth `0`. Its magnitude is not read from any file. `FUN_0048d2c0`, the
collision-damage function, computes it per contact at `0x0048d3cc`–`0x0048d409` from the true
airspeed `obj+0x934`, the severity cosine the sweep returned, the literal `0.03` at `0x006080c4`
(the same literal the player's contact push-out uses) and a ceiling `0.15` at `0x006036a8`. Two
guards stand over the kick and nothing else does: the object is the player (`0x0048d3c4`) and its
`fd` switch `obj+0x384` is clear (`0x0048d3aa`). The second guard never fires in play: `obj+0x384`
is the `-fd` developer switch, written only by the command line, the debug console and the vehicle
constructor's zero (`flightModel.md`, "`+0x384` is a developer switch"), so a wreck's contact kicks
the camera exactly as a live airframe's does.

**Every resolved contact kicks it, a graze included.** The caller gates `FUN_0048d2c0` on a positive
severity cosine and on nothing else (`0x48ed79` guarding the call at `0x48ed8b`,
[`flightModel.md`](flightModel.md)), so there is no minimum severity, no closing-speed threshold and
no cooldown between kicks. The magnitude scales linearly with both terms rather than with the pair's
cube, and at any flight speed the ceiling is reached by a cosine around `0.05`, so even the
shallowest contact saturates and the whole run of contacts from a scrape to a nose-in reads as the
same-sized kick. **Ported** as `CollisionDamage.ContactShake` feeding `PlaneShake.ContactHit`, on
every human pilot rather than a single player pointer, the same widening the bounce impulse takes.

⚠ **The magnitude is a velocity, not a displacement.** `FUN_0042be10` adds it to the block's
`[3]/[4]/[5]` accumulators, which the integrator `FUN_0042bec0` turns into the `[6]/[7]/[8]`
positions the consumer sums, so the original's rendered roll from a saturated kick is a fraction of
`0.15` rad rather than that angle. `PlaneShake` models each block as an envelope in radians of roll
directly, which is the same modelling gap the fire source carries (`BL-266(a)`); the kick's
magnitude is the decode and how it renders is that item's question.

## `fire_bullet`, per-shot roll, `magnitude_factor × CALIBER`

Edge-traced in `crimson.exe` (`analysis/gun-wobble-shake/`). The consume site is the plane
per-tick firing loop `FUN_004b6820` (`FILD` weapons-ext `CALIBER` at `weapon+0x210 → +0x10` ×
`*(camera+0x3c)` = `magnitude_factor`, loaded raw by the `shakes.zrd` parser `FUN_0042bc10`;
camera = `DAT_0064ef78`).

A dead-astern chase clip of the original firing 40-cal slugs shows a roll-dominated wobble
(left/right wing vertical motion anti-correlated at −0.86) of 2.8e-3 rad RMS / ~4.0e-3 rad peak;
the dead-astern view makes the screen angle the world roll angle with no projection model.
Candidates: caliber 40 × 7e-5 = 2.8e-3 rad (match); damage 4.5 → 3.15e-4 (~9× under);
velocity 900 → 6.3e-2 (~16× over), an order-of-magnitude discrimination, not a one-coincidence
match.

⚠ **Amplitude-call caution:** the 2.8e-3 rad is the clip's *rendered* RMS, not the oscillator's
*kick* amplitude, a kick of envelope `E` renders only ~0.28·E as RMS at the real 8/s fire rate
(sawtooth duty × damp envelope decay), so the engine renders ~0.28× the law's literal number
regardless of the clip (decoded: ~8e-4 rad RMS / ~0.20 px/frame at the ±205 px lever).

⚠ **The ×CALIBER multiplicand is confirmed; the downstream step is the block's own authored law.**
`FUN_0042be10` builds the step from the *parsed* block, and `shakes.zrd` authors `fire_bullet`
with `sawtooth 1` and `frequency 15.0`, so the waveform selector takes the **`4.0`** branch
(`0x00603514`) and `step = mag × 15 × 4 = 60·mag`, giving a per-shot velocity kick uniform in
**±36·mag rad/s** into `camera+0x24` (`(rand01−0.5) × step × 1.2`). For wep40 that is ±0.101 rad/s,
and what renders is the position the sawtooth integrator builds out of it, not the kick.
An earlier reading of this line took `camera+0x1c`'s `2.0` for a gain and the `6.2832` branch for
the waveform, which is the constructor's uninitialised block rather than the parsed one; both are
corrected here. The engine's landed `_fire` source is still a displacement walk of ±7.54 per shot,
a *different* mechanism from the velocity kick above, and reconciling it is `BL-266(a)`'s question
rather than this page's.

⚠ **That consumer is shared**, `high_speed` drives the IDENTICAL `FUN_0042be10` random-walk
accumulator (a second component, block index 4, at `camera+0xd4/+0xd8/+0xdc`) through the same
`FUN_0042c0e0`, and visibly wobbles in the clips, so the mechanism is live for both. Whether
`magnitude_factor` reads right against the original is the clip/fidelity judgment rather than the
decode, see `analysis/gun-wobble-shake/FINDINGS.md`.

**Unmeasured residue:** whether plane model/weight also enter (single-plane, single-gun clip,
owed capture in `playtest.md`), and the impact sources' own quantities (the engine stands in
caliber for gun hits and armor damage for rockets, declared TUNE).

## `high_speed`, overspeed rattle, excess over the gate

`high_speed`'s input reads as speed normalised by the plane's rated max (`fd_speed`), so the
`min_speed` 1.0 gate means "beyond rated max", the overspeed/dive rattle. Level cruise in the
firing clip shows a motionless idle floor (~0.01 px/frame), which an absolute-speed reading with
a gate at 1.0 m/s could not produce.

**The magnitude is the EXCESS over the gate, `(speedRatio − min_speed)/magnitude_quotient`**, not
the whole ratio (`PlaneShake.SetSpeedRatio`): the gate value is
*subtracted* from the numerator, so the rattle is zero at rated max (speedRatio 1.0, `min_speed`)
and ramps gently with overspeed, landing in the same order as the gun buzz in a dive. Reading it
as the whole `speedRatio/quotient` instead, the earlier wiring, snapped on at `1.0/70` rad the
moment you crossed rated max, 5× the entire 40-cal gun buzz, and barely ramped after (+27% over
the envelope); that is what the whole-ratio read did wrong. The overspeed audio layer
(`prop_sound`) engages in the same regime.

**`high_speed` shares the SAME random-walk accumulator as the gun**
(`FUN_0048c470`, the per-frame player updater, reads `camera+0xec` (`min_speed`) and
`camera+0xf0` (`magnitude_quotient`), and when the gate trips calls `FUN_0042c070(4, mag)`, the
exact same dispatcher/accumulator the `fire_bullet` path uses, just component index 4 instead of 0:
`this = camera + 4·0x2c + 0x18`, kicking the three block-4 accumulators `camera+0xd4/+0xd8/+0xdc`
roll/pitch/yaw per frame). So in the original both sources are the same 3-axis random-walk; they
differ only in block (0 vs 4), magnitude law (per-shot `magnitude_factor×CALIBER` vs per-frame
`(speedRatio−min_speed)/magnitude_quotient`), and cadence (fire once per shot @8/s; `high_speed`
every frame while over the gate). Full trace: `analysis/gun-wobble-shake/FINDINGS.md`.

**Ported as the original's own component block.** `PlaneShake` runs `high_speed` and `nitro`
through a private `Block` that carries the decoded pair, the velocity kick of `FUN_0042be10` and
the two-branch integrator of `FUN_0042bec0` at the same `1/150` substep, and renders the block's
roll *position*. The deterministic damped sawtooth these two sources used before was a different
mechanism, and the reason a dive read as a muted buzz instead of a rattle. Three readings this
port takes and their grounds:

- **Roll is the first of the kicked triple**, the `×1.2` axis, not the `×2.5` yaw axis. The block
  layout `[3]/[4]/[5]` is roll/pitch/yaw throughout this page, and the engine renders roll only,
  so the roll weight is the one that applies. Reading it as the yaw axis instead would be about
  2.08× larger.
- **The kick is per frame, as the original's is.** The original's frame rate therefore sets the
  drive, and so does ours, which is a rate dependence the original has too rather than one the
  port introduces. `PlaneShake.DiveRattleKickScale` and `NitroWobbleKickScale` both default to
  `1`, the faithful step, and are the only knobs to dial. `magnitude_quotient` and the authored
  `magnitude` are decode, not tuning.
- **Nothing scales the decoded magnitude.** The engine wires `(speedRatio − min_speed)/quotient`
  and the authored `0.05` exactly as parsed; the amount of roll that reaches the screen is
  whatever the integrator makes of them.

## `nitro`, one kick per engage, the raw authored `magnitude`

The nitro source has no computed law at all. `FUN_004b2131`, the engage path, plays the AI twin
`FUN_00473430(1)` at `0x4b21b2`, then for the player only kicks block 6 with the value the parser
stored, `FUN_0042c070(6, *(camera+0x144))` at `0x4b21ce`, which is the authored `magnitude` `0.05`
unscaled by speed, plane or boost duration. There is one kick per engage rather than a per-frame
drive, so the whole wobble is the block ringing down on its own law afterwards.

That law is `frequency 4.0`, `damp 3.0`, `sawtooth 1`, so the step is `0.05 × 4 × 4 = 0.2` and the
kick is a roll velocity uniform in **±0.48 rad/s** (`±9.6` per unit magnitude). Under the sawtooth
integrator that renders as a decaying triangle wave, reversing roughly every ten ticks at 60 Hz
and losing about a third of its amplitude per swing, so an engage reads as a wobble of about a
second and a half whose size differs from engage to engage because the kick is a single random
draw. `PlaneShake.NitroEngaged` wires it on every human pilot rather than a single player pointer,
the same widening the contact kick takes.

## Camera-attachment rule for our port

Mount the cockpit (mode 6) and nose (mode 7) cameras as children of the **plane model** (below
`ShakePivot`) so they inherit the wobble for free, the original's 6/7 pair are the *same*
`cockpit_camera` point and both ride the rocking plane, so the two first-person views are not
handled differently from each other. The chase/fixed/external cameras stay **top-level**, steered
from the controller's pose (`_renderPose` = controller attitude), *above* the pivot, they must
never read `ShakePivot`, or the `damage_shakes`-style rock would rattle the 3rd-person view too.
When the first-person views land, they go **below** the pivot (inherit); every current camera sits
**above** it (opt out), the same split `damage_shakes` carves between the plane-rocking `aishake`
and the camera's own half.
