"""The fir builder firtree1.py, firtree2.py and dougfirtree1.py share: each holds one card species'
numbers (a SPEC dict) and calls main(SPEC).

A fir is a trunk, a foliage core up toward the leader (so no bare trunk shows up the crown where the
card shows none), and many small drooping clumps scattered up the crown on a golden-angle spiral,
whose tips trace the card's silhouette (PROFILE: crown radius over card width at a height fraction).
A species with `tiers` gathers its clumps into whorls with gaps between them, as an open-grown fir
shows its trunk between branch tiers. A clump is lit on its ridge and highlighted toward its tip,
dark where its sides hang. The normals point out from the trunk axis and up, so the crown shades as
one volume. The texture is drawn here with numpy (needle strokes along twigs, a needle-noise patch
for the core, a bark patch), so the model carries no pixel of the original.

The atlas, 256 x 256, v up: the frond strip below (v 0 to 0.75), the core and bark patches above.
Usage and conventions: veg_blender.py.
"""

import math
import os

import bmesh
import numpy as np
from mathutils import Vector

import veg_blender as vb

FROND_V = 0.75
CORE_U = (0.0, 0.5)
BARK_U = (0.5, 1.0)
SIZE = 256

# What a SPEC leaves out takes firtree1's values.
DEFAULTS = {
    # Needle light on a clump: base, falloff across it, gain toward its tip, and the highlight odds.
    "needle": {"base": 0.5, "across": 0.6, "tip": 0.15, "high": (0.03, 0.3)},
    "trunk": {"r_base": 0.22, "r_top": 0.12, "top": 0.4, "colour": (0.20, 0.14, 0.09)},
    # Core rings (height fractions), its radius over the crown's (shrinking by `taper` up the crown),
    # and where the cone closes.
    "core": {"fs": (0.06, 0.2, 0.4, 0.6, 0.75, 0.87), "r": 0.4, "taper": 0.15, "apex": 0.92},
    # Clumps: the crown span they fill, the weight floor (a share of the width) that keeps the top's share,
    # the height fraction above which they stand up and droop less, and a factor on every clump's droop.
    "clumps": {"span": (0.03, 0.94), "floor": 0.2, "top": 0.8, "half_width": (0.32, 0.45), "reach": (0.8, 1.12), "droop": 1.0},
    "leader": 0.7,
}


def _merged(spec):
    out = dict(spec)
    for k, v in DEFAULTS.items():
        out[k] = dict(v, **spec.get(k, {})) if isinstance(v, dict) else spec.get(k, v)
    return out


def radius(spec, f, w):
    fs, rs = zip(*spec["profile"])
    return float(np.interp(f, fs, rs)) * w


def needle_colour(spec, light):
    """dark at 0, mid at 0.5, high at 1."""
    dark, mid, high = (np.array(c, np.float32) for c in spec["colours"])
    light = min(1.0, max(0.0, light))
    if light < 0.5:
        return dark + (mid - dark) * (light / 0.5)
    return mid + (high - mid) * ((light - 0.5) / 0.5)


