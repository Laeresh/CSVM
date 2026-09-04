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
never fatal. `Start` refuses a definition whose REQUIRED node-state prerequisite is unmet
(`NodePrerequisitesMet`, the node's visibility against `AnimDefinition.PrereqNodes`), counted as
`Start(prerequisite unmet)` and otherwise silent like the hull-death gate: CM07's hangar drop calls
both of each camera leg pair and lets `hdrop_direction`'s state pick one. Optional entries and
`MINIMUM_TO_SATISFY` over nodes are parsed, not enforced. Also hosts the destructible-damage entries (`DamageAt`/`CollideDamageAt`/
`ApplyDamageStages`/`RunDeathSequence`/`ResetDestructible`) and the world-effects runtime
(`PlayEffectAt` over a hidden template stage). **A death's callee rides its call site.** The
world runtime's `CallAnimation` arm hands a death's effect call to the world-effects runtime
through `ExternalEffect` with the site's world point, the site node (the callee's `INPUT_NODE`)
and a follow flag that is set whenever the call resolved a site; `PlayEffectAt` then places the
pooled copy with `TemplateStage.PlaceFollowing`. The un-routed half (a library-root callee such
as `dblcannon_flying_parts`, placed by the world runtime itself) takes the same follow while
`_deathCallDepth` is open, through `PlaceFollowing` or `PlaceAt(follow: true)`; the debris pieces
integrate in the root's frame (`MotionRuntime`), so re-placing the root each frame carries their
flight along with the hull. `AT_NODE` and `WITH_NODE` are not told apart here: at the original's
controls a zeppelin gun ring's whole death (its `large_30sec_fire`, its `AT_NODE` fireballs and
its flying parts) moves with the hull as it flies on, and the decode does not contradict that
(`docs/org/sequences.md`, the CALL_ANIMATION section). A world-fixed site reads identically
with or without the follow, and relocating calls outside a death (a damage stage's panel tear on
a flying aircraft) keep their one-time placement. The emitter itself is untouched:
`EmitterDirector.Tick` reads its host, the placed copy's node, which the follow has already moved
that frame. Regression: `ring-death-effects-follow-hull` (a real C1C/M01 piratezep ring killed on
the hull, then the hull moved as `ZeppelinRuntime.Place` moves it: the routed fireball's fed
position and the flying-parts copy's root both move by the ring's displacement, the piece adding
only its own few metres of flight) and `turret-death-fire-follows-hull` (the C1/M04 ring's routed
fire), beside `turret-death-effect-world-anchor`, which covers the un-routed `PUFFER_STATE` on the
world runtime. **A pool-slot checkout returns its copies to their
spawn state** (`ResetCheckedOutCopies`, run by `PlayEffectAt` between `TakeNextSlot`/`PlaceOn` and
`Start`): for every def the played anim reaches through CALL_ANIMATION (`AnimProgram.Subset`, memoized
per anim name), on the anchors sitting in the slot(s) the call took, it takes the three steps
`ResetDestructible`'s `ResetCalled` takes — `Stop` the instance, `RestoreRestPoses`, then re-apply the
RESET_STATE. The original instances a fresh template copy per call, which always starts from its
authored base pose; the pool hands the same node tree out again, still in whatever END pose its last
run left and with its last run's motions still driving it. Both halves matter. A def whose sequences
end INACTIVE (the sonic burst's `ring_up1..4`/`ring_down1` on `sonic_ring1..5`) played once per slot
and drew nothing from the wrap on without the RESET_STATE; a slot recycled while still live
re-launched its debris from a MID-FLIGHT pose without the stop and the pose restore, so the HE
burst's `fly_trail1..5` walked further out on every wrap. ⚠ Stopping is what makes the restore
stick — the incumbent motions would otherwise overwrite the restored pose the same frame.
`RESET_TIME -1` is "never self-reset while playing", not an exemption from this. Scoped to the call's
own closure and its slot, never the whole `pool<N>` container: another effect live on the same slot
number must not be re-posed under its running motions. The authored pose the restore reads is banked
for a whole pooled copy at `IndexPooledCopy` (`PrimeRest`), as-built, rather than left to `RestOf`'s
lazy capture on the first thing that moves a node — on a reused copy that first mover has already
displaced it. Regressions: the `effect-pool-reset` and `effect-pool-spawn-pose` suites.
**`PrewarmEmitters(params Node3D[]
callSiteAnchors)` builds every emitter the bound program's `PUFFER_STATE 1` events can name before
anything plays**, through `EmitterDirector.Prewarm`: a named `at_node` resolves to every node of that
name in the runtime's scope, unfiltered, since a play anchors the def inside its CALLER's pool copy
and the staging-admission rule with no scope rejects exactly those copies; an `INPUT_NODE` host
resolves to every node a `CALL_ANIMATION` in the program targets the def with, the def's own staged
copies, and the anchors handed in. It starts and poses nothing. Both the crash rig and the
world-effects runtime call it at bind (`WorldEffectsFactory.BuildFlightCrashRuntime` and
`BuildWorldEffectsRuntime`), never the ambient world runtime. Regression: the
`emitter-prewarm` suite. `Play`/`PlayWithin`/`StopWithin` start a def's
instances by anim name, the latter two scoped to one subtree (a NAME can repeat across a chapter,
e.g. C1's three `hangerdoors`). `OBJECT_ADD_CHILD`/`OBJECT_DELETE_CHILD` take their node-reparent
form here (`Reparent`, keeping the LOCAL transform) once the sound-emitter form has declined,
which is how a cutscene composes its camera inside the node it frames; the decode is
`docs/formats/anim-definitions/cutscenes.md`. An adopted child that is a staged library copy
(TopLevel from `PlaceNodeAt`) is un-pinned and set back to its authored rest pose in the parent's
frame, so CM07's passenger rides the caboose instead of hanging where the call site was; and an
add-child dispatched from inside a staged copy (`_stagedCopies`, filled by `IndexPooledCopy`) may
lazily build its library-root child the way a ranged or mission call may, since a staged actor's
own choreography (the passenger's wave loop, and the flare it puts in its hand) runs on ordinary
ticks long after the call that staged it. Regression: the `landings-train-pickup-ride` suite. A ranged startanim with an unplaced immediate callee is
deferred until range; range-triggered and explicit mission-trigger calls (`PlayMissionTrigger`: the
`landings.zrd` rows and the objective script's `WAKE_ANIM`) may then lazily build their
library-root callees, and an add-child may do the same for an explicitly named child — except where
the call's own `AT_NODE` site IS the callee's root node, which names the node to run on rather than
asking for a copy. The authored site is passed on to the pool only when the call actually names
one, so a site-less call keeps one copy per anchor and reaches no staged actor at all
(`Mech3/WorldSession.cs` has why CM07's hangar drop needs that). The mission-trigger right is remembered as the started definition's whole call
closure, not as a call-stack depth: a cutscene's later beats run off delayed sequence events seconds
after the trigger's own dispatch has returned (CM02's crew is thrown out fourteen seconds in) and
instance their library roots exactly as the beats inside it do. `MissionTriggerOwner`, called at the
top of the same method, is the cutscene trigger slot; `Session/CutsceneController.cs` has it. `IndexSpawnedVehicle` is the other half of that resolution: it makes one live
aircraft answer for the gamez library-root vehicle node its roster block was spawned from, by name
and compiled index, which is what a cutscene posed `AT_NODE` that vehicle needs
(`Mech3/RosterMarkers.cs` is the caller). It also keeps three things beside that rig which the
resolver's shared index must never hold: the rig's own parts by gamez name, so a definition
ANCHORED on the vehicle reaches the parts a capture animates (`Targets`' second narrow rescue —
the shared airframe spells them `pilot`, `body`, `healthy`, and indexing those globally would let
any definition claim them); the rotation the roster placed it at, which is what an `AT_NODE_XYZ`
rotate reads off an aeroplane (`PlacedRotationOf`, decode in
`docs/formats/anim-definitions/cutscenes.md`); and the engine-only nodes between the rig root and
the airframe it draws, so an `OBJECT_ACTIVE_STATE` addressed to the vehicle reaches the aeroplane
rather than the rig node alone (`SetTargetActive`, which is how a capture holds its subject drawn
against the cutscene's own AI park). The range
sweep (`TickDeferredByRange`) runs only when a player crosses an 8 m check cell, and it measures
each anchor's range origin (`VisualOriginOf`, a mesh-bounds walk) once, carrying it in the anchor's
own frame (`_rangeOriginLocal`) from then on: at cruise the sweep runs every few frames, and
re-walking every deferred anchor's subtree per sweep allocated tens of MB/s of finalizable Godot
wrappers, which is what fed the periodic gen1 collection pause.
Every construction site hands over a sealed `TemplateStage`
(`NewTemplateStage`/`ForEffects`/`ForCrashRig`). Sibling modules, each with its own entry: the
sequence interpreter is `SequenceRunner.cs`, live motions are `Anim/MotionSet.cs`, name resolution
is `Anim/NameResolver.cs` (this class forwards through `Resolve`/`ResolveScoped`/`Anchors`), puffer
emitters are `Anim/EmitterDirector.cs`, ambient/one-shot sound is `Anim/SoundChannel.cs`, point
lights are `Anim/LightChannel.cs`, the object-pose/visual family is `Anim/PoseChannel.cs`, and the
effect-template pool/placement is `Anim/TemplateStage.cs`.
The router keeps the `SOUND_NODE`/`SOUND` case labels and the sound reach-ins inside
`OBJECT_ACTIVE_STATE`/`OBJECT_ADD_CHILD`, delegating every body to `Sound`; `Sounds`/
`SoundHandledElsewhere` stay public fields here, since callers configure them, and `Sound` reads
both live rather than snapshotting them. The router also keeps the `LIGHT_STATE`/`LIGHT_ANIMATION`
case labels, delegating every body to `Light`; `Lights`/`LightViewerPositions` stay public fields
here for the same reason, and `Light` reads both live. The nine `OBJECT_*` pose/visual case labels
delegate to `Pose` the same way, each adding the handler's returned op count to the census counter;
the `_rest` pose table stays here, since the death flow (`RestoreRestPoses`/`ApplyDeathSwap`) reads
it too, and both the family and the motion value types reach it only through `RestOf`.
`CALLBACK` offers each code to `CallbackHost` first (the mission-script host, given the raising
definition's animation name and its root node name, `ANIMATION_ROOT_NAME` before the anchoring
`NAME` — `Session/CutsceneController.cs`), then raises the two vehicle-death
codes through caller-supplied seams (`WreckVelocity`, `StopDamageStages`) and counts every other
one; decodes in `docs/org/vehicleDamage.md` and
`docs/formats/anim-definitions/cutscenes.md`.
`FBFX_COLOR_FROM_TO`/`LIGHT_ANIMATION` report their `run_time` as the
event's duration, spacing a chain instead of firing it in one instant; decode in
`docs/formats/anim-definitions.md`.
`PLAYER_1ST_PERSON` (condition 120) answers off the `FirstPersonView` seam, polled per
evaluation: the session hands over "any human pilot is in Cockpit or Nose"
(`GameSession.AnyPilotFirstPerson` off each rig's `FlightController.FirstPersonView`), and a
runtime with no seam wired — a lab, a test, the bootstrap before any rig exists — reads false,
which is what this condition answered everywhere before the view modes existed. Regression: the
`first-person-condition` suite, over the shipped `bullet1` def.
`SyncDestructiblePool` keeps a destructible's `DestructibleRegistry` instance in step with an
`OBJECT_ACTIVE_STATE` healthy/destroyed role swap dispatched outside `DamageAt`'s own kill — a
start-state script or an ON_STARTUP sequence authoring an object destroyed before the player
arrives. Called from the `ObjectActiveState` dispatch case right after `Pose.HandleActiveState`,
it matches the same role-name convention `AuthorsSwap`/`ApplyDeathSwap` already use and writes
`Health`/`Status` only when they still disagree with the swap direction, so a live kill's own
dispatch (which already set the pool via `DamageAt` before running the death sequence) is a
no-op there. Bootstrap's Pass 1 registers each anchored destructible in `_destructibles` BEFORE
dispatching its `RESET_STATE`, not after, so a def authored to start destroyed (its own
`RESET_STATE` puts the destroyed role ACTIVE) reaches this same sync with a pool already there
to find; no shipped def uses that shape today, but the ordering is the general contract every
other `OBJECT_ACTIVE_STATE` dispatch site already follows. Regression: the
`start-state-swap-pool` suite.
`CarryState` is the other direction, the persist log's: it writes the pool first (`Health`,
`Status`, `DamageStage` from the def's `DAMAGE_SEQUENCE` thresholds) and then, for a carried
kill, `ApplyDeathPose` puts the nodes where the death would leave them without playing it. The
pose is read off the sequences `RunDeathSequence` would play (the def's Initial sequences, its
compiled destruction slot, a chained swap target's sequences): every `OBJECT_ACTIVE_STATE` that
switches a node off, plus the destroyed/`dbase` role nodes switched on; a non-role piece
switched on mid-death is debris that flies and is hidden, so it stays off. A def with no role
swap in those sequences takes the RESET-derived `ApplyDeathSwap`, withheld when the def authors
its own visible death, the same rule the live kill applies. Shipped `DAMAGE_SEQUENCE`s carry only
`CallAnimation` puffer calls (and three `StopAnimation`s), so a carried partial HP lands as the
stage counter alone and no stage burst plays. Regression: the `carried-state-silent` suite.
**`NODE_UNDERCOVER` is a real probe**, not a stub: `EvaluateCondition`'s arm casts a vertical
segment of the condition's own signed length from the named node against `ContactMask`, excluding
the collider bodies under the host the probed node belongs to, since the original clears the probed
node's own collidable bit and one gamez node is a whole subtree of bodies here. The operand needs
decoding before it is a length (`UndercoverReach`); the probe's semantics, the sign convention and
the u32 bit pattern are in `docs/org/sequences.md` and `docs/formats/anim-definitions.md`. No mask
wired answers false, the same structural fallback the contact tiers take, so every lab, golden and
collision-less suite is unaffected. This is what makes a downed zeppelin's `killpzep` breakup play:
its `main_altitude_check` polls 65 m under the hull and opens as the wreck sinks, and each engine's
own break polls 4 m under that engine. Regression: the `zeppelin-breakup` suite.

## src/Mech3/Anim/
`AnimRuntime`'s private nested types promoted to top-level `internal` types in their own
namespace, purely for file size — not an independently-owned subsystem, still driven entirely by
`AnimRuntime`. `IAnimMotion` (`ScriptPlayback`/`SpinMotion`/`FromToMotion`/`OpacityFade`/
`MotionRuntime`), `AnimLight` (owned by `LightChannel` below), and the bind-census `AnchorKind`
enum. `SpinMotion.ComposeSpin` is
the one member reached from outside this namespace without going through `AnimRuntime` at all —
`Flight/PropAnimator.cs` calls it directly so a plane's own props spin through the identical
accumulate-from-rest decode instead of a second hand conversion; it takes a rest `Basis` and a
rate, no `AnimRuntime`/`MotionSet` state, so the reach-in is inert to everything else here.
`MotionSet`, `EmitterDirector`,
`SoundChannel`, `LightChannel`, `PoseChannel`, `NameResolver` and `TemplateStage` share the
namespace but ARE independently owned — their own entries below.
**The original's `OBJECT_MOTION` update is written up in [org/objectMotion.md](org/objectMotion.md)**
— the function map, the flag word, the linear elevation, `delta` as an acceleration, both contact
tiers and how they pick a surface, the landing response, the termination model, and the retired
readings (the spherical elevation, the ÷`run_time` tumble, `DebrisTune`, `no_altitude` as a second
terrain test). Read it before changing a mechanism here; only what this engine adds is below.
`MotionRuntime`'s launch seeds from the node's LIVE pose and a chain of events on one node
continues leg from leg (org/objectMotion.md, "A launch starts from the node's live pose"); what
keeps a repeat from compounding is the reset and checkout bookkeeping, never the launch.
`RangeLaunchDirection` is the launch decode's ONE
expression and `TumbleAxis` the tumble's; `ProjectilePool`'s gun-casing ejection reads the same
`gunshell` event through both (INSTR-3), because two spellings of the maths is how they disagree —
see their doc comments in `Anim/MotionRuntime.cs` for the non-normalisation rule.
Contact is the DEFAULT and comes in the original's two tiers: `TryGroundColumn`, a vertical column
through the body, unless `do_intersections` upgrades it to `TryContact`'s trajectory sweep (166
events install-wide); `no_altitude` vetoes the column only, and `gunshell` alone authors it. That
column reads DOWNWARD from the body first and, when nothing answers, DOWNWARD again from a column's
height above it, because the original's query is a cell lookup at `(x, z)` whose answer does not
depend on the body's height: a body that stepped past the surface is lifted back onto it rather than
drifting to its watchdog. ⚠ Both casts come from above deliberately: `SceneBuilder`'s colliders
honour the polygon's own `SHOW_BACKFACE` and every water polygon in the install clears it, so a ray
sent up from under a surface answers nothing at all. No mask wired
means neither tier, which is the structural fallback every lab and 9 of the 14 goldens take;
`c1-debris-rest` is the one golden that wires a mask and reaches the column tier, a killed
`m_build03` piece resting with its landing's own spark puffer as the pixel-level tell. Both
end on one shared response. `MotionRuntime` separates the duration it REPORTS (`RunTime`) from the
ceiling that ENDS it (the original's watchdog, or `RUN_TIME`); see `RunTime`'s and the watchdog
constants' own doc comments for the split and why it must not collapse.
`FromToMotion`, `NonSingularScale`, and the `AnimRuntime` members these motion types reach into
(`RestOf`, `_rng`, `SetSubtreeOpacity`, `NameOf`, `VisualOriginOf`) carry their own rules and
visibility rationale on their declarations — read those before touching either file.

## src/Mech3/Anim/MotionSet.cs
`AnimRuntime`'s live motions as a module: `Add` (owner stamp + `(Target, Channel)` eviction +
`LaunchCount`), the per-frame `Tick` sweep, `DiscardFor`/`Reset`, and the predicates the rest of
the runtime asks — `OwesBounce` (the retirement hold `AnimRuntime.Retirable` consults),
`HasSpinOn` (the `Loop{-1}` spin re-assert guard), and `LiveFromToMotion` (the target's still-live
transform tween, so `PoseChannel` can carry an about-to-be-evicted channel into its replacement
instead of losing it). Never constructs a motion — `PoseChannel` builds them and hands them over. `Tick` drops a motion whose target node has been freed before touching it: a motion outlives the
node it drives (an airframe swap, a rig torn down), and writing a transform to a disposed object
throws out of the whole runtime advance rather than losing one motion. `Node3D`-typed and otherwise
never dereferenced: every other operation here is identity comparison, so the behaviour is
engine-free even though the type is not — the in-engine `bounce-launch` suite is what an off-engine
fake cannot cover.

## src/Mech3/Anim/EmitterDirector.cs
One runtime's `PUFFER_STATE` emitters as a module: `Assert` (start / revive / re-home), the four
stops (`End`, `EndOn`, `EndFor`, `Discard`), `Reset` (the crash rig's respawn), the per-frame `Tick`
follow, and `Census`. `AnimRuntime` keeps only the dispatch case, the `at_node` sentinel resolution
and the `active_state` read. One director per runtime; `IEmitterFactory` is what builds (`Puffer`,
`TextureArchive` and the parent node sit behind that seam), so a suite can install a fake. The
selector/disposition split across the four stops, the emitter-keying tradeoff and the stop-family
history live in this file's own doc comments, not here. `Prewarm` builds the emitter for a key ahead
of any assert and leaves it unclaimed: the first `Assert` on that key takes it over (ownership, the
start and the `Built` count) exactly as if it had constructed it, the owner-selected stops pass an
unclaimed entry over, and `Prewarmed` counts what was built ahead. `Reset` keeps every emitter whose
host node still exists, returned to that unclaimed state, so a respawned rig's next crash builds
nothing; only an emitter whose host is gone is destroyed.

## src/Mech3/Anim/SoundChannel.cs
One runtime's `SOUND_NODE`/`SOUND` events as a module: `HandleSoundNode` (declare/place/start the
pooled ambient emitter), `HandleSound` (the one-shot destruction/impact player, positioned by
`OneShotSoundPosition`), the late-failure census (`ReportLateSoundFailure`, gated on
`MarkCensusPrinted`), `Reset` (the crash rig's respawn) and `DiscardFor` (the teardown reach-in,
keyed by anchor like lights). `AnimRuntime` keeps the `SOUND_NODE`/`SOUND` case labels and the two
sound reach-ins the router still owns outright — `TrySetActive` for the `OBJECT_ACTIVE_STATE` case
(an ordinary node event whose NAME can turn out to be a sound emitter instead) and
`TryGetChild`/`Attach` for the sound-emitter three-quarters of `OBJECT_ADD_CHILD`. `Sounds` and
`SoundHandledElsewhere` stay public fields on `AnimRuntime`, since callers configure them (and
`SoundHandledElsewhere` differs between the world and effects runtimes sharing one world); the
channel reads both through closures rather than a constructor snapshot, since `Sounds` goes
non-null only once the world build finishes. `OneShotSoundsPlayed` is a one-line forward from
`AnimRuntime` to the channel's own counter.

## src/Mech3/Anim/LightChannel.cs
One runtime's `LIGHT_STATE`/`LIGHT_ANIMATION` events as a module: `HandleLightState` (the partial
update that declares a light on first use and never defaults an absent field), `HandleLightAnimation`
(the signed-delta tween over `run_time`), `Tick` (the per-frame tween advance and submission to
`WorldLights`), `Reset` (the crash rig's respawn) and `DiscardFor` (the teardown reach-in, keyed by
anchor like sound emitters). `HandleLightState`/`HandleLightAnimation` report whether they applied
through their return value rather than reaching for an `_opsApplied`/unhandled-count callback
directly; `AnimRuntime` applies both after the call, matching what the handlers always did inline.
`Lights` and `LightViewerPositions` stay public fields on `AnimRuntime`, since callers configure
them; the channel reads both through closures rather than a constructor snapshot, folding
`LightViewerPositions`' single-camera fallback (`PlayerPos`) into the same closure. `AnimLight`
stays its own value type in the `Anim` namespace, constructed only by this channel.

## src/Mech3/Anim/PoseChannel.cs
One runtime's object-pose/visual events as a module: the nine `OBJECT_*` handler bodies
(`ACTIVE_STATE`'s non-sound remainder, the three `*_STATE` poses, both opacity events and the three
motion events), the pose helpers (`PoseTranslate`/`PoseRotate`/`PoseScale`, which mission setup's
pass 0 also drives), the subtree opacity/fade machinery (the per-root opacity cache, the fade-twin
material tables, `SetSubtreeOpacity`), and the landing-resume marks
(`ConsumeLandingResume`/`MarkLandingResume`). It carries the motion-BUILDER role: it parses the
motion events into `MotionRuntime`/`FromToMotion`/`SpinMotion`/`ScriptPlayback`/`OpacityFade`
instances and hands them to `MotionSet`, which stays a pure live-set container; the tick spine
stays in `AnimRuntime.Advance`. Every handler returns how many ops it applied and the router adds
that to its census counter, the same return-value shape `LightChannel` uses; multi-key unhandled
tallies go through an `Action<string>` count dependency instead, since one return value cannot name
them. A mission-triggered SI script retains its authored duration when its named cross-archive actor
is absent, so the rest of that cutscene cannot collapse to time zero; ordinary ambient misses remain
zero-duration. The `ROOT`/`ALL_NAMES` form (`HandleMotionSiScriptAllNames`, the skeletal person and
ladder scripts) plays the `motions` list `AnimDefinition.Parse` decoded off the raw payload as that
many single-node scripts started together, and reports the longest as the event's run time. The channel's constructor takes `AnimRuntime` itself as one dependency — the motion value
types already declare it as their host argument, and the pose helpers reach the `_rest` table
through the same `RestOf` seam the builders use — plus the `Targets` resolver, the `MotionSet`,
and closures over `Emitters` and the program's `ScriptFor` (both late-bound). `_rest` itself stays
on `AnimRuntime`, read by the death flow; `AnimRuntime` keeps thin internal forwards for
`ConsumeLandingResume`/`MarkLandingResume`/`SetSubtreeOpacity`, whose callers (`MotionRuntime`,
the `ground-contact` suite, `OpacityFade`) name the runtime. No teardown reach-in exists: none of
this family's state is per-instance the way emitters, lights and sounds are.
The `AT_NODE` form of the translate and rotate poses (`PoseAtNode`) takes another node's world
frame with `state` as an offset inside it, rather than an absolute pose; the host is resolved by
name over the whole index because it is a root of its own, not something the event's anchor
contains. It reads the host's frame through `AnimRuntime.WorldTransform`, so a bootstrap-time pose
(the world root not yet parented) still composes correctly instead of reading Godot's identity
fallback, and writes the target's LOCAL transform when the target itself is out of tree.
`HandleRotateState` reads the rotate's host under either spelling, and the two are different rules
rather than two names for one: `AtNodeMatrix` takes the host's composed orientation, `AtNodeXYZ`
the rotation the host was last SCRIPTED to (`PoseAtNode`'s `scripted` arm, off
`AnimRuntime.PlacedRotationOf`), which is why a wing-walk frame posed off an aeroplane stays level
however the aeroplane is banking. Both carry `state` in radians by the time they reach here.
Spellings, addresses and census: docs/formats/anim-definitions/cutscenes.md.

## src/Mech3/Anim/NameResolver.cs
Name→node resolution as one public module, generic over the node type (`NameResolver<TNode>`): the
index, the wildcard `Matcher` (`*` any run, `#` a digit run, case-insensitive, `.flt` suffix match),
the memoized `FindAll`, the scoped tier chain (`Resolve`/`ResolveScoped`), the symbol authority
(`SymbolClaims`/`NarrowToSymbolRoot` — the `air_gen`/`eairg31` cross-bind fix), `Anchors` (NAME
match → symbol narrowing → root lift, policy inputs `NameResolveFallback`/`SuppressRootLift`/
`MaxRootLift`), and the bind census (`OpenCensus`/`CloseCensus`, `ResolutionLines`). Node identity
is constructor-supplied (`IEqualityComparer<TNode>`; the engine keys on `GetInstanceId()`), never
the node type's inherited `Equals`. `FindAll` and the by-index map both skip a node the caller's
liveness test rejects, and the map gives that index up to the next `Add`: an airframe swap frees the
aircraft it staged and re-stages the same cross-archive block. **Skipping is not enough on its own,
because a freed node stays a dictionary KEY.** The ancestry map `IsWithin` walks and the memoized
find cache are keyed by the supplied identity, so the comparer dereferences a freed node for any
query whose key hashes into its bucket, and the throw lands in whatever query collided rather than
at the free. `DropFreed` retires those rows, rebuilding the keyed collections rather than removing
from them (a `Remove` hashes the dead key it is handed, which is the dereference being avoided);
`FreedRows` counts what a stage would otherwise leave behind. The owner decides when:
`AnimRuntime.IndexRebasedStage` sweeps before it grows the table, since it is the one staging entry
that puts a subtree in over one its caller may have freed. A claimed
index the build never created is answered by `SoleStagedCopy`: the one live pooled copy carrying
that gamez index, and only when exactly one does, so a multi-copy effect pool stays anchor-scoped
while a mission's single staged actor (CM07's pickup switch, sensor and passenger) is reachable by
the definitions that toggle it through their symbol table alone (`pickup_timing`). A claim made
from inside a staged library copy (the `privateCopyOf` hook, `AnimRuntime.StagedCopyRootOf`) is
narrowed to that copy: a bound node outside it yields to the copy's own node of that name, and a
name the copy lacks keeps its binding. That is the private-copy rule applied to the symbol path,
and it is needed because a cross-archive symbol table binds by index: CM07's `pickup_flare`
claims the aircraft archive's `cp_lh`, and once `AircraftStage` has parked that figure the
by-index map answers with the parked figure's hand instead of the passenger's, which is where
the definition runs. `AnimRuntime`'s `Resolve`/`ResolveScoped`/`FindAll`/`Anchors`
are one-line forwards; the engine-free instantiation over a plain token type is `CSVM.Tests`' suite.

**Every tier is filtered by `AdmissibleStaging`, and the template pool is why.** The original
resolves a node name inside a subtree the definition was given a private copy of at load
([org/sequences.md](org/sequences.md), "The definition owns a private copy of its subtree"), so an
unrelated instance's copy of a common name (`pilot`, `geometry`, `healthy`) can never answer first.
Our stand-in is the effect-template pool, whose copies hang under the same crash root a definition
anchors on, so the raw subtree walk is not exclusive at all. The `stagingAdmits` hook is the owner's
verdict on one pooled copy (`AnimRuntime.StagingAdmits`): a copy is visible when its root answers to
the definition's own NAME or `ANIMATION_ROOT_NAME`, when the scope this tier searches sits inside it
(a `CALL_ANIMATION` retargeted onto its call site's copy), or when the definition's symbol table
names that root. Everything outside the pool always resolves, and on a non-pooled runtime nothing is
refused, which is what keeps the ambient world boot byte-identical. ⚠ The filter belongs on every
tier: applied to the first alone it only hands the same foreign copy to the next one down.
**The call-site allowance stops at the definition's own copy** (`HasOwnCopyBeside`). A `CALL_ANIMATION`
anchors its callee on the CALLER's node, so the anchor tier searches the caller's copy; where the
callee has a staged copy of its own in that slot, the original would have searched the private copy
it was handed and a name both copies carry belongs to the callee's. Refusing the caller's copy drops
the name to the own-root tier, which is the pair the original reads as `def+0x6c`/`def+0x48`. Without
it a callee's motion drives the caller's node: the sonic burst's `sonic_puff1` translates
`sonic_emit1` 30 m up to draw its vapour column, and the caller anchors its ground ring, both lights
and its second flare on that same name, so all four rode into the air. Regression:
`sonic-ground-ring`.

## src/Mech3/Anim/TemplateStage.cs
The effect-template stage as one module (`TemplateStage<TNode>`): pool-slot arithmetic (`SlotOf`,
`TakeNextSlot`, `RootsFor`, the `AssignCallerSlot` caller-slot claim), template placement
(`PlaceAt`/`PlaceOn`, and `PlaceFollowing`, also reached as `PlaceAt(follow: true)`, for a copy
that must keep riding a moving call site, a death's callee on a carried node:
the root is placed once and then re-placed by `FollowSites`, every frame from `AnimRuntime.Advance`
before the emitters read their hosts, at the site's live pose plus the offset the placement had in
the site's own frame; a fresh `PlaceOn` of that root, a hide through `Reveal`, or a freed site ends
the follow, and `Following` counts what rides), the copy-identity questions (`IsAt`, `RootsOf`, `SharedWithLiveInstance`),
the pooled-copy staging entry (`IndexPooledCopy`), `Recycles`, and the reveal/retire/sweep ritual
(`Reveal`, `RetireWhenIdle`, `Sweep`). The stage's own reset pass (`applyResetStates`, wired from
`AnimRuntime.ApplyResetStatesWithin`) runs once per copy, when it is staged; a copy `TakeNextSlot`
hands out is returned to its spawn state by the runtime on every checkout
(`AnimRuntime.ResetCheckedOutCopies`, its entry above), because the copy is a reused node tree
standing in for the original's fresh instance per call. `IndexPooledCopy` is also where that spawn
pose is banked, while the copy is still as-built. Carries the three template policy flags as sealed
constructor state — `Pooled`, `Shown`, `Places` — get-only, no setter anywhere. Generic like
`NameResolver<TNode>`: engine hooks at construction, the runtime-dependent hooks (`findAll`,
`anchors`, `isLive`, live instances, …) late-bound via `Wire` at the handover, since the factory
that builds the stage exists before any resolver does. Off-engine charter:
`AssignCallerSlot` claims per (template root, call anchor, authored call event) and hands a repeat
site its own copy to start on, because instance identity is (def, anchor) and two live calls on one
anchor need two anchors. Off-engine charter:
`CSVM.Tests/TemplateStageTests.cs`; `effect-template-mesh`/`effects-census`/`damage-template-pool`/
`repeat-call-slots` are the in-engine integration tier.

## src/Mech3/SequenceRunner.cs
The engine-free sequence interpreter, extracted from `AnimRuntime` behind the `ISequenceHost` seam
(four members since `BL-228` added `PendingWait`; the other three are unchanged).
`SequenceRunner` runs one sequence's event list on a clock — **two** clocks, in fact: its own, and
the owning `AnimInstance.Clock` that a `START_TIME ANIMATION` gates against (the original's
`anim+0xb0`, one per definition instance and shared by all its sequences). The two differ for every
sequence a later CALL_SEQUENCE starts, which 191 shipped events read; a null `start` encodes as
`Animation + 0.0` but must stay on the relative path, and `SetDue`'s comment says why.
A runner is done only when its cursor is past the last event AND that event's run time has elapsed
(`_base`), the original's "still running until the run time is up": a sequence ending on an SI
script keeps its instance live for the script's length, which is what a caller's
`WAIT_FOR_COMPLETION` on CM07's `caboosepickup` holds on. Its scope is per-event START_TIME gating, LOOP with
authored-count-0 = infinite, and IF/ELSEIF/ELSE/ENDIF via a `_branchTaken` stack + a deliberately
**non**-nesting-aware `Scan` — the original counts no depth, and 48 shipped `gunhit` sequences
observe the difference; the constraint and its one residual live in `Scan`'s own comment).
**The original's sequence runtime is written up in [org/sequences.md](org/sequences.md)** — the
function map, the three START_TIME origins, the LOOP's pass counter and 60 Hz frame denomination,
and the clock-carry a timed loop needs; the authored side stays in
[formats/anim-definitions.md](formats/anim-definitions.md).
`AnimInstance` holds **one slot per `Def.Sequences` entry and walks them ASCENDING**, mirroring the
original's per-definition sequence array, plus an unslotted list for runners the definition does not
list (the death slot, the damage-stage host), which the original likewise keeps off the array and
steps outside the walk. The walk order is behaviour, not housekeeping: `CALL_SEQUENCE` writes the
callee's own slot, so a call runs in the same tick exactly when the callee is declared AFTER the
caller and waits a tick when it is declared before — 99.5 % of the install's calls point forward.
One slot stepped at most once per pass is also why no same-tick recursion cap is needed.
It carries the CALL_SEQUENCE/STOP_SEQUENCE semantics (decode in `docs/org/sequences.md`;
`AnimRuntime`'s dispatch cases are thin shims over these). **One runner per sequence, keyed on the
`AnimSequence` OBJECT and never on its name** — the original holds a sequence's state inside the
definition's own sequence array (`004eb570`), so `CallSequence` starts a sequence only when nothing
is running it AND its authored activation is ON_CALL; a call into a running or non-ON_CALL sequence
is a silent no-op that still reports *found*, since callers read the return as "did the name
resolve" for the CALL_ANIMATION fallback. Names are not unique (`he_ground_effect` ships two
unnamed sequences), which is why identity is the object. `StopSequence` halts every matching runner
**and does nothing else** (`004eb610` writes the sequence DONE and has no start-if-not-running
path): a stop naming a parked ON_CALL sequence therefore runs no teardown at all, which is what 16
shipped definitions author (`flame_ball_01/02 → stop_p1trail`, every chapter, inside the HE
explosion's chain). The stop is also a DISABLE for the life of the instance: `AnimInstance` keeps
the set of sequences a stop has named, and `CallSequence` refuses a call into one of them, which is
what `004eb570`'s parked-only start does to a sequence `004eb610` wrote DONE. 123 definitions name
one sequence in both a call and a stop, and four reach the refusal: C1's `car_loop1_start`,
`car_go_home_start`, `hauler1_start` and `truck1_start`, whose lap loop calls a dust or exhaust
sequence, stops it later in the lap and calls it again on the next lap, so the called sequence
runs on the first lap only (the `stop-sequence` suite drives `car_loop1_start`). The other 119
(`flame_light_seq` in the destroy defs, `chuteman_drop`/`chuteman_sway`, the `sail_splash*`/
`yacht_splash*` sets, `warhawk`'s `smokepuff1..3`) stop after their last call, so the refusal
never fires for them; `RefusedStoppedCalls` is the counter and the `anim: CALL_SEQUENCE … refused`
log line names any new one. A restart builds a fresh instance and clears the set. Both are
public so `CSVM.Tests` drives them against a fake host; the host is any `ISequenceHost` (the game's
real one is `AnimRuntime`, tests pass a recorder). Anchors are opaque `Node3D?` pass-through — the
interpreter never dereferences them.
The up-counting `_loopPasses` mechanism, the `goto case "Elseif"`, and the 256-fires-per-frame guard
all LOOK refactorable and are all load-bearing (each a shipped, measured bug); the `Loop`/`Advance`
code comments in `SequenceRunner.cs` carry the measured evidence — read them before touching any of
the three. `WAIT_FOR_COMPLETION` (`BL-228`) is the seam's fourth member, `PendingWait`: a
`Func<bool>?` predicate (not a duration — the callee's own length is not knowable at the call) the
host arms during `Dispatch`, and the runner polls each advance until it reads false; the hold gates
only the sequence's next event, never the runner's lifetime — see the code comment where `Advance`
installs it. `OnEventDispatched` being a get-only nullable delegate on the seam is the
same shape: the null-conditional short-circuits the whole `EventDispatch` construction when no
debugger is attached, the zero-cost contract `ISequenceHost.OnEventDispatched`'s own doc comment
states.

## src/Mech3/DestructibleRegistry.cs
Live, mutable per-instance HP for the world's destructibles — any `AnimDefinition` with
`HEALTH > 0`. One `Instance` per `(def, anchor)` pair, seeded from the authored `HEALTH`, plus a
coarse healthy/damaged/destroyed `State` and a monotonic `DamageStage`; built during AnimRuntime's
bootstrap, read by `ANIM_HEALTH` eval, escalated by `ApplyDamageStages`, damaged via `DamageAt`.
`Resolve(struck)` maps a raycast-hit node back to its instance, climbing to the nearest node a pool
claims. A pool claims its own DAMAGE NODE (`Register`'s `damageNode`, which
`AnimRuntime.DamageNodeOf` reads off the def's `ANIMATION_ROOT_NAME` inside that anchor) and its
anchor only as a fallback, which is how two defs sharing one anchor are told apart. That fallback
answers only while the pool's own damage node stands in the world, so a round on the hatch over a
stowed broadside cannon reaches no pool; the rule and the `def+0x6c` decode behind it are in
docs/formats/destructibles.md, "Which node takes the hit" and "Remake node resolution".
Regression: the `campaign-balloon-death` and `zeppelin-cannon-stowed` suites.
Schema: docs/formats/destructibles.md.
`Instance.Reseed(max)` re-seeds a pool from a mission record — the F18 zeppelin zones, where
`zeppelins.json` hp beats the def's own `HEALTH` — and refuses once damaged, so a late wire-up
cannot heal a fight in progress. `Instance.Team`, `Instance.Owner` and `Instance.Dormant` are what a mission
record can put on a pool: an owning side where the data names one (a pool with no team is neutral,
so nobody's target), the name of the
entity the pool is a PART of (a zeppelin's zones carry their hull's name, which is the only thing a
`rating_biases` pattern naming the airship can match), and "registered but not in the world yet",
which `AimCandidateSet.AddStructures` refuses outright and `AnimRuntime.DamageAt` refuses as a hit
that found nothing, read live so the pool takes damage once woken.
Two writers author a team. `ZeppelinRuntime` fans a mission record's own over an airship, and
`Register` reads one off the pool's damage node where `SceneBuilder` stamped it, which is how a
pool standing on a mission-structure node becomes a candidate with a side rather than scenery: in
C1/M05 that puts the Red Cross hospital ship on the player's side and the mission's zeppelin zones
on the enemy's, from the same field (`MissionStructureTeamOf`, `Instance.Gasbag`,
docs/org/targeting.md "What a mission structure's team is"; `turret-structure-targets` suite).

## src/Mech3/WavFile.cs
Pure-C# WAV parser with an MS ADPCM→PCM16 decoder (`DecodeMsAdpcm`), no Godot dependencies —
Godot cannot load the game's WAV format (see `docs/formats/sounds.md`).

## src/Mech3/SoundArchive.cs
WAV lookup over a soundsh/soundsl extraction (zip or dir), decoded through `WavFile` into cached
`AudioStreamWav`s; `Find(name, looped)` marks the stream as a forward loop when asked.

**The lifetime rule.** A session opens this for the world build and closes it at the end of it
(`ArchiveIntent.Session` → `SoundsOutliveBuild = false`), while `FlightAudio`, `ProjectilePool` and
the rest keep the object and keep asking. `Dispose` therefore releases the OS handle without ending
the archive's usable life: a later read reopens the zip, takes its one entry and closes again. The
entry map is built once at construction and kept across `Dispose`, which is what makes that cheap,
and `Find` caches the decoded stream, so each distinct sound pays a reopen at most once.

The two backing shapes fail differently, and that asymmetry is the point. A directory-backed
archive re-reads a path per lookup and cannot be closed under itself; a zip-backed one holds a
handle that can. The build-scoped close rested on the build-time prewarm covering every sound the
session would ever want, and it did not. On a developer tree, where `ExtractAssets.ps1 -Unzip`
leaves unpacked folders that `SessionPaths.PreferUnzipped` prefers, that gap is invisible. On an
exported build, which ships only the `.zip`, every uncovered read threw `ObjectDisposedException`
out of `SessionSimulation.Step` and skipped every later phase, the AI aircraft included. Run with
`--zip-assets` (`SessionPaths.ForceZipped`) to take the export's asset shape on a developer tree.

**The weapons half of the prewarm.** `GameSession.PrewarmWeaponSounds` decodes everything
`WeaponDefs.SoundCues` names, while the archive is still open. Nothing else covers those: they are
bound by `weapons.json` rather than by the anim program, and each is first reached in flight from a
trigger pull or an impact. It walks the whole 48-entry catalogue, not one loadout, because the AI
and the turret gunners fire from it too, and it expands a `SOUND_GROUPS` name (`bullet_hit_sg`) to
its members, since the pick is per shot.

⚠ A cue carries the `LOOPED` flag its PLAY SITE asks for, not the one `sounds.json` holds. `Find`
caches per `(wav, looped)`, and both play sites override the definition: `FlightAudio.StartGunLoop`
forces looped true on a firing loop, `ProjectilePool.PlaySound` forces false on everything else.
That mismatch is why `snd_40cal` stayed cold through a prewarm that had already decoded it, and it
is why `WorldSounds.Prewarm` (whose loader takes `d.Looped`) cannot serve this list.

A read that still fails returns null, never a throw: this runs inside the session step, where an
escaping exception costs every phase after it.

## src/Mech3/MusicPlayer.cs
The state-driven score: one non-positional streaming channel beside `WorldSounds`' pooled 3D
emitters, so the menu, the cabin and the mission director all drive the same track. `Enter(state)`
cues the sound-group or definition name the original's data names for that state; `Cue(name)` is
the raw form for a name the data supplies directly. One track at a time, hard cuts, no crossfade;
`NoteCombat` + `Tick` run the 20-second battle hold and its fade. The selection rules, the fade
rates and the tracks that ship with no trigger are `docs/org/music.md`.

## src/Mech3/MissionRadio.cs
The mission radio queue: the third playback channel, beside `MusicPlayer`'s streaming track and
`WorldSounds`' pooled 3D emitters, and the one a mission's objective callouts speak on. `Cue(name)`
takes a queued radio definition or a VO dialogue chain and returns how many lines it will speak, 0
for a name this channel does not own, so the caller can fall through to the channel that does.
One call speaks at a time: a chain runs its lines back to back as one call, a later cue queues
behind rather than cutting in, `Cancel` is `STOP_QUEUED_SOUNDS`, and a call waiting past its
definition's `QUEUE` tolerance is dropped unheard. Streams come from `WorldSounds.StreamFor`, so
the world's prewarm is what makes a callout survive the sound archive closing. The 1 s cue delay is
`docs/formats/objectives.md`; the definition classes and the tolerance are
`docs/formats/sounds.md`.

## src/Mech3/SoundDefs.cs
sounds.json SETS parser: `snd_*` name → `SoundDef` (wav name, flags, range, volume); the entry
grammar and flag/key meanings are in `docs/formats/sounds.md`. `LoadGroups` parses the sibling
`SOUND_GROUPS` block into `SoundGroup`s — the weighted random destruction/impact sounds a one-shot
`SOUND` event resolves through (`air_mixed_exp_sg` → `snd_exp_hit*`).

## src/Mech3/CombatVoice.cs
The combat-voice resolver (`docs/formats/combat-voice.md`): roster `accentID` (slot 65) →
`voice.zrd` row (the ACCENT table, 35 rows) → pilot VO id pool → clip defs. `PlayableFor(voId,
family)` returns the one name to hand `WorldSounds.PlayOneShot`: the shipped
`snd_<FAMILY>-A_id<N>_random` variant group when authored (466 are), else the bare def (the 12
bearing tokens). `SessionPrewarmNames` is the flight session's mission-roster prewarm set
(`GameSession.BuildWorldStage` → `WorldSession.Options.VoiceClipNames`, CLI accents joined via
`extraAccents`). E16's dispatch sits above this seam: `Flight/AiVoiceDispatcher.cs` (rules) +
`Session/AiVoiceRuntime.cs` (wiring), never in it.

## src/Mech3/WorldSession.cs
Builds one chapter world and binds its `AnimProgram` — the world+anim half of a session build;
`Build` returns Root, Runtime, Program, Builder, Clutter, CloudDeck and Lights. `Options.Decode`
(null by default) is where the `AnimProgram` and the aircraft archive come from when the caller
holds a `DecodeCache`; both are then shared and read-only.
`Options.EmitterFactory` (null → the real `Anim.PufferEmitterFactory` over this build's texture
archive and `EffectsParent`) is read once, here, and never reassigned after `Build` returns — a
caller supplies its own to observe emitter lifetime with no GPU (`CSVM.Testing.CountingEmitterFactory`
is the one caller, through `TestContext.EmitterFactory`); a post-build swap would miss the bootstrap,
where most `PUFFER_STATE`s fire. Two debug options ride the clutter step: `Options.NoClutter`
(`--no-clutter`) skips the clutter build outright, leaving `Clutter` null exactly as a chapter with
no templates does, and `Options.DebugClutterFlag` (`--debug-clutterflag`) hands the flag view to
`WorldBuilder`, then stamps every clutter MultiMesh with a full-strength `SceneBuilder.ClutterColor`
tint and prints the flagged/clear polygon census. The blue is stamped per instance rather than per
material because both clutter paths share the placed world's materials — colouring those would
repaint the ground with them. `Options.ExtraPrewarmNames` (D33) prewarms sound-group names the
`AnimProgram` never sees on its own (`ObjectiveScript.SoundGroupNames()` is the only caller today)
before the build's sound archive closes; without it a campaign mission's `WAKEUP_SOUND_GROUP`/
`COMPLETED_SOUND_GROUP` cue decodes to nothing the moment the archive that could decode it is gone.
`Options.CallbackHost` is installed on the runtime BEFORE the bind, since a bootstrapped intro
raises its codes the instant it starts, and `Options.CutsceneRoots` builds the two roots the
`world1` walk never reaches (`camera1`, and the `letterbox` bars, switched off) — only for a
mission whose start-anims reach one of `CutsceneController.IntroAnims` through their
`CALL_ANIMATION` closure (`BootstrapsCutscene`; C3/M03's is called, not listed), that arms an
approach trigger, or whose own mission list names a cutscene definition (`MissionCutsceneAnims`,
CM07's hangar drop), so every other session's node census is exactly what it was. The synthetic `camera1`
carries the gamez name and INDEX metadata a scene-built node would, because every compiled
cutscene binds it through its symbol table and an unbuilt claim makes the runtime drop the event.
`BuildCompositionFrames` stands up the same-shaped third case beside them, data-driven off the bound
program: a bodiless, childless gamez library root the program names as an `OBJECT_ADD_CHILD` parent
is the frame a cutscene composes its shot in (two in this install, CM02's `wingwalk_parent` and
CM07's `carney_pickup_parent`). It is built `TopLevel` — see docs/org/objectMotion.md's re-home rule.
`Options.PlanesGamezPath` feeds `AircraftStage` beside those roots, under the same gate: every
mission that plays a cutscene, because a mid-mission definition poses the flown aeroplane on the
same `player` marker an intro does and with no stage the pilot is held undrawn for the whole
sequence. The archive is opened nowhere else in this build, so no other session pays for it.
`Options.TriggerOwner` is installed on the runtime beside the callback host, and the opening
cutscene's own name is handed to it directly before the bind, since that definition starts inside
the start-list walk rather than through a trigger call (`BootstrapCutsceneOf`).
`ResolveLibraryRoot` is the lazy pool behind a mission or death call that names a library root:
copies are keyed on the caller's anchor AND on the authored call EVENT, so one definition calling
the same actor several times from one anchor gets a copy each, which is what instance identity
being `(def, anchor)` requires. Membership is the gamez's own parentless-root rule plus the one
staged actor the chapter gamez has no record of at all, `AircraftStage`'s `chuteman`: the staged
subtree is the first copy and further copies are duplicates of it, since the aircraft archive is
closed by then. CM02's crew bailing out is the whole of that case, three calls one second apart at
the same authored offset off the wing-walk frame; sizes are in `data/effect_pools.json`.
That staged actor is served to a call that NAMES a site and to no other. A copy is relocated onto
its call's site, and the figure's own script poses its children in world coordinates, so a call
naming no site (CM07's hangar drop calls `hdchute1` with no `AT_NODE` at all) has to keep driving
the actor where the stage put it: pooled there, the parachutist descends kilometres off the hangar
and the twin leg's call takes a second copy nothing animates. A gamez library root keeps the older
rule, its placement being the call anchor either way.

## src/Mech3/AircraftStage.cs
The aircraft-archive subtrees a story-mission intro, a chuteman-carrying drop cutscene or a
wing-walk capture animates, staged into a chapter world before the animation bind: `piratefighter`
built from the shared aircraft archive as a prop with no pilot, drawn in the archive's own shipped
state (ACTIVE) because no definition switches it on: `generic_intro`'s `gi_pfighter1` re-asserts
that state and parents it under the airship, while C1/M04's `pfighter11`..`pfighter13` only fly it
on SI scripts, so a prop built switched off leaves that intro's wingman out of the launch and the
dive; a bodiless `player` marker the flown aircraft is posed onto; `chuteman`'s parachutist
subtree (`chutemanparent` → `pilot`/`stamp`), switched off (the shared `chuteman.zrd` RESET_STATE)
until a mid-mission drop's own definition (e.g. C3/M01's `tdchute`) reparents and activates it;
`balmoral`, own `Visible` matching its archive ACTIVE state but staged under a switched-off
holder like `FigureNodes` rather than at the world root the way `piratefighter` is, since C2/M05's
capture drop is the only definition that ever names it and nothing else ever reparents it out from
under that holder: built at the world root directly it drew, idle, at the archive's build origin
in every OTHER mission that stages an aircraft, and moved a golden hash before this was caught;
`FigureNodes` (`rope_ladder`, `pickup_cpilot`) under the same kind of switched-off holder rather
than switched off themselves, because nothing ever activates the wing-walking pilot: it is the
capture's own `OBJECT_ADD_CHILD` into the shot that draws him, which is what the original gets from
a library root its `world1` walk never reaches, and `PropNodes` (`anim_bloodhawk`, the Bloodhawk on the
hangar floor while the pilot parachutes in; `bloodhawk_gear`, the undercarriage the flown aeroplane
wears on the lift), built switched off the way `chuteman` is because the hangar drop's own legs
add and activate them (`Props`). All carry a rebased gamez index (`PointerBaseOf`: the chapter's node count rounded up to the
next multiple of 2500), which is what makes a compiled definition's cross-archive symbol table
bind them instead of claiming a name with no node. Built for every mission that plays a cutscene
(`WorldSession`'s intro, approach-trigger or mission-list gate), so every other session's node
census is exactly what it was. `StageFlown` puts the FLOWN aircraft's own airframe subtree in the runtime's node table under the same rebase, run when the rigs are built and again after an airframe swap: that is what makes a hookup definition's per-airframe branches decidable, since each tests one `player_<airframe>` node's active bit and then poses that airframe's own hook, wing fold and mount offset. The airframe it replaces does not leave that table on its own: `IndexRebasedStage` retires every row naming a freed node first, because the rows survive the free and the resolver's own identity comparer dereferences one (`NameResolver.DropFreed`, its entry above). Driving six airframes in one process left 562 stale rows by the last of them, and threw an `ObjectDisposedException` on some runs and not others. That rebased index runs no general RESET_STATE pass, because a chapter definition anchoring on a generic airframe node name must not re-pose a live aeroplane, so `StageFlown` follows it with `AnimRuntime.ParkDockingHook`: the RESET_STATE of every definition anchored on a `*_hook` group inside that model, then every node that group's own `<x>_hook_extend` moves seeded from that definition's own FROM pose, which wins wherever a RESET_STATE omits a node or parks the wrong axis (five of eleven airframes do one or the other). The archive's inactive bit parks the group and nothing else, so without the seed an unparked node is drawn at its archive pose for the second before its own motion starts and then snaps, which reads as a second hook swing; a rotate-only and a scale-only FROM_TO on the same node in the same tick is `PoseChannel`/`FromToMotion`'s own case, carried forward rather than evicted unticked. The pose half is
`Session/CutsceneController.cs`; the decode is
`docs/formats/anim-definitions/cutscenes.md`.

## src/Mech3/SessionArchives.cs
`OpenFor(ArchiveIntent, gamezPath, texturesPath, soundsPath, zrdrPath, mute)` opens the five
archives one chapter build needs (gamez, textures, sounds, sound defs, sound groups) and returns
them alongside the `WorldSession.Options.TexturesOutliveBuild`/`SoundsOutliveBuild` pair
`ArchiveIntent` implies — `Session`/`Lab` (textures outlive; sounds only in `Lab`) or `Suite`
(neither). One seam replaces the two near-identical open sequences `GameSession.LoadArchives` and
`TestHarness.BuildWorld` used to hand-write (`BL-241`'s own fix note: the harness forgot
`TexturesOutliveBuild`). `StartupProfile.Mark`/`Record` calls are unconditional here, same as
`WorldSession.Build`'s own phases — a no-op with no session under measurement, which is what lets
the test harness drive the same code blind. The optional `decode` argument makes only the returned
`Gamez` a shared read-only instance (`DecodeCache`); the other four are always this call's own.

## src/Mech3/DecodeCache.cs
The decoded inputs a world build can reuse, keyed by the absolute paths they were decoded from:
`Gamez(path)` and `Anim(shared, chapterZrdr, missionZrdr, chapterAnim, missionAnim)`. The paths are
the whole key because they already carry data root, chapter and mission; collision, mute, the
emitter factory and the prewarm list all act after the decode, on objects this never holds. It
owns nothing disposable and nothing Godot — the texture and sound archives keep their
`ArchiveIntent` lifetimes, and scene nodes, runtimes and worlds are never stored.
**Everything handed back is shared and read-only by contract**, which is why `AnimProgram`'s three
collections are `IReadOnlyList`; `GameZ` has no such enforcement, so its one in-place writer
(`EffectCycles.Apply`) has to stay idempotent over a constant input, which it is. Callers are
serial by construction (`AnimArchive`'s SI-script pool is an unsynchronised lazy dictionary). An
instance lives as long as its holder: the harness keeps one per run, a game session keeps none.

## src/Mech3/EmptyStage.cs
The `--stage=empty` test stage: a flat collidable 20 km ground plane under a 100 m grid, standing in
for a chapter world so flight/ballistics runs boot in ~2 s with nothing else in the frame.

