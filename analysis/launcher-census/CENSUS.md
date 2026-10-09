# Launcher census: what changes for which reason

`CSVM/src/Launch/Launcher.cs` is the composition root (Main.tscn's root). This census groups its
fields and the members that use them by the reason they change, names the module each group
becomes, and records which groups this pass moved out and which wait for a later pass.

## How the counts were taken

Fields are counted per declarator (`private int _a, _b;` is two), constants separately, over the
`Launcher` class alone (`LauncherContext`, which shares the file, is excluded). A trailing `//`
comment is stripped before a line is read. "Members" are the `public` and `internal`
non-override members, the per-wave measure; the six Godot overrides are left out because a node
cannot shed them.

| | file lines | class lines | fields | constants | public/internal members |
|---|---|---|---|---|---|
| Launcher, before | 3852 | 3731 | 99 | 5 | 21 |
| Launcher, after | 2751 | 2630 | 62 | 1 | 5 |
| FrameInstruments (new) | 401 | 401 | 31 | 3 | 7 |
| NetFlight (new) | 516 | 516 | 11 | 1 | 23 |
| SeatFields (new) | 368 | 368 | 0 | 0 | 11 |

The 37 fields that left are 29 instrument fields and 10 network fields, less the two that compose
them (`_instruments`, `_net`). Two new fields in `FrameInstruments` (`_spec`, `_counters`) carry
what `Launcher` used to read off its own state. The 17 members that left are the network rules the
suites call (`VersusLaunchField`, `LobbyLanding` and the rest), now on `SeatFields` and `NetFlight`.
The one member that arrived is `Launcher`'s constructor, which builds `NetFlight` over the node's
two exits.

## The groups

Each group lists its fields (before the split), its members, and the reason it changes. The
reasons are read off the file's recent history: master server and WebRTC defaults, Public/Private
listing and join by code, the guest clock trace, seat rosters with bots, the lobby landing after a
match, the spyglass census, the `--debug-mem` lines, the quit's task-queue drain and ENet close,
Steam's on-screen keyboard, and the build stamp's messagebox.

### 1. Frame instruments: moved to `FrameInstruments.cs`

- Fields (29): `_perfFrameMs`, `_perfFrameMsSorted`, `_perfClock`, `_perfFrames`, `_perfProcess`,
  `_perfGpu`, `_perfCpuRender`, `_perfPhysics`, `_perfSetup`, `_perfDraws`, `_perfPrims`,
  `_perfNodes`, `_perfMem`, `_perfDiscs`, `_perfDiscDraws`, `_perfDiscShadowDraws`,
  `_perfDiscPrims`, `_perfDiscGpu`, `_gcTrace`, `_drawMarksHooked`, `_rateWallMs`, `_rateWorstMs`,
  `_rateFrames`, `_hitchMonitor`, `_perfHud`, `_renderTime`, `_lastFrameStamp`, `_memCensusStamp`,
  `_hitchSidecar`. Constants: `PerfWindowFrames`, `Perf95Index`, `RateWindowSeconds`.
- Members: `ReadFrameCounters`, `InjectHitch`, `ReportRate`, `RearmRate`, `ReportPerf`, the
  instrument block of `_Process`, the rearm sequence `LaunchSession` and `ReturnToMenu` each
  repeated, and the `--debug-mem` exit line in `_ExitTree`.
- Reason to change: what is measured and how it is reported (the `--perf` terms and their
  verification rules, the spyglass disc counts, the memory census, the hitch record). Nothing else
  in the launcher reads these fields; the launcher only needs the frame's wall cost back (for the
  graphics switch cover) and the four calls `BeginFrame`, `EndFrame`, `Rearm`, `Exit`.
- Seam: clean. The one ordering constraint, three construction points in `_Ready` (the monitor
  before any early quit, the sidecar after `Log.Open`, the readout after the early quits), is now
  three calls on the module: the constructor, `OpenSidecar`, `BuildHud`.

### 2. The flight's wire and door: moved to `NetFlight.cs`

- Fields (10): `_netDoor`, `_netWire`, `_netRoster`, `_netIsHost`, `_coopFlight`, `_lobbyFlight`,
  `_keepLobby`, `_coopSeatFits`, `_seatBuilds`, `_coopStock`. Constant: `NetLinkWaitSeconds`.
- Members: the door's construction in `BuildMenuHost` and `MasterServer`, `OpenCliNet`,
  `AwaitCliNetLink`, `TakeNetLaunch`, `TakeCoopLaunch`, `RelaunchCoop`, `CloseNetLaunch`,
  `TickCoopFlight`, `TickVersusFlight`, `CoopSeatFit`, `NetSeatBuild`, the landing read in
  `ExitSession`, the co-op leave notice in `ReturnToMenu`, and the flight-end rules
  `CoopGuestFlightOver`, `CoopRelaunch`, `LobbyLanding`, `LandsOnScores`, `RaceLanding`,
  `VersusGuestFlightOver`, `EndNetWire`.
