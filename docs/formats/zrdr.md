# zrdr reader archives — overview

Part of the [format documentation](README.md). `zrdr.zbd` archives hold the engine's
"reader" files — config/definition lists (flight stats, spawns, weather, sounds, anims,
effects) that mech3ax (`unzbd cs reader`) extracts to JSON files of nested arrays. The
shared list-shape conventions (alternating key/list dicts, bare-scalar blocks, dual
colour encoding, `kind_of` inheritance, wildcards, everything-is-float) are in the
[README](README.md#shared-conventions-zrdr-readers).

This page maps the archives and their reader families to the pages documenting them.
(Until 2026-07-19 it was a single coarse flight-stats/spawns/sounds/effects page — that
content now lives in the per-family pages below.)

## The three scopes

A mission sees three zrdr archives; readers with mission relevance (anims, states) must
be scanned across all three:

| Scope | Archive | Typical readers |
|---|---|---|
| shared | top-level `zrdr.zbd` | `vehicle.json`, `engines.json`, `player.json`, `sounds.json`, effect readers (`flame_ball`, `pufftrails`, `fire`, …), building/zeppelin/prop anims, `player_plane_destruct.json` |
| chapter | `<Cx>/zrdr.zbd` | chapter scenery anims (`hangar3`, `train`, `fuel_tanks`, `st_light`, `dock_light`, `ap_*`), `cars_moving`/`trucks_moving`, the `ne0NNNNN.zrd` patrol nets + `neindex.zrd` |
| mission | `<Cx>/<mission>/zrdr.zbd` | `ia.json`, `objectives.json`, `weather.json`, `zepstate.json`, `startanims.json`, `mis_anim.json`, `zeppelins.json`, `egen.json`, `dzones.json`, `aiv.json`, `location.json`, `map.json`, `Briefing` |

## Reader family index

| Reader file(s) | Documented in |
|---|---|
| `vehicle.json`, `engines.json`, `player.json` globals | [vehicle.md](vehicle.md) |
| `ia.json` (spawns, mission type, enemy groups, the ace), `objectives.json` `PLAYER_INIT` + mission map | [spawns.md](spawns.md) |
| `dzones.json` (per-mission zone overrides) | [missions.md](missions.md) |
| `zeppelins.json`, `egen.json` (mission entities) | [mission-entities.md](mission-entities.md) |
| `ne0NNNNN.zrd` patrol nets + `neindex.zrd` (the chapter AI waypoint graphs) | [ai-nets.md](ai-nets.md) |
| `sounds.json` SETS, `player.json` sound curves, WAV format | [sounds.md](sounds.md) |
| `weather.json` (fog / sunlight / cloud cover / wind / precipitation) | [weather.md](weather.md) |
| `zepstate.json`, `startanims.json`, `mis_anim.json`, building/vehicle anims, the compiled `cam_anim.zbd`/`mis_anim.zbd` | [anim-definitions.md](anim-definitions.md) |
| `PUFFER_STATE` effect readers (`flame_ball`, `fire`, `pufftrails`, …) | [effects.md](effects.md) |
| `weapons.json` (the shared `BALLISTICS` weapon catalogue) | [weapons.md](weapons.md) |
| weapon effect readers (`muzzle_burst`, `gunhit`, the `*_control` ordnance bursts) | [weapon-effects.md](weapon-effects.md) |
| destructible `ANIMATION_DEFINITION`s (`HEALTH` / `DAMAGE_SEQUENCE` / `ACTIVATION`) | [destructibles.md](destructibles.md) |
| `shakes.json`, `damage_shakes.json` (camera-shake oscillator sources + ON_CALL shake defs) | [shakes.md](shakes.md) |
| `interp.json` boot scripts (not zrdr, but the same config ecosystem) | [clutter.md](clutter.md) |
