import json
import sys
from collections import Counter, defaultdict

import numpy as np

# python -I a1_strips.py <gamez dir> [<texture dir>] [strip texture ...]
# What lies under the painted-coast strips (C3 shore1/shore_trans, C2 beach1): every other surface
# whose XZ footprint covers a sample point inside a strip, with its height there.
root = sys.argv[1]
tex_dir = sys.argv[2] if len(sys.argv) > 2 else None
strip_names = [s.lower() for s in sys.argv[3:]] or ["shore1.tif", "shore_trans.tif", "beach1.tif"]

with open(f"{root}/textures.json", encoding="utf-8") as f:
    names = [t["name"] for t in json.load(f)]
with open(f"{root}/materials.json", encoding="utf-8") as f:
    mats = json.load(f)
mat_tex = {}
water_mats = set()
for i, m in enumerate(mats):
    body = next(iter(m.values()))
    if "texture_index" in body:
        n = names[body["texture_index"]].lower()
        mat_tex[i] = n
        if n.startswith(("srf", "wtr", "water")):
            water_mats.add(i)
with open(f"{root}/models.json", encoding="utf-8") as f:
    models = json.load(f)
with open(f"{root}/nodes.json", encoding="utf-8") as f:
    nodes = json.load(f)


def local_xf(node):
    # As the engine's GameZ.ParseTransform: "Initial" is the identity; RotateTranslateScale takes the
    # stored matrix when present (its rows are the rotation's columns), else Euler radians composed
    # R = Ry . Rx . Rz (Godot's YXZ). Scale is unit everywhere and ignored, as the engine ignores it.
    body = next(iter(node["data"].values()))
    m = np.eye(4)
    t = body.get("transform") if isinstance(body, dict) else None
    if isinstance(t, dict) and "RotateTranslateScale" in t:
        rts = t["RotateTranslateScale"]
        r, tr, orig = rts["rotate"], rts["translate"], rts.get("original")
        if isinstance(orig, dict):
            m[:3, :3] = np.array([[orig["r%d%d" % (i, j)] for j in range(3)] for i in range(3)]).T
        else:
            cx, sx = np.cos(r["x"]), np.sin(r["x"])
            cy, sy = np.cos(r["y"]), np.sin(r["y"])
            cz, sz = np.cos(r["z"]), np.sin(r["z"])
            rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
            ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
            rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
            m[:3, :3] = ry @ rx @ rz
        m[:3, 3] = [tr["x"], tr["y"], tr["z"]]
    return m


def rotated(node):
    body = next(iter(node["data"].values()))
    t = body.get("transform") if isinstance(body, dict) else None
    if not (isinstance(t, dict) and "RotateTranslateScale" in t):
        return False
    rts = t["RotateTranslateScale"]
    return isinstance(rts.get("original"), dict) or any(abs(v) > 1e-9 for v in rts["rotate"].values())


def chain(i):
    while True:
        yield i
        p = nodes[i].get("parent_indices")
        if not p:
            return
        i = p[0]


cache = {}


def xf_of(i):
    if i in cache:
        return cache[i]
    n = nodes[i]
    m = local_xf(n)
    if n.get("parent_indices"):
        m = xf_of(n["parent_indices"][0]) @ m
    cache[i] = m
    return m


strips = []  # (texture, world verts, uv)
tris, tri_tex, tri_node = [], [], []  # every other polygon, fan-triangulated
land_near = []  # land vertices within 1 m of sea level (the shore-water scripts' "land")
strip_nodes = set()
for i, n in enumerate(nodes):
    mi = n["model_index"]
    if mi < 0:
        continue
    model = models[mi]
    if not model["vertices"]:
        continue
    m = xf_of(i)
    verts = np.array([[v["x"], v["y"], v["z"], 1.0] for v in model["vertices"]])
    wv = (m @ verts.T).T[:, :3]
    for p in model["polygons"]:
        if not p["materials"]:
            continue
        idx = p["vertex_indices"]
        mat = p["materials"][0]["material_index"]
        tex = mat_tex.get(mat, "<colour>")
        w = wv[idx]
        if tex in strip_names:
            strips.append((tex, w, np.array([[uv["u"], uv["v"]] for uv in p["materials"][0]["uv_coords"]])))
            strip_nodes.add(i)
            continue
        if mat not in water_mats:
            land_near.extend(w[np.abs(w[:, 1]) < 1.0][:, [0, 2]])
        for k in range(1, len(w) - 1):
            tris.append((w[0], w[k], w[k + 1]))
            tri_tex.append(tex)
            tri_node.append(n["name"])

