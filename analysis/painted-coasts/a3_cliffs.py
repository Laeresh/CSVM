import json
import sys
from collections import Counter, defaultdict

import numpy as np
from PIL import Image

# C3's cliff1_watertrans* polygons: which are flat at sea level (the ocean mask's candidates), which
# UV axis runs across the coast, each texture's colour profile along it, whether the flat polygons
# meet the base sheet and the land, and two camera poses over them.
#   python -I a3_cliffs.py <unpacked C3 gamez dir> <C3 texture png dir>
root, texdir = sys.argv[1], sys.argv[2]
STEM = "cliff1_watertrans"

with open(f"{root}/textures.json", encoding="utf-8") as f:
    names = [t["name"] for t in json.load(f)]
with open(f"{root}/materials.json", encoding="utf-8") as f:
    mats = json.load(f)
cliff_mats, base_mats, water_mats, tex_of = {}, set(), set(), {}
for i, m in enumerate(mats):
    body = next(iter(m.values()))
    if "texture_index" in body:
        n = names[body["texture_index"]].lower()
        tex_of[i] = names[body["texture_index"]]
        # gamez names are cut to 20 characters ("cliff1_watertrans.ti"); the digit after the stem
        # is the only part that tells the three apart.
        if n.startswith(STEM):
            cliff_mats[i] = STEM + (n[len(STEM)] if n[len(STEM)].isdigit() else "")
        if n.startswith("wtr"):
            base_mats.add(i)
        if n.startswith(("srf", "wtr", "water")) or "watersquirt" in n:
            water_mats.add(i)
print("cliff materials:", cliff_mats, "base-sheet materials:", sorted(base_mats))
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


def rotated(i):
    # The shore-water Euler order (ZYX) differs from the game's (GameZ.ParseTransform, YXZ), so the
    # transform is exact only where no node up the chain rotates.
    while True:
        body = next(iter(nodes[i]["data"].values()))
        t = body.get("transform") if isinstance(body, dict) else None
        if isinstance(t, dict) and "RotateTranslateScale" in t:
            r = t["RotateTranslateScale"]["rotate"]
            if abs(r["x"]) + abs(r["y"]) + abs(r["z"]) > 1e-6:
                return True
        if not nodes[i].get("parent_indices"):
            return False
        i = nodes[i]["parent_indices"][0]


cliff = defaultdict(list)  # texture -> [(world verts, uv)]
base_edges, land = [], []  # land: (world verts, texture) of non-water polygons touching sea level
later_pass = 0
rotated_nodes = set()
overlays = defaultdict(Counter)
for i, n in enumerate(nodes):
    mi = n["model_index"]
    if mi < 0 or not models[mi]["vertices"]:
        continue
    m = xf_of(i)
    verts = np.array([[v["x"], v["y"], v["z"], 1.0] for v in models[mi]["vertices"]])
    wv = (m @ verts.T).T[:, :3]
    for p in models[mi]["polygons"]:
        if not p["materials"]:
            continue
        later_pass += any(pm["material_index"] in cliff_mats for pm in p["materials"][1:])
        mat = p["materials"][0]["material_index"]
        w = wv[p["vertex_indices"]]
        if mat in cliff_mats:
            cliff[cliff_mats[mat]].append((w, np.array([[c["u"], c["v"]] for c in p["materials"][0]["uv_coords"]])))
            # Every later pass, as a2_tiles.py reports it: C1's tiles carry an alpha overlay there.
            over = [tex_of.get(q["material_index"], "?") for q in p["materials"][1:]]
            overlays[cliff_mats[mat]][("overlay " + "+".join(over) if over else "no overlay", "flat" if (np.abs(w[:, 1]) < 0.5).all() else "not flat")] += 1
            if rotated(i):
                rotated_nodes.add(i)
        elif mat in base_mats:
            if w[:, 1].min() > -0.5 and w[:, 1].max() < 0.5:
                base_edges.extend((w[k][[0, 2]], w[(k + 1) % len(w)][[0, 2]]) for k in range(len(w)))
        elif mat not in water_mats and w[:, 1].min() < 0.5 and w[:, 1].max() > -0.5:
            land.append((w, tex_of.get(mat, "untextured")))

print("polygons carrying a cliff1_watertrans material in a later pass:", later_pass)
for t in sorted(overlays):
    print("material passes on %s polygons: %s" % (t, dict(overlays[t])))
