# Effects

Particle systems: the puffer emitter and its GPU renderer seam, the ambient cloud clutter field, precipitation, and the mission wind that drives them.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Effects/Puffer.cs
The engine's billboard-particle emitter, data-driven from `PUFFER_STATE` blocks: `PufferState.Load`
or `PufferState.FromAnimEvent` reads the state, `Puffer.Create` bakes the frame atlas and hands it
to an `IEmitterRenderer` (`EmitterRenderer.cs`), and this class owns only the CPU integration.
The authored state picks burst, distance-trail or sustained mode; callers drive it through
`Emit`/`Stop`, the one-shot `Burst` and the hard-kill `Clear`. `CreateWith` reaches all three modes
with no atlas, no `TextureArchive` and no GPU. The distance fade, the emission accumulator, the
pool sizes and the two unauthored-interval constants each carry their constraint at their own
member. Keys and switches: [../formats/effects.md](../formats/effects.md); decode: [../org/puffer.md](../org/puffer.md).

## src/Effects/WorldWind.cs
Two types delivering the mission's authored wind to every puffer that reads it. `WorldWind` is the
gust model, a static base vector plus a horizontal random-walk gust stepped once per frame off its
own `Rng.Wind` stream, authored in `weather.zrd`'s `WIND` block (schema:
[../formats/weather.md](../formats/weather.md); decode: [../org/weather.md](../org/weather.md)).
`EffectAmbience` is the seam holding the per-frame world state a `Puffer` reads but does not own,
the wind and every pane's camera pose, handed in at construction rather than reached for.
`GameSession` owns the one instance and `Session/WeatherRig.Tick` writes it once per frame;
`EffectAmbience.Still` is the null object every unwired puffer reads. Read `Puffer.cs` next.

## src/Effects/EmitterRenderer.cs
`Puffer`'s lower seam. `IEmitterRenderer` takes live particles (`Attach` sizes the pool, `Grow`
re-sizes it when a continuous emitter outgrows it, `Write` per particle, `Show` publishes the
frame), which is what makes the three emitter modes reachable without a GPU; a suite reaches them
through `RecordingEmitterRenderer`. `MultiMeshEmitterRenderer` draws the particles as ONE MultiMesh
of camera-billboarded quads and owns the shader: quad-rim fade, flipbook column from per-instance
custom data, the soft-particle depth fade, and the `csky_srgb_to_linear` pass on the `COLORS` ramp.
The atlas and the blend verdict arrive already resolved from `Puffer.Create`, which is what keeps
this seam free of `TextureArchive`. Read `Puffer.cs` for the CPU half.

## src/Effects/FogVolumeClutter.cs
The ambient cloud field, entirely authored: `fogvol.zrd`'s weighted clutter table scattered through
every `fvol*` volume the gamez carries, one alpha-blended MultiMesh per sprite kind, plus a
map-edge continuation (`ExtendPastMapEdge`/`EmitExtensionRegion`) that tiles the same field past
the map rim for a chapter's map-spanning slab, matching `MapEdgeExtender`'s own terrain
continuation. Templates resolve through `ClutterBuilder.FindTemplateRoot`, the trees' own lookup,
and the two TUNE constants carry their remarks at their own fields. Per-view visibility gating
lives in `GameSession`/`WorldBuilder`/`WeatherRig`, not here. Schema, per-chapter values, the
decoded/inferred split and the map-edge measurements: [../formats/fogvol.md](../formats/fogvol.md).

## src/Effects/Precipitation.cs
Rain and snow from `weather.json`'s precipitation block (`WeatherState.PrecipData`): ONE MultiMesh
whose shader derives each quad's position from a per-instance seed, `csky_time` and
`CAMERA_POSITION_WORLD`, wrapped into a camera-centred box, so the field costs no per-frame CPU.
SNOW flutters as flakes; RAIN streaks along the data's world fall velocity. The sprites are
procedural (`MakeFlakeTexture`/`MakeStreakTexture`), the original having drawn untextured
primitives no archive carries. Schema and the data-to-look TUNE mapping:
[../formats/weather.md](../formats/weather.md).
