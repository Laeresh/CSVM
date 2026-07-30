# Godot project — per-module implementation notes

**Start at the module index below.** One routing line per module, grouped by namespace. Find the
module there, then read only its entry: `Grep "## src/Mech3/SceneBuilder.cs" -A 12` returns the
whole thing, because the entry shape guarantees it. **Never read this file whole** — it is ~140 KB.

One `## src/...` entry per module in `CSVM/src`. Entry shape: 1–2 sentences of purpose beyond the
index line, then every still-binding constraint or deliberate-design marker as a `⚠` one-liner.
Body ≤ ~8 lines (~12 for the heaviest modules). Entry order is historical, not grouped — the index
is the map, grep is the lookup.

Narratives, diagnoses, and landed-work stories do not live here: they get a short dated entry in
`HISTORY.md`, and git history keeps the rest. Knowledge about the game's data formats belongs in
`docs/formats/`, not here.

⚠ **A new or renamed module updates the index and its entry in the same edit.** Both are in this
  file precisely so they cannot drift apart; `CLAUDE.md` carries only the namespace-level map and
  must not grow a per-module list again.

## Module index

### `src/Mech3/` — extraction readers, world and scene building

Everything that turns the player's install into a live scene: the mech3ax extraction readers, the
GameZ→Godot builders, and the animation runtime that drives the world.

- `src/Mech3/GameZ.cs` — GameZ extraction loader (zip or dir): nodes/models/materials/textures JSON → C# objects, either extraction shape.
- `src/Mech3/TextureArchive.cs` — texture lookup (zip or dir): resolves the name quirks, classifies each texture's alpha (soft vs hard).
- `src/Mech3/SceneBuilder.cs` — shared GameZ-subtree → MeshInstance3D builder: triangulation, LOD, depth bias, billboards, fog, UV scroll.
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
- `src/Flight/Projectile.cs` — `ProjectilePool`: the weapon-fire subsystem — ballistics, tracers, flashes, per-surface impact, damage to destructibles.
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
- `src/Flight/DamageLab.cs` — the viewer's `--damage` slider UI: one HP slider per part driving flight's own DamageVisuals.
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
- `src/UI/WeaponLab.cs` — the `--viewer` weapon lab (W): guns from gun groups, hardpoints from pylons, at a stand-in target; `--weapon-test` fires all 48.
- `src/UI/NodeLabels.cs` — floating `cs_name` labels over scene nodes (T): Off/Meshes/All, anchored on mesh centres, de-cluttered.
- `src/UI/MarkerOverlay.cs` — the `--viewer` firepoint/pylon/target overlay (K, `--markers`): coloured gizmos + de-cluttered labels.
- `src/UI/SelectionService.cs` — the shared `--freecam`/`--anim-lab` selection: click-pick + the `cs_name` ancestor ladder, breadcrumb + highlight box.
- `src/UI/NodeLab.cs` — the `--freecam`/`--anim-lab` node lab (N, `--debug-nodelab`): lazy `cs_name` tree, search, frame/hide, dependencies, destructibles.
- `src/UI/WorldDamageLab.cs` — the `--freecam`/`--anim-lab` world damage lab (H, `--debug-damage`): HP slider + kill/reset on the selection's destructible pool.
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
- `src/Testing/Suites.cs` — the nine registered suites and their golden counts (48 weapon defs, 11 airframes, the destructible census, the original's own flight envelope).
- `src/Testing/GoldenShot.cs` — the engine half of the golden-image tripwire: raw-pixel md5 + GPU adapter, printed on every `--screenshot`.
- `src/Testing/ProbeRunner.cs` — the `--dump-*`/`--run-tests`/`--*-test`/`--destroy=` probe wrappers the Launcher and the session node quit into.
- `src/Testing/CaptureDirector.cs` — the `--screenshot=`/`--shots=`/`--frames=` capture state machine + F11/F12, ticked from `_Process`.

### `src/Session/` — the launch/session layer

The `Launcher` scene root, the per-launch `GameSession` node, and the low-coupling session-build
clusters they delegate to (PLAN-planeviewer-split).

- `src/Session/Launcher.cs` — Main.tscn root: the once-per-process bootstrap (args → paths → log/seed/window), shader-global registration, persistent camera/lighting, launchscreen + menu flow; instantiates a `GameSession` session node per launch.
- `src/Session/GameSession.cs` — the per-launch session node (instantiated by `Launcher`): builds one session — rigs, world, plane, HUD, weather — from its `SessionSpec`; return-to-menu `QueueFree`s it.
- `src/Session/LiveryResolver.cs` — resolves each player's livery against a `SessionSpec`: the paint catalog, the pattern-mask library, and the per-player scheme pick.
- `src/Session/SpawnPicker.cs` — resolves each player's flight spawn against a `SessionSpec`: the shared spawn-list index and the per-player point (or the `--spawn-at=` override).
- `src/Session/PlaneRoster.cs` — pure lookups over a `SessionSpec`'s plane roster: which plane a player flies, and its display name.
- `src/Session/FlightRigAssembler.cs` — assembles one player's flight rig: painted plane, `FlightController`, loadout/ordnance, HUD instruments, damage visuals, audio, stunt run, spawn, crash runtime.

### Session root and tests

- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy (span every pad) plus the `--no-pads` switch.
- `src/SessionPaths.cs` — resolves extracted-data paths (per-chapter gamez/texture/zrdr; `PreferUnzipped`); extracted from `GameSession`.
- `src/SessionSpec.cs` — the launch args as one immutable, engine-free value: `Parse` parses **and** resolves (closed `SessionMode`, `--det` bundle, placement, `BuildsCollision`), plus the pure arg parsers.

- `CSVM.Tests/` — the xUnit project (`dotnet test`): engine-free reader units on hand-authored fixtures + `extracted/` golden counts, skipped when absent.

## CSVM.Tests/
The xUnit project `dotnet test` runs (net8.0, `ProjectReference` to `CSVM.csproj`, listed in
`CSVM.sln`). Covers the readers that need no running engine: `Zrdr`/`ZrdrDict`, `WavFile`,
`SoundDefs`, `WeaponDefs`, `Messages`, `MissionTargets`, `SessionPaths`, `GameZ`'s transform
arithmetic, `MarkerRig`, `AnimDefs`, and the Godot-free halves of `StockLoadouts`/`TextureArchive`.
⚠ **The `SessionSpec*Tests` trio is the launch surface's only per-rule coverage.** `…Tests` is the
  resolution truth table, `…ParserTests` the seven value grammars, `…MenuTests` the launchscreen —
  84 facts, each shown able to fail by perturbing its rule. `analysis/session-baseline/` covers the
  same surface as a whole-command-line regression and reports only THAT a row moved; the goldens
  never open the menu at all. Two deliberate defects are asserted as they are and labelled at the
  fact — do not "fix" one to make a test read better; that is a behaviour change needing its own
  item.
⚠ Two input kinds, deliberately separate. `fixtures/` is hand-authored from `docs/formats/` with
  invented `probe_*` names; byte-level inputs (WAV/ADPCM) are assembled in the test code so every
  byte's provenance is visible. **A trimmed piece of a real extraction is still a game asset and
  never gets committed** — `fixtures/README.md` restates the rule at the point of temptation.
⚠ Golden invariants read the player's own install. `CSVM_DATA_ROOT` names a checkout holding
  `extracted/` (the engine's own convention) or the extraction tree itself; when neither resolves,
  `[ExtractedDataFact]`/`[ExtractedDataTheory]` set xUnit's `Skip`, so the runner reports **skipped**
  rather than a silent pass. Golden *numbers* commit; golden *content* never does.
⚠ Anything reaching `GD.*`, `Image`, `FileAccess`, `ProjectSettings` or a live `Node` belongs to the
  in-engine suites instead — `Loadout.Bind`, `Weather.Load`, `SoundArchive`, `Config`, `HudMetrics`,
  `TextureArchive.Find`. Do not refactor a reader to get it in here; that trade was declined by plan.

## src/Mech3/GameZ.cs
Loads a mech3ax GameZ extraction (zip or unpacked dir): nodes/models/materials/textures JSON into
plain C# objects, reading both the v0.6.1 "legacy" and the fork "unified" shapes (field mapping:
docs/formats/gamez.md); `WorldTransformOf` resolves a node's world transform without building it.
⚠ GameZNode.Index is the flat list position, NEVER the unified JSON `index` (1-based, duplicated);
  child_indices are flat positions too — getting this wrong rebuilds the graph without erroring.
⚠ The unified transform `scale` is deliberately ignored (measured unit on every transformed node).
⚠ ModelType/FacadeMode/TextureScroll are unified-only: null/zero on a legacy tree, SceneBuilder
  falls back to its texture-name heuristic. Reading both shapes keeps a v0.6.1 rollback data-only.

## src/Mech3/TextureArchive.cs
Texture lookup over an unzbd texture zip or unpacked PNG dir; absorbs the stored-name quirks
(20-char truncation prefix match, legacy `.-N` renames, the fork's trailing doubled period — see
docs/formats/gamez.md) and classifies each texture's alpha channel via LastHadAlpha /
LastAlphaIsSoft ("soft" = a 0.5 scissor cutout would erase or shred it; drives blend-vs-scissor).
⚠ Unresolved names are reported ONCE via plain GD.Print (MissingTextures), never GD.PushWarning —
  Godot .NET prints a full managed stack trace per PushWarning call and buries real errors.
⚠ IsKnownAbsent (pir_spinner, barngrill — verified absent from the whole extraction) renders
  neutral gray; debug magenta must keep meaning a genuine name-resolution failure, not a data gap.
⚠ `TextureDropIn` (same file) is the `--tex-override`/`--tex-census` hook, and it hooks HERE because
  Find is the one resolve point every consumer goes through. The contract is **RGB bytes only**:
  size, pixel format, alpha channel and mip chain stay the original's, so the alpha class read just
  above the swap — and the blend/scissor variant, cutout silhouette and mip chain that follow from
  it — are what a normal run would have produced (measured: an overridden texture's visible extent
  is the same 114,820 px with the census on and off; a hard-alpha clutter cutout is pixel-identical).
⚠ Census colours are a pure hash of the name, never an assignment order — the map must mean the
  same thing in every chapter and every run. Eight bits a channel leave ~200k colours, so ~0.5 % of
  a chapter's names collide: warn per collision, never resolve it by nudging (that would make a
  colour depend on what loaded first). Counting is chromaticity-based and its counts are LOWER
  bounds; the tolerances and their evidence are in docs/cli.md.

## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order
as depth bias (priority × surface rank × node index → polygon offset).
⚠ Instance-uniform block is an ORDERING CONTRACT — every shader on one
  instance must declare the same block (csky_instance_uniforms).
⚠ A shader with NO instance uniform must not take the preamble
  (16-vec4 per-instance buffer cost).
⚠ Hybrid by design: the 128 blend/scroll/clamp variants stay generated in C#;
  .gdshaderinc holds only the shared blocks.
⚠ UV scroll reads the `csky_time` global (`csky_time.gdshaderinc`), never Godot's `TIME` — the
  uniform is the sim clock's shader-side twin, and it must keep TIME's 3600 s wrap because every
  install rate (0.07/0.4/0.5/0.7/1.0) × 3600 is a whole number of texture repeats.
⚠ The include is emitted ONLY on the scroll variants, so every non-scrolling material's shader
  text stays byte-for-byte what it was (measured: the static plane viewer is md5-unchanged).
⚠ Do not raise DepthBiasPerLevel/SurfaceRankBias/NodeOrderBias — the measured coplanar-separation
  floor is ~1e-6 of view distance; a uniform raise scrambles the authored layering (C5 got worse).
⚠ Never blanket repeat_disable: UV clamp is per-surface (UvsWithinUnitSquare); 54% of surfaces tile.
⚠ `BuildSubtree` is the whole of the `--node=` stage's build — it already takes an arbitrary
  GameZNode, so slicing one subtree needed no new geometry code. It sets the built root's transform
  from the node's OWN `Local`; a caller slicing a nested node must overwrite that with
  `GameZ.WorldTransformOf` or the subtree lands at its parent's origin.

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
⚠ player_fortune ships a pattern name with NO colours (they live engine-side); its catalog entry
  is filled with the Fortune Hunters red 223,0,41 documented in paint.md.
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
(`BuildHorizon` makes the camera-anchored skydome), `fvol*`, `dzpaths`. Splits the overcast deck into
`CloudDeck` (GameSession moves it with the player); hides origin-parked unplaced vehicles.
⚠ Cloud/sky is a TEXTURE test (`IsCloudOrSkyTexture`), never node names. Collision exempts via
  `IsNonSolidSkyTexture` (= that AND NOT `skywal*`, a BUILDING texture) — narrow there only; the
  shared predicate also drives the cloud alpha-blend rule and MapEdgeExtender's tile filter.
⚠ The deck is found STRUCTURALLY (`FindCloudDeck`: flat one-quad tiles bucketed by altitude,
  coverage ≥ `DeckCoverageFraction` of the map area) — a `sky*` texture rule drags terrain into it.
⚠ `ResolveHorizonZone` falls back to the horizon's FIRST zone child — C5 ships no `zone2` (see
  docs/formats/weather.md); the skydome fogs on purpose (FOG_ALTITUDE fade), never shadows/collides.
