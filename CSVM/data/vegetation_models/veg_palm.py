"""The palm builder palmtree1.py, palmtree2.py and palmtree3.py share: each holds one card species'
numbers (a SPEC dict) and calls main(SPEC).

A palm is a tapered trunk, a crown core where the frond bases meet, and pinnate fronds radiating
from it. A frond is a strip folded down along its rachis (the leaflets hang either side), bent
down progressively along its length, so the upper fronds arch and the lower ones weep; its
leaflets are drawn into the atlas with gaps between them, cut by the alpha. Optional parts: a
skirt of dead fronds hanging down the trunk under the crown, and nuts under the crown. The
normals point out from the crown centre and up, so the crown shades as one volume.

The cards' base patch (yellow-green ground cover around the trunk) is not modelled: the ground
under the stamp supplies it.

The atlas, 256 x 256, v up: frond strip A (v 0 to 0.375), frond strip B (0.375 to 0.75: dead
fronds for a species with a skirt, else frond_b or a second strip like A), then the bark, core and nut
patches side by side. Usage and conventions: veg_blender.py.
"""

import math
import os

import bmesh
import numpy as np
from mathutils import Vector

import veg_blender as vb

SIZE = 256
FROND_A = (0.0, 0.375)
FROND_B = (0.375, 0.75)
PATCH_V = 0.75
BARK_U = (0.0, 0.5)
CORE_U = (0.5, 0.75)
NUT_U = (0.75, 1.0)

# Rows along a frond, as fractions of its length.
FROND_ROWS = (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)
SKIRT_ROWS = (0.0, 0.35, 0.7, 1.0)


def envelope(s):
    """Leaflet reach along a frond (0 at its base, 1 at its tip): short at the base, longest a
    third along, tapering to the tip. Peaks near 1."""
    return min(1.0, (s + 0.06) / 0.38) ** 0.7 * max(0.0, 1.0 - s) ** 0.6 / 0.75


def strip_width(s):
    """The strip's half width over the frond's, at s: the envelope with a floor, so the tip quad keeps
    an area and every leaflet the texture draws at s falls inside it."""
    return max(0.25, min(1.0, envelope(s)))


def shade(cols, light):
    """cols[0] at 0, cols[1] at 0.5, cols[2] at 1."""
    dark, mid, high = (np.array(c, np.float32) for c in cols)
    light = min(1.0, max(0.0, light))
    if light < 0.5:
        return dark + (mid - dark) * (light / 0.5)
    return mid + (high - mid) * ((light - 0.5) / 0.5)


def _line(img, x0, y0, x1, y1, col, thick, y_lo, y_hi):
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 2
    xs = np.round(np.linspace(x0, x1, n)).astype(int)
    ys = np.round(np.linspace(y0, y1, n)).astype(int)
    for dy in range(thick):
        yy = ys + dy
        # Pixels past the band are dropped, not clamped, so no edge row fills in.
        keep = (yy >= y_lo) & (yy < y_hi) & (xs >= 0) & (xs < SIZE)
        img[yy[keep], xs[keep], :3] = col
        img[yy[keep], xs[keep], 3] = 1.0


def draw_frond(img, band, frond, rng):
    """Leaflets either side of a rachis along the band, slanted toward the tip."""
    y_lo, y_hi = int(band[0] * SIZE), int(band[1] * SIZE)
    cy = (y_lo + y_hi) / 2 - 0.5
    half = (y_hi - y_lo) / 2 - 1
    cols = frond["cols"]
    # Clear texels carry the mid colour, so a mip level's edges do not darken.
    img[y_lo:y_hi, :, :3] = np.array(cols[1], np.float32)
    leaflets = []
    for x in np.arange(2.0, SIZE * 0.97, frond["step"]):
        s = x / SIZE
        for side in (-1, 1):
            if rng.uniform() < frond["gaps"]:
                continue
            k = rng.uniform(0.8, 1.0)
            a = math.radians(rng.uniform(*frond["angle"]))
            # The leaflet's tip, not its base, lies on the envelope, so the frond tapers to a point.
            reach = half * min(1.0, envelope(s)) * k
            for _ in range(3):
                reach = half * min(1.0, envelope(min(1.0, s + reach / math.tan(a) / SIZE))) * k
            light = frond["light"] + rng.uniform(-frond["spread"], frond["spread"])
            if rng.uniform() < frond["high"]:
                light = rng.uniform(0.8, 1.0)
            leaflets.append((light, x, side * reach, reach / math.tan(a)))
    # The brightest leaflets last, so the highlights sit on top of the shade.
    for light, x, dy, dx in sorted(leaflets):
        _line(img, x, cy, x + dx, cy + dy, shade(cols, light), frond["thick"], y_lo, y_hi)
    _line(img, 0, cy, SIZE * 0.95, cy, np.array(frond["rachis"], np.float32), 2, y_lo, y_hi)


