"""The rosette builder brush1.py calls with its numbers (a SPEC dict): main(SPEC).

A rosette is stiff, broad, pointed leaves rising from a small base, as an agave's. Each leaf is a strip
on a quadratic curve from the base to a tip on the card's silhouette (PROFILE: radius over card width at
a height fraction), rising to its peak (`arch`, a share of the way from its tip's height to the top) and
leaning out to its tip. It lies across its bearing, its face toward the centre and up, folded along its
middle by `fold`, and takes one of three leaf cells, lit, plain or shaded, by its height and reach. The
normals point out from the plant's centre and up, so it shades as one volume. The texture is drawn here
with numpy, so the model carries no pixel of the original.

The atlas, 256 x 256, v up: three 64-pixel leaf cells (lit, plain, shaded, from the bottom), each running
base to tip along u. Usage and conventions: veg_blender.py. A rosette is not forest, so it writes no
clump tiles.
"""

import math
import os

import bmesh
import numpy as np
from mathutils import Matrix, Vector

import veg_blender as vb

SIZE = 256
STRIP = 64
# Leaf cells' v0 by light: lit, plain, shaded.
CELLS = (0.0, 0.25, 0.5)


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


def draw_leaf(img, y0, spec, offset, rng):
    """One leaf cell, base at u 0 and tip at u 1: broad to a quarter of its length, then tapering to a
    point, darker down its fold, pale at its margins and lighter toward its tip."""
    lf = spec["leaf"]
    cy, half = STRIP / 2, STRIP / 2 - 1
    light = 0.5 + offset + rng.uniform(-lf["spread"], lf["spread"])
    streak = rng.uniform(-1, 1, STRIP)
    for x in range(int(0.98 * SIZE)):
        s = x / SIZE
        r = half * min(1.0, 0.55 + s / 0.25 * 0.45) * (1 - s / 0.98) ** lf["taper"]
        if r < 0.6:
            continue
        ys = np.arange(max(0, int(cy - r)), min(STRIP, int(cy + r) + 1))
        across = np.abs(ys - cy) / r
        lit = light + lf["along"] * (s - 0.5) + lf["streak"] * streak[ys] + rng.uniform(-0.04, 0.04, len(ys))
        lit = np.where(across > 1 - lf["margin"], lit + 0.3, lit - lf["fold"] * (1 - across))
        for y, v in zip(ys, lit):
            img[y0 + y, x, :3] = shade(spec["colours"], v)
            img[y0 + y, x, 3] = 1.0


def draw_texture(spec, rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    # Clear texels carry the body colour, so a mip level's edges do not darken.
    img[..., :3] = np.array(spec["colours"][1], np.float32)
    for v0, offset in zip(CELLS, spec["cell_light"]):
        draw_leaf(img, int(v0 * SIZE), spec, offset, rng)
    return img


def build(spec, w, h, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    up = Vector((0, 0, 1))
    centre = Vector((0, 0, spec["centre"] * h))
    rmax = max(r for _, r in spec["profile"]) * w

    def vert(p, n):
        v = bm.verts.new(p)
        normals[v] = Vector(n).normalized()
        return v

    def plant_normal(p):
        """Out from the plant's centre, scaled to its proportions, and up."""
        q = Vector((p.x / rmax, p.y / rmax, (p.z - centre.z) / (h / 2)))
        return (q.normalized() if q.length > 1e-3 else up) + up * 0.4

    st = spec["leaves"]
    n, segs = st["count"], st["segments"]
    top_f = st["span"][1]
    for i in range(n):
        # Stratified tip heights, so the outline is covered from the lowest tip to the top.
        f = st["span"][0] + (st["span"][1] - st["span"][0]) * (i + rng.uniform(0.1, 0.9)) / n
        a = i * 2.39996 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a), math.sin(a), 0))
        base = d * rng.uniform(0.0, st["base_r"]) + up * 0.02
        tip = d * radius(spec, f, w) * rng.uniform(*st["reach"]) + up * (f * h)
        # Its peak p, between its tip's height and the top; a quadratic from z 0 through a control at cz
        # tops out at cz^2 / (2 cz - tz), so cz = p + sqrt(p^2 - p tz) puts the top at p.
        p = f + rng.uniform(*st["arch"]) * max(0.0, top_f - f)
        pz, tz = p * h, tip.z
        cz = pz + math.sqrt(max(0.0, pz * pz - pz * tz)) if pz > tz + 1e-3 else tz
        ctl = base + (tip - base) * rng.uniform(*st["ctl_r"])
        ctl.z = cz
        width = rng.uniform(*st["width"])
        # Across its bearing, turned a little round it, so the leaves do not all face the centre.
        side = Matrix.Rotation(rng.uniform(-st["twist"], st["twist"]), 3, d) @ Vector((-d.y, d.x, 0))
        light = 0.6 * f + 0.6 * tip.xy.length / max(rmax, 1e-3) + rng.uniform(-0.25, 0.25)
        v0 = CELLS[0] if light > 0.95 else CELLS[1] if light > 0.6 else CELLS[2]
        rows = []
        for j in range(segs + 1):
            t = j / segs
            c = base * (1 - t) ** 2 + ctl * 2 * t * (1 - t) + tip * t * t
            tan = (ctl - base) * 2 * (1 - t) + (tip - ctl) * 2 * t
            # The face's normal, toward the centre and up; the fold drops the midline below the edges.
            fn = side.cross(tan.normalized()).normalized()
            if fn.z < 0:
                fn = -fn
            hw = width * (1 - st["taper"] * t)
            row = []
            for k in (-1, 0, 1):
                q = c + side * (hw * k) - (fn * st["fold"] * hw if k == 0 else Vector())
                q.z = max(q.z, 0.02)
                row.append((vert(q, plant_normal(q)), (t, v0 + 0.25 * (k + 1) / 2)))
            rows.append(row)
        for j in range(segs):
            for k in range(2):
                quad = (rows[j][k], rows[j][k + 1], rows[j + 1][k + 1], rows[j + 1][k])
                face = bm.faces.new([q[0] for q in quad])
                for loop, q in zip(face.loops, quad):
                    loop[uv].uv = q[1]

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
