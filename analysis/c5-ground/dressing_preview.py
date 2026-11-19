"""Offline montages of C5's ground dressing models (B14), for the author's eyes: every node of every
gd_* model in CSVM/data/city_models/C5 and the city tree in CSVM/data/vegetation_models/C5, at one
scale, under analysis/city-hulls/preview.py's flat key light and under its dim night stand-in over a
block floor at texture luma 20 (Decision 3), plus a courtyard composed of them. C1's bush1 and bush2,
the hedge candidates, stand beside the tree at a hedge's scale. No original pixel is drawn, but the
sheets go outside the repo like the city previews.

    py -I analysis/c5-ground/dressing_preview.py <out dir> [--scale=S]

Writes dressing_neutral.png, dressing_night.png, yard_neutral.png and yard_night.png.
"""
import io
import json
import os
import struct
import sys

sys.dont_write_bytecode = True

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "city-hulls"))
import margin as M  # noqa: E402
import preview as P  # noqa: E402
import render as R  # noqa: E402

A = P.A
VEG = os.path.join(os.path.dirname(M.MODELS), "vegetation_models")
KINDS = ["gd_wall", "gd_fence", "gd_hedge", "gd_shed", "gd_sedan", "gd_truck", "gd_ashcans", "gd_bench"]
# The block floor's texture luma (Decision 3) as a flat grey, the ground under the night views.
FLOOR = np.array((21, 20, 18)) / 255.0
NIGHT_BG = (8, 10, 18)


def night_light(n):
    """preview.night_colour's stand-in for C5's night: a cool ambient and a weak moon key."""
    return np.array((0.42, 0.46, 0.58)) + 0.35 * max(0.0, float(n @ R.LIGHT))


def hull_colour(factor, row, night):
    """preview.flat_colour's albedo (grade, ungraded metal, poster gain, the lattice's holes), lit by the flat key or
    the night stand-in."""
    flat = P.flat_colour(factor, row)

    def colour(i, vals, n):
        if not night:
            return flat(i, vals, n)
        x, y = P.texel(vals[:, 0], vals[:, 1], vals[:, 2], vals[:, 3], row)
        grade = np.where((vals[:, 3] > 1.5)[:, None], 1.0, np.array(factor)[None, :])
        grade = np.where(((vals[:, 3] > 3.5) & (vals[:, 2] < A.PORTRAIT + 0.5))[:, None], P.POSTER_GAIN, grade)
        return P.srgb_to_lin(P.ATLAS[y, x, :3] / 255.0) * grade * night_light(n)
    return colour


def read_veg(path):
    """A vegetation GLB's one mesh: positions (n, 3), uv (n, 2), triangles (m, 3) and its texture (H, W, 4) 0..1."""
    data = open(path, "rb").read()
    jlen = struct.unpack("<I", data[12:16])[0]
    j = json.loads(data[20:20 + jlen])
    bin_ = data[20 + jlen + 8:]
    comps = {5126: np.float32, 5123: np.uint16, 5125: np.uint32}
    width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3}

    def acc(i):
        a = j["accessors"][i]
        bv = j["bufferViews"][a["bufferView"]]
        n = a["count"] * width[a["type"]]
        return np.frombuffer(bin_, comps[a["componentType"]], n, bv.get("byteOffset", 0) + a.get("byteOffset", 0))

    prim = j["meshes"][0]["primitives"][0]
    at = prim["attributes"]
    bv = j["bufferViews"][j["images"][0]["bufferView"]]
    img = Image.open(io.BytesIO(bin_[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]])).convert("RGBA")
    return (acc(at["POSITION"]).reshape(-1, 3).astype(float), acc(at["TEXCOORD_0"]).reshape(-1, 2).astype(float),
            acc(prim["indices"]).reshape(-1, 3), np.asarray(img) / 255.0)


def veg_tris(path, scale=1.0, offset=(0, 0, 0)):
    """Triangles both ways round (the material is double sided), uv as the attributes, and the texture."""
    pos, uv, tri, tex = read_veg(path)
    tri = np.concatenate([tri, tri[:, ::-1]])
    return pos[tri] * scale + np.array(offset), np.pad(uv[tri], ((0, 0), (0, 0), (0, 2))), tex


