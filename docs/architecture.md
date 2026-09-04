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
- `src/Mech3/WorldCollision.cs` — derives every world collider's `Disabled` flag from its owner's tree visibility (+ the fade channel), so hiding anything drops its collision.
- `src/Mech3/PlaneBuilder.cs` — builds one aircraft from its GameZ subtree (shaded, backface-culled); `Repaint` re-liveries it in place.
- `src/Mech3/PaintScheme.cs` — one aircraft livery: pattern + 3 colours + 3 decals, parsed from vehicle.json or drawn at random.
- `src/Mech3/PatternLibrary.cs` — decodes the original's `.BM` paint patterns from the extracted ROF archive; `PatternsFor` lists a plane's liveries.
- `src/Mech3/PlanePainter.cs` — applies a `PaintScheme` to one aircraft: composites skins from the pattern's region masks, swaps decals.
- `src/Mech3/PropParts.cs` — classifies prop/rotor nodes by name; spin axis + rate from the original anims (props Z, rotor Y).
- `src/Mech3/ControlSurfaces.cs` — classifies left/right aileron, elevator and rudder mesh nodes and their hinge axes (X ailerons/elevators, Y rudders).
- `src/Mech3/WingLights.cs` — the one source for wingtip nav lights: flare node names, glow texture, warm-amber colour, blink period.
- `src/Mech3/WorldBuilder.cs` — builds a chapter world: placed + partition subtrees, cloud deck, camera-anchored skydome, edge extender.
- `src/Mech3/MapEdgeExtender.cs` — rolling window of repeated border-cell blocks + clutter continuing the world past the map edge, per camera; block depth is per chapter (`DefaultBlockCells`).
- `src/Mech3/Clutter.cs` — stamps interp.json clutter templates onto matching-textured terrain at the polygon's own UV lattice, gated per polygon by `no_clutter`: sprites, plus C2/C5's solid 3D city blocks.
- `src/Mech3/ClutterTemplates.cs` — the `templates.zrd` reader: each clutter decoration model's authored substitution table, scale range and fade distances, plus the five keys no chapter authors.
- `src/Mech3/FogVolumes.cs` — the `fogvol.zrd` reader + the gamez `fvol*` volume census: what the ambient cloud field scatters, and where.
- `src/Mech3/Zrdr.cs` — zrdr extraction reader (zip or dir) + `ZrdrDict`, the key/[values…] view over a reader's list.
- `src/Mech3/LandingApproaches.cs` — a chapter's `landings.zrd` approach table resolved against the gamez: each row's condition volume (the `cone`/`half_cone`/`sphere` child's single authored triangle, expressed in the approach node's own frame), its attitude cone and its speed band, plus the geodesic attitude test. Engine-free geometry; `LandingApproachRuntime` flies a player against it. Decode: `docs/formats/anim-definitions/cutscenes.md`.
- `src/Mech3/Pickups.cs` — a mission's compact `pickups.zrd` sensor/radius table, the spheres `LadderSwitchRuntime` tests the player against. Nothing starts the pickup timing off it: the train's own `train_on_track` definition calls `pickup_timing` at mission load. Decode: `docs/formats/anim-definitions/cutscenes.md`.
- `src/Mech3/MissionCutscenes.cs` — which of a mission's `mis_anim.zrd` `ANIMATION_DEFINITION_FILE` entries sit under its own `cutscenes\` directory, and the `ANIMATION_NAME`s they define. That directory is the authored classifier for mid-mission choreography (nine story missions ship one); `WorldSession` hands the names to the cutscene host and to `AnimRuntime.RangeGatedCalls`. Decode: `docs/formats/anim-definitions/cutscenes.md`.
- `src/Mech3/AiNets.cs` — the chapter AI patrol nets: `ne0NNNNN` waypoint graphs + the `neindex` id→name table, raw tags/trailer and the net's own three volumes included.
- `src/Mech3/AiVolumes.cs` — `AiVolume`/`AiVolumeSet`: the activation/attack/return volumes as a roster block (slots 8–19) and a net record (elements 2–10) author them, with the engine's non-zero overlay.
- `src/Mech3/RosterMarkers.cs` — grafts a roster block's authored marker scaffolding onto the rig its spawn built: the chapter's own copy of a vehicle is a library root the world never places, so whatever that copy adds under `markers` past the shared airframe's is built there, hung under the airframe's mark of the same name, switched to its authored `active` bit and indexed on the world runtime, which is what gives an index-addressed definition a node to write and a condition volume that moves with its aircraft. It also makes the rig itself answer for that library root's own name and index (`AnimRuntime.IndexSpawnedVehicle`), so a definition posed `AT_NODE` the vehicle reaches the aeroplane the mission actually spawned.
- `src/Mech3/VehicleDefs.cs` — the `vehicle.json` def index a roster spawn resolves a block against: the def behind a block name, its `mode` through `kind_of`, and the player airframe node its model is built from.
- `src/Mech3/Maneuvers.cs` — the shared maneuver library (`zrdr/maneuvers.zrd`): 17 timed attitude-step programs with `natural_touch` difficulty gates, the eligibility cull, and the `signature_maneuvers` bitmask decode.
- `src/Mech3/CampaignSequence.cs` — the shared `cm_sequence.zrd` reader: the campaign's 24 flat mission entries, each one's storage address (world folder, mission folder, `Persist.NNN`/`Mission.NNN` save id) and the backwards walk to the previous mission of the same world folder that cross-mission persistence is scoped by. Decode: `docs/formats/campaign-sequence.md`.
- `src/Mech3/EnemyGenerators.cs` — the mission `egen.zrd.json` reader: the 23 enemy generators in their three shapes (zeppelin launch / plain / moving spawner), `[null]` files as empty.
- `src/Mech3/Zeppelins.cs` — the mission `zeppelins.zrd.json` reader: the 58 zeppelin instances (motion limits, net, gasbags/healthy/engines, cannons), all values in authored units.
- `src/Mech3/InstantAction.cs` — `InstantActionDef` + the `ia.zrd.json`/`--ia=` readers: mission type, wingmen, four waves, ace, with every optional key resolved to the original's own built-in default.
- `src/Mech3/AiSkills.cs` — the `ai_skill_parameters` endpoint pairs from player.json (1–9 ratings, linear between the decoded endpoints) + the roster accessors: the skill vector (slots 22–30 by stat name), `primary_target` (slot 6), `rating_biases` (slot 33, `AiRatingBias` wildcards) and the spawn-facing slots (`netids`, pose, team, group, title, `deactivated`, `pref_engage_alt`, the signature mask, `taxiPath`, the accent).
- `src/Mech3/Messages.cs` — the game's localized string table: the `messages.json` key→value map behind every `MSG_*` key.
- `src/Mech3/UiStrings.cs` — the original's UI string table (`extracted/rof/ui_strings.json`) by id: langui rows only (ids repeat across the file's two tables), `FormatMessage` placeholders (`%1!d!`) converted to composite format, leading `[FONTID]` tags stripped.
- `src/Mech3/TgaImage.cs` — the engine-free TGA decoder behind the hangar's art (`extracted/rof/ASSETS/GRAPHICS`): types 2 and 10 (RLE) truecolour at 24/32 bits, both row orders, to top-down RGBA8; anything else, or a malformed/absent file, is null. `FromRgba` wraps an already-decoded buffer as one of these, which is how `PngImage` reaches the same art path.
- `src/Mech3/PngImage.cs` — the engine-free PNG decoder behind the menus' `rimage` art (the campaign briefing's maps and flag pins), returning a `TgaImage` so both decoders feed one seam: 8-bit non-interlaced truecolour with (colour type 6) and without (type 2) alpha, which is all 254 files that extraction ships, all five row filters; a palette, a 16-bit channel, an Adam7 file or a malformed/absent one is null rather than a throw.
- `src/Mech3/ArtImage.cs` — the one door menu art is loaded through: a path in, a decoded `TgaImage` or null out, decoder picked from the extension (`.PNG` → `PngImage`, `.TGA` → `TgaImage`). A screen names the file the extraction ships and stops caring what format it is, which is what let the hangar's art seam stay TGA-only while the PNG art beside it went undrawn. **JPEG has no decoder and needs none.** `extracted/rof/ASSETS/GRAPHICS` holds 26 `.JPG` (11 `PC_P_HANGAR<n>`, 15 menu backgrounds, 14 of those 800x600 and `MP_ERRORMESSAGEBACKGROUND` 380x206), 24 of them baseline (SOF0) and two progressive (SOF2, `CR_BACKGROUND` and `MP_LOBBY_BACKGROUND`), so a hand-written baseline decoder several times the size of `PngImage` would leave two files blank. It is not needed and neither is an extract-time PNG sidecar: every JPEG picture a screen names is a **board** picture, and `ComposedBoardView.Load` reads it through Godot's own loader, both progressive files included, pixel-identical to a reference decode. A `.JPG` handed to this door still returns null, which is the correct answer: a stand-in picture on a fidelity screen reads as a verdict about the original.
- `src/Mech3/MarkerRig.cs` — a plane's firepoint/pylon/target rig from planes.zbd: plane-frame positions + co-located mounts; feeds `--dump-markers`.
- `src/Mech3/CompiledAnim.cs` — reader for the compiled `cam_anim`/`mis_anim` archives: anim defs, sequences/events, lazy SI-script pool.
- `src/Mech3/AnimDefs.cs` — the zrdr front-end: ANIMATION_DEFINITIONS reader files, normalized into one `AnimDefinition` model.
- `src/Mech3/AnimProgram.cs` — merges the compiled + reader defs for one mission, holds `startanims`, resolves SI-script slots.
- `src/Mech3/TextureCycler.cs` — runs the gamez material `cycle` flipbooks (water, surf, wakes) by swapping `albedo_tex`.
- `src/Mech3/WorldSounds.cs` — `SOUND_NODE` ambient 3D emitters (one pooled player per host node) + `PlayOneShot` for destruction/impact audio.
- `src/Mech3/WorldLights.cs` — packs the world's `LIGHT_STATE` point lights into the `csky_light_data` texture the fullbright world shader reads.
- `src/Mech3/MissionSetup.cs` — parses + applies the per-mission `.gw` interp script deciding which world entities a mission shows.
- `src/Mech3/AnimRuntime.cs` — the animation engine: bootstrap, live def instances, event dispatch, motions, conditions, lights, puffers, world effects.
- `src/Mech3/Anim/` — `AnimRuntime`'s motion value types (`IAnimMotion` and its four implementations, the bind-census enums), split out of `AnimRuntime.cs` into their own files/namespace for size; plus the sound, light and pose/visual dispatch-axis families.
- `src/Mech3/Anim/MotionSet.cs` — the live motion collection: the two registration rules, the per-frame sweep, and the pending-bounce predicate the instance walk retires on.
- `src/Mech3/Anim/EmitterDirector.cs` — every PUFFER_STATE emitter's whole life on one runtime: the keying rule, the start, all four stops, the respawn wipe, the per-frame follow, and the census. Plus `IEmitter`/`IEmitterFactory` and the real/retired adapters.
- `src/Mech3/Anim/SoundChannel.cs` — one runtime's `SOUND_NODE`/`SOUND` events: the pooled ambient emitters, the one-shot player, the late-failure census, and the sound half of `OBJECT_ADD_CHILD`/`OBJECT_ACTIVE_STATE`.
- `src/Mech3/Anim/LightChannel.cs` — one runtime's `LIGHT_STATE`/`LIGHT_ANIMATION` events: the live point-light table, the signed-delta tween, and the per-frame submission to `WorldLights`. `AnimLight` stays its own value type in the same namespace.
- `src/Mech3/Anim/PoseChannel.cs` — one runtime's object-pose/visual events (`OBJECT_ACTIVE_STATE` through `OBJECT_MOTION_SI_SCRIPT`): the pose helpers, the subtree opacity/fade machinery, and the motion-builder role that hands finished motions to `MotionSet`.
- `src/Mech3/Anim/NameResolver.cs` — name→node resolution: the index, wildcard matcher, memoized `FindAll`, the three-tier scope chain (`Resolve`/`ResolveScoped` with the `ownRootsOf` hook and the `stagingAdmits` pooled-copy filter), the symbol authority, `Anchors` (narrowing + root lift) and the bind census; generic over the node type, off-engine testable.
- `src/Mech3/SequenceRunner.cs` — the engine-free sequence interpreter (event clock / LOOP / IF-ELSEIF), extracted behind the 3-member `ISequenceHost` seam; headlessly testable.
- `src/Mech3/DestructibleRegistry.cs` — live per-instance HP for `HEALTH>0` anim defs, one pool per `(def,anchor)`; `Resolve` maps a struck collider back.
- `src/Mech3/ScriptedPath.cs` — resolves an authored waypoint path (`pp1` → the gamez `pp1_aipath` subtree) into ordered world-space waypoints.
- `src/Mech3/WorldPartitionGrid.cs` — which gamez nodes a world-space XZ rectangle covers, off the World node's own cell table; the area verb's selector.
- `src/Mech3/WorldSession.cs` — builds a chapter world + binds its `AnimProgram` (load→WorldBuilder→clutter→bind→sound-prewarm); `--node=` slices it to one subtree.
- `src/Mech3/AircraftStage.cs` — stages the two aircraft-archive subtrees a story-mission intro animates into a chapter world's animation node table, at that chapter's cross-archive pointer base.
- `src/Mech3/SessionArchives.cs` — `OpenFor(ArchiveIntent)` opens the five archives a chapter build needs and the matching `WorldSession.Options` lifetime flags, so `GameSession`, the anim lab and the test harness open the same five without hand-setting the flags.
- `src/Mech3/DecodeCache.cs` — the opt-in store of decoded, read-only world inputs (`GameZ`, `AnimProgram`) keyed by their source paths, so a process building one chapter many times decodes it once.
- `src/Mech3/EmptyStage.cs` — the `--stage=empty` test stage: a collidable ground plane under a code-generated grid, standing in for a chapter world.
- `src/Mech3/WavFile.cs` — pure-C# WAV parser + MS ADPCM→PCM16 decoder (the game's format; Godot can't load it).
- `src/Mech3/WavCues.cs` — the RIFF `cue ` chunk of a WAV, as times in seconds (zip or dir): the briefing narration's marker points, which is the only clock a reveal script does not carry itself. Times come back ascending because a `WaitForMarker` number indexes them by sample offset and 13 of the 24 briefing wavs store their points out of that order. Kept apart from `SoundArchive`, which decodes to a Godot stream, so a menu page needing only timings stays engine-free. Decode: `docs/formats/briefing.md`.
- `src/Mech3/SoundArchive.cs` — WAV lookup over a sounds extraction → cached `AudioStreamWav` (forward loop when LOOPED).
- `src/Mech3/MusicPlayer.cs` — the state-driven score: one 2D streaming channel for menu, cabin and mission, with the decoded battle hold.
- `src/Mech3/MissionRadio.cs` — the mission radio queue: the non-positional voice channel the campaign's objective callouts and VO dialogue chains speak on.
- `src/Mech3/SoundDefs.cs` — sounds.json parser: SETS `snd_*` → `SoundDef`; `LoadGroups` → the weighted-random `SOUND_GROUPS` + their dialogue chains.
- `src/Mech3/CombatVoice.cs` — the combat-voice chain: roster `accentID` → `voice.zrd` pool → pilot VO id → clip defs / the shipped `_random` variant groups; the mission's voice prewarm set.
- `src/Mech3/Anim/TemplateStage.cs` — the effect-template stage as one module: pool-slot arithmetic, root resolution and retirement.
- `src/Mech3/EffectCycles.cs` — the `EFFECTS` block of the shared `effects.zrd`: the second source of animated material cycles.
- `src/Mech3/SurfaceRegistry.cs` — the original's compiled+level-supplied surface-name registry: material `soil` id → name, for building `player_crash_<name>`/`touchdown_<name>`.

