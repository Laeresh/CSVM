# Model packs

How CSVM reads a replacement model pack: higher-detail glTF meshes drawn in place of the original's
gamez meshes, and a per-airframe `rig.json` that moves an aircraft's marker nodes. This is a
CSVM format, not a decode of the original; it is the reference a pack author works from. A pack
changes what is drawn and where an airframe's markers sit, never what the game is: every collider,
hit box and gameplay measurement keeps reading the original's geometry.

## Contents

- [Conceptual model](#conceptual-model)
- [Reference](#reference): [folder and keys](#folder-and-keys), [what a file must hold](#what-a-file-must-hold),
  [materials](#materials), [liveries](#liveries), [`shadow.glb`](#shadowglb), [`rig.json`](#rigjson),
  [clump tiles](#clump-tiles), [vegetation models](#vegetation-models)
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
edit it from there. An aircraft's cockpit interior is never in that file: the export writes it
beside it as `cockpit1.glb` (F10: `<file>_cockpit1.glb`, renamed `cockpit1.glb` in the pack), root
`cockpit1` at the origin, in the interior's own units and hierarchy with every state node kept, so
`planes/player_bhawk/cockpit1.glb` replaces the interior node by node.

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
| Gauge parts | A replaced gauge part keeps driving when its surfaces are texture-named: a belt light (`ggindicator0`), a damage zone and a needle bind as the original's. A replaced character readout (`4char_ammo`, `6char_type`) needs one surface per character, and the characters run left to right by each surface's mean x in the node's frame. |
| Under a headset | The stereo interior leaves out `cockpit1`'s head-and-shoulders plate, which a tracked head leaning back would see from the front. A replacement's plate goes by its shape: a triangle on a surface not named after an original texture, lying on one z plane between 0 and 8 interior units behind the `cockpit1` origin and facing aft (+Z) by its winding, is not drawn. The export's plate meets this unedited (it shares a node with canopy structure: `structure` itself or a node under it, `g366` on the Bloodhawk). Keep an authored plate flat, facing aft and untextured, or the headset draws it; a texture-named triangle of that shape always draws. The collimated reticle is read from the glass as drawn: each surface named after the glass texture (`cockpit6`, the Warhawk's `crosshair`) is one glass, wherever the file or the `gauges` table puts it. Keep it flat with its UVs affine over it. A glass under any other name draws as itself with no reticle; the screen's pipper still aims. |
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
priority 0. The original builds one surface per material and priority, so a texture it wears at
two priorities is two surfaces, and the export writes both. A file holding exactly as many surfaces
of a texture as the original builds gives each, in file order, the material and priority of the
original's surface in the same place (the Bloodhawk's canopy rims and its `cockpit10` panel).

**Any other name.** The surface wears the remake's shader with the file's base-colour texture, or
its base colour when it has no texture. A textured surface draws the texture times `baseColorFactor`,
alpha included, as glTF specifies. It fogs, takes world light and follows a graphics-mode switch
like any surface, but no livery or texture pack reaches it. Flags come from the standard glTF
fields: `doubleSided`, `alphaMode` `MASK` (cut at the file's `alphaCutoff`, 0.5 when absent) and
`BLEND`, and `KHR_materials_unlit`. It draws at priority 0. An untextured original surface is
exported under such a name, with `KHR_materials_unlit` when its model is `lighting: false` (the
artificial horizon's ball), so it draws as lit or unlit as the original.

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
`--dump-markers` writes a starting file per airframe to `./.scratch/rigs/<model>/rig.json`. It holds
every marker at the original's position and a `cockpit` section with `"from": "body"` that poses
every gauge assembly and the sight glass where `cockpit1` drew it on the flat screen, with explicit
`position`, `rotation` and `scale` (on the Brigand and the Firebrand the assembly holding the glass,
`g730` or `g184`, stands in for `glass`). The dumped file is therefore not a no-op: copied unedited,
it switches the airframe to a body cockpit, which draws the airframe from inside with the original's
dials in place. Deleting the `cockpit` section keeps `cockpit1`.

```json
{
  "markers": { "firepoint1": [-0.42, 0.18, -2.10], "pylon1": [-3.20, -0.35, 0.40] },
  "props": [ { "node": "prop1", "axis": [0, 0, 1], "deg_per_sec": -220, "blur": false } ],
  "surfaces": [ { "node": "l_aileron1", "kind": "aileron", "side": "left",
                  "hinge": { "origin": [-2.8, 0.0, 0.6], "axis": [1, 0, 0.12] } } ],
  "flares": { "left": "wing_flare1", "right": "wing_flare2" },
  "cockpit": { "from": "body", "hide": ["g12"],
               "gauges": { "speedometer": { "position": [-0.22, 0.48, -0.74], "scale": 0.05 },
                           "glass": { "position": [0, 0.72, -0.62] } } }
}
```

| Section | Rule |
|---|---|
| `markers` | Marker name to position. Moves any marker the airframe has. It may add `target`, `cockpit_camera`, `exhaust1`, `exhaust2`, `ground_level`, `ladder_pos` or `cf_light` when the model lacks one, never a firepoint or pylon. `pylon1` to `pylon8` only, an odd pylon at x < 0 and an even one at x > 0 (none on the centreline). Moving `cockpit_camera` moves the first-person eye. |
| `props` | `node`, and optional `axis` (plane frame, any length but not zero), `deg_per_sec` and `blur` (whether the node is a blur disc). A node the original does not spin needs both `axis` and `deg_per_sec`. |
| `surfaces` | `node`, `kind` (`aileron`, `elevator`, `rudder`), `side` (`left` or `right`, required except on a rudder, where it changes nothing), and an optional `hinge` with `origin` and `axis` in the plane frame. Without a hinge the surface turns about the original's hinge parent and node-local axis, so a replacement mesh must sit in that frame. |
| `cockpit` | `from`, `hide` and `gauges`, each optional. `"from": "body"` makes a body cockpit: cockpit view draws the airframe, replaced or not, from `cockpit_camera`, and none of `cockpit1`. `hide` (needs `"from": "body"`) lists airframe nodes outside `cockpit1` that leave the pilot's own cockpit view; other panes and every outside view still draw them. `gauges` maps an assembly under `cockpit1`'s `gauges` node (`speedometer`, `altimeter`, `comp`, the horizon `horizn`, `horiz`, `horizm` or `hparent`, `gungauge`, `missilegauge`, `nitrogauge`, `damageindicator`; an unknown name's error lists the airframe's own) or `glass`, the sight glass, to a pose: `position` (required), `rotation` (Euler degrees in Godot's YXZ order, as `Node3D.RotationDegrees` reads them) and `scale` (one factor above zero). In a body cockpit a pose is plane-frame metres. A named assembly draws there in the pilot's own cockpit view, its needles, lamps, readouts, belts and damage zones driven as in `cockpit1`, with `cockpit1`'s shading and no shadow from the airframe. An absent `rotation` or `scale` keeps the facing and size `cockpit1` showed the assembly at; an assembly not named is not drawn. The glass's position is its centre, and it draws only when named. On the Brigand and the Firebrand the glass is itself an assembly (`g730`, `g184`), so naming that and `glass` together fails. While one named assembly is drawn the screen-space dials are off. Under a headset a body cockpit draws the same airframe, dials and glass at the same plane-frame poses, real scale whatever the plane's `vr_cockpits.json` scale factor, with the eye at `cockpit_camera` plus the plane's seated-eye offset; a named glass shows the collimated reticle, read from `cockpit_camera`. Without `"from": "body"` a pose is in interior units in `cockpit1`'s own frame, relative to the `cockpit1` root as `cockpit1.glb` holds it, and the named assembly moves there inside `cockpit1`, replaced or not, still driven. An absent `rotation` or `scale` keeps the assembly's own; an assembly not named stays where the original put it, and an empty replacement hides one ([Hiding a node](#what-a-file-must-hold)). A named glass is cut loose with its centre at the position and no longer draws where it was. Under a headset the collimated reticle draws on the moved glass, and the pose scales with the plane's `vr_cockpits.json` factor as all of `cockpit1` does. |
| `flares` | `left` and `right`: the nodes the wingtip lamps light. A side not given keeps the original's flare for that side. An original flare's side is its position, not its digit: `wing_flare1` is port on every airframe that has flares except the Warhawk, where it is starboard (the Bloodhawk and the autogyro carry none). A rig flare takes over every effect aimed at the original it replaced, so the fuel leak, which darkens `wing_flare2`, darkens the rig's flare on that side. |

A rig says where, never what: guns, calibres, ammunition, pylon count and pylon side stay the
original's and the save's. Validation is loud: an unknown key, a non-finite number, a zero axis, a
name defined twice, a pylon out of range or on the wrong side, or a node the airframe lacks fails
the aircraft's build with a message naming the file and the entry. A flight launched from the menu
is the exception: it logs that error, tells the player at the menu's next show, and flies the
aircraft without its rig, none of the rig's entries applied.

### Clump tiles

Under Enhanced and Rich the far forest draws as impostor clumps, cards of a few trees each
(`Mech3/CanopyClumps.cs`). Their atlas holds six tiles. A species with a clump tile fills its share
of them with its own tiles; the other species' tiles are composed at load from their cards. The
remake ships a species' tile beside its 3D model in `CSVM/data/vegetation_models/<chapter>/`,
rendered from that model (`MakeVegetationModel.ps1`, [tooling.md](../tooling.md)), and a pack
replaces it under the same key.

| Item | Rule |
|---|---|
| Key | `<chapter>/<card>.clump.png`, the chapter spelt as its `extracted/` folder. `<card>` is the listed card's model without `.flt` (`C1/firtree1.clump.png`); over painted canopy (C3) it is the canopy texture without its extension (`C3/forest1.clump.png`, the crowns cut from `forest1.tif`). A chapter key only: one card name is a different size per chapter. |
| Files | The image `<card>.clump.png`, its sidecar `<card>.clump.json`, and an optional side map `<card>.clump.side.png`, all in one pack. Shipped, the images are `.png.bin` so Godot imports nothing. |
| Precedence | A pack's tile, then the shipped one, then the tiles composed from the cards. All the files come from the one source that holds the image and its sidecar: a pack holding only one of the two, or a bad sidecar, logs a warning and keeps the shipped tile. |
| Image | A PNG strip of equal tiles side by side, RGBA, cut to clear and solid at alpha 0.5. A tile is `width_m` wide and as tall as its pixels make it at that width, with the trees standing on its bottom row. The game draws it at 10 px a metre, so a larger image is scaled down. The atlas slot is 24 m wide and as tall as the tallest of the listed cards' composed tiles and the tiles in it, a tile centred on its floor. |
| Sidecar | `{"width_m": 24, "trees": [3, 2, 4]}`. `width_m`: a tile's width in metres, above 0 and at most 24. `trees`: one whole count of at least 1 per tile, which also sets how many tiles the strip holds. Keys starting `_` are prose; any other key fails. |
| Side map | The image's size; red is each solid texel's sun-side share on its tree's right half, green on its left, full a sixth of the tree's width off its trunk. The game's sun-side gain then lands per tree as on a card. Without it each tile is shaded as one tree. |
| Slots | A tiled species takes its share of the card stamps in slots, by largest remainder; the untiled species together take the rest, composed. A species under about a twelfth of the stamps gets no slot and stays composed. Its slots cycle through its tiles. |
| Density | The clumps carry the cards' trees per area: each cell's clumps are its kept stamps over the mean tree count of the six slots, which reads a tile's count from its sidecar. A tile with other counts changes how many clumps stand, not how many trees. |
| Separate from models | A clump file keeps its whole name as its key, so no gamez node resolves to it, and it counts in the load line's `clump tile files`, not in its replacements. Any other `.png` in a pack is ignored. |

Each build logs `canopy clumps: ... tiles: <card> in <n> slots from <file>` (or `all composed`),
the quickest check that a key was read.

### Vegetation models

Under Enhanced and Rich a listed card species with a 3D model draws it on the card's own stamps near
the camera (`Mech3/VegetationModels.cs`): the model alone inside the near edge of the 3D Vegetation
option's band (150 m at Near, 250 m at Far), the card alone past its far edge (250 or 400 m), the two
dithered over the band between. The remake ships a species' model beside its clump tile,
generated by `MakeVegetationModel.ps1`, and a pack replaces it under the same key. The card itself is
never replaced, and a species with no model draws its card at every distance.

| Item | Rule |
|---|---|
| Key | `<chapter>/<card>.glb` (or a loose `.gltf`), the chapter spelt as its `extracted/` folder and `<card>` the listed card's model without `.flt` (`C1/firtree1.glb`). A chapter key only: a bare `firtree1.glb` or another chapter's file is not read for it. A billboard is never replaced, so the key reaches no other node. |
| Precedence | A pack's model, then the shipped `CSVM/data/vegetation_models/<chapter>/<card>.glb.bin`, then none. A file that fails to import, or holds no textured mesh, logs a warning and the next source is read. |
| File | The first mesh, drawn whole under one material that wears its first surface's base-colour texture. Metres, Y up, the pivot at the trunk base on the ground, sized to the card it stands in for. `alphaMode` is read as a cut at 0.5 whatever the file says; both faces draw. |
| Look | Drawn as its card is drawn, not lit by the scene: the card's fog, tint, breeze and sun-side gain, the stamp's mirror and a turn hashed from where it stands. A grade scales the model's brightness to its card's, measured over the top half of each (the crown): the model's texels weighted by the surface they cover against the card texture's, so a lighter or darker file still meets its card at the handover. The model keeps its own hue; each colour channel moves at most 10 % off that brightness ratio, so tune a file's colours to its card. |
| Not read | Every other mesh, surface and material field, animation, skins and lights. No collider is ever built. |

Each build logs `vegetation model <file>: <n> triangles, grade r g b, band 150-250 m` (the band standing) for each
species it reads, the quickest check that a key was read.

## Reader rules and edge cases

### Never replaced

Whatever the pack holds, these keep the original's mesh: billboards and glow sprites (a vegetation
card's far clumps take a [clump tile](#clump-tiles) instead, and its near stamps a
[vegetation model](#vegetation-models)), single-axis
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
  real install and finds the same needles, lamps, readouts, belts and damage zones as without it.
  The same suite exports the Bloodhawk and packs its `cockpit1.glb` unedited: all 67 meshed interior
  nodes are replaced, each with the original's vertex count, bounds, texture names and unlit
  surfaces, each textured surface at its original draw priority, and the airframe file holds no
  interior node. A cockpit-view shot of that pack differs from the original in 9 pixels, by at most
  8 levels. Without the one-for-one surface rule, the canopy rims, worn at priorities 0 and 5, draw
  all at 5 over the padding at the frame's bottom rows.
  The rig rules are `Mech3/PlaneRig.cs` and `Mech3/PlaneRigPatch.cs`, covered
  by `CSVM.Tests/PlaneRigTests.cs` and the `plane-rig` suite. The gauge table is
  `Mech3/CockpitTransplant.cs`, covered by `CSVM.Tests/CockpitTransplantTests.cs` and the
  `plane-rig-cockpit-gauges` (body cockpit) and `plane-rig-cockpit-inside` (inside `cockpit1`)
  suites; the latter moves a `gungauge` drawn from a texture-named export and drives its belt lights
  and readouts.
- The headset rules are `Mech3/CockpitPlate.cs` (`Kept`) and `Mech3/GunsightReticle.cs` (`Drawn`).
  `CSVM.Tests/CockpitPlateTests.cs` triangulates every untextured polygon of the eleven player
  interiors as the build does: the shape rule takes exactly the plate's triangles and no other, while
  textured triangles of that shape stand on the Autogyro, Brigand and Kestrel. The
  `hd-model-cockpit1-stereo` suite packs the Bloodhawk's unedited export under the stereo build: the 25
  plate triangles are cut, the interior draws stock's 1151 triangles, and the one reticle reads the glass
  as stock's does; a `gauges` table moving the glass, and a texture-named glass file at a new place,
  each put the reticle in the moved glass's plane. Its `--xr-sim` left eye differs from stock's in 3
  pixels, by 1 level.
- The UV-compatible livery rule is measured on the Bloodhawk. An unedited export, packed one copy
  per node or as the one root file `planes/player_bhawk/player_bhawk.glb`, wears three patterns with
  chosen decals within one level per pixel of the original, parked, in flight and through the
  livery lab; from below, the same 16 silhouette pixels differ painted as unpainted. A copy edited in
  Blender with its UVs and material names kept takes each livery on the changed shape, and the same
  copy with its wing materials renamed keeps on its wings the painted skin the export embedded.
- The clump tile rules are `Mech3/ClumpTile.cs` (the sources and the sidecar), `Extraction/HdModels.cs`
  (the keys) and `Mech3/CanopyClumps.cs` (`Slots`, `MeanTrees`, the decode), covered by
  `CSVM.Tests/ClumpTileTests.cs`, `HdModelsTests.cs` and `CanopyClumpsTests.cs` and by the
  `canopy-clumps` suite, which reads the shipped `firtree1` tile on C1, composes without it, and
  takes a pack's tile with other counts at a higher tree count a clump.
- The vegetation model rules are `Mech3/VegetationModels.cs` and `Extraction/HdModels.cs`
  (`Vegetation`), covered by `CSVM.Tests/VegetationModelsTests.cs`, `HdModelsTests.cs` and the
  `vegetation-models` suite, which reads the shipped `firtree1` on C1 and a pack's under the same key.
- The `rig.json` schema and its checks are CSVM's own; the marker frame and pylon sides are the
  original's ([markers.md](markers.md)).
- What Godot's importer gives the builder: `alphaCutoff` as the alpha scissor threshold,
  `baseColorFactor` as the albedo colour (sRGB, so the shader's `source_color` reads it back linear,
  as the texture), `KHR_materials_unlit` as the unshaded mode. `GltfExporter` writes a texture-named
  material with no alpha mode, a white factor and no unlit extension, so an unedited export maps back
  to the original's material in every field read.
