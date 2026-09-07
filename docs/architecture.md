# Godot project — per-module implementation notes

**Start at the module index below.** One routing line per module, grouped by namespace. Find the
module there, then read only its entry in `docs/architecture/<Namespace>.md`:
`Grep "## src/Mech3/SceneBuilder.cs" -A 12 docs/architecture/` returns the whole thing, because the
entry shape guarantees it.

One `## src/...` entry per module in `CSVM/src`. **This file orients a reader and nothing else:**
what the module is for, what it owns, and which module to look at next. Entry shape: 1–2 sentences
of purpose beyond the index line, plus the pointers a reader needs. Body ≤ ~8 lines (~12 for the
heaviest modules). Entry order is historical, not grouped — the index is the map, grep is the lookup.

⚠ **Traps do not live here.** A constraint that would stop a wrong edit belongs in the code, on the
member it binds, under `PROJECT_CONTEXT.md`'s comment caps — that is where somebody about to make
the edit is actually looking. Format and decode knowledge belongs in `docs/formats/` and
`docs/org/`; a way a measurement misleads belongs in `docs/verification.md`. Narratives, diagnoses
and landed-work stories go in the commit message.

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
- `src/Mech3/WorldCollision.cs` — derives every world collider's `Disabled` flag from its owner's tree visibility and the fade channel.
- `src/Mech3/PlaneBuilder.cs` — builds one aircraft from its GameZ subtree (shaded, backface-culled); `Repaint` re-liveries it in place.
- `src/Mech3/PaintScheme.cs` — one aircraft livery: pattern + 3 colours + 3 decals, parsed from vehicle.json or drawn at random.
- `src/Mech3/PatternLibrary.cs` — decodes the original's `.BM` paint patterns from the extracted ROF archive; `PatternsFor` lists a plane's liveries.
- `src/Mech3/PlanePainter.cs` — applies a `PaintScheme` to one aircraft: composites skins from the pattern's region masks, swaps decals.
- `src/Mech3/MilitiaPaint.cs` — each militia's paint pattern by display name, which is all a militia decides on Instant Action.
- `src/Mech3/PropParts.cs` — classifies prop/rotor nodes by name; spin axis + rate from the original anims (props Z, rotor Y).
- `src/Mech3/ControlSurfaces.cs` — classifies left/right aileron, elevator and rudder mesh nodes and their hinge axes (X ailerons/elevators, Y rudders).
- `src/Mech3/WingLights.cs` — the one source for wingtip nav lights: flare node names, glow texture, warm-amber colour, blink period.
- `src/Mech3/WorldBuilder.cs` — builds a chapter world: placed + partition subtrees, cloud deck, camera-anchored skydome, edge extender.
- `src/Mech3/MapEdgeExtender.cs` — rolling window of repeated border tiles and clutter continuing the world past the map edge, one per session.
- `src/Mech3/Clutter.cs` — stamps the boot-script clutter templates onto matching-textured terrain at the polygon's own UV lattice.
- `src/Mech3/ClutterTemplates.cs` — the `templates.zrd` reader: each clutter decoration model's authored substitution table, scale range and fade distances.
- `src/Mech3/FogVolumes.cs` — the `fogvol.zrd` reader + the gamez `fvol*` volume census: what the ambient cloud field scatters, and where.
- `src/Mech3/Zrdr.cs` — zrdr extraction reader (zip or dir) + `ZrdrDict`, the key/[values…] view over a reader's list.
- `src/Mech3/LandingApproaches.cs` — a chapter's `landings.zrd` approach table resolved against the gamez: volume, attitude cone, speed band.
- `src/Mech3/Pickups.cs` — a mission's compact `pickups.zrd` sensor and radius table, the spheres the ladder switch tests against.
- `src/Mech3/MissionCutscenes.cs` — the animation names a mission's own `cutscenes\` reader files define: the authored mark of mid-mission choreography.
- `src/Mech3/AiNets.cs` — the chapter AI patrol nets: `ne0NNNNN` waypoint graphs, the `neindex` id to name table, tags and volumes.
- `src/Mech3/AiVolumes.cs` — `AiVolume`/`AiVolumeSet`: the activation, attack and return volumes both authors write, plus the overlay.
- `src/Mech3/RosterMarkers.cs` — grafts a roster block's authored marker scaffolding onto the rig its spawn built, and indexes it.
- `src/Mech3/VehicleDefs.cs` — the `vehicle.json` def index a roster spawn resolves a block against, and its airframe-node inverse.
- `src/Mech3/Maneuvers.cs` — the shared maneuver library (`zrdr/maneuvers.zrd`): timed attitude-step programs and their gates.
- `src/Mech3/CampaignSequence.cs` — the shared `cm_sequence.zrd` reader: the campaign's 24 flat mission entries and each one's storage address.
- `src/Mech3/EnemyGenerators.cs` — the mission `egen.zrd.json` reader: the enemy generators in their three authored shapes.
- `src/Mech3/Zeppelins.cs` — the mission `zeppelins.zrd.json` reader: each instance's motion, net, damage zones and cannons.
- `src/Mech3/InstantAction.cs` — `InstantActionDef` and its three producers: a chapter's file, `--ia=`, and the launchscreen wizard.
- `src/Mech3/AiSkills.cs` — `player.json`'s `ai_skill_parameters` endpoint pairs by rating, plus the aiv roster block accessors.
- `src/Mech3/Messages.cs` — the game's localized string table: the `messages.json` key→value map behind every `MSG_*` key.
- `src/Mech3/UiStrings.cs` — the original's UI string table by id: `langui` rows, with its placeholders converted for composite format.
- `src/Mech3/TgaImage.cs` — the engine-free TGA decoder behind the hangar's art, and the decoded-image type the art seam uses.
- `src/Mech3/PngImage.cs` — the engine-free PNG decoder behind the menus' `rimage` art, returning a `TgaImage` into the same seam.
- `src/Mech3/ArtImage.cs` — the one door menu art is loaded through: a path in, a decoded image or null out, decoder by extension.
- `src/Mech3/MarkerRig.cs` — a plane's firepoint/pylon/target rig from planes.zbd: plane-frame positions + co-located mounts; feeds `--dump-markers`.
- `src/Mech3/CompiledAnim.cs` — reader for the compiled `cam_anim`/`mis_anim` archives: anim defs, sequences/events, lazy SI-script pool.
- `src/Mech3/AnimDefs.cs` — the zrdr front-end: ANIMATION_DEFINITIONS reader files, normalized into one `AnimDefinition` model.
- `src/Mech3/AnimProgram.cs` — merges the compiled + reader defs for one mission, holds `startanims`, resolves SI-script slots.
- `src/Mech3/TextureCycler.cs` — runs the gamez material `cycle` flipbooks (water, surf, wakes) by swapping `albedo_tex`.
- `src/Mech3/WorldSounds.cs` — `SOUND_NODE` ambient 3D emitters (one pooled player per host node) + `PlayOneShot` for destruction/impact audio.
- `src/Mech3/WorldLights.cs` — packs the world's `LIGHT_STATE` point lights into the `csky_light_data` texture the fullbright world shader reads.
- `src/Mech3/MissionSetup.cs` — parses + applies the per-mission `.gw` interp script deciding which world entities a mission shows.
- `src/Mech3/AnimRuntime.cs` — the animation engine: bootstrap, live def instances, event dispatch, motions, conditions, lights, puffers, world effects.
- `src/Mech3/Anim/` — `AnimRuntime`'s motion value types and bind-census enums, split into their own namespace for size; the dispatch-axis modules share it.
- `src/Mech3/Anim/MotionSet.cs` — the live motion collection: registration and eviction, the per-frame sweep, and the predicates the runtime asks it.
- `src/Mech3/Anim/EmitterDirector.cs` — every `PUFFER_STATE` emitter's life on one runtime: start, the four stops, prewarm, respawn, follow and census.
- `src/Mech3/Anim/SoundChannel.cs` — one runtime's `SOUND_NODE`/`SOUND` events: the pooled ambient emitters, the one-shot player, and the late-failure census.
- `src/Mech3/Anim/LightChannel.cs` — one runtime's `LIGHT_STATE`/`LIGHT_ANIMATION` events: the live point-light table, the tween, the `WorldLights` submission.
- `src/Mech3/Anim/PoseChannel.cs` — one runtime's object-pose and visual events: the pose helpers, the opacity/fade machinery and the motion-builder role.
- `src/Mech3/Anim/NameResolver.cs` — name to node resolution: the index, wildcard matcher, scope tier chain, symbol authority, anchors and the bind census.
- `src/Mech3/SequenceRunner.cs` — the engine-free sequence interpreter (event clock, LOOP, IF/ELSEIF, WAIT_FOR_COMPLETION) behind the `ISequenceHost` seam.
- `src/Mech3/DestructibleRegistry.cs` — live per-instance HP for `HEALTH>0` anim defs, one pool per `(def, anchor)`; `Resolve` maps a struck collider back.
- `src/Mech3/ScriptedPath.cs` — resolves an authored waypoint path (`pp1` → the gamez `pp1_aipath` subtree) into ordered world-space waypoints.
- `src/Mech3/WorldPartitionGrid.cs` — which gamez nodes a world-space XZ rectangle covers, off the World node's own cell table; the area verb's selector.
- `src/Mech3/WorldSession.cs` — builds a chapter world and binds its `AnimProgram`, from load through the sound prewarm; `Options` is the whole caller seam.
- `src/Mech3/AircraftStage.cs` — stages the aircraft-archive subtrees a cutscene animates into a chapter world's node table, at that chapter's pointer base.
- `src/Mech3/SessionArchives.cs` — opens the five archives a chapter build needs and the matching `WorldSession.Options` lifetime flags, per `ArchiveIntent`.
- `src/Mech3/DecodeCache.cs` — the opt-in store of decoded, read-only world inputs keyed by their source paths, so one chapter built many times is decoded once.
- `src/Mech3/EmptyStage.cs` — the `--stage=empty` test stage: a collidable ground plane under a code-generated grid, standing in for a chapter world.
- `src/Mech3/WavFile.cs` — pure-C# WAV parser + MS ADPCM→PCM16 decoder (the game's format; Godot can't load it).
- `src/Mech3/WavCues.cs` — a WAV's RIFF `cue ` chunk as ascending times in seconds, the marker clock the briefing narration's reveal script waits on.
- `src/Mech3/SoundArchive.cs` — WAV lookup over a sounds extraction → cached `AudioStreamWav` (forward loop when LOOPED).
- `src/Mech3/MusicPlayer.cs` — the state-driven score: one 2D streaming channel for menu, cabin and mission, with the decoded battle hold.
- `src/Mech3/MissionRadio.cs` — the mission radio queue: the non-positional voice channel the campaign's objective callouts and VO dialogue chains speak on.
- `src/Mech3/SoundDefs.cs` — sounds.json parser: SETS `snd_*` → `SoundDef`; `LoadGroups` → the weighted-random `SOUND_GROUPS` + their dialogue chains.
- `src/Mech3/CombatVoice.cs` — the combat-voice chain: roster `accentID` → `voice.zrd` pool → pilot VO id → clip defs, plus the mission's voice prewarm set.
- `src/Mech3/Anim/TemplateStage.cs` — the effect-template stage as one module: pool-slot arithmetic, placement and following, copy identity, reveal and retire.
- `src/Mech3/EffectCycles.cs` — the `EFFECTS` block of the shared `effects.zrd`: the second source of animated material cycles.
- `src/Mech3/SurfaceRegistry.cs` — the original's surface-name registry: a material's `soil` id to its name, and the direction back.

