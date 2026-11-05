# PLAN-painted-coasts A2: the painted coast tiles at y = 0, their UV-to-world scale, each texture's
# one-dimensional profile across the coast, its crossing and fade, and whether land and sheet meet
# the tile's edges. C1 reads water1_trans1/2 (opaque colour ramps); C2 reads cliff01_trans2 (an
# alpha edge drawn at priority 1). Usage:
#   python -I a2_tiles.py <unpacked gamez dir> <texture png dir> [profile plot .png]
import json
import sys
from collections import Counter, defaultdict

import numpy as np
from PIL import Image

root, texdir = sys.argv[1], sys.argv[2]
plot_path = sys.argv[3] if len(sys.argv) > 3 else None

with open(f"{root}/textures.json", encoding="utf-8") as f:
    names = [t["name"] for t in json.load(f)]
with open(f"{root}/materials.json", encoding="utf-8") as f:
    mats = json.load(f)
tex_of = {}
for i, m in enumerate(mats):
    body = next(iter(m.values()))
    if "texture_index" in body:
        tex_of[i] = names[body["texture_index"]].lower().rsplit(".", 1)[0]
with open(f"{root}/models.json", encoding="utf-8") as f:
    models = json.load(f)
with open(f"{root}/nodes.json", encoding="utf-8") as f:
    nodes = json.load(f)

C1 = "water1_trans1" in tex_of.values()
TILES = ("water1_trans1", "water1_trans2") if C1 else ("cliff01_trans2",)


def is_sheet(t):
    return t == "water1" or t.startswith("wtr")


# XF-START
def local_xf(node):
    # As GameZ.ParseTransform: "Initial" is the identity; RotateTranslateScale uses its stored matrix
    # ("original", rows are the rotation's columns) when present, else Ry * Rx * Rz; scale is ignored.
    body = next(iter(node["data"].values()))
    m = np.eye(4)
    t = body.get("transform") if isinstance(body, dict) else None
    if isinstance(t, dict) and "RotateTranslateScale" in t:
        rts = t["RotateTranslateScale"]
        r, tr = rts["rotate"], rts["translate"]
        o = rts.get("original")
        if isinstance(o, dict):
            m[:3, :3] = np.array([[o[f"r{c}{k}"] for k in range(3)] for c in range(3)]).T
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
# XF-END


def triangles(p):
    # A tri_strip polygon is a strip (with degenerate joins), anything else a fan.
    k = len(p["vertex_indices"])
    if p["flags"].get("tri_strip"):
        return [(j, j + 1, j + 2) for j in range(k - 2)]
    return [(0, j, j + 1) for j in range(1, k - 1)]


def cross2(a, b):
    return a[0] * b[1] - a[1] * b[0]


def image(name):
    return np.asarray(Image.open(f"{texdir}/{name}.png")).astype(float)


def pct(d):
    d = np.asarray(d)
    return "n %d, min %.1f, median %.1f, p90 %.1f, max %.1f" % (len(d), d.min(), np.median(d), np.percentile(d, 90), d.max())


