# Making a plane mod

A walk-through for preparing a model so CSVM flies it as a new plane. It follows a Blender workflow,
since Blender's glTF exporter writes what CSVM reads; any tool that writes the same glTF works. Every
exact rule (keys, limits, what is skipped) is in the reference,
[formats/mod-planes.md](../formats/mod-planes.md); this page is the order to do things in and the mistakes
that cost the most time.

## What a mod is

A mod is one folder (or a zip of it) under `Mods/Planes/` in the data root:

```
Mods/Planes/my_plane/
  plane.json        the definition: name, parent, overrides
  plane.glb         the model
  my_mask.png       optional: livery mask
  my_detail.png     optional: livery detail map
```

The folder name is the plane's id (`my_plane`): at most 31 bytes, not a stock airframe's node name
(`player_brigand`, `player_avenger` and the rest) and not another mod's id.

Every mod names a stock **parent** (`kind_of`). The plane flies with the parent's flight model,
damage model, weapon slots, engine sounds, cockpit and AI, changed only where `plane.json` overrides a
value. Pick the stock plane closest to yours: engine count, a turret or not, size and role. The parent
also decides which node names the model needs, since its animations and weapon fit look nodes up by
name.

## 1. Start from the parent's export

Export the parent once and keep it open beside your model as the reference for names and marker
positions:

```
CSVM.exe -- --viewer --plane=player_brigand --export-gltf=C:\work\brigand.glb
```

(From a source checkout, `RunGame.ps1` takes the same arguments without the `--`.) This writes the airframe and its cockpit interior (`cockpit1.glb`) beside it. The viewer hides the
spinning blur discs, so the export holds only the stopped `staticpropN` blades; the `propN`/`propNb`
names below come from this guide. An unedited export wrapped as a mod flies as its parent does,
which makes it a good first test of the folder and `plane.json`.

The export holds the original game's meshes and textures. Use it as a reference; share only your own
model and textures.

## 2. Set up the scene

| Item | Rule |
|---|---|
| Units | Metres, at the real size (the E-1C Avenger spans 11.7 m). |
| Axes in Blender | Nose toward +Y, up +Z, right wing toward +X. The exporter's +Y Up option turns this into glTF's nose -Z, which is what CSVM reads. |
| Transforms | Apply scale on every mesh (Object > Apply > Scale). Rotations belong only on the hinge empties described below. |
| Root | One empty named exactly the mod's id. Everything the plane needs hangs below it; nothing else is exported. |

## 3. Build the node tree

Use empties for every grouping node and marker, and the stock names (matched without case; Blender's
`.001` suffix is dropped on import). The tree a twin-engine Brigand-based mod uses:

```
my_plane                          root, named the mod id
  geometry
    healthy
      nearest
        nose                      fuselage, canopy, nose guns, exhausts
        tail                      tail boom, fin, stabilisers
          lft_elev > l_elevator1  hinge empty > moving surface
          rt_elev  > r_elevator1
          lrudder1 > l_rudder1
        leftwing                  left wing, nacelle, pylon, wing guns
          lailer1  > l_aileron1
        rightwing
          railer1  > r_aileron1
        pilot_pos > pilot > body > head
    dontmove
      staticprop1, staticprop2    stopped blades
      prop1, prop1b, prop2, prop2b  blur discs
  markers
    cockpit_camera, firepoint1..8, pylon1..8, exhaust1, exhaust2, ground_level, target
```

Take the surface and prop names from your parent: a single-engine parent has `prop1` only, and a
surface under any other name does not move.

### Body and damage zones

Each mesh sits under one of the four zones, `nose`, `tail`, `leftwing` or `rightwing`. The zone is
where a hit on that mesh is charged and which wreck piece it becomes when the plane breaks up. The
drawn triangles are also the collision and hit geometry, so keep stray helper geometry (reference
planes, hidden boxes) out of the exported tree.

