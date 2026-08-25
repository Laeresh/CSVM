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
- [The reparent is how a cutscene is composed](#the-reparent-is-how-a-cutscene-is-composed)
- [`player`, and the two pointer spaces a definition addresses](#player-and-the-two-pointer-spaces-a-definition-addresses)
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

## The reparent is how a cutscene is composed

A cutscene's camera keyframes are written in the frame of the node the shot is about, and
`OBJECT_ADD_CHILD` / `OBJECT_DELETE_CHILD` are what put the camera in that frame. Both take a
`parent` and a `child` name; an add moves the child under the parent and a delete detaches it back
to the world root, and **neither preserves the child's world pose** — the local transform is the
whole point.

`camera1-generic_intro`'s `start_script` opens with `OBJECT_DELETE_CHILD [world1, camera1]`,
the same for `player`, then `OBJECT_ADD_CHILD [piratezep, camera1]` and `[piratezep, player]`;
its `RESET_STATE` deletes all three of `camera1`, `player` and `piratefighter` from `piratezep`
and re-adds only `player` to `world1`, which is consistent with a delete meaning "detach", since
`camera1` and `piratefighter` are parentless roots in the gamez to begin with. C3/M01's
`player-texdrop` does the same around a per-shot aiming node: it adds `player` under
`do_direction`, runs an SI script on it, deletes it again, and its third sequence adds `camera1`
under `do_direction` for the shot and deletes it after.

The offsets only make sense read this way. `gi_cam1`'s first keyframe is
`(-155.7, -62.0, -768.9)` and `piratezep` sits at `(-1401, 500, -1412)`: a y of −62 is an offset
inside a node at y = 500, and in world space it is 62 m under the sea.

⚠ The reparent is authored as a live sequence event. A `RESET_STATE` walk carries the undo, and a
consumer that applies it during a bootstrap pass moves shipped nodes off a parent no definition
has changed yet.

## `player`, and the two pointer spaces a definition addresses

### A cross-archive `ptr` is the aircraft archive's index plus a per-chapter base

A compiled definition's symbol table gives every name a `ptr`, and those pointers do not all index
the same table. A chapter node's `ptr` is its position in that chapter's own `nodes.json`:
`world1` 0, `camera1` 3, `letterbox` 6, and C3's `piratezep` 1020. A node from the shared aircraft
archive (`planes/nodes.json`, 3317 nodes) is its position there plus a base, and the base is that
chapter's own node count rounded up to the next multiple of 2500:

| chapter | `nodes.json` | base | `player` | `piratefighter` |
|---|---|---|---|---|
| C1 | 7064 | 7500 | 8918 | 9824 |
| C1B | 5603 | 7500 | 8918 | 9824 |
| C1C | 5644 | 7500 | 8918 | 9824 |
| C2 | 4956 | 5000 | 6418 | 7324 |
| C2B | 4901 | 5000 | 6418 | 7324 |
| C3 | 5408 | 7500 | 8918 | 9824 |
| C4 | 8289 | 10000 | 11418 | 12324 |
| C5 | 11438 | 12500 | 13918 | 14824 |

Eight chapters, one rule, no exceptions. Within C3 the same base resolves nine cross-archive names:

| name in `camera1-generic_intro` and its called defs | `ptr` | `planes/nodes.json` |
|---|---|---|
| `cockpit1` | 7549 | 49 |
| `player_balmoral` | 7525 | 25 |
| `healthy` | 7649 | 149 |
| `shadow` | 7701 | 201 |
| `destroyed` | 7702 | 202 |
| `player_warhawk` | 8135 | 635 |
| `player` | 8918 | 1418 |
| `piratefighter` | 9824 | 2324 |
| `staticprop1` | 9963 | 2463 |

The base always clears the chapter's own table, so the two spaces cannot overlap. Reading a
cross-archive `ptr` against the chapter's table is what makes `player` look like a name with no
node behind it, and it is why the same name carries a different number in every chapter while the
aircraft archive it points into is one shared file.

### `player` is the player's own aircraft

`player` (aircraft-archive node 1418) is a parentless `Object3d` whose single child is
`player_pfighter`. The airframe name table at `0x00620cc0` in `crimson.exe` carries seven name
pointers per airframe (display name, player-model node, `p<name>`, `r<name>`, `w<name>`, remote
node, def name), and `player_pfighter` is the **Devastator's** player-model node: that row reads
`Devastator`, `player_pfighter`, `pdevastator`, `rdevastator`, `wingman`, `piratefighter`,
`devastator`, the only row whose fifth and sixth fields break the `w<name>` / `<name>` pattern.
Every other airframe's player model is a parentless root of its own (`player_bhawk`,
`player_fury`); `player_pfighter` is the one that ships inside a wrapper.

The name is what resolves, not the pointer. `FUN_004d0280(7, "player")` walks node table 7
comparing strings, and `FUN_0042e5e0` calls it to switch the node off around a render pass and
back on after. The mission setup path builds the player's own vehicle record and passes that same
literal with it: `FUN_004136e0` ends with `FUN_00414f40("player", <record>)`, once per arm of a
two-way choice over the loadout, and that function packs the record and calls
`FUN_0041a320(<record>, "player")`.

The intro's own use of the name agrees from three further directions. `callback_sequence` opens
with `OBJECT_ACTIVE_STATE [player, false]` beside code 11, which is what takes the player out of
flight, and `RESET_STATE` closes with `[player, true]` beside codes 1 and 10, the handoff and the
systems restore. `start_script` moves `player` out of `world1` and under `piratezep`, and the undo
puts it back under `world1`, which is where the flown aircraft lives. And codes 965, 966 and 967
swap the player onto a named airframe, which only means anything if the node they swap is the
player's.

⚠ So the shipped `player` node wraps a Devastator because that is what sat in the slot when the
aircraft archive was built, not because the intro is about a Devastator. What airframe the intro
actually shows is the one the player flies, which is also why C1/M04's intro branches on
`check_balmoral` / `check_warhawk`.

### What a `generic_intro` stages

Two aircraft, both from the aircraft archive rather than the chapter's gamez.

- **`piratefighter`.** `gi_pfighter1` activates it, sets `healthy` on and `destroyed` / `shadow` /
  `staticprop1` off, calls `wing_lights_blink` and `spinprops` on it, adds it under `piratezep`,
  and runs its own SI script. `gi_pfighter2` repeats that for the later shot.
- **`player`.** `gi_1stperson` activates it, sets `healthy` on and `cockpit1` off, and runs
  `gi_player1`; `gi_playerdrop` runs `gi_player2` over the launch, with `snd_droplaunch` and a
  `wing_lights_blink` addressed to `player_pfighter` **by name**, the one place the authored data
  reaches past the slot to the Devastator's model directly.

⚠ CSVM stages neither. The world build puts no aircraft-archive node into the animation runtime's
node table, so both names claim a symbol with a null binding and every event that poses them drops
(`BL-482`). The camera move plays over an empty stage; what CSVM does instead of
`OBJECT_ACTIVE_STATE [player, false]` is callback 11's own out-of-flight state, which holds the
flown airframe undrawn for the whole cutscene rather than posing it.

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

#### The condition object is an authored triangle

The loader `FUN_0045d8f0` reads the file and hands each record to the parser `FUN_0045da80`, which
resolves the row into a nine-field record and drops the row outright when a field fails to resolve.
The keys it reads are exactly `node`, `anim`, `angle`, `speed` and `auto`.

| record field | source | meaning |
|---|---|---|
| `[0]` | `node [name]` | the approach node, whose world pose the attitude test measures against |
| `[1]` | its `land_on` descendant | the arming gate: the row is dead while that node's active bit (bit 2 of `+0x24`, `gwNodeSetActive`) is clear |
| `[2]` | its `cone`, `half_cone` or `sphere` descendant | the shape node, whose world pose the volume is expressed in |
| `[3]` | built from `[2]`'s model | the condition object, one of three classes with a `Contains` virtual at vtable slot 0 |
| `[4]` | `angle [deg]` | `(deg · π/180 ÷ 2)²`; `FLT_MAX` when unauthored, which skips the attitude test |
| `[5]`, `[6]` | `speed [min, max]` | mph × `0.44704`, so metres per second; `∓FLT_MAX` when unauthored |
| `[7]` | `auto` | the auto-land flag |
| `[8]` | `anim [name]` | the animation definition, resolved by `FUN_00523820` |

The shape node is **deactivated at parse time** (`FUN_004cca30(node, 0)`), so the marker never
renders in a mission; CSVM never builds it either, because a lone untextured triangle is what
`GameZ.IsMarkerGizmo` culls.

**The volume is that node's single authored triangle.** The parser requires the shape node's model
to carry exactly one polygon of exactly three vertices and reads the three as:

- **`cone`** (`PTR_FUN_00607b18`, 0x2c bytes): apex `v0`, base centre `v1`, axis `normalize(v1 − v0)`,
  base radius `|v2 − v1|`. `FUN_0045d5b0` tests a point `p` by `t = dot(axis, p − v1) / dot(axis, v0 − v1)`,
  requiring `0 ≤ t ≤ 1` and `|(p − v1) − (v0 − v1)·t| ≤ (1 − t)·R`. `t` runs 0 at the base disc to 1 at
  the apex, so the radius closes to nothing exactly at the target.
- **`half_cone`** (`PTR_FUN_00607b24`, 0x38 bytes): the same cone plus the half-space
  `dot(p − v1, normalize(perp)) ≥ 0`, where `perp` is the triangle's own component perpendicular to
  the axis. `FUN_0045d730` applies the half-space first.
- **`sphere`** (`PTR_FUN_00607b0c`, 0x14 bytes): centre `v0`, radius `|v1 − v0|`. `FUN_0045cf60`
  tests `|p − centre| < r` with the centre put through the shape node's world transform.

All 34 shipped rows across the eight chapters resolve: one node match each, one shape child each,
one `land_on` each. Six of C3's are `cone` (the `do_approachN` drop ring, 250.79 m deep and 271.76 m
at the base, a 47.3° half-angle, all six sharing model 490 at one point with 60° of yaw between
them), the two `hooked_to_klondike` rows are a 96 × 32 m `half_cone` and a 500 m `sphere`, and every
other chapter's rows are `half_cone` except its own auto row.

