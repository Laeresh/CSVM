# ANIMATION_DEFINITION readers (zepstate, startanims, building/vehicle anims)

Validated against this install's zrdr extraction (mech3ax v0.6.1), 2026-07-18, while
implementing the anim-state engine part 1 (mission start states). Field tables + tiny
excerpt values only — no bulk game data.

The original compiles these reader sources into per-mission `mis_anim.zbd` archives
(a binary format mech3ax does **not** support); the zrdr JSON sources carry the same
definitions, so scanning them is a full substitute for state purposes.

## Where definitions live

Three zrdr scopes are visible to a mission (the remake scans all reader files in each
whose content mentions `ANIMATION_DEFINITIONS`):

| Scope | Examples |
|---|---|
| shared `zrdr.zbd` | `small_building`, `medium_building`, `aa_gun`, `wing_light`, `plane_props`, zeppelin part anims (`multi1_*`, `pzep_*`, …) |
| chapter `<Cx>/zrdr.zbd` | C1: `hangar3`, `train`, `fuel_tanks`, `ref_fueltanks`, `train_signal`, `ap_*` airfield buildings |
| mission `<Cx>/<mission>/zrdr.zbd` | `zepstate` (per-mission base object states), `mis_anim` (mostly `ANIMATION_DEFINITION_FILE` references), mission one-offs (`hangar_panic` in C1/M02) |

A mission's `mis_anim.json` lists `ANIMATION_DEFINITION_FILE` paths into the *source*
data tree (`..\data\common\zrdr\zeps\*.zrd`); those files are the shared-scope readers
under their extracted names — following the references is unnecessary when scanning all
three scopes.

## File shape

```
[["ANIMATION_DEFINITIONS", [
    ("GRAVITY", [-9.8])?  ("ANIMATION_PATH", ["..\\data\\…"])?
    "ANIMATION_LIST", [
        "ANIMATION_DEFINITION", [ …def… ],     ; repeated
        "ANIMATION_DEFINITION_FILE", ["path"], ; mis_anim only
        … ] ] ]]
```

Alternating key/value-list pairs **with meaningful duplicate keys** (multiple
`SEQUENCE_DEFINITION`s per def, repeated op kinds per sequence) — a collapsing dict
view loses data; walk the raw list. A key followed by `null` (or by another key) is a
bare flag (`LOCAL_NODES_ONLY`).

## ANIMATION_DEFINITION fields

| Key | Value | Meaning |
|---|---|---|
| `NAME` | 1 string | The world/object node(s) this def anchors to. Wildcards make one anim instance per matching node: `*`/`**` = any run of characters (`ftank0*`, `s_build**`), `#` = run of digits (`air_gen#`). Node names may be given without their `.flt` model suffix (`ap_radiotwr` ↔ gamez node `ap_radiotwr.flt`). |
| `NAME1` | list of (wildcard, [path…]) pairs | Multi-target form (zeppelin nacelle/turret sets): maps anim-instance name patterns to node paths inside a parent object. Per-object anims — part 2 scope. |
| `ANIMATION_NAME` | 1 string | The name `startanims.json` / `CALL_ANIMATION` refer to. May itself carry a wildcard in template defs (`ftank_boom*`). |
| `ANIMATION_ROOT_NAME` | 1 string | The node inside each instance the anim attaches to (`s_bld_healthy`, bare `healthy`). Needed to locate instances whose roots have free names: `m_build**` instances in C1 are `apbuild01.flt`/`aphngr01.flt`/…, found via their `m_bld_healthy` child. |
| `LOCAL_NODES_ONLY` | bare flag | Op node names resolve inside the instance subtree only (building/vehicle templates). |
| `ACTIVATION` | `ON_STARTUP` \| `ON_CALL` | ON_STARTUP defs run their sequences at mission load (`zepstate`); ON_CALL waits for `CALL_ANIMATION`/game events. Sequences may carry their own `ACTIVATION ON_CALL` (run only via `CALL_SEQUENCE`). |
| `RESET_TIME` | 1–2 floats (−1 common) | Reset scheduling (undecoded detail). |
| `RESET_STATE` | op list | The object's **base state**, applied at load: healthy variants ACTIVE, `destroyed` variants INACTIVE, doors at rest pose. This is what fixes the destroyed-over-healthy coplanar flicker. |
| `SEQUENCE_DEFINITION` | op list (repeatable) | One timeline of ops; optional `NAME`, optional `ACTIVATION`. |
| `HEALTH`, `DAMAGE_SEQUENCE` | | Destructible-object HP + damage-threshold script (`IF ANIM_HEALTH n … CALL_ANIMATION sputter_fire_smoke_obj …`). |
| `SAVE_LOG`, `PERSIST_LOG`, `EXECUTION_PRIORITY`, `AUTO_RESET_NODE_STATES`, `AUTO_ADD_TO_WORLD` | | Engine bookkeeping, undecoded detail. |

