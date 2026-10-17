# Launcher census: what changes for which reason

`CSVM/src/Launch/Launcher.cs` is the composition root (Main.tscn's root). This census groups its
fields and the members that use them by the reason they change, names the module each group
becomes, and records which groups have moved out and which stay. Three passes have moved the
groups with a reason of their own: the first took groups 1 to 3, the second groups 7 and 8, and the
third group 4's base paths (with group 11's hand-back that re-resolves them), group 6's menu host
construction, and group 10.

## How the counts were taken

Fields are counted per declarator (`private int _a, _b;` is two), constants separately, over the
`Launcher` class alone (`LauncherContext`, which shares the file, is excluded). A trailing `//`
comment is stripped before a line is read. "Members" are the `public` and `internal`
non-override members, the per-wave measure; the six Godot overrides are left out because a node
cannot shed them.

| | file lines | class lines | fields | constants | public/internal members |
|---|---|---|---|---|---|
| Launcher, before | 3852 | 3731 | 99 | 5 | 21 |
| Launcher, after the first pass | 2751 | 2630 | 62 | 1 | 5 |
| FrameInstruments (new) | 401 | 401 | 31 | 3 | 7 |
| NetFlight (new) | 516 | 516 | 11 | 1 | 23 |
| SeatFields (new) | 368 | 368 | 0 | 0 | 11 |
| Launcher, before the second pass | 2745 | 2579 | 62 | 1 | 5 |
| Launcher, after the second pass | 2344 | 2178 | 60 | 1 | 5 |
| LiveSettings (new) | 231 | 216 | 7 | 0 | 7 |
| LaunchBoards (new) | 282 | 262 | 0 | 0 | 3 |
| Launcher, before the third pass | 2348 | 2178 | 60 | 1 | 5 |
| Launcher, after the third pass | 2012 | 1852 | 43 | 0 | 3 |
| BasePaths (new, in `CSVM.Spec`) | 35 | 24 | 0 | 0 | 1 |
| LaunchAudio (new) | 195 | 181 | 7 | 1 | 12 |
| ProcessMenu (new) | 194 | 172 | 6 | 0 | 4 |

The second pass's class lines run from the class declaration to its closing brace, so they read
lower than the first pass's for the same file; its file lines and field counts compare directly.

The 37 fields that left are 29 instrument fields and 10 network fields, less the two that compose
them (`_instruments`, `_net`). Two new fields in `FrameInstruments` (`_spec`, `_counters`) carry
what `Launcher` used to read off its own state. The 17 members that left are the network rules the
suites call (`VersusLaunchField`, `LobbyLanding` and the rest), now on `SeatFields` and `NetFlight`.
The one member that arrived is `Launcher`'s constructor, which builds `NetFlight` over the node's
two exits.

The third pass's 17 fields are the eight base paths (now the one value `_paths`), seven menu fields
(`_menuHost`, `_menuWheel`, `_menuAudio`, `_originalLayout`, `_menuAid`, `_chapterCinema`,
`_closingCinema`) and five sound fields (`_music`, `_musicArchive`, `_musicRng`, `_cinemaShown`,
`_focusMuted`), less the three that compose them (`_paths`, `_audio`, `_menu`). `MasterBus` moved
with the sound. The two members that left are `PlayCinema` and `StopCinema`, now `LaunchAudio`'s.
Two fields in `LaunchAudio` (`_dataRoot`, `_masterVolume`) carry what `PlayCinema` used to read
off the launcher, and `ProcessMenu`'s `Host` property holds the menu host. `LauncherContext`'s
eight path properties are now its one `Paths`; `GameSession`, `ProbeRunner` and `LaunchBoards`
hold the value rather than its strings. `BasePaths` lives in `CSVM.Spec`, the family the probe
runner (`CSVM.Tooling`) can name.

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

### 4. Process bootstrap and paths: the base paths moved to `Spec/BasePaths.cs`, the rest stays

