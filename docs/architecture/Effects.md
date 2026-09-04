# Effects

Particle systems: the puffer emitter and its GPU renderer seam, the ambient cloud clutter field, precipitation, and the mission wind that drives them.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Effects/Puffer.cs
The original engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks
(schema: [formats/effects.md](formats/effects.md)). `PufferState.Load` finds the fully-defined
state in an effects reader; `Puffer.Create` builds the atlas and hands it to an `IEmitterRenderer`
(`EmitterRenderer.cs`) — the class itself owns only the CPU integration, so `CreateWith` reaches
all three modes with no atlas, no `TextureArchive` and no GPU. The continuous surface is one pair,
`Emit(worldPos, worldBasis, dt, staticBurnMps = 0f)` / `Stop()`, plus the one-shot `Burst` and the
hard-kill `Clear` — the authored state picks burst, trail or sustained mode, callers never do.
`PufferState.FromAnimEvent` parses the compiled anim payloads; `Parse` reads the reader form.
**Two different numbers answer "no `TIME_INTERVAL` authored"**, and both parsers keep them apart:
`TimeIntervalDefault` is the engine constructor's 1 s, and `StillHostSputterInterval` is CSVM's own
0.1 s cadence for a `DISTANCE_INTERVAL` state whose host holds still (a mode the engine does not
have at all). A compiled event says "unauthored" with a **zero** interval, which the engine's own
setter refuses; a reader block says it by omitting the key, and every reader block that omits it is
a distance block. Merging the two constants moves either every static building's sputter or every
unauthored state by a factor of ten, so the `puffer-modes` suite asserts both cadences.
`Create`'s atlas comes from a per-archive cache keyed by the frame list and the sequenced flag
(`BuildAtlas` over `BakeAtlas`), so a state many emitters share is baked and luminance-measured
once; the archive is the key, so a chapter change never serves another chapter's frames.
Three config knobs (`puffer.burstSizeScale`/`trailSizeScale`/`sustainSizeScale`) scale `BaseSize`
per spawn path, registered in `Config.WarmTuningRegistry` for `--dump-config`.
A dormant emitter is off Godot's `_Process` list: `SetActive` is the only writer of the active flag
and it moves `SetProcess` with it, `_Ready` puts the node back where `SetActive` left it, and the
`puffer-idle-process-gate` suite holds both directions. A mission pre-warms thousands of emitters,
so what they cost the frame is set by how many ask for the callback.
The wind it reads is `Effects/WorldWind.cs` — see its own entry below.

**The camera-distance fade** (`DistanceAlpha`) is view-space depth off `EffectAmbience`'s camera
poses, run against every pane since `NearestViewerAlpha` keeps the most favourable answer across
them. The mechanism, field order and the three `puffer.*` switches are in
[formats/effects.md](formats/effects.md); read that before touching this.

The emission accumulator (a teleport guard on the distance path, no per-frame batch cap on
either), the `PRIORITY` size nudge, and the blend-mode derivation are all decoded and traced in
[org/puffer.md](org/puffer.md) — read it before adding a mechanism here. **The two continuous
modes share one batch loop** (`EmitBatches`, fed metres by `TrailAdvance` and seconds by
`SustainAt`, both spawning through `SpawnSustained`): a distance trail therefore lays `NUMBER`
puffs per interval, rotates `LOCAL_VELOCITY` into the host's frame and gives each puff its
sub-frame birth age, exactly as the time mode does. ⚠ Do not give the trail its own spawn again:
the one it had dropped all three, and the smoke screen (`smokerpuff`: `NUMBER 4`, `LOCAL_VELOCITY`
10 m/s astern) laid a quarter of its cloud drifting on the world axes. A continuous emitter's pool
starts at `TrailPool` / the sustained steady-state estimate and doubles on demand up to
`ContinuousPoolMax` (`GrowPool` → `IEmitterRenderer.Grow`); the ceiling is invented like every
pool size here, the growth is not, since the engine bounds particles only by what can be born
alive. The authored-key side
stays in [formats/effects.md](formats/effects.md); the blend trace continues in
[org/textures.md](org/textures.md). Proven by `PufferDistanceFadeTests`, `PufferWindTests`,
`PufferPriorityTests`, `PufferStartAgeTests` and the `puffer-fire-column` suite.

