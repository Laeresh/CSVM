"""C5 ground survey for PLAN-c5-ground items A1 and A2 (issue #357).

Measures how C5's painted `cblock` ground relates to the building sets that stand on it:

- per template, each building decoration's top-down triangles mapped into texture UV through the
  template ground quad's own affine XZ->UV, the way `Clutter.ParseTemplate` projects a
  decoration's origin (`CSVM/src/Mech3/Clutter.cs`), rasterised at texel scale;
- the combinations table: per visible paint and standing building set, the built share of the
  block area, the painted structure the buildings leave uncovered, the free floor's luma and the
  widest free spot, all per 256 m tile;
- the eight-frame test: the luma inside a template's own footprints under each of the eight
  axis-aligned texture frames;
- the world census: every `cblock` polygon reached from `world1` (each node once, nearest LOD,
  the template subtrees left out), its area, `no_clutter` flag (`unk3`), UV frame, metres per
  repeat and UV phase, and the exact coplanar overlap of every overlay polygon (`cblock1/2/3/7`)
  with every base polygon (`cblock4/5/6`), clipped triangle against triangle, no bounding boxes;
- the area per combination of visible paint and building set, and the flagged ground with no
  base beneath it (no buildings) per paint, with its luma and where it lies.

extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the checkout that does,
defaulting to this one. Embeds no game data and writes nothing by default. --overlays writes
texture overlays, which hold original pixels, so it refuses a directory inside the repo.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/c5-ground/survey.py [--overlays <dir outside the repo>]
"""
import argparse
import math
import os
import sys
from collections import defaultdict

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(HERE, "..", "city-hulls"))
import census as C  # noqa: E402

E = C.E
OVERLAY = ("cblock1", "cblock2", "cblock3", "cblock7")
BASE = ("cblock4", "cblock5", "cblock6")
# The plan's street rule: a texel is street where its 16 m box-filtered luma exceeds this.
STREET_LUMA = 55
STREET_BOX_M = 16
# Luma at or above which a block texel counts as painted structure (roofs, yards, lit walls).
STRUCTURE_LUMA = 25
# The combinations table's rows: visible paint and the template whose buildings stand there.
COMBOS = [("cblock1", "cblock1"), ("cblock1", "cblock4"), ("cblock2", "cblock2"),
          ("cblock2", "cblock5"), ("cblock3", "cblock3"), ("cblock3", "cblock6"),
          ("cblock7", "cblock7"), ("cblock7", "cblock4"), ("cblock4", "cblock4")]


def luma(a):
    return 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]


def tex_luma(name):
    return luma(np.asarray(Image.open(C.tex_png(name)).convert("RGB"), dtype=float))


# ------------------------------------------------------------------------------- templates

def ground_affine(ch, g):
    """The ground quad's own XZ->UV affine, least squares over its first polygon's corners."""
    m = ch.models[g.model_index]
    p = m["polygons"][0]
    a = np.array([[m["vertices"][i]["x"], m["vertices"][i]["z"], 1.0] for i in p["vertex_indices"]])
    u = np.array([[q["u"], q["v"]] for q in p["materials"][0]["uv_coords"]])
    sol = np.linalg.lstsq(a, u, rcond=None)[0]
    return sol, ch.mat_tex[p["materials"][0]["material_index"]]


def template(ch, t):
    """Every building decoration of template `t` as UV triangles, with its origin's UV."""
    root = E.find_template_root(ch.nodes, t)
    g = ch.nodes[E.first_with_mesh(ch.nodes, C._Meshes(ch), root, include_self=True)]
    aff, tex = ground_affine(ch, g)
    blds = []
    for c in g.children:
        n = ch.nodes[c]
        name = n.name.lower()
        if name in C.NOT_BUILDINGS:
            continue
        loc = n.local or E.IDENTITY
        to_uv = lambda p: tuple(np.array([p[0], p[2], 1.0]) @ aff)
        tris = [[to_uv(E._apply(loc, p)) for p in tri[0]] for tri in ch.geometry(c)]
        blds.append({"name": name, "tris": tris, "uv": to_uv(loc[1]), "det": "det" in name})
    return {"tex": tex.lower().replace(".tif", ""), "aff": aff, "blds": blds}


