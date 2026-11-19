"""Exports C5's ground layout for PLAN-c5-ground item B11 (issue #357).

Writes CSVM/data/city_models/C5/ground_layout.json, the one file the block floor generator (B12)
and the dressing stamper (B14) read in place of the game's files. It holds measured geometry only,
no pixels:

- `templates`: per `cblock` template, its building lots and lamp posts in texture UV. A lot is
  the decorations standing at one origin (a building and its `det` half). Its footprint is
  everything the lot can show: every model its substitute roll can pick for each of its
  decorations (templates.zrd), each with all of its meshes (walls, cornices, awnings, fire
  escapes), seen from above. The meshes are rasterised at 4 px per metre in the template frame,
  grown 0.5 m so a wall seen edge-on has width, their enclosed holes filled, and each connected
  piece written as the convex hull of the exact vertices that fall in it. `h` is the lot's top
  above the template ground in metres.
- `paints`: per ground texture, its streets (survey.py's `find_streets`: family, centreline and
  half-width in that family's UV coordinate, kerb to kerb), its variant cell, and the building
  sets standing on it in the world with their area (survey.py's arrangement), "none" being the
  flagged ground with no layer beneath.

Conventions, also written into the file's `conventions`: UV as `Clutter.ParseTemplate` projects a
decoration (`CSVM/src/Mech3/Clutter.cs`), u along the texture's columns and v along its rows,
one unit per 256 m repeat. A lot's origin is wrapped into [0, 1); its polygons stay whole and
may cross the tile edge, so a consumer draws each one again shifted by -1 and +1 in u and v.
Numbers are rounded to 1e-5 UV (2.6 mm) and 0.1 m.

extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the checkout that does.
Embeds no game data. --plot writes, per paint, the exported data alone beside the same over the
paint; those images hold original pixels, so --plot refuses a directory inside the repo.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/c5-ground/export_layout.py [--out <json>] [--plot <dir outside the repo>]
"""
import argparse
import json
import math
import os
import sys
from collections import defaultdict

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import ConvexHull

# `py -I` leaves the script's own directory off the path.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import survey as S  # noqa: E402

C, E = S.C, S.E
OUT = os.path.join(S.REPO, "CSVM", "data", "city_models", "C5", "ground_layout.json")
LAMP, GLOW = "lightpole.flt", "w_lightglow.flt"
PX_PER_M = 4
GROW_M = 0.5
# Rows of the world census smaller than this are left out of a paint's sets (0.008 km2 of a tie
# between two unflagged paints, 0.016 km2 of flagged over flagged).
MIN_KM2 = 0.02


def uv5(x):
    return round(float(x), 5)


def lot_models(ch, blocks, deco_idx, name):
    """(model name, node whose meshes it draws) for every model the decoration's roll can pick.
    A substitute target draws the first gamez node of its name, as Clutter.ResolveModel does."""
    out = [(name, deco_idx)]
    for _, tgt in (blocks.get(name) or {}).get("substitute", []):
        if tgt == name:
            continue
        node = next((i for i in ch.by_name.get(tgt, []) if ch.parts(i)), None)
        if node is not None:
            out.append((tgt, node))
    return out


def lot_footprint(ch, decos, blocks):
    """Footprint pieces (template XZ metres, exact vertices) and top of one lot."""
    tris, top, names = [], -1e9, []
    for idx, name in decos:
        loc = ch.nodes[idx].local or E.IDENTITY
        for m, node in lot_models(ch, blocks, idx, name):
            names.append(m.replace(".flt", ""))
            for t in ch.geometry(node):
                pts = [E._apply(loc, p) for p in t[0]]
                tris.append([(p[0], p[2]) for p in pts])
                top = max(top, max(p[1] for p in pts))
    allp = np.array([p for t in tris for p in t])
    lo = allp.min(0) - 2
    w, h = (np.ptp(allp, 0) + 4) * PX_PER_M
    im = Image.new("L", (int(w) + 1, int(h) + 1), 0)
    d = ImageDraw.Draw(im)
    to_px = lambda p: ((p[0] - lo[0]) * PX_PER_M, (p[1] - lo[1]) * PX_PER_M)
    for t in tris:
        d.polygon([to_px(p) for p in t], fill=1, outline=1)
    grow = int(GROW_M * PX_PER_M)
    m = ndimage.binary_fill_holes(ndimage.binary_dilation(np.asarray(im) > 0, iterations=grow))
    # The grown mask shrunk back: the footprint with thin walls joined, to weigh the hulls against.
    solid = ndimage.binary_erosion(m, iterations=grow, border_value=0)
    lab, n = ndimage.label(m)
    pieces = defaultdict(list)
    for t in tris:
        for p in t:
            x, y = to_px(p)
            k = lab[min(lab.shape[0] - 1, int(y)), min(lab.shape[1] - 1, int(x))]
            if k:
                pieces[k].append(p)
    hulls, waste = [], 0.0
    for k, pts in pieces.items():
        pts = np.unique(np.round(np.array(pts), 3), axis=0)
        if len(pts) < 3:
            continue
        hull = ConvexHull(pts, qhull_options="QJ")
        hulls.append(pts[hull.vertices])
        waste += hull.volume - ((lab == k) & solid).sum() / PX_PER_M ** 2
    return hulls, top, sorted(set(names)), waste