def veg_colour(tex, night):
    h, w = tex.shape[:2]

    def colour(i, vals, n):
        x = np.clip((vals[:, 0] % 1.0 * w).astype(int), 0, w - 1)
        y = np.clip((vals[:, 1] % 1.0 * h).astype(int), 0, h - 1)
        t = tex[y, x]
        alb = P.srgb_to_lin(t[:, :3])
        out = alb * night_light(n) if night else P.srgb_to_lin(P.lin_to_srgb(alb) * (0.55 + 0.45 * abs(float(n @ R.LIGHT))))
        return np.where((t[:, 3] < 0.5)[:, None], np.nan, out)
    return colour


def ground_tris(x0, x1, z0, z1):
    pts = np.array([[(x0, 0, z0), (x0, 0, z1), (x1, 0, z1)], [(x0, 0, z0), (x1, 0, z1), (x1, 0, z0)]], float)
    return pts, np.zeros((2, 3, 4))


def ground_colour(i, vals, n):
    return np.tile(P.srgb_to_lin(FLOOR) * night_light(n), (len(vals), 1))


class Scene:
    """Triangle sets, each with its own colour function, drawn by preview.raster in one z-buffer."""

    def __init__(self):
        self.P, self.attrs, self.owners = [], [], []

    def add(self, tris, attrs, colour):
        self.P.append(np.asarray(tris, float))
        self.attrs.append(np.asarray(attrs, float))
        self.owners.append((len(tris), colour))

    def draw(self, scale, az, el, bg):
        starts = np.cumsum([0] + [n for n, _ in self.owners])

        def colour(i, vals, n):
            b = int(np.searchsorted(starts, i, side="right") - 1)
            return self.owners[b][1](i - starts[b], vals, n)
        return P.raster(np.concatenate(self.P), np.concatenate(self.attrs), scale, colour, az=az, el=el, bg=bg)


def node_cell(meshes, factor, name, row, night, scale, el):
    P_, attrs = P.model_tris(meshes, name)
    s = Scene()
    if night:
        lo, hi = P_.reshape(-1, 3).min(0), P_.reshape(-1, 3).max(0)
        s.add(*ground_tris(lo[0] - 1, hi[0] + 1, lo[2] - 1, hi[2] + 1), ground_colour)
    s.add(P_, attrs, hull_colour(factor, row, night))
    return s.draw(scale, 35.0, el, NIGHT_BG if night else R.BG), len(P_), P_.reshape(-1, 3)


def veg_cell(path, night, scale, el, size=1.0):
    tris, attrs, tex = veg_tris(path, size)
    s = Scene()
    if night:
        lo, hi = tris.reshape(-1, 3).min(0), tris.reshape(-1, 3).max(0)
        s.add(*ground_tris(lo[0] - 1, hi[0] + 1, lo[2] - 1, hi[2] + 1), ground_colour)
    s.add(tris, attrs, veg_colour(tex, night))
    return s.draw(scale, 35.0, el, NIGHT_BG if night else R.BG), len(tris) // 2, tris.reshape(-1, 3)


def dims(p):
    d = p.max(0) - p.min(0)
    return "%.1fx%.1fx%.1f m" % (d[0], d[2], d[1])


def sheet(out, night, scale):
    el = 35.0 if night else 22.0
    rows = []
    for k, kind in enumerate(KINDS):
        meshes, factor = M.read_glb(os.path.join(M.MODELS, "C5", kind + ".glb.bin"))
        row = []
        for name in sorted(meshes, key=lambda n: (n == "far", n)):
            im, n, p = node_cell(meshes, factor, name, k, night, scale, el)
            row.append(R.label(im, ["%s %s" % (kind, name), "%d tri, %s" % (n, dims(p))]))
        rows.append(row)
    row = []
    for path, size, title in ((os.path.join(VEG, "C5", "citytree.glb.bin"), 1.0, "citytree (near)"),
                              (os.path.join(VEG, "C5", "citytree_far.glb.bin"), 1.0, "citytree_far"),
                              (os.path.join(VEG, "C1", "bush1.glb.bin"), 0.4, "C1 bush1 x0.4 (hedge?)"),
                              (os.path.join(VEG, "C1", "bush2.glb.bin"), 0.4, "C1 bush2 x0.4 (hedge?)")):
        im, n, p = veg_cell(path, night, scale, el, size)
        row.append(R.label(im, [title, "%d tri, %s" % (n, dims(p))]))
    rows.append(row)
    light = ("dim night stand-in (cool ambient, weak moon key, no fog, no lamps) over a floor at texture luma 20, "
             "35 deg down") if night else "flat key light, 22 deg down"
    img = R.sheet(rows, "C5 ground dressing (B14): every node at %.0f px/m, %s; each row a kind, near nodes then far; sizes W x D x H"
                  % (scale, light))
    img.save(os.path.join(out, "dressing_%s.png" % ("night" if night else "neutral")))


