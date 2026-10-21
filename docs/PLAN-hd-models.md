# HD models: glTF model packs and plane rigs

**ACTIVE PLAN** (written 2026-10-21). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

Higher-detail replacement models for the original's meshes, read from a model pack folder the way
`Extraction/HdTextures.cs` reads texture packs, with glTF as the one accepted format. A replacement
swaps the mesh a gamez node draws and leaves the node itself (transform, `NameMeta`/`IndexMeta`,
`ACTIVE`, zone layer, draw-order bias) as the original built it, so animations, control surfaces,
props, damage toggles and weapon mounts keep binding unchanged. Beside it, a per-plane `rig.json`
moves the plane's marker nodes (firepoints, pylons, `cockpit_camera`, exhausts, `target`) and
overrides the prop, control-surface and wing-flare classification, so a model with a different
shape can still carry its guns, ordnance and moving parts in the right places.

Out of scope: terrain (`TerrainCarve` cuts craters into its `ArrayMesh`, and clutter stamping reads
the gamez polygons), the cockpit interior including the VR interior, replacement collision, glTF
animation and skinning, billboards and facades, the ocean, the skydome and the cloud deck, and new
airframes (a new airframe needs flight-model data, not a model). No `backlog` issue feeds this plan;
its items come from this session's analysis of the code and are not re-verified against the tracker.

**Where each item runs.** Every item is tagged:

- **[cloud]**: needs no game data. The code and its proof are engine-free unit tests or a suite over
  `--synthetic-data` with a generated `.glb`, so it can be done in a cloud session.
  ⚠ The cloud container this plan was written in has neither `dotnet` nor Godot nor `pwsh`;
  a cloud session either gets an environment that installs them, or its item lands as code and is
  verified by the next local `.\RunTests.ps1`. <TODO: decide which, and record the environment setup>
- **[local]**: needs the player's install and `extracted/` (real planes, real chapter worlds, the
  8-chapter regression, a look at the screen), so it runs on the development machine.

## Milestone goal

- A model pack folder (`HDModels` by default, `models.hd` / `--hd-models=`, off with
  `--no-hd-models`) whose `.glb` (zips and folders) and `.gltf` (loose folders) files replace
  gamez meshes by key, with the same later-wins, top-folder-dropped rules as texture packs.
- World props, destructibles in all their damage states, rockets in flight and on pylons, the gun
  casing and the water-splash impact models, and plane airframes can all be replaced.
- A replacement wears the remake's own shaders (fog, world light, fades, Classic/Enhanced twins),
  takes HD textures and, when its material names an original texture, the plane livery.
- A `rig.json` per airframe moves markers and overrides props, surfaces and flares, and is drawn by
  the existing marker overlay.
- A round trip works: export with F10 or NodeLab, edit in Blender, drop into the pack, and the
  unedited file renders as the original did.

**A replacement changes what is drawn, never what the game is.** Collision, weapon caliber and ammo,
pylon count and side, and flight data stay the original's, so a pack cannot change gameplay beyond
the marker positions the rig moves, and those are handled as gameplay data in network play (C26).

## Decisions (2026-10-21)

| # | Question | Decision |
|---|---|---|
| 1 | Model format | **glTF only for the first version**: Godot loads it at runtime (`GltfDocument`), and the repo already writes it (`Tooling/GltfExporter.cs`). `.glb` inside zips, `.gltf` only in loose folders, since external buffers in a zip need path resolution. |
| 2 | Pack folder | **A separate model folder like `HD`, sharing one pack index with `HdTextures`.** Whether models and textures may also share one folder is left open (A2 TODO). |
| 3 | Replacement granularity | **Replace a node's mesh, not the node or subtree.** Every binding (animation, hinge, prop, damage, pylon) rides the node, which stays the original's. |
| 4 | Collision | **Keep the original's colliders.** `CollidersForMesh` builds them from the gamez polygons by surface class, soil and sidedness, which the weapon ray depends on. |
| 5 | Materials | **A glTF material named after an original texture goes through the existing material path**; any other name loads its texture from the pack but still wears the `csky_*` shader. Never `StandardMaterial3D` in play. |
| 6 | What the rig may change | **The rig says where, never what.** It moves `pylon1..8` and named markers and overrides props, surfaces and flares; it cannot add a ninth pylon, flip a pylon's side, or change guns or ammo (those stay in `stock_loadouts.json` and the save). |
| 7 | Rig without a model | **Allowed**: a `rig.json` may correct an original airframe with no replacement mesh. |
| 8 | Rig validation | **Loud**, like `Loadout`'s missing-marker exception: an unknown node, a non-finite value or a wrong-side pylon is an error naming the file and entry. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, pack and rig readers (all [cloud])

