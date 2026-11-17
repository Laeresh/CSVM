"""Offline previews of the city models in CSVM/data/city_models/C5, for the user's eyes.

Writes into <out dir>, which must be outside the repo (compare.png holds original pixels):
- compare.png: per cblock1 shape, the original (render.py's rasteriser) beside near0, near1 and
  far, at one scale and angle under render.py's flat key light, each model wearing another
  facade row of its family, graded by its material's baseColorFactor.
- atlas.png and atlas_ids.png: the used part of the atlas, and the ids texture in false colour
  (window id as hue, floor index, glass weight), labelled by family and row.
- night.png: a street of the near models under a dim stand-in for C5's night light, windows lit
  from the ids texture per building: every window, then a fixed scatter (each building rolls a
  lit share of 15 to 70 percent, each window rolls against it), then single windows with whole
  lit floors. The per-window roll keys on the window's tile column floor(u), its id R and its
  absolute floor floor(v) * FLOORS + G, the values the shader has.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/city-hulls/preview.py <out dir>
"""
import io
import math
import os
import sys

sys.dont_write_bytecode = True

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import census as C  # noqa: E402
import margin as M  # noqa: E402
import render as R  # noqa: E402

sys.path.insert(0, os.path.join(C.REPO, "CSVM", "data", "city_models"))
import city_atlas as A  # noqa: E402

SHAPES = ["cb00a", "cb01a", "cb01c", "cb01d", "cb02a", "cb02b", "cb03a", "cb03b", "cb04a", "cb04b", "cb05a", "cb05b"]
FONT = ImageFont.load_default(size=14)
SS = 2


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def load_png(name):
    with open(os.path.join(M.MODELS, "C5", name), "rb") as f:
        return np.asarray(Image.open(io.BytesIO(f.read())))


ATLAS = load_png("facade.png.bin")
IDS = load_png("facade_ids.png.bin")


def texel(u, v, fam, cls, row):
    """Nearest texel (x, y) of the atlas for facade tile coordinates, as the shader would fetch."""
    col = np.where(cls > 0.5, A.ROOF_COL, row)
    fu, fv = u - np.floor(u), v - np.floor(v)
    x = col * A.CELL + A.GUTTER + np.minimum(np.floor(fu * A.TILE), A.TILE - 1)
    y = np.round(fam).astype(int) * A.CELL + A.GUTTER + np.minimum(np.floor((1 - fv) * A.TILE), A.TILE - 1)
    return x.astype(int), y.astype(int)


