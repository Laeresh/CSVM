# shakes.json / damage_shakes.json — the camera-shake laws

Two of the shared-scope zrdr readers ([zrdr.md](zrdr.md)); in an extraction they land as
`extracted/zrdr/shakes.zrd.json` and `extracted/zrdr/damage_shakes.zrd.json`. First read
2026-08-05 (`BL-266`). **Nothing in the engine consumes either file yet** — this page records
what they hold and what still blocks an implementation.

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

⚠ **What `magnitude_factor` multiplies is not authored, and is the blocker.** Each factor-based
source scales some per-event quantity — for `fire_bullet` the candidates (the round's damage,
caliber, or muzzle velocity) differ by orders of magnitude in the result, and several land
suggestively near `nitro`'s 0.05 — the classic one-coincidence trap (`BL-248`(d)). Undecidable
from the data alone; wants a shake-amplitude measurement off original footage
(`Gun Wobble and animation.mp4` is a candidate source for `fire_bullet`).

## `damage_shakes.json` — ON_CALL shake animation defs

Standard `ANIMATION_DEFINITIONS` ([anim-definitions.md](anim-definitions.md)), all `ON_CALL`:
`large`/`medium`/`small_camshake` on `NAME player`, and `large`/`medium`/`small_aishake` on
`NAME bloodhawk`. Each camshake def is two sequences: `camera_shake` rocking node `camera1` by
`XYZ_ROTATION` (large ±(0, 3.2, 32), medium ±(0, 1.6, 16), small ±(0, 0.6, 4.5)) over
`RUN_TIME` 0.05 s (0.04 for small) each way, `LOOP` 3 — a ~0.3 s wobble — and a `player_shake`
rocking the plane's `healthy` node the same way. The aishake defs carry only the plane-rocking
half. The motions are fully authored in the runtime's settled `XYZ_ROTATION` semantics;
⚠ **the triggers are not** — which damage magnitude calls `small` vs `medium` vs `large` (and
what calls them at all) lives in the exe. Wiring them needs either original footage of being hit
or an accepted TUNE threshold set, recorded as such.

## The `SHAKES_CAMERA` weapon flag

Exactly **one** of the 48 weapons carries it: `wep_26` "FW" (`MSG_WEAP_FAKE_WEAPON`) — a
zero-damage scripted rocket with `IMPACT_PROXIMITY` 100 ([weapons.md](weapons.md)). No player
loadout mounts it. So the flag is **not** the player-gunfire shake mechanism (that would be the
`fire_bullet` source above, which no flag gates) — it reads as "this scripted weapon's
detonation shakes the camera", presumably through the `explosion`/`missile_impact` source, for
missions that rattle the player without hurting them.
