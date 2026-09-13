# What the original applies to an `fvol` cloud sprite, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`). Every claim names the function or the shipped byte it
came from. No decompiler output is reproduced; the addresses are given so any claim can be
re-checked at source.

This page answers one question: **what does the original apply to a cloud sprite card between its
authored data and the pixel?** The gate that decides whether a card takes the mission sun, and what
the sun does to a lit one, is [`vertexLighting.md`](vertexLighting.md); the reader and the volumes
the cards are scattered through are [`../formats/fogvol.md`](../formats/fogvol.md); the fog colour
and the dome the faded field reveals are [`weather.md`](weather.md).

## The answer in one paragraph

**Nothing touches a cloud card's colour.** The card's authored per-vertex 240 reaches the Direct3D
vertex diffuse unchanged, the texture is a constant-RGB alpha mask, and the only per-card terms the
engine applies are alpha terms. The one that matters is a **draw distance scaled by the viewing
angle**: every sprite carries a pointer to the plane normal of the `fvol` polygon it was scattered
on, and its fade band is multiplied by the cosine between that normal and the direction from the
sprite to the eye. On a horizontal slab that makes the field a disc whose radius is the **geometric
mean of the camera's height above the cards and the sprite's own far distance**, so a camera 118 m
above the deck sees the field end at about 600 m while one 610 m above it sees 1,400 m. Each
sprite's fade band is also drawn per placement, interpolated between the reader's **two**
`far_fade_range` pairs rather than selected from them, so the field's edge is a population-wide
statistical ramp and never a rim.

## Function map

| Address | Role |
|---|---|
| `FUN_0044e010` | Loads `fogvol.zrd` under `srand(0x9b3a9ce2)`, hides every `fvol*` node, and pushes each `clutter` block into the block vector at `DAT_0064ff24`…`DAT_0064ff28` |
| `FUN_0044dc90` | Walks the `fvol` mesh **polygon by polygon** and calls the scatter once per polygon (once per strip triangle for a triangle strip) |
| `FUN_0044e870` | Builds that polygon's plane normal, transforms it to world space and interns it in a shared list (`DAT_0064ff14`); the sprite keeps a pointer to the interned copy |
| `FUN_0044c780` | The scatter: the staggered lattice, the weighted kind draw, the fade band, the perpendicular offset, the perturbation, the scale, and the instance the grid is handed |
| `FUN_0044c310` | Point-in-polygon test in the polygon's own plane, over its edges against its normal |
| `FUN_004db010` | Files the finished instance in the world's clutter grid cell |
| `FUN_0044c1c0` | The per-instance view-angle term: `max(0, dot(polygon normal, unit(eye - sprite)))` |
| `FUN_004d5de0` | The per-instance clutter draw: evaluates the fade, sets the global opacity around the draw, restores it after |
| `FUN_004d6010` | The clutter quadtree walk, which culls a whole subtree on the **unscaled** band |
| `FUN_0054e0e0` | Writes the global opacity `DAT_00a06f98` (default 1.0) |
| `FUN_004d2120` | Writes the clutter distance factor `DAT_0062d170` |
| `FUN_00440750` | Turns the `EffectsLevel` setting into that factor: 1.0, 4.0 or 9.0 |
| `FUN_0043fb50` | Reads `detail.zrd`; `EffectsLevel_HW` is the key that reaches `FUN_00440750` on the hardware path |
| `0x006232c8` | The settings name table: `HIGH` 0, `MEDIUM` 1, `LOW` 2 |
| `FUN_00554550` | The hardware model draw: packs the vertex diffuse and the specular fog factor |
| `FUN_00552020` | The per-model mask the draw keys on (lighting, distance fog, band fog) |
| `FUN_005a0e00` | Device set-up: stage 0 colour op MODULATE of texture by diffuse |
| `0x005a6160` | The transparent-queue drain, which sets the blend states a card is drawn under |

## The colour: the authored 240 and nothing else

`FUN_00554550` fills each vertex's work slot with a colour and then packs it into the Direct3D
diffuse as `(int)(c + 0.5)` clamped to 0..255. Which colour depends only on the submit record's own
lighting flags:

- **no lighting flags at all** (the state a `lighting: false` card is in, since `FUN_00552020`
  returns a zero mask for it): the authored per-vertex colour is **copied** into the work slot. The
  packed diffuse is exactly 240.
- **lighting flags set, per-vertex shading unavailable**: the authored colour is multiplied by one
  flat light triple taken from the first vertex.
