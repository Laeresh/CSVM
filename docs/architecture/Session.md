# Session

The `Launcher` scene root, the per-launch `GameSession` node, and the low-coupling session-build clusters they delegate to.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Session/GameSession.cs
The per-launch orchestrator: `Launcher` constructs it from `(SessionSpec, LauncherContext)` and
`StartSession` runs ordered build phases over one local `BuildState`. It owns the session clock,
world root, panes, mode runtimes and resource lifetimes, delegating aircraft assembly and membership
to `FlightRoster`, world construction to `WorldSession`, and effects staging to `WorldEffectsFactory`.
After the synchronous build it constructs one `SessionSimulation`. `_PhysicsProcess` requests one
realtime step; `_Process` requests each parent-driven `GameClock` substep. Debug/CLI forces remain
outer-frame input injection. The nested runtime adapter maps named phases to concrete owners, which
do not self-step from Godot callbacks. Its mission-ending admission path holds both simulation and
authored animation while advancing only the campaign hand-off. All human and dynamic AI aircraft
enter through `FlightRoster`;
`AllAircraft` combines its AI view with ordered rig controllers for non-step consumers. Exit frees
the session subtree atomically and releases only the non-node resources this orchestrator owns.

## src/Session/SessionSimulation.cs
The session simulation: one plain-C# module owning hold admission and the exact order of flight,
combat, mission, radio, effects and match advancement. `Step(dt)` snapshots eligible AI membership
at entry, then advances incoming fire → projectiles → human aircraft → zeppelins → emplacements →
generators → surface vehicles → captured AI → landing approaches → Instant Action → campaign →
radio → smoke → tags →
AI voice → Versus. A generator-spawned aircraft therefore first flies on the next step, including
the next fixed-accumulator substep in the same rendered frame. A landing or campaign callback that
raises `SimHeld` halts the remaining phases immediately. An active ending hold advances only the
campaign hand-off; one raised by the campaign also rejects every later phase and same-frame substep.
Exceptions are fail-fast. Authored animation,
cutscene presentation, weather, lens flare, terrain extension and wall-time watches stay outside.
`ISessionSimulationRuntime` is the named recording/production seam; it is not a participant registry.

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
`SetupLighting` builds the session's one `DirectionalLight3D` and `WorldEnvironment` at
`WeatherRig.DefaultEnergies` and a hand-picked bearing, which is what a viewer, a menu or a
mission with no weather.json flies under. A mission that has weather overwrites both the bearing
and the energies per zone-apply (`WeatherRig.ApplyZone`), so these are defaults rather than the
level every flight renders at.
The default root is export-aware: editor (and editor-run builds) → the repo checkout
(`res://`'s parent — `GlobalizePath("res://")` maps to disk only there), exported build → the
exe's own directory; `CSVM_DATA_ROOT`/`--data-root=` override either.
`LaunchSession()` instantiates a `GameSession` per
launch; `ReturnToMenu(destination)` `QueueFree`s it and shows the menu again; a menu launch
derives its spec via `SessionSpec.FromMenu(_cli, …)`, never from the outgoing spec. `ExitSession`
is the boards' Exit item, handed down through `LauncherContext`: back to the menu at its top level
when the process launched into it, out of the game otherwise. The routing Esc used to do — Esc now
opens the pause board instead, so leaving a flight is reachable from a pad, and this one rule lives
here rather than being restated per board.

The menu is owned as a `MenuHost` (`src/UI/Menu/MenuHost.cs`), built by `BuildMenuHost` on the
first show and kept for the life of the process: a `PresentationRegistry` with the Built-in
presentation registered under `PresentationId.BuiltIn` (its factory closes over this node as the
parent, the data paths, the `--menu=` aid from `_cli` and the first seat's poller) and the Original
presentation under `PresentationId.Original` (over the layout the availability check loaded and
the same `--menu=` aid, read as Original's own values), the shared `FreeFlightFeature`, the first
seat (a `PointerSeat` over a `BuiltInSeat` over a keyboard-plus-unclaimed-pads `MenuInput`, the
viewport's mouse position and the left button as the pointer), the `MenuAudioService` over the
process's music channel, sound archive and the rof tree's `ASSETS/SOUNDS`, and `OnMenuExit` as
the sink. The active presentation is settled once there through `MenuHost.Select`
(`PresentationResolution.Resolve` with `--force-builtin`, `--presentation=` and the saved
`OptionsStore` request; availability is registration plus `OriginalAvailable`, which loads the
decoded layout through `OriginalAvailability`, keeps it for the factory and logs the optional
absences as their own `ui` line, and is asked again on every switch so a repaired tree is seen)
and logged as one `ui`
line with the requested and active ids and the fallback reason; an unknown, blank or unavailable
request falls back to Built-in rather than crashing, and a fallback never rewrites the saved
request. An `OptionsApplyExit` is acted on one frame later (`_pendingApply`, the exit
arrives inside the presentation's own tick): `ApplyOptions` saves both the presentation request and
the graphics word through `OptionsStore`, calls `Deactivate` (freeing the presentation and
discarding transient feature state), re-selects with the saved request in place of any
`--presentation=` override (the force flag still wins) and shows the top level. This is the only
writer of the options file anywhere in the codebase, which is what keeps a driven Options screen
in a test or a suite from writing the player's own. Nothing here gates a presentation on anything
but registration and availability: a saved `original` request selects Original on a cold start with
no flag, so Original's normal exposure is the two Options choosers' toggle plus `OptionsStore`'s
accepted token set, and the whole contract a further presentation registers against is
[`docs/menu-presentations.md`](menu-presentations.md). `ShowMenu(destination)` shows the host at a semantic
`MenuReturnDestination`, then does what is the owner's: the `loadboard` aids, the menu music cue,
and the one-shot `--debug-join=`/`--debug-waves=`/`--debug-wingmen=`/`--debug-preset=` aids
through `BuiltInMenu`, the one door onto the launchscreen (`Active as BuiltInPresentation`, null
under Original), also used for the failed-build note. The `--menu=` aid is the cold start's alone:
`BuildMenuHost` parks it in `_menuAid`, both registry factories read it when they run (inside the
first `Show`, so the first instance gets it and the fresh instance a switch creates gets none), and
the first `ShowMenu` consumes it, applying the `loadboard` aids once. Every later show, a return
from flight through `ReturnToMenu` or a debrief through `OpenDebrief`, lands on the destination
itself in both presentations. A failed build returns to the top level, logs one `ui` line naming
the active presentation, and shows Built-in's error line where Built-in is active; Original has no
note and its top level shows bare. Esc ownership and the capture director's menu flag read
`MenuHost.Shown`, never a node's visibility. `_Process` ticks the host last, after `RunOwedLaunch`,
which is where the launchscreen's own process callback ran when it ticked itself as a child.

`OnMenuExit` is the one way out of the menu: a `LaunchExit` becomes `StartSessionFromMenu`
(`FromMenu` over the chapter, the mode, the wizard's def and the seat choices unpacked into the
four parallel lists the factory takes, a campaign-exported custom plane's stored fit standing in
where a seat set none), a `CampaignMissionExit` becomes `StartCampaignFromMenu` (`FromCampaign`
over the profile and story position; it names no chapter and no mission, since
`CampaignDirector.ResolveSpec` reads those out of `cm_sequence` in the session's constructor, so
one place resolves a story position whether it came from a cabin or a `--campaign=` command line),
and a `QuitExit` quits the tree. The host has already hidden the presentation when the sink runs,
with its screens kept so a failed build can show it again where it stood. The campaign's return
leg is `CampaignMissionEnded`, handed down through `LauncherContext` and non-null only in a
menu-driven process: a campaign mission's end, won or lost, queues the profile name alongside its
`CampaignMissionResult`, and the next `_Process` frees the session and shows the menu at a
`DebriefReturn(profile, seq)`, which each presentation maps onto its own book with the cabin on its
far side. Queued rather than acted on directly, because the mission ends inside the session's own
physics step, which is no place to free it. The boards' Exit is `ExitSession`, a
`ReturnToMenu(TopLevel)` in a menu-driven process and a quit otherwise; `RestartSession` rebuilds
without touching the menu. These three, the failed build's return and the two sinks above are every
way a session hands control back, and each is a destination or a quit, never a screen name.
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
`SetupLighting` keeps `_sun.ShadowEnabled = false` in the faithful path and calls `EnableSunShadows`
in enhanced graphics mode only: PSSM 4 splits, blended, splits 0.06/0.17/0.42, bias 0.05 and normal
bias 1.25 (TUNE, the pair judged against acne at C1's 25 degree sun and against peter-panning of the
biased road decals), `LightAngularDistance` 2.0 degrees so a cast edge on flat water reads as a soft
penumbra rather than a hard line, and a `DirectionalShadowMaxDistance` that is a FALLBACK: a flown mission
overwrites it per zone from that zone's pushed-out fog far (`WeatherRig`). The world meshes and the
aircraft are the only casters; every other population is already `ShadowCastingSetting.Off` or
declares `shadows_disabled` in its shader. The front-culled world needs no `DoubleSided` casting:
the source's visible side is Godot's back face, which is the face the sun sees. `EnableWaterReflections`
is the same mode gate on the Environment's SSR, for the one glossy population `SceneBuilder` builds;
what it can and cannot reflect is measured in `PLAN-enhanced-graphics` C24.
`UseMissionSky` is the same mode gate on the Environment's sky: enhanced mode swaps Godot's
placeholder `ProceduralSkyMaterial` for a flat `PanoramaSkyMaterial` carrying one colour, which
`WeatherRig` rewrites per zone from that zone's `FOG_COLOR`, and takes the ambient off the sky so
the authored `SUNLIGHT_AMBIENT` keeps it. The faithful path keeps the procedural sky, which it
never shows: the dome is gamez geometry drawn over the background.
`SetupLighting` also sets SSAO on the enhanced `_env` (radius 2.5, intensity 2.0, power 1.5, detail
0.5, horizon 0.06, sharpness 0.98; all TUNE for this world's street-canyon scale). The faithful path
never enables it, since SSAO reads ambient light and the unshaded path has none for it to modulate.
`CockpitOverlay.NewOverlay` duplicates `_env` at build time, so the interior pass inherits the same
settings automatically.
`EnableGlowAndTonemap` is the same mode gate for `_env`'s glow and tonemap: `GlowHdrThreshold` 1.0
with `GlowBloom` 0 so only the glow-arm sprites (`SceneBuilder`'s `col.rgb * 1.5`, the only pixels
enhanced mode pushes above 1.0) bloom; `TonemapMode` AgX with its own white/contrast pair recovers
the day chapters' far-ridge washout instead of clipping it (all TUNE, judged against C1/C4/C5
captures in `PLAN-enhanced-graphics` C22). `CockpitOverlay`'s duplicated `_env` inherits
this too, so the interior pass tonemaps once, the same as the world pass. See "Rendering: the
enhanced graphics mode" above for the divergence record as a whole.
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
airframes): flight rigs, AI spawns and the static `--plane`/`--damage`/`--viewer` views alike. An
AI spawn wears an explicit scheme first (the Instant Action ace's `ace_*` livery), then its own
def's `paint_*` scheme (`DefScheme`, via `AiFlightAssembler.MilitiaScheme`), and reaches this
default only when neither is authored. That default is a straight catalog lookup and consumes no RNG draw, so pinned liveries are unmoved by it;
`--paint=none` is the only way to the bare shipped skins, and an absent `player_fortune` catalog
entry (no vehicle.json) falls back to them with a one-line note. The Instant Action enemies are the
exception (`SchemeFor`'s `useDefaultPattern: false`) — see `InstantActionRuntime.cs`'s entry for why.

## src/Session/SpawnPicker.cs
Resolves each player's flight spawn:
`ChooseSpawnBase` (the shared `--spawn=`-or-random list index), `ChooseSpawn` (a player's
position/look-at from that list, objectives.json `PLAYER_INIT`, or the `--spawn-at=` debug
override), and `LogSpawn`. Constructed once per session build (`_spawnPicker`, same lifetime as
`LiveryResolver`). Also the plain `IFlightStarts`: `ChooseStarts` just loops its own `ChooseSpawn`,
which is the placement every session flies except a splitscreen race or a co-op campaign mission.
`StartGrid` delegates to
`ChooseSpawn` for its anchor, and the weapon lab and freecam spectator call it directly, so this
type stays the single owner of spawn resolution.

## src/Session/IFlightStarts.cs
Where every pilot in a session starts: `ChooseStarts(spawns, missionZrdrPath, spawnBase,
playerCount)` returns one `FlightStart` — the same `(pos, lookAt)` pair `FlightController.Setup`
already took — per player. Two implementations: `SpawnPicker` (the plain per-player walk of the
mission's spawn list) and `StartGrid`. `HumanFlightAdapter` holds the interface and resolves the
field lazily on its first `Assemble`, so the resolve still happens where it always did.

## src/Session/StartGrid.cs
The abreast starting grid and second `IFlightStarts`: it fans slots symmetrically across one anchor
heading, then lifts the whole field by its lowest terrain clearance. Anchor selection stays with
`SpawnPicker`, preserving mission `PLAYER_INIT`, Instant Action lists, fallbacks, and `--pos`.
Heading comes from the anchor's position-to-look-at pair so `--direction` survives. Terrain is an
injected `Func<Vector3, float?>`; production uses `GameSession.GroundSampler()`.
`startGrid.slotSpacing` and `startGrid.groundClearance` are registered TUNE values. `GameSession`
selects this grid for multiplayer campaign sessions and eligible splitscreen stunt races; solo,
Dogfight, and deterministic scripted race starts keep their own placement paths. Geometry and
terrain lifting are pinned by `CSVM.Tests/StartGridTests.cs`.

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
handler that logs the lives left or hands the pane over through `Session/SpectateHandoff.cs`, which
the campaign's own loss rule shares; and the whole-window wrap-up board,
its counters summed across every seat (`enemiesShotDown` filtered on `killer != null` — a bare
terrain crash never reaches the take-hit body the original counts in — Shot % through
`ProjectilePool.ScoredShooters`, zones read live at `MissionEnded` time, P1's stunt best recorded
under the mission's own score key). `BuildStuntSummary` (`BL-426`) is the record's own gate: it
gets THIS run's own `AllComplete`, never the mission's win/loss flag, since a splitscreen mission
ends once every pilot is done OR spent, so P1's own zone set can still be short when the mission
itself ends on the other pilot going out of lives — each pilot's summary reads that pilot's own
`StuntMission`, so the gate is per-pilot by construction. The stored `prevBest` is read before the
gated record either way, so a run that does not qualify still shows the true stored best instead of
nothing; reading it after the record would show a completing new-best run its own just-written time
as "previous". A failed run's working default, pending the author's own judgement, is to show its
elapsed total with no NEW BEST flag — the original's own behaviour here is not decoded, and whether
an already-poisoned `user://stunt_scores.json` needs invalidating is a separate open question this
item does not settle.
`ForceDebugScoreboard()` is `--debug-scoreboard`'s single-fire force, attributed to P1:
`dogfight_ace`/`dogfight_squadron` through `DebugForceCrash`; `stunt_flying` needs nothing,
already forced by `HumanFlightAdapter`'s own `DebugCompleteStunt` wiring; `zeppelin_run` has no
force, its verification drove the mode through real damage.

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
own `objective` entries plus every roster block that authors the flag on itself
(`CampaignDirector.RosterObjectiveMarkers`, aiv slot 37 — CM11's `secfury_5`/`secfury_6`, the
shipped case), minus whichever of those a completed objective's `REMOVE_OBJECTIVE_TARGET` names,
plus whatever `ADD_OBJECTIVE_TARGET` has added: `ObjectiveGraph.ObjectiveTargets` alone starts
empty and a mission that only ever REMOVES its sites would offer nothing, CM11's own OBJECTIVE1
included, which removes both stunt planes without ever adding them. One `ObjectiveSite` instance
lives as long as the mission flags it, since the selection is held by source identity; its position
and labels are re-read every frame, which is what tracks a site under a moving node. `PointFor`
prefers the bare `TRAVELERS` point of the objective that edits a target over the world node of the
same name, because C3/M01's village node stands at the world origin. Next, a roster marker reads
its own spawned `FlightController`'s live `WorldPosition` (`RosterAircraftPosition`): a roster
aircraft with no chapter-gamez library root under its own block name is never indexed on
`AnimRuntime` (`RosterMarkers.Attach` only indexes one when that root exists), so `Resolve`/
`SiteAnchor` below can never find it. Otherwise a site on a world node is marked at `SiteAnchor`,
the centre of the world bounding box of everything that node draws, which is what the
original publishes for a mission structure (`docs/org/targeting.md`); its own position is only the
fallback for a node that draws nothing. C1/M05's balloon groups stand on the water with the balloon
16 m above them and C2's `sghangar` stands at the world origin, so the node's position is not the
site. A site carries the team of the node it stands on where that node is a mission structure, and
neutral otherwise, which is the original's own split: a record naming a flagged node keeps that
object's team, and a record that has to build its own builds it neutral. Almost every site is the
second case, since a group is flagged on a child (a roster-aircraft site is always this case, since
`RosterAircraftPosition` resolves no `DestructibleRegistry` instance). A site is keyed by
`ObjectiveTarget.Key`, and `ResolveTarget` walks a path one name at a time with `FindNodes` scoped
to the node before, so `piratezep/rock_zeppelin` is the hull's own child and a bare name is the
first global match; `targets.zrd` is looked up by the whole key first (a path-authored entry
keys `parent/child` there too) and by the path's last node as the fallback. The help label reads
the graph's own `SET_HELP_LABEL` first, then the roster block's own slot 39, then `targets.zrd`'s.
The marker's verb, proper name and colour all come off that table through `Messages`
(`Zeppelin [Disable] -` over `Worker's Voyage` in red, `[Dock] -` over `Worker's Voyage Docking
Hook` in blue), and a site whose key finds no entry falls back to its node name, which is what a
table loaded from the wrong scope looks like. `GameSession` binds it through
`FlightRoster.SetTargetObjectives`. Pinned by `campaign-objective-markers`,
`campaign-objective-target-path`, `campaign-objective-labels`, `campaign-race-chain` (the
hangar anchor, and C2/M03's race chain of per-zone objectives with a racer-death DEDG each) and
`campaign-cm11-stunt-marker` (the roster-marker source, over CM11's own built roster and graph).

## src/Session/CampaignHumanField.cs
Engine-free objective rules over every joined human, represented by `HumanState` position, captured
group, and wreck state. `CampaignDirector.World.SnapshotHumans` is the sole producer. `Travelers`
uses the nearest human, so approaching succeeds on the first arrival and departing on the last
exit; wrecks continue reporting their positions. `LiveInGroup` counts non-wrecked humans in a
captured group and ignores temporary cutscene inertia. The scripted player remains a separate P1
identity for authored `player` tokens, roster leaders, and anchored net trailers. Rules are pinned by
`CSVM.Tests/CampaignHumanFieldTests.cs`.

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
an empty world as "the group is wiped out" would win missions on the first tick. Every condition on
that interface reads the whole human field rather than one aeroplane
(`Session/CampaignHumanField.cs`), which is why the interface doc carries the scripted-player split
too: an implementer that answers `TravelersMet` off P1 alone leaves a co-op mission unwinnable by
anybody else.
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
`ResolveSeatedPlane` runs in the same constructor chain, last, and is the command line's
counterpart to the cabin's seat: a `--campaign=` launch passed no launchscreen and so names
nobody's aeroplane, so the profile's `SelectedPlane` is read here (with its `CustomPlaneStore`
build, or its award template where a granted aircraft has no file) and installed through
`SessionSpec.WithSeatedAircraft`. `--plane=` entry 0 beats it and warns naming what it overrode,
which is what lets a golden pin a co-op campaign shot with no hand-authored profile on disk;
entries 1 and up name guests and are left alone. The warning fires on the NODE differing, not on
where the name came from, so a cabin launch (whose entry 0 is already that node) stays silent
without this needing to know which factory built the spec.
The world adapter answers TWO questions that both used to be spelled "the player", and keeping them
apart is what makes a co-op mission playable: `Player()` is the scripted player, `WorldInputs.PlayerAircraft`'s
one aeroplane, always P1's, and it is what the lost ending, the music damage ping and the escort
leader read. `SnapshotHumans()` is the human field, `WorldInputs.Humans` (`GameSession.HumanAircraft`,
the rigs' controllers and no AI) refilled into one reused buffer and handed to
`Session/CampaignHumanField.cs`. Three reads moved onto the field: the `TRAVELERS` condition whose
subject is the authored `player`, the `DEDG` walk's human arm, and the danger-zone update, which is
now driven per human so a gate pair may be split between two of them. A session that names no field
is one where the scripted player IS the field, which is every solo sortie and every suite that
builds one rig, so the co-op read and the solo read stay on one code path and a 1P mission answers
byte-identically. `HumanRigs()` is that same field held as the aeroplanes themselves, for the two
seams that need one rather than a reading of one: the per-seat death wiring and the wreck wait.
⚠ A `TRAVELERS` with an empty field still falls back to
`WorldInputs.ListenerPosition`, because a suite that builds no rig at all resolves its conditions
that way and always has.
`TryCreate` loads the profile and the script on the same "a failure warns and flies without a
mission" contract `InstantActionDirector.TryCreate` has. `Attach(WorldInputs)` arms the graph once
every runtime a directive can touch is up, applies the chapter's persist log and hands the world's
`DangerZoneRibbons` to every roster pilot; `Step(dt)` is
called from BOTH of `GameSession`'s drive paths. Each step also walks the roster for a wingman
whose escort leader has left play and seats it on that leader's own patrol net
(`TakeLostLeadersNets` through `SeatOnNet`, the body `SET_AI_NET` shares), since a netless escort
holds the orders its last pursuit wrote once its leader is gone; a leader flying no net, which is
every player-led escort, leaves its wingman untouched and is reported once. Every graph transition is one
`[campaign] objective N woke|napped|completed|killed|slept|expired [by M] [for Ns] at Ts` line
through `Log.Info`, so the file sink carries the chain a sortie report is about. `WireScoredShooter`
registers the scripted player's aircraft into `ProjectilePool.ScoredShooters`, deferred into `Step`
like the damage ping because that aircraft is built after `Attach` runs, so the recorded attempt's
`Shots`/`Hits` read the seated pilot alone and a guest's cannon fire is never counted (decision 13).
Mission end records the attempt through
`CampaignProgression`, merges `CampaignPersistLog.Capture` into the profile, saves it, writes any
aircraft award's build into `CustomPlaneStore` (`SaveAwardedBuilds`, which is where the cabin's
launch looks a plane's fit up by name), and starts the LEAVING HOLD; `ReturnToCabin` and
`MissionEnded` come at the far end of it, `LeavingHoldS` (2 s) later, and the cabin screen itself
is C22's. The hold is the original's `FUN_00443090`, which records the result and then pushes its
"Fade State" over a copy of the frame the ending landed on for that fade's default 2 s before the
next screen takes the state machine: the world is not advanced and the stick is not read while it
runs, so the last flown frame is the frame the ending landed on. `Leaving` says the hold is
running; `GameSession` reads it in BOTH drive paths ahead of everything else, holds `GameClock`'s
sim on the realtime one, and steps nothing but the director until it expires.
Which directives reach the engine today: `INACTIVEn` (node visibility, the decoded active bit),
`ANIM_STATE` (`AnimRuntime.AnimStateOf`), both forms of `TRAVELERS` (the node form against
`ListenerPosition`/a named node; the group form tallying the spawned roster's live, non-inert
members of the named group inside or outside the radius against `spec.Count`, the decoded
`FUN_00465b40` shape `docs/formats/objectives.md` already carried), `WAKEUP_TURRETS` /
`WAKEUP_ZEP_TURRETS` (`TurretEmplacementRuntime.SetActivatedUnder`), `WAKEUP_GENERATOR`
(`AiGeneratorRuntime.GrantWaveCapacity`), `DEDG` over the spawned roster, every generator launch
booked into it under its launch name (`RegisterGeneratorLaunch`, carrying the template's group, so
C5/M04's `DEDG [5, 0]` counts Miles from the moment the Dante drops him), plus the human rig when
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
its PLACED pose (a world node of the block's name where one exists, its authored spawn otherwise,
never a stale copy of the plan), or a dormant `ZeppelinRuntime` record put into the world.
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
handoff speed), books a block's own objective-target flag and label into `RosterObjectiveMarkers`
(`RegisterObjectiveMarker`, aiv slots 37/39, also called from the generator-launch and surface
paths), and logs one `campaign: roster '<name>'` line per block. A block whose
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
`DamageApplied` (the music ping) is subscribed on the aircraft `WorldInputs.PlayerAircraft` answers
and re-subscribed whenever it answers a different one; `Downed` (the lost ending) is subscribed the
same way but per SEAT of `World.HumanRigs()`, the human field held as the aeroplanes themselves
rather than as a reading of them. Both re-subscribe by identity, because an airframe swap rebuilds
a rig and a death in the new aeroplane has to count too. `BuildRoster` also stamps each spawned
rig's `FlightController.Group` from its plan.
Losing the aircraft is the graph's fourth ending, in the original's two stages, with a human field
widening each: the LAST seat's `Downed` report closes the graph's gate, and the mission ends once
no human's wreck is still falling. An earlier seat's death only takes that human out of the flight
(decision 9 rejects respawning): the seat latches down, and `WorldInputs.BeginSpectate` hands that
pane over through `Session/SpectateHandoff.cs`. ⚠ The director decides WHEN and builds no camera
itself, which is the same "this class adds no node" rule the rest of it keeps. The last seat is not
handed a camera, because the mission ends with it and there is nothing left to watch, which is also
what keeps a 1P death answering exactly as it did before there was a field.
`EndsOnPlayerDeath` (false under `--no-crash-loss`) is the only switch; `GameSession` is its writer.
⚠ It gates the whole rule, the spectate hand-off included: a debugging session flying on past a
crash needs its wreck unpinned so `R` still flies it again, and a pane already given away would
leave that pilot blind.
A lost attempt is recorded like any other and commits nothing to the persist log, so a retry starts
from the chapter state the profile already held (`CampaignPersistLog.CommitsOn`). It does count
against the mission's own failed-attempt counter, and the fourth failure of a mission never yet
completed raises the skip offer (BL-622/B14, `docs/org/debrief.md#the-four-attempt-skip-offer`):
`OnMissionEnded` then captures the lost world's destruction state into `CampaignMissionResult`
alongside the chapter, because the offer's Yes is a synthetic win whose save gate writes exactly
that state and the capture cannot be taken once the world is down. The offer is carried, never
asked here: the screen that shows the result is what asks it (`CampaignProgression.AcceptSkip`).
`BuildRoster` also wires each spawned aircraft's `Downed` report to `CreditKill` (BL-622/B13): the
player did it (`killer == WorldInputs.PlayerAircraft().PlayerIndex`), the victim's roster-authored
`Team` is hostile to `AimAssist.PlayerTeam`, and `UI.PlanePickerRoster.AirframeOf` resolves the
victim's `PlaneNode` back to one of the eleven stock airframes; the roster plan's own `Ace` flag
(slot 67) then picks which of `_kills`/`_aceKills` the mission-end attempt reads
(`docs/org/debrief.md#what-the-tallies-count`). A generator-launched aircraft's kill is not yet
credited: `AiGeneratorRuntime`'s launches never enter `_roster`, so this is a known gap rather than
an invented behaviour.

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
SCRIPTED PLAYER, P1's rig). ⚠ Not the human field, and one of the two reads deliberately left off
it: an escorting block follows one aeroplane, and a leader chosen from whichever human is handiest
would hand the wing a different lead every mission (`Session/CampaignHumanField.cs`). The `handover` argument is the airframe-swap counterpart of the wingman override:
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
escorting block's `primary_target` alone, because there it names a leader and not a target. A
plan's `InitHealth`/`Armor` (`AiSkills.RosterInitHealth`/`RosterArmor`, aiv slots 7/66) travel
through `SpawnFor` onto the `AiSpawn` record; `AiFlightAssembler.Assemble` is what applies them. A
plan's `ObjectiveTarget`/`HelpLabel` (slots 37/39) are not applied here at all: `CampaignDirector`
reads them straight off the plan at spawn to book its own `RosterObjectiveMarkers`.
Pinned in `CSVM.Tests/CampaignRosterPlanTests.cs`, `RosterDurabilityOverrideTests.cs` and
`RosterObjectiveMarkerTests.cs`; the placement half is the `campaign-roster` suite, the generator
half the `generator-roster-params` suite.

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

