# Weapon effect readers & projectile prototypes

Part of the [format documentation](README.md). The muzzle, flyout and impact assets that a
weapon's [`FIRE` / `FLYOUT` / `IMPACT`](weapons.md#fire--flyout--impact-bindings) bindings name:
the effect-reader files (`ON_CALL` `ANIMATION_DEFINITION`s) and the gamez node prototypes each
binding resolves to.

Each weapon in [weapons.md](weapons.md) points at its effects by name — `FIRE` names a muzzle
animation, `FLYOUT` a projectile `MODEL`, `IMPACT` a per-surface hit animation. Those names
resolve to one of two places: an **effect reader** in `extracted/zrdr/` (a named animation
built from `PUFFER_STATE`/`LIGHT_STATE`/`OBJECT_MOTION` events — the event vocabulary is in
[anim-definitions.md](anim-definitions.md), the particle system in [effects.md](effects.md)),
or a **gamez node prototype** (a model root under a chapter's `nodes.json`, instanced at the
firepoint/impact point). This page maps every binding target to its source.

## Muzzle flashes — `muzzle_burst.zrd.json`

`FIRE`'s `ANIMATION` slot names one of these. All are `ON_CALL`, `EXECUTION_PRIORITY 6`, and
toggle a gamez node of the same name active for one frame:

| Animation | Bound by | What |
|---|---|---|
| `muzzle_burst_slug` / `_dum` / `_ap` / `_mag` | the four ammo types (`wep_X0`–`X3`) | LOD-gated `CALL_ANIMATION muzzleburst_effects AT_NODE` (offset y −0.2), then a random `mb_spinflame` roll (30° / 80° / 140°) |
| `muzzle_burst` | the base guns / turret guns | the fuller flash: ejects `gunshell`, runs the `muzzlepuffer` `PUFFER_STATE` (smoke101–103, 0.05 s interval), and picks 1st- vs 3rd-person muzzle lights (`muzzle_lt`, `bigmuzzle_lt`) by `PLAYER_1ST_PERSON` |
| `muzzle_burst2` | the heavy mounts | a larger flash with `muzzle_lt2` (range up to 16 m) |

The flash animation and the flash *node* share a name; the reader animates the prototype node
listed under [Projectile prototypes](#projectile-prototypes-gamez-roots).

### Engine wiring (M3, C22) — casing, muzzle smoke, muzzle light

`ProjectilePool` renders `muzzle_burst`'s three secondaries per gun shot, each from the def's own
values, none through a shared `gunshell` anchor (whose `RUN_TIME 2` under `CallAnimation`'s
already-live gate would drop every ejection but one per 2 s window):

- **Casing** — a pooled instance of the `gunshell` gamez subtree (the `g1` child carries the
  mesh) per shot, flying the gunshell def's `OBJECT_MOTION` verbatim under `MotionRuntime`'s
  semantics: `TRANSLATION_RANGE` xz `[10,−10]` / y `[−75,−85]` as distances travelled over
  `RUN_TIME 2` (random azimuth, `GRAVITY −3` folded), and `FORWARD_ROTATION TIME 20.94` rad
  (1200°) as a **total** angle over the run time — a 10.47 rad/s tumble about local X.
- **Muzzle smoke** — the `muzzlepuffer` values (aft 20 m/s in the muzzle frame, ±0.8 random,
  size 0.3–0.6 m, life 0.1–0.2 s, deviation 0.05 m, `smoke101`) as oriented sprites on the
  pool's sprite path; the per-shot count glosses the authored 0.05 s × 0.3 s emission window.
- **Muzzle light** — a pooled `OmniLight3D` per shot using the `3rdperson_lts` 3-way
  `RANDOM_WEIGHT` variants' range/colour verbatim (1–2 / 1.25–3.25 / 2–3.75 m; 0.88–0.93,
  0.78, 0.36); the def deactivates it on the next event tick, rendered as a ~2-frame flash.
  The `PLAYER_1ST_PERSON` `bigmuzzle_lt` branch (range up to 21 m, ±11 offsets) is unbuilt —
  there is no first-person view yet.

