# Launch

The composition root: the `Launcher` scene root, the per-launch `GameSession` node, and the services only they reach. `CSVM.Launch` is the top family bar `Testing`, so it may name any other and nothing below names it (`CSVM.Tests/FamilyOrderTests.cs`).

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Launch/Launcher.cs
Main.tscn's root and the process bootstrap: CLI parse into `_cli`/`_spec`, data-root precedence, the
editor check that gives an export its `logs\` and audible volume default, the developer gain on bus 0 with the saved mix under it, at startup and on an Options apply (`Utils/MasterVolume.cs` resolves the first, `Utils/AudioMix.cs` writes the second), the `--dump-*`/`--run-tests` early quits and the `--extract` run (`Extraction/ExtractionRun.cs` on a worker thread),
and what outlives a session (camera, sun, audio, music, the perf and hitch instruments, the stick pump `Sticks/StickPump.cs` started after the pad roster is logged, the typed-character feed `UI/Boards/TypedText.cs` its `_Input` fills with every key event's character and paste chord, and the one `ChapterCinema` and `ClosingCinema` the campaign's doors play through). It owns the
menu as one `MenuHost` built on the first show, the presentation resolution, the only options write (an apply from the in-flight `UI/Screens/PausePreferences.cs` leaf takes that same route, without the presentation reselect a menu-side apply ends on),
the frame pacing and the window's screen, mode and size at startup and on an Options apply (`Utils/VSyncSetting.cs`, `Utils/MonitorSetting.cs`, `Utils/DisplayModeSetting.cs`, `Utils/ResolutionSetting.cs`), the graphics mode at startup and switched live on an apply or a seat's Toggle Graphics Mode action (`SwitchGraphicsMode`, dressing the sun and Environment through `EnhancedLook.cs`),
and the sink every menu exit takes ([../menu-presentations.md](../menu-presentations.md)); with no
extraction or a stale stamp it shows `UI/Screens/NoGameDataScreen.cs` before any sound archive opens, and enters the menu from it in the same process. Each launch hands the session one `LauncherContext`, the settled paths, the persistent nodes, the process services and the match's wire (`NetTransport` with `NetHost` and `NetAirframes` beside it, or `NetSeats` and `NetHandshake` where a caller has them already), never a new argument, which is a `SessionSpec` change. The wire arrives from the `UI/Menu/NetPlayFeature.cs` door the launcher registers, or from `--net-host`/`--net-join` on a headless run, which waits for the link before it launches; both open through `Net/NetCarrier.cs`, so which carrier a build ships is not this file's to know; the launcher steps neither, since the session owns a wire from the moment it takes it, but it does give the router's port back when the flight ends. `LaunchSession`, `ReturnToMenu`, `RestartSession` and
`BeginLaunch`/`RunOwedLaunch` are every path a session starts or ends on (the load screen stays up past the build while the session's owed build steps run one a frame through `GameSession.StepOwedLoad`, which is what makes it a yield of several frames, and while a network start is held, with a fresh start cover after it; a CLI launch has no screen and drains them inside `LaunchSession`), a flight left early comes back to the screen it was launched from (settled by the launch through `MenuReturnDestination.ForLaunch`, not by the exit press), and what the persistent `WorldEnvironment` draws behind all of it is `Utils/WorldBackdrop.cs`'s: black while the menu owns the screen and at the quits that still draw, the sky again at every launch. A co-op campaign's field is `CoopLaunchField`: the host's roster names each guest and keeps each seat's `CoopFit`, and every machine resolves a seat's loadout through `CoopSeatFitFor` into the context's `NetSeatFit`, while `CoopGuestFlightOver` ends a guest's flight when the host's boards leave the mission. A lobby Dogfight's field is `VersusLaunchField`, the host's first seat named as its advert names the host (its player tag when that is empty), each guest's plane and fit read off the lobby's picks under the host's plane rules; every seat's custom plane goes through `SeatBuildsFor` and `SeatBuildFor` into the context's `NetSeatBuild`, and `LobbyLanding` returns a completed one to that lobby's Game Scores with the door kept open.

## src/Launch/GameSession.cs
The per-launch orchestrator: `Launcher` constructs it from `(SessionSpec, LauncherContext)` and `StartSession` runs ordered build steps over one `BuildState.cs`, the shared input of every step. It owns the session clock, the world root, panes and seats, resource lifetimes and the per-frame tick order across the runtimes (`_Process`, both clock adapters and the phase map it hands `SessionSimulation`).
It composes the steps that each write one subsystem: `SkyStage.cs` (weather, cloud field and banks, decks, flare), `InspectionLabs.cs` (the parked-plane view, freecam, anim lab, selection labs, debug overlays), `SessionProbes.cs` (the scripted probes and build-time forces), `SessionBoards.cs` (pause, results boards, photo mode), `ProjectileStage.cs` (the shared pool and its sinks), `SessionVoices.cs` (the radio and combat voice), `KillLines.cs`, `OppositionStage.cs` (the `--ai` squadrons, zeppelins, generators, emplacements) and `ObjectiveReadouts.cs`, beside `FlightRoster` for aircraft, `WorldSession` for the world and `WorldEffectsFactory` for effects. The mode tails sit on their directors, `InstantActionDirector`, `CampaignDirector` and, for a Dogfight, `Session/World/VersusDirector.cs`, and `BuildFlightRigs` calls the directors' phases between the steps in the order the build needs.
`_rigs` is the pane list every camera-anchored system reads; `_seatRigs` is the whole field, a network match's guests included, and sizes the roster, the spawn walk, the versus board, the human-aircraft step and a campaign's human field (`HumanField`), so a guest flown elsewhere counts at its interpolated pose, and a co-op campaign of more than one seat starts on `StartGrid` whatever its local pane count.
The wire and everything wired onto it is `SessionNet.cs`, opened in the constructor so a host can answer a join before its world stands; the session calls its join, seat build, clock, start gate and link steps at their build points and steps it before each simulation step, so an arrival is applied on the step after it landed. After the synchronous build it constructs one `SessionSimulation`, which owns the step order.
`AllAircraft` combines the roster's AI view with the ordered rig controllers, and `StepOwedLoad` drains the wave aeroplanes `OppositionStage.OrderWaveAirframes` put behind the load screen.
Exit frees the session subtree atomically and releases only the non-node resources it owns; the prohibitions that keep these rules true sit on the members they bind. Read `SessionSimulation.cs` next.

## src/Launch/BuildState.cs
The per-build state `GameSession.StartSession`'s ordered steps thread through: the settled paths, the archives `LoadArchives` opened and which of them outlive the build, the world build's outputs (the gamez, the scene and runtime, the crash program, the craters, the staged aircraft, the landing rows and pickups) and the running mesh, collider and summary counts. It is the one input the build steps share, so a step reads what an earlier one wrote here rather than a session field, and nothing in it is cached across a rebuild. Read `GameSession.cs` next.

## src/Launch/SkyStage.cs
The sky step of a session build and what it leaves standing: the mission's `Session/World/WeatherRig.cs` with each rig's horizon domes (one per zone the gate can tell apart, each scaled inside the camera's far plane), the fogvol cloud field and the Enhanced volumetric banks, each pane's copy of the cloud deck, and the sun's `LensFlareRig`. A FOG_STATE the world bootstrap raises before the rig exists is held and applied over the zone. The session ticks `Weather` and `Flare` in its own frame order and asks `FollowCloudBanks` on a live graphics switch. Read `Session/World/WeatherRig.cs` next.

## src/Launch/SessionProbes.cs
The session build's scripted probes and build-time forces: `--damage-test` and `--effects-test` over the chapter world just built, `--dump-tilegrid` once the map edge exists and the parked `--weapon-test` bench, each of which reports through `Tooling/ProbeRunner.cs` and ends the process, and `--destroy=` and `--debug-objective=`, which change the built world and let the session fly on. A probe that ran answers true and the build stops at that step. Read `Tooling/ProbeRunner.cs` next.

## src/Launch/InspectionLabs.cs
The inspection side of a session build, each step joining its nodes at the session's own build point: the parked-plane view (`--viewer` or a bare `--plane=`) with its damage, livery, mesh and marker labs; the freecam and the anim lab's quiet stage; the shared world selection with its node and world damage labs; player 1's flight damage and weapon labs; the overlays every observing mode carries (colliders, classes, the map edge's tile grid, node labels); and the flight's debug keys (F13's patrol nets, F15's targeting lines, F16's markers, F17's kill). `Spectator` is the freecam or anim-lab camera a `--destroy=` frames. Nothing here runs per frame. Read `UI/Screens/SelectionService.cs` next.

## src/Launch/SessionBoards.cs
The whole-window boards over one flight: the pause board (the Original presentation's sheet for a campaign mission, a Dogfight or an Instant Action sortie, the Built-in `PauseBoard` otherwise) under the launcher's `PausePreferences` leaf, the results boards (the race, the dogfight, the Instant Action wrap-up, each pane's solo stunt scoreboard), one `MenuInput` per local player, and photo mode, which suspends whichever board is up and restores it on the way out. Each board's Restart is the mode's, handed in, and the Instant Action and solo stunt boards reach their directors through the `BuildIaWrapupBoard` and `BuildSoloStuntBoard` seams. Read `UI/Screens/ResultsBoard.cs` next.

## src/Launch/SessionNet.cs
A session's end of the wire and every link on it, outside a network match a null `Link` whose steps all do nothing. `AwaitJoin` pumps a guest's wire until the host's seed, seats and roster land, ahead of `Rng.Reset`; `BuildSeatRigs` fills the session's seat list (a pane-less rig per seat flown elsewhere) and claims the aircraft-state samples. `WireClock` and `WireStartGate` open the `NetClockPing` round trip and the `NetStartGate` before the build, and `HoldStart` holds the clock on the gate until every machine has loaded (`StepStartHold` steps only the wire meanwhile). `WireCombat` runs after the match: an owner's fire event spawns the round on every peer, the shooter's machine decides a hit and addresses the victim's owner, that owner applies it and reports its death (`ReportDeath`, from the Dogfight director's `Downed` handler in a match), and the host alone scores it and relays each between guests. `WireChat`, `WireDirector` (`NetDirectorLink.cs`), `WireWorld` (`NetWorldLink.cs`), `WirePositionalStarts` and `WireCutscenes` follow at their build points, each taking what it binds as arguments; `RewireSeat` puts a swapped airframe back on the wire, since that wiring is per controller, and `TakeGuestLeft` retires a seat whose guest walked out. The session keeps the tick order: `Step` before each simulation step, `Advance` per frame, `BroadcastAircraftState` at the end of the human-aircraft phase. Read `Net/NetSession.cs` next.

## src/Launch/ProjectileStage.cs
The flight's one shared `ProjectilePool`, built over the `BuildState` with its sinks: world hits to the destructibles, a CRATER strike to the crater field and, under Enhanced, `RegisterScorch` (the one decision point for a scorch decal), and a wash to the struck pane. Every pane's camera is bound as a viewer and the world's point lights take the muzzle flash. Read `Flight/Weapons/Projectile.cs` next.

## src/Launch/SessionVoices.cs
The mission radio and the combat voice over the world's prewarmed sounds, and the ai_skill_parameters table both they and the AI activation floor (`MinAiActiveDist`) read. `Build` registers every human (a network seat as its lobby pilot), and every AI spawn path registers through `RegisterAi`. The session steps both runtimes in its own phase order. Read `Session/Roster/AiVoiceRuntime.cs` next.

## src/Launch/KillLines.cs
The kill and crash lines in each pane's message stack: a downed aircraft posts one line per pane worded as that pane reads it, a hull flown into the world posts its crash notice in its own pane, and a Dogfight seat's death takes the match's death lines instead. The roster's own hook covers the aircraft a wave releases. Read `Flight/Hud/HudMessages.cs` next.

## src/Launch/OppositionStage.cs
The opposition outside the directors' own rosters, in four steps the session calls at their build points: `BuildSquadrons` (the `--ai` entries, on a net, on the empty stage's ring, or ahead of P1), `BuildZeppelins` (the mission's hulls with their damage, cannons and target sub-parts, or the `--zep=` graft), `BuildGenerators` (the egen runtime, its launches through the roster and `OrderWaveAirframes` for the load screen) and `PlaceEmplacements` (the world AA guns with the Instant Action zeppelin arm before `--wake-turrets`). Each returns the runtime it built for the session to step. Read `Session/Roster/AiGeneratorRuntime.cs` next.

## src/Launch/ObjectiveReadouts.cs
A flown mission's objective readouts once the graph is armed: each pane's objectives readout and mission-end fade and the clock's expiry notices on a campaign, and the objective sites on the player's target cycles. A mode without a director takes the same site feed off the mission's own targets.zrd when `BindsMissionTargetTable` says so. Read `Session/Objectives/ObjectiveSites.cs` next.

## src/Launch/TuningWarmup.cs
The startup pass that fills `Config`'s key registry before `Config.ReportOrphans` and
`--dump-config` run. `Run` builds a throwaway `FlightModel` and steps it once, reads the
`HudMetrics` scales, and registers the keys whose reads happen only on a path the launch never
drives (rocket and tracer tunables, the loadout caps, the `Puffer` scales, the `StartGrid`
spacing, the graphics keys). It sits in `Launch` so that `Utils.Config` names none of the modules
it serves. A module newly wired to `Config` adds its line here. `Launcher._Ready` is the one caller.

## src/Launch/MenuAudioService.cs
The host's `IMenuAudio` over the process's playback. `BeginNarration` ducks the music and restarts the narration player on
the resolved stream; `EndNarration` lifts the duck and stops it, idempotent because the launchscreen calls it every frame
no briefing shows. `Cue` resolves a semantic name through `MenuCueTable` to a file under the constructor's cue directory,
decodes it once and replays it from the start; an unknown name, a missing file or a failed decode is logged once and
cached as silence. `PreviewMix` applies a mix page's levels as they move and sounds the moved category over the original's
`sfx_loop.wav`/`voice_loop.wav`, leaving a running clip alone so a drag is one clip and not one per frame; `EndMixPreview`
puts back the captured gains, and previews are off unless the constructor says otherwise. Narration is begun and ended by
whichever presentation shows a briefing, so this service knows nothing of which screen is up.

## src/Launch/MenuCueTable.cs
Which wav under the extracted sound directory a semantic menu cue name resolves to: the rollover,
the click and the two edit-box keystroke sounds, the four files the original's globals script binds.
The indirection from a name to a file is the remake's, since the original's scripts name raw wavs,
and it lives here so `MenuAudioService` owns lookup alone. Engine-free, so the table is unit-tested
against the names the presentations ask for. Read `MenuAudioService.cs` next.

## src/Launch/MasterServerLink.cs
The shipped way to the master server, over .NET's `HttpClient` and `ClientWebSocket`: `FetchGames`
is the games list GET, `Open` an `IMasterSocket` whose receive and send loops run on the pool with
queues to the frame. The send loop repeats a host's listing through a stalled frame for up to
`KeepListedSeconds`. Here rather than in `Net/` because the seam names no socket API; the launcher
hands it to the door and `NetCarrier`. Read `MasterServerLinkTests.cs`, which runs it against the server.

## src/Launch/EnhancedLook.cs
The enhanced graphics mode on a running process, on one sun and one Environment and both ways:
PSSM sun shadows, SSAO, SSR, glow, the AgX tonemap and the mission sky, with the `EnhancedPasses`
doors; off writes a fresh object's defaults back. `Switch` is the whole live mode switch, refused
for a network session: shaders, sun, Environment, clutter fade, display quality, then
`GameSession.ApplyGraphicsMode`, which opens with `FollowAlphaDepth`; its log line carries
`Utils/SwitchProfile.cs`'s steps. `HasSwitched` lets a later load warm the other mode's shaders,
`WarmAdvancedVariants` their TAA variants through one hidden frame. `ApplyViewDistance` is the live
View Distance; `Utils/SunShadow.cs` carries the sun to the cockpit; `SwitchCover.cs` draws a switch over.

## src/Launch/SwitchCover.cs
A live graphics switch over a flying world, drawn over so the stall does not read as a crash. The
launcher's `RequestGraphicsSwitch` builds one for the G action, the Options apply and
`--debug-graphics-switch`: it raises `HaltReason.Switching` on the session's `PauseState` (or holds
the clock directly where there is none), puts a blackboard `LoadBoard` over the whole window,
runs `EnhancedLook.Switch` on the cover's second frame, and drops both once three frames in a row
come in under 50 ms or twice the fastest since (never past 500 ms). A player's pause stays up.
`GraphicsMode.SwitchLocked` greys the Options row in a network session. Read `EnhancedLook.cs`.
