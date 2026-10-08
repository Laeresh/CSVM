# Screen-shake and wobble behaviour, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function or address it came
from. No decompiler output is reproduced; the addresses are given so any claim can be re-checked at
source.

**Where the neighbours live.** The *authored* shake content, the six oscillator-source blocks of
`shakes.json` and the `ON_CALL` damage-shake animations of `damage_shakes.json`, is the shared
zrdr reader [`../formats/shakes.md`](../formats/shakes.md). This page is what the original does
with those laws: the per-shot/per-frame magnitudes, the accumulator they feed, the consumer that
rocks the plane, the camera attachment, and the port's fidelity gap.

## Two oscillators, one mechanism

In 3rd-person views of the original the **plane itself** wobbles against the world. One mechanism
does it: the shake turns the plane node, mirroring how the `damage_shakes` defs rock the plane's
`healthy` node (there the camera gets its own authored half because the chase camera is not
rigidly attached). The engine implements exactly this: `PlaneShake` turns a pivot the plane model
hangs under; physics and the camera never see it.

**Every shake source is the same 3-axis random-walk accumulator.** `fire_bullet` (per shot),
`high_speed` (per frame) and `nitro` (per engage) all run through `FUN_0042be10`, a random walk
that kicks three camera-relative block accumulators. Its step is built from the block's own
authored law, not from a fixed gain (`0042be10`–`0042be60`): `W = 6.2832` when the block's
`sawtooth` word is clear and `4.0` when it is set (`0x006040b0` / `0x00603514`), then
`step = magnitude × frequency × W`, and the three velocity components take
`Δ[3] = Δ[4] = (rand01−0.5)·step·1.2` and `Δ[5] = (rand01−0.5)·step·2.5` (`0x006040ac`,
`0x006040a8`). `[3]/[4]/[5]` render as pitch, yaw and roll, so **roll is the `×2.5` component**
("The rendered rotation" below). The three sources differ only in component block, magnitude law,
and cadence:

| | `fire_bullet` | `high_speed` | `nitro` |
|---|---|---|---|
| component block | 0 (`camera+0x24/+0x28/+0x2c` pitch/yaw/roll) | 4 (`camera+0xd4/+0xd8/+0xdc`) | 6 (`camera+0x12c/+0x130/+0x134`) |
| magnitude law | `magnitude_factor × CALIBER` per shot | `(speedRatio − min_speed)/magnitude_quotient` per frame | the authored `magnitude` 0.05 |
| cadence | once per shot at 8/s | every frame while over the gate | once per engage |
| pitch and yaw step per unit magnitude | ±36 (freq 15, sawtooth) | ±36 (freq 15, sawtooth) | ±9.6 (freq 4, sawtooth) |
| roll step per unit magnitude | ±75 | ±75 | ±20 |

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
blocks" below). Each block's `[3]/[4]/[5]` (pitch/yaw/roll) are the **velocities** the kick adds
to; `[6]/[7]/[8]` are the **positions** that integrate from them and that the consumer sums.

The render consumer is **`FUN_0042c0e0`**: it walks the camera's **seven** component blocks
(0xb dwords = 0x2c bytes apart), runs the per-block integrator `FUN_0042bec0`, **sums all
blocks' positions** `[6]/[7]/[8]` (the walk starts at `camera+0x30`, which is block 0's `[6]`, and
strides `0xb` dwords), transforms the total through the quaternion helpers
(`FUN_0053fbf0/f850/fa40/df30`) and applies it to the **plane node** `DAT_0071c304` via
`FUN_004d1a30` ("The rendered rotation" below says what that transform does). Its only caller, the per-view render handler **`FUN_0042e5e0`**, calls it **first,
unconditionally, every frame, with no branch on the live mode byte `camera+0x14c`**. The mode
byte is used elsewhere only for FOV (mode 6→80°, `FUN_0042b660`), head-lock (mode 7), cockpit-
interior draw, and hiding scene nodes, **never to scale the wobble**. So **there is no per-view
dampening**: in cockpit(6)/nose(7) the plane node turns by the full wobble, as it does outside.

  *The "dampening" is per-**source**, not per-**view**: `FUN_0042bec0` integrates each block on
  its own authored law, at a fixed `1/150` s substep (`0x3bda740e` at `0042beda`) with the last
  substep of a frame cut short, and the position it builds is what renders. Identical for every
  camera view. The two laws are these:*

