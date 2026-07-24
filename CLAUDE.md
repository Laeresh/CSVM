# CLAUDE.md

## ⚠ Keep this file + `docs/` up to date

**This file is the compact, authoritative index of project context for Claude; deep detail lives in `docs/`.** Update documentation in the same turn as the change it describes:

- Changes to *what the tool does from the outside* (flags, outputs, defaults, algorithms, UI, entry points) → the relevant section **here**.
- Module purpose + still-binding constraints, as `⚠` one-liners → the module's `## src/...` entry in `docs/architecture.md` (body ≤ ~8 lines, ~12 for the heaviest). **Read a module's entry there before modifying that module.** Diagnosis narratives do NOT go there — they get a short dated `docs/HISTORY.md` entry; what survives of one is a `⚠` line or a verification.md rule.
- Format / reverse-engineering knowledge → `docs/formats/`.
- The extraction pipeline, the launch scripts, or the mech3ax fork → `docs/tooling.md`.
- A way a MEASUREMENT can mislead (non-determinism, an instrument that manufactures its own answer, a masked effect) → `docs/verification.md`, as a transferable rule. A dated `HISTORY.md` entry alone buries it — nobody reads a chronological log before starting work. Shape: a bold 1–2-line imperative + at most one sentence of measured evidence — no narrative.
- Landed work → a dated entry appended to `docs/HISTORY.md` — a few lines: what landed, how verified, outcome — plus tick the active plan's checklist and **swap** the "Current status" next-step pointer. The status section must be no longer after the landing edit than before it; the description of what landed lives in those two files, never here.
- Pure refactors with no external effect → usually no update needed. When in doubt, update.

**Budget and shape — this file is an index, not a narrative.** It once grew to 162 KB because every session appended while nobody owned the total; three rules keep that from happening again:

- **Budget: CLAUDE.md stays under ~35 KB.** If a change would push it over, the content belongs in `docs/` and this file gets a *pointer* instead. Check with `(Get-Item CLAUDE.md).Length` before adding a paragraph, not after.
- **Shape: a module index entry is ONE line** (~120 chars: path, what the module is, its role). If it needs a second sentence, that sentence is an `docs/architecture.md` edit, not a CLAUDE.md edit. The same goes for any list here that another file already indexes — point at that file rather than restating it.
- **"Current status" holds pointers and item IDs — never a description of landed work, in any tense.** The loophole that keeps regrowing it is present-tense narrative: "C25 removes colliders on death, proven by the col census" reads like current state but is a log line; its home is the plan's item note and `docs/HISTORY.md`, and here the item is an ID at most. The section's fixed shape and permitted edits are spelled out in the section itself; the tripwire is size — over ~15 lines / ~2 KB means history crept back in.

**Standing rule — AI-assistance disclosure (decided 2026-07-21).** Every outward-facing communication about this work discloses that it was done with the help of Claude Code: PR bodies, issues, discussion posts, comments, and any community writeup — not only an initial submission. Commits carry a `Co-Authored-By: Claude` trailer. The user owns all upstream/community communication, so this is a constraint on what gets *drafted* for them, not an instruction to post anything.

## Project Description

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive, Microsoft): a modern engine that plays the original game using the player's own legally-owned game files. Nothing like this exists yet for Crimson Skies — this is a first-of-its-kind effort.

**Public home: <https://github.com/Laeresh/CSVM>** (`origin`, branch `main`). The repo is named **CSVM**; use that name in outward-facing text and as the CC-BY attribution target for `docs/formats/`.

