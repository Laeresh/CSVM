# Effects: `PUFFER_STATE` emitters, effect readers, flipbook textures

Part of the [format documentation](README.md). Covers the original's fully data-driven
effect system (surveyed 2026-07-14 for the crash sequence + damage trails; no binary anim
format needed for any of it). Consumed by `CSVM/src/Effects/Puffer.cs`.

The system has three layers, all in zrdr + the chapter `texture.zbd`:

1. **Sequencing scripts** — `ANIMATION_DEFINITIONS` readers (`ai_plane_destruct.json`,
   `player_plane_destruct.json`) defining per-plane, surface-sensitive crash sequences
   (`player_crash_default`/`_dirt`/`_water`) that toggle model subtrees
   (healthy→destroyed), play named sounds at nodes, and `CALL_ANIMATION` into effect
   scripts. Schema in [anim-definitions.md](anim-definitions.md).
2. **Particle emitters** — `PUFFER_STATE` blocks (below) in `flame_ball.json` (16 states,
   e.g. `large_fireball`), `fire.json` (`fire_n_smoke` sustained wreck fire),
   `pufftrails.json` (`dense_firetrail` smoke/fire pair, `short_firetrail` panel trails),
   `flak_trails.json`, `zepskinfire.json`, plus inline states in vehicle anims (the
   train's steam in `train.json`).
3. **Flipbook textures** in the chapter `texture.zbd`: `fire_f01–06` (fireball),
   `fire101–112` (sustained fire), `smoke101–103`, `exp_yel01`/`exp_red01`/`exp_blu02`/
   `exp_gre01` (flash sprites), `fireflare1`.

The gamez effect-prototype nodes (`ball_of_fire`, `flak_explosion`, `dense_firetrail`,
`explode_here1–9`, …) are mostly empty parentless Object3d anchors the emitters attach
to at runtime — they are not world scenery (see [world-structure.md](world-structure.md)).

## `PUFFER_STATE` schema

A state is a **fully-defined emitter iff it has `NUMBER` (burst) or `DISTANCE_INTERVAL`
(trail)**; the readers also hold *stop stubs* sharing the same `NAME` but carrying only
`ACTIVE_STATE` — skip those when looking a definition up by name.

| Key | Value | Meaning |
|---|---|---|
| `NAME` | string | referenced by anims' `PUFFER_STATE` calls |
| `AT_NODE` | `[nodeName, dx?, dy?, dz?]` | attach point; the optional trailing offset is in the host node's own frame, same convention as `LOCAL_VELOCITY` — what spreads C1's three waterfall splash puffers ±11 m either side of the shared anchor `waterfall01` instead of stacking them on one point (2026-07-21 fix: the offset was parsed nowhere and silently dropped, in both the compiled-event and reader-event front-ends — 862 of 4387 PUFFER_STATE events in this install carry a non-zero one) |
| `NUMBER` | int | burst mode: sprites spawned per `TIME_INTERVAL` |
| `TIME_INTERVAL` | s | burst spawn period (default 0.1) |
| `DISTANCE_INTERVAL` | m | trail mode: one sprite per N meters of the followed node's motion (`dense_firetrail`: smoke 1.0 m / fire 0.25 m) |
| `LOCAL_VELOCITY` / `WORLD_VELOCITY` | xyz | initial velocity, emitter-local / world frame |
| `MIN_RANDOM_VELOCITY` / `MAX_RANDOM_VELOCITY` | xyz | per-sprite random velocity range |
| `WORLD_ACCELERATION` | xyz | constant acceleration (buoyant smoke rises) |
| `FRICTION` | float | velocity damping |
| `SIZE_RANGE` | [min, max] m | initial sprite size |
| `LIFETIME_RANGE` | [min, max] s | sprite life |
| `GROWTH_FACTOR` | float | size growth over life |
| `DEVIATION_DISTANCE` | m | positional jitter |
| `TEXTURE_SEQUENCE` | [(name, t)…] | **flipbook**: frames keyed by **fraction of the sprite's own lifetime**, 0–1 — *not* seconds (see below) |
| `TEXTURES` | [name…] | **static pool**: each sprite picks one at random (smoke101/102/103) |
| `COLORS` | [[lifeFrac, r, g, b, a]…] | colour-over-age ramp; rgb dual-encoded (the [weather.md](weather.md) rule: any component > 1 ⇒ ÷255), alpha 0–1. `dense_firetrail`'s smoke is born orange (255,164,90) → near-black |
| `FADE_RANGE` | [near, far] m | camera-distance fade (`fire_n_smoke`: 1500–1700). Parsed by mech3ax, **not implemented** — set on 400 of C1's 721 events |
| `NEAR_FADE` | [a, b] | near-camera fade (70/20 almost everywhere). The compiled payload calls it `unk_range`. Parsed, **not implemented** |
| `START_AGE` / `WIND_FACTOR` / `PRIORITY` | — | carried by 11 / 6 / 11 C1 events, **not implemented** |

**`TEXTURE_SEQUENCE` times are lifetime fractions, not seconds.** Across all 1,750 flipbook
`PufferState` events in this install the largest key is **0.8** and none exceeds 1.0 — while
`LIFETIME_RANGE` maxima run from 0.2 s to 5.5 s, so under a seconds reading nobody ever wrote a
sequence longer than 0.8 s for a 5.5 s sprite, 1,750 times running; and the 16 `mag_gunhit`
`firepuffer` events key frames out to 0.5 with a 0.1–0.2 s lifetime, which under that reading
could never draw at all. Read as seconds, `large_30sec_fire`'s `fire_n_smoke` burned
`fire_f01 → fire_f06` in a quarter second and then held the near-black smoke frame for the
other ~95 % of a 3.5–5.5 s life — which is what collapsed the game's most-called death effect
into a stationary red ball instead of a climbing flame.

Rendering note (measured, remake convention): the authored data never states a blend mode, so
it is **derived from the sprite a particle dies on** — the last flipbook frame, or the mean of a
static pool. Near-black ⇒ alpha-blend, else additive. A COLORS ramp forces alpha-blend too (its
own alpha ends at 0, and a near-black smoke ramp is invisible additively). The measured
population separates cleanly: alpha-weighted mean luminance is 0.004 for `thickblksmoke*` and
0.018 for `fire_f06`, then nothing until 0.12 (`fire101`), 0.17 (`exp_yel01`), 0.22 (`smoke101`)
and 0.34 (`fire_f01`) — so white smoke and flashes stay additive and only genuinely black
sprites flip. A presence-of-COLORS rule alone was not enough: `fire_n_smoke` and
`large_black_smokeball` both carry `colors: null` yet end on black sprites, and adding those
turned every dying smoke puff into more glow.

## Aircraft speed-cue wisps

Each chapter's `speed_cue.zrd` authors the pale puffs that appear ahead of the player's aircraft.
Although the animation and its three puffer sequences are `ON_CALL`, player setup starts
`speed_cue`; its controller then loops every 0.1 s. This is why a census that excludes `ON_CALL`
puffers incorrectly misses an ambient effect.

The controller disables the effect within 50 m of the ground. By camera altitude it selects a
30 m interval / 18 m deviation puffer below 800 m, 15 m / 25 m from 800–900 m, 8 m / 30 m from
900–1200 m, and 15 m / 25 m from 1200–1500 m. Each emitter is attached to `player` at local
`(0,0,-60)`, 60 m ahead of the aircraft. All use zero base velocity, ±0.8 m/s random velocity,
2.5–4.5 m initial size, 3–4 s lifetime, growth 1.25, and random static
`smoke101`/`smoke102`/`smoke103` textures. Their colour ramp is transparent white → low-alpha
white at half-life → transparent black. C1 uses peak alpha 0.4/0.5/0.5; C4 uses 0.6/0.7/0.7.

The generic distance-puffer update leaves emitted particles in world space. Consequently the
aircraft passes through each puff, and its screen-visible duration falls approximately inversely
with airspeed. This effect is separate from both chapter cloud-card populations and the
hard-coded throttle-rise exhaust below. CSVM implements it in `Flight.SpeedCue`, loading the
chapter reader verbatim and assigning one private renderer set to each player rig.

## Hard-coded aircraft throttle-rise exhaust

`crimson.exe` also constructs one puffer outside the authored `PUFFER_STATE` readers. Aircraft
initialization (`FUN_00476250`) resolves `exhaust%d` model nodes (`exhaust1`, `exhaust2`, …)
and calls `FUN_004af9e0` → `FUN_004afa20` once per node. The latter allocates a generic puffer
through `FUN_00550100` and attaches it at zero local offset.

It is a distance trail: one particle per **0.4 m**, random velocity **−0.1..+0.1 m/s** on each
axis, initial size **0.2..0.3 m**, lifetime **0.5..1.5 s**, friction **1.2**, deviation **0.001 m**,
and normalized scale **1.0 → 3.45** over life. Each particle chooses `smoke101`, `smoke102` or
`smoke103`; the colour ramp is near-black at age 0.2 and transparent black at age 1, with a
**200..300 m** distance fade. `FUN_004afbc0` activates it from a positive
commanded-versus-current throttle gap and otherwise decays it to off. The generic puffer update
`FUN_0054ee10` → `FUN_0054f8b0` samples each exhaust node's world transform and leaves emitted
particles in world space, which is why they pass behind the moving aircraft.

This is the executable counterpart of the throttle-rise smoke described in
`CSVM/src/Flight/ThrottleSlamSmoke.cs`, not the speed-cue wisp system. The full BL-317 trace is in
`analysis/bl-317-plane-wisps/FINDINGS.md`.


## Texture flipbooks, layer by layer (2026-07-21)

The install animates textures through **three** distinct mechanisms. They are easy to confuse
because they share the same frame sets (`fire101-112` etc.), so:

1. **Puffer flipbooks** - `PUFFER_STATE`'s `TEXTURES`/`TEXTURE_SEQUENCE`, played per *particle*.
   Implemented (`src/Effects/Puffer.cs`); this is what animates crash fireballs and damage trails.
2. **Material cycles** - a gamez material's own `cycle` block: `texture_indices` (the frame
   list), `speed` (fps), `looping`. Played on the *surface*. Implemented 2026-07-21
   (`src/Mech3/TextureCycler.cs`). Only 1-7 materials per chapter carry one, but they cover the
   animated sea: C1B has `wtr00000` x16 @10 fps over 695 polygons and `srf0001` x16 @9 over 375,
   plus `wakefront1` x5 @12 (boat wakes) and `turb01` x6 @12 (turbulence); C1 has `splash01` x3
   @4 and the `bmanwalk`/`bmanrun` x6 @9 crowd sprites. `ObjectCycleTexture{name, reset}` (144
   events, carrying no frame list of its own) is the anim-side trigger for these - still
   unimplemented, so cycles currently run free rather than being started/reset by animation.