tris = np.array(tris)
tri_tex = np.array(tri_tex)
tri_water = np.array([t.startswith(("srf", "wtr", "water")) for t in tri_tex])
tri_sky = np.array([t.startswith("sky") for t in tri_tex])
land_near = np.array(land_near)
print("strip polygons:", dict(Counter(s[0] for s in strips)), "other triangles:", len(tris))
rot_any = sum(rotated(nodes[i]) for i in range(len(nodes)) if nodes[i]["model_index"] >= 0)
print("strip nodes: %d, with a rotation anywhere up the chain: %d (rotated model nodes in the chapter: %d)"
      % (len(strip_nodes), sum(any(rotated(nodes[k]) for k in chain(i)) for i in strip_nodes), rot_any))
low = tris[:, :, 1].min(axis=1)
print("lowest other geometry: %.1f m (%s); triangles below -0.25 m above the -1000 m cut-off: %d"
      % (low.min(), tri_node[int(low.argmin())], int(((low < -0.25) & (low > -1000)).sum())))

# Census, as #270 measured it: y span, width across the alpha axis, each edge to the nearest land vertex.
tex_axis = {}
if tex_dir:
    from PIL import Image

    for t in sorted({s[0] for s in strips}):
        a = np.asarray(Image.open(f"{tex_dir}/{t.rsplit('.', 1)[0]}.png").convert("RGBA"), dtype=float)[:, :, 3]
        col, row = a.mean(axis=0), a.mean(axis=1)  # along U (columns), along V (rows)
        axis = 0 if np.ptp(col) >= np.ptp(row) else 1
        prof = col if axis == 0 else row
        tex_axis[t] = axis
        print("%s %dx%d: alpha varies along %s; profile (8 bins) %s" % (t, a.shape[1], a.shape[0], "UV"[axis], np.round(prof.reshape(8, -1).mean(axis=1)).astype(int).tolist()))
        over = np.nonzero(prof >= 127.5)[0]
        print("  mean alpha passes 0.5 at %s = %.3f and falls below it at %.3f" % ("UV"[axis], over[0] / len(prof), (over[-1] + 1) / len(prof)))


def nearest_land(pts):
    return np.array([np.sqrt(((land_near - p) ** 2).sum(axis=1)).min() for p in pts])


for t in sorted({s[0] for s in strips}):
    mine = [s for s in strips if s[0] == t]
    ys = np.concatenate([w[:, 1] for _, w, _ in mine])
    print("\n%s: %d polygons, y min %.3f max %.3f" % (t, len(mine), ys.min(), ys.max()))
    uvr = Counter((round(uv[:, 0].min(), 2), round(uv[:, 0].max(), 2), round(uv[:, 1].min(), 2), round(uv[:, 1].max(), 2)) for _, _, uv in mine)
    print("  UV ranges (u min, u max, v min, v max): polygons", uvr.most_common(8))
    for ax in (0, 1):
        widths, lo_pts, hi_pts = [], [], []
        for _, w, uv in mine:
            c = uv[:, ax]
            lo = w[c <= c.min() + 0.05]
            hi = w[c >= c.max() - 0.05]
            if np.ptp(c) > 0.05:
                widths.append(np.linalg.norm(lo.mean(axis=0)[[0, 2]] - hi.mean(axis=0)[[0, 2]]))
                lo_pts.extend(lo[:, [0, 2]])
                hi_pts.extend(hi[:, [0, 2]])
        if not widths:
            continue
        widths = np.array(widths)
        print("  across %s: min %.1f p10 %.1f median %.1f p90 %.1f max %.1f m (%d polygons span it)"
              % ("UV"[ax], widths.min(), np.percentile(widths, 10), np.median(widths), np.percentile(widths, 90), widths.max(), len(widths)))
        for side, pts in (("min", lo_pts), ("max", hi_pts)):
            d = nearest_land(np.array(pts))
            print("    %s-%s edge vertices to the nearest land vertex (|y| < 1 m): median %.1f m, p10 %.1f, p90 %.1f"
                  % ("UV"[ax], side, np.median(d), np.percentile(d, 10), np.percentile(d, 90)))


