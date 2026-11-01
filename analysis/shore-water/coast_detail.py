import json
import sys
from collections import Counter

import numpy as np

root = sys.argv[1]
srf_name = sys.argv[2] if len(sys.argv) > 2 else "srf0001.tif"

with open(f"{root}/textures.json", encoding="utf-8") as f:
    names = [t["name"] for t in json.load(f)]
with open(f"{root}/materials.json", encoding="utf-8") as f:
    mats = json.load(f)
srf_mats = set()
water_mats = set()
for i, m in enumerate(mats):
    body = next(iter(m.values()))
    if "texture_index" in body:
        n = names[body["texture_index"]].lower()
        if n.startswith("srf"):
            srf_mats.add(i)
        if n.startswith(("srf", "wtr", "water")):
            water_mats.add(i)
print("surf materials:", {i: names[next(iter(mats[i].values()))["texture_index"]] for i in srf_mats})
with open(f"{root}/models.json", encoding="utf-8") as f:
    models = json.load(f)
with open(f"{root}/nodes.json", encoding="utf-8") as f:
    nodes = json.load(f)


def local_xf(node):
    # "Initial" is the identity; RotateTranslateScale carries Euler radians, a translation and a scale.
    body = next(iter(node["data"].values()))
    m = np.eye(4)
    t = body.get("transform") if isinstance(body, dict) else None
    if isinstance(t, dict) and "RotateTranslateScale" in t:
        rts = t["RotateTranslateScale"]
        r, tr, sc = rts["rotate"], rts["translate"], rts["scale"]
        cx, sx = np.cos(r["x"]), np.sin(r["x"])
        cy, sy = np.cos(r["y"]), np.sin(r["y"])
        cz, sz = np.cos(r["z"]), np.sin(r["z"])
        rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
        ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
        m[:3, :3] = (rz @ ry @ rx) @ np.diag([sc["x"], sc["y"], sc["z"]])
        m[:3, 3] = [tr["x"], tr["y"], tr["z"]]
    return m


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


sample = next(n for n in nodes if n["model_index"] >= 0)
print("transform sample:", json.dumps(next(iter(sample["data"].values())).get("transform"))[:200])

srf = []
land_tris = []
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
        if mat in srf_mats:
            srf.append((wv[idx], np.array([[uv["u"], uv["v"]] for uv in p["materials"][0]["uv_coords"]]), p["vertex_colors"]))
        elif mat not in water_mats:
            w = wv[idx]
            if w[:, 1].min() < 0.25 and w[:, 1].max() > -0.25:
                land_tris.append(w)

print("surf polygons:", len(srf))
if srf:
    ys = np.concatenate([s[0][:, 1] for s in srf])
    print("surf y min %.2f max %.2f" % (ys.min(), ys.max()))
    # Vertex colour against u: is the dark grey on the sea side (u = 1) or the land side?
    by_u = Counter()
    for w, uv, cols in srf:
        if cols:
            for u, c in zip(uv[:, 0], cols):
                by_u[(round(float(u)), c["r"])] += 1
    print("colour r by u side (u, r): count", sorted(by_u.items()))
    widths = []
    for w, uv, _ in srf:
        lo = w[uv[:, 0] <= uv[:, 0].min() + 0.05]
        hi = w[uv[:, 0] >= uv[:, 0].max() - 0.05]
        if len(lo) and len(hi):
            widths.append(np.linalg.norm(lo.mean(axis=0)[[0, 2]] - hi.mean(axis=0)[[0, 2]]))
    widths = np.array(widths)
    print("ring width m: min %.1f p10 %.1f median %.1f p90 %.1f max %.1f" % (widths.min(), np.percentile(widths, 10), np.median(widths), np.percentile(widths, 90), widths.max()))
    # Which edge of the ring is the land edge: the distance from its u = 0 and u = 1 vertices to the
    # nearest land vertex within 1 m of sea level, over a sample of ring vertices.
    land_near = np.concatenate([w for w in land_tris])
    land_near = land_near[np.abs(land_near[:, 1]) < 1.0][:, [0, 2]]
    for side, pick in (("u = 0", lambda u: u <= 0.05), ("u = 1", lambda u: u >= 0.95)):
        pts = np.concatenate([w[pick(uv[:, 0])][:, [0, 2]] for w, uv, _ in srf if pick(uv[:, 0]).any()])
        pts = pts[:: max(1, len(pts) // 300)]
        d = np.array([np.sqrt(((land_near - p) ** 2).sum(axis=1)).min() for p in pts])
        print("%s vertices to the nearest land vertex (|y| < 1 m): median %.1f m, 90th percentile %.1f m" % (side, np.median(d), np.percentile(d, 90)))

# Land slope at the coastline: land polygons with an edge at sea level; rise of the far vertex over its horizontal distance.
slopes = []
for w in land_tris:
    at0 = np.abs(w[:, 1]) < 0.25
    if at0.sum() >= 2 and (~at0).sum() >= 1:
        base = w[at0].mean(axis=0)
        for v in w[~at0]:
            run = np.hypot(v[0] - base[0], v[2] - base[2])
            if run > 0.5:
                slopes.append(np.degrees(np.arctan2(v[1] - base[1], run)))
slopes = np.array(slopes)
print("land polygons touching sea level: %d; slope deg p10 %.1f median %.1f p90 %.1f; share flatter than 10 deg %.2f; share below 0 (going down) %.2f"
      % (len(land_tris), np.percentile(slopes, 10), np.median(slopes), np.percentile(slopes, 90), (np.abs(slopes) < 10).mean(), (slopes < -1).mean()))
