"""Generates canopytree, the broadleaf crown C3's near models stand over its painted jungle canopy
(forest1, forest1-128, cliff1), where no card stands. It is a crown and nothing below it: a
central dome and a ring of lower domes round it, each a leafy disc lit at its top and darkening to
its rim, so it reads as a lumpy crown in the game's unlit draw. It stands sunk in the painted
canopy (CanopyClumps places it), so no trunk shows and none is built.

No card exists for it, so --render sets the model beside a 12.8 m swatch of C3's forest1 (one
repeat of the painted canopy) as the colour reference, scratch only. The palette starts from
forest1's texels by luminance band (D33, "Colour in game") and is set greener and yellower, as
the lit painted ground reads in game. Usage and conventions: veg_blender.py.
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
    "model": "canopytree",
    "chapters": ["C3"],
    "seed": 31,
    # sRGB shade, body and highlight: greener and yellower than forest1's 10-30 %, 40-60 % and 80-100 %
    # luminance bands (35, 51, 26), (50, 70, 38) and (92, 113, 66), which drew greyer than the lit painted
    # ground in game (the user's ruling, option C). Set at 0.85 of that option's levels, so D32's grade lands
    # inside its 0.25 floor (0.240 at full) and the crowns draw at their target brightness.
    "colours": ((0.094, 0.170, 0.051), (0.145, 0.247, 0.077), (0.340, 0.425, 0.170)),
    # The top dome: radius, height of its equator over the pivot, vertical over horizontal radius.
    "top": {"r": 3.6, "z": 4.4, "squash": 0.75},
    # The ring of domes round it: count, distance out, radius, equator height, each jittered.
    "ring": {"count": 5, "out": (3.5, 4.3), "r": (2.4, 3.0), "z": (2.0, 2.9), "squash": 0.8},
    # Sides of a dome, and its rings by polar angle from the top in degrees (the last is the skirt).
    "sides": 5,
    "rings": (50.0, 90.0, 130.0),
    # How far a rim vertex strays in or out, over the dome's radius.
    "lumps": 0.16,
    # The disc's light: at its centre, its fall to the rim, the soft crownlets on it and the grain.
    "leaves": {"top": 0.68, "fall": 0.7, "crownlets": 6, "crownlet": 0.35, "grain": 0.4, "high": 0.06},
    "swatch": ("C3", "forest1.png", 12.8),
}

SIZE = 256
CELL = 128
# Disc cells (u0, v0) in UV: lit (the top dome) and three plain ones for the ring.
CELLS = ((0.0, 0.0), (0.5, 0.0), (0.0, 0.5), (0.5, 0.5))


def shade(cols, light):
    """cols[0] at 0, cols[1] at 0.5, cols[2] at 1, per texel of the light array."""
    dark, mid, high = (np.array(c, np.float32) for c in cols)
    t = np.clip(light, 0.0, 1.0)[..., None]
    return np.where(t < 0.5, dark + (mid - dark) * (t / 0.5), mid + (high - mid) * ((t - 0.5) / 0.5))


def draw_disc(img, x0, y0, spec, lift, rng):
    """One dome's disc: the centre (the dome's top) lit and the rim (its skirt) dark, under a few soft
    lighter crownlets and a fine leaf grain about as fine as the painted canopy's. Clear outside it."""
    lv = spec["leaves"]
    c = CELL / 2
    yy, xx = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    r = np.hypot(xx - c + 0.5, yy - c + 0.5) / (c - 1)
    light = lv["top"] - lv["fall"] * r ** 1.5 + lift
    for _ in range(lv["crownlets"]):
        rr, a = 0.75 * math.sqrt(rng.uniform()), rng.uniform(0, 2 * math.pi)
        bx, by, s = c + rr * c * math.cos(a), c + rr * c * math.sin(a), rng.uniform(0.08, 0.16) * CELL
        light += lv["crownlet"] * (1.0 - rr) * np.exp(-((xx - bx) ** 2 + (yy - by) ** 2) / (2 * s * s))
    # The grain: specks a texel or two across, each a texel's own draw blurred by its neighbours.
    grain = rng.uniform(-1, 1, (CELL, CELL)).astype(np.float32)
    grain = (grain + np.roll(grain, 1, 0) + np.roll(grain, 1, 1)) / 3
    light += lv["grain"] * grain
    specks = rng.uniform(size=(CELL, CELL)) < lv["high"] * np.clip(1.0 - r, 0, 1)
    light = np.where(specks, rng.uniform(0.8, 1.0, (CELL, CELL)), light)
    inside = r <= 1.0
    cell = img[y0:y0 + CELL, x0:x0 + CELL]
    cell[inside, :3] = shade(spec["colours"], light)[inside]
    cell[inside, 3] = 1.0


