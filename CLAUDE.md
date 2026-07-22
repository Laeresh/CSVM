# CLAUDE.md

## ⚠ Keep this file + `docs/` up to date

**This file is the compact, authoritative index of project context for Claude; deep detail lives in `docs/`.** Update documentation in the same turn as the change it describes:

- Changes to *what the tool does from the outside* (flags, outputs, defaults, algorithms, UI, entry points) → the relevant section **here**.
- Implementation detail, diagnosis narratives, verified gotchas → the module's bullet in `docs/architecture.md`. **Read a module's bullet there before modifying that module** — dead ends and misdiagnoses are recorded so they don't get re-chased.
- Format / reverse-engineering knowledge → `docs/formats/`.
- A way a MEASUREMENT can mislead (non-determinism, an instrument that manufactures its own answer, a masked effect) → `docs/verification.md`, as a transferable rule. A dated `HISTORY.md` entry alone buries it — nobody reads a chronological log before starting work.
- Landed work → a dated entry appended to `docs/HISTORY.md`, plus refresh "Current status" here (current state + next step only — it is not a log).
- Pure refactors with no external effect → usually no update needed. When in doubt, update.

## Project Description

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive, Microsoft): a modern engine that plays the original game using the player's own legally-owned game files. Nothing like this exists yet for Crimson Skies — this is a first-of-its-kind effort.

