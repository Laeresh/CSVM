# Extraction status, what comes out of a retail install

Which of Crimson Skies' archive types can be read, how far each has been validated, and what
unzbd's output of them looks like. Everything here was validated against **this project's
retail install**.

**Every archive type this install ships is read.** If you only need the practical facts, they
are:

- CSVM reads every game file in place, with its own C# readers, from an install, a drive, an
  `.iso` or a `.cue`/`.img` ([README.md](README.md#reader-map) maps each archive to its
  reader). There is no extraction step and CSVM ships no extractor. The non-ZBD half (`.rof` UI
  archives, the DLL string tables, the cinemas) is read the same way (`RofFiles`,
  [rof.md](rof.md#reading-in-place)).
- Each reader is checked by a model diff against unzbd's output of the same archive
  (`CSVM.Tests/ModelDiff.cs`): the two build the same loader model, floats compared by their bits.
- unzbd, the extractor of [mech3ax](https://github.com/TerranMechworks/mech3ax), is what a modder
  runs. Its output mirrors the game's own folder structure, chapter and mission folders upper
  case (`C1B/M03`) and archive names lower case (`zrdr.zip`, `rtexture12.zip`), and an opt-in
  modding folder holding it overrides the game files archive by archive. The shape CSVM reads is
  the one the build CSVM v0.4.0 bundled writes (see "Supported extraction shapes" below); naming the folder and the files read are in [../modding.md](../modding.md).

## Support matrix

| Format | Status |
|---|---|
| `texture.zbd` / `rtexture*.zbd` / `rimage.zbd` | ✅ read by `TextureZbd.cs`; unzbd extracts PNGs and round-trips them **byte-identically** (C1 verified) |
| `soundsh.zbd` / `soundsl.zbd` | ✅ read by `SoundZbd.cs`; unzbd extracts WAVs |
| `zrdr.zbd` (reader / mission config) | ✅ read by `ZrdrZbd.cs`; unzbd extracts JSON (ai, engines, Briefing, …) |
| `interp.zbd` | ✅ read by `InterpScripts.cs`; unzbd extracts JSON (engine boot scripts) |
| `gamez.zbd` (world geometry) | ✅ read by `GameZZbd.cs`; unzbd extracts metadata / textures / materials / meshes / nodes JSON and round-trips **byte-identically** in v0.6.1 (C1 + C5 verified) **and in the fork's build** (all 8 chapters). The two builds' JSON *shapes* differ, see the `planes.zbd` row |
| `planes.zbd` (aircraft models) | ✅ read by `GameZZbd.cs`. It is a GameZ-format file (the boot script loads it via `GameZReadZBDFile`). unzbd v0.6.1's round-trip differed by 72 bytes / 6 MB, swapped `\0`/`.` garbage past the null terminator in fixed-width texture-name fields, where the fork's build round-trips byte-identically: the period of a `prefix\0suffix` name was restored at the *first* zero on reading and converted at the *last* one on writing |
| `cam_anim.zbd` / `mis_anim.zbd` | ✅ read by `AnimZbd.cs` and `AnimZbdEvents.cs`, **consumed by the Godot project** (`CompiledAnim.cs` → `AnimProgram.cs` → `AnimRuntime.cs`). unzbd extracts them in the fork's build only (not v0.6.1), **all 61 archives of this install byte-identical through its zip round trip** |
| `GOSDATA/ASSETS/*.rof` | ✅ **not a ZBD, not unzbd**, decoded by this project and read in place (`RofArchive`, `RofFiles`). 846 members, all inflating to their exact declared size. Holds the customisation UI and the per-pattern **paint region masks** ([rof.md](rof.md)) |
| `BINARIES/langui.dll` | ✅ Win32 STRINGTABLE, read in place by this project (`PeStringTable`), 1,247 UI strings including the aircraft names and descriptions ([strings.md](strings.md)) |
| `GOSDATA/ASSETS/GRAPHICS/MPG/*.mpg` | ✅ **read in place, never converted** (`RofFiles`, under upper-case names). The ten movies are not archive members: the `.rof` carries `ASSETS\GRAPHICS\MPG` as a directory entry with nothing behind it and the files sit loose in the install, so the view reads them from there and CSVM decodes MPEG-1 at runtime ([cinemas.md](cinemas.md)) |

## Animation archives

`cam_anim.zbd`/`mis_anim.zbd` have no Crimson Skies support in upstream mech3ax; the fork's
unzbd build added it. Every `AnimDef` field, support array and sequence event is **semantically
decoded** (`AnimZbd.cs`, `AnimZbdEvents.cs`; the events share PM's payload layouts, the CS-only
e46 `SOUND_ADJUST` and e47 si-script `ALL_NAMES` are carried raw; `hangar3_doors` was verified
field-for-field against its zrdr JSON source), and **every SI script frame-decodes**, all 1,090
of them. Rotate cubics are half-angle offsets composed `exp(v)⊗base`; splines are kept as raw
bytes, as MW/PM's are.

In unzbd's output the CS-only container data rides in optional metadata fields (`base_files`,
raw `ptrs`), and the six NaN deltas in C5/M02's `carneypkup_cam.zan` are kept bit-exact in
`delta_raw`.

The schema itself is documented in [anim-definitions.md](anim-definitions.md).

## Supported extraction shapes

The fork's extraction JSON is **intentionally not shape-compatible** with v0.6.1's. Same data,
different spelling:

| v0.6.1 ("legacy") | fork ("unified") |
|---|---|
| `{"Object3d":{…}}` wrapper | flat node with the variant under `data` |
| `mesh_index` / `children` / `transformation` | `model_index` / `child_indices` / `transform` |
| `matrix.a…i` | `original.r00…r22` (no-transform case is the string `"Initial"`) |
| `meshes.json` | `models.json` |
| polygon `unk04` / `triangle_strip` | `priority` / `tri_strip` |
| mesh light `extra` | `vertices` |
| `textures.json` = `{original, renamed}`, plus `texture_ptrs` | `textures.json` = `{name}`, materials carry `texture_index` |
| partition cells `nodes[].index` | `values[].node_index` |
| zrdr entry naming **replaces** the source extension: `vehicle.zrd` → `vehicle.json` | **appends** to it: `vehicle.zrd` → `vehicle.zrd.json` |

Content is unaffected by the zrdr naming difference, all 222 readers of the shared archive
were verified semantically identical across the two extractions.

CSVM's readers of unzbd's output, the modding path, take the **unified** shape only, the one the
build CSVM v0.4.0 bundled writes; the v0.6.1 shape is not read. Two traps in the unified shape
are worth knowing before touching that code, and are recorded in `docs/architecture.md`: its own `index` field is 1-based and
duplicated (v0.6.1's `node_index`) while `child_indices` are **not** in that space, they
stay flat list positions; and unified `scale` is unit everywhere measured, so it is deliberately
ignored.

## Aircraft extraction data

Confirmed usable: `nodes.json` holds **3,317 nodes**, including full hierarchies for
`player_bhawk`, `player_peacemaker`, `player_kestrel`, `player_autogyro`, `player_avenger`,
`player_balmoral` and `player_fury`, with control surfaces (ailerons / elevators), props, gear,
firepoints and cockpits.

Note that **plane skin pixels live in each chapter's `texture.zbd`, not in `planes.zbd`**, and that
the shipped skins are unpainted key textures, see [paint.md](paint.md).

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