def footprint(blds, w, h, wrap=True):
    """Texel mask of the non-`det` buildings. A building is drawn in the tile its origin falls in;
    with `wrap` the parts that cross a tile edge are drawn again on the opposite side, since the
    texture repeats there."""
    im = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(im)
    shifts = [(i, j) for i in (-1, 0, 1) for j in (-1, 0, 1)] if wrap else [(0, 0)]
    for b in blds:
        if b["det"]:
            continue
        ou, ov = math.floor(b["uv"][0]), math.floor(b["uv"][1])
        for tri in b["tris"]:
            for i, j in shifts:
                d.polygon([((u - ou + i) * w, (v - ov + j) * h) for u, v in tri], fill=255)
    return np.asarray(im) > 0


def street_mask(lum):
    mpt = 256.0 / lum.shape[1]
    st = ndimage.uniform_filter(lum, size=max(3, int(STREET_BOX_M / mpt)), mode="wrap") > STREET_LUMA
    return ndimage.binary_opening(st, iterations=1)


def floor_stats(lum, built):
    """Per-tile numbers of one paint under one building mask (None for no buildings)."""
    h, w = lum.shape
    mpt = 256.0 / w
    blk = ~street_mask(lum)
    m = built if built is not None else np.zeros_like(blk)
    free = blk & ~m
    paint = blk & (lum >= STRUCTURE_LUMA)
    dt = ndimage.distance_transform_edt(np.tile(free, (3, 3)))[h:2 * h, w:2 * w] * mpt
    return {
        "street_share": float(1 - blk.mean()), "street_luma": float(lum[~blk].mean()),
        "built": float((m & blk).sum() / blk.sum()),
        "uncovered": float((paint & ~m).sum() / max(1, paint.sum())),
        "free_mean": float(lum[free].mean()), "free_median": float(np.median(lum[free])),
        "free_dark": float((free & (lum <= 5)).sum() / free.sum()),
        "widest_m": float(2 * dt.max()),
    }


def eight_frames(lum, m):
    """Mean luma inside the footprint mask under each of the eight axis-aligned frames."""
    out = []
    for k in range(4):
        for flip in (False, True):
            mm = np.rot90(m, k)
            mm = mm[:, ::-1] if flip else mm
            out.append(float(lum[mm].mean()))
    return out


# ------------------------------------------------------------------------------- the world

def tris_of(p):
    vi = p["vertex_indices"]
    if (p.get("flags") or {}).get("tri_strip"):
        return [(i, i + 1, i + 2) for i in range(len(vi) - 2)]
    return [(0, i, i + 1) for i in range(1, len(vi) - 1)]


def frame(j):
    """`j` maps dXZ to dUV; names the world axis +U and +V run along, or 'off'."""
    out = []
    for vec in np.linalg.inv(j):
        a = math.degrees(math.atan2(vec[1], vec[0])) % 360
        k = round(a / 90) % 4
        if abs(((a - k * 90) + 180) % 360 - 180) > 5:
            return "off"
        out.append(["+X", "+Z", "-X", "-Z"][k])
    return "U%s V%s" % tuple(out)


def world_polys(ch):
    """One record per `cblock` polygon instance reached from world1."""
    wi = next(i for i, n in enumerate(ch.nodes) if n.kind == "World" and n.name.lower() == "world1")
    xf = E.build_transform_cache(ch.nodes, wi)
    skip = set()
    for t in OVERLAY + BASE:
        stack = [E.find_template_root(ch.nodes, t)]
        while stack:
            k = stack.pop()
            skip.add(k)
            stack += ch.nodes[k].children
    out = []
    for idx, x in xf.items():
        n = ch.nodes[idx]
        if idx in skip or not ch.has_mesh(idx):
            continue
        m = ch.models[n.model_index]
        vs = [E._apply(x, (v["x"], v["y"], v["z"])) for v in m["vertices"]]
        for pi, p in enumerate(m["polygons"]):
            ms = p.get("materials") or []
            tex = ch.mat_tex[ms[0]["material_index"]] if ms else None
            if not tex or not tex.lower().startswith("cblock") or len(p["vertex_indices"]) < 3:
                continue
            vi, uv = p["vertex_indices"], ms[0]["uv_coords"]
            tris, fr = [], None
            for a, b, c in tris_of(p):
                pa, pb, pc = vs[vi[a]], vs[vi[b]], vs[vi[c]]
                xz = [(pa[0], pa[2]), (pb[0], pb[2]), (pc[0], pc[2])]
                ys = (pa[1], pb[1], pc[1])
                uvs = [(uv[k]["u"], uv[k]["v"]) for k in (a, b, c)]
                tris.append((xz, uvs, ys))
                area = abs(_cross(xz))
                if fr is None and area > 2:
                    d = np.array([[xz[1][0] - xz[0][0], xz[1][1] - xz[0][1]],
                                  [xz[2][0] - xz[0][0], xz[2][1] - xz[0][1]]])
                    du = np.array([[uvs[1][0] - uvs[0][0], uvs[1][1] - uvs[0][1]],
                                   [uvs[2][0] - uvs[0][0], uvs[2][1] - uvs[0][1]]])
                    j = np.linalg.solve(d, du)
                    # World X and Z where U and V are whole numbers, in metres modulo 256.
                    period = 1 / math.sqrt(abs(np.linalg.det(j)))
                    fr = (frame(j), round(period), xz[0][0] - uvs[0][0] * period,
                          xz[0][1] - uvs[0][1] * period)
            out.append({"node": idx, "poly": pi, "tex": tex.lower().replace(".tif", ""),
                        "flag": bool((p.get("flags") or {}).get("unk3")), "tris": tris,
                        "area": sum(abs(_cross(t[0])) for t in tris) / 2, "frame": fr,
                        "y": round(sum(t[2][0] for t in tris) / len(tris), 2)})
    return out


