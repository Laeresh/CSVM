"""Generates C5's city models: the facade atlas, its window ids texture, and per building kind a
model file with two to four near variants and a far box (city_blender.py holds the conventions).

Each HULLS entry holds measured numbers only: PROFILE is analysis/city-hulls/tiers.py's output
for the kind's main mesh, RGB its area-weighted mean unlit texture colour from census.py (the
texels at or below its UNLIT_LUMA, so the original's painted lit windows are left out; the
grade's target, since the engine lights the windows itself), FAMILY the facade family its
original texture belongs to (city_atlas.py), TURN the degrees the building stands turned in
its mesh frame, GABLES whether its slopes' vertical faces are walls, BANDS the builders of the
bands a stack of rectangles cannot hold (city_blender.hip_band, pyramids). Every C5 building kind
is here; the det dressing models are not.

  .\\scripts\\MakeVegetationModel.ps1 -Folder city_models -Species c5 -NoRender
"""

import argparse
import os
import sys
import zlib

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402

import city_blender as cb  # noqa: E402

STONE, BRICK, WARM, STEEL, STONE_TILE, BRICK_TILE = 0, 1, 2, 3, 4, 5


def sq(h):
    return [(-h, h, -h, h)]


APEX = [(0.0, 0.0, 0.0, 0.0)]

# The near variants. near0 keeps the hull's shape and dresses its roofs; near1 sets the top floor
# back or reshapes the crown, with other dressing. Kit entries: (kind, (x, z) spot share or none).
# A shape placed 1,000 times or more in the city takes a third variant and one placed 2,000 times
# or more a fourth (the tuples' later entries, analysis/city-hulls/FINDINGS.md "Variants"): a
# set-back top floor, light wells cut into the long sides, or a crown stepped in twice.
WALKUP = (
    dict(parapet=1.1, kit=[("tank", (0.25, 0.7)), ("bulkhead", (0.8, 0.25)), ("vent",), ("vent",)]),
    dict(parapet=0.7, raised=1.0, kit=[("bulkhead", (0.2, 0.3)), ("vent",), ("vent",), ("vent",)]),
    dict(setback=1.5, parapet=0.9, ledge=0.5, kit=[("tank", (0.5, 0.5)), ("vent",), ("vent",)]),
    dict(notch=[("z-", 8.0, 2.5), ("z+", 8.0, 2.5)], parapet=1.2,
         kit=[("bulkhead", (0.2, 0.5)), ("tank", (0.8, 0.5)), ("vent",), ("vent",)]),
)
TENEMENT = (
    dict(parapet=1.0, kit=[("chimney", (0.05, 0.5)), ("chimney", (0.95, 0.5)), ("tank", (0.6, 0.65)), ("bulkhead", (0.2, 0.2))]),
    dict(parapet=0.6, raised=1.2, kit=[("chimney", (0.5, 0.05)), ("chimney", (0.5, 0.95)), ("chimney", (0.05, 0.3)),
                                       ("bulkhead", (0.75, 0.6)), ("vent",)]),
    dict(notch=[("z-", 8.0, 2.5)], parapet=0.8, kit=[("chimney", (0.05, 0.95)), ("chimney", (0.5, 0.95)),
                                                     ("chimney", (0.95, 0.95)), ("tank", (0.5, 0.35)), ("vent",)]),
)
TOWER = (
    dict(parapet=1.2, ledge=0.9, kit=[("machine", (0.5, 0.45)), ("tank", (0.15, 0.85)), ("vent",)], ledge_kit=[("vent",)]),
    dict(setback=2.0, parapet=0.8, ledge=1.0, kit=[("tank", (0.75, 0.3)), ("bulkhead", (0.2, 0.75)), ("vent",), ("vent",)]),
    dict(steps=[(8.0, 1.25), (4.0, 2.5)], parapet=0.7, ledge=0.6, kit=[("machine", (0.5, 0.5)), ("vent",)],
         ledge_kit=[("tank",)]),
)


def chimneys_on_hip(mesh, planes):
    """Two chimneys through cb01d's hipped roof, 1.4 m above the slope where they stand."""
    for x, z in ((-8.0, 4.0), (8.0, -5.0)):
        cb.box(mesh, x - 0.4, x + 0.4, z - 0.6, z + 0.6, 20.0, 23.4)


def mast(mesh, planes):
    """A short mast on cb05a's crown apex."""
    cb.box(mesh, -0.2, 0.2, -0.2, 0.2, 58.0, 62.4)


def finial(mesh, planes):
    cb.box(mesh, -0.6, 0.6, -0.6, 0.6, 58.0, 60.2)


# cb04a's further mesh: a thin fin on each corner's diagonal over its upper slope, as (distance
# from the axis along the diagonal, y), measured; the decoration's motif.
FIN = ((12.25, 64.08), (11.67, 64.91), (10.95, 64.72), (7.82, 69.31), (7.82, 68.12))


def fins_cb04a(mesh, planes):
    """The four corner fins, each drawn from both sides."""
    for sx, sz in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        p = [(r * sx, y, r * sz) for r, y in FIN]
        side = cb.np.array((sx, 0.0, -sz))
        for a, b, c in ((0, 1, 2), (2, 1, 3), (2, 3, 4)):
            tri = [p[a], p[b], p[c]]
            uvs = [(q[0] / cb.TW, q[1] / cb.TW) for q in tri]
            mesh.face(tri, uvs, cb.ROOF, side)
            mesh.face(tri, uvs, cb.ROOF, -side)


HIP = (
    dict(extras=[chimneys_on_hip]),
    dict(crown=[(23.0, sq(4.0))], parapet=0.4, kit=[("bulkhead", (0.5, 0.5))]),
)
DECO_CROWN = (
    dict(extras=[mast]),
    dict(crown=[(56.0, sq(7.5)), (57.0, sq(7.5)), (57.0, sq(3.6)), (58.0, sq(3.6))], parapet=0.5, ledge=0.5,
         extras=[finial]),
)

def posts(mesh, spots, y0, y1, w=0.6):
    """Light metal posts of w metres square, centred on spots (x, z), their ends hidden."""
    for x, z in spots:
        cb.box(mesh, x - w / 2, x + w / 2, z - w / 2, z + w / 2, y0, y1, top=False, cls=cb.METAL)


def deck(mesh, h, y0, y1):
    """A thin slab over the square of half side h: light metal edges, roof on top and underneath
    (the original's slabs read dark from below)."""
    cb.box(mesh, -h, h, -h, h, y0, y1, top=False, cls=cb.METAL)
    cb.cap(mesh, [(-h, h, -h, h)], y1)
    pts = [(-h, y0, -h), (h, y0, -h), (h, y0, h), (-h, y0, h)]
    mesh.face(pts, [(p[0] / cb.TW, p[2] / cb.TW) for p in pts], cb.ROOF, (0, -1, 0))


# cb07b's open top, measured: four corner posts of light metal (angles 0.73 m a leg) from the top
# tier's roof at 64 m to 73.58 m, carrying two 0.19 m slabs over the whole 16 m tier, at 68.6 and
# 73.39 m, open between the posts.
CORNERS7 = [(sx * 7.7, sz * 7.7) for sx in (-1, 1) for sz in (-1, 1)]


def canopy(mesh, planes):
    """The original's reading: the two slabs on the corner posts."""
    posts(mesh, CORNERS7, 64.0, 73.39)
    deck(mesh, 8.0, 68.6, 68.79)
    deck(mesh, 8.0, 73.39, 73.58)


def landing(mesh, planes):
    """A landing deck: the top slab alone on posts at the corners and mid edges, a rail round it."""
    posts(mesh, CORNERS7 + [(0.0, -7.7), (0.0, 7.7), (-7.7, 0.0), (7.7, 0.0)], 64.0, 73.39)
    deck(mesh, 8.0, 73.39, 73.58)
    posts(mesh, CORNERS7, 73.58, 74.6, w=0.12)
    for x0, x1, z0, z1 in ((-7.76, 7.76, -7.76, -7.64), (-7.76, 7.76, 7.64, 7.76),
                           (-7.76, -7.64, -7.64, 7.64), (7.64, 7.76, -7.64, 7.64)):
        cb.box(mesh, x0, x1, z0, z1, 74.6, 74.7, cls=cb.METAL)


def mast_cb09(mesh, planes):
    """A mast through cb09's pyramid apex at 108 m."""
    cb.box(mesh, -0.25, 0.25, -0.25, 0.25, 106.0, 111.5)


def finial_cb09(mesh, planes):
    cb.box(mesh, -0.4, 0.4, -0.4, 0.4, 105.5, 110.8)


