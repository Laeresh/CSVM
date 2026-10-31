import json
import sys

import numpy as np

root = sys.argv[1]
with open(f"{root}/textures.json", encoding="utf-8") as f:
    names = [t["name"] for t in json.load(f)]
with open(f"{root}/materials.json", encoding="utf-8") as f:
    mats = json.load(f)
srf_mats, water_mats = set(), set()
for i, m in enumerate(mats):
    body = next(iter(m.values()))
    if "texture_index" in body:
        n = names[body["texture_index"]].lower()
        if n.startswith("srf"):
            srf_mats.add(i)
        if n.startswith(("srf", "wtr", "water")):
            water_mats.add(i)
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


ring_outer = []
below = []  # land triangles with any vertex below -0.3 m
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
        w = wv[idx]
        if mat in srf_mats:
            uv = np.array([uvc["u"] for uvc in p["materials"][0]["uv_coords"]])
            ring_outer.extend(w[uv > 0.5][:, [0, 2]])
        elif mat not in water_mats and w[:, 1].min() < -0.3:
            below.append((w[:, 0].min(), w[:, 0].max(), w[:, 2].min(), w[:, 2].max(), w[:, 1].min(), n["name"]))

ring_outer = np.array(ring_outer)
print("ring outer-edge vertices:", len(ring_outer), "land triangles with a vertex below -0.3 m:", len(below))
bb = np.array([b[:5] for b in below])
hits = 0
depths = []
names_hit = {}
for p in ring_outer[:: max(1, len(ring_outer) // 300)]:
    sel = (bb[:, 0] <= p[0]) & (bb[:, 1] >= p[0]) & (bb[:, 2] <= p[1]) & (bb[:, 3] >= p[1])
    if sel.any():
        hits += 1
        depths.append(bb[sel, 4].min())
        for k in np.nonzero(sel)[0][:3]:
            names_hit[below[k][5]] = names_hit.get(below[k][5], 0) + 1
n = len(ring_outer[:: max(1, len(ring_outer) // 300)])
print("sampled outer-edge points %d, with a below-sea triangle's XZ box over them: %d" % (n, hits))
if depths:
    print("depth of that geometry: median %.1f m, shallowest %.1f m" % (np.median(depths), max(depths)))
    print("nodes:", sorted(names_hit.items(), key=lambda kv: -kv[1])[:8])
