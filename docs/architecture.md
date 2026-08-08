# Godot project — per-module implementation notes

**Start at the module index below.** One routing line per module, grouped by namespace. Find the
module there, then read only its entry: `Grep "## src/Mech3/SceneBuilder.cs" -A 12` returns the
whole thing, because the entry shape guarantees it. **Never read this file whole** — it is ~110 KB.

One `## src/...` entry per module in `CSVM/src`. Entry shape: 1–2 sentences of purpose beyond the
index line, then every still-binding constraint or deliberate-design marker as a `⚠` one-liner.
Body ≤ ~8 lines (~12 for the heaviest modules). Entry order is historical, not grouped — the index
is the map, grep is the lookup.

Narratives, diagnoses, and landed-work stories do not live here: they go in the commit message,
and git history keeps the rest (pre-2026-08-06 narratives: `docs/HISTORY.md`). Knowledge about the
game's data formats belongs in `docs/formats/`, not here.

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
- `src/Mech3/ConflictRank.cs` — the world's cross-node draw-order tie-break: ranks nodes by their conflict graph, one slot per coplanar layer.
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
- `src/Mech3/FogVolumes.cs` — the `fogvol.zrd` reader + the gamez `fvol*` volume census: what the ambient cloud field scatters, and where.
- `src/Mech3/Zrdr.cs` — zrdr extraction reader (zip or dir) + `ZrdrDict`, the key/[values…] view over a reader's list.
- `src/Mech3/AiNets.cs` — the chapter AI patrol nets: `ne0NNNNN` waypoint graphs + the `neindex` id→name table, raw tags/trailer included.
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
- `src/Mech3/Anim/EmitterDirector.cs` — every PUFFER_STATE emitter's whole life on one runtime: the keying rule, the start, all four stops, the respawn wipe, the per-frame follow, and the census. Plus `IEmitter`/`IEmitterFactory` and the real/retired adapters.
- `src/Mech3/Anim/NameResolver.cs` — name→node resolution: the index, wildcard matcher, memoized `FindAll`, the three-tier scope chain (`Resolve`/`ResolveScoped` with the `ownRootsOf` hook), the symbol authority, `Anchors` (narrowing + root lift) and the bind census; generic over the node type, off-engine testable.
- `src/Mech3/SequenceRunner.cs` — the engine-free sequence interpreter (event clock / LOOP / IF-ELSEIF), extracted behind the 3-member `ISequenceHost` seam; headlessly testable.
- `src/Mech3/DestructibleRegistry.cs` — live per-instance HP for `HEALTH>0` anim defs, one pool per `(def,anchor)`; `Resolve` maps a struck collider back.
- `src/Mech3/WorldSession.cs` — builds a chapter world + binds its `AnimProgram` (load→WorldBuilder→clutter→bind→sound-prewarm); `--node=` slices it to one subtree.
- `src/Mech3/SessionArchives.cs` — `OpenFor(ArchiveIntent)` opens the five archives a chapter build needs and the matching `WorldSession.Options` lifetime flags, so `GameSession`, the anim lab and the test harness open the same five without hand-setting the flags.
- `src/Mech3/EmptyStage.cs` — the `--stage=empty` test stage: a collidable ground plane under a code-generated grid, standing in for a chapter world.
- `src/Mech3/WavFile.cs` — pure-C# WAV parser + MS ADPCM→PCM16 decoder (the game's format; Godot can't load it).
- `src/Mech3/SoundArchive.cs` — WAV lookup over a sounds extraction → cached `AudioStreamWav` (forward loop when LOOPED).
- `src/Mech3/SoundDefs.cs` — sounds.json parser: SETS `snd_*` → `SoundDef`; `LoadGroups` → the weighted-random `SOUND_GROUPS`.

### `src/Flight/` — the flying aircraft

The plane as a flying, shooting, damageable thing, plus its HUD and stunt mode. Reads plane stats
from the extracted zrdr; owns the arcade physics and everything drawn over the pilot's view.

- `src/Flight/PlaneStats.cs` — typed per-plane stats from vehicle/engines/player.json: dynamics, engine sound, destroyable parts.
- `src/Flight/ShakeDefs.cs` — typed reader over shakes.json: the six shake-oscillator sources (law + per-source magnitude term).
- `src/Flight/WeaponDefs.cs` — typed reader over `weapons.json` `BALLISTICS`: 48 `WeaponDef`s; inspect with `--dump-weapons`.
- `src/Flight/Loadout.cs` — `stock_loadouts.json` reader + `Bind` to a built plane: gun groups + hardpoints, markers→muzzle nodes; `--dump-loadout`.
- `src/Flight/WeaponBench.cs` — the world-less 48-weapon mount-and-fire pass check behind `--weapon-test` and `weapons-fire`; fires the whole `ForRig` rig, no lab node involved.
- `src/Flight/FireControl.cs` — the engine-free fire-control state machine (BL-295): trigger edges, fire clocks, ammo draw-down, both selectors, dry cues; `FlightController` performs its `FireOutcome`.
- `src/Flight/WeaponCursor.cs` — `FireControl`'s internal ammo-slot index math (`NextArmed`/`NextSelectable`); nothing else calls it.
- `src/Flight/Ballistics.cs` — the VELOCITY/ACCELERATION/GRAVITY integration step, shared by `ProjectilePool` and the reticle's projected impact point.
- `src/Flight/CamParams.cs` — one aircraft's camera tuning from `camparam.json`: `default` plus its own block, keyed by DISPLAY name. Only `Dist` is applied.
- `src/Flight/CameraController.cs` — the flown plane's camera: roll-following chase, numpad fixed views, paused orbit. Steers a `Camera3D` it does not own.
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
- `src/Flight/VersusMatch.cs` — Dogfight deathmatch bookkeeping: per-player kills/deaths, the host-fed match clock, threshold/time-out completion, ranked standings.
- `src/Flight/Weather.cs` — weather.json reader → `WeatherState`: per-zone fog, sunlight, cloud whiteout, wind, precipitation.
- `src/Flight/FlightAudio.cs` — own-plane loops (engine, overspeed whine, rattle) + crash/prop one-shots, per-player `MixGain`.
- `src/Flight/SpectatorCamera.cs` — the `--freecam`/`--anim-lab` observation camera: RMB-look + WASD/QE, no roll; `Frame`/`FollowNode` track an object.
- `src/Flight/FlightModel.cs` — the arcade velocity-vector flight physics: thrust/drag/gravity/lift, stall, calibrated control rates.
- `src/Flight/PropAnimator.cs` — spins the collected prop/rotor discs about their local axes, throttle-scaled (idle floor 0.4); `--fly` only.
- `src/Flight/ThrottleSlamSmoke.cs` — a large throttle jump streams dark exhaust trail smoke for a few seconds; a single notch or a decrease shows nothing.
- `src/Flight/ControlSurfaceAnimator.cs` — deflects ailerons/elevators/rudders to an absolute pose from slewed stick input; `--fly` only.
- `src/Flight/WingLightBlinker.cs` — blinks the wingtip flares 0.08 s every 1.5 s, reset off on respawn; `--fly` only.
- `src/Flight/PylonOrdnance.cs` — the rockets under the wings: one FLYOUT-model body per loaded pylon, hidden as its ammo depletes; `--fly` only.
- `src/Flight/PlaneShake.cs` — the plane-wobble oscillators (gunfire buzz, overspeed rattle, being-hit rocks) summed to visual-only roll on the rig's ShakePivot.
- `src/Flight/PlaneCollider.cs` — derives 5–8 plane-frame collision boxes from the built model's triangles, with no per-plane data.
- `src/Flight/CollisionLayers.cs` — the named physics layers (world / aircraft): the one place a layer bit is assigned a meaning.
- `src/Flight/AircraftBody.cs` — the flying plane's physics body: the shared `PlaneCollider` boxes on the aircraft layer; struck shape → part name.
- `src/Flight/PlaneDamage.cs` — per-part HP model from vehicle.json `destroyable_parts`; maps struck box + impact point to a data part.
- `src/Flight/DamageVisuals.cs` — flips the torn-skin `pdpN` panels (paired by mesh position) at the data's injure thresholds, plus fire trails.
- `src/Flight/DamageLab.cs` — the `--damage`/F5 slider UI: one HP slider per part, driving the parked plane's DamageVisuals or the flown plane's real PlaneDamage.
- `src/Flight/CompassTape.cs` — the top-centre heading tape from the game's own HUD textures, drawn as a cylindrical drum seen edge-on.
- `src/Flight/GaugeCluster.cs` — the cockpit dials as HUD (altimeter/speedo/damage + gun/missile), geometry from the plane's `gauges` subtree.
- `src/Flight/FlightController.cs` — the flying-aircraft node: input → FlightModel → transform, chase camera, HUD, collision/crash, respawn; `FireControl`'s engine adapter.
- `src/Flight/PlayerRig.cs` — one rendered view's state: camera, SubViewport, HUD parent, visual layer, controller, own sky/deck/puffs.

### `src/Effects/` — particle systems

