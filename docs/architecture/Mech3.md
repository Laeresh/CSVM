# Mech3

Everything that turns the player's install into a live scene: the mech3ax extraction readers, the GameZ to Godot builders, and the animation runtime that drives the world (including its `Mech3/Anim/` subfolder).

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Mech3/GameZ.cs
Loads a mech3ax GameZ extraction (zip or unpacked dir): nodes/models/materials/textures JSON into
plain C# objects, reading both the v0.6.1 "legacy" and the fork "unified" shapes (field mapping:
docs/formats/gamez.md); `WorldTransformOf` resolves a node's world transform without building it.
Carries the node's `flags.intersect_surface` as `IntersectSurface` (default true when flags are
absent) — the original's collision-participation flag, honoured by WorldBuilder.NoCollisionNode.
Carries `flags.active` as `Active` (same default) — initial runtime visibility, honoured by
`WorldBuilder.Add`, which stages an inactive world-build root hidden so mission choreography can
still resolve and activate it later.
Carries the node's `zone_id` as `ZoneId` (default −1 when the field is absent, i.e. ungated) — the
original's per-node visibility zone, honoured per node by `SceneBuilder` and per camera by
`Mech3/ZoneGate.cs`.
Carries the node's `field040` as `MissionTargetWord` (absent from the JSON when zero), the word the
original reads a mission structure's flag and team out of: bit 31 flags it (`IsMissionStructure`),
bit 22 marks a gasbag (`IsGasbagStructure`), and bits 0-21 are eleven two-bit ownership slots, one
per mission of the chapter, so the team needs the mission number — `MissionStructureTeam(mission)`
reads one slot, `MissionSlotOf` turns a mission folder name into that 1-based number, and
`WorldObjectTeam` resolves a node the way the engine's factory does, taking the nearest ancestor's
slot where the node itself authors none. `SceneBuilder` stamps the answer on the flagged nodes and
`DestructibleRegistry` puts it on the pool (docs/org/targeting.md "What a mission structure's team
is").
Carries the World node's partition grid twice: `PartitionNodes` is the flat distinct set every
placement walk uses, and `PartitionCellNodes` (with the grid origin and cell size read off the first
cell's own bounds) keeps the per-cell membership an area query needs (see `WorldPartitionGrid`).
`IsMarkerGizmo(meshIndex)` classifies a mesh as an authoring mark rather than scenery (one flat-
coloured untextured triangle — see docs/formats/world-structure.md); SceneBuilder draws none.
Carries each model's `flags.lighting`/`flags.fog` as `GameZMesh.Lighting`/`Fog` (default true) —
the original's self-lit and unfogged marks, honoured by SceneBuilder's per-model shader variants.
Carries each material's `soil` label as `GameZMaterial.SoilId` (the original's numeric surface type
id — `player_crash_*`/`touchdown_*` selection indexes on it, `analysis/surface-classification/
FINDINGS.md` 2026-08-11), mapped through `SoilLabelToId`. An unmapped label throws rather than
defaulting to 0 — mech3ax's label is just its Soil enum variant name, and a new one means the
extractor changed, not that the id is 0.
Parses the WHOLE per-polygon `materials` list: element 0 is the base skin, the rest become
`GameZPolygon.OverlayPasses` (`GameZPolygonPass`: material + its own UVs), which SceneBuilder draws
as extra surfaces — 619 polygons install-wide, none in planes.zbd (docs/formats/gamez.md).
Parses the per-polygon `zone_set` list into `GameZPolygon.ZoneSet` (`int?`, unified-only; at most
one value per polygon install-wide — docs/formats/world-structure.md's census); nothing reads it
yet, parse+census only (`BL-057`).
`VertexColorsRestateMaterialColor(poly, materialIndex)` spots the redundantly-duplicated flat
colour: an untextured (`Colored`) material whose colour every one of the polygon's vertex colours
repeats. That is ONE authored value in two slots, so SceneBuilder applies it once instead of
multiplying (which squares it — 176 → 120). 87 polygons install-wide, 85 of them the skydome
skirts, censused per chapter by `CSVM.Tests/FlatColorTests.cs`.

## src/Mech3/TextureArchive.cs
Texture lookup over an unzbd texture zip or unpacked PNG dir; absorbs the stored-name quirks
(20-char truncation prefix match, legacy `.-N` renames, the fork's trailing doubled period — see
docs/formats/gamez.md) and classifies each texture's alpha channel via LastHadAlpha /
LastAlphaIsSoft ("soft" = the ink is mostly partial alpha: opaque/ink < 0.45, measured install-wide
in `analysis/alpha-classification/`; drives blend-vs-scissor — scissor both erases sub-0.5 ink AND
solidifies partial alpha above it, so only essentially-binary ink scissors faithfully).
`SoftAlphaCoastline` names the five waterline sheets the ratio misreads, because their solid
dry-land half outvotes the feathered ramp that is the point of the texture.
`Build` is the one construction path — decode, classify, drop-in, mip chain — and `Find` caches
its result; `BuildMipped` hands the same Image to `--dump-mips` un-cached.
`LastAlphaClass` is a second, unrelated alpha answer: the extractor's own `None`/`Simple`/`Full`
field, read from the archive's `manifest.json` at construction, which is what the original's
per-surface lighting exemption keys on (docs/org/vertexLighting.md). It is not interchangeable with
`LastHadAlpha`, and the pixel test it falls back to where no manifest ships loses the `Simple`
class.
Two sets name the textures the retail data itself lacks, and they differ in what gets drawn:
`KnownAbsentFromGameData` (`pir_spinner`, `barngrill`) takes a neutral gray card, while
`AbsentAndUndrawn` (`cloud1`, `cloud2`, C3's skydome) drops the polygon in `SceneBuilder.BuildMesh`.
Anything on neither list stays loud (debug magenta plus a not-found line), since it is likelier our
own name resolution failing. ⚠ `IsAbsentAndUndrawn` also requires the lookup to fail, so the seven
chapters that do ship `cloud1`/`cloud2` keep drawing them — see docs/org/textures.md.

## src/Mech3/SceneBuilder.cs
Shared GameZ-subtree → MeshInstance3D builder: triangulation, material/mesh
caches, nearest-LOD only, skip predicate. Replicates the original's draw order as depth bias:
priority, then subface, then overlay pass, then within-mesh surface rank, with the cross-node
tie-break coming from `NodeBiasOf` — the world's `ConflictRank` map where the caller set one, the
flat node index otherwise (aircraft, `--node=`). Every `node_bias` in the project goes through
that one method. `BuildSubtree`'s `zoneGate` flag (off by default; on for the world walk and the
map-edge extension) moves each built mesh instance onto its own node's `zone_id` visual layer
(`Mech3/ZoneGate.cs`). It stays off for the cloud deck and the skydome, which are per-rig
camera-anchored copies gated by `Node3D.Visible` instead.
`CollidersForMesh` splits a mesh's colliding geometry into one trimesh per surface class actually
present (water/buildings/untagged, each polygon's own texture deciding), each also carrying
`SurfaceIdMeta`, the original's numeric surface id for the bucket's dominant material. A class
splits again by SIDEDNESS, since `BackfaceCollision` is a whole-shape flag and the polygon's own
`SHOW_BACKFACE` decides: the one-sided half is emitted with REVERSED winding, because the source's
visible side is Godot's back face. Both halves go on ONE body, so body counts, names and metas are
unchanged. Each collider-bearing node registers with `WorldCollision`, which owns its `Disabled`
flag from then on. `CollisionSidedness` and `CollisionBackToBackPairs` are that split's census. A
`GameZ.IsMarkerGizmo` mesh draws nothing but its Node3D is still built, since animations
attach puffers and sounds to those nodes by name.
A node the scene data flags as a mission structure carries `MissionStructureTeamMeta` (and
`MissionStructureGasbagMeta` where it is a gasbag), the channel `DestructibleRegistry` reads a
pool's team through, since the registry meets a pool as a Node3D with no gamez node to ask. The
team is resolved HERE because a node authors one owner per mission of the chapter and only the
build knows which mission it is: `MissionSlot` carries that number in, `WorldSession` sets it from
the mission folder through `WorldBuilder.MissionSlot`, and a node authoring no owner for this
mission is stamped with none.
A surface's colour is `vertex colour × material` (the original's baked-lighting modulate), except
where `GameZ.VertexColorsRestateMaterialColor` detects the two are the same authored value
(mostly skydome skirts — docs/formats/weather.md). `DebugClutterFlag` (`--debug-clutterflag`)
overrides that colour with the decoded `no_clutter` bit instead. Every shader on one instance
shares the ordered preamble in `csky_instance_uniforms.gdshaderinc`; see that file for the
contract, and `GetBiasShader` for how the model's `lighting`/`fog` flags select shader variants
instead of driving a uniform. A textured surface takes a second, independent lighting exemption from
its own texture's alpha class (`LastTextureExemptFromLight`), which is the original's per-texture
gate; `AlphaExemptMaterialCount` is its per-chapter census. It applies on the fullbright world pass
alone, since the shaded aircraft pass carries no world-light term for it to cancel. The mesh, material and collider memos are per builder, since every
override is baked into what they hold; the three SHADER memos are process-wide, because a generated
text is a pure function of its key and Godot charges a compile for each fresh `Shader` a material
takes (`docs/verification.md` PERF-22). Each of the three keys also carries `GraphicsMode.Enhanced`
as its own bit. In enhanced mode `GetBiasShader`'s world arm for a surface authored `lighting: true`
drops `unshaded` and shades off the decoded normals (pre-negated for `cull_front`) as a matte
material, keeping the gamma modulate and the fog while dropping the `csky_world_light` multiply and
the LIGHT_STATE spill; `lighting: false` surfaces and both billboard generators stay fullbright. That
lit arm writes its fog ramp through the spatial shader's post-lighting `FOG` output instead of into
`ALBEDO`, so the fog colour is never itself lit and a fully fogged fragment lands on the same value
the fullbright arm's mix gives. In enhanced mode the two billboard generators' `glow` arm (the
original's own camera-facing light-source
class, plus the flare/fire/flame cylindrical facades) additionally scales its colour by
`EmissiveScale` so the pixels exceed 1.0 for the glow pass; those arms are `unshaded`, where Godot
discards EMISSION, so the scale is applied to the colour. Inside that lit world arm, a surface
`ClassifySurface` calls water takes `WaterRoughness`/`WaterSpecular` in place of the matte values,
which is what `Launcher.EnableWaterReflections`' screen-space reflection has to march against. See
"Rendering: the enhanced graphics mode" above for the divergence record as a whole.
Format/decode: docs/formats/gamez.md, docs/formats/world-structure.md, docs/formats/gotchas.md,
docs/org/vertexLighting.md (the lighting-bit census enhanced mode's glow arm is keyed on).

## src/Mech3/ZoneGate.cs
The original's per-node visibility gate (`FUN_0056c430`). `FUN_004d62d0` arms the camera each frame
with the zone set `{0, camera weather state}`; the walk draws a node iff its gamez `zone_id` is
`-1`, or is in that set. Four members: `Draws(zoneId, state)` (the rule), `LayerFor(zoneId)` (the
visual layer a gated zone's meshes are MOVED onto — 0 for `-1`/`0`, i.e. leave on the default
layer), `CullMask(mask, state)` (narrows the band to one zone, every other bit untouched) and
`OpenCullMask(mask)` (the whole band back — `--no-zone-cull` and the launcher camera's per-session
reset). Constraints and evidence live on the class itself; see `CSVM.Tests/ZoneGateTests.cs`.

## src/Mech3/ConflictRank.cs
The world's cross-node draw-order tie-break. Buckets every built triangle by its world plane,
finds the cross-node pairs that are coplanar, same-priority, same-subface and genuinely overlap
(clipped area > 1 m², never an AABB touch), and layers that DAG by longest path — so a node's rank
counts conflicting layers beneath it, not nodes before it. Every edge runs low node index → high,
so the layering is a topological order of the original's own draw order and cannot invert authored
layering. `WorldBuilder.RankConflicts` runs it before the build; 11–45 ms per chapter.

## src/Mech3/WorldCollision.cs
Owns every `SceneBuilder`-built collider's `Disabled` flag and derives it: enabled exactly while the
owning node is visible in the scene tree and no ancestor is faded out. `Track` (called once per
collider-bearing node as it is built) binds it to the node's `VisibilityChanged` — which Godot
propagates to descendants — plus `TreeEntered`, because a world is assembled and bootstrapped while
still detached, where visibility writes emit nothing. `SetFaded` is the second input: an
`OBJECT_OPACITY_*` fade is a shader parameter visibility knows nothing about. `OwnerOf` reads the
same relation backwards, naming the world object a collider body stands for: `SurfaceIdMeta` marks
the per-surface-class bodies `SceneBuilder` carved from one mesh node, so they answer their shared
parent, and anything else answers itself. Callers that must count objects rather than bodies
(`ProjectilePool.ApplyDamage`'s splash shares) key on it.

## src/Mech3/PlaneBuilder.cs
Builds one aircraft from its GameZ subtree (shaded, cullBackfaces: true — interior lattice must be
backface-culled or it paints over the skin), skipping cockpit/destroyed/shadow subtrees, and
`*_hook` unless `dockingHook` asks for it: a human rig gets the airframe's skyhook group built and
parked at its archive-authored inactive bit, because the hookup cutscene's `<x>_hook_extend`
activates that group rather than creating it (`docs/formats/anim-definitions/cutscenes.md`).
Repaint(scheme) re-liveries the built plane in place; BuildDestroyed builds the wreck subtree with
the plane-root→destroyed transform chain baked in; WingFlares/DamagePanels expose collected nodes.
Flight (`spinningProps`) now builds the static `staticpropN` disc alongside the spinning blur discs
it always built, not just one or the other — the startprops/stopprops choreography cross-fades
between them at spawn/engine-stop (`FlightController`), so both must exist. `staticrotorN` (the
autogyro) is unaffected and stays skipped in flight — that def only names propeller nodes.
`Build` also reads `CockpitCameraOffset`, the plane-local `cockpit_camera` marker translate
(`MarkerRig.FindNamedMarker`, fallback the origin), for `CameraController`'s first-person
placement; the marker read walks past the alternate-state subtrees to the
authored node in the top-level `markers` group.

`cockpitInterior: true` takes `cockpit1` back out of the skip list for
that build alone and mounts it hidden as `CockpitInterior`: local transform = the
`cockpit_camera` offset, a uniform `InteriorScale`, and the fixed
`CameraController.HeadPitchOffsetRad` tilt, then `ParkInteriorStates` walks it. Only a
human rig asks for it — an AI plane never builds a cockpit. The subtree's `pcdp4`/`pcdp6` torn-skin
panels build hidden alongside it, kept off `DamagePanels` (the exterior set the pairing walk
measures mesh centers over) and exposed instead on their own `CockpitDamagePanels` list, which
`DamageVisuals` flips off the same `pdpanel4`/`pdpanel6` entries as the exterior pair (B12).

⚠ **The interior's off-states ship `active: true`.** Five windshield bullet-hole groups
(`bullet1`…`bullet5`) and two warning lamps (`lowalt_on`/`stallwarning_on`) are authored visible on
all 11 airframes and hidden engine-side until something drives them, so an unparked build paints
bullet holes across the sky of a pristine plane and holds both lamps lit. `IsInteriorDrivenState`
is that named set; `ParkInteriorStates` hides it plus anything the gamez marks `active: false` (the
Devastator's `nitrogauge`, the only such node). ⚠ It is a NAMED set, not a blanket hide: the
damage-dial zones, the belt segments and the needles are always-drawn geometry that changes
COLOUR, which is `GaugeCluster`'s own decode of the same nodes.

⚠ **The interior is authored in its own space, and the two spaces are not a similarity apart.**
The eye sits at `cockpit1`'s origin looking down −Z (`extracted/zrdr/instruments.zrd.json` places
the whole instrument panel at z −17.5 straight ahead of it), while the interior's own elevators sit
at y −10.5 where the exterior's sit at −0.40 — it is a stylised model built to be looked at from
one point, not a scaled copy of the aircraft. So the framing is scale-invariant and
`InteriorScale` is a port TUNE choosing only how the interior composites against world geometry.
`cockpit2` is skipped defensively and appears in no shipped tree.

⚠ **The mount carries the −4.70° head-pitch tilt, and that is what puts the gunsight on the guns.**
The offset tilts the WORLD view down; the pilot's relationship to his own cockpit does not tilt
with it, because the original draws the interior in its own pass from the interior origin along the
interior's own −Z. Mounting the subtree tilted is how a single-pass renderer says the same thing.
Measured against `OriginalScreenshots/Videos/CAP-02 Cockpit Second10.mp4`: there the sight ring's
crosshair sits 4.79° above screen centre and never moves by a pixel across the clip, which is the
head-pitch offset itself — the sight is on the nose axis, and the gun pipper (which marks that same
axis, `ImpactReticle`) sits on it in straight flight. Mounted untilted the sight rides 3.9° above
the pipper and the two never meet. Head-look is NOT applied to the mount: the interior stays
plane-fixed, so panning the head still swings the cockpit across the view, as the original does.
⚠ A residual remains: the tilted mount overshoots by 0.60°, leaving the pipper ~6 px above the
crosshair at 720p where the original has them coincident. The exact fit is a 3.82° tilt, but that
is a Bloodhawk-fitted number with no decode behind it and the sight's height is per-airframe
geometry, so the decoded constant is what ships. `BL-` follow-up: measure the same offset on a
second airframe's cockpit footage before trading the constant for a TUNE.

## src/Mech3/PaintScheme.cs
One aircraft livery: pattern name + three colours + three decal indices — the paint_* record a
vehicle.json def carries (see docs/formats/paint.md). LoadCatalog keeps one scheme per pattern
name (the 12 shipped patterns); Random() draws a plausible livery when none is given.

## src/Mech3/PatternLibrary.cs
Decodes the original's .BM paint patterns from extracted/rof/ASSETS/GRAPHICS/<PATTERN>/ (produced
by ExtractRof.ps1; .BM layout in docs/formats/rof.md). PatternsFor(prefix) lists the patterns
shipping skins for one aircraft — a pattern is per plane; Skin() caches per (pattern, skin) so
several aircraft in one session share a decode.

## src/Mech3/PlanePainter.cs
Applies a PaintScheme to one aircraft: composites its skins from the pattern's region masks and
swaps the three decal placeholders. Read docs/formats/paint.md and rof.md first — the composite
formula, the shading-plane choice, and the bottom-up .BM rows are documented there.

## src/Mech3/PropParts.cs
Classifies a plane's propeller/rotor subnodes by name (staticpropN/staticrotorN, nitropropN,
propN/propNb, rotorN/rotorNb) and supplies each spinning kind's local axis + rate — the
XYZ_ROTATION values (deg/s, docs/formats/anim-definitions.md) from plane_props.json (spinprops)
and autogyro.json (agyro_rotors): props spin about local Z, rotors about local Y.

## src/Mech3/ControlSurfaces.cs
Classifies a plane's control-surface mesh nodes + hinge axes: the deflecting node (l/r_aileronN,
l/r_elevatorN, l/r_rudderN, the Fury's l/r_rudder_rotate) hangs under a hinge parent group whose
transform places/orients the hinge line; ailerons/elevators hinge about local X, rudders local Y.
The name patterns are the original's own six `sprintf` node lists (docs/org/flightModel.md, "The
original's control-surface animation"), which is why the elevators classify per side: they carry a
differential roll term, so left and right settle at different angles.

## src/Mech3/WingLights.cs
Single source of truth for wingtip nav lights: the flare-node predicate (wing_flare1/2), the glow
texture (oil_liteflare), the warm-amber flash colour (0.88, 0.78, 0.36 = wing_light.json's
LIGHT_STATE COLOR), the blink period (1.5 s = its LOOP SEQUENCE_OFFSET) and the point-light range
(0.5-1.25 m, also LIGHT_STATE). PlaneBuilder hides and re-skins the flares (additive tint, one-sided
as authored, no billboard); WingLightBlinker flashes them and emits a matching OmniLight3D per side.

## src/Mech3/WorldBuilder.cs
Builds a chapter world (fullbright): World children + partition-referenced subtrees; skips `horizon`
(`BuildHorizon` builds the camera-anchored skydome separately, unfogged per its authored
`fog: false`), `fvol*` (`IsFogVolumeNode`, shared with `FogVolumeSpec.VolumesOf` so the skipped set
and the cloud-scatter set are one list), `dzpaths`.

Every world node the walk builds is stamped with its own `zone_id` visual layer (`SceneBuilder`,
`zoneGate: true` — see `Mech3/ZoneGate.cs`), so the camera's weather state culls it. The **deck**
and the **dome** are the two exceptions: both are per-rig camera-anchored copies with no shared
visual layer to stamp, and take the zone rule through `Session/WeatherRig.Tick` instead. The deck
(`CloudDeck`), its altitude, its SUNLIGHT dimming and its map-edge annulus, and the dome's
per-chapter zone selection and dome count (`DomeZonesToBuild`), are all measured original
behaviour — read docs/formats/weather.md before touching either, and docs/org/weather.md for the
original's own zone-selection function map.

`HideUnplacedEntities`/`RestorePlacedEntities` switch off, then restore, the entities a chapter
parks at the world origin awaiting mission placement. `NoCollisionNode` exempts sky/cloud/billboard
geometry and anything authoring `intersect_surface: false` from colliders (docs/formats/gamez.md).
Placed roots whose authored `active` flag is false are built and indexed but begin hidden; C3/M03's
`barracuda` is activated later by `sub_movement`.
A static probe (`analysis/collider-probe/probe.py`) reproduces `ColliderCount` independently
(`BL-070`). `CreateEdgeExtender` hands off to `MapEdgeExtender.cs`; clutter decoration is
`Clutter.cs`.

Static helpers (`HorizonZonesOf`, `CloudDeckAltitudeOf`, `DomeZonesToBuild`, `DetachedWorldAabb`,
`FogVolumeZoneIdOf`, `MatchNodes`) are pure over `GameZ`/a built subtree and testable off-engine
(`CSVM.Tests/HorizonDomeTests.cs`, `DeckRegimeTests.cs`).

## src/Mech3/MapEdgeExtender.cs
Rolling window (`Rings`=5 of 1024 m cells, diffed only on cell crossings) of repeated border tiles +
clutter (grown from `ClutterBuilder.ExportedKinds`, each copy keeping its source stamp's fade
thresholds) continuing the world past the map edge, one window per session shared by every player
camera; the window's 5120 m reach exceeds the largest authored clutter fade at every detail level, and is unchanged by `graphics.clutterFarFade=false`, which simply leaves the window edge (under the mission's fog) as the extension's visible limit. `ClassifyGroundMesh`/`IsCompletionStrip`/
`FoldAxis` are pure statics pinned by `MapEdgeTileTests`/`MapEdgeFoldTests`; `--dump-tilegrid` writes
the per-cell acceptance census `WriteCensus` builds. The original's own continuation behaviour and
the per-chapter fold measurements: docs/formats/world-structure.md.

## src/Mech3/Clutter.cs
Stamps the boot-script clutter templates across placed polygons carrying the template's ground
texture, **at the polygon's own texture-UV lattice** — one stamp per integer UV repeat across each
triangle; sprites → one fullbright Y-billboard
MultiMesh per kind, solids → `SceneBuilder.SharedMesh`; the split is `SceneBuilder.ClassifyBillboard`.
The sprite shader takes the decoration model's own `lighting`/`fog` flags as variants (every tree and
bush card in the install is `lighting: false`, so clutter does not dim with the mission SUNLIGHT),
plus a UV-clamp variant from `SceneBuilder.UvsWithinUnitSquare` over the kind's own card UVs.
`BuildKindInstance` applies the texture's alpha-class lighting exemption to that `lighting` flag as
the world path does, so a card cannot take a sun term the world surface beside it is exempt from;
on the shipped data it changes nothing, because the alpha-textured cards outside C5 are the tree and
bush families, which already author `lighting: false`, and C5 authors `world_light` 1 in every zone
(docs/org/vertexLighting.md).
Every stamp carries its authored far fade as MultiMesh custom data (`Kind.Fades`, exported beside
`Placements`), applied by the sprite shader and by `SceneBuilder.SharedMesh(clutterFade: true)`
for the solid kinds, under the `EffectsLevel` global; the fade's draw is its own stream off the
placement seed. `TemplateNames` reads the chapter's `AddClutterTemplates` list **unfiltered** — which district
dresses a given patch is the per-polygon `no_clutter` gate's decision (`PlaceOnMesh`), not a
curated list here; `OverrideTemplateNames` is `--clutter-templates=`'s replacement for it — the caller's names,
filtered to the ones this gamez carries a root for — so one district can be loaded alone and A/B'd
against the original. It prints one line naming what was requested, what resolved and what this
chapter does not carry, since an absent name is retail-data-normal and would otherwise read as an
empty district.