### `src/Flight/` — the flying aircraft

The plane as a flying, shooting, damageable thing, plus its HUD and stunt mode. Reads plane stats
from the extracted zrdr; owns the arcade physics and everything drawn over the pilot's view.

- `src/Flight/PlaneStats.cs` — typed per-plane stats from vehicle/engines/player.json: dynamics, engine sound, destroyable parts.
- `src/Flight/ShakeDefs.cs` — typed reader over shakes.json: the six shake-oscillator sources (law + per-source magnitude term).
- `src/Flight/WeaponDefs.cs` — typed reader over `weapons.json` `BALLISTICS`: 48 `WeaponDef`s; inspect with `--dump-weapons`.
- `src/Flight/Loadout.cs` — `stock_loadouts.json` reader + `Bind` to a built plane: gun groups + hardpoints, markers→muzzle nodes; `--dump-loadout`.
- `src/Flight/LoadoutChoice.cs` — one pilot's slot-keyed edits to a fit, plus the Ammo Selection screen's two authored dropdown rosters.
- `src/Flight/WeaponBench.cs` — the world-less 48-weapon mount-and-fire pass check behind `--weapon-test` and `weapons-fire`; fires the whole `ForRig` rig.
- `src/Flight/FireControl.cs` — the engine-free fire-control state machine: trigger edges, fire clocks, ammo draw-down, both selectors, the dry cues.
- `src/Flight/AimAssist.cs` — the gun aim assist: the per-muzzle slot state and catch-up pass, the intercept solver, the candidate scan, the launch scatter.
- `src/Flight/TargetRef.cs` — the player-targeting abstraction: one value over every selectable thing, wrapping an `AimCandidate` and adding class and label.
- `src/Flight/TargetPool.cs` — the player's classed candidate pool: the three cycles of `TargetRef`, rebuilt from scratch off the aim assist's own lists.
- `src/Flight/TargetSelection.cs` — the sticky player selection: owns a `TargetPool`, sorts the decoded cycle order, re-finds by entity, carries every action.
- `src/Flight/TargetHud.cs` — the per-pane targeting HUD: the selected target's bracket and label, the nearest-hostile fallback, and `--debug-markers`' overlay.
- `src/Flight/TurretDefs.cs` — typed reader over `ai.zrd`'s `TURRET` section: 42 `TurretDef`s, carried/standalone split, arcs, duty cycle, weapon block.
- `src/Flight/TurretController.cs` — one turret gunner, carried or emplaced: acquire, intercept, arc clamp, bounded slew, duty cycle, fire into the shared pool.
- `src/Flight/AiPilot.cs` — the non-player `FlightModel` driver: standing orders, patrol, gunner, escort and mode machine into one `FlightInput` per sim step.
- `src/Flight/AiControlLaw.cs` — the original's own AI steering law: an aim point, its velocity and one of four decoded tables into stick and throttle lever.
- `src/Flight/AiEscort.cs` — the formation-escort law a netless `mode wingman` flies: leader and target snapshots into one station point and its velocity.
- `src/Flight/AiModeMachine.cs` — the nine-mode AI state machine over the engine's own mode vocabulary, with the steady-hand and sixth-sense reaction rolls.
- `src/Flight/AiGunner.cs` — the AI's forward-gun gunnery: the intercept lead, the quick-draw cone and engagement window, the traverse clamp, the scatter.
- `src/Flight/AiRocketeer.cs` — the AI's ordnance employment: the per-pylon gates, an aim cosine tighter than the gun's, the lockout, the per-pylon lead solve.
- `src/Flight/AiVoiceDispatcher.cs` — the combat-voice trigger dispatch: the talker roll, the bearing halving, the broadcast election, the damage tiers.
- `src/Flight/AiTargetRanking.cs` — the decoded target-ranking formula, minimised over weight, distance and objective bias, the deconfliction pick, and the two-scorer selector.
- `src/Flight/SurfaceGunMount.cs` — a `mode ship` hull's gun mount: the elevation guards, the 4.0/s slew, and the residual measured against the raw lead.
- `src/Flight/PursuitQuarry.cs` — the flight law's one-step snapshot of the standing target of any class: an aircraft, a turret or a zeppelin part.
- `src/Flight/AiNetFollower.cs` — walks an `AiNet` patrol graph as waypoints, nose-picked edges and along-leg arrival; shared by `AiPilot` and `ZeppelinMotion`.
- `src/Flight/DangerZoneRibbon.cs` — one `dzpathN` route as a metre-parameterised spline with lanes, a pilot's cursor on it, and the rail integrator.
- `src/Flight/DangerZoneRibbons.cs` — a mission's ribbon set off the chapter gamez with its inactive list; one per session, lanes being occupancy-counted.
- `src/Flight/ZeppelinBroadside.cs` — the pure broadside law: the decoded side arc, the per-cannon deploy machine and re-fire timer, the lead and gasbag picks.
- `src/Flight/ZeppelinDamage.cs` — the pure zeppelin kill arithmetic: the survivor count over the `healthy` list, the engine recount, the gasbag gate, stages.
- `src/Flight/ZeppelinMotion.cs` — the kinematic zeppelin motion law: forward-only net flight under the record's limits, the eased steer law, the stop approach.
- `src/Flight/ManeuverExecutor.cs` — plays one library maneuver's attitude-step program as `FlightInput` per sim step, for the `evasive maneuver` mode.
- `src/Flight/WeaponCursor.cs` — `FireControl`'s internal ammo-slot index math (`NextArmed`/`NextSelectable`); nothing else calls it.
- `src/Flight/RocketTriggerLatch.cs` — the rocket trigger's consumed-press latch: a press still held when flight regains input reads as released.
- `src/Flight/Ballistics.cs` — the VELOCITY/ACCELERATION/GRAVITY integration step, shared by `ProjectilePool` and the reticle's projected impact point.
- `src/Flight/DisablingIntensity.cs` — the decoded `SONIC`/`FLASH` intensity plateau and `FLASH`'s facing test, on squared distances; feeds wash and stun.
- `src/Flight/Difficulty.cs` — the difficulty setting as the engine's 0/1/2, its two naming vocabularies, and the enemy armour/health multiplier at spawn.
- `src/Flight/TanglerChoke.cs` — the choker's engine-dead duration and the `ENGINE_DEAD` globals it reads; the squared-over-raw radius mismatch, reproduced.
- `src/Flight/SmokeScreens.cs` — the smoke screen's stun trap: the world's active screens walked over the roster each sim step, the cone rule, the wash cadence.
- `src/Flight/BeeperTags.cs` — the beeper's paint and the seeker's pick: the world tag list with its countdown and tail, the tagging gate, the selection rule.
- `src/Flight/CamParams.cs` — one aircraft's camera tuning from `camparam.json`: `default` plus its own block, keyed by DISPLAY name. Only `Dist` is applied.
- `src/Flight/PilotViewMode.cs` — the three selectable views (Chase/Cockpit/Nose = camera modes 0/6/7) and `PilotView`, the pure rules over them.
- `src/Flight/CameraController.cs` — the flown plane's camera: chase, numpad fixed views, look-behind, the selected view mode and the lab's held-airframe orbit.
- `src/Flight/HeadLook.cs` — the pilot's head in a first-person view: snap directions, free-look, the centre key and the smoothing to the shown angles.
- `src/Flight/CockpitVisibility.cs` — the per-mode hiding of the pilot's OWN plane in first person; `Rules` is pure, `Bind`/`Apply` write it onto a built model.
- `src/Flight/CockpitOverlay.cs` — `--cockpit-pass`: the cockpit interior drawn in a `SubViewport` world of its own, composited under the HUD; one per player.
- `src/Flight/CockpitGauges.cs` — the 3D instrument panel inside `cockpit1`: needles, horizon ball, belts and lamps, driven off `GaugeCluster`'s state.
- `src/Flight/ImpactOutcome.cs` — what a weapon×surface hit should do (effect, sound, stand-in, damage) as a value; `Resolve` is pure and engine-free.
- `src/Flight/Projectile.cs` — `ProjectilePool`, the weapon-fire subsystem: ballistics, guidance, fuses, the hit ray, tracers, impact and splash damage.
- `src/Flight/ProjectileFlyoutAnim.cs` — `ProjectilePool`'s `FLYOUT MODEL_ANIMATION` half: every ordnance round runs its own def on the sequence interpreter.
- `src/Flight/WarningShotCue.cs` — the shipped near-miss accumulator (player.json `warning_shot_*`) and the swept-segment/point distance; unit-testable alone.
- `src/Flight/IncomingFire.cs` — `--incoming`: the near-miss test rig, a phantom shooter on each player's six, so the cue is reachable without an AI gunner.
- `src/Flight/SpawnPoints.cs` — flight spawn from the mission's own zrdr: ia.json `spawn_points`, or objectives.json PLAYER_INIT as fallback.
- `src/Flight/MissionTargets.cs` — mission `targets.json`: target key to its objective display keys, plus the marker flags a mission starts with.
- `src/Flight/StuntMission.cs` — Stunt Flying state: ia.json `dzones` → a danger-zone run with completion, clock and splits, one per pilot.
- `src/Flight/HudMetrics.cs` — the one rule for HUD sizing: window height / 1440, damped by `sqrt(paneH/windowH)` for splitscreen.
- `src/Flight/HudFont.cs` — the game's own 5px HUD bitmap font, auto-segmented from `rimage/5pointhud*.png`; `--hud-font-test` proves it.
- `src/Flight/HudFontTest.cs` — the `--hud-font-test` overlay: a known string in both variants, with a rule marking the width `Measure` reports.
- `src/Flight/ImpactReticle.cs` — the gun aiming pipper: 0.5 s of the selected group's flight along the nose (the original's own rule), projected each frame.
- `src/Flight/EdgeMarker.cs` — the off-screen marker's placement rules, engine-free: on-screen test, behind-mirror, edge clamp, and the o'clock bearing.
- `src/Flight/MarkerDraw.cs` — the world marker's drawing primitives: reticle, edge arrow, centred text block and its clamped variant, marker blue and shadow.
- `src/Flight/MarkerHud.cs` — the stunt objective marker HUD: reticle, screen-edge arrow + o'clock bearing, run status, banners; one per player.
- `src/Flight/ResultsBoard.cs` — the shared shell every results board is built on: backdrop and panel, the palette, the halt contract, and the standard menu.
- `src/Flight/StuntScoreboard.cs` — end-of-run results overlay: a per-pane panel of per-zone splits, total, and the persisted best time.
- `src/Flight/StuntSplits.cs` — the stunt run's split table, shared by the scoreboard and the wrap-up board: per-zone rows, the total, and the best comparison.
- `src/Flight/StuntRace.cs` — splitscreen stunt race bookkeeping: one `Racer` per player, finish placings, standings, rematch reset.
- `src/Flight/StuntRaceBoard.cs` — the race's shared ranked results overlay, on its own full-window CanvasLayer above the splitscreen panes.
- `src/Flight/ScoreStore.cs` — stunt best-time persistence: `user://stunt_scores.json` keyed chapter/mission/plane, faster runs only.
- `src/Flight/CustomPlaneDef.cs` — a custom-built plane as a pure model: the saved record's chosen fields only, with the campaign loadout export alongside.
- `src/Flight/CustomPlaneStore.cs` — JSON persistence for a built plane, one file per name under `user://Planes/`, over a plain directory so it unit-tests.
- `src/Flight/CustomPlaneRecord.cs` — import-only reader for the original's 204-byte saved-plane files, one record or a whole install directory to defs.
- `src/Flight/CustomPlaneBuild.cs` — the join from a saved plane onto what a spawn consumes: the loadout over the stock fit, the paint, the armoured zones.
- `src/Flight/HangarEconomy.cs` — the hangar's decoded economy over a built plane: the component tables, per-line costs and weights, the totals and the verdict.
- `src/Flight/VersusMatch.cs` — Dogfight deathmatch bookkeeping: kills and deaths per player, the host-fed clock, threshold and time-out completion, standings.
- `src/Flight/VersusHud.cs` — per-pane Dogfight status line: remaining time, this player's kills, the leader, and the hostile marker.
- `src/Flight/VersusBoard.cs` — the Dogfight results overlay, one whole-window CanvasLayer above the splitscreen panes.
- `src/Flight/IaWrapupBoard.cs` — Instant Action's wrap-up board: outcome headline and the per-counter score rows, summed across every seat.
- `src/Flight/PauseState.cs` — who is holding the sim clock and why: the pause owner and the results-board halt, engine-free.
- `src/Flight/HaltReason.cs` — why the clock is stopped; the clock advances only when no reason is set.
- `src/Flight/PauseBoard.cs` — the shared pause board and its Resume · Restart · Exit menu, one whole-window CanvasLayer.
- `src/Flight/PhysicsConstants.cs` — `NomGravity`, the single `nom_gravity` value the flight model and its tests share.
- `src/Flight/Weather.cs` — weather.json reader → `WeatherState`: per-zone fog, sunlight, cloud whiteout, wind, precipitation.
- `src/Flight/FlightAudio.cs` — own-plane loops (engine, overspeed whine, rattle) + crash/prop one-shots, per-player `MixGain`.
- `src/Flight/AiEngineAudio.cs` — an AI aircraft's positional engine loops and the 2000-unit cull.
- `src/Flight/EngineAudioCurves.cs` — the engine-slot definition choice and curve maths both audio paths share.
- `src/Flight/SpectatorCamera.cs` — the `--freecam`/`--anim-lab` observation camera: RMB-look plus WASD/QE, no roll; `Frame`/`FollowNode` track an object.
- `src/Flight/OrbitLock.cs` — the re-lock rule behind that key: nearest first, then outward, engine-free.
- `src/Flight/FlightModel.cs` — the arcade velocity-vector flight physics: thrust/drag/gravity/lift, stall, calibrated control rates.
- `src/Flight/StickRamp.cs` — the keyboard stick as an accumulator: a held key ramps the axis at 2.5/s, release or reversal drops it to centre in one frame.
- `src/Flight/PathFollower.cs` — the second movement law: a placed vehicle driven along an authored waypoint path instead of through the flight model.
- `src/Flight/PropAnimator.cs` — spins the collected prop/rotor discs about their local axes, throttle-scaled (idle floor 0.4); `--fly` only.
- `src/Flight/ThrottleSlamSmoke.cs` — a large throttle jump streams dark exhaust trail smoke for a few seconds; a single notch or a decrease shows nothing.
- `src/Flight/FuelTank.cs` — the flown tank: burns with the lever, and a dry one freezes the throttle lever where it stands. Engine-free.
- `src/Flight/SpeedCue.cs` — chapter-authored pale smoke wisps emitted 60 m ahead of each player, density selected by camera altitude.
- `src/Flight/ControlSurfaceMix.cs` — the decoded control-surface angle solver: three stick channels into six clamped slots, smoothed at 2/s. No scene node.
- `src/Flight/ControlSurfaceAnimator.cs` — poses ailerons/elevators/rudders from those slot angles; `--fly` only.
- `src/Flight/WingLightBlinker.cs` — blinks the wingtip flares for about a frame every 1.5 s, reset off on respawn; `--fly` only.
- `src/Flight/PylonOrdnance.cs` — the rockets under the wings: one FLYOUT-model body per loaded pylon, hidden as its ammo depletes; `--fly` only.
- `src/Flight/PlaneShake.cs` — the plane-wobble oscillators (gunfire buzz, overspeed rattle, hit rocks, nitro engage) summed to roll on `ShakePivot`.
- `src/Flight/NitroSystem.cs` — the nitro boost lifecycle: the decoded tank, one-shot engage, cutoff, gates and animation edges, engine-free.
- `src/Flight/PlaneCollider.cs` — derives up to 8 plane-frame convex collision hulls from the built model's triangles, with no per-plane data.
- `src/Flight/ConvexHull.cs` — an engine-free convex hull over a point cloud: vertices, faces, thickness padding and the point-distance query.
- `src/Flight/ScreenSize.cs` — screen-space sizing for world sprites: the pixel-floor inversion, and the nearest-viewer floor one shared mesh takes.
- `src/Flight/CollisionLayers.cs` — the named physics layers (world / aircraft): the one place a layer bit is assigned a meaning.
- `src/Flight/CollisionDamage.cs` — the original's collision arithmetic: the severity cosine, the damage pair's terms, the camera kick, and the grace windows.
- `src/Flight/AircraftBody.cs` — the flying plane's physics body: the shared `PlaneCollider` hulls on the aircraft layer; struck shape → part name.
- `src/Flight/IWorldQuery.cs` — the one seam onto the live physics world: a shape swept along a motion, a ray, and a standing overlap test.
- `src/Flight/GodotWorldQuery.cs` — the only adapter over `DirectSpaceState`; implements `IWorldQuery`.
- `src/Flight/ContactReport.cs` — one detected contact as a value: impact, normal, struck part, collider name, stop fraction, and whether it was an aeroplane.
- `src/Flight/ContactOutcome.cs` — what a contact costs the striker: fate, the damage pair, the charged zone, the push-out, and the struck-aircraft instruction.
- `src/Flight/AircraftContactResolver.cs` — the decoded contact rules for one aircraft: the damage pair, the fate and the un-embed loop, holding no node.
- `src/Flight/SweepCadence.cs` — the original's alternate-step collision sweep: which sim steps sweep, and the skipped step's motion carried into the next one.
- `src/Flight/AircraftLifecycle.cs` — the states one aircraft moves between and the spawn timers; every transition reports what the node must then perform.
- `src/Flight/PlaneDamage.cs` — the decoded damage ledger: per-part pools plus the whole-vehicle pair, the armour-first take-hit flow, and the kill rule.
- `src/Flight/DamageVisuals.cs` — flips the torn-skin `pdpN` panels (paired by mesh position) at the data's injure thresholds, plus fire trails.
- `src/Flight/DamageLab.cs` — the `--damage` and F5 slider panel: one slider per part, driving a parked plane's visuals or the flown plane's real ledger.
- `src/Flight/CompassTape.cs` — the top-centre heading tape from the game's own HUD textures, drawn as a cylindrical drum seen edge-on.
- `src/Flight/GaugeCluster.cs` — the cockpit dials as HUD (altimeter/speedo/damage + gun/missile), geometry from the plane's `gauges` subtree.
- `src/Flight/FlightController.cs` — the flying-aircraft node: input → FlightModel → transform, weapons, collision, crash and respawn.
- `src/Flight/FlightHud.cs` — everything one pane draws for its pilot, fed one per-frame state struct; the controller's seven HUD collaborators live here.
- `src/Flight/FlightControllerBuild.cs` — FlightRoster's internal, write-once construction handoff for a controller before tree attachment.
- `src/Flight/IFlightInputSource.cs` — the seam a sim step reads this frame's pilot intent through; `Bind` resolves one of its three adapters once per aircraft.
- `src/Flight/PlayerRig.cs` — one rendered view's state: camera, SubViewport, HUD parent, visual layer, controller, own sky/deck/puffs.
- `src/Flight/ViewerSet.cs` — the session-owned "every pane's camera" registry, bound once after the rigs are built; the tracer floor is its first consumer.