## src/Effects/WorldWind.cs
Two small types, one job: get the mission's authored wind to every puffer that reads it.

`WorldWind` is the gust model, decoded from `FUN_0054ee10` and authored in `weather.zrd`'s `WIND`
block (schema: docs/formats/weather.md; decode: docs/org/weather.md): a static base vector plus a
horizontal random-walk gust, stepped once per frame off its own `Rng.Wind` stream.

`EffectAmbience` is the seam: the per-frame world state (wind, every pane's camera pose) a
`Puffer` READS but does not own, handed in at construction rather than reached for. `GameSession`
owns the one instance and `Session/WeatherRig.Tick` writes it once per frame; `EffectAmbience.Still`
is the null object every unwired puffer reads (unit suites, the plane viewer, no-weather missions).

## src/Effects/EmitterRenderer.cs
`Puffer`'s lower seam: `IEmitterRenderer` takes live particles (`Attach` sizes the pool, `Grow`
re-sizes it when a continuous emitter outgrows it, `Write` per particle, `Show` publishes the
frame) and `MultiMeshEmitterRenderer` draws them as ONE MultiMesh of camera-billboarded quads,
owning the shader — quad-rim fade, flipbook column from per-instance custom data, and the
soft-particle depth fade. The `COLORS` ramp arrives as the per-instance colour and is linearised
in the shader (`csky_srgb_to_linear`, the same include every fullbright pass uses): the ramp is
authored in DX7 framebuffer bytes, so multiplied in raw it drew every ramped puffer two shades
too pale (the smoke screen's `53,74,37` came out `109,126,92` against the reference's `50,68,35`).
The atlas and the blend verdict both arrive already resolved from `Puffer.Create`, which is what
keeps the three modes reachable with no `TextureArchive` below this seam. Reached in a suite by
`RecordingEmitterRenderer`. `Attach` takes its `Shader` from a static per-variant cache (blend ×
soft, four at most): a `Shader` per emitter compiled in about 6 ms, which was most of a live
`effect_pool_miss` and of a crash rig's pre-warm; the `ShaderMaterial` stays per emitter, since it
carries the atlas.

## src/Effects/FogVolumeClutter.cs
The ambient cloud field, entirely authored: `fogvol.zrd`'s weighted clutter table scattered
through every `fvol*` volume the gamez carries, one alpha-blended MultiMesh per sprite kind (two
per chapter), plus a map-edge continuation (`ExtendPastMapEdge`/`EmitExtensionRegion`) that tiles
the same field past the map rim for a chapter's map-spanning slab, engine-side and not authored,
matching `MapEdgeExtender`'s own terrain continuation. Templates resolve through
`ClutterBuilder.FindTemplateRoot` — the trees' own lookup. The two TUNE constants
(`TopAnchorHeightFactor`, `CardVertexColorTune`) and their remarks live at their own fields.
Per-view visibility gating lives in `GameSession`/`WorldBuilder`/`WeatherRig`, not here.
Schema, per-chapter values, the decoded/inferred split and the map-edge-continuation measurements:
docs/formats/fogvol.md. The shared template lookup: docs/org/clutter.md. Proven at the render —
see fogvol.md's evidence sections and `FogVolumeTests`/`FogVolumeWhiteoutTests`.

## src/Effects/Precipitation.cs
Rain/snow from weather.json's precip block (`WeatherState.PrecipData`): ONE MultiMesh whose
shader derives each quad's position from a per-instance seed + `csky_time` + `CAMERA_POSITION_WORLD`,
wrapped into a camera-centred box — zero per-frame CPU. SNOW = fluttering flakes; RAIN =
streaks along the data's WORLD fall velocity (not plane-relative — TUNE pending A/B); sprites
are procedural `MakeFlakeTexture`/`MakeStreakTexture` (the original drew untextured primitives).
Schema and the data→look TUNE mapping: docs/formats/weather.md.

