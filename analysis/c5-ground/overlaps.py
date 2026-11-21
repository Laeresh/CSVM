"""C5 placed-building census for issue #363: overlapping pairs and repeated rows.

Stamps every building lot of `ground_layout.json` into the world the way `Clutter.PlaceOnTriangle`
does (`CSVM/src/Mech3/Clutter.cs`): per unflagged `cblock` triangle of world1 (survey.py's
`world_polys`), every whole UV repeat its UV box spans, every lot whose origin the triangle
contains, mapped through the triangle's own UV->XZ affine. A lot's footprint is its own, in metres
round that point: the model keeps its template-frame basis. The world's modelled buildings
(`world_buildings`) join the census as convex pieces. Then:

- overlaps: pairs whose world footprints intersect by more than OVERLAP_M2, excluding pairs that
  already overlap inside their template (those repeat on every tile), counted by kind; those
  with a stamp in them and outside a squeezed frame are listed;
- rows: per polygon, a lot placed again closer than ROW_M to itself, which a 256 m repeat never does.

`--at x,z` lists what stands within 120 m of a world point. Embeds no game data.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/c5-ground/overlaps.py [--at x,z]
"""
import argparse
import json
import math
import os
import re
import sys
from collections import defaultdict

import numpy as np
from scipy.spatial import ConvexHull

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import survey as S  # noqa: E402

LAYOUT = os.path.join(S.REPO, "CSVM", "data", "city_models", "C5", "ground_layout.json")
OVERLAP_M2 = 4.0
# The city's wall and roof textures (analysis/city-hulls/FINDINGS.md): what makes a mesh a building.
BUILDING_TEX = re.compile(r"(sm|gen)?bldg\d|roof0")
ROW_M = 128.0


def ccw(p):
    a = sum(p[i][0] * p[i - 1][1] - p[i - 1][0] * p[i][1] for i in range(len(p)))
    return p if a < 0 else p[::-1]   # S.half's left side, as S.split uses it


def inter_area(a, b):
    """Area of the intersection of two convex polygons."""
    rest, b = a, ccw(b)
    for k in range(len(b)):
        rest = S.half(rest, b[k - 1], b[k], left=True)
        if len(rest) < 3:
            return 0.0
    return S.poly_area(rest)


def affine(uvs, xz):
    """(u, v, 1) -> (x, z) for one triangle."""
    a = np.array([[u, v, 1.0] for u, v in uvs])
    return np.linalg.solve(a, np.array(xz))


def stamp(polys, layout, to_m):
    """Every placed lot. Only the origin goes through the triangle's map: the model keeps its
    template-frame basis (`cell.OnQuad.Basis`), so its footprint is the lot's own, in metres."""
    placed, seen = [], set()
    for pi, p in enumerate(polys):
        if p["flag"] or p["tex"] not in layout["templates"]:
            continue
        lots = layout["templates"][p["tex"]]["lots"]
        inv = to_m[p["tex"]]
        for xz, uvs, _ in p["tris"]:
            if abs(S._cross(xz)) < 1.0:   # Clutter.Lattice's 0.5 m2 floor
                continue
            try:
                m = affine(uvs, xz)
            except np.linalg.LinAlgError:
                continue
            (u0, v0), (u1, v1), (u2, v2) = uvs
            det = (u1 - u0) * (v2 - v0) - (u2 - u0) * (v1 - v0)
            us, vs = [q[0] for q in uvs], [q[1] for q in uvs]
            for ui in range(math.floor(min(us)), math.floor(max(us)) + 1):
                for vi in range(math.floor(min(vs)), math.floor(max(vs)) + 1):
                    for li, lot in enumerate(lots):
                        cu, cv = ui + lot["o"][0], vi + lot["o"][1]
                        b1 = ((cu - u0) * (v2 - v0) - (u2 - u0) * (cv - v0)) / det
                        b2 = ((u1 - u0) * (cv - v0) - (cu - u0) * (v1 - v0)) / det
                        if b1 < 0 or b2 < 0 or b1 + b2 > 1:
                            continue
                        o = np.array([cu, cv, 1.0]) @ m
                        key = (p["tex"], li, round(o[0] * 4), round(o[1] * 4))
                        if key in seen:
                            continue
                        seen.add(key)
                        fp = []
                        for flat in lot["poly"]:
                            d = np.array([[u - lot["o"][0], v - lot["o"][1]] for u, v in zip(flat[0::2], flat[1::2])])
                            fp.append([tuple(r) for r in d @ inv + o])
                        placed.append({"tpl": p["tex"], "lot": li, "x": o[0], "z": o[1], "fp": fp,
                                       "h": lot["h"], "models": lot["models"], "poly": pi,
                                       "node": p["node"], "jac": np.linalg.norm(m[:2], axis=1)})
    return placed


