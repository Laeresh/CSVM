# Session

The `Launcher` scene root, the per-launch `GameSession` node, and the low-coupling session-build clusters they delegate to.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Session/GameSession.cs
The per-launch orchestrator: `Launcher` constructs it from `(SessionSpec, LauncherContext)` and
`StartSession` runs ordered build phases over one local `BuildState`. It owns the session clock,
world root, panes, mode runtimes and resource lifetimes, delegating aircraft assembly and
membership to `FlightRoster`, world construction to `WorldSession`, and effects staging to
`WorldEffectsFactory`. After the synchronous build it constructs one `SessionSimulation`, which
owns the step order; `_PhysicsProcess` requests one realtime step and `_Process` each
parent-driven `GameClock` substep. `AllAircraft` combines the roster's AI view with the ordered
rig controllers, for consumers that read membership without stepping it. Exit frees the session
subtree atomically and releases only the non-node resources this orchestrator owns. The
prohibitions that keep those rules true, no `Teardown`, no argument parsing here and no
re-derived camera set, sit on the members they bind. Read `SessionSimulation.cs` for the step
order and `Launcher.cs` for what outlives one session.

## src/Session/SessionSimulation.cs
The session simulation: one plain-C# module owning hold admission and the exact order of flight,
combat, mission, radio, effects and match advancement. `Step(dt)` snapshots eligible AI
membership at entry, then advances incoming fire, projectiles, human aircraft, zeppelins,
emplacements, generators, surface vehicles, captured AI, landing approaches, Instant Action,
campaign, radio, smoke, tags, AI voice and Versus. Authored animation, cutscene presentation,
weather, lens flare, terrain extension and wall-time watches stay outside it, with
`GameSession`. `ISessionSimulationRuntime` is the named recording and production seam rather
than a participant registry. Read `GameSession.cs` for who owns each phase.

## src/Session/Launcher.cs
Main.tscn's root and the once-per-process bootstrap: CLI parse into `_cli`/`_spec`, data-root
precedence, the `--dump-*`/`--run-tests` early quits, and everything that outlives a session
(camera, orbit rig, sun and `WorldEnvironment`, the master audio bus levers, the music channel,
the perf and hitch instruments, the enhanced-graphics lighting gates). It owns the menu as one
`MenuHost` built on the first show, the presentation resolution and the only options write in
the codebase, and the sink every way out of the menu passes through
([../menu-presentations.md](../menu-presentations.md)). `LaunchSession`, `ReturnToMenu`,
`RestartSession` and `BeginLaunch`/`RunOwedLaunch` are every path a session starts or ends on.

## src/Session/LiveryResolver.cs
Resolves which livery each player flies: the shipped paint catalog and the per-pattern
region-mask library (both lazy and cached), `PatternsForPlane`, and the per-player `SchemeFor`
pick that reads a `SessionSpec`'s `--paint=`/`--paint-color=`/`--paint-decal=` overrides.
Constructed once per session build, so a relaunch resolves against the fresh spec. With no
override every aircraft wears `DefaultPattern`, the only pattern covering all eleven airframes;
an AI spawn prefers an explicit scheme, then its own def's `paint_*`, and reaches that default
only when neither is authored. Instant Action enemies are the exception, for the reason
`InstantActionRuntime.cs` gives. Paint decode: [../org/paint.md](../org/paint.md).

## src/Session/SpawnPicker.cs
Resolves each player's flight spawn: `ChooseSpawnBase` (the shared `--spawn=`-or-random list
index), `ChooseSpawn` (a player's position and look-at from that list, `objectives.json`'s
`PLAYER_INIT`, or the `--spawn-at=` debug override) and `LogSpawn`. Constructed once per session
build, the same lifetime as `LiveryResolver`. Also the plain `IFlightStarts`: `ChooseStarts`
loops its own `ChooseSpawn`, which is the placement every session flies except a splitscreen
race or a co-op campaign mission. `StartGrid` takes its anchor from here, and the weapon lab and
the freecam spectator call it directly, so this type stays the single owner of spawn resolution.
Spawn data: [../formats/spawns.md](../formats/spawns.md).

