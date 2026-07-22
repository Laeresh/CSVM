"""Q1/Q2: what the cblock family is, and how g4683's own surfaces relate in world space.

Read-only. Uses item9_lib (the committed loader) so triangulation/transform handling is
identical to the earlier passes.
"""
import sys, os, json
from collections import defaultdict, Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

ch = Chapter("C5")
bn = ch.built_nodes()
byidx = {i: xf for i, xf in bn}

# --- which built nodes use any cblock material, and how much area each ---
use = defaultdict(lambda: defaultdict(float))   # node -> tex -> area
pri = defaultdict(lambda: defaultdict(set))
for idx, xf in bn:
    for p, m, tri in triangles_world(ch, idx, xf):
        t = ch.tex_name(m)
        if not t or 'cblock' not in t:
            continue
        a, b, c = tri
        ux, uy, uz = b[0]-a[0], b[1]-a[1], b[2]-a[2]
        vx, vy, vz = c[0]-a[0], c[1]-a[1], c[2]-a[2]
        cx, cy, cz = uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx
        use[idx][t] += 0.5*(cx*cx+cy*cy+cz*cz)**0.5
        pri[idx][t].add(p)

print("== C5: nodes using cblock materials ==")
tot = defaultdict(float)
for idx in sorted(use, key=lambda i: -sum(use[i].values())):
    n = ch.nodes[idx]
    parts = " ".join(f"{t.replace('.tif','')}:{a:,.0f}m2/pri{sorted(pri[idx][t])}"
                     for t, a in sorted(use[idx].items()))
    print(f"  node {idx:5d} {n['name']:<12} {parts}")
    for t, a in use[idx].items():
        tot[t] += a
print("\n  totals by texture:")
for t, a in sorted(tot.items()):
    print(f"    {t:<16} {a:>14,.0f} m2")
print(f"\n  nodes total: {len(use)}")
