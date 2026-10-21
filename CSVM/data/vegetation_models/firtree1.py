"""Generates firtree1, the look-alike of the dense fir card C1's forest is stamped with.

A trunk, a dark solid core that keeps the crown from reading hollow, and whorls of drooping
alpha-masked fronds whose tips trace the card's silhouette. The fronds' normals point out from
the trunk axis and up, so the crown shades as one volume rather than as flat planes. The texture
is drawn here with numpy (needle strokes along twigs, a needle-noise patch for the core, a bark
patch), so the model carries no pixel of the original.

Usage and conventions: veg_blender.py.
"""

import math
import os
import sys

import bmesh
import numpy as np
from mathutils import Vector

# No __pycache__ beside the shipped models.
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import veg_blender as vb  # noqa: E402

MODEL = "firtree1.flt"

# The card quad per chapter, width and height in metres: extracted/<C>/gamez, the model under
# firtree1.flt. --render checks the extracted card against this and stops on a mismatch.
CARDS = {"C1": (10.026, 17.834)}

# Crown radius as a fraction of the card width, at a height fraction of the card height, read off
# the C1 card's alpha silhouette (half the opaque span per row).
PROFILE = [(0.0, 0.30), (0.1, 0.35), (0.3, 0.35), (0.45, 0.29), (0.55, 0.26), (0.65, 0.21),
           (0.75, 0.14), (0.85, 0.09), (0.93, 0.04), (0.975, 0.0)]

# Atlas regions in UV (Blender's v runs up): fronds below, the core and bark patches above.
FROND_V = 0.75
CORE_U = (0.0, 0.5)
BARK_U = (0.5, 1.0)

SIZE = 256


def radius(f, w):
    fs, rs = zip(*PROFILE)
    return float(np.interp(f, fs, rs)) * w


def srgb(c):
    return np.array(c, dtype=np.float32)


