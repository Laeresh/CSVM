# Clutter: `interp.json` boot scripts & clutter templates

Part of the [format documentation](README.md). Covers how the original populates
forests/bushes (and city blocks) without a single placed tree node in the gamez.
Consumed by `CSVM/src/Mech3/Clutter.cs`.

## `interp.json` — the engine boot scripts

`interp.zbd` (extracted by `unzbd cs interp`) is a JSON array of scripts
`{ "name": "support\\<chapter>\\adjust.gw", "lines": [ … ] }` — the engine's boot/setup
command lists. The chapter's `adjust.gw` script registers clutter templates via lines of
the form:

```
AddClutterTemplates terpat02 river1 river2
```

(one or more template names per line; C1 registers `terpat02`, `river1`, `river2`).
The same script loads the templates into the gamez as **parentless, unreferenced
subtrees** — which is why a placed-node walk of the world never sees them, and why
template roots must be looked up among parentless Object3d nodes by name (the world also
contains unrelated same-named leaf nodes).

## Template subtree shape

```
templateRoot (parentless Object3d, e.g. terpat02)
└─ ground node — first descendant with a mesh: a flat quad whose
   │             • texture names the terrain texture it decorates
   │             •   (terpat02.tif = C1's forest texture)
   │             • quad size = the world-space tiling period
   │             •   (512 m for C1 terpat02; "-128" template variants = 128 m)
   └─ decoration nodes — one local translation each (position within the patch),
      └─ sprite mesh below: a single vertical one-sided quad
         (tree/bush billboard; its texture = the decoration kind, e.g. firtree1.tif)
```

The engine dresses **every world polygon textured with a template's ground texture**
with that template's decorations, repeated at the tiling period. C1's three templates
stamp 9,303 tree/bush sprites. The decorations are single one-sided cards, so the
original must render them as upright (Y-axis) billboards.

**Undecoded:** the original's exact alignment of the pattern to the terrain. The world's
UV tiling is wildly non-uniform on hillsides (256–1280 m per repeat), so UV-space
placement would visibly stretch the clutter with it; the remake tiles each template on a
fixed world-space X/Z grid of its authored period instead (authored density everywhere,
seam-consistent across polygons), planting each decoration at the polygon's interpolated
surface height.

**Sprite vs non-sprite is in the data, not the shape.** A decoration is a billboard card
iff its model carries `model_type: "Facade"` — the original engine's own billboard flag
(see [gamez.md](gamez.md) and the `FacadeMode` axis values). Every tree/bush/palm
decoration in the install is `Facade` + `CylindricalY`, one polygon, four vertices, flat
in local Z; every 3D building decoration is `model_type: "Default"` with 2–27 polygons.
**Do not classify on `facade_mode` alone** — the 3D building decorations carry a *stale*
`CylindricalY` in that field while being `Default`, so the type is the discriminator.

**Non-sprite decorations:** C2's `filmblock*`/`resblock*`/`parklot*` and C5's `cblock*`
city-block templates carry 3D building meshes as decorations, not sprite quads — a
different placement system the remake skips (logged once per template). Their *sprite*
decorations are still placed: C5's `cblock*` templates each ship `lightpole` (CylindricalY)
posts and `poleflare` (SphericalY) glows, 139,388 sprites in total.

## Clutter is not collidable

**Corrected 2026-07-22.** This page previously stated:

> Trees are collidable in the original (`spruce_destroy` anims exist; destruction is
> weapons-era behavior).

**That was a misreading of the data.** The only two `spruce_destroy` strings in the whole
install are `..\data\common\zrdr\planes\spruce_destroy1.zrd` (C2/M01) and
`..\data\common\zrdr\planes\spruce_destroy2.zrd` (C5/M03) — in the **`planes\`** directory.
Reading the files settles it: they define `g_engine*` animations with `prop_part`, `spin` /
`counterspin`, `snd_propstart` and engine puffers. This is the **Spruce Goose**, Howard
Hughes' flying boat and the C2/M01 mission object, whose folder siblings are
`sprucegoose-fly_the_goose`, `free_the_goose`, `goose_cooked` and `spruce_enginedest`.

**There is no spruce-*tree* animation, and no tree-destruction animation of any kind,
anywhere in the install.** The remake therefore gives clutter no collider at all (user
decision, 2026-07-22) — consistent with every other billboard, which is a flat card whose
collider would be a phantom wall wherever the card happens to be facing. The map-edge
continuation carries the border tiles' clutter along, likewise without collision
(see [world-structure.md](world-structure.md)).
