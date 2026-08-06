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
| `zrdr/fogvol.json` | `fogvol.md` | the fog-volume header keys, a weighted `clutter` block, its two `far_fade_range` bands and the scatter ranges |
| `gamez-plane/` | `gamez.md`, `markers.md` | node tree parse, the Yxz Euler order, marker rig extraction |
| `messages.json` | `missions.md` | the message table and its `%1`/`!d!` placeholder grammar |
