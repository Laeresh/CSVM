"""The tussock builder brush3.py calls with its numbers (a SPEC dict): main(SPEC).

A tussock is a mound of long grass blades. Its texture is a fan of blades drawn here with numpy: each
blade rises from the base on its tip's side on a quadratic curve, nearly straight (a little arch by
`arch`), to a tip on, inside or past the card's silhouette (PROFILE: radius over card width at a height
fraction), so the blade ends trace the mound's outline loosely and the outer ones reach the ground.
The geometry stands that fan on upright quads: a few crossed through the centre at the card's full
width and height, which give the outline from every side, and smaller tufts around them, each two
crossed quads (one facing out, one across), which give the mound depth; the outer tufts take the
sparse lit cell, so the edge reads loose. A small dark core keeps it from reading see-through at its
foot. The normals point out from the mound's centre and up, so it shades as one volume.

The atlas, 512 x 512, v up: three 256-pixel fan cells (lit bottom left, plain bottom right, shaded top
left) and the core patch top right. Usage and conventions: veg_blender.py. A tussock is not forest, so
it writes no clump tiles.
"""

import math
import os

import bmesh
import numpy as np
from mathutils import Vector

import veg_blender as vb

SIZE = 512
CELL = 256
# Fan cells (u0, v0) by light: lit, plain, shaded; and the core patch's.
CELLS = ((0.0, 0.0), (0.5, 0.0), (0.0, 0.5))
CORE = (0.5, 0.5)


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


def draw_fan(img, x0, y0, spec, offset, count, rng):
    """One fan cell of count blades from the base to tips on and past the mound's outline, the darker drawn
    first. The outline sits at 1 / `over` of the cell, so a tip can run past it by up to `over`."""
    bl = spec["blades"]
    rmax = max(r for _, r in spec["profile"])
    half, c, top = CELL / 2 - 3, CELL / 2, CELL - 3
    inner = 1 / bl["over"]
    blades = []
    for _ in range(count):
        f = rng.uniform(0.02, 0.98)
        side = 1 if rng.uniform() < 0.5 else -1
        # Most tips on the outline or past it, the rest inside it, so the body is full and its edge loose.
        reach = rng.uniform(0.8, bl["over"]) if rng.uniform() < bl["edge"] else rng.uniform(0.0, 0.8)
        tx, ty = c + side * radius(spec, f, 1.0) / rmax * half * inner * reach, min(top, f * top * inner * max(1.0, reach))
        # The foot on the tip's side, so the blades fan out from the base and do not cross it.
        bx, by = c + side * rng.uniform(-0.1, bl["base"]) * half * reach, 0.0
        # The curve's peak p, between the tip's height and the top: a quadratic from 0 through a control at
        # cy tops out at cy^2 / (2 cy - ty), so cy = p + sqrt(p^2 - p ty) puts the top at p.
        p = ty + rng.uniform(*bl["arch"]) * (top - ty)
        cy = p + math.sqrt(max(0.0, p * p - p * ty)) if p > ty + 1e-3 else ty
        cx = bx + (tx - bx) * rng.uniform(0.3, 0.6)
        light = 0.45 + offset + rng.uniform(-bl["spread"], bl["spread"])
        if rng.uniform() < bl["high"]:
            light = rng.uniform(0.8, 1.0)
        blades.append((light, (bx, by), (cx, cy), (tx, ty), rng.uniform(*bl["width"])))
    for light, b, k, t, w0 in sorted(blades):
        b, k, t = np.array(b), np.array(k), np.array(t)
        n = int(np.linalg.norm(t - b) * 1.5) + 4
        for s in np.linspace(0.0, 1.0, n):
            x, y = b * (1 - s) ** 2 + k * 2 * s * (1 - s) + t * s * s
            r = w0 * (1 - s) ** 0.7 + 0.45
            ys = slice(max(0, int(y - r)), min(CELL, int(y + r) + 1))
            xs = slice(max(0, int(x - r)), min(CELL, int(x + r) + 1))
            # Darker at the foot, lighter toward the tip.
            img[y0 + ys.start:y0 + ys.stop, x0 + xs.start:x0 + xs.stop, :3] = shade(spec["colours"], light + bl["along"] * (s - 0.5))
            img[y0 + ys.start:y0 + ys.stop, x0 + xs.start:x0 + xs.stop, 3] = 1.0


