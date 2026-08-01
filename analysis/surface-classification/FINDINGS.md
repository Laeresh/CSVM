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

## 2026-07-31 — the area quorum's own bug: a real water polygon can still lose the mesh it's on (`BL-204`)

PT-05 (2026-07-31): C2's turquoise (open) water splashed, its blue (near-shore) water didn't — read
at first as a `BL-041` name-pattern gap (some water texture never matching `classify()`). It wasn't:
every water texture C2 actually uses (`wtr00000`, `srf0001`, `watersquirt`) already matches, and no
other texture family is used for water anywhere in the chapter — extending the patterns would
reclassify nothing, a **measured disproof**, not a finding to build on.

The real mechanism is the area quorum itself. It answers "what does this WHOLE MESH count as", and
a coastal tile is mostly beach/cliff/dock by area with only a fringe of real, non-trivial water
polygons — so the tile loses the vote outright and every polygon on it, water included, reads
`default`. This is structurally the same failure the quorum was built to fix (`BL-041`'s single-vote
mesh 33), just from the other direction: instead of a stray sliver dragging a whole mesh's tag UP to
`water`, a real water area gets dragged DOWN to `default` because the mesh it happens to share with
land is mostly not water.

**Measured, stranded area under the whole-mesh vote** (`census.py`'s per-polygon-split section —
every polygon's own texture area vs. what the area-quorum vote actually reaches):

| chapter | water total area | quorum reached | stranded | buildings total area | quorum reached | stranded |
|---|---|---|---|---|---|---|
| C1 | 37,317,186 | 35,406,462 | 5.1% | 1,041,637 | 903,798 | 13.2% |
| C2 | 54,611,451 | 50,305,379 | 7.9% | 259,537 | 91,785 | 64.6% |
| C4 | 3,801,845 | 3,442,421 | 9.5% | 173,081 | 23,092 | 86.7% |

Water's stranded fraction is the C2 symptom (7.9%, real and player-visible along the coast);
buildings' is far larger in relative terms (up to 86.7% in C4) because small building clusters
sharing a mesh with open terrain are exactly the shape that loses a whole-mesh vote — this was
latent in every install chapter, not something `BL-204`'s report singled out.

**The fix drops the vote, not the classifier.** `ClassifySurface` (the name-pattern function) is
unchanged. `SceneBuilder.CollidersForMesh` now builds one collider PER SURFACE CLASS actually
present in a mesh — each polygon's own texture decides which trimesh it joins — instead of forcing
the whole mesh under one winning tag. A polygon with a merely name-matching but literally zero-area
texture reference (mesh 33's stray `splash` poly, area 0.5 of 38) still contributes nothing, because
its own triangulated area is what a collider is built from — no separate area threshold was needed
to keep that case fixed.

In-engine confirmation (C2, `--collision=show` overlay, `--det`): water colliders 75→104,
buildings colliders 35→80, and the coastal fringe that used to draw as plain unclassified terrain
now draws in the water/buildings wireframe colour matching what it visually is. `RunTests.ps1`
green throughout (312 units, 12/12 suites, 13/13 goldens hash-identical — collision never touches
a rendered pixel).

## 2026-08-01 — the film-set skyscrapers never classified `buildings` (`BL-203` buildings half)

A square-on `--det` fire probe at C2's `nycity` towers logged `-> Default` on `nycity/col`: the
landmark tower walls texture as `empire1`/`chrysler1`/`chrysler2`, which match no name pattern, so
under the per-polygon split their polygons join the default trimesh — and a gun hit there showed
the dirt stand-in, not the buildings binding. Measured before widening (the `BL-041` gate): those
are the **only 3 matching textures install-wide** (C2 + C5, nothing else contains either
substring), moving one model per chapter — C2 `nycity` 22,694 area units (empire1 15,344 +
chrysler2 6,221 + chrysler1 1,129), C5 53,652 (chrysler2 45,996 + chrysler1 7,656) — from default
to `buildings`; nothing else can drift. `classify()` here and `SceneBuilder.ClassifySurface` both
gained `empire*`/`chrysler*`. Post-change the same probe logs 8/8 `-> Buildings` on
`nycity/col_buildings`. Still-unclassified residual on `nycity`, accepted as before: `tankerdeck`
(a ship deck), `aphagar01/05`, `woodsupport*`, `oldroad1`, signage (~18 k area total).