# --- Geometry -------------------------------------------------------------------------------
census = {t: Counter() for t in TILES}
tile_tris = {t: [] for t in TILES}  # dicts: world xyz, base uv, overlay, node
sheet_tris = []
land = []  # (world xyz, base texture, node name, polygon id) for every non-water triangle
flat_other = defaultdict(list)  # candidate scan: flat non-sheet polygons at y = 0, by base texture
nontrivial_xf = 0
pid = 0
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
        pid += 1
        base = tex_of.get(p["materials"][0]["material_index"], "")
        idx = np.array(p["vertex_indices"])
        w_all = wv[idx]
        if base in TILES:
            if not np.allclose(m, np.eye(4)):
                nontrivial_xf += 1
            c = census[base]
            c["polygons"] += 1
            c["tri_strip" if p["flags"].get("tri_strip") else "fan"] += 1
            c["priority %d" % p.get("priority", 0)] += 1
            c["flat at y = 0" if np.abs(w_all[:, 1]).max() < 0.25 else "rising, max y %.1f" % w_all[:, 1].max()] += 1
            over = [tex_of.get(q["material_index"], "?") for q in p["materials"][1:]]
            c["overlay " + "+".join(over) if over else "no overlay"] += 1
            c["node " + n["name"]] += 1
            uv = np.array([[q["u"], q["v"]] for q in p["materials"][0]["uv_coords"]])
            ouv = np.array([[q["u"], q["v"]] for q in p["materials"][1]["uv_coords"]]) if over else None
            for tri in triangles(p):
                tri = list(tri)
                w = wv[idx[tri]]
                if len(set(idx[tri])) < 3 or abs(cross2(uv[tri[1]] - uv[tri[0]], uv[tri[2]] - uv[tri[0]])) < 1e-9:
                    c["degenerate strip joins"] += 1
                    continue
                if np.abs(w[:, 1]).max() >= 0.25:
                    c["rising triangles skipped"] += 1
                    continue
                tile_tris[base].append(dict(w=w, uv=uv[tri], over=over[0] if over else None,
                                            ouv=ouv[tri] if over else None, node=n["name"]))
        elif is_sheet(base):
            for tri in triangles(p):
                sheet_tris.append(wv[idx[list(tri)]])
        elif not base.startswith(("water", "srf")):
            if np.abs(w_all[:, 1]).max() < 0.25 and base:
                flat_other[base].append(w_all)
            for tri in triangles(p):
                land.append((wv[idx[list(tri)]], base, n["name"], pid))

print("chapter:", "C1" if C1 else "C2", " tile textures:", TILES)
print("tile polygons under a non-identity node transform:", nontrivial_xf)
for t in TILES:
    c = census[t]
    tt = tile_tris[t]
    area = sum(0.5 * abs(cross2(q["w"][1, [0, 2]] - q["w"][0, [0, 2]], q["w"][2, [0, 2]] - q["w"][0, [0, 2]])) for q in tt)
    ys = np.concatenate([q["w"][:, 1] for q in tt])
    print(f"\n{t}: {c['polygons']} polygons ({c['fan']} fans, {c['tri_strip']} strips), {len(tt)} flat triangles, "
          f"flat y {ys.min():.2e}..{ys.max():.2e}, area {area / 1e6:.3f} km2")
    print("  ", {k: v for k, v in c.items() if not k.startswith(("node ", "polygons", "fan", "tri_strip"))})
    print("   nodes:", len([k for k in c if k.startswith("node ")]), sorted(((k[5:], v) for k, v in c.items() if k.startswith("node ")), key=lambda kv: -kv[1])[:6])

# Sea-level land: each land triangle's points at y = 0 (vertices within 0.25 m, and edge crossings).
segs, seg_tex = [], []
for w, tname, _, _ in land:
    pts = [w[j, [0, 2]] for j in range(3) if abs(w[j, 1]) <= 0.25]
    for j in range(3):
        a, b = w[j], w[(j + 1) % 3]
        if (a[1] > 0.25 and b[1] < -0.25) or (a[1] < -0.25 and b[1] > 0.25):
            f = a[1] / (a[1] - b[1])
            pts.append((a + f * (b - a))[[0, 2]])
    for j in range(len(pts)):
        for k2 in range(j + 1, len(pts)):
            segs.append((pts[j], pts[k2]))
            seg_tex.append(tname)
segs = np.array(segs)
seg_tex = np.array(seg_tex)
sheet_segs = np.array([(w[j, [0, 2]], w[(j + 1) % 3, [0, 2]]) for w in sheet_tris for j in range(3)])
print(f"\nsea-level land segments {len(segs)}, sheet triangles {len(sheet_tris)}")


def seg_dist(pts, s):
    # Distance from each point to the nearest segment, and that segment's index.
    a, b = s[:, 0], s[:, 1]
    ab = b - a
    ll = np.maximum((ab ** 2).sum(1), 1e-12)
    out, arg = np.empty(len(pts)), np.empty(len(pts), int)
    for i, q in enumerate(pts):
        t = np.clip(((q - a) * ab).sum(1) / ll, 0, 1)
        d = np.hypot(*(a + t[:, None] * ab - q).T)
        arg[i] = d.argmin()
        out[i] = d[arg[i]]
    return out, arg


