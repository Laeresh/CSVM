"""Generates C5's city models: the facade atlas, its window ids texture, and per building kind a
model file with two to four near variants and a far box (city_blender.py holds the conventions).

Each HULLS entry holds measured numbers only: PROFILE is analysis/city-hulls/tiers.py's output
for the kind's main mesh, RGB its area-weighted mean unlit texture colour from census.py (the
texels at or below its UNLIT_LUMA, so the original's painted lit windows are left out; the
grade's target, since the engine lights the windows itself), FAMILY the facade family its
original texture belongs to (city_atlas.py), TURN the degrees the building stands turned in
its mesh frame, GABLES whether its slopes' vertical faces are walls. The kinds of cblock1 to
cblock4 and cblock7 are here; the other districts' hulls join them.

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
        far = cb.far_model(h["profile"], h["family"], gables, h.get("cornice", 0.0))
        for extra in h.get("far_extras", ()):
            extra(far, None)
        nears = [cb.near_model(h["profile"], h["family"], n, np.random.default_rng([zlib.crc32(model.encode()), i]), gables,
                        h.get("cornice", 0.0))
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


main()