## src/Session/IFlightStarts.cs
Where every pilot in a session starts: `ChooseStarts(spawns, missionZrdrPath, spawnBase,
playerCount)` returns one `FlightStart` per player, the same `(pos, lookAt)` pair
`FlightController.Setup` takes. Two implementations: `SpawnPicker`, the plain per-player walk of
the mission's spawn list, and `StartGrid`. `HumanFlightAdapter` holds the interface and resolves
the field lazily on its first `Assemble`. Answering for the whole field at once is the point of
the seam, and the constraints that keep it so sit on the interface's own members.

## src/Session/StartGrid.cs
The abreast starting grid and second `IFlightStarts`: it fans slots symmetrically across one
anchor heading, then lifts the whole field by its lowest terrain clearance. Anchor selection
stays with `SpawnPicker`, preserving mission `PLAYER_INIT`, Instant Action lists, fallbacks and
`--pos`; the heading comes from the anchor's position-to-look-at pair, so `--direction` survives.
Terrain is an injected `Func<Vector3, float?>`, production using `GameSession.GroundSampler()`,
and `startGrid.slotSpacing`/`startGrid.groundClearance` are registered TUNE values.
`GameSession` selects this grid for multiplayer campaign sessions and eligible splitscreen stunt
races; solo, Dogfight and deterministic scripted race starts keep their own placement paths.

## src/Session/PlaneRoster.cs
Static, spec-free lookups over a `SessionSpec`'s plane roster: `PlaneFor(spec, index)`,
`PlaneDisplayName(stats)`, `Humanize(s)`. A plane's display name is the def's AUTHORED `title`
(`PlaneStats.AiTitle`, "Medusa Kestrel") where something has resolved it through the string table,
and the def-name derivation ("Bloodhawk") otherwise, which is what a player load and a bare rig get.
No session state: every call takes the `SessionSpec` explicitly rather than caching one, since
these are pure over their arguments.

