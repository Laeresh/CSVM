# The weapon ray's intersection test, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function or address it came
from. No decompiler output is reproduced.

**The question this page answers.** Whether the original's weapon-ray polygon test consults the hit
UV's texture alpha, or a bit in the texture header render-flags word decoded in
[`textures.md`](textures.md), so that a shot passes through the transparent parts of an
alpha-cutout card. **It consults neither.** The test is pure geometry against the polygon's
vertices, gated only by per-node flags. The one place the whole path touches texture space is to
*write* a bullet-hole decal after the hit is already decided.

What sits either side of this page: what is done with a hit once it lands is
[`weaponImpact.md`](weaponImpact.md); the node flags this page gates on are documented as data in
[`../formats/gamez.md`](../formats/gamez.md).

## Function map

| Address | Role |
|---|---|
| `FUN_004c7620` | sets the caller's node-flag exclusion mask (`DAT_0071e8b4`) for the next query |
| `FUN_004cd210` | sets/clears a node's `INTERSECT_SURFACE` bit, how the firing plane excludes itself |
| `FUN_004c8ec0` | the segment query: gather every hit, then keep the nearest |
| `FUN_004c8f70` | walks the world partition grid along the segment |
| `FUN_004c9a00` | the per-node recursion: the flag gates, the LOD/switch descent, the bbox arm |
| `FUN_0055c9c0` | the per-model polygon loop, the ray test proper |
| `FUN_0055d6c0` | ray vs polygon, geometry only |
| `FUN_0055db90` | ray vs polygon, geometry only, plus the hit UV |
| `FUN_005595e0` | stores that UV in `_DAT_00a06fec`/`_DAT_00a06ff0` |
| `FUN_00558f80` | stamps the bullet-hole decal into the texture at that UV |
| `FUN_005ad330` | the hit resolve: the one rule that lets a round pass through a surface |

## The query is a nearest-hit over an array of up to 32 hits

`FUN_004c8ec0` calls `FUN_004c8f70` to fill a caller-supplied array, then walks it and writes the
index of the smallest distance back into the array's first byte. `FUN_004c9a00` refuses to add a
33rd (`0x1f < *DAT_0071e9f0`) and logs `Database intersections array is full`. A record is 44 bytes
at `array + 4 + i*0x2c`: normal at `+0x00`, hit point at `+0x0c`, distance at `+0x1c`, the struck
material pointer at `+0x20`, the same pointer `weaponImpact.md`'s `material + 0x20` soil id is
read off.

Both weapon paths use this one query: the tracer/gun segment stepper (`FUN_005b0cb0`) and the
rocket/flyout stepper (`FUN_005acf60`).

## The per-node gates are node flags, and nothing else

`FUN_004c9a00` returns immediately unless `node+0x24` has **`ACTIVE` (`1<<2`)** and
**`INTERSECT_SURFACE` (`1<<4`)** set, and unless the caller's exclusion mask
(`_DAT_0071e8b4`, written by `FUN_004c7620`) shares no bit with the node's flags. The bit numbers
are confirmed against the GameGen keyword reader: `FUN_004c2610` collects `intersect_off` and
`intersect_bbox` into a struct that `FUN_004c2130` applies, and its first two calls are
`FUN_004cd210` (bit 4) and `FUN_004cd2a0` (bit 5). The same walk fixes `LANDMARK` at bit 7
(`FUN_004cd360`), `CAN_MODIFY` at 16, `CLIP_TO` at 17, `OVERRIDE` at 23 and `ID_ZONE_CHECK` at 24,
which is mech3ax's table exactly.

Three consequences:

- **`INTERSECT_BBOX` (`1<<5`) replaces the polygon test with a bounding-box test.** The node's
  polygons are never reached; `FUN_004cc010` decides, and `FUN_004c7f50` records the hit.
- **A LOD node is descended into for exactly one child** (`FUN_004c9a00` case 7), and which one is
  decided by `ACTIVE` on the level nodes, tested by the parent's child loop. So the original
  intersects the level it is currently drawing, not every level.
- **The shooter excludes itself by clearing its own `INTERSECT_SURFACE`** for the first segment
  (`FUN_005b0cb0` brackets the query with `FUN_004cd210(owner, 0)` / `(owner, 1)`), not by an
  owner comparison inside the test.

The gun/rocket paths pass `0x40000` (bit 18) as the exclusion mask. **No shipped node carries that
bit**: mech3ax's CS node reader asserts every flag outside its known-variable set is absent and the
install round-trips byte-identically, so the mask excludes nothing in practice.

## The aircraft reach the polygon loop, and the bounding-box arm is also a pre-test

**No aircraft node carries `INTERSECT_BBOX`.** Across the twenty-two airframe subtrees in the
planes archive, the eleven flyable `player_*` roots and the eleven AI airframes, every
mesh-bearing node is `intersect_surface: true` except the four `piece1`-`piece4` wreck fragments,
and `intersect_bbox` is false on every node in the file. The flag is live elsewhere in the same
install: 341 nodes carry it across the eight chapter `gamez` trees, on gun mounts, turret hulls
and destructible `healthy` variants. A round fired at an aeroplane therefore runs `FUN_0055c9c0`'s
polygon loop against that aeroplane's own triangles, so the original's aircraft hit geometry is
the model and not a box. Searching `extracted/planes/nodes.json` for `"intersect_bbox": true`
returns nothing, which is the whole of the evidence.