def surfaces_at(p, cand):
    # Every candidate triangle covering XZ point p, with its height there (barycentric, XZ projection).
    a, b, c = tris[cand, 0], tris[cand, 1], tris[cand, 2]
    v0, v1 = b[:, [0, 2]] - a[:, [0, 2]], c[:, [0, 2]] - a[:, [0, 2]]
    v2 = p - a[:, [0, 2]]
    den = v0[:, 0] * v1[:, 1] - v1[:, 0] * v0[:, 1]
    ok = np.abs(den) > 1e-6  # vertical faces have no footprint
    den = np.where(ok, den, 1.0)
    l1 = (v2[:, 0] * v1[:, 1] - v1[:, 0] * v2[:, 1]) / den
    l2 = (v0[:, 0] * v2[:, 1] - v2[:, 0] * v0[:, 1]) / den
    inside = ok & (l1 >= -1e-6) & (l2 >= -1e-6) & (l1 + l2 <= 1 + 1e-6)
    y = a[:, 1] + l1 * (b[:, 1] - a[:, 1]) + l2 * (c[:, 1] - a[:, 1])
    return cand[inside], y[inside]


def samples(w, coord, step=6.0):
    # Barycentric grid over each fan triangle, about one point per step metres along an edge,
    # with the texture coordinate across the strip (coord, per vertex) interpolated to each point.
    out, cs = [], []
    for k in range(1, len(w) - 1):
        a, b, c = w[0][[0, 2]], w[k][[0, 2]], w[k + 1][[0, 2]]
        n = max(2, int(max(np.linalg.norm(b - a), np.linalg.norm(c - a)) / step))
        for i in range(n + 1):
            for j in range(n + 1 - i):
                s, r = (i + 0.5) / (n + 1.5), (j + 0.5) / (n + 1.5)
                out.append(a + (b - a) * s + (c - a) * r)
                cs.append(coord[0] + (coord[k] - coord[0]) * s + (coord[k + 1] - coord[0]) * r)
    return np.array(out), np.array(cs)


bb_lo = tris[:, :, [0, 2]].min(axis=1)
bb_hi = tris[:, :, [0, 2]].max(axis=1)
FLOOR = -1000.0  # the world floor sits kilometres down (C1B's g1165 at -11,680 m)

# Union strips into coast runs by shared vertices, so the answer can be read along the coast.
parent = list(range(len(strips)))


def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i


owner = {}
for i, (_, w, _) in enumerate(strips):
    for v in w:
        key = (round(v[0]), round(v[2]))
        if key in owner:
            parent[find(i)] = find(owner[key])
        else:
            owner[key] = i

per_tex = defaultdict(lambda: Counter())
heights = defaultdict(list)
under_tex = defaultdict(Counter)
runs = defaultdict(lambda: {"n": 0, "pts": [], "top": [], "cls": Counter()})
self_check = []
by_coord = defaultdict(Counter)
land_y = defaultdict(list)
for si, (t, w, uv) in enumerate(strips):
    pts, cs = samples(w, uv[:, tex_axis.get(t, 0)])
    lo, hi = w[:, [0, 2]].min(axis=0), w[:, [0, 2]].max(axis=0)
    cand = np.nonzero((bb_hi[:, 0] >= lo[0]) & (bb_lo[:, 0] <= hi[0]) & (bb_hi[:, 1] >= lo[1]) & (bb_lo[:, 1] <= hi[1]))[0]
    run = runs[find(si)]
    run["n"] += 1
    run["pts"].append(w.mean(axis=0))
    for p, cv in zip(pts, cs):
        hit, y = surfaces_at(p, cand)
        floor_hit = (y <= FLOOR).any()
        keep = (y > FLOOR) & ~tri_sky[hit]  # the sky dome is no surface
        hit, y = hit[keep], y[keep]
        txs, water = tri_tex[hit], tri_water[hit]
        land = ~water
        at0 = np.abs(y) <= 0.25
        parts = []
        if (land & (y < -0.25)).any():
            parts.append("land below")
        if (land & at0).any():
            parts.append("land at 0")
        if (water & at0).any():
            parts.append("water at 0")
        if (water & ~at0).any():
            parts.append("water off 0")
        if (land & (y > 0.25)).any():
            parts.append("land above (%s)" % ("<2 m" if y[land & (y > 0.25)].max() < 2 else "<10 m" if y[land & (y > 0.25)].max() < 10 else ">=10 m"))
        cls = " + ".join(parts) or ("the world floor only" if floor_hit else "nothing at all")
        per_tex[t][cls] += 1
        run["cls"][cls] += 1
        short = "land at 0" if "land at 0" in parts else "water at 0 only" if parts == ["water at 0"] else "other" if parts else "nothing or floor"
        by_coord[(t, min(8, int(cv * 8)))][short] += 1
        land_y[t].extend(y[land & (y < 10)])
        for tx2, y2 in zip(txs, y):
            under_tex[t]["%s %s" % (tx2, "below" if y2 < -0.25 else "at 0" if y2 <= 0.25 else "above")] += 1
        if len(y):
            heights[t].append(y.min())
            run["top"].append(y.min())
    if si < 20:
        # METHOD-9: the same sampler over the strip's own triangles must find it at its own height.
        own = np.array([(w[0], w[k], w[k + 1]) for k in range(1, len(w) - 1)])
        saved = tris
        tris = own
        found = [surfaces_at(p, np.arange(len(own)))[1] for p in pts]
        tris = saved
        self_check.append(all(len(f) >= 1 and np.allclose(f, w[:, 1].mean(), atol=0.01) for f in found))