#### The per-frame test

`FUN_0045df60` runs one record and gates in this order:

1. the player's `+0x91d` cutscene flag is clear, and the landings slot `DAT_0071b1dc` is empty;
2. the record's `land_on` node is absent or active;
3. `[5] ≤ player+0x934 ≤ [6]`, the speed band, inclusive;
4. the attitude: `FUN_0053f610` turns the approach node's Euler rotation into a quaternion,
   `FUN_0053f9b0` multiplies it against the player's own at `player+0x150`, and `FUN_0053fca0` takes
   that relative quaternion's log map, whose magnitude is exactly half the rotation angle. Comparing
   its square against `[4]` is therefore the exact test **"the player's whole orientation is within
   `angle` degrees of the approach node's"**, roll included, not just heading;
5. the condition object's `Contains`, with the player's position in the shape node's live world
   frame.

A row that passes and carries `auto` sets `DAT_00719109` instead of starting anything; the next
frame `FUN_0045e120` turns that into the on-screen auto-land prompt (message `0xb5`, or `0xb6` when
the binding is a pad button, over key binding `0x6a`). Every other row starts its animation with
`FUN_004edda0` and registers the mission-script host on it with `FUN_004ee160`.

#### Arming is the mission script's job

The gate node is what a mission opens and closes. C3/M01 lists `disable_dropoff` in
`NEW_GAME_START`, which sets all six `do_approachN/land_on` `INACTIVE`, so the drop ring is dead
from mission load. `OBJECTIVE21`'s `WAKE_ANIM [enable_dropoff]` sets them `ACTIVE`, and
`OBJECTIVE22`'s `WAKE_ANIM [disable_dropoff]` closes them again once the drop has played. The
klondike hookup is armed the same way, by `OBJECTIVE14`'s `WAKE_ANIM [pzhomebase]`. **The approach
table is the mechanism; the objective script decides when each row is live.**