def draw_texture(spec, rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    # Clear texels carry the body colour, so a mip level's edges do not darken.
    img[..., :3] = np.array(spec["colours"][1], np.float32)
    for (u0, v0), offset, count in zip(CELLS, spec["cell_light"], spec["blades"]["count"]):
        draw_fan(img, int(u0 * SIZE), int(v0 * SIZE), spec, offset, count, rng)
    x0, y0 = int(CORE[0] * SIZE), int(CORE[1] * SIZE)
    noise = rng.uniform(0, 1, (CELL, CELL))
    img[y0:, x0:, :3] = np.array([[shade(spec["colours"], 0.05 + 0.3 * v) for v in row] for row in noise])
    img[y0:, x0:, 3] = 1.0
    return img


def build(spec, w, h, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    up = Vector((0, 0, 1))
    centre = Vector((0, 0, spec["centre"] * h))
    rmax = max(r for _, r in spec["profile"]) * w
    # The fan's outline spans 1 / `over` of the cell less its margin; a quad this wide puts it on the card's.
    full = 2 * rmax * spec["blades"]["over"] * (CELL / 2) / (CELL / 2 - 3)

    def vert(p, n):
        v = bm.verts.new(p)
        normals[v] = Vector(n).normalized()
        return v

    def plant_normal(p):
        """Out from the mound's centre, scaled to its proportions, and up."""
        q = Vector((p.x / rmax, p.y / rmax, (p.z - centre.z) / (h / 2)))
        return (q.normalized() if q.length > 1e-3 else up) + up * 0.4

    def fan(p, d, width, height, cell):
        """An upright quad at p across direction d, the fan cell on it."""
        u0, v0 = cell
        corners = (p - d * width / 2, p + d * width / 2, p + d * width / 2 + up * height, p - d * width / 2 + up * height)
        vs = [vert(q, plant_normal(q)) for q in corners]
        f = bm.faces.new(vs)
        for loop, c in zip(f.loops, ((u0, v0), (u0 + 0.5, v0), (u0 + 0.5, v0 + 0.5), (u0, v0 + 0.5))):
            loop[uv].uv = c

    co = spec["core"]
    rings = []
    for f in co["fs"]:
        row = []
        for k in range(7):
            a = 2 * math.pi * k / 6
            p = Vector((math.cos(a), math.sin(a), 0)) * radius(spec, f, w) * co["r"] + up * (f * h)
            row.append(vert(p, plant_normal(p)))
        rings.append(row)
    apex = vert(up * (co["apex"] * h), up)
    cu = lambda k: CORE[0] + 0.5 * k / 6
    cv = lambda i: CORE[1] + 0.5 * i / len(co["fs"])
    for i in range(len(rings) - 1):
        for k in range(6):
            face = bm.faces.new([rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]])
            for loop, c in zip(face.loops, ((cu(k), cv(i)), (cu(k + 1), cv(i)), (cu(k + 1), cv(i + 1)), (cu(k), cv(i + 1)))):
                loop[uv].uv = c
    for k in range(6):
        face = bm.faces.new([rings[-1][k], rings[-1][k + 1], apex])
        for loop, c in zip(face.loops, ((cu(k), cv(len(rings) - 1)), (cu(k + 1), cv(len(rings) - 1)), (cu(k), CORE[1] + 0.5))):
            loop[uv].uv = c

    # The centre fans, evenly turned, at the card's full width and height.
    n = spec["centre_fans"]
    a0 = rng.uniform(0, math.pi)
    for i in range(n):
        a = a0 + math.pi * i / n
        fan(Vector((0, 0, 0)), Vector((math.cos(a), math.sin(a), 0)), full, h, CELLS[1])

    # Tufts round them, smaller toward the edge, inside the outline; the outer and taller ones lit.
    tf = spec["tufts"]
    for i in range(tf["count"]):
        a = i * 2.39996 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a), math.sin(a), 0))
        r = rmax * rng.uniform(*tf["r"])
        s = rng.uniform(*tf["scale"]) * (1 - 0.5 * r / rmax)
        width = min(full * s, 2 * (rmax * 1.05 - r))
        height = h * width / full
        p = d * r
        cell = CELLS[0] if r / rmax + rng.uniform(-0.2, 0.2) > 0.55 else CELLS[2]
        fan(p, Vector((-d.y, d.x, 0)), width, height, cell)
        fan(p, d, width, height, cell)

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