FRAME = (
    dict(parapet=0.0, ledge=0.9, kit=[("bulkhead", (0.25, 0.25))], ledge_kit=[("vent",)], extras=[canopy]),
    dict(parapet=0.0, ledge=0.5, kit=[("machine", (0.5, 0.5))], extras=[landing]),
)
# cb09's pyramid: a mast on it, or a crown stepped in three tiers under its slope.
SPIRE = (
    dict(extras=[mast_cb09]),
    dict(crown=[(99, sq(8)), (99, sq(6)), (102, sq(6)), (102, sq(3.5)), (105.5, sq(3.5))], parapet=0.5, ledge=0.4,
         extras=[finial_cb09]),
)
# cb10a's shed top rising to 108 m on its +z edge: kept, or built as four floors stepping up under it.
SHED = (
    TOWER[0],
    dict(crown=[(95, sq(8)), (95, [(-8, 8, -4, 8)]), (99, [(-8, 8, -4, 8)]), (99, [(-8, 8, 0, 8)]),
                (103, [(-8, 8, 0, 8)]), (103, [(-8, 8, 4, 8)]), (107, [(-8, 8, 4, 8)])],
         parapet=0.5, ledge=0.5, kit=[("vent",)], ledge_kit=[("vent",)]),
)

# ---- the walk-ups of cblock4 and cblock7 (cb12 to cb16)

def awning(axis, sign, coord, s0, s1, y_wall, y_edge, depth):
    """A street awning on the wall plane x = coord (axis 0) or z = coord (axis 2), sloping from
    y_wall at the wall to y_edge depth metres out, over s0..s1 along the wall: a sloped top, a flat
    underside, a fascia and two ends."""
    s0, s1 = min(s0, s1), max(s0, s1)

    def at(s, y, d):
        return (s, y, d) if axis == 2 else (d, y, s)

    def build(mesh, planes):
        out, lip = coord + sign * depth, y_edge - 0.3
        up = cb.np.zeros(3)
        up[axis] = sign
        faces = [([at(s0, y_wall, coord), at(s1, y_wall, coord), at(s1, y_edge, out), at(s0, y_edge, out)], up + (0, 1, 0)),
                 ([at(s0, lip, coord), at(s1, lip, coord), at(s1, lip, out), at(s0, lip, out)], (0, -1, 0)),
                 ([at(s0, lip, out), at(s1, lip, out), at(s1, y_edge, out), at(s0, y_edge, out)], up)]
        for s, d in ((s0, -1), (s1, 1)):
            side = cb.np.zeros(3)
            side[2 - axis] = d
            faces.append(([at(s, lip, coord), at(s, lip, out), at(s, y_edge, out), at(s, y_wall, coord)], side))
        for pts, n in faces:
            mesh.face(pts, [((p[0] + p[2]) / cb.TW, (p[1] + abs(p[axis])) / cb.TW) for p in pts], cb.ROOF,
                      cb.np.array(n, float))
    return build


def awnings(axis, half, spans, y_wall, y_edge, depth):
    """Awnings on both walls x (axis 0) or z (axis 2) = +-half, over each span (s0, s1)."""
    return [awning(axis, sign, sign * half, s0, s1, y_wall, y_edge, depth) for sign in (-1, 1) for s0, s1 in spans]


def dormers(axis, front, back, spans, y0, y1):
    """Dormers on both long slopes: a facade front at +-front (axis 0 for x, 2 for z), its sides and
    flat top running back to +-back, over each span (s0, s1) along the eave."""
    def build(mesh, planes):
        for sign in (-1, 1):
            f, b = sign * front, sign * back
            for s0, s1 in spans:
                planes.bay((axis, f, sign), s0, s1)
                cb.wall(mesh, planes, (axis, f, sign, s0, s1), y0, y1)
                for s, d in ((s0, -1), (s1, 1)):
                    pts = [(s, y0, b), (s, y0, f), (s, y1, f), (s, y1, b)] if axis == 2 else \
                          [(b, y0, s), (f, y0, s), (f, y1, s), (b, y1, s)]
                    n = cb.np.zeros(3)
                    n[2 - axis] = d
                    mesh.face(pts, [(p[2 - axis] / cb.TW + p[axis] / cb.TW, p[1] / cb.TW) for p in pts], cb.ROOF, n)
                lo, hi = min(f, b), max(f, b)
                cb.cap(mesh, [(s0, s1, lo, hi) if axis == 2 else (lo, hi, s0, s1)], y1)
    return build


def chimneys(spots, y0, y1, w=0.8, d=1.2):
    """Chimneys of w x d metres centred on spots (x, z), from y0 (inside the roof) to y1."""
    def build(mesh, planes):
        for x, z in spots:
            cb.box(mesh, x - w / 2, x + w / 2, z - d / 2, z + d / 2, y0, y1)
    return build


def skylight(mesh, planes):
    """cb15a's roof light, measured: a pyramid on an 8 m square at 26 m to its apex at 30.83 m."""
    cb.slope(mesh, (-5.44, 2.56, -3.76, 4.24), 26.0, (-1.44, -1.44, 0.24, 0.24), 30.83)


def chimney_cb12b(y0):
    """cb12b's chimney, measured: 2 m square at x 4..6, z -0.58..1.42, up to 22 m, from roof y0."""
    return lambda mesh, planes: cb.box(mesh, 4.0, 6.0, -0.58, 1.42, y0, 22.0)


R12, R13, R14 = (-10, 10, -6, 6), (-6, 6, -10, 10), (-8, 8, -6, 6)
R15, R16 = (-28, 28, -12, 12), (-12, 12, -28, 28)
# The walls run to the cornice's top, which falls in a floor's glass band, so the cornice band reads
# as the solid band; the cornice's 0.2 to 0.55 m flare is left out (inside the margin). The top
# CORNICE_M metres under each roof edge and eave are the cornice class, the originals' bright line
# along their dentil cornice (measured on a night frame).
CORNICE_M = 0.6
P_CB12 = [(0, [R12]), (14, [R12])]
P_CB13 = [(0, [R13]), (26, [R13])]
P_CB14 = [(0, [R14]), (12, [R14]), (17, APEX)]
P_CB14C = [(0, [R14]), (12, [R14]), (17, [(-8, 8, 0, 0)])]
# The awnings, measured: the further mesh g8, 2 m out on the long walls, 4 m at the wall and 2 m at
# the edge (cb12b's 1.6 m higher).
AWN_12A = awnings(2, 6, [(-10, 10)], 4.0, 2.0, 2.0)
AWN_12B = awnings(2, 6, [(-10, 10)], 5.6, 3.6, 2.0)
AWN_13 = awnings(0, 6, [(-10, 10)], 4.0, 2.0, 2.0)
# Light wells 2.5 m deep, above the floors whose awnings would make them deeper than the margin.
WELL12 = cb.exposed([R12], [(-2, 2, 3.5, 6)])
WELLS13 = cb.exposed([R13], [(-6, -3.5, -2, 2), (3.5, 6, -2, 2)])