### `src/Effects/` — particle systems

- `src/Effects/Puffer.cs` — data-driven `PUFFER_STATE` billboard-particle emitter: burst, distance-trail, or sustained at-node modes.
- `src/Effects/EmitterRenderer.cs` — the `IEmitterRenderer` seam under `Puffer` and the `MultiMesh` billboard-shader renderer behind it.
- `src/Effects/FogVolumeClutter.cs` — the authored ambient cloud field: `fogvol.zrd` clutter scattered through its `fvol*` volumes, one MultiMesh per kind.
- `src/Effects/Precipitation.cs` — weather.json rain/snow: one camera-following MultiMesh of flakes or streaks, self-animating on the GPU.
- `src/Effects/WorldWind.cs` — the mission's global wind (static vector plus random-walk gust) and `EffectAmbience`, the seam a `Puffer` reads it through.

### `src/UI/` — screens, overlays and the inspection labs

The launchscreen and splitscreen rig, plus the interactive debug labs. Every lab has a scripted
`--debug-*` twin so a finding can be reproduced headlessly — see `docs/cli.md`.

- `src/UI/MenuInput.cs` — one player's menu input source: keyboard flag, a `Pads` binding, edge and auto-repeat polling, and the typed characters a field needs.
- `src/UI/Menu/PresentationId.cs` — the identity a presentation registers under and Options persist; `built-in` and `original` ship.
- `src/UI/Menu/IMenuPresentation.cs` — one presentation's lifecycle: activate at a mapped destination, tick over the host's seats, deactivate.
- `src/UI/Menu/PresentationRegistry.cs` — presentation registration: one factory per id, a fresh instance per activation, an unknown id refused.
- `src/UI/Menu/IMenuHost.cs` — what the menu host lends a presentation: the shared features, the audio, the per-seat sources, the one exit.
- `src/UI/Menu/MenuHost.cs` — the process-lifetime host: it selects, shows and ticks the presentation and owns everything a switch outlives.
- `src/UI/Menu/BuiltIn/BuiltInPresentation.cs` — the Built-in presentation: `LaunchMenu` under its own id, the return destination mapped onto it.
- `src/UI/Menu/BuiltIn/BuiltInSeat.cs` — a pad-side input source: one `MenuInput` polled and translated into one seat's command frame.
- `src/UI/Menu/IMenuFeature.cs` — the shared-feature contract: typed state and semantic operations; `Discard()` drops transient setup.
- `src/UI/Menu/MenuFeatureSet.cs` — the host-owned feature registry, fetched by concrete type; `DiscardTransient()` is what a switch drops.
- `src/UI/Menu/MenuCommands.cs` — one seat's semantic commands plus `IMenuInputSource`, the device-neutral seam every device sits behind.
- `src/UI/Menu/IMenuAudio.cs` — the shared menu audio contract: presentations ask for cues and narration, the service owns everything else.
- `src/UI/Menu/MenuExit.cs` — the one typed menu exit `Launcher` consumes: launch, campaign mission, quit, options-apply. No presentation builds a session.
- `src/UI/Menu/MenuReturnDestination.cs` — semantic return destinations (top level, cabin, debrief) each presentation maps into its own graph.
- `src/UI/Menu/MenuChapters.cs` — the shared chapter roster: the eight chapter worlds, which carry Danger Zones, and the per-mode filter.
- `src/UI/Menu/MenuLayout.cs` — the runtime reader of `extracted/rof/menu_layout.json`: screens, widgets with typed fields, navigation edges.
- `src/UI/Menu/ControlsFeature.cs` — the shared rebinding screen: one seat's keymaps, the cursors, the capture, and the steal it names first.
- `src/UI/Menu/PlayerSetupFeature.cs` — the shared player setup: seats claimed by source identity, the roster, the two-stage pick, the gate.
- `src/UI/Menu/HangarFeature.cs` — the shared hangar: one scratch build over a plane store and an optional wallet, and the purchase gate.
- `src/UI/Menu/CampaignFeature.cs` — the campaign as a shared feature: the profile roster, the seated player, the mission, and every write.
- `src/UI/Menu/CampaignBriefing.cs` — one mission's briefing as the feature holds it: the state, the narration, the note, the reveal's progress.
- `src/UI/Menu/CampaignWallet.cs` — the seated profile as the hangar's wallet: funds, affordability, availability, the builds, purchase and sale.
- `src/UI/Menu/CampaignAidProfiles.cs` — the scratch profile store the campaign screenshot aids seat a player over, unable to reach the real one.
- `src/UI/Menu/MenuIdleSource.cs` — a seat's input source with no device behind it, idle every frame; the screenshot aid's extra players.
- `src/UI/MenuSeatDevices.cs` — the pad side of the shared player setup: seat 0's claimed pad, the join gesture, hotplug, the flight binding.
- `src/UI/Menu/FreeFlightFeature.cs` — Free Flight as a shared feature: the chapter roster, the pick, the launch gate and the typed exit.
- `src/UI/Menu/InstantActionFeature.cs` — Instant Action as a shared feature: the decoded option sets, the typed setup state, the built def.
- `src/UI/Menu/Original/OriginalShell.cs` — the Original presentation's screen graph over the decoded layout, and its nine partials below.
- `src/UI/Menu/Original/OriginalGameOptions.cs` — the shell's Game Options page (a `partial`): the shared options as a table of authored rows.
- `src/UI/Menu/Original/OriginalVideo.cs` — the shell's VIDEO page (a `partial`): the display settings as a table of authored rows, the resolution row's words enumerated per screen.
- `src/UI/Menu/Original/OriginalCredits.cs` — the shell's credits screen (a `partial`): the painted background pane, ABOUT drawn disabled, the DONE plaque.
- `src/UI/Menu/Original/OriginalSeats.cs` — the shell's two sortie screens (a `partial`): the chapters, the windowed aircraft column, FLY.
- `src/UI/Menu/Original/OriginalSeatPlane.cs` — the shell's per-seat aircraft screen (a `partial`): one joined seat picking on the plane-selection board's shape.
- `src/UI/Menu/Original/OriginalInstantAction.cs` — the shell's Instant Action screen (a `partial`): the contents list, dropdowns, enemy pages, the Build and Weapon Loadout doors.
- `src/UI/Menu/Original/OriginalLoadout.cs` — the shell's Instant Action Weapon Loadout (a `partial`): the decoded ammo chrome over the pilot's or the wingmen's shared fit.
- `src/UI/Menu/Original/OriginalHangar.cs` — the shell's hangar (a `partial`): the name screen, the tabbed hub, the totals page, the inventory.
- `src/UI/Menu/Original/OriginalCampaign.cs` — the shell's campaign (a `partial`): the nine decoded screens over the shared board component.
- `src/UI/Menu/Original/OriginalPresentation.cs` — the Original presentation node: the shell drawn through `ComposedBoardView`, seats polled.
- `src/UI/Menu/Original/OriginalAvailability.cs` — Original's availability answer before entry: a refusal reason, or the loaded layout.
- `src/UI/Menu/Original/OriginalAssetManifest.cs` — the required/optional file manifest derived from the layout, and the check over a tree.
- `src/UI/Menu/Original/OriginalRosters.cs` — the Original sortie screens' chapter labels and the eleven stock airframes with their nodes.
- `src/UI/Menu/Original/OriginalCues.cs` — the four cue names Original asks for: a rollover, a press, and an edit box's two sounds.
- `src/UI/Menu/Original/PointerSeat.cs` — seat 0 with the mouse as its `MenuPointer`, the click a press edge and the wheel's steps; device reads injected.
- `src/Session/MenuCueTable.cs` — the menu cue table: cue name to wav under the rof tree's `ASSETS/SOUNDS`, the four the globals script binds.
- `src/UI/BoardMenu.cs` — a board's cursor and item list, engine-free, so the selection rules test off engine.
- `src/UI/BoardMenuItem.cs` — the rows a board menu can offer: Resume, Photo, Restart, Exit.
- `src/UI/BoardMenuView.cs` — draws a board menu's rows in the launchscreen's cursor idiom, inside the board style.
- `src/UI/BoardMenuHost.cs` — menu, rows and reader kept together, so a board wires one in two lines.
- `src/UI/CursorRow.cs` — one centred list row and its cursor marker, shared by the launchscreen's lists and every board menu.
- `src/UI/HudLayers.cs` — the canvas-layer order for everything drawn over the 3D view: whiteout, HUD, sun wash, debug overlays, labs, boards.
- `src/UI/SplitScreen.cs` — the splitscreen rig: one SubViewport pane per player (2-4), a shared `World3D`, every pane a 3D audio listener.
- `src/UI/LaunchMenu.cs` — the Built-in presentation's launchscreen: the screen graph, the Godot controls, per-seat polling, and the hangar and campaign doors.
- `src/UI/MenuZones.cs` — how the launchscreen divides a window: a fixed header and footer, the list in what is left, one shared scale. Engine-free.
- `src/UI/Menu/InstantActionPresets.cs` — the Table of Contents: the 19 decoded preset scenarios by name, resolved to the setup screens' own cursor positions.
- `src/UI/PlanePickerRoster.cs` — the roster every human plane picker draws: the stock airframes then the store's saved customs. Engine-free.
- `src/UI/PlaneDiagrams.cs` — the original's plan and head-on diagram sheets sliced per airframe, shared by ammo selection, the flight check and the hangar.
- `src/UI/PlaneNameTables.cs` — the two authored word lists the PLANENAME screen rolls a plane name from.
- `src/UI/PlaneFit.cs` — what one campaign aircraft carries, resolved from its hangar build or its airframe's stock fit; engine-free.
- `src/UI/PlaneRatings.cs` — the four Poor-to-Excellent ratings the plane selection screen prints beside an aircraft; only agility is decoded.
- `src/UI/HangarFlow.cs` — the Build Custom Plane flow, Built-in's walk of the shared hangar feature: the screen order, the cursor and the page mount point.
- `src/UI/HangarAirframePage.cs` — the AIRFRAME screen: the eleven airframes, the blueprint preview, and the defaults ask a pick raises.
- `src/UI/HangarEnginePage.cs` — the ENGINE screen: the airframe's six engines plus the explicit None row, each with its decoded cost and weight.
- `src/UI/HangarArmourPage.cs` — the ARMOR screen: the four zones stepped on the dropdown's own units-times-five scale.
- `src/UI/HangarGunsPage.cs` — the GUNS screen: four slots stepping the eleven-entry calibre cycle, priced per mount.
- `src/UI/HangarHardpointsPage.cs` — the HARDPOINTS screen: a 0-to-4 count per wing, priced per hardpoint.
- `src/UI/HangarPaintPage.cs` — the PAINT screen: a pattern, three colour and shade pairs and three decals over a preview from the original's own masks.
- `src/Flight/HangarPaintTables.cs` — the paint screen's decoded swatch and pattern tables plus the decal names, as CSVM data; the colour resolver is pure.
- `src/UI/HangarNamePage.cs` — the PLANENAME screen: two word steppers, a roll across both, and a typed name over the result.
- `src/UI/HangarPurchasePage.cs` — the PURCHASE screen: the itemised bill, the totals row, and the purchase gate in the original's own words.
- `src/UI/CampaignFlow.cs` — the campaign's out-of-mission flow, engine-free: a stack of screens over one profile, the `ICampaignPage` mount point.
- `src/UI/Menu/CampaignFlightField.cs` — a campaign sortie's humans: joined count, the check showing, each guest's pick, and the no-duplicate rule.
- `src/UI/CampaignRosterPage.cs` — the player profile screen: the name field over the roster, continue, a confirmed delete, and the name refusals.
- `src/UI/CampaignCabinPage.cs` — the cabin hub: next mission, previous missions, plane construction and the way back to the main menu, over the cabin art.
- `src/UI/CampaignBriefingPage.cs` — the mission briefing: the revealed map, the parchment objectives note, the narration a shell plays, and the three buttons.
- `src/UI/CampaignFlightCheckPage.cs` — the FLIGHT CHECK screen: each crew slot's plane, guns and rockets, the ammo and plane doors, and FLY MISSION.
- `src/UI/CampaignAmmoPage.cs` — the AMMO SELECTION screen: four gun-group and eight pylon drop-downs over a working copy, written only by ACCEPT LOADOUT.
- `src/UI/CampaignPlaneSelectionPage.cs` — the PLANE SELECTION screen: a drop-down, silhouette, ratings and weapon lists per slot, EXPORT, and its refusal.
- `src/UI/Menu/BriefingScript.cs` — the reveal script, engine-free: the `Briefing.zrd` reader and the interpreter that runs a state's beat sheet.
- `src/UI/Menu/BriefingObjectives.cs` — the briefing's parchment note from a mission's `objectives.zrd`, ordered by priority, which a reveal opcode indexes.
- `src/UI/CampaignPreviousMissionsPage.cs` — the scrapbook's contents list, one row per completed mission, plus the results page a mission's records compute.
- `src/UI/CampaignScrapbookPage.cs` — the scrapbook itself: the browsed spread's scraps, the results card with its tabs and stamps, and the page arrows.
- `src/UI/CampaignScrapbookZoomPage.cs` — one scrap's detail view: the zoom family's background, the inset image, its three text lines, and EXPORT TO DESKTOP.
- `src/UI/ScrapbookComposition.cs` — the scrapbook's per-spread scrap layout read from the shipped CSV, gated on the mission's own progress mask.
- `src/UI/ScrapbookExport.cs` — EXPORT TO DESKTOP's copy: the scrap's file to the desktop, answering with the name or the OS reason. Engine-free.
- `src/UI/CampaignCombo.cs` — a campaign screen's drop-down field: its authored box, its scrolling window, and a candidate it never commits itself.
- `src/UI/CampaignModal.cs` — the one-button dialog a campaign screen raises over the board, held by the flow because two screens reach the same box.
- `src/UI/CampaignTextEntry.cs` — a campaign screen's one-line text field, typed from a keyboard or stepped from a pad through one alphabet.
- `src/UI/CampaignAidScript.cs` — the input script a `--menu=` colon argument spells: counted moves, confirms and button words a campaign aid replays.
- `src/UI/ListWindow.cs` — a scrolled list as a pointer sees it: the window's box, the thumb on its track, and where a wheel step or a thumb drag puts the window.
- `src/UI/BoardFit.cs` — how the original's fixed 800x600 dialog space lands on any window: one uniform scale, the board centred, the rest letterboxed.
- `src/UI/ComposedBoard.cs` — what a composed campaign screen is made of: backdrop, fills, pictures, strokes, lines, plaques and flowed lists in draw order.
- `src/UI/CampaignBoards.cs` — the fixed chrome of the eight campaign screens, and the composer that turns a page and a cursor into one board.
- `src/UI/CampaignLayout.cs` — the decoded menu layout as the boards read it: geometry and art by section and key, every read carrying its own fallback.
- `src/UI/ComposedBoardView.cs` — the Godot half of the boards: a composed board drawn through `BoardFit` at nearest filtering, the art cache, the hint band.
- `src/UI/BoardPalette.cs` — the ink a campaign board writes in, one palette per background family.
- `src/UI/LoadBoard.cs` — the load screen a session builds behind: the original's chart sheet for a campaign launch, its blackboard for everything else.
- `src/UI/ObjectivesHud.cs` — the campaign mission's objectives readout, drawn on the pause screen alone, one instance per rig.
- `src/UI/MissionEndFade.cs` — the mission-end black-out, painting `CampaignDirector.LeavingFade` onto a full-screen rect every frame, one instance per rig.
- `src/UI/ScreenFlash.cs` — the full-screen wash, two channels per pane: the proximity-routed burst ramp and the victim-routed blend, composited at paint time.
- `src/UI/BlendWash.cs` — one pane's victim-routed wash: the sonic, flash and smoke blend rule and its attack, sustain and release envelope.
- `src/UI/LiveryLab.cs` — the `--viewer` livery editor (L): squadron, colour and decal steppers, a live repaint and copy-CLI-args.
- `src/UI/MeshLab.cs` — the geometry and shading lab (M): normal lines, smoothing seams, cull and normal overrides, on the parked plane or on the selection.
- `src/UI/ColliderOverlay.cs` — the collider wireframes (C): every built collision shape drawn, coloured by the surface id it resolves to.
- `src/UI/ClassOverlay.cs` — the colour-by-class overlay (X): every drawn mesh tinted destructible, facade, clutter or scenery, a findable-targets view.
- `src/UI/AiNetsOverlay.cs` — the AI patrol-net overlay (F13): the chapter's nets as coloured graphs with labels, plus a live leash per AI aircraft.
- `src/UI/TileGridOverlay.cs` — the map-edge tile-grid overlay (`--debug-tilegrid`): every ground tile tinted by repetition band, so one band is one block.
- `src/UI/WeaponLab.cs` — the weapon lab panel (B): steppers that arm the held plane's live loadout, and click-to-place on a world surface. Fires nothing.
- `src/UI/PanelFocus.cs` — the one rule every flight-hosted panel applies: no widget takes keyboard focus, or a focused button eats the fire key.
- `src/UI/NodeLabels.cs` — floating `cs_name` labels over scene nodes (F16): off, meshes or all, anchored on mesh centres and de-cluttered.
- `src/UI/MarkerOverlay.cs` — the `--viewer` firepoint, pylon and target overlay (K): coloured gizmos with de-cluttered labels.
- `src/UI/PhotoModeHud.cs` — photo mode's fading hint line and its Escape or pad-B way out; it raises an event and decides nothing.
- `src/UI/PerfHud.cs` — the frame-cost readout (F14): fps, current frame cost and worst recent frame, once for the window, drawn above the launchscreen too.
- `src/UI/BuildStamp.cs` — the build's version in the menu's bottom-right corner, once for the window and over every presentation; hidden in flight.
- `src/UI/NoGameDataScreen.cs` — the screen shown instead of the menu when the data root holds no extraction: what is missing, and the extraction step that fills it.
- `src/UI/TargetingOverlay.cs` — the targeting overlay (F15): a line from every gunner to its acquired target, coloured by the gate holding the trigger.
- `src/UI/DebugKillTarget.cs` — the kill key (F17): kills player 1's selected target through its own death path; inert on a turret, which has no health key.
- `src/UI/SelectionService.cs` — the shared `--freecam` and `--anim-lab` selection: click-pick, the `cs_name` ancestor ladder, a breadcrumb and a highlight box.
- `src/UI/NodeLab.cs` — the node lab (N): a lazy `cs_name` tree, search, frame, hide and glTF export, a dependency readout and a destructibles view.
- `src/UI/WorldDamageLab.cs` — the world damage lab (F5): an HP slider with kill and reset on the selection's own destructible pool.
- `src/UI/OrbitCamera.cs` — the static inspection view's orbit camera: orbit, zoom and AABB framing over a camera it does not own.
- `src/UI/AnimLab.cs` — the `--anim-lab` debugger: a quiet stage, a fixed-dt clock, a transport panel, a def picker, the timeline and a freecam.
- `src/UI/AnimTimeline.cs` — the anim lab's per-sequence timeline: authored event blocks against runtime-fired ticks, the scheduler-divergence instrument.

