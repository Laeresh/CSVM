# CLAUDE.md

## ⚠ Keep this file + `docs/` up to date

**This file is the compact, authoritative index of project context for Claude; deep detail lives in `docs/`.** Update documentation in the same turn as the change it describes:

- Changes to *what the tool does from the outside* (flags, outputs, defaults, algorithms, UI, entry points) → the relevant section **here**.
- Implementation detail, diagnosis narratives, verified gotchas → the module's bullet in `docs/architecture.md`. **Read a module's bullet there before modifying that module** — dead ends and misdiagnoses are recorded so they don't get re-chased.
- Format / reverse-engineering knowledge → `docs/formats/`.
- The extraction pipeline, the launch scripts, or the mech3ax fork → `docs/tooling.md`.
- A way a MEASUREMENT can mislead (non-determinism, an instrument that manufactures its own answer, a masked effect) → `docs/verification.md`, as a transferable rule. A dated `HISTORY.md` entry alone buries it — nobody reads a chronological log before starting work.
- Landed work → a dated entry appended to `docs/HISTORY.md`, plus refresh "Current status" here (current state + next step only — it is not a log).
- Pure refactors with no external effect → usually no update needed. When in doubt, update.

**Budget and shape — this file is an index, not a narrative.** It grew to 162 KB (~40k tokens, every session paying for it) precisely because the rule above says to update it in the same turn as each change, and every session appended while nobody owned the total. Three rules keep it from happening again:

- **Budget: CLAUDE.md stays under ~35 KB.** If a change would push it over, the content belongs in `docs/` and this file gets a *pointer* instead. Check with `(Get-Item CLAUDE.md).Length` before adding a paragraph, not after.
- **Shape: a module index entry is ONE line** (~120 chars: path, what the module is, its role). If it needs a second sentence, that sentence is an `docs/architecture.md` edit, not a CLAUDE.md edit. The same goes for any list here that another file already indexes — point at that file rather than restating it.
- **"Current status" is current state + next step ONLY.** Landed work goes to a dated `docs/HISTORY.md` entry and is **removed** from here, not also summarised here. A status section that accumulates finished work is the single biggest way this file regrows.

Precedent: `docs/verification.md` (`d0ad876`) and the `docs/cli.md` / `docs/formats/extraction.md` splits are exactly this move — scattered or bulky content into one purpose-built page, plus a routing line above so it does not scatter back.

**Standing rule — AI-assistance disclosure (decided 2026-07-21).** Every outward-facing communication about this work discloses that it was done with the help of Claude Code: PR bodies, issues, discussion posts, comments, and any community writeup — not only an initial submission. Commits carry a `Co-Authored-By: Claude` trailer. The user owns all upstream/community communication, so this is a constraint on what gets *drafted* for them, not an instruction to post anything.

## Project Description

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive, Microsoft): a modern engine that plays the original game using the player's own legally-owned game files. Nothing like this exists yet for Crimson Skies — this is a first-of-its-kind effort.

**Public home: <https://github.com/Laeresh/CSVM>** (`origin`, branch `main`). The repo is named **CSVM**; use that name in outward-facing text and as the CC-BY attribution target for `docs/formats/`.

