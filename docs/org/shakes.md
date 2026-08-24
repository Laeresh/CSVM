# Screen-shake and wobble behaviour, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), over 2026-08-18 / 2026-08-19, settling the
`BL-266` gun-wobble and overspeed-rattle half of the fire-rate backlog. Every claim names the
function or address it came from. No decompiler output is reproduced; the addresses are given so
any claim can be re-checked at source.

**Where the neighbours live.** The *authored* shake content — the six oscillator-source blocks of
`shakes.json` and the `ON_CALL` damage-shake animations of `damage_shakes.json` — is the shared
zrdr reader [`../formats/shakes.md`](../formats/shakes.md). This page is what the original does
with those laws: the per-shot/per-frame magnitudes, the accumulator they feed, the consumer that
rocks the plane, the camera attachment, and the port's fidelity gap.

## Two oscillators, one mechanism

In 3rd-person views of the original the **plane itself** wobbles against the world; cockpit/nose
views read as camera shake. One mechanism explains both — rock the plane node, and any
plane-mounted camera inherits the motion — mirroring how the `damage_shakes` defs rock the plane's
`healthy` node (there the camera gets its own authored half because the chase camera is not
rigidly attached). The engine implements exactly this: `PlaneShake` rolls a pivot the plane model
hangs under; physics and the camera never see it.

**The shake sources are the same 3-axis random-walk accumulator.** `fire_bullet` (per shot) and
`high_speed` (per frame) both run through `FUN_0042be10`, a random-walk that kicks three
camera-relative block accumulators `Δroll/pitch = (rand01−0.5)·fVar·1.2`, `Δyaw = …·2.5`. They
differ only in component block (0 vs 4), magnitude law, and cadence:

| | `fire_bullet` | `high_speed` |
|---|---|---|
| component block | 0 (`camera+0x24/+0x28/+0x2c` roll/pitch/yaw) | 4 (`camera+0xd4/+0xd8/+0xdc`) |
| magnitude law | `magnitude_factor × CALIBER` per shot | `(speedRatio − min_speed)/magnitude_quotient` per frame |
| cadence | once per shot at 8/s | every frame while over the gate |

## Where the wobble STATE lives vs. where it displaces — and the CONSUMER

The random-walk accumulators are written **onto the camera object**, not the plane —
`FUN_0048c470` (the per-frame player updater) reads `camera+0xec`/`+0xf0` for `high_speed` when
its `min_speed` gate trips and calls `FUN_0042c070(4, mag)`; the gun path calls
`FUN_0042c070(block, mag)` the same way. `FUN_0042c070` kicks `camera`-relative component blocks,
one per authored source, indexed in the parser's own order (the table in "The seven component
blocks" below). Each block's `[3]/[4]/[5]` (roll/pitch/yaw) are the **velocities** the kick adds
to; `[6]/[7]/[8]` are the **positions** that integrate from them and that the consumer sums.

The render consumer is **`FUN_0042c0e0`**: it walks the camera's **seven** component blocks
(0xb dwords = 0x2c bytes apart), runs the per-block spring-damper `FUN_0042bec0`, **sums all
blocks**' accumulated roll/pitch/yaw, transforms the total through the quaternion helpers
(`FUN_0053fbf0/f850/fa40/df30`) and applies it to the **plane node** `DAT_0071c304` via
`FUN_004d1a30`. Its only caller, the per-view render handler **`FUN_0042e5e0`**, calls it **first,
unconditionally, every frame — with no branch on the live mode byte `camera+0x14c`**. The mode
byte is used elsewhere only for FOV (mode 6→80°, `FUN_0042b660`), head-lock (mode 7), cockpit-
interior draw, and hiding scene nodes — **never to scale the wobble**. So **there is no per-view
dampening**: cockpit(6)/nose(7) inherit the full undampened wobble 1:1 from the plane node.

  *The "dampening" is per-**source**, not per-**view**: `FUN_0042bec0` runs a small damped-spring
  integrator per block — position `[6]/[7]/[8]` integrates from velocity `[3]/[4]/[5]` at dt=1/150,
  and velocity decays through `[1]`=frequency and `[2]`=damping — smoothing each random-walk
  source into a bounded wobble. Identical for every camera view.*

  *`camera+0x24` is therefore **not** a cockpit dampener — it is block 0's roll accumulator
  `[3]` (base `camera+0x18`). The earlier "static-trace NEGATIVE" on it (`BL-266`, `7f8881a0`)
  is now a POSITIVE: the reader is `FUN_0042c0e0`, which read it indirectly via the 7-block walk
  (a register-relative float add, not a direct `camera+0x24` load) — that indirection is why the
  original field search missed it.*