def world_buildings(ch, min_h=4.0):
    """The world's own modelled buildings: every non-`cblock` mesh reached from world1 (nearest
    LOD, template subtrees left out), split into pieces joined by shared vertices, each as its XZ
    convex hull, kept when it stands at least `min_h` tall."""
    wi = next(i for i, n in enumerate(ch.nodes) if n.kind == "World" and n.name.lower() == "world1")
    xf = S.E.build_transform_cache(ch.nodes, wi)
    skip = set()
    for t in S.OVERLAY + S.BASE:
        stack = [S.E.find_template_root(ch.nodes, t)]
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
        polys = [p for p in m["polygons"] if p.get("materials") and
                 BUILDING_TEX.match((ch.mat_tex[p["materials"][0]["material_index"]] or "").lower())]
        if not polys:
            continue
        vs = np.array([S.E._apply(x, (v["x"], v["y"], v["z"])) for v in m["vertices"]])
        root = list(range(len(vs)))

        def find(a):
            while root[a] != a:
                root[a] = root[root[a]]
                a = root[a]
            return a
        for p in polys:
            vi = p["vertex_indices"]
            for a in vi[1:]:
                root[find(a)] = find(vi[0])
        # Vertices at one place join too: a modelled block often repeats corners per face.
        at = {}
        for i, v in enumerate(vs):
            k = (round(v[0], 2), round(v[1], 2), round(v[2], 2))
            if k in at:
                root[find(i)] = find(at[k])
            else:
                at[k] = i
        pieces = defaultdict(set)
        for p in polys:
            for a in p["vertex_indices"]:
                pieces[find(a)].add(a)
        for vi in pieces.values():
            pts = vs[sorted(vi)]
            if np.ptp(pts[:, 1]) < min_h or len(pts) < 3:
                continue
            xz = np.unique(np.round(pts[:, [0, 2]], 3), axis=0)
            try:
                hull = ConvexHull(xz, qhull_options="QJ")
            except Exception:
                continue
            out.append({"tpl": "world", "lot": idx, "x": float(xz[:, 0].mean()), "z": float(xz[:, 1].mean()),
                        "fp": [[tuple(xz[k]) for k in hull.vertices]], "h": float(np.ptp(pts[:, 1])),
                        "models": [n.name], "poly": -1, "node": idx, "jac": (0.0, 0.0)})
    return out


def template_overlaps(layout):
    """(tpl, lot a, lot b) pairs that overlap inside the template itself, tile repeats included."""
    out = set()
    for t, d in layout["templates"].items():
        lots = d["lots"]
        for i in range(len(lots)):
            for j in range(i + 1, len(lots)):
                for di in (-1, 0, 1):
                    for dj in (-1, 0, 1):
                        for fa in lots[i]["poly"]:
                            for fb in lots[j]["poly"]:
                                a = list(zip(fa[0::2], fa[1::2]))
                                b = [(u + di, v + dj) for u, v in zip(fb[0::2], fb[1::2])]
                                if inter_area(a, b) * 256 * 256 > OVERLAP_M2:
                                    out.add((t, i, j))
    return out


