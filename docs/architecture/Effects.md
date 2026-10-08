# Effects

Particle systems: the puffer emitter and its GPU renderer seam, the ambient cloud clutter field, precipitation, and the mission wind that drives them.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Effects/Puffer.cs
The engine's billboard-particle emitter, read from `PUFFER_STATE` blocks by `PufferState.Load` or
`FromAnimEvent` (`Mech3/PufferState.cs`): `Puffer.Create` bakes the frame atlas and each frame's blend (the texture's additive
bit) into an `IEmitterRenderer` (`EmitterRenderer.cs`); this class owns only the CPU integration.
The authored state picks burst, distance-trail or sustained mode; callers drive it through
`Emit`/`Stop`, `Burst` and the hard-kill `Clear`, and `CreateWith` reaches all three with no atlas,
archive or GPU. `_Process` writes the frame's draws farthest-first against pane 0. `BirthAlpha` is
an opacity each particle keeps from birth, 1 except on the code-built exhaust trail. `FollowAlphaDepth` bakes an archive's atlases again in place when their frames change depth on a live switch. The distance fade, the accumulator, the pools and the two unauthored-interval constants carry their own constraint. Keys and decode: [../formats/effects.md](../formats/effects.md), [../org/puffer.md](../org/puffer.md).

## src/Effects/WorldWind.cs
Two types delivering the mission's authored wind to every puffer. `WorldWind` is the gust model, a
static base vector plus a horizontal random-walk gust stepped once per frame off its own `Rng.Wind`
stream, authored in `weather.zrd`'s `WIND` block (schema:
[../formats/weather.md](../formats/weather.md); decode: [../org/weather.md](../org/weather.md)).
`EffectAmbience` is the seam holding the per-frame state a `Puffer` reads, the wind and every
pane's camera pose. `GameSession` owns the one instance and `WeatherRig.Tick` writes it per frame;
the world build's emitter factory closes over that same instance, so its emitters fade and cull
like the player's own. `Still` is the camera-less null object an unwired puffer reads. Under Enhanced it also holds the emitters burning a fire column, and `SubmitFires`, a `WorldLights` source, lights each. Read `Puffer.cs` next.

## src/Effects/PufferEmitterFactory.cs
The one real implementation of the animation layer's `IEmitterFactory` seam (`Mech3/Anim/IEmitter.cs`):
`Create` turns a `PufferState` into a sustained `Puffer` wrapped as an `IEmitter`, parented under
the world root and reading the session's `EffectAmbience`. The seam is owned below, so the
animation runtime drives emitters without naming this layer; `GameSession` and the test harness
hand the factory in through `WorldSession.Options`. A texture-less state is a stub and builds nothing.