- **lighting flags set, per-vertex shading available**: the authored colour is multiplied by that
  vertex's own accumulator, which is what [`vertexLighting.md`](vertexLighting.md) decodes.

There is no material-colour term on a textured polygon (the polygon's own colour triple is used
only when the material is untextured), no per-instance colour, and no distance term. The clutter
draw `FUN_004d5de0` writes a scalar opacity and never a colour.

⚠ **The cloud texture carries no colour variation, so a card has exactly one colour.** Read off the
shipped `cloud1.png`/`cloud2.png` in every tier of C1's texture archives: **every texel is RGB 239**
except the fully transparent ones, which are RGB 0, and the alpha channel carries the whole image
(maximum 254, so no texel is ever fully opaque). The card is an alpha mask on a flat grey. Its
rendered ceiling is therefore one number, `239 x 240/255 = 224.9` in the framebuffer's own space,
and **any brightness a frame shows below that is accumulated alpha over whatever is behind, not a
colour**. A brightness gap on this population cannot be closed by a colour scale.

## The alpha: three terms, one of them the whole far-field story

A card's Direct3D vertex alpha is `0xff` unless the global opacity `DAT_00a06f98` is below 1.0, in
which case it is that opacity times 255, clamped. The fog factor rides the **specular** alpha
instead (`255 - fog x 255`), and `FUN_00552020` only sets the fog mask bits from the model's own
flag word, so a card authored `fog: false` is submitted with a specular alpha of 255 and takes no
fog at any distance. The texture's own alpha is the third term, applied by the device's stage 0
alpha op.

