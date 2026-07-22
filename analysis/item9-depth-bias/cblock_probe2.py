"""Q2 (load-bearing): do the cblock variants genuinely occupy the same ground, or tile?

For every built node, bucket triangles by (exact plane, priority) and measure TRUE
triangle-intersection area between every pair of cblock textures (Sutherland-Hodgman via
item9_lib.clip_area). Also count EXACT duplicate triangles (same three world vertices,
any winding) between two different cblock materials -- the strongest possible signal that
one is a coincident copy of the other rather than a neighbouring tile.
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NORM_TOL, D_TOL, MIN_OVERLAP = 1e-4, 1e-3, 1.0
Q = 1e-3   # vertex quantisation for the exact-duplicate test (1 mm)


def quant(n, d):
    return (round(n[0]/NORM_TOL), round(n[1]/NORM_TOL), round(n[2]/NORM_TOL), round(d/D_TOL))


def tkey(tri):
    return tuple(sorted(tuple(round(c/Q) for c in v) for v in tri))


def run(chname):
    ch = Chapter(chname)
    pair_ov = defaultdict(float)      # (texA,texB) -> overlapping area
    pair_dup = defaultdict(float)     # (texA,texB) -> area of EXACT duplicate triangles
    pair_nodes = defaultdict(set)
    dup_nodes = defaultdict(set)
    samepri = defaultdict(lambda: defaultdict(float))   # (a,b) -> (priA,priB) -> area

    def tri_area(t):
        a, b, c = t
        u = (b[0]-a[0], b[1]-a[1], b[2]-a[2]); v = (c[0]-a[0], c[1]-a[1], c[2]-a[2])
        cx, cy, cz = u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]
        return 0.5*(cx*cx+cy*cy+cz*cz)**0.5

    for idx, xf in ch.built_nodes():
        tris = []
        for pri, m, tri in triangles_world(ch, idx, xf):
            t = ch.tex_name(m)
            if t and 'cblock' in t:
                tris.append((pri, t.replace('.tif', ''), tri))
        if len(tris) < 2:
            continue
        # exact-duplicate test (ignores plane bucketing entirely)
        bykey = defaultdict(list)
        for pri, t, tri in tris:
            bykey[tkey(tri)].append((pri, t, tri))
        for k, group in bykey.items():
            texs = {g[1] for g in group}
            if len(texs) > 1:
                for a in sorted(texs):
                    for b in sorted(texs):
                        if a < b:
                            pair_dup[(a, b)] += tri_area(group[0][2])
                            dup_nodes[(a, b)].add(idx)
                            pa = {g[0] for g in group if g[1] == a}
                            pb = {g[0] for g in group if g[1] == b}
                            samepri[(a, b)][(tuple(sorted(pa)), tuple(sorted(pb)))] += tri_area(group[0][2])
        # true-overlap test, per exact plane (priority ignored: we want geometric coincidence)
        buck = defaultdict(list)
        for pri, t, tri in tris:
            pl = plane_of(tri)
            if pl is None:
                continue
            buck[quant(*pl)].append((t, tri, pl[0]))
        for key, items in buck.items():
            if len(items) < 2:
                continue
            u, v = project_basis(items[0][2])
            recs = []
            for (t, tri, _n) in items:
                t2 = ensure_ccw(to2d(tri, u, v))
                xs = [p[0] for p in t2]; ys = [p[1] for p in t2]
                recs.append((t, t2, min(xs), min(ys), max(xs), max(ys)))
            recs.sort(key=lambda r: r[2])
            for i in range(len(recs)):
                at, a2, ax0, ay0, ax1, ay1 = recs[i]
                for j in range(i+1, len(recs)):
                    bt, b2, bx0, by0, bx1, by1 = recs[j]
                    if bx0 >= ax1:
                        break
                    if at == bt or ay1 <= by0 or by1 <= ay0:
                        continue
                    ov = clip_area(a2, b2)
                    if ov <= MIN_OVERLAP:
                        continue
                    k = tuple(sorted((at, bt)))
                    pair_ov[k] += ov
                    pair_nodes[k].add(idx)

    print(f"== {chname}: cblock x cblock, same node ==")
    print("  TRUE overlapping area between different cblock materials (coplanar, any priority)")
    for k in sorted(pair_ov, key=lambda k: -pair_ov[k]):
        print(f"    {k[0]:<9} x {k[1]:<9} {pair_ov[k]:>14,.0f} m2   over {len(pair_nodes[k])} nodes")
    print("  EXACT DUPLICATE triangles (identical world vertices) between different materials")
    for k in sorted(pair_dup, key=lambda k: -pair_dup[k]):
        pris = "; ".join(f"pri{a}/{b}:{v:,.0f}" for (a, b), v in
                         sorted(samepri[k].items(), key=lambda kv: -kv[1])[:4])
        print(f"    {k[0]:<9} x {k[1]:<9} {pair_dup[k]:>14,.0f} m2   over {len(dup_nodes[k])} nodes   {pris}")


if __name__ == "__main__":
    for c in (sys.argv[1:] or ["C5"]):
        run(c)
