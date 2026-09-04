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
class that builds no node of its own. `ResolveSpec` runs in `GameSession`'s constructor and turns
a `--campaign=<profile>:<seq>` story position into an ordinary chapter and mission; `Attach` arms
the graph once every runtime a directive can touch is up; `Step` runs the graph, the roster's
escort repair and the mission's two music duties; `BuildRoster` plans and spawns the `aiv` blocks
through `CampaignRoster.cs`. The nested `World` is the `IObjectiveWorld` implementation, where a
directive with no seam in this session is a named no-op. Mission end records the attempt, folds
the persist log into the profile and holds before the cabin. Debrief: [../org/debrief.md](../org/debrief.md).

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
nothing. Stepped only by `SessionSimulation`, after the generators that may launch another hull;
it has no independent Godot callback. `GameSession` builds one lazily (`EnsureSurfaceVehicles`) for
the roster phase and the generator block, only where a chapter world exists; `CampaignDirector`
reaches it through `RosterInputs.SpawnSurface` and `WorldInputs.SurfaceVehicles`. Observability:
one `surface: '<name>' … built at (…) water=…` line per hull. `CollectVehicles(AimCandidateSet)`
appends every built hull to the SAME candidate list the aircraft roster feeds — never the
structure or turret one, the decoded rule (docs/org/aim-assist.md "The four lists") — with an
inert hull present but not live, the shape a crashed pilot already takes; `GameSession` wires the
runtime onto every rig's `FlightController.SurfaceVehicles` through `FlightWorldBindings`, and
`StepTargeting`/`ApplyFireOutcome` read it beside `CollectAircraft` for the HUD bracket and the
gun aim assist alike. Pinned by `campaign-surface-vehicles`.

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
re-asserted, so this code writes both edges itself through `SetViewedFromOutside`: the airframe
drawn and the interior pass down while it presents, the pilot's own selected view back at the
hand-back), 11 the player out of flight (EVERY human `Held` + `Inert` + engine audio paused, and the EPISODE
OWNER's airframe posed on the staged `player` marker through `FlightController.StageAt` while that
state holds, asserted in that same instant rather than on the next tick, because the definition
raising the code goes on posing the aircraft in the same dispatch; there is exactly one marker, so
the other humans hold the coordinates the code found them at and no second staging geometry is
placed), 913/914 park and
reveal the AI (`Inert` plus `Parked`, the hold flag the original sets instead of its dead byte, so
an objective walk still counts a parked aircraft; only what this controller parked comes back), 666/667 the camera-parameter gate
(tracked, not acted on — this engine applies that profile once per rig and never on a view change),
1/10 the handoff and the in-flight systems; 951 the re-placement, which reads the staged `player`
marker's world pose and moves the EPISODE OWNER's hand-back target through
`FlightController.ResumeAt`, so a mid-mission drop or hookup leaves that pilot where its own
definition parked that node rather than where it found them, and every other human flies out of its
own coordinates; a definition raising 951 without posing that marker authors no placement, so the
code is declined and the pilot keeps the pose the episode found them at (the marker is a bodiless
stand-in for the original's own vehicle node, and unposed it reads as the world origin). ⚠ Do not
re-place a held human it did not name: `ResumeAt` on a held aircraft only
moves the hand-back target, so the mistake shows up a second later as the whole field materialising
on the drop point. 965/966/967 the mid-mission airframe swap, through the
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
WHICH HUMAN the episode belongs to rides the same slot: `Own` takes the triggering rig beside the
definition name, and `EpisodeOwner` is that rig latched when the episode took the session, or the
scripted player's (P1's) where the trigger named none, which is every mission intro and every
1P session. The owner is what the airframe swap rebuilds (`AirframeSwapOrder.Owner`, read by
`GameSession.SwapPlayerAirframe` in place of the rig list's first entry) and what `StageFlown`
puts in the runtime's node table, so a guest who flies the capture ends up in the captured
aeroplane and a hookup definition resolves that aeroplane's own hook. ⚠ The rig rides the slot and
not the raiser: a slot whose definition is no longer running loses the episode to the raiser, and
its claim on the owner with it. The owner is rewritten as the NEXT episode takes the session rather
than at the handoff, the way the code record is, so a swap's replacement stays staged afterwards.
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
It also owns when the episode owner's flown airframe reaches the runtime's node table
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
drop one. ANY human may take it, and `Skip(playerIndex)` logs which one did; the session resolves
that index from the device the event came from (a pad is bound to one seat, the keyboard is the
scripted player's) and names them on screen through `UI/SplitScreen.NoteSkip`.
`FillsWindow` is the window seam, raised true as an episode takes the session and false at the
restore, which both exits reach: a splitscreen session answers it by giving pane 1 the whole window
(`UI/SplitScreen.Fill`), since every rig camera mirrors `camera1` and four panes would show four
small copies of one shot. `BindRigs` re-raises it for an episode still playing, because a mission
intro's first code lands in the animation bootstrap, before the pane rig exists. Unbound in a
single-player session, which has one pane and nothing to collapse.
⚠ `IntroAnims` is the scope, and it is a NAME test: Instant Action's `player_setup` authors the same
nine codes, so a code test would give every mission a letterbox and a suspended world. The list is
the three definitions a story mission's start list plays as its opening movie: the two intros and
C3/M03's `cgzep_camera`, which no start list names (its start anim `calldestroy_the_cargozep`
calls it first and the zeppelin's destruction half a second later, so the destruction plays under
the camera from that one call and the host re-issues nothing). Decode:
`docs/formats/anim-definitions/cutscenes.md`.
The card's own material carries no occlusion guarantee: `BindWorld` overrides it (no depth test,
top render priority, moved into the sorted-transparent pass) so the card wins the pixel regardless
of what the episode flies between the camera and it, without touching the card's position or the
field of view `FrameBars` computes from its extent. The override cannot be a plain duplicate of
the card's own material, since every world mesh carries `SceneBuilder.BiasMaterial`'s
`ShaderMaterial`, which has no depth-test or render-priority property of its own; it is built
fresh instead, reading the source shader's `albedo_color` parameter so the card keeps its authored
colour, unshaded and both-sided so neither the light nor the source polygon's authored sidedness
changes how it reads. The `letterbox` definition only ever toggles the node's `ACTIVE` state and
its pose (see `docs/formats/anim-definitions/cutscenes.md`), never an opacity or a colour, so
replacing the material outright authors no fade the override could fight.

