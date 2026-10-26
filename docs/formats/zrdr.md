# zrdr reader archives, overview

Part of the [format documentation](README.md). `zrdr.zbd` archives hold the engine's
"reader" files, config/definition lists (flight stats, spawns, weather, sounds, anims,
effects) that mech3ax (`unzbd cs reader`) extracts to JSON files of nested arrays, and that
`CSVM/src/Mech3/ZrdrZbd.cs` reads in place. The
shared list-shape conventions (alternating key/list dicts, bare-scalar blocks, dual
colour encoding, `kind_of` inheritance, wildcards, everything-is-float) are in the
[README](README.md#shared-conventions-zrdr-readers).

This page gives the binary encoding of a reader file and the lookup by name, then maps the
archives and their reader families to the pages documenting them.

## Contents

- [Binary encoding](#binary-encoding)
- [Lookup by name](#lookup-by-name)
- [The three scopes](#archive-scopes)
- [Reader family index](#reader-family-map)

## Binary encoding

Each entry of a `zrdr.zbd` ([named-entry archive](zbd-archive.md)) is named `<stem>.zrd` and holds
one value, always a list, that fills the entry exactly. A value is a 4-byte tag, then its body; all
fields are little-endian.

| Tag | Value | Body |
|---|---|---|
| `1` | integer | `i32` |
| `2` | float | `f32` |
| `3` | string | `u32` length, then that many ASCII bytes, no terminator |
| `4` | list | `u32` count, then *count* - 1 values |

- **A list's count is one more than its items.** A count of `1` is an empty list; `0` does not
  occur.
- **The model** (`Zrdr.LoadFile` over today's JSON, `ZrdrZbd` over the file) holds strings,
  `float`s and nested `List<object?>`s. Both number tags become a `float`: an integer converted to
  the nearest one, a float with its stored bits. The JSON detour widened each `f32` to `f64` and
  the loader narrowed it back; for every shipped float that gives back the stored bits.
- **An empty nested list is `null`** in the model, as the extraction wrote it, which is why a key
  followed by `null` reads as a bare flag. An empty root is an empty list for
  `Zrdr.LoadFileOrEmpty` and a refusal for `Zrdr.LoadFile`. Five readers are empty: the root's
  `dlgMessage.zrd` and `mp_dialog.zrd`, C1C's and C2B's `templates.zrd`, C2B/MP1's `targets.zrd`.
- **Every shipped float is finite and every string ASCII**; the extraction refused anything else
  and read every archive. `ZrdrZbd` carries any float's bits and refuses a non-ASCII byte.

## Lookup by name

A consumer names a reader file by its stem in one of three spellings: the stored `vehicle.zrd`,
or today's zip spellings `vehicle.json` and `vehicle.zrd.json`, which all mean `vehicle.zrd`. The
match ignores case; no archive holds two names differing only by case.

- **A repeat is renamed** `stem-N.ext`, the smallest `N` from 1 not yet taken, matched with case
  (the collision rule of [zbd-archive.md](zbd-archive.md#reader-rules-and-edge-cases)). The 62 archives
  hold 1,293 readers and one repeat: the root's second `player.zrd`, looked up as `player-1.zrd`
  (`player-1.json`). The two are different files. The first holds the player-plane globals
  (`ai_skill_parameters`, the sound curves) that `player.json` has always meant; the second holds
  the player plane's damage `ANIMATION_DEFINITIONS`
  ([anim-definitions.md](anim-definitions.md)).
- **Content scans see every entry.** A consumer that walks all readers for a family
  (`Zrdr.LoadMatchingFiles`) reaches the repeat by content, under its rename.

## Archive scopes

A mission sees three zrdr archives; readers with mission relevance (anims, states) must
be scanned across all three:

| Scope | Archive | Typical readers |
|---|---|---|
| shared | top-level `zrdr.zbd` | `vehicle.json`, `engines.json`, `player.json`, `sounds.json`, effect readers (`flame_ball`, `pufftrails`, `fire`, …), building/zeppelin/prop anims, `player_plane_destruct.json` |
| chapter | `<Cx>/zrdr.zbd` | chapter scenery anims (`hangar3`, `train`, `fuel_tanks`, `st_light`, `dock_light`, `ap_*`), `cars_moving`/`trucks_moving`, the `ne0NNNNN.zrd` patrol nets + `neindex.zrd` |
| mission | `<Cx>/<mission>/zrdr.zbd` | `ia.json`, `objectives.json`, `weather.json`, `zepstate.json`, `startanims.json`, `mis_anim.json`, `zeppelins.json`, `egen.json`, `dzones.json`, `aiv.json`, `location.json`, `map.json`, `Briefing` |

## Reader family map

| Reader file(s) | Documented in |
|---|---|
| `vehicle.json`, `engines.json`, `player.json` globals | [vehicle.md](vehicle.md) |
| `ia.json` (spawns, mission type, enemy groups, the ace), `objectives.json` `PLAYER_INIT` + mission map | [spawns.md](spawns.md) |
| `ia.json`'s setup/wrap-up **UI** surface (`INSTANTACTION.SCRIPT`/`IA_WRAPUP.SCRIPT`, `LAYOUT.CSV`, `langui` option strings) | [instant-action.md](instant-action.md) |
| `dzones.json` (per-mission zone overrides) | [missions.md](missions.md) |
| `zeppelins.json`, `egen.json` (mission entities) | [mission-entities.md](mission-entities.md) |
| `ne0NNNNN.zrd` patrol nets + `neindex.zrd` (the chapter AI waypoint graphs) | [ai-nets.md](ai-nets.md) |
| `net.zrd` (the per-mission multiplayer spawn table, unrelated to the patrol nets) | [net-spawns.md](net-spawns.md) |
| `aiv.zrd` (AI rosters), `maneuvers.zrd`, `player.zrd`'s `ai_skill_parameters` | [ai-rosters.md](ai-rosters.md) |
| `ai.zrd` (turrets and AA emplacements) | [turrets.md](turrets.md) |
| `voice.zrd` (the `accentID` → pilot-voice table) | [combat-voice.md](combat-voice.md) |
| `sounds.json` SETS, `player.json` sound curves, WAV format | [sounds.md](sounds.md) |
| `weather.json` (fog / sunlight / cloud cover / wind / precipitation) | [weather.md](weather.md) |
| `zepstate.json`, `startanims.json`, `mis_anim.json`, building/vehicle anims, the compiled `cam_anim.zbd`/`mis_anim.zbd` | [anim-definitions.md](anim-definitions.md) |
| `PUFFER_STATE` effect readers (`flame_ball`, `fire`, `pufftrails`, …) | [effects.md](effects.md) |
| `weapons.json` (the shared `BALLISTICS` weapon catalogue) | [weapons.md](weapons.md) |
| weapon effect readers (`muzzle_burst`, `gunhit`, the `*_control` ordnance bursts) | [weapon-effects.md](weapon-effects.md) |
| destructible `ANIMATION_DEFINITION`s (`HEALTH` / `DAMAGE_SEQUENCE` / `ACTIVATION`) | [destructibles.md](destructibles.md) |
| `shakes.json`, `damage_shakes.json` (plane-wobble oscillator sources + ON_CALL shake defs) | [shakes.md](shakes.md) |
| `templates.zrd` (the clutter decorations' per-model substitution/scale/fade properties) | [templates.md](templates.md) |
| `interp.json` boot scripts (not zrdr, but the same config ecosystem) | [clutter.md](clutter.md) |
| `Briefing.zrd` (the shared briefing dialog and its per-mission reveal scripts); `map.zrd`/`location.zrd` (the in-flight cockpit map, not the briefing) | [briefing.md](briefing.md) |
| `declient.zrd` (the terrain-deformation sizes for craters and quicksand). ⚠ Chapter-level only: it lives in each chapter's `zrdr.zbd`, not the top-level one, so `unzbd`'s reader mode over the shared archive does not produce it | [org/craters.md](../org/craters.md) |

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.

The encoding and the lookup rule are checked by `CSVM.Tests/ZrdrZbdTests.cs`: every one of the 62
archives in `ZbdManifest.Expected`, read in place, builds the same model as today's zip
(`Zrdr.LoadFileOrEmpty` per entry, keyed by zip name), floats compared by their bits, 576,469 leaf
values in all; the counts of readers, renames and empty readers above come from the same run. The
English install is the only one checked.