- **A block with `sawtooth` clear is a damped spring** (`0042bf20`–`0042bf90`):
  `v -= (damp·v + (2π·freq)²·x)·h` per substep, `freq` and `damp` the block's `[1]` and `[2]`.
  A kick therefore rings down at the authored rate, which is the reading `PlaneShake`'s envelope
  sources approximate.
- **A block with `sawtooth` set is a ramp with a reversal test** (`0042bfd8`–`0042c000`): when
  the position and velocity triples point the same way (a positive dot product) and the velocity's
  length is under `|x|·freq·4.0`, the whole velocity triple is replaced by
  `v = -4.0·freq·e^(-damp/(2·freq))·x` (the exponential is `FUN_00460410` called at `0042c000`,
  a Taylor series under `0.1` and the table in `FUN_0053e2e0` above it; constants `0.5` at
  `0x006032e0` and `-4.0` at `0x006040b4`). Otherwise velocity is left alone. Either way
  `x += v·h` closes the substep at `0042c025`.

  *The reversal makes a sawtooth block coast in a straight line until it has travelled far enough,
  then snap to the opposite heading at a speed proportional to how far out it is, so an untouched
  block draws a decaying triangle wave and a block re-kicked every frame wanders. Both of the
  sources this page owns, `high_speed` and `nitro`, author `sawtooth 1`, and so does
  `fire_bullet`.*

  *`camera+0x24` is therefore **not** a cockpit dampener, it is block 0's pitch velocity `[3]`
  (base `camera+0x18`). Its reader is `FUN_0042c0e0`, through the 7-block walk (a
  register-relative float add, not a direct `camera+0x24` load), which is why a search for a direct
  field load finds nothing.*

  *The reversal makes a block's whole decay a per-reversal factor: nothing else bleeds the velocity,
  so between reversals a sawtooth block coasts undamped. For `fire_bullet` the factor is
  `e^(−12.5/30)` ≈ 0.66 per reversal. The decay test is `FUN_00460410`, the Taylor series
  `1 − x + x²/2 − x³/6` of `e^(−x)` below `0.1`.*

The first-person placement `FUN_0042d980` (modes 6/7, reached only from the mode switch
`FUN_0042c5c0`, [`cameraViews.md`](cameraViews.md)) builds the camera's position and orientation
from the 3×4 matrix at the watched vehicle's `+0x180`, a field of the vehicle object, and adds no
wobble of its own. The shake is written to the node `DAT_0071c304` instead, so on this reading the
view does not turn with the wobble; what turns is the plane model under that node, the cockpit
interior with it. The two cockpit views need no separate handling ("Camera attachment" below).

## The rendered rotation

What `FUN_0042c0e0` writes to the plane node is the seven blocks' summed position triple `s`, read
as a **rotation vector at twice its length**, with the triple's three components as pitch, yaw and
roll. The chain, each step from the instruction listing:

1. The sum starts from `DAT_0075d1b8`/`+4`/`+8`, a global zero vector that only ever has reads.
2. `FUN_0053fbf0` turns `s` into the quaternion `(cos|s|, sin|s|·ŝ)`. Both of its trig paths
   (`FSINCOS` at `0x0053fc73`, `FSIN`/`FCOS` at `0x0053fc5f`) take `|s|` itself, with no halving
   anywhere in the function, and a quaternion with `cos θ` in front is a rotation by `2θ`.
3. `FUN_0053f850` normalises it; `FUN_0053fa40` is the standard quaternion-to-matrix, stored
   column-major (its `[1]` is `2(xy + wz)`, `[7]` is `2(yz − wx)`).
4. `FUN_0053df30` reads Euler angles back out of that matrix in the engine's own node order
   (R = Ry·Rx·Rz, `docs/formats/gotchas.md`): `x = asin(−m[7])` (`0x0053df4e`–`0x0053df54`),
   `y = atan2(m[6], m[8])` and `z = atan2(m[1], m[4])`.
