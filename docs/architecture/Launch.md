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
The per-launch orchestrator: `Launcher` constructs it from `(SessionSpec, LauncherContext)` and `StartSession` runs ordered build phases over one local `BuildState`. It owns
the session clock, world root, panes, seats, mode runtimes and resource lifetimes, delegating aircraft assembly and membership to `FlightRoster`, world construction to
`WorldSession`, and effects staging to `WorldEffectsFactory`. `_rigs` is the pane list every camera-anchored system reads; `_seatRigs` is the whole field, a network match's
guests included, and sizes the roster, the spawn walk, the versus board, the respawn rotation, the human-aircraft step and a campaign's human field (`HumanField`), so a guest flown elsewhere counts at its interpolated pose, and a co-op campaign of more than one seat starts on `StartGrid` whatever its local pane count; `TakeGuestLeft` retires a seat whose guest walked out. A context transport opens a `Net.NetSession` in
the constructor, before the world, so a host can answer a join it has not built for yet; `AwaitNetJoin` pumps that wire until the host's seed, seat and roster have landed,
ahead of `Rng.Reset` and the seat sizing, and the handshake's clock opens the `NetClockSlew` advanced each frame, which `WireNetClock`'s `NetClockPing` round trip corrects from the first step. `WireStartGate` arms the `NetStartGate` before the build (a guest claims its words before the join's pump, where a host's early hold lands), whose last act holds the clock on it (`GameClock.StartHeld`) until every machine has loaded, with only the wire stepped meanwhile. After the synchronous build it constructs one
`SessionSimulation`, which owns the step order; both step paths step the wire first, so an arrival is applied on the step after it landed, and the human-aircraft phase
puts every seat flown here on the wire on the `AircraftStateCadence` as the SIM pose, while a sample for a seat flown elsewhere reaches that seat's own pose buffer.
`WireNetCombat` puts combat on the same wire: an owner's fire event spawns the round on every peer, the shooter's machine decides a hit and addresses the victim's owner,
that owner applies the damage and reports its own death (a match from its own `Downed` handler, any other mission from the one `WireNetCombat` adds, which is what plays a guest's wreck in a host's campaign field), and the host alone scores it and relays each of those between guests. A match death's kill lines follow the host's scoring as a death notice, so every machine posts them once. `WireNetSpawns` puts placement on it under one rule: the OPENING spawn is the shared seed's own walk over the mission table and crosses no wire, while every later return is GRANTED, a downed seat asking the host and the host's single rotation answering the whole field with a table entry every peer applies through the same call the owner would have made locally. `WireNetMatch` makes the host the only writer of the match itself: it sends the clock, both limits and the ending as one reliable message, change-driven (a rematch, an ending) plus a `MatchStateCadence` tick a second that carries the host's session clock into every guest's `NetClockSlew`, and a guest hands its `VersusMatch` over rather than advancing a clock or arming a limit of its own. ⚠ The ending is sent AFTER the scores that settled the round and never from the match's completion event, which fires before them. The scoreboard itself is never sent: every machine derives it from the scores it was already sent seat by seat. `WireNetDirector` puts a campaign mission's objective graph on the same wire through `NetDirectorLink.cs`: the host's graph publishes every event it raises and a guest's is replicated, so it follows them and decides nothing. `WireNetWorld` hands the AI aircraft and the world's destructible pools to `NetWorldLink.cs`, admitted from the capture phase and sent after the AI phase: the host flies every AI and spends every world hit, and a guest's AI fly from the host's samples while its pools spend nothing of their own. The landing trigger and the ladder switch read `_seatRigs`, and `WireNetPositionalStarts` hands their decisions to `NetPositionalStartLink.cs`; an airframe swap wires its replacement for combat again through `WireSeatCombat`, since that wiring is per controller.
Under a Versus lives rule a pilot out of lives is held spectating and its respawn refused (`VersusMatch.OutOfLives`). A guest builds no rotation of its own, and a field larger than the table is served by that rotation relaxing its one-living-seat-per-point rule rather than failing. A `--ctf` match adds `FlagRuntime` (`WireFlags`), stepped ahead of the match clock, and a `--zvz` match `ZeppelinVersusRuntime` (`WireZeppelinVersus`), which also answers a return with `SpawnAtMessage` and whose board's Restart leaves for the lobby rather than rerunning on burnt hulls, and any Dogfight whose world holds rearm nodes `RearmRuntime` (`WireRearmBases`). `AllAircraft` combines the roster's AI view with the ordered rig controllers, and `OrderWaveAirframes` with `StepOwedLoad` puts the coming waves behind the load screen.
Exit frees the session subtree atomically and releases only the non-node resources it owns; the prohibitions that keep these rules true sit on the members they bind. Read `SessionSimulation.cs` next.

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