def draw_texture(rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    img[..., :3] = srgb((0.07, 0.11, 0.05))
    fh = int(SIZE * FROND_V)
    cy = fh / 2

    # A ragged edge, so a frond's outline breaks up rather than reading as a cut shape.
    phases = rng.uniform(0, 2 * math.pi, 3)

    def envelope(x):
        s = x / SIZE
        rag = 1 + 0.12 * sum(math.sin(s * k * 9 + p) for k, p in zip((2, 5, 11), phases)) / 3
        return (fh / 2 - 2) * min(1.0, s / 0.08) * max(0.0, 1 - s) ** 0.6 * min(1.0, rag)

    def stroke(x0, y0, x1, y1, col, thick):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 2
        xs = np.clip(np.round(np.linspace(x0, x1, n)).astype(int), 0, SIZE - 1)
        ys = np.round(np.linspace(y0, y1, n)).astype(int)
        for dy in range(thick):
            yy = np.clip(ys + dy, 0, fh - 1)
            keep = np.abs(yy + 0.5 - cy) <= np.array([envelope(x) for x in xs]) + 1
            img[yy[keep], xs[keep], :3] = col
            img[yy[keep], xs[keep], 3] = 1.0

    # Twigs: the main one along the frond, side twigs angled out toward the envelope.
    twigs = [(0.0, cy, SIZE - 1.0, cy)]
    for x in np.arange(6, SIZE * 0.9, 9):
        reach = SIZE * 0.2
        for side in (-1, 1):
            twigs.append((x, cy, x + reach, cy + side * envelope(x + reach) * 0.97))
    for (x0, y0, x1, y1) in twigs:
        length = math.hypot(x1 - x0, y1 - y0)
        ang = math.atan2(y1 - y0, x1 - x0)
        twig_shade = rng.uniform(0.6, 1.15)
        for t in np.linspace(0, 1, max(3, int(length / 1.8))):
            px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for side in (-1, 1):
                a = ang + side * math.radians(rng.uniform(45, 80))
                ln = rng.uniform(6, 12)
                # Lighter and yellower toward the frond's tip, as new growth is.
                tip = px / SIZE
                shade = twig_shade * rng.uniform(0.75, 1.25) * (0.8 + 0.5 * tip)
                col = np.clip(srgb((0.11 + 0.08 * tip, 0.23, 0.06)) * shade, 0, 1)
                stroke(px, py, px + math.cos(a) * ln, py + math.sin(a) * ln, col, 2)
        stroke(x0, y0, x1, y1, srgb((0.12, 0.09, 0.05)), 1)

    # Core patch: opaque needle noise, darker than the fronds.
    cu0, cu1 = int(CORE_U[0] * SIZE), int(CORE_U[1] * SIZE)
    noise = rng.uniform(0.55, 1.1, (SIZE - fh, cu1 - cu0, 1)).astype(np.float32)
    img[fh:, cu0:cu1, :3] = srgb((0.07, 0.14, 0.04)) * noise
    img[fh:, cu0:cu1, 3] = 1.0
    # Bark patch: brown with vertical streaks.
    bu0, bu1 = int(BARK_U[0] * SIZE), int(BARK_U[1] * SIZE)
    streak = rng.uniform(0.7, 1.2, (1, bu1 - bu0, 1)).astype(np.float32)
    img[fh:, bu0:bu1, :3] = srgb((0.20, 0.14, 0.09)) * streak
    img[fh:, bu0:bu1, 3] = 1.0
    return img


def build(w, h, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}

    def vert(p, n):
        v = bm.verts.new(p)
        normals[v] = Vector(n).normalized()
        return v

    def face(vs, uvs):
        f = bm.faces.new(vs)
        for loop, c in zip(f.loops, uvs):
            loop[uv].uv = c

    def crown_normal(p, fallback):
        r = Vector((p[0], p[1], 0))
        r = r.normalized() if r.length > 0.3 else fallback
        return r + Vector((0, 0, 0.5))

    # Trunk: a six-sided tapered tube, mostly hidden.
    sides, top = 6, 0.92 * h
    ring = []
    for k in range(sides + 1):
        a = 2 * math.pi * k / sides
        d = Vector((math.cos(a), math.sin(a), 0))
        ring.append((vert(d * 0.22, d), vert(d * 0.03 + Vector((0, 0, top)), d), BARK_U[0] + (BARK_U[1] - BARK_U[0]) * k / sides))
    for k in range(sides):
        (b0, t0, u0), (b1, t1, u1) = ring[k], ring[k + 1]
        face([b0, b1, t1, t0], [(u0, FROND_V), (u1, FROND_V), (u1, 1), (u0, 1)])

    # Core: a lathe at a fraction of the crown, so gaps between fronds read dark, not see-through.
    sides = 6
    fs = [0.04, 0.15, 0.35, 0.55, 0.75, 0.9]
    rings = []
    for f in fs:
        r = radius(f, w) * 0.35
        row = []
        for k in range(sides + 1):
            a = 2 * math.pi * k / sides
            d = Vector((math.cos(a), math.sin(a), 0))
            p = d * r + Vector((0, 0, f * h))
            row.append(vert(p, crown_normal(p, d)))
        rings.append(row)
    apex = vert((0, 0, 0.975 * h), (0, 0, 1))
    cu = lambda k: CORE_U[0] + (CORE_U[1] - CORE_U[0]) * k / sides
    cv = lambda i: FROND_V + (1 - FROND_V) * i / len(fs)
    for i in range(len(fs) - 1):
        for k in range(sides):
            face([rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]],
                 [(cu(k), cv(i)), (cu(k + 1), cv(i)), (cu(k + 1), cv(i + 1)), (cu(k), cv(i + 1))])
    for k in range(sides):
        face([rings[-1][k], rings[-1][k + 1], apex], [(cu(k), cv(len(fs) - 1)), (cu(k + 1), cv(len(fs) - 1)), (cu(k), 1)])

    # Fronds: whorls up the trunk, fewer and shorter toward the top, drooping more near the ground.
    # Each frond's length, droop and bearing vary a little, so the tiers do not read as a pagoda.
    whorls = 15
    for i in range(whorls):
        f = 0.05 + 0.88 * i / (whorls - 1) + rng.uniform(-0.01, 0.01)
        count = max(5, round(7 - 3 * f))
        r0 = 0.15
        for k in range(count):
            length = max(1.1, radius(f, w) * rng.uniform(0.9, 1.15) - r0)
            droop = (0.45 + 0.15 * (1 - f)) * rng.uniform(0.8, 1.2)
            a = i * 2.39996 + 2 * math.pi * k / count + rng.uniform(-0.3, 0.3)
            d = Vector((math.cos(a), math.sin(a), 0))
            side = Vector((-d.y, d.x, 0))
            grid = []
            for s, width in ((0.0, 0.7), (0.5, 1.0), (1.0, 0.8)):
                c = d * (r0 + length * s) + Vector((0, 0, f * h + length * (0.15 * s - droop * s * s)))
                hw = 0.6 * length * width
                sag = Vector((0, 0, -0.6 * hw))
                row = []
                for t in (-1, 0, 1):
                    p = c + side * (hw * t) + (sag if t else Vector())
                    # A low frond that would reach below the ground rests on it instead.
                    p.z = max(p.z, 0.1)
                    row.append(vert(p, crown_normal(p, d)))
                grid.append((s, row))
            vs = (0.0, FROND_V / 2, FROND_V)
            for j in range(2):
                (s0, r_a), (s1, r_b) = grid[j], grid[j + 1]
                for t in range(2):
                    face([r_a[t], r_a[t + 1], r_b[t + 1], r_b[t]],
                         [(s0, vs[t]), (s0, vs[t + 1]), (s1, vs[t + 1]), (s1, vs[t])])

    bm.verts.index_update()
    ordered = [normals[v] for v in bm.verts]
    return vb.mesh_object("firtree1", bm, ordered, mat)


def main():
    args = vb.parse_args(list(CARDS))
    w, h = CARDS[args.chapter]
    vb.reset_scene()
    rng = np.random.default_rng(1)
    atlas = vb.make_image("firtree1_atlas", draw_texture(rng))
    mat = vb.atlas_material("firtree1", atlas)
    obj = build(w, h, mat, rng)
    glb = vb.export_glb(obj, args.out, args.chapter, os.path.splitext(MODEL)[0])
    renders = []
    if args.render:
        card = vb.read_card(args.data_root, args.chapter, MODEL)
        if abs(card[0] - w) > 0.01 or abs(card[1] - h) > 0.01:
            raise SystemExit("card is %.3f x %.3f m, CARDS says %.3f x %.3f" % (card[0], card[1], w, h))
        renders = vb.compare_render(obj, card, args.render, "%s_%s" % (args.chapter, "firtree1"), atlas)
    vb.report(obj, glb, renders)


main()