5. `FUN_004d1a30` stores `(x, y, z)` as the node's own rotation (`+0x18`/`+0x1c`/`+0x20` of its
   class data), replacing it outright every frame from the zero base, so the node's rotation is
   the shake and nothing else. The node is `DAT_0071c304`, which `FUN_0047fcb0` stores at
   `0x0047fcd4` from a by-name node lookup made after one for `player` (`0x00628708`).

For small angles that chain gives node rotation `≈ (2·s[0], 2·s[1], 2·s[2])` about X, Y and Z. In
the engine's frame (Y up, nose along −Z) rotation about X is pitch, about Y is yaw and about Z is
roll. So:

- **Roll is the `×2.5` component `[5]/[8]`**, and pitch and yaw are the two `×1.2` components.
  The original dead-astern firing clip agrees: its wobble is roll-dominated, wing against wing at
  −0.86 (`analysis/gun-wobble-shake/FINDINGS.md`), where equal pitch and roll weights would move
  the fuselage up and down as much as they tilt the wings.
- **Every source moves the nose.** All seven blocks feed all three axes, so the nitro engage, the
  dive rattle and the gun buzz each pitch and yaw the aeroplane as well as roll it.
- **Every rendered angle is twice its block position.**

**The kicker's draws.** `FUN_0042be10` calls `rand` (the pointer at `0x00a20350`) three times per
kick, in the order pitch, yaw, roll, each scaled to `[0, 1]` by `0x00603598` (≈ 1/32767) less the
double `0.5` at `0x00603458`, so every component is uniform and independent.

**The port.** `PlaneShake` runs all seven blocks as above: the kick of `FUN_0042be10` (three
draws, `×1.2`, `×1.2`, `×2.5`), the integrator of `FUN_0042bec0` over the whole triple at the
`1/150` substep, the sum of `FUN_0042c0e0`, and `PlaneShake.NodeRotation`, the quaternion,
matrix and YXZ readback of steps 2 to 4, written to `ShakePivot.Rotation`, whose own order is the
engine's YXZ. Three differences remain, none of them a rescaling:

- **The integrator runs per sim tick, not per rendered frame.** The original passes its frame time
  `0x009ad744` to every block; the port passes the sim `dt`. The substep law is the same.
- **A round taken kicks once.** `FUN_004b9b30` re-runs the take-hit body per leftover pass and each
  pass kicks again; the port kicks once per round, because its damage spend
  (`PlaneDamage.Apply`) runs that leftover loop inside itself. A second pass happens only when a
  round overflows the zone it struck.
- **A block below `1e-6` rad with a matching velocity is set to rest.** Neither law reaches zero on
  its own, and the original never stops integrating.

`GunBuzzKickScale`, `DiveRattleKickScale` and `NitroWobbleKickScale` stay at `1`, the original's
own kick, which reads right against the original's dive, nitro and gun clips at the controls.

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
| 1 | `+0x44` | `bullet_impact` | `magnitude_factor` `+0x68` | `FUN_004b9bc0` at `0x4b9d26`, index 1 | one `CANNON` round taken |
| 2 | `+0x70` | `missile_impact` | `magnitude_factor` `+0x94`, `he_factor` `+0x98` | the same site, index 2 | any other round taken, direct or by its blast |
| 3 | `+0x9c` | `explosion` | `max_magnitude` `+0xc0` | the same site, index 3 | a `SHAKES_CAMERA` round that is not `HIGH_EXPLOSIVE` |
| 4 | `+0xc8` | `high_speed` | `min_speed` `+0xec`, `magnitude_quotient` `+0xf0` | `FUN_0048c470` at `0x48d1bc` | every frame over the gate |
| 5 | `+0xf4` | `turbulence` | none parsed; `+0x118` is never written | `FUN_0048d2c0` at `0x48d409` | one collision contact (`PlaneShake.ContactHit`, every human pilot) |
| 6 | `+0x120` | `nitro` | `magnitude` `+0x144` | `FUN_004b2131` at `0x4b21ce` | nitro engaged, player only (`PlaneShake.NitroEngaged`, every human pilot) |

