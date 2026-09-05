# Utils

The things every subsystem depends on: the clock, the log, the seed. Changing one of these changes determinism repo-wide; read `docs/verification.md` first.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Utils/HoldToRepeat.cs
Tap-versus-hold timing for a single button: `Press` arms the timer, `Tick(dt)` returns true once
the initial delay has elapsed and then once per repeat interval until `Release`. A zero repeat
interval fires every tick after the delay, which is the pause transport's "hold to step every
rendered frame"; a nonzero one is `MenuInput`'s d-pad initial-delay and repeat-rate shape, driven
through `MenuInput.StepAxis` for both cursor axes. Engine-free, so it decodes without a device.
Read `TapHoldButton.cs`, which wraps it for the tap-or-hold split.

## src/Utils/TapHoldButton.cs
One button carrying two actions, split by how long it is held. Feed it the button's LEVEL each
frame; it edge-detects, times over `HoldToRepeat`, and answers `TapHold.Tap` / `Hold` / `None`.
Engine-free: the input read stays with the caller, which is what makes the decoding unit-testable
when the device is not. Pinned by the `target-input` suite.

## src/Utils/GameClock.cs
The session's simulation clock: `BeginFrame(wallDelta)` sets `Steps` and `Dt`, and `GameSession`
turns those into requests to `SessionSimulation`, so pausing, single-stepping and fixed-dt replay
all enter through one ordered step. Modes are Realtime, FixedAccum (the interactive anim lab) and
FixedStep (scripted runs and `--det`); `Halted`, `SimHeld` and `AuthoredAnimationHeld` are
orthogonal holds, each carrying its own rule at its own field. Published as `GameClock.Current`,
session-scoped and nulled on teardown, where null reads as the raw frame delta. Read
`RenderPoses.cs` for the render half of the same tick and `ShaderTime.cs` for the shaders' view.

