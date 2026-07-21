# ANIMATION_DEFINITION readers (zepstate, startanims, building/vehicle anims)

Part of the [format documentation](README.md). Validated against this install's zrdr
extraction (mech3ax v0.6.1), 2026-07-18, while implementing the anim-state engine part 1
(mission start states). Field tables + tiny excerpt values only — no bulk game data.

The original compiles these reader sources into per-mission `mis_anim.zbd` archives
(a binary format upstream mech3ax does not support; the project's mech3ax **fork decodes
it** — see the compiled-archives section below); the zrdr JSON sources carry the same
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
| `OBJECT_TRANSLATE_STATE` | `NAME`, `STATE` [x,y,z], `RELATIVE` | **Absolute** position in the node's parent frame (see below). `RELATIVE` is `false` in all 1143 uses in this install. |
| `OBJECT_ROTATE_STATE` | `NAME`, `STATE` [x,y,z], `BASIS` | **Absolute** orientation in the parent frame, **radians**. `BASIS` is `"Absolute"` in 6430 of ~6600 uses; the rest are `AtNodeXYZ`/`AtNodeMatrix` look-at forms (zeppelins, cameras). |
| `OBJECT_MOTION_FROM_TO` | `NAME`, `TRANSLATE`/`ROTATE`/`SCALE` `{from,to}` (+ `*_DELTA` variants), `RUN_TIME` [s] | Timed motion between two **absolute** parent-frame poses (C1 hangar 3: four `h3_dr*` doors over 9–10 s). |

### Transform channels are absolute, and rotations are radians

**Every transform channel — `translate`, `rotate`, `scale`, in both the `*_STATE` events and
`OBJECT_MOTION_FROM_TO` — is an absolute value in the node's own parent frame, not an offset
from its authored rest pose.** The `*_DELTA` channels (`translate_delta`, `rotate_delta`,
`scale_delta` — 29 uses across the whole install) are the genuinely relative ones.

Evidence, surveyed over all 8 chapters (2026-07-21):

- C1's `mafia` car moves `from (-6796, 128, -5958) to (-6521, 128, -5958)`; its gamez node's
  authored translate is `(-6795.828, 128.0, -5956.72)` and its parent is the `world1` root.
  Adding the channel to the rest pose put the C1 traffic at `(-13574, 256, -11914)` — exactly
  double, y included — i.e. ~7 km off the map.
- C5's `m_gerter` crane hook moves between `(21.3, 19.9, -20.2)` and `(21.3, 7.9, -20.2)`,
  small numbers because its parent is `m_crane`. C1's `sinker` moves `(0,0,0) → (0,-7.5,3.5)`
  in its parent boat's frame.
- For `OBJECT_TRANSLATE_STATE`, 627 of the resolvable uses have `STATE` **exactly equal to**
  the node's authored rest translate — the doubling case again. The 367 zero-valued uses are
  no-ops only because those nodes rest at the origin.
- "Does `from` match the node's rest pose" is a **bad test**: nodes whose animation is what
  places them (C2's `sailboat2`, C1's `car_go_home`) rest at their parent's origin and
  legitimately don't match. Absolute-in-parent-frame is the rule that covers both families.

Rotations are **radians**, not degrees: the maximum magnitude in the data is `15.708 = 5π`,
99.93% of values are ≤ 2π, and 228 sit on exact π/2 multiples. Converting them with
`DegToRad` makes every rotation ~57× too small — visually, nothing turns.

### `IF`/`ELSEIF` conditions are all evaluable

Ten condition kinds appear across the install. None of them is opaque gameplay state that a
world build has no value for — an earlier reading that led the runtime to skip every branch:

| Count | Condition | How to evaluate |
|---:|---|---|
| 4537 | `RandomWeight` (0..1) | A dice roll. |
| 4007 | `AnimHealth` | Object health — full in a fresh world. |
| 1052 | `PlayerRange` | Distance from the player/camera; live scene state. |
| 717 | `NodeActive` | Whether a node is active; our own scene state. |
| 473 | `NodeUndercover` | Node + distance. |
| 124 | `AnimHealthRange` | `{min,max}` health window. |
| 120 | `AnimationLod` | Our own detail-level setting. |
| 120 | `PlayerFirstPerson` | Our own camera mode. |
| 28 | `NodeBelowAlt` | Node altitude vs a threshold; scene state. |
| 17 | `HwRender` | Hardware rendering — true. |

