# Godot project — per-module implementation notes

**Start at the module index below.** One routing line per module, grouped by namespace. Find the
module there, then read only its entry: `Grep "## src/Mech3/SceneBuilder.cs" -A 12` returns the
whole thing, because the entry shape guarantees it. **Never read this file whole** — it is ~190 KB.

One `## src/...` entry per module in `CSVM/src`. **This file orients a reader and nothing else:**
what the module is for, what it owns, and which module to look at next. Entry shape: 1–2 sentences
of purpose beyond the index line, plus the pointers a reader needs. Body ≤ ~8 lines (~12 for the
heaviest modules). Entry order is historical, not grouped — the index is the map, grep is the lookup.

⚠ **Traps do not live here.** A constraint that would stop a wrong edit belongs in the code, on the
member it binds, under `PROJECT_CONTEXT.md`'s comment caps — that is where somebody about to make
the edit is actually looking. Format and decode knowledge belongs in `docs/formats/` and
`docs/org/`; a way a measurement misleads belongs in `docs/verification.md`. Narratives, diagnoses
and landed-work stories go in the commit message (pre-2026-08-06: `docs/HISTORY.md`).

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
- `src/Flight/CustomPlaneDef.cs` — a custom-built plane as a pure model: the decoded 204-byte record's chosen fields (airframe, engine, armour x4, guns x4 with twin bits, hardpoint counts x2, paint pattern/picks/colours, name), none of its derived fields; engine-free.
- `src/Flight/CustomPlaneStore.cs` — one JSON file per custom plane under `user://Planes/` (versioned schema, name = identity, same name overwrites); list/load/save/delete over a plain absolute directory so it unit-tests, `UserPlanes()` resolves the `user://` scheme. `Delete(name)` sanitises the name exactly as `Save` does and treats a missing file as a no-op. Version 2 stores paint as the original's index pairs plus three decals; a version-1 file still loads, its "picks" read as the decals they were and each RGB triple mapped to the nearest authored swatch, and upgrades on its next save.
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
- `src/Effects/EmitterRenderer.cs` — the `IEmitterRenderer` seam under `Puffer` (particles → GPU) and the real `MultiMesh` + billboard-shader renderer behind it.
- `src/Effects/FogVolumeClutter.cs` — the authored ambient cloud field: `fogvol.zrd`'s clutter scattered through the gamez `fvol*` boxes, one static MultiMesh per sprite kind.
- `src/Effects/Precipitation.cs` — weather.json rain/snow: one camera-following MultiMesh of flakes or streaks, self-animating on the GPU.
- `src/Effects/WorldWind.cs` — the mission's global wind (weather.json's `WIND` block: static vector + horizontal random-walk gust) and `EffectAmbience`, the per-session seam a `Puffer` reads it through.

### `src/UI/` — screens, overlays and the inspection labs

The launchscreen and splitscreen rig, plus the interactive debug labs. Every lab has a scripted
`--debug-*` twin so a finding can be reproduced headlessly — see `docs/cli.md`.

- `src/UI/MenuInput.cs` — one player's menu input source: keyboard flag + a `Pads` binding, edge/auto-repeat `Poll(dt)`, plus the typed characters and pad-only cursor axes a text field needs.
- `src/UI/BoardMenu.cs` — a board's cursor and item list, engine-free, so the selection rules test off engine.
- `src/UI/BoardMenuItem.cs` — the rows a board menu can offer: Resume, Restart, Exit.
- `src/UI/BoardMenuView.cs` — draws a board menu's rows in the launchscreen's cursor idiom, inside the board style.
- `src/UI/BoardMenuHost.cs` — menu, rows and reader kept together, so a board wires one in two lines.
- `src/UI/SplitScreen.cs` — the splitscreen rig: one SubViewport pane per player (2–4), shared `World3D`, per-player visual-layer band.
- `src/UI/LaunchMenu.cs` — the in-game launchscreen: Mode → Chapter → Plane, pad join/lock, then `Launch` into a session; also the hangar's two doors, the campaign's one, and the renderer both flows draw through.
- `src/UI/InstantActionPresets.cs` — the Table of Contents: the 19 decoded preset scenarios by name, resolved to the setup screens' own cursor positions.
- `src/UI/PlanePickerRoster.cs` — the one roster every human plane picker draws: 11 stock airframes then the store's saved customs, each custom carrying its store name and its airframe's stock node (D32's launch seam); engine-free build/lookup rules.
- `src/UI/PlaneDiagrams.cs` — the two plane-diagram sheets the original draws beside a fitted aircraft, framed per airframe: `OL_PLANEDIAGRAMSTOP.PNG` (204x1870) and `OL_PLANEDIAGRAMSFRONT.PNG` (245x1100), each eleven equal frames stacked top to bottom in airframe-id order. Shared by ammo selection, the campaign's flight check and the hangar's airframe list; the decode is cached for the process, misses included, and a sheet whose height is not a whole multiple of the airframe count draws nothing rather than a mis-sliced picture.
- `src/UI/HangarFlow.cs` — the Build Custom Plane flow, engine-free: the original's nine screens over one scratch `CustomPlaneDef`, back/next navigation, the `IHangarPage` mount point C22-C26 fill (rows, detail, stepper, optional page `HangarArt` and a per-row one), the plane-selection screen's two-stage delete (the original's Sell Plane with no economy to sell into), and the gated commit into `CustomPlaneStore`.
- `src/UI/HangarAirframePage.cs` — the AIRFRAME screen: all 11 airframes as rows, focus previewing one and confirm picking it (raising the string-206 defaults ask as an inline two-row confirm), the stat table's figures and the economy's star ratings per row, the focused airframe's blueprint TGA as page art with its `PlaneDiagrams` plan view under it as row art; nothing is ticked until a pick is made and the ←→ stepper is inert.
- `src/UI/HangarEnginePage.cs` — the ENGINE screen: the airframe's six engines (langui 3100+af*6+id) plus the None row (1165, the decoded dropdown's own last row; 1171 stays the purchase wording), the pick ticked and opened on, confirm writing the scratch engine and the stepper inert, each row's decoded cost and weight via `HangarEconomy.EngineLine`.
- `src/UI/HangarArmourPage.cs` — the ARMOR screen: the four zones through their own langui formats (1191-1194) on the record's own units x5 display scale (0 to 60 in fives, the original's 13-row dropdown), the detail naming the pick as that dropdown does (1165 "None" on zero, else 1170 of units x5) beside the x4 priced cost and weight.
- `src/UI/HangarGunsPage.cs` — the GUNS screen: always four slots titled from the stat table's slot-title strings, each stepping the original's 11-entry dropdown (five calibres single, five twinned via format 506, No Gun 3315), the detail pricing the slot's wing or turret column (doubled for twin) with the calibre's magazine rounds.
- `src/UI/HangarHardpointsPage.cs` — the HARDPOINTS screen: the two per-wing counts through langui 1176/1177, the stepper walking 0-4, the detail speaking the dropdown's 1165/1168/1169 vocabulary with the decoded $410 / 480 lb per hardpoint and the wing's line total.
- `src/UI/HangarPaintPage.cs` — the PAINT screen on the original's own model: the pattern row stepping only the patterns this airframe's availability mask allows (labels langui 3425+index) and loading that entry's six colour/shade defaults, a colour row and a shade row per slot over the 27-row swatch table and its ramps, three decal rows each showing the chosen decal's own tile out of the shipped `PX_P_DECALS.TGA` sheet, and a live preview composed from the pair's own paint-screen artwork (`PaintIcons`, `PX_ICON_<airframe>_<pattern>_0..3.TGA`: the detail plate plus three region masks in their alphas, alpha-over in slot order then the plate on top). That is a different mask set from the `.BM` skins `PlanePainter` paints the flying aircraft with, and a different formula: no shading multiply and no weight normalisation, so a fully-masked texel IS its resolved colour.
- `src/Flight/HangarPaintTables.cs` — the paint screen's decoded tables as CSVM data: the swatch table (`data/hangar_swatches.json`, 27 rows of base colour, default variant and shade ramp) and the pattern table plus the 50 decal names (`data/hangar_patterns.json`). `Resolve(colour, shade)` is the original's own resolver; `Available(pattern, airframe)` is the availability mask; `Nearest(rgb)` maps a version-1 store file's free triple onto an authored swatch. Engine-free and pure, so the whole colour model resolves without a session.
- `src/UI/HangarNamePage.cs` — the PLANENAME screen: one row per character stepped through a filename-safe alphabet plus a length row that adds and removes them, capped at the original's 32-character name, with the detail line assembling the name and marking the focused character.
- `src/UI/HangarPurchasePage.cs` — the PURCHASE screen: the itemised review, one row per priced thing the scratch plane carries (airframe always, engine when chosen, armed gun slots, armoured zones via 1191-1194, wings with hardpoints via 1176/1177) with its decoded cost and weight, a totals row, and the Purchase Now row that commits, flagged with the problems text (1182 + 1227 / 1171) whenever the verdict is not Ok.
- `src/UI/CampaignFlow.cs` — the campaign's out-of-mission flow, engine-free: a stack of screens over one selected `CampaignProfileDef`, the `ICampaignPage` mount point the later screens fill (rows, detail, footer, optional `HangarArt`, optional text field), a registry keyed by `CampaignScreen`, and the navigation API (`GoTo`, `Back`, `SelectProfile`, `Cancel`) those pages steer with.
- `src/UI/CampaignRosterPage.cs` — the player-profile screen: the name field over the roster, CONTINUE creating or continuing a player and opening the cabin, a roster row selecting then continuing, DELETE PLAYER as a confirmed second stage, CANCEL back to the launchscreen, and the original's own name refusals (langui 200/202/212/707).
- `src/UI/CampaignTextEntry.cs` — a campaign screen's one-line text field: the original's alphanumeric-and-space rule and 32-character cap, typed from the keyboard and stepped from the pad through one alphabet, so the field needs no keyboard and produces nothing the profile store would have to sanitise.
- `src/UI/CampaignCabinPage.cs` — the cabin hub: NEXT MISSION (opens the briefing for the profile's next mission, or refuses in the campaign's own words once all 24 are complete), PREVIOUS MISSIONS, PLANE CONSTRUCTION (a `CampaignExit.OpenHangar` request the shell fulfils), and RETURN TO MAIN MENU; `Pictures` layers the pilot's own aircraft photo (`PC_P_HANGAR<airframe>.JPG`) under the painted cabin, whose colour-keyed hole is the window, and the board draws it; `Art` is the flat cabin scene alone, for a caller that wants pixels. `MapPinCount` is a pure, tested stand-in for pins nothing places yet.
- `src/UI/CampaignPreviousMissionsPage.cs` — the scrapbook's finished-missions list, one row per completed mission in `seq` order (long name, area, plane flown from the best-of record) plus VIEW SELECTED (a no-op; the detail line already says everything it would), REPLAY MISSION (`SetMission` + `GoTo(Briefing)`, no advance) and RETURN TO CABIN.
- `src/UI/CampaignBriefingPage.cs` — the mission briefing: everything resolved from `CampaignFlow.MissionSeq` alone, through `cm_sequence` to the storage address, `brief_c%d%d` to the dialog state, the state to its map bitmap and narration name, `sounds.zrd`'s `SETS` to the wav file, and the mission's own `objectives.zrd` to the note, so nothing is computed from the story position. REPLAY BRIEFING / RETURN TO CABIN / GO TO FLIGHT CHECK are the screen's only rows: an uncovered objective is written on the parchment through `Notes`, the `BoardNote` carrying the dialog's own `LIST` widget, so the mission's text is read and never a cursor stop (`BL-487`). The map is the page's `HangarArt`. Labels are `messages.json`'s own `MSG_BTN_*` and an unresolved objective key shows as the raw key, so a missing extraction degrades to the three buttons rather than throwing. It plays nothing: `NarrationWav` and `NarrationStarts` name what a shell must play, and `Advance(seconds)` is the clock a shell drives.
- `src/UI/BriefingScript.cs` — the reveal script, engine-free: the `Briefing.zrd` reader (`BriefingDialog`/`BriefingState`/`BriefingStep`, walking the root list where the 24 states actually live) and `BriefingReveal`, the interpreter that runs a state's 12-opcode beat sheet against a caller-advanced clock, blocking on `Wait`'s authored seconds and `WaitForMarker`'s cue times and keeping each element's opacity, rotation and position as its tweens land. Elements come out in placement order, which is draw order. With no cue points every marker releases at once, so the map finishes under the narration rather than a timing being invented. Decode: `docs/formats/briefing.md`.
- `src/UI/BriefingObjectives.cs` — the briefing's parchment note from a mission's `objectives.zrd`: every `IDENTITY` carrying a `MSG_BRF_*` key, ordered by priority ascending, which is the list an `Objective id index` opcode indexes 0-based. Takes the reader list rather than a path, so it tests without an extraction; resolves text through `Messages`, leaving the raw key visible when the table cannot.
- `src/UI/ObjectivesHud.cs` — the campaign mission's objectives readout, drawn on the **pause screen** and nowhere else (`BL-466`): the original keeps its objectives on the pause screen's parchment and leaves the flight HUD to the gauges, so the whole layer is hidden until `PauseState.Paused`. Reads `CampaignDirector`'s `ObjectiveGraph.Rows` directly (not a re-parse), text through `Messages`, and shows every row rather than gating on the row's own `Awake` flag (an objective authored with no `BEGIN_DORMANT` starts awake without ever running a wake action, so its row's `Awake` flag never turns on even though it is live from the mission's first tick, C1/M02's own primary OBJECTIVE3, and filtering on it would hide exactly the objective a player needs to see first). This also matches the original's own decoded display mechanism (`docs/formats/objectives.md`, `FUN_004acc20`/`FUN_004ad240`): every `IDENTITY` row is built once and shown unconditionally, only the completion mark toggles. A row the mission gives **no message key** resolves to no text and is dropped from the drawing (`DrawnLines`), since drawn it would be a mark against blank space, and `BuildLines` still carries one line per graph row so a suite counts against the graph. The mark takes a column of its own, so a completed line's text starts where every other line's does. Self-mounting like `PerfHud` (its own `CanvasLayer`, on `HudLayers.Board` with the pause board and after it in tree order), so `GameSession` only hands it the shared `PauseState` and adds it. The reference frame (`Complete Mission M02.mkv` at t=12 s) fixes the top-right corner and nothing else, so the glyphs and metrics are TUNE.
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
determinism repo-wide — read `docs/verification.md` first.

- `src/Utils/Config.cs` — dev tuning-override: typed getters over an optional sparse `res://config.json`, else the in-code `const`.
- `src/Utils/EffectsLevel.cs`: the original's graphics EffectsLevel option (`graphics.effectsLevel`, default `high`) and the one global it drives, the clutter fade's squared distance scale; `graphics.clutterFarFade` (default `true`) switches the authored fade off entirely.
- `src/Utils/GameClock.cs` — the session sim clock every sim consumer takes dt from: run mode (realtime/fixed), halt + single-step, time scale.
- `src/Utils/Log.cs` — the diagnostic log: 9 categories × 4 levels, `--log=` console filter, always-on full-detail `.scratch/logs/` file sink.
- `src/Utils/ShaderTime.cs` — the `csky_time` global uniform: the clock's GPU twin, replacing `TIME` in every generated shader; wraps at 3600 s.
- `src/Utils/StartupProfile.cs` — the always-on `[perf] startup …` line: every session build split by phase, `total = boot + Σphases + rest + first_frame`.
- `src/Utils/HitchMonitor.cs` — the always-on frame-hitch detector: `frame_ms > max(medianMultiple × rolling_median, floorMs)`, a `HitchRecord` per trip.
- `src/Utils/HitchSidecar.cs` — the hitch detector's write path: queues a tripped record and drains it a few seconds later to one `[perf] hitch …` line plus one JSON line in `.scratch/logs/<mode>-<stamp>.hitches.jsonl`.
- `src/Utils/PerfSample.cs` — ambient timed leaf scopes: `using (PerfSample.Scope(PerfSite.X))` accumulates per site per frame, and every hitch record carries the sites plus the remainder no site claimed.
- `src/Utils/Rng.cs` — the session's one master seed and the ten named subsystem generators every random draw derives from.
- `src/Utils/ScriptedWindow.cs` — Win32-only window hiding for scripted runs; `ScriptedWindow.Hide()` uses `ShowWindow(SW_HIDE)` on the native window.

### `src/Testing/` — the in-engine assertion harness

`--run-tests` and the `--dump-*` probes. The units that need no running engine live in `CSVM.Tests/`
instead.

- `src/Testing/Probes.cs` — the assertion cores behind the `--dump-*`/`--damage-test` reports: report text **and** a verdict, shared with the suites.
- `src/Testing/EnvelopeMargins.cs` — one flight scenario's distance from every term that could bound it, plus which decoded branches it drove; the parity ledger's coverage half.
- `src/Testing/TestHarness.cs` — `--run-tests`: suite registry, `TestContext`, the PASS/FAIL/SKIP table, JSON report, exit code, engine-error allowlist.
- `src/Testing/SuiteShards.cs` — the `shard:<index>/<count>` term and the deterministic weighted division behind it, over the measured weights in `analysis/engine-suite-weights.json`.
- `src/Testing/CountingEmitterFactory.cs` — the no-GPU `IEmitterFactory` fake a suite installs to observe `PUFFER_STATE` emitter lifetime.
- `src/Testing/RecordingEmitterRenderer.cs` — the no-GPU `IEmitterRenderer` fake that keeps a `Puffer`'s particles instead of drawing them, so its three modes are assertable.
- `src/Testing/SuiteCatalog.cs` — the ordered registry of the in-engine suites; domain scenario bodies live in `*Suites.cs` modules, while `SuiteConstants` holds their shared golden inputs. Six no-blocker suites (`flight-envelope`, `gauge-colours`, `gauge-arrow-tween`, `weapons-defs`, `weapon-blast`, `markers-rig` — 11 airframes, blast/fuse rules — moved to `CSVM.Tests` (`FlightEnvelopeTests`, `GaugeColoursTests`, `GaugeArrowTweenTests`, `WeaponsDefsTests`, `WeaponBlastTests`, `MarkersRigTests`) since their bodies called only `Probes.*`/plain statics with no live Node. `GaugeCluster`'s colour/sweep statics (`GunIndicatorColor`, `HardpointIndicatorColor`, `SlotIndicatorColor`, `DamageZoneColor`, `TargetArrowAngle`, `TweenArrow`, `IndicatorLowFrac`, `ArrowSweepDegPerSimS`) went `internal` → `public` for the move; `StallBlinkHalfPeriodS`/`AdvanceStallLamp` and the stall-specific consts stay `internal` (`stall-warning` is Wave B, scoped to `GaugeCluster` only).
- `src/Testing/*Suites.cs` — the domain scenario modules: puffer, combat, ordnance, Instant Action, AI, the campaign, music, targeting, targeting candidates, wingmen, zeppelins, damage, destroy choreography, animation/effects, world/tools, mid-mission world fidelity, and the weapon-ray occluder census.
- `src/Testing/SuiteConstants.cs` / `BurstTimeline.cs` / `SuiteViewers.cs` / `EffectStageSuiteHelper.cs` — the focused shared inputs, timeline values, pane-camera fixtures, and staged-effect fixture used by more than one suite module.
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
- `src/Session/EffectCatalogue.cs` — the record of which authored anims are playable effects, and what their defs need staged: the effect/crash/damage-shim name tables, the two surface-indexed def vectors (`CrashDefTable`/`TouchdownDefTable`), and the anchor-root derivation both binds stage from.
- `src/Session/SurfaceDefTable.cs` — one of the original's per-surface anim-def vectors (`player_crash_*` / `touchdown_*` over the surface registry) and the cascade that indexes it with a struck material's surface id.
- `src/Utils/EffectPools.cs` — the `data/effect_pools.json` reader: how many copies of each effect template the stage builds, per ROOT, scaled by player count.
- `src/Session/WeatherRig.cs` — loads the mission's weather and drives the per-rig skydome, whiteout, deck and zone gate each frame.
- `src/Session/WorldEffectsFactory.cs` — builds the impact/destruction effect stages and the per-plane crash runtime.
- `src/Session/LensFlareRig.cs` — the sun's lens flare: screen-space sprites along the sun→centre line, occlusion-tested.
- `src/Session/FlightRoster.cs` — the session's aircraft set: builds the human field in player order and introduces AI aircraft later through one assembly seam.
- `src/Session/FlightRosterInputs.cs` — the roster's three grouped dependency contracts: aircraft resources, world bindings, and human-session bindings, plus the copied flight policy.
- `src/Session/HumanFlightAdapter.cs` — the FlightRoster's internal human-rig adapter: painted plane, `FlightController`, loadout/ordnance, HUD instruments, damage visuals, audio, stunt run, spawn, crash runtime.
- `src/Session/AiFlightAssembler.cs` — the FlightRoster's internal AI path: pilot preparation, model/controller/loadout, damage/crash runtime, and placement.
- `src/Session/InstantActionDirector.cs` — the engine-side sequencing of one Instant Action mission: construction, the actor build phases, the zeppelin switch and wave arm, the sequencer tick and the end-condition/wrap-up wiring, called by `GameSession` at its pinned build and drive points.
- `src/Session/InstantActionRuntime.cs` — owns one Instant Action mission's actor set: the loaded `InstantActionDef`, the ace's own spawn draw and team/rating, the wingmen's fan placement/escort chain/flight-size clamp, E11's two per-wave-member draws (the five-row pilot-personality table, the accent-12 re-roll), and F12's objective-zeppelin selection.
- `src/Session/InstantActionWaves.cs` — the decoded wave sequencer's own selection/trigger/geometry, pure and engine-free: the wave counter (advance-on-last-kill, 0-enemy fall-through, no advance past wave 4), the 500-m-from-nearest-human spawn draw with its literal-index-0 fallback, and the 100 m/45° fan.
- `src/Session/ScriptedPathVehicles.cs` — one mission's scripted-path vehicles: the snap onto waypoint 0, the freeze, `START_TAXI`'s release, and the handoff back to the flight model.
- `src/Session/CampaignRoster.cs` — the engine-free plan of a campaign mission's `aiv` roster: each block resolved to an airframe, its authored net or the decoded netless-wingman escort (never both), the merged volumes and the leader lookup; `CampaignDirector.BuildRoster` places it.
- `src/Session/GeneratorCycle.cs` — the decoded egen launch timing law for one generator, pure and engine-free: composed periods, hold-not-cancel blocking, the capacity stand-in and F12's wave-credit budget that switches it back off.
- `src/Session/NetTrailerTargets.cs` — resolves a patrol net's trailer name (`player`, a zeppelin, a train) to a live position, so an anchored net rides its target (`BL-377`).
- `src/Session/AiGeneratorRuntime.cs` — runs a mission's egen generators (`--generators`): load-time drop rules, per-cycle stepping, spawns through the handed roster callback — or, on an Instant Action zeppelin run (F12), releases an already-built wave member instead.
- `src/Session/AiVoiceRuntime.cs` — wires E16's dispatch into a session: the decoded event sources (hit-path DI, Downed death cries, acquisition call-outs, taunts) played through `CombatVoice` + `WorldSounds.PlayOneShot`.
- `src/Session/ZeppelinRuntime.cs` — runs a mission's zeppelins (M4 F17+F18+F19, `--zeppelins`): places each record's world node at its authored pose, flies it along its net through `ZeppelinMotion`, owns the multi-zone damage (per-part registry pools, the survivor-count kill, the authored hull death) and fires the broadside (`ZeppelinRuntime.Cannons.cs`: real unowned `wep_28` rounds through `ZeppelinBroadside`).
- `src/Session/TurretEmplacementRuntime.cs` — the world AA emplacements: the standalone `ai.zrd` family placed at its `NODES` patterns against the built chapter world, shipped `ACTIVATED` honoured, `SetActivatedUnder` the Instant Action builder's subtree activation (what arms the objective zeppelin's rings), `--wake-turrets` the `WAKEUP_TURRETS` stand-in.
- `src/Session/CampaignProfileStore.cs` — JSON persistence for a named campaign profile under `user://Profiles/<name>/profile.json`, following `ScoreStore`/`CustomPlaneStore`'s precedent: funds, owned planes (name-referenced into the global `user://Planes/` store, never a copy of it) with their per-gun ammo and per-pylon ordnance picks, mission results (the original's two halves, latest attempt and best-of merge), the completed-mission count, the granted aircraft awards and the cross-mission destruction log. `SessionSpec.CampaignProfile`/`CampaignMissionSeq` (`--campaign=<profile>:<seq>`) carry the launch-time selection as plain values; building the mission from them is the campaign director's job, not this store's.
- `src/Session/CampaignProgression.cs` — the rules that write a profile: the best-of merge of one mission attempt (each field's rule is the original's), the monotonic position only a completed primary objective raises, the replay rule Previous Missions flies under, and the five aircraft awards granted once per profile, each with the build its special-plane template authors (`AwardBuild`, `docs/org/hangar.md`) so a reward aircraft flies its own guns, armour and engine tier rather than its stock airframe. The cash half of the reward table stays with the hangar economy; this class banks the money an attempt reports.
- `src/Session/CampaignPersistLog.cs` — the cross-mission state log (`BL-243`): captures what `PERSIST_LOG` defs a mission left destroyed out of `AnimRuntime.Destructibles`, keyed by chapter, by the capturing mission's `cm_sequence` position and by gamez node index, and re-applies it to a later mission of that chapter through `AnimRuntime.CarryState`, silently: the pool reads destroyed at HP 0 (or the carried HP at its damage stage) and the nodes take the pose the death ends in, with no effect, sound or choreography, so a later hit on a carried kill is a no-op. `Through`/`ApplyTo` take the position of the most recent EARLIER mission of the chapter (`CampaignSequence.PreviousInSameChapter`, which `CampaignDirector` resolves once at construction) and carry only what positions at or before it captured. ⚠ Never fold a chapter without that cut, and never route the replay through `DamageAt`: the first opens a mission on its own previous sortie's wreckage (CM07 is chapter 1's first mission, so its fort came up with the AA guns already dead and silent), the second runs the previous mission's death choreography at mission open. A state stored without a position is an older profile's and belongs to whatever earlier mission wrote it. Persistence is read from the READER def bound to a node, never from the compiled twin, which drops the flag. Regression: the `carried-state-silent`, `campaign-persistence` and `c1-aa-guns` suites.
- `src/Session/CampaignLoadout.cs` — the bridge between a profile's stored picks and a flying aircraft's fit: one `OwnedPlane`'s ammunition indices and ordnance table indices as the `LoadoutChoice` a launch hands the session, which `Loadout.Bind` then lays over the aircraft's base fit. Engine-free, and both encodings are the campaign screens' own rather than the original's per-pylon ordnance id, which is a rocket table index decoded in `docs/formats/saved-games.md` and deliberately not adopted here, so an unset pylon is left to the base rather than written back. ⚠ `PylonRow` is the one decoder of the stored one-based ordnance value, and every screen that reads the field calls it rather than subtracting one itself; a second reading of the field puts a different rocket on the flight check than the ammo screen just committed.
- `src/Session/ObjectiveScript.cs` — one mission's parsed `objectives.zrd`: the file-level keys and the contiguous `OBJECTIVEn` blocks in the typed shape the graph runs, found by exact name so every shipped misspelling lands in no field and stays dead. Decode: `docs/formats/objectives.md`.
- `src/Session/ObjectiveGraph.cs` — the objectives runtime, engine-free: the four-state machine per objective, the rotating one-completion-per-tick scan, the chaining executor with its already-awake truncation, the nap that clears a completed flag, the condition families' OR, the mission countdown, the win/loss flags and the display rows D33 reads. Reaches the world only through `IObjectiveWorld`.
- `src/Session/ObjectiveSites.cs` — the flown campaign mission's objective sites as targeting candidates, which is what tells the player where to go: the set is `targets.zrd`'s own `objective` entries edited by `objectives.zrd`'s `ADD_`/`REMOVE_OBJECTIVE_TARGET`, offered to each player's `TargetPool` with the mission's objective flag so they head the Enemy cycle and the ordinary selection draws one at a time. Rebuilt from its live source every frame, so a site under a moving node moves with it. A site the mission names by a bare `TRAVELERS` point sits at that point, not at the world node of the same name.
- `src/Session/CampaignDirector.cs` — the engine side of a campaign mission and the sibling of `InstantActionDirector`: resolves a `--campaign=<profile>:<seq>` launch to its chapter/mission, arms the graph against the built world's runtimes, and at mission end records the attempt through `CampaignProgression`, folds the destruction log into the profile and raises the return-to-cabin exit the session layer acts on. It also owns the mission's two music duties: routing a `mu*` sound group to the process music channel instead of a positional emitter, and running the decoded proximity scan and player-damage ping that put the score into battle. The wingman's aircraft and fit are resolved here too, from the profile it already has open; nothing spawns that aircraft yet. A cutscene hold (callback 20) stops its whole step.
- `src/Session/CutsceneController.cs` — the host a cutscene definition raises its `CALLBACK` codes to: the letterbox bars and the cutscene camera the definition itself drives, the world/objectives hold, the player out of flight with the chrome off, the AI parked, then one hard cut back to gameplay on the definition's end or on a skip. It answers for the two story-mission intros always, and for whatever `HostDefinitions` registers (the landings trigger's own rows and their `CALL_ANIMATION` closure). Scoping is by definition name, never by authored code.
- `src/Session/LandingApproachRuntime.cs` — the mid-mission cutscene trigger: ticks a story mission's resolved `LandingApproaches` against the flown aircraft (arming gate, speed band, attitude cone, condition volume) and starts the row as an explicit mission trigger, so its call closure can stage library-root actors and satisfy animation-state or node-state objectives; it hands the row's definition to `CutsceneController.Own` first, so the episode belongs to the row however deep the callee raising its first code sits. A row fires once per entry into its volume: the handoff leaves the aircraft where the cutscene parked it, still inside the volume that started it. An `auto` row lights `AutoLandOffered` (the HUD's prompt line) rather than starting anything by itself; the auto-land button (`FlightController.AutoLandPressed`) starts the row's own animation, latched the same way the manual row is. Story missions only, for the reason `WorldSession.Options.LandingTriggers` gives. Rows whose approach nodes are staged later are retained and bound when those nodes appear: CM02 grafts its three Balmoral cones with the roster, while CM07 summons its train-pickup cone, which the train's own `pickup_timing` opens and closes in step with the track loop. The trigger starts nothing off the pickup sensor; restarting the timing on entry re-phased the switch the passenger's wave-or-drop fork reads.
- `src/Session/LadderSwitch.cs` — the original's rope-ladder switch as an engine-free rule and state machine: the ladder is wanted when the aircraft's own up axis is within 45 degrees of world up and the player is inside an active `pickups.zrd` sensor, and the switch starts `drop_ladder` or `retract_ladder` to match, one transition at a time, holding a transient state until the definition's own `CALLBACK 123` settles it. A mission that authors neither definition flips the state silently, so no per-mission table exists. Decode: `docs/org/ladderSwitch.md`.
- `src/Session/LadderSwitchRuntime.cs` — `LadderSwitch` flown against the built world: every frame outside a cutscene, with the player flying, it reads the flown aircraft's attitude and position against the mission's pickup sensors and starts the ladder definitions as mission triggers, so the drop's `OBJECT_ADD_CHILD` can materialize the library rope ladder. It takes the runtime's `CALLBACK` host slot and chains to the cutscene host behind it, which is where the original registers the switch on each definition. Bound with the landings trigger, story missions only.

### Session root and tests

- `src/Pads.cs` — single owner of "which gamepads exist": the phantom-device policy (span every pad) plus the `--no-pads` switch.
- `src/SessionPaths.cs` — resolves extracted-data paths (per-chapter gamez/texture/zrdr; `PreferUnzipped`); extracted from `GameSession`.
- `src/SessionSpec.cs` — the launch args as one immutable, engine-free value: `Parse` parses **and** resolves (closed `SessionMode`, `--det` bundle, placement, `BuildsCollision`), plus the pure arg parsers.

- `CSVM.Tests/` — the xUnit project (`dotnet test`): engine-free reader units on hand-authored fixtures + `extracted/` golden counts, skipped when absent; plus eight former in-engine suites moved here as `Probes.*`/plain-static/`StuntMission`/`GaugeCluster` facts.

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
Carries `flags.active` as `Active` (same default) — initial runtime visibility, honoured by
`WorldBuilder.Add`, which stages an inactive world-build root hidden so mission choreography can
still resolve and activate it later.
Carries the node's `zone_id` as `ZoneId` (default −1 when the field is absent, i.e. ungated) — the
original's per-node visibility zone, honoured per node by `SceneBuilder` and per camera by
`Mech3/ZoneGate.cs`.
Carries the World node's partition grid twice: `PartitionNodes` is the flat distinct set every
placement walk uses, and `PartitionCellNodes` (with the grid origin and cell size read off the first
cell's own bounds) keeps the per-cell membership an area query needs (see `WorldPartitionGrid`).
`IsMarkerGizmo(meshIndex)` classifies a mesh as an authoring mark rather than scenery (one flat-
coloured untextured triangle — see docs/formats/world-structure.md); SceneBuilder draws none.
Carries each model's `flags.lighting`/`flags.fog` as `GameZMesh.Lighting`/`Fog` (default true) —
the original's self-lit and unfogged marks, honoured by SceneBuilder's per-model shader variants.
Carries each material's `soil` label as `GameZMaterial.SoilId` (the original's numeric surface type
id — `player_crash_*`/`touchdown_*` selection indexes on it, `analysis/surface-classification/
FINDINGS.md` 2026-08-11), mapped through `SoilLabelToId`. An unmapped label throws rather than
defaulting to 0 — mech3ax's label is just its Soil enum variant name, and a new one means the
extractor changed, not that the id is 0.
Parses the WHOLE per-polygon `materials` list: element 0 is the base skin, the rest become
`GameZPolygon.OverlayPasses` (`GameZPolygonPass`: material + its own UVs), which SceneBuilder draws
as extra surfaces — 619 polygons install-wide, none in planes.zbd (docs/formats/gamez.md).
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
`SoftAlphaCoastline` names the five waterline sheets the ratio misreads, because their solid
dry-land half outvotes the feathered ramp that is the point of the texture.
`Build` is the one construction path — decode, classify, drop-in, mip chain — and `Find` caches
its result; `BuildMipped` hands the same Image to `--dump-mips` un-cached.

## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order as depth bias:
priority, then subface, then overlay pass, then within-mesh surface rank, with the cross-node
tie-break coming from `NodeBiasOf` — the world's `ConflictRank` map where the caller set one, the
flat node index otherwise (aircraft, `--node=`). Every `node_bias` in the project goes through
that one method. `BuildSubtree`'s `zoneGate` flag (off by default; on for the world walk and the
map-edge extension) moves each built mesh instance onto its own node's `zone_id` visual layer
(`Mech3/ZoneGate.cs`). It stays off for the cloud deck and the skydome, which are per-rig
camera-anchored copies gated by `Node3D.Visible` instead.
`CollidersForMesh` splits a mesh's colliding geometry into one trimesh per surface class actually
present (water/buildings/untagged, each polygon's own texture deciding), each also carrying
`SurfaceIdMeta`, the original's numeric surface id for the bucket's dominant material. Each
collider-bearing node registers with `WorldCollision`, which owns its `Disabled` flag from then
on. A `GameZ.IsMarkerGizmo` mesh draws nothing but its Node3D is still built, since animations
attach puffers and sounds to those nodes by name.
A surface's colour is `vertex colour × material` (the original's baked-lighting modulate), except
where `GameZ.VertexColorsRestateMaterialColor` detects the two are the same authored value
(mostly skydome skirts — docs/formats/weather.md). `DebugClutterFlag` (`--debug-clutterflag`)
overrides that colour with the decoded `no_clutter` bit instead. Every shader on one instance
shares the ordered preamble in `csky_instance_uniforms.gdshaderinc`; see that file for the
contract, and `GetBiasShader` for how the model's `lighting`/`fog` flags select shader variants
instead of driving a uniform. Format/decode: docs/formats/gamez.md, docs/formats/world-structure.md,
docs/formats/gotchas.md.

## src/Mech3/ZoneGate.cs
The original's per-node visibility gate (`FUN_0056c430`). `FUN_004d62d0` arms the camera each frame
with the zone set `{0, camera weather state}`; the walk draws a node iff its gamez `zone_id` is
`-1`, or is in that set. Four members: `Draws(zoneId, state)` (the rule), `LayerFor(zoneId)` (the
visual layer a gated zone's meshes are MOVED onto — 0 for `-1`/`0`, i.e. leave on the default
layer), `CullMask(mask, state)` (narrows the band to one zone, every other bit untouched) and
`OpenCullMask(mask)` (the whole band back — `--no-zone-cull` and the launcher camera's per-session
reset). Constraints and evidence live on the class itself; see `CSVM.Tests/ZoneGateTests.cs`.

## src/Mech3/ConflictRank.cs
The world's cross-node draw-order tie-break. Buckets every built triangle by its world plane,
finds the cross-node pairs that are coplanar, same-priority, same-subface and genuinely overlap
(clipped area > 1 m², never an AABB touch), and layers that DAG by longest path — so a node's rank
counts conflicting layers beneath it, not nodes before it. Every edge runs low node index → high,
so the layering is a topological order of the original's own draw order and cannot invert authored
layering. `WorldBuilder.RankConflicts` runs it before the build; 11–45 ms per chapter.

## src/Mech3/WorldCollision.cs
Owns every `SceneBuilder`-built collider's `Disabled` flag and derives it: enabled exactly while the
owning node is visible in the scene tree and no ancestor is faded out. `Track` (called once per
collider-bearing node as it is built) binds it to the node's `VisibilityChanged` — which Godot
propagates to descendants — plus `TreeEntered`, because a world is assembled and bootstrapped while
still detached, where visibility writes emit nothing. `SetFaded` is the second input: an
`OBJECT_OPACITY_*` fade is a shader parameter visibility knows nothing about.

## src/Mech3/PlaneBuilder.cs
Builds one aircraft from its GameZ subtree (shaded, cullBackfaces: true — interior lattice must be
backface-culled or it paints over the skin), skipping cockpit/destroyed/shadow subtrees, and
`*_hook` unless `dockingHook` asks for it: a human rig gets the airframe's skyhook group built and
parked at its archive-authored inactive bit, because the hookup cutscene's `<x>_hook_extend`
activates that group rather than creating it (`docs/formats/anim-definitions/cutscenes.md`).
Repaint(scheme) re-liveries the built plane in place; BuildDestroyed builds the wreck subtree with
the plane-root→destroyed transform chain baked in; WingFlares/DamagePanels expose collected nodes.
Flight (`spinningProps`) now builds the static `staticpropN` disc alongside the spinning blur discs
it always built, not just one or the other — the startprops/stopprops choreography cross-fades
between them at spawn/engine-stop (`FlightController`), so both must exist. `staticrotorN` (the
autogyro) is unaffected and stays skipped in flight — that def only names propeller nodes.
`Build` also reads `CockpitCameraOffset`, the plane-local `cockpit_camera` marker translate
(`MarkerRig.FindNamedMarker`, fallback the origin), for `CameraController`'s first-person
placement (PLAN-cockpit-view, A2); the marker read walks past the alternate-state subtrees to the
authored node in the top-level `markers` group.

`cockpitInterior: true` (PLAN-cockpit-view, B11) takes `cockpit1` back out of the skip list for
that build alone and mounts it hidden as `CockpitInterior`: local transform = the
`cockpit_camera` offset, a uniform `InteriorScale`, and the fixed
`CameraController.HeadPitchOffsetRad` tilt, then `ParkInteriorStates` walks it. Only a
human rig asks for it — an AI plane never builds a cockpit. The subtree's `pcdp4`/`pcdp6` torn-skin
panels build hidden alongside it, kept off `DamagePanels` (the exterior set the pairing walk
measures mesh centers over) and exposed instead on their own `CockpitDamagePanels` list, which
`DamageVisuals` flips off the same `pdpanel4`/`pdpanel6` entries as the exterior pair (B12).

⚠ **The interior's off-states ship `active: true`.** Five windshield bullet-hole groups
(`bullet1`…`bullet5`) and two warning lamps (`lowalt_on`/`stallwarning_on`) are authored visible on
all 11 airframes and hidden engine-side until something drives them, so an unparked build paints
bullet holes across the sky of a pristine plane and holds both lamps lit. `IsInteriorDrivenState`
is that named set; `ParkInteriorStates` hides it plus anything the gamez marks `active: false` (the
Devastator's `nitrogauge`, the only such node). ⚠ It is a NAMED set, not a blanket hide: the
damage-dial zones, the belt segments and the needles are always-drawn geometry that changes
COLOUR, which is `GaugeCluster`'s own decode of the same nodes.

⚠ **The interior is authored in its own space, and the two spaces are not a similarity apart.**
The eye sits at `cockpit1`'s origin looking down −Z (`extracted/zrdr/instruments.zrd.json` places
the whole instrument panel at z −17.5 straight ahead of it), while the interior's own elevators sit
at y −10.5 where the exterior's sit at −0.40 — it is a stylised model built to be looked at from
one point, not a scaled copy of the aircraft. So the framing is scale-invariant and
`InteriorScale` is a port TUNE choosing only how the interior composites against world geometry.
`cockpit2` is skipped defensively and appears in no shipped tree.

⚠ **The mount carries the −4.70° head-pitch tilt, and that is what puts the gunsight on the guns.**
The offset tilts the WORLD view down; the pilot's relationship to his own cockpit does not tilt
with it, because the original draws the interior in its own pass from the interior origin along the
interior's own −Z. Mounting the subtree tilted is how a single-pass renderer says the same thing.
Measured against `OriginalScreenshots/Videos/CAP-02 Cockpit Second10.mp4`: there the sight ring's
crosshair sits 4.79° above screen centre and never moves by a pixel across the clip, which is the
head-pitch offset itself — the sight is on the nose axis, and the gun pipper (which marks that same
axis, `ImpactReticle`) sits on it in straight flight. Mounted untilted the sight rides 3.9° above
the pipper and the two never meet. Head-look is NOT applied to the mount: the interior stays
plane-fixed, so panning the head still swings the cockpit across the view, as the original does.
⚠ A residual remains: the tilted mount overshoots by 0.60°, leaving the pipper ~6 px above the
crosshair at 720p where the original has them coincident. The exact fit is a 3.82° tilt, but that
is a Bloodhawk-fitted number with no decode behind it and the sight's height is per-airframe
geometry, so the decoded constant is what ships. `BL-` follow-up: measure the same offset on a
second airframe's cockpit footage before trading the constant for a TUNE.

## src/Mech3/PaintScheme.cs
One aircraft livery: pattern name + three colours + three decal indices — the paint_* record a
vehicle.json def carries (see docs/formats/paint.md). LoadCatalog keeps one scheme per pattern
name (the 12 shipped patterns); Random() draws a plausible livery when none is given.

## src/Mech3/PatternLibrary.cs
Decodes the original's .BM paint patterns from extracted/rof/ASSETS/GRAPHICS/<PATTERN>/ (produced
by ExtractRof.ps1; .BM layout in docs/formats/rof.md). PatternsFor(prefix) lists the patterns
shipping skins for one aircraft — a pattern is per plane; Skin() caches per (pattern, skin) so
several aircraft in one session share a decode.

## src/Mech3/PlanePainter.cs
Applies a PaintScheme to one aircraft: composites its skins from the pattern's region masks and
swaps the three decal placeholders. Read docs/formats/paint.md and rof.md first — the composite
formula, the shading-plane choice, and the bottom-up .BM rows are documented there.

## src/Mech3/PropParts.cs
Classifies a plane's propeller/rotor subnodes by name (staticpropN/staticrotorN, nitropropN,
propN/propNb, rotorN/rotorNb) and supplies each spinning kind's local axis + rate — the
XYZ_ROTATION values (deg/s, docs/formats/anim-definitions.md) from plane_props.json (spinprops)
and autogyro.json (agyro_rotors): props spin about local Z, rotors about local Y.

## src/Mech3/ControlSurfaces.cs
Classifies a plane's control-surface mesh nodes + hinge axes: the deflecting node (l/r_aileronN,
l/r_elevatorN, l/r_rudderN, the Fury's l/r_rudder_rotate) hangs under a hinge parent group whose
transform places/orients the hinge line; ailerons/elevators hinge about local X, rudders local Y.
The name patterns are the original's own six `sprintf` node lists (docs/org/flightModel.md, "The
original's control-surface animation"), which is why the elevators classify per side: they carry a
differential roll term, so left and right settle at different angles.

## src/Mech3/WingLights.cs
Single source of truth for wingtip nav lights: the flare-node predicate (wing_flare1/2), the glow
texture (oil_liteflare), the warm-amber flash colour (0.88, 0.78, 0.36 = wing_light.json's
LIGHT_STATE COLOR), the blink period (1.5 s = its LOOP SEQUENCE_OFFSET) and the point-light range
(0.5-1.25 m, also LIGHT_STATE). PlaneBuilder hides and re-skins the flares (additive tint, one-sided
as authored, no billboard); WingLightBlinker flashes them and emits a matching OmniLight3D per side.

## src/Mech3/WorldBuilder.cs
Builds a chapter world (fullbright): World children + partition-referenced subtrees; skips `horizon`
(`BuildHorizon` builds the camera-anchored skydome separately, unfogged per its authored
`fog: false`), `fvol*` (`IsFogVolumeNode`, shared with `FogVolumeSpec.VolumesOf` so the skipped set
and the cloud-scatter set are one list), `dzpaths`.

Every world node the walk builds is stamped with its own `zone_id` visual layer (`SceneBuilder`,
`zoneGate: true` — see `Mech3/ZoneGate.cs`), so the camera's weather state culls it. The **deck**
and the **dome** are the two exceptions: both are per-rig camera-anchored copies with no shared
visual layer to stamp, and take the zone rule through `Session/WeatherRig.Tick` instead. The deck
(`CloudDeck`), its altitude, its SUNLIGHT dimming and its map-edge annulus, and the dome's
per-chapter zone selection and dome count (`DomeZonesToBuild`), are all measured original
behaviour — read docs/formats/weather.md before touching either, and docs/org/weather.md for the
original's own zone-selection function map.

`HideUnplacedEntities`/`RestorePlacedEntities` switch off, then restore, the entities a chapter
parks at the world origin awaiting mission placement. `NoCollisionNode` exempts sky/cloud/billboard
geometry and anything authoring `intersect_surface: false` from colliders (docs/formats/gamez.md).
Placed roots whose authored `active` flag is false are built and indexed but begin hidden; C3/M03's
`barracuda` is activated later by `sub_movement`.
A static probe (`analysis/collider-probe/probe.py`) reproduces `ColliderCount` independently
(`BL-070`). `CreateEdgeExtender` hands off to `MapEdgeExtender.cs`; clutter decoration is
`Clutter.cs`.

Static helpers (`HorizonZonesOf`, `CloudDeckAltitudeOf`, `DomeZonesToBuild`, `DetachedWorldAabb`,
`FogVolumeZoneIdOf`, `MatchNodes`) are pure over `GameZ`/a built subtree and testable off-engine
(`CSVM.Tests/HorizonDomeTests.cs`, `DeckRegimeTests.cs`).

## src/Mech3/MapEdgeExtender.cs
Rolling window (`Rings`=5 of 1024 m cells, diffed only on cell crossings) of repeated border tiles +
clutter (grown from `ClutterBuilder.ExportedKinds`, each copy keeping its source stamp's fade
thresholds) continuing the world past the map edge, one window per session shared by every player
camera; the window's 5120 m reach exceeds the largest authored clutter fade at every detail level, and is unchanged by `graphics.clutterFarFade=false`, which simply leaves the window edge (under the mission's fog) as the extension's visible limit. `ClassifyGroundMesh`/`IsCompletionStrip`/
`FoldAxis` are pure statics pinned by `MapEdgeTileTests`/`MapEdgeFoldTests`; `--dump-tilegrid` writes
the per-cell acceptance census `WriteCensus` builds. The original's own continuation behaviour and
the per-chapter fold measurements: docs/formats/world-structure.md.

## src/Mech3/Clutter.cs
Stamps the boot-script clutter templates across placed polygons carrying the template's ground
texture, **at the polygon's own texture-UV lattice** — one stamp per integer UV repeat across each
triangle; sprites → one fullbright Y-billboard
MultiMesh per kind, solids → `SceneBuilder.SharedMesh`; the split is `SceneBuilder.ClassifyBillboard`.
The sprite shader takes the decoration model's own `lighting`/`fog` flags as variants (every tree and
bush card in the install is `lighting: false`, so clutter does not dim with the mission SUNLIGHT),
plus a UV-clamp variant from `SceneBuilder.UvsWithinUnitSquare` over the kind's own card UVs.
Every stamp carries its authored far fade as MultiMesh custom data (`Kind.Fades`, exported beside
`Placements`), applied by the sprite shader and by `SceneBuilder.SharedMesh(clutterFade: true)`
for the solid kinds, under the `EffectsLevel` global; the fade's draw is its own stream off the
placement seed. `TemplateNames` reads the chapter's `AddClutterTemplates` list **unfiltered** — which district
dresses a given patch is the per-polygon `no_clutter` gate's decision (`PlaceOnMesh`), not a
curated list here; `OverrideTemplateNames` is `--clutter-templates=`'s replacement for it — the caller's names,
filtered to the ones this gamez carries a root for — so one district can be loaded alone and A/B'd
against the original. It prints one line naming what was requested, what resolved and what this
chapter does not carry, since an absent name is retail-data-normal and would otherwise read as an
empty district.

**The original's placement runtime is written up in [org/clutter.md](org/clutter.md)** — the
function map, the template lookup's first-match scan, the UV-lattice stamp and its local triangle
frame, the engine defaults no chapter authors, and the weight list's sum-and-divide. Read it before
changing a placement rule; the authored side stays in [formats/clutter.md](formats/clutter.md) and
[formats/templates.md](formats/templates.md). The remake-only rules (no world grid, the fixed
placement seed, the `seen` dedup, shared collision shapes) are comments on the members that hold
them.

## src/Mech3/ClutterTemplates.cs
The chapter's `templates.zrd` (`ClutterTemplateSpec.Load`/`.Parse`): one `ClutterKindProps` per
clutter DECORATION MODEL — `substitute`'s weighted roll, `scale_range`, `far_fade_range`, and the
jitter/rotation/slope/damage keys the retail data leaves at their defaults. Schema, offsets and the
per-chapter census: docs/formats/templates.md. Static over a reader list, so all eight chapters are
pinned off-engine (`CSVM.Tests/ClutterTemplatesTests.cs`). Consumed by `ClutterBuilder` for
`substitute`, `scale_range` and `far_fade_range`, the last through `FadeThresholds`, the pure
per-stamp `(near², far², reciprocal)` the fade shaders read. See the class and member doc
comments in the file, and docs/formats/templates.md, for the decode detail — the keying by
decoration model rather than template, the nested-pair bound grouping, the slope-key inversion, and
the substitute-roll/duplicate-name resolution rules are all there.

## src/Mech3/Zrdr.cs
Zrdr extraction reader (zip or unpacked dir): `LoadFile`, `LoadFileOrEmpty`, content-sniffing
`LoadMatchingFiles`, name-predicate `LoadFilesNamed` (for families with nothing to sniff, e.g. the
`ne0*` nets), and `ZrdrDict`, the key/[values…] view over a reader's alternating list.

## src/Mech3/SurfaceRegistry.cs
`Names[id]`: the id→name table a struck material's `soil` field indexes into, so a caller can build
`"player_crash_" + name` / `"touchdown_" + name` and resolve the same def the original resolves.
`IdForName` is the parse-time direction (a name the registry does not carry answers null and is
discarded, as `FUN_005ad630` discards it), which is how a weapon's `IMPACT` blocks land at their id;
`Default`/`Water`/`Player`/`Enemy`/`Buildings` name the five slots code branches on outright.
Ids 0–5 are compiled into `crimson.exe`; ids 6–13 are the `LoadSoils`-loaded list from `ZBD/zrdr.zbd`
`0xe631c`, reproduced by `analysis/surface-classification/soils_list.py`.

## src/Mech3/AiNets.cs
The chapter patrol-net reader (`docs/formats/ai-nets.md`): every `ne0NNNNN.zrd.json` in a chapter
zrdr scope joined with its `neindex.zrd.json` name — nodes, the explicit edge list, raw per-node
tags, the trailer attach target, and the net's own three volumes (`Volumes`, record elements 2–10
as an `AiVolumeSet`). Plus the lookups both ways the data references nets:
`ById` (aiv field 0), `ByName` (egen/zeppelins/objectives, case-insensitive), `Resolve` (either
spelling), and `ChapterFirst` (the net an Instant Action actor is given). Consumers:
`UI/AiNetsOverlay.cs` and `Flight/AiNetFollower.cs`. Golden counts asserted in
`CSVM.Tests/AiNetsTests.cs`.

## src/Mech3/Maneuvers.cs
The shared maneuver-library reader (`docs/formats/ai-rosters.md`): `zrdr/maneuvers.zrd`'s 17
entries as `Maneuver` (name, `natural_touch` difficulty, timed attitude steps, the
autogyro/relative/nitro/bias flags), plus the selection cull (`EligibleFor`: difficulty ≤ the
pilot's 1–9 `natural_touch`, no interpolation table — the stat has no `ai_skill_parameters`
entry by design) and the roster `signature_maneuvers` bitmask decode (`SignatureNames`, over
`ExeTableOrder`). Consumers: `Flight/ManeuverExecutor.cs`; goldens in `ManeuversTests`.

## src/Mech3/EnemyGenerators.cs
The mission `egen.zrd.json` reader (docs/formats/mission-entities.md): the enemy generators that
feed AI aircraft into a live mission, typed as `EnemyGeneratorDef` in the three shipped shapes
(zeppelin launch 17, plain spawner 5, moving spawner 1); a `[null]` file reads as an empty list.
An unauthored door pair takes the loader's node-name default (`DefaultDoorAnim`, `%.5s_open%.2s`),
the close being the open name, as the original's loader has it. Consumed by
`Session/AiGeneratorRuntime`; golden counts in `CSVM.Tests/EnemyGeneratorsTests.cs`.

## src/Mech3/Zeppelins.cs
The mission `zeppelins.zrd.json` reader (docs/formats/mission-entities.md): the 58 zeppelin
instances typed as `ZeppelinDef` — motion limits, net name, targets, healthy zones +
`num_healthy_required` (defaulted to 1 and clamped to the healthy count, the decoded load
rule), engines, gasbags, cannons and `cannon_health`. Motion keys feed
`Flight/ZeppelinMotion` (F17); the damage half feeds `Flight/ZeppelinDamage` +
`Session/ZeppelinRuntime.WireDamage` (F18). Fixture units + install goldens
in `CSVM.Tests/ZeppelinsTests.cs`.

## src/Mech3/InstantAction.cs
`InstantActionDef` (docs/formats/instant-action.md) plus the three
producers decision 2 names, converging on one record: `Load` for a chapter's shipped
`ia.zrd.json`, `LoadFromJson` for a hand-authored `--ia=<path>` file — a plain JSON object using
the same field names, not the zrdr archive's flat-alternating shape — and `BuildFromWizard` for
the launchscreen's Instant Action wizard. `Load`/`LoadFromJson` funnel through one private
`BuildDef(ZrdrDict)`; `LoadFromJson`'s only job is `FromJsonObject`, the small mapping from a JSON
object onto the same key/[values…] shape `ZrdrDict` already wraps (a nested `group1`…`group4`
object flattens the same way), so a JSON-authored mission parses through exactly the same
field-population path a real chapter's does. `BuildFromWizard(baseDef, missionType, playerPlane,
numWingmen, wingmanPlane, waves, lives)` takes a different shape: `baseDef` is the chosen
environment's own `Load` result, and only the fields the wizard actually lets a pilot configure
are overlaid — the ace, the zeppelin node names and `disallow_missions` carry over from `baseDef`
unedited, since they are chapter-level facts with no wizard control. It applies the same
`dogfight_ace` zero-forcing rule `BuildDef` does, so a stale wizard wingmen/waves state behind a
just-switched-to-ace mission type can't produce a solo-breaking def. `EmptyWave` (`MakeWave(null,
false)`) is what an unconfigured wizard wave slot resolves to — byte-identical to a JSON file's
own omitted `groupN`, which is the whole point: `LaunchMenu.WaveFor` returns it outright for any
slot at 0 enemies, regardless of what the militia/aircraft/skill cursors are sitting on.
`Defaults()` is `BuildDef` over an empty `ZrdrDict` — the wizard's fallback if an environment's own
file somehow fails to load. `spawn_points` and `dzones` stay where they already were
(`Flight/SpawnPoints.LoadIa`, `Flight/StuntMission`) — this def does not repeat either.
`PlaneNodeFor` is the eleven-entry display-name → gamez-node table
(`"Bloodhawk"` → `"player_bhawk"`), a deliberate duplicate of `UI.LaunchMenu.Planes` rather than a
shared one — the plan's file-contention notes reserved `LaunchMenu.cs` for H15/H16 alone. Fixture
units + install goldens in `CSVM.Tests/InstantActionTests.cs`; the wizard's own build path is
`CSVM.Tests/InstantActionTests.cs`'s "The wizard's own build path" region (a wizard-built def and
its hand-authored `--ia=` equivalent compared field for field) and
`CSVM.Tests/LaunchMenuWizardTests.cs` (the militia/aircraft/skill rosters, `WaveFor`).
Every optional key resolves to the original's own reset-then-overlay default rather than null; see
`InstantActionDef`'s class and member doc comments in the file, and docs/formats/instant-action.md
"The built-in defaults", for the per-field rules (the `Devastator`/ace fallbacks, the
`dogfight_ace` parse-time wingmen/wave zeroing, `ZeppelinType`'s lone undecoded default, and why
`ground_target_name`/`ground_target_node` stay out).

## src/Mech3/AiSkills.cs
The AI pilot-skill constants (docs/formats/ai-rosters.md): player.json's
`ai_skill_parameters` block as `[value@1, value@9]` endpoint pairs indexed by the 1–9 rating
(`At`, plus named helpers for D14's two angles), the roster accessors
(`RosterSkills`: aiv slots 22–30 by stat name, `-1`/omitted = null; `RosterPrimaryTarget`:
slot 6; `RosterRatingBiases`: slot 33 as `AiRatingBias` — wildcard `Matches`, shipped pairs,
a third element accepted and preserved raw, never acted on; the spawn-facing `Roster*` readers for
slots 0–5, 20, 21, 31, 32, 40 and 65, every one defensive over a short block) and the thin
per-mission roster loader (`LoadRoster`), plus the positional-header join that exposes disabled
generator parameter blocks (`LoadGeneratorRoster`). Units + shipped-constant goldens in `AiSkillsTests`;
slot 6/33 census goldens in `AiTargetRankingTests`; the spawn slots in `CampaignRosterPlanTests`.

## src/Mech3/AiVolumes.cs
`AiVolume` (radius, upper, lower) and `AiVolumeSet` (activation, attack, return): the one shape
both authors of an AI's range volumes are read into, a roster block's twelve slots 8–19
(`FromRosterSlots`, three named per volume plus a flag no block authors) and a net record's nine
floats at elements 2–10 (`FromNetRecord`). `Overlaid` is the engine's per-field non-zero test, so
`net.Overlaid(block)` is the decoded order (docs/org/aiPilot.md "Net assignment"); the
`min_ai_active_dist` floor is `Session/CampaignRoster.cs`'s `ApplyVolumes`. The altitude bands are
read and carried but have no consumer: `Flight/AiModeMachine.cs` gates on radii alone.

## src/Mech3/VehicleDefs.cs
The `vehicle.json` def table as an index, next to `Flight/PlaneStats.cs`'s full read of one def:
`DefForBlock` strips a block name's trailing `_N` ordinals until a def matches, `ModeOf` walks
`kind_of` to the nearest authored `mode` (`jet` at the root, the engine's zero default),
`AirframeFor` finds the player airframe node an AI def's model is built from (the `p`-prefixed twin
of the nearest ancestor, else of the chain's `nodename`), `BaseDefForPlayerNode` is its inverse and
`DerivesFrom` is the variant test `PlaneStats.LoadForAi` enforces. Pure over the parsed root
(`FromRoot`), pinned in `CampaignRosterPlanTests`.

## src/Mech3/FogVolumes.cs
The chapter's `fogvol.zrd` (`FogVolumeSpec.Load`/`Parse`) plus `VolumesOf`, the gamez census of
`fvol*` volumes — the two halves of the authored ambient cloud field, rendered by
`Effects/FogVolumeClutter`. Schema, per-chapter values and the decoded/inferred split:
docs/formats/fogvol.md. Both halves are static over a `GameZ`/reader list, so the pair is testable
off-engine (`CSVM.Tests/FogVolumeTests.cs` pins all eight chapters).

`FogVolumeBox` carries the authored shape (its face planes), not just its axis-aligned bounds.
`FogVolumeWhiteout` is the in-volume whiteout rule C5 alone arms, pure and off-engine-tested
(`CSVM.Tests/FogVolumeWhiteoutTests.cs`); `Session/WeatherRig.Tick` is its one consumer.
`FindMapSpanningSlab` is the data-driven test for a chapter's map-edge-continuation cloud slab.
See the member doc comments in the file, and docs/formats/fogvol.md, for the decode detail.

## src/Mech3/Messages.cs
The game's localized string table: plain `System.Text.Json` over the extracted `messages.json`
(NOT a zrdr reader), a case-insensitive key→value map resolving the `MSG_*` keys missions reference.
`Fill`/`Format` substitute a template's `%1`…`%9` placeholders (the HUD strings' format).

## src/Mech3/MarkerRig.cs
A player airframe's weapon marker rig read from planes.zbd GameZ: `Extract` walks a `player_*`
root, accumulating locals down to each `firepoint*`/`pylon*`/`target`, and reports plane-frame
positions + co-located groups (two gun groups on one mount). `Format` prints one dump block per
plane; `PlayerAirframes` is the model→display list. The committed instrument `docs/formats/markers.md`
regenerates from, and the source of truth `--dump-markers` and `UI.MarkerOverlay` share.
`FindNamedMarker` is the sibling read for one non-weapon node by name (e.g. `cockpit_camera`,
`PlaneBuilder.CockpitCameraOffset`'s reader): the same accumulate-below-root walk, skipping
`cockpit1`/`cockpit2`/`destroyed`/`player_damage_off` so a plane whose interior/wreck carries its
own same-named node still resolves to the authored one in the top-level `markers` group.

## src/Mech3/CompiledAnim.cs
Reader for the fork's compiled `cam_anim`/`mis_anim` extraction (zip or dir): typed defs, events,
and the SI-script pool — `Script(index)` parses lazily, ordered by `metadata.json`. Decode facts
(ptr = flat node index, shifted quat labels, half-angle cubics): docs/formats/anim-definitions.md.
The `unknown_seq` destruction slot parses into `AnimDefinition.DeathSlot`, deliberately OFF
`Sequences` (bootstrap and the sequence-walking derivations never see it); only
`AnimRuntime.RunDeathSequence` dispatches it (`BL-276`, docs/formats/destructibles.md). The
`ACTIVATION_PREREQUISITE` node-state form (a run of `Parent` entries closed by an `Object` leaf
carrying `active_raw`/`required`) parses into `AnimDefinition.PrereqNodes` beside the anim-list
form's `PrereqAnims`; `AnimDefs` reads the reader spelling into the same list. ⚠ The leaf's state
is bit 0 of `active_raw`, never mech3ax's `active`: the word is two flags, bit 0 the
ACTIVE/INACTIVE list and bit 1 the def's LOCAL_NODES_ONLY scope (`FUN_0051d7b0`), so the 726
local INACTIVE entries (every `finish*gasbag*` panel finisher) compile as 2, which mech3ax reports
active. Read as active, the finisher never starts after its burn switched the panels off, and no
anim-authored zeppelin (C1/M04's `hk_zep`) can burn out and sink.

## src/Mech3/AnimDefs.cs
The zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into CompiledAnim's
`AnimDefinition` model (op key SNAKE_CASE→PascalCase IS the compiled tag; unclaimed bodies stay
under `raw`). Exists because compiled archives are incomplete: `zepstate`/`startanims` are reader-only.
Unit normalization happens HERE so handlers see one convention: reader rotations are DEGREES
(ROTATE_STATE, FROM_TO rotate, XYZ_ROTATION → radians), PLAYER_RANGE metres (→ m²), ANIMATION_LOD
tokens (→ numbers) — see docs/formats/anim-definitions.md.

## src/Mech3/AnimProgram.cs
Merges the compiled + reader front-ends for one mission — load both, prefer compiled on collision,
keep the remainder — plus `StartAnims`; `ScriptFor` resolves an event slot to its archive SI script.
The mission-scope gate against the compiled manifest (a library, not a full roster) and the
shared-scope FILE gate (`ListedSharedFiles`: a shared reader file loads only when the shared
`anim.zrd` index closure, the chapter's `cam_anim.zrd` or the mission's `mis_anim.zrd` names it,
reported as `SharedFilesSkipped`) are decode knowledge: docs/formats/anim-definitions.md "Mission
library scope". Both gates apply only with a compiled mission manifest present, so a reader-only
extraction is untouched. Pinned by the `mission-off-turrets` suite (C3/M03 loads no balloon def,
C3/M02 does). Which world ENTITIES a mission shows is MissionSetup
plus the interp boot script, not this file. `Defs`, `StartAnims` and `MissionLibrarySkipped` are
`IReadOnlyList` over private backing lists: one program is already shared by every runtime `Subset`
binds from it, and may be shared by several world builds (`DecodeCache`).

## src/Mech3/TextureCycler.cs
Runs the gamez material `cycle` flipbooks (water, surf, wakes, crowds) by swapping `albedo_tex`;
frames resolve at build time while the TextureArchive is open — an incomplete flipbook stays static.

## src/Mech3/EffectCycles.cs
The `EFFECTS` block of the shared `effects.zrd`: the second source of material flipbooks, and the one
that lights C1's refinery vent. An entry names a node but animates that node's MATERIAL, so this pass
resolves each entry (node → first mesh under it → surface 0's material) and writes the frame list
onto that `GameZMaterial` before the world build, leaving `SceneBuilder.RegisterCycle` to pick it up
unchanged. Two entries exist install-wide (`fire1.flt` 12@10, `fire2.flt` 6@5).

## src/Mech3/WorldSounds.cs
`SOUND_NODE` ambient looping 3D emitters: one pooled AudioStreamPlayer3D per live emitter,
following its host's pose per frame. `PlayOneShot(name, worldPos, rng)` is the one-shot `SOUND`
half: fire-and-forget destruction/impact audio, resolving a `SOUND_GROUPS` name to a member
first; the `Sound` anim event calls it. The `PlayOneShot(name, Node3D source, rng)` overload rides
the source's pose per Tick (a voice line from a moving aircraft; a freed source leaves it finishing
at its last position). `HasStream(name)` answers clip availability after the prewarm, which a def
alone cannot. Who hears these emitters is the pinned per-pane listener model (`UI/SplitScreen`);
`SetListeners` feeds the `--debug-anim` log alone, whose `dist` column names the NEAREST listener and
the pane it belongs to, because that is the pane whose volume wins the engine's mix.
`OneShotsStarted` (D33) counts every one-shot that actually started an `AudioStreamPlayer3D`, so a
suite can assert a cue fired by counting rather than grepping the `Debug`-gated log line.

## src/Mech3/WorldLights.cs
Packs the animated world's `LIGHT_STATE` point lights into the 2×N RGBAF texture the fullbright
world shader reads as spill (global `csky_light_data`, loop bounded by `csky_light_count`); the
uniform is session-global (a lit light is lit for every pane), but `Commit`'s 900–1500 m fade and
its `MaxActive`-slot significance rank both answer to the NEAREST of every viewer position handed
in, not one camera — a light beside player 4 stays lit even with player 1 far away (`BL-366`;
`AnimRuntime.LightViewerPositions`, fed from `GameSession`'s `ViewerSet`). One position (single
player) reduces to the pre-B13 rule exactly.

## src/Pads.cs
Single source of truth for gamepads — every reader goes through it, never `Input.GetConnectedJoypads()`.
Owns the phantom policy (span every pad, never `pads[0]`), `Disabled` (`--no-pads`), the focus gate.
`Connected` (the roster) and `For` (the input gate) answer different questions; see the class
remarks. `AssignPads` is the launch-time roster split, its leftover-pool rule for P1 covered by
the pure, engine-free overload in `PadsTests.cs`.

## src/Mech3/MissionSetup.cs
Parses + applies the per-mission `.gw` interp script that decides which world entities a mission
shows; acts on `NodeSetActive`/`DeleteTree`/`Object3DSetScroll`/`Object3DTranslate`/`Object3DRotate`/
`WorldPartitionSetActive`, counts + reports every other verb. The area verb takes two calls, not one:
`BindPartitions(gamez)` resolves its rectangles to gamez node indices through `WorldPartitionGrid`
while the gamez is in hand, and `Apply`'s `setActiveByIndex` delegate switches them in the built
world. Without the bind the verb is counted unapplied. Decode: docs/formats/interp.md.

## src/Mech3/ScriptedPath.cs
One authored waypoint path, resolved against the BUILT world through the runtime's own name
resolver rather than out of the gamez: the roster names `pp1`, the chapter carries the transform-only
subtree `pp1_aipath`, and its `pp1_aipN` children are the waypoints in ordinal order. Ten vehicles in
three missions carry one. A missing subtree, or fewer than two waypoints, resolves to null so the
caller reports it instead of inventing a route. Where the name comes from:
docs/formats/ai-rosters.md's `taxiPath` slot.

## src/Mech3/WorldPartitionGrid.cs
The world's spatial cell grid as a query: which gamez nodes does a world-space XZ rectangle cover?
Built from `GameZNode.PartitionCellNodes` (the per-cell membership `PartitionNodes` flattens away)
and the cells' own bounds. Its one reader is `MissionSetup`'s area verb. The rectangle is half-open
in cell space and the two axes run opposite ways; both are in docs/formats/interp.md.

## src/Mech3/AnimRuntime.cs
The animation engine: bootstrap passes (mission setup, anchored RESET_STATEs, ON_STARTUP,
startanims, a safety net), then dispatch-table event playback; an unhandled event kind is counted,
never fatal. `Start` refuses a definition whose REQUIRED node-state prerequisite is unmet
(`NodePrerequisitesMet`, the node's visibility against `AnimDefinition.PrereqNodes`), counted as
`Start(prerequisite unmet)` and otherwise silent like the hull-death gate: CM07's hangar drop calls
both of each camera leg pair and lets `hdrop_direction`'s state pick one. Optional entries and
`MINIMUM_TO_SATISFY` over nodes are parsed, not enforced. Also hosts the destructible-damage entries (`DamageAt`/`CollideDamageAt`/
`ApplyDamageStages`/`RunDeathSequence`/`ResetDestructible`) and the world-effects runtime
(`PlayEffectAt` over a hidden template stage). **A death's callee rides its call site.** The
world runtime's `CallAnimation` arm hands a death's effect call to the world-effects runtime
through `ExternalEffect` with the site's world point, the site node (the callee's `INPUT_NODE`)
and a follow flag that is set whenever the call resolved a site; `PlayEffectAt` then places the
pooled copy with `TemplateStage.PlaceFollowing`. The un-routed half (a library-root callee such
as `dblcannon_flying_parts`, placed by the world runtime itself) takes the same follow while
`_deathCallDepth` is open, through `PlaceFollowing` or `PlaceAt(follow: true)`; the debris pieces
integrate in the root's frame (`MotionRuntime`), so re-placing the root each frame carries their
flight along with the hull. `AT_NODE` and `WITH_NODE` are not told apart here: at the original's
controls a zeppelin gun ring's whole death (its `large_30sec_fire`, its `AT_NODE` fireballs and
its flying parts) moves with the hull as it flies on, and the decode does not contradict that
(`docs/org/sequences.md`, the CALL_ANIMATION section). A world-fixed site reads identically
with or without the follow, and relocating calls outside a death (a damage stage's panel tear on
a flying aircraft) keep their one-time placement. The emitter itself is untouched:
`EmitterDirector.Tick` reads its host, the placed copy's node, which the follow has already moved
that frame. Regression: `ring-death-effects-follow-hull` (a real C1C/M01 piratezep ring killed on
the hull, then the hull moved as `ZeppelinRuntime.Place` moves it: the routed fireball's fed
position and the flying-parts copy's root both move by the ring's displacement, the piece adding
only its own few metres of flight) and `turret-death-fire-follows-hull` (the C1/M04 ring's routed
fire), beside `turret-death-effect-world-anchor`, which covers the un-routed `PUFFER_STATE` on the
world runtime. **A pool-slot checkout returns its copies to their
spawn state** (`ResetCheckedOutCopies`, run by `PlayEffectAt` between `TakeNextSlot`/`PlaceOn` and
`Start`): for every def the played anim reaches through CALL_ANIMATION (`AnimProgram.Subset`, memoized
per anim name), on the anchors sitting in the slot(s) the call took, it takes the three steps
`ResetDestructible`'s `ResetCalled` takes — `Stop` the instance, `RestoreRestPoses`, then re-apply the
RESET_STATE. The original instances a fresh template copy per call, which always starts from its
authored base pose; the pool hands the same node tree out again, still in whatever END pose its last
run left and with its last run's motions still driving it. Both halves matter. A def whose sequences
end INACTIVE (the sonic burst's `ring_up1..4`/`ring_down1` on `sonic_ring1..5`) played once per slot
and drew nothing from the wrap on without the RESET_STATE; a slot recycled while still live
re-launched its debris from a MID-FLIGHT pose without the stop and the pose restore, so the HE
burst's `fly_trail1..5` walked further out on every wrap. ⚠ Stopping is what makes the restore
stick — the incumbent motions would otherwise overwrite the restored pose the same frame.
`RESET_TIME -1` is "never self-reset while playing", not an exemption from this. Scoped to the call's
own closure and its slot, never the whole `pool<N>` container: another effect live on the same slot
number must not be re-posed under its running motions. The authored pose the restore reads is banked
for a whole pooled copy at `IndexPooledCopy` (`PrimeRest`), as-built, rather than left to `RestOf`'s
lazy capture on the first thing that moves a node — on a reused copy that first mover has already
displaced it. Regressions: the `effect-pool-reset` and `effect-pool-spawn-pose` suites.
**`PrewarmEmitters(params Node3D[]
callSiteAnchors)` builds every emitter the bound program's `PUFFER_STATE 1` events can name before
anything plays**, through `EmitterDirector.Prewarm`: a named `at_node` resolves to every node of that
name in the runtime's scope, unfiltered, since a play anchors the def inside its CALLER's pool copy
and the staging-admission rule with no scope rejects exactly those copies; an `INPUT_NODE` host
resolves to every node a `CALL_ANIMATION` in the program targets the def with, the def's own staged
copies, and the anchors handed in. It starts and poses nothing. Both the crash rig and the
world-effects runtime call it at bind (`WorldEffectsFactory.BuildFlightCrashRuntime` and
`BuildWorldEffectsRuntime`), never the ambient world runtime. Regression: the
`emitter-prewarm` suite. `Play`/`PlayWithin`/`StopWithin` start a def's
instances by anim name, the latter two scoped to one subtree (a NAME can repeat across a chapter,
e.g. C1's three `hangerdoors`). `OBJECT_ADD_CHILD`/`OBJECT_DELETE_CHILD` take their node-reparent
form here (`Reparent`, keeping the LOCAL transform) once the sound-emitter form has declined,
which is how a cutscene composes its camera inside the node it frames; the decode is
`docs/formats/anim-definitions/cutscenes.md`. An adopted child that is a staged library copy
(TopLevel from `PlaceNodeAt`) is un-pinned and set back to its authored rest pose in the parent's
frame, so CM07's passenger rides the caboose instead of hanging where the call site was; and an
add-child dispatched from inside a staged copy (`_stagedCopies`, filled by `IndexPooledCopy`) may
lazily build its library-root child the way a ranged or mission call may, since a staged actor's
own choreography (the passenger's wave loop, and the flare it puts in its hand) runs on ordinary
ticks long after the call that staged it. Regression: the `landings-train-pickup-ride` suite. A ranged startanim with an unplaced immediate callee is
deferred until range; range-triggered and explicit mission-trigger calls (`PlayMissionTrigger`: the
`landings.zrd` rows and the objective script's `WAKE_ANIM`) may then lazily build their
library-root callees, and an add-child may do the same for an explicitly named child — except where
the call's own `AT_NODE` site IS the callee's root node, which names the node to run on rather than
asking for a copy. The authored site is passed on to the pool only when the call actually names
one, so a site-less call keeps one copy per anchor and reaches no staged actor at all
(`Mech3/WorldSession.cs` has why CM07's hangar drop needs that). The mission-trigger right is remembered as the started definition's whole call
closure, not as a call-stack depth: a cutscene's later beats run off delayed sequence events seconds
after the trigger's own dispatch has returned (CM02's crew is thrown out fourteen seconds in) and
instance their library roots exactly as the beats inside it do. `MissionTriggerOwner`, called at the
top of the same method, is the cutscene trigger slot; `Session/CutsceneController.cs` has it. `IndexSpawnedVehicle` is the other half of that resolution: it makes one live
aircraft answer for the gamez library-root vehicle node its roster block was spawned from, by name
and compiled index, which is what a cutscene posed `AT_NODE` that vehicle needs
(`Mech3/RosterMarkers.cs` is the caller). It also keeps three things beside that rig which the
resolver's shared index must never hold: the rig's own parts by gamez name, so a definition
ANCHORED on the vehicle reaches the parts a capture animates (`Targets`' second narrow rescue —
the shared airframe spells them `pilot`, `body`, `healthy`, and indexing those globally would let
any definition claim them); the rotation the roster placed it at, which is what an `AT_NODE_XYZ`
rotate reads off an aeroplane (`PlacedRotationOf`, decode in
`docs/formats/anim-definitions/cutscenes.md`); and the engine-only nodes between the rig root and
the airframe it draws, so an `OBJECT_ACTIVE_STATE` addressed to the vehicle reaches the aeroplane
rather than the rig node alone (`SetTargetActive`, which is how a capture holds its subject drawn
against the cutscene's own AI park). The range
sweep (`TickDeferredByRange`) runs only when a player crosses an 8 m check cell, and it measures
each anchor's range origin (`VisualOriginOf`, a mesh-bounds walk) once, carrying it in the anchor's
own frame (`_rangeOriginLocal`) from then on: at cruise the sweep runs every few frames, and
re-walking every deferred anchor's subtree per sweep allocated tens of MB/s of finalizable Godot
wrappers, which is what fed the periodic gen1 collection pause.
Every construction site hands over a sealed `TemplateStage`
(`NewTemplateStage`/`ForEffects`/`ForCrashRig`). Sibling modules, each with its own entry: the
sequence interpreter is `SequenceRunner.cs`, live motions are `Anim/MotionSet.cs`, name resolution
is `Anim/NameResolver.cs` (this class forwards through `Resolve`/`ResolveScoped`/`Anchors`), puffer
emitters are `Anim/EmitterDirector.cs`, ambient/one-shot sound is `Anim/SoundChannel.cs`, point
lights are `Anim/LightChannel.cs`, the object-pose/visual family is `Anim/PoseChannel.cs`, and the
effect-template pool/placement is `Anim/TemplateStage.cs`.
The router keeps the `SOUND_NODE`/`SOUND` case labels and the sound reach-ins inside
`OBJECT_ACTIVE_STATE`/`OBJECT_ADD_CHILD`, delegating every body to `Sound`; `Sounds`/
`SoundHandledElsewhere` stay public fields here, since callers configure them, and `Sound` reads
both live rather than snapshotting them. The router also keeps the `LIGHT_STATE`/`LIGHT_ANIMATION`
case labels, delegating every body to `Light`; `Lights`/`LightViewerPositions` stay public fields
here for the same reason, and `Light` reads both live. The nine `OBJECT_*` pose/visual case labels
delegate to `Pose` the same way, each adding the handler's returned op count to the census counter;
the `_rest` pose table stays here, since the death flow (`RestoreRestPoses`/`ApplyDeathSwap`) reads
it too, and both the family and the motion value types reach it only through `RestOf`.
`CALLBACK` offers each code to `CallbackHost` first (the mission-script host, given the raising
definition's animation name and its root node name, `ANIMATION_ROOT_NAME` before the anchoring
`NAME` — `Session/CutsceneController.cs`), then raises the two vehicle-death
codes through caller-supplied seams (`WreckVelocity`, `StopDamageStages`) and counts every other
one; decodes in `docs/org/vehicleDamage.md` and
`docs/formats/anim-definitions/cutscenes.md`.
`FBFX_COLOR_FROM_TO`/`LIGHT_ANIMATION` report their `run_time` as the
event's duration, spacing a chain instead of firing it in one instant; decode in
`docs/formats/anim-definitions.md`.
`PLAYER_1ST_PERSON` (condition 120) answers off the `FirstPersonView` seam, polled per
evaluation: the session hands over "any human pilot is in Cockpit or Nose"
(`GameSession.AnyPilotFirstPerson` off each rig's `FlightController.FirstPersonView`), and a
runtime with no seam wired — a lab, a test, the bootstrap before any rig exists — reads false,
which is what this condition answered everywhere before the view modes existed. Regression: the
`first-person-condition` suite, over the shipped `bullet1` def.
`SyncDestructiblePool` keeps a destructible's `DestructibleRegistry` instance in step with an
`OBJECT_ACTIVE_STATE` healthy/destroyed role swap dispatched outside `DamageAt`'s own kill — a
start-state script or an ON_STARTUP sequence authoring an object destroyed before the player
arrives. Called from the `ObjectActiveState` dispatch case right after `Pose.HandleActiveState`,
it matches the same role-name convention `AuthorsSwap`/`ApplyDeathSwap` already use and writes
`Health`/`Status` only when they still disagree with the swap direction, so a live kill's own
dispatch (which already set the pool via `DamageAt` before running the death sequence) is a
no-op there. Bootstrap's Pass 1 registers each anchored destructible in `_destructibles` BEFORE
dispatching its `RESET_STATE`, not after, so a def authored to start destroyed (its own
`RESET_STATE` puts the destroyed role ACTIVE) reaches this same sync with a pool already there
to find; no shipped def uses that shape today, but the ordering is the general contract every
other `OBJECT_ACTIVE_STATE` dispatch site already follows. Regression: the
`start-state-swap-pool` suite.
`CarryState` is the other direction, the persist log's: it writes the pool first (`Health`,
`Status`, `DamageStage` from the def's `DAMAGE_SEQUENCE` thresholds) and then, for a carried
kill, `ApplyDeathPose` puts the nodes where the death would leave them without playing it. The
pose is read off the sequences `RunDeathSequence` would play (the def's Initial sequences, its
compiled destruction slot, a chained swap target's sequences): every `OBJECT_ACTIVE_STATE` that
switches a node off, plus the destroyed/`dbase` role nodes switched on; a non-role piece
switched on mid-death is debris that flies and is hidden, so it stays off. A def with no role
swap in those sequences takes the RESET-derived `ApplyDeathSwap`, withheld when the def authors
its own visible death, the same rule the live kill applies. Shipped `DAMAGE_SEQUENCE`s carry only
`CallAnimation` puffer calls (and three `StopAnimation`s), so a carried partial HP lands as the
stage counter alone and no stage burst plays. Regression: the `carried-state-silent` suite.

## src/Mech3/Anim/
`AnimRuntime`'s private nested types promoted to top-level `internal` types in their own
namespace, purely for file size — not an independently-owned subsystem, still driven entirely by
`AnimRuntime`. `IAnimMotion` (`ScriptPlayback`/`SpinMotion`/`FromToMotion`/`OpacityFade`/
`MotionRuntime`), `AnimLight` (owned by `LightChannel` below), and the bind-census `AnchorKind`
enum. `SpinMotion.ComposeSpin` is
the one member reached from outside this namespace without going through `AnimRuntime` at all —
`Flight/PropAnimator.cs` calls it directly so a plane's own props spin through the identical
accumulate-from-rest decode instead of a second hand conversion; it takes a rest `Basis` and a
rate, no `AnimRuntime`/`MotionSet` state, so the reach-in is inert to everything else here.
`MotionSet`, `EmitterDirector`,
`SoundChannel`, `LightChannel`, `PoseChannel`, `NameResolver` and `TemplateStage` share the
namespace but ARE independently owned — their own entries below.
**The original's `OBJECT_MOTION` update is written up in [org/objectMotion.md](org/objectMotion.md)**
— the function map, the flag word, the linear elevation, `delta` as an acceleration, both contact
tiers and how they pick a surface, the landing response, the termination model, and the retired
readings (the spherical elevation, the ÷`run_time` tumble, `DebrisTune`, `no_altitude` as a second
terrain test). Read it before changing a mechanism here; only what this engine adds is below.
`MotionRuntime`'s launch seeds from the node's LIVE pose and a chain of events on one node
continues leg from leg (org/objectMotion.md, "A launch starts from the node's live pose"); what
keeps a repeat from compounding is the reset and checkout bookkeeping, never the launch.
`RangeLaunchDirection` is the launch decode's ONE
expression and `TumbleAxis` the tumble's; `ProjectilePool`'s gun-casing ejection reads the same
`gunshell` event through both (INSTR-3), because two spellings of the maths is how they disagree —
see their doc comments in `Anim/MotionRuntime.cs` for the non-normalisation rule.
Contact is the DEFAULT and comes in the original's two tiers: `TryGroundColumn`, a vertical column
under the body, unless `do_intersections` upgrades it to `TryContact`'s trajectory sweep (166 events
install-wide); `no_altitude` vetoes the column only, and `gunshell` alone authors it. No mask wired
means neither tier, which is the structural fallback every lab and 9 of the 14 goldens take;
`c1-debris-rest` is the one golden that wires a mask and reaches the column tier, a killed
`m_build03` piece resting with its landing's own spark puffer as the pixel-level tell. Both
end on one shared response. `MotionRuntime` separates the duration it REPORTS (`RunTime`) from the
ceiling that ENDS it (the original's watchdog, or `RUN_TIME`); see `RunTime`'s and the watchdog
constants' own doc comments for the split and why it must not collapse.
`FromToMotion`, `NonSingularScale`, and the `AnimRuntime` members these motion types reach into
(`RestOf`, `_rng`, `SetSubtreeOpacity`, `NameOf`, `VisualOriginOf`) carry their own rules and
visibility rationale on their declarations — read those before touching either file.

## src/Mech3/Anim/MotionSet.cs
`AnimRuntime`'s live motions as a module: `Add` (owner stamp + `(Target, Channel)` eviction +
`LaunchCount`), the per-frame `Tick` sweep, `DiscardFor`/`Reset`, and the two predicates the rest of
the runtime asks — `OwesBounce` (the retirement hold `AnimRuntime.Retirable` consults) and
`HasSpinOn` (the `Loop{-1}` spin re-assert guard). Never constructs a motion — `PoseChannel` builds
them and hands them over. `Tick` drops a motion whose target node has been freed before touching it: a motion outlives the
node it drives (an airframe swap, a rig torn down), and writing a transform to a disposed object
throws out of the whole runtime advance rather than losing one motion. `Node3D`-typed and otherwise
never dereferenced: every other operation here is identity comparison, so the behaviour is
engine-free even though the type is not — the in-engine `bounce-launch` suite is what an off-engine
fake cannot cover.

## src/Mech3/Anim/EmitterDirector.cs
One runtime's `PUFFER_STATE` emitters as a module: `Assert` (start / revive / re-home), the four
stops (`End`, `EndOn`, `EndFor`, `Discard`), `Reset` (the crash rig's respawn), the per-frame `Tick`
follow, and `Census`. `AnimRuntime` keeps only the dispatch case, the `at_node` sentinel resolution
and the `active_state` read. One director per runtime; `IEmitterFactory` is what builds (`Puffer`,
`TextureArchive` and the parent node sit behind that seam), so a suite can install a fake. The
selector/disposition split across the four stops, the emitter-keying tradeoff and the stop-family
history live in this file's own doc comments, not here. `Prewarm` builds the emitter for a key ahead
of any assert and leaves it unclaimed: the first `Assert` on that key takes it over (ownership, the
start and the `Built` count) exactly as if it had constructed it, the owner-selected stops pass an
unclaimed entry over, and `Prewarmed` counts what was built ahead. `Reset` keeps every emitter whose
host node still exists, returned to that unclaimed state, so a respawned rig's next crash builds
nothing; only an emitter whose host is gone is destroyed.

## src/Mech3/Anim/SoundChannel.cs
One runtime's `SOUND_NODE`/`SOUND` events as a module: `HandleSoundNode` (declare/place/start the
pooled ambient emitter), `HandleSound` (the one-shot destruction/impact player, positioned by
`OneShotSoundPosition`), the late-failure census (`ReportLateSoundFailure`, gated on
`MarkCensusPrinted`), `Reset` (the crash rig's respawn) and `DiscardFor` (the teardown reach-in,
keyed by anchor like lights). `AnimRuntime` keeps the `SOUND_NODE`/`SOUND` case labels and the two
sound reach-ins the router still owns outright — `TrySetActive` for the `OBJECT_ACTIVE_STATE` case
(an ordinary node event whose NAME can turn out to be a sound emitter instead) and
`TryGetChild`/`Attach` for the sound-emitter three-quarters of `OBJECT_ADD_CHILD`. `Sounds` and
`SoundHandledElsewhere` stay public fields on `AnimRuntime`, since callers configure them (and
`SoundHandledElsewhere` differs between the world and effects runtimes sharing one world); the
channel reads both through closures rather than a constructor snapshot, since `Sounds` goes
non-null only once the world build finishes. `OneShotSoundsPlayed` is a one-line forward from
`AnimRuntime` to the channel's own counter.

## src/Mech3/Anim/LightChannel.cs
One runtime's `LIGHT_STATE`/`LIGHT_ANIMATION` events as a module: `HandleLightState` (the partial
update that declares a light on first use and never defaults an absent field), `HandleLightAnimation`
(the signed-delta tween over `run_time`), `Tick` (the per-frame tween advance and submission to
`WorldLights`), `Reset` (the crash rig's respawn) and `DiscardFor` (the teardown reach-in, keyed by
anchor like sound emitters). `HandleLightState`/`HandleLightAnimation` report whether they applied
through their return value rather than reaching for an `_opsApplied`/unhandled-count callback
directly; `AnimRuntime` applies both after the call, matching what the handlers always did inline.
`Lights` and `LightViewerPositions` stay public fields on `AnimRuntime`, since callers configure
them; the channel reads both through closures rather than a constructor snapshot, folding
`LightViewerPositions`' single-camera fallback (`PlayerPos`) into the same closure. `AnimLight`
stays its own value type in the `Anim` namespace, constructed only by this channel.

## src/Mech3/Anim/PoseChannel.cs
One runtime's object-pose/visual events as a module: the nine `OBJECT_*` handler bodies
(`ACTIVE_STATE`'s non-sound remainder, the three `*_STATE` poses, both opacity events and the three
motion events), the pose helpers (`PoseTranslate`/`PoseRotate`/`PoseScale`, which mission setup's
pass 0 also drives), the subtree opacity/fade machinery (the per-root opacity cache, the fade-twin
material tables, `SetSubtreeOpacity`), and the landing-resume marks
(`ConsumeLandingResume`/`MarkLandingResume`). It carries the motion-BUILDER role: it parses the
motion events into `MotionRuntime`/`FromToMotion`/`SpinMotion`/`ScriptPlayback`/`OpacityFade`
instances and hands them to `MotionSet`, which stays a pure live-set container; the tick spine
stays in `AnimRuntime.Advance`. Every handler returns how many ops it applied and the router adds
that to its census counter, the same return-value shape `LightChannel` uses; multi-key unhandled
tallies go through an `Action<string>` count dependency instead, since one return value cannot name
them. A mission-triggered SI script retains its authored duration when its named cross-archive actor
is absent, so the rest of that cutscene cannot collapse to time zero; ordinary ambient misses remain
zero-duration. The `ROOT`/`ALL_NAMES` form (`HandleMotionSiScriptAllNames`, the skeletal person and
ladder scripts) plays the `motions` list `AnimDefinition.Parse` decoded off the raw payload as that
many single-node scripts started together, and reports the longest as the event's run time. The channel's constructor takes `AnimRuntime` itself as one dependency — the motion value
types already declare it as their host argument, and the pose helpers reach the `_rest` table
through the same `RestOf` seam the builders use — plus the `Targets` resolver, the `MotionSet`,
and closures over `Emitters` and the program's `ScriptFor` (both late-bound). `_rest` itself stays
on `AnimRuntime`, read by the death flow; `AnimRuntime` keeps thin internal forwards for
`ConsumeLandingResume`/`MarkLandingResume`/`SetSubtreeOpacity`, whose callers (`MotionRuntime`,
the `ground-contact` suite, `OpacityFade`) name the runtime. No teardown reach-in exists: none of
this family's state is per-instance the way emitters, lights and sounds are.
The `AT_NODE` form of the translate and rotate poses (`PoseAtNode`) takes another node's world
frame with `state` as an offset inside it, rather than an absolute pose; the host is resolved by
name over the whole index because it is a root of its own, not something the event's anchor
contains. It reads the host's frame through `AnimRuntime.WorldTransform`, so a bootstrap-time pose
(the world root not yet parented) still composes correctly instead of reading Godot's identity
fallback, and writes the target's LOCAL transform when the target itself is out of tree.
`HandleRotateState` reads the rotate's host under either spelling, and the two are different rules
rather than two names for one: `AtNodeMatrix` takes the host's composed orientation, `AtNodeXYZ`
the rotation the host was last SCRIPTED to (`PoseAtNode`'s `scripted` arm, off
`AnimRuntime.PlacedRotationOf`), which is why a wing-walk frame posed off an aeroplane stays level
however the aeroplane is banking. Both carry `state` in radians by the time they reach here.
Spellings, addresses and census: docs/formats/anim-definitions/cutscenes.md.

## src/Mech3/Anim/NameResolver.cs
Name→node resolution as one public module, generic over the node type (`NameResolver<TNode>`): the
index, the wildcard `Matcher` (`*` any run, `#` a digit run, case-insensitive, `.flt` suffix match),
the memoized `FindAll`, the scoped tier chain (`Resolve`/`ResolveScoped`), the symbol authority
(`SymbolClaims`/`NarrowToSymbolRoot` — the `air_gen`/`eairg31` cross-bind fix), `Anchors` (NAME
match → symbol narrowing → root lift, policy inputs `NameResolveFallback`/`SuppressRootLift`/
`MaxRootLift`), and the bind census (`OpenCensus`/`CloseCensus`, `ResolutionLines`). Node identity
is constructor-supplied (`IEqualityComparer<TNode>`; the engine keys on `GetInstanceId()`), never
the node type's inherited `Equals`. Nothing removes a row, so `FindAll` and the by-index map both
skip a node the caller's liveness test rejects and the map gives that index up to the next `Add`:
an airframe swap frees the aircraft it staged and re-stages the same cross-archive block. A claimed
index the build never created is answered by `SoleStagedCopy`: the one live pooled copy carrying
that gamez index, and only when exactly one does, so a multi-copy effect pool stays anchor-scoped
while a mission's single staged actor (CM07's pickup switch, sensor and passenger) is reachable by
the definitions that toggle it through their symbol table alone (`pickup_timing`). A claim made
from inside a staged library copy (the `privateCopyOf` hook, `AnimRuntime.StagedCopyRootOf`) is
narrowed to that copy: a bound node outside it yields to the copy's own node of that name, and a
name the copy lacks keeps its binding. That is the private-copy rule applied to the symbol path,
and it is needed because a cross-archive symbol table binds by index: CM07's `pickup_flare`
claims the aircraft archive's `cp_lh`, and once `AircraftStage` has parked that figure the
by-index map answers with the parked figure's hand instead of the passenger's, which is where
the definition runs. `AnimRuntime`'s `Resolve`/`ResolveScoped`/`FindAll`/`Anchors`
are one-line forwards; the engine-free instantiation over a plain token type is `CSVM.Tests`' suite.

**Every tier is filtered by `AdmissibleStaging`, and the template pool is why.** The original
resolves a node name inside a subtree the definition was given a private copy of at load
([org/sequences.md](org/sequences.md), "The definition owns a private copy of its subtree"), so an
unrelated instance's copy of a common name (`pilot`, `geometry`, `healthy`) can never answer first.
Our stand-in is the effect-template pool, whose copies hang under the same crash root a definition
anchors on, so the raw subtree walk is not exclusive at all. The `stagingAdmits` hook is the owner's
verdict on one pooled copy (`AnimRuntime.StagingAdmits`): a copy is visible when the scope this tier
searches sits inside it (a `CALL_ANIMATION` retargeted onto its call site's copy), when its root
answers to the definition's own NAME or `ANIMATION_ROOT_NAME`, or when the definition's symbol table
names that root. Everything outside the pool always resolves, and on a non-pooled runtime nothing is
refused, which is what keeps the ambient world boot byte-identical. ⚠ The filter belongs on every
tier: applied to the first alone it only hands the same foreign copy to the next one down.

## src/Mech3/Anim/TemplateStage.cs
The effect-template stage as one module (`TemplateStage<TNode>`): pool-slot arithmetic (`SlotOf`,
`TakeNextSlot`, `RootsFor`, the `AssignCallerSlot` caller-slot claim), template placement
(`PlaceAt`/`PlaceOn`, and `PlaceFollowing`, also reached as `PlaceAt(follow: true)`, for a copy
that must keep riding a moving call site, a death's callee on a carried node:
the root is placed once and then re-placed by `FollowSites`, every frame from `AnimRuntime.Advance`
before the emitters read their hosts, at the site's live pose plus the offset the placement had in
the site's own frame; a fresh `PlaceOn` of that root, a hide through `Reveal`, or a freed site ends
the follow, and `Following` counts what rides), the copy-identity questions (`IsAt`, `RootsOf`, `SharedWithLiveInstance`),
the pooled-copy staging entry (`IndexPooledCopy`), `Recycles`, and the reveal/retire/sweep ritual
(`Reveal`, `RetireWhenIdle`, `Sweep`). The stage's own reset pass (`applyResetStates`, wired from
`AnimRuntime.ApplyResetStatesWithin`) runs once per copy, when it is staged; a copy `TakeNextSlot`
hands out is returned to its spawn state by the runtime on every checkout
(`AnimRuntime.ResetCheckedOutCopies`, its entry above), because the copy is a reused node tree
standing in for the original's fresh instance per call. `IndexPooledCopy` is also where that spawn
pose is banked, while the copy is still as-built. Carries the three template policy flags as sealed
constructor state — `Pooled`, `Shown`, `Places` — get-only, no setter anywhere. Generic like
`NameResolver<TNode>`: engine hooks at construction, the runtime-dependent hooks (`findAll`,
`anchors`, `isLive`, live instances, …) late-bound via `Wire` at the handover, since the factory
that builds the stage exists before any resolver does. Off-engine charter:
`AssignCallerSlot` claims per (template root, call anchor, authored call event) and hands a repeat
site its own copy to start on, because instance identity is (def, anchor) and two live calls on one
anchor need two anchors. Off-engine charter:
`CSVM.Tests/TemplateStageTests.cs`; `effect-template-mesh`/`effects-census`/`damage-template-pool`/
`repeat-call-slots` are the in-engine integration tier.

## src/Mech3/SequenceRunner.cs
The engine-free sequence interpreter, extracted from `AnimRuntime` behind the `ISequenceHost` seam
(four members since `BL-228` added `PendingWait`; the other three are unchanged).
`SequenceRunner` runs one sequence's event list on a clock — **two** clocks, in fact: its own, and
the owning `AnimInstance.Clock` that a `START_TIME ANIMATION` gates against (the original's
`anim+0xb0`, one per definition instance and shared by all its sequences). The two differ for every
sequence a later CALL_SEQUENCE starts, which 191 shipped events read; a null `start` encodes as
`Animation + 0.0` but must stay on the relative path, and `SetDue`'s comment says why.
A runner is done only when its cursor is past the last event AND that event's run time has elapsed
(`_base`), the original's "still running until the run time is up": a sequence ending on an SI
script keeps its instance live for the script's length, which is what a caller's
`WAIT_FOR_COMPLETION` on CM07's `caboosepickup` holds on. Its scope is per-event START_TIME gating, LOOP with
authored-count-0 = infinite, and IF/ELSEIF/ELSE/ENDIF via a `_branchTaken` stack + a deliberately
**non**-nesting-aware `Scan` — the original counts no depth, and 48 shipped `gunhit` sequences
observe the difference; the constraint and its one residual live in `Scan`'s own comment).
**The original's sequence runtime is written up in [org/sequences.md](org/sequences.md)** — the
function map, the three START_TIME origins, the LOOP's pass counter and 60 Hz frame denomination,
and the clock-carry a timed loop needs; the authored side stays in
[formats/anim-definitions.md](formats/anim-definitions.md).
`AnimInstance` holds **one slot per `Def.Sequences` entry and walks them ASCENDING**, mirroring the
original's per-definition sequence array, plus an unslotted list for runners the definition does not
list (the death slot, the damage-stage host), which the original likewise keeps off the array and
steps outside the walk. The walk order is behaviour, not housekeeping: `CALL_SEQUENCE` writes the
callee's own slot, so a call runs in the same tick exactly when the callee is declared AFTER the
caller and waits a tick when it is declared before — 99.5 % of the install's calls point forward.
One slot stepped at most once per pass is also why no same-tick recursion cap is needed.
It carries the CALL_SEQUENCE/STOP_SEQUENCE semantics (decode in `docs/org/sequences.md`;
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
explosion's chain). The stop is also a DISABLE for the life of the instance: `AnimInstance` keeps
the set of sequences a stop has named, and `CallSequence` refuses a call into one of them, which is
what `004eb570`'s parked-only start does to a sequence `004eb610` wrote DONE. 123 definitions name
one sequence in both a call and a stop, and four reach the refusal: C1's `car_loop1_start`,
`car_go_home_start`, `hauler1_start` and `truck1_start`, whose lap loop calls a dust or exhaust
sequence, stops it later in the lap and calls it again on the next lap, so the called sequence
runs on the first lap only (the `stop-sequence` suite drives `car_loop1_start`). The other 119
(`flame_light_seq` in the destroy defs, `chuteman_drop`/`chuteman_sway`, the `sail_splash*`/
`yacht_splash*` sets, `warhawk`'s `smokepuff1..3`) stop after their last call, so the refusal
never fires for them; `RefusedStoppedCalls` is the counter and the `anim: CALL_SEQUENCE … refused`
log line names any new one. A restart builds a fresh instance and clears the set. Both are
public so `CSVM.Tests` drives them against a fake host; the host is any `ISequenceHost` (the game's
real one is `AnimRuntime`, tests pass a recorder). Anchors are opaque `Node3D?` pass-through — the
interpreter never dereferences them.
The up-counting `_loopPasses` mechanism, the `goto case "Elseif"`, and the 256-fires-per-frame guard
all LOOK refactorable and are all load-bearing (each a shipped, measured bug); the `Loop`/`Advance`
code comments in `SequenceRunner.cs` carry the measured evidence — read them before touching any of
the three. `WAIT_FOR_COMPLETION` (`BL-228`) is the seam's fourth member, `PendingWait`: a
`Func<bool>?` predicate (not a duration — the callee's own length is not knowable at the call) the
host arms during `Dispatch`, and the runner polls each advance until it reads false; the hold gates
only the sequence's next event, never the runner's lifetime — see the code comment where `Advance`
installs it. `OnEventDispatched` being a get-only nullable delegate on the seam is the
same shape: the null-conditional short-circuits the whole `EventDispatch` construction when no
debugger is attached, the zero-cost contract `ISequenceHost.OnEventDispatched`'s own doc comment
states.

## src/Mech3/DestructibleRegistry.cs
Live, mutable per-instance HP for the world's destructibles — any `AnimDefinition` with
`HEALTH > 0`. One `Instance` per `(def, anchor)` pair, seeded from the authored `HEALTH`, plus a
coarse healthy/damaged/destroyed `State` and a monotonic `DamageStage`; built during AnimRuntime's
bootstrap, read by `ANIM_HEALTH` eval, escalated by `ApplyDamageStages`, damaged via `DamageAt`.
`Resolve(struck)` maps a raycast-hit node back to its instance. Schema: docs/formats/destructibles.md.
`Instance.Reseed(max)` re-seeds a pool from a mission record — the F18 zeppelin zones, where
`zeppelins.json` hp beats the def's own `HEALTH` — and refuses once damaged, so a late wire-up
cannot heal a fight in progress. `Instance.Team`, `Instance.Owner` and `Instance.Dormant` are what a mission
record can put on a pool: an owning side where the data names one (`ZeppelinRuntime` is the only
writer of either today, and a pool with no team is neutral, so nobody's target), the name of the
entity the pool is a PART of (a zeppelin's zones carry their hull's name, which is the only thing a
`rating_biases` pattern naming the airship can match), and "registered but not in the world yet",
which `AimCandidateSet.AddStructures` refuses outright.

## src/Mech3/WavFile.cs
Pure-C# WAV parser with an MS ADPCM→PCM16 decoder (`DecodeMsAdpcm`), no Godot dependencies —
Godot cannot load the game's WAV format (see `docs/formats/sounds.md`).

## src/Mech3/SoundArchive.cs
WAV lookup over a soundsh/soundsl extraction (zip or dir), decoded through `WavFile` into cached
`AudioStreamWav`s; `Find(name, looped)` marks the stream as a forward loop when asked.

## src/Mech3/MusicPlayer.cs
The state-driven score: one non-positional streaming channel beside `WorldSounds`' pooled 3D
emitters, so the menu, the cabin and the mission director all drive the same track. `Enter(state)`
cues the sound-group or definition name the original's data names for that state; `Cue(name)` is
the raw form for a name the data supplies directly. One track at a time, hard cuts, no crossfade;
`NoteCombat` + `Tick` run the 20-second battle hold and its fade. The selection rules, the fade
rates and the tracks that ship with no trigger are `docs/org/music.md`.

## src/Mech3/MissionRadio.cs
The mission radio queue: the third playback channel, beside `MusicPlayer`'s streaming track and
`WorldSounds`' pooled 3D emitters, and the one a mission's objective callouts speak on. `Cue(name)`
takes a queued radio definition or a VO dialogue chain and returns how many lines it will speak, 0
for a name this channel does not own, so the caller can fall through to the channel that does.
One call speaks at a time: a chain runs its lines back to back as one call, a later cue queues
behind rather than cutting in, `Cancel` is `STOP_QUEUED_SOUNDS`, and a call waiting past its
definition's `QUEUE` tolerance is dropped unheard. Streams come from `WorldSounds.StreamFor`, so
the world's prewarm is what makes a callout survive the sound archive closing. The 1 s cue delay is
`docs/formats/objectives.md`; the definition classes and the tolerance are
`docs/formats/sounds.md`.

## src/Mech3/SoundDefs.cs
sounds.json SETS parser: `snd_*` name → `SoundDef` (wav name, flags, range, volume); the entry
grammar and flag/key meanings are in `docs/formats/sounds.md`. `LoadGroups` parses the sibling
`SOUND_GROUPS` block into `SoundGroup`s — the weighted random destruction/impact sounds a one-shot
`SOUND` event resolves through (`air_mixed_exp_sg` → `snd_exp_hit*`).

## src/Mech3/CombatVoice.cs
The combat-voice resolver (`docs/formats/combat-voice.md`): roster `accentID` (slot 65) →
`voice.zrd` row (the ACCENT table, 35 rows) → pilot VO id pool → clip defs. `PlayableFor(voId,
family)` returns the one name to hand `WorldSounds.PlayOneShot`: the shipped
`snd_<FAMILY>-A_id<N>_random` variant group when authored (466 are), else the bare def (the 12
bearing tokens). `SessionPrewarmNames` is the flight session's mission-roster prewarm set
(`GameSession.BuildWorldStage` → `WorldSession.Options.VoiceClipNames`, CLI accents joined via
`extraAccents`). E16's dispatch sits above this seam: `Flight/AiVoiceDispatcher.cs` (rules) +
`Session/AiVoiceRuntime.cs` (wiring), never in it.

## src/Flight/WeaponDefs.cs
Typed reader over the shared `weapons.zrd.json` `BALLISTICS` block — 48 `WeaponDef`s (guns /
rockets / ordnance) keyed by `wep_*`, plus the `NO_AMMO_WARNING` empty-clip sound. Ballistics,
damage, allotment, the class flags, the specials, and the `FIRE`/`FLYOUT`/`IMPACT` bindings
(`IMPACT` keyed by `SurfaceRegistry` id); `DESC` resolved through `Messages`. Modelled on PlaneStats.
Schema: docs/formats/weapons.md. Verify/inspect with `--dump-weapons`.

`RANGE`, `DETONATION_DISTANCE` and `IMPACT_PROXIMITY` are exposed twice: the authored metres, and
`RangeSqM` / `DetonationDistanceSqM` / `ImpactProximitySqM`, squared once at parse as the original
squares them (`FUN_005ad630`). Compare a `Sq` field against a squared distance and never square-root
one to reach the authored field. The authored form is still the right one where a real length is
wanted — the blast sphere-query radius, the reticle's `RANGE` path cap, a threshold on the authored
number — and every comparison in the tree today measures through a geometry helper that already
returns a plain distance, so none of them changed. `TANGLER`'s `RADIUS` has no square by design:
the original stores it raw and compares it against a squared distance
(docs/org/ordnanceTypes.md).

`ImpactHook` is the per-weapon impact hook the detonation calls first (weapon `+0x20c`), an enum
rather than a delegate because the binary installs exactly one, in the `TANGLER` parse; the parse
here sets `ImpactHook.Tangler` where `FUN_004ba6f0` calls `FUN_005aec90`, and
`ProjectilePool.RunImpactHook` is the dispatch. `TanglerData` carries the engine's defaults for a
block omitting `TIME` (5.0) or `RADIUS` (10.0).

## src/Flight/Loadout.cs
Two layers over `CSVM/data/stock_loadouts.json`. `StockLoadouts.Load` parses the file (default
`res://data/`) into per-plane `LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves
each gun slot's markers to live muzzle `Node3D`s and its caliber+ammo to a `WeaponDef` (via
`GunWeaponId` = `wep_{N+k}`), and each hardpoint to its `pylon`, yielding `GunGroup`s (independent
ammo counters from `CLUSTER_SIZE`) + `Hardpoint`s. Turret slots bind but `IsTurret` (inert, M4).
Schema: docs/formats/loadouts.md. Verify/inspect with `--dump-loadout` (add `--weapon-lab` to bind
the full-rig loadout below instead of the stock one). `StockLoadouts.Load`'s missing-file warning
logs through `Log` (`Utils`), not `GD.PushWarning` (`engine-free-suites` A2).

A third layer sits above those two: `src/Flight/LoadoutChoice.cs`. `LoadoutOptions` holds the Ammo
Selection screen's two dropdown rosters, parsed from the same file's `selectable` block (the eleven
offered ordnance types plus `none`, and the four ammo types plus `none`, both in the original's own
order). `LoadoutChoice` records one pilot's edits keyed by **slot identity** — gun slots 1–4,
pylons 1–8, the formats' own ceilings — and `ApplyTo(LoadoutDef)` lays them over a base handed in
rather than looked up, dropping a pick for a slot the base lacks. That is what lets a custom plane's
saved fit (`BL-354`) use the same path. `none` on a gun omits the group; on a pylon it keeps the
array entry as a sentinel that `Bind` skips, because entry *i* binds to `PylonFillOrder[i]` and
dropping it would move every later pylon to the other wing.

`Loadout.ForRig(plane, WeaponDefs, LoadoutDef?)` synthesizes a lab loadout covering the
airframe's **whole** rig rather than only what stock names: the 4 gun-group slots the reverse-index
rule seats (`docs/formats/markers.md` "Slot → firepoint binding" — W1→fp(9−2n),(10−2n)), each
populated with whichever of its firepoint pair the rig actually has (the Kestrel's W1 resolves to
the lone centreline `firepoint7`), plus one hardpoint per `pylonN` present — then runs the
synthesized `LoadoutDef` through the same `Bind`, so there is still exactly one bind path. A slot
stock does name keeps its weapon/mount/caliber; one it doesn't defaults to the stock's first gun
weapon (`wep_30` if the plane has no stock guns at all) under a generic mount label ("Gun Group N").

## src/Flight/WeaponBench.cs
The world-less "do all 48 weapons mount and fire without throwing" pass check behind
`--weapon-test` and the `weapons-fire` in-engine suite: one static
`Run(plane, Loadout, WeaponDefs, ProjectilePool)` over a **parked** plane that spawns straight into
the caller's pool and returns the report plus the counts a suite asserts on (`Total`/`Ok`/`Errors`/
`Skipped` + `GunMounts`/`PylonMounts`). Needs no world, no colliders and no frame — `Spawn` does the
muzzle math and the pool insert synchronously. Hand it `Loadout.ForRig`'s loadout (both callers do)
and every weapon fires from **every** mount of its class: measured on the Bloodhawk, 4 gun groups ×
2 muzzles for each of the 31 guns and 8 pylons for each of the 17 hardpoint weapons — 48/48, 0
errors, 0 skipped.

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

## src/Flight/AimAssist.cs
The gun aim assist (`BL-342`, decoded in `docs/org/aim-assist.md`). `GunAimSlot` is one gun
barrel's plane-local state — `Smoothed` (what the round fires along), `Target` (what `Smoothed`
chases), `LastUpdate` (game-time seconds) — mirroring the original's eight `0x24`-byte slots at
plane `+0x3a4`. `AimAssist.Tick` is the per-frame forget + catch-up pass
(`FUN_004b3e50`): past `forgetInterval` seconds since the slot was last touched the target unwinds
to local forward; otherwise `Smoothed` slerps toward `Target` at `catchupRate` per second, snapping
outright once a single frame covers the whole turn (`catchupRate·dt ≥ 1`). A plain, engine-free
static class — no `GameClock` read inside it, `now`/`dt` are always passed in — so it unit-tests
without a `FlightController`; proven in the in-engine `aim-assist` suite (`Suites.cs`), which B3/B4/
B5 add their own cases to. `FlightController` owns one `GunAimSlot[]` per firable gun group (indexed
exactly as `FireControl`'s own `(group, muzzle)` pairs — no re-derivation of the original's
`weaponGroup·2+barrelToggle` index), built alongside `_firableGuns`, and ticks every group's array
immediately BEFORE performing `_fire.Step`'s outcome — the original restamps a slot's `lastUpdate`
on every round that goes out (B5's job), so the forget pass must see the pre-shot state. Gated on
`FlightController.IsHumanPiloted` (default true) — the original ticks this only for the local
player, and an AI plane's dead-eye path has no slots at all. Every CSVM plane is human-piloted
today (Decision 7 in `docs/plans/PLAN-sticky-bullets.md`), so the gate is a no-op until M4 lands AI
aircraft; `AssistedGunDirection` falls back to the unassisted muzzle axis for a non-human pilot,
the same fallback a barrel with no slot already takes.

`AimAssist.TryIntercept` (`FUN_00460e30`) is the constant-velocity intercept solver: given a
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

`AimAssist.Scan` (`FUN_004b6530`'s scan half) picks the target the original would pick, or none.
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
`FlightController`s), `Turrets` (fed since M4 C9a by `ProjectilePool.CollectTurrets` — every
registered aircraft's carried gunners, on their host's team), `Structures`
(`AddStructures(DestructibleRegistry)` — an **approximation** of the original's `targets.zrd`
`MStructList`, recorded as one; `MissionTargets` is not the analogue, it holds objective display
strings and nothing damageable) and `Ordnance` (`ProjectilePool.CollectFusedOrdnance` — a FILTER
over the rounds in flight, not a structure of its own). The ordnance filter is
`FUN_00441830`'s two independent reasons to wrap a round: a fuse over `AimAssist.MinFuseDistance`,
**or** `TARGETABLE`. Only a `TARGETABLE` round carries a `Source` (its `ProjectilePool.Flyout`),
which is the admission byte the wrapper sets at `+0x6c` and the only thing `TargetPool` admits.

`AimAssist.FireDirection` is the whole fire call in one place (`FUN_004b6530`'s step order):
seed the slot's target with the plane's forward axis, run `Scan`, rotate the winner world→local into
`GunAimSlot.Target`, restamp `LastUpdate`, rotate the slot's `Smoothed` local→world as the direction
actually fired, and scatter it. `AimAssist.Scatter` (`FUN_004608a0`) is that scatter: a uniform roll
about the aim axis, then a polar angle **uniform in `[0, inaccuracy]`**.

## src/Flight/TargetRef.cs
The one abstraction over everything the player can select: an enemy Fury, a zeppelin engine and a
turret emplacement are three unrelated C# types, and every consumer downstream (the classed pool,
the cycles, the label formatter, the marker) reads this and never the underlying type. It WRAPS an
`AimCandidate` rather than restating it, adding what the aim assist has no use for: `Kind`, `Class` +
`Objective`, `Name`/`DisplayName`/`TypeLabel`/`Category`, and optional `Health`/`Armor`. `Classify`
is the decoded class model (`FUN_004b5cd0`); `CategoryLine` composes the marker's line 1;
`SortsFirst` is `FUN_004bbd60`'s pair of `key = −1` overrides, an objective **or** a hostile round in
flight. Pure data, no Godot node. Decode: [org/targeting.md](org/targeting.md). Pinned by the
`target-ref` suite.

## src/Flight/TargetPool.cs
The player's classed candidate pool: three lists of `TargetRef` (`Enemy`, `Ally`, `NonAircraft`,
reachable through `Of(TargetClass)`), rebuilt from scratch on every `Rebuild` call, which is the
original's own contract and why a runtime spawn appears and a death disappears with no extra
plumbing. It walks three of the aim assist's four lists (`Vehicles`, `Turrets`, `Ordnance`);
selectable structures arrive through `Rebuild`'s separate `subParts` argument, filled only by
`ZeppelinRuntime.CollectTargetParts`, and the campaign's objective sites through its `objectives`
argument, filled by `ObjectiveSites.Collect`. The two mission flags stay separate: `objectiveTarget`
comes in with the candidate and puts a site on the Enemy cycle, `otherTarget` is stood in for by
what the candidate is and puts a sub-part on the Non-Aircraft one. An `Ordnance` entry is admitted only when its source is a
`ProjectilePool.Flyout` with the `TARGETABLE` admission byte set and still live, so a round wrapped
only because it is fused stays unselectable. `Describe` is the only place in the targeting path that reads
a concrete source type, beside `NameOf`/`OwnerOf`, the identity pair the AI ranker's
`rating_biases` match reuses so it grows no second switch of its own. `TargetSelection` owns the instance; `HumanFlightAdapter` wires one per human
pane and `FlightController.StepTargeting` feeds it every frame. Decode:
[org/targeting.md](org/targeting.md) "The candidate list". Pinned by the `target-pool` suite, with
the carried-gunner exclusion on `turret-gunner`.

## src/Flight/TargetSelection.cs
One pilot's target selection: the sticky choice, the eleven actions and the lifecycle. One instance
per pane; it OWNS its `TargetPool`. The split between the action handlers (which only mutate the
class and the selection identity, stepping the list that already exists) and `Resolve` (the per-frame
pass that re-sorts and re-finds the selection by entity, falling back to the list head) is the
original's, and that one fallback is the entire lifecycle: auto-acquire, switch-on-death and
drop-on-class-change are all the same failed re-find. `SectorKey` is the cycle comparator
(`TargetRef.SortsFirst` ahead of every sector, then ahead, behind, left, right, nearest-first inside
each); `Select`/`ApplyInitial` are `--target=`'s
seam, the only things here with no counterpart in the original. No Godot node dependency. Decode:
[org/targeting.md](org/targeting.md). Pinned by the `target-selection` and `target-flag` suites.

## src/Utils/TapHoldButton.cs
One button carrying two actions, split by how long it is held. Feed it the button's LEVEL each
frame; it edge-detects, times over `HoldToRepeat`, and answers `TapHold.Tap` / `Hold` / `None`.
Engine-free: the input read stays with the caller, which is what makes the decoding unit-testable
when the device is not. Pinned by the `target-input` suite.

## src/Flight/TurretDefs.cs
Typed reader over the shared `ai.zrd`'s `TURRET` section — 42 `TurretDef`s (docs/formats/turrets.md):
the carried/standalone split (`CREATE_STANDALONE` present-and-zero = carried, looked up by `TITLE`
from a host; `NODES` patterns = world emplacements), the `PARTS` kinematic chain (3-element
`[yaw, pitch, firepoint(s)]` or 2-element with no traverse ring), the `WEAPON` sub-block
(`NAME` is a BALLISTICS id), arcs, the attack/bored duty-cycle windows and `SOUNDS.CANNON`.
Tolerates the eight engine-accepted never-authored keys. `FindByTitle` mirrors the engine's lookup:
a titleless entry matches unconditionally. `TeamId` carries the decoded loader default — an
absent TEAM is the first ENEMY team (id 2; the space is 0 neutral / 1 ally / 2+ enemy), and the
four authored TEAM 1 standalone entries are the piratezep's own allied rings.

## src/Flight/TurretController.cs
One `ai.zrd` turret gunner, both families: carried (`BuildCarried`, per host from
the vehicle def's `thirdp` `TurretMount`s × `TurretDefs` × the built plane model, ticked from
`FlightController.SimStep`) and world emplacement (`BuildEmplacements`, per matched `NODES`
pattern node via `AnimRuntime.FindNodes` with multi-segment paths scoped to the prior match,
ticked by `Session/TurretEmplacementRuntime`). Per tick: nearest hostile aircraft inside
`DETECTION_RANGE` (team gate through `AimAssist.Hostile`; carried = host's
`FlightController.Team`, emplacement = the authored/default `TurretDef.TeamId` with no conversion,
since one integer space covers aircraft and emplacements alike, until `SetTeam` fans a zeppelin
record's own team over the guns standing on that hull),
`AimAssist.TryIntercept` lead (no solution ⇒ track, hold fire),
wrap-aware directed yaw clamp + pitch clamp, bounded slew (3.0/s), pose written onto the PARTS
nodes, then the fire gates: `Activated`, attack window, 15° barrel-on-solution cone, cached
1–2 s world-only line of sight, `FIRE_RATE` redraw. `Alive` reads the `HEALTHY_NODE` visible IN
THE TREE, not its own flag: a mission `.gw` switches a site off at its root (C3/M03's
`b_turret1..6`), and a gunner under it is dead to its tick and to every gunner's scan
(`mission-off-turrets` suite). `PlatformOf`/`PlatformColliderRids` are what
keep an emplacement's own mounting section out of its line-of-sight ray, and the same set rides
each of its rounds as the pool's `ownerBodies`, so the flak neither strikes nor splashes the gun
that fired it while a neighbouring gun's burst still lands (`turret-self-fire`).
A carried gunner's line of sight runs through `WorldBlocksLine`, a static method mirroring
`FlightController.WorldBlocksLine`'s exact call shape against the `IWorldQuery` `BuildCarried`
hands the constructor (a `GodotWorldQuery` over the host); `_host` itself stays for what it alone
gives (`WorldVelocity`, `InPlay`, `PlayerIndex`). An emplacement has no host and no `IWorldQuery`
either, so it keeps its own `WorldRayBlocked` twin, deliberately left alone: a gunner mounted on
world geometry needs its own section excluded from the ray, which a carried gunner never does.
Format and decode: [formats/turrets.md](formats/turrets.md). Proven by the `carried-turrets` and
`world-turrets` suites, `TurretDefsTests` and `TurretLineOfSightTests`.

## src/Flight/WeaponCursor.cs
`FireControl`'s internal ammo-slot index math (an `internal` class — nothing else may call it):
`NextArmed` is the firing cursor — the selected slot while it has rounds, else the next armed slot
forward-wrapping (`-1` when all empty); `NextSelectable` is where the manual G/H step lands — the
next armed slot strictly after the cursor, skipping empties. Each slot is its own position
regardless of ordnance/weapon type, so H cycles even a uniform loadout. That per-hardpoint reading
is confirmed against the original (user at the controls, 2026-08-14): the player picks a hardpoint,
the game does not merely drain them in pylon order. Stateless; proven through
`FireControl`'s interface (`FireControlTests`), not its own.
The order it walks matches the original's (same observation), so the sequence is settled and not a
knob.

## src/Flight/Ballistics.cs
The VELOCITY/ACCELERATION/GRAVITY integration every round steps with: a static, Godot-`Node`-free
class with `Step` (one round's per-frame advance, mutating pos/vel in place — `ProjectilePool.SimStep`
owns the surrounding raycast/fuse-test loop and its own `dt`) and `March` (the reticle's whole capped
walk — range cap and 4096-iteration bound included — called once per frame by
`FlightController.BallisticImpactPoint` with its own fixed `dt`, for the distance the decoded pipper
rule asks for). Extracted so the two callers cannot
silently diverge; each still owns its own step size. The weapon census behind why a fixed `dt` is
safe for `March`: [formats/weapons.md](formats/weapons.md).

**The motor and its cap** (`FUN_005afd50`, org/ordnanceTypes.md): `ACCELERATION × dt` raises the
round's own speed only while it is **below** `speedCap`, and clamps there. `LaunchSpeed` is the pair
the pool and the march both seed from, and it is where the counter-intuitive half lives: a weapon
with no motor is seeded AT its cap (nothing accelerates it), while a motor round leaves at its
**launcher's** speed and climbs to `VELOCITY` above that. ⚠ **Nothing here may ever reduce a speed.**
The original has no drag term — a round already faster than its cap is left alone rather than clamped
down — and that absence is why its rounds carry so far; `NoStepEverSlowsARound` pins it. `grav` is
the weapon's `GRAVITY` in m/s² directly, not a scale on world gravity, and every shipped entry
authors 0.0. Both vectors are the round's OWN velocity: a launcher's share is the caller's to carry,
so `March` advances position by `vel + inheritVel` while accelerating `vel` alone.

## src/Flight/DisablingIntensity.cs
The shared `SONIC`/`FLASH` intensity (`FUN_0042e840`, decoded in
[org/ordnanceTypes.md](org/ordnanceTypes.md)): a static, Godot-`Node`-free `TryResolve` returning the
wash weight and the stun duration (five times it), plus `FacingDot` for the `FLASH` direction
convention. The curve is a plateau, full strength to a squared ratio of 0.6 (77% of the radius) and
fading over the last quarter, so **both distance inputs are squares** — the engine's distance routine
returns a square and this path never takes a root. `FLASH` adds the facing test (nothing behind the
victim, scaled by twice the dot below 0.5); `SONIC` does not, and that is the only behavioural
difference between the flags. The consumers are the player's screen wash, the AI stun and the smoke
screen; the module itself knows about none of them.

## src/Flight/Difficulty.cs
The difficulty setting, as the engine's own 0/1/2, and the single thing it does: multiply an enemy
vehicle's armour and health maxima at spawn by 0.75 / 1.0 / 1.25 (`FUN_0047c210`, decoded in
[org/vehicleDamage.md](org/vehicleDamage.md)). `Parse` takes both shipped vocabularies, the campaign
selector's Normal/Hard/Hardest and Instant Action's novice/veteran/ace, which name the same three
tiers; `FactorForSpawn` owns the team gate, which is inequality with the player's team and not
hostility, so a neutral or team-less spawn is scaled too. `PlaneStats.WithEnemyDurability` applies
the factor, and the per-spawn jitter runs after it, banding around the scaled hull. The setting
reaches nothing else: in the executable it is readable only through `FUN_00440710`, whose four
callers are that spawn, the options screen, the settings save pass, and Instant Action's
save/set/restore around the same spawn. No AI skill, accuracy or aggression is keyed to it.

## src/Flight/TanglerChoke.cs
The choker's engine-dead duration (`FUN_004b9bc0`'s `TANGLER` branch, decoded in
[org/ordnanceTypes.md](org/ordnanceTypes.md)): a static, Godot-`Node`-free `Duration` of
`ENGINE_DEAD_max × (1 − d²/RADIUS)` floored at `ENGINE_DEAD_min`, plus `EngineDeadBounds`, which
resolves the bounds the way the original does — they are a pair of GLOBALS every `TANGLER` parse
overwrites, so the last entry carrying one wins for every choker in the install (this one authors
exactly one, `wep_12` at `[5, 13]`). **The numerator is squared and the radius is raw**, an authentic
unit mismatch that puts the full-strength zone of a 35 m weapon at about 4.6 m; the floor covers
everything past it, so the curve alone never returns less than the minimum, and whether an aircraft
is caught at all is the cloud's squared-radius test in `ProjectilePool.StepTanglerClouds`. The
seconds go to `FlightController.TryChokeEngine`; this module knows nothing about aircraft.

## src/Flight/SmokeScreens.cs
The `SMOKE_SCREEN` mechanism (`FUN_004b8fd0`, decoded in
[org/ordnanceTypes.md](org/ordnanceTypes.md) "SMOKE_SCREEN is a stun trap"), three types in one
file. `SmokeScreenRule` is static and Godot-`Node`-free: `Catches` (strictly inside the raw range
AND the unit layer-to-victim line's dot with the layer's BACKWARD axis strictly above the stored
half-angle cosine) and `StepWash` (the human wash's cadence over one re-arm timer: 0.97 on a first
hit, 0.9 every 1.5 s while inside, 2 s duration, the grey-green `(0.2, 0.29, 0.145)`).
`SmokeScreenTunables` reads `smokescreen_stun_range` / `_angle` / `_interval` off `player.json`
with the loader's own image defaults (200 m, cos 0.8, 3 s) for an absent key; the angle is stored
as `cos(angle/2)`, so the shipped 170° reaches 85° off axis. `SmokeScreens` is the world registry:
`Lay(layer, timeSeconds)` is the fire path's entry (a `SMOKE_SCREEN` weapon spawns no round; it lays
a screen for its `TIME`), `SimStep(dt)` runs the timer down, ends a screen whose layer is out of
play on the spot, and walks the roster delegate for every running screen, hitting every in-play
aircraft other than the layer inside the cone about the layer's LIVE pose: a human gets the wash
through the `SmokeWashSink` (`ScreenFlash.PlayBlend` in the session) addressed to its own
`PlayerIndex`, an AI gets `FlightController.TryStunPilot(interval)` refreshed every step it stays
inside, so it goes limp for the whole screen and the interval beyond it. The wash re-arm timer is
kept per victim per screen (the original's one slot is single-player), Decision 2. It is NOT an
occluder: no collision, no visibility and no targeting role — the smoke is drawn, and stops nothing.
Each screen carries its own emitter over the `ISmokeEmitter` seam: `Lay` asks the settable
`Emitters` factory for one and homes it with a zero step at the launch pose, `SimStep` drives it at
the layer's live pose every step, and the same teardown both end conditions reach stops it.
`SmokeScreenEmitters` is the engine side of that seam, reading `generate_smokescreen`'s
DISTANCE_INTERVAL `PUFFER_STATE`s out of the world `AnimProgram` (the session wires it once the
chapter's textures exist) and pooling one `Puffer` per authored state, reused only once its previous
screen's puffs have decayed. The compiled archive is where the definition is taken from; the reader
normalizer now carries `DISTANCE_INTERVAL` too, so the reader form of the same definition is a trail
rather than the no-trail sustain it used to read as. The cloud's look is the authored numbers through `Puffer` unchanged (`smokerpuff`:
four puffs per 0.65 m, `SIZE_RANGE` 0.15–0.25 growing 85× over a 2.5–4 s life, `LOCAL_VELOCITY`
10 m/s astern plus ±17 m/s of random, `NEAR_FADE 30,10` so a camera nearer than 30 m of depth
sees none of it, the `53,74,37` ramp at alpha 0.8; `smokerpuff2` is the thin 1–1.3 s ribbon at the
tail); the two `INACTIVE` events at `Animation 2.0` are not applied, since the emitter runs for the
screen's `TIME` and the reference footage shows the cloud still being laid well past 2 s. Pinned
by `SmokeScreenTests` (rules, tunables) and the `smoke-screen` suite (a live five-aircraft roster:
the AI astern stunned throughout and recovering after expiry, the AI beyond 85° untouched, the
human astern washed on its own pane while the layer's and a third human's stay clear, a downed
layer's screen ending at once, the emitter started homed and stopped with the screen, the two
authored trail states read off C1's compiled `cam_anim`, and `smokerpuff` driven at 100 m/s for
four seconds through the particle runtime holding thousands of puffs live past its starting pool,
tens of metres across near the end of life and blown down the host's own backward axis) and by
the `ordnance-launch-axis` suite for the
launch side (a `wep_13` pylon lays one screen, spends its ammo and puts no round in the pool). Not a
`Node`; the session owns and steps it.

## src/Flight/BeeperTags.cs
The `BEEPER` / `BEEPER_SEEKER` pair (`FUN_004b88a0`, `FUN_004b8ad0`, `FUN_004b8ce0`, `FUN_004b8b50`,
decoded in [org/ordnanceTypes.md](org/ordnanceTypes.md) "The beeper and the seeker, which are one
weapon in two halves"), three types in one file. `IBeeperSubject` is the three facts a tag reads off
its aircraft (`WorldPosition`, `InPlay`, `Team`); `FlightController` implements it through a partial
declaration in this file, so the registry and its tests run without an engine. `BeeperTagRule` is
static and Godot-`Node`-free: `Step` (the countdown: an aircraft out of play slams a still-painting
tag to −1 before the frame's decrement, the frame that reaches or crosses zero ends the paint, and
the original never slams again after that), `AlignmentDot` (the unit vector FROM the tag TOWARD the
round, dotted with the round's heading, so a tag dead ahead scores −1 and LOWER is better aligned)
and `Prefers`, the running-best comparison with the four literals: a better-aligned candidate
replaces the best under a SQUARED-distance ratio of 1.2 (about 9.5% farther as a length); a
worse-or-equally-aligned one must be strictly nearer (ratio under 1.0) AND either sit at or under a
dot of 0.7 (anything less than about 134° off the round's nose) or give up under 0.1 of alignment.
Net effect: the nearest painted aircraft, unless it is well behind the round, with a
better-aligned one stealing only within the 20% squared window; and because it is a running best
in tag order, a near-worse and a far-better pair inside that window resolves to whichever was
tagged LATER. `BeeperTags<TAircraft>` is the world registry: `TryTag(shooterTeam, victim, seconds)`
is the hit path's entry and holds every creation gate the original has (`AimAssist.Hostile` on the
teams, the victim in play, no live tag on it already, and one tag per sim step, which is the
original's clock stamp compared on the next creation); a second beeper hit on a live-tagged aircraft
makes no tag and does NOT refresh the first, while an aircraft in its tail takes a fresh one.
`SimStep(dt)` runs every tag through `Step` and deletes it once its countdown sits at or below −5,
five seconds of tail after expiry (four after a slam) so nothing holding a reference sees it vanish.
`PickTarget(roundPos, roundHeading)` is the seeker's per-frame query over tags with a countdown
strictly above zero and returns the aircraft or null; an untagged aircraft is never returned, and
in-play is not tested there because a dead aircraft's tag collapses on the next step. Pinned by
`BeeperTagsTests` (every threshold from both sides, the slam, the tail, the gates, the order
dependence, and `wep_10`'s `TIME 20` read off the extracted file). Not a `Node`; the session owns
and steps it after every aircraft, in both step paths, and hands it to `ProjectilePool.BeeperTags`
for the hit-side tagging and the seeker's retarget, which are the projectile integrator's.

## src/Flight/CamParams.cs
One aircraft's camera tuning out of `camparam.json` ([formats/camparam.md](formats/camparam.md)):
the `default` block, then the plane's own block layered on top. Seven of the eleven airframes carry
one; the other four take the 13.0 default. Mirrors `PlaneStats.Load`'s shape (same
`Load(zrdrPath, planeNodeName)`, same nearest-wins resolution), and like it is loaded once per
distinct plane and cached by `GameSession`. `FromData` is false when the file was absent — the
built-in fallbacks are the shipped `default` block verbatim, so a partial extraction still flies and
the session log distinguishes "the data says 13" from "we guessed 13".

## src/Flight/CameraController.cs
The flown aircraft's camera, split out of `FlightController`: the roll-following chase camera, the
numpad fixed views (`Views`, `ActiveView`, `FixedView`, `LogView`), the look-behind view
(`BackView`, numpad 0 / `--view=back` / a pad click, at the chase radius bounded into the authored
`back_dist_min/max`), the E42 (`BL-372`) analog look-around (`PadLook`, the right stick — a
continuous twin of the fixed views at the SAME dynamic radius, rigid and instant so releasing the
stick reads as a snap back to the ordinary chase pose), the authored crash camera (`CrashView`, a
hard cut to a static elevated vantage `crash_horiz` behind / `crash_y` above the impact, held until
respawn — framing decoded off the original's crash footage; the shared world clearance and the
decoded death/flyby modes remain unimplemented under `BL-260`) and the free orbit used while the
weapon lab holds an airframe. ⚠ That orbit no longer answers to a HALT (`BL-429`): a board's menu
cursor reads the same `WASD`/arrows/left stick `OrbitInput` does, so a halted world that also flew
the camera meant choosing a menu row swung the view. `FlightController` writes nothing to the
camera while a board is up, and the free look moved behind the board's Photo Mode row, which hands
the pane to a `SpectatorCamera` instead. `Held` is the only remaining orbit source here, and no
menu shares its keys. Steers a `Camera3D` it does not own, as `UI/OrbitCamera` does for the
static viewer. Beside the held views it carries the pilot's SELECTED view mode (`ViewMode`,
`FirstPerson`, `CycleCockpitViews`, `SelectChase`): Chase, Cockpit or Nose, seeded from
`--view=cockpit`/`=nose` and changed at the controls by F8 (Cockpit → Nose → Chase) or F6
(directly to Chase). The decisions themselves are `PilotView`'s, not this class's, so they are testable
without an engine; this class holds the state and the camera. ⚠ The modes are deliberately NOT rows
in `Views`: `BL-150` rebuilds that table later and must be able to replace it without touching them
(PLAN-cockpit-view, Decision 2). ⚠ Outside first person a held numpad key overrides the mode for as
long as it is down and leaves the selection alone, the same precedence it has over `--view=`'s
pinned digit; INSIDE Cockpit or Nose it holds no view at all, because the numpad is the head-look
snap cluster there, which is what the original binds it to (`OriginalScreenshots/Keybinds Views
2.png`: `Kp1`–`Kp9` = Look Up/Left/Rear … Look Forward … Look Up/Right). `PilotView.HoldsFixedViews`
is that rule and `ActiveView` returns −1 under it, so the per-frame chain and `Snap` obey it
together. Numpad 0's look-behind is outside the cluster and still overrides every mode. Cockpit and
Nose sit at the plane's authored `cockpit_camera` marker (`FirstPersonPose`, a static, engine-free
law: `camera_world = plane_pos + plane_rotation × offset`, plus the fixed −4.70° head-pitch
tilt — `FirstPersonView` is its thin write onto the owned `Camera3D`), rigidly mounted with no
smoothing and no camera-side shake so the camera inherits the plane node's wobble for free
(`docs/org/shakes.md`). The offset comes in through the constructor
(`PlaneBuilder.CockpitCameraOffset`, fallback the origin). The aim is `Head`'s (a `HeadLook`)
current angles, composed azimuth-about-the-plane's-up then elevation-about-the-yawed-right-axis,
with the fixed tilt riding the elevation axis; `Snap` recenters the head, so a respawn never frames
itself over the pilot's shoulder. Each mode carries its own FOV
(`HorizontalToVerticalFovDeg`, a static, engine-free law: `vertical = 2·atan(tan(H/2) ·
(4/3)/liveAspect)`, 80°H Cockpit / 60°H Nose — `ApplyFirstPersonFov` is its thin write, reading
the owned camera's OWN viewport for the live aspect so a splitscreen pane derives its own answer).
Every other pose runs on the external FOV the camera carried at construction
(`RestoreExternalFov`, captured once so this class never reaches into `GameSession`'s 62° global);
`FlightController._Process` calls it by default and only the `FirstPerson` arm overrides it, so a
look-behind while SELECTED Cockpit/Nose gets the external FOV while held and the first-person FOV
back on release. `Snap` and `CrashView` carry the same default/override shape, so
a spawn/respawn/crash-cut never shows a stale FOV. `LogView` already names the modes
(`view n=cockpit`), which is what makes a scripted mode selection verifiable. The chase RADIUS is dynamic per plane (BL-248): `d = Dist + DistFactor·V` (both
authored) plus a first-order acceleration transient relaxing at the MEASURED 0.65 /sim-s
(`UpdateDynamics`, host-called once per sim step); the offset's DIRECTION (behind and above at
~15.7° elevation) is not in the data and stays hand-picked. Collaborators: `FlightController`
(the only host) and `CamParams`.

## src/Flight/HeadLook.cs
The pilot's head in the two first-person views, decoded from the original's shared look controller
(`docs/PLAN-cockpit-view.md`, "What the data actually ships"). It holds two pairs of angles: the
TARGETS the input sets, and the SHOWN angles that chase them exponentially,
`shown = target + (shown − target)·e^(−rate·dt)`, at 3.0/s for elevation and 5.0/s for azimuth.
Elevation is 0 at level and +π/2 straight up, clamped to `ElevationFloor`..π/2; azimuth is 0 dead
ahead, positive to the left, wrapped to ±π. `Step` picks this frame's target and then always
chases it, so snap, free-look, the center key and C22's autohead all reach the eye through one law.
The three input paths are the original's: a **snap** direction maps to a target through
`SnapTargets` (dead ahead looks straight UP, a 45° diagonal 45° up, anything else level, azimuth
being the direction's own angle mirrored so that pointing right looks right), releasing it returns
the targets to straight ahead; **free-look** integrates the targets at a fixed 2 rad/s along the
input direction, which is normalised first because the rate IS the law — the original's input is a
hat switch, so a light stick deflection pans exactly as fast as a hard one; the **center key**
zeroes both targets at once and beats a held snap. ⚠ `ElevationFloor` is a constructor parameter,
not a constant: the original's first-person caller passes 0 and its chase caller −π/2, and the
chase look-around (a filed E41 item) is the same controller. `IdleAim` is C22's seam — consulted
only on a frame with no look input at all, its answer becomes the targets directly, deliberately
past the floor, because autohead's own floor is below level. `AutoheadTarget` (static, engine-free)
is that seam's law: local-frame sideways/vertical velocity only (forward speed dropped — a port
decision, docs/formats/vehicle/player-globals.md's autohead row), scaled by `autohead_turn_time`,
capped in magnitude at `autohead_turn_max`, its components read DIRECTLY as (elevation, azimuth)
rather than through an arctangent. `FlightController.AutoheadTarget` is `IdleAim`'s live wiring —
gated on `ViewMode == Cockpit` and a `Config` toggle (`headLook.autohead`, default ON).

Engine-free apart from `Mathf`, so every law unit-tests without a camera. Collaborators:
`CameraController` (owns one as `Head` and composes its shown angles into the view basis) and
`FlightController` (reads the devices and steps it on the SIM clock — a wall-clock step would run
the smoothing 39% off, the same trap the chase distance transient records).

## src/Flight/CockpitVisibility.cs
The per-mode node hiding the original applies to the pilot's OWN aircraft while a first-person view
is on the screen (`docs/org/cameraViews.md`, "Mode 6 = Cockpit" / "Mode 7 = Nose"): Cockpit draws
`cockpit1` and hides the `healthy` body; Nose hides the interior, the body, and the `markers` and
`dontmove` groups; every external pose renders the aircraft exactly as it was built. `Rules` is the
whole decision as a pure function over `(PilotViewMode, firstPerson)`, so it unit-tests engine-free;
`Bind` finds the four groups in a built plane model and `Apply` writes one frame's answer onto
them. `FlightController._Process` calls it every frame beside the camera write, keyed to the pose
that frame actually took — the look-behind is an external pose and brings the body back while it is
held, the same shape `CameraController.RestoreExternalFov` has. A held numpad key reaches this only
from an external selection, since in first person it drives the head instead of the camera.

⚠ `Bind`'s group search is interior-blind: each gauge sub-assembly inside `cockpit1` carries its own
`markers` child, so a plain depth-first walk can bind a gauge's instead of the airframe's.

⚠ **Splitscreen is a shared scene tree.** Visibility is a property of the node, not of a viewport,
so a pane whose pilot sits in the cockpit hides that plane's body in EVERY pane. Each rig owns its
own plane model, so the rule is at least per-pilot rather than keyed to player 1; making it
per-pane needs render layers, which Decision 5 defers.

## src/Flight/CockpitOverlay.cs
The cockpit interior's own render pass, behind `--cockpit-pass` and off by default. It takes the
built `cockpit1` node out of the plane model and re-parents it into a `SubViewport` carrying its own
`World3D`, at zero translation with the mount basis `PlaneBuilder` gave it (the head-pitch tilt and
`InteriorScale`); the pass's camera sits at that world's origin, aimed by
`CameraController.FirstPersonPose` with the plane position and the `cockpit_camera` offset both
zero, since those two cancel between the eye and the panel. The projection is therefore the main
world's exactly, computed from small numbers instead of chapter-scale ones. The viewport is
transparent-backed on `HudLayers.CockpitPass`, so the world draws under the panel and the whiteout,
the screen wash and the HUD still draw over it. Lighting is a copy of the world's sun, re-aimed by
the inverse plane attitude each frame, plus the world environment duplicated with its background
cleared. `GameSession.BuildCockpitPasses` builds one per `PlayerRig`, on that rig's own `HudParent`;
`FlightController._Process` calls `Sync` beside the camera write, and `Sync` follows the interior's
own `Visible` so `CockpitVisibility` keeps deciding which views show a cockpit.

## src/Flight/ImpactOutcome.cs
"What should happen when this weapon hits this surface id" as a value — `EffectName` (the row's
`ANIMATION`, else its `SURFACE_ANIMATION`) with `SurfaceOriented` naming which slot it came from,
`Sound`, the `ImpactStandIn`, `Damage`/`BlastRadius` (+ `HasBlastDamage`) — plus the pure static
`Resolve` that computes it from a `WeaponDef`, a `SurfaceRegistry` id and the impact hook's
`ImpactSuppression` mask (`FUN_005ac7a0`'s: `Sound` nulls the sound, `Animation` drops the
`ANIMATION` slot and leaves a `SURFACE_ANIMATION` standing, `Effects` covers the crater carve and
the row's `EFFECT`, neither of which CSVM plays; the damage figures are under no bit). No Godot type, no scene, no sink, no sound archive, so the dispatch's one
decision is readable by a unit test; `ImpactOutcomeTests` is that test, including a suite over all 48
shipped weapons × the eight reachable ids asserting the *rule* (an effect or a stand-in but
never neither; a resolved sound names either a `SoundDefs` entry or a `SOUND_GROUPS` name;
`HasBlastDamage` matches the raw damage/radius fields) rather than a table of expected per-weapon
outcomes. `ProjectilePool.Impact` reads the struck id (`SurfaceIdOf`) and calls it, then `Apply`
performs the result; `ProjectilePool.HasBlastDamage` is the weapon-level spelling of the same
`HasBlastDamage` rule, so the rule exists once. The stand-in ladder (model ▸ explosion ▸ ricochet ▸
spark, no arm for ground) is decoded on `StandInFor`; the `buildings`(11) surface-id pitfall is in
[formats/weapons.md](formats/weapons.md).

## src/Flight/Projectile.cs
**The original's projectile-visual runtime is written up in [org/tracers.md](org/tracers.md)** — how
the engine draws a round at all (the `FLYOUT MODEL` is aimed ONCE at spawn and thereafter only
translated; the shared model node is multi-parented, not cloned, so one `slug.flt` serves every live
round), plus the authored tracer geometry the hand-tuned constants here stand in for: two crossed
0.2 × 4.5 m quads whose **tail** is the tracked point, a separate 0.29 m `*tip` quad 4.56 m ahead,
and a 600 m LOD past which the original draws nothing. Its closing table lists every place this file
deliberately differs — read it before retuning `TracerLength`/`TracerWidth`/`TracerBrightness`/
`TracerMinPixels`.

**A gun round leaves along the aim assist's line, not the muzzle axis.** The retail engine runs a
per-muzzle assist at spawn time (target scan → constant-velocity intercept → plane-local smoothing →
1° scatter), decoded in [org/aim-assist.md](org/aim-assist.md) and built in `AimAssist.cs`
(`BL-342`). It is a **launch-direction** assist: nothing steers a round in flight, so it belongs
at the fire call, not in this file's integrator.
`Spawn`'s optional `ownerBodies` is the original's owner node for a round nobody's aircraft fired
(a world emplacement's own mount): the hit ray excludes those bodies and the splash gather skips
them, the same way a pilot's own airframe is excluded through the shooter id
(`org/ordnanceTypes.md` "Half two, the splash"). Nothing else is exempt from splash: a neighbouring
gun's burst, or a rocket into the pit, still kills the gun.
`Spawn`'s optional `aimDir` is how it arrives — a world direction the CALLER computed
(`FlightController.AssistedGunDirection`); omitted, `Spawn` still uses the muzzle axis, which is
what every rig, the bench and the rockets pass. Only the round's velocity uses it — the muzzle flash still rides
`muzzle.Basis`, because the barrel has not moved.
`CollectFusedOrdnance`/`CollectAircraft`/`CollectTurrets` build three of the assist's four
candidate lists off this pool's own state: the live rounds the engine wraps (a FILTER, every def
with a fuse longer than `AimAssist.MinFuseDistance` **or** `TARGETABLE`, `FUN_00441830`'s two
independent reasons), the registered aircraft, and each
registered aircraft's carried turret gunners — the same roster the hit ray and the fuse
already use, so the assist cannot drift onto a second list. `PlayShotSound` is the turret gunners'
launch bark through the pool's own one-shot pool. `DefaultVelocity` (500 m/s, the
launch speed for a def with no `VELOCITY`) is shared with the scan so the lead is solved for the
speed the round actually leaves at.

**A round's velocity is two vectors, not one** (`org/ordnanceTypes.md`, "Launch velocity is
inherited"). `Proj.Vel` is the round's OWN velocity, the original's `heading × speed` and what
`ACCELERATION` raises; `Proj.Inherited` is the launcher's velocity the spawn copied into it. Every
reader asking how fast and which way a round is travelling goes through `WorldVelocity`, which adds
what `InheritedFraction` has left of the second: 1 at launch falling linearly to 0 at `LOCK_ON`
seconds. `CarriesLockOn` is the inherit-at-all flag (`FUN_005aef40` copies the launcher's vector
only for a weapon authoring `LOCK_ON` and zeroes it otherwise, so the choker and the two
non-`LOCK_ON` emplacement rounds inherit nothing) and is the decay's whole gate here.
⚠ **That is a deliberate, recorded divergence from the original.** There the decay lives inside
the steering step `FUN_005af960`, which `FUN_005af720` runs only for a `LOCK_ON` weapon whose round
**holds a target** (`SteeringStepRuns`), and its shot routine always satisfies that half by handing
a `LOCK_ON` weapon a synthetic ring-buffer target when the player has none selected (undecoded and
not reproduced). A CSVM round can hold no target at all, so gating the decay on it would fly a
torpedo fired with nothing selected at launcher speed plus 60 m/s for its whole range; the turn
stays gated on the target, only the decay does not. **Guns are held outside the rule on
purpose** (`InheritedAtLaunch`): no `CANNON` in this install authors `LOCK_ON`, so applying it to
them would strip every bullet of its launcher's velocity, and both the gun aim assist and the impact
reticle (`Ballistics.March`) are built on the inheriting round. `CollectLiveRounds` is the seam a
scripted run samples a round's speed through, and a breadcrumb reports the first four rounds that
finish shedding.

**The steering step turns only what the original turns, and turning costs speed**
(`org/ordnanceTypes.md`, "Guidance"). Per round and per sim step, in `FUN_005af720`'s own order:
`RetargetSeeker` (a `BEEPER_SEEKER` round asks `BeeperTags.PickTarget` with its position and unit
heading and REPLACES `Proj.Target` with the answer, null included, as `FUN_00441780` writes zeros
when nothing is painted; so the target a seeker was launched with is gone on its first frame and a
seeker steers only at a painted aircraft), then `Steer` on a round `SteeringStepRuns` admits whose
target has a position (`TargetPosition`; `Proj.Target` stays `object?` because the slot takes an
aircraft, an emplacement or a zeppelin sub-part alike), then `Ballistics.Step`, so the motor acts
on the penalised speed. Inside `Steer`: the desired direction is the bearing to the target, or
`LeadDesired`'s blend; `MaxTurnRad` is `TURN_RATE × dt × ramp` in radians times the engine's
per-shot turn scalar (`DAT_00a1e1b8`, initialised to 1.0 and reset to 1.0 by every spawn, so 1 on
every steering frame; the ramp is `TURN_SUSPEND_TIME`'s, unauthored, so 1 from the first frame and
kept as a term); an angle over the clamp slerps the heading by exactly `clamp / angle`
(`SlerpDirection`, renormalised, a dead-astern target left alone), otherwise it snaps; and whenever
the angle was above zero the own speed is multiplied by `TurnPenaltyFactor(turned)`,
`0.8 + 0.2·cos` of the angle actually swung THIS frame (the clamp when clamped, the whole angle when
it snapped). That is per steering frame, never per turn, and because it is the cosine of one
frame's swing the loss over a whole turn scales with the frame's authority: at 60 fps the seeker's
1.25 rad/s costs about 0.3% over a 90° swing (`ordnance-guidance` asserts the exact product), and a
coarser step costs more. The gate is on the flag: every dumbfire type authors `TURN_RATE 0.001` and
so, with a target, is steered by 0.001 rad/s, which the same suite measures on the torpedo (0.23°
in 4 s). `LeadDesired` is `LOCK_ON_LEAD` (`FUN_005af960`'s first block): from element 0 of age, on
a target with a velocity (`TargetVelocity`, the original's second target field, round `+0x74`),
the constant-velocity intercept **`AimAssist.TryIntercept`** (the same solver `AiGunner` and
`AiRocketeer` use; the original's `FUN_0053e56d` is the same solve on the round's OWN speed and the
target's velocity) replaces the bearing, slerped in from the bearing at element 0 to the full solve
at element 1 (`FUN_005ad630` at `0x005ade43` stores `1/(el1 − el0)` at weapon `+0x84` and clamps
el0 to at most el1); no solution leaves the bearing. ⚠ No shipped carrier reaches it: `wep_04`,
`wep_25` and `wep_27` are the three, all pair it with the 0.001 sentinel, and all expire at their
900 m `RANGE` before their 4 s / 5 s onset even off a standing launcher (`wep_04` at 3.46 s), so the
suite exercises the blend on a lab def cloned from `wep_11`.

**A `BEEPER` hit paints and deals nothing.** In `Apply`, a weapon with `BeeperTime` reaching an
`AircraftBody`, ray-struck or fused, calls `BeeperTags.TryTag(round team, victim rig, TIME)` and
returns before either damage path (`FUN_004b9bc0`'s `BEEPER` branch: the tag, then both damage
figures zeroed): the pair is discarded structurally rather than passed as zero, so an authored
pair would still spend nothing; `wep_10` authors 0.0/0.0 either way. The effect and sound above it
play as usual. `Impact`/`Apply` carry the round's own `Proj.Team` for that gate, stamped once at
spawn and never re-derived. `CollectHeldTargets` is the seam a scripted run reads a seeker's pick
through. All of B6 to B9 is flown by the `ordnance-guidance` suite on a live pool with a lab
`BeeperTags` beside it.

**The impact hook runs first, and the `SURFACE_ANIMATION` sits on the surface**
(`org/ordnanceTypes.md`, "Half one, the direct impact"). `Impact` calls `RunImpactHook` before it
resolves anything: the dispatch over `WeaponDef.ImpactHook`, an enum because the binary installs
exactly one hook (the `TANGLER` parse's `LAB_004ba660`), whose arm spawns the choker cloud below and
returns `ImpactSuppression.Sound`, so a choker's `IMPACT` row plays no sound, as the original's does
not. The mask goes into `ImpactOutcome.Resolve`, so `Apply` performs a row already stripped of what
the hook silenced and decides nothing new. `EffectOrient` picks the template basis the effect is
placed with: `SurfaceUpBasis(normal)` (the shortest rotation from world up onto the struck normal,
`FUN_0053fd40` from `DAT_006379c0 = (0,1,0)`) for the `SURFACE_ANIMATION` slot and identity for a
plain `ANIMATION`, which is BL-293's parked half: the fixed-axis upper ring is a plain `ANIMATION`
and stays fixed. It reaches the world-effects runtime through `EffectSink`'s new `Basis` argument
(`AnimRuntime.PlayEffectAt(orient)` → `TemplateStage.PlaceOn(orient)`, which sets the root's whole
basis when given and leaves it alone when null, so every other placement path is unchanged) and the
gamez-model spawn through `SpawnImpactModel(orient)`. On flat ground the rule changes nothing;
`impact-orientation` fires into a 30° slope and into flat ground and reads the basis back off the
sink. `SurfaceBasis` (Z along the normal) stays the sprite stand-ins' own frame.

**A `SONIC` or `FLASH` burst disables every aircraft in its radius and hurts none of them**
(`org/ordnanceTypes.md`, "The hit-side dispatch" and "SONIC and FLASH"). `ApplyDisabling` runs
from `Apply` on every burst of such a weapon, struck, fused or timed out, over the same
`GatherAircraftCandidates` + `BlastCovered` + 32-cap walk the damage splash takes, because the
original hands each splash entry to `FUN_004b9bc0` and its `SONIC`/`FLASH` branch runs the intensity
on the entry's squared surface distance. Per victim: `DisablingIntensity.TryResolve` on that square,
`WeaponDef.ImpactProximitySqM`, the `FLASH` flag and `FacingDot(NoseDirection, WorldPosition,
burst)`; then the victim's kind decides. A human's pane gets `WashSink(PlayerIndex, colour,
intensity, stunSeconds, DisablingWashStartDelay)`, red `(1,0,0)` for `SONIC` and white for `FLASH`,
duration five times the intensity, after the 1.0 s start delay the branch passes `FUN_0042e9d0`;
`GameSession` assigns `ScreenFlash.PlayBlend` to that sink, so it is per pane and blends on overlap.
An AI gets `FlightController.TryStunPilot(stunSeconds)`, which holds the guards; nothing on the
human path touches a control. `AircraftDamageDiscarded` is the four no-damage types (`SONIC`,
`FLASH`, `BEEPER`, `TANGLER`) and is read twice: `Apply` returns before `TakeProjectileHit` for a
struck aircraft, and `ApplyDamage` skips its aircraft gather for them, so a scaled zero never
reaches a plane's ledger, flashes "HIT" or wakes the AI (world bodies still take the authored, zero,
pair). `disabling-hits` flies all of it on two human rigs over a real two-pane `ScreenFlash` and two
AI rigs, including a burst fusing 29 m abeam that stuns for the fade's 3.8 s and a direct hit that
overwrites it to 5 s.

**The choker is a cloud** (`org/ordnanceTypes.md`, "The choker, settled"). `SpawnTanglerCloud`,
the hook's arm, leaves a `TanglerCloud` at the burst with the weapon's `RADIUS` both raw (the
duration's denominator) and squared (the catch), living the shared `TIME` (2 s on `wep_12`);
`StepTanglerClouds`, run by `SimStep` after the rounds, counts each down and, while it lives, hands
every in-play aircraft whose ORIGIN sits inside the squared radius `TanglerChoke.Duration(originSq,
radiusRaw, EngineDeadBounds)` through `FlightController.TryChokeEngine`, whose timer is extend-only,
so a victim inside is held at the formula's value until the cloud is gone and only then runs down.
Nothing excludes the shooter, as nothing does in `FUN_004b9590`. `EngineDeadBounds` is the pool's
copy of `TanglerChoke.EngineDeadBounds(weaponDefs)`, assigned by `GameSession`; the static image's
pair until then. The round's `DETONATION_DISTANCE` decides only where the cloud forms.
`CollectTanglerClouds` is the seam a suite reads the clouds through; `disabling-hits` reads a 12.7 s
choke on the struck AI, the 5 s floor on a human 20 m out, the refresh while the cloud lives and the
countdown once it is gone. The at-the-controls piece still owed is the choked aircraft's sound: the
original swaps the engine loop (`FUN_004b15c0`) and this side cuts thrust only.

`Proj.Cap` is the own speed `ACCELERATION` climbs to, seeded beside `Vel` at spawn from
`Ballistics.LaunchSpeed` (**after** the `weapons.rocketSpeedScale` dev factor, so scaling a rocket
scales its cap with it) off the raw `inheritVel`, which the original reads before the `LOCK_ON` gate.
The four motor weapons therefore leave at their launcher's speed rather than at `VELOCITY`: a flak
emplacement's `wep_27` starts at nothing and is still short of its authored 850 m/s when its 900 m
`RANGE` runs out. A motor round's first-frame speed is ~0, so `Spawn` poses the `FLYOUT` body down
the launch direction instead of a velocity too short to normalise. `Proj.Grav` is the weapon's
`GRAVITY` verbatim in m/s²; `WorldGravity` is now the sprite debris' fall rate alone. The
`motor-acceleration` suite flies all of this on a live pool.

**A round ends itself for one of exactly three reasons, and never on the step it collides**
(`org/ordnanceTypes.md`, "The motion step, and where a round dies"). `EndConditionMet` is the
original's own branch: `Proj.Travelled` reaching `Proj.Range` wins outright and neither fuse is
consulted after it, `TargetFuseTriggered` is the per-round fuse on the round's OWN target (squared
throughout, gated on `LOCK_ON` and a held target and a non-zero `DETONATION_DISTANCE`), and the
`DETONATION_TIME` age test is what a round failing that gate falls through to. All three run
**before** the swept step's ray, because the original ends a round inside its motion step and only
moves and collides what survives, and all three leave through `EndRound`, which shares the effect,
sound and splash paths a struck surface gets. `DetonatesAtRange` is the expiry's own question: the
original's rule is `LOCK_ON` and not `EXPIRES`, or `DETONATE_AT_RANGE`, and since this install
authors neither of the last two, carrying `LOCK_ON` is the whole rule — the choker, the cannonball
and the fake weapon vanish at their range where every other ordnance type detonates. `DefaultRange`
is the engine's own 500 m for a def authoring no `RANGE`. ⚠ `ProximityFuseTriggered` is a
DIFFERENT path and stays as it is: it sweeps the aircraft roster for anything passed near, is
aircraft-only by decode (`BL-233`), and holds fire while a candidate is still being closed on. The
two are complementary, and the `ordnance-end-conditions` suite flies a case where the sweep is
holding fire on a nearer aircraft while the target fuse goes off anyway.

**`RANGE_MINIMUM` is a hittability gate riding the same accumulator, and hides nothing.** A weapon
carrying both `FLYOUT_HEALTH` and `RANGE_MINIMUM` (`FlyoutUnhittableAtLaunch`, the torpedo alone)
spawns with `Proj.IntersectOff` set, and `ArmFlyoutIntersect` clears it once `Proj.Travelled`
passes the authored distance; `FlyoutStruck` skips a round while it is set. That is the whole
effect: `FUN_004cd210`, which the spawn and the gate call, writes node flag `0x10`, the
`INTERSECT_SURFACE` bit, and visibility is a different bit nothing on the projectile path touches
(`org/ordnanceTypes.md`, "Hittable distance"). The body and its `MODEL_ANIMATION` run from the
spawn frame like every other ordnance round's; the torpedo's folded launch look, its 3.5 s switch
to wings, prop and white puffs, and its beeps are all the def's own timeline, run by
`ProjectileFlyoutAnim.cs` below. `CollectFlyoutIntersect` is the seam a scripted run reads the gate
through and `CollectFlyoutBodies` the one it reads the launch look through. ⚠ An earlier reading
here had the round hidden for 300 m and held its trail back to match; both are gone, and the
`ordnance-end-conditions` and `shootable-flyout` suites assert the drawn-from-launch rule.

**A `TARGETABLE` or `FLYOUT_HEALTH` round carries a `ProjectilePool.Flyout`** (`SeedFlyout`,
minted for the torpedo alone in this data), which is both halves of "shootable": the admission byte
`TARGETABLE` sets on the target wrapper (`FUN_00441830`, the byte at `+0x6c`) and the armour/health
pair the spawn seeds from weapon `+0x8c`/`+0x90`. It is a CLASS because the player's target registry
re-finds a selection by source object every frame and a pooled round is a slot in a struct array with
no identity of its own; a round carrying neither key gets none of it, which is the same outcome as
the engine's **−1.0** not-shootable sentinel without the per-round allocation. The sentinel itself is
still carried, on a `TARGETABLE` round authoring no `FLYOUT_HEALTH`, and is what keeps the
`Health == 0` frame test safe. `Flyout.Spend` is `FUN_005abcf0`'s arithmetic verbatim: each pool
clamped at zero, health touched only once armour is empty. ⚠ It is NOT `PlaneDamage.Spend`
(`FUN_004b7f80`), which is a different, richer routine with an armour-shielded share; the flyout
branch is the plain clamp-and-subtract pair, and the two must not be merged.

**A flyout is struck through the pool, not through a Godot body.** `FlyoutStruck` walks the live
rounds and slab-tests each swept segment against the struck round's own box (`Proj.HitCentre`/
`HitHalf`, the `FLYOUT MODEL`'s AABB in the round's flyout pose), and the nearer of that answer and
the world ray's wins, which is `FUN_005b03f0`'s rule for its one intersect database. The round
excludes its OWN box and nothing else: the decode has no owner exclusion here, so a pilot can shoot
down a torpedo he launched himself. Splash never reaches a flyout — the pair is spent only on this
direct-hit path — and a flyout is never cover. A struck flyout spends the pair and the impact ends
there: no per-surface effect, no sound, no splash (`FUN_005abcf0`'s first branch returns). The
choice of a pool sweep over a physics body is deliberate: a `--run-tests` pass never yields a frame,
so a body's queued transform never reaches the physics server and a real ray through it misses.

**Destruction at zero is not a detonation.** `FUN_005af720` tests health `== 0.0` every frame after
the retarget callback and before the guidance gate, so a shot-down round neither steers nor moves on
the frame it dies. `DestroyFlyout` plays `DESTROY_ANIMATION` (`torpedo_destroy_effect`) at the
round's position through `EffectSink` and retires it; only a shootable round authoring no
`DESTROY_ANIMATION` falls through to the ordinary detonation, which nothing in this install does, so
a shot-down torpedo never sets off its warhead. `CollectFlyouts` is the seam a scripted run reads the
pair and the admission byte through. Pinned by the `shootable-flyout` suite.

`ProjectilePool` — the shared-world weapon-fire subsystem: a fixed pool of projectiles integrated
with `Ballistics` (VELOCITY/ACCELERATION/GRAVITY, ending at RANGE), plus tracer streaks,
muzzle flashes, and the per-surface IMPACT sound + effect model. Per-class impact looks: a
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
(unclassified-terrain) hit takes the same single spark as every other unhandled surface: it has no
stand-in of its own.
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
Each quad is
centred by default (`Sprite.Pos`), except the muzzle flash, which sets `Sprite.AnchorLeft` so
`RenderSprites` derives the quad centre from `Pos + Orient.X * (currentSize/2)` — the texture's left
edge (QuadMesh's default UV, U=0 at local X=-0.5) stays pinned at the muzzle as the quad shrinks over
its life, instead of the texture being buried under a centred blob. `AddMultiMesh`
draws every texture un-mirrored (a `Uv1Scale` mirror without a matching `Uv1Offset` here once
degenerated, under `TextureRepeat=false` clamping, into every sprite this pool draws — tracer,
muzzle, impact, smoke — rendering as a flat single-column colour stripe); a texture needing a
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
entry are gone). `weapons.tracerLength`/`tracerWidth`/`tracerBrightness` still route the
constants through `Config`, but the defaults are now **measured off the model**, not tuned by eye —
and `tracerBrightness` sits at a neutral 1.0, since the ×3 overbright was compensating for the tip
disc and second quad this shape supplies itself. `RenderTracers` still floors the drawn width/length
per round against `weapons.tracerMinPixels` via `ScreenSize.MinWorldSizeForPixels`, screen-space
over the one shared world-space mesh, so bound to `ProjectilePool.Viewers` (a `ViewerSet`, this
file's own entry below) and sized for the **nearest** viewer — see docs/org/tracers.md for the
deliberate conflict this floor carries against the decode's 600 m LOD, and for the splitscreen bug
a P1-only floor produced. `TracerScreenSizeTests` pins the rule.
`Spawn(weapon, worldMuzzle, inheritVel, shooterId)` fires one round; one pool per session, fed by
every player's guns — `shooterId` is the firing `PlayerIndex` (`NoShooter` for the lab's), carried on
the round so the near-miss cue can exclude its own. `NearMissTargets` is that cue's registry (BL-087):
each step measures the round's ACTUAL travelled segment — hit/fuse point included — against every
registered aircraft but its shooter's, and reports the pass distance (`WarningShotCue`).
`RegisterAircraft` is the hittability half: the hit ray runs world+aircraft with each round
excluding its own shooter's registered `AircraftBody` by RID.
`ScoredShooters`/`CannonRoundsFired`/`CannonHits` are the wrap-up
board's "Shot %" counters: `Spawn` increments the fired side once per round actually created (the
pool was not full) and `Impact` increments the hit side, both gated on `weapon.IsCannon` and the
shooter's id being in `ScoredShooters` — `GameSession` populates that set with every human seat's
`PlayerIndex` on an Instant Action mission and leaves it empty otherwise, so the counters cost
nothing outside one. `Impact` is CSVM's single choke point for all three of the decode's hit sites:
a cannon round only ever reaches it through `SimStep`'s direct-hit ray, since the fuse/range-expiry
call sites below are both gated to rockets.
A struck plane classifies `Player`
and routes to `FlightController.TakeProjectileHit` (struck shape → part, armor-first damage),
carrying the round's `Shooter` id through so a kill is attributable — never the destructible
pipeline. A missing round may still fuse: the `DETONATION_DISTANCE` proximity fuse arms
against AIRCRAFT ONLY — never world geometry (BL-233: a world fuse detonated every rocket short
of its aimed surface and, with a null collider, locked every hardpoint weapon out of its
per-surface IMPACT entry) — checked AFTER the ray so a dead-on rocket keeps its direct hit, and
detonating at the round's CLOSEST APPROACH to the registered boxes within the swept step
(`AircraftBody.SegmentDistance`), holding while still closing at step end: most rockets author
`DETONATION_DISTANCE` equal to `IMPACT_PROXIMITY`, so a first-entry fuse would always detonate
exactly where the blast reaches zero. The fused-on body rides into `Impact` for
per-surface effect/sound selection, but its damage arrives only through `ApplyDamage`'s gather
(`GatherAircraftCandidates`): every registered plane inside the blast radius — never the shooter's
own, never a wreck — takes the weapon's ARMOR/HEALTH scaled by the splash falloff, measured to the
nearest point of its OWN boxes (`NearestShape`, 0 inside, the blast-neighbor-shape rule) and struck
at that box, so part mapping and kill attribution run the direct-hit path. Planes never enter
`DamageSink`; the destructible blast sphere stays world-masked.
`DamageSink` (→ `AnimRuntime.DamageAt`) turns a world hit into destructible damage —
`WorldDamageGate` (→ `ZeppelinRuntime.GateWeaponDamage`, F18) is asked per struck body first,
so a weapon without `DAMAGES_ZEPPELIN` cannot hurt a gasbag while its impact effect/sound still
play;
`EffectSink` (→ `AnimRuntime.PlayEffectAt`) plays the non-model impact effects, each with the
template basis `EffectOrient` chose — rockets on the runtime's own bound, gun hits under
`GunEffectTtl` 0.3 s (the `*_gunhit` family's longest authored stop, and the only bound the
stop-less slug defs have) and one play per `GunEffectInterval` 0.1 s per effect name = per firing
group (`GunEffectDue`, on the sim clock);
`WashSink` (→ `ScreenFlash.PlayBlend`) is the disabling wash's route to a struck human's pane and
`EngineDeadBounds` the choker's `ENGINE_DEAD` pair, both assigned by the session (null / the image
pair in a lab); `BeeperTags` (→ the session's `BeeperTags<FlightController>`) is the world's tag
list, assigned beside the sinks, read by the `BEEPER` hit's `TryTag` and the `BEEPER_SEEKER` round's
per-frame `PickTarget` (null tags and seeks nothing);
`SurfaceIdOf` is `public static` — the struck body's numeric surface id off `SceneBuilder
.SurfaceIdMeta`, the SAME index space the crash and graze cascades use, with `default`(0) for an
untagged collider (`FUN_005acf60`'s null-material arm) and `player`(6) for a struck `AircraftBody`;
`Impact` reads it and calls `ImpactOutcome.Resolve`, then `Apply` obeys the result and decides
nothing — the first 8
impacts log a breadcrumb of that record (`id/name` + `fx=`/`snd=`/`standin=`), so the probe line and the unit
assertion say the same thing, which is what makes a "these two surfaces look the same" report
answerable without a lucky screenshot (`BL-019`); rockets fly
their FLYOUT model body via `BuildFlyoutBody` (shared with `PylonOrdnance`) and run their FLYOUT
`MODEL_ANIMATION` def from the spawn frame (`ProjectileFlyoutAnim.cs`, resolved from the world
`AnimProgram`, ctor `flyoutAnims`): the trail puffers, the sonic's authored 8.73 rad/s body roll and
the torpedo's launch look all come off that instance. Gun shots add the
`muzzle_burst` secondaries: a pooled per-shot `gunshell` casing instance flying the def's
OBJECT_MOTION verbatim (per-shot nodes on purpose — a shared anchor under `CallAnimation`'s
already-live gate drops gun-rate ejections), the authored `muzzlepuffer` smoke on a gravity-free
sprite pool — 6 puffs the instant-spawn gloss of the def's 0.05 s interval over its 0.3 s window
(`BL-261`; the invented white eject-puff cluster the smoke had been misread as is deleted) —
and a pooled `OmniLight3D` flash from the def's `3rdperson_lts` range/colour variants, its
magnitudes TUNE. `PlaySound`
(FIRE's launch bark, played once per `Spawn`; IMPACT's per-surface hit) resolves a `SOUND_GROUPS`
name (e.g. the incendiary rocket's `ground_mixed_exp_sg` default impact) through `_soundGroups`
first, same as `WorldSounds.PlayOneShot` — `FIRE.SOUND` is null for every cannon in the data
(`LOOPED_SOUND_NAME` covers continuous gunfire instead), so the one-shot never doubles up (BL-211).
**D31 (`BL-370`):** these 8 voices are plain `AudioStreamPlayer`s, never `AudioStreamPlayer3D`, so
A2's per-pane-listener engine rule never touches them — splitscreen has to be applied by hand.
`PlaySound` now multiplies the existing `def.Volume * 0.2f` (the pre-existing tuned balance,
carried verbatim) by `MixGain` (the same equal-power `1/√N` figure `FlightAudio.MixGain` already
gives a plane's own-ship loops) and `DistanceGain` — a linear falloff between the sound's own
`RANGE` (1 at/inside the full-volume distance, 0 at/past the audible one, the same [full-volume,
audible] reading `WorldSounds`' positional emitters give the pair) measured to the NEAREST entry
in `PlayerPositions` (`GameSession.PlayerPositionsSnapshot`, C21's `PLAYER_RANGE` seam — nearest
human, not nearest pane camera, per this plan's seam-choice decision). A shooter's own muzzle bark
and impact read full volume (distance ≈ 0 to themselves); a firefight at the far end of a
splitscreen map fades for everyone else. `PlayerPositions` null (the weapon bench, `Suites.cs`
labs — no human to measure against) skips the term entirely, gain 1. `PlayShotSound` (a turret
gunner's launch bark) takes the same treatment from its firepoint's position.
Positive-`HEALTH_DAMAGE` blasts fall on the original's curve, `1 − d²/IMPACT_PROXIMITY²`
(`BlastFalloff`, fed `WeaponDef.ImpactProximitySqM` and a `DistanceSquaredTo`; `FUN_005acac0`,
docs/org/ordnanceTypes.md "Half two, the splash"), quadratic in distance so 0.75 at half the
radius, zero at it, and applied to both pools; `DAMAGE 0` effect radii never damage. The struck body
always takes full damage (`ApplyDamage`'s direct-hit branch, unscaled by falloff); every OTHER body
the blast sphere overlaps is scored from the nearest point on **its own collision shape**, not its
transform origin (`NearestBlastPoint`, BL-239) — a `GetRestInfo` query against the same sphere with
every other candidate body excluded, so a large neighbour (a zeppelin gasbag, a long building mesh)
is scored by how close the blast actually is to its skin, not by how far the blast is from wherever
its origin happens to sit. Falls back to the struck shape's bounds centre only if that query somehow
finds no contact. The original measures to a per-node bounding sphere instead; the shape is kept
because a Godot body has no such sphere and a chapter mesh's would engulf the map. Planes and world
bodies then share ONE candidate list (`_blastCandidates`), sorted nearest first, and each is
**cover-tested** before it takes its share (`BlastCovered`, C11): a ray from the burst, lifted
`CoverRayLift` 0.1 m along the struck normal so the wall the round hit shields what stands behind it
and not its own side, to the candidate's centre, world layer only (`_coverRay`; aircraft are not
cover, see the field), the candidate itself excluded; any hit drops it. A world candidate's centre
is the **bounds centre of its struck collision shape** (`BlastCentre`, the original's per-node box
centre), read off the physics server (`BodyGetShape` / `BodyGetShapeTransform` / `ShapeGetData`,
bounds cached per shape RID in `_shapeBounds`), never a node origin: a chapter mesh node's origin
sits at ground level, so a ray to it ends on the terrain and every building reads as covered, an
origin-parked root's is the world origin, and a clutter region body (`Clutter.BuildSolidCollision`,
`MapEdgeExtender`) has server-side shapes with no `CollisionShape3D` owner at all, on which the
`ShapeFindOwner` / `ShapeOwnerGetTransform` pair errors and returns identity. At most
`MaxBlastTargets` 32 candidates take damage per burst, the original's hit-buffer size; when more
are inside the radius the pool prints one `blast limit:` line naming the weapon, the burst and how
many were dropped (Decision 9 of PLAN-ordnance-types: never silent; not "cap", which this repo
uses for captures). `MaxBlastBodies` 4096 is only the raw sphere
query ceiling. The fuse tests the whole swept segment per candidate plane (no tunnelling at
~20 m/step) and gates through `DETONATION_DOT_PRODUCT` toward the nearest hull point. The
1 N·s/HP impulse is TUNE (BL-227's open half).

## src/Flight/ProjectileFlyoutAnim.cs
The `FLYOUT MODEL_ANIMATION` half of `ProjectilePool`, a partial-class file. Every ordnance round
runs its own `AnimInstance` of its weapon's def (`he_rocket`, `sonic`, `torpedo_trail`, …) on the
real `SequenceRunner`, and the pool is the `ISequenceHost`: `StartFlyoutAnim` poses the def's
`RESET_STATE`, adds the initial sequences and fires their t=0 events inside `Spawn`, which is where
the original starts it (`FUN_005aef40` hands weapon `+0x110` to `FUN_004edda0`), and
`AdvanceFlyoutAnim` runs the instance each sim step after the round has moved, then ticks its
motions and feeds every emitter it has on. `Dispatch` runs the kinds these defs author against
the round's own model instance: `ObjectActiveState` (node visibility, the torpedo's folded wings
and absent prop), `ObjectScaleState`, `ObjectMotionFromTo` (`FlyoutTween`, `FromToMotion`'s rule
without the world runtime: absolute channels, held components), `ObjectMotion` spins (`SpinMotion`
on a child node; on the model root the rate becomes `Proj.RollRate`, since the flight pose rewrites
the root every frame), `PufferState` (`AcquireEmitter`/`StopEmitter` over the pool's reusable
`TrailEmitter`s, `Puffer.Emit` driving both `DISTANCE_INTERVAL` and `TIME_INTERVAL` states),
`Sound` (`PlaySound` at the round), `CallSequence`/`StopSequence`, and `CallAnimation` (the
`EffectSink`, placed where the round is now). Anything else is logged once and skipped, which
today is the rear-arc flare's `ObjectOpacityFromTo`. Targets bind through the def's symbol table
to the built node's gamez index first (`AnimRuntime.IndexMeta`), the original name second. The
host seam carries no round identity, so `_animSlot`/`_animPos`/`_animBasis` are pinned around each
`Advance`. `PufferState`s are cached per authored event so emitter reuse can match on identity, and
a round with no anim program (the suites' bare pools, the empty stage) runs none of this. Decode:
`org/ordnanceTypes.md`, "The launch look is the def's timeline".

## src/Flight/WarningShotCue.cs
The incoming-fire near-miss cue's shipped accumulator (player.json `warning_shot_max` 2.0 /
`_dissipation` 2.0 / `_interval` 1.0), plus the swept-segment-to-point distance a round's step is
measured with — lifted out of `FlightController` so both unit-test without a live node, the same
split `WeaponCursor` uses. One pass accrues 1.0, saturating at max, draining at dissipation/s; the
cue re-triggers no faster than the interval.

## src/Flight/AiNetFollower.cs
Walks an `AiNet` patrol graph as a waypoint stream. Given the vehicle's nose it is the decoded
walk (`FUN_00431e40`, `docs/org/aiPilot.md`): seat on the nearest node and fly the far end of the
edge whose leg best lines up with that nose, then the same pick at every arrival with the edge just
flown excluded. Nothing draws, so vehicles seated on one node facing one way leave it together,
which is what makes a group sharing one net fly in formation (`BL-498`). A caller with no nose
(`ZeppelinMotion`, and a zeppelin is not a vehicle in the original) keeps the older nearest-node
seat with branches drawn from its own seeded `Random` (per
plane off the `Rng.Ai` stream at spawn, never Godot's global rng). Aircraft-agnostic on purpose:
positions in, target node out; its two consumers are `AiPilot.Patrol` (aircraft) and
`ZeppelinMotion` (F17's kinematic node follow). Arrival is the decoded ALONG-LEG test — a tenth of
the leg's horizontal length, floored at 10 m (`docs/org/aiPilot.md`) — so a vehicle that cannot
turn tightly enough flows past its node instead of orbiting a capture sphere it never enters.
A zeppelin raises that floor to clear its own turning circle, which is invented and only a floor.
Also holds a net's live STOP-POINT flags, seeded from the file and rewritten by
`COMPLETED_STOPPOINT` through `SetStopPoint`: an armed node is never advanced past, and once the
walk is inside 30 m of it (`StopPointHoldM`, the zeppelin follower's own hold distance, not the
leg's arrival radius) `Holding` goes true. ⚠ Off by default — `observesStopPoints` is set only by
`ZeppelinRuntime`, because only the zeppelin follower reads the flag; the aircraft one reads a
node's danger-zone fields instead (`docs/formats/ai-nets.md`).
`StopsAt`/`Holding` also cover a STRUCTURAL dead end — the current node's only edge is the one just
flown — for an observing follower: decoded (`FUN_004bf9d0`'s own-node gate calls the level/hold
routine, `FUN_004bf500`, unconditionally, ahead of and regardless of any armed stop point), this is
what stops a zeppelin on an open route whose far node authors no stop point at all from re-picking
its only neighbour and shuttling the route forever (`BL-529`, `docs/formats/mission-entities.md`
"Route ends and stop points"). The aircraft follower keeps its own decoded turn-back at a dead end
(`PickOnward`'s degree-1 short-circuit) unchanged; the unconditional hold is opt-in on
`ObservesStopPoints`.
Pinned by `AiNetFollowerTests` + the `ai-net-follow` suite.
`ArrivedNode` is the danger-zone report: the node the last `Update` reached and stepped past,
which `AiPilot` reads for its tag, never the node being flown toward.
`Reseat` is the original's activation snap (`FUN_004b0f40` into `FUN_00432010`): an Instant Action
wave member ticks while it is inert, presence being no sim gate, so without it the member's first
update latches a node near its parking pose and it flies back there after the teleport (`BL-364`).
It optionally names an edge the next seat pick must refuse, which is how the danger-zone
exit continues the course: the original's exit (`FUN_00490590`) hands `FUN_00431e40` the edge id
the walk was on when the run began, so a racer set down by the ribbon beside its own entry node
cannot fly that leg again and re-lock the zone it just flew (`BL-615`). An undirected neighbour
list says the same thing by naming the edge's far end; the exclusion is spent on the seat it
applies to.

## src/Flight/DangerZoneRibbon.cs
The decoded danger-zone run (`docs/org/aiPilot.md` "The danger-zone run"), engine-free:
`DangerZoneRibbon` is one `dzpathN` route as the original builds it, the polygon's vertices joined
by cubics parameterised in metres plus the lane table (the zero lane and one per child node);
`DangerZoneRun` is a pilot's cursor on it (entered from the nearer end, walking the segments either
way, `Done` past the exit); `DangerZoneRail` is the state-5 integrator that writes the pose off the
ribbon in place of the flight model, closing the aeroplane's residual offset, banking the wings into
the bend and settling on the 155 mph cruise. Every constant is read out of the image and named at
its declaration. Pinned by `DangerZoneRibbonTests`.

## src/Flight/DangerZoneRibbons.cs
A mission's ribbon set read straight off the chapter gamez, independent of `--debug-dzpaths`: every
`dzpathN` node's route polygon by the route-versus-gate-pair material rule (`docs/formats/missions.md`,
never polygon index), its children as lanes, and `dzones.zrd`'s `disable` list as the inactive
flag. `ByIndex` serves a numbered net tag, `NearestEnd` the negative one. One instance per session,
shared through `AiPilot.DangerZones`, because lanes are occupancy-counted across pilots;
`CampaignDirector.Attach` builds it and hands it to every roster pilot.

## src/Flight/ZeppelinBroadside.cs
The pure zeppelin broadside law (M4 F19), engine-free: the decoded 90° arc
(`dot(toTarget, sideNormal) > 0.707` against the MOVING hull's lateral axis, `SideNormal`/
`TargetSide` — side alternation is geometric, the opposite cones never both bear, no cadence
invented), the per-cannon stowed→deploy→ready→fire machine (`Step` emits deploy/retract/
ready lists; deploy/retract durations come from the authored anim defs) with its own re-fire
timer (`cannon_fire_delay`, armed by `Fired` per cannon), `TryAim` (the intercept solve,
`AimAssist.TryIntercept` consumed), `PickGasbag` (the zeppelin-vs-zeppelin rand() pick over
the target's in-arc live gasbags), `FirstLiveTarget` (the decoded candidate walk over the
record's `targets` in authored order, `player` resolving like any zeppelin node, no team or
hostility read) and `CannonsEngaged` (the decoded zeppelin byte `+0xc`: off at construction,
written only by the script's `COMPLETED_ZEPCANNONS`; while clear `Step` deploys nothing and
retracts any ready cannon outright, which is why no shipped broadside ever fires on the player;
`formats/mission-entities.md` "Broadside firing" has the chain).
`Session/ZeppelinRuntime.Cannons.cs` wires it. Pinned by
`ZeppelinBroadsideTests` + the `zeppelin-broadside` suite.

## src/Flight/ZeppelinDamage.cs
The pure zeppelin kill arithmetic (M4 F18), engine-free: `Survivors`/`IsDead` over the record's
`healthy` list (counted literally, entry by entry — C5/M01 ships `gasbag5` twice), the
`AliveEngines` recount, `MayDamageGasbag` (= `WeaponDef.DamagesZeppelin`, the gasbag-only
routing gate) and `CrossedStages` (the `cannon_health` 0.6/0.3 stage list, injure_anims
semantics: every crossed threshold fires, once). The zone pools live in `DestructibleRegistry`;
`Session/ZeppelinRuntime` supplies the aliveness views. Pinned by `ZeppelinDamageTests` + the
`zeppelin-damage` suite.

## src/Flight/ZeppelinMotion.cs
The kinematic zeppelin motion law (M4 F17): flies a `ZeppelinDef` along its net through
`AiNetFollower`, forward-only along the facing, speed by `max_accel` toward `max_speed`, yaw and
pitch through the decoded steer law (`Steer`, `FUN_004bf530`/`FUN_004bf620`, one routine per
axis): the target is the bearing and the raw slope from the hull to the current node, the rate
asked for is the record's `max_rate_*` eased to `max_rate · (error/25°)²` inside 25° of error
(`EaseRad`), the live rate moves toward it at `accel_*`, and the angle advances by that rate
scaled by `speed / max_speed` (the authored one), so a stopped hull cannot turn. No per-step
pitch band: the record's ±30 is a degree-valued pair the original compares against radians, so
it neither clamps the initial pitch nor the drawn pose (`FUN_004bf950`), and the law here has no
clamp either (`docs/formats/mission-entities.md` "Steering"). ⚠ The bang-bang rate this
replaced (ask for `error/dt`, reach it at `accel_*`) rang up under `accel_pitch` 0.5°/s² into a
standing ±30° swing on level legs, which read at the controls as the Pandora diving along its
route; the ease is what damps it. Pure state — no Node, no flight model; `ZeppelinRuntime` writes
the pose onto the world node, and `ResumeAt` re-seats it in place after a scripted motion.
The stop-point half is the decoded approach (`FUN_004bf360`): full speed until the along-facing
range to a halting node falls under 250 m, then linearly down to zero; inside the follower's
30 m (`Dock`) the throttle is cut, the pitch holds, and the hull and heading decay onto the node
and the leg's bearing at e^(−0.2·dt), which is what closes the last metre onto the hold sphere.
Once the follower is `Holding`, a station-keep: pitch commanded to 0 and heading kept, both
frozen by the speed factor at speed 0. Pinned by `ZeppelinMotionTests` + the `zeppelin-motion`
and `zeppelin-pandora-dead-end` suites.

## src/Flight/AiPilot.cs
The non-player `FlightModel` driver: standing orders in (heading in the mission-data
`SpawnPoint.HeadingDeg` convention, altitude, throttle, optional `Patrol` net follower, optional
`Gunner` whose live target is chased at the decoded lead offset ahead of it, optional `Machine` —
D11's nine-mode state machine, which when set is stepped first and picks this step's AIM POINT and
parameter table: patrol flies the net node, a reached node's danger-zone tag starts a
`DangerZoneRun` (approach on the emergency table, then `RailPose` published for the host to apply
in place of the model step, then the net re-seated), pursue leads the gunner's target on
the engaged table (or aims at it outright for the head-on firing solution), lay off holds its
entry course and then walks the throttle toward `sixth_sense_factor` × the pursuer's speed so the
human catches up, evade flies the machine's orders, avoid crash aims 1000 m up on the emergency
arm, displaced 1000 m right of its own ground track (`ClimbOutBreakM`, invented and measured) for
a netted pilot but purely vertical for an ESCORTING one, which is the decoded aim both of the
original's laws build,
an evasive maneuver plays its `ManeuverExecutor`, stunned returns neutral sticks),
and an optional `Escort` (`AiEscort`) which, whenever its leader is in play, takes the dispatch
away from all of those but stunned and avoid crash, the original's own `mode wingman` fork,
one `FlightInput` per sim step out, read by a `FlightController` whose `Pilot` is set. Pure over
the model state and its own fields, seeded randomness only, so a fixed-dt run is deterministic
(`AiPilotTests`). The original's own steering law is `AiControlLaw`; this class is only its driver
(docs/org/aiPilot.md). `Stun(seconds)` is the AI stun's entry (`FUN_004200d0`, reached by a
`SONIC`/`FLASH` burst and the smoke screen through `FlightController.TryStunPilot`, which holds the
victim guards): the mask is neutral stick and rudder with the throttle lever left where it was, the
three channels the original zeroes and the one it does not, so the aircraft stays on the flight
model and coasts under power. With a `Machine` the stun is its `Stunned` mode; without one the pilot
keeps its own countdown; `IsStunned` reads either and `ClearStun` is the respawn reset.

## src/Flight/AiControlLaw.cs
The original's own AI steering law, decoded as plan D31 in `docs/org/aiControlLaw.md` — read
that page before changing anything here. An aim point, that point's velocity and one of four
parameter tables read out of the image in, one `FlightInput` out: a desired speed from the aim
point's own speed plus range-weighted lead terms, an intercept solve (`AimAssist.TryIntercept`) for
the direction, bank-to-turn with the elevator joining once the bank command is inside a deadband, a
wings-level rule, a low-speed unload, and a per-axis scale/limit stage off `PlaneStats`. The
throttle lever has one path, the walk toward the desired speed; the original's distance-gated
open-loop branch is not ported (`docs/org/aiControlLaw.md`'s throttle section says why).
Engine-free and pure over its arguments; pinned against the decode by `AiControlLawTests`.

## src/Flight/AiEscort.cs
The formation-escort law a netless `mode wingman` aircraft flies, which in the shipped data is the
campaign's `wingman_N` / `bswingman_N` roster blocks and nothing else (`docs/org/aiPilot.md`, "The
escort law"). A leader snapshot (position, attitude basis, velocity, whether it is the player) and
an optional target snapshot in, one station point and that point's velocity out, over the engine's
own five-state machine: close on the leader, hold the body-frame station, fly a station on the
target, and the two re-join states nothing in the law enters. Every constant is decoded, the two
stations included ((6, 0, 18) off the player, (8, −2, −8) off an AI); the 80 m separation push is
what makes the hold a weave rather than a tight join. Engine-free and deterministic, holding only
its state and last station; the driver is `AiPilot.Escort`, the table `AiLawParams.Wingman`, and
the live check is the `wingman-station` suite. The campaign hand-off is not a join problem: at the
first stepped frame after C3/M01's intro the wingman reads 117.6 m and 53.6 m/s against the 700 m /
20.576 m/s gate and is in `Station` on the next frame, so a wingman that ends up high and behind
left the escort law rather than never entering it.

## src/Flight/AiModeMachine.cs
The nine-mode AI state machine, owned by `AiPilot.Machine` and stepped from its `Next`:
the mode list and vocabulary are the engine's own debug-readout dispatch (`NameOf` returns them
verbatim; docs/formats/ai-rosters.md "AI modes, engine-side"). Decoded and wired: activation
into pursue inside `min_ai_active_dist`/vehicle `attack` (both 2000 shipped, `AiSkills`/
`PlaneStats`); a hit rolls steady-hand (`NotifyDamage`, called from
`FlightController.TakeProjectileHit` for AI planes) and a FAILED test breaks off; a pursued AI
target entering an evasive state rolls sixth-sense and a FAILED test stuns for
`stun_recovery_interval`; `Stun(seconds)` is the one stun entry (the original's `FUN_004200d0`,
shared by that roll, a `SONIC`/`FLASH` hit and the smoke screen): it enters `Stunned` from ANY
mode, dropping a running maneuver or climb-out, and a stun landing on a stunned pilot OVERWRITES
the remaining time (clock + seconds, no max), which is what lets the smoke screen refresh it every
frame; the mode clears on its own clock and returns to the mode it interrupted, and an external
`Enter` releases it with no stale expiry; an evasive maneuver is an `EligibleFor`-culled, signature-weighted,
seeded library draw played to `ManeuverExecutor.Done`, then back; `lay off` is D15's rubber-band
assist (decoded: the mode and `sixth_sense_factor` 0.994→1.07, "the ease-off while pursued") —
pursue eases into it when a chasing HUMAN target has fallen behind, and it releases when the
pursuer catches up or stops chasing; `AssistEnabled` false (`--no-assist`) never enters it.
Transitions raise `ModeChanged` (the session's `ai mode:` log lines); rolls raise `RollLogged`
in the engine's pass/fail wording. `avoid crash` runs the original's three altitude bands: below
`AltitudeFloorM` (20) the climb-out arms with no ray at all, above `ProbeCeilingM` (8000) nothing
is cast and a running one releases, and between them ONE ray along the aeroplane's own velocity,
cast every 0.5–1.0 s per plane, decides, releasing on the first clear ray (docs/org/aiPilot.md).
Engine-free; pinned by
`AiModeMachineTests` + the `ai-modes` suite. Named inventions (evade's scramble run, the probe's
minimum reach, lay off's entry/exit cones) are marked at
their own declaration. The two danger-zone modes are entered only by `AiPilot`, off a reached net
node's tag; `approaching` still runs the crash check and hands back to itself, `navigating` runs
nothing (the pose is the ribbon's), and a stun or climb-out out of either returns to the approach.

## src/Flight/ManeuverExecutor.cs
Plays one library maneuver's timed step program as `FlightInput` values — `Next(model,
dt)` each sim step until `Done`, consumed the way `AiPilot` is; D11's state machine holds one per
`evasive maneuver` run and switches back to its own law on `Done`. Steps are TARGET ATTITUDES in
degrees (not stick deflections, not rates), composed onto the entry frame — level entry-heading
frame normally, the full entry attitude for a `relative` maneuver; a positive-duration step is
held for its time, a zero-duration step advances when the attitude is captured. Pure and
engine-free; deterministic on a fixed dt (`ManeuverExecutorTests` demonstrates a real Bloodhawk
flying the shipped dive and split_s).

## src/Flight/AiGunner.cs
The AI's forward-gun gunnery: per sim tick the host `FlightController` hands it the
fire geometry (`Solve`), it answers with the trigger (`WantsFire`) and the intercept, and each
round leaves along `ShotDirection(muzzlePos)` — the line from THAT barrel to the intercept point
(wing guns converge; parallel lines straddle a fuselage) perturbed inside the dead-eye cone, one
seeded draw per shot. Gates in the engine's order: the quick-draw cone off the TARGET's nose/tail
axis, the separation inside the slot's authored engagement window (1–900 m on every AI gun), then
the airframe's ±11° `gun_pitch`/`gun_yaw` clamp on the lead (`AimAssist.TryIntercept`, consumed
never re-derived) with the residual the clamp leaves gated at 10°, so the employable cone is the
traverse limit plus the gate (`docs/org/aiPilot/aiWeapons.md`).
Engine-free (`AiGunnerTests`); the live half is the `ai-gunnery` suite.

## src/Flight/AiRocketeer.cs
The AI's ordnance employment, the gun path's twin (`docs/org/aiPilot/aiWeapons.md`): per sim tick
the host `FlightController` (`DriveAiRocketeer`) ages the vehicle-wide lockout and hands over the
fire geometry (`Solve`), which answers with the trigger (`WantsFire`), the hardpoint it chose
(`SelectedPylon`) and the direction the round leaves along (`LaunchDirWorld`, the clamped mount aim
rather than the raw lead). Gates in the engine's order: the quick-draw cone aborting the whole pass,
then per pylon the armed check, the two-way `DAMAGES_ZEPPELIN` match, the squared engagement band
and the traverse clamp's residual against `AimQualityCos` = 0.9962, cos 5°. That constant is the
ordnance half of a decoded pair and is deliberately not shared with `AiGunner.AimQualityCos` = 0.9848,
cos 10°: a lead 18° off the nose clamps to 11° and is taken by the gun and refused by the ordnance.
The lockout is stamped BEFORE the `quick_draw_chance` roll, so a failed roll spends the whole refire
interval instead of retrying next tick.

**The lead is solved in the frame the round flies in, per pylon** (`org/aiPilot/aiWeapons.md`, the
branch table under the lead solver). A pylon whose weapon authors `ACCELERATION` (`RoundAccel`,
`wep_04` among the AI's ordnance) takes `TryMotorIntercept`: the round starts at rest in its
launcher's frame and climbs to `VELOCITY` at `ACCELERATION`, so the intercept is where the
separation from the target's RELATIVE track equals the path flown, `0.5·a·t²` inside the ramp and
`VELOCITY·t − VELOCITY²/(2a)` past it, bisected where the original roots a polynomial
(`FUN_00462ce0`). A pylon without a motor takes `AimAssist.TryIntercept` at `VELOCITY` against the
target's WORLD velocity, since such a round is seeded at its cap rather than off its launcher
(`FUN_00460e30`, the world-frame call). The difference is large at the shipped numbers: `wep_04` at
450 m/s off a 150 m/s² motor needs 3 s to reach its `VELOCITY`, and a 600 m shot at a 100 m/s
crosser leads 26.5° where the constant-speed solve leads 12.8°. `AiGunner` keeps the relative-frame
constant-speed solve, which is the original's third branch, the `CANNON` one.

Three properties of the original hold here by construction rather than by a test. The aim gate is
skipped for the player, and this class only runs for a non-human pilot, so player fire never reaches
it (a human's rocket leaves along the aircraft's own axis through `FireControl`). It holds no target of its
own, mirroring the original's single validated target across the whole weapon walk. And the
`DAMAGES_ZEPPELIN` match's zeppelin side is unexercised in play: the AI acquisition admits aircraft
alone (`BL-363`), so `targetIsGasbag` is always false at the call site, and no shipped stock loadout
carries `wep_14` because the vehicle def's `weapons` tuple is not parsed yet (`BL-394`). Both halves
of the match are pinned in tests against the Black Hat Warhawk's authored fit instead.
Engine-free (`AiRocketeerTests`).

## src/Flight/AiVoiceDispatcher.cs
The E16 trigger dispatch, engine-free (`docs/formats/combat-voice.md`): events in, (speaker,
clip, outcome) decisions out — the talker roll, the hardcoded halving on bearing ids 1–12, the
broadcast speaker election (one line per event, a failed roll passes to the NEXT candidate,
wrapping), the DI tiers at 70/50/30 % most-severe-first, the death cries (20/21) with force, and
`BearingTriggerId` = 1 + 3·bearing + band. Availability comes from the injected resolver
(`CombatVoice.PlayableFor` + `HasStream`), never def presence. Pinned by
`AiVoiceDispatcherTests` + the `ai-voice` suite.

## src/Flight/AiTargetRanking.cs
The decoded target-ranking formula (docs/formats/ai-rosters.md "AI modes, engine-side"):
`rank = weight × 1200 + distance + objectiveBias`, MINIMISED; player base weight 0.7, others
1.0, ±0.2 weight terms for bearing / altitude sign / target facing, `1e21` beyond the
activation radius (never picked). Snapshots in, index + `TargetScore` out, engine-free
(`AiTargetRankingTests`); `SelectBest` prefers the best candidate no ally already holds and
falls back to the overall best when the pool is exhausted (the design's deconfliction, minimum
reading); `ObjectiveBiasFor` matches `rating_biases` patterns, first match wins, and saturates at
both ends — 1.0 or more is always-target, −1.0 or less is the exclusion.

## src/Flight/IncomingFire.cs
`--incoming[=metres[,wep_id]]` — the near-miss test rig: a phantom shooter 120 m on each player's
six, alternating sides, firing the target's own gun (or the named weapon) into the shared pool under
a shooter identity no player holds. Exists for deterministic near-miss testing: an AI gunner
(D14's `--ai-attack`) is a real shooter but aims to hit, and a splitscreen pilot needs a second
human. Aims along the target's own nose with a lateral offset, so the round overtakes on a
parallel track and the pass distance holds without lead maths.

## src/Flight/PhysicsConstants.cs
`PhysicsConstants.NomGravity` — the single 20 m/s² player.json `nom_gravity` value, shared by
`PlaneStats.Gravity`'s default and `ProjectilePool.WorldGravity` so the two can't drift apart.

## src/Flight/PlaneStats.cs
Typed per-plane stats: vehicle.json `dynamics` (resolved through the `kind_of` def chain), the
def's `turrets` block as `TurretMount`s (title + node per viewpoint rig — the host→`ai.zrd`
gunner link) +
engines.json stock engine power + player.json globals (the flight constants, the near-miss cue's
`warning_shot_*` block, the gun aim assist's `sticky_bullet_catchup_rate`/`_forget_interval`
(`AimAssist.cs`'s B2), `_dist_factor` (B4's scoring) and `_inaccuracy` (B5's launch scatter, stored
in RADIANS as the original stores it), the Cockpit head's `autohead_turn_time`/`_turn_max`/
`_turn_min_pitch` (C22, `HeadLook.AutoheadTarget`'s constants — `turn_max`'s authored-vs-default
asymmetry, docs/formats/vehicle/player-globals.md), plus the decoded model's
lift/AoA/G, turn/yaw-curve, pitch-fade and drag-fade-speed globals — docs/org/flightModel.md; converted
exactly as the original does: MPH×0.44704, AoA/liftAOAs cosined, highGs/lowGs raw G; the turn/yaw
curves, the pitch fade, the G limiter and the AOA window are all live in the model, the first two
neutral on the authored values and the window binding on all of them), the `crash` block's
`bounce_factor` (a raw scalar, read one level down inside that block
— the collision restitution's ceiling), the `engine_sound` def name with its
volume/pitch `SoundCurve`s (clamped two-point ramps), `destroyable_parts` → `DestroyablePart`
records (name, max HP, max armor, `critical`/`engine` flags, `got_hit_anim`, per-part
`injure_anims`), the def-level `VehicleInjureAnims`, and the `collision` probe list as
`CollisionProbes` (nearest def in the damage chain: the player def's six points, or
`basic_airplane`'s single origin probe on every AI load, which is the shape `FlightController`'s
AI sweep carries). Schema: docs/formats/vehicle.md.
Two flavours of one airframe: `Load` resolves everything down the player chain, `LoadForAi` takes
  ONLY the damage model (pair, parts, def-level ladder) from the AI def's own chain — the player
  def's name minus its leading `p`, validated — and leaves `DefName`, dynamics, turrets and the
  model on the player chain. An AI aircraft is therefore **zone-less**: an authored `armor`/`health`
  pair and NO `destroyable_parts`, which is what every roster-named def chain resolves in the
  shipped game (`docs/org/vehicleDamage.md`'s 2026-08-16 correction). `DefName` stays the player
  def on both flavours on purpose (`AiDefName`'s own doc); `damaged_engine_sound` and
  `cockpit_engine_sound` both parse fully — `EngineAudioCurves.EngineDefFor` is what selects
  between them and the plain `engine_sound` (see `src/Flight/EngineAudioCurves.cs` below).
  `LoadForAi` also reads the AI chain's `title` message key into `AiTitleKey`, the authored name a
  militia def carries ("MSG_VEH_MEDUSA_KESTREL") and a plain def inherits from its airframe;
  `AiFlightAssembler` resolves it into `AiTitle` for the targeting readout.
  `VehicleMode` carries the def chain's own `mode` key, and `WithAiSpawnJitter` is gated on it: the
  original jitters the `jet` and `heli` classes only, so the `mode wingman` family flies its authored
  dynamics (`docs/org/flightModel.md`, "The per-spawn jitter").

## src/Flight/SpawnPoints.cs
Reads the flight spawn from a mission's OWN zrdr (`extracted/<chapter>/<mission>/zrdr/` — a
different archive than the shared `--zrdr`), two schemas both yielding
`SpawnPoint(Position, HeadingDeg)`: `LoadIa` (instant-action ia.json `spawn_points` per scenario;
only IA1 folders have one, the original picks one at random per launch) and `LoadPlayerInit`
(story objectives.json `PLAYER_INIT`, position + yaw). Schema: docs/formats/spawns.md.

## src/Flight/MissionTargets.cs
Loads a mission's targets.json: target KEY → objective display keys
(`description`/`category_label`/`help_label`), resolved through `Messages`, plus the valueless
`objective`/`other_target` marker flags a mission starts with. A bare node name keys itself and a
nested `[parent, child]` entry keys `parent/child`, the same key `ObjectiveTarget` gives the
script's target directives, so the two tables meet on one string. `Load(mission, chapter)` is the
original's reader search path (`init.gw`'s `RdrAddPath` chain): the mission's own file, else the
chapter's, one file whole and never a merge. ⚠ Read both scopes for a campaign mission: C1C/M01
ships no targets.zrd and every one of its objective labels sits in `C1C/zrdr/targets.zrd`.
Generic across mission types; no file in either scope yields an empty set. `ByNode` exposes the
whole table for a consumer that needs the starting flags rather than one key's labels. Schema:
docs/formats/missions.md.

## src/Flight/MarkerDraw.cs
The world marker's drawing primitives, shared by `MarkerHud` and `TargetHud`: the
shadowed reticle, the edge arrow with its tail stroke, and the centred text block (`Lines`) with
the pane-clamped variant (`LinesClamped`) an off-screen marker needs. Owns the marker blue and
the drop shadow; colour and scaled sizes stay with the caller, since each HUD scales through its
own `HudMetrics.Scale`. Where a marker GOES is `EdgeMarker`'s; this is only what it looks like.

## src/Flight/StuntMission.cs
Stunt Flying state: `Load` builds the ordered zone list from ia.json `dzones` (HUD positions via
`GameZ.WorldTransformOf`, gate polygons from `dzpathN`; strings via MissionTargets + Messages; null
when a mission has none → free flight); `Update` requires both polygon-plane crossings in either
order, fires events, advances the target; clock/scoring via
`Elapsed`/`CompletedAt`/`CompletionOrder`/`InCompletionOrder`; `ForAnotherPlayer()` clones an
independent run so the archives parse once per session. Logs through `Log` (`Utils`), not
`GD.Print`/`GD.PushWarning` (`engine-free-suites` A2) — the class itself has no other engine
dependency, which is what lets `stunt-gates` move off-engine in A4.

## src/Flight/HudMetrics.cs
The one place the flight HUD decides how big it draws: `Scale(control, reference = 1440)` =
window height / reference, damped by `PaneFactor` = sqrt(paneH/windowH) inside a splitscreen
pane (2P ≈ 71 %, 4P 50 %; the damping exponent is TUNE). CompassTape, GaugeCluster, MarkerHud,
StuntScoreboard and FlightController's text block all route through it.
`hud.statusTextScale` and `hud.markerTextScale` multiply only their matching flight-HUD text,
clamped from 0.5 to 2.0; arrows and layout remain at the base scale.

## src/Flight/HudFont.cs
The game's own HUD bitmap font, rebuilt from `extracted/rimage/5pointhud.png` (+ the brighter
`5pointhudbrite.png` highlight variant): a proportional 5-px font covering printable ASCII
`0x20`–`0x7e` (layout/colours: docs/formats/hud.md). `Load` returns null (one log line) if the
atlas is absent; `Draw(CanvasItem,…)`/`Measure` render onto any caller's canvas, sized via
`HudMetrics`. The E34 foundation E35/E36 draw with.

## src/Flight/WeaponReadout.cs
The selected-weapon text readout: a bottom-centre two-line `Control` drawing the current gun
group + rocket type and their live ammo in `HudFont`, from the game's own `MSG_HUD_GUNGAUGE` /
`MSG_HUD_MISSLES` templates (`Messages.Fill`, never hardcoded). FlightController pushes the state
each frame (`%1` = gun mount name / rocket display name, `%2` = per-group / per-pylon rounds).

## src/Flight/ImpactReticle.cs
The gun aiming reticle: a viewport-filling `Control` drawing `impact_point.png` (the game's
pipper, from `extracted/rimage/`) at a world impact point fed each frame by FlightController,
projected via `Camera3D.UnprojectPosition` at `_Draw` time (mirrors MarkerHud, never cached).
Fixed screen size scaled by `HudMetrics`; one per player pane. What the pipper follows — the nose
axis at 0.5 s of flight, range-smoothed, deliberately never the assist's line — is decoded in
docs/org/aim-assist.md "What the gun pipper follows".

## src/Flight/EdgeMarker.cs
The off-screen edge marker's placement rules, engine-free and pure: `Resolve(projected, behind,
paneSize, margin)` answers on-screen vs edge-clamped (the margin-inset rect test, the
behind-the-camera mirror, the degenerate-direction fallback, the clamp along the direction to the
inset boundary) as a `Placement`, and `ClockHour(ownPos, headingDeg, targetPos)` is the "N o'clock"
bearing. Owns `RefEdgeMargin`. The camera stays with the callers — `MarkerHud`, `VersusHud` and
`TargetHud` project through their own pane's camera and keep their own arrow/tag/label styling.
Off-engine coverage: `CSVM.Tests/EdgeMarkerTests.cs`.

## src/Flight/MarkerHud.cs
The stunt objective marker HUD: a viewport-filling `Control` drawing the on-screen reticle/text
block, the off-screen edge arrow (`EdgeMarker.Resolve` + clock-hour bearing), run status and banners
(`CompleteBanner` branches solo vs race); one per player, sized via `HudMetrics.Scale(this)`.

## src/Flight/StuntScoreboard.cs
End-of-run results overlay: plain Godot UI (dimming backdrop → CenterContainer →
PanelContainer → VBox + 3-column split grid) filled from `StuntMission.InCompletionOrder()`;
wakes on `RunCompleted` (records via `ScoreStore.RecordIfBest`, logs the split table to stdout
for headless review), branching NEW BEST vs BEST on the stored record. Raises `HaltReason.Ended`
and carries a Restart · Exit `BoardMenu`; `_Process` releases both once `AllComplete` clears, so
R and pad Y reach the rerun without going through the menu. Not built while Instant Action is
active — `IaWrapupBoard` carries the splits there instead (`BL-358`).

## src/Flight/ScoreStore.cs
Stunt best-time persistence: one JSON object in `user://stunt_scores.json` keyed
`chapter/mission/plane` → `{best, date}`; `GetBest` / `RecordIfBest` (returns whether it was a
new best — never worsens a record).

## src/Flight/StuntRace.cs
The internal `Remove` operation is compensation for an uncommitted roster build, including removal
of that racer's completion subscription; normal race membership remains append-only.

Splitscreen race bookkeeping: one `Racer` per player (own `StuntMission`, `Rank`, `FinishTime`);
finishing stamps the next placing, `RaceCompleted` fires when the last pilot is in; `Standings()`
orders finishers by placing then in-flight players by progress; `Restart()` (rematch) resets
every mission and clears placings — the planes are respawned by GameSession, which owns them.
Its console lines, and `StuntScoreboard`/`StuntRaceBoard`'s, route through `Log.Info("flight", …)`
instead of a bare `GD.Print`. Off-engine coverage:
`CSVM.Tests/StuntRaceTests.cs` (finish ordering, rematch reset, standings ties).

## src/Flight/StuntRaceBoard.cs
The race's shared ranked results overlay: same clean-Godot-UI construction as StuntScoreboard,
but covering the WHOLE window — on its own CanvasLayer (Layer 10, above SplitScreen's 0) under
the session root, one row per player from `StuntRace.Standings()` (placing, tag, plane, zones,
total + gap to the winner; DNF when unfinished). Wakes on `RaceCompleted` and raises
`HaltReason.Ended`; `_Process` hides it and releases the clock once `AllFinished` clears, so R and
pad Y reach the rematch without going through the menu. Carries a Restart · Exit `BoardMenu`
driven by player 1, Exit's label following how the session was launched.

## src/Flight/VersusMatch.cs
Dogfight deathmatch bookkeeping: `RegisterKill(shooter,
victim)` scores the shooter and tallies the victim's death, `RegisterDeath(victim)` tallies a death
alone (terrain/mid-air — no killer, no score change); `Advance(dt)` is the host-fed match clock;
`MatchCompleted` fires once on kill threshold or time-out (leader wins, equal top kills draw);
`Standings()` ranks by kills descending with ties sharing a rank; `Restart()` (rematch) zeroes every
score and re-arms completion. Off-engine coverage: `CSVM.Tests/VersusMatchTests.cs` (threshold win,
time-out win, draw, post-completion no-op, rematch re-arm, each limit disabled on its own).

## src/Flight/VersusHud.cs
Per-pane Dogfight HUD, `--vs` only: a compact status line — remaining
time (omitted once `VersusMatch.TimeLimit` is disabled), this pane's own K/D, and the current
leader's tag — drawn in MarkerHud's run-status slot (`RefStatusY` — Stunt and Versus are mutually
exclusive, so the two never compete for it); a transient "P2 DOWNED P3" kill banner ("P3 DOWN"
with no killer); and one opponent marker per living rig (`Rigs`, excluding `PlayerIndex` and any
`Controller.Crashed` seat) — an on-screen tag at the projected point, or the edge-arrow +
clock-hour bearing (`EdgeMarker`'s placement) when off screen/behind, in that
opponent's own `SplitScreen.PlayerColor`. `Build(match, playerIndex, camera)` binds the match +
this pane's own camera (opponent markers project through it, exactly like MarkerHud's zone);
`Rigs` is attached once by `HumanFlightAdapter` (the SAME live list `GameSession` keeps, not a
snapshot — every seat exists before this pane assembles, only `.Controller` fills in as siblings
do); `PlanePos`/`HeadingDeg` are fed every frame by `FlightController`, same site as `Marker`'s.
The status line pulls the match live every `_Draw` (no pose to project for it) and `OnKill` is
pushed once per `Downed` report by GameSession's own broadcast — a second subscription, never
piggybacked on the scoring one, so every pane hears every kill/death, not just the two it
happened to.
The single-target marker whose shape this reuses is `TargetHud`'s; the original has no per-opponent
marker at all, so drawing one per human opponent is CSVM's own splitscreen answer to its radar.
The shape's provenance is decoded in [`org/targeting.md`](org/targeting.md).

## src/Flight/TargetHud.cs
The per-pane targeting HUD, built on EVERY human pane in every flight session (`--vs` panes get one
alongside `VersusHud`): the pilot's own selected target from `TargetSelection` (a campaign
mission's objective sites included, since they ride that same selection), a nearest-AI-hostile
fallback where no selection exists, and `--debug-markers`' every-live-aircraft overlay. Draws the
original's bracket box, label block and off-screen edge arrow + clock bearing, and owns the colour
table (`MarkerColor`), the label layout (`LabelLines`), the selected gun's reach gate (`GunReaches`,
fed by `FlightController.GunReachesTarget`) and the debug identity string (`DebugTag`). The marker's
decode is [`org/targeting.md`](org/targeting.md); the edge placement and clock bearing are
`EdgeMarker`'s, only the styling is this HUD's own. Pinned by the `hostile-marker-hud` suite,
`HostileTagTests` and the `c1-targeting-hud` golden.


## src/Flight/VersusBoard.cs
The dogfight's shared results overlay — `StuntRaceBoard`'s construction
almost verbatim: winner (their own `SplitScreen.PlayerColor`, or "DRAW" on a tie) on top, then one
ranked row per player (tag, kills, deaths) from `VersusMatch.Standings()`, covering the WHOLE
window on its own CanvasLayer (Layer 10, above SplitScreen's 0) — the match ends for everybody at
once, unlike a per-pane HUD element. Wakes on `MatchCompleted`, hides in `_Process` once
`Completed` clears (a rematch); `Populate` runs ONLY from `OnMatchCompleted`, so the drawn rows
stay the ones the match actually ended with even after `Restart()` zeroes the live state —
`StuntRaceBoard`'s `FinishTime`-snapshot discipline, achieved here for free since `VersusStanding`
is a value-type snapshot already. Raises `HaltReason.Ended`, so the world stops rather than leaving
the losers to fly under a board that has already counted them. Carries a Restart · Exit
`BoardMenu` driven by player 1; Restart routes through `GameSession.RestartMatch` (mirrors
`RestartRace`: `VersusMatch.Restart()` then every rig respawns), which clears `Completed` and lets
`_Process` do the hide and the clock release. R and pad Y reach the same call directly, owned only
while the board is up via `FlightController.Match is { Completed: true }` — polled in
`PollResultsShortcuts` off the rendered frame, since the halt means no sim step runs to read it.

## src/Flight/HaltReason.cs
Why the sim clock is stopped, as a flags set: `Paused` (a player asked, and carries an owner) and
`Ended` (a results board is up). The clock advances only when the set is empty, which is what lets
two systems halt it at once without either resuming it out from under the other. `PauseState`
arbitrates; nothing else writes a reason.

## src/Flight/PauseState.cs
Who is holding the sim clock and why (`BL-373`) — `VersusMatch`'s engine-free shape: no `GD.*`, no
`Godot.` type, no `Node`. One instance per session, built by `GameSession.BuildFlightRigs` ahead of
the rig loop (the assembler hands it to the per-pane stunt board) and assigned to every rig's
`FlightController.PauseState`. `TryToggle(playerIndex)` owns the pause half: not paused → pauses
and claims `OwnerPlayerIndex`; paused → resumes only if `playerIndex` matches the owner, otherwise
a silent no-op (`Changed` does not fire on a rejected attempt, so `PauseBoard` never flickers on
another player's futile press). Refused outright while `Ended` is set — the results board's own
menu already offers the only two things left to do, so a pause menu would stack on nothing.
`Raise`/`Clear` own the results-board half and REJECT `Paused`, which carries ownership and must
go through `TryToggle`; `ForceResume` drops a pause whoever owns it, for a rerun or an exit chosen
from the menu. Off-engine coverage: `CSVM.Tests/PauseStateTests.cs`.

## src/Flight/PauseBoard.cs
The shared pause overlay (`BL-373`) — `VersusBoard`'s WHOLE-window construction (pausing
stops the game for everybody at once, not one pane), built once by `GameSession` on its own
CanvasLayer (`UI.HudLayers.Board`, same layer the race/dogfight/wrap-up boards share) and wired to
`PauseState.Changed` instead of a match/race completion event. Shows "PAUSED", the pausing
player's tag in their own `SplitScreen.PlayerColor`, and a `BoardMenu` of Resume · Restart · Exit
driven by that same player alone — `PauseState` lets only the owner resume, so binding the cursor
to the owner keeps one rule rather than two, and stops a second pad steering a menu whose Restart
and Exit decide the whole session. Exit's label follows how the session was launched. A fresh menu
each pause, so the cursor starts on Resume and a stray confirm cannot destroy a run.
`Populate()` runs only on a fresh pause (mirrors `VersusBoard`'s snapshot discipline); `Visible`
tracks `PauseState.Paused` on every `Changed` event, and `_Process` polls the host only to move
the cursor. A campaign session's objectives readout (`UI/ObjectivesHud`) rides the same pause on a
layer of its own rather than inside this board, since it belongs to the flown mission and this
board is shared by every mode.

## src/Flight/IaWrapupBoard.cs
Instant Action's wrap-up board — `VersusBoard`'s WHOLE-window
construction, since the mission ends for every human at once (decisions 10/14), not
`StuntScoreboard`'s per-pane shape. Four label/value rows (Time to Complete Mission, Enemies Shot
Down, Danger Zones Completed, Shot %) — the langui titles at ids 1134-1137, kept as literal
strings rather than read off `ui_strings.json` at runtime, since that table is a build-time
extraction artifact of the `.rof` archive and not one of the five archives `SessionArchives.OpenFor`
loads. Unlike `VersusBoard`/`StuntRaceBoard` it takes no live match object at all: `Present`'s
arguments are the caller's own snapshot, handed in once from `InstantActionRuntime.MissionEnded` —
`InstantActionDirector` owns every source (the mission clock, the kill tally, `ProjectilePool`'s
shot counters, the summed `StuntMission.CompletedCount`) and this class only draws what it is given.
On a `stunt_flying` mission `Present` also takes a `StuntSummary`, and the board grows the run's
zone splits, total and best-time row in `StuntScoreboard`'s layout; the scoreboard is then not
built at all, which is how `BL-358`'s two stacked boards became one. Safe because the scoreboard
only ever existed single-pane: several pilots take the race branch, which builds none.
Raises `HaltReason.Ended` on `Present` and carries a Restart · Exit `BoardMenu` driven by player 1.
Nothing else retires this board — a mission that has ended stays ended — so unlike the race and
dogfight boards the hide and the clock release happen on the menu's own Restart. That Restart is a
restart and not a rerun: it reaches the Launcher's `RestartSession`, which rebuilds the world,
because the mission's waves, ace and zeppelin cannot be put back in place.

## src/Flight/Weather.cs
`WeatherState`: per-mission atmosphere from the flown mission's own weather.json — per-zone
`ZoneWeather` records holding fog (`FOG_COLOR`/`FOG_RANGES`/`FOG_ALTITUDE`), `SUNLIGHT_*` →
`WorldLight` (`SunIncidence` 0.46 / `MinWorldLight` 0.15, TUNE) and `SUNLIGHT_ORIENTATION` →
`SunOrientation`, plus the `CLOUD_COVER` whiteout band (`WhiteoutAmount` trapezoid), `WIND`, and
precipitation → `PrecipData`. Schema + colours + zone names: weather.md.
**The original's weather/sky/fog/light runtime is written up in [org/weather.md](org/weather.md)** —
the camera weather state machine, the `zone_id` visibility gate, the zone apply's edge trigger, the
band flicker's two curves, and the sun/dome/deck rules. Read it before changing a weather mechanism.

`PreferPopulatedHorizonZone` is the second, geometry-driven correction (`BL-277`): a request whose
`horizon/zone*` subtree carries NO meshes yields to the one zone that does, which is what makes
C1B/C2/C3 render a sky at all. The `ResolveZone(requested, horizonZones)` overload runs both in
order and takes the geometry pick ONLY if this mission's weather.json also defines fog for it, so
sky and fog are always the same zone. Census + per-chapter table: weather.md; the census itself is
`WorldBuilder.HorizonZonesOf`; coverage `CSVM.Tests/SkyZoneTests.cs`.

`CameraWeatherState(cameraPosition, fogZoneArmed, volumes)` (`FUN_0042ee40`) is the binary's
per-frame camera zone 1/2/3, published by `WeatherRig.Tick` onto each
`PlayerRig.CameraWeatherState`: 1 default; 2 when `HasCloudBand` and the camera's altitude is
at/above `CloudCoreBottom` — a THIRD spelling alongside `CloudBandCentre` (the deck-regime flip) and
`CloudBottom` (the visual floor), all within ~80 m of each other in C1 but never unified (Decision
1); 3 when `fogZoneArmed` (`FogVolumeSpec.FogZoneArmed`) and the camera is inside any `FogVolumeBox`
— the exact half-space `Contains` test, not the AABB — and state 3 wins over state 2 on overlap
(never actually exercised in shipped data: only C5 arms `fogZoneArmed`, and its band sits far above
every C5 volume).
`ZoneForState(state)` is B11's consumer-side half: state *n* asks for `zone<n>` and goes through
`ResolveZone`'s file fallback, so a mission with no `ZONE<n>` keeps its first zone rather than
falling to `NoFog` (no fog, fullbright). Deliberately NOT routed through the horizon-aware
`ResolveZone` overload — that one owns the DOME's single per-flight zone; the fog zone changes
underneath it every time the camera crosses the cloud core.

## src/Flight/FlightAudio.cs
Own-plane non-positional loops (engine, overspeed whine, rattle) + one-shots: `StartEngine`/`EngineStartRamp` prop-start fade (re-fired via the loop-restart hook
in `Update`; `EngineStartRamp` 2.0 s is sourced from startprops' authored prop cross-fade duration,
not a bare literal), `OnCrash` → `snd_exp_plane1..4` (the `plane_destroy_sg` group every crash def
that names it wants), `OnGroundExplosion`/`OnWaterExplosion` layering the boom the *chosen crash
def* authors (`snd_exp_ground_a` off `player_crash_dirt` itself, `snd_exp_water_a` from the
`plane_big_splash` inside the sea dive — the crash runtime renders effects, never sound; the
fallback `player_crash_default` Sounds only `plane_destroy_sg`, so it layers neither),
`OnEngineStop` → `snd_propstop`, called right after the explosion one-shots on every crash/destruction
(`FlightController.Crash`) so the loops end on the authored wind-down cue instead of a cut,
`OnGraze(water)` → the survivable scrape's authored `snd_exp_water_b`/`snd_exp_ground_b`
(touchdown.zrd; rate-limited by `FlightController`, not here, and its argument now comes from the
CHOSEN `touchdown_*` def, since the sound is authored inside that def). `OnWarningShot` draws one
`bullet_warning_sg` variant per near miss (player.json `warning_shot_sound` is a SOUND_GROUPS name, so
`Setup` takes the group table too; rate-limited by `FlightController`'s `WarningShotCue`, same split).
The engine is ONE voice on one slot; its pitch, gain and definition all come from
`EngineAudioCurves`, shared with `AiEngineAudio` (see that entry). `UpdateEngineSlot` swaps the
slot's stream for `damaged_engine_sound` while `EngineAudioCurves.EngineDamaged` holds (worst zone
below a quarter health, or the engine choked) and for `cockpit_engine_sound`
while the pilot's SELECTED view (`FlightController.FirstPersonView`, A1's mode-6/7 equivalents) is
Cockpit or Nose, both resolved at `Setup`; damaged takes precedence when both apply
(`EngineAudioCurves.EngineDefFor` carries the rule — no def authors a damaged cockpit variant, and
the plan's evidence does not decode which of the two wins, so damage feedback keeps priority as a
port decision). The view swap keys to the SELECTED mode, not the per-frame camera pose, so a held
numpad key or look-behind does not retrigger it (D31, closes `BL-161`). `MixGain` is the only
own-ship scale left and stays here — splitscreen, not a fidelity knob.

## src/Flight/EngineAudioCurves.cs
The engine-audio slot maths both audio paths read: `EngineDamaged`, `EngineDefFor` and
`DamagedPitchMul` (whether the airframe counts as damaged, which definition the slot then holds, and
the swap's one-off pitch draw), `Engine` and `Whine` (each slot's pitch and gain off the
`PlaneStats` curves), `DriveFrom`, and `CullDistanceSq`. It exists because the original runs one
per-frame routine for the player and every AI vehicle; the decode is in
[formats/vehicle.md](formats/vehicle.md), "The engine audio's slots" and "What makes an airframe
damaged". The damaged swap is a health-fraction gate, not a took-a-hit one.
The engine slot's parameter is **not the throttle lever alone**: `DriveFrom` reads a turn rate off
the two body axes perpendicular to the nose and a climb attitude off the orientation, and `Engine`
adds them to each curve's NORMALISED parameter under a [0, 1.5] clamp before the curve maps it out.
That ordering is why `SoundCurve` exposes `Frac`/`Remap` separately from `Eval`, and the headroom
above 1.0 is what lets a hard pull overshoot the curve's own top. The turn-rate-into-volume term is
inert against the shipped flat volume curve and is kept because the data, not the mechanism, is what
makes it so. `BL-109`'s `CAP-10` measurements are the acceptance test, pinned by `engine-note`.

## src/Flight/AiEngineAudio.cs
The positional twin of `FlightAudio` that an AI-flown aircraft carries instead of it: the same two
engine slots on `AudioStreamPlayer3D`s, plus the cull that stops them past `CullDistanceSq` from
the nearest listener and starts them again inside it. `Attach` is the whole spawner-side surface.
Deliberately carries no own-ship concept — no `MixGain`, no start ramp, no prop-start cue, no crash
one-shots: an AI kill is audible from its crash animation's own authored `Sound` events. Audio
cannot be screenshot-verified, so the `sound` log carries the whole observable: one line per
aircraft at build naming what each slot resolved to (or that there was no archive at all), then one
per cull transition and one per damaged-engine swap. The pair is what separates "silent past the
cull" from "silent because the definition never resolved".

## src/Effects/Puffer.cs
The original engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks
(schema: [formats/effects.md](formats/effects.md)). `PufferState.Load` finds the fully-defined
state in an effects reader; `Puffer.Create` builds the atlas and hands it to an `IEmitterRenderer`
(`EmitterRenderer.cs`) — the class itself owns only the CPU integration, so `CreateWith` reaches
all three modes with no atlas, no `TextureArchive` and no GPU. The continuous surface is one pair,
`Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)` / `Stop()`, plus the one-shot `Burst` and the
hard-kill `Clear` — the authored state picks burst, trail or sustained mode, callers never do.
`PufferState.FromAnimEvent` parses the compiled anim payloads; `Parse` reads the reader form.
**Two different numbers answer "no `TIME_INTERVAL` authored"**, and both parsers keep them apart:
`TimeIntervalDefault` is the engine constructor's 1 s, and `StillHostSputterInterval` is CSVM's own
0.1 s cadence for a `DISTANCE_INTERVAL` state whose host holds still (a mode the engine does not
have at all). A compiled event says "unauthored" with a **zero** interval, which the engine's own
setter refuses; a reader block says it by omitting the key, and every reader block that omits it is
a distance block. Merging the two constants moves either every static building's sputter or every
unauthored state by a factor of ten, so the `puffer-modes` suite asserts both cadences.
`Create`'s atlas comes from a per-archive cache keyed by the frame list and the sequenced flag
(`BuildAtlas` over `BakeAtlas`), so a state many emitters share is baked and luminance-measured
once; the archive is the key, so a chapter change never serves another chapter's frames.
Three config knobs (`puffer.burstSizeScale`/`trailSizeScale`/`sustainSizeScale`) scale `BaseSize`
per spawn path, registered in `Config.WarmTuningRegistry` for `--dump-config`.
The wind it reads is `Effects/WorldWind.cs` — see its own entry below.

**The camera-distance fade** (`DistanceAlpha`) is view-space depth off `EffectAmbience`'s camera
poses, run against every pane since `NearestViewerAlpha` keeps the most favourable answer across
them. The mechanism, field order and the three `puffer.*` switches are in
[formats/effects.md](formats/effects.md); read that before touching this.

The emission accumulator (a teleport guard on the distance path, no per-frame batch cap on
either), the `PRIORITY` size nudge, and the blend-mode derivation are all decoded and traced in
[org/puffer.md](org/puffer.md) — read it before adding a mechanism here. **The two continuous
modes share one batch loop** (`EmitBatches`, fed metres by `TrailAdvance` and seconds by
`SustainAt`, both spawning through `SpawnSustained`): a distance trail therefore lays `NUMBER`
puffs per interval, rotates `LOCAL_VELOCITY` into the host's frame and gives each puff its
sub-frame birth age, exactly as the time mode does. ⚠ Do not give the trail its own spawn again:
the one it had dropped all three, and the smoke screen (`smokerpuff`: `NUMBER 4`, `LOCAL_VELOCITY`
10 m/s astern) laid a quarter of its cloud drifting on the world axes. A continuous emitter's pool
starts at `TrailPool` / the sustained steady-state estimate and doubles on demand up to
`ContinuousPoolMax` (`GrowPool` → `IEmitterRenderer.Grow`); the ceiling is invented like every
pool size here, the growth is not, since the engine bounds particles only by what can be born
alive. The authored-key side
stays in [formats/effects.md](formats/effects.md); the blend trace continues in
[org/textures.md](org/textures.md). Proven by `PufferDistanceFadeTests`, `PufferWindTests`,
`PufferPriorityTests`, `PufferStartAgeTests` and the `puffer-fire-column` suite.

## src/Effects/WorldWind.cs
Two small types, one job: get the mission's authored wind to every puffer that reads it.

`WorldWind` is the gust model, decoded from `FUN_0054ee10` and authored in `weather.zrd`'s `WIND`
block (schema: docs/formats/weather.md; decode: docs/org/weather.md): a static base vector plus a
horizontal random-walk gust, stepped once per frame off its own `Rng.Wind` stream.

`EffectAmbience` is the seam: the per-frame world state (wind, every pane's camera pose) a
`Puffer` READS but does not own, handed in at construction rather than reached for. `GameSession`
owns the one instance and `Session/WeatherRig.Tick` writes it once per frame; `EffectAmbience.Still`
is the null object every unwired puffer reads (unit suites, the plane viewer, no-weather missions).

## src/Effects/EmitterRenderer.cs
`Puffer`'s lower seam: `IEmitterRenderer` takes live particles (`Attach` sizes the pool, `Grow`
re-sizes it when a continuous emitter outgrows it, `Write` per particle, `Show` publishes the
frame) and `MultiMeshEmitterRenderer` draws them as ONE MultiMesh of camera-billboarded quads,
owning the shader — quad-rim fade, flipbook column from per-instance custom data, and the
soft-particle depth fade. The `COLORS` ramp arrives as the per-instance colour and is linearised
in the shader (`csky_srgb_to_linear`, the same include every fullbright pass uses): the ramp is
authored in DX7 framebuffer bytes, so multiplied in raw it drew every ramped puffer two shades
too pale (the smoke screen's `53,74,37` came out `109,126,92` against the reference's `50,68,35`).
The atlas and the blend verdict both arrive already resolved from `Puffer.Create`, which is what
keeps the three modes reachable with no `TextureArchive` below this seam. Reached in a suite by
`RecordingEmitterRenderer`. `Attach` takes its `Shader` from a static per-variant cache (blend ×
soft, four at most): a `Shader` per emitter compiled in about 6 ms, which was most of a live
`effect_pool_miss` and of a crash rig's pre-warm; the `ShaderMaterial` stays per emitter, since it
carries the atlas.

## src/Effects/FogVolumeClutter.cs
The ambient cloud field, entirely authored: `fogvol.zrd`'s weighted clutter table scattered
through every `fvol*` volume the gamez carries, one alpha-blended MultiMesh per sprite kind (two
per chapter), plus a map-edge continuation (`ExtendPastMapEdge`/`EmitExtensionRegion`) that tiles
the same field past the map rim for a chapter's map-spanning slab, engine-side and not authored,
matching `MapEdgeExtender`'s own terrain continuation. Templates resolve through
`ClutterBuilder.FindTemplateRoot` — the trees' own lookup. The two TUNE constants
(`TopAnchorHeightFactor`, `CardVertexColorTune`) and their remarks live at their own fields.
Per-view visibility gating lives in `GameSession`/`WorldBuilder`/`WeatherRig`, not here.
Schema, per-chapter values, the decoded/inferred split and the map-edge-continuation measurements:
docs/formats/fogvol.md. The shared template lookup: docs/org/clutter.md. Proven at the render —
see fogvol.md's evidence sections and `FogVolumeTests`/`FogVolumeWhiteoutTests`.

## src/Effects/Precipitation.cs
Rain/snow from weather.json's precip block (`WeatherState.PrecipData`): ONE MultiMesh whose
shader derives each quad's position from a per-instance seed + `csky_time` + `CAMERA_POSITION_WORLD`,
wrapped into a camera-centred box — zero per-frame CPU. SNOW = fluttering flakes; RAIN =
streaks along the data's WORLD fall velocity (not plane-relative — TUNE pending A/B); sprites
are procedural `MakeFlakeTexture`/`MakeStreakTexture` (the original drew untextured primitives).
Schema and the data→look TUNE mapping: docs/formats/weather.md.

## src/Flight/SpectatorCamera.cs
The `--freecam`/`--anim-lab` observation camera: WASD move, RMB-held mouse look (captured only
while held), wheel speed, pads via `Pads.For(_padDevices)`; lab additions `Frame(Aabb)`, the
`FollowNode` orbit-lock (released by any translation input; `ExitFollow` keeps orientation) and
a public `Camera` accessor — all inert in plain `--freecam`. Rates TUNE.
While locked, the orbit answers the mouse **and the pad**: right stick swings it, the triggers
dolly it (`OrbitPad`, RT out / LT in, the sense `FlightController.OrbitInput` already uses). The
target key `F` / pad `X` (`CycleLock`) re-locks onto what `LockCandidates` offers, nearest first
then outward (`OrbitLock.Next`) — the only route back into a lock, since a release is otherwise
one-way. `GameSession.LockCandidateAircraft` supplies the roster (every `InPlay` aircraft, AI and
human) to all four spectator cameras; a null provider leaves the key inert.
⚠ The target key is handled in `_UnhandledInput`, NOT polled like the axes. This camera reads raw
key state, which bypasses `SetInputAsHandled`, so a polled edge would fire behind a host that has
already consumed the key — `BL-279`'s mechanism. The same rule is why the anim lab's def picker
moved from `F` to `F18` (`BL-428`) rather than the two sharing a key. Pad button events are gated
through `ReadsPad`, the event-side twin of `Pads.For`, so `--no-pads`, an unfocused window and a
per-seat binding all still hold.
It is also the pane an Instant Action pilot out of lives watches from
(`InstantActionDirector`'s spectate hand-off): the lab's own `FollowNode` orbit is what "follow a
live aircraft" needed, so nothing was added for it. Constructor params `padDevices`/`useKeyboard` (
`BL-375`) default to null/true — every connected pad plus the keyboard, unchanged for `--freecam`,
the anim lab and the weapon lab — but the director passes its rig's own
`FlightController.PadDevices`/`UseKeyboard`, the same filter the flying panes use, so two
splitscreen pilots watching at once move independently instead of lockstep. Mouse look has no such
split (one physical mouse) and stays shared. ⚠ Default start is the mission spawn — RANDOM per
launch; pass `--pos`/`--direction` for comparisons. ⚠ `KeyboardCaptured` zeroes keyboard axes while
a text field owns focus — raw key polls bypass GUI focus. ⚠ Vertical is Q/E plus the **Z/U**
  alternate — not C/Space: C toggles the collider overlay and Space fires guns, and because this
  camera POLLS raw key state, sharing either key moved the camera as a side effect of the other
  action (`BL-279` moved Space off; Z stays clear of C for the same reason).

## src/Flight/OrbitLock.cs
`SpectatorCamera`'s re-lock rule, taken as positions and an index rather than nodes so it runs
off-engine (`OrbitLockTests`) — the same split `Pads.AssignPads`'s pure overload uses. `Next`
answers the nearest target to the eye from no lock, the next one outward from a held one, and
wraps past the farthest back to the nearest so no press is ever a dead one. It RANKS rather than
sorts: the answer is an index into the caller's list, so a sorted copy would only have to be
undone. Ties break on the index, so two aircraft at equal range still get distinct turns and
neither is unreachable. A `current` that is out of range reads as no lock, which is what a stale
index (its plane gone since the last press) resolves to.

## src/Flight/FlightModel.cs
The decoded, data-driven aircraft plant. Rotation sums stick torque, bank coupling, `return_rate`
weathervane and ground blow before exponential `ang_momentum_damp` decay. `BodyRates` stores the
original's quaternion half-angle rate; `PhysicalBodyRates` and the attitude update double it. Authored speed curves
scale the stick command only, roll on the base ramp and pitch on that ramp times the authored
high-speed fade. `OpposingCommandLimitAt` softens the pitch and yaw commands that swing the nose
further off the flight path, on the decoded G ramp and the decoded AOA window; `AoaLimiterFactor`
is the window's A/B seam (1, the default, is the decode); the pitch rate it produces is the answer
and the filmed 33 °/s is discarded beside it.
Translation composes the original's own chain: the lag vector as a clamped lift demand plus
decoded Mach drag, thrust and gravity; the velocity direction rotates only through that lift and
the ground-blow steer (`NoseChaseFactor` 0 pins the retired kinematic chase out, config-selectable
for A/B). The
footage altitude clamp, the STALL lamp's fraction and the dive-speed cap are ours, and are the
whole of what is not decoded in the file; every constant's class is in
[`org/flightModel.md`](org/flightModel.md)'s inventory, censused by `FlightConstantInventoryTests`.
`UsesAiForcePath` holds near-field differences, and `FarFieldPlant` is the original's level-of-detail
branch, re-decided every step: an AI aircraft more than 1 km horizontally from the NEAREST human
pilot holds `throttle · fd_speed + 5` along its nose at a 1/s lag and skips lift, drag, thrust,
gravity, the authority curves, the command limiter and the bank coupling, keeping its stick torques
and the ground blow. The range arrives as `FlightInput.NearestHumanDistSqM`, which is 0 for a plant
nobody tells; player-only guards widen to all humans. `Collide` is the decoded contact response
whole: the placement (0.03 m off the surface for a human, the sweep's stop exactly for an AI) and
the human-only normal impulse on velocity and body rates, with NO tangential, friction or
vertical-speed term, so a scrape bleeds speed only through repeated impulses; contact lifecycle
and the remaining invented laws (`C22`) stay in `AircraftContactResolver`/`FlightController`.
The choker's extend-only timer zeroes thrust alone. There is no engine torque: every write to the
original's angular accumulator is a product of state-derived vectors and none reads the throttle,
so the rotational plant is mirror-symmetric and `EngineTorqueAbsenceTests` pins it that way; do not
re-chase the GDD's one-sided turn assist. `FlightInput.Boost` is the nitro flag: it REPLACES the
lever with `BoostLever` 1.8 at the thrust read (the throttle and its slew are untouched, so an idle
boost is a full-throttle boost) and scales the drag coefficient by `BoostDragFactor` 0.8; the
lifecycle that sets it is `NitroSystem`. Full decode, and every mechanism's class (decoded, named
product exception, or unsupported): [`org/flightModel.md`](org/flightModel.md), "Parity ledger";
measurement rules: `verification.md`.

## src/Flight/NitroSystem.cs
The original's nitro boost lifecycle, engine-free (docs/org/flightModel.md, "Nitro"): a 30-unit
tank burned at 4/s while boosting and refilled at 1/s always, so a burn nets 3/s and runs 9.5 s
from full to the 5 % cutoff; the human arm (`HumanCommand`) engages on a held command only from a
99 % tank and re-asserts the flag until the cutoff, so nothing stops a burn and the refill to
re-arm takes 28.2 s; the AI arm (`AiSet`) has no engage line and fires once per nitro-flagged
maneuver. Both refuse an engine-out aircraft and a re-engage while the boost or decay animation
is alive; the boost animation lives at least 1 s after an engage. `Installed` is the injector
(the hangar's nitrous engine ids 3-5, or the roster block's `nitro` slot); `EngagedThisTick` and
`ReleasedThisTick` are the edges `FlightController.AdvanceNitro` turns into the shake kick, the
`nitro_boost`/`nitro_decay` defs and the `snd_nitro` loop. Every constant is censused by
`FlightConstantInventoryTests`; `NitroSystemTests` pins the lifecycle and the force couplings.

## src/Flight/PathFollower.cs
The engine's SECOND movement law, and the exclusive alternative to `FlightModel`: the dispatcher
picks between them before any flight law runs, so nothing here is a steering input. Pure state and
maths, driven by `Session/ScriptedPathVehicles.cs`. Holds two independent flags, `Following` (the
path owns this vehicle) and `Frozen` (it is placed and waiting), because folding them together
cannot express the state most authored path vehicles spend a mission in. Every constant is decoded,
not tuned. The final leg steers at, and ENDS at, the decoded point 300 m along the leg from the
waypoint behind it, raised with speed, so a run whose final leg is shorter than that flies past its
last waypoint climbing (`FinalLegOvershoot`; the motion's `Pitch` is the bearing to that target's
height, exposed for whoever poses the vehicle). What is NOT pinned by the decode: the ride height,
which for the aircraft movement classes reads a vehicle-type field this project has not identified.
Law, constants and the gap: [`org/flightModel.md`](org/flightModel.md), "The scripted-path
follower".

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
puffers via `Puffer.Emit`/`Stop` (DISTANCE_INTERVAL, the same mechanism
`DamageVisuals` uses for the nose smoke trail) — the authored def is a distance-triggered trail,
not a time-interval burst. `Reset(throttle)` (crash/respawn) hard-stops any plume and re-anchors the
climb tracker so the throttle jump those moments make is never itself read as a slam.

## src/Flight/FuelTank.cs
The flown aircraft's tank, engine-free so the arithmetic is testable without a scene. `Step(dt,
lever)` takes `dt · lever · 5` off `Remaining`, clamps at zero, and returns whether the throttle
lever may move this tick; it returns false on a dry tank, which is what freezes the lever where it
stands rather than closing it. The lever handed in is the value entering the tick, before that
tick's slew, matching the original's ordering. `Capacity` comes from `PlaneStats.FuelCapacity` (the
def's `fuel` key, 54926 on every player airframe) and `Fill` is the spawn-time top-up; a
non-positive capacity frees the lever instead of freezing it, so a fixture without the authored key
still flies. Only `FlightController.ReadKeyboard` burns, which is the original's player-only gate:
an AI-flown or scripted aircraft leaves its tank full, and a crashed airframe burns nothing while
still moving its lever. Nitro costs no fuel, because the burn reads the lever and not the boost
flag. Decode: `docs/org/flightModel.md`, "Part-throttle equilibrium"; the key:
`docs/formats/vehicle.md`.

## src/Flight/SpeedCue.cs
Its internal `Dispose` removes every puffer node when roster assembly is rolled back.

Loads `cuepuffer1..3` directly from the chapter's `speed_cue.zrd` and drives exactly one through
`Puffer.Emit` at the aircraft pose. Camera altitude selects the authored 30/15/8/15 m density
bands; within 50 m AGL none emits, while the script's empty branch above 1500 m preserves the
current selection. One instance is built per player and its puffer renderers are stamped onto that
rig's visual layer, so splitscreen panes never see another pilot's private speed cue. It shares the
session `EffectAmbience`, so the authored 500–600 m camera-distance fade and wind apply. `Reset`
hard-clears all three on crash/respawn so a teleported aircraft cannot bridge its old position.

## src/Flight/ControlSurfaceMix.cs
The decoded angle solver behind the surfaces, engine-free so the arithmetic is testable without a
scene: roll drives the two aileron slots at ∓0.5 rad, pitch the two elevator slots at −0.6 rad with
a ±0.18 rad differential roll term added, yaw both rudder slots at −0.61086524 rad times the
reverse-authority factor. Aileron and elevator targets are clamped (±0.5, ±0.6); the rudder is not.
Each slot then decays exponentially toward its target at 2/s, exp(−rate·dt), so the shape holds at
any frame rate. `Advance`'s `animate` flag is the original's player-only guard: false writes no
slot at all, which is how an AI aircraft's surfaces stay frozen. Addresses and the list population
that fixes which node takes which slot: docs/org/flightModel.md, "The original's control-surface
animation".

## src/Flight/ControlSurfaceAnimator.cs
The node side of that: collects every classified surface, poses it as an absolute pose (build-time
local basis, Basis = base · Rot(hingeAxis, slotAngle · scale)) rather than accumulating, and owns
the two frame flips the original does not need: a mount rotated by yaw π, and a nose-mounted
canard, which raises the nose the other way. --fly only; frozen while paused/crashed, reset on
respawn. `FlightController` passes its `IsHumanPiloted` as the guard, which is the plan's Decision
3: every human pilot animates for splitscreen, AI stays frozen as the original has it.

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
undid the leak's deactivation within 1.5 s. `HumanFlightAdapter`'s `DamageEffectSink` calls
`Suspend("wing_flare2")` right after playing `player_fuelleak` through the rig runtime — a suspended
lamp's index is skipped outright in `Advance`, deterministically, not by detecting a stray write after
the fact (which would miss the common case where the blinker's own commanded state already happened
to read "off" the instant the leak fires).

## src/Flight/PylonOrdnance.cs
The rockets mounted under a plane's wings: `Build` instances ONE FLYOUT MODEL body per loaded
pylon via `ProjectilePool.BuildFlyoutBody` (the SAME gamez prototype the round flies), parents it to
that pylon marker at identity local transform (nose -Z forward, tail at the mount = the launch pose),
and `Update` shows/hides each per its live `Hardpoint.Ammo`. FlightController drives `Update` after
UpdateRockets; the mounted body rides the plane and is freed with it. --fly only. `Unmount` takes the
set back off — detaching each body from its pylon IMMEDIATELY, not merely queueing it — so the weapon
lab's rebuild-on-swap cannot leave the old ordnance hanging beside the new.

## src/Flight/PlaneCollider.cs
Derives up to 8 plane-frame convex hulls from the built model's mesh triangles alone (no per-plane
data): region-clipped geometry (tail, wing, fuselage out to the wing band), greedy volume-guided
refinement cutting one OR two parallel planes per axis (the double cut separates bilateral pairs
like twin fins), then one `ConvexHull` per refined piece. The refinement and the part order are
judged on the pieces' boxes, so the hull is only the emitted shape and never moves a cut or a name.
Single-sourced: the terrain sweep casts these hulls AND `AircraftBody` mounts the same
`ConvexPolygonShape3D` resources as the plane's hittable body — never a second derivation.
`Layout` is the engine-free half (`Triangle`s in, named `Region`s out) the `airframe-hull-coverage`
suite and the unit tests measure; `Build` wraps it in shapes.

## src/Flight/ConvexHull.cs
A convex hull over a point cloud with no engine dependency: vertices, outward faces, edges, bounds
and volume, plus `Contains` and the point-to-surface `Distance` the fuse and blast passes ask.
Incremental construction on millimetre integer coordinates with exact 64-bit volume signs, so a
near-coplanar mesh cannot fold it (a float-epsilon hull did, on two airframes). A cloud thinner
than the thickness floor along an axis is padded to it first, the per-dimension floor the box
shapes applied; a cloud too degenerate to hull falls back to its padded bounding box.

## src/Flight/ShakeDefs.cs
Typed reader over the shared `shakes.zrd.json` — the six shake-oscillator sources
(`docs/formats/shakes.md`), modelled on `WeaponDefs`: load once (`GameSession` →
`AircraftAssemblyResources.Shakes`), named accessors per source, unhandled-key tripwire.
Each source is one law (frequency/damp/sawtooth) plus exactly one magnitude-term variant
(`magnitude_factor` [+ `he_factor`], `min_speed`+`magnitude_quotient`, or absolute `magnitude`);
absent sources read as null and `PlaneShake` no-ops them.

## src/Flight/PlaneShake.cs
The plane-wobble oscillators: gunfire buzz (`fire_bullet`), overspeed rattle (`high_speed`),
being-hit rocks (`bullet_impact`/`missile_impact`/`explosion`) and the nitro engage (`nitro`, one
kick of its absolute authored `magnitude`, human pilots only), summed each sim tick into
`Roll` — radians the controller writes to `ShakePivot`, the node the assembler hung the plane
model under. Engine-free on purpose (unit-tested); the pivot write is the controller's one line.
Amplitude = `magnitude_factor × caliber` in radians of roll, **measured** off original footage
(`analysis/gun-wobble-shake/`); a rocket hit stands in its armor damage (declared TUNE).
`high_speed`'s input is speed over the plane's `fd_speed`, so the authored `min_speed` 1.0 gate
means "beyond rated max" — the dive rattle whose magnitude is the EXCESS over the gate,
`(speedRatio − min_speed)/quotient` (zero at rated max, gentle overspeed ramp); cruise stays
silent like the footage's idle floor. `ContactHit` is block 5, the one oscillator no def authors:
it runs on the constructor's law (2 Hz, damp 4.5) and takes its magnitude from
`CollisionDamage.ContactShake`, which every resolved contact spends for a human pilot.

## src/Flight/FlightControllerBuild.cs
The internal construction handoff from `FlightRoster` to `FlightController`. It contains one
resolved controller's pre-tree state from either flight adapter, and `Bind` consumes it exactly once, before tree
attachment; a repeated or late bind is a construction error. The roster keeps the data-resolution
and lifecycle ordering, while the controller keeps its runtime interface. `HoldSegments` (the
scripted hold profile, when a caller wants one) rides this DTO the same way `Pilot` does, so
`FlightController` never exposes a public field for either arm; `Bind` copies it to the private
`_holdSegments` field before resolving `IFlightInputSource` (below) and storing it, alongside the
`IWorldQuery` seam.

## src/Flight/IFlightInputSource.cs
The seam a sim step reads this frame's pilot intent through: `Read(dt)` returns one `FlightInput`.
`PilotInputSource`/`KeyboardInputSource` are thin wrappers calling the matching `FlightController`
method (kept `internal` rather than `private` so the adapters can reach them); `ScriptedInputSource`
is the third arm and carries its own state instead — the segment list and its own elapsed-time
clock — so a suite can construct one directly with no `FlightController` in the process. Its
`Reset()` is what a respawn calls to restart the sequence from the first segment.
`FlightController.Bind` resolves which one flies a given aircraft off whichever of the private
`_holdSegments` field (assigned from `FlightControllerBuild.HoldSegments`, the caller's scripted
profile) or `Pilot` is set, and stores it, because that choice cannot change afterward (both arrive
only through the build DTO, before the first sim step). A private `InputSource` property lazily
resolves and caches the same way for a bare test rig that never binds — no suite mutates either
field once a rig is stepping, so this is never stale. Ground-blow probing and the AI ground-blow
write stay on `FlightController` after `Read` returns: they need the live world, which a source
does not have.

## src/Flight/FlightHud.cs
Everything one pane draws for its pilot, in one module the flight node holds privately: the heading
tape (`CompassTape`), the cockpit dials and their two weapon gauges (`GaugeCluster`), the gun pipper
(`ImpactReticle`), the selected-weapon text readout (`WeaponReadout`), the stunt objective marker
(`MarkerHud`), the targeting HUD (`TargetHud`), the `--hud-font-test` overlay (`HudFontTest`) and
the flight text block. Nothing outside this class writes one of them. The text block's auto-land
line is a placeholder pending the `langui` string extraction, marked as such where it is declared.
The per-frame entry is `Draw(in FlightHudState)`, a struct passed by `in` and never a class: this
runs once per rendered frame per aircraft, so it allocates nothing. The struct carries aircraft
STATE, not readout values: `Crashed`, `Halted`, `Held`, `StallWarned` and the rest arrive raw, and
the composing into text, dial positions and gates happens here, which is what makes the mapping
assertable with no Godot `Control` in the process. Three query properties exist so the caller can
skip work it would only throw away: `DrawsReticle` (a muzzle midpoint costs one world transform per
barrel), `DrawsTextBlock` (the damage ledger's `Summary` walks and joins every hurt zone) and
`NeedsStuntStatusLine` (the marker HUD normally carries that line instead). Each guards a real
per-frame cost on aircraft that draw nothing, AI rigs above all.
`Attach(canvas, versusHud, scoreboard)` builds the text block and parents every readout in the
shipped draw order; `VersusHud` and `StuntScoreboard` are still the flight node's, and are threaded
through because their z-order slots sit INSIDE this order rather than after it. `SetVisible` is
the hide-everything a cutscene's chrome-off and photo mode both take (`BL-429`, forwarded from
`FlightController.SetPilotHudVisible` because `GameSession` drives it per rig; the compass tape goes
with the rest); `SetInstrumentsVisible` is the narrower one `--debug-spectate`
wants, which keeps the marker HUD deliberately. `StepAgl` is the altimeter's LOW ALT feed, one ray
per physics frame through the same `IWorldQuery` seam every other aircraft query uses, and
`AglMeters` reads it back for the flight telemetry line. `Flash` raises the impact line and `Reset`
is what a respawn calls. The damage flash counts down on WALL time, so a halted session does not
burn it off while nothing is drawn.
The state-to-readout mapping itself is decoded into static, Control-free pieces `CSVM.Tests`
(`FlightHudMappingTests`) drives directly: `ComputeStallWarning`, `MphFromSpeedMps`,
`FeetFromWorldY` and `ComputeAgl` are pure functions of the struct (or a synthetic `IWorldQuery`);
`DrawnText` (internal, test-only) reads the built `Label` back, so an in-engine suite can assert
what a realtime frame actually put on screen rather than only what `ComposeTextLines` returned.
`ComputeGunGauge`/`ComputeMissileGauge` take bare `GunGroup`/`Hardpoint` lists (no bound `Loadout`
needed) and return a `GunGaugeReadout`/`MissileGaugeReadout`, filling the reused belt-fraction list
by pylon NUMBER, not list position; `AdvanceDamageFlash` is the wall-time countdown, gated off while
halted or crashed, independent of whether a text block exists to show it; and `ComposeTextLines`
returns the text block's lines as a list rather than one concatenated string. `Draw` and the three
`Update*` helpers stay the thin writers pushing those return values onto the seven Controls.

## src/Flight/FlightController.cs
The flying-aircraft node: input → FlightModel → transform (or, for an AI pilot publishing a
`RailPose`, the danger-zone ribbon's pose in place of the model step, the sweep still run), weapon fire as
`FireControl`'s engine adapter. `Group` is the roster cohort (the `aiv` block's `group`, the
original's `+0x388`), null outside a campaign roster spawn until a 967 capture swap stamps the
captured aircraft's on the human rig; `CampaignDirector`'s `DEDG` walk reads it. Weapon fire as
`FireControl`'s engine adapter (polls the held triggers, `Step`s the machine each sim tick,
performs the `FireOutcome`: muzzle-transform spawns, gun-loop start/stop, dry cues, breadcrumb
logs), crash and respawn. The pilot HUD is `FlightHud`'s (see that entry): this node holds the
module privately, feeds it one `FlightHudState` per rendered frame, and forwards photo mode's
`SetPilotHudVisible` because `GameSession` calls it; the seven readouts are no longer fields here.
`VersusHud` and `Scoreboard` stay board-adjacent fields on this node.
The camera is `CameraController`'s — this node only feeds it
the pose, the dt and the mixed orbit axes (`OrbitInput`), plus the two view-selection keys
(`PollViewModeKeys`: F8 cycles Cockpit → Nose → Chase, F6 selects Chase, both edge-detected on their own
slots like the targeting keys). `PinnedViewMode` seeds the selection from `--view=`; `ViewMode` and
`FirstPersonView` read it back live, and the session polls the latter for the anim data's
`PLAYER_1ST_PERSON` condition. `Cockpit` (a `CockpitVisibility`, null on any rig built without an
interior) is applied in the same block, keyed to whether the pose THIS frame was a first-person
one rather than to the selection, so a look-behind restores the aircraft while it is down.
Head-look input is read here too and nowhere else (`HeadLookRead`, `SnapLookInput`, `FreeLookRead`,
`MouseLookDelta`), for the same reason `OrbitInput` is: `CameraController` never learns about pads,
mice or key layouts. `SnapLookInput` reads `Kp1`–`Kp9` and `Kp5` recenters, the original's own
bindings; they are only reachable in a first-person mode, where `ActiveView` holds no fixed view.
Head-look is gathered and stepped only inside the first-person arm, on the SIM dt, so a look-behind
freezes the head where it was and releasing resumes it. The mouse is polled like
every other control (a screen-position delta while the right button is held, `UseKeyboard`-gated as
player 1's) rather than event-driven, which the fixed pan rate makes safe: only the DIRECTION of
the motion is read, so a stale delta on the frame first person is entered is worth one frame of
2 rad/s and nothing more. `Head.IdleAim` is wired here too, once, in `Setup` (C22): the private
`AutoheadTarget` reads `_model.Attitude`/`VelocityDir`/`Speed`/`Stats` and gates on `ViewMode ==
Cockpit` plus the `headLook.autohead` `Config` toggle, so `HeadLook` itself never learns about the
flight model or the mode.
On a crash it cuts to `CrashView` once,
writes nothing to the camera until respawn, and hides the HUD layer (the original's crash camera
shows no HUD — footage), restoring it on respawn. Every physics query — the PlaneCollider boxes'
sweep on every other sim step (`SweepCadence`, the original's parity, the skipped step's motion
carried into the next sweep) plus every ray (ground AGL, the ground-blow probe, the camera's height
check, both turret/AI lines of sight) — goes through the one `IWorldQuery` bound in `Bind`
(`GodotWorldQuery`, the sole adapter over `DirectSpaceState`); mask world+aircraft with its own
`Body` (`AircraftBody`, built in `_Ready` from the same boxes) excluded by RID, so another plane
is solid and a mid-air resolves through the same contact rules as terrain; `Crash`/`Respawn`
toggle the body's hittability. Contact detection is three fillers of one `ContactReport` (see that
entry): `SweepAirframe` from the hull sweep on a human rig, `SweepProbes` on an AI rig (the def's
`PlaneStats.CollisionProbes` carried along the motion as rays, the earliest strike winning, which
is the original's contact test and resolves to ONE origin point on every AI def, so an AI's wings
clip through a slot a hull cannot pass, the CM13 racers' `dzpath2` arch among them), and
`CenterRayContact` from the anti-tunnelling centre ray when neither reached the obstacle. Deciding what that contact does is
`AircraftContactResolver`'s (see that entry); this node builds the `ContactConditions`, hands over
a `ContactEffects` for the applying, and performs the `ContactOutcome` (the struck rig's share and
both grace windows, the HUD flash, the un-embed push, `Crash` on a fatal fate). Arming the struck
rig's own grace window goes through its `ArmCollisionGrace()`, a narrow public method, rather than
a direct write to the other instance's private field.
Which state the aircraft is in, and what moves it between states, is `AircraftLifecycle`'s (see that
entry): this node holds one privately, forwards `Crashed`/`Destroyed`/`WreckFalling`/`Inert`/`InPlay`
onto it, and performs what a transition reports rather than deciding it. `StageAt` is the one
exception to `Inert` meaning undrawn: an intro cutscene writes the pose the animation runtime put
its `player` marker in and draws the model there, then hands back the pose the aircraft held when
the staging began, or the one `ResumeAt` re-placed it at (`Session/CutsceneController.cs`).
`BindCrashRig` takes the
crash runtime, the def table, the anchor and the two respawn snapshots in one call, so the rig
cannot be half-bound and only `CrashRuntime`/`CrashAnchor` stay readable as properties. The DEATH family (`CRASH into`, `midair aspect`, every
`vehicle health exhausted`, `graze`, `embedded in terrain`, `AI ram`, `impact`) routes through
`Log.Info("flight", …)`, so a play session's file sink carries how each aircraft died; the
per-round weapon breadcrumbs around them are a different family and still `GD.Print`.
The sim half is `SimStep(dt)`, called by
`_PhysicsProcess` (realtime clock) or by `GameSession` (fixed/halted clock). `SimStep` also ticks
`Turrets` (the carried gunners) after the fire outcome, so the crash branch's early
return silences them; `WorldBlocksLine` is their world-only line-of-sight ray. Which stick flies a
given aircraft is one `IFlightInputSource` (see `src/Flight/IFlightInputSource.cs`), resolved once
in `Bind` and read through `InputSource.Read(dt)` in place of the old per-frame ternary. An AI
aircraft is this SAME node with `Pilot` (an `AiPilot`) driving that source, `IsHumanPiloted`
false, `Setup(null)` for the camera (every camera write skipped, `_cam` null) and no HUD canvas
built — flight, collision, weapons and damage are byte-for-byte the player's path. The one thing an
AI rig is told that a human rig is not is `HumanPositions`, the session's nearest-human snapshot,
which this node turns into a horizontal squared range every step (the wreck fall included, since the
original re-tests it whatever is flying the aircraft) and hands the plant as
`FlightInput.NearestHumanDistSqM` for its far-field branch.
`TakeProjectileHit` and the contact's ledger spend run the decoded damage flow (PlaneDamage: dead-zone redirect +
whole-pool overflow, 2026-08-14) — the struck zone is Apply's ANSWER, not the geometric guess,
and `IsDestroyed` is tested on every hit, zone-less included; the HUD DMG line leads with the
hull pair. The two disabling entries a hit path calls on a struck aircraft sit beside them:
`TryStunPilot(seconds)` (never a human) and `TryChokeEngine(seconds)`, the choker's, which does apply
to a human because the original's `TANGLER` branch has no player guard — the engine is a mechanical
system and nobody, AI included, is told about it. Both refuse an out-of-play airframe, and `Respawn`
clears both. `EngineDeadRemainingS` reads the timer back out of the model. Collaborators:
FlightModel, CameraController + CamParams, SpeedCue, Loadout + ProjectilePool (guns/rockets),
`CollideDamageSink` →
`AnimRuntime.CollideDamageAt` (fly-through facades), CrashRuntime, every HUD widget and animator.
Pause (`BL-373`): `AllowPause` gates whether THIS rig's P / Esc / gamepad-Start reads at all (true
for every human rig, false for AI rigs and the suites' bare test rigs); the edge-detected press
then goes through `PauseState` — every human rig in a session shares the SAME instance
(`GameSession` assigns it, the way `Match` is), so any player can pause but `PauseState.TryToggle`
only lets `OwnerPlayerIndex` resume it. Esc joins the toggle rather than leaving the flight: a
board menu's Exit item is what leaves, so a pad can reach it. `_Process` mirrors `PauseState.Halted`
into the session's `GameClock.Halted` every frame (a no-op write once every rig agrees), which is
the one field every other halt-aware consumer (animation, puffers, the projectile pool, `.`'s
single-step) already reads. Reading the COMBINED value is what lets a results board halt the world
without this mirror fighting it back to running on the next frame — `PauseState` decides who may
flip it, not how a halt behaves once flipped. A rig built with no `PauseState` (the suites) falls
back to the pre-E43 unconditional toggle, unreachable there since `AllowPause` is false on every
such rig. `PauseBoard` is the shared pause board.

`PollResultsShortcuts` reads R / pad Y while a results board is up, from `_Process` on wall time
rather than from the sim step. The board halts the clock, so the sim step no longer runs to read
them, and the hold harness's automatic rematch would have stopped with them. The sim step keeps
only the structural halves of those branches: the early returns, and the `_simPrev = _simCurr`
hold that leaves no stale pair to interpolate at the finish pose.
`Crash` reads the struck body's numeric surface id (`SceneBuilder.SurfaceIdMeta`) and hands it to
the lifecycle, which indexes `CrashDefs` (`SurfaceDefTable`) with it, the original's own cascade: `dirt`(13) plays
`player_crash_dirt` + `snd_exp_ground_a`, `water`(1) `player_crash_water` + `snd_exp_water_a`, and
everything else — id 0 plus the ids whose def this install does not ship — falls back to slot 0,
`player_crash_default`, which authors no surface boom of its own. `--crash` has no struck body, so
it takes the null-material arm to that same slot 0. On an AI plane `CrashDefs` is the `ai_crash_*`
vector instead (M4 G21 — `BuildFlightCrashRuntime` keys the family on `IsHumanPiloted`; same
cascade, same trio per chapter, and its defs hide the wreck rather than flinging pieces);
`LastCrashDef` records the selection and the `CRASH … def=` line prints it, which is how the
`ai-crash-defs` suite and a headless run read which family fired.
**Death is two-stage, and the two stages are two methods.** `Destroy` is stage one: whole-vehicle
health at zero starts `DestroyDef`, the airframe's own self-named def (`fury-fury`, `player-player`),
leaves the wreck VISIBLE, cuts the camera and raises `Downed`. `Crash` is stage two, the ground
contact — a live aircraft flown into terrain, or that wreck landing. Which system carries the wreck
down is asked of the data (`EffectCatalogue.FliesOwnHull`): the eleven airframe defs author an
`ObjectMotion` on `MAIN_ROOT_NODE` with their own bounce landing and own the fall outright, so no
`*_crash_*` follows; `player` authors none, so the lifecycle's `WreckFalling` keeps the hull in the flight model
(no input, no weapons) until `StepWreckFall`'s sweep reaches the world and `Crash` plays
`player_crash_*`. That second call is the one re-entry `Crash` allows while `_crashed`, and it
re-fires neither `Downed` nor the camera cut. The `ai-wreck-fall` suite drives both stages on one
spawned aircraft end to end and is where a regression in either shows up. `player_crash_default` is the cascade's **fallback
arm**, reached by a null material, an out-of-range id or an empty slot — it is NOT an air/no-impact
variant, a reading `BL-059` disproved. `this+0x19f ∈ {0,4}` inside `FUN_004b82d0` is undecoded and
stays unmodelled.
It also fires `Audio.OnEngineStop` (the wind-down cue, layered over the explosion) and plays
`stopprops` on `CrashRuntime` — the one call site every engine-death path shares, whether the
collision resolver called it for a full-speed impact, for whole-vehicle health exhausting on a
survivable-speed graze (a `ContactFate.Crash` outcome), or for a projectile kill
(`TakeProjectileHit`: the pool-resolved hit — part-mapped armor-first damage plus the graze's
feedback triple, no cooldown since rounds are discrete; its `damageScale` is the blast falloff
share for a splash hit, 1 for a direct round). The weapon/graze kill test is
`PlaneDamage.IsDestroyed` — whole-vehicle health at zero, the decoded rule (D14 retired the
old any-critical-part kill; the `critical` flag stays parsed, nothing consults it). An AI
pilot's trigger and lead are its `AiPilot.Gunner`: `SimStep` drives the gunner before
the fire step (`DriveAiGunner` — standing target kept while live, else re-acquired through
`SelectRankedTarget`, D12/D36's decoded ranking over aircraft, turrets and structures (`BL-363`)
with the same team gate: primary_target outranks — by NAME the first match, by the `player` token
the human NEAREST this attacker so a wave spreads over the panes instead of converging on P1
(BL-367), both aircraft-only — ranking otherwise, activation-cutoff candidates never picked, a
non-aircraft winner routed to `AiGunner.GroundTarget` rather than `Target` so `AiPilot` never sees
it, first acquisition logged with its rank inputs; with a
mode machine a standing target is kept in every mode but only pursue/lay off solve and shoot),
the fire inputs read `WantsFire` instead of the raw controls, and `AssistedGunDirection`'s
non-human arm fires the gunner's per-shot dead-eye scatter — an AI plane NEVER ticks or reads
the aim-assist slots (pinned by the `ai-gunnery` suite's A/B). `TakeProjectileHit` also rolls
the D11 damage reaction for AI planes (`AiModeMachine.NotifyDamage` — the steady-hand test),
and `NextPilotInput` lazily wires `WorldBlocksLine` as the machine's terrain probe. Every `Crash` raises `Downed` exactly
once — (victim `PlayerIndex`, killer: the killing round's shooter id; null for terrain, mid-air,
an unowned `NoShooter` round and every other cause) — a fact report the session scores in `--vs`;
flight holds no match state, and `Respawn` emits nothing. `AutoRespawnAfter` (session-armed —
Versus sets 3 s on every rig) auto-respawns a crash on the sim clock with R still skipping early;
null, the default, keeps every other mode manual-R (scripted hold runs keep their 1.5 s).
In a splitscreen stunt race, a finished pilot continues normal flight, collision, weapons and
crash/respawn while `StuntMission` holds their timer/objectives and `MarkerHud` holds their placing;
this prevents their finish pose from obstructing another pilot's gate.
`Respawn` plays `startprops` back and resets
`ThrottleSmoke`, which `Update` otherwise drives every frame off the live throttle.
A survivable graze also REBOUNDS along the contact normal (retiring `BL-172`): the decoded
`bounce_factor` impulse (`FlightModel.BounceNormalSpeed`) replaces the normal component the
tangential slide strips out, computed before the attitude kick so it reads the rates the contact was
entered with. Player-only, as the original is; an AI aircraft gets the position correction and nothing else. The `graze-bounce`
suite flies both into the same floor (e = 0.56 against 0.00) and a player rig along a vertical face,
where the same impulse fires horizontally and leaves the altimeter alone — there is no surface test
anywhere in it, and `CAP-14`'s flat-versus-vertical split must not be implemented as one.
A survivable graze plays touchdown.zrd's per-surface reaction (`GrazeReaction`) through the SAME
cascade: the struck body's surface id indexes `TouchdownDefs` (the session's one `SurfaceDefTable`,
built by `WorldEffectsFactory` against the world program because the original's touchdown vector is
a level-init global). `dirt`(13) raises dust, `water`(1) splashes, and everything else falls back to
slot 0, so **ordinary terrain and buildings alike spark off `touchdown_default`** — the same
correction B11 made to the crash, and the reverse of what this build did before. The def is staged
at the contact point via `GrazeEffectSink` (the world-effects runtime) with `FlightAudio.OnGraze`
under it, gated on the CHOSEN DEF rather than on a water flag, one per `GrazeReactionInterval`.
Where it stages is an open A/B — `graze.siteAtContact`, default the contact point (judged at the
controls); false stages on the aircraft, which is what the def's `MAIN_ROOT_NODE` offsets assume.
`AttachWarningShotCue` registers the aircraft on the pool as a near-miss target and `OnNearMiss`
rates the passes through `WarningShotCue` into `FlightAudio.OnWarningShot`; `PlayerIndex` is both
the pane seat and the identity every round this pilot fires carries, so it must be set before the
registration (the assembler sets it at construction, not in the stunt block). `Team` is this
aircraft's side for every hostility test — the aim-assist candidate scan, `SelectRankedTarget`,
carried `TurretController`s and `AiVoiceDispatcher` registration all read it, none re-derives one
from `PlayerIndex` — and defaults to `AimAssist.TeamOfPilot(PlayerIndex)` until a mission sets it
explicitly, so free flight and `--vs` are unchanged; plain flight's `--coop` is the one other
explicit setter, `HumanFlightAdapter` giving it `AimAssist.PlayerTeam` the same way Instant Action
does. `Inert` is this aircraft's other lifecycle state: BUILT but held
completely out of the session — not stepped (`SimStep` and `_Process` return at once), not drawn,
not on the aircraft collision layer, not hittable, not a targeting candidate, and not counted as
living. `InPlay` (`!Crashed && !Inert`) is the one "is it there" question every roster asks; a
consumer that still tests `Crashed` alone silently sees inert aircraft. Only two things are pushed
as state, by the private `ApplyPresence()` — `ShakePivot.Visible` and `Body.SetHittable` — and it
runs from `Respawn` AND `_Ready`, because `Setup` calls `Respawn` before `_Ready` has built the
body. **⚠ The pivot, never the model root**: that root is the airframe's own aircraft-archive node
and its `Visible` is the ACTIVE bit a hookup definition reads to decide which aeroplane it is posing
(`docs/formats/anim-definitions/cutscenes.md`), while a cutscene holds the aircraft `Inert`
throughout. The two crash paths still hide the model itself, which is a death state rather than
presence, and coming back into play undoes it. Everything else consults the flag: `TakeProjectileHit`/`DebugForceCrash` refuse,
`DriveAiGunner` drops a standing target that leaves play, and outside this class
`ProjectilePool.CollectAircraft` (which carries the aim assist, `SelectRankedTarget` and the
hostile tracker with it), the pool's fuse/blast passes, `TurretController.Alive`, `AiPilot.Next`'s quarry
test and `TargetHud`'s hostile draw all read `InPlay`. `InertChanged` is the event a session-level
roster mirrors it into (`AiVoiceRuntime` → `Speaker.Alive`). `Activate(pos, lookAt)` is the
inverse — re-home, clear the flag, `Respawn` — the original's teleport-then-reactivate in one call.
`Spectating` is the third lifecycle flag and the narrowest: a pilot out
of lives stays crashed for the rest of the mission. Checked at the TOP of the crash branch, ahead
of both respawn triggers, so neither R nor the armed `AutoRespawnAfter` timer can fly it again —
clearing the timer instead would leave R working. The session sets it from its own lives ledger and
hands the pane to a `SpectatorCamera` through `CameraOwned`; this node holds no mission state and
decides no rule, exactly as it holds none for `--vs`.
`Held` (the weapon lab) pins this ONE airframe while the session runs on: the sim step skips input,
`FlightModel.Step` and the whole collision sweep and re-applies the pinned pose through
`FlightModel.Reset(pos, attitude, 0, 0)` instead — everything from the pose commit down (weapon
selectors, guns, rockets, ordnance, gauges, telemetry) runs exactly as in free flight, which is what
makes the lab fire through the real path. `PlaceHeld(pos, lookAt)` moves the pin (C6/C7's re-park)
through the same `Reset` + `SnapCamera` pair `Respawn` uses; `ReleaseHeld(velocity, throttle)` is
the scripted-path follower's handoff, un-holding in place at that velocity and lever with the net
reseated and NO respawn or spawn grace (the original clears the path flag and nothing else, so the
sweep and ground blow run from the first flown step); `SelectGunGroup`/`SelectPylon` are the
programmatic twins of G/H for the lab panel. A held airframe also takes the ORBIT camera rather than
the chase — `halted || Held`, since both mean "the plane is standing still and the view should swing
around it" — and `CameraOwned` makes this node write nothing to the camera at all while the lab
hands the same `Camera3D` to a `SpectatorCamera`; clearing it re-seeds the orbit from wherever the
free camera left the eye.
`SelectedGun`/`MuzzleMidpoint` are the shared "which gun is selected, and where does its fire leave
from" answer, read by the pipper and by `GunReachesTarget`, the targeting marker's bracket gate
(see `TargetHud.cs`). `Stats` exposes the flight model's own `PlaneStats`, needed for the airframe's
display name; it is null on a rig `Setup` has not run on.
`StepTargeting` is the player-targeting frame, run for every human pane whose `Targeting` is set:
rebuild the pool and re-resolve, prune the attacker queue, then dispatch input. `TargetSubParts` is
the delegate that adds the zeppelin sub-parts, bound by `GameSession` rather than the assembler.
D-pad Up runs through `TapHoldButton`; `T`/`Y`/`U`/`I`/`O` are the curated keyboard set.
`ApplyInitialTarget` spends `--target=`'s one application (`cli.md`), and `TakeProjectileHit` feeds
the attacker queue `Next Enemy/Objective` walks backwards. Decode:
[`org/targeting.md`](org/targeting.md).

`ApplyFireOutcome` is where the gun aim assist meets the world (`BL-342`/B5): it rebuilds
`AimAssist`'s candidate set ONCE per tick (aircraft + fused ordnance off the pool, the world's
destructibles off `Destructibles` when a world runtime wired one), then `AssistedGunDirection`
runs each firing barrel's slot through `AimAssist.FireDirection` and hands the result to
`ProjectilePool.Spawn`. The rocket call deliberately gets no assist (the original reaches it from
the gun branch alone) but it does get a launch direction, from `OrdnanceLaunchDir`, and the two
shooters differ there: a human's round leaves along the AIRCRAFT's own basis axis, negated (as-is
for a `REAR` weapon), with the pylon marker giving the spawn position alone, while an AI's leaves
along the clamped mount aim `AiRocketeer` wrote. That asymmetry is the original's
([`org/ordnanceTypes.md`](org/ordnanceTypes.md), "Who aims ordnance, and who does not"); no shipped
airframe cants a pylon marker, so the human rule is a guard on this data rather than a visible
change. A pylon whose weapon carries `SmokeScreenTime` spawns nothing at all: it calls
`SmokeScreens.Lay` instead, so no round, no `FIRE` sound and no `FIRE` animation, while ammo, the
fire clock and the rate limit run as for any other pylon weapon. What the rocket call also passes is
the shooter's current target, a human's own `Targeting.Current` selection or an AI's `Gunner.Target`
quarry: it is the second half of `ProjectilePool.SteeringStepRuns`'s gate, so it decides whether a
`LOCK_ON` round is steered. The original's own shot routine hands a `LOCK_ON` weapon a synthetic
target when the player holds none; CSVM does not, so a round fired with nothing selected is not
turned, though it still sheds its inherited launch velocity (the divergence recorded on
`Projectile.cs`). A `BEEPER_SEEKER` round drops whatever it was handed here on its first frame and
steers only at the tag list's pick. The `ordnance-launch-axis` suite pins the launch halves.
Two one-shot breadcrumbs on the first gun round make the wiring visible in any flight log: the
candidate counts per list **with the nearest structure's range** (a registry whose anchors carried
no world transform would report its full count from the world origin — untargetable, and the count
alone would not show it), and the first snap with its kind, score and range. Measured in C1 flying
at the airfield: `vehicles=1 turrets=0 (M4) structures=210 ordnance=0, nearest structure 306 m`,
then `P1 snapped onto Structure (score 1.000, 288 m out)`.

## src/Flight/PlaneDamage.cs
The decoded vehicle damage ledger (org/vehicleDamage.md, corrected 2026-08-14): per-part pools
from destroyable_parts PLUS a real whole-vehicle (armor, health) pair — authored where the def
chain carries one (AI defs), else the sum over parts (player defs; player_bhawk 80/80, Fury
90/90). Apply is the decoded take-hit flow ported instruction-for-instruction: the named zone
spends armor-first (a dead/unknown zone REDIRECTS to a random surviving zone — the resolver
rule), the whole pair recomputes as parts' fraction × whole maxima after every part spend, then
the unabsorbed leftover re-enters zone-less and drains the whole pair directly. IsDestroyed =
whole health ≤ 0 (reachable with zones still healthy — the overflow kill). Summary leads with
the hull pair for the HUD DMG line; SummaryHealthFraction reads the whole pool (DI voice);
WorstFraction stays the worst PART on the combined pool (the injure_anims scale);
WorstHealthFraction is the decoded damage-state reading (worst zone on health alone, the hull
pair where an airframe resolves no zones) and is what the engine-audio swap gates on. `ScalePools`
and `SetWholePools` are the airframe swap's two writers, the only ones outside the take-hit flow:
one scales every zone by a fraction and recomputes the whole pair, the other writes that pair
directly. The stock
armor/HP doubling and the kill rule are decoded in
docs/org/vehicleDamage.md and docs/formats/vehicle.md.

## src/Flight/DamageVisuals.cs
Visible damage driven purely by data thresholds: as a part's HEALTH-ONLY fraction (armour is in
neither pool — docs/org/vehicleDamage.md "Damage staging") crosses an
injure_anims entry it shows the torn pdpN panel, hides the healthy skin, and plays the entry's
AUTHORED anim through `DamageEffectSink` — the player's own rig runtime (`BL-259`): `pdpanelN`
(gimmeflakes debris + the staged short_firetrail/loop_short_firetrail burn-down at the panel),
`player_fuelleak` (0.85 — gunhit flash + fuel vapor at a random pdp1–3, its stream authored-gated
on that panel being ACTIVE, so it renders only once torn), the `<part>_damage_effects` spark shims
(0.99, `player_pfighter` data alone — B4; their general home is the weapons.json `player` IMPACT
surface, live since M4 A2+D14 fielded AI shooters), and, for the data's 0.10 `player_smoketrail`, `player_damage_trail`
(short_firetrail at prop1 + the fire_lt light) — `RigAnimFor` owns that one mapping.
`DamageEffectStop` (Reset, first) stops the whole stage CLOSURE, derived from the program — a
stopped pdpanelN cannot reach the trail it CALLed, and prop1's trail has no authored exit;
`DamageEffectStopOne` is the single-stage form a retraction uses. Its lines route through `Log`
(`flight` for the panel and smoke-trail state, `anim` for the stage routing), not a bare `GD.Print`,
so a play session's own file sink records which stages fired. Staging is keyed per LADDER
ENTRY and cleared on the upward crossing alone, so a repair un-stages and the entry can fire again
(`StagedEntryCount`). Panel pairing (def-derived candidate sets, positional assignment, the three
crossed-naming outliers) is decoded on `PairHealthySkins` and runs only for an airframe whose own
data names a `pdpanel*` stage (`PairsPanels`); the null-sink stand-in fallback is on
`UpdateStatic`/`PlayStage`. An AI ladder's own two anims (`pfsmoketrail`,
`random_remote_damage`) play through the same sink: `RigAnimFor` is a membership test over
`EffectCatalogue.DamageStageAnims` + `PlaneDamageEffectAnims`, curated rather than
program-existence, so the cockpit gauge defs (`*_damage_green/yellow/red`, `*_got_hit`) can never
play on an airframe.

**The cockpit-interior twins pcdp4/pcdp6 (PLAN-cockpit-view, B12).** `PlaneBuilder.CockpitDamagePanels`
joins `DamagePanels` in the same `_panels` table (an optional constructor param, empty outside a
cockpit-interior build), so `ApplyPartStage`/`Retract` flip `pcdp4`/`pcdp6` alongside `pdp4`/`pdp6`
off the identical `pdpanel4`/`pdpanel6` entries — no separate cockpit rule, and `Reset()` clears
both together for free (neither carries the `_h` suffix that keeps a healthy skin visible). The
pair has no healthy twin of its own to pair (no `pcdp4_h`/`pcdp6_h` ships anywhere in `planes.zbd`),
so the crossed-numbering trap that pairs `pdpN`↔`pdpN_h` by mesh position does not extend to them —
there is nothing to pair. `CockpitVisibility.Apply` (B11) only ever toggles the four top-level
groups it binds, never a panel's own `Visible`, so a torn cockpit panel stays torn across a
Cockpit↔Nose↔external switch with no extra code.

## src/Flight/DamageLab.cs
The damage lab (F5 toggles): one armor slider (parts the data gives an armor pool) plus one health
slider per destroyable part — `PartFrac` (Health, Armor, Combined) is what an `IDamageLabTarget`
reads/writes. `DamageVisuals` itself is driven off the HEALTH fraction alone; `Combined` is
`DamageLab`'s own derived one-number reading, the scale the mirrored GaugeCluster damage dial is
on (see the class's own doc for both). `ReadSliders` floors a part's armor at 0 whenever its
health reads below 1, mirroring `PlaneDamage.Apply`'s armor-first real path (armor absorbs a round
in full before any of it reaches health, so no reachable state has health short of max with armor
still standing) — armor alone can still be driven to 0 with health untouched, just not the
reverse. Presets (--damage=part:frac) set both sliders to the same raw fraction through the same
ValueChanged path as a hand drag, so a fraction below 1 floors armor there too. One panel, two
hosts, chosen by the injected IDamageLabTarget (same file): ViewerDamageTarget drives
DamageVisuals on a parked plane (it holds no model), FlightDamageTarget writes P1's real
PlaneDamage — each pool through its own single-pool `PlaneDamage.Apply(part, healthDamage,
armorDamage)` call after `Reset`, using the fractions `ReadSliders` already floored. Neither
target reimplements visuals, only decides when to rebuild them. The `--damage=` flag's own
open-at-launch and splitscreen (F51, `BL-376`, P1-only) behaviour is the description of record in
[`cli.md`](cli.md).

## src/Flight/CompassTape.cs
The original's top-centre heading tape rebuilt from the game's own compassticks2/compasstxt
textures: a cylindrical drum seen edge-on — DrumX = center − R·sin(Δ), headings increase LEFT,
cos(Δ) fade (rendering model: docs/formats/hud.md). Metrics are probe-fitted Ref* constants ×
HudMetrics.Scale; Build returns null if a texture is missing; _Process re-anchors on resize.

## src/Flight/GaugeCluster.cs
The original's cockpit dials as a screen-space HUD: altimeter, speedometer, damage display, plus
the gun + missile weapon gauges and the nitro dial (`nitrogauge`, drawn only with the injector
installed; its two needles chase the decoded targets through `NitroNeedle`'s exponential at 3/s
and 1.5/s over a 216° sweep, negated at the draw site because the decoded angles are
counter-clockwise-positive; it sits at the bottom of the right column, below the speedometer, and
is the one dial whose bezel centre and radius are read from the tree rather than assumed), all
geometry extracted from the plane's own gauges subtree
(structure/scales/quirks: docs/formats/hud.md); polys draw by data priority, rest rotations
ignored; PartFraction binds flight or the lab; dial centres are bottom-anchored (FromBottom) so
panes keep them on screen. `DamageZoneColor(frac, yellowAt, orangeAt, redAt)` (`BL-085`/`BL-173`) is
the damage-dial band function — `frac` is `PartFraction`'s COMBINED armor+health value (both bound
sources, flight and the lab, feed that scale; nothing here computes it),
`yellowAt`/`orangeAt`/`redAt` are mined per-part from the data's own `*_damage_green/yellow/red`
injure_anims. Both `Border` and `Fill` always take the same colour index — `BL-173`'s refuted fix
shape was a synthetic per-pool ring split; there is only ever one colour per zone.
`GunIndicatorColor`/`HardpointIndicatorColor`/`SlotIndicatorColor`/
`DamageZoneColor`/`TargetArrowAngle`/`TweenArrow`/`IndicatorLowFrac`/`ArrowSweepDegPerSimS`/
`StallBlinkHalfPeriodS` are `public` (not `internal`) so `CSVM.Tests` (`GaugeColoursTests`,
`GaugeArrowTweenTests`, `StallWarningTests`) can call them from outside the assembly — moved from
the in-engine `gauge-colours`/`gauge-arrow-tween`/`stall-warning` suites. The two animated
cues are plain nested structs, `GaugeCluster.ArrowSweep` (`Angle`/`Advance`/`Reset`) and
`GaugeCluster.StallLamp` (`Lit`/`Advance`/`Set`) — B11 retired the `internal` testability-escape
hatches (`StallLampLit`, `AdvanceStallLamp`, the private
`_gunArrowAngle`/`_missileArrowAngle`/`_stallBlinkPhase`/`_stallDwellS`/`_stallLampOn`/
`_stallWarnPrev` fields) that existed only so the in-engine suite could reach a live `GaugeCluster`;
the structs need no `Control` to construct, so `CSVM.Tests` drives them directly. Scoped to
`GaugeCluster` only (Decision 5) — `FlightController`'s stall/arrow feed predicates are untouched.

## src/UI/LaunchMenu.cs
The in-game launchscreen CanvasLayer: Mode branches two ways. Free
Flight/Dogfight go Mode → Chapter → Plane, unchanged. Instant Action (the Mode row that used to
read Stunt Flying — decision 17) instead opens its own five-step wizard: Mode → Environment →
MissionType → Waves → Wingmen → Plane, shared with the other two modes as the final step.
Dogfighting an Ace skips Waves/Wingmen entirely, both forward (`HandleAccept`'s MissionType case)
and on the way back out of Plane (its own Back handler) — the decoded setup screen's own behaviour
(A1: mission type 0 hides every enemy control). Input polled every frame through one MenuInput per
player (no input-map/focus wiring); joining is gated to the Plane screen, and with >1 player that
screen becomes real SplitScreen.PaneRect panes — pick in the pane you fly in. Three Mode rows —
Free Flight/Instant Action/Dogfight — map 1:1 onto `SessionSpec.MenuMode`'s ordinals (the enum
member stays named `Stunt`, out of this item's file-contention scope — only the row's label
changed); `Launch` now carries a fourth value, the wizard's own built `InstantActionDef?` (null
outside Instant Action) alongside the chapter/choices/mode it always carried — H16's own build
path, replacing H15's interim "map the picked mission type onto whichever of Free/Stunt's
behaviour it most resembles" (that mapping is gone; `FireLaunch` always passes `_mode` straight
through now, and `SessionSpec.FromMenu`'s `iaDef` parameter is what actually decides
Scenario/Stunt, precisely, off the wizard's own pick).
`Environments` (7 rows, the decoded dropdown order, A5 — NOT `Chapters`' alphabetic one),
`MissionTypes` (4 rows, the UI dropdown order — NOT the internal id order), `Militias` (13 rows,
the `.BM` pattern-coverage aircraft lists, decision 7) and `Skills` (novice/veteran/ace) are the
wizard's own tables. `Planes` is a fifth: the eleven airframes in the langui 3700 order, which the
original stores an aircraft as an index INTO, so the order is decoded rather than cosmetic and the
positional defaults (`slot.PlaneIndex`, `_wingmanPlaneIndex`, both index 0) resolve to the Autogyro
that `gui_continue` itself selects. Each militia's own list is that same order filtered to
`FUN_00410420`'s 11-byte mask, never a per-militia reordering.
`CurrentMissionTypes` filters Stunt Flying out for whichever environment's
chapter bars it via `disallow_missions` (decoded: only C2B, "the clouds"), read through the same
`Chapters`-table `DangerZones` flag `ChapterCodesFor` already uses, so the two screens cannot
disagree. `MenuInput.MoveX` also drives WaveEdit's four fields (Enemies/Militia/Aircraft/Skill, one
focused at a time by `Move`) and Wingmen's count/aircraft; picking a new Militia resets
`AircraftIndex` to 0 (the decoded `AV[BA].QG = 0`). A wave slot's own build step (`WaveFor`, static
+ public) returns `InstantAction.EmptyWave` outright at 0 enemies, regardless of the
militia/aircraft/skill cursors — they are not "configured" until a pilot raises the count.
`FireLaunch` hands the built def to `InstantAction.BuildFromWizard`, using the environment's own
`ia.zrd.json` (loaded once, on Environment's own Accept, cached as `_iaBaseDef`) as the
ace/zeppelin/`disallow_missions` base — `SessionPaths.MissionZrdr(_dataRoot, code, "IA1")`, which
is why `Build` now also takes `dataRoot`. `DebugWaves(N)`/`DebugWingmen(N)` (--debug-waves=/
--debug-wingmen=) are `DebugJoin`'s own screenshot-aid pattern, extended to the wizard's own
screens.
`Screen.Presets` is the Table of Contents (`BL-352`), reached from step 1 by `MenuInput.Presets`
(P / X) and nowhere else — the original picks a preset with a mouse on a list sharing its page with
the dropdowns, so both the button and "opt in from step 1 rather than open on it" are stated
divergences, not oversights. Accept calls `ApplyPreset` and returns to `Screen.Environment`, which
is the original's own page order: the contents list is page 1, and View Story opens page 2, the
configuration screen under the preset's name. `PresetCrumb` is that heading, carried through every
Instant Action breadcrumb from a `_presetIndex` of −1 (custom) upward; nothing clears it when a
field is then changed by hand, matching `IDS_IA_STORYTITLE`'s one-time format. `ApplyPreset` is
deliberately partial — it writes the environment, mission type, waves, wingman count and aircraft,
and PLAYER 1's plane cursor only. It does not touch `_lives` (INVENTED, no preset value, and a
setting the preset has no authority over), other players' cursors (ours, not the original's), or
`_iaBaseDef` (still loaded by Environment's own Accept). So a preset is exactly a set of field
values: what flies is reachable by hand, and nothing about the built def says a preset was used.
The list is the file's only scrolling one — `PresetWindow` is `LAYOUT.CSV`'s decoded 14 visible rows
onto 19 items, `_presetTop` follows the cursor through `ScrollPresetsToCursor`, and `Rebuild` draws
that slice while `Row` keeps taking the absolute index. `DebugPreset(N)` (--debug-preset=) applies
one and opens on step 1, the aid for what units cannot see: a wrong aircraft or militia looks
entirely plausible on screen.
The campaign's screens are the third layout, drawn through `ComposedBoardView` rather than rebuilt
as controls, and the shell owns the two things a page cannot: the briefing's narration player and
its reveal clock (`TickCampaignAudio`). A running reveal repaints the board every frame, because a
reveal is an animation and no property watch describes one; `campaign-briefing-repaint` measures it.
`_UnhandledInput` also takes the screenshot key (`F12`) for both layouts and marks it handled, so a
press on any menu screen writes one file through `CaptureDirector.SaveScreenshot`, into the same
`Screenshots/` folder a flight shot lands in. The menu owns that binding rather than borrowing the
launcher's, because a board is what a menu defect has to be reported from (`BL-489`);
`menu-screenshot-key` pushes a real key event through the real viewport on both layouts and fails if
the menu leaves the key to whatever is above it.

## src/UI/InstantActionPresets.cs
The original's Table of Contents: the 19 preset scenarios decoded from 19 `0x230`-byte records at
`0x0061b090` and applied by `FUN_004102c0` (docs/formats/instant-action.md, "Table of Contents
presets"). The table is transcribed by NAME, not by the record's own dropdown indices, so it diffs
line-for-line against the decode and a roster reordering cannot silently invalidate it; `Resolve`
does the name-to-index step against `LaunchMenu`'s public rosters, which are the screen's single
source of order. Three things it does beyond copying fields. The mission-type cursor indexes the
FILTERED roster for the preset's environment, so a zeppelin run on "the clouds" is row 2, not row 3.
Unused wave slots take `FUN_004102c0`'s own sentinel substitution (Fortune Hunter / Devastator /
veteran at 0 enemies) rather than a zeroed default: it never reaches a flown mission, since
`FUN_004175f0` skips any wave at 0 enemies, but it is what a pilot inherits on raising an empty
wave's count. And a wingman aircraft is reported only where there are wingmen to fly it, `null`
otherwise, because the decode reports no value for the five presets that fly alone. An unresolvable
name throws, the same fail-loud policy `LaunchMenu.AircraftFor` applies to wizard-only data. Presets
fly stock airframes, so nothing here waits on the hangar. Units in
`CSVM.Tests/InstantActionPresetsTests.cs`.

The hangar (`HangarFlow`) has two doors, both through `OpenHangar`, which remembers the screen to
land back on: a trailing `Build Custom Plane` row past the three Mode rows, and the same row past
the eleven airframes on the Instant Action plane pick (PLAN-hangar Decision 6). `Screen.Hangar`
draws through the same centred body every other screen uses: heading, rows, detail and footer all
read off `_hangar.Page`. The rows and their detail line live in a content column of their own, so
that when the page's `Art` is non-null an art column (`HangarArtColumn`) stands to its LEFT, the
side the original's own paint screen puts its preview on: the page's decoded RGBA as a
letterboxed texture with a caption, and under it the focused row's own smaller picture when
`RowArt` returns one (the paint screen's decal tile). Each texture is rebuilt only when the page
hands over a different image, and `LayoutScale` counts the column only where it is taller than the
rows it stands beside. The detail label autowraps in that 560px content column, which is what
keeps the airframe-defaults ask (langui 206, a two-sentence question) on a 16:9 screen instead of
stretching the centred body past its edges. Every hangar screen also carries the persistent totals line under its heading
(`HangarFlow.TotalsLine`, PLAN-hangar Decision 10), error-coloured via `TotalsOverweight` and
counted by `LayoutScale` the same way. A page needs no change here, art included. The hangar's
screens sit behind a flow rather than behind the screen enum, so `--menu=` reaches them through
`OpenHangarAid`: `hangar` the plane list, `airframe` the airframe list, `defaults` its ask, `paint` the
preview on a Fury in Fortune Hunters colours with a nose decal chosen. A screenshot aid only.
Every human plane picker (the lone-pilot centred Plane screen and every splitscreen pane) draws
one roster, `_roster`: the eleven stock airframes then the store's saved customs
(`PlanePickerRoster.Build`), re-read by `ShowMenu` and by `CloseHangar` so a new save appears
without a menu restart. A completed build lands in `LastBuiltPlane` and `CloseHangar` puts player
1's cursor on the new plane by name (the original's index-11 after-build select). A custom pick
survives the menu layer as `PlayerChoice.CustomPlane`; its `PlaneNode` is the airframe's stock
node, and `Launcher.StartSessionFromMenu` reads the name back into a def through
`CustomPlaneStore` and carries it on `SessionSpec.MenuCustomPlanes`, one entry per pane, for
`HumanFlightAdapter.Assemble` to build the aircraft from (`CustomPlaneBuild`). A plane whose file
went away between the listing and the launch warns and flies the stock airframe rather than
refusing the session. Wingmen stay stock-only (`Planes`), and the scripted paths
(`--plane=`, `--det`) never see the roster: they name planes by node in `SessionSpec` directly.
⚠ The plane pick's hangar row is offered only to a lone pilot under Instant Action
(`HangarRowOnPlaneScreen`): it trails the customs, a splitscreen pane never draws it, and
`RebuildPanes` clamps every cursor back into the roster, so `PlaneIndex` can never point past the
roster anywhere a plane is actually read. The row is a door, not an aircraft, so it cannot be
locked or confirmed and no launch path sees it. Outside `OpenHangarAid`'s scripted-screenshot
values, the hangar is reached only through interactive menu input — no `SessionSpec` field,
nothing a `--det` run can touch.

The campaign (`CampaignFlow`) has one door, the `Campaign` row between the three modes and the
hangar's, and `Screen.Campaign` draws through the same centred body: heading, rows, detail and
footer all read off `_campaign.Page`, and the footer is the page's own, since a screen with an armed
text field has a different control set from the same screen with the cursor on its list. The art
column is shared with the hangar through `PageArt`/`PageRowArt`, so a campaign page that hands over
a picture needs no change here. Input is player 1's alone: `HandleCampaignInput` reads `Move`/
`MoveX` normally, and while `CampaignFlow.CapturesText` is true it reads `PadMove`/`PadMoveX`
instead and feeds `Typed`/`Erase` into the field, because W, A, S and D are letters there. The
flow's `Message` rides the same error line the hangar's gate uses. `--menu=campaign` opens the real
`user://Profiles` roster; every other `campaign-*` value is a screenshot aid over a scratch profile
directory, so those shots are the same on every machine and cannot write into a real campaign. The
one exception is `campaign-fly`, which launches a mission and therefore has to use the real store,
because the session's own director reads that one.

The flow's three exits are the shell's three jobs. `Cancelled` returns to the Mode screen.
`OpenHangar` opens `HangarFlow` over a `HangarCampaignContext` on the flow's own profile and leaves
the campaign flow standing; `CloseHangar` calls `Resume` on it, built or cancelled alike, so a
purchase or a sale shows on the cabin the moment the hangar closes. `FlyMission` saves the profile,
stops the score and hands the host a `CampaignLaunch`: the profile, the story position, and the
pilot's aircraft as its stock node, its hangar build where it has one, and the fit
`CampaignLoadout` derives from the profile's picks. The wingman is deliberately not in it, since
`CampaignDirector` resolves that binding from the same profile it opens anyway.

Two things the campaign pages cannot own live here, because a page holds no Godot node and has no
frame to advance on: the briefing's reveal clock (`page.Advance(delta)` once per frame while the
briefing is the screen showing) and its narration, an `AudioStreamPlayer` of this board's own on
the Master bus, restarted whenever the page's `NarrationStarts` moves and stopped the moment the
briefing stops showing. The board redraws on a reveal only when it uncovered a row or changed its
map, so a reveal waiting on a marker costs one comparison a frame. The score is the host's, entered
on every menu and cabin entry and stopped where a launch leaves the boards.

## src/UI/PlanePickerRoster.cs
The picker roster rule behind `LaunchMenu._roster`, engine-free so it tests without a menu
instance. `Build(stock, customs)` lists the given stock rows first in their given order, then one
`PickerPlane` per saved `CustomPlaneDef` in the store's own name-sorted order (the original's
11+customs list sizing, `docs/org/hangar.md` count callback 1024). A custom row carries its store
name in `CustomName` (the identity every consumer distinguishes stock from custom by) and its
airframe's STOCK node in `Node`; `AirframeNode(id)` is the id 0-10 to `player_*` node table in
the stat table's row order, the Hoplite resolving to `player_autogyro` (the shipped data's
two-names aircraft). `IndexOf(roster, name)` is the after-build auto-select's case-blind lookup;
-1 when absent. Deliberately NOT `Session.PlaneRoster` (which answers "which plane does player N
fly" off a `SessionSpec`): this type is the menu-side list, that one the session-side read.
Tests: `CSVM.Tests/PlanePickerRosterTests.cs`.

## src/Flight/CustomPlaneBuild.cs
The fidelity-bearing join: a saved `CustomPlaneDef` onto the three things a spawn consumes. Pure
and engine-free — every input is handed in, so nothing here looks up an airframe, a store or a
session.

`LoadoutFor(def, stockBase)` builds over the airframe's stock fit, which stays unmutated.
**Guns**: slot n's calibre row c becomes `Caliber = 30 + 10c` with `WeaponId` left null, so
`Loadout.Bind` resolves `wep_{caliber + ammoIndex}` and the Ammo Selection layer still composes on
top (`LoadoutChoice.ApplyTo` over the result). A twin mount is ONE gun over `firepoint(9-2n)` and
`firepoint(10-2n)`, a single takes the low one; the stock slot's own marker list narrows that pair
when the rig is short of it, which is what keeps a twin pick on the Kestrel's slot 1 off the
`firepoint8` it does not have (binding an absent marker is a loud throw). `Turret` and `Mount` come
from the stock slot; an empty pick (the dropdown's id 5) omits the slot entirely rather than
building it with no rounds. **Hardpoints**: the record counts pylons per wing and the stock fit says
what hangs on each. `Loadout.PylonFillOrder` alternates the wings entry by entry, so its two
interleaved halves are the wings (1-4 and 5-8; which is physically left is undecoded, `markers.md`
omits the pylon positions). A wing's count takes that wing's fill-order entries in order and **caps
at the pylons the stock fit authors** — the record names no ordnance of its own, so a fourth pylon
on a Bloodhawk wing has nothing to hang. Unchosen entries keep their place as
`LoadoutChoice.None`, since dropping one would slide every later pylon onto the other wing.

`PaintFor(def, patternName)` is the record's three resolved colours and its three decals under a
pattern name the caller resolves from the 0-13 index (`HangarPaintPage.PatternName`). The decals
are the same 0-49 index space `vehicle.json`'s `paint_decalN` uses, so they carry straight over; a
slot the build never chose keeps the `-1` "leave the shipped placeholder" sentinel, which a fresh
plane has on all three (the pattern defaults carry no decals).

`ArmouredParts` / `DamageFor` put the bought armour on the damage zones. Armour units reach the
pool at `ArmourUnitScale` = 5, the record's own premultiply, and **no other rescaling**: CSVM's
`destroyable_parts` armour pools are the shipped stock allocations (15/20/25/30/35/40,
`docs/formats/vehicle.md`), the same scale the original's hangar writes its raw x5 floats onto. Only
the ARMOUR pool is set; structure (`MaxHp`) stays the def's, exactly as the original leaves the
mission file's structure alone. Parts are **copied**, never overwritten: a `PlaneStats` is cached
and shared by every plane of that airframe. A zone the record does not name is carried across
untouched. The vehicle totals stay derived — no player def authors an `armor`/`health` pair, so
`PlaneDamage` sums the rebuilt zones, which is the original's own recompute-on-every-zone-write.

⚠ **Engine and weight reach nothing.** The engine pick decomposes into a power tier and a nitrous
flag; the tier indexes the same shipped `engines.json` table `PlaneStats.EnginePower` already reads
an airframe's stock row from, so wiring it means moving `FlightModel`'s thrust term rather than
adding anything here, and it is deliberately not part of this join. Total weight and the stat-table
power rating are hangar-only in the original and must never gain a flight dependency
(`docs/org/hangar.md`, "Into the mission").

## src/UI/HangarFlow.cs
The Build Custom Plane flow, engine-free the way `BoardMenu` is: the launchscreen owns every Godot
control and this file owns the order, the state and the rules. `HangarFlow.Order` is the original's
nine screens (plane selection, airframe, engine, armour, guns, hardpoints, paint, name, purchase;
`docs/org/hangar.md`), walked over one scratch `CustomPlaneDef`. Nothing is written until
`Commit()`, which is what makes cancelling from any screen residue-free by construction rather than
by an undo path: `Back()` off the first screen sets `Exit = Cancelled` and the scratch is simply
dropped. `Commit()` is the whole gate in one place: a name (langui 203), then
`HangarEconomy.Price`'s verdict in the original's own words (1182 + 1227 OVERWEIGHT, 1182 + 1171 No
Engine Selected), then `CustomPlaneStore.Save`; funds are never checked (PLAN-hangar Decision 2).
Editing a saved plane starts from a copy made through the store's own canonical serialisation, so
abandoning an edit cannot touch what is on disk. `DeleteSaved(name)` is the plane-selection
screen's Sell Plane (`ps_b_sellp`) in a build with no economy: it removes the file, re-reads
`Saved` and clamps the cursor back into the shortened list. `HangarPlaneSelectionPage` reaches it
through a two-stage gesture rather than a stepper, since a stepper on a destructive action is one
stray nudge from losing a build: a trailing `Delete a saved plane` row (offered only while
anything is saved) opens a second list whose every row reads `Delete <name>`, plus Cancel, so the
press that removes a plane names the plane it removes. The list closes when it empties. The
launchscreen's own `RefreshRoster`, which `CloseHangar` runs on every exit, is what keeps a
picker cursor inside the shortened roster afterwards.

`IHangarPage` is the mount point Wave C's remaining items fill: `Title`, `RowCount`, `RowText`,
`Detail`, `Step` (the launchscreen's live ←→ stepper), `Accept` (returning false hands the press
back to the flow, which advances), `OpeningRow` (the row the cursor lands on when the flow arrives,
0 for most screens and the current pick on the two pick screens), `TotalsPlane(row)` (which plane
the shell's totals row prices while that row is focused, the scratch plane by default and null to
hide the row), `Art`, an optional decoded `TgaImage` plus caption the shell
renders (null by default via `HangarPage`; the C22 seam every art-bearing screen uses), and
`RowArt(row)`, the same thing again for the focused row alone (only the paint screen's decal rows
have one). All plain
text, plain indices and raw pixels, so a page is engine-free and testable and the shell needs no
change to draw one. `HangarFlow.DataRoot` (optional, null in tests) is where a page resolves its
TGAs; absence reads as no art. `HangarPage` is the base carrying the flow, the scratch plane and
the heading resolved from the screen's own langui id (1017/1004-1010/1401);
`HangarFlow.PageFor`'s switch is the single line each of C22-C26 replaces; `HangarPlaceholderPage`
(the right heading, a Continue row and a real summary of what the scratch plane carries, editing
nothing) stands only in the switch's default arm now that every screen has its own page.
`HangarPlaneSelectionPage`, `HangarAirframePage`, `HangarEnginePage`, `HangarArmourPage`,
`HangarGunsPage`, `HangarHardpointsPage`, `HangarPaintPage`, `HangarNamePage` and
`HangarPurchasePage` (each its own file) are all real; the placeholder stands only in the
switch's default arm. `HangarFlow.TotalsLine` (with its `TotalsOverweight` colour flag) is the
persistent second stats line the launchscreen draws under every hangar screen's heading
(PLAN-hangar Decision 10): the build's total price and weight against the airframe's capacity,
recomputed from `HangarEconomy.Price` on demand and carrying the original's OVERWEIGHT word
(langui 1227) when over. Which plane it prices is the page's answer, through `TotalsPlane`: the
plane-selection screen prices the saved plane under the cursor and hands back null on its action
rows (New Plane, the delete stage, Cancel), where the line is "" and the shell draws nothing.

The airframe-defaults ask (string 206) lives on the flow: `DefaultsAsk` names the airframe whose
defaults are on offer and `DefaultsAskText` carries the formatted question (%1 the new airframe,
%2 the plane being built, its name or its previous airframe's name). `PickAirframe` raises it, so
only an explicit confirm on a row that is not already the pick asks (the switch itself stands
either way); a new plane arrives with `AirframeChosen` false, nothing ticked and nothing asked,
and `StartFromSaved` starts chosen and never asks. `AnswerDefaultsAsk(true)`
runs `LoadAirframeDefaults`: gun picks and per-wing hardpoint counts read back off the airframe's
stock fit (`StockFits`, the A3 mapping and `StockWingCounts`, D32's wing rule reversed), engine
id 1 (the stock Lvl-2 tier), and armour from the stock zone allocations (`ZrdrPath` through
`PlaneStats`, pools / 5); a missing source loads that default empty. Off-engine coverage:
`CSVM.Tests/HangarFlowTests.cs`, `CSVM.Tests/HangarAirframePageTests.cs`,
`CSVM.Tests/HangarEnginePageTests.cs`, `CSVM.Tests/HangarArmourPageTests.cs`,
`CSVM.Tests/HangarGunsPageTests.cs`, `CSVM.Tests/HangarHardpointsPageTests.cs`,
`CSVM.Tests/HangarPaintPageTests.cs`, `CSVM.Tests/HangarNamePageTests.cs` and
`CSVM.Tests/HangarPurchasePageTests.cs`.

## src/UI/CampaignFlow.cs
The campaign's out-of-mission screens as one engine-free flow, the same split `HangarFlow` uses: a
page owns its rows and its navigation, the launchscreen owns every Godot control. Screens are a
stack, not a fixed order, because the campaign's navigation is a graph; `GoTo` returns to a screen
already open instead of stacking a second copy, and backing out of the first one ends the flow with
`CampaignExit.Cancelled`. `CampaignFlow.Registry` maps a `CampaignScreen` to its page factory and is
the wave's whole mount point: a new screen is one page file plus one line there, with
`CampaignPlaceholderPage` covering any screen not yet registered. A page may hand the shell a
`HangarArt` (the same art column the hangar draws) and a `CampaignTextEntry`; while that field is
armed, `CapturesText` tells the shell the keyboard is typing, and the flow's own cursor axes edit
the name instead of the list. Profiles are created, read and deleted only through
`CampaignProfileStore`, so a deletion takes the profile directory and never `user://Planes/`. That
store also remembers which profile was last seated, by name; `ICampaignPage.OpeningRow` is where a
screen says which row the cursor arrives on, and the roster page answers with that profile's row
(`docs/org/campaign-board.md`, "Which player the profile screen opens on").
⚠ A campaign page draws as a composed board, not as the shared `BoardMenu` idiom: it contributes its
`Pictures`, `Strokes` and `Captions` and names which of the screen's authored buttons each row
presses through `Button`, and `CampaignBoards` supplies the geometry. The `HangarArt` a page still
hands over is the hangar's own art column and is unused on the campaign path.
Off-engine coverage: `CSVM.Tests/CampaignFlowTests.cs`,
`CSVM.Tests/CampaignRosterPageTests.cs`, `CSVM.Tests/CampaignTextEntryTests.cs`,
`CSVM.Tests/CampaignCabinPageTests.cs`, `CSVM.Tests/CampaignPreviousMissionsPageTests.cs`.

## src/UI/BoardFit.cs
How the original's fixed 800x600 campaign dialog space lands on an arbitrary window: one uniform
scale on both axes, the board centred, the remainder letterboxed. A `record struct` with `X`, `Y`
and `Length`, so every element goes through the same mapping and only the scale changes. ⚠ The two
rejected alternatives (an integer scale, a fit-to-height bleed) and why the art must be sampled
nearest rather than smoothly are in `docs/org/campaign-board.md`; that decision is inherited by
every campaign screen, so changing it here changes all six. A viewport with no area falls back to
1:1 rather than a scale nothing can draw at. Off-engine coverage:
`CSVM.Tests/ComposedBoardTests.cs`.

## src/UI/ComposedBoard.cs
What a composed campaign screen is made of, engine-free: pictures at authored pixel positions,
connector strokes, text lines, button plaques, and flowed list widgets, each in draw order.
`BoardNote` is the last of those: a widget's entries plus its authored wrap box and spacing, which
`Flow(height)` turns into placed lines. It takes the measurement as an argument because how tall a
wrapped entry drew is a font metric the engine-free half does not hold, and it is a layer of its own
rather than one `BoardLine` per entry for exactly that reason (`BL-490`). Carries the two state rules
as statics — `PlaqueFrame` picks a four-frame strip's disabled/normal/rollover/depressed frame,
`PlaqueInk` picks a one-frame plaque's label face, which is the whole of the briefing's
`BtnLabelNormal`/`Rollover`/`Activate` triad. `BoardArt` names a bitmap and how many stacked frames
it holds; the renderer, not the model, resolves it to a file, which is what keeps the JPG the screen
backgrounds ship as out of the engine-free half.

## src/UI/CampaignBoards.cs
The authored geometry of all six campaign screens plus the composer that turns a page and a cursor
into a `ComposedBoard`. Every coordinate is the original's own: `ASSETS\LAYOUT.CSV` for the five
script-driven screens, `Briefing.zrd`'s own chrome for the briefing, and, for the rows the shipped
layout leaves as unresolved authoring macros, a measurement off the reference screenshots recorded
in `docs/org/campaign-board.md`. A page names one of these buttons per row through
`ICampaignPage.Button`; every other row lists down that screen's own text widgets via `TextSlot`.
`ObjectivesNote` is the one widget that is not a slot list: the briefing parchment's `LIST` flows its
entries, so it carries a wrap box and a spacing instead of a pitch and the renderer measures it.
⚠ `Labelled` on a slot, not the frame count, decides whether a plaque's words are drawn over it: the
generic paper buttons are four-frame strips that still carry an `IDS_*` label. Off-engine coverage:
`CSVM.Tests/ComposedBoardTests.cs`.

## src/UI/ComposedBoardView.cs
The Godot half of the campaign boards: draws one `ComposedBoard` over the whole window through
`BoardFit`, with `TextureFilter` pinned to Nearest so the authored pixel grid stays hard. Owns the
texture cache and the only art resolution there is — `extracted/rimage/<name>.png` for mission art,
`extracted/rof/ASSETS/GRAPHICS/<name>` for screen chrome — loading through `Image.LoadFromFile`,
which reads the JPG backgrounds no engine-free decoder here covers. A miss is cached, so an absent
extraction is probed once per name and the screen degrades rather than throwing. Supplies the font
metric a `BoardNote` cannot take for itself (`Measure`), which is the only reason a flowed list is
not composed engine-free. Carries the one
piece of chrome that is not the original's: a two-line hint band across the top of the board with
the focused row's description and the controls line, because the original said both with a mouse
pointer and a pad has none.

## src/UI/BoardPalette.cs
The ink a campaign board writes in, one palette per background family, because the screens are
painted art and the grey the flight check's forms use is invisible on the cabin's dark hangar. The
flight check and ammo values are their layout rows' own ARGB fields; the rest are chosen to read on
their background, and `docs/org/campaign-board.md` says which is which.

## src/UI/BoardMenu.cs
A board's cursor and item list, engine-free so the selection rules test off engine the way
`PauseState` does. Holds no input source: the board polls its menu owner through `MenuInput` and
feeds one frame's result to `Handle(move, accept, back)`, which is what stops a pad steering a menu
it does not own. Returns whether the highlight moved, so a board repaints only when it has to.
Opens on the first item, and the boards order their rows so the first is the harmless one (Resume,
else **Photo Mode**) — a stray confirm on a menu that just appeared then cannot destroy a run. That
is why Photo Mode leads a results board rather than trailing it: the alternative resting row is
Restart, which throws away the run just finished (`BL-429`). Confirm
beats back in the same frame, the row having already been chosen. A results board's menu is not
`Dismissable`: dismissing it would leave the player in a halted world with no way back, so it
answers no back key and advertises none. Off-engine coverage: `CSVM.Tests/BoardMenuTests.cs`.

## src/UI/LoadBoard.cs
The load screen drawn over the whole window while a session builds: the original's own composed
artwork (`docs/org/loading-screen.md`) through `ComposedBoardView`, so it inherits the campaign
boards' authored-pixel surface and `BoardFit`'s scaling rule. Two compositions, the split the
original makes: a campaign launch gets the chart sheet (`loadframe`, the unlit scale at `90,548`),
everything else the blackboard (`loadframempt2`, its three authored photographs each centred on its
own coordinate, the unlit lamp strip and one still propeller frame). Free flight and dogfight are
ours rather than the original's and take the non-campaign screen. Populated in `_Ready` rather than
`Build`, since the view sizes itself off the viewport and a node outside the tree has none to read.
Its subject line names the chapter and the flight the way a player picked it —
`Launcher.LaunchSubject` takes an Instant Action mission's name from
`InstantAction.MissionTypeLabel` ("Attacking a Zeppelin"), never `SessionSpec.ModeName`, which is
the log file's internal tag ("fly", "stunt") and not a player's word. ⚠ The bar draws its UNLIT
strip and the propeller one still frame, and neither ever moves: a fill and an animation both need
the build decoupled from the draw, and while the build stays one synchronous block `StartupProfile`
reports its phases only after the fact, so a moving bar would be a fiction. ⚠ The two text lines are
placed by us; the decode carries no text coordinates. The Launcher owns the show/free pair; see its
entry for the deferred-build handshake.

## src/UI/BoardMenuItem.cs
The rows a board menu can offer — Resume, Restart, Exit. The board owning the menu decides which it
carries and what each does; Resume appears only on the pause board, and Exit's label follows
whether the session can return to the launchscreen or only quit.

## src/UI/CursorRow.cs
One centred list row with a ▶ cursor, shared by every menu that has one: the launchscreen's screens
(`LaunchMenu.Row`), its per-player aircraft panes, and every board menu through `BoardMenuView`.
⚠ The marker is a cell of its own, never a prefix on the row's text. Prefixing a centred label with
`"▶  "` when selected and `"     "` when not centres it on the PADDING too, and the two are not the
same width, so every unselected row drifted sideways — which is what made these menus read as
uncentred. A fixed-width marker cell plus an identical mirror cell on the right puts the label on
the panel's centre line in both states, and centring the three as a GROUP (label at its natural
width, not expanding) is what keeps the marker beside the text instead of out at the panel edge.
The marker is emptied rather than hidden when unselected: a Godot container skips invisible
children, which would collapse the cell.

## src/UI/BoardMenuView.cs
Draws a `BoardMenu`'s rows as `CursorRow`s (dim rows, the highlighted one gold behind a marker)
inside the board style all five boards already share, so the cursor reads the same wherever it
appears and a layout fix lands once — in `CursorRow`, which the launchscreen draws too. `Refresh()` recolours from the current highlight,
touching only label overrides. The footer is the button legend, since nothing else on a board
teaches the cursor; a results board's has no back key to name.

## src/UI/BoardMenuHost.cs
`BoardMenu` + `BoardMenuView` + the reader, kept together so a board wires a menu in two lines
rather than restating the poll-handle-repaint order five times. `Build` primes the reader, so a
button still held from whatever raised the board is not read as a fresh press. ⚠ `Poll` takes WALL
time: the clock this menu is holding does not advance, so auto-repeat on sim dt would never fire.
Reads `PadBack`, not `Back` — Esc and Start reach the pause toggle through `FlightController`, so
the combined back would act twice on one press.

## src/UI/MenuInput.cs
One player's menu input source — keyboard flag (player 1 only), `Pads` binding, edge/auto-repeat
state; `Poll(dt)` fills Move/MoveX/Accept/Back/PadBack/Start (polled: actions can't read a
named device). `Pads` is nullable, null meaning every connected pad, which is the same binding
`FlightController.PadDevices` takes — a single-player session has no per-player assignment to hand
over. `PadBack` is the pad's B alone, for a reader whose Escape is spoken for elsewhere; a board
menu's is. Serves both the launchscreen and the in-flight board menus.
`MoveX` (Left/Right) is `Move`'s horizontal twin, added
so an Instant Action wizard screen can carry a vertical list cursor and a horizontal stepper at
once without either read starving the other: MissionType's lives, WaveEdit's four fields and
Wingmen's count/aircraft all read it — every other screen ignores it.
`Typed` (the letters, digits and spaces pressed this frame, upper case under Shift) and `Erase`
serve a screen with a text field; they are polled and edge-detected per key like everything else
here, not read off an input event, so text and navigation share one clock. `PadMove`/`PadMoveX` are
the pad's own halves of the two cursor axes: a screen whose keyboard is typing reads those instead,
since W, A, S and D are letters there and the combined axes would move the cursor as one types.

## src/UI/SplitScreen.cs
The splitscreen rig for 2–4 players (1P never constructs it, keeping that path untouched): black
gutter backdrop, one `SubViewport` pane per player sharing the main `World3D`, plus the
`PlayerColor`/`PlayerTag` identity table.
**The pinned 3D audio listener model (2026-08-15): every pane is a listener**
(`AudioListenerEnable3D`). Godot 4.7 takes the per-channel MAXIMUM over all listener-enabled
viewports of the `World3D` and culls `max_distance` per listener, so an emitter is heard at its
NEAREST pane's volume with no N-fold buildup and no manual attenuation; the cost is that panning is
unioned across panes, which share one stereo out. Without it a splitscreen session has NO listener —
the main camera stands down here and a camera is in the `World3D` listener set only while current —
and every `AudioStreamPlayer3D` in the world goes silent, uncounted and unlogged. Pinned by the
`splitscreen-listeners` suite.

## src/UI/ScreenFlash.cs
The full-screen colour wash: **two channels over one pixel per pane, one hidden `ColorRect` per
rendered view** (`HudLayers.WorldOverlay`, under each rig's `HudParent`, built with the rigs so
every runtime can be handed the same sinks). The **ramp channel** is the `FBFX_COLOR_FROM_TO` wash,
a close HE, AP or flak burst ramping the picture from one RGBA to another over the event's run time.
`AnimRuntime`'s handler pushes `(from, to,
run_time, origin, radius²)` into `Play`; the node lerps in RGBA on `GameClock` sim time and ends — it does NOT
hold the `to` colour, because the original re-arms its frame-buffer object for the current frame only
and a completed chain simply stops re-arming. `Play` **replaces** whatever that pane is running,
which is the original's composition rule literally: one process-wide state a second burst overwrites
(decode in `docs/formats/anim-definitions.md`).
The **blend channel** (`PlayBlend(playerIndex, colour, weight, duration, startDelay)`) is the
victim-routed wash of a sonic, flash or smoke hit, one `BlendWash` per pane (`src/UI/BlendWash.cs`,
below), addressed by the struck aircraft's `FlightController.PlayerIndex`: a human seat's index is
its pane index by construction (`FlightRigAssembler` assigns `pi`), an AI's is `ShooterIdBase + n`
and out of range, so it paints nothing. No camera is read on this channel. The two composite at
paint time only (`Apply`: `BlendWash.Composite`, the standard alpha "over" of the wash on top of the
ramp, folded into one RGBA), so a pane with no blend wash paints exactly the ramp's own colour and
the HE/AP/flak picture is unchanged by the channel's existence; `CurrentFor` reads the composite,
`BlendFor` the blend state alone. `Advance(dt)` is `_Process`'s body, exposed so a suite can step
both channels on its own clock. **Per pane, not one global**: the original holds one wash state for
the whole machine (`DAT_0064ef9c` and neighbours), which would blind viewer 1 when viewer 3 is
flashed; that divergence is deliberate (`docs/PLAN-ordnance-types.md`, Decision 2). `--debug-wash=N`
addresses two overlapping scripted washes to viewer N so the channel can be seen with no weapon
firing it (`GameSession._Process`). Pinned by the `fbfx-flash` suite (routing: player 2 addressed,
pane 1 untouched, ramp composited under) and `BlendWashTests` (the rules).
**Which panes the RAMP washes: every pane whose own camera is inside the burst's authored
radius**, read off the session's `ViewerSet`, which `GameSession` hands to `Build` alongside the
same rig list the panes come from — so pane *i* and camera *i* are the same rig by construction, and
a set that does not match the pane count is not indexed at all (every pane washes, the labs' case).
The radius is the wash def's own `If PlayerRange` gate — `10000` m² = **100 m**, and all 24 shipped
wash defs (`he_ground_effect`/`ap_ground_effect`/`flak_effect` × 8 chapters) author exactly that one
gate — so the rule here is the original's own gate re-asked per player rather than a new constant.
Radius 0 means ungated and paints every pane; the intro cutscene's `gi_scene1` is the one such
carrier, and an ungated wash is not a proximity effect.

## src/UI/BlendWash.cs
One pane's victim-routed wash state, the original's `FUN_0042e9d0` (start) and `FUN_0042eb80`
(tick) held per pane instead of in their one global; pure state and arithmetic, no node, so
`BlendWashTests` covers it off the engine. `Start(colour, weight, duration, startDelay)`: a first
hit takes the weight as `Peak` and starts the displayed `Weight` at 0; a hit landing on a running
wash **blends**, `Peak' = p + w − p·w` and the colour mixed as `(old·p + new·w) / (Peak' + w)`, the
original's own arithmetic including its normalisation by the NEW peak plus the incoming weight
(white then red at full weight reads pink `(1, 0.5, 0.5)`), then restarts the envelope's clock
without dropping the displayed weight. A non-positive duration clears the pane, as the original's
routine does. The **envelope**, stepped on sim time: attack over `0.15 × duration` (the displayed
weight climbs `dt/attack` of the peak per step, capped at the peak), sustain at the peak, release
over the last `0.35 × duration` (the peak sheds `dt/release` of itself each step, so it decays toward
`1/e` of the sustain by the cut), then a hard cut at the duration. The release decays `Peak` itself,
so a re-hit late in a wash blends against a lighter one. `startDelay` holds the pane clear before
the envelope begins and is read on the first hit only; the original passes 1.0 s for the sonic/flash
wash and 0 for the smoke wash (its two callers, `FUN_004b9bc0` and `FUN_004b8fd0`), which the
consumers `D15`/`D18` author. `Composite(under, colour, weight)` is the paint-time rule
`ScreenFlash.Apply` uses: the wash "over" the ramp's RGBA, `a = a₁ + w(1 − a₁)`, colour
`(c₁·a₁·(1 − w) + c·w) / a`, clamped; weight 0 returns `under` unchanged.

## src/Flight/PlayerRig.cs
One rendered view's state bag: index, camera, optional `SubViewport`, `HudParent`, `VisualLayer`,
the player's FlightController, and private camera-anchored copies (`Horizon`/`Deck`/`Whiteout`) —
those re-anchor to the view's camera every frame, so N players need N of each. The ambient cloud
field is deliberately not one of them: the authored fogvol clutter is world-anchored static
geometry every pane shares, gated per-view through `Camera.CullMask` instead of a duplicated
subtree.
`CameraWeatherState` (1/2/3) is the same shape as the deck
regime: a per-rig field, not shared, because splitscreen panes can sit in different states at the
same instant. Written each frame by `Session/WeatherRig.Tick`; rig 0's value drives the per-state
fog switch there — the fog globals are session-wide, so only rig 0's is read for them.

## src/Flight/ViewerSet.cs
`ViewerSet` — the "what do the cameras see" seam promoted out of
`ProjectilePool.Viewers`/`ScreenSize.NearestFloor`, the pattern the tracer floor proved 2026-08-10
([`org/tracers.md`](org/tracers.md)). `GameSession` owns one instance (`_viewers`) and `Bind`s it
once, right after `BuildRigs` returns (`StartSession`) — the same rig-camera list every rig loop
reads, single player included (one entry wrapping the main camera). `Cameras` hands back the raw
bound list unfiltered, for a consumer (`ProjectilePool.TracerFloor`) that needs each viewer's own
FOV and pane height alongside its position and already skips a freed instance itself; `Positions`/
`Poses` are the two derived shapes B13 and B11 consume, respectively — position only, or position
plus forward for a view-space depth comparison. `Poses(into)` fills a caller-owned buffer for B11's
consumer, `EffectAmbience`, which republishes the set every frame and would otherwise allocate a
list per frame. `ScreenSize`'s arithmetic did not move: this class carries cameras, not the
screen-size/view-depth math itself.
Consumers today: `ProjectilePool.Viewers` (tracer floor), `WeatherRig.Tick` → `EffectAmbience`
(the puffer distance fade), both handed the session's one instance at construction, and
`UI.ScreenFlash` (B12's wash routing), handed it at `Build`. That last one reads `Cameras` rather
than `Positions` because it needs the index to stay aligned with its own per-pane rects, and
`Positions` skips a freed camera. `Positions()` also feeds `AnimRuntime.LightViewerPositions` (B13's world-light budget,
`WorldLights.Commit`) via `GameSession`'s `() => _viewers.Positions()` closure — the same
resolved-per-call shape `PlayerPosition`/`PlayerPositions`/`ListenerPositions` already use, since a
pane's own camera moves every frame and the runtime is built before any rig's transform is final.

## src/UI/LiveryLab.cs
The `--viewer` livery editor (key L): squadron stepper (loads the squadron's whole livery via
`LoadSquadronLivery`), per-slot RGB sliders, decal steppers, random livery, copy-CLI-args.

## src/UI/NodeLabels.cs
Floating node-name labels (key F16) in both the static viewer and flight, cycling Off → Meshes → All;
`--debug-names[=meshes|all]` presets the mode at launch. In splitscreen the nearest/de-clutter pick
is P1's viewpoint alone (rig 0's camera); every pane still renders the resulting labels, since they
are ordinary world-space children of the root.

## src/UI/MarkerOverlay.cs
The `--viewer` marker overlay (key K): draws every firepoint / pylon / target on the parked
aircraft as a coloured gizmo + billboarded label (firepoints orange, shared-mount firepoints
magenta, pylons cyan, target green); `--markers` opens it at launch. Reuses `MarkerRig.Classify`
+ `GroupCoLocated`, so its gizmos agree with `--dump-markers` by construction.

## src/UI/PhotoModeHud.cs
Photo mode's only screen furniture and its way out: a hint line naming the bindings on a
`HudLayers.Board` layer of its own, and the Escape / pad-`B` read that raises `Exit` for
`GameSession.ExitPhotoMode` to act on. Decides nothing about the mode itself.
The hint **fades** (5 s lit, 1.5 s out, both TUNE) rather than persisting or toggling: the mode
exists to compose a frame and a permanent strip would be in it, while a mode that has swallowed the
menu with no visible way back is the worst thing it could be. The fade runs on WALL time — photo
mode holds the clock, so a sim-timed fade would never start. Pad reads go through the seat's own
`Pads.For` filter, so in splitscreen another player's pad cannot close a mode that is not theirs,
and the exit press is marked handled so it cannot also reach the suspended board behind it.

Photo mode itself lives in `GameSession.EnterPhotoMode`/`ExitPhotoMode`: it sets `CameraOwned`,
hides the whole pilot HUD (`FlightController.SetPilotHudVisible`), stands up a `SpectatorCamera`
on the pane with the rig's own device filter and `LockCandidateAircraft`, and locks onto the
player's OWN aircraft — `FollowNode` seeds from the current eye, so following what the camera is
already looking at never jumps, while locking any other plane would keep the offset and teleport.
The halt is never dropped, so the world stays the still frame the board froze.
⚠ Suspending a board is hide AND `ProcessMode.Disabled`, not hide alone: a board left processing
still polls its owner's menu reader, so the cursor keys would drive an invisible menu while the
same keys fly the camera — the very collision this mode exists to remove.
⚠ `FlightController.InPhotoMode` silences that node's pause key for the duration, or one Escape
would both leave the mode and unpause the session behind it.
⚠ Leaving primes every board's `MenuInput` before the cursor comes back. `MenuInput` polls raw key
state, which bypasses the handled flag the exit press set, so an Escape still under the player's
finger would read as a fresh press on the board that just returned and dismiss the pause it was
meant to reopen — `BL-279`'s mechanism, a third time.

## src/UI/PerfHud.cs
The frame-cost readout (key **F14**): fps, current frame cost and the
worst recent frame, cycling Off → Compact → Full → Off; `--debug-fps[=compact|full]` presets the
mode at launch. Built once by `Launcher` (never per `GameSession`, never per splitscreen pane) —
fps/frame-cost/GC are process-wide facts, so one readout for the whole window is correct and a
per-pane copy would just be four identical readouts at four times the layout cost. That hosting
is also what makes it work at the launchscreen, in `--viewer`/`--freecam` and in flight for free.
Fed the same raw `Stopwatch` `frameMs` `HitchMonitor` ticks on (never Godot's `delta`), every
frame, unconditionally — the worst-frame peak has to already be warm the instant F14 is pressed,
or it would have nothing to say about the hitch that made someone look. Off by default and builds
nothing until switched on, so the 11 golden screenshots stay byte-identical.
Both its controls are placed by their **right edge only** (`PlaceTopRight`: `OffsetRight = -8`,
`OffsetLeft` derived from the width, `GrowHorizontal.Begin`), so the readout's right edge stays
8 px inside the window at every window size and font scale while the text grows leftward. A
right-anchored control's `OffsetLeft` is where its box was pinned, not where grow-left ended up
drawing it, so reading it back (or assigning `Size`, which derives `OffsetRight` from it) walks the
control a full width off the side of the window; that is what put the frame-time strip off screen,
and `--run-tests=perf-hud-layout` is the arm that holds it.
On `HudLayers.PerfReadout` (11), **above `HudLayers.Board`**: the launchscreen's background is a
full-screen opaque `ColorRect` on `Board`, and this readout has to read there too. Sized off
`HudMetrics.ReferenceHeight` through the plain window-height ratio, not `HudMetrics.Scale` —
that method's `PaneFactor` damping is exactly wrong for a control that isn't per-pane.
**Full** adds four lines under Compact's fps/frame/worst headline — the
current frame's `FrameCounters` split (script/render-cpu/gpu/physics ms), its draws/prims/nodes/
mem terms, `GC.CollectionCount` per generation (raw counts, not deltas — a live readout reads
better as "gc2 has fired 3 times" than as an almost-always-zero per-refresh delta), and C8's
breadcrumbs (`PerfSample.SnapshotInto`, the same `site:callsxms` grammar `HitchSidecar`'s log line
uses) — plus `PerfHudStrip`, a second top-level `Control` in the same file (the `PerfSample.cs`
precedent for more than one type per file) drawing a rolling bar graph of recent frame times with
the trigger threshold marked as a line. Every Full term is a SECOND VIEW of data collected
elsewhere, never a new sample: the split/count/memory terms are the same `FrameCounters` read
`HitchMonitor.Tick` was just handed, and the strip reads `HitchMonitor.CopyRing` — a new accessor
onto the monitor's own always-live ring buffer (distinct from `Last.Ring`, which only advances on
a trigger) — every draw, so the display and a hitch record can never disagree about the same
frame. `perfHud.stripFrames` (TUNE, default 120) sizes the strip, clamped to
`HitchMonitor.RingFrames` since asking for more than the ring keeps is meaningless.

## src/UI/MeshLab.cs
The geometry/shading lab (key M): normal lines, smoothing-seam wireframe, collider boxes, light
sliders + headlight, cull × normal-source override cyclers (`--debug-mesh=` scripts them). Two
shapes: the `--viewer` lab owns the parked plane; the scoped lab (`--freecam`/`--anim-lab`, over a
`SelectionService`) attaches to the current selection on M and restores on change/deselect.

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
(`cs_name` ancestor via `SelectionService.NameOf`, surface id via `ProjectilePool.SurfaceIdOf`,
reported as `id/name`, distance) and re-parks the held plane on that same ray at the panel's
stand-off through `PlaceHeld`;
shift-click aims without moving, and an orange ball marks the aim point. The scripted twins all fire
on the first physics frame, most specific first — `--weapon-target=x,y,z`, then
`--weapon-surface=<registry name>` (nearest collider carrying that surface id — any of the
fourteen since B12, so `dirt` now means id 13 and NOT "everything untagged", which is `default` —
measured to the nearest
collision VERTEX, since a chapter's water tiles all sit at the world origin), then
`--weapon-click=x,y[,aim]` — and every one of them ends in the same `PlaceOn` as a real click, at
`--weapon-standoff=` metres. **V** hands the rig's camera to a `SpectatorCamera` and back
(`--weapon-camera=free|<frames>`), the controller standing down via `CameraOwned` in between.
`--weapon-cycle=N` steps the weapon list every N physics frames; stepping, placing and the camera
hand-off are the only things this node does per frame. In splitscreen the lab stays P1-only by
design, one panel on rig 0's aircraft, camera hand-off included; a log line says so and the other
panes fly normally.

## src/UI/PanelFocus.cs
`Strip(subtree, who)` — makes every `Control` under a panel unfocusable and logs the tally
(`N control(s), M made unfocusable, focusable_left=0`), the invariant every panel hosted in a
**flight** session must hold. A focused `Button` answers Space with "press me again", so the pilot's
fire key re-fires the last-clicked stepper instead of the guns, and the arrow keys walk the focus
chain instead of reaching the aircraft or the lab's orbit camera (user-reported on the weapon lab,
2026-08-03; the flight damage lab had it too — 11 and 5 focusable widgets respectively).

## src/UI/SelectionService.cs
The shared world selection in `--freecam`/`--anim-lab`: left-click picks the mesh under the
cursor, PgUp/PgDn walk its `cs_name` ancestor ladder, a breadcrumb HUD line + wireframe box show
the current rung. `Current`/`Ladder`/`Level`/`CurrentBox` + the `Changed` event are the state the
other inspect tools read; `Select(node)` is the programmatic entry; `--debug-select=x,y[,up]`
replays a click for scripted runs. `ExtraRoots` walks props parked beside the world content rather
than under it (the anim lab's `--plane=` prop), each also capping its own ancestor ladder.
`SubtreeWorldAabb` is the shared box measurement (`AnimRuntime.VisualOriginOf` and `NodeLab` read
it too) and walks children by index with a prebuilt `StringName` for the overlay key: `GetChildren()`
and a string-to-`StringName` conversion each allocate a finalizable wrapper per node visited, which
a walk over a world subtree cannot afford on a per-frame path.

## src/UI/TargetingOverlay.cs
The targeting overlay (F15, `--debug-targets`): a per-frame line from every turret gunner
(`TurretController.TargetPosition`) and AI gunner (`AiGunner.Target`/`GroundTarget`, D36) to its acquired target,
coloured by the gate holding the trigger (`TurretController.Gate`), with that gate named per shooter
in the HUD. Depth test off, since the line into a hull is the one worth seeing. In splitscreen the
world-space lines draw in every pane on their own (default render layer, in every camera's
`CullMask`) while the HUD roll-call is drawn once for the window, like `PerfHud`, because it is
process-wide combat state.

## src/UI/DebugKillTarget.cs
The debug kill key (F17, `BL-534`): kills P1's currently selected `Flight.TargetSelection`
target through its own death path, so kill-count and `ObjectiveGraph`/`GroupLiveCount`
bookkeeping see it exactly as a real shot would, never by freeing the node. Routes on the
selection's `Candidate.Source` type: a `FlightController` crashes through
`FlightController.DebugForceCrash(killer)` — the same attributed `Crash`/`Downed` path
`--debug-scoreboard`'s scripted kill and `--crash` already use; a
`Mech3.DestructibleRegistry.Instance` (a zeppelin gasbag, cannon or engine) is destroyed through
`AnimRuntime.DamageAt(inst.Anchor, inst.MaxHealth + 1f)`, the same call a rocket makes and
`WorldDamageLab`'s own Kill button uses. A `TurretController` selection (world emplacement or
carried) is left inert: `Flight.TargetRef.Health`'s own decoded rule is that a turret carries no
`HEALTH` key at all, and nothing in the engine toggles a turret's kill switch
(`TurretController.Alive`'s healthy-node visibility) today, so routing a kill through it would be
an invented mechanism, not a decoded one. Nothing selected is inert too. P1-only, the precedent
F5's `WorldDamageLab` and F51's weapon lab already set for a single-pane debug tool; wired into
`GameSession` beside the F15 `TargetingOverlay` build, reading P1's own `FlightController` and
the session's `AnimRuntime` through closures for the same reason `TargetingOverlay` does (both
outlive the line that builds them: waves activate and turrets die long after). The routing switch
is exposed as `KillSource` so a suite can drive it against hand-built sources with no live
`AimCandidateSet` scan behind it.

## src/UI/TileGridOverlay.cs
The map-edge tile-grid overlay, flag-only (`--debug-tilegrid`; no key is bound): every ground tile
tinted 20 % by repetition band, so one colour band is one block. `--map-edge-block=` and
`--map-edge-mode=` set the depth and the fold once at launch. This is the instrument that settled
the map-edge fold; the measurements it produced are in
[formats/world-structure.md](formats/world-structure.md).

## src/UI/ColliderOverlay.cs
The collision wireframe overlay (key C, `--collision=show`/`--debug-colliders` script it) in
`--freecam`/`--anim-lab`/`--fly`: one `ImmediateMesh` per collider host, colour-coded by the
**surface id** its body resolves to (`0/default`, `1/water`, `13/dirt` …) plus the three owner keys
neither surface tag decides (clutter / plane / other), rebuilt from the live tree on every show. A
colour→key legend (`BuildLegendText`, sourced from `ColorFor` alone so
a palette change can't desync it) sits under the summary whenever wireframes are actually shown —
never for the "no collision built" notice, an empty-legend echo of that same gap. Measured C2: 1,848
node-backed shapes + 10k–14k clutter placements. The id drawn is the RESOLVED one, not the raw
stamp (`EffectCatalogue.ResolvedSurfaceIds`), and it is a picture of what the engine will select,
not of the material data: the id is stamped per collider body, not per polygon, and the body's
displayed NAME still comes from the texture-derived class, which can disagree with the id on real
bodies (`analysis/surface-classification/FINDINGS.md`).

## src/UI/AiNetsOverlay.cs
The AI patrol-net overlay (F13; `--debug-ainets[=name,…]` scripts it) — added to every chapter
world by `GameSession.BuildWorldStage` (skipped on the `--node=` partial stage). Draws each net in
a stable id-derived colour (golden-ratio hue): edges as individual segments off the edge list,
sphere markers per node (tagged nodes bigger), one fixed-size `Label3D` per net with the trailer
(`M4ReinfAce#10 → player`), all depth-tested. Nets load lazily on first toggle; the census — one
line per net — goes to the `world` log. A HUD text field narrows the drawn set live by
case-insensitive name prefix. F13 is the first tenant of the F13–F24 debug-overlay key
range (`docs/controls.md`).
It also draws LIVE LEASHES while up: one `ImmediateMesh` line per AI aircraft, from the plane to
the node its follower is flying at, plus a short vertical tick at the plane end. The overlay knows
nothing about aircraft: `CollectLeashes` is an `Action<List<AiNetLeash>>` the session fills from
each pilot's own `AiNetFollower.CurrentTarget` (built before any AI exists, hence a supplier and
not a snapshot). `AiNetLeash.Steering` is `AiPilot.SteeringPatrol`, which the pilot REPORTS off its
own dispatch rather than the overlay re-deriving it from the mode: a leash for a plane that only
holds its node while pursuing draws dimmed, and the HUD line counts the two separately.
An ANCHORED net is drawn where it actually is, not where the file says: each net is one
`Node3D` of authored-space children, so `TrailerOffsetOf` (the session's `NetTrailerTargets`) is
applied per frame as that root's `Position` and nothing is rebuilt. Without the supplier every net
draws at its authored coordinates.

## src/UI/ClassOverlay.cs
The colour-by-class overlay (key X, `--debug-classoverlay` scripts it) — same mode set as
`ColliderOverlay` (`--freecam`/`--anim-lab`/`--fly`/`--stunt`), a findable-targets view rather than a
collision one. Mixes a class colour over every drawn mesh at 50 % (`TintStrength`), so a
target stays recognisable as itself: destructible (red, via `DestructibleRegistry.Resolve` — the
exact climb a weapon hit takes), facade (pink, via `SceneBuilder.ClassifyBillboard` on the source
`GameZMesh`, resolved back through the built node's `AnimRuntime.IndexMeta`), clutter (green, every
`MultiMeshInstance3D` under the world root — nothing else in this codebase parents one there),
everything else scenery (blue). Rebuilt on every X press rather than cached, clearing each tinted
node's `csky_tint` first. Deliberately keyed on neither surface tag: not the texture-derived
`SceneBuilder.SurfaceMeta` (decides only which collider a mesh's polygons join) and not
`SurfaceIdMeta` (answers "what happens when you touch this", the collider overlay's key) — two
unrelated objects can share either tag, and neither answers "what is this object".

## src/UI/NodeLab.cs
The node lab (N) in `--freecam`/`--anim-lab`: the world's `cs_name` tree, a search box, per-node
Frame / Hide-Show, a dependency readout for `SelectionService.Current` (anim defs, destructible
pool + DAMAGE_SEQUENCE, geometry/textures, colliders) and a destructibles view with coverage
columns, plus `ExtraRoots` top-level branches for props beside the world content (the anim lab's
`--plane=` prop, so its parts show in the tree, search and `SelectByName`).
`--debug-nodelab[=deps,dest,open,node=<cs_name>]` is the scripted twin. A row's text/colour
follow live `Node3D.Visible`, re-read on the panel's 4 Hz status cadence rather than latched off the
hide button, so a def re-showing a hidden node reads visible again on its own.

## src/UI/WorldDamageLab.cs
The world damage lab (F5) in `--freecam`/`--anim-lab`: the destructible pools of whatever
`SelectionService` holds, each with live HP, and a slider + Kill + Reset on the one a weapon hit
reaches, driving `AnimRuntime.DamageAt`/`ResetDestructible`. `--debug-damage[=node=,pool=,hp=,kill,
reset,tick=,open]` is the scripted twin (an ordered script, not a token set). Only the pool
`DestructibleRegistry.Resolve` names is drivable — a node can carry several `(def, anchor)` pools
(C1's water tower: compiled + reader wildcard) — and the rest are listed read-only with the reason,
since driving a twin would damage a pool nothing can ever hit.

## src/UI/OrbitCamera.cs
The static inspection view's orbit-camera controller (LMB-drag orbit, wheel zoom, AABB framing):
owns the orbit state and drives a `Camera3D` it does not own; `Frame` takes the eye + pivot the host
resolved, and `MergedAabb(Node3D)` merges a subtree's world-space mesh AABBs (shared with the anim
lab). `Frame`'s `lookAt` is a pivot point, not a direction — with the eye it also sets the orbit
radius the wheel and the drag then work in; `GameSession.FrameCamera` synthesizes a pivot on the
aim ray before calling in, since collapsing that back to a direction would leave the camera
spinning about its own eye.

## src/UI/AnimLab.cs
The `--anim-lab` debugger: a quiet `WorldSession` stage (`AutoStart=false`, seed pinned), fixed-dt
clock, transport button panel, def picker, `AnimTimeline`, `SpectatorCamera` freecam following the
shared selection, and a staged effect/crash anchor set so placeless on-call defs play at the camera.
Interactive (FixedAccum) frames draw each live transform-motion target interpolated between its
last two SIM poses (`GameClock.StepFraction`); sim poses are restored before any step runs, so
render smoothing never leaks into event held-pose seeding and FixedStep stays byte-identical.
Puffer particle spread is unseeded RNG (DIAG-21, docs/verification.md): same-step shots differ in
particle noise alone.
⚠ The picker toggle is `F18`, not `F` (`BL-428`). The shared `SpectatorCamera` owns `F` as its
target key, and this lab's `SetInputAsHandled` cannot protect a camera that polls raw key state,
so the two are separated by binding rather than by ordering.

## src/UI/AnimTimeline.cs
The anim lab's authored-vs-fired timeline (custom-drawn `Control`): authored blocks above, fired
ticks below, one lane per Initial sequence; a slanted first-firing connector = scheduler divergence.
`BuildLane` deliberately re-derives the documented scheduling rule independently rather than
calling the runtime's `SequenceRunner` — that independence is the whole instrument.

## src/SessionPaths.cs
Static resolver for the extracted-data paths (`ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr`) under a data root, plus `PreferUnzipped` (an unpacked sibling dir beats its `.zip`).
The `rtextureN` tier decode is on `docs/tooling.md`; the `--gamez=`/`--textures=` override policy
stays in `GameSession`, not here.

## src/SessionSpec.cs
Everything the command line settles about a session, as one immutable record: `Parse(args)` parses
**and resolves**; the pure arg parsers (`ParseVec3`, `ParsePlanes`, `ParseHold`, …) are public so
they are testable. `SessionMode` is closed — Menu/Fly/Viewer/Freecam/AnimLab — with modifiers
(`Stunt`, `Versus`) and `SessionProbe`; per-rule coverage lives in `CSVM.Tests/SessionSpec*Tests`.
`Versus` (`--vs`, `--vs-kills=`, `--vs-time=`) beats `Stunt` by fixed precedence, not last-wins.
`Resolve`'s step order and the purity contract (DET-9) are on the class and method themselves;
`FromMenu`'s no-re-resolve/Dogfight-lock/`iaDef` rules are on `FromMenu` and `IaDef`.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram` — the world+anim half of a session build;
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights. `Options.Decode`
(null by default) is where the `AnimProgram` and the aircraft archive come from when the caller
holds a `DecodeCache`; both are then shared and read-only.
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
repaint the ground with them. `Options.ExtraPrewarmNames` (D33) prewarms sound-group names the
`AnimProgram` never sees on its own (`ObjectiveScript.SoundGroupNames()` is the only caller today)
before the build's sound archive closes; without it a campaign mission's `WAKEUP_SOUND_GROUP`/
`COMPLETED_SOUND_GROUP` cue decodes to nothing the moment the archive that could decode it is gone.
`Options.CallbackHost` is installed on the runtime BEFORE the bind, since a bootstrapped intro
raises its codes the instant it starts, and `Options.CutsceneRoots` builds the two roots the
`world1` walk never reaches (`camera1`, and the `letterbox` bars, switched off) — only for a
mission whose start-anims reach one of `CutsceneController.IntroAnims` through their
`CALL_ANIMATION` closure (`BootstrapsCutscene`; C3/M03's is called, not listed), that arms an
approach trigger, or whose own mission list names a cutscene definition (`MissionCutsceneAnims`,
CM07's hangar drop), so every other session's node census is exactly what it was. The synthetic `camera1`
carries the gamez name and INDEX metadata a scene-built node would, because every compiled
cutscene binds it through its symbol table and an unbuilt claim makes the runtime drop the event.
`BuildCompositionFrames` stands up the same-shaped third case beside them, data-driven off the bound
program: a bodiless, childless gamez library root the program names as an `OBJECT_ADD_CHILD` parent
is the frame a cutscene composes its shot in (two in this install, CM02's `wingwalk_parent` and
CM07's `carney_pickup_parent`). It is built `TopLevel` — see docs/org/objectMotion.md's re-home rule.
`Options.PlanesGamezPath` feeds `AircraftStage` beside those roots, under the same gate: every
mission that plays a cutscene, because a mid-mission definition poses the flown aeroplane on the
same `player` marker an intro does and with no stage the pilot is held undrawn for the whole
sequence. The archive is opened nowhere else in this build, so no other session pays for it.
`Options.TriggerOwner` is installed on the runtime beside the callback host, and the opening
cutscene's own name is handed to it directly before the bind, since that definition starts inside
the start-list walk rather than through a trigger call (`BootstrapCutsceneOf`).
`ResolveLibraryRoot` is the lazy pool behind a mission or death call that names a library root:
copies are keyed on the caller's anchor AND on the authored call EVENT, so one definition calling
the same actor several times from one anchor gets a copy each, which is what instance identity
being `(def, anchor)` requires. Membership is the gamez's own parentless-root rule plus the one
staged actor the chapter gamez has no record of at all, `AircraftStage`'s `chuteman`: the staged
subtree is the first copy and further copies are duplicates of it, since the aircraft archive is
closed by then. CM02's crew bailing out is the whole of that case, three calls one second apart at
the same authored offset off the wing-walk frame; sizes are in `data/effect_pools.json`.
That staged actor is served to a call that NAMES a site and to no other. A copy is relocated onto
its call's site, and the figure's own script poses its children in world coordinates, so a call
naming no site (CM07's hangar drop calls `hdchute1` with no `AT_NODE` at all) has to keep driving
the actor where the stage put it: pooled there, the parachutist descends kilometres off the hangar
and the twin leg's call takes a second copy nothing animates. A gamez library root keeps the older
rule, its placement being the call anchor either way.

## src/Mech3/AircraftStage.cs
The aircraft-archive subtrees a story-mission intro, a chuteman-carrying drop cutscene or a
wing-walk capture animates, staged into a chapter world before the animation bind: `piratefighter`
built from the shared aircraft archive as a prop with no pilot, drawn in the archive's own shipped
state (ACTIVE) because no definition switches it on: `generic_intro`'s `gi_pfighter1` re-asserts
that state and parents it under the airship, while C1/M04's `pfighter11`..`pfighter13` only fly it
on SI scripts, so a prop built switched off leaves that intro's wingman out of the launch and the
dive; a bodiless `player` marker the flown aircraft is posed onto; `chuteman`'s parachutist
subtree (`chutemanparent` → `pilot`/`stamp`), switched off (the shared `chuteman.zrd` RESET_STATE)
until a mid-mission drop's own definition (e.g. C3/M01's `tdchute`) reparents and activates it; and
`FigureNodes` (`rope_ladder`, `pickup_cpilot`) under a switched-off holder rather than switched off
themselves, because nothing ever activates the wing-walking pilot: it is the capture's own
`OBJECT_ADD_CHILD` into the shot that draws him, which is what the original gets from a library
root its `world1` walk never reaches, and `PropNodes` (`anim_bloodhawk`, the Bloodhawk on the
hangar floor while the pilot parachutes in; `bloodhawk_gear`, the undercarriage the flown aeroplane
wears on the lift), built switched off the way `chuteman` is because the hangar drop's own legs
add and activate them (`Props`). All carry a rebased gamez index (`PointerBaseOf`: the chapter's node count rounded up to the
next multiple of 2500), which is what makes a compiled definition's cross-archive symbol table
bind them instead of claiming a name with no node. Built for every mission that plays a cutscene
(`WorldSession`'s intro, approach-trigger or mission-list gate), so every other session's node
census is exactly what it was. `StageFlown` puts the FLOWN aircraft's own airframe subtree in the runtime's node table under the same rebase, run when the rigs are built and again after an airframe swap: that is what makes a hookup definition's per-airframe branches decidable, since each tests one `player_<airframe>` node's active bit and then poses that airframe's own hook, wing fold and mount offset. The pose half is
`Session/CutsceneController.cs`; the decode is
`docs/formats/anim-definitions/cutscenes.md`.

## src/Mech3/SessionArchives.cs
`OpenFor(ArchiveIntent, gamezPath, texturesPath, soundsPath, zrdrPath, mute)` opens the five
archives one chapter build needs (gamez, textures, sounds, sound defs, sound groups) and returns
them alongside the `WorldSession.Options.TexturesOutliveBuild`/`SoundsOutliveBuild` pair
`ArchiveIntent` implies — `Session`/`Lab` (textures outlive; sounds only in `Lab`) or `Suite`
(neither). One seam replaces the two near-identical open sequences `GameSession.LoadArchives` and
`TestHarness.BuildWorld` used to hand-write (`BL-241`'s own fix note: the harness forgot
`TexturesOutliveBuild`). `StartupProfile.Mark`/`Record` calls are unconditional here, same as
`WorldSession.Build`'s own phases — a no-op with no session under measurement, which is what lets
the test harness drive the same code blind. The optional `decode` argument makes only the returned
`Gamez` a shared read-only instance (`DecodeCache`); the other four are always this call's own.

## src/Mech3/DecodeCache.cs
The decoded inputs a world build can reuse, keyed by the absolute paths they were decoded from:
`Gamez(path)` and `Anim(shared, chapterZrdr, missionZrdr, chapterAnim, missionAnim)`. The paths are
the whole key because they already carry data root, chapter and mission; collision, mute, the
emitter factory and the prewarm list all act after the decode, on objects this never holds. It
owns nothing disposable and nothing Godot — the texture and sound archives keep their
`ArchiveIntent` lifetimes, and scene nodes, runtimes and worlds are never stored.
**Everything handed back is shared and read-only by contract**, which is why `AnimProgram`'s three
collections are `IReadOnlyList`; `GameZ` has no such enforcement, so its one in-place writer
(`EffectCycles.Apply`) has to stay idempotent over a constant input, which it is. Callers are
serial by construction (`AnimArchive`'s SI-script pool is an unsynchronised lazy dictionary). An
instance lives as long as its holder: the harness keeps one per run, a game session keeps none.

## src/Mech3/EmptyStage.cs
The `--stage=empty` test stage: a flat collidable 20 km ground plane under a 100 m grid, standing in
for a chapter world so flight/ballistics runs boot in ~2 s with nothing else in the frame.

## src/Session/GameSession.cs
The per-launch orchestrator: `Launcher` constructs it from `(SessionSpec, LauncherContext)`, then
`StartSession` runs ordered build phases over one local `BuildState`. It owns the session clock,
world root, panes, mode runtimes and archive/resource lifetimes, while delegating aircraft assembly
and membership to `FlightRoster`, world construction to `WorldSession`, effects/crash staging to
`WorldEffectsFactory`, and scripted probes/captures to the Launcher-owned testing services.
`BuildFlightRigs` translates resolved session facts into the roster's four grouped contracts; all
initial humans and later command-line/mission/wave/generator AI enter through that aggregate.
`AllAircraft` combines the ordered rig controllers with the roster's AI view for simulation-facing
consumers. Exit frees the session subtree atomically, asks the roster to release non-node membership,
and disposes only the non-node resources this orchestrator owns.

## src/Utils/GameClock.cs
The session's simulation clock: `BeginFrame(wallDelta)` (first thing in `GameSession._Process`)
sets `Steps` + `Dt`; consumers read `FrameDt`, or loop `Steps` times on `Dt`. Modes: Realtime,
FixedAccum (interactive anim lab), FixedStep (scripted runs / `--det`); `Halted` is orthogonal.
`SimHeld` freezes the `PhysicsDt` consumers alone, which is how a cutscene's world hold reaches an
aircraft stepping itself on a realtime tick; `FrameDt` is untouched, so animation keeps playing.
On a realtime session `AnimRuntime` is a `PhysicsDt` consumer too (its `_PhysicsProcess`), so the
world's motions step on the same tick as the aircraft and the objective graph; its `_Process` takes
over only under `SimHeld`, a halt, or a parent-driven mode. Regression: the `anim-clock-realtime`
suite. Published as `GameClock.Current` (session-scoped, nulled on teardown; null = raw frame delta).

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

## src/Utils/ShaderTime.cs
The GPU's view of the clock: the `csky_time` global shader uniform (seconds), registered once in
`Launcher._Ready` and written once per rendered frame from `GameClock.Time`. Every animated
shader this project generates reads it instead of Godot's `TIME`.

## src/Utils/StartupProfile.cs
The always-on startup timing report: one `[perf] startup mode=… <subject> total=… boot=… <phases…>
rest=… first_frame=…` line per session build. `Mark()`/`Record(phase, mark)` are ambient statics over
`Current`, so the shared build code (`WorldSession`, which the test harness also drives) records blind.
Reading pitfalls for `boot`/`rest`: verification.md PERF-16/17. `Phases` is a read-only view of the
same accumulator for a caller that wants to aggregate the recorded spans without emitting the line
(`TestContext.BuildWorld` installs its own private instance as `Current` for exactly this — never the
one a real session would install, so a suite's phases and a session's `[perf] startup` line can never
mix) — see `src/Testing/PhaseAttribution.cs` below.

## src/Utils/HitchMonitor.cs
The always-on frame-hitch detector, ticked from `Launcher._Process` in every
mode: a frame trips when `frame_ms > max(medianMultiple × rolling_median, floorMs)`, and a
`HitchRecord` is assembled describing it: unaveraged script/render-CPU/GPU/physics, draws/prims/
nodes/mem as absolutes AND as deltas against the frame before, `GC.CollectionCount` per generation
plus allocated bytes, a ring buffer of the preceding frames ending with the hitching one, and
the frame's named work from `PerfSample` plus the remainder no scope claimed.
It only detects: nothing is logged from here, so a clean run is silent (B6 owns the sidecar).
Godot-free by construction (the caller samples the engine counters into a `FrameCounters` and hands
them in), so the trigger, both wraparounds and the grace window are unit-tested off-engine in
`CSVM.Tests/HitchMonitorTests.cs`; `PerfSample` is the one thing read ambiently rather than handed
in, and it is engine-free too. Five `hitchMonitor.*` config keys over `const` defaults, read in
the constructor (which is what registers them for `--dump-config`). `FrameCount` exposes the same
counter `HitchRecord.Frame` reports, one call early, so `--hitch-inject=` can fire on a stated
ordinal in this monitor's own frame space rather than the sim frame. `RingFrames`/`CopyRing` expose
the ring buffer itself, live — every `Tick`, not just on a trigger like `Last.Ring` — for
`PerfHud`'s Full-tier frame-time strip; `CopyRing` returns the MOST RECENT entries when handed a
shorter destination than the ring holds, oldest of those first. TUNE defaults, vsync interaction,
and the build-time-preset blind spot: verification.md PERF-12/PERF-13/PERF-14.

## src/Utils/HitchSidecar.cs
`HitchMonitor`'s write path: a tripped `HitchRecord` is copied — never
referenced, since `Last` is overwritten on the next trip — into a small preallocated queue (default depth 16,
`hitchSidecar.queueDepth`, drained after a default 2 s, `hitchSidecar.flushSeconds` — both TUNE), then
drained a few seconds later to one `[perf] hitch …` line (`ReportPerf`'s own flat key=value grammar,
ms terms as-is, byte counts as MB) plus one JSON line in `.scratch/logs/<mode>-<stamp>.hitches.jsonl`,
sharing the main log's stem. Both carry C8's attribution at the end: `samples=site:callsxms,…`
(`none` when nothing declared) with `attributed_ms`/`unattributed_ms`/`sample_violations` beside it,
and a `"samples":[{"site","ms","calls"}]` array in the JSON. All-numeric record apart from those
site names — compile-time `[a-z_]` constants from a closed enum — so the JSON is hand-written
(no library) via
`string.Create(CultureInfo.InvariantCulture, …)`, never plain `$"..."` interpolation, which would
format under `CurrentCulture` instead. The file opens once for the process's whole life with `Log`'s
own recipe (UTF-8 WITH a BOM, `AutoFlush`) — a line reaches disk the instant it is written.

## src/Utils/PerfSample.cs
Ambient timed leaf scopes: `using (PerfSample.Scope(PerfSite.DebrisSpawn))`
adds its wall time to that site's total for the frame in progress, and any code path can do it
without knowing the monitor, the readout, or whether anything is listening — statics over a
preallocated per-site array, the same ambient shape `StartupProfile` uses and for the same reason
(a scope several call layers down cannot be handed an accumulator). `Launcher._Process` calls
`EndFrame()` at the instant it stamps the frame's wall cost, so the scopes and the `frame_ms` they
ran inside describe the same span, and `HitchMonitor.Fill` snapshots that closed frame into
`HitchRecord.Samples` — its one ambient read, taken there rather than by the caller so a record can
never carry a stale frame's attribution. `Reset()` on a build or teardown, beside `Rearm`. Sites are
a closed enum (`debris_spawn` · `part_detach` · `ai_spawn` · `effect_checkout` · `effect_pool_miss` ·
`material_create` · `resource_load` · `audio_load`); a site nothing called is ABSENT from the record
rather than reported as zero.
All eight sites are seeded: `AnimRuntime.RunDeathSequence` (debris_spawn),
`FlightController.Crash` (part_detach), `FlightRoster.SpawnAi` (ai_spawn),
`AnimRuntime.PlayEffectAt` (effect_checkout), `EmitterDirector.Assert`'s miss branch
(effect_pool_miss), `EmitterRenderer.Attach` (material_create), `TextureArchive.FindImage`
(resource_load), `WorldSounds.Spawn`/`Create`'s decode-on-miss (audio_load). Confirmed live on two
real (non-injected) scenarios — `--destroy=` and `--crash=5` — with a temporarily grace-bypassed
`HitchMonitor` writing genuine `.hitches.jsonl` records carrying real `samples` (both reverted).
Interpreting `sample_violations` on a dominant site: verification.md INSTR-17.

## src/Utils/Rng.cs
The session's randomness policy: one master seed and ten named subsystem generators derived from it
(`weapons`, `flightaudio`, `spawn`, `paint`, `anim`, `crash`, `effects`, `puffer`, `clouds`,
`precip`). `Reset(master, pinned)` runs once per session build, before anything draws;
`Stream(name)` is the shared generator, `SeedFor`/`IntSeedFor` the pure seed, `NewIntSeed`/
`NewSystemRandom` a per-instance stream off the subsystem's own. Unpinned, the master comes from
`TimeSeed()` so the shipped game keeps its variety; a scripted flag implies `--det` and pins it to 1
(`docs/cli.md`).

## src/Testing/Probes.cs
The assertion cores behind the `--dump-markers` / `--dump-weapons` / `--dump-loadout` /
`--dump-flight` / `--dump-mips` / `--damage-test` / `--effects-test` inspection reports. Each probe
does the work once and returns both halves: the report text the flag prints and writes, and a
structured verdict (counts, per-row booleans, failure strings) a `--run-tests` suite asserts on.
`FlightEnvelopeAll` is the whole-plant instrument `--dump-flight=all` and the parity ledger share, so
a dump diffed against an older one and the ledger published from it cannot disagree. A flight row's
`Target` is always decoded or a named product exception; a footage figure lives in the row's text as
a discarded annotation and gates nothing. Angular rows read `PhysicalBodyRates`, never the original's
stored quaternion half-angle state. Every scenario also carries `EnvelopeMargins`: its distance
from each bounding term (the G clamp, the C_L ceiling, the AOA window, the stall flag, the altitude
band, the dive cap) and which decoded branches it drove.
`Effects` owns the effects sweep — the puffer half and the template-MESH half (`BL-061`), the
latter counted only through `MeshCensus` (per-root per-tick PEAK + distance-to-play-point +
post-stop residual; a final-sample census misses meshes the data turns off inside the window,
INSTR-11). The `effect-template-mesh` suite counts through `MeshCensus` too, so the sweep's
verdicts and the suite's assertions cannot drift apart.

## src/Testing/EnvelopeMargins.cs
The reachability half of the flight-envelope report. `Sample` reads one completed `FlightModel`
step's public state and keeps, per scenario, the peak load-factor demand against the ±5/9 clamp, the
peak demand over the aerodynamic C_L ceiling, the deepest opposing-command limit, the closest
approach to the stall speed, the peak altitude against the 2000 m band boundary, the delivered
body-up G against the authored `lowGs`/`highGs` starts, and the peak speed against the dive cap;
`Take` formats that as the row's `margins:` line. Across the whole airframe it also records which of
`Branches` the scenarios reached. Nothing here feeds a force. ⚠ One instance per airframe and not
thread-safe: the probe owns it for one report. Which instrument drives each unreached branch is in
[`org/flightModel.md`](org/flightModel.md), "Parity ledger".

## src/Testing/TestHarness.cs
`--run-tests[=filter]`: the suite registry, `TestContext` (assert verbs, resolved data paths, a
scene-tree host, and `WithWorld` — the chapter-world builder over `WorldSession`; the
mission-override form `WithWorld(chapter, collision, mission, body)` builds a chapter at another
mission and never caches it, since the cache is keyed by chapter alone — `zeppelin-damage` wants
C1 at M04; a collidable build evicts every cached collidable world first, since one physics space
holds them all and a cached chapter's scenery would stand inside the new world's airspace, which is
how C1's scenery met C2/M03's racers), the
PASS/FAIL/SKIP table, `test-report.json` in `TestContext.ScratchDir`, and the process exit code.
`Select` is the pure selector over the flag's value — comma-separated terms, `suite:` exact, `tier:`
a `SuiteCatalog` tier, anything else a substring — returning registry order and reporting every term
that matched nothing, which `Run` refuses before any suite starts. `SuiteShards` handles the one
term that divides rather than selects; `Run` applies it AFTER the miss checks, so an empty shard is
a legitimate division and an empty selector is still a typo. A sharded run's `ScratchDir` moves
beside its `--log-file`, which is what keeps concurrent shards (and concurrent runs) off each
other's report and artifacts. `TestContext.
EmitterFactory` (mutable, default null) forwards straight into `WorldSession.Options.EmitterFactory`
for the next `WithWorld` build. `WithPrivateWorld` is what a suite installing one uses: never read
from the shared cache and never written to it, freed when the body returns, so no build option a
suite chose can ride into a later suite's world. `BuildWorld` opens its
archives through `SessionArchives.OpenFor(ArchiveIntent.Suite, …)`, then `using`s the returned
`Textures`/`Sounds` itself — `OpenFor` states the (both-false) lifetime flags, it does not own the
disposal. One `DecodeCache` per run rides both that call and `WorldSession.Options.Decode`, so the
58 builds of a full catalog decode 8 chapter gamez and 16 programs rather than 58 of each; every
`Gamez`/`Program` a suite reads is therefore shared and read-only. `TestWorld` also carries the
parsed `Gamez` past the build (not disposable, unlike the texture archive) so a suite can build
real geometry of its own from it — the effect-template stage `effect-template-mesh` needs.
Engine-error allowlisting and pass/fail policy: `ErrorAllowlist`, in code. Windowed-run rule:
verification.md LOG-8.

**B11's phase attribution** (`test-report.json` schema 2). `BuildWorld` installs a private
`StartupProfile` as `Current` for the span of one build (restored in a `finally`, so a thrown build
cannot leak it onto a later suite or a real session), so `WorldSession.Build`'s and
`SessionArchives.OpenFor`'s own `Mark`/`Record` calls — the same ones a real session's `[perf]
startup` line reads — land on it with no second instrumentation invented for the harness.
`PhaseAttribution.Categorize` buckets those phases into archive/decode, sound preparation and
runtime/world construction against the *outer* wall-clock stopwatch `BuildWorld` keeps of its own
(not the profile's internal clock), so the four figures it returns always sum to exactly what
`TestContext.WorldBuildSeconds` attributes to the suite — no second clock to drift against the
first. `TestContext.ResetForSuite` clears that accumulator, and the three build knobs
(`EmitterFactory`, `ExtraPrewarmSoundNames`, `CutsceneRoots`), once per suite in `Run`, the same
lifetime `Failures`/`Notes`/`Counts` already have; disposing a world a suite built (not the shared
cache's own end-of-run teardown, reported only as the run's `finalDisposalSeconds`) is timed the
same way. What is left of a suite's wall time once build and disposal are subtracted is `rest`:
manual simulation plus assertion work, floored at zero, with any stopwatch overrun reported
separately rather than folded silently into a healthy-looking zero. The console line stays one line
per suite (a no-world suite prints no phase suffix at all) plus one totals line after the loop;
`test-report.json` carries the same figures per suite and as run totals, plus the run's `selector`
and the loaded `CSVM.dll`'s own path/MD5 (`binary`, read from the fixed
`<repo>/CSVM/.godot/mono/temp/bin/Debug/CSVM.dll` `RunTests.ps1`'s own perf stage hashes as
`$PerfDll` — not `Assembly.GetExecutingAssembly().Location`, which Godot's Mono host returns empty),
so a report can be matched to the exact build and suite set that produced it.

## src/Testing/SuiteShards.cs
Godot-free and pure (`CSVM.Tests` proves it without the engine): the `shard:<index>/<count>` term
and the division behind it. `Parse` lifts that term out of a `--run-tests=` value and hands the rest
back as the selector, treating a malformed or out-of-range one as an error rather than as a full
run. `Plan` divides an already-selected list longest-unit-first onto the lightest shard, ties broken
on the item's own position, and returns each shard in the input's order, so one tree always divides
the same way. `SuiteWeights` is `analysis/engine-suite-weights.json`: per-suite measured seconds, a
default for a suite the file does not name (`Unweighted` reports those), and `Groups`, the sets a
shard may not split. A missing or unreadable file weighs every suite the same, because an even
division is still a correct one.

## src/Testing/PhaseAttribution.cs
Godot-free and pure (`CSVM.Tests` proves it without the engine): buckets a `StartupProfile`'s raw
phase names — `gamez`/`textures`/`anim`/`zrdr` as archive/decode, `sounds`/`prewarm` as sound
preparation, `world`/`clutter`/`bind` as runtime/world construction — into `Categorized`, with
whatever a build's own wall-clock time does not cover landing in `OtherMs` rather than vanishing.
`Rest`/`Overrun` do the suite-level arithmetic: `wall − build − disposal`, clamped at zero, with the
clamped shortfall reported by `Overrun` instead of a falsely healthy zero. Both `zrdr` phases (a
mission's `MissionSetup.Load` inside `WorldSession.Build`, and the sound-def/group load inside
`SessionArchives.OpenFor`) share one name by `StartupProfile`'s own same-name-accumulates rule; both
are decode, so the shared archive/decode bucket is correct either way.

## src/Testing/CountingEmitterFactory.cs
`IEmitterFactory` for a suite: `Create` always succeeds and hands back a `CountingEmitter` — no
`TextureArchive`, no `MultiMesh`, no Godot type anywhere in its own state, just `Started`/`Stopped`
counts and whether it is sustaining now. Reached by installing it on `TestContext.EmitterFactory`
before a `WithWorld` build (`WorldSession.cs`); `CountingEmitterFactory.Built` is the list a suite
reads to confirm the fake was actually reached rather than a real `Puffer` — the seam `BL-241`'s
own fix note asked for.

## src/Testing/RecordingEmitterRenderer.cs
`IEmitterRenderer` for a suite: it keeps the particles a `Puffer` hands it (`LastFrame`, `Shown`,
`MaxShown`, `MaxIndex`, `MaxFrame`, plus the `Capacity` the emitter sized) instead of drawing them,
so `puffer-modes` asserts on burst / distance-trail / sustain with no atlas, `TextureArchive` or
`MultiMesh` in the path. The mirror of `CountingEmitterFactory` one seam lower: that fake replaces
the whole emitter so `EmitterDirector`'s LIFETIME is assertable, this one replaces the draw so the
emitter's own MODES are. Neither covers the other's job.

## src/Testing/SuiteCatalog.cs
The ordered registry of the in-engine assertion suites. Scenario bodies are grouped by domain in
the `*Suites.cs` modules; `Names` is the registry-order test surface and the count's one home.
Registration order is presentation only: no suite depends on running after another, which is what
lets any subset of this registry run in a process of its own. `QuickTier` is the checked-in
membership of `--run-tests=tier:quick`, resolved through `Tier(name)`, and holds one representative
per failure surface rather than the cheapest rows. The suites cover plane/loadout bindings (stock and, since M3 B4,
the full-rig `Loadout.ForRig`), live weapon fire, the carried turret gunners (`carried-turrets`:
build from ai.zrd + the thirdp mount, arc-centre rest pose, track/fire/hit under the host's
shooter id, bored-window fire suppression with live tracking, the nearer-end-stop park, YAW [0,0]
as unrestricted, a crashed host going quiet), the air-to-air hit chain (`air-to-air`: two real
flight rigs on manual sim steps — body strike, struck-shape→part mapping, armor-first data-value
damage, the whole-vehicle kill rule (a dead critical nose alone does NOT crash — the retired
divergence's own pin — and exhausting the fourth zone does), crashed-plane immunity, the
zero-self-hits negative case, which
must stay non-optional, `Downed`-into-`VersusMatch` attribution: the weapon kill scores
exactly the shooter, killer-less and unowned-round deaths score nobody, and the VS respawn loop:
`AutoRespawnAfter` 3 s respawns at that mark in sim frames, respawn reports nothing, null waits
for R),
the AI actor seam (`ai-actor`: an `AiPilot`-driven plane spawned into an already-stepped sim —
present, flying its orders, retargetable mid-flight, damageable and killable with the kill
attributed),
the far-field plant's session plumbing (`ai-far-field-plant`: two AI rigs at 100 m and 1200 m from
a human, so the branch is watched selecting in both directions: the range horizontal, the NEAREST
of several humans deciding it, an unbound seam staying near-field, and the far rig holding
throttle × fd_speed + 5 m/s where the near rig on identical orders does not), the AI gunnery
(`ai-gunnery`: held rigs firing through the real fire-control path — nearest-hostile
acquisition as mutable state, the quick-draw and ±11° cone gates, dead-eye skill 1 vs 9 hit
rates on a fixed seed, the kill under the AI's shooter id, and the IsHumanPiloted assist
exclusion A/B'd on one rig),
the inert aircraft state (`inert-aircraft`: four REAL instruments — a
physics raycast, `CollectAircraft` into an `AimAssist.Scan`, a round fired through the pool, and
`SimStep` — run over a live control, an aircraft built inert, and that same aircraft after
`Activate`, so every observation is watched flipping in both directions rather than only being
absent; plus the roster check that an inert plane is listed as a not-live candidate),
the Instant Action zeppelin run (`instant-action-zeppelin`: the
`zeppelin_type` selection with its cargo fallback, a real generator on the wave-credit budget
launching nothing uncredited and exactly one wave's members once credited — from the same bay drop
point the plane arm uses, never a fresh spawn — the parked-counts-as-present trigger, and
`ZeppelinRuntime.Hold`; plus, against C1/IA1's own built world, the objective starting hidden with
every one of its gasbag's collision shapes off and the decoded activation bringing both back),
the Instant Action mission end (`instant-action-end`: one mission type at
a time, each driven to its end through the SAME signal `GameSession` subscribes to — a spawned
ace's own `Downed`, `InstantActionWaves` stepped over real aircraft, `StuntRace`'s all-finished
path over C1/IA1's authored zones, and C1/M04's piratezep really destroyed through the F18 damage
path — every win check paired with a SECOND runtime of another mission type on the same signal that
must stay Running, plus the lives ledger's two ends on one real `FlightController`: with a life
left the armed 3 s crash cam respawns it, with `Spectating` set it is still a wreck 10 s later.
Its M04 world is mission-overridden and therefore never cached, so the gasbag kills cannot reach
another suite),
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
`collision:true` immediately after forces a real rebuild for everyone downstream. Per-suite traps
(one-frame physics limits, the shared-world read-only rule, golden-count provenance, the
loadout-bind split decision) live as comments on the suites themselves, in code.
`fbfx-flash` reuses `effect-template-mesh`'s `WithEffectStage` host to play `he_ground_effect` at the
camera (so its own `PLAYER_RANGE` gate passes) with the runtime's `ScreenFlash` sink recording:
it asserts the six wash steps' authored run times AND the gaps between their fires, since the
per-step run time alone is reported correctly even by a handler that returns 0 as its duration and
fires all six in one instant. Shown able to fail exactly that way. The chain's total gets one step
of headroom per gap — the authored run times are exact multiples of the step but not of binary
float, and three of the five gaps land one step late. It then asserts B12's routing on both sides of
the sink: every step reports the burst's own point and the def's authored `10000` m² gate, and a
real two-pane `ScreenFlash` over two `Camera3D` nodes 120 m apart paints one pane, the other pane, or
both, purely by where the burst is. The able-to-fail control is the same overlay with no `ViewerSet`
bound, which paints both — the pre-B12 behaviour; disabling the routing fails three of the checks.
Its blend-channel half puts both cameras at ONE point, so the ramp's proximity gate cannot tell the
panes apart and any difference is the victim routing alone: `PlayBlend(1, …)` stepped through its
attack paints pane 2 red and leaves pane 1 clear, an HE ramp then reaching both paints pane 1 exactly
as before the channel existed and pane 2 the wash composited over it, the wash is gone at its
duration, and an AI's player index addresses no pane.
`ordnance-burst-timeline` proves the ordnance burst timeline: it plays `he_ground_effect`,
`flash_effect` and `sonic_ground_effect` on its own miniature world-effects stage and matches the
WHOLE recorded `OnEventDispatched` log of each — every sequence, every event, in its sequence's
order, at its authored instant — against a table read off the def JSON by hand. Order is asserted
by CONSUMPTION (a row is claimed by the first lane whose next unconsumed step it matches on
sequence/index/kind/name, so an early or duplicated row matches nothing and is reported stray),
which is what a membership check cannot do and what every Wave B item needs to be measured at all:
`large_fireball`'s parked `stop_p1trail` must dispatch nothing (B12 — the shown-able-to-fail case),
`sonic_light_seq` must run exactly twice, the second pass restarting at 1.2 s, and the
`START_TIME ANIMATION`/`SEQUENCE` gates must land on 1.2/1.5 s. It drives at **1/240 s**, four
times finer than `SequenceRunner.AnimFrame`, because the authored gaps go down to 0.01 s and none
of the three defs carries a `LOOP` for the AnimFrame floor to matter to. Two traps are closed by
construction rather than by assertion order alone: the staged template roots are DERIVED
(`EffectCatalogue.StageRootsFor` against the chapter gamez, which throws on an anchor that resolves
nowhere) instead of hand-listed, and the TTL is 32 s — inheriting `--effects-test`'s 0.3 s would
truncate the 1.2 s wash while everything else still read green. The full log of all three lands in
`.scratch/ordnance-burst-timeline.txt`.
`effect-pool-reset` proves the checkout re-reset (`AnimRuntime.ResetCheckedOutCopies`): the same
derived sonic stage built over FOUR pool slots, `sonic_ground_effect` played five times to completion
(so `PoolRecycles` stays 0 and the fifth play lands on the first's copy), and every ring mesh under
slot 0's `sonic_ring1..5` read three frames into play 1 and play 5: visible-in-tree, scale and the
per-instance opacity must agree, and both plays must be drawing at least one ring. Without the
re-reset the fifth play's rings read INACTIVE at opacity 0, which is the sortie-long dead-burst
symptom this suite exists to hold shut.
`airframe-hull-coverage` builds all eleven player airframes and measures `PlaneCollider.Layout`
against each one's own triangles: every hull inside the box it replaces and above the thickness
floor, the fuselage leading the part order with only the four names `PlaneDamage.MapStruckPart`
knows, and no more than 0.5 % of the silhouette's triangle area outside every hull; its artifact
lists the per-part box and hull volumes, which is the overhang the sweep no longer bridges.

## src/Testing/*Suites.cs
Seventeen domain modules hold the in-engine scenario bodies, each named for the whole of what it
files: `PufferSuites` (the emitter model's modes, wind, fades and fire column), `CombatSuites`
(loadouts, live fire, aim assist and the hit chain), `OrdnanceSuites` (a round's flight, guidance
and ends), `InstantActionSuites` (the mission runtime from spawn to wrap-up), `AiSuites` (how a
computer-controlled combatant behaves: pilots, mounted gunners, combat voice, and the inert state
they wait in), `TargetingSuites` (the `TargetRef` abstraction, candidate pool, sticky selection,
input decoding and marker HUD), `TargetingCandidateSuites` (D36's widened AI acquisition,
`BL-363`: the team gate over a registered structure and the win routed to `GroundTarget`, never
`Target`), `WingmanSuites` (D34's netless `mode wingman` station-keeping, as geometry and flown
against a scripted leader), `CampaignSuites` (profile persistence, the objectives runtime and the
mission-end flow), `MusicSuites` (the state-driven score), `ZeppelinSuites` (motion, fighter
launch, multi-zone damage,
broadsides), `DamageSuites` (spending armor and health, and the injure staging those ledgers
fire), `DestroyChoreographySuites` (the choreography a death dispatches: destroy defs, wreck
flights, crash rigs, callbacks and stops), `AnimationAndEffectsSuites` (anim launches, effect
templates, washes and burst timelines), `WorldAndToolSuites` (the built world's data gates
and censuses, lighting and viewers, and the lab surfaces), and `WorldFidelitySuites` (the
mid-mission world behaviours the shipped data drives: the area-selected node toggle over C3's three
story rectangles, the scripted-path follower over C1's own takeoff path, the mission script's and
the generator's hangar doors over C1/M04, and the `FOG_STATE` event over its intro), plus
`AlphaCutoutRaySuites` (the BL-477 census: what actually stops a weapon ray short of C3/M01's cargo
zeppelin's slung tanks, as first-collider node names over a sphere of aspects) and
`AirframeColliderSuites` (the collision hulls measured against the mesh they came from) and
`CampaignRacerSuites` (CM13's six racers spawned from C2/M03's roster into its collidable world,
flying `dzpath1` and `dzpath2` on rails end to end with the mission's opening stepped through the
director, so the propane tanks hung in `dzpath1`'s gate are blown before anyone reaches them, and
nobody rams the `dbase` arch). They depend on
`TestHarness` through
`TestContext`; shared fixtures are separate focused modules, not an all-purpose suite helper.

## src/Testing/SuiteConstants.cs
The shared golden inputs used by more than one scenario module: airframe and weapon counts, puffer
timing, the destructible census, and texture samples.

## src/Testing/BurstTimeline.cs
The three value types describing an authored ordnance-burst timeline and its observed dispatches.

## src/Testing/SuiteViewers.cs
Builds a test pane camera at a supplied world position for suites that exercise `ViewerSet`.

## src/Testing/EffectStageSuiteHelper.cs
Builds and frees a production-shaped, pooled effect-template stage for mesh-visibility suites.
## src/Testing/GoldenShot.cs
The engine half of the golden-image tripwire: `PixelHash(Image)` (md5, lower-case hex) and
`Adapter()` (`"<gpu> / <api>"`). Called at the `--screenshot` save site, which prints
`[core] shot pixmd5=… size=… gpu=…` on every capture; `RunTests.ps1`'s `goldens` stage parses that
line and compares against `analysis/goldens/manifest.json`.

## src/Testing/ProbeRunner.cs
The `--dump-markers`/`--dump-weapons`/`--dump-flight`/`--dump-loadout`/`--dump-mips`/`--run-tests`/
`--effects-test`/`--damage-test`/`--destroy=` probe wrappers,
constructed once in `Launcher._Ready` after the base paths settle — the Launcher dispatches
the `--dump-*`/`--run-tests` early quits itself and hands the runner to each session node.
Each method reads a `SessionSpec` passed **per call**, not stored — a menu launch can replace the
caller's spec between calls, so a cached one would silently answer with a stale launch's flags.
`TriggerDestroy` (`--destroy=`) and `ForceObjective` (`--debug-objective=`) are the two scripted
world forces that live here rather than in a session: `ForceObjective` wakes one campaign objective
and drives the nodes its `INACTIVEn` conditions name inactive, so the graph completes it off its
own conditions on the next step instead of a mark being faked into the display. The objectives
suite drives its completions through the same `DriveInactive`.

## src/Testing/CaptureDirector.cs
The `--screenshot=`/`--shots=`/`--frames=` state machine plus F11/F12's placement print and ad-hoc
save, constructed once in `Launcher._Ready` from the launch spec
(process-scoped, never re-armed by a menu relaunch); `Tick()` runs from the Launcher's `_Process`
, which is what keeps `--menu --screenshot` capturing the launchscreen with no session node
alive. No back-reference to the host node — `Tick`/`PrintPlacement` take the
camera/orbit/rigs/clock/plane/menu-visible they need as parameters.
`SaveScreenshot` is the ad-hoc save every screen shares, and `ShotDir()` is the single folder they
all write into (`Screenshots/` at the repo root, git-ignored). It returns the file it wrote, which is
how a caller or a suite says where a shot landed rather than re-deriving the path.

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
`SessionSpec.FromMenu(_cli, …)`, never from the outgoing spec. `ExitSession` is the boards' Exit
item, handed down through `LauncherContext`: back to the launchscreen when the process launched
into it, out of the game otherwise. The routing Esc used to do — Esc now opens the pause board
instead, so leaving a flight is reachable from a pad, and this one rule lives here rather than
being restated per board.
`StartCampaignFromMenu` is the cabin's own launch, deriving its spec via
`SessionSpec.FromCampaign(_cli, ...)` from the profile and story position the campaign flow
settled. It names no chapter and no mission: `CampaignDirector.ResolveSpec` reads those out of
`cm_sequence` in the session's constructor, so one place resolves a story position whether it came
from a cabin or a `--campaign=` command line. Its return leg is `ReturnToCabin`, handed down
through `LauncherContext` and non-null only in a menu-driven process: a campaign mission's end
queues the profile name, and the next `_Process` frees the session and reopens the launchscreen on
that profile's cabin. Queued rather than acted on directly, because the mission ends inside the
session's own physics step, which is no place to free it.
The music channel is built here too, once per process and after every early-quit probe, over a
`SoundArchive` of its own rather than the build-scoped `SessionArchives.Sounds`: one channel has to
outlive a mission launch, or the cabin track would restart every time the player left a board. It
is entered on every launchscreen show, stopped in `BeginLaunch` (the one place a session leaves the
boards), and ticked on wall time from `_Process` so a paused or stepped session cannot stall a fade
halfway. An install with no readable sound archive or sound definitions leaves it null, which is
silence rather than a refusal to launch.
`RestartSession` is its sibling for the boards' Restart on an Instant Action mission: `QueueFree`
this session, step the sortie seed exactly as flying again from the menu does (so an unpinned
restart draws a new mission and a pinned one repeats), and build a fresh session from the same
spec. A mission's opposition lives in the world, so putting it back means rebuilding the world.
Both interactive paths in — the launchscreen's Fly and that Restart — go through `BeginLaunch`,
which shows the `LoadBoard` and owes the build to `RunOwedLaunch` at the tail of the NEXT
`_Process`: a build is one synchronous block, so the load screen cannot be drawn during it, and
the outgoing session (freed at the end of the requesting frame) is gone before the new one builds,
which is what stops its exit-tree duties (the published clock, the world lights, the camera
restore) landing on top of the new session. The load screen is freed in the same tick the build
returns, before anything renders, so it can never draw over the world's first frame or a
`--screenshot` capture. ⚠ The CLI launch in `_Ready` deliberately does NOT come through here: it
stays inline, so no scripted, golden or perf run gains a frame it did not have before.
`ReportPerf`'s window line carries `max_ms`/`p95_ms` beside its means:
a preallocated `_perfFrameMs` ring holds each frame's unaveraged wall cost, sorted into scratch
at window close. No `p99_ms` — at `PerfWindowFrames` = 60 it would equal `max_ms` by construction.
One `ReadFrameCounters()` per frame samples the eight engine counters once and feeds both
instruments: `HitchMonitor.Tick` wants them unaveraged, `ReportPerf` sums
them, and the two `TIME_*` monitors are converted from seconds to ms at that single read. The
monitor is constructed alongside the other process-scoped services (ahead of every probe's early
quit and of `--dump-config`, which is what registers its five keys) and `Rearm`ed by
`LaunchSession`/`ReturnToMenu`, since a build or a teardown legitimately stalls the loop.
`--hitch-inject=` fires right before the QPC stamp, on the `_Process` call
where `HitchMonitor.FrameCount + 1` matches the flag's frame — so the injected stall counts as that
call's own frame cost instead of the next one's.
`PerfSample.EndFrame()` is called on the same line as that stamp, so a frame's scopes and its
wall cost cover the same span — the session node processes at priority -1000, one notch ahead of
this one, so the work it declared is already in — and `PerfSample.Reset()` sits beside every
`Rearm`, since a build's own loads belong to no frame.
`_hitchSidecar` is built one step later than the monitor, right after `Log.Open` (its path
derives from `Log.SinkPath`): a trip queues into it from `_Process`, and `LaunchSession`/
`ReturnToMenu`/`_ExitTree` all flush it before `HitchMonitor.Rearm` — a build, a teardown and an
ordinary quit all legitimately stall or end the loop, and none of them should wait out the sidecar's
own flush interval to write down what it already has queued.
Measured render time is enabled once in `_Ready` (`ViewportSetMeasureRenderTime`) rather than per
frame from `ReportPerf`, because the hitch record needs the CPU/GPU split on every run, not only a
`--perf` one.
Vsync resolves at the same `_Ready` site as the shader clock / `--perf` tick: `display.vsync`
config key (default true) or `--no-vsync`, the flag always beating the key.
The config read is unconditional even when the flag already decided, so the key still registers
into `--dump-config` on a `--no-vsync` run — the same reason `ApplyMasterVolume` reads
`audio.volume` unconditionally. The resolved state logs either way (`vsync on` / `vsync off
source=…`), so a session's log always says which mode it ran in.

## src/Session/LiveryResolver.cs
Resolves which livery each player flies: the shipped paint catalog (`PaintCatalog`, lazy + cached), the per-pattern
region-mask library (`Patterns`, lazy + cached), `PatternsForPlane`, and the per-player
`SchemeFor` pick that reads a `SessionSpec`'s `--paint=`/`--paint-color=`/`--paint-decal=`
overrides. Constructed once per session build (`_liveryResolver` in `GameSession.StartSession`,
never across a menu rebuild — a relaunch gets a fresh instance over the fresh `_spec`).
With no `--paint=`, every aircraft wears `LiveryResolver.DefaultPattern` (`player_fortune`, the
Fortune Hunters livery the original's stock planes wear, and the only pattern covering all eleven
airframes): flight rigs, AI spawns and the static `--plane`/`--damage`/`--viewer` views alike. That
default is a straight catalog lookup and consumes no RNG draw, so pinned liveries are unmoved by it;
`--paint=none` is the only way to the bare shipped skins, and an absent `player_fortune` catalog
entry (no vehicle.json) falls back to them with a one-line note. The Instant Action enemies are the
exception (`SchemeFor`'s `useDefaultPattern: false`) — see `InstantActionRuntime.cs`'s entry for why.

## src/Session/SpawnPicker.cs
Resolves each player's flight spawn:
`ChooseSpawnBase` (the shared `--spawn=`-or-random list index), `ChooseSpawn` (a player's
position/look-at from that list, objectives.json `PLAYER_INIT`, or the `--spawn-at=` debug
override), and `LogSpawn`. Constructed once per session build (`_spawnPicker`, same lifetime as
`LiveryResolver`). Also the plain `IFlightStarts`: `ChooseStarts` just loops its own `ChooseSpawn`,
which is the placement every session flies except a splitscreen race. `RaceGrid` delegates to
`ChooseSpawn` for its anchor, and the weapon lab and freecam spectator call it directly, so this
type stays the single owner of spawn resolution.

## src/Session/IFlightStarts.cs
Where every pilot in a session starts: `ChooseStarts(spawns, missionZrdrPath, spawnBase,
playerCount)` returns one `FlightStart` — the same `(pos, lookAt)` pair `FlightController.Setup`
already took — per player. Two implementations: `SpawnPicker` (the plain per-player walk of the
mission's spawn list) and `RaceGrid`. `HumanFlightAdapter` holds the interface and resolves the
field lazily on its first `Assemble`, so the resolve still happens where it always did.

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
lists them even on a launch that never builds a race. Three `CSVM.Tests` assertions exist solely
to catch a per-plane-lift regression, and Dogfight's own spacing (rejected as a splitscreen use of
this class) is `BL-301`'s call.

## src/Session/PlaneRoster.cs
Static, spec-free lookups over a `SessionSpec`'s plane roster: `PlaneFor(spec, index)`,
`PlaneDisplayName(stats)`, `Humanize(s)`. A plane's display name is the def's AUTHORED `title`
(`PlaneStats.AiTitle`, "Medusa Kestrel") where something has resolved it through the string table,
and the def-name derivation ("Bloodhawk") otherwise, which is what a player load and a bare rig get.
No session state — every call takes the `SessionSpec` explicitly rather than caching one, since
these are pure over their arguments.

## src/Session/SurfaceDefTable.cs
One of the original's per-surface anim-def vectors — `"player_crash_" + name`, `"ai_crash_" +
name` or `"touchdown_" + name` over every `SurfaceRegistry` slot — plus the cascade that
indexes it with a struck material's numeric surface id (`SceneBuilder.SurfaceIdMeta`). Faithful to
`FUN_0048b920`
`0x0048bac5`–`0x0048bb00`: a null struck material, a negative id, an id at/beyond the vector length,
or a slot naming a def the program does not define all resolve **slot 0**; an empty vector or an
empty slot 0 resolves the bare last-resort anim name; anything else is `vector[id]`. Built once per
bind from a caller-supplied "does this def exist" test, so `PlayableDefs` is what a runtime must
bind and `DefForSurfaceId` is what an impact asks. Engine-free and pure. The AI family is the same
cascade over the same fields (`FUN_00475820` copies its params-built vector onto the vehicle,
`FUN_00476250` swaps in the player vector only on the vehicle named `player`); its last resort is
the vehicle's own name (`FUN_00479240` at `0x0047b11b`), so `AiCrashDefTable` passes the plane name.
Full decode, including the touchdown family's one difference (its empty last-resort arm plays
nothing) and the weapon `IMPACT` table's own, non-sharing fallback over the same registry id space:
`analysis/surface-classification/FINDINGS.md`.

## src/Utils/EffectPools.cs
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

## src/Session/InstantActionDirector.cs
The engine-side sequencing of one Instant Action mission, behind `GameSession`'s one nullable
`_iaDirector` field. A plain sealed class, not a Node: `GameSession` owns the tick order and calls
the phases at its pinned points (its entry has the order), and every node built on the mission's
behalf parents under the handed `_worldRoot`, so the session's no-Teardown rule holds unchanged.
The decoded rules stay engine-free in `InstantActionRuntime` and `InstantActionWaves`; this class
is where they meet the engine, and it owns every "ia:" log line.
`TryCreate(spec)` is construction: `SessionSpec.IaDef` (the wizard's already-built def, H16)
first, else `--ia=<path>` through `InstantAction.LoadFromJson`; both producers converge on the one
`new InstantActionRuntime(def)` call (⚠ in the code — two similar calls is the failure it avoids),
and a load failure warns and returns null rather than aborting the launch.
`BuildActors(ActorBuildInputs)` is the contiguous actor phase: stable mission references plus the
two delegates `GameSession` keeps private behaviour behind, the roster-spawn lambda and
`RegisterAiVoice`: the
chapter's FIRST patrol net armed on every actor, the `dogfight_ace` ace (spawn draw
`ChooseAceSpawn`, authored livery/team/rating), D9's wingmen (`FlownWingmen` clamp,
`WingmanSlotFor` fan off P1's pose, `player_fortune` livery, explicit `attackRating: 5`, the
escort chain set AFTER spawn so wingmen 2/4 target the already-spawned 1/3), and E11's waves —
EVERY configured wave built inert at the world origin on all four modes (Decision 6: build inert,
then teleport-and-activate, folding "wave 1 spawns live" into the path every later wave takes),
each member's rating and accent drawn from `Rng.Stream(Rng.Ai)`, its livery the militia pattern or
its own shipped skins, never the Fortune Hunters default. Wave 1 starts here on every mode but
`zeppelin_run`.
F12's zeppelin run rewires three points without a second code path: `SwitchZeppelins` writes every
distinct authored zeppelin node `Visible = objective` (the decoded `gwNodeSetActive`, colliders
derive from it) with `ZeppelinRuntime.Hold` on the ones switched off, returning the switched nodes
for `GameSession`'s turret arm; `ArmZeppelinRun` hands the private `ReleaseWaveMember` launch hook
to the objective's generator (`UseInstantActionLaunches`) and only then starts wave 1, since
activating it means crediting a generator that did not exist at `BuildActors` time; `ActivateWave`
branches at the top, stamping the launch-wave group (the generator's decoded `+0x64`) and calling
`GrantWaveCapacity` instead of drawing a spawn point.
`Step(dt)` (⚠ called from BOTH drive paths) advances the mission clock, ticks
`InstantActionWaves.Step` with the current wave's alive count (a member still parked in the
zeppelin's bay COUNTS as present, as the decoded walk counts a still-deactivated enemy), activates
whatever wave it returns, and reports `WavesCleared` when the sequencer finishes.
`WireEndConditions(EndConditionInputs)` is G13+G14: each mode's own signal routed into the runtime
(the ace's `Downed`; `StuntMission.RunCompleted` into the zone-set check, re-checked when a pilot
goes out, never polled; both zeppelin signals filtered to the OBJECTIVE node, engines first; the
sequencer's exhausted counter from `Step`), a mission whose win signal cannot arrive disabled with
a WARN at build; every human seat on the lives ledger with the 3 s respawn delay and a `Downed`
handler that logs the lives left or hands the pane to the spectate path (the pilot's own pad
filter, so two downed splitscreen pilots move independently); and the whole-window wrap-up board,
its counters summed across every seat (`enemiesShotDown` filtered on `killer != null` — a bare
terrain crash never reaches the take-hit body the original counts in — Shot % through
`ProjectilePool.ScoredShooters`, zones read live at `MissionEnded` time, P1's stunt best recorded
under the mission's own score key).
`ForceDebugScoreboard()` is `--debug-scoreboard`'s single-fire force, attributed to P1:
`dogfight_ace`/`dogfight_squadron` through `DebugForceCrash`; `stunt_flying` needs nothing,
already forced by `HumanFlightAdapter`'s own `DebugCompleteStunt` wiring; `zeppelin_run` has no
force, its verification drove the mode through real damage.

## src/Session/InstantActionRuntime.cs
Owns one Instant Action mission's actor set: the loaded `InstantActionDef`, the ace's spawn draw
and rating, the wingmen's fan placement, each wave's per-member draws, the objective-zeppelin
selection, and the mission's end. Static, engine-free helpers `InstantActionDirector` calls:
`ChooseAceSpawn`, `RepresentativeRating`, `WingmanSlotFor`/`FlownWingmen`,
`RandomPilotStats`/`ResolveWaveAccentId`, `ZeppelinNodes`/`SelectedZeppelinNode`/`ZeppelinTypeIndex`,
`FormatElapsed`/`ShotPercent`. The end half (`Objective`, `ReportObjective`, `DisableObjective`,
`ZoneSetsFlown`, the lives ledger, `Outcome`/`MissionEnded`, `Elapsed`) holds no engine type and
calls no `GD.*`, the same construction rule `VersusMatch` follows —
`CSVM.Tests/InstantActionEndTests.cs` pins it off-engine, and `InstantActionDirector` owns every
log line about it. Format and decode: docs/formats/instant-action.md.

## src/Session/InstantActionWaves.cs
The decoded wave sequencer's own selection, trigger and geometry logic (`FUN_0045b9d0`): pure state
over `Start`/`Step` calls, in the shape of `GeneratorCycle` —
`CSVM.Tests\InstantActionWavesTests.cs` pins it off-engine. `InstantActionDirector.BuildActors`
builds every configured wave's members INERT at the world origin, tracked in the director's own
per-wave rosters, then calls `Start()` and activates whatever it returns; the director's `Step`
ticks `InstantActionWaves.Step` once per sim step, and its `ActivateWave` resolves the spawn draw
against every live human's CURRENT position before calling `FlightController.Activate` on each
member. Format and decode: docs/formats/instant-action.md.

## src/Session/ObjectiveScript.cs
One mission's `objectives.zrd`, parsed into the typed shape `ObjectiveGraph` runs. `Load` takes a
mission zrdr scope and yields an empty script for a mission with no file, which is what every
Instant Action and multiplayer stub amounts to. Two parser rules are the original's and are what
keep the shipped data honest: blocks are read `OBJECTIVE1`, `OBJECTIVE2`, … and **stop at the first
missing number**, and every directive is found by EXACT name over the block's flat alternating list,
so `WAKEUP_OBJECTIVE_WHEN_I_COMPLETE` and the truncated `SET_AI_` land in no field and stay dead
without anything special-casing them. The lookup stays at the top level rather than recursing into
nested lists as the original does: no shipped file exercises the recursion, so the answer is the
same on all 53 files and no data string can false-match a keyword. A `null` block parses to a
directive-free objective, which starts awake and completes as a no-op. The four target directives
and `SET_HELP_LABEL` read `ObjectiveTarget`s, not names: a string is a bare name and a nested list is
ONE `[parent, child, ...]` path, keyed as `parent/child` (`ObjectiveTarget.Key`) everywhere a
target is stored or compared. ⚠ Flattening the nesting into names is how C1/M04's
`[[piratezep, rock_zeppelin]]` lit the hull root and a ground `rock_zeppelin` as two markers.
`SoundGroupNames()` (D33) collects every sound-group name the script's directives can hand to
`PlaySoundGroup`, a vocabulary the mission's anim program never sees, so nothing else prewarms it;
a session hands this to `WorldSession.Options.ExtraPrewarmNames`.
Format and decode: docs/formats/objectives.md.

## src/Session/ObjectiveSites.cs
The flown campaign mission's objective sites, offered to each player's `TargetPool` as
objective-flagged candidates: the original carries an objective as a companion flag on the Enemy
cycle, so one site is drawn at a time and d-pad up steps between them. The set is `targets.zrd`'s
own `objective` entries minus those a completed objective's `REMOVE_OBJECTIVE_TARGET` names, plus
whatever `ADD_OBJECTIVE_TARGET` has added: `ObjectiveGraph.ObjectiveTargets` alone starts empty and
a mission that only ever REMOVES its sites would offer nothing. One `ObjectiveSite` instance lives
as long as the mission flags it, since the selection is held by source identity; its position and
labels are re-read every frame, which is what tracks a site under a moving node. `PointFor` prefers
the bare `TRAVELERS` point of the objective that edits a target over the world node of the same
name, because C3/M01's village node stands at the world origin. A site on a world node is marked
at `SiteAnchor`: the node's own position for a placed node, and for a group node standing at the
world origin that draws nothing itself the centre of its built meshes, a `door`-named leaf pair
winning over the whole (the stunt mode's aperture rule), because C2's `sghangar` is such a group
and its parts carry the coordinates. A site is keyed by
`ObjectiveTarget.Key`, and `ResolveTarget` walks a path one name at a time with `FindNodes` scoped
to the node before, so `piratezep/rock_zeppelin` is the hull's own child and a bare name is the
first global match; `targets.zrd` is looked up by the whole key first (a path-authored entry
keys `parent/child` there too) and by the path's last node as the fallback, the help label by the
whole key. The marker's verb, proper name and colour all come off that table through `Messages`
(`Zeppelin [Disable] -` over `Worker's Voyage` in red, `[Dock] -` over `Worker's Voyage Docking
Hook` in blue), and a site whose key finds no entry falls back to its node name, which is what a
table loaded from the wrong scope looks like. `GameSession` binds it through
`FlightRoster.SetTargetObjectives`. Pinned by `campaign-objective-markers`,
`campaign-objective-target-path`, `campaign-objective-labels` and `campaign-race-chain` (the
hangar anchor, and C2/M03's race chain of per-zone objectives with a racer-death DEDG each).

## src/Session/ObjectiveGraph.cs
The objectives runtime over a parsed script, pure state over `Step` calls in the shape of
`InstantActionWaves` — no Godot type, no logging, `CSVM.Tests/ObjectiveGraphTests.cs` pins it
off-engine and `CampaignDirector` owns every log line about it. Implemented as decoded, shipped
quirks included: the four states (dormant/awake/napping/retired); at most ONE completion per tick
from a scan index that advances every tick, completion or not; `TICK_DEPENDS_ON_OBJ` gating the
whole objective on its dependency being AWAKE, not merely alive; the wake executor's early return on
an already-awake target, which TRUNCATES the rest of the caller's wake list;
`NAP_OBJECTIVE_WHEN_I_COMPLETE` clearing the target's completed flag as the only re-run path;
condition families OR-ing together with a conditionless objective completing on its first eligible
tick; and `DANGER_ZONES_COMPLETED` counting only zones flagged while the objective was awake.
`NotifyDockingComplete` is the one ending that comes from outside the script: callback 13, raised by
the docking animation itself, which the original answers with the call its objectives runtime makes
when a primary completes and then the mission-end path, in the same breath and with NO wrap-up, so
the debrief opens on the frame the film raises the code. Where a mission also authors an
`ANIM_STATE ... EXECUTED` objective over the same definition (C3/M05's `OBJECTIVE19`), the code
always gets there first: it is raised by that definition's last sequence, so the definition is
still `RUNNING` when it lands and the objective's `INSTANTWIN` arrives to an ended mission and does
nothing. `Session/CutsceneController.cs` raises it.
The world seam is `IObjectiveWorld`: a method returning `null` means "this engine cannot answer",
which makes the family report FALSE and bumps `UnresolvedConditions` rather than guess — ⚠ reading
an empty world as "the group is wiped out" would win missions on the first tick.
The read model D33 consumes is `Rows` (one row per unique `IDENTITY` priority, ascending, the
priority the row key and the sort key), `ObjectiveTargets`/`OtherTargets`/`HelpLabels`, and the
`Woke`/`Completed`/`TargetsChanged`/`MissionEnded` events; wake and complete events carry the
`WAKEUP_SOUND_GROUP` / `COMPLETED_SOUND_GROUP` names, which is also how D37 sees the music groups.
`Transitioned` fires on every state change (woke, napped, completed, killed, slept, expired) with
the objective whose completion caused it, the mission time, the nap length, and whether the
objective is held by a `TICK_DEPENDS_ON_OBJ` dependency that is not awake; `CampaignDirector`
turns it into the sortie log's `objective N ...` lines. A held nap does not count down: that is the
decoded gate, not a defect, and `CSVM.Tests/ObjectiveGraphTests.cs` drives the shipped C1/M04 chain
(tower down inside the distress window, through the held nap to the squad wake and the docking)
to pin it.
`CompletedMask` is bit-per-row, so bit 0 is the lowest priority and therefore the primary objective
the profile's merge gates on (docs/formats/saved-games.md).
The fourth ending, the player's own death, is `NotifyPlayerLost` then `EndAfterPlayerLost`: the
first stops `Step` entirely, countdown and pending wrap-up included, and the second delivers the
outcome. ⚠ That outcome is the won flag alone, so a mission won before the death is still won, and
neither call plays a sound.

## src/Session/CampaignDirector.cs
The engine side of one campaign mission, behind `GameSession`'s one nullable `_campaign` field and
the sibling of `InstantActionDirector`: a plain sealed class that builds no node of its own.
`ResolveSpec` runs in `GameSession`'s CONSTRUCTOR, before any chapter-dependent path is derived: a
`--campaign=<profile>:<seq>` launch names a story position, so the sequence is read and
`SessionSpec.WithCampaignMission` points the rest of the build at an ordinary chapter/mission.
`TryCreate` loads the profile and the script on the same "a failure warns and flies without a
mission" contract `InstantActionDirector.TryCreate` has. `Attach(WorldInputs)` arms the graph once
every runtime a directive can touch is up, applies the chapter's persist log and hands the world's
`DangerZoneRibbons` to every roster pilot; `Step(dt)` is
called from BOTH of `GameSession`'s drive paths. Every graph transition is one
`[campaign] objective N woke|napped|completed|killed|slept|expired [by M] [for Ns] at Ts` line
through `Log.Info`, so the file sink carries the chain a sortie report is about. Mission end records the attempt through
`CampaignProgression`, merges `CampaignPersistLog.Capture` into the profile, saves it, writes any
aircraft award's build into `CustomPlaneStore` (`SaveAwardedBuilds`, which is where the cabin's
launch looks a plane's fit up by name), and raises
`ReturnToCabin` plus `MissionEnded` for the session layer; the cabin screen itself is C22's.
Which directives reach the engine today: `INACTIVEn` (node visibility, the decoded active bit),
`ANIM_STATE` (`AnimRuntime.AnimStateOf`), both forms of `TRAVELERS` (the node form against
`ListenerPosition`/a named node; the group form tallying the spawned roster's live, non-inert
members of the named group inside or outside the radius against `spec.Count`, the decoded
`FUN_00465b40` shape `docs/formats/objectives.md` already carried), `WAKEUP_TURRETS` /
`WAKEUP_ZEP_TURRETS` (`TurretEmplacementRuntime.SetActivatedUnder`), `WAKEUP_GENERATOR`
(`AiGeneratorRuntime.GrantWaveCapacity`), `DEDG` over the spawned roster plus the human rig when
its `FlightController.Group` is the counted group (a 967 capture swap stamps it; a crashed rig
drops out, an inert one does not, since a human rig is inert under a cutscene; a roster rig counts
unless `FlightController.Deactivated`, which is inert with no cutscene park behind it, so the
wing walk's 913 park keeps the captured bomber counted until 967 hides it), `WAKE_ANIM` (`AnimRuntime.PlayMissionTrigger`, so the
woken definition may stage library roots), both sound-group directives through
`MissionRadio`, falling through to `WorldSounds.PlayOneShot` for a cue the radio does not own,
`STOP_QUEUED_SOUNDS` through `MissionRadio.Cancel`, `START_TAXI` through the director's own
`ScriptedPathVehicles` registry (`Paths`), which it also steps beside the graph,
`COMPLETED_STOPPOINT` through `ZeppelinRuntime.SetStopPoint` (it arms or releases one stop point of
a named net, and the airship holds or leaves), and, over the spawned roster, `DEDG`
(`GroupLiveCount`: not-crashed members of the block group, a parked one counting as alive) and
`WAKEUP_ENEMIES`, one directive over two deactivated flags: an inert named aircraft re-activated at
its spawn pose, or a dormant `ZeppelinRuntime` record put into the world.
`SET_AI_NET` / `SET_AI_TEAM` / `SET_AI_ATTACK_RADIUS` share one lookup by roster block name
(`Commanded`) and write the follower, the team and the attack range over the spawned roster; their
zeppelin arm has no seam here, so an unmatched name is always reported.
`COMPLETED_ZEPCANNONS` writes each named zeppelin's broadside engage flag
(`ZeppelinRuntime.SetCannonsEngaged`), the one thing that lets a hatch open and a volley leave.
The rest (`WARP_VEHICLE`) is a NAMED no-op, logged once per kind. ⚠ Never turn one of those into an invented
behaviour: the missing consumer is the finding.
`BuildRoster(RosterInputs)` is the roster phase, called by `GameSession` right after
`InstantActionDirector.BuildActors` at the point where the human rigs exist: it plans the mission's
`aiv` blocks through `Session/CampaignRoster.cs`, spawns each through the handed delegate (an
authored `netids` becomes `AiPilot.Patrol` on the chapter's net with its trailer; a netless
`mode wingman` block gets `AiPilot.Escort` in a SECOND pass once every rig exists, its
`primary_target` resolving to the player rig or a block by name; never both), applies the merged
volumes and the `min_ai_active_dist` floor, the signature maneuvers, the rating biases and the
accent, builds a `deactivated` block inert, places a `taxiPath` block held on its path
(`PlaceOnPath`: re-pinned through `FlightController.PlaceHeld` each tick, `Activate`d at the
handoff speed), and logs one `campaign: roster '<name>'` line per block. A block whose
`primary_target` is not spawned holds its course; a leader that dies later is `AiPilot`'s own
fallback. `Roster` is the spawned map by block name; the player's block is skipped. A surface
vehicle (`mode ship`) plans as a hull and goes to `RosterInputs.SpawnSurface` instead of the
aircraft spawner (`PlaceSurface`): built by `Session/SurfaceVehicleRuntime.cs` at the block's spot,
put on its authored net, kept in `Vessels` by block name (never in `Roster`), and reported when
the stage has no such runtime. The hulls take the same directives as the aircraft where they
apply: `WAKEUP_ENEMIES` wakes one, `SET_AI_NET` and `SET_AI_TEAM` reach a roster hull or a
generator's launch (`CommandedVessel`, through the runtime), and `DEDG` and the group form of
`TRAVELERS` count a woken, undestroyed hull as a live member of its group.
`HoldForCutscene(bool)` is callback 20's objectives half: a held director advances no dormancy
timer or reminder fuse while a cutscene owns the session (`Session/CutsceneController.cs`).
The player's `Downed` (the lost ending) and `DamageApplied` (the music ping) hooks are subscribed
on the aircraft `WorldInputs.PlayerAircraft` answers and re-subscribed whenever it answers a
different one: an airframe swap rebuilds the rig, and a death in the new one has to end the
mission too. `BuildRoster` also stamps each spawned rig's `FlightController.Group` from its plan.
The player's own death is the graph's fourth ending, in the original's two stages: the aircraft's
`Downed` report closes the graph's gate, and the wreck no longer falling ends the mission.
`EndsOnPlayerDeath` (false under `--no-crash-loss`) is the only switch; `GameSession` is its writer.
A lost attempt is recorded like any other and commits nothing to the persist log, so a retry starts
from the chapter state the profile already held (`CampaignPersistLog.CommitsOn`).

## src/Session/CampaignRoster.cs
The engine-free half of the campaign roster spawner: `CampaignRosterPlan.Build` turns
`AiSkills.LoadRoster`'s blocks into one `RosterSpawnPlan` each, using `Mech3/VehicleDefs.cs` for
the def behind the block name, its `mode` and its player airframe, and the chapter's `AiNet`s for
the authored `netids` (a multi-entry list takes the handed `rand() % count` draw). The decoded fork
is here and nowhere else: `Escorts` is `mode wingman` AND no authored net, `Net` is any authored
net that the chapter carries, and a plan never has both (docs/org/aiPilot.md "A net demotes a
wingman"). `Volumes` is the net's set overlaid by the block's own; `ApplyVolumes` writes the radii
onto an `AiModeMachine` and floors activation at `min_ai_active_dist`. The profile's wingman
airframe replaces the block's own for the named block (`wingman_1`), taking the `w<plane>` def for
its stats; a def that is no `kind_of` variant of its airframe flies the plain base def, with the
block's own def still deciding the mode. A def with no airframe whose mode is `ship` plans as a
`Surface` hull (`PlaneNode` is then the def, the chapter's library-root model; no `AiDef`), and
`SpawnFor` refuses such a plan; any other airframe-less def is `Skipped`. `ResolveLeader` is the second-pass lookup (`player` = the
first human). The `handover` argument is the airframe-swap counterpart of the wingman override:
in the two missions that resolve `wingman_4`, that block flies the PLAYER's airframe and paint from
mission start, because the swap is about to hand it that aeroplane. An `enabled 0` block is
excluded from the initial roster and
`BuildGeneratorTemplate` resolves it separately when an enemy generator's `vehicle.params` names
its positional header label; `GeneratorTemplates` is the whole mission's map of those, keyed by
that label, and `GameSession` builds it on any run with the generators on rather than only a
campaign one, since the parameter blocks are mission data. `ResolveGeneratorLaunch` is the
four-way read of that map for one launch: the block the label names (`Template`), the same block
when it is a hull (`Surface`, C2/M01's `Eshipg31_params` naming `patrolboat_eg0`), the CLI
airframe when no label is authored, or `GeneratorLaunch.Empty` when the label names no block. Empty is the decoded
shape, not a gap: `FUN_00451bf0` then spawns the generator's `vehicle.type` (unauthored in every
shipped file), finds no def for the empty name and still reports a launch, so nothing is built and
the launch is counted. C1/M04's `eairg32` (`Eairg32_params` against a label table spelling
`Earig32_params`) is the shipped case; the data stays as shipped. `ApplyPlan` is the after-the-spawn half of a
plan (volumes under the floor, signature maneuvers, the gunner's rating biases and its assignment),
shared by the campaign placement and the generator launch so the two cannot drift; ⚠ it leaves an
escorting block's `primary_target` alone, because there it names a leader and not a target.
Pinned in `CSVM.Tests/CampaignRosterPlanTests.cs`; the placement half
is the `campaign-roster` suite, the generator half the `generator-roster-params` suite.

## src/Session/ScriptedPathVehicles.cs
One campaign mission's scripted-path vehicles: `Place` binds a spawned body to its authored
`ScriptedPath`, snapping it onto waypoint 0 facing down the first leg and freezing it there
(`FUN_004940d0`, the same placement the non-zeppelin generator launch uses), `Release` is what
`START_TAXI` calls, and `Step` drives each
follower and writes its pose onto the body, or hands it to the caller's `setPose` for a body whose
pose a simulation of its own owns (a held `FlightController`). A finished vehicle raises its handoff
callback with the speed the path left it at and leaves the registry, so nothing keeps overwriting
the flight model's pose. The law is `Flight/PathFollower.cs` and the route `Mech3/ScriptedPath.cs`.

## src/Session/SurfaceVehicleRuntime.cs
Builds and steps a mission's surface vehicles, the `mode ship` blocks (`patrolboat`, `t_truck`)
that have no player airframe: `Spawn(plan, position, forward, nodeName)` copies the chapter's
library-root model of the def (`SceneBuilder.BuildSubtree`, colliders and all, so a weapon hit and
a ram reach it through the world mask), parents it under the world root at the authored spot with
its height read off the water (a downward probe on the world mask carried past any other surface
it meets first, since a ship generator's launch point sits under the host's own deck; no water hit
keeps the authored height), and indexes it on the world runtime through
`AnimRuntime.IndexSpawnedCopy`, which is what lets the chapter's own `patrolboat` definitions
(the reader files `patrol_boat_destroy`, `ptboat_damage`, `ptboat_wake`) anchor on every copy and
register its destructible pool. A def with no library root in the chapter is logged and builds
nothing. Stepped from `_PhysicsProcess` on a realtime clock or `GameSession.DriveSimSteps`
after the generators, like them. `GameSession` builds one lazily (`EnsureSurfaceVehicles`) for
the roster phase and the generator block, only where a chapter world exists; `CampaignDirector`
reaches it through `RosterInputs.SpawnSurface` and `WorldInputs.SurfaceVehicles`. Observability:
one `surface: '<name>' … built at (…) water=…` line per hull. Pinned by `campaign-surface-vehicles`.

## src/Session/SurfaceVehicle.cs
One built hull: no pilot, no flight model, no `FlightController`. Its movement is
`Flight/PathFollower.cs`, the scripted-path law (docs/org/flightModel.md "The scripted-path
follower"), over an unbounded route (`SurfaceRoute`): the generator's take-off run first, then a
lazily extended walk of the net's edges from the node nearest the run's end (a random onward
edge, never straight back unless that is the only one), so the follower never reaches its final
leg, which is the aircraft climb-out and not a patrol. The follower steers in the plane; the
hull's height is pinned to the water it was placed on, whatever the net's nodes author, since a
net is a route and not a waterline. `Patrol(net)` is the roster assignment and the `SET_AI_NET`
arm (the route restarts from where the hull is), `Launch(run, net)` the generator's. A block's
`deactivated` builds it `Inert`: hidden, its pool `Dormant` (no target), its follower frozen;
`Wake()` (the `WAKEUP_ENEMIES` arm) shows it, arms the pool, releases the follower and plays the
def's `start_anims` (the wake puffers on `pt_emitter1/2`) through `AnimRuntime.PlayWithin`, which
a hull built active plays at once. Damage is the pool the chapter's definition registered on the
root (`HEALTH 20`; `Team` and `Owner` written from the block so the aim assist and `rating_biases`
see it): `Step` plays each `injure_anims` rung once as the pool's fraction falls through it, and
on `Destroyed` raises the event once, stops the follower and leaves the parts to the death
sequence (the sinking, the debris motions, the slick). ⚠ Nothing here writes a child's transform:
the death's `ObjectMotion`s own those through `MotionSet`'s channel rule, and the hull root is the
only node this class poses. The turret nodes the model carries (`healthy/turret/gun/firepoint`)
are built but not driven: no `ai.zrd` entry names a patrol boat, and its `weapons` gunnery is the
AI mode machine's (`BL-523`), not this class's.

## src/Session/CutsceneController.cs
The host a story mission's intro definition raises its `CALLBACK` codes to, and the session state
those codes describe. A `Node` only so it can tick LAST in the frame (`ProcessPriority` 1000): the
rig cameras take the pose that frame's animation advance put `camera1` in, so the bars, posed inside
that advance, never sit against a camera one frame behind them. Hosted: 20 world+objectives hold
(`GameSession`'s drive paths, the session clock's `SimHeld`, which is what stops a node stepping
itself on a realtime tick, and `CampaignDirector.HoldForCutscene` read it), 2 chrome off and the
view off the aircraft (`FlightController.CameraOwned`, which also stops the cockpit rules being
re-asserted), 11 the player out of flight (`Held` + `Inert` + engine audio paused, and the airframe posed on the
staged `player` marker through `FlightController.StageAt` while that state holds, asserted in that
same instant rather than on the next tick, because the definition raising the code goes on posing
the aircraft in the same dispatch), 913/914 park and
reveal the AI (`Inert` plus `Parked`, the hold flag the original sets instead of its dead byte, so
an objective walk still counts a parked aircraft; only what this controller parked comes back), 666/667 the camera-parameter gate
(tracked, not acted on — this engine applies that profile once per rig and never on a view change),
1/10 the handoff and the in-flight systems; 951 the re-placement, which reads the staged `player`
marker's world pose and moves the hand-back target through `FlightController.ResumeAt`, so a
mid-mission drop or hookup leaves the pilot where its own definition parked that node rather than
where it found them; 965/966/967 the mid-mission airframe swap, through the
`SwapAirframe` seam the session fills with `FlightRoster.RunSwap` (the three codes, their def/node
pairs and the hand-over decode are `Session/AirframeSwap.cs`); 14 and 123 are named gaps with one
log line each. `Host` takes the raising definition's ROOT node name beside its anim name and passes
it into the swap order, which is how 967 reaches the aircraft its capture animation belongs to;
whatever the swap hid then leaves the parked list, since 913 parks that aircraft before 967 hides it
and 914 would otherwise put it back.
A swap sets the cutscene flags 11 and 2 set between them, and clears nothing: the definition
ending is what gives the player flight back, now in the new airframe.
Which definition the episode belongs to is the original's trigger slot, not the raiser of the
first code: the slot is written with the started definition BEFORE it starts, and the episode ends
when that definition has ended AND no code-authoring definition in its call
closure (seeded at the start, plus whatever actually raised a code) is still running.
The slot is written on every path that starts a definition, not just the landings one:
`AnimRuntime.MissionTriggerOwner` is called from `PlayMissionTrigger` itself, so the approach rows,
the objective script's `WAKE_ANIM` and the ladder switch all book it, and `WorldSession` hands the
opening cutscene's own name to the same seam before the bind, since the intro starts inside the
start-list walk rather than through a trigger call. `Own` declines a definition whose call closure
authors no `CALLBACK`: an ordinary `WAKE_ANIM` goes through the same call, and a slot it claimed
would outrank the real raiser of the next episode while it was still running. C5/M02's ending is
the shipped objective-path case (`nypd_southward` raises nothing and calls `nypd_player`, which
raises 11, 2 and then 13); no shipped `WAKE_ANIM` target reaches more than one code-authoring
definition, so the multi-raiser shape exists on the landings path alone.
Code 13 is the mission-completion code, and the only ending a mission that finishes on a
zeppelin's hook has: nothing in the shipped objective data completes on a landing. It reaches
`ObjectiveGraph.NotifyDockingComplete` through the `MissionComplete` seam, which wins the mission on
the ordinary wrap-up. Twenty missions' `hooked_to_klondike` and two mission-ending drop definitions
raise it. Both halves
come from CM06's docking, whose row definition raises nothing itself, calls the hookup that raises
the first codes and ends with the aeroplane still hung, and calls the unhook last with a trailing
`WAIT_FOR_COMPLETION` that holds no runner open (`docs/org/sequences.md`), so the row ends 2.6 s
before the unhook raises 1 and 951 at its own end. The original's flags are written by the codes
alone, never by a definition ending. `FlightController.ResumeAt` places the aeroplane at once when
the handoff has already been raised (CM06 authors 1 before 951), and defers to the hand-back
otherwise. The
handoff raises the gameplay state the definition's own `RESET_STATE` asserts, because a CSVM
`RESET_STATE` dispatch deliberately raises no callbacks and `RESET_TIME` is undecoded; it then runs
the rest of that same block through `AnimRuntime.RunResetStateEvents` (callbacks still suppressed, so
the codes are recorded once), which is where a definition's authored calls and child detaches land —
CM07's hangar drop clears its objective node through a `CALL_ANIMATION` there and nowhere else. It
also retracts the bars, returns `camera1` to the runtime's world root (a definition composes itself by
reparenting it) and parks it at the origin, which other definitions pose against.
The staged `player` marker goes home the same way, and for the same reason: a definition that poses
it reparents it (CM07's train pickup leaves it under `caboose`, C1C/M01's docking under
`pzhookpoint`), and CSVM has a node to strand where the original has none, its `player` being the
flown vehicle itself and a pose written there a world pose. A marker left on another node's frame
makes the NEXT episode pose the flown aeroplane in that frame: CM07's hangar drop then rides the
moving train's coordinates instead of the hangar's, so the aeroplane is nowhere near the shot and
the 951 flies the pilot out kilometres from the doors. The marker is returned AFTER the restore
codes, since the 951 above reads the pose the ending definition left it in. ⚠ A suite that measures
the handed-back aeroplane against the marker has to remember the pose from the last playing frame,
not read the marker on the frame after: by then it is home at the origin.
A re-placement (951) the ending definition authors in that same `RESET_STATE` is raised at the
handoff too, ahead of the restore codes, since the reset walk suppresses callbacks: CM07's hangar
drop leaves the pilot on the lift in front of the open doors rather than back on the approach. The
staged archive props (`AircraftStage.Props`) are switched off again at the handoff, because the
reset's own `OBJECT_DELETE_CHILD` detaches one to the world root, where the original's walk no
longer reaches it but this scene still draws it.
It also owns when player 1's flown airframe reaches the runtime's node table
(`AircraftStage.StageFlown`), from `BindRigs` and again after a swap, which is what lets a hookup
definition resolve that aeroplane's own hook, wings and mount offset. `BindRigs` with no cutscene
playing also re-asserts the `player` marker ACTIVE: `player_setup`, on every mission's start list,
switches that node off in its sequence and back on in the `RESET_STATE` its `RESET_TIME` 0
schedules at its end, a schedule this runtime does not run, and a marker left off is exactly a
mid-mission drop that holds the pilot undrawn for its whole length (the flown vehicle's node is
active once gameplay starts). An intro still playing at that point keeps its own hidden state.
Skip is any key (not Escape) or pad button, and is offered only where the original offers it:
`Skippable` is the original's active-cutscene slot, armed by code 20 and cleared at the handoff, so
a definition that never holds the world is played out and the key press falls through to the rest of
the session. Where a skip is armed, it force-stops the definition and runs the same restore, which
loses nothing: an intro's remaining codes are the chrome ones and its `RESET_STATE` authors exactly
the four `RestoreCodes` raises. ⚠ The gate is not a convenience: every definition that swaps the
player's airframe or re-places the pilot is one the original arms no skip on, so a skip can never
drop one.
⚠ `IntroAnims` is the scope, and it is a NAME test: Instant Action's `player_setup` authors the same
nine codes, so a code test would give every mission a letterbox and a suspended world. The list is
the three definitions a story mission's start list plays as its opening movie: the two intros and
C3/M03's `cgzep_camera`, which no start list names (its start anim `calldestroy_the_cargozep`
calls it first and the zeppelin's destruction half a second later, so the destruction plays under
the camera from that one call and the host re-issues nothing). Decode:
`docs/formats/anim-definitions/cutscenes.md`.

## src/Session/GeneratorCycle.cs
The decoded egen launch timing law for ONE generator (M4 B6 + F20), pure over `Step` calls (no
clock, no randomness, no nodes), so `CSVM.Tests/GeneratorCycleTests.cs` pins it off-engine. The
law: the timer always advances; blocking HOLDS (never cancels); `ind_period` gaps individuals
inside a wave and `ind_period + wave_period` gaps waves (they compose). `DoorOpen` runs the
decoded hardcoded door timings inside the same `Step`: open 4 s before a due spawn, minimum 4 s
open (measured on the same since-spawn timer), close early only when the next spawn is over 8 s
away; while blocked ONLY the door closes, and a disabled (host-dead) generator's door keeps its
last state (the decoded loop early-outs before any door rule). `UseWaveCredits()`/`GrantCapacity`
are F12's arm: an Instant Action `zeppelin_run` runs the decoded capacity rule regardless of the
authored `capacity`, from zero remaining, topped up per wave. Format and decode, including the
capacity-stand-in puzzle: `docs/formats/mission-entities/enemy-generators.md`.

## src/Session/NetTrailerTargets.cs
Resolves a patrol net's TRAILER name to a live position supplier (`BL-377`), the session half of
"an anchored net rides its target", so `Flight/AiNetFollower` can do the arithmetic knowing nothing
about players or world nodes. `For(net)` returns a `Func<Vector3?>` only for the anchored-and-named
shape (`[nodeIndex, "name"]`, 76 nets); the other three shipped shapes get null, which means "fly
the authored coordinates". `player` is the player rig; anything else is a world node through the
same `WorldRuntime.FindNodes` lookup `ZeppelinRuntime` uses. `OffsetOf(net)` is the overlay's read
of the same offset. Every follower the session builds shares one instance (`GameSession._netTrailers`).
Pinned by `NetTrailerTargetsTests` + the `ai-net-follow` suite.

## src/Session/AiGeneratorRuntime.cs
Runs a mission's egen generators (M4 B6 + F20, behind `--generators[=plane]`): one
`GeneratorCycle` per surviving `EnemyGeneratorDef`, host altitude read live off the resolved host
node, spawns through the handed roster callback at the origin node's LIVE position (it rides
F17's moving zeppelin) in the authored `rotation` drop attitude, each pilot patrolling the cyclic
net pick through `AiNetFollower` (`SpawnedNet`). A surface host instead launches off its own
`<base>_aip<n>` take-off path, whose absence is a third load drop, and then FLIES that path: the
launched aircraft is held (`FlightController.Held`, no flight integration and no collision, so a
launch standing on its own deck is never a ram) and driven by a `PathFollower` over the path
nodes' live positions (`LiveWaypoints`, so a run off a still-driving hull stays on it), nose along
the follower's own motion (the leg's climb, then the final leg's climb-out), until the final leg's
decoded 300 m point, well past a short strip's last point, where `ReleaseHeld` drops it into the
flight model at the speed and climb the run reached with the decoded 1.0 lever and its patrol net
reseated where it arrived (`StepRuns`, the same shape as `CampaignDirector.PlaceOnPath`'s roster
taxi; `RunningCount` counts the runs in flight). Runs step BEFORE the cycles each `SimStep`, so a
launch this step first moves on the next. Pinned by the `generator-takeoff-run` suite over
C1/M02's `eairg31` (hand-off about 250 m past the last point, 54 m up, 53 m/s) and by
`generator-launch-climb-out`, the same launch flown on by its pilot for 30 s over the real
airfield with the world's colliders up, alive and above the field; the launch pose alone by
`campaign-submarine`. `LaunchOrdinal` numbers
every launch for the decoded `%s_eg%d` instance name. Door transitions play the authored or
node-name-defaulted `open_anim`/`close_anim` (`EnemyGenerators.DefaultDoorAnim`; an unauthored
close is the open, as in the loader) through host-scoped hooks (`AnimRuntime.PlayWithin`/
`StopWithin`); a name resolving no def runs the timing machine log-only. A ground hangar's door is
this cycle's, not the mission script's (`hangar-door-wake` suite). Every drop/live/door/spawn prints an `egen:`
line, which is the flag's observability. `NotifyHostDied(node)`: the zeppelin death aggregator
(`ZeppelinRuntime.ZeppelinKilled`, F18) calls it and the matching cycles disable permanently.
Pinned by the `zeppelin-launch` suite. For campaign missions, `RequireWakeupCredits(hostNode)`
starts cycles named by a script's `WAKEUP_GENERATOR` empty, and
`GrantWaveCapacity(hostNode, n)` is its top-up (`--wake-generators` grants the script's whole
credit at build, the logged headless stand-in for playing up to the objective); the spawn
callback receives the whole `EnemyGeneratorDef`, allowing `vehicle.params` to select its AIV
template while position is still read from the live host. That selection is the mission spawner's
roster read and runs on any generator session: `GameSession.SpawnFromGenerator` spawns the matched
template through `CampaignRosterPlan.SpawnFor`, applies the rest of its slots with `ApplyPlan` and
registers the block's accent, and falls back to the `--generators=` airframe only when the
generator authors no `vehicle.params` at all. A label that names no block returns null, and the
runtime books that as the decoded empty launch: `LaunchOrdinal` advances and the `max_active` slot
stays taken by nothing (the original never frees it, so C1/M04's `eairg32` blocks itself after
four), never an airframe in the block's place. C5/M04's `dantezep` is the one shipped case whose
block authors the nitro slot. The spawn callback returns a `LaunchedVehicle` (an aircraft, a
`SurfaceVehicle`, or neither; a bare `FlightController` converts): a hull off a ship generator
(`GeneratorLaunch.Surface`, C2/M01's `eshipg31`) is booked like an aircraft launch, its
`Destroyed` frees the `max_active` slot, and it is handed its host's take-off path and net
(`SurfaceVehicle.Launch`) BEFORE the aircraft take-off run, which it never enters.
`UseInstantActionLaunches(hostNode, release)` plus the same credit are F12's arm: the
objective zeppelin's generator goes onto the wave-credit budget and its launches RELEASE an
already-built (inert) wave member through the caller's hook instead of spawning a fresh aircraft —
the decoded shape, since `FUN_00452450` finds the parked airframes whose group matches and drops
them from the bay. A released member takes no net pick (it carries its own `primary_target`), and a
hook returning null (the wave has nothing parked left) is accounted exactly like a failed spawn.

## src/Session/ZeppelinRuntime.cs
Runs a mission's zeppelins (M4 F17 motion + F18 damage + F19 broadside, behind
`--zeppelins`): each
`ZeppelinDef` whose world node and net resolve has its hull node switched ON (the record is the
activation: C2 ships `piratezep` with its gamez active bit clear and no mission `.gw` sets it back,
so without this CM13 docks with a Pandora nothing draws), gets a `ZeppelinMotion` on B5's `AiNetFollower`
(arrival radius widened per record to clear the turning circle, and the only follower that
observes stop points), is placed at its authored
position/yaw/pitch, and the NODE is flown kinematically — no FlightController.
One writer per transform channel: a hull an animation motion drives (`MotionSet.DrivesTransform`,
handed in by `GameSession` at construction and adopted from the runtime in `WireDamage`) is
neither placed nor stepped, since the ScriptPlayback and the follower would otherwise both write
the node every frame and the render shows whichever ran last (on the realtime clock the zeppelin
runtime's `_PhysicsProcess`, which is why CM04's Pandora stood in the dock while `pzep_todrydock`
flew it; on a parent-driven clock the anim runtime's `_Process`, which is why no `--det` probe
showed it). The follower parks (`Park`, a `zep:` line) and, on the first step after the motion
ends, `Resume` re-seats the same `ZeppelinMotion` at the hull's live pose
(`ZeppelinMotion.ResumeAt`: pose replaced, speed and turn rates zeroed, engines and limits as
they stand), re-seats the follower and logs the hand-back. The record's seat stays data: C3/M03's
`piratezep` record is the script's END pose, node 0 of `M3PirateZep`, an armed stop point, so the
resumed follower holds the dock there. Neither the scripted-path snap nor the dead-end hold runs
while the hull is scripted, since both live in the step that is skipped. Pinned by the
`zeppelin-scripted-pose` suite over C3/M03's built world.
`SetStopPoint(net, id, halts)` is the whole of `COMPLETED_STOPPOINT`: it arms or releases one stop
point on every follower flying that net, so an airship spawned on an armed node sits docked until
the objective that owns it completes. ⚠ The original keeps the flag on the shared net record rather
than per vehicle; no shipped mission puts two zeppelins on one net, so the readings do not
separate. `WireDamage`
builds the F18 zones over the world registry: gasbags/`cannon_health` cannons seeded from the
RECORD where authored (record hp beats a def pool via `Instance.Reseed`; a fresh pool registers
on the record's destroy-anim def, so zero-HP death plays the authored destruction), engines and
everything unauthored keep their compiled def `HEALTH`; a zone with neither is not damageable
and logs so — never an invented default. `PollDamage` (per `SimStep`) drives
`Motion.AliveEngines`, plays record cannon stages, and owns the kill (`ZeppelinDamage.IsDead`);
the kill logs, stops the motion, plays the prerequisite-gated hull-death def
(`all_pzep_gasbags`-shaped, found by data, never by name) and raises `ZeppelinKilled` (the
generator disable). The same recount raises `ZeppelinEnginesDisabled` once the LAST engine dies
(gated on the hull, since the original's list compaction stops at death): that is Instant Action's
own `zeppelin_run` win, ahead of the hull kill, and `WireZones` warns outright about an engine with
no pool because such an engine can never die and would leave the mode unwinnable on its own
objective. `GateWeaponDamage` is the pool's `WorldDamageGate`. Observability is the
`zep:` lines (wired/zone kills/engines/DESTROYED, plus F19's deploy/fire/skip). The broadside
half is the `ZeppelinRuntime.Cannons.cs` partial: `WireCannons(pool, weapons)` resolves the
HARDCODED `wep_28` and each cannon's node + F18 pool (a destroyed cannon thins the volley; the
lateral sign is re-derived from the built cannon positions), and per step it resolves the
record's `targets` ('player' = nearest human aircraft; any other name = a mission zeppelin,
aimed at a rand()-picked in-arc gasbag) once `SetCannonsEngaged` (the `COMPLETED_ZEPCANNONS`
seam, off until a script runs it) has armed the broadside, gates on `cannon_fire_range` + the arc, plays the
authored deploy/retract anims scoped to the hull, and spawns unowned rounds
(`ProjectilePool.NoShooter`, C9b's convention) scattered by `cannon_inaccuracy`. Pinned by
`zeppelin-motion` + `zeppelin-damage` + `zeppelin-broadside` suites. Zeppelins ride an anchored net
too (`BL-377`, via `NetTrailerTargets`); `formats/ai-nets.md` has the two-of-222 census.
`CollectTargetParts(List<AimCandidate>)` offers those same F18 zones — gasbags, engines, cannons —
to the player's `TargetPool`, one candidate per part, each carrying the hull's own velocity so the
bracket gate has something to lead. It is the only channel by which a structure becomes selectable.
`AuthoredTeam` reads a record's team and `WireZones` fans it, plus the hull's own name as each
pool's `Owner`, onto every zone; `FanTeamsOntoTurrets` finishes that fan on the guns standing on the
hull, which do not exist until `GameSession` has built the emplacements. An unauthored record fans
neither, and its parts are then neutral.
`Hold(node)` (F12) is the runtime counterpart of that flag for a zeppelin Instant Action's own
builder switched off: placed, but no longer stepped, so it neither flies its net nor fires an
invisible broadside. It stands in for `FUN_0045a390`'s `FUN_0045a2a0`, which deletes the vehicle/AI
objects under the deactivated node — CSVM has no such object graph to delete. Switching the world
NODE off is the CALLER's act (`GameSession`, the decoded `gwNodeSetActive`), because the builder's
three `*_zeppelin` names need not be zeppelin records at all. `Wake(node)` is the other flag's
counterpart: a record authoring `deactivated` starts DORMANT, posed at fade alpha 0 so its colliders
drop with it and out of both candidate channels, until a mission's `WAKEUP_ENEMIES` puts it in the
world and the objective's own reveal animation fades it up. `AuthoredTeam(def)` reads the record's
own side into the one shared team space and `WireDamage` fans it onto every zone pool, the way the
original fans one value across the whole airship; a record authoring none leaves the pool's null in
place. Format and decode: `formats/ai-nets.md`, `formats/mission-entities.md`,
`org/targeting.md`.

## src/Session/TurretEmplacementRuntime.cs
The world AA emplacements: `TurretController.BuildEmplacements` resolved against the
built chapter world (`AnimRuntime.FindNodes`; a multi-segment `NODES` path scopes each further
segment to the prior match's subtree), registered with the shared pool so every player's aim
assist sees them (`ProjectilePool.CollectTurrets`), and stepped from its own `_PhysicsProcess` on a
realtime clock or from `GameSession.DriveSimSteps` on a parent-driven one — added to the tree after
the zeppelin runtime, so a slung mount reads its ride's moved pose under either. Built
unconditionally with a chapter flight — the original's world placement pass is unconditional too.
Observability: the `turrets: N world emplacement(s) placed…` census line plus per-turret
`woken`/`engaging` breadcrumbs. Pinned by the `world-turrets` suite (C1 census 74, C4 census 92)
and `mission-off-turrets` (a site the mission's `.gw` switched off places, and stays dead).
`SetActivatedUnder` is the Instant Action builder's own subtree write (the objective hull's 14
rings come up armed, a switched-off hull's go quiet); `SetTeamUnder` is the same walk for the team
a zeppelin record fans across its whole airship; `WakeAll` is the `--wake-turrets` stand-in.
Format and decode, including the wake ordering and the awake-by-data census:
docs/formats/turrets.md "Waking a whole subtree".

## src/Session/AiVoiceRuntime.cs
Wires E16's dispatch into a running flight session (built with the rigs when the world has a
`WorldSounds`; ticks on the sim clock like every consumer): registers each `--ai=…:accent=N`
spawn as a speaker on its real `FlightController.Team` (B7 — no longer teamless), each human rig
as a damage source broadcasting on its own `Team`, and subscribes the wired sites — hit-path DI
tiers (`DamageApplied` summary), `Downed` death cries with force (id 20 `DA` when the dying
aircraft's `Team` is `AimAssist.PlayerTeam`, id 21 `DE` otherwise), patrol→pursue vs a human =
`WA-Attack` + the bearing broadcast on the target's `Team` (our chosen stand-in for the undecoded
"enemy spotted"), sixth-sense stun = the AI evader's `TA-FailTail`, reaction complete =
`TA-SucShk`. Plays through `WorldSounds.PlayOneShot(Node3D)` only; the wired/unwired table is
combat-voice.md "The remake's dispatch sites". Observability: the `ai voice:` lines (resolution at
spawn, every roll outcome, every played clip).
`RegisterAi` also mirrors `FlightController.InPlay` into the dispatcher's `Speaker.Alive` and
subscribes `InertChanged` — the dispatcher is engine-free and can see no controller, so this
is the only place the two meet. An INERT aircraft is registered in speaker order like any other and
is simply not eligible until its wave launches.

## src/Session/FlightRoster.cs
The session-owned aircraft aggregate. `BuildPlayers` commits the whole human field in ascending
player order; `SpawnAi` commits one later mission/wave/generator aircraft. Both paths publish only
finished controllers, preserve the shared livery and spawn streams, and roll back new world nodes
on failure. Rollback tracks each created controller and releases its external HUD, projectile,
speed-cue and race registrations; it also restores human paint/start state, while an AI failure
restores caller pilot state and the paint/AI/spawn streams and leaves its shooter id/name
unconsumed. The aggregate owns the live
human/AI membership views, fans target-source updates to present and future members, and drops its
non-node bindings in `ClearMembership`; the session subtree remains the aircraft node owner.
`HumanFlightAdapter` and `AiFlightAssembler` are the two private assembly implementations.
`SwapPlayerAirframe` is the third commit path, a mission putting one player into a different
airframe mid-flight (callback codes 965 to 967): it removes the outgoing aircraft and re-runs the
human assembler on the named airframe with the pose, heading, throttle and speed that aircraft
held. ⚠ The removal has to precede the build, because `DetachRosterBindings` drops every near-miss
registration carrying that pilot's shooter id and the replacement registers its own under the same
id. The replacement carries the new airframe's own everything: stats, stock guns and hardpoint
table at full ammunition, damage zones, camera profile and engine audio. Rounds already in the air
are unaffected, since they belong to the shared pool under the pilot's unchanged shooter id. The
livery stream is captured and restored around the build so a swap cannot change what a later AI
wave is painted, and a swap builds no stunt run, scoreboard or custom-plane fit: the run belongs to
the pilot, and a bought plane's armour and pylon counts belong to the airframe they were bought
for. `RunSwap` is the whole order a callback raises: that rebuild, then 967's capture half (the
definition's root node resolved through `AiNamed`, hidden, and what is left of its hull AND its own
`PaintScheme`/`ShippedSkins` reading carried onto the new one, off `FlightController.Scheme`/
`ShippedSkins` (a null scheme that reading produced still carries, rather than falling back to the
player's own default paint) and the hand-over of the outgoing aeroplane to `wingman_4` where the
mission resolves that name. It answers an `AirframeSwapResult` naming the aircraft it hid, because
the cutscene that raised the code may be holding that aircraft too. A code carrying an
`AwardAirframe` (965) rebuilds on that special-plane template through `CustomPlaneBuild` instead of
the stock fit, so the injector and the template's guns, pylons and armour land on the replacement,
and one carrying `ShippedSkins` (965) is drawn in the airframe's shipped skin textures with no
scheme composited over them, the state the original's vehicle build leaves a def with no
`paint_pattern` in. 967's livery carry is undecoded in the executable; decode and the deliberate
divergences: `docs/formats/anim-definitions/cutscenes.md`.
Two more things `RunSwap` does, both the original's: every AI pilot holding the outgoing aircraft
as its escort leader or its standing quarry is re-pointed onto the replacement (`RepointHolders`,
the rebuild's `TargetVehicle` walk; the outgoing node is freed, so a holder left on it steers on
a disposed object), and a 967 whose capture root resolved stamps the captured aircraft's
`FlightController.Group` on the replacement (`AirframeHandover.CarriesCapturedGroup`, the
`+0x388` copy), so `CampaignDirector`'s `DEDG` walk counts the player as the bomber they took.

## src/Session/FlightRosterInputs.cs
The grouped construction facts accepted by `FlightRoster`: copied `FlightRosterPolicy`, immutable
aircraft/archive resources, live world services, and human-session bindings. These contracts keep
the roster from accepting all of `SessionSpec` or exposing either internal assembler while making
required dependencies explicit at the production seam.

## src/Session/AiFlightAssembler.cs
The roster's private AI assembly path. It prepares authored/fallback pilot skills and maneuvers,
builds the model, controller, livery, loadout/ordnance, damage visuals and optional crash runtime,
then places the finished node. It also resolves the def's authored `title` into
`PlaneStats.AiTitle` (the militia name the targeting readout prints), because this is where the
loaded def and the session's string table meet. `FlightController.Scheme`/`ShippedSkins`/`Painter`
record this build's paint resolution and its own painter, read back by an airframe swap carrying a
captured rig's livery onto the player's rebuild. The assembler owns the one AI skills cache;
`FlightRoster` lends that already-loaded table to the session's voice adapter without reopening
the archive.

## src/Session/HumanFlightAdapter.cs
The roster's private human-aircraft implementation. One `Assemble(pi, rig, swap)` builds the painted
model, `FlightController`, loadout/ordnance, carried turrets, HUD/instruments, damage visuals,
audio, stunt/match bindings, target selection, authored start placement and crash runtime.
It reads only the roster's copied policy plus grouped aircraft, world and human-session contracts;
it never receives `SessionSpec` or publishes a partially configured controller to the caller.
Player order remains load-bearing for the shared paint and spawn streams. A `swap.ShippedSkins`
scheme (an airframe hand-over's captured rig) is drawn as-is, null included, ahead of the default/
custom paint fallback, so a captured rig with no scheme is not silently repainted the pilot's own.
A swap's `Build` stands in for the pilot's own custom plane on that assembly, which is how 965's
Blue Streak reaches the engine override, the injector, the fit and the armour by the one path a
bought plane takes.
`BuildDamageVisuals` is also the common first phase for AI damage; `WorldEffectsFactory.BuildFlightCrashRuntime`
supplies the optional second phase once a controller is in the tree.
## src/Session/EffectCatalogue.cs
The record of which authored anims are playable effects, and what their defs need staged: the name
tables every effect producer must stay inside, static and engine-free. Owns `EffectAnimNames`, the
crash-rig's own name sets (`CrashDefTable`/`AiCrashDefTable`/`TouchdownDefTable`,
`PlaneDamageEffectAnims`, `PropChoreographyAnims`, `NitroAnims`, and the two damage-stage menus
`PlayerDamageStageAnims`/`AiDamageStageAnims` with their union `DamageStageAnims`), the death
path's OTHER slot (`AirframeDestroyAnims`/`DestroyAnimFor`, the self-named destroy def, with
`FliesOwnHull` asking the data which family owns the landing), `ResolvedSurfaceIds`
(the collider overlay's colour key, `BL-345`), and the anchor-root derivation
(`StageRootsFor`/`WorldStageRoots`/`CrashStageRoots`) — this IS `WorldEffectsFactory`'s stage
source; an unstageable anchor fails the build rather than leaving a def anchored on nothing. Every
name producer carries a producer-range unit tripwire in `CSVM.Tests` (`ImpactOutcomeTests`,
`EffectCatalogueTests`) asserting its whole producible range resolves inside these tables.

## src/Session/WorldEffectsFactory.cs
Builds the impact/destruction effect stages and the per-plane crash runtime: the world-effects runtime and
`BuildFlightCrashRuntime` — which despite the name binds every def that plays ON one aircraft: EVERY
playable slot of the crash-def vector (`EffectCatalogue.CrashDefTableFor` — `player_crash_*` for a
human rig, `ai_crash_*` for an AI plane, keyed on `IsHumanPiloted`; handed to the
controller as `CrashDefs`; the struck surface is only known at impact, so the whole vector is
bound and `FlightController.Crash` indexes it with the struck body's surface id — the ai family's
authored NAME `kestrel` resolves nowhere in a rig, so those defs take the crash root as their
context node the way the original's caller supplies one).
The pool-config drift check (`EffectPools.UnknownCrashRoots`) is asked against the roots BOTH rig
kinds stage, derived by `BothRigKindsStageRoots`, not this rig's alone: `effect_pools.json` carries
one `crashRoots` section for two families that stage different roots (an AI rig stages no
`apassengers`, a human rig no `small_injure_fireball`), so a per-rig test reports every correct entry
the other kind owns. ⚠ That union is resolved BEFORE the templates are staged, because a staged root
answers `InScope` rather than `Stage` and drops out of the derivation.
**plus** this rig's destroy def (`EffectCatalogue.DestroyAnimFor` → `DestroyDef`, with
`DestroyDefFliesWreck` recording whether that def carries the wreck's fall and landing itself)
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
for the regression shape. The stage has **two** sources: the chapter gamez, then the planes gamez
for a root it has none of, which is the only place the destroy def's parachute (`chuteman`) lives;
both spawners pass it, and its own builder is cached here for the session.
After the bind, `BuildFlightCrashRuntime` pre-warms the rig's emitters
(`AnimRuntime.PrewarmEmitters` with the plane model and the crash root as the call-site anchors),
recorded as the `emitters` startup phase and logged per rig, so a crash or a damage stage finds its
puffers and materials built and trips no `effect_pool_miss`. Measured on the Bloodhawk in C1: 217
emitters in about 45 ms, of which the shader and atlas caches (`EmitterRenderer`, `Puffer`) are the
difference from 2.3 s. Every rig kind is pre-warmed, an AI rig included, since its crash pays the
same first-use cost. `BuildWorldEffectsRuntime` pre-warms the same way after its own `Bind`, with
no call-site anchors, since its callers place pooled copies and the staged-copy term already covers
the `INPUT_NODE` hosts: 455 emitters in about 65 ms at one player in C1, so an ordnance impact's
puffers (the sonic burst's nine) exist before the first burst rather than being built inside it.
`LevelPlacedTemplateNames` is set once, from `EffectCatalogue.CrashSurfaceLevelAnimNames`
(`BL-292`) plus `EffectCatalogue.BailoutAnimNames` — the named defs only ever play from within a
crash sequence, so unlike `InheritedWorldVelocity` (written per anim instance by `Callback 16`,
since it carries the dying vehicle's live velocity) this needs no per-crash toggle. ⚠ There is no
companion opt-out list for the momentum: which motions inherit is the authored `impact_force` bit,
read per event in `MotionRuntime` (`docs/org/objectMotion.md`), and a name list there would
re-answer by hand a question the data already answers. The parachute is on that list for a different reason than the splashes:
its template is authored at identity and lives in the planes gamez, so only this rig's staging (the
copy hangs under the crash root) gives the placing call a rotated basis to freeze, and levelling
restores the pose the original places it at. `AnchorWarnAnimNames`/`AnchorWarnLabel` are injected the same way, from
`EffectCatalogue.DamageStageAnims` and the plane's own name: the stage defs are authored against one
airframe and retarget onto whichever plane stages them, so an anchor they name may be absent, which
is a soft failure (the call lands on the airframe root and still draws) that nothing else reports.
The
prop choreography's own defs resolve their `staticpropN`/`propN`/`propNb` node names against the
plane model directly (`LOCAL_NODES_ONLY`) — `FlightController.Respawn`/`Crash` call
`CrashRuntime.Play("startprops"/"stopprops", PlaneModel, applyReset: false)` themselves, since
nothing else calls them. The
names it binds now live in `EffectCatalogue` (its own entry) — `EffectAnimNames` covers impact +
death effects, including the 12 gun `*_gunhit` variants (a gun hit plays throttled and
time-bounded), the `DAMAGE_SEQUENCE` stage pair `sputter_black_smoke_obj`/`sputter_fire_smoke_obj`
(root `partial_damage_obj`), and the airframe's three graze reactions
(`touchdown_default`/`_dirt`/`_water`, roots `spark_touchdown`/`dust_touchdown`/`splash_touchdown`
+ `yellow_spark_01`, played by `FlightController.GrazeReaction` off `EffectCatalogue
.TouchdownDefTable`'s surface-indexed vector, which `WorldEffectAnimNames` appends to the bind).
This module still does the staging: `Subset` handles 8/30 destruction targets; 22 live-object
choreography names remain local (`analysis/death-effect-closure/`), and the stage-call closure
excludes C4's train-anchored `b_steamtrail`. Constructed once per session (`_worldEffectsFactory`,
same lifetime as `LiveryResolver`/`SpawnPicker`) from
`(SessionSpec, Node3D worldRoot, Func<Vector3> playerPosition, EffectAmbience?, Func<IReadOnlyList<Vector3>>? playerPositions)`,
plus a settable `ScreenFlash`
sink it hands to the effects runtime — the three defs carrying an `FBFX_COLOR_FROM_TO`
(`he_ground_effect`/`ap_ground_effect`/`flak_effect`) all play there. The sink's last two arguments
are the burst point and the def's own gate radius squared, B12's pane routing; this class only
forwards them. `playerPositions` (`BL-365`; null → the single `playerPosition` alone, the
pre-C21 behaviour a caller with no seam — `AiCrashDefs`' test rig — still gets) is set as
`PlayerPositions` on the built world-effects runtime, so its own `If PlayerRange` gates (the same
three washes) answer to the nearest human rather than one camera; `GameSession` feeds the identical
snapshot this factory gets and `WorldSession.Options.PlayerPositions` gets, from one
`PlayerPositionsSnapshot()` method, so the two runtimes can never disagree about who is nearest.
The effects runtime's puffer factory passes `softParticles: false` for MIX-ramp states — these effects
emit at ground-level sites, where the depth fade zeroes fresh dark puffs against the terrain (the
crash-smokeball lesson; the damage-stage smoke measured near-invisible with it on) — and keeps the
soft edge for additive fire. Templates build with collision suppressed and the stage is visible with
each ROOT hidden (`TemplateStage.Shown` reveals one while an effect plays on it), so a
template's meshes render — the rocket's per-type explosion rings, the fireball facades. The
runtime's stage is built here and handed into `ForEffects` **sealed** — `Pooled`+`Shown`+`Places`
as constructor state. Until A4 the last two were written onto the returned
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

## src/Session/WeatherRig.cs
Loads/applies the flown mission's weather and drives its per-rig skydome/whiteout/deck/zone-gate
update every frame: `LoadWeather`/`SetupWeather` become `Build`, and the per-rig update block from
`_Process` becomes `Tick`. Constructed once per session (`_weatherRig`, same lifetime as
`LiveryResolver`/`SpawnPicker`/`WorldEffectsFactory`) and discarded with the session node on
return-to-menu — its per-rig nodes hang under `_worldRoot`, so the session's `QueueFree` frees them.
**What the original does per frame is written up in [org/weather.md](org/weather.md)** — including
the retired `DeckCeilingHeight` fits. The authored side is [formats/weather.md](formats/weather.md)
and [formats/weather/atmosphere.md](formats/weather/atmosphere.md); the fog-volume whiteout curtain
is [formats/fogvol.md](formats/fogvol.md). `ApplyFogState` is the animation runtime's `FOG_STATE`
sink (via `GameSession`, which holds an event raised inside the world bootstrap until the rig has
written its zone): it writes only the fields the event carries onto the same fog globals, and the
next zone edge writes the zone back over it, the original's last-writer order. `FogGlobals` mirrors
the last writes, since the renderer refuses to read a global back outside the editor. Proven by
`CSVM.Tests/FogZoneStateTests.cs`, `DeckRegimeTests.cs`, `FlatColorTests.cs`,
`FogVolumeWhiteoutTests.cs`, `BandFlickerTests.cs` and the `fog-state` suite.

## src/Session/LensFlareRig.cs
The sun's lens flare: four screen-space sprites strung along the sun→screen-centre vector at
fractions 0.50/0.90/2.0, plus a full-screen white wash whose opacity is ~linear in the sun's screen
distance from centre. Mirrors `WeatherRig` — constructed once per session beside it, `Build` once,
`Tick` from the same per-rig block of `_Process`, one instance per pane. The whole spec is measured
(`CAP-13`; method and calibration in `analysis/bl-165-lens-flare/`), and the per-pane state lives on
this class rather than on `PlayerRig` because an instance is several nodes plus fade state — the
whiteout could live there only because it is a bare `ColorRect`.
`--no-flare` suppresses the effect so C2/C3 captures stay usable for unrelated comparisons.

## src/Utils/Config.cs
Dev-facing tuning-override layer: static `Config` parses an optional sparse `res://config.json`;
the typed getters (`GetFloat`/`GetInt`/`GetBool`/`GetString`) return the file's value for a present
key, else the caller's in-code `const` default — read-through at the point of use, keys
`moduleCamelCase.fieldCamelCase`, grouped one nesting level in the JSON and flattened to dot-keys.
Read-only — nothing writes the file; `config.json` is git-ignored, so the consts stay canonical.

## src/Utils/EffectsLevel.cs
The original's graphics EffectsLevel option as a config key (`graphics.effectsLevel`: `high`,
`medium`, `low`; default `high`, which `detail.zrd` selects on any CPU over 600 MHz), and the one
global it drives today: `csky_clutter_fade_scale_sq`, the squared distance scale every
templates-clutter fade multiplies into its camera distance (1.0/4.0/9.0), registered once by
`Launcher` beside the fog globals and declared in `shaders/csky_clutter_fade.gdshaderinc`. The
level's meaning and direction: docs/formats/templates.md. Consumed by `ClutterBuilder`'s sprite
shader and `SceneBuilder`'s `clutterFade` bias-shader variant.
A second key, `graphics.clutterFarFade` (bool, default `true`), is the remake's own switch rather
than an engine option: `false` resolves the same global to 0, which is a never-fades scale because
the shader multiplies it into the squared camera distance, so no stamp reaches its near² and
clutter draws out to the fog instead of ending at the authored metres. Both keys and the resolved
scale are on the `[world] clutter fade:` launch line.

## src/Utils/ScriptedWindow.cs
Win32-only window hiding for scripted runs: `ScriptedWindow.Hide()` calls `ShowWindow(SW_HIDE)` on
the native window handle. Fully static, one call site in `Launcher._Ready` right after the `--det` block — the same
predicate drives both window hiding (scripted run) and focus request (interactive run).

## src/Session/ExtractionStamp.cs
Reads the provenance stamp `ExtractAssets.ps1`/`ExtractRof.ps1` leave at `extracted/VERSION.json`
(unzbd version line + exe SHA-256 + fork commit, dates, schema integer) and compares the schema
against its `Schema` const in `Launcher._Ready`, right after the base paths settle. At most ONE
warning line per boot — stale schema, missing file, or unreadable — each naming the fix (re-run
the extraction scripts). Warn, never block: the dev tree holds valid extractions predating the stamp.

## src/Flight/CollisionLayers.cs
The named physics collision layers — world (layer 1, the engine default every pre-existing
collider sits on implicitly) and aircraft (layer 2, `AircraftBody`) — plus the combined mask.
The first and only place a layer bit is assigned a meaning; new layers go here, never inline.

## src/Flight/AircraftBody.cs
The flying aircraft's physics body: one `AnimatableBody3D` child of `FlightController`, one
`CollisionShape3D` per `PlaneCollider.Part` reusing the SAME `ConvexPolygonShape3D` + local
transform the terrain sweep casts, on the aircraft layer. Rides the controller's transform;
`PartName(shapeIdx)` maps a query's struck shape back to the part (shapes added in `Parts` order);
`ExcludeSelf` is the cached one-entry RID list the owner's own queries pass; `SetHittable` drops it
to layer 0 while the plane is out of play — crashed, or INERT — and back when it is in play again,
both driven from `FlightController.ApplyPresence`. Also the fuse/blast geometry oracle, answering
from the same `ConvexHull` set without a physics query: `NearestShape(point)` (nearest hull, its
skin distance + surface point — blast falloff), `SegmentDistance(from,to)` (closest approach of a
swept round, ternary search per hull — distance to a convex set is convex along the segment),
`BoundRadius` for the cheap per-step reject, and `TakeProjectileHit(..., damageScale)` scaling
both damage magnitudes by the blast falloff share (1 = direct round).

## src/Flight/IWorldQuery.cs
The one seam onto the live physics world: `Sweep` (a shape moved along a motion, earliest stop
across a named part list), `Ray` (one ray), and `Overlaps` (does any named part touch anything at
a standing pose, which is what the un-embed loop asks). `FlightController` reads the world only
through this; nothing else may reach `DirectSpaceState`. `SweepReport`/`RayReport` carry the answer,
including the struck collider as a plain `Node?` so a caller builds its own name. A carried
`TurretController` reads the same seam for its line-of-sight check
(`TurretController.WorldBlocksLine`), built with the `GodotWorldQuery` its `BuildCarried` makes
from the host it is riding; a synthetic `IWorldQuery` proves the mask and the blocked/clear cases
off-engine (`TurretLineOfSightTests`), with no live node in the process.

## src/Flight/ContactReport.cs
One detected contact, as the value both halves of detection fill: the impact, the struck surface's
normal, which airframe box reached it first, the collider's name, how far along the frame's motion
the airframe stopped, and `StruckIsAircraft`. `FlightController.SweepAirframe` fills it from the
`IWorldQuery` sweep; `CenterRayContact` fills the same shape from the anti-tunnelling centre ray,
where there is no struck box and no surface normal, so the part reads `center`, the normal is the
reversed motion (making the contact head-on) and the stop fraction stays 1. The report holds no
`Node` on purpose: the only question the decision side asks about the struck object is whether it
is an aeroplane, and the caller keeps the collider for the applying (the struck rig's damage, the
crash def's surface id, the graze reaction). The grace window is a precondition of detection rather
than a filter on a report: while `_collisionGrace` is live no sweep runs at all.

## src/Flight/ContactOutcome.cs
What one contact costs the striking aircraft, as a value with no `Node` and no physics space behind
it: the fate (`ContactFate.Graze` survivable, `Crash` fatal), the decoded damage pair both parties
spend, the doom rule's answer, the zone the ledger charged (`Apply`'s answer, not the geometric
guess), the pilot HUD's flash line, `PushOut` (how far along the normal the caller must move the
striker to un-embed it, applied on a crash too), `ShakeMagnitude` (the block-5 camera kick, zero on
an AI), and `DamageStruckAircraft`, the instruction a
caller owes because only it holds the struck rig: hand that aeroplane the pair and arm the
collision grace on both parties. One value with no optional parts, so forgetting to perform it is
forgetting one statement rather than four. `AircraftContactResolver` fills it.

## src/Flight/AircraftContactResolver.cs
The decoded contact rules for one aircraft, holding an `IWorldQuery` and no `Node`: the damage pair
both parties spend (`FUN_0048d2c0`), the fate (the doom rule, no damage data, health
exhausted, an airframe that cannot un-embed), and the un-embed loop over the
seam's `Overlaps`. One call answers one contact with one `ContactOutcome` the caller performs.
The engine effects it interleaves with, because each one's result is the next rule's premise, go
through `IContactEffects`: the fly-through offer, the graze reaction, the ledger spend and its
readouts, and the contact response. `FlightController` implements that as a per-contact
`ContactEffects`, keeping the struck `Node`, the sweep cadence and the flight model on the node.
`ContactConditions` is the striker's state per call, `IsHumanPiloted` included. Every contact the
resolver is handed spends the pair; the original's every-other-frame cadence is the sweep's
(`SweepCadence`), never a gate on the spend.
`AircraftContactResolverTests` pins the rule table off-engine against a synthetic `IWorldQuery` and
a scriptable `IContactEffects`: the doom rule for an AI ramming a non-aeroplane, the entity cut for
AI into AI, the player's exemption from both (asserted on the damage magnitude, not just
`DamageStruckAircraft`, since the entity cut applies only on the non-player branch and only against
another aeroplane), a sustained slide exhausting its ledger against a control that spends nothing,
the camera kick every human-piloted contact spends and an AI's spends none, and the un-embed loop's
three-try give-up.

## src/Flight/SweepCadence.cs
The original's alternate-step collision sweep (`FUN_0048d7f0`'s parity gate and the `obj+0x6B0`
accumulator, docs/org/flightModel.md "Collision response") as a pure value with no `Node`:
`Advance` answers whether this sim step sweeps and, after a skipped step, the origin the sweep runs
from, so the carried motion is swept whole; `Respawn` resets the phase. The parity gates the SWEEP,
never the spend: a contact the sweep resolves always spends the pair. `SweepCadenceTests` drives it
with the real resolver and ledger against a kinematic wall, pinning the shallow-then-steeper sequence
from the controls, a sustained scrape dying in three 50-floor spends, and the spend-gated control
that locks onto the free steps and never spends.

## src/Flight/AircraftLifecycle.cs
The states one aircraft moves between and the rules that move it: in play, crashed, destroyed with
its wreck still flying, inert, and back to spawned. It owns those flags plus the collision-grace,
carrier-drop ground-blow and auto-respawn timers, holds the crash-def table and the selection off it
(`LastCrashDef`), and holds no `Node`, so the whole table runs in a unit test. Every transition
REPORTS what happened instead of performing it (Decision 7 of
`docs/plans/PLAN-flightcontroller-deepening.md`): `Crash(surfaceId, killer)` answers one `CrashOutcome`
(did it happen, was it the wreck landing, which crash def, whether the shutdown, the camera cut and
the `Downed` report are owed, and the killer to name) and `Destroy(destroyDef, killer)` one
`DestroyOutcome` on the same terms, with `WreckFalling` deciding whether the hull flies itself down.
Each is one value with no optional parts, so a caller that forgets half a crash is forgetting one
statement rather than four. `FlightController` keeps `Crashed`, `Destroyed`, `WreckFalling`, `Inert`
and `InPlay` as forwards onto it, so its fifteen internal readers and every session-side consumer
read the same spellings they always did, and it keeps the `Downed`/`InertChanged`/`DamageApplied`
events, which the session subscribes to. The guard that a crashed aircraft cannot crash again is a
transition rule here: `Crash` refuses while `Crashed`, and the single exception is the falling
wreck's own landing, which reports `WreckLanding` and owes neither the cut nor the report because
the death was reported at the kill. `ArmSpawnTimers(carrierDrop)` opens the spawn's collision-free
window (`CollisionDamage.SpawnGrace`), and the carrier-drop arm adds
`CarrierDropGroundBlow` seconds of the 0.15 ground-blow multiplier on top; `ArmCollisionGrace` is
the shorter window a resolved ram writes to both parties. `SetInert` answers whether the flag moved,
which is what makes the node's presence write and its `InertChanged` raise conditional.

## src/Flight/GodotWorldQuery.cs
The only adapter over Godot's `DirectSpaceState`, implementing `IWorldQuery`. Resolves the wrapped
node's `World3D` at each call rather than caching it, since the node may be bound before it joins
the tree. `Sweep` holds the airframe's whole per-part cast/rest-info dance, including the 0.05 m
nudge past the first overlap (`GetRestInfo` can come back empty exactly at the unsafe fraction).