def draw_texture(spec, rng):
    nd = spec["needle"]
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    img[..., :3] = np.array(spec["colours"][0], np.float32)
    fh = int(SIZE * FROND_V)
    cy = fh / 2
    half = fh / 2 - 2

    # A clump widens out to a rounded end, so it reads as a hanging mass, not a leaf.
    def envelope(x):
        s = x / SIZE
        return half * min(1.0, s / 0.35) ** 0.7 * max(0.0, 1 - s) ** 0.3

    def stroke(x0, y0, x1, y1, col, thick):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 2
        xs = np.round(np.linspace(x0, x1, n)).astype(int)
        ys = np.round(np.linspace(y0, y1, n)).astype(int)
        for dy in range(thick):
            yy = ys + dy
            # Pixels past the frond region are dropped, not clamped, so no edge row fills in.
            keep = (yy >= 0) & (yy < fh) & (xs >= 0) & (xs < SIZE)
            img[yy[keep], xs[keep], :3] = col
            img[yy[keep], xs[keep], 3] = 1.0

    # Twigs: the main one along the frond, side twigs of random reach, so the outline is ragged
    # where they end rather than a cut leaf shape. Drawn edge first, so the lit ridge stays on top.
    twigs = []
    for x in np.arange(4, SIZE * 0.92, 6):
        for side in (-1, 1):
            reach = SIZE * rng.uniform(0.12, 0.26)
            end = envelope(x + reach) * rng.uniform(0.5, 0.88)
            twigs.append((x, cy, x + reach, cy + side * end))
    twigs.append((0.0, cy, SIZE - 1.0, cy))
    needles = []
    for (x0, y0, x1, y1) in twigs:
        length = math.hypot(x1 - x0, y1 - y0)
        ang = math.atan2(y1 - y0, x1 - x0)
        twig_light = rng.uniform(-0.12, 0.12)
        for t in np.linspace(0, 1, max(3, int(length / 1.5))):
            px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for side in (-1, 1):
                a = ang + side * math.radians(rng.uniform(40, 80))
                ln = rng.uniform(4, 9)
                # Lit on the ridge, dark where the sides hang; the highlights are specks of new
                # growth, most toward the tips and on the ridge.
                across = abs(py - cy) / half
                tip = px / SIZE
                light = nd["base"] - nd["across"] * across + nd["tip"] * tip + 0.1 * t + twig_light + rng.uniform(-0.15, 0.15)
                if rng.uniform() < nd["high"][0] + nd["high"][1] * tip * t * (1 - across):
                    light = rng.uniform(0.8, 1.0)
                needles.append((light, px, py, px + math.cos(a) * ln, py + math.sin(a) * ln))
    # The brightest needles last, so the highlights sit on top of the shade.
    for light, x0, y0, x1, y1 in sorted(needles):
        stroke(x0, y0, x1, y1, needle_colour(spec, light), 2)

    # Core patch: opaque needle noise in the shade colours with a few lit specks, so the gaps
    # between clumps read as deeper foliage, not as a trunk.
    cu0, cu1 = int(CORE_U[0] * SIZE), int(CORE_U[1] * SIZE)
    light = rng.uniform(0.0, 0.4, (SIZE - fh, cu1 - cu0))
    light[rng.uniform(size=light.shape) < 0.04] = 0.75
    img[fh:, cu0:cu1, :3] = np.array([[needle_colour(spec, v) for v in row] for row in light])
    img[fh:, cu0:cu1, 3] = 1.0
    # Bark patch: brown with vertical streaks.
    bu0, bu1 = int(BARK_U[0] * SIZE), int(BARK_U[1] * SIZE)
    streak = rng.uniform(0.7, 1.2, (1, bu1 - bu0, 1)).astype(np.float32)
    img[fh:, bu0:bu1, :3] = np.array(spec["trunk"]["colour"], np.float32) * streak
    img[fh:, bu0:bu1, 3] = 1.0
    return img