Because the first-person placement `FUN_0042d980` (modes 6/7, [`cameraViews.md`](cameraViews.md)) reads
the plane's basis directly and adds **no** wobble of its own at attachment, a plane-mounted camera
inherits the rocked rotation automatically — that is why cockpit/nose read as camera shake and the
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
| 5 | `+0xf4` | `turbulence` | none parsed; `+0x118` is never written | `FUN_0048d2c0` at `0x48d409` | one collision contact |
| 6 | `+0x120` | `nitro` | `magnitude` `+0x144` | `FUN_004b2131` at `0x4b21ce` | nitro engaged, player only (`PlaneShake.NitroEngaged`, every human pilot) |

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
`FUN_0048d2c0`, which computes its own per-collision magnitude, so the slot the design named is
in service as the collision oscillator. There is no per-frame, ungated caller of `FUN_0042c070` on
any block: of the five call sites, four are per-event (a round fired, damage taken, a contact, a
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

## `fire_bullet` — per-shot roll, `magnitude_factor × CALIBER`

Edge-traced in `crimson.exe` (`analysis/gun-wobble-shake/`). The consume site is the plane
per-tick firing loop `FUN_004b6820` (`FILD` weapons-ext `CALIBER` at `weapon+0x210 → +0x10` ×
`*(camera+0x3c)` = `magnitude_factor`, loaded raw by the `shakes.zrd` parser `FUN_0042bc10`;
camera = `DAT_0064ef78`).

A dead-astern chase clip of the original firing 40-cal slugs shows a roll-dominated wobble
(left/right wing vertical motion anti-correlated at −0.86) of 2.8e-3 rad RMS / ~4.0e-3 rad peak;
the dead-astern view makes the screen angle the world roll angle with no projection model.
Candidates: caliber 40 × 7e-5 = 2.8e-3 rad (match); damage 4.5 → 3.15e-4 (~9× under);
velocity 900 → 6.3e-2 (~16× over) — an order-of-magnitude discrimination, not a one-coincidence
match.

⚠ **Amplitude-call caution:** the 2.8e-3 rad is the clip's *rendered* RMS, not the oscillator's
*kick* amplitude — a kick of envelope `E` renders only ~0.28·E as RMS at the real 8/s fire rate
(sawtooth duty × damp envelope decay), so the engine renders ~0.28× the law's literal number
regardless of the clip (decoded: ~8e-4 rad RMS / ~0.20 px/frame at the ±205 px lever).

⚠ **The ×CALIBER multiplicand is confirmed, but the original's downstream gain is not yet
reconciled.** `FUN_0042be10` scales the `2.8e-3` product by the camera-shake-component gain (×2.0
at `camera+0x1c`) and the waveform factor, whose selector `*this == 0` takes the **`6.2832`**
branch (not the 4.0 sawtooth branch), so `fVar1 = mag × 2.0 × 6.2832 = 3.518e-2` and the per-shot
input is `(rand01−0.5) × fVar1 × 1.2` — **Δroll uniform in ±2.11e-2 rad/shot** for wep40, an order
larger than the raw law (the decode is closed-form from the binary — `FUN_0042be10` kicks the
`camera+0x24` accumulator, and its render-layer consumer is `FUN_0042c0e0` above — called
unconditionally by `FUN_0042e5e0` with **no camera-mode gate**, so there is **no per-view
dampening**; no live instrument needed).

⚠ **That consumer is shared** — `high_speed` drives the IDENTICAL `FUN_0042be10` random-walk
accumulator (a second component, block index 4, at `camera+0xd4/+0xd8/+0xdc`) through the same
`FUN_0042c0e0`, and visibly wobbles in the clips, so the mechanism is live for both. Whether
`magnitude_factor` needs a ~3.5× decode correction to match the original is the clip/fidelity
judgment, not the decode — see `analysis/gun-wobble-shake/FINDINGS.md`.

**Unmeasured residue:** whether plane model/weight also enter (single-plane, single-gun clip —
owed capture in `playtest.md`), and the impact sources' own quantities (the engine stands in
caliber for gun hits and armor damage for rockets, declared TUNE).