1. ☑ [cloud] Shared pack index out of `HdTextures`
2. ☑ [cloud] `HdModels` lookup, keys and launch settings
3. ☑ [cloud] `PlaneRig` reader and validation

### Wave B, engine hooks (code [cloud], look [local])

11. ☑ [cloud] glTF mesh import with winding reversal and a shared cache
12. ☑ [cloud] Mesh swap in `SceneBuilder.FillSubtree` and `SharedMesh`
13. ☑ [cloud] code, [local] look: material mapping onto the remake's shaders
14. ☑ [cloud] Apply `rig.json` to the built plane
15. ☑ [cloud] code, [local] check: exporter keeps gamez names; `--dump-markers` writes a rig template

### Wave C, content paths (all [local])

21. ☐ [local] Round-trip proof on a real plane and a real world prop
22. ☐ [local] Weapons: rockets, pylon ordnance, gun casing, splash models
23. ☐ [local] World props and destructibles in every damage state
24. ☐ [local] Plane airframes without livery
25. ☐ [local] Rig on a real airframe
26. ☐ [cloud] code, [local] two-instance check: pack and rig agreement in network play

### Wave D, livery

31. ☐ [local] Liveries on replacement planes

## Dependency and parallelism notes

A1 blocks A2. A2 and A3 are independent and can run in parallel cloud sessions (A2 owns
`Extraction/`, A3 owns a new `Mech3/PlaneRig.cs`). B11 needs A2; B12 needs B11; B13 needs B12.
B14 needs A3 and is independent of B11 to B13, so it can run beside them. B15 is independent of
everything and can start any time. Wave C needs B12 and B13 (C25 needs B14 instead); C22 to C24
can run in any order, but C23 and C24 both lean on B13's material path, so land B13's fixes before
splitting them. C26 needs B14. D31 needs C24.

File contention: B12, B13 and C23 all edit `Mech3/SceneBuilder.cs`; never run them in parallel
worktrees. B14 and C24 both edit `Mech3/PlaneBuilder.cs`, and B14 also touches `PropParts.cs` and
`ControlSurfaces.cs`.

---

# Wave A, pack and rig readers

## A1 ☑ [cloud] Shared pack index out of `HdTextures`

**Landed.** `CSVM/src/Extraction/PackIndex.cs` (internal) with `CSVM.Tests/PackIndexTests.cs`.
`Open` takes the accepted extensions and a key function that names or refuses each file; the index
owns the open zips, the locked read and the unreadable-zip warning. `HdTextures` keeps its public
API and log line. Files within one pack now also load in name order (case-insensitive, then exact),
so two files sharing a key resolve by name: `foo.gltf` beats `foo.glb`, and in a texture pack
`x.png` beats `x.jpg`, where that order was undefined before.

**Verified.** In a cloud container, on the tree carrying A1, A2 and A3: `dotnet build
CSVM/CSVM.sln` with 0 warnings and the full unit run (6351 passed, 0 failed, 314 skipped) green.
The `.ps1` gates and an in-game launch were not run. <TODO: run the content gate and one launch
with a pack folder locally.>

**Original approach (kept for reference).**


**Goal.** One engine-free index enumerates a pack folder's folders and zips (name order, later wins,
top folder dropped, keys without case or extension) for any set of accepted extensions; `HdTextures`
uses it with unchanged behaviour.

**Evidence (confidence: traced).** The discovery, key and zip-locking rules are all in
`CSVM/src/Extraction/HdTextures.cs` (`Open`, `KeyOf`, `DropTop`, `Add`'s `.png/.jpg/.jpeg` filter,
the locked `Read`). Coverage is `CSVM.Tests/HdTexturesTests.cs`.

**Approach.** Extract the enumeration, keying and locked read into a pack-index type parameterized
by accepted extensions; `HdTextures` keeps `ForWorld`/`ForExtracted` over it. Pure refactor: no
log, setting or behaviour change.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** `HdTexturesTests` pass unchanged; <TODO: name a new test of the index alone, e.g. a
`.glb` filter over a temp folder and zip>.

