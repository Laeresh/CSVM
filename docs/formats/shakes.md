# Screen-shake readers - `shakes.json` and `damage_shakes.json`

Part of the [format documentation](README.md). These shared zrdr readers define aircraft wobble
and camera-shake laws. `ShakeDefs` reads `shakes.json`; `PlaneShake` applies five of its six
sources as visual-only roll. The caller for `damage_shakes.json` remains unknown.
## Oscillator sources

A plain alternating `name, properties` list. Each block is one shake *source* with an oscillator
law — frequency, damping, waveform — and a magnitude term:

| Source | `frequency` | `damp` | `sawtooth` | magnitude term |
|---|---|---|---|---|
| `fire_bullet` | 15.0 | 12.5 | 1 | `magnitude_factor` 7e-5 |
| `bullet_impact` | 2.2 | 14.0 | 0 | `magnitude_factor` 5e-4 |
| `missile_impact` | 2.2 | 6.5 | 0 | `magnitude_factor` 1e-3, `he_factor` 2.0 |
| `explosion` | 2.2 | 8.0 | 0 | `magnitude_factor` 5e-4 |
| `high_speed` | 15.0 | 12.5 | 1 | `magnitude_quotient` 70.0, `min_speed` 1.0 |
| `nitro` | 4.0 | 3.0 | 1 | `magnitude` 0.05 (absolute) |

Readings with confidence: `frequency` in Hz, `damp` a decay rate (the impulse sources die fast),
`sawtooth` selects the waveform (1 = the buzzy sources: firing, speed rattle, nitro), and
`high_speed`'s magnitude is its driving quantity divided by `magnitude_quotient` (its input is
self-evidently airspeed — it carries a `min_speed` gate). `nitro` is the only source with an
**absolute** magnitude, 0.05.

**`fire_bullet`'s magnitude is `magnitude_factor × CALIBER`, in radians of roll — edge-traced in
`crimson.exe`** (`analysis/gun-wobble-shake/`). The consume site is the plane per-tick firing
loop `FUN_004b6820` (`FILD` weapons-ext `CALIBER` at `weapon+0x210 → +0x10` × `*(camera+0x3c)`
=`magnitude_factor`, loaded raw by the `shakes.zrd` parser `FUN_0042bc10`; camera =
`DAT_0064ef78`). A dead-astern chase clip of the original firing 40-cal slugs shows a
roll-dominated wobble (left/right wing vertical motion anti-correlated at −0.86) of 2.8e-3 rad
RMS / ~4.0e-3 rad peak; the dead-astern view makes the screen angle the world roll angle with
no projection model. Candidates: caliber 40 × 7e-5 =
2.8e-3 rad (match); damage 4.5 → 3.15e-4 (~9× under); velocity 900 → 6.3e-2 (~16× over) — an
order-of-magnitude discrimination, not a one-coincidence match. ⚠ **Amplitude-call caution:**
the 2.8e-3 rad is the clip's *rendered* RMS, not the oscillator's *kick* amplitude — a kick of
envelope `E` renders only ~0.28·E as RMS at the real 8/s fire rate (sawtooth duty × damp
envelope decay), so the engine renders ~0.28× the law's literal number regardless of the clip
(decoded: ~8e-4 rad RMS / ~0.20 px/frame at the ±205 px lever). ⚠ **The ×CALIBER multiplicand is
confirmed, but the original's downstream gain is not yet reconciled** — `FUN_0042be10` scales the
`2.8e-3` product by the camera-shake-component gain (×2.0 at `camera+0x1c`) and the waveform
factor, whose selector `*this == 0` takes the **`6.2832`** branch (not the 4.0 sawtooth branch),
so `fVar1 = mag × 2.0 × 6.2832 = 3.518e-2` and the per-shot input is
`(rand01−0.5) × fVar1 × 1.2` — **Δroll uniform in ±2.11e-2 rad/shot** for wep40, an order larger
than the raw law (see `analysis/gun-wobble-shake/FINDINGS.md`; the decode is closed-form from the
binary — `FUN_0042be10` kicks the `camera+0x24` accumulator, and its render-layer consumer is
`FUN_0042c0e0` (walks the 7 blocks, springs them via `FUN_0042bec0`, sums roll/pitch/yaw, rocks the
plane node `DAT_0071c304`) — called unconditionally by `FUN_0042e5e0` with **no camera-mode gate**,
so there is **no per-view dampening**; no live instrument needed). ⚠ **That consumer is shared —
the `high_speed` note below drives the IDENTICAL `FUN_0042be10` random-walk accumulator (a
second component, block index 4, at `camera+0xd4/+0xd8/+0xdc`) through the same `FUN_0042c0e0`, and
visibly wobbles in the clips, so the mechanism is live for both.** Whether `magnitude_factor` needs a ~3.5× decode
correction to match the original is the clip/fidelity judgment, not the decode — see
`analysis/gun-wobble-shake/FINDINGS.md`. Unmeasured residue: whether plane model/weight also
enter (single-plane, single-gun clip — owed capture in `playtest.md`), and the impact sources'
own quantities (the engine stands in caliber for gun hits and armor damage for rockets, declared
TUNE).