### Control surfaces

A control surface is a hinge empty with the surface mesh as its child. The game turns the empty, so
the empty's placement is the hinge line:

| Item | Rule |
|---|---|
| Empty origin | On the hinge line. |
| Empty axes (Blender) | Ailerons and elevators: the empty's local X runs along the hinge. Rudder: its local Z runs along the hinge. Tilt the empty to follow a swept or angled hinge. |
| Surface mesh | Parented to the empty with an identity local transform: location 0, rotation 0, scale 1, and its vertices in the empty's frame. |

Getting the identity transform by hand is fiddly. This Blender Python snippet bakes a surface into its
hinge without moving it on screen:

```python
import bpy
from mathutils import Matrix
hinge, surf = bpy.data.objects["lailer1"], bpy.data.objects["l_aileron1"]
world = surf.matrix_world.copy()
surf.parent = hinge
surf.data.transform(hinge.matrix_world.inverted() @ world)
surf.matrix_parent_inverse = Matrix.Identity(4)
surf.matrix_basis = Matrix.Identity(4)
```

To check a hinge, rotate the **empty** about its axis and watch the surface swing, then set the
empty's rotation back to its rest value. Never zero the mesh's rotation after a test: that turns the
surface by the hinge's tilt and leaves it misaligned at rest.

### Propellers

| Node | What it is |
|---|---|
| `staticpropN` | The stopped blades, shown in the viewer and whenever the props are not spinning. |
| `propN`, `propNb` | Two blur discs, the main one and a second layer, shown spinning in flight. |

Put each prop node's origin on the hub and give it **no rotation**. A disc spins about its node's own
nose axis (glTF local Z, Blender local Y), so model the disc flat in the node's Blender XZ plane,
facing forward. A rotated prop node tumbles in flight instead of spinning. Give the discs a
semi-transparent texture and an alpha-blended material.

### Pilot

`pilot_pos > pilot > body > head`, with the head as its own mesh under `head`. A see-through canopy
shows it.

### Markers

Markers are empties under `markers`. Only their positions are read.

| Marker | Place it |
|---|---|
| `cockpit_camera` | The pilot's eye. The cockpit interior is mounted here. |
| `firepoint1`..`firepoint8` | Gun muzzles, odd numbers on the left (port) side. Gun slot Wn fires from `firepoint(9-2n)` and `firepoint(10-2n)`: W1 from 7 and 8, W4 from 1 and 2. |
| `pylon1`..`pylon8` | Bomb and rocket hardpoints, odd numbers on the left, outboard first. |
| `exhaust1`, `exhaust2` | Engine exhaust puffs. |
| `ground_level`, `target` | As in the parent's export: `ground_level` under the plane's lowest point, `target` near its centre. |

Every marker the parent's weapon fit names must exist, or the plane flies unarmed and the log says
`loadout bind failed`. Copying the parent's marker set and moving each empty onto your model is the
safe route.

## 4. The cockpit

### Borrowing a stock cockpit

Without your own interior the plane mounts a stock one at `cockpit_camera`: the parent's, or the one
`plane.json`'s `cockpit` key names (`"cockpit": "player_warhawk"`). Fly it in cockpit view
(`CSVM.exe -- --fly --plane=my_plane --view=cockpit`) and move `cockpit_camera` until the stock panel
sits right; check that your canopy frame does not cover the gunsight.

### Building your own cockpit

An interior of your own is a `cockpit1` node directly under the mod's root. When the model has one,
the `cockpit` key is not used. The game finds the instruments by name, so the reliable start is the
parent's interior: the `--export-gltf` run in step 1 writes it beside the airframe as `cockpit1.glb`,
with every gauge, lamp and state node in place. Import it into the mod's scene and reshape it from
there. The mod ships no separate `cockpit1.glb`: the interior is exported inside `plane.glb` with the
rest of the plane.

