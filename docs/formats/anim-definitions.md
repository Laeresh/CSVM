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

## Compiled anim archives — `cam_anim.zbd` / `mis_anim.zbd` (surveyed 2026-07-18, extraction deferred)

The binary archives mech3ax does not support for CS. Surveyed while scoping the
anim-playback engine (Run-2 item 9); **playback is deferred until the data is extractable
via a mech3ax extension** (decided 2026-07-18 — note: upstream mech3ax HEAD has since
dropped its CS gamez support, so the future fork must account for that). Everything below
was validated by direct binary analysis of this install's C1 archives; the partial decode
is recorded so that work can resume where this left off.

**Why they matter:** the vehicle motion (`OBJECT_MOTION_SI_SCRIPT`) references `.zan`
spline scripts that exist **nowhere as loose files** — they are compiled only into these
archives. Everything else about the anims (schema, sequences, timing, puffers) is already
in the zrdr readers; the `.zan` frame data is the *only* missing piece for the train.

- **Scope split (verified by strings):** a mission's `mis_anim.zbd` compiles only the defs
  its `mis_anim.json` lists (C1/IA1: the 8 zeppelin `ANIMATION_DEFINITION_FILE`s + its own
  file — no train, no vehicles). The **chapter's `cam_anim.zbd`** is not camera-only: it is
  the chapter-scope compiled anim archive, holding chapter + shared defs **including all
  vehicle SI scripts** (C1: the 4 train cars, 2 fueltrucks, mission-intro cameras,
  `cpilot_eject` body parts — 48 scripts total).
- **Container** (same family as MW3 `anim.zbd`, which mech3ax fully supports — the natural
  implementation template): header `{signature u32 = 0x08170616` (identical to MW3),
  `version u32 = 53` (MW3 = 39), `nBase u32 = 2, nAnimFiles u32}`; then 2 ×
  `{path char[128], mtime u32}` (the gamez.zbd + planes.zbd the archive was built against),
  then nAnimFiles × `{path char[80], mtime u32}` (the `.zrd`/`.zan` sources; mtimes are
  year-2000 Unix timestamps). Then an anim-info block (~0x6c bytes: gravity −9.8 f32,
  counts, runtime pointers), then the AnimDef records (fixed C struct + inline sequence
  data, first record a `reserved_anim_0` placeholder — **internals not decoded**), then the
  SI-script pool, which runs byte-exactly to EOF.
- **SI-script pool** (the part item 9 needs): back-to-back records of
  `{source path\0, object name\0, frames…}`. Frame = `{flags u32: 1=translate, 2=rotate,
  4=scale; start f32, end f32}` + one **19-float block per set flag**. Translate block
  (verified): `base Vec3` + 4 floats `(0, avgVel x,y,z)` + per-axis `{value, c1, c2, c3}`
  where `component(t) = value + c1·t + c2·t² + c3·t³`, `t` seconds since frame start —
  verified exact against each next frame's base value and C1-continuous (next frame's `c1`
  = previous frame's exit derivative). Rotate block: starts with a unit quaternion
  `(w,x,y,z)`; remaining 15 floats presumed the same avg+cubic scheme — **not decoded**.
- **Validation state:** a flags-driven frame walker parses **24 of C1's 48 scripts
  byte-exactly** to the next record — including all four train scripts and both
  fueltrucks (every vehicle motion) — with contiguous monotonic times; the failures are
  confined to camera/`cpilot_eject` scripts (an undecoded variant: they stop early on
  padding or run slightly past — likely an extra sub-record). Train data: 4 scripts
  (`tr_passengine1/tankercar1/boxcar1/caboose1.zan`) × 90 frames × ~3.64 s (= 40 ticks at
  `SCRIPT_FRAME_RATE` 11), total ~327 s per loop, starting at the parked consist position
  `(−6943…−6961, 128, −5456…−5412)` and covering a ~3.9 × 2.2 km track loop — all
  consistent with the C1 gamez node positions and the reader's frame rate.
- **What playback could already use without the binaries:** the C1 road vehicles
  (`cars_moving.json` `mafia_move1`/`police_chase`/`car_go_home_start`/`car_loop1_start`,
  `trucks_moving.json` `truck1_start` — all `ON_STARTUP`) move via `OBJECT_MOTION_FROM_TO`
  chains (`TRANSLATE_FROM/TO` + `ROTATE_FROM/TO` + `RUN_TIME` + `START_TIME` + `LOOP` +
  `CALL_SEQUENCE`/`STOP_SEQUENCE`) fully present in the extracted readers, as are the
  hangar-door motions; the train's steam `PUFFER_STATE` is fully inline in `train.json`
  (all emitter params + `smokestack` attach node). Firetrucks / fueltrucks / patrol boat
  are `ON_CALL` only (mission-event driven — nothing calls them in free flight).
- One plan-evidence correction: the `ANIMATION_PATH` key in `mis_anim.json` is the
  **directory** the engine resolves anim sources from (`..\data\c1\ia1\zrdr\zeps`), not a
  waypoint-motion primitive; no waypoint-path op exists in any C1 reader — path motion is
  SI scripts.