## src/Utils/RenderPoses.cs
The render half of the fixed-tick simulation, shared by every subsystem that moves something
visible. A realtime session advances poses on the 60 Hz physics tick while the display redraws at
its own rate, so a pose drawn raw is held for a frame and then jumps. Two consumption shapes over
one gate and one fraction: a node writer calls `Record(node)` right after writing the node and
`Draw()` places it between the last two simulation poses, while a subsystem holding its own
coordinates (`Projectile`'s rounds) reads `Fraction` and interpolates its own pair. `Restore(tick)`
opens every physics callback, so nothing the simulation seeds from a node's transform can observe
a drawn one. Realtime only; elsewhere `Fraction` is 1 and every path is an identity rewrite.

## src/Utils/Log.cs
The diagnostic log: `Log.Info("world", $"…")` and its `Warn` / `Error` / `Debug` siblings over a
fixed category vocabulary and four levels, with two sinks that have different jobs. The console is
the human's view and stays quiet by default; the `.scratch/logs/<mode>-<stamp>.log` file is the
machine's and always takes everything, line-flushed. `ConsoleSink` and the scoped
`PushConsoleSink` redirect console lines, so a plain class that logs is callable without an engine
and a test can assert on its own lines without a parallel class stealing them. Categories, levels,
the file-line grammar, the `--log=` filter and the sink path:
[../org/logging.md](../org/logging.md). `HitchSidecar.cs` shares this sink's stem.

## src/Utils/ShaderTime.cs
The GPU's view of the clock: the `csky_time` global shader uniform (seconds), registered once in
`Launcher._Ready` and written once per rendered frame from `GameClock.Time`. Every animated
shader this project generates reads it instead of Godot's `TIME`.

## src/Utils/StartupProfile.cs
The always-on startup timing report: one `[perf] startup …` line per session build, split into the
phases the build spends its time in. `Mark()` and `Record(phase, mark)` are ambient statics over
`Current`, so the shared build code (`WorldSession`, which the test harness also drives) records
without being handed an accumulator. `Phases` is a read-only view of the same accumulator for a
caller that wants to aggregate the spans without emitting the line, which is how
`src/Testing/PhaseAttribution.cs` reads a harness build. Line grammar, the phase rules and the
`boot` / `rest` / `first_frame` terms: [../org/startup-profile.md](../org/startup-profile.md).

## src/Utils/HitchMonitor.cs
The always-on frame-hitch detector, ticked from `Launcher._Process` in every mode: a frame costing
far more than its recent neighbours gets a `HitchRecord` assembled for it, describing the frame's
cost split, its engine counts as absolutes and as deltas, its GC activity, the ring of frames
leading up to it, and the named work `PerfSample` attributed. It only detects, and nothing is
logged from here, so a clean run is silent and `HitchSidecar.cs` owns the write path. Godot-free by
construction (the caller samples the engine counters into a `FrameCounters` and hands them in), so
the trigger and its edges unit-test off-engine. Trigger formula, the `hitchMonitor.*` keys and the
record's fields: [../org/hitch.md](../org/hitch.md).

## src/Utils/HitchSidecar.cs
`HitchMonitor`'s write path: a tripped record is copied, never referenced, into a small
preallocated queue and drained a few seconds later to one `[perf] hitch …` line plus one JSON line
in `.scratch/logs/<mode>-<stamp>.hitches.jsonl`, which shares the main log's stem. Writing off the
hitching frame is the point, since a string interpolation and a file write are avoidable
allocation-heavy work at the worst possible moment. A record is all numeric apart from the site
names, so the JSON is hand-written rather than serialized. Line grammar, the JSON shape, the
`hitchSidecar.*` keys and the flush behaviour: [../org/hitch.md](../org/hitch.md).

## src/Utils/PerfSample.cs
Ambient timed leaf scopes: `using (PerfSample.Scope(PerfSite.DebrisSpawn))` adds its wall time to
that site's total for the frame in progress, and any code path can do it without knowing the
monitor, the readout, or whether anything is listening. Statics over a preallocated per-site array,
the same ambient shape `StartupProfile` uses and for the same reason, since a scope several call
layers down cannot be handed an accumulator. `Launcher._Process` calls `EndFrame()` where it stamps
the frame's wall cost, so the scopes and the `frame_ms` they ran inside describe the same span. The
site vocabulary, the seeded call sites and the attribution terms a record carries:
[../org/hitch.md](../org/hitch.md).

## src/Utils/PhysicsTickCost.cs
The wall cost of one whole Godot physics tick and how many ticks a wall second actually got,
measured by two `PhysicsTickBracket` nodes pinned to the extremes of the physics priority order so
the pair spans every `_PhysicsProcess` callback in the tree. `Take()` drains the window as total
milliseconds, the worst single tick in it and the tick count; `NominalHz` turns that count into the
sim seconds a wall second bought, which is what shows a sim running at half speed. Godot's
`TIME_PHYSICS_PROCESS` monitor answers neither question, and the misreading it invites is
`docs/verification.md` PERF-21.

## src/Utils/Rng.cs
The session's randomness policy: one master seed and a named generator per subsystem derived from
it, independent across subsystems so a draw added to one cannot shift another's. The stream names
are the `public const string` fields on `Rng` itself, each carrying the reason it is its own
stream. `Reset(master, pinned)` runs once per session build, before anything draws; `Stream(name)`
is the shared generator, `SeedFor` / `IntSeedFor` the pure seed, and `NewIntSeed` /
`NewSystemRandom` a per-instance stream off the subsystem's own. Unpinned, the master comes from
`TimeSeed()` so the shipped game keeps its variety; a scripted flag implies `--det` and pins it.

## src/Utils/EffectPools.cs
The `CSVM/data/effect_pools.json` reader: how many copies of each effect-template ROOT the
world-effects stage builds. Hand-authored engine config, in a file rather than a `const` because it
is invented; the original copies its template per CALL_ANIMATION and has no such number, so any
finite pool is our approximation and the user must be able to move it without a rebuild. Three
sections for three namespaces: `roots` / `default` (the world stage, per-player scaled),
`localCallRoots` (library-root clones) and `crashRoots` / `crashDefault` (the per-player crash
rig). `SlotsFor(root, players) = clamp(base + perExtraPlayer × (players − 1), 1, maxSlots)`, and
`UnknownRoots` / `UnknownCrashRoots` name an authored root the derived stage set does not carry.

## src/Utils/Config.cs
Dev-facing tuning-override layer: static `Config` parses an optional sparse `res://config.json`,
and the typed getters (`GetFloat` / `GetInt` / `GetBool` / `GetString`) return the file's value for
a present key, else the caller's in-code `const` default, read through at the point of use. Keys
are `moduleCamelCase.fieldCamelCase`, grouped one nesting level in the JSON and flattened to
dot-keys. Nothing writes the file and `config.json` is git-ignored, so the consts stay canonical;
querying a key is also what registers it for `--dump-config`.

## src/Utils/EffectsLevel.cs
The original's graphics EffectsLevel option as a config key (`graphics.effectsLevel`: `high`,
`medium` or `low`, default `high`), and the one global it drives today:
`csky_clutter_fade_scale_sq`, the squared distance scale every templates-clutter fade multiplies
into its camera distance. The level's meaning and direction:
[../formats/templates.md](../formats/templates.md). A second key, `graphics.clutterFarFade`, is the
remake's own switch: false resolves the global to a never-fades scale, so clutter draws out to the
fog instead of ending at the authored metres. Enhanced mode scales it by
`WeatherRig.EnhancedFogScale()` squared. Read `ClutterBuilder` and `MapEdgeExtender` next.