# cb12a, a three-floor brick walk-up under a flat tile roof, 20 x 12 m: flat with a tank; the top
# floor set back all round; the right third a floor lower; the ends lower round a central attic.
FLAT12A = (
    dict(parapet=0.9, kit=[("tank", (0.3, 0.5)), ("vent",)], extras=AWN_12A),
    dict(profile=[(0, [R12]), (8, [R12]), (8, [cb.inset(R12, 2.0)]), (14, [cb.inset(R12, 2.0)])],
         parapet=0.6, ledge=0.5, kit=[("bulkhead", (0.5, 0.5))], extras=AWN_12A),
    dict(profile=[(0, [R12]), (11.5, [R12]), (11.5, [(-10, 2, -6, 6)]), (14, [(-10, 2, -6, 6)])],
         parapet=1.0, ledge=0.6, kit=[("tank", (0.4, 0.5))], ledge_kit=[("bulkhead",)], extras=AWN_12A),
    dict(profile=[(0, [R12]), (12, [R12]), (12, [(-6, 6, -6, 6)]), (14, [(-6, 6, -6, 6)])],
         parapet=0.7, ledge=0.5, kit=[("chimney", (0.05, 0.5)), ("chimney", (0.95, 0.5))], ledge_kit=[("tank",)],
         extras=AWN_12A),
)
# cb12b, the same block with its tall chimney (kept on every variant): raised parapet panels; a
# light well in the back; the long sides set back over two floors; the left end a floor lower.
FLAT12B = (
    dict(parapet=1.0, raised=0.8, kit=[("bulkhead", (0.15, 0.3))], extras=AWN_12B + [chimney_cb12b(14)]),
    dict(profile=[(0, [R12]), (8, [R12]), (8, WELL12), (14, WELL12)], parapet=0.8, ledge=0.3,
         kit=[("tank", (0.15, 0.4))], extras=AWN_12B + [chimney_cb12b(14)]),
    dict(profile=[(0, [R12]), (8, [R12]), (8, [(-10, 10, -4.5, 4.5)]), (14, [(-10, 10, -4.5, 4.5)])],
         parapet=0.6, ledge=0.7, kit=[("bulkhead", (0.1, 0.5))], extras=AWN_12B + [chimney_cb12b(14)]),
    dict(profile=[(0, [R12]), (11.5, [R12]), (11.5, [(-2, 10, -6, 6)]), (14, [(-2, 10, -6, 6)])],
         parapet=0.9, ledge=0.5, kit=[("bulkhead", (0.1, 0.3))], ledge_kit=[("tank",)],
         extras=AWN_12B + [chimney_cb12b(14)]),
)
# cb13, a tall narrow six-floor walk-up, 12 x 20 m: flat with a tank; the top floor set back; light
# wells in both long sides; the back wing a little lower.
TALL13 = (
    dict(parapet=0.9, kit=[("tank", (0.5, 0.3)), ("bulkhead", (0.5, 0.85))], extras=AWN_13),
    dict(profile=[(0, [R13]), (20, [R13]), (20, [cb.inset(R13, 2.0)]), (26, [cb.inset(R13, 2.0)])],
         parapet=0.6, ledge=0.6, kit=[("tank", (0.5, 0.5))], extras=AWN_13),
    dict(profile=[(0, [R13]), (4, [R13]), (4, WELLS13), (26, WELLS13)], parapet=1.1, ledge=0.3,
         kit=[("bulkhead", (0.5, 0.15)), ("tank", (0.5, 0.85))], extras=AWN_13),
    dict(profile=[(0, [R13]), (23.5, [R13]), (23.5, [(-6, 6, -10, 2)]), (26, [(-6, 6, -10, 2)])],
         parapet=0.8, ledge=0.6, kit=[("chimney", (0.1, 0.1)), ("chimney", (0.9, 0.1))], ledge_kit=[("tank",)],
         extras=AWN_13),
)
# cb14a and cb14b, a three-floor walk-up, 16 x 12 m under a hipped roof rising 5 m to a point.
HIP14 = dict(
    chimneys=dict(extras=[chimneys([(-4.0, 2.5), (4.5, -2.0)], 12.0, 15.9)]),
    deck=dict(profile=[(0, [R14]), (12, [R14]), (14.5, [(-4, 4, -3, 3)])], parapet=0.5,
              kit=[("tank", (0.5, 0.5))]),
    setback=dict(profile=[(0, [R14]), (8, [R14]), (8, [cb.inset(R14, 2.0)]), (12, [cb.inset(R14, 2.0)]), (17, APEX)],
                 ledge=0.5, extras=[chimneys([(2.0, -1.0)], 12.0, 16.9)]),
    dormers=dict(profile=[(0, [R14]), (12, [R14]), (12, [cb.inset(R14, 0.8)]), (17, APEX)], ledge=0.7,
                 extras=[dormers(2, 4.6, 2.0, [(-1.0, 1.0)], 12.0, 15.0)]),
    walk=dict(profile=[(0, [R14]), (12, [R14]), (15.5, [(-2.4, 2.4, -1.8, 1.8)])], parapet=0.4,
              extras=[lambda mesh, planes: cb.box(mesh, -1.0, 1.0, -0.8, 0.8, 15.5, 17.3),
                      chimneys([(-5.0, 0.0)], 12.0, 15.6)]),
    four_dormers=dict(extras=[dormers(2, 5.0, 2.4, [(-3.0, -1.0), (1.0, 3.0)], 12.0, 15.0),
                              chimneys([(0.0, 0.0)], 15.0, 18.2)]),
)
# cb14c, the same block under a gable along its length: the ridge chimney at one end; the ridge cut
# to a flat walk; the long sides set back under the gable; two dormers on each slope.
GABLE14 = (
    dict(extras=[chimneys([(6.8, 0.0)], 12.0, 18.4)]),
    dict(profile=[(0, [R14]), (12, [R14]), (15.5, [(-8, 8, -1.8, 1.8)])], parapet=0.4,
         kit=[("chimney", (0.05, 0.5)), ("chimney", (0.95, 0.5))]),
    dict(profile=[(0, [R14]), (8, [R14]), (8, [(-8, 8, -4.5, 4.5)]), (12, [(-8, 8, -4.5, 4.5)]),
                  (17, [(-8, 8, 0, 0)])], ledge=0.5, extras=[chimneys([(-6.8, 0.0)], 12.0, 18.4)]),
    dict(extras=[dormers(2, 5.0, 2.4, [(-5.0, -3.0), (3.0, 5.0)], 12.0, 15.0), chimneys([(6.8, 0.0)], 12.0, 18.4)]),
)
# cb15a, a long six-floor block, 56 x 24 m, with a roof light; cb15b, five floors with a raised
# middle; cb16a and cb16b, 24 x 56 m blocks of five floors under a hipped roof to a point.
LONG15A = (
    dict(parapet=1.0, raised=1.0, kit=[("tank", (0.12, 0.5)), ("tank", (0.88, 0.3)), ("bulkhead", (0.8, 0.85))],
         extras=[skylight]),
    dict(profile=[(0, [R15]), (20, [R15]), (20, [cb.inset(R15, 2.0)]), (26, [cb.inset(R15, 2.0)])],
         parapet=0.7, ledge=0.6, kit=[("bulkhead", (0.1, 0.5)), ("tank", (0.85, 0.5))], extras=[skylight]),
)
LONG15B = (
    dict(parapet=0.8, ledge=0.9, kit=[("tank", (0.5, 0.5))], ledge_kit=[("bulkhead",), ("vent",)]),
    dict(notch=[("x-", 8.0, 2.5), ("x+", 8.0, 2.5)], parapet=1.0, ledge=0.6,
         kit=[("bulkhead", (0.3, 0.5)), ("tank", (0.75, 0.5))]),
)
# cb16's awnings stand half turned: the two long walls carry them over spans a half turn apart.
AWN_16A = [awning(0, sign, sign * 12, sign * a, sign * b, 4.0, 2.75, 2.13) for sign in (-1, 1)
           for a, b in ((-28, -12), (8, 28))]
AWN_16B = [awning(0, sign, sign * 12, sign * -12, sign * 8, 4.0, 2.75, 2.13) for sign in (-1, 1)] + [
    awning(2, -1, -28, -12, -4.5, 4.0, 2.75, 2.13), awning(2, 1, 28, 4.5, 12, 4.0, 2.75, 2.13)]
HIP16A = (
    dict(extras=AWN_16A + [chimneys([(-6.0, -14.0), (6.0, 12.0)], 22.0, 27.4)]),
    dict(profile=[(0, [R16]), (22, [R16]), (22, [cb.inset(R16, 0.8)]), (30, APEX)], ledge=0.8,
         extras=AWN_16A + [dormers(0, 8.4, 4.3, [(-10.0, -8.0), (8.0, 10.0)], 24.0, 26.9)]),
)
HIP16B = (
    dict(extras=AWN_16B + [chimneys([(-5.0, -10.0), (5.0, 14.0)], 22.0, 25.7)]),
    dict(profile=[(0, [R16]), (16, [R16]), (16, [cb.inset(R16, 1.5)]), (22, [cb.inset(R16, 1.5)]), (26, APEX)],
         ledge=0.6, extras=AWN_16B + [chimneys([(0.0, -8.0)], 22.0, 26.4)]),
)

# ---- the cblock5 and cblock6 kinds (cb17 to cb24), each turned 45 degrees in its mesh frame

def pyramids(pairs, y0, y1):
    """A slope per (lower rectangle, upper rectangle or apex) pair, for twin roofs over one band."""
    return lambda mesh, planes: [cb.slope(mesh, a, y0, b, y1) for a, b in pairs]


def board(axis, coord, s0, s1, y0, y1, fmt, front):
    """A roof billboard, measured: a thin board standing on the roof at x = coord (axis 0) or
    z = coord (axis 2) over s0..s1. The face toward front (+1 or -1 along the axis, 0 for both, as the
    original's) shows a poster of format fmt, whole, picked by the building's seed; another face the
    plank back."""
    def build(mesh, planes):
        pts = [(coord, y0, s0), (coord, y0, s1), (coord, y1, s1), (coord, y1, s0)] if axis == 0 else \
              [(s0, y0, coord), (s1, y0, coord), (s1, y1, coord), (s0, y1, coord)]
        for d in (-1, 1):
            n = cb.np.zeros(3)
            n[axis] = d
            r = [float(cb.np.dot(cb._right(axis, d), p)) for p in pts]
            if front in (0, d):
                uvs = [((t - min(r)) / (max(r) - min(r)), (p[1] - y0) / (y1 - y0)) for t, p in zip(r, pts)]
                mesh.face(pts, uvs, cb.BOARD, n, family=fmt)
            else:
                uvs = [(t / BOARD_TILE[0], p[1] / BOARD_TILE[1]) for t, p in zip(r, pts)]
                mesh.face(pts, uvs, cb.BOARD, n, family=cb.A.BACK)
    return build


