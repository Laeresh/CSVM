"""The C5 repro footprint, exhaustively: EVERY coplanar same-priority overlapping triangle
pair near the recorded camera, classified by what separates the two (nothing / surface rank
/ node bias / priority), with the actual bias delta each one gets today.

This is the question the previous three diagnoses never asked in this form.
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from item9_withinmesh import quant, surface_ranks, NORM_TOL, D_TOL, CAP

MIN_OVERLAP = 1.0
CAM = (-9533.178, 76.319, -3367.413)
LOOK = (-9451.281, 28.148, -3398.597)
R = float(sys.argv[1]) if len(sys.argv) > 1 else 1200.0
CH = sys.argv[2] if len(sys.argv) > 2 else "C5"
if CH == "C1B":
    CAM = (-7698.844, 48.763, -5797.924)
    LOOK = (-7749.957, -20.093, -5849.367)

ch = Chapter(CH)
bn = ch.built_nodes()

# collect every triangle near the camera, tagged with the bias it will actually receive
tris = []
for idx, xf in bn:
    if ch.nodes[idx]["name"].startswith("fvol"):
        continue
    mi = model_index(ch.nodes[idx])
    if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
        continue
    rk, _ = surface_ranks(ch.models[mi])
    for pri, matidx, tri in triangles_world(ch, idx, xf):
        c0 = (tri[0][0] + tri[1][0] + tri[2][0]) / 3.0
        c2 = (tri[0][2] + tri[1][2] + tri[2][2]) / 3.0
        if (c0 - CAM[0]) ** 2 + (c2 - CAM[2]) ** 2 > R * R:
            continue
        pl = plane_of(tri)
        if pl is None:
            continue
        rank = min(rk.get((matidx, pri), 0), CAP)
        node_index = ch.nodes[idx]["index"]      # NOTE: GameZNode.Index is the FLAT position
        bias = max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05) + rank * SURFACE_RANK_BIAS \
            + idx * NODE_ORDER_BIAS
        tris.append((idx, pri, matidx, rank, bias, pl, tri))

print(f"== {CH} repro, radius {R:.0f} m ==")
print(f"  triangles in footprint: {len(tris):,} from {len({t[0] for t in tris})} nodes")

buck = defaultdict(list)
for rec in tris:
    buck[(quant(*rec[5]), rec[1])].append(rec)

rows = []
for key, items in buck.items():
    if len(items) < 2:
        continue
    u, v = project_basis(items[0][5][0])
    recs = []
    for rec in items:
        t2 = ensure_ccw(to2d(rec[6], u, v))
        xs = [p[0] for p in t2]; ys = [p[1] for p in t2]
        recs.append((rec, t2, min(xs), min(ys), max(xs), max(ys)))
    recs.sort(key=lambda r: r[2])
    for i in range(len(recs)):
        ra, a2, ax0, ay0, ax1, ay1 = recs[i]
        for j in range(i + 1, len(recs)):
            rb, b2, bx0, by0, bx1, by1 = recs[j]
            if bx0 >= ax1:
                break
            if ay1 <= by0 or by1 <= ay0:
                continue
            ov = clip_area(a2, b2)
            if ov > MIN_OVERLAP:
                rows.append((ra, rb, ov, key))

print(f"  coplanar same-priority OVERLAPPING triangle pairs: {len(rows):,}")

agg = defaultdict(lambda: [0.0, 0])
for ra, rb, ov, key in rows:
    same_node = ra[0] == rb[0]
    same_surface = same_node and ra[2] == rb[2] and ra[1] == rb[1]
    dbias = abs(ra[4] - rb[4])
    if same_surface:
        cls = "A. same node, SAME surface        "
    elif same_node:
        cls = "B. same node, different surface   "
    else:
        cls = "C. different nodes                "
    sep = "separates (>= 1e-6)" if dbias >= DEPTH_FLOOR else \
          ("NO separation at all" if dbias == 0 else f"below floor ({dbias:.2e})")
    k = (cls, sep)
    agg[k][0] += ov
    agg[k][1] += 1

print()
print("  classification of every overlapping pair:")
for k in sorted(agg, key=lambda kk: -agg[kk][0]):
    area, cnt = agg[k]
    print(f"    {k[0]} | {k[1]:22s} | {cnt:6d} pairs | {area:14,.0f} m^2")

print()
print("  worst offenders (zero or sub-floor separation), by overlap area:")
det = defaultdict(float)
for ra, rb, ov, key in rows:
    dbias = abs(ra[4] - rb[4])
    if dbias >= DEPTH_FLOOR:
        continue
    nrm = (key[0][0]*NORM_TOL, key[0][1]*NORM_TOL, key[0][2]*NORM_TOL)
    det[(ra[0], rb[0], ra[1], ch.tex_name(ra[2]), ch.tex_name(rb[2]), ra[3], rb[3],
         round(nrm[1], 2), round(key[0][3]*D_TOL, 1), round(dbias, 12))] += ov
for k, a in sorted(det.items(), key=lambda kv: -kv[1])[:20]:
    n1, n2, pri, t1, t2, k1, k2, ny, d, db = k
    print(f"    {n1}({ch.nodes[n1]['name']}) x {n2}({ch.nodes[n2]['name']}) pri {pri} "
          f"n.y={ny} d={d}")
    print(f"        {t1} (rank {k1}) vs {t2} (rank {k2}); bias delta {db:.2e}; "
          f"{a:,.0f} m^2")