| Item | Rule |
|---|---|
| Place in the tree | `my_plane > cockpit1 > ...`, a sibling of `geometry` and `markers`. |
| Units | Interior units, about 25 to the metre, never metres: a 1 m wide panel is 25 Blender units wide. The game draws the whole interior at 0.04 scale. |
| Frame | The pilot's eye is at `cockpit1`'s origin, looking forward (Blender +Y, glTF -Z). The game mounts that origin at `cockpit_camera`. |
| `cockpit1`'s own transform | Ignored. To preview the interior in place in Blender, move `cockpit1` to `cockpit_camera` and scale it to 0.04; the export keeps that transform and the game drops it. |
| Instruments | Found by the stock node names (`gauges`, `speed`, `pfhorizon`, `lowalt_on` and the rest the export holds). Keep each name on the part it drives; a renamed needle stops moving. |
| Belt lights and damage zones | Found by the stock texture names (`ggindicator0` is a belt light). Keep those surfaces' materials named after the texture. |
| Readouts | A character readout (`4char_ammo`, `6char_type`) needs one surface per character, ordered left to right. |

A material named after an original texture wears that texture from the player's own game files, so
the mod does not ship it. Model the shapes yourself and keep the stock names; the original's art then
reaches your panel without being in your files.

Under a VR headset two more rules apply:

- **Head plate:** the flat head-and-shoulders plate behind the eye is hidden so a leaning head never
  sees it from the front. Keep it flat, facing aft and untextured, or it draws.