def _cross(t):
    (ax, az), (bx, bz), (cx, cz) = t
    return (bx - ax) * (cz - az) - (cx - ax) * (bz - az)


def _ccw(t):
    return t if _cross(t) > 0 else [t[0], t[2], t[1]]


def half(poly, a, b, left):
    """The part of a convex polygon on one side of the line a->b (left = counter-clockwise side)."""
    side = lambda p: ((b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])) * (1 if left else -1)
    out = []
    for k in range(len(poly)):
        cur, prv = poly[k], poly[k - 1]
        sc, sp = side(cur), side(prv)
        if (sc >= 0) != (sp >= 0):
            t = sp / (sp - sc)
            out.append((prv[0] + t * (cur[0] - prv[0]), prv[1] + t * (cur[1] - prv[1])))
        if sc >= 0:
            out.append(cur)
    return out if len(out) >= 3 and poly_area(out) > 1e-6 else []


def split(piece, tri):
    """Splits a convex piece by a counter-clockwise triangle: (the parts outside, the part inside)."""
    outside, rest = [], piece
    for k in range(3):
        a, b = tri[k], tri[(k + 1) % 3]
        o = half(rest, a, b, left=False)
        if o:
            outside.append(o)
        rest = half(rest, a, b, left=True)
        if not rest:
            break
    return outside, rest


def poly_area(p):
    return abs(sum(p[i][0] * p[i - 1][1] - p[i - 1][0] * p[i][1] for i in range(len(p)))) / 2


def arrangement(polys, cell=256.0):
    """Exact planar arrangement of every coplanar `cblock` triangle: each region of the ground
    once, with the set of polygons stacked on it. A region is counted at the lowest-numbered
    triangle that covers it and labelled with every higher one that covers it too. Only polygons
    at the same height (to 1 cm) stack. Returns {frozenset(polygon ids): area}."""
    tris = []
    for pi, p in enumerate(polys):
        for xz, _, _ in p["tris"]:
            if abs(_cross(xz)) > 1e-6:
                tris.append((pi, _ccw(xz), p["y"]))
    grid = defaultdict(set)
    box = []
    for ti, (_, t, _) in enumerate(tris):
        xs, zs = [q[0] for q in t], [q[1] for q in t]
        box.append((min(xs), min(zs), max(xs), max(zs)))
        for gx in range(math.floor(min(xs) / cell), math.floor(max(xs) / cell) + 1):
            for gz in range(math.floor(min(zs) / cell), math.floor(max(zs) / cell) + 1):
                grid[gx, gz].add(ti)
    near = defaultdict(set)
    for members in grid.values():
        for a in members:
            near[a] |= members
    regions = defaultdict(float)
    for i, (pi, ti, yi) in enumerate(tris):
        pieces = [(ti, frozenset([pi]))]
        bi = box[i]
        for j in sorted(near[i] - {i}):
            pj, tj, yj = tris[j]
            bj = box[j]
            if yj != yi or bj[0] >= bi[2] or bi[0] >= bj[2] or bj[1] >= bi[3] or bi[1] >= bj[3]:
                continue
            nxt = []
            for piece, labels in pieces:
                outside, inside = split(piece, tj)
                nxt += [(o, labels) for o in outside]
                if inside and j > i:
                    nxt.append((inside, labels | {pj}))
            pieces = nxt
        for piece, labels in pieces:
            regions[labels] += poly_area(piece)
    return regions