### `src/Utils/` — session-wide services

The things every subsystem depends on: the clock, the log, the seed. Changing one of these changes
determinism repo-wide; read `docs/verification.md` first.

- `src/Utils/BuildVersion.cs` — the build's own version, read once from `application/config/version`; the log's first line and the menu's corner stamp state it.
- `src/Utils/Config.cs` — dev tuning-override: typed getters over an optional sparse `res://config.json`, else the caller's in-code `const`.
- `src/Utils/DisplayModeSetting.cs` — the window's display mode: the saved word against the shipped windowed default, and the one place the window mode is set.
- `src/Utils/EffectPools.cs` — the `effect_pools.json` reader: how many copies of each effect-template root the two stages build, scaled by player count.
- `src/Utils/EffectsLevel.cs` — the original's EffectsLevel option and the clutter fade's squared distance scale it drives, plus the remake's far-fade switch.
- `src/Utils/GameClock.cs` — the session sim clock every sim consumer takes dt from: run mode (realtime/fixed), halt and single-step, time scale, the holds.
- `src/Utils/GraphicsMode.cs` — the opt-in enhanced-lighting setting, resolved once at launch into the one boolean every scene builder reads.
- `src/Utils/HitchMonitor.cs` — the always-on frame-hitch detector: a frame far costlier than its recent neighbours gets a record; it logs nothing itself.
- `src/Utils/HitchSidecar.cs` — the hitch detector's write path: queues a tripped record and drains it to one `[perf] hitch` line plus one JSON sidecar line.
- `src/Utils/HoldToRepeat.cs` — tap-versus-hold timing for one button: an initial delay, then a repeat every interval until release.
- `src/Utils/Log.cs` — the diagnostic log: a fixed category vocabulary over four levels, a filtered console and an always-complete file sink (`.scratch/logs/`, `logs/` in an exported build).
- `src/Utils/MonitorSetting.cs` — the screen the window sits on: the machine's screens labelled, the saved index dropped where no screen answers to it, and the one place the window's screen is set.
- `src/Utils/OptionsStore.cs` — version-tolerant JSON persistence of the process-wide options in `user://options.json`, written atomically.
- `src/Utils/PerfSample.cs` — ambient timed leaf scopes: `PerfSample.Scope(site)` accumulates per site per frame, and a hitch record carries the frame's named work.
- `src/Utils/PhysicsTickCost.cs` — the wall cost of one whole physics tick and the tick count a wall second got, measured by a bracket pair spanning the tick.
- `src/Utils/PresentationResolution.cs` — the requested-versus-active menu presentation resolver, availability checked separately from the saved request.
- `src/Utils/RenderPoses.cs` — the render half of the fixed-tick simulation: the pose a realtime session draws between two simulation steps.
- `src/Utils/ResolutionSetting.cs` — the window size: the sizes a screen can hold, the saved one against the shipped default, and the one place the window size is set.
- `src/Utils/Rng.cs` — the session's one master seed and the named subsystem generators every random draw derives from.
- `src/Utils/ScriptedWindow.cs` — Win32-only window hiding for scripted runs; `ScriptedWindow.Hide()` uses `ShowWindow(SW_HIDE)` on the native window.
- `src/Utils/ShaderTime.cs` — the `csky_time` global uniform: the clock's GPU twin, replacing `TIME` in every generated shader; wraps at 3600 s.
- `src/Utils/StartupProfile.cs` — the always-on `[perf] startup …` line: every session build split into the phases it spends its time in.
- `src/Utils/TapHoldButton.cs` — one button carrying two actions split by how long it is held; the caller feeds it the button level and switches on the answer.
- `src/Utils/VSyncSetting.cs` — the frame pacing: the flag/saved/config ladder, and the one place the vsync mode and the frame cap are applied to the engine.

