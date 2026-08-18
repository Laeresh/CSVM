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
`FUN_0042c070(block, mag)` the same way. `FUN_0042c070` kicks `camera`-relative component blocks
(fire block 0, high_speed block 4, impact block 5). Each block's `[3]/[4]/[5]`
(roll/pitch/yaw) are the positions; `[6]/[7]/[8]` are velocity.

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
  integrator per block — velocity `[6]/[7]/[8]` integrates from position `[3]/[4]/[5]` at dt=1/150,
  and position decays through `[1]`=frequency and `[2]`=damping — smoothing each random-walk
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