⚠ `HideUnplacedEntities` needs the BUILT subtree's world AABB (gamez `child_bbox` is LOCAL and
  matches all terrain); one-shot sweeps break motion targets still at origin → `RestorePlacedEntities`.
⚠ `BuildNode` (the `--node=` stage) slices ONE named subtree out instead of walking the world, and is
  deliberately unlike `Build` in three ways, each of which would otherwise erase the subject: no
  `SkipWorldNode` filter (the caller named it, so even `horizon`/`dzpaths` build), no cloud-deck
  split, and **no origin-parked registration** — the transformless vehicle a `--node=` run most often
  asks for is exactly what `HideUnplacedEntities` switches off. It also leaves `_builtWorld` null, so
  `CreateEdgeExtender` correctly returns nothing. `MatchNodes`/`SuggestNodes` do the lookup on the
  SOURCE name (`.flt` optional, case-insensitive), never the Godot name — WORLD-8.
⚠ `DetachedWorldAabb` is the world-frame box of a subtree **not yet in the tree** (from the built
  meshes + node transforms). Use it, not `OrbitCamera.MergedAabb`, before the subtree is parented —
  `GlobalTransform` on a detached node is identity and logs an error per call.

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
⚠ Sprites are NOT collidable — no tree-destruction anim exists in the install (`spruce_destroy*`
  is the Spruce Goose; docs/formats/clutter.md). Solid decorations ARE collidable.
⚠ Collision shapes are SHARED, never expanded per placement (that costs seconds of BVH build): one
  `ConcavePolygonShape3D` per distinct mesh, `BodyAddShape`d at each placement onto per-region
  `clutter_bld_<cx>_<cz>` bodies. They live on the body RID — a `ShapeOwner*` call would run
  `_update_shapes()` and clear them; `SharedShapeMeta` on the clutter root anchors them against the GC.
⚠ Convex/box stand-ins were rejected on the data: the decoration shells fill ≤5% of their bbox — phantom solids.
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
⚠ `ReaderCondition` is the ONE place reader↔compiled unit conversions live (PLAYER_RANGE m→m²,
  ANIMATION_LOD HIGH→2); `AddPufferState` bridges the PUFFER_STATE shape differences.
⚠ `DAMAGE_SEQUENCE` parses into a sequence literally named `DAMAGE_SEQUENCE` (the compiled twin's
  name) — the magic name `AnimRuntime.ApplyDamageStages` invokes; it is otherwise an ordinary
  IF/ELSEIF `ANIM_HEALTH` event list (docs/formats/destructibles.md).

## src/Mech3/AnimProgram.cs
Merges the compiled + reader front-ends for one mission — load both, prefer compiled on collision,
keep the remainder — plus `StartAnims`; `ScriptFor` resolves an event slot to its archive SI script.
⚠ Definition identity is (anchor name, animation name) — the dedupe key reader twins must collide on.
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
⚠ C#-side swapping is deliberate (1–7 cycling materials per chapter): no shader variant, atlas, or same-size assumption.
⚠ Screenshots cannot verify open water (frames differ ~2/255); use `--debug-anim`'s flipbook log.
⚠ Frozen outright while `TextureDropIn.Active` (`--tex-override`/`--tex-census`): each frame is its
  own texture with its own flat colour, so a running cycle would repaint the surface a different
  colour every few frames and a census count would report whichever the shot caught.
⚠ The `EFFECTS` reader (`fire1`/`fire2`) is this mechanism bound to a NODE and is NOT wired up —
  it needs OBJECT_ADD_CHILD (docs/formats/effects.md).

## src/Mech3/WorldSounds.cs
`SOUND_NODE` ambient looping 3D emitters: one pooled AudioStreamPlayer3D per live emitter,
following its host's pose per frame. `PlayOneShot(name, worldPos, rng)` is the one-shot `SOUND`
half (D31): fire-and-forget destruction/impact audio, resolving a `SOUND_GROUPS` name to a member
first; the `Sound` anim event calls it.
⚠ Pooled emitters are never parented into world subtrees — AnimRuntime's FindAll memoization forbids runtime reparenting.
⚠ An emitter is silenced while its host is not visible in tree (the point-light rule) — what makes
  raw emitter counts harmless; a blown-up host pose is silenced + logged (an anim-runtime defect).
⚠ `Prewarm` (SoundNode + one-shot `Sound` names, the latter expanded through `SOUND_GROUPS`) must
  run BEFORE the sound archive closes: the Loader dies with the world build and most events first
  fire at runtime; late failures warn via `AnimRuntime.ReportLateSoundFailure`. Prewarm decodes
  quietly (`Loader(def, warn: false)`) — a chapter archive lacking a referenced WAV is normal.
⚠ One-shot players are fire-and-forget: registered in `_oneShots`, swept in `Tick` once they stop
  (no reliance on the `Finished` signal). `FlushOneShots` frees them for the synchronous damage-test
  harness, which pumps no frames so neither the sweep nor a deferred `QueueFree` ever runs.

## src/Mech3/WorldLights.cs
Packs the animated world's `LIGHT_STATE` point lights into the 2×N RGBAF texture the fullbright
world shader reads as spill (global `csky_light_data`, loop bounded by `csky_light_count`).
⚠ Not OmniLight3D — the unshaded world ignores dynamic lights, and the flare at each light is
  already gamez Facade geometry (a glow sprite would double-draw); spill is the missing behaviour.
⚠ Packed via BitConverter, never Image.SetPixel/Color — world-metre positions would hit the 0..1 clamp.
⚠ Count 0 must leave the spill term exactly vec3(0.0): an unlit world stays bit-identical to pre-lights.
⚠ Beyond `MaxActive` (16), rank by significance (range/distance × intensity), not distance; the
  900→1500 m fade (TUNE) dims a light before the budget can drop it, so nothing pops.

## src/Pads.cs
Single source of truth for gamepads — every reader goes through it, never `Input.GetConnectedJoypads()`.
Owns the phantom policy (span every pad, never `pads[0]`), `Disabled` (`--no-pads`), the focus gate.
⚠ `Disabled` is set by `--no-pads` AND by the `--det` bundle, which every scripted run implies —
  so a `--screenshot`/`--dump-*`/`--damage-test` run reads no pad at all without saying `--no-pads`.
  Interactive runs are untouched: that boundary is the bundle's whole constraint.
⚠ The focus gate is on `For` (the read), NOT `Connected` (the roster) — `For(bound)` never
  consults `Connected`, and an empty roster would un-join menu players (`LaunchMenu.SyncDevices`)
  and break `Pads.AssignPads` at session build.
⚠ `Focused` defaults true and FAILS OPEN (headless runs unchanged); only pads need the gate —
  Godot releases held keys on focus loss, SDL pads are polled regardless.
⚠ Every joy read sits in a `Pads.For` loop except `MenuInput.JoinPressed` (checks `InputBlocked` inline).
⚠ `AssignPads`/`LogPads` moved here from `GameSession` (PLAN-planeviewer-split A3, verbatim) —
  static, no session state; `GameSession._menuPads` still overrides them when the launchscreen's
  join flow bound pads itself.

## src/Mech3/MissionSetup.cs
Parses + applies the per-mission `.gw` interp script that decides which world entities a mission
shows; acts on `NodeSetActive`/`DeleteTree`/`Object3DSetScroll`, counts + reports every other verb.
⚠ Read docs/formats/interp.md before extending: order matters and last write wins, a `FindNode`
  matching nothing is NORMAL (never warn), and `DeleteTree` names its own target.
⚠ `Object3DRotate` is unimplemented on purpose — the data's angle unit is ambiguous; guessing would silently mis-pose props.
⚠ `ScrollByModel(gamez)` runs BEFORE the world build — a scroll rate is part of SceneBuilder's
  material cache key; per MODEL, not node, because the verb writes the model's `texture_scroll`.
⚠ The entity half applies as AnimRuntime bootstrap pass 0, before animation state (engine load order).

## src/Mech3/AnimRuntime.cs
The animation engine: bootstrap passes (mission setup, anchored RESET_STATEs, ON_STARTUP, startanims,
a safety net), then dispatch-table event playback; unhandled event kinds are counted, never fatal.
⚠ `FindAll` is memoized (`_findCache`); adding or reparenting world nodes at runtime must invalidate it.
⚠ `HandledEventKinds`/`PartialEventKinds` are the inspect tools' view of `Dispatch`'s cases and must
  move with them — a kind added there and not here reads as unimplemented in the coverage columns.
  `AnchorsOf`/`FindNodes` are read-only wrappers; both are safe post-bootstrap because the anchoring
  census is closed by then, so a tool asking cannot perturb the bind report.
⚠ `_Process` runs `GameClock.Current.Steps` separate `Advance(Dt)` calls, never one summed step —
  the scheduler resolves per step, so N small steps ≠ one big one. Zero steps (a halted clock)
  means no call at all, not `Advance(0)`, which would still dispatch a frame's worth of t=0 events.
⚠ A no-time loop iteration yields a frame — the test is `_iterScheduledTime`, not the clock; authored
  `Loop` count 0 = INFINITE, normalised to -1 where first read, never at the `_loopsLeft == 0` test.
⚠ Absent tween channels HOLD the live value; `FromToMotion`/`ScriptPlayback` keep rot/scale/origin separate.
⚠ `SpinMotion` re-seeds rest from the current pose — a bounded, deliberately-unfixed drift (backlog).
⚠ One motion per node, last registration wins (`AddMotion`); re-assertion is idempotent — only explicit
  `Stop` tears resources down (`TearDownResourcesOf`), `Start`'s own restart must NOT.
⚠ The safety net matches ONLY `destroyed` — a `*_dest` suffix names healthy destructible groups.
⚠ Crash runtime: `NameResolveFallback` keeps `_byIndex` EMPTY (non-portable ptrs, colliding index
  spaces); `Targets()` never falls back to names; crash puffers must parent at world level.
⚠ `_rng` is the runtime's ONE die — `RANDOM_WEIGHT` verdicts, `SOUND_GROUPS` one-shot picks,
  `MotionRuntime`'s crash-debris scatter. **Every session now sets `Seed`, not just the lab**: the
  world runtime from `Rng.Anim`, each crash rig from `Rng.Crash`, the world-effects one from
  `Rng.Effects`. Route any new dice through `_rng` or a replay stops being identical. `Reseed()`
  re-pins it AND clears the sound groups' recency memory, which lives outside the RNG.
⚠ `ANIM_HEALTH`/`ANIM_HEALTH_RANGE` read LIVE HP via `HealthOf` → `DestructibleRegistry`, not
  `def.Health`; the registry is built in bootstrap pass 1 beside RESET_STATE. Nothing damages HP
  in normal play yet, so every instance is at full health and the read is a no-op today.
⚠ `ApplyDamageStages(instance)` runs a destructible's `DAMAGE_SEQUENCE` against its live HP (C22),
  firing the ONE stage effect for the crossed threshold. It escalates via `DamageStage` (only
  when a deeper threshold is crossed) — do NOT lean on `CALL_ANIMATION`'s live guard for "once":
  a one-shot damage effect (`damage3_mp1zreng11`) finishes and would re-fire without the gate.
⚠ `DamageAt(struck, healthDamage)` is the weapon-hit entry (C23): resolves the struck collider to
  its destructible (`Registry.Resolve`), spends `HEALTH_DAMAGE` (world objects have HEALTH only —
  no armour pool), escalates, and marks it `Destroyed` at zero. Fed by `ProjectilePool.DamageSink`.
