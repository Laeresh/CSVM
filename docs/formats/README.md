# Crimson Skies format documentation

Reverse-engineered format reference for **Crimson Skies** (2000, Zipper Interactive /
Microsoft), validated against a retail install with [mech3ax](https://github.com/TerranMechworks/mech3ax)
(`unzbd cs <mode>` — originally v0.6.1, and since 2026-07-21 this project's fork; see
[extraction.md](extraction.md)). This is the project's public deliverable under the XWVM legal
model: **format documentation and code only — no game asset data.** Pages carry field
tables and tiny excerpt values, never bulk extracted content.

Most of this was decoded by inspecting extracted data and matching behavior against the original
game (screenshots, videos, in-game measurements). A few pages additionally cite `crimson.exe` —
where the retail binary embeds a literal that settles a question the data cannot, such as the
editor format comment naming every `aiv` roster field ([ai-rosters.md](ai-rosters.md)) or the
absent `ThrustFactor` token ([vehicle.md](vehicle.md)). Such claims name the evidence at the point
of use; no code is reproduced.

A page whose claims come from the executable **wholesale** does not live here at all — it goes in
[`docs/org/`](../org/), outside this directory and outside its licence:
[flightModel.md](../org/flightModel.md) (the flight model), [puffer.md](../org/puffer.md) (the
particle runtime behind [effects.md](effects.md)), [tracers.md](../org/tracers.md) (the projectile
visual runtime behind [weapons.md](weapons.md)), [objectMotion.md](../org/objectMotion.md) (the
launch/contact/run-time model behind the `OBJECT_MOTION` sections of
[anim-definitions.md](anim-definitions.md) and [destructibles.md](destructibles.md)),
[aim-assist.md](../org/aim-assist.md) (the sticky-bullet gun assist behind
[vehicle.md](vehicle.md)'s `player.json` table), [aiControlLaw.md](../org/aiControlLaw.md) (the AI
steering law behind [ai-rosters.md](ai-rosters.md)'s skill and control-scale slots),
[aiPilot.md](../org/aiPilot.md) (what a roster vehicle actually flies: the `mode` classes, the
patrol-net follower and the formation escort, behind [ai-rosters.md](ai-rosters.md) and
[ai-nets.md](ai-nets.md)) and
[textures.md](../org/textures.md) (the texture header layout and the additive-vs-mix blend rule
behind [effects.md](effects.md)'s sprites).

## Reader map

### Start here

- [extraction.md](extraction.md) � extraction modes, output, and round-trip support.
- [gotchas.md](gotchas.md) � cross-cutting reader and renderer rules.
- [zrdr.md](zrdr.md) � reader archives and their family index.

### World and scene

- [gamez.md](gamez.md), [world-structure.md](world-structure.md), [interp.md](interp.md), [clutter.md](clutter.md), [templates.md](templates.md), and [fogvol.md](fogvol.md).
- [weather.md](weather.md), [anim-definitions.md](anim-definitions.md),`r`n  - [Weather atmosphere controls](weather/atmosphere.md) � cloud cover, wind, and precipitation. [destructibles.md](destructibles.md), and [effects.md](effects.md).`r`n  - [Compiled animation archives](anim-definitions/compiled-archives.md) � the compiled archive and SI-script reference.

### Aircraft and combat

- [vehicle.md](vehicle.md), [markers.md](markers.md),`r`n  - [Player global blocks](vehicle/player-globals.md) � `player.json` globals. [loadouts.md](loadouts.md), [paint.md](paint.md), and [camparam.md](camparam.md).
- [weapons.md](weapons.md), [weapon-effects.md](weapon-effects.md),`r`n  - [Ordnance effects and projectile prototypes](weapon-effects/ordnance.md). [turrets.md](turrets.md), [shakes.md](shakes.md), and [sounds.md](sounds.md).

### Missions and AI

- [spawns.md](spawns.md), [missions.md](missions.md), [mission-entities.md](mission-entities.md), and [instant-action.md](instant-action.md).`r`n  - [Instant Action wrap-up](instant-action/wrap-up.md) � scoring and friendly-fire rules.
- [ai-nets.md](ai-nets.md), [ai-rosters.md](ai-rosters.md), and [combat-voice.md](combat-voice.md).

### Presentation and UI

- [hud.md](hud.md), [rof.md](rof.md), and [strings.md](strings.md).
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
  is optional (`ap_radiotwr` ↔ node `ap_radiotwr.flt`). **Not only node names** — a
  `PUFFER_STATE`'s own `NAME`, which is a separate namespace, takes a `*` too and expands at
  compile time (`torch_puffer*` → `torch_puffer1`/`2`); see
  [anim-definitions.md](anim-definitions.md).
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
