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
- `src/Mech3/ZoneGate.cs` — the original's per-node `zone_id` visibility gate: the rule, its visual-layer allocation, and the per-camera cull mask.
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
- `src/Mech3/MapEdgeExtender.cs` — rolling window of repeated border-cell blocks + clutter continuing the world past the map edge, per camera; block depth is per chapter (`DefaultBlockCells`).
- `src/Mech3/Clutter.cs` — stamps interp.json clutter templates onto matching-textured terrain at the polygon's own UV lattice, gated per polygon by `no_clutter`: sprites, plus C2/C5's solid 3D city blocks.
- `src/Mech3/ClutterTemplates.cs` — the `templates.zrd` reader: each clutter decoration model's authored substitution table, scale range and fade distances, plus the five keys no chapter authors.
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
- `src/Flight/AimAssist.cs` — the gun aim assist (`BL-342`): `GunAimSlot`'s plane-local per-muzzle state and the engine-free forget + catch-up pass (B2), the constant-velocity intercept solver (B3), and the four-list candidate scan with its rejection gates and scorer (B4).
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
- `src/Flight/SpeedCue.cs` — chapter-authored pale smoke wisps emitted 60 m ahead of each player, density selected by camera altitude.
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
- `src/Effects/WorldWind.cs` — the mission's global wind (weather.json's `WIND` block: static vector + horizontal random-walk gust) and `EffectAmbience`, the per-session seam a `Puffer` reads it through.

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
- `src/UI/TileGridOverlay.cs` — the map-edge tile-grid overlay (F14, `--debug-tilegrid`): every ground tile tinted 20 % by repetition band, so one colour band is one block; F15 steps the block depth, F16 swaps repeat/mirror. The instrument that settled the map-edge fold.
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
Carries the node's `zone_id` as `ZoneId` (default −1 when the field is absent, i.e. ungated) — the
original's per-node visibility zone, honoured per node by `SceneBuilder` and per camera by
`Mech3/ZoneGate.cs`.
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
`VertexColorsRestateMaterialColor(poly, materialIndex)` spots the redundantly-duplicated flat
colour: an untextured (`Colored`) material whose colour every one of the polygon's vertex colours
repeats. That is ONE authored value in two slots, so SceneBuilder applies it once instead of
multiplying (which squares it — 176 → 120). 87 polygons install-wide, 85 of them the skydome
skirts, censused per chapter by `CSVM.Tests/FlatColorTests.cs`.

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
that one method. `BuildSubtree`'s `zoneGate` flag (off by default; on for the world walk and the
map-edge extension) moves each built mesh instance onto its OWN node's `zone_id` visual layer
(`Mech3/ZoneGate.cs`), counted into `ZoneGatedMeshes` — per node, never inherited down the subtree,
because the data puts parents and children on different zones. It stays OFF for the cloud deck and
the skydome, which are per-rig camera-anchored copies gated by `Node3D.Visible` instead.
`CollidersForMesh`
splits a mesh's colliding geometry into one trimesh per surface class actually present
(water/buildings/untagged, each polygon's own texture deciding) rather than forcing the
whole mesh under one dominant-class vote — a coastal tile is mostly beach by area, so the
area-quorum vote it replaced gave real water polygons on it to `default` outright
(`analysis/surface-classification/`). Each collider-bearing node is registered with
`WorldCollision`, which owns its `Disabled` flag from then on. A `GameZ.IsMarkerGizmo` mesh draws nothing, but its Node3D is
still built with its transform — animations attach puffers and sounds to those nodes by name.
A surface's colour is `vertex colour × material` (the original's baked-lighting modulate) — except
where the two are the same authored value, which `GameZ.VertexColorsRestateMaterialColor` detects
and `EmitPolygon` answers by writing white corners, so the value lands once. `PLAN-overcast-match`
`B18`: every skydome's below-horizon skirt is an untextured polygon authored in its zone's own
`FOG_COLOR`, and squaring that is what made the horizon join a hard band in seven of eight chapters.
`DebugClutterFlag` (`--debug-clutterflag`, set by the caller before building) is the one thing that
overrides that colour: `EmitTriangle` writes the polygon's decoded `no_clutter` bit as the vertex
colour (red flagged / green clear) and the fullbright shader's last ALBEDO write becomes
`ClutterFlagTintLine` instead of `TintLine`, since C5's night art would swallow a tint. Per polygon
because the flag varies WITHIN a mesh, which no per-instance tint can express; the tint mix survives
in that line so `WorldSession` can still force the clutter populations blue per instance. Off — every
other builder, and the shaded aircraft path always — emits `TintLine` itself, so the default shader
text is byte-for-byte unchanged. `FlaggedPolygonCount`/`ClearPolygonCount` count per BUILT MODEL, not
per placement.
⚠ Instance-uniform block is an ORDERING CONTRACT — every shader on one instance declares the same
  block (csky_instance_uniforms); a shader with NO instance uniform must not take the preamble
  (16-vec4 per-instance buffer cost). The model's `lighting`/`fog` flags therefore select shader
  VARIANTS (and join the material cache keys) rather than adding a uniform: a lit, fogged surface
  keeps byte-identical shader text, so honouring the flags cannot perturb the rest of the world.
  `BuildSubtree`/`GetMesh`/`BuildMesh` all carry a `forceLit` override beside `forceDoubleSided`
  (`PLAN-overcast-match` C22, `WorldBuilder.Add`'s deck path): `bool lit = mesh.Lighting ||
  forceLit` still only PICKS an existing lit/fogged variant, so this composes with the ordering
  contract rather than working around it. Both overrides join the mesh cache key
  (`(Model, Force, ForceLit)`) because sidedness and the lit choice are baked into the built
  surfaces, not read per frame. That baking is why a RUNTIME lit-ness change has to be a mesh
  swap: `SharedMesh(meshIndex, forceDoubleSided, forceLit)` takes the same two flags and hits the
  same cache, so a caller can hold both variants of one model and assign either onto a live
  `MeshInstance3D` — the deck's regime-conditional dimming (`PLAN-overcast-match` C23,
  `Session/WeatherRig`) is exactly that and needs no second build of the deck.
⚠ UV scroll reads the `csky_time` global, never Godot's `TIME` — it must keep TIME's 3600 s wrap
  because every install rate (0.07/0.4/0.5/0.7/1.0) × 3600 is a whole number of texture repeats.
⚠ `BuildSubtree` sets the built root's transform from the node's OWN `Local` — a caller slicing a
  nested node must overwrite it with `GameZ.WorldTransformOf` or it lands at its parent's origin.
⚠ A polygon's overlay passes become their own surfaces, appended after every base group, ordered by
  `OverlayPassBias` and NOT by surface rank — 97 of the 307 overlay-bearing models are already at
  the rank cap, where an appended group would share its base's rank and z-fight it. Declined on
  sprite/facade meshes (no biasable material); `OverlayPassDeclinedCount` is the tripwire and is 0
  across the install.
⚠ `BuildFlatQuadMesh` (`PLAN-overcast-match` C26, `WorldBuilder.AddDeckAnnulus`) is the one caller
  that builds geometry with NO gamez node behind it at all — raw world-space quad corners into a
  `SurfaceTool`, given the SAME `GetMaterial(-1, …)` no-texture branch a `Colored` polygon with an
  out-of-range material index gets, so it shares the ordinary bias-shader fog/lighting pipeline
  (`fogged: true`, `lit: false`) rather than a hand-rolled second one. `materialIndex = -1` is a
  deliberate reuse of an existing fallback path, not a new one.

## src/Mech3/ZoneGate.cs
The original's per-node visibility gate (`FUN_0056c430`, `PLAN-weather-decompile-match` B12,
2026-08-09). `FUN_004d62d0` arms the camera each frame with the zone set `{0, camera weather
state}`; the walk draws a node iff its gamez `zone_id` is `-1`, or is in that set. Four members:
`Draws(zoneId, state)` (the rule), `LayerFor(zoneId)` (the visual layer a gated zone's meshes are
MOVED onto — 0 for `-1`/`0`, i.e. leave on the default layer), `CullMask(mask, state)` (narrows the
band to one zone, every other bit untouched) and `OpenCullMask(mask)` (the whole band back —
`--no-zone-cull` and the launcher camera's per-session reset).
⚠ **Per NODE, never inherited down a subtree.** The data puts parents and children on different
  zones — C1's `flaglite1`/`flaglite2` are `zone_id -1` under a zone-1 parent — and the binary
  calls the gate per node during the walk. `SceneBuilder.BuildSubtree(…, zoneGate: true)` stamps
  each node's own mesh instances accordingly, which is also why the gate can never be node
  visibility: a hidden parent takes its children with it in Godot, and a cull mask does not.
⚠ **A cull mask, not `Node3D.Visible`, for shared world content.** Splitscreen panes sit in
  different states at the same instant, and `AnimRuntime`'s `NodeActive` condition + uncovered-
  `destroyed` sweep, `WorldBuilder.HideUnplacedEntities`/`RestorePlacedEntities` and
  `DamageVisuals` all read or write `Visible` on that very content. A visibility gate would answer
  their questions with "the camera is above the deck". The two per-rig camera-anchored copies (deck,
  dome) are the exception and DO use `Visible`, because a per-player visual layer and a zone layer
  cannot share one instance — a cull mask ORs its bits, so it cannot express "this pane AND this
  zone". `Session/WeatherRig.Tick` owns both surfaces.
⚠ **Bits 13–15** (layers 14–16), immediately below `UI.SplitScreen`'s per-player band at 16–19.
  Every cull mask the engine builds starts with all three set, so the gate only ever NARROWS —
  a mode, chapter or camera that never applies it renders every zone.

## src/Mech3/ConflictRank.cs
The world's cross-node draw-order tie-break. Buckets every built triangle by its world plane,
finds the cross-node pairs that are coplanar, same-priority, same-subface and genuinely overlap
(clipped area > 1 m², never an AABB touch), and layers that DAG by longest path — so a node's rank
counts conflicting layers beneath it, not nodes before it. Every edge runs low node index → high,
so the layering is a topological order of the original's own draw order and cannot invert authored
layering. `WorldBuilder.RankConflicts` runs it before the build; 11–45 ms per chapter.
⚠ The step (`SceneBuilder.ConflictRankBias`, 1.2e-5) is boxed in from both sides and is the only
  value that fits: it must exceed `SurfaceRankCap × SurfaceRankBias` = 1e-5 or within-mesh rank
  out-bids it, and `ConflictRankCap × it + 1e-5` must stay under `NoClutterLayerBias` so a
  no_clutter overlay + tie-break keeps inside one priority level. Measured in Godot: 5e-6 leaves a coplanar pair
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
(`BuildHorizon` makes the camera-anchored skydome; every horizon model in every chapter is authored
`fog: false` and, since `PLAN-overcast-match` `B16` (2026-08-08), that flag is honoured like
everywhere else — dome materials build unfogged; its `lighting: false` is honoured too, as before),
`fvol*` (`IsFogVolumeNode`, shared with `FogVolumeSpec.VolumesOf` so the skipped set and the
cloud-scatter set are one list), `dzpaths`.

`B16` reverts the 2026-07 `SceneBuilder.ForceFogged` deviation, which force-fogged the whole dome
on the premise that high fragments would stay clear via the `FOG_ALTITUDE` fade; `B16`'s arithmetic
showed that fade never actually happens at the dome's own authored size (every dome tops out
+982…+4108 m over the camera, well under every reachable `FOG_ALTITUDE` band), so the "high
fragments clear, horizon band greys" deal never delivered and the dome only ever painted flat fog
colour (`BL-303`'s C3/C2B/C5 skies, and the C1 above-deck gray band one zone over). `ForceFogged`
itself is deleted — `BuildHorizon` was its only setter.
`B18` then closed the seam honouring `fog: false` exposed: the dome is a textured wall from local
Y=0 up plus an untextured **skirt** cone from Y=0 down, and that skirt is authored in the zone's own
`FOG_COLOR` so it merges into the terrain's fog wall. It read as a hard band only because its flat
colour was being applied twice (see `SceneBuilder.cs`); nothing about the geometry or the scaling
was wrong, and `ForceFogged` was never needed to hide it.
`HorizonZonesOf`/`HorizonZones` census the `horizon` node's `zone*` children with the meshed-node
count each subtree carries **and that child's own gamez `zone_id`** — read BEFORE the build,
because the zone the dome and the fog share is picked from it
(`Flight.WeatherState.PreferPopulatedHorizonZone`; three chapters ship a `zone2` that is a bare
marker). Static over a `GameZ` so it needs no built scene and is testable off-engine.

**Zone groups (`PLAN-weather-decompile-match` B12).** Every world node the walk builds is stamped
with its own `zone_id` visual layer by `SceneBuilder` (`zoneGate: true` — see `Mech3/ZoneGate.cs`),
so the camera's weather state culls it as `FUN_0056c430` does. Two populations are excluded from
that stamp because they are per-rig camera-anchored COPIES, whose per-player visual layer a zone
layer cannot share: the **deck** (`Add` passes `zoneGate: !isDeck`; its tiles' own zone is
`CloudDeckZoneId`, uniform-or-−1) and the **dome** (`BuildHorizon` never stamps). Both take the
same rule through `Node3D.Visible` in `Session/WeatherRig.Tick`. `FogVolumeZoneIdOf` reports the
zone every `fvol*` node authors — the zone the `FogVolumeClutter` sprite field is gated on, read
per chapter and never assumed: **C2B ships −1** where C1/C1C/C4 ship 2 and C5 ships 1.
`ZoneGatedMeshes` is the per-zone build census, printed as the `zone gate:` line (C1: 1343 / 626 /
0 on zones 1/2/3) so "the gate stopped stamping" cannot read the same as "this chapter authors no
zoned content".
⚠ `BuildHorizon` builds exactly the zone it is handed, INCLUDING an empty one — the selection is
  the caller's, and an explicit `--sky-zone=` is meant to be able to show a bare marker's nothing.
  Its own name-absent fallback (first zone child) stays a no-op in the normal path.
⚠ **`DomeZonesToBuild(zones, activeZone)` decides how MANY domes a world builds**
  (`PLAN-weather-decompile-match` B14, 2026-08-09). The original draws the dome of the zone its
  camera is IN, so a deck chapter needs both present: below the deck its `zone_id 2` dome is culled
  and `horizon/zone1`'s own geometry is the sky *and* the ceiling. The rule is the gate's own
  arithmetic, never a chapter list — a second zone is added only if it builds geometry, its
  `zone_id` is gateable (1…3; `ZoneGate.Draws` passes −1/0 at every state) and distinct from every
  zone already taken, and only if the active zone's own id is gateable too. Result per chapter:
  C1/C1C/C2B/C4 and C5 build two, C1B/C2/C3 build one (their `zone2` is a bare marker). Pure and
  static; pinned against every chapter's real census in `CSVM.Tests/HorizonDomeTests.cs`.
`BuildDzPaths` is its
debug-only custom renderer: material-matched gate polygons green/red at 50% alpha, route as an open
white line strip (never a filled or closed polygon).
Splits the overcast deck into
`CloudDeck` (GameSession moves it with the player); hides origin-parked unplaced vehicles.
`Add` builds every deck tile with `forceLit: isDeck` beside its existing `forceDoubleSided:
isDeck` (`PLAN-overcast-match` C22): the deck tiles author `lighting: false` like the dome and
the `cloudsprite` field, but the deck alone was actually SUNLIGHT-dimmed in the original —
`SunIncidence` was calibrated on this exact texture (`Flight/Weather.cs`) — so `forceLit`
applies `csky_world_light` to the deck regardless of its own authored flag, deck-local, never a
change to the `lighting` gate or to `csky_world_light` itself. C4's deck is unaffected by
construction (its `WorldLight` clamps to 1.0), which is the control that proves the fix is
deck-local rather than a hidden global change.
⚠ That dimming is the BELOW-BAND regime only (`PLAN-overcast-match` C23, user's fork verdict
2026-08-09): the ceiling a camera under the band sees is the overcast's dimmed UNDERSIDE, the
floor a camera above it sees is the undimmed top, and the original's above-band frames contain no
pixel below `FOG_COLOR` at all. `RecordDeckUndimmedMesh` therefore asks `SceneBuilder.SharedMesh`
for each tile's mesh in BOTH `forceLit` variants as it builds — the cache means the dimmed one is
the very resource the built instance carries — and `CloudDeckUndimmedMeshes` publishes the pairs
keyed by the dimmed mesh's `Rid`. `Session/WeatherRig.Tick` assigns one variant per rig at the
band crossing; nothing here decides which. Keyed by RID because a splitscreen session's extra deck
copies (`GameSession.AssignCloudDecks`) are `Duplicate`s sharing these resources.
`AddDeckAnnulus` (`PLAN-overcast-match` C26, 2026-08-09) adds ONE more child under the same `deck`
node once the 144 tiles are built: a flat, untextured four-quad picture frame around
`MergedLocalAabb(deck)` (the tiles' own measured AABB — never a hardcoded origin, so the annulus
stays exactly centred on the tile grid and the eventual `GameSession.AssignCloudDeckIfBuilt`
re-measurement via `OrbitCamera.MergedAabb` lands on the same centre, unperturbed), reaching a
20,480 m half-span. ~~(rim ≈ 3.95 px, `f·K/halfSpan` — see the `Session/WeatherRig` entry)~~
⚠ **That derivation is dead and the half-span now rests on nothing** (`D31`, 2026-08-09): `K`
(`DeckCeilingHeight`) was deleted by `B14` and `B13` made the floor world-fixed, so the far edge's
elevation is `f·(cameraY − 960)/20480` and grows with altitude instead of sitting at a constant
3.95 px — measured 6 px below the horizon at C1 y = 1192 and 33 px at y = 2000 (against 22.6 and
101 px for the bare 6,144 m tile sheet, so the extension is still load-bearing). `BL-328` owns
re-deriving the number; do not reinstate a camera-anchored ceiling to make the old formula fit. Its
material is `SceneBuilder.BuildFlatQuadMesh`'s `GetMaterial(-1, …)` call — the SAME no-texture
`BuildMaterial` branch a `Colored` gamez polygon with no material entry gets, `fogged: true`, so
its `ALBEDO = mix(ALBEDO, csky_fog_color, fog_amt)` line is byte-for-byte the deck tiles' own; every
point it is built for sits beyond every deck chapter's own authored `FOG_RANGES` far, so `fog_amt`
is 1.0 there and one static mesh (`lit: false`) serves both regimes with no dimmed/undimmed pair.
Tagged `WorldBuilder.DeckExtensionMeta` node metadata so it counts toward neither this file's own
"144 tiles" print (which reads `_deckNodes.Count`, not `deck.GetChildCount()`) nor
`WeatherRig.CollectDeckTiles`'s "N of M deck tile(s) carry an undimmed twin" census — both stay
144/144. ⚠ 20,480 m is close to a ceiling, not just a tidy round number: C1/C1C/C2B/C4's zone2
dome renders at 8.74 km × 2.5 = 21.85 km, and this flat sheet must stay well inside that (never
touch the dome) or its outer edge would sit past the dome wall it renders in front of.
⚠ The annulus stays attached to the floor at whatever altitude `Session/WeatherRig.Tick` places the
  deck at (`B13`) with NO code of its own to do it: it is a child of `deck`, built in the SAME local
  coordinates as the 144 tiles (`MergedLocalAabb(deck)`'s own `Y`), so it moves only because its
  parent's `Position` does — the same mechanism that already kept it centred through
  `AssignCloudDeckIfBuilt`'s re-measurement.
`CloudDeckAltitude` (`PLAN-weather-decompile-match` B13, 2026-08-09) exposes `_deckAltitude` — the
same coverage-winning altitude bucket `FindCloudDeck` classifies the 144 tiles by (C1/C1C/C2B 960,
C4 1050) — as the tiles' AUTHORED Y, read off the built data rather than hardcoded.
`Session/WeatherRig.SetDeckAltitude` takes it (`GameSession`, beside `SetDeckZoneId`) so the
above-band regime can place the floor there instead of the `CLOUD_COVER` band centre (`A7`'s pin,
which the decompile showed was C4's own coincidence). `CloudDeckAltitudeOf(gamez, worldName)` is
the SAME computation as a public static function of the raw `GameZ` data alone — no scene build,
no `TextureArchive` — so `CSVM.Tests/DeckRegimeTests.cs` can pin a chapter's authored altitude
against the extraction without paying for a full world build; both share one tile test,
`FlatTileOf` (moved beside `SkipWorldNode` — StyleCop's internal-before-private, static-before-
instance ordering rules pin it there, not beside the instance `FlatTile` wrapper it backs).
`CloudClusters` censuses the OTHER ambient cloud population after the walk — every `cloudparent`
subtree the world places (C1 28, C1B 70, C1C 30, C4 45; C2/C2B/C3/C5 none), logged per chapter so
"none" cannot read like a broken census. They need no layer handling of their own since `B12`: they
are ordinary world nodes, so `SceneBuilder` already stamped each with its own `zone_id` layer
during the walk (C1/C1C/C4 author them `zone_id 2`, C1B `zone_id 1`) — the census survives as the
evidence line, not as a hook.
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
⚠ Repeats a BORDER-CELL BLOCK, never the map interior (whole-map tiling brings the airport back,
  which 10+ minutes of flight past the edge says never happens). `FoldAxis(i, n, block, repeat)`
  folds an outside index into the nearest `block`-deep band; at `block`=1 with `repeat`=false it
  reduces exactly to the pre-2026-08-08 clamp, which `MapEdgeFoldTests` pins.
⚠ **It REPEATS, it does not mirror** — A/B'd against the original at the controls 2026-08-08,
  matching exactly on C1/C2/C4/C5 with no seam gaps. This REVERSED the earlier `CAP-17` strip
  reading, which got both the fold and the distance wrong (post-mortem in
  `analysis/video-flight-calibration/FINDINGS.md`). `--map-edge-mode=mirror` keeps the old
  behaviour to look at. ⚠ Do not size a block from a video-derived period — fly it.
⚠ **`BlockCells` is per chapter** — `DefaultBlockCells`: 2 on C1/C2/C4, 1 on C5 and on the four
  water-bordered chapters (C1B/C1C/C2B/C3), whose borders were measured to be water-only, which
  fixes them at 1 and makes the depth moot. Unknown chapters fall back to 1.
⚠ **A cell's ground is not always ONE tile.** `AdoptComplements` runs after `ScanTiles`: a cell whose
  accepted tiles do not span it adopts the full-cell-spanning FLAT strips `ClassifyGroundMesh`
  refused for being thin. Without it three C5 border cells — a base tile plus a 256–384 m water
  strip completing it — left a hole their own width in every copy: sky, no collision, outward
  forever. ⚠ Keyed on the CELL being short, never on the strip alone: flatness by
  itself adopts hangar floors and rooftops, and the surface class cannot separate them either
  (`cblock*`, the city GROUND texture, classifies as `buildings`). `ClassifyGroundMesh` and
  `IsCompletionStrip` are pure statics pinned by `MapEdgeTileTests`; `--dump-tilegrid` writes the
  per-cell coverage census that settled it — 16 adoptions on C5, 0 on the other seven.
⚠ Extension sprites carry no collider (matching the map); buildings DO — one lazy `clutter_bld_ext`
  body per cell attaches the shared `KindExport.CollisionShape`; 3D kinds mirror as whole transforms.
⚠ The window is the UNION of all player cameras' neighbourhoods — one focus strands the other pane.

## src/Mech3/Clutter.cs
Stamps the boot-script clutter templates across placed polygons carrying the template's ground
texture, **at the polygon's own texture-UV lattice** — one stamp per integer UV repeat across each
triangle; sprites → one fullbright Y-billboard
MultiMesh per kind, solids → `SceneBuilder.SharedMesh`; the split is `SceneBuilder.ClassifyBillboard`.
The sprite shader takes the decoration model's own `lighting`/`fog` flags as variants (every tree and
bush card in the install is `lighting: false`, so clutter does not dim with the mission SUNLIGHT),
plus a UV-clamp variant from `SceneBuilder.UvsWithinUnitSquare` over the kind's own card UVs.
`TemplateNames` reads the chapter's `AddClutterTemplates` list **unfiltered** — which district
dresses a given patch is a per-polygon decision, see the `no_clutter` note below;
`OverrideTemplateNames` is `--clutter-templates=`'s replacement for it — the caller's names,
filtered to the ones this gamez carries a root for — so one district can be loaded alone and A/B'd
against the original. It prints one line naming what was requested, what resolved and what this
chapter does not carry, since an absent name is retail-data-normal and would otherwise read as an
empty district.

**The original's placement runtime is written up in [org/clutter.md](org/clutter.md)** — the
function map, the template lookup's first-match scan, the UV-lattice stamp and its local triangle
frame, the engine defaults no chapter authors, and the weight list's sum-and-divide. Read it before
changing a placement rule; the authored side stays in [formats/clutter.md](formats/clutter.md) and
[formats/templates.md](formats/templates.md).
⚠ **`no_clutter` (raw polygon bit `0x800`, carried as `GameZPolygon.NoClutter`) gates every stamp**,
  reproducing `FUN_004de2c0`. It does **not** mean "leave this ground bare": where two COPLANAR
  layers are painted over each other it selects which one decorates, and flagged means skip the
  overlay so the layer beneath stamps instead. All of C5's city is such a pair — flagged ground is
  dressed by `cblock4/5/6` (low-rise, ≤52 m), clear ground by `cblock1/2/3/7` (towers, ≤108 m),
  measured at odds ratio 1,037× and confirmed at the controls
  (`analysis/bl-305-clutter-uv/FINDINGS-layer-pairing.md`). Reading it as "no clutter here" and
  dressing flagged ground with towers is `BL-305`, and the map-wide `BuriedClutterDistricts`
  exemption that stood in for this gate is **gone** — do not reintroduce either half alone: the
  gate without the districts empties C5's downtown, the districts without the gate double the city
  (`BL-250`).
⚠ **`substitute` and `scale_range` ARE applied (C22); `far_fade_range` is not.** The per-kind data
  comes from `Mech3/ClutterTemplates.cs` (docs/formats/templates.md), handed in at construction —
  a null spec rebuilds the pre-C22 monoculture at authored size, which is what a caller with no
  reader gets. `translate_uv_range`, `rotation_range` and `align_normal` are authored by **no
  chapter in the install** — inert, not missing, and that is precisely why the original's placement
  has no random input affecting position or orientation, and why C1's tree positions come out
  *exactly* the original's (confirmed at the controls). `far_fade_range` (143 blocks) stays
  unapplied by Decision 3 of the plan; C23 owns it.
⚠ **The substitute/scale stream is seeded with a FIXED constant, deliberately not `Rng.Master`.**
  The original seeds its whole world build with one (`srand(0x8EA91836)` … `srand(time(0))`,
  `FUN_004df1d0`), so a chapter's forest is the same forest on every launch; deriving from the
  session master instead rerolled C1's species mix on every unpinned run (measured: firtree1 15,154
  vs 15,148), which no golden could survive. Do not "fix" this to respect `--seed=`. Matching the
  original's *sequence* is a different thing and is not attempted — different PRNG, different
  traversal, different draw count. `clutter-determinism` (in-engine suite) is the guard.
⚠ **A substituted stamp keeps the SOURCE kind's properties.** `FUN_004dd6e0` holds the decoration
  entry's own kind block throughout and the roll rewrites only the model pointer, so a `firtree2`
  that came from a `firtree1` roll is scaled by *firtree1's* 0.9–1.1, while a `firtree2` the
  template placed itself is scaled by its own 0.9–1.5. The plan's C22 predicted the opposite; the
  decompile says otherwise.
⚠ **A substitution target that no template scatters is MINTED as a kind** (`ResolveModel`), because
  the engine resolves targets through its global model table — 40 such in C5, plus C3's
  `palmtree2/3` and C4's `firtree2`. Minted kinds live in `_allKinds`, never in `Template.Kinds`:
  they have no `CellPlacements` and must never be walked as a source. An entry naming the kind's
  OWN model stays in that kind rather than being re-resolved, so mesh duplicates of one model do
  not shuffle instances between themselves for no visible reason.
⚠ **Cull margins scale with the placements.** `KindExport.CullMargin` is the card width times the
  largest scale any instance actually drew (C2's spruce reaches 3.0×); `MapEdgeExtender` uses the
  same value, or a scaled card would pop at the screen edge.
⚠ **There is NO world-space grid and no global clutter origin** — the original has neither
  (`analysis/bl-305-clutter-uv/FINDINGS-A2.md`). Placement is `ClutterBuilder.UvTriangle`, i.e.
  `FUN_004dd6e0` steps 4/6/7: floor the triangle's UV bbox to an integer lattice, test containment
  **in UV space**, recover XYZ (Y included — a triangle is planar, so the affine map and a
  barycentric height are the same number) through the triangle's own affine UV→world map. **That
  map is valid only inside its own triangle**; C1's `terpat02` uses eight different UV frames with
  handedness split almost evenly, so reusing a neighbour's is a wrong answer, not an optimisation.
  Restoring a `gx * extent` grid re-loses C1 ~4× of its trees (A1: `terpat02` repeats every ~260 m
  against a 512 m quad).
⚠ **Skip on BOTH areas, not one.** A zero-area WORLD triangle can carry a healthy UV area — fan and
  strip artifacts of n-gons with repeated or collinear corners, 1,773 of them in C1 — and its
  affine map is finite but meaningless. Both counts, plus the null-UV-array and over-large-lattice
  refusals and an in-source-triangle assertion over every placement, go out on one
  `clutter uv lattice:` log line per build. All four skip counters bar the world-area one are 0 on
  retail data, and `outside_source` is 0 in every chapter — treat any nonzero as stop-the-line.
⚠ `PlaceOnMesh` reads `materials[0]` only, where the original iterates every texture layer
  (`FUN_004de190`). A3 measured that no polygon in the install names a registered template on layer
  1+, so this is unreachable on retail data — but it IS a deviation, and no A/B can detect it.
⚠ **A decoration's position is stored as the ground quad's own interpolated TEXTURE UV**, in
  `[0,1)`, the way `FUN_004dd230` stores it (`GroundInfo` → `GroundQuad.TryUv`, fmod-wrapped) —
  never as metres from a corner, and never divided by a scalar period. `max(extentX, extentZ)`
  relabels the UV exactly on 28 of the 32 shipped template quads and is wrong on the other four:
  `filmblock1` 64×128, `cliff1_sandtrans` 128×64, `parklot1` 16×32 and `parklot2` 32×16, the last
  two UV-MIRRORED, worst error 0.74 UV (`analysis/bl-305-clutter-uv/FINDINGS-A2.md`). The quad
  map is held and evaluated in DOUBLE and rounded once: in float the square templates' round trip
  loses an ulp and moves a golden.
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
⚠ **No slope cull.** `MinSlopeCos = 0.25f` was deleted in B13 as an inert invention: the original's
  cull is authored per kind (`min_slope`/`max_slope` → cosines, `FUN_004deab0`) and defaults to
  ±1.0, no chapter authors either key, **and the constant never fired** — `xzArea/trueArea` is
  `|Ny|`, and the steepest clutter-eligible triangle in the install is C1's at 0.4598 against a
  0.25 (~75.5°) threshold. Zero culled in every chapter; the same census at 0.50 culls 3, so the
  zero is a measurement. Do not reintroduce it; a cull belongs in `templates.zrd`'s `min_slope`.
  The `xzArea < 0.5f` sliver rule beside it is a different, still-live rule (14 triangles in C5).
⚠ **The quarter-metre `seen` dedup is KEPT deliberately, and it is remake-only — NOT fully retired
  by the `UvTriangle.Contains` fix.** B13 measured it doing two jobs. (1) Standing in for the
  `no_clutter` gate — retired, `PlaceOnMesh` reads the flag itself now. (2) Catching real
  duplicates from `Contains` being INCLUSIVE on the edge, where a lattice candidate landing exactly
  on two triangles' shared diagonal was claimed by both (pre-fix: C1 38, C4 139, C5 1,096) — the
  original's step-6 test is STRICT and claims such a point in neither triangle, so this was fixed
  in `UvTriangle.Contains` (2026-08-10), not in this set. **Measured after the fix, per chapter
  (`DedupRejected` in the build log), and it is NOT a clean win:** C1B/C2/C3/C5 drop to exactly
  zero, matching the shared-diagonal hypothesis — but C1 barely moves (38→36) and C4 is unchanged
  (139→139), so most of THEIR duplicates come from a different, still-undiagnosed source. `seen`
  stays for that reason, not as a defensive leftover.
⚠ **STALE — pre-B15. `PlaceOnMesh` now DOES read `GameZPolygon.NoClutter`** (renamed 2026-08-10
  from `.Subface`) and skips a flagged polygon (`FUN_004de2c0`'s gate), and
  `ClutterBuilder.BuriedClutterDistricts` no longer exists — B13/B15 landed the gate coupled with
  retiring that exemption, which is what actually resolves `BL-305`'s CAP-22 pose (the visible
  ground there is the flagged overlay; the exempted base layer had to come back for the gate to
  leave anything behind). See `docs/plans/PLAN-clutter-uv-placement.md` items B13/B14/B15 for the
  full account; this paragraph needs rewriting to match, not just re-pointing — flagged rather than
  silently corrected here.
  The `SceneBuilder.NoClutterLayerBias` depth-bias fix (renamed 2026-08-10 from `SubfaceBias`) is a
  separate mechanism: it only resolves which ground TEXTURE wins the z-fight, and has no effect on
  this file, which walks the same gamez tree independently.

## src/Mech3/ClutterTemplates.cs
The chapter's `templates.zrd` (`ClutterTemplateSpec.Load`/`.Parse`): one `ClutterKindProps` per
clutter DECORATION MODEL — `substitute`'s weighted roll, `scale_range`, `far_fade_range`, and the
jitter/rotation/slope/damage keys the retail data leaves at their defaults. Schema, offsets and the
per-chapter census: docs/formats/templates.md. Static over a reader list, so all eight chapters are
pinned off-engine (`CSVM.Tests/ClutterTemplatesTests.cs`). Consumed by `ClutterBuilder` for
`substitute` + `scale_range` (C22); `far_fade_range` is read and unapplied (C23).
⚠ **Keyed by decoration model, NOT by template.** C3 registers only `cliff1_sandtrans` and its file
  describes palms, because that template's quad scatters `palmtree1.flt`. The template registry is
  `interp.json`'s `AddClutterTemplates` (`ClutterBuilder.TemplateNames`); this file never meets it.
⚠ **Five keys plus all three damage blocks are authored by NO chapter** — `translate_uv_range`,
  `rotation_range`, `align_normal`, `min_slope`, `max_slope`. Read anyway, and asserted per chapter,
  because that zero is the evidence the original's placement has no positional randomness at all.
  Do not "simplify" the reader by dropping them: the measurement disappears with them.
⚠ **The nested pairs group by BOUND, not by band**: `[[nearMin, farMin], [nearMax, farMax]]`, which
  `FUN_004dd6e0`'s lerps (`+0x2c`→`+0x30` for near) settle. The two readings agree on the chained
  values (`[[200,300],[300,350]]`) and differ on C1's `firtree2` — so a wrong grouping survives
  casual inspection. `scale_range` is a FLAT pair and must not go through the same helper.
⚠ **The slope keys invert**: `min_slope`'s cosine is the UPPER bound on the normal's Y. Fields are
  named `NormalYMin`/`NormalYMax` after what they bound, never after the key.
⚠ `substitute` weights are RELATIVE and the engine normalises them (9.0/1.0 = 90/10). Properties of
  a substituted stamp resolve from the TARGET model — which is why 43 of C5's 78 blocks describe
  models that are never placed directly. Two C5 targets have no block: that means defaults, not
  "skip".
⚠ C5 ships one duplicate name (`cb05det01.flt`). `Find` returns the FIRST block, matching the
  engine's linear scan — and the first is the one carrying the substitute.

## src/Mech3/Zrdr.cs
Zrdr extraction reader (zip or unpacked dir): `LoadFile`, `LoadFileOrEmpty`, content-sniffing
`LoadMatchingFiles`, name-predicate `LoadFilesNamed` (for families with nothing to sniff, e.g. the
`ne0*` nets), and `ZrdrDict`, the key/[values…] view over a reader's alternating list.
⚠ `LoadFile` accepts both entry namings — v0.6.1 writes `X.json`, the fork writes `X.zrd.json` —
  in both the zip and directory branches.
⚠ An EMPTY reader is the four bytes `null`, which `LoadFile` rejects as "not a reader list" —
  indistinguishable there from corruption. `LoadFileOrEmpty` returns an empty list instead, still
  throwing when the entry is absent. C1C/C2B's `templates.zrd` are the shipped case.
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
⚠ `fog_zone` is NOT the sky/fog zone selector — `docs/HISTORY.md`'s "no chapter has a `fog_zone`
  key" is a wrong negative (five do), but the values do not name a weather zone either; see
  fogvol.md. `BL-277`'s geometry rule stands. It is a BOOL: `FogZoneArmed` (`FogZone != 0`,
  `FUN_0044e010`) drives `WeatherState.CameraWeatherState`'s state-3 gate (A2) and
  `FogVolumeWhiteout` (C21), and is true only for C5.
⚠ **`FogVolumeWhiteout` is the in-volume whiteout RULE** (`PLAN-weather-decompile-match` C21,
  `FUN_0044e6f0`): the chapter's `fog_fade_dist`/`interior_fog_fade_dist`/`fog_color` plus its
  volumes, answering one 0..1 density for a camera position. Pure, off-engine, unit-tested
  (`CSVM.Tests/FogVolumeWhiteoutTests.cs`); `Session/WeatherRig.Tick` is the only consumer.
  Approach ramp OUTSIDE (0 at `fog_fade_dist` → 1 at the wall), decay INSIDE (1 at the wall → 0 at
  `interior_fog_fade_dist` deep), union `a + b − a·b`. ⚠ The interior half reads backwards alone —
  the volume is a transition curtain and `ZONE3`'s fog (`C22`) carries the interior look; do not
  invert it. `Disarmed` is the seven other chapters, and it short-circuits before touching geometry.
⚠ **Two distances, and they are not interchangeable.** `FogVolumeBox.SignedDistance` is the max
  over the face planes — EXACT inside (for a convex polytope the nearest wall is the least-negative
  plane, so `−SignedDistance` is the penetration depth) but only a LOWER BOUND outside.
  `FogVolumeBox.ExteriorDistance` is the true Euclidean distance to the hull, by **Dykstra's
  alternating projection** over the face half-spaces — cyclic projection *with* the per-set
  correction term, which converges to the projection onto the intersection where plain POCS reaches
  only some point of it. Exactness is iterative (a whole cycle moving under 1e-4 m, capped at 64
  cycles; an axis-aligned box is exact in one), and the tests pin it against closed-form distances
  where the face planes alone are 29 % low on a box edge and 42 % low at a corner. Allocation-free:
  the corrections are a `stackalloc` of 32 entries, far above the widest shipped volume's 5 planes.
  `Tick` only pays for it when the cheap bound already lands inside the ramp — at C5's 16 m, almost
  never.
⚠ A volume is NOT the `CLOUD_COVER` band: only C1's floor coincides, and C1C/C4/C5 all disagree.
⚠ `FindMapSpanningSlab` (A5) is the data-driven test for "does this chapter have a map-spanning
  slab to continue past the map edge" — never a chapter name or a hardcoded `fvol1..9`. A volume
  qualifies only if it is axis-aligned (`FogVolumeBox.IsAxisAlignedBox`, the same corner test
  `CSVM.Tests/FogVolumeTests.cs`'s own census uses) AND top-anchored by the SAME rule
  `FogVolumeClutter.Scatter` classifies volumes with (A3's `TopAnchorHeightFactor`, passed in
  rather than duplicated) AND, together with every other volume passing those two, the set's
  footprints exactly tile their own combined bounding rectangle (a re-check of A1's "exact 3x3
  partition of `World.area`" finding, not an assumption). Pure geometry, no RNG, no render state —
  testable off-engine like `VolumesOf` beside it (`CSVM.Tests/FogVolumeTests.cs`). Returns
  `(null, reason)` rather than throwing when a candidate set fails the top-agreement or tiling
  cross-check, so a future non-conforming extraction is reported rather than silently producing no
  extension (verification.md DIAG-15) — no shipped chapter hits that branch.

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
`FBFX_COLOR_FROM_TO` is the one event whose effect is screen-space: the case reads `from`/`to`/
`run_time`, pushes them to the `ScreenFlash` sink (`UI/ScreenFlash.cs` — read its entry) and
reports `run_time` as the event's **duration**, which is what spaces `he_ground_effect`'s six steps
over their authored 1.2 s instead of collapsing them into one instant (the original's handler
instead returns "still running" until the time is up; the gate is the same). A sink and not a node
because this class is world-scoped and instanced per effect pool and per crash rig — the overlay
belongs to the session, which sets it on the world and world-effects runtimes. RESET_STATE skips it:
a wash is a thing that happens, not a base state. Decode (blend, interpolation, the single global
state a second burst overwrites, and why `alpha_delta` is not read) in
`docs/formats/anim-definitions.md`.
`LIGHT_ANIMATION` reports its `run_time` as the event's **duration**, so a chain of ramps is
spaced instead of firing in one instant — the original's handler (dispatch slot 5, `004e82b0`)
returns "still running" until the sequence's event timer passes the run time, exactly as
`FBFX_COLOR_FROM_TO` does. The ramp itself is asynchronous here (`AnimLight.TweenLeft`, ticked in
`TickLights`), which is the same picture only because the sequence is held: without the duration
each step re-armed the tween the one before it had started, and C1's `red_police` beacon
(`LIGHT_STATE` / +40 over 0.25 s / −40 over 0.1 s / `LOOP −1`) did not flash at all. Decode in
`docs/formats/anim-definitions.md`; the `ordnance-burst-timeline` suite is what measures it.
`Callback`/`ObjectCycleTexture`/`ObjectDeleteChild`/`CameraState` (dispatch slots 35/17/16/20) stay
on `default:` — decoded in full (`docs/formats/anim-definitions.md`), none gets a case. `Callback`
is `has_callbacks` mission plumbing with no registered consumer here; `ObjectCycleTexture` is the
`<part>_damage_*` cockpit indicator already recorded unwired on `Flight/DamageVisuals.cs` (no
cockpit); `ObjectDeleteChild`'s scene-graph reparent is either unreached (`camera1-generic_intro`'s
rig, `apassengers-rem_pas`'s `pass_st`, which is not a gamez node anywhere) or reached but masked
(the `cpeject*` defs hide `cpilot` immediately after, delete or not); `CameraState`'s only caller is
the same unreached intro-cutscene chain.
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
`MotionRuntime`'s `translation_range` is a POLAR launch — `xz` azimuth, `y` elevation, both in
degrees, `initial` the speed (`analysis/object-motion-range/`, decoded 2026-08-01) — and a launch
seeds from the node's authored rest pose, since a shared effect template's children are re-homed by
nothing between calls. `RangeLaunchDirection` is that decode's ONE expression; `ProjectilePool`'s
gun-casing ejection reads the same `gunshell` event through it (INSTR-3).
⚠ The elevation is LINEAR, not spherical, and the direction is deliberately NOT unit length:
`FUN_004e8fa0`'s `flags & 8` block computes `dirY = elev · 0.011111111` (1/90 written out) and gives
the horizontal the L1 remainder `1 − |elev|/90`, so the length dips to 0.707 at 45°; only the
AZIMUTH is converted deg→rad and passed to the sincos at `FUN_0053c6c0`. **Do not normalise it** —
the unit-sphere reading that shipped until 2026-08-11 launched 60–70° debris 20–25 % too fast and is
what cut `m_build03` part1 at ~70 % of its authored 5.0 s `RUN_TIME`. Which world bearing azimuth 0
points along (+X) remains a choice, and `FUN_0053c6c0`'s output order is unchecked — the cos-on-X /
sin-on-Z assignment is inherited, not decoded. Its `scale` channel is an
OFFSET from unit scale (`1 + initial + delta·u`), unlike the absolute `PoseScale`/`OBJECT_SCALE_STATE`
— 30 of the 45 distinct SCALE events carry a bare `-0.1`, which absolute is a negative scale.
`gravity.complex` picks between two forms of the same fold: plain drops the value into the parent
frame's Y, `complex` converts world-down INTO that frame — identical under a world-aligned parent,
which is why the install authors it on aircraft wreckage alone (254 events / 25 shapes, all with a
`RUN_TIME`).
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
`SequenceRunner` runs one sequence's event list on a clock — **two** clocks, in fact: its own, and
the owning `AnimInstance.Clock` that a `START_TIME ANIMATION` gates against (the original's
`anim+0xb0`, one per definition instance and shared by all its sequences). The two differ for every
sequence a later CALL_SEQUENCE starts, which 191 shipped events read; a null `start` encodes as
`Animation + 0.0` but must stay on the relative path, and `SetDue`'s comment says why.
Its scope is per-event START_TIME gating, LOOP with
authored-count-0 = infinite, and IF/ELSEIF/ELSE/ENDIF via a `_branchTaken` stack + a deliberately
**non**-nesting-aware `Scan` — the original counts no depth, and 48 shipped `gunhit` sequences
observe the difference; the constraint and its one residual live in `Scan`'s own comment).
**The original's sequence runtime is written up in [org/sequences.md](org/sequences.md)** — the
function map, the three START_TIME origins, the LOOP's pass counter and 60 Hz frame denomination,
and the clock-carry a timed loop needs; the authored side stays in
[formats/anim-definitions.md](formats/anim-definitions.md).
`AnimInstance` holds a definition's concurrent runners and removes them as they finish, and carries
the CALL_SEQUENCE/STOP_SEQUENCE semantics (decode in `docs/formats/anim-definitions.md`;
`AnimRuntime`'s dispatch cases are thin shims over these). **One runner per sequence, keyed on the
`AnimSequence` OBJECT and never on its name** — the original holds a sequence's state inside the
definition's own sequence array (`004eb570`), so `CallSequence` starts a sequence only when nothing
is running it AND its authored activation is ON_CALL; a call into a running or non-ON_CALL sequence
is a silent no-op that still reports *found*, since callers read the return as "did the name
resolve" for the CALL_ANIMATION fallback. Names are not unique (`he_ground_effect` ships two
unnamed sequences), which is why identity is the object. `StopSequence` halts every matching runner
**and does nothing else** (`004eb610` writes the sequence DONE and has no start-if-not-running
path): a stop naming a parked ON_CALL sequence therefore runs no teardown at all, which is what 16
shipped definitions author (`flame_ball_01/02 → stop_p1trail`, every chapter, inside the HE
explosion's chain). CSVM does not persist the resulting DISABLE, so a later CALL_SEQUENCE can still
start a stopped sequence where the original would refuse it — 123 definitions name one sequence in
both a call and a stop, but no def is known to reach the stop first. Both are
public so `CSVM.Tests` drives them against a fake host; the host is any `ISequenceHost` (the game's
real one is `AnimRuntime`, tests pass a recorder). Anchors are opaque `Node3D?` pass-through — the
interpreter never dereferences them.
The up-counting `_loopPasses` mechanism (0 is infinite because the counter starts at 0 and only
grows, so it can't re-equal 0 once a pass has run — no normalisation needed), the
`goto case "Elseif"` and the 256-fires-per-frame guard all LOOK refactorable and are all
load-bearing (each a shipped, measured bug: the bowl sign's 38% blank frames, frozen traffic loops,
the double-polling waterfall) — the `Loop`/`Advance` code comments in `SequenceRunner.cs` carry the
measured evidence; read them before touching any of the three. `OnEventDispatched` being a get-only
nullable delegate on the seam is the same shape: the null-conditional at the fire site short-circuits
the `EventDispatch` construction when no debugger is attached, the documented zero-cost contract on
the hot dispatch path, explained on `ISequenceHost.OnEventDispatched`'s own doc comment — making it a
method would break that.
⚠ **An instantaneous LOOP pass costs one `AnimFrame` (1/60 s) of SIM time, never one rendered
  frame** — a `LOOP n` is an authored timer of n frames, so pacing it per frame made every such
  timer scale with the client's hardware. The gate is applied at the FOOT of the advance loop
  (`_frameGatePending`), because the trailing `SetDue()` there re-gates on whatever event control
  flow landed on and silently overwrites a `_due` written in the LOOP case — which is why the
  pre-fix code reached for an early `return`, and that return *was* the frame lock. Below 60 Hz the
  loop catches up within the frame (the 256 guard bounds it); at 1/60 it is one pass per step,
  bit-identical, which is why no `--det` capture moved. **The catch-up is a DELIBERATE divergence
  from `crimson.exe`** (`004ebfd0`), recorded, not accidental: the original hard-zeroes both timers
  on every pass and returns immediately — one pass per its OWN engine tick, always, whatever that
  tick's length. That quantises correctly only because the original paces itself; CSVM must pace an
  authored duration against sim time at whatever step size the session runs, so dropping the
  overshoot instead of carrying it costs real accuracy — `BL-237` measured a 0.02 s period taking 2
  steps instead of 1.2 at 60 Hz, and `ww_balmoral1/2/3` (`LOOP 1000 @ 0.01 s`, authored 10 s) taking
  16.7 s. The carry is what keeps a timed loop's total duration correct at any step size; the
  original's own tick-quantised total is not the target.
⚠ **A called sequence's first event fires one tick LATE, and that is a known, measured, deliberate
  non-fix** (`BL-135`): `CallSequence` appends past the descending walk's cursor. A bounded
  same-pass drain was built and measured install-wide on 2026-08-04 and NOT kept — it repairs
  nothing observable (7 of 8 chapter captures pixel-identical, every runtime total unchanged; only
  the bootstrap censuses move, which is LOG-2) yet moves 4 of the 13 goldens, `c1-crash` by 79.7 %
  of its pixels. Before re-implementing it read `analysis/bl-135-callsequence-lag/FINDINGS.md`: it
  carries the implementation, the cap sized from the data (deepest authored same-tick fan-out 15,
  so 64) and the one def that makes a bound mandatory (`marypickford` rings, instantaneously,
  because `OBJECT_MOTION_SI_SCRIPT_ALL_NAMES` has no handler and reports duration 0).
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

## src/Flight/AimAssist.cs
The gun aim assist (`BL-342`, decoded in `docs/org/aim-assist.md`). `GunAimSlot` is one gun
barrel's plane-local state — `Smoothed` (what the round fires along), `Target` (what `Smoothed`
chases), `LastUpdate` (game-time seconds) — mirroring the original's eight `0x24`-byte slots at
plane `+0x3a4`. `AimAssist.Tick` (B2) is the per-frame forget + catch-up pass
(`FUN_004b3e50`): past `forgetInterval` seconds since the slot was last touched the target unwinds
to local forward; otherwise `Smoothed` slerps toward `Target` at `catchupRate` per second, snapping
outright once a single frame covers the whole turn (`catchupRate·dt ≥ 1`). A plain, engine-free
static class — no `GameClock` read inside it, `now`/`dt` are always passed in — so it unit-tests
without a `FlightController`; proven in the in-engine `aim-assist` suite (`Suites.cs`), which B3/B4/
B5 add their own cases to. `FlightController` owns one `GunAimSlot[]` per firable gun group (indexed
exactly as `FireControl`'s own `(group, muzzle)` pairs — no re-derivation of the original's
`weaponGroup·2+barrelToggle` index), built alongside `_firableGuns`, and ticks every group's array
immediately BEFORE performing `_fire.Step`'s outcome — the original restamps a slot's `lastUpdate`
on every round that goes out (B5's job), so the forget pass must see the pre-shot state. Runs for
every pilot unconditionally today because every pilot in CSVM is human (Decision 7 in
`docs/PLAN-sticky-bullets.md`); B6 adds the human-piloted gate once AI planes exist.
⚠ **Godot's `Vector3.Slerp` throws "Argument is not normalized" when the two directions are
  numerically parallel or opposite** — its rotation axis comes from a cross product that
  degenerates at 0°/180° separation (`FlightModel`'s `VelocityDir` slerp hits the identical crash
  and guards it the same way). `Tick` falls back to a normalized lerp near-parallel and snaps
  outright near-opposite; do not replace that branch with a bare `Slerp` call.
⚠ `PlaneStats.StickyBulletCatchupRate`/`StickyBulletForgetInterval` are the two of the four
  `sticky_bullet_*` keys B2 consumes (shipped 5.0 / 1.5); `StickyBulletDistFactor` (shipped 0.0) is
  B4's, and `Inaccuracy` is B5's and is not parsed yet — do not add it here ahead of the item that
  uses it.

`AimAssist.TryIntercept` (B3, `FUN_00460e30`) is the constant-velocity intercept solver: given a
muzzle position, the round's speed, a target position, and the target's velocity RELATIVE to the
shooter (the caller subtracts before calling), it returns the fire direction and time of flight, or
false for no solution. Frame-agnostic — every input in one space (world or plane-local), the answer
comes out in that space; B5 supplies plane-local inputs. Internally solves for `u = 1/t` rather than
`t` directly: `t`'s own quadratic has `|relVel|² − speed²` as its leading coefficient, which sits
near zero whenever the target's closing speed is close to the round's (the common case), while `u`'s
leading coefficient is `|displacement|²`, essentially never near zero for a real separation — that
substitution, not the quadratic formula's own cancellation avoidance, is the "numerically stable"
part. Fails on near-zero separation, a negative discriminant, or both `u` roots non-positive (no
forward-time solution — the case that catches a target receding faster than the round in a straight
line, which can still leave the discriminant positive). Proven in the `aim-assist` suite's
dead-ahead/crossing/receding cases.
⚠ Do **not** "fix" the Citardauq (`q = -0.5·(b + sign(b)·√disc)`, roots `q/a` and `c/q`) form back
  into the textbook `(-b±√disc)/2a` — it reintroduces the cancellation the substitution above exists
  to avoid, and the difference only shows up at long range where it is hardest to notice.
⚠ The original's square root is a bit-trick approximation (`(x>>1)+0x1fc00000`), accurate to roughly
  a per cent — `TryIntercept` uses a real `Mathf.Sqrt`. Do not reproduce the approximation, and do
  not read a sub-per-cent disagreement with a hand-computed reference as a bug in either.

`AimAssist.Scan` (B4, `FUN_004b6530`'s scan half) picks the target the original would pick, or none.
It takes an `AimScan` context (world muzzle, shooter velocity + team, the plane's forward axis, the
weapon's speed/`RANGE²`/cone cosine, `dist_factor`) and an `AimCandidateSet`, and returns the
highest-scoring survivor as an `AimScanResult` (which list it came from, the intercept direction,
time of flight, score, and the candidate's `Source` object). One scorer runs over all four of the
original's lists in its order — the engine ships four byte-identical scorers differing only in the
container accessor. Gates, in the engine's order: the shooter itself (matched by reference against
`AimScan.Self`), a candidate that is not live (the vtable `+0x14` predicate), same team **or either
side unaffiliated**, no intercept, `speed²·t² > RANGE²`, outside the acceptance cone
(`AimAssist.WeaponConeCos` = `cos(CANNON_SPREAD°)`, or the candidate's own `+0x50` half-angle in
radians via `ConeCosFor`). Survivors rank by `alignment − distance × dist_factor`. All world-space:
B5 rotates the winner world→local into the slot's target field. Proven in the `aim-assist` suite,
every gate with its able-to-fail baseline.
`AimCandidateSet` keeps the four lists **separately** and deliberately: `Vehicles` (the live
`FlightController`s), `Turrets` (**empty until M4** — the pass exists and iterates nothing, so
wiring turrets in is an `AddTurret` call and not a rediscovery of this item), `Structures`
(`AddStructures(DestructibleRegistry)` — an **approximation** of the original's `targets.zrd`
`MStructList`, recorded as one; `MissionTargets` is not the analogue, it holds objective display
strings and nothing damageable) and `Ordnance` (`ProjectilePool.CollectFusedOrdnance` — a FILTER
over the rounds in flight, `DetonationDistance > AimAssist.MinFuseDistance`, not a structure of its
own).
⚠ **CSVM has no team model at all**, and the engine's team gate needs one, so `AimAssist` supplies
  the convention: `TeamOfPilot` = `PlayerIndex + 1` (every pane hostile to every other, which is
  what `--vs` is), `NeutralTeam` 0 for a round nobody owns, `WorldTeam` for the destructibles. Team
  0 on EITHER side rejects the pair — it is "never a target", not a wildcard.
⚠ `dist_factor` **ships at 0.0**, which deletes the distance term outright: selection is purely
  most-aligned, and a distant on-axis target beats a near off-axis one at any range inside `RANGE`.
  The executable's compiled default is `2.5e-4`; "restoring" it during tuning re-enables something
  the shipped data turns off. The suite pins both, and shows the winner flipping between them.
⚠ Scoping the candidate set to aircraft is a silent behaviour change, not a simplification: guns
  snap onto a live proximity-fused round by design (the engine's own priority query ranks that list
  above the other three), which is why the ordnance filter is here and not deferred.

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
**The original's projectile-visual runtime is written up in [org/tracers.md](org/tracers.md)** — how
the engine draws a round at all (the `FLYOUT MODEL` is aimed ONCE at spawn and thereafter only
translated; the shared model node is multi-parented, not cloned, so one `slug.flt` serves every live
round), plus the authored tracer geometry the hand-tuned constants here stand in for: two crossed
0.2 × 4.5 m quads whose **tail** is the tracked point, a separate 0.29 m `*tip` quad 4.56 m ahead,
and a 600 m LOD past which the original draws nothing. Its closing table lists every place this file
deliberately differs — read it before retuning `TracerLength`/`TracerWidth`/`TracerBrightness`/
`TracerMinPixels`. ⚠ Same siting rule as `org/puffer.md`: executable decodes live in `docs/org/`,
never in the CC-BY `docs/formats/` tree.

**Rounds leave the muzzle along the nose axis — the original's don't.** The retail engine runs a
per-muzzle gun aim assist at spawn time (target scan → constant-velocity intercept → plane-local
smoothing → scatter cone), decoded in [org/aim-assist.md](org/aim-assist.md) and built in
`AimAssist.cs` (`BL-342`); `Spawn` is not wired to it yet (B5). ⚠ It is a **launch-direction**
assist: nothing steers a round in flight, so it belongs at the fire call, not in this file's
integrator.
`CollectFusedOrdnance(AimCandidateSet)` is this file's one contribution to it: the assist's fourth
candidate list is a FILTER over the live pool (every round whose def carries a proximity fuse longer
than `AimAssist.MinFuseDistance`), so the guns can snap onto an incoming rocket the way the
original's do. `DefaultVelocity` (500 m/s, the launch speed for a def with no `VELOCITY`) is shared
with the scan so the lead it solves is solved for the speed the round actually leaves at.

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
carries the same per-ammo axis (`tracer_slug`/`_dumdum`/`_armorpierce`/`_magnesium`) and takes its
UVs from its own mesh.

**The tracer is the authored shape, not a tuned sprite** (`org/tracers.md`). `CrossedStreakMesh`
is the original's `rabbit_blur`: two perpendicular quads in ONE `ArrayMesh` — so a round still costs
one instance — 0.2 m wide × 4.5 m long, its local +Y the front, U running 0-at-front to 1-at-tail.
`RenderTracers` scales both width axes by `TracerWidth` and the length axis by `TracerLength`
(`new Basis(xAxis*width, yAxis*len, zAxis*width)`) and places the round's position at the streak's
**tail**, geometry running forward, because that is where the engine attaches the model. A second
pass draws the **tip disc** — the authored bright head is a separate mesh (`TipTextures`:
`slugtip`/`dumdumtip`/`armourpiercetip`/`magnesiumtip`), `TracerTipSize` across, `TracerTipOffset`
ahead of the round and perpendicular to flight, so it self-hides side-on exactly as the data does.
Three consequences worth not re-litigating: **no camera** enters the streak basis (the crossed pair
reads solid from any angle — the eye is consulted only for the pixel floor below), **no growth ramp**
(the engine never scales a gun round; it is full size from the spawn frame), and **ordnance draws no
streak at all** (every rocket `FLYOUT` prototype is a missile body — its trail is the
`MODEL_ANIMATION` puffer smoke, so `RocketStreakScale`/`RocketExhaustScale` and the generic `tracer1`
entry are gone). `weapons.tracerLength`/`tracerWidth`/`tracerBrightness` (C23) still route the
constants through `Config`, but the defaults are now **measured off the model**, not tuned by eye —
and `tracerBrightness` sits at a neutral 1.0, since the ×3 overbright was compensating for the tip
disc and second quad this shape supplies itself.
⚠ `RenderTracers` still floors the drawn width/length per round against `weapons.tracerMinPixels`
via `ScreenSize.MinWorldSizeForPixels` (inverts a camera's vertical FOV/viewport-height projection),
so a round far enough out reads as a fleck instead of shrinking under a pixel. **This is a known,
deliberate conflict with the decode**, which measures a hard 600 m LOD past which the original draws
nothing at all. It stays until a shot fired at a *known* range settles which reading is right; the
tip disc is deliberately left unfloored.
⚠ **The floor is a screen-space rule over ONE shared world-space mesh, so bind every pane's camera
to `ProjectilePool.Viewers`, not just player 1's.** `ScreenSize.NearestFloor` sizes each round for
the **nearest** viewer, measuring every camera with its own FOV and its own pane height. Binding P1
alone (as this did until the splitscreen fix) sized every round against P1's distance and then drew
that geometry in all the other panes — a round 1000 m from P1 but 100 m from P2 came out ~10×
oversized in P2's view, which is what "P1's tracers look right, everyone else's are huge" was.
Minimum is load-bearing: sizing for the *farthest* viewer inflates every nearer pane, while sizing
for the nearest can only under-floor a distant one, which is just the un-floored look.
`TracerScreenSizeTests` pins the rule (and the per-viewer pane height, where a far viewer in a tall
pane legitimately beats a near one in a short pane).
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
⚠ **RETRACTED 2026-08-13 (A1, `BL-342`): `CANNON_SPREAD` is not a dispersion cone — it is the
  unbuilt aim assist's acceptance cone, decoded in `org/aim-assist.md`.** The original applies no
  scatter at the fire call; `Projectile.cs:534`'s `ApplySpread(forward, weapon.CannonSpread)` call
  was a fidelity bug, now removed. A round leaves the muzzle exactly along its aim.
⚠ The stand-in fireball's sprite scatter draws from `Rng.Weapons` — a pinned run repeats its whole
  impact pattern (two `--det` C1B dives: 8/8 identical impact positions); new randomness must route
  through it. Trail-puffer scatter draws each emitter's own `Rng.Puffer` stream.
⚠ A trail emitter is reusable only when its round died AND `LiveCount == 0` — reusing sooner
  grafts the new rocket's trail onto the old one's live smoke. A chapter lacking the rocket's
  prototype model now flies the smoke trail **alone** — the `RocketStreakScale` stand-in streak that
  used to cover that case is deleted, since the data gives ordnance no streak at all; the empty stage
  (no world program) flies trail-less.

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
⚠ Standoff (120 m) was originally kept short because `CANNON_SPREAD` was wrongly read as a dispersion
  cone that grows with range; A1 (`BL-342`) removed that scatter, so a round now leaves dead straight
  and the achieved pass distance equals the requested one at any standoff. No data-driven reason
  remains for this exact figure.

## src/Flight/PhysicsConstants.cs
`PhysicsConstants.NomGravity` — the single 20 m/s² player.json `nom_gravity` value, shared by
`PlaneStats.Gravity`'s default and `ProjectilePool.WorldGravity` so the two can't drift apart.

## src/Flight/PlaneStats.cs
Typed per-plane stats: vehicle.json `dynamics` (resolved through the `kind_of` def chain) +
engines.json stock engine power + player.json globals (the flight constants, the near-miss cue's
`warning_shot_*` block, the gun aim assist's `sticky_bullet_catchup_rate`/`_forget_interval`
(`AimAssist.cs`'s B2) and `_dist_factor` (B4's scoring — `_inaccuracy` is B5's and is not parsed
yet), plus the decoded model's
lift/AoA/G, turn/yaw-curve, pitch-fade and drag-fade-speed globals — docs/org/flightModel.md; converted
exactly as the original does: MPH×0.44704, AoA/liftAOAs cosined, highGs/lowGs raw G — and as yet
unread by FlightModel.cs), the `engine_sound` def name with its
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
`WeatherState`: per-mission atmosphere from the flown mission's own weather.json — per-zone
`ZoneWeather` records holding fog (`FOG_COLOR`/`FOG_RANGES`/`FOG_ALTITUDE`), `SUNLIGHT_*` →
`WorldLight` (`SunIncidence` 0.46 / `MinWorldLight` 0.15, TUNE) and `SUNLIGHT_ORIENTATION` →
`SunOrientation`, plus the `CLOUD_COVER` whiteout band (`WhiteoutAmount` trapezoid), `WIND`, and
precipitation → `PrecipData`. Schema + colours + zone names: weather.md.
**The original's weather/sky/fog/light runtime is written up in [org/weather.md](org/weather.md)** —
the camera weather state machine, the `zone_id` visibility gate, the zone apply's edge trigger, the
band flicker's two curves, and the sun/dome/deck rules. Read it before changing a weather mechanism.
⚠ The record is named for the whole zone, not for the fog, because `WeatherRig.ApplyZone` writes
  all three in ONE call on the zone edge — the binary's own shape (`FUN_00472ea0` sets the fog
  parameters and then the `sunlight` node's orientation). A zone change that moved the fog and left
  the light behind is the bug that single record makes unrepresentable (`BL-324`).
⚠ `SunOrientation` is Godot euler RADIANS, assignable straight to `DirectionalLight3D.Rotation`:
  the gamez→Godot mapping is the **identity** (gamez node eulers are already read as
  `Basis.FromEuler(v, Yxz)`, Godot's default order is YXZ, a directional light shines along local
  −Z). It shades AIRCRAFT only — the world is fullbright. Pinned against the original's own
  euler→direction helper in `CSVM.Tests/SunOrientationTests.cs`.
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
  geometry cannot decide — resolved by render evidence instead (`PLAN-overcast-match` `B12`, all
  four = `zone2`), which is what the default already ships; not a gap here.

`CameraWeatherState(cameraPosition, fogZoneArmed, volumes)` (`PLAN-weather-decompile-match` A2,
`FUN_0042ee40`) is the binary's per-frame camera zone 1/2/3, published by `WeatherRig.Tick` onto
each `PlayerRig.CameraWeatherState`: 1 default; 2 when
`HasCloudBand` and the camera's altitude is at/above `CloudCoreBottom` — a THIRD spelling
alongside `CloudBandCentre` (the deck-regime flip) and `CloudBottom` (the visual floor), all
within ~80 m of each other in C1 but never unified (Decision 1); 3 when `fogZoneArmed`
(`FogVolumeSpec.FogZoneArmed`) and the camera is inside any `FogVolumeBox` — the exact
half-space `Contains` test, not the AABB — and state 3 wins over state 2 on overlap (never
actually exercised in shipped data: only C5 arms `fogZoneArmed`, and its band sits far above
every C5 volume).
`ZoneForState(state)` is B11's consumer-side half: state *n* asks for `zone<n>` and goes through
`ResolveZone`'s file fallback, so a mission with no `ZONE<n>` keeps its first zone rather than
falling to `NoFog` (no fog, fullbright). Deliberately NOT routed through the horizon-aware
`ResolveZone` overload — that one owns the DOME's single per-flight zone; the fog zone changes
underneath it every time the camera crosses the cloud core.

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
never do. `Emit` dispatches: DISTANCE_INTERVAL trails per interval of the authored AT_NODE
point's actual motion, including its host-frame offset (the speed cue's point is 60 m ahead;
CAP-15's density, `BL-259`); TrailPool-sized even on the sustained path, since the time-cadence
pool floor silently dropped ~85% of a flight-speed trail; a still host keeps the time cadence
(the static
building sputters, whose distance can never elapse — every distance state carries the parsers'
synthetic 0.1 s TIME_INTERVAL, so that cadence always exists); a host that CANNOT move declares
`staticBurnMps` and spends virtual metres at the held pose instead (the damage lab's parked
plane). `Stop` ends the trail as well as the emission, unconditionally and idempotently, so a
revived emitter re-homes rather than drawing a puff line from its pooled slot's previous call
site (the rocket ghost trails). **All five external callers (`PufferEmitter`, `ProjectilePool`,
`DamageVisuals`, `ThrottleSlamSmoke`, `SpeedCue`) drive the emitter through `Emit`/`Stop`
only** — the six
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
`puffer.trailSizeScale` / `puffer.sustainSizeScale` (`SizeScaleDefault` **2**, decoded from
`FUN_0057c5c0`/`FUN_0054e6e0`: `SIZE_RANGE` is a screen-space HALF-extent, so the world quad's
side is `2 × SIZE_RANGE` — not a judgement call, `PLAN-puffer-engine-deltas` A1. The knobs remain
for deliberate per-path tuning — the cull margin scales with the largest). Two more scale the
`fire_n_smoke` family ONLY — `puffer.fireRiseScale` / `puffer.fireLifetimeScale` (defaults 2.5/1.5,
invented against original footage rather than decoded, signed off at the controls 2026-08-06):
vertical spawn velocity + puff lifetime of the crash/destruction/tank fires, identity for every
other emitter — left untouched by A1/A2; D10 re-judges them against the corrected sim. All read at
`Init`; registered in `Config.WarmTuningRegistry` for `--dump-config`. Moving
`SizeScaleDefault` re-pins every puffer-bearing golden and only those. Measured on the 4->1 revert:
`c1-waterfall`, `c3-island`, `c5-city-night`, `c1-destroy-effects`, `c1-crash`. Re-measured on the
1->2 A1/A2 landing, which found two more carrying a live puffer that revert had missed:
`c1-flight` (`ThrottleSlamSmoke`'s exhaust trail — see the `⚠` two paragraphs up) and `c4-snow` (an
ambient puffer visible on the mountainside in that shot's pose) — **seven** puffer-bearing goldens
total, the other 6 carry no live emitter.
⚠ TEXTURE_SEQUENCE times are FRACTIONS of a particle's lifetime, not seconds (effects.md).
⚠ `SpawnSustained`'s draw order (pos → vel → size → life) is shared determinism: reordering the
  `Rand` calls re-scatters EVERY sustained emitter — measured as five goldens moving with the
  fire tune inert in all of them.
`START_AGE_RANGE` (`PufferState.StartAgeMin`/`StartAgeMax`, `PLAN-puffer-engine-deltas` B4): a
particle is born at `Rand(StartAgeMin, StartAgeMax)` instead of age 0, gated on
`HasStartAgeRange` so the extra `Rand()` draw is skipped entirely for the ~2,900 puffers that
don't author the key (only 4 in the install do).
A negative age (`fire_at_zepskin3`'s min is −1.0) is drawn on the frame it's born, pinned to
stop 0 of every ramp/envelope (`p.Age > 0f ? p.Age / p.Life : 0f` in `_Process`), and outlives its
authored `LIFETIME_RANGE` by `|age0|` since reap is `age >= life` with no sign test.
**Sub-frame emission** (`PLAN-puffer-engine-deltas` B5): `SustainAt` keeps the previous frame's
emitter origin and spreads a frame's batches along the motion segment instead of stacking them on
today's pose — batch `b` of `batches` spawns at `prevOrigin.Lerp(origin, frac)` with
`frac = (b+1)·interval / accumulator`, and carries the engine's matching `(1 - frac)·dt` added to
its start age (`FUN_0054f8b0`). The first frame after a `Stop()`/revive re-homes `_sustainPrevOrigin`
to the current pose rather than trailing from the stale one — the same ghost-trail rule
`TrailAdvance` follows (commit 450131a).
**The born-dead skip IS implemented** (`if (age0 >= life)` ⇒ not created, no pool slot consumed, all
three spawn paths). B4 closed it as an unreachable disproof by comparing the authored
`START_AGE_RANGE` alone (max 0.1 s vs. min lifetime 1.0 s) — right about the key, **wrong about the
guard**: `age0` is that draw *plus* B5's `(1 - frac)·dt`, so a long frame makes the skip reachable
for ANY puffer that authors no `START_AGE_RANGE` at all. A 5 s hitch gives the earliest catch-up
batch ~4.8 s of start age, past every lifetime in the install; those batches really are that old, so
a hitch produces mostly-empty catch-up and the pool fills on the *following* frame instead, as the
leftover accumulator drains at a normal `dt`. Do not "fix" this by clamping the age-offset `dt` —
that clamp was tried, it keeps dead batches alive, and it is an invented divergence the skip exists
to make unnecessary. Every draw is made before the skip decides, so a skipped particle consumes the
same `_rng` stream a created one would.
**Wind-coupled friction and the traced integration order** (`PLAN-puffer-engine-deltas` B6).
`_Process` now integrates exactly as `FUN_0054ee10` does, and the ORDER is the change:
`pos += v·dt` on the velocity the particle had at the top of the frame, **then** `v += a·dt`,
**then** — only when `FRICTION != 0` (`0054f016`'s own gate) —
`v = (v − wind·WIND_FACTOR)·damp + wind·WIND_FACTOR`. Friction damps toward the WIND, not toward
rest. Ours previously did all three the other way round (`v = v·damp + a·dt; pos += v·dt`), which
puts an accelerating particle exactly one frame of velocity ahead of where the engine puts it.
`WIND_FACTOR` (`PufferState.WindFactor`) **defaults to 1, not 0** — the puffer object's ctor
(`FUN_00550100`) writes `1.0` to `+0x6c` and the applier only overwrites it when the flag is set,
so 2,802 of the install's 2,863 friction-bearing compiled events are FULLY wind-carried and only
12 (the six `subdoors_puffer` names) author an explicit `0.0` to opt out. Both parsers therefore
distinguish absent from zero.
B6 moved **six** goldens, and an able-to-fail control (a build publishing `Vector3.Zero` instead of
the stepped wind) splits them cleanly: `c1-waterfall`, `c3-island` and `c4-snow` reproduce the
zero-wind hashes exactly, so those three moved on the INTEGRATION REORDER alone and their emitters
never feel the wind (`FRICTION 0`, so the gate excludes them); `c1-flight`,
`c1-destroy-effects` and `c1-crash` hash differently with and without it, which is the proof the
wind reaches real puffers rather than only the suite's synthetic ones. `c5-city-night` is the one
puffer-bearing golden B6 left byte-identical — its emitter carries neither friction nor world
acceleration, which makes the reorder an exact algebraic identity there.
⚠ The friction gate is load-bearing now. It used to be an unconditional `Exp(0) == 1` damp, an
identity at `FRICTION 0`; with a wind inside the block it is not, and a frictionless puffer must
feel no wind at all.
The wind itself is `Effects/WorldWind.cs` — see its own entry.

**The camera-distance fade** (`PLAN-puffer-engine-deltas` C7, `DistanceAlpha`). `FADE_RANGE`/
`NEAR_FADE` become a per-particle alpha and two hard culls at DRAW time — the mechanism, the field
order, the cross-wire and the three `puffer.*` switches are all written up in
[formats/effects.md](formats/effects.md); read that before touching this. What matters here is the
plumbing: the distance is view-space depth off `EffectAmbience`'s camera pose (see
`Effects/WorldWind.cs`), and a discarded particle keeps living, moving and ageing — it is simply not
written this frame, exactly as the engine's split between `FUN_0054ee10` (sim) and `FUN_0054e6e0`
(draw) has it. That is why `_Process` carries a separate `drawn` cursor for the renderer: its
contract is indices `0…n-1` packed and ascending followed by `Show(n)`, and `n` is now the DRAWN
count, at most `_liveCount`.
⚠ The distance alpha MULTIPLIES the `COLORS` ramp's alpha or the life envelope; it replaces neither.
  Getting that precedence wrong makes every ramped puffer invisible.
⚠ C7 moved **two** goldens and three able-to-fail controls split them: with the fade neutered all 13
  are hash-identical (so the parsers, the `drawn` cursor and the alpha plumbing move nothing);
  with only the near cull disabled `c1-crash` returns to its old hash, so **its whole delta is the
  authored 70 m near cull** on the crash fireball — a large, visible loss at an 18.5 m chase camera;
  and `c1-destroy-effects` moves either way, so its delta is the authored far ramp (400→600 m on
  `ap_radiotwr`'s `puffer1`) and is sub-perceptual side by side. A third control shows the
  unauthored depth-0 rule (behind-camera particles) moves nothing anywhere in the set.
⚠ `DEVIATION_DISTANCE` scatters **±0.5·d**, not ±d (`PLAN-puffer-engine-deltas` A2):
  `FUN_0054f8b0` spawns at `prev + delta*frac + (rand01 - 0.5) * d` per axis, so the offset is a
  HALF-width around the origin — `Rand(-d, d)` was drawing twice the authored width per axis
  (eight times the authored volume). All three spawn paths (`SpawnSustained`, `SpawnTrailPuff`,
  `SpawnBatch`) now draw `Rand(-0.5f*d, 0.5f*d)`, one `Rand()` call per axis exactly as before —
  changing the draw *count* would re-scatter every emitter for an unrelated reason.
⚠ The blend is derived, never authored: a COLORS ramp or a near-black dying sprite (measured off
  the atlas, `SmokeLuminance`) ⇒ blend_mix + no depth fade, else blend_add (effects.md).
  `Create`'s `blend`/`softParticles` force the verdict for a caller that knows better.
⚠ Each emitter's `_rng` is a per-instance stream off `Rng.Puffer`, so particle spread is pinned by
  the master seed: measured, the C1 waterfall mist moved 0.47% of a `--det` frame before and 0.00%
  after. Its seed depends on how many puffers were built before it — deterministic under `--det`,
  and pinned to WHERE `new Puffer()` sits in `Create`. Moving it, or constructing one anywhere on a
  capture path, re-pins all seven puffer-bearing goldens (the `SizeScaleDefault` list above — an
  earlier "four", then "five", here was a stale count); `CreateWith` is a test entry point only.

**The emission accumulator: a teleport guard, and no batch cap** (`PLAN-puffer-engine-deltas` C9).
`FUN_0054f8b0` accumulates into the emitter's interval counter under a branch, and the two arms are
NOT alternatives — the plan's own framing ("the 200 m guard *vs.* our cap") was wrong. In DISTANCE
mode (`+0x3c` set) it adds the frame's motion length **only `if (len < 200.0)`**; in TIME mode it
adds `dt` with no test at all. Emission is then `floor(accumulator × 1/interval)` with the remainder
carried, and **neither arm has a per-frame batch cap**.
- The guard is ours now, verbatim: `Puffer.TeleportGuardMeters`, in `TrailAdvance` only. A jump
  lays no puff line along itself. `TrailBurnAt` has no equivalent because it spends VIRTUAL metres
  at a held pose — the engine has no such mode, so there is no motion length to test. `Stop()`'s
  re-home rule already covered the teleport that goes through a stop; this covers the pooled slot
  re-pointed at a new site while still trailing, which had nothing. It reaches 1,523 of the
  install's 4,535 compiled events (33.6 %), including the `spurtpuffer1..5` crash-debris trails.
- **`MaxSustainBatchesPerFrame = 8` was deleted.** It was written against a pool blowout that the
  born-dead skip and the pool clamp already prevent twice over; it never fired as a hitch guard
  across an 8-chapter regression; it bound EVERY FRAME at 60 fps on `torpufferblast` (the torpedo
  trail, the one puffer authoring a 1 ms interval, 8 compiled events), halving its authored rate;
  and on a real hitch it produced a full-pool burst on the FOLLOWING frames that the engine never
  produces. The reasoning and the one trade it leaves — loop iterations unbounded in `dt`, output
  still bounded by the pool — are in `SustainAt`'s own remark. Read that before re-adding a cap.
⚠ The 0.1 s TIME_INTERVAL both parsers fall back to is **invented**, and the engine's own answer is
  now decoded: the puffer ctor `FUN_00550100` writes `1.0` to `+0x40`/`+0x44` (and `1` to `+0x04`,
  `NUMBER`). It is left alone deliberately — on a DISTANCE state that 0.1 s is not a default but our
  synthetic still-host sputter cadence, a mechanism the engine does not have, and changing it would
  move goldens for a reason unrelated to C9. `BL-336`.
⚠ A `PUFFER_STATE` event carrying only `NAME` + `ACTIVE_STATE` is a **re-activation toggle**, not a
  definition — 1,589 of the install's 3,012 time-typed events are these, and they compile with
  `has_interval_value: false` and a filler `interval_value` of 0. They never build an emitter
  (`PufferEmitterFactory` returns null on a textureless stub), which is why that 0 never reaches
  `SustainAt`'s `Max(interval, 1e-3)` divide guard. A census that counts them as emitters will
  conclude the sub-millisecond path is 13× busier than it is.

**`PRIORITY` inflates the sprite** (`PLAN-puffer-engine-deltas` C8). `FUN_0054e6e0` scales the
drawn screen radius by `1 + K·PRIORITY`; `PufferState.Priority` (both parsers, default 0 — the
puffer object's own ctor default) is folded into `BaseSize` at spawn instead, via
`Puffer.PriorityScaleDefault` (`_priorityFactor`, read once at `Init`) rather than a config knob —
it is a decoded engine constant, not a tuning surface. `K` is `_DAT_00a06fb0`, written by
`FUN_0054d9c0` to `0.01` on the software path and `0.02` on the hardware one; this project has no
software path (the same split `DistanceAlpha`'s remark documents), so `PriorityScaleDefault` is
the hardware value. Almost certainly a depth-priority constant reused for a size nudge — this trace
found only the size use; a depth-ordering use, if one exists, needs its own trace and is not
implemented here. 47 puffers in the install author a non-zero `PRIORITY` (192 compiled events);
C1's `spew_puffer` (the waterfall splash) is one of them, which is why `c1-waterfall` is the one
golden C8 moved — every other puffer-bearing golden's emitters author no `PRIORITY` and are
byte-identical across the change.

**The whole original runtime is written up in [org/puffer.md](org/puffer.md)** (`PLAN-puffer-engine-deltas`
D10) — the function map, the emitter/particle layouts, the ctor's defaults (which settle every
"what does an unauthored key do?" question), the tick order, the accumulator, the render equation,
and one table of every place this implementation deliberately differs. Read it before adding a
mechanism here; the authored-key side stays in [formats/effects.md](formats/effects.md).
⚠ It is in `docs/org/`, NOT `docs/formats/`: the formats tree is the CC-BY public deliverable and
  states "no exe decompilation", so executable decodes live outside it (the decision is
  `PLAN-flight-model-rewrite`'s item 6, and `docs/org/flightModel.md` is the precedent).

**The fire pair is DELETED** (D10, 2026-08-10). `puffer.fireRiseScale` 2.5 / `fireLifetimeScale` 1.5
— the last invented multiplier in this file — are gone, along with their config keys and the
`fire_n_smoke` name gate; the fire family runs its authored numbers like every other puffer.
Measured (`puffer-fire-column`, `large_30sec_fire`'s own compiled payload, 30 s, drawn top): the
plan itself raised the authored column from 21.6 → 25.9 m in still air (A1's doubled sprite) and to
**32.1 m** in C1 IA1's authored wind, because B6 made friction damp toward a wind that in this
mission blows straight UP, so the plume climbs until its lifetime ends instead of turning over.
Against that, the tuned build drew **67.8 m** — the tune was contributing **2.11×** — and at the
controls the refuel-tank flames were judged *"~twice the height of the originals"*. The ratio and
the eye agree, so the pair came out.
⚠ **Do not re-add a rise/lifetime multiplier for the fire family**, and do not "restore" the keys at
  1.0 either: the point is that no knob exists to reach for. A fire that reads wrong now is a
  question about authored density (`BL-218`), the blend verdict (below), or the wind.
⚠ Every height is one seed's EXTREME (the run's tallest particle) and moves ~±1.5 m with the
  emitter's RNG stream — which is seeded by how many puffers were built before it, so the same suite
  under `-Filter` reads different decimals than the full run. The suite's assertions are ratios and
  bands for that reason; do not tighten them onto a decimal.

⚠ **Our blend verdict disagrees with the engine's — open, and it is what makes dark smoke paint over
fire.** The original selects a particle's draw routine on ONE test, "does it have a `COLORS` ramp?"
(`FUN_0054e6e0` → `FUN_0057c5c0`'s fifth argument, dispatching to `DAT_009be790` vs `DAT_009be78c`);
the sprite's darkness plays no part. `Puffer.Create` adds `SmokeLuminance`, the luminance of the
frame a particle dies on, which flips `fire_n_smoke` (`colors: null` — ramp-less, so additive in the
original) onto `blend_mix`. Since each emitter is one `MultiMesh` with `depth_draw_never` and no
per-particle sort, mixed sprites paint in instance-index order and an old near-black puff can cover
a young bright flame. The full trace is in [org/puffer.md](org/puffer.md). ⚠ Reverting the darkness
rule is NOT a one-liner: it was added because ramp-less near-black smoke drawn additively became
*more glow*, and it will move puffer-bearing goldens.

## src/Effects/WorldWind.cs
Two small types, one job: get the mission's authored wind to every puffer that reads it.

`WorldWind` is the gust model, decoded verbatim from `FUN_0054ee10`'s opening block and authored in
`weather.json`'s `WIND` block (schema and addresses: docs/formats/weather.md). A static base vector
plus a HORIZONTAL random-walk gust — heading turned by `±RANDOM_ANG_VEL·dt`, magnitude stepped by
`±RANDOM_ACCEL`, reflected through +π when it goes negative and clamped to `RANDOM_MAX_SPEED`.
`Step(dt)` once per frame; `Velocity` is the answer. All 53 `weather.zrd.json` in the install
author the identical block: `STATIC_VELOCITY (0,2,0)`, `MAX_SPEED 10`, `ACCEL 5`, `ANG_VEL 5` — a
steady 2 m/s updraft under a gust wandering a 10 m/s horizontal disc, so this is a real visual
force everywhere, not a nil default.
⚠ `RANDOM_ANG_VEL` is in **degrees** per second; the binary converts on the way into its global and
so does the constructor.
⚠ **The magnitude step carries no `dt`** — traced (`0054eea1` multiplies by the rate and nothing
else, while `0054ee3c` multiplies the heading rate by the frame delta). One `±RANDOM_ACCEL` jump
per FRAME makes the gust magnitude frame-rate dependent, and at the shipped 5-against-10 it is
effectively re-drawn every frame. Reproduced as traced, not smoothed: a `dt` nobody wrote would be
an invented breeze. Deterministic under `--det` (fixed step) off its own `Rng.Wind` stream — its
own, not `Rng.Puffer`, so the wind's frame count can never perturb any emitter's spawn scatter.

`EffectAmbience` is the seam: a small mutable holder of the per-frame world state a `Puffer` READS
but does not own, handed in at construction (`Puffer.Create`/`CreateWith`/`MakePuffer`'s `ambience`
parameter) rather than reached for. `GameSession` owns the one instance — it has to hand it to the
emitter factories at `StartSession`, long before the first weathered build exists — passes it to
`WorldEffectsFactory` → `PufferEmitterFactory`, to `ProjectilePool` (rocket trails), to
`FlightRigAssembler.Inputs` (`ThrottleSlamSmoke`'s exhaust) and to the damage lab's stand-ins, and
`WeatherRig.Tick` writes it once per frame, before the rig loop — one wind for the world, exactly
where `FUN_0054ee10` derives it, and NOT once per camera (a splitscreen session must not walk the
gust twice as fast).
C7 landed on the same seam: `CameraPosition`/`CameraForward`/`HasCamera`, written by the same
`WeatherRig.Tick` call, one camera for the world rather than one per pane. ⚠ `HasCamera` false means
**no distance fade at all** rather than one measured against the origin — the right answer for every
caller with no camera to give (unit suites, plane viewer, damage lab), which would otherwise be
near-culled wholesale by the unauthored `NEAR_FADE (0,0)` cutting at depth 0. ⚠ The original
evaluates the fade per particle per DRAW, so a splitscreen pane would get its own distances; ours is
one `MultiMesh` per emitter shared by every pane with the alpha written once per frame, so every pane
sees **player 1's** fade. A recorded divergence that costs nothing on the single-player and freecam
paths every capture uses. There is also a one-frame lag at session start: an emitter that draws on
the very first frame can beat the first `Tick` and draw unfaded once.
`EffectAmbience.Still` is the null object every unwired puffer reads (unit suites, the plane
viewer, a mission with no weather.json). It **refuses to be written**, so a session that forgets to
hand its own over fails loudly at the writer instead of silently blowing one wind through the whole
process.

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
docs/formats/fogvol.md. **This file holds two TUNE constants and no more**, each with its own
remarks: `TopAnchorHeightFactor` (A3), a shape-classification threshold decided from the volumes'
own thickness gap rather than authored data — see fogvol.md's vertical-spread entry; and
`CardVertexColorTune` (C23, 2026-08-09), which scales the card's authored vertex colour 240 → 225
in `BuildCardMesh` so the saturated card renders 208.8 instead of 222.7. ⚠ That one has **no
decoded mechanism** — C23 refuted four candidates on data (no `cloudsprite` opacity state exists
anywhere in the zrdr; `WorldLight` on C1's cards overshoots to 178.6; `fog: true` is contradicted
by the same reader's trees and the placed cloud facades; the field does not ride up with the deck)
— it is a calibrated match to the original's measured plateau (208.88 / 209.16 in two independent
above-band frames). RGB only, never alpha: alpha is the card's coverage. If the real mechanism is
ever found it REPLACES this constant. It applies to `fvol` cards alone; the world's placed
`cloudparent` facades are `SceneBuilder` geometry and keep their own authored rules. Otherwise it replaced
`CloudPuffs`, whose entire field (Count 12, Radius 620, SizeMin/Max, BaseAlpha, BandBelow/Above,
VertFull/Fade) was hand-tuned because this reader had not been found.
⚠ Built ONCE, world-anchored, no per-frame hook and NOT per rig — unlike the dome/deck/whiteout,
  nothing here follows a camera. Splitscreen shares one field; the far fade is evaluated per view
  inside the shader, which is what makes that correct.
⚠ Its GEOMETRY is shared but its VISIBILITY is per view (`B12`, which replaced `A7`'s altitude
  rule): `GameSession` moves these MultiMeshes off the default layer onto the zone layer **its own
  `fvol*` volumes author** (`WorldBuilder.FogVolumeZoneIdOf` — C1/C1C/C4 `2`, C5 `1`, and **C2B
  `−1`**, which stays on the default layer and is therefore never culled at all), and
  `Session/WeatherRig.Tick` keeps one zone bit in each camera's cull mask by that camera's own
  weather state. Same principle as the far fade: one shared field, decided per view. Never hide it
  by node visibility — that would take it out of every pane at once. The field is one MultiMesh
  per sprite kind spanning every volume, so it can carry only ONE zone; `FogVolumeZoneIdOf`
  returns −1 (ungated) rather than guessing if a chapter's volumes ever disagree.
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
⚠ **A5's map-edge continuation** (`ExtendPastMapEdge`/`EmitExtensionRegion`) tiles the SAME
  `distance`-cell field past the map rim for the chapter's map-spanning slab, if it has one
  (`FogVolumeSpec.FindMapSpanningSlab`, `FogVolumes.cs`) — engine-side, matching the terrain's own
  continuation (`MapEdgeExtender.cs`), NOT authored data (`fogvol.zrd` says nothing about content
  past the map). Bounded to the largest authored `far_fade.y` among the chapter's kinds (3,500 m
  for every shipped deck chapter) rather than to `MapEdgeExtender`'s own reach (`Rings` (5) x
  1,024 m tile = 5,120 m): a full ring to 5,120 m would place ~2.35x this field's own base count
  (extrapolated from the measured 3,500 m ring), past a sane budget for a structure built once and
  kept for the process lifetime, and every kind's own shader already collapses a sprite past its
  `far_fade.y` to a degenerate quad, so the wider ring would buy zero visible pixels. Measured:
  C1/C2B/C4 add 13,176 (1.46x base), C1C the same 13,176 (1.38x its own larger base), C5 and the
  three deckless chapters add 0 (their volumes fail the slab test).
⚠ Each extension cell draws off `Rng.NewSystemRandom(Rng.Clouds, gx, gz)` (`Utils/Rng.cs`) — a
  hash keyed on the cell's own coordinates, NOT the interior loop's shared sequential stream. The
  interior draw's own realization is therefore untouched by the extension (base counts are
  bit-identical to A3/A6/A7's own), and the extension itself is stable under `--det` regardless of
  how many cells the far_fade bound admits or what order they are visited in — there is no "next
  draw in sequence" for a runtime-computed cell set to depend on (A2's per-cell-hash trap was moot
  for the interior loop; it is exactly right here, for the opposite reason).
⚠ Extension cells are unconditionally accepted (no `FogVolumeBox.Contains` call) — there is no
  authored shape outside the map to test against — and use the slab's own constant top `Y` (every
  qualifying piece's `box.End.Y` is checked equal by `FindMapSpanningSlab`) rather than drawing
  again, exactly as the interior's own top-anchored cells do.
⚠ The eight-region decomposition (four edge strips + four corner squares around the slab's
  bounding rectangle) is chosen so every inner edge is EXACTLY `bx0`/`bx1`/`bz0`/`bz1` — the same
  coordinate the interior loop's own outermost cell is clipped to — so the join has no gap and no
  overlap by construction, not by matching a period phase across the boundary.

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
ang_momentum_damp, decayed EXPONENTIALLY (`BodyRates *= exp(-dt·damp)`, applied to the whole rate
AFTER this tick's torque is added — C24, not the explicit-Euler linear subtraction it replaced;
the two forms agree to first order per step but not at steady state, and the steady rates moved a
few % at this engine's dt = 1/60 s, all still inside tolerance, no `*Tune` refit), per axis
(PitchTune/YawTune/RollTune); yaw torque is additionally scaled by
`YawAuthorityAt` — the original's authored piecewise speed table (a low-speed floor, ramping to
full authority at `yaw_max`, then DECLINING to a high-speed floor at `yaw_fade_out`), YAW ONLY,
replacing the interim `eff`. Thrust, drag, gravity and lift integrate
on the velocity vector (speed passes through zero). Lift is a DEMAND — the airflow blended toward
the nose over the authored `liftAOAs` cosine window, `lift_accel_rate·(wind − v)` plus `nom_gravity`
on world-up, projected onto the body X/Y plane, delivered as `clamp(|·|/9.82, −5, +9)` G and capped
at `(0.75 − 0.15·Mach)·q·RefArea/Weight` (imperial q, dense band ρ = 2.2688e-3 slug/ft³) — so level
flight at zero incidence cancels weight IDENTICALLY, and the nose-chase runs at that same authored
rate. Drag is the original's parabolic polar in MACH, `C_D = 0.73·(0.12 + 0.8·M + 0.5·M²)` applied
as `q·RefArea·DragFactor·C_D` opposing velocity — there is NO induced-drag term of any kind, so a
pull costs speed only through the lift vector's own tilt. Thrust is
`EnginePower·RefArea·T_avail(M)·throttle` (LINEAR lever), `T_avail = q_ref·0.73·(0.12−M/60) /
(M·pow(1.3146, 1.41·M))` at `q_ref = ½ρ((0.84M+0.112)·a)²`, Mach floored at 0.1 — it RISES with
speed; the thrust MARGIN is what falls. Available thrust is then scaled by NOSE ATTITUDE
(`AttitudeThrustScale`, D32): `(1+0.24a)·(a ≤ 0 ? 1+0.13a : 1)` on `a = Attitude.Z.Y = −nose.Y`, so
a vertical climb keeps 0.6612 and a vertical dive gets 1.24 — a climb is PENALISED. That argument is
NEGATIVE in a climb, and a dropped sign swaps climb for dive while still flying plausibly, so
`AttitudeThrustTests` reads the term back out of the integrator and fails under the flip. Gravity acts at
full strength in EVERY attitude; the fitted `ClimbGravityScale = 0.6` is retired with its config key
(it made the sustained climb worse on the post-B14 shapes, 276.7 mph against a measured 163.1, and
was absorbing the old drag/thrust error). Nothing in the force path is fitted.
Bank couples straight into rate, the original's coordinated-turn cheat: `0.205·(starboard·up)` into
yaw (signed) and `0.165·|starboard·up|` into pitch (always nose-up), plus `0.205·|bodyUp·up|` into
PITCH once inverted — the same 0.205 constant, not a third number. Both vanish at wings-level
upright, so nothing that flies level can see them; they make the banked turn FASTER, so they are
not the missing explanation of the original's 1.6×-slower banked pull (`BL-095` owns that).
`return_rate` is the WEATHERVANE torque, not damping: `WeathervaneTorque()` adds
`return_rate·(α/2)·unit(nose × VelocityDir)` (body frame, × RecInertia) into the same command, and
`damp` is `ang_momentum_damp` alone — a spring-damper, second order, where folding `return_rate`
into the damping coefficient was a first-order lag. Its axis is ⊥ the nose, so it can never reach
ROLL, and it vanishes identically at α = 0, which is why every stick-centred, wings-level scenario
is untouched — but a SUSTAINED FULL-STICK manoeuvre holds α ≈ 18° and it opposes the stick there,
which is what `PitchTune` 0.89 / `YawTune` 1.57 re-pin (C23; it does not close `BL-147`, it closes
about a sixth of it).
`Alpha` (deg) is angle(nose, VelocityDir) — an emergent LAG, not modelled
incidence, and now instrument-only: no force reads it — and the
stall is TWO DIFFERENT mechanisms, not one margin split two ways (B15). `isStalled()` is the
airframe's own `StallSpeed` — the speed at which the SAME aerodynamic ceiling lift uses,
`clMax(V)·q·RefArea`, can no longer equal `VehWeight` (a load factor of exactly 1, not
`nom_gravity/StandardG` ≈ 2.04 — the decode's own worked example, reproducing 75.5/309 mph for the
fallback aircraft under the dense/thin bands, only matches the bare-Weight read). `IsStallWarned()`
is unchanged: the lamp at a fixed 0.30 fd (`StallFraction` = speed/fd_speed), which led the
Bloodhawk's break by 2.64 sim s in the clip that measured it (`BL-148`/`CAP-06`) — never drive both
cues off one number. ⚠ The Bloodhawk's own computed `StallSpeed` (56.5 mph) no longer reproduces
that clip's ~76 mph nose-drop — the fixed `0.25 fd` this replaced only matched the footage because
0.25 × the Bloodhawk's fd_speed happens to sit near the FALLBACK aircraft's stall speed, not the
Bloodhawk's own (a coincidence of wing loading). Recorded as a decode-vs-footage conflict, not
closed by switching G-conventions to fit one clip. The autogyro moves most of the eleven airframes
(57.0 → 18.5 mph, its huge ref_area relative to weight), not the Balmoral (44.2 → 45.5 mph, nearly
unmoved) as the plan predicted.
⚠ The ±5/9 clamp is a LOAD FACTOR in G, never an angle — re-deriving it as degrees gives a model
  that looks right at small inputs and diverges at the limits. The authored `highGs`/`lowGs`
  control limiters are a WIDER, inert pair (`BL-095`) and must not be folded into it: measured peak
  demand is 2.13–5.01 G against `highGs[0]` = 9 and peak α 8.9–25.6° against `maxAOA` = 46° on all
  eleven airframes, so neither limiter can fire and neither is implemented (D33 —
  `ControlLimiterTests` asserts each airframe against its OWN loaded thresholds, and
  `LoadFactorDemand` is the pre-clamp instrument it reads; if it ever fails, the limiter gates only
  input OPPOSING the current rotation). A third authored-inert feature, the same family:
  `high_speed_pitch_fade` [1000,1001] mph is beyond even
  the model's own hard dive ceiling (1.75×fd_speed, 528.5 mph at its highest, the Bloodhawk) on all
  eleven airframes (C24) — deliberately NOT implemented; do not add it "for completeness".
⚠ The three *Tune rates are pinned to the original off cockpit-gauge video
  (`analysis/video-flight-calibration/`) and are not free TUNEs. They re-pin only when a decoded
  mechanism moves the steady rate they hold (C21's yaw curve, C23's weathervane) — never to chase a
  transient or a feel report (`BL-147`). The 2003 m altitude clamp
  (`BL-094`/`CAP-03`, traced to C1B IA1 only) is a numerical backstop, not a modelled limit.
  Accepted artifacts, not bugs: loop energy pump, steep-climb equilibrium, stall hang.
⚠ The drag polar's variable is MACH, never `C_L` — the original passes `C_L` to its drag routine
  and never reads it. The same three coefficients read as a `C_L` polar give a drag floor and an
  induced-drag term the original does not have; only the raw bytes settle it (`docs/org/flightModel.md`).
  The ≈2–3.6× gap against `CAP-05`'s zero-thrust points is a RECORDED decode-vs-footage CONFLICT,
  not an open scale question: the force→acceleration chain is byte-verified conversion-free
  (`docs/org/flightModel.md`, "The force scale — settled"), and no constant can close the set —
  a rescale that fixed the decel breaks the accel row the same footage pins. Never refit the
  polar/thrust coefficients against it; `accel-150-290`, `decel-290-150`, `sustained-turn-speed` and
  `sustained-turn-sink` (both riding the unattributed turn-rate gap, `BL-095`) sit informational in
  `FlightEnvelopeTests` with their owners named in the rows. `terminal-dive` came BACK to asserting
  when the attitude scale landed (−5.3% → +0.2%; the suite asserts 7 again).
  The sustained climb joins that same recorded-conflict list from the other side: it settles ≈25%
  FAST (204.0 mph against a measured 163.1) and is not tuned; the leading candidate is that the
  original held a large α there (its clip is a 90° pull) where the probe holds α = 0, and at a 90°
  nose with the measured 56° path the same force path balances to −3.3%. `CAP-20` would settle it.
⚠ Lift is BANK-INDEPENDENT and the knife-edge sag has NO term of its own (D31, settled). At 90° of
  bank the body yaw axis is horizontal, so C22's `0.205` bank→yaw IS the sag and C23's weathervane
  deepens it — the footage's shape, from the original's own constants. The bounded
  `KnifeNoseSag`/`KnifeNoseRate` pair is retired: it double-counted the onset (−7.3° at +3 s against
  a measured −4.9°, −4.9° without it) and, keyed on `1 − |bodyUp·up|`, fought every wings-level pull
  at up to 11.5 °/s. Do not add one back. `wingVert` survives ONLY in the nose-chase floor
  (`KnifeAlignFloor`), kept on measurement not decode — the original holds its nose 4.8° → 8.3°
  below its path and removing `wingVert` collapses that to 1.9° → 0.5° while the 36 s loss rises
  1087 → 1334 m against a measured 540. Still open, and now one number: the whole banked rotation
  runs ≈1.6× fast, the same ratio as `sustained-turn-rate` (`BL-095`).

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

## src/Flight/SpeedCue.cs
Loads `cuepuffer1..3` directly from the chapter's `speed_cue.zrd` and drives exactly one through
`Puffer.Emit` at the aircraft pose. Camera altitude selects the authored 30/15/8/15 m density
bands; within 50 m AGL none emits, while the script's empty branch above 1500 m preserves the
current selection. One instance is built per player and its puffer renderers are stamped onto that
rig's visual layer, so splitscreen panes never see another pilot's private speed cue. It shares the
session `EffectAmbience`, so the authored 500–600 m camera-distance fade and wind apply. `Reset`
hard-clears all three on crash/respawn so a teleported aircraft cannot bridge its old position.

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
FlightModel, CameraController + CamParams, SpeedCue, Loadout + ProjectilePool (guns/rockets),
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
In a splitscreen stunt race, a finished pilot continues normal flight, collision, weapons and
crash/respawn while `StuntMission` holds their timer/objectives and `MarkerHud` holds their placing;
this prevents their finish pose from obstructing another pilot's gate.
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
⚠ The zone-gate band (`Mech3.ZoneGate.LayerBand`, bits 13–15 = layers 14–16) is the other named
  allocation and is NOT per player: three shared layers, one per gamez `zone_id` 1/2/3, so
  `Session/WeatherRig.Tick` can gate world content per CAMERA by cull mask (`B12`). It sits just
  below the player band on purpose — every mask this file builds includes all three, so the gate is
  something that NARROWS and a mode, chapter or `--no-zone-cull` run that never applies it renders
  every zone as before. Instances are MOVED onto a zone layer, off layer 1, or dropping the bit
  would change nothing. It is allocated in `Mech3` rather than here because `SceneBuilder` stamps it
  at build time, node by node. (Bit 15 alone was `CloudFieldLayer`, the A7 altitude gate over the
  two ambient cloud populations, which this band replaced.)

## src/UI/ScreenFlash.cs
The `FBFX_COLOR_FROM_TO` full-screen wash — a close HE, AP or flak burst ramping the whole picture
from one RGBA to another over the event's run time. **One ramp state, one hidden `ColorRect` per
rendered view** (`HudLayers.WorldOverlay`, under each rig's `HudParent`, built with the rigs so
every runtime can be handed the same `Play` sink). `AnimRuntime`'s handler pushes `(from, to,
run_time)`; the node lerps in RGBA on `GameClock` sim time and ends — it does NOT hold the `to`
colour, because the original re-arms its frame-buffer object for the current frame only and a
completed chain simply stops re-arming. `Play` **replaces** whatever is running, which is the
original's composition rule literally: one process-wide state a second burst overwrites (decode in
`docs/formats/anim-definitions.md`).
⚠ **Per view, not per window, and per-rig is what makes that right.** The original is single-view,
  so "the whole picture" is unambiguous there; in splitscreen each pane IS a picture, and painting
  the window instead would wash the 2 px gutters and the empty 3P quadrant, which are neither.
  One state drives all panes, so all panes flash together as the single global state implies.
  Under the HUD (unlike the lens flare's sun wash, which `CAP-13` measured whitening the
  instruments — there is no footage saying this one does) and unreachable by the launchscreen and
  the scoreboards, which sit at `HudLayers.Board`.
⚠ The layer stays **`Visible = false` with no ramp running**, so a session that never sees a close
  burst renders exactly what it rendered before this existed — that is what keeps the golden set
  byte-identical rather than a claim about a transparent rect costing nothing.

## src/Flight/PlayerRig.cs
One rendered view's state bag: index, camera, optional `SubViewport`, `HudParent`, `VisualLayer`,
the player's FlightController, and private camera-anchored copies (`Horizon`/`Deck`/`Whiteout`) —
those re-anchor to the view's camera every frame, so N players need N of each.
⚠ The ambient cloud field is deliberately NOT one of them (`BL-273`): the authored fogvol clutter
  is world-anchored static geometry every pane shares, and the `Puffs` slot went with `CloudPuffs`.
  It still gets a per-pane ANSWER, just not a per-pane copy — `WeatherRig.Tick` gates it (and the
  world's `cloudparent` clusters, and every other zoned world node) through `Camera.CullMask` over
  `Mech3.ZoneGate`'s zone-layer band, so the per-view decision lives on the rig's camera rather
  than in a duplicated subtree (`B12`, which replaced `A7`'s single cloud-field layer).
⚠ Single player holds exactly one rig wrapping the main-viewport camera with `VisualLayer` 0, so
  every loop over the rigs degenerates to the old single-camera code.
⚠ In splitscreen the camera's parent is a `SubViewport`, not a Node3D — local `Position` IS the
  world transform, so per-frame anchoring reads `Camera.Position` directly (correct in both modes).
`CameraWeatherState` (1/2/3, `PLAN-weather-decompile-match` A2) is the same shape as the deck
regime: a per-rig field, not shared, because splitscreen panes can sit in different states at the
same instant. Written each frame by `Session/WeatherRig.Tick`; rig 0's value drives the per-state
fog switch there (B11) — the fog globals are session-wide, so only rig 0's is read for them.

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
  — `Resolve` requires a registered `HEALTH > 0` anchor, and the C2 SeaHangar's
  `sgh_door1`/`sgh_door2` have none: they are driven by `sghangar-opensgdoors`, a HEALTH-0
  `OnStartup` open-the-doors animation, so they are animated scenery and read as such.
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
where most `PUFFER_STATE`s fire. Two debug options ride the clutter step: `Options.NoClutter`
(`--no-clutter`) skips the clutter build outright, leaving `Clutter` null exactly as a chapter with
no templates does, and `Options.DebugClutterFlag` (`--debug-clutterflag`) hands the flag view to
`WorldBuilder`, then stamps every clutter MultiMesh with a full-strength `SceneBuilder.ClutterColor`
tint and prints the flagged/clear polygon census. The blue is stamped per instance rather than per
material because both clutter paths share the placed world's materials — colouring those would
repaint the ground with them.
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
sharing one `BuildState`; menu and CLI share that one build path. Owns the session `GameClock`,
`StartupProfile` and the `UI.ScreenFlash` overlay (built with the rigs, since it needs one surface
per view, and handed as a sink to the world runtime and — via `WorldEffectsFactory.ScreenFlash` —
the world-effects one); delegates to the `src/Session/` clusters (LiveryResolver, SpawnPicker,
PlaneRoster, FlightRigAssembler, WorldEffectsFactory, WeatherRig, LensFlareRig) and to `Testing.ProbeRunner`/
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
  - **Per DOME, not per rig** (`B14`): a rig's `Horizon` is now a bare container at the camera
    holding one dome per built horizon zone (`WorldBuilder.DomeZonesToBuild`), and each dome is
    fitted on its OWN AABB. That is what keeps the flown dome's scale exactly where it was when a
    second one is added beside it — C1's `zone1` is 8813 m to `zone2`'s 8744, and a shared fit
    would have let the new dome move the old one's scale. Measured: the pinned above-deck C1 pose
    and the C1B clamp canary are both **byte-identical** across B14.
  - ⚠ **No vertical-only scale split, and the reason is geometric** (`B14`; `B15` owns the audit).
    A camera-CENTRED, UNIFORMLY scaled backdrop is scale-invariant in everything a frame can show:
    the dome is unfogged (`fog: false`) and has no parallax, so the only observable is the
    ELEVATION each feature subtends, which a uniform scale preserves exactly and a Y-only scale
    would change. Scaling Y by 1 while XZ stays 2.5 would flatten C1's ceiling cap from 46.9° to
    ~24° of elevation — a visible distortion, in service of an "authored 396.4 m cap" that the data
    does not contain (see `docs/formats/weather.md`'s zone-1 ceiling table: 396.4 is `bbox_mid.y`,
    a bbox midpoint; the cap polygon is at +2792.8). So the domes keep one uniform fitted scale and
    `B15` inherits the question of whether the fitted 2.5× itself is right, not of splitting it.
  - **The rule, audited and stated (`B15`, 2026-08-09).** *Each dome is anchored at its own rig's
    camera and scaled UNIFORMLY by `HorizonScaleFor(that dome)`.* Justification, in three steps:
    the dome is camera-CENTRED (zero parallax) and `fog: false` (no distance cue), so the only
    quantity a frame can carry is the ELEVATION each feature subtends; a uniform scale about the
    camera maps every dome-local direction to itself, so it preserves every such elevation exactly;
    fitting it PER DOME is what keeps each one inside `Camera3D.Far` without any dome's size
    deciding another's. **The scale is therefore observable in exactly two places, neither of them
    an authored angle:** (1) whether the dome's far wall survives the far plane — C1B's clamp
    (~1.65×) exists for that and is the regression canary; (2) where the dome INTERSECTS world
    geometry — terrain (why 2.5× exists at all) and the deck annulus, whose 20,480 m half-span
    brackets C1 from below at **20480 / 8813 = 2.32×** (`zone1`) and **20480 / 8744 = 2.34×**
    (`zone2`). So 2.5× is bracketed on both sides, not free, and the two brackets coexist only
    because the clamped chapter (C1B) authors no deck.
  - **What could contradict it, and what the record says.** (a) *The climb ordering.* A
    camera-anchored ceiling can never be reached by climbing, and that is exactly the original's
    behaviour on record: the user at the controls of the original (`PLAN-overcast-match` A7,
    2026-08-08) reported that "climbing below the deck, the texture's look is *exactly the same* at
    every altitude — a world-fixed sheet would grow and parallax", and `CAP-12`'s six deck crossings
    record the whiteout taking over instead (first wisps ~982 m, full obscuration 1003–1085 m,
    clear above ~1128 m) with **no ceiling reached at any altitude**. Nothing in the footage record
    describes penetrating the below-deck ceiling. (b) *The cap rim's elevation.* Only C1 can show
    one: its zone-1 dome is the only one built from two materials (a `sky2.tif` vault under a flat
    `FOG_COLOR` 176 cap), so the rim is a visible boundary — B14 measured **48–52°** against the
    authored **46.95°**. C1C/C2B (`Colored` 176), C4 (192) and C5's `zone3` (16) are each ONE flat
    Colored material with `lighting: false`, so their caps have no rim any render can find and no
    scale of any kind is observable there; measured at a below-deck pose looking up 45°
    (`--pos=-7325,192,-3829 --direction=0,0.7071,-0.7071`): C1C **175.94** and C4 **191.84**, flat
    to their authored colours, against `--sky-zone=zone2`'s 156.8/157.0/158.4 as the able-to-fail
    control. Pinned in `CSVM.Tests/HorizonDomeTests.cs`.
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
⚠ `NewSystemRandom(subsystem, cellX, cellZ)` (A5) is a SEPARATE, coordinate-keyed generator, not a
  per-instance draw off the subsystem's shared stream: the seed is `splitmix64(seedFor(subsystem) ^
  fnv1a("cellX,cellZ"))`, so it never touches `Streams` and is a pure function of the master, the
  subsystem and the two coordinates alone — no dependency on call order, count, or any other
  subsystem's draws. Added for `FogVolumeClutter`'s map-edge continuation, whose cell set is a
  runtime computation (bounded by each kind's `far_fade`) rather than a fixed walk over authored
  volumes, so there is no "next draw in sequence" for it to be. Only touches `System.Random`, not
  `Godot.RandomNumberGenerator`/`GD.Seed` — deliberately, so it (unlike everything else here) is
  callable off-engine and pinned directly in `CSVM.Tests/RngTests.cs`.

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
The 32 registered in-engine assertion suites cover plane/loadout bindings (stock and, since M3 B4,
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
`fbfx-flash` reuses `effect-template-mesh`'s `WithEffectStage` host to play `he_ground_effect` at the
camera (so its own `PLAYER_RANGE` gate passes) with the runtime's `ScreenFlash` sink recording:
it asserts the six wash steps' authored run times AND the gaps between their fires, since the
per-step run time alone is reported correctly even by a handler that returns 0 as its duration and
fires all six in one instant. Shown able to fail exactly that way. The chain's total gets one step
of headroom per gap — the authored run times are exact multiples of the step but not of binary
float, and three of the five gaps land one step late.
`ordnance-burst-timeline` is `PLAN-anim-original-match` D31's proof: it plays `he_ground_effect`,
`flash_effect` and `sonic_ground_effect` on its own miniature world-effects stage and matches the
WHOLE recorded `OnEventDispatched` log of each — every sequence, every event, in its sequence's
order, at its authored instant — against a table read off the def JSON by hand. Order is asserted
by CONSUMPTION (a row is claimed by the first lane whose next unconsumed step it matches on
sequence/index/kind/name, so an early or duplicated row matches nothing and is reported stray),
which is what a membership check cannot do and what every Wave B item needs to be measured at all:
`large_fireball`'s parked `stop_p1trail` must dispatch nothing (B12 — the shown-able-to-fail case),
`sonic_light_seq` must run exactly twice, the second pass restarting at 1.2 s (B11), and the
`START_TIME ANIMATION`/`SEQUENCE` gates must land on 1.2/1.5 s. It drives at **1/240 s**, four
times finer than `SequenceRunner.AnimFrame`, because the authored gaps go down to 0.01 s and none
of the three defs carries a `LOOP` for the AnimFrame floor to matter to. Two traps are closed by
construction rather than by assertion order alone: the staged template roots are DERIVED
(`EffectCatalogue.StageRootsFor` against the chapter gamez, which throws on an anchor that resolves
nowhere) instead of hand-listed, and the TTL is 32 s — inheriting `--effects-test`'s 0.3 s would
truncate the 1.2 s wash while everything else still read green. The full log of all three lands in
`.scratch/ordnance-burst-timeline.txt`.
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
audio, the throttle-slam exhaust smoke and chapter-authored `SpeedCue` (private visual layer per
rig), this player's stunt run + marker/scoreboard/race entry
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
`(SessionSpec, Node3D worldRoot, Func<Vector3> playerPosition)`, plus a settable `ScreenFlash`
sink it hands to the effects runtime — the three defs carrying an `FBFX_COLOR_FROM_TO`
(`he_ground_effect`/`ap_ground_effect`/`flak_effect`) all play there.
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
**What the original does per frame is written up in [org/weather.md](org/weather.md)** — including
the retired `DeckCeilingHeight` fits, which must not be re-derived.
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
⚠ `SetFogVolumes(volumes, spec)` (`PLAN-weather-decompile-match` A2/C21) is called separately
  from `Build`, same reason as `SetDeckCenter`: the fog-volume census is chapter/world data
  (`GameSession`'s own `FogVolumeSpec.VolumesOf`/`Load`), not mission weather. `Tick` feeds both
  it and the resolved `WeatherState` into `WeatherState.CameraWeatherState` once per rig, per
  frame, publishing the result onto `PlayerRig.CameraWeatherState` (logged only on a change, at
  debug verbosity). Never calling `SetFogVolumes` at all just keeps every camera at state 1/2 and
  every frame's volume whiteout at 0.
⚠ **The whiteout overlay is ONE surface with TWO sources** (`C21`, 2026-08-09, Decision 6): the
  `CLOUD_COVER` band's altitude whiteout, and — where the chapter arms `fog_zone` — the `fvol`
  volumes' own curtain (`Mech3.FogVolumeWhiteout.Density`, evaluated per rig per frame from that
  camera's position). Screen-space, because that is the original's own mechanism: `FUN_0042ee40`
  computes ONE camera-space density per frame and blends the frame with it; it never builds
  per-volume fog meshes, and re-deriving one would be a different renderer, not a fidelity fix.
  - **They union** (`a + b − a·b`, the binary's own between-volume combiner) with the volume
    curtain composited OVER the band — union alpha, each layer's colour weighted by its share.
    ⚠ **Unmeasurable in shipped data and deliberately so**: C5 is the only chapter arming
    `fog_zone` and its band sits at 9950–10150 m, ~9.8 km over its highest street strip. A union
    rather than a pick, so a future chapter authoring both cannot silently lose one.
  - **A zero volume density takes the pre-C21 path verbatim**, which is what makes the seven
    disarmed chapters byte-identical by construction rather than by measurement.
  - **The colour is `fogvol.zrd`'s `fog_color`**, defaulting to the mission's `CLOUD_COVER`
    `TOP_COLOR` (`FUN_0044e010`) and then to `WhiteoutFallbackColor`. ⚠ It is a framebuffer (sRGB)
    value and is NOT linearised: this paints a `ColorRect`, not a shader input, unlike everything
    `ApplyFogGlobals` writes. Measured rather than assumed — at C5's half-ramp pose the sRGB-space
    composite predicts 17.26 against a measured 17.29, the linear-space one 18.22.
  - ⚠ C5's `fog_color` is `[16,16,16]`, so this "whiteout" is nearly a **blackout**, matched to its
    own `ZONE3` `FOG_COLOR` of 16 — and its ramps are 16 m each, not the engine's 400/20. Do not
    read a dark C5 street transition as a bug.
  - The overlay is now built for `HasCloudBand` **or** an armed `fog_zone`, so a chapter that armed
    one without authoring the other would still have a surface to paint on. No shipped chapter is
    in that state; the condition exists so the two cannot drift.
  - `fvol whiteout: armed — N volume(s), approach … interior decay … colour …` prints once per
    session (C5 only), and `Tick` logs the density at debug on each 0 ↔ non-0 crossing and each
    0.05 move — the evidence a probe reads, since a curtain that never fires and one that fires at
    0.02 are the same picture in a night frame.
⚠ **The FOG follows that state; the DOME does not** (`PLAN-weather-decompile-match` B11,
  2026-08-09, `FUN_00472ea0`). After the per-rig loop, `Tick` hands rig 0's state to
  `FogStateTrigger.Next`, which resolves `WeatherState.ZoneForState(state)` (state *n* →
  `zone<n>` through the file fallback) and answers non-null only when the state CHANGED; on an
  answer that also changes the live zone, `ApplyFogGlobals` rewrites `csky_fog_color`/`_range`/
  `_alt`/`csky_world_light` for the new zone. `_activeZone` — the dome's zone, resolved once at
  `Build` from the mission's names plus the horizon census — is deliberately left alone; below the
  deck a deck chapter therefore flies ZONE1's fog under ZONE2's sky, which is what the original
  does and what `B14` will reconcile geometrically.
  - **Edge-triggered, not per-frame**, and that is a requirement: the C26 rim annulus reads these
    same globals, so a per-frame rewrite would shimmer at the boundary. `FogStateTrigger`
    (nested, pure, unit-tested — `CSVM.Tests/FogZoneStateTests.cs`) counts its `Applications` so a
    test can catch a regression that no screenshot can see.
  - **Rig 0 drives it**, because these are GLOBAL uniforms — one set per session, unlike the
    whiteout overlay and the deck regime, which are per rig. Splitscreen panes on opposite sides of
    the deck therefore share player 1's fog. Pre-existing (`SetupWeather` always wrote one global
    set), not introduced here; per-pane fog needs per-instance uniforms, the hazard the
    `csky_fog_on` note below exists about.
  - **`ApplyFogGlobals` is the only writer** and is idempotent; `SetupWhiteoutAndPrecip` holds the
    node-building half of `SetupWeather`, so a state change never rebuilds an overlay.
  - The original's per-zone `CLIP_RANGES` far is still NOT applied — a **kept divergence** (fog
    hides distance in the remake; see `docs/formats/weather.md`'s `CLIP_RANGES` note). `ZONE3`'s
    300 m far inherits that rule at `C22`.
⚠ **State 3 = ZONE3, and it is a pure consumer of the machinery above** (`C22`, landed 2026-08-09,
  no code change). `FogStateTrigger`/`ZoneForState` are already generic over the state number, so
  state 3 (armed only where `fog_zone` is set — C5 alone) walks the identical edge-triggered path
  as state 2: entering a C5 street volume flips `csky_fog_range`/`_alt`/`_color`/`csky_world_light`
  to `ZONE3`'s 50–250 m / `[16,16,16]` / its own `SUNLIGHT_*` block; leaving restores `ZONE1`.
  **The switch is hard, deliberately** — no extra smoothing is authored, matching the original,
  because C21's in-volume whiteout curtain already saturates the frame at the boundary (measured:
  1 m inside reads sd ≈ 0.07, essentially flat `[16,16,16]`), so a discontinuity in the fog globals
  underneath it is never seen. Verified live (`.scratch/c22/`, `--freecam --chapter=C5
  --pos=-2000,182,-1792 --no-zone-cull --det`): the log reads `camera state 3 -> fog zone 'zone3'
  — fog 50-250 m … world light 1.00`; the same pose under `--sky-zone=zone1` never emits that line
  (the trigger is disarmed, Decision 5) and renders a different frame (pixmd5 differs, 21.8% of
  pixels). All 8 C5 missions author both `ZONE1` and `ZONE3` (`CSVM.Tests/FogZoneStateTests.cs`'s
  `EveryC5MissionAuthorsZone1AndZone3`, one row per mission, not just `IA1`).
⚠ An explicit `--sky-zone=` skips the geometry correction and is honoured literally, empty dome and
  all (`SkyZoneExplicit`) — it is the flag for looking at a named zone, and `analysis/`'s recorded
  repro poses depend on it. The weather-file fallback still applies to it, as before. Per Decision 5
  of `PLAN-weather-decompile-match` it now ALSO disarms the state machine above
  (`FogStateTrigger(stateDriven: false)`): an inspection pose that silently swapped zone with
  altitude would not be reproducible.
⚠ **`GlobalShaderParameterSet`, never `Add`.** `GlobalShaderParameterAdd` runs once per process in
  `Launcher._Ready`; `Build`'s fog/whiteout writes must stay `Set`-only, or every in-process menu
  relaunch that flies a second foggy mission crashes on the duplicate `Add`.
⚠ **`csky_fog_range` carries the AUTHORED `FOG_RANGES` unscaled** (`B15`, 2026-08-08). The
  `fogRangeFactor = 2.0` that used to halve them is deleted: `VIEWING_RANGE` ships HIGH
  `FOG_SCALE` 1.0 in all eight chapters and every multiplier in that block is ≤ 1, so no shipped
  datum shortens a range. Do not re-introduce a scale here — a chapter that looks over-fogged is a
  question about the fog COLOUR, the surface's own brightness or the mix space, not about a
  factor. `SetupWeather` logs the applied range beside the authored one, which is how an A/B on
  this proves which range it rendered with. The fade between `near` and `far` is a **linear** ramp
  in `shaders/csky_atmosphere.gdshaderinc` (the gamez `world1` node's `fog_state == 1` = LINEAR,
  asserted by the reader in every chapter), not the `smoothstep` it shipped with; the ALTITUDE
  term keeps its smoothstep on purpose — see that file's own comment and `docs/formats/weather.md`.
⚠ `SetDeckCenter` (and `SetDeckUndimmedMeshes` beside it) is called separately from `Build`,
  whenever a chapter's cloud deck geometry loads
  (`GameSession`'s `cloudDeck != null` branch) — broader than "this rig has weather", so it is
  guarded with `_weatherRig?.SetDeckCenter(...)` rather than assumed non-null. An empty
  undimmed-mesh map is a valid state (no deck, or a caller that never supplied one): the deck then
  simply stays as built, which is the below-band look.
⚠ **The deck is a WORLD-FIXED sheet at its own authored altitude, at every camera altitude**
  (`B13`+`B14`, 2026-08-09). `DeckRegime(cameraY, bandCentre, authoredY)` returns `authoredY`
  unconditionally; the only thing the `CLOUD_COVER` band centre still decides is which lit variant
  the sheet wears (`DeckDimmed`, below). The rule is pure — assert against it, not against the loop.
  - ⚠ **RETIRED (`B14`): the below-band "ceiling carried with the camera at
    `camera.y + DeckCeilingHeight`".** `A7` decoded the BEHAVIOUR correctly at the controls (a
    ceiling whose texture looks identical at every altitude on the way up) but attributed it to the
    wrong object. The tiles are ordinary world meshes carrying `zone_id 2`; nothing in the
    decompile moves them, and below the deck the original culls them outright — what the player
    sees overhead there is `horizon/zone1`'s own camera-anchored, UV-scrolled dome, which of course
    looks the same at every altitude, being anchored to the eye. Both fits of the constant are void
    with it: `A7`'s 400 m (wrong texture period) and `C21`/`C25`'s 135 m, which fitted the
    **authored fog ramp** to a surface the original does not fog at all — every horizon model in
    every chapter is authored `fog: false`. That same fact is the standing explanation for
    `PLAN-overcast-match` `B15`'s "the original's ceiling texture survives to ~12.6 km" anomaly.
  - ⚠ **The above-band floor sits at the tiles' OWN AUTHORED altitude, not the band centre**
    (`PLAN-weather-decompile-match` B13, 2026-08-09 — supersedes `A7`'s band-centre pin here,
    which the disproof-4 decompile finding showed was C4's own coincidence: C4's authored altitude
    equals its own centre, 1050, so the pin only ever looked right there). `authoredY` is
    `_deckAltitude`, set by `WeatherRig.SetDeckAltitude` from `WorldBuilder.CloudDeckAltitude` —
    the coverage-winning altitude bucket `FindCloudDeck` already classifies the 144 tiles by,
    never a hardcoded 960/1050. `WorldBuilder.CloudDeckAltitudeOf(gamez)` is the same computation
    as a pure static function of the raw `GameZ` data (no scene build), so a test can pin a
    chapter's authored altitude against the extraction cheaply — see `CSVM.Tests/DeckRegimeTests.cs`.
    C1's above-band floor drops 87 m (1047 → 960) from the pre-B13 pin; C4's does not move at all
    (1050 → 1050, the coincidence above). No golden in `analysis/goldens` holds an above-deck pose,
    so the whole set is byte-identical across this item — confirmed by an A/B against the
    pre-B13 build, not merely asserted.
  - The crossing itself is still pinned at `bandCentre` (Decision 1: unified with neither the zone
    state's own `CloudCoreBottom` threshold nor moved to chase the new target), and as of `B14` the
    deck's altitude no longer jumps there at all — only its lit variant does.
⚠ **The band-centre crossing is a JUMP of the deck's brightness, masked only by the whiteout
  core.** It is
  placed at the band centre precisely because that is the middle of the fully-opaque core
  (C1: total in 1032–1062) — measured: the ladder frames at 1035/1046/1048/1060 m are bit-identical
  flat white, before and after the brightness half was added. Moving the flip altitude, or thinning
  `CLOUD_COVER`'s `THICKNESS`, makes it visible;
  if a pop ever shows, that is a finding about the whiteout band, not a licence to move the flip.
  `DeckRegimeTests` asserts the masking against the AUTHORED band, so the data moving fails a test.
⚠ **`DeckDimmed`: `C22`'s SUNLIGHT dimming is the BELOW-band regime only** (`PLAN-overcast-match`
  C23, user's fork verdict 2026-08-09). The two regimes are two different objects — an underside
  and a top — and the evidence splits the same way: the original's underside reads 167.7 (ours
  168.9, dimmed) while **no pixel in any original above-band frame falls below `FOG_COLOR` 175**,
  which a 168.9 surface cannot satisfy at any fog setting (fog only pulls TOWARD the fog colour).
  `Tick` applies it as a per-INSTANCE mesh swap on that rig's own deck copy, between the two
  variants `WorldBuilder.CloudDeckUndimmedMeshes` built (lit-ness is baked into the material — a
  shader variant, see `Mech3/SceneBuilder`), resolved once per deck node and written only on a
  change. Per instance and never per material for the same reason the gate is a per-camera cull
  mask: two panes on opposite sides of the band must be able to disagree. Inert where
  `WorldLight` is 1.0 (C4) by construction. `deck lighting: N of M deck tile(s)…` is printed once
  per session — `0 of 144` is what a broken RID lookup would look like.
~~⚠ **`DeckCeilingHeight` (135 m) is a TUNE matched to one original still, and it is the only free
  parameter in the model**~~ **RETIRED 2026-08-09 (`B14`) — the constant is deleted, and BOTH of
  its fits measured the wrong surface** (see the deck entry above: the below-deck ceiling is the
  unfogged `horizon/zone1` dome, not the deck sheet, so a fit against the deck's authored fog ramp
  has nothing to fit). Kept below as the record of how it was derived, because the next reader
  must not re-derive it. (`C21`/`C25`, 2026-08-08). Supersedes `A7`'s 400 m, which fit apparent
  mottling scale against the WRONG texture period — the deck's authored UVs make
  `cloudlayer.tif` repeat every 2048 m, not the 1024 m tile `A7` assumed — and against a render
  that is heavily mip-blurred at grazing angles where the original is not, both of which biased
  that estimator's K upward. `C21` re-derived it by fitting the SAME still's ceiling to the
  zone's own **authored fog ramp** instead of to texture appearance (the deck tiles author
  `fog: true`, so this is the ceiling's actual fade mechanism, not a proxy for one): bracket
  110–155 m. That fit also explains B16's "sky stripe below the deck" as a pure `K` artifact —
  the sheet's rim sits at `f·K/6144`, and at 400 m it landed exactly where the original still
  shows deck, exposing the un-fogged dome behind it; at 135 m the rim sits inside both the
  fog-saturated band and this pose's own terrain onset, so it can never be seen. The user picked
  135 from that bracket at the controls, side by side against 400 and the original still
  (`.scratch/c25/k-decision-montage.png`, 2026-08-08) — changing K revisits `A7`'s approved look,
  which is a call the fit alone cannot make. One value for every deck chapter: C1's river still
  is the only original frame that can measure one; the constant's own comment carries the full
  derivation.
⚠ **`f·K/halfSpan` has a second free knob besides `K`, and `C26` (2026-08-09) fixed it: the
  144-tile sheet's own radius.** `C25` left the sheet's textured half-span at 6144 m (12×1024 m
  tiles ÷ 2), which puts the rim at 13 px — inside which the dome WALL's own authored base-ring
  gradient (`docs/formats/weather.md`, "the wall's LOWEST ring") is still visibly darkening, the
  residual the item traced (not a fog or `K` defect; the wall renders correctly). `WorldBuilder.
  AddDeckAnnulus` extends the CEILING alone — an untextured, already-fog-saturated annulus around
  the 144 tiles, never more textured tiles (the deck census stays 144) — out to a 20,480 m
  half-span, dropping the rim to ~4 px, where that same gradient has lost only ~2 units. `K` itself
  is untouched; only how far the ceiling that hides the wall's base reaches.
  ⚠ **`B14` retires that DERIVATION but keeps the geometry.** With the sheet no longer a below-band
  ceiling there is no `K` and no below-band rim; the 20,480 m half-span is unchanged and is now
  justified by the ABOVE-band regime alone, where it does the same edge-hiding job for a floor seen
  from above (rim at `f·(camY − deckY)/halfSpan`). Nothing about the annulus changed, so no golden
  moved for it.
  ⚠ **`D31` measured it and handed the number to `BL-328` rather than re-picking it** (2026-08-09):
  the above-band rim is altitude-DEPENDENT where the below-band one was constant — 6 px below the
  horizon at C1 y = 1192, 33 px at y = 2000, against 22.6/101 px for the bare sheet — so the
  extension is still load-bearing at every above-deck altitude and 20,480 m is still inside the
  21.86 km `zone2` dome, but it is no longer *derived* from anything. Below the deck the whole sheet
  is culled (`zone_id 2` at camera state 1), and the strip the annulus was built to hide is closed
  there by the zone-1 dome instead: the `C26` climb ladder now reads dip **0.15** / step **0.14** at
  every rung, against `C26`'s own post-fix 2.07 / +1.11.
⚠ **`Tick` is the ONE owner of render visibility, and the rule is the original's `zone_id` gate**
  (`PLAN-weather-decompile-match` B12, 2026-08-09, `FUN_0056c430` — see `Mech3/ZoneGate.cs`). Per
  rig, per frame, it narrows that camera's cull mask to the single zone layer its own weather state
  arms, and sets `Node3D.Visible` on the rig's private deck and dome copies by the same rule
  (`ZoneGate.Draws`). A cull mask, not visibility, for the shared world: splitscreen panes sit in
  different states at the same instant, and `AnimRuntime`'s `NodeActive` condition,
  `WorldBuilder`'s unplaced-entity hide/restore pair and `DamageVisuals` all read or write
  `Visible` on that very content — a visibility gate would answer their questions with "the camera
  is above the deck".
  - It **subsumed the A7 cloud gate** (a single `CloudFieldLayer` bit switched on camera altitude,
    covering the `fvol` clutter and the `cloudparent` clusters). That rule was this gate's
    `zone_id 2` special case, and reading the zone from the DATA instead of from an altitude is
    what fixes **C2B**, whose nine `fvol` volumes are `zone_id −1` and must keep rendering below
    its deck (measured at `(-7325, 192, -3829)`: 123,989 sprite px gated vs 0 ungated — the
    ungated frame's zone-2 deck occludes them). `DeckRegime` no longer answers the question at all.
  - The gate runs in **every chapter and every rig**, unlike the guarded A7 rule: a chapter with no
    reachable band never leaves state 1, and state 1 is exactly what its zone-1 content wants. C5's
    street haze is `zone_id 1` and its state is 1 at street level, so it survives by the rule
    rather than by a guard.
  - **`GameSession.BuildRigs` still resets the band** on the main camera (`ZoneGate.OpenCullMask`),
    for the same reason as before and a bigger one: that camera is the Launcher's and outlives the
    session, `Tick` only ever NARROWS the band, and quitting a C1 flight from ABOVE the deck would
    otherwise cull every zone-1 node of the next flight's world — in C1 that is the whole ground
    world.
  - `--no-zone-cull` (`SessionSpec.NoZoneCull`) restores the pre-B12 picture exactly: the band goes
    back open and neither deck nor dome is hidden. It is the isolation switch Decision 4 asked for
    and the able-to-fail control every B12 probe is measured against.
  - ⚠ **The DECK and the DOME are both gated** (`B14` completed the pair). The deck's tiles are
    `zone_id 2`, so below the deck they are culled (measured: 363,480 deck px at the C1 river pose
    ungated → 0 gated) and the ceiling in their place is `horizon/zone1`'s dome. A world holding
    only ONE dome still leaves it up at every state (`rig.HorizonDomes.Count > 1` arms the swap),
    because the original always has a dome for the state it is in and gating the only one we build
    would leave a camera with no sky at all — that is C1B/C2/C3, whose `zone2` is a bare marker.
⚠ The deck and the `fvol` field are the SAME sheet seen from two sides, so they are read together:
  the deck mesh is what an underside view shows and the sprite field is what a view from above
  shows. Any change to either one's altitude has to be checked against the other's
  (`Effects/FogVolumeClutter`, `docs/formats/fogvol.md`). ⚠ The `fvol` sprite FIELD never moves
  with this item — it is anchored to the volumes, not to the deck mesh — and as of `B14` the deck
  MESH's rendered altitude equals its authored one at EVERY camera altitude
  (`_deckAltitude`), so mesh and field finally share one frame of reference in both regimes.
  The authored 960/1050 is what the scatter is read against either way (fogvol.md's
  mesh-10 m-under-the-slab invariant, pinned per chapter in `DeckRegimeTests`) — a fact about the
  DATA, unaffected by which regime is currently rendering the mesh.
⚠ **The band whiteout flickers inside the interior — `BandFlicker` (nested class), the decompiled
  `FUN_0042ee40` remap** (`PLAN-weather-decompile-match` D32, 2026-08-09). Only the `CLOUD_COVER`
  band's own opacity (`WeatherState.WhiteoutAmount`) is remapped, and only while it sits strictly
  inside `(0,1)` — the binary's exact `*pfVar5 != 0.0 && *pfVar5 != 1.0` guard, applied in `Tick`
  right after `band` is read and before the volume-curtain union, so both paths (C21's union and
  the pre-C21 straight assignment) see the flickered value. The volume curtain
  (`FogVolumeWhiteout.Density`) is untouched — the binary's remap block only ever touches the
  band's own opacity, never the `fvol` density.
  - **Two curves, blended by a drifting parameter.** `BandFlicker.LogCurve(op) = ln(op·5+1)/ln(6)`
    and `AtanCurve(op) = (atan((op−0.5)·10)+0.5)/(atan(5)+0.5)` are `FUN_0042ee40`'s `fVar1/fVar2`
    (computed as `log2` in the binary; the base cancels in the ratio, so natural log reproduces it
    exactly) and its `param_1`. `Remap(op, t) = clamp(AtanCurve(op) + t·(LogCurve(op) −
    AtanCurve(op)), 0, 1)` is the binary's `fVar3` line plus its own clamp.
  - **The drift is a per-instance ping-pong, not a one-shot ramp.** `t` advances by
    `rate · frameDt · driftSpeed · 0.1` each `Apply` call (`GameClock.Current.FrameDt`, never a
    wall clock — DET-1); on overshoot past either bound a fresh `driftSpeed` is drawn in
    `[0.2, 1.0)`, the low bound resets `t` to 0 keeping that speed POSITIVE, and the high bound
    clamps `t` to 1 and NEGATES it — exactly the binary's `_DAT_0064efcc`/`_DAT_0062154c` pair,
    which is what turns a monotonic drift into oscillation between the two curves.
  - **One `BandFlicker` per rig** (`WeatherRig._bandFlicker`, keyed by `rig.Index`), unlike the
    binary's single global pair: splitscreen panes on opposite sides of the band must not share a
    drift phase, the same reasoning as the per-rig whiteout `ColorRect` itself. Each is seeded
    lazily on its rig's first flickering frame from `Rng.NewSystemRandom(Rng.Clouds)` — a
    per-instance `System.Random`, not the shared `Rng.Stream` `RandomNumberGenerator`, deliberately:
    a native Godot RNG object cannot be constructed off-engine, and `BandFlickerTests` exercises the
    class without the engine running (the same reason `RngTests` avoids it for
    `Rng.NewSystemRandom(string, int, int)`).
  - ⚠ **Frame-0 identity is an amplitude ramp, not `t = 0`** — the trap this item names directly:
    `FlatColorTests`/`DeckRegimeTests` pin static poses and all 13 `analysis/goldens` shots are
    frame-0 captures, and neither curve equals the identity function at an interior opacity
    (`AtanCurve(0.5) = 0.267`, not 0.5), so starting `t` at 0 alone would still move a static
    reading. `BandFlicker.Apply` instead ramps the remap's blended AMPLITUDE in linearly from 0
    over `RampFrames` (30, i.e. 0.5 s at the fixed 60 Hz `--det` step) calls: a fresh instance's
    first call returns `op` completely unchanged (`op + 0f · anything == op` bit-for-bit), which is
    what kept all 13 goldens hash-identical across this item.
  - **`BandFlicker.DefaultRate = 5.5f`** is a declared TUNE (`backlog.md` `BL-329`) — the
    decompile's rate multiplier reads a per-mission weather-struct field (≈ `+0x934`) no reader
    decodes and no capture pins a value for. Picked so the drift speed's midpoint (mean 0.6 of its
    `[0.2, 1.0)` range) traverses the full blend range in `5.5 · 0.6 · 0.1 = 0.33`/s — "a few
    seconds", not a decoded figure. `RampFrames` is likewise a guess (short next to a flight, long
    next to one frame), not a measurement.

## src/Session/LensFlareRig.cs
The sun's lens flare: four screen-space sprites strung along the sun→screen-centre vector at
fractions 0.50/0.90/2.0, plus a full-screen white wash whose opacity is ~linear in the sun's screen
distance from centre. Mirrors `WeatherRig` — constructed once per session beside it, `Build` once,
`Tick` from the same per-rig block of `_Process`, one instance per pane. The whole spec is measured
(`CAP-13`; method and calibration in `analysis/bl-165-lens-flare/`), and the per-pane state lives on
this class rather than on `PlayerRig` because an instance is several nodes plus fade state — the
whiteout could live there only because it is a bare `ColorRect`.
⚠ **Anchored to the gamez `sun` node, never to `SUNLIGHT_ORIENTATION`.** The weather file's
  orientation is the *shading* direction: C3 authors yaw 135 while the `sun` node sits at yaw 45, so
  a flare keyed off the parameter draws 90° from the visible disc. Anchoring to the node also makes
  the flare immune to any gamez→Godot axis error, since disc and flare share the conversion
  (`WORLD-26`). Our `DirectionalLight3D` now wears `SUNLIGHT_ORIENTATION` (`BL-324`), so it agrees
  with the *shading* and still not with the disc — which is the original's own arrangement: its
  `sunlight` light node and its `sun` billboard are unrelated objects with no code path between
  them. Reproduced, not reconciled.
⚠ **Two gates, from two files, that must agree**: a `sun` node in the horizon subtree, and
  `LensFlareTexture` slots in `support\<ch>\init.gw`. Both are true of **C2 and C3 only** — as is the
  `sun` texture, a third agreement. Nothing is keyed to a chapter name; a disagreement is logged, not
  smoothed over, and the `lens-flare-gates` suite asserts both directions. C2's flare is **predicted,
  never verified** — no footage of it exists.
⚠ **Occlusion is one centre ray** against the existing colliders, for the sprites only. Terrain and
  the own plane block; particles and billboard sprites carry no colliders and therefore cannot, which
  is what the footage requires. A shape/sphere cast would be wrong — it triggers on any overlap and
  would kill the flare under the partial cover that measurably changes nothing. The wash skips the
  test entirely: it survives terrain occlusion.
⚠ **Layer order is measured, not chosen**: world < flare sprites < HUD/cockpit < wash — see
  `UI/HudLayers.cs`, which carries the whole canvas-layer table and why the debug bands sit above the
  wash. `--no-flare` suppresses the effect so C2/C3 captures stay usable for unrelated comparisons.

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
