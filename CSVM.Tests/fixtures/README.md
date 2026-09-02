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
| `menu-layout-original/` | `menu-layout.md`, `instant-action.md` | the Original shell's screens under the shipped widget keys on invented lines and invented `PM_*`/`PI_*` art: the main menu's panes and six buttons, the flight check's paper plaque, and the Instant Action section (the contents list, the dropdowns on shared lines for the two enemy pages, the radio pair, the text rows) |
