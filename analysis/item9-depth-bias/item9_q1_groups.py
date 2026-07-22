"""⚠ SUPERSEDED by item9_conflicts.py — kept only as the record of a wrong step.
This version treated each polygon's raw vertex_indices list as an outline. That is
wrong for every `tri_strip` polygon (its index list is not a polygon; a Newell normal
over it is meaningless — docs/verification.md §4), which manufactured bogus horizontal
planes and bogus overlaps between objects kilometres apart. Use triangles.
"""
"""Q1/Q2: how many coplanar same-priority MULTI-NODE groups exist, and how big?

Definition used (deliberately strict, per verification.md rule 9):
  - a polygon contributes its world-space plane (canonically oriented normal, offset d)
  - two polygons are "coplanar" if their normals agree to within NORM_TOL and their
    offsets to within D_TOL
  - a GROUP is a set of >= 2 DISTINCT built nodes sharing one (plane, priority) bucket
  - a group is CONFLICTING only if some cross-node polygon pair in it has non-zero true
    polygon-intersection AREA (Sutherland-Hodgman), not merely overlapping AABBs.

Usage: python item9_q1_groups.py [chapter ...]
"""
import sys, os, math, time
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NORM_TOL = 1e-4      # normals must agree to ~0.006 degrees
D_TOL = 1e-3         # plane offsets within 1 mm
MIN_OVERLAP = 1.0    # m^2 - ignore slivers from float noise


def quant(n, d):
    return (round(n[0] / NORM_TOL), round(n[1] / NORM_TOL), round(n[2] / NORM_TOL),
            round(d / D_TOL))


def analyse(chname, verbose=False, dump_nodes=None):
    ch = Chapter(chname)
    bn = ch.built_nodes()
    # bucket[(planekey, priority)] -> list of (node_idx, poly2d, area, matidx, u, v, plane)
    buckets = defaultdict(list)
    npoly = 0
    for idx, xf in bn:
        for pri, matidx, poly, raw in polygons_world(ch, idx, xf):
            npoly += 1
            pl = plane_of(poly)
            if pl is None:
                continue
            n, d = pl
            buckets[(quant(n, d), pri)].append((idx, poly, n, d, matidx))

    multi = []
    for key, items in buckets.items():
        nodes = {it[0] for it in items}
        if len(nodes) >= 2:
            multi.append((key, items, nodes))

    # true-area overlap test, cross-node only
    conflicting = []
    t0 = time.time()
    for key, items, nodes in multi:
        n = items[0][2]
        u, v = project_basis(n)
        # precompute 2D + aabb
        recs = []
        for (idx, poly, _n, _d, matidx) in items:
            p2 = to2d(poly, u, v)
            xs = [p[0] for p in p2]; ys = [p[1] for p in p2]
            recs.append((idx, p2, min(xs), min(ys), max(xs), max(ys), matidx))
        pairs = set()
        total_ov = 0.0
        # spatial pre-filter then exact clip
        for i in range(len(recs)):
            ai, a2, ax0, ay0, ax1, ay1, _ = recs[i]
            for j in range(i + 1, len(recs)):
                bi, b2, bx0, by0, bx1, by1, _ = recs[j]
                if ai == bi:
                    continue
                if ax1 <= bx0 or bx1 <= ax0 or ay1 <= by0 or by1 <= ay0:
                    continue
                ov = overlap_area(a2, b2)
                if ov > MIN_OVERLAP:
                    pairs.add((min(ai, bi), max(ai, bi)))
                    total_ov += ov
        if pairs:
            gnodes = set()
            for a, b in pairs:
                gnodes.add(a); gnodes.add(b)
            conflicting.append((key, gnodes, pairs, total_ov, items))

    # connected components over conflicting node pairs, WITHIN a priority level
    # (a "group" that would need dense ranks = one component)
    comp_by_pri = defaultdict(list)
    for key, gnodes, pairs, total_ov, items in conflicting:
        comp_by_pri[key[1]].append((gnodes, pairs, total_ov, key))

    print(f"== {chname} ==")
    print(f"  built nodes {len(bn)}, polygons {npoly}")
    print(f"  (plane,priority) buckets spanning >=2 nodes: {len(multi)}")
    print(f"  ... of which have REAL cross-node area overlap: {len(conflicting)}")
    if conflicting:
        sizes = sorted((len(g) for _, g, _, _, _ in conflicting), reverse=True)
        print(f"  group node-counts: max {sizes[0]}, median {sizes[len(sizes)//2]}, "
              f"total distinct nodes {len({n for _,g,_,_,_ in conflicting for n in g})}")
        print(f"  size histogram: ", end="")
        h = defaultdict(int)
        for s in sizes:
            h[s] += 1
        print(" ".join(f"{k}:{v}" for k, v in sorted(h.items())))
        top = sorted(conflicting, key=lambda c: -len(c[1]))[:6]
        for key, gnodes, pairs, ov, items in top:
            (qn0, qn1, qn2, qd), pri = key
            nrm = (qn0 * NORM_TOL, qn1 * NORM_TOL, qn2 * NORM_TOL)
            print(f"    plane n=({nrm[0]:.3f},{nrm[1]:.3f},{nrm[2]:.3f}) d={qd*D_TOL:.2f} pri={pri}: "
                  f"{len(gnodes)} nodes, {len(pairs)} pairs, overlap {ov:,.0f} m^2")
            names = sorted(gnodes)
            print("      nodes: " + ", ".join(f"{i}({ch.nodes[i]['name']})" for i in names[:14])
                  + (" ..." if len(names) > 14 else ""))
    return ch, conflicting


if __name__ == "__main__":
    chs = sys.argv[1:] or ["C5"]
    for c in chs:
        analyse(c)
