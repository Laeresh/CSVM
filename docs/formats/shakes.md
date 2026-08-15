# shakes.json / damage_shakes.json — the plane-wobble / camera-shake laws

Two of the shared-scope zrdr readers ([zrdr.md](zrdr.md)); in an extraction they land as
`extracted/zrdr/shakes.zrd.json` and `extracted/zrdr/damage_shakes.zrd.json`. First read
The engine reads the engine reads `shakes.json` through
`ShakeDefs` and plays five of the six sources through `PlaneShake` as visual-only roll on the
plane node; `damage_shakes.json` stays unconsumed (unknown caller, below).

## At a glance

This page is the current reference for its documented format family.

## At a glance

This page is the current reference for its documented format family.

## `shakes.json` — six oscillator sources

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

**`fire_bullet`'s magnitude is `magnitude_factor × CALIBER`, in radians of roll — measured, not
inferred** (`analysis/gun-wobble-shake/`). A dead-astern chase clip of the original
firing 40-cal slugs shows a roll-dominated wobble (left/right wing vertical motion
anti-correlated at −0.86) of 2.8e-3 rad RMS / ~4.0e-3 rad peak; the dead-astern view makes the
screen angle the world roll angle with no projection model. Candidates: caliber 40 × 7e-5 =
2.8e-3 rad (match); damage 4.5 → 3.15e-4 (~9× under); velocity 900 → 6.3e-2 (~16× over) — an
order-of-magnitude discrimination, not a one-coincidence match. Unmeasured residue: whether
plane model/weight also enter (single-plane, single-gun clip — owed capture in `playtest.md`),
and the impact sources' own quantities (the engine stands in caliber for gun hits and armor
damage for rockets, declared TUNE).

**What the oscillators displace**: in 3rd-person views of the original the **plane itself**
wobbles against the world; cockpit/nose views read as camera shake. One mechanism explains
both — rock the plane node, and any plane-mounted camera inherits the motion — mirroring how
the `damage_shakes` defs below rock the plane's `healthy` node (there the camera gets its own
authored half because the chase camera is not rigidly attached). The engine implements exactly
this: `PlaneShake` rolls a pivot the plane model hangs under; physics and the camera never see
it.

**`high_speed`'s input reads as speed normalised by the plane's rated max** (`fd_speed`), so the
`min_speed` 1.0 gate means "beyond rated max" — the overspeed/dive rattle. Level cruise in the
firing clip shows a motionless idle floor (~0.01 px/frame), which an absolute-speed reading with
a gate at 1.0 m/s could not produce. Unverified against a calibrated dive measurement; the
overspeed audio layer (`prop_sound`) engages in the same regime.

## `damage_shakes.json` — ON_CALL shake animation defs

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

## The `SHAKES_CAMERA` weapon flag

Exactly **one** of the 48 weapons carries it: `wep_26` "FW" (`MSG_WEAP_FAKE_WEAPON`) — a
zero-damage scripted rocket with `IMPACT_PROXIMITY` 100 ([weapons.md](weapons.md)). No player
loadout mounts it. So the flag is **not** the player-gunfire shake mechanism (that would be the
`fire_bullet` source above, which no flag gates) — it reads as "this scripted weapon's
detonation shakes the camera", presumably through the `explosion`/`missile_impact` source, for
missions that rattle the player without hurting them.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