**⚠ Traps.** `KeyOf` drops the extension so `.png` matches `.JPG`; for models that same rule would
let `foo.gltf` and `foo.glb` collide, so the later-wins rule must decide, not enumeration luck.

## A2 ☑ [cloud] `HdModels` lookup, keys and launch settings

**Landed.** `CSVM/src/Extraction/HdModels.cs` with `CSVM.Tests/HdModelsTests.cs`;
`HdModels.For(node, chapter, airframe)` returns `HdModel(Bytes, IsGlb, GltfPath)`. Precedence:
airframe key, then chapter key, then bare name. Names are lowercased and a trailing `.flt` is
dropped from node and file name alike. A `.gltf` in a zip is refused with a warning and displaces
nothing. Flags follow `--hd=`: default `HDModels`, config.json `models.hd`, `--hd-models=`, no pack
under `--det` unless the flag names one; `--no-hd-models` beats any folder in either argument
order. Wired in `Launcher` after the texture packs; nothing reads it yet (B12). Decision 2: models
and textures stay in separate folders.

**Verified.** In a cloud container, on the tree carrying A1, A2 and A3: `dotnet build
CSVM/CSVM.sln` with 0 warnings and the full unit run (6351 passed, 0 failed, 314 skipped) green.
The `.ps1` gates and an in-game launch were not run. <TODO: run the content gate and one launch
with a pack folder locally.>

**Original approach (kept for reference).**


**Goal.** `HdModels.Current` answers "is there a replacement for this node" by chapter, by airframe
and by bare name, from `HDModels` or `--hd-models=`, off under `--no-hd-models`, and logs
`HD models: N replacements`.

**Evidence (confidence: lead-only).** Key scheme from this session's discussion:
`C3/<node>.glb` for one chapter, `planes/<airframe>/<node>.glb` (or one `.glb` per airframe holding
a mesh per node, matched by name) for one aircraft, bare `<node>.glb` everywhere. The bare name is
required because rockets, `gunshell` and the splash models sit in each chapter's own gamez
(`Projectile.cs:1232`, `:3094`, `:2066`). Gamez names repeat and carry `.flt` inconsistently
(`SceneBuilder.cs:1163` comment), so keys use the original name, never Godot's `Name`.

**Approach.** An engine-free `Extraction/HdModels.cs` on A1's index, returning the bytes plus
whether it is `.glb` or `.gltf` (and the base path for a loose `.gltf`). Settings plumbing follows
`--hd=` / `textures.hd` through `Spec/SessionSpec.cs` and `Launch/Launcher.cs`; `docs/cli.md` gets
the flags.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: unit tests for each key form, `.flt` normalisation, a chapter key beating a bare
name, and a `.gltf` inside a zip being refused>.

**⚠ Traps.** <TODO: decide whether one folder may carry both textures and models; Decision 2 leaves
it open.> How `.flt` is normalised in a key is not settled.

## A3 ☑ [cloud] `PlaneRig` reader and validation

**Landed.** `CSVM/src/Mech3/PlaneRig.cs` (`Load`, `Parse`) with `CSVM.Tests/PlaneRigTests.cs`.
Every section is optional and absent reads as null or empty. Unknown keys, non-finite numbers (after
narrowing to float), zero axes, duplicate names, `pylonN` outside 1..8 and a pylon on the wrong side
throw `InvalidDataException` naming the source and entry. A pylon at x == 0 is rejected, since the
centreline belongs to neither wing. Rudder `side` is optional, because `ControlSurfaces` has one
sideless rudder kind.

**Verified.** In a cloud container: `dotnet build CSVM/CSVM.sln` with 0 warnings, 28 `PlaneRig`
tests and the full unit run (6342 passed, 0 failed, 314 skipped) green. The `.ps1` gates were
checked by hand only. <TODO: run the content gate locally.>

**Owed.** The schema has no permanent home a pack author can read; the type's doc comment lists
the sections only. <TODO: decide where the pack-author documentation lives.>

**Original approach (kept for reference).**


**Goal.** A `rig.json` beside an airframe's models parses into markers, props, surfaces and flares,
and every rule of Decisions 6 and 8 that needs no built tree is enforced at load.

**Evidence (confidence: traced for the constraints, lead-only for the schema).** Pylons: at most 8,
odd to port and even to starboard, `|x|` falling as the number rises (`Loadout.cs:285` to `:296`,
`docs/formats/markers.md`). Marker frame: metres, nose -Z, right +X (`markers.md`). Prop defaults
are the constants at `PropParts.cs:19` to `:22`. Proposed schema:

