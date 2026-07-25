# Why some C2 buildings are water and some water is a building

`SceneBuilder.SurfaceForMesh` tags each collidable mesh `water`, `buildings` or null (terrain) by a
**majority vote over its polygons' material textures**. `ColliderOverlay` colours by that tag, and
`Projectile` picks the impact sound and effect from it — the same metadata, so a mis-tag is not an
overlay bug, it changes what the player hears and sees when they shoot the thing.

`census.py` replicates the classifier over a chapter's extracted gamez and reports how thin each
mesh's winning vote is.

## The mechanism

The vote **skips unclassified polygons entirely** (`if (tag == null) continue;`), so it is not a
majority of the mesh — it is a majority of whatever happened to match a name pattern. A mesh whose
texture is overwhelmingly unrecognised is tagged by however few stray polygons did match, with no
threshold and no notion of "mostly unclassified means unclassified".

## Measured

| chapter | meshes | tagged | tagged on a MINORITY of their own polygons |
|---|---|---|---|
| C1 | 2237 | 109 (51 water, 58 buildings) | **42** |
| C2 | 1765 | 190 (79 water, 111 buildings) | **96 — 51 % of everything tagged** |
| C4 | 2431 | 106 (13 water, 93 buildings) | **70** |

The thinnest votes are single polygons: C2 mesh 33 is tagged **water on 1 of 76 polygons (1.3 %)**,
mesh 829 **buildings on 1 of 24**, mesh 456 **water on 1 of 19**. C1 and C4 carry the same mesh 33
case, so it is shared geometry rather than one bad map.

## Two candidate fixes that were checked and rejected

**The `soil` field is not the answer.** Every `Textured` material carries one, which looks like the
original's own surface classification — but it is almost entirely `Default`: C2 has exactly **one**
`Water` material out of 484, C1 three of 548. The other values (`Mech`, `NoSlip`, `Silt`) are
MechWarrior 3 soil types inherited from mech3ax's origins, not Crimson Skies surfaces. Reading
`soil` instead of texture names would classify essentially nothing.

**The design document does not describe the mechanism.** It confirms the system's *shape* — impact
effect and sound are chosen by what was hit, water gets a rising column and a splash, everything
else ricochets, with `splashsm`/`splashbg` and `ricco1`–`ricco4` named in its sound table — but says
nothing about how the engine determined the surface. Searched for collision-mesh, terrain-database,
per-polygon-flag and material-to-sound wording; nothing. Per the standing trust rule it is
authoritative for shape and never for mechanism, so this is a dead end, not a gap to re-search.

## What a fix has to respect

Whatever replaces the vote has to keep the impact classes the shipped data actually supports —
`Projectile` maps the tag to a sound and an effect model, and `--tex-census` is the instrument for
checking which textures really cover a surface before trusting any new rule.