This matters because the branches gate real content: C1's `refinery_fire_always` and
`ref_light_always1..6` wrap their entire light sequence in `If { AnimationLod: 2 }`, and
`litehouse_sparking` gates its spark bursts on `If { RandomWeight: 0.7 }`. Skipping branches
means those effects never run at all.

Playback ops seen and deferred to part 2+: `OBJECT_MOTION` (continuous spin),
`OBJECT_MOTION_SI_SCRIPT` (spline `.zan` scripts — the train), `OBJECT_ADD_CHILD`/
`OBJECT_DELETE_CHILD` (reparenting), `CALL_ANIMATION`/`CALL_SEQUENCE`/`STOP_ANIMATION`/
`INVALIDATE_ANIMATION`, `SOUND`/`SOUND_NODE`, `PUFFER_STATE`, `LIGHT_STATE`,
`CAMERA_STATE`, `FBFX_COLOR_FROM_TO`, `CALLBACK`, `IF`/`ELSEIF`/`ELSE`/`ENDIF`
(`RANDOM_WEIGHT`, `NODE_ACTIVE`, `ANIM_HEALTH` conditions), `LOOP`, `DETONATE_WEAPON`.

## The mission zrdr scope is a LIBRARY, not a manifest

A mission folder ships reader files it never uses. **A mission-scope reader definition applies
only if the mission's compiled `mis_anim` archive contains it** (matched on the compiled
extraction's own file naming, `<anchor>-<anim_name>.json`).

C1/IA1 carries a `zepstate.zrd.json` that hides `dliner1` and `cargotrain`, yet in the
original both are present in Instant Action — `dliner1` is the zeppelin inside the Passenger
Hangar (`dz1` is 140 m from it) and `cargotrain` is the consist parked in the cut below the
terminal at (-5102, 128, -3852). Its `mis_anim` compiles neither. Verified over every
zepstate in this install:

| Mission | zepstate defs | in compiled `mis_anim`? |
|---|---|---|
| C1/IA1, C1B/IA1, C1C/IA1, C2/IA1, C2B/IA1, C4/IA1 | `dliner1`, `cargotrain` | **unused** |
| C1/M02 | `hk_zep`, `lkshadow`, `tethershadow`, `tethertower` | COMPILED |
| C1/M04 | `dliner1`, `cargotrain` | COMPILED |
| C3/IA1, C3/M02, C3/M03, C4/M03 | `cargozep1` | COMPILED |

C3/IA1 compiles `cargozep1`, so this is **not** "Instant Action ignores zepstate" — the
compiled set is the authority. Two user observations of the original corroborate it: C1/M04
shows the field zeppelin with an empty hangar and no parked train (what compiling both defs
produces), and C1/M02 is the only mission where the tether tower disappears — the only
mission that compiles `tethertower`.

⚠ This **supersedes** the earlier note that `zepstate` "is never compiled into any archive",
which was generalised from C1/IA1. It is compiled into the missions that use it; being
uncompiled is exactly the signal that the mission does not instantiate it.

### Mission-spawned entities (open)

Scenery props are hidden by compiled `zepstate` defs as above, but *entities* work the other
way round — they are absent unless a roster spawns them. C1's `hk_zep` (the Hollywood Knights
zeppelin, at (-5248, 200, -5208) beside `tethertower`) has no def in IA1 scope at all, yet the
original does not show it in Instant Action. The rosters:

- **`aiv.zrd.json`** — AI vehicles. C1/IA1: player only. C1/M02: `hk_zep`. C1/M04: `hk_zep`, `piratezep`.
- **`zeppelins.zrd.json`** — flyable zeppelins, with position/yaw/engines/cannons/gasbags.
  C1/IA1: `multiplayer1zep`. C1/M04: `piratezep`. MP1/MP2: none. MP3: `multiplayer1zep`, `multiplayer2zep`.