The **white puff cluster** the retail captures show riding each ejected casing matches **no
shipped effect def** (only `muzzle_burst` references `gunshell`, and the `gunshell` def carries
no puffer), so the engine's cluster is a hand-authored stand-in (`BL-200` TUNE).

## Bullet impacts — `gunhit.zrd.json`

`IMPACT`'s `default` `ANIMATION` for a gun names a `<caliber><ammo>_gunhit` — `3040` (30/40-cal),
`5060` (50/60-cal) or `70`, crossed with `slug` / `dum` / `ap` / `mag`, i.e. the same
caliber×ammo axis as the weapon matrix. Each is one `ON_CALL` def whose `ANIMATION_NAME` is the
bound name (`3040slug_gunhit`, …) and whose body is distance-gated:

- **`PLAYER_RANGE 500`** gates a `PUFFER_STATE` — `blacksmokepuffer` for slug (a slow 1.1 s
  smoke that fades black→clear), `whitehotpuffer` + `firepuffer` for dum/mag (fast, `magnesiumtip`
  / `fire_f01`–`04` textures). Beyond 500 m the impact is silent visual-wise.
- **`PLAYER_RANGE 200`** adds flung debris via ballistic `OBJECT_MOTION` (`bit1`/`bit2`/`bit3`/
  `chunk`, `RUN_TIME` 1–4 s).
- **`PLAYER_RANGE 1000` + `ANIMATION_LOD HIGH`** randomly flashes a short `gunhit_lt` light.

Puffer definitions are shared with [effects.md](effects.md); the range gates are the reason a
distant hit shows nothing.

### Engine wiring (M3, D30)

On a projectile impact `ProjectilePool` plays the struck surface's `IMPACT` sound and, for the
effect **animation**, splits by what the bound name resolves to:

- **A gamez model prototype** (the name IS a `nodes.json` root) → the model is instanced at the
  hit point and shown briefly. In practice this is the **water splash**: `splash1.flt` (guns) and
  `bsplsh.flt` (HE) each instance 2 meshes (`splash1_base`/`splash1_splash`, …) — verified on
  C1B/C2B.
- **A reader/control def** (`3040slug_gunhit`, `he_ground_effect`, `large_fireball`) or an
  **undefined** name (`bld_damage.flt`, …) names no root, so nothing instances and a stand-in
  spark shows. The authored **puffer/particle** half of these (the `blacksmokepuffer` smoke, the
  fireball puffs) does **not** render at runtime in flight: the puffer factory + `TextureArchive`
  are torn down after the world build (`KeepArchivesOpen` is lab-only), so a runtime `PUFFER_STATE`
  builds nothing. Rendering them needs a dedicated world-effects runtime that keeps textures open
  and relocates the effect templates onto the hit point — the same machinery the per-player crash
  runtime already proves (`BuildFlightCrashRuntime`) and that **destruction effects (D32)** share,
  so the impact-puffer wiring folds into D32.

### Engine wiring (M3, D32) — the world-effects runtime

That dedicated runtime is now built (`WorldEffectsFactory.BuildWorldEffectsRuntime`): one per session, a
world-scoped `AnimRuntime` bound to the closure of every impact/destruction effect name, over a
**hidden** stage of their gamez template roots (`gunhit`, `flame_ball_01`, `he_ring`, …), with a
live puffer factory (the session textures stay open for the crash runtime already).
`PlayEffectAt(name, worldPoint)` relocates the effect's template root onto the point and starts the
def — its puffers ride that root and parent at world level, so they render. Two callers:

- **Rocket/ordnance impact** → `ProjectilePool.EffectSink`. The rocket ground/building bursts
  (`large_fireball`, `he_ground_effect`, `ap_ground_effect`, `flak_effect`, …) render their smoke/
  fire at the hit. **Guns are excluded** (`!weapon.IsGun`): the `gunhit` `blacksmokepuffer` has no
  `ACTIVE_STATE 0` stop, so a per-round shared emitter would collapse onto one ever-emitting puff — a
  documented follow-up, not wired per-round (the names are still bound, testable via `--effects-test`).