# A tiling board cell's size in metres: its texels at the atlas's PX_M.
BOARD_TILE = (cb.A.PORT_TILE[0] / cb.A.PX_M, cb.A.PORT_TILE[1] / cb.A.PX_M)


def brick_gable(mesh, pts, out):
    """A gable end in red brick, the original's: the board class's gable cell, tiled in metres."""
    axis = 0 if abs(out[0]) > 0.5 else 2
    right = cb._right(axis, int(round(out[axis])))
    uvs = [(float(cb.np.dot(right, p)) / BOARD_TILE[0], p[1] / BOARD_TILE[1]) for p in pts]
    mesh.face(pts, uvs, cb.BOARD, out, family=cb.A.GABLE)


def masts(spots, y0, y1, w=0.5):
    return lambda mesh, planes: [cb.box(mesh, x - w / 2, x + w / 2, z - w / 2, z + w / 2, y0, y1) for x, z in spots]


# cb17a, a U of 8 m wings round a court open to +z, walls to 24 m under ridged roofs to 32 m with
# red brick gable ends on the wing ends. The ridge band's corners move in
# along their mitre; the gable corners only across their wing.
U17A = [(-16, -8, -16, 16), (-8, 16, -16, -8), (8, 16, -8, 16)]
OUT17A = [(-16, 16), (-16, -16), (16, -16), (16, 16), (8, 16), (8, -8), (-8, -8), (-8, 16)]
DIR17A = [(1, 0), (1, 1), (-1, 1), (-1, 0), (1, 0), (1, -1), (-1, -1), (-1, 0)]
RIDGE17A = {24: cb.hip_band(OUT17A, DIR17A, 24, 32, 4, brick_gable)}
# The ridges cut to a flat walk 2 m wide at 30 m, a mansard.
WALK17A = [(-13, -11, -13, 16), (-11, 11, -13, -11), (11, 13, -13, 16)]
U17 = (
    dict(extras=[chimneys([(-12, 2.0), (12, 6.0), (0, -12)], 30.5, 33.6)]),
    dict(profile=[(0, U17A), (24, U17A), (30, WALK17A)], bands={24: cb.hip_band(OUT17A, DIR17A, 24, 30, 3, brick_gable)},
         parapet=0.4, extras=[chimneys([(-12, -4.0), (12, -4.0), (-4, -12), (4, -12)], 29.5, 31.6)]),
)
# cb17b, a U round a court open to -z with flat roofs at 24 m and a corner stair tower to 32 m
# under a pyramid to 36 m; the court's back wall is its further mesh.
U17B = [(-16, -8, -16, 16), (-8, 16, 8, 16), (8, 16, -16, 8)]
T17B = (-16, -8, -16, -8)
TOWER17 = (
    dict(ledge=1.0, ledge_kit=[("tank", (0.5, 0.6)), ("bulkhead", (0.5, 0.2)), ("vent",)],
         extras=[masts([(-12, -12)], 35.0, 38.0, 0.3)]),
    dict(crown=[(32, [(-14, -10, -14, -10)]), (35, [(-14, -10, -14, -10)])], parapet=0.5, ledge=0.7,
         ledge_kit=[("bulkhead", (0.5, 0.5)), ("vent",)], extras=[masts([(-12, -12)], 35.0, 38.5, 0.3)]),
)
P_CB18 = [(0, sq(16)), (20, sq(16)), (24, APEX)]
# cb18b: the low hip as is, or its top floor set back under the hip.
HIP18B = (
    HIP[0],
    dict(profile=[(0, sq(16)), (16, sq(16)), (16, sq(14)), (20, sq(14)), (24, APEX)], ledge=0.6,
         extras=[chimneys([(-7.0, 4.0), (7.0, -5.0)], 20.0, 23.0)]),
)
# cb19a, a square with a notch in the middle of each side and the top floor set in. The notch
# backs stand 4 cm in from the top floor's wall above them, so each is a wall plane of its own
# whose 7.94 m fits two bays and keeps its windows (on the top floor's 6-bay grid they were solid).
# Its 25.22 m roof stands at 24.8 m, the foot of a floor's glass, so the far box's walls are not
# split there.
B19A = [(-15.89, -3.97, -15.89, -3.97), (-15.89, -3.97, 3.97, 15.89), (-12.67, 12.67, -3.97, 3.97),
        (-3.97, 15.89, -12.67, -3.97), (-3.97, 15.89, 3.97, 12.67), (3.97, 15.89, -15.89, -12.67),
        (3.97, 15.89, 12.67, 15.89)]
NOTCH19A = (
    TOWER[0],
    dict(crown=[(28.0, sq(12.71)), (28.0, sq(10.71)), (30.09, sq(10.71))], parapet=0.8,
         ledge=0.6, kit=[("tank", (0.7, 0.3)), ("bulkhead", (0.2, 0.7))]),
)
# cb19b, a square with 4 m notches in the middle of each side to 20 m, a hipped skirt rising 4 m
# to the 24 m square of its upper block, and a pyramid to 40 m.
B19B = [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12),
        (4, 16, -16, -12), (4, 16, 12, 16)]
OUT19B = [(-16, -16), (-4, -16), (-4, -12), (4, -12), (4, -16), (16, -16), (16, -4), (12, -4), (12, 4), (16, 4),
          (16, 16), (4, 16), (4, 12), (-4, 12), (-4, 16), (-16, 16), (-16, 4), (-12, 4), (-12, -4), (-16, -4)]
DIR19B = [(1, 1), (0, 1), (0, 0), (0, 0), (0, 1), (-1, 1), (-1, 0), (0, 0), (0, 0), (-1, 0),
          (-1, -1), (0, -1), (0, 0), (0, 0), (0, -1), (1, -1), (1, 0), (0, 0), (0, 0), (1, 0)]
SKIRT19B = {20: cb.hip_band(OUT19B, DIR19B, 20, 24, 4)}
P_CB19B = [(0, B19B), (20, B19B), (24, sq(12)), (32, sq(12)), (40, APEX)]
SKIRT19 = (
    dict(extras=[masts([(0, 0)], 38.5, 43.0, 0.4), chimneys([(-14, -10), (14, 10), (10, -14), (-10, 14)], 20.5, 23.6)]),
    dict(profile=P_CB19B[:4] + [(28, sq(12)), (28, sq(10.5)), (32, sq(10.5)), (37.5, sq(3.28))], parapet=0.5,
         ledge=0.6, kit=[("bulkhead", (0.5, 0.5))]),
)
# cb20a, a podium stepping up at 12, 16 and 20 m round a 20 m tower to 44 m under a pyramid whose
# apex stands off centre, over the podium's inner corner.
T20 = (-4, 16, -4, 16)
P_CB20A = [(0, sq(16)), (12, sq(16)), (12, [(-16, 4, -16, 16), (4, 16, -4, 16)]), (16, [(-16, 4, -16, 16), (4, 16, -4, 16)]),
           (16, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]), (20, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]),
           (20, [T20]), (44, [T20]), (52, [(4, 4, 4, 4)])]
STEP20A = (
    dict(ledge=0.9, ledge_kit=[("tank",), ("vent",)], extras=[masts([(4, 4)], 50.0, 54.5, 0.4)]),
    dict(profile=P_CB20A[:7] + [(40, [T20]), (40, [cb.inset(T20, 2.0)]), (44, [cb.inset(T20, 2.0)]), (52, [(4, 4, 4, 4)])],
         ledge=0.6, ledge_kit=[("bulkhead",), ("vent",)]),
)
# cb20b, the same podium widened round two towers, 24 m and 20 m, each under its own pyramid.
TA20, TB20 = (-28, -4, -28, -4), (-4, 16, -4, 16)
A20B, B20B = (-16, -16, -16, -16), (4, 4, 4, 4)
P_CB20B = [(0, [(-28, 4, -28, 4), (-16, 16, 4, 16), (4, 16, -16, 4)]), (12, [(-28, 4, -28, 4), (-16, 16, 4, 16), (4, 16, -16, 4)]),
           (12, [(-28, 4, -28, -4), (-16, 16, -4, 16)]), (16, [(-28, 4, -28, -4), (-16, 16, -4, 16)]),
           (16, [(-28, -4, -28, -4), (-16, 16, -4, 4), (-4, 4, -16, -4), (-4, 16, 4, 16)]),
           (20, [(-28, -4, -28, -4), (-16, 16, -4, 4), (-4, 4, -16, -4), (-4, 16, 4, 16)]),
           (20, [TA20, TB20]), (44, [TA20, TB20]), (52, [A20B, B20B])]