⚠ At zero, `RunDeathSequence` plays the def's death via `Start(def)` — its Initial sequences ARE the
  destruction (the swap sequence's name varies: `destroyit`/`destroy_h2twr`/unnamed, always Initial,
  so don't pick by name). `ApplyDeathSwap` is the fallback for the ~10% (AA guns) that declare the
  healthy/destroyed pair but author no swap: flip the roles the def's own RESET_STATE named, only
  when RESET declares a `destroyed` node — the def's explicit targets, NOT a world scan (C24).
⚠ `HandleSound` (D31) fires the one-shot `SOUND` — the death/damage/impact audio a sequence emits
  (`air_mixed_exp_sg` on a struck building) — as a fire-and-forget `WorldSounds.PlayOneShot` at the
  event's AT_NODE (`{name,pos}` compiled / flat `at_node`+`translate` reader). NAME is a sound
  *definition* or a `SOUND_GROUPS` name, never a node. `OneShotSoundsPlayed` counts successful
  plays for the damage-test (audio can't be screenshot-verified). With this landed the death path
  has no stubbed event kind left (its swap, debris tumble, effects and now sound all run).
⚠ World-effects runtime (D32): a second, world-scoped AnimRuntime (like the crash runtime but not
  per-player) bound to the impact/destruction effect closure over a hidden template stage. `Handles`
  tests a name; `PlayEffectAt(name, worldPoint)` relocates the template root onto the point and
  `Start`s the def — the puffers ride the (relocated) root, parented at world level, so they render;
  `ProjectilePool.EffectSink` calls it on a rocket impact, and the WORLD runtime's `ExternalEffect`
  routes a death's CALL_ANIMATION of a curated effect here (its own factory is gone post-build).
  `EffectTtl` bounds a stop-less sustained emitter (`large_30sec_fire`); `SoundHandledElsewhere`
  makes its SOUND/SOUND_NODE no-ops (D30/D31 own that audio); `StopAll`/`PuffersBuilt` serve the
  `--effects-test` verify. `SweepEffectTtls` runs each Advance. Guns don't route (documented follow-up).
⚠ PUFFER_STATE AT_NODE `INPUT_NODE`/`MAIN_ROOT_NODE` resolve to the anchor (`IsSelfNodeRef`, the same
  rule ConditionNode applies) — so a `PufferState(fire_n_smoke, at=INPUT_NODE)` death fire emits on
  the effect's own relocated root instead of nowhere. Before D32 it fell to `ResolveOne`→null→no host.
⚠ Collider removal on death is FREE (C25), not separate code: `SetSubtreeActive` toggles
  `SetCollidersEnabled` with `Visible`, so the swap that hides `healthy`/shows `destroyed` also
  un-solids the door/building and solids the wreck. Measured off/on per kill (C2 gates: off 1, on 8).
  The healthy collider only exists in the FLIGHT build — `Collision` is `_fly` — so a `--freecam`
  census reads zero; the C25 harness forces it with `|| _damageTest`.
⚠ Debris tumble (C26) is FREE too: the ballistic `ObjectMotion` half (`MotionRuntime` —
  translation_range/gravity/forward_rotation/scale over a run_time) was built by the M2 crash work and
  is REACHED on death because the death's `OBJECT_MOTION` events are `Initial`, so `Start` runs them.
  The launch is SCHEDULED mid-sequence (water tower at t=2.2 s), so it only fires as the death plays
  out — a kill-and-check must `Advance` the clock to see it (`BallisticMotionsLaunched` counts them).
  `do_intersections`/`bounce_sequence` ground-rest stays deferred.
⚠ `CollideDamageAt(struck, healthDamage)` is the plane-COLLISION entry (C27): only a
  `WeaponOrCollideHit` destructible (the 44 facades/windows/`agyrobus`) accepts it — gated on
  `def.Activation`, then applied through `DamageAt` — and returns true so the caller flies the plane
  THROUGH it; a `WeaponHit` object returns false and stays solid (ram it → crash, decision 6). Wired to
  `FlightController.CollideDamageSink`.
⚠ `ResetDestructible(inst)` (C28) is the death's inverse — for the debug tools + respawn: `Stop` the
  def's death, `RestoreRestPoses` (put the flown debris back — `_rest` holds each moved node's authored
  pose; `Stop` alone leaves it displaced), re-apply `RESET_STATE` (healthy visible+collidable/destroyed
  hidden — undoes the swap AND the `ApplyDeathSwap` fallback), then restore HP/Status/DamageStage.
  Idempotent: destroy→reset→destroy is identical.
⚠ **The bind never throws on a mostly-absent world — it degrades, silently, in two ways that look
  the same from outside** (audited for the `--node=` stage). A def whose NAME resolves nothing gets
  `Anchors() == []` and is `continue`d: *no handler ever fires*. A def that IS anchored but names a
  node the build skipped bumps `_opsUnresolved` and dispatches into nothing: *the node is not here*.
  Both leave a still object. `ReportResolution` turns on a bind-time census that separates them by
  definition — `[anim] bind census …` / `bind unanchored=…` / `bind target_missing=… why=…` with
  `index-not-built` (the compiled symbol table's gamez index was never built) vs `name-no-match`.
  Off by default; `WorldSession` sets it for a node stage. Measured on C1 `--node=hk_zep`: 50
  anchored, 763 unanchored, 134 target-missing, every one of them `index-not-built`.
⚠ **`MaxRootLift`'s premise is a WHOLE-WORLD node count, so a partial world inverts it** — hence
  `SuppressRootLift`. The 16-match cap exists so a generic `ANIMATION_ROOT_NAME` (`healthy`, 217× in
  C1) cannot anchor a def onto every building; a single subtree drops *under* the cap, so defs that
  never anchor in the full world anchor here, on whatever generic child the subtree owns. Measured
  on C1's 20-node `ap_radiotwr`: **95 lifted defs and 91 phantom destructible instances**, versus 1
  def and 2 instances with the lift refused. A caller building part of a world must set it, and the
  refusals are counted and printed (`root_lift_suppressed=`), never dropped quietly.

## src/Mech3/DestructibleRegistry.cs
Live, mutable per-instance HP for the world's destructibles — any `AnimDefinition` with
`HEALTH > 0`. One `Instance` per `(def, anchor)` pair, seeded from the authored `HEALTH`, plus a
coarse healthy/damaged/destroyed `State` and a monotonic `DamageStage`; built during AnimRuntime's
bootstrap, read by `ANIM_HEALTH` eval, escalated by `ApplyDamageStages`, damaged via `DamageAt`.
`Resolve(struck)` maps a raycast-hit node back to its instance. Schema: docs/formats/destructibles.md.
⚠ Keyed per `(def, anchor)`, NOT per def — a wildcard NAME binds many node groups, each an
  independent pool (one tower's damage must not touch its siblings).
⚠ Instances can exceed node groups (`Count` vs `DistinctAnchors`): the reader's wildcard def and
  the compiler's per-instance defs both bind the same nodes, so one object carries several pools
  (same HEALTH). `_authoritative` keeps ONE per anchor node, compiled-preferred.
⚠ `PoolsOn(node)` is every pool anchored on exactly that node; pair it with `Resolve` (which names
  the one a hit reaches) rather than filtering `All` again — the node lab and the world damage lab
  both read it, and the ones `Resolve` does not name cannot be damaged at all.
⚠ `Resolve` walks the WHOLE parent chain and takes the nearest COMPILED anchor, not the first hit:
  a reader wildcard can grab an inner node the compiled def doesn't (tower `ap_h2otwr*` matches
  `ap_h2otwr.flt`, between the collider and the compiled `ap_h2otwr1` root), and the compiled def
  owns the real DAMAGE_SEQUENCE + death sequence.

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

## src/Flight/Projectile.cs
`ProjectilePool` — the shared-world weapon-fire subsystem (B13/B14/B15/D29/D30/D33): a fixed pool of
projectiles integrated with the data's ballistics (VELOCITY/ACCELERATION/GRAVITY, expiring at
RANGE), plus tracer streaks, muzzle flashes, the per-surface IMPACT sound + effect model, and the
stand-in spark. `Spawn(weapon, worldMuzzle, inheritVel)` fires one round (with a CANNON_SPREAD cone)
and flashes the muzzle; it runs itself each physics frame. One pool per session, fed by every player's guns.
⚠ Hit detection is a per-step world raycast; the flying plane has no physics body, so a round never
  hits its own launcher and `player`/`enemy` IMPACT classes are unreachable in M3.
⚠ `CANNON_SPREAD` jitter and the stand-in fireball draw from a held `Rng.Weapons` stream, so a
  pinned run repeats its whole impact pattern: two `--det` C1B dives log 8/8 identical impact
  positions where the unseeded build shared none.
⚠ `DamageSink` (wired to `AnimRuntime.DamageAt` in flight, C23) turns a hit into destructible damage:
  every `Impact` invokes it with the struck collider + `HEALTH_DAMAGE`; a no-op for terrain/water.
  Null in views with no anim runtime, where impacts stay cosmetic.
⚠ Surface class comes from the struck collider's `SceneBuilder.SurfaceMeta` (water/buildings),
  stamped at build time from the mesh's dominant material texture; absent ⇒ `default`.
⚠ IMPACT effect (D30): `SpawnImpactModel` instances the per-surface `ANIMATION`/`SURFACE_ANIMATION`
  when its name IS a chapter-gamez root (reusing the flyout GameZ/SceneBuilder) — the water splash
  `splash1.flt`/`bsplsh.flt`; a geometry-less or unresolved name (`3040slug_gunhit`, `bld_damage.flt`,
  `he_ground_effect`, `large_fireball`) instances nothing and the spark stands in. The puffer half of
  those named effects can't render in flight (the puffer factory is torn down after the world build —
  `KeepArchivesOpen` is lab-only), so it is D32's world-effects-runtime work, not the pool's.
⚠ `EffectSink` (D32) plays the puffer half of a named IMPACT effect through the world-effects runtime
  (`AnimRuntime.PlayEffectAt`) when the effect is NOT a gamez model — the rocket fireballs/smoke
  (`large_fireball`, `he_ground_effect`, …). Gated to `!weapon.IsGun`: the `gunhit` smoke has no stop
  event, so a per-round shared emitter would collapse onto one ever-emitting puff (guns follow-up).
  The runtime no-ops on a name it doesn't carry, so the spark still stands in for the inert names.
⚠ When `EffectSink` is null (a scene-less pool: the weapon lab), a hardpoint (`!IsGun`) impact shows
  `SpawnExplosion` — a cluster of large additive orange sprites — in place of the single spark, so the
  blast is visible where the real puffer can't build. Flight keeps its real fireball (EffectSink set).
⚠ Tracers are velocity-aligned, NOT billboarded (billboard would collapse the streak to a
  screen-vertical bar); muzzle/impact bursts ARE round billboards. Per-instance colour via MultiMesh.
