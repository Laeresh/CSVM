"""Contact sheets of C5's original building models and their textures, for A1's montage.

A small numpy rasteriser (orthographic, z-buffered, nearest texel, a fixed key light) draws
each model from census.Chapter's geometry, so no game or Godot launch is needed. The sheets
hold original pixels: write them outside the repo and never commit them.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/city-hulls/render.py <out dir>

Writes t_cblock1..7.png (one row per lot: the a model's variants, the det half's variants, and
the a model with its det01 half as stamped together), cblock7_views.png (each cblock7 model
from two sides) and textures.png (every texture a building model uses, flat).
"""
import math
import os
import sys
from collections import defaultdict

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census as C  # noqa: E402

BG = (26, 28, 36)
LIGHT = np.array([0.45, 0.8, 0.4]) / np.linalg.norm([0.45, 0.8, 0.4])
MAX_SIDE = 2500
SS = 2  # supersampling, then a box filter down
FONT = ImageFont.load_default(size=13)

_tex = {}


def texture(name):
    if name not in _tex:
        p = C.tex_png(name) if name else None
        _tex[name] = np.asarray(Image.open(p).convert("RGB"), dtype=np.float32) if p else None
    return _tex[name]


def camera(az, el):
    a, e = math.radians(az), math.radians(el)
    c = np.array([math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e)])
    right = np.array([math.cos(a), 0.0, -math.sin(a)])
    up = np.cross(c, right)
    return right, up, c


