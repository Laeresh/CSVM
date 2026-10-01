# Test fixtures — hand-authored, never extracted

Every file here was written **by hand from `docs/formats/`**, describing invented content:
`probe_*` node names, `wep_probe_*` weapon ids, `snd_probe_*` sound names, `MSG_PROBE_*`
message keys. None of it is a copy — trimmed, sampled or otherwise — of a Crimson Skies
extraction. The repo's hard rule (see `PROJECT_CONTEXT.md`) is that a "small real example" is still a
game asset, so anything sourced from the install stays out of version control and is covered
instead by the golden-invariant tests, which read the player's own `extracted/` at run time and
skip when it is absent.

Byte-level fixtures (WAV/ADPCM) are not files at all: they are assembled field by field in
`WavFileTests.cs`, so the provenance of every byte is visible in the code that writes it.

The engine reads this folder too, from the repo checkout: `--synthetic-data` assembles its
invented `extracted/` tree from files named here plus files generated in code
(`CSVM/src/Tooling/SyntheticData.cs`). A record a synthetic family copies keeps its path, or the
tree fails to build.

| Path | Format page | Exercises |
|---|---|---|
| `zrdr/shapes.json` | `zrdr.md`, `README.md` "Shared conventions" | alternating key/list dicts, bare flags, stray values, duplicate keys |
| `zrdr/weapons.json` | `weapons.md` | `BALLISTICS` typing, class flags, `IMPACT` surface classes, the unhandled-key tripwire |
| `zrdr/sounds.json` | `sounds.md` | `SETS` flags and value keys, `SOUND_GROUPS` weights and `DYNAMIC_WEIGHTS` recency |
| `zrdr/targets.json` | `missions.md` | `targets.json` pair-list entries, multi-node entries |
| `zrdr/demo_anims.json` | `anim-definitions.md` | reader→compiled normalization, the unit conversions, `DAMAGE_SEQUENCE` |
| `zrdr/maneuvers.json` | `ai-rosters.md` | both step shapes (4- and 7-element), the flags (`relative`, `autogyro_allowed`, `nitro`, `bias`), a stub with no `steps`, the eligibility cull |
| `zrdr/fogvol.json` | `fogvol.md` | the fog-volume header keys, a weighted `clutter` block, its two `far_fade_range` bands and the scatter ranges |
| `zrdr/weather.json` | `weather.md` | `CLOUD_COVER`'s bare-scalar `TOP`/`BOTTOM`/`THICKNESS`, one `ZONE1` fog block, `WeatherState.CameraWeatherState`'s state-2 threshold |
| `weather-no-cloud/weather.json` | `weather.md` | a mission with `ZONE1` fog and no `CLOUD_COVER` block at all — `CameraWeatherState` pinned to state 1 at any altitude |
| `gamez-plane/` | `gamez.md`, `markers.md` | node tree parse, the Yxz Euler order, marker rig extraction |
| `messages.json` | `missions.md` | the message table and its `%1`/`!d!` placeholder grammar |
| `ia/ia.zrd.json` | `instant-action.md` | the full `InstantActionDef` record, the `num_enemies` clamp to 6, a bare `groupN, null` wave |
| `ia-minimal/ia.zrd.json` | `instant-action.md` | every optional key's built-in default, and the `dogfight_ace` wingmen/wave zero-forcing rule |
| `ia-cli.json` | `instant-action.md` | the `--ia=` plain-JSON-object shape (not the zrdr flat-alternating one), a JSON-`null` wave |
| `menu-layout/` | `menu-layout.md` | the sectioned-CSV grammar, `V<n>` beating `G<n>`, an unresolvable macro, every widget type's field order, the `ResID` → header → text join, the button colour tail and frame counts, the quoted scrapbook rectangle, a stray line and an unknown type |
| `menu-layout-original/` | `menu-layout.md`, `instant-action.md` | the Original shell's screens under the shipped widget keys on invented lines and invented `PM_*`/`PI_*` art: the main menu's panes and six buttons, the flight check's paper plaque, the Instant Action section (the contents list, the dropdowns on shared lines for the two enemy pages, the radio pair, the text rows), and the ammunition screen's backdrop, plane diagrams and text rows; `--synthetic-data` decodes it into the tree's `menu_layout.json` |
| `synthetic/rof/ui_strings.json` | `strings.md` | `ui_strings.json`'s row shape (`id`, `symbol`, `font`, `text`, `dll`) with invented text: one row per `IDS_` symbol `menu-layout-original/` names, on invented ids, and rows with no symbol for ids the code reads directly (the lobby's three type lines) |
| `synthetic/rof/art.json` | `menu-layout.md` | an invented pixel size for every picture the Original shell draws, a button strip four frames tall (eight for a check or radio) and a pane strip its `NumFrames`; one PNG, JPEG or TGA per entry is generated at that size |
| `synthetic/C1/texture/manifest.json` | `docs/org/textures.md` | the extraction manifest's `texture_infos` (`name`, `width`, `height`, `alpha`, `stretch`) for the synthetic C1 texture archive; one PNG per entry is generated at that size |
| `synthetic/planes/` | `gamez.md`, `markers.md` | the stand-in `probe_plane`'s legacy-shape tree: `geometry` → `healthy` → one LOD, the `markers` rig (eight firepoints in mirror pairs, eight pylons odd to port, `target`, `cockpit_camera`, two exhausts), `dontmove` props, `destroyed` pieces, `pdpN` torn panels and a `cockpit1` interior with `pcdp4`/`pcdp6`; `boxes.json` lists one box per mesh index, and `models.json` is generated from it |
| `synthetic/zrdr/vehicle.json`, `engines.json`, `player.json` | `vehicle.md`, `vehicle/player-globals.md`, `ai-rosters.md` | the `pprobe` player def (`nodename probe_plane`, dynamics, four zones with `pdpanelN` injure entries, six collision probes) and its `probe` AI flavour with a `weapons` block; three engine rows; flight globals, the `crash` block and every `ai_skill_parameters` pair. The tree also copies `zrdr/maneuvers.json` above as its maneuver library |
| `synthetic/zrdr/weapons.json`, `shakes.json`, `synthetic/messages.json` | `weapons.md`, `shakes.md`, `missions.md` | one gun (`wep_probe_gun`) and one rocket (`wep_probe_rocket`), the six shake sources, and the strings the flight reads, each a `MSG_PROBE_*` key or an invented wording of a key the HUD names |
| `synthetic/stock_loadouts.json` | `loadouts.md` | the stand-in's stock fit, its guns named by `weapon` rather than caliber; read in place as `StockLoadouts.Supplement`, not written into the tree |
