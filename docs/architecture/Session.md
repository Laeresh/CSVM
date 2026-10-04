# Session

The session-build clusters `GameSession` (`Launch.md`) delegates to. Five sub-namespaces, one folder each, and one page for all of them: `Session.InstantAction`, `Session.Campaign` (the director and the profile), `Session.Roster` (who flies and how each got an aeroplane), `Session.World` (the simulation step and the non-aeroplane things in the world) and `Session.Objectives` (the mission script and its rules). The module index in `docs/architecture.md` groups the entries by sub-namespace.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Session/World/SessionSimulation.cs
The session simulation: one plain-C# module owning hold admission and the exact order of flight,
combat, mission, radio, effects and match advancement. `Step(dt)` snapshots eligible AI
membership at entry, then advances incoming fire, projectiles, human aircraft, zeppelins,
emplacements, generators, surface vehicles, captured AI, landing approaches, Instant Action,
campaign, radio, smoke, tags, AI voice and Versus. Authored animation, cutscene presentation,
weather, lens flare, terrain extension and wall-time watches stay outside it, with
`GameSession`. `ISessionSimulationRuntime` is the named recording and production seam rather
than a participant registry. Read `GameSession.cs` for who owns each phase.

## src/Session/Roster/LiveryResolver.cs
Resolves which livery each player flies: the shipped paint catalog and the per-pattern
region-mask library (both lazy and cached), `PatternsForPlane`, and the per-player `SchemeFor`
pick that reads a `SessionSpec`'s `--paint=`/`--paint-color=`/`--paint-decal=` overrides.
Constructed once per session build, so a relaunch resolves against the fresh spec. With no
override every aircraft wears `DefaultPattern`, the only pattern covering all eleven airframes;
an AI spawn prefers an explicit scheme, then its own def's `paint_*`, and reaches that default
only when neither is authored. Instant Action enemies are the exception, for the reason
`InstantActionRuntime.cs` gives. Paint decode: [../org/paint.md](../org/paint.md).

## src/Session/Roster/HumanFieldPlanes.cs
Which aircraft each human in the field flies, read off a `SessionSpec`: `PlaneFor(spec, index)`
gives a player their own pick, or the last plane named when the list is shorter, and
`InstantActionOverride(spec, node)` says whether an Instant Action def's one `player_plane` binds
every human (a def read off a file) or is only player 1's pick (the launchscreen wizard's def).
Pure over the spec. The built aircraft's display name is `Flight/Airframe/PlaneRoster.cs`'s.

## src/Session/Roster/SpawnPicker.cs
Resolves each player's flight spawn: `LoadSpawnList` (which list the session walks, the mission's
`ia.json` scenario or a Dogfight launch's `net.zrd` block, the whole table when `SeatTeams` names a team, which `PlanTeams` walks by team block), `ChooseSpawnBase` (the shared
`--spawn=`-or-random list index), `ChooseSpawn` (a player's position and look-at from that list,
`objectives.json`'s `PLAYER_INIT`, or the `--spawn-at=` debug override), `StartState` (the field's
throttle and speed) and `LogSpawn`. Constructed once per session build. Also the plain
`IFlightStarts`: `ChooseStarts` loops its own `ChooseSpawn`, the placement every session flies
except a splitscreen race or a co-op campaign mission. `StartGrid` takes its anchor from here, so
this type owns it. Data: [spawns](../formats/spawns.md), [net](../formats/net-spawns.md).

## src/Session/Roster/IFlightStarts.cs
Where every pilot in a session starts: `ChooseStarts(spawns, missionZrdrPath, spawnBase,
playerCount)` returns one `FlightStart` per player, the same `(pos, lookAt)` pair
`FlightController.Setup` takes. Two implementations: `SpawnPicker`, the plain per-player walk of
the mission's spawn list, and `StartGrid`. `HumanFlightAdapter` holds the interface and resolves
the field lazily on its first `Assemble`. Answering for the whole field at once is the point of
the seam, and the constraints that keep it so sit on the interface's own members.

## src/Session/Roster/StartGrid.cs
The abreast starting grid and second `IFlightStarts`: it fans slots symmetrically across one
anchor heading, then lifts the whole field by its lowest terrain clearance. Anchor selection
stays with `SpawnPicker`, preserving mission `PLAYER_INIT`, Instant Action lists, fallbacks and
`--pos`; the heading comes from the anchor's position-to-look-at pair, so `--direction` survives.
Terrain is an injected `Func<Vector3, float?>`, production using `GameSession.GroundSampler()`,
and `startGrid.slotSpacing`/`startGrid.groundClearance` are registered TUNE values.
`GameSession` selects this grid for multiplayer campaign sessions and eligible splitscreen stunt
races; solo, Dogfight and deterministic scripted race starts keep their own placement paths.

## src/Session/InstantAction/InstantActionDirector.cs
The engine side of one Instant Action mission, behind `GameSession`'s one nullable `_iaDirector`
field: a plain sealed class that builds no node of its own, so every actor it makes parents under
the handed world root. `TryCreate` takes the wizard's def or `--ia=<path>`; `BuildActors` is the
contiguous actor phase (the chapter's first patrol net, the ace, the wingman fan and its escort
chain, every configured wave built inert at the world origin, each actor's walk seated where it spawns and kept on activation, each actor named on its `AiSpawn.PilotName`: the ace's `ace_name`, a wave's `enemy_name`, a wingman slot's fixed pilot); `Step` ticks the sequencer and activates what it returns; `WireEndConditions` routes each mode's own win signal, the lives
ledger and the wrap-up, which it reaches only through `IaWrapupSnapshot.cs`'s seam (the board `Launch/SessionBoards.cs` builds, or the menu page), snapshotting the four counters at the ending and holding the pilots' seats (not the world, not the cameras) until the hold runs out and the board is due: a win keeps the stick and loses the commands, a loss loses both, and a hull lost inside the hold spends no life and takes no pane. The decoded rules stay engine-free in
`InstantActionRuntime.cs` and `InstantActionWaves.cs`; this class owns every `ia:` log line, and `Scenario`, `IsStuntRun` and `PlayerPlaneOverride` settle the mission's spawn table, its stunt objective and the aircraft it forces on the humans.

## src/Session/Roster/SpectateHandoff.cs
The shared pane handoff for an Instant Action pilot out of lives or a campaign human whose aircraft
is lost while teammates continue. `Begin` pins the wreck through `FlightController.Spectating`,
releases the pane camera through `CameraOwned`, and creates a `SpectatorCamera` at the crash view's
last pose. It follows the first other rig still `InPlay`, or starts free when none exists, and uses
the downed pilot's own device filter. A false result means that pane already has a spectator.
Candidate and tracking lists are optional for callers without a roster or rerun path.

## src/Session/InstantAction/IaWrapupSnapshot.cs
What one ended Instant Action mission hands its wrap-up. `IaWrapupSnapshot` is the final numbers,
read once at the ending and flattened to strings and photograph records so it outlives the session;
it travels to the Original presentation's page inside `UI/Menu/MenuReturnDestination.cs`.
`IIaWrapupBoard` is the in-flight board as `InstantActionDirector` drives it: the launch builds a
`UI/Screens/IaWrapupBoard.cs` behind it, so the director never names a screen. Read
`InstantActionDirector.cs` next.

