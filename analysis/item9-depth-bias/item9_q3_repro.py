"""Q2/Q3: the C5 repro footprint in detail.

Answers:
  - are the nine World-child nodes at y=5 EXACTLY coplanar, or near-coplanar?
  - which of them actually overlap in area (vs merely abut, verification.md rule 9)?
  - what does the reference (fine city ground on top, no coarse quad) require of a rank?
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NINE = [1777, 1799, 1800, 1801, 1813, 1814, 1822, 1823, 1837]
# the recorded C5 repro camera
CAM = (-9533.178, 76.319, -3367.413)
LOOK = (-9451.281, 28.148, -3398.597)

ch = Chapter("C5")
bn = dict(ch.built_nodes())

print("=== the nine World-child nodes at the C5 repro ===")
rows = []
for idx in NINE:
    n = ch.nodes[idx]
    xf = bn.get(idx)
    mi = model_index(n)
    mdl = ch.models[mi]
    polys = list(polygons_world(ch, idx, xf))
    pris = sorted({p[0] for p in polys})
    mats = sorted({ch.tex_name(p[1]) for p in polys})
    ys = set()
    for pri, matidx, poly, raw in polys:
        for v in poly:
            ys.add(round(v[1], 6))
    xs = [v[0] for _, _, poly, _ in polys for v in poly]
    zs = [v[2] for _, _, poly, _ in polys for v in poly]
    print(f"  {idx:5d} {n['name']:10s} npoly {len(polys):4d} pri {pris} "
          f"y-levels {sorted(ys)[:6]}{'...' if len(ys) > 6 else ''}")
    print(f"        x [{min(xs):9.1f},{max(xs):9.1f}] z [{min(zs):9.1f},{max(zs):9.1f}] "
          f"tex {mats[:4]}")
    rows.append((idx, polys))

print()
print("=== exact-coplanarity test: every polygon whose plane is y = 5 ===")
flat = defaultdict(list)   # (priority) -> [(node, poly, normal, d)]
for idx, polys in rows:
    for pri, matidx, poly, raw in polys:
        pl = plane_of(poly)
        if pl is None:
            continue
        n, d = pl
        if abs(n[1]) > 0.999999 and abs(abs(d) - 5.0) < 0.01:
            flat[pri].append((idx, poly, n, d, ch.tex_name(matidx)))
for pri, items in sorted(flat.items()):
    ds = sorted({round(abs(it[3]), 9) for it in items})
    nys = sorted({round(abs(it[2][1]), 12) for it in items})
    print(f"  priority {pri}: {len(items)} polys from "
          f"{len({it[0] for it in items})} nodes; distinct |d| {ds}; distinct |n.y| {nys}")

print()
print("=== TRUE area overlap between the nine, on the y = 5 plane, same priority ===")
for pri, items in sorted(flat.items()):
    u, v = project_basis((0.0, 1.0, 0.0))
    recs = [(idx, to2d(poly, u, v), tex) for idx, poly, n, d, tex in items]
    pair_area = defaultdict(float)
    abut = defaultdict(int)
    for i in range(len(recs)):
        ai, a2, at = recs[i]
        for j in range(i + 1, len(recs)):
            bi, b2, bt = recs[j]
            if ai == bi:
                continue
            ov = overlap_area(a2, b2)
            key = (min(ai, bi), max(ai, bi))
            if ov > 1.0:
                pair_area[key] += ov
            else:
                abut[key] += 1
    print(f"  priority {pri}: {len(pair_area)} overlapping node pairs, "
          f"{len(abut)} merely-abutting/disjoint node pairs")
    for (a, b), ar in sorted(pair_area.items(), key=lambda kv: -kv[1]):
        print(f"    {a}({ch.nodes[a]['name']}) x {b}({ch.nodes[b]['name']}): "
              f"{ar:,.0f} m^2  | index delta {b-a} -> node_bias delta "
              f"{(b-a)*NODE_ORDER_BIAS:.2e} ({(b-a)*NODE_ORDER_BIAS/DEPTH_FLOOR:.2f} x floor)")

print()
print("=== which nodes actually sit in the repro camera footprint? ===")
# crude: the camera looks down-forward; take everything whose y=5 polys are within 900 m
for idx, polys in rows:
    hits = 0
    for pri, matidx, poly, raw in polys:
        for vtx in poly:
            dx, dz = vtx[0] - CAM[0], vtx[2] - CAM[2]
            if dx * dx + dz * dz < 900 * 900:
                hits += 1
                break
    print(f"  {idx} {ch.nodes[idx]['name']:10s} polys within 900 m of the camera: {hits}")