### `src/Testing/` — the in-engine assertion harness

`--run-tests` and the `--dump-*` probes. The units that need no running engine live in `CSVM.Tests/`
instead.

- `src/Testing/Probes.cs` — the assertion cores behind the `--dump-*` reports: one pass yields the report text and the verdict a suite asserts on.
- `src/Testing/EnvelopeMargins.cs` — one flight scenario's distance from every term that could bound it, plus the decoded branches it drove.
- `src/Testing/TestHarness.cs` — `--run-tests`: the suite registry, `TestContext`, the PASS/FAIL/SKIP table, `test-report.json` and the process exit code.
- `src/Testing/SuiteShards.cs` — the `shard:<index>/<count>` term and the deterministic weighted division behind it, over `analysis/engine-suite-weights.json`.
- `src/Testing/PhaseAttribution.cs` — buckets a build's `StartupProfile` phases into archive/decode, sound preparation and world construction for the report.
- `src/Testing/CountingEmitterFactory.cs` — the no-GPU `IEmitterFactory` fake a suite installs to observe `PUFFER_STATE` emitter lifetime.
- `src/Testing/RecordingEmitterRenderer.cs` — the no-GPU `IEmitterRenderer` fake: keeps a `Puffer`'s particles instead of drawing, so its modes are testable.
- `src/Testing/SuiteCatalog.cs` — the registry of the in-engine suites, discovered from the `[Suite]` attribute on each body and ordered by name.
- `src/Testing/*Suites.cs` — the domain scenario modules holding the marked suite bodies: puffer, combat, ordnance, Instant Action, AI, campaign, zeppelins.
- `src/Testing/SuiteConstants.cs` / `BurstTimeline.cs` / `SuiteViewers.cs` / `EffectStageSuiteHelper.cs` — shared golden inputs, timeline values and fixtures.
- `src/Testing/MenuSuiteHost.cs` — the launchscreen fixture a menu suite builds on: a `MenuHost` with the launcher's features, one seat and silent audio.
- `src/Testing/GoldenShot.cs` — the engine half of the golden-image tripwire: raw-pixel md5 + GPU adapter, printed on every `--screenshot`.
- `src/Testing/ProbeRunner.cs` — the `--dump-*`/`--run-tests`/`--*-test`/`--destroy=` probe wrappers the Launcher and the session node quit into.
- `src/Testing/CaptureDirector.cs` — the `--screenshot=`/`--shots=`/`--frames=` capture state machine + F11/F12, ticked from `_Process`.
- `src/Testing/GltfExporter.cs` — exports the viewer plane subtree to glTF (mesh + livery + baked damage) for `--export-gltf=`/F10, on a throwaway duplicate.