## src/Session/InstantAction/InstantActionRuntime.cs
Owns one Instant Action mission's actor set: the loaded `InstantActionDef`, the ace's spawn draw and
rating, the wingmen's fan placement, each wave's per-member draws, the objective-zeppelin selection,
and the mission's end with the decoded `WrapupHoldS` that `Advance` spends before the `WrapupDue`
cue for the board. The static, engine-free helpers `InstantActionDirector` calls are here
(`ChooseAceSpawn`, `RepresentativeRating`, `WingmanSlotFor`/`FlownWingmen`, `RandomPilotStats`/
`ResolveWaveAccentId`, `VoiceAccentIds` (voice prewarm), the zeppelin lookups, `ShotPercent`).
The end half holds no engine type and calls no `GD.*`, like `VersusMatch`, and the director owns
every log line about it. Format and decode: [../formats/instant-action.md](../formats/instant-action.md).

## src/Session/InstantAction/InstantActionWaves.cs
The decoded wave sequencer's own selection, trigger and geometry logic (`FUN_0045b9d0`): pure
state over `Start`/`Step` calls, in the shape of `GeneratorCycle`, pinned off-engine by
`CSVM.Tests/InstantActionWavesTests.cs`. `InstantActionDirector.BuildActors` builds every
configured wave's members inert at the world origin, then calls `Start()` and activates whatever
it returns; the director's `Step` ticks this once per sim step, and its `ActivateWave` resolves
the spawn draw against every live human's current position before activating each member. Format
and decode: [../formats/instant-action.md](../formats/instant-action.md).

## src/Session/Objectives/ObjectiveScript.cs
One mission's `objectives.zrd`, parsed into the typed shape `ObjectiveGraph` runs. `Load` takes a
mission zrdr scope and yields an empty script for a mission with no file, which is what every
Instant Action and multiplayer stub amounts to. Two parser rules are the original's and are what
keep the shipped data honest: blocks are read `OBJECTIVE1`, `OBJECTIVE2` and stop at the first
missing number, and every directive is found by exact name over the block's flat alternating
list, so a truncated or misspelled key lands in no field and stays dead. The four target
directives and `SET_HELP_LABEL` read `Flight/Weapons/ObjectiveTarget.cs` values, where a nested list is ONE
path keyed `parent/child`. Format and decode: [../formats/objectives.md](../formats/objectives.md).

## src/Session/Objectives/ObjectiveSites.cs
The flown mission's flagged target sites, offered to each player's `TargetPool` carrying the flag
their own record authors: `CollectFlagged` runs once per `TargetFlag`, over `targets.zrd`'s
`objective` half (the Enemy cycle) and then its `other_target` half (the Non-Aircraft cycle), the
curated list admitting a mission's chosen structures and no other destructible. A campaign director's script edits both with
`ADD_`/`REMOVE_`; the director-free constructor is what Instant Action and the multiplayer modes
take, their table unedited. World SITES only, one `Flight/Weapons/ObjectiveSite.cs` per `ObjectiveTarget.Key`, re-read every frame so
a site tracks a moving node and reads `Live` off its `DestructibleRegistry` state and whether its node is switched on; a roster block that flags itself
rides its own aeroplane. `Sides` lets a team mode label, place or hide a key. Bound by `GameSession`; [../org/targeting.md](../org/targeting.md).

## src/Session/Campaign/CampaignHumanField.cs
Engine-free objective rules over every joined human, represented by `HumanState` position, captured
group, and wreck state. `CampaignDirector.World.SnapshotHumans` is the sole producer, over every
seat: a guest flown elsewhere is read at its buffered pose, one read-behind late, and its wreck is
the one its own death report plays. `Travelers` uses the nearest human, so approaching succeeds on
the first arrival and departing on the last exit; wrecks keep reporting their positions.
`LiveInGroup` counts non-wrecked humans in a captured group and ignores cutscene inertia. The
scripted player stays a separate P1 identity for authored `player` tokens, roster leaders and
anchored net trailers. Rules are pinned by `CSVM.Tests/CampaignHumanFieldTests.cs`.

## src/Session/Campaign/CampaignDangerZones.cs
A campaign mission's own danger zones: the `dzpathN` names its `objectives.zrd` authors inside
`DANGER_ZONES_COMPLETED`, narrowed by the mission's `dzones.zrd` `disable` list and resolved
against the world's gate geometry, with `Load` answering null when a mission arms none. The same
file's `objective_numbers` and `nosnapshot` ride on each armed zone and `TryZone` answers them: that
number is the zone's completed-objective bit and names the scrapbook photograph it writes. A zone is
a route ribbon plus a material-matched pair of gate polygons, crossed by a segment passing inside
each in either order, the rule `Flight/Modes/StuntMission.cs` reads off `ia.json`'s zone list; the two differ by authoring surface, not by mechanism. Crossings are tracked per human, each with its own previous position.
Gate decode: [../formats/missions.md](../formats/missions.md).