## src/Utils/GraphicsMode.cs
The opt-in enhanced-lighting mode's setting (`original` or `enhanced`, default `original`),
resolved once by `Launcher._Ready` into the single boolean `GraphicsMode.Enhanced` every later
scene builder reads, so the sources can be layered without touching a reader. The order mirrors
`PresentationResolution`'s: `--graphics=` beats the saved `graphicsMode` option (`OptionsStore`),
which beats the `graphics.mode` config key, which beats the default; an unknown word at any layer
warns and falls back. `--det` drops both machine-state layers and keeps only an explicit
`--graphics=`, which is how a golden or a deterministic capture pins the mode on purpose. The mode
itself is written up as a divergence in `docs/architecture/Root.md`.

## src/Utils/ScriptedWindow.cs
Win32-only window hiding for scripted runs: `ScriptedWindow.Hide()` calls `ShowWindow(SW_HIDE)` on
the native window handle. Fully static, one call site in `Launcher._Ready` right after the `--det`
block, where the same predicate drives both window hiding and the interactive run's focus request.

## src/Utils/OptionsStore.cs
Process-wide, version-tolerant JSON persistence for `OptionsDef`, today the requested menu
presentation, the requested graphics mode and the difficulty word: one file, `user://options.json`, independent of
`Session/CampaignProfileStore.cs`. A missing or malformed file reads as empty, an unknown version
invalidates it, an unknown value drops only that field, and a field the file does not carry reads
as never set, which is why adding a field does not bump `Version`. `Save` writes a sibling temp
file and renames it over the real one. Under `--run-tests`, `UserOptions()` reads and writes an
emptied per-process scratch directory (`DirectoryOverride`) instead, so no suite touches the
player's file; `Launcher.ApplyOptions` is the only writer.

## src/Utils/PresentationResolution.cs
The requested-versus-active menu presentation resolver: force-Built-in → CLI override → saved
request → Built-in default, with the caller's availability check applied only after the request is
picked. `Resolve` never rewrites what `Requested` would answer, so a fallback cannot alter
`OptionsStore`'s saved value. Presentation names are plain strings; no presentation contract type
lives here.