Charter decided 2026-07-14 (full detail in Claude's project memory):

- **Milestone 1** — complete asset extraction: fill the Crimson Skies gaps in mech3ax (`planes.zbd`, `gamez.zbd`).
- **Milestone 2** — vertical slice: free flight only. One plane, one map (candidate: C1 instant-action arena), arcade controls, original sounds. No AI, objectives, or weapons.
- Long-term direction (not commitment): full campaign remake.

## 🚫 Hard rule: no game assets in version control — ever

Public open-source project under the **XWVM legal model**: the repo ships **code and format documentation only**. Game files, extracted assets, ZBD contents, screenshots of hexdumps containing bulk asset data — none of it gets committed or published. The importer reads the player's own install at runtime. `.gitignore` enforces this for `CrimsonSkiesGame/`, `extracted/`, and `tools/`; keep it that way when adding directories.

**Licensing (decided 2026-07-22).** Code is **GPL-3.0-or-later** (root `LICENSE`); `docs/formats/` is **CC BY 4.0** (`docs/formats/LICENSE`) so the format knowledge is reusable by permissively-licensed projects. New code files need no per-file header — the root `LICENSE` plus the `README.md` notice covers the repo. The mech3ax fork is **EUPL-1.2** (upstream's license, not a choice) and is a separate tool, not linked into the engine; GPL-3.0 was chosen partly because the EUPL Appendix lists it as compatible, so fork code *could* later be vendored in. Do not relicense any part permissively without checking that chain. `README.md` also carries the not-affiliated-with-Microsoft disclaimer and the AI-assistance disclosure.

## Scratch / probe output

When generating temporary artifacts — probe images, screenshots, rendered
previews, debug dumps, golden-test captures, etc. — always write them into
`./.scratch/` inside this workspace. Create the folder if it doesn't exist.

- Do NOT write scratch output to the OS temp directory
  (e.g. AppData\Local\Temp\claude\...). Files there aren't reachable from
  VS Code's Explorer and clickable links to them don't resolve.
- Use descriptive filenames (e.g. `probe_full.png`, `zbd_texture_dump_01.png`)
  rather than UUIDs, so they're easy to find in the sidebar.
- When you produce such a file, print its workspace-relative path
  (e.g. `./.scratch/probe_full.png`) so it can be opened directly.

## Architecture & key decisions

- **Engine:** Godot 4 .NET (C#). Consumes extraction output via mech3ax / Mech3DotNet (strongly-typed C# wrapper).
- **Format reverse engineering:** happens in a fork of [mech3ax](https://github.com/TerranMechworks/mech3ax) (Rust) at `tools/mech3ax/`. Their byte-identical round-trip test harness (extract→repack) is the correctness standard. **The CS work stays in the fork, not upstream** (upstream dropped CS for maintenance reasons and is dormant) — remotes, branch roles and the sync procedure are in `docs/tooling.md`.
- **Flight model:** data-driven approximation — parameterized by plane stats from extracted zrdr reader files, hand-tuned against the original game. No exe decompilation.
- **Division of labor:** Claude writes the Rust parsers, Godot code, and docs; the user reviews, playtests flight feel, and owns upstream/community communication.
- **Git: commit to `main`.** This is a single-developer repo with no PR workflow, so **do not create a branch** when asked to commit — commit straight to `main`. The user will say so explicitly if a particular change should go on its own branch. Pushing is still never automatic: commit when asked, push only when asked.


## Coding conventions
- Never write braceless control-flow bodies. Always wrap the body of if, else if, else, for, foreach, while, and do in braces, even for a single statement — this prevents dangling-else and merge-conflict bugs.
## Repo layout

One line each — **the extraction pipeline, the launch scripts and the mech3ax fork are in `docs/tooling.md`**; none of it is needed to write engine code.

- `CSVM/` — the Godot 4 .NET project (the actual remake; committed). See "Godot project" below.
- `CrimsonSkiesGame/` — the user's retail install (git-ignored): ZBD archives in `CrimsonSkiesGame/ZBD/` as chapters `C1`–`C5`, each with `IA1` / `M0x` / `MP1`–`3`; cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored), mirroring the game's ZBD structure. Loaders prefer an unpacked sibling folder over its `.zip`. Layout + what is deliberately not loaded: `docs/tooling.md`.
- `ExtractAssets.ps1` — bulk ZBD extractor (`unzbd cs <mode>` per type, fork build, idempotent). Details: `docs/tooling.md`.
- `ExtractRof.ps1` — extractor for the non-ZBD half: the `.rof` UI archives + DLL string tables → `extracted/rof/`. Details: `docs/tooling.md`.
- `RunGame.ps1` / `RunDev.ps1` — play and dev launch scripts (build + Godot; dev one prompts). Details: `docs/tooling.md`.
- `CleanScratch.ps1` — sweeps `.scratch/` artifacts **and finished `.claude/worktrees/` agent worktrees**; `-WhatIf`/`-Force`/`-OlderThanDays`/`-Keep`/`-SkipWorktrees`/`-IncludeDirtyWorktrees`/`-PruneBranches`. Spares backups and dirty worktrees; leaves branches alone by default.
- `tools/` — downloaded binaries (git-ignored): pinned mech3ax v0.6.1, the mech3ax fork, the Godot 4.7 .NET editor.
- `docs/tooling.md` — the extraction pipeline, the launch scripts, and the fork's remotes/branches/sync procedure.
- `docs/architecture.md` — deep per-module implementation notes for `CSVM/src`. **Read a module's bullet before changing it.**
- `docs/formats/` — the public reader-format reference, one page per format family; `README.md` is the index + shared reader conventions.
- `docs/cli.md` — the full per-flag CLI reference (CLAUDE.md keeps only the day-to-day table).
- `docs/verification.md` — how to verify a change here, and how the instruments lie. Read before measuring anything.
- `docs/HISTORY.md` — chronological development log: every landed change with its verification details. Append a dated entry when work lands.
- `docs/plans/` — completed plans, each banner-marked `COMPLETE` with its date; kept for their evidence and recorded dead ends, read as history. **A plan sitting in `docs/` rather than in here is live** — see "Current status" for which is active when more than one is present.
- `backlog.md` — unscheduled work: blocked/deferred items, feature backlog, open fidelity questions, and the TUNE list. Move items into a plan when scheduled; **delete when landed — a `FIXED`/closed entry does not stay here.** Its record belongs in `docs/HISTORY.md`; its traps in `docs/verification.md` or `docs/architecture.md`. **If closing it leaves follow-up work, that follow-up becomes its own new entry with a `⚠ Traps` section** naming the rejected fixes and the misleading instruments — an open thread buried inside a section headed `FIXED` is invisible to anyone scanning for work.
- `OriginalScreenshots/` — user-captured reference shots + videos from the original game. **Git-ignored in full since 2026-07-22** (was committed until then). `docs/` cites these by filename as evidence, so those citations resolve only in the user's local tree — **ask the user if a referenced capture is missing.**

## Godot project (`CSVM/`)

Godot 4.7 .NET, C# / net8.0. Build & run:

```
dotnet build CSVM/CSVM.sln
tools/godot/.../Godot_v4.7-stable_mono_win64_console.exe --path CSVM res://scenes/Main.tscn -- --plane=player_bhawk
```

(First time only: run with `--headless --import` once before running scenes.)

Compact module index — **deep implementation notes, verified diagnoses, and dead ends for every module live in `docs/architecture.md`; read that module's bullet before changing it.**

- `src/Mech3/GameZ.cs` — GameZ extraction loader (zip or dir): nodes/models/materials/textures JSON → C# objects; reads both extraction shapes.
- `src/Mech3/TextureArchive.cs` — texture lookup (zip or dir): resolves the name quirks and classifies each texture's alpha (soft vs hard).
- `src/Mech3/SceneBuilder.cs` — shared GameZ-subtree → MeshInstance3D builder: triangulation, LOD, depth bias, billboards, fog, UV scroll.
- `src/Mech3/PlaneBuilder.cs` — builds one aircraft from its GameZ subtree (shaded, backface-culled); `Repaint` re-liveries it in place.
- `src/Mech3/PaintScheme.cs` — one aircraft livery: pattern + 3 colours + 3 decals, parsed from vehicle.json or drawn at random.
- `src/Mech3/PatternLibrary.cs` — decodes the original's `.BM` paint patterns from the extracted ROF archive; `PatternsFor` lists a plane's liveries.
- `src/Mech3/PlanePainter.cs` — applies a `PaintScheme` to one aircraft: composites skins from the pattern's region masks, swaps the decals.
- `src/Mech3/PropParts.cs` — classifies prop/rotor nodes by name; spin axis + rate from the original anims (props local Z, rotor local Y).
- `src/Mech3/ControlSurfaces.cs` — classifies aileron/elevator/rudder mesh nodes and their hinge axes (local X ailerons/elevators, local Y rudders).
- `src/Mech3/WingLights.cs` — single source of truth for wingtip nav lights: flare node names, glow texture, warm-amber colour, blink period.
- `src/Mech3/WorldBuilder.cs` — builds a chapter world: placed + partition subtrees, cloud deck by map coverage, camera-anchored skydome, edge extender.
- `src/Mech3/MapEdgeExtender.cs` — rolling window of mirrored border tiles + clutter (buildings solid) continuing the world past the map edge, per camera.
- `src/Mech3/Clutter.cs` — stamps interp.json clutter templates onto matching-textured terrain: billboard sprites, plus C2/C5's solid 3D city blocks.
- `src/Mech3/Zrdr.cs` — zrdr extraction reader (zip or dir) + `ZrdrDict`, the key/[values…] view over a reader's alternating list.
- `src/Mech3/Messages.cs` — the game's localized string table: a plain `messages.json` key→value map resolving the `MSG_*` keys missions reference.
- `src/Mech3/CompiledAnim.cs` — reader for the compiled `cam_anim`/`mis_anim` archives: anim defs, sequences/events, lazy SI-script pool.
- `src/Mech3/AnimDefs.cs` — the zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into the same `AnimDefinition` model.
- `src/Mech3/AnimProgram.cs` — merges the compiled + reader defs for one mission, holds `startanims`, resolves SI-script slots.
- `src/Mech3/TextureCycler.cs` — runs the gamez material `cycle` flipbooks (water, surf, wakes, crowds) by swapping `albedo_tex`.
- `src/Mech3/WorldSounds.cs` — `SOUND_NODE` ambient 3D emitters (waterfalls, train, sirens, engines), one pooled player per host node.
- `src/Mech3/WorldLights.cs` — packs the world's `LIGHT_STATE` point lights into the `csky_light_data` texture the fullbright world shader reads.
- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy (span every pad) plus the `--no-pads` switch.
- `src/Mech3/MissionSetup.cs` — parses + applies the per-mission `.gw` interp script deciding which world entities a mission shows.
- `src/Mech3/AnimRuntime.cs` — the animation engine: bootstrap passes, live def instances, event dispatch, motions, conditions, lights, puffers.
- `src/Mech3/WavFile.cs` — pure-C# WAV parser + MS ADPCM→PCM16 decoder (the game's format; Godot can't load it).
- `src/Mech3/SoundArchive.cs` — WAV lookup over a sounds extraction → cached `AudioStreamWav` (forward loop when LOOPED).
- `src/Mech3/SoundDefs.cs` — sounds.json SETS parser: `snd_*` → `SoundDef` (wav, flags, range, volume).
- `src/Flight/PlaneStats.cs` — typed per-plane stats from vehicle/engines/player.json: dynamics, engine sound, destroyable parts.
- `src/Flight/SpawnPoints.cs` — flight spawn from the mission's own zrdr: ia.json `spawn_points`, or objectives.json PLAYER_INIT as fallback.
- `src/Flight/MissionTargets.cs` — mission `targets.json` loader: world-node name → objective display keys, resolved through `Messages`.
- `src/Flight/StuntMission.cs` — Stunt Flying state: ia.json `dzones` → a danger-zone run with completion, clock and splits, one per pilot.
- `src/Flight/HudMetrics.cs` — the one rule for HUD sizing: window height / 1440, damped by `sqrt(paneH/windowH)` for splitscreen panes.
- `src/Flight/MarkerHud.cs` — the stunt objective marker HUD: reticle, screen-edge arrow + o'clock bearing, run status, banners; one per player.
- `src/Flight/StuntScoreboard.cs` — end-of-run results overlay: a Godot-UI panel of per-zone splits, total, and the persisted best time.
- `src/Flight/StuntRace.cs` — splitscreen stunt race bookkeeping: one `Racer` per player, finish placings, standings, rematch reset.
- `src/Flight/StuntRaceBoard.cs` — the race's shared ranked results overlay, on its own full-window CanvasLayer above the splitscreen panes.
- `src/Flight/ScoreStore.cs` — stunt best-time persistence: `user://stunt_scores.json` keyed chapter/mission/plane, saves only faster runs.
- `src/Flight/Weather.cs` — weather.json reader → `WeatherState`: per-zone fog, sunlight, cloud whiteout, wind, precipitation.
- `src/Flight/FlightAudio.cs` — own-plane loops (engine, overspeed whine, rattle) + crash/prop one-shots, with a per-player `MixGain`.
- `src/Effects/Puffer.cs` — data-driven `PUFFER_STATE` billboard-particle emitter: burst, distance-trail, or sustained at-node modes.
- `src/Effects/CloudPuffs.cs` — synthetic ambient cloud field: one alpha-blended MultiMesh of billboards anchored to the CLOUD_COVER band.
- `src/Effects/Precipitation.cs` — weather.json rain/snow: one camera-following MultiMesh of flakes or streaks, self-animating on the GPU.
- `src/Flight/SpectatorCamera.cs` — the `--freecam` observation camera: RMB-look + WASD over the live world, no aircraft, no collision, no roll.
- `src/Flight/FlightModel.cs` — the arcade velocity-vector flight physics: thrust/drag/gravity/lift, stall, per-axis calibrated control rates.
- `src/Flight/PropAnimator.cs` — spins the collected prop/rotor discs about their local axes, throttle-scaled (idle floor 0.4); `--fly` only.
- `src/Flight/ControlSurfaceAnimator.cs` — deflects ailerons/elevators/rudders to an absolute pose from slewed stick input; `--fly` only.
- `src/Flight/WingLightBlinker.cs` — blinks the wingtip flares 0.08 s every 1.5 s, reset off on respawn; `--fly` only.
- `src/Flight/PlaneCollider.cs` — derives 5–8 plane-frame collision boxes from the built model's mesh triangles, with no per-plane data.
- `src/Flight/PlaneDamage.cs` — per-part HP model from vehicle.json `destroyable_parts`; maps struck box + impact point to a data part.
- `src/Flight/DamageVisuals.cs` — flips the torn-skin `pdpN` panels (paired by mesh position) at the data's injure thresholds, plus fire trails.
- `src/Flight/DamageLab.cs` — the viewer's `--damage` slider UI: one HP slider per part driving flight's own DamageVisuals.
- `src/Flight/CrashBreakup.cs` — scatters the plane's `destroyed` subtree on a crash: ballistic tumble, ground-rest, ~10 s burn.
- `src/Flight/CompassTape.cs` — the top-centre heading tape from the game's own HUD textures, drawn as a cylindrical drum seen edge-on.
- `src/Flight/GaugeCluster.cs` — the cockpit dials as HUD (altimeter/speedo/damage), geometry extracted from the plane's `gauges` subtree.
- `src/Flight/FlightController.cs` — the flying-aircraft node: input → FlightModel → transform, chase camera, HUD feeds, collision/crash, respawn.
- `src/UI/MenuInput.cs` — one launchscreen player's input source: keyboard flag + a `Pads` array, edge/auto-repeat `Poll(dt)`.
- `src/UI/SplitScreen.cs` — the splitscreen rig: one SubViewport pane per player (2–4), shared `World3D`, per-player visual-layer band.
- `src/Flight/PlayerRig.cs` — one rendered view's state: camera, SubViewport, HUD parent, visual layer, controller, own sky/deck/puffs.
- `src/UI/LaunchMenu.cs` — the in-game launchscreen: Mode → Chapter → Plane, pad join/lock, then `Launch` into a session.
- `src/UI/LiveryLab.cs` — the `--viewer` livery editor (L): squadron/colour/decal steppers, live `Repaint`, copy-CLI-args.
- `src/UI/MeshLab.cs` — the `--viewer` geometry/shading lab (M): normal lines, smoothing seams, collider boxes, cull/normal overrides.
- `src/UI/NodeLabels.cs` — floating `cs_name` labels over scene nodes (T): Off/Meshes/All, anchored on mesh centres, de-cluttered.
- `src/PlaneViewer.cs` — Main.tscn root: parses the user args, then shows the launchscreen or builds a session (rigs, world, plane, HUD, weather).

### User args (after `--`)

**Flight is the default (2026-07-20 CLI inversion).** Any content arg builds a *flight* unless `--viewer` is present: `--plane=player_fury` flies the Fury and `--chapter=C4` flies over C4, where both used to open a static orbit view. `--viewer` asks for that static inspection view back, and is where the damage / livery / mesh labs live. `--fly` is still accepted and still means exactly this — it is simply redundant now. A bare launch (no content arg) shows the launchscreen.

The day-to-day set. **Every flag, with its full behaviour, is in [`docs/cli.md`](docs/cli.md)** — including the whole `--debug-*` family, the paint overrides, spawn/mission selection, scripted `--hold` input, and manual camera placement.

| Flag | Does |
|---|---|
| `--chapter[=C1]` | which chapter world (`C1`/`C1B`/`C1C`/`C2`/`C2B`/`C3`/`C4`/`C5`); flown by default |
| `--plane=` | which aircraft; comma-separated gives one per splitscreen player |
| `--fly` | free flight (the default): world + skydome + plane + arcade controls |
| `--stunt` | flight + the mission's Danger Zones as timed fly-through objectives; a race with `--players` |
| `--viewer` | the static inspection view; hosts the damage (H), livery (L) and mesh (M) labs |
| `--freecam` | spectator mode: the live animated world, no aircraft, free-flying camera |
| `--players=N` | splitscreen 1–4 in one shared world, one pane/camera/HUD/pad each |
| `--screenshot=<path>` | render a few frames, save PNG, quit — the automated-verification workhorse |
| `--frames=N` / `--shots=N` | warm-up delay before the shot / capture N consecutive frames |
| `--debug-anim` | log every live animation's pose, condition verdicts and sound emitters once a second |
| `--perf` | log the CPU/GPU/**physics** frame-time split once a second (the headless profiler stand-in) |
| `--no-pads` | ignore every gamepad — a drifting stick silently ruins a scripted run |
| `--mute` | skip flight audio |

In-flight keys: WASD/arrows pitch+roll, Q/E rudder, Shift/Ctrl throttle, R respawn, P pause, T node-name labels, Tab cycle stunt target, Esc quit. F12 screenshot, F11 print the camera pose as ready-to-paste `--campos=`/`--lookat=`.

### Format gotchas (the ones that bite constantly)

- `nodes.json` `children`/`parent` are **flat list positions**, NOT the `node_index` field (node_index has duplicates).
- Euler `transformation.rotation` composes **R = Ry(y)·Rx(x)·Rz(z)** = Godot's `EulerOrder.Yxz`; when `matrix` is present it wins and is stored **transposed**.
- Coordinates are right-handed Y-up with the nose at **−Z** — Godot's frame exactly; no mirroring, no UV V-flip.
- `meshes.json` has `null` entries — keep them to preserve `mesh_index` alignment.
- **Weather/horizon zone names are per chapter, not a fixed pair:** C1–C4 ship `zone1`+`zone2`, **C5 ships `zone1`+`zone3`**. Never hardcode the pair — see `docs/formats/weather.md`.
- Texture name quirks: fixed-width 20-char truncation (prefix-match) and mech3ax duplicate renames (strip `.-N` suffix). Plane skin pixels are in each chapter's `texture.zbd`, not planes.zbd.
- **Plane skins ship UNPAINTED** — they are shading maps carrying paint-region keys, which the engine recolours with the scheme's `paint_color1..3` at load time. Rendering them raw gives the desaturated blue-gray Bloodhawk instead of the original's red. **Implemented 2026-07-20** (`PlanePainter`); the region keys are NOT palette index ranges (that earlier reading is corrected in `docs/formats/paint.md` — the palettes are plain luminance-sorted quantizer output). The engine's **real region masks** are in `crimson.rof` — per-pattern, per-skin, three per-pixel weight masks summing to 255 (`docs/formats/rof.md`), extracted by `ExtractRof.ps1` and applied by `PlanePainter` since 2026-07-20. (They replaced an earlier hand-authored hue-window table, which could not paint a region with no hue; that approach is documented as superseded in `docs/formats/paint.md`.) Likewise `*_noselogo`/`*_taillogo`/`*_winglogo` are 16×16 **placeholders**, not artwork — the real decal is one of the numbered 00–49 textures picked by `paint_decalN` (not every plane has all three: no `fir_noselogo`).
- Per-corner `vertex_colors` = **baked lighting** (really an AO/shadow mask — 70% pure white, 30% darker): the world renders fullbright (texture × vertex color); shaded comes out murky-dark. Some polys are baked pure black on purpose (painted shadows). **The multiply is gamma-space (item 6):** the original DX7 engine multiplied texture × vertex color in sRGB space; our linear-space multiply washes out the baked-dark corners, so the fullbright shaders linearise the vertex color first (`SrgbToLinearFn`). On top of the vertex colors the original applies the mission's **SUNLIGHT** (weather.json) as a per-mission brightness — the remake's `csky_world_light` (item 6).
- **Terrain UVs are a mirrored triangle wave, not a sawtooth** — U rises to exactly 1.0 and *folds back* rather than wrapping to 0 (that is the mirror symmetry across C4's river, and how non-seamless textures tile seamlessly). So a surface whose UVs stay in [0,1] must be sampled **CLAMPed**: `repeat_enable` makes the filter wrap at the fold and blend in the texture's opposite edge, which is the C3/C4 hairline seams (fixed 2026-07-22, `SceneBuilder.UvsWithinUnitSquare`). **Per surface only — 54% of surfaces genuinely tile (U reaches 407); a blanket clamp changes 80% of the C5 city.** Full diagnosis in `docs/architecture.md`.
- Polygons carry a signed **draw priority** (v0.6.1 `unk04`, upstream `priority`); equal priorities resolve by **draw order, later wins** (polygon list order within a mesh, flat nodes.json order across nodes). Ignoring either z-fights every decal — SceneBuilder maps both to depth bias.
- Polygon flag `unk2` (upstream `SHOW_BACKFACE`, bit 0) = double-sided; without it the original backface-culls. The visible side is the CCW loop = Godot's *back* face → `cull_front`.
- **That `cull_front` inverts aircraft lighting unless you cancel it (2026-07-20).** Because the visible side is Godot's back face, *every* visible aircraft fragment is back-facing — and Godot negates `NORMAL` on back faces. The file's normals are correct (measured: 0 of 1827 Fury triangles disagree with their winding, no mirrored nodes), but they arrive at the light calculation pointing into the airframe, so every upward surface shades as if lit from below. SceneBuilder's **shaded** path therefore emits `NORMAL = -normalize(...)`; the fullbright world never reads NORMAL and is untouched. Don't "simplify" that minus away.

Full validated format documentation lives in **`docs/formats/`** — 17 pages, one per format family. **`README.md` there is the index + the shared reader conventions; start there** rather than duplicating its table here. **Rule: new decodes land with their docs page in the same change.**

## Format support status

**Extraction is complete.** Every archive type this install ships extracts, and every one
round-trips **byte-identically** in the fork — including `planes.zbd` and the net-new
`cam_anim.zbd`/`mis_anim.zbd`, neither of which upstream mech3ax supports for Crimson Skies.
`extracted/` is fork-produced since 2026-07-21, and `GameZ.cs`/`TextureArchive.cs`/`Zrdr.cs`
read **either** extraction shape, so `ExtractAssets.ps1 -Unzbd <v0.6.1 unzbd>` rolls back with
no code change.

Per-type status, round-trip evidence, the legacy↔unified shape table and the extracted aircraft
inventory: **`docs/formats/extraction.md`**. (mech3ax's own README support matrix is outdated
for CS — do not use it as the reference.)

## Current status / next step

**This section is current state and next step ONLY — it is not a log.** Landed work goes to a dated entry in `docs/HISTORY.md` and is *removed* from here, not also summarised here. That rule is what keeps this file an index; ignoring it is what grew this section to 65 KB — 27 landed-work bullets, every one already recorded in HISTORY, deleted 2026-07-22.

**Where the project is.** Milestones 1, 2 and 2.5 are delivered, and every plan written so far is complete:

| Plan | Scope | Status |
|---|---|---|
| `docs/plans/PLAN-M2-polish.md` | M2 polish run 1 (8 items) | ✅ 2026-07-17 |
| `docs/plans/PLAN-M2-polish-2.md` | M2 polish run 2 (13 items) | ✅ 2026-07-19 |
| `docs/plans/PLAN-M2.5-prototype.md` | Stunt mode, launchscreen, splitscreen (7 items) | ✅ 2026-07-19 |
| `docs/plans/PLAN-mech3ax-cs-revival.md` | The fork: anim + gamez/planes support (14 items) | ✅ 2026-07-21 |
| `docs/plans/PLAN-anim-playback.md` | The animation engine (7 items) | ✅ 2026-07-21 |
| `docs/plans/PLAN-anim-rendering-followups.md` | Conditions, lights, world setup, UV scroll, audio (4 items) | ✅ 2026-07-22 |
| `docs/plans/PLAN-docs-cleanup.md` | Shrink this file back to an index (10 items) | ✅ 2026-07-22 |
| `docs/plans/PLAN-M2-polish-3.md` | M2 polish run 3 (10 items; 3 and 11 closed as disproven) | ✅ 2026-07-22 |

**One plan sits in `docs/`. The active plan is [`docs/PLAN-M3-weapons.md`](docs/PLAN-M3-weapons.md)** (written 2026-07-22) — Milestone 3, weapons and destruction: 44 items in six waves, scope settled with the user. **No item is user-gated** — both user-owned inputs landed 2026-07-22: the gun mount-name table (from which the slot→firepoint binding rule fell out — reverse index order, `W𝑛 → fp(9−2𝑛), fp(10−2𝑛)`), and the `CLUSTER_SIZE` playtest (stock Bloodhawk = 9 HE rockets, 3 per hardpoint → `CLUSTER_SIZE` is rounds-per-slot, `AMMO_LIMIT` a purchase cap). Wave A (research/docs) is the place to start.

Concretely: the player flies any of 11 aircraft over any of 8 chapter worlds — free flight, stunt mode, or 2–4-player splitscreen racing — launched from an in-game menu, in a livery painted the way the original paints it, over a world that is **animated** (trains, doors, road vehicles, propellers, point lights, ambient sound, UV-scrolled water, and per-mission entity setup). Extraction is complete: every ZBD type this install ships round-trips byte-identically in the fork, and `extracted/` is fork-produced.

**Next step: start `docs/PLAN-M3-weapons.md` wave A** — the research/docs items, which create new files and touch no existing module. Highest priority within it is the marker-reference tool (`--dump-markers` + a labelled `--viewer` overlay), because it is what lets the user fill in the gun mount-name column that blocks the whole loadout implementation. Separately, **the owed playtests** remain the real blocker on calling Milestone 2.5 done, and they need the user at the controls — several need **two controllers**, which this machine does not have. Both lists live in `backlog.md`: "Owed playtests" and "TUNE constants pending playtest".

**Known issues — diagnosed, unscheduled.** Full diagnoses are in `backlog.md` so they are not re-chased:

- **C5 / C1B / C3 z-fighting** — it is **our depth-bias replication**, and the measured cause (2026-07-22, third pass) is that `NodeOrderBias` (5e-8) sits **20× below this renderer's depth-resolution floor (~1e-6 of view distance)**, so the cross-node tie-break cannot separate coplanar sibling nodes. Two earlier answers are disproven and recorded so they are not re-chased: it is *not* coarse-sheet-vs-partition-ground, and it is *not* `g4683` fighting its own polygons (that rested on an AABB overlap; the real outlines share edges and overlap by zero area). A per-polygon within-surface tie-break was implemented and moves nothing. Do **not** raise the bias constants — measured, `NodeOrderBias` 2e-6 fixes C1B (31%→2.4%) and makes C5 **worse** (36%→41.7%). Full diagnosis in `backlog.md` + `docs/plans/PLAN-M2-polish-3.md` item 11.
- **C3 references `cloud1`/`cloud2`**, which its own `texture.zbd` does not ship — a retail-data gap, true in both extraction trees. The one-line fix is deliberately left to the user because it trades away the magenta "this is our bug" signal for those names.

**Everything else unscheduled** — blocked/deferred items, the feature backlog, open original-game fidelity questions, and the TUNE list — is in `backlog.md`. Keep it updated as items land or get scheduled.