```json
{
  "markers": { "firepoint1": [-0.42, 0.18, -2.10], "pylon1": [-3.20, -0.35, 0.40] },
  "props": [ { "node": "prop1", "axis": [0, 0, 1], "deg_per_sec": -220 } ],
  "surfaces": [ { "node": "l_aileron1", "kind": "aileron", "side": "left",
                  "hinge": { "origin": [-2.8, 0.0, 0.6], "axis": [1, 0, 0.12] } } ],
  "flares": { "left": "wing_flare1", "right": "wing_flare2" }
}
```

**Approach.** Engine-free `Mech3/PlaneRig.cs`, every section optional, a missing entry meaning the
original's value. Reject `pylon9+`, a pylon whose x sign disagrees with its parity, non-finite
numbers, a zero axis and unknown section keys. Name checks against the plane's nodes wait for B14.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: unit tests for each rejection and for a partial file>.

**⚠ Traps.** The rig must not grow gun groups, calibers or pylon counts (Decision 6); those live in
`CSVM/data/stock_loadouts.json` and the saved game, and the hangar's wing split reads them.

# Wave B, engine hooks

## B11 ☑ [cloud] glTF mesh import with winding reversal and a shared cache

**Landed.** `CSVM/src/Mech3/HdModelMeshes.cs`: `HdModelMeshes.Current.For(node, chapter, airframe)`
returns a shared `ArrayMesh` or null, cached per `HdModels` pack key under a lock (a failed import is
cached as null and logs one warning). It reads the parsed `GltfState` from `AppendFromBuffer` and
generates no scene, so names stay the file's own (`GltfNode.OriginalName`). Node rule: the first
glTF node whose name matches through `HdModels.NodeKey`, its own mesh, else its one mesh-bearing
child (where the exporter puts it), else the file's only mesh. Each triangle surface's indices are
reversed (an unindexed surface gets identity indices first); the mesh is rebuilt from
`ImporterMesh.GetSurfaceArrays`, because `GetMesh()` compresses positions by up to 1/65536 of the
extent. Materials are carried across untouched for B13. `HdModels` gains `KeyFor` and a public
`Read`. Suite `hd-model-mesh-import` (CI tier) round-trips a `.glb` and a loose `.gltf` exactly.

**Verified.** In a cloud container on the merged tree: build, format, StyleCop and the five gate
scripts clean; 6355 unit tests pass; the CI tier 193 passed, 0 failed.

**Original approach (kept for reference).**


**Goal.** A pack file's mesh for a given node name comes back as an `ArrayMesh` in the original's
winding, built once per key and shared by every instance.

