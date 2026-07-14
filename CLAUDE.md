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

- `CrimsonSkiesGame/` — the user's retail game install (git-ignored). ZBD archives in `CrimsonSkiesGame/ZBD/` organized as campaign chapters `C1`–`C5`, each with instant action (`IA1`), story missions (`M0x`), multiplayer maps (`MP1`–`3`). Cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored).
- `tools/` — downloaded binaries, e.g. mech3ax releases (git-ignored).

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

**Milestone 1 (extraction) is essentially already delivered by mech3ax v0.6.1** — the planned RE work is reduced to (a) the cosmetic planes.zbd padding nit (upstream PR candidate) and (b) the deferred anim formats. **Next step: Milestone 2** — Godot 4 .NET project skeleton + importer that reads unzbd output (ZIP/JSON/PNG) from the player's install, starting with one plane mesh + C1 terrain rendered.
