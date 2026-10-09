# Testing

The in-engine assertion harness behind `--run-tests`, plus the `CSVM.Tests/` xUnit project's engine-free reader units and golden invariants. The `--dump-*` probes, the capture loop and the glTF export the suites also drive live in `Tooling.md`.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## CSVM.Tests/
The xUnit project `dotnet test` runs (net8.0, `ProjectReference` to `CSVM.csproj`): the
engine-free reader units (`Zrdr`, `WavFile`, `SoundDefs`, `WeaponDefs`, `Messages`,
`SessionPaths`, `GameZ` transform arithmetic, `MarkerRig`, `AnimDefs` and the rest) plus the
golden invariants over a player's own `extracted/`. `TestData.cs` owns both input sources, the
hand-authored `fixtures/` tree and the `CSVM_DATA_ROOT` lookup behind `[ExtractedDataFact]`, and
states the rule binding each. A check that needs a live Godot belongs in `CSVM/src/Testing`
instead; read `TestHarness.cs` for that half.

## src/Testing/TestHarness.cs
`--run-tests[=filter]`: the suite registry, `TestContext` (assert verbs, resolved data paths, a
scene-tree host, `SyncPhysics` for the space a one-frame run leaves behind, and the `WithWorld`
chapter-world builder over `WorldSession`), the PASS/FAIL/SKIP table, `test-report.json` in
`TestContext.ScratchDir` (its `syntheticData` names a synthetic data root), and the exit code.
Its input gates SKIP a suite and name what it lacks: `RequireData` (a file) and `RequireZrdrEntry`
(a reader file in a zrdr ZIP or folder) on every tree, `RequireTexture` and `RequirePlane` (a shipped name) on the synthetic one.
`Select` is the pure flag selector, `SkipFailures` the SKIPs a tier makes FAILs and `SuiteShards` the shard term. The world
cache and its eviction, the mission-override and private-world forms, `DecodeCache`, `StartupProfile`, the queued-free flush and the engine-error allowlist carry their rules at their members. After a suite that grew Godot's static memory it collects and finalizes, so dead wrappers free their sessions before the next suite (`CollectAfterStaticGrowth`). A destroyed `TestWorld` takes the shader cache's tracked materials with it. Under `--debug-mem` the harness logs a `MemoryCensus` line per suite, with the cache's tracked count. Read `SuiteCatalog.cs` for registration, `PhaseAttribution.cs` for time.

## src/Testing/FinalizerGate.cs
The `--debug-finalizers` instrument: `TestHarness.Run` wraps each suite in one gate, which parks the
.NET finalizer thread on a sentinel, forces a collection every few milliseconds while the suite runs,
then releases and drains the queue before the next suite. A Godot wrapper finalized after its object
was reached again natively then logs its error at the boundary of the suite that dropped it, and the
gate's own `finalizer gate` line counts those throws. Off unless the flag is given. Read
`docs/verification.md` for the binding states it exposes.

## src/Testing/SuiteShards.cs
Godot-free and pure (`CSVM.Tests` proves it without the engine): the `shard:<index>/<count>` term
and the division behind it. `Parse` lifts that term out of a `--run-tests=` value and hands the rest
back as the selector; `Plan` divides an already-selected list longest-unit-first onto the lightest
shard and returns each shard in the input's order, so one tree always divides the same way.
`SuiteWeights` reads the measured per-suite seconds in `analysis/engine-suite-weights.json`, with a
default for a suite the file does not name, `Groups` for the sets a shard may not split and `Alone`
for the suites that take a shard of their own. Read
`TestHarness.cs` for where a plan is applied.

## src/Testing/SuitePorts.cs
The one table of where each suite that opens a real socket opens it: an offset into this process's
port block, `Net/NetPorts.cs`'s base up to `Block` ports above it. `RunTests.ps1` hands every
engine shard its own base, and these offsets keep one shard's suites apart, so no two live sockets
share a port. `Walk` bounds each suite's fallback walk and `At` turns an offset into a port. The
report's `shard.netPortBase` is the base the process used, which `RunTests.ps1` checks.

## src/Testing/LoopbackMaster.cs
The master server's socket side in one process, for the WebRTC suites: a host is given a code, a
guest naming it is numbered from 2 and announced, and a signal reaches only the end it names with
the sender written as its source. It hands out no ICE servers, so a link stands on this machine's
host candidates. The list, the expiry and the limits are the server's (`server/MasterServer`).

## src/Testing/PhaseAttribution.cs
Godot-free and pure (`CSVM.Tests` proves it without the engine): buckets a `StartupProfile`'s raw
phase names into archive/decode, sound preparation and runtime/world construction, and does the
suite-level arithmetic (`Rest`, `Overrun`) behind the console phase suffix and `test-report.json`'s
per-suite and run totals. Which phase name lands in which bucket, and what happens to one that
lands in none, are stated at the sets themselves. Read `TestHarness.cs` for where a live profile is
fed in.

## src/Testing/CountingEmitterFactory.cs
`IEmitterFactory` for a suite: `Create` always succeeds and hands back a `CountingEmitter` that
holds no Godot type in its own state, just started/stopped counts, whether it is sustaining now and
the last position it was fed. Reached by installing it on `TestContext.EmitterFactory` before a
`WithWorld` build; `Built` is the list a suite reads to confirm the fake was reached rather than a
real `Puffer`. Read `RecordingEmitterRenderer.cs` for the seam one level lower.