print("material passes per polygon, whole chapter:", dict(Counter(len(p["materials"]) for mo in models for p in mo["polygons"])))
print("nodes carrying cliff1_watertrans polygons with a rotation up their chain:", len(rotated_nodes))
base_a = np.array([e[0] for e in base_edges])
base_b = np.array([e[1] for e in base_edges])
land_pts, land_tex, land_top = [], [], []
for w, t in land:
    for v in w[np.abs(w[:, 1]) < 0.5]:
        land_pts.append(v[[0, 2]])
        land_tex.append(t)
        land_top.append(w[:, 1].max())
land_pts, land_top = np.array(land_pts), np.array(land_top)


def seg_dist(p, a, b):
    ab = b - a
    t = np.clip(((p - a) * ab).sum(axis=1) / np.maximum((ab ** 2).sum(axis=1), 1e-12), 0, 1)
    return np.sqrt(((a + ab * t[:, None] - p) ** 2).sum(axis=1)).min()


def fan(n):
    # Godot's triangulation of an n-gon in the scene builder: a fan from the first vertex.
    return [[0, k, k + 1] for k in range(1, n - 1)]


def flat(w):
    # OceanMask.Read's sea-level test (OceanMask.cs:520).
    return w[:, 1].min() > -0.5 and w[:, 1].max() < 0.5


def pct(a, q):
    return float(np.percentile(a, q))


def area(w):
    xz = w[:, [0, 2]]
    return 0.5 * abs(np.dot(xz[:, 0], np.roll(xz[:, 1], -1)) - np.dot(xz[:, 1], np.roll(xz[:, 0], -1)))


# --- (a) the split, (c) the edges, the coast axis and its scale ---------------------------------
flat_all = []
m_per_v = {}
for tex in sorted(cliff):
    polys = cliff[tex]
    tris = [(w[t], flat(w[t])) for w, _ in polys for t in fan(len(w))]
    fl = [(w, uv) for w, uv in polys if flat(w)]
    hi = [w for w, _ in polys if not flat(w)]
    print("\n== %s: %d polygons, %d flat, %d not; triangles %d, flat %d" % (tex, len(polys), len(fl), len(hi), len(tris), sum(f for _, f in tris)))
    for w in hi:
        print("  not flat: y %.2f..%.2f, %d vertices, centre x %.0f z %.0f" % (w[:, 1].min(), w[:, 1].max(), len(w), w[:, 0].mean(), w[:, 2].mean()))
    print("  flat polygons' y %.4f..%.4f, area %.0f m2" % (min(w[:, 1].min() for w, _ in fl), max(w[:, 1].max() for w, _ in fl), sum(area(w) for w, _ in fl)))
    # The coast axis: the UV coordinate at vertices shared with a land polygon against vertices
    # on the base sheet's edges only.
    at_land, at_base, met, tops = [], [], Counter(), []
    v0_land = v0_n = v1_base = v1_n = 0
    for w, uv in fl:
        for k in range(len(w)):
            p = w[k, [0, 2]]
            dl = ((land_pts - p) ** 2).sum(axis=1)
            j = int(np.argmin(dl))
            on_land = dl[j] < 0.25
            on_base = seg_dist(p, base_a, base_b) < 0.5
            if on_land:
                at_land.append(uv[k])
                met[land_tex[j]] += 1
                tops.append(land_top[(dl < 0.25)].max())
            elif on_base:
                at_base.append(uv[k])
            if uv[k, 1] < 0.01:
                v0_n += 1
                v0_land += on_land
            if uv[k, 1] > 0.99:
                v1_n += 1
                v1_base += on_base
    at_land, at_base = np.array(at_land), np.array(at_base)
    print("  vertices on a land vertex: %d, u mean %.2f, v mean %.2f, v = 0 on %d" % (len(at_land), *at_land.mean(axis=0), (at_land[:, 1] < 0.01).sum()))
    print("  vertices on a base-sheet edge only: %d, u mean %.2f, v mean %.2f, v = 1 on %d" % (len(at_base), *at_base.mean(axis=0), (at_base[:, 1] > 0.99).sum()))
    print("  v = 0 vertices on a land vertex: %d of %d; v = 1 vertices on a base-sheet edge: %d of %d" % (v0_land, v0_n, v1_base, v1_n))
    print("  land textures met: %s; tallest land polygon at those vertices: median %.1f max %.1f m" % (met.most_common(6), np.median(tops), max(tops)))
    # Scale: metres per unit v from the affine fit XZ -> uv, and the v = 0 to v = 1 width.
    mv, widths = [], []
    for w, uv in fl:
        xz = w[:, [0, 2]]
        g, *_ = np.linalg.lstsq(np.c_[xz, np.ones(len(xz))], uv, rcond=None)
        gv = np.hypot(g[0, 1], g[1, 1])
        if gv > 1e-9:
            mv.append(1 / gv)
        lo, hi_ = xz[uv[:, 1] < 0.01], xz[uv[:, 1] > 0.99]
        if len(lo) and len(hi_):
            # Across the strip: from each v = 1 vertex to the v = 0 edge.
            widths.extend(seg_dist(q, lo[:-1], lo[1:]) if len(lo) > 1 else np.linalg.norm(q - lo[0]) for q in hi_)
    m_per_v[tex] = float(np.median(mv))
    print("  metres per unit v: min %.1f p10 %.1f median %.1f p90 %.1f max %.1f" % (min(mv), pct(mv, 10), np.median(mv), pct(mv, 90), max(mv)))
    print("  v = 1 vertex to the v = 0 edge, m: min %.1f p10 %.1f median %.1f p90 %.1f max %.1f" % (min(widths), pct(widths, 10), np.median(widths), pct(widths, 90), max(widths)))
    flat_all.extend((tex, w, uv) for w, uv in fl)