## `high_speed` — overspeed rattle, excess over the gate

`high_speed`'s input reads as speed normalised by the plane's rated max (`fd_speed`), so the
`min_speed` 1.0 gate means "beyond rated max" — the overspeed/dive rattle. Level cruise in the
firing clip shows a motionless idle floor (~0.01 px/frame), which an absolute-speed reading with
a gate at 1.0 m/s could not produce.

**The magnitude is the EXCESS over the gate, `(speedRatio − min_speed)/magnitude_quotient`**, not
the whole ratio (`PlaneShake.SetSpeedRatio`, decode correction 2026-08-18): the gate value is
*subtracted* from the numerator, so the rattle is zero at rated max (speedRatio 1.0, `min_speed`)
and ramps gently with overspeed, landing in the same order as the gun buzz in a dive. Reading it
as the whole `speedRatio/quotient` instead — the earlier wiring — snapped on at `1.0/70` rad the
moment you crossed rated max, 5× the entire 40-cal gun buzz, and barely ramped after (+27% over
the envelope); that is what the whole-ratio read did wrong. The overspeed audio layer
(`prop_sound`) engages in the same regime.

**`high_speed` shares the SAME random-walk accumulator as the gun** (decode 2026-08-19, from
`crimson.exe`: `FUN_0048c470`, the per-frame player updater, reads `camera+0xec` (`min_speed`) and
`camera+0xf0` (`magnitude_quotient`), and when the gate trips calls `FUN_0042c070(4, mag)` — the
exact same dispatcher/accumulator the `fire_bullet` path uses, just component index 4 instead of 0:
`this = camera + 4·0x2c + 0x18`, kicking the three block-4 accumulators `camera+0xd4/+0xd8/+0xdc`
roll/pitch/yaw per frame). So in the original both sources are the same 3-axis random-walk; they
differ only in block (0 vs 4), magnitude law (per-shot `magnitude_factor×CALIBER` vs per-frame
`(speedRatio−min_speed)/magnitude_quotient`), and cadence (fire once per shot @8/s; `high_speed`
every frame while over the gate). The engine's current `PlaneShake._speed` (deterministic damped
sawtooth) is therefore a *different mechanism* from the original — the root cause of `BL-266(d)`'s
"6× muted dive." The fidelity fix is a second random-walk accumulator (a `_fire` clone) fed by the
existing excess-over-gate `SetSpeedRatio` law. Full trace: `analysis/gun-wobble-shake/FINDINGS.md`.

## Camera-attachment rule for our port

Mount the cockpit (mode 6) and nose (mode 7) cameras as children of the **plane model** (below
`ShakePivot`) so they inherit the wobble for free — the original's 6/7 pair are the *same*
`cockpit_camera` point and both ride the rocking plane, so the two first-person views are not
handled differently from each other. The chase/fixed/external cameras stay **top-level**, steered
from the controller's pose (`_renderPose` = controller attitude), *above* the pivot — they must
never read `ShakePivot`, or the `damage_shakes`-style rock would rattle the 3rd-person view too.
When the first-person views land, they go **below** the pivot (inherit); every current camera sits
**above** it (opt out) — the same split `damage_shakes` carves between the plane-rocking `aishake`
and the camera's own half.