def build(spec, w, h, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    tr, co, cl = spec["trunk"], spec["core"], spec["clumps"]

    def vert(p, n):
        v = bm.verts.new(p)
        normals[v] = Vector(n).normalized()
        return v

    def face(vs, uvs):
        f = bm.faces.new(vs)
        for loop, c in zip(f.loops, uvs):
            loop[uv].uv = c

    def crown_normal(p, fallback, bend=0.0):
        r = Vector((p[0], p[1], 0))
        r = r.normalized() if r.length > 0.3 else fallback
        return r + Vector((0, 0, 0.7 + bend))

    # Slimmer up the crown, where the clumps are short and would leave a wide core showing.
    core_r = lambda f: radius(spec, f, w) * (co["r"] - co["taper"] * max(0.0, f - 0.4) / 0.6)

    # Trunk: a six-sided tapered tube, inside the core. A mossy one takes the core's needle patch.
    sides, top = 6, tr["top"] * h
    tu = CORE_U if tr.get("moss") else BARK_U
    ring = []
    for k in range(sides + 1):
        a = 2 * math.pi * k / sides
        d = Vector((math.cos(a), math.sin(a), 0))
        ring.append((vert(d * tr["r_base"], d), vert(d * tr["r_top"] + Vector((0, 0, top)), d), tu[0] + (tu[1] - tu[0]) * k / sides))
    for k in range(sides):
        (b0, t0, u0), (b1, t1, u1) = ring[k], ring[k + 1]
        face([b0, b1, t1, t0], [(u0, FROND_V), (u1, FROND_V), (u1, 1), (u0, 1)])

    # Core: a foliage lathe at a fraction of the crown, up to the apex, so gaps between clumps read
    # as deeper foliage, not see-through and not bare trunk.
    fs = co["fs"]
    rings = []
    for f in fs:
        row = []
        for k in range(sides + 1):
            a = 2 * math.pi * k / sides
            d = Vector((math.cos(a), math.sin(a), 0))
            p = d * core_r(f) + Vector((0, 0, f * h))
            row.append(vert(p, crown_normal(p, d)))
        rings.append(row)
    # The core stops short of the apex: the leader's ragged frond, not an opaque spike, ends the crown.
    apex = vert((0, 0, co["apex"] * h), (0, 0, 1))
    cu = lambda k: CORE_U[0] + (CORE_U[1] - CORE_U[0]) * k / sides
    cv = lambda i: FROND_V + (1 - FROND_V) * i / len(fs)
    for i in range(len(fs) - 1):
        for k in range(sides):
            face([rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]],
                 [(cu(k), cv(i)), (cu(k + 1), cv(i)), (cu(k + 1), cv(i + 1)), (cu(k), cv(i + 1))])
    for k in range(sides):
        face([rings[-1][k], rings[-1][k + 1], apex], [(cu(k), cv(len(fs) - 1)), (cu(k + 1), cv(len(fs) - 1)), (cu(k), 1)])

    def clump(base, d, length, half_width, lift, droop, roll):
        """A drooping frond quad grid from base out along d: lifted at first, then hanging."""
        side = Vector((-d.y, d.x, 0))
        grid = []
        for s, width in ((0.0, 0.45), (0.45, 1.0), (1.0, 0.75)):
            c = base + d * (length * s) + Vector((0, 0, length * (lift * s - droop * s * s)))
            hw = half_width * width
            row = []
            for t in (-1, 0, 1):
                # The sides hang, one more than the other by the clump's roll.
                sag = Vector((0, 0, -(0.8 + roll * t) * hw)) if t else Vector()
                p = c + side * (hw * t) + sag
                # A low clump that would reach below the ground rests on it instead.
                p.z = max(p.z, 0.1)
                row.append(vert(p, crown_normal(p, d, -0.8 if t else 0.3)))
            grid.append((s, row))
        vs = (0.0, FROND_V / 2, FROND_V)
        for j in range(2):
            (s0, r_a), (s1, r_b) = grid[j], grid[j + 1]
            for t in range(2):
                face([r_a[t], r_a[t + 1], r_b[t + 1], r_b[t]],
                     [(s0, vs[t]), (s0, vs[t + 1]), (s1, vs[t + 1]), (s1, vs[t])])

    # Clumps: spread up the crown by the crown's girth plus a floor, so the top keeps its share,
    # each at its own height and bearing (a golden-angle spiral, jittered), so no tiers form unless
    # the species has them.
    grid_f = np.linspace(cl["span"][0], cl["span"][1], 200)
    weight = np.array([radius(spec, f, w) for f in grid_f]) + cl["floor"] * w
    if "tiers" in spec:
        # Whorls: each a narrow band of clumps; the share left between them is the gap's.
        tiers = spec["tiers"]
        weight = weight * (tiers["gap"] + sum(np.exp(-0.5 * ((grid_f - f) / tiers["width"]) ** 2) for f in tiers["at"]))
    cdf = np.cumsum(weight)
    cdf = (cdf - cdf[0]) / (cdf[-1] - cdf[0])
    n = spec["count"]
    for i in range(n):
        f = float(np.interp((i + rng.uniform(0.2, 0.8)) / n, cdf, grid_f))
        a = i * 2.39996 + rng.uniform(-0.4, 0.4)
        d = Vector((math.cos(a), math.sin(a), 0))
        r0 = core_r(f) * 0.6
        # Drooping tips land lower, where the crown is wider: aim at the profile a little below.
        reach = radius(spec, max(0.0, f - 0.03), w) * rng.uniform(*cl["reach"]) - r0
        length = max(0.8, reach)
        top = f > cl["top"]
        lift = rng.uniform(0.25, 0.45) if top else rng.uniform(0.1, 0.3)
        droop = (rng.uniform(0.25, 0.4) if top else rng.uniform(0.45, 0.8)) * (1 + 0.3 * (1 - f)) * cl["droop"]
        half_width = length * rng.uniform(*cl["half_width"]) + (0.45 if top else 0.25)
        base = d * r0 + Vector((0, 0, f * h))
        clump(base, d, length, half_width, lift, droop, rng.uniform(-0.25, 0.25))

    # The leader: two crossed fronds standing up from the core's top, so the crown ends in a soft
    # spire of foliage.
    f0 = fs[-1] - 0.02
    for a in (0.3, 0.3 + math.pi / 2):
        d = Vector((math.cos(a), math.sin(a), 0))
        b = [vert(d * (spec["leader"] * t) + Vector((0, 0, f0 * h)), crown_normal(d * t, d)) for t in (-1, 1)]
        tip = vert((0, 0, spec["apex"] * h), d + Vector((0, 0, 0.7)))
        # Its edges cross the frond's ragged sides, so the spire's outline is ragged, not cut.
        face([b[0], b[1], tip], [(0.3, 0.0), (0.3, FROND_V), (0.9, FROND_V / 2)])

    bm.verts.index_update()
    ordered = [normals[v] for v in bm.verts]
    return vb.mesh_object(spec["name"], bm, ordered, mat)