assert all(self_check), "sampler failed to find a strip under its own samples"
print("\nself-check: the sampler finds each of %d strips under all of its own samples" % len(self_check))

for t in sorted(per_tex):
    tot = sum(per_tex[t].values())
    print("\n%s, %d sample points: the surfaces covering each (sky and the world floor excluded)" % (t, tot))
    for cls, c in per_tex[t].most_common():
        print("  %-48s %6d  %5.1f %%" % (cls, c, 100.0 * c / tot))
    h = np.array(heights[t])
    if len(h):
        print("  lowest surface's height: min %.2f p1 %.2f p10 %.2f median %.2f p90 %.2f max %.2f m"
              % (h.min(), np.percentile(h, 1), np.percentile(h, 10), np.median(h), np.percentile(h, 90), h.max()))
    ly = np.array(land_y[t])
    if len(ly):
        print("  land hits below 10 m: %d, height min %.3f p1 %.3f median %.3f p99 %.3f max %.3f m"
              % (len(ly), ly.min(), np.percentile(ly, 1), np.median(ly), np.percentile(ly, 99), ly.max()))
    print("  surfaces by texture and height (sample hits):", [(str(k), v) for k, v in under_tex[t].most_common(14)])

print("\nacross the strip (the alpha axis in eighths of the texture; the last row is beyond 1.0):")
for (t, q), c in sorted(by_coord.items()):
    tot = sum(c.values())
    print("  %s %s in [%.3f, %.3f): %5d samples  %s" % (t, "UV"[tex_axis.get(t, 0)], q / 8, (q + 1) / 8, tot,
          ", ".join("%s %.0f %%" % (k, 100.0 * v / tot) for k, v in c.most_common())))

print("\ncoast runs (strips joined by shared vertices), largest first:")
for r in sorted(runs.values(), key=lambda r: -r["n"])[:25]:
    c = np.array(r["pts"]).mean(axis=0)
    span = np.ptp(np.array(r["pts"])[:, [0, 2]], axis=0)
    top = np.array(r["top"]) if r["top"] else np.array([np.nan])
    print("  %3d strips centre (%.0f, %.0f) span %.0fx%.0f m  lowest surface median %.2f min %.2f max %.2f  %s"
          % (r["n"], c[0], c[2], span[0], span[1], np.nanmedian(top), np.nanmin(top), np.nanmax(top), dict(r["cls"])))

# Pose candidates: in each of the largest runs, the strip nearest the run's centre, the eye 150 m up
# and 150 m out on its water side (the coordinate-0 edge, the dark painted water in every texture).
print("\npose candidates (largest runs):")
for root_i, r in sorted(runs.items(), key=lambda kv: -kv[1]["n"])[:4]:
    members = [i for i in range(len(strips)) if find(i) == root_i]
    c = np.array(r["pts"]).mean(axis=0)
    si = min(members, key=lambda i: np.linalg.norm(strips[i][1].mean(axis=0) - c))
    t, w, uv = strips[si]
    k = uv[:, tex_axis.get(t, 0)]
    sea = w[k <= k.min() + 0.05].mean(axis=0) - w[k >= k.max() - 0.05].mean(axis=0)
    sea = sea[[0, 2]] / (np.linalg.norm(sea[[0, 2]]) or 1.0)
    mid = w.mean(axis=0)
    eye = mid[[0, 2]] + sea * 150.0
    print("  '--pos=%.0f,150,%.0f' '--lookat=%.0f,0,%.0f'  (%s in a run of %d strips)" % (eye[0], eye[1], mid[0], mid[2], t, r["n"]))