Charter decided 2026-07-14 (full detail in Claude's project memory):

- **Milestone 1** — complete asset extraction: fill the Crimson Skies gaps in mech3ax (`planes.zbd`, `gamez.zbd`).
- **Milestone 2** — vertical slice: free flight only. One plane, one map (candidate: C1 instant-action arena), arcade controls, original sounds. No AI, objectives, or weapons.
- Long-term direction (not commitment): full campaign remake.

## 🚫 Hard rule: no game assets in version control — ever

Public open-source project under the **XWVM legal model**: the repo ships **code and format documentation only**. Game files, extracted assets, ZBD contents, screenshots of hexdumps containing bulk asset data — none of it gets committed or published. The importer reads the player's own install at runtime. `.gitignore` enforces this for `CrimsonSkiesGame/`, `extracted/`, and `tools/`; keep it that way when adding directories.

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
- **Format reverse engineering:** happens in a fork of [mech3ax](https://github.com/TerranMechworks/mech3ax) (Rust), PR'd upstream to TerranMechworks. Their byte-identical round-trip test harness (extract→repack) is the correctness standard.
- **Flight model:** data-driven approximation — parameterized by plane stats from extracted zrdr reader files, hand-tuned against the original game. No exe decompilation.
- **Division of labor:** Claude writes the Rust parsers, Godot code, and docs; the user reviews, playtests flight feel, and owns upstream/community communication.
- **Git: commit to `main`.** This is a single-developer repo with no PR workflow, so **do not create a branch** when asked to commit — commit straight to `main`. The user will say so explicitly if a particular change should go on its own branch. Pushing is still never automatic: commit when asked, push only when asked.

## Repo layout

- `CrimsonSkies/` — the Godot 4 .NET project (the actual remake; committed). See "Godot project" below.
- `CrimsonSkiesGame/` — the user's retail game install (git-ignored). ZBD archives in `CrimsonSkiesGame/ZBD/` organized as campaign chapters `C1`–`C5`, each with instant action (`IA1`), story missions (`M0x`), multiplayer maps (`MP1`–`3`). Cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored). Populated by `ExtractAssets.ps1` (below), which mirrors the game's ZBD folder structure: top-level `planes.zip` (unzbd of planes.zbd), `zrdr.zip`, `soundsh.zip`/`soundsl.zip`, `interp.json`, `rimage.zip`, plus per-chapter `C1/gamez.zip`, `C1/texture.zip`, `C1/rtexture*.zip`, `C1/zrdr.zip`, and per-mission `C1/IA1/zrdr.zip` etc. The viewer's defaults read `planes.zip`, `C1/gamez.zip`, `C1/texture.zip`, `zrdr.zip`, `soundsh.zip` — but for each default it **prefers the unpacked sibling folder when present** (e.g. reads `extracted/C1/texture/` over `C1/texture.zip`), so running `ExtractAssets.ps1 -Unzip` (or unzipping just the archives you want to grep in the editor) makes the viewer load loose files and skip zip decompression. All four loaders (`GameZ`, `TextureArchive`, `Zrdr`, `SoundArchive`) accept a zip or a directory; an explicit `--gamez=`/`--textures=`/`--zrdr=`/`--sounds=` is used verbatim. **The `rtexture*`/`rimage` archives are NOT loaded, by design (verified Run-2 item 2):** each `rtextureN.zip` is a **downscaled quality tier** of the same texture set, not a hi-res replacement — measured across all 896 C5 textures, `texture` == `rtexture14` (both max-res), `rtexture2` = ¼, `rtexture4/6/8` = ½, and no rtexture file ever exceeds `texture`; so the base `texture.zip` the viewer loads is already the highest resolution. `rimage.zip` is the UI/HUD image set (crosshairs, buttons, cursor, menu splash, briefing thumbnails), no world geometry textures. **`extracted/rof/`** is produced by the separate `ExtractRof.ps1` (not `ExtractAssets.ps1`) and holds the unpacked `.rof` UI archives + `ui_strings.json`; nothing in the Godot project loads it yet.
- `ExtractAssets.ps1` (repo root) — bulk extractor: walks `CrimsonSkiesGame/ZBD`, runs `unzbd cs <mode>` on every ZBD with the right mode for its type (interp→interp/.json; planes+gamez→gamez; soundsh/soundsl→sounds; zrdr→reader; rimage/texture/rtexture*→textures; cam_anim/mis_anim→anim), and writes the output to the mirrored relative path under `extracted/` (basename kept, extension → `.zip`, or `.json` for interp). **Extracts with the fork build since 2026-07-21** (`tools/mech3ax/target/release/unzbd.exe`; plan item 13) — `-Unzbd <path>` overrides it, e.g. back to the pinned `tools/mech3ax-v0.6.1-.../unzbd.exe`, which needs **no code change** because the Godot loaders read either extraction shape (see the `GameZ.cs` bullet). Idempotent (skips outputs newer than their source unless `-Force`); `-Unzip` also expands each `.zip` into a sibling folder; `-Source`/`-Dest` override the roots. unzbd's stderr is captured and judged by exit code (PowerShell 5.1 turns a native exe's stderr into terminating errors under `$ErrorActionPreference = "Stop"`); upstream mech3ax's `object3d transform fail` notes — one per node whose euler angles don't recompose to the stored matrix bit-for-bit, informational since the matrix itself is preserved and preferred — are counted and summarized, not printed (155 on a full run). **`messages.json` is not produced by this script** (it comes from `unzbd cs messages CrimsonSkiesGame/strings.dll`).
- `ExtractRof.ps1` (repo root) — extractor for the **non-ZBD** half of the install (2026-07-20): the `.rof` UI resource archives (`GOSDATA/ASSETS/crimson.rof` + the `crimptch.rof` patch overlay) and the `langui.dll`/`language.dll` Win32 string tables, into `extracted/rof/`. Writes every archive member at its archive path, decodes each custom `.BM` texture to `<name>.png` (greyscale shading map) + `<name>_mask.png` (**the paint region masks** — R/G/B = paint slots 1/2/3), and emits `ui_strings.json` (every UI string joined to its `RESOURCE.H` symbol — this is where the aircraft names + description text live). `-Raw` skips the decoding and the string table, `-Force` re-runs an up-to-date extraction, `-Source`/`-Dest` override the roots. The decode work is an inline C# type (`Add-Type`), so a full run is ~1.5 s. Formats: `docs/formats/rof.md`, `docs/formats/strings.md`.
- `RunDev.ps1` (repo root) — **dev** build + launch helper (console prompts): `dotnet build`, then Godot with the forwarded viewer args. No args = interactive **console** menus (plane roster + chapter), then `--fly`; `--fly`/extra flight flags prompt for whatever's missing; bare `--plane=X` / `--chapter[=X]` static views pass through promptless; `--damage[=…]` is its own static flow — prompts only for the plane, never adds `--fly`/`--chapter` (combined with an explicit `--fly`/`--chapter` it passes through verbatim and the viewer ignores it with a note).
- `RunGame.ps1` (repo root) — **play** entry point (M2.5 item 4): `dotnet build`, then Godot with **no user args**, so the in-game launchscreen (Mode → Chapter → Plane; see `src/UI/LaunchMenu.cs`) shows. Any args you pass are forwarded verbatim, so an explicit content arg (`--fly`/`--stunt`/`--plane=`/`--chapter=`/`--screenshot=`) bypasses the launchscreen and builds directly. No console prompts (unlike RunDev.ps1). **Both Run scripts set `SDL_JOYSTICK_DIRECTINPUT=0`** (respecting a pre-set value) — the controller-disconnect freeze workaround (2026-07-19, see backlog "Drop the SDL_JOYSTICK_DIRECTINPUT=0 workaround"): Godot's bundled SDL hangs the main thread forever when a >255-button DirectInput device (the 8BitDo Ultimate 2 dongle) disconnects; disabling the dinput backend removes those phantom views, real pads keep working via XInput/HIDAPI. Direct editor/exe launches don't get the workaround.
- `tools/` — downloaded binaries (git-ignored): mech3ax v0.6.1, Godot 4.7 .NET editor at `tools/godot/Godot_v4.7-stable_mono_win64/` (`*_console.exe` for CLI use).
- `docs/PLAN-M2-polish.md` — the agreed Milestone 2 polish plan (ordered work items with goal/evidence/approach/verification + status checklist); keep its checklist in sync as items land. **All 8 items done (2026-07-17).**
- `docs/PLAN-M2-polish-2.md` — Milestone 2 polish **Run 2** (planned 2026-07-17 after a grilling session; same format + ground rules): 13 ordered items from NOTES.md's "Polishing Run 2" section + the open Issues bullets. Keep its checklist in sync as items land.
- `docs/PLAN-M2.5-prototype.md` — Milestone 2.5 **"First Prototype"** (planned 2026-07-19 after a grilling session; same format + ground rules): 7 ordered items — stunt mission mode (ia.json `dzones` → sphere detection, original-format marker HUD from targets.json+messages.json, timed scoring + scoreboard), launchscreen (Mode → Chapter → Plane, keyboard/controller), and 2–4-player splitscreen (join via Start, simultaneous plane pick, shared-world race — last three items, explicitly cuttable). **All 7 items landed 2026-07-19.**
- `docs/PLAN-mech3ax-cs-revival.md` — plan (2026-07-20) for `tools/mech3ax` (the user's fork, `tools/mech3ax/`): re-adding Crimson Skies `gamez.zbd`/`planes.zbd` support, which upstream deleted outright in commit `7f592ec` ahead of a ~24-commit RC/MW/PM common-infra refactor (a port onto current `pm`-based code, not a revert), and adding net-new `cam_anim.zbd`/`mis_anim.zbd` support as a fourth variant of the existing `crates/anim` MW/PM/RC container format (unlocks the deferred Run-2 item 9, "animated world vehicles" — train/truck SI-script motion). Two independent tracks; anim recommended first (smaller, additive, unblocks real backlog work; gamez revival doesn't block anything since `tools/` still pins the pre-removal v0.6.1 binary).
- `docs/upstream-pr/` — the **ready-to-open upstream contribution package** (plan item 14, prepared 2026-07-21, nothing pushed): `README.md` (the two independent PR branches in `tools/mech3ax/` — `pr-cs-anim`, `pr-cs-gamez`, both off rc3 — plus split rationale, per-branch verification, and the push commands), `pr-0-discussion.md` (the cheap pre-PR question to upstream: was dropping CS `gamez` bandwidth or architecture?), `pr-1-anim.md` / `pr-2-gamez.md` (ready-to-paste PR bodies, first line = title). **Status 2026-07-21: issue [#3](https://github.com/TerranMechworks/mech3ax/issues/3) posted (with the disclosure follow-up), the anim PR opened, and the gamez PR deliberately held pending upstream's answer on #3** — if that answer is "architectural", it should not be opened at all. **Standing rule (2026-07-21): every outward-facing communication about this work — PR bodies, issues, comments, community writeups — discloses that it was done with the help of Claude Code.** Both PR bodies carry it under the title; commits carry `Co-Authored-By: Claude`; issue [#3](https://github.com/TerranMechworks/mech3ax/issues/3) predates the rule, so the follow-up comment at the bottom of `pr-0-discussion.md` needs posting to that thread.
- `docs/PLAN-anim-rendering-followups.md` — plan (2026-07-21) for 4 independent, separately-sessionable items found chasing the user's C1/IA1 rendering reports: (1) `If`/`Elseif` condition evaluation + `AnimationLod` as a real quality setting per user request, (2) `LightState` + the remaining unacted-on event kinds (`Sound`, `ObjectOpacityState`, `ObjectCycleTexture`, `ObjectAddChild`, `FbfxColorFromTo`, `CameraState`, `ObjectMotion`), (3) mission-spawned entity rosters (`hk_zep`, CTF props — absent unless a roster spawns them, the mirror image of the landed zepstate scenery fix), (4) `texture_scroll` rendering (parsed, unwired — low priority, nothing reported needs it). Each item has Goal/Evidence/Approach/Verify; items 1+2 are a dependent pair (1 alone produces no visible change), 3 and 4 are fully independent. **Items 1, 3 and 4 have landed** (2026-07-21 / 2026-07-22 / 2026-07-22 — item 3's roster premise turned out to be false; the mechanism is the interp boot script, see `docs/formats/interp.md`); item 2 is partially landed, and it is all that is left of this plan.
- `backlog.md` — unscheduled future work: blocked/deferred items (cam_anim extension, upstream PR), feature backlog, open original-game fidelity questions, pointer to the TUNE list. Move items into a plan when scheduled; delete when landed.
- `OriginalScreenshots/` — user-captured reference screenshots from the original game (committed; UI/plane/sky references for fidelity comparisons — contains no bulk asset data). Reference **videos** live in `OriginalScreenshots/Videos/` (git-ignored — large binaries; docs reference them, ask the user if one is missing).
- `docs/HISTORY.md` — chronological Milestone-2 development log (moved out of this file 2026-07-18): every landed change with its verification details. Append new dated entries there when work lands.
- `docs/verification.md` — **how to verify a change in this project, and how the instruments lie.** Read it before measuring anything: the non-deterministic surfaces (the `--freecam` default camera is a random spawn pick per launch), the traps that have each cost real time (a screenshot diff proving nothing because something occludes or fogs the subject; `--perf`'s `script` figure reading ~2.2× real frame time; a regression test never seen to fail), and the standing pre-flight checklist. Distilled from ~90 incidents scattered through `docs/HISTORY.md`; **new verification traps land here, not only in a dated entry.**
- `docs/architecture.md` — deep per-module implementation notes for `CrimsonSkies/src` (the long narratives formerly inline in this file): read a module's bullet before touching it; update it in the same turn as a change.
- `docs/formats/` — the public reader-format reference (Run-2 item 13, completed 2026-07-19): `README.md` index + shared conventions, one page per format family (see the list under "Format gotchas" below). New decodes land with their docs page in the same change.

## Godot project (`CrimsonSkies/`)

Godot 4.7 .NET, C# / net8.0. Build & run:

```
dotnet build CrimsonSkies/CrimsonSkies.sln
tools/godot/.../Godot_v4.7-stable_mono_win64_console.exe --path CrimsonSkies res://scenes/Main.tscn -- --plane=player_bhawk
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
- `src/Mech3/WorldBuilder.cs` — builds a chapter world: placed + partition subtrees, cloud/sky by texture, camera-anchored skydome, edge extender.
- `src/Mech3/MapEdgeExtender.cs` — rolling window of mirrored border tiles + clutter continuing the world past the map edge, per camera.
- `src/Mech3/Clutter.cs` — stamps interp.json clutter templates (trees/bushes) onto matching-textured terrain as billboarded MultiMesh sprites.
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
| `--perf` | log the CPU/GPU frame-time split once a second (the headless profiler stand-in) |
| `--no-pads` | ignore every gamepad — a drifting stick silently ruins a scripted run |
| `--mute` | skip flight audio |

In-flight keys: WASD/arrows pitch+roll, Q/E rudder, Shift/Ctrl throttle, R respawn, P pause, T node-name labels, Tab cycle stunt target, Esc quit. F12 screenshot, F11 print the camera pose as ready-to-paste `--campos=`/`--lookat=`.

### Format gotchas (the ones that bite constantly)

- `nodes.json` `children`/`parent` are **flat list positions**, NOT the `node_index` field (node_index has duplicates).
- Euler `transformation.rotation` composes **R = Ry(y)·Rx(x)·Rz(z)** = Godot's `EulerOrder.Yxz`; when `matrix` is present it wins and is stored **transposed**.
- Coordinates are right-handed Y-up with the nose at **−Z** — Godot's frame exactly; no mirroring, no UV V-flip.
- `meshes.json` has `null` entries — keep them to preserve `mesh_index` alignment.
- Texture name quirks: fixed-width 20-char truncation (prefix-match) and mech3ax duplicate renames (strip `.-N` suffix). Plane skin pixels are in each chapter's `texture.zbd`, not planes.zbd.
- **Plane skins ship UNPAINTED** — they are shading maps carrying paint-region keys, which the engine recolours with the scheme's `paint_color1..3` at load time. Rendering them raw gives the desaturated blue-gray Bloodhawk instead of the original's red. **Implemented 2026-07-20** (`PlanePainter`); the region keys are NOT palette index ranges (that earlier reading is corrected in `docs/formats/paint.md` — the palettes are plain luminance-sorted quantizer output). The remake currently substitutes a hand-authored per-aircraft hue-window table — **superseded but not yet reworked:** the original's real region masks were found 2026-07-20 in `crimson.rof` (per-pattern, per-skin, three per-pixel weight masks summing to 255; `docs/formats/rof.md`), extracted by `ExtractRof.ps1`. Rework is deliberately a separate session. Likewise `*_noselogo`/`*_taillogo`/`*_winglogo` are 16×16 **placeholders**, not artwork — the real decal is one of the numbered 00–49 textures picked by `paint_decalN` (not every plane has all three: no `fir_noselogo`).
- Per-corner `vertex_colors` = **baked lighting** (really an AO/shadow mask — 70% pure white, 30% darker): the world renders fullbright (texture × vertex color); shaded comes out murky-dark. Some polys are baked pure black on purpose (painted shadows). **The multiply is gamma-space (item 6):** the original DX7 engine multiplied texture × vertex color in sRGB space; our linear-space multiply washes out the baked-dark corners, so the fullbright shaders linearise the vertex color first (`SrgbToLinearFn`). On top of the vertex colors the original applies the mission's **SUNLIGHT** (weather.json) as a per-mission brightness — the remake's `csky_world_light` (item 6).
- Polygons carry a signed **draw priority** (v0.6.1 `unk04`, upstream `priority`); equal priorities resolve by **draw order, later wins** (polygon list order within a mesh, flat nodes.json order across nodes). Ignoring either z-fights every decal — SceneBuilder maps both to depth bias.
- Polygon flag `unk2` (upstream `SHOW_BACKFACE`, bit 0) = double-sided; without it the original backface-culls. The visible side is the CCW loop = Godot's *back* face → `cull_front`.
- **That `cull_front` inverts aircraft lighting unless you cancel it (2026-07-20).** Because the visible side is Godot's back face, *every* visible aircraft fragment is back-facing — and Godot negates `NORMAL` on back faces. The file's normals are correct (measured: 0 of 1827 Fury triangles disagree with their winding, no mirrored nodes), but they arrive at the light calculation pointing into the airframe, so every upward surface shades as if lit from below. SceneBuilder's **shaded** path therefore emits `NORMAL = -normalize(...)`; the fullbright world never reads NORMAL and is untouched. Don't "simplify" that minus away.

Full validated format documentation lives in `docs/formats/` (17 pages; **`README.md` is the index + shared reader conventions** — start there): `extraction.md` (which archive types extract, round-trip status, the two extraction JSON shapes), `interp.md` (`.gw` boot scripts + the per-mission world setup), `gamez.md` (GameZ/planes.zbd structure, aircraft trees, damage states, node kinds), `world-structure.md` (chapter worlds, skydome zones, point-sprite lights), `zrdr.md` (the reader archives: three scopes + per-family index), `vehicle.md` (vehicle defs: dynamics/engines, destroyable_parts/injure_anims damage model, the 6-point collision block), `spawns.md` (ia/PLAYER_INIT + campaign mission↔folder map), `missions.md` (stunt `dzones` Danger Zones, targets.json node→string keys, the messages.json string table), `sounds.md` (SETS, player curves, MS-ADPCM), `weather.md` (weather.json blocks, dual color encoding, SUNLIGHT), `anim-definitions.md` (ANIMATION_DEFINITION schema + cam_anim survey), `effects.md` (PUFFER_STATE schema + effects survey), `clutter.md` (interp.json templates), `hud.md` (compass tape + cockpit gauges), `paint.md` (paint schemes/colours/decals — why shipped skins are unpainted key textures), `rof.md` (the `.rof` UI resource archives + the `.BM` texture format carrying the paint region masks), `strings.md` (the `langui.dll` UI string table — aircraft names + descriptions). **Rule: new decodes land with their docs page in the same change.**

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
| `docs/PLAN-M2-polish.md` | M2 polish run 1 (8 items) | ✅ 2026-07-17 |
| `docs/PLAN-M2-polish-2.md` | M2 polish run 2 (13 items) | ✅ 2026-07-19 |
| `docs/PLAN-M2.5-prototype.md` | Stunt mode, launchscreen, splitscreen (7 items) | ✅ 2026-07-19 |
| `docs/PLAN-mech3ax-cs-revival.md` | The fork: anim + gamez/planes support (14 items) | ✅ 2026-07-21 |
| `docs/PLAN-anim-playback.md` | The animation engine (7 items) | ✅ 2026-07-21 |
| `docs/PLAN-anim-rendering-followups.md` | Conditions, lights, world setup, UV scroll, audio (4 items) | ✅ 2026-07-22 |
| **`docs/PLAN-docs-cleanup.md`** | **Shrink this file back to an index (10 items)** | **◐ active — the only one** |

Concretely: the player flies any of 11 aircraft over any of 8 chapter worlds — free flight, stunt mode, or 2–4-player splitscreen racing — launched from an in-game menu, in a livery painted the way the original paints it, over a world that is **animated** (trains, doors, road vehicles, propellers, point lights, ambient sound, UV-scrolled water, and per-mission entity setup). Extraction is complete: every ZBD type this install ships round-trips byte-identically in the fork, and `extracted/` is fork-produced.

**Next step: finish `docs/PLAN-docs-cleanup.md`, then the owed playtests.** The cleanup is mechanical and self-contained. The playtests are the real blocker on calling Milestone 2.5 done, and they need the user at the controls — several need **two controllers**, which this machine does not have. Both lists live in `backlog.md`: "Owed playtests" and "TUNE constants pending playtest".

**Known issues — diagnosed, unscheduled.** Full diagnoses are in `backlog.md` so they are not re-chased:

- **C5 ground z-fighting** — it is **our depth-bias replication**, not the map-edge extender and not entity rosters (both ruled out by measurement). Do **not** simply raise the bias constants: that makes a coarse low-resolution quad win over the detailed night-city ground, which is probably the wrong surface.
- **C3 references `cloud1`/`cloud2`**, which its own `texture.zbd` does not ship — a retail-data gap, true in both extraction trees. The one-line fix is deliberately left to the user because it trades away the magenta "this is our bug" signal for those names.

**Everything else unscheduled** — blocked/deferred items, the feature backlog, open original-game fidelity questions, and the TUNE list — is in `backlog.md`. Keep it updated as items land or get scheduled.