**Evidence (confidence: traced).** The data's visible side is Godot's back face, and the exporter
reverses every surface for that reason (`docs/formats/gotchas.md`, "A glTF export has to reverse
the winding"; `docs/architecture/Tooling.md`, `GltfExporter`). Axes need no conversion: Y-up, nose
-Z, metres (`gotchas.md`). `SceneBuilder` caches meshes per mesh index (`SceneBuilder.cs:1420`).

**Approach.** `GltfDocument.AppendFromBuffer` + `GenerateScene`, find the mesh node by original name,
reverse each surface's indices, keep the result in a cache keyed by pack key. Free the generated
scene after taking its meshes.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: a suite that writes a one-quad `.glb` with `GltfExporter`, re-imports it, and
asserts the winding and vertex positions match the source mesh>.

**⚠ Traps.** Do not fix an inverted import by turning culling off: two-sided materials hide it but
draw interiors over exteriors and make back-to-back pairs z-fight (`gotchas.md`). Read the
`Utils/SceneCopy` entry before duplicating any generated node; `Duplicate()` crashes under the
separate render thread.

## B12 ☑ [cloud] Mesh swap in `SceneBuilder.FillSubtree` and `SharedMesh`

**Landed.** `SceneBuilder.FillSubtree` asks `Replacement(node, meshIndex)` before `GetMesh`, and
`SharedMesh` does when given the node; only the drawn mesh changes, and the colliders keep reading
the gamez model. The key context is two fields set before building, like `MissionSlot`:
`HdChapter` (`WorldSession` sets the chapter folder, so flyout bodies, pylon ordnance, the casing and
splash models built on the world's builder get it) and `HdAirframe` (`PlaneBuilder.Build` and
`BuildDestroyed` set the model node). A builder with neither replaces nothing (cockpit interior,
cutscene props, test builders). Never replaced: billboards, glow sprites, single-axis facades, a node
whose original draws nothing, the horizon dome, cloud and sky meshes (`skywal*` walls excepted),
open-sea meshes, and terrain: `CAN_MODIFY` nodes, all-water meshes, cell-sized ground tiles
(`MapEdgeExtender.ClassifyGroundMesh`) and border strips (`IsCompletionStrip`). With no pack the cost
is one `HdModels.Current.Count` check per meshed node. Clutter solids and their extra parts replace
through `SharedMesh`. Suite `hd-model-swap` (CI tier), shown to fail with the hook or the exclusions
removed. Replacement surfaces still wear the imported `StandardMaterial3D` until B13.

**Verified.** In a cloud container: build, format, StyleCop and the five gate scripts clean; 6357
unit tests pass; the CI tier 195 passed, 0 failed. <TODO: locally, the full `RunTests.ps1` with
goldens (the no-pack path should leave every golden unchanged) and the terrain rule against real
chapters; clutter replacement has no suite, since the synthetic data has no clutter templates.>

**Original approach (kept for reference).**


**Goal.** Any node `SceneBuilder` builds draws its replacement when the pack has one, which by
itself covers world props, planes, rockets, pylon ordnance, the gun casing and the splash models.

**Evidence (confidence: traced).** `BuildSubtree` (`SceneBuilder.cs:1131`) is the one builder for
world, plane and weapon subtrees; `FillSubtree` creates the `"mesh"` instance from
`GetMesh(node.MeshIndex)` at `SceneBuilder.cs:1204`. Weapons reach it through
`Projectile.cs:1232` (flyout and, through `PylonOrdnance`, pylon bodies), `:2066` (impact models)
and `:3094` (casing). Clutter solids go through `SharedMesh` (`SceneBuilder.cs:819`).

**Approach.** Before `GetMesh`, ask `HdModels` by the builder's context (chapter or airframe) and
`node.Name`; on a hit, use B11's mesh. Keep `node_bias`, zone layer, shadow setting and colliders as
they are. Never swap a billboard, glow sprite, cylindrical facade, the horizon, ocean or cloud
nodes, or terrain. Count swaps for the log and the regression.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: a `--synthetic-data` suite with one replaced node asserting `NameMeta`,
`IndexMeta`, the transform and the collider count are unchanged and the mesh is the pack's>.

**⚠ Traps.** The billboard pivot moves the instance (`_meshPivotCache`, `SceneBuilder.cs:1224`); a
replacement must not inherit an original pivot offset. How the builder knows its chapter or
airframe for the key is not settled <TODO>.

## B13 ☑ [cloud] code, [local] look: material mapping onto the remake's shaders

**Landed.** Each `SceneBuilder` draws its own copy of a replacement, built from the shared import's
surface arrays (`HdModelMeshes.SurfacesOf`) under this builder's materials, cached by (pack key,
gamez model, override flags); the shared mesh and its materials are never edited. A glTF material
whose name (trimmed, extension dropped, case ignored) is a texture of the node's own polygons or the
gamez material table goes through `GetMaterial`, the same cache as an original surface, so HD
textures, liveries and flipbooks apply; it takes `lighting`, fog and scroll from the gamez model and
sidedness from `doubleSided`. Any other material wears the remake shader with its own base-colour
texture (a shared mip-mapped copy, `HdModelMeshes.AlbedoOf`) or colour. Flags come from the standard
glTF fields as Godot 4.7 imports them: `doubleSided` to two-sided (else `cull_front`, as a one-sided
original after the winding reversal), `alphaMode` `MASK` to scissor and `BLEND` (imported as
`AlphaDepthPrePass`) to blend, `KHR_materials_unlit` to unlit; no `extras`. Draw order: priority 0,
pass 0, file order as the within-mesh rank. CLAMP reads each replacement surface's own UVs
(`UvAxesWithinUnitSquare(IReadOnlyList<Vector2>)`, `ClampFor`). The `hd-model-swap` suite covers all
of it, a graphics-mode switch and `SceneBuilder.Repaint`, and was shown to fail four ways.

**Verified.** In a cloud container: build, format, StyleCop and the five gate scripts clean; 6359
unit tests pass; the CI tier 195 passed, 0 failed. <TODO: locally, goldens with no pack, a look at a
replaced prop under fog and through a mode switch, and a real livery through `PlanePainter` (the
synthetic tree has no paintable airframe).>