## src/Session/SurfaceDefTable.cs
One of the original's per-surface anim-def vectors (`"player_crash_" + name`, `"ai_crash_" +
name` or `"touchdown_" + name` over every `SurfaceRegistry` slot) plus the cascade that indexes
it with a struck material's numeric surface id (`SceneBuilder.SurfaceIdMeta`). Built once per
bind from a caller-supplied "does this def exist" test, so `PlayableDefs` is what a runtime binds
and `DefForSurfaceId` is what an impact asks. Engine-free and pure; the fallback cascade and its
empty-slot arm carry their prohibition at the type itself. The AI family runs the same cascade
over the same fields with the vehicle's own name as its last resort. Full decode, including the
touchdown family and the weapon `IMPACT` table: `analysis/surface-classification/FINDINGS.md`.

## src/Session/InstantActionDirector.cs
The engine side of one Instant Action mission, behind `GameSession`'s one nullable `_iaDirector`
field: a plain sealed class that builds no node of its own, so every actor it makes parents under
the handed world root. `TryCreate` takes the wizard's def or `--ia=<path>`; `BuildActors` is the
contiguous actor phase (the chapter's first patrol net, the ace, the wingman fan and its escort
chain, every configured wave built inert at the world origin); `Step` ticks the sequencer and
activates what it returns; `WireEndConditions` routes each mode's own win signal, the lives
ledger and the whole-window wrap-up board. The decoded rules stay engine-free in
`InstantActionRuntime.cs` and `InstantActionWaves.cs`; this class owns every `ia:` log line.

## src/Session/SpectateHandoff.cs
The shared pane handoff for an Instant Action pilot out of lives or a campaign human whose aircraft
is lost while teammates continue. `Begin` pins the wreck through `FlightController.Spectating`,
releases the pane camera through `CameraOwned`, and creates a `SpectatorCamera` at the crash view's
last pose. It follows the first other rig still `InPlay`, or starts free when none exists, and uses
the downed pilot's own device filter. A false result means that pane already has a spectator.
Candidate and tracking lists are optional for callers without a roster or rerun path.

## src/Session/InstantActionRuntime.cs
Owns one Instant Action mission's actor set: the loaded `InstantActionDef`, the ace's spawn draw
and rating, the wingmen's fan placement, each wave's per-member draws, the objective-zeppelin
selection, and the mission's end. The static, engine-free helpers `InstantActionDirector` calls
are here (`ChooseAceSpawn`, `RepresentativeRating`, `WingmanSlotFor`/`FlownWingmen`,
`RandomPilotStats`/`ResolveWaveAccentId`, the zeppelin lookups, `FormatElapsed`/`ShotPercent`).
The end half holds no engine type and calls no `GD.*`, the same construction rule `VersusMatch`
follows, and the director owns every log line about it. Format and decode:
[../formats/instant-action.md](../formats/instant-action.md).

## src/Session/InstantActionWaves.cs
The decoded wave sequencer's own selection, trigger and geometry logic (`FUN_0045b9d0`): pure
state over `Start`/`Step` calls, in the shape of `GeneratorCycle`, pinned off-engine by
`CSVM.Tests/InstantActionWavesTests.cs`. `InstantActionDirector.BuildActors` builds every
configured wave's members inert at the world origin, then calls `Start()` and activates whatever
it returns; the director's `Step` ticks this once per sim step, and its `ActivateWave` resolves
the spawn draw against every live human's current position before activating each member. Format
and decode: [../formats/instant-action.md](../formats/instant-action.md).

## src/Session/ObjectiveScript.cs
One mission's `objectives.zrd`, parsed into the typed shape `ObjectiveGraph` runs. `Load` takes a
mission zrdr scope and yields an empty script for a mission with no file, which is what every
Instant Action and multiplayer stub amounts to. Two parser rules are the original's and are what
keep the shipped data honest: blocks are read `OBJECTIVE1`, `OBJECTIVE2` and stop at the first
missing number, and every directive is found by exact name over the block's flat alternating
list, so a truncated or misspelled key lands in no field and stays dead. The four target
directives and `SET_HELP_LABEL` read `ObjectiveTarget`s, where a nested list is ONE path keyed
`parent/child`. Format and decode: [../formats/objectives.md](../formats/objectives.md).

## src/Session/ObjectiveSites.cs
The flown campaign mission's objective sites, offered to each player's `TargetPool` as
objective-flagged candidates, which is what tells the player where to go. The set is
`targets.zrd`'s own `objective` entries plus every roster block that authors the flag on itself,
minus whichever of those a completed objective's `REMOVE_OBJECTIVE_TARGET` names, plus whatever
`ADD_OBJECTIVE_TARGET` has added. A site is keyed by `ObjectiveTarget.Key` and re-read every
frame, so it tracks a moving node; `PointFor` and `SiteAnchor` decide where its marker stands,
and `Messages` gives the marker its verb, proper name and colour. `GameSession` binds the set
through `FlightRoster.SetTargetObjectives`. Marker decode: [../org/targeting.md](../org/targeting.md).

## src/Session/CampaignHumanField.cs
Engine-free objective rules over every joined human, represented by `HumanState` position, captured
group, and wreck state. `CampaignDirector.World.SnapshotHumans` is the sole producer. `Travelers`
uses the nearest human, so approaching succeeds on the first arrival and departing on the last
exit; wrecks continue reporting their positions. `LiveInGroup` counts non-wrecked humans in a
captured group and ignores temporary cutscene inertia. The scripted player remains a separate P1
identity for authored `player` tokens, roster leaders, and anchored net trailers. Rules are pinned by
`CSVM.Tests/CampaignHumanFieldTests.cs`.

## src/Session/CampaignDangerZones.cs
A campaign mission's own danger zones: the `dzpathN` names its `objectives.zrd` authors inside
`DANGER_ZONES_COMPLETED`, narrowed by the mission's `dzones.zrd` `disable` list and resolved
against the world's gate geometry, with `Load` answering null when a mission arms none. A zone is
a route ribbon plus a material-matched pair of gate polygons, crossed by a segment passing inside
each in either order, which is the rule `Flight/StuntMission.cs` reads off `ia.json`'s zone list;
the two differ by authoring surface, not by mechanism. Crossings are tracked per human, each with
its own previous position. `CampaignDirector` counts what the graph's condition asks about. Gate
decode: [../formats/missions.md](../formats/missions.md).

## src/Session/ObjectiveGraph.cs
The objectives runtime over a parsed script, pure state over `Step` calls in the shape of
`InstantActionWaves`: no Godot type, no logging, pinned off-engine by
`CSVM.Tests/ObjectiveGraphTests.cs`, with `CampaignDirector` owning every log line about it.
Implemented as decoded, shipped quirks included: the four states per objective, at most one
completion per tick from a rotating scan, dependency gating on an AWAKE target, the wake
executor's truncating early return, the nap that clears a completed flag, and the condition
families' OR. Four endings reach it, three from the script and `NotifyDockingComplete` from the
animation. World seam: `IObjectiveWorld`. Decode: [../formats/objectives.md](../formats/objectives.md).

## src/Session/CampaignDirector.cs
The engine side of one campaign mission and the sibling of `InstantActionDirector`: a plain sealed
class building no node of its own. `ResolveSpec` runs in `GameSession`'s constructor and turns a
`--campaign=<profile>:<seq>` position into a chapter and mission; `BuildRoster` plans and spawns
the `aiv` blocks through `CampaignRoster.cs`; `Attach` arms the graph once every runtime a
directive can touch is up; `BindCallbackHost` takes the `CALLBACK` slot ahead of the generator
runtime's, where 801 to 803 reactivate the lowest-numbered still-deactivated Black Hat of their
family, CM19's only launch path; `Step` runs the graph, the escort repair and the music. The
nested `World` is the `IObjectiveWorld`, a directive with no seam here a named no-op; mission end records the attempt, folds the persist log into the profile and holds before the cabin behind `LeavingFade`, the ramp `UI.MissionEndFade` paints. Debrief: [../org/debrief.md](../org/debrief.md).

## src/Session/CampaignProgression.cs
The campaign's progression rules over a profile: recording one mission attempt with the original's
best-of merge, raising the position, and granting the aircraft awards. The position is a single
monotonic integer that only a completed primary objective raises, because `cm_sequence.zrd` is a
flat list with no predicate and no alternates. `Record` also works out what a mission pays, since
the reward table is gated on what the profile has already banked, and reports it back through
`MissionRecorded`. The four-attempt skip offer is raised here and answered by `AcceptSkip` on the
screen that shows the result. `AirframeCount` fixes the kill tallies' width. Reward table:
[../org/hangar.md](../org/hangar.md).

## src/Session/CampaignProfileStore.cs
JSON persistence for one named campaign profile under `user://Profiles/<name>/profile.json`,
following `ScoreStore` and `CustomPlaneStore`'s precedent: funds, owned planes with their per-gun
ammunition and per-pylon ordnance picks, each mission's record in the original's two halves
(latest attempt and best-of merge) with its failed-attempt counter, the completed-mission count,
the granted aircraft awards and the cross-mission destruction log. An owned plane names a build
in the global `user://Planes/` store rather than copying it, so deleting a profile orphans
nothing. A file saved before a field existed reads it at rest rather than failing to load. Save
format: [../formats/saved-games.md](../formats/saved-games.md).