- Reason to change: how a network flight opens, holds and ends its wire (master server and WebRTC
  wiring, the CLI link wait, co-op restart, the lobby landing, the host's close notice).
- Seam: the state is the module's own. `WithNet` writes the session's whole network slice into a
  copy of the launcher's `LauncherContext`, `RefusesRestart` and `Restart` hold the co-op restart
  rule, and the launcher gives it its two exits, `ReturnToMenu` and `ExitSession`, as delegates. The order of every call is kept: the take
  before the spec is derived, the local field after the sortie seed steps, the close inside
  `ReturnToMenu` and `BlankAndQuit`.

### 3. Seat fields: moved to `SeatFields.cs`

- Fields: none. Members: `CoopSeatFitFor`, `CoopWingmanFor`, `CoopLaunchField`,
  `VersusLaunchField`, `SeatBuildsFor`, `CoopSeatBuilds`, `SeatBuildFor`, `TeamNames`,
  `LocalVersusField`, `ResolveBots`, `LogBotSeats`, `WarnBotsLeftOut`, and `BuildCliNetRoster`
  (now the pure `CliHostField`).
- Reason to change: who sits where and flies what (bot seats, voices, team numbers, custom planes
  under the host's rules). These change with the seat roster's features, not with how a wire
  opens or closes, so they are their own module rather than part of `NetFlight`.
- Seam: clean. They were already `internal static` and pure; 13 suites call them directly.

### 4. Process bootstrap and paths: stays (later pass)

- Fields (19): `_cli`, `_spec`, `_repoRoot`, `_exported`, `_dataRoot`, `_syntheticError`,
  `_planesGamezPath`, `_zrdrPath`, `_soundsPath`, `_interpPath`, `_messagesPath`, `_rofPath`,
  `_probeRunner`, `_captureDirector`, `_gltfExporter`, `_masterSeed`, `_processSeed`, `_sortie`,
  `_clock`.
- Members: most of `_Ready`, `ResolveSyntheticData`, `StartHeadlessExtraction`, `LogMasterSeed`,
  `StepSortieSeed`.
- Reason to change: a new command-line flag or process-wide side effect.
- Why it waits: `_Ready` is one ordered sequence whose order is the behaviour (the log opens after
  the parse warnings, the memory admission before anything heavy, each early quit after what it
  needs). The clearest sub-seam is the eight base paths, resolved together in `_Ready` and again in
  `LeaveExtractionScreen`: a `BasePaths` value built by one function would remove the duplicate.
  That is a value, not a module with a reason of its own, and it reaches `LauncherContext` and
  `GameSession`, so it belongs with a pass that also reshapes the context.

### 5. Session lifecycle and load screen: stays (it is the composition root's job)

- Fields (12): `_session`, `_menuDriven`, `_exitDestination`, `_menuPads`, `_pendingDebrief`,
  `_pendingWrapup`, `_pendingLobbyLanding`, `_loadLayer`, `_launchFramesWaited`, `_loadStepsRun`,
  `_startHeldFrames`, `_startFade`.
- Members: `BeginLaunch`, `RunOwedLaunch`, `TryLaunchSession`, `LaunchSession`, `RaiseStartCover`,
  `DropStartCover`, `ShowLoadScreen`, `HideLoadScreen`, `LaunchBriefing`, `LaunchSubject`,
  `CampaignLoadSheet`, `StartSessionFromMenu`, `StartCampaignFromMenu`, `Unpack`,
  `BindMenuPads`, `OpenDebrief`, `RestartSession`, `ExitSession`, `ReturnToMenu`, `BlankAndQuit`,
  `LaunchedFrom`.
- Reason to change: how a session starts and ends. This is what the composition root is for, and
  every other group hangs off it, so it stays.

### 6. Menu host, presentation and menu input: stays (later pass)

- Fields (13): `_menuHost`, `_menuWheel`, `_menuAudio`, `_originalLayout`, `_menuAid`,
  `_pendingJoin`, `_pendingWaves`, `_pendingWingmen`, `_pendingPreset`, `_buildStamp`,
  `_chapterCinema`, `_closingCinema`, `_pendingApply`.
- Members: `BuildMenuHost`, `OriginalAvailable`, `MousePosition`, `MenuPrimaryPressed`,
  `TakeMenuWheel`, `ApplyMenuDebugAids`, `OnMenuExit`, `ShowMenu`, `EnterMenu`, `MenuStep`,
  `_Input`.
- Reason to change: the menu presentations and their seat (Steam's on-screen keyboard, the
  stamp's messagebox, the pointer seat).
- Why it waits: `ShowMenu` is entangled with the lifecycle (it is the end of every
  `ReturnToMenu`) and with the board aids below, and `OnMenuExit` is the sink that starts sessions.
  Splitting it means a callback surface as wide as the group. A later pass should first move the
  board aids out (group 8), then see whether the host's construction alone is a module.

### 7. Options apply and the graphics switch: stays (next candidate)

- Fields (3): `_pendingGraphicsToggle`, `_switchCover`, `_debugSwitchesDone`; it also dresses the
  sun and Environment of group 9.
- Members: `ApplyOptions`, `PersistOptions`, `SwitchGraphicsMode`, `RequestGraphicsSwitch`,
  `DropSwitchCover`, `ApplyShadowQuality`, `SaveGraphicsMode`, `ApplyViewDistance`,
  `BuildPauseOptions`, and the display and graphics resolution block of `_Ready`.
- Reason to change: the Options page's settings and how each applies live.
- Why it waits: the seam is fair (it needs the sun, the Environment, the session and the data
  paths), but the startup resolution in `_Ready` resolves the same settings a second way, and the
  two should move together, which is a larger change than this pass. Docs and suites cite
  `Launcher.ApplyOptions` and `Launcher.PersistOptions` by name in several places.

### 8. Screenshot board aids: stays (next candidate, clearest remaining seam)

- Fields: none. Members: `ShowPauseSheet`, `ShowInstantActionPauseSheet`,
  `ShowMultiplayerPauseSheet`, `PauseAidReadout`, `CampaignMissionAt`, `SeatedMemento` (shared with
  the load screen's chart sheet), and the `loadboard`/`pauseboard` branches of `ShowMenu`.
- Reason to change: the decoded pause and load screens (the Dogfight pause strips).
- Why it waits: it holds no state and reads only the data paths and the spec, so it is the
  cleanest remaining split; it was left out only to keep this pass to the three groups whose state
  moved. Its module would build each aid's layer and hand it to the launcher to parent.

### 9. Persistent rendering nodes and shader globals: stays

- Fields (4): `_camera`, `_sun`, `_env`, `_orbit`.
- Members: `SetupLighting`, the global shader parameter registration in `_Ready`.
- Reason to change: the world's lighting defaults and the shader globals.
- The issue's lead, "per-pane render setup", does not hold for this file: per-pane fog and the
  spyglass discs live in `FogViewTable`, `GameSession` and the camera modules. What remains here is
  about 80 lines of one-time registration, which shares `_Ready`'s ordering constraint (registered
  before the `--dump-*` branches build materials). Not worth a module on its own.

### 10. Audio, music and cinemas: stays

- Fields (5): `_music`, `_musicArchive`, `_musicRng`, `_focusMuted`, `_cinemaShown` (the two
  cinemas are counted under group 6, whose campaign feature holds them). Constant: `MasterBus`.
- Members: `BuildMusic`, `PlayCinema`, `StopCinema`, `PlayBootSequence`, `ApplyMasterVolume`,
  `SetFocusMuted`, `_Notification`.
- Reason to change: the score and the master bus.
- Why it waits: `PlayCinema` is the public seam the campaign's cinemas and the boot card call on
  the node itself, and `BuildMusic` is re-run after an extraction; a move changes those call
  sites for little gain.

### 11. Extraction screen: stays

- Fields (1): `_extractionScreen`. Members: `ShowExtractionScreen`, `RunExtraction`,
  `LeaveExtractionScreen`, `ApplyExtractionAid`.
- Reason to change: the in-game extraction flow.
- Why it waits: its hand-back re-resolves the base paths and the music, so it moves with group 4.

### 12. Process quit and debug readouts: stays

- Fields (3): `_netReadout`, `_sinceNetReadout`, `NoRigs`. Members: `_ExitTree` (the ENet close
  first, then the task-queue drain), `DrainLowPriorityTasks`, `MirrorEngineLog`, `TickNetReadout`,
  `_UnhandledInput` (F10, F11, F12, Esc).
- Reason to change: process lifetime, and the `--debug-net` readout.
- Why it waits: about 120 lines with almost no state; the quit's order (transports closed before
  the drain, the drain before the instruments' exit, the log mirror last) is clearest kept in
  `_ExitTree` itself.

## Order for a later pass

Group 8 (board aids) first, since it holds no state. Then group 7 (options apply and graphics
switch) together with the startup resolution it duplicates. Group 4's base paths go with any
change to `LauncherContext`.