# --- (b) the colour profile along v ---------------------------------------------------------------
sheet = np.asarray(Image.open(f"{texdir}/wtr00000.png").convert("RGB"), dtype=float).reshape(-1, 3)
W = sheet.mean(axis=0)
sd = np.sqrt(((sheet - W) ** 2).sum(axis=1))
print("\nwtr00000 mean texel RGB %.1f %.1f %.1f; its own texels' distance from that mean: median %.1f p90 %.1f max %.1f"
      % (*W, np.median(sd), pct(sd, 90), sd.max()))


def crossing(d, own_ends=False):
    # d is the distance from the sheet's mean texel, index 0 at the water end. The land end is an
    # 8-row mean. Rule A (own_ends False) takes the sheet's mean as the water, so the fractions run
    # from 0 to the land end; rule B runs from the profile's own 8-row water end. The crossing at f
    # is the first row from the water end whose 5-row mean reaches the f point.
    s = np.convolve(np.r_[np.full(2, d[0]), d, np.full(2, d[-1])], np.ones(5) / 5, mode="valid")
    dw, dl = (d[:8].mean() if own_ends else 0.0), d[-8:].mean()
    out = {}
    for f in (0.1, 0.5, 0.9):
        hit = np.nonzero(s - dw >= f * (dl - dw))[0]
        out[f] = hit[0] if len(hit) else None
    return dw, dl, out