#### Limits and readings

- **Undecoded: whether the host reaches a called definition.** `FUN_004ee160` writes the host on the
  animation instance the trigger starts, and it is the only writer install-wide. The `do_approachN`
  definitions author no `CALLBACK` at all: they set `ObjectRotateState do_direction` and then
  `CallAnimation texdrop`, and it is `player-texdrop` that raises 11, 951, 2 and 1 plus the
  `letterbox` call. No propagation of `anim+0x74` from a caller to a callee was found. CSVM takes
  the **reading** that the host answers for a started row's definition *and* its `CALL_ANIMATION`
  closure, because the drop is a letterboxed movie that has to hide the chrome and take the player
  out of flight, and no other mechanism does that.
- **Undecoded: when the landings slot clears.** `DAT_0071b1dc` holds the running instance and is
  cleared only by `SceneAnimCallback_0045e0f0` seeing callback **0**, which nothing authors. Read
  literally, one landings cutscene per mission load would lock out the rest, which C3/M01 (drop,
  then hookup) contradicts. CSVM instead re-arms as soon as the started definition ends, which is
  what the mission needs.
- **A reading: the volume's frame is static.** The original re-reads the shape node's world pose
  every frame. Nothing in the install animates a `cone`/`half_cone`/`sphere` marker or its approach
  node, so CSVM folds the shape's local chain into the approach node once at load and follows one
  node.