def draw_texture(spec, rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    # Clear texels carry the body colour, so a mip level's edges do not darken.
    img[..., :3] = np.array(spec["colours"][1], np.float32)
    for (u0, v0), lift in zip(CELLS, (0.08, -0.02, -0.06, 0.0)):
        draw_disc(img, int(u0 * SIZE), int(v0 * SIZE), spec, lift, rng)
    return img


def build(spec, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    n = spec["sides"]
    polar = [0.0] + [math.radians(a) for a in spec["rings"]]
    centre = Vector((0, 0, spec["top"]["z"]))

    domes = [(Vector((0, 0, spec["top"]["z"])), spec["top"]["r"], spec["top"]["squash"], CELLS[0])]
    rg = spec["ring"]
    for i in range(rg["count"]):
        a = 2 * math.pi * i / rg["count"] + rng.uniform(-0.3, 0.3)
        d = rng.uniform(*rg["out"])
        domes.append((Vector((d * math.cos(a), d * math.sin(a), rng.uniform(*rg["z"]))), rng.uniform(*rg["r"]),
                      rg["squash"], CELLS[1 + i % 3]))

    for o, r, squash, (u0, v0) in domes:
        turn = rng.uniform(0, 2 * math.pi)

        def vert(theta, phi):
            # Each rim vertex pushed in or out a little, so no dome outlines a regular polygon.
            q = r * (1.0 + spec["lumps"] * rng.uniform(-1, 1) * math.sin(theta))
            p = o + Vector((q * math.sin(theta) * math.cos(phi), q * math.sin(theta) * math.sin(phi), r * squash * math.cos(theta)))
            v = bm.verts.new(p)
            # Out from the crown's centre and the dome's own, so the crown shades as one volume in a render.
            normals[v] = ((p - centre).normalized() + (p - o).normalized()).normalized()
            # The disc's centre is the dome's top, its rim the skirt: polar angle over the last ring's.
            s = 0.25 * 0.97 * theta / polar[-1]
            return v, (u0 + 0.25 + s * math.cos(phi + turn), v0 + 0.25 + s * math.sin(phi + turn))

        apex = vert(0.0, 0.0)
        rings = [[vert(t, 2 * math.pi * k / n) for k in range(n)] for t in polar[1:]]
        for k in range(n):
            a, b = rings[0][k], rings[0][(k + 1) % n]
            f = bm.faces.new((apex[0], a[0], b[0]))
            for loop, c in zip(f.loops, (apex[1], a[1], b[1])):
                loop[uv].uv = c
        for i in range(len(rings) - 1):
            for k in range(n):
                q = (rings[i][k], rings[i + 1][k], rings[i + 1][(k + 1) % n], rings[i][(k + 1) % n])
                f = bm.faces.new([p[0] for p in q])
                for loop, (_, c) in zip(f.loops, q):
                    loop[uv].uv = c

    # The pivot at the lowest point, as every model's is: CanopyClumps sinks it below the painted canopy.
    low = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= low
    bm.verts.index_update()
    ordered = [normals[v] for v in bm.verts]
    return vb.mesh_object(spec["model"], bm, ordered, mat)


def main(spec):
    args = vb.parse_args(spec["chapters"])
    vb.reset_scene()
    rng = np.random.default_rng(spec["seed"])
    atlas = vb.make_image(spec["model"] + "_atlas", draw_texture(spec, rng))
    mat = vb.atlas_material(spec["model"], atlas)
    obj = build(spec, mat, rng)
    glb = vb.export_glb(obj, args.out, args.chapter, spec["model"])
    renders = []
    if args.render:
        chapter, texture, size = spec["swatch"]
        png = os.path.join(args.data_root, "extracted", chapter, "texture", texture)
        renders = vb.compare_render(obj, (size, size, png), args.render, "%s_%s" % (args.chapter, spec["model"]), atlas)
    vb.report(obj, glb, renders)


main(SPEC)