TWIN20B = {44: pyramids([(TA20, A20B), (TB20, B20B)], 44, 52)}
# Or both pyramids cut at 49 m, five eighths of the way to their apexes, to flat decks.
TOP20B = [(-20.5, -11.5, -20.5, -11.5), (1, 8.5, 1, 8.5)]
TWIN20 = (
    dict(ledge=0.9, ledge_kit=[("tank",), ("vent",)], extras=[masts([(-16, -16), (4, 4)], 50.0, 54.5, 0.4)]),
    dict(profile=P_CB20B[:-1] + [(49, TOP20B)], bands={44: pyramids([(TA20, TOP20B[0]), (TB20, TOP20B[1])], 44, 49)},
         parapet=0.5, ledge=0.7, kit=[("bulkhead", (0.5, 0.5))], ledge_kit=[("vent",)]),
)
# cb22 and cb23, two-floor walk-ups of 16 x 12 m off their lot's centre, walls to the cornice's top
# at 10 m, a street awning on each long wall, and billboards on the roof: cb22a two 12 x 6 m boards
# across, cb22b two 5.9 x 8.86 m, cb23 one 12 x 6 m board along the middle. A variant cuts light
# wells into the ends above the awning floor, clear of the boards.
R22, R23 = (-7.41, 8.59, -4.59, 7.41), (-4.59, 7.41, -8.59, 7.41)
P_CB22, P_CB23 = [(0, [R22]), (10, [R22])], [(0, [R23]), (10, [R23])]
WELLS22 = cb.exposed([R22], [(-7.41, -4.91, -0.59, 3.41), (6.09, 8.59, -0.59, 3.41)])
WELLS23 = cb.exposed([R23], [(-4.59, -2.19, -0.59, 3.41), (5.01, 7.41, -0.59, 3.41)])
AWN_22 = [awning(2, -1, -4.59, -5.22, 4.67, 4.0, 2.0, 2.0), awning(2, 1, 7.41, -5.22, 4.67, 4.0, 2.0, 2.0)]
AWN_23 = [awning(0, -1, -4.59, -4.67, 5.22, 4.0, 2.0, 2.0), awning(0, 1, 7.41, -4.67, 5.22, 4.0, 2.0, 2.0)]
BOARDS_22A = [board(2, -3.0, -5.41, 6.59, 10.0, 16.0, cb.A.LANDSCAPE, -1), board(2, 5.0, -5.41, 6.59, 10.0, 16.0, cb.A.LANDSCAPE, 1)]
BOARDS_22B = [board(2, -2.17, -1.41, 4.49, 10.0, 18.86, cb.A.PORTRAIT, -1), board(2, 5.0, -1.41, 4.49, 10.0, 18.86, cb.A.PORTRAIT, 1)]
BOARDS_23 = [board(0, 1.84, -6.59, 5.41, 10.0, 16.0, cb.A.LANDSCAPE, 0)]


# cb21's light wells stand 2.4 m deep where cb13's are 2.5, so turned onto margin.py's grid they
# read inside the margin.
WELLS21 = dict(TALL13[2], profile=[(0, [R13]), (4, [R13]),
                                   (4, cb.exposed([R13], [(-6, -3.6, -2, 2), (3.6, 6, -2, 2)])),
                                   (26, cb.exposed([R13], [(-6, -3.6, -2, 2), (3.6, 6, -2, 2)]))])


def billboard_set(r, wells, extras):
    return (dict(parapet=0.6, extras=extras),
            dict(profile=[(0, [r]), (4, [r]), (4, wells), (10, wells)], parapet=0.9, ledge=0.4, extras=extras))


# cb24a and cb24b are cb15a and cb15b turned a quarter: 24 x 56 m, the roof light measured on cb24a.
R24 = (-12, 12, -28, 28)


def skylight24(mesh, planes):
    cb.slope(mesh, (-3.76, 4.24, -2.56, 5.44), 26.0, (0.24, 0.24, 1.44, 1.44), 30.83)


LONG24A = (
    dict(parapet=1.0, raised=1.0, kit=[("tank", (0.5, 0.12)), ("tank", (0.3, 0.88)), ("bulkhead", (0.85, 0.8))],
         extras=[skylight24]),
    dict(profile=[(0, [R24]), (20, [R24]), (20, [cb.inset(R24, 2.0)]), (26, [cb.inset(R24, 2.0)])],
         parapet=0.7, ledge=0.6, kit=[("bulkhead", (0.5, 0.1)), ("tank", (0.5, 0.85))], extras=[skylight24]),
)
LONG24B = (
    dict(parapet=0.8, ledge=0.9, kit=[("tank", (0.5, 0.5))], ledge_kit=[("bulkhead",), ("vent",)]),
    dict(notch=[("z-", 8.0, 2.0), ("z+", 8.0, 2.0)], parapet=1.0, ledge=0.6,
         kit=[("bulkhead", (0.5, 0.3)), ("tank", (0.5, 0.75))]),
)

# cb02a's L-steps, measured at 5.3 m, stand on the 4 m grid of its 31.78 m walls, 1.33 m in.
L02 = -15.89 + 5 * 31.78 / 8
# Its set-back and stepped tops stand in less, so they stay inside the margin with the step's 1.33 m.
TOWER02A = (TOWER[0], dict(TOWER[1], setback=1.5), dict(TOWER[2], steps=[(8.0, 0.75), (4.0, 1.5)]))
P_CB00 = [(0, [(-20, 20, -12, 12)]), (16, [(-20, 20, -12, 12)])]
P_CB01 = [(0, sq(16)), (24, sq(16))]
P_CB03 = [
    (0, sq(16)), (36, sq(16)),
    (36, [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12), (4, 16, -16, -12), (4, 16, 12, 16)]),
    (60, [(-16, -4, -16, -4), (-16, -4, 4, 16), (-12, 12, -4, 4), (-4, 16, -12, -4), (-4, 16, 4, 12), (4, 16, -16, -12), (4, 16, 12, 16)]),
    (60, sq(12)), (64, sq(12)),
]
P_CB07 = [(0, sq(16)), (36, sq(16)), (36, sq(12)), (56, sq(12)), (56, sq(8)), (80, sq(8))]
# The two chamfers rise 3.6 m from tier to tier; tiers.py's 12.01 and 8.01 are its extrapolation.
P_CB09 = [(0, sq(16)), (36, sq(16)), (39.6, sq(12)), (84, sq(12)), (87.6, sq(8)), (96, sq(8)), (108, APEX)]
P_CB11 = [(0, sq(16)), (32, sq(16))]