**Every kicker carries an AI twin, and that is what the `damage_shakes` `*_aishake` defs are
for.** `FUN_00473430(this, index)` plays one of `_DAT_0071c2f4`/`+4`/`+8` (`small`, `medium`,
`large_aishake`, resolved by name at startup in `FUN_004735b0` at `0x473911`) on the vehicle's own
node `[obj+0xc]`, refuses when the vehicle IS the player (`DAT_0071c298`) and when a shake instance
is already alive in `[obj+0x6ec]`, and clears that handle from the instance's completion callback,
so one aircraft rocks to one def at a time. Its five call sites are the same five functions as the
table above, each standing immediately before that block's player arm:

| site | index | when it plays | CSVM |
|---|---|---|---|
| `0x4b6e13` | 0 | never: the call sits inside the branch `0x4b6dfc` takes only for the player, whom `FUN_00473430` refuses | nothing to wire; the fire buzz is a human pilot's alone |
| `0x4b9d0e` | 0, 1 or 2 | every round taken: 0 for any round that is not `HIGH_EXPLOSIVE`; for an HE round by the squared burst distance, 1 inside 400 (`0x00603580`), 2 inside 100 (`0x006032e4`), else 0 (`0x4b9c9a`–`0x4b9cd4`) | `FlightController.RoundTakenShake`, `EffectCatalogue.AiShakeForHit` |
| `0x48d204` | 1 | every frame the true airspeed is past `fd_speed × 1.2` (`0x006040ac`, `0x0048d1e9`–`0x0048d1fe`), a higher gate than the player's `min_speed` 1.0 | the controller's sim step |
| `0x48d3bf` | 2 | every resolved contact, with no `fd` switch test on this side of the branch | `ContactOutcome.AiShake` |
| `0x4b21b2` | 1 | every nitro engage | `FlightController.AdvanceNitro` |

So the split is by who is flying rather than by event: a person gets the camera block, everyone
else rocks the aeroplane. CSVM draws the line at `IsHumanPiloted`, so no camera block reaches an
AI's pivot and every human pilot takes the player's arms.

That table is the complete kicker list. `FUN_0042c070` is a one-line forwarder to `FUN_0042be10`,
`FUN_0042be10` has no other caller, and the five call sites above are every xref to
`FUN_0042c070`. The only other writer of any block's accumulators is the integrator
`FUN_0042bec0`, whose sole caller is the render consumer `FUN_0042c0e0`. No function outside the
shake module stores a float at the block offsets.

⚠ **The `explosion` block's magnitude term never fills.** `FUN_0042bc10` looks the `explosion`
source up and then reads only the key `max_magnitude` (the string at `0x006215fc`) into `+0xc0`;
`shakes.zrd` authors `magnitude_factor` for that source instead, which this parser never asks for.
The slot therefore keeps the constructor's zero and the original's explosion shake has magnitude
zero. `PlaneShake.ExplosionAt` reads `max_magnitude` the same way and so kicks nothing.

⚠ **`camera+0x1c` holding `2.0` is the constructor default, not the live value.** The parse
overwrites it with the authored `fire_bullet` frequency, and the same applies to every block the
data authors. Any gain read off the defaults is a reading of an uninitialised camera.

## What a round taken kicks

`FUN_004b9bc0`, the per-pass take-hit body ([`vehicleDamage.md`](vehicleDamage.md)), sizes the kick
at `0x004b9c48`–`0x004b9d06` from the weapon's flags dword (`weapon+0x210`, bits in
[`ordnanceTypes.md`](ordnanceTypes.md)) and the two-float damage pair it was handed, then kicks at
`0x004b9d26` only when the victim is the player (`0x004b9d13`). In the order it tests them:

1. **`CANNON` (`0x40`): block 1, `CALIBER × bullet_impact.magnitude_factor`.** `FILD` of the
   extension's `+0x10` at `0x004b9c58` times `camera+0x68` (5e-4). The same quantity the fire path
   uses, and nothing else enters: a 40-calibre round taken is `2.0e-2`.