def render(tris, scale, az=35.0, el=22.0, pad=6):
    """One model to an RGB image at `scale` px per metre."""
    right, up, fwd = camera(az, el)
    P = np.array([t[0] for t in tris], dtype=np.float64)          # (n, 3, 3)
    sx, sy, sz = P @ right, P @ up, P @ fwd
    s = scale * SS
    x0, y1 = sx.min(), sy.max()
    W = int(math.ceil((sx.max() - x0) * s)) + 2 * pad * SS
    H = int(math.ceil((y1 - sy.min()) * s)) + 2 * pad * SS
    img = np.zeros((H, W, 3), np.float32)
    img[:] = BG
    zb = np.full((H, W), -np.inf)
    X = (sx - x0) * s + pad * SS
    Y = (y1 - sy) * s + pad * SS
    for i, (_, uv, tex) in enumerate(tris):
        xa, ya = X[i], Y[i]
        ax, bx = int(max(0, math.floor(xa.min()))), int(min(W - 1, math.ceil(xa.max())))
        ay, by = int(max(0, math.floor(ya.min()))), int(min(H - 1, math.ceil(ya.max())))
        if bx < ax or by < ay:
            continue
        gx, gy = np.meshgrid(np.arange(ax, bx + 1) + 0.5, np.arange(ay, by + 1) + 0.5)
        d = (ya[1] - ya[2]) * (xa[0] - xa[2]) + (xa[2] - xa[1]) * (ya[0] - ya[2])
        if abs(d) < 1e-9:
            continue
        w0 = ((ya[1] - ya[2]) * (gx - xa[2]) + (xa[2] - xa[1]) * (gy - ya[2])) / d
        w1 = ((ya[2] - ya[0]) * (gx - xa[2]) + (xa[0] - xa[2]) * (gy - ya[2])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue
        z = w0 * sz[i, 0] + w1 * sz[i, 1] + w2 * sz[i, 2]
        sub = zb[ay:by + 1, ax:bx + 1]
        m = inside & (z > sub)
        if not m.any():
            continue
        n = np.cross(P[i, 1] - P[i, 0], P[i, 2] - P[i, 0])
        nl = np.linalg.norm(n)
        shade = 0.55 + 0.45 * (abs(n @ LIGHT) / nl if nl else 0.0)
        t = texture(tex)
        if t is None:
            col = np.full((m.sum(), 3), 128.0)
        else:
            u = w0 * uv[0][0] + w1 * uv[1][0] + w2 * uv[2][0]
            v = w0 * uv[0][1] + w1 * uv[1][1] + w2 * uv[2][1]
            th, tw = t.shape[:2]
            tx = np.floor(u[m] * tw).astype(int) % tw
            ty = np.floor(v[m] * th).astype(int) % th
            col = t[ty, tx]
        sub[m] = z[m]
        img[ay:by + 1, ax:bx + 1][m] = col * shade
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
    return im.resize((max(1, W // SS), max(1, H // SS)), Image.BOX)


def label(im, lines):
    h = 15 * len(lines) + 4
    out = Image.new("RGB", (max(im.width, 150), im.height + h), BG)
    out.paste(im, (0, 0))
    d = ImageDraw.Draw(out)
    for k, s in enumerate(lines):
        d.text((3, im.height + 2 + 15 * k), s, fill=(225, 225, 225), font=FONT)
    return out


def cell(ch, rows, names, scale, title=None, az=35.0):
    tris = [t for m in names for t in ch.geometry(rows[m]["node"])]
    im = render(tris, scale, az=az)
    if title:
        return label(im, [title])
    r = rows[names[0]]
    return label(im, [names[0].replace(".flt", ""),
                      "%.0fx%.0fx%.0f m  %d tri" % (r["w"], r["d"], r["h"], r["tris"])])


def sheet(rows_of_cells, title, gap=10):
    widths = [sum(c.width for c in row) + gap * (len(row) + 1) for row in rows_of_cells]
    heights = [max(c.height for c in row) for row in rows_of_cells]
    W = max(widths + [int(ImageDraw.Draw(Image.new("RGB", (1, 1))).textlength(
        title, font=ImageFont.load_default(size=16))) + 2 * gap])
    H = 30 + sum(heights) + gap * (len(rows_of_cells) + 1)
    out = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(out)
    d.text((gap, 8), title, fill=(255, 220, 140), font=ImageFont.load_default(size=16))
    y = 30 + gap
    for row, h in zip(rows_of_cells, heights):
        x = gap
        for c in row:
            out.paste(c, (x, y + h - c.height))
            x += c.width + gap
        y += h + gap
    return out


def fit(img):
    k = MAX_SIDE / max(img.size)
    return img if k >= 1 else img.resize((int(img.width * k), int(img.height * k)), Image.LANCZOS)


def variants(rows, m):
    return [m] + [t for _, t in rows[m]["substitute"] if t != m and t in rows and not rows[t].get("missing")]


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    ch = C.Chapter()
    blocks, _ = C.zrd_blocks()
    import json
    tmp = os.path.join(out_dir, "_census.json")
    sys.argv = ["census", "--json", tmp]
    import contextlib
    import io
    with contextlib.redirect_stdout(io.StringIO()):
        C.main()
    with open(tmp, encoding="utf-8") as f:
        census = json.load(f)
    rows = census["rows"]
    for m, r in rows.items():
        r["substitute"] = [tuple(x) for x in r.get("substitute", [])]

    # One lot type per (a model, det model) pair in each template, in first-seen order.
    lot_types = defaultdict(list)
    for lot in census["lots"]:
        key = tuple(lot["models"])
        if key not in lot_types[lot["template"]]:
            lot_types[lot["template"]].append(key)

    scale = 2.6  # px per metre, shared by every template sheet so sizes compare
    for t in C.TEMPLATES:
        grid = []
        for key in lot_types[t]:
            a = [m for m in key if "det" not in m][0]
            det = [m for m in key if "det" in m]
            row = [cell(ch, rows, [v], scale) for v in variants(rows, a)]
            if det:
                row += [cell(ch, rows, [v], scale) for v in variants(rows, det[0])]
                row.append(cell(ch, rows, [a, det[0]], scale,
                                title="%s + %s" % (a.replace(".flt", ""), det[0].replace(".flt", ""))))
            grid.append(row)
        n = sum(1 for lot in census["lots"] if lot["template"] == t)
        img = sheet(grid, "%s: %d buildings, %d lot types; each row = the lot's a-model variants, then its "
                          "det-half variants, then a + det01 together. %.1f px/m" % (t, n, len(grid), scale))
        fit(img).save(os.path.join(out_dir, "t_%s.png" % t))

    # cblock7: every model it can draw, two sides each, larger.
    names = []
    for key in lot_types["cblock7"]:
        for v in variants(rows, key[0]):
            names.append(v)
    grid = [[cell(ch, rows, [m], 7.0) for m in names],
            [cell(ch, rows, [m], 7.0, title=m.replace(".flt", "") + " (back)", az=215.0) for m in names]]
    fit(sheet(grid, "cblock7: its three lots' models side by side, front (top) and back (bottom), 7 px/m")).save(
        os.path.join(out_dir, "cblock7_views.png"))

    # Every texture a building model uses, flat, nearest-neighbour enlarged.
    use = defaultdict(set)
    for m, r in rows.items():
        for tx in r.get("textures", []):
            use[tx].add(m.replace(".flt", ""))
    cells = []
    for tx in sorted(use):
        t = texture(tx)
        if t is None:
            continue
        im = Image.fromarray(t.astype(np.uint8))
        k = max(1, 192 // max(im.size))
        im = im.resize((im.width * k, im.height * k), Image.NEAREST)
        mean = t.reshape(-1, 3).mean(axis=0)
        ms = sorted(use[tx])
        cells.append(label(im, [tx.replace(".tif", "") + "  %dx%d" % (t.shape[1], t.shape[0]),
                                "rgb %d,%d,%d L %d" % (*mean, C.luma(mean)),
                                "%d models: %s" % (len(ms), " ".join(ms[:3]) + (" ..." if len(ms) > 3 else ""))]))
    grid, row, w = [], [], 0
    for c in cells:
        if row and w + c.width + 10 > MAX_SIDE - 20:
            grid.append(row)
            row, w = [], 0
        row.append(c)
        w += c.width + 10
    grid.append(row)
    fit(sheet(grid, "Every texture on a C5 building model, flat (nearest-neighbour enlarged)")).save(
        os.path.join(out_dir, "textures.png"))
    os.remove(tmp)


if __name__ == "__main__":
    main()