- **Destructible death** → the world runtime's `ExternalEffect` routes a death sequence's
  `CALL_ANIMATION` of a curated effect (`large_30sec_fire`, `great_balls_of_fire`, …) here.

`--effects-test` is the headless verify: it plays each of the 28 bound names at the camera point and
reports which resolve and which actually **build a puffer** (`verification.md` WORLD-12 — a started
def whose factory/textures are absent renders nothing). Deterministic (seeded, `StopAll` between
names): **16 build a puffer** — the fireballs (`large_fireball`/`small_fireball`/`large_30sec_fire`/
`great_balls_of_fire`/`large_black_smokeball`/`big_splash`), the gun `*_gunhit` smoke, and the
`ap`/`sonic`/`flak`/`scatter`/`torpedo` ground bursts. The rest are light/model/container effects
(`he_ground_effect`/`flash_effect` are point-light flashes; `biggun_flying_parts` rides unstaged
`fly_trail*` sub-trails) or `RANDOM_WEIGHT`-gated gun variants. The template **meshes** (the `gunhit`
debris bits, the `he_ring`/splash models) stay hidden — only the puffers render; the mesh half is a
follow-up. A `PUFFER_STATE` whose `AT_NODE` is `INPUT_NODE`/`MAIN_ROOT_NODE` emits on the effect's
own relocated root (the sentinel = "the node this def was invoked on"; see
[anim-definitions.md](anim-definitions.md)).

## Ordnance effect readers

An ordnance weapon splits its effects across a `*_control` reader (the impact/ground burst) plus
trail/puffer sub-readers driven from it. The `IMPACT`/`FIRE`/`FLYOUT` target → reader map:

| Reader | Defines (binding target) | Bound via | Role |
|---|---|---|---|
| `flak_control.zrd.json` | `flak_effect` | IMPACT | flak airburst; `flak_trails.zrd.json` supplies the smoke trails |
| `he_control.zrd.json` | `he_ground_effect` | IMPACT | high-explosive ground burst; `he_effects.zrd.json` the sub-effects |
| `ap_control.zrd.json` | `ap_ground_effect` | IMPACT | armor-piercing ground burst |
| `sonic_control.zrd.json` | `sonic_ground_effect` | IMPACT | sonic burst; `sonic_puffers` / `sonic_rings` the shockwave |
| `scatter_control.zrd.json` | `scatter_effect` | IMPACT | scatter/choker burst; `scatter_trails` the submunition trails |
| `flash_control.zrd.json` | `flash_effect`, `rear_flash_effect` | IMPACT | blinding-flash burst |
| `torpedo_effects.zrd.json` | `torpedo_trail` (FLYOUT), `torpedo_ground_effect`, `torpedo_water_effect` (IMPACT) | both | aerial-torpedo wake + impacts |
| `rear_arc.zrd.json` | `deploy_reararc` (FLYOUT), `rear_flash_effect` | both | the rear-arc flare deployment |
| `missile_puffers.zrd.json` | `generate_smokescreen` | FIRE | the smoke-screen laydown |

### FLYOUT `MODEL_ANIMATION` — the in-flight smoke trails

Every rocket's `FLYOUT` also names a `MODEL_ANIMATION` — an `ON_CALL` def sharing the projectile
prototype's name (`he_rocket`, `flak`, `sonic`, …). Reader source: `missile_puffers.zrd.json`
(most types) / `torpedo_effects.zrd.json` / `rear_arc.zrd.json`; all are also compiled into every
chapter's `cam_anim`, where the engine reads them. Each def activates the prototype node and runs
one or two **`DISTANCE_INTERVAL` `PUFFER_STATE`s** `AT_NODE` the round itself — the authored trail:
one puff per interval meters of flight, texture `splashbase` (a soft round blob), random velocity
±0.8 m/s, size 0.3–0.9 m, growth `[0→1, 1→0.25]`, and a per-type `COLORS` ramp that is the trail's
whole character (each stops emitting at animation time 10 s):