The transparent-queue drain at `0x005a6160` sets, per polygon: shade mode Gouraud, alpha blending
on, z-write off, the stage 0 alpha op to MODULATE (or the material's own stored op when the first
vertex's diffuse is not opaque), and the destination blend to INVSRCALPHA, or to ONE for a polygon
flagged additive. So a card composites the ordinary way, in the framebuffer's own space.

### The fade band is drawn per sprite, not selected per detail level

`FUN_0044c780` draws **one** random `t` per placement and interpolates **both** `far_fade_range`
pairs with it:

```
near = near0 + t * (near1 - near0)
far  = far0  + t * (far1  - far0 )
```

and stores `near^2`, `far^2` and `1/(far^2 - near^2)` in the instance at `+0x34`, `+0x38` and
`+0x3c`. For C1's authored `[[2000, 3000], [3100, 3500]]` that gives a sprite whose fade starts
anywhere in 2,000 to 3,100 m and ends anywhere in 3,000 to 3,500 m, the two correlated through the
one `t`. The pair is **not** a per-detail-level choice and the engine never picks one of the two.

### The view-angle term

Each instance carries the address of a function at `+0x58` and a pointer at `+0x5c`. The scatter
sets them to `FUN_0044c1c0` and to the interned world-space normal of the `fvol` polygon the sprite
was scattered on. `FUN_0044c1c0` returns

```
c = max(0, dot(n, unit(eye - p)))
```

and `FUN_004d5de0` uses its square, with `S = c * c`, `D = DAT_0062d170 * |eye - p|^2`:

| Condition | Result |
|---|---|
| `S * near^2 >= D` | opacity 1, and the global is left alone because the draw skips it above 0.996 |
| `S * far^2 <= D` | the sprite is **not drawn at all**, which includes every sprite with `c = 0` |
| between | `opacity = (far^2 - D/S) / (far^2 - near^2)`, dropped entirely below 0.004 |

On a horizontal polygon (a slab's top face, normal `+Y`) and a camera `dy` above the sprite,
`c = dy / d`, so the conditions collapse to distances in closed form:

```
fully opaque out to   d = sqrt(dy * near) / k^(1/4)
gone past             d = sqrt(dy * far ) / k^(1/4)
```

with `k = DAT_0062d170`. The field is a disc whose radius is the geometric mean of the camera's
height above the cards and the sprite's own far distance, and a sprite in the polygon's own plane or
on its far side is dropped. C1's cards sit at their slab's 1,090.55 m top plus the reader's
`perp_dist_range`, so at `k = 1`:

| Camera altitude | `dy` | opaque out to | gone past |
|---|---|---|---|
| 1,208 m | 118 m | 486 m | 595 to 643 m |
| 1,219 m | 129 m | 508 m | 622 to 672 m |
| 1,698 m | 608 m | 1,103 m | 1,350 to 1,459 m |

The band in the last column is the spread the per-sprite `t` opens, and the perpendicular offset and
the sprite's own position spread it further. That is what makes the field's far edge a deep
statistical ramp rather than an edge: at a grazing pose the population thins out over hundreds of
metres, and past it the surface the frame shows is the fogged deck sheet and the dome.

### The distance factor, and the one setting that moves it

`DAT_0062d170` multiplies the squared distance, so it divides the field's radius by its square root:
1.0 leaves the authored metres alone, 4.0 halves the radius, 9.0 thirds it. `FUN_00440750` writes it
from the `EffectsLevel` setting, which on the hardware path is `detail.zrd`'s `EffectsLevel_HW`:
`HIGH` when `CPU_MHZ >= 600`, `MEDIUM` at 400, `LOW` below. `HIGH` resolves to 0 through the name
table at `0x006232c8`, and 0 is the case that writes 1.0. **Every retail capture this project
measures against is `HIGH`, so the authored metres apply as they stand**; the other two settings
shrink the whole field and are not a faithful target.

The clutter quadtree walk `FUN_004d6010` applies the same squared-distance test to a subtree's
bounding sphere **without** the view-angle term, using the block's own band. It is a conservative
early-out, not a second rule.

## The scatter, which is where the normal comes from

`FUN_0044dc90` iterates the `fvol` mesh's polygons, skipping any polygon flagged `0x800` and
splitting a triangle strip (`0x2000`) into its triangles, and calls `FUN_0044c780` for each. So the
field is scattered **over the volume mesh's faces**, not through its interior. Per polygon:

- the basis is the polygon's own: `U = unit(v1 - v0)`, `V = cross(U, n)`, and the loop runs over the
  polygon's vertices projected onto the two;
- the steps are `distance * sqrt(3) / 2` along `U` and `distance` along `V`, each extent divided
  into a whole number of steps, and **every other row is offset by half a step**, which is a
  staggered lattice rather than a square one;
- each cell's point is tested by `FUN_0044c310` against the polygon's own outline and skipped when
  it falls outside;
- the point is then displaced **along `unit(p - ref)`** by a random draw over `perp_dist_range`,
  where `ref` is a per-volume reference point the caller passes from the volume record's `+0x64`
  (untraced beyond that; for a slab's top face the direction it gives is up near the middle and
  increasingly outward towards the rim), and then perturbed on all three axes independently by up
  to half of one random draw over `perturb_dist_range`;
- the scale is one uniform random draw over `scale_range`, applied to all three axes;
- the kind is drawn by weight twice, once over the blocks and once over a block's nodes.

Every `rand()` above runs inside the fixed-seed window `FUN_0044e010` opens, so the whole field is
the same every launch.

## Where CSVM stands

`FogVolumeClutter` and its generated card shader carry the colour path and the alpha path above.

- **The colour is the authored 240, unscaled.** There is no colour term left in the cloud path:
  the card's own vertex colour reaches the shader as the data authors it, and a brightness gap on
  this population is read as coverage rather than corrected as a colour.
- **The fade band is drawn per sprite.** The scatter draws one `t` per placement and hands it to
  the shader as instance custom data; the shader interpolates both authored `far_fade_range` pairs
  with it and ramps linearly in squared distance, not as a smoothstep.
- **The view-angle term is applied.** Each placement also carries the outward normal it is scaled
  against, packed into the same custom-data slot, and the shader evaluates `c = max(0, dot(n,
  unit(eye − p)))` and the `c²·near²` / `c²·far²` tests above. `DAT_0062d170` is the same global
  the templates clutter already takes from the graphics `EffectsLevel`, so `HIGH` leaves the
  authored metres literal and the two clutter populations cannot drift apart.
- **The card takes no fog in either build**, which is the one place the two agree by construction.

⚠ **The normal is exact only where the volume is a slab.** Our scatter fills a volume's interior by
cells rather than laying the lattice over its faces, so a placement has no face to inherit a normal
from. A top-anchored draw sits on the volume's own top face and takes `+Y`, which is exact for the
four deck chapters' map-spanning slabs; a uniformly filled volume's placement takes the outward
normal of the face it lies nearest, which is a stand-in. C1C's build-up frusta and C5's street
prisms are that case, and only scattering over the authored faces makes their normals the
original's.

⚠ **The field is culled, not merely faded, wherever the angle closes.** A sprite whose face points
away from the eye is dropped outright, which is the original's own rule and is why a slab's field
disappears from below rather than fading out. The map-edge continuation's ring is still bounded at
the largest authored `far`, which stays conservative: the disc's horizontal reach
`sqrt(dy·far − dy²)` peaks at `far/2`.
