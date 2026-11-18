"""Generates citytree, C5's deciduous city tree (a London plane or elm, plan Decision 14) for the
gardens and parks of the Enhanced block floor, and citytree_far, its cheap stand-in at range. No
card exists for it: the original city has no tree, so the model stands where the generated
layout puts it (docs/PLAN-c5-ground.md, B14).

The near model is a clear trunk with mottled plane-tree bark, three limbs forking into the crown,
and a crown of domes as canopytree's (a top dome and a ring of lower ones, each a leafy disc lit
at its top and dark at its rim, normals bent out per dome and from the crown's centre), on a
5-sided trunk. The far model is the trunk on three sides and one broad dome. Both share one
atlas: three leaf disc cells (lit, plain, shaded) and the bark patch. Heights and widths are
TUNE (about 8 to 12 m tall, Decision 5's exception to the 4 m cap).

No --render comparison exists without a card: --render writes the atlas there, and the montage is
analysis/c5-ground/dressing_preview.py's. Usage and conventions: veg_blender.py.
"""

import math
import os
import sys

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import veg_blender as vb  # noqa: E402

SPEC = {
    "seed": 57,
    # sRGB leaf shade, body and highlight: a summer plane's mid green, a step lighter than C3's
    # jungle crowns so it reads over a block floor at texture luma 20 at night. TUNE in game.
    "colours": ((0.085, 0.130, 0.050), (0.165, 0.240, 0.090), (0.340, 0.420, 0.180)),
    # Bark: the plane's flaking camouflage, olive grey with cream and brown patches (sRGB).
    "bark": ((0.36, 0.35, 0.28), (0.62, 0.60, 0.48), (0.25, 0.21, 0.16)),
    # The trunk: radius at the foot and at the crown, clear height (where the limbs leave) and its top.
    "trunk": {"r": (0.24, 0.17), "clear": 3.2, "top": 6.2, "sides": 5},
    # Limbs from the clear height to under the ring domes, their radius and sides.
    "limbs": {"count": 3, "r": 0.10, "sides": 3},
    "top": {"r": 3.0, "z": 7.2, "squash": 0.8},
    "ring": {"count": 5, "out": (2.0, 2.5), "r": (2.0, 2.4), "z": (5.6, 6.3), "squash": 0.75},
    "sides": 5,
    "rings": (50.0, 90.0, 130.0),
    "lumps": 0.25,
    "normals": {"crown": 0.3, "jitter": 0.5},
    "leaves": {"top": 0.68, "fall": 0.7, "clumps": 14, "clump": 0.18, "crack": 0.45, "crack_px": 6.0, "grain": 0.4,
               "high": 0.06},
    # The far stand-in: one dome as wide as the near crown, a trunk on three sides.
    "far": {"r": 4.4, "z": 6.0, "squash": 0.72, "sides": 6, "rings": (60.0, 120.0), "trunk_sides": 3},
}

SIZE = 256
CELL = 128
# Disc cells (u0, v0) in UV by light: lit, plain, shaded; the bark patch is the top-right quarter.
CELLS = ((0.0, 0.0), (0.5, 0.0), (0.0, 0.5))
BARK = (0.5, 0.5)


def shade(cols, light):
    dark, mid, high = (np.array(c, np.float32) for c in cols)
    t = np.clip(light, 0.0, 1.0)[..., None]
    return np.where(t < 0.5, dark + (mid - dark) * (t / 0.5), mid + (high - mid) * ((t - 0.5) / 0.5))