⚠ Rockets fly the FLYOUT MODEL body (B14): `Spawn` instances the weapon's `.flt` prototype root
  (`he_rocket` …) from the chapter gamez via the world `SceneBuilder` (collision-exempt, so rounds
  don't obstruct one another), posed nose-along-(-Z) down the velocity via `Basis.LookingAt`. Only
  rockets get a mesh (≤1 alive at 1/s); guns stay on the MultiMesh tracer quad (≈10/s, dozens alive).
  A rocket with a body trails a slim exhaust streak; `RocketStreakScale` is now only the fallback
  when a chapter lacks the prototype. The FLYOUT `MODEL_ANIMATION` smoke trail is still pending (D-wave).
⚠ `BuildFlyoutBody(weapon)` (public) is the shared "FLYOUT MODEL name → fresh un-parented instance"
  path — resolve+cache the prototype, `BuildSubtree` collision-exempt. The private `BuildFlyoutModel`
  wraps it for the in-flight round (parents under the pool); `PylonOrdnance` (D44) calls it for the
  mounted body — the round on the wing and the round that flies off it are the same asset.

## src/Flight/PlaneStats.cs
Typed per-plane stats: vehicle.json `dynamics` (resolved through the `kind_of` def chain) +
engines.json stock engine power + player.json globals, the `engine_sound` def name with its
volume/pitch `SoundCurve`s (clamped two-point ramps), `destroyable_parts` → `DestroyablePart`
records (name, max HP, `critical`/`engine` flags, `got_hit_anim`, per-part `injure_anims`), and
the def-level `VehicleInjureAnims`. Schema: docs/formats/vehicle.md.
⚠ Def-level injure_anims are consumed as ANY-part HP fractions, not per-part — see DamageVisuals.

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
Stunt Flying state: `Load` builds the ordered zone list from ia.json `dzones` (positions via
`GameZ.WorldTransformOf`, strings via MissionTargets + Messages; null when a mission has none →
free flight); `Update` completes zones within `DzRadius` (15 m, TUNE), fires events, advances
the target; clock/scoring via `Elapsed`/`CompletedAt`/`CompletionOrder`/`InCompletionOrder`;
`ForAnotherPlayer()` clones an independent run so the archives parse once per session.
⚠ Drive off the dzones LIST, never the gamez `dzN` nodes — numbering is non-contiguous (missions.md).
⚠ `GeometryAnchor` covers zones naming world GEOMETRY (C2's `sghangar`: transform `"Initial"` →
  `WorldTransformOf` = origin, ~8 km off): anchor = union centre of the subtree's `door`-named
  leaf pair (the flown aperture), else the whole-geometry centre; null for every childless
  `model_index -1` marker. `child_bbox`/`node_bbox`/`active_bbox` stay deliberately unparsed.
⚠ Deliberately NOT reset on respawn (a mid-run crash keeps zones + clock); `Reset()` is the opposite.
⚠ `FormatTime` is InvariantCulture (German locale renders `2:13,6` otherwise); MarkerHud delegates to it.

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
⚠ `LoadTexture` (the `rimage`/`impact_point.png` PNG loader, moved here from `GameSession` in
  PLAN-planeviewer-split A3) is static and loader-only — it does not build a reticle; callers pass
  its result to `Build`.

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
⚠ `FinishTime` is snapshotted separately from `Mission.Elapsed` so the board still reads
  correctly after a rematch has reset the missions.
⚠ Deliberately not a Node — it is freed with the session, so the `RunCompleted` subscriptions
  need no teardown.

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
⚠ `ParseColor` normalizes dual-encoded triples (÷255 iff any component > 1); GameSession treats
  every result as DX7 sRGB and linearises.

## src/Flight/FlightAudio.cs
Own-plane non-positional loops (engine with throttle-driven pitch, overspeed whine, rattle) +
one-shots: `StartEngine`/`EngineStartRamp` prop-start fade (re-fired via the loop-restart hook
in `Update`), `OnCrash` → `snd_exp_plane1..4`, `OnGroundExplosion` layering `snd_exp_ground_a`.
⚠ `WhineMixGain` 0.12 (TUNE): don't raise it back — reader "volume" is not a linear mix gain
  (the original's whine sits 12–18 dB below the raw curve cap); re-derive from a new reference.
⚠ `OnEngineStop` is deliberately NOT called on crash; a future shutdown flow must also stop
  driving `Update`, or the restart hook re-fires propstart.
⚠ `MixGain` (1/√N in splitscreen, TUNE) covers only the three loops, never the one-shots.
⚠ The `snd_exp_plane1..4` pick draws from `Rng.FlightAudio` and prints `crash sound: <name>` —
  the pick's only trace outside the speakers, and the sole way to verify it from a headless run.

## src/Effects/Puffer.cs
The original engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks
(schema: docs/formats/effects.md). `PufferState.Load` finds the fully-defined state in an
effects reader; `Puffer.Create` builds a texture atlas + ONE MultiMesh whose shader billboards
each quad, with quad-rim fade + soft-particle depth fade. Modes: `Burst`, `TrailAdvance` /
`TrailBurnAt` (distance trails), `SustainAt` (continuous at a moving node — pool sized to steady
state, catch-up capped); `PufferState.FromAnimEvent` parses the compiled anim payloads.
⚠ COLORS ramp ⇒ blend_mix, else blend_add (effects.md); `Create`'s `blend`/`softParticles`
  overrides exist because the data can lie — the crash `large_black_smokeball` has
  `colors: null` yet needs MIX, and the depth fade zeroes fresh ground-level smoke. Defaults
  leave every existing caller byte-identical.
⚠ A fading additive fireball READS AS SMOKE — isolate the emitter before believing smoke works.
⚠ Each emitter's `_rng` is a per-instance stream off `Rng.Puffer`, so particle spread is pinned by
  the master seed: measured, the C1 waterfall mist moved 0.47% of a `--det` frame before and 0.00%
  after. Its seed depends on how many puffers were built before it — deterministic under `--det`.
⚠ Compiled-payload quirks (`interval_garbage`, Distance-trail meters, `growth_factors`): anim-definitions.md.
⚠ `MakePuffer` (moved here from `GameSession` in PLAN-planeviewer-split A3, verbatim) is the
  `PufferState.Load` + `Create` + `AddChild` convenience the flight assembly and the static damage
  lab both use — call it as `Effects.Puffer.MakePuffer(...)`.

## src/Effects/CloudPuffs.cs
Synthetic ambient cloud field: ONE alpha-blended MultiMesh of cloud1/cloud2 billboards in a
cylindrical shell around the camera — Y anchored to the CLOUD_COVER band, X/Z following the
plane, passed puffs recycling to the leading edge, WIND-driven drift, alpha fading at the shell
edge and by vertical distance. Feel constants all TUNE (`BaseAlpha` kept low — overlaps saturate).
⚠ Synthetic by design: the world's own ~600 cloud sprites cluster near the airfield and no zrdr
  defines an ambient emitter — don't try to source this from world data.
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
⚠ Cloud-band gate: precip renders only BELOW the CLOUD_COVER band; a huge sentinel band
  disables the gate when a mission has precip but no cloud band.

## src/Flight/SpectatorCamera.cs
The `--freecam`/`--anim-lab` observation camera: WASD move, RMB-held mouse look (captured only
while held), wheel speed, pads via `Pads.For(null)`; lab additions `Frame(Aabb)`, the
`FollowNode` orbit-lock (released by any translation input; `ExitFollow` keeps orientation) and
a public `Camera` accessor — all inert in plain `--freecam`. Rates TUNE.
⚠ Deliberate: NO collision; pitch clamped (`PitchLimit` ~89°, `OrbitPitchLimit` ~80°); roll can
  never enter — `ApplyOrientation` is world-up yaw then local-X pitch; vertical move is world up.
⚠ Default start is the mission spawn — RANDOM per launch; pass `--pos`/`--direction` for comparisons.
⚠ The ctor keeps only the DIRECTION to its look-at (distance discarded), so the host may hand it a
  `--lookat` point or a `--direction` projected one unit ahead — the two are interchangeable here.
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
⚠ Cruise is Slerp's degenerate case — pathDot > 0.999f branches to a normalized lerp (the
  near-parallel cross-product axis is float noise; Rotated throws). Never revert to a bare Slerp.
⚠ While stalled the nose cannot rise over the horizon: world elevation is capped at max(horizon,
  frame-start elevation), so a slow full-pull loop breaks at the top — original behavior.
⚠ The knife-edge nose sag (KnifeNoseSag/KnifeNoseRate) is a bound approached at a rate cap, both
  measured: a free-falling target never settles, an exponential rewrites stall recovery. It also
  acts at zero bank (knife grows with pure pitch) and shifts the settled path ~1:1 by construction.
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
The flying-aircraft node: input → FlightModel → transform, roll-following chase camera, text HUD +
telemetry, crash and respawn; drives every HUD widget and animator, and sweeps the PlaneCollider
boxes via CastMotion each physics frame (the old center ray stays as an anti-tunnelling backstop).
⚠ The chase camera slerps its BASIS, never a re-derived hard LookAt — inverted flight renders upside down.
⚠ The chase camera takes the SIM clock's dt (its exponential smoothing makes its pose a function of
  dt, so wall time made scripted flight captures frame-rate dependent); the halted orbit camera keeps
  wall time on purpose, so a freeze can still be flown around.
⚠ The fixed numpad views (`Views` + ActiveView/ApplyFixedView; held Kp1–Kp9, or pinned by `--view=`)
  REPLACE the chase update for that frame — they never smooth, and both the offset and the whole
  basis are carried by the plane's attitude (`Attitude * LookingAt(-dir, up)`), never a world-up
  LookAt, which is the same reason the chase camera slerps its basis. Their `up` is the plane's up
  except for the belly view, whose view axis IS that up. Nothing held and no `--view=` is the chase
  camera byte for byte (verified md5 against the pre-view binary).
⚠ SurviveHit reads the contact normal at a pose 5 cm past the cast hit — at just-touching the rest
  query finds nothing and the head-on fallback turns shallow grazes into crashes; don't shallow it.
⚠ A dead `critical` part crashes regardless of impact speed; billboard trees are intangible (solid clutter only).
⚠ C27 collide-through: SweepAirframe/HitWorld now also out the struck `Node`; before the crash/graze
  decision, `CollideDamageSink` (→ `AnimRuntime.CollideDamageAt`) is offered the hit. If it returns
  true (a `WeaponOrCollideHit` facade/window/`agyrobus`) the hit is CLEARED — the plane keeps its
  full-motion pose and flies through, the object taking `vn × CollideDamagePerVn` HEALTH_DAMAGE. Every
  other object stays solid. Null sink (viewer/static) = every collision solid, as before.
⚠ The crash is data-driven: CrashRuntime plays player_crash_dirt (InheritedWorldVelocity = impact
  velocity × WreckMomentum); Respawn resets it and re-homes CrashRestPoses; null runtime = hide only.
⚠ Firing (needs Loadout + Projectiles): UpdateGuns holds Space/pad-B → the SELECTED group fires at
  its FIRE_RATE from its own ammo (ONE group at a time, no ALL — user-confirmed); UpdateRockets F/pad-A
  → ONE rocket per pull from the next armed pylon (round-robin), FIRE_RATE-gated (1 s). `--fire`/
  `--fire-rockets` auto-hold; `--infinite-ammo`.
⚠ Two selectors (CycleWeaponSelectors, edge-detected, --no-pads-safe): guns G/dpad-L cycles the
  firable groups; hardpoints H/dpad-R cycles ordnance types (stock = one, so a no-op). `--gun-select=N`
  (0-based) seeds the gun group for headless tests; selections survive respawn.
⚠ `Ordnance?.Update()` runs after UpdateRockets each physics frame — hides a pylon's mounted rocket
  (PylonOrdnance, D44) the instant its ammo hits zero; RefillWeapons/respawn re-arms and re-shows.
⚠ UpdateReticle (E37, in `_Process`): `BallisticImpactPoint` marches a round of the SELECTED group's
  weapon from the averaged muzzle pose through the pool's own VELOCITY/ACCEL/GRAVITY to
  `GunConvergenceDist` (TUNE 250 m) and feeds the world point to `ImpactReticle` — hidden when crashed
  or no firable gun. Shares `ProjectilePool.WorldGravity` (now `internal`) so reticle and rounds agree.
⚠ Rocket pad button A also respawns, but only from the crashed / run-complete screens (early-return
  states this live-flight path never reaches), so the two never collide. RefillWeapons re-arms all on respawn.
⚠ PadDevices null = every connected pad, never pads[0] (phantom devices read idle); UseKeyboard
  gates keys to P1; AllowPause is false in splitscreen — the freeze halts the shared world.
⚠ The sim half is `SimStep(dt)`, called either by `_PhysicsProcess` (realtime clock) or by
  `GameSession` (fixed/halted clock — `GameClock.PhysicsDt` returns 0). There is no local `_paused`
  any more: P / gamepad Start is polled in `_Process` (which keeps running) and toggles
  `GameClock.Halted`; the halt then simply stops the SimStep calls, and `_Process` seeds the orbit
  camera on the transition and pauses the engine loops. The cameras and HUD stay on wall time.
⚠ The stunt/race AllComplete freeze runs BEFORE the crash branch; Respawn never resets a mid-run stunt.

## src/Flight/PlaneDamage.cs
Per-part hit points from vehicle.json destroyable_parts (via PlaneStats). MapStruckPart maps a
struck collider box + plane-local impact to the data part: wing/canard by impact X sign (left =
−X), fuselage fore/aft of z 0 → nose/tail. Apply subtracts, Reset refills on respawn, Summary
feeds the HUD DMG line.
⚠ The "tail" arm ignores localImpact and is correct only because PlaneCollider.Relabel hands it
  no outboard boxes — do not fix tail sidedness here; widening the signature was rejected.
⚠ The `engine` flag (power loss) is unwired **by design, not deferred** — the original states damage
  never degrades performance; but the shipped data still sets the flag, so retail may have walked
  that back (docs/formats/vehicle.md).

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's HP fraction crosses an injure_anims
entry it shows the torn pdpN panel, hides the healthy skin, and assigns a discrete-puff fire trail
from the emitter pool; def-level player_smoketrail starts the nose smoke/fire pair. UpdateStatic
burns the trails in place at StaticBurnSpeed for the parked damage lab.
⚠ PairHealthySkins pairs torn↔healthy by merged mesh-AABB position, never by name (the _h
  numbering is crossed on three models — docs/formats/gamez.md) and never by node origin (the
  placement is baked into mesh space); unpaired _h skins are never hidden.
⚠ player_fuelleak and the *_damage_green/yellow/red cockpit cycle stay unwired (no cockpit).

## src/Flight/DamageLab.cs
The --viewer damage lab (H toggles): one HP slider per destroyable part with threshold readouts,
plus a mirrored GaugeCluster damage dial; presets (--damage=part:frac) land through the same
ValueChanged path as a hand drag. It drives the SAME DamageVisuals instance flight uses — it never
reimplements visuals, only decides when to rebuild them.
⚠ Reapply's crossed-anim set-diff (TargetAnims) is load-bearing twice: it implements repair
  (re-derives from pristine) and keeps a slider drag from restarting the fires at every pixel.
⚠ Built in EVERY --viewer session (StartHidden without --damage) so H has a receiver; two H
  presses must return a byte-identical frame (SetLabVisible toggles panel + gauge layer together).

## src/Flight/CompassTape.cs
The original's top-centre heading tape rebuilt from the game's own compassticks2/compasstxt
textures: a cylindrical drum seen edge-on — DrumX = center − R·sin(Δ), headings increase LEFT,
cos(Δ) fade (rendering model: docs/formats/hud.md). Metrics are probe-fitted Ref* constants ×
HudMetrics.Scale; Build returns null if a texture is missing; _Process re-anchors on resize.
⚠ The filtering split is deliberate: ticks point-sampled without mips on the tape Control (mips
  crush the tile vertically), labels bilinear on a child LabelLayer — do not unify them.
⚠ TileOverscan/RimGain and the nearest-tick look are TUNE pending user A/B; north = −Z is
  FlightController's one-line assumption (open question in hud.md).

## src/Flight/GaugeCluster.cs
The original's cockpit dials as a screen-space HUD: altimeter, speedometer, damage display, plus
the gun + missile weapon gauges (E35), all geometry extracted from the plane's own gauges subtree
(structure/scales/quirks: docs/formats/hud.md); polys draw by data priority, rest rotations
ignored; PartFraction binds flight or the lab; dial centres are bottom-anchored (FromBottom) so
panes keep them on screen.
⚠ Never color-key needle.tif — a black key erases the hub's two black discs; the engine-side slim
  taper is replicated as a load-time alpha mask (Needle*Frac constants, TUNE).
⚠ The face textures hold dark UNLIT copies of the STALL / LOW ALT windows — compare pixel values
  (~58,0,0 unlit vs 180+,0,0 lit) before concluding a warning state is wrong; bitten twice.