**The original's placement runtime is written up in [org/clutter.md](org/clutter.md)** — the
function map, the template lookup's first-match scan, the UV-lattice stamp and its local triangle
frame, the engine defaults no chapter authors, and the weight list's sum-and-divide. Read it before
changing a placement rule; the authored side stays in [formats/clutter.md](formats/clutter.md) and
[formats/templates.md](formats/templates.md). The remake-only rules (no world grid, the fixed
placement seed, the `seen` dedup, shared collision shapes) are comments on the members that hold
them. ⚠ The solid decorations' shapes stay TWO-SIDED where the world's honour `SHOW_BACKFACE`, for
two reasons: `AppendTriangles` does not alternate a strip's winding, so its triangles have no agreed
front to be solid from; and whether the original's intersection database holds a stamped decoration
at all is undecoded, since [org/weaponRay.md](org/weaponRay.md)'s per-node gates are gamez node
flags. `SolidCollisionOneSidedTriangles` sizes what that leaves (C2 1439 of 1439, C5 3156 of 3738);
only those two chapters build solid decorations.

## src/Mech3/ClutterTemplates.cs
The chapter's `templates.zrd` (`ClutterTemplateSpec.Load`/`.Parse`): one `ClutterKindProps` per
clutter DECORATION MODEL — `substitute`'s weighted roll, `scale_range`, `far_fade_range`, and the
jitter/rotation/slope/damage keys the retail data leaves at their defaults. Schema, offsets and the
per-chapter census: docs/formats/templates.md. Static over a reader list, so all eight chapters are
pinned off-engine (`CSVM.Tests/ClutterTemplatesTests.cs`). Consumed by `ClutterBuilder` for
`substitute`, `scale_range` and `far_fade_range`, the last through `FadeThresholds`, the pure
per-stamp `(near², far², reciprocal)` the fade shaders read. See the class and member doc
comments in the file, and docs/formats/templates.md, for the decode detail — the keying by
decoration model rather than template, the nested-pair bound grouping, the slope-key inversion, and
the substitute-roll/duplicate-name resolution rules are all there.

