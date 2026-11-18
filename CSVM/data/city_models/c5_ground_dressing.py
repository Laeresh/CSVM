"""Generates C5's low block dressing for the Enhanced ground (docs/PLAN-c5-ground.md, B14): the
courtyard walls, fences, box hedges, sheds, parked period cars, ash cans and benches that stand in the free
area of the city blocks. Each kind is a model file like a building's (city_blender.py's
conventions): mesh nodes near0 (near1 where a kind has a second shape on the same footprint) and
far, one material whose baseColorFactor is the grade of its roof-class surfaces, and the facade
atlas's surface classes, so CityModels' shader draws them as it draws the city's det dressing.

Which atlas cell each surface wears, and why:
- Brick walls take the board class's brick gable (format GABLE), fences, shed walls and bench
  slats its plank back (format BACK); both tile at PX_M and are ungraded, so they keep the
  colour the city's gables and board backs have on a night frame.
- Copings, shed roofs, doors, car bodies, iron bench ends, ash cans and hedges are roof class, graded by
  the file's RGB (a target sRGB mean, as c5.py's RGB is for a building) from the family's roof
  cell. The seed's brightness and tint spread then varies them per stamp.
- Car glass and ash can lids are light metal, which the shader takes ungraded, so a car's glass
  house and a can's lid catch the street light over a dark body.
- The delivery truck's box sides are a landscape poster (format LANDSCAPE), which the shader
  picks per stamp from its seed, as a company's painted van side.
No surface is wall class, which would light windows.

Conventions for the stamping half: metres, Y up (glTF's frame), the pivot at the footprint's
centre on the ground, the long axis along X (a car's front at +X, a fence's posts on -Z), every
node unmoved. Every structure stays under HEIGHT_CAP (Decision 5). None of it is solid. The
generator reads no game file and does not write the atlas (c5.py owns facade.png.bin).

  .\\scripts\\MakeVegetationModel.ps1 -Folder city_models -Species c5_ground_dressing -NoRender
"""

import argparse
import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np  # noqa: E402

import city_blender as cb  # noqa: E402

A = cb.A
STONE, STEEL = 0, 3
ROOF, METAL, BOARD = cb.ROOF, cb.METAL, cb.BOARD
# A tiled board's tile in metres (city_atlas's portrait cell at PX_M): 3 x 4.5 m.
BT = (A.PORT_TILE[0] / A.PX_M, A.PORT_TILE[1] / A.PX_M)
# TUNE. Decision 5's structure height cap, checked on every node.
HEIGHT_CAP = 4.0
BRICK, PLANK, POSTER = (BOARD, A.GABLE), (BOARD, A.BACK), (BOARD, A.LANDSCAPE)
TAR, GLASS = (ROOF, None), (METAL, None)


def _uvs(pts, out, scale):
    """A planar map on the face's own axes: along it horizontally, then up it (or x, z on a flat face)."""
    n = np.asarray(out, float)
    n = n / np.linalg.norm(n)
    if abs(n[1]) > 0.99:
        e1, e2 = np.array((1.0, 0, 0)), np.array((0, 0, 1.0))
    else:
        e1 = np.cross((0, 1.0, 0), n)
        e1 /= np.linalg.norm(e1)
        e2 = np.cross(n, e1)
    return [(float(np.dot(p, e1)) / scale[0], float(np.dot(p, e2)) / scale[1]) for p in np.asarray(pts, float)]


def quad(mesh, pts, surf, out, both=False, uvs=None):
    """A face of surface (class, board format or None); both adds the back face, for a thin piece."""
    cls, fam = surf
    uvs = uvs or _uvs(pts, out, BT if cls == BOARD else (cb.TW, cb.TW))
    mesh.face(pts, uvs, cls, out, fam)
    if both:
        mesh.face(pts, uvs, cls, -np.asarray(out, float), fam)