- Fields (19): `_cli`, `_spec`, `_repoRoot`, `_exported`, `_dataRoot`, `_syntheticError`,
  `_planesGamezPath`, `_zrdrPath`, `_soundsPath`, `_interpPath`, `_messagesPath`, `_rofPath`,
  `_probeRunner`, `_captureDirector`, `_gltfExporter`, `_masterSeed`, `_processSeed`, `_sortie`,
  `_clock`.
- Members: most of `_Ready`, `ResolveSyntheticData`, `StartHeadlessExtraction`, `LogMasterSeed`,
  `StepSortieSeed`.
- Reason to change: a new command-line flag or process-wide side effect.
- What moved: the eight base paths (`_repoRoot`, `_dataRoot`, `_planesGamezPath`, `_zrdrPath`,
  `_soundsPath`, `_interpPath`, `_messagesPath`, `_rofPath`) are one `BasePaths` value, and
  `BasePaths.Resolve` is the one function `_Ready` and `LeaveExtractionScreen` both call. The two
  roots are locals of `_Ready` until the value is built; `ResolveSyntheticData` answers the root
  the run reads, and `StartHeadlessExtraction` takes both roots. `LauncherContext` carries the value
  as `Paths`, which `GameSession` holds and the five suites that build a session construct; the
  probe runner and `LaunchBoards` take the value too.
- Order kept: `Resolve` runs where the unpacked-folder preference ran, after
  `SessionPaths.ForceZipped` is set, and calls `PreferUnzipped` for the planes, zrdr and sounds
  archives in that order. Nothing between the data root's settling and that point read the raw
  paths. The hand-back now re-derives the interp, messages and rof paths too, which are the same
  strings, since neither the data root nor the spec changes.
- Why the rest stays: `_Ready` is one ordered sequence whose order is the behaviour (the log opens
  after the parse warnings, the memory admission before anything heavy, each early quit after what
  it needs).

### 5. Session lifecycle and load screen: stays (it is the composition root's job)

- Fields (12): `_session`, `_menuDriven`, `_exitDestination`, `_menuPads`, `_pendingDebrief`,
  `_pendingWrapup`, `_pendingLobbyLanding`, `_loadLayer`, `_launchFramesWaited`, `_loadStepsRun`,
  `_startHeldFrames`, `_startFade`.
- Members: `BeginLaunch`, `RunOwedLaunch`, `TryLaunchSession`, `LaunchSession`, `RaiseStartCover`,
  `DropStartCover`, `ShowLoadScreen`, `HideLoadScreen`, `StartSessionFromMenu`,
  `StartCampaignFromMenu`, `Unpack`, `BindMenuPads`, `OpenDebrief`, `RestartSession`, `ExitSession`,
  `ReturnToMenu`, `BlankAndQuit`, `LaunchedFrom`.
- Reason to change: how a session starts and ends. This is what the composition root is for, and
  every other group hangs off it, so it stays. What the load screen draws (`LaunchBriefing`,
  `LaunchSubject`, `CampaignLoadSheet`) changes with the decoded screens rather than with the
  lifecycle, so the second pass moved it with group 8; `ShowLoadScreen` keeps the layer and its
  timing.

### 6. Menu host, presentation and menu input: the host moved to `ProcessMenu.cs`, the rest stays

- Fields (13): `_menuHost`, `_menuWheel`, `_menuAudio`, `_originalLayout`, `_menuAid`,
  `_pendingJoin`, `_pendingWaves`, `_pendingWingmen`, `_pendingPreset`, `_buildStamp`,
  `_chapterCinema`, `_closingCinema`, `_pendingApply`.
- Members: `BuildMenuHost`, `OriginalAvailable`, `MousePosition`, `MenuPrimaryPressed`,
  `TakeMenuWheel`, `ApplyMenuDebugAids`, `OnMenuExit`, `ShowMenu`, `EnterMenu`, `MenuStep`,
  `_Input`.