**Owed.** <TODO: decide each: a `MASK` `alphaCutoff` other than 0.5 is not honoured (the shader's
threshold is fixed); `baseColorFactor` is ignored on a textured surface (no tint uniform);
`KHR_materials_unlit` is ignored on a texture-named surface; replacement surfaces are outside the
surface census and `HiddenAlpha`.>

**Original approach (kept for reference).**


**Goal.** A replacement surface wears the same shader path as an original surface, so it fogs,
fades, takes world light, follows a Classic/Enhanced switch and takes HD textures.

**Evidence (confidence: traced for what the path does, lead-only for the mapping).** `SceneBuilder`'s
shaders carry draw-order bias, CLAMP per surface, vertex colour times material and
`csky_world_light` (`docs/architecture/Mech3.md`, `SceneBuilder`); `ShaderTwins.Regenerate` moves
materials between modes; fades write `csky_opacity`. A `StandardMaterial3D` does none of that.

**Approach.** Map each glTF material by name: an original texture name builds the material exactly
as an original surface of that texture; any other name loads its base-colour texture from the pack
(through `HdArt` decoding) into the same shader. <TODO: which per-surface flags (lighting,
two-sided, alpha) a non-original material takes, and where they come from, glTF extras or defaults>.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: cloud suite asserting a replaced surface's material is a tracked `ShaderTwins`
material; local look at a replaced prop under fog and through a graphics-mode switch>.

**⚠ Traps.** <TODO: none known yet>.

## B14 ☑ [cloud] Apply `rig.json` to the built plane