def inside_any(pts, tris):
    # Whether each XZ point lies inside one of the triangles (a coplanar layer under it).
    t = np.array([w[:, [0, 2]] for w in tris])
    out = np.zeros(len(pts), bool)
    for i, q in enumerate(pts):
        s = [cross2((t[:, (j + 1) % 3] - t[:, j]).T, (q - t[:, j]).T) for j in range(3)]
        s = np.array(s)
        out[i] = (((s >= -1e-6).all(0)) | ((s <= 1e-6).all(0))).any()
    return out


# --- Candidate scan: every flat texture at y = 0 that touches the sheet ----------------------------
print("\nFlat non-sheet textures at y = 0 with a vertex within 1 m of a sheet edge (srf* excluded):")
for t, ws in sorted(flat_other.items(), key=lambda kv: -len(kv[1])) + [(t, [q["w"] for q in tile_tris[t]]) for t in TILES]:
    pts = np.unique(np.round(np.concatenate([w[:, [0, 2]] for w in ws]), 1), axis=0)
    d, _ = seg_dist(pts[:: max(1, len(pts) // 300)], sheet_segs)
    if (d < 1.0).any():
        try:
            a = image(t)
            ch = a.shape[2] if a.ndim == 3 else 1
            al = "alpha col means %.0f..%.0f, row means %.0f..%.0f" % (a[..., 3].mean(0).min(), a[..., 3].mean(0).max(), a[..., 3].mean(1).min(), a[..., 3].mean(1).max()) if ch == 4 else "opaque"
            px = f"{a.shape[1]}x{a.shape[0]}, {ch} channels, {al}"
        except OSError:
            px = "no png"
        print(f"  {t}: {len(ws)} polygons, share of sampled vertices on a sheet edge {np.mean(d < 1.0):.2f}; {px}")

# --- Profile coordinate per tile triangle -----------------------------------------------------
def coast_coord(t, uv):
    # Per vertex: the coordinate along the texture's ramp and the profile it reads.
    if t == "water1_trans2":
        # The ramp runs along s = u + v with a seam at s = 1; p = s - half inside each half.
        cell = np.floor(uv.mean(0))
        s = (uv - cell).sum(1)
        half = int(s.mean() >= 1)
        return s - half, f"{t} {'lower (s>1)' if half else 'upper (s<1)'}"
    return uv[:, 0] - np.floor(uv[:, 0].mean()), t


geo = defaultdict(list)
for t in TILES:
    for q in tile_tris[t]:
        xz = q["w"][:, [0, 2]]
        A = np.c_[xz, np.ones(3)]
        g = np.linalg.solve(A, q["uv"])  # rows: d/dx, d/dz, offset
        gu, gv = g[:2, 0], g[:2, 1]
        c, prof = coast_coord(t, q["uv"])
        gc = np.linalg.solve(A, c)[:2]
        geo[t].append(dict(q, xz=xz, c=c, prof=prof, crosses=c.min() < -1e-3 or c.max() > 1 + 1e-3,
                           m_per_u=1 / np.hypot(*gu), m_per_v=1 / np.hypot(*gv),
                           uv_angle=np.degrees(np.arccos(min(1, abs(gu @ gv) / np.hypot(*gu) / np.hypot(*gv)))),
                           m_per_c=1 / np.hypot(*gc), gc=gc / np.hypot(*gc),
                           area=0.5 * abs(cross2(xz[1] - xz[0], xz[2] - xz[0]))))

m_per_c = {}
for t in TILES:
    gs = geo[t]
    print(f"\n{t} UV to world, per triangle (metres per unit):")
    print("   U:", pct([q["m_per_u"] for q in gs]))
    print("   V:", pct([q["m_per_v"] for q in gs]))
    print("   angle between the U and V axes in world, deg:", pct([q["uv_angle"] for q in gs]))
    for prof in sorted({q["prof"] for q in gs}):
        sel = [q for q in gs if q["prof"] == prof]
        mc = np.array([q["m_per_c"] for q in sel])
        m_per_c[prof] = float(np.median(mc))
        print(f"   {prof}: {len(sel)} triangles, metres per unit of the profile coordinate across the ramp: {pct(mc)}; "
              f"triangles reading past a seam {sum(q['crosses'] for q in sel)}")
        print("     coordinate span per triangle (min, max, overlay):",
              Counter((round(float(q['c'].min()), 3), round(float(q['c'].max()), 3), q["over"]) for q in sel).most_common(8))

# --- Which end meets land, which meets the sheet --------------------------------------------------
print("\nTile vertices to the nearest sea-level land segment and to the sheet's edges, by profile coordinate (m):")
end_tex = defaultdict(Counter)
land_share = {}
for t in TILES:
    rows = defaultdict(list)
    for q in geo[t]:
        for j in range(3):
            rows[(q["prof"], round(float(q["c"][j]), 3))].append(q["xz"][j])
    for (prof, c), pts in sorted(rows.items()):
        pts = np.unique(np.round(np.array(pts), 2), axis=0)
        if len(pts) < 4:
            continue
        dl, arg = seg_dist(pts, segs)
        ds, _ = seg_dist(pts, sheet_segs)
        end_tex[(prof, c)].update(seg_tex[arg[dl < 1.0]])
        land_share[(prof, c)] = np.mean(dl < 1)
        print(f"  {prof} c={c}: to land {pct(dl)}; to the sheet {pct(ds)}; share within 1 m: land {np.mean(dl < 1):.2f}, sheet {np.mean(ds < 1):.2f}")
print("\nLand textures within 1 m of tile vertices, by profile coordinate:")
for k, v in sorted(end_tex.items()):
    if v:
        print("  ", k, v.most_common(4))
for t in TILES:
    cent = np.array([q["xz"].mean(0) for q in geo[t]])
    print(f"{t}: triangle centroids over a coplanar sheet triangle: {inside_any(cent, sheet_tris).sum()} of {len(cent)}")

# Overlay passes: where their own V runs against the base coordinate.
for t in TILES:
    ov = [q for q in geo[t] if q["over"]]
    if ov:
        pairs = Counter((round(float(c), 3), round(float(o[1]), 3)) for q in ov for c, o in zip(q["c"], q["ouv"]))
        print(f"{t} overlays {Counter(q['over'] for q in ov)}: (base coordinate, overlay v) at vertices: {pairs.most_common(6)}")
        cv = np.array([(c, o[1]) for q in ov for c, o in zip(q["c"], q["ouv"])])
        b_, a_ = np.polyfit(cv[:, 1], cv[:, 0], 1)
        for oname in sorted({q["over"] for q in ov}):
            al = image(oname)[..., 3].mean(1) / 255  # per texel row, i.e. along the overlay's V
            vv = (np.arange(len(al)) + 0.5) / len(al)
            i_ = int(np.argmax(al < 0.5))
            print(f"   {oname} {image(oname).shape[1]}x{image(oname).shape[0]}: row-mean alpha {np.round(al[::8], 2).tolist()}; "
                  f"falls through 0.5 at v = {vv[i_]:.3f}, base coordinate {a_ + b_ * vv[i_]:.3f} (fit c = {a_:.3f} + {b_:.3f} v)")

# --- Land rising on or beside the tiles ------------------------------------------------------------
# A land polygon rising above y = 0.25 counts as attached when a sea-level vertex lies within 1 m of a tile
# vertex, and as standing in the water when its XZ centroid lies over a tile or a sheet triangle
# within 150 m of one.
tile_v = np.unique(np.round(np.concatenate([q["xz"] for t in TILES for q in geo[t]]), 2), axis=0)
tile_nodes = {q["node"] for t in TILES for q in geo[t]}
tile_ws = [q["w"] for t in TILES for q in geo[t]]
lo_, hi_ = tile_v.min(0) - 150, tile_v.max(0) + 150
near_sheet = [w for w in sheet_tris if np.hypot(*(tile_v - w[:, [0, 2]].mean(0)).T).min() < 150]
by_poly = {}
for w, tname, node, p_ in land:
    if w[:, 1].max() <= 0.25:
        continue
    xz = w[:, [0, 2]]
    if (xz.max(0) < lo_).any() or (xz.min(0) > hi_).any():
        continue
    attached = any(abs(w[j, 1]) < 0.25 and np.hypot(*(tile_v - xz[j]).T).min() < 1.0 for j in range(3))
    standing = bool(inside_any(xz.mean(0)[None], tile_ws + near_sheet)[0]) and abs(w[:, 1].min()) < 0.25
    if attached or standing:
        r = by_poly.setdefault(p_, dict(tex=tname, node=node, ymax=0.0, ymin=1e9, attached=False, standing=False))
        r["ymax"], r["ymin"] = max(r["ymax"], w[:, 1].max()), min(r["ymin"], w[:, 1].min())
        r["attached"] |= attached
        r["standing"] |= standing
for lab, key in (("attached to a tile vertex", "attached"), ("standing in the water (over a tile or the sheet)", "standing")):
    rs = [r for r in by_poly.values() if r[key]]
    if rs:
        print(f"\nrising land polygons {lab}: {len(rs)}; max y {pct([r['ymax'] for r in rs])}; min y {pct([r['ymin'] for r in rs])}")
        print("   textures:", Counter(r["tex"] for r in rs).most_common(6))
        print("   in a node that also carries tiles:", sum(r["node"] in tile_nodes for r in rs), "nodes:", Counter(r["node"] for r in rs).most_common(5))
    else:
        print(f"\nrising land polygons {lab}: none")

# --- Profiles -----------------------------------------------------------------------------------
sheet_tex = Counter(tex_of.get(p["materials"][0]["material_index"], "") for n in nodes if n["model_index"] >= 0
                    for p in models[n["model_index"]]["polygons"] if p["materials"]
                    and is_sheet(tex_of.get(p["materials"][0]["material_index"], ""))).most_common(1)[0][0]
W = image(sheet_tex)[..., :3].reshape(-1, 3).mean(0)
land_end = Counter()
for (prof, c), v in end_tex.items():
    land_end.update(v)
L_name = land_end.most_common(1)[0][0]
L = image(L_name)[..., :3].reshape(-1, 3).mean(0)
print(f"\nsheet {sheet_tex} mean texel RGB {np.round(W, 1)}; land texture at the tiles' land edge {L_name} mean texel RGB {np.round(L, 1)}; "
      f"distance {np.linalg.norm(L - W):.1f}")


def profiles_of(t):
    a = image(t)
    if t == "water1_trans2":
        h, w = a.shape[:2]
        yy, xx = np.mgrid[0:h, 0:w]
        k = xx + yy  # s = (k + 1) / w at texel centres; k = w - 1 is the seam itself
        out = {}
        for half, lab in ((0, "upper (s<1)"), (1, "lower (s>1)")):
            ks = range(0, w - 1) if half == 0 else range(w, 2 * w - 1)
            out[f"{t} {lab}"] = (np.array([(kk + 1) / w - half for kk in ks]), np.array([a[k == kk].mean(0) for kk in ks]))
        return out
    return {t: ((np.arange(a.shape[1]) + 0.5) / a.shape[1], a.mean(0))}


def smooth(x, n=5):
    xp = np.pad(x, (n // 2, n // 2), mode="edge")
    return np.convolve(xp, np.ones(n) / n, mode="valid")


tex_means = {}
for n_ in sorted(set(tex_of.values())):
    try:
        a_ = image(n_)
    except OSError:
        continue
    if a_.ndim == 3:
        tex_means[n_] = a_[..., :3].reshape(-1, 3).mean(0)

results = {}
for t in TILES:
    for prof, (c, P) in profiles_of(t).items():
        if P.shape[1] == 4:
            # An alpha edge: land-ness is the coverage; the crossing is alpha 0.5.
            t_raw = P[:, 3] / 255
        else:
            # An opaque ramp: land-ness is the colour's projection on the sheet-to-land axis.
            t_raw = (P[:, :3] - W) @ (L - W) / ((L - W) @ (L - W))
        ts = smooth(t_raw)
        # Oriented by geometry: the land end is the profile end whose tile vertices sit on land.
        water_at_start = land_share.get((prof, 0.0), 0) < land_share.get((prof, 1.0), 0)
        o = np.arange(len(c)) if water_at_start else np.arange(len(c))[::-1]
        cs, Po = c[o], P[o]
        we, le = Po[:4, :3].mean(0), Po[-4:, :3].mean(0)
        mpc = m_per_c.get(prof, float("nan"))
        print(f"\n{prof}: water end at c = {cs[0]:.3f}, land end at c = {cs[-1]:.3f}")
        print(f"   water end RGB {np.round(we, 1)}, distance to the sheet's mean {np.linalg.norm(we - W):.1f}; "
              f"land end RGB {np.round(le, 1)}, distance to {L_name}'s mean {np.linalg.norm(le - L):.1f}")
        print(f"   largest single-texel step in land-ness {np.abs(np.diff(t_raw)).max():.3f}")
        near = sorted((np.linalg.norm(m_ - le), n_) for n_, m_ in tex_means.items() if n_ not in TILES)[:4]
        print("   textures whose mean texel is nearest the land end:", [(n_, round(float(d_), 1)) for d_, n_ in near])
        # Two normalisations: against the sheet and land texture means (the plan's rule), and against
        # the profile's own two ends (A3's), whose 10 % point is where the colour leaves the painted water.
        own = (Po[:, :3] - we) @ (le - we) / ((le - we) @ (le - we)) if P.shape[1] == 3 else t_raw[o]
        for lab, tt in (("sheet-to-land", ts[o]), ("own ends", smooth(own))):
            if tt.max() < 0.5:
                print(f"   [{lab}] range {tt.min():.2f}..{tt.max():.2f}: never reaches 0.5, so this normalisation has no crossing")
                continue
            i50 = int(np.argmax(tt >= 0.5))
            x50 = cs[i50 - 1] + (0.5 - tt[i50 - 1]) / (tt[i50] - tt[i50 - 1]) * (cs[i50] - cs[i50 - 1])
            i10 = i50 - int(np.argmax(tt[:i50][::-1] < 0.1)) if (tt[:i50] < 0.1).any() else 0
            i90 = i50 + int(np.argmax(tt[i50:] > 0.9)) if (tt[i50:] > 0.9).any() else len(tt) - 1
            results.setdefault(prof, {})[lab] = dict(c=cs, t=tt, x50=x50, x10=cs[i10], x90=cs[i90])
            print(f"   [{lab}] range {tt.min():.2f}..{tt.max():.2f}; 0.1 at c = {cs[i10]:.3f} ({abs(cs[i10] - cs[0]) * mpc:.1f} m from the water end), "
                  f"0.5 at c = {x50:.3f} ({abs(x50 - cs[0]) * mpc:.1f} m), 0.9 at c = {cs[i90]:.3f} ({abs(cs[i90] - cs[0]) * mpc:.1f} m); "
                  f"fade 0.1 to 0.9 {abs(cs[i90] - cs[i10]):.3f} of the ramp = {abs(cs[i90] - cs[i10]) * mpc:.1f} m at {mpc:.1f} m per unit")
        results[prof]["c"] = cs
        if P.shape[1] == 4:
            # The alpha edge is ragged: each texel row along V crosses 0.5 at its own U.
            a = image(t)[..., 3] / 255
            per_row = [((np.argmax(r >= 0.5) if water_at_start else len(r) - 1 - np.argmax(r[::-1] >= 0.5)) + 0.5) / len(r) for r in a]
            print(f"   per-row crossings along V: {pct(per_row)} (U)")
        print("   profile every 16th texel (c, land-ness):", [(round(float(x), 3), round(float(y), 2)) for x, y in zip(cs[::16], tt[::16])])

# --- Poses ------------------------------------------------------------------------------------
# Cells of 384 m by tile area; the camera 150 m up, 170 m seaward of the tiles' area centroid (C2's
# 30 m strips: 100 m up, 110 m seaward),
# looking at it so the coast runs across the frame. A pose is kept only if no land vertex within
# 30 m of the sight line rises above it, and the next pose sits in another cell (in C1, over the
# other texture).
lvert = np.concatenate([w for w, *_ in land])
lvert = lvert[lvert[:, 1] > 1.0]
cells = defaultdict(list)
for t in TILES:
    for q in geo[t]:
        cells[tuple(np.floor(q["xz"].mean(0) / 384).astype(int))].append(q)
picked = []
for key, qs in sorted(cells.items(), key=lambda kv: -sum(q["area"] for q in kv[1])):
    tex = Counter(q["prof"].split()[0] for q in qs).most_common(1)[0][0]
    if picked and ((C1 and tex == picked[0][0]) or np.hypot(*(np.array(key) - picked[0][1])) < 3):
        continue
    a = np.array([q["area"] for q in qs])
    cen = (np.array([q["xz"].mean(0) for q in qs]) * a[:, None]).sum(0) / a.sum()
    # Landward: the land-ness gradient, signed by which end of the profile is land.
    n = np.sum([(1 if results[q["prof"]]["c"][-1] > results[q["prof"]]["c"][0] else -1) * q["gc"] * q["area"] for q in qs], 0)
    n /= np.hypot(*n)
    up, out = (150, 170) if C1 else (100, 110)
    cam = cen - out * n
    box = lvert[(np.abs(lvert[:, 0] - (cam[0] + cen[0]) / 2) < 150) & (np.abs(lvert[:, 2] - (cam[1] + cen[1]) / 2) < 150)]
    blocked = 0.0
    for f in np.linspace(0, 1, 41):
        pt = cam + f * (cen - cam)
        h = up * (1 - f)
        near = box[np.hypot(box[:, 0] - pt[0], box[:, 2] - pt[1]) < 30]
        if len(near) and near[:, 1].max() > h:
            blocked = max(blocked, near[:, 1].max() - h)
    if blocked:
        print(f"pose candidate over {tex} at ({cen[0]:.0f}, {cen[1]:.0f}) rejected: land {blocked:.0f} m above the sight line")
        continue
    picked.append((tex, np.array(key)))
    print(f"\npose over {tex} ({len(qs)} tile triangles, {a.sum():.0f} m2 in the cell): tiles' centroid ({cen[0]:.0f}, 0, {cen[1]:.0f}), "
          f"landward ({n[0]:.2f}, {n[1]:.2f}), sight line clear")
    print(f"   '--pos={cam[0]:.0f},{up},{cam[1]:.0f}' '--lookat={cen[0]:.0f},0,{cen[1]:.0f}'")
    if len(picked) == 2:
        break
if C1:
    # The c1-lake-enhanced golden's lookat, against the tiles, as written and with Z mirrored.
    look = np.array([-7868.0, -3449.0])
    allxz = np.concatenate([q["xz"] for t in TILES for q in geo[t]])
    for lab, pt in (("as written", look), ("Z mirrored", look * [1, -1])):
        print(f"c1-lake-enhanced lookat {lab}: nearest tile vertex {np.hypot(*(allxz - pt).T).min():.0f} m")

if plot_path:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, ax = plt.subplots(figsize=(8, 4.5))
    for prof, rr in results.items():
        for lab, ls in (("sheet-to-land", "-"), ("own ends", ":")):
            if lab not in rr:
                continue
            r = rr[lab]
            ax.plot(r["c"], r["t"], ls, label=f"{prof}, {lab}: 0.5 at {r['x50']:.3f}")
    ax.axhline(0.5, color="grey", lw=0.5)
    ax.set_xlabel("ramp coordinate (U; s - half for water1_trans2)")
    ax.set_ylabel("land-ness (0 sheet, 1 land)")
    ax.legend(fontsize=8)
    fig.tight_layout()
    fig.savefig(plot_path, dpi=100)
