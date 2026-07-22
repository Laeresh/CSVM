"""⚠ SUPERSEDED by item9_conflicts.py — kept only as the record of a wrong step.
This version treated each polygon's raw vertex_indices list as an outline. That is
wrong for every `tri_strip` polygon (its index list is not a polygon; a Newell normal
over it is meaningless — docs/verification.md §4), which manufactured bogus horizontal
planes and bogus overlaps between objects kilometres apart. Use triangles.
"""
"""Q1 (the decisive arithmetic): how many DENSE RANK SLOTS would a conflict-local bias need?

`node_bias` is ONE scalar per MeshInstance3D. So any "dense rank within a coplanar group"
scheme still has to produce a SINGLE global number per node. For every conflicting pair
(a, b) it must hold that
    |rank(a) - rank(b)| >= 1      (so the separation clears the ~1e-6 resolution floor)
and, to preserve the authored layering (nodes.json DFS order = draw order, later wins)
    a.Index < b.Index  =>  rank(a) < rank(b).

That makes the conflict relation a DAG (edges point from lower node index to higher), and
the number of rank slots needed is the LONGEST PATH in it + 1.

Budget: ranks must stay strictly inside one priority level, i.e.
    slots * unit < DepthBiasPerLevel (2e-4), with unit >= floor (1e-6)
    => at most 200 slots.

Also excludes, per the plan's cautions:
  - roots parked at the world origin (item 4's HideUnplacedEntities) - not drawn
  - `fvol*` fog volumes
"""
import sys, os, time
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NORM_TOL = 1e-4
D_TOL = 1e-3
MIN_OVERLAP = 1.0
SLOT_BUDGET = int(DEPTH_BIAS_PER_LEVEL / DEPTH_FLOOR)   # 200


def quant(n, d):
    return (round(n[0] / NORM_TOL), round(n[1] / NORM_TOL), round(n[2] / NORM_TOL),
            round(d / D_TOL))


def parked_roots(ch):
    """Mirror of WorldBuilder.IsParkedAtOrigin: transformless walk root whose built
    geometry AABB strictly straddles the origin in x and z."""
    out = set()
    for r in ch.walk_roots():
        if not (0 <= r < len(ch.nodes)):
            continue
        if node_local(ch.nodes[r]) is not None:
            continue
        lo = [1e30] * 3
        hi = [-1e30] * 3
        stack = [(r, mat_identity())]
        seen = set()
        got = False
        while stack:
            idx, xf = stack.pop()
            if idx in seen:
                continue
            seen.add(idx)
            n = ch.nodes[idx]
            loc = node_local(n)
            cur = mat_mul(xf, loc) if loc else xf
            mi = model_index(n)
            if 0 <= mi < len(ch.models) and isinstance(ch.models[mi], dict):
                for v in (ch.models[mi].get("vertices") or []):
                    w = mat_xform(cur, (v["x"], v["y"], v["z"]))
                    got = True
                    for k in range(3):
                        lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])
            for c in (n.get("child_indices") or n.get("children") or []):
                if 0 <= c < len(ch.nodes):
                    stack.append((c, cur))
        if got and lo[0] < 0 < hi[0] and lo[2] < 0 < hi[2]:
            out |= seen
    return out


def conflicts(ch, drop):
    bn = [(i, x) for i, x in ch.built_nodes()
          if i not in drop and not ch.nodes[i]["name"].startswith("fvol")]
    buckets = defaultdict(list)
    npoly = 0
    for idx, xf in bn:
        for pri, matidx, poly, raw in polygons_world(ch, idx, xf):
            npoly += 1
            pl = plane_of(poly)
            if pl is None:
                continue
            n, d = pl
            buckets[(quant(n, d), pri)].append((idx, poly, n))

    edges = set()            # (lower node index, higher node index)
    bucket_groups = []       # (key, set(nodes), pairs)
    for key, items in buckets.items():
        if len({it[0] for it in items}) < 2:
            continue
        n = items[0][2]
        u, v = project_basis(n)
        recs = []
        for (idx, poly, _n) in items:
            p2 = to2d(poly, u, v)
            xs = [p[0] for p in p2]; ys = [p[1] for p in p2]
            recs.append((idx, p2, min(xs), min(ys), max(xs), max(ys)))
        recs.sort(key=lambda r: r[2])
        pairs = set()
        for i in range(len(recs)):
            ai, a2, ax0, ay0, ax1, ay1 = recs[i]
            for j in range(i + 1, len(recs)):
                bi, b2, bx0, by0, bx1, by1 = recs[j]
                if bx0 >= ax1:
                    break
                if ai == bi or ay1 <= by0 or by1 <= ay0:
                    continue
                if (min(ai, bi), max(ai, bi)) in pairs:
                    continue
                if overlap_area(a2, b2) > MIN_OVERLAP:
                    pairs.add((min(ai, bi), max(ai, bi)))
        if pairs:
            edges |= pairs
            bucket_groups.append((key, {x for p in pairs for x in p}, pairs))
    return bn, npoly, edges, bucket_groups


def longest_path(edges):
    """Edges are (lo, hi) with lo < hi, so node index order is already a topological
    order. Returns (max chain length in NODES, the chain)."""
    succ = defaultdict(list)
    nodes = set()
    for a, b in edges:
        succ[a].append(b)
        nodes.add(a); nodes.add(b)
    best = {}
    prev = {}
    for n in sorted(nodes, reverse=True):
        b, p = 1, None
        for m in succ[n]:
            if best[m] + 1 > b:
                b, p = best[m] + 1, m
        best[n] = b
        prev[n] = p
    if not best:
        return 0, []
    start = max(best, key=lambda k: best[k])
    chain = [start]
    while prev[chain[-1]] is not None:
        chain.append(prev[chain[-1]])
    return best[start], chain


def run(chname, use_drop=True):
    t0 = time.time()
    ch = Chapter(chname)
    drop = parked_roots(ch) if use_drop else set()
    bn, npoly, edges, groups = conflicts(ch, drop)
    comp = defaultdict(set)
    parent = {}

    def find(x):
        while parent.get(x, x) != x:
            parent[x] = parent.get(parent[x], parent[x])
            x = parent[x]
        return x

    for a, b in edges:
        parent.setdefault(a, a); parent.setdefault(b, b)
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    for n in parent:
        comp[find(n)].add(n)
    sizes = sorted((len(v) for v in comp.values()), reverse=True)
    depth, chain = longest_path(edges)

    print(f"== {chname} ==  ({time.time()-t0:.0f}s)")
    print(f"  built nodes {len(bn)} (dropped {len(drop)} parked-at-origin), polygons {npoly}")
    print(f"  conflicting NODE PAIRS (true coplanar area overlap, same priority): {len(edges)}")
    print(f"  distinct nodes involved: {len({x for p in edges for x in p})}")
    print(f"  buckets with a conflict: {len(groups)}")
    print(f"  connected components: {len(comp)}; sizes max {sizes[0] if sizes else 0}, "
          f"top10 {sizes[:10]}")
    print(f"  >>> LONGEST ORDERED CHAIN (= rank slots required): {depth}   "
          f"[budget {SLOT_BUDGET}]  {'OK' if depth <= SLOT_BUDGET else 'OVER BUDGET'}")
    if chain:
        c = list(reversed(chain))
        print(f"      chain head: " + " -> ".join(f"{i}({ch.nodes[i]['name']})" for i in c[:10])
              + (" ..." if len(c) > 10 else ""))
    return depth, sizes


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    keep = "--keep-parked" in sys.argv
    for c in (args or CHAPTERS):
        run(c, use_drop=not keep)