## src/Session/GeneratorCycle.cs
The decoded egen launch timing law for ONE generator (M4 B6 + F20), pure over `Step` calls (no
clock, no randomness, no nodes), so `CSVM.Tests/GeneratorCycleTests.cs` pins it off-engine. The
law: the timer always advances; blocking HOLDS (never cancels); `ind_period` gaps individuals
inside a wave and `ind_period + wave_period` gaps waves (they compose). `DoorOpen` runs the
decoded hardcoded door timings inside the same `Step`: open 4 s before a due spawn, minimum 4 s
open (measured on the same since-spawn timer), close early only when the next spawn is over 8 s
away; while blocked ONLY the door closes, and a disabled (host-dead) generator's door keeps its
last state (the decoded loop early-outs before any door rule). `HostDied()` starts the
`HostDeathGraceSeconds` grace (3 s, the decoded wreck timer) during which the bay still launches,
and `Step` disables it once the grace has run: ⚠ a deliberate deviation from the decoded kill-tick
disable, because C5/M04's fourth gasbag can die inside the 0.5 s between OBJECTIVE10 and the
OBJECTIVE11 credit for Miles's launch (enemy-generators.md "The host's death"). The authored
`capacity` is never read: every cycle starts at zero remaining, blocks while the wave's remainder
exceeds it, and `GrantCapacity` is the one way launches arrive (a script's `WAKEUP_GENERATOR`, an
Instant Action wave's member count, cutscene callback 800). Format and decode:
`docs/formats/mission-entities/enemy-generators.md`.

## src/Session/NetTrailerTargets.cs
Resolves a patrol net's TRAILER name to a live position supplier (`BL-377`), the session half of
"an anchored net rides its target", so `Flight/AiNetFollower` can do the arithmetic knowing nothing
about players or world nodes. `For(net)` returns a `Func<Vector3?>` only for the anchored-and-named
shape (`[nodeIndex, "name"]`, 76 nets); the other three shipped shapes get null, which means "fly
the authored coordinates". `player` is the SCRIPTED PLAYER's rig, P1's, and ⚠ deliberately not the
human field: a trailer target is ONE aircraft the graph is drawn behind, and there is no field-wide
answer to what it should trail (`Session/CampaignHumanField.cs`). Anything else is a world node through the
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
(`ZeppelinRuntime.ZeppelinKilled`, F18) calls it and the matching cycles go on the 3 s launch
grace, then disable permanently (one `egen:` line each way).
Pinned by the `zeppelin-launch` suite; the credit-after-kill shape by `generator-launch-dedg`.
Every cycle starts uncredited, and `GrantWaveCapacity(hostNode, n)` is the one credit: a script's
`WAKEUP_GENERATOR` (`--wake-generators` grants the script's whole credit at build, the logged
headless stand-in for playing up to the objective), an Instant Action wave, and cutscene callback
800, which `BindCallbackHost` answers from the runtime's `CALLBACK` host chain as five launches on
the generator named `cargozep1`, the original's literal, chaining every other code on (the
`generator-callback-credit` suite over C4/M03; bound after the ladder switch's last bind, since a
later re-bind of another chained host would loop an unanswered code between the two); the spawn
callback receives the whole `EnemyGeneratorDef`, allowing `vehicle.params` to select its AIV
template while position is still read from the live host. That selection is the mission spawner's
roster read and runs on any generator session: `GameSession.SpawnFromGenerator` spawns the matched
template through `CampaignRosterPlan.SpawnFor`, applies the rest of its slots with `ApplyPlan`,
registers the block's accent, books the launch into the campaign roster under its launch name
(`CampaignDirector.RegisterGeneratorLaunch`, so the script's `DEDG`, `TRAVELERS` and `SET_AI_*`
address it; pinned by `generator-launch-dedg`), and falls back to the `--generators=` airframe only when the
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
(arrival radius floored at `ArrivalFloorM`, a flat TUNE constant below the shortest shipped
zeppelin leg so a short leg is flown rather than skipped at once, and the only follower that
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
generator disable). That def calls `killpzep`, whose whole breakup waits on a `NODE_UNDERCOVER`
probe under the hull, so the pitch-over, the six gasbag drops and the gondola drop arrive as the
wreck sinks rather than at the kill; the gasbag splashes are sited by the `CALL_ANIMATION` arm from
each gasbag's live transform at the moment its bounce fires, which is what a template that snaps to
an absolute world point needs. The same recount raises `ZeppelinEnginesDisabled` once the LAST engine dies
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
assist sees them (`ProjectilePool.CollectTurrets`), and stepped by `SessionSimulation` after the
zeppelin runtime, so a slung mount reads its ride's moved pose. Built
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
`SpawnAi` returns before the aeroplane's crash rig exists: the roster owns a `CrashRigQueue` and the
session pumps it one step a frame through `PumpDeferredCrashRigs`. ⚠ A caller that configures
rig-owned state (`DestroyDef` is the one that bites) between the spawn and the build has to force it
first with `FlightController.EnsureCrashRig`, or the build writes over what it set.
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