def draw_texture(spec, rng):
    img = np.zeros((SIZE, SIZE, 4), np.float32)
    draw_frond(img, FROND_A, spec["frond"], rng)
    # Strip B: the dead fronds of a skirt, else frond_b (a species whose sunlit fronds read as whole lit
    # surfaces, palmtree1), else a second strip like A.
    draw_frond(img, FROND_B, spec["skirt"]["frond"] if "skirt" in spec else spec.get("frond_b", spec["frond"]), rng)

    y0 = int(PATCH_V * SIZE)
    rows, cols = np.mgrid[0:SIZE - y0, 0:SIZE].astype(np.float32)
    noise = rng.uniform(-1, 1, rows.shape).astype(np.float32)

    # Bark: diamond leaf-base scars, or rings, over vertical streaks.
    b = spec["bark"]
    bu0, bu1 = int(BARK_U[0] * SIZE), int(BARK_U[1] * SIZE)
    x, y = cols[:, bu0:bu1], rows[:, bu0:bu1]
    if b["pattern"] == "diamond":
        # Leaf-base scars: rows of crescents, each row shifted, lit on their upper lip.
        row = np.floor(y / 6)
        phase = (x / 9 + 0.5 * row) % 1
        t = np.clip(1.2 * ((y % 6) / 6) - 0.5 * np.abs(phase - 0.5), 0, 1)
    else:
        t = 0.5 + 0.5 * np.cos(2 * math.pi * y / b["ring_px"]) ** 8
        t = 1 - t * 0.6
    streak = rng.uniform(-1, 1, (1, bu1 - bu0)).astype(np.float32)
    t = np.clip(t + 0.12 * streak + 0.2 * noise[:, bu0:bu1], 0, 1)[..., None]
    dark, light = np.array(b["dark"], np.float32), np.array(b["light"], np.float32)
    img[y0:, bu0:bu1, :3] = dark + (light - dark) * t
    img[y0:, bu0:bu1, 3] = 1.0

    # Core and nuts: opaque noise between two colours.
    for (u0, u1), key in ((CORE_U, "core"), (NUT_U, "nut")):
        c0, c1 = (np.array(c, np.float32) for c in spec[key])
        a, z = int(u0 * SIZE), int(u1 * SIZE)
        t = np.clip(0.5 + 0.5 * noise[:, a:z], 0, 1)[..., None]
        img[y0:, a:z, :3] = c0 + (c1 - c0) * t
        img[y0:, a:z, 3] = 1.0
    return img


