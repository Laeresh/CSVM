# Model packs

How CSVM reads a replacement model pack: higher-detail glTF meshes drawn in place of the original's
gamez meshes, and a per-airframe `rig.json` that moves an aircraft's marker nodes. This is a
CSVM format, not a decode of the original; it is the reference a pack author works from. A pack
changes what is drawn and where an airframe's markers sit, never what the game is: every collider,
hit box and gameplay measurement keeps reading the original's geometry.

## Contents

- [Conceptual model](#conceptual-model)
- [Reference](#reference): [folder and keys](#folder-and-keys), [what a file must hold](#what-a-file-must-hold),
  [materials](#materials), [liveries](#liveries), [`shadow.glb`](#shadowglb), [`rig.json`](#rigjson)
- [Reader rules and edge cases](#reader-rules-and-edge-cases): [never replaced](#never-replaced),
  [gameplay keeps the original](#gameplay-keeps-the-original), [network play](#network-play)
- [Evidence & limits](#evidence--limits)

## Conceptual model

A replacement swaps the mesh one gamez node draws. The node itself stays as the original built it:
its transform, name, animation bindings, damage toggles, control-surface hinge, prop spin and weapon
mounts all ride the node, so every one of them keeps working over the new mesh. A pack therefore
replaces node by node, under the original's node names, in each node's own frame.

The surest start is an export of the original: **F10** in the game, or `--export-gltf=<path>`
(`docs/cli.md`), writes an aircraft or, with `--chapter=` and `--node=`, a world subtree, with the
gamez node names and texture-named materials kept. Put unedited into a pack under its root's key
(`planes/player_bhawk/player_bhawk.glb` for the Bloodhawk), that file draws as the original does;
edit it from there.

## Reference

### Folder and keys

| Item | Rule |
|---|---|
| Folder | `HDModels` under the data root by default; `models.hd` in `config.json` or `--hd-models=<folder>` names another, `--no-hd-models` reads none. A `--det` run reads none unless the flag names one. Separate from the HD texture folder (`--hd=`). |
| Packs | Each subfolder and each `.zip` directly inside the folder is one pack. Loose files at the folder's top are ignored. Packs load in name order, and a later pack wins a key. |
| Top folder | Each pack's own top folder is dropped: `HDModels/mypack/planes/player_bhawk/body.glb` and a zip entry `mypack/planes/player_bhawk/body.glb` both key `planes/player_bhawk/body`. ⚠ A zip needs one top folder; an entry `planes/player_bhawk/body.glb` keys `player_bhawk/body` and replaces nothing. |
| File types | `.glb` from zips and subfolders; `.gltf` (with its buffers and images beside it) from subfolders only, since a zip entry has no folder to resolve a relative path against. A `.gltf` in a zip is skipped with a warning. |
| Airframe key | `planes/<model node>/<node>.glb`, the model node being the airframe's gamez root (`player_bhawk`, `player_kestrel`). Reaches that airframe's nodes only, its `cockpit1` interior's included. |
| Cockpit interior | `planes/<model node>/cockpit1.glb` is the root file of the interior the pilot sees in cockpit view, and replaces each `cockpit1` node it holds a glTF node for. Being below the airframe, an interior node is also reached by the airframe's root file, by its own airframe key and by a bare file, after `cockpit1.glb`. No meshed interior node shares a name with a node outside it on any airframe, so an airframe file holding only exterior names reaches none. Its frame is in [what a file must hold](#what-a-file-must-hold). |
| Chapter key | `<chapter>/<node>.glb`, the chapter spelt as its `extracted/` folder (`C1`, `C1B`, `C3`). Reaches that chapter's world and the weapons built from its gamez. |
| Bare key | `<node>.glb`. Reaches every builder: every chapter and every airframe. |
| Precedence | A file named after the node's nearest gamez ancestor that names the node comes first (the root scope below); then the airframe key, the chapter key and the bare key, in that order. |
| Root scope | A file keyed by a gamez ancestor (`planes/player_bhawk/player_bhawk.glb`, `C1/ap_h2otwr1.glb`, `he_rocket.glb`) replaces each node below that ancestor that it holds a glTF node for, and no other node of that name. Generic names repeat (`g12` draws the body of five rockets and 15 to 25 other nodes per chapter, `part1` names 74 C1 nodes), so key a destructible, a weapon or a whole airframe by its root. Inside a root file, of several glTF nodes of one name, the one whose ancestors follow most of the gamez names between the root and the node wins. A root file has no whole-file fallback, and a node it does not hold falls to its own key. |
| Names | A node is its original gamez name, never Godot's node name, matched without case. A trailing `.flt` is dropped from the node and the file alike, so `he_rocket.glb` and `he_rocket.flt.glb` both reach `he_rocket` and `he_rocket.flt`. Any other dot is kept. |

Each build logs how many meshes it drew from the pack (`HD models: N mesh instance(s) of
player_bhawk drawn from the model pack`, and the same for the wreck pieces and, as `cockpit interior
mesh instance(s)`, the cockpit interior), which is the quickest
check that a key reached its node.

### What a file must hold

| Item | Rule |
|---|---|
| Frame | Metres, Y up, nose toward -Z, right +X, as glTF and the exporter write it. Each mesh is in its gamez node's own frame: the matched glTF node's own transform is ignored, because the game already places the node by the original's. Transforms of nodes below the matched node do apply. |
| Interior frame | A `cockpit1` node's mesh is in the interior's own units, the units its gamez nodes are authored in, about 25 to the metre, never in metres. The game mounts the whole interior at the `cockpit_camera` marker scaled by 0.04 (and tilted by the first-person view's fixed head pitch on the flat screen), so a replacement is scaled with it. The interior's draw-order bias is built for that scale, and a replacement's surfaces take it. |
| Matching node | The first glTF node named like the gamez node gives the mesh; failing an exact name, one with Blender's repeat suffix (`wing.001`). In a file keyed by the node itself (not a root file) and holding no such name, every mesh in the file merges from its scene roots. |
| Merge | Every mesh at or below the matched node merges into one, placed by the transforms below it. A subtree named after one of the gamez node's own descendants is left out, since that descendant draws it (the exporter writes `tail`'s moving `lrudder1` under `tail`). A mirroring transform is honoured. |
| Hiding a node | A matched node with no mesh below it draws nothing. The node, its bindings and its colliders stay. A file that fails to import draws the original and logs one warning. |
| Winding | glTF's own: the front face is the counter-clockwise side, as an exported file and Blender have it. The importer converts. |
| Scale | Any. A larger or smaller model changes no box the game measures (see below). The water splash bases are scaled by their animation from the original's base size (radius 0.12 m for `splash1`, 0.95 m for `bsplsh`), so author a splash base at that size. |
| LOD | Only an LOD group whose range starts at 0 is built (`nearest` and `l12` on the Bloodhawk), so a file for a far LOD's node never draws. |
| Not read | Animation, skins, morph targets, cameras, lights and node visibility. |

### Materials

A glTF material falls in one of two kinds, decided by its name.

**Named after an original texture.** The name, trimmed, without case and with everything after its
last dot dropped (`blo_wing`, `blo_wing.tif`, Blender's `blo_wing.001`), is a texture of the gamez
node's own polygons or of the gamez material table. The surface then wears exactly the material an
original surface of that texture wears: the archive's texture (or the HD texture pack's), the
livery, a texture flipbook, the alpha class, the gamez model's lighting, fog and scroll. Two fields
are read from the file: `doubleSided`, and `KHR_materials_unlit`, which makes the surface unlit on a
model the original lights (absent, the surface keeps the model's lighting). The file's own image,
colour, `alphaMode` and `alphaCutoff` are ignored, since an export writes a texture-named material
opaque, white and lit whatever the original's alpha class and lighting. It draws at
the highest priority of the node's own polygons wearing that texture, so a decal on the original's
coplanar decal polygons stays over the skin; a texture the node's own polygons do not wear draws at
priority 0.

**Any other name.** The surface wears the remake's shader with the file's base-colour texture, or
its base colour when it has no texture. A textured surface draws the texture times `baseColorFactor`,
alpha included, as glTF specifies. It fogs, takes world light and follows a graphics-mode switch
like any surface, but no livery or texture pack reaches it. Flags come from the standard glTF
fields: `doubleSided`, `alphaMode` `MASK` (cut at the file's `alphaCutoff`, 0.5 when absent) and
`BLEND`, and `KHR_materials_unlit`. It draws at priority 0.

Within one mesh, surfaces draw in the file's surface order. A one-sided surface is culled like a
one-sided original. Replacement surfaces count in the world's surface census as original ones do
(the load log's UV-clamped and edge-clamped totals, and the blended and cutout counts), and
`--hide-alpha` drops a blended or cutout replacement surface with its class.

### Liveries

A replacement airframe wears the original's liveries when it is **UV-compatible**: each painted
surface keeps the material name of the original skin texture it stands for (`blo_wing`,
`blo_fusalagetop`) and UVs laid out like the original's over that texture. The painter composites a
pattern's skins from the pattern's per-texel region masks in the original skin's texture layout
([paint.md](paint.md), [rof.md](rof.md)), so a surface whose UVs follow that layout takes the colours
where the original takes them. There is no way for a pack to carry its own region masks or decal
placement.

| Item | Rule |
|---|---|
| Painted surfaces | A material named after a texture with the airframe's own skin prefix (`blo_` on the Bloodhawk) that the chosen pattern ships a `.BM` for (`extracted/rof/ASSETS/GRAPHICS/<PATTERN>/`). Another airframe's skin texture keeps its shipped image. |
| Decals | A material named `<prefix>_noselogo`, `_taillogo` or `_winglogo` takes the chosen decal. The decal fills the texture's whole UV square, so map each decal surface's UVs as the original placeholder polygons do, and put it on the node whose original carries that decal (or lift it off the skin), since only there does it take the decal's draw priority. |
| Fixed paint | A surface under any other name keeps its own texture or colour under every livery. A model that is not UV-compatible is a fixed-paint model. An export embeds each skin as it was painted when exported, so a renamed exported material keeps that livery's colours. |
| Where it applies | Everywhere the airframe is built: the flown aircraft, the viewer's livery lab, AI aircraft, a network seat (which sends its livery as pattern, swatch and decal indices, painted on each machine over that machine's pack), the damage panels and the wreck pieces. The hangar's paint screen previews the original's flat `PX_ICON` art and never shows a model. |

### `shadow.glb`

`planes/<model node>/shadow.glb` (or `.gltf`) is a low-poly outline for the aircraft's ground shadow,
in the airframe's frame. Without it the shadow keeps the original's outline, whatever the pack
draws. Only the airframe key counts: a bare or chapter `shadow` file belongs to a world node of that
name. The propeller and rotor blur discs stay the built model's. An empty file leaves only the discs,
and a failed import keeps the original outline. Keep it small: the shadow is rasterised every frame
for every aircraft.

### `rig.json`

`planes/<model node>/rig.json` corrects where an airframe's markers and moving parts sit. It is the
one `.json` a pack reads, and it needs no replacement model beside it. Every section and field is
optional, and an absent one keeps the original's value. Vectors are metres in the plane frame
([markers.md](markers.md)): below the airframe root, nose -Z, right +X, up +Y.
`--dump-markers` writes a starting file per airframe to `./.scratch/rigs/<model>/rig.json`.

```json
{
  "markers": { "firepoint1": [-0.42, 0.18, -2.10], "pylon1": [-3.20, -0.35, 0.40] },
  "props": [ { "node": "prop1", "axis": [0, 0, 1], "deg_per_sec": -220, "blur": false } ],
  "surfaces": [ { "node": "l_aileron1", "kind": "aileron", "side": "left",
                  "hinge": { "origin": [-2.8, 0.0, 0.6], "axis": [1, 0, 0.12] } } ],
  "flares": { "left": "wing_flare1", "right": "wing_flare2" }
}
```

| Section | Rule |
|---|---|
| `markers` | Marker name to position. Moves any marker the airframe has. It may add `target`, `cockpit_camera`, `exhaust1`, `exhaust2`, `ground_level`, `ladder_pos` or `cf_light` when the model lacks one, never a firepoint or pylon. `pylon1` to `pylon8` only, an odd pylon at x < 0 and an even one at x > 0 (none on the centreline). Moving `cockpit_camera` moves the first-person eye. |
| `props` | `node`, and optional `axis` (plane frame, any length but not zero), `deg_per_sec` and `blur` (whether the node is a blur disc). A node the original does not spin needs both `axis` and `deg_per_sec`. |
| `surfaces` | `node`, `kind` (`aileron`, `elevator`, `rudder`), `side` (`left` or `right`, required except on a rudder, where it changes nothing), and an optional `hinge` with `origin` and `axis` in the plane frame. Without a hinge the surface turns about the original's hinge parent and node-local axis, so a replacement mesh must sit in that frame. |
| `flares` | `left` and `right`: the nodes the wingtip lamps light. A side not given keeps the original's flare for that side. An original flare's side is its position, not its digit: `wing_flare1` is port on every airframe that has flares except the Warhawk, where it is starboard (the Bloodhawk and the autogyro carry none). A rig flare takes over every effect aimed at the original it replaced, so the fuel leak, which darkens `wing_flare2`, darkens the rig's flare on that side. |

A rig says where, never what: guns, calibres, ammunition, pylon count and pylon side stay the
original's and the save's. Validation is loud: an unknown key, a non-finite number, a zero axis, a
name defined twice, a pylon out of range or on the wrong side, or a node the airframe lacks fails
the aircraft's build with a message naming the file and the entry. A flight launched from the menu
is the exception: it logs that error, tells the player at the menu's next show, and flies the
aircraft without its rig, none of the rig's entries applied.

## Reader rules and edge cases

### Never replaced

Whatever the pack holds, these keep the original's mesh: billboards and glow sprites, single-axis
facades (the splash columns, `g1182` on C1's water tower), a node whose original draws nothing (every
weapon root such as `he_rocket` is a meshless group, so its body is a descendant like `g12`), the
horizon dome, the cloud and sky meshes (the `skywal*` walls excepted), open-sea meshes, and terrain:
`CAN_MODIFY` nodes, all-water meshes, cell-sized ground tiles and the map's border strips. Also never
replaced: the subtrees a cutscene stages from the aircraft archive
(`piratefighter`, `balmoral`, `chuteman` and the rest) and the `shadow` card, whose key is the shadow mesh above.

### Gameplay keeps the original

Colliders, a plane's collision hulls, its terrain sweep and hit body, a shootable round's hit box,
the damage-panel pairing, the ground shadow (without `shadow.glb`), targeting and aim-assist centres,
objective markers, object zone gates, range gates and where fire and smoke are sited all read the
gamez model a replacement draws over. A pack of a very different size or shape therefore plays
exactly as the original does, and only a rig's markers move a gameplay position.

A destructible is a node per state only at healthy and destroyed: no damage sequence in the install
switches a node, so its damaged look is the healthy mesh with its smoke and fire stages. Key it by its
root and give the file a node per state (`C1/ap_h2otwr1.glb` holding `h2twr_healthy` and the
`h2twr_destroyed` parts).

### Network play

A pack needs nothing in network play, and two machines may run different packs. Every outcome a rig
could move is decided on one machine: a shot leaves from the shooter's own firepoint or pylon and
crosses the wire with its origin, and the shooter's machine decides the hit. Each machine draws every
aircraft with its own pack and its own rig, so another player's rocket can leave from a pylon other
than the one this machine draws its store on.

## Evidence & limits

- The key, folder and name rules are `Extraction/HdModels.cs` and `Extraction/PackIndex.cs`, covered
  by `CSVM.Tests/HdModelsTests.cs` and `PackIndexTests.cs`; the import, merge and scope rules are
  `Mech3/HdModelMeshes.cs`, and the material, draw-order and never-replaced rules are
  `Mech3/SceneBuilder.cs` and `Mech3/WorldBuilder.cs`, covered by the `hd-model-mesh-import` and
  `hd-model-swap` suites. The interior's scope is `Mech3/PlaneBuilder.cs`, covered by the
  `hd-model-cockpit1` suite, which also binds the cockpit gauges over a replaced gauge face on the
  real install and finds the same needles, lamps, readouts, belts and damage zones as without it. The rig rules are `Mech3/PlaneRig.cs` and `Mech3/PlaneRigPatch.cs`, covered
  by `CSVM.Tests/PlaneRigTests.cs` and the `plane-rig` suite.
- The UV-compatible livery rule is measured on the Bloodhawk. An unedited export, packed one copy
  per node or as the one root file `planes/player_bhawk/player_bhawk.glb`, wears three patterns with
  chosen decals within one level per pixel of the original, parked, in flight and through the
  livery lab; from below, the same 16 silhouette pixels differ painted as unpainted. A copy edited in
  Blender with its UVs and material names kept takes each livery on the changed shape, and the same
  copy with its wing materials renamed keeps on its wings the painted skin the export embedded.
- The `rig.json` schema and its checks are CSVM's own; the marker frame and pylon sides are the
  original's ([markers.md](markers.md)).
- What Godot's importer gives the builder: `alphaCutoff` as the alpha scissor threshold,
  `baseColorFactor` as the albedo colour (sRGB, so the shader's `source_color` reads it back linear,
  as the texture), `KHR_materials_unlit` as the unshaded mode. `GltfExporter` writes a texture-named
  material with no alpha mode, a white factor and no unlit extension, so an unedited export maps back
  to the original's material in every field read.