## src/Effects/EmitterRenderer.cs
`Puffer`'s lower seam. `IEmitterRenderer` takes live particles (`Attach` sizes the pool, `Grow`
re-sizes it, `Write` per particle, `Show` publishes the frame), reaching the three emitter modes
without a GPU via `RecordingEmitterRenderer`. `MultiMeshEmitterRenderer` draws them as MultiMeshes of camera-billboarded quads, over one
process-wide unit quad and one compiled shader per blend and soft pair, with a twin per graphics mode that a live switch moves the layers onto (`SceneBuilder.RegenerableShader`) while the per-column gain and grade are read per particle, and owns that shader: quad-rim fade, flipbook column from
per-instance custom data, the soft-particle depth fade, `csky_srgb_to_linear` on the `COLORS` ramp, the two per-column marks Enhanced Graphics grades by (`IsFireSprite`, an ALBEDO gain over the glow threshold that the mixed alpha's clamp carries through; `IsSmokeSprite`, a `csky_sun_dir` gradient across the quad in the mix variant alone, clamped under that threshold: the faithful text carries neither term), the
mission's distance fog off the sky's globals, and a mixed alpha that lands on DX7's byte-space mix. Blend arrives per atlas column from `Puffer.Create`,
keeping this seam free of `TextureArchive`; a column set spanning both draws one MultiMesh per
blend, each in write order and depth-sorted on its cloud's AABB centre. Read `Puffer.cs` next.

## src/Effects/FogVolumeClutter.cs
The ambient cloud field, entirely authored: `fogvol.zrd`'s weighted clutter table laid on a
staggered lattice over every unflagged polygon of every `fvol*` volume, one alpha-blended
MultiMesh per sprite kind, plus a map-edge continuation
(`ExtendPastMapEdge`/`EmitExtensionRegion`) past the map rim for a map-spanning slab. Templates
resolve through `ClutterBuilder.FindTemplateRoot`. `BandData` packs each sprite's own face normal
and the one draw both `far_fade_range` pairs are interpolated with into a custom-data slot, which
`csky_clutter_fade_alpha_angled` turns into the view-angle fade; that draw takes its own
`Rng.CloudBands` stream. The shipped field is that decoded lattice plus a remake-only X/Z offset per card (`ShippedJitter`, 30 m, overridden by `--cloud-jitter=`), drawn off `Rng.CloudJitter` and reaching no other population. The quad is posed by `csky_facade_spherical` (`shaders/csky_facade.gdshaderinc`), a world-up look-at standing in for the original's SphericalY tracker, which reads the eye's position and not its basis, so neither the camera's roll nor a sideways move turns a card ([../org/cloudCards.md](../org/cloudCards.md)). A `lighting: true` card (C1C, C2B, C5) carries its three authored normals and takes the original's per-vertex `AMBIENT + DIFFUSE x max(N.L, 0)` through that same pose off `WeatherRig`'s uncollapsed globals, never `csky_world_light` ([../org/vertexLighting.md](../org/vertexLighting.md)). Under `GraphicsMode.Enhanced` alone, `ShaderCode` layers a grade by the global `csky_sun_dir` over either variant, leaving the faithful and lit text byte-identical, and draws both kinds from the deck pool of rendered puffs (`Mech3/CloudPuffs.cs`), tinted by the authored mask's colour, each card picking its puff, tilt, mirror and size off a hash of its own position. `FollowGraphicsMode` moves each kind onto the card shader for the standing mode, one compiled per text and kept, and writes its pool and cull margin again; `WarmOtherMode` compiles the other mode's ahead. Gating: `GameSession`/`WorldBuilder`/`WeatherRig`. Schema: [../formats/fogvol.md](../formats/fogvol.md).

## src/Effects/Ocean.cs
The Enhanced wave ocean on every chapter with a sea at y = 0 (`Ocean.Covers`, all but C4). A camera-centred polar
grid with a Gerstner swell in its vertex stage and drifting noise in its fragment normals replaces the sea-level base
sheet, which steps aside through `csky_ocean.gdshaderinc` only where the ocean draws; its colliders stay flat. It
draws a priority level below the lowest base sheet, one grid per zone-gate group; a spyglass disc
(`SceneBuilder.FlatSeaEye`) sees the flat sheet. Owns the grids, the sea's uniforms and up to 16 ship calm zones
(`OceanCalmZone.cs`, `OceanMovers.cs`), nearest the eye first; the swell, bent crests, noise detail, foam and coast
ramps are the shader text `OceanShader.cs` writes from the chapter's `SeaState.cs`, all on `csky_time`. `Apply`
takes a new sea live, recompiling only when the text changes. `GameSession.FollowOcean` builds and drops it.

## src/Effects/OceanSeas.cs
The shipped per-chapter seas, `CSVM/data/ocean_seas.json`, read through `res://` at each sea chapter's build: one
object per chapter (`Chapters`, which `Ocean.Covers` reads) holding only the fields that differ from the defaults.
A missing entry or field takes the default; an unknown chapter or field, a non-number and a clamped value are each
a `world` warning. `WithEntry` rewrites one chapter's entry in place for the ocean lab's Save, keeping every other
key and its order; `SourceTreePath` is null in an exported build. `OceanSeasTests`. Read `SeaState.cs` next.

## src/Effects/OceanShader.cs
The wave ocean's shader text from one `SeaState`, with no engine object touched: the swell, detail and foam patch
tables, the bending field, the coast ramps and the open sea's tint, written as literals. At the defaults the text is
byte-identical to the ocean's tune (`OceanShaderTests` against hashes and a fixture), so nothing moves until a sea
is saved. Every swell omega, detail drift and patch drift is rounded to whole cycles per `csky_time` wrap at any
setting. Height, foam strength and roughness stay uniforms, set by `Ocean.cs`.

## src/Effects/SeaState.cs
One chapter's sea as a record: every tunable of the wave ocean (swell, bending and detail, foam, coast and colour)
with today's tune as its default, and `Fields` naming each one's key, label, group, range and slider step for the
file, the flag and the lab. `Clamped`, which every path into the shader takes, holds each value in range, sharpness
times height at the fold limit, and each coast ramp at least 4 m wide within the mask's 160 m reach.
`WithOverrides` reads `--debug-ocean`. `SeaStateTests`. Read `OceanShader.cs` next.

## src/Effects/OceanCalmZone.cs
One ship's calm zone on the wave ocean: a box on the water along the hull's heading, grown over
world-space footprints (a mesh AABB under its global transform, or a point), with no engine object
touched. `Distance` and `Calm` are the shader's `ship_calm` in C#: no waves within a margin of the
box, full height a fade further out on a smoothstep. `OceanCalmZoneTests` hold the box and the fade.

## src/Effects/OceanMask.cs
The wave ocean's shore mask at 8 m texels, baked once per built world (keyed on its `SceneBuilder`) and reused by
every rebuild of the ocean over it. Owns sea coverage, the distance to the nearest shore, surf or solid texel, the base sheet's
baked tint and, where the sheet spans zone-gate layers, each texel's zone group. The same walk finds the wake
sheets, the tile size and the sheet's lowest priority. A wake's ship and every mover judged a hull
(`OceanMovers.cs`) stay out of the mask. While an ocean stands, `Publish` hands the mask, zone texture and rect to
the `csky_ocean_mask`/`_zone`/`_rect` globals the sheet's hide reads, which steps aside at `SeaThreshold`;
`Withdraw` restores the no-sea defaults. `--dump-ocean-mask=` writes the mask and the tint. Read
`OceanMaskRaster.cs` for the texels and `Ocean.cs` for the sampling.

## src/Effects/OceanMaskRaster.cs
The mask bake's compute: world-space triangles in, the RG8 mask, RGB8 tint and R8 zone bytes out, with no engine
object touched. Owns the triangle fill, the shore distance pass and the off-sheet tint, and runs
in row bands on the thread pool. A texel off the sheet takes its tinted neighbours' mean, so the filtered tint does
not lighten the sheet's edge. `OceanMask.cs` collects the triangles and uploads the bytes. `OceanMaskRasterTests`
hold the banded bytes to a plain single pass.

## src/Effects/OceanMovers.cs
The rule for the boats an animation carries across the sea, with no engine object touched. A mover is the target
of a played OBJECT_MOTION_FROM_TO with a translate channel, an SI script or an OBJECT_TRANSLATE_STATE; reset
states, spins and ballistic debris are not. `GameSession.AnimatedMovers` asks `AnimRuntime.TargetsOf` for the
nodes `MovingEvents` moves, resolved by the dispatch's own rule. `OceanMask`'s walk judges each: a hull has its origin within 3 m of
sea level over the world's own sea-level water and meshes within 2 m of it; one hidden at the bake is judged by
`Ocean` once it shows. `Nearest` picks the zones when more hulls float than there are slots. `OceanMoversTests`.

## src/Effects/Precipitation.cs
Rain and snow from `weather.json`'s precipitation block (`WeatherState.PrecipData`): ONE MultiMesh
whose shader derives each quad's position from a per-instance seed, `csky_time` and
`CAMERA_POSITION_WORLD`, wrapped into a camera-centred box, so the field costs no per-frame CPU.
SNOW flutters as flakes; RAIN streaks along the data's world fall velocity. The sprites are
procedural (`MakeFlakeTexture`/`MakeStreakTexture`), the original having drawn untextured
primitives no archive carries. Schema and the data-to-look TUNE mapping:
[../formats/weather.md](../formats/weather.md).

## src/Effects/Weather.cs
`WeatherState`, the flown mission's own weather.json as per-zone `ZoneWeather` records: fog colour,
ranges and altitude, the sunlight block resolved into a world light, a sun orientation and its two
uncollapsed colours, the cloud-cover whiteout band, wind, and precipitation. `DefaultDiffuse` and
`DefaultAmbient` are the install's modal day pair, public because both lighting mappings anchor a
zone against them. `ResolveZone` picks the flown zone by name, falling back to the one zone whose
horizon subtree carries meshes where the requested one is empty and this mission also fogs it;
`CameraWeatherState` and `ZoneForState` are the per-frame camera zone `WeatherRig.Tick` publishes.
Schema: [../formats/weather.md](../formats/weather.md); runtime: [../org/weather.md](../org/weather.md).

## src/Effects/ViewerSet.cs
The "what do the cameras see" registry, session-owned and bound once after the rigs are built, so
every draw rule needing it shares one registration, single player included. `Cameras` hands back
the raw bound list for a consumer that needs each viewer's own field of view and pane height and
already skips a freed instance; `Positions` and `Poses` are the two derived shapes, the latter
filling a caller-owned buffer for a consumer that republishes the set every frame. It carries
cameras, not the screen-size or view-depth arithmetic, which stays in `ScreenSize`. Its consumers
are the tracer floor, the puffer distance fade, the screen wash and the world-light budget.

## src/Effects/WindStreaks.cs
Remake-only wind streaks, a layer OVER the authored speed cue (`Flight/Hud/SpeedCue.cs`) rather than a
replacement: one MultiMesh of thin procedural quads in a camera-centred wrap box on
`Precipitation`'s pattern, aligned to the aircraft's world velocity, with the same near and rim
fades. `Create` returns null unless `GraphicsMode.Enhanced` and the `graphics.windStreaks` config key is on (it ships off); `HumanFlightAdapter` gives each player
pane its own, `FlightController` drives it, and a live switch takes it out of the tree and back. `Update` is the whole law: opacity zero below a
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
`ProjectileStage.RegisterScorch` is the one decision point (a bowl was carved, or the impact played one
of `EffectCatalogue`'s fireballs); `Flight/Projectile.ScorchSink` is the hook and skips water. Size
comes from the weapon's crater radius. Every size, darkness and life constant is TUNE.