- **A divergence: CSVM arms the table only in a story mission.** The original's mission load
  (`FUN_00464680`) arms it for every mission type, and the anim-not-found rejection is what keeps
  most Instant Action missions out. It does not keep all of them out: C3/IA1 carries
  `hooked_to_klondike` and C3 ships `pz_manual_land/land_on` active, so an Instant Action sortie
  there would take a docking cutscene. Whether the original means that is undecoded, so CSVM scopes
  by session (`WorldSession.Options.LandingTriggers`).

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
| 965, 966, 967 | 1, 1, 3 | swap the player onto a specific airframe (`pbloodhawk`/`player_bhawk`, `pwarhawk`/`player_warhawk`, `pbalmoral`/`player_balmoral`) with its armour and hardpoint table, and set the cutscene flags. The data-side counterpart of the intro defs' `check_balmoral`/`check_warhawk` branches. Decoded in full [below](#the-airframe-swap-codes-965-966-and-967). |
| 968 | 1 | tests `bswingman_1`. |

**The two gaps, named.** Codes **14** (6 occurrences, four of them in the intro defs) and **123**
(8 occurrences) reach no case in `FUN_0047e080`; its switch covers 0, 1, 2, 3, 10, 11, 12, 13 and
20 below `0x15`, and 4–9 and 14–19 fall through. No other host that can be installed on a cutscene
animation takes them either, since the only host the cutscene trigger installs is this one. They
are recorded as gaps, not guessed at: nothing in the exe tells us what 14 or 123 were meant to do.

### The airframe swap codes 965, 966 and 967

The three codes are the only mechanism in the shipped data that changes what the player is flying
without ending the mission. Their five occurrences are `C1/M02`'s `hangar_3-hangar_drop` (965),
`C4/M04`'s `player-bm_unhook_player` (966) and the three `britbalmoral_<n>-ww_balmoral<n>` capture
definitions in `C3/M05` (967), where the code is the last event of the sequence that flies the wing
walk.

Each case does the same five things, in this order.

1. **Measure the outgoing airframe.** `FUN_0047bcd0(name, &armour, &structure)` reads one hull
   section's CURRENT pair off the vehicle's section array (at `veh+0x9c`, stride `0x58`, name at
   `+4`, armour max/current at `+0x24`/`+0x28` and structure max/current at `+0x2c`/`+0x30`),
   answering −1/−1 for a section the airframe has none of. The four sections are the body, the tail
   and the two wings, and the two sums are kept for step 5. 965 skips this step.
2. **Rebuild the vehicle.** `FUN_0047fd50(<def>, <planes.zbd node>)` returns immediately if the
   player is already in that def. Otherwise it saves the attitude quaternion (`+0x54`), the velocity
   (`+0x81`) and two further fields, tears the old vehicle body down (`FUN_0047bab0`), builds the
   new one from the plane record (`FUN_0047c210`), **walks the global target list and re-points
   every `TargetVehicle` that pointed at the player onto the new object** (both the `+0x948` and the
   `+0x2fc` slot), re-registers the collision and landing sound handles, restores the saved motion
   state and re-applies the camera-parameter profile. It clears `+0x91d`/`+0x91e`/`+0x91f` on the
   way through, which is why step 4 re-asserts them.