⚠ The two weapon gauges (gungauge above the speedometer, missilegauge above the altimeter) render
  only when FlightController pushes a `WeaponGauge` each frame — null in the labs (no loadout). The
  4-digit readout is per-GROUP for guns, per-PYLON for rockets (the arrow's pylon), NOT a total;
  the type row shows the weapon NAME upper-cased; belt lights step green/yellow/red by that slot's
  fraction (thresholds TUNE). Digit/letter/indicator glyphs are chapter textures, not rimage.
⚠ The gungauge/missilegauge face is on a generic child (`g815`/`g819`) on ALL planes (no Bloodhawk
  special case, unlike the damage dial) — so "any unrecognised child = face" is the extraction rule.

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
⚠ `Prime()` seeds edge flags from raw state — a button held through a screen transition is no press.
⚠ `LastActivePad` excludes Start: the join gesture is not proof somebody owns the pad.

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
⚠ Starts hidden — an unadorned `--viewer` stays byte-identical; `--debug-livery[=N]` scripts the L key.
⚠ `_suppressCallbacks` guards `SyncWidgets` against slider `ValueChanged` re-entry.

## src/UI/NodeLabels.cs
Floating node-name labels (key T) in both the static viewer and flight, cycling Off → Meshes → All;
`--debug-names[=meshes|all]` presets the mode at launch.
⚠ Names come from the `cs_name` meta SceneBuilder stamps, never `Node.Name` — Godot sanitises and
  renames, so its name is often not greppable in the extraction; nodes without the meta are skipped.
⚠ Labels anchor at the mesh-AABB centre in node-local space, not the node origin — origins sit far
  from the geometry and are shared, which collapsed all labels into a single screen cell.
⚠ The nearest-first grid de-clutter (3×3 neighbourhood) is the readability limiter, not `Radius` (1500 m).
⚠ Own plane deprioritised, not excluded; rescans on a 0.35 s timer; builds nothing until enabled.

## src/UI/MarkerOverlay.cs
The `--viewer` marker overlay (key K): draws every firepoint / pylon / target on the parked
aircraft as a coloured gizmo + billboarded label (firepoints orange, shared-mount firepoints
magenta, pylons cyan, target green); `--markers` opens it at launch. Reuses `MarkerRig.Classify`
+ `GroupCoLocated`, so its gizmos agree with `--dump-markers` by construction.
⚠ Markers come from the built plane tree via the `cs_name` meta, not GameZ — the same source
  `NodeLabels` reads; a co-located pair's labels are stacked up the airframe so both survive.
⚠ Gizmo dots always show (no mount position is ever lost); only the LABELS de-clutter, nearest-
  first with firepoints prioritised over pylons — the full named table stays in `--dump-markers`.
⚠ Builds nothing until first shown, so an unadorned `--viewer` screenshot is byte-identical.

## src/UI/MeshLab.cs
The geometry/shading lab (key M): normal lines, smoothing-seam wireframe, collider boxes, light
sliders + headlight, and independent cull × normal-source override cyclers (`--debug-mesh=` scripts
them; `cycle=N` steps the cycler, `force` builds the overrides at the data's own settings, `restore`
attaches then detaches). Two shapes: the `--viewer` lab owns the parked plane for the session; the
**scoped** lab (`--freecam`/`--anim-lab`, ctor taking a `SelectionService`) attaches to the current
selection on M and restores it on M again, on a selection change and on a deselection.
⚠ Built after the subject joins the tree — `GlobalTransform` on a detached node is identity + error spam.
⚠ **Override materials are the surface's OWN shader with two edits** — the cull token in
  `render_mode`, and a `csky_lab_normal_mode` rewrite injected at the top of `fragment()` (the top,
  because the fullbright path derives its light normal inside the body) — plus every uniform copied
  by name. Measured: `force` at AsData/AsData is raw-pixel identical to the shipped render on both a
  world subtree and the parked plane. The hand-written replica shader is the FALLBACK only; on a
  fullbright world surface it moved 1,682 px of a 2,500 px subject (no fog/scroll/alpha terms).
⚠ Bounds-check override slots against the INSTANCE (`GetSurfaceOverrideMaterialCount()`), not the
  mesh — `SmoothMesh` refuses 0-surface meshes; `SetOverride` recovers by re-assigning the mesh.
⚠ `BoundingRadius` is the geometry's own box half-diagonal, NEVER max |v|: a world subtree's
  vertices are absolute under an identity node transform, so the C1 water tower read 7,420 m
  (its distance from the map corner) and drew 163 m normal lines across the chapter.
⚠ Scoped mode: single-letter cyclers OFF (the free camera flies on W/G/C/V — buttons only), overlays
  parented to the lab and ridden onto the target rather than added into the measured subtree, and
  the light sliders drive the lab's OWN `DirectionalLight3D` — never the world's sun or ambient.
  A fullbright target says "no light reaches it" instead of offering a control that does nothing.

## src/UI/WeaponLab.cs
The `--viewer` weapon lab (key W): mounts a weapon and fires it, driving its OWN `ProjectilePool` so
a round runs the identical ballistics flight fires. Two banks match the game — GUNS fire from the
plane's named gun groups (bound from the stock `Loadout` — "Inner Wing Guns" …), HARDPOINTS fire from
its pylons; the bank filters both the weapon list and the mount list, so a gun can only fire from a
gun group and a rocket only from a pylon. Steppers pick bank / weapon (with live ballistics) / mount
/ target surface; a slider parks a stand-in target wall 15–1100 m ahead; auto-fire + fire-once +
Space; copy-CLI-args (`--weapon-lab=<id> [--weapon-mount=g<slot>|pylon<n>] [--weapon-fire]`).
`RunSelfTest` fires all 48 once, each from a mount of its class — the `--weapon-test` pass check.
Built in every parked `--viewer` session so W always toggles, hidden until engaged so a plain viewer
screenshot is byte-identical.
⚠ No chapter world: the pool is built scene-less, so rockets fly streak-only, gun impacts show the
  spark and hardpoint impacts show the pool's explosion stand-in (no real puffer runtime), and
  `DamageSink` is null. The target wall is the raycast's only hit; tagged (`SceneBuilder.SurfaceMeta`)
  so the pool's classifier picks the IMPACT class. Mounts bind from the stock `Loadout`; a plane the
  table omits (or a bind failure) falls back to the raw firepoint/pylon marker rig.
⚠ Pool + target + mounts build in the CONSTRUCTOR (not `_Ready`), so `RunSelfTest` works synchronously
  right after `AddChild` before `_Ready` (Spawn needs no frame). UI + target placement wait for `_Ready` (in tree).
⚠ `_engaged` (target shown, firing processed) follows `_panel` on W, but `--weapon-fire` starts engaged
  with the panel HIDDEN (clean firing screenshots) — firing gates on `_engaged`, never panel visibility.
⚠ Ballistics decimals are formatted `InvariantCulture` (a dot) — a raw `{v:0.#}` interpolation prints
  a locale comma (`h4,5`) in a de-DE run.

## src/UI/SelectionService.cs
The shared world selection in `--freecam`/`--anim-lab`: left-click picks the mesh under the cursor,
PgUp/PgDn (Home/End) walk its `cs_name` ancestor ladder, a breadcrumb HUD line names every rung and
an `ImmediateMesh` wireframe outlines the current rung's subtree. `Current`/`Ladder`/`Level`/
`CurrentBox` + the `Changed(service, freshPick)` event are the state the other inspect tools read;
`Select(node)` is the programmatic entry (a tree panel, a search hit). `--debug-select=x,y[,up]`
replays a click and a ladder walk for scripted runs.
⚠ **The pick is NOT a physics raycast** — neither mode builds collision (WORLD-9), so it is a manual
  ray-vs-AABB scan over the visible `MeshInstance3D`s under the world root, nearest hit wins, one
  walk per click. It is AABB-accurate, not triangle-accurate. **This is the mechanism every later
  inspect tool inherits**; it was lifted out of `AnimLab.PickObject`, which no longer picks.
⚠ `MaxPickDiag` (350 m) skips map-scale meshes, so **terrain is unpickable by design** — a click
  that finds nothing logs `select miss … tested= skipped_oversize=` rather than going quiet.
⚠ Rungs are the `cs_name` meta, never `Node.Name` (WORLD-8) — C1's second `box_car.flt` is
  `godot=@Node3D@5`. SceneBuilder's unnamed `mesh`/`lights`/`col` children are skipped, and the walk
  stops below the world content root, so the outermost rung is the placed object (`hk_zep`).
⚠ The box is measured from the selected subtree's OWN meshes here, not via `OrbitCamera.MergedAabb`
  over the live tree (WORLD-14 — an overlay parked elsewhere would enter the merge); empty meshes are
  skipped and the highlight is parented to the service, never into the subtree it measures.
⚠ The highlight's corners are baked into the rung's local frame once, then it rides that node's
  `GlobalTransform` — exact for rigid motion (train, zeppelin), so it does NOT grow to follow
  articulation inside the subtree.
⚠ Builds nothing until the first selection (no CanvasLayer, no mesh): four `--det` poses are
  raw-pixel md5-identical to a build without this file.
⚠ `OverlayMeta` is the "this is a tool's drawing, not content" marker: a subtree carrying it is
  skipped by BOTH the pick and the box measurement, which is what lets the collider wireframes be
  parented onto the very objects they annotate without becoming clickable or growing their boxes.

## src/UI/ColliderOverlay.cs
The collision wireframe overlay (key C, `--collision=show`/`--debug-colliders` script it) in
`--freecam`/`--anim-lab`/`--fly`: one `ImmediateMesh` per collider host, colour-coded by owner class
(world / water / buildings / clutter / plane / other), built once on the first toggle and
`Visible`-flipped after. Measured C2: 1,848 node-backed shapes + 10k–14k clutter placements.
⚠ **Its first job is the notice.** Pressing C in a mode that built no collision prints the reason on
  screen and in the log and draws NOTHING — an empty overlay would read as "nothing here is solid",
  which is exactly WORLD-9's trap.
⚠ Clutter shapes hang off the region body's RID with no node, so they are read back through
  `PhysicsServer3D.BodyGetShape*` only — a `ShapeOwner*` call on one of those bodies would make
  Godot rebuild it from the nodes it does not have and silently empty it.
⚠ One ImmediateMesh SURFACE per body, not per shape: the cap is 256 surfaces and a city region
  carries thousands of placements (over it, every call errors and nothing draws). A surface closed
  with no vertices is an error too — `HasGeometry` is checked before opening one.
⚠ Each wireframe's visibility follows its shape's live `Disabled` flag (re-read 4×/s), so a
  destructible's death swaps the drawing with it; the tallies are logged as **separate on and off
  counts plus the names that flipped**, never a net (WORLD-10: the C2 gate nets +7 — `col[off 1, on 8]`).
⚠ Budgets, both reported: a trimesh over `MaxShapeTris` (2,000) or past the 400k-line budget draws
  as its bounding box instead. Counts are pose-dependent — the map-edge extender adds clutter bodies.
⚠ Cost with it up (C4, `--perf --no-vsync`): draws 2,181 → 2,532, prims 217k → 257k, `render_cpu`
  1.05 → 1.42 ms, memory 225 → 266 MB. Read those, never `fps`/`frame_ms` (PERF-11).

## src/UI/NodeLab.cs
The node lab (N) in `--freecam`/`--anim-lab`: the world's `cs_name` tree, a search box, per-node
Frame / Hide-Show, a dependency readout for `SelectionService.Current` (anim defs, destructible
pool + DAMAGE_SEQUENCE, geometry/textures, colliders) and a destructibles view with F41's coverage
columns. `--debug-nodelab[=deps,dest,open,node=<cs_name>]` is the scripted twin.
⚠ **Lazy per branch, never per frame.** Each expandable row carries ONE placeholder child that is
  *reused* as its first real row on expand — no `TreeItem` is ever freed. A branch is capped at
  `MaxBranchItems` (C5's world root has 557 named children) with the overflow stated in a row. The
  only per-frame work while open is one status Label at 4 Hz.
⚠ **Tree children are the nearest `cs_name` descendants** — the exact inverse of the selection
  ladder's ancestor walk, so a world click and a tree row name the same relation.
⚠ `Select(node)` reaches what a click cannot: `SelectionService`'s 350 m cap makes terrain
  unpickable, and `node=<cs_name>` selects it anyway (C5 `z3terrain`, a 512×0×512 box).
⚠ **A mode-dependent source SAYS it is absent, never shows an empty list** (LOG-1/WORLD-9): the
  collider line prints the not-built-in-this-mode notice, and on a `--node=` slice the anim and
  destructible readouts carry a PARTIAL WORLD banner with the bind census. `collisionBuilt` is
  wired to the real `WorldSession` option, which `--debug-damage` forces on (as `--damage-test`
  does); without it no mode this lab runs in builds collision.
⚠ Destructible rows come from the PROGRAM's `HEALTH>0` defs joined to the registry, so a def that
  bound nothing shows as `UNRESOLVED`; the totals equal the `destructible-census` suite's.
⚠ Builds no UI until N (or `--debug-nodelab`): the 11 goldens hold unchanged with it in the tree.

## src/UI/WorldDamageLab.cs
The world damage lab (H) in `--freecam`/`--anim-lab`: the destructible pools of whatever
`SelectionService` holds, each with live HP, and a slider + Kill + Reset on the one a weapon hit
reaches, driving `AnimRuntime.DamageAt`/`ResetDestructible`. `--debug-damage[=node=,pool=,hp=,kill,
reset,tick=,open]` is the scripted twin (an ordered script, not a token set).
⚠ **Only the pool `DestructibleRegistry.Resolve` names is drivable.** A node can carry several
  `(def, anchor)` pools (C1's water tower: compiled + reader wildcard); the others are listed
  read-only with the reason. Driving a twin damages a pool nothing can ever hit.
⚠ **The slider is absolute HP** — down spends through `DamageAt`, up runs `ResetDestructible` then
  re-damages, because the model has no healing (`DamageStage` only climbs).
⚠ Swap + collider census are read PRE-tick (synchronous), debris POST-tick (scheduled, WORLD-11);
  colliders print `off=`/`on=` separately (WORLD-10) or the not-built notice (WORLD-9).
⚠ **Freecam builds no world-effects runtime** — the first damage action asks `GameSession` for the
  one `--destroy` uses. Its bound name closure does NOT include the `sputter_*_obj` stage puffers or
  a def's own `PUFFER_STATE`, so those fire in the log and draw nothing here (WORLD-12).
⚠ Builds no UI until H (or `--debug-damage`): the 11 goldens hold unchanged with it in the tree.

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
⚠ The lab no longer picks: `SelectionService` owns the click (and the `GuiReleaseFocus` that frees
  the picker's filter field). The lab only reacts to `Changed` — **frame + follow on a fresh pick,
  re-follow WITHOUT re-framing on a ladder walk**, since re-framing every rung would fling the
  camera out to the whole zeppelin's radius mid-walk. Bound in `_Ready` before the `ShowUi` return,
  so a scripted `--debug-select` run still tracks what it picked.
⚠ The lab does not own its clock: it drives the session `GameClock` (P → `Halted`, `.` →
  `StepOnce`, the speed buttons → `Scale`) and takes `Steps`/`Dt` from it. The mode is
  `GameSession`'s choice — FixedAccum interactively, FixedStep in a scripted `--screenshot` run, so
  captures land on exact step counts.
⚠ The clock hand-off is `AnimRuntime.ManualAdvance`, NOT `SetProcess(false)` (READY auto-enable trap).
⚠ Ordering: `Play` sets the timeline focus BEFORE `AnimRuntime.Play` (t=0 events dispatch
  synchronously); `Step` bumps `_steps` + `_playhead` before `Advance` so the playhead equals the
  runner's clock. The playhead is the lab's own accumulation, NOT `GameClock.Frame`/`Time`: the
  clock advances those by the whole frame's `Steps` at once, which would stamp every sub-step of a
  multi-step frame at the frame's end time.
⚠ `ShowUi` gates all UI — hidden in `--screenshot` runs; `--debug-anim-ui` forces it back on.
⚠ The stage parks `StageAnchorDist` (55 m) ahead on each fresh Play — Restart reuses the snapshot —
  and passes as `fallbackAnchor`; framed/followed only when the def actually resolved onto it.
⚠ Determinism boundary: puffer spread is unseeded RNG — same-step shots differ in particle noise.

## src/UI/AnimTimeline.cs
The anim lab's authored-vs-fired timeline (custom-drawn `Control`): authored blocks above, fired
ticks below, one lane per Initial sequence; a slanted first-firing connector = scheduler divergence.
⚠ `BuildLane` re-derives the documented scheduling rule independently — deliberately NOT via the
  runtime's `SequenceRunner`. That independence is the whole instrument; don't "simplify" it away.
⚠ `StaticDuration` mirrors `Dispatch`'s duration out-param so authored block widths stay honest.
⚠ Scope: one instance of the played def (first anchor) plus its CALL_ANIMATION children; same-name
  siblings and post-ambient children are untracked; ticks cap at 600/lane, Restart clears.
⚠ `MouseFilter = Ignore`, so orbit-dragging over the strip still reaches the camera.

## src/SessionPaths.cs
Static resolver for the extracted-data paths (`ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr`) under a data root, plus `PreferUnzipped` (an unpacked sibling dir beats its `.zip`).
⚠ Pure path arithmetic — the only I/O is `PreferUnzipped`'s directory-exists probe.
⚠ The `--gamez=`/`--textures=` override policy deliberately stays in GameSession; this class only
  builds the default extraction-tree paths.

## src/SessionSpec.cs
Everything the command line settles about a session, as one immutable record: `Parse(args)` parses
**and resolves**, and the pure arg parsers (`ParseVec3`, `ParsePlanes`, `ParseHold`, `ParseView`,
`ParsePaintColors`, `ParsePaintDecals`, `ParseDamagePreset`) are public so they are testable.
`SessionMode` is closed — Menu/Fly/Viewer/Freecam/AnimLab — with Stunt/Players/EmptyStage/NodeName/
DamageLab as modifiers and `SessionProbe` naming the three probes that coerce a mode.
⚠ **The raw stage never escapes.** Mode flags arrive as private fields because a vote is not an
  outcome: `--damage`/`--markers`/`--weapon-*` all vote viewer, `--damage-test`/`--effects-test`
  vote freecam, `--play-anim=`/`--debug-anim-ui` vote anim lab. `Fly`/`Viewer`/`Freecam`/`AnimLab`
  are computed from `Mode`, so each has exactly one definition; properties resolution overwrites are
  marked **Resolved** on themselves.
⚠ **Step order in `Resolve` IS the behaviour.** `--stunt` moves `Scenario` before arbitration can
  clear `Stunt` (`--anim-lab --stunt` still uses the stunt spawn list); the freecam/anim-lab-only
  debug tools are dropped after `--node=` has forced the viewer. Both look like tidying chances.
⚠ **Pure — no engine state, no globals, no logging, no clock.** `NoPads`/`TexOverrides`/`LogSpecs`
  are recorded, never applied; `PadsDisabled` is a value, not a write to `Pads.Disabled`;
  `PinnedSeed` is null when unpinned rather than drawing `Rng.TimeSeed()`, because a spec that read
  the clock would not be a function of its args (DET-9). Complaints go to `Warnings` as
  `(category, message)`, empty category meaning a bare console line. That is what keeps the surface
  reachable from `CSVM.Tests`, which has no Godot runtime to print into.
⚠ **Two known defects are reproduced on purpose**: `ModeName` omits `--dump-flight` from its "dump"
  arm, and `ShowsMenu` is true under `--run-tests` (a third, milder: `--dump-flight` turns the
  bundle on through `ScriptedBy` yet is not a term of `IsScripted`). All three are pinned by facts
  labelled as defects in `CSVM.Tests/SessionSpecTests.cs` — **the only thing checking this file
  since the resolution baseline was retired**, so a rule added here needs its fact there. Fixing one
  is a behaviour change and needs its own item.
⚠ The two lab spec grammars stay in their labs (`UI.NodeLab`/`UI.WorldDamageLab.ParseDebugSpec`);
  they gained an optional `rejected` list so a caller can take the tokens as data instead of the
  `Log.Warn`, which is how the spec normalises those two values engine-free.
⚠ Path flags are override VALUES only, null when unset — no default arithmetic here, that is
  `SessionPaths`. `DataRoot` is the raw arg; its precedence against `CSVM_DATA_ROOT` is the
  caller's, because every base path derives from the winner.
⚠ **`FromMenu` is static and takes its base as a PARAMETER** — the launchscreen derives from the
  pristine command-line spec, so nothing a previous session settled reaches the next one. Passing the
  live spec instead is a compile error (CS0026), which is the whole point: the three carry-over
  patches this replaced were correct only as long as somebody remembered them. It writes Chapter /
  PlaneNames / PlaneName / Players / Stunt / Mode=Fly / WorldMode / Scenario and **must keep writing
  every field a pick can change** — a menu-settable field added without its write re-opens the
  carry-over bug, and the pristine base then hides it behind a command-line value.
⚠ **`FromMenu` does not re-resolve, deliberately.** Re-running arbitration would let a `--viewer`
  vote win a second time and the menu would stop launching flight; re-running placement would move a
  `--pos` routed to the camera at parse time onto the menu's plane. It overwrites an answer rather
  than asking the question again — which is what the launchscreen has always done.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram` — the world+anim half of a session build;
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights.
⚠ Stops before the per-view steps (unplaced-entity watch, edge extender, horizon, weather — those
  stay in GameSession) and does NOT add `Root` to the tree; effects go under `Options.EffectsParent`.
⚠ Disposal contract: `textures`/`sounds` stay the caller's `using` locals — nulls `PufferFactory` +
  the sound loader after bootstrap (prewarming first) unless `Options.KeepArchivesOpen` (the lab).
⚠ Returning `Program` keeps crash-effect-param loading in the caller — no Mech3→Flight dep here.
⚠ `PlayerPosition` is a single per-call delegate — no camera exists at build time.
⚠ **The phase boundaries are a reported contract.** Each step records its own span into
  `StartupProfile` — `zrdr` (mission setup) · `world` (WorldBuilder) · `clutter` · `anim`
  (AnimProgram load) · `bind` (bind + bootstrap) · `prewarm` — and those are the bulk of the
  `[perf] startup` accounting. Move a step, move its `Record` with it: a dropped phase does not
  read as missing, it reads as a growing `rest`. Keep them leaves — never nest one inside another.
⚠ `Options.NodeSubtree` (the `--node=` stage) is the same pipeline with three steps switched off:
  `WorldBuilder.BuildNode` replaces the world build, mission setup and clutter are skipped, and the
  runtime's `ReportResolution` + `SuppressRootLift` go on. The anim program still loads and still
  binds — what a partial world does to the bind is the question the stage exists to answer. Mission
  setup is skipped rather than run because the one verb that reliably WOULD resolve is the one that
  switches the requested subject off (C1/IA1 hides `hk_zep`); consequence: a node stage shows the
  subtree in its gamez base state, not this mission's, including its `texture_scroll` defaults.

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
The per-launch session node (PLAN-planeviewer-split B7/B8): `Session.Launcher` (Main.tscn's root)
instantiates one per launch with `(SessionSpec, LauncherContext)`, adds it to the tree, then runs
`StartSession()` — menu and CLI share that one build path; return-to-menu is a bare `QueueFree`
(`Launcher.ReturnToMenu`). Bootstrap, launchscreen, persistent camera/lighting and the process-wide
per-frame machinery (shader clock, `--perf`, capture tick, Esc/F11/F12) live on
`src/Session/Launcher.cs` — read that entry too before touching anything around the build's edges.
⚠ **`StartSession`'s build body is an ordered sequence of phase methods** (PLAN-planeviewer-split
  C9): `LoadArchives` → `ResolveNodeSubtree` → `BuildEmptyStage`/`BuildWorldStage`
  (→ `BuildAnimLabStage`)/`BuildStaticStage` → `AttachPlaneAndLabs` → `AssignCloudDeckIfBuilt` →
  `BuildFreecamSpectator` → `BuildFlightRigs` → `ApplyDestroyOverride` → `LogBuildSummary`, then
  (outside the try) `FinishFraming`. They share one `BuildState` (a private nested class) instead
  of the flat local-variable graph the phases used to close over — add a new cross-phase value
  there, not as a new local. The `try`/`catch` around the whole build, and its `StartupProfile`
  mark/record pairs per phase, are unchanged.
⚠ **`BuildFlightRigs` loads only the session-wide flight data** (PLAN-planeviewer-split C10): the
  planes gamez, the per-plane stats cache, pads, the paint RNG, the spawn list/base, the weapons
  catalogue + loadouts, the shared projectile pool, the HUD font/reticle textures and the stunt
  mission/race. It then hands all of that to one `Session.FlightRigAssembler` as its `Inputs` and
  calls `Assemble(pi, rig)` per rig, ascending — **anything per-player belongs in the assembler,
  anything shared here**; the assembler's accumulated `MeshInstances`/`WhatSuffix` fold into
  `BuildState` after the loop (before the race board's `What`, as when the loop was inline). The
  shared race board, the rematch wiring and the splitscreen summary line stay here, since they are
  built once the last rig is in.
⚠ **The `--*-test`/`--destroy=` probe wrappers moved to `Testing.ProbeRunner`**
  (PLAN-planeviewer-split A1); the runner is process-scoped (the Launcher constructs it and
  dispatches the `--dump-*`/`--run-tests` early quits itself) and every call site here is a
  one-line delegation passing `_spec` — see `src/Testing/ProbeRunner.cs`'s entry.
⚠ **The `--screenshot=`/`--shots=`/`--frames=` capture pipeline is `Testing.CaptureDirector`**
  (PLAN-planeviewer-split A2), owned and `Tick()`ed by the Launcher (B7). Every
  `--screenshot`-conditioned display choice here (HUD/panel visibility, `--anim-lab`'s fixed-step
  clock choice, exit-on-build-failure) reads `_captureDirector.Pending` instead of a raw field —
  see `src/Testing/CaptureDirector.cs`'s entry for the state machine and its sim-frame trap.
⚠ **Livery/spawn resolution and a clutch of pure helpers moved out** (PLAN-planeviewer-split A3):
  paint catalog/pattern-mask/scheme-pick logic → `Session.LiveryResolver` (`_liveryResolver`,
  constructed at the top of `StartSession` from the live `_spec` — never cached across a menu
  rebuild); spawn-index/spawn-point resolution → `Session.SpawnPicker` (`_spawnPicker`, same
  lifetime); plane-roster lookups → the static `Session.PlaneRoster`; pad assignment logging →
  `Pads.AssignPads`/`Pads.LogPads`; the reticle PNG loader → `ImpactReticle.LoadTexture`; the puffer
  factory → `Effects.Puffer.MakePuffer`; the Config warmup → `Config.WarmTuningRegistry`. See
  `src/Session/LiveryResolver.cs` and `src/Session/SpawnPicker.cs` for the paint-RNG-order and
  per-player-index traps that moved with the code.
⚠ **It parses no args and resolves nothing.** The Launcher parses the command line and hands this
  node the one spec its session is built from (`SessionSpec.FromMenu(_cli, …)` for a menu launch);
  every consumer reads `_spec`. **A new flag is a SessionSpec change**; adding a field here to
  hold one puts the answer in two places again, which is the smell the extraction removed.
  `_menuPads` is the deliberate exception and is not an arg — it is join-flow session state and
  rides the `LauncherContext`, never the spec.
⚠ **There is no `Teardown()` — return-to-menu is `QueueFree` (PLAN-planeviewer-split B8).** The whole
  session subtree (world, plane, HUD, rigs, effects) hangs under `_worldRoot`, a child of this node,
  so it frees atomically with the node — no field-by-field null-out. Only the duties `QueueFree`
  cannot reach run in `_Notification` on `NotificationExitTree`: null the published `GameClock.Current`
  (a static, not a child), `Dispose()` `_worldLights` (clears `csky_light_count`) and `_sessionTextures`
  (the archive kept open for lazy crash puffers), and restore the persistent camera's `Current` (splitscreen
  stood it down). All three disposals are null-guarded, so a failed build that already disposed them does
  not double-free; the menu relaunch happens a frame after this node has exited, so nulling `GameClock.Current`
  never races the next session setting it.
⚠ **`--pos`/`--direction` are routed by mode in ONE place** — `ResolvePlacement`, after the `--det`
  block (it needs `_fly`, settled far earlier). Flight gets `_spawnAt`/`_spawnDir`, everything else
  `_camPos`/`_camDir`; nothing downstream re-decides. **Do not "simplify" `_camDir` into `_lookAt`:**
  `--lookat` is a POINT (the orbit pivot, and `--freecam`'s aim when no `--pos` was given) while
  `--direction` is a vector, and only flight converts one to the other. `--campos`/`--spawn-at`/
  `--spawn-dir` remain as deprecated aliases with their old per-mode reach — `--campos` never places
  the plane, `--spawn-at` still moves the anim lab's parked prop — and log their replacement once.
⚠ **`FrameCamera`'s subject box must be measured BEFORE the labs join the subtree.** `MeshLab` parks
  three EMPTY overlay meshes at the session origin, and `OrbitCamera.MergedAabb` folds them in —
  harmless for a parked plane or a whole world (both already contain the origin), ruinous for a
  `--node=` subtree 7 km out, whose box stretched back to the origin and framed it at 12 km. Hence
  the optional `subject` argument, filled from `WorldBuilder.DetachedWorldAabb` at build time.
⚠ **`GameSession.BuildsCollision` is the only spelling of "does this session build colliders".** Four
  sources — `_fly`, `_damageTest`, `--collision`, `--debug-damage` — and `--collision` is the
  interactive one: the world's colliders are a flight-build product, so the C overlay and any hand
  check in `--freecam`/`--anim-lab`/`--viewer` need it or they measure an absence. The node lab, the
  world damage lab and the C overlay each read it too, and each must get the same answer
  `WorldSession.Options.Collision` did — three hand-written copies had already dropped a different
  term apiece, so `--collision --freecam` told the labs nothing was built and `--debug-damage` made
  the C overlay report "this mode built NO collision" over colliders that existed. Measured C2 startup cost, warm, 3 runs each: total 2,462 → 3,106 ms, of which `world`
  352 → 865 and `clutter` only 47 → 56.
⚠ `--stage=empty` and `--node=` are settled in the SAME mode-resolution block as the rest: the node
  stage forces `--viewer` (unless `--anim-lab`) and sets `_chapterGiven`; the empty stage forces
  `_worldMode` false, which is what routes `gamezPath` to planes.zbd and takes the third branch in
  the build. `--node=` also turns off the anim lab's own auto-frame — on a one-object stage the
  subject IS the stage, and re-aiming on every Play swings the camera off the only thing there.
⚠ `FrameCamera` synthesizes the `--viewer` orbit pivot when only a `--direction` was given: the point
  on the aim ray nearest the plane's AABB centre (min radius 1 m), or the AABB centre with the eye
  swung to the aim when there is no `--pos`. It logs the value, because a synthesized pivot the user
  never typed is exactly the thing a later capture cannot explain.
⚠ Owns the session `GameClock`: built per session (mode from `--det`/`--anim-lab`), published as
  `GameClock.Current`, nulled on teardown. `ProcessPriority = -1000` so `BeginFrame` runs before
  any consumer reads the clock — do not let another node undercut it (the Launcher sits one notch
  behind at -999). `DriveSimSteps` steps the `SimStep` consumers when the clock is not realtime;
  P/`.` are bound in `_UnhandledInput` for freecam/viewer only (flight polls P itself, the anim
  lab owns its own transport).
⚠ **Weather (PLAN-planeviewer-split A5) moved to `Session.WeatherRig`**: `_weatherRig` is
  constructed and `Build()`-ed here, but the per-rig horizon build LOOP stays inline — it's a
  `SceneBuilder` concern, not weather state — passed to `Build` as the `buildDomes` callback that
  runs between zone-resolve and fog/whiteout/puffs setup, at the same point the old inline code
  ran it; everything the callback builds reads the zone `Build` resolves, so dome + fog always
  share a zone. `_Process` drives the per-rig skydome/whiteout/deck/puffs update via
  `_weatherRig?.Tick(_rigs, dt)`; `_deckCenter` moved into the rig too (`SetDeckCenter`, called
  whenever a chapter's cloud deck geometry loads, independent of whether `_weatherRig` itself was
  built for this session). See `src/Session/WeatherRig.cs`'s entry.
⚠ Per-rig: the skydome is REBUILT per rig (star mesh `csky_light_fade` instance uniform); the cloud
  deck is duplicated + `CopyInstanceShaderParams` — `Node.Duplicate()` drops instance shader params.
⚠ **Owns the session `StartupProfile`** — built at the TOP of `StartSession` (before `_worldRoot`)
  and published as `StartupProfile.Current`; `EndBuild()` on the success return, `Frame()` from
  `_Process`, `Emit()` from `NotificationExitTree` for the runs that quit mid-build. Its phases here
  are `gamez` · `textures` · `sounds` · `zrdr` · `plane` · `weather` (dome + fog + cloud visuals) ·
  `edge`, all LEAVES; anything between them lands in `rest`, which is why `rest` is per-mode work
  (viewer ≈ 255 ms of lab construction, flight ≈ 240 ms of rig/pool/effects wiring, freecam ≈ 30 ms).
  A new load or build step gets its own `Record` or it silently inflates `rest`.
⚠ `RunDamageTest` (`--damage-test[=name]`, freecam) is the headless destructible harness: continuous
  HP sweep (C22 stages) or, with `--damage-hd=N`, discrete N-`HEALTH_DAMAGE` hits via `DamageAt`
  (C23/C24/C25/C26) — resolve✓ walk-up, healthy/destroyed swap, `col[off,on]` (colliders switched by
  the kill), and `debris[N]` (ballistic pieces the death launched). It forces `Collision` on (as
  `--debug-damage` does) so colliders EXIST; `--freecam` alone builds none. Discrete mode covers EVERY
  destructible (doors instant-die, no `DAMAGE_SEQUENCE`), continuous mode only the staged ones.
⚠ Discrete mode adds the world subtree to the tree (`ManualAdvance` so `_Process` doesn't
  double-drive) and `Advance`s the death ~3.5 s AFTER the swap/col census: the debris `OBJECT_MOTION`
  is scheduled (t≈2.2 s), so it needs the clock ticked — and ticking an out-of-tree world spams
  `!is_inside_tree` (global-transform reads). Swap/col are measured pre-tick (immediate post-death),
  debris post-tick; being in-tree also makes positions real (no more C23 (0,0,0) trap).
⚠ **The effect/crash stage factories moved out** (PLAN-planeviewer-split A4):
  `BuildWorldEffectsRuntime`/`EnsureWorldEffects`/`BuildFlightCrashRuntime` and the effect/crash-anchor
  name tables → `Session.WorldEffectsFactory` (`_worldEffectsFactory`, constructed at the top of
  `StartSession` alongside `_liveryResolver`). `--effects-test` (`RunEffectsTest`, still in
  `ProbeRunner`) is its headless verify: plays each effect at the camera point, seeds the RNG for
  reproducibility, `StopAll`s between names (they share `trailpuffer2`), and reports resolve✓ +
  puffer-built count (WORLD-12) to `./.scratch/effects_test.txt`. See
  `src/Session/WorldEffectsFactory.cs`'s entry for the runtime-ownership split.
⚠ `TriggerDestroy` (`--destroy=<name>`, F42) kills every destructible whose def/anim/anchor-`cs_name`
  contains the name (deduped to authoritative anchors, capped 64) via `DamageAt` — the swap fires
  synchronously, the runtime self-ticks the death out during the `--screenshot` warm-up. `--freecam`
  auto-frames the killed object (unless `--pos`/`--direction` set) and builds the world-effects runtime
  itself (gated on `--destroy`, so a plain `--freecam` regression is byte-identical) so its fire renders.

## src/Utils/GameClock.cs
The session's simulation clock: one object deciding how much sim time a rendered frame is worth.
`BeginFrame(wallDelta)` (first thing in `GameSession._Process`) sets `Steps` + `Dt`; consumers read
`FrameDt` once per frame, or loop `Steps` times on `Dt` when they must see each sub-step.
Published as `GameClock.Current` (session-scoped, nulled by `ReturnToMenu`); a null means "use your
raw frame delta", so nothing outside a session breaks.
⚠ Modes: **Realtime** (`Steps` 1, `Dt` = wall delta — arithmetically what every consumer used
  before this class, which is what makes the shipped modes byte-identical), **FixedAccum** (whole
  1/60 s steps from a wall accumulator clamped at 0.25 s — the interactive anim lab),
  **FixedStep** (exactly one `FixedDt × Scale` step per rendered frame — a scripted lab run and
  `--det`, which every scripted flag implies). `Halted` is orthogonal to all three: 0 steps until
  `StepOnce`. Under FixedStep `--frames=N` is exactly sim frame N (`sim_frame=120 sim_time=2`).
⚠ `PhysicsDt` returns 0 in every non-realtime mode and while halted, and **0 means the consumer
  returns without stepping** — `GameSession.DriveSimSteps` calls its `SimStep` instead, `Steps`
  times, in the tree order Godot's physics tick used (projectile pool → flight controllers;
  weapon lab → the pool it owns). Godot's physics tick keeps its own 60 Hz cadence regardless, so
  it cannot be the sim clock.
⚠ UI and camera code deliberately stays on the raw frame delta (HUD widgets, `SpectatorCamera`,
  the launchscreen, the flight chase/orbit camera, the `--perf` and unplaced-entity instruments):
  a halt must still let you look around, draw the HUD, and measure frame budgets.
⚠ The GPU reads the same clock through `ShaderTime` / the `csky_time` global — that is what makes
  a halt a true freeze-frame (measured: frames 120 and 300 of a halted C1 waterfall are md5-equal)
  and a `--det` shot a function of the frame count. Any new animated shader takes `csky_time`.

## src/Utils/Log.cs
The diagnostic log: `Log.Info("world", $"…")` / `Warn` / `Error` / `Debug` over nine categories
(`anim world flight weapons sound perf test ui core`) and four levels. Two sinks with different
jobs — the console is the human's, the `.scratch/logs/<mode>-<stamp>.log` file is the machine's.
⚠ **The file sink always takes EVERYTHING** — every category, every level, no filter. `--log=` only
  moves the *console* threshold. That is the point: a post-hoc grep can never miss a category
  nobody enabled before the run. Console default is info (what an unconverted `GD.Print` did);
  warnings and errors are never suppressible; `--debug-anim` implies `--log=anim:debug,sound:debug`.
⚠ Grammar: every line ends `[cat] message key=value`, the file prefixing a 5-char level token.
  **No timestamp column, deliberately** — a `--det` run must produce a byte-identical log; a line
  that needs time carries it as an explicit `key=value`.
⚠ A message is ONE `FormattableString`, rendered invariant. `$"a{x}" + $"b{y}"` is a `string` and
  will not compile — deliberately, since the concatenation formats its floats in the current
  culture first. A composite's own `ToString()` escapes it too: log the fields, not the record.
⚠ `Warn` prints plain via `GD.Print`, never `GD.PushWarning` (which appends a managed stack trace
  per call). Line-flushed `StreamWriter`, UTF-8 **with BOM** (PowerShell 5.1 reads BOM-less as ANSI
  and mojibakes the em dashes). Lines logged before `Open` sit in a 512-line prelude, flushed on open.
⚠ **Migration is incremental by decision, not by neglect — do NOT bulk-sweep the remaining
  `GD.Print` sites** (223 across 37 files; a bulk text rewrite has corrupted files here before,
  verification SHELL-3). New code uses `Log`; a family converts when an item touches it, keeping
  each site's original level unless a comment there says the level was compromised.

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
⚠ **It only reports.** No thresholds, no verdicts, no A/B — a comparison needs a warm-up protocol
  this class deliberately does not own (C22's).
⚠ The line asserts `total = boot + Σ(phases) + rest + first_frame` and that identity is checkable —
  keep every phase a LEAF (never nested inside another) or the sum silently double-counts.
  `rest` = build minus its phases: real uninstrumented work, not an error term.
⚠ `first_frame` is measured at the top of the SECOND `_Process` after the build, so the first draw
  (and its shader compilation) is inside it. A run that quits during the build prints
  `first_frame=none` — emitted from `GameSession`'s `NotificationExitTree`, the only hook those
  headless probes still reach — and its build closes at teardown, so its `rest` also holds whatever
  the probe itself did (`--damage-test`'s sweep). Don't read a probe run's `rest` as build overhead.
⚠ `boot` is engine start → build start, so on a launchscreen-driven rebuild it also holds however
  long the menu was up. Read it, don't assume the session was the process's first.
⚠ `Current` is null outside a session build **on purpose** — `--run-tests` builds eight census
  worlds through `WorldSession` and must not accumulate them into one line.

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
  A hot-path caller holds its stream reference (`ProjectilePool`) rather than re-resolving per draw.
⚠ `UI/LiveryLab`'s generator deliberately stays outside this — it is driven by a button press, and a
  wall-time/input-dependent path must never share a sim subsystem's stream.

## src/Testing/Probes.cs
The assertion cores behind the `--dump-markers` / `--dump-weapons` / `--dump-loadout` /
`--dump-flight` / `--damage-test` inspection reports. Each probe does the work
once and returns both halves: the report text the flag prints and writes, and a structured verdict
(counts, per-row booleans, failure strings) a `--run-tests` suite asserts on.
⚠ `FlightEnvelope` steps a throwaway `FlightModel` through the manoeuvres the ORIGINAL was
  recorded flying; its targets are the Bloodhawk's only, since it is the only airframe on video.
  A row with `Informational` set is measured but deliberately not asserted (an open question) —
  never promote one to a verdict without the measurement that closes it.
⚠ **One source of truth.** The flags in `ProbeRunner` are thin wrappers over these; a check added
  to a probe reaches both the report and the suite. Never re-implement a check in a suite.
⚠ **A verdict is a field, never a glyph.** The `✓`/`✗` in a report line is formatting; the boolean
  it came from is on `DamageRow`. Parsing a report back to automate it is the thing this replaced.
⚠ `Probes.SweepCap` (16) caps the swept ROWS, not the registry totals — a census must read
  `DamageResult.TotalInstances` / `DistinctAnchors` or it silently under-counts (LOG-5).
⚠ `Probes.Damage` needs the world subtree in the tree with `ManualAdvance` set: it ticks past the
  death schedule for the debris count, and an out-of-tree global-transform read returns identity.
⚠ `EnabledColliders` / `WorldRootOf` / `CountVariants` are the shared kill-census helpers — the
  world damage lab reports through the same three, so the panel's numbers and `--damage-hd`'s
  cannot drift apart (measured equal on C1's `ap_h2otwr1`).
⚠ Every probe forces `CultureInfo.InvariantCulture` — the damage report used to write `HEALTH 0,01`
  on this German machine.

## src/Testing/TestHarness.cs
`--run-tests[=filter]`: the suite registry, `TestContext` (assert verbs, resolved data paths, a
scene-tree host, and `WithWorld` — the chapter-world builder over `WorldSession`), the
PASS/FAIL/SKIP table, `.scratch/test-report.json`, and the process exit code.
⚠ **In-engine is the smaller half.** Only checks that need a live Godot belong here — a built
  plane read by global transform, a ticked chapter world, a spawning projectile pool. Anything
  that runs without the engine goes in `CSVM.Tests` (`dotnet test`) instead.
⚠ **SKIP is never PASS.** A suite whose input is absent throws `SuiteSkippedException` via
  `RequireData` and is counted separately; the run still exits 0. "No data" must not read as green.
⚠ **Engine-error policy.** Native `ERROR: …` lines are C++ `ERR_FAIL_COND` prints and cannot be
  intercepted from C#, so they are screened out of band: the run reads its own engine log
  (`--log-file`, else the project's default rotating log if this run wrote it) and classifies each
  error against `ErrorAllowlist`. **Every allowance carries a cap and its measured count is printed
  even on a pass** — an uncapped or invisible allowance is how a new error hides inside an old
  one's shape. Unknown error → fail; over cap → fail; no log → SKIP, never PASS.
⚠ `Screen` is pure (no Godot API, no IO) and unit-tested in `CSVM.Tests` — the classifier is the
  one part of the harness that could turn the whole thing into a rubber stamp.
⚠ Run **windowed**: `--headless` compiles no shaders, so a clean error screen says nothing about
  them (LOG-8). The harness logs a warning when it detects the headless display.
⚠ A suite must never write outside `.scratch/`; `WriteArtifact`/`ScratchDir` are the only route.

## src/Testing/Suites.cs
The nine registered suites: `weapons-defs`, `flight-envelope`, `markers-rig`, `loadout-bind`,
`weapons-fire`, `damage-stages`, `damage-hd`, `destructible-census`, `tex-dropin`.
⚠ The expected numbers are **golden counts against the retail install** (48 weapon defs, 11
  airframes, the per-chapter destructible census) — the data is a fixed input, so they are
  invariants. Change one only with the measurement that moved it. `flight-envelope`'s targets are
  golden in the same sense: they measure the original itself, not our model.
⚠ `weapons-fire` asserts `skipped == 0` as well as `ok == 48`: a skipped weapon is a
  success-looking outcome (no mount on this plane) that nothing else would notice.
⚠ `--loadout=<def>` reaches `loadout-bind` — `--run-tests=loadout-bind --loadout=pbloodhawk` is the
  real able-to-fail control (a def wanting `firepoint8` bound to the 7-firepoint Kestrel).

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
The `--dump-markers`/`--dump-weapons`/`--dump-flight`/`--dump-loadout`/`--run-tests`/
`--effects-test`/`--damage-test`/`--destroy=` probe wrappers (PLAN-planeviewer-split A1),
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
save (PLAN-planeviewer-split A2), constructed once in `Launcher._Ready` from the launch spec
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

## src/Session/Launcher.cs
Main.tscn's root (PLAN-planeviewer-split B7): the once-per-process bootstrap — CLI parse into
`_cli`/`_spec`, data-root precedence + base paths, `Pads.Disabled`/`TextureDropIn`/`Log`/master-seed
side effects, the `--dump-*`/`--run-tests` early quits — plus everything that persists across
in-process relaunches: camera, orbit rig, sun, WorldEnvironment, launchscreen, focus mute, and the
per-frame shader clock / `--perf` / capture tick. `LaunchSession()` instantiates a `GameSession`
session node per launch; `ReturnToMenu` calls its `Teardown()` and frees it.
⚠ `GlobalShaderParameterAdd` (fog / world-light / `WorldLights` / `ShaderTime.RegisterGlobal`) runs
  in `_Ready` ONCE, ahead of both the dump branches and the first shader build — a session rebuild
  must never double-Add (that errors; `WeatherRig.Build` only `Set`s), and Godot refuses to compile
  a shader naming an unregistered global.
⚠ **The `--det` bundle is resolved on the SPEC, not here** — what stays is the half a pure value
  cannot do: drawing an unpinned master seed from the clock (`PinnedSeed ?? Rng.TimeSeed()`),
  writing `Pads.Disabled`, and announcing the resolved set on one `[core] det …` line whose absence
  means the run was interactive. **Never let a constituent leak into an interactive default** — a
  bare `--fly` keeps its random spawn, random liveries and live pads.
⚠ **A scripted session HIDES its window, an interactive one asks for focus** — the same predicate
  drives both, right after the `--det` block. `ScriptedWindow.Hide()` uses `ShowWindow(SW_HIDE)`;
  **never swap that for minimize**, which stops rendering and blanks every capture (SHOT-16). Both
  directions are load-bearing: get the predicate wrong and either a test run covers the desktop or
  somebody's game launches invisible.
⚠ **It holds the pristine `_cli`; `_spec` is what the live session was built from.** A menu launch
  replaces `_spec` with `SessionSpec.FromMenu(_cli, …)` — derived from `_cli`, never from the
  outgoing spec. **A new flag is a SessionSpec change, never a Launcher or context field.**
  `_pendingJoin` (consumed by the first launchscreen) and `_menuPads` (the join flow's binding,
  handed to the session via `LauncherContext`) are session/join state, not args.
⚠ `_Process` runs at priority -999 — right behind the session node's -1000, ahead of everything
  else, the same point in the frame the single-root class ran. `ShaderTime.Advance` is
  UNCONDITIONAL: the null-clock branch (launchscreen, the frame after a teardown) keeps shader
  animation on wall time so nothing stalls behind the menu. The only clock it owns is
  `RunTestSuites`' out-param; everything else reads `GameClock.Current`.
⚠ **`ReportPerf`'s window is 60 RENDERED frames, not a wall second** — under `--det` exactly 60 sim
  steps, so two runs produce the same number of samples, which is what makes `RunTests.ps1 -Perf`'s
  paired medians comparable. Keep the line one flat `key=value` string: the script parses it.
  **`physics_ms` is empty under `--det` by construction** — see `GameClock.ParentDriven`.
⚠ F11 (`CaptureDirector.PrintPlacement`) prints the SUBJECT, per mode: in flight player 1's plane
  pose (position + nose `-Z`), not the chase camera; in the orbit view `--pos`/`--lookat` (only a
  point reproduces the radius); elsewhere `--pos`/`--direction`. Directions print to 5 decimals —
  3 would quantise a unit vector's aim to ~0.03°.
⚠ Focus mute is the master-bus mute on purpose; `MixGain = 0` is the wrong mechanism — WorldSounds
  has no gain plumbing and one-shots bypass `MixGain`, so most audio would stay audible.

## src/Session/LiveryResolver.cs
Resolves which livery each player flies (PLAN-planeviewer-split A3, moved verbatim off
`GameSession`): the shipped paint catalog (`PaintCatalog`, lazy + cached), the per-pattern
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
Resolves each player's flight spawn (PLAN-planeviewer-split A3, moved verbatim off `GameSession`):
`ChooseSpawnBase` (the shared `--spawn=`-or-random list index), `ChooseSpawn` (a player's
position/look-at from that list, objectives.json `PLAYER_INIT`, or the `--spawn-at=` debug
override), and `LogSpawn`. Constructed once per session build (`_spawnPicker`, same lifetime as
`LiveryResolver`).
⚠ `ChooseSpawnBase`'s random branch draws from `Rng.Stream(Rng.Spawn)` — under `--det` this is
  pinned by the master seed same as before the move; do not reorder relative to other RNG draws.

## src/Session/PlaneRoster.cs
Static, spec-free lookups over a `SessionSpec`'s plane roster (PLAN-planeviewer-split A3, moved
verbatim off `GameSession`): `PlaneFor(spec, index)`, `PlaneDisplayName(stats)`, `Humanize(s)`.
No session state — every call takes the `SessionSpec` explicitly rather than caching one, since
these are pure over their arguments.

## src/Session/FlightRigAssembler.cs
Assembles one player's flight rig (PLAN-planeviewer-split C10, moved verbatim out of
`GameSession.BuildFlightRigs`): the painted plane model, the `FlightController` and everything hung
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
Builds the impact/destruction effect stages and the per-player crash runtime
(PLAN-planeviewer-split A4, moved verbatim off `GameSession`): the world-effects runtime (D32) and
`BuildFlightCrashRuntime`. Constructed once per session (`_worldEffectsFactory`, same lifetime as
`LiveryResolver`/`SpawnPicker`) from `(SessionSpec, Node3D worldRoot, Func<Vector3> playerPosition)`
— the ctor closure over `GameSession`'s `_rigs`/`_camera` replaces the old inline lambda, unchanged
in effect since it is only ever evaluated per-frame from inside the built `AnimRuntime`.
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
⚠ `BuildWorldEffectsRuntime`/`EnsureWorldEffects`/`BuildFlightCrashRuntime` read `_spec.DebugAnim` and
  `_worldRoot` off the factory instance instead of a passed session — same values, same lifetime, just
  no longer re-passed at every call site.

## src/Session/WeatherRig.cs
Loads/applies the flown mission's weather and drives its per-frame rig state
(PLAN-planeviewer-split A5, moved verbatim off `GameSession`): `LoadWeather`/`SetupWeather` become
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
⚠ Every getter self-registers `(key, default)`. `--dump-config` emits that registry as a full nested
  template; `ReportOrphans` warns loudly about file keys no getter queried (the typo detector); a
  queried-but-missing key on a *loaded* file warns once. `WarmTuningRegistry` (moved here from
  GameSession, PLAN-planeviewer-split A3) steps a throwaway `FlightModel` once, on every launch
  before `ReportOrphans`, so both are complete with **no built world / no game data**.
⚠ Read-only this pass — nothing writes the file; `res://` was chosen so a writable `user://` layer
  can later stack UNDER the getters without touching a call site. Loaded once at `_Ready`; live-reload
  (re-parse on mtime) is deferred but cheap because reads are already read-through.
⚠ Only `FlightModel` is wired so far (its 15 `TUNE` consts, read into locals at the top of `Step`
  so every key registers even on a frame that skips the stall/knife branches); other modules still
  read their consts directly. `config.json` is git-ignored — the consts stay the canonical values.

## src/Utils/ScriptedWindow.cs
Win32-only window hiding for scripted runs: `ScriptedWindow.Hide()` calls `ShowWindow(SW_HIDE)` on
the native window handle (PLAN-planeviewer-split A6, moved off the old session bootstrap's
`HideScriptedWindow`). Fully static, one call site in `Launcher._Ready` right after the `--det` block — the same
predicate drives both window hiding (scripted run) and focus request (interactive run).
⚠ Hiding is not minimizing: a minimized window stops rendering, which blanks every screenshot
  capture. `ShowWindow(SW_HIDE)` is load-bearing — never swap it for minimize.