def overlaps(placed, same_tpl):
    grid = defaultdict(list)
    for k, b in enumerate(placed):
        grid[int(b["x"] // 128), int(b["z"] // 128)].append(k)
    out = []
    for (gx, gz), ks in grid.items():
        near = [m for dx in (-1, 0, 1) for dz in (-1, 0, 1) for m in grid.get((gx + dx, gz + dz), [])]
        for a in ks:
            for b in near:
                if b <= a:
                    continue
                A, B = placed[a], placed[b]
                if A["tpl"] == B["tpl"] and (A["tpl"], *sorted((A["lot"], B["lot"]))) in same_tpl:
                    continue
                area = sum(inter_area(fa, fb) for fa in A["fp"] for fb in B["fp"])
                if area > OVERLAP_M2:
                    out.append((area, A, B))
    return out


def rows(placed):
    by = defaultdict(list)
    for b in placed:
        if b["tpl"] != "world":
            by[b["tpl"], b["lot"]].append(b)
    out = []
    for key, bs in by.items():
        grid = defaultdict(list)
        for b in bs:
            grid[int(b["x"] // ROW_M), int(b["z"] // ROW_M)].append(b)
        for b in bs:
            gx, gz = int(b["x"] // ROW_M), int(b["z"] // ROW_M)
            for dx in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for c in grid.get((gx + dx, gz + dz), []):
                        d = math.hypot(c["x"] - b["x"], c["z"] - b["z"])
                        if 0.25 < d < ROW_M and id(c) > id(b):
                            out.append((d, b, c))
    return out


def desc(b):
    return "%s lot %d (%s, h %.0f) at (%.1f, %.1f), poly %d node %d, m/repeat %.0f x %.0f" % (
        b["tpl"], b["lot"], "+".join(b["models"][:2]), b["h"], b["x"], b["z"], b["poly"], b["node"],
        b["jac"][0], b["jac"][1])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--at", help="x,z: list the placed lots within 120 m")
    args = ap.parse_args()
    with open(LAYOUT, encoding="utf-8") as f:
        layout = json.load(f)
    ch = S.C.Chapter()
    # dUV -> dXZ metres in each template's own frame.
    to_m = {t: np.linalg.inv(S.template(ch, t)["aff"][:2]) for t in layout["templates"]}
    polys = S.world_polys(ch)
    placed = stamp(polys, layout, to_m)
    print("placed lots: %d" % len(placed))
    world = world_buildings(ch)
    print("modelled world pieces: %d in %d nodes" % (len(world), len({b["node"] for b in world})))
    placed += world
    if args.at:
        x, z = map(float, args.at.split(","))
        for b in sorted(placed, key=lambda b: math.hypot(b["x"] - x, b["z"] - z)):
            if math.hypot(b["x"] - x, b["z"] - z) < 120:
                print("  " + desc(b))
        return

    ov = overlaps(placed, template_overlaps(layout))
    print("overlapping pairs over %.0f m2: %d" % (OVERLAP_M2, len(ov)))
    squeezed = lambda b: b["tpl"] != "world" and min(b["jac"]) < 200
    by_kind = defaultdict(int)
    for _, a, b in ov:
        by_kind["squeezed frame" if squeezed(a) or squeezed(b) else
                "+".join(sorted(("world" if q["tpl"] == "world" else "stamp") for q in (a, b)))] += 1
    print("  by kind: %s" % dict(by_kind))
    for area, a, b in sorted(ov, key=lambda r: -r[0]):
        # A world piece is a convex hull, so two pieces of one landmark (a base, its tower) overlap
        # by construction; only pairs with a stamp in them are listed.
        if squeezed(a) or squeezed(b) or a["tpl"] == b["tpl"] == "world":
            continue
        print("  %.0f m2: %s\n          %s" % (area, desc(a), desc(b)))
    rw = rows(placed)
    print("same lot again within %.0f m: %d pairs" % (ROW_M, len(rw)))
    groups = defaultdict(list)
    for d, b, c in rw:
        if b["poly"] == c["poly"]:
            groups[b["poly"]].append(d)
    for pi, ds in sorted(groups.items(), key=lambda kv: -len(kv[1])):
        p = polys[pi]
        xs = [q[0] for t in p["tris"] for q in t[0]]
        zs = [q[1] for t in p["tris"] for q in t[0]]
        print("  poly %d (node %d, %s, %s at %d m/repeat): %d pairs, pitch %.1f m, x %.0f..%.0f z %.0f..%.0f" % (
            pi, p["node"], p["tex"], p["frame"][0], p["frame"][1], len(ds), min(ds), min(xs), max(xs), min(zs), max(zs)))


if __name__ == "__main__":
    main()
