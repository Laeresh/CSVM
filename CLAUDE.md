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

## Format support status (mech3ax, verified 2026-07-14)

| Format | Status |
|---|---|
| `texture.zbd` / `rtexture*.zbd` | ✅ supported |
| `soundsh.zbd` / `soundsl.zbd` | ✅ supported |
| `zrdr.zbd` (reader/mission config) | ✅ supported |
| `interp.zbd` | ✅ supported |
| `planes.zbd` (aircraft models) | ❌ to be reverse engineered (MW3 `mechlib.zbd` is the documented sibling) |
| `gamez.zbd` (world geometry) | ❌ to be reverse engineered (MW3 `gamez.zbd` is documented) |
| `cam_anim.zbd` / `mis_anim.zbd` | ❌ deferred — not needed for free flight |

## Current status / next step

Repo just initialized; no code yet. **Next agreed step:** validate mech3ax prebuilt release binaries against this install's four supported formats, then take a first structured look at `planes.zbd` to size up the RE work.