2. **Anything else: block 2, the larger of the damage pair times
   `missile_impact.magnitude_factor`** (`0x004b9c6e`–`0x004b9c92`, `camera+0x94`, 1e-3). The pair
   is what this pass delivers, so a blast victim's is already the falloff share,
   `t × ARMOR_DAMAGE` and `t × HEALTH_DAMAGE` ([`ordnanceTypes.md`](ordnanceTypes.md), "Half two,
   the splash"). A 40/60 HE rocket kicks with 60, not with its armour figure.
3. **`HIGH_EXPLOSIVE` (`0x400`) multiplies either of the two by `he_factor`** (`camera+0x98`, 2.0,
   `0x004b9caf` and `0x004b9cd7`). Its distance test against 400 only picks the AI twin's index;
   both arms apply the same factor.
4. **Otherwise `SHAKES_CAMERA` (`0x2000`) on the player replaces the kick with block 3**:
   `(weapon+0x40 − d²) × max_magnitude / weapon+0x40` (`0x004b9cef`–`0x004b9d06`), the same
   quadratic falloff as the splash, since `+0x40` is `IMPACT_PROXIMITY²` and `d²` the squared
   distance the caller passes. With `max_magnitude` unread it is zero, so the one weapon carrying
   the flag, `wep_26`, rocks the player by nothing.

Two consequences of where the kick sits:

- **It runs once per pass, and the wrapper loops.** `FUN_004b9b30` calls the body again with the
  unabsorbed leftover while both figures stay positive and health remains, so a round that
  overflows its zone kicks again with the smaller leftover (or the same caliber), adding another
  random velocity to the block. The port kicks once per round ("The rendered rotation", the port).
- **It runs before every early return of the body** except the dead, destructing and network
  guards at its top: an absorbed round, a no-damage victim, a `SONIC`, `FLASH`, `BEEPER` or
  `TANGLER` round and a round whose shooter is its victim all kick first. CSVM's disabling types
  never reach `TakeProjectileHit`, so they kick nothing; the shooter's own blast is dropped at the
  gather.

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
positions the consumer sums, so the original's rendered rotation from a saturated kick is set by
the damped spring and "The rendered rotation" rather than being `0.15` rad. `PlaneShake` runs it
as that block: a saturated kick's step is `0.15 × 2 × 2π` ≈ 1.88, a roll velocity uniform in
±2.36 rad/s, which the spring swings out to about 0.79 of `v/ω` with `ω = 4π`.

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

⚠ **Amplitude-call caution:** the 2.8e-3 rad is the clip's *rendered* RMS, not a kick. The law's
product `7e-5 × 40` is a velocity magnitude fed through block 0, and what renders is set by the
integrator and the doubling ("The rendered rotation"). Run through the decoded chain, a 3 s burst
of wep40 at 8/s renders a roll of about 2.7e-3 rad RMS and 8e-3 rad mean peak, with pitch and yaw
each about half that (`analysis/gun-wobble-shake/FINDINGS.md`, "The port"). That lands near the
clip's figure, which is consistency rather than a second derivation.

⚠ **The ×CALIBER multiplicand is confirmed; the downstream step is the block's own authored law.**
`FUN_0042be10` builds the step from the *parsed* block, and `shakes.zrd` authors `fire_bullet`
with `sawtooth 1` and `frequency 15.0`, so the waveform selector takes the **`4.0`** branch
(`0x00603514`) and `step = mag × 15 × 4 = 60·mag`, giving a per-shot velocity kick uniform in
**±75·mag rad/s** of roll (`(rand01−0.5) × step × 2.5` into `camera+0x2c`) and ±36·mag rad/s of
pitch and yaw (`× 1.2` into `camera+0x24`/`+0x28`). For wep40 the roll kick is ±0.21 rad/s, and
what renders is twice the position the sawtooth integrator builds out of it, not the kick.
A reading of this line that takes `camera+0x1c`'s `2.0` for a gain and the `6.2832` branch for
the waveform describes the constructor's uninitialised block rather than the parsed one.
`PlaneShake.FireBullet` kicks block 0 with exactly this law.

⚠ **That consumer is shared**, `high_speed` drives the IDENTICAL `FUN_0042be10` random-walk
accumulator (a second component, block index 4, at `camera+0xd4/+0xd8/+0xdc`) through the same
`FUN_0042c0e0`, and visibly wobbles in the clips, so the mechanism is live for both. Whether
`magnitude_factor` reads right against the original is the clip/fidelity judgment rather than the
decode, see `analysis/gun-wobble-shake/FINDINGS.md`.

**Nothing but the calibre enters.** The kick at `0x004b6e20`–`0x004b6e38` is the extension's
`CALIBER` times `camera+0x3c` and is pushed straight to `FUN_0042c070`, so no plane model, weight or
fire rate term exists for a capture to find. The impact sources' quantities are in "What a round
taken kicks" above.

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
pitch/yaw/roll per frame). So in the original both sources are the same 3-axis random-walk; they
differ only in block (0 vs 4), magnitude law (per-shot `magnitude_factor×CALIBER` vs per-frame
`(speedRatio−min_speed)/magnitude_quotient`), and cadence (fire once per shot @8/s; `high_speed`
every frame while over the gate). Full trace: `analysis/gun-wobble-shake/FINDINGS.md`.