### `src/Session/` — the launch/session layer

The `Launcher` scene root, the per-launch `GameSession` node, and the low-coupling session-build
clusters they delegate to.

- `src/Session/Launcher.cs` — Main.tscn's root: the once-per-process bootstrap, what outlives a session, the menu host, and every path a session starts or ends.
- `src/Session/GameSession.cs` — the per-launch session node: ordered build phases over one `SessionSpec`, owning the clock, world root, panes and runtimes.
- `src/Session/SessionSimulation.cs` — the plain-C# owner of one haltable, ordered session-simulation step; `GameSession` maps its named phases to their owners.
- `src/Session/ExtractionStamp.cs` — reads the extraction provenance stamp at boot and warns once when it is stale or unreadable; `Behind` is the blocking read.
- `src/Session/MenuAudioService.cs` — the menus' audio host: the music channel, the briefing narration player and the cue player behind `MenuCueTable`.
- `src/Session/LiveryResolver.cs` — each player's livery from a `SessionSpec`: the paint catalog, the pattern-mask library and the per-player scheme pick.
- `src/Session/SpawnPicker.cs` — each player's flight spawn: the shared spawn-list index and the per-player point; also the plain `IFlightStarts`.
- `src/Session/IFlightStarts.cs` — the spawn-placement seam: one call answering for the whole field, and the `FlightStart` pair every rig is placed from.
- `src/Session/StartGrid.cs` — the abreast starting grid: every pilot fanned about one anchor spawn, the whole field lifted as one to clear terrain.
- `src/Session/PlaneRoster.cs` — pure lookups over a `SessionSpec`'s plane roster: which plane a player flies, and its display name.
- `src/Session/EffectCatalogue.cs` — the name tables saying which authored anims are playable effects, and the anchor roots both effect binds stage from.
- `src/Session/SurfaceDefTable.cs` — one of the original's per-surface anim-def vectors and the cascade that indexes it with a struck material's surface id.
- `src/Session/WeatherRig.cs` — loads the mission's weather and drives the per-rig skydome, whiteout, deck and zone gate each frame.
- `src/Session/WorldEffectsFactory.cs` — builds the impact/destruction effect stages and the per-plane crash runtime.
- `src/Session/LensFlareRig.cs` — the sun's lens flare: screen-space sprites along the sun-to-centre line plus the wash, one instance per pane.
- `src/Session/FlightRoster.cs` — the session's aircraft set: builds the human field in player order and introduces AI aircraft later through one assembly seam.
- `src/Session/FlightRosterInputs.cs` — the roster's grouped dependency contracts: aircraft resources, world bindings, human-session bindings and the policy.
- `src/Session/HumanFlightAdapter.cs` — the roster's private human path: painted plane, controller, loadout, instruments, damage visuals, spawn, crash rig.
- `src/Session/AiFlightAssembler.cs` — the roster's private AI path: pilot preparation, model, controller, loadout, damage and crash runtime, and placement.
- `src/Session/CrashRigQueue.cs` — the queue of crash rigs for aeroplanes already flying, advanced one build step a frame so a launch costs less on its frame.
- `src/Session/InstantActionDirector.cs` — the engine side of one Instant Action mission: the actor phases, the sequencer tick and the end-condition wiring.
- `src/Session/InstantActionRuntime.cs` — one Instant Action mission's actor set and its end, engine-free: the ace, wingmen, wave draws and objective zeppelin.
- `src/Session/InstantActionWaves.cs` — the decoded wave sequencer, engine-free: the wave counter, the spawn draw against live humans, the fan geometry.
- `src/Session/SpectateHandoff.cs` — the shared pane handoff for a downed pilot whose teammates fly on: the wreck pinned, a spectator camera in the freed pane.
- `src/Session/ScriptedPathVehicles.cs` — one mission's scripted-path vehicles: the snap onto waypoint 0, the freeze, `START_TAXI`'s release, the handoff back.
- `src/Session/SurfaceVehicleRuntime.cs` — builds and steps a mission's `mode ship` hulls: a library-root copy placed on the water, indexed on the runtime.
- `src/Session/SurfaceVehicle.cs` — one built hull: the scripted-path follower over its patrol net, the wake and injure anims, and the pool a hit reaches.
- `src/Session/SurfaceGunner.cs` — a hull's own gun: the non-jet acquisition, the 20 s target hold, the mount, and the fire decision on the def's authored tuple.
- `src/Session/CampaignRoster.cs` — the engine-free plan of a campaign mission's `aiv` roster: each block's airframe, and its net or its netless escort.
- `src/Session/GeneratorCycle.cs` — the decoded egen launch timing law for one generator, pure and engine-free: composed periods, hold-not-cancel, the credit.
- `src/Session/NetTrailerTargets.cs` — resolves a patrol net's trailer name (`player`, a zeppelin) to a live position, so an anchored net rides its target.
- `src/Session/AiGeneratorRuntime.cs` — runs a mission's egen generators (`--generators`): the load drops, the cycle stepping, each launch's spawn or release.
- `src/Session/AiVoiceRuntime.cs` — wires the combat-voice dispatcher into a session: the speakers, the damage sources and the sites each clip plays from.
- `src/Session/ZeppelinRuntime.cs` — runs a mission's zeppelins (`--zeppelins`): the placement, the net flight, the per-part damage and kill, the script's arms.
- `src/Session/ZeppelinRuntime.Cannons.cs` — the broadside half of that partial: the cannon wiring, the target and arc gate, the anims and the rounds fired.
- `src/Session/TurretEmplacementRuntime.cs` — the world AA emplacements: placed against the built world, in the shared aim pool, stepped after the airships.
- `src/Session/CampaignProfileStore.cs` — JSON persistence for one named campaign profile: funds, owned planes, mission records, awards and the destruction log.
- `src/Session/CampaignProgression.cs` — the rules that write a profile: an attempt's best-of merge, the monotonic position, the rewards and the skip offer.
- `src/Session/CampaignPersistLog.cs` — the cross-mission state log: what a mission left destroyed, carried silently into later missions of the same chapter.
- `src/Session/CampaignLoadout.cs` — the bridge between a profile's stored ammunition and ordnance picks and the `LoadoutChoice` a launch hands the session.
- `src/Session/ObjectiveScript.cs` — one mission's parsed `objectives.zrd`: the contiguous `OBJECTIVEn` blocks, in the typed shape the graph runs.
- `src/Session/ObjectiveGraph.cs` — the objectives runtime, engine-free: the four-state machine, the rotating completion scan, the conditions, the four endings.
- `src/Session/CampaignHumanField.cs` — the human field's rules, engine-free: what a condition naming one aeroplane asks once two to four humans fly.
- `src/Session/ObjectiveSites.cs` — the flown campaign mission's objective sites as targeting candidates, rebuilt from their live source every frame.
- `src/Session/CampaignDirector.cs` — the engine side of a campaign mission: the graph armed against the built world, the roster spawned and launched off its hooks, the attempt recorded.
- `src/Session/CampaignDangerZones.cs` — a campaign mission's own danger zones: the `dzpathN` gates its script arms, tracked per human by the stunt gate rule.
- `src/Session/AirframeSwap.cs` — the three `CALLBACK` codes that hand the player a different airframe in mid mission, and the def and node each names.
- `src/Session/CutsceneController.cs` — the host a cutscene definition raises its `CALLBACK` codes to, and the session state those codes describe.
- `src/Session/LandingApproachRuntime.cs` — the mid-mission cutscene trigger: `landings.zrd` rows tested against each flying human, and the auto-land offer.
- `src/Session/LadderSwitch.cs` — the rope-ladder switch as an engine-free rule and state machine, plus the co-op holder rule deciding which human owns it.
- `src/Session/LadderSwitchRuntime.cs` — that switch flown against the built world: the per-human attitude and sensor read, and the definitions it starts.