**What the oscillators displace**: in 3rd-person views of the original the **plane itself**
wobbles against the world; cockpit/nose views read as camera shake. One mechanism explains
both — rock the plane node, and any plane-mounted camera inherits the motion — mirroring how
the `damage_shakes` defs below rock the plane's `healthy` node (there the camera gets its own
authored half because the chase camera is not rigidly attached). The engine implements exactly
this: `PlaneShake` rolls a pivot the plane model hangs under; physics and the camera never see
it.

⚠**Where the wobble STATE lives vs. where it displaces — and the CONSUMER (2026-08-19, consumer
found):** the random-walk accumulators are written **onto the camera object**, not the plane —
`FUN_0048c470` / the gun path call `FUN_0042c070(block, mag)`, which kicks `camera`-relative
component blocks (fire block 0 `+0x24/+0x28/+0x2c`, high_speed block 4 `+0xd4/+0xd8/+0xdc`,
impact block 5). Each block's `[3]/[4]/[5]` (roll/pitch/yaw) are the positions; `[6]/[7]/[8]` are
velocity. The render consumer is **`FUN_0042c0e0`**: it walks the camera's **seven** component
blocks (0xb dwords = 0x2c bytes apart), runs the per-block spring-damper `FUN_0042bec0`, **sums all
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

Because the first-person placement `FUN_0042d980` (modes 6/7, `docs/org/cameraViews.md`) reads
the plane's basis directly and adds **no** wobble of its own at attachment, a plane-mounted camera
inherits the rocked rotation automatically — that is why cockpit/nose read as camera shake and the
two cockpit views need no separate handling.

**Camera-attachment rule for our port**: mount the cockpit (mode 6) and nose (mode 7) cameras as
children of the **plane model** (below `ShakePivot`) so they inherit the wobble for free — the
original's 6/7 pair are the *same* `cockpit_camera` point and both ride the rocking plane, so the
two first-person views are not handled differently from each other. The chase/fixed/external
cameras stay **top-level**, steered from the controller's pose (`_renderPose` = controller
attitude), *above* the pivot — they must never read `ShakePivot`, or the `damage_shakes`-style
rock would rattle the 3rd-person view too. When the first-person views land, they go **below** the
pivot (inherit); every current camera sits **above** it (opt out) — the same split `damage_shakes`
carves between the plane-rocking `aishake` and the camera's own half.

**`high_speed`'s input reads as speed normalised by the plane's rated max** (`fd_speed`), so the
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
roll/pitch/yaw per frame). So in the original both sources are the same 3-axis random-walk
(`Δroll/pitch = (rand01−0.5)·fVar·1.2`, `Δyaw = …·2.5`); they differ only in block (0 vs 4),
magnitude law (per-shot `magnitude_factor×CALIBER` vs per-frame
`(speedRatio−min_speed)/magnitude_quotient`), and cadence (fire once per shot @8/s; `high_speed`
every frame while over the gate). The engine's current `PlaneShake._speed` (deterministic damped
sawtooth) is therefore a *different mechanism* from the original — the root cause of `BL-266(d)`'s
"6× muted dive." The fidelity fix is a second random-walk accumulator (a `_fire` clone) fed by the
existing excess-over-gate `SetSpeedRatio` law. Full trace: `analysis/gun-wobble-shake/FINDINGS.md`.

## Damage-shake animations

Standard `ANIMATION_DEFINITIONS` ([anim-definitions.md](anim-definitions.md)), all `ON_CALL`:
`large`/`medium`/`small_camshake` on `NAME player`, and `large`/`medium`/`small_aishake` on
`NAME bloodhawk`. Each camshake def is two sequences: `camera_shake` rocking node `camera1` by
`XYZ_ROTATION` (large ±(0, 3.2, 32), medium ±(0, 1.6, 16), small ±(0, 0.6, 4.5)) over
`RUN_TIME` 0.05 s (0.04 for small) each way, `LOOP` 3 — a ~0.3 s wobble — and a `player_shake`
rocking the plane's `healthy` node the same way. The aishake defs carry only the plane-rocking
half. The motions are fully authored in the runtime's settled `XYZ_ROTATION` semantics;
⚠ **the triggers are not** — what calls `small` vs `medium` vs `large` (and what calls them at
all) lives in the exe, and these defs stay **unwired**. The routine being-hit feedback does not
need them: the `bullet_impact`/`missile_impact`/`explosion` oscillator sources above scale
continuously per hit, so the ON_CALL defs read as script/set-piece calls. Wiring them waits for
footage of whatever actually invokes them.

## Weapon camera-shake flag

Exactly **one** of the 48 weapons carries it: `wep_26` "FW" (`MSG_WEAP_FAKE_WEAPON`) — a
zero-damage scripted rocket with `IMPACT_PROXIMITY` 100 ([weapons.md](weapons.md)). No player
loadout mounts it. So the flag is **not** the player-gunfire shake mechanism (that would be the
`fire_bullet` source above, which no flag gates) — it reads as "this scripted weapon's
detonation shakes the camera", presumably through the `explosion`/`missile_impact` source, for
missions that rattle the player without hurting them.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