Charter decided 2026-07-14 (full detail in Claude's project memory):

- **Milestone 1** — complete asset extraction: fill the Crimson Skies gaps in mech3ax (`planes.zbd`, `gamez.zbd`).
- **Milestone 2** — vertical slice: free flight only. One plane, one map (candidate: C1 instant-action arena), arcade controls, original sounds. No AI, objectives, or weapons.
- **Milestone 3** — weapons and destruction (scoped 2026-07-22): [`docs/PLAN-M3-weapons.md`](docs/PLAN-M3-weapons.md).
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
- `./.scratch/` is a tmp folder. If anything needs to stay as evidence move it into `./analyis`

## Architecture & key decisions

- **Engine:** Godot 4 .NET (C#). Consumes extraction output via mech3ax / Mech3DotNet (strongly-typed C# wrapper).
- **Format reverse engineering:** happens in a fork of [mech3ax](https://github.com/TerranMechworks/mech3ax) (Rust) at `tools/mech3ax/`. Their byte-identical round-trip test harness (extract→repack) is the correctness standard. **The CS work stays in the fork, not upstream** (upstream dropped CS for maintenance reasons and is dormant) — remotes, branch roles and the sync procedure are in `docs/tooling.md`.
- **Flight model:** data-driven approximation — parameterized by plane stats from extracted zrdr reader files, hand-tuned against the original game. No exe decompilation.
- **Division of labor:** Claude writes the Rust parsers, Godot code, and docs; the user reviews, playtests flight feel, and owns upstream/community communication.
- **Git: commit to `main`.** This is a single-developer repo with no PR workflow, so **do not create a branch** when asked to commit — commit straight to `main`. The user will say so explicitly if a particular change should go on its own branch. Pushing is still never automatic: commit when asked, push only when asked.


## Coding conventions
- Never write braceless control-flow bodies. Always wrap the body of if, else if, else, for, foreach, while, and do in braces, even for a single statement — this prevents dangling-else and merge-conflict bugs.
- Comments state what and why, briefly — never provenance (dates, plan/milestone/item references), never history, never instructions to a reviewer. If a comment's only content is where a change came from, it should not exist.
## Repo layout

One line each — **the extraction pipeline, the launch scripts and the mech3ax fork are in `docs/tooling.md`**; none of it is needed to write engine code.

- `CSVM/` — the Godot 4 .NET project (the actual remake; committed). See "Godot project" below.
- `CSVM/shaders/` — shared `.gdshaderinc` blocks `#include`d by the generated shaders (instance-uniform order, sRGB, fog, lights).
- `CSVM/data/` — **committed** hand-authored engine config (not extracted assets), loaded via `res://`. Holds `stock_loadouts.json` (the 11 planes' stock weapon fit; see `docs/formats/loadouts.md`).
- `CrimsonSkiesGame/` — the user's retail install (git-ignored): ZBD archives in `CrimsonSkiesGame/ZBD/` as chapters `C1`–`C5`, each with `IA1` / `M0x` / `MP1`–`3`; cutscenes are plain MPGs in `CrimsonSkiesGame/GOSDATA/ASSETS/GRAPHICS/MPG/`.
- `extracted/` — extraction output workdir (git-ignored), mirroring the game's ZBD structure. Loaders prefer an unpacked sibling folder over its `.zip`. Layout + what is deliberately not loaded: `docs/tooling.md`.
- `ExtractAssets.ps1` — bulk ZBD extractor (`unzbd cs <mode>` per type, fork build, idempotent). Details: `docs/tooling.md`.
- `ExtractRof.ps1` — extractor for the non-ZBD half: the `.rof` UI archives + DLL string tables → `extracted/rof/`. Details: `docs/tooling.md`.
- `RunGame.ps1` / `RunDev.ps1` — play and dev launch scripts (build + Godot; dev one prompts). Details: `docs/tooling.md`.
- `CleanScratch.ps1` — sweeps `.scratch/` artifacts **and finished `.claude/worktrees/` agent worktrees**; `-WhatIf`/`-Force`/`-OlderThanDays`/`-Keep`/`-SkipWorktrees`/`-IncludeDirtyWorktrees`/`-PruneBranches`. Spares backups and dirty worktrees; leaves branches alone by default.
- `tools/` — downloaded binaries (git-ignored): pinned mech3ax v0.6.1, the mech3ax fork, the Godot 4.7 .NET editor.
- `analysis/` — **committed** read-only analysis scripts + their `FINDINGS.md`, one dir per question. For instruments whose result `docs/` cites, because `.scratch/` is swept. No game data in them, ever.
- `docs/tooling.md` — the extraction pipeline, the launch scripts, and the fork's remotes/branches/sync procedure.
- `docs/architecture.md` — per-module purpose + still-binding constraints for `CSVM/src`, one `##` entry each. **Read a module's entry before changing it.**
- `docs/formats/` — the public reader-format reference, one page per format family; `README.md` is the index + shared reader conventions.
- `docs/cli.md` — the full per-flag CLI reference (CLAUDE.md keeps only the day-to-day table).
- `docs/verification.md` — how to verify a change here, and how the instruments lie. Read before measuring anything.
- `docs/HISTORY.md` — chronological development log: every landed change with its verification details. Append a dated entry when work lands.
- `docs/plans/` — completed plans, indexed in `plans.md` there; each banner-marked `COMPLETE`, kept for evidence and dead ends, read as history. **A plan sitting in `docs/` rather than in here is live** — see "Current status" for which is active.
- `backlog.md` — unscheduled work: blocked/deferred items, feature backlog, open fidelity questions, and the TUNE list. Move items into a plan when scheduled; **delete when landed — a `FIXED`/closed entry does not stay here.** Its record belongs in `docs/HISTORY.md`; its traps in `docs/verification.md` or `docs/architecture.md`. **If closing it leaves follow-up work, that follow-up becomes its own new entry with a `⚠ Traps` section** naming the rejected fixes and the misleading instruments — an open thread buried inside a section headed `FIXED` is invisible to anyone scanning for work.
- `playtest.md` — the consolidated at-the-controls checklist: every owed playtest + TUNE, each with what to look for, the launch command, and what it blocks. Deep evidence stays in `backlog.md`; keep the two in step.
- `OriginalScreenshots/` — user-captured reference shots + videos from the original game (git-ignored). `docs/` cites these by filename as evidence, so those citations resolve only in the user's local tree — **ask the user if a referenced capture is missing.**

## Godot project (`CSVM/`)

Godot 4.7 .NET, C# / net8.0. Build & run:

```
dotnet build CSVM/CSVM.sln
tools/godot/.../Godot_v4.7-stable_mono_win64_console.exe --path CSVM res://scenes/Main.tscn -- --plane=player_bhawk
```

(First time only: run with `--headless --import` once before running scenes.)

Compact module index — **every module's purpose and still-binding constraints live in `docs/architecture.md` as its `##` entry; read that module's entry before changing it.**

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
- `src/Mech3/MarkerRig.cs` — a plane's firepoint/pylon/target rig from planes.zbd: plane-frame positions + co-located mounts; feeds `--dump-markers` and the overlay.
- `src/Mech3/CompiledAnim.cs` — reader for the compiled `cam_anim`/`mis_anim` archives: anim defs, sequences/events, lazy SI-script pool.
- `src/Mech3/AnimDefs.cs` — the zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into the same `AnimDefinition` model.
- `src/Mech3/AnimProgram.cs` — merges the compiled + reader defs for one mission, holds `startanims`, resolves SI-script slots.
- `src/Mech3/TextureCycler.cs` — runs the gamez material `cycle` flipbooks (water, surf, wakes, crowds) by swapping `albedo_tex`.
- `src/Mech3/WorldSounds.cs` — `SOUND_NODE` ambient 3D emitters (one pooled player per host node) + `PlayOneShot`, the fire-and-forget one-shot `SOUND` (destruction/impact audio, D31).
- `src/Mech3/WorldLights.cs` — packs the world's `LIGHT_STATE` point lights into the `csky_light_data` texture the fullbright world shader reads.
- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy (span every pad) plus the `--no-pads` switch.
- `src/Mech3/MissionSetup.cs` — parses + applies the per-mission `.gw` interp script deciding which world entities a mission shows.
- `src/Mech3/AnimRuntime.cs` — the animation engine: bootstrap passes, live def instances, event dispatch, motions, conditions, lights, puffers; `PlayEffectAt`/`ExternalEffect` drive the D32 world-effects runtime.
- `src/Mech3/DestructibleRegistry.cs` — live mutable per-instance HP for `HEALTH>0` anim defs, one pool per `(def,anchor)`; feeds `ANIM_HEALTH` eval + `DAMAGE_SEQUENCE` stages, `Resolve` maps a struck collider back to its instance (weapon damage, C23).
- `src/Mech3/WorldSession.cs` — builds a chapter world + binds its `AnimProgram` (load→WorldBuilder→clutter→bind→sound-prewarm); extracted from `PlaneViewer` for `--anim-lab`.
- `src/Mech3/WavFile.cs` — pure-C# WAV parser + MS ADPCM→PCM16 decoder (the game's format; Godot can't load it).
- `src/Mech3/SoundArchive.cs` — WAV lookup over a sounds extraction → cached `AudioStreamWav` (forward loop when LOOPED).
- `src/Mech3/SoundDefs.cs` — sounds.json parser: SETS `snd_*` → `SoundDef`; `LoadGroups` → `SoundGroup` (the `SOUND_GROUPS` weighted-random destruction sounds, `DYNAMIC_WEIGHTS`).
- `src/Flight/PlaneStats.cs` — typed per-plane stats from vehicle/engines/player.json: dynamics, engine sound, destroyable parts.
- `src/Flight/WeaponDefs.cs` — typed reader over `weapons.json` `BALLISTICS`: 48 `WeaponDef`s (ballistics/damage/ammo/flags + FIRE/FLYOUT/IMPACT bindings); inspect with `--dump-weapons`.
- `src/Flight/Loadout.cs` — `stock_loadouts.json` reader + `Bind` to a built plane: gun groups (independent ammo) + hardpoints, markers→muzzle nodes, turrets inert; inspect with `--dump-loadout`.
- `src/Flight/Projectile.cs` — `ProjectilePool`: the shared-world weapon-fire subsystem — ballistics integration, tracers, muzzle flashes, per-surface impact sound + effect model (water splash instanced at the hit, D30) with a spark fallback, hits damage world destructibles (`DamageSink`, C23); guns tracer-quad, rockets fly the FLYOUT MODEL body; a rocket impact plays its named IMPACT puffer effect via `EffectSink` (D32); `Spawn` into it.
- `src/Flight/SpawnPoints.cs` — flight spawn from the mission's own zrdr: ia.json `spawn_points`, or objectives.json PLAYER_INIT as fallback.
- `src/Flight/MissionTargets.cs` — mission `targets.json` loader: world-node name → objective display keys, resolved through `Messages`.
- `src/Flight/StuntMission.cs` — Stunt Flying state: ia.json `dzones` → a danger-zone run with completion, clock and splits, one per pilot.
- `src/Flight/HudMetrics.cs` — the one rule for HUD sizing: window height / 1440, damped by `sqrt(paneH/windowH)` for splitscreen panes.
- `src/Flight/HudFont.cs` — the game's own HUD bitmap font (`extracted/rimage/5pointhud*.png`): a 5px printable-ASCII atlas, auto-segmented, drawn onto any canvas sized via `HudMetrics`; `--hud-font-test` overlay (`HudFontTest.cs`) proves it.
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
- `src/Flight/SpectatorCamera.cs` — the `--freecam`/`--anim-lab` observation camera: RMB-look + WASD, no roll; `Frame`/`FollowNode` let the lab frame + track an object.
- `src/Flight/FlightModel.cs` — the arcade velocity-vector flight physics: thrust/drag/gravity/lift, stall, per-axis calibrated control rates.
- `src/Flight/PropAnimator.cs` — spins the collected prop/rotor discs about their local axes, throttle-scaled (idle floor 0.4); `--fly` only.
- `src/Flight/ControlSurfaceAnimator.cs` — deflects ailerons/elevators/rudders to an absolute pose from slewed stick input; `--fly` only.
- `src/Flight/WingLightBlinker.cs` — blinks the wingtip flares 0.08 s every 1.5 s, reset off on respawn; `--fly` only.
- `src/Flight/PylonOrdnance.cs` — the rockets under the wings: one FLYOUT-model body per loaded pylon (same asset the round flies), hidden as that pylon's ammo depletes; `--fly` only.
- `src/Flight/PlaneCollider.cs` — derives 5–8 plane-frame collision boxes from the built model's mesh triangles, with no per-plane data.
- `src/Flight/PlaneDamage.cs` — per-part HP model from vehicle.json `destroyable_parts`; maps struck box + impact point to a data part.
- `src/Flight/DamageVisuals.cs` — flips the torn-skin `pdpN` panels (paired by mesh position) at the data's injure thresholds, plus fire trails.
- `src/Flight/DamageLab.cs` — the viewer's `--damage` slider UI: one HP slider per part driving flight's own DamageVisuals.
- `src/Flight/CompassTape.cs` — the top-centre heading tape from the game's own HUD textures, drawn as a cylindrical drum seen edge-on.
- `src/Flight/GaugeCluster.cs` — the cockpit dials as HUD (altimeter/speedo/damage + gun/missile gauges), geometry from the plane's `gauges` subtree.
- `src/Flight/FlightController.cs` — the flying-aircraft node: input → FlightModel → transform, chase camera, HUD, collision/crash, respawn.
- `src/UI/MenuInput.cs` — one launchscreen player's input source: keyboard flag + a `Pads` array, edge/auto-repeat `Poll(dt)`.
- `src/UI/SplitScreen.cs` — the splitscreen rig: one SubViewport pane per player (2–4), shared `World3D`, per-player visual-layer band.
- `src/Flight/PlayerRig.cs` — one rendered view's state: camera, SubViewport, HUD parent, visual layer, controller, own sky/deck/puffs.
- `src/UI/LaunchMenu.cs` — the in-game launchscreen: Mode → Chapter → Plane, pad join/lock, then `Launch` into a session.
- `src/UI/LiveryLab.cs` — the `--viewer` livery editor (L): squadron/colour/decal steppers, live `Repaint`, copy-CLI-args.
- `src/UI/MeshLab.cs` — the `--viewer` geometry/shading lab (M): normal lines, smoothing seams, collider boxes, cull/normal overrides.
- `src/UI/NodeLabels.cs` — floating `cs_name` labels over scene nodes (T): Off/Meshes/All, anchored on mesh centres, de-cluttered.
- `src/UI/MarkerOverlay.cs` — the `--viewer` firepoint/pylon/target overlay (K, `--markers`): coloured gizmos + de-cluttered labels, shared mounts flagged magenta.
- `src/UI/OrbitCamera.cs` — the `--viewer` orbit camera (orbit/zoom/framing), extracted from `PlaneViewer` for `--anim-lab`.
- `src/UI/AnimLab.cs` — the `--anim-lab` debugger: quiet stage, fixed-dt clock, transport panel, def picker, timeline, freecam, click-to-follow; stages placeless on-call defs (the crash) in front of the camera.
- `src/UI/AnimTimeline.cs` — the anim lab's per-sequence timeline: authored event blocks vs runtime-fired ticks (the scheduler-divergence instrument).
- `src/Utils/Config.cs` — dev tuning-override: typed getters over an optional sparse `res://config.json`, else the in-code `const`; `--dump-config` writes a template.
- `src/SessionPaths.cs` — resolves extracted-data paths (per-chapter gamez/texture/zrdr; `PreferUnzipped`); extracted from `PlaneViewer`.
- `src/PlaneViewer.cs` — Main.tscn root: parses the user args, then shows the launchscreen or builds a session (rigs, world, plane, HUD, weather).

### User args (after `--`)

**Flight is the default.** Any content arg builds a *flight* unless `--viewer` is present: `--plane=player_fury` flies the Fury and `--chapter=C4` flies over C4. `--viewer` gives the static inspection view, where the damage / livery / mesh labs live. `--fly` is accepted but redundant. A bare launch (no content arg) shows the launchscreen.

The day-to-day set. **Every flag, with its full behaviour, is in [`docs/cli.md`](docs/cli.md)** — including the whole `--debug-*` family, the paint overrides, spawn/mission selection, scripted `--hold` input, and manual camera placement.

| Flag | Does |
|---|---|
| `--chapter[=C1]` | which chapter world (`C1`/`C1B`/`C1C`/`C2`/`C2B`/`C3`/`C4`/`C5`); flown by default |
| `--plane=` | which aircraft; comma-separated gives one per splitscreen player |
| `--fly` | free flight (the default): world + skydome + plane + arcade controls |
| `--stunt` | flight + the mission's Danger Zones as timed fly-through objectives; a race with `--players` |
| `--viewer` | the static inspection view; hosts the damage (H), livery (L) and mesh (M) labs |
| `--freecam` | spectator mode: the live animated world, no aircraft, free-flying camera |
| `--anim-lab` | the animation debugger: quiet world stage + def playback (`--play-anim=`, `--seed=`) on a fixed-dt clock; a transport button panel, the freecam camera, and click-to-follow |
| `--players=N` | splitscreen 1–4 in one shared world, one pane/camera/HUD/pad each |
| `--screenshot=<path>` | render a few frames, save PNG, quit — the automated-verification workhorse |
| `--frames=N` / `--shots=N` | warm-up delay before the shot / capture N consecutive frames |
| `--debug-anim` | log every live animation's pose, condition verdicts and sound emitters once a second |
| `--perf` | log the CPU/GPU/**physics** frame-time split once a second (the headless profiler stand-in) |
| `--no-pads` | ignore every gamepad — a drifting stick silently ruins a scripted run |
| `--mute` | skip flight audio |

In-flight keys: WASD/arrows pitch+roll, Q/E rudder, Shift/Ctrl throttle, **Space (pad B) fire guns**, **F (pad A) fire rockets** (one per pull), **G (D-pad L) select gun group** (one at a time), **H (D-pad R) select ordnance**, R respawn, P pause, T node-name labels, Tab cycle stunt target, Esc quit. F12 screenshot, F11 print the camera pose as ready-to-paste `--campos=`/`--lookat=`. In `--viewer`: H damage lab, L livery lab, M mesh lab, K marker overlay.

### Format gotchas

**The cross-cutting gotchas that bite constantly live in [`docs/formats/gotchas.md`](docs/formats/gotchas.md)** — flat-position child indexing, the Yxz Euler order, the mirrored-triangle-wave UVs, gamma-space vertex colors, draw priority + subfaces, `cull_front` and the normals minus, unpainted skins, per-chapter weather zone names. **Read it before writing any reader, transform, or shader code.**

Full validated format documentation lives in **`docs/formats/`** — one page per format family. **`README.md` there is the index + the shared reader conventions; start there** rather than duplicating its table here. **Rule: new decodes land with their docs page in the same change.**

## Current status / next step

**Fixed shape — five short paragraphs, ~15 lines / ~2 KB: this rule, where the project is, active plan + wave position, next items, pointers.** When work lands, the only edits allowed here are: advance the wave-position clause, swap the "Next" IDs, delete text. **Adding a sentence about the landed item is forbidden in every tense** — "C25 removes colliders on death" is a log line even though it reads like current state; its home is the plan's item note and `docs/HISTORY.md`, and here the item is an ID at most. If this section is longer after your edit than before it, the edit was wrong. (The weaker version of this rule let the section hit 65 KB once, and 6 KB again by 2026-07-24.)

**Where the project is.** Milestones 1, 2 and 2.5 are delivered (plans indexed in [`docs/plans/plans.md`](docs/plans/plans.md)): 11 flyable aircraft over 8 animated chapter worlds — free flight, stunt mode, or 2–4-player splitscreen, launched from the in-game menu, with original liveries, weather, world animation and sound; extraction is complete and round-trips byte-identically. M3 has since added firing guns and rockets, and world destructibles that take damage, die, lose collision, throw debris and reset. The owed at-the-controls playtests ([`playtest.md`](playtest.md); several need two controllers, which this machine lacks) still gate calling M2.5 done.

**Active plan: [`docs/PLAN-M3-weapons.md`](docs/PLAN-M3-weapons.md)** — Milestone 3, weapons and destruction. **Per-item status (☑/☐, landed notes, decisions, deferrals) is the plan's checklist and item notes — read those, not this section, for what is done and how it was verified.** Position: Wave A complete; Wave B complete (B19/B20 guided flight deferred to M4); Wave C complete; Wave D complete; Wave E in progress (E34, E35 done). Waves A–D done; Wave E (HUD) and Wave F (debug) remain.

**Next: E36** (selected-weapon readout `MSG_HUD_GUNGAUGE`, draws with E34's `HudFont`), then E37 (impact-point reticle).

**Pointers.** The pad-read-on-focus gate is user-vetoable (its own commit; `backlog.md` "Blocked / deferred"). Everything else unscheduled — known issues, deferred items, fidelity questions, the TUNE list — is in `backlog.md`; keep it updated as items land or get scheduled.