| Type (weapon) | Puffer(s) | Interval | Lifetime | Colour ramp (rgb, life fraction) |
|---|---|---|---|---|
| HE (`wep_06`/`_24`) | `he_rocket_trail` | 1.5 m | 3.0–4.5 s | 255,180,0 → 100,100,100 @ 0.1 |
| AP (`wep_05`) | `trailpuffer_ap` | 2.0 m | 3.0–5.0 s | 204,255,0 → 234,255,151 @ 0.15 → 100,100,100 @ 0.3 |
| Flak (`wep_07`) | `trailpuffer_dark` | 2.0 m | 3.5–5.0 s | 255,180,0 → 50,50,50 @ 0.05 (near-black) |
| Incendiary (`wep_04`/`_11`/`_25`/`_26`) | `trailpuffer2` | 2.0 m | 3.5–6.5 s | 230,90,90 → 255,220,163 @ 0.07 → white @ 0.2 |
| Sonic (`wep_08`) | `sonicpuffertrail1`+`2` | 2.0 m | 3.5–6.5 s | 131,200,190 → 68,115,109 @ 0.07 → 40,40,40 @ 0.2 (teal; size 0.2–0.5, friction 0.2) |
| Scatter / beeper / flash | `scatterpuffer_dark` / `beeper_trail` / `flash_trail` | 2.0 m | 2.5–5 s | the flak ramp |
| AA flak (`wep_27`) | `trailpuffer` | 2.0 m | 2.0–3.0 s | no ramp — a fire→smoke flipbook (`fireflare1`/`fire_f01`/`smoke101`/`smoke102`) |
| Cannonball (`wep_28`) | `trailpuffer2` + `forwardpuffer` | 2.0 m | 0.2–0.3 s | white trail + an orange forward glow (`local_velocity` z −350) |
| Torpedo (`wep_14`) | `torpuffertrail1`/`2` + `torpufferblast` | 0.2 m | 0.5–1.2 s | fire flipbooks; the blast cloud is TIME-interval |

The `sonic` def additionally runs a `sonic_spinner` sequence: a steady `OBJECT_MOTION`
`XYZ_ROTATION` roll of the round's body at **8.7266 rad/s (500°/s)** about z, looped forever —
the only rocket that spins.

**Engine wiring (M3, C21).** `ProjectilePool` resolves each rocket's `MODEL_ANIMATION` name
through the world `AnimProgram`, takes every ACTIVE `DISTANCE_INTERVAL` `PUFFER_STATE` verbatim
(`PufferState.FromAnimEvent`) and drives one `Puffer.TrailAdvance` per live round; the spinner
rate rolls the FLYOUT body. Emitters are pooled and reused once their smoke decays. Deliberately
not rendered yet: the TIME-interval `torpufferblast` cloud, and the torpedo def's wing/prop
`OBJECT_MOTION` events (`wep_14` is mountable via `--rocket=` but on no stock loadout).

`large_fireball` / `small_fireball` (bound by `FIRE`/`IMPACT` on the heaviest ordnance) are the
**shared** destruction fireballs defined in `flame_ball.zrd.json` and reused by nearly every
destructible — see [destructibles.md](destructibles.md) and [effects.md](effects.md), not a
weapon-specific asset.