# Family by the original's facade texture: smbldg1 and smbldg2 are grey stone walk-ups, smbldg3
# and smbldg4 brick tenements, bldg1 and bldg4 warm office towers, bldg2 and bldg3 dark ones.
HULLS = {
    "cb00a": dict(family=STONE, rgb=(35, 36, 35), profile=P_CB00, near=WALKUP),      # smbldg1
    "cb00b": dict(family=STONE, rgb=(33, 34, 27), profile=P_CB00, near=WALKUP),      # smbldg2
    "cb01a": dict(family=STONE, rgb=(34, 35, 34), profile=P_CB01, near=WALKUP[:3]),  # smbldg1
    "cb01b": dict(family=STONE, rgb=(31, 33, 24), profile=P_CB01, near=WALKUP[:3]),  # smbldg2
    "cb01c": dict(family=BRICK, rgb=(35, 31, 27), profile=P_CB01, near=TENEMENT[:2]),  # smbldg3
    "cb01d": dict(family=BRICK, rgb=(37, 28, 18), near=HIP,                          # smbldg4
                  profile=[(0, sq(16)), (20, sq(16)), (24, APEX)]),
    "cb02a": dict(family=STEEL, rgb=(31, 32, 42), near=TOWER02A, profile=[           # bldg3
        (0, sq(15.89)), (35, sq(15.89)),
        (35, [(-15.89, 15.89, -15.89, L02), (-L02, 15.89, L02, 15.89)]),
        (40, [(-15.89, 15.89, -15.89, L02), (-L02, 15.89, L02, 15.89)]),
        (40, [(-15.89, L02, -15.89, L02), (-L02, 15.89, L02, 15.89), (L02, 15.89, -L02, L02)]),
        (44.99, [(-15.89, L02, -15.89, L02), (-L02, 15.89, L02, 15.89), (L02, 15.89, -L02, L02)]),
        (44.99, [(-15.89, L02, -15.89, L02)]), (65.18, [(-15.89, L02, -15.89, L02)])]),
    "cb02b": dict(family=WARM, rgb=(36, 32, 27), near=TOWER, profile=[               # bldg4
        (0, sq(16)), (36, sq(16)),
        (36, [(-16, 16, -16, 4), (-4, 16, 4, 16)]), (40, [(-16, 16, -16, 4), (-4, 16, 4, 16)]),
        (40, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]), (44, [(-16, 4, -16, 4), (-4, 16, 4, 16), (4, 16, -4, 4)]),
        (44, [(-16, 4, -16, 4)]), (64, [(-16, 4, -16, 4)])]),
    "cb03a": dict(family=WARM, rgb=(42, 33, 28), profile=P_CB03, near=TOWER[:2]),    # bldg1
    "cb03b": dict(family=STEEL, rgb=(33, 34, 28), profile=P_CB03, near=TOWER[:2]),   # bldg2
    "cb04a": dict(family=STEEL, rgb=(33, 35, 29), near=(                             # bldg2, clock, motif
        dict(TOWER[0], extras=[fins_cb04a]), dict(TOWER[1], extras=[fins_cb04a])),
        profile=[(0, sq(16)), (32, sq(16)), (36, sq(12)), (63.53, sq(12)), (68, sq(8)), (80, sq(8))]),
    "cb04b": dict(family=STEEL, rgb=(36, 34, 41), near=TOWER[:2],                    # bldg3
                  profile=[(0, sq(16)), (16, sq(16)), (20, sq(12)), (60, sq(12)), (64, sq(8)), (80, sq(8))]),
    "cb05a": dict(family=WARM, rgb=(37, 34, 30), near=DECO_CROWN,                    # bldg4, teal crown
                  profile=[(0, sq(11.86)), (56, sq(11.86)), (58.93, APEX)]),
    "cb05b": dict(family=WARM, rgb=(42, 33, 28), near=TOWER[:2],                     # bldg1
                  profile=[(0, sq(12)), (64, sq(12)), (64, sq(8)), (92, sq(8))]),
    # cblock2 and cblock3 (cb06 and cb11 also stand in cblock5 and cblock6). Each stands turned
    # 45 degrees in its mesh frame; its profile is measured turned back (tiers.py --turn=45).
    "cb06a": dict(family=BRICK, rgb=(37, 30, 24), profile=P_CB00, near=TENEMENT, turn=45),   # smbldg5
    "cb06b": dict(family=STONE, rgb=(35, 36, 35), profile=P_CB00, near=WALKUP[:3], turn=45),  # smbldg1
    "cb06c": dict(family=STONE, rgb=(33, 34, 27), profile=P_CB00, near=WALKUP[:3], turn=45),  # smbldg2
    "cb07a": dict(family=WARM, rgb=(42, 33, 28), profile=P_CB07, near=TOWER[:2], turn=45),    # bldg1
    "cb07b": dict(family=WARM, rgb=(36, 33, 28), near=FRAME, turn=45,                         # bldg4, open crown
                  profile=P_CB07[:-1] + [(64, sq(8))], far_extras=[canopy]),
    "cb08a": dict(family=WARM, rgb=(42, 33, 28), profile=P_CB03, near=TOWER[:2], turn=45),    # bldg1
    "cb08b": dict(family=STEEL, rgb=(33, 34, 28), profile=P_CB03, near=TOWER[:2], turn=45),   # bldg2
    "cb09a": dict(family=STEEL, rgb=(33, 35, 29), profile=P_CB09, near=SPIRE, turn=45),       # bldg2
    "cb09b": dict(family=WARM, rgb=(41, 33, 28), profile=P_CB09, near=SPIRE, turn=45),        # bldg1
    "cb10a": dict(family=STEEL, rgb=(31, 32, 42), near=SHED, turn=45, profile=[               # bldg3
        (0, sq(16)), (4, sq(16)), (4, sq(12)), (84, sq(12)), (84, sq(8)), (92, sq(8)), (108, [(-8, 8, 8, 8)])]),
    "cb10b": dict(family=STEEL, rgb=(31, 32, 42), near=TOWER[:2], turn=45, profile=[          # bldg3
        (0, sq(16)), (4, sq(16)), (4, sq(12)), (84, sq(12)), (84, sq(8)), (108, sq(8))]),
    "cb11a": dict(family=STONE, rgb=(33, 34, 33), profile=P_CB11, near=WALKUP[:3], turn=45),  # smbldg1
    "cb11b": dict(family=STONE, rgb=(31, 32, 23), profile=P_CB11, near=WALKUP[:3], turn=45),  # smbldg2
    "cb11c": dict(family=BRICK, rgb=(34, 31, 26), profile=P_CB11, near=TENEMENT, turn=45),    # smbldg3
    # cblock4 and cblock7 (cb12 to cb14 also stand in cblock4), none turned. The awnings, the
    # cornice ring and the further walls are part of their building (analysis/city-hulls/FINDINGS.md).
    "cb12a": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(46, 38, 31), profile=P_CB12, near=FLAT12A),       # smbldg3, roof02
    "cb12b": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(51, 36, 24), profile=P_CB12, near=FLAT12B,        # smbldg4, roof02
                  far_extras=[chimney_cb12b(14)]),
    "cb13a": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(43, 31, 23), profile=P_CB13, near=TALL13),        # smbldg5, roof02
    "cb13b": dict(cornice=CORNICE_M, family=STONE_TILE, rgb=(41, 38, 35), profile=P_CB13, near=TALL13),        # smbldg1, roof02
    "cb14a": dict(family=STONE, rgb=(34, 35, 34), profile=P_CB14, near=tuple(                # smbldg1
        HIP14[k] for k in ("chimneys", "deck", "setback", "dormers"))),
    "cb14b": dict(family=STONE, rgb=(32, 34, 26), profile=P_CB14, near=tuple(                # smbldg2
        HIP14[k] for k in ("walk", "four_dormers", "deck", "setback"))),
    "cb14c": dict(family=BRICK, rgb=(39, 34, 29), profile=P_CB14C, near=GABLE14, gables=True),  # smbldg3
    "cb15a": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(58, 43, 33), profile=[(0, [R15]), (26, [R15])],
                  near=LONG15A, far_extras=[skylight]),                                      # smbldg1/3/5, roof02
    "cb15b": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(59, 44, 35), near=LONG15B,                         # smbldg1/3, roof02
                  profile=[(0, [R15]), (22, [R15]), (22, [(-8, 12, -12, 12)]), (24, [(-8, 12, -12, 12)])]),
    "cb16a": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(54, 42, 34), near=HIP16A,                          # smbldg1/3, roof02
                  profile=[(0, [R16]), (22, [R16]), (30, APEX)]),
    "cb16b": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(54, 41, 32), near=HIP16B,                          # smbldg1/4/5, roof02
                  profile=[(0, [R16]), (22, [R16]), (26, APEX)]),
    # cblock5 and cblock6 (cb06 and cb11 above also stand there), each turned 45 degrees in its
    # mesh frame. The towers carry no cornice line; the walk-ups under tile roofs do.
    "cb17a": dict(family=WARM, rgb=(42, 34, 29), profile=[(0, U17A), (24, U17A), (32, APEX)], bands=RIDGE17A,  # bldg1
                  near=U17, turn=45),
    "cb17b": dict(family=STEEL, rgb=(31, 32, 42), near=TOWER17, turn=45,                                      # bldg3
                  profile=[(0, U17B), (24, U17B), (24, [T17B]), (32, [T17B]), (36, [(-12, -12, -12, -12)])]),
    "cb18a": dict(family=STONE, rgb=(34, 35, 34), profile=P_CB18, near=HIP, turn=45),                         # smbldg1
    "cb18b": dict(family=BRICK, rgb=(37, 29, 24), profile=P_CB18, near=HIP18B, turn=45),                      # smbldg5
    "cb19a": dict(family=WARM, rgb=(36, 32, 27), near=NOTCH19A, turn=45,                                      # bldg4
                  profile=[(0, B19A), (24.8, B19A), (24.8, sq(12.71)), (30.09, sq(12.71))]),
    "cb19b": dict(family=STEEL, rgb=(32, 33, 41), profile=P_CB19B, bands=SKIRT19B, near=SKIRT19, turn=45),     # bldg3
    "cb20a": dict(family=WARM, rgb=(36, 33, 29), profile=P_CB20A, near=STEP20A, turn=45),                     # bldg4
    "cb20b": dict(family=WARM, rgb=(41, 34, 29), profile=P_CB20B, bands=TWIN20B, near=TWIN20, turn=45),       # bldg1
    # cb21a (= cb21b) is cb13's walk-up turned: two variants each, a different pair per model.
    "cb21a": dict(cornice=CORNICE_M, family=STONE_TILE, rgb=(41, 38, 35), profile=P_CB13, turn=45,           # smbldg1, roof02
                  near=(TALL13[0], WELLS21)),
    "cb21b": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(41, 35, 28), profile=P_CB13, turn=45,           # smbldg3, roof02
                  near=(TALL13[1], TALL13[3])),
    "cb22a": dict(cornice=CORNICE_M, family=STONE_TILE, rgb=(51, 45, 42), profile=P_CB22, turn=45,           # smbldg1, roof02
                  near=billboard_set(R22, WELLS22, AWN_22 + BOARDS_22A), far_extras=BOARDS_22A),
    "cb22b": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(52, 38, 28), profile=P_CB22, turn=45,           # smbldg5, roof02
                  near=billboard_set(R22, WELLS22, AWN_22 + BOARDS_22B), far_extras=BOARDS_22B),
    "cb23a": dict(cornice=CORNICE_M, family=STONE_TILE, rgb=(46, 40, 28), profile=P_CB23, turn=45,           # smbldg2, roof02
                  near=billboard_set(R23, WELLS23, AWN_23 + BOARDS_23), far_extras=BOARDS_23),
    "cb23b": dict(cornice=CORNICE_M, family=STONE_TILE, rgb=(48, 44, 42), profile=P_CB23, turn=45,           # smbldg1, roof02
                  near=billboard_set(R23, WELLS23, AWN_23 + BOARDS_23), far_extras=BOARDS_23),
    "cb24a": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(57, 43, 34), profile=[(0, [R24]), (26, [R24])],  # smbldg1/3, roof02
                  near=LONG24A, far_extras=[skylight24], turn=45),
    "cb24b": dict(cornice=CORNICE_M, family=BRICK_TILE, rgb=(59, 44, 35), near=LONG24B, turn=45,             # smbldg1/3, roof02
                  profile=[(0, [R24]), (22, [R24]), (22, [(-12, 12, -8, 12)]), (24, [(-12, 12, -8, 12)])]),
}

