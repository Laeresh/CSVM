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
| [gamez.md](gamez.md) | The GameZ container (`gamez.zbd`, `planes.zbd`): nodes/meshes/materials JSON, transforms, draw priority, backface flags, aircraft trees, damage-panel states |
| [world-structure.md](world-structure.md) | Chapter worlds: partition grid, terrain tiling, skydome zones, point-sprite lights, flare billboards, map-edge behavior |
| [zrdr.md](zrdr.md) | The zrdr reader archives: what they are, the three scopes (shared / chapter / mission), which reader file is documented where |
| [vehicle.md](vehicle.md) | `vehicle.json` aircraft defs: `kind_of` inheritance, dynamics, engines, `destroyable_parts` damage model, collision points |
| [spawns.md](spawns.md) | Player spawns: `ia.json` `spawn_points`, `objectives.json` `PLAYER_INIT`, the campaign mission ↔ folder map |
| [missions.md](missions.md) | Mission objectives: the stunt `dzones` Danger Zones, `targets.json` node→string keys, the `messages.json` string table |
| [sounds.md](sounds.md) | `sounds.json` SETS, the `player.json` volume/pitch curves, the game's MS-ADPCM WAV format |
| [weather.md](weather.md) | `weather.json`: per-zone fog, `SUNLIGHT_*` world lighting, cloud cover, wind, precipitation, the dual colour encoding |
| [anim-definitions.md](anim-definitions.md) | `ANIMATION_DEFINITION` readers (zepstate/startanims/building anims) + the compiled `cam_anim.zbd`/`mis_anim.zbd` survey |
| [effects.md](effects.md) | `PUFFER_STATE` billboard-particle emitters, the effect reader files, flipbook textures, anchor nodes |
| [interp.md](interp.md) | `interp.zbd` boot scripts (`.gw`): the command format, and the **per-mission world setup** that decides which entities a mission shows (zeppelins, CTF props) |
| [clutter.md](clutter.md) | The clutter system: `interp.json` boot scripts, `AddClutterTemplates`, template subtree shape |
| [hud.md](hud.md) | HUD: the compass tape textures + drum projection, the cockpit gauge dials (altimeter/speedometer/damage display) |
| [paint.md](paint.md) | Aircraft paint: `paint_pattern`/`paint_color`/`paint_decal` schemes, the numbered 00–49 decal set, why shipped skins are unpainted key textures |
| [rof.md](rof.md) | `.rof` UI resource archives: the container, the GUI scripts + `LAYOUT.CSV`, and the `.BM` texture format carrying the **paint region masks** |
| [strings.md](strings.md) | UI text: the `langui.dll` Win32 string table, `RESOURCE.H` symbols, the `[FONTID]` convention, the aircraft name/description blocks |

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