def stack_kind(polys, labels):
    """(visible paint, building sets) of a region. A flagged polygon draws on top of the
    coplanar ones it covers, and only unflagged polygons decorate (docs/org/clutter.md,
    `no_clutter`). Two unflagged paints of different textures tie, written `a/b`."""
    flagged = sorted({polys[i]["tex"] for i in labels if polys[i]["flag"]})
    clear = sorted({polys[i]["tex"] for i in labels if not polys[i]["flag"]})
    visible = "|".join(flagged) if flagged else "/".join(clear)
    return visible, tuple(clear)


def clusters(polys, weight, gap=1.0):
    """Groups polygons whose XZ bounds touch, to say roughly where a set of polygons lies;
    `weight` maps each polygon to the area it contributes."""
    boxes = []
    for i, a in weight.items():
        xs = [q[0] for t in polys[i]["tris"] for q in t[0]]
        zs = [q[1] for t in polys[i]["tris"] for q in t[0]]
        boxes.append([min(xs), min(zs), max(xs), max(zs), a, 1])
    merged = True
    while merged:
        merged = False
        for a in range(len(boxes)):
            for b in range(a + 1, len(boxes)):
                p, q = boxes[a], boxes[b]
                if p[0] - gap <= q[2] and q[0] - gap <= p[2] and p[1] - gap <= q[3] and q[1] - gap <= p[3]:
                    boxes[a] = [min(p[0], q[0]), min(p[1], q[1]), max(p[2], q[2]), max(p[3], q[3]),
                                p[4] + q[4], p[5] + q[5]]
                    del boxes[b]
                    merged = True
                    break
            if merged:
                break
    return sorted(boxes, key=lambda b: -b[4])


def uv_cover(polys, idxs, w, h):
    """How many times each texel of the tile is laid on the ground by the given polygons, from
    their UV triangles wrapped into the tile. Per-tile statistics weight every texel alike; this
    weights them as the polygons actually show them."""
    acc = np.zeros((h, w))
    for i in idxs:
        for _, uvs, _ in polys[i]["tris"]:
            us, vs = [q[0] for q in uvs], [q[1] for q in uvs]
            for cu in range(math.floor(min(us)), math.ceil(max(us))):
                for cv in range(math.floor(min(vs)), math.ceil(max(vs))):
                    im = Image.new("L", (w, h), 0)
                    ImageDraw.Draw(im).polygon([((u - cu) * w, (v - cv) * h) for u, v in uvs], fill=1)
                    acc += np.asarray(im)
    return acc


def weighted_floor(lum, wgt):
    """Free-floor luma (no buildings) weighted by how often each texel is laid on the ground."""
    free = ~street_mask(lum)
    wv = wgt * free
    order = np.argsort(lum, axis=None)
    cum = np.cumsum(wv.ravel()[order])
    median = lum.ravel()[order][np.searchsorted(cum, cum[-1] / 2)]
    return float((lum * wv).sum() / wv.sum()), float(median), float((wv * (lum <= 5)).sum() / wv.sum())


