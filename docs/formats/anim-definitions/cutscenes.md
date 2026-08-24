# Cutscenes: the `letterbox` node and `CALLBACK` codes

Part of: [animation definitions](../anim-definitions.md).

Source: the `letterbox`/`camera1` nodes in every chapter's `gamez.zbd`, the shared readers
`zrdr/letterbox.zrd`, `zrdr/generic_intro.zrd` and each chapter's `zrdr/landings.zrd`, the
per-mission intro readers (`C1/M04/zrdr/intro.zrd` and `scenes.zrd` are the worked example),
and `crimson.exe`. Scope: how a scripted cutscene presents itself (black bars, camera, suspended
simulation) and what the `CALLBACK` event kind means. The event's own reader syntax and its
compiled shape are on the [landing page](../anim-definitions.md) and in
[`org/sequences.md`](../../org/sequences.md).

## Contents

- [Conceptual model](#conceptual-model)
- [The `letterbox` node](#the-letterbox-node)
- [`CALLBACK`: the dispatch chain](#callback-the-dispatch-chain)
- [`CALLBACK` code reference](#callback-code-reference)
- [The intro defs' eight dispatches](#the-intro-defs-eight-dispatches)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

A cutscene in this engine is an ordinary animation definition that happens to drive `camera1`,
plus two mechanisms that make it read as a movie instead of as gameplay.

**The bars are data.** `letterbox` is a pair of opaque black quads shipped in every chapter's
gamez and a four-line shared animation definition that activates them and glues them to the
camera. The string `letterbox` does not occur anywhere in `crimson.exe`; nothing engine-side
knows the feature exists. Any def that wants bars calls the def by name.

**The state changes are notifications.** A `CALLBACK` event carries one integer and nothing else.
The engine's handler passes that integer to whatever native function is registered on the running
animation instance, so **the authored value is the whole message and its meaning belongs to the
registered host**, not to the def it sits in. The host that mission scripting installs is a single
interpreter with a switch over the integer; a different host (the vehicle-death one) reads the
same integers as something else entirely.

The two mechanisms are independent. The bars need no callback, and the callbacks say nothing
about the bars.

## The `letterbox` node

### The geometry

`letterbox` is node index 6 in every chapter's `nodes.json`, sitting among the engine roots
(`world1`, `display`, `window1`, `camera1`, `sgwin`, `spyglass`). It carries no model of its own
and exactly one child, `g1`, which carries model index 0. That model is eight vertices forming
two quads in the camera's own frame:

| quad | x | y | z |
|---|---|---|---|
| top bar | ±6.6461835 | 2.490714 to 4.0474105 | −7.5 |
| bottom bar | ±6.6461835 | −4.0474105 to −2.460203 | −7.5 |

Every vertex colour is white, every UV is `(0, 0)`, and both polygons use **material 0, a
`Colored` material of `rgb (0, 0, 0)` at `alpha 255`**, opaque black with no texture. The card as a
whole is 13.2924 wide by 8.0948 tall with a 4.9509-tall gap between the bars, so the open window
is about 61 % of the card's height and the bars overhang the card's own width by design. The
record is bit-identical in all eight chapters (C1, C1B, C1C, C2, C2B, C3, C4, C5), model 0 in
each.

### Why the node ships `active: false`

`zrdr/letterbox.zrd` holds one definition, and its `RESET_STATE` is a single
`OBJECT_ACTIVE_STATE { NAME letterbox, STATE INACTIVE }`.

`RESET_STATE` is the object's authored base state, applied at load
([landing page](../anim-definitions.md#definition-fields)). The gamez therefore ships the node in
exactly the state its own definition asserts for it: **the `active: false` flag is not an opt-out
and not dead data, it is the def's base state baked into the scene**. The bars exist, fully built,
from mission load onward and are simply switched off until something calls the def. This is the
same node pair `world-structure.md` counts as one of the two parentless roots shipped inactive.

### What the def does

```
ANIMATION_DEFINITION NAME[letterbox] ACTIVATION[ON_CALL] EXECUTION_PRIORITY[5] RESET_TIME[0]
  RESET_STATE          OBJECT_ACTIVE_STATE letterbox INACTIVE
  SEQUENCE_DEFINITION  OBJECT_ACTIVE_STATE letterbox ACTIVE
  SEQUENCE_DEFINITION  OBJECT_TRANSLATE_STATE letterbox AT_NODE[camera1]
                       OBJECT_ROTATE_STATE    letterbox AT_NODE_MATRIX[camera1, 0, 0, 0]
                       LOOP LOOP_COUNT[-1]
```

The first sequence turns the bars on, in one event with no tween: **the bars are simply there from
the moment the mission load ends**, and the intro's own `fadefromblack` FBFX then fades up behind
them. There is no bars-in animation to reproduce (answered at the controls against the original).

The second is the `LOOP{-1}` re-assert idiom: every tick it
copies `camera1`'s position onto the node and adopts `camera1`'s orientation matrix with a zero
offset. **The node is never reparented** (no `OBJECT_ADD_CHILD` anywhere targets it), so the bars
stay a parentless root that tracks the camera by transform copy, and the quads' authored `z = −7.5`
puts them 7.5 m in front of the eye, well inside `camera1`'s `clip_near` of 1.0.

The def is `ON_CALL` and every call site gives it the local name `local_letterbox`. Nothing in the
install ever stops it: a scan of every `STOP_ANIMATION`, `INVALIDATE_ANIMATION` and
`RESET_ANIMATION` in every reader finds **zero** that names `letterbox` or `local_letterbox`. The
bars go away when the node returns to `INACTIVE`, which is what the def's own `RESET_STATE` does.
`RESET_TIME [0]` is the scheduling field whose detail this project has not decoded, so the exact
moment of that reset is an open question; see [limits](#evidence-and-limits).

### Where it is called from

27 `CALL_ANIMATION letterbox` sites across 18 readers:

| reader | sites | what it is |
|---|---|---|
| `generic_intro` | 1 | the shared story-mission intro, in `start_script` |
| `C1/M04` `scenes.zrd` | 5 | the bespoke M04 intro's scene beats |
| `hangar_drop` | 4 | C1/M02 hangar drop |
| `blacke_drop`, `bhmhookup` | 2 each | C4 drop and hookup |
| `pzep_hookup`, `copilot_pkup`, `pickford_pickup`, `paratroopers`, `tex_drop`, `destroy_cargozep`, `wingwalk`, `wv_tailhook`, `sparks_pkup`, `cghookup`, `carney_pkup`, `nypd_drop`, `miles_drop` | 1 each | mid-mission pickup, hookup and drop cutscenes |

Only one of the 27 is an intro. **Letterbox is the engine's general cutscene idiom**, and the
mid-mission pickup and hookup sequences are cutscenes by the same construction as the intros.

## `CALLBACK`: the dispatch chain

| stage | where | what happens |
|---|---|---|
| reader parse | `FUN_00518470`, reached from the event-name dispatch in `FUN_0051c3e0` | `CALLBACK [VALUE[n]]` (plus optional `START_TIME`) becomes a 16-byte event of kind `0x23` (35) carrying `n` at `+0xc`. Parsing one also sets the def's has-callbacks flag, bit `0x10` at def `+0x9c`. |
| runtime dispatch | slot 35, `FUN_004ec5e0` | calls the animation instance's registered native function at `anim+0x74` with `(anim, anim+0x78 context, value)`. **If none is registered the event does nothing.** |
| host registration | `FUN_004ee160(anim, fn, ctx)` | the only setter, 13 call sites install-wide. |
| the mission-script host | `0045e0f0` → `FUN_0047e080` | the interpreter with the code switch below. |

Of the 13 registration sites, exactly one installs the mission-script host: **`FUN_0045df60`, the
`landings.zrd` trigger**. The other twelve belong to vehicles, weapons and the danger-zone system,
and each installs its own handler with its own reading of the integers (the vehicle-death handler
`LAB_00480710` takes 0, 15 and 16 only, see [`org/vehicleDamage.md`](../../org/vehicleDamage.md)).

### `landings.zrd` is the cutscene trigger table

Each chapter ships a `zrdr/landings.zrd` of records shaped
`anim [name], node [name], angle [deg] speed [min, max]` or `anim, node, auto`. C1's three:

```
anim[hooked_to_klondike] node[pz_manual_land] angle[35] speed[50, 220]
anim[hooked_to_klondike] node[pz_auto_land]   auto
anim[lookat_copilotpkup] node[agent_approach_cone] angle[45] speed[50, 250]
```

`FUN_0045df60` ticks these every frame: with no cutscene already running and the player not already
in the uncontrolled state, it tests the player against the named approach node's condition object
and speed band, starts the named animation, and registers the mission-script host on it. That
registration is why a mid-mission cutscene's `CALLBACK`s are live. 32 distinct anim and node names
appear across the eight files, all of them approach cones, hookups and landings; **no intro
definition is named in any `landings.zrd`**.

### The intro defs run without a host

Intro definitions are bootstrapped from `StartAnims.zrd`, whose two sections are `NEW_GAME_START`
and `LOAD_GAME_START` (C1/M04 lists `mission_intro_animation` under `NEW_GAME_START` only, so the
intro plays on a fresh start of the mission and not on a load). The loader is `FUN_0046c370`
(`mission.cpp`), and it starts each named def with `FUN_004edda0(def, 0, 0, 0, 0)` and **no**
`FUN_004ee160` call. A def started that way has no host, so `FUN_004ec5e0` finds a null pointer at
`anim+0x74` and every `CALLBACK` in it is a no-op in the original as well.

The engine does the equivalent imperatively instead. `FUN_004654e0`, the new-mission start,
runs `FUN_0041f250` / `FUN_004a95f0` / `FUN_004516e0(0)` / `FUN_00453660(0)`, which is code 913's
body verbatim, then `FUN_00455800(0)` (chrome off) and sets the player's `+0x91d` cutscene flag,
and only then calls `FUN_0046c370(StartAnims.zrd, NEW_GAME_START)`. The intro's authored
notifications restate a state the engine has already entered.

For a remake this matters in one direction only: **the codes are still the authoritative
description of the cutscene's shape** (what is hidden, when the simulation stops, when control
returns), which is what a cutscene player has to reproduce. They are not a set of messages that
must be delivered to an existing listener.

## `CALLBACK` code reference

The mission-script host `FUN_0047e080`. Counts are occurrences in the loose `zrdr` readers of this
install; the compiled archives carry the same events.

| code | count | what the host does |
|---|---|---|
| 0 | 0 | clears the active-cutscene slot if this animation owns it. Never authored. |
| 1 | 20 | `FUN_0042e5c0`: `FUN_00493e40` clears the player's `+0x91d`/`+0x91e`/`+0x91f` cutscene flags and restores the view target, then `FUN_00455800(1)` puts the cockpit and HUD chrome back. **The end-of-cutscene handoff to gameplay.** |
| 2 | 22 | `FUN_0042e5a0`: clears the view target (`FUN_0042c250(0)`) and `FUN_00455800(0)` hides the cockpit and HUD chrome. **The cutscene presentation, on.** |
| 3 | 2 | forces the view back to mode 0 and clears the two view-mode slots at `DAT_0064ef60`/`DAT_0064ef64`. |
| 10 | 5 | restores the in-flight systems as a block: engine audio (`FUN_004a0af0`/`FUN_004a0a30`), `FUN_00455800(1)` chrome on, `FUN_00494b20`, `FUN_00443d60(1)`, `FUN_004696f0`, and `FUN_004b24d0` on the player. |
| 11 | 21 | `FUN_004b1510` releases the vehicle's four sound handles, sets the player's `+0x91d` and `+0x91e` cutscene flags, `FUN_00455800(0)` chrome off. **Takes the player out of flight.** |
| 12 | 4 | in multiplayer, hands control back through `FUN_00470a10`; otherwise re-arms the player's crash animation path. |
| 13 | 3 | `FUN_00463c10(1)`, the same call the objectives runtime makes when a primary objective completes, then the mission-end path `FUN_00443090`. Partially decoded. |
| 14 | 6 | **not handled.** Falls through the host's switch. See the gap note below. |
| 15, 16 | 12, 12 | not this host's: the vehicle-death handler `LAB_00480710` takes these ([`org/vehicleDamage.md`](../../org/vehicleDamage.md), [`org/objectMotion.md`](../../org/objectMotion.md)). |
| 20 | 4 | stores this animation in `DAT_0071c50c`, the active-cutscene slot. **Consequence: the per-frame world update `FUN_004897c0` and the objectives update `FUN_0046a490` both return immediately while the slot is set, so the simulation is suspended for the duration.** |
| 86 | 1 | `FUN_005aef00`. |
| 123 | 8 | **not handled.** |
| 666, 667 | 2, 3 | `DAT_00621378` gates automatic application of the camera-parameter profile when the view mode changes (see [camparam.md](../camparam.md); the applier is `FUN_00472ea0`). 666 clears the gate so a cutscene's own camera work is not overwritten; 667 restores it and resets the view mode to 0. |
| 701, 702 | 1, 1 | applies camera-parameter set `code − 700` directly. |
| 800–803 | 1 each | mission-specific: damage `cargozep1`, and three "is any of this wing still alive" sweeps over `bhatwarhawk*`/`bhatbrigand*`/`bhatgyro*`. |
| 913 | 7 | `FUN_0041f250` parks every AI vehicle that is not the player and not itself in a cutscene: sets its hold flag, pushes its next-think time far out, and deactivates its scene node. Plus `FUN_004a95f0` (detaches the wave director's node update), `FUN_004516e0(0)` and `FUN_00453660(0)`. **Clears the world of AI aircraft for the duration of the movie.** |
| 914 | 8 | the exact inverse (`FUN_0041f2e0`, `FUN_004a9610`, `FUN_004516e0(1)`, `FUN_00453660(1)`), reactivating each AI vehicle with a randomised next-think. Skipped in multiplayer. |
| 950, 951 | 14 for 951 | 951 teleports the player to the current camera pose and rebuilds its motion state; 950 is a lookup form. |
| 965, 966, 967 | 1, 1, 3 | swap the player onto a specific airframe (`pbloodhawk`/`player_bhawk`, `pwarhawk`/`player_warhawk`, `pbalmoral`/`player_balmoral`) with its armour and hardpoint table, and set the cutscene flags. The data-side counterpart of the intro defs' `check_balmoral`/`check_warhawk` branches. |
| 968 | 1 | tests `bswingman_1`. |

**The two gaps, named.** Codes **14** (6 occurrences, four of them in the intro defs) and **123**
(8 occurrences) reach no case in `FUN_0047e080`; its switch covers 0, 1, 2, 3, 10, 11, 12, 13 and
20 below `0x15`, and 4–9 and 14–19 fall through. No other host that can be installed on a cutscene
animation takes them either, since the only host the cutscene trigger installs is this one. They
are recorded as gaps, not guessed at: nothing in the exe tells us what 14 or 123 were meant to do.

### Handoff and skip

Two exe paths end a cutscene without a `CALLBACK`, and a cutscene player needs both behaviours:

- **Handoff.** `FUN_00480480(vehicle)` clears `+0x91d`/`+0x91e`/`+0x91f` and force-stops the
  animation recorded at `vehicle+0x6d0`. Its caller `FUN_00470a10` follows that with
  `FUN_00455800(1)` (chrome back) and a view-mode restore.
- **Skip.** The per-frame state core `FUN_004a0220` (`StateCore.cpp`) checks the active-cutscene
  slot against an input poll each frame: while `DAT_0071c50c` is set and `FUN_00536000` reports an
  event, it clears the slot and force-stops the animation through `FUN_004ed480`.

## The intro defs' eight dispatches

`camera1-generic_intro` (shared; 12 of the 13 story missions bootstrap it) authors exactly **eight**
`CALLBACK` events, and they are the eight the project's census counts per chapter:

| where in the def | codes, in authored order |
|---|---|
| `RESET_STATE` | 1, 914, 10, 667 |
| `SEQUENCE_DEFINITION callback_sequence` | 20, 2, 11, 14 |

C1/M04's bespoke `camera1-mission_intro_animation` authors the same eight in the same two places,
with `RESET_STATE` ordered 1, 10, 914, 667 and a ninth, **913**, closing `callback_sequence`.

A tenth code, **666**, is authored once more inside `generic_intro`, in the `gi_1stperson` sub-def.
That def's `NAME` is `player`, not `camera1`, which is why a census anchored on `camera1` does not
count it. It sits immediately before a `CAMERA_STATE` that pulls `camera1`'s near clip in to 1 m,
which is exactly the case where the automatic camera-parameter profile must not overwrite the
authored value.

### ⚠ The codes do not identify a cutscene

`camera1-player_setup` authors **the same nine codes** (1, 10, 914, 20, 2, 11, 14, 913 plus the
`RESET_STATE` ordering), and every Instant Action mission bootstraps it out of its own
`startanims.zrd`; C1/M04 lists it under `LOAD_GAME_START`. What the original does with it is
undecoded, and it is not one of the 13 story-mission intros the `generic_intro` /
`mission_intro_animation` census counts. **A consumer that decides "this is a cutscene" from the
authored codes therefore gives every mission in the install a letterbox and a suspended world.**
Ask by definition name.

Reading the eight as a pair of state transitions:

- `RESET_STATE` (applied at load) asserts the **gameplay** end state: hand control back and restore
  the chrome (1), restore the in-flight systems (10), reveal the AI vehicles (914), restore the
  camera-parameter gate and view mode (667).
- `callback_sequence` (runs when the definition plays) asserts the **movie** state: suspend the
  simulation (20), hide the chrome and detach the view target (2), take the player out of flight
  (11), and 14.

The def named the sequence `callback_sequence` itself, which is the author's own label for "the
beat that notifies the host".

### The C1/M04 worked example

M04's intro is spread over three readers, all listed by its `mis_anim.zrd`:

- `intro.zrd` holds `camera1-mission_intro_animation`: the callbacks above, a
  [`FogState`](../anim-definitions.md#fogstate--decoded-deliberately-not-acted-on) `drop_fog`, the
  pirate zeppelin's start pose, then `start_script`, which calls `scene1`, stops it at 15.9 s, calls
  `scene2`, stops it 12.4 s after that event, and branches on which airframe the player is flying
  (`check_balmoral` → `check_warhawk` → `drop_planes`).
- `scenes.zrd` holds the beats. Five of them call `letterbox`.
- `startanims.zrd` puts `mission_intro_animation` in `NEW_GAME_START`.

The zeppelin's two visible jumps are the cuts between those beats, and its disappearance is the last
beat deactivating it; the smooth phase between them is
`introanm/pzep1-piratezep.zan`, 48 frames at 1/3 s on a straight line.

## Reader rules and edge cases

- **Never read a callback's meaning from the def it sits in.** The authored `VALUE` is the whole
  message. The same integer means different things to different hosts, and one host answers only a
  subset of the integers authored against it.
- **A `CALLBACK` with no host is a no-op, in the original too.** Absence of an effect is not
  evidence the code is meaningless.
- **The bars are world geometry, not a UI overlay.** Opaque black quads on material 0, 7.5 m ahead
  of the camera, wider than the frame. Model 0 carries `facade_mode: CylindricalY` and
  `flags.lighting`/`flags.fog` both true; at 7.5 m the fog contribution is negligible, and the def
  pins the node's full orientation to `camera1` every tick regardless of the facade mode, so a
  builder should copy the transform and not rely on billboarding to keep the bars square.
- **`AT_NODE` on a pose event is spelled differently in each front-end.** The reader writes
  `OBJECT_TRANSLATE_STATE … AT_NODE [camera1]` and `OBJECT_ROTATE_STATE … AT_NODE_MATRIX
  [camera1, 0, 0, 0]`; the compiled twin writes a flat `at_node: "camera1"` on the translation and a
  nested `basis: { AtNodeMatrix: "camera1" }` on the rotation, with `state` carrying the offset
  inside that frame (zero for the bars) rather than an absolute pose. **The compiled def wins**, so
  a consumer that reads only the reader spelling sees the target teleported to its parent's origin.
  Install-wide there are 190 non-null `at_node` translations and 8 `AtNodeMatrix` rotations (the
  eight chapters' letterbox); `INPUT_NODE` appears as an `at_node` value and is a sentinel, not a
  node name.
- **A change to `generic_intro` is verified by what disappears, not by what looks right.** Twelve
  missions share it, and an 8-chapter freecam regression cannot see any of it: no Instant Action or
  multiplayer mission bootstraps an intro, so that regression is inert here by construction
  (`docs/verification.md` DIAG-10). Check per mission.
- **The letterbox node is a library root.** Parentless, no spatial-partition reference, and the def
  never reparents it, so a renderer that only walks `world1` will never reach it. See
  [`world-structure.md`](../world-structure.md) for the library-root test.
- **Code 20 stops the world, not just the camera.** Vehicles, AI and the objectives runtime all
  early-out while the active-cutscene slot is set. A player that only moves the camera will let the
  mission run underneath the movie.
- ⚠ **The story-mission intro defs must play.** `generic_intro` (12 missions) and
  `mission_intro_animation` (C1/M04) are the missions' authored intro movies and are working assets;
  nothing on this page is a reason to suppress one, and skipping them at bootstrap has been ruled
  out. C1/M04's pirate zeppelin flying above the overcast is an accepted artifact of the intro
  playing without a player, not a defect to work around.

## Evidence and limits

- Geometry, material and node flags read from `extracted/<Cx>/gamez/{nodes,models,materials}.json`
  for all eight chapters; the `letterbox` subtree is identical in each.
- Reader values read from `extracted/zrdr/{letterbox,generic_intro}.zrd.json`,
  `extracted/<Cx>/zrdr/landings.zrd.json` and `extracted/C1/M04/zrdr/{intro,scenes,mis_anim,
  startanims}.zrd.json`. Counts are over every `*.zrd.json` in the extraction.
- Exe claims name the function they came from. The registration census is complete: `FUN_004ee160`
  is the only writer of the host pointer at `anim+0x74` on an animation instance, and its 13 call
  sites were each read.
- **Undecoded: `RESET_TIME`.** The letterbox def's `RESET_TIME [0]` is the scheduling field the
  landing page also marks undecoded, so *when* a running letterbox returns to its `INACTIVE` base
  state inside a mission is not established here. Nothing stops the def by name, so a consumer has
  to retract the bars itself at the handoff (`CutsceneController`, `docs/architecture.md`).
- **Undecoded: what field of view a cutscene is framed at.** The bars are a fixed card 7.5 m ahead
  of the eye, 13.2924 by 8.0948, so the frame they letterbox is 56.7° vertical at 4:3 and the card
  overhangs it horizontally. `camera1`'s own gamez `Camera` record carries `fov_h_base` /
  `fov_v_base` of 0 (runtime-filled), and the `CAMERA_STATE` events in the intro readers set only
  `NEAR_CLIP` and `LOD_MULTIPLIER`, so the number itself is not in the data.
- **Undecoded: what `camera1-player_setup` is for.** It carries the whole cutscene vocabulary and
  every Instant Action mission starts it; nothing establishes what the original shows while it runs.
- **Undecoded: how the original draws a parentless active root.** `gwNodeSetActive`
  (`FUN_004cca30`) only flips the node's active bit; the traversal that reaches `letterbox` without
  it being anyone's child was not traced.
- **Named gaps:** callback codes 14 and 123 are authored but reach no case in the mission-script
  host, and no other installable host takes them.