def export_template(ch, blocks, t):
    root = E.find_template_root(ch.nodes, t)
    g = ch.nodes[E.first_with_mesh(ch.nodes, C._Meshes(ch), root, include_self=True)]
    aff, _ = S.ground_affine(ch, g)
    ground_y = np.mean([v["y"] for v in ch.models[g.model_index]["vertices"]])
    to_uv = lambda x, z: np.array([x, z, 1.0]) @ aff
    lots, lamps, glows = defaultdict(list), [], []
    for c in g.children:
        n = ch.nodes[c]
        name = n.name.lower()
        o = (n.local or E.IDENTITY)[1]
        if name in (LAMP, GLOW):
            uv = to_uv(o[0], o[2]) % 1.0
            (lamps if name == LAMP else glows).append([uv5(uv[0]), uv5(uv[1])])
            continue
        lots[round(o[0], 1), round(o[2], 1)].append((c, name))
    out, waste, area = [], 0.0, 0.0
    for (x, z), decos in sorted(lots.items()):
        hulls, top, names, w = lot_footprint(ch, decos, blocks)
        shift = np.floor(to_uv(x, z))
        polys = []
        for hpts in hulls:
            uv = [to_uv(px, pz) - shift for px, pz in hpts]
            polys.append([uv5(c) for q in uv for c in q])
            area += ConvexHull(hpts, qhull_options="QJ").volume
        o = to_uv(x, z) - shift
        out.append({"o": [uv5(o[0]), uv5(o[1])], "h": round(float(top - ground_y), 1),
                    "models": names, "poly": polys})
        waste += w
    return {"lots": out, "lamps": sorted(lamps), "glows": sorted(glows)}, waste, area


def raster(polys, n, mask=None):
    """The exported footprint polygons on an n x n grid of the tile, drawn with the +-1 shifts."""
    im = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(im)
    for flat in polys:
        pts = list(zip(flat[0::2], flat[1::2]))
        for i in (-1, 0, 1):
            for j in (-1, 0, 1):
                d.polygon([((u + i) * n, (v + j) * n) for u, v in pts], fill=1)
    return np.asarray(im) > 0


def ground_sets(ch):
    """{paint: [(sets, area m2)]} from survey.py's exact arrangement of the world's ground."""
    polys = S.world_polys(ch)
    rows = defaultdict(float)
    for labels, a in S.arrangement(polys).items():
        rows[S.stack_kind(polys, labels)] += a
    out = defaultdict(list)
    for (vis, sets), a in sorted(rows.items(), key=lambda x: -x[1]):
        if vis in S.OVERLAY and a >= MIN_KM2 * 1e6:
            out[vis].append((sets, a))
    return out