def draw_disc(img, x0, y0, spec, lift, rng):
    """One dome's disc, canopytree's: lit at the centre, dark at the rim, leaf clumps with cracks."""
    lv = spec["leaves"]
    c = CELL / 2
    yy, xx = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    r = np.hypot(xx - c + 0.5, yy - c + 0.5) / (c - 1)
    light = lv["top"] - lv["fall"] * r ** 1.5 + lift
    seeds = rng.uniform(0, CELL, (lv["clumps"], 2)).astype(np.float32)
    d = np.hypot(xx[..., None] - seeds[:, 0], yy[..., None] - seeds[:, 1])
    order = np.argsort(d, axis=2)
    d1 = np.take_along_axis(d, order[..., :1], 2)[..., 0]
    d2 = np.take_along_axis(d, order[..., 1:2], 2)[..., 0]
    own = rng.uniform(-1, 1, lv["clumps"]).astype(np.float32)[order[..., 0]]
    light += lv["clump"] * own - lv["crack"] * np.clip(1.0 - (d2 - d1) / lv["crack_px"], 0.0, 1.0)
    grain = rng.uniform(-1, 1, (CELL, CELL)).astype(np.float32)
    light += lv["grain"] * (grain + np.roll(grain, 1, 0) + np.roll(grain, 1, 1)) / 3
    specks = rng.uniform(size=(CELL, CELL)) < lv["high"] * np.clip(1.0 - r, 0, 1)
    light = np.where(specks, rng.uniform(0.8, 1.0, (CELL, CELL)), light)
    inside = r <= 1.0
    cell = img[y0:y0 + CELL, x0:x0 + CELL]
    cell[inside, :3] = shade(spec["colours"], light)[inside]
    cell[inside, 3] = 1.0


def draw_bark(img, spec, rng):
    """The bark patch, opaque and seamless across u: blurred noise cut into cream and brown flakes
    over the olive grey, the flakes longer up the trunk than across it."""
    base, cream, brown = (np.array(c, np.float32) for c in spec["bark"])

    def flakes():
        n = rng.standard_normal((CELL, CELL)).astype(np.float32)
        for _ in range(3):
            n = (n + np.roll(n, 1, 1) + np.roll(n, -1, 1)) / 3
        for _ in range(6):
            n = (n + np.roll(n, 1, 0) + np.roll(n, -1, 0)) / 3
        return n / n.std()

    a, b = flakes(), flakes()
    col = np.broadcast_to(base, (CELL, CELL, 3)).copy()
    col = np.where((a > 0.8)[..., None], cream, col)
    col = np.where((b > 1.0)[..., None], brown, col)
    col *= (1.0 + 0.06 * rng.uniform(-1, 1, (CELL, CELL)))[..., None]
    x0, y0 = int(BARK[0] * SIZE), int(BARK[1] * SIZE)
    img[y0:y0 + CELL, x0:x0 + CELL, :3] = np.clip(col, 0, 1)
    img[y0:y0 + CELL, x0:x0 + CELL, 3] = 1.0