# ---- the det dressing: pieces at their measured places, each its original's triangles on the far node. The near
# node adds the undersides of the flat pieces, so a stair or an awning seen from below does not vanish.


def _quad(mesh, pts, uvs, cls, out, near, family=None):
    mesh.face(pts, uvs, cls, out, family)
    if near:
        mesh.face(pts, uvs, cls, -cb.np.asarray(out, float), family)


def _at(axis, s, y, d):
    """A point s along the wall plane and d off axis 0 (x) or 2 (z), at height y."""
    return (d, y, s) if axis == 0 else (s, y, d)


def fire_escape(axis, wall, out, lo, hi, flights, top=False):
    """A fire escape on the wall plane x (axis 0) or z (axis 2) = wall, standing 1 m out (out +1 or -1): flights
    rising 8 m from the lo end to the hi end and landings between them, dark plates of the roof surface as the
    original's are; on the outer face a lattice band
    1.6 m deep along each flight and a 1 m rail over each landing, cut out (board format LATTICE). With top, a
    last landing at the head of the top flight."""
    d0, d1 = wall, wall + out
    n = cb.np.zeros(3)
    n[axis] = out
    span = hi - lo

    def build(mesh, near):
        for k in range(flights):
            y = 8.0 * k
            slope = [_at(axis, lo, y, d0), _at(axis, lo, y, d1), _at(axis, hi, y + 8, d1), _at(axis, hi, y + 8, d0)]
            _quad(mesh, slope, [(p[0] / cb.TW, p[2] / cb.TW) for p in slope], cb.ROOF, (0, 1, 0), near)
            band = [_at(axis, lo, y, d1), _at(axis, lo, y + 1.6, d1), _at(axis, hi - 0.2 * span, y + 8, d1),
                    _at(axis, hi, y + 8, d1)]
            length = (span ** 2 + 64) ** 0.5
            uvs = [(0, 0), (0, 1 / 3), (length * 0.8 / BOARD_TILE[0], 1 / 3), (length / BOARD_TILE[0], 0)]
            mesh.face(band, uvs, cb.BOARD, n, family=cb.A.LATTICE)
            if k:
                land = [_at(axis, lo, y, d0), _at(axis, lo, y, d1), _at(axis, hi, y, d1), _at(axis, hi, y, d0)]
                _quad(mesh, land, [(p[0] / cb.TW, p[2] / cb.TW) for p in land], cb.ROOF, (0, 1, 0), near)
                rail = [_at(axis, lo, y, d1), _at(axis, lo + 0.125 * span, y + 1, d1), _at(axis, hi, y + 1, d1),
                        _at(axis, hi, y, d1)]
                w = abs(span) / BOARD_TILE[0]
                mesh.face(rail, [(0, 0), (w * 0.125, 1 / 3), (w, 1 / 3), (w, 0)], cb.BOARD, n, family=cb.A.LATTICE)
        if top:
            y = 8.0 * flights
            land = [_at(axis, lo, y, d0), _at(axis, lo, y, d1), _at(axis, hi, y, d1), _at(axis, hi, y, d0)]
            _quad(mesh, land, [(p[0] / cb.TW, p[2] / cb.TW) for p in land], cb.ROOF, (0, 1, 0), near)
    return build


def awnings_on(axis, wall, spans, y_wall, y_edge, depth=2.17):
    """A street awning on both walls x (axis 0) or z (axis 2) = +-wall over each span: a slope from y_wall at the
    wall to y_edge depth metres out, and an end triangle at each side, in light metal: the originals are pale."""
    def build(mesh, near):
        for sign in (-1, 1):
            w, e = sign * wall, sign * (wall + depth)
            for s0, s1 in spans:
                top = [_at(axis, s0, y_wall, w), _at(axis, s1, y_wall, w), _at(axis, s1, y_edge, e), _at(axis, s0, y_edge, e)]
                up = cb.np.zeros(3)
                up[axis] = sign
                _quad(mesh, top, [(p[0] / cb.TW, p[2] / cb.TW) for p in top], cb.METAL, up + (0, 1, 0), near)
                for s, side in ((s0, -1), (s1, 1)):
                    tri = [_at(axis, s, y_wall, w), _at(axis, s, y_edge, e), _at(axis, s, y_edge, w)]
                    nn = cb.np.zeros(3)
                    nn[2 - axis] = side
                    _quad(mesh, tri, [((p[0] + p[2]) / cb.TW, p[1] / cb.TW) for p in tri], cb.METAL, nn, near)
    return build


def roof_box(x0, x1, z0, z1, y0, y1):
    """A roof box or a vent stub: four sides and a top, the tower dressing's roof surface."""
    return lambda mesh, near: cb.box(mesh, x0, x1, z0, z1, y0, y1)


def ledge_ring(outer, inner, y0, y1):
    """A ledge round the footprint: a flat ring at y1 between inner and outer, a light metal band under its edge."""
    def build(mesh, near):
        (ox0, ox1, oz0, oz1), (ix0, ix1, iz0, iz1) = outer, inner
        o = [(ox0, oz0), (ox1, oz0), (ox1, oz1), (ox0, oz1)]
        i = [(ix0, iz0), (ix1, iz0), (ix1, iz1), (ix0, iz1)]
        for k in range(4):
            j = (k + 1) % 4
            top = [(o[k][0], y1, o[k][1]), (o[j][0], y1, o[j][1]), (i[j][0], y1, i[j][1]), (i[k][0], y1, i[k][1])]
            _quad(mesh, top, [(p[0] / cb.TW, p[2] / cb.TW) for p in top], cb.ROOF, (0, 1, 0), near)
            band = [(o[k][0], y0, o[k][1]), (o[j][0], y0, o[j][1]), (o[j][0], y1, o[j][1]), (o[k][0], y1, o[k][1])]
            mid = cb.np.array(((o[k][0] + o[j][0]) / 2, 0, (o[k][1] + o[j][1]) / 2))
            mesh.face(band, [((p[0] + p[2]) / cb.TW, p[1] / cb.TW) for p in band], cb.METAL, mid / cb.np.linalg.norm(mid))
    return build


def mast(base, y0, top, y1):
    """A lattice mast: the four faces of a frustum from rectangle base at y0 to rectangle top at y1."""
    def build(mesh, near):
        cb_, ct = ([(r[0], r[2]), (r[1], r[2]), (r[1], r[3]), (r[0], r[3])] for r in (base, top))
        centre = cb.np.array(((base[0] + base[1]) / 2, (y0 + y1) / 2, (base[2] + base[3]) / 2))
        for k in range(4):
            j = (k + 1) % 4
            pts = [(cb_[k][0], y0, cb_[k][1]), (cb_[j][0], y0, cb_[j][1]), (ct[j][0], y1, ct[j][1]), (ct[k][0], y1, ct[k][1])]
            wide = ((cb_[j][0] - cb_[k][0]) ** 2 + (cb_[j][1] - cb_[k][1]) ** 2) ** 0.5
            uvs = [(0, 0), (wide / BOARD_TILE[0], 0), (wide / BOARD_TILE[0], (y1 - y0) / BOARD_TILE[1]), (0, (y1 - y0) / BOARD_TILE[1])]
            mid = cb.np.mean([cb.np.array(p) for p in pts], axis=0)
            mesh.face(pts, uvs, cb.BOARD, mid - centre, family=cb.A.LATTICE)
    return build


