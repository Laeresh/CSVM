# Plane mods

How CSVM reads a plane mod: a new airframe a modder adds beside the eleven stock ones, as a model
and a small definition that inherits a stock plane's stats. This is a CSVM format, not a decode of
the original; it is the reference a mod author works from. A [model pack](model-packs.md) changes
how an existing airframe looks; a mod adds an airframe the game did not have.

## Contents

- [Conceptual model](#conceptual-model)
- [Reference](#reference): [folder and id](#folder-and-id), [`plane.json`](#planejson),
  [content hash](#content-hash)
- [Reader rules and edge cases](#reader-rules-and-edge-cases): [a skipped mod](#a-skipped-mod)
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
| Folder | `Mods/Planes` under the data root. Each subfolder and each `.zip` directly inside it is one mod; loose files at its top are ignored. Mods load in name order. |
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
  "overrides": { "fd_speed": 120.0, "turrets": [] }
}
```

| Key | Rule |
|---|---|
| `kind_of` | Required. The stock parent's `planes.zbd` node, one of `player_autogyro`, `player_avenger`, `player_balmoral`, `player_bhawk`, `player_brigand`, `player_pfighter`, `player_fbrand`, `player_fury`, `player_kestrel`, `player_peacemaker`, `player_warhawk`, matched without case. |
| `name` | The name a menu shows. Absent, the id. |
| `overrides` | An object of def field name to number, each a finite JSON number, applied over the parent's def (`vehicle.json`, [vehicle.md](vehicle.md)). `"turrets": []` clears the parent's turret mounts; any other `turrets` value fails. |
| `unlock` | When the plane enters the campaign hangar: `start`, `with_parent` (the default), or a mission or chapter id. |
| `cockpit` | A stock node whose cockpit interior the plane uses when its model has none. Absent, the parent's. |
| `sound` | A stock node whose engine sound the plane uses. Absent, the parent's. |
| `portrait` | The path of an image inside the mod that menus show for the plane. |

Every value is a literal. A key starting with `_` is prose and ignored; any other unknown key, a
wrong type, an empty string or a name outside the stock list skips the mod.

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

## Evidence & limits

- The folder, id, `plane.json` and hash rules are `Flight/Hangar/ModPlanes.cs`, covered by
  `CSVM.Tests/ModPlanesTests.cs`.
- The format is CSVM's own. The override names are not checked against the parent's def when the
  mod loads, and a mission or chapter `unlock` id is not checked against the campaign.
