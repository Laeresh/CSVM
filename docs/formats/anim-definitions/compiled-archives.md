# Compiled animation archives

Part of: [animation definitions](../anim-definitions.md).

## Compiled archive reference

The binary archives upstream mech3ax does not support for CS. The project's mech3ax fork
(`tools/mech3ax`) implements them in `crates/anim/src/cs/`: the container, the full semantic
`AnimDef` + event decode, and the SI-script frame decode round-trip **byte-identically on
all 61 archives of this install** with no raw regions left except the per-axis spline
coefficient blocks (kept as bytes by upstream's own MW/PM convention; their semantics are
decoded below). The CLI commands `unzbd cs anim <archive> <zip>` / `rezbd cs anim <zip> <archive>`
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
  the closest sibling**, verified against mech3ax's own structs): 16-byte header
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
- **AnimDef records (semantic decode COMPLETE, fork plan item 4** — every field,
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
  byte 32); **activation prereqs (48 B) ARE used** (contra the survey note): object
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
  count × 76-byte records, each one embedded e12 event: the 12-byte header `{type 12,
  start_offset 1, pad, size 76, start_time 0}` then `{0, node_index, script_index, 52 zero
  bytes}`, where `node_index` is 1-based into the def's `nodes` list and `script_index` a slot in
  its script-id list; `caboosepickup`'s 15 records map `pickup_agent`, `cp_rt` … `cp_torso` onto
  `cabpickup-<part>.zan` in order. The extraction keeps the payload raw; `AnimDefinition.Parse`
  decodes it). **CS event quirks** (all preserved by
  the fork): `IF`/`ELSEIF` conditions add **NODE_BELOW_ALT 0x100** (node + altitude),
  **ANIM_HEALTH 0x800** (float), **ANIM_HEALTH two-value form 0x1000** (min/max — uses the
  dword PM asserts zero; `locklear_zep_nacelles` `ANIM_HEALTH [20, 32]`), and
  **NODE_ACTIVE 0x2000** (node index); `NODE_NEAR_GROUND` compiles to NODE_UNDERCOVER
  (0x10) **with the value negated**; node references know two sentinels — **INPUT_NODE =
  −200** (also in SOUND AT_NODE and PUFFER_STATE AT_NODE) and **MAIN_ROOT_NODE = −100**
  (both resolve to the def's anchor — `AnimRuntime.IsSelfNodeRef`; a `PUFFER_STATE` with
  `AT_NODE INPUT_NODE`, e.g. `large_30sec_fire`'s `fire_n_smoke`, thus emits on the effect's own
  relocated root, which is what puts a called destruction fire at the D32 call/hit site)
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
- **SI-script pool (fully decoded, fork plan item 5):** all 28-byte
  `SiScriptC` headers first (name pointers/lengths, `spline_interp`, `frame_count`,
  `script_data_len` — PM's format verbatim), then per script `{source path\0, object
  name\0, frames…}` with names exactly `strlen+1` (single nul, no garbage) and sizes
  declared exactly. `spline_interp` is a **real per-script bool** in CS (usually true;
  15 of the install's 1090 scripts carry false; PM: always false). Which scripts belong
  to which def is declared too: the def's u32 list (above) holds its pool indices, and
  each e12 event names its slot in that list. Frame = `{flags u32: 1=translate, 2=rotate,
  4=scale; start f32, end f32}` + one **76-byte block per set flag** (`flags=0` frames
  exist — 8,596 of 76,845 frames — and carry no blocks; this is what broke the
  sentinel-guessing walker). Translate block (verified): `base Vec3` + 4 floats
  `(0, avgVel x,y,z)` + per-axis `{value, c1, c2, c3}` where `component(t) = value + c1·t
  + c2·t² + c3·t³`, `t` seconds since frame start, constant term = the base component
  (absolute) — verified exact against each next frame's base value and C1-continuous;
  the u32 at offset 12 is usually 0 but carries garbage in places (preserved). **Rotate
  block (decoded):** `base quaternion (w,x,y,z)` + `delta Vec3` + the same
  three per-axis `{value, c1, c2, c3}` cubic blocks, but **relative and in half-angle
  radians**: the constant term is 0 (the cubics give each axis's half-angle offset from
  the frame's base), `delta` = the cubic's average rate over the frame
  (`f(dt)/dt`), and the rotation at time `t` composes **in the parent frame** as
  `q(t) = exp((fx,fy,fz)(t)) ⊗ base` (quaternion exponential of the half-angle vector,
  left-multiplied). Discriminated against right-multiplication and all Euler orders on
  every consecutive rotate-frame pair of the install: 53,515/60,411 pairs close the next
  frame's base to <1e-5 under L-exp (57,675 <1e-3); no competing hypothesis comes close.
  Residuals are compiler fit error on fast rotations (the M01/M02 ladder-climb scripts,
  ~1° over 60 ms frames) plus scripts with **uninitialized spline memory**
  (`pfighter11.zan`, C1/M04 — coefficients like 1.7e+27; its base quaternions still march
  smoothly, so bases are authoritative and splines only interpolate within a frame — and
  the reason spline blocks must stay raw bytes for round-trip, which is also upstream's
  own MW/PM choice). Scale block: translate's shape (rare — 477 frames set scale).
- **`spline_interp: false` means the spline blocks are GARBAGE, not merely unused
 .** The uninitialized-memory quirk above is not a one-off: `pfighter11.zan`
  is simply one of the **15** `spline_interp: false` scripts, and *every* one of them
  carries junk in its coefficient blocks — leftover pointers, frame times, whatever the
  compiler's stack held. `piratezep.zan` (C1/M04) is the worst: its scale block's constant
  terms are `(0.0, 4.259e27, 4.611e27)`, i.e. a **singular** basis with one zero axis and
  two astronomical ones. Reading those bytes is not a slightly-wrong interpolation, it is a
  destroyed transform, and it propagates to every descendant's world pose.
  **A reader must branch on the flag**, and the non-spline form is plain linear motion from
  the fields that *are* initialised:

  | channel | `spline_interp: true` | `spline_interp: false` |
  |---|---|---|
  | translate / scale | cubic, constant term = `base` | `base + delta·dt` |
  | rotate | `exp(cubic(dt)) ⊗ base`, constant term 0 | `exp(delta·dt) ⊗ base` |

  Both rules were verified against the next frame's `base` over the whole non-spline set:
  translate/scale worst relative error **1.8e-4** (float32 rounding), rotate worst angular
  error **0.022°** over all 576 rotate frame pairs — versus **21.6°** if `delta` is ignored
  and the base simply held. So `delta` is a per-second rate in both, and the non-spline
  frame is *exactly the degenerate cubic* `(base, delta, 0, 0)` — which is how CSVM
  implements it, keeping one branch-free evaluator (`SiVectorChannel.Parse`).

  **The 15 are all one content family**, which is why this hid so long: the C1 `hkzep`
  zeppelin set (`hk_zep`, `lkgasbag01`–`05`, `lkztailgasbag`, `noserotate`, `zfronthalf`,
  `zbackhalf`), C1/M04's intro `piratezep` + `piratefighter`, and three C4 hookup cameras
  (`bhmhookup_cam1`/`cam2`, `cghookupcam1`). Nothing else in the install sets the flag
  false, so seven of eight chapters are completely unaffected by getting it wrong.
- **Validation state:** the fork's semantic decode round-trips **all 61 archives
  byte-identically** with every one of the install's **1090 scripts frame-decoded**
  (76,845 frames) — since item 6 also through the real CLI zip pipeline (test.py
  `--- ALL OK ---`, which additionally exercises the JSON layer). The survey's
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