## src/Mech3/Zrdr.cs
Zrdr extraction reader (zip or unpacked dir): `LoadFile`, `LoadFileOrEmpty`, content-sniffing
`LoadMatchingFiles`, name-predicate `LoadFilesNamed` (for families with nothing to sniff, e.g. the
`ne0*` nets), and `ZrdrDict`, the key/[values…] view over a reader's alternating list.

## src/Mech3/SurfaceRegistry.cs
`Names[id]`: the id→name table a struck material's `soil` field indexes into, so a caller can build
`"player_crash_" + name` / `"touchdown_" + name` and resolve the same def the original resolves.
`IdForName` is the parse-time direction (a name the registry does not carry answers null and is
discarded, as `FUN_005ad630` discards it), which is how a weapon's `IMPACT` blocks land at their id;
`Default`/`Water`/`Player`/`Enemy`/`Buildings` name the five slots code branches on outright.
Ids 0–5 are compiled into `crimson.exe`; ids 6–13 are the `LoadSoils`-loaded list from `ZBD/zrdr.zbd`
`0xe631c`, reproduced by `analysis/surface-classification/soils_list.py`.

## src/Mech3/AiNets.cs
The chapter patrol-net reader (`docs/formats/ai-nets.md`): every `ne0NNNNN.zrd.json` in a chapter
zrdr scope joined with its `neindex.zrd.json` name — nodes, the explicit edge list, raw per-node
tags, the trailer attach target, and the net's own three volumes (`Volumes`, record elements 2–10
as an `AiVolumeSet`). Plus the lookups both ways the data references nets:
`ById` (aiv field 0), `ByName` (egen/zeppelins/objectives, case-insensitive), `Resolve` (either
spelling), and `ChapterFirst` (the net an Instant Action actor is given). Consumers:
`UI/AiNetsOverlay.cs` and `Flight/AiNetFollower.cs`. Golden counts asserted in
`CSVM.Tests/AiNetsTests.cs`.

