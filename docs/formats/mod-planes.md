# Plane mods

How CSVM reads a plane mod: a new airframe a modder adds beside the eleven stock ones, as a model
and a small definition that inherits a stock plane's stats. This is a CSVM format, not a decode of
the original; it is the reference a mod author works from. A [model pack](model-packs.md) changes
how an existing airframe looks; a mod adds an airframe the game did not have.

## Contents

- [Conceptual model](#conceptual-model)
- [Reference](#reference): [folder and id](#folder-and-id), [`plane.json`](#planejson),
  [overrides](#overrides), [`plane.glb`](#planeglb), [paint](#paint), [content hash](#content-hash)
- [Reader rules and edge cases](#reader-rules-and-edge-cases): [a skipped mod](#a-skipped-mod),
  [flying a mod](#flying-a-mod)
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
| Id | The folder's name, or the zip's name without `.zip`, as spelt on disk. Ids match without case. An id that is a stock airframe's `planes.zbd` node (`player_avenger` is the Hellhound's) or an earlier mod's id skips the mod. |
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
  "tint": { "mask": "avenger_mask.png", "detail": "avenger_detail.png", "materials": ["avenger"] }
}
```

| Key | Rule |
|---|---|
| `kind_of` | Required. The stock parent's `planes.zbd` node, one of `player_autogyro`, `player_avenger`, `player_balmoral`, `player_bhawk`, `player_brigand`, `player_pfighter`, `player_fbrand`, `player_fury`, `player_kestrel`, `player_peacemaker`, `player_warhawk`, matched without case. |
| `name` | The name a menu shows. Absent, the id. |
| `overrides` | An object of def field name to number, each a finite JSON number, applied over the parent's def (`vehicle.json`, [vehicle.md](vehicle.md)); the names are in [Overrides](#overrides). `"turrets": []` clears the parent's turret mounts; any other `turrets` value fails. |
| `unlock` | When the plane enters the campaign hangar: `start`, `with_parent` (the default), or a mission or chapter id. |
| `cockpit` | A stock node whose cockpit interior the plane uses when its model has none. Absent, the parent's. |
| `sound` | A stock node whose engine sound the plane uses. Absent, the parent's. |
| `portrait` | The path of an image inside the mod that menus show for the plane. |
| `tint` | How a livery colours the plane's own surfaces: `mask` and `detail`, each the path of a `.png` inside the mod, and `materials`, a non-empty list of `plane.glb` material names. All three are required; a `_` key inside is prose. Absent, the plane's own surfaces keep their paint under every livery. The rules are in [Paint](#paint). |

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

A bot flying the mod plane takes the parent's AI def: its damage model, armament, skills and
turrets come from that def's chain as for the parent, and the `dynamics` and def-level overrides
apply as on a flown plane. The zone overrides have nothing to act on there, since an AI load
carries no zones. The plane keeps the parent's def name, which is what its stock fit is found by.

`"turrets": []` clears the turret mounts on the flown and the bot load alike, and makes each of
the parent's turret gun slots a forward gun slot firing from the firepoints the slot names, with
its calibre, ammunition and barrel count unchanged. A Brigand-based mod's W4 Rear Turret becomes a
forward 30 calibre gun on `firepoint1` and `firepoint2`. The stock planes' fits are unchanged.

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
| Cockpit | A `cockpit1` node directly under the root is the plane's own interior, in interior units as a stock `cockpit1.glb` is. Without one the plane takes a copy of the interior of `plane.json`'s `cockpit`, else its parent's, mounted at the mod's `cockpit_camera`. |
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
no `--loadout=`, the stock fit (with `"turrets": []` applied). The mod has no wreck pieces or damage
panels unless its model holds the stock `destroyed` and `pdpN` nodes, a crash then playing the parent's
destroy def without pieces, and its engine sounds as its parent's (the def's `engine_sound`). The hangar, the campaign, bot seats, saves and the network wire still list the
stock eleven alone.

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
- The `tint` field is `ModPlanes`' and the composite `Mech3/ModTint.cs`, which `Mech3/PlanePainter.cs`
  applies to the materials `SceneBuilder` hands it from the mod's file. Covered by
  `CSVM.Tests/ModTintTests.cs`, `CSVM.Tests/ModPlanesTests.cs` and the `mod-plane-paint` suite: a
  hand-made mod's half-masked body takes each of two schemes' colour 1 on the masked half and keeps its
  base texture on the other, in a flown build, an AI build's shared painter and a livery lab repaint, no
  livery leaves the base texture, and its `suite_noselogo` surface wears each scheme's nose decal.
- The format is CSVM's own. The override names are checked when the stats load, not when the mod
  loads, so a misspelt name warns without skipping the mod. A mission or chapter `unlock` id is not
  checked against the campaign.
