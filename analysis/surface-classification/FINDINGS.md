# Why some C2 buildings were water and some water was a building — and the fix

`SceneBuilder.SurfaceForMesh` tags each collidable mesh `water`, `buildings` or null (terrain) from
its polygons' material textures. `ColliderOverlay` colours by that tag, and `Projectile` picks the
impact sound and effect from it — the same metadata, so a mis-tag is not an overlay bug, it changes
what the player hears and sees when they shoot the thing.

`census.py` replicates the shipped classifier over a chapter's extracted gamez and compares it
against the count vote it replaced.

## The original mechanism (the bug)

The old vote counted polygons per tag and **skipped unclassified polygons entirely**, so it was not
a majority of the mesh — it was a majority of whatever happened to match a name pattern. A mesh
whose texture was overwhelmingly unrecognised was tagged by however few stray polygons did match,
with no threshold and no notion of "mostly unclassified means unclassified". The thinnest votes
were single polygons: mesh 33 was tagged **water on 1 of 76 polygons (1.3 %)** — it turned out to
be `puffertexture`, the fire/effect atlas mesh, "water" via its one `splash` polygon (C1, C2 and
C4 all carry it, so it was shared geometry rather than one bad map).

## The shipped rule (2026-07-30)

**Area-weighted vote with a quorum**: each polygon contributes its triangulated area (strip order
for tri_strips, a fan otherwise — matching `EmitPolygon`; a strip's raw index list is not an
outline, WORLD-6) to its texture's class, unclassified polygons abstain but still count toward the
whole, and the winning tag must cover **at least half the mesh's total surface area** or the mesh
stays null/terrain. Area rather than count because area is the static analogue of what
`--tex-census` measures — which texture actually covers the surface — and because it keeps real
buildings a count quorum would drop: C1 mesh 433 (a terminal) is `buildings` on 78 % of its area
but only 16 % of its polygons (few large hangar walls, many small trim polys).

## Measured, before → after

| chapter | meshes | old count vote | tagged on a MINORITY of own polygons | shipped area quorum |
|---|---|---|---|---|
| C1 | 2237 | 109 (51 water, 58 buildings) | **42** | 75 (37 water, 38 buildings) |
| C2 | 1765 | 190 (79 water, 111 buildings) | **96 — 51 % of everything tagged** | 89 (57 water, 32 buildings) |
| C4 | 2431 | 106 (13 water, 93 buildings) | **70** | 39 (11 water, 28 buildings) |

Minority tags are 0 by construction under the quorum, so the evidence is in *what* got
reclassified. Everything dropped to default is a structure or shoreline the old rule mis-tagged:
the `puffertexture` fire mesh (was water), beach/cliff shoreline tiles with a couple of `wtr00000`
polys (was water), the wooden docks and the boardwalk carrying `building4`/`sign_awning` textures
(was water — the "buildings drawn in the water colour" sighting), and cranes, tugboats, water
towers, oil pipes and bridges each tagged by a single `hangar*`/`bld*` poly (was buildings).
Everything kept is the open-water tiles and the real building sets (C2's `nycity` film set wins on
98 %+ of its area).

In-engine confirmation (scripted `--det` dives in C2, `Projectile` impact-log breadcrumbs): open
water tile `g29239` → 8/8 `Water` + `splash1.flt` instanced; `nycity` → 8/8 `Buildings`; the
reclassified dock `g36347` → 8/8 `Default`, no splash.

**Known residual, accepted:** a handful of small huts/houses whose wall textures match no pattern
(`houseside1`, `thatched_wall`) and whose only classified polys are a `*roof*` texture now fall to
default — a plain ricochet instead of the buildings binding. Widening the name patterns is the
documented trap (measure with `--tex-census` first); revisit only if a playtest actually hears it.

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