**Landed.** `CSVM/src/Mech3/PlaneRigPatch.cs`, run inside `PlaneBuilder.Build` after
`BuildSubtree`, so every caller (human, AI, labs, probes) gets it: markers move or are added (only
`firepoint1..8`, `pylon1..8`, `target` and the reference markers), prop and surface overrides are
stamped, flares chosen; then `CockpitCameraOffset` is read (the rig's `cockpit_camera` if given),
the interior mounts and the flare walk runs. Callers' `Loadout.Bind` sees the patched tree. A bad
rig frees the node and throws `InvalidDataException` naming the source and entry. The airframe key
is the model node (`player_bhawk`); `HdModels.RigFor` reads `planes/<model node>/rig.json`, the one
`.json` the pack index accepts. The four open fields: `hinge.*` and prop axes are plane frame,
converted to node-local, a hinge axis read as a line so its direction never flips deflection; a
flare's side comes from its plane-frame x, and a rig side replaces that side's flare; a prop on a
node `PropParts` gives no spin needs both `axis` and `deg_per_sec`; rudder `side` has no effect.
`PropAnimator` turns a tilted rig axis about its own line. Rig surfaces skip the `InNumbering` stop.
Suite `plane-rig` (CI tier), shown to fail with the hinge pivot or prop override broken.

**Verified.** In a cloud container on the merged tree: build, format, StyleCop and the five gate
scripts clean; 6357 unit tests pass; Godot import clean; the CI tier 194 passed, 0 failed.

**Owed.** <TODO: which `wing_flare` is port on real data; the fuel-leak def's
`Suspend("wing_flare2")` acts on the darkened original when a rig replaces that flare, decide
whether it should follow the rig's node.> A bad `rig.json` fails the plane build, which is
Decision 8; <TODO: decide whether a menu launch should report it and fall back instead.>

**Original approach (kept for reference).**


**Goal.** After `PlaneBuilder.Build` and before `Loadout.Bind`, the rig moves markers, adds marker
nodes the loadout names, and feeds prop, surface and flare overrides to their classifiers.

**Evidence (confidence: traced).** Firepoints and pylons resolve by `cs_name` meta on the built tree
(`Loadout.cs:409`, `:451`) and a missing one throws; `cockpit_camera` is read in `PlaneBuilder`
(`PlaneBuilder.cs:258`) through `MarkerRig.FindNamedMarker`; props classify by name with fixed rates
(`PropParts.cs:33`, `:19`); surfaces classify by the original's numbered lists
(`ControlSurfaces.cs`) and pose from a base basis plus hinge axis (`ControlSurfaceAnimator`); flares
are `wing_flare1/2` (`WingLights.cs`).

**Approach.** Patch the built tree's marker nodes' local transforms; validate names against the
tree (Decision 8). Give `PropParts.Classify` and the control-surface collection an override table
consulted before name matching. Read `cockpit_camera` after the patch, not before.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: synthetic-plane suite: a moved `pylon1` carries its rocket body there, a moved
firepoint moves the muzzle midpoint, an override prop spins about its axis, the hangar wing split
(`Loadout.WingPylons`) is unchanged>.

**⚠ Traps.** A3's reader stores four fields without meaning, and B14 decides each: which frame
`hinge.origin` and `hinge.axis` are in (`ControlSurfaces` models only a node-local axis, the hinge
position coming from the parent group); which of `wing_flare1/2` is port (`WingLights` matches by
prefix only); a rig prop on a node whose name `PropParts` does not classify has no default axis or
rate, so it may need a `prop`/`rotor` kind field; rudder `side` changes nothing today.
`InNumbering` stops at the first missing index over the whole plane (`ControlSurfaces`
entry); rig entries must not be subject to that stop, and names must stay subject to it. The
`cockpit_camera` read today happens inside `Build` (`PlaneBuilder.cs:258`), so the patch order
matters.

## B15 ☑ [cloud] code, [local] check: exporter keeps gamez names; `--dump-markers` writes a rig template

**Landed.** Godot 4.7's glTF writer exports no node metadata except a meta key literally named
`extras`, and writes each node a sanitised name unique in the file, but serialises a `GltfNode`'s
`ResourceName` verbatim. `GltfExporter.RestoreGamezNames` therefore sets each exported node's
name to its `NameMeta` after `AppendFromScene`, so `.`, `:`, `@` and repeated siblings survive in
the file. On import, `GenerateScene` sanitises again, but `GltfState.GetNodes()[i].OriginalName`
holds the file's spelling, which B11 matches on. `--dump-markers` also writes
`./.scratch/rigs/<model>/rig.json`, a markers-only template that `PlaneRig.Parse` reads back with
every position unchanged (`MarkerRig.FormatRigTemplate`). New suite `gltf-export-names` (in the CI
tier), shown to fail with the rename removed.

**Verified.** In a cloud container on the merged tree: build, format, StyleCop and the five gate
scripts clean; 6355 unit tests pass; the CI tier 191 passed with `lan-discovery` failing to bind
its port while another agent's Godot held it, and passing alone on another port base. <TODO: run
`--dump-markers` against the real `planes.zbd` locally.>

**Original approach (kept for reference).**


**Goal.** An exported plane or prop re-imports node for node, and `--dump-markers` can emit a
starting `rig.json`.

**Evidence (confidence: traced).** The exporter writes Godot node names (`GltfExporter.cs`), which
are sanitised and renamed on sibling collisions (`SceneBuilder.cs:1122` `Sanitize`, comment at
`:1163`). `MarkerRig.Format` already prints every marker position (`docs/architecture/Mech3.md`,
`MarkerRig`).

**Approach.** Write the original name into the exported node (glTF extras or a name the importer
maps back) <TODO: confirm Godot's glTF writer exports node meta as extras in the engine version
used>. Add a rig-template output to `--dump-markers`.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: synthetic suite exporting a subtree with two same-named siblings and reading
the names back; local `--dump-markers` against `planes.zbd`>.

**⚠ Traps.** <TODO: none known yet>.

# Wave C, content paths

## C21 ☐ [local] Round-trip proof on a real plane and a real world prop

**Goal.** An F10 or NodeLab export, put unedited into a pack, renders as the original did.

**Evidence (confidence: lead-only).** Follows from B11, B12, B13 and B15; nothing measured yet.

**Approach.** Export one airframe and one C-chapter prop, pack them, compare captures and counts
with and without `--no-hd-models`.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: capture pose and the image-diff rule from `docs/verification.md` that applies>;
the 8-chapter regression with mesh and node counts unchanged.

**⚠ Traps.** The export bakes damage by pruning hidden nodes (`GltfExporter` entry), so an exported
plane lacks its damage panels; a round trip of the healthy state only is the expected result.
Blender renames a repeated object name to `wing.001`, which the importer's `.flt` rule does not
strip; decide whether the importer or the pack author handles it. The exporter writes no material
names, so an unedited export's surfaces take B13's non-original path rather than the original
texture's material; name each exported material after its texture, without the extension, since
Godot's writer turns a dot in a material name into an underscore.

## C22 ☐ [local] Weapons: rockets, pylon ordnance, gun casing, splash models

**Goal.** A bare-name replacement of `he_rocket` (and the other flyout roots), `gunshell` and a
splash model shows in every chapter, in flight and on the pylons.

**Evidence (confidence: traced for the paths).** `Projectile.cs:1232`, `:2066`, `:3094`;
`PylonOrdnance` builds through `ProjectilePool.BuildFlyoutBody` (`docs/architecture/Flight.md`).
Tracers (`CrossedStreakMesh`, `Projectile.cs:1967`) and muzzle flashes (facades) are not models.

**Approach.** No new code expected beyond B12; check each path in two chapters and the weapon lab.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO>

**⚠ Traps.** `--stage=empty` builds no chapter gamez, so rockets there fly without a body
(`docs/cli.md`); test in a chapter.

## C23 ☐ [local] World props and destructibles in every damage state

**Goal.** A destructible's healthy, damaged and destroyed nodes can each be replaced, and fire and
smoke still appear in the right place.

**Evidence (confidence: lead-only).** Damage states are separate nodes toggled by animation;
`DestructibleRegistry` and effect siting take box centres from built meshes (`SubtreeBounds`,
`DestructibleRegistry` entries in `docs/architecture/Mech3.md`), so a much larger or smaller
replacement moves the centre.

**Approach.** Pick one multi-state destructible <TODO: which, in which chapter>, replace all its
states, compare effect positions with the original.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO>

**⚠ Traps.** A partial pack that replaces only the healthy state changes look on the first hit;
this is expected and the pack author's choice, not an engine bug. Whether `Centre` should read the
original mesh's box rather than the replacement's is undecided <TODO>.

## C24 ☐ [local] Plane airframes without livery

**Goal.** A replacement airframe flies with its control surfaces, props, damage panels and wreck
working, in a fixed paint.

**Evidence (confidence: traced for the bindings).** Surfaces hinge about the parent group's frame
(`ControlSurfaces` entry); props spin about `PropParts` axes; `PlaneBuilder` collects damage panels
and builds the `destroyed` subtree; `GroundShadowSilhouette` takes the triangles of the node the
original rasterises.

**Approach.** Replace the meshes of one airframe node by node; decide whether the ground shadow
reads the replacement or keeps the original outline <TODO>.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO>

**⚠ Traps.** A surface mesh must sit in its hinge parent's frame or it swings about the wrong line;
B14's `hinge` override is the escape when the model cannot follow that.

## C25 ☐ [local] Rig on a real airframe

**Goal.** A rig that moves a pylon and a firepoint on a real airframe moves the mounted rocket, the
launch point and the muzzle, and the marker overlay shows it.

**Evidence (confidence: traced for the consumers).** See B14; the overlay is `--viewer --markers`
(key K) over `MarkerRig` (`docs/formats/markers.md`).

**Approach.** Author a rig from B15's template, move one pylon and one firepoint, fly and look.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO>; the hangar's per-wing pylon counts are unchanged.

**⚠ Traps.** Firepoints move gun convergence and the tracer origin; that is a gameplay change and
belongs to C26's rules in network play.

## C26 ☐ [cloud] code, [local] two-instance check: pack and rig agreement in network play

**Goal.** Two seats with different rigs either agree on one (the host's) or refuse to start, so a
moved firepoint cannot desynchronise a match.

**Evidence (confidence: lead-only).** Nothing read yet in `src/Net/` <TODO: how the lobby handshake
carries per-seat data, and whether shot origins are simulated per seat or sent>.

**Approach.** <TODO: a pack/rig hash in the handshake, and the host-wins or refuse rule>.

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO: two local instances, one with a rig, one without>.

**⚠ Traps.** <TODO: none known yet>.

# Wave D, livery

## D31 ☐ [local] Liveries on replacement planes

**Goal.** A replacement airframe can wear the original's liveries.

**Evidence (confidence: traced for the constraint).** `PlanePainter` composites skins from the
pattern's region masks over the original UV layout and swaps the decal placeholders
(`docs/architecture/Mech3.md`, `PlanePainter`; `docs/formats/paint.md`).

**Approach.** Two options, undecided <TODO>: keep the painted surfaces' UVs compatible with the
original skins (no engine work, a hard authoring constraint), or let the pack ship its own region
masks and decal placement (extends the paint format).

**Model recommendation.** high (the Opus tier), the author's call for every item in this plan.

**Verify.** <TODO>

**⚠ Traps.** <TODO: none known yet>.