3. **Write the new airframe's tables.** The twelve ints at `DAT_0062ae28` are the player's
   ammunition table, four gun-group counts then eight hardpoint counts, `−1` for a slot the airframe
   has none of. The Bloodhawk takes 40/30 and two hardpoints of six, the Warhawk 70/50 and six
   hardpoints of six, the Balmoral 50/50/30/30 and eight of six; the four bytes at `DAT_0062ae58`
   ride the four gun slots. `FUN_004b24d0` pushes the table onto the player and `FUN_004b2350`
   re-picks the selected gun and the selected hardpoint as the first slot in each half with a
   positive count, so the readouts follow. `FUN_0047bd90(name, armour, −1)` then sets each of the
   four hull sections to that airframe's own armour, max and current together: 20 across for the
   Bloodhawk, 30 across for the Warhawk, 40/35/25/25 for the Balmoral, which is the same row CSVM's
   own stat table carries for `player_balmoral`. Last, `FUN_00449140(<airframe id>)` reads a scalar
   off the stat row (id 0xb, 0x23, 0x29) into `player+0x66c`.
4. **Set the cutscene flags.** `player+0x91d = 1` and `player+0x91e = 1`, then `FUN_0042e5a0()`.
   Those are exactly the state **code 11** and **code 2** assert between them: the player out of
   flight, and the chrome and view target off. Nothing in the case ends that state; the definition
   ending does, through the ordinary handoff. 965 additionally sets `player+0x946`, the nitrous
   injector bit.
5. **Hand the outgoing airframe to `wingman_4`** (966 and 967 only). `DAT_0071c4f0` is resolved once
   at mission start by `FUN_004735b0`, which looks `wingman_4` up in the live vehicle list in
   `c3`/`m05` and `c4`/`m04` and holds 0 everywhere else. The same function gives a record named
   `wingman_4` **the player's own aircraft type and livery** in those two missions. The case places
   it 100 m from the player at 45° off the nose, gives it the section sums measured in step 1 and
   reveals it (`FUN_004b0f40`). 967 also hides the aircraft the capture animation belongs to, and
   scales the new airframe's four sections by that aircraft's own armour and structure fractions,
   so the captured plane's damage carries onto the one the player is now flying.

⚠ **CSVM implements steps 2, 3 and 4 and not steps 1 and 5.** The rig is rebuilt through the flight
roster's own assembler on the named airframe, at the pose, heading, throttle and speed the outgoing
aircraft held, with that airframe's stock fit at full ammunition and its own armour pools; the
cutscene flags land on the aircraft the swap built. The `wingman_4` handover and the damage
carry-over are per-mission exe behaviour keyed on the chapter and mission strings, and nothing in
the data asks for them.

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
  [`FogState`](../anim-definitions.md#fogstate--an-inline-fog-written-over-the-zone) `drop_fog`, the
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
- The `player` reading above rests on the aircraft archive's own `nodes.json` (the base rule holds
  for all eight chapters, and within C3 for all nine cross-archive pointers the intro's symbol
  tables carry), on the airframe name table at `0x00620cc0`, and on three exe sites: `FUN_004d0280`
  (name lookup over node table 7), `FUN_0042e5e0` (its caller, hiding the node for a render pass)
  and `FUN_004136e0` (mission setup, passing the literal with the loadout record). **What
  `FUN_0041a320` does with the pair was not read**, so "the record is created under that name" is
  the shape of the call, not a traced construction.
- **Undecoded: what computes the base.** The rounding rule is read off the eight chapters' own
  numbers; no exe site that computes it was traced, and no chapter's node count lands exactly on a
  multiple of 2500, so whether the rounding is strict or inclusive is undetermined. Resolve these
  names by name rather than by arithmetic on the pointer.
- **Undecoded: what `camera1-player_setup` is for.** It carries the whole cutscene vocabulary and
  every Instant Action mission starts it; nothing establishes what the original shows while it runs.
- **Undecoded: how the original draws a parentless active root.** `gwNodeSetActive`
  (`FUN_004cca30`) only flips the node's active bit; the traversal that reaches `letterbox` without
  it being anyone's child was not traced.
- **Named gaps:** callback codes 14 and 123 are authored but reach no case in the mission-script
  host, and no other installable host takes them.