## src/Session/CampaignPersistLog.cs
The cross-mission state log: what a campaign mission left destroyed, carried into later missions
of the SAME chapter, keyed by chapter, by the capturing mission's story position and by gamez node
index. `Capture` reads the live world through the pool the hit path damages; `Through`/`ApplyTo`
take the position of the most recent earlier mission of the chapter and carry only what positions
at or before it captured; `AnimRuntime.CarryState` re-applies the states silently, so a later hit
on a carried kill is a no-op. Only `PERSIST_LOG` defs are carried, read off the reader def rather
than the compiled twin. The prohibitions on the cut and on the replay path sit on the members they
bind. Decode: [../formats/saved-games.md](../formats/saved-games.md).

## src/Session/CampaignLoadout.cs
The bridge between a campaign profile's stored picks and a flying aircraft's fit: one
`OwnedPlane`'s ammunition and ordnance arrays as the `LoadoutChoice` a launch hands the session,
which `Loadout.Bind` then lays over the aircraft's base fit. Engine-free, and both encodings are
the campaign screens' own rather than the original's undecoded per-pylon ordnance id, so an unset
pylon is left to the base rather than written back. `PylonRow` is the one decoder of the stored
one-based ordnance value, and every screen reading the field goes through it. The same reading
serves an exported `CustomPlaneDef`'s own picks, which is how a campaign plane flown from the
Instant Action picker carries its fit. Screens: [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/Session/AirframeSwap.cs
The three `CALLBACK` codes that hand the player a different airframe in mid mission, and what each
names: the vehicle def the stats and the stock fit come from, the planes.zbd node the model is
built from, an optional special-plane award template, and whether the rebuild draws the airframe's
shipped skins instead of the pilot's paint. `AirframeSwapOrder` is one raised swap (the airframe,
the raising definition's root, and the episode owner whose rig it lands on) and
`AirframeSwapResult` reports whether an aircraft was replaced and which one left the world. A swap
is always to the player's OWN rig, and what it deliberately does not rebuild is stated at the
request record. Callback table: [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/CampaignRoster.cs
The engine-free half of the campaign roster spawner: `CampaignRosterPlan.Build` turns
`AiSkills.LoadRoster`'s blocks into one `RosterSpawnPlan` each, resolving the def behind a block
name, its `mode`, its player airframe and the chapter's `AiNet` for an authored `netids`. The
decoded fork lives here and nowhere else: `Escorts` is `mode wingman` with no authored net, `Net`
is any authored net the chapter carries, and a plan never has both. `ApplyPlan` is the
after-the-spawn half (volumes, maneuvers, gunner ratings), shared by the campaign placement and
the generator launch so the two cannot drift; `BuildGeneratorTemplate`/`ResolveGeneratorLaunch`
serve the enemy generators. Nets and modes: [../org/aiPilot.md](../org/aiPilot.md).

## src/Session/ScriptedPathVehicles.cs
One campaign mission's scripted-path vehicles. `Place` binds a spawned body to its authored
`ScriptedPath`, snapping it onto waypoint 0 facing down the first leg and freezing it there;
`Release` is what `START_TAXI` calls, and `Step` drives each follower and writes its pose onto the
body, or hands it to the caller's `setPose` where a simulation of its own owns that pose. A
finished vehicle raises its handoff callback with the speed the path left it at and leaves the
registry. The following law is `Flight/PathFollower.cs`, the route `Mech3/ScriptedPath.cs`.

## src/Session/SurfaceVehicleRuntime.cs
Builds and steps a mission's surface vehicles, the `mode ship` blocks (`patrolboat`, `t_truck`)
that have no player airframe: each is a copy of the chapter's library-root model, parented under
the world root at its authored spot with its height read off the water, and indexed on the world
runtime so the chapter's own definitions anchor on it and register its destructible pool.
`GameSession` builds one lazily for the roster phase and the generator block, and
`SessionSimulation` steps it after the generators that may launch another hull. `CollectVehicles`
offers every hull to the aim assist's vehicle list ([../org/aim-assist.md](../org/aim-assist.md)).
Read `SurfaceVehicle.cs` for what one hull then does.

## src/Session/SurfaceVehicle.cs
One built hull: no pilot, no flight model, no `FlightController`. Its movement is the scripted-path
follower's law (`Flight/PathFollower.cs`, [../org/flightModel.md](../org/flightModel.md)) over an
unbounded route, the generator's take-off run and then a lazy walk of the patrol net's edges, with
the hull's height pinned to the water it was placed on. `Patrol` is the roster and `SET_AI_NET`
assignment, `Launch` the generator's, and `Wake` the `WAKEUP_ENEMIES` arm a block's `deactivated`
waits on. Damage is the pool the chapter's definition registered on the root: a rung of the injure
ladder plays as the pool falls through its fraction, and the death leaves the parts to the death
sequence. Read `SurfaceVehicleRuntime.cs` for how one is built.

## src/Session/CutsceneController.cs
The host a story mission's intro or landings definition raises its `CALLBACK` codes to, and the
session state those codes describe: the world and objectives held, the chrome off and the view off
the aircraft, the humans out of flight with the episode owner posed on the staged `player` marker
and the runtime's range gates reading where they last flew rather than where the film puts them,
the AI parked (before any intro plays, whether or not its own data authors 913), the mid-mission
airframe swap, the re-placement, and one restore at the definition's end or at a skip. A `Node`
only so it can tick last in the frame, after the animation advance that poses `camera1`. Which
definition and which human an episode belongs to is the slot `Own` claims, not the raiser of the first code. Decode: [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/LandingApproachRuntime.cs
The mid-mission cutscene trigger: a story mission's resolved `LandingApproaches` are tested each
frame against every flying human (arming gate, speed band, attitude cone, condition volume), and
the first passing row is started as an explicit mission trigger with `CutsceneController.Own` given
both the row and the human who flew it. A row fires once per entry into its volume; an `auto` row
lights the auto-land prompt in each passing human's pane instead of starting anything. Rows whose
approach nodes are staged later are bound when those nodes appear. Read `CutsceneController.cs` for
what a started row then does, and
[../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md) for the table.

## src/Session/LadderSwitch.cs
The original's rope-ladder switch as an engine-free rule and state machine: the ladder is wanted
when the aircraft is within 45 degrees of upright and inside an active `pickups.zrd` sensor, and
the switch starts `drop_ladder` or `retract_ladder` to match, one transition at a time, holding a
transient state until the running definition's own callback settles it. `Holder` is the co-op half,
one incumbent human keeping the switch until they stop qualifying. Decode:
[../org/ladderSwitch.md](../org/ladderSwitch.md); the flown half is `LadderSwitchRuntime.cs`.

## src/Session/LadderSwitchRuntime.cs
`LadderSwitch` flown against the built world: outside a cutscene it reads each flying human's
attitude and position against the mission's pickup sensors, resolves the one holder, and starts the
ladder definitions as mission triggers, so the drop's `OBJECT_ADD_CHILD` can materialize the
library rope ladder. It takes the runtime's `CALLBACK` host slot and chains to the cutscene host
behind it, which is where the original registers the switch on each definition. Bound with the
landings trigger, story missions only. Read `LadderSwitch.cs` for the rule it flies.

## src/Session/GeneratorCycle.cs
The decoded egen launch timing law for ONE generator, pure over `Step` calls (no clock, no
randomness, no nodes) so a unit test pins it off-engine: the timer always advances, blocking holds
rather than cancels, and the individual and wave periods compose. `DoorOpen` runs the decoded
hangar-door timings inside the same step, and `HostDied` starts the wreck grace the bay keeps
launching through, after which the cycle disables permanently. `GrantCapacity` is the one way
launches arrive. Format and decode:
[../formats/mission-entities/enemy-generators.md](../formats/mission-entities/enemy-generators.md).
Read `AiGeneratorRuntime.cs` for what drives it.

## src/Session/NetTrailerTargets.cs
Resolves a patrol net's TRAILER name to a live position supplier, the session half of "an anchored
net rides its target", so `Flight/AiNetFollower` can do the arithmetic knowing nothing about
players or world nodes. `For(net)` answers only the anchored-and-named shape; the other shipped
shapes get null, which means "fly the authored coordinates". `player` is the scripted player's rig
and anything else a world node through the same lookup `ZeppelinRuntime` uses, while `OffsetOf` is
the overlay's read of the same offset. Every follower the session builds shares one instance.
Census of the shipped shapes: [../formats/ai-nets.md](../formats/ai-nets.md).

## src/Session/AiGeneratorRuntime.cs
Runs a mission's egen generators behind `--generators`: one `GeneratorCycle` per surviving
`EnemyGeneratorDef`, host altitude read live off the resolved host node, and spawns through the
handed roster callback at the origin node's live position. An air host drops its launch; a surface
host flies its own take-off path held and hands the aircraft to the flight model at the decoded
release point. Door transitions play the authored open and close anims scoped to the host,
`GrantWaveCapacity` is the one credit, and `NotifyHostDied` puts the matching cycles on the wreck
grace. Every drop, launch and door prints an `egen:` line. Decode:
[../formats/mission-entities/enemy-generators.md](../formats/mission-entities/enemy-generators.md).

## src/Session/ZeppelinRuntime.cs
Runs a mission's zeppelins behind `--zeppelins`: each `ZeppelinDef` whose world node and net
resolve has its hull switched on, is placed at its authored pose, and is flown kinematically by a
`ZeppelinMotion` over `AiNetFollower` with no `FlightController`. A hull an animation motion drives
is neither placed nor stepped, and the follower resumes from the pose that motion left it in.
`WireDamage` builds the per-part pools over the world registry, `PollDamage` owns the kill, the
engine count Instant Action wins on and the generator disable, and `CollectTargetParts` is the only
channel by which a structure becomes selectable. `SetStopPoint`, `Hold` and `Wake` are the script's
arms. Decode: [../formats/mission-entities.md](../formats/mission-entities.md).

## src/Session/ZeppelinRuntime.Cannons.cs
The broadside half of `ZeppelinRuntime`, the second file of that partial class. `WireCannons`
resolves the hardcoded `wep_28` round and each cannon's node and damage pool; per step the runtime
resolves the record's authored `targets`, gates on the authored fire range and the decoded arc,
plays the deploy and retract anims scoped to the hull, and fires real unowned rounds scattered by
the record's inaccuracy. The broadside stays off until a script arms it through the
`COMPLETED_ZEPCANNONS` seam, and a destroyed cannon thins the volley. Decode:
[../formats/mission-entities.md](../formats/mission-entities.md), "Broadside firing".

## src/Session/TurretEmplacementRuntime.cs
The world AA emplacements: the standalone `ai.zrd` turret family resolved against the built chapter
world, registered with the shared projectile pool so every player's aim assist sees them, and
stepped by `SessionSimulation` after the zeppelin runtime so a slung mount reads its ride's moved
pose. Built unconditionally with a chapter flight, as the original's own placement pass is.
`SetActivatedUnder` is the Instant Action builder's subtree write, `SetTeamUnder` the same walk for
the team a zeppelin record fans across its airship, and `WakeAll` the `--wake-turrets` stand-in.
Format and decode, including the wake ordering and the awake-by-data census:
[../formats/turrets.md](../formats/turrets.md).

## src/Session/AiVoiceRuntime.cs
Wires the combat-voice dispatcher into a running flight session, built with the rigs wherever the
world has a `WorldSounds` and ticked on the sim clock: each AI spawn is registered as a speaker on
its own `FlightController.Team`, each human rig as a damage source, and the wired sites are
subscribed (the hit-path damage tiers, the death cries, the patrol-to-pursue call-out, the
sixth-sense stun and the completed reaction). `RegisterAi` also mirrors `InPlay` into the speaker's
liveness, which is the only place the engine-free dispatcher and a controller meet. Clips play
through `WorldSounds.PlayOneShot` alone and every roll prints an `ai voice:` line. The wired and
unwired table is [../formats/combat-voice.md](../formats/combat-voice.md).

## src/Session/FlightRoster.cs
The session-owned aircraft aggregate. `BuildPlayers` commits the whole human field in ascending
player order and `SpawnAi` commits one later mission, wave or generator aircraft; both publish only
finished controllers, preserve the shared livery and spawn streams, and roll back new world nodes
and every external registration on failure. It owns the live human and AI membership views and the
target-source fan-out. `SwapPlayerAirframe` is the third commit path, a mission putting one player
into a different airframe mid-flight, and `RunSwap` the whole order a cutscene code raises.
`HumanFlightAdapter.cs` and `AiFlightAssembler.cs` are the two private assembly paths, and the
swap decode is [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/FlightRosterInputs.cs
The grouped construction facts `FlightRoster` accepts: the copied flight policy, the immutable
aircraft and archive resources, the live world services, and the human-session bindings. These
contracts keep the roster from taking all of `SessionSpec` or exposing either assembler, while
leaving its required dependencies explicit at the production seam. Read `FlightRoster.cs` next.

## src/Session/CrashRigQueue.cs
The session's queue of crash rigs whose aeroplane is already flying. A mid-flight AI introduction is
the one aircraft build that lands on a frame the player is watching, and the crash rig is the only
block of it the aeroplane does not need in order to be in the world, so `AiFlightAssembler` opens
the rig and hands it here instead of building it. `Pump`, once a frame from the session, advances
the head build by one step, while `Defer` arms the aeroplane itself so any reader of the rig builds
it in place first. `Drop` is the rollback path and `Discard` the membership clear's. Read
`WorldEffectsFactory.cs` for the build being stepped.

## src/Session/AiFlightAssembler.cs
`FlightRoster`'s private AI assembly path: authored or fallback pilot skills and maneuvers, then the
model, controller, livery, loadout and ordnance, damage visuals and optional crash runtime, then the
finished node placed. The crash runtime is OPENED rather than built wherever the caller supplied a
queue, so the launch frame carries no rig and the prop choreography plays from the queue's
completion hook. It chains the roster's durability override ahead of the enemy scale and the spawn
jitter, the engine's own order ([../org/vehicleDamage.md](../org/vehicleDamage.md)), resolves the
def's authored title for the targeting readout, and owns the one AI skills cache the session's voice
adapter borrows. Read `FlightRoster.cs` next.

## src/Session/HumanFlightAdapter.cs
`FlightRoster`'s private human-aircraft path: one `Assemble` builds the painted model,
`FlightController`, loadout and ordnance, carried turrets, HUD and instruments, damage visuals,
audio, stunt and match bindings, target selection, the authored start placement and the crash
runtime. It reads only the roster's copied policy plus the grouped aircraft, world and
human-session contracts. Player order decides the shared paint and spawn draws. An airframe swap's
captured scheme and its own build are laid over that assembly, which is the one path a bought plane
takes. `BuildDamageVisuals` is also the common first phase for AI damage. Read `FlightRoster.cs`
next.

## src/Session/EffectCatalogue.cs
The record of which authored anims are playable effects and what their defs need staged: the name
tables every effect producer must stay inside, static and engine-free. It owns the impact and death
effect names, the crash rig's own def tables and the two surface-indexed def vectors, the
damage-stage and prop-choreography menus, the destroy-def lookup, the collider overlay's surface
colour key, and the anchor-root derivation that IS `WorldEffectsFactory`'s stage source. An
unstageable anchor fails the build rather than leaving a def anchored on nothing. Every name
producer carries a producer-range tripwire in `CSVM.Tests` asserting its whole range resolves inside
these tables. Read `WorldEffectsFactory.cs` next.

## src/Session/WorldEffectsFactory.cs
Builds the two effect stages a session needs and the runtimes bound to them: the world-effects
runtime for impacts and destruction, and the per-plane crash runtime, which despite its name binds
every def that plays ON one aircraft (the crash-def vector, the destroy def, the panel damage shims
and the prop choreography). Both stages are built in pool slots sized from `data/effect_pools.json`
and handed to their runtime sealed, and both pre-warm their emitters after the bind so a first burst
finds its puffers already made. `BeginFlightCrashRuntime` opens the crash build as a handle a caller
steps a phase at a time (`CrashRigQueue.cs`) and `BuildFlightCrashRuntime` is the one-call form. The
names it binds are `EffectCatalogue.cs`; the slot mechanism is `Mech3/TemplateStage.cs`.

## src/Session/WeatherRig.cs
Loads and applies the flown mission's weather and drives each rig's skydome, whiteout, deck regime
and zone gate every frame. `ApplyZone` writes the zone's authored fog and its `SUNLIGHT` pair
through one arm per graphics mode, mirrored onto every registered extra (sun, env) pair so a cockpit
overlay crosses zones too; `ApplyFogState` is the animation runtime's `FOG_STATE` sink, writing only
the fields the event carries under the same last-writer order. Enhanced mode additionally caps a
night zone, paints the sky the zone's own fog colour and pushes the fog range out, which the session
sun's shadow distance then follows. Decode: [../org/weather.md](../org/weather.md); the authored
side is [../formats/weather.md](../formats/weather.md).

## src/Session/LensFlareRig.cs
The sun's lens flare: four screen-space sprites strung along the sun-to-screen-centre vector, plus a
full-screen white wash whose opacity is about linear in the sun's screen distance from centre. It
mirrors `WeatherRig`, constructed once per session beside it, `Build` once and `Tick` from the same
per-rig block, one instance per pane. The per-pane state lives here rather than on `PlayerRig`
because an instance is several nodes plus fade state. `--no-flare` suppresses the effect so a
capture stays usable for unrelated comparisons. The spec is measured from capture footage; method
and calibration: `git show analysis-archive:analysis/bl-165-lens-flare/FINDINGS.md`.

## src/Session/ExtractionStamp.cs
Reads the provenance stamp the extraction scripts leave at `extracted/VERSION.json` (unzbd version
line, exe hash, fork commit, schema integer) and compares its schema against this class's own
`Schema` const, in `Launcher._Ready` right after the base paths settle. At most one warning line per
boot, each naming the fix, which is to re-run the extraction. Warn rather than block, because a dev
tree holds valid extractions older than the stamp. `Behind` is the same read for a caller that
blocks instead of warning: true only when the stamp is present, carries a schema and is under what
the caller asked for, so an unstamped or unreadable tree still runs.

## src/Session/MenuAudioService.cs
The host's `IMenuAudio` over the process's playback: the music channel, the sound archive and the
briefing narration player. `BeginNarration` ducks the music and restarts the player on the resolved
stream; `EndNarration` lifts the duck and stops it, idempotent because the launchscreen calls it
every frame no briefing is showing. `Cue` resolves a semantic name through `MenuCueTable` to a file
under the cue directory the constructor was given, decodes it once and replays it from the start; an
unknown name, a missing file or a failed decode is logged once and cached as silence. Narration is
begun and ended by whichever presentation shows a briefing, so this service knows nothing of which
screen is up.

## src/Session/MenuCueTable.cs
Which wav under the extracted sound directory a semantic menu cue name resolves to: the rollover,
the click and the two edit-box keystroke sounds, the four files the original's globals script binds.
The indirection from a name to a file is the remake's, since the original's scripts name raw wavs,
and it lives here so `MenuAudioService` owns lookup alone. Engine-free, so the table is unit-tested
against the names the presentations ask for. Read `MenuAudioService.cs` next.
