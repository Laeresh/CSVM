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
| `EXECUTION_BY_RANGE` | 1 float, **metres** | Proximity gate: the def executes only with a player within this distance of its anchor. Compiled as `execution: {ByRange: {min, max}}` in **metres SQUARED** (reader 50 ↔ compiled 2500) — the same reader↔compiled unit divergence as `PLAYER_RANGE`; `min` is 0 across the install. On an ON_STARTUP def the runtime defers the start until a player first enters the band (the C3 `spiderweb_gone` 50 m fade; the 300 m nacelle-prop spins; C1's reader-only `cloudparent#` at 1900 m). 883 compiled defs carry it. |
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
| `OBJECT_MOTION` | `NAME`, `XYZ_ROTATION` [ix,iy,iz,dx,dy,dz], optional `RUN_TIME` [s] — plus the `GRAVITY`/`TRANSLATION[_RANGE]`/`FORWARD_ROTATION`/`SCALE`/`BOUNCE_SEQUENCE` channels | Two ops in one: a **steady spin** (`XYZ_ROTATION` alone, at `initial` rad/s — deg/s in the reader — endless without `RUN_TIME`; the zeppelin nacelle props) OR a **ballistic body** (translate/launch + scale ramp + tumble under gravity; the crash pieces and debris arcs). See "`OBJECT_MOTION` is two ops sharing one event". |

### `OBJECT_OPACITY_STATE` is translucency, not visibility

`state` is whether translucency is **enabled**; `opacity` is the alpha while it is. The data
settles this: across all 168 compiled uses, `state=false` pairs with `opacity=1.0` **136 times
and with 0.0 never**, so `false` means "render normally", not "disappear". Hiding is
`OBJECT_ACTIVE_STATE`'s job and the data uses both side by side. The three shapes shipped are
`(false, 1.0)` ×136, `(true, 1.0)` ×16 and `(true, 0.4)` ×16.

**The reader writes the token and the value in EITHER ORDER, and the value is optional.** All
five spellings occur: `["ON", 0.6]`, `[0.4, "ON"]`, `["OFF", 1]`, `[1, "OFF"]` and a bare
`["OFF"]` (value defaults to 1). So a normalizer must scan the list by type, not by position.

`OBJECT_OPACITY_FROM_TO` tweens the same pair with a `RUN_TIME`; a fade-out is
`(true,1.0) → (false,0.0)` ×5073 and a fade-in `(true,0.0) → (false,1.0)` ×4609, i.e. the end
state disables translucency once the object is fully opaque again (or is deactivated outright).
**Implemented 2026-07-23** (`AnimRuntime.OpacityFade`, the biggest un-handled kind at 9,917
events): a linear lerp of the two `opacity` numbers over `RUN_TIME`, driven through the same
per-instance `csky_opacity` parameter `OBJECT_OPACITY_STATE` writes. ⚠ **The endpoint `state`
flag does NOT invert the value** — surveyed across all 9,917, `(state=false, opacity=0)` fades to
invisible and `(state=false, opacity=1)` to opaque — so it is a literal lerp of the opacity, not
the "false → render normally (1.0)" rule `OBJECT_OPACITY_STATE` uses. `opacity_delta` is null in
100% of them (the dead relative form, like `OBJECT_MOTION_FROM_TO`'s `*_delta`). Opacity is a
separate channel from the transform, so a fade coexists with a live motion on the same node (the
crash `dust` scales and fades at once). **Only ONE of the 9,917 is ever reached at bootstrap** —
C3's `spiderweb_gone` (`ON_STARTUP`, fades `spiderweb` 1→0 over 0.7 s so the web is gone by
default); the rest are `ON_CALL`/`WEAPON_HIT`, fired by a crash or (in M3) a kill.

**What is reachable:** 683 dispatches across C1/C3/C4/C5, **zero unresolvable**. C1's
`cloudparent#` — `ON_STARTUP`, `EXECUTION_BY_RANGE 1900`, a `LOOP{-1}` re-asserting every frame
— sets the cloud sprites to **0.6**, and is the single largest use in the game; C5 sets `wl_glw`
and `cfglow` to 0.4; C3/C4 drive the barrage balloons (`bont*`/`balloon_t*`/`tether*`) and
`bhf_support*`, all at `state=false` (i.e. normal).

⚠ **Observing the C1 clouds needs two things switched off**, which is why a first pass wrongly
concluded the event had no visible effect: the opaque `cloudlayer` **`CloudDeck` occludes them
from below**, and at any normal viewing distance **fog washes them to exactly `FOG_COLOR`**. With
the deck hidden and fog off (static `--viewer --chapter=C1`, no `--sky-zone`) the effect is
obvious — 74,129 px change, the clouds going from hard opaque white to translucent.

### `FogState` — decoded, deliberately not acted on

The compiled archives carry a `FogState` event kind: mid-mission weather change is a real engine
capability. **The data uses it exactly once install-wide** —
`extracted/C1/M04/mis_anim/camera1-mission_intro_animation.json`, `reset_state/events[4]`
(surveyed across all 12,746 `mis_anim` + 3,368 `cam_anim` files, 2026-07-22):

```json
{"FogState": {"name": "drop_fog", "type_": null,
              "color": {"r": 0.69, "g": 0.69, "b": 0.69},
              "altitude": {"min": 10000.0, "max": 11000.0},
              "range": {"min": 1000.0, "max": 1500.0}}}
```

Note what it is *not*: it carries its fog parameters **inline** and matches neither of C1's
weather.json zones (zone1 1000–1750 alt 970–1047, zone2 1000–4000 alt 4000–5000). So it is an
ad-hoc third fog state applied to a cutscene camera, **not a zone selector** — it does not answer
"which zone does a mission fly", which remains engine-side (see
[weather.md](weather.md#which-zone-a-mission-flies-is-not-in-any-file-searched-exhaustively-2026-07-22)).
`AnimRuntime` therefore does not implement it: one occurrence, on the one cutscene camera the
remake does not run, and implementing it would mean a second write path onto the `csky_fog_*`
globals that `Session.WeatherRig.Build` owns. If the user ever observes fog visibly changing
*during* a mission somewhere else, that is evidence for the engine-side zone switch and this
should be revisited.

### `OBJECT_MOTION` is two ops sharing one event

`OBJECT_MOTION` is the original's rigid-body descriptor, and its 7,442 uses split cleanly into
two jobs that have nothing to do with each other. Surveyed across all 8 chapters, 2026-07-22:

| Shape | Count | What it is |
|---|---:|---|
| `XYZ_ROTATION` only (± `RUN_TIME`) | 3,521 | A **steady spin** about the node's own axes. |
| `+ GRAVITY`/`TRANSLATION[_RANGE]`/`FORWARD_ROTATION`/`BOUNCE_SEQUENCE` | 3,807 | **Ballistic debris** thrown by a kill. |
| `SCALE` only | 114 | A scale ramp (`ballflare`, the explosion flare, 65→75 over 1.75 s). |

**The split is exactly the reachability boundary.** All 590 `ON_STARTUP` uses are
rotation-only; every ballistic use is `ON_CALL`/`WEAPON_HIT`, fired by a crash or (in M3) a kill.
So the rotation half runs through the lightweight `AnimRuntime.SpinMotion` (unchanged — the
ambient world boots byte-for-byte the same), and the ballistic/scale/tumble half is **implemented
2026-07-23** through `AnimRuntime.MotionRuntime`, a full rigid body in the node's parent frame,
seeded from its **authored rest** pose for a launch and from its live pose otherwise (2026-08-01: a
shared effect template's children are never re-homed between calls, so seeding a launch from the
live pose walked every repeat explosion's debris further from the blast than the one before):

- `TRANSLATION.initial` is the launch **velocity** (a crash piece leaves at y=10 m/s); `rnd_xz` a
  per-axis random spread added to it (through the runtime's **seedable** `_rng`, so a lab replay
  is deterministic); `delta` a velocity ramp over `RUN_TIME`, 0 on every reachable piece.
- `TRANSLATION_RANGE` is a ballistic launch in **spherical form** — **`xz` is an AZIMUTH and `y` an
  ELEVATION, both in DEGREES, and `initial` is the launch SPEED in m/s** (`delta` a speed ramp over
  `RUN_TIME`). **Decoded 2026-08-01**, replacing a distance reading that threw debris hundreds of
  metres; census + evidence in `analysis/object-motion-range/`. Over all 1,217 events install-wide
  every `xz` lies in [−170, 359] and every `y` but one in [−90, 90]; `y` goes negative exactly where
  the thing falls (a balloon turret's parts at −70…−90 against a collapsing dock platform's +70…+80);
  the five `fly_trailN` of one explosion carry evenly spaced azimuth bands (35–55, 85–105, 135–165,
  185–205, 235–255) — the starburst the original's HE impact shows; and `gunshell` reads ±10° of
  bearing at −75…−85° elevation and 1.5–1.8 m/s, i.e. brass dropping out of the gun port.
  `TRANSLATION_RANGE_MIN_ONLY` (112 events) marks rows whose `max` fields are all 0 and meaningless
  — there the min IS the value. ⚠ 623 of 3,651 ranges have `min > max`, so interpolate rather than
  clamp. ⚠ Which world bearing azimuth 0 points along (+X in the engine) is a choice, not a decode:
  the data fixes the trails' spacing relative to each other, never their absolute compass.
- `GRAVITY.value` (negative) accelerates the launch, and is an **absolute m/s², not an offset to the
  aircraft's arcade `nom_gravity` of 20** — the census carries a literal −9.8 on 173 events (and −10
  on 400); the weak −1/−2/−3 values sit on smoke trails, where floating is the authored look.
  `DO_INTERSECTIONS` ground-rest and the `BOUNCE_SEQUENCE` re-launch are a **Layer-1.5 follow-up**
  (they need a physics ray) — the body integrates freely over `RUN_TIME` then finishes.
- `FORWARD_ROTATION.Time.initial` is a tumble **total angle over `RUN_TIME`**, not a rate: divide
  by `RUN_TIME` before integrating (read as rad/s, the crash pieces spin ~15 rad/s, visibly wrong;
  the ÷`RUN_TIME` reading passed the crash A/B playtest and remains a TUNE handle, not a decode).
  ⚠ the axis is a reasoned choice (local X): the data carries a scalar, not an axis.
- `SCALE.initial`/`delta` a linear scale ramp (absolute, like `OBJECT_SCALE_STATE`) — the crash
  `dust` grows and shrinks over 6 s.

Verified 2026-07-23 in `--anim-lab --play-anim=player_crash_dirt` (seeded, fixed-dt): the five
`fly_trail*` debris anchors integrate outward and the two carrying `FORWARD_ROTATION` tumble while
the others do not, `dust` runs a scale ramp and an `OpacityFade` at once, `flydirt` sinks on its
`TRANSLATION`. An 8-chapter ambient regression is byte-identical bar C3's one reachable opacity
fade — because nothing ambient fires the ballistic half (see the reachability boundary above).

What the reachable spins are: 576 of 590 are zeppelin nacelle propellers — `spin` at −40°/s and
`counterspin` at +30°/s about local Z, counter-rotating — plus rotating signage (`ammosign`,
`jsign` at 24°/s about Y) and a few `prop`/`rotor` pairs. The companion `propoff` sequence
deactivates the static `propstill` disc and activates the `spin`/`counterspin` blur meshes: the
same static-disc-vs-blur-layer split `PlaneBuilder` already does for the player's own aircraft.

**Units diverge between the front-ends, as usual.** The reader writes one flat six-number list
in **degrees/second**, `XYZ_ROTATION [0, 0, -40, 0, 0, 0]` = initial triple then delta triple;
the compiled form nests it as `{initial:{x,y,z}, delta:{x,y,z}}` in **radians/second**
(−0.6981317 = −40°). Converted once in `AnimDefs.Spin`, the same way `PLAYER_RANGE` (m vs m²)
and `ANIMATION_LOD` (`HIGH` vs `2`) are.

**`initial` is a rate, not a pose** — the question worth settling, since `delta` is zero in 589
of the 590 reachable events, which would make them all *static* under a pose reading. The
autogyro's destruction tumble writes `XYZ_ROTATION [55, 20, -175, 0, 0, 0]` on a wreck falling
under `GRAVITY [COMPLEX, DO_INTERSECTIONS]`; a falling wreck with a fixed pose is not a thing,
and the sequences are named `spin_rotor`. Verified in flight: the rendered prop advances
−40.5°/−81.0° at 1 s/2 s, i.e. exactly the authored −40°/s.

**`delta` is NOT decoded and is deliberately not guessed.** It reads as acceleration on a blown
chassis (`+15°/s` added to a 30°/s spin), as a *decelerating* ramp on `chuteman_sway`
(initial `(-10,0,10)`, delta `(+10,0,-10)`, `RUN_TIME` 2 — a parachutist swaying back), and
could equally be a random spread, which this data uses elsewhere. Nothing reachable needs it, so
it is counted as `ObjectMotion(rotation delta)` and reported — the same call `Object3DRotate`'s
ambiguous angle unit got in `interp.md`.

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

**An absent `OBJECT_MOTION_FROM_TO` channel means HOLD the node's current value**, not
"return to the authored rest pose" — the reader spelling's "a missing FROM means from
wherever the object currently is", generalised to the whole channel. Surveyed install-wide:
883 of 1,802 FROM_TO events carry no rotate channel, and on 89 of them (26 nodes) the held
value differs from rest — C1's traffic and firetrucks, C2's ten studebakers, its sailboats
and yachts (up to `sailboat1`'s 300 s leg held 180° from rest). Seeded from rest, C1's
`black_car1` (one ROTSTATE 180° then a single 16 s translate-only loop) drives its whole
route exactly sideways.

The compiled `*_delta` channels arrive as a bare `{x,y,z}` vector, **not** a `{from,to}`
pair — 26 events install-wide (15 translate, 6 rotate, 5 scale); the reader front-end emits
no delta channels at all. A `{from,to}`-shaped parser reads every one as (null, null), i.e.
the relative form is currently dead in CSVM (tracked in `backlog.md`).

### `IF`/`ELSEIF` conditions are all evaluable

Ten condition kinds appear across the install. None of them is opaque gameplay state that a
world build has no value for — an earlier reading, which led the runtime to skip every
branch. **All ten are evaluated as of 2026-07-21** (`AnimRuntime.EvaluateCondition`).

The compiled payload is a one-key union under `data.If.condition`, e.g.
`{"AnimationLod": 2}`; the reader spells the same conditions with its own vocabulary and, for
two of them, **different units** — that conversion happens once, in `AnimDefs.ReaderCondition`,
so the runtime has a single convention.

| Count | Condition | Reader spelling | Rule used |
|---:|---|---|---|
| 4537 | `RandomWeight` (0..1) | `RANDOM_WEIGHT [w]` | `rand() < w`, re-rolled per evaluation. |
| 4007 | `AnimHealth` | `ANIM_HEALTH [n]` | `health <= n` — "worn down to n". Full health in a world build, so uniformly false. |
| 1052 | `PlayerRange` | `PLAYER_RANGE [m]` | `dist²(anchor, player) <= value`. **Compiled is metres SQUARED** (reader 270 ↔ compiled 72900, exact across the install). |
| 717 | `NodeActive` | `NODE_ACTIVE [name]` | the node is visible. Compiled carries an INDEX, reader a name — see below. |
| 473 | `NodeUndercover` | `NODE_NEAR_GROUND [name, d]` | **stubbed false** — needs a ground/occlusion probe. All 473 sit in `ON_CALL` defs the bootstrap never reaches. |
| 124 | `AnimHealthRange` | — | `min <= health <= max`. Same as `AnimHealth`: false at full health. |
| 120 | `AnimationLod` | `ANIMATION_LOD [HIGH]` | `ourLod >= n`. **Our setting, not the data's** — see below. |
| 120 | `PlayerFirstPerson` | `PLAYER_1ST_PERSON` | our camera mode; false until a cockpit view exists. |
| 28 | `NodeBelowAlt` | `NODE_BELOW_ALT [name, alt]` | node world Y < alt. |
| 17 | `HwRender` | `HW_RENDER` | true. |

`HW_RENDER` and `PLAYER_1ST_PERSON` take **no reader argument** and every compiled instance
stores `false` in the shared 4-byte value slot, so that slot is unused for them and the
condition is the runtime flag itself. (The one piece of counter-evidence is C1B's
`four_bulletholes`, whose branches read backwards under that rule — but it is an `ON_CALL`
player-cockpit def that never runs in a world build, so nothing turns on it.)

`AnimationLod` is a **quality setting**: 2 is the only value anywhere in the install (the
reader spells it `HIGH`), so the project defaults to 2 and every LOD-gated branch passes —
the hardware has no reason to hide detail the original hid only for performance.
`--anim-lod=N` lowers it for A/B comparison.

**Condition node references are 1-based indices into the definition's own `nodes` support
array**, not gamez node indices and not names — mech3ax resolves index→name for every other
event kind but leaves conditions raw. Two negative sentinels ride in the same u32 field:
`-100` `MAIN_ROOT_NODE` and `-200` `INPUT_NODE` (arriving as 4294967196 / 4294967096), both
meaning "the node this definition was invoked on" = the anchor. float32 JSON parsing cannot
tell those two apart, which does not matter since they resolve identically.

This matters because the branches gate real content: C1's `refinery_fire_always` and
`ref_light_always1..6` wrap their entire light sequence in `If { AnimationLod: 2 }`,
`litehouse_sparking` gates its spark bursts on `If { RandomWeight: 0.7 }`, and C1/MP1's
`rearm_node_1/call_door` is a **poll** — `If { PlayerRange: 625 } → CallAnimation; Endif;
Loop{-1}` — that fires the rearm-bay door when the player closes to 25 m. Skipping branches
meant none of it ran.

That poll idiom pins down one more semantic: **`CALL_ANIMATION` does not restart an animation
that is already running** on the same anchor. The call is re-issued every frame the condition
holds, so restarting would freeze the 2 s door at its first frame for as long as you hover.

No `RESET_STATE` in either source contains control flow (verified across the install), so the
instantaneous base-state pass never has to interpret a branch.

Playback ops seen and deferred: `OBJECT_DELETE_CHILD`,
`SOUND` (the one-shot form — see below), `OBJECT_CYCLE_TEXTURE`, `CAMERA_STATE`,
`FBFX_COLOR_FROM_TO`, `CALLBACK`, `DETONATE_WEAPON`.
(`LIGHT_STATE`/`LIGHT_ANIMATION` landed 2026-07-21 — see below;
`SOUND_NODE` + the sound half of `OBJECT_ADD_CHILD` landed 2026-07-22 — see "SOUND_NODE is a
three-event triple"; `OBJECT_MOTION`'s rotation half landed 2026-07-22 and its
ballistic/scale/tumble half 2026-07-23 — see "OBJECT_MOTION is two ops in one";
`OBJECT_OPACITY_STATE` landed 2026-07-22 and `OBJECT_OPACITY_FROM_TO` 2026-07-23 — see
"OBJECT_OPACITY_STATE is translucency".)

`CALL_ANIMATION` dispatched from the start but **ignored its target node** until 2026-07-21 —
see "CALL_ANIMATION carries a target node" below; that is the data's template-instancing
mechanism and `OBJECT_ADD_CHILD` is **not** (surveyed: the fire templates are never its
children, and 75% of its 1,152 uses attach sound *definitions* rather than nodes).

## `CALL_ANIMATION` carries a target node — this is the template-instancing mechanism

Surveyed and implemented 2026-07-21. A call may name **another node to run the callee on**,
and that is how one authored definition serves many sites. Three spellings, two shapes:

| Reader | Compiled | Uses |
|---|---|---|
| `WITH_NODE [name]` | `parameters: {"WithNode": {node, position}}` | 29,633 |
| `AT_NODE [name]` | `parameters: {"AtNode": {node, position, translate}}` | 7,640 |
| `OPERAND_NODE [name]` | `operand_node: "name"` (bare, not nested) | 179 |

`parameters` is a one-key union, so `AnimData.Union()` reads it directly. The reader front-end
normalizes `WITH_NODE`/`AT_NODE` into the compiled shape (`AnimDefs.AddCallTarget`) so the
runtime has a single path. **The target is written in the CALLER's namespace** and resolves
through the caller's definition and scope.

Why it matters, with the case that proves it: C1/M05's eight zeppelin engines each run
`stop_wvzreng1..4`/`stop_wvzleng1..4`, and each calls the **generic** definition `gen_zep`/
`random_prop` — whose whole body is "rotate the node named `propstill` to a random angle" —
targeting its own `propstill`. Ignore the target and all eight calls resolve `propstill`
globally to whichever one is first, so eight engines share one prop angle. Honour it and each
engine gets its own. The same mechanism is what places effect templates:
`CALL_ANIMATION [NAME [huge_30sec_fire], WITH_NODE [rc*_dbase1]]` burns one ship section.

Two implementation notes. Instance identity is `(definition, anchor)`, so retargeting is also
what lets one definition run concurrently on many sites — the `IsLive` check must use the
**resolved** anchor, not the caller's, or the second site is swallowed as a duplicate. And an
unresolvable target falls back to the caller's anchor rather than dropping the call, so a node
the builder skipped cannot make an effect vanish; the fallback is counted and reported.

**There is no index form.** Animations are referenced by name string everywhere in this data —
relevant because the four `fire.zrd.json` definitions are called by nothing (see below).

**Placing an effect template means moving its root.** An effect template hosts its puffers on
its OWN root subtree — `small_yellow_sparks`' puffer `at_node` is `yellow_spark_01`,
`call_crash_trails`' are `fly_trail1..5` — so re-scoping the callee's name resolution to the
call target is not enough: the template's root node must be relocated to the call site, or the
effect emits at the template's authored gamez origin. `AT_NODE`'s `position` is an offset in the
target node's frame, added to the site (world position is what matters — the puffers key off the
host origin). The original instantiates by *copying* the template mesh; a consumer that relocates
the single shared template instead must expect overlapping same-template calls to collapse onto
the last site.

## `STOP_SEQUENCE` halts the named running sequence — or calls it (the stopper idiom)

Decoded 2026-08-01 from an install-wide survey of every site (94 raw occurrences across the
readers, ~73 distinct authored signatures; the wire format is identical to `CALL_SEQUENCE` — a
36-byte struct carrying only the name). One rule satisfies all of them, with zero counter-sites:

**`STOP_SEQUENCE [NAME [x]]`: halt every active runner of sequence `x` on this instance —
including the sequence carrying the event. If none is running, invoke `x` exactly like
`CALL_SEQUENCE`.** Three authored idioms hang off it:

- **Break** (`test_player`×33, `setprop`×8): a sequence stops *itself* inside an `IF` branch —
  `random_prop` picks one of 8 random prop rotations and `STOP_SEQUENCE [setprop]` ends the
  taken pass so the remaining branches never evaluate. Requires the halt reading on self.
- **Halt a running sibling** (`large_30sec_fire`'s `fire_n_smoke`, `zepskinfire`×5,
  `flame_light_seq`): the target is genuinely running — a `LOOP -1` poll or a sequence started
  earlier by `CALL_SEQUENCE`. The halt is load-bearing beyond bookkeeping: a `PUFFER_STATE`
  re-assert *revives* a stopped emitter (the damage-stage sputter contract), so the
  `PUFFER_STATE INACTIVE` these stops pair with cannot end the fire alone — the un-halted poll
  would re-light it one frame later.
- **Stopper** (`flame_ball.zrd`'s `stop_p1trail`): the target is `ACTIVATION ON_CALL`, not
  running at fire time, and its body is pure teardown (`PUFFER_STATE … INACTIVE`,
  `OBJECT_ACTIVE_STATE … INACTIVE`) — the event falls through to a call. The same file uses a
  literal `CALL_SEQUENCE [stop_p1trail]` for the identical purpose elsewhere
  (`moving_fire_ball_01`'s `fly_flare`), which is what settles the fallback: the two events are
  author-interchangeable for reaching a stopper. No target install-wide is reachable *only* via
  `STOP_SEQUENCE`.

Halting a runner never retracts what its events already launched — motions, puffers and lights
run out their own authored lifetimes (the same independence that keeps a rocket ring's scale
motion alive after its launching sequence ends).

## Fire: templates, flipbooks, and a trigger that lives in the exe

Decoded 2026-07-21 while chasing the user's "there is a fire flipbook at the refinery" report.
Three separate layers, none of which is `OBJECT_ADD_CHILD`:

- **Templates.** `fire1`/`fire2` (C1 nodes 494/496) sit under the **parentless roots**
  `fire1.flt`/`fire2.flt` (493/495) — real single-polygon `Facade`/`CylindricalY` meshes on
  materials 88/133 (`fire101.tif`/`fire102.tif`). `WorldBuilder` builds only World children plus
  partition-referenced subtrees, so they are never in the scene. The same is true of the other
  effect roots (`large_firetrail`, `short_firetrail`, `lg_fireball`, `flame_ball_01`, … at
  gamez indices ~74–150).
- **The flipbook — `effects.zrd.json`.** `["fire1.flt", NAME ["fire1"], SPEED [10.0], LOOPING
  ["ON"], MAPS [fire101.tif … fire112.tif]]` and `fire2` with 6 maps @ 5 fps. The maps resolve
  **by filename against the texture archive**, not through the gamez texture table — every
  chapter's `textures.json` registers only `fire101`/`fire102`, while `extracted/<ch>/texture/`
  ships all twelve `fire1NN.png`. That asymmetry is the tell that EFFECTS is its own lookup path.
- **Behaviours — `fire.zrd.json`.** Four `ON_CALL` definitions, **all anchored on `fire2.flt`**:
  `timed_big_fire`, `persistent_big_fire`, `persistent_small_fire`, `timed_small_fire`. They
  scale the template up and back down and flicker a `big_fire_light`.

**EFFECTS binds to the NODE, not the texture or material** (settled 2026-07-21 by user
observation, and it decides the design). `flame01` — the refinery gas flare, node 2998 under
`vent1` → `refinery.flt` → `refinery` → `world1` — renders material 88, i.e. **frame 1 of the
`fire1` flipbook**, as does the 3-polygon muzzle burst `mb_spinflame`. Were the effect bound to
the texture, both would animate for free. A **sustained** muzzle flash never changes texture
(always `fire101`, rotating and flashing but no frame advance), which rules that out — note a
*brief* flash would have proved nothing, since 0.1 s at 10 fps is one frame. So `flame01` is a
static base flame and the animated fire at the refinery is a **placed `fire2` instance**.

**Nothing in the data triggers them.** All four names appear in exactly one file — their own.
No compiled archive, no other reader, and there is no index-based call form. The original
invokes them engine-side, so reproducing a persistent fire requires choosing our own trigger.

## `LIGHT_STATE` / `LIGHT_ANIMATION` — the world's point lights

Surveyed across the whole install 2026-07-21: **1,468 `LIGHT_STATE` events, every one of them
`type_: "PointSource"`** — no directional or spot lights exist in this data. A definition's
`lights` array is its symbol table for them, exactly as `objects`/`nodes` are for geometry, so
a light name is scoped to the definition instance (two refineries each own an `orange_light`).

| Field | Notes |
|---|---|
| `name` | Index into the def's own `lights` array. |
| `active_state` | On/off. true 1147× / false 321×. |
| `translate` | `{AtNode:{name,pos}}` (761×) or null (707×) — a gamez node plus a local offset, the same shape and frame as a puffer's `AT_NODE`. |
| `range` | `{min,max}` — full brightness inside `min`, nothing past `max`. Present on exactly the 1147 "on" events. **Every startup light in this install has a `max` between 2 and 22 m.** |
| `color` | `{r,g,b}`, present 765×. A DX7 sRGB value, so it needs linearising like `FOG_COLOR`. |
| `directional`, `saturated`, `subdivide`, `lightmap`, `static_`, `bicolored`, `orientation`, `ambient*`, `diffuse` | Null or false almost everywhere; nothing in this install depends on them. |

**The load-bearing semantic is that a `LIGHT_STATE` is a PARTIAL update.** A fire or refinery
flicker is a stream of `{name, range}` events 0.03–0.07 s apart that must leave position,
colour and active state untouched — the reader spells this the same way
(`"LIGHT_STATE", ["NAME", […], "RANGE", […]]` and nothing else). The compiled nulls line up
exactly: `range` is null on precisely the 321 events that switch a light *off*. So a handler
must apply only the fields present and never default the absent ones. 66 reader files also
carry `LIGHT_STATE`, so the zrdr front-end needs the same normalizer (`AnimDefs.AddLightState`)
— skipping it would repeat the `PUFFER_STATE` bug in a subtler form.

`LIGHT_ANIMATION` (535 events) ramps a light over `run_time`, and its `range`/`color` are
**signed deltas, not targets**: C1B's `ap_light` pulse runs `{min +50, max +160}` over 0.1 s
then `{min −50, max −160}` over 0.05 s, and a negative range is not a value a light can hold.
The reader's `RANGE` carries four numbers (`[min, max, altMin, altMax]`) where the compiled
form splits the trailing pair into `range_alt` (null throughout this install).

**Which chapters actually light anything:** only **C1**. It is the sole chapter with
`OnStartup` definitions containing `LIGHT_STATE` (36 of them — the refinery flare, six
docklights, six reflights, the police light); every other chapter's light events sit in
`ON_CALL` combat/destruction effects (`gunhit_lt`, `muzzle_lt`, `fuel_light`) that a bootstrap
never reaches. C1 reports 35 lights at startup, growing to ~57 as delayed and looping
sequences fire.

**What a point light is FOR here.** The visible flare at a light's own position is *already*
separate gamez geometry — C1's `docklight_flare` is a `Facade`/`SphericalY` mesh textured
`dock_liteflare.tif`, and the refinery's `flame01` is a `Facade`/`CylindricalY` mesh textured
`fire101.tif`. Both render without any animation. So `LIGHT_STATE` is not what draws the lamp;
it is the **spill onto surrounding geometry**, which is what the original's DX7 point lights
did to the same baked vertex lighting our world shader reads. Rendering notes in
`docs/architecture.md` under `WorldLights.cs`.

**Cost warning for anyone adding a handler here.** These events are not occasional. C1 fires
~2,700 `LIGHT_STATE`s per second at steady state, because each fire's flicker re-issues its
*full* event — `AT_NODE` included — every loop iteration. Resolving that node name per event
put ~1,740 calls/second through `AnimRuntime.ResolveOne`'s full-world fallback scan (7,064
nodes against a regex matcher, ~12M comparisons/second) and cost ~7 ms/frame on its own. The
name is what identifies the target, so the resolution is cached per light and only redone when
the name changes.

## `SOUND_NODE` is a three-event triple — the world's ambient audio

Implemented 2026-07-22 (`src/Mech3/WorldSounds.cs`). This is the waterfall roar, the train, the
firetruck and police sirens, the zeppelin nacelle engines, the fire crackle and the cockpit
warning beeper.

**`SOUND` and `SOUND_NODE` are different animals, and only one of them is ambient world audio.**
Surveyed across the whole install:

| | `SOUND_NODE` | `SOUND` |
|---|---|---|
| events | 1,244 | 6,091 |
| distinct names | **10** | 87 |
| all present in `sounds.json` | yes (10/10) | no (21 missing) |
| `3D` | 10/10 | 41/87 |
| `LOOPED` | 9/10 (`snd_freighter` is the exception) | 3/87 |
| activation | 951 `OnCall`, **293 `OnStartup`** | 4,378 `OnCall`, 1,650 `WeaponHit`, **8 `OnStartup`** |

So `SOUND_NODE` is a small, fully-resolvable set of looping positional emitters bound to nodes,
and `SOUND` is one-shot combat/destruction audio — gated behind the weapon hits and death
sequences M3 now produces, and 21 of its names are not plain `sounds.json` entries at all but
`DYNAMIC_WEIGHTS` groups (`air_mixed_exp_sg` picks one of five `snd_exp_hit*` at random) needing
their own decode. The ambient half landed first; the one-shot half **landed in M3 D31** —
`AnimRuntime.HandleSound` fires a fire-and-forget `WorldSounds.PlayOneShot` at the event's
AT_NODE, resolving a `SOUND_GROUPS` name through the decode now in
[sounds.md](sounds.md#soundsjson--the-sound_groups-block). The event's NAME is a sound
*definition* or a group, never a gamez node (the lone reader-scope one-shot names
`snd_waterfall`, a definition, and resolves zero node targets — see the C3 note in
`PLAN-anim-rendering-followups.md`).

**The reader spells one emitter as three consecutive events**, which is the whole shape of the
feature:

```
SOUND_NODE          ["NAME", ["snd_waterfall"]]                        -- declare the emitter
OBJECT_ACTIVE_STATE ["NAME", ["snd_waterfall"], "STATE", ["ACTIVE"]]   -- switch it on
OBJECT_ADD_CHILD    ["PARENT_CHILD", ["waterfall01", "snd_waterfall"]] -- attach it to a world node
```

The middle event is an ordinary `OBJECT_ACTIVE_STATE` whose NAME is a **sounds.json definition,
not a gamez node** — letting it fall through to the normal node resolution scans the world for
`snd_waterfall`, finds nothing, and books an unresolved op. The third is what positions the
emitter.

**The compiled form carries the same three facts inline** — `{name, active_state, translate}` —
but only sometimes: `translate` is an `AtNode` on **379** events and null on exactly **865**, and
865 is also exactly the number of `OBJECT_ADD_CHILD` events that attach a sound definition
(`snd_zepengine`→`spin` alone is 849). The two counts matching to the event is what proves
`SOUND_NODE` and the sound three-quarters of `OBJECT_ADD_CHILD` are **one mechanism**, which is
why they had to land together — and it is the concrete form of the dependency noted when
`OBJECT_ADD_CHILD` was withdrawn ("it is mostly the positioning layer for sound emitters, so it
should follow `SOUND`, not precede it").

Two field traps:

- **Compiled `active_state` is a JSON boolean**, where `PUFFER_STATE`'s identically-named field is
  numeric. Reading it with a number accessor returns null for `true`, and an absent-means-leave-
  alone default then leaves every emitter in the world switched off. That is exactly what it did
  until the headless log showed 38 correctly-placed emitters all reading "off".
- **The reader form carries no `active_state` at all** (its ACTIVE is the next event), so absent
  must mean "leave alone" and not `?? 0` = OFF — the same shape as the bug that silently killed
  the C1 waterfall's splash puffers.

**Reader-only coverage is dormant.** Of 252 reader `SOUND_NODE` definitions across the chapters,
231 have a compiled twin (compiled wins in `AnimProgram`), and all 21 that do not — `sprucegoose`/
`g_enginesound`, `locklear_gasbag`, the zep nacelles — are `ON_CALL`, which the bootstrap never
reaches. So the reader triple path is implemented and correct by construction but is not
exercised in a default session, the same status `NODE_UNDERCOVER` has.

**An emitter is silent while its host is not visible in tree**, the same rule the point lights
use: C1/IA1 deactivates both multiplayer zeppelins, so 36 of its 38 emitters are built and
stopped, and only the waterfall and the train sound. That is also what makes the counts safe —
C5 builds 108 emitters and plays none.

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

### Mission-spawned entities — SOLVED 2026-07-22, and not by a roster

Scenery props are hidden by compiled `zepstate` defs as above. *Entities* — the zeppelins,
the CTF props, the vehicles and guns — are governed by a different system entirely: the
**per-mission interp boot script** `support\<chapter>\<mission>.gw`, documented in
[interp.md](interp.md). They are present by default and the script switches them off, which
is the same polarity as `zepstate`, not the mirror image of it.

C1's `hk_zep` (the Hollywood Knights zeppelin, at (-5248, 200, -5208) beside `tethertower`)
has no def in IA1 scope at all, and needs none: `support\c1\ia1.gw` contains
`FindNode hk_zep` / `NodeSetActive off`, while `support\c1\m04.gw` does not — which is
exactly why it is on the field in M04 and nowhere else. The CTF props are switched off by
every mission script except `mp2.gw`.

⚠ **This corrects the roster hypothesis previously recorded here.** Neither candidate could
have gated anything, and both were checked before implementing:

- **`aiv.zrd.json`** is the AI *vehicle* table, not a spawn roster. Its only mention of
  `hk_zep` anywhere in the install is inside a wingman's target-priority list in C1/M02; C1/M04,
  the one mission that shows the zeppelin, does not name it at all.
- **`zeppelins.zrd.json`** is the flyable-zeppelin gameplay config (position/yaw/engines/
  cannons/gasbags). C1/IA1 lists `multiplayer1zep` — a node that mission's boot script
  switches *off* — and C1/M04 lists only `piratezep`.

The `dliner1` caveat that motivated the roster idea also dissolves: no name pattern is
involved, the script names its nodes outright.

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
(`tools/mech3ax`, plan `docs/plans/PLAN-mech3ax-cs-revival.md`) has implemented them in
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
  ~1° over 60 ms frames) plus scripts with **uninitialized spline memory**
  (`pfighter11.zan`, C1/M04 — coefficients like 1.7e+27; its base quaternions still march
  smoothly, so bases are authoritative and splines only interpolate within a frame — and
  the reason spline blocks must stay raw bytes for round-trip, which is also upstream's
  own MW/PM choice). Scale block: translate's shape (rare — 477 frames set scale).
- **`spline_interp: false` means the spline blocks are GARBAGE, not merely unused
  (2026-07-22).** The uninitialized-memory quirk above is not a one-off: `pfighter11.zan`
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
in order to **run** them (`docs/plans/PLAN-anim-playback.md`, revival-plan item 7) — four facts that
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
  ⚠ **Counter-example: the generic plane defs' indices are non-portable.** The compiled
  `player_crash_dirt` call closure references `piece1..4` at node indices 7703–7706 — out of
  range for the shipping planes gamez, because the def is generic across all 11 aircraft and no
  single plane's index space can satisfy it. The 136,048-reference verification above covers the
  world-scope `cam_anim`/`mis_anim` defs; a consumer binding plane-scope defs must resolve by
  NAME (and note a plane subtree staged into a world scene mixes two index spaces that collide —
  world index 400 and plane index 400 are different nodes).
- **Event scheduling** (inferred from the data, not stated by the format): each event's optional
  `start` is `{offset, time}` with `offset` ∈ `Animation` (since the animation started) /
  `Sequence` (since this sequence started) / `Event` (since the **previous event completed**);
  an absent `start` — 174,938 of the install's events — is `Event + 0`, i.e. as soon as the
  previous event finishes. The discriminating case is the C1 train: each car's sequence is
  `[ObjectMotionSiScript, Loop{-1}]` with no start offsets, and only "after the previous event
  completes" turns that into the surveyed ~327 s track loop instead of a zero-length infinite
  loop. A definition's sequences run **concurrently** — the train drives its four cars from four
  sibling `Initial` sequences, each with its own script and its own loop.
  A present `start` gates **the event it is attached to**, not its successor — including the first
  event of a sequence, and including control-flow events (`LOOP`/`IF`), which take no run time and
  therefore do not advance the "previous event completed" base. The discriminating case is C1's
  bowl sign (`bowl`, gamez 4868, def `on_off`): nine strict on/off SWAP pairs where only the first
  event of each pair carries a timestamp — gating the *successor* instead shifts every sequence in
  the install by one slot (timestamped events fire a slot early, their unstamped partners a slot
  late, the sign spends 38% of frames blank), and a loop back-jump that resets the gate to zero
  discards the trailing `Loop {Event 1.2}`'s inter-cycle pause.
- **`LOOP` has two spellings of "infinite": `-1` and `0`** (decoded 2026-07-22). `-1` is the
  common one; `0` is *not* "run zero more times". Across the compiled `cam_anim`/`mis_anim` of the
  whole install the count distribution is **`-1` × 2,919, `0` × 26, positive N × 530**, and all 26
  zeros sit in 25 defs that are, without exception, **ground-vehicle route animations** — C1's
  `police_car`/`mafia`/`black_car1`/`truck1`/`car_loop1`/`car_go_home`, C2's ten `studebaker*`,
  C3/M02's nine `stude_move*`. Every one is `activation: OnStartup`, and in every one the `LOOP`
  is the **last event of its sequence**, over a body of `ObjectMotionFromTo` legs carrying explicit
  from/to (so a replay re-seats the car at the route start). Nothing that must terminate uses it:
  no door, gate, one-shot, hangar, bomb or explosion def carries `Count: 0`. Reading `0` as "stop"
  makes every car in the game drive its route once and freeze.
  **The reader (`zrdr`) scope never uses it** — 703 `LOOP` events there, `LOOP_COUNT` ∈ {`-1`
  (575), positive N}, zero zeros. (An earlier note claimed the reader scope has *no* `LOOP` events
  at all; it has 703. The usable fact is the absence of `0`, not the absence of `LOOP`.)
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
  ramp: all identical). Four shape facts: the emission interval is **always** in
  `interval_garbage.interval_value` (`interval` itself is null in all 4,387 PUFFER_STATE events
  of this install); the number in `interval_garbage.interval_value` is **seconds** for a Time
  emitter but **meters** for an `interval_type: "Distance"` trail (the crash-debris
  `spurtpuffer`s), and its flag shape is inverted — `has_interval_value` is **false** even when
  `interval_value` holds the real distance, so key off `interval_type`, never the flag;
  `GROWTH_FACTOR` arrives as a two-entry `growth_factors` array whose
  **second entry's max** is the reader's scalar (matches 172 of 177 puffers whose name resolves
  to a single reader definition); and an event whose `textures` array is **empty** is an
  adjust/stop stub referencing a puffer another event defines — the readers have the same idiom
  (C1's `truck1dust_puffer` and `black_exhaust_puffer`). A `textures[]` entry's `run_time` is a
  **fraction of the sprite's lifetime**, not a second count — the survey that settles it, and what
  reading it as seconds did to `large_30sec_fire`, are in
  [effects.md](effects.md#puffer_state-schema). `at_node` is the attach point, and is
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