The same shape governs the CTF props (`ctf_1`/`ctf_2`, `cs_flag_1`/`cs_flag_2`), referenced
only by C1/MP2's `targets.zrd.json` and visible only in Capture the Flag. Not yet
implemented; note `dliner1` must stay visible under any such rule, so the entity set has to be
derived from the rosters rather than from a name pattern.

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

## Compiled anim archives — `cam_anim.zbd` / `mis_anim.zbd` (fork support COMPLETE, CLI wired)

The binary archives upstream mech3ax does not support for CS. Surveyed 2026-07-18 while
scoping the anim-playback engine (Run-2 item 9); since then the project's mech3ax fork
(`tools/mech3ax`, plan `docs/PLAN-mech3ax-cs-revival.md`) has implemented them in
`crates/anim/src/cs/`: the container (item 3, 2026-07-20), the full semantic
`AnimDef` + event decode (item 4, 2026-07-21), and the SI-script frame decode (item 5,
2026-07-21) round-trip **byte-identically on all 61 archives of this install** with no
raw regions left except the per-axis spline coefficient blocks (kept as bytes by
upstream's own MW/PM convention; their semantics are decoded below). The CLI landed with
item 6 (2026-07-21): `unzbd cs anim <archive> <zip>` / `rezbd cs anim <zip> <archive>`
extract to the same zip-of-JSON shape as MW/PM/RC (per-def JSONs + per-script `*.zan.json`
+ `metadata.json`; the CS-only container data — the two base-file entries and the raw
runtime pointers `defs_ptr`/`scripts_ptr`/`world_ptr`/`unk40`/`zero_def_flags`, which vary
per archive and fit no PM-style mission table — ride in optional metadata fields).
Everything below is byte-verified against this install.

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
- **Container** (same family as MW3/PM `anim.zbd`, which mech3ax fully supports — **PM is
  the closest sibling**, verified 2026-07-20 against mech3ax's own structs): 16-byte header
  `{signature u32 = 0x08170616` (identical to MW3), `version u32 = 53` (RC 28 / MW 39 /
  PM 50), `nBase u32 = 2, nAnimFiles u32}` (byte order confirmed: C1 = `16 06 17 08 | 35 |
  02 | AA`); then nBase × `{path char[128], mtime u32}` (the gamez.zbd + planes.zbd the
  archive was built against — a CS-only list), then nAnimFiles × `{path char[80], mtime u32}`
  (the `.zrd`/`.zan` sources; mtimes are year-2000 Unix timestamps; entry shape = mech3ax's
  common `AnimDefFileC` exactly, though CS keeps the count in the header rather than before
  the entries). Then the anim-info block — **PM's 108-byte (0x6c) `AnimInfoC` layout
  verbatim**, decoded field-for-field on C1 cam_anim @0x38E0: `def_count u16` @10 (476),
  `defs_ptr` @12, `script_count u32` @16 (48 — the SI-script pool count), `scripts_ptr` @20,
  `msg_count/msgs_ptr` @24/28 (0), `world_ptr` @32, `gravity f32` @36 (−9.8), `unk40` @40
  (1), `one60` @60 (1), zeros elsewhere; the three pointers are per-archive runtime garbage
  (C1 cam_anim: defs 0x048F631C / scripts 0x048BB5DC / world 0x03B8000C — mech3ax's
  per-game `Mission` tables preserve these for byte-identical round-trip). Then `def_count`
  AnimDef records, then the SI-script pool, which runs byte-exactly to EOF.
- **AnimDef records (semantic decode COMPLETE 2026-07-21, fork plan item 4** — every field,
  support array and sequence event is decoded into mech3ax's API types in
  `crates/anim/src/cs/`; round-trip **byte-identical on all 61 archives**, and the decoded
  `hangar3_doors` matches its reader-JSON source field-for-field — activation, SAVE_LOG,
  all five RESET_STATE ops, all four sequences with their door motions**)**: each record is
  a **272-byte** C struct — PM's 268-byte `AnimDefC` layout with one extra dword — with the
  same field offsets as PM (`anim_name`[32]@0, `name`[32]@40, `anim_root_name`[32]@76,
  flags@156, status/activation/priority/`2`@160–163, exec range@164, `reset_time`@172,
  health@180, `seq_defs_ptr`@204, `reset_state_ptr`@208, `unknown_seq_ptr`@212, eight u8
  counts@216 (seq/object/node/light/puffer/dynamic-sound/static-sound/effect), prereq
  count/min@224, anim-ref count@226, then the support-array pointers). First record is the
  `reserved_anim_0` placeholder (carries its name, ON_CALL activation, INVALID anim ptrs,
  and a per-archive flags dword — C1/M05 has bit 21). After each record its support arrays
  follow inline **in this order**: NAME1 node path, objects, nodes, lights, puffers,
  dynamic sounds, static sounds, activation prereqs, anim refs, SI-script-id list; then an
  optional RESET_SEQUENCE, the counted sequences (64-byte PM `SeqDefInfoC` headers + `size`
  bytes of events), and an optional extra unnamed sequence when `unknown_seq_ptr`@212 ≠ 0.
  **CS deltas from PM** (all byte-verified): `execution_priority` ∈ {1,4,5,6} (PM always
  4); `anim_ptr`/`anim_root_ptr` @72/@108 hold real hash-like values (PM: 0xFFFFFFFF);
  `anim_name`/`anim_root_name` can carry truncation garbage past the terminator (preserved
  as pads); the **"unknowns" array is the compiled `NAME1` node path** — one 36-byte
  `{name[32], node ptr/index u32}` record per path component, e.g. `mp1zrprop11` carries
  {multiplayer1zep, reng11}, exactly `NAME1 ["mp1zrprop1*", ["multiplayer1zep","reng1*"]]`
  resolved for the instance; the **u32 index list** (count in PM's `zero227`@227, pointer
  in PM's `zero264`@264) is the def's **SI-script pool indices** — `freightercruise` → [0],
  `hooked_to_klondike` → [5..10], `cpeject1` → [14..28] — and the def's
  OBJECT_MOTION_SI_SCRIPT events index into it. Object refs are PM's 92-byte shape (node
  *indices* where PM stores pointers, and live `root_idx`); node refs PM's 44-byte shape
  with live `flags`/`root_idx`/`ptr`; a def's node list can contain **duplicate names**
  (same name, different pointers — the mech3ax fork disambiguates with a reversible `~N`
  suffix since events reference nodes by index); light/puffer/dynamic-sound 44 bytes;
  static-sound refs **40 bytes** = one garbage-padded name field (the garbage runs past
  byte 32); **activation prereqs (48 B) ARE used** (contra the earlier survey note): object
  prereqs carry `active` ∈ {0,1,2} and real pointers, `min_to_satisfy` up to the count;
  anim refs 72 B — `ref_ty` **1 = CALL_ANIMATION with LOCAL_NAME** (name + local_name
  halves, both garbage-padded), 0 = plain CALL_ANIMATION. Object/node name fields use MW/PM's
  `Default_node_name` padding convention, with zero-padded and garbage exceptions.
- **Events (all decoded):** the standard 12-byte header `{type u8, start_offset u8 ∈
  {1,2,3}, pad u16 = 0, size u32 incl. header, start_time f32}`. 35 event types occur in
  this install; **every type shared with PM has PM's exact payload layout** (e01 16 B, e02
  60, e04 140, e05 100, e06 8, e07 20, e08 16, e09 20, e10 320, e11 132, e13 12, e14 24,
  e15/e16 4, e17 8, e20 36, e22/e23 36, e24 68, e25 36, e26/e27 36, e28 68, e30 8,
  e31/e33 16, e32/e34 0, e35 4, e36 52, e41 24, e42 584). Three are CS-specific:
  **e12 OBJECT_MOTION_SI_SCRIPT is 64 bytes** `{0, node_index, script_index, 52 zero
  bytes}` where `script_index` indexes the def's script-id list; **e46 = SOUND_ADJUST**
  (176 B, the volume/frequency/pan fade-series op — `hdplayer*` defs; payload not yet
  field-decoded); **e47 = OBJECT_MOTION_SI_SCRIPT in its `ROOT`/`ALL_NAMES` form** (multi-
  node skeletal person animations — `caboosewave`, ladder climbs; payload = count u32 +
  count × 76-byte records, not yet field-decoded). **CS event quirks** (all preserved by
  the fork): `IF`/`ELSEIF` conditions add **NODE_BELOW_ALT 0x100** (node + altitude),
  **ANIM_HEALTH 0x800** (float), **ANIM_HEALTH two-value form 0x1000** (min/max — uses the
  dword PM asserts zero; `locklear_zep_nacelles` `ANIM_HEALTH [20, 32]`), and
  **NODE_ACTIVE 0x2000** (node index); `NODE_NEAR_GROUND` compiles to NODE_UNDERCOVER
  (0x10) **with the value negated**; node references know two sentinels — **INPUT_NODE =
  −200** (also in SOUND AT_NODE and PUFFER_STATE AT_NODE) and **MAIN_ROOT_NODE = −100**
  (`OPERAND_NODE ["MAIN_ROOT_NODE"]`, and plain node refs); e27 INVALIDATE_ANIMATION
  carries index 0 or −100; e24 CALL_ANIMATION has stale small `wait_for` values without
  the flag; e10 OBJECT_MOTION adds flag bit 15 = **`GRAVITY [..., DO_INTERSECTIONS]`** and
  ranges compiled with only the MIN flag bit; e28 FOG_STATE carries real fog names
  (`drop_fog`); e04 light ranges can be negative/reversed; e42 puffer data has
  `START_AGE_RANGE` min −1, zero deviation distance with the flag set, size ranges with
  max == min, and the never-in-MW/PM `UNKNOWN_RANGE` flag live (incl. reversed values).
- **AnimDef flags (partially named):** bit 1 = EXECUTION_BY_RANGE (exact iff with the range
  fields); bit 4 = HAS_CALLBACKS; **bit 5 = "RESET_TIME key present in the reader source"**
  (oracle-confirmed: `hangar3_doors` has `RESET_TIME [-1.0]` and bit 5 set with value −1 —
  in CS the flag does NOT mean the value is ≠ −1); bits 10/11 = NETWORK_LOG SET/ON, bits
  12/13 = SAVE_LOG SET/ON (oracle-confirmed via `SAVE_LOG ON`); unknown CS-only bits: 2
  (prop/rotor spin defs), 14+15 (always together; `m_build`/`s_build` building templates),
  17 (rare; firetrucks/flak), 18 (nearly all defs), 21, 22 (nearly all defs), 23
  (camera/intro defs). The fork stores the raw dword (`flags_raw`) since the unknown bits
  make reconstruction impossible.
- **SI-script pool (fully decoded, fork plan item 5, 2026-07-21):** all 28-byte
  `SiScriptC` headers first (name pointers/lengths, `spline_interp`, `frame_count`,
  `script_data_len` — PM's format verbatim), then per script `{source path\0, object
  name\0, frames…}` with names exactly `strlen+1` (single nul, no garbage) and sizes
  declared exactly. `spline_interp` is a **real per-script bool** in CS (usually true;
  15 of the install's 1090 scripts carry false; PM: always false). Which scripts belong
  to which def is declared too: the def's u32 list (above) holds its pool indices, and
  each e12 event names its slot in that list. Frame = `{flags u32: 1=translate, 2=rotate,
  4=scale; start f32, end f32}` + one **76-byte block per set flag** (`flags=0` frames
  exist — 8,596 of 76,845 frames — and carry no blocks; this is what broke the 2026-07-18
  sentinel-guessing walker). Translate block (verified): `base Vec3` + 4 floats
  `(0, avgVel x,y,z)` + per-axis `{value, c1, c2, c3}` where `component(t) = value + c1·t
  + c2·t² + c3·t³`, `t` seconds since frame start, constant term = the base component
  (absolute) — verified exact against each next frame's base value and C1-continuous;
  the u32 at offset 12 is usually 0 but carries garbage in places (preserved). **Rotate
  block (decoded 2026-07-21):** `base quaternion (w,x,y,z)` + `delta Vec3` + the same
  three per-axis `{value, c1, c2, c3}` cubic blocks, but **relative and in half-angle
  radians**: the constant term is 0 (the cubics give each axis's half-angle offset from
  the frame's base), `delta` = the cubic's average rate over the frame
  (`f(dt)/dt`), and the rotation at time `t` composes **in the parent frame** as
  `q(t) = exp((fx,fy,fz)(t)) ⊗ base` (quaternion exponential of the half-angle vector,
  left-multiplied). Discriminated against right-multiplication and all Euler orders on
  every consecutive rotate-frame pair of the install: 53,515/60,411 pairs close the next
  frame's base to <1e-5 under L-exp (57,675 <1e-3); no competing hypothesis comes close.
  Residuals are compiler fit error on fast rotations (the M01/M02 ladder-climb scripts,
  ~1° over 60 ms frames) plus one script with **uninitialized spline memory**
  (`pfighter11.zan`, C1/M04 — coefficients like 1.7e+27; its base quaternions still march
  smoothly, so bases are authoritative and splines only interpolate within a frame — and
  the reason spline blocks must stay raw bytes for round-trip, which is also upstream's
  own MW/PM choice). Scale block: translate's shape (rare — 477 frames set scale).
- **Validation state:** the fork's semantic decode round-trips **all 61 archives
  byte-identically** with every one of the install's **1090 scripts frame-decoded**
  (76,845 frames) — since item 6 also through the real CLI zip pipeline (test.py
  `--- ALL OK ---`, which additionally exercises the JSON layer). The 2026-07-18 survey's
  "24 of 48 C1 scripts fail to parse" was an artifact of not knowing the record delimiting
  (resolved by the headers + flags=0 frames); no camera/`cpilot_eject` frame-data variant
  exists. A second uninitialized-data quirk besides `pfighter11.zan` surfaced at the JSON
  layer (item 6): `carneypkup_cam.zan;camera1` (C5/M02) carries one degenerate frame
  (start=end=0) whose translate+rotate `delta` vectors are six `0xFFC00000` NaNs — the
  only non-finite decoded floats in the whole install (measured field-by-field). JSON
  cannot represent NaN, so the fork preserves the bits in an optional `delta_raw` field. Train data: 4 scripts
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
## Consuming the extraction (playback, 2026-07-21)

Everything above is about *decoding* the archives. This section is what the Godot side needed
in order to **run** them (`docs/PLAN-anim-playback.md`, revival-plan item 7) — four facts that
are not visible from the byte format alone, each measured against this install.

- **The two sources are complementary; neither is sufficient.** The compiled archives are the
  better data (typed events, resolved refs, the SI scripts), but a mission's `mis_anim.zbd`
  compiles only the defs its `mis_anim.json` lists — C1/IA1 is 160 defs, all of them the eight
  zeppelin files. **`zepstate` and `startanims` are never compiled into any archive**; they stay
  zrdr readers the engine loads at runtime. So a player has to merge both, preferring compiled
  on collision (keyed by anchor name + animation name, which is exactly how the extraction names
  its files: `<name>-<anim_name>.json`).
- **Reader op keys and compiled event tags are the same vocabulary under a spelling change.**
  SNAKE_CASE → PascalCase converts one to the other exactly, across the whole event set
  (`OBJECT_ACTIVE_STATE` → `ObjectActiveState`, `OBJECT_MOTION_SI_SCRIPT` →
  `ObjectMotionSiScript`, `FBFX_COLOR_FROM_TO` → `FbfxColorFromTo`, `IF`/`ELSEIF`/`ENDIF` →
  `If`/`Elseif`/`Endif`). Only the payload *field* names need per-kind mapping — and upstream
  spells the target field inconsistently per event type (`node` on ObjectActiveState/
  ObjectTranslateState, `name` on ObjectRotateState/ObjectMotionFromTo).
- **A support-array `ptr` IS the flat gamez node index — this is the correct binding.**
  Verified exactly: **136,048 references across all 8 chapters' `cam_anim` + every `mis_anim`
  resolve to a node whose name matches**, with the only apparent exceptions being the fork's own
  reversible `~N` duplicate-name suffixes (which the event names carry too, so lookups still
  hit). Events name their target as a *string*, which is ambiguous in the world — C1 has a
  `caboose` (the real consist, a child of the world root) and a `caboose.flt` (an unrelated rail-
  yard instance under a different parent), and name matching drives both, putting one of them
  somewhere wrong. A def's `objects`/`nodes` arrays are therefore its **symbol table**: resolve
  the event's name through them to get the exact index. Reader-sourced defs have no such table
  and keep the wildcard name matching (`ftank0*`, `s_build**`, `air_gen#`).
- **Event scheduling** (inferred from the data, not stated by the format): each event's optional
  `start` is `{offset, time}` with `offset` ∈ `Animation` (since the animation started) /
  `Sequence` (since this sequence started) / `Event` (since the **previous event completed**);
  an absent `start` — 174,938 of the install's events — is `Event + 0`, i.e. as soon as the
  previous event finishes. The discriminating case is the C1 train: each car's sequence is
  `[ObjectMotionSiScript, Loop{-1}]` with no start offsets, and only "after the previous event
  completes" turns that into the surveyed ~327 s track loop instead of a zero-length infinite
  loop. A definition's sequences run **concurrently** — the train drives its four cars from four
  sibling `Initial` sequences, each with its own script and its own loop.
- **JSON-layer trap: the `.zan` rotate quaternion's field labels are shifted.** mech3ax reads the
  file's `(w, x, y, z)` float order straight into a `#[repr(C)] struct Quaternion {x, y, z, w}`,
  so in the emitted JSON **real `w` = json `x`, real `x` = json `y`, real `y` = json `z`, real
  `z` = json `w`**. Byte-identical round-trip is unaffected (the bytes never change meaning), so
  nothing on the mech3ax side reveals it. Verified on the C1 passenger engine: under that remap
  every base quaternion is unit-norm and the yaw tracks the frame-to-frame chord heading to ~1°
  (frame 1 quat-yaw −33.11° against a chord of −43.12°, spanned by the frame's own −0.0505 rad/s
  rate); read literally the values are not even normalised. Undo the shift at the parse boundary.
- **Compiled `PUFFER_STATE` payloads** (consumed 2026-07-21): the event carries the emitter's
  full parameter set inline, so no reader lookup is needed — cross-checked field-for-field
  against `train.json`'s own `steampuffer` (interval 0.03, LOCAL_VELOCITY 0/15/0, SIZE_RANGE
  0.8–1.5, LIFETIME_RANGE 0.5–4.5, friction 3, the five texture names, the three-stop colour
  ramp: all identical). Three shape facts: the emission interval is **always** in
  `interval_garbage.interval_value` (`interval` itself is null in all 4,387 PUFFER_STATE events
  of this install); `GROWTH_FACTOR` arrives as a two-entry `growth_factors` array whose
  **second entry's max** is the reader's scalar (matches 172 of 177 puffers whose name resolves
  to a single reader definition); and an event whose `textures` array is **empty** is an
  adjust/stop stub referencing a puffer another event defines — the readers have the same idiom
  (C1's `truck1dust_puffer` and `black_exhaust_puffer`). `at_node` is the attach point, and is
  NOT the event's `name` (that is the puffer's own name, a separate namespace). `ACTIVE_STATE`
  1 starts a continuous emitter and 0 stops it; definitions re-assert their puffers on every
  loop iteration, so a consumer must treat re-assertion as idempotent.
- **Only `nodes` and `objects` carry node indices.** `lights`, `puffers` and `dynamic_sounds`
  hold runtime pointers instead — measured over C1/C2/C4/C5, **every one** of their 2,616 `ptr`
  values is outside the node-array range, while `nodes`/`objects` resolve 46,481/46,481 and
  31,323/31,323. Admitting the other three into a name→index table silently binds a puffer's
  own name to a bogus index.
- One plan-evidence correction: the `ANIMATION_PATH` key in `mis_anim.json` is the
  **directory** the engine resolves anim sources from (`..\data\c1\ia1\zrdr\zeps`), not a
  waypoint-motion primitive; no waypoint-path op exists in any C1 reader — path motion is
  SI scripts.
