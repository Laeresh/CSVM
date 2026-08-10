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
| `FADE_RANGE` (= `FAR_FADE`) | [rampStart, cutoff] m | camera-distance fade: full alpha up to `[0]`, linear to zero at `[1]`, discarded beyond (`fire_n_smoke`: 1500–1700). Two spellings of one block — 574 readers say `FADE_RANGE`, exactly one (C3's `volcanosmoke`) says `FAR_FADE`. **Implemented** (`PufferState.FarFadeStart`/`FarFadeEnd`, C7); the install's widest authored key at 2,508 compiled events over 238 puffers |
| `NEAR_FADE` | [cutoff, fullAlpha] m | near-camera band, compiled name `unk_range`. `[0]` is the **hard discard cutoff** and `[1]` the distance alpha would reach 1 — ⚠ **do not read that order off the values**, five of the six authored pairs are descending (`70,20` almost everywhere). **Implemented** (`PufferState.NearFadeStart`/`NearFadeEnd`, C7). With the shipped data it is a **cull, never a partial alpha** — see the distance-fade section below |
| `START_AGE_RANGE` | [min, max] s | random birth age — a particle is born at `Rand(min, max)` instead of age 0, negative values included (`fire_at_zepskin3`: −1.0 to 0.1). **Implemented** (`PufferState.StartAgeMin`/`StartAgeMax`); authored by only 4 puffers in the install, 80 compiled events total (`PLAN-puffer-engine-deltas` B4). ⚠ The key is not the whole birth age: the engine's `age0` is this draw **plus** `(1 - frac)·dt`, the sub-frame term of the time-cadence spawn (B5), and it discards the particle outright when `age0 >= life` — so that skip fires on a long frame for **any** puffer, authored key or not |
| `WIND_FACTOR` | float | how strongly the world's wind carries this puffer's particles; **defaults to 1, not 0**, and is inert unless `FRICTION` is non-zero. **Implemented** (B6) — see [architecture.md](../architecture.md)'s `Effects/WorldWind.cs` entry |
| `PRIORITY` | float | a per-puffer sprite-size nudge (`1 + K·PRIORITY`), **not implemented** — 192 compiled events over 47 puffers (`PLAN-puffer-engine-deltas` C8) |

## The camera-distance fade (`FADE_RANGE` + `NEAR_FADE`)

Decoded from the head of `FUN_0054e6e0`, the original's per-particle draw, and implemented in
`Puffer.DistanceAlpha` (C7). The distance `d` is the **view-space DEPTH** along the camera's forward
axis, in metres — not the euclidean range. (`FUN_0054ed10` pre-scales the view matrix's third column
by `_DAT_009fd5d0`, the draw multiplies by `_DAT_009fd5c0`, and on the hardware path `FUN_0053c110`
makes those exact reciprocals; on the software path they do not cancel and the distances come out
scaled, but this project has no software path.) The evaluation, in order:

1. discard if `d·globalFadeFactor >= FADE_RANGE[1]`;
2. if `d·globalFadeFactor <= FADE_RANGE[0]` the **near** band decides: discard if `d <= NEAR_FADE[0]`,
   full alpha if `d >= NEAR_FADE[1]`, else ramp;
3. otherwise ramp across the far band: `alpha = (FADE_RANGE[1] − d·globalFadeFactor) / (FADE_RANGE[1] − FADE_RANGE[0])`;
4. discard if the resulting alpha is `<= 0`.

The alpha **multiplies** whatever the `COLORS` ramp or the life envelope already produced; it
replaces neither.

⚠ **A cross-wire in the original, reproduced deliberately.** The NEAR ramp's origin is
`FADE_RANGE[0]`, not `NEAR_FADE[0]` (`0054e7b5 FSUB [ESI+0x3c]` against the near reciprocal at
`ESI+0x44`, read in raw assembly). It is consistent with `FADE_RANGE` being the original field and
`NEAR_FADE` bolted on later by copy-pasting the far-band line. C3's `volcanosmoke` is the only puffer
in the install whose near pair ascends and therefore the only one that reaches that ramp — where the
wrong origin drives its alpha negative, so it is culled below `NEAR_FADE[1]` and pops in at 75 m.
Every other near pair descends and takes the alpha-1 exit. **The near band is therefore a hard cull
on every puffer in the install**, and "fixing" the cross-wire would invent a 1→75 m fade-in the
original does not have.

⚠ **Almost every explosion effect in `flame_ball.zrd.json` authors `NEAR_FADE [70, 20]`** —
`fierypuffer`, `trailpuffer2`, `fire_n_smoke` and the ball family. Within 70 m of the camera the
original draws none of them, which is a large, visible consequence at any close chase-camera pose;
`puffer.nearCull` exists to switch it off, and defaults on because that is what the original does.

**Three config switches, all defaulting to the original's behaviour** (`puffer.distanceFade` the
authored far ramp, `puffer.farCull` the hard discard past the band, `puffer.nearCull` the near
discard), plus `puffer.globalFadeFactor` — the original's own `PufferSetGlobalFadeFactor`
(`00637a94`, default 1.0), which scales the FAR band only. ⚠ `puffer.farCull:false` alone changes
nothing visible: the authored ramp reaches zero at exactly the cutoff distance, so the two are the
same line. Keeping distant puffers drawn takes `distanceFade:false` and `farCull:false` together.

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