def main(spec):
    """Writes the model and its clump tiles for the chapter asked for, and with --render the comparison."""
    args = vb.parse_args(list(spec["cards"]))
    w, h = spec["cards"][args.chapter]
    name = os.path.splitext(spec["model"])[0]
    spec = _merged(dict(spec, name=name))
    # Colours per chapter where one card name carries another picture's colours (C2's and C4's spruce).
    if isinstance(spec["colours"], dict):
        spec["colours"] = spec["colours"][args.chapter]
    vb.reset_scene()
    rng = np.random.default_rng(spec["seed"])
    atlas = vb.make_image(name + "_atlas", draw_texture(spec, rng))
    mat = vb.atlas_material(name, atlas)
    obj = build(spec, w, h, mat, rng)
    glb = vb.export_glb(obj, args.out, args.chapter, name)
    tiles = vb.clump_render(obj, w, h, args.out, args.chapter, name, spec["clump_trees"], 2, spec["clump_grade"][args.chapter], args.render)
    renders = []
    if args.render:
        card = vb.read_card(args.data_root, args.chapter, spec["model"])
        if abs(card[0] - w) > 0.01 or abs(card[1] - h) > 0.01:
            raise SystemExit("card is %.3f x %.3f m, CARDS says %.3f x %.3f" % (card[0], card[1], w, h))
        renders = vb.compare_render(obj, card, args.render, "%s_%s" % (args.chapter, name), atlas)
    vb.report(obj, glb, tiles + renders)
