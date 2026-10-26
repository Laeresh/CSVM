"""The bush builder bush1.py and bush2.py share: each holds one card species' numbers (a SPEC dict)
and calls main(SPEC).

A bush is a few stems fanning up from the base, an optional dark leaf core (a dense bush, so it
does not read see-through), and leaf clusters scattered through the card's silhouette (PROFILE:
radius over card width at a height fraction), their outer edges on it. A cluster is two crossed
quads, one facing out of the bush and one standing across it, so it fills the outline from every
side. Each takes one of three cluster cells, lit, plain or shaded, the shaded ones deeper inside
and lower. The normals point out from the bush's centre and up, so it shades as one volume, not
quad by quad. The leaves are drawn here with numpy, so the model carries no pixel of the original.

The atlas, 256 x 256, v up: three 128-pixel cluster cells (lit bottom left, plain bottom right,
shaded top left) and the bark and core patches side by side in the top right.
Usage and conventions: veg_blender.py. Bushes are not forest, so they write no clump tiles.
"""

import math
import os

import bmesh
import numpy as np
from mathutils import Matrix, Vector

import veg_blender as vb

SIZE = 256
CELL = 128
# Cluster cells (u0, v0) in UV, by light: lit, plain, shaded.
CELLS = ((0.0, 0.0), (0.5, 0.0), (0.0, 0.5))
BARK_U = (0.5, 0.75)
CORE_U = (0.75, 1.0)
PATCH_V = (0.5, 1.0)


def radius(spec, f, w):
    fs, rs = zip(*spec["profile"])
    return float(np.interp(f, fs, rs)) * w


def shade(cols, light):
    """cols[0] at 0, cols[1] at 0.5, cols[2] at 1."""
    dark, mid, high = (np.array(c, np.float32) for c in cols)
    light = min(1.0, max(0.0, light))
    if light < 0.5:
        return dark + (mid - dark) * (light / 0.5)
    return mid + (high - mid) * ((light - 0.5) / 0.5)


def draw_cluster(img, x0, y0, spec, offset, rng):
    """One cluster cell: leaves inside a ragged disc, lit from above, the brightest drawn last. With
    `rosettes`, the leaves gather round that many sub-centres, leaving holes between them."""
    lv = spec["leaves"]
    c = CELL / 2
    lobes = rng.uniform(0, 2 * math.pi, 3)
    edge = lambda a: (c - 3) * (0.78 + 0.08 * math.sin(3 * a + lobes[0]) + 0.08 * math.sin(5 * a + lobes[1]) + 0.06 * math.sin(7 * a + lobes[2]))
    yy, xx = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    centres = [(rng.uniform(-0.55, 0.55) * c, rng.uniform(-0.55, 0.55) * c) for _ in range(lv.get("rosettes", 0))]
    leaves = []
    while len(leaves) < lv["count"]:
        if centres:
            cx, cy = centres[rng.integers(len(centres))]
            r, a = abs(rng.normal(0, lv["rosette_r"] * c)), rng.uniform(0, 2 * math.pi)
            x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        else:
            r, a = c * math.sqrt(rng.uniform()), rng.uniform(0, 2 * math.pi)
            x, y = r * math.cos(a), r * math.sin(a)
        if math.hypot(x, y) > edge(math.atan2(y, x)):
            continue
        # Row 0 is the bottom (v up), so a leaf higher in the cell is lit more.
        light = 0.5 + offset + lv["top"] * y / c + rng.uniform(-lv["spread"], lv["spread"])
        if rng.uniform() < lv["high"]:
            light = rng.uniform(0.8, 1.0)
        leaves.append((light, x + c, y + c, rng.uniform(*lv["size"]), rng.uniform(0, math.pi)))
    for light, x, y, ln, ang in sorted(leaves):
        # A leaf is an ellipse ln long and 0.45 ln wide, turned by ang.
        dx, dy = xx - x, yy - y
        u = (dx * math.cos(ang) + dy * math.sin(ang)) / (ln / 2)
        v = (-dx * math.sin(ang) + dy * math.cos(ang)) / (ln * 0.225)
        m = u * u + v * v <= 1.0
        cell = img[y0:y0 + CELL, x0:x0 + CELL]
        cell[m, :3] = shade(spec["colours"], light)
        cell[m, 3] = 1.0