## src/Session/Campaign/CampaignSnapshot.cs
The campaign Danger Zone photograph through `Flight/Modes/DangerZonePhotograph.cs`, written into the flying profile's
directory under the `Snap_<mission>_<objective>` name a capture row resolves against. `Window` frames the pane's
centred 4:3 part, a wider pane's flanks dropped, and the file is the 640x480 every retail photograph is. `Stage`
requests one zone's still on the crossing frame, and a worker writes it under a `.PN_` pending name when the
frame lands. `Commit` sweeps ids 10 to 31 at mission end, keeping them under their scrapbook names on a win and
deleting them on a loss (the original's two-step); a still landing after the sweep takes the verdict left for it.
`Flight/Modes/StuntCapture.cs` is the other camera: an Instant Action run has no mission slot or objective to be named
by. Rows and gate: [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/Session/Objectives/ObjectiveGraph.cs
The objectives runtime over a parsed script, pure state over `Step` calls in the shape of
`InstantActionWaves`: no Godot type, no logging, pinned off-engine by
`CSVM.Tests/ObjectiveGraphTests.cs`, with `CampaignDirector` owning every log line about it.
Implemented as decoded, shipped quirks included: the four states per objective, at most one
completion per tick from a rotating scan, dependency gating on an AWAKE target, the wake
executor's truncating early return, the nap that clears a completed flag, and the condition
families' OR, whose `DEDG` arm also widens the watched group's engagement volume on every tick it is tested. Four endings reach it, three from the script and `NotifyDockingComplete` from the
animation. `Replicate` hands the graph to another machine's: it then evaluates no condition and lets no timer expire, and changes only through the `Apply*` calls that replay the host's events, each running the same bookkeeping the host's own transition ran; a transition replayed late starts its private timer, nap and countdown that far along, and the replicated step runs those clocks pinned at zero. World seam: `IObjectiveWorld`. Decode: [../formats/objectives.md](../formats/objectives.md).

## src/Session/Campaign/CampaignDirector.cs
The engine side of one campaign mission and the sibling of `InstantActionDirector`: a plain sealed
class building no node of its own. `ResolveSpec` runs in `GameSession`'s constructor and turns a
`--campaign=<profile>:<seq>` position into a chapter and mission; `BuildRoster` plans and spawns
the `aiv` blocks through `CampaignRoster.cs`, seating each netted one's walk where it spawns, and a wake moves an off-path block by its net's trailer (`AiNetFollower.Carry`); `Attach` arms the graph once every runtime a
directive can touch is up; `BindCallbackHost` takes the `CALLBACK` slot ahead of the generator
runtime's, where 801 to 803 reactivate the lowest-numbered still-deactivated Black Hat of their
family, CM19's only launch path; `WarpDrawn` raises the world stream's `WARP_VEHICLE` pick, and `TakeWarpsFromHost` makes a guest director wait for the host's instead of drawing; 968 takes C4/M03's escorting wingman out of the world as that mission's docking film says her name, and a `TRAVELERS ... DELETE_ON_SUCCESS` takes the aircraft it counted out for good (`Removed`); `Step` runs the graph, the escort repair, the music and the danger-zone tracker, whose completed zones photograph into the profile through `CampaignSnapshot`, raise the flight's praise line through `WorldInputs.DangerZoneSpoken` and make `DangerZoneMask`, the id 18 to 30 half of the completed-objective mask. The
nested `World` is the `IObjectiveWorld`, a directive with no seam here a named no-op, and `WidenGroupEngagement` is where an awake `DEDG` reaches its group's live members; `Memento` is the picture the flying profile hangs, which the pause sheet's own slot takes; mission end records the attempt, folds the persist log into the profile and holds before the cabin behind `LeavingFade`, the ramp `UI.Screens.MissionEndFade` paints. A replicated graph's end builds the result and holds the world the same way but records nothing, since the attempt is the host's; `HasStore` says whether this director writes a profile at all, which a co-op guest's never does. A guest's `TryCreate` binds `wingman_1` from the host's co-op wingman word (`CoopWingmanOf` writes it), never from its own default profile. A human's death under the loss rule pins that aircraft `CrashIsFinal`, so neither the respawn button nor its prompt reaches the wreck. `WakeGenerators` is `--wake-generators`' whole-credit grant and `TraceObjectives` the parent-driven clock's objective trace. Debrief: [../org/debrief.md](../org/debrief.md).

## src/Session/Objectives/NetDirectorLink.cs
The objectives graph over the wire, a static pair of calls with no state of its own. `Publish`
subscribes to the host's graph and sends every event it raises (a transition, a settled completion,
the countdown's expiry, the decided ending and the mission's end) as one reliable
`DirectorTransitionMessage` stamped with the host's clock, in the order it was raised; `Follow`
replicates a guest's graph and replays each arrival through the graph's `Apply*` calls, through
`NetDirectorCatchUp` when one is given. Cutscene codes are never sent: a guest's own animation
runtime raises them from the definitions its replayed `WAKE_ANIM` starts. The replay and derive
mapping, and the code and id layout: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Session/Objectives/NetDirectorCatchUp.cs
A guest's catch-up on a director event that arrived late. The lateness is the guest's shared clock
minus the event's host stamp, never negative. It applies the event with that lateness, so the
graph's timers start as far along as the host's, and sets it on `WorldSounds` and `MissionRadio`
for the event's sounds. It then has `AnimRuntime.CatchUp` step the instances the event started.
`Enabled` off is the suite's control. What is advanced and what is not:
[../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Session/World/NetWorldLink.cs
The host-owned world over the wire, one per network session. `Admit` names each AI by its roster
ordinal: the host hooks its fire, death, hull and presence; a guest gives it a pose buffer and
claims its own seat's rounds. `StepSends` sends every host AI on the seat cadence and every zeppelin
and hull path each half second. `FollowZeppelins` puts a guest's zeppelins on those samples by
placement index, `FollowVehicles` its hulls by spawn index and `NameKey` hash plus the host's
`WARP_VEHICLE` picks, and `FollowGenerators` has it build the host's generator launches at the
host's ordinals. Pools go out off `DestructibleDamaged` at once and `DestructibleChipped` once per
seat tick, and apply through `ApplyReplicatedHealth`. `FollowVoice` relays each `AiVoiceRuntime.Raised` by ordinal as world event 7, which a guest hands to `TakeRaise`, and feeds a guest's hull events to `TakeHull`. Layouts: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Session/World/VersusDirector.cs
The engine side of one Dogfight (`--vs`), split screen or over the wire, the sibling of `InstantActionDirector` and `CampaignDirector`: `TryCreate` builds the `VersusMatch`, teamed off the lobby's seats, ahead of the roster, and `Wire` feeds it every seat's Downed report (reported to the host on the wire; scored here in a local match, which posts the death lines too), sets each pilot's crash-cam respawn (a bot seat's never waits for Fire Guns, whatever Auto Respawn says) and builds the `VersusSpawnRotation` of the host or the local machine, which relaxes its one-living-seat-per-point rule for a field larger than the table rather than failing. Having a wire and having a seat roster are separate questions: a local match with bots holds a roster and no wire, and decides its returns, scores and rematch itself.
`WireSpawns` puts placement on the wire under one rule: the OPENING spawn is the shared seed's walk over the mission table and crosses no wire, while every later return is GRANTED by the host's single rotation, a table entry every peer applies through the call the owner would have made (`SpawnsTaken`, `SpawnEntries`). `WireMatchState` makes the host the only writer of the match: the clock, both limits and the ending go out as one reliable message, change-driven plus a `MatchStateCadence` tick that carries the host clock into every guest's `NetClockSlew`, and a guest hands its match over, its rematch the host's (`RematchIsTheHosts`). `ScoreDeath` is the host's one scoring site; it sends the scores, then the death notice every machine posts once, and the ending only after the scores that settled the round. The scoreboard itself is never sent.
`StepMatch` advances the clock, holds a pilot out of lives spectating (`VersusMatch.OutOfLives`) and posts its lives line; the session steps the flags and the rearm bases ahead of it. `WireFlags`, `WireZeppelinVersus` and `WireRearmBases` open `FlagRuntime.cs`, `ZeppelinVersusRuntime.cs` (whose return is a `SpawnAtMessage` and whose Restart leaves for the lobby rather than rerunning on burnt hulls) and `RearmRuntime.cs`. Decode: [../org/multiplayer-scoring.md](../org/multiplayer-scoring.md).

## src/Session/World/FlagRuntime.cs
Capture the Flag in a network match, built by `VersusDirector.WireFlags` for a `--ctf` launch: one
`FlagMatch` flag per lobby team whose `cs_flag_n` the mission world holds. Each machine checks its
own seats and asks the host (`FlagRequestMessage`); the host decides, scores through
`VersusMatch.AddScore` and sends its `FlagTableMessage`. Every machine moves the props from the
changes, hangs the carried flag under the holder's `cf_light`, speaks the `snd_CTF*` lines and posts
the flag rows. A death, a drop or `Eject` (the console's `ejectflag`, relayed by the host) floats a
flag everywhere, and the host sends it home when the throw runs out. `SideOf` labels the three
markers, and the carrier carries its tag. Decode: [../org/multiplayer-ctf.md](../org/multiplayer-ctf.md).

## src/Session/World/ZeppelinVersusRuntime.cs
Zeppelin vs Zeppelin in a network match, built by `VersusDirector.WireZeppelinVersus` for a `--zvz`
launch over the `MP3` world's `ZeppelinRuntime`: hull 0 goes to the first side's lobby team and
hull 1 to the second (`SetTeam`), both broadsides engaged with rounds named for their hull. Off
`PartDestroyed` the host scores each gas bag to the seat whose hit killed it, and every machine
speaks the gas bag line. `ZeppelinKilled` ends the match on the host, a guest taking the winner
from the `0x17` state; `Rules` names the side a broadside's kill goes to. `RespawnPoint` is
the host's return, sent as `SpawnAtMessage`, and `SideOf` labels each hull's marker and its rearm
base's. Decode: [../org/multiplayer-zvz.md](../org/multiplayer-zvz.md).

## src/Session/World/RearmRuntime.cs
The multiplayer rearm bases in any Dogfight, built by `VersusDirector.WireRearmBases`: the world's
`rearm_node_n` serving lobby team `n`, or in Zeppelin vs Zeppelin each hull's `zep_rearm_node_n`
serving its side while the hull lives. Each machine steps only the seats it flies through
`RearmBases`, and on entry calls `FlightController.Rearm` and posts "Rearmed!" in the seat's own
pane. The restored ledger reaches every other machine in the next `0x40` damage report, where a
hurt copy reading full again takes its stages off. Decode: [../org/multiplayer-rearm.md](../org/multiplayer-rearm.md).

## src/Session/World/NetCutsceneLink.cs
The cutscene skip over the wire, one per network session with a cutscene host. On the host it
broadcasts every skip `CutsceneController.Skipped` reports, its own and the guests' asks it took,
and takes an ask only from the machine that owns the seat. On a guest it binds `SkipAsked`, so a
skip input sends the ask and ends nothing locally, and hands each skip the host sends to
`TakeSkip`, which ends only the episode the key and ordinal name. Layout and episode naming:
[../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Session/World/NetChatLink.cs
The in-flight chat over the wire, one per network session. `Open` puts the entry under a seat's
prompt, `Submit` posts the echo and sends the line: a guest's to the host, which forwards an
all-chat to every machine and a team line only to machines flying a seat on the typist's lobby
team, once each. `TakeKey` is the typing seat's keys, and `HoldsKeyboard` keeps that seat's
flight keys idle until every key pressed into a line is up. A line whose first word is the original
console's `ejectflag` is no chat: it runs `EjectFlag` for the typist. It owns the machine's
`Flight/Hud/FlightChat.cs`. Layout: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Session/Campaign/NetPositionalStartLink.cs
The landing rows, the ladder switch and the code-raising range gates over the wire, one per network
session with any of them bound. On the host it sends each row start, holder change and gate verdict
from `LandingApproachRuntime.Started`, `LadderSwitchRuntime.HolderChanged` and
`RangeGateAuthority.Decided`, and gives a guest's copy the held auto-land button that seat's own
machine reports. On a guest it replicates both runtimes, starts the row the host named for that
seat's rig, takes the host's holder, answers each gate with the host's verdict, and `Step` reports
its own seats' held button when it changes. Kinds and replay mapping: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Session/Campaign/CampaignProgression.cs
The campaign's progression rules over a profile: recording one mission attempt with the original's
best-of merge, raising the position, and granting the aircraft awards. The position is a single
monotonic integer that only a completed primary objective raises, because `cm_sequence.zrd` is a
flat list with no predicate and no alternates. `Record` also works out what a mission pays, since
the reward table is gated on what the profile has already banked, and reports it back through
`MissionRecorded`. The four-attempt skip offer is raised here and answered by `AcceptSkip` on the
screen that shows the result. `AirframeCount` fixes the kill tallies' width. Reward table:
[../org/hangar.md](../org/hangar.md).

## src/Session/Campaign/CampaignMementos.cs
The pictures a campaign profile may hang on its cabin wall: the executable's own award table (name,
the mission that awards it and which bit of that mission's merged objective mask admits it) and the
rule that reads a profile's records to say which rows it holds. Seven rows carry no mission and are
held from the first frame, so a chooser is never empty; `Current` answers what an absent or unknown
stored name draws as, and `Bitmap` is the truncation the drawn file name takes. `BitmapFor` is the
one name the cabin wall, the pause sheet and the campaign load screen all draw, so a chosen picture
cannot reach one and miss another. The table, its addresses and the row it can never admit:
[../org/pause-screen.md](../org/pause-screen.md).

## src/Session/Campaign/CampaignProfileStore.cs
JSON persistence for one campaign profile under `user://Profiles/<folder>/profile.json`, after
`ScoreStore` and `CustomPlaneStore`: funds, owned planes with their ammunition and ordnance picks,
each mission's latest and best-of record with its failed-attempt counter, the completed-mission
count, granted awards, the chosen memento and the destruction log. An owned plane names a build in
`user://Planes/` rather than copying it. A field a file predates reads at rest. The folder is the
identity: `Save` writes a loaded profile back to its `Folder`, and a file whose `"name"` points at
another folder (a copy, a hand rename) is read under the folder's name with a warning. `ForSession`
is a launch's store, `--profiles=` or `user://Profiles/`. Format: [../formats/saved-games.md](../formats/saved-games.md).

## src/Session/Campaign/CoopPlanePool.cs
The rule that settles a co-op campaign sortie's own plane picks, for splitscreen seats and network
guests alike. `Resolve` runs the picks in seat order: a seat keeps a pick no earlier seat holds,
compared by name, and otherwise takes the first free plane, else `Stock` (the shared stock
Devastator, `StockAirframe`), so on a same-moment clash the earlier seat wins. `Wingman` comes after
every human: its saved plane while no seat holds it, else the first free plane, else `Stock`.
Engine-free: the host settles, and the wire carries only the answer. Wire: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).
Coverage: `CSVM.Tests/CoopPlanePoolTests.cs`.

## src/Session/Campaign/ChapterCinema.cs
Which film plays before a campaign chapter, and the one handoff to the passenger cabin that
follows it. The chapter is `seq / 5 + 1` over the profile's own position, so no screen passes a
chapter number in, and chapter N plays `chapN.mpg`. `CampaignCabinPage.MapPinCount` reads the same
story chapter for the cabin map's pins; `CampaignSequence.Chapter` is a different number, the world
folder. Its skip set is its own `Keys`. Playing is a `Video/CinemaHandoff.cs` `CinemaPlay` the caller supplies, `Launcher.PlayCinema`
being what it is handed, which leaves the film to `UI/Screens/CinemaScreen.cs` and keeps every decision here
testable with no engine present; the cabin opens through that file's `Once`. `Launcher` holds the process's one instance and hands it to `Menu/CampaignFeature.cs`, which is how both presentations' cabin doors reach it (`UI/Campaign/CampaignFlow.cs`, `UI/Menu/Original/OriginalCampaignScreen.cs`). A co-op guest's `Play` plays the chapter its host named and leaves the latch alone, and `Stop` (`Launcher.StopCinema`) ends it when the host's film ends. Films: [../formats/cinemas.md](../formats/cinemas.md).

## src/Session/Campaign/ClosingCinema.cs
Whether the campaign's closing film plays before the scrapbook a flown mission opens, and the one
handoff to that book. The gate is the mission just flown, its result and its story position: a win
on the campaign's last mission plays the film, first flight and replay alike, and any other ending
reaches the book with no film, which is what the original's own script does when its gate callback
answers false. Nothing is latched and completion state decides nothing, so the flown result travels
with the menu return (`Menu/MenuReturnDestination.cs`). The film is the `FinalCinema` layout row's
name and its `Keys` are Escape and the left mouse alone, narrower than `ChapterCinema.cs`'s on
purpose; playing is a `Video/CinemaHandoff.cs` `CinemaPlay` (`Launcher.PlayCinema`) whose `Once` opens the book, and `Launcher` holds the one instance and hands it to `Menu/CampaignFeature.cs`. A co-op guest's `Play` has no gate, since the film is its host's, and `Stop` ends it with the host's. Films: [../formats/cinemas.md](../formats/cinemas.md).

## src/Session/Campaign/CampaignPersistLog.cs
The cross-mission state log: what a campaign mission left destroyed, carried into later missions
of the SAME chapter, keyed by chapter, by the capturing mission's story position and by gamez node
index. `Capture` reads the live world through the pool the hit path damages; `Through`/`ApplyTo`
take the position of the most recent earlier mission of the chapter and carry only what positions
at or before it captured; `AnimRuntime.CarryState` re-applies the states silently, so a later hit
on a carried kill is a no-op. Only `PERSIST_LOG` defs are carried; the readers are where that flag
is authored, and `AnimProgram` hands it to whichever def its dedup keeps. The prohibitions on the cut and on the replay path sit on the members they
bind. Decode: [../formats/saved-games.md](../formats/saved-games.md).

## src/Session/Campaign/CampaignLoadout.cs
The bridge between a campaign profile's stored picks and a flying aircraft's fit: one `OwnedPlane`'s
ammunition and ordnance arrays as the `LoadoutChoice` a launch hands the session, which
`Loadout.Bind` lays over the aircraft's base fit. Engine-free, and both encodings are the campaign
screens' own rather than the original's undecoded per-pylon ordnance id, so an unset pylon is left to
the base. `PylonRow` is the one decoder of the stored one-based value; a cell travels as a cell (a
wing and an ordinal), never a pylon number, since which pylon a wing's second cell is depends on what
the aircraft hangs. The same reading serves an exported `CustomPlaneDef`, which is how a campaign
plane flown from Instant Action carries its fit, and a co-op seat's `Net.CoopFit` (`For`, `FitOf`). [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/Session/Roster/AirframeSwap.cs
The three `CALLBACK` codes that hand the player a different airframe in mid mission, and what each
names: the def, the planes.zbd node, an optional award template, and whether the rebuild draws
shipped skins. `AirframeSwapOrder` is one raised swap (airframe, raising root, episode owner) and
`AirframeSwapResult` says whether an aircraft was replaced and which one left the world. A swap is
always to the player's OWN rig; what it does not rebuild is stated at the request record.
`AirframeHandover` splits what a capture carries: the captured livery rides 966 and 967 (966 falls
back to `bhatwarhawk`'s scheme with no live rig), its damage and roster group 967 alone. Callback table: [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/Campaign/CampaignRoster.cs
The engine-free half of the campaign roster spawner: `CampaignRosterPlan.Build` turns
`AiSkills.LoadRoster`'s blocks into one `RosterSpawnPlan` each, resolving the def behind a block
name, its `mode`, its player airframe and the chapter's `AiNet` for an authored `netids`. The
decoded fork lives here and nowhere else: `Escorts` is `mode wingman` with no authored net, `Net`
is any authored net the chapter carries, and a plan never has both. `ApplyPlan` is the
after-the-spawn half (volumes, maneuvers, gunner ratings), shared by the campaign placement and
the generator launch so the two cannot drift; `BuildGeneratorTemplate`/`ResolveGeneratorLaunch`
serve the enemy generators, and `WidenForDedg` is the one write an awake `DEDG` makes to a member's activation radius. Nets and modes: [../org/aiPilot.md](../org/aiPilot.md).

## src/Session/World/ScriptedPathVehicles.cs
One campaign mission's scripted-path vehicles. `Place` binds a spawned body to its authored
`ScriptedPath`, snapping it onto waypoint 0 facing down the first leg and freezing it there;
`Release` is what `START_TAXI` calls, and `Step` drives each follower and writes its pose onto the
body, or hands it to the caller's `setPose` where a simulation of its own owns that pose. A
finished vehicle raises its handoff callback with the speed the path left it at and leaves the
registry. The following law is `Flight/Ai/PathFollower.cs`, the route `Mech3/ScriptedPath.cs`.

## src/Session/World/SurfaceVehicleRuntime.cs
Builds and steps a mission's surface vehicles, the `mode ship` blocks (`patrolboat`, `t_truck`)
with no player airframe: each is a copy of the chapter's library-root model under the world root
at its authored spot, its height read off the water, indexed on the world runtime so the chapter's
definitions anchor on it and register its destructible pool. `GameSession` builds one lazily for
the roster phase and the generator block; `SessionSimulation` steps it after the generators that
may launch another hull. A plane reads it as `Flight/Weapons/ISurfaceVehicles.cs`. `CollectVehicles` offers every hull to the aim assist's vehicle list
([../org/aim-assist.md](../org/aim-assist.md)); `Projectiles`/`Weapons`/`Voices` arm and voice its gun on the attack radius `AttackRadiusOf` resolves in the engine's own write order, the block's and net's slot over the def's `attack` over the decoded 400 m default and never a zero reach ([../org/aiPilot.md](../org/aiPilot.md)); `Strings` names it, slot 20 else the def's title into `MarkerName`.
A guest's `Replicate`d runtime puts every hull it holds or spawns on the host's samples. Read `Flight/Ai/SurfaceVehicle.cs` next.

## src/Session/World/CutsceneController.cs
The host a story mission's intro or landings definition raises its `CALLBACK` codes to, and the
session state those codes describe: the world and objectives held, the chrome off and the view off
the aircraft, the humans out of flight with the episode owner posed on the staged `player` marker
and the runtime's range gates reading where they last flew rather than where the film puts them,
the AI parked (at the start of a mission of any type, and again before any intro plays whether or not its own data authors 913, lifted by code 914 from whichever definition the mission bootstraps), the mid-mission airframe swap, the re-placement, the clearing of every round still in flight, and one restore at the definition's end or at a skip, refused once the mission has ended under the episode so the leaving fade keeps the film's shot. It also owns the held-input fast-forward (`Mech3/Anim/CutsceneFastForward.cs`), scoped to the episode's call closure and offered only where no skip is armed. A `Node`
only so it can tick last in the frame, after the animation advance that poses `camera1`. Which
definition and which human an episode belongs to is the slot `Own` claims, not the raiser of the first code; an episode nobody claimed belongs to the scripted player, the host's first seat on a network guest. That human's own airframe is staged into the animation runtime's node table as the episode takes the session, so a hookup resolves the landing pilot's aeroplane rather than the seat that was staged at the bind. Decode: [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/Campaign/LandingApproachRuntime.cs
The mid-mission cutscene trigger: a story mission's resolved `LandingApproaches` are tested each
frame against every flying human (arming gate, speed band, attitude cone, condition volume), and
the first passing row is started as an explicit mission trigger with `CutsceneController.Own` given
both the row and the human who flew it. A row fires once per entry into its volume; an `auto` row
lights the auto-land prompt in each passing human's pane instead of starting anything. Rows staged
later bind when their nodes appear. `Started` reports each start; a `Replicate`d trigger on a
network guest starts only what `StartRow` names, still offering its own humans the prompt and
recording a held button (`Pressing`). Next: `CutsceneController.cs`, [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/Campaign/LadderSwitch.cs
The original's rope-ladder switch as an engine-free rule and state machine: the ladder is wanted
when the aircraft is within 45 degrees of upright and inside an active `pickups.zrd` sensor, and
the switch starts `drop_ladder` or `retract_ladder` to match, one transition at a time, holding a
transient state until the running definition's own callback settles it. `Holder` is the co-op half,
one incumbent human keeping the switch until they stop qualifying. Decode:
[../org/ladderSwitch.md](../org/ladderSwitch.md); the flown half is `LadderSwitchRuntime.cs`.

## src/Session/Campaign/LadderSwitchRuntime.cs
`LadderSwitch` flown against the built world: outside a cutscene it reads each flying human's
attitude and position against the mission's pickup sensors, resolves the one holder, and starts the
ladder definitions as mission triggers, so the drop's `OBJECT_ADD_CHILD` can materialize the
library rope ladder. It takes the runtime's `CALLBACK` host slot and chains to the cutscene host
behind it, which is where the original registers the switch on each definition. Bound with the
landings trigger, story missions only. `HolderChanged` reports a new holder; a `Replicate`d switch
steps on the holder `TakeHolder` hands it. Read `LadderSwitch.cs` for the rule it flies.

## src/Session/Roster/GeneratorCycle.cs
The decoded egen launch timing law for ONE generator, pure over `Step` calls (no clock, no
randomness, no nodes) so a unit test pins it off-engine: the timer always advances, blocking holds
rather than cancels, and the individual and wave periods compose. `DoorOpen` runs the decoded
hangar-door timings and holds a spawn behind a door that has to travel; `HostDied` starts the wreck grace
the bay keeps launching through, after which the cycle disables permanently. `GrantCapacity` is
the one way launches arrive. Format and decode:
[../formats/mission-entities/enemy-generators.md](../formats/mission-entities/enemy-generators.md).
Read `AiGeneratorRuntime.cs` for what drives it.

## src/Session/Objectives/NetTrailerTargets.cs
Resolves a patrol net's TRAILER name to a live position supplier, the session half of "an anchored
net rides its target", so `Flight/Ai/AiNetFollower` can do the arithmetic knowing nothing about
players or world nodes. `For(net)` answers only the anchored-and-named shape; the other shipped
shapes get null, which means "fly the authored coordinates". `player` is the scripted player's rig
and anything else a world node through the same lookup `ZeppelinRuntime` uses, while `OffsetOf` is
the overlay's read of the same offset. Every follower the session builds shares one instance.
Census of the shipped shapes: [../formats/ai-nets.md](../formats/ai-nets.md).

## src/Session/Roster/AiGeneratorRuntime.cs
Runs a mission's egen generators behind `--generators`: one `GeneratorCycle` per surviving
`EnemyGeneratorDef`, host altitude read live off the resolved host node, and spawns through the
handed roster callback at the origin node's live position. An air host drops its launch; a surface
host flies its own take-off path held to the decoded release point. Doors play the authored anims
scoped to the host, `GrantWaveCapacity` is the one credit, and `NotifyHostDied` starts the wreck
grace. `AircraftLaunched` reports each aircraft launch; after `Replicate` the spawner refuses the
cycles' own and `LaunchReplicated` builds the host's. Every drop, launch and door prints an `egen:`
line. Decode: [../formats/mission-entities/enemy-generators.md](../formats/mission-entities/enemy-generators.md).

## src/Session/World/ZeppelinRuntime.cs
Runs a mission's zeppelins behind `--zeppelins`: a `ZeppelinDef` whose node and net resolve is
switched on, placed at its authored pose and flown by `ZeppelinMotion`; an animation-driven hull is
neither placed nor stepped. `WireDamage` builds the part pools, `PollDamage` owns the kill (and
`PartDestroyed`, each part's with its killing shooter), the Instant Action engine count and the
generator disable; `CollectTargetParts` makes a structure selectable under `TargetPool`'s torpedo
gate. A def with no net is held; `--zep=` grafts one on. Script arms: `SetStopPoint`, `Hold`, `Wake`, `SetNet` and `SetTeam`. Networked, the host sends
`TryReadPath` and a guest's `Replicate` gives each hull a `ZeppelinReplica` that `TakePath` feeds in
place of the follower. Decode: [../formats/mission-entities.md](../formats/mission-entities.md).

## src/Session/World/ZeppelinRuntime.Cannons.cs
The broadside half of `ZeppelinRuntime`, the second file of that partial class. `WireCannons`
resolves the hardcoded `wep_28` round and each cannon's node and damage pool; per step the runtime
resolves the record's authored `targets`, gates on the authored fire range and the decoded arc,
plays the deploy and retract anims scoped to the hull, and fires real rounds scattered by the
record's inaccuracy, unowned unless `NamesBroadsideRounds` names them for their hull. The broadside
stays off until a script arms it through the `COMPLETED_ZEPCANNONS` seam or Zeppelin vs Zeppelin
arms both hulls, and a destroyed cannon thins the volley. Decode:
[../formats/mission-entities.md](../formats/mission-entities.md), "Broadside firing".

## src/Session/World/TurretEmplacementRuntime.cs
The world AA emplacements: the standalone `ai.zrd` turret family resolved against the built chapter
world, registered with the shared projectile pool so every player's aim assist sees them, and
stepped by `SessionSimulation` after the zeppelin runtime so a slung mount reads its ride's moved
pose. Built unconditionally with a chapter flight, as the original's own placement pass is.
`SetActivatedUnder` is the Instant Action builder's subtree write, `SetTeamUnder` the same walk for
the team a zeppelin record fans across its airship, and `WakeAll` the `--wake-turrets` stand-in.
Format and decode, including the wake ordering and the awake-by-data census:
[../formats/turrets.md](../formats/turrets.md).

## src/Session/Roster/AiVoiceRuntime.cs
Wires the combat-voice dispatcher into a running flight session, built with the rigs wherever the world has a `WorldSounds`
and ticked on the sim clock: an accented AI spawn is registered as a speaker on its own `FlightController.Team` with the pilot its accent's pool deals it in registration order (so every network end deals the same one), each human
rig as a damage source whose rounds draw the ally distress out of a teammate they strike (a network seat's rig, flown here through `RegisterPlayer` or elsewhere through `RegisterRemotePlayer`, also speaks as the pilot its player chose, the lines each end derives for it), and the mode machine and death report of EVERY aircraft handed over are watched, accented or not,
because the bearing call-out, the taunt, the killer's gloat and the flight's enemy-down call on a player kill are spoken by an aircraft other than the one the event reached.
An evade episode's end speaks the successful-shake taunt, and only once the machine's own evade flag has cleared.
`Step` raises the rest of the pursue path for every AI in play whose gunner holds a hostile human, at the slot cooldown's own interval rather than on a mode edge, so a commit inside the mute window is not lost: the taunt the pursuer's own nose against that human picks, its `WA-Attack`, and the flight's bearing call-out; it also raises the taunt of every hostile player flown elsewhere within range of a local one.
`RegisterAi` also mirrors `InPlay` into the speaker's liveness, the only place the engine-free dispatcher and a controller meet. A replicated AI (`RemoteOwned`) raises nothing here: the raises that read the flying end's own state fire `Raised` for the network link to relay, and a guest replays them through `TakeRaise` and derives the DI tiers through `TakeHull`.
Lines play flat through `MissionRadio.Speak` (the queue the objective callouts share) with the speaker id that answers the "already talking" hook; every roll and first "no clip" refusal prints an `ai voice:` line. `WatchTurrets` adds the one non-aircraft source, a gunner's acquisition of a human player off the shared `ProjectilePool`, broadcast on that player's team, and `DangerZoneCompleted` the other, the flight's praise for a player's run through the gates, which the campaign's own completion report raises. [../formats/combat-voice.md](../formats/combat-voice.md).

## src/Session/Roster/FlightRoster.cs
The session-owned aircraft aggregate. `BuildPlayers` commits the whole human field in ascending player order and `SpawnAi` commits one
later mission, wave or generator aircraft; both publish only finished controllers, preserve the shared livery and spawn streams, and roll
back new nodes and registrations on failure. It owns the live human and AI membership views, the target-source fan-out, and
`VehicleDowned`, the AI death report the HUD kill line is fed from. `SwapPlayerAirframe` is the third commit path, a mission putting one
player into a different airframe mid-flight, and `RunSwap` the whole order a cutscene code raises. The loading screen builds the coming
waves' aeroplanes through `OrderWaveAirframes` and `BuildOrderedAirframe`, and `PumpDeferredCrashRigs` takes one more off the owed list
per quiet frame, never on a frame a crash rig or a launch already builds on. A network bot seat is a member of the human field, not of the AI list. `HumanFlightAdapter.cs`, `AiFlightAssembler.cs` and
`AiAirframePool.cs` are the private assembly paths; the swap decode is [../formats/anim-definitions/cutscenes.md](../formats/anim-definitions/cutscenes.md).

## src/Session/Roster/FlightRosterInputs.cs
The grouped construction facts `FlightRoster` accepts: the copied flight policy, the immutable
aircraft and archive resources, the live world services, and the human-session bindings. These
contracts keep the roster from taking all of `SessionSpec` or exposing either assembler, while
leaving its required dependencies explicit at the production seam. `HumanRosterBindings.RigCount`
counts SEATS, guests on other machines included, and `NetSeats` is that roster indexed by seat,
empty outside a network match, and `SeatFit` is a co-op seat's loadout. `SoloStuntBoard` is how a
solo stunt run gets its results board without the roster naming a screen. Read `FlightRoster.cs` next.

## src/Session/Roster/CrashRigQueue.cs
The session's queue of crash rigs whose aeroplane is already flying. A mid-flight AI introduction is
the one aircraft build that lands on a frame the player is watching, and the crash rig is the only
block of it the aeroplane does not need in order to be in the world, so `AiFlightAssembler` opens
the rig and hands it here instead of building it. `Pump`, once a frame from the session, advances
the head build by one step, while `Defer` arms the aeroplane itself so any reader of the rig builds
it in place first. `Drop` is the rollback path and `Discard` the membership clear's. Read
`WorldEffectsFactory.cs` for the build being stepped.

## src/Session/Roster/AiAirframePool.cs
The wave aeroplanes a mission is going to need, built before it starts and held outside the tree, so
a launch costs the bind, the loadout and the tree insert instead of the painted model, its collision
hulls and its animators. One slot per airframe and livery, `Order`ed off the roster blocks the waves
launch from, `BuildOne` per loading-screen step and per quiet frame in play, `Claim` at the launch,
`Discard` at teardown. A claim on an empty ordered slot counts a miss and the caller builds in place,
which is what every spawn did before the pool; `Claims`/`Misses`/`Owed`/`Ready` are what the hitch
suite reads. It holds no spawn index, the jitter draws its own at the launch, so nothing here moves
the spawn streams. Read `AiFlightAssembler.cs` for the build it calls.

## src/Session/Roster/AiFlightAssembler.cs
`FlightRoster`'s private AI assembly path: authored or fallback pilot skills and maneuvers, then the airframe, controller, livery,
loadout (a spawn's fit through `Loadout.BindWingman` ahead of the AI def's `weapons`) and ordnance, damage visuals, the positional engine and weapon voices that stand in for the own-ship `FlightAudio`, the
optional crash runtime, then the node placed. The airframe (painted model, hulls, prop, wing-light and surface animators) is CLAIMED
from `AiAirframePool.cs` where one is ready and built in place otherwise, over one shared `PlaneCollider` per airframe and one shared
`PlanePainter` per airframe and livery (PERF-22). That runtime is OPENED rather than built wherever the caller supplied a queue, so the
launch frame carries no rig and the prop choreography plays from the queue's completion hook. It chains the durability override ahead of
the enemy scale and the spawn jitter, the engine's own order ([../org/vehicleDamage.md](../org/vehicleDamage.md)), resolves the readout's
title, stamps the block's objective marker, and owns the AI skills cache. The danger-zone look's daredevil chance is drawn from that cache here and zeroed for anything but a `jet`, the original's own class gate on the arm that rolls it. `ArmSeatPilot` arms a network bot seat's pilot (gunner, ordnance, mode machine and the airframe's ranges) on a personality rolled off `Rng.Bots` and shifted by the seat's tier (`BotSeats.cs`), its gunner preferring no player so a Dogfight ranks a person and a bot alike, for the human path that builds the seat's aeroplane, so no tier reaches its hull. Read `FlightRoster.cs` next.

## src/Session/Roster/BotSeats.cs
What a network bot seat is once its host seats it, engine-free. `Resolve` turns the command line's
bot field into seats: a Random plane drawn over the eleven stock airframes, so the roster carries a
real one, and each unnamed bot a callsign drawn once from `CallsignPool`, the message table's
character names cut to the Callsign box's 12 characters, skipping every name a seat already holds.
`Personality` is Instant Action's five-row roll and `Ratings` shifts it by the seat's tier, the whole
of a tier's effect. Only the host calls it; a guest reads the roster. Decodes:
[../formats/missions.md](../formats/missions.md#message-table), [../formats/instant-action.md](../formats/instant-action.md).
Read `AiFlightAssembler.cs` next.

## src/Session/Roster/HumanFlightAdapter.cs
`FlightRoster`'s private seat path: `Assemble` builds the painted model, `FlightController`, loadout and ordnance, carried turrets, HUD and instruments, damage visuals, audio, stunt and match bindings, target selection, the start placement, the Danger Zone eye, the crash runtime, and last
the `UI.Boards.SplitScreen.SeatAirframe` stamp that keeps the model out of this pilot's spyglass disc and the cockpit-hidden body out of this pilot's pane alone. A seat of the bindings' `NetSeats` without a pane
(flown elsewhere, or a host's bot) takes the aeroplane, paint, loadout, spawn slot and score row, only one flown elsewhere is built with the `RemotePoseBuffer` that IS its ownership, and
both skip every pane, HUD, camera, listener, pad and pause key, the roster's airframe pick and a co-op seat's `SeatFit` beating this machine's launch flags. A bot seat is AI-piloted
(`IsHumanPiloted` false) on every machine, so each plays its hits, shakes and `ai_crash_*` wreck alike, and on the host it flies an `AiPilot` armed by `AiFlightAssembler.ArmSeatPilot`
under its seat index as `PlayerIndex`, its far-field plant measured against every person's seat rather than this machine's panes. Every later return of that aeroplane, through `FlightController.Respawned`, starts the pilot over with `AiPilot.ResetForSpawn` on its new placement's course. It stays in the seat list, never in the roster's AI, so the world link never admits it and the seat paths carry its state, fire, hits and death.
It reads only the roster's copied policy plus the grouped aircraft, world and human-session contracts; player order decides the paint and spawn draws. An airframe swap lays its captured scheme and own build over
that assembly, the one path a bought plane takes. An Instant Action racer takes no `Race`, so it flies on through the ending's hold, and `BuildDamageVisuals` opens AI damage too. Read `FlightRoster.cs` next.

## src/Session/World/WorldEffectsFactory.cs
Builds the two effect stages a session needs and the runtimes bound to them: the world-effects
runtime for impacts and destruction, and the per-plane crash runtime, which despite its name binds
every def that plays ON one aircraft (the crash-def vector, the destroy def, the panel damage shims,
the prop choreography, and on a human rig the canopy holes, whose `PLAYER_1ST_PERSON` branch this rig's own pilot answers). Both stages are built in pool slots sized from `data/effect_pools.json`
and handed to their runtime sealed, and both pre-warm their emitters after the bind so a first burst
finds its puffers already made; `EnsureWorldEffects` hands the effects runtime the world's `WorldLights` as a contributor, so a burst's authored `LIGHT_STATE` renders on both presentations, and `FollowGraphicsMode` re-reads the replaced-light set and frees the heat shimmer on a live switch. `BeginFlightCrashRuntime` opens the crash build as a handle a caller
steps a phase at a time (`CrashRigQueue.cs`), where the pre-warm itself repeats a slice at a time so a rig's two hundred emitters never
land on one frame, and `BuildFlightCrashRuntime` is the one-call form. The bind step also snapshots the built parent, pose and visibility of the airframe, the wreck and every template a bound def adopts (`OBJECT_ADD_CHILD`'s child, the eject's `cpilot`), which is what a respawn restores. The names it binds are `Flight/Airframe/EffectCatalogue.cs`; the slot mechanism is `Mech3/TemplateStage.cs`.

## src/Session/World/WeatherRig.cs
Applies the flown mission's weather, driving each rig's skydome, whiteout, deck regime and zone gate every frame, plus the one session-wide `ObjectZoneGate.cs` pass; the whiteout is one pane-filling overlay per rig, carrying the cloud band and the fog-volume curtain on `HudLayers.Whiteout`, under that pane's own cockpit pass, so the window whites out and the interior stays clear.
Each rig keeps its own fog (`FogOf`) and vertex light (`SunOf`), moved by its own camera-state edge and published through `FogViewTable.cs`; the first rig's edge (or a change of first rig) also runs `ApplyZone`, the one-per-session scene sun and ambient and the plain globals, through one arm per
graphics mode, mirrored onto every registered extra (sun, env) pair so a cockpit overlay crosses
zones too; both arms put the ambient half through `WriteColorAmbient`, colour-sourced and never a sky
contribution, so a night zone's scene fill is darker than a day zone's. `ApplyFogState` is the
animation runtime's `FOG_STATE` sink, writing only the fields the event carries under the same
last-writer order. Enhanced mode also caps a night zone, paints the sky the zone's own fog colour and
pushes the fog range out, which the sun's shadow distance follows. `SunlightRgb` publishes the applied pair scaled by its authored colours, for the reader that needs the light rather than an energy. Both arms also set `csky_sun_dir` and `csky_sun_light`, the same bearing and pair uncollapsed, for the lit cloud cards, which shade per vertex off normals that turn with the camera, and `csky_sun_ambient_rgb`/`csky_sun_diffuse_rgb` (`SunVertexLight`, the bicolored rule) for the in-flight aircraft's per-vertex term ([../org/vertexLighting.md](../org/vertexLighting.md)), plus `csky_sun_fill_rgb` (`PhotographFill`), the Danger Zone photograph's raised ambient half. Decode: [../org/weather.md](../org/weather.md), authored side [../formats/weather.md](../formats/weather.md).

## src/Session/World/FogViewTable.cs
The per-view atmosphere table: every camera drawing the shared world (each splitscreen pane and each
live spyglass picture) with the fog colour, range, altitude and world light of its own rig's zone in
the `csky_view_0`..`_7` mat4 globals, and that zone's vertex light (the `csky_sun_*` set) in
`csky_view_sun_0`..`_7`, with the `csky_view_count` global that `shaders/csky_atmosphere.gdshaderinc`
searches, nearest eye first. While every view carries the same records the count stays 0 and the
shaders read the plain `csky_fog_*` and `csky_sun_*` globals, so a single view renders as it always
did. `WeatherRig.cs` fills it each frame; a session teardown and the test harness clear it.

## src/Session/Objectives/ObjectZoneGate.cs
The per-object half of the zone gate, ticked by `WeatherRig` over the session's aircraft and
zeppelins. Each frame an object's merged world extent is judged against the cloud band's midpoint
(`Effects/Weather.cs`'s `ObjectZone`) and its meshes move onto that zone's `Mech3/ZoneGate.cs` layer,
so a camera on the far side of the band does not draw it at all, which is the original's per-object
zone assignment plus its camera zone gate. The extent is measured once per root because an airframe
is rigid, and the walk takes whatever hangs under it, so an effect parented to an aircraft rides
along; a mesh already wearing a layer of its own (the own-airframe hide, a per-pane copy) is left
alone. `--no-fog` and `--no-zone-cull` open the gate. Decode: [../formats/weather/atmosphere.md](../formats/weather/atmosphere.md).

## src/Session/World/LensFlareRig.cs
The sun's lens flare: four screen-space sprites strung along the sun-to-screen-centre vector, plus a
full-screen white wash whose opacity is about linear in the sun's screen distance from centre. It
mirrors `WeatherRig`, constructed once per session beside it, `Build` once and `Tick` from the same
per-rig block, one instance per pane. The per-pane state lives here rather than on `PlayerRig`
because an instance is several nodes plus fade state. `--no-flare` suppresses the effect so a
capture stays usable for unrelated comparisons. The spec is measured from capture footage; method
and calibration: `git show analysis-archive:analysis/bl-165-lens-flare/FINDINGS.md`.