## src/Mech3/Maneuvers.cs
The shared maneuver-library reader (`docs/formats/ai-rosters.md`): `zrdr/maneuvers.zrd`'s 17
entries as `Maneuver` (name, `natural_touch` difficulty, timed attitude steps, the
autogyro/relative/nitro/bias flags), plus the selection cull (`EligibleFor`: difficulty ≤ the
pilot's 1–9 `natural_touch`, no interpolation table — the stat has no `ai_skill_parameters`
entry by design) and the roster `signature_maneuvers` bitmask decode (`SignatureNames`, over
`ExeTableOrder`). Consumers: `Flight/ManeuverExecutor.cs`; goldens in `ManeuversTests`.

## src/Mech3/EnemyGenerators.cs
The mission `egen.zrd.json` reader (docs/formats/mission-entities.md): the enemy generators that
feed AI aircraft into a live mission, typed as `EnemyGeneratorDef` in the three shipped shapes
(zeppelin launch 17, plain spawner 5, moving spawner 1); a `[null]` file reads as an empty list.
An unauthored door pair takes the loader's node-name default (`DefaultDoorAnim`, `%.5s_open%.2s`),
the close being the open name, as the original's loader has it. Consumed by
`Session/AiGeneratorRuntime`; golden counts in `CSVM.Tests/EnemyGeneratorsTests.cs`.

## src/Mech3/Zeppelins.cs
The mission `zeppelins.zrd.json` reader (docs/formats/mission-entities.md): the 58 zeppelin
instances typed as `ZeppelinDef` — motion limits, net name, targets, healthy zones +
`num_healthy_required` (defaulted to 1 and clamped to the healthy count, the decoded load
rule), engines, gasbags, cannons and `cannon_health`. Motion keys feed
`Flight/ZeppelinMotion` (F17); the damage half feeds `Flight/ZeppelinDamage` +
`Session/ZeppelinRuntime.WireDamage` (F18). Fixture units + install goldens
in `CSVM.Tests/ZeppelinsTests.cs`.