- **Gunsight:** the collimated reticle draws only on glass whose material is named after the stock
  glass texture (`cockpit6`, the Warhawk's `crosshair`). Keep that glass flat, with its UVs laid flat
  over it.

To test, fly in cockpit view and check every gauge moves (speed, altitude, horizon, ammo, damage
lights); with a headset, also look over your shoulder and through the gunsight. The exact rules are
in the reference, [formats/mod-planes.md](../formats/mod-planes.md) (`plane.glb`, Cockpit) and
[formats/model-packs.md](../formats/model-packs.md) (What a file must hold).

## 5. Materials and textures

| Item | Rule |
|---|---|
| UVs | Unwrap everything a texture should reach. One atlas for the whole body keeps the livery files simple. |
| Names | Give your materials your own names (`my_plane`, `my_glass`). A material named after an original game texture (`blo_wing`) wears that texture instead of yours. |
| Glass | A base colour with alpha below 1 and an alpha-blended material. |
| Embedding | The `.glb` carries its textures; there is nothing to copy beside it. |

### Liveries

So the game's paint schemes reach your plane, ship a mask and a detail map in the UVs of the body
texture, and list the materials they apply to:

```json
"tint": { "mask": "my_mask.png", "detail": "my_detail.png", "materials": ["my_plane"] }
```

- **Mask:** red is the scheme's main body colour, green its dark trim, blue its light trim. Paint the
  body red, stripes and wing tips green, small accents (spinners, rings) blue. A texel left black keeps
  the base texture.
- **Detail map:** greyscale. Grey 128 shows the scheme's colour unchanged, darker shades it. Bake panel
  lines and grime into it so they survive every repaint.
- Leave glass, blur discs and anything with its own UVs out of `materials`.

### Decals

The scheme's emblems land on surfaces whose material name ends in `_winglogo`, `_taillogo` or
`_noselogo` (`my_plane_winglogo`). Each surface shows the whole decal across its 0..1 UV square.

1. Model a square of faces where the emblem goes and follow the skin's curve, lifted 5 to 10 mm off
   it. Make it part of the wing or fin mesh, as an extra material slot after the body material, so it
   draws after the body and stays in that part's damage zone.
2. Lay its UVs over the full 0..1 square. UV up (v = 1) is the top of the emblem; mirror the u axis on
   the faces you see from the other side (the left face of a fin) so the emblem does not read
   reversed.
3. Give the material a transparent cut-out: in Blender, an Image Texture node with any small
   placeholder image, its Alpha through a Math node set to Round into the Principled BSDF's Alpha. The
   exporter writes this as `alphaMode` `MASK`. The game swaps the placeholder for the scheme's decal.
4. An emblem that crosses a hinge (fin onto rudder) goes in two pieces: the fixed part in the fin
   mesh, the rest in the rudder mesh, so it moves with the surface.

One material can serve several squares: one `_winglogo` material covers both wings.

## 6. Damage

Nothing is required. A model without wreck pieces breaks into four pieces cut from its own meshes by
zone, and fires burn at each zone's centre. For hand-made wreckage or torn-skin panels, use the stock
`destroyed > piece1..4` and `pdpN` / `pdpN_h` names (reference, Damage).

## 7. Export

Select the root and every object below it, then File > Export > glTF 2.0:

| Option | Setting |
|---|---|
| Format | glTF Binary (`.glb`) |
| Include | Selected Objects, Active Scene |
| Transform | +Y Up |
| Mesh | Apply Modifiers, UVs, Normals |
| Animation | Off |

Save it as `plane.glb` in the mod folder. Exporting from the active scene only keeps a stray object
from another scene (Blender's default cube) out of the file. The same export from Python:

```python
bpy.ops.export_scene.gltf(filepath=r"C:\...\Mods\Planes\my_plane\plane.glb", export_format='GLB',
    use_selection=True, use_active_scene=True, export_yup=True, export_apply=True,
    export_animations=False)
```

## 8. Write `plane.json`

A minimal definition:

```json
{
  "name": "My Plane",
  "kind_of": "player_brigand"
}
```

From there add what the plane needs: `overrides` for speed, armour and handling numbers, `"turrets": []`
to turn a parent's turret into forward guns, `loadout` for its default guns and pylons,
`hangar_capacity`, `description` (the plane builder's flavour text), `cockpit`, `sound`, `unlock`,
`portrait` and `tint`. Each key is in the reference.
A key starting with `_` is a comment.

## 9. Test it

```
CSVM.exe -- --viewer --plane=my_plane                    look at it (L opens the livery lab)
CSVM.exe -- --viewer --plane=my_plane --paint=hughes     one livery
CSVM.exe -- --plane=my_plane                             fly it
```

A command-line run reads `Mods` under the data root; `--mods=<folder>` reads another folder. The log
says what loaded:

| Log line | Meaning |
|---|---|
| `mod planes: N loaded, M skipped from <folder>` | The folder was read. |
| `mod plane my_plane skipped: <reason>` | Fix the reason; the mod is left out whole. |
| `HD models: N mesh instance(s) of my_plane drawn from its mod's plane.glb` | The model was built. |
| `loadout bind failed` | A marker the parent's fit names is missing. |
| `override <name> is no def field CSVM reads, ignored` | A misspelt override; the mod still loads. |

In flight, check each of these:

| Symptom | Usual cause |
|---|---|
| A surface swings about the wrong line, or sits crooked at rest | The hinge empty's origin or axis, or a surface mesh whose local transform is not identity. |
| A surface does not move | Its name or its hinge empty's name differs from the stock names. |
| The blur discs tumble | A rotated prop node. |
| The eye is at the plane's centre | `cockpit_camera` is missing. |
| A livery leaves the plane in its own colours | The material is not in `tint.materials`, or the mask is not in the body texture's UVs. |
| A decal shows as a solid square | Its material exported as opaque; check the Round node into Alpha. |
| Guns fire from odd places | Firepoint numbering; odd numbers are on the left. |

## 10. Share it

Zip the mod folder (one top folder inside the zip, or the files at its root) and drop it into
`Mods/Planes/`, or copy the folder as it is. In multiplayer every player needs the host's mods with
identical files: the game compares a hash of each mod's files and refuses the join with the mod named
when they differ.