### `src/Bindings/` — the input binding model

What a binding is, how one resolves against hardware, and the named actions a polling site asks
for. The registry that turns a device identity into a live pad and the map that holds the actions
both sit on top of these types.

- `src/Bindings/DeviceId.cs` — which device a binding is on, as a value: the one keyboard, the one mouse, or a joypad named by its stable hardware string.
- `src/Bindings/BindingControl.cs` — the tagged control: a key, a button, a mouse button, one signed half of an axis past a deadzone, or one hat direction.
- `src/Bindings/Binding.cs` — one control on one named device, plus `ControlValue`, the held/how-far pair every resolution returns.
- `src/Bindings/IDeviceState.cs` — the tick's raw hardware state addressed by device identity; the seam that keeps resolution engine-free.
- `src/Bindings/GodotDeviceState.cs` — the live `IDeviceState` over Godot's `Input` singleton, resolving an identity to an index through a `DeviceRegistry`.
- `src/Bindings/DeviceRegistry.cs` — the joypad index-to-identity table, rebuilt from the connected-pad list rather than trusting an index across a replug.
- `src/Bindings/SeatDeviceState.cs` — the `IDeviceState` a seat reads: the platform's keyboard and mouse, plus the seat's pad set behind a placeholder.
- `src/Bindings/BindingSet.cs` — the bindings one action holds, ORed the way the original ORs its four slots, the deepest deflection winning the analogue read.
- `src/Bindings/InputAction.cs` — the enum of named actions, one per binding a polling site holds, contiguous because the snapshot indexes arrays by it.
- `src/Bindings/ActionMap.cs` — one player's keymap: which control fires which action, with assignment taking a control off every action that held it.
- `src/Bindings/ControlCapture.cs` — what a rebinding screen may capture, and the release-first scan that turns a press into a binding on the seat's identity.
- `src/Bindings/ICaptureDevices.cs` — the hardware a capture reads through: one reader per context, with the pad identity that context's bindings sit on.
- `src/Bindings/SeatCaptureDevices.cs` — one seat's capture readers, a `SeatDeviceState` per context over one pad list, on that context's placeholder identity.
- `src/Bindings/BindingLabels.cs` — what a rebinding screen prints: an action's name, a control's keycap name, and a row that counts what it is not showing.
- `src/Bindings/ActionSnapshot.cs` — the tick's resolved values, so two consumers reading one action in one tick get the same answer. No edges and no history.
- `src/Bindings/PlayerActions.cs` — the seam a polling site holds: a map, the tick's snapshot, and the pad-only keyboard gate for splitscreen players.
- `src/Bindings/InputContext.cs` — which set of controls a seat is reading (flight, menu, camera), because one control means different things per mode.
- `src/Bindings/DefaultBindings.cs` — the shipped keymap as data, one map per context, reproducing `docs/controls.md`, and the placeholder a pad default uses.
- `src/Bindings/BindingProfile.cs` — one seat's whole input: a map and a `PlayerActions` per context, plus the keyboard gate that applies to all of them.
- `src/Bindings/BindingStore.cs` — the versioned JSON keymap file, one per player under `user://`, falling back per action to the shipped default.
- `src/Bindings/LaunchBindings.cs` — where a seat's keymap comes from when the seat is built: the player's saved file, or the shipped defaults.

### Session root and tests

- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy, the launch-time roster split, the focus gate and `--no-pads`.
- `src/SessionPaths.cs` — resolves the extracted-data paths (per-chapter gamez/texture/zrdr, per-mission zrdr) under a data root, unpacked folder or `.zip`.
- `src/SessionSpec.cs` — the launch args as one immutable, engine-free value: `Parse` parses **and** resolves, plus the pure arg parsers the tests reach.

- `CSVM.Tests/` — the xUnit project (`dotnet test`): engine-free reader units on hand-authored fixtures + `extracted/` golden counts, skipped when absent; plus eight former in-engine suites moved here as `Probes.*`/plain-static/`StuntMission`/`GaugeCluster` facts.