**Ported as the original's own component block.** `PlaneShake` runs `high_speed` as block 4, like
every other source ("The rendered rotation", the port). Two readings this port takes and their
grounds:

- **The kick is per frame, as the original's is.** The original's frame rate therefore sets the
  drive, and so does ours, which is a rate dependence the original has too rather than one the
  port introduces. `PlaneShake.DiveRattleKickScale` defaults to `1`, the original's own kick, and
  is the knob to dial. `magnitude_quotient` and the authored `magnitude` are decode, not tuning.
- **Nothing scales the decoded magnitude.** The engine wires `(speedRatio − min_speed)/quotient`
  and the authored `0.05` exactly as parsed; the amount of roll that reaches the screen is
  whatever the integrator makes of them.

## The rattle SOUND is a gate at full level, not the authored ramp

The wobble above has an audio twin in the same per-frame function, and the two are separate
mechanisms that happen to share a speed regime. `FUN_0048c470` ends with a second overspeed block at
**`0x0048d209`**, thirty instructions past the `high_speed` kick, that starts and holds the
`snd_planeshake` loop. Three conditions stand over it and nothing else does:

- `0x0048d20e`, the vehicle is the one the camera is watching, `camera+0x150` (`DAT_0064ef78+0x150`).
  Not the player pointer `DAT_0071c298`, so a spectated aircraft rattles and the spectator's own
  does not;