# A back court 40 x 28 m: (kind or tree, node, x, z, yaw degrees). Walls and fences ring it, a shed and ash cans
# against the back wall, a sedan and the truck in the yard, a bench under trees in a garden corner.
YARD = [("gd_wall", "near1", -16, -14, 0), ("gd_wall", "near0", -8, -14, 0), ("gd_wall", "near1", 0, -14, 0),
        ("gd_wall", "near0", 8, -14, 0), ("gd_wall", "near1", 16, -14, 0),
        ("gd_fence", "near0", -20, -10, 90), ("gd_fence", "near0", -20, -2, 90), ("gd_fence", "near0", -20, 6, 90),
        ("gd_fence", "near0", 4, 0, 0), ("gd_fence", "near0", 12, 0, 0),
        ("gd_shed", "near0", -14, -11.5, 0), ("gd_shed", "near1", -9, -11.5, 0), ("gd_ashcans", "near0", -5, -13, 0),
        ("gd_sedan", "near0", -6, -4, 15), ("gd_truck", "near0", 4, -8, 0),
        ("gd_hedge", "near0", 6.5, 13, 0), ("gd_hedge", "near1", 9.5, 13, 0), ("gd_hedge", "near0", 12.5, 13, 0),
        ("gd_hedge", "near1", 2.5, 8, 90), ("gd_bench", "near0", 12, 6, 180), ("gd_ashcans", "near0", 18, 1.5, 0),
        ("tree", "citytree", 9, 8, 0), ("tree", "citytree", 16, 10, 70), ("tree", "citytree_far", -12, 8, 0)]


def yaw_pts(p, deg, at):
    a = np.radians(deg)
    c, s = np.cos(a), np.sin(a)
    q = p.copy()
    q[..., 0], q[..., 2] = c * p[..., 0] + s * p[..., 2], -s * p[..., 0] + c * p[..., 2]
    return q + np.array(at)


def yard(out, night, scale=12.0):
    s = Scene()
    s.add(*ground_tris(-22, 22, -16, 14), ground_colour if night else (lambda i, v, n: np.tile(
        P.srgb_to_lin(P.lin_to_srgb(P.srgb_to_lin(FLOOR)) * (0.55 + 0.45 * abs(float(n @ R.LIGHT)))), (len(v), 1))))
    for k, (kind, node, x, z, yaw) in enumerate(YARD):
        if kind == "tree":
            tris, attrs, tex = veg_tris(os.path.join(VEG, "C5", node + ".glb.bin"))
            s.add(yaw_pts(tris, yaw, (x, 0, z)), attrs, veg_colour(tex, night))
            continue
        meshes, factor = M.read_glb(os.path.join(M.MODELS, "C5", kind + ".glb.bin"))
        p, attrs = P.model_tris(meshes, node)
        s.add(yaw_pts(p, yaw, (x, 0, z)), attrs, hull_colour(factor, k, night))
    im = s.draw(scale, 30.0, 40.0, NIGHT_BG if night else R.BG)
    light = "dim night stand-in over a floor at texture luma 20" if night else "flat key light, the floor at luma 20"
    R.sheet([[im]], "A back court of the dressing, 40 x 28 m, %.0f px/m, 40 deg down, %s" % (scale, light)).save(
        os.path.join(out, "yard_%s.png" % ("night" if night else "neutral")))


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    scale = float(next((a[8:] for a in sys.argv[2:] if a.startswith("--scale=")), 20.0))
    for night in (False, True):
        sheet(out, night, scale)
        yard(out, night)


if __name__ == "__main__":
    main()