def draw_texture(spec, rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    # Clear texels carry the body colour, so a mip level's edges do not darken.
    img[..., :3] = np.array(spec["colours"][1], np.float32)
    for (u0, v0), offset in zip(CELLS, spec["cell_light"]):
        draw_cluster(img, int(u0 * SIZE), int(v0 * SIZE), spec, offset, rng)
    y0 = int(PATCH_V[0] * SIZE)
    noise = rng.uniform(-1, 1, (SIZE - y0, SIZE)).astype(np.float32)
    bu0, bu1 = int(BARK_U[0] * SIZE), int(BARK_U[1] * SIZE)
    dark, light = (np.array(c, np.float32) for c in spec["bark"])
    t = np.clip(0.5 + 0.35 * noise[:, bu0:bu1] + 0.2 * rng.uniform(-1, 1, (1, bu1 - bu0)), 0, 1)[..., None]
    img[y0:, bu0:bu1, :3] = dark + (light - dark) * t
    cu0, cu1 = int(CORE_U[0] * SIZE), int(CORE_U[1] * SIZE)
    img[y0:, cu0:cu1, :3] = np.array([[shade(spec["colours"], 0.15 + 0.2 * v) for v in row] for row in noise[:, cu0:cu1]])
    img[y0:, bu0:, 3] = 1.0
    return img


def build(spec, w, h, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    up = Vector((0, 0, 1))
    centre = Vector((0, 0, spec["centre"] * h))
    half_h = h / 2

    def vert(p, n):
        v = bm.verts.new(p)
        normals[v] = Vector(n).normalized()
        return v

    def face(vs, uvs):
        f = bm.faces.new(vs)
        for loop, c in zip(f.loops, uvs):
            loop[uv].uv = c

    def bush_normal(p):
        """Out from the bush's centre, scaled to its proportions, and up."""
        rmax = max(r for _, r in spec["profile"]) * w
        q = Vector(((p.x) / rmax, (p.y) / rmax, (p.z - centre.z) / half_h))
        return (q.normalized() if q.length > 1e-3 else up) + up * 0.4

    # Stems: four-sided rods fanning up from the base to inside the leaves.
    st = spec["stems"]
    for i in range(st["count"]):
        a = i * 2.39996 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a), math.sin(a), 0))
        f = rng.uniform(*st["top"])
        tip = d * radius(spec, f, w) * rng.uniform(0.3, 0.55) + up * (f * h)
        # Raised by its radius, so the rod's ring stays on the ground, where the pivot is.
        foot = d * rng.uniform(0.0, st["r"] * 2) + up * st["r"]
        axis = (tip - foot).normalized()
        side = axis.cross(up).normalized()
        other = axis.cross(side).normalized()
        ring = []
        for k in range(5):
            b = 2 * math.pi * k / 4
            o = side * math.cos(b) + other * math.sin(b)
            u = BARK_U[0] + (BARK_U[1] - BARK_U[0]) * k / 4
            ring.append((vert(foot + o * st["r"], o), vert(tip + o * st["r"] * 0.4, o), u))
        for k in range(4):
            (b0, t0, u0), (b1, t1, u1) = ring[k], ring[k + 1]
            face([b0, b1, t1, t0], [(u0, PATCH_V[0]), (u1, PATCH_V[0]), (u1, PATCH_V[1]), (u0, PATCH_V[1])])

    # Core: a dark leaf lathe inside the clusters, so a dense bush does not read see-through.
    co = spec.get("core")
    if co:
        rings = []
        for f in co["fs"]:
            row = []
            for k in range(7):
                a = 2 * math.pi * k / 6
                d = Vector((math.cos(a), math.sin(a), 0))
                p = d * radius(spec, f, w) * co["r"] + up * (f * h)
                row.append(vert(p, bush_normal(p)))
            rings.append(row)
        top = vert(up * (co["apex"] * h), up)
        cu = lambda k: CORE_U[0] + (CORE_U[1] - CORE_U[0]) * k / 6
        cv = lambda i: PATCH_V[0] + (PATCH_V[1] - PATCH_V[0]) * i / len(co["fs"])
        for i in range(len(rings) - 1):
            for k in range(6):
                face([rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]],
                     [(cu(k), cv(i)), (cu(k + 1), cv(i)), (cu(k + 1), cv(i + 1)), (cu(k), cv(i + 1))])
        for k in range(6):
            face([rings[-1][k], rings[-1][k + 1], top], [(cu(k), cv(len(rings) - 1)), (cu(k + 1), cv(len(rings) - 1)), (cu(k), PATCH_V[1])])

    def quad(p, n, t, s, cell):
        """A square of half side s at p, facing n, its edges along t and n x t, on one cluster cell."""
        b = n.cross(t)
        u0, v0 = cell
        corners = (-t - b, t - b, t + b, -t + b)
        uvs = ((u0, v0), (u0 + 0.5, v0), (u0 + 0.5, v0 + 0.5), (u0, v0 + 0.5))
        vs = []
        for c in corners:
            q = p + c * s
            # A low cluster that would reach below the ground rests on it instead.
            q.z = max(q.z, 0.05)
            vs.append(vert(q, bush_normal(q)))
        face(vs, uvs)

    # Clusters: spread up the bush by its cross-section, each at its own bearing, pushed out so its
    # outer edge meets the silhouette; the deep and low ones take the shaded cell.
    cl = spec["clusters"]
    grid_f = np.linspace(cl["span"][0], cl["span"][1], 200)
    weight = np.array([radius(spec, f, w) for f in grid_f]) ** 2 + 0.02 * w * w
    cdf = np.cumsum(weight)
    cdf = (cdf - cdf[0]) / (cdf[-1] - cdf[0])
    n = cl["count"]
    for i in range(n):
        f = float(np.interp((i + rng.uniform(0.2, 0.8)) / n, cdf, grid_f))
        a = i * 2.39996 + rng.uniform(-0.5, 0.5)
        d = Vector((math.cos(a), math.sin(a), 0))
        s = rng.uniform(*cl["size"]) * w
        # A top cluster sits low enough that its leaves end at the card's height, not above it.
        f = min(f, 1.0 - 1.4 * s / h)
        depth = rng.uniform(*cl["depth"])
        r = max(0.0, radius(spec, f, w) - s * 0.7) * depth
        out = (d * 0.8 + up * rng.uniform(-0.2, 0.6)).normalized()
        if f > cl.get("cap", 2.0):
            # A flat-topped bush: clusters above `cap` spread over the whole top and face up, so
            # the top is leaves, not a ring of them round a hole.
            r *= math.sqrt(rng.uniform(0.0, 1.0))
            out = (d * 0.35 + up).normalized()
        p = d * r + up * (f * h)
        # A random roll round the outward axis, so the cells' leaves do not line up.
        t = Matrix.Rotation(rng.uniform(0, math.pi), 3, out) @ out.cross(up).normalized()
        light = depth + 0.4 * f + rng.uniform(-0.2, 0.2)
        cell = CELLS[0] if light > 1.15 else CELLS[1] if light > 0.8 else CELLS[2]
        quad(p, out, t, s, cell)
        quad(p, out.cross(t).normalized(), out, s, cell)

    bm.verts.index_update()
    ordered = [normals[v] for v in bm.verts]
    return vb.mesh_object(spec["name"], bm, ordered, mat)


def main(spec):
    """Writes the model for the chapter asked for, and with --render the comparison."""
    args = vb.parse_args(list(spec["cards"]))
    w, h = spec["cards"][args.chapter]
    name = os.path.splitext(spec["model"])[0]
    spec = dict(spec, name=name)
    vb.reset_scene()
    rng = np.random.default_rng(spec["seed"])
    atlas = vb.make_image(name + "_atlas", draw_texture(spec, rng))
    mat = vb.atlas_material(name, atlas)
    obj = build(spec, w, h, mat, rng)
    glb = vb.export_glb(obj, args.out, args.chapter, name)
    renders = []
    if args.render:
        card = vb.read_card(args.data_root, args.chapter, spec["model"])
        if abs(card[0] - w) > 0.01 or abs(card[1] - h) > 0.01:
            raise SystemExit("card is %.3f x %.3f m, CARDS says %.3f x %.3f" % (card[0], card[1], w, h))
        renders = vb.compare_render(obj, card, args.render, "%s_%s" % (args.chapter, name), atlas)
    vb.report(obj, glb, renders)
