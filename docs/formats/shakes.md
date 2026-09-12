# Screen-shake readers - `shakes.json` and `damage_shakes.json`

Part of the [format documentation](README.md). These shared zrdr readers define aircraft wobble
and camera-shake laws. `ShakeDefs` reads `shakes.json`; `PlaneShake` applies five of its six
sources as visual-only roll. `damage_shakes.json`'s `ON_CALL` defs are played by the exe's own
shake player, one arm of it wired here (below).

⚠ Where the original **consumes** these laws, the per-shot/per-frame shake magnitudes, the
random-walk accumulator they feed, the camera-attachment rule, and the engine's fidelity gap, is
a `crimson.exe` decode and lives in [`../org/shakes.md`](../org/shakes.md). This page is the
reader reference only.

## Oscillator sources

A plain alternating `name, properties` list. Each block is one shake *source* with an oscillator
law, frequency, damping, waveform, and a magnitude term:

| Source | `frequency` | `damp` | `sawtooth` | magnitude term |
|---|---|---|---|---|
| `fire_bullet` | 15.0 | 12.5 | 1 | `magnitude_factor` 7e-5 |
| `bullet_impact` | 2.2 | 14.0 | 0 | `magnitude_factor` 5e-4 |
| `missile_impact` | 2.2 | 6.5 | 0 | `magnitude_factor` 1e-3, `he_factor` 2.0 |
| `explosion` | 2.2 | 8.0 | 0 | `magnitude_factor` 5e-4 |
| `high_speed` | 15.0 | 12.5 | 1 | `magnitude_quotient` 70.0, `min_speed` 1.0 |
| `nitro` | 4.0 | 3.0 | 1 | `magnitude` 0.05 (absolute) |

Six sources is the whole file, and there is no per-campaign, per-mission or per-airframe override
of it anywhere in the extracted tree. The original's parser looks for a **seventh** source,
`turbulence`, which no shipped data authors and which carries no magnitude key even in the parser;
its block serves the collision shake instead, on the constructor's law (frequency 2.0, damp 4.5, no
sawtooth) and a magnitude the collision function computes rather than reads. A `turbulence` block
added to this file would therefore retune the collision shake and author no ambient jostle. See
[`../org/shakes.md`](../org/shakes.md), "Ambient turbulence does not ship" and "Block 5, the
per-contact kick".

Readings with confidence: `frequency` in Hz, `damp` a decay rate (the impulse sources die fast),
`sawtooth` selects the waveform (1 = the buzzy sources: firing, speed rattle, nitro), and
`high_speed`'s magnitude is its driving quantity divided by `magnitude_quotient` (its input is
self-evidently airspeed, it carries a `min_speed` gate). `nitro` is the only source with an
**absolute** magnitude, 0.05. The `fire_bullet` and `high_speed` magnitude *laws*, what the
drive quantity actually is per shot / per frame, are decoded from `crimson.exe` in
[`../org/shakes.md`](../org/shakes.md).

## Damage-shake animations

Standard `ANIMATION_DEFINITIONS` ([anim-definitions.md](anim-definitions.md)), all `ON_CALL`:
`large`/`medium`/`small_camshake` on `NAME player`, and `large`/`medium`/`small_aishake` on
`NAME bloodhawk`. Each camshake def is two sequences: `camera_shake` rocking node `camera1` by
`XYZ_ROTATION` (large ±(0, 3.2, 32), medium ±(0, 1.6, 16), small ±(0, 0.6, 4.5)) over
`RUN_TIME` 0.05 s (0.04 for small) each way, `LOOP` 3, a ~0.3 s wobble, and a `player_shake`
rocking the plane's `healthy` node the same way. The aishake defs carry only the plane-rocking
half.

The triggers are decoded, and they are the AI half of the camera shake rather than script calls:
one player (`FUN_00473430`) plays the three defs by index on any vehicle that is not the player's,
from the same five sites that kick the player's own oscillator blocks (a round fired, a round
taken, overspeed, a collision contact, a nitro engage). The decode, with the addresses and the
one-at-a-time handle, is in [`../org/shakes.md`](../org/shakes.md), "The seven component blocks and
every kicker". CSVM wires the nitro engage; the other four sites are not wired.

## Weapon camera-shake flag

Exactly **one** of the 48 weapons carries it: `wep_26` "FW" (`MSG_WEAP_FAKE_WEAPON`), a
zero-damage scripted rocket with `IMPACT_PROXIMITY` 100 ([weapons.md](weapons.md)). No player
loadout mounts it. So the flag is **not** the player-gunfire shake mechanism (that would be the
`fire_bullet` source above, which no flag gates), it reads as "this scripted weapon's
detonation shakes the camera", presumably through the `explosion`/`missile_impact` source, for
missions that rattle the player without hurting them.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