## src/Mech3/InstantAction.cs
`InstantActionDef` (docs/formats/instant-action.md) plus the three
producers decision 2 names, converging on one record: `Load` for a chapter's shipped
`ia.zrd.json`, `LoadFromJson` for a hand-authored `--ia=<path>` file — a plain JSON object using
the same field names, not the zrdr archive's flat-alternating shape — and `BuildFromWizard` for
the launchscreen's Instant Action wizard. `Load`/`LoadFromJson` funnel through one private
`BuildDef(ZrdrDict)`; `LoadFromJson`'s only job is `FromJsonObject`, the small mapping from a JSON
object onto the same key/[values…] shape `ZrdrDict` already wraps (a nested `group1`…`group4`
object flattens the same way), so a JSON-authored mission parses through exactly the same
field-population path a real chapter's does. `BuildFromWizard(baseDef, missionType, playerPlane,
numWingmen, wingmanPlane, waves, lives)` takes a different shape: `baseDef` is the chosen
environment's own `Load` result, and only the fields the wizard actually lets a pilot configure
are overlaid — the ace, the zeppelin node names and `disallow_missions` carry over from `baseDef`
unedited, since they are chapter-level facts with no wizard control. It applies the same
`dogfight_ace` zero-forcing rule `BuildDef` does, so a stale wizard wingmen/waves state behind a
just-switched-to-ace mission type can't produce a solo-breaking def. `EmptyWave` (`MakeWave(null,
false)`) is what an unconfigured wizard wave slot resolves to — byte-identical to a JSON file's
own omitted `groupN`, which is the whole point: `LaunchMenu.WaveFor` returns it outright for any
slot at 0 enemies, regardless of what the militia/aircraft/skill cursors are sitting on.
`Defaults()` is `BuildDef` over an empty `ZrdrDict` — the wizard's fallback if an environment's own
file somehow fails to load. `spawn_points` and `dzones` stay where they already were
(`Flight/SpawnPoints.LoadIa`, `Flight/StuntMission`) — this def does not repeat either.
`PlaneNodeFor` is the eleven-entry display-name → gamez-node table
(`"Bloodhawk"` → `"player_bhawk"`), a deliberate duplicate of `UI.LaunchMenu.Planes` rather than a
shared one — the plan's file-contention notes reserved `LaunchMenu.cs` for H15/H16 alone. Fixture
units + install goldens in `CSVM.Tests/InstantActionTests.cs`; the wizard's own build path is
`CSVM.Tests/InstantActionTests.cs`'s "The wizard's own build path" region (a wizard-built def and
its hand-authored `--ia=` equivalent compared field for field) and
`CSVM.Tests/LaunchMenuWizardTests.cs` (the militia/aircraft/skill rosters, `WaveFor`).
Every optional key resolves to the original's own reset-then-overlay default rather than null; see
`InstantActionDef`'s class and member doc comments in the file, and docs/formats/instant-action.md
"The built-in defaults", for the per-field rules (the `Devastator`/ace fallbacks, the
`dogfight_ace` parse-time wingmen/wave zeroing, `ZeppelinType`'s lone undecoded default, and why
`ground_target_name`/`ground_target_node` stay out).

## src/Mech3/AiSkills.cs
The AI pilot-skill constants (docs/formats/ai-rosters.md): player.json's
`ai_skill_parameters` block as `[value@1, value@9]` endpoint pairs indexed by the 1–9 rating
(`At`, plus named helpers for D14's two angles), the roster accessors
(`RosterSkills`: aiv slots 22–30 by stat name, `-1`/omitted = null; `RosterPrimaryTarget`:
slot 6; `RosterRatingBiases`: slot 33 as `AiRatingBias` — wildcard `Matches`, shipped pairs,
a third element accepted and preserved raw, never acted on; the spawn-facing `Roster*` readers for
slots 0–5, 20, 21, 31, 32, 40 and 65, every one defensive over a short block; `RosterAce`: slot 67,
the debrief's kill-crediting flag; `RosterInitHealth`: slot 7, null unless authored > 0;
`RosterArmor`: slot 66, null unless authored >= 0 or the block is too short to carry it;
`RosterObjectiveTarget`: slot 37, a strict boolean; `RosterHelpLabel`: slot 39 raw, gated on
`RosterObjectiveTarget` by the caller, not here) and the thin per-mission roster loader
(`LoadRoster`), plus the positional-header join that exposes disabled generator parameter blocks
(`LoadGeneratorRoster`). Units + shipped-constant goldens in `AiSkillsTests`; slot 6/33 census
goldens in `AiTargetRankingTests`; the spawn slots in `CampaignRosterPlanTests`; the two durability
gates and the absent-slot case in `RosterDurabilityOverrideTests`; slots 37/39 in
`RosterObjectiveMarkerTests`.

## src/Mech3/AiVolumes.cs
`AiVolume` (radius, upper, lower) and `AiVolumeSet` (activation, attack, return): the one shape
both authors of an AI's range volumes are read into, a roster block's twelve slots 8–19
(`FromRosterSlots`, three named per volume plus a flag no block authors) and a net record's nine
floats at elements 2–10 (`FromNetRecord`). `Overlaid` is the engine's per-field non-zero test, so
`net.Overlaid(block)` is the decoded order (docs/org/aiPilot.md "Net assignment"); the
`min_ai_active_dist` floor is `Session/CampaignRoster.cs`'s `ApplyVolumes`. The altitude bands are
read and carried but have no consumer: `Flight/AiModeMachine.cs` gates on radii alone.

## src/Mech3/VehicleDefs.cs
The `vehicle.json` def table as an index, next to `Flight/PlaneStats.cs`'s full read of one def:
`DefForBlock` strips a block name's trailing `_N` ordinals until a def matches, `ModeOf` walks
`kind_of` to the nearest authored `mode` (`jet` at the root, the engine's zero default),
`AirframeFor` finds the player airframe node an AI def's model is built from (the `p`-prefixed twin
of the nearest ancestor, else of the chain's `nodename`), `BaseDefForPlayerNode` is its inverse and
`DerivesFrom` is the variant test `PlaneStats.LoadForAi` enforces. Pure over the parsed root
(`FromRoot`), pinned in `CampaignRosterPlanTests`.

## src/Mech3/FogVolumes.cs
The chapter's `fogvol.zrd` (`FogVolumeSpec.Load`/`Parse`) plus `VolumesOf`, the gamez census of
`fvol*` volumes — the two halves of the authored ambient cloud field, rendered by
`Effects/FogVolumeClutter`. Schema, per-chapter values and the decoded/inferred split:
docs/formats/fogvol.md. Both halves are static over a `GameZ`/reader list, so the pair is testable
off-engine (`CSVM.Tests/FogVolumeTests.cs` pins all eight chapters).

`FogVolumeBox` carries the authored shape (its face planes), not just its axis-aligned bounds.
`FogVolumeWhiteout` is the in-volume whiteout rule C5 alone arms, pure and off-engine-tested
(`CSVM.Tests/FogVolumeWhiteoutTests.cs`); `Session/WeatherRig.Tick` is its one consumer.
`FindMapSpanningSlab` is the data-driven test for a chapter's map-edge-continuation cloud slab.
See the member doc comments in the file, and docs/formats/fogvol.md, for the decode detail.

## src/Mech3/Messages.cs
The game's localized string table: plain `System.Text.Json` over the extracted `messages.json`
(NOT a zrdr reader), a case-insensitive key→value map resolving the `MSG_*` keys missions reference.
`Fill`/`Format` substitute a template's `%1`…`%9` placeholders (the HUD strings' format).

## src/Mech3/MarkerRig.cs
A player airframe's weapon marker rig read from planes.zbd GameZ: `Extract` walks a `player_*`
root, accumulating locals down to each `firepoint*`/`pylon*`/`target`, and reports plane-frame
positions + co-located groups (two gun groups on one mount). `Format` prints one dump block per
plane; `PlayerAirframes` is the model→display list. The committed instrument `docs/formats/markers.md`
regenerates from, and the source of truth `--dump-markers` and `UI.MarkerOverlay` share.
`FindNamedMarker` is the sibling read for one non-weapon node by name (e.g. `cockpit_camera`,
`PlaneBuilder.CockpitCameraOffset`'s reader): the same accumulate-below-root walk, skipping
`cockpit1`/`cockpit2`/`destroyed`/`player_damage_off` so a plane whose interior/wreck carries its
own same-named node still resolves to the authored one in the top-level `markers` group.

## src/Mech3/CompiledAnim.cs
Reader for the fork's compiled `cam_anim`/`mis_anim` extraction (zip or dir): typed defs, events,
and the SI-script pool — `Script(index)` parses lazily, ordered by `metadata.json`. Decode facts
(ptr = flat node index, shifted quat labels, half-angle cubics): docs/formats/anim-definitions.md.
The `unknown_seq` destruction slot parses into `AnimDefinition.DeathSlot`, deliberately OFF
`Sequences` (bootstrap and the sequence-walking derivations never see it); only
`AnimRuntime.RunDeathSequence` dispatches it (`BL-276`, docs/formats/destructibles.md). The
`ACTIVATION_PREREQUISITE` node-state form (a run of `Parent` entries closed by an `Object` leaf
carrying `active_raw`/`required`) parses into `AnimDefinition.PrereqNodes` beside the anim-list
form's `PrereqAnims`; `AnimDefs` reads the reader spelling into the same list. ⚠ The leaf's state
is bit 0 of `active_raw`, never mech3ax's `active`: the word is two flags, bit 0 the
ACTIVE/INACTIVE list and bit 1 the def's LOCAL_NODES_ONLY scope (`FUN_0051d7b0`), so the 726
local INACTIVE entries (every `finish*gasbag*` panel finisher) compile as 2, which mech3ax reports
active. Read as active, the finisher never starts after its burn switched the panels off, and no
anim-authored zeppelin (C1/M04's `hk_zep`) can burn out and sink.

## src/Mech3/AnimDefs.cs
The zrdr front-end: ANIMATION_DEFINITIONS reader files normalized into CompiledAnim's
`AnimDefinition` model (op key SNAKE_CASE→PascalCase IS the compiled tag; unclaimed bodies stay
under `raw`). Exists because compiled archives are incomplete: `zepstate`/`startanims` are reader-only.
Unit normalization happens HERE so handlers see one convention: reader rotations are DEGREES
(ROTATE_STATE, FROM_TO rotate, XYZ_ROTATION → radians), PLAYER_RANGE metres (→ m²), ANIMATION_LOD
tokens (→ numbers) — see docs/formats/anim-definitions.md.

## src/Mech3/AnimProgram.cs
Merges the compiled + reader front-ends for one mission — load both, prefer compiled on collision,
keep the remainder — plus `StartAnims`; `ScriptFor` resolves an event slot to its archive SI script.
The mission-scope gate against the compiled manifest (a library, not a full roster) and the
shared-scope FILE gate (`ListedSharedFiles`: a shared reader file loads only when the shared
`anim.zrd` index closure, the chapter's `cam_anim.zrd` or the mission's `mis_anim.zrd` names it,
reported as `SharedFilesSkipped`) are decode knowledge: docs/formats/anim-definitions.md "Mission
library scope". Both gates apply only with a compiled mission manifest present, so a reader-only
extraction is untouched. Whatever survives them is then deduplicated on the (`NAME`,
`ANIMATION_NAME`) pair in `Add`, and since the compiled archives load first the compiled form always
wins: that third rule, absent from the census line, is what stops a plain-`NAME` shared definition
coexisting with its compiled twin, and what makes the archives' own duplicate files free. Pinned by the `mission-off-turrets` suite (C3/M03 loads no balloon def,
C3/M02 does). Which world ENTITIES a mission shows is MissionSetup
plus the interp boot script, not this file. `Defs`, `StartAnims` and `MissionLibrarySkipped` are
`IReadOnlyList` over private backing lists: one program is already shared by every runtime `Subset`
binds from it, and may be shared by several world builds (`DecodeCache`).

## src/Mech3/TextureCycler.cs
Runs the gamez material `cycle` flipbooks (water, surf, wakes, crowds) by swapping `albedo_tex`;
frames resolve at build time while the TextureArchive is open — an incomplete flipbook stays static.

## src/Mech3/EffectCycles.cs
The `EFFECTS` block of the shared `effects.zrd`: the second source of material flipbooks, and the one
that lights C1's refinery vent. An entry names a node but animates that node's MATERIAL, so this pass
resolves each entry (node → first mesh under it → surface 0's material) and writes the frame list
onto that `GameZMaterial` before the world build, leaving `SceneBuilder.RegisterCycle` to pick it up
unchanged. Two entries exist install-wide (`fire1.flt` 12@10, `fire2.flt` 6@5).

## src/Mech3/WorldSounds.cs
`SOUND_NODE` ambient looping 3D emitters: one pooled AudioStreamPlayer3D per live emitter,
following its host's pose per frame. `PlayOneShot(name, worldPos, rng)` is the one-shot `SOUND`
half: fire-and-forget destruction/impact audio, resolving a `SOUND_GROUPS` name to a member
first; the `Sound` anim event calls it. The `PlayOneShot(name, Node3D source, rng)` overload rides
the source's pose per Tick (a voice line from a moving aircraft; a freed source leaves it finishing
at its last position). `HasStream(name)` answers clip availability after the prewarm, which a def
alone cannot. Who hears these emitters is the pinned per-pane listener model (`UI/SplitScreen`);
`SetListeners` feeds the `--debug-anim` log alone, whose `dist` column names the NEAREST listener and
the pane it belongs to, because that is the pane whose volume wins the engine's mix.
`OneShotsStarted` (D33) counts every one-shot that actually started an `AudioStreamPlayer3D`, so a
suite can assert a cue fired by counting rather than grepping the `Debug`-gated log line.

## src/Mech3/WorldLights.cs
Packs the animated world's `LIGHT_STATE` point lights into the 2×N RGBAF texture the fullbright
world shader reads as spill (global `csky_light_data`, loop bounded by `csky_light_count`); the
uniform is session-global (a lit light is lit for every pane), but `Commit`'s 900–1500 m fade and
its `MaxActive`-slot significance rank both answer to the NEAREST of every viewer position handed
in, not one camera — a light beside player 4 stays lit even with player 1 far away (`BL-366`;
`AnimRuntime.LightViewerPositions`, fed from `GameSession`'s `ViewerSet`). One position (single
player) uses the single-viewer distance rule exactly. Given a parent `Node3D` (`WorldSession`
passes its world root) and enhanced mode, the same `Commit` also mirrors `_pending[0..n)` onto a
pooled `OmniLight3D` per committed light (position, `OmniRange` from range max, colour and energy
from the already-faded linear colour), so the lit world and the aircraft receive the light for
real; original mode passes no parent and spawns nothing. See "Rendering: the enhanced graphics
mode" above for the divergence record as a whole.

## src/Mech3/MissionSetup.cs
Parses + applies the per-mission `.gw` interp script that decides which world entities a mission
shows; acts on `NodeSetActive`/`DeleteTree`/`Object3DSetScroll`/`Object3DTranslate`/`Object3DRotate`/
`WorldPartitionSetActive`, counts + reports every other verb. The area verb takes two calls, not one:
`BindPartitions(gamez)` resolves its rectangles to gamez node indices through `WorldPartitionGrid`
while the gamez is in hand, and `Apply`'s `setActiveByIndex` delegate switches them in the built
world. Without the bind the verb is counted unapplied. Decode: docs/formats/interp.md.

## src/Mech3/ScriptedPath.cs
One authored waypoint path, resolved against the BUILT world through the runtime's own name
resolver rather than out of the gamez: the roster names `pp1`, the chapter carries the transform-only
subtree `pp1_aipath`, and its `pp1_aipN` children are the waypoints in ordinal order. Ten vehicles in
three missions carry one. A missing subtree, or fewer than two waypoints, resolves to null so the
caller reports it instead of inventing a route. Where the name comes from:
docs/formats/ai-rosters.md's `taxiPath` slot.

## src/Mech3/WorldPartitionGrid.cs
The world's spatial cell grid as a query: which gamez nodes does a world-space XZ rectangle cover?
Built from `GameZNode.PartitionCellNodes` (the per-cell membership `PartitionNodes` flattens away)
and the cells' own bounds. Its one reader is `MissionSetup`'s area verb. The rectangle is half-open
in cell space and the two axes run opposite ways; both are in docs/formats/interp.md.

## src/Mech3/AnimRuntime.cs
The animation engine: bootstrap passes (mission setup, anchored RESET_STATEs, ON_STARTUP,
startanims, a safety net), then dispatch-table event playback; an unhandled event kind is counted,
never fatal. It owns the live definition instances and their condition evaluation, the
destructible-damage entries (`DamageAt`/`ApplyDamageStages`/`RunDeathSequence`/`CarryState`), the
world-effects runtime (`PlayEffectAt` over a hidden template stage), the emitter prewarm, the
range-deferred start sweep and the vehicle/library-root index, and hands every construction site a
sealed `TemplateStage`. What binds a member is on that member: the pool-slot checkout reset, the
prewarm's scope, the mission-trigger closure, the undercover probe's decode, the death call's site
follow. Each dispatch axis is a sibling module with its own entry, while the router keeps the case
labels and the public fields callers configure: `SequenceRunner.cs`, `Anim/MotionSet.cs`,
`Anim/NameResolver.cs`, `Anim/EmitterDirector.cs`, `Anim/SoundChannel.cs`, `Anim/LightChannel.cs`,
`Anim/PoseChannel.cs`, `Anim/TemplateStage.cs`. Decode: docs/org/sequences.md.

## src/Mech3/Anim/
`AnimRuntime`'s private nested types promoted to top-level `internal` types in their own namespace,
purely for file size: `IAnimMotion` (`ScriptPlayback`/`SpinMotion`/`FromToMotion`/`OpacityFade`/
`MotionRuntime`), `AnimLight` (built only by `LightChannel`) and the bind-census `AnchorKind` enum.
Not an independently owned subsystem; `AnimRuntime` drives all of it. `MotionSet`, `EmitterDirector`,
`SoundChannel`, `LightChannel`, `PoseChannel`, `NameResolver` and `TemplateStage` share the
namespace and ARE owned in their own right, each with its entry below. `SpinMotion.ComposeSpin` is
the one member reached from outside without `AnimRuntime` at all, by `Flight/PropAnimator.cs`. The
motion decode, both contact tiers and the readings they supersede: [../org/objectMotion.md](../org/objectMotion.md).

## src/Mech3/Anim/MotionSet.cs
`AnimRuntime`'s live motions as a module: `Add` (owner stamp, `(Target, Channel)` eviction,
`LaunchCount`), the per-frame `Tick` sweep, `DiscardFor`/`Reset`, and the predicates the rest of the
runtime asks: `OwesBounce` (the retirement hold `AnimRuntime.Retirable` consults), `HasSpinOn` (the
`Loop{-1}` spin re-assert guard) and `LiveFromToMotion` (the still-live transform tween
`PoseChannel` carries into a replacement channel). It never constructs a motion; `PoseChannel`
builds them and hands them over. `Node3D`-typed but never dereferenced, every operation here being
identity comparison, which is why the freed-target rule sits on `Tick`. Read `Anim/PoseChannel.cs`.

## src/Mech3/Anim/EmitterDirector.cs
One runtime's `PUFFER_STATE` emitters as a module: `Assert` (start, revive, re-home), the four stops
(`End`, `EndOn`, `EndFor`, `Discard`), `Prewarm` (built ahead and left unclaimed until the first
assert takes it over), `Reset` (the crash rig's respawn, which keeps every emitter whose host still
exists), the per-frame `Tick` follow and `Census`. `AnimRuntime` keeps only the dispatch case, the
`at_node` sentinel resolution and the `active_state` read. One director per runtime;
`IEmitterFactory` is the build seam (`Puffer`, `TextureArchive` and the parent node sit behind it),
so a suite can install a fake. The selector/disposition split across the stops and the emitter-keying
tradeoff are in this file's own doc comments. Emitter decode: [../org/puffer.md](../org/puffer.md).

## src/Mech3/Anim/SoundChannel.cs
One runtime's `SOUND_NODE`/`SOUND` events as a module: `HandleSoundNode` (declare, place and start
the pooled ambient emitter), `HandleSound` (the one-shot destruction/impact player, positioned by
`OneShotSoundPosition`), the late-failure census, `Reset` (the crash rig's respawn) and `DiscardFor`
(the teardown reach-in, keyed by anchor like lights). `AnimRuntime` keeps the two case labels and
the sound reach-ins inside `OBJECT_ACTIVE_STATE` and `OBJECT_ADD_CHILD`, and keeps `Sounds` and
`SoundHandledElsewhere` as public fields callers configure; the channel reads both through closures
rather than a constructor snapshot. Ambient emitter plumbing is `WorldSounds.cs`; the definition
grammar is docs/formats/sounds.md.

## src/Mech3/Anim/LightChannel.cs
One runtime's `LIGHT_STATE`/`LIGHT_ANIMATION` events as a module: `HandleLightState` (the partial
update that declares a light on first use and never defaults an absent field),
`HandleLightAnimation` (the signed-delta tween over `run_time`), `Tick` (the per-frame advance and
submission to `WorldLights`), `Reset` and `DiscardFor`. Both handlers report through their return
value whether they applied, and the router adds that to its counters. `Lights` and
`LightViewerPositions` stay public fields on `AnimRuntime` for callers to configure, read here
through closures that fold in the single-camera fallback. `AnimLight` is this channel's own value
type, constructed nowhere else. Read `WorldLights.cs` next.

## src/Mech3/Anim/PoseChannel.cs
One runtime's object-pose/visual events as a module: the nine `OBJECT_*` handler bodies, the pose
helpers (`PoseTranslate`/`PoseRotate`/`PoseScale`/`PoseAtNode`, which mission setup's pass 0 also
drives), the subtree opacity and fade machinery, and the landing-resume marks. It carries the
motion-BUILDER role, parsing the motion events into `MotionRuntime`/`FromToMotion`/`SpinMotion`/
`ScriptPlayback`/`OpacityFade` and handing them to `MotionSet`, which stays a pure live-set
container, while the tick spine stays in `AnimRuntime.Advance`. The two `AT_NODE` rotate spellings
and the SI-script duration rules are on their own members. Spellings and census:
docs/formats/anim-definitions/cutscenes.md.

## src/Mech3/Anim/NameResolver.cs
Name to node resolution as one public module, generic over the node type (`NameResolver<TNode>`):
the index, the wildcard `Matcher`, the memoized `FindAll`, the scoped tier chain
(`Resolve`/`ResolveScoped`), the symbol authority (`SymbolClaims`/`NarrowToSymbolRoot`), `Anchors`
(NAME match, symbol narrowing, root lift) and the bind census. Node identity is
constructor-supplied, never the node type's inherited `Equals`, and `DropFreed` retires the rows
naming a freed node. Every tier is filtered by `AdmissibleStaging`, the owner's verdict on one
pooled copy; that filter and its limits are on the members. Decode:
[../org/sequences.md](../org/sequences.md), "The definition owns a private copy of its subtree".

## src/Mech3/Anim/TemplateStage.cs
The effect-template stage as one module (`TemplateStage<TNode>`): pool-slot arithmetic (`SlotOf`,
`TakeNextSlot`, `RootsFor`, `AssignCallerSlot`), template placement (`PlaceAt`/`PlaceOn`, and
`PlaceFollowing` for a copy that must keep riding a moving call site, re-placed each frame by
`FollowSites`), the copy-identity questions (`IsAt`, `RootsOf`, `SharedWithLiveInstance`), the
pooled-copy staging entry `IndexPooledCopy`, and the reveal/retire/sweep ritual. The three template
policy flags (`Pooled`, `Shown`, `Places`) are sealed constructor state. Generic like
`NameResolver<TNode>`, with the runtime-dependent hooks late-bound through `Wire`; the off-engine
charter is `CSVM.Tests/TemplateStageTests.cs`. Read `AnimRuntime.cs` for the checkout reset.

## src/Mech3/SequenceRunner.cs
The engine-free sequence interpreter, extracted from `AnimRuntime` behind the `ISequenceHost` seam.
`SequenceRunner` runs one sequence's event list against two clocks, its own and the owning
`AnimInstance.Clock` a `START_TIME ANIMATION` gates on; its scope is per-event START_TIME gating,
LOOP with authored count 0 as infinite, IF/ELSEIF/ELSE/ENDIF over a branch stack, and
`WAIT_FOR_COMPLETION` as a host-armed predicate. `AnimInstance` holds one runner per `Def.Sequences`
slot and walks them ascending, plus an unslotted list for the death and damage-stage runners, and
carries the CALL_SEQUENCE/STOP_SEQUENCE semantics. Anchors are opaque pass-through, and a test
drives the seam with a recorder host. Decode: docs/org/sequences.md.

## src/Mech3/DestructibleRegistry.cs
Live, mutable per-instance HP for the world's destructibles, any `AnimDefinition` with
`HEALTH > 0`: one `Instance` per `(def, anchor)` pair seeded from the authored `HEALTH`, plus a
coarse healthy/damaged/destroyed `State` and a monotonic `DamageStage`. Built during
`AnimRuntime`'s bootstrap, read by `ANIM_HEALTH` evaluation, escalated by `ApplyDamageStages`,
damaged via `DamageAt`. `Resolve(struck)` maps a raycast-hit node back to its instance by climbing
to the nearest claiming pool. `Instance` also carries what a mission record authors on a pool
(`Team`, `Owner`, `Gasbag`, `Dormant`, `Reseed`), each rule on its own member. Schema:
docs/formats/destructibles.md; the team space is docs/org/targeting.md.

## src/Mech3/WavFile.cs
Pure-C# WAV parser with an MS ADPCM to PCM16 decoder (`DecodeMsAdpcm`), no Godot dependencies:
Godot cannot load the game's WAV format (see docs/formats/sounds.md).

## src/Mech3/WavCues.cs
The RIFF `cue ` chunk of a WAV as times in seconds, read from a sound extraction (zip or directory)
or from bytes already in hand. The briefing narration is the one consumer: a reveal state's
`WaitForMarker n` blocks on cue point `n`, and the durations around it are authored constants, so
these times are the only clock the script does not carry itself. Times come back ascending, which
is what `n` indexes. A file with no cue chunk, a non-WAV or a truncated one yields an empty list
rather than a failure. Kept apart from `SoundArchive.cs`, which decodes to a Godot stream, so a
menu page needing only timings stays engine-free. Decode: docs/formats/briefing.md.

## src/Mech3/SoundArchive.cs
WAV lookup over a soundsh/soundsl extraction (zip or directory), decoded through `WavFile` into
cached `AudioStreamWav`s; `Find(name, looped)` marks the stream as a forward loop when asked, and
caches per `(wav, looped)` because the play site's flag wins over the definition's. A read that
fails returns null rather than throwing, since callers run inside the session step. `Dispose`
releases the OS handle without ending the archive's usable life: the entry map is built once and
kept, so a later read reopens the zip for its one entry. Directory-backed and zip-backed archives
therefore fail differently, which is what `--zip-assets` (docs/cli.md) exists to expose on a
developer tree. Read `WorldSounds.cs` for the prewarm that fills the cache.

## src/Mech3/MusicPlayer.cs
The state-driven score: one non-positional streaming channel beside `WorldSounds`' pooled 3D
emitters, so the menu, the cabin and the mission director all drive the same track. `Enter(state)`
cues the sound-group or definition name the original's data names for that state; `Cue(name)` is
the raw form for a name the data supplies directly. One track at a time, hard cuts, no crossfade;
`NoteCombat` + `Tick` run the battle hold and its fade. The selection rules, the fade rates and the
tracks that ship with no trigger are docs/org/music.md.

## src/Mech3/MissionRadio.cs
The mission radio queue: the third playback channel, beside `MusicPlayer`'s streaming track and
`WorldSounds`' pooled 3D emitters, and the one a mission's objective callouts speak on. `Cue(name)`
takes a queued radio definition or a VO dialogue chain and returns how many lines it will speak, 0
for a name this channel does not own, so a caller can fall through to the channel that does. One
call speaks at a time: a chain runs its lines back to back as one call, a later cue queues behind
rather than cutting in, `Cancel` is `STOP_QUEUED_SOUNDS`, and a call waiting past its definition's
`QUEUE` tolerance is dropped unheard. Streams come from `WorldSounds.StreamFor`. The cue delay is
docs/formats/objectives.md; the definition classes and the tolerance are docs/formats/sounds.md.

## src/Mech3/SoundDefs.cs
sounds.json SETS parser: `snd_*` name to `SoundDef` (wav name, flags, range, volume); the entry
grammar and flag/key meanings are in docs/formats/sounds.md. `LoadGroups` parses the sibling
`SOUND_GROUPS` block into `SoundGroup`s, the weighted random destruction/impact sounds a one-shot
`SOUND` event resolves through (`air_mixed_exp_sg` to `snd_exp_hit*`).

## src/Mech3/CombatVoice.cs
The combat-voice resolver: roster `accentID` (slot 65) to a `voice.zrd` ACCENT row, to a pilot VO
id pool, to clip defs. `PlayableFor(voId, family)` returns the one name to hand
`WorldSounds.PlayOneShot`: the shipped `snd_<FAMILY>-A_id<N>_random` variant group where one is
authored, else the bare def. `SessionPrewarmNames` is the flight session's mission-roster prewarm
set, reached through `WorldSession.Options.VoiceClipNames` with CLI accents joined in. Dispatch
sits above this seam, in `Flight/AiVoiceDispatcher.cs` (the rules) and `Session/AiVoiceRuntime.cs`
(the wiring), never in it. Decode: docs/formats/combat-voice.md.

## src/Mech3/MissionCutscenes.cs
Which of a mission's `mis_anim.zrd` `ANIMATION_DEFINITION_FILE` entries sit under its own
`cutscenes\` directory, and the `ANIMATION_NAME`s those reader files define. That directory is the
authored classifier for mid-mission choreography: the ambient world furniture (the zeppelin wiring,
the walkers, the ladders) sits outside it, and nine story missions ship one. A mission with no such
entry, and one whose reader is missing or unreadable, resolves to nothing, so a caller can treat
"hosts no cutscene" and "is not a story mission" alike. `WorldSession` hands the names to the
cutscene host and to `AnimRuntime.RangeGatedCalls`. Decode:
docs/formats/anim-definitions/cutscenes.md.

## src/Mech3/CampaignSequence.cs
The shared `cm_sequence.zrd` reader: the campaign's 24 flat mission entries in file order, each
one's storage address (world folder, mission folder, `Persist.NNN`/`Mission.NNN` save id) and
whether it flies with a wingman. `PreviousInSameChapter` is the engine's own backwards walk to the
last earlier mission of the same world folder, which is what cross-mission persistence is scoped
by. There is no branch, no predicate and no alternate; the only selection rule is "the next `seq`",
which is why `Session/CampaignProgression.cs` models a single integer position. The three
numberings one mission carries, and the folder-number-is-not-the-act rule, are on their own members.
Decode: docs/formats/campaign-sequence.md.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram`, the world+anim half of a session build:
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights, and stops before
the per-view steps the caller drives. `Options` is the whole seam: the shared `DecodeCache`, the
emitter factory a suite substitutes, the clutter debug switches, the extra sound-group prewarm
names, the callback and trigger hosts, and the cutscene gate that builds `camera1`, the letterbox
bars, the data-named composition frames and the `AircraftStage`, so every other session's node
census is unchanged. `ResolveLibraryRoot` is the lazy pool behind a mission or death call naming a
library root, keyed on the caller's anchor and the authored call event. Read `WorldBuilder.cs`.