- `src/Effects/Puffer.cs` — data-driven `PUFFER_STATE` billboard-particle emitter: burst, distance-trail, or sustained at-node modes.
- `src/Effects/EmitterRenderer.cs` — the `IEmitterRenderer` seam under `Puffer` (particles → GPU) and the real `MultiMesh` + billboard-shader renderer behind it.
- `src/Effects/FogVolumeClutter.cs` — the authored ambient cloud field: `fogvol.zrd`'s clutter scattered through the gamez `fvol*` boxes, one static MultiMesh per sprite kind.
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
- `src/UI/AiNetsOverlay.cs` — the AI patrol-net overlay (F13, `--debug-ainets`): the chapter's nets as coloured graphs with labels + census log.
- `src/UI/WeaponLab.cs` — the weapon lab panel (B): steppers that arm the held plane's live loadout, click-to-place on a real world surface. Fires nothing itself.
- `src/UI/PanelFocus.cs` — the one-line rule every flight-hosted panel applies: no widget takes keyboard focus, or a focused button eats the fire key.
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
- `src/Testing/CountingEmitterFactory.cs` — the no-GPU `IEmitterFactory` fake a suite installs to observe `PUFFER_STATE` emitter lifetime.
- `src/Testing/RecordingEmitterRenderer.cs` — the no-GPU `IEmitterRenderer` fake that keeps a `Puffer`'s particles instead of drawing them, so its three modes are assertable.
- `src/Testing/Suites.cs` — the 26 registered suites and their golden counts (48 weapon defs, destructibles, glTF round trip). Six no-blocker suites (`flight-envelope`, `gauge-colours`, `gauge-arrow-tween`, `weapons-defs`, `weapon-blast`, `markers-rig` — 11 airframes, blast/fuse rules — moved to `CSVM.Tests` (`FlightEnvelopeTests`, `GaugeColoursTests`, `GaugeArrowTweenTests`, `WeaponsDefsTests`, `WeaponBlastTests`, `MarkersRigTests`) since their bodies called only `Probes.*`/plain statics with no live Node — `PLAN-engine-free-suites.md` A3. `GaugeCluster`'s colour/sweep statics (`GunIndicatorColor`, `HardpointIndicatorColor`, `SlotIndicatorColor`, `DamageZoneColor`, `TargetArrowAngle`, `TweenArrow`, `IndicatorLowFrac`, `ArrowSweepDegPerSimS`) went `internal` → `public` for the move; `StallBlinkHalfPeriodS`/`AdvanceStallLamp` and the stall-specific consts stay `internal` (`stall-warning` is Wave B, scoped to `GaugeCluster` only).
- `src/Testing/GoldenShot.cs` — the engine half of the golden-image tripwire: raw-pixel md5 + GPU adapter, printed on every `--screenshot`.
- `src/Testing/ProbeRunner.cs` — the `--dump-*`/`--run-tests`/`--*-test`/`--destroy=` probe wrappers the Launcher and the session node quit into.
- `src/Testing/CaptureDirector.cs` — the `--screenshot=`/`--shots=`/`--frames=` capture state machine + F11/F12, ticked from `_Process`.
- `src/Testing/GltfExporter.cs` — exports the viewer plane subtree to glTF (mesh + current livery + baked damage) for `--export-gltf=`/F10; converts shader skins on a throwaway duplicate.

### `src/Session/` — the launch/session layer

The `Launcher` scene root, the per-launch `GameSession` node, and the low-coupling session-build
clusters they delegate to.

- `src/Session/Launcher.cs` — Main.tscn root: the once-per-process bootstrap (args → paths → log/seed/window), shader-global registration, persistent camera/lighting, launchscreen + menu flow; instantiates a `GameSession` session node per launch.
- `src/Session/GameSession.cs` — the per-launch session node (instantiated by `Launcher`): builds one session — rigs, world, plane, HUD, weather — from its `SessionSpec`; return-to-menu `QueueFree`s it.
- `src/Session/ExtractionStamp.cs` — boot-time check of `extracted/VERSION.json` (the provenance stamp the extraction scripts write): schema const + at most one warning line when the stamp is stale, missing, or unreadable.
- `src/Session/LiveryResolver.cs` — resolves each player's livery against a `SessionSpec`: the paint catalog, the pattern-mask library, and the per-player scheme pick.
- `src/Session/SpawnPicker.cs` — resolves each player's flight spawn against a `SessionSpec`: the shared spawn-list index and the per-player point (or the `--spawn-at=` override); the plain `IFlightStarts`.
- `src/Session/IFlightStarts.cs` — the spawn-placement seam: one call answering for the **whole field** at once, plus the `FlightStart` pos/look-at pair every rig is placed from.
- `src/Session/RaceGrid.cs` — the race starting grid: every pilot fanned symmetrically about one anchor spawn on its heading, with the whole field lifted as one to clear terrain.
- `src/Session/PlaneRoster.cs` — pure lookups over a `SessionSpec`'s plane roster: which plane a player flies, and its display name.
- `src/Session/EffectCatalogue.cs` — the record of which authored anims are playable effects, and what their defs need staged: the effect/crash/damage-shim name tables, the pure `TouchdownFor` graze pick, and the anchor-root derivation both binds stage from.
- `src/Session/EffectPools.cs` — the `data/effect_pools.json` reader: how many copies of each effect template the stage builds, per ROOT, scaled by player count.
- `src/Session/FlightRigAssembler.cs` — assembles one player's flight rig: painted plane, `FlightController`, loadout/ordnance, HUD instruments, damage visuals, audio, stunt run, spawn, crash runtime.

### Session root and tests

- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy (span every pad) plus the `--no-pads` switch.
- `src/SessionPaths.cs` — resolves extracted-data paths (per-chapter gamez/texture/zrdr; `PreferUnzipped`); extracted from `GameSession`.
- `src/SessionSpec.cs` — the launch args as one immutable, engine-free value: `Parse` parses **and** resolves (closed `SessionMode`, `--det` bundle, placement, `BuildsCollision`), plus the pure arg parsers.

- `CSVM.Tests/` — the xUnit project (`dotnet test`): engine-free reader units on hand-authored fixtures + `extracted/` golden counts, skipped when absent; plus, since `PLAN-engine-free-suites.md` (A3/A4/B11), eight former in-engine suites moved here as `Probes.*`/plain-static/`StuntMission`/`GaugeCluster` facts.

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
Carries `flags.active` as `Active` (same default) — the build script's own `NodeSetActive` record,
honoured by `WorldBuilder.Add`, which skips building an inactive world-build root outright.
`IsMarkerGizmo(meshIndex)` classifies a mesh as an authoring mark rather than scenery (one flat-
coloured untextured triangle — see docs/formats/world-structure.md); SceneBuilder draws none.
⚠ GameZNode.Index is the flat list position, NEVER the unified JSON `index` (1-based, duplicated);
  child_indices are flat positions too — getting this wrong rebuilds the graph without erroring.
⚠ The unified transform `scale` is deliberately ignored (measured unit on every transformed node).
Carries each model's `flags.lighting`/`flags.fog` as `GameZMesh.Lighting`/`Fog` (default true) —
the original's self-lit and unfogged marks, honoured by SceneBuilder's per-model shader variants.
Parses the WHOLE per-polygon `materials` list: element 0 is the base skin, the rest become
`GameZPolygon.OverlayPasses` (`GameZPolygonPass`: material + its own UVs), which SceneBuilder draws
as extra surfaces — 619 polygons install-wide, none in planes.zbd (docs/formats/gamez.md).
⚠ ModelType/FacadeMode/TextureScroll are unified-only: null/zero on a legacy tree, SceneBuilder
  falls back to its texture-name heuristic. Reading both shapes keeps a v0.6.1 rollback data-only.
Parses the per-polygon `zone_set` list into `GameZPolygon.ZoneSet` (`int?`, unified-only; at most
one value per polygon install-wide — docs/formats/world-structure.md's census); nothing reads it
yet, parse+census only (`BL-057`).

## src/Mech3/TextureArchive.cs
Texture lookup over an unzbd texture zip or unpacked PNG dir; absorbs the stored-name quirks
(20-char truncation prefix match, legacy `.-N` renames, the fork's trailing doubled period — see
docs/formats/gamez.md) and classifies each texture's alpha channel via LastHadAlpha /
LastAlphaIsSoft ("soft" = the ink is mostly partial alpha: opaque/ink < 0.45, measured install-wide
in `analysis/alpha-classification/`; drives blend-vs-scissor — scissor both erases sub-0.5 ink AND
solidifies partial alpha above it, so only essentially-binary ink scissors faithfully).
`Build` is the one construction path — decode, classify, drop-in, mip chain — and `Find` caches
its result; `BuildMipped` hands the same Image to `--dump-mips` un-cached.
⚠ Unresolved names are reported ONCE via plain GD.Print (MissingTextures), never GD.PushWarning —
  Godot .NET prints a full managed stack trace per PushWarning call and buries real errors.
⚠ **Mip levels 1/2 are the archive's authored `_1`/`_2` siblings** (`Mips`, `--mips=`), installed
  over an already-generated chain — so ordering is load-bearing: the alpha-softness read needs raw
  base pixels and runs BEFORE any of it, the scissor-cutout alpha-coverage rescale
  (`ScissorMipsKeepCoverage`, boost-only — the fix for lattices vanishing at distance) runs on the
  generated chain BEFORE the authored install so shipped levels keep artist alpha, and a sibling is
  refused unless it is exactly half/quarter the base's size. `MipSource.Generated` restores the pure box filter bit-for-bit (verified: it
  reproduces the pre-2026-08-02 hashes of all three goldens the change moved).
⚠ `TextureDropIn` (the `--tex-override`/`--tex-census` hook, at Find because it is the one resolve
  point) swaps **RGB bytes only** — size, format and alpha stay the original's, so alpha class /
  blend-vs-scissor / silhouette match a normal run (measured: 114,820 px on and off); authored mip
  levels are flattened with the same colour. Census colours are a pure hash of the name, never
  assignment order; ~0.5 % collide — warn per collision, never nudge. Counts are chromaticity-based
  LOWER bounds (docs/cli.md).

## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order as depth bias:
priority, then subface, then overlay pass, then within-mesh surface rank, with the cross-node
tie-break coming from `NodeBiasOf` — the world's `ConflictRank` map where the caller set one, the
flat node index otherwise (aircraft, `--node=`). Every `node_bias` in the project goes through
that one method. `CollidersForMesh`
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
⚠ A polygon's overlay passes become their own surfaces, appended after every base group, ordered by
  `OverlayPassBias` and NOT by surface rank — 97 of the 307 overlay-bearing models are already at
  the rank cap, where an appended group would share its base's rank and z-fight it. Declined on
  sprite/facade meshes (no biasable material); `OverlayPassDeclinedCount` is the tripwire and is 0
  across the install.

## src/Mech3/ConflictRank.cs
The world's cross-node draw-order tie-break. Buckets every built triangle by its world plane,
finds the cross-node pairs that are coplanar, same-priority, same-subface and genuinely overlap
(clipped area > 1 m², never an AABB touch), and layers that DAG by longest path — so a node's rank
counts conflicting layers beneath it, not nodes before it. Every edge runs low node index → high,
so the layering is a topological order of the original's own draw order and cannot invert authored
layering. `WorldBuilder.RankConflicts` runs it before the build; 11–45 ms per chapter.
⚠ The step (`SceneBuilder.ConflictRankBias`, 1.2e-5) is boxed in from both sides and is the only
  value that fits: it must exceed `SurfaceRankCap × SurfaceRankBias` = 1e-5 or within-mesh rank
  out-bids it, and `ConflictRankCap × it + 1e-5` must stay under `SubfaceBias` so subface +
  tie-break keeps inside one priority level. Measured in Godot: 5e-6 leaves a coplanar pair
  swapping winner on 1,774 px under a 1 mm camera move, 1.2e-5 leaves 0 (analysis/bl-053-dense-rank).
⚠ The origin-parked pile is EXCLUDED from the graph — it is hidden at bootstrap
  (`WorldBuilder.HideUnplacedEntities`) so nothing in it is on screen to fight, and it alone
  stretches the longest chain from 7 to 28, which no admissible step fits inside a priority level.
⚠ Plane bucketing is float and pairs each bucket with the next offset up. That is deliberate: the
  float geometry is what the GPU renders, so it is the right question to ask, and the neighbour
  pass recovers pairs a quantisation boundary splits (C5 294 → 326).

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
  (plane hitboxes, `EmptyStage`'s ground) are untracked and keep working.

## src/Mech3/PlaneBuilder.cs
Builds one aircraft from its GameZ subtree (shaded, cullBackfaces: true — interior lattice must be
backface-culled or it paints over the skin), skipping cockpit/destroyed/shadow/*_hook subtrees.
Repaint(scheme) re-liveries the built plane in place; BuildDestroyed builds the wreck subtree with
the plane-root→destroyed transform chain baked in; WingFlares/DamagePanels expose collected nodes.
Flight (`spinningProps`) now builds the static `staticpropN` disc alongside the spinning blur discs
it always built, not just one or the other — the startprops/stopprops choreography cross-fades
between them at spawn/engine-stop (`FlightController`), so both must exist. `staticrotorN` (the
autogyro) is unaffected and stays skipped in flight — that def only names propeller nodes.
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
⚠ Units are settled fact, not a TUNE (`BL-264`): these are the exact `spin_rotorN`/`spin_rotorNb`
  authored rates (-220/60/165/-60 deg/s), and `PropAnimator` converts them through the same
  `Mathf.DegToRad` + accumulate-from-rest decode `AnimDefs.Spin`/`SpinMotion` apply to the ambient
  world's own `XYZ_ROTATION` spins — verified: same numbers, same conversion, no separate guess.

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
LIGHT_STATE COLOR), the blink period (1.5 s = its LOOP SEQUENCE_OFFSET) and the point-light range
(0.5-1.25 m, also LIGHT_STATE). PlaneBuilder hides and re-skins the flares (additive tint, one-sided
as authored, no billboard); WingLightBlinker flashes them and emits a matching OmniLight3D per side.
⚠ oil_liteflare also skins a few real airframe meshes — scope the additive-tint treatment to the
  flare NODES by name, never through the texture, or those meshes lose their real shading.

## src/Mech3/WorldBuilder.cs
Builds a chapter world (fullbright): World children + partition-referenced subtrees; skips `horizon`
(`BuildHorizon` makes the camera-anchored skydome, and is the ONE caller that sets
`SceneBuilder.ForceFogged` — every horizon model in every chapter is authored `fog: false`, and
honouring that on a dome that is 2.5x scaled ~22 km out would delete the horizon band; its
`lighting: false` is honoured), `fvol*` (`IsFogVolumeNode`, shared with `FogVolumeSpec.VolumesOf`
so the skipped set and the cloud-scatter set are one list), `dzpaths`.
`HorizonZonesOf`/`HorizonZones` census the `horizon` node's `zone*` children with the meshed-node
count each subtree carries — read BEFORE the build, because the zone the dome and the fog share is
picked from it (`Flight.WeatherState.PreferPopulatedHorizonZone`; three chapters ship a `zone2`
that is a bare marker). Static over a `GameZ` so it needs no built scene and is testable
off-engine.
⚠ `BuildHorizon` builds exactly the zone it is handed, INCLUDING an empty one — the selection is
  the caller's, and an explicit `--sky-zone=` is meant to be able to show a bare marker's nothing.
  Its own name-absent fallback (first zone child) stays a no-op in the normal path.
`BuildDzPaths` is its
debug-only custom renderer: material-matched gate polygons green/red at 50% alpha, route as an open
white line strip (never a filled or closed polygon).
Splits the overcast deck into
`CloudDeck` (GameSession moves it with the player); hides origin-parked unplaced vehicles.
`CloudClusters` censuses the OTHER ambient cloud population after the walk — every `cloudparent`
subtree the world places (C1 28, C1B 70, C1C 30, C4 45; C2/C2B/C3/C5 none), logged per chapter so
"none" cannot read like a broken census. `GameSession` puts them on `UI.SplitScreen.CloudFieldLayer`
beside the `fvol` clutter, because a camera's altitude gate treats the two as one population
(`Session/WeatherRig`, `A7`).
⚠ `CloudClusters` matches on the node's ORIGINAL gamez name (`AnimRuntime.NameMeta`), never
  `Node.Name`: all 28 of C1's are literally named `cloudparent`, so Godot's duplicate-sibling
  renaming is free to have touched the built name (WORLD-8). They are nested too deep for a walk
  root to recognise (`world1 → g0|g27816 → l2586` Lod `→ cloudparent`), which is why this is a
  post-walk pass over the built tree rather than a `FindCloudDeck`-style pre-pass.
`NoCollisionNode` exempts three rendered-but-not-solid classes, subtree-inherited: sky/cloud
textures, billboards, and any node with gamez `intersect_surface` false — the original's own
collision flag, false on props/debris/effects/glows and the C3 spiderweb (docs/formats/gamez.md);
honouring it is what lets the plane fly through the web and wreck debris as the original does.
A static probe re-deriving this walk from `extracted/` alone (`analysis/collider-probe/probe.py`)
reproduces `SceneBuilder.ColliderCount` exactly on all 8 chapters (`BL-070`); it deliberately does
not model `ClutterBuilder` — clutter's decoration subtrees are parentless and never reached by this
walk, so their colliders (a wholly separate `BodyAddShape` mechanism) never counted here either.
`Add` skips a world-build root outright when gamez `flags.active` is false (default true when
absent) — the build script's own `NodeSetActive` record; `BuildNode` (`--node=`) deliberately does
not check it, since the caller named the subtree explicitly.
`RankConflicts` runs `ConflictRank` over the same walk before building and hands the map to the
scene builder — the `active`/skip/nearest-LOD filters are repeated there deliberately, so the graph
covers exactly the geometry `Add` will build; `WrapsOrigin` is `IsParkedAtOrigin`'s geometry half
taken from the gamez meshes, before there is a built tree to measure.
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
bush card in the install is `lighting: false`, so clutter does not dim with the mission SUNLIGHT),
plus a UV-clamp variant from `SceneBuilder.UvsWithinUnitSquare` over the kind's own card UVs.
⚠ Sprites are NOT collidable — no tree-destruction anim exists in the install (`spruce_destroy*`
  is the Spruce Goose; docs/formats/clutter.md). Solid decorations ARE collidable.
⚠ Collision shapes are SHARED, never expanded per placement (that costs seconds of BVH build): one
  `ConcavePolygonShape3D` per distinct mesh, `BodyAddShape`d at each placement onto per-region
  `clutter_bld_<cx>_<cz>` bodies. They live on the body RID — a `ShapeOwner*` call would run
  `_update_shapes()` and clear them; `SharedShapeMeta` on the clutter root anchors them against the GC.
⚠ Sprites drop basis + local Y at placement; an upright render is NOT proof the basis is consumed
  (authored bases ≈ identity) — the evidence is docs/formats/clutter.md.
⚠ `FindTemplateRoot(GameZ, name)` is public and SHARED: the fog-volume cloud field
  (`Effects/FogVolumeClutter`, driven by a different reader) resolves `cloudsprite*` by the same
  parentless-Object3d rule. A null return is retail-data-normal — C2B registers three templates
  its gamez lacks, and C1B/C2/C3's `fogvol.zrd` names a `cloudsprite` no chapter carries.
⚠ **`PlaceOnMesh` matches a template to a polygon by TEXTURE NAME ONLY — it never reads
  `GameZPolygon.Subface`.** In C5, `cblock1/2/3`'s subface polygons sit directly on top of
  `cblock4/5/6`'s base polygons (88.5–100% footprint overlap, `analysis/item9-depth-bias/CBLOCK-LOD.md`),
  so stamping both doubled the clutter buildings — settled 2026-08-07 by excluding the always-buried
  `cblock4/5/6` via `ClutterBuilder.BuriedClutterDistricts` (CAP-22 established the original draws
  the `cblock1/2/3` city; closing commit: `git log --grep=BL-250`). The subface depth-bias fix
  (`SceneBuilder.SubfaceBias`) only resolves which ground TEXTURE wins the z-fight; it has no effect
  on this file, which walks the same gamez tree independently.

## src/Mech3/Zrdr.cs
Zrdr extraction reader (zip or unpacked dir): `LoadFile`, content-sniffing `LoadMatchingFiles`,
name-predicate `LoadFilesNamed` (for families with nothing to sniff, e.g. the `ne0*` nets),
and `ZrdrDict`, the key/[values…] view over a reader's alternating list.
⚠ `LoadFile` accepts both entry namings — v0.6.1 writes `X.json`, the fork writes `X.zrd.json` —
  in both the zip and directory branches.
⚠ `ZrdrDict` COLLAPSES duplicate keys; anim definitions repeat keys meaningfully, so AnimDefs
  walks the raw lists instead.
⚠ It also DROPS an unkeyed value in the root list. `FogVolumeSpec.Parse` walks by hand for that
  reason — three chapters' `fogvol.zrd` carries its clutter block with no key in front of it.

## src/Mech3/AiNets.cs
The chapter patrol-net reader (`docs/formats/ai-nets.md`): every `ne0NNNNN.zrd.json` in a chapter
zrdr scope joined with its `neindex.zrd.json` name — nodes, the explicit edge list, raw per-node
tags, and the trailer attach target. First consumer: `UI/AiNetsOverlay.cs`; M4's net-following
(B5/F17) is the intended second. Golden counts asserted in `CSVM.Tests/AiNetsTests.cs`.
⚠ A net is a GRAPH: only `Edges` is connectivity — node order is not a route, loops are one
  authoring choice. Tags and the trailer are exposed raw, never interpreted (undecoded).
⚠ The `neindex` first element is NOT the pair count (C1: 46 over 29 pairs) — parse pairs to the
  list's end.

## src/Mech3/FogVolumes.cs
The chapter's `fogvol.zrd` (`FogVolumeSpec.Load`/`Parse`) plus `VolumesOf`, the gamez census of
`fvol*` volumes — the two halves of the authored ambient cloud field, rendered by
`Effects/FogVolumeClutter`. Schema, per-chapter values and the decoded/inferred split:
docs/formats/fogvol.md. Both halves are static over a `GameZ`/reader list, so the pair is testable
off-engine (`CSVM.Tests/FogVolumeTests.cs` pins all eight chapters).
⚠ A `FogVolumeBox` carries BOTH its bounds and its authored face planes, and `Contains` is a
  half-space test over the latter. That is EXACT rather than a convex approximation because every
  one of the 65 shipped volumes is convex; a future non-convex one would silently be filled to its
  hull, so the shape census in the tests is the tripwire. Only 38 of the 65 are boxes — C1C's
  twelve build-ups are rotated tapering frusta, C5's fifteen non-box strips polygonal prisms.
⚠ Faces are oriented outward by the VERTEX CENTROID, which lies inside any convex body — not by
  polygon winding, which the gamez does not guarantee. Coplanar duplicates are merged: C1C's
  `fvol9` carries the twelve build-up footprints as subfaces cut into its own top face, 55
  polygons over 5 distinct planes.
⚠ The census shares `WorldBuilder.IsFogVolumeNode` with the world walk's skip rule, so the set
  excluded from the render and the set filled with clutter cannot drift apart.
⚠ **Neither half alone says whether a chapter has clouds.** C1B/C2/C3 ship a reader file naming a
  `cloudsprite` template no gamez carries, AND no `fvol*` node, AND no `clutter` key. The parser
  deliberately reads their keyless block anyway so the empty result is a proven lookup failure.
⚠ `fog_zone` (and C5's `fog_color`/`*_fade_dist`) are read and reported, consumed by nothing. It is
  NOT the sky/fog zone selector — `docs/HISTORY.md`'s "no chapter has a `fog_zone` key" is a wrong
  negative (five do), but the values do not name a weather zone either; see fogvol.md. `BL-277`'s
  geometry rule stands.
⚠ A volume is NOT the `CLOUD_COVER` band: only C1's floor coincides, and C1C/C4/C5 all disagree.

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
The `unknown_seq` destruction slot parses into `AnimDefinition.DeathSlot`, deliberately OFF
`Sequences` (bootstrap and the sequence-walking derivations never see it); only
`AnimRuntime.RunDeathSequence` dispatches it (`BL-276`, docs/formats/destructibles.md).
⚠ Payloads stay a generic `AnimData` bag, not per-kind DTOs — a new event kind costs this file nothing.
⚠ Degrade, never fail the build: missing archive → null (normal); corrupt archive → log + skip.
⚠ Keep `Parse`'s spline handling: `spline_interp: false` coefficients are garbage that can be
  FINITE — `SiCubic.Eval`'s non-finite guard cannot catch it; degenerate cubics are synthesised
  at parse (so `At(dt)` stays branch-free), and non-finite/zero quaternions are rejected there.

## src/Mech3/AnimDefs.cs
The zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into CompiledAnim's
`AnimDefinition` model (op key SNAKE_CASE→PascalCase IS the compiled tag; unclaimed bodies stay
under `raw`). Exists because compiled archives are incomplete: `zepstate`/`startanims` are reader-only.
Unit normalization happens HERE so handlers see one convention: reader rotations are DEGREES
(ROTATE_STATE, FROM_TO rotate, XYZ_ROTATION → radians), PLAYER_RANGE metres (→ m²), ANIMATION_LOD
tokens (→ numbers) — see docs/formats/anim-definitions.md.
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
shows; acts on `NodeSetActive`/`DeleteTree`/`Object3DSetScroll`/`Object3DTranslate`/`Object3DRotate`,
counts + reports every other verb.
⚠ Read docs/formats/interp.md before extending: order matters and last write wins, a `FindNode`
  matching nothing is NORMAL (never warn), and `DeleteTree` names its own target.
⚠ `Object3DRotate`'s angle unit is ambiguous **per script**, not globally (BL-249, with
  `Object3DTranslate`): `RotateAsRadians` decides once per script by magnitude — any component
  over 2π marks that script's rotations as degrees — because C1/M05 and C3/MP1/MP2 disagree with
  each other, not just with a single global guess. No mission this project defaults to (an IA1)
  exercises either verb, so this is unreachable from the goldens; verified by targeted `--freecam`
  captures instead (see docs/formats/interp.md).
⚠ The entity half applies as AnimRuntime bootstrap pass 0, before animation state (engine load
  order) — translate/rotate reuse `AnimRuntime.PoseTranslate`/`PoseRotate` rather than writing the
  transform directly, so the first later touch of the same node (`RestOf`) records the
  mission-placed pose as rest, not the pre-placement corner.

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
`Bind`s + adds the returned node) — the world runtime stays a plain inline `new AnimRuntime`. Every
construction site hands over a **sealed `TemplateStage`** as the constructor argument
(`AnimRuntime.NewTemplateStage(pooled, shown, placesCalled, debugMotions)` is the one place the
Godot adapter hooks are spelled); the parameterless ctor takes an inert all-off one, which is what
the ambient world and the plain testing runtimes get. The
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
`Targets` reads the event payloads and resolves through the resolver's symbol authority
(`SymbolClaims`); a claimed-but-unbuilt index falls back to a strictly anchor-scoped name match
(never global) — how a re-anchored exploder template (`genx12`) binds its meshless `pt*` parameter
nodes onto the call-site wreck's same-named pieces (D31). Everything else about resolution —
anchoring, twin narrowing, root lift, the bind census and the three-tier scope order (`BL-219`:
staged templates reuse node names, `fly_trail1`-`5` is `he_trails` AND `ap_trails` AND
`carnage_trails`) — is resolver-owned (`NameResolver` — read its entry): this class resolves only
through its `Resolve`/`ResolveScoped`/`Anchors` forwards, hands its
`NameResolveFallback`/`SuppressRootLift`/`ReportResolution` flags over at `Bind`, and wires the
pool's per-slot root list as the resolver's `ownRootsOf` hook (`TemplateStage.RootsFor` — the
slot arithmetic lives on the stage, PLAN-template-stage A2, and the resolver still sees only a
resolved root list).
Puffer emitters live in `Anim/EmitterDirector.cs` (`Emitters`) — read its entry before touching
anything emitter-shaped. This class keeps only the dispatch case, the `at_node` sentinel resolution
and the `active_state` read, then forwards; `DefScopedPufferKeys` is the one role flag it still
carries, read once when the director is built. `OBJECT_ACTIVE_STATE … false` forwards to
`Emitters.EndOn` with `sparingSameInstant: !instant` — a PLAYED deactivation spares an emitter
started in its own instant (`BL-229`, the rule and its census live on the director), a RESET_STATE
one does not. Effect templates are **pooled** on the effects runtime (`TemplateStage.Pooled`,
`BL-225`): the stage holds
`WorldEffectsFactory.EffectPoolSlots` copies of each template, one per slot container carrying
`PoolSlotMeta`, and each `PlayEffectAt` takes the next slot (`TemplateStage.TakeNextSlot`, cursor
per template ROOT name, not per anim name — two defs on one root must not both be handed slot 0).
The slot arithmetic, placement, copy-identity questions and the three template policy flags live in
`Anim/TemplateStage.cs`
(PLAN-template-stage A2–A4 — read its entry, which carries the Decision-16 reinterpretation): this
class supplies the engine and runtime hooks and calls through; it reads exactly one of the flags
itself (`Places`, in the `CALL_ANIMATION` arm's relocation test) and owns none of them. Everything
template-shaped is slot-scoped through `TemplateStage.RootsFor(def, node)`: which copy is placed
(`PlaceAt`/`PlaceOn`), revealed (`Reveal`), tested for a move
(`IsAt`) and searched for the def's own names (the resolver's own-root tier, which `RootsFor`
feeds as its `ownRootsOf` hook) — all off the slot the CALL's anchor sits in, so a nested
CALL_ANIMATION stays inside its caller's copy instead of driving all four `fly_trail*` sets. The
cursor wraps: past the pool a call recycles a still-live slot, which is the
old shared-template collapse, counted in `PoolRecycles` (a forward of `TemplateStage.Recycles`)
and named once per effect.
The pool is NOT a third keying scheme (the rule and its ⚠ live on `EmitterDirector`); distinct
emitters per call fall out of the host node being a different node per slot, and the director is
handed already-resolved host and anchor nodes — the property PLAN-deepening Decision 16 pinned,
preserved by construction across the stage extraction (see `TemplateStage.cs`'s entry).
⚠ The stage's `PlaceOn` write (`AnimRuntime.PlaceNodeAt`) sets `TopLevel = true` on a placed root before writing its `GlobalTransform`
  (`BL-288`, one half of the fix — the pool paragraph below is the other) — a staged root stays parented
  where it was built (the per-player crash rig's roots sit under the controller subtree,
  `WorldEffectsFactory.BuildFlightCrashRuntime`), so without `TopLevel` a still-flying caller drags
  the whole template, and every child `OBJECT_MOTION` computed in ITS parent frame (`MotionRuntime`),
  along on every later frame instead of leaving it at the call site. The plane-parented-effect trap's
  family (`BL-229`'s puffer `TopLevel`, the smoke-trail fix, `trail-world-anchor`) — harmless on a
  stationary call site (buildings, the world-effects pool) and on the crash's own frozen templates;
  only a still-moving caller ever exposed it. Still correct and still needed; keep it.
⚠ `TopLevel` alone still leaves a placed root's BASIS inherited from its parent chain at the moment
  it is read — fine for a stationary pool container (identity), but the per-player crash rig's roots
  sit under `crashRoot`, whose own `Transform` is deliberately the plane's attitude at the crash
  (`WorldEffectsFactory.BuildFlightCrashRuntime`'s own note) — the fourth bite of the
  plane-parented-effect trap: a template that is supposed to lie flat on the struck surface (the
  water splash's spray column/rings, the dirt burst's dust plane) instead sprayed off at the plane's
  impact angle (`BL-292`). `TemplateStage.PlaceOn`'s `level` parameter — driven by
  `LevelPlacedTemplateNames`, a named allowlist keyed by `AnimName ?? Name` — overwrites the placed
  root's basis to `Basis.Identity` for exactly those defs. **Named, not blanket**: an earlier,
  whole-runtime version of this flag also releveled `call_crash_trails`' flying debris chunks
  (`fly_trail1-5`), which author their xz/y launch spread in the TEMPLATE's own local frame on
  purpose — leveling it stripped the co-rotation that made debris continue along the crash's own
  attitude/momentum (reinforced separately by `InheritedWorldVelocity`) and sent it off on a fixed
  world heading unrelated to the impact instead (caught visually on the `c1-crash` golden's later
  frames, past the fireball). `EffectCatalogue.CrashSurfaceLevelAnimNames` is the one list; see its
  own comment for exactly which defs are in and why the rest (fireball/smoke/debris,
  `large_steam_spray`) stay out.
The crash rig pools its templates too (`BL-288`), with a twist the world pool does not need:
its calls anchor on the PLANE's own nodes (each `pdpanelN` tear CALLs `gimmeflakes` onto its own
`pdpN`; the crash defs CALL `large_firetrail` onto their four `pieceN`), which sit in no slot
container — so slot choice cannot be read off the anchor's ancestry. `TemplateStage.AssignCallerSlot`
(invoked from the CALL dispatch, before the placed-where test) pins each (template root, call
anchor) pair to its own slot on the anchor's first call, sticky for the session: a re-tear
restarts ITS OWN copy, and `RootsFor` consults the claim whenever the ancestry walk
comes back empty. Sizes are the AUTHORED distinct-anchor counts per root
(`effect_pools.json`'s crash section — not TUNE; re-count only if the shared damage-stage defs
change), and more anchors than copies wrap by `RootsFor`'s modulo, counted in
`PoolRecycles`. Before this, the root lookup returned the same single node for every caller,
and a second panel's `CALL_ANIMATION` found `IsLive(target, startAnchor)` false (keyed on the
FIRST anchor), teleported the shared root to the new site and restarted it from rest pose —
discarding the first burst mid-flight, leaving the stale instance under the old anchor alive, and
letting `MotionSet`'s per-`(Target,Channel)` eviction pick the winner: the user-visible
"panels fly away repeatedly, and from the wrong site". The whole per-panel family shared the
mechanism (`planeflakes`/`planeflakes2`, `short_firetrail`, `large_firetrail`,
`small_injure_fireball`, `flame_ball_02`, `yellow_spark_02`) and is pooled by the same fix; the
`damage-template-pool` suite holds the regression shape (two panel defs, one template, two
copies). `ForCrashRig` bakes none of the template flags itself — `BuildFlightCrashRuntime` builds
the stage `Pooled`+`Places` beside the slot containers it builds and hands it in, mirroring the
world side; `Shown` stays off (crash templates hide by their own reset states).
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
The staged-hidden reveal/retire/sweep ritual is `TemplateStage.Shown`'s
(`Reveal`/`RetireWhenIdle`/`Sweep`, PLAN-template-stage A3 — read its
entry for the holds and what each measured): both entry points drive it through the one module,
`PlayEffectAt` and the relocating **CALL_ANIMATION** arm alike (`BL-061`,
`analysis/bl-061-template-mesh/`), and `Advance`'s retire walk hands finished instances to
`RetireWhenIdle` while the frame's `Sweep` drains the deferrals. This class keeps only the
motion-domain half as the stage's supplied `stillAnimated` predicate (`TemplateStillAnimated` —
asked of the ROOT, and an endless spin excluded, the `MotionSet.OwesBounce` narrowness).
`WAIT_FOR_COMPLETION`'s host half is `InstallWait`, off the `CallAnimation` case: it closes over the
`(target, startAnchor)` pairs the call resolved — collected as the loop runs, whether or not the
live guard let it Start, since "wait until that animation completes" is about the animation, not
about which call started it — and arms nothing when none of them is live (a callee finished inside
its own t=0 burst is never an instance). `Dispatch` clears `_pendingWait` on entry, so a flagged
call that arms nothing cannot be answered by the previous one's test. `WaitCeilingS` (120 s) is a
BACKSTOP, not a model: "completes" is OUR instance lifetime, and a callee held open by something
the data cannot predict would wedge a caller silently — indistinguishable from the behaviour before
this landed. All four outcomes are named as they happen (armed / abandoned at the ceiling / routed
to the effects runtime / nothing live to hold) rather than through `Count`, whose report is the
bootstrap census and could never carry a death-time event (LOG-16). The routed case is the wait's
one scope boundary: an `ExternalEffect` call leaves no instance HERE, and the delegate carries no
return path to poll the effects runtime's.
⚠ `_rng` is the runtime's ONE die (`RANDOM_WEIGHT`, `SOUND_GROUPS` picks, crash-debris scatter) —
  every session sets `Seed` (`Rng.Anim`/`Rng.Crash`/`Rng.Effects`); route new dice through it or a
  replay stops being identical. `Reseed()` also clears the sound groups' recency memory, which
  lives outside the RNG.
⚠ The bind degrades silently two ways on a partial world — unanchored (`Anchors() == []`) vs
  target-missing (`_opsUnresolved`) — both leave a still object; the `ReportResolution` bind
  census is the only tell (C1 `--node=hk_zep`: 50 anchored, 763 unanchored, 134 target-missing).
⚠ The resolver's `MaxRootLift` 16-match cap assumes WHOLE-WORLD node counts — a partial `--node=`
  build drops under the cap and anchors phantom defs, so it must set `SuppressRootLift` (measured
  on C1's `ap_radiotwr`: 95 lifted defs / 91 phantom instances vs 1 / 2 with the lift refused).
⚠ **Do not re-propose splitting the modes into three interfaces** — decided **no** by
  `PLAN-deepening` `G18` (design-it-twice, 2026-08-03; `G19` closed `❌` with it). The 65-public-
  declaration surface survives D/E unshrunk, but a per-caller census showed the width is NOT mode
  coupling: production callers already hold narrow slices (effects callers 11 members, crash 6,
  the combat plumbing 4 — the last mostly through `DamageSink`/`EffectSink` delegates that narrow
  harder than any interface), and the rest is one construction site's init-knob block
  (`WorldSession.cs:201`) plus the labs/probes/suites, which observe the implementation on purpose
  and would be blinded by any honest interface. Two independent designs under opposite constraints
  (three minimal role interfaces vs. an immutable `AnimRole` record + consumer facets) BOTH
  declined the three-way split on that usage evidence; every shipped bug in this family
  (`BL-224`/`232`/`233`/`235`/`236`/`242`) was a selector/wiring error no mode interface catches.

## src/Mech3/Anim/
`AnimRuntime`'s private nested types promoted to top-level `internal` types in their own
namespace, purely for file size — not an independently-owned subsystem, still driven entirely by
`AnimRuntime`. `IAnimMotion` (`ScriptPlayback`/`SpinMotion`/`FromToMotion`/`OpacityFade`/
`MotionRuntime`), `AnimLight`, and the bind-census `AnchorKind` enum. `SpinMotion.ComposeSpin` is
the one member reached from outside this namespace without going through `AnimRuntime` at all —
`Flight/PropAnimator.cs` calls it directly so a plane's own props spin through the identical
accumulate-from-rest decode instead of a second hand conversion; it takes a rest `Basis` and a
rate, no `AnimRuntime`/`MotionSet` state, so the reach-in is inert to everything else here.
`MotionSet`, `EmitterDirector`,
`NameResolver` and `TemplateStage` share the namespace but ARE independently owned — their own
entries below.
`MotionRuntime`'s `translation_range` is a SPHERICAL launch — `xz` azimuth, `y` elevation, both in
degrees, `initial` the speed (`analysis/object-motion-range/`, decoded 2026-08-01) — and a launch
seeds from the node's authored rest pose, since a shared effect template's children are re-homed by
nothing between calls. `RangeLaunchDirection` is that decode's ONE expression; `ProjectilePool`'s
gun-casing ejection reads the same `gunshell` event through it (INSTR-3). Its `scale` channel is an
OFFSET from unit scale (`1 + initial + delta·u`), unlike the absolute `PoseScale`/`OBJECT_SCALE_STATE`
— 30 of the 45 distinct SCALE events carry a bare `-0.1`, which absolute is a negative scale.
`MotionRuntime`'s flight solve is admitted by an **absent `RUN_TIME` plus an apex**, never by the
`BOUNCE_SEQUENCE` — `BL-257`'s census (`analysis/bl-257-nulled-launch/`) found 167 events / 119
distinct defs naming NEITHER field, which end instead with the flying piece's own null-start
`ACTIVE_STATE 0`; gated on the bounce they all reported duration 0 and were hidden on the tick they
launched. `PendingBounce` still arms only where a bounce IS named, so the second shape flies and
owes nothing.
⚠ Do NOT re-narrow that gate to the bounce, and do not widen it past the apex. `FlightToLaunchHeight`
  returning 0 for `v0y <= 0` is what keeps `BL-245`'s falls out — including 8 of those 167 (a level
  `bridge_truck01`, `rope1burn`'s five rope ends, two `fuelbox` rockerarms whose speed range is
  −45…45, so `sin(elevation)·speed` inverts). A `chuteman` parabola solve divides by zero.
⚠ `RestOf`, `_rng`, `SetSubtreeOpacity` and `NonSingularScale` on `AnimRuntime` are `internal`
  (not `private`) specifically so these motion types can reach them — same-assembly only, no wider
  exposure intended; don't widen further without a reason. `NameOf`/`VisualOriginOf` joined them for
  `EmitterDirector` (log lines and the WORLD-15 emission point), which is what a reason looks like:
  `VisualOriginOf` is shared with `AnimRuntime`'s own `ExternalEffect` siting, so it could not move.
⚠ Every pose-scale write goes through `AnimRuntime.NonSingularScale` (`FromToMotion.Seek`,
  `PoseScale`): the data ends scale channels at exact 0 ("shrink away" — 217 FROM_TOs + 216
  SCALE_STATEs install-wide), and an un-clamped singular basis makes the physics server's
  `affine_inverse` spam native `det == 0` for every StaticBody3D under the node (BL-007).
⚠ `FromToMotion` reads NO `*_delta` channel and must not start: all 51 compiled ones are the
  sibling absolute channel's rate, `(to − from) / run_time` (0 mismatches, worst residual 4e-6,
  `analysis/bl-050-fromto-delta/`), so composing one doubles the motion.

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

## src/Mech3/Anim/EmitterDirector.cs
One runtime's PUFFER_STATE emitters as a module: `Assert` (start / revive / re-home), the four stops,
`Reset` (the crash rig's respawn — `Clear` + `Destroy`, not a `SustainEnd`, since respawn is
immediate), the per-frame `Tick` follow, and `Census`. `AnimRuntime` keeps only the dispatch case,
the `at_node` sentinel resolution and the `active_state` read. One director per runtime (a shared one
would make `Reset` a filtered delete over a discriminator — the selector error this family has
shipped five times, re-created at session scope); `IEmitterFactory` is what builds, so `Puffer`,
`TextureArchive` and the parent node are all behind the seam and a suite can install a fake.
Emission sits at the host's mesh-bounds centre only when its node origin lies outside them
(absolute-modelled subtrees, WORLD-15 — zero offset, byte-identical, otherwise).
`Census` spans the KNOWN emitters, not the active ones — active-only cannot tell a paused-revivable
entry from a forgotten one, which is the `EndFor`/`Discard` distinction itself; `--debug-anim`'s
active line and the bootstrap emitter line are both projections of it, never parallel re-derivations.
`EndOn` (the host-subtree stop) spares an emitter that started in the SAME instant — one
`Advance` pass, the granularity every zero-`START_TIME` run of events fires in (`BL-229`). Emitters
are stamped in `Assert` off a counter bumped once per `Tick`. Censused install-wide
(`analysis/bl-229-emitter-host-deactivation/`): of 414 activate/emit/deactivate pairs, the 32
same-instant ones are 4 shapes — the splash family, whose callee authors a 0.6 s run this stop
erased — and the other 382 sit a median 3.5 s out, smallest gap 1 ms. Not a time threshold: there is
no number between the populations. The RESET_STATE path passes `sparingSameInstant: false`, being
base state where the last write wins.
⚠ The four stops do NOT collapse into one parameterised call. They vary on two independent axes —
  SELECTOR (key / host subtree / owning instance) × DISPOSITION (pause-revivable / pause-and-forget)
  — proven independent by `EndFor` and `Discard` sharing a selector and differing only in
  disposition. Every shipped bug here (`BL-224`, `BL-233`, `BL-236`, `BL-242`) was a SELECTOR error.
⚠ Emitters key `(name, host[, def])` and BOTH cases are measured. Def-scoped **required** on the
  effects runtime — the two damage-stage sputters both declare `black_smoke` and masked each other.
  **Forbidden** on the world runtime — C5's six same-node `m_crane_go` spark defs stacked six
  emitters and moved the `c5-city-night` golden (re-measured 2026-08-03: forcing def-scope
  everywhere moves `c5-city-night` AND `c3-island`). The effect-template pool is not a third scheme;
  do not add slot to a key.
⚠ `SpentEmitterFactory`'s warn stays a warn. A null object that silently swallows **is** `BL-234` —
  a world with no fire, dust or smoke reading as a clean log — and the bootstrap census cannot cover
  it, printing before the first death can reach a `PUFFER_STATE` (LOG-16).
A `PUFFER_STATE 1` re-assert REVIVES a `SustainEnd`ed emitter (the `puffit` sputter loop cycles 0/1
forever), and the re-asserting instance takes ownership. `EndOn` is NOT expressible as an
`IsVisibleInTree` gate the way `TickLights` is: the effects stage keeps template roots hidden while
their world-space particles show.

## src/Mech3/Anim/NameResolver.cs
Name→node resolution as one public module, generic over the node type (`NameResolver<TNode>`,
PLAN-name-resolver): the index (`Add(node, srcName, parent, gamezIndex?, indexByPointer)`),
the wildcard `Matcher` (`*` any run, `#` a digit run including zero, case-insensitive, the `.flt`
suffix double match), the memoized `FindAll`, the **scoped tier chain**
(`Resolve`/`ResolveScoped`), the **symbol authority** (`SymbolClaims` over the by-index map `Add`
builds; `NarrowToSymbolRoot` — the `air_gen`/`eairg31` cross-bind fix, tri-state: null means
undecidable and leaves the name match standing, never conflate it with an empty narrowing),
**`Anchors`** (NAME match → symbol narrowing → root lift, with
`NameResolveFallback`/`SuppressRootLift`/`MaxRootLift` as its documented policy inputs — the
runtime copies its flags over at `Bind`), and the **bind census** (`OpenCensus`/`CloseCensus`
bracket the bootstrap; `--debug-anim`/`--node=` project `ResolutionLines` through
`AnimRuntime.ResolutionLines`, never re-derive it). The by-index map obeys two refusals inside
`Add`: empty under `NameResolveFallback` (colliding index spaces) and skipped for
`indexByPointer:false` rows (a pooled copy shares its source's indices — `IndexPooledCopy`). Node
identity is constructor-supplied (`IEqualityComparer<TNode>` — the engine keys on
`GetInstanceId()`, `AnimRuntime.Node3DIdentity`), never the node type's inherited `Equals`.
`AnimRuntime`'s `Resolve`/`ResolveScoped`/`FindAll`/`Anchors` are one-line forwards; the
engine-free instantiation over a plain token type (plus plain `AnimDefinition`s) is `CSVM.Tests`'
suite.
⚠ **The three-tier scope order is structural: `ResolvePath` is private to the module.**
  `ResolveScoped` runs call-anchor subtree → the def's OWN template roots (the constructor's
  `ownRootsOf(def, anchor)` hook — `TemplateStage.RootsFor`, so the pool reaches the
  resolver only as a resolved root list and the slot arithmetic stays out) → global unless
  `LOCAL_NODES_ONLY`; a null/dead anchor (the `isLive` predicate) drops the scoped tiers for the
  plain whole-index walk. The order once diverged caller-side (the emitter host and the motion
  targets took different routes to the same name, so an authored stop never reached its emitter)
  — keep the primitives unreachable.
⚠ **No node an `Add` has already indexed may be reparented afterwards.** Ancestry is a snapshot
  (each row's `parent`, walked once at index time), not a live tree query — `FindAll`'s scope
  filter, `NarrowToSymbolRoot`'s containment test and the root lift's parent step all read it and
  silently misread it if a node moves. A subtree staged after the bootstrap (an effect template, a
  pooled copy) may still `Add` more rows, but the caller must also call `ClearFindCache()`.
⚠ `Anchors` is the ONE census-recording resolution call (once per def identity — the bootstrap asks
  on several passes). `FindAll` and the private census-free paths (`ComputeAnchors`, `ResolvePath`,
  the own-root tier) record nothing; the `ownRootsOf` hook and per-event callers
  (`TemplateStage.RootsFor`) must keep resolving through those, never through `Anchors` or a second
  recording path.
`FindAll`'s memoized result must be treated as read-only — the same list instance is returned on
every repeat query (a single-name `ResolveScoped` hands it back directly), which is what makes it
cheap for C5's ~400 live poll loops re-dispatching every frame. **This is not the declined G18
split** — G18 ("⚠ Do not re-propose splitting the modes into three interfaces", the `AnimRuntime`
entry) refused per-mode *observation* interfaces; this plan extracted one concept all three modes
share, unchanged — one resolver for all of them. No re-open.

## src/Mech3/Anim/TemplateStage.cs
The effect-template stage as one module (`TemplateStage<TNode>`, PLAN-template-stage A2–A4): pool-slot
arithmetic (`SlotOf` memoized over a raw-walk hook, `TakeNextSlot`'s per-root cursor, `RootsFor`'s
slot scoping with the modulo fallback for callees staged shallower than their caller's slot), the
BL-288 caller-slot claim (`AssignCallerSlot` — sticky per (root, anchor), wraps through the same
modulo), template placement (`PlaceAt`/`PlaceOn`, the BL-292 `level` basis reset), the
copy-identity questions (`IsAt` on the ONE named move tolerance — 0.25 m², the two coincident
0.25 f literals merged per Decision 6 —, `RootsOf`, `SharedWithLiveInstance`), the pooled-copy
staging entry (`IndexPooledCopy` over supplied runtime hooks), `Recycles`, which counts BOTH
wrap flavours, and the reveal/retire/sweep ritual both entry points drive (`Reveal` — the one
visibility write, gated on `Shown`, scheduling its own hide for a def whose t=0 events already
finished it; `RetireWhenIdle`, deferring on the two measured holds, the supplied `stillAnimated`
predicate asked of the ROOT and `SharedWithLiveInstance`; `Sweep`, draining the deferrals once a
frame). It also carries the **three template policy flags as sealed constructor state** — `Pooled`,
`Shown` and `Places` (was `AnimRuntime.PooledTemplates`/`ShowPlacedTemplates`/`PlaceCalledTemplates`),
get-only, no setter anywhere. Generic like `NameResolver<TNode>`: ~8 engine hooks at construction
(identity, slot walk,
transform read, the `TopLevel`+`GlobalTransform` placement write, the visibility write,
print/debug) plus those three flags, the runtime-dependent hooks late-bound via `Wire` at the handover (`findAll`,
`anchors`, `isLive`, live instances, `LevelsTemplate`, `stillAnimated`, `NameOf`, the index/reset
services) — they cannot be construction arguments, because the factory that builds the stage
exists before any resolver does, and the resolver's own `ownRootsOf` hook is this class's
`RootsFor` (both directions are delegates). The off-engine charter is
`CSVM.Tests/TemplateStageTests.cs` (slot wrap, modulo fallback, recycle counting both flavours,
caller-slot stickiness, placement, the tolerance, the hide-deferral holds and the sweep drain);
`effect-template-mesh`/`effects-census`/`damage-template-pool` stay the in-engine integration
tier — nothing is asserted in both.
⚠ **Decision 16, reinterpreted — not reopened, and not silently overridden.** Two prior texts pin
  these members to `AnimRuntime`: PLAN-deepening Decision 16, and PLAN-name-resolver's milestone
  goal (*"`SlotOf` / `TemplateRootsFor` / `NextPooledAnchors` / `PlaceTemplateAt` stay where
  Decision 16 of PLAN-deepening pinned them"*), echoed by the `AnimRuntime` entry's own "the
  director is handed already-resolved host and anchor nodes". PLAN-template-stage Decision 1
  reads the pins' letter as blocking a move into `EmitterDirector` and their spirit as the
  property that the pool never becomes a third keying scheme. That property survives this peer
  module by construction: the stage resolves roots and hands them out; nothing here keys an
  emitter, and `EmitterDirector` still receives resolved host and anchor nodes.
⚠ Root resolution goes through the `findAll` hook with one deliberate asymmetry: per-EVENT paths
  (`RootsFor`, `IsAt`, the resolver's own-root tier) run on poll loops and must never re-enter
  `NameResolver.Anchors`' census; per-CALL paths (`TakeNextSlot`, `RootsOf`) may use the
  `anchors` hook — once per call, census recorded once per def identity. Exactly the pre-move
  behaviour; do not "clean up" the asymmetry in either direction.
⚠ `SlotOf`'s memo assumes slot containers are built before `Bind` and never reparented — the same
  snapshot terms as the resolver's `FindAll` cache.
**The sealing leak is closed structurally, not documented (A4, Decision 4).** Until A4 the factory
wrote `ShowPlacedTemplates`/`PooledTemplates` onto the runtime AFTER `ForEffects` returned — an
accepted shallow spot on `AnimRuntime`'s entry that worked only because both happened to be read
after `Bind`. The stage is now built sealed by whoever knows the runtime's role and handed in as a
constructor argument (`WorldEffectsFactory` for the world-effects and crash rigs, `WorldSession` for
the world one via `Options.PlacesCalledTemplates`, the suites for their own); `AnimRuntime`'s
parameterless ctor takes an inert all-off stage, and `AnimRuntime.NewTemplateStage` is the one place
the Godot adapter hooks are spelled. There is no post-seal write to misorder, and the census that
proves it is that no name outside this file reads or writes the three flags — `AnimRuntime` reads
`Places` at exactly one site (the `CALL_ANIMATION` relocation test) and nothing else. Do not
re-introduce a settable mirror on the runtime "for the labs": the anim lab's need is a construction
option (`WorldSession.Options.PlacesCalledTemplates`), and reaching it after `Bind` is the exact
shape that was leaking.

## src/Mech3/SequenceRunner.cs
The engine-free sequence interpreter, extracted from `AnimRuntime` behind the `ISequenceHost` seam
(four members since `BL-228` added `PendingWait`; the other three are unchanged).
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
⚠ **A called sequence's first event fires one tick LATE, and that is a known, measured, deliberate
  non-fix** (`BL-135`): `CallSequence` appends past the descending walk's cursor. A bounded
  same-pass drain was built and measured install-wide on 2026-08-04 and NOT kept — it repairs
  nothing observable (7 of 8 chapter captures pixel-identical, every runtime total unchanged; only
  the bootstrap censuses move, which is LOG-2) yet moves 4 of the 13 goldens, `c1-crash` by 79.7 %
  of its pixels. Before re-implementing it read `analysis/bl-135-callsequence-lag/FINDINGS.md`: it
  carries the implementation, the cap sized from the data (deepest authored same-tick fan-out 15,
  so 64) and the one def that makes a bound mandatory (`marypickford` rings, instantaneously,
  because `OBJECT_MOTION_SI_SCRIPT_ALL_NAMES` has no handler and reports duration 0).
⚠ `OnEventDispatched` is a get-only nullable delegate on the seam ON PURPOSE — the null-conditional
  at the fire site short-circuits the `EventDispatch` construction when no debugger is attached, the
  documented zero-cost contract on the hot dispatch path. Making it a method breaks that.
`WAIT_FOR_COMPLETION` (`BL-228`) is the seam's fourth member, `PendingWait` — a `Func<bool>?` the
host arms during `Dispatch` and the runner reads back once, then polls each advance until it reads
false. A PREDICATE, not a duration, because the callee's length is not knowable at the call (its
sequences call further sequences; an SI script's run time lives in another archive); a CLOSURE
rather than a re-ask by name, because the host must test the instances THIS call reached and
re-resolving would take another template pool slot.
⚠ **The hold gates the sequence's NEXT event — never the runner's lifetime, and that is the DATA's
  rule, not a shortcut.** Censused over both front-ends (`analysis/wait-for-completion/`,
  `callee_shapes.py`): of the 2,999 flagged calls the runtime can reach, 2,770 are the last event of
  their block and every one of those names a callee that never terminates (the `sputter_*` /
  `gen_drop_ladder` `LOOP{-1}` idiom). Not ONE flagged call with an event behind it names a
  non-terminating callee, in either front-end. So the `_pc < Events.Count` test at the install site
  is load-bearing: read as a lifetime hold, those 2,770 wedge open for the session. Only 165 calls
  can shift any timing at all. On release the runner re-bases `_base` to the release instant and
  sets `_iterScheduledTime` — the hold replaces the call's duration, so a trailing `Event + t`
  offset measures from completion, and an enclosing LOOP must not also charge the iteration the
  AnimFrame floor.

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
Schema: docs/formats/loadouts.md. Verify/inspect with `--dump-loadout` (add `--weapon-lab` to bind
the full-rig loadout below instead of the stock one). `StockLoadouts.Load`'s missing-file warning
logs through `Log` (`Utils`), not `GD.PushWarning` (`engine-free-suites` A2).
⚠ A missing marker is a LOUD throw naming plane/slot/marker — never a silent skip (a silent one
  fires a gun from nowhere). Markers resolve by `cs_name` meta from the built tree, like MarkerOverlay.
⚠ Gun ammo is per group (Balmoral's two .50s carry 2000 each); rocket ammo is per pylon
  (`CLUSTER_SIZE` each, total = pylons × that) — A9. Config lives at `res://`, NOT under `--data-root`.
⚠ `Load` reads a `res://` path through `Godot.FileAccess` (the pck copy in an exported build —
  `GlobalizePath` + System.IO cannot see inside the pck, B11 2026-08-05) and an explicit disk path
  through System.IO (unit tests run without a Godot runtime; a native call there crashes the host).
⚠ Hardpoints bind via `PylonFillOrder = {1,5,2,6,3,7,4,8}`, never `pylon1..pylonN` sequentially
  (`BL-294`, PT-31) — a partial stock fit alternates wings, so `Hardpoint.Index` (the resolved pylon
  NUMBER) is not the loop position. `GaugeCluster`/`FlightController.UpdateWeaponGauges` key the
  hardpoint gauge's belt lights and arrow target off that Index against the dial's fixed 8-slot
  ring, not off position in the (possibly reordered) `Hardpoints` list.

`Loadout.ForRig(plane, WeaponDefs, LoadoutDef?)` (M3 B4) synthesizes a lab loadout covering the
airframe's **whole** rig rather than only what stock names: the 4 gun-group slots the reverse-index
rule seats (`docs/formats/markers.md` "Slot → firepoint binding" — W1→fp(9−2n),(10−2n)), each
populated with whichever of its firepoint pair the rig actually has (the Kestrel's W1 resolves to
the lone centreline `firepoint7`), plus one hardpoint per `pylonN` present — then runs the
synthesized `LoadoutDef` through the same `Bind`, so there is still exactly one bind path. A slot
stock does name keeps its weapon/mount/caliber; one it doesn't defaults to the stock's first gun
weapon (`wep_30` if the plane has no stock guns at all) under a generic mount label ("Gun Group N").
⚠ Every synthesized group is `IsTurret = false`, even a slot stock marks a turret — deliberately
  making all four fireable in the lab; stock's inert-turret behaviour is untouched.
⚠ Never assumes 8 firepoints/pylons — it discovers the rig same as `Bind`'s own marker walk and
  only synthesizes a slot when at least one of its pair actually exists.

## src/Flight/WeaponBench.cs
The world-less "do all 48 weapons mount and fire without throwing" pass check (M3 D9) behind
`--weapon-test` and the `weapons-fire` in-engine suite: one static
`Run(plane, Loadout, WeaponDefs, ProjectilePool)` over a **parked** plane that spawns straight into
the caller's pool and returns the report plus the counts a suite asserts on (`Total`/`Ok`/`Errors`/
`Skipped` + `GunMounts`/`PylonMounts`). Needs no world, no colliders and no frame — `Spawn` does the
muzzle math and the pool insert synchronously. Hand it `Loadout.ForRig`'s loadout (both callers do)
and every weapon fires from **every** mount of its class: measured on the Bloodhawk, 4 gun groups ×
2 muzzles for each of the 31 guns and 8 pylons for each of the 17 hardpoint weapons — 48/48, 0
errors, 0 skipped.
⚠ It lives here, NOT on `WeaponLab`: the lab is a flight-mode panel that fires nothing of its own
  (M3 B5) and neither caller constructs one any more. Do not fold this back in — a firing loop on
  the panel is exactly what B5 removed.
⚠ There is deliberately **no cross-bank fallback** — a gun with no gun group SKIPs rather than
  borrowing a pylon. `Skipped` is the success-looking outcome (a weapon that never fires and nothing
  notices), so both callers assert it at 0, and `GunMounts` is pinned at ForRig's 4 so shrinking the
  coverage cannot hide behind an unchanged 48/48.

## src/Flight/FireControl.cs
The fire-control state machine (BL-295), a plain engine-free class: trigger edges (first shot on
the press tick), per-group `FIRE_RATE` accumulators + muzzle rotation, ammo draw-down, both weapon
selectors with their on-empty auto-advance, the rocket pull/cooldown gate and the two once-only dry
cues. `Step(dt, FireInputs)` takes raw HELD booleans — every edge is detected inside — and returns
decisions in one reused `FireOutcome` (spawn commands as (group, muzzle)/pylon indices, the
declarative gun-loop state, the cues); `FlightController.ApplyFireOutcome` performs them against
muzzle transforms, `ProjectilePool` and `FlightAudio`. Ammo mutates through the node-free
`IGunSlot`/`IPylonSlot` views (`GunGroup`/`Hardpoint` implement them), so `Loadout` stays the single
store the gauges read and a decision can never diverge from the counters mid-tick. `Refill` re-arms
everything but deliberately keeps the gun pick; the pylon cursor doubles as the firing cursor, so it
resets to pylon 0.
⚠ **The interface is the test surface**: proven in `FireControlTests` (xUnit); the in-engine
`weapons-fire` suite is only the mounting census — never re-add rate/edge/cue assertions there.
⚠ `AutoFireRockets`/`InfiniteAmmo` are runtime-mutable properties, NOT frozen config: the weapon
lab flips them live on `FlightController`'s public fields (and `GameSession` sets `InfiniteAmmo`
post-`_Ready`), so the adapter mirrors both into the machine every `Step`. `--fire` (guns) is just
a forced held input, OR'd in by the adapter.

## src/Flight/WeaponCursor.cs
`FireControl`'s internal ammo-slot index math (an `internal` class — nothing else may call it):
`NextArmed` is the firing cursor — the selected slot while it has rounds, else the next armed slot
forward-wrapping (`-1` when all empty); `NextSelectable` is where the manual G/H step lands — the
next armed slot strictly after the cursor, skipping empties. Each slot is its own position
regardless of ordnance/weapon type, so H cycles even a uniform loadout. Stateless; proven through
`FireControl`'s interface (`FireControlTests`), not its own.

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

## src/Flight/CamParams.cs
One aircraft's camera tuning out of `camparam.json` ([formats/camparam.md](formats/camparam.md)):
the `default` block, then the plane's own block layered on top. Seven of the eleven airframes carry
one; the other four take the 13.0 default. Mirrors `PlaneStats.Load`'s shape (same
`Load(zrdrPath, planeNodeName)`, same nearest-wins resolution), and like it is loaded once per
distinct plane and cached by `GameSession`. `FromData` is false when the file was absent — the
built-in fallbacks are the shipped `default` block verbatim, so a partial extraction still flies and
the session log distinguishes "the data says 13" from "we guessed 13".
⚠ The file is keyed by DISPLAY name ("Bloodhawk"), so the lookup goes through
  `MarkerRig.PlayerAirframes`. `PlaneRoster.PlaneDisplayName` strips a leading `p` and title-cases,
  which yields "Fbrand" for `player_fbrand` — it would silently drop the Firebrand's override
  rather than fail. `CamParamsTests` pins that case.
⚠ `Dist` and `DistFactor` drive the chase radius (`d = Dist + DistFactor·V`, CAP-21-decoded,
  BL-248); everything else is parsed and deliberately DORMANT. `dist_min`/`dist_max` is NOT a
  clamp — in the `default` block `dist_min` (15.7) exceeds `dist` (13.0) and CAP-21 never reaches
  `dist_max` — and the catch-up triplet's units are still undecoded (the one measured rate,
  0.65 /sim-s, matches none of the three). See the format page before wiring more.

## src/Flight/CameraController.cs
The flown aircraft's camera, split out of `FlightController`: the roll-following chase camera, the
numpad fixed views (`Views`, `ActiveView`, `FixedView`, `LogView`), the look-behind view
(`BackView`, numpad 0 / `--view=back`, at the chase radius bounded into the authored
`back_dist_min/max`), the authored crash camera (`CrashView`, a hard cut to a static elevated
vantage `crash_horiz` behind / `crash_y` above the impact, held until respawn — framing decoded
off the original's crash footage; `crash_elev`/`crash_chord_y` stay capture-gated on `BL-260`,
as do the death and flyby cameras) and the free orbit used while the debug freeze holds the
world. Steers a `Camera3D` it does not own, as `UI/OrbitCamera` does for the static viewer. The chase RADIUS is dynamic per plane (BL-248): `d = Dist + DistFactor·V` (both
authored) plus a first-order acceleration transient relaxing at the MEASURED 0.65 /sim-s
(`UpdateDynamics`, host-called once per sim step); the offset's DIRECTION (behind and above at
~15.7° elevation) is not in the data and stays hand-picked. Collaborators: `FlightController`
(the only host) and `CamParams`.
⚠ Deliberately passive — no clock, no input devices. The host passes the dt, because which clock a
  camera runs on is behaviour: `Chase` takes the SIM clock's dt so a scripted capture is frame-rate
  independent, `Orbit` takes WALL dt because the point of the freeze is to fly around a stopped
  world. The orbit's axes arrive pre-mixed and `ActiveView` takes a `Func<Key,bool>` that already
  folds in `UseKeyboard`, so pad devices and window focus stay out of here.
⚠ The chase camera slerps its BASIS, never a re-derived LookAt — that is what lets inverted flight
  render upside down. On the realtime clock the DRAWN pose is `_renderPose`, interpolated between
  the last two sim poses (DET-10), and anything bolted to the plane (the rigid numpad views) must
  read it, never the raw sim pose. `Chase` smooths the plane→camera OFFSET, never the world
  position — a world-position follower trails by V/rate at speed, which CAP-21's speed-flat
  apparent size rules out (BL-248).
⚠ The fixed views share the chase radius, so a distance change moves BOTH cameras — including the
  dynamic terms, by design (they are one number; moving one desyncs them) — and it is why the four
  golden shots with a flown plane moved when the per-plane distances landed (and again when the
  dynamics did), while the eight `--freecam` shots and `viewer-bhawk` did not. `BL-150`'s rebuild
  of the view LAYOUT is still open.

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
  (the lab's own pick readout agrees: three classes, not four). A weapon's `quicksand` IMPACT entry
  is still real data the reader parses correctly; `Resolve` is just never called with it. `B5`'s
  suite runs the 48 weapons across `{Default, Water, Buildings}` only — a fourth case would be
  invented coverage.

## src/Flight/Projectile.cs
`ProjectilePool` — the shared-world weapon-fire subsystem: a fixed pool of projectiles integrated
with `Ballistics` (VELOCITY/ACCELERATION/GRAVITY, expiring at RANGE), plus tracer streaks,
muzzle flashes, and the per-surface IMPACT sound + effect model. Per-class impact looks (A2): a
water hit instances the authored splash model and plays its def's own curves for the 2 s run
(`AdvanceSplash` — base disc 1→2→1.8 xz, column popped to ×100 Y collapsing to 0, the splash1/bsplsh
zrd values verbatim) on the shared world materials, which honour the models' authored
`lighting/fog: false` themselves since BL-214 — the hand-rolled `OverrideUnlit` this needed is
gone, verified pixel-identical on a C1B night water burst. C6 (`BL-265`) adds the def's other two
pieces: the 0.05 s opacity fade-in / 1 s fade-out (`OBJECT_OPACITY_FROM_TO` targets base AND column
together) through a per-instance translucent twin (`EnsureSplashFade`, `SceneBuilder.FadeShaderFor`
— never edits the shared cached material, since concurrent splashes share it) driven via
`SetInstanceShaderParameter`, and the column's `splash01→03` flipbook (`EnsureSplashFlipbook`,
reusing `TextureCycler`'s frame-swap machinery — registered manually because the gun splash's own
polygon binds to a non-cycling sibling material, confirmed against C1B's gamez data, so
`SceneBuilder`'s automatic per-polygon path never reaches it). Column *width* plays at 8×
(`SplashColumnWidthScale`; judged at the controls 2026-08-06 with the fades in — the authored quad
is 5 cm wide, sub-pixel past ~30 m, while the reference ticks measure ~0.35 m, which 8× matches;
the authored 1× stays reachable, `static readonly` not `const`, so the branch stays compiled). A dirt
(unclassified-terrain) hit spawns tumbling chips (`SpawnDirtDebris`) drawn on the gunhit def's own
`bit01–04` chip textures in alpha-blended per-texture pools (through the additive
muzzle-flash-textured impact pool they read as a small flame — the BL-203 mechanism), launched
outward and arcing via `Sprite.Vel`/`SpinAxis`/`SpinRate`, zero and inert for every other sprite.
⚠ **RETRACTED 2026-08-07 — this effect is scheduled for deletion (`BL-313`).** It rested on the
inference *"the def's bit1–3 gamez nodes carry no geometry, so the textures ARE the chips."* The
original draws no such chips: `OriginalScreenshots/Videos/70 DD Dirt.mp4` shows a 70-slug dirt hit
producing only the `chunk` quad and one faint black `blacksmokepuffer` puff. Read literally, the
zero-vertex `bit1`–`bit3` nodes draw nothing and only `chunk` (one 4-vertex quad) does. The
inference's sole motive — why ship `bit01`–`bit04` if nothing draws them — is answered by a
different consumer, `zep_skin_fire3`. **Do not generalise "zero-vertex node ⇒ draw its texture as a
sprite" anywhere else.**
A gun hit on a
buildings-classed surface spawns a ricochet spark burst + flash (`SpawnRicochet`, additive — a
judged stand-in: the authored `bld_damage.flt`/`rcochet1` are 2 of the 5 install-missing names). Each burst sprite carries its own orientation basis (`Sprite.Orient`): the muzzle flash
rolls in the firing plane's basis, the impact spark/explosion/debris faces the struck surface
normal (then tumbles, for debris) — a fixed world plane for none of them (BL-139); tracers stay
velocity-aligned, and none of the three is billboarded. A gun shot's muzzle flash (`BL-263`) is
the triad — three `_muzzle1` quads 120° apart sharing one continuous per-shot roll — **anchored
to the firing muzzle node** (`Sprite.Anchor`: Pos/Orient stored local, resolved to world per
frame in `RenderSprites`; a freed anchor skips the draw), so the flash rides the plane instead of
being flown through at speed. The per-ammo axis is `MuzzleAmmoIndex` (`{slug,dum,ap,mag}_muzzle1`
from the weapon's `FIRE` binding); the roll draws `Rng.Weapons`, so `--det` stays reproducible.
⚠ The def's pick-one reading (one `mb_spinflame` quad at a discrete 30/80/140° roll + the
`_muzzle1`→`_muzzle2` flip) was implemented and REJECTED at the controls (2026-08-05) — it does
not reproduce the reference stills, and whether the original engine renders one branch or all
three is not recoverable from the data. Do not re-land it without new footage evidence. Each quad is
centred by default (`Sprite.Pos`), except the muzzle flash, which sets `Sprite.AnchorLeft` so
`RenderSprites` derives the quad centre from `Pos + Orient.X * (currentSize/2)` — the texture's left
edge (QuadMesh's default UV, U=0 at local X=-0.5) stays pinned at the muzzle as the quad shrinks over
its life, instead of the texture being buried under a centred blob (C21). `AddMultiMesh`
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
registered aircraft but its shooter's, and reports the pass distance (`WarningShotCue`).
`RegisterAircraft` is the hittability half: the hit ray runs world+aircraft with each round
excluding its own shooter's registered `AircraftBody` by RID; a struck plane classifies `Player`
and routes to `FlightController.TakeProjectileHit` (struck shape → part, armor-first damage),
carrying the round's `Shooter` id through so a kill is attributable — never the destructible
pipeline. A missing round may still fuse (B14): the `DETONATION_DISTANCE` proximity fuse arms
against AIRCRAFT ONLY — never world geometry (BL-233: a world fuse detonated every rocket short
of its aimed surface and, with a null collider, locked every hardpoint weapon out of its
per-surface IMPACT entry) — checked AFTER the ray so a dead-on rocket keeps its direct hit, and
detonating at the round's CLOSEST APPROACH to the registered boxes within the swept step
(`AircraftBody.SegmentDistance`), holding while still closing at step end: most rockets author
`DETONATION_DISTANCE` equal to `IMPACT_PROXIMITY`, so a first-entry fuse would always detonate
exactly where the linear blast reaches zero. The fused-on body rides into `Impact` for
per-surface effect/sound selection, but its damage arrives only through `BlastAircraftPass`: every
registered plane inside the blast radius — never the shooter's own, never a wreck — takes the
weapon's ARMOR/HEALTH scaled by the linear falloff, measured to the nearest point of its OWN
boxes (`NearestShape`, the blast-neighbor-shape rule) and struck at that box, so part mapping and
kill attribution run the direct-hit path. Planes never enter `DamageSink`; the destructible blast
sphere stays world-masked.
⚠ `_ray` is a shared mutable query object: per-shot `Exclude` is set AND reset around every
  query — a leaked exclusion silently shields the next round's target.
`DamageSink` (→ `AnimRuntime.DamageAt`) turns a world hit into destructible damage;
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
`AnimProgram` (ctor `flyoutAnims`), one pooled/reused `Puffer.Emit`/`Stop` set per live round
(PLAN-puffer-interface A3),
plus the sonic's authored 8.73 rad/s body roll (weapon-effects.md). Gun shots add the
`muzzle_burst` secondaries (C22): a pooled per-shot `gunshell` casing instance flying the def's
OBJECT_MOTION verbatim (per-shot nodes on purpose — a shared anchor under `CallAnimation`'s
already-live gate drops gun-rate ejections), the authored `muzzlepuffer` smoke on a gravity-free
sprite pool — 6 puffs the instant-spawn gloss of the def's 0.05 s interval over its 0.3 s window
(A2, `BL-261`; the invented white eject-puff cluster the smoke had been misread as is deleted) —
and a pooled `OmniLight3D` flash from the def's `3rdperson_lts` range/colour variants, its
magnitudes TUNE. `PlaySound`
(FIRE's launch bark, played once per `Spawn`; IMPACT's per-surface hit) resolves a `SOUND_GROUPS`
name (e.g. the incendiary rocket's `ground_mixed_exp_sg` default impact) through `_soundGroups`
first, same as `WorldSounds.PlayOneShot` — `FIRE.SOUND` is null for every cannon in the data
(`LOOPED_SOUND_NAME` covers continuous gunfire instead), so the one-shot never doubles up (BL-211).
Positive-`HEALTH_DAMAGE` blasts linearly fall from full at direct contact to zero at the authored
`IMPACT_PROXIMITY`; `DAMAGE 0` effect radii never damage. The struck body always takes full damage
(`ApplyDamage`'s direct-hit branch, unscaled by falloff); every OTHER body the blast sphere overlaps
is scored from the nearest point on **its own collision shape**, not its transform origin
(`NearestBlastPoint`, BL-239) — a `GetRestInfo` query against the same sphere with every other
candidate body excluded, so a large neighbour (a zeppelin gasbag, a long building mesh) is scored by
how close the blast actually is to its skin, not by how far the blast is from wherever its origin
happens to sit. Falls back to the shape owner's transform origin only if that query somehow finds no
contact. The fuse tests the whole swept segment per candidate plane (no tunnelling at ~20 m/step)
and gates through `DETONATION_DOT_PRODUCT` toward the nearest hull point. The linear curve and
1 N·s/HP impulse are TUNE.
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
records (name, max HP, max armor, `critical`/`engine` flags, `got_hit_anim`, per-part
`injure_anims`), and the def-level `VehicleInjureAnims`. Schema: docs/formats/vehicle.md.
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
independent run so the archives parse once per session. Logs through `Log` (`Utils`), not
`GD.Print`/`GD.PushWarning` (`engine-free-suites` A2) — the class itself has no other engine
dependency, which is what lets `stunt-gates` move off-engine in A4.
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
⚠ `StuntMission` (its `Racer.Mission`) logs through `Log` since the engine-free-suites A2/A4
  conversion (zero direct `GD.*` sites), and `CSVM.Tests` installs a process-wide no-op
  `Log.ConsoleSink` before any test runs (`TestHostLogSink.cs`, BL-302) — so calling `Load` from a
  test is safe now (`StuntGatesTests` calls it). `StuntRaceTests` still builds a `StuntMission`
  via reflection on the private constructor and fires `RunCompleted` directly — it has no public
  constructor, and a real `Complete()` does more than the finish-ordering check wants.

## src/Flight/StuntRaceBoard.cs
The race's shared ranked results overlay: same clean-Godot-UI construction as StuntScoreboard,
but covering the WHOLE window — on its own CanvasLayer (Layer 10, above SplitScreen's 0) under
the session root, one row per player from `StuntRace.Standings()` (placing, tag, plane, zones,
total + gap to the winner; DNF when unfinished). Wakes on `RaceCompleted`, hides in `_Process`
once `AllFinished` clears; the footer's exit hint follows how the session was launched.
⚠ Scales on raw window height / 720, NOT HudMetrics — pane damping would shrink a full-window
  overlay for no reason.

## src/Flight/VersusMatch.cs
Dogfight deathmatch bookkeeping (M4-A1 front-load `PLAN-vs-mode.md` B12): `RegisterKill(shooter,
victim)` scores the shooter and tallies the victim's death, `RegisterDeath(victim)` tallies a death
alone (terrain/mid-air — no killer, no score change); `Advance(dt)` is the host-fed match clock;
`MatchCompleted` fires once on kill threshold or time-out (leader wins, equal top kills draw);
`Standings()` ranks by kills descending with ties sharing a rank; `Restart()` (rematch) zeroes every
score and re-arms completion. Off-engine coverage: `CSVM.Tests/VersusMatchTests.cs` (threshold win,
time-out win, draw, post-completion no-op, rematch re-arm, each limit disabled on its own).
⚠ Zero engine dependency at all — no `GD.*`, no `Godot.` type, no `Node`, no logging (unlike
  `StuntRace`, not even through `Log`): a `GD.Print` in this family once crashed the xUnit host with
  an unmanaged `AccessViolationException`, so this class stays engine-free by construction rather
  than by a later fix.
⚠ Kill target 0 and/or time limit 0 each disable that end condition independently; both 0 is a
  valid, deliberate untimed-unlimited match that never completes on its own.
⚠ Deliberately not a Node — freed with the session, host-fed exactly like `StuntRace`'s `Tick`/
  `Advance` timekeeping.

## src/Flight/VersusHud.cs
Per-pane Dogfight HUD (`PLAN-vs-mode.md` C23/C24): a compact status line — remaining time
(omitted once `VersusMatch.TimeLimit` is disabled), this pane's own K/D, and the current leader's
tag — drawn in MarkerHud's run-status slot (`RefStatusY` — Stunt and Versus are mutually
exclusive, so the two never compete for it); a transient "P2 DOWNED P3" kill banner ("P3 DOWN"
with no killer); and one opponent marker per living rig (`Rigs`, excluding `PlayerIndex` and any
`Controller.Crashed` seat) — an on-screen tag at the projected point, or MarkerHud's edge-arrow +
clock-hour bearing (`EdgePoint`/`ClockHour`, copied verbatim) when off screen/behind, in that
opponent's own `SplitScreen.PlayerColor`. `Build(match, playerIndex, camera)` binds the match +
this pane's own camera (opponent markers project through it, exactly like MarkerHud's zone);
`Rigs` is attached once by `FlightRigAssembler` (the SAME live list `GameSession` keeps, not a
snapshot — every seat exists before this pane assembles, only `.Controller` fills in as siblings
do); `PlanePos`/`HeadingDeg` are fed every frame by `FlightController`, same site as `Marker`'s.
The status line pulls the match live every `_Draw` (no pose to project for it) and `OnKill` is
pushed once per `Downed` report by GameSession's own broadcast — a second subscription, never
piggybacked on the scoring one, so every pane hears every kill/death, not just the two it
happened to.
⚠ `LeaderText` reads "LEADER —" whenever more than one player shares rank 1, including the 0-0
  tie before the first kill — accurate, not a placeholder: nobody leads yet.
⚠ Opponent positions come off `PlayerRig.Controller.GlobalPosition` directly, never
  `AnimRuntime.PlayerPosition` (a P1-only singleton) — the same rule MarkerHud/FlightController
  already follow.

## src/Flight/VersusBoard.cs
The dogfight's shared results overlay (`PLAN-vs-mode.md` C25) — `StuntRaceBoard`'s construction
almost verbatim: winner (their own `SplitScreen.PlayerColor`, or "DRAW" on a tie) on top, then one
ranked row per player (tag, kills, deaths) from `VersusMatch.Standings()`, covering the WHOLE
window on its own CanvasLayer (Layer 10, above SplitScreen's 0) — the match ends for everybody at
once, unlike a per-pane HUD element. Wakes on `MatchCompleted`, hides in `_Process` once
`Completed` clears (a rematch); `Populate` runs ONLY from `OnMatchCompleted`, so the drawn rows
stay the ones the match actually ended with even after `Restart()` zeroes the live state —
`StuntRaceBoard`'s `FinishTime`-snapshot discipline, achieved here for free since `VersusStanding`
is a value-type snapshot already. Live flying continues underneath (nothing scores post-match); R
routes through `GameSession.RestartMatch` (mirrors `RestartRace`: `VersusMatch.Restart()` then
every rig respawns), owned only while the board is visible via `FlightController.Match is
{ Completed: true }` — checked before the crash branch, same R-ownership shape `Race`/
`RestartRace` already use.
⚠ Scales on raw window height / 720, NOT HudMetrics — same reason `StuntRaceBoard` does: pane
  damping would shrink a full-window overlay for no reason.

## src/Flight/Weather.cs
`WeatherState`: per-mission atmosphere from the flown mission's own weather.json — per-zone fog
(`FOG_COLOR`/`FOG_RANGES`/`FOG_ALTITUDE`), `SUNLIGHT_*` → `ZoneFog.WorldLight` (`SunIncidence`
0.46 / `MinWorldLight` 0.15, TUNE), the `CLOUD_COVER` whiteout band (`WhiteoutAmount`
trapezoid), `WIND`, and precipitation → `PrecipData`. Schema + colours + zone names: weather.md.
⚠ `CLOUD_COVER`/`WIND`/precip keys pair with BARE scalars — `ZrdrDict.FromAlternating` cannot
  read them; walked raw (`StringAfter`/`ScalarAfter`/…). Per-zone blocks are list-valued (dict).
⚠ `CloudBandCentre` is the band midpoint and is deliberately ONE spelling for two consumers: the
  opaque whiteout core is centred on it (`WhiteoutAmount`) and the cloud deck's ceiling→floor
  regime flips on it (`Session/WeatherRig.DeckRegime`, `A7`). That coincidence is exactly what
  hides the flip — do not give either consumer its own midpoint.
⚠ `ZoneKeys` collects `ZONE<digits>` keys from the raw list in FILE ORDER (a Dictionary loses
  order; `ResolveZone`'s fallback is the file's FIRST zone); `SW_ZONE*` twins are excluded.
⚠ The default stays `zone2` (user decision) — which zone a mission flies is in no file
  (negative result in weather.md), so a fallback is the only correct behaviour.
`PreferPopulatedHorizonZone` is the second, geometry-driven correction (`BL-277`): a request whose
`horizon/zone*` subtree carries NO meshes yields to the one zone that does, which is what makes
C1B/C2/C3 render a sky at all. The `ResolveZone(requested, horizonZones)` overload runs both in
order and takes the geometry pick ONLY if this mission's weather.json also defines fog for it, so
sky and fog are always the same zone. Census + per-chapter table: weather.md; the census itself is
`WorldBuilder.HorizonZonesOf`; coverage `CSVM.Tests/SkyZoneTests.cs`.
⚠ **The rule must never fall back to the horizon's FIRST zone.** C5's weather.json lists `ZONE1`
  first while its horizon lists `zone3` first, so that would pair one zone's sky with another's
  fog. A request the horizon does not name at all is left to `ResolveZone`'s weather-file fallback,
  which is what already lands C5 on `zone1`.
⚠ It fires on a UNIQUE populated sibling only. Two buildable zones (C1, C1C, C2B, C4) means the
  geometry cannot decide and the request stands — that is `BL-100`, still open, not a gap here.

## src/Flight/FlightAudio.cs
Own-plane non-positional loops (engine with throttle-driven pitch, overspeed whine, rattle,
damaged-engine blend keyed to `Update`'s `damageFrac` via `PlaneStats.DamagedEngineGain`) +
one-shots: `StartEngine`/`EngineStartRamp` prop-start fade (re-fired via the loop-restart hook
in `Update`; `EngineStartRamp` 2.0 s is sourced from startprops' authored prop cross-fade duration,
not a bare literal), `OnCrash` → `snd_exp_plane1..4`, `OnGroundExplosion`/`OnWaterExplosion` layering
the crash variant's boom (`snd_exp_ground_a` off the dirt def itself, `snd_exp_water_a` from the
`plane_big_splash` inside the sea dive — the crash runtime renders effects, never sound),
`OnEngineStop` → `snd_propstop`, called right after the explosion one-shots on every crash/destruction
(`FlightController.Crash`) so the loops end on the authored wind-down cue instead of a cut,
`OnGraze(water)` → the survivable scrape's authored `snd_exp_water_b`/`snd_exp_ground_b`
(touchdown.zrd; rate-limited by `FlightController`, not here). `OnWarningShot` draws one
`bullet_warning_sg` variant per near miss (player.json `warning_shot_sound` is a SOUND_GROUPS name, so
`Setup` takes the group table too; rate-limited by `FlightController`'s `WarningShotCue`, same split).
The engine loop (`BL-078`) is two voices of the same clip, pitch-split ±half of `EngineDetuneRatio`
(0.05 TUNE, `flightAudio.engineDetuneRatio`) around `EnginePitch.Eval`; each voice is held at the
fixed equal-power `EngineVoiceGain` (1/√2, not a TUNE) so the pair sums to the old single loop's
loudness.
⚠ `WhineMixGain` 0.12 (TUNE), `Config`-wired (`flightAudio.whineMixGain`): don't raise it back —
  reader "volume" is not a linear mix gain (the original's whine sits 12–18 dB below the raw curve
  cap); re-derive from a new reference. `DamagedEngineMixGain` 1.0 is the same shape
  (`flightAudio.damagedEngineMixGain`) with no reference recording yet to derive a value from.
⚠ The visual start/stop choreography (`startprops`/`stopprops`, the static-blade-prop cross-fade +
  startup smokepuff burst) is a SEPARATE per-plane `AnimRuntime` play (`FlightController`
  `Respawn`/`Crash`), not this class — `StartEngine`/`OnEngineStop` are audio-only; keep both halves
  in step by hand, there is no shared trigger.
⚠ `MixGain` (1/√N in splitscreen, TUNE) covers the four loops and the per-player cues
  (`PlayEmptyClip`, `OnGraze`); the crash/prop one-shots are deliberately left unscaled.
⚠ `BL-268`: `MakeLoop`/`MakeOneShot` and the gun-loop/warning-shot one-offs used to multiply
  `def.Volume` by a blanket, uncommented-beyond-"Temporary fix" ×0.2 before any of the named
  mix gains above were applied. Audited against every other own-ship/world reader of the same
  `sounds.json` VOLUME field — `WorldSounds.Create`/`PlayOneShot` and `OnCrash`'s plane-explosion
  pick, which never carried it — and found no reference-level basis for the factor, so it is
  removed rather than promoted to a named constant: authored VOLUME now plays unscaled, with
  `MixGain`/`WhineMixGain`/`DamagedEngineMixGain` as the only deliberate attenuations. This
  raises the own-ship mix roughly 5×; `BL-285`'s owed engine start/stop listen is re-based on it.

## src/Effects/Puffer.cs
The original engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks
(schema: docs/formats/effects.md). `PufferState.Load` finds the fully-defined state in an
effects reader; `Puffer.Create` builds the atlas and hands it to an `IEmitterRenderer`
(`EmitterRenderer.cs`) — the class itself owns only the CPU integration, so `CreateWith` builds any
mode with no atlas, no `TextureArchive` and no GPU. The continuous surface is ONE pair
(PLAN-puffer-interface A2): `Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)` / `Stop()`,
plus the one-shot `Burst` and the hard-kill `Clear` — the authored state picks the mode, callers
never do. `Emit` dispatches: DISTANCE_INTERVAL trails per interval of actual host motion (CAP-15's
density, `BL-259`; TrailPool-sized even on the sustained path, since the time-cadence pool floor
silently dropped ~85% of a flight-speed trail); a still host keeps the time cadence (the static
building sputters, whose distance can never elapse — every distance state carries the parsers'
synthetic 0.1 s TIME_INTERVAL, so that cadence always exists); a host that CANNOT move declares
`staticBurnMps` and spends virtual metres at the held pose instead (the damage lab's parked
plane). `Stop` ends the trail as well as the emission, unconditionally and idempotently, so a
revived emitter re-homes rather than drawing a puff line from its pooled slot's previous call
site (the rocket ghost trails). **All four external callers (`PufferEmitter`, `ProjectilePool`,
`DamageVisuals`, `ThrottleSlamSmoke`) drive the emitter through `Emit`/`Stop` only** — the six
mode verbs (`TrailAdvance`/`TrailEnd`/`TrailBurnAt`/`SustainAt`/`SustainEnd`) are `private`
(PLAN-puffer-interface A3); `DriveAt` was deleted (it had been a straight `Emit` alias with no
remaining caller once `PufferEmitter` moved to calling `Emit` directly).
⚠ `Emit`'s very first call on a DISTANCE_INTERVAL state that hasn't moved yet (a trail's homing
frame) also fires one `SustainAt` batch — a single 1-puff burst at the muzzle/exhaust, since a
distance state's synthetic TIME_INTERVAL cadence is always live underneath the distance dispatch.
`EmitterDirector`'s own callers always had this; migrating `ProjectilePool`'s rocket trails and
`ThrottleSlamSmoke`'s exhaust trail onto `Emit` gave them the same one-puff homing sputter they
didn't carry under raw `TrailAdvance` — judged negligible-to-desirable and confirmed at the
controls (rocket-volley capture: a continuous, non-ghosted trail; the `c1-flight` golden's
one-golden move is exactly this, its `--hold` throttle jump crossing `ThrottleSlamSmoke`'s slam
threshold on the capture's first frame).
`PufferState.FromAnimEvent` parses the compiled anim payloads.
Three config knobs scale `BaseSize` per spawn path — `puffer.burstSizeScale` /
`puffer.trailSizeScale` / `puffer.sustainSizeScale` (`SizeScaleDefault` **1**, the authored
SIZE_RANGE verbatim; the knobs remain for deliberate per-path tuning — the cull margin scales with
the largest). Two more scale the `fire_n_smoke` family ONLY — `puffer.fireRiseScale` /
`puffer.fireLifetimeScale` (defaults 2.5/1.5, invented against original footage rather than
decoded, signed off at the controls 2026-08-06): vertical spawn velocity + puff
lifetime of the crash/destruction/tank fires, identity for every other emitter. All read at
`Init`; registered in `Config.WarmTuningRegistry` for `--dump-config`. Moving
`SizeScaleDefault` re-pins every puffer-bearing golden and only those (measured on the 4->1 revert:
`c1-waterfall`, `c3-island`, `c5-city-night`, `c1-destroy-effects`, `c1-crash`; the other 8 carry no
live emitter).
⚠ TEXTURE_SEQUENCE times are FRACTIONS of a particle's lifetime, not seconds (effects.md).
⚠ `SpawnSustained`'s draw order (pos → vel → size → life) is shared determinism: reordering the
  `Rand` calls re-scatters EVERY sustained emitter — measured as five goldens moving with the
  fire tune inert in all of them.
⚠ The blend is derived, never authored: a COLORS ramp or a near-black dying sprite (measured off
  the atlas, `SmokeLuminance`) ⇒ blend_mix + no depth fade, else blend_add (effects.md).
  `Create`'s `blend`/`softParticles` force the verdict for a caller that knows better.
⚠ Each emitter's `_rng` is a per-instance stream off `Rng.Puffer`, so particle spread is pinned by
  the master seed: measured, the C1 waterfall mist moved 0.47% of a `--det` frame before and 0.00%
  after. Its seed depends on how many puffers were built before it — deterministic under `--det`,
  and pinned to WHERE `new Puffer()` sits in `Create`. Moving it, or constructing one anywhere on a
  capture path, re-pins all five puffer-bearing goldens (the `SizeScaleDefault` list above — an
  earlier "four" here was a stale count); `CreateWith` is a test entry point only.

## src/Effects/EmitterRenderer.cs
`Puffer`'s lower seam: `IEmitterRenderer` takes live particles (`Attach` sizes the pool, `Write`
per particle, `Show` publishes the frame) and `MultiMeshEmitterRenderer` draws them as ONE MultiMesh
of camera-billboarded quads, owning the shader — quad-rim fade, flipbook column from per-instance
custom data, and the soft-particle depth fade. Reached in a suite by `RecordingEmitterRenderer`.
⚠ The atlas is built ABOVE this seam, in `Puffer.Create`, and passed to the renderer's constructor.
  That is the whole point of the cut: below it, a `Puffer` would still need a `TextureArchive` and
  the three modes would stay unreachable (`PLAN-deepening` Decision 9, `E15b`).
⚠ The blend arrives resolved. `Create` derives additive-vs-mix from the COLORS ramp and the dying
  sprite's luminance; this type holds no state to re-derive it from, so there is no `Auto` here.

## src/Effects/FogVolumeClutter.cs
The ambient cloud field, ENTIRELY authored (`BL-273`): `fogvol.zrd`'s weighted clutter table
scattered through every `fvol*` volume the gamez carries, one alpha-blended MultiMesh per sprite
kind (two per chapter). Each volume is cut into `distance` × `distance` cells anchored on the world
origin and every cell gets ONE placement drawn uniformly inside it — `distance` is the field's
areal DENSITY (mean spacing), not a lattice phase.
Templates resolve through `ClutterBuilder.FindTemplateRoot` — the trees' own lookup. Per placement:
kind by weight, `perturb_dist_range` in the plane at a free bearing, Y = TOP-ANCHORED (drawn at the
volume's own top) for a volume no more than 1.5 card-heights thick, UNIFORM across the full volume
height otherwise, then `perp_dist_range` added after containment either way; size = the card's
authored extent × `scale_range`. Schema, per-chapter values and the decoded/inferred split:
docs/formats/fogvol.md. **This file holds no TUNE constant, except `TopAnchorHeightFactor`** (A3):
not authored data, a shape-classification threshold decided from the volumes' own thickness gap —
see the constant's own remarks and fogvol.md's vertical-spread entry. Otherwise it replaced
`CloudPuffs`, whose entire field (Count 12, Radius 620, SizeMin/Max, BaseAlpha, BandBelow/Above,
VertFull/Fade) was hand-tuned because this reader had not been found.
⚠ Built ONCE, world-anchored, no per-frame hook and NOT per rig — unlike the dome/deck/whiteout,
  nothing here follows a camera. Splitscreen shares one field; the far fade is evaluated per view
  inside the shader, which is what makes that correct.
⚠ Its GEOMETRY is shared but its VISIBILITY is per view (`A7`): `GameSession` moves these
  MultiMeshes off the default layer onto `UI.SplitScreen.CloudFieldLayer` (with the world's placed
  `cloudparent` clusters — one population to an altitude gate) and `Session/WeatherRig.Tick` adds
  or drops that bit in each camera's cull mask by that camera's own altitude against the
  `CLOUD_COVER` centre. Same principle as the far fade: one shared field, decided per view. Never
  hide it by node visibility — that would take it out of every pane at once.
⚠ Sampling Y AT the volume's top (rather than a random band near it) still respects a sloped or
  tapered top: `Contains` runs the exact face test (`FogVolumes.cs`), so an XZ drawn in a cell is
  rejected exactly when it falls outside the true top footprint at that height — no separate
  per-column top lookup needed. Skipping the uniform Y draw for a top-anchored cell also skips one
  RNG call, which re-aligns every later draw in the shared stream; this moves C1C's count by a few
  sprites past its own frustum-taper effect (9,569 → 9,572) even though the build-up volumes'
  own logic is untouched — expected under one shared seeded stream, not a second bug.
⚠ Determinism and world-anchoring are the SAME property here: one seeded `Rng.Clouds` stream drawn
  in a fixed volume/cell order, and nothing reads a camera, a pane count or a frame. There is no
  per-view cell, so there is nothing to hash — do not introduce a coordinate hash.
⚠ Placement is drawn against the volume's AUTHORED shape (`FogVolumeBox.Contains`), not its bounds;
  a draw outside it places nothing. Resampling until it lands inside instead would crowd the
  surplus inward and raise the local density above the authored spacing.
⚠ Containment is tested on the in-volume draw, BEFORE `perturb_dist_range`/`perp_dist_range` are
  added. A card may therefore hang outside its volume's wall, which is what a perturbation means —
  and it is what lets a vertical rule move the Y distribution without the containment test
  rejecting the whole field.
⚠ The shader collapses a sprite past `far_fade.y` to a degenerate quad (`cull` scales the
  billboard basis to 0). Without it the whole map's field would rasterize alpha-0 fragments over
  huge quads every frame; with it, a static MultiMesh needs no streaming at all.
⚠ Scatter runs once per VOLUME, not once per clutter block. Per block would double the authored
  density (the block `weight` × node weight is one two-level table); first-volume-wins would drop
  C1C's twelve stacked build-up volumes and render a flat deck.
⚠ The cards are authored `fog: false` and carry `far_fade_range` instead, so this shader does NOT
  fog them — the opposite of `CloudPuffs`, which applied the cylindrical fog term to its puffs.
⚠ `Rng.Clouds` is drawn in a fixed volume/cell order at build, so the field is a pure function of
  the master seed — which is what re-pinned seven goldens deterministically.

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
⚠ Vertical is Q/E plus the **Z/U** alternate — not C/Space: C toggles the collider overlay and
  Space fires guns, and because this camera POLLS raw key state, sharing either key moved the
  camera as a side effect of the other action (`BL-279` moved Space off; Z stays clear of C for
  the same reason).

## src/Flight/FlightModel.cs
Velocity-vector arcade flight model: body rates = control torque × reciprocal inertia vs
ang_momentum_damp, scaled per axis (PitchTune/YawTune/RollTune). Thrust/drag/gravity integrate on
the velocity vector (speed passes through zero); lift cancels gravity's cross-path share; drag is a
speed power law normalized so drag(fd_speed) = max thrust, PLUS an induced term in α (below).
`Alpha` (deg) is angle(nose, VelocityDir), read once per
step right after the nose vector — an emergent LAG from the nose-chase, not a modelled aerodynamic
state, and reported in every `--dump-flight` row's detail. Lift consumes it as the pull's stand-in:
`liftFrac = min(1, speedLift × |up·Y| × n(α))`, `n` ramping 1 g → `LiftLoadMax` over the authored
`liftAOAs [5,9]` read as degrees (a hypothesis, not a decode — `BL-095`). `|up·Y|` stays the carrier
and must NOT be flattened: it is what makes knife-edge depart, and the α ramp is what lets a hard
pull hold altitude through a steep bank without a bank term. The stall is TWO measured thresholds
over one margin
(`StallFraction` = speed/fd_speed): `isStalled()` is the nose-drop at 0.25 fd, `IsStallWarned()` the
STALL lamp at 0.30 — the lamp leads the break by 2.64 sim s (`BL-148`/`CAP-06`); never drive both
off one number, and never recompute the margin beside them.
⚠ ThrustConst and the three *Tune rates are NOT free TUNEs — they are pinned to the original,
  measured off cockpit-gauge video, and `--run-tests`' flight-envelope suite fails if they move.
  Retune by feel and you are overwriting a measurement (`analysis/video-flight-calibration/`).
⚠ MaxDiveSpeedFrac is a numerical backstop, not a terminal speed: terminal dive is EMERGENT from
  the drag curve and lands within 0.3% of the original, so a value that binds replaces a measured
  number with a guess. Keep it above every airframe's emergent terminal — worst is the Balmoral,
  1.678 in a 71° dive (`--dump-flight=player_balmoral`) and ~1.71 vertical.
⚠ Accepted artifacts, not bugs: loop energy pump, steep-climb equilibrium, stall hang. Induced
  drag is `A · C_i · sin²α` — `InducedDragCoef` is the one genuinely FITTED constant here and is
  NOT transferable: it absorbs our 1.71× turn-rate error, so closing that gap means refitting it.
  A hard altitude clamp (2003 m,
  `BL-094`/`CAP-03`) deletes climbing velocity at/above the cap rather than fading thrust/lift/drag
  toward it — traced to one mission (C1B IA1) only, not assumed global/per-chapter/per-aircraft.

## src/Flight/PropAnimator.cs
Spins the flying aircraft's prop/rotor blur discs: Build collects every node PropParts classifies
(rest pose + rate, radians/second local axes). Advance recomputes each disc's absolute pose from
its stored rest pose through `SpinMotion.ComposeSpin` — the same accumulate-from-rest decode
`AnimRuntime` plays the ambient world's own `XYZ_ROTATION` spins through — rather than stepping
`RotateObjectLocal`, so a long flight session cannot drift. FlightController drives it
throttle-scaled with a PropIdleSpin 0.4 floor (0 while crashed). --fly only — the static viewer
keeps the still disc.

## src/Flight/ThrottleSlamSmoke.cs
The throttle-slam exhaust smoke (`CAP-21` re-read): a large sudden throttle increase streams the
`nitro_boost` def's own `nitropuffN` puffers (AT_NODE `exhaust1..4`) from the plane's exhaust marker
nodes for a few sim-seconds, reused WITHOUT the rest of that def (no `nitropropN` discs, no
`snd_nitrostart`) — the capture shows the same trail-smoke shape on a plain throttle jump with no
boost. `Update(dt, throttle)` runs an edge-triggered gate: it tracks the throttle at the start of the
current unbroken climb and fires once per climb the instant the cumulative rise crosses
`SlamThreshold`, never again for that climb and never on a flat or falling throttle — so tapping one
notch at a time (each tap separated by a flat/falling frame) evaluates fresh every time. Drives the
puffers via `Puffer.Emit`/`Stop` (PLAN-puffer-interface A3; DISTANCE_INTERVAL, the same mechanism
`DamageVisuals` uses for the nose smoke trail) — the authored def is a distance-triggered trail,
not a time-interval burst. `Reset(throttle)` (crash/respawn) hard-stops any plume and re-anchors the
climb tracker so the throttle jump those moments make is never itself read as a slam.
⚠ `SlamThreshold` 0.25 (TUNE): the capture only bounds it between a firing idle→5/8 (0.625) and a
  silent single 1/8 (0.125) — 2/8-4/8 is unobserved. 0.25 is the smallest round two-notch jump
  consistent with both endpoints.
⚠ `DurationSimSeconds` (≈3.89 s = 2.8 wall-s × 1.390) is TUNE within the capture's "~2-3 wall-s"
  dissipation estimate, not a measured edge.

## src/Flight/ControlSurfaceAnimator.cs
Deflects ailerons/elevators/rudders to an absolute pose: each surface stores its build-time local
basis and gets Basis = base · Rot(hingeAxis, angle); three channels slew toward the stick at
SlewPerSec (TUNE), ±20° per kind. --fly only; frozen while paused/crashed, reset on respawn.
⚠ The per-surface sign bakes three flips: the stick convention (ailerons opposite per side, TE
  against the commanded rotation), a canard flip (hinge z < CanardMaxZ ⇒ nose-mounted ⇒ pull
  deflects TE-down), and a frame flip from the accumulated hinge axis vs its canonical plane-space
  direction (canard groups mounted yaw-π). Account for all three before touching any sign.

## src/Flight/WingLightBlinker.cs
Flashes the wingtip flares for FlashDuration ~0.033 s (TUNE — matches the original's own footage at
~1 frame @ 30 fps; the authored 0.0001 s literal is unusable) each WingLights.BlinkPeriod, and
toggles a matching OmniLight3D per flare (range/colour from WingLights) on the same window. Gated on
the session's ANIMATION_LOD quality flag (SessionSpec.AnimLod vs AnimRuntime.HighLod) — below HIGH
the flares/lights stay off for the instance's life, the def's authored low-detail branch. Reset
(respawn) restarts the cycle with everything off, handing every suspended lamp back. Advanced each
_Process, frozen while paused or crashed. --fly only.
`Suspend(flareName)` (BL-287) hands a named lamp to whatever just deactivated it — the fuel-leak stage
(`player_fuelleak`'s `OBJECT_ACTIVE_STATE … wing_flare2 false`, never reversed by the def) used to
fight the blink cycle, which unconditionally re-asserted the flare/light `Visible` every tick and
undid the leak's deactivation within 1.5 s. `FlightRigAssembler`'s `DamageEffectSink` calls
`Suspend("wing_flare2")` right after playing `player_fuelleak` through the rig runtime — a suspended
lamp's index is skipped outright in `Advance`, deterministically, not by detecting a stray write after
the fact (which would miss the common case where the blinker's own commanded state already happened
to read "off" the instant the leak fires).

## src/Flight/PylonOrdnance.cs
The rockets mounted under a plane's wings (D44): `Build` instances ONE FLYOUT MODEL body per loaded
pylon via `ProjectilePool.BuildFlyoutBody` (the SAME gamez prototype the round flies), parents it to
that pylon marker at identity local transform (nose -Z forward, tail at the mount = the launch pose),
and `Update` shows/hides each per its live `Hardpoint.Ammo`. FlightController drives `Update` after
UpdateRockets; the mounted body rides the plane and is freed with it. --fly only. `Unmount` takes the
set back off — detaching each body from its pylon IMMEDIATELY, not merely queueing it — so the weapon
lab's rebuild-on-swap cannot leave the old ordnance hanging beside the new.
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
Single-sourced: the terrain sweep casts these boxes AND `AircraftBody` mounts the same
`BoxShape3D` resources as the plane's hittable body — never a second derivation.
⚠ Relabel renames aft outboard boxes `wing` (box wholly one side of the centerline + centre
  outboard of WingBandFrac) so PlaneDamage's localImpact-blind "tail" arm never sees a wingtip
  strike. The half-span is known here — do not side-split in PlaneDamage instead.
⚠ Boxes deliberately overlap; the earliest in Parts order is what gets reported.
⚠ Known limit: the Bloodhawk's canard tips stay uncovered.

## src/Flight/ShakeDefs.cs
Typed reader over the shared `shakes.zrd.json` — the six shake-oscillator sources
(`docs/formats/shakes.md`), modelled on `WeaponDefs`: load once (`GameSession` →
`FlightRigAssembler.Inputs.Shakes`), named accessors per source, unhandled-key tripwire.
Each source is one law (frequency/damp/sawtooth) plus exactly one magnitude-term variant
(`magnitude_factor` [+ `he_factor`], `min_speed`+`magnitude_quotient`, or absolute `magnitude`);
absent sources read as null and `PlaneShake` no-ops them.

## src/Flight/PlaneShake.cs
The plane-wobble oscillators: gunfire buzz (`fire_bullet`), overspeed rattle (`high_speed`),
and being-hit rocks (`bullet_impact`/`missile_impact`/`explosion`), summed each sim tick into
`Roll` — radians the controller writes to `ShakePivot`, the node the assembler hung the plane
model under. Engine-free on purpose (unit-tested); the pivot write is the controller's one line.
Amplitude = `magnitude_factor × caliber` in radians of roll, **measured** off original footage
(`analysis/gun-wobble-shake/`); a rocket hit stands in its armor damage (declared TUNE).
`high_speed`'s input is speed over the plane's `fd_speed`, so the authored `min_speed` 1.0 gate
means "beyond rated max" — the dive rattle; cruise stays silent like the footage's idle floor.
⚠ **Visual-only by construction:** physics, aim, spread and the chase camera read the
  controller's transform, which sits *above* the pivot — route any new consumer of the plane's
  pose to the controller, never to a node inside the pivot, or the wobble leaks into it.
⚠ `nitro` and the ON_CALL `damage_shakes` defs stay unwired (no nitro system; unknown caller).

## src/Flight/FlightController.cs
The flying-aircraft node: input → FlightModel → transform, text HUD + telemetry, weapon fire as
`FireControl`'s engine adapter (polls the held triggers, `Step`s the machine each sim tick,
performs the `FireOutcome`: muzzle-transform spawns, gun-loop start/stop, dry cues, breadcrumb
logs), crash and respawn. The camera is `CameraController`'s — this node only feeds it
the pose, the dt and the mixed orbit axes (`OrbitInput`); on a crash it cuts to `CrashView` once,
writes nothing to the camera until respawn, and hides the HUD layer (the original's crash camera
shows no HUD — footage), restoring it on respawn. Sweeps the
PlaneCollider boxes via CastMotion each physics frame — mask world+aircraft with its own
`Body` (`AircraftBody`, built in `_Ready` from the same boxes) excluded by RID, so another plane
is solid and a mid-air resolves through the same SurviveHit/Crash as terrain; `Crash`/`Respawn`
toggle the body's hittability; the sim half is `SimStep(dt)`, called by
`_PhysicsProcess` (realtime clock) or by `GameSession` (fixed/halted clock). Collaborators:
FlightModel, CameraController + CamParams, Loadout + ProjectilePool (guns/rockets),
`CollideDamageSink` →
`AnimRuntime.CollideDamageAt` (fly-through facades), CrashRuntime, every HUD widget and animator.
`Crash` reads the struck body through the same `ProjectilePool.ClassifySurface` (`ClassifySurface`
here maps it to `CrashSurface`) to pick the variant: a `water`-tagged body plays
`player_crash_water` + `snd_exp_water_a`, everything else `player_crash_dirt` + `snd_exp_ground_a`;
`Air` is the no-impact destruct and has no trigger. `--crash` has no struck body, so it forces dirt.
It also fires `Audio.OnEngineStop` (the wind-down cue, layered over the explosion) and plays
`stopprops` on `CrashRuntime` — the one call site every engine-death path shares, whether the
collision resolver called it for a full-speed impact, for a critical part reaching 0 HP on a
survivable-speed graze (`SurviveHit` returning false), or for a projectile kill
(`TakeProjectileHit`: the pool-resolved hit — part-mapped armor-first damage plus the graze's
feedback triple, no cooldown since rounds are discrete; its `damageScale` is the blast falloff
share for a splash hit, 1 for a direct round). Every `Crash` raises `Downed` exactly
once — (victim `PlayerIndex`, killer: the killing round's shooter id; null for terrain, mid-air,
an unowned `NoShooter` round and every other cause) — a fact report the session scores in `--vs`;
flight holds no match state, and `Respawn` emits nothing. `AutoRespawnAfter` (session-armed —
Versus sets 3 s on every rig) auto-respawns a crash on the sim clock with R still skipping early;
null, the default, keeps every other mode manual-R (scripted HoldSegments runs keep their 1.5 s).
`Respawn` plays `startprops` back and resets
`ThrottleSmoke`, which `Update` otherwise drives every frame off the live throttle.
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
`Held` (the weapon lab) pins this ONE airframe while the session runs on: the sim step skips input,
`FlightModel.Step` and the whole collision sweep and re-applies the pinned pose through
`FlightModel.Reset(pos, attitude, 0, 0)` instead — everything from the pose commit down (weapon
selectors, guns, rockets, ordnance, gauges, telemetry) runs exactly as in free flight, which is what
makes the lab fire through the real path. `PlaceHeld(pos, lookAt)` moves the pin (C6/C7's re-park)
through the same `Reset` + `SnapCamera` pair `Respawn` uses; `SelectGunGroup`/`SelectPylon` are the
programmatic twins of G/H for the lab panel. A held airframe also takes the ORBIT camera rather than
the chase — `halted || Held`, since both mean "the plane is standing still and the view should swing
around it" — and `CameraOwned` (D8) makes this node write nothing to the camera at all while the lab
hands the same `Camera3D` to a `SpectatorCamera`; clearing it re-seeds the orbit from wherever the
free camera left the eye.
⚠ Held is NOT `GameClock.Halted` — the point is that the world keeps running while one plane stops.
  The pose goes back in through the MODEL, never by writing GlobalTransform behind it, so every
  `_model` reader stays consistent; and because a held plane sits at 0 m/s (below every stall speed)
  and may legally be parked under `UnderMapY`, the stall gauge/HUD line and the under-map respawn
  backstop are explicitly exempt while held.
⚠ The reticle march (`BallisticImpactPoint`) calls `Ballistics.March`, the same integration
  `ProjectilePool.SimStep` steps real rounds with — see `Ballistics.cs`'s entry for the one thing
  that still differs between them (`dt`).
⚠ The stunt/race AllComplete freeze runs BEFORE the crash branch; Respawn never resets a mid-run stunt.
⚠ Dogfight's R-ownership gate (`Match is { Completed: true }`, C25) is ALSO checked before the
  crash branch, mirroring the stunt/race rule above, but does NOT freeze the sim like it — the
  match keeps flying under its results board on every frame the button is not pressed, unlike a
  finished stunt run. `DebugForceCrash(killer)` (`--debug-scoreboard --vs`) is the scripted twin of
  a real weapon kill: it carries a killer id through the same `Crash` → `Downed` path a live hit
  does, so a screenshot's K/D/leader/banner/board are real facts, not staged ones.

## src/Flight/PlaneDamage.cs
Per-part armor + hit points from vehicle.json destroyable_parts (via PlaneStats). MapStruckPart
maps a struck collider box + plane-local impact to the data part: wing/canard by impact X sign
(left = −X), fuselage fore/aft of z 0 → nose/tail. Apply(part, healthDamage, armorDamage) spends
armor first and carries the share armor could not absorb into health within the same shot, scaled
by the round's health magnitude — so a bare zone takes exactly HEALTH_DAMAGE; the one-magnitude
overload (collisions) spends that amount across both pools. Reset refills both on respawn, Summary
feeds the HUD DMG line, Fraction/WorstFraction are the COMBINED armor+health progression (the scale
the injure_anims thresholds are on), feeding whole-plane feedback like FlightAudio's damaged-engine
loop.
⚠ The "tail" arm ignores localImpact and is correct only because PlaneCollider.Relabel hands it
  no outboard boxes — do not fix tail sidedness here; widening the signature was rejected.
⚠ The `engine` flag (power loss) is unwired **by design, not deferred** — the original states damage
  never degrades performance; but the shipped data still sets the flag, so retail may have walked
  that back (docs/formats/vehicle.md).
⚠ A stock zone's effective pool is DOUBLE its MaxHp (armor == hp on all 88 shipped entries, spent
  first) — faithful to the original, not a regression to tune away. Armor at 0 is a stripped zone,
  not a dead one: only Hp ≤ 0 downs a critical part. FlightController.Crash still never calls in
  (a hard hit is a boolean destroy) and player.json's crash block is still unbound (`BL-172`).

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's combined armor+HP fraction crosses an
injure_anims entry it shows the torn pdpN panel, hides the healthy skin, and plays the entry's
AUTHORED anim through `DamageEffectSink` — the player's own rig runtime (`BL-259`): `pdpanelN`
(gimmeflakes debris + the staged short_firetrail/loop_short_firetrail burn-down at the panel),
`player_fuelleak` (0.85 — gunhit flash + fuel vapor at a random pdp1–3, its stream authored-gated
on that panel being ACTIVE, so it renders only once torn), the `<part>_damage_effects` spark shims
(0.99, `player_pfighter` data alone — B4; their general home is the weapons.json `player` IMPACT
surface, blocked on `BL-222`), and, for the data's 0.10 `player_smoketrail`, `player_damage_trail`
(short_firetrail at prop1 + the fire_lt light) — `RigAnimFor` owns that one mapping.
`DamageEffectStop` (Reset, first) stops the whole stage CLOSURE, derived from the program — a
stopped pdpanelN cannot reach the trail it CALLed, and prop1's trail has no authored exit.
⚠ `gimmeflakes` "firing on every damage stage" (`BL-288`, fixed) was never this class's dispatch:
  `_applied`'s per-anim-name guard fires each `pdpanelN` (and its one `CALL_ANIMATION gimmeflakes`)
  exactly once, and each `AT_NODE` resolved to its own panel throughout. The repeated-burst/
  wrong-site symptom was the crash rig's then-unpooled shared template being teleported to each new
  tear — fixed by the crash-rig template pool + caller-slot assignment (see `src/Mech3/AnimRuntime.cs`'s
  pool paragraphs). Any future probe in this family: use `--plane=player_bhawk` explicitly — the
  airframe the bug was reported on, one of the three crossed-naming outliers below.
⚠ PairHealthySkins' CANDIDATE sets are def-derived (`BL-270`, `PanelPairingSets`: plane_reset's
  re-ACTIVE list = the hideable _h skins, the pdpanelN targets = the torn set; the viewer loads
  the two reader files, flight reads the bound program; no def data = a loud Warn + the unscoped
  fallback). The ASSIGNMENT inside those sets stays positional — no def ever deactivates an _h
  node, so the pairing itself is not authored anywhere — by merged mesh-AABB position, never by
  name (the _h numbering is crossed on three models — docs/formats/gamez.md) and never by node
  origin (the placement is baked into mesh space); unpaired _h skins are never hidden.
⚠ Null sinks fall back: the parked viewer keeps stand-in Puffers burning in place (UpdateStatic, at
  the panels + the authored prop1 anchor — a parked plane travels no distance, so the authored
  distance-interval trails would emit nothing); a world-less flight renders panel flips only, logged.
⚠ got_hit_anim's nosedamage blink and the *_damage_green/yellow/red cockpit cycle stay unwired (no
  cockpit; GaugeCluster.OnPartDamage approximates the blink by hand — do not merge the two,
  different data and different surface).

## src/Flight/DamageLab.cs
The damage lab (F5 toggles): one armor slider (parts the data gives an armor pool) plus one health
slider per destroyable part — `PartFrac` (Health, Armor, Combined) is what an `IDamageLabTarget`
reads/writes, `Combined` is `DamageLab`'s own derived (armorFrac×MaxArmor + healthFrac×MaxHp)/
(MaxArmor+MaxHp), the scale the injure_anims thresholds and the mirrored GaugeCluster damage dial
are on. `ReadSliders` (`BL-278`) floors a part's armor at 0 whenever its health reads below 1,
mirroring `PlaneDamage.Apply`'s armor-first real path (armor absorbs a round in full before any of
it reaches health, so no reachable state has health short of max with armor still standing) —
armor alone can still be driven to 0 with health untouched, just not the reverse. Presets
(--damage=part:frac) set both sliders to the same raw fraction through the same ValueChanged path
as a hand drag, so a fraction below 1 floors armor there too. One panel, two hosts, chosen by the
injected IDamageLabTarget (same file): ViewerDamageTarget drives DamageVisuals on a parked plane
from `Combined` alone (it holds no model), FlightDamageTarget writes P1's real PlaneDamage — each
pool through its own single-pool `PlaneDamage.Apply(part, healthDamage, armorDamage)` call after
`Reset`, using the fractions `ReadSliders` already floored. Neither target reimplements visuals,
only decides when to rebuild them.
⚠ Reapply's crossed-anim set-diff (TargetAnims) is load-bearing twice: it implements repair
  (re-derives from pristine) and keeps a slider drag from restarting the fires at every pixel.
⚠ Built in EVERY viewer AND flight session (StartHidden without --damage) so F5 has a receiver; two
  F5 presses must return a byte-identical frame (SetLabVisible toggles panel + gauge layer together).
⚠ Its widgets are stripped of keyboard focus through `UI.PanelFocus` — this panel has a flight host,
  where a focused "repair all" would swallow the Space trigger.
⚠ FlightDamageTarget.Tick is deliberately empty and it must not touch Gauges.PartFraction:
  FlightController already drives DamageVisuals from the live pose and binds the dial to the same
  PlaneDamage the sliders write. SyncFromTarget's read-back skips sliders being dragged.
⚠ `--damage=` forces `DamageLab` (the CLI flag) false whenever `WorldMode` is set (`SessionSpec.cs`
  ~1024) — a chaptered `--fly --damage=` still builds and presets the panel, just hidden behind F5,
  not open on launch; only a chapter-less `--viewer --damage=` opens it immediately. Pre-existing,
  not a B12 change.

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
panes keep them on screen. `DamageZoneColor(frac, yellowAt, orangeAt, redAt)` (`BL-085`/`BL-173`,
`PLAN-armour-layer` C21) is the damage-dial band function — `frac` is `PartFraction`'s COMBINED
armor+health value (both bound sources, flight and the lab, feed that scale; nothing here computes
it), `yellowAt`/`orangeAt`/`redAt` are mined per-part from the data's own
`*_damage_green/yellow/red` injure_anims. Both `Border` and `Fill` always take the same colour
index — `BL-173`'s refuted fix shape was a synthetic per-pool ring split; there is only ever one
colour per zone. `GunIndicatorColor`/`HardpointIndicatorColor`/`SlotIndicatorColor`/
`DamageZoneColor`/`TargetArrowAngle`/`TweenArrow`/`IndicatorLowFrac`/`ArrowSweepDegPerSimS`/
`StallBlinkHalfPeriodS` are `public` (not `internal`) so `CSVM.Tests` (`GaugeColoursTests`,
`GaugeArrowTweenTests`, `StallWarningTests`) can call them from outside the assembly — moved from
the in-engine `gauge-colours`/`gauge-arrow-tween`/`stall-warning` suites (`PLAN-engine-free-suites.md`
A3, B11). The two animated cues are plain nested structs, `GaugeCluster.ArrowSweep`
(`Angle`/`Advance`/`Reset`) and `GaugeCluster.StallLamp` (`Lit`/`Advance`/`Set`) — B11 retired the
`internal` testability-escape hatches (`StallLampLit`, `AdvanceStallLamp`, the private
`_gunArrowAngle`/`_missileArrowAngle`/`_stallBlinkPhase`/`_stallDwellS`/`_stallLampOn`/
`_stallWarnPrev` fields) that existed only so the in-engine suite could reach a live `GaugeCluster`;
the structs need no `Control` to construct, so `CSVM.Tests` drives them directly. Scoped to
`GaugeCluster` only (Decision 5) — `FlightController`'s stall/arrow feed predicates are untouched.
⚠ The gauge textures lie — compare pixel values, never appearances: the faces hold dark UNLIT
  copies of the STALL / LOW ALT windows (~58,0,0 unlit vs 180+,0,0 lit); bitten twice. The needle
  draws its shipped RGBA art untouched (the pointer silhouette is the rtexture-tier alpha, BL-048)
  — never re-add keying or load-time shaping.
⚠ The weapon-gauge 4-digit readout is per-GROUP for guns, per-PYLON for rockets — NOT a total; the
  belt-indicator yellow tier is likewise GUN-ONLY (hardpoint/pylon indicators go green→red, never
  yellow — confirmed against the original).
⚠ Both animated cues advance in `_Process` on **sim** dt (`GameClock.Current.FrameDt`), never the
  raw frame delta or `_time` — their rates are video-decoded in sim seconds and the wall figures
  would run them 39% fast. (1) The gun/missile arrow SWEEPS at a shared constant 168.7 °/sim-s
  (`ArrowSweepDegPerSimS`, `BL-184`/`CAP-18`, `TweenArrow`, shortest-way wrap), not drawn from
  `Selected` — `_Draw` reads the already-advanced `_gunArrow.Angle`/`_missileArrow.Angle`
  (`GaugeCluster.ArrowSweep`), and the readout digits/type name still snap on the sweep's first
  frame; do not tween those too. (2) The STALL lamp's blink is a RATE ramp
  (`GaugeCluster.StallLamp.Advance`, `BL-148`/`CAP-06`): binary brightness, duty 0.50, half-period
  ∝ the fd fraction the controller feeds as `StallFrac`. It is INTEGRATED, not read off a clock —
  a period that changes mid-dwell must shorten the remainder, not jump the lamp. `Reset()` calls
  each struct's own `Reset`/`Set` to clear the arrows to NaN and re-arm the lamp lit, so a respawn
  snaps. The LOW ALT cue is legitimately a plain fixed blink (`WarnBlinkPeriod`) — do not
  generalise the ramp onto it.
⚠ The gungauge/missilegauge face is on a generic child (`g815`/`g819`) on ALL planes (no Bloodhawk
  special case, unlike the damage dial) — "any unrecognised child = face" is the extraction rule.
⚠ The hardpoint gauge's `WeaponGauge.Slots`/`Selected` index by PYLON NUMBER
  (`Hardpoint.Index − 1`), never by position in `Loadout.Hardpoints` (`BL-294`) — that list is bound
  via `PylonFillOrder`, not `1..N`, so a plane with fewer than 8 pylons must show gaps at the
  ring's unfitted physical positions rather than piling its lit slots at the start.
  `FlightController.UpdateWeaponGauges` builds a fixed `HardpointRingSize`-length array (unfitted
  defaults 0f, reading red same as spent) and reindexes `Selected` through the bound `Hardpoint`'s
  own `Index`, not the loop position. Guns need no equivalent fix: turret slots (the only ones
  `FirableGuns` skips) are always trailing (W3/W4), so the compacted gun-group index already equals
  the physical slot.

## src/UI/LaunchMenu.cs
The in-game launchscreen CanvasLayer: Mode → Chapter → Plane, input polled every frame through
one MenuInput per player (no input-map/focus wiring); joining is gated to the Plane screen, and
with >1 player that screen becomes real SplitScreen.PaneRect panes — pick in the pane you fly in.
Three Mode rows — Free Flight/Stunt Flying/Dogfight — map 1:1 onto `SessionSpec.MenuMode`'s
ordinals; `Launch` carries the enum (not a bool) straight through to `SessionSpec.FromMenu`. Size
comes from GetViewport().GetVisibleRect() (a CanvasLayer is not a CanvasItem); LayoutScale caps
fonts so 4P fits 720p.
⚠ Player 1 is the keyboard + the SET of all unclaimed pads until ClaimP1Pad pins its real pad —
  never pads[0], which re-breaks the phantom-device fix; the leftover set makes hand-off free.
⚠ Re-entrant: ShowMenu resets to Mode, clears locks (joined players survive), and primes input +
  join edges from the CURRENT raw state — a still-held Esc/Start must not read as a fresh press.
⚠ Dogfight withholds the launch gesture below 2 joined players (`CanLaunch`), even once P1 is
  locked — the hint line says so; every other mode launches solo exactly as before.

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
⚠ `CloudFieldLayer` (bit 15, layer 16) is the other named allocation and is NOT per player: one
  shared layer carrying BOTH ambient cloud populations (`fvol` clutter + placed `cloudparent`), so
  `Session/WeatherRig.Tick` can gate them per CAMERA by cull mask (`A7`). It sits just below the
  player band on purpose — every mask this file builds includes it, so the gate is something that
  switches OFF and a mode or chapter that never runs the gate renders the clouds as before.
  Instances are MOVED onto it, off layer 1, or dropping the bit would change nothing.

## src/Flight/PlayerRig.cs
One rendered view's state bag: index, camera, optional `SubViewport`, `HudParent`, `VisualLayer`,
the player's FlightController, and private camera-anchored copies (`Horizon`/`Deck`/`Whiteout`) —
those re-anchor to the view's camera every frame, so N players need N of each.
⚠ The ambient cloud field is deliberately NOT one of them (`BL-273`): the authored fogvol clutter
  is world-anchored static geometry every pane shares, and the `Puffs` slot went with `CloudPuffs`.
  It still gets a per-pane ANSWER, just not a per-pane copy — `WeatherRig.Tick` gates it (and the
  world's `cloudparent` clusters) through `Camera.CullMask` on `SplitScreen.CloudFieldLayer`, so
  the per-view decision lives on the rig's camera rather than in a duplicated subtree (`A7`).
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
The weapon lab's **panel** (`--weapon-lab`, key **B**): a configurator for the held aircraft's LIVE
loadout, hosted top-right in flight (the gauge cluster owns the bottom-right corner, the HUD the
top-left). It owns no weapon and fires nothing — the steppers write into the `FlightController`'s
bound `Loadout` and the aircraft's own trigger then fires it. GUNS arm gun groups, HARDPOINTS arm
pylons: a gun goes onto the SELECTED group with a full clip of its `CLUSTER_SIZE`; a hardpoint
weapon re-arms EVERY pylon (`ProbeRunner.ApplyRocketOverride`) and rebuilds `PylonOrdnance`. The
mount stepper drives `SelectGunGroup`/`SelectPylon`, the auto-fire toggle `AutoFire`/
`AutoFireRockets` by bank, and "reset to stock" restores the fit the session launched with (stock
as `--rocket=`/`--loadout=` left it, not as the file reads). Gun mounts are in `FirableGuns` order
because that is the order `SelectGunGroup` indexes; a plane with no loadout at all falls back to the
raw marker rig, which has nothing live to arm.
**Click to place:** a left click casts the lab's OWN physics ray from the camera, names what it hit
(`cs_name` ancestor via `SelectionService.NameOf`, class via `ProjectilePool.ClassifySurface`,
distance) and re-parks the held plane on that same ray at the panel's stand-off through `PlaceHeld`;
shift-click aims without moving, and an orange ball marks the aim point. The scripted twins all fire
on the first physics frame, most specific first — `--weapon-target=x,y,z`, then
`--weapon-surface=water|buildings|dirt` (nearest collider of that class, measured to the nearest
collision VERTEX, since a chapter's water tiles all sit at the world origin), then
`--weapon-click=x,y[,aim]` — and every one of them ends in the same `PlaceOn` as a real click, at
`--weapon-standoff=` metres. **V** hands the rig's camera to a `SpectatorCamera` and back
(`--weapon-camera=free|<frames>`), the controller standing down via `CameraOwned` in between.
`--weapon-cycle=N` steps the weapon list every N physics frames; stepping, placing and the camera
hand-off are the only things this node does per frame.
⚠ No widget of this panel takes keyboard focus (`UI.PanelFocus` strips the subtree at build): Space
  is the fire key here, and a focused stepper would re-press itself instead.
⚠ This node NEVER spawns a round — there is no exception left since D9 moved the `--weapon-test`
  48-weapon pass check out to `src/Flight/WeaponBench.cs`, and it takes no `ProjectilePool` at all.
  Do not give the panel a firing loop back — the lab exists to fire exactly what free flight fires.
⚠ The ray is cast in the PHYSICS step, never in the input handler (the space state cannot be queried
  while the server flushes queries), and the class is read off THE BODY THE RAY RETURNED and nothing
  else — one mesh yields a body per surface class present (`col`, `col_water`, `col_buildings`), so
  adjacent picks on one object legitimately differ. A click that hits nothing leaves the plane put.
⚠ A hardpoint swap must `PylonOrdnance.Unmount()` the old set BEFORE building the new one, or every
  swap leaves a body under each pylon; `ordnance_nodes=` on the `weapons` debug line is the tripwire
  (it must equal `mounted=`). A weapon whose `FLYOUT` model this chapter's gamez lacks mounts
  nothing — `Build` returns null and the wings go empty, never a throw.

## src/UI/PanelFocus.cs
`Strip(subtree, who)` — makes every `Control` under a panel unfocusable and logs the tally
(`N control(s), M made unfocusable, focusable_left=0`), the invariant every panel hosted in a
**flight** session must hold. A focused `Button` answers Space with "press me again", so the pilot's
fire key re-fires the last-clicked stepper instead of the guns, and the arrow keys walk the focus
chain instead of reaching the aircraft or the lab's orbit camera (user-reported on the weapon lab,
2026-08-03; the flight damage lab had it too — 11 and 5 focusable widgets respectively).
⚠ Applied to the **whole subtree**, not per widget, precisely so a control added to a panel later
  cannot re-open the hole — the older `--freecam`/`--anim-lab` labs set `FocusMode` per button, and
  a slider or toggle added beside those buttons would be missed. The panels not covered are the
  `--viewer`-only ones (`LiveryLab`, `MeshLab`), where no trigger key exists to swallow.
⚠ `focusable_left` is **counted again after the walk**, not assumed from what the walk changed —
  that number, not the intent, is what the log line reports.

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

## src/UI/AiNetsOverlay.cs
The AI patrol-net overlay (F13; `--debug-ainets[=name,…]` scripts it) — added to every chapter
world by `GameSession.BuildWorldStage` (skipped on the `--node=` partial stage). Draws each net in
a stable id-derived colour (golden-ratio hue): edges as individual segments off the edge list,
sphere markers per node (tagged nodes bigger), one fixed-size `Label3D` per net with the trailer
(`M4ReinfAce#10 → player`), all depth-tested. Nets load lazily on first toggle; the census — one
line per net — goes to the `world` log. A HUD text field narrows the drawn set live by
case-insensitive name prefix. F13 is the first tenant of the F13–F24 debug-overlay key
range (`docs/controls.md`).
⚠ Never draw node order as the route — the graph branches; only the edge list is connectivity.
⚠ The filter field is a deliberate PanelFocus exception (a text filter cannot work unfocusable):
  focus arrives only by clicking the field, and Enter releases it back to the aircraft.

## src/UI/ClassOverlay.cs
The colour-by-class overlay (key X, `--debug-classoverlay` scripts it) — same mode set as
`ColliderOverlay` (`--freecam`/`--anim-lab`/`--fly`/`--stunt`), a findable-targets view rather than a
collision one (`BL-029`). Mixes a class colour over every drawn mesh at 50 % (`TintStrength`), so a
target stays recognisable as itself: destructible (red, via `DestructibleRegistry.Resolve` — the
exact climb a weapon hit takes), facade (pink, via `SceneBuilder.ClassifyBillboard` on the source
`GameZMesh`, resolved back through the built node's `AnimRuntime.IndexMeta`), clutter (green, every
`MultiMeshInstance3D` under the world root — nothing else in this codebase parents one there),
everything else scenery (blue). Rebuilt on every X press rather than cached, clearing each tinted
node's `csky_tint` first.
⚠ **The tint is an instance shader parameter (`SceneBuilder.TintParam`), never a `MaterialOverride`
  — and that is a bug fix, not a style choice.** An installed material is a different shader from
  the world's: it defaults to `cull_back` where this world is `cull_front` (the overlay rendered
  inside-out, 2026-08-03), it lacks the bias shader's `skip_vertex_transform` depth scale (the tint
  z-fights its own geometry), and it lacks Clutter's billboard spin (ghost tree cards at a fixed
  heading). Tinting inside the real shader — `SceneBuilder.TintLine`, last write to `ALBEDO`, after
  fog — has none of those by construction, and is the only form that can blend WITH the texture.
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
Interactive (FixedAccum) frames draw each live transform-motion target interpolated between its
last two SIM poses (`GameClock.StepFraction`); sim poses are restored before any step runs, so
render smoothing never leaks into event held-pose seeding and FixedStep stays byte-identical.
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
⚠ Near-pure path arithmetic — the only I/O is `PreferUnzipped`'s directory-exists probe and
  `ChapterTextures`' scan for the chapter's top `rtextureN` tier (the archive the original renders
  from — same files/resolutions as `texture.zbd`, different pixels; see docs/tooling.md).
⚠ The `--gamez=`/`--textures=` override policy deliberately stays in GameSession; this class only
  builds the default extraction-tree paths.

## src/SessionSpec.cs
Everything the command line settles about a session, as one immutable record: `Parse(args)` parses
**and resolves**; the pure arg parsers (`ParseVec3`, `ParsePlanes`, `ParseHold`, …) are public so
they are testable. `SessionMode` is closed — Menu/Fly/Viewer/Freecam/AnimLab — with modifiers
(`Stunt`, `Versus`) and `SessionProbe`; per-rule coverage lives in `CSVM.Tests/SessionSpec*Tests`.
`Versus` (`--vs`, `--vs-kills=`, `--vs-time=`) beats `Stunt` by fixed precedence, not last-wins.
⚠ **Step order in `Resolve` IS the behaviour**: `--stunt`/`--vs` move `Scenario` before arbitration
  can clear the mode that set it; the freecam/anim-lab-only debug tools are dropped after `--node=`
  has forced the viewer. Both look like tidying chances.
⚠ **Pure — no engine state, no globals, no logging, no clock** (DET-9: a spec that read the clock
  would not be a function of its args); complaints go to `Warnings`, never a print.
⚠ **`FromMenu` is static, takes its base as a PARAMETER, and does NOT re-resolve** — it must keep
  writing every menu-settable field, or the pristine base re-opens the carry-over bug. Takes a
  `MenuMode` (Free/Stunt/Versus, also defined here so `LaunchMenu`'s tests stay engine-free), not
  a bool — the >= 2-player Dogfight lock is `LaunchMenu`'s job, not this factory's.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram` — the world+anim half of a session build;
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights.
`Options.EmitterFactory` (null → the real `Anim.PufferEmitterFactory` over this build's texture
archive and `EffectsParent`) is read once, here, and never reassigned after `Build` returns — a
caller supplies its own to observe emitter lifetime with no GPU (`CSVM.Testing.CountingEmitterFactory`
is the one caller, through `TestContext.EmitterFactory`); a post-build swap would miss the bootstrap,
where most `PUFFER_STATE`s fire.
⚠ Disposal contract, **one flag per archive because the two lifetimes differ**: calls
  `Runtime.Emitters.RetireFactory()` unless `Options.TexturesOutliveBuild` **or the caller supplied
  its own `Options.EmitterFactory`** (a caller-supplied one holds no archive reference, so it is
  never auto-retired regardless of the flag — `CountingEmitterFactory` needs neither
  `TextureArchive` nor `TexturesOutliveBuild` to stay alive past the bootstrap), and nulls the sound
  loader (prewarming first) unless `SoundsOutliveBuild`. A game session owns the TEXTURE archive all
  session (so this is always true there — a false left the world with no runtime fire, trails or
  dust, `BL-234`) and scopes the SOUND archive to the build; only the lab owns both. A retired
  factory warns once at the first late `PUFFER_STATE` — the bootstrap census cannot report a runtime
  miss.
⚠ **The phase boundaries are a reported contract** (`StartupProfile` spans zrdr/world/clutter/anim/
  bind/prewarm): move a step, move its `Record` — a dropped phase reads as a growing `rest`, not as
  missing. Keep them leaves.
⚠ `Options.NodeSubtree` skips MissionSetup deliberately — the one verb that reliably resolves is
  the one that switches the subject off (C1/IA1 hides `hk_zep`); a node stage shows the subtree in
  its gamez base state.

## src/Mech3/SessionArchives.cs
`OpenFor(ArchiveIntent, gamezPath, texturesPath, soundsPath, zrdrPath, mute)` opens the five
archives one chapter build needs (gamez, textures, sounds, sound defs, sound groups) and returns
them alongside the `WorldSession.Options.TexturesOutliveBuild`/`SoundsOutliveBuild` pair
`ArchiveIntent` implies — `Session`/`Lab` (textures outlive; sounds only in `Lab`) or `Suite`
(neither). One seam replaces the two near-identical open sequences `GameSession.LoadArchives` and
`TestHarness.BuildWorld` used to hand-write (`BL-241`'s own fix note: the harness forgot
`TexturesOutliveBuild`). `StartupProfile.Mark`/`Record` calls are unconditional here, same as
`WorldSession.Build`'s own phases — a no-op with no session under measurement, which is what lets
the test harness drive the same code blind.
⚠ **`OpenFor` chooses the flags, it does not collapse them** — `WorldSession.cs`'s own decision
  ("one flag per archive because the two lifetimes differ") stands; `Session` and `Lab` still
  disagree on `SoundsOutliveBuild`. Archive ownership past `OpenFor`'s return (`_sessionTextures`,
  `LabTextures`/`LabSounds`, the `using` locals in `TestHarness.BuildWorld`) is still each caller's
  own — `OpenFor` only opens the archives and states the two flags, it does not dispose anything.
⚠ The sound archive is scoped to the build (`haveSounds` gates it on `!mute` and the file/directory
  existing) and the texture archive never is — reproduce that asymmetry per intent; do not
  normalise it away.

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
`LoadArchives` opens the five session archives through `SessionArchives.OpenFor` (`ArchiveIntent.Lab`
when `_spec.AnimLab`, else `.Session`), which is also where `TexturesOutliveBuild`/
`SoundsOutliveBuild` come from now — `BuildWorldStage`'s `WorldSession.Options` reads them off
`BuildState`, it does not set them by hand. In `--vs`, `BuildFlightRigs` builds one `VersusMatch`
(`VsKills`, `VsTimeMinutes`×60 s) BEFORE the rig loop — same reason `StuntRace` is built early —
so `FlightRigAssembler` can bind every pane's `VersusHud` to it (C23); once every rig exists it
forwards each one's `Downed` into TWO independent subscriptions: the scoring one (a killer inside
the roster is `RegisterKill`, anything else — terrain, mid-air, unowned or `IncomingFire` rounds —
is a plain `RegisterDeath`) and a kill-banner broadcast that pushes the same fact to every pane's
`VersusHud.OnKill` (never piggybacked on the scoring handler); arms every rig's 3 s auto-respawn
(`VersusRespawnDelay`, R skips); sets `Controller.Match` + `Controller.RestartMatch` on every rig
(C25 — the R-ownership seam, mirroring `Race`/`RestartRace`); and builds `VersusBoard` on its own
CanvasLayer, same construction as the race board just above it. `RestartMatch(match)` (private,
invoked through the delegate above) mirrors `RestartRace`: `match.Restart()` then every rig
respawns. The match clock advances on sim dt only (`_PhysicsProcess` realtime, `DriveSimSteps`
when parent-driven), so a halt freezes the match with the sim. `DriveSimSteps` also carries
`--debug-scoreboard --vs`'s one-shot forced kill (`_versusDebugKillFired`, same single-fire shape
as `--crash`'s `_crashFired`): P1 downs P2 through the real `DebugForceCrash(killer)` → `Crash` →
`Downed` path on the first sim step, so a scripted screenshot has a real, attributed kill without
scripting an actual shot.
⚠ **It parses no args and resolves nothing** — the Launcher hands it the one `SessionSpec` its
  session is built from; **a new flag is a SessionSpec change**. `_menuPads` is the deliberate
  exception: join-flow session state riding the `LauncherContext`, never the spec.
⚠ **There is no `Teardown()` — return-to-menu is `QueueFree`**; the session subtree frees
  atomically under `_worldRoot`. Only three non-child duties run in `_Notification(ExitTree)` —
  null `GameClock.Current`, `Dispose()` `_worldLights` + `_sessionTextures` — all null-guarded
  (no double-free after a failed build) and race-free (menu relaunch is a frame later).
⚠ **The weapon lab is built in `BuildFlightRigs`, not the viewer path** (A3): it needs the session's
  `ProjectilePool` + world-effects wiring and player 1's held controller, all of which exist only
  there. The viewer path builds **no** lab node at all: `--weapon-test` is `WeaponBench.Run` over a
  parked plane and a scene-less pool, and quits the session (D9). `DriveSimSteps` no longer steps a
  lab or a second pool.
⚠ **`HorizonScale` 2.5x is a MAXIMUM, not a constant** (`HorizonScaleFor`). The skydome is anchored
  on the camera, so its far wall sits at (its own radius × the scale) from the eye in every
  direction; past `Camera3D.Far` (40 km) it is clipped and the engine clear colour shows through
  the sky. Every chapter's dome is 6.4–12.0 km and clears that at 2.5x — except **C1B's zone1 at
  21.8 km**, which 2.5x puts at 54.5 km, so it renders as a hole (seen at the controls when
  `BL-277`'s per-chapter zone selection first chose it). The scale is fitted to
  `HorizonFarFraction` (0.9) of the far plane from the built dome's OWN AABB, never a per-chapter
  table: C1B lands ~1.65x, every other chapter keeps 2.5x exactly, which is why no other chapter's
  sky moved. Raising `Camera3D.Far` instead would cost depth precision world-wide.
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
logs is callable from `CSVM.Tests` without an engine. `CSVM.Tests` installs a process-wide no-op
default once, before any test runs (`TestHostLogSink.cs`, `[ModuleInitializer]`, BL-302) — the
per-class save/restore alone raced across xunit's parallel classes and could restore the sink to
null mid-run, and the resulting `GD.Print` fallthrough killed the test host with an unmanaged
`AccessViolationException` on ~1 in 3 full runs. A test that asserts on console lines takes a
**scoped** sink instead — `Log.PushConsoleSink(sink)` returns an `IDisposable`, nests, and is
per execution flow, so a class running in parallel can neither steal its lines nor add its own
(BL-306; a console line resolves scoped sink → process-wide `ConsoleSink` → the engine).
⚠ **The two tiers are separate storage on purpose** — folding the process-wide default into the
  `AsyncLocal` would break it: `TestHostLogSink` installs from a `[ModuleInitializer]`, whose flow
  xunit's test threads do not inherit, so every test would silently drop back onto the
  host-killing `GD.Print` fallthrough. The converse edge is real too: a thread the code under test
  spawns without capturing the execution context sees `ConsoleSink`, not the scope.
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
`--dump-flight` / `--dump-mips` / `--damage-test` / `--effects-test` inspection reports. Each probe
does the work once and returns both halves: the report text the flag prints and writes, and a
structured verdict (counts, per-row booleans, failure strings) a `--run-tests` suite asserts on.
`Effects` owns the effects sweep — the puffer half and the template-MESH half (`BL-061`), the
latter counted only through `MeshCensus` (per-root per-tick PEAK + distance-to-play-point +
post-stop residual; a final-sample census misses meshes the data turns off inside the window,
INSTR-11). The `effect-template-mesh` suite counts through `MeshCensus` too, so the sweep's
verdicts and the suite's assertions cannot drift apart.
⚠ `FlightEnvelope` steps a throwaway `FlightModel` through the manoeuvres the ORIGINAL was
  recorded flying; its targets are the Bloodhawk's only, since it is the only airframe on video.
  A row with `Informational` set is measured but deliberately not asserted (an open question) —
  never promote one to a verdict without the measurement that closes it. A row with `UpperBound`
  set asserts a CEILING, not a band, for a measurement whose failure is one-directional
  (`sustained-turn-sink`: sinking harder than the original is the defect; sinking less is a
  different divergence and must not be folded into the same verdict).
⚠ **One source of truth.** The flags in `ProbeRunner` are thin wrappers over these; a check added
  to a probe reaches both the report and the suite. Never re-implement a check in a suite.
⚠ `Probes.SweepCap` (16) caps the swept ROWS, not the registry totals — a census must read
  `DamageResult.TotalInstances` / `DistinctAnchors` or it silently under-counts (LOG-5).

## src/Testing/TestHarness.cs
`--run-tests[=filter]`: the suite registry, `TestContext` (assert verbs, resolved data paths, a
scene-tree host, and `WithWorld` — the chapter-world builder over `WorldSession`), the
PASS/FAIL/SKIP table, `.scratch/test-report.json`, and the process exit code. `TestContext.
EmitterFactory` (mutable, default null) forwards straight into `WorldSession.Options.EmitterFactory`
for the next `WithWorld` build — a suite sets it, on a chapter other than `Chapter` so a cached
default-chapter world built before the set is never reused in its place. `BuildWorld` opens its
archives through `SessionArchives.OpenFor(ArchiveIntent.Suite, …)`, then `using`s the returned
`Textures`/`Sounds` itself — `OpenFor` states the (both-false) lifetime flags, it does not own the
disposal. `TestWorld` also carries the parsed `Gamez` past the build (not disposable, unlike the
texture archive) so a suite can build real geometry of its own from it — the effect-template stage
`effect-template-mesh` needs.
⚠ **In-engine is the smaller half.** Only checks that need a live Godot belong here; anything
  that runs without the engine goes in `CSVM.Tests` (`dotnet test`) instead.
⚠ **Engine-error policy.** Native `ERROR: …` lines are screened out of band against
  `ErrorAllowlist`; **every allowance carries a cap and its measured count is printed even on a
  pass**. Unknown error → fail; over cap → fail; no log → SKIP, never PASS.
⚠ Run **windowed**: `--headless` compiles no shaders, so a clean error screen says nothing about
  them (LOG-8).

## src/Testing/CountingEmitterFactory.cs
`IEmitterFactory` for a suite: `Create` always succeeds and hands back a `CountingEmitter` — no
`TextureArchive`, no `MultiMesh`, no Godot type anywhere in its own state, just `Started`/`Stopped`
counts and whether it is sustaining now. Reached by installing it on `TestContext.EmitterFactory`
before a `WithWorld` build (`WorldSession.cs`); `CountingEmitterFactory.Built` is the list a suite
reads to confirm the fake was actually reached rather than a real `Puffer` — the seam `BL-241`'s
own fix note asked for.
⚠ Honest about `SustainEnd`-then-revive: `IsValid` stays true after a stop, same as a real `Puffer`
  — `EmitterDirector` revives a stopped entry through its own dictionary, never by asking the
  factory again, and a fake that went invalid on stop would assert against a lie.

## src/Testing/RecordingEmitterRenderer.cs
`IEmitterRenderer` for a suite: it keeps the particles a `Puffer` hands it (`LastFrame`, `Shown`,
`MaxShown`, `MaxIndex`, `MaxFrame`, plus the `Capacity` the emitter sized) instead of drawing them,
so `puffer-modes` asserts on burst / distance-trail / sustain with no atlas, `TextureArchive` or
`MultiMesh` in the path. The mirror of `CountingEmitterFactory` one seam lower: that fake replaces
the whole emitter so `EmitterDirector`'s LIFETIME is assertable, this one replaces the draw so the
emitter's own MODES are. Neither covers the other's job.

## src/Testing/Suites.cs
The 26 registered in-engine assertion suites cover plane/loadout bindings (stock and, since M3 B4,
the full-rig `Loadout.ForRig`), live weapon fire, the air-to-air hit chain (`air-to-air`: two real
flight rigs on manual sim steps — body strike, struck-shape→part mapping, armor-first data-value
damage, critical-zero Crash, crashed-plane immunity, the zero-self-hits negative case, which
must stay non-optional, `Downed`-into-`VersusMatch` attribution: the weapon kill scores
exactly the shooter, killer-less and unowned-round deaths score nobody, and the VS respawn loop:
`AutoRespawnAfter` 3 s respawns at that mark in sim frames, respawn reports nothing, null waits
for R),
destructible stages/death/census, animation
stops and bounce-terminated launches, the full effects sweep (`effects-census`: every effect
resolves, template meshes peak at the CALL SITE not the stage origin, none stays lit after its
stop — `Probes.Effects` rows asserted; its puffer/mesh tallies are golden counts under the
suite's own conditions, literal seed 1 + the counting factory, pinned separately from the probe's;
plus the staged-root derivation tripwire at both binds, the crash half on two airframes),
emitter lifetime and the emitter's own modes, texture
flattening, and glTF/collision/node visibility. `emitter-lifetime` is registered FIRST — it is
the only suite installing a fake `IEmitterFactory`, and `WithWorld` caches one world per chapter, so
running first means it builds the shared C1 world while the fake is in effect; `damage-hd`'s
`collision:true` immediately after forces a real rebuild for everyone downstream.
⚠ **Eight suites moved out** (`PLAN-engine-free-suites.md` A3+A4+B11, 2026-08-06): `flight-envelope`,
  `gauge-colours`, `gauge-arrow-tween`, `weapons-defs`, `weapon-blast`, `markers-rig` (A3),
  `stunt-gates` (A4, once `StuntMission` itself went engine-free in A2) and `stall-warning` (B11,
  once its `StallLamp`/`ArrowSweep` cues became plain `GaugeCluster` structs) are now `CSVM.Tests`
  facts calling the same `Probes.*`/plain statics/`StuntMission.Load`/`GaugeCluster.StallLamp` —
  this registry shrank from 32 to 24.
⚠ **`loadout-bind` does NOT split, correcting the plan's Decision 1.** `Probes.Loadouts` — the
  function the plan's A1 classification called "pure" — calls `StockLoadouts.Load()` (its
  no-arg default reads `res://data/stock_loadouts.json` through `Godot.FileAccess`) and then
  `PlaneBuilder.Build` for every stock plane to resolve `Loadout.Bind`'s markers; both are
  native-backed and crash the xUnit host exactly like `tex-dropin`'s `Image` calls (verified
  empirically, A4, 2026-08-06: an off-engine call into `Probes.Loadouts` throws
  `AccessViolationException` at the `Godot.FileAccess.FileExists` call, before `PlaneBuilder` is
  even reached). Binding inherently needs a built plane — there is no engine-free half to extract
  without reimplementing `Loadout.Bind`'s marker resolution, the "never re-implement a check" trap.
  `loadout-bind` stays whole here, unlike the classification table's clean-mover/splits framing.
  The already-off-engine half (`StockLoadouts.Load`'s own JSON parsing, `PylonFillOrder`) was
  already covered by `CSVM.Tests/LoadoutTests.cs` in A3.
⚠ Expected numbers are **golden counts against the retail install** (48 weapon defs, 11 airframes,
  per-chapter destructibles); change one only with the measurement that moved it.
⚠ `bounce-launch` asserts a **band**, not a time: the launch draws speed and elevation per instance,
  and the draw moves with suite order (the same run gave `part4` 4.083 s filtered and 3.883 s in the
  full sweep). Its zero-miss checks are carried invariants the seed does not discriminate — the
  suite is shown able to fail on the solve and the dispatch only (BL-240). It also samples
  `MotionSet.OwesBounce` on every tick of the `refuel*` kill's flight (D11): the retirement hold's
  own mechanism, which the zero-miss checks cannot catch — sampling once right after `DamageAt`
  reads false regardless, since the death's debris motion is scheduled seconds in.
⚠ `effect-template-mesh` builds REAL geometry — `WorldEffectsFactory.BuildEffectStage` from the
  chapter gamez (`TestWorld.Gamez`) into one `pool0` slot, under an `AnimRuntime.ForEffects` runtime
  whose sealed stage carries the production `Shown`/`Pooled` pair. Named empty nodes cannot
  express mesh visibility, which is the whole subject; and the reveal exists only under those flags,
  so a suite that dropped them would assert nothing. Both halves are asserted (a CALLED template
  showing, an ended effect leaving nothing lit) because either alone passes a broken runtime.
⚠ `puffer-modes` detaches `GameClock.Current` for its duration and drives `_Process` itself. The
  harness's clock is a FixedStep one nothing steps, so `FrameDt` is 0 — left installed, every tick
  advances no sim and every check passes vacuously.
⚠ `wait-for-completion` runs `player_crash_water`'s `destroy_crash` — the install's ONE clean
  discriminator, eleven events of which exactly one carries the flag — on a crash-rig-shaped runtime
  over real gamez node names, and asserts the gap (measured 3.050 s against the authored 3.0 s)
  TOGETHER with the unflagged `call_crash_trails`/`large_10sec_fire` beside it staying at t=0. Both
  halves, because either alone passes a broken runtime: with the hold deleted the gap reads 0.000 s;
  with the flag test dropped (every call held) the sequence wedges on its first event and neither
  callee becomes an instance at all. It also asserts `WaitsAbandoned == 0` — a hold that ended at
  the ceiling instead of at its callee would otherwise look like a working wait.
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
  calls `GetTree().Quit(code)` itself. `RunEffectsTest` likewise takes the camera, the caller's
  `EffectAnimNames` table (`EffectCatalogue.EffectAnimNames`, passed in per call) and the
  template stage (`WorldEffectsFactory.EffectStage`).
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
`--perf` / capture tick at priority -999). Both audio levers are master-*bus* writes from here,
because only `FlightAudio` has a gain to scale and `WorldSounds` would sound through any factor
threaded through the other path: `SetFocusMuted` owns the bus's mute FLAG (alt-tab), while
`ApplyMasterVolume` writes its VOLUME once per launch from `--volume=`, else the `audio.volume`
config key. Separate properties, so neither disturbs the other — and unlike `--mute`, a zero volume
still loads and plays everything, so the sound counters and log lines stay intact.
The default root is export-aware: editor (and editor-run builds) → the repo checkout
(`res://`'s parent — `GlobalizePath("res://")` maps to disk only there), exported build → the
exe's own directory; `CSVM_DATA_ROOT`/`--data-root=` override either.
`LaunchSession()` instantiates a `GameSession` per
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
`LiveryResolver`). Also the plain `IFlightStarts`: `ChooseStarts` just loops its own `ChooseSpawn`,
which is the placement every session flies except a splitscreen race. `RaceGrid` delegates to
`ChooseSpawn` for its anchor, and the weapon lab and freecam spectator call it directly, so this
type stays the single owner of spawn resolution.
⚠ `ChooseSpawnBase`'s random branch draws from `Rng.Stream(Rng.Spawn)` — under `--det` this is
  pinned by the master seed same as before the move; do not reorder relative to other RNG draws.
⚠ **The `_spec.SpawnAt` override branch is tested BEFORE the list branch** — that ordering is the
  whole reason `--pos` beats the mission spawn list, and `RaceGrid` inherits the override for free
  by delegating rather than reimplementing. Do not move it.

## src/Session/IFlightStarts.cs
Where every pilot in a session starts: `ChooseStarts(spawns, missionZrdrPath, spawnBase,
playerCount)` returns one `FlightStart` — the same `(pos, lookAt)` pair `FlightController.Setup`
already took — per player. Two implementations: `SpawnPicker` (the plain per-player walk of the
mission's spawn list) and `RaceGrid`. `FlightRigAssembler` holds the interface and resolves the
field lazily on its first `Assemble`, so the resolve still happens where it always did.
⚠ **Whole-field, never per-player — the shape is the point of the seam.** A centred fan needs the
  player count before any slot is known, and a field lifted as one by its worst slot needs every
  slot probed before *any* answer is final; a per-player signature was rejected because it forces an
  implementation to accumulate state across four calls and leaves player 1's answer wrong until
  player 4 has asked.
⚠ **Ascending player order is a contract, not an implementation detail** — the spawn-list index
  wraps on from `spawnBase` per player, and the `spawn [...]` lines are read in player order.

## src/Session/RaceGrid.cs
The abreast starting grid, and the second `IFlightStarts`: slot `i` of `n` sits
`(i − (n−1)/2) × spacing` metres along the perpendicular to the anchor heading, so an even field
straddles the anchor and an odd one puts its middle plane on it. The anchor is
`SpawnPicker.ChooseSpawn(playerIndex: 0)` — delegated, so the ia.json list, objectives.json
`PLAYER_INIT`, the C1 last-resort fallback and `--pos` all keep working without the grid knowing any
of them exist. The heading comes from the anchor's own pos→look-at pair, never the spawn's
`HeadingDeg`: `--pos` carries no heading field and re-reading the list entry would silently ignore
`--direction`. Terrain arrives as an injected `Func<Vector3, float?>` so fan and lift are testable
off-engine; the production closure is `GameSession.GroundSampler()`, which also owns what an empty
probe means. `raceGrid.slotSpacing` (60 m) and `raceGrid.groundClearance` (100 m) are
`Config.GetFloat` **TUNE** values self-registered in `Config.WarmTuningRegistry`, so `--dump-config`
lists them even on a launch that never builds a race.
⚠ **Lift the whole field by its worst slot — never each plane by its own ground.** Per-plane lift
  starts a race at four different altitudes, which is the same unfairness the grid exists to remove
  wearing a new coordinate, and unlike a bad fan no screenshot shows it. Three `CSVM.Tests`
  assertions exist solely to go red on it.
⚠ **Scripted paths bypass this by NOT CONSTRUCTING it** (`GameSession.cs:1422` picks the
  implementation once) — `--det`, solo flight, `--vs` and the zone-less chapters get `SpawnPicker`.
  Never add a bypass branch inside this class: the byte-identical `--det` spawn guarantee is
  structural, and it is not a splitscreen spawner (four dogfighters abreast on one heading is an
  instant head-on merge — Dogfight's spacing is `BL-301`'s call).
⚠ **Grid geometry is not photographable** — the panes are chase-cam only, so at 60 m a neighbour
  sits outside the frustum. The per-slot `spawn [Pn grid slot i of n] … spacing= lift=` line is
  therefore the primary field instrument, and anything the grid is judged by has to be readable in
  it. A uniform lift *is* visible, as one shared HUD altitude across the panes.

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
approximation and the user must be able to move it without a rebuild. Three sections, three
namespaces: `roots`/`default` (the world stage, per-player scaled), `localCallRoots` (library-root
clones, `BL-253`), and `crashRoots`/`crashDefault` (`BL-288` — the per-player crash rig's
templates; `CrashSlotsFor`/`CrashDepthFor`/`UnknownCrashRoots`, no player term since the rig is
already per-player). The crash sizes are the AUTHORED distinct call-anchor counts read off the
shared damage-stage defs, not TUNE — the file's why lines carry the per-root counts.
`SlotsFor(root, players) = clamp(base + perExtraPlayer × (players − 1), 1, maxSlots)` — the
per-player term is what keeps splitscreen/multiplayer from collapsing back onto one copy, since every
extra aircraft is another gun and another rocket landing somewhere else. `DepthFor` is the deepest
root = how many slot containers the stage needs; `UnknownRoots` names an authored root that is not in
`WorldEffectsFactory.EffectStageRootNames(program, gamez)` — the DERIVED stage set, since B3 — as a
typo would otherwise size nothing silently. Asserted twice, because that set is now chapter data: in
`EffectPoolsTests` against C1's bound program (an `ExtractedDataFact`, skipped without an
extraction) and as an `effects-census` condition on whatever chapter the run was given.
⚠ `Parse` (bytes → sizes) is deliberately separate from `Load` (file IO + engine warnings): the
  sizing decision is pure and unit-tested without a session, and a missing or malformed file warns
  and falls back to `EffectPools.Fallback` rather than failing the launch — the same policy `Config`'s
  `const` defaults have. Keep the fallback values in step with the shipped file.
⚠ `Load` reads a `res://` path through `Godot.FileAccess` (the pck copy in an exported build —
  `GlobalizePath` + System.IO cannot see inside the pck, B11 2026-08-05) and an explicit disk path
  through System.IO (unit tests run without a Godot runtime; a native call there crashes the host).
⚠ The three gun-impact roots are sized **1** on purpose (C8 throttles the family to one play per
  0.1 s per name and bounds each to 0.3 s): pooling them buys copies nothing uses. Raising them
  belongs with removing that throttle, which is its own step with its own emitter-count check.

## src/Session/FlightRigAssembler.cs
Assembles one player's flight rig: the painted plane model, the `FlightController` and everything hung
on it — loadout/ordnance, compass, gauges, HUD font test/weapon readout/reticle, damage visuals,
audio, the throttle-slam exhaust smoke (`ThrottleSlamSmoke.Build`, after `Setup` so it can seed its
climb tracker from the live spawn throttle), this player's stunt run + marker/scoreboard/race entry
(or, in `--vs`, its `VersusHud` bound to `Inputs.VersusMatch` + `Inputs.Rigs` for the opponent
markers — C23/C24), the spawn placement, and the crash runtime built after the controller joins
the tree. Constructed
once per session build from
`(SessionSpec, LiveryResolver, SpawnPicker, WorldEffectsFactory, worldRoot, Inputs)`, then
`Assemble(pi, rig)` once per rig; `MeshInstances`/`WhatSuffix` accumulate across the rigs for the
caller's build summary. `--weapon-lab` sets `FlightController.Held` on every rig right after `Setup`
(which places the plane) — the pin is captured at the first held sim step, so it takes the spawn pose
— and binds `Loadout.ForRig` (every firepoint + every pylon, seeded from the same stock fit) in place
of `Loadout.Bind`, so the lab panel can arm a mount the stock file never names. `Setup` itself already
called `Respawn` (which plays `startprops` on `CrashRuntime` if one exists) before the crash runtime
is built below — so this method plays `startprops` once more right after
`BuildFlightCrashRuntime`, the only way the very first spawn's choreography is not silently skipped.
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
⚠ **The `DamageEffectSink` closure also arbitrates node ownership between damage-stage anims and
  other per-frame systems** (BL-287): playing `player_fuelleak` through the rig runtime calls
  `controller.WingLights?.Suspend("wing_flare2")` right after, so `WingLightBlinker`'s 1.5 s cycle
  stops re-asserting a flare the leak just turned off. A future damage-stage anim that contests
  another per-frame-owned node belongs here too, named explicitly — not a generic "did someone else
  touch this" scan.

## src/Session/EffectCatalogue.cs
The record of which authored anims are playable effects, and what their defs need staged: the name
tables every effect producer must stay inside, static and engine-free. Owns `EffectAnimNames` (the
33 impact/destruction/graze names, with the curation comments naming every exclusion —
`b_steamtrail`, `random_gun_impact`, the LOCAL_CHOREOGRAPHY fail-closed set — as the load-bearing
knowledge), the crash-rig's own name sets (`CrashDefNames`: `player_crash_dirt`/`_water`;
`PlaneDamageEffectAnims`: the four `<part>_damage_effects` shims; `PropChoreographyAnims`:
`startprops`/`stopprops`, played directly by `FlightController` rather than through a CALL;
`PlayerDamageStageAnims`: the authored damage-stage menu `pdpanelN`/`player_fuelleak`/
`player_damage_trail` DamageVisuals plays as injure_anims thresholds cross, `BL-259`), and the pure
`TouchdownFor(SurfaceClass)` the graze reaction's three-way pick routes through.
`GroundSplashAnimNames` (`flydirt_plane`) names the crash's ground-splash def for
`AnimRuntime.InheritedVelocityExempt` (`BL-274`) — its `ObjectMotion` is authored the same
vertical-only shape as a launched wreck piece, so only the name can tell "stay planted" from
"scatter with the crash's momentum" apart.
`CrashSurfaceLevelAnimNames` (`plane_big_splash`, `plane_big_ripple`, `hg_splasher`,
`flydirt_plane`) names the crash def's own sub-effects that belong flat on the struck surface for
`AnimRuntime.LevelPlacedTemplateNames` (`BL-292`) — the water splash's spray column/rings and the
dirt burst's dust plane; the fireball/smoke/debris family and `large_steam_spray` are deliberately
excluded (own comment carries why — `TemplateStage.PlaceOn`'s ⚠ in `AnimRuntime`'s entry has the mechanism). It also derives what
those names need staged: `StageRootsFor(program, names, resolveRoot)` walks `AnimProgram.Subset`'s
CALL_ANIMATION closure, takes each reached definition's NAME (the gamez node its instance anchors on),
and resolves it three ways — a parentless gamez root is **staged**, a name the bind's own scope
already carries is **in scope**, a name that resolves nowhere throws `EffectAnchorException` naming
the anchor and the animations on it. `resolveRoot` is caller-supplied, so the world-effects and
per-player crash binds share the one function (`WorldEffectsFactory.StageRootResolver`); the result is
sorted, so no caller's staging order can depend on definition load order.
**This derivation IS the stage's source** (B3, 2026-08-05): `WorldStageRoots` (the closure of
`EffectAnimNames`) and `CrashStageRoots` (the closure of `CrashRigAnimNames` — both crash variants
plus the four damage shims) are what `WorldEffectsFactory` builds a copy of, per pool slot; the two
hand root-tables are gone, and an unstageable anchor now fails the build instead of leaving a def
anchored on nothing.
`WorldEffectsFactory` consumes these names to build and stage the runtime; it no longer owns the
naming itself. Every producer of a name here — `ImpactOutcome`'s gunhit lookup, `TouchdownFor`,
`PlaneDamageEffectAnims` against every plane's real `injure_anims` data — carries a producer-range
unit tripwire in `CSVM.Tests` (`ImpactOutcomeTests`'s B5 battery, `EffectCatalogueTests`) asserting
its whole producible range resolves inside `EffectAnimNames`/`PlaneDamageEffectAnims`.
⚠ Strings, not typed entries, cross every play seam (`GrazeEffectSink`, `ExternalEffect`,
  `ProjectilePool.EffectSink`) on purpose — a typed catalogue entry would thread this module's types
  through the deliberately engine-free `ImpactOutcome`. The producer-range tripwires close the drift
  a typo would otherwise open, at far less churn than four delegate signatures.
⚠ **Anchors the closure reports that are knowledge the mechanical walk cannot derive** are curated
  out before `resolveRoot` is asked: a definition every call re-sites with `AT_NODE` onto a subtree
  that already supplies it, or whose own `Play` caller always supplies the anchor directly
  (`CallSuppliedAnchors`: `zep_can_dstry1.flt`, absent from C2's gamez entirely; `warhawk`,
  `startprops`/`stopprops`' own NAME — a shared authoring label no real airframe carries) and one
  authored against the Devastator's own model root (`AirframeScopedAnchors`: `player_pfighter`).
  Extend those lists, never the walk (`PLAN-effect-catalogue` B2's Outcome).

## src/Session/WorldEffectsFactory.cs
Builds the impact/destruction effect stages and the per-player crash runtime: the world-effects runtime (D32) and
`BuildFlightCrashRuntime` — which despite the name binds every def that plays ON one aircraft: BOTH
crash variants' closures (`EffectCatalogue.CrashDefNames`: `player_crash_dirt` + `player_crash_water`;
the surface is only known at impact, so both are bound and `FlightController.ClassifySurface` picks)
**plus** `EffectCatalogue.PlaneDamageEffectAnims` (the four `<part>_damage_effects` shims →
`random_gun_impact` → `yellow_sparks_follow`) **plus** `EffectCatalogue.PropChoreographyAnims`
(`startprops`/`stopprops`), because those need exactly what it already has — the
`player` anim root, the plane's own `pdpN` panels as INPUT_NODEs, and a live emitter factory.
Its templates are staged in **pool slots** like the world stage (`BL-288`): sizes per root from
`effect_pools.json`'s crash section (most stay single-copy in slot 0; the per-panel damage-stage
family gets one copy per authored call anchor), the runtime's `TemplateStage` built `Pooled`+`Places`
beside the slot build and handed into `ForCrashRig` sealed, and
the stage's caller-slot assignment pins each call anchor (`pdpN`, `prop1`, `pieceN`) to its own
copy — see `AnimRuntime`'s pool paragraphs for the mechanism and the `damage-template-pool` suite
for the regression shape. `LevelPlacedTemplateNames` is set beside
`InheritedVelocityExempt`, once, from `EffectCatalogue.CrashSurfaceLevelAnimNames` (`BL-292`) — the
named defs only ever play from within a crash sequence, so unlike `InheritedWorldVelocity` (set
per-crash in `FlightController.Crash`, since it depends on the live impact speed/direction) this
needs no per-crash toggle. The
prop choreography's own defs resolve their `staticpropN`/`propN`/`propNb` node names against the
plane model directly (`LOCAL_NODES_ONLY`) — `FlightController.Respawn`/`Crash` call
`CrashRuntime.Play("startprops"/"stopprops", PlaneModel, applyReset: false)` themselves, since
nothing else calls them. The
names it binds now live in `EffectCatalogue` (its own entry) — `EffectAnimNames` covers impact +
death effects, including the 12 gun `*_gunhit` variants (a gun hit plays throttled and
time-bounded, C8), the `DAMAGE_SEQUENCE` stage pair `sputter_black_smoke_obj`/`sputter_fire_smoke_obj`
(root `partial_damage_obj`), and the airframe's three graze reactions
(`touchdown_default`/`_dirt`/`_water`, roots `spark_touchdown`/`dust_touchdown`/`splash_touchdown`
+ `yellow_spark_01`, played by `FlightController.GrazeReaction` through `EffectCatalogue.TouchdownFor`).
This module still does the staging: `Subset` handles 8/30 destruction targets; 22 live-object
choreography names remain local (`analysis/death-effect-closure/`), and the stage-call closure
excludes C4's train-anchored `b_steamtrail`. Constructed once per session (`_worldEffectsFactory`,
same lifetime as `LiveryResolver`/`SpawnPicker`) from
`(SessionSpec, Node3D worldRoot, Func<Vector3> playerPosition)`.
The effects runtime's puffer factory passes `softParticles: false` for MIX-ramp states — these effects
emit at ground-level sites, where the depth fade zeroes fresh dark puffs against the terrain (the
crash-smokeball lesson; the damage-stage smoke measured near-invisible with it on) — and keeps the
soft edge for additive fire. Templates build with collision suppressed and the stage is visible with
each ROOT hidden (`TemplateStage.Shown` reveals one while an effect plays on it), so a
template's meshes render — the rocket's per-type explosion rings, the fireball facades (D31). The
runtime's stage is built here and handed into `ForEffects` **sealed** — `Pooled`+`Shown`+`Places`
as constructor state (PLAN-template-stage A4). Until A4 the last two were written onto the returned
runtime instead, which was the accepted sealing leak; do not re-introduce a post-`ForEffects` write.
The world-effects stage is built in **pool slots** (`BL-225`): each root is staged in as many copies as
`EffectPools` sizes it for this session, one copy per `pool<N>` container stamped with
`AnimRuntime.PoolSlotMeta`, so overlapping calls to one effect each get their own copy (see
`AnimRuntime`'s pool paragraph for how a call picks its slot). The containers carry no `cs_name` and
are invisible to name resolution. Sizes are **per root and per player count**, so the deeper slots
hold only the roots sized that deep and a def whose root has no copy in its slot falls back to one
that exists (`TemplateStage.RootsFor` picks by modulo — never "all of them", which would be the collapse
again). The build line names the sizes, not just the total, because a bare count cannot say whether a
root someone just re-sized actually got its copies.
`EffectStage` exposes that stage node read-only, for `--effects-test`'s mesh census (`BL-061`) —
a puffer count cannot see whether a template's geometry drew, and the two halves fail independently
(`docs/verification.md` INSTR-11).
⚠ **This module owns no root list any more** (`PLAN-effect-catalogue` B3, 2026-08-05).
  `EffectStageRoots`/`EffectTemplateRoots` are deleted; both binds stage what
  `EffectCatalogue.WorldStageRoots`/`CrashStageRoots` derive from the very names they are about to
  bind — `EffectStageRootNames(program, gamez)` and `CrashStageRootNames(program, gamez, rigScope)`
  are the two thin forwards, and `BuildEffectStage` takes the roots it is handed. The failure the
  tables carried in their own comments (a def anchors on the node its NAME names, so an omitted root
  leaves it unanchored and it plays nothing, silently — staging 19 of 28 cost the rings, all four
  trail columns, the sonic puffs and the torpedo ripple) is now impossible to reach by omission: an
  anchor that resolves nowhere throws `EffectAnchorException` naming the def and the node.
  `EnsureWorldEffects` turns that throw into its "runtime could not be built" warning carrying the
  anchor list; the crash rig lets it propagate. Do not re-introduce a hand list "to pin the order" —
  the derived list is sorted, and the goldens proved staging is order-insensitive here (13/13
  hash-identical across the switch from authored order to sorted).
⚠ **Runtime ownership stays split, by design.** The factory's own `_worldEffects` field is the ONE
  lazily-built world-effects runtime (`EnsureWorldEffects` builds it on first demand and caches it
  there); `GameSession` no longer mirrors that reference — the runtime node hangs under `_worldRoot`,
  so freeing the session node on `ReturnToMenu` frees it too, and the factory itself is discarded and
  rebuilt fresh next `StartSession`, same as `LiveryResolver`/`SpawnPicker`. Do not add a
  `GameSession`-side cache of the runtime "for symmetry" — it would be a second place to keep in sync
  with the factory's. `BuildWorldEffectsRuntime` is `private` **for exactly this reason**
  (`PLAN-deepening` F17, closing `BL-232`): it used to be public and `GameSession` called it directly
  from two sites that never populated `_worldEffects`, so a later `EnsureWorldEffects` demand in the
  same session found the cache empty and built a second runtime. `EnsureWorldEffects` is now the only
  way in, and it also wires `ProjectilePool.EffectSink` when a pool is passed (gated on "unset", same
  as `ExternalEffect`) — the wiring `GameSession`'s raw call used to do inline.
  ⚠ **`EnsureWorldEffects` keeps its 6-param signature — folding it was examined and declined**
  (`PLAN-template-stage` B11, no-go 2026-08-07). Its four world params look call-order-dependent and
  are not: `GameSession.cs:665–667` assigns `state.CrashProgram`/`WorldScene`/`WorldRuntime` from
  `session.Program`/`Builder.Scene`/`Runtime`, so all four call sites pass one and the same five
  objects — whichever caller populates the cache first, it does so with identical arguments. Moving
  them onto the factory is blocked by construction order: it is built at `GameSession.cs:249`, long
  before the world exists, so the fold is either a two-phase `Bind` (which re-expresses the ordering
  dependence — the damage lab's site is a lambda fired on first damage, so the contract becomes
  "attach before the first demand", failing as a null-ref) or a move of the factory's construction
  past the world build, forking it across the world/empty-stage/plane-only paths and breaking the
  one-per-session lifetime above. The `BL-232` defect was the cache-population hole, already closed
  by F17's `private`. The version worth doing one day is bigger and needs its own plan: the crash rig
  re-takes the same four objects through `FlightRigAssembler.Inputs` (`GameSession.cs:1402–1409`), so
  a factory owning the world's build inputs would shorten two signatures — and it hits the same wall.
⚠ `BuildEffectStage` and `BuildCrashAnchorSet` are `public static` (no session state) —
  `GameSession`'s anim-lab stage calls them as `Session.WorldEffectsFactory.X`, not through the
  instance. The lab builds its crash-anchor set FIRST and parents it AFTER the templates, so it can
  derive its roots against that scope without changing the stage's child order. `EffectAnimNames`
  moved to `EffectCatalogue` — `--effects-test`'s `ProbeRunner.RunEffectsTest` and the
  `effects-census` suite now read it from there.

## src/Session/WeatherRig.cs
Loads/applies the flown mission's weather and drives its per-frame rig state: `LoadWeather`/`SetupWeather` become
`Build`, and the per-rig skydome/whiteout/deck update block from `_Process` becomes `Tick`.
Constructed once per session (`_weatherRig`, same lifetime as `LiveryResolver`/`SpawnPicker`/
`WorldEffectsFactory`) and discarded with the session node on return-to-menu — its per-rig nodes
hang under `_worldRoot`, so the `QueueFree` of the session frees them; `_Process`'s `_weatherRig?.Tick`
null guard covers the frame before that deferred free lands (it can never be null mid-session).
⚠ **The horizon (skydome) build loop stays on `GameSession`** — it's a `SceneBuilder` concern, not
  weather state. `Build` takes it as a `buildDomes` callback, invoked between resolving the zone and
  applying fog/whiteout/precip, at exactly the point the original inline code ran it — do not
  reorder `Build`'s three steps (zone → domes → setup) relative to each other.
⚠ **The ambient cloud field is not here** (`BL-273`, 2026-08-06). It is chapter data — `fogvol.zrd`
  plus the gamez `fvol*` boxes — not mission weather, it is world-anchored rather than per rig, and
  it needs no `Tick`; `GameSession` builds it beside the world under the same fly/freecam/sky-zone
  gate. Until then a hand-tuned per-rig `CloudPuffs` lived here, keyed off `CLOUD_COVER`, and
  `PlayerRig.Puffs` is gone with it. See `Effects/FogVolumeClutter`. **`Tick` does own whether each
  camera SEES it** (`A7`, below) — that is a per-view cull-mask decision about shared geometry, not
  ownership of the geometry, and it must not become a reason to rebuild the field per rig.
`Build` also takes the chapter's `WorldBuilder.HorizonZones()` census, because `LoadWeather`
resolves the rendered zone from BOTH the mission's zone names and the horizon's geometry
(`BL-277` — see `Flight/Weather.cs`). `_activeZone` is the single answer both the fog and the dome
are built from, and it is logged with the meshed counts it was decided on.
⚠ An explicit `--sky-zone=` skips the geometry correction and is honoured literally, empty dome and
  all (`SkyZoneExplicit`) — it is the flag for looking at a named zone, and `analysis/`'s recorded
  repro poses depend on it. The weather-file fallback still applies to it, as before.
⚠ **`GlobalShaderParameterSet`, never `Add`.** `GlobalShaderParameterAdd` runs once per process in
  `Launcher._Ready`; `Build`'s fog/whiteout writes must stay `Set`-only, or every in-process menu
  relaunch that flies a second foggy mission crashes on the duplicate `Add`.
⚠ `SetDeckCenter` is called separately from `Build`, whenever a chapter's cloud deck geometry loads
  (`GameSession`'s `cloudDeck != null` branch) — broader than "this rig has weather", so it is
  guarded with `_weatherRig?.SetDeckCenter(...)` rather than assumed non-null.
⚠ **The deck is ENGINE TRICKERY in two regimes, not a placed sheet** (`A7`, 2026-08-08 — decoded
  by the user at the controls of the original). `Tick` splits at the `CLOUD_COVER` band centre
  (`WeatherState.CloudBandCentre`): **below** it the deck is a ceiling carried with the camera in
  ALL THREE axes at `camera.y + DeckCeilingHeight`; **at/above** it a world-fixed floor sitting on
  the band centre, still following in X/Z. The rule is the pure `DeckRegime(cameraY, bandCentre)`,
  which also answers whether that camera renders the ambient clouds — assert against that, not
  against the loop. Below-band consequence, and it is the item's own evidence: the sky is
  BIT-IDENTICAL at 192/300/600/900 m, which is what "the texture looks the same at every altitude"
  means and what a world-fixed sheet cannot do (the pre-A7 build moves 87 % of those pixels).
  Above-band consequence: the pinned above-deck pose renders bit-identical to the pre-`A6` pin,
  because that pin WAS the above-band half of this trick applied in both regimes.
⚠ **The regime flip is a JUMP of `DeckCeilingHeight`, masked only by the whiteout core.** It is
  placed at the band centre precisely because that is the middle of the fully-opaque core
  (C1: total in 1032–1062) — measured: the ladder frames at 1035/1046/1048/1060 m are bit-identical
  flat white. Moving the flip altitude, or thinning `CLOUD_COVER`'s `THICKNESS`, makes it visible;
  if a pop ever shows, that is a finding about the whiteout band, not a licence to move the flip.
  `DeckRegimeTests` asserts the masking against the AUTHORED band, so the data moving fails a test.
⚠ **`DeckCeilingHeight` (400 m) is a TUNE matched to one original still, and it is the only free
  parameter in the model** — derived by apparent mottling scale from
  `OriginalScreenshots/C1 IA1 Fog river.png` (the constant's own comment carries the method and the
  260–590 m bracket; it scales with the assumed FOV). One value for every deck chapter: C1's river
  still is the only original frame that can measure one.
⚠ **The cloud gate is a per-camera CULL MASK over `UI.SplitScreen.CloudFieldLayer`, never node
  visibility.** Both ambient populations — the `fvol` clutter MultiMeshes and the world's placed
  `cloudparent` clusters — are moved onto that one shared layer by `GameSession`; hiding them as
  nodes would take them out of every splitscreen pane at once, and two players routinely sit on
  opposite sides of the band. `Tick` writes each rig camera's own mask.
⚠ **A chapter with NO deck mesh never arms the gate at all** — the whole block is inside
  `rig.Deck != null && _weather is { HasCloudBand: true }`. C5 is why: it has 16,170 clutter
  sprites, no deck, and a band at 9950–10150 m no one can reach, so an unguarded gate would hide
  its street haze at street level for ever (measured: 161,541 cloud px at street level; C1B, no
  deck and 70 `cloudparent`, 473,915). **The other half of that guard is
  `GameSession.BuildRigs`**, which re-adds the layer to the main camera's mask at session start:
  that camera is the Launcher's and outlives the session, `Tick` only ever CLEARS the bit, and a
  chapter that never arms the gate never sets it back — so without the reset, quitting a C1 flight
  from under the deck would hide the NEXT flight's clouds.
⚠ The deck and the `fvol` field are the SAME sheet seen from two sides, so they are read together:
  the deck mesh is what an underside view shows and the sprite field is what a view from above
  shows. Any change to either one's altitude has to be checked against the other's
  (`Effects/FogVolumeClutter`, `docs/formats/fogvol.md`). ⚠ But the deck's RENDERED altitude is now
  neither chapter's authored one — the authored 960/1050 is what the scatter is read against
  (fogvol.md's mesh-10 m-under-the-slab invariant), not where the mesh is drawn.

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

## src/Session/ExtractionStamp.cs
Reads the provenance stamp `ExtractAssets.ps1`/`ExtractRof.ps1` leave at `extracted/VERSION.json`
(unzbd version line + exe SHA-256 + fork commit, dates, schema integer) and compares the schema
against its `Schema` const in `Launcher._Ready`, right after the base paths settle. At most ONE
warning line per boot — stale schema, missing file, or unreadable — each naming the fix (re-run
the extraction scripts). Warn, never block: the dev tree holds valid extractions predating the stamp.
⚠ `Schema` bumps together with `$StampSchema` in BOTH scripts, in the same commit as any reader
  change that invalidates old extractions — a hand-maintained promise, not automation.
⚠ The stamp is parsed via `File.ReadAllText`, not bytes: PowerShell 5.1 writes UTF-8 WITH a BOM,
  which `JsonDocument.Parse(byte[])` rejects.

## src/Flight/CollisionLayers.cs
The named physics collision layers — world (layer 1, the engine default every pre-existing
collider sits on implicitly) and aircraft (layer 2, `AircraftBody`) — plus the combined mask.
The first and only place a layer bit is assigned a meaning; new layers go here, never inline.
⚠ Godot's default query mask is ALL layers: a query that should not see planes must say
  `CollisionLayers.World` explicitly (weapon-lab picks, the pool's fuse/blast spheres do).
⚠ Nothing assigns `World` to world colliders — they carry it by engine default. Assigning it
  everywhere would be churn for zero behavior; the constant documents the meaning instead.

## src/Flight/AircraftBody.cs
The flying aircraft's physics body: one `AnimatableBody3D` child of `FlightController`, one
`CollisionShape3D` per `PlaneCollider.Part` reusing the SAME `BoxShape3D` + local transform the
terrain sweep casts, on the aircraft layer. Rides the controller's transform; `PartName(shapeIdx)`
maps a query's struck shape back to the part (shapes added in `Parts` order); `ExcludeSelf` is the
cached one-entry RID list the owner's own queries pass; `SetHittable` drops it to layer 0 while
crashed, back at respawn. Also the fuse/blast geometry oracle (B14), answering from the same box
set without a physics query: `NearestShape(point)` (nearest box, its skin distance + surface
point — blast falloff), `SegmentDistance(from,to)` (closest approach of a swept round, ternary
search per box — distance to a box is convex along the segment), `BoundRadius` for the cheap
per-step reject, and `TakeProjectileHit(..., damageScale)` scaling both damage magnitudes by the
blast falloff share (1 = direct round).
⚠ The plane stays Node3D-moved by `FlightModel` — `SyncToPhysics` false, `CollisionMask` 0: the
  body is a query target only, and nothing may let the physics engine push plane transforms. A
  plane-vs-plane impact resolves through the striking plane's `SurviveHit`/`Crash`, never a solver.
⚠ Shapes are shared resources, not copies — a fidelity upgrade edits `PlaneCollider`, not this.
