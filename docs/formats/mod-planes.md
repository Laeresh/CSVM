# Plane mods

How CSVM reads a plane mod: a new airframe a modder adds beside the eleven stock ones, as a model
and a small definition that inherits a stock plane's stats. This is a CSVM format, not a decode of
the original; it is the reference a mod author works from, and [mods/plane-mods.md](../mods/plane-mods.md) is the
walk-through for preparing a model. A [model pack](model-packs.md) changes how an existing airframe
looks; a mod adds an airframe the game did not have.

## Contents

- [Conceptual model](#conceptual-model)
- [Reference](#reference): [folder and id](#folder-and-id), [`plane.json`](#planejson),
  [overrides](#overrides), [default loadout](#default-loadout), [`plane.glb`](#planeglb),
  [paint](#paint), [damage](#damage), [content hash](#content-hash)
- [Reader rules and edge cases](#reader-rules-and-edge-cases): [a skipped mod](#a-skipped-mod),
  [flying a mod](#flying-a-mod), [the hangar](#the-hangar), [the campaign](#the-campaign), [menu art](#menu-art),
  [bot seats](#bot-seats), [multiplayer](#multiplayer)
- [Evidence & limits](#evidence--limits)

## Conceptual model

A mod is one folder or zip holding a model, `plane.glb`, and a definition, `plane.json`. The
definition names a stock parent (`kind_of`), and the mod plane flies with the parent's def, changed
only where `overrides` gives a literal value. Borrowing another stock plane's value means copying
its number; there is no reference to another def. The mod's id is its folder or zip name, and every
machine in a network match needs the same mods, which the content hash compares.

## Reference

### Folder and id

| Item | Rule |
|---|---|
| Folder | `Mods/Planes` under the data root, or `Planes` under the folder `--mods=<folder>` names (`docs/cli.md`). Each subfolder and each `.zip` directly inside it is one mod; loose files at its top are ignored. Mods load in name order. A `--det` or `--run-tests` run reads no mods unless `--mods=` names a folder, so no golden or suite depends on what one machine dropped in. |
| Id | The folder's name, or the zip's name without `.zip`, as spelt on disk. Ids match without case. An id that is a stock airframe's `planes.zbd` node (`player_avenger` is the Hellhound's) or an earlier mod's id skips the mod, and so does one longer than 31 bytes of UTF-8, the network roster's plane field ([Multiplayer](#multiplayer)). |
| Files | `plane.json` and `plane.glb` at the mod's top, both required. Other files (a portrait) may sit anywhere below it. Paths inside a mod match without case, with `/` or `\`. |
| Zip | `plane.json` at the zip's root, or under one top folder (`avenger/plane.json`), marks the mod's top; entries outside it are not part of the mod. `plane.json` under two top folders and none at the root skips the mod. |
| Hidden files | A file or folder whose name starts with `.` (`.DS_Store`, `.git`) is not part of the mod. |

### `plane.json`

```json
{
  "_note": "keys starting with _ are prose",
  "name": "E-1C Avenger",
  "kind_of": "player_brigand",
  "unlock": "with_parent",
  "cockpit": "player_warhawk",
  "sound": "player_fury",
  "portrait": "portrait.png",
  "overrides": { "fd_speed": 120.0, "turrets": [] },
  "tint": { "mask": "avenger_mask.png", "detail": "avenger_detail.png", "materials": ["avenger"] },
  "hangar_capacity": 12500,
  "loadout": {
    "guns": [ { "calibre": 60 }, { "calibre": 30 }, { "calibre": 30 }, { "calibre": 30, "twin": true } ],
    "hardpoints_per_wing": 2
  }
}
```

| Key | Rule |
|---|---|
| `kind_of` | Required. The stock parent's `planes.zbd` node, one of `player_autogyro`, `player_avenger`, `player_balmoral`, `player_bhawk`, `player_brigand`, `player_pfighter`, `player_fbrand`, `player_fury`, `player_kestrel`, `player_peacemaker`, `player_warhawk`, matched without case. |
| `name` | The name a menu shows. Absent, the id. |
| `overrides` | An object of def field name to number, each a finite JSON number, applied over the parent's def (`vehicle.json`, [vehicle.md](vehicle.md)); the names are in [Overrides](#overrides). `"turrets": []` clears the parent's turret mounts; any other `turrets` value fails. |
| `unlock` | When the plane enters the campaign hangar: `start`, `with_parent` (the default), or a mission or chapter id, `c1b/m02` or `c1b` (the world folders of `cm_sequence`, [campaign-sequence.md](campaign-sequence.md)), matched without case. The rules are in [The campaign](#the-campaign). |
| `cockpit` | A stock node whose cockpit interior the plane uses when its model has no `cockpit1` ([`plane.glb`](#planeglb), Cockpit). Absent, the parent's. |
| `sound` | A stock node whose engine sounds the plane uses: the engine loop, the cockpit-view loop, the damaged loop with its pitch range and the dive whine its def names, in the original and the Enhanced sound mode alike. Absent, the parent's. |
| `portrait` | The path of an image inside the mod (`.png`, `.jpg`, `.webp`, `.tga` or `.bmp`) that menus show as the plane's portrait ([Menu art](#menu-art)). Absent, a render of the model. |
| `tint` | How a livery colours the plane's own surfaces: `mask` and `detail`, each the path of a `.png` inside the mod, and `materials`, a non-empty list of `plane.glb` material names. All three are required; a `_` key inside is prose. Absent, the plane's own surfaces keep their paint under every livery. The rules are in [Paint](#paint). |
| `hangar_capacity` | The hangar's weight capacity in lb, a whole number from 1 to 100000, replacing the parent's stat-table capacity in the overweight check ([The hangar](#the-hangar)). Absent, the parent's. |
| `loadout` | The plane's default fit, replacing the parent's stock fit: see [Default loadout](#default-loadout). Absent, the parent's. |

Every value is a literal. A key starting with `_` is prose and ignored; any other unknown key, a
wrong type, an empty string or a name outside the stock list skips the mod.

### Overrides

The mod plane's stats are its parent's, loaded as the parent's are, then each override sets one
field to its literal value in the units `vehicle.json` authors. Every name is spelt as in
`vehicle.json`, matched with case.

| Name | Where it lives in the def |
|---|---|
| `pitch_torque`, `roll_torque`, `rudder_torque`, `return_rate`, `ang_momentum_damp`, `fd_speed`, `drag_factor`, `veh_weight`, `ref_area` | the `dynamics` block |
| `flight_ceiling`, `fuel` | the def itself |
| `<zone>_hp`, `<zone>_armor`, zone one of `nose`, `tail`, `leftwing`, `rightwing` | that zone's hit points or armor in `destroyable_parts` |

A name outside this list (a typo, `rec_moments_inertia`, which is a triple, `engine`, which picks
an engines.json row) is logged as `mod plane <id>: override <name> is no def field CSVM reads,
ignored` and changes nothing; the mod still loads. The stock engine row stays the parent's, so an
`fd_speed` copied from another plane still scales by the parent's engine power.

An AI aircraft flying the mod plane (`--ai=<id>`) takes the parent's AI def: its damage model, armament, skills and
turrets come from that def's chain as for the parent, and the `dynamics` and def-level overrides
apply as on a flown plane. The zone overrides have nothing to act on there, since an AI load
carries no zones. The plane keeps the parent's def name, which is what its stock fit is found by.

`"turrets": []` clears the turret mounts on the flown and the bot load alike, and makes each of
the parent's turret gun slots a forward gun slot firing from the firepoints the slot names, with
its calibre, ammunition and barrel count unchanged. A Brigand-based mod's W4 Rear Turret becomes a
forward 30 calibre gun on `firepoint1` and `firepoint2`. The stock planes' fits are unchanged.

### Default loadout

`loadout` replaces the guns, the pylons or both of the parent's stock fit (after `"turrets": []`
has turned its turret slots forward). The result is the fit a flown mod plane carries with no
`--loadout=` and the fit the hangar's default configuration loads.

| Key | Rule |
|---|---|
| `guns` | Exactly four entries, gun slots W1 to W4 in order. `null` leaves the slot empty. An object holds `calibre`, one of 30, 40, 50, 60, 70 (.30 Zephyr, .40 Carver, .50 Barret, .60 Cheyenne, .70 Goliath, [hangar.md](../org/hangar.md)), and optionally `twin`, `true` (the default) for a gun on both of the slot's firepoints and `false` for one gun on the lower-numbered one. Slot n's firepoints are `firepoint(9-2n)` and `firepoint(10-2n)`, so W1 is `firepoint7`/`firepoint8` and W4 `firepoint1`/`firepoint2`. Absent, the parent's guns. |
| `hardpoints_per_wing` | Pylons hung on each wing, 0 to 4, outboard first, each carrying the high explosive a hangar build hangs. Absent, the parent's pylons. |

A slot keeps the parent's mount name and turret flag, fires slug and leaves its ammunition to Ammo
Selection, as a hangar build does (`CustomPlaneBuild.LoadoutFor`). The Avenger's default, 2 x .60
and 6 x .30, is `[{"calibre": 60}, {"calibre": 30}, {"calibre": 30}, {"calibre": 30}]` over the
Brigand with `"turrets": []`: W3, empty on the Brigand, gets a .30 pair on `firepoint3`/`firepoint4`.
A key starting with `_` is prose; any other key, a slot count other than four, a calibre outside the
five, a non-boolean `twin` or a pylon count outside 0 to 4 skips the mod.

### `plane.glb`

The model is the airframe itself: its node tree becomes the plane's nodes, and every system that
reads a stock airframe's nodes reads the mod's under the same names. The surest start is a stock
export (`--export-gltf=`, [model-packs.md](model-packs.md)): an unedited Brigand export flies as a
Brigand does.

| Item | Rule |
|---|---|
| Frame | Metres, Y up, nose toward -Z, right +X, as glTF and a stock export write it ([model-packs.md](model-packs.md), "What a file must hold"). |
| Tree | Each glTF node is one airframe node, at its own transform, under a root named the mod's id. One scene root named the id or a stock node (an export's `player_brigand`) stands for that root and its own transform is dropped; any other scene roots hang under it. Blender's repeat suffix (`wing.001`) is dropped from a name. |
| Names | The stock names, matched without case. `geometry > healthy > nearest > {nose, tail, leftwing, rightwing}` holds the body. A control surface is `l_aileron1`, `r_elevator1`, `l_rudder1` and the rest under its hinge group (`lailer1`, `lft_elev`, `lrudder1`), whose origin and rotation set the hinge line; the surface turns about its own X (aileron, elevator) or Y (rudder). `dontmove > {staticpropN, propN, propNb}` are the props: the still `staticpropN` shows stopped and the `propN`/`propNb` blur discs spin. `pilot_pos > pilot > body > head` is the pilot. |
| Markers | Empty nodes: `firepointN` and `pylonN` (the parent's fit binds by these names, so the mod carries every marker that fit names), `cockpit_camera` (the first-person eye), `exhaust1`/`exhaust2`, `ground_level` and `target`. Positions are read in the plane frame below the root. |
| Meshes | Every mesh draws as a model pack's root file draws its nodes, its materials by the pack's rules ([model-packs.md](model-packs.md), "Materials"): a material named after an original texture wears that texture, and takes the livery when it is a skin of the airframe whose interior the mod carries (the painter reads the skin prefix there). Any other material wears its own base colour and texture, which the livery reaches only as [Paint](#paint) describes. Its triangles are also the gameplay geometry: the collision hulls, the hit body, the ground shadow and every box the game measures. |
| Cockpit | A `cockpit1` node directly under the root is the plane's own interior. Its subtree is in interior units as a stock `cockpit1.glb` is (about 25 units per metre, the eye at `cockpit1`'s origin looking down -Z), drawn at 0.04 at the mod's `cockpit_camera`; `cockpit1`'s own transform is not read. The gauges, lamps and readouts are found by the stock node names (`gauges`, `speed`, `pfhorizon`, `lowalt_on`) and the belts and damage zones by the stock texture names, so a stock `cockpit1.glb` export put under the root works unedited. Without one the plane takes a copy of the interior of `plane.json`'s `cockpit`, else its parent's, mounted the same way and drawn from the archive only: no node of the file reaches it. |
| Not read | Animation, skins, morph targets, cameras, lights and node visibility. A model pack's replacement files never reach a mod's meshes; its `planes/<id>/rig.json` does. |

A file that does not import as glTF skips the mod when the game data binds, with the reason in the
log and the skipped list. A missing name is not checked at load: a marker the parent's fit names and
the model lacks leaves the plane unarmed with a `loadout bind failed` warning, a missing
`cockpit_camera` puts the eye at the plane's origin, and a node the stock systems look for and do not
find (a surface, a prop) does not move.

### Paint

A livery (a pattern, a militia's scheme, or `--paint-color=`) is three colours and three decal
indices ([paint.md](paint.md)). A stock airframe takes the colours through the pattern's own region
masks, which follow the original skins' UVs; a mod's own UVs follow no stock skin, so the mod ships
its region mask itself and any pattern's colours reach it.

| Item | Rule |
|---|---|
| Mask | `tint.mask`, a PNG in the UVs of the tinted materials' base-colour texture. Red is the scheme's colour 1, green colour 2, blue colour 3, each channel the share of that colour at the texel. A texel with no channel set shows the base texture. |
| Slots | Colour 1 is the body colour, colour 2 the dark trim and colour 3 the light trim, the order `paint_color1..3` carry ([paint.md](paint.md), "Slot order, confirmed"). Mask the main body in red, the large secondary areas (stripes, wing tips) in green and small accents (spinners, rings) in blue: Fortune Hunters then paints red, black and white, Hughes yellow, black and white, Blackhat three browns. |
| Detail | `tint.detail`, a greyscale PNG (its first channel is read) that shades the colour, so panel lines and grime survive a repaint. Grey 128 shows the colour unchanged, darker darkens it and lighter brightens it up to white. |
| Composite | Per texel, `base * (1 - cover) + detail / 128 * (r * colour1 + g * colour2 + b * colour3)`, with `r`, `g`, `b` the mask's channels from 0 to 1 and `cover` their sum. A sum above 1 is shared out between the channels in proportion and hides the base. Alpha stays the base texture's. Mask and detail are sampled at the base texture's size, nearest texel, so a smaller mask works at coarser edges. |
| Materials | Only the materials `tint.materials` names, matched without case, take the tint, and only where they carry a base-colour texture. Leave glass, blur discs and parts with their own UVs out of the list. |
| No livery | `--paint=none`, or a mod without `tint`, shows the base texture everywhere. |
| Decals | A material whose name ends in `_noselogo`, `_taillogo` or `_winglogo` (`avenger_noselogo`) takes the scheme's nose, tail or wing decal over its whole UV square, as an original placeholder does. Give it `alphaMode` `MASK` so the decal's transparent ground cuts out, put it after the body in the file's surface order, and lift it slightly off the skin. |
| Pattern list | The patterns the paint pickers offer and `--paint=random` draws from are those of the airframe whose interior the mod carries (`cockpit`, else `kind_of`); a pattern outside that list still tints the mod with its colours. |
| Where it applies | Everywhere the airframe is built: the flown aircraft, AI aircraft, a network seat (which sends the livery as pattern, colour and decal indices, so each machine tints from its own copy of the mod), the viewer's livery lab, the damage panels and the wreck. |

### Damage

A stock airframe shows damage through nodes of its model: torn-skin panels `pdpN` with their healthy
twins `pdpN_h`, which a zone's `injure_anims` ladder flips through `pdpanelN`, and a `destroyed`
group of wreck pieces `piece1`..`piece4`, which the crash and destroy defs show and fling. Those defs
are the parent's, and they also site the zone fires and smoke at named nodes: each `pdpanelN` entry
burns at its `pdpN`, the fuel leak at `pdp1` and `pdp3`, the crash fires at the pieces, and an AI
plane's damage stages at `prop1`, `lailer1`, `railer1`, `lft_elev` and `rt_elev`.

| Item | Rule |
|---|---|
| Own nodes | A model that holds `destroyed` (its pieces named as the stock ones) or `pdpN` / `pdpN_h` nodes, under the stock names, uses them as a stock airframe does. |
| Auto-wreck | A model with no `destroyed` group breaks into four pieces cut from its own mesh: every drawn triangle under `healthy` goes to the damage zone of the collision hull that holds its centroid (the zone a hit there is charged to), `piece1` the nose, `piece2` the tail, `piece3` the left wing and `piece4` the right wing. Each piece turns about its zone's centre, the centre of its triangles' bounds, and wears the airframe's materials and livery. A zone with no triangles gives an empty piece. |
| Fire anchors | For each stock anchor the model has no node for, an empty node stands at its zone's centre: `pdpN` at the zone whose ladder names `pdpanelN` (on a Brigand-based mod, `pdp1`, `pdp2` and `pdp6` on the right wing, `pdp3` to `pdp5` on the left, `pdp7` on the nose, `pdp8` on the tail), `prop1` at the nose, `lailer1` and `railer1` at the wings, `lft_elev` and `rt_elev` at the tail, and `nose`, `tail`, `leftwing`, `rightwing` at their own zones. They hang under `healthy`, so they leave with the airframe when the wreck appears. |
| No panels | Without `pdpN` nodes the damage stages show no torn skin; their fire and smoke still play at the anchors. |

### Content hash

SHA-256 over every file of the mod, in the ordinal order of their paths, each as its path (lower
case, `/` separated, from the mod's top) and a zero byte, its length as a little-endian 64-bit
integer, then its bytes; written as 64 lower-case hex digits. A folder and a zip of the same files
hash alike, a renamed or changed file changes the hash, and a hidden file does not. Two machines
fly a mod together only when id and hash both match.

## Reader rules and edge cases

### A skipped mod

A mod that fails any check is left out whole: the log names it and the reason (`mod plane avenger
skipped: kind_of pbrigand is not a stock airframe node (...)`), and the list of skipped mods, each
with its id, folder or zip and reason, is kept for a menu to show. A zip that does not open, bad
JSON, a missing `plane.json` or `plane.glb`, and a portrait not in the mod all skip it. Each load
logs `mod planes: N loaded, M skipped from <folder>`.

### Flying a mod

`--plane=<id>` flies a loaded mod as `--plane=` flies a stock node, alone or in a `--plane=` list;
each build logs `HD models: N mesh instance(s) of <id> drawn from its mod's plane.glb`. A reader with
no data of the mod's own takes its parent's: the camera block, the destroy def, the AI def and, with
no `--loadout=`, the stock fit (with `"turrets": []` applied). A crash plays the parent's defs on the
mod's own wreck or the auto-wreck ([Damage](#damage)), logging `crash rig '<id>': zones cut from the
mesh nose N, tail N, leftwing N, rightwing N triangle(s), M effect anchor(s) added`. Its engine sounds
as its parent's, or as `sound`'s plane, flown or as a bot; the build logs the loop it chose
(`audio: engine=snd_brigandengine ...`). A network seat flies it on every machine that holds the
mod ([Multiplayer](#multiplayer)).

### The hangar

The plane builder's airframe list offers each loaded mod after the eleven stock airframes, under
its `name`. A build on it is priced on the parent's stat-table row (cost, weight, agility, armour,
availability, slot titles, engine rows), with `hangar_capacity` in place of the capacity and, for a
mod with `"turrets": []`, no turret slots, so the converted slot costs and weighs as a wing gun
([hangar.md](../org/hangar.md)). Its default configuration is the [default loadout](#default-loadout),
engine tier 2, and the armour of the parent's zones after the mod's `<zone>_armor` overrides. The
description prose and the engine names are the parent's; the picture is the mod's ([Menu art](#menu-art)).

A saved build keeps the mod's id as `modAirframe` beside its parent's stock id in `airframe` in
CSVM's custom plane file; a stock build's file carries no `modAirframe`. The plane pickers fly such a
build as the mod's own node. With the mod not installed the record is kept untouched, the build
flies and prices as the stored parent, and the log says so once per id (`custom plane <name>: mod
airframe <id> is not installed, it flies as <parent>`). On the wire the build carries its parent's
stock id and the mod's row of the session ([Multiplayer](#multiplayer)).

### The campaign

A plane the campaign hangar builds on a mod is owned by the profile with the mod's id as
`modAirframe` beside its parent's stock id in `airframe` ([saved-games.md](saved-games.md), "Funds
and the plane list"); a stock plane's record carries no `modAirframe`. With the mod installed the
plane flies as the mod's own node from the flight check and from a `--campaign=` launch, and the
plane selection and flight check name it by its `name`. With the mod not installed it flies and is
named as the stored parent, the log says so once per id (`campaign plane <name>: mod airframe <id>
is not installed, it flies as <parent>`), and the record is never rewritten. A co-op host flies its
mod plane, and a guest seated on a hangar plane built on a mod flies the mod, since every guest
holds the host's mods ([Multiplayer](#multiplayer)). The campaign wingman, the mission records and
the per-airframe kill tallies keep the parent's stock id.

The hangar offers a mod by `unlock`, against the profile's completed-mission count as it offers a
stock airframe by the stat table's availability:

| `unlock` | Offered from |
|---|---|
| `start` | the first mission |
| `with_parent` or absent | the mission the parent is offered from |
| `<chapter>/<mission>`, e.g. `c1b/m02` | that mission: once the missions before it are completed |
| `<chapter>`, e.g. `c1b` | that world folder's first mission in `cm_sequence` |
| an id `cm_sequence` does not hold | the parent's mission, with the warning `mod plane <id>: unlock <value> names no campaign mission or chapter, it unlocks with its parent <parent>` |

The id is checked each time the game data binds, after the mods' models are grafted. The menu
cheat that unlocks every airframe unlocks the mods too.

### Menu art

A mod has three menu pictures. The portrait is `portrait`'s image, else a three-quarter view of
the model from ahead, left and above in its own paint (no livery, the base textures as `plane.glb`
carries them) on a transparent ground. The plan and head-on views are black silhouettes from above
(nose up) and from ahead, the view of the original's plane-icon sheets, rendered whether or not the
mod has a portrait. Each is drawn into the slot's own box with its aspect kept and centred.

| Slot | Picture | Box |
|---|---|---|
| Plane Construction, every tab (the airframe list's focused mod row, else the build's mod) | portrait | the parent's `PX_<n>_BLUEPRINT.TGA`, 358 by 335 |
| The per-seat aircraft screen, a mod build's row | plan | one `FC_PlaneIcons.png` frame, 80 by 80 |
| Instant Action's Weapon Loadout, a mod build | plan and head-on | one frame of each `OL_PLANEDIAGRAMS*.PNG` sheet |

A `portrait` that does not decode is logged and its slot draws the parent's art. The renders are
made once, on the first frame after the game data binds, by building the grafted airframe in a viewport
of its own, and kept as PNGs in `mod_art/` under Godot's user folder, named
`<id>_<first 16 hex digits of the content hash>_<view>.png`. A later launch reads the files; a
changed mod has a new hash and renders again. A run with no renderer (`--headless`) renders nothing,
and the slots then draw the parent's art. A screen the render lands after is composed again. Every
other plane picture stays the parent's: the campaign pages and the bot panel carry the parent's stock
id ([The hangar](#the-hangar)), the network lobby names a mod row by its `name` over the parent's icon
and ratings, and the local lobby's Select Plane column names the build as a stock airframe of its
parent.

### Bot seats

A bot seat may fly a mod in every field that seats bots: a local Dogfight or stunt race from the
command line (`--vs-bot=<id>`) or from the join board, whose Edit Bot plane list offers each loaded
mod after the eleven stock airframes under its `name`, a `--net-host` field, and the network lobby,
whose bot plane list offers each mod of the host's set after the eleven. A networked field needs no
rule of its own, since every guest holds the host's mods ([Multiplayer](#multiplayer)).

| Seat | Plane |
|---|---|
| Named mod | the mod's id, on a militia that flies its parent, which paints it (the tint mask takes that militia's colours) |
| Random | any militia/airframe pair at equal odds, each mod adding a copy of its parent's pairs, so a mod is drawn as often as its parent |
| A node no airframe has | Random, with the warning `bot: '<node>' is no airframe node, the bot flies a Random one` |

The plane is checked when the field is seated, after the mods are registered, so `--vs-bot=` keeps
the id it was given. A bot seat's airframe is a person's: it flies the mod's def (the parent's with
the overrides). No militia def names a mod, so the bot carries the mod's stock fit
([default loadout](#default-loadout)) rather than a militia's weapons block. A mod bot is judged by
its parent's stock id: on the join board its icon and ratings are the parent's, and on a course that
refuses the parent a named mod bot flies Random and the Random draw passes the mod over.

Scripted campaign enemies and Instant Action's waves stay as the mission authors them.

### Multiplayer

Every machine in a network session flies the host's mods from its own copy, so each guest must hold
them. The host names its mod set, each mod's id, content hash and parent, to every guest: its lobby on
connect, and its session at each join before the handshake. A host with no mod sends nothing, so a
stock session's wire is what it was ([multiplayer-messages.md](../org/multiplayer-messages.md), "Plane
mods").

| Case | What happens |
|---|---|
| The guest holds every host mod with the same hash | it joins; any seat may fly any of the host's mods |
| The guest lacks a host mod, or holds it with another hash | the guest refuses the host. A menu join reads `Install the host's plane mods: <id>, ...` over the games list; a `--net-join` logs `net: the host flies plane mods this machine lacks or holds in another version, the join failed: <id>, ...` and never builds |
| The guest holds a mod the host lacks | it joins; that mod is offered nowhere in the session (the lobby's plane list, its custom planes) |

The rule refuses on any difference in the host's set, not only on a mod a seat flies, because a
lobby's picks, bots and Random draws can put any host mod on a seat until the launch. Each seat's
airframe crosses as its node, the mod's id, so every machine builds the mod from its own `plane.glb`
and paints it from the livery's usual pattern, swatch and decal indices. In the lobby a plane is a
row of the host's set: the eleven stock airframes, then the host's mods in name order, each offered
under its `name` with its parent's icon and ratings. A custom plane on a mod crosses as its parent's
build with the mod's row beside it, and only where the session holds the mod.

| Seat | Plane |
|---|---|
| `--net-host --plane=<id>` | the host's seats, and each guest's (a command-line guest flies the host's plane), fly the mod |
| A lobby pilot's pick or custom plane on a mod | the mod, on every machine |
| A bot, named, picked in the lobby or Random | the mod ([Bot seats](#bot-seats)) |
| A co-op host's plane, or a co-op guest's hangar plane built on a mod | the mod |
| The co-op wingman | its parent's stock airframe |

The master server's game listing carries no mod set; a player learns of a mismatch when the join is
refused.


## Evidence & limits

- The folder, id, `plane.json` and hash rules are `Flight/Hangar/ModPlanes.cs`, covered by
  `CSVM.Tests/ModPlanesTests.cs`. The overrides and the turret rule are `ModPlane.Stats`,
  `ModPlane.StockLoadout` and `PlaneStats.OverrideDefField`, covered by
  `CSVM.Tests/ModPlaneStatsTests.cs`. The `plane.glb` rules are `Mech3/ModAirframes.cs`, covered by
  `CSVM.Tests/ModAirframesTests.cs` and the `mod-plane-build` suite: a Brigand flight build exported
  and wrapped as a mod builds with every drawn mesh from the file and the Brigand's 1315 visible
  triangles, 15 collider hulls, 8 control surfaces, 2 spinning props, 17 weapon markers, reference
  markers and cockpit camera, and the Brigand fit binds 4 hardpoints with its turret slot firing
  forward.
- The cockpit rules are covered by the `mod-plane-cockpit` suite: three Brigand mods mount, at the
  mod's `cockpit_camera`, the Fury's interior exported into the file as `cockpit1`, the Warhawk's
  named by `cockpit` (its file also holds the Fury's interior under another name, and no node of it
  reaches the copy) and the Brigand's, each drawing as many triangles and binding as many gauges as on its stock
  plane. Under the stereo build each leaves out or cuts that interior's head plate, and a model pack's
  body cockpit rig over a mod builds its panel.
- `sound` is `ModPlane.Stats` through `PlaneStats.TakeEngineSounds`, covered by
  `CSVM.Tests/ModPlaneStatsTests.cs`. The Enhanced sound mode's tuning is one set of curves for every
  airframe and a sound pack replaces a sound by its file name, so both follow the defs `sound` picks.
- The `tint` field is `ModPlanes`' and the composite `Mech3/ModTint.cs`, which `Mech3/PlanePainter.cs`
  applies to the materials `SceneBuilder` hands it from the mod's file. Covered by
  `CSVM.Tests/ModTintTests.cs`, `CSVM.Tests/ModPlanesTests.cs` and the `mod-plane-paint` suite: a
  hand-made mod's half-masked body takes each of two schemes' colour 1 on the masked half and keeps its
  base texture on the other, in a flown build, an AI build's shared painter and a livery lab repaint, no
  livery leaves the base texture, and its `suite_noselogo` surface wears each scheme's nose decal.
- The two hangar fields, the economy over a mod's row, the custom plane file's `modAirframe` and
  the missing-mod fallback are covered by `CSVM.Tests/ModPlaneHangarTests.cs`, and the builder with
  a mod loaded by the `menu-original-hangar-mod` suite.
- The damage rules are `Flight/Airframe/AutoWreck.cs`, which `Session/World/WorldEffectsFactory.cs`
  runs for a mod before the crash rig binds. Covered by the `mod-plane-wreck` suite: a hand-made
  fuselage, two wings and a fin cut into four pieces, each in its own zone and on its own side, with
  every drawn triangle in exactly one piece; each missing anchor at its zone's centre and the model's
  own `nose` kept; and a mod's own `destroyed > piece1` built from its file. The anchor names are
  those the parent's defs logged as unresolved on a mod without them (`--debug-anim`).
- The menu art is `UI/Hangar/ModPlaneArt.cs`, covered by `CSVM.Tests/ModPlaneArtTests.cs` (the
  cache name, the portrait choice, the fit), the `mod-plane-art` suite (a Brigand export renders a
  coloured portrait and black silhouettes once into the cache, a second bind reads the files, a changed
  mod renders under its new hash, a mod's portrait image is shown and only its silhouettes render) and
  the `menu-original-hangar-mod` suite (the hub draws a mod's portrait in the blueprint's place).
- The bot seat rules are `Session/Roster/BotSeats.cs` (`Named`, the mod pairs of `DrawPair`), which
  every field of `Launch/SeatFields.cs` calls, and the join board's `DogfightBot.ModAirframe`. Covered
  by `CSVM.Tests/BotSeatsTests.cs`, `CSVM.Tests/VsBotFlagTests.cs`, `CSVM.Tests/JoinBoardBotTests.cs`
  and the `mod-plane-bot` suite: a Brigand mod is seated named and drawn as Random on a Brigand
  militia, a registry without it draws none, it builds from its file, takes the Brigand's AI def with
  its `fd_speed` override, is named by no militia def and binds its fit with the turret slot forward.
  Probe: `--vs --vs-bot=avenger_e1c --mods=<folder>` seats the local Avenger for Medusa, painted in
  Medusa's colours.
- The multiplayer rules are `Net/NetModSet.cs` (the set, its rows, `MissingFrom` and `ModSetMessage`),
  the seat roster's plane field (`SeatRosterMessage`), `NetSession`'s and `NetLobby`'s use of the set,
  `NetPlaneBuild.ModRow` through `Flight/Hangar/CustomPlaneWire.cs`, and the lobby rows of
  `UI/Menu/DogfightLobby.cs`. Covered by `CSVM.Tests/NetMessagesTests.cs` (a stock node and a mod id
  round trip, a 46-byte entry refused, the set's round trip and rows, the comparison),
  `CSVM.Tests/NetSessionTests.cs` (a matched guest joins on a mod seat, a stale one names the mod, a
  stock host sends nothing new), `CSVM.Tests/NetPlaneMessagesTests.cs` (a build on a mod),
  `CSVM.Tests/DogfightLobbyTests.cs` (a guest's mod pick and a bot on a mod row reach the launch's
  roster) and the `net-mod-plane` suite: over loopback a host's guest seat on a scratch mod builds from
  its `plane.glb` on both ends, and a guest holding the mod with another `plane.json` is refused with the
  mod named, in the session and in the lobby.
- The format is CSVM's own. The override names are checked when the stats load, not when the mod
  loads, so a misspelt name warns without skipping the mod. A mission or chapter `unlock` id is
  checked against `cm_sequence` when the game data binds, not when the mod loads, so an unknown id
  warns and falls back to the parent without skipping the mod.
- The campaign rules are `OwnedPlane.ModIn`, `CampaignProfileStore`, `CampaignWallet` and
  `ModPlane.BindCampaign`, covered by `CSVM.Tests/ModPlaneCampaignTests.cs` and the
  `campaign-mod-plane` suite: a `--campaign=` launch over a `--profiles=` store seats an installed
  mod's node and builds it from its `plane.glb`, seats the parent without it and leaves the profile
  byte-identical through a load and save, and a mission `unlock` read from `cm_sequence` offers the
  mod from that mission.