def build(spec, mat, rng):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    normals = {}
    up = Vector((0, 0, 1))

    def vert(p, n):
        v = bm.verts.new(p)
        normals[v] = Vector(n).normalized()
        return v

    def face(vs, uvs):
        f = bm.faces.new(vs)
        for loop, c in zip(f.loops, uvs):
            loop[uv].uv = c

    t_ = spec["trunk"]
    crown_z = spec["crown_z"]
    lean = t_.get("lean", 0.0)
    axis = lambda t: Vector((lean * t ** 2, 0, crown_z * t))
    trunk_r = lambda t: t_["r_top"] + (t_["r_base"] - t_["r_top"]) * (1 - t) + t_["r_base"] * t_.get("swell", 0.0) * (1 - t) ** 6
    centre = axis(1.0)

    # Trunk: a tapered tube up its centre line to the crown centre. The bark repeats `repeats` times
    # up it, through loop UVs, so the verts stay shared.
    sides, segs, reps = t_["sides"], t_["segs"], t_["repeats"]
    rings = []
    for i in range(segs + 1):
        t = i / segs
        row = []
        for k in range(sides + 1):
            a = 2 * math.pi * k / sides
            d = Vector((math.cos(a), math.sin(a), 0))
            row.append(vert(axis(t) + d * trunk_r(t), d))
        rings.append(row)
    bu = lambda k: BARK_U[0] + (BARK_U[1] - BARK_U[0]) * k / sides
    per = segs / reps
    for i in range(segs):
        v0 = PATCH_V + (1 - PATCH_V) * ((i % per) / per)
        v1 = PATCH_V + (1 - PATCH_V) * ((i % per + 1) / per)
        for k in range(sides):
            face([rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k]],
                 [(bu(k), v0), (bu(k + 1), v0), (bu(k + 1), v1), (bu(k), v1)])

    # Core: a bulb round the crown centre where the frond bases meet, so no hole shows between them.
    c = spec["core_r"]
    shape = [(-1.6 * c, trunk_r(1.0)), (-0.8 * c, c), (0.0, c), (0.6 * c, 0.6 * c)]
    lathe = []
    for dz, r in shape:
        row = []
        for k in range(sides + 1):
            a = 2 * math.pi * k / sides
            d = Vector((math.cos(a), math.sin(a), 0))
            row.append(vert(centre + d * r + Vector((0, 0, dz)), d + up * 0.5))
        lathe.append(row)
    top = vert(centre + Vector((0, 0, c)), up)
    cu = lambda k: CORE_U[0] + (CORE_U[1] - CORE_U[0]) * k / sides
    cv = lambda i: PATCH_V + (1 - PATCH_V) * i / len(shape)
    for i in range(len(shape) - 1):
        for k in range(sides):
            face([lathe[i][k], lathe[i][k + 1], lathe[i + 1][k + 1], lathe[i + 1][k]],
                 [(cu(k), cv(i)), (cu(k + 1), cv(i)), (cu(k + 1), cv(i + 1)), (cu(k), cv(i + 1))])
    for k in range(sides):
        face([lathe[-1][k], lathe[-1][k + 1], top], [(cu(k), cv(3)), (cu(k + 1), cv(3)), (cu(k), 1.0)])

    def frond(base, bearing, elev, bend, length, half_w, fold, band, srows):
        """A strip along a rachis that leaves `base` at `elev` above the horizon toward `bearing` and
        turns down by `bend` radians over its length; its sides hang by `fold` of the half width."""
        dh = Vector((math.cos(bearing), math.sin(bearing), 0))
        side = Vector((-dh.y, dh.x, 0))
        p, s_prev, grid = base.copy(), 0.0, []
        for s in srows:
            # Integrate the rachis in small steps from the previous row.
            steps = max(1, int((s - s_prev) * 20))
            for j in range(steps):
                sm = s_prev + (s - s_prev) * (j + 0.5) / steps
                th = max(elev - bend * sm ** 1.5, -1.5)
                p += (dh * math.cos(th) + up * math.sin(th)) * (length * (s - s_prev) / steps)
            s_prev = s
            th = max(elev - bend * s ** 1.5, -1.5)
            tang = dh * math.cos(th) + up * math.sin(th)
            lift = tang.cross(side)
            if lift.z < 0:
                lift = -lift
            hw = half_w * strip_width(s)
            out = (p - centre).normalized()
            row = []
            for t in (-1, 0, 1):
                q = p + side * (hw * t) - lift * (fold * hw * abs(t))
                q.z = max(q.z, 0.1)
                row.append(vert(q, out + up * (0.6 if t == 0 else 0.3)))
            grid.append((s, hw / half_w, row))
        vlo, vhi = band
        vmid, vhalf = (vlo + vhi) / 2, (vhi - vlo) / 2
        for j in range(len(grid) - 1):
            (s0, g0, ra), (s1, g1, rb) = grid[j], grid[j + 1]
            for t in range(2):
                va = [vmid + vhalf * g0 * (t - 1 + k) for k in (0, 1)]
                vb_ = [vmid + vhalf * g1 * (t - 1 + k) for k in (0, 1)]
                face([ra[t], ra[t + 1], rb[t + 1], rb[t]],
                     [(s0 * 0.97, va[0]), (s0 * 0.97, va[1]), (s1 * 0.97, vb_[1]), (s1 * 0.97, vb_[0])])

    # Fronds: bearings on a jittered golden-angle spiral; elevations even in angle from the top frond
    # down to the lowest, the lower ones bent further, so the crown is a dome that weeps.
    fr = spec["fronds"]
    n = fr["count"]
    two_bands = "skirt" not in spec
    for i in range(n):
        # elev_pow above 1 puts more fronds in the upright tuft and fewer in the wide arching middle.
        f = ((i + rng.uniform(0.2, 0.8)) / n) ** fr.get("elev_pow", 1.0)
        elev = math.radians(fr["elev"][0] + (fr["elev"][1] - fr["elev"][0]) * f)
        bearing = i * 2.39996 + rng.uniform(-0.3, 0.3)
        # Only the topmost fronds stand straight; the bend grows fast below them, so the rest arch over.
        bend = (fr["bend"][0] + (fr["bend"][1] - fr["bend"][0]) * f ** 0.5) * rng.uniform(0.85, 1.15)
        length = (fr["length"][0] + (fr["length"][1] - fr["length"][0]) * f) * rng.uniform(0.88, 1.08)
        dh = Vector((math.cos(bearing), math.sin(bearing), 0))
        base = centre + dh * (c * 0.6) + up * (c * 0.3 * (1 - f))
        # With a skirt, the oldest (lowest) fronds are dead too, on strip B.
        dead = not two_bands and f > spec["skirt"]["dead_from"]
        band = FROND_B if (two_bands and i % 2) or dead else FROND_A
        # The young fronds of the tuft are still half folded, so they stand slim.
        frond(base, bearing, elev, bend, length, length * fr["width"] * (0.4 + 0.6 * min(1.0, f / 0.25)), fr["fold"] * rng.uniform(0.8, 1.2), band, FROND_ROWS)

    # Skirt: dead fronds hanging down the trunk under the crown.
    sk = spec.get("skirt")
    if sk:
        for i in range(sk["count"]):
            bearing = i * 2.39996 + 1.0 + rng.uniform(-0.3, 0.3)
            dh = Vector((math.cos(bearing), math.sin(bearing), 0))
            base = centre + dh * (c * 0.8) - up * (c * rng.uniform(0.4, 1.0))
            length = sk["length"] * rng.uniform(0.8, 1.1)
            frond(base, bearing, math.radians(rng.uniform(*sk["elev"])), 0.2, length, length * sk["width"], 0.3, FROND_B, SKIRT_ROWS)

    # Nuts: a cluster of octahedra under the crown.
    nu = spec.get("nuts")
    if nu:
        mid = ((NUT_U[0] + NUT_U[1]) / 2, (PATCH_V + 1) / 2)
        for i in range(nu["count"]):
            a = 2 * math.pi * i / nu["count"] + rng.uniform(-0.3, 0.3)
            o = centre + Vector((math.cos(a), math.sin(a), 0)) * (c * 0.9) - up * (c * rng.uniform(0.6, 1.1))
            r = nu["r"] * rng.uniform(0.85, 1.1)
            axes = [Vector(v) for v in ((1, 0, 0), (0, 1, 0), (-1, 0, 0), (0, -1, 0))]
            ring = [vert(o + d * r, d) for d in axes]
            poles = [vert(o + up * r, up), vert(o - up * r, -up)]
            for k in range(4):
                a_, b_ = ring[k], ring[(k + 1) % 4]
                face([a_, b_, poles[0]], [(NUT_U[0], PATCH_V), (NUT_U[1], PATCH_V), mid])
                face([b_, a_, poles[1]], [(NUT_U[1], PATCH_V), (NUT_U[0], PATCH_V), mid])

    bm.verts.index_update()
    ordered = [normals[v] for v in bm.verts]
    return vb.mesh_object(spec["name"], bm, ordered, mat)


def main(spec):
    args = vb.parse_args(list(spec["cards"]))
    w, h = spec["cards"][args.chapter]
    name = os.path.splitext(spec["model"])[0]
    spec = dict(spec, name=name)
    vb.reset_scene()
    rng = np.random.default_rng(spec["seed"])
    atlas = vb.make_image(name + "_atlas", draw_texture(spec, rng))
    mat = vb.atlas_material(name, atlas)
    obj = build(spec, mat, rng)
    glb = vb.export_glb(obj, args.out, args.chapter, name)
    renders = []
    if args.render:
        card = vb.read_card(args.data_root, args.chapter, spec["model"])
        if abs(card[0] - w) > 0.01 or abs(card[1] - h) > 0.01:
            raise SystemExit("card is %.3f x %.3f m, CARDS says %.3f x %.3f" % (card[0], card[1], w, h))
        renders = vb.compare_render(obj, card, args.render, "%s_%s" % (args.chapter, name), atlas)
    vb.report(obj, glb, renders)
