# Utils

The things every subsystem depends on: the clock, the log, the seed. Changing one of these changes determinism repo-wide; read `docs/verification.md` first.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Utils/TapHoldButton.cs
One button carrying two actions, split by how long it is held. Feed it the button's LEVEL each
frame; it edge-detects, times over `HoldToRepeat`, and answers `TapHold.Tap` / `Hold` / `None`.
Engine-free: the input read stays with the caller, which is what makes the decoding unit-testable
when the device is not. Pinned by the `target-input` suite.

## src/Utils/GameClock.cs
The session's simulation clock: `BeginFrame(wallDelta)` (first thing in `GameSession._Process`)
sets `Steps` + `Dt`; `GameSession` translates those into requests to `SessionSimulation`. Modes:
Realtime, FixedAccum (interactive anim lab), FixedStep (scripted runs / `--det`); `Halted` is
orthogonal. `SimHeld` is the authoritative session-simulation hold read by `SessionSimulation`;
`FrameDt` is untouched, so animation keeps playing during a cutscene. `AuthoredAnimationHeld`
distinguishes the mission-ending hold, where both animation callbacks must keep the current pose.
On a realtime session `AnimRuntime` is a `PhysicsDt` consumer too (its `_PhysicsProcess`), so the
authored motions step on the physics tick; its `_Process` takes over only under `SimHeld`, a halt,
or a parent-driven mode unless authored animation itself is held. Regression: the `anim-clock-realtime`
suite. Published as `GameClock.Current` (session-scoped, nulled on teardown; null = raw frame delta).

## src/Utils/RenderPoses.cs
The render half of the fixed-tick simulation, shared by every subsystem that moves something
visible. A realtime session advances poses on the 60 Hz physics tick while the display redraws at
its own rate, so a pose drawn raw is held for a frame and jumped the next, and it steps against the
camera in proportion to speed. Two consumption shapes over one gate and one fraction: a node writer
calls `Record(node)` right after writing it and `Draw()` places it between its last two simulation
poses, while a subsystem holding its own coordinates (`Projectile`'s rounds) reads `Fraction` and
interpolates its own pair. `Restore(tick)` opens every physics callback, putting the exact
simulation pose back and rolling the pair once per tick, so nothing the simulation seeds from a
node's transform can observe a drawn one. `Draw()` has one caller, `GameSession._Process`, which is
why a suite stepping the simulation by hand always reads the simulation pose. Realtime only:
`Fraction` is 1 elsewhere and the whole-step case takes the stored pose rather than an
interpolation to 1, so fixed-step captures stay byte-identical. Regression: the `render-poses`
suite. Static like `GameClock.Current` and cleared beside it on teardown.

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
`WorldEffectsFactory.EffectStageRootNames(program, gamez)` — the derived stage set — as a
typo would otherwise size nothing silently. Asserted twice, because that set is now chapter data: in
`EffectPoolsTests` against C1's bound program (an `ExtractedDataFact`, skipped without an
extraction) and as an `effects-census` condition on whatever chapter the run was given.

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
scale are on the `[world] clutter fade:` launch line. Enhanced mode pushes the resolved scale by
`WeatherRig.EnhancedFogScale()` squared, so clutter reaches as far as the pushed fog instead of
standing at its authored distance underneath it; original mode's factor is identity, so its value
is unchanged. `MapEdgeExtender`'s own clutter continuation shares this same global, so it follows
without its own code.

## src/Utils/GraphicsMode.cs
The opt-in enhanced-lighting mode's setting (`original`/`enhanced`, default `original`), resolved
once by `Launcher._Ready` beside `EffectsLevel` into the single boolean `GraphicsMode.Enhanced`
every later scene builder reads, rather than each reader querying a source itself. That
indirection is what lets the sources be layered without touching a reader. The order mirrors
`PresentationResolution`'s: `--graphics=original|enhanced` (`docs/cli.md`) beats the saved
`graphicsMode` option (`OptionsStore`, written by both Options screens), which beats the
`graphics.mode` config key, which beats the default. `Launcher._Ready` loads the saved option and
hands it to `Resolve`, so no reader gains a second source.

`--det` drops both machine-state layers and leaves the flag. `--det`'s `Config.ClearOverrides`
(`Launcher.cs`) drops a `graphics.mode` config override the same way it drops `EffectsLevel`'s,
since the resolution runs after that block, and `Launcher._Ready` passes no saved option at all
under `--det`, since `user://options.json` is one machine's state and a golden that depended on it
would move the day its owner used the Options screen. An explicit `--graphics=` is carried on
`SessionSpec` and never touches either, so it survives `--det` and is the one mechanism a golden or
a deterministic capture uses to pin the mode on purpose. An unknown word at any layer warns and
falls back to `original`. Announced on the `[world] graphics mode:` launch line beside the
clutter-fade line. The whole mode is written up as a divergence in "Rendering: the enhanced
graphics mode" above.

## src/Utils/ScriptedWindow.cs
Win32-only window hiding for scripted runs: `ScriptedWindow.Hide()` calls `ShowWindow(SW_HIDE)` on
the native window handle. Fully static, one call site in `Launcher._Ready` right after the `--det` block — the same
predicate drives both window hiding (scripted run) and focus request (interactive run).

## src/Utils/OptionsStore.cs
Process-wide, version-tolerant JSON persistence for `OptionsDef`, today the requested menu
presentation (`menuPresentation`) and the requested graphics mode (`graphicsMode`): one file,
`user://options.json`, independent of `Session/CampaignProfileStore.cs`; under `--run-tests`
`UserOptions()` reads and writes an emptied scratch directory instead (`DirectoryOverride`), so no
driven suite depends on or touches the player's file. That directory is per process rather than
shared, because concurrent shards start together and race for one.
Missing/malformed reads as
empty, an unknown version invalidates the file, an unknown value drops only that field, and a field
the file does not carry reads as never set. That last rule is why adding a field does not bump
`Version`: an older file loads with everything it does have. The version moves only when an
existing field changes meaning or shape. `Save` writes a sibling temp file then renames it over the
real one, the first store here to need an atomic write rather than a direct one. `Launcher` is its
only writer (`ApplyOptions`), so no presentation, test or suite writes the player's own file.

## src/Utils/PresentationResolution.cs
The requested-versus-active menu presentation resolver: force-Built-in → CLI override → saved
request → Built-in default, with the caller's availability check applied only after the request is
picked. `Resolve` never rewrites what `Requested` would answer, so a fallback cannot alter
`OptionsStore`'s saved value. Presentation names are plain strings; no presentation contract type
lives here.

