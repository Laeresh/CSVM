# Crimson Skies format documentation

Reverse-engineered format reference for **Crimson Skies** (2000, Zipper Interactive /
Microsoft), validated against a retail install with [mech3ax](https://github.com/TerranMechworks/mech3ax)
(`unzbd cs <mode>` — originally v0.6.1, and since 2026-07-21 this project's fork; see
[extraction.md](extraction.md)). This is the project's public deliverable under the XWVM legal
model: **format documentation and code only — no game asset data.** Pages carry field
tables and tiny excerpt values, never bulk extracted content.

Everything here was decoded by inspecting extracted data and matching behavior against
the original game (screenshots, videos, in-game measurements) — no exe decompilation.

## Pages

| Page | Covers |
|---|---|
| [extraction.md](extraction.md) | **Start here for tooling:** which archive types extract, how far each round-trips, the two extraction JSON shapes, where the output lands |
| [gotchas.md](gotchas.md) | **The cross-cutting gotchas that bite constantly:** node indexing, transforms/handedness, winding + culling, vertex-color lighting, UV tiling, draw priority + subfaces — read before writing any reader or renderer |
| [gamez.md](gamez.md) | The GameZ container (`gamez.zbd`, `planes.zbd`): nodes/meshes/materials JSON, transforms, draw priority, backface flags, aircraft trees, damage-panel states |
| [world-structure.md](world-structure.md) | Chapter worlds: partition grid, terrain tiling, skydome zones, point-sprite lights, flare billboards, map-edge behavior |
| [zrdr.md](zrdr.md) | The zrdr reader archives: what they are, the three scopes (shared / chapter / mission), which reader file is documented where |
| [vehicle.md](vehicle.md) | `vehicle.json` aircraft defs: `kind_of` inheritance, dynamics, engines, `destroyable_parts` damage model, collision points, and the weapon/turret/AI keys |
| [markers.md](markers.md) | Aircraft weapon rig (`planes.zbd` `markers`): firepoints + pylons + `target`, the `IDS_AIRFRAMEGUNGROUPNAMES` gun-group enum, the per-airframe W1–W4 gun-mount table, and the slot→firepoint binding rule |
| [spawns.md](spawns.md) | Player spawns + the Instant Action config: `ia.json` (`spawn_points`, mission/enemy/ace setup), `objectives.json` `PLAYER_INIT`, the campaign mission ↔ folder map |
| [missions.md](missions.md) | Mission objectives: the stunt `dzones` Danger Zones + their entry/exit gate geometry, per-mission `dzones.json` overrides, `targets.json` node→string keys, the `messages.json` string table |
| [mission-entities.md](mission-entities.md) | `zeppelins.json` (motion, gasbags, broadside cannons, critical-zone threshold) and `egen.json` (enemy generators, the zeppelin fighter-launch altitude gate) |
| [ai-nets.md](ai-nets.md) | The chapter AI patrol graphs: `ne0NNNNN.zrd` waypoint nets (nodes + explicit branching edge list + attach-target trailer) and the `neindex.zrd` id→name table every AI reader references |
| [sounds.md](sounds.md) | `sounds.json` SETS, the `player.json` volume/pitch curves, the game's MS-ADPCM WAV format |
| [weather.md](weather.md) | `weather.json`: per-zone fog, `SUNLIGHT_*` world lighting, cloud cover, wind, precipitation, the dual colour encoding |
| [anim-definitions.md](anim-definitions.md) | `ANIMATION_DEFINITION` readers (zepstate/startanims/building anims) + the compiled `cam_anim.zbd`/`mis_anim.zbd` survey |
| [destructibles.md](destructibles.md) | World destructibles: how an `ANIMATION_DEFINITION` becomes a destructible object — `HEALTH`, `WeaponHit`/`WeaponOrCollideHit` activation, the `ANIM_HEALTH` `DAMAGE_SEQUENCE`, the death sequence, the 44 collide-destructibles |
| [effects.md](effects.md) | `PUFFER_STATE` billboard-particle emitters, the effect reader files, flipbook textures, anchor nodes |
| [weapons.md](weapons.md) | `weapons.json` `BALLISTICS`: the 48-entry weapon catalogue (guns/rockets/ordnance), damage & allotment fields, the player caliber×ammo matrix + AI detune, the `FIRE`/`FLYOUT`/`IMPACT` surface-class bindings |
| [loadouts.md](loadouts.md) | **Our** `CSVM/data/stock_loadouts.json` (not an extracted format): the 11 aircraft's stock weapon fit — gun groups (mount/caliber/ammo/markers), turret slots, pylons — plus the gun→`wep_*` and slot→firepoint resolution rules |
| [weapon-effects.md](weapon-effects.md) | The muzzle/flyout/impact effect readers (`muzzle_burst`, `gunhit`, the `*_control` ordnance bursts) and the gamez projectile prototype roots the weapon bindings resolve to |
| [interp.md](interp.md) | `interp.zbd` boot scripts (`.gw`): the command format, and the **per-mission world setup** that decides which entities a mission shows (zeppelins, CTF props) |
| [clutter.md](clutter.md) | The clutter system: `interp.json` boot scripts, `AddClutterTemplates`, template subtree shape |
| [templates.md](templates.md) | `templates.zrd`: the clutter decorations' per-model properties — substitution, scale, fade, and the five keys no chapter authors |
| [fogvol.md](fogvol.md) | Fog volumes: `fogvol.zrd`'s clutter table + the gamez `fvol*` boxes — the authored ambient cloud field, and the three chapters that render none |
| [hud.md](hud.md) | HUD: the compass tape textures + drum projection, the cockpit gauge dials (altimeter/speedometer/damage display) |
| [camparam.md](camparam.md) | `camparam.json`: the chase/third-person camera tuning — per-plane chase distance (keyed by DISPLAY name), catch-up rates, and the look-behind/death/crash/flyby geometry |
| [shakes.md](shakes.md) | `shakes.json` (six plane-wobble oscillator sources: frequency/damp/waveform + a magnitude term; fire_bullet's decoded as caliber → radians of roll, measured) and `damage_shakes.json` (ON_CALL small/medium/large shake defs, callers exe-side, unconsumed) |
| [paint.md](paint.md) | Aircraft paint: `paint_pattern`/`paint_color`/`paint_decal` schemes, the numbered 00–49 decal set, why shipped skins are unpainted key textures |
| [rof.md](rof.md) | `.rof` UI resource archives: the container, the GUI scripts + `LAYOUT.CSV`, and the `.BM` texture format carrying the **paint region masks** |
| [strings.md](strings.md) | UI text: the `langui.dll` Win32 string table, `RESOURCE.H` symbols, the `[FONTID]` convention, the aircraft name/description blocks — plus the **bindable-command inventory** (`MSG_CMD_*`/`MSG_CAM*`), the authoritative list of what the retail game let a player do |

## Shared conventions (zrdr readers)

The zrdr "reader" files are the engine's config/script lists; mech3ax extracts each to a
JSON file of nested arrays. Conventions that recur across every reader family:

- **All numbers arrive as floats.** mech3ax emits every scalar through a single-precision
  path, so integer-looking values (`PARTICLES 100`) parse as `100.0` — read as float,
  cast as needed.
- **Alternating key/list dicts.** The dominant shape is a flat list alternating
  `"KEY", [values…]` — a dictionary by convention, not by JSON structure. Caveat:
  **duplicate keys can be meaningful** (multiple `SEQUENCE_DEFINITION`s per animation
  definition, repeated ops per sequence) — a collapsing dict view silently loses data;
  walk the raw list where duplicates matter (see [anim-definitions.md](anim-definitions.md)).
- **Bare-scalar blocks.** Some blocks instead pair keys with a *bare* value
  (`"TOP", 1124` / `"TYPE", "SNOW"`) or mix bare scalars and lists — a dict view built
  for `key,[list]` drops the values. Known cases: `weather.json`'s `CLOUD_COVER`, `WIND`
  and precipitation blocks (see [weather.md](weather.md)).
- **A key followed by `null` (or by another key) is a bare flag** (`LOCAL_NODES_ONLY`,
  `LOOPED`).
- **Dual colour-triple encoding.** RGB triples coexist in two encodings, even within one
  file: normalized floats (`[0.69, 0.69, 0.69]`) and integer 0–255 (`[192, 192, 192]`).
  Rule, verified unambiguous across the install: **divide by 255 iff any component is
  strictly > 1** (a component of exactly 1.0 belongs to a float triple). The normalized
  value is a DX7 sRGB framebuffer colour. Details + the proof in [weather.md](weather.md).
- **`kind_of` inheritance.** Definition files (e.g. `vehicle.json`) alternate
  `defName, [properties…]`; a def's `kind_of` names its parent def and properties resolve
  nearest-first through the chain (`pbloodhawk` → `player_airplane` → `basic_airplane`).
- **Name wildcards.** Where readers reference scene-node names (animation definitions),
  `*`/`**` match any run of characters, `#` a run of digits, and the `.flt` model suffix
  is optional (`ap_radiotwr` ↔ node `ap_radiotwr.flt`).
- **Units** are meters, seconds, and degrees throughout (`fd_speed 135` m/s ≈ 302 mph =
  the Bloodhawk's published top speed; `RANGE` distances in meters; rotations in degrees).

## Extraction map

`ExtractAssets.ps1` (repo root) runs the right `unzbd cs` mode per archive type:
`interp` → `interp.json` (boot scripts); `planes.zbd`/`gamez.zbd` → `gamez` (JSON + mesh
data); `soundsh/soundsl` → `sounds` (WAVs); `zrdr` → `reader` (JSON);
`rimage`/`texture`/`rtexture*` → `textures` (PNGs). The per-chapter `rtextureN` archives
are *downscaled* quality tiers of the base `texture` set (never higher-res); `rimage` is
menu/briefing UI only. `cam_anim.zbd`/`mis_anim.zbd` → `anim` (JSON defs + SI scripts) —
**supported in this project's mech3ax fork**, not in upstream or in the pinned v0.6.1 binary;
the schema lives in [anim-definitions.md](anim-definitions.md). Per-type support and
round-trip status: [extraction.md](extraction.md).

`ExtractRof.ps1` (repo root) covers the non-ZBD half of the install: the `.rof` UI resource
archives and the `langui.dll` string table, into `extracted\rof\`. These are decoded by this
project rather than by mech3ax — see [rof.md](rof.md) and [strings.md](strings.md).

## License

This directory is licensed under **Creative Commons Attribution 4.0 International**
([LICENSE](LICENSE)) — deliberately more permissive than the GPL-3.0-or-later covering the
rest of the repository, so these findings can be reused by any project whatever its own
license. Attribute to **CSVM** (<https://github.com/Laeresh/CSVM>). The rest of the repository,
including the code that reads these formats, remains GPL-3.0-or-later.