Supporting effect readers with no direct binding target, driven by the controls above or by the
guns: `gunshell.zrd.json` (the ejected shell casing), `flak_trails` / `scatter_trails` /
`sonic_puffers` / `sonic_rings` / `he_effects` / `pufftrails`, `beeper_plug.zrd.json` (the beeper
tag), `cockpit_bulletholes.zrd.json` (hits on the player's own canopy), and the emplacement guns
`8inch_cannon` / `aa_gun` / `maa_gun` / `fbgun` / `fbgun2`.

## Projectile prototypes (gamez roots)

`FLYOUT`'s `MODEL` and a few `IMPACT`/`FIRE` targets name a **node prototype**, a model root
present in every chapter's `nodes.json` (all confirmed in C1). `SceneBuilder` instances it at
the firepoint or impact point. "Confirmed" here means **the name resolves to a gamez node**,
not that the node carries a mesh — see the footnote below for `gunshell`/`muzzle_burst`, the
two names known to differ:

| Group | Prototype roots |
|---|---|
| Gun rounds (`FLYOUT MODEL`) | `slug.flt`, `dumdum.flt`, `armorpiercing.flt`, `magnesium.flt` |
| Ordnance (`FLYOUT MODEL`) | `ap_rocket`, `he_rocket`, `flak`, `flash`, `beeper`, `scatter`, `incendiary`, `smoker`, `sonic`, `reararc`, `a_torpedo`, `aaflak`, `cannonball` |
| Muzzle | `muzzle_burst`¹, `muzzle_burst_slug` / `_ap` / `_dum` / `_mag`, `muzzle_burst2` |
| Impact / misc | `gunhit`, `dum_gunhit`, `mag_gunhit`, `gunshell`¹, `ballflare.flt`, `bsplsh.flt`, `splash1.flt` |

¹ Measured across all 8 chapters (`analysis/weapon-effects-node-shape/`, `BL-140`): both roots
carry `model_index: -1` (no mesh of their own) and exactly one child. `muzzle_burst`'s child
(`dummy`) is also `model_index: -1` — the whole subtree is genuinely meshless, so instancing
this root alone lights/moves nothing visible. `gunshell`'s child (`g1`) carries a real mesh
(`model_index: 60` in every chapter, 10 vertices / 7 polygons) and is structurally parented
under `gunshell` itself — so the *casing* prototype does resolve to a visible mesh, one node
below the name `FLYOUT`/`CallAnimation` target. The C22 ejection wiring instances the whole
`gunshell` subtree per shot (see the muzzle-flash engine-wiring section above), which renders
the `g1` mesh with its own materials — never assume the root alone shows anything.

`firepoint` is the marker prototype (the aircraft's own firepoints are documented in
[markers.md](markers.md)).

## Muzzle & tracer textures — the ammo-type axis

The per-ammo appearance is texture-driven, on the **same four-way axis as the `wep_X0`–`X3`
ammo types**. In each chapter's `texture/`:

- **Muzzle flash:** `{slug,dum,ap,mag}_muzzle1` / `_muzzle2` — a two-frame flipbook per ammo
  (the `1`/`2` frame pair, see [anim-definitions.md](anim-definitions.md)).
- **Tracer:** `tracer_slug` / `tracer_dumdum` / `tracer_armorpierce` / `tracer_magnesium`, plus
  the generic `tracer1`; `slugtip`, `shell1` / `shell2`, `atorp`.

## Binding resolution — 5 unresolved names

Of the **57** distinct asset names referenced across all 48 weapons' `FIRE`/`FLYOUT`/`IMPACT`
bindings, **52 resolve** to a reader above or a gamez prototype root, and all **23** referenced
sound defs resolve in [sounds.md](sounds.md). **Five names have no locatable definition** — they
appear only as references inside `weapons.json`, with no matching reader animation or gamez node
in any of the 8 chapters:

| Unresolved | Referenced by | Slot | Likely |
|---|---|---|---|
| `bld_damage.flt` | gun `IMPACT` `buildings` | IMPACT | an external `.flt` model, not extracted as a node |
| `rcochet1` | gun `IMPACT` | IMPACT | a ricochet spark effect |
| `call_small_flash` | `FIRE` | FIRE | a small muzzle-flash variant |
| `f18sparks2` | `IMPACT` | IMPACT | an impact-spark effect (dev-named) |
| `flak_effectplayer` | flak `IMPACT` `player` | IMPACT | a player-surface flak variant (cf. `flak_effect`) |

These are **leads for Wave D**, not confirmed content: each is a named binding whose asset was
not found in this install's extraction. **D30 confirmed all five inert for impacts:** none names
a gamez model root, so `ProjectilePool` instances nothing for them and the stand-in spark shows
(no crash) — measured on C4/C5 building hits (`bld_damage.flt`, `large_fireball`).