# Measured per piece in each decoration's own frame (analysis/city-hulls/FINDINGS.md, "The det dressing"); the
# cb06, cb08 and cb11 dressing stands turned 45 degrees like its building. FAMILY is the building's, RGB the
# original's unlit mean (census.py), the grade of the roof pieces. CUTOUT marks a file with lattice.
AWN_00 = awnings_on(2, 12, [(-18.75, -11.25), (6.25, 13.75)], 4.92, 3.67)
AWN_01 = awnings_on(2, 16, [(-11.75, -4.25), (4.25, 11.75)], 7.12, 5.87)
AWN_02 = awnings_on(2, 15.89, [(-10.56, -3.06), (3.06, 10.56)], 4.92, 3.67)
AWN_03 = awnings_on(0, 15.89, [(-10.56, -3.06), (3.06, 10.56)], 4.92, 3.67)
FE_00X = fire_escape(0, -20, -1, 3.8, -2.2, 2)
FE_00Z = fire_escape(2, 12, 1, -4, -10, 2)
BOX_00 = roof_box(13.04, 18.04, -5.2, 5.8, 16, 17.72)
VENT_00 = roof_box(-15, -13.95, -6.9, -6, 16, 17.05)
RING_00 = ledge_ring((-22, 22, -14, 14), (-20, 20, -12, 12), 3.55, 4)
RING_01 = ledge_ring((-18, 18, -18, 18), (-16, 16, -16, 16), 3.55, 4)
DRESSING = {
    "cb00det01": dict(family=STONE, rgb=(56, 55, 53), pieces=[AWN_00, FE_00X], cutout=True),
    "cb00det02": dict(family=STONE, rgb=(57, 57, 56), pieces=[AWN_00, FE_00Z, VENT_00, BOX_00], cutout=True),
    "cb00det03": dict(family=STONE, rgb=(48, 50, 49), pieces=[FE_00Z, BOX_00, RING_00], cutout=True),
    "cb01det01": dict(family=STONE, rgb=(50, 52, 51), cutout=True, pieces=[
        fire_escape(2, 16, 1, 0, -6, 3), roof_box(9.04, 14.04, 1.77, 12.77, 22.28, 25.72), RING_01]),
    "cb01det02": dict(family=STONE, rgb=(57, 58, 57), cutout=True, pieces=[
        AWN_01, fire_escape(0, 16, 1, 0, 6, 3, top=True), roof_box(9.04, 14.04, 1.77, 12.77, 22.28, 25.72)]),
    "cb02det01": dict(family=STEEL, rgb=(66, 66, 65), pieces=[
        AWN_02, roof_box(2.3, 3.35, -12.59, -11.69, 65.18, 66.23), roof_box(-12.7, -7.7, -4.71, 2.11, 65.18, 66.9)]),
    "cb02det02": dict(family=STEEL, rgb=(50, 52, 52), cutout=True, pieces=[
        roof_box(12.89, 13.94, 10.8, 11.7, 44.99, 46.04), roof_box(-13.7, -8.7, -12.89, -6.06, 65.18, 66.9),
        mast((-0.7, 1.3, -1.89, 0.11), 65.18, (0.05, 0.55, -1.14, -0.64), 69.18)]),
    "cb03det01": dict(family=WARM, rgb=(72, 70, 68), pieces=[AWN_03, roof_box(8.71, 9.76, 8.81, 9.71, 65.18, 66.23)]),
    "cb03det02": dict(family=WARM, rgb=(74, 75, 75), pieces=[roof_box(-8.29, -7.24, -8.19, -7.29, 65.18, 66.23)]),
    "cb06det01": dict(family=STONE, rgb=(48, 50, 49), turn=45, pieces=[FE_00Z, BOX_00, RING_00], cutout=True),
    "cb06det02": dict(family=STONE, rgb=(57, 57, 56), turn=45, pieces=[AWN_00, FE_00Z, VENT_00, BOX_00], cutout=True),
    "cb06det03": dict(family=STONE, rgb=(56, 55, 53), turn=45, pieces=[AWN_00, FE_00X], cutout=True),
    "cb08det01": dict(family=WARM, rgb=(72, 70, 68), turn=45, pieces=[AWN_03, roof_box(7.81, 8.71, 8.66, 9.71, 65.18, 66.23)]),
    "cb08det02": dict(family=WARM, rgb=(74, 75, 75), turn=45, pieces=[roof_box(-8.18, -7.28, -8.67, -7.62, 65.18, 66.23)]),
    "cb11det01": dict(family=STONE, rgb=(46, 48, 46), turn=45, cutout=True, pieces=[
        fire_escape(2, 16, 1, 0, -6, 4), roof_box(9.04, 14.04, 1.77, 12.77, 32, 33.72), RING_01]),
    "cb11det02": dict(family=STONE, rgb=(51, 52, 50), turn=45, cutout=True, pieces=[
        AWN_01, fire_escape(0, 16, 1, 0, 6, 4), roof_box(9.04, 14.04, 1.77, 12.77, 32, 33.72)]),
}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out", default=os.path.dirname(os.path.abspath(__file__)))
    p.add_argument("--only", nargs="*", help="kinds to write (the atlas is always written)")
    # Accepted for the runner's sake: the city's comparison renders are analysis/city-hulls/preview.py's.
    p.add_argument("--render")
    p.add_argument("--data-root")
    p.add_argument("--chapter", default="C5", choices=["C5"])
    return p.parse_args(argv)


def main():
    args = parse_args()
    cb.vb.reset_scene()
    means = cb.write_atlas(args.out, args.chapter)
    for model, h in HULLS.items():
        if args.only and model not in args.only:
            continue
        gables = h.get("gables", False)
        far = cb.far_model(h["profile"], h["family"], gables, h.get("cornice", 0.0), h.get("bands"))
        for extra in h.get("far_extras", ()):
            extra(far, None)
        nears = [cb.near_model(h["profile"], h["family"], n, np.random.default_rng([zlib.crc32(model.encode()), i]), gables,
                        h.get("cornice", 0.0), h.get("bands"))
                 for i, n in enumerate(h["near"])]
        if h.get("turn"):
            for m in nears + [far]:
                cb.turn(m, h["turn"])
        factor = cb.grade(far, h["family"], h["rgb"], means)
        mat = cb.grade_material(model, factor)
        objs = [cb.to_object("near%d" % i, m, h["family"], mat) for i, m in enumerate(nears)]
        objs.append(cb.to_object("far", far, h["family"], mat))
        path = cb.export(objs, args.out, args.chapter, model)
        print("CITY %s: %s, far %d triangles; family %d; grade %.4f %.4f %.4f; %s (%d bytes)" % (
            model, ", ".join("near%d %d" % (i, m.triangles()) for i, m in enumerate(nears)), far.triangles(),
            h["family"], *factor, path, os.path.getsize(path)))
        for o in objs:
            me = o.data
            cb.bpy.data.objects.remove(o)
            cb.bpy.data.meshes.remove(me)
        cb.bpy.data.materials.remove(mat)
    for model, h in DRESSING.items():
        if args.only and model not in args.only:
            continue
        far, near = cb.Mesh(), cb.Mesh()
        for piece in h["pieces"]:
            piece(far, False)
            piece(near, True)
        if h.get("turn"):
            for m in (near, far):
                cb.turn(m, h["turn"])
        factor = cb.grade(far, h["family"], h["rgb"], means)
        # CityModels reads the name's suffix as the flag for its cut-out shader.
        mat = cb.grade_material(model + (".cutout" if h.get("cutout") else ""), factor)
        objs = [cb.to_object("near0", near, h["family"], mat), cb.to_object("far", far, h["family"], mat)]
        path = cb.export(objs, args.out, args.chapter, model)
        print("CITY %s: near0 %d, far %d triangles; family %d; grade %.4f %.4f %.4f; %s (%d bytes)" % (
            model, near.triangles(), far.triangles(), h["family"], *factor, path, os.path.getsize(path)))
        for o in objs:
            me = o.data
            cb.bpy.data.objects.remove(o)
            cb.bpy.data.meshes.remove(me)
        cb.bpy.data.materials.remove(mat)


main()