`FUN_004cc010` serves twice inside `FUN_004c9a00`, and only one of the two is the flag's doing.
Before the polygon loop, and before descending a group or a LOD node, the recursion calls it
whenever the node has at least one sibling: the parent passes its own child count down as the
second argument and the gate is `1 < param_2`. A nonzero return there ends that node and its
subtree, so the box is a broadphase reject and never an answer. Only with `INTERSECT_BBOX` set
does the box hit become the answer, recorded by `FUN_004c7f50`. An only child skips the pre-test
and goes straight to its polygons.

## The polygon test reads vertices, never texels

`FUN_0055c9c0` walks the model's polygon array (stride `0x28`), and for each one calls a ray-vs-
polygon routine with the polygon's world-space vertices, its vertex count (`flags & 0x3ff`) and one
flag bit. Nothing in that loop, or in either routine, dereferences a texture, a palette or an image
object.

- `FUN_0055d6c0` builds the Newell normal, rejects on the sign test, solves for the plane crossing
  and runs a point-in-polygon edge walk in the dominant-axis projection. Geometry only.
- `FUN_0055db90` does the same, then additionally interpolates the hit UV from the polygon's
  `uv_coords` and hands it to `FUN_005595e0`. It returns a hit **unconditionally** once the edge
  walk passes; the UV never gates anything.

**Which of the two is called is decided by the material, not by transparency.** The selector is
`*(byte *)(*(int *)poly[5] + 1) & 2`, bit 1 of the material record's flag byte, which is
mech3ax's `MaterialFlags::UNKNOWN` and the `flag` field in extracted `materials.json`. Its other
decoded consumer is `FUN_00558f80`, the bullet-hole stamper, which blits a decal bitmap into the
material's own texture surface at exactly that stored UV and requires the same bit
(plus `TEXTURED`, and `CYCLE` clear). So the bit marks *decal-receiving* materials, and the UV is
computed so a hole can be painted. In C3 it is set on 21 of 483 materials, every one of them an
aircraft or cockpit skin (`bal_fuslage`, `bldhwk_cowling`, `cphead`, `damage1`, …). It is clear on
every cargo-zeppelin material, so on that airship the original does not even compute a hit UV.

**Backface is honoured.** The flag passed through is runtime bit 10 of the polygon flags word, which
is the file's `show_backface`; when it is clear both routines reject a hit whose plane faces away
(`if (dot >= 0) return 0`). A single-sided polygon is solid from its front only. The remake reads
the same flag on the collider rather than at the query, `SceneBuilder.CollidersForMesh` giving each
surface class a two-sided shape and a one-sided one; the sidedness census is in
[`../formats/gotchas.md`](../formats/gotchas.md).

## Only one rule lets a round pass through a surface, and it is the soil id

`FUN_005ad330` is the sole place the nearest hit is discarded in favour of a further one. Its test
is `material + 0x20 == 1`, the **`water`** surface id: when the nearest hit is water the round
throws the water row's animation and then rescans the array for the closest hit whose material is
*not* water. Every other surface stops the round where it is struck. There is no alpha arm, no
transparency arm, and no second chance keyed on anything but that id.

## What this settles for the remake

The original polygon-tests an alpha-cutout card exactly the way it tests a solid wall, so a
see-through truss, fence or tree card is solid to weapon fire in the original too. **A rule that
skips a collision polygon because its texture carries alpha would be a divergence from the
original, not a fix for one**, and would reach every fence, railing and tree card in eight chapters
as well as aircraft terrain collision.

Two real divergences the same decode does expose, neither of them about alpha:

- **LOD selection is static here and dynamic there.** `SceneBuilder` keeps the level whose
  `range.min` is 0 and gives it collision at every distance; the original intersects whichever level
  is drawn. Inside the highest level's band the two agree. Beyond it the original narrows and we do
  not, so we present the wider high-detail geometry where the original presents the coarser one.
- **The water pass-through is unimplemented.** `Projectile` retires on the first hit, so a round that
  clips water in front of a target stops there where the original would carry on to the target.
- **An aircraft is a convex decomposition here and a polygon set there.** `PlaneCollider` gives each
  airframe up to sixteen convex hulls, and a hull bridges the concavities that survive the cut: the
  gap between wing and tailplane, the notch behind a canard, the air either side of a fin. The
  `airframe-collider-hit-rate` suite measures what that costs, firing a raster of rays and a real
  gun burst at each of the eleven airframes from a fixed standoff and counting both instruments.
  The hulls never fall inside the silhouette, so no hit is lost; they present more of it than the
  mesh does, so hits are invented, and the excess is larger head-on than side-on.

  **The four regions partition the airframe, and the tail is bounded on x as well as z.** A tail
  region cut on z alone reaches from wingtip to wingtip, and one convex hull over it bridges the
  whole wing gap, which was most of the head-on excess: the Devastator presented 4.39 times its
  mesh head-on with that region and 1.72 with it bounded and the budget at sixteen. The outboard
  aft geometry the tail gives up is wing region at every z, so the silhouette stays covered and no
  hit is lost. The hull budget is the other half: every hull is a shape the terrain sweep casts
  each physics frame, and the ratios improve sharply up to sixteen hulls and little above it.
  Presented-area ratio by airframe, side-on / head-on: Bloodhawk 1.08 / 1.38, Devastator
  1.08 / 1.72, Fury 1.39 / 1.62, Warhawk 1.01 / 1.51, Hoplite 1.81 / 2.23, Hellhound 1.30 / 2.23,
  Balmoral 1.12 / 1.44, Brigand 1.15 / 1.24, Firebrand 1.15 / 1.81, Kestrel 1.08 / 1.48,
  Peacemaker 1.18 / 1.69. The autogyro is the outlier: its rotor stands clear above the body on a
  mast, and a convex hull over that region bridges the air under the disc whatever the budget.
