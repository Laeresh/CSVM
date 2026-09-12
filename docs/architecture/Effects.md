# Effects

Particle systems: the puffer emitter and its GPU renderer seam, the ambient cloud clutter field, precipitation, and the mission wind that drives them.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Effects/Puffer.cs
The engine's billboard-particle emitter, read from `PUFFER_STATE` blocks by `PufferState.Load` or
`FromAnimEvent`: `Puffer.Create` bakes the frame atlas, reads each frame's blend off the texture's
own additive bit, and hands both to an `IEmitterRenderer` (`EmitterRenderer.cs`); this class owns
only the CPU integration. The authored state picks burst, distance-trail or sustained mode; callers
drive it through `Emit`/`Stop`, `Burst` and the hard-kill `Clear`, and `CreateWith` reaches all
three with no atlas, archive or GPU. `_Process` buffers the frame's draw payloads and writes them
farthest-first against pane 0. The distance fade, the accumulator, the pools and the two
unauthored-interval constants carry their own constraint. Keys and decode: [../formats/effects.md](../formats/effects.md), [../org/puffer.md](../org/puffer.md).

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
re-sizes it on demand, `Write` per particle, `Show` publishes the frame), reaching the three
emitter modes without a GPU via `RecordingEmitterRenderer`. `MultiMeshEmitterRenderer` draws them
as MultiMeshes of camera-billboarded quads and owns the shader: quad-rim fade, flipbook column from
per-instance custom data, the soft-particle depth fade, and `csky_srgb_to_linear` on the `COLORS`
ramp. Blend arrives per atlas column from `Puffer.Create`, keeping this seam free of
`TextureArchive`; a column set spanning both draws one MultiMesh per blend, each in write order and
depth-sorted on its cloud's AABB centre. Read `Puffer.cs` for the CPU half.

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