def dump(o, ind=0):
    """JSON with one line per lot, street and set: diff-friendly and still readable."""
    p = "  " * ind
    if isinstance(o, dict):
        return "{\n" + ",\n".join("%s  %s: %s" % (p, json.dumps(k), dump(v, ind + 1)) for k, v in o.items()) + "\n" + p + "}"
    if isinstance(o, list) and o and isinstance(o[0], dict):
        return "[\n" + ",\n".join(p + "  " + json.dumps(v, separators=(", ", ": ")) for v in o) + "\n" + p + "]"
    return json.dumps(o, separators=(", ", ": "))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT)
    ap.add_argument("--plot", help="write per-paint check images (original pixels) here")
    args = ap.parse_args()
    if args.plot and os.path.abspath(args.plot).lower().startswith(S.REPO.lower()):
        sys.exit("--plot must name a directory outside the repo: the plots hold original pixels")

    ch = C.Chapter()
    blocks, _ = C.zrd_blocks()
    T = {t: S.template(ch, t) for t in C.TEMPLATES}
    L = {t: S.tex_luma(T[t]["tex"]) for t in T}
    templates = {}
    for t in C.TEMPLATES:
        templates[t], waste, area = export_template(ch, blocks, t)
        print("%s: %d lots, %d lamps, %d glows; the hulls exceed the solid footprint by %.0f m2 of %.0f m2" % (
            t, len(templates[t]["lots"]), len(templates[t]["lamps"]), len(templates[t]["glows"]), waste, area))

    sets_on = ground_sets(ch)
    paints = {}
    for paint, standing in S.STANDING.items():
        built = np.zeros((S.GRID, S.GRID), bool)
        for s in standing:
            built |= S.footprint(T[s]["blds"], S.GRID, S.GRID)
        streets = S.find_streets(L[paint], built)
        st = S.street_mask(streets, S.GRID, S.GRID)
        fams = sorted({s["fam"] for s in streets})
        entry = {
            "streets": [{"family": {"+": "u+v", "-": "u-v"}.get(s["fam"], s["fam"]),
                         "centre": uv5(s["centre"]), "half": uv5(s["half"]),
                         "kerb_to_kerb_m": round(s["width_m"], 1)} for s in streets],
            # The cell edges lie on the street centrelines (Decisions 4 and 7). A centreline is
            # measured to within 1.5 m of the half tile and a seam need only fall inside the band
            # (16 m or more each side), so the origin snaps to an eighth of the tile: 0.5 on all.
            "cell": {"family": [{"+": "u+v", "-": "u-v"}.get(f, f) for f in fams],
                     "origin": [round(s["centre"] * 8) / 8 for s in sorted(streets, key=lambda s: s["fam"])]},
        }
        if paint in S.OVERLAY:
            rows = []
            for sets, a in sets_on[paint]:
                fp = np.zeros((S.GRID, S.GRID), bool)
                for s in sets:
                    fp |= raster([q for lot in templates[s]["lots"] for q in lot["poly"]], S.GRID)
                rows.append({"set": "+".join(sets) or "none", "km2": round(a / 1e6, 3),
                             "on_street": round(float((fp & st).sum() / max(1, fp.sum())), 3)})
            entry["sets"] = rows
        paints[paint] = entry

    doc = {
        "about": "C5 ground layout, measured by analysis/c5-ground/export_layout.py (PLAN-c5-ground B11). "
                 "Derived geometry only; regenerate with that script, never edit by hand.",
        "conventions": {
            "uv": "u along the texture's columns, v along its rows, one unit per 256 m repeat; the ground "
                  "is laid U+X V+Z with whole UVs at world multiples of 256 m on 99.5 % of it",
            "polygons": "flat [u0, v0, u1, v1, ...], convex, counter-clockwise or clockwise; a lot's origin is "
                        "wrapped into [0, 1) and its polygons are not, so draw each again shifted by -1 and +1 "
                        "in u and v; a lot's footprint is the union of its polygons",
            "h": "metres from the template ground to the lot's highest vertex over every model it can roll",
            "streets": "the band |c - centre - k| <= half for whole k, c = u, v, u + v or u - v by family",
            "cell": "index floor(c - origin) per family listed; take c from world XZ (u = x / 256, "
                    "v = z / 256), not from a polygon's own UV, whose origin differs between polygons",
            "sets": "building sets standing on the visible paint, with the world area in km2; none is "
                    "flagged ground with no layer beneath; on_street is the share of the set's footprint "
                    "inside the paint's streets",
        },
        "templates": templates,
        "paints": paints,
    }
    text = dump(doc) + "\n"
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %s (%d bytes)" % (args.out, len(text.encode("utf-8"))))

    # The B11 check: the exported lots must cover survey.py's footprints of the placed models. One
    # pixel (0.25 m) of tolerance absorbs the two rasterisers' different edge pixels.
    for t in C.TEMPLATES:
        n = 1024
        exp = raster([q for lot in templates[t]["lots"] for q in lot["poly"]], n)
        a1 = S.footprint(T[t]["blds"], n, n)
        cov = (ndimage.binary_dilation(exp) & a1).sum() / a1.sum()
        print("%s: exported footprint covers %.5f of the placed models' footprint and is %.2f times its area" % (
            t, cov, exp.sum() / a1.sum()))
    if args.plot:
        plot(doc, L, args.plot)


def plot(doc, L, out, n=512):
    """Per paint: on the left the exported data alone, on the right the same over the paint.
    Streets yellow, the first standing set's lots green, the others magenta, lamps cyan."""
    os.makedirs(out, exist_ok=True)
    for paint, sets in S.STANDING.items():
        lum = np.asarray(Image.fromarray(L[paint].astype(np.uint8)).resize((n, n), Image.NEAREST), dtype=float)
        base = np.clip((lum / 255) ** 0.45 * 2.2, 0, 1)
        y, x = np.mgrid[0:n, 0:n]
        u, v = (x + 0.5) / n, (y + 0.5) / n
        coord = {"u": u, "v": v, "u+v": u + v, "u-v": u - v}
        st = np.zeros((n, n), bool)
        for s in doc["paints"][paint]["streets"]:
            st |= np.abs((coord[s["family"]] - s["centre"] + 0.5) % 1.0 - 0.5) <= s["half"]
        panes = []
        for under in (np.zeros((n, n)), base):
            a = np.stack([under] * 3, -1)
            a[st] = a[st] * 0.5 + np.array([0.5, 0.45, 0.0])
            for k, s in enumerate(sets):
                m = raster([q for lot in doc["templates"][s]["lots"] for q in lot["poly"]], n)
                edge = m & ~ndimage.binary_erosion(m)
                a[edge] = (0, 1, 0) if k == 0 else (1, 0, 1)
            for lu, lv in doc["templates"][sets[0]]["lamps"]:
                r, c = int(lv * n), int(lu * n)
                a[np.arange(r - 2, r + 3)[:, None] % n, np.arange(c - 2, c + 3) % n] = (0, 1, 1)
            a[0, :] = a[:, 0] = (1, 0, 0)
            panes.append(a)
        img = np.concatenate([panes[0], np.ones((n, 8, 3)), panes[1]], axis=1)
        Image.fromarray((img * 255).astype(np.uint8)).save(os.path.join(out, "layout_%s.png" % paint))
    print("plots written to %s" % out)


if __name__ == "__main__":
    main()