## src/Mech3/AircraftStage.cs
The aircraft-archive subtrees a story-mission intro, a hangar or chuteman drop cutscene or a
wing-walk capture animates, staged into a chapter world before the animation bind: the
`piratefighter` prop, the bodiless `player` marker the flown aircraft is posed onto, `chuteman`'s
parachutist, `balmoral`, and the `FigureNodes` and `PropNodes` groups. Each node's shipped active
state and the holder it hangs under are on its own member, since those decide whether it draws in a
mission that never names it. All carry a rebased gamez index (`PointerBaseOf`), which is what lets
a compiled cross-archive symbol table bind them. `StageFlown` puts the flown airframe in the same
table and parks its docking hook. Decode: docs/formats/anim-definitions/cutscenes.md.

## src/Mech3/SessionArchives.cs
`OpenFor(ArchiveIntent, gamezPath, texturesPath, soundsPath, zrdrPath, mute)` opens the five
archives one chapter build needs (gamez, textures, sounds, sound defs, sound groups) and returns
them alongside the `WorldSession.Options.TexturesOutliveBuild`/`SoundsOutliveBuild` pair the intent
implies: `Session` and `Lab` let textures outlive the build, only `Lab` lets sounds, `Suite`
neither. One seam for `GameSession`, the anim lab and the test harness, so none of them hand-sets
those flags. `StartupProfile.Mark`/`Record` calls are unconditional, a no-op with no session under
measurement, which is what lets the test harness drive the same code blind. The optional `decode`
argument makes only the returned `Gamez` a shared read-only instance.

## src/Mech3/DecodeCache.cs
The decoded inputs a world build can reuse, keyed by the absolute paths they were decoded from:
`Gamez(path)` and `Anim(shared, chapterZrdr, missionZrdr, chapterAnim, missionAnim)`. The paths are
the whole key because they already carry data root, chapter and mission; collision, mute, the
emitter factory and the prewarm list all act after the decode, on objects this never holds. It owns
nothing disposable and nothing Godot. **Everything handed back is shared and read-only by
contract**, which is why `AnimProgram`'s three collections are `IReadOnlyList`; `GameZ` has no such
enforcement, so its one in-place writer (`EffectCycles.Apply`) has to stay idempotent, which it is.
Callers are serial by construction. An instance lives as long as its holder.

## src/Mech3/EmptyStage.cs
The `--stage=empty` test stage: a flat collidable 20 km ground plane under a 100 m grid, standing in
for a chapter world so flight and ballistics runs boot in about 2 s with nothing else in the frame.