## src/Testing/RecordingEmitterRenderer.cs
`IEmitterRenderer` for a suite: it keeps the particles a `Puffer` hands it (`LastFrame`, `Shown`,
`MaxShown`, `MaxIndex`, `MaxFrame`, plus the `Capacity` the emitter sized) instead of drawing them,
so a suite asserts on burst, distance-trail and sustain with no atlas, `TextureArchive` or
`MultiMesh` in the path. The mirror of `CountingEmitterFactory` one seam lower: that fake replaces
the whole emitter so `EmitterDirector`'s lifetime is assertable, this one replaces the draw so the
emitter's own modes are. Neither covers the other's job.

## src/Testing/SuiteCatalog.cs
The registry of the in-engine assertion suites, discovered from the `[Suite("name", "what")]`
attribute each body carries in the `*Suites.cs` modules; the catalog keeps no per-suite table, so
adding a suite means adding one marked body in one of those modules and nothing else. `QuickTier`
and `CiTier` are the memberships of `tier:quick` and `tier:ci`; `Tier(name)` resolves each into a
`SuiteTier`, whose `SkipFails` (the ci tier's alone) makes a member's SKIP fail the run. The
determinism rule behind the alphabetical order, and why a malformed declaration throws rather
than being skipped, are stated at the members. Read `SuiteShards.cs` for what depends on that
order, and `docs/tooling.md` for each tier's selection rule.

## src/Testing/*Suites.cs
The in-engine scenario bodies, one module per domain: the emitter model, combat and ordnance,
Instant Action, AI, targeting, wingmen, the campaign and its menus, music, zeppelins, damage and
destroy choreography, animation and effects, the built world's data gates and censuses, and the
landing, capture and coop surfaces, and the two-session network harness that stands a host and a
guest `GameSession` up in one process over a loopback mesh. Each module holds `[Suite("name", "what")]` bodies,
a `static void` taking a `TestContext`; `SuiteCatalog` discovers them by that attribute, so adding
a suite means adding a marked body to the module that already covers its domain, or a new module
when none does, and registering nothing anywhere else. The membership itself is the catalog's
output: `--run-tests` prints the table and writes `test-report.json`. They reach the harness only
through `TestContext`, and shared fixtures are separate focused modules (`SuiteConstants.cs`,
`BurstTimeline.cs`, `SuiteViewers.cs`, `EffectStageSuiteHelper.cs`, `BotSuiteHelper.cs`, `MenuSuiteHost.cs`) rather than
an all-purpose helper. Per-suite traps live as comments on the suites themselves, in code.

## src/Testing/SuiteConstants.cs
The shared golden inputs used by more than one scenario module: airframe and weapon counts, puffer
timing, the destructible census, texture samples, and the ordnance-burst step and slack. It also
reads a `player.json` float back raw (`PlayerGlobal`), so a check can hold a typed field to the
record it came from on any data tree.

## src/Testing/BurstTimeline.cs
The three value types describing an authored ordnance-burst timeline and its observed dispatches.

## src/Testing/SuiteViewers.cs
Builds a test pane camera at a supplied world position for suites that exercise `ViewerSet`.

## src/Testing/EffectStageSuiteHelper.cs
Builds and frees a production-shaped, pooled effect-template stage for mesh-visibility suites.
`WithAnimSource` hands an effect suite its anim program, template gamez and scene builder, and
`WithAnimWorld` a destructible suite its world root and bound runtime. On an extraction both are the
chapter world; under `--synthetic-data` they are the `effects` family's invented records
(`Tooling/SyntheticEffects.cs`), the world a private one of its destructible roots.

## src/Testing/BotSuiteHelper.cs
The readings the bot suites share: lifting a pilot clear of the ground, the spawn-table entry a
placed aeroplane stands on, the Dogfight's ranked board as one line, and a pane's message stack.

## src/Testing/MenuSuiteHost.cs
The launchscreen fixture a menu suite stands a `LaunchMenu` on. `Bare` builds a `MenuHost` over an
empty `PresentationRegistry`, a silent `IMenuAudio` and a caller-owned exit list; `AddFeatures`
registers the same Free Flight, Instant Action, player-setup, hangar, campaign and controls
features the launcher wires; `Build` stands a launchscreen on a host over the suite's own
`ScratchPlanes` store, and `Menu` does so on a bare host. Presentations take that store too, so no
menu suite reads `user://Planes`; `DropScratchPlanes` removes it. Seat 0 joins through the setup
feature, so it is added after the features; the controls feature is the form that saves nothing.
A suite that drives the multiplayer door passes its own `netDoor`; `UI/Menu/MenuHost.cs` is the host.

## src/Testing/ScreenKeyboardRecorder.cs
A suite's stand-in for Steam's on-screen keyboard: `Utils/ScreenKeyboard.cs` reads as available
and every URL it would open is recorded instead. Disposing it lowers whatever is still up and puts
back the detected state and the real handler. Used by `menu-screen-keyboard` and
`menu-original-screen-keyboard`.
