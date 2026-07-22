"""Q3 (cont.): the actual y = 5 outlines around the C5 repro, and what (if anything)
node 1777 `g4683` genuinely overlaps anywhere in the chapter.

Also cross-checks the abut-vs-overlap verdict with an independent RASTER test, because
the whole item-9 history is a chain of trusting one geometric predicate too far.
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NINE = [1777, 1799, 1800, 1801, 1813, 1814, 1822, 1823, 1837]
CAM = (-9533.178, 76.319, -3367.413)

ch = Chapter("C5")
bn = dict(ch.built_nodes())

print("=== every y = 5 polygon of the nine, as an x/z outline ===")
for idx in NINE:
    n = ch.nodes[idx]
    got = []
    for pri, matidx, poly, raw in polygons_world(ch, idx, bn[idx]):
        if all(abs(v[1] - 5.0) < 1e-4 for v in poly):
            xs = [v[0] for v in poly]; zs = [v[2] for v in poly]
            got.append((pri, ch.tex_name(matidx), min(xs), max(xs), min(zs), max(zs), len(poly)))
    print(f"  {idx} {n['name']} — {len(got)} flat polys at y=5")
    for g in sorted(got, key=lambda t: (t[0], t[2], t[4])):
        print(f"      pri {g[0]:4d} {str(g[1]):16s} x[{g[2]:9.0f},{g[3]:9.0f}] "
              f"z[{g[4]:9.0f},{g[5]:9.0f}] ({g[6]}-gon, {(g[3]-g[2])*(g[5]-g[4]):,.0f} m^2 bbox)")

print()
print("=== does ANY built node overlap g4683 (1777) in area, on any shared plane? ===")
target = 1777
tpolys = []
for pri, matidx, poly, raw in polygons_world(ch, target, bn[target]):
    pl = plane_of(poly)
    if pl:
        tpolys.append((pri, pl[0], pl[1], poly, ch.tex_name(matidx)))

hits = []
for idx, xf in bn.items():
    if idx == target:
        continue
    for pri, matidx, poly, raw in polygons_world(ch, idx, xf):
        pl = plane_of(poly)
        if pl is None:
            continue
        n2, d2 = pl
        for (tpri, tn, td, tpoly, ttex) in tpolys:
            if tpri != pri:
                continue
            if abs(n2[0]-tn[0]) > 1e-4 or abs(n2[1]-tn[1]) > 1e-4 or abs(n2[2]-tn[2]) > 1e-4:
                continue
            if abs(d2 - td) > 1e-3:
                continue
            u, v = project_basis(tn)
            ov = overlap_area(to2d(tpoly, u, v), to2d(poly, u, v))
            if ov > 1.0:
                hits.append((idx, ch.nodes[idx]['name'], pri, round(td, 3), ov,
                             ttex, ch.tex_name(matidx)))
print(f"  overlapping polygon pairs found: {len(hits)}")
agg = defaultdict(float)
for idx, name, pri, d, ov, t1, t2 in hits:
    agg[(idx, name, pri, d, t1, t2)] += ov
for k, ov in sorted(agg.items(), key=lambda kv: -kv[1])[:25]:
    print(f"    node {k[0]} {k[1]:12s} pri {k[2]:4d} plane d={k[3]:.2f} "
          f"{ov:,.0f} m^2  ({k[4]} vs {k[5]})")

print()
print("=== independent RASTER cross-check of the nine at y = 5, priority 0 ===")
# 4 m cells over the repro footprint; count cells covered by >1 distinct node
cells = defaultdict(set)
STEP = 8.0
for idx in NINE:
    for pri, matidx, poly, raw in polygons_world(ch, idx, bn[idx]):
        if pri != 0 or not all(abs(v[1] - 5.0) < 1e-4 for v in poly):
            continue
        p2 = [(v[0], v[2]) for v in poly]
        xs = [p[0] for p in p2]; zs = [p[1] for p in p2]
        x0 = math.floor(min(xs) / STEP) * STEP
        z0 = math.floor(min(zs) / STEP) * STEP
        x = x0
        while x < max(xs):
            z = z0
            while z < max(zs):
                cx, cz = x + STEP / 2, z + STEP / 2
                # point-in-polygon
                inside = False
                m = len(p2)
                for a in range(m):
                    x1, z1 = p2[a]; x2, z2 = p2[(a + 1) % m]
                    if (z1 > cz) != (z2 > cz):
                        xi = x1 + (cz - z1) * (x2 - x1) / (z2 - z1)
                        if xi > cx:
                            inside = not inside
                if inside:
                    cells[(int(x // STEP), int(z // STEP))].add(idx)
                z += STEP
            x += STEP
tot = len(cells)
multi = sum(1 for v in cells.values() if len(v) > 1)
print(f"  {tot:,} covered {STEP:.0f} m cells; {multi:,} covered by MORE THAN ONE of the nine "
      f"({multi/max(tot,1):.2%})")
if multi:
    ex = [(k, sorted(v)) for k, v in cells.items() if len(v) > 1][:10]
    for k, v in ex:
        print(f"    cell {k} covered by {v}")