- `0x0048d21a`, the rattle definition resolved at startup, `DAT_0071c334`;
- `0x0048d227`–`0x0048d242`, the speed test: `FLD [obj+0x934]` (true airspeed),
  `FLD [0x0071c344]` (the `rattle` block's `speed_range[0]`), `FMUL [obj+0x668]` (`fd_speed`),
  then `FCOMPP`, continuing only while **`speed_range[0] × fd_speed <= speed`**. On the shipped data
  that fraction is `1.0`, the airframe's own rated maximum.

Inside, `FUN_004a6330` wraps the vehicle as the emitter and `FUN_0045e470(emitter, token, def, 0.0,
0, 0)` at `0x0048d272` keeps the loop alive for this frame. That helper is a shared keep-alive
registry: it starts a sound the registry has no live handle for with `FUN_00593590(def, 1.0)` and
refreshes the entry's expiry to `now + param_4`, which here is `0.0`. `FUN_0045e410`, the per-frame
sweeper, stops every entry whose expiry has fallen behind the clock, so the loop dies within a frame
of the gate closing. **The call passes no volume of its own**, and `FUN_00593b80` resolves what
reaches the mixer as `def.VOLUME × masterSfxGain × callGain`, with `callGain` the hardcoded `1.0`.

⚠ **The `rattle` block's `volume_range` and its second speed are parsed and never read.**
`FUN_004735b0` reads the block at `0x00473a66`–`0x00473b65`: `volume_range` into `0x0071c33c` /
`0x0071c340` (defaults `0.25` / `1.0` at `0x00473a77` / `0x00473a81`, the shipped file writing `0.0`
/ `1.0`) and `speed_range` into `0x0071c344` / `0x0071c348` (defaults `1.0` / `2.0` at `0x00473a8b` /
`0x00473a95`, the shipped file writing `1.0` / `1.2`). Of those four globals only `0x0071c344` has a
reader anywhere in the executable, the `FLD` at `0x0048d22d` above; the other three carry a single
write xref each and no read. So the "volume 0→1 over `1.0`→`1.2× fd_speed`" the data invites is not a
law the game runs: the loop is **off below `1.0× fd_speed` and at full level from it upward**,
however deep the dive goes.

That full level is the engine slot's level. `snd_planeshake` authors no `VOLUME` field, so its base
gain is `1.0`, and the `engine_sound` volume curve is flat `1.0`, so both loops hand the same number
to the same gain chain. The rattle is a 3D definition (`RANGE 130`/`420`) emitted from the plane the
camera is watching, which sits inside the inner radius, so distance attenuation takes nothing off it.
**Ported** as `EngineAudioCurves.Rattle` against `PlaneStats.RattleSpeedGate`, driven by
`FlightAudio` on the pilot's own non-positional loop; the port keeps the per-player mix gain that
every own-ship loop takes in splitscreen, and gates on each human pilot rather than on one camera.
⚠ **One chosen departure:** the port plays the rattle at `1.3`, not the decoded `1.0`. At the
controls the level-with-the-engine loop read right in shape and audible but low, and the
original's output chain is not the remake's, so the gain is the user's ear rather than the decode.
The gate, the flat top and the absence of a ramp are still the decoded law.

## `nitro`, one kick per engage, the raw authored `magnitude`

The nitro source has no computed law at all. `FUN_004b2131`, the engage path, plays the AI twin
`FUN_00473430(1)` at `0x4b21b2`, then for the player only kicks block 6 with the value the parser
stored, `FUN_0042c070(6, *(camera+0x144))` at `0x4b21ce`, which is the authored `magnitude` `0.05`
unscaled by speed, plane or boost duration. There is one kick per engage rather than a per-frame
drive, so the whole wobble is the block ringing down on its own law afterwards.

That law is `frequency 4.0`, `damp 3.0`, `sawtooth 1`, so the step is `0.05 × 4 × 4 = 0.8` and the
kick is a roll velocity uniform in **±1.0 rad/s** (`±20` per unit magnitude, the `×2.5` component)
with pitch and yaw velocities each uniform in ±0.48 rad/s. Under the sawtooth integrator that
renders as a decaying triangle wave, reversing roughly every ten ticks at 60 Hz and losing about a
third of its amplitude per swing, so an engage reads as a wobble of about a second and a half whose
size differs from engage to engage because the kick is a single random draw. **The engage moves the
nose as well as the roll**: the same kick lands on the pitch and yaw components, and the plane node
renders all three at twice their positions ("The rendered rotation"). `PlaneShake.NitroEngaged`
wires it on every human pilot rather than a single player pointer, the same widening the contact
kick takes. The first swing of one engage peaks at about `2 × |v_roll|/16` rad of roll, up to
0.12 rad.

## Camera attachment

Every camera sits **above** `ShakePivot`, steered from the controller's pose (`_renderPose`, the
controller's attitude), and must never read the pivot. The chase, fixed and external cameras need
that or the wobble would rattle the 3rd-person view; the cockpit (mode 6) and nose (mode 7) views
take it from `FUN_0042d980`, which places the original's first-person camera from the vehicle's
own matrix rather than from the node the shake turns ("Where the wobble STATE lives" above). What
turns is the plane model, and the cockpit interior with it: CSVM's `--cockpit-pass` draws the
interior outside the pivot's subtree, so `CockpitOverlay.WobbledMount` applies the pivot's
rotation to it. The same split `damage_shakes` carves between the plane-rocking `aishake` and the
camera's own half.
