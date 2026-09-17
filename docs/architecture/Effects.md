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
Two types delivering the mission's authored wind to every puffer. `WorldWind` is the gust model, a
static base vector plus a horizontal random-walk gust stepped once per frame off its own `Rng.Wind`
stream, authored in `weather.zrd`'s `WIND` block (schema:
[../formats/weather.md](../formats/weather.md); decode: [../org/weather.md](../org/weather.md)).
`EffectAmbience` is the seam holding the per-frame state a `Puffer` reads, the wind and every
pane's camera pose. `GameSession` owns the one instance and `WeatherRig.Tick` writes it per frame;
the world build takes that same instance by option, so its emitters fade and cull like the player's
own. `Still` is the camera-less null object an unwired puffer reads. Read `Puffer.cs` next.

## src/Effects/EmitterRenderer.cs
`Puffer`'s lower seam. `IEmitterRenderer` takes live particles (`Attach` sizes the pool, `Grow`
re-sizes it, `Write` per particle, `Show` publishes the frame), reaching the three emitter modes
without a GPU via `RecordingEmitterRenderer`. `MultiMeshEmitterRenderer` draws them as MultiMeshes of camera-billboarded quads, over one
process-wide unit quad and one compiled shader per blend, soft and graphics-mode triple, and owns that shader: quad-rim fade, flipbook column from
per-instance custom data, the soft-particle depth fade, `csky_srgb_to_linear` on the `COLORS` ramp, the two per-column marks Enhanced Graphics grades by (`IsFireSprite`, an ALBEDO gain over the glow threshold; `IsSmokeSprite`, a `csky_sun_dir` gradient across the quad in the mix variant alone, clamped under that threshold: the faithful text carries neither term), and the
mission's distance fog off the sky's globals. Blend arrives per atlas column from `Puffer.Create`,
keeping this seam free of `TextureArchive`; a column set spanning both draws one MultiMesh per
blend, each in write order and depth-sorted on its cloud's AABB centre. Read `Puffer.cs` next.

## src/Effects/FogVolumeClutter.cs
The ambient cloud field, entirely authored: `fogvol.zrd`'s weighted clutter table laid on a
staggered lattice over every unflagged polygon of every `fvol*` volume, one alpha-blended
MultiMesh per sprite kind, plus a map-edge continuation (`ExtendPastMapEdge`/`EmitExtensionRegion`)
past the map rim for a map-spanning slab. Templates resolve through
`ClutterBuilder.FindTemplateRoot`. `BandData` packs each sprite's own face normal and the one draw
both `far_fade_range` pairs are interpolated with into a custom-data slot, which
`csky_clutter_fade_alpha_angled` turns into the view-angle fade; that draw takes its own `Rng.CloudBands` stream.
`ShaderCode`'s third variant grades the cards by `csky_sun_dir` (`../../CSVM/shaders/csky_sun.gdshaderinc`) under `GraphicsMode.Enhanced` alone, leaving the faithful text byte-identical. Gating: `GameSession`/`WorldBuilder`/`WeatherRig`. Schema: [../formats/fogvol.md](../formats/fogvol.md).

## src/Effects/FogVolumeBanks.cs
Enhanced Graphics only: the soft volumetric bank standing inside each authored `fvol*` volume, under
the cards `FogVolumeClutter` lays over the same geometry. `Create` builds one bank per volume, each
laying its own bounds down as `FogVolume` boxes over one shared `FogMaterial`, and `ApplyFroxelFog`
arms the Environment's froxel pass for a world that built some and clears it for one that did not,
with zero global density so the banks carry it all and the authored `csky_fog_*` ramp is not hazed
twice. `WeatherRig`'s zone apply calls `ApplyZone`, so the scattering colour is the zone's own, or
the chapter's authored whiteout colour where `fogvol.zrd` arms one, which also sets the density.
Every constant is TUNE, including the tile width, which is an engine limit. Volumes: [../formats/fogvol.md](../formats/fogvol.md).

## src/Effects/Precipitation.cs
Rain and snow from `weather.json`'s precipitation block (`WeatherState.PrecipData`): ONE MultiMesh
whose shader derives each quad's position from a per-instance seed, `csky_time` and
`CAMERA_POSITION_WORLD`, wrapped into a camera-centred box, so the field costs no per-frame CPU.
SNOW flutters as flakes; RAIN streaks along the data's world fall velocity. The sprites are
procedural (`MakeFlakeTexture`/`MakeStreakTexture`), the original having drawn untextured
primitives no archive carries. Schema and the data-to-look TUNE mapping:
[../formats/weather.md](../formats/weather.md).

## src/Effects/WindStreaks.cs
Remake-only wind streaks, a layer OVER the authored speed cue (`Flight/SpeedCue.cs`) rather than a
replacement: one MultiMesh of thin procedural quads in a camera-centred wrap box on
`Precipitation`'s pattern, aligned to the aircraft's world velocity, with the same near and rim
fades. `Create` returns null unless `GraphicsMode.Enhanced`; `HumanFlightAdapter` gives each player
pane its own and `FlightController` drives it. `Update` is the whole law: opacity zero below a
cruise fraction of `PlaneStats.FdSpeed`, rising with the speed fraction plus a term on
`FlightModel.LoadFactorDemand`, length growing with airspeed, and the drift accumulated on the CPU
rather than off a clock, since the rate changes with airspeed. Every constant is TUNE.

## src/Effects/HeatShimmer.cs
Remake-only heat shimmer over a fireball, built only under `GraphicsMode.Enhanced` and only where
`WorldEffectsFactory.RegisterHeatShimmer` decides: one billboard quad per burst from a fixed pool,
all of them in ONE MultiMesh, so every live burst shares one draw and one colour-buffer copy. The
quad samples `hint_screen_texture` at a `csky_time`-driven noise offset scaled by a radial mask and
by the fireball's own liveness, and writes it back with no gain, so it lifts no pixel over the glow
bar. That copy is taken before the transparent pass and holds no fire or smoke, which is why the
quad stands clear above the flame. `Spawn` recycles the oldest slot at the cap, `Step` retires a
quad the frame its liveness goes false, and every size, lift, decay and amplitude constant is TUNE.

## src/Effects/ScorchField.cs
Remake-only scorch marks, a layer OVER the crater carve (`Mech3/CraterField.cs`) and never instead
of it: a capped pool of `Decal` nodes sharing one procedural radial burn texture built on first use,
projected along the struck surface normal and faded out over their own life. `Create` returns null
unless `GraphicsMode.Enhanced`, so the faithful build holds no pool, no node and no texture.
`GameSession.RegisterScorch` is the one decision point (a bowl was carved, or the impact played one
of `EffectCatalogue`'s fireballs); `Flight/Projectile.ScorchSink` is the hook and skips water. Size
comes from the weapon's crater radius. Every size, darkness and life constant is TUNE.
