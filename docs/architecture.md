# Godot project — per-module implementation notes

**Start at the module index below.** One routing line per module, grouped by namespace. Find the
module there, then read only its entry: `Grep "## src/Mech3/SceneBuilder.cs" -A 12` returns the
whole thing, because the entry shape guarantees it. **Never read this file whole** — it is ~110 KB.

One `## src/...` entry per module in `CSVM/src`. Entry shape: 1–2 sentences of purpose beyond the
index line, then every still-binding constraint or deliberate-design marker as a `⚠` one-liner.
Body ≤ ~8 lines (~12 for the heaviest modules). Entry order is historical, not grouped — the index
is the map, grep is the lookup.

Narratives, diagnoses, and landed-work stories do not live here: they get a short dated entry in
`HISTORY.md`, and git history keeps the rest. Knowledge about the game's data formats belongs in
`docs/formats/`, not here.

⚠ **A new or renamed module updates the index and its entry in the same edit.** Both are in this
  file precisely so they cannot drift apart; `PROJECT_CONTEXT.md` carries only the namespace-level
  map and must not grow a per-module list again.

## Module index

### `src/Mech3/` — extraction readers, world and scene building

Everything that turns the player's install into a live scene: the mech3ax extraction readers, the
GameZ→Godot builders, and the animation runtime that drives the world.

- `src/Mech3/GameZ.cs` — GameZ extraction loader (zip or dir): nodes/models/materials/textures JSON → C# objects, either extraction shape.
- `src/Mech3/TextureArchive.cs` — texture lookup (zip or dir): resolves the name quirks, classifies each texture's alpha (soft vs hard).
- `src/Mech3/SceneBuilder.cs` — shared GameZ-subtree → MeshInstance3D builder: triangulation, LOD, depth bias, billboards, fog, UV scroll.
- `src/Mech3/WorldCollision.cs` — derives every world collider's `Disabled` flag from its owner's tree visibility (+ the fade channel), so hiding anything drops its collision.
- `src/Mech3/PlaneBuilder.cs` — builds one aircraft from its GameZ subtree (shaded, backface-culled); `Repaint` re-liveries it in place.
- `src/Mech3/PaintScheme.cs` — one aircraft livery: pattern + 3 colours + 3 decals, parsed from vehicle.json or drawn at random.
- `src/Mech3/PatternLibrary.cs` — decodes the original's `.BM` paint patterns from the extracted ROF archive; `PatternsFor` lists a plane's liveries.
- `src/Mech3/PlanePainter.cs` — applies a `PaintScheme` to one aircraft: composites skins from the pattern's region masks, swaps decals.
- `src/Mech3/PropParts.cs` — classifies prop/rotor nodes by name; spin axis + rate from the original anims (props Z, rotor Y).
- `src/Mech3/ControlSurfaces.cs` — classifies aileron/elevator/rudder mesh nodes and their hinge axes (X ailerons/elevators, Y rudders).
- `src/Mech3/WingLights.cs` — the one source for wingtip nav lights: flare node names, glow texture, warm-amber colour, blink period.
- `src/Mech3/WorldBuilder.cs` — builds a chapter world: placed + partition subtrees, cloud deck, camera-anchored skydome, edge extender.
- `src/Mech3/MapEdgeExtender.cs` — rolling window of mirrored border tiles + clutter continuing the world past the map edge, per camera.
- `src/Mech3/Clutter.cs` — stamps interp.json clutter templates onto matching-textured terrain: sprites, plus C2/C5's solid 3D city blocks.
- `src/Mech3/Zrdr.cs` — zrdr extraction reader (zip or dir) + `ZrdrDict`, the key/[values…] view over a reader's list.
- `src/Mech3/Messages.cs` — the game's localized string table: the `messages.json` key→value map behind every `MSG_*` key.
- `src/Mech3/MarkerRig.cs` — a plane's firepoint/pylon/target rig from planes.zbd: plane-frame positions + co-located mounts; feeds `--dump-markers`.
- `src/Mech3/CompiledAnim.cs` — reader for the compiled `cam_anim`/`mis_anim` archives: anim defs, sequences/events, lazy SI-script pool.
- `src/Mech3/AnimDefs.cs` — the zrdr front-end: ANIMATION_DEFINITIONS reader files, normalized into one `AnimDefinition` model.
- `src/Mech3/AnimProgram.cs` — merges the compiled + reader defs for one mission, holds `startanims`, resolves SI-script slots.
- `src/Mech3/TextureCycler.cs` — runs the gamez material `cycle` flipbooks (water, surf, wakes) by swapping `albedo_tex`.
- `src/Mech3/WorldSounds.cs` — `SOUND_NODE` ambient 3D emitters (one pooled player per host node) + `PlayOneShot` for destruction/impact audio.
- `src/Mech3/WorldLights.cs` — packs the world's `LIGHT_STATE` point lights into the `csky_light_data` texture the fullbright world shader reads.
- `src/Mech3/MissionSetup.cs` — parses + applies the per-mission `.gw` interp script deciding which world entities a mission shows.
- `src/Mech3/AnimRuntime.cs` — the animation engine: bootstrap, live def instances, event dispatch, motions, conditions, lights, puffers, world effects.
- `src/Mech3/Anim/` — `AnimRuntime`'s motion + light value types (`IAnimMotion` and its four implementations, `AnimLight`, the bind-census enums), split out of `AnimRuntime.cs` into their own files/namespace for size.
- `src/Mech3/Anim/MotionSet.cs` — the live motion collection: the two registration rules, the per-frame sweep, and the pending-bounce predicate the instance walk retires on.
- `src/Mech3/SequenceRunner.cs` — the engine-free sequence interpreter (event clock / LOOP / IF-ELSEIF), extracted behind the 3-member `ISequenceHost` seam; headlessly testable.
- `src/Mech3/DestructibleRegistry.cs` — live per-instance HP for `HEALTH>0` anim defs, one pool per `(def,anchor)`; `Resolve` maps a struck collider back.
- `src/Mech3/WorldSession.cs` — builds a chapter world + binds its `AnimProgram` (load→WorldBuilder→clutter→bind→sound-prewarm); `--node=` slices it to one subtree.
- `src/Mech3/EmptyStage.cs` — the `--stage=empty` test stage: a collidable ground plane under a code-generated grid, standing in for a chapter world.
- `src/Mech3/WavFile.cs` — pure-C# WAV parser + MS ADPCM→PCM16 decoder (the game's format; Godot can't load it).
- `src/Mech3/SoundArchive.cs` — WAV lookup over a sounds extraction → cached `AudioStreamWav` (forward loop when LOOPED).
- `src/Mech3/SoundDefs.cs` — sounds.json parser: SETS `snd_*` → `SoundDef`; `LoadGroups` → the weighted-random `SOUND_GROUPS`.

### `src/Flight/` — the flying aircraft

The plane as a flying, shooting, damageable thing, plus its HUD and stunt mode. Reads plane stats
from the extracted zrdr; owns the arcade physics and everything drawn over the pilot's view.

- `src/Flight/PlaneStats.cs` — typed per-plane stats from vehicle/engines/player.json: dynamics, engine sound, destroyable parts.
- `src/Flight/WeaponDefs.cs` — typed reader over `weapons.json` `BALLISTICS`: 48 `WeaponDef`s; inspect with `--dump-weapons`.
- `src/Flight/Loadout.cs` — `stock_loadouts.json` reader + `Bind` to a built plane: gun groups + hardpoints, markers→muzzle nodes; `--dump-loadout`.
- `src/Flight/WeaponCursor.cs` — pure ammo-slot stepping shared by rockets (H) and gun groups (G): manual select + on-empty auto-advance, engine-free so it unit-tests.
- `src/Flight/Ballistics.cs` — the VELOCITY/ACCELERATION/GRAVITY integration step, shared by `ProjectilePool` and the reticle's projected impact point.
- `src/Flight/ImpactOutcome.cs` — what a weapon×surface hit should do (effect, sound, stand-in, damage) as a value; `Resolve` is pure and engine-free.
- `src/Flight/Projectile.cs` — `ProjectilePool`: the weapon-fire subsystem — ballistics, tracers, flashes, per-surface impact, damage to destructibles.
- `src/Flight/WarningShotCue.cs` — the shipped near-miss accumulator (player.json `warning_shot_*`) + swept-segment/point distance; engine-free so it unit-tests.
- `src/Flight/IncomingFire.cs` — `--incoming`: the near-miss test rig — a phantom shooter on each player's six, so the cue is reachable before anything in the world shoots back.
- `src/Flight/SpawnPoints.cs` — flight spawn from the mission's own zrdr: ia.json `spawn_points`, or objectives.json PLAYER_INIT as fallback.
- `src/Flight/MissionTargets.cs` — mission `targets.json` loader: world-node name → objective display keys, resolved through `Messages`.
- `src/Flight/StuntMission.cs` — Stunt Flying state: ia.json `dzones` → a danger-zone run with completion, clock and splits, one per pilot.
- `src/Flight/HudMetrics.cs` — the one rule for HUD sizing: window height / 1440, damped by `sqrt(paneH/windowH)` for splitscreen.
- `src/Flight/HudFont.cs` — the game's own 5px HUD bitmap font, auto-segmented from `rimage/5pointhud*.png`; `--hud-font-test` proves it.
- `src/Flight/WeaponReadout.cs` — the selected-weapon text readout: gun group + rocket type and live ammo, in the game's own HUD font.
- `src/Flight/ImpactReticle.cs` — the gun aiming pipper: the selected group's ballistic impact point, projected each frame; trails the nose.
- `src/Flight/MarkerHud.cs` — the stunt objective marker HUD: reticle, screen-edge arrow + o'clock bearing, run status, banners; one per player.
- `src/Flight/StuntScoreboard.cs` — end-of-run results overlay: a Godot-UI panel of per-zone splits, total, and the persisted best time.
- `src/Flight/StuntRace.cs` — splitscreen stunt race bookkeeping: one `Racer` per player, finish placings, standings, rematch reset.
- `src/Flight/StuntRaceBoard.cs` — the race's shared ranked results overlay, on its own full-window CanvasLayer above the splitscreen panes.
- `src/Flight/ScoreStore.cs` — stunt best-time persistence: `user://stunt_scores.json` keyed chapter/mission/plane, faster runs only.
- `src/Flight/Weather.cs` — weather.json reader → `WeatherState`: per-zone fog, sunlight, cloud whiteout, wind, precipitation.
- `src/Flight/FlightAudio.cs` — own-plane loops (engine, overspeed whine, rattle) + crash/prop one-shots, per-player `MixGain`.
- `src/Flight/SpectatorCamera.cs` — the `--freecam`/`--anim-lab` observation camera: RMB-look + WASD/QE, no roll; `Frame`/`FollowNode` track an object.
- `src/Flight/FlightModel.cs` — the arcade velocity-vector flight physics: thrust/drag/gravity/lift, stall, calibrated control rates.
- `src/Flight/PropAnimator.cs` — spins the collected prop/rotor discs about their local axes, throttle-scaled (idle floor 0.4); `--fly` only.
- `src/Flight/ControlSurfaceAnimator.cs` — deflects ailerons/elevators/rudders to an absolute pose from slewed stick input; `--fly` only.
- `src/Flight/WingLightBlinker.cs` — blinks the wingtip flares 0.08 s every 1.5 s, reset off on respawn; `--fly` only.
- `src/Flight/PylonOrdnance.cs` — the rockets under the wings: one FLYOUT-model body per loaded pylon, hidden as its ammo depletes; `--fly` only.
- `src/Flight/PlaneCollider.cs` — derives 5–8 plane-frame collision boxes from the built model's triangles, with no per-plane data.
- `src/Flight/PlaneDamage.cs` — per-part HP model from vehicle.json `destroyable_parts`; maps struck box + impact point to a data part.
- `src/Flight/DamageVisuals.cs` — flips the torn-skin `pdpN` panels (paired by mesh position) at the data's injure thresholds, plus fire trails.
- `src/Flight/DamageLab.cs` — the `--damage`/F5 slider UI: one HP slider per part, driving the parked plane's DamageVisuals or the flown plane's real PlaneDamage.
- `src/Flight/CompassTape.cs` — the top-centre heading tape from the game's own HUD textures, drawn as a cylindrical drum seen edge-on.
- `src/Flight/GaugeCluster.cs` — the cockpit dials as HUD (altimeter/speedo/damage + gun/missile), geometry from the plane's `gauges` subtree.
- `src/Flight/FlightController.cs` — the flying-aircraft node: input → FlightModel → transform, chase camera, HUD, collision/crash, respawn.
- `src/Flight/PlayerRig.cs` — one rendered view's state: camera, SubViewport, HUD parent, visual layer, controller, own sky/deck/puffs.

### `src/Effects/` — particle systems

- `src/Effects/Puffer.cs` — data-driven `PUFFER_STATE` billboard-particle emitter: burst, distance-trail, or sustained at-node modes.
- `src/Effects/CloudPuffs.cs` — synthetic ambient cloud field: one alpha-blended MultiMesh of billboards on the CLOUD_COVER band.
- `src/Effects/Precipitation.cs` — weather.json rain/snow: one camera-following MultiMesh of flakes or streaks, self-animating on the GPU.

### `src/UI/` — screens, overlays and the inspection labs

The launchscreen and splitscreen rig, plus the interactive debug labs. Every lab has a scripted
`--debug-*` twin so a finding can be reproduced headlessly — see `docs/cli.md`.

- `src/UI/MenuInput.cs` — one launchscreen player's input source: keyboard flag + a `Pads` array, edge/auto-repeat `Poll(dt)`.
- `src/UI/SplitScreen.cs` — the splitscreen rig: one SubViewport pane per player (2–4), shared `World3D`, per-player visual-layer band.
- `src/UI/LaunchMenu.cs` — the in-game launchscreen: Mode → Chapter → Plane, pad join/lock, then `Launch` into a session.
- `src/UI/LiveryLab.cs` — the `--viewer` livery editor (L): squadron/colour/decal steppers, live `Repaint`, copy-CLI-args.
- `src/UI/MeshLab.cs` — the geometry/shading lab (M): normal lines, smoothing seams, cull/normal overrides; on the parked plane, or on the selection.
- `src/UI/ColliderOverlay.cs` — the collider wireframes (C): every built collision shape drawn, coloured by owner class; needs `--collision` outside flight.
- `src/UI/ClassOverlay.cs` — the colour-by-class overlay (X): every drawn mesh tinted destructible/facade/clutter/scenery, a findable-targets view.
- `src/UI/WeaponLab.cs` — the `--viewer` weapon lab (W): guns from gun groups, hardpoints from pylons, at a stand-in target; `--weapon-test` fires all 48.
- `src/UI/NodeLabels.cs` — floating `cs_name` labels over scene nodes (T): Off/Meshes/All, anchored on mesh centres, de-cluttered.
- `src/UI/MarkerOverlay.cs` — the `--viewer` firepoint/pylon/target overlay (K, `--markers`): coloured gizmos + de-cluttered labels.
- `src/UI/SelectionService.cs` — the shared `--freecam`/`--anim-lab` selection: click-pick + the `cs_name` ancestor ladder, breadcrumb + highlight box.
- `src/UI/NodeLab.cs` — the `--freecam`/`--anim-lab` node lab (N, `--debug-nodelab`): lazy `cs_name` tree, search, frame/hide, dependencies, destructibles.
- `src/UI/WorldDamageLab.cs` — the `--freecam`/`--anim-lab` world damage lab (F5, `--debug-damage`): HP slider + kill/reset on the selection's destructible pool.
- `src/UI/OrbitCamera.cs` — the `--viewer` orbit camera (orbit/zoom/framing), extracted from `GameSession` for `--anim-lab`.
- `src/UI/AnimLab.cs` — the `--anim-lab` debugger: quiet stage, fixed-dt clock, transport panel, def picker, timeline, freecam, follows the selection.
- `src/UI/AnimTimeline.cs` — the anim lab's per-sequence timeline: authored event blocks vs runtime-fired ticks (the scheduler-divergence instrument).

### `src/Utils/` — session-wide services

The things every subsystem depends on: the clock, the log, the seed. Changing one of these changes
determinism repo-wide — read `docs/verification.md` first.

- `src/Utils/Config.cs` — dev tuning-override: typed getters over an optional sparse `res://config.json`, else the in-code `const`.
- `src/Utils/GameClock.cs` — the session sim clock every sim consumer takes dt from: run mode (realtime/fixed), halt + single-step, time scale.
- `src/Utils/Log.cs` — the diagnostic log: 9 categories × 4 levels, `--log=` console filter, always-on full-detail `.scratch/logs/` file sink.
- `src/Utils/ShaderTime.cs` — the `csky_time` global uniform: the clock's GPU twin, replacing `TIME` in every generated shader; wraps at 3600 s.
- `src/Utils/StartupProfile.cs` — the always-on `[perf] startup …` line: every session build split by phase, `total = boot + Σphases + rest + first_frame`.
- `src/Utils/Rng.cs` — the session's one master seed and the ten named subsystem generators every random draw derives from.
- `src/Utils/ScriptedWindow.cs` — Win32-only window hiding for scripted runs; `ScriptedWindow.Hide()` uses `ShowWindow(SW_HIDE)` on the native window.

### `src/Testing/` — the in-engine assertion harness

`--run-tests` and the `--dump-*` probes. The units that need no running engine live in `CSVM.Tests/`
instead.

- `src/Testing/Probes.cs` — the assertion cores behind the `--dump-*`/`--damage-test` reports: report text **and** a verdict, shared with the suites.
- `src/Testing/TestHarness.cs` — `--run-tests`: suite registry, `TestContext`, the PASS/FAIL/SKIP table, JSON report, exit code, engine-error allowlist.
- `src/Testing/Suites.cs` — the 18 registered suites and their golden counts (48 weapon defs, 11 airframes, blast/fuse rules, destructibles, flight envelope, glTF round trip).
- `src/Testing/GoldenShot.cs` — the engine half of the golden-image tripwire: raw-pixel md5 + GPU adapter, printed on every `--screenshot`.
- `src/Testing/ProbeRunner.cs` — the `--dump-*`/`--run-tests`/`--*-test`/`--destroy=` probe wrappers the Launcher and the session node quit into.
- `src/Testing/CaptureDirector.cs` — the `--screenshot=`/`--shots=`/`--frames=` capture state machine + F11/F12, ticked from `_Process`.
- `src/Testing/GltfExporter.cs` — exports the viewer plane subtree to glTF (mesh + current livery + baked damage) for `--export-gltf=`/F10; converts shader skins on a throwaway duplicate.

### `src/Session/` — the launch/session layer

The `Launcher` scene root, the per-launch `GameSession` node, and the low-coupling session-build
clusters they delegate to.

- `src/Session/Launcher.cs` — Main.tscn root: the once-per-process bootstrap (args → paths → log/seed/window), shader-global registration, persistent camera/lighting, launchscreen + menu flow; instantiates a `GameSession` session node per launch.
- `src/Session/GameSession.cs` — the per-launch session node (instantiated by `Launcher`): builds one session — rigs, world, plane, HUD, weather — from its `SessionSpec`; return-to-menu `QueueFree`s it.
- `src/Session/LiveryResolver.cs` — resolves each player's livery against a `SessionSpec`: the paint catalog, the pattern-mask library, and the per-player scheme pick.
- `src/Session/SpawnPicker.cs` — resolves each player's flight spawn against a `SessionSpec`: the shared spawn-list index and the per-player point (or the `--spawn-at=` override).
- `src/Session/PlaneRoster.cs` — pure lookups over a `SessionSpec`'s plane roster: which plane a player flies, and its display name.
- `src/Session/EffectPools.cs` — the `data/effect_pools.json` reader: how many copies of each effect template the stage builds, per ROOT, scaled by player count.
- `src/Session/FlightRigAssembler.cs` — assembles one player's flight rig: painted plane, `FlightController`, loadout/ordnance, HUD instruments, damage visuals, audio, stunt run, spawn, crash runtime.

### Session root and tests

- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy (span every pad) plus the `--no-pads` switch.
- `src/SessionPaths.cs` — resolves extracted-data paths (per-chapter gamez/texture/zrdr; `PreferUnzipped`); extracted from `GameSession`.
- `src/SessionSpec.cs` — the launch args as one immutable, engine-free value: `Parse` parses **and** resolves (closed `SessionMode`, `--det` bundle, placement, `BuildsCollision`), plus the pure arg parsers.

- `CSVM.Tests/` — the xUnit project (`dotnet test`): engine-free reader units on hand-authored fixtures + `extracted/` golden counts, skipped when absent.

## Cross-module conventions

⚠ **Labs are opt-in.** Every inspection lab (SelectionService, NodeLab, WorldDamageLab, LiveryLab,
  NodeLabels, MeshLab, …) builds no UI and changes no pixel until its key or `--debug-*` flag is
  used — the 11 golden screenshots stay byte-identical.
⚠ **Every formatted number uses `CultureInfo.InvariantCulture`.** A German-locale machine renders
  `0,5` and corrupts logs, reports and parsed round-trips (Probes/Log/StuntMission precedent).

## CSVM.Tests/
The xUnit project `dotnet test` runs (net8.0, `ProjectReference` to `CSVM.csproj`): engine-free
reader units (`Zrdr`, `WavFile`, `SoundDefs`, `WeaponDefs`, `Messages`, `SessionPaths`, `GameZ`
transform arithmetic, `MarkerRig`, `AnimDefs`, …) plus golden invariants over `extracted/`.
⚠ **The `SessionSpec*Tests` trio is the launch surface's only per-rule coverage** (84 facts); two
  deliberate defects are asserted as-is and labelled at the fact — do not "fix" one to make a test
  read better.
⚠ `fixtures/` is hand-authored with invented `probe_*` names; **a trimmed piece of a real
  extraction is still a game asset and never gets committed**.
⚠ Golden invariants resolve data via `CSVM_DATA_ROOT`; when absent, `[ExtractedDataFact]` sets
  xUnit `Skip` (never a silent pass). Golden *numbers* commit; golden *content* never does.

## src/Mech3/GameZ.cs
Loads a mech3ax GameZ extraction (zip or unpacked dir): nodes/models/materials/textures JSON into
plain C# objects, reading both the v0.6.1 "legacy" and the fork "unified" shapes (field mapping:
docs/formats/gamez.md); `WorldTransformOf` resolves a node's world transform without building it.
Carries the node's `flags.intersect_surface` as `IntersectSurface` (default true when flags are
absent) — the original's collision-participation flag, honoured by WorldBuilder.NoCollisionNode.
`IsMarkerGizmo(meshIndex)` classifies a mesh as an authoring mark rather than scenery (one flat-
coloured untextured triangle — see docs/formats/world-structure.md); SceneBuilder draws none.
⚠ GameZNode.Index is the flat list position, NEVER the unified JSON `index` (1-based, duplicated);
  child_indices are flat positions too — getting this wrong rebuilds the graph without erroring.
⚠ The unified transform `scale` is deliberately ignored (measured unit on every transformed node).
Carries each model's `flags.lighting`/`flags.fog` as `GameZMesh.Lighting`/`Fog` (default true) —
the original's self-lit and unfogged marks, honoured by SceneBuilder's per-model shader variants.
⚠ ModelType/FacadeMode/TextureScroll are unified-only: null/zero on a legacy tree, SceneBuilder
  falls back to its texture-name heuristic. Reading both shapes keeps a v0.6.1 rollback data-only.

## src/Mech3/TextureArchive.cs
Texture lookup over an unzbd texture zip or unpacked PNG dir; absorbs the stored-name quirks
(20-char truncation prefix match, legacy `.-N` renames, the fork's trailing doubled period — see
docs/formats/gamez.md) and classifies each texture's alpha channel via LastHadAlpha /
LastAlphaIsSoft ("soft" = a 0.5 scissor cutout would erase or shred it; drives blend-vs-scissor).
`Build` is the one construction path — decode, classify, drop-in, mip chain — and `Find` caches
its result; `BuildMipped` hands the same Image to `--dump-mips` un-cached.
⚠ Unresolved names are reported ONCE via plain GD.Print (MissingTextures), never GD.PushWarning —
  Godot .NET prints a full managed stack trace per PushWarning call and buries real errors.
⚠ **Mip levels 1/2 are the archive's authored `_1`/`_2` siblings** (`Mips`, `--mips=`), installed
  over an already-generated chain — so ordering is load-bearing: the alpha-softness read needs raw
  base pixels and runs BEFORE any of it, and a sibling is refused unless it is exactly half/quarter
  the base's size. `MipSource.Generated` restores the pure box filter bit-for-bit (verified: it
  reproduces the pre-2026-08-02 hashes of all three goldens the change moved).
⚠ `TextureDropIn` (the `--tex-override`/`--tex-census` hook, at Find because it is the one resolve
  point) swaps **RGB bytes only** — size, format and alpha stay the original's, so alpha class /
  blend-vs-scissor / silhouette match a normal run (measured: 114,820 px on and off); authored mip
  levels are flattened with the same colour. Census colours are a pure hash of the name, never
  assignment order; ~0.5 % collide — warn per collision, never nudge. Counts are chromaticity-based
  LOWER bounds (docs/cli.md).

## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order
as depth bias (priority × surface rank × node index → polygon offset). `CollidersForMesh`
splits a mesh's colliding geometry into one trimesh per surface class actually present
(water/buildings/untagged, each polygon's own texture deciding) rather than forcing the
whole mesh under one dominant-class vote — a coastal tile is mostly beach by area, so the
area-quorum vote it replaced gave real water polygons on it to `default` outright
(`analysis/surface-classification/`). Each collider-bearing node is registered with
`WorldCollision`, which owns its `Disabled` flag from then on. A `GameZ.IsMarkerGizmo` mesh draws nothing, but its Node3D is
still built with its transform — animations attach puffers and sounds to those nodes by name.
⚠ Instance-uniform block is an ORDERING CONTRACT — every shader on one instance declares the same
  block (csky_instance_uniforms); a shader with NO instance uniform must not take the preamble
  (16-vec4 per-instance buffer cost). The model's `lighting`/`fog` flags therefore select shader
  VARIANTS (and join the material cache keys) rather than adding a uniform: a lit, fogged surface
  keeps byte-identical shader text, so honouring the flags cannot perturb the rest of the world.
⚠ UV scroll reads the `csky_time` global, never Godot's `TIME` — it must keep TIME's 3600 s wrap
  because every install rate (0.07/0.4/0.5/0.7/1.0) × 3600 is a whole number of texture repeats.
⚠ `BuildSubtree` sets the built root's transform from the node's OWN `Local` — a caller slicing a
  nested node must overwrite it with `GameZ.WorldTransformOf` or it lands at its parent's origin.

## src/Mech3/WorldCollision.cs
Owns every `SceneBuilder`-built collider's `Disabled` flag and derives it: enabled exactly while the
owning node is visible in the scene tree and no ancestor is faded out. `Track` (called once per
collider-bearing node as it is built) binds it to the node's `VisibilityChanged` — which Godot
propagates to descendants — plus `TreeEntered`, because a world is assembled and bootstrapped while
still detached, where visibility writes emit nothing. `SetFaded` is the second input: an
`OBJECT_OPACITY_*` fade is a shader parameter visibility knows nothing about.
⚠ Godot visibility is INHERITED, `Disabled` is not. That asymmetry is the whole reason this exists —
  any code writing the two separately re-creates the invisible-wall class (1,484 solid-but-invisible
  colliders across the 8 chapters when it is off, measured); `--run-tests=collision-visibility` is
  the tripwire.
⚠ Scoped to world colliders on purpose: deliberately invisible-but-solid bodies built elsewhere
  (the weapon lab's target, plane hitboxes, `EmptyStage`'s ground) are untracked and keep working.

## src/Mech3/PlaneBuilder.cs
Builds one aircraft from its GameZ subtree (shaded, cullBackfaces: true — interior lattice must be
backface-culled or it paints over the skin), skipping cockpit/destroyed/shadow/*_hook subtrees.
Repaint(scheme) re-liveries the built plane in place; BuildDestroyed builds the wreck subtree with
the plane-root→destroyed transform chain baked in; WingFlares/DamagePanels expose collected nodes.
⚠ Skip pdpN/pcdpN torn panels (or build hidden under spinningProps/damagePanels) but always render
  the pdpN_h healthy twins — skipping all of player_damage_on amputates real airframe sections.
⚠ nitropropN stays hidden in flight: a NON-spinning blur disc overlaid on spinning ones shimmers.
⚠ *blur* disc textures must alpha-blend, never scissor — alpha peaks ~26%, scissor erases them.

## src/Mech3/PaintScheme.cs
One aircraft livery: pattern name + three colours + three decal indices — the paint_* record a
vehicle.json def carries (see docs/formats/paint.md). LoadCatalog keeps one scheme per pattern
name (the 12 shipped patterns); Random() draws a plausible livery when none is given.
⚠ Colour triples are always integer 0-255 — never weather.json's dual float/int encoding.
⚠ Random() is deliberately NOT three independent RGBs (clown planes): identity colour + two trims.
⚠ Index catalogs with RandiRange, never (int)Randi() % n — the uint cast goes negative half the time.

## src/Mech3/PatternLibrary.cs
Decodes the original's .BM paint patterns from extracted/rof/ASSETS/GRAPHICS/<PATTERN>/ (produced
by ExtractRof.ps1; .BM layout in docs/formats/rof.md). PatternsFor(prefix) lists the patterns
shipping skins for one aircraft — a pattern is per plane; Skin() caches per (pattern, skin) so
several aircraft in one session share a decode.
⚠ A missing rof extraction is NOT an error: Load returns an empty library plus one line naming
  ExtractRof.ps1, and everything downstream builds unpainted.

## src/Mech3/PlanePainter.cs
Applies a PaintScheme to one aircraft: composites its skins from the pattern's region masks and
swaps the three decal placeholders. Read docs/formats/paint.md and rof.md first — the composite
formula, the shading-plane choice, and the bottom-up .BM rows are documented there.
⚠ The ZBD skin's alpha is copied onto the composite: SceneBuilder already chose blend-vs-scissor
  from that texture's alpha class, so a substitute must preserve it.
⚠ PrefixFor reads the skin prefix off ANY of the three *_noselogo/_taillogo/_winglogo materials —
  the Firebrand ships no fir_noselogo.
⚠ Built per plane instance and never mutates the shared TextureArchive cache.

## src/Mech3/PropParts.cs
Classifies a plane's propeller/rotor subnodes by name (staticpropN/staticrotorN, nitropropN,
propN/propNb, rotorN/rotorNb) and supplies each spinning kind's local axis + rate — the
XYZ_ROTATION values (deg/s, docs/formats/anim-definitions.md) from plane_props.json (spinprops)
and autogyro.json (agyro_rotors): props spin about local Z, rotors about local Y.
⚠ nitropropN is classified but never spun — no nitro system yet; PlaneBuilder keeps it hidden.
⚠ The perceived rate is a visual TUNE: a blur disc reads as spinning at any smooth rate.

## src/Mech3/ControlSurfaces.cs
Classifies a plane's control-surface mesh nodes + hinge axes: the deflecting node (l/r_aileronN,
l/r_elevatorN, l/r_rudderN, the Fury's l/r_rudder_rotate) hangs under a hinge parent group whose
transform places/orients the hinge line; ailerons/elevators hinge about local X, rudders local Y.
⚠ Parent hinge-group names deliberately do NOT classify — rotating parent and child would double
  the deflection (digits are required on the bare l_rudder form for exactly this reason).
⚠ No zrdr anim defines deflection — the original drives these procedurally, so angles/rates are
  TUNE in ControlSurfaceAnimator, not data.

## src/Mech3/WingLights.cs
Single source of truth for wingtip nav lights: the flare-node predicate (wing_flare1/2), the glow
texture (oil_liteflare), the warm-amber flash colour (0.88, 0.78, 0.36 = wing_light.json's
LIGHT_STATE COLOR) and the blink period (1.5 s = its LOOP SEQUENCE_OFFSET). PlaneBuilder hides and
re-skins the flares; WingLightBlinker flashes them.
⚠ oil_liteflare also skins a few real airframe meshes — scope the additive-billboard treatment to
  the flare NODES by name, never through the texture, or those meshes get recentered/billboarded.

## src/Mech3/WorldBuilder.cs
Builds a chapter world (fullbright): World children + partition-referenced subtrees; skips `horizon`
(`BuildHorizon` makes the camera-anchored skydome, and is the ONE caller that sets
`SceneBuilder.ForceFogged` — every horizon model in every chapter is authored `fog: false`, and
honouring that on a dome that is 2.5x scaled ~22 km out would delete the horizon band; its
`lighting: false` is honoured), `fvol*`, `dzpaths`. `BuildDzPaths` is its
debug-only custom renderer: material-matched gate polygons green/red at 50% alpha, route as an open
white line strip (never a filled or closed polygon).
Splits the overcast deck into
`CloudDeck` (GameSession moves it with the player); hides origin-parked unplaced vehicles.
`NoCollisionNode` exempts three rendered-but-not-solid classes, subtree-inherited: sky/cloud
textures, billboards, and any node with gamez `intersect_surface` false — the original's own
collision flag, false on props/debris/effects/glows and the C3 spiderweb (docs/formats/gamez.md);
honouring it is what lets the plane fly through the web and wreck debris as the original does.
⚠ `HideUnplacedEntities` is HALF the rule — motion targets still at origin (OBJECT_MOTION_FROM_TO)
  need `RestorePlacedEntities`, the other half; a one-shot sweep breaks them.
⚠ `BuildNode` (the `--node=` stage) is deliberately unlike `Build` in three ways, each of which
  would erase the subject: no `SkipWorldNode` filter, no cloud-deck split, no origin-parked
  registration; lookups are on the SOURCE name, never the Godot name — WORLD-8.
⚠ `DetachedWorldAabb` for subtrees not yet in the tree — `GlobalTransform` on a detached node is
  identity and logs an error per call.

## src/Mech3/MapEdgeExtender.cs
Rolling window (`Rings`=5 of 1024 m cells, diffed only on cell crossings) of repeated border tiles +
clutter (grown from `ClutterBuilder.ExportedKinds`) continuing the world past the map edge.
⚠ Repeats the LOCAL BORDER CELL, never the map interior (whole-map tiling brought the airport
  back); `MirrorAxis` clamps to the border cell and alternately reflects copies — OUR seam-free
  construction; the original may plainly repeat (open fidelity question; swapping is one line there).
⚠ Extension sprites carry no collider (matching the map); buildings DO — one lazy `clutter_bld_ext`
  body per cell attaches the shared `KindExport.CollisionShape`; 3D kinds mirror as whole transforms.
⚠ The window is the UNION of all player cameras' neighbourhoods — one focus strands the other pane.

## src/Mech3/Clutter.cs
Stamps the boot-script clutter templates across placed polygons carrying the template's ground
texture, on a fixed world-space X/Z grid of the template period; sprites → one fullbright Y-billboard
MultiMesh per kind, solids → `SceneBuilder.SharedMesh`; the split is `SceneBuilder.ClassifyBillboard`.
The sprite shader takes the decoration model's own `lighting`/`fog` flags as variants (every tree and
bush card in the install is `lighting: false`, so clutter does not dim with the mission SUNLIGHT).
⚠ Sprites are NOT collidable — no tree-destruction anim exists in the install (`spruce_destroy*`
  is the Spruce Goose; docs/formats/clutter.md). Solid decorations ARE collidable.
⚠ Collision shapes are SHARED, never expanded per placement (that costs seconds of BVH build): one
  `ConcavePolygonShape3D` per distinct mesh, `BodyAddShape`d at each placement onto per-region
  `clutter_bld_<cx>_<cz>` bodies. They live on the body RID — a `ShapeOwner*` call would run
  `_update_shapes()` and clear them; `SharedShapeMeta` on the clutter root anchors them against the GC.
⚠ Sprites drop basis + local Y at placement; an upright render is NOT proof the basis is consumed
  (authored bases ≈ identity) — the evidence is docs/formats/clutter.md.

## src/Mech3/Zrdr.cs
Zrdr extraction reader (zip or unpacked dir): `LoadFile`, content-sniffing `LoadMatchingFiles`,
and `ZrdrDict`, the key/[values…] view over a reader's alternating list.
⚠ `LoadFile` accepts both entry namings — v0.6.1 writes `X.json`, the fork writes `X.zrd.json` —
  in both the zip and directory branches.
⚠ `ZrdrDict` COLLAPSES duplicate keys; anim definitions repeat keys meaningfully, so AnimDefs
  walks the raw lists instead.

## src/Mech3/Messages.cs
The game's localized string table: plain `System.Text.Json` over the extracted `messages.json`
(NOT a zrdr reader), a case-insensitive key→value map resolving the `MSG_*` keys missions reference.
`Fill`/`Format` substitute a template's `%1`…`%9` placeholders (the HUD strings' format, E36).
⚠ Degrades, never throws: a missing file yields an empty table, and `Get` returns the raw key for
  an unknown entry (visible, not blank) — display strings are cosmetic.
⚠ `Fill` grammar: `%N` = arg N (missing ⇒ empty), a trailing bang-spec like `!d!` is consumed (the
  arg is already a formatted string), `%%` = literal `%`. Resolve the key via `Get` first, then fill.

## src/Mech3/MarkerRig.cs
A player airframe's weapon marker rig read from planes.zbd GameZ: `Extract` walks a `player_*`
root, accumulating locals down to each `firepoint*`/`pylon*`/`target`, and reports plane-frame
positions + co-located groups (two gun groups on one mount). `Format` prints one dump block per
plane; `PlayerAirframes` is the model→display list. The committed instrument `docs/formats/markers.md`
regenerates from, and the source of truth `--dump-markers` and `UI.MarkerOverlay` share.
⚠ `Classify` requires a numeric suffix, so the AI airframes' bare `firepoint`/`pylon` are excluded;
  only the 11 player roots are walked.
⚠ Co-location is exact-position (1 cm tol) and crosses mirror-pair boundaries — Brigand fp1≡fp4,
  not fp1≡fp2 — so group by position, never by consecutive index.

## src/Mech3/CompiledAnim.cs
Reader for the fork's compiled `cam_anim`/`mis_anim` extraction (zip or dir): typed defs, events,
and the SI-script pool — `Script(index)` parses lazily, ordered by `metadata.json`. Decode facts
(ptr = flat node index, shifted quat labels, half-angle cubics): docs/formats/anim-definitions.md.
⚠ Payloads stay a generic `AnimData` bag, not per-kind DTOs — a new event kind costs this file nothing.
⚠ Degrade, never fail the build: missing archive → null (normal); corrupt archive → log + skip.
⚠ Keep `Parse`'s spline handling: `spline_interp: false` coefficients are garbage that can be
  FINITE — `SiCubic.Eval`'s non-finite guard cannot catch it; degenerate cubics are synthesised
  at parse (so `At(dt)` stays branch-free), and non-finite/zero quaternions are rejected there.

## src/Mech3/AnimDefs.cs
The zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into CompiledAnim's
`AnimDefinition` model (op key SNAKE_CASE→PascalCase IS the compiled tag; unclaimed bodies stay
under `raw`). Exists because compiled archives are incomplete: `zepstate`/`startanims` are reader-only.
⚠ `ParseDef` defaults `AnimName ??= Name` — without it a reader def misses its compiled twin in
  AnimProgram's dedupe key and runs as a second, independently-anchored copy.
⚠ `AddCallTarget` must NOT touch `data["node"]`/`data["name"]` — for CALL_ANIMATION those hold the CALLED animation's name.
⚠ `DAMAGE_SEQUENCE` parses into a sequence literally named `DAMAGE_SEQUENCE` — the magic name
  `AnimRuntime.ApplyDamageStages` invokes (docs/formats/destructibles.md).

## src/Mech3/AnimProgram.cs
Merges the compiled + reader front-ends for one mission — load both, prefer compiled on collision,
keep the remainder — plus `StartAnims`; `ScriptFor` resolves an event slot to its archive SI script.
⚠ `StartAnims` is a LIST — order matters, last write wins (C1/IA1 layers two anims on the same doors).
⚠ The mission zrdr scope is a LIBRARY, not a manifest: a mission-scope reader def applies only if
  the mission's compiled archive contains it (docs/formats/anim-definitions.md); the gate engages
  only when the manifest actually loaded, and skips are recorded in `MissionLibrarySkipped`.
⚠ Not what decides which world ENTITIES a mission shows — that is MissionSetup + interp.

## src/Mech3/TextureCycler.cs
Runs the gamez material `cycle` flipbooks (water, surf, wakes, crowds) by swapping `albedo_tex`;
frames resolve at build time while the TextureArchive is open — an incomplete flipbook stays static.
⚠ `SceneBuilder.RegisterCycle` registers each BUILT material, not the source: the (material,
  priority, rank, sidedness) cache key yields several ShaderMaterials per cycling source.
⚠ Screenshots cannot verify open water (frames differ ~2/255); use `--debug-anim`'s flipbook log.
⚠ The `EFFECTS` reader (`fire1`/`fire2`) is this mechanism bound to a NODE and is NOT wired up —
  it needs OBJECT_ADD_CHILD (docs/formats/effects.md).

## src/Mech3/WorldSounds.cs
`SOUND_NODE` ambient looping 3D emitters: one pooled AudioStreamPlayer3D per live emitter,
following its host's pose per frame. `PlayOneShot(name, worldPos, rng)` is the one-shot `SOUND`
half (D31): fire-and-forget destruction/impact audio, resolving a `SOUND_GROUPS` name to a member
first; the `Sound` anim event calls it.
⚠ Pooled emitters are never parented into world subtrees — AnimRuntime's FindAll memoization forbids runtime reparenting.
⚠ `Prewarm` (SoundNode + one-shot `Sound` names, expanded through `SOUND_GROUPS`) must run BEFORE
  the sound archive closes — the Loader dies with the world build and most events first fire at
  runtime; it decodes quietly (a chapter archive lacking a referenced WAV is normal).
⚠ `FlushOneShots` frees finished one-shot players for the synchronous damage-test harness, which
  pumps no frames so neither the `Tick` sweep nor a deferred `QueueFree` ever runs.

## src/Mech3/WorldLights.cs
Packs the animated world's `LIGHT_STATE` point lights into the 2×N RGBAF texture the fullbright
world shader reads as spill (global `csky_light_data`, loop bounded by `csky_light_count`).
⚠ Not OmniLight3D — the unshaded world ignores dynamic lights, and the flare at each light is
  already gamez Facade geometry (a glow sprite would double-draw); spill is the missing behaviour.
⚠ Packed via BitConverter, never Image.SetPixel/Color — world-metre positions would hit the 0..1 clamp.
⚠ Count 0 must leave the spill term exactly vec3(0.0): an unlit world stays bit-identical to pre-lights.

## src/Pads.cs
Single source of truth for gamepads — every reader goes through it, never `Input.GetConnectedJoypads()`.
Owns the phantom policy (span every pad, never `pads[0]`), `Disabled` (`--no-pads`), the focus gate.
⚠ `Disabled` is set by `--no-pads` AND by the `--det` bundle — every scripted run reads no pad.
⚠ The focus gate is on `For` (the read), NOT `Connected` (the roster) — an empty roster would
  un-join menu players and break `Pads.AssignPads` at session build.
⚠ Every joy read sits in a `Pads.For` loop except `MenuInput.JoinPressed`; `GameSession._menuPads`
  still overrides `AssignPads` when the launchscreen's join flow bound pads itself.

## src/Mech3/MissionSetup.cs
Parses + applies the per-mission `.gw` interp script that decides which world entities a mission
shows; acts on `NodeSetActive`/`DeleteTree`/`Object3DSetScroll`, counts + reports every other verb.
⚠ Read docs/formats/interp.md before extending: order matters and last write wins, a `FindNode`
  matching nothing is NORMAL (never warn), and `DeleteTree` names its own target.
⚠ `Object3DRotate` is unimplemented on purpose — the data's angle unit is ambiguous; guessing would silently mis-pose props.
⚠ The entity half applies as AnimRuntime bootstrap pass 0, before animation state (engine load order).

## src/Mech3/AnimRuntime.cs
The animation engine: bootstrap passes (mission setup, anchored RESET_STATEs, ON_STARTUP,
startanims, a safety net), then dispatch-table event playback; unhandled event kinds are counted,
never fatal. An ON_STARTUP def carrying `EXECUTION_BY_RANGE` defers at bootstrap and starts once,
the first time a player is inside its band (`TickDeferredByRange`, swept on an 8 m position-cell
crossing, never per frame; measures from `PlayerPositions` — the aircraft, not the chase camera,
which trails ~25 m behind). Also hosts the destructible-damage entries (`DamageAt`/`CollideDamageAt`/
`ApplyDamageStages`/`RunDeathSequence`/`ResetDestructible`, fed by `ProjectilePool.DamageSink` and
`FlightController.CollideDamageSink`) and the world-effects runtime (`PlayEffectAt` over a hidden
template stage). Second instances serve per-player crash rigs and the world-effects closure, built
unbound via the `ForEffects`/`ForCrashRig` static factories (construction-only; the caller still
`Bind`s + adds the returned node) — the world runtime stays a plain inline `new AnimRuntime`. The
sequence interpreter (event clock / LOOP / IF-ELSEIF) lives in `SequenceRunner.cs` and the live
motions in `Anim/MotionSet.cs` (`Motions`); this class
satisfies its `ISequenceHost` seam by explicit interface implementation (`Dispatch`,
`EvaluateCondition`, the get-only `OnEventDispatched` hook — off its own public surface).
⚠ A partial opacity on an opaque-shader mesh swaps that instance's surfaces to a fade-twin
  material (`EnsureOpacityPath`/`SceneBuilder.FadeShaderFor`) — per-instance surface overrides
  only; the shared material/mesh caches must never be edited, and opacity 1 removes the override.
⚠ `SetSubtreeActive` writes ONLY `Visible`, and the fade path only `WorldCollision.SetFaded` —
  collision is DERIVED from both. Never write `CollisionShape3D.Disabled` from here again: a
  recursive walk re-solidifies activations landing inside an already-hidden subtree.
`Targets` prefers the compiled symbol table; an index the build skipped falls back to a strictly
anchor-scoped name match (never global) — how a re-anchored exploder template (`genx12`) binds its
meshless `pt*` parameter nodes onto the call-site wreck's same-named pieces (D31).
`Anchors` obeys that same authority: a multi-match NAME narrows to the instance holding the def's
symbol-table ROOT node (`NarrowToSymbolRoot`). The compiler expands one object into a def per
instance but leaves them sharing a NAME — C1's two hangars are both `air_gen`, telling themselves
apart only by their symbol tables — so name matching alone hands every twin every anchor and they
cross-bind, which is how destroying `eairg31` used to explode `eairg32`. It narrows only: a reader
def, an unbuilt index or a root outside every candidate leaves the name match standing.
Every name→node lookup routes through `ResolveScoped` — call anchor, then the def's OWN root, then
global — because staged effect templates reuse node names (`fly_trail1`-`5` is `he_trails` AND
`ap_trails` AND `carnage_trails`) and the unplaced copies sit at the stage origin (`BL-219`).
Puffer emitters key `(name, host[, def])`: def-scoped only where `DefScopedPufferKeys` is set (the
effects runtime — the two damage-stage sputters both declare `black_smoke`; on the world runtime the
collapsed key de-dups C5's six same-node `m_crane_go` spark defs, measured via the c5 golden — see
the `_puffers` field comment before changing this). A `PUFFER_STATE 1` re-assert REVIVES a
SustainEnd'ed emitter (the `puffit` sputter loop cycles 0/1 forever), and `OBJECT_ACTIVE_STATE false`
ends emission under that node (`EndSustainedOn`) — the only *authored* stop a stop-less
`PUFFER_STATE` has (`BL-224`), and NOT expressible as an `IsVisibleInTree` gate the way `TickLights` is, since the
effects stage keeps template roots hidden while their world-space particles show; emission sits at the host's
mesh-bounds centre only when its node origin lies outside them (absolute-modelled subtrees,
WORLD-15 — zero offset, byte-identical, otherwise). Effect templates are **pooled** on the effects runtime (`PooledTemplates`, `BL-225`): the stage holds
`WorldEffectsFactory.EffectPoolSlots` copies of each template, one per slot container carrying
`PoolSlotMeta`, and each `PlayEffectAt` takes the next slot (`NextPooledAnchors`, cursor per template
ROOT name, not per anim name — two defs on one root must not both be handed slot 0). Everything
template-shaped is then slot-scoped through `TemplateRootsFor(def, node)`: which copy is placed
(`PlaceTemplateAt`/`PlaceTemplateOn`), revealed (`ShowTemplate`), tested for a move (`TemplateIsAt`)
and searched for the def's own names (`ResolveInOwnRoot`) — all off the slot the CALL's anchor sits
in, so a nested CALL_ANIMATION stays inside its caller's copy instead of driving all four
`fly_trail*` sets. The cursor wraps: past the pool a call recycles a still-live slot, which is the
old shared-template collapse, counted in `PoolRecycles` and named once per effect.
⚠ The pool is NOT a third keying scheme — puffer keys are unchanged (`DefScopedPufferKeys` on the
  effects runtime, the collapsed `(name, host)` on the WORLD runtime, which has no pool at all: its
  templates are the world's own nodes). Distinct emitters per call fall out of the host node being a
  different node per slot; do not add slot to a key.
`PlayEffectAt(name, point, inputNode, ttl)` carries
the call-site node: it resolves the callee's INPUT_NODE (the sputter emits on, and its `NodeActive`
loop gate reads, the damaged object); `ttl` overrides `EffectTtl` per call (a gun hit passes 0.3 s,
C8) and one deadline is kept per (def, anchor), so a replay's instance is not stopped on the
previous one's deadline; a NodeActive-governed def gets no `EffectTtl` and tears its
resources down when its loop exits (the death swap hides `healthy`), and `ResetDestructible` stops
routed stage effects through `ExternalEffectStop` (a heal never flips `NodeActive`).
An instance whose sequences END also stops the sustained emitters it started
(`FinishEffectInstance`, off the `inst.Finished` branch) — on EVERY runtime since `BL-236`, not just
the effects one; the TTL entry is consumed when there is one rather than being the ticket in. That
is the stop for a def carrying an `ACTIVE_STATE 1` and neither authored stop (the torpedo's ground
fire, C1's refuel tanks). It cannot reach the ambient emitters: theirs are the 619 defs whose
`LOOP {-1}` means the instance never finishes. **Instance-scoped, never sequence-scoped** — a lone
`PufferState` in a one-tick sequence (`part1_trail`) is the debris-trail idiom.
`ShowPlacedTemplates` pairs a staged template root's visibility with the EFFECT's life, not its
instance's: revealed after `Start`, hidden only on teardown (Stop/TTL), because the authored
scale/opacity motions outlive the sequence that launched them — hiding on instance-finish would cut
an explosion ring off mid-expansion (D31). What shows INSIDE the root stays the data's call.
⚠ `_rng` is the runtime's ONE die (`RANDOM_WEIGHT`, `SOUND_GROUPS` picks, crash-debris scatter) —
  every session sets `Seed` (`Rng.Anim`/`Rng.Crash`/`Rng.Effects`); route new dice through it or a
  replay stops being identical. `Reseed()` also clears the sound groups' recency memory, which
  lives outside the RNG.
⚠ The bind degrades silently two ways on a partial world — unanchored (`Anchors() == []`) vs
  target-missing (`_opsUnresolved`) — both leave a still object; the `ReportResolution` bind
  census is the only tell (C1 `--node=hk_zep`: 50 anchored, 763 unanchored, 134 target-missing).
⚠ `MaxRootLift`'s 16-match cap assumes WHOLE-WORLD node counts — a partial `--node=` build drops
  under the cap and anchors phantom defs, so it must set `SuppressRootLift` (measured on C1's
  `ap_radiotwr`: 95 lifted defs / 91 phantom instances vs 1 / 2 with the lift refused).

## src/Mech3/Anim/
`AnimRuntime`'s private nested types promoted to top-level `internal` types in their own
namespace, purely for file size — not an independently-owned subsystem, still driven entirely by
`AnimRuntime`. `IAnimMotion` (`ScriptPlayback`/`SpinMotion`/`FromToMotion`/`OpacityFade`/
`MotionRuntime`), `AnimLight`, and the bind-census `AnchorKind` enum. `MotionSet` shares the
namespace but IS independently owned — its own entry below.
`MotionRuntime`'s `translation_range` is a SPHERICAL launch — `xz` azimuth, `y` elevation, both in
degrees, `initial` the speed (`analysis/object-motion-range/`, decoded 2026-08-01) — and a launch
seeds from the node's authored rest pose, since a shared effect template's children are re-homed by
nothing between calls. `RangeLaunchDirection` is that decode's ONE expression; `ProjectilePool`'s
gun-casing ejection reads the same `gunshell` event through it (INSTR-3). Its `scale` channel is an
OFFSET from unit scale (`1 + initial + delta·u`), unlike the absolute `PoseScale`/`OBJECT_SCALE_STATE`
— 30 of the 45 distinct SCALE events carry a bare `-0.1`, which absolute is a negative scale.
⚠ `RestOf`, `_rng`, `SetSubtreeOpacity` and `NonSingularScale` on `AnimRuntime` are `internal`
  (not `private`) specifically so these motion types can reach them — same-assembly only, no wider
  exposure intended; don't widen further without a reason.
⚠ Every pose-scale write goes through `AnimRuntime.NonSingularScale` (`FromToMotion.Seek`,
  `PoseScale`): the data ends scale channels at exact 0 ("shrink away" — 217 FROM_TOs + 216
  SCALE_STATEs install-wide), and an un-clamped singular basis makes the physics server's
  `affine_inverse` spam native `det == 0` for every StaticBody3D under the node (BL-007).

## src/Mech3/Anim/MotionSet.cs
`AnimRuntime`'s live motions as a module: `Add` (owner stamp + `(Target, Channel)` eviction +
`LaunchCount`), the per-frame `Tick` sweep, `DiscardFor`/`Reset`, and the two predicates the rest of
the runtime asks — `OwesBounce` (the BL-240 retirement hold's own mechanism, which `AnimRuntime.
Retirable` consults) and `HasSpinOn` (the `Loop{-1}` spin re-assert guard, which cannot fold into
`Add` because it must run before `SpinMotion`'s constructor writes `Target.Transform`). Never
constructs a motion — `AnimRuntime` builds them and hands them over.
⚠ `Tick` RETURNS its landings; the caller dispatches them. They must dispatch **between** `Tick` and
  the instance walk — `AnimRuntime.TickMotions` does both in one statement for this reason. Split
  them and an instance held open only by a landed piece is `Finished` with nothing owed, so it
  retires and `FinishEffectInstance` SustainEnds the piece's trail emitter (BL-236).
⚠ `Reset()` clears the list but does NOT zero `LaunchCount` — all four external readers of
  `AnimRuntime.BallisticMotionsLaunched` take a delta across an event.
⚠ A node carries at most ONE motion per `MotionChannel` (`Transform` or `Opacity`) — a transform
  motion and an opacity fade coexist on the same node, but two motions on the same channel evict
  each other (`Add`).
`Node3D`-typed but never dereferenced: every operation is identity comparison, so the behaviour is
engine-free even though the type is not. An off-engine fake can only return `null!` for a target,
which collapses all three identity-keyed rules — the tier stays the in-engine `bounce-launch` suite.

## src/Mech3/SequenceRunner.cs
The engine-free sequence interpreter, extracted from `AnimRuntime` behind the `ISequenceHost` seam.
`SequenceRunner` runs one sequence's event list on a clock (per-event START_TIME gating, LOOP with
authored-count-0 = infinite, IF/ELSEIF/ELSE/ENDIF via a `_branchTaken` stack + nesting-aware `Scan`);
`AnimInstance` holds a definition's concurrent runners and removes them as they finish, and carries
the CALL_SEQUENCE/STOP_SEQUENCE semantics (`CallSequence`/`StopSequence`: halt every matching
runner, else call — decode in `docs/formats/anim-definitions.md`; `AnimRuntime`'s dispatch cases are
thin shims over these). Both are
public so `CSVM.Tests` drives them against a fake host; the host is any `ISequenceHost` (the game's
real one is `AnimRuntime`, tests pass a recorder). Anchors are opaque `Node3D?` pass-through — the
interpreter never dereferences them.
⚠ Behaviour-preserving move only — the `_loopsLeft == -2` sentinel, the `goto case "Elseif"` and the
  256-fires-per-frame guard all LOOK refactorable and are all load-bearing (each a shipped, measured
  bug: the bowl sign's 38% blank frames, frozen traffic loops, the double-polling waterfall). The
  comments carry the measured evidence; do not trim them.
⚠ **An instantaneous LOOP pass costs one `AnimFrame` (1/60 s) of SIM time, never one rendered
  frame** — a `LOOP n` is an authored timer of n frames, so pacing it per frame made every such
  timer scale with the client's hardware. The gate is applied at the FOOT of the advance loop
  (`_frameGatePending`), because the trailing `SetDue()` there re-gates on whatever event control
  flow landed on and silently overwrites a `_due` written in the LOOP case — which is why the
  pre-fix code reached for an early `return`, and that return *was* the frame lock. Below 60 Hz the
  loop catches up within the frame (the 256 guard bounds it); at 1/60 it is one pass per step,
  bit-identical, which is why no `--det` capture moved.
⚠ `OnEventDispatched` is a get-only nullable delegate on the seam ON PURPOSE — the null-conditional
  at the fire site short-circuits the `EventDispatch` construction when no debugger is attached, the
  documented zero-cost contract on the hot dispatch path. Making it a method breaks that.

## src/Mech3/DestructibleRegistry.cs
Live, mutable per-instance HP for the world's destructibles — any `AnimDefinition` with
`HEALTH > 0`. One `Instance` per `(def, anchor)` pair, seeded from the authored `HEALTH`, plus a
coarse healthy/damaged/destroyed `State` and a monotonic `DamageStage`; built during AnimRuntime's
bootstrap, read by `ANIM_HEALTH` eval, escalated by `ApplyDamageStages`, damaged via `DamageAt`.
`Resolve(struck)` maps a raycast-hit node back to its instance. Schema: docs/formats/destructibles.md.
⚠ Keyed per `(def, anchor)`, NOT per def — a wildcard NAME binds many node groups, each an
  independent pool (one tower's damage must not touch its siblings).
⚠ `PoolsOn(node)` pairs with `Resolve` (which names the one pool a hit reaches) — the pools
  `Resolve` does not name cannot be damaged at all.
⚠ `Resolve` walks the WHOLE parent chain and takes the nearest COMPILED anchor, not the first hit —
  a reader wildcard can grab an inner node while the compiled def owns the real DAMAGE_SEQUENCE.

## src/Mech3/WavFile.cs
Pure-C# WAV parser with an MS ADPCM→PCM16 decoder (`DecodeMsAdpcm`), no Godot dependencies —
Godot cannot load the game's WAV format (see `docs/formats/sounds.md`).

## src/Mech3/SoundArchive.cs
WAV lookup over a soundsh/soundsl extraction (zip or dir), decoded through `WavFile` into cached
`AudioStreamWav`s; `Find(name, looped)` marks the stream as a forward loop when asked.
⚠ `Find(…, warn: false)` is the speculative bulk-decode path (the sound prewarm): a per-chapter
  archive legitimately lacks WAVs the program can reference, so a "not found" there is quiet — the
  authoritative "silent for the session" report is at the point of use, not here.

## src/Mech3/SoundDefs.cs
sounds.json SETS parser: `snd_*` name → `SoundDef` (wav name, flags, range, volume); the entry
grammar and flag/key meanings are in `docs/formats/sounds.md`. `LoadGroups` parses the sibling
`SOUND_GROUPS` block into `SoundGroup`s — the weighted random destruction/impact sounds a one-shot
`SOUND` event resolves through (`air_mixed_exp_sg` → `snd_exp_hit*`).
⚠ `SoundGroup.Pick(rng)` is weighted-random with a recency scalar: `DYNAMIC_WEIGHTS factor` (0.5)
  halves the last pick's weight so a variant does not repeat back-to-back. Pass the runtime's
  seedable `_rng` (a replay must be deterministic), not `GD.Randf`.
⚠ That recency memory (`_last`) is mutable state OUTSIDE the RNG, so re-seeding a generator alone
  does not replay a pick sequence — `ResetRecency()` exists for exactly that, and whoever re-seeds
  calls it (`AnimRuntime.Reseed` → `WorldSounds.ResetGroupRecency`). A fresh session is safe without
  it only because `LoadGroups` parses new objects per session.
⚠ VO dialogue chains (`snd_assignments`, `snd_HI1*`) contribute no weighted member and are skipped;
  music `*_sg` groups parse but no `SOUND` event names them.

## src/Flight/WeaponDefs.cs
Typed reader over the shared `weapons.zrd.json` `BALLISTICS` block — 48 `WeaponDef`s (guns /
rockets / ordnance) keyed by `wep_*`, plus the `NO_AMMO_WARNING` empty-clip sound. Ballistics,
damage, allotment, the class flags, the specials, and the `FIRE`/`FLYOUT`/`IMPACT` bindings
(`IMPACT` keyed by `SurfaceClass`); `DESC` resolved through `Messages`. Modelled on PlaneStats.
Schema: docs/formats/weapons.md. Verify/inspect with `--dump-weapons`.
⚠ Flags (`CANNON`/`ROCKET`/`HIGH_EXPLOSIVE`/…) are `KEY,null` in the data — `ZrdrDict` bare-flag
  handling makes them present-but-empty, so `Has` is the test; a valued struct (`BEEPER`/`TANGLER`)
  is `Has`+`Dict`.
⚠ `IMPACT` is walked as raw class/value pairs, not via `ZrdrDict` — a null class value (`enemy`,
  "no effect on that surface") must be skipped, not read back as an empty binding.
⚠ `UnhandledKeys` is a tripwire: empty for this install (asserted by `--dump-weapons`); non-empty
  means the data grew a key `KnownKeys` hasn't learned — update the reader, don't ignore it.

## src/Flight/Loadout.cs
Two layers over `CSVM/data/stock_loadouts.json`. `StockLoadouts.Load` parses the file (default
`res://data/`) into per-plane `LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves
each gun slot's markers to live muzzle `Node3D`s and its caliber+ammo to a `WeaponDef` (via
`GunWeaponId` = `wep_{N+k}`), and each hardpoint to its `pylon`, yielding `GunGroup`s (independent
ammo counters from `CLUSTER_SIZE`) + `Hardpoint`s. Turret slots bind but `IsTurret` (inert, M4).
Schema: docs/formats/loadouts.md. Verify/inspect with `--dump-loadout`.
⚠ A missing marker is a LOUD throw naming plane/slot/marker — never a silent skip (a silent one
  fires a gun from nowhere). Markers resolve by `cs_name` meta from the built tree, like MarkerOverlay.
⚠ Gun ammo is per group (Balmoral's two .50s carry 2000 each); rocket ammo is per pylon
  (`CLUSTER_SIZE` each, total = pylons × that) — A9. Config lives at `res://`, NOT under `--data-root`.

## src/Flight/WeaponCursor.cs
The ammo-slot selector as pure index math, shared by rocket hardpoints (H, `_selectedPylon`) and
firable gun groups (G, `_gunSel`), lifted out of `FlightController` so it unit-tests without a live
node (`WeaponCursorTests`). `NextArmed` is the firing cursor — the selected slot while it has rounds,
else the next armed slot forward-wrapping (`-1` when all empty); `NextSelectable` is where the manual
selector moves — the next armed slot strictly after the cursor, skipping empties. Each slot is its own
position regardless of ordnance/weapon type, so H cycles even a uniform loadout (all 11 stock planes
carry one hardpoint type). `FlightController` owns the two cursor fields and the gauge `Selected`
feeds and advances each cursor the instant its slot empties (in `UpdateRockets`/`UpdateGuns`); this
file is stateless.

## src/Flight/Ballistics.cs
The VELOCITY/ACCELERATION/GRAVITY integration every round steps with: a static, Godot-`Node`-free
class with `Step` (one round's per-frame advance, mutating pos/vel in place — `ProjectilePool.SimStep`
owns the surrounding raycast/fuse-test loop and its own `dt`) and `March` (the reticle's whole capped
walk — range cap and 4096-iteration bound included — called once per frame by
`FlightController.BallisticImpactPoint` with its own fixed `dt`). Extracted so the two callers cannot
silently diverge; each still owns its own step size.
⚠ Both callers integrate at a different `dt` — `SimStep` the caller's sim step, `March` a hard-coded
  `1/120 s` — and that is settled, not an oversight to "fix" in passing: of the 48 weapons only
  `wep_04`/`25`/`26`/`27` carry a non-zero `ACCELERATION`, none carries a non-zero `GRAVITY`, and no
  gun group can resolve any of the four (a gun is caliber + ammo → `wep_30..73`). Every marched
  round is therefore a straight line, on which the step size cannot move the endpoint, and a fixed
  step keeps the reticle from twitching with the frame rate. `BallisticsTests` guards the census.

## src/Flight/ImpactOutcome.cs
"What should happen when this weapon hits this surface" as a value — `EffectName` (the class's
`ANIMATION`, else its `SURFACE_ANIMATION`), `Sound`, the `ImpactStandIn`, `Damage`/`BlastRadius`
(+ `HasBlastDamage`) — plus the pure static `Resolve` that computes it from a `WeaponDef` and a
`SurfaceClass`. No Godot type, no scene, no sink, no sound archive, so the dispatch's one decision
is readable by a unit test; `ImpactOutcomeTests` is that test, including a suite over all 48
shipped weapons × the three reachable surfaces asserting the *rule* (an effect or a stand-in but
never neither; a resolved sound names either a `SoundDefs` entry or a `SOUND_GROUPS` name;
`HasBlastDamage` matches the raw damage/radius fields) rather than a table of expected per-weapon
outcomes. `ProjectilePool.Impact` classifies the
surface and calls it, then `Apply` performs the result; `ProjectilePool.HasBlastDamage` is the
weapon-level spelling of the same `HasBlastDamage` rule, so the rule exists once.
⚠ `modelResolved` and `hasEffectsRuntime` are **inputs**, not things `Resolve` discovers: whether
  the effect name is a real gamez node needs the chapter scene, and whether a world-effects runtime
  exists is a fact about the caller (a scene-less pool takes the explosion stand-in). Passing a
  guess for either silently changes which stand-in a caller draws.
⚠ The stand-in ladder's order is load-bearing and mirrors `Impact`'s branch chain exactly —
  model ▸ explosion (non-gun, no runtime) ▸ dirt debris (Default) ▸ ricochet (Buildings + gun) ▸
  spark. Reordering it gives a weapon a different look.
⚠ `Player`/`Enemy` get no case of their own — unreachable in M3, they read off the table and fall
  to the spark like any unbound class. Do not author behaviour for them; and note the reader drops
  an all-null class entry, so "present but empty" and "absent" are the same thing here.
⚠ `Quicksand` is unreachable too, for a different reason than `Player`/`Enemy`: `ProjectilePool
  .ClassifySurface` and `SceneBuilder.ClassifySurface(string?)` only ever stamp a collider `water`
  or `buildings`, defaulting everything else — including quicksand terrain — to `Default`
  (`WeaponLab.SurfaceNames` agrees: three classes, not four). A weapon's `quicksand` IMPACT entry
  is still real data the reader parses correctly; `Resolve` is just never called with it. `B5`'s
  suite runs the 48 weapons across `{Default, Water, Buildings}` only — a fourth case would be
  invented coverage.

## src/Flight/Projectile.cs
`ProjectilePool` — the shared-world weapon-fire subsystem: a fixed pool of projectiles integrated
with `Ballistics` (VELOCITY/ACCELERATION/GRAVITY, expiring at RANGE), plus tracer streaks,
muzzle flashes, and the per-surface IMPACT sound + effect model. Per-class impact looks (A2): a
water hit instances the authored splash model and plays its def's own scale curves for the 2 s run
(`AdvanceSplash` — base disc 1→2→1.8 xz, column popped to ×100 Y collapsing to 0, the
splash1/bsplsh zrd values verbatim; column *width* ×8 is TUNE — the authored quad is 5 cm wide,
sub-pixel past ~30 m) on the shared world materials, which honour the models' authored
`lighting/fog: false` themselves since BL-214 — the hand-rolled `OverrideUnlit` this needed is
gone, verified pixel-identical on a C1B night water burst; a dirt
(unclassified-terrain) hit spawns tumbling chips (`SpawnDirtDebris`) drawn on the gunhit def's own
`bit01–04` chip textures in alpha-blended per-texture pools (through the additive
muzzle-flash-textured impact pool they read as a small flame — the BL-203 mechanism; the def's
bit1–3 gamez nodes carry no geometry, the textures ARE the chips), launched outward and arcing via
`Sprite.Vel`/`SpinAxis`/`SpinRate`, zero and inert for every other sprite; a gun hit on a
buildings-classed surface spawns a ricochet spark burst + flash (`SpawnRicochet`, additive — a
judged stand-in: the authored `bld_damage.flt`/`rcochet1` are 2 of the 5 install-missing names). Each burst sprite carries its own orientation basis (`Sprite.Orient`): the muzzle flash
rolls in the firing plane's basis, the impact spark/explosion/debris faces the struck surface
normal (then tumbles, for debris) — a fixed world plane for none of them (BL-139); tracers stay
velocity-aligned, and none of the three is billboarded. A gun shot's muzzle flash (C24) is three
quads 120° apart around that basis's facing normal, the triad sharing one random per-shot roll
(seeded via `Rng.Weapons`, so `--det` stays reproducible) — one `List<Sprite>[]`/`MultiMesh[]` pair
per ammo-type texture (`{slug,dum,ap,mag}_muzzle1`, `MuzzleAmmoIndex` resolved from the weapon's
`FIRE` binding), since a `MultiMesh`'s material is shared across every instance it draws. Each quad
is centred by default (`Sprite.Pos`), except the muzzle flash triad, which sets `Sprite.AnchorLeft`
so `RenderSprites` derives the quad centre from `Pos + Orient.X * (currentSize/2)` — the texture's
left edge (QuadMesh's default UV, U=0 at local X=-0.5) stays pinned at the muzzle as the quad
shrinks over its life, instead of the texture being buried under a centred blob (C21). `AddMultiMesh`
draws every texture un-mirrored (a `Uv1Scale` mirror without a matching `Uv1Offset` here once
degenerated, under `TextureRepeat=false` clamping, into every sprite this pool draws — tracer,
muzzle, impact, smoke — rendering as a flat single-column colour stripe, C21); a texture needing a
flip gets it from its own geometry instead, never from that shared material — the tracer streak
(C25) carries the same per-ammo axis (`tracer_slug`/`_dumdum`/`_armorpierce`/`_magnesium`, ordnance
falling back to the generic `tracer1`) and rotates its own quad 180° about its facing normal
(`RenderTracers`: `new Basis(-yAxis*len, -xAxis*width, zAxis)`) to put the authored texture's head at
the round's current position, drawn with a uniform overbright tint baked from `weapons.tracerBrightness`
at spawn since additive blending with no bloom pass otherwise caps a tracer at the texture's own
pixel value. `weapons.tracerLength`/`tracerWidth`/`tracerBrightness` (C23) route the C25 constants
through `Config`, defaults unchanged; `RenderTracers` also floors the drawn width/length per round
against `weapons.tracerMinPixels` via `MinWorldSizeForPixels` (inverts the listener camera's vertical
FOV/viewport-height projection), so a round far enough out still reads as a fleck instead of
shrinking under a pixel — floored *before* the muzzle-growth cap, so it never outgrows how far the
round has actually flown.
`Spawn(weapon, worldMuzzle, inheritVel, shooterId)` fires one round; one pool per session, fed by
every player's guns — `shooterId` is the firing `PlayerIndex` (`NoShooter` for the lab's), carried on
the round so the near-miss cue can exclude its own. `NearMissTargets` is that cue's registry (BL-087):
each step measures the round's ACTUAL travelled segment — hit/fuse point included — against every
registered aircraft but its shooter's, and reports the pass distance (`WarningShotCue`). `DamageSink` (→ `AnimRuntime.DamageAt`) turns a hit into destructible damage;
`EffectSink` (→ `AnimRuntime.PlayEffectAt`) plays the non-model impact effects — rockets on the
runtime's own bound, gun hits under `GunEffectTtl` 0.3 s (the `*_gunhit` family's longest authored
stop, and the only bound the stop-less slug defs have) and one play per `GunEffectInterval` 0.1 s
per effect name = per firing group (`GunEffectDue`, on the sim clock);
`ClassifySurface` is `public static` — the ONE surface classifier, shared with the airframe's
graze reaction so a round and a wingtip never disagree about what they hit; `Impact` classifies and
calls `ImpactOutcome.Resolve`, then `Apply` obeys the result and decides nothing — the first 8
impacts log a breadcrumb of that record (`fx=`/`snd=`/`standin=`), so the probe line and the unit
assertion say the same thing, which is what makes a "these two surfaces look the same" report
answerable without a lucky screenshot (`BL-019`); rockets fly
their FLYOUT model body via `BuildFlyoutBody` (shared with `PylonOrdnance`) and trail their FLYOUT
`MODEL_ANIMATION` smoke (C21): the def's DISTANCE_INTERVAL puffers resolved from the world
`AnimProgram` (ctor `flyoutAnims`), one pooled/reused `Puffer.TrailAdvance` set per live round,
plus the sonic's authored 8.73 rad/s body roll (weapon-effects.md). Gun shots add the
`muzzle_burst` secondaries (C22): a pooled per-shot `gunshell` casing instance flying the def's
OBJECT_MOTION verbatim (per-shot nodes on purpose — a shared anchor under `CallAnimation`'s
already-live gate drops gun-rate ejections), the authored muzzlepuffer smoke plus a hand-authored
white eject-puff cluster on a gravity-free sprite pool, and a pooled `OmniLight3D` flash from the
def's `3rdperson_lts` range/colour variants — stand-in magnitudes are `BL-200` TUNE. `PlaySound`
(FIRE's launch bark, played once per `Spawn`; IMPACT's per-surface hit) resolves a `SOUND_GROUPS`
name (e.g. the incendiary rocket's `ground_mixed_exp_sg` default impact) through `_soundGroups`
first, same as `WorldSounds.PlayOneShot` — `FIRE.SOUND` is null for every cannon in the data
(`LOOPED_SOUND_NAME` covers continuous gunfire instead), so the one-shot never doubles up (BL-211).
Positive-`HEALTH_DAMAGE` blasts linearly fall from full at direct contact to zero at the authored
`IMPACT_PROXIMITY`, measured from each intersected collision-shape centre; `DAMAGE 0` effect radii
never damage. A swept `DETONATION_DISTANCE` sphere prevents fuse tunnelling and gates its actual
contact point through `DETONATION_DOT_PRODUCT`. The linear curve and 1 N·s/HP impulse are TUNE.
⚠ Hit detection is a per-step world raycast vs a body-less plane — a round never hits its own
  launcher, and `player`/`enemy` IMPACT classes are unreachable in M3.
⚠ `CANNON_SPREAD` jitter and the stand-in fireball draw from `Rng.Weapons` — a pinned run repeats
  its whole impact pattern (two `--det` C1B dives: 8/8 identical impact positions); new randomness
  must route through it. Trail-puffer scatter draws each emitter's own `Rng.Puffer` stream.
⚠ A trail emitter is reusable only when its round died AND `LiveCount == 0` — reusing sooner
  grafts the new rocket's trail onto the old one's live smoke. `RocketStreakScale` stays the
  fallback when a chapter lacks the rocket's prototype model; the empty stage (no world program)
  flies trail-less.

## src/Flight/WarningShotCue.cs
The incoming-fire near-miss cue's shipped accumulator (player.json `warning_shot_max` 2.0 /
`_dissipation` 2.0 / `_interval` 1.0), plus the swept-segment-to-point distance a round's step is
measured with — lifted out of `FlightController` so both unit-test without a live node, the same
split `WeaponCursor` uses. One pass accrues 1.0, saturating at max, draining at dissipation/s; the
cue re-triggers no faster than the interval.
⚠ The three values ship but their UNITS do not — this reading is chosen. With the shipped numbers
  the interval is the only term a pilot hears (one pass drains in 0.5 s); the meter's wider purpose
  is likely the battle state the neighbouring `pre_battle_sound`/`in_battle_sound` keys drive.
⚠ `PassRadius` 15 m is **not in the data** (TUNE `BL-230`, `weapons.warningShotRadius`). The sound
  def's `RANGE [20,200]` is the 3D falloff window, NOT a trigger radius — do not read one as the other.
⚠ The whole swept segment must be tested, never the endpoints: a gun round covers ~8 m per 60 Hz
  frame, so a per-frame point test misses most passes outright.

## src/Flight/IncomingFire.cs
`--incoming[=metres[,wep_id]]` — the near-miss test rig: a phantom shooter 120 m on each player's
six, alternating sides, firing the target's own gun (or the named weapon) into the shared pool under
a shooter identity no player holds. Exists because nothing in the world shoots back until M4 AI, so
the cue would otherwise need a second pilot in splitscreen. Aims along the target's own nose with a
lateral offset, so the round overtakes on a parallel track and the pass distance holds without lead
maths.
⚠ **Near misses only — a hit cannot be simulated by any rig.** An aircraft exists to the projectile
  raycast as nothing at all (collision is the swept `PlaneCollider` query boxes, not a body), which
  is why `bullet_hit_sg` stays unbuildable (`BL-226`).
⚠ Standoff is short on purpose: `CANNON_SPREAD` grows with range and past ~200 m throws rounds clean
  outside the trigger radius, which would read as a broken cue. The achieved distance still scatters
  a few metres around the requested one — judge over a burst, never one pass.

## src/Flight/PhysicsConstants.cs
`PhysicsConstants.NomGravity` — the single 20 m/s² player.json `nom_gravity` value, shared by
`PlaneStats.Gravity`'s default and `ProjectilePool.WorldGravity` so the two can't drift apart.

## src/Flight/PlaneStats.cs
Typed per-plane stats: vehicle.json `dynamics` (resolved through the `kind_of` def chain) +
engines.json stock engine power + player.json globals (the flight constants and the near-miss cue's
`warning_shot_*` block), the `engine_sound` def name with its
volume/pitch `SoundCurve`s (clamped two-point ramps), `destroyable_parts` → `DestroyablePart`
records (name, max HP, `critical`/`engine` flags, `got_hit_anim`, per-part `injure_anims`), and
the def-level `VehicleInjureAnims`. Schema: docs/formats/vehicle.md.
⚠ Def-level injure_anims are consumed as ANY-part HP fractions, not per-part — see DamageVisuals.
⚠ `damaged_engine_sound` is now parsed (`DamagedEngineSound` + `DamagedEngineGain`); only
  `cockpit_engine_sound` remains unparsed — it needs a cockpit view (`BL-161`).
⚠ `DamagedEngineGain`'s two source floats (0.0, 1.0 for every plane — one shared `basic_airplane`
  entry) are undecoded; read here as a fade window over accumulated damage fraction, a TUNE
  candidate not a confirmed mechanic — see `FlightAudio.cs`.

## src/Flight/SpawnPoints.cs
Reads the flight spawn from a mission's OWN zrdr (`extracted/<chapter>/<mission>/zrdr/` — a
different archive than the shared `--zrdr`), two schemas both yielding
`SpawnPoint(Position, HeadingDeg)`: `LoadIa` (instant-action ia.json `spawn_points` per scenario;
only IA1 folders have one, the original picks one at random per launch) and `LoadPlayerInit`
(story objectives.json `PLAYER_INIT`, position + yaw). Schema: docs/formats/spawns.md.
⚠ Throttle/speed from the data are deliberately ignored — PLAYER_INIT[3]/[4] are not spawn
  throttle/speed (see spawns.md); the remake uses FlightController's fixed start.

## src/Flight/MissionTargets.cs
Loads a mission's targets.json: world-node NAME → objective display keys
(`description`/`category_label`/`help_label`), resolved through `Messages`. Generic across
mission types; a missing file yields an empty set. Schema: docs/formats/missions.md.
⚠ The file is a list of [key, value]-pair lists, NOT the flat-alternating reader shape — walk
  it as pairs, never through `ZrdrDict`.

## src/Flight/StuntMission.cs
Stunt Flying state: `Load` builds the ordered zone list from ia.json `dzones` (HUD positions via
`GameZ.WorldTransformOf`, gate polygons from `dzpathN`; strings via MissionTargets + Messages; null
when a mission has none → free flight); `Update` requires both polygon-plane crossings in either
order, fires events, advances the target; clock/scoring via
`Elapsed`/`CompletedAt`/`CompletionOrder`/`InCompletionOrder`; `ForAnotherPlayer()` clones an
independent run so the archives parse once per session.
⚠ Ordinals lie here, twice: drive off the dzones LIST, never the gamez `dzN` nodes (numbering is
  non-contiguous, missions.md); and inside a `dzpathN` mesh tell the route from the gate pair by
  MATERIAL, not polygon index — the route is polygon 0 in only 2 of C4's 15 zones.
⚠ `GeometryAnchor` covers zones naming world GEOMETRY (C2's `sghangar`, ~8 km off via
  `WorldTransformOf`): anchor = union centre of the subtree's `door`-named leaf pair.
⚠ Deliberately NOT reset on respawn (a mid-run crash keeps zones + clock); `Reset()` is the opposite.

## src/Flight/HudMetrics.cs
The one place the flight HUD decides how big it draws: `Scale(control, reference = 1440)` =
window height / reference, damped by `PaneFactor` = sqrt(paneH/windowH) inside a splitscreen
pane (2P ≈ 71 %, 4P 50 %; the damping exponent is TUNE). CompassTape, GaugeCluster, MarkerHud,
StuntScoreboard and FlightController's text block all route through it.
⚠ The single-player identity is load-bearing: a full-screen view has `PaneFactor` exactly 1, so
  `Scale` returns the plain height ratio unchanged.
⚠ Damped sizes only stay on screen if positions anchor to a pane EDGE — see GaugeCluster's
  bottom-anchored dials and FlightController's text block.

## src/Flight/HudFont.cs
The game's own HUD bitmap font, rebuilt from `extracted/rimage/5pointhud.png` (+ the brighter
`5pointhudbrite.png` highlight variant): a proportional 5-px font covering printable ASCII
`0x20`–`0x7e` (layout/colours: docs/formats/hud.md). `Load` returns null (one log line) if the
atlas is absent; `Draw(CanvasItem,…)`/`Measure` render onto any caller's canvas, sized via
`HudMetrics`. The E34 foundation E35/E36 draw with.
⚠ Source rects are auto-segmented at load as maximal inked-column runs assigned from `0x21` up —
  exact only because no glyph has a blank interior column (94 runs = 94 codes); it warns if the
  count drifts. Black is keyed transparent, green kept — a white modulate reproduces the original.
⚠ The drawing control MUST set a Nearest texture filter (it is a pixel font); `HudFontTest.cs` is
  the `--hud-font-test` proof overlay (added per pane, so 1P vs a 4P pane compare).
⚠ In flight the font now loads unconditionally (E36 uses it), not only under `--hud-font-test`; that
  flag now gates only the `HudFontTest` overlay, not the font load.

## src/Flight/WeaponReadout.cs
The selected-weapon text readout (E36): a bottom-centre two-line `Control` drawing the current gun
group + rocket type and their live ammo in `HudFont`, from the game's own `MSG_HUD_GUNGAUGE` /
`MSG_HUD_MISSLES` templates (`Messages.Fill`, never hardcoded). FlightController pushes the state
each frame (`%1` = gun mount name / rocket display name, `%2` = per-group / per-pylon rounds).
⚠ A null name hides that line (no guns / no hardpoints / no loadout); bottom-anchored like the dials
  so a damped splitscreen pane keeps it on screen. This replaced the interim `FlightController.AmmoLine`.

## src/Flight/ImpactReticle.cs
The gun aiming reticle (E37): a viewport-filling `Control` drawing `impact_point.png` (the game's
pipper, from `extracted/rimage/`) at a world impact point fed each frame by FlightController,
projected via `Camera3D.UnprojectPosition` at `_Draw` time (mirrors MarkerHud, never cached).
Fixed screen size scaled by `HudMetrics`; one per player pane.
⚠ NOT pinned to screen centre — the point is FlightController's `BallisticImpactPoint` of the
  SELECTED gun group at `GunConvergenceDist` (a TUNE, 250 m — no data field), integrated exactly as
  `ProjectilePool` fires, so it trails the nose in a hard turn and sits on the rounds level.
⚠ `Active=false` hides it (crashed / no firable gun / behind-camera); `_Draw` early-returns at zero
  height (can run before the pane is sized).
⚠ `LoadTexture` (the `rimage`/`impact_point.png` PNG loader) is static and loader-only — it does
  not build a reticle; callers pass its result to `Build`.

## src/Flight/MarkerHud.cs
The stunt objective marker HUD: a viewport-filling `Control` drawing the on-screen reticle/text
block, the off-screen edge arrow (`EdgePoint`, `ClockHour` bearing), run status and banners
(`CompleteBanner` branches solo vs race); one per player, sized via `HudMetrics.Scale(this)`.
⚠ Projects in `_Draw` via `Camera3D.UnprojectPosition`/`IsPositionBehind` each frame —
  deliberately not cached in `_Process`, so the marker never lags a fast roll.
⚠ Edge-arrow direction is NEGATED when `IsPositionBehind` — behind-camera points unproject mirrored.
⚠ `_Draw` early-returns at zero height and clamps font sizes to `Max(1, …)` — it can run before
  the Control/pane is sized (Godot font-cache `p_size.x <= 0` errors otherwise).

## src/Flight/StuntScoreboard.cs
End-of-run results overlay: plain Godot UI (dimming backdrop → CenterContainer →
PanelContainer → VBox + 3-column split grid) filled from `StuntMission.InCompletionOrder()`;
wakes on `RunCompleted` (records via `ScoreStore.RecordIfBest`, logs the split table to stdout
for headless review), branching NEW BEST vs BEST on the stored record.
⚠ Hides itself in `_Process` the moment `AllComplete` clears (a restart) — no explicit teardown
  wiring; `Populate` rebuilds the panel each time so a second run's board is clean.
⚠ Scale is `max(0.5, HudMetrics.Scale(this, 720))` — a plain viewport ratio floored at 1
  overflowed a splitscreen pane.

## src/Flight/ScoreStore.cs
Stunt best-time persistence: one JSON object in `user://stunt_scores.json` keyed
`chapter/mission/plane` → `{best, date}`; `GetBest` / `RecordIfBest` (returns whether it was a
new best — never worsens a record).
⚠ Read/written via Godot's `FileAccess` + `Json` — only that API resolves `user://`, and
  `Json.Stringify` is locale-neutral. A missing/malformed file loads as an empty store, never throws.
⚠ Deliberately not consulted by the splitscreen race — race totals aren't comparable across
  player counts or spawn indices.

## src/Flight/StuntRace.cs
Splitscreen race bookkeeping: one `Racer` per player (own `StuntMission`, `Rank`, `FinishTime`);
finishing stamps the next placing, `RaceCompleted` fires when the last pilot is in; `Standings()`
orders finishers by placing then in-flight players by progress; `Restart()` (rematch) resets
every mission and clears placings — the planes are respawned by GameSession, which owns them.
Its console lines, and `StuntScoreboard`/`StuntRaceBoard`'s, route through `Log.Info("flight", …)`
(M3 `PLAN-deepening.md` C6/C7) instead of a bare `GD.Print`. Off-engine coverage:
`CSVM.Tests/StuntRaceTests.cs` (finish ordering, rematch reset, standings ties).
⚠ `FinishTime` is snapshotted separately from `Mission.Elapsed` so the board still reads
  correctly after a rematch has reset the missions.
⚠ Deliberately not a Node — it is freed with the session, so the `RunCompleted` subscriptions
  need no teardown.
⚠ `StuntMission` (its `Racer.Mission`) is NOT part of this seam — `Load`/`Complete` still call
  `GD.Print`/`GD.PushWarning` directly and crash the test host outside the engine (an unmanaged
  `AccessViolationException`, not a catchable one). `StuntRaceTests` never calls either: it builds
  a `StuntMission` via reflection on the private constructor and fires `RunCompleted` the same way.

## src/Flight/StuntRaceBoard.cs
The race's shared ranked results overlay: same clean-Godot-UI construction as StuntScoreboard,
but covering the WHOLE window — on its own CanvasLayer (Layer 10, above SplitScreen's 0) under
the session root, one row per player from `StuntRace.Standings()` (placing, tag, plane, zones,
total + gap to the winner; DNF when unfinished). Wakes on `RaceCompleted`, hides in `_Process`
once `AllFinished` clears; the footer's exit hint follows how the session was launched.
⚠ Scales on raw window height / 720, NOT HudMetrics — pane damping would shrink a full-window
  overlay for no reason.

## src/Flight/Weather.cs
`WeatherState`: per-mission atmosphere from the flown mission's own weather.json — per-zone fog
(`FOG_COLOR`/`FOG_RANGES`/`FOG_ALTITUDE`), `SUNLIGHT_*` → `ZoneFog.WorldLight` (`SunIncidence`
0.46 / `MinWorldLight` 0.15, TUNE), the `CLOUD_COVER` whiteout band (`WhiteoutAmount`
trapezoid), `WIND`, and precipitation → `PrecipData`. Schema + colours + zone names: weather.md.
⚠ `CLOUD_COVER`/`WIND`/precip keys pair with BARE scalars — `ZrdrDict.FromAlternating` cannot
  read them; walked raw (`StringAfter`/`ScalarAfter`/…). Per-zone blocks are list-valued (dict).
⚠ `ZoneKeys` collects `ZONE<digits>` keys from the raw list in FILE ORDER (a Dictionary loses
  order; `ResolveZone`'s fallback is the file's FIRST zone); `SW_ZONE*` twins are excluded.
⚠ The default stays `zone2` (user decision) — which zone a mission flies is in no file
  (negative result in weather.md), so a fallback is the only correct behaviour.

## src/Flight/FlightAudio.cs
Own-plane non-positional loops (engine with throttle-driven pitch, overspeed whine, rattle,
damaged-engine blend keyed to `Update`'s `damageFrac` via `PlaneStats.DamagedEngineGain`) +
one-shots: `StartEngine`/`EngineStartRamp` prop-start fade (re-fired via the loop-restart hook
in `Update`), `OnCrash` → `snd_exp_plane1..4`, `OnGroundExplosion`/`OnWaterExplosion` layering the
crash variant's boom (`snd_exp_ground_a` off the dirt def itself, `snd_exp_water_a` from the
`plane_big_splash` inside the sea dive — the crash runtime renders effects, never sound),
`OnGraze(water)` → the survivable scrape's authored `snd_exp_water_b`/`snd_exp_ground_b`
(touchdown.zrd; rate-limited by `FlightController`, not here). `OnWarningShot` draws one
`bullet_warning_sg` variant per near miss (player.json `warning_shot_sound` is a SOUND_GROUPS name, so
`Setup` takes the group table too; rate-limited by `FlightController`'s `WarningShotCue`, same split).
⚠ `WhineMixGain` 0.12 (TUNE), `Config`-wired (`flightAudio.whineMixGain`): don't raise it back —
  reader "volume" is not a linear mix gain (the original's whine sits 12–18 dB below the raw curve
  cap); re-derive from a new reference. `DamagedEngineMixGain` 1.0 is the same shape
  (`flightAudio.damagedEngineMixGain`) with no reference recording yet to derive a value from.
⚠ `OnEngineStop` is deliberately NOT called on crash; a future shutdown flow must also stop
  driving `Update`, or the restart hook re-fires propstart.
⚠ `MixGain` (1/√N in splitscreen, TUNE) covers the four loops and the per-player cues
  (`PlayEmptyClip`, `OnGraze`); the crash/prop one-shots are deliberately left unscaled.

## src/Effects/Puffer.cs
The original engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks
(schema: docs/formats/effects.md). `PufferState.Load` finds the fully-defined state in an
effects reader; `Puffer.Create` builds a texture atlas + ONE MultiMesh whose shader billboards
each quad, with quad-rim fade + soft-particle depth fade. Modes: `Burst`, `TrailAdvance` /
`TrailBurnAt` (distance trails), `SustainAt` (continuous at a moving node — pool sized to steady
state, catch-up capped); `PufferState.FromAnimEvent` parses the compiled anim payloads.
Three config knobs scale `BaseSize` per spawn path — `puffer.burstSizeScale` /
`puffer.trailSizeScale` / `puffer.sustainSizeScale` (`SizeScaleDefault` **4**, a TUNE stand-in for a
missing engine constant settled at the controls — 1 is the authored SIZE_RANGE verbatim, which reads
as a thin scatter of specks; the cull margin scales with the largest). Read at `Init`; registered in
`Config.WarmTuningRegistry` for `--dump-config`. Moving `SizeScaleDefault` re-pins every
puffer-bearing golden and only those (measured: `c1-waterfall`, `c3-island`, `c1-destroy-effects`,
`c1-crash`; the other 9 carry no live emitter).
⚠ TEXTURE_SEQUENCE times are FRACTIONS of a particle's lifetime, not seconds (effects.md).
⚠ The blend is derived, never authored: a COLORS ramp or a near-black dying sprite (measured off
  the atlas, `SmokeLuminance`) ⇒ blend_mix + no depth fade, else blend_add (effects.md).
  `Create`'s `blend`/`softParticles` force the verdict for a caller that knows better.
⚠ Each emitter's `_rng` is a per-instance stream off `Rng.Puffer`, so particle spread is pinned by
  the master seed: measured, the C1 waterfall mist moved 0.47% of a `--det` frame before and 0.00%
  after. Its seed depends on how many puffers were built before it — deterministic under `--det`.

## src/Effects/CloudPuffs.cs
Synthetic ambient cloud field: ONE alpha-blended MultiMesh of cloud1/cloud2 billboards in a
cylindrical shell around the camera — Y anchored to the CLOUD_COVER band, X/Z following the
plane, passed puffs recycling to the leading edge, WIND-driven drift, alpha fading at the shell
edge and by vertical distance. Feel constants all TUNE (`BaseAlpha` kept low — overlaps saturate).
⚠ Synthetic by design only for DENSITY: the world's own ~600 cloud sprites cluster near the airfield
  and no zrdr defines an ambient emitter or a puff count/opacity. The vertical EXTENT is not
  synthetic — `weather.json`'s CLOUD_COVER band is the truth, and the hand-picked `BandBelow`/
  `BandAbove`/`VertFull`/`VertFade` margins currently span most of the flight envelope instead.
⚠ The shader keeps `fog_disabled` yet carries the custom `csky_fog_*` cylindrical fog term —
  that render mode only disables Godot's BUILT-IN fog; ours is custom.
⚠ `_rng` is a per-field stream off `Rng.Clouds` (one field per splitscreen rig, each independent).
  Puff *recycling* is camera-position driven, so the draw count is sim state, not a fixed series —
  identical only when the camera path is.

## src/Effects/Precipitation.cs
Rain/snow from weather.json's precip block (`WeatherState.PrecipData`): ONE MultiMesh whose
shader derives each quad's position from a per-instance seed + `csky_time` + `CAMERA_POSITION_WORLD`,
wrapped into a camera-centred box — zero per-frame CPU. SNOW = fluttering flakes; RAIN =
streaks along the data's WORLD fall velocity (not plane-relative — TUNE pending A/B); sprites
are procedural `MakeFlakeTexture`/`MakeStreakTexture` (the original drew untextured primitives).
⚠ This module has NO per-frame C# hook, so the `csky_time` global is the only handle on the
  animation: halting or fixed-stepping the sim clock is the sole way to stop or pin the fall.
  Never reintroduce `TIME` here — the rain would keep falling through a halt.
⚠ The per-instance seeds are one draw sequence off `Rng.Precip` at construction, so the whole
  field's layout is a function of the master seed. Measured on the same `--det` pose across two
  runs: C2B rain 5.44% of pixels before, 0.00% after; C4 snow 25.84% before, 0.00% after.
⚠ World-sized `CustomAabb` (±40 km) stops frustum culling — the instances sit at the node origin.

## src/Flight/SpectatorCamera.cs
The `--freecam`/`--anim-lab` observation camera: WASD move, RMB-held mouse look (captured only
while held), wheel speed, pads via `Pads.For(null)`; lab additions `Frame(Aabb)`, the
`FollowNode` orbit-lock (released by any translation input; `ExitFollow` keeps orientation) and
a public `Camera` accessor — all inert in plain `--freecam`. Rates TUNE.
⚠ Default start is the mission spawn — RANDOM per launch; pass `--pos`/`--direction` for comparisons.
⚠ `KeyboardCaptured` zeroes keyboard axes while a text field owns focus — raw key polls bypass GUI focus.
⚠ Vertical is Q/E plus the **Z/Space** alternate — Z, not C: C toggles the collider overlay, and
  because this camera POLLS raw key state, sharing the key descended on every toggle press.

## src/Flight/FlightModel.cs
Velocity-vector arcade flight model: body rates = control torque × reciprocal inertia vs
ang_momentum_damp, scaled per axis (PitchTune/YawTune/RollTune). Thrust/drag/gravity integrate on
the velocity vector (speed passes through zero); lift cancels gravity's cross-path share; drag is
normalized so drag(fd_speed) = max thrust.
⚠ ThrustConst and the three *Tune rates are NOT free TUNEs — they are pinned to the original,
  measured off cockpit-gauge video, and `--run-tests`' flight-envelope suite fails if they move.
  Retune by feel and you are overwriting a measurement (`analysis/video-flight-calibration/`).
⚠ MaxDiveSpeedFrac is a numerical backstop, not a terminal speed: terminal dive is EMERGENT from
  the drag curve and lands within 0.3% of the original, so a value that binds replaces a measured
  number with a guess. Keep it above every airframe's emergent terminal — worst is the Balmoral,
  1.678 in a 71° dive (`--dump-flight=player_balmoral`) and ~1.71 vertical.
⚠ Accepted artifacts, not bugs: loop energy pump, steep-climb equilibrium, stall hang. Known
  MISSING, both measured: no induced drag (a hard pull costs no speed) and no altitude limit.

## src/Flight/PropAnimator.cs
Spins the flying aircraft's prop/rotor blur discs: Build collects every node PropParts classifies
(local axis + rate), Advance rotates each via RotateObjectLocal so the disc spins in-plane
regardless of parent orientation. FlightController drives it throttle-scaled with a PropIdleSpin
0.4 floor (0 while crashed). --fly only — the static viewer keeps the still disc.

## src/Flight/ControlSurfaceAnimator.cs
Deflects ailerons/elevators/rudders to an absolute pose: each surface stores its build-time local
basis and gets Basis = base · Rot(hingeAxis, angle); three channels slew toward the stick at
SlewPerSec (TUNE), ±20° per kind. --fly only; frozen while paused/crashed, reset on respawn.
⚠ The per-surface sign bakes three flips: the stick convention (ailerons opposite per side, TE
  against the commanded rotation), a canard flip (hinge z < CanardMaxZ ⇒ nose-mounted ⇒ pull
  deflects TE-down), and a frame flip from the accumulated hinge axis vs its canonical plane-space
  direction (canard groups mounted yaw-π). Account for all three before touching any sign.

## src/Flight/WingLightBlinker.cs
Flashes the wingtip flares for FlashDuration 0.08 s (TUNE — the source flash is a single frame,
widened so the blink reads) each WingLights.BlinkPeriod; Reset (respawn) restarts the cycle with
the flares off. Advanced each _Process, frozen while paused or crashed. --fly only.

## src/Flight/PylonOrdnance.cs
The rockets mounted under a plane's wings (D44): `Build` instances ONE FLYOUT MODEL body per loaded
pylon via `ProjectilePool.BuildFlyoutBody` (the SAME gamez prototype the round flies), parents it to
that pylon marker at identity local transform (nose -Z forward, tail at the mount = the launch pose),
and `Update` shows/hides each per its live `Hardpoint.Ammo`. FlightController drives `Update` after
UpdateRockets; the mounted body rides the plane and is freed with it. --fly only.
⚠ ONE model per pylon, not one per CLUSTER_SIZE round — the original shows a single rocket per
  hardpoint (D44 trap). Show while `Ammo > 0`, hide at zero; a respawn refill re-shows next frame.
⚠ No double-up with airframe geometry: NO plane model carries static ordnance mesh — every
  rocket/missile/bomb/torpedo name search is empty and pylon nodes are all `model_index -1` markers.
⚠ Null when nothing mounts (viewer, or a chapter gamez lacking the prototype root) — the round then
  flies its streak-only fallback and the wing simply shows no ordnance; never a hard failure.

## src/Flight/PlaneCollider.cs
Derives 5–8 plane-frame collision boxes from the built model's mesh triangles alone (no per-plane
data): region-clipped geometry (tail/wing/fuselage), then greedy volume-guided refinement cutting
one OR two parallel planes per axis (the double cut separates bilateral pairs like twin fins).
⚠ Relabel renames aft outboard boxes `wing` (box wholly one side of the centerline + centre
  outboard of WingBandFrac) so PlaneDamage's localImpact-blind "tail" arm never sees a wingtip
  strike. The half-span is known here — do not side-split in PlaneDamage instead.
⚠ Boxes deliberately overlap; the earliest in Parts order is what gets reported.
⚠ Known limit: the Bloodhawk's canard tips stay uncovered.

## src/Flight/FlightController.cs
The flying-aircraft node: input → FlightModel → transform, roll-following chase camera + fixed
numpad views, text HUD + telemetry, weapon firing/selection, crash and respawn. Sweeps the
PlaneCollider boxes via CastMotion each physics frame; the sim half is `SimStep(dt)`, called by
`_PhysicsProcess` (realtime clock) or by `GameSession` (fixed/halted clock). Collaborators:
FlightModel, Loadout + ProjectilePool (guns/rockets), `CollideDamageSink` →
`AnimRuntime.CollideDamageAt` (fly-through facades), CrashRuntime, every HUD widget and animator.
`Crash` reads the struck body through the same `ProjectilePool.ClassifySurface` (`ClassifySurface`
here maps it to `CrashSurface`) to pick the variant: a `water`-tagged body plays
`player_crash_water` + `snd_exp_water_a`, everything else `player_crash_dirt` + `snd_exp_ground_a`;
`Air` is the no-impact destruct and has no trigger. `--crash` has no struck body, so it forces dirt.
A survivable graze also plays touchdown.zrd's per-surface reaction (`GrazeReaction`): the struck
collider classified through the same call picks `touchdown_default` (buildings,
sparks) / `touchdown_dirt` / `touchdown_water`, staged at the contact point via `GrazeEffectSink`
(the world-effects runtime) with `FlightAudio.OnGraze` under it, one per `GrazeReactionInterval`.
Where it stages is an open A/B — `graze.siteAtContact`, default the contact point (judged at the
controls); false stages on the aircraft, which is what the def's `MAIN_ROOT_NODE` offsets assume.
`AttachWarningShotCue` registers the aircraft on the pool as a near-miss target and `OnNearMiss`
rates the passes through `WarningShotCue` into `FlightAudio.OnWarningShot`; `PlayerIndex` is both
the pane seat and the identity every round this pilot fires carries, so it must be set before the
registration (the assembler sets it at construction, not in the stunt block).
⚠ The chase camera slerps its BASIS, never a re-derived LookAt (inverted flight renders upside
  down), and takes the SIM clock's dt; the halted orbit camera keeps wall time on purpose. Its
  distance/lag constants are hand-picked while `extracted/zrdr/camparam.zrd.json` ships real ones
  (per-plane) that nothing reads — check there before adding or retuning any camera constant.
  On the realtime clock the DRAWN pose is `_renderPose` — interpolated between the last two sim
  poses, because the 60 Hz sim stutters against >60 fps rendering (DET-10) — and anything bolted
  to the plane (the rigid numpad views) must read it, never the raw sim pose.
⚠ The reticle march (`BallisticImpactPoint`) calls `Ballistics.March`, the same integration
  `ProjectilePool.SimStep` steps real rounds with — see `Ballistics.cs`'s entry for the one thing
  that still differs between them (`dt`).
⚠ The stunt/race AllComplete freeze runs BEFORE the crash branch; Respawn never resets a mid-run stunt.

## src/Flight/PlaneDamage.cs
Per-part hit points from vehicle.json destroyable_parts (via PlaneStats). MapStruckPart maps a
struck collider box + plane-local impact to the data part: wing/canard by impact X sign (left =
−X), fuselage fore/aft of z 0 → nose/tail. Apply subtracts, Reset refills on respawn, Summary
feeds the HUD DMG line, WorstFraction (lowest part fraction, 1f pristine) feeds whole-plane
feedback like FlightAudio's damaged-engine loop.
⚠ The "tail" arm ignores localImpact and is correct only because PlaneCollider.Relabel hands it
  no outboard boxes — do not fix tail sidedness here; widening the signature was rejected.
⚠ The `engine` flag (power loss) is unwired **by design, not deferred** — the original states damage
  never degrades performance; but the shipped data still sets the flag, so retail may have walked
  that back (docs/formats/vehicle.md).
⚠ There is no armour pool anywhere in the collision path: Apply is a flat subtract on one Hp, and
  FlightController.Crash never calls in at all (a crash is a boolean destroy). Do not assume armour
  is spent first on a graze or a crash — it is not modelled.

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's HP fraction crosses an injure_anims
entry it shows the torn pdpN panel, hides the healthy skin, and assigns a discrete-puff fire trail
from the emitter pool; def-level player_smoketrail starts the nose smoke/fire pair. UpdateStatic
burns the trails in place at StaticBurnSpeed for the parked damage lab. A `<part>_damage_effects`
entry (the 0.99 one) goes out through `DamageEffectSink` to the player's own rig runtime, which
sparks at a `pdpN` panel — **authored on `player_pfighter` alone**, so the other 10 aircraft reach
this branch never (B4). The same effect's general home is the weapons.json `player` IMPACT surface,
blocked on enemy fire (`BL-222`); do not add the entry to other planes to "fix" the asymmetry.
⚠ PairHealthySkins pairs torn↔healthy by merged mesh-AABB position, never by name (the _h
  numbering is crossed on three models — docs/formats/gamez.md) and never by node origin (the
  placement is baked into mesh space); unpaired _h skins are never hidden.
⚠ player_fuelleak, got_hit_anim's nosedamage blink and the *_damage_green/yellow/red cockpit cycle
  stay unwired (no cockpit; GaugeCluster.OnPartDamage approximates the blink by hand — do not merge
  the two, different data and different surface).
⚠ These smoke/fire puffers are built directly via Puffer.MakePuffer — NOT through the world-effects
  runtime's fixed name set. Fixing the world-destructible puffer gap does not touch this path, or
  vice versa; diagnose them separately.

## src/Flight/DamageLab.cs
The damage lab (F5 toggles): one HP slider per destroyable part with threshold readouts, plus a
mirrored GaugeCluster damage dial in the viewer; presets (--damage=part:frac) land through the same
ValueChanged path as a hand drag. One panel, two hosts, chosen by the injected IDamageLabTarget
(same file): ViewerDamageTarget drives DamageVisuals on a parked plane, FlightDamageTarget writes
P1's real PlaneDamage while the sim runs. It never reimplements visuals, only decides when to
rebuild them.
⚠ Reapply's crossed-anim set-diff (TargetAnims) is load-bearing twice: it implements repair
  (re-derives from pristine) and keeps a slider drag from restarting the fires at every pixel.
⚠ Built in EVERY viewer AND flight session (StartHidden without --damage) so F5 has a receiver; two
  F5 presses must return a byte-identical frame (SetLabVisible toggles panel + gauge layer together).
⚠ FlightDamageTarget.Tick is deliberately empty and it must not touch Gauges.PartFraction:
  FlightController already drives DamageVisuals from the live pose and binds the dial to the same
  PlaneDamage the sliders write. SyncFromTarget's read-back skips sliders being dragged.

## src/Flight/CompassTape.cs
The original's top-centre heading tape rebuilt from the game's own compassticks2/compasstxt
textures: a cylindrical drum seen edge-on — DrumX = center − R·sin(Δ), headings increase LEFT,
cos(Δ) fade (rendering model: docs/formats/hud.md). Metrics are probe-fitted Ref* constants ×
HudMetrics.Scale; Build returns null if a texture is missing; _Process re-anchors on resize.
⚠ The filtering split is deliberate: ticks point-sampled without mips on the tape Control (mips
  crush the tile vertically), labels bilinear on a child LabelLayer — do not unify them.
⚠ TileOverscan/RimGain and the nearest-tick look are TUNE pending user A/B. North = −Z is confirmed
  against the original — do not reopen or re-flip it.

## src/Flight/GaugeCluster.cs
The original's cockpit dials as a screen-space HUD: altimeter, speedometer, damage display, plus
the gun + missile weapon gauges (E35), all geometry extracted from the plane's own gauges subtree
(structure/scales/quirks: docs/formats/hud.md); polys draw by data priority, rest rotations
ignored; PartFraction binds flight or the lab; dial centres are bottom-anchored (FromBottom) so
panes keep them on screen.
⚠ The gauge textures lie — compare pixel values, never appearances: never color-key needle.tif (a
  black key erases the hub's two black discs), and the faces hold dark UNLIT copies of the STALL /
  LOW ALT windows (~58,0,0 unlit vs 180+,0,0 lit); bitten twice.
⚠ The weapon-gauge 4-digit readout is per-GROUP for guns, per-PYLON for rockets — NOT a total; the
  belt-indicator yellow tier is likewise GUN-ONLY (hardpoint/pylon indicators go green→red, never
  yellow — confirmed against the original).
⚠ The gungauge/missilegauge face is on a generic child (`g815`/`g819`) on ALL planes (no Bloodhawk
  special case, unlike the damage dial) — "any unrecognised child = face" is the extraction rule.

## src/UI/LaunchMenu.cs
The in-game launchscreen CanvasLayer: Mode → Chapter → Plane, input polled every frame through
one MenuInput per player (no input-map/focus wiring); joining is gated to the Plane screen, and
with >1 player that screen becomes real SplitScreen.PaneRect panes — pick in the pane you fly in.
⚠ Player 1 is the keyboard + the SET of all unclaimed pads until ClaimP1Pad pins its real pad —
  never pads[0], which re-breaks the phantom-device fix; the leftover set makes hand-off free.
⚠ Re-entrant: ShowMenu resets to Mode, clears locks (joined players survive), and primes input +
  join edges from the CURRENT raw state — a still-held Esc/Start must not read as a fresh press.
⚠ Size from GetViewport().GetVisibleRect() (a CanvasLayer is not a CanvasItem); LayoutScale caps fonts so 4P fits 720p.

## src/UI/MenuInput.cs
One launchscreen player's input source — keyboard flag (player 1 only), `Pads` device array, edge/
auto-repeat state; `Poll(dt)` fills Move/Accept/Back/Start (polled: actions can't read a named device).
⚠ `Pads` is an array, not an int: player 1 holds every unclaimed device. Reads OR the buttons and
  take the max-magnitude axis, so idle phantom devices contribute nothing.
⚠ Raw reads go through `CSVM.Pads.For(Pads)`, never the field: the field is the player's binding
  (join bookkeeping needs it while unfocused), `For` is the focus gate; `JoinPressed` inlines the gate.
⚠ `Prime()` seeds edge flags from raw state — a button held through a screen transition is no
  press; `LastActivePad` excludes Start (the join gesture is not proof somebody owns the pad).

## src/UI/SplitScreen.cs
The splitscreen rig for 2–4 players (1P never constructs it, keeping that path untouched): black
gutter backdrop, one `SubViewport` pane per player sharing the main `World3D`, plus the
`PlayerColor`/`PlayerTag` identity table.
⚠ `PaneRect(index, players, size)` is the ONE pane-layout definition — the launchscreen aircraft
  select uses the identical call, so the menu pane you pick in is exactly the flight pane you get.
⚠ `SubViewport.Msaa3D` does not inherit the project msaa_3d setting (root viewport only) — copy it
  across; `RenderTargetUpdateMode` must be `Always`.
⚠ `PlayerVisualLayer` reserves layers 17–20 (`PlayerLayerBit0` = 16); the world stays on layer 1;
  `PlayerCullMask(i)` adds only that player's bit — a pane sees only its own sky/deck/puffs.

## src/Flight/PlayerRig.cs
One rendered view's state bag: index, camera, optional `SubViewport`, `HudParent`, `VisualLayer`,
the player's FlightController, and private camera-anchored copies (`Horizon`/`Deck`/`Puffs`/
`Whiteout`) — those re-anchor to the view's camera every frame, so N players need N of each.
⚠ Single player holds exactly one rig wrapping the main-viewport camera with `VisualLayer` 0, so
  every loop over the rigs degenerates to the old single-camera code.
⚠ In splitscreen the camera's parent is a `SubViewport`, not a Node3D — local `Position` IS the
  world transform, so per-frame anchoring reads `Camera.Position` directly (correct in both modes).

## src/UI/LiveryLab.cs
The `--viewer` livery editor (key L): squadron stepper (loads the squadron's whole livery via
`LoadSquadronLivery`), per-slot RGB sliders, decal steppers, random livery, copy-CLI-args.
⚠ Single write path: every edit funnels through `Apply()` → `PlaneBuilder.Repaint`, and `--viewer`
  builds the plane BARE — `--viewer --paint=X --screenshot` end-to-end tests the repaint itself.
⚠ Steppers walk `PatternLibrary.PatternsFor` (this aircraft's list), never the vehicle catalog;
  `CliArgs()` resolves the canonical entry by NAME and emits bare `--paint=` only on a verbatim match.

## src/UI/NodeLabels.cs
Floating node-name labels (key T) in both the static viewer and flight, cycling Off → Meshes → All;
`--debug-names[=meshes|all]` presets the mode at launch.
⚠ Labels anchor at the mesh-AABB centre in node-local space, not the node origin — origins sit far
  from the geometry and are shared, which collapsed all labels into a single screen cell.
⚠ The nearest-first grid de-clutter (3×3 neighbourhood) is the readability limiter, not `Radius` (1500 m).
⚠ Own plane deprioritised, not excluded; rescans on a 0.35 s timer.

## src/UI/MarkerOverlay.cs
The `--viewer` marker overlay (key K): draws every firepoint / pylon / target on the parked
aircraft as a coloured gizmo + billboarded label (firepoints orange, shared-mount firepoints
magenta, pylons cyan, target green); `--markers` opens it at launch. Reuses `MarkerRig.Classify`
+ `GroupCoLocated`, so its gizmos agree with `--dump-markers` by construction.
⚠ Markers come from the built plane tree via the `cs_name` meta, not GameZ — the same source
  `NodeLabels` reads; a co-located pair's labels are stacked up the airframe so both survive.
⚠ Gizmo dots always show (no mount position is ever lost); only the LABELS de-clutter, nearest-
  first with firepoints prioritised over pylons — the full named table stays in `--dump-markers`.

## src/UI/MeshLab.cs
The geometry/shading lab (key M): normal lines, smoothing-seam wireframe, collider boxes, light
sliders + headlight, cull × normal-source override cyclers (`--debug-mesh=` scripts them). Two
shapes: the `--viewer` lab owns the parked plane; the scoped lab (`--freecam`/`--anim-lab`, over a
`SelectionService`) attaches to the current selection on M and restores on change/deselect.
⚠ **Override materials are the surface's OWN shader with two edits** (cull token + a
  `csky_lab_normal_mode` rewrite); measured raw-pixel identical to the shipped render. The
  hand-written replica shader is the FALLBACK only (moved 1,682 px of a 2,500 px subject).
⚠ `BoundingRadius` is the geometry's own box half-diagonal, NEVER max |v| — the C1 water tower
  read 7,420 m and drew 163 m normal lines across the chapter.
⚠ Built after the subject joins the tree — `GlobalTransform` on a detached node is identity + error spam.

## src/UI/WeaponLab.cs
The `--viewer` weapon lab (key W): mounts a weapon and fires it, driving its OWN `ProjectilePool`
so a round runs the identical ballistics flight fires. GUNS fire from the plane's gun groups,
HARDPOINTS from its pylons; steppers pick bank/weapon/mount/target surface, a slider parks a
stand-in target wall 15–1100 m ahead; copy-CLI-args (`--weapon-lab=<id>`).
⚠ No chapter world: the pool is built scene-less — rockets fly streak-only, gun impacts show the
  spark, hardpoint impacts the explosion stand-in, and `DamageSink` is null.
⚠ Mounts bind from the stock `Loadout`; a plane the table omits (or a bind failure) falls back to
  the raw firepoint/pylon marker rig.
⚠ A live volley (`FireVolley`, the trigger/auto-fire path) fires one mount node per pull and
  alternates, mirroring `FlightController.UpdateGuns`'s per-group muzzle cursor — never every node
  at once. `RunSelfTest` is the one exception: it still fires every node of a mount directly (not
  through `FireVolley`), for the `--weapon-test` pass check across all 48 weapons.

## src/UI/SelectionService.cs
The shared world selection in `--freecam`/`--anim-lab`: left-click picks the mesh under the
cursor, PgUp/PgDn walk its `cs_name` ancestor ladder, a breadcrumb HUD line + wireframe box show
the current rung. `Current`/`Ladder`/`Level`/`CurrentBox` + the `Changed` event are the state the
other inspect tools read; `Select(node)` is the programmatic entry; `--debug-select=x,y[,up]`
replays a click for scripted runs. `ExtraRoots` walks props parked beside the world content rather
than under it (the anim lab's `--plane=` prop), each also capping its own ancestor ladder (BL-043).
⚠ The pick is a manual ray-vs-AABB scan, NOT a physics raycast — neither mode builds collision
  (WORLD-9); every later inspect tool inherits this mechanism.
⚠ Rungs are the `cs_name` meta, never `Node.Name` (WORLD-8) — C1's second `box_car.flt` is `godot=@Node3D@5`.
⚠ The box is measured from the selected subtree's OWN meshes, never an `OrbitCamera.MergedAabb`
  live-tree merge (WORLD-14 — an overlay parked elsewhere would enter the merge).

## src/UI/ColliderOverlay.cs
The collision wireframe overlay (key C, `--collision=show`/`--debug-colliders` script it) in
`--freecam`/`--anim-lab`/`--fly`: one `ImmediateMesh` per collider host, colour-coded by owner class
(world / water / buildings / clutter / plane / other), rebuilt from the live tree on every show. A colour→class legend (`BuildLegendText`, sourced from `ColorFor` alone so
a palette change can't desync it) sits under the summary whenever wireframes are actually shown —
never for the "no collision built" notice, an empty-legend echo of WORLD-9. Measured C2: 1,848
node-backed shapes + 10k–14k clutter placements.
⚠ **Its first job is the notice.** Pressing C in a mode that built no collision prints the reason
  and draws NOTHING — an empty overlay would read as "nothing here is solid" (WORLD-9). The
  tallies that follow are logged as **separate on and off counts plus the names that flipped**,
  never a net (WORLD-10: the C2 gate nets +7 — `col[off 1, on 8]`).
⚠ Two frame traps in the drawing. Plane airframe boxes parent to the `FlightController`, never
  the plane MODEL node (`Parts.Local` is in the model's PARENT frame — under the model its local
  transform applies twice). And `Inflate` scales trimesh vertices about their AABB centre, never
  the local origin: world trimeshes carry world-baked vertices, so an origin-relative scale shifts
  the wireframe by 0.25% of position — ~20 m at a map corner, invisible near the origin (BL-198).
⚠ Cost with it up (C4, `--perf --no-vsync`): draws 2,181 → 2,532, prims 217k → 257k, `render_cpu`
  1.05 → 1.42 ms, memory 225 → 266 MB. Read those, never `fps`/`frame_ms` (PERF-11).

## src/UI/ClassOverlay.cs
The colour-by-class overlay (key X, `--debug-classoverlay` scripts it) — same mode set as
`ColliderOverlay` (`--freecam`/`--anim-lab`/`--fly`/`--stunt`), a findable-targets view rather than a
collision one (`BL-029`). Tints every drawn mesh's `MaterialOverride` flat by class: destructible
(red, via `DestructibleRegistry.Resolve` — the exact climb a weapon hit takes), facade (pink, via
`SceneBuilder.ClassifyBillboard` on the source `GameZMesh`, resolved back through the built node's
`AnimRuntime.IndexMeta`), clutter (green, every `MultiMeshInstance3D` under the world root —
nothing else in this codebase parents one there), everything else scenery (blue). Rebuilt on every
X press rather than cached, restoring each tinted node's original `MaterialOverride` first.
⚠ **Deliberately NOT keyed on `SceneBuilder.SurfaceMeta`** — that tag answers "what does a bullet do
  here" (water/buildings/default, for impact-effect selection), not "what is this object"; two
  unrelated objects can share a surface tag.
⚠ **A door that only LOOKS breakable reads as scenery, and that is the correct finding, not a bug**
  — `Resolve` requires a registered `HEALTH > 0` anchor (BL-009's C2 SeaHangar doors have none).
⚠ No gamez world to classify (`--stage=empty`) prints the same "nothing to draw" notice
  `ColliderOverlay` prints for no collision built, rather than a silently empty overlay.

## src/UI/NodeLab.cs
The node lab (N) in `--freecam`/`--anim-lab`: the world's `cs_name` tree, a search box, per-node
Frame / Hide-Show, a dependency readout for `SelectionService.Current` (anim defs, destructible
pool + DAMAGE_SEQUENCE, geometry/textures, colliders) and a destructibles view with F41's coverage
columns, plus `ExtraRoots` top-level branches for props beside the world content (the anim lab's
`--plane=` prop, so its parts show in the tree, search and `SelectByName` — BL-043).
`--debug-nodelab[=deps,dest,open,node=<cs_name>]` is the scripted twin. A row's text/colour
follow live `Node3D.Visible`, re-read on the panel's 4 Hz status cadence rather than latched off the
hide button, so a def re-showing a hidden node reads visible again on its own (BL-044).
⚠ **Tree children are the nearest `cs_name` descendants** — the exact inverse of the selection
  ladder's ancestor walk; `Select(node)` bypasses the 350 m pick cap (C5 `z3terrain`).
⚠ **A mode-dependent source SAYS it is absent, never shows an empty list** (LOG-1/WORLD-9): the
  collider line prints the not-built notice; a `--node=` slice carries a PARTIAL WORLD banner.
⚠ Destructible rows come from the PROGRAM's `HEALTH>0` defs joined to the registry, so a def that
  bound nothing shows as `UNRESOLVED`; the totals equal the `destructible-census` suite's.

## src/UI/WorldDamageLab.cs
The world damage lab (F5) in `--freecam`/`--anim-lab`: the destructible pools of whatever
`SelectionService` holds, each with live HP, and a slider + Kill + Reset on the one a weapon hit
reaches, driving `AnimRuntime.DamageAt`/`ResetDestructible`. `--debug-damage[=node=,pool=,hp=,kill,
reset,tick=,open]` is the scripted twin (an ordered script, not a token set).
⚠ **Only the pool `DestructibleRegistry.Resolve` names is drivable.** A node can carry several
  `(def, anchor)` pools (C1's water tower: compiled + reader wildcard); the others are listed
  read-only with the reason. Driving a twin damages a pool nothing can ever hit.
⚠ Swap + collider census are read PRE-tick (synchronous), debris POST-tick (scheduled, WORLD-11);
  colliders print `off=`/`on=` separately (WORLD-10) or the not-built notice (WORLD-9).
⚠ **Freecam builds no world-effects runtime until the lab's first damage action asks** — bound
  effect names (death effects, the `sputter_*_obj` stage puffers) render from then on; a def's own
  `PUFFER_STATE` sequences still fire in the log and draw nothing (WORLD-12).

## src/UI/OrbitCamera.cs
The static inspection view's orbit-camera controller (LMB-drag orbit, wheel zoom, AABB framing):
owns the orbit state and drives a `Camera3D` it does not own; `Frame` takes the eye + pivot the host
resolved, and `MergedAabb(Node3D)` merges a subtree's world-space mesh AABBs (shared with the anim lab).
⚠ **`Frame`'s `lookAt` is a PIVOT POINT, not a direction** — with the eye it also sets the orbit
  RADIUS, which the wheel and the drag then work in. A `--direction` cannot be passed through here;
  `GameSession.FrameCamera` synthesizes a pivot on the aim ray first. Collapsing that back to a
  direction (radius 0) leaves the camera spinning about its own eye — measured: a 25° `--jitter`
  swings the parked plane clean out of frame, where the synthesized pivot keeps it centred.
⚠ The FOV read in `Frame` is 50 — the orbit view never runs in `--fly`/`--freecam`, where FOV is 62.
⚠ `MergedAabb` on a meshless subtree returns a zero-size box at the origin — callers special-case
  it — and the nodes must be IN the tree (`GlobalTransform` on a detached node = identity + errors).

## src/UI/AnimLab.cs
The `--anim-lab` debugger: a quiet `WorldSession` stage (`AutoStart=false`, seed pinned), fixed-dt
clock, transport button panel, def picker, `AnimTimeline`, `SpectatorCamera` freecam following the
shared selection, and a staged effect/crash anchor set so placeless on-call defs play at the camera.
⚠ On `SelectionService.Changed`: **frame + follow on a fresh pick, re-follow WITHOUT re-framing on
  a ladder walk** — re-framing every rung would fling the camera out to the zeppelin's radius.
⚠ Ordering is load-bearing: `Play` sets the timeline focus BEFORE `AnimRuntime.Play` (t=0 events
  dispatch synchronously); the playhead is the lab's own accumulation, NOT `GameClock.Frame`; the
  clock hand-off is `AnimRuntime.ManualAdvance`, NOT `SetProcess(false)` (READY auto-enable trap).
⚠ Determinism boundary: puffer spread is unseeded RNG — same-step shots differ in particle noise.

## src/UI/AnimTimeline.cs
The anim lab's authored-vs-fired timeline (custom-drawn `Control`): authored blocks above, fired
ticks below, one lane per Initial sequence; a slanted first-firing connector = scheduler divergence.
⚠ `BuildLane` re-derives the documented scheduling rule independently — deliberately NOT via the
  runtime's `SequenceRunner`. That independence is the whole instrument; don't "simplify" it away.
⚠ Scope: one instance of the played def (first anchor) plus its CALL_ANIMATION children; same-name
  siblings and post-ambient children are untracked; ticks cap at 600/lane, Restart clears.

## src/SessionPaths.cs
Static resolver for the extracted-data paths (`ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr`) under a data root, plus `PreferUnzipped` (an unpacked sibling dir beats its `.zip`).
⚠ Pure path arithmetic — the only I/O is `PreferUnzipped`'s directory-exists probe.
⚠ The `--gamez=`/`--textures=` override policy deliberately stays in GameSession; this class only
  builds the default extraction-tree paths.

## src/SessionSpec.cs
Everything the command line settles about a session, as one immutable record: `Parse(args)` parses
**and resolves**; the pure arg parsers (`ParseVec3`, `ParsePlanes`, `ParseHold`, …) are public so
they are testable. `SessionMode` is closed — Menu/Fly/Viewer/Freecam/AnimLab — with modifiers and
`SessionProbe`; per-rule coverage lives in `CSVM.Tests/SessionSpec*Tests`.
⚠ **Step order in `Resolve` IS the behaviour**: `--stunt` moves `Scenario` before arbitration can
  clear `Stunt`; the freecam/anim-lab-only debug tools are dropped after `--node=` has forced the
  viewer. Both look like tidying chances.
⚠ **Pure — no engine state, no globals, no logging, no clock** (DET-9: a spec that read the clock
  would not be a function of its args); complaints go to `Warnings`, never a print.
⚠ **`FromMenu` is static, takes its base as a PARAMETER, and does NOT re-resolve** — it must keep
  writing every menu-settable field, or the pristine base re-opens the carry-over bug.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram` — the world+anim half of a session build;
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights.
⚠ Disposal contract, **one flag per archive because the two lifetimes differ**: nulls `PufferFactory`
  unless `Options.TexturesOutliveBuild` and the sound loader (prewarming first) unless
  `SoundsOutliveBuild`. A game session owns the TEXTURE archive all session (so this is always true
  there — a false left the world with no runtime fire, trails or dust, `BL-234`) and scopes the SOUND
  archive to the build; only the lab owns both. A cleared factory now warns once at the first late
  `PUFFER_STATE` — the bootstrap census cannot report a runtime miss.
⚠ **The phase boundaries are a reported contract** (`StartupProfile` spans zrdr/world/clutter/anim/
  bind/prewarm): move a step, move its `Record` — a dropped phase reads as a growing `rest`, not as
  missing. Keep them leaves.
⚠ `Options.NodeSubtree` skips MissionSetup deliberately — the one verb that reliably resolves is
  the one that switches the subject off (C1/IA1 hides `hk_zep`); a node stage shows the subtree in
  its gamez base state.

## src/Mech3/EmptyStage.cs
The `--stage=empty` test stage: a flat collidable 20 km ground plane under a 100 m grid, standing in
for a chapter world so flight/ballistics runs boot in ~2 s with nothing else in the frame.
⚠ The grid texture is DRAWN pixel-by-pixel here. Never load one — the repo ships no assets, and a
  test stage is the easiest place to break that rule by accident.
⚠ The collider is a sunk `BoxShape3D` whose TOP face is y=0, not a `WorldBoundaryShape3D` and not a
  trimesh: the weapon and airframe raycasts want a definite thickness under the surface.
⚠ It carries the `cs_name` meta (`ground`) like a built gamez node, so the impact log and the node
  labels read a real name off it (`on ground/col`).

## src/Session/GameSession.cs
The per-launch session node: `Session.Launcher` instantiates one per launch with
`(SessionSpec, LauncherContext)` and runs `StartSession()` — an ordered sequence of phase methods
sharing one `BuildState`; menu and CLI share that one build path. Owns the session `GameClock`
and `StartupProfile`; delegates to the `src/Session/` clusters (LiveryResolver, SpawnPicker,
PlaneRoster, FlightRigAssembler, WorldEffectsFactory, WeatherRig) and to `Testing.ProbeRunner`/
`CaptureDirector` on the Launcher — read `src/Session/Launcher.cs`'s entry too before touching the
build's edges. `BuildsCollision` is the only spelling of "does this session build colliders".
⚠ **It parses no args and resolves nothing** — the Launcher hands it the one `SessionSpec` its
  session is built from; **a new flag is a SessionSpec change**. `_menuPads` is the deliberate
  exception: join-flow session state riding the `LauncherContext`, never the spec.
⚠ **There is no `Teardown()` — return-to-menu is `QueueFree`**; the session subtree frees
  atomically under `_worldRoot`. Only three non-child duties run in `_Notification(ExitTree)` —
  null `GameClock.Current`, `Dispose()` `_worldLights` + `_sessionTextures` — all null-guarded
  (no double-free after a failed build) and race-free (menu relaunch is a frame later).
⚠ **`--pos`/`--direction` are routed by mode in ONE place** (`ResolvePlacement`): flight gets
  `_spawnAt`/`_spawnDir`, everything else `_camPos`/`_camDir`. **Never fold `_camDir` into
  `_lookAt`** — `--lookat` is a POINT, `--direction` a vector; only flight converts one to the other.

## src/Utils/GameClock.cs
The session's simulation clock: `BeginFrame(wallDelta)` (first thing in `GameSession._Process`)
sets `Steps` + `Dt`; consumers read `FrameDt`, or loop `Steps` times on `Dt`. Modes: Realtime,
FixedAccum (interactive anim lab), FixedStep (scripted runs / `--det`); `Halted` is orthogonal.
Published as `GameClock.Current` (session-scoped, nulled on teardown; null = raw frame delta).
⚠ Realtime is arithmetically what every consumer used before this class — that is what keeps the
  shipped modes byte-identical.
⚠ `PhysicsDt` returns 0 in every non-realtime mode and while halted, and **0 means the consumer
  returns without stepping** — `GameSession.DriveSimSteps` calls its `SimStep` instead, `Steps`
  times, in the tree order Godot's physics tick used.
⚠ The GPU reads the same clock through `ShaderTime`/`csky_time` — a halt is a true freeze-frame
  (measured: frames 120 and 300 of a halted C1 waterfall are md5-equal); any new animated shader
  takes `csky_time`.

## src/Utils/Log.cs
The diagnostic log: `Log.Info("world", $"…")` / `Warn` / `Error` / `Debug` over nine categories
(`anim world flight weapons sound perf test ui core`) and four levels. Two sinks with different
jobs — the console is the human's, the `.scratch/logs/<mode>-<stamp>.log` file is the machine's.
`Log.ConsoleSink` (`Action<string>?`, default null) overrides where console lines go; null means
`GD.Print`/`GD.PrintErr` as before. Installed by a test host so a plain (non-`Node`) class that
logs is callable from `CSVM.Tests` without an engine.
⚠ **The file sink always takes EVERYTHING** — every category, every level, no filter, and is
  untouched by `ConsoleSink`; `--log=` only moves the *console* threshold, so a post-hoc grep can
  never miss a category.
⚠ **No timestamp column, deliberately** — a `--det` run must produce a byte-identical log; a line
  that needs time carries it as an explicit `key=value`.
⚠ **Migration is incremental by decision — do NOT bulk-sweep the remaining `GD.Print` sites** (a
  bulk text rewrite has corrupted files here before, verification SHELL-3); a family converts when
  an item touches it.

## src/Utils/ShaderTime.cs
The GPU's view of the clock: the `csky_time` global shader uniform (seconds), registered once in
`Launcher._Ready` and written once per rendered frame from `GameClock.Time`. Every animated
shader this project generates reads it instead of Godot's `TIME`.
⚠ `RolloverSecs = 3600` is a CONTRACT, not a tuning constant: it matches Godot's
  `rendering/limits/time/time_rollover_secs`, and every UV scroll rate in this install
  (0.07/0.4/0.5/0.7/1.0 u/s) × 3600 is a whole number of texture repeats, so the wrap lands on an
  identical frame. Measured: a +3600 s and a +7200 s offset render the C1 waterfall pixel-identically,
  +1234.5 s moves 43.8% of it.
⚠ With no session clock the value keeps advancing on the wall delta from where the last session
  left it — the menu must not freeze, and the uniform must never sit pinned at 0.
⚠ Declared in `res://shaders/csky_time.gdshaderinc`, one include shared by every shader that reads
  it: a global uniform's TYPE must agree across shaders, so it is declared in exactly one place.

## src/Utils/StartupProfile.cs
The always-on startup timing report: one `[perf] startup mode=… <subject> total=… boot=… <phases…>
rest=… first_frame=…` line per session build. `Mark()`/`Record(phase, mark)` are ambient statics over
`Current`, so the shared build code (`WorldSession`, which the test harness also drives) records blind.
⚠ The line asserts `total = boot + Σ(phases) + rest + first_frame` — keep every phase a LEAF or
  the sum silently double-counts; `rest` = real uninstrumented work, not an error term.
⚠ `first_frame` is measured at the top of the SECOND `_Process` after the build; a probe run's
  `rest` also holds whatever the probe itself did — don't read it as build overhead.
⚠ `boot` is engine start → build start, so on a launchscreen-driven rebuild it also holds however
  long the menu was up.

## src/Utils/Rng.cs
The session's randomness policy: one master seed and ten named subsystem generators derived from it
(`weapons`, `flightaudio`, `spawn`, `paint`, `anim`, `crash`, `effects`, `puffer`, `clouds`,
`precip`). `Reset(master, pinned)` runs once per session build, before anything draws;
`Stream(name)` is the shared generator, `SeedFor`/`IntSeedFor` the pure seed, `NewIntSeed`/
`NewSystemRandom` a per-instance stream off the subsystem's own.
⚠ A subsystem's seed is `splitmix64(master ^ fnv1a(name))` — **independent across subsystems**, so
  adding a draw in one cannot shift another's sequence; only order WITHIN a subsystem matters, and
  the fixed `GameClock` pins that. Never derive a seed from `string.GetHashCode()`: .NET randomizes
  it per process, which is exactly the non-determinism this class removes.
⚠ Unpinned (no `--det`, no `--seed`, not `--anim-lab`/`--effects-test`) the master comes from
  `TimeSeed()`, so the shipped game keeps its variety — a bare `--fly` still gets a random spawn and
  random liveries. That boundary is the reason nothing here branches on `Pinned`. A scripted flag
  (`--screenshot=`, `--dump-*`, `--damage-test`) implies `--det`, so those runs are pinned to 1.
⚠ `Reset` also calls `GD.Seed(master)`: the net for any draw not yet routed through a named stream.

## src/Testing/Probes.cs
The assertion cores behind the `--dump-markers` / `--dump-weapons` / `--dump-loadout` /
`--dump-flight` / `--dump-mips` / `--damage-test` inspection reports. Each probe does the work
once and returns both halves: the report text the flag prints and writes, and a structured verdict
(counts, per-row booleans, failure strings) a `--run-tests` suite asserts on.
⚠ `FlightEnvelope` steps a throwaway `FlightModel` through the manoeuvres the ORIGINAL was
  recorded flying; its targets are the Bloodhawk's only, since it is the only airframe on video.
  A row with `Informational` set is measured but deliberately not asserted (an open question) —
  never promote one to a verdict without the measurement that closes it.
⚠ **One source of truth.** The flags in `ProbeRunner` are thin wrappers over these; a check added
  to a probe reaches both the report and the suite. Never re-implement a check in a suite.
⚠ `Probes.SweepCap` (16) caps the swept ROWS, not the registry totals — a census must read
  `DamageResult.TotalInstances` / `DistinctAnchors` or it silently under-counts (LOG-5).

## src/Testing/TestHarness.cs
`--run-tests[=filter]`: the suite registry, `TestContext` (assert verbs, resolved data paths, a
scene-tree host, and `WithWorld` — the chapter-world builder over `WorldSession`), the
PASS/FAIL/SKIP table, `.scratch/test-report.json`, and the process exit code.
⚠ **In-engine is the smaller half.** Only checks that need a live Godot belong here; anything
  that runs without the engine goes in `CSVM.Tests` (`dotnet test`) instead.
⚠ **Engine-error policy.** Native `ERROR: …` lines are screened out of band against
  `ErrorAllowlist`; **every allowance carries a cap and its measured count is printed even on a
  pass**. Unknown error → fail; over cap → fail; no log → SKIP, never PASS.
⚠ Run **windowed**: `--headless` compiles no shaders, so a clean error screen says nothing about
  them (LOG-8).

## src/Testing/Suites.cs
The 18 registered in-engine assertion suites cover typed weapon data, blast/fuse rules, the original's
flight envelope, plane/loadout bindings, live weapon fire, destructible stages/death/census, animation
stops and bounce-terminated launches, texture flattening, glTF round trips, collision/node
visibility, and authored stunt gates.
⚠ Expected numbers are **golden counts against the retail install** (48 weapon defs, 11 airframes,
  per-chapter destructibles); change one only with the measurement that moved it.
⚠ `bounce-launch` asserts a **band**, not a time: the launch draws speed and elevation per instance,
  and the draw moves with suite order (the same run gave `part4` 4.083 s filtered and 3.883 s in the
  full sweep). Its zero-miss checks are carried invariants the seed does not discriminate — the
  suite is shown able to fail on the solve and the dispatch only (BL-240).
⚠ `weapons-fire` asserts `skipped == 0` as well as `ok == 48`; a skipped mount is not success.
⚠ `--loadout=<def>` reaches `loadout-bind`; `--run-tests=loadout-bind --loadout=pbloodhawk` is its able-to-fail cross-bind.
## src/Testing/GoldenShot.cs
The engine half of the golden-image tripwire: `PixelHash(Image)` (md5, lower-case hex) and
`Adapter()` (`"<gpu> / <api>"`). Called at the `--screenshot` save site, which prints
`[core] shot pixmd5=… size=… gpu=…` on every capture; `RunTests.ps1`'s `goldens` stage parses that
line and compares against `analysis/goldens/manifest.json`.
⚠ **Hash the raw buffer, never the PNG.** `Image.GetData()` only — encoded bytes differ between
  pixel-identical images (SHOT-6), so a file hash reports encoder state.
⚠ **The hash is a property of this GPU.** A driver change moves every shot at once; the adapter
  travels on the same line precisely so that case is readable rather than mysterious (GOLD-1).
⚠ The comparison lives in PowerShell, not here: the suites in `TestHarness` run inside one `_Ready`
  call and never yield a frame, so no in-engine suite can photograph anything.

## src/Testing/ProbeRunner.cs
The `--dump-markers`/`--dump-weapons`/`--dump-flight`/`--dump-loadout`/`--dump-mips`/`--run-tests`/
`--effects-test`/`--damage-test`/`--destroy=` probe wrappers,
constructed once in `Launcher._Ready` after the base paths settle (B7) — the Launcher dispatches
the `--dump-*`/`--run-tests` early quits itself and hands the runner to each session node.
Each method reads a `SessionSpec` passed **per call**, not stored — a menu launch can replace the
caller's spec between calls, so a cached one would silently answer with a stale launch's flags.
⚠ **No back-reference to the host node.** `RunTestSuites` takes the parent `Node` (to host its
  throwaway `TestHost` world) and the `Camera3D` as parameters and returns the exit code plus the
  fixed-step `GameClock` it created via `out` — the caller assigns its own `_clock` field and
  calls `GetTree().Quit(code)` itself. `RunEffectsTest` likewise takes the camera and the caller's
  `EffectAnimNames` table (`WorldEffectsFactory.EffectAnimNames`, passed in per call).
⚠ `ApplyRocketOverride` and `TriggerDestroy` are static (no instance state) — call them as
  `Testing.ProbeRunner.X(...)`, not through `_probeRunner`.
⚠ `WriteScratch` is the one shared write path to `.scratch/<report>.txt`; `GameSession`'s
  `--weapon-test` report (not itself a moved wrapper) also writes through `_probeRunner.WriteScratch`
  rather than duplicating the helper.

## src/Testing/CaptureDirector.cs
The `--screenshot=`/`--shots=`/`--frames=` state machine plus F11/F12's placement print and ad-hoc
save, constructed once in `Launcher._Ready` from the launch spec
(process-scoped, never re-armed by a menu relaunch); `Tick()` runs from the Launcher's `_Process`
(B7), which is what keeps `--menu --screenshot` capturing the launchscreen with no session node
alive. No back-reference to the host node — `Tick`/`PrintPlacement` take the
camera/orbit/rigs/clock/plane/menu-visible they need as parameters.
⚠ **`--frames=N` is a sim coordinate, not a wall-clock delay** — `Tick`'s warm-up countdown
  decrements exactly once per `_Process` call, in the same place in the frame GameSession's inline
  block used to; move that decrement anywhere else (an early return above it, a second call path)
  and every golden lands on a different sim frame. `Pending` (was `_pendingShot != null`) is the
  predicate every other `--screenshot`-conditioned choice elsewhere reads — never re-derive it from
  the spec, since a burst clears it mid-session.
⚠ `Vec3Arg`/`DirArg`/`SaveScreenshot` are static — call them as `Testing.CaptureDirector.X(...)`,
  not through `_captureDirector`; `FrameCamera`'s orbit-pivot log line is the one call site outside
  the capture/placement paths.
⚠ **`Tick`'s `GetImage()` can come back null** (a renderer with no GPU context, e.g. `--headless`)
  — it quits nonzero instead of NRE-looping forever (BL-049); `Launcher._Ready` rejects the known
  `--headless`+`--screenshot` combo earlier, so this is the backstop for a future renderer-less path.

## src/Testing/GltfExporter.cs
Exports the viewer plane's `Node3D` subtree to a glTF file — mesh + the currently painted livery
texture, current damage state baked in, no animation. `Export(plane, path)` works on a throwaway
`plane.Duplicate()`: it frees every hidden `Node3D` (the panel/flare `Visible` toggles are how damage
is baked) and the point-sprite `"lights"` instances, converts each surface's custom `ShaderMaterial`
skin to a `StandardMaterial3D` (painted `albedo_tex` + vertex-colour-as-albedo, mirroring
`PlaneBuilder.FlareMaterial`), then `GltfDocument.AppendFromScene` + `WriteToFilesystem`. Format is
extension-driven (`.glb` default). Two triggers: the `--export-gltf=` one-shot via the frame-stepped
`Tick()` (waits for the plane, exports, quits with the write's success as the exit code), and F10 in
the Launcher.
⚠ **Export must never mutate the live scene** — all pruning and material overrides happen on the
  duplicate, so golden screenshots are identical after an export. Convert on the node via
  `SetSurfaceOverrideMaterial`, never on the shared `ArrayMesh`.

## src/Session/Launcher.cs
Main.tscn's root: the once-per-process bootstrap — CLI parse into `_cli`/`_spec`, data-root
precedence, `Pads.Disabled`/`TextureDropIn`/`Log`/master-seed side effects, the
`--dump-*`/`--run-tests` early quits, the `--headless`+`--screenshot` rejection (after `Log.Open`,
so the message actually lands somewhere) — plus everything that persists across in-process relaunches
(camera, orbit rig, sun, WorldEnvironment, launchscreen, focus mute, the per-frame shader clock /
`--perf` / capture tick at priority -999). `LaunchSession()` instantiates a `GameSession` per
launch; `ReturnToMenu` `QueueFree`s it; a menu launch derives its spec via
`SessionSpec.FromMenu(_cli, …)`, never from the outgoing spec.
⚠ `GlobalShaderParameterAdd` runs in `_Ready` ONCE — a session rebuild must never double-Add
  (that errors; `WeatherRig.Build` only `Set`s).
⚠ **A scripted session HIDES its window** (`ScriptedWindow.Hide()` = `ShowWindow(SW_HIDE)`) —
  **never swap that for minimize**, which stops rendering and blanks every capture (SHOT-16).
⚠ F11 prints the SUBJECT, per mode (flight: player 1's plane pose, not the chase camera);
  directions print to 5 decimals — 3 would quantise a unit vector's aim to ~0.03°.

## src/Session/LiveryResolver.cs

## src/Session/LiveryResolver.cs
Resolves which livery each player flies: the shipped paint catalog (`PaintCatalog`, lazy + cached), the per-pattern
region-mask library (`Patterns`, lazy + cached), `PatternsForPlane`, and the per-player
`SchemeFor` pick that reads a `SessionSpec`'s `--paint=`/`--paint-color=`/`--paint-decal=`
overrides. Constructed once per session build (`_liveryResolver` in `GameSession.StartSession`,
never across a menu rebuild — a relaunch gets a fresh instance over the fresh `_spec`).
⚠ **`NewPaintRng`'s draw order/count is load-bearing under `--det`.** Liveries are seed-pinned
  (the master seed's paint stream, or `--paint-seed=` explicit); constructing or advancing the RNG
  a different number of times, or in a different order relative to the other per-session RNGs,
  reshuffles every pinned livery and moves golden hashes. Verified unchanged by the 11 goldens.
⚠ `SchemeFor`'s `index` parameter is the PLAYER index into `_spec.PaintNames`/the paint RNG draw
  order — keep call sites passing the same per-player index they did before the move.

## src/Session/SpawnPicker.cs
Resolves each player's flight spawn:
`ChooseSpawnBase` (the shared `--spawn=`-or-random list index), `ChooseSpawn` (a player's
position/look-at from that list, objectives.json `PLAYER_INIT`, or the `--spawn-at=` debug
override), and `LogSpawn`. Constructed once per session build (`_spawnPicker`, same lifetime as
`LiveryResolver`).
⚠ `ChooseSpawnBase`'s random branch draws from `Rng.Stream(Rng.Spawn)` — under `--det` this is
  pinned by the master seed same as before the move; do not reorder relative to other RNG draws.

## src/Session/PlaneRoster.cs
Static, spec-free lookups over a `SessionSpec`'s plane roster: `PlaneFor(spec, index)`,
`PlaneDisplayName(stats)`, `Humanize(s)`.
No session state — every call takes the `SessionSpec` explicitly rather than caching one, since
these are pure over their arguments.

## src/Session/EffectPools.cs
The `CSVM/data/effect_pools.json` reader — how many copies of each effect-template ROOT the
world-effects stage builds (`BL-225`; the numbers are `BL-231` in the TUNE list). Hand-authored
engine config, in a file rather than a `const` precisely because it is **invented**: the original
copies its template per CALL_ANIMATION and has no such number, so any finite pool is our
approximation and the user must be able to move it without a rebuild.
`SlotsFor(root, players) = clamp(base + perExtraPlayer × (players − 1), 1, maxSlots)` — the
per-player term is what keeps splitscreen/multiplayer from collapsing back onto one copy, since every
extra aircraft is another gun and another rocket landing somewhere else. `DepthFor` is the deepest
root = how many slot containers the stage needs; `UnknownRoots` names an authored root that is not in
`WorldEffectsFactory.EffectStageRootNames`, since a typo would otherwise size nothing silently
(asserted in `EffectPoolsTests` against the live table).
⚠ `Parse` (bytes → sizes) is deliberately separate from `Load` (file IO + engine warnings): the
  sizing decision is pure and unit-tested without a session, and a missing or malformed file warns
  and falls back to `EffectPools.Fallback` rather than failing the launch — the same policy `Config`'s
  `const` defaults have. Keep the fallback values in step with the shipped file.
⚠ The three gun-impact roots are sized **1** on purpose (C8 throttles the family to one play per
  0.1 s per name and bounds each to 0.3 s): pooling them buys copies nothing uses. Raising them
  belongs with removing that throttle, which is its own step with its own emitter-count check.

## src/Session/FlightRigAssembler.cs
Assembles one player's flight rig: the painted plane model, the `FlightController` and everything hung
on it — loadout/ordnance, compass, gauges, HUD font test/weapon readout/reticle, damage visuals,
audio, this player's stunt run + marker/scoreboard/race entry, the spawn placement, and the crash
runtime built after the controller joins the tree. Constructed once per session build from
`(SessionSpec, LiveryResolver, SpawnPicker, WorldEffectsFactory, worldRoot, Inputs)`, then
`Assemble(pi, rig)` once per rig; `MeshInstances`/`WhatSuffix` accumulate across the rigs for the
caller's build summary.
⚠ **Call it in ascending player order.** `Inputs.PaintRng` and `SpawnBase` are shared streams — the
  livery draw and the spawn index wrap are order-dependent, so reordering or parallelising the rigs
  silently repaints and respawns the whole field.
⚠ **The loadout (and its `--rocket=` override and `Ordnance`) is bound before the controller enters
  the tree** — `FlightController._Ready` builds the fire state and the ordnance-type list from it.
  Same for `FontTest` (its `_Ready` adds it to the HUD canvas). `_worldRoot.AddChild(controller)` is
  therefore near the end, and the crash runtime is the only thing built after it.
⚠ **`Inputs` is set once and never mutated per rig** — it is the "shared" half of the old loop's
  local graph, made explicit. A value that differs per player is a local in `Assemble`, not a field
  here; a new shared load belongs in `GameSession.BuildFlightRigs` and a new `Inputs` field.

## src/Session/WorldEffectsFactory.cs
Builds the impact/destruction effect stages and the per-player crash runtime: the world-effects runtime (D32) and
`BuildFlightCrashRuntime` — which despite the name binds every def that plays ON one aircraft: BOTH
crash variants' closures (`player_crash_dirt` + `player_crash_water`; the surface is only known at
impact, so both are bound and `FlightController.ClassifySurface` picks) **plus**
`PlaneDamageEffectAnims` (the four `<part>_damage_effects` shims →
`random_gun_impact` → `yellow_sparks_follow`), because those need exactly what it already has — the
`player` anim root, the plane's own `pdpN` panels as INPUT_NODEs, and a live puffer factory.
`EffectAnimNames` binds impact + death effects — including the 12 gun `*_gunhit` variants, which a
gun hit plays throttled and time-bounded (C8) — **and** the
`DAMAGE_SEQUENCE` stage pair `sputter_black_smoke_obj`/`sputter_fire_smoke_obj` (root
`partial_damage_obj`, staged via `EffectStageRoots`) **and** the airframe's three graze reactions
(`touchdown_default`/`_dirt`/`_water`, roots `spark_touchdown`/`dust_touchdown`/`splash_touchdown`
+ `yellow_spark_01`, played by `FlightController.GrazeReaction`). Its `Subset` handles 8/30 destruction targets; 22 live-object choreography names remain local (`analysis/death-effect-closure/`).
The stage-call closure excludes C4's train-anchored `b_steamtrail`. Constructed once per session (`_worldEffectsFactory`, same lifetime as
`LiveryResolver`/`SpawnPicker`) from `(SessionSpec, Node3D worldRoot, Func<Vector3> playerPosition)`.
The effects runtime's puffer factory passes `softParticles: false` for MIX-ramp states — these effects
emit at ground-level sites, where the depth fade zeroes fresh dark puffs against the terrain (the
crash-smokeball lesson; the damage-stage smoke measured near-invisible with it on) — and keeps the
soft edge for additive fire. Templates build with collision suppressed and the stage is visible with
each ROOT hidden (`AnimRuntime.ShowPlacedTemplates` reveals one while an effect plays on it), so a
template's meshes render — the rocket's per-type explosion rings, the fireball facades (D31).
The world-effects stage is built in **pool slots** (`BL-225`): each root is staged in as many copies as
`EffectPools` sizes it for this session, one copy per `pool<N>` container stamped with
`AnimRuntime.PoolSlotMeta`, so overlapping calls to one effect each get their own copy (see
`AnimRuntime`'s pool paragraph for how a call picks its slot). The containers carry no `cs_name` and
are invisible to name resolution. Sizes are **per root and per player count**, so the deeper slots
hold only the roots sized that deep and a def whose root has no copy in its slot falls back to one
that exists (`TemplateRootsFor` picks by modulo — never "all of them", which would be the collapse
again). The build line names the sizes, not just the total, because a bare count cannot say whether a
root someone just re-sized actually got its copies.
⚠ `EffectStageRoots` must stay the WHOLE anchor-root set of `EffectAnimNames`' call closure, and
  `EffectTemplateRoots` the same for the crash rig's two variants — a def anchors on the node its
  NAME names, so an omitted root leaves it unanchored and it plays nothing, silently. Staging 19 of
  28 cost the rings, all four trail columns, the sonic puffs and the torpedo ripple; regenerate
  either list with `analysis/effect-anchor-roots/` (it takes the anim names), never by hand.
⚠ **Runtime ownership stays split, by design.** The factory's own `_worldEffects` field is the ONE
  lazily-built world-effects runtime (`EnsureWorldEffects` builds it on first demand and caches it
  there); `GameSession` no longer mirrors that reference — the runtime node hangs under `_worldRoot`,
  so freeing the session node on `ReturnToMenu` frees it too, and the factory itself is discarded and
  rebuilt fresh next `StartSession`, same as `LiveryResolver`/`SpawnPicker`. Do not add a
  `GameSession`-side cache of the runtime "for symmetry" — it would be a second place to keep in sync
  with the factory's.
⚠ `BuildEffectStage`, `BuildCrashAnchorSet` and `EffectAnimNames` are `public static` (no session
  state) — `GameSession`'s anim-lab stage and `--effects-test`'s `ProbeRunner.RunEffectsTest` call
  them as `Session.WorldEffectsFactory.X`, not through the instance.

## src/Session/WeatherRig.cs
Loads/applies the flown mission's weather and drives its per-frame rig state: `LoadWeather`/`SetupWeather` become
`Build`, and the per-rig skydome/whiteout/deck/puff update block from `_Process` becomes `Tick`.
Constructed once per session (`_weatherRig`, same lifetime as `LiveryResolver`/`SpawnPicker`/
`WorldEffectsFactory`) and discarded with the session node on return-to-menu — its per-rig nodes
hang under `_worldRoot`, so the `QueueFree` of the session frees them; `_Process`'s `_weatherRig?.Tick`
null guard covers the frame before that deferred free lands (it can never be null mid-session).
⚠ **The horizon (skydome) build loop stays on `GameSession`** — it's a `SceneBuilder` concern, not
  weather state. `Build` takes it as a `buildDomes` callback, invoked between resolving the zone and
  applying fog/whiteout/puffs/precip, at exactly the point the original inline code ran it — do not
  reorder `Build`'s three steps (zone → domes → setup) relative to each other.
⚠ **`GlobalShaderParameterSet`, never `Add`.** `GlobalShaderParameterAdd` runs once per process in
  `Launcher._Ready`; `Build`'s fog/whiteout writes must stay `Set`-only, or every in-process menu
  relaunch that flies a second foggy mission crashes on the duplicate `Add`.
⚠ `SetDeckCenter` is called separately from `Build`, whenever a chapter's cloud deck geometry loads
  (`GameSession`'s `cloudDeck != null` branch) — broader than "this rig has weather", so it is
  guarded with `_weatherRig?.SetDeckCenter(...)` rather than assumed non-null.

## src/Utils/Config.cs
Dev-facing tuning-override layer: static `Config` parses an optional sparse `res://config.json`;
the typed getters (`GetFloat`/`GetInt`/`GetBool`/`GetString`) return the file's value for a present
key, else the caller's in-code `const` default — read-through at the point of use, keys
`moduleCamelCase.fieldCamelCase`, grouped one nesting level in the JSON and flattened to dot-keys.
⚠ Absent file / absent key / wrong-typed value all fall through to the passed default, returned
  **verbatim** — so no config.json ⇒ behaviour byte-identical to the consts (scripted shots stay inert).
  Malformed JSON / non-object root → one error line, no overrides, never throws.
⚠ Every getter self-registers `(key, default)`. `--dump-config` emits that registry as a full
  nested template; `ReportOrphans` warns loudly about file keys no getter queried (the typo
  detector); `WarmTuningRegistry` steps a throwaway `FlightModel` once per launch before
  `ReportOrphans`, so both are complete with **no built world / no game data**.
⚠ Read-only — nothing writes the file; `res://` was chosen so a writable `user://` layer can later
  stack UNDER the getters without touching a call site. `config.json` is git-ignored — the consts
  stay the canonical values.

## src/Utils/ScriptedWindow.cs
Win32-only window hiding for scripted runs: `ScriptedWindow.Hide()` calls `ShowWindow(SW_HIDE)` on
the native window handle. Fully static, one call site in `Launcher._Ready` right after the `--det` block — the same
predicate drives both window hiding (scripted run) and focus request (interactive run).
⚠ Hiding is not minimizing: a minimized window stops rendering, which blanks every screenshot
  capture. `ShowWindow(SW_HIDE)` is load-bearing — never swap it for minimize.
