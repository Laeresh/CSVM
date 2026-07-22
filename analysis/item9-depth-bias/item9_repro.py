"""Q2/Q3 on TRIANGLES: what is actually coplanar-and-overlapping in the C5 repro
footprint, and does any grouping rule order it the way the reference demands?

Repro pose (from the plan):
  --viewer --chapter=C5 --sky-zone=zone2
  --campos=-9533.178,76.319,-3367.413 --lookat=-9451.281,28.148,-3398.597
"""
import sys, os, math
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from item9_conflicts import quant, NORM_TOL, D_TOL, MIN_OVERLAP

NINE = [1777, 1799, 1800, 1801, 1813, 1814, 1822, 1823, 1837]
CAM = (-9533.178, 76.319, -3367.413)
LOOK = (-9451.281, 28.148, -3398.597)
FOV_R = 1200.0   # generous radius around the camera; the view is ground level looking down

ch = Chapter("C5")
bn = dict(ch.built_nodes())

# ---------------------------------------------------------------- 1. the nine, on triangles
print("=== A. the nine World-child nodes: do they overlap EACH OTHER at all? ===")
buck = defaultdict(list)
for idx in NINE:
    for pri, matidx, tri in triangles_world(ch, idx, bn[idx]):
        pl = plane_of(tri)
        if pl is None:
            continue
        buck[(quant(*pl), pri)].append((idx, tri, pl[0], matidx))
pairs = defaultdict(float)
shared_planes = 0
for key, items in buck.items():
    if len({i[0] for i in items}) < 2:
        continue
    shared_planes += 1
    u, v = project_basis(items[0][2])
    t2 = [(i[0], ensure_ccw(to2d(i[1], u, v))) for i in items]
    for a in range(len(t2)):
        for b in range(a + 1, len(t2)):
            if t2[a][0] == t2[b][0]:
                continue
            ov = clip_area(t2[a][1], t2[b][1])
            if ov > MIN_OVERLAP:
                pairs[(min(t2[a][0], t2[b][0]), max(t2[a][0], t2[b][0]), key[1])] += ov
print(f"  planes shared by >=2 of the nine (same priority): {shared_planes}")
print(f"  node pairs with REAL area overlap: {len(pairs)}")
for k, a in sorted(pairs.items(), key=lambda kv: -kv[1]):
    print(f"    {k[0]}({ch.nodes[k[0]]['name']}) x {k[1]}({ch.nodes[k[1]]['name']}) "
          f"pri {k[2]}: {a:,.0f} m^2")
if not pairs:
    print("    -> the nine TILE. They abut; they do not overlap. The 'nine coplanar")
    print("       World-child nodes stack' reading is an AABB artifact.")

# ------------------------------------------- 2. everything drawn near the repro camera
print()
print("=== B. every conflicting node pair whose overlap lies inside the repro footprint ===")
near = []
for idx, xf in bn.items():
    if ch.nodes[idx]["name"].startswith("fvol"):
        continue
    for pri, matidx, tri in triangles_world(ch, idx, xf):
        c = ((tri[0][0] + tri[1][0] + tri[2][0]) / 3.0,
             (tri[0][1] + tri[1][1] + tri[2][1]) / 3.0,
             (tri[0][2] + tri[1][2] + tri[2][2]) / 3.0)
        if (c[0] - CAM[0]) ** 2 + (c[2] - CAM[2]) ** 2 > FOV_R ** 2:
            continue
        pl = plane_of(tri)
        if pl is None:
            continue
        near.append((idx, pri, pl, tri, matidx))
print(f"  triangles within {FOV_R:.0f} m of the camera: {len(near):,} "
      f"from {len({n[0] for n in near})} nodes")

nb = defaultdict(list)
for idx, pri, pl, tri, matidx in near:
    nb[(quant(*pl), pri)].append((idx, tri, pl[0], matidx))
conf = defaultdict(float)
conf_tex = defaultdict(set)
for key, items in nb.items():
    if len({i[0] for i in items}) < 2:
        continue
    u, v = project_basis(items[0][2])
    recs = []
    for (idx, tri, _n, matidx) in items:
        t2 = ensure_ccw(to2d(tri, u, v))
        xs = [p[0] for p in t2]; ys = [p[1] for p in t2]
        recs.append((idx, t2, min(xs), min(ys), max(xs), max(ys), matidx))
    recs.sort(key=lambda r: r[2])
    for i in range(len(recs)):
        ai, a2, ax0, ay0, ax1, ay1, am = recs[i]
        for j in range(i + 1, len(recs)):
            bi, b2, bx0, by0, bx1, by1, bm = recs[j]
            if bx0 >= ax1:
                break
            if ai == bi or ay1 <= by0 or by1 <= ay0:
                continue
            ov = clip_area(a2, b2)
            if ov > MIN_OVERLAP:
                k = (min(ai, bi), max(ai, bi), key[1], key[0])
                conf[k] += ov
                conf_tex[k].add((ch.tex_name(am), ch.tex_name(bm)))
print(f"  conflicting node pairs in the footprint: {len(conf)}")
for k, a in sorted(conf.items(), key=lambda kv: -kv[1])[:25]:
    lo, hi, pri, pk = k
    nrm = (pk[0] * NORM_TOL, pk[1] * NORM_TOL, pk[2] * NORM_TOL)
    delta = hi - lo
    print(f"    {lo}({ch.nodes[lo]['name']}) x {hi}({ch.nodes[hi]['name']})  pri {pri}  "
          f"n=({nrm[0]:.2f},{nrm[1]:.2f},{nrm[2]:.2f}) d={pk[3]*D_TOL:.1f}  {a:,.0f} m^2")
    print(f"        index delta {delta} -> node_bias {delta*NODE_ORDER_BIAS:.2e} "
          f"({delta*NODE_ORDER_BIAS/DEPTH_FLOOR:.2f}x floor) "
          f"{'SEPARATES' if delta*NODE_ORDER_BIAS >= DEPTH_FLOOR else 'CANNOT SEPARATE'}"
          f"   tex {sorted(conf_tex[k])[:2]}")

# ------------------------------------------- 3. what is actually drawn on the ground here
print()
print("=== C. ground coverage at the repro: which nodes put a triangle at y = 5 here? ===")
gr = defaultdict(float)
for idx, pri, pl, tri, matidx in near:
    n, d = pl
    if abs(n[1]) < 0.999 or abs(abs(d) - 5.0) > 0.01:
        continue
    u, v = project_basis(n)
    gr[(idx, pri, ch.tex_name(matidx))] += tri_area_xz(tri) if False else abs(
        (tri[1][0]-tri[0][0])*(tri[2][2]-tri[0][2]) - (tri[2][0]-tri[0][0])*(tri[1][2]-tri[0][2])) / 2
for k, a in sorted(gr.items(), key=lambda kv: -kv[1])[:20]:
    idx, pri, tex = k
    par = ch.nodes[idx].get("parent_indices")
    isworld = idx in set(ch.world_children)
    ispart = idx in set(ch.partition_roots)
    print(f"    {idx:5d} {ch.nodes[idx]['name']:12s} pri {pri:4d} {str(tex):15s} "
          f"{a:12,.0f} m^2  parent {par} "
          f"{'[world child]' if isworld else ''}{'[partition root]' if ispart else ''}")
