# Effects: `PUFFER_STATE` emitters, effect readers, flipbook textures

Part of the [format documentation](README.md). Covers the original's fully data-driven
effect system (surveyed 2026-07-14 for the crash sequence + damage trails; no binary anim
format needed for any of it). Consumed by `CrimsonSkies/src/Effects/Puffer.cs`.

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
| `TEXTURE_SEQUENCE` | [(name, t)…] | **flipbook**: frames with per-frame timestamps |
| `TEXTURES` | [name…] | **static pool**: each sprite picks one at random (smoke101/102/103) |
| `COLORS` | [[lifeFrac, r, g, b, a]…] | colour-over-age ramp; rgb dual-encoded (the [weather.md](weather.md) rule: any component > 1 ⇒ ÷255), alpha 0–1. `dense_firetrail`'s smoke is born orange (255,164,90) → near-black |

Rendering note (measured, remake convention): states **with** a COLORS ramp must be
alpha-blended (a near-black smoke ramp is invisible additively); ramp-less fire/flash
states read correctly additive. The fade at end-of-life belongs to the ramp when present
(its alpha ends at 0).


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
