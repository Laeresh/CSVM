# CLAUDE.md

## ⚠ Keep this file up to date

**This file is authoritative project context for Claude.** Whenever you make a non-trivial change to this project — new source modules, new CLI flags, changed defaults, new algorithms, new output fields, renamed entry points, structural refactors — **update this file in the same turn as the code change**. A stale CLAUDE.md misleads future sessions and wastes the user's time.

Update rules:
- Code changes that alter *what the tool does from the outside* (flags, outputs, algorithms, ui) → update the relevant section here.
- Bug fixes that correct a documented invariant → update the invariant.
- Pure refactors with no external effect → usually no update needed.
- When in doubt, update.

## Project Description

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive, Microsoft): a modern engine that plays the original game using the player's own legally-owned game files. Nothing like this exists yet for Crimson Skies — this is a first-of-its-kind effort.

Charter decided 2026-07-14 (full detail in Claude's project memory):

- **Milestone 1** — complete asset extraction: fill the Crimson Skies gaps in mech3ax (`planes.zbd`, `gamez.zbd`).
- **Milestone 2** — vertical slice: free flight only. One plane, one map (candidate: C1 instant-action arena), arcade controls, original sounds. No AI, objectives, or damage.
- Long-term direction (not commitment): full campaign remake.

## 🚫 Hard rule: no game assets in version control — ever

Public open-source project under the **XWVM legal model**: the repo ships **code and format documentation only**. Game files, extracted assets, ZBD contents, screenshots of hexdumps containing bulk asset data — none of it gets committed or published. The importer reads the player's own install at runtime. `.gitignore` enforces this for `CrimsonSkiesGame/`, `extracted/`, and `tools/`; keep it that way when adding directories.

## Architecture & key decisions

- **Engine:** Godot 4 .NET (C#). Consumes extraction output via mech3ax / Mech3DotNet (strongly-typed C# wrapper).
- **Format reverse engineering:** happens in a fork of [mech3ax](https://github.com/TerranMechworks/mech3ax) (Rust), PR'd upstream to TerranMechworks. Their byte-identical round-trip test harness (extract→repack) is the correctness standard.
- **Flight model:** data-driven approximation — parameterized by plane stats from extracted zrdr reader files, hand-tuned against the original game. No exe decompilation.
- **Division of labor:** Claude writes the Rust parsers, Godot code, and docs; the user reviews, playtests flight feel, and owns upstream/community communication.

## Repo layout

- `CrimsonSkies/` — the Godot 4 .NET project (the actual remake; committed). See "Godot project" below.
- `CrimsonSkiesGame/` — the user's retail game install (git-ignored). ZBD archives in `CrimsonSkiesGame/ZBD/` organized as campaign chapters `C1`–`C5`, each with instant action (`IA1`), story missions (`M0x`), multiplayer maps (`MP1`–`3`). Cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored). Notably `planes-gamez.zip` (unzbd of planes.zbd) and `c1-texture.zip` (unzbd of C1/texture.zbd), which the viewer reads.
- `tools/` — downloaded binaries (git-ignored): mech3ax v0.6.1, Godot 4.7 .NET editor at `tools/godot/Godot_v4.7-stable_mono_win64/` (`*_console.exe` for CLI use).

## Godot project (`CrimsonSkies/`)

Godot 4.7 .NET, C# / net8.0. Build & run:

```
dotnet build CrimsonSkies/CrimsonSkies.sln
tools/godot/.../Godot_v4.7-stable_mono_win64_console.exe --path CrimsonSkies res://scenes/Main.tscn -- --plane=player_bhawk
```

(First time only: run with `--headless --import` once before running scenes.)

- `src/Mech3/GameZ.cs` — loads a mech3ax GameZ extraction (ZIP or unpacked dir): nodes.json / meshes.json / materials.json into plain C# objects.
- `src/Mech3/TextureArchive.cs` — texture lookup over an unzbd texture ZIP (PNGs), handles the two name quirks below.
- `src/Mech3/PlaneBuilder.cs` — builds a Node3D/MeshInstance3D tree for one aircraft; one ArrayMesh surface per material; skips cockpit/destroyed/damage/shadow subtrees and non-nearest LODs.
- `src/PlaneViewer.cs` — Main.tscn root script: orbit camera, lighting. User args (after `--`): `--plane=`, `--gamez=`, `--textures=`, `--yaw=`, `--pitch=`, `--screenshot=<path>` (render a few frames, save PNG, quit — used for automated visual verification).

### GameZ format facts (validated on this install, planes.zbd)

- `nodes.json` `children`/`parent` are **flat list positions**, NOT the `node_index` field (node_index has duplicates).
- Euler `transformation.rotation` composes **R = Ry(y)·Rx(x)·Rz(z)** = Godot's `EulerOrder.Yxz` (fit numerically, zero error, against the 221 nodes that also carry a matrix). When `matrix` is present, use it instead; it is stored transposed — real columns are (a,b,c),(d,e,f),(g,h,i).
- Coordinates are right-handed Y-up with the nose at **-Z** — Godot's frame exactly; no mirroring, no UV V-flip.
- `meshes.json` has `null` entries (empty slots) — keep them to preserve `mesh_index` alignment.
- Polygons are n-gons (3..35 verts): triangulate as fan, or as strip when `flags.triangle_strip`; `normal_indices`/`uv_coords` may be null (272 polys have no normals → flat-shade fallback).
- Materials are `Colored` (RGB 0-255 + alpha) or `Textured` (texture referenced **by name**). Two name quirks: fixed-width 20-char truncation (`blo_fusalagebottom.t` → prefix-match) and mech3ax duplicate renames (`bldhwk_cowling.-12.tif` → strip `.-N` suffix).
- Plane skin pixel data is NOT in planes.zbd — it's in each chapter's `texture.zbd` (C1's contains all player-plane skins).
- Aircraft tree shape: `player_*` → `geometry` → `healthy` → LOD nodes (`nearest` = range.min 0 is highest detail) + `markers` (firepoints/pylons/camera), plus `cockpit1` (separate interior model), `destroyed`, `shadow`, `dontmove` (props: `staticprop1` static; `prop1`/`prop1b`/`prop2*`/`nitroprop1` are spin-animation frames).

## Format support status (validated against THIS install with mech3ax v0.6.1, 2026-07-14)

mech3ax's README support matrix is outdated — actual v0.6.1 support for CS is far better. All validation ran on this install (`unzbd cs …`, round-trip via `rezbd cs …` + sha256):

| Format | Status |
|---|---|
| `texture.zbd` / `rtexture*.zbd` / `rimage.zbd` | ✅ extracts to PNGs; round-trip **byte-identical** (C1 verified) |
| `soundsh.zbd` / `soundsl.zbd` | ✅ extracts to WAVs |
| `zrdr.zbd` (reader/mission config) | ✅ extracts to JSON (ai, engines, Briefing, …) |
| `interp.zbd` | ✅ extracts to JSON (engine boot scripts) |
| `gamez.zbd` (world geometry) | ✅ extracts (metadata/textures/materials/meshes/nodes JSON); round-trip **byte-identical** (C1 + C5 verified) |
| `planes.zbd` (aircraft models) | ✅ extracts — it's a GameZ-format file (the boot script loads it via `GameZReadZBDFile`). Round-trip differs by only 72 bytes / 6 MB: swapped `\0`/`.` garbage past the null terminator in fixed-width texture-name fields. Semantically lossless; upstream fix candidate. |
| `cam_anim.zbd` / `mis_anim.zbd` | ❌ genuinely unsupported — deferred, not needed for free flight |

Extracted plane data confirmed usable: `nodes.json` has 3,317 nodes including full hierarchies for `player_bhawk`, `player_peacemaker`, `player_kestrel`, `player_autogyro`, `player_avenger`, `player_balmoral`, `player_fury` with control surfaces (ailerons/elevators), props, gear, firepoints, cockpits.

## Current status / next step

**Milestone 1 (extraction) is essentially already delivered by mech3ax v0.6.1** — the planned RE work is reduced to (a) the cosmetic planes.zbd padding nit (upstream PR candidate) and (b) the deferred anim formats.

**Milestone 2 in progress (2026-07-14): plane rendering works.** The Godot project renders textured aircraft from the player's own extracted data — verified via screenshots for `player_bhawk`, `player_kestrel`, `player_autogyro` (correct skins, decals upright, geometry not mirrored). **Next steps:** C1 terrain/world rendering from `c1-gamez.zip`, then arcade flight controls (zrdr plane stats), then sound.
