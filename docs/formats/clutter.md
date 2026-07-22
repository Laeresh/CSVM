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

**Non-sprite decorations:** C2/C5 city-block templates (`cblock1`, …) carry 3D building
meshes as decorations, not sprite quads — a different placement system the remake skips
(logged once per template).

Trees are collidable in the original (`spruce_destroy` anims exist; destruction is
weapons-era behavior). The map-edge continuation carries the border tiles' clutter along
(see [world-structure.md](world-structure.md)).