def draw_texture(spec, rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    # Clear texels carry the body colour, so a mip level's edges do not darken.
    img[..., :3] = np.array(spec["colours"][1], np.float32)
    for (u0, v0), lift in zip(CELLS, (0.08, -0.02, -0.08)):
        draw_disc(img, int(u0 * SIZE), int(v0 * SIZE), spec, lift, rng)
    draw_bark(img, spec, rng)
    return img


def prism(bm, uv, normals, a, b, ra, rb, sides):
    """An open prism from point a (radius ra) to b (rb), its UVs round the bark patch, normals out from its axis."""
    axis = (b - a).normalized()
    side = axis.orthogonal().normalized()
    other = axis.cross(side)
    rings = []
    for p, r in ((a, ra), (b, rb)):
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            out = side * math.cos(ang) + other * math.sin(ang)
            v = bm.verts.new(p + out * r)
            normals[v] = out
            ring.append(v)
        rings.append(ring)
    for k in range(sides):
        j = (k + 1) % sides
        f = bm.faces.new((rings[0][k], rings[0][j], rings[1][j], rings[1][k]))
        u0, u1 = BARK[0] + 0.5 * k / sides, BARK[0] + 0.5 * (k + 1) / sides
        for loop, c in zip(f.loops, ((u0, BARK[1]), (u1, BARK[1]), (u1, 1.0), (u0, 1.0))):
            loop[uv].uv = c


def dome(bm, uv, normals, spec, o, r, squash, cell, sides, rings, centre, rng):
    """canopytree's dome: an apex fan and rings of quads down to a skirt, UVs on a disc cell."""
    u0, v0 = cell
    polar = [0.0] + [math.radians(a) for a in rings]
    turn = rng.uniform(0, 2 * math.pi)
    nm = spec["normals"]

    def vert(theta, phi):
        q = r * (1.0 + spec["lumps"] * rng.uniform(-1, 1) * math.sin(theta))
        p = o + Vector((q * math.sin(theta) * math.cos(phi), q * math.sin(theta) * math.sin(phi), r * squash * math.cos(theta)))
        v = bm.verts.new(p)
        normals[v] = ((p - centre).normalized() * nm["crown"] + (p - o).normalized()
                      + Vector(tuple(rng.uniform(-1, 1, 3))) * nm["jitter"]).normalized()
        s = 0.25 * 0.97 * theta / polar[-1]
        return v, (u0 + 0.25 + s * math.cos(phi + turn), v0 + 0.25 + s * math.sin(phi + turn))

    apex = vert(0.0, 0.0)
    ring = [[vert(t, 2 * math.pi * k / sides) for k in range(sides)] for t in polar[1:]]
    for k in range(sides):
        a, b = ring[0][k], ring[0][(k + 1) % sides]
        f = bm.faces.new((apex[0], a[0], b[0]))
        for loop, c in zip(f.loops, (apex[1], a[1], b[1])):
            loop[uv].uv = c
    for i in range(len(ring) - 1):
        for k in range(sides):
            q = (ring[i][k], ring[i + 1][k], ring[i + 1][(k + 1) % sides], ring[i][(k + 1) % sides])
            f = bm.faces.new([p[0] for p in q])
            for loop, (_, c) in zip(f.loops, q):
                loop[uv].uv = c


def finish(name, bm, normals, mat):
    bm.verts.index_update()
    return vb.mesh_object(name, bm, [normals[v] for v in bm.verts], mat)


def build_near(spec, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    t = spec["trunk"]
    clear = Vector((0, 0, t["clear"]))
    prism(bm, uv, normals, Vector((0, 0, 0)), Vector((0, 0, t["top"])), t["r"][0], t["r"][1], t["sides"])
    centre = Vector((0, 0, spec["top"]["z"]))
    rg = spec["ring"]
    domes = [(centre, spec["top"]["r"], spec["top"]["squash"], CELLS[0])]
    for i in range(rg["count"]):
        a = 2 * math.pi * i / rg["count"] + rng.uniform(-0.3, 0.3)
        d = rng.uniform(*rg["out"])
        domes.append((Vector((d * math.cos(a), d * math.sin(a), rng.uniform(*rg["z"]))), rng.uniform(*rg["r"]),
                      rg["squash"], CELLS[1 + i % 2]))
    # The limbs reach from the clear height to under every other ring dome, hidden inside the crown at their tips.
    lm = spec["limbs"]
    for o, *_ in domes[1:1 + 2 * lm["count"]:2]:
        prism(bm, uv, normals, clear, Vector((o.x * 0.7, o.y * 0.7, o.z - 0.6)), lm["r"] * 1.4, lm["r"], lm["sides"])
    for o, r, squash, cell in domes:
        dome(bm, uv, normals, spec, o, r, squash, cell, spec["sides"], spec["rings"], centre, rng)
    return finish("citytree", bm, normals, mat)


def build_far(spec, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    t, f = spec["trunk"], spec["far"]
    prism(bm, uv, normals, Vector((0, 0, 0)), Vector((0, 0, f["z"])), t["r"][0], t["r"][1], f["trunk_sides"])
    centre = Vector((0, 0, f["z"]))
    dome(bm, uv, normals, spec, centre, f["r"], f["squash"], CELLS[0], f["sides"], f["rings"], centre, rng)
    return finish("citytree_far", bm, normals, mat)


def main(spec):
    args = vb.parse_args(["C5"])
    vb.reset_scene()
    rng = np.random.default_rng(spec["seed"])
    atlas = vb.make_image("citytree_atlas", draw_texture(spec, rng))
    mat = vb.atlas_material("citytree", atlas)
    near = build_near(spec, mat, rng)
    far = build_far(spec, mat, rng)
    for obj in (near, far):
        vb.report(obj, vb.export_glb(obj, args.out, args.chapter, obj.name), [])
    if args.render:
        os.makedirs(args.render, exist_ok=True)
        atlas.save_render(os.path.join(args.render, "C5_citytree_atlas.png"))


main(SPEC)