## State ops (the part-1 subset)

| Op | Body | Meaning |
|---|---|---|
| `OBJECT_ACTIVE_STATE` | `NAME` [node…], `STATE` [`ACTIVE`\|`INACTIVE`] | Show/hide a subtree (and its collidability). A multi-entry NAME is a parent→child path (`["piratezep","interior"]`). |
| `OBJECT_TRANSLATE_STATE` | `NAME`, `STATE` [x,y,z] | Base local translation offset from the authored rest pose (RESET_STATE uses `[0,0,0]`). |
| `OBJECT_ROTATE_STATE` | `NAME`, `STATE` [x,y,z] | Base local rotation, degrees. |
| `OBJECT_MOTION_FROM_TO` | `NAME`, `TRANSLATE_TO`/`ROTATE_TO` [x,y,z], `RUN_TIME` [s] | Timed motion; part 1 applies the **end** pose (C1 hangar 3: four `h3_dr*` doors `TRANSLATE_TO ±25/±50, 0, 0` over 9–10 s), part 2 plays it. |

Playback ops seen and deferred to part 2+: `OBJECT_MOTION` (continuous spin),
`OBJECT_MOTION_SI_SCRIPT` (spline `.zan` scripts — the train), `OBJECT_ADD_CHILD`/
`OBJECT_DELETE_CHILD` (reparenting), `CALL_ANIMATION`/`CALL_SEQUENCE`/`STOP_ANIMATION`/
`INVALIDATE_ANIMATION`, `SOUND`/`SOUND_NODE`, `PUFFER_STATE`, `LIGHT_STATE`,
`CAMERA_STATE`, `FBFX_COLOR_FROM_TO`, `CALLBACK`, `IF`/`ELSEIF`/`ELSE`/`ENDIF`
(`RANDOM_WEIGHT`, `NODE_ACTIVE`, `ANIM_HEALTH` conditions), `LOOP`, `DETONATE_WEAPON`.

## startanims.json

```
[["NEW_GAME_START", [["player_setup"], ["train_on_track"], …],
  "LOAD_GAME_START", null]]
```

Anim names (matched against `ANIMATION_NAME`) run at mission start, in list order —
order matters: C1/IA1 runs `hangar3_doors` (doors to ±50) then `mp_hangar3_open`
(same doors to ±25); last write wins. Names may be **undefined** in the mission's
visible scopes (C1/IA1 lists `pure_panic`, defined only in C1/M02's folder) — the
engine evidently tolerates the miss; skip and log.

## zepstate.json

Ordinary ANIMATION_DEFINITIONs, `ACTIVATION ON_STARTUP`, whose sequences hold only
`OBJECT_ACTIVE_STATE`s — the per-mission roster of world objects present: C1/IA1
deactivates `dliner1` (the passenger zeppelin in the shed) and `cargotrain`. The
chapter gamez contains *every* mission's objects; without applying these states,
phantom zeppelins/trains render in every mission.

## Scenario dimension (analyzed 2026-07-18 — negative)

Instant-action scenario names (`zeppelin_run`, `dogfight_ace`, …) appear **only** in
`ia.json` spawn lists (plus `disallow_missions`). No reader carries scenario-conditional
world state: the IA world state is per-mission (`zepstate` + `startanims`), identical
across scenarios; `zeppelins.json` is the gameplay config of the always-present IA
zeppelin (`multiplayer1zep`), not a state selector. Scenario selection evidently drives
spawns/objectives/AI at engine level, not the world build.
