"""Generates canopytree, the broadleaf crown C3's near models stand over its painted jungle canopy
(forest1, forest1-128, cliff1), where no card stands, and its two sibling shapes canopytree2 (a
tall, narrow emergent) and canopytree3 (a broad, low crown), which CanopyClumps mixes by a hash so
no two neighbours share a silhouette. Each is a crown and nothing below it: a central dome and a
ring of lower domes round it, each a leafy disc lit at its top and darkening to its rim, broken
into leaf clumps by dark cracks, over normals that shade each dome and clump on its own under the
game's sun term, so it reads as a lumpy crown and not a ball. It stands sunk in the painted canopy
(CanopyClumps places it), so no trunk shows and none is built.

One run writes the three models and one clump strip, canopytree.clump.*, the far crowns: each tile
stands the three shapes as CanopyClumps stands the near crowns (fillers under emergents, sunk so
their tops rise over the painted canopy, each tinted as the game tints a near crown) on a holdout
ground that hides what the canopy hides, seen from VIEW_ELEVATION above, so a crown shows its top
as the near models do from the air. The game grades the strip to the crowns' mean at load, as it
grades the models.

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
import bpy  # noqa: E402
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
    "lumps": 0.28,
    # A vertex normal's mix: the crown's centre (one volume) over the dome's own (lumps), plus a random part,
    # so the game's sun term shades each crown as leaf clumps rather than one ball.
    "normals": {"crown": 0.3, "jitter": 0.5},
    # The disc's light: at its centre, its fall to the rim, the leaf clumps on it (a cellular pattern, each
    # lit its own way, a dark crack this many texels wide between neighbours) and the grain.
    "leaves": {"top": 0.68, "fall": 0.7, "clumps": 14, "clump": 0.18, "crack": 0.45, "crack_px": 6.0, "grain": 0.4, "high": 0.06},
    "swatch": ("C3", "forest1.png", 12.8),
}

# The siblings: the emergent stands taller and narrower, the broad crown wider and flatter.
SPECS = (
    SPEC,
    dict(SPEC, model="canopytree2", seed=32, top={"r": 3.2, "z": 7.0, "squash": 0.95},
         ring={"count": 4, "out": (2.4, 3.2), "r": (2.2, 2.8), "z": (4.0, 5.2), "squash": 0.9}),
    dict(SPEC, model="canopytree3", seed=33, top={"r": 4.6, "z": 2.6, "squash": 0.5},
         ring={"count": 7, "out": (4.0, 5.2), "r": (2.8, 3.6), "z": (1.2, 2.2), "squash": 0.55}),
)

SIZE = 256
CELL = 128
# Disc cells (u0, v0) in UV: lit (the top dome) and three plain ones for the ring.
CELLS = ((0.0, 0.0), (0.5, 0.0), (0.0, 0.5), (0.5, 0.5))
# How CanopyClumps stands a near crown: its top MODEL_RISE_M over the painted canopy at scale 1, an emergent
# (1 to 1.5) at EMERGENT_SHARE, else a filler (0.45 to 0.8), its colour spread by CROWN_BRIGHTNESS overall
# and CROWN_TINT along the greens (CanopyClumps.CrownModels' formula). The strip bakes that spread per crown,
# since one instance tint over a clump's crowns would patch the hill.
MODEL_RISE_M = 6.0
EMERGENT_SHARE = 0.3
CROWN_BRIGHTNESS, CROWN_TINT = 0.3, 0.2
# Crowns a strip tile: about the near crowns' 14 a 32 m cell over a tile's 24 by 16 m.
CLUMP_TREES = (5, 6, 4, 5, 6, 5)
# The strip is the view from this far above the horizon, the pitch the jungle is flown over at: a crown
# shows its top, as the near models do, not the side a fir's tile shows.
VIEW_ELEVATION = 30.0


def shade(cols, light):
    """cols[0] at 0, cols[1] at 0.5, cols[2] at 1, per texel of the light array."""
    dark, mid, high = (np.array(c, np.float32) for c in cols)
    t = np.clip(light, 0.0, 1.0)[..., None]
    return np.where(t < 0.5, dark + (mid - dark) * (t / 0.5), mid + (high - mid) * ((t - 0.5) / 0.5))


def draw_disc(img, x0, y0, spec, lift, rng):
    """One dome's disc: the centre (the dome's top) lit and the rim (its skirt) dark, broken into leaf
    clumps with dark cracks between them, under a fine leaf grain about as fine as the painted canopy's.
    Clear outside it."""
    lv = spec["leaves"]
    c = CELL / 2
    yy, xx = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
    r = np.hypot(xx - c + 0.5, yy - c + 0.5) / (c - 1)
    light = lv["top"] - lv["fall"] * r ** 1.5 + lift
    # The clumps: each texel belongs to its nearest seed, lit by that clump's own draw, and darkens where
    # the second-nearest seed is about as close, the crack. Coarse enough to survive the mips in game.
    seeds = rng.uniform(0, CELL, (lv["clumps"], 2)).astype(np.float32)
    d = np.hypot(xx[..., None] - seeds[:, 0], yy[..., None] - seeds[:, 1])
    order = np.argsort(d, axis=2)
    d1 = np.take_along_axis(d, order[..., :1], 2)[..., 0]
    d2 = np.take_along_axis(d, order[..., 1:2], 2)[..., 0]
    own = rng.uniform(-1, 1, lv["clumps"]).astype(np.float32)[order[..., 0]]
    light += lv["clump"] * own - lv["crack"] * np.clip(1.0 - (d2 - d1) / lv["crack_px"], 0.0, 1.0)
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
            # Out from the dome's own centre, a little from the crown's, and a random part: leaf clumps, not a ball.
            nm = spec["normals"]
            normals[v] = ((p - centre).normalized() * nm["crown"] + (p - o).normalized()
                          + Vector(tuple(rng.uniform(-1, 1, 3))) * nm["jitter"]).normalized()
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


def stand(objs, rng):
    """One crown of a strip tile as CanopyClumps stands a near crown: a shape by a draw, an emergent or a
    filler, sunk so its top stands MODEL_RISE_M over the ground at scale 1, and tinted as the game tints a
    near crown. The game decodes an instance tint from sRGB, so the bake takes the tint to that power."""
    obj = objs[min(len(objs) - 1, int(rng.uniform() * len(objs)))]
    scale = 1 + 0.5 * rng.uniform() if rng.uniform() < EMERGENT_SHARE else 0.45 + 0.35 * rng.uniform()
    b, t = 1 + CROWN_BRIGHTNESS * rng.uniform(-1, 1), CROWN_TINT * rng.uniform(-1, 1)
    tint = tuple(c ** 2.2 for c in (b * (1 + t), b * (1 + 0.3 * t), b * (1 - 0.5 * t))) + (1.0,)
    return obj, scale, obj.dimensions.z - MODEL_RISE_M, tint


def ground():
    """The painted canopy the crowns stand sunk in: a holdout plane at the ground, which cuts each crown's
    sunk part and hides a deeper row's foot, as the painted canopy does in game."""
    me = bpy.data.meshes.new("ground")
    me.from_pydata([(-200, -400, 0), (200, -400, 0), (200, 400, 0), (-200, 400, 0)], [], [(0, 1, 2, 3)])
    mat = bpy.data.materials.new("holdout")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.links.new(nt.nodes.new("ShaderNodeHoldout").outputs[0], next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL").inputs["Surface"])
    me.materials.append(mat)
    obj = bpy.data.objects.new("ground", me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def main(specs):
    args = vb.parse_args(specs[0]["chapters"])
    vb.reset_scene()
    built = []
    for spec in specs:
        rng = np.random.default_rng(spec["seed"])
        atlas = vb.make_image(spec["model"] + "_atlas", draw_texture(spec, rng))
        obj = build(spec, vb.atlas_material(spec["model"], atlas), rng)
        built.append((spec, obj, atlas, vb.export_glb(obj, args.out, args.chapter, spec["model"])))
    objs = [b[1] for b in built]
    plane = ground()
    # The game grades the strip to the crowns' mean at load, so no grade here; the tile is as tall as the
    # tallest emergent's top over the ground plus the back row's rise in the view, the widest crown deep.
    width = max(max(o.dimensions.x, o.dimensions.y) for o in objs)
    tiles = vb.clump_render(objs, width, MODEL_RISE_M * 1.5, args.out, args.chapter, specs[0]["model"], CLUMP_TREES, 2,
                            (1.0, 1.0, 1.0), args.render, stand=lambda rng: stand(objs, rng), elevation=VIEW_ELEVATION)
    bpy.data.objects.remove(plane)
    for spec, obj, atlas, glb in built:
        renders = []
        if args.render:
            for o in objs:
                o.hide_render = o is not obj
            chapter, texture, size = spec["swatch"]
            png = os.path.join(args.data_root, "extracted", chapter, "texture", texture)
            renders = vb.compare_render(obj, (size, size, png), args.render, "%s_%s" % (args.chapter, spec["model"]), atlas)
            # The render's card, ground, sun and camera, cleared before the next model's.
            for o in list(bpy.context.scene.objects):
                if o not in objs:
                    bpy.data.objects.remove(o)
        vb.report(obj, glb, (tiles if obj is objs[0] else []) + renders)


main(SPECS)