### `src/Flight/` — the flying aircraft

The plane as a flying, shooting, damageable thing, plus its HUD and stunt mode. Reads plane stats
from the extracted zrdr; owns the arcade physics and everything drawn over the pilot's view.

- `src/Flight/PlaneStats.cs` — typed per-plane stats from vehicle/engines/player.json: dynamics, engine sound, destroyable parts.
- `src/Flight/ShakeDefs.cs` — typed reader over shakes.json: the six shake-oscillator sources (law + per-source magnitude term).
- `src/Flight/WeaponDefs.cs` — typed reader over `weapons.json` `BALLISTICS`: 48 `WeaponDef`s; inspect with `--dump-weapons`.
- `src/Flight/Loadout.cs` — `stock_loadouts.json` reader + `Bind` to a built plane: gun groups + hardpoints, markers→muzzle nodes; `--dump-loadout`.
- `src/Flight/WeaponBench.cs` — the world-less 48-weapon mount-and-fire pass check behind `--weapon-test` and `weapons-fire`; fires the whole `ForRig` rig, no lab node involved.
- `src/Flight/FireControl.cs` — the engine-free fire-control state machine (BL-295): trigger edges, fire clocks, ammo draw-down, both selectors, dry cues; `FlightController` performs its `FireOutcome`.
- `src/Flight/AimAssist.cs` — the gun aim assist (`BL-342`): `GunAimSlot`'s plane-local per-muzzle state and the forget + catch-up pass, the intercept solver, the four-list candidate scan, and the fire call's step order + 1° launch scatter.
- `src/Flight/TargetRef.cs` — the player-targeting abstraction: one value over every selectable thing (aircraft, mission structure, turret), wrapping an `AimCandidate` for the pose/team/liveness/source half and adding class, label, optional health/armor, plus `Classify` (the decoded class model) and source-identity matching.
- `src/Flight/TargetPool.cs` — the player's classed candidate pool: the three cycles (Enemy/Objective, Ally, Non-Aircraft) of `TargetRef`, rebuilt from scratch off the aim assist's `Vehicles`/`Turrets` lists plus an explicit sub-part list; the one place a concrete source type is read.
- `src/Utils/TapHoldButton.cs` — one button carrying two actions, split by hold duration: edge-detects a level read, times it over `HoldToRepeat`, and answers tap / hold / nothing. The tap resolves on RELEASE; a press whose hold fired is spent. Pure, so the decoding unit-tests even though a gamepad does not.
- `src/Flight/TargetSelection.cs` — the sticky player selection: owns a `TargetPool`, sorts it into the decoded cycle order, re-finds the selection by entity each frame, and carries every action (next/previous/nearest per class, nearest-crosshairs, target-nothing) plus the attacker queue and the lifecycle. `ApplyInitial` is `--target=`'s seam.
- `src/Flight/TargetHud.cs` — the per-pane targeting HUD, built in every flight session: the selected-target bracket marker and label, the nearest-AI-hostile fallback, and `--debug-markers`' every-aircraft overlay.
- `src/Flight/TurretDefs.cs` — typed reader over `ai.zrd`'s `TURRET` section: 42 `TurretDef`s, carried/standalone split, arcs, duty cycle, weapon block.
- `src/Flight/TurretController.cs` — one carried turret gunner: acquire, intercept, wrap-aware arc clamp, bounded slew, duty cycle, geometric fire into the shared pool.
- `src/Flight/AiPilot.cs` — the non-player `FlightModel` driver: mutable standing orders (heading/altitude/throttle, optional patrol net, optional gunner whose live target is pursued, optional formation escort, optional mode machine that dispatches all of it) → one `FlightInput` per sim step; each mode picks the aim point and table `AiControlLaw` steers on. `SteeringPatrol` reports whether the last step actually flew the net (F13's leashes read it).
- `src/Flight/AiControlLaw.cs` — the original's own AI steering law (decoded in `docs/org/aiControlLaw.md`): aim point + that point's velocity + one of four decoded parameter tables → stick and throttle lever. Engine-free and pure.
- `src/Flight/AiEscort.cs` — the formation-escort law a netless `mode wingman` flies (D34, decoded in `docs/org/aiPilot.md`): leader and selected-target snapshots in, one station point and its velocity out, over the engine's own five-state machine. Held by `AiPilot.Escort`, which dispatches to it in place of every other mode but stunned and avoid crash.
- `src/Flight/AiModeMachine.cs` — the nine-mode AI state machine, the engine's decoded mode vocabulary: patrol/pursue/lay off/evade/evasive maneuver/stunned/avoid crash + two enum-only danger-zone modes; steady-hand and sixth-sense reaction rolls on the shipped chances.
- `src/Flight/AiGunner.cs` — the AI's forward-gun gunnery: intercept lead via `AimAssist.TryIntercept`, the quick-draw cone and the engagement window as fire gates, the ±11° traverse clamp with its 10° residual gate, per-shot dead-eye scatter; two mutable target fields (D36, `BL-363`) — aircraft-only `Target`, `AiPilot`'s own pursuit quarry, and non-aircraft `GroundTarget` (a turret or a world/zeppelin structure) so the flight law never chases what it cannot dogfight — plus primary-target name and rating biases (the D12 script seams).
- `src/Flight/AiRocketeer.cs` — the AI's ordnance employment: the quick-draw cone over the whole pass, then per pylon the armed check, the two-way `DAMAGES_ZEPPELIN` match, the 200–800 m band and the traverse clamp with its 5° residual gate (tighter than the gun's 10°), then the vehicle-wide lockout stamped ahead of the `quick_draw_chance` roll; the lead is per pylon, a motor round on its `ACCELERATION` ramp in the launcher's frame and a round without one at `VELOCITY` in the world's. Holds no target of its own: the host walks it against `FlightController`'s standing-target lookup (`AiGunner.Target` or `GroundTarget`).
- `src/Flight/AiVoiceDispatcher.cs` — the combat-voice trigger dispatch, engine-free: the talker roll, the 15 s per-slot cooldown armed on failure too, the bearing halving, the broadcast election, the DI tiers, the death cries with force, the computed bearing index.
- `src/Flight/AiTargetRanking.cs` — the decoded target-ranking formula: rank = weight × 1200 + distance + objectiveBias, minimised; player base weight 0.7, ±0.2 bearing/altitude/facing terms, 1e21 beyond activation; rating-bias matching (a turret's flat `+37.5` handicap included, D36) and the allied-attacker deconfliction pick.
- `src/Flight/AiNetFollower.cs` — walks an `AiNet` patrol graph as waypoints: nearest node first, then edge-list neighbours, seeded branch draws, and an anchored net offset onto its live trailer target (`BL-377`); aircraft-agnostic, shared by `AiPilot` and `ZeppelinMotion`.
- `src/Flight/ZeppelinBroadside.cs` — the pure broadside law (M4 F19): the decoded 90° side arc (dot > 0.707 on the moving hull's lateral axis), the per-cannon stowed→deploy→ready→fire machine with its own re-fire timer, the ballistic lead solve (skip on no solution) and the seeded gasbag pick.
- `src/Flight/ZeppelinDamage.cs` — the pure zeppelin kill arithmetic (M4 F18): the decoded survivor threshold over the `healthy` list, the engine recount, the DAMAGES_ZEPPELIN gasbag gate, the record-stage crossing helper.
- `src/Flight/ZeppelinMotion.cs` — the kinematic zeppelin motion law (M4 F17): forward-only flight along a net under the record's speed/accel/rate/pitch limits, plus the decoded sqrt engine-loss curve behind the `AliveEngines` seam.
- `src/Flight/ManeuverExecutor.cs` — plays one maneuver's attitude-step program as `FlightInput` per sim step: the input source D11's state machine runs during `evasive maneuver`.
- `src/Flight/WeaponCursor.cs` — `FireControl`'s internal ammo-slot index math (`NextArmed`/`NextSelectable`); nothing else calls it.
- `src/Flight/RocketTriggerLatch.cs` — the rocket trigger's consumed-press latch (`BL-583`): arms when flight regains input (a cutscene skip, a pause-menu Resume) while F/A is still down, and reads the trigger released until that button lets go. Public so its own unit tests can drive it; `FlightController`'s only caller.
- `src/Flight/Ballistics.cs` — the VELOCITY/ACCELERATION/GRAVITY integration step, shared by `ProjectilePool` and the reticle's projected impact point.
- `src/Flight/DisablingIntensity.cs` — the decoded `SONIC`/`FLASH` intensity plateau and `FLASH`'s facing test, on squared distances; feeds the player's wash weight and the AI stun's duration.
- `src/Flight/Difficulty.cs` — the difficulty setting as the engine's 0/1/2, its two naming vocabularies, and the enemy armour/health multiplier it scales spawns by; it reaches nothing else.
- `src/Flight/TanglerChoke.cs` — the choker's engine-dead duration and the `ENGINE_DEAD` globals it reads; the original's squared-distance-over-raw-radius mismatch, reproduced.
- `src/Flight/SmokeScreens.cs` — the smoke screen's stun trap: the world's active screens, walked over the roster every sim step to stun AI and wash humans behind the layer; the cone rule, the wash cadence and the three `player.json` tunables beside it.
- `src/Flight/BeeperTags.cs` — the beeper's paint and the seeker's pick: the world's tag list with its countdown, dead-aircraft slam and five-second tail, the tagging gate, and the per-frame query with the original's inverted-dot, squared-distance selection rule.
- `src/Flight/CamParams.cs` — one aircraft's camera tuning from `camparam.json`: `default` plus its own block, keyed by DISPLAY name. Only `Dist` is applied.
- `src/Flight/PilotViewMode.cs` — the three player-selectable views (Chase/Cockpit/Nose, valued as the engine's own camera modes 0/6/7) and `PilotView`, the pure rules over them: cycle, first-person test, the held-key override and whether the numpad holds a fixed view at all in this mode (`HoldsFixedViews` — it does not in first person, where the numpad is the head-look snap cluster), the `--view=` spelling. Engine-free, so the decisions unit-test.
- `src/Flight/CameraController.cs` — the flown plane's camera: roll-following chase, numpad fixed views, the pilot's selected view mode, the weapon lab's held-airframe orbit. Steers a `Camera3D` it does not own.
- `src/Flight/HeadLook.cs` — the pilot's head in a first-person view: snap directions, free-look integration, the center key, and the exponential smoothing that carries the shown angles to their targets. Engine-free, so every law unit-tests.
- `src/Flight/CockpitVisibility.cs` — the per-mode hiding of the pilot's OWN aircraft in a first-person view: interior in and body out for Cockpit, both out plus `markers`/`dontmove` for Nose, everything back for any external pose. `Rules` is pure; `Bind`/`Apply` write it onto one built plane model.
- `src/Flight/CockpitOverlay.cs` — `--cockpit-pass`: the cockpit interior drawn in a `SubViewport` world of its own, camera and panel at the origin, composited under the HUD. One per player, on that rig's `HudParent`.
- `src/Flight/ImpactOutcome.cs` — what a weapon×surface hit should do (effect, sound, stand-in, damage) as a value; `Resolve` is pure and engine-free.
- `src/Flight/Projectile.cs` — `ProjectilePool`: the weapon-fire subsystem — ballistics, the steering step (turn clamp, speed penalty, `LOCK_ON_LEAD`, the seeker's retarget), tracers, flashes, per-surface impact, damage to destructibles, the beeper's paint.
- `src/Flight/ProjectileFlyoutAnim.cs` — `ProjectilePool`'s `FLYOUT MODEL_ANIMATION` half: each ordnance round runs its def on the sequence interpreter, the pool as host (trail puffers, the torpedo's launch look and switch, its sounds).
- `src/Flight/WarningShotCue.cs` — the shipped near-miss accumulator (player.json `warning_shot_*`) + swept-segment/point distance; engine-free so it unit-tests.
- `src/Flight/IncomingFire.cs` — `--incoming`: the near-miss test rig — a phantom shooter on each player's six, so the cue is reachable deterministically without an AI gunner.
- `src/Flight/SpawnPoints.cs` — flight spawn from the mission's own zrdr: ia.json `spawn_points`, or objectives.json PLAYER_INIT as fallback.
- `src/Flight/MissionTargets.cs` — mission `targets.json` loader: world-node name → objective display keys and the `objective`/`other_target` marker flags a mission starts with, resolved through `Messages`.
- `src/Flight/StuntMission.cs` — Stunt Flying state: ia.json `dzones` → a danger-zone run with completion, clock and splits, one per pilot.
- `src/Flight/HudMetrics.cs` — the one rule for HUD sizing: window height / 1440, damped by `sqrt(paneH/windowH)` for splitscreen.
- `src/Flight/HudFont.cs` — the game's own 5px HUD bitmap font, auto-segmented from `rimage/5pointhud*.png`; `--hud-font-test` proves it.
- `src/Flight/WeaponReadout.cs` — the selected-weapon text readout: gun group + rocket type and live ammo, in the game's own HUD font.
- `src/Flight/ImpactReticle.cs` — the gun aiming pipper: 0.5 s of the selected group's flight along the nose (the original's own rule), projected each frame.
- `src/Flight/EdgeMarker.cs` — the off-screen edge marker's placement rules, engine-free: on-screen test, behind-mirror, edge clamp (`Resolve`) and the clock-hour bearing (`ClockHour`); MarkerHud, VersusHud and TargetHud all place through it.
- `src/Flight/MarkerDraw.cs` — the world marker's drawing primitives, engine-side but camera-free: reticle, edge arrow, centred text block and its clamped variant, plus the marker blue and the drop shadow. `EdgeMarker` places a marker; this draws it.
- `src/Flight/MarkerHud.cs` — the stunt objective marker HUD: reticle, screen-edge arrow + o'clock bearing, run status, banners; one per player.
- `src/Flight/StuntScoreboard.cs` — end-of-run results overlay: a Godot-UI panel of per-zone splits, total, and the persisted best time.
- `src/Flight/StuntRace.cs` — splitscreen stunt race bookkeeping: one `Racer` per player, finish placings, standings, rematch reset.
- `src/Flight/StuntRaceBoard.cs` — the race's shared ranked results overlay, on its own full-window CanvasLayer above the splitscreen panes.
- `src/Flight/ScoreStore.cs` — stunt best-time persistence: `user://stunt_scores.json` keyed chapter/mission/plane, faster runs only.
- `src/Flight/CustomPlaneDef.cs` — a custom-built plane as a pure model: the decoded 204-byte record's chosen fields (airframe, engine, armour x4, guns x4 with twin bits, hardpoint counts x2, paint pattern/picks/colours, name), none of its derived fields; engine-free. `Ammo`/`Ordnance` carry the campaign loadout EXPORT writes, in `OwnedPlane`'s own encoding with `NoAmmoPick`/`NoOrdnancePick` for a slot nobody fitted, and `HasLoadout` says whether any of it was; `SetLoadout` is the only writer and touches nothing else, since export must not rewrite the build. `Clamp` leaves those two alone: their vocabulary is `CampaignLoadout`'s, which is the one decoder of both.
- `src/Flight/CustomPlaneStore.cs` — one JSON file per custom plane under `user://Planes/` (versioned schema, name = identity, same name overwrites); list/load/save/delete over a plain absolute directory so it unit-tests, `UserPlanes()` resolves the `user://` scheme. `Delete(name)` sanitises the name exactly as `Save` does and treats a missing file as a no-op. Version 2 stores paint as the original's index pairs plus three decals; a version-1 file still loads, its "picks" read as the decals they were and each RGB triple mapped to the nearest authored swatch, and upgrades on its next save. The `loadout` block (the exported ammunition and ordnance) is optional and written only for a plane that carries one, so a hangar-built plane's file is the one earlier builds wrote; ⚠ it deliberately did not raise the version, because a reader without it skips an unknown property and a reader with it defaults the fields, and a bump would only have made older builds refuse a plane they can read.
- `src/Flight/CustomPlaneRecord.cs` — import-only reader for the original's 204-byte saved-plane files (`docs/formats/paint.md` "Saved custom planes"): one record or a whole install `Planes\` directory to `CustomPlaneDef`s; every paint field read as the indices it is (colour, shade, decal), the +0x68 RGBA left out as the engine's own cache and exposed by `StoredColour` for the cross-check, derived fields ignored, an unreadable file reads as null.
- `src/Flight/CustomPlaneBuild.cs` — the join from a saved `CustomPlaneDef` to what a spawn consumes: a `LoadoutDef` over the airframe's stock fit (calibre + twin onto slot markers, hardpoint counts onto the two wings' pylons), a `PaintScheme` from the record's pattern and colours, and a `PlaneDamage` ledger with the bought armour on the four zones; engine and weight deliberately reach nothing.
- `src/Flight/HangarEconomy.cs` — the hangar's decoded economy over a `CustomPlaneDef`: the airframe/gun/engine tables as data, per-line costs and weights, the two totals, the capacity/engine purchase verdict, and the display-only star ratings; pure, provenance in `docs/org/hangar.md`.
- `src/Flight/VersusMatch.cs` — Dogfight deathmatch bookkeeping: per-player kills/deaths, the host-fed match clock, threshold/time-out completion, ranked standings.
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
- `src/Flight/SpectatorCamera.cs` — the `--freecam`/`--anim-lab` observation camera: RMB-look + WASD/QE, no roll; `Frame`/`FollowNode` track an object, `F`/pad `X` re-locks onto one.
- `src/Flight/OrbitLock.cs` — the re-lock rule behind that key: nearest first, then outward, engine-free.
- `src/Flight/FlightModel.cs` — the arcade velocity-vector flight physics: thrust/drag/gravity/lift, stall, calibrated control rates.
- `src/Flight/PathFollower.cs` — the second movement law: a placed vehicle driven along an authored waypoint path instead of through the flight model, handing itself back at the final leg's 300 m point.
- `src/Flight/PropAnimator.cs` — spins the collected prop/rotor discs about their local axes, throttle-scaled (idle floor 0.4); `--fly` only.
- `src/Flight/ThrottleSlamSmoke.cs` — a large throttle jump streams dark exhaust trail smoke for a few seconds; a single notch or a decrease shows nothing.
- `src/Flight/FuelTank.cs` — the flown tank: burns with the lever, and a dry one freezes the throttle lever where it stands. Engine-free.
- `src/Flight/SpeedCue.cs` — chapter-authored pale smoke wisps emitted 60 m ahead of each player, density selected by camera altitude.
- `src/Flight/ControlSurfaceMix.cs` — the decoded control-surface angle solver: three stick channels into six clamped slots, smoothed at 2/s, with the human-pilot guard. No scene node.
- `src/Flight/ControlSurfaceAnimator.cs` — poses ailerons/elevators/rudders from those slot angles; `--fly` only.
- `src/Flight/WingLightBlinker.cs` — blinks the wingtip flares 0.08 s every 1.5 s, reset off on respawn; `--fly` only.
- `src/Flight/PylonOrdnance.cs` — the rockets under the wings: one FLYOUT-model body per loaded pylon, hidden as its ammo depletes; `--fly` only.
- `src/Flight/PlaneShake.cs` — the plane-wobble oscillators (gunfire buzz, overspeed rattle, being-hit rocks, the nitro engage) summed to visual-only roll on the rig's ShakePivot.
- `src/Flight/NitroSystem.cs` — the nitro boost lifecycle: the decoded tank, one-shot engage, cutoff, gates and animation edges, engine-free.
- `src/Flight/PlaneCollider.cs` — derives up to 8 plane-frame convex collision hulls from the built model's triangles, with no per-plane data.
- `src/Flight/ConvexHull.cs` — an engine-free convex hull over a point cloud: vertices, outward faces, thickness padding, and the point-distance query the fuse and blast passes ask.
- `src/Flight/CollisionLayers.cs` — the named physics layers (world / aircraft): the one place a layer bit is assigned a meaning.
- `src/Flight/AircraftBody.cs` — the flying plane's physics body: the shared `PlaneCollider` hulls on the aircraft layer; struck shape → part name.
- `src/Flight/IWorldQuery.cs` — the one seam onto the live physics world: a shape swept along a motion, a ray, and a standing overlap test.
- `src/Flight/GodotWorldQuery.cs` — the only adapter over `DirectSpaceState`; implements `IWorldQuery`.
- `src/Flight/ContactReport.cs` — one detected contact as a value: impact, normal, struck part, collider name, stop fraction, and whether an aeroplane was struck.
- `src/Flight/ContactOutcome.cs` — what a contact costs the striker: fate, the decoded damage pair, doom, the charged zone, the HUD flash, the push-out, and the struck-aircraft instruction.
- `src/Flight/AircraftContactResolver.cs` — the decoded contact rules for one aircraft: the damage pair, the fate, and the un-embed loop, with no `Node` in sight.
- `src/Flight/SweepCadence.cs` — the original's alternate-step collision sweep: which sim steps sweep, and the skipped step's motion carried into the next one.
- `src/Flight/AircraftLifecycle.cs` — the states one aircraft moves between (in play, crashed, destroyed, inert) with the spawn timers, holding a `SurfaceDefTable` for the crash-def selection; every transition returns what the node must perform.
- `src/Flight/PlaneDamage.cs` — per-part HP model from vehicle.json `destroyable_parts`; maps struck box + impact point to a data part; owns the whole-vehicle kill rule (`IsDestroyed`).
- `src/Flight/DamageVisuals.cs` — flips the torn-skin `pdpN` panels (paired by mesh position) at the data's injure thresholds, plus fire trails.
- `src/Flight/DamageLab.cs` — the `--damage`/F5 slider UI: one HP slider per part, driving the parked plane's DamageVisuals or the flown plane's real PlaneDamage.
- `src/Flight/CompassTape.cs` — the top-centre heading tape from the game's own HUD textures, drawn as a cylindrical drum seen edge-on.
- `src/Flight/GaugeCluster.cs` — the cockpit dials as HUD (altimeter/speedo/damage + gun/missile), geometry from the plane's `gauges` subtree.
- `src/Flight/FlightController.cs` — the flying-aircraft node: input → FlightModel → transform, chase camera, HUD, collision/crash, respawn; `FireControl`'s engine adapter.
- `src/Flight/FlightHud.cs` — everything one pane draws for its pilot, fed one per-frame state struct; the controller's seven HUD collaborators live here.
- `src/Flight/FlightControllerBuild.cs` — FlightRoster's internal, write-once construction handoff for a controller before tree attachment.
- `src/Flight/IFlightInputSource.cs` — the seam a sim step reads this frame's pilot intent through; `Bind` resolves one of its three adapters once per aircraft.
- `src/Flight/PlayerRig.cs` — one rendered view's state: camera, SubViewport, HUD parent, visual layer, controller, own sky/deck/puffs.
- `src/Flight/ViewerSet.cs` — session-owned "every pane's camera" registry: `GameSession` binds it once after the rigs are built; `ProjectilePool.Viewers` is its first consumer.

### `src/Effects/` — particle systems

- `src/Effects/Puffer.cs` — data-driven `PUFFER_STATE` billboard-particle emitter: burst, distance-trail, or sustained at-node modes.
- `src/Effects/EmitterRenderer.cs` — the `IEmitterRenderer` seam under `Puffer` and the `MultiMesh` billboard-shader renderer behind it.
- `src/Effects/FogVolumeClutter.cs` — the authored ambient cloud field: `fogvol.zrd` clutter scattered through its `fvol*` volumes, one MultiMesh per kind.
- `src/Effects/Precipitation.cs` — weather.json rain/snow: one camera-following MultiMesh of flakes or streaks, self-animating on the GPU.
- `src/Effects/WorldWind.cs` — the mission's global wind (static vector plus random-walk gust) and `EffectAmbience`, the seam a `Puffer` reads it through.

### `src/UI/` — screens, overlays and the inspection labs

The launchscreen and splitscreen rig, plus the interactive debug labs. Every lab has a scripted
`--debug-*` twin so a finding can be reproduced headlessly — see `docs/cli.md`.

- `src/UI/MenuInput.cs` — one player's menu input source: keyboard flag + a `Pads` binding, edge/auto-repeat `Poll(dt)`, plus the typed characters and pad-only cursor axes a text field needs.
- `src/UI/Menu/PresentationId.cs` — the identity a menu presentation registers under and Options persist: a non-empty ordinal token; `built-in` and `original` are the shipped ones.
- `src/UI/Menu/IMenuPresentation.cs` — one menu presentation's lifecycle: activate at a mapped return destination, tick over the host's seats, deactivate and be discarded.
- `src/UI/Menu/PresentationRegistry.cs` — presentation registration: one factory per id, a fresh instance per activation, an unknown id answered by fallback rather than a throw.
- `src/UI/Menu/IMenuHost.cs` — what the menu host lends the active presentation: shared features, shared audio, per-seat input sources, and the one typed exit.
- `src/UI/Menu/IMenuFeature.cs` — the shared-feature contract: typed state and semantic operations per feature; `Discard()` drops transient setup on a presentation switch.
- `src/UI/Menu/MenuFeatureSet.cs` — the host-owned feature registry, fetched by concrete type; `DiscardTransient()` is everything a presentation switch discards.
- `src/UI/Menu/MenuCommands.cs` — per-seat semantic menu commands plus `IMenuInputSource`, the device-neutral seam keyboard, mouse and pad feed and a later HOTAS/HOSAS source plugs into.
- `src/UI/Menu/IMenuAudio.cs` — the shared menu audio contract: presentations request cues and narration; the service owns lookup, playback, volume and session handoff.
- `src/UI/Menu/MenuExit.cs` — the one typed menu exit `Launcher` consumes: `LaunchExit`, `CampaignMissionExit`, `QuitExit`, `OptionsApplyExit`; presentations never build sessions.
- `src/UI/Menu/MenuReturnDestination.cs` — semantic return destinations (top level, cabin, debrief) that each presentation maps into its own screen graph.
- `src/UI/Menu/MenuLayout.cs` — the runtime reader of `extracted/rof/menu_layout.json`: screens, widgets with typed field access through the artifact's own kind table, navigation edges, script-named assets; engine-free.
- `src/UI/Menu/ControlsFeature.cs` — the shared rebinding screen: one seat's keymaps, the row and slot cursors, the capture in progress, and the steal it names before performing; engine-free.
- `src/UI/Menu/PlayerSetupFeature.cs` — the shared player setup: seats claimed by input-source identity, the aircraft roster (`MenuAircraft`), each seat's cursor, two-stage pick and fit, the launch gate per mode, the `MenuSeatChoice` list; device-neutral and engine-free.
- `src/UI/Menu/HangarFeature.cs` — the shared hangar: one scratch build over a `CustomPlaneStore` and an optional `IHangarWallet`, its three starts, the airframe pick with the defaults ask, the per-tab operations, the purchase gate in the original's words, the commit, the sale or deletion, the name rules and the discard; engine-free, walked by Built-in's `HangarFlow` and Original's hub alike.
- `src/UI/Menu/MenuIdleSource.cs` — a seat's input source with no device behind it, "no device", idle every frame; what the screenshot aid seats extra players over.
- `src/UI/MenuSeatDevices.cs` — the pad side of the shared player setup for any presentation: seat 0's claimed pad, the join gesture per pad, hotplug, and the flight binding a seat's source carries.
- `src/UI/Menu/Original/OriginalShell.cs` — the Original presentation's screen graph, engine-free: the decoded top level plus the Free Flight, Dogfight and hangar doors, the two sortie screens over the shared player setup, the Options screen over the decoded Preferences chrome, the decoded Game Options, Instant Action, campaign and hangar screens (their own partials), the messagebox dialog over any screen, pointer hit-testing, column focus, cues, exits and the composed board.
- `src/UI/Menu/Original/OriginalGameOptions.cs` — the shell's Game Options page (a `partial`) over the decoded `[@GameOptions@]` section: the shared options as a table of rows placed at the authored row pitch, a dropdown over the presentations and a checkbox over the graphics mode, ACCEPT CHANGES as the options apply and CANCEL CHANGES back to Preferences.
- `src/UI/Menu/Original/OriginalSeats.cs` — the shell's sortie screens (a `partial`): the aircraft window over the shared roster, every seat's cursor tagged on it, the seat strip, the hint, FLY as seat 0's confirmation and the launch, later seats' own frames.
- `src/UI/Menu/Original/OriginalHangar.cs` — the shell's hangar (a `partial`) over the shared hangar feature: the decoded PLANE NAME screen, the Plane Construction hub with its six tab sections as siblings, the construction totals page and the INVENTORY, each composed from its layout section, with the defaults ask as a dialog and every dropdown's list under its box.
- `src/UI/Menu/Original/OriginalPresentation.cs` — the Original presentation node: the shell drawn through `ComposedBoardView` on the board layer, every seat polled, the pointer mapped through `BoardFit`, pad joins scanned on the sortie screens, seat 0's text capture following the shell, the OS pointer hidden while shown.
- `src/UI/Menu/Original/OriginalAvailability.cs` — Original's availability answer before entry: the stamp is not behind, the layout reads with a `[MainMenu]` in it, the asset manifest's required files are all there; a reason, or the loaded layout.
- `src/UI/Menu/Original/OriginalAssetManifest.cs` — the versioned required/optional file manifest derived from the decoded layout, and the structural check over a data root that answers with every fault at once.
- `src/UI/Menu/Original/OriginalRosters.cs` — the sortie screens' chapter labels over the shared roster, and the eleven stock airframes resolved through the Instant Action decode, the stock half of the shared aircraft roster.
- `src/UI/Menu/Original/OriginalCues.cs` — the two cue names Original asks for: a button rollover and a button click.
- `src/UI/Menu/Original/PointerSeat.cs` — seat 0 with the mouse as its `MenuPointer`, the click a press edge; device reads injected, so engine-free.
- `src/Session/MenuCueTable.cs` — the menu cue table: cue name to wav under the rof tree's `ASSETS/SOUNDS`, the four the globals script binds.
- `src/UI/BoardMenu.cs` — a board's cursor and item list, engine-free, so the selection rules test off engine.
- `src/UI/BoardMenuItem.cs` — the rows a board menu can offer: Resume, Restart, Exit.
- `src/UI/BoardMenuView.cs` — draws a board menu's rows in the launchscreen's cursor idiom, inside the board style.
- `src/UI/BoardMenuHost.cs` — menu, rows and reader kept together, so a board wires one in two lines.
- `src/UI/SplitScreen.cs` — the splitscreen rig: one SubViewport pane per player (2–4), shared `World3D`, per-player visual-layer band.
- `src/UI/LaunchMenu.cs` — the in-game launchscreen: Mode → Chapter → Plane, the seats, joins and picks offered over `src/UI/Menu/PlayerSetupFeature.cs`, then the typed launch exit (Free Flight through `src/UI/Menu/FreeFlightFeature.cs`, the first shared feature); also the hangar's two doors, the campaign's one (`OpenCampaignCabin`) and its mission-end debrief (`OpenCampaignScrapbook`, both re-reading the profile from the store), the Options door (the menu presentation chooser) and the renderer both flows draw through.
- `src/UI/MenuZones.cs` — how the launchscreen divides a window: a fixed header band, a fixed footer band, the selection list in what is left, and the one scale all three share. Engine-free.
- `src/UI/InstantActionPresets.cs` — the Table of Contents: the 19 decoded preset scenarios by name, resolved to the setup screens' own cursor positions.
- `src/UI/PlanePickerRoster.cs` — the one roster every human plane picker draws: 11 stock airframes then the store's saved customs, each custom carrying its store name and its airframe's stock node (D32's launch seam); engine-free build/lookup rules.
- `src/UI/PlaneDiagrams.cs` — the two plane-diagram sheets the original draws beside a fitted aircraft, framed per airframe: `OL_PLANEDIAGRAMSTOP.PNG` (204x1870) and `OL_PLANEDIAGRAMSFRONT.PNG` (245x1100), each eleven equal frames stacked top to bottom in airframe-id order. Shared by ammo selection, the campaign's flight check and the hangar's airframe list; the decode is cached for the process, misses included, and a sheet whose height is not a whole multiple of the airframe count draws nothing rather than a mis-sliced picture.
- `src/UI/HangarFlow.cs` — the Build Custom Plane flow, Built-in's walk of the shared `HangarFeature`, engine-free: the original's nine screens in order over the feature's scratch plane, back/next navigation, the `IHangarPage` mount point the pages fill (rows, detail, stepper, optional page `HangarArt` and a per-row one), the plane-selection screen's two-stage delete, and the commit and every rule delegated to the feature.
- `src/UI/HangarAirframePage.cs` — the AIRFRAME screen: all 11 airframes as rows, focus previewing one and confirm picking it (raising the string-206 defaults ask as an inline two-row confirm), the stat table's figures and the economy's star ratings per row, the focused airframe's blueprint TGA as page art with its `PlaneDiagrams` plan view under it as row art; nothing is ticked until a pick is made and the ←→ stepper is inert.
- `src/UI/HangarEnginePage.cs` — the ENGINE screen: the airframe's six engines (langui 3100+af*6+id) plus the None row (1165, the decoded dropdown's own last row; 1171 stays the purchase wording), the pick ticked and opened on, confirm writing the scratch engine and the stepper inert, each row's decoded cost and weight via `HangarEconomy.EngineLine`.
- `src/UI/HangarArmourPage.cs` — the ARMOR screen: the four zones through their own langui formats (1191-1194) on the record's own units x5 display scale (0 to 60 in fives, the original's 13-row dropdown), the detail naming the pick as that dropdown does (1165 "None" on zero, else 1170 of units x5) beside the x4 priced cost and weight.
- `src/UI/HangarGunsPage.cs` — the GUNS screen: always four slots titled from the stat table's slot-title strings, each stepping the original's 11-entry dropdown (five calibres single, five twinned via format 506, No Gun 3315), the detail pricing the slot's wing or turret column (doubled for twin) with the calibre's magazine rounds.
- `src/UI/HangarHardpointsPage.cs` — the HARDPOINTS screen: the two per-wing counts through langui 1176/1177, the stepper walking 0-4, the detail speaking the dropdown's 1165/1168/1169 vocabulary with the decoded $410 / 480 lb per hardpoint and the wing's line total.
- `src/UI/HangarPaintPage.cs` — the PAINT screen on the original's own model: the pattern row stepping only the patterns this airframe's availability mask allows (labels langui 3425+index) and loading that entry's six colour/shade defaults, a colour row and a shade row per slot over the 27-row swatch table and its ramps, three decal rows each showing the chosen decal's own tile out of the shipped `PX_P_DECALS.TGA` sheet, and a live preview composed from the pair's own paint-screen artwork (`PaintIcons`, `PX_ICON_<airframe>_<pattern>_0..3.TGA`: the detail plate plus three region masks in their alphas, alpha-over in slot order then the plate on top). That is a different mask set from the `.BM` skins `PlanePainter` paints the flying aircraft with, and a different formula: no shading multiply and no weight normalisation, so a fully-masked texel IS its resolved colour.
- `src/Flight/HangarPaintTables.cs` — the paint screen's decoded tables as CSVM data: the swatch table (`data/hangar_swatches.json`, 27 rows of base colour, default variant and shade ramp) and the pattern table plus the 50 decal names (`data/hangar_patterns.json`). `Resolve(colour, shade)` is the original's own resolver; `Available(pattern, airframe)` is the availability mask; `Nearest(rgb)` maps a version-1 store file's free triple onto an authored swatch. Engine-free and pure, so the whole colour model resolves without a session.
- `src/UI/HangarNamePage.cs` — the PLANENAME screen: one row per character stepped through a filename-safe alphabet plus a length row that adds and removes them, capped at the original's 32-character name, with the detail line assembling the name and marking the focused character.
- `src/UI/HangarPurchasePage.cs` — the PURCHASE screen: the itemised review, one row per priced thing the scratch plane carries (airframe always, engine when chosen, armed gun slots, armoured zones via 1191-1194, wings with hardpoints via 1176/1177) with its decoded cost and weight, a totals row, and the Purchase Now row that commits, flagged with the problems text (1182 + 1227 / 1171) whenever the verdict is not Ok.
- `src/UI/CampaignFlow.cs` — the campaign's out-of-mission flow, engine-free: a stack of screens over one selected `CampaignProfileDef`, the `ICampaignPage` mount point the later screens fill (rows, detail, footer, optional `HangarArt`, optional text field), a registry keyed by `CampaignScreen` (`Roster`, `Cabin`, `PreviousMissions`, `Briefing`, `FlightCheck`, `Ammo`, `PlaneSelection`, `Scrapbook`, `ScrapbookZoom`), and the navigation API (`GoTo`, `Back`, `SelectProfile`, `Cancel`) those pages steer with. `RaiseModal`/`Modal` is the dialog facility every screen shares, and it takes every press until it is answered. `ZoomTarget` names the mission/spread/item a `ScrapbookZoom` screen opens on, set by `SetScrapbookZoom` and re-read fresh rather than cached.
- `src/UI/CampaignRosterPage.cs` — the player-profile screen: the name field over the roster, CONTINUE creating or continuing a player and opening the cabin, a roster row selecting then continuing, DELETE PLAYER as a confirmed second stage, CANCEL back to the launchscreen, and the original's own name refusals (langui 200/202/212/707).
- `src/UI/CampaignTextEntry.cs` — a campaign screen's one-line text field: the original's alphanumeric-and-space rule and 32-character cap, typed from the keyboard and stepped from the pad through one alphabet, so the field needs no keyboard and produces nothing the profile store would have to sanitise.
- `src/UI/CampaignCabinPage.cs` — the cabin hub: NEXT MISSION (opens the briefing for the profile's next mission, or refuses in the campaign's own words once all 24 are complete), PREVIOUS MISSIONS, PLANE CONSTRUCTION (a `CampaignExit.OpenHangar` request the shell fulfils), and RETURN TO MAIN MENU; `Pictures` layers the pilot's own aircraft photo (`PC_P_HANGAR<airframe>.JPG`) under the painted cabin, whose colour-keyed hole is the window, and the board draws it, the three panes at `[@PassengerCabin@]`'s `PC_PLANE`, `PC_BACKGROUND` and `PC_FRAME` rows through `CampaignLayout` and the memento window chosen rather than read; `Art` is the flat cabin scene alone, for a caller that wants pixels. `MapPinCount` is a pure, tested stand-in for pins nothing places yet.
- `src/UI/CampaignPreviousMissionsPage.cs` — the scrapbook's table of contents, one 80-pixel row per completed mission in `seq` order at `SBTOC_L_TOCList`'s own geometry (its box, row height, window and three scroll bitmaps read once through `CampaignLayout` when the page is built, the shipped values as the fallback; the two headers at `SBTOC_T_CHARACTER`/`SBTOC_T_MISSIONS` with their own justification): an `FC_PlaneIcons.png` silhouette beside the mission's short name, its area and the plane flown, all three off the best-of record. Past the listbox's four visible rows the window scrolls to keep the cursor's row on screen and the layout's own scrollbar draws beside it. The picked row washes and outlines in the list sub-script's own colours, the focused row in a fainter pair, both as `Fills`. A confirm on a row picks it and a second confirm on the picked row is REPLAY MISSION's own press, the original's double-click folded onto one pad button. Then VIEW SELECTED (`uiData` 2405 mode 1: opens the book at the picked mission's first spread), REPLAY MISSION (`SetMission` + `GoTo(Briefing)`, no advance, offered only where `uiData` 2411 offers it), the CURRENT MISSION bookmark (the book at the campaign's own next mission) and RETURN TO CABIN. The same file carries `CampaignScrapbookResults` (static): the book's results page (spread 1) computed from one `MissionResult` — the outcome line, the four drawn rows (Run Time, Gun Hit Ratio, Cash Earned, Overall Planes Downed; Rockets Expended is authored and never drawn) and the two tab titles, each at its own `SB_T_*` row through the `CampaignLayout` a caller hands in. Row labels are literal strings, `IaWrapupBoard`'s own precedent. Reproduces the original's Best to Date bug: `0x0040a7e6` reads a never-written offset for that tab instead of the merged mask, so it always renders Mission Failed. `Stamps`/`StampPictures`/`StampLabels` fill the eleven `SB_KILL`/`SB_KILLTEXT` slots densely from the plain kill tally then the ace tally, skipping zeros, at the `SB_killMARKERcombined.png` strip frame each names (0-10 plain, 11-21 starred), each slot at its `SB_KILL<n>`/`SB_KILLTEXT<n>` row; the eleven slot positions are not in reading order. `NotYetFlown` is the results card's own placeholder (langui 1219) for a mission with no recorded attempt, at the outcome line's own position. Wired into `CampaignFlow` as `CampaignScreen.Scrapbook` by `CampaignScrapbookPage.cs`.
- `src/UI/CampaignScrapbookPage.cs` — the scrapbook itself, opened on a browsed
  `(mission, spread)` position that defaults to `CampaignFlow.MissionSeq`'s own spread 1 and resets
  there whenever `MissionSeq` or `CampaignFlow.ScrapbookEntry` changes underneath a reused page
  instance, so reopening the book on the mission it is already browsing still lands on spread 1.
  Every spread carries `SB_T_NAMEANDAREA`, langui 1215 over the player's name and the mission's
  short name, at that row's position through `Flow.Layout`. Spread 1 adds the `SB_STATCARD` chrome
  (its row's position and art), the Best to Date / Most Recent tabs and
  `CampaignScrapbookResults`' rows and kill stamps off whichever half the tabs select, reset to
  Most Recent on every entry; a spread-1 view of a mission with no recorded attempt shows
  `CampaignScrapbookResults.NotYetFlown` (langui 1219) instead of a block. ⚠ The two tabs are a z
  sandwich around the card, which is the whole of the selection cue, so only the selected one is a
  plaque: the other is drawn as a picture under the card with its own label line over it
  (`CampaignBoards.SlotOf`). Every spread draws its own shipped scraps under that
  (`ScrapbookComposition.Pictures`: "the results page is a story page with the card laid over its
  right half"), gated on the mission's merged best-to-date mask (0 for an unattempted one). The
  page/mission arrows (`sb_b_prev`/`sb_b_next`) step by probing `SCRAPBOOK.CSV` for the neighbouring
  spread's item 1, exactly as `FUN_00406170` does, rather than storing a page count. The forward
  arrow appears only where there is a spread to turn to; the back arrow is always offered and, at
  the front of the book, opens the mission overview instead, which is where the original's own
  `sb_b_prev` falls. VIEW ALL MISSIONS (`SB_B_TOC`) goes there directly. The Current Mission
  bookmark (`sb_b_current`, langui 1200) appears only while the browsed mission differs from
  `MissionSeq` and jumps back to its spread 1. REPLAY MISSION and RETURN TO CABIN act on whichever
  mission is browsed (`uiData` 2405's own "the mission the open page shows" read), not necessarily
  `MissionSeq`: REPLAY MISSION calls `SetMission` on the browsed mission before opening the
  briefing, and is offered only where `uiData` 2411 offers it, on a results page whose mission's
  record holds a time in either half (a lost attempt counts, a completion bit is not the gate).
  Every scrap that opens (`ScrapbookComposition.Openable`) gets its own row after the
  fixed rows, drawing no `RowText` of its own (its picture already stands at its authored position)
  but naming itself on the hint line (title, else caption, else body, else the image name);
  confirming one opens `CampaignScreen.ScrapbookZoom` via `CampaignFlow.SetScrapbookZoom`.
  `CampaignFlow.CapturePath` (D21, the danger-zone scrap slot) resolves a `Snap_`-prefixed row's
  file against `CampaignFlow.Store.DirFor(profile.Name)`, the profile directory `BL-256`'s
  still-unbuilt capture writer would save into; `ScrapbookComposition.Pictures`/`Openable` skip the
  row when it is absent and draw it from that path, nudged and grimed, the moment a file exists.
  `ScrapOf(row)` hands a pointer-driven presentation the scrap a row stands for, whose
  `ScrapbookScrap.Region` (the CSV's quoted `Left,Top,Right,Bottom` column) is the rectangle it
  hit-tests.
  The mount's own `Objective` gate is authored
  independently of its paired capture's (`SCRAPBOOK.CSV` rows `1_2_4`/`1_2_6`: mount objective 1,
  capture objective 18), so the two can show and hide on different mission progress, which this
  page reproduces by treating every row's gate as its own rather than inferring a pairing.
- `src/UI/CampaignScrapbookZoomPage.cs` — one scrap's detail view, opened on
  `CampaignFlow.ZoomTarget` and closing back to `Scrapbook` (CLOSE, or the default `Back()`): the
  zoom family's background (`SB_BG_<letter>.jpg`), the scrap's inset image at its own
  `ZoomX`/`ZoomY`, and up to three text lines at the family's own boxes
  (`CampaignLayout.ZoomFamily` off the decoded `SBZ_T_*` rows, else
  `ScrapbookComposition.ZoomFamily`'s own read of `LAYOUT.CSV`); the grime frame is `SBZ_GRIME`'s
  row and the capture sits inset from it. The title/caption/body are the shipped row's own langui
  *symbols* (`IDS_SB_...`): `RESRC1.H` assigns them numeric ids under `ScrapBook.Rc`, a resource
  script never extracted, so nothing resolves them to real text. Shown as the raw symbol, the same
  degrade `CampaignBriefingPage`/`BriefingObjectives` use for an unresolved key, rather than
  invented English. A target with no such item draws nothing rather than throwing. A capture takes
  the `SBZ_GRIME` torn frame instead of its own `ZoomX`/`ZoomY`, sitting at that frame's position
  plus ten and eight the way the script places it. EXPORT TO DESKTOP (`SBZ_B_EXPORT`) is offered
  only where a source file resolves, which is the original deactivating it for a zoom with no inset
  image; it copies through `ScrapbookExport` and reports langui 705 or 706 on the flow's message
  line, where the original opened a message box.
- `src/UI/ScrapbookExport.cs` — EXPORT TO DESKTOP's copy (`uiData` 2412, `FUN_00406870`): the
  scrap's own file to the desktop under its own base name, overwriting, returning whether it landed
  and either the name or the OS reason, which are langui 705's and 706's own arguments. Engine-free,
  and the folder is a parameter so a test writes somewhere other than a real desktop.
- `src/UI/ScrapbookComposition.cs` — the scrapbook's per-spread scrap layout, read from
  `extracted\rof\ASSETS\SCRAPBOOK.CSV` rather than invented: `Items` enumerates a mission slot's
  spread from item 1 upward and stops at the first missing key, the way the original's own reader
  does; `Pictures` gates each row's `Objective` against the mission's merged best-to-date mask
  (bit 0 "ever won", a positive value its own bit set, a negative value its own bit clear), skips a
  `Snap_`-prefixed capture when the caller-supplied resolver returns no path for it (`BL-256`, the
  capture writer, does not exist yet, so no such file exists on a real profile), and stacks the
  survivors by ascending `DrawOrder`. A capture draws from the resolver's own path rather than the
  asset library, offset the three and four pixels the original moves it, with a `SB_P_Grime` frame
  over it from `ScrapbookGrime` (`uiData` 2413: seeded `(mission << 8) | spread`, each of the ten
  frames handed out once before repeating, so a page's smudges are stable and its neighbours' differ). `Openable` narrows the same gate to
  `ScrapbookScrap.Opens`, the `Zoom` column alone (`!= '0'`) rather than `ImageType`'s second
  letter (`docs/formats/campaign-screens.md`, "Resolving a row to a file"). `ZoomFamily` reads
  `LAYOUT.CSV`'s `SBZ_T_TITLE`/`CAPTION`/`TEXT<letter>` rows for a family's three text boxes (X, Y,
  wrap width only; colour is unreadable by a `BoardLine` regardless, and two families' colour
  fields are typo'd). Parsed rows are cached per file path, misses included, `PlaneDiagrams`' own
  precedent. CSVM carries no unlock-flag analogue, so unlike the original the objective gate cannot
  be bypassed.
- `src/UI/CampaignBriefingPage.cs` — the mission briefing: everything resolved from `CampaignFlow.MissionSeq` alone, through `cm_sequence` to the storage address, `brief_c%d%d` to the dialog state, the state to its map bitmap and narration name, `sounds.zrd`'s `SETS` to the wav file, and the mission's own `objectives.zrd` to the note, so nothing is computed from the story position. REPLAY BRIEFING / RETURN TO CABIN / GO TO FLIGHT CHECK are the screen's only rows: an uncovered objective is written on the parchment through `Notes`, the `BoardNote` carrying the dialog's own `LIST` widget, so the mission's text is read and never a cursor stop (`BL-487`). The map is the page's `HangarArt`. Labels are `messages.json`'s own `MSG_BTN_*` and an unresolved objective key shows as the raw key, so a missing extraction degrades to the three buttons rather than throwing. It plays nothing: `NarrationWav` and `NarrationStarts` name what a shell must play, and `Advance(seconds)` is the clock a shell drives.
- `src/UI/CampaignCombo.cs` — a campaign screen's drop-down (`PS_D_PILOTPLANE`, `OL_D_AMMO0`): the authored rectangle, the row height and row count the list opens at, the window that scrolls when the cursor leaves it, and a closed field's horizontal step. ⚠ It never moves its own pick: `Confirm` and `Next` return a candidate and `Select` is the only mutation, because the screens that own one refuse some picks.
- `src/UI/CampaignModal.cs` — the dialog a screen raises over the composed board, the original's `messagebox.script`: a message, one button and the callback its answer runs. Held by `CampaignFlow`, not by a page, since two screens reach the same box. ⚠ It is not `CampaignFlow.Message`, the one-line refusal band a navigation clears; answering a refusal in both would say it twice.
- `src/UI/CampaignFlightCheckPage.cs` — the FLIGHT CHECK screen (`FLIGHTCHECK.SCRIPT`): the mission title, a PILOT block and, where `cm_sequence` sets the wingman flag, a WINGMAN block, each with its silhouette, its GUNS and ROCKETS tables and its CHANGE AMMO and CHANGE PLANE rows, then RETURN TO BRIEFING and FLY MISSION. The objectives note is a caption at `FC_T_OBJECTIVES`' own row, read once per mission rather than once per repaint; every fixed element on the screen (title, mission line, headings, tables, silhouettes) is its `FC_*` row through `CampaignLayout`, with the title's x, the paper plaques' y and the wingman tables' 17-pixel drop pinned to their measurements (`docs/org/campaign-board.md`). CHANGE AMMO names the slot and opens `CampaignScreen.Ammo`; CHANGE PLANE names the slot and opens `CampaignScreen.PlaneSelection`, the picker being the one place a plane changes and therefore the one place the duplicate rule is enforced. Its two gates are the script's own: barred on mission ordinals 13 and 17, and while the profile owns fewer than three planes; a guest's row is offered unconditionally, both gates being rules about the seated profile's own aircraft. The screen has no horizontal stepper at all: a guest's CHANGE PLANE takes the same door the seated player's does, so the duplicate rule is enforced in one place. Weapons resolve build-or-stock through `CampaignFlightField.IsStock`, never by looking a stock record up in `CustomPlaneStore` by name.
- `src/UI/CampaignAmmoPage.cs` — the AMMO SELECTION screen (`ORDINANCELAYOUT.SCRIPT`): four gun-group drop-downs (`OL_D_AMMO0..3`) and eight pylon drop-downs (`OL_D_ROCKETS0..7`) as `CampaignCombo` fields at their rows' boxes, item heights and windows, the calibre captions at `OL_T_GunName0..3`, the title and the two panel headings with their notes at their `OL_T_*` rows, the two aircraft diagrams at `OL_P_PLANETOPICON`/`OL_P_PLANEFRTICON`, all through `CampaignLayout` with the shipped values as fallback; the title's x is pinned at the measured 138 where the row says 132 centred, and the description column (`CampaignBoards.DetailSlot`) keeps its measured y 92 where `OL_S_AMMODESC` says 96. ACCEPT LOADOUT writes the picks through `CampaignFeature.CommitLoadout`; CANCEL writes nothing.
- `src/UI/CampaignPlaneSelectionPage.cs` — the PLANE SELECTION screen (`PLANESELECTION.SCRIPT`): a combo, a silhouette, four ratings and a gun and hardpoint list per active crew slot, EXPORT per slot, ACCEPT writing both picks and CANCEL restoring the pair the screen opened with. A pick the other active crew slot already flies is refused with langui 710 and the field left where it was, which is message 10015's own arm plus its `sender.QG` revert; the comparison is by plane name, `CampaignFlightField.KeyOf`'s rule for a profile aircraft, since two of them may share an airframe legally. The refusal answers a commit, a picked list row or a closed field's step, never movement inside an open list. EXPORT writes the slot's plane into `CustomPlaneStore` under its own name with the ammunition and ordnance the campaign fitted, and answers with langui 702 through `UiStrings.Format`; a record already on file keeps its paint, armour and engine, and a starter or granted aircraft with none gets one built from its award template or its airframe's stock weapons (`HangarFlow.LoadStockWeapons`), since a record with no guns would export an aircraft that flies unarmed. ⚠ A stock record is refused outright (`CampaignFlightField.IsStock`): it is named for its airframe, so the write would land on any hangar plane sharing that name. TOP SPEED and OFFENSE have no decoded formula and draw a stand-in (`BL-653`). A guest's check (`CampaignFlow.Field.Current` above zero) opens the same page over that guest's own `Choices` instead: one PILOT slot whatever the mission flies, no EXPORT row at all (a guest's record carries the owner's plane name, so the write would rewrite the owner's build), a refusal reading "Each player must fly a different plane." because langui 710 names a Pilot and a Wingman a guest's check has no concept of, and an ACCEPT that moves the guest's own pick through `CampaignFlightField.Choose` and writes nothing. ⚠ The guest refusal asks `CampaignFlightField.Taken` rather than carrying a second copy of the rule: only the field knows what the other humans took, and it is what compares a stock entry by airframe and a profile copy by name. Every fixed element (combos, silhouettes, title, mission line, headings, plane lines, ratings, weapon lists) is its `PS_*` row through `CampaignLayout`; the wingman's plane line keeps the pilot's row dropped by 218 where `PS_T_WINGPLANE` says 323, and the title stays left-justified where its row says centred.
- `src/UI/BriefingScript.cs` — the reveal script, engine-free: the `Briefing.zrd` reader (`BriefingDialog`/`BriefingState`/`BriefingStep`, walking the root list where the 24 states actually live) and `BriefingReveal`, the interpreter that runs a state's 12-opcode beat sheet against a caller-advanced clock, blocking on `Wait`'s authored seconds and `WaitForMarker`'s cue times and keeping each element's opacity, rotation and position as its tweens land. Elements come out in placement order, which is draw order. With no cue points every marker releases at once, so the map finishes under the narration rather than a timing being invented. Decode: `docs/formats/briefing.md`.
- `src/UI/BriefingObjectives.cs` — the briefing's parchment note from a mission's `objectives.zrd`: every `IDENTITY` carrying a `MSG_BRF_*` key, ordered by priority ascending, which is the list an `Objective id index` opcode indexes 0-based. Takes the reader list rather than a path, so it tests without an extraction; resolves text through `Messages`, leaving the raw key visible when the table cannot.
- `src/UI/ObjectivesHud.cs` — the campaign mission's objectives readout, drawn on the **pause screen** and nowhere else (`BL-466`): the original keeps its objectives on the pause screen's parchment and leaves the flight HUD to the gauges, so the whole layer is hidden until `PauseState.Paused`. Reads `CampaignDirector`'s `ObjectiveGraph.Rows` directly (not a re-parse), text through `Messages`, and shows every row rather than gating on the row's own `Awake` flag (an objective authored with no `BEGIN_DORMANT` starts awake without ever running a wake action, so its row's `Awake` flag never turns on even though it is live from the mission's first tick, C1/M02's own primary OBJECTIVE3, and filtering on it would hide exactly the objective a player needs to see first). This also matches the original's own decoded display mechanism (`docs/formats/objectives.md`, `FUN_004acc20`/`FUN_004ad240`): every `IDENTITY` row is built once and shown unconditionally, only the completion mark toggles. A row the mission gives **no message key** resolves to no text and is dropped from the drawing (`DrawnLines`), since drawn it would be a mark against blank space, and `BuildLines` still carries one line per graph row so a suite counts against the graph. The mark takes a column of its own, so a completed line's text starts where every other line's does. Self-mounting (its own `CanvasLayer`, on `HudLayers.Board` with the pause board and after it in tree order), so `GameSession` only hands each built instance the shared `CampaignDirector`, `Messages` and `PauseState` and adds it — unlike `PerfHud`'s one-for-the-window instance, a splitscreen campaign session builds ONE INSTANCE PER RIG (B15), each under that rig's own `HudParent`, so every pane draws and polls the shared `ObjectiveGraph` on its own. The reference frame (`Complete Mission M02.mkv` at t=12 s) fixes the top-right corner and nothing else, so the glyphs and metrics are TUNE.
- `src/UI/ScreenFlash.cs` — the full-screen wash, two channels per pane: the `FBFX_COLOR_FROM_TO` ramp routed by camera proximity, and the victim-routed blend wash, composited at paint time.
- `src/UI/BlendWash.cs` — one pane's victim-routed wash: the sonic/flash/smoke blend rule and attack/sustain/release envelope, plus the paint-time composite over the ramp.
- `src/UI/LiveryLab.cs` — the `--viewer` livery editor (L): squadron/colour/decal steppers, live `Repaint`, copy-CLI-args.
- `src/UI/MeshLab.cs` — the geometry/shading lab (M): normal lines, smoothing seams, cull/normal overrides; on the parked plane, or on the selection.
- `src/UI/ColliderOverlay.cs` — the collider wireframes (C): every built collision shape drawn, coloured by the surface id it resolves to; needs `--collision` outside flight.
- `src/UI/ClassOverlay.cs` — the colour-by-class overlay (X): every drawn mesh tinted destructible/facade/clutter/scenery, a findable-targets view.
- `src/UI/AiNetsOverlay.cs` — the AI patrol-net overlay (F13, `--debug-ainets`): the chapter's nets as coloured graphs with labels + census log.
- `src/UI/TileGridOverlay.cs` — the map-edge tile-grid overlay (`--debug-tilegrid`): every ground tile tinted by repetition band, so one colour band is one block.
- `src/UI/WeaponLab.cs` — the weapon lab panel (B): steppers that arm the held plane's live loadout, click-to-place on a real world surface. Fires nothing itself.
- `src/UI/PanelFocus.cs` — the one-line rule every flight-hosted panel applies: no widget takes keyboard focus, or a focused button eats the fire key.
- `src/UI/NodeLabels.cs` — floating `cs_name` labels over scene nodes (F16): Off/Meshes/All, anchored on mesh centres, de-cluttered.
- `src/UI/MarkerOverlay.cs` — the `--viewer` firepoint/pylon/target overlay (K, `--markers`): coloured gizmos + de-cluttered labels.
- `src/UI/PhotoModeHud.cs` — photo mode's fading hint line and its Escape/pad-B way out; raises an event, decides nothing.
- `src/UI/PerfHud.cs` — the frame-cost readout (F14, `--debug-fps=`): fps/current-frame-cost/worst-recent-frame, once for the window, drawn above the launchscreen too.
- `src/UI/TargetingOverlay.cs` — the targeting overlay (F15, `--debug-targets`): a line from every gunner to its acquired target, coloured by the gate holding the trigger.
- `src/UI/DebugKillTarget.cs` — the debug kill key (F17): kills P1's `TargetSelection.Current` through its own death path (`DebugForceCrash` for an aircraft, `AnimRuntime.DamageAt` for a zeppelin sub-part); inert on a turret, which has no `HEALTH` key at all.
- `src/UI/SelectionService.cs` — the shared `--freecam`/`--anim-lab` selection: click-pick + the `cs_name` ancestor ladder, breadcrumb + highlight box.
- `src/UI/NodeLab.cs` — the `--freecam`/`--anim-lab` node lab (N, `--debug-nodelab`): lazy `cs_name` tree, search, frame/hide, dependencies, destructibles.
- `src/UI/WorldDamageLab.cs` — the `--freecam`/`--anim-lab` world damage lab (F5, `--debug-damage`): HP slider + kill/reset on the selection's destructible pool.
- `src/UI/OrbitCamera.cs` — the `--viewer` orbit camera (orbit/zoom/framing), extracted from `GameSession` for `--anim-lab`.
- `src/UI/AnimLab.cs` — the `--anim-lab` debugger: quiet stage, fixed-dt clock, transport panel, def picker, timeline, freecam, follows the selection.
- `src/UI/AnimTimeline.cs` — the anim lab's per-sequence timeline: authored event blocks vs runtime-fired ticks (the scheduler-divergence instrument).

### `src/Utils/` — session-wide services

The things every subsystem depends on: the clock, the log, the seed. Changing one of these changes
determinism repo-wide; read `docs/verification.md` first.

- `src/Utils/Config.cs` — dev tuning-override: typed getters over an optional sparse `res://config.json`, else the caller's in-code `const`.
- `src/Utils/EffectPools.cs` — the `effect_pools.json` reader: how many copies of each effect-template root the world and crash stages build, scaled by player count.
- `src/Utils/EffectsLevel.cs` — the original's EffectsLevel option and the clutter fade's squared distance scale it drives, plus the remake's far-fade switch.
- `src/Utils/GameClock.cs` — the session sim clock every sim consumer takes dt from: run mode (realtime/fixed), halt and single-step, time scale, the holds.
- `src/Utils/GraphicsMode.cs` — the opt-in enhanced-lighting setting, resolved once at launch into the one boolean every scene builder reads.
- `src/Utils/HitchMonitor.cs` — the always-on frame-hitch detector: a frame far costlier than its recent neighbours gets a record describing it; it logs nothing.
- `src/Utils/HitchSidecar.cs` — the hitch detector's write path: queues a tripped record and drains it later to one `[perf] hitch` line plus one JSON sidecar line.
- `src/Utils/HoldToRepeat.cs` — tap-versus-hold timing for one button: an initial delay, then a repeat every interval until release.
- `src/Utils/Log.cs` — the diagnostic log: a fixed category vocabulary over four levels, a quiet filtered console and an always-complete `.scratch/logs/` file sink.
- `src/Utils/OptionsStore.cs` — version-tolerant JSON persistence of the process-wide options in `user://options.json`, written atomically.
- `src/Utils/PerfSample.cs` — ambient timed leaf scopes: `using (PerfSample.Scope(site))` accumulates per site per frame, and a hitch record carries the frame's named work.
- `src/Utils/PhysicsTickCost.cs` — the wall cost of one whole physics tick and the tick count a wall second got, measured by a bracket pair spanning the tick.
- `src/Utils/PresentationResolution.cs` — the requested-versus-active menu presentation resolver, availability checked separately from the saved request.
- `src/Utils/RenderPoses.cs` — the render half of the fixed-tick simulation: the pose a realtime session draws between two simulation steps.
- `src/Utils/Rng.cs` — the session's one master seed and the named subsystem generators every random draw derives from.
- `src/Utils/ScriptedWindow.cs` — Win32-only window hiding for scripted runs; `ScriptedWindow.Hide()` uses `ShowWindow(SW_HIDE)` on the native window.
- `src/Utils/ShaderTime.cs` — the `csky_time` global uniform: the clock's GPU twin, replacing `TIME` in every generated shader; wraps at 3600 s.
- `src/Utils/StartupProfile.cs` — the always-on `[perf] startup …` line: every session build split into the phases it spends its time in.
- `src/Utils/TapHoldButton.cs` — one button carrying two actions split by how long it is held; the caller feeds it the button level and switches on the answer.

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
- `src/Session/ExtractionStamp.cs` — boot-time check of `extracted/VERSION.json` (the provenance stamp the extraction scripts write): schema const + at most one warning line when the stamp is stale, missing, or unreadable, plus the `Behind` read a caller blocks on.
- `src/Session/LiveryResolver.cs` — each player's livery from a `SessionSpec`: the paint catalog, the pattern-mask library and the per-player scheme pick.
- `src/Session/SpawnPicker.cs` — each player's flight spawn: the shared spawn-list index and the per-player point; also the plain `IFlightStarts`.
- `src/Session/IFlightStarts.cs` — the spawn-placement seam: one call answering for the whole field, and the `FlightStart` pair every rig is placed from.
- `src/Session/StartGrid.cs` — the abreast starting grid: every pilot fanned about one anchor spawn, the whole field lifted as one to clear terrain.
- `src/Session/PlaneRoster.cs` — pure lookups over a `SessionSpec`'s plane roster: which plane a player flies, and its display name.
- `src/Session/EffectCatalogue.cs` — the record of which authored anims are playable effects, and what their defs need staged: the effect/crash/damage-shim name tables, the two surface-indexed def vectors (`CrashDefTable`/`TouchdownDefTable`), and the anchor-root derivation both binds stage from.
- `src/Session/SurfaceDefTable.cs` — one of the original's per-surface anim-def vectors and the cascade that indexes it with a struck material's surface id.
- `src/Utils/EffectPools.cs` — the `data/effect_pools.json` reader: how many copies of each effect template the stage builds, per ROOT, scaled by player count.
- `src/Session/WeatherRig.cs` — loads the mission's weather and drives the per-rig skydome, whiteout, deck and zone gate each frame.
- `src/Session/WorldEffectsFactory.cs` — builds the impact/destruction effect stages and the per-plane crash runtime.
- `src/Session/LensFlareRig.cs` — the sun's lens flare: screen-space sprites along the sun→centre line, occlusion-tested.
- `src/Session/FlightRoster.cs` — the session's aircraft set: builds the human field in player order and introduces AI aircraft later through one assembly seam.
- `src/Session/FlightRosterInputs.cs` — the roster's three grouped dependency contracts: aircraft resources, world bindings, and human-session bindings, plus the copied flight policy.
- `src/Session/HumanFlightAdapter.cs` — the FlightRoster's internal human-rig adapter: painted plane, `FlightController`, loadout/ordnance, HUD instruments, damage visuals, audio, stunt run, spawn, crash runtime.
- `src/Session/AiFlightAssembler.cs` — the FlightRoster's internal AI path: pilot preparation, model/controller/loadout, damage/crash runtime, and placement.
- `src/Session/InstantActionDirector.cs` — the engine side of one Instant Action mission: the actor phases, the sequencer tick and the end-condition wiring.
- `src/Session/InstantActionRuntime.cs` — one Instant Action mission's actor set and its end, engine-free: the ace, wingmen, wave draws and objective zeppelin.
- `src/Session/InstantActionWaves.cs` — the decoded wave sequencer, engine-free: the wave counter, the spawn draw against live humans, the fan geometry.
- `src/Session/SpectateHandoff.cs` — the shared pane handoff for a downed pilot whose teammates fly on: the wreck pinned, a spectator camera in the freed pane.
- `src/Session/ScriptedPathVehicles.cs` — one mission's scripted-path vehicles: the snap onto waypoint 0, the freeze, `START_TAXI`'s release, and the handoff back to the flight model.
- `src/Session/CampaignRoster.cs` — the engine-free plan of a campaign mission's `aiv` roster: each block's airframe, and its net or its netless escort.
- `src/Session/GeneratorCycle.cs` — the decoded egen launch timing law for one generator, pure and engine-free: composed periods, hold-not-cancel blocking, and the credit budget that starts at zero whatever the authored `capacity` says.
- `src/Session/NetTrailerTargets.cs` — resolves a patrol net's trailer name (`player`, a zeppelin, a train) to a live position, so an anchored net rides its target (`BL-377`).
- `src/Session/AiGeneratorRuntime.cs` — runs a mission's egen generators (`--generators`): load-time drop rules, per-cycle stepping, spawns through the handed roster callback — or, on an Instant Action zeppelin run (F12), releases an already-built wave member instead.
- `src/Session/AiVoiceRuntime.cs` — wires E16's dispatch into a session: the decoded event sources (hit-path DI, Downed death cries, acquisition call-outs, taunts) played through `CombatVoice` + `WorldSounds.PlayOneShot`.
- `src/Session/ZeppelinRuntime.cs` — runs a mission's zeppelins (M4 F17+F18+F19, `--zeppelins`): places each record's world node at its authored pose, flies it along its net through `ZeppelinMotion`, owns the multi-zone damage (per-part registry pools, the survivor-count kill, the authored hull death) and fires the broadside (`ZeppelinRuntime.Cannons.cs`: real unowned `wep_28` rounds through `ZeppelinBroadside`).
- `src/Session/TurretEmplacementRuntime.cs` — the world AA emplacements: the standalone `ai.zrd` family placed at its `NODES` patterns against the built chapter world, shipped `ACTIVATED` honoured, `SetActivatedUnder` the Instant Action builder's subtree activation (what arms the objective zeppelin's rings), `--wake-turrets` the `WAKEUP_TURRETS` stand-in.
- `src/Session/CampaignProfileStore.cs` — JSON persistence for one named campaign profile: funds, owned planes, mission records, awards and the destruction log.
- `src/Session/CampaignProgression.cs` — the rules that write a profile: an attempt's best-of merge, the monotonic position, the rewards and the skip offer.
- `src/Session/CampaignPersistLog.cs` — the cross-mission state log: what a mission left destroyed, carried silently into later missions of the same chapter.
- `src/Session/CampaignLoadout.cs` — the bridge between a profile's stored ammunition and ordnance picks and the `LoadoutChoice` a launch hands the session.
- `src/Session/ObjectiveScript.cs` — one mission's parsed `objectives.zrd`: the contiguous `OBJECTIVEn` blocks, in the typed shape the graph runs.
- `src/Session/ObjectiveGraph.cs` — the objectives runtime, engine-free: the four-state machine, the rotating completion scan, the conditions, the four endings.
- `src/Session/CampaignHumanField.cs` — the human field's rules, engine-free: what a condition naming one aeroplane asks once two to four humans fly.
- `src/Session/ObjectiveSites.cs` — the flown campaign mission's objective sites as targeting candidates, rebuilt from their live source every frame.
- `src/Session/CampaignDirector.cs` — the engine side of a campaign mission: the graph armed against the built world, the roster spawned, the attempt recorded.
- `src/Session/CampaignDangerZones.cs` — a campaign mission's own danger zones: the `dzpathN` gates its script arms, tracked per human by the stunt gate rule.
- `src/Session/AirframeSwap.cs` — the three `CALLBACK` codes that hand the player a different airframe in mid mission, and the def and node each names.
- `src/Session/CutsceneController.cs` — the host a cutscene definition raises its `CALLBACK` codes to: the letterbox bars and the cutscene camera the definition itself drives, the world/objectives hold, the player out of flight with the chrome off, the AI parked, then one hard cut back to gameplay on the definition's end or on a skip. It answers for the two story-mission intros always, and for whatever `HostDefinitions` registers (the landings trigger's own rows and their `CALL_ANIMATION` closure). Scoping is by definition name, never by authored code.
- `src/Session/LandingApproachRuntime.cs` — the mid-mission cutscene trigger: ticks a story mission's resolved `LandingApproaches` against **every flying human** (arming gate, speed band, attitude cone, condition volume) and starts the row as an explicit mission trigger, so its call closure can stage library-root actors and satisfy animation-state or node-state objectives; it hands the row's definition AND the human who flew it to `CutsceneController.Own` first, so the episode belongs to the row however deep the callee raising its first code sits, and to that human. The rig rides `_startingFor` rather than the `AnimRuntime.MissionTriggerOwner` seam's arguments, because that seam carries a definition name alone and every other path through it means the scripted player. The first human in player order wins a row two of them satisfy on the same tick. A row fires once per entry into its volume, latched **per row and not per row and human**: the handoff leaves the aircraft where the cutscene parked it, still inside the volume that started it, and a second human arriving is not a second entry. An `auto` row lights the prompt in the pane of each human inside its sphere (`OffersAutoLandTo`, which `GameSession` fans onto each rig's `FlightController.AutoLandOffered`; `AutoLandOffered` here is the any-human answer) rather than starting anything by itself; the auto-land button (`FlightController.AutoLandPressed`) starts the row's own animation for the human who pressed it, latched the same way the manual row is, and a human outside the sphere pressing it starts nothing. Story missions only, for the reason `WorldSession.Options.LandingTriggers` gives. Rows whose approach nodes are staged later are retained and bound when those nodes appear: CM02 grafts its three Balmoral cones with the roster, while CM07 summons its train-pickup cone, which the train's own `pickup_timing` opens and closes in step with the track loop. The trigger starts nothing off the pickup sensor; restarting the timing on entry re-phased the switch the passenger's wave-or-drop fork reads.
- `src/Session/LadderSwitch.cs` — the original's rope-ladder switch as an engine-free rule and state machine: the ladder is wanted when the aircraft's own up axis is within 45 degrees of world up and the player is inside an active `pickups.zrd` sensor, and the switch starts `drop_ladder` or `retract_ladder` to match, one transition at a time, holding a transient state until the definition's own `CALLBACK 123` settles it. A mission that authors neither definition flips the state silently, so no per-mission table exists. `Holder` is the co-op half, engine-free for the same reason the rest is: the incumbent human keeps the switch for as long as they qualify and only once they stop is the field asked, first qualifier in player order winning. ⚠ A single owner rather than "any human qualifies", because the transient states mean two humans drifting through one sensor under an "any" rule would thrash the drop against the retract; the incumbent is asked first precisely so a second qualifier changes nothing. Decode: `docs/org/ladderSwitch.md`.
- `src/Session/LadderSwitchRuntime.cs` — `LadderSwitch` flown against the built world: every frame outside a cutscene it reads each flying human's attitude and position against the mission's pickup sensors, resolves the one holder through `LadderSwitch.Holder`, and starts the ladder definitions as mission triggers, so the drop's `OBJECT_ADD_CHILD` can materialize the library rope ladder. A holder that has stopped qualifying with nobody to take over is what retracts the ladder, and the same pass finding a replacement is what stops that retraction ever starting. It takes the runtime's `CALLBACK` host slot and chains to the cutscene host behind it, which is where the original registers the switch on each definition. Bound with the landings trigger, story missions only.

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