## src/Session/CrashRigQueue.cs
The session's queue of crash rigs whose aeroplane is already flying. A mid-flight AI introduction is
the one aircraft build that happens on a frame the player is watching, and the crash rig is the only
block of it the aeroplane does not need in order to be in the world, so `AiFlightAssembler` opens the
rig (`WorldEffectsFactory.BeginFlightCrashRuntime`) and hands it here instead of building it. `Pump`,
called once a frame from `GameSession._Process` through `FlightRoster.PumpDeferredCrashRigs`,
advances the head build by one step; `Defer` arms the aeroplane itself
(`FlightController.ArmPendingCrashRig`) so any reader of the rig, and both damage intakes and the
ground contact, build it in place first. ⚠ Strictly one build at a time, head first: the seed is
already drawn at the request, but the emitter and node counts a run reports would otherwise depend on
frame timing. `Drop` is the rollback path (a controller being freed keeps no queued rig) and
`Discard` the membership clear's. Measured effect and what remains: `docs/verification.md` PERF-25.

## src/Session/AiFlightAssembler.cs
The roster's private AI assembly path. It prepares authored/fallback pilot skills and maneuvers,
builds the model, controller, livery, loadout/ordnance, damage visuals and optional crash runtime,
then places the finished node. The crash runtime is OPENED rather than built: the block is handed to
`CrashRigQueue` where the caller supplied one, so the launch frame carries the model, controller,
loadout, turrets, damage visuals and placement only, and `startprops` plays from the queue's
completion hook. With no queue the assembler finishes the rig in place, which is what every
off-frame caller wants. `Assemble` chains `PlaneStats.WithRosterDurability(spawn.InitHealth,
spawn.Armor)` ahead of `WithEnemyDurability`/`WithAiSpawnJitter`, the engine's own order
(docs/org/vehicleDamage.md), so a named ace's authored hull is what the scale and the jitter land
on. It also resolves the def's authored `title` into
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
`BuildFlightCrashRuntime` is the one-call form of `BeginFlightCrashRuntime`, which opens the same
build as a `CrashRigBuild` handle a caller advances with `Step()` or runs out with `Finish()`. The
phases are the build's own joints: prepare (def table, destroy def, crash root, root-name
derivation), one authored pool slot each through `StageCrashSlot`, the wreck subtree, the runtime
bind, and the emitter pre-warm. A mid-flight AI introduction takes the stepped form so the launch
frame carries only what puts the aeroplane in the world (`CrashRigQueue`, `BL-641`); every other
caller takes the one-call form. Two rules the split imposes: the crash RNG stream is drawn in
`BeginFlightCrashRuntime`, at the request rather than at the bind, so a deferred rig's seed follows
the order its aeroplanes were introduced and not the order the pumps finish; and `WireDamageStages`
is handed the runtime rather than reading `FlightController.CrashRuntime`, because that property
forces the very build it is part of.
After the bind, the build pre-warms the rig's emitters
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
choreography names remain local (the retired `analysis/death-effect-closure/`,
`git show analysis-archive:analysis/death-effect-closure/FINDINGS.md`), and the stage-call closure
excludes C4's train-anchored `b_steamtrail`. Constructed once per session (`_worldEffectsFactory`,
same lifetime as `LiveryResolver`/`SpawnPicker`) from
`(SessionSpec, Node3D worldRoot, Func<Vector3> playerPosition, EffectAmbience?, Func<IReadOnlyList<Vector3>>? playerPositions)`,
plus a settable `ScreenFlash`
sink it hands to the effects runtime — the three defs carrying an `FBFX_COLOR_FROM_TO`
(`he_ground_effect`/`ap_ground_effect`/`flak_effect`) all play there. The sink's last two arguments
are the burst point and the def's own gate radius squared, for per-pane routing; this class only
forwards them. `playerPositions` (`BL-365`; null → the single `playerPosition` alone, the
single-camera behaviour a caller with no seam — `AiCrashDefs`' test rig — still gets) is set as
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

`ApplyZone` drives the aircraft light from the zone's uncollapsed `SUNLIGHT_DIFFUSE`/`AMBIENT` in
**both** graphics modes, through one arm each, both pinned by `CSVM.Tests/SunlightEnergyTests.cs`
and wired-in by the `sun-energy` suite. The faithful arm (`FaithfulEnergies`,
`ApplyFaithfulLighting`) scales each authored scalar against the install's modal day pair and caps
it there, so the day missions keep the energies the launcher builds with (`DefaultEnergies`, 1.6
sun / 0.9 ambient, both TUNE) and only a dimmer zone moves. That cap is the faithful path's own
constraint rather than a copy of the enhanced factors: this pass has no tonemap, so an energy past
the day level clips a plane to flat white. There is deliberately **no night gate** on this arm,
because the faithful world light ignores `FOG_COLOR` too, and capping C5's plane would sink it
below its own fullbright terrain.
⚠ **The faithful ambient write reaches the Environment but the renderer ignores it**, measured:
with `AmbientLightSource.Sky` at the default full sky contribution the ambient comes off the sky
cubemap scaled by the background energy, not by `AmbientLightEnergy`, and zeroing that energy moves
no golden pixel. Whether the faithful path should stop taking its ambient from the sky is an open
rendering-design question; until it is settled the aircraft's ambient fill is the same procedural
sky at night as by day, so only the sun half of this mapping is visible.

The enhanced arm additionally neutralises `csky_world_light` to 1.0 so the fullbright
dimming does not land twice. A zone whose authored `FOG_COLOR`
is near-black is treated as a night zone (`IsNightZone`), which caps those two energies at the
install's own night pair; every day zone is untouched. `WriteSkyColor` then paints the Environment's
sky the zone's own `FOG_COLOR` as a flat panorama, so the water's specular reflects the mission's
authored sky at every zone rather than a placeholder gradient (census in
[org/weather.md](org/weather.md)).
Enhanced mode also pushes the fog out. `FogRangeFor` scales a zone's authored near/far by
`EnhancedFogRangeScale` (2.0, TUNE) and is identity in original mode; both fog-range writers
(`ApplyZone` and `ApplyFogState`) go through it, so a FOG_STATE edge cannot snap the haze back to
the authored distance mid-flight. The scaled NEAR (where the ramp starts, not the far edge of the
haze) then drives the one session sun's `DirectionalShadowMaxDistance`, with `DirectionalShadowFadeStart`
fading the last cascade out before that distance, so a shadow is gone before the ramp begins rather
than running through it. Nothing else scales: the whiteout and cloud band read CLOUD_COVER altitudes, the zone
gate is a `zone_id` cull mask with no distance in it, `WorldLights` fades on its own 900/1500 m
pair, the skydome is fitted from the camera's far plane, and `ZoneWeather.ClipFar` is parsed and
logged but reaches no consumer (the camera far plane is `Launcher`'s fixed 40000 m, past every
pushed fog far).
`RegisterExtraLighting` takes a second (sun, env) pair — the cockpit overlay's own clones — and
both lighting arms write their energies onto every registered pair beside the session sun/env, so a
zone crossing mid-flight reaches the interior pass too. `GameSession.BuildCockpitPasses` registers
in both modes for that reason. The shadow max
distance is deliberately excluded from that mirroring: a registered clone owns its own
camera-relative distance, set once at registration (`CockpitOverlay`'s 100 m far plane). Both
energy mappings and the fog-range scale are open TUNE judgements; see "Rendering: the
enhanced graphics mode" above.

## src/Session/LensFlareRig.cs
The sun's lens flare: four screen-space sprites strung along the sun→screen-centre vector at
fractions 0.50/0.90/2.0, plus a full-screen white wash whose opacity is ~linear in the sun's screen
distance from centre. Mirrors `WeatherRig` — constructed once per session beside it, `Build` once,
`Tick` from the same per-rig block of `_Process`, one instance per pane. The whole spec is measured
(`CAP-13`; method and calibration in the retired `analysis/bl-165-lens-flare/`,
`git show analysis-archive:analysis/bl-165-lens-flare/FINDINGS.md`), and the per-pane state lives on
this class rather than on `PlayerRig` because an instance is several nodes plus fade state — the
whiteout could live there only because it is a bare `ColorRect`.
`--no-flare` suppresses the effect so C2/C3 captures stay usable for unrelated comparisons.

## src/Session/ExtractionStamp.cs
Reads the provenance stamp `ExtractAssets.ps1`/`ExtractRof.ps1` leave at `extracted/VERSION.json`
(unzbd version line + exe SHA-256 + fork commit, dates, schema integer) and compares the schema
against its `Schema` const in `Launcher._Ready`, right after the base paths settle. At most ONE
warning line per boot — stale schema, missing file, or unreadable — each naming the fix (re-run
the extraction scripts). Warn, never block: the dev tree holds valid extractions predating the stamp.
Schema 2 is the first the menu layout reader (`src/UI/Menu/MenuLayout.cs`) requires: a tree
extracted before `menu_layout.json` existed is reported stale rather than read as an empty menu.
`Behind(dataRoot, need, out reason)` is the same stamp read for a caller that blocks on it rather
than warning: true only when the stamp is there, carries a schema and is under `need`, so a tree
with no stamp or an unreadable one still runs. Original's availability check is its one caller.

## src/Session/MenuAudioService.cs
The host's `IMenuAudio` over the process's playback: the music channel, the sound archive and the
briefing narration player (an `AudioStreamPlayer` child on the Master bus, formerly the
launchscreen's own). `BeginNarration(wav)` ducks the music, restarts the player on the resolved
stream (a missing one is silence, logged as `stream=no`) and counts the start for the log;
`EndNarration` lifts the duck and stops the player, idempotent since the launchscreen calls it
every frame the briefing is not showing. `Cue` resolves the name through `MenuCueTable`
(`src/Session/MenuCueTable.cs`: `menu.rollover` to `MOUSEOVER.WAV`, `menu.click` to
`MOUSECLICK.WAV`, `menu.text` and `menu.text-error` to the two edit-box wavs, the four files the
original's globals script binds) to a file under the cue directory the constructor was given (the
rof tree's `ASSETS/SOUNDS`), decodes it once through `WavFile` into an `AudioStreamWav` and replays
it from the start on a second `AudioStreamPlayer`; a name the table lacks, a missing directory or
file, or a failed decode is logged once and cached as silence. Built-in requests no cues; Original
requests all four (the rollover and the click on its buttons, the keystroke and the reject on its
two edit boxes). Narration is begun by whichever presentation shows a briefing and ended by it on
every door out; the service itself knows nothing of which screen is up.