- Reason to change: the menu presentations and their seat (Steam's on-screen keyboard, the
  stamp's messagebox, the pointer seat).
- What moved: `_menuHost` (now `ProcessMenu.Host`), `BuildMenuHost` (now the private `Build`,
  run by `ProcessMenu.Show` on the first show), `OriginalAvailable`, `MousePosition`,
  `MenuPrimaryPressed` and `TakeMenuWheel`, with the fields only they read: `_menuWheel`,
  `_menuAudio`, `_originalLayout`, `_menuAid` and the two cinemas. The pause leaf's body moved too
  (`PauseOptions`), since it reads the layout and the audio service; `BuildPauseOptions` stays as
  the one-line factory the context hands the session.
- Seam: the module is built in the launcher's constructor over the node, `LaunchAudio`,
  `NetFlight` and a live read of the paths, with `OnMenuExit` as its sink. The presentations still
  take the node, as the parent their screens go up under, which is a `Node` parameter rather than a
  reach into the launcher. `Show` takes the two specs and the build stamp for the first show's
  build, and keeps the aid's order (read before the host's `Show`, which creates the presentation
  that takes it, cleared after). `_Input` hands the wheel through `TurnWheel`. Every feature is
  added in the same order, the door among them.
- Why the rest stays: `ShowMenu` is entangled with the lifecycle (it is the end of every
  `ReturnToMenu`), and `OnMenuExit` is the sink that starts sessions. The Built-in debug aids
  (`_pending*`, `ApplyMenuDebugAids`), `_pendingApply` and `_buildStamp` stay with them, reading the
  host through `ProcessMenu.Host`.

### 7. Options apply and the graphics switch: moved to `LiveSettings.cs`

- Fields (3): `_pendingGraphicsToggle`, `_switchCover`, `_debugSwitchesDone`; it also dresses the
  sun and Environment of group 9, which it is handed at construction.
- Members: `PersistOptions` (now `Persist`), `SwitchGraphicsMode` and `RequestGraphicsSwitch`
  (now the private `Request`), `DropSwitchCover` (now `DropCover`), `SaveGraphicsMode`, the
  switch block of `_Process` (now `Tick`), and the two startup blocks of `_Ready`: the pacing
  and window (`ApplyWindowAtLaunch`) and the graphics mode and word settings
  (`ResolveGraphicsAtLaunch`).
- Reason to change: the Options page's settings and how each reaches the process, at launch and
  on an apply. The word settings registry, the water quality row and the four graphics settings'
  shared resolver each changed `Launcher` for this reason.