def box(mesh, x0, x1, z0, z1, y0, y1, side=TAR, top=TAR, skip=(), sides=None):
    """A box without a bottom; sides maps a side key (-x, +x, -z, +z) to its own surface."""
    faces = {"-x": ([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], (-1, 0, 0)),
             "+x": ([(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], (1, 0, 0)),
             "-z": ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
             "+z": ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1))}
    for k, (pts, out) in faces.items():
        if k not in skip:
            quad(mesh, pts, (sides or {}).get(k, side), out)
    if top:
        quad(mesh, [(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], top, (0, 1, 0))


def frustum(mesh, lo, y0, hi, y1, side, top=None, skip=()):
    """The sides of a frustum from rectangle lo (x0, x1, z0, z1) at y0 to hi at y1, and hi's top if given."""
    c = [(0, 2), (1, 2), (1, 3), (0, 3)]
    keys = ["-z", "+x", "+z", "-x"]
    mid = np.array(((lo[0] + lo[1]) / 2, (y0 + y1) / 2, (lo[2] + lo[3]) / 2))
    for k in range(4):
        if keys[k] in skip:
            continue
        a, b = c[k], c[(k + 1) % 4]
        pts = [(lo[a[0]], y0, lo[a[1]]), (lo[b[0]], y0, lo[b[1]]), (hi[b[0]], y1, hi[b[1]]), (hi[a[0]], y1, hi[a[1]])]
        out = np.mean(np.asarray(pts, float), 0) - mid
        out[1] = 0.0
        quad(mesh, pts, side, out)
    if top:
        quad(mesh, [(hi[0], y1, hi[2]), (hi[1], y1, hi[2]), (hi[1], y1, hi[3]), (hi[0], y1, hi[3])], top, (0, 1, 0))


# ---- the kinds. Each builder fills one mesh node.

def wall_near(piers):
    """A brick courtyard wall 8 m long and 2.4 m tall with a coping; piers adds three brick piers."""
    def build(mesh):
        box(mesh, -4, 4, -0.175, 0.175, 0, 2.3, BRICK, None)
        box(mesh, -4, 4, -0.225, 0.225, 2.3, 2.4)
        for x in ((-3.7, 0.0, 3.7) if piers else ()):
            box(mesh, x - 0.3, x + 0.3, -0.275, 0.275, 0, 2.6, BRICK)
    return build


def wall_far(mesh):
    for z, s in ((-0.225, -1), (0.225, 1)):
        quad(mesh, [(-4, 0, z), (4, 0, z), (4, 2.4, z), (-4, 2.4, z)], BRICK, (0, 0, s))
    quad(mesh, [(-4, 2.4, -0.225), (4, 2.4, -0.225), (4, 2.4, 0.225), (-4, 2.4, 0.225)], TAR, (0, 1, 0))


def fence_near(mesh):
    """A board fence 8 m long and 1.8 m tall, its posts (tar capped) and two rails on the -Z side."""
    quad(mesh, [(-4, 0, 0), (4, 0, 0), (4, 1.8, 0), (-4, 1.8, 0)], PLANK, (0, 0, 1), both=True)
    for x in (-3.9, 0.0, 3.9):
        box(mesh, x - 0.06, x + 0.06, -0.12, 0, 0, 1.9, PLANK, TAR, skip=("+z",))
    for y in (0.3, 1.45):
        box(mesh, -3.84, 3.84, -0.07, 0, y, y + 0.1, PLANK, PLANK, skip=("+z", "-x", "+x"))


def fence_far(mesh):
    quad(mesh, [(-4, 0, 0), (4, 0, 0), (4, 1.8, 0), (-4, 1.8, 0)], PLANK, (0, 0, 1), both=True)


SHED = (-1.5, 1.5, -1.2, 1.2)


def shed_lean(mesh):
    """A plank shed 3 x 2.4 m under a tar lean-to roof, 2.6 m at the door side (+Z) and 2.15 m behind."""
    x0, x1, z0, z1 = SHED
    lo, hi, o = 2.15, 2.6, 0.2
    y = lambda z: lo + (hi - lo) * (z - z0) / (z1 - z0)
    quad(mesh, [(x0, 0, z1), (x1, 0, z1), (x1, hi, z1), (x0, hi, z1)], PLANK, (0, 0, 1))
    quad(mesh, [(x0, 0, z0), (x1, 0, z0), (x1, lo, z0), (x0, lo, z0)], PLANK, (0, 0, -1))
    for x, s in ((x0, -1), (x1, 1)):
        quad(mesh, [(x, 0, z0), (x, 0, z1), (x, hi, z1), (x, lo, z0)], PLANK, (s, 0, 0))
    roof = [(x0 - o, y(z0 - o) + 0.05, z0 - o), (x1 + o, y(z0 - o) + 0.05, z0 - o),
            (x1 + o, y(z1 + o) + 0.05, z1 + o), (x0 - o, y(z1 + o) + 0.05, z1 + o)]
    quad(mesh, roof, TAR, (0, 1, 0), both=True)
    quad(mesh, [(-0.45, 0, z1 + 0.01), (0.45, 0, z1 + 0.01), (0.45, 2.0, z1 + 0.01), (-0.45, 2.0, z1 + 0.01)], TAR,
         (0, 0, 1))


def shed_gable(mesh):
    """The same footprint under a gabled tar roof, eaves 2.3 m on the long sides and the ridge 3.2 m along X."""
    x0, x1, z0, z1 = SHED
    eave, ridge, o = 2.3, 3.2, 0.2
    for z, s in ((z0, -1), (z1, 1)):
        quad(mesh, [(x0, 0, z), (x1, 0, z), (x1, eave, z), (x0, eave, z)], PLANK, (0, 0, s))
    for x, s in ((x0, -1), (x1, 1)):
        quad(mesh, [(x, 0, z0), (x, 0, z1), (x, eave, z1), (x, ridge, 0.0), (x, eave, z0)], PLANK, (s, 0, 0))
    drop = (ridge - eave) / z1 * o
    for z, s in ((z0, -1), (z1, 1)):
        ze = z + s * o
        pts = [(x0 - o, eave - drop, ze), (x1 + o, eave - drop, ze), (x1 + o, ridge, 0.0), (x0 - o, ridge, 0.0)]
        quad(mesh, pts, TAR, (0, z1, s * (ridge - eave)), both=True)
    quad(mesh, [(x1 + 0.01, 0, -0.45), (x1 + 0.01, 0, 0.45), (x1 + 0.01, 2.0, 0.45), (x1 + 0.01, 2.0, -0.45)], TAR,
         (1, 0, 0))


def shed_far(mesh):
    x0, x1, z0, z1 = SHED
    lo, hi = 2.2, 2.65
    quad(mesh, [(x0, 0, z1), (x1, 0, z1), (x1, hi, z1), (x0, hi, z1)], PLANK, (0, 0, 1))
    quad(mesh, [(x0, 0, z0), (x1, 0, z0), (x1, lo, z0), (x0, lo, z0)], PLANK, (0, 0, -1))
    for x, s in ((x0, -1), (x1, 1)):
        quad(mesh, [(x, 0, z0), (x, 0, z1), (x, hi, z1), (x, lo, z0)], PLANK, (s, 0, 0))
    quad(mesh, [(x0, lo, z0), (x1, lo, z0), (x1, hi, z1), (x0, hi, z1)], TAR, (0, 1, 0))


def axles(mesh, xs, r, half):
    """Each axle's pair of wheels as one dark box under the body, its outer sides only: under the chassis its front
    and back show only as a sliver of ground seen through."""
    for x in xs:
        box(mesh, x - r, x + r, -half, half, 0, 2 * r * 0.95, top=None, skip=("-x", "+x"))


def sedan_near(mesh):
    """A late-1920s sedan, 4.3 x 1.72 x 1.7 m: a body to the belt line with the hood at its height, fenders and
    running boards wider and lower, an upright glass house and a roof slab."""
    box(mesh, -2.15, 2.15, -0.86, 0.86, 0.30, 0.62)
    box(mesh, -2.0, 2.05, -0.72, 0.72, 0.62, 1.08)
    frustum(mesh, (-1.8, 0.75, -0.70, 0.70), 1.08, (-1.65, 0.55, -0.64, 0.64), 1.58, GLASS)
    box(mesh, -1.72, 0.62, -0.68, 0.68, 1.58, 1.70)
    axles(mesh, (-1.35, 1.45), 0.34, 0.80)


def sedan_far(mesh):
    box(mesh, -2.15, 2.15, -0.86, 0.86, 0.30, 1.08)
    box(mesh, -1.75, 0.7, -0.68, 0.68, 1.08, 1.70)


def _poster_sides(mesh, x0, x1, z, y0, y1):
    """The box's long sides as one poster each, read left to right from outside."""
    for s in (-1, 1):
        pts = [(x0, y0, s * z), (x1, y0, s * z), (x1, y1, s * z), (x0, y1, s * z)]
        u = (lambda x: (x - x0) / (x1 - x0)) if s > 0 else (lambda x: (x1 - x) / (x1 - x0))
        quad(mesh, pts, POSTER, (0, 0, s), uvs=[(u(p[0]), (p[1] - y0) / (y1 - y0)) for p in pts])


def truck_near(mesh):
    """A delivery truck, 5.4 x 1.9 x 2.6 m: a chassis with fenders, a hood and cab with its glass, and a box body
    whose sides carry a painted company sign (a poster the stamp's seed picks)."""
    box(mesh, -2.7, 2.7, -0.92, 0.92, 0.35, 0.70)
    box(mesh, 1.75, 2.65, -0.5, 0.5, 0.70, 1.25, skip=("-x",))
    box(mesh, 0.75, 1.75, -0.88, 0.88, 0.70, 1.30, top=None, skip=("-x",))
    frustum(mesh, (0.8, 1.72, -0.86, 0.86), 1.30, (0.85, 1.55, -0.82, 0.82), 1.85, GLASS, TAR, skip=("-x",))
    box(mesh, -2.7, 0.7, -0.95, 0.95, 0.70, 2.6, skip=("-z", "+z"))
    _poster_sides(mesh, -2.7, 0.7, 0.95, 0.70, 2.6)
    axles(mesh, (-1.8, 1.95), 0.38, 0.86)


def truck_far(mesh):
    box(mesh, 0.7, 2.65, -0.9, 0.9, 0.35, 1.85, skip=("-x",))
    box(mesh, -2.7, 0.7, -0.95, 0.95, 0.35, 2.6, skip=("-z", "+z"))
    _poster_sides(mesh, -2.7, 0.7, 0.95, 0.35, 2.6)


CANS = ((-0.32, -0.18), (0.32, -0.20), (0.02, 0.36))


def ashcans_near(mesh):
    """Three galvanised ash cans, 0.56 m across and 0.78 m tall, as six-sided prisms with pale lids."""
    r, h, n = 0.28, 0.78, 6
    for cx, cz in CANS:
        ring = [(cx + r * np.cos(2 * np.pi * k / n), cz + r * np.sin(2 * np.pi * k / n)) for k in range(n)]
        for k in range(n):
            a, b = ring[k], ring[(k + 1) % n]
            quad(mesh, [(a[0], 0, a[1]), (b[0], 0, b[1]), (b[0], h, b[1]), (a[0], h, a[1])], TAR,
                 ((a[0] + b[0]) / 2 - cx, 0, (a[1] + b[1]) / 2 - cz))
        mesh.face([(x, h, z) for x, z in ring], [(x / cb.TW, z / cb.TW) for x, z in ring], METAL, (0, 1, 0))


def ashcans_far(mesh):
    box(mesh, -0.6, 0.6, -0.48, 0.64, 0, 0.78, top=GLASS)


def bench_near(mesh):
    """A park bench 1.8 m long: plank seat and tilted back, slats along X, on two dark iron ends."""
    seat = [(-0.9, 0.45, -0.22), (0.9, 0.45, -0.22), (0.9, 0.45, 0.22), (-0.9, 0.45, 0.22)]
    quad(mesh, seat, PLANK, (0, 1, 0), both=True, uvs=[(p[2] / BT[0], p[0] / BT[1]) for p in seat])
    back = [(-0.9, 0.52, -0.25), (0.9, 0.52, -0.25), (0.9, 0.88, -0.33), (-0.9, 0.88, -0.33)]
    quad(mesh, back, PLANK, (0, 0.2, 1), both=True, uvs=[(p[1] / BT[0], p[0] / BT[1]) for p in back])
    for x in (-0.8, 0.8):
        quad(mesh, [(x, 0, 0.2), (x, 0.45, 0.2), (x, 0.9, -0.34), (x, 0, -0.24)], TAR, (1, 0, 0), both=True)


def hedge_near(h):
    """A clipped box hedge 3 m long and 0.8 m deep, h tall, its top edges chamfered so it does not read as a wall.
    The tar cell's gravel speckle under a green grade reads as leaves; its metre seams as clipped sections."""
    def build(mesh):
        frustum(mesh, (-1.5, 1.5, -0.4, 0.4), 0, (-1.5, 1.5, -0.4, 0.4), h - 0.2, TAR)
        frustum(mesh, (-1.5, 1.5, -0.4, 0.4), h - 0.2, (-1.4, 1.4, -0.28, 0.28), h, TAR, TAR)
    return build


def hedge_far(mesh):
    box(mesh, -1.5, 1.5, -0.4, 0.4, 0, 1.2, skip=("-x", "+x"))


def bench_far(mesh):
    quad(mesh, [(-0.9, 0.45, -0.22), (0.9, 0.45, -0.22), (0.9, 0.45, 0.22), (-0.9, 0.45, 0.22)], PLANK, (0, 1, 0))
    quad(mesh, [(-0.9, 0.52, -0.25), (0.9, 0.52, -0.25), (0.9, 0.88, -0.33), (-0.9, 0.88, -0.33)], PLANK, (0, 0.2, 1))


# Per kind: the family whose roof cell the roof-class surfaces wear, RGB the sRGB mean they are graded to, and the
# near and far builders. Every near0 holds a roof-class surface, so every file writes a baseColorFactor (the
# exporter leaves out a white one, which margin.read_glb needs). TUNE: the RGB targets stand between the block floor (texture luma about 20, Decision 3) and the city's det dressing (48 to 75).
KINDS = {
    "gd_wall": dict(family=STONE, rgb=(72, 70, 66), near=[wall_near(False), wall_near(True)], far=wall_far),
    "gd_fence": dict(family=STONE, rgb=(56, 54, 52), near=[fence_near], far=fence_far),
    "gd_shed": dict(family=STONE, rgb=(56, 54, 52), near=[shed_lean, shed_gable], far=shed_far),
    "gd_sedan": dict(family=STEEL, rgb=(54, 42, 42), near=[sedan_near], far=sedan_far),
    "gd_truck": dict(family=STEEL, rgb=(42, 54, 46), near=[truck_near], far=truck_far),
    "gd_ashcans": dict(family=STONE, rgb=(70, 70, 68), near=[ashcans_near], far=ashcans_far),
    "gd_bench": dict(family=STEEL, rgb=(44, 46, 44), near=[bench_near], far=bench_far),
    # Green on the city atlas rather than the tree's leaf atlas, so the hedge loads with the other kinds and takes
    # the seed's variant pick and its brightness and tint spread.
    "gd_hedge": dict(family=STONE, rgb=(38, 56, 30), near=[hedge_near(1.3), hedge_near(1.05)], far=hedge_far),
}


def bounds(mesh):
    p = np.array([q for pts, _, _ in mesh.faces for q in pts])
    return p.min(0), p.max(0)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out", default=os.path.dirname(os.path.abspath(__file__)))
    p.add_argument("--only", nargs="*", help="kinds to write")
    # Accepted for the runner's sake: the montages are analysis/c5-ground/dressing_preview.py's.
    p.add_argument("--render")
    p.add_argument("--data-root")
    p.add_argument("--chapter", default="C5", choices=["C5"])
    return p.parse_args(argv)


def main():
    args = parse_args()
    cb.vb.reset_scene()
    means = A.paint()[2]
    for model, k in KINDS.items():
        if args.only and model not in args.only:
            continue
        nears = []
        for build in k["near"]:
            nears.append(cb.Mesh())
            build(nears[-1])
        far = cb.Mesh()
        k["far"](far)
        for m in nears + [far]:
            lo, hi = bounds(m)
            assert hi[1] <= HEIGHT_CAP + 1e-6 and lo[1] >= -1e-6, "%s stands %.2f m tall" % (model, hi[1])
        # No surface is wall class, so cb.grade weighs the roof cell alone; near0 holds roof faces where a far may not.
        factor = cb.grade(nears[0], k["family"], k["rgb"], means)
        mat = cb.grade_material(model, factor)
        objs = [cb.to_object("near%d" % i, m, k["family"], mat) for i, m in enumerate(nears)]
        objs.append(cb.to_object("far", far, k["family"], mat))
        path = cb.export(objs, args.out, args.chapter, model)
        lo, hi = bounds(nears[0])
        print("CITY %s: %s, far %d triangles; %.2f x %.2f x %.2f m (x, z, y) from (%.2f, %.2f) to (%.2f, %.2f); "
              "family %d; grade %.4f %.4f %.4f; %s (%d bytes)" % (
                  model, ", ".join("near%d %d" % (i, m.triangles()) for i, m in enumerate(nears)), far.triangles(),
                  hi[0] - lo[0], hi[2] - lo[2], max(bounds(m)[1][1] for m in nears), lo[0], lo[2], hi[0], hi[2],
                  k["family"], *factor, path, os.path.getsize(path)))
        for o in objs:
            me = o.data
            cb.bpy.data.objects.remove(o)
            cb.bpy.data.meshes.remove(me)
        cb.bpy.data.materials.remove(mat)


main()