# ------------------------------------------------------------------------------- report

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--overlays", help="write footprint overlays (original pixels) to this directory")
    args = ap.parse_args()
    if args.overlays and os.path.abspath(args.overlays).lower().startswith(REPO.lower()):
        sys.exit("--overlays must name a directory outside the repo: overlays hold original pixels")

    ch = C.Chapter()
    T = {t: template(ch, t) for t in C.TEMPLATES}
    L = {t: tex_luma(T[t]["tex"]) for t in T}

    print("== combinations, per 256 m tile (street: %d m box luma > %d; structure: luma >= %d)"
          % (STREET_BOX_M, STREET_LUMA, STRUCTURE_LUMA))
    print("paint    set      street(L)    built uncovered free mean/median dark<=5 widest  | "
          "unwrapped: built uncovered mean/median dark widest")
    for paint, bset in COMBOS:
        lum = L[paint]
        h, w = lum.shape
        s = floor_stats(lum, footprint(T[bset]["blds"], w, h))
        # The unwrapped columns clip a building at the tile edge, as the first scratch survey did.
        u = floor_stats(lum, footprint(T[bset]["blds"], w, h, wrap=False))
        print("%-8s %-8s %.2f (L%3.0f)  %.2f  %.2f      %4.1f, %3.0f     %.2f   %3.0f m  |  %.2f %.2f %4.1f, %3.0f %.2f %3.0f m"
              % (paint, bset, s["street_share"], s["street_luma"], s["built"], s["uncovered"],
                 s["free_mean"], s["free_median"], s["free_dark"], s["widest_m"],
                 u["built"], u["uncovered"], u["free_mean"], u["free_median"], u["free_dark"], u["widest_m"]))
    for paint in OVERLAY:
        s = floor_stats(L[paint], None)
        print("%-8s none     %.2f (L%3.0f)  0.00  1.00      %4.1f, %3.0f     %.2f   %3.0f m"
              % (paint, s["street_share"], s["street_luma"], s["free_mean"], s["free_median"],
                 s["free_dark"], s["widest_m"]))

    print()
    print("== eight-frame test: mean luma inside a template's own footprints, per frame "
          "(identity first), and the share of its painted structure outside them")
    for t in C.TEMPLATES:
        lum = L[t]
        m = footprint(T[t]["blds"], *lum.shape[::-1])
        fr = eight_frames(lum, m)
        blk = ~street_mask(lum)
        paint = blk & (lum >= STRUCTURE_LUMA)
        outside = [float((paint & ~np.rot90(m, k)[:, ::-1 if f else 1]).sum() / paint.sum())
                   for k in range(4) for f in (False, True)]
        print("%-8s %s | outside %.2f to %.2f | texture mean %.1f" % (
            t, " ".join("%4.1f" % v for v in fr), min(outside), max(outside), lum.mean()))

    polys = world_polys(ch)
    print()
    print("== world UV-frame census: area per texture and flag, by frame and metres per repeat")
    by = defaultdict(lambda: defaultdict(float))
    for p in polys:
        key = "%s %dm" % p["frame"][:2] if p["frame"] else "degenerate"
        by[p["tex"], p["flag"]][key] += p["area"]
    for k in sorted(by):
        tot = sum(by[k].values())
        print("%-8s %-5s %6.2f km2  %s" % (k[0], k[1], tot / 1e6, ", ".join(
            "%s %.1f%%" % (f, 100 * a / tot) for f, a in sorted(by[k].items(), key=lambda x: -x[1]))))
    phase, origin = defaultdict(float), defaultdict(float)
    for p in polys:
        if p["frame"] and p["frame"][0] == "U+X V+Z" and p["frame"][1] == 256:
            phase[round(p["frame"][2]) % 256, round(p["frame"][3]) % 256] += p["area"]
            origin[round(p["frame"][2] / 256), round(p["frame"][3] / 256)] += p["area"]
    tot = sum(phase.values())
    print("UV phase of the U+X V+Z 256 m polygons (world X, Z of a whole UV, mod 256): %s"
          % ", ".join("(%d, %d) %.2f%%" % (k[0], k[1], 100 * a / tot)
                      for k, a in sorted(phase.items(), key=lambda x: -x[1])[:6]))
    # A cell hash on raw UV seams wherever two neighbours put UV (0, 0) at different world tiles.
    print("their UV origin (world tile of UV 0, 0): %d distinct, the largest %s" % (
        len(origin), ", ".join("(%d, %d) %.1f%%" % (k[0], k[1], 100 * a / tot)
                               for k, a in sorted(origin.items(), key=lambda x: -x[1])[:4])))
    heights = defaultdict(float)
    for p in polys:
        heights[p["y"]] += p["area"]
    print("heights: %s" % ", ".join("y=%g %.2f km2" % (y, a / 1e6) for y, a in sorted(heights.items())))

    regions = arrangement(polys)
    union = sum(regions.values())
    print()
    print("== ground stacks: %.3f km2 of ground under %.3f km2 of polygons" % (
        union / 1e6, sum(p["area"] for p in polys) / 1e6))
    stacks = defaultdict(float)
    for labels, a in regions.items():
        stacks[tuple(sorted("%s%s" % (polys[i]["tex"], "*" if polys[i]["flag"] else "") for i in labels))] += a
    for k, a in sorted(stacks.items(), key=lambda x: -x[1]):
        print("  %-40s %7.3f km2" % (" + ".join(k), a / 1e6))
    print("  (* = flagged no_clutter; a texture twice is two coplanar polygons of it)")

    rows = defaultdict(float)
    odd = defaultdict(float)
    bare_of = defaultdict(float)
    for labels, a in regions.items():
        vis, sets = stack_kind(polys, labels)
        rows[vis, sets] += a
        for i in labels:
            p = polys[i]
            if not p["flag"] and p["frame"] and p["frame"][:2] != ("U+X V+Z", 256):
                odd[vis, p["tex"], "%s %dm" % p["frame"][:2]] += a
            if not sets:
                bare_of[i] += a
    print()
    print("== area per visible paint and standing building set (exact)")
    for (vis, sets), a in sorted(rows.items(), key=lambda x: (x[0][0], -x[1])):
        print("  %-15s sets %-17s %7.3f km2  %5.2f%%" % (vis, "+".join(sets) or "none", a / 1e6, 100 * a / union))
    print("  decorating polygons laid in another frame than U+X V+Z 256 m:")
    for (vis, tex, fr), a in sorted(odd.items(), key=lambda x: -x[1]):
        print("    under visible %-8s a clear %s at %-14s %6.3f km2" % (vis, tex, fr, a / 1e6))

    print()
    print("== flagged overlay polygons by the share of them that has a decorating layer beneath")
    hist = defaultdict(lambda: [0, 0.0])
    for i, p in enumerate(polys):
        if p["tex"] in OVERLAY and p["flag"]:
            f = 1 - min(1.0, bare_of[i] / p["area"])
            b = "none" if f < 1e-3 else "full" if f > 0.999 else "partial"
            hist[b][0] += 1
            hist[b][1] += p["area"]
    for b, (n, a) in hist.items():
        print("  %-7s %4d polygons %7.3f km2" % (b, n, a / 1e6))

    print()
    print("== bare ground: flagged overlay with no decorating layer beneath it, per paint")
    for paint in OVERLAY:
        # Seam slivers of a few square metres are not bare ground; a bare polygon is mostly bare.
        weight = {i: a for i, a in bare_of.items()
                  if polys[i]["tex"] == paint and polys[i]["flag"] and a > 0.5 * polys[i]["area"]}
        bare = rows.get((paint, ()), 0.0)
        s = floor_stats(L[paint], None)
        lum = L[paint]
        laid = weighted_floor(lum, uv_cover(polys, weight, *lum.shape[::-1]))
        print("  %s: %.3f km2, %d polygons hold %.3f km2 of it; free floor per tile L mean %.1f, median %.0f, "
              "dark %.2f, widest %.0f m; as laid L mean %.1f, median %.0f, dark %.2f" % (
                  paint, bare / 1e6, len(weight), sum(weight.values()) / 1e6, s["free_mean"],
                  s["free_median"], s["free_dark"], s["widest_m"], *laid))
        for b in clusters(polys, weight)[:6]:
            print("    x %7.0f..%7.0f  z %7.0f..%7.0f  %6.3f km2 (%d polygons)" % (
                b[0], b[2], b[1], b[3], b[4] / 1e6, b[5]))

    if args.overlays:
        write_overlays(T, args.overlays)


def write_overlays(T, out):
    """The paint brightened (gamma 0.5, x1.6) with its own footprints in green and, for an
    overlay paint, the base set beneath it in magenta."""
    os.makedirs(out, exist_ok=True)
    pairs = {"cblock1": "cblock4", "cblock2": "cblock5", "cblock3": "cblock6", "cblock7": "cblock4"}
    for t, d in T.items():
        im = Image.open(C.tex_png(d["tex"])).convert("RGB")
        w, h = im.size
        k = max(1, 1024 // w)
        a = np.asarray(im.resize((w * k, h * k), Image.NEAREST), dtype=float) / 255.0
        big = Image.fromarray((np.clip(a ** 0.5 * 1.6, 0, 1) * 255).astype(np.uint8))
        for src, col in ((t, (0, 255, 0)), (pairs.get(t), (255, 0, 255))):
            if src is None:
                continue
            m = footprint(T[src]["blds"], w * k, h * k)
            edge = m & ~ndimage.binary_erosion(m)
            px = np.asarray(big).copy()
            px[edge] = col
            big = Image.fromarray(px)
        big.save(os.path.join(out, "%s_overlay.png" % t))
    print("overlays written to %s" % out)


if __name__ == "__main__":
    main()