for tex in sorted(cliff):
    img = np.asarray(Image.open(f"{texdir}/{tex}.png"), dtype=float)
    h, wdt = img.shape[:2]
    print("\n== %s.png %dx%d, %d channels" % (tex, wdt, h, img.shape[2]))
    rgb = img[..., :3]
    # Row r holds v = (r + 0.5) / h (Godot's UV origin is the image's top-left); v = 1 is water.
    prof = rgb.mean(axis=1)[::-1]  # index 0 = the water end, v = 1
    d = np.sqrt(((prof - W) ** 2).sum(axis=1))
    v_of = lambda k: 1 - (k + 0.5) / h if k is not None else float("nan")
    print("  distance by v, every 8th row from the water end:", " ".join("%.2f:%.0f" % (v_of(k), d[k]) for k in range(0, h, 8)))
    for rule, own in (("A, sheet mean to land end", False), ("B, profile's own ends", True)):
        dw, dl, c = crossing(d, own)
        fade = v_of(c[0.1]) - v_of(c[0.9])
        print("  rule %s: water %.1f land %.1f threshold %.1f; 10%% at v %.3f, crossing at v %.3f, 90%% at v %.3f; fade 10-90%% %.3f in v = %.0f m at the median %.0f m per unit v"
              % (rule, dw, dl, (dw + dl) / 2, v_of(c[0.1]), v_of(c[0.5]), v_of(c[0.9]), fade, fade * m_per_v[tex], m_per_v[tex]))
    # Per column, rule A: how far the painted waterline wanders across u.
    cols = []
    for x in range(wdt):
        _, _, cc = crossing(np.sqrt(((rgb[::-1, x] - W) ** 2).sum(axis=1)))
        cols.append(v_of(cc[0.5]))
    cols = np.array(cols)
    print("  per-column rule-A crossing v: p10 %.3f median %.3f p90 %.3f; median by u in eighths: %s"
          % (np.nanpercentile(cols, 10), np.nanmedian(cols), np.nanpercentile(cols, 90),
             " ".join("%.2f" % np.nanmedian(cols[k:k + wdt // 8]) for k in range(0, wdt, wdt // 8))))
    # Per texel: within rule A's threshold of the sheet's mean. The water's footprint by quarter.
    water = np.sqrt(((rgb - W) ** 2).sum(axis=2)) < crossing(d)[1] / 2
    q = water.reshape(4, h // 4, 4, wdt // 4).mean(axis=(1, 3))
    print("  water-coloured texel share %.2f; per quarter (rows v 0 to 1 top to bottom, columns u 0 to 1):" % water.mean())
    for r in q:
        print("    " + " ".join("%.2f" % x for x in r))
    share = water.mean(axis=1)[::-1]
    k = np.nonzero(np.convolve(np.r_[np.full(2, share[0]), share, np.full(2, share[-1])], np.ones(5) / 5, mode="valid") < 0.5)[0]
    print("  row water share falls below one half at v %.3f" % v_of(k[0] if len(k) else None))

# --- (d) poses ------------------------------------------------------------------------------------
# The c3-island golden aims --direction=-0.35,-0.35,-1 from (-4518, 400, -2015); with identity
# axes its ray meets y = 0 at (-4918, -3158). Coast there confirms the convention.
for label, z in (("as is", -3158.0), ("z negated", 3158.0)):
    p = np.array([-4918.0, z])
    print("\nc3-island aim (%s) x -4918 z %.0f: nearest sea-level land vertex %.0f m, nearest base-sheet edge %.0f m"
          % (label, z, np.sqrt(((land_pts - p) ** 2).sum(axis=1)).min(), seg_dist(p, base_a, base_b)))

cells = defaultdict(list)
for tex, w, uv in flat_all:
    c = w[:, [0, 2]].mean(axis=0)
    cells[(tex,) + tuple(int(k) for k in c // 300)].append((tex, w, uv))
ranked = sorted(cells.items(), key=lambda kv: -sum(area(w) for _, w, _ in kv[1]))
# The three largest 300 m cells of each texture.
for key, group in [kv for t in sorted(cliff) for kv in [r for r in ranked if r[0][0] == t][:3]]:
    pts = np.concatenate([w[:, [0, 2]] for _, w, _ in group])
    ar = np.array([area(w) for _, w, _ in group])
    cen = (np.array([w[:, [0, 2]].mean(axis=0) for _, w, _ in group]) * ar[:, None]).sum(axis=0) / ar.sum()
    # Seaward: the mean direction of increasing v.
    sea = np.zeros(2)
    for _, w, uv in group:
        g, *_ = np.linalg.lstsq(np.c_[w[:, [0, 2]], np.ones(len(w))], uv, rcond=None)
        sea += g[:2, 1] / max(np.hypot(*g[:2, 1]), 1e-9)
    sea /= np.linalg.norm(sea)
    # Over the sea, 250 m out and 180 m up, looking back at the centroid: about 45 degrees down.
    cam = cen + sea * 250
    near = np.array([w[:, 1].max() for w, _ in land if np.hypot(*(w[:, [0, 2]].mean(axis=0) - cam)) < 150] or [0.0])
    print("cell %s: %d polygons %s, %.0f m2, extent x %.0f..%.0f z %.0f..%.0f, centroid %.0f %.0f, seaward %.2f %.2f; tallest land within 150 m of the camera %.0f m"
          % (key, len(group), dict(Counter(t for t, _, _ in group)), ar.sum(), pts[:, 0].min(), pts[:, 0].max(), pts[:, 1].min(), pts[:, 1].max(), *cen, *sea, near.max()))
    print("   pose '--pos=%.0f,180,%.0f' '--lookat=%.0f,0,%.0f'" % (cam[0], cam[1], cen[0], cen[1]))
