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
the firepoint or impact point:

| Group | Prototype roots |
|---|---|
| Gun rounds (`FLYOUT MODEL`) | `slug.flt`, `dumdum.flt`, `armorpiercing.flt`, `magnesium.flt` |
| Ordnance (`FLYOUT MODEL`) | `ap_rocket`, `he_rocket`, `flak`, `flash`, `beeper`, `scatter`, `incendiary`, `smoker`, `sonic`, `reararc`, `a_torpedo`, `aaflak`, `cannonball` |
| Muzzle | `muzzle_burst`, `muzzle_burst_slug` / `_ap` / `_dum` / `_mag`, `muzzle_burst2` |
| Impact / misc | `gunhit`, `dum_gunhit`, `mag_gunhit`, `gunshell`, `ballflare.flt`, `bsplsh.flt`, `splash1.flt` |

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
not found in this install's extraction. Confirm each renders (or is inert) when wiring impact
and muzzle effects.