3. **The `EFFECTS` reader** - `extracted/zrdr/effects.zrd.json`, the same idea bound to a NODE:

   ```
   ["fire1.flt", "NAME", ["fire1"], "SPEED", [10.0], "LOOPING", ["ON"],
    "MAPS", ["fire101.tif", ..., "fire112.tif"]]
   ["fire2.flt", "NAME", ["fire2"], "SPEED", [5.0],  "LOOPING", ["ON"],
    "MAPS", ["fire101.tif", ..., "fire106.tif"]]
   ```

   Exactly two entries exist, and no compiled anim definition anywhere has a non-null `effects`
   array, so the binding is by node name from this reader alone. `fire1`/`fire2` are single
   `Facade`/`CylindricalY` quads (`fire101.tif`/`fire102.tif`) under **parentless template
   nodes** - `fire.zrd.json` activates and scales `fire2.flt` wherever something burns. Not
   wired up: the templates only reach a burn site through `OBJECT_ADD_CHILD` reparenting, which
   is unimplemented, so EFFECTS belongs with that work rather than before it.

**What is NOT a flipbook:** C1's refinery gas flare. `flame01` is a static `fire101.tif`
billboard; its only animation is `refinery_fire.zrd.json`'s `LIGHT_STATE` flicker, cycling
`orange_light`'s range 2->11, 3->15, 1.5->10, 2.5->14 in a tight loop. The flame's apparent
flickering is the *light* flickering. (Checked because the fire textures' existence makes a
texture cycle the intuitive guess; the reader is explicit that it is not.)