- What stays: `ApplyOptions` (it ends the presentation and shows the menu again) and
  `BuildPauseOptions` (it reads the menu host's layout, controls and audio). Both hand the
  choices to `Persist`. The two startup calls stay at their own points in `_Ready`, since the
  vsync read must follow the options directory and the graphics resolution must follow the
  `--det` block. The audio mix, haptics and three remake-only toggles read at launch stay in
  `_Ready` too: each is one line among the process-wide reads of group 4.

### 8. Load and pause boards: moved to `LaunchBoards.cs`

- Fields: none. Members: `ShowPauseSheet`, `ShowInstantActionPauseSheet`,
  `ShowMultiplayerPauseSheet`, `PauseAidReadout`, `CampaignMissionAt`, `SeatedMemento`, the
  `pauseboard` branches of `ShowMenu` (now `ShowPauseAid`), the board half of `ShowLoadScreen`
  with `LaunchBriefing`, `LaunchSubject` and `CampaignLoadSheet` (now `LoadScreen`), and the
  switch cover's blackboard (now `SwitchScreen`).
- Reason to change: the decoded pause and load screens (the Dogfight briefing on both, the
  campaign chart sheet, the Instant Action dialog, the load bar's fractions).
- Seam: clean. It holds no state; it reads the live spec and the three data paths, built per use
  because an extraction's hand-back re-resolves them. `SeatedMemento` and `CampaignMissionAt` are
  shared by the load screen's chart sheet and the pause door, which is why the load screen's
  content moved with the aids. The `loadboard` branches stay in `ShowMenu`, since they stand the
  launcher's own load layer.

### 9. Persistent rendering nodes and shader globals: stays

- Fields (4): `_camera`, `_sun`, `_env`, `_orbit`.
- Members: `SetupLighting`, the global shader parameter registration in `_Ready`.
- Reason to change: the world's lighting defaults and the shader globals.
- The issue's lead, "per-pane render setup", does not hold for this file: per-pane fog and the
  spyglass discs live in `FogViewTable`, `GameSession` and the camera modules. What remains here is
  about 80 lines of one-time registration, which shares `_Ready`'s ordering constraint (registered
  before the `--dump-*` branches build materials). Not worth a module on its own.

### 10. Audio, music and cinemas: moved to `LaunchAudio.cs`

- Fields (5): `_music`, `_musicArchive`, `_musicRng`, `_focusMuted`, `_cinemaShown` (the two
  cinemas are counted under group 6, whose campaign feature holds them). Constant: `MasterBus`.
- Members: `BuildMusic` (now `Build`), `PlayCinema`, `StopCinema`, `PlayBootSequence` (now
  `PlayBoot`), `ApplyMasterVolume`, `SetFocusMuted`, and the score's tick, stop, menu cue and the
  archive's close (`Tick`, `StopMusic`, `CueMenu`, `Exit`). `_Notification` stays, a node override
  that calls `SetFocusMuted`.
- Reason to change: the score, the films and the master bus.
- Seam: built in the launcher's constructor, so a focus change is answered from the first frame as
  before. The campaign's cinemas, the boot card and the `--movie=` run take `PlayCinema` as the
  `CinemaPlay` delegate they always took, now the module's method rather than the node's. The film's
  data root comes from `Build`, which runs before any film can play, and the master-volume warning
  reads the gain `ApplyMasterVolume` resolved; `--volume=` is parsed once and every derived spec
  carries it unchanged. Every call keeps its point in `_Ready`, `_Process` and `_ExitTree`.

### 11. Extraction screen: stays

- Fields (1): `_extractionScreen`. Members: `ShowExtractionScreen`, `RunExtraction`,
  `LeaveExtractionScreen`, `ApplyExtractionAid`.
- Reason to change: the in-game extraction flow.
- Its hand-back is now two calls, `BasePaths.Resolve` and `LaunchAudio.Build`, before `EnterMenu`.
  The screen itself stays: it is shown from `_Ready`'s ordered sequence, and its exits are the
  launcher's own (`EnterMenu`, `BlankAndQuit`).

### 12. Process quit and debug readouts: stays

- Fields (3): `_netReadout`, `_sinceNetReadout`, `NoRigs`. Members: `_ExitTree` (the ENet close
  first, then the task-queue drain), `DrainLowPriorityTasks`, `MirrorEngineLog`, `TickNetReadout`,
  `_UnhandledInput` (F10, F11, F12, Esc).
- Reason to change: process lifetime, and the `--debug-net` readout.
- Why it waits: about 120 lines with almost no state; the quit's order (transports closed before
  the drain, the drain before the instruments' exit, the log mirror last) is clearest kept in
  `_ExitTree` itself.

## What stays

The groups left on `Launcher` are the composition root's own: the bootstrap's ordered `_Ready`
(group 4 less its paths), the session lifecycle and load layer (group 5), the menu's show and exit
sink with Built-in's debug aids (group 6 less the host's construction), the persistent rendering
nodes and shader globals (group 9), the extraction screen (group 11) and the quit (group 12). Each
stays for the reason its section gives: its order is the behaviour, or it is what the root is for.