def raster(P, attrs, scale, colour, az=35.0, el=22.0, pad=6, bg=R.BG, light=None):
    """Orthographic z-buffered triangles, render.py's camera. attrs (n, 3, k) are interpolated per
    pixel and handed with the face normal to colour(i, vals, n) -> linear RGB (m, 3)."""
    right, up, fwd = R.camera(az, el)
    sx, sy, sz = P @ right, P @ up, P @ fwd
    s = scale * SS
    x0, y1 = sx.min(), sy.max()
    W = int(math.ceil((sx.max() - x0) * s)) + 2 * pad * SS
    H = int(math.ceil((y1 - sy.min()) * s)) + 2 * pad * SS
    img = np.zeros((H, W, 3), np.float32)
    img[:] = srgb_to_lin(np.array(bg) / 255.0)
    zb = np.full((H, W), -np.inf)
    X = (sx - x0) * s + pad * SS
    Y = (y1 - sy) * s + pad * SS
    for i in range(len(P)):
        xa, ya = X[i], Y[i]
        ax, bx = int(max(0, math.floor(xa.min()))), int(min(W - 1, math.ceil(xa.max())))
        ay, by = int(max(0, math.floor(ya.min()))), int(min(H - 1, math.ceil(ya.max())))
        if bx < ax or by < ay:
            continue
        n = np.cross(P[i, 1] - P[i, 0], P[i, 2] - P[i, 0])
        if np.dot(n, fwd) <= 0:
            continue  # back face: the material is single sided
        gx, gy = np.meshgrid(np.arange(ax, bx + 1) + 0.5, np.arange(ay, by + 1) + 0.5)
        d = (ya[1] - ya[2]) * (xa[0] - xa[2]) + (xa[2] - xa[1]) * (ya[0] - ya[2])
        if abs(d) < 1e-9:
            continue
        w0 = ((ya[1] - ya[2]) * (gx - xa[2]) + (xa[2] - xa[1]) * (gy - ya[2])) / d
        w1 = ((ya[2] - ya[0]) * (gx - xa[2]) + (xa[0] - xa[2]) * (gy - ya[2])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        z = w0 * sz[i, 0] + w1 * sz[i, 1] + w2 * sz[i, 2]
        sub = zb[ay:by + 1, ax:bx + 1]
        m = inside & (z > sub)
        if not m.any():
            continue
        vals = w0[m][:, None] * attrs[i, 0] + w1[m][:, None] * attrs[i, 1] + w2[m][:, None] * attrs[i, 2]
        sub[m] = z[m]
        img[ay:by + 1, ax:bx + 1][m] = colour(i, vals, n / np.linalg.norm(n))
    im = Image.fromarray(np.round(lin_to_srgb(img) * 255).astype(np.uint8))
    return im.resize((max(1, W // SS), max(1, H // SS)), Image.BOX)


def model_tris(meshes, name, offset=(0, 0, 0)):
    pos, uv0, uv1, tri = meshes[name]
    assert set(np.unique(uv1[:, 1])) <= {0.0, 1.0}, "TEXCOORD_1.y is not a surface class"
    P = pos[tri] + np.array(offset)
    attrs = np.concatenate([uv0, uv1], axis=1)[tri]
    return P, attrs


def flat_colour(factor, row):
    """render.py's flat key: albedo times 0.55 + 0.45 |n.L|, in sRGB space as render.py shades."""
    def colour(i, vals, n):
        x, y = texel(vals[:, 0], vals[:, 1], vals[:, 2], vals[:, 3], row)
        alb = srgb_to_lin(ATLAS[y, x, :3] / 255.0) * np.array(factor)
        shade = 0.55 + 0.45 * abs(float(n @ R.LIGHT))
        return srgb_to_lin(lin_to_srgb(alb) * shade)
    return colour


def compare(out):
    ch = C.Chapter()
    scale = 5.0
    lines = []
    for k in range(0, len(SHAPES), 2):
        row = []
        for m in SHAPES[k:k + 2]:
            node = next(i for i in ch.by_name[m + ".flt"] if ch.parts(i))
            tris = ch.geometry(node)
            r = R.render(tris, scale)
            row.append(R.label(r, ["%s original" % m, "%d tri" % len(tris)]))
            meshes, factor = M.read_glb(os.path.join(M.MODELS, "C5", m + ".glb.bin"))
            fam = int(meshes["far"][2][0, 0])
            for variant, (name, facade) in enumerate((("near0", 0), ("near1", 1), ("far", 2))):
                P, attrs = model_tris(meshes, name)
                im = raster(P, attrs, scale, flat_colour(factor, facade))
                row.append(R.label(im, ["%s %s" % (m, name), "%d tri, %s row %d" % (
                    len(P), A.FAMILY_NAMES[fam], facade)]))
        lines.append(row)
    img = R.sheet(lines, "cblock1 shapes: original | near0 | near1 | far, %.0f px/m, flat key light; each model "
                         "graded by its baseColorFactor, wearing facade row 0, 1 and 2 of its family" % scale)
    R.fit(img).save(os.path.join(out, "compare.png"))


def atlas_views(out):
    used_w, used_h = (A.ROOF_COL + 1) * A.CELL, len(A.FAMILIES) * A.CELL
    k = 1.5
    left, top = 190, 44

    def frame(img, title):
        big = Image.fromarray(img[:used_h, :used_w, :3]).resize((int(used_w * k), int(used_h * k)), Image.NEAREST)
        out_im = Image.new("RGB", (left + big.width + 10, top + big.height + 10), R.BG)
        out_im.paste(big, (left, top))
        d = ImageDraw.Draw(out_im)
        d.text((8, 4), title, fill=(255, 220, 140), font=FONT)
        for c in range(A.ROOF_COL + 1):
            d.text((left + int((c * A.CELL + A.GUTTER) * k), top - 16), "roof" if c == A.ROOF_COL else "row %d" % c,
                   fill=(225, 225, 225), font=FONT)
        for f, name in enumerate(A.FAMILY_NAMES):
            d.text((8, top + int((f * A.CELL + A.CELL / 2) * k)), "family %d\n%s" % (f, name), fill=(225, 225, 225),
                   font=FONT)
        return out_im

    frame(ATLAS, "facade.png.bin, %dx%d, cells of %d px (tile %d + gutter %d), the used %d x %d shown at %.1fx"
          % (A.ATLAS, A.ATLAS, A.CELL, A.TILE, A.GUTTER, used_w, used_h, k)).save(os.path.join(out, "atlas.png"))

    r, g, b = (IDS[..., i].astype(float) for i in range(3))
    hue = (r * 0.618034) % 1.0
    hsv = np.stack([hue, np.full_like(hue, 0.75), 0.25 + 0.75 * b / 255.0], -1)
    idc = np.asarray(Image.fromarray(np.round(hsv * 255).astype(np.uint8), "HSV").convert("RGB")).copy()
    idc[r == 0] = (40, 40, 40)
    flc = np.zeros_like(idc)
    flc[..., 0] = np.where(g == 1, 230, 40) * (b / 255.0)
    flc[..., 2] = np.where(g == 0, 230, 40) * (b / 255.0)
    flc[r == 0] = (40, 40, 40)
    a = frame(idc, "facade_ids.png.bin: R window id (hue per id, grey is wall), B glass weight (brightness: clear "
                   "255, blind %d, bar 0)" % A.BLIND)
    bimg = frame(flc, "facade_ids.png.bin: G floor index within the tile (blue 0, red 1), scaled by B")
    both = Image.new("RGB", (a.width, a.height + bimg.height), R.BG)
    both.paste(a, (0, 0))
    both.paste(bimg, (0, a.height))
    R.fit(both).save(os.path.join(out, "atlas_ids.png"))


def hash01(*keys):
    """A 32-bit integer hash of integer arrays, to 0..1."""
    h = np.uint64(2166136261)
    for k in keys:
        h = (h ^ (np.asarray(k).astype(np.int64).astype(np.uint64) & np.uint64(0xFFFFFFFF))) * np.uint64(16777619)
        h &= np.uint64(0xFFFFFFFF)
        h ^= h >> np.uint64(15)
        h = (h * np.uint64(0x2C1B3C6D)) & np.uint64(0xFFFFFFFF)
    return h.astype(np.float64) / 4294967296.0


LIGHTS = np.array([(1.0, 0.74, 0.42), (1.0, 0.62, 0.32), (0.92, 0.86, 0.70), (1.0, 0.80, 0.55)])


def night_colour(factor, row, seed, tint, mode):
    share = 0.15 + 0.55 * hash01(seed, 1)
    floor_share = 0.06 + 0.08 * hash01(seed, 2)

    def colour(i, vals, n):
        u, v, fam, cls = vals.T
        x, y = texel(u, v, fam, cls, row)
        alb = srgb_to_lin(ATLAS[y, x, :3] / 255.0) * np.array(factor) * tint
        # A stand-in for C5's night: a cool ambient and a weak moon key.
        lit_sky = np.array((0.42, 0.46, 0.58)) + 0.35 * max(0.0, float(n @ R.LIGHT))
        col = alb * lit_sky
        wid, flr, glass = IDS[y, x, 0], IDS[y, x, 1], IDS[y, x, 2] / 255.0
        win = (wid > 0) & (glass > 0) & (cls < 0.5)
        column = np.floor(u).astype(np.int64)
        absfloor = np.floor(v).astype(np.int64) * A.FLOORS + flr
        if mode == "all":
            on = win
        else:
            on = win & (hash01(seed, column, wid, absfloor) < share)
            if mode == "floors":
                on = win & ((hash01(seed, column, wid, absfloor) < share * 0.6) | (hash01(seed, 7, absfloor) < floor_share))
        pick = (hash01(seed, column, wid, absfloor, 3) * len(LIGHTS)).astype(int)
        bright = 0.55 + 0.6 * hash01(seed, column, wid, absfloor, 4)
        emit = LIGHTS[pick] * (bright * glass)[:, None]
        return np.where(on[:, None], col + emit, col)
    return colour


def night(out):
    street = ["cb00a", "cb02a", "cb01a", "cb03a", "cb01c", "cb05b", "cb00b", "cb04b", "cb01a", "cb02b", "cb03b",
              "cb01d", "cb05a", "cb04a", "cb03a", "cb01b"]
    scale, gap = 4.0, 6.0
    rows = []
    for mode, title in (("all", "every window lit"),
                        ("scatter", "fixed scatter: each building lights 15 to 70 percent, each window rolls"),
                        ("floors", "single windows plus whole lit floors")):
        P_all, A_all, owners = [], [], []
        x = 0.0
        for k, m in enumerate(street):
            meshes, factor = M.read_glb(os.path.join(M.MODELS, "C5", m + ".glb.bin"))
            seed = 1000 + 37 * k
            near = "near%d" % int(hash01(seed, 5) * 2)
            pos = meshes[near][0]
            P, attrs = model_tris(meshes, near, (x - pos[:, 0].min(), 0, 0))
            x += pos[:, 0].max() - pos[:, 0].min() + gap
            row = int(hash01(seed, 6) * A.VARIANTS)
            tint = 0.85 + 0.3 * hash01(seed, 8)
            P_all.append(P)
            A_all.append(attrs)
            owners.append((len(P), night_colour(factor, row, seed, tint, mode)))
        P = np.concatenate(P_all)
        attrs = np.concatenate(A_all)
        starts = np.cumsum([0] + [n for n, _ in owners])

        def colour(i, vals, n):
            b = int(np.searchsorted(starts, i, side="right") - 1)
            return owners[b][1](i - starts[b], vals, n)
        im = raster(P, attrs, scale, colour, az=20.0, el=8.0, bg=(8, 10, 18))
        rows.append([R.label(im, [title])])
    img = R.sheet(rows, "A cblock1 street at night, offline mock: near models, per building facade row, tint and "
                        "lit share from a seed; dim stand-in light, no fog, no bloom. %.0f px/m" % scale)
    R.fit(img).save(os.path.join(out, "night.png"))


def main():
    out = sys.argv[1]
    if os.path.abspath(out).lower().startswith(os.path.abspath(C.REPO).lower()):
        raise SystemExit("write the previews outside the repo: compare.png holds original pixels")
    os.makedirs(out, exist_ok=True)
    atlas_views(out)
    night(out)
    compare(out)


if __name__ == "__main__":
    main()
