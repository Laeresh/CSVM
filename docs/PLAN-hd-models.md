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

1. ☐ [cloud] Shared pack index out of `HdTextures`
2. ☐ [cloud] `HdModels` lookup, keys and launch settings
3. ☐ [cloud] `PlaneRig` reader and validation

### Wave B, engine hooks (code [cloud], look [local])

11. ☐ [cloud] glTF mesh import with winding reversal and a shared cache
12. ☐ [cloud] Mesh swap in `SceneBuilder.FillSubtree` and `SharedMesh`
13. ☐ [cloud] code, [local] look: material mapping onto the remake's shaders
14. ☐ [cloud] Apply `rig.json` to the built plane
15. ☐ [cloud] code, [local] check: exporter keeps gamez names; `--dump-markers` writes a rig template

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

## A1 ☐ [cloud] Shared pack index out of `HdTextures`

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

## A2 ☐ [cloud] `HdModels` lookup, keys and launch settings

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

## A3 ☐ [cloud] `PlaneRig` reader and validation

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

## B11 ☐ [cloud] glTF mesh import with winding reversal and a shared cache

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

## B12 ☐ [cloud] Mesh swap in `SceneBuilder.FillSubtree` and `SharedMesh`

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

## B13 ☐ [cloud] code, [local] look: material mapping onto the remake's shaders

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

## B14 ☐ [cloud] Apply `rig.json` to the built plane

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

**⚠ Traps.** `InNumbering` stops at the first missing index over the whole plane (`ControlSurfaces`
entry); rig entries must not be subject to that stop, and names must stay subject to it. The
`cockpit_camera` read today happens inside `Build` (`PlaneBuilder.cs:258`), so the patch order
matters.

## B15 ☐ [cloud] code, [local] check: exporter keeps gamez names; `--dump-markers` writes a rig template

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
