"""polish-4 item 9 — the load-bearing measurement, on TRIANGLES.

Supersedes item9_q1_groups.py / item9_q1_dag.py, which used raw polygon index lists and
were therefore wrong for every `tri_strip` polygon (a strip's index list is not an
outline; its Newell normal is meaningless — docs/verification.md §4).

Produces, per chapter:
  - the set of node pairs that are genuinely CONFLICTING: same priority, exactly coplanar
    in world space, with non-zero true triangle-intersection AREA (not an AABB overlap —
    verification.md rule 9)
  - the connected components of that relation
  - the LONGEST INDEX-ORDERED CHAIN, which is the number of dense rank slots a
    conflict-local bias would have to spend inside one priority level.

Budget: DepthBiasPerLevel / depth-resolution floor = 2e-4 / 1e-6 = 200 slots.
"""
import sys, os, time, json
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NORM_TOL = 1e-4
D_TOL = 1e-3
MIN_OVERLAP = 1.0        # m^2
SLOT_BUDGET = int(DEPTH_BIAS_PER_LEVEL / DEPTH_FLOOR)


def quant(n, d):
    return (round(n[0] / NORM_TOL), round(n[1] / NORM_TOL), round(n[2] / NORM_TOL),
            round(d / D_TOL))


def tri_area(t2):
    (x1, y1), (x2, y2), (x3, y3) = t2
    return abs((x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1)) * 0.5


def build(chname, drop_parked=False, drop_names=("fvol",)):
    ch = Chapter(chname)
    bn = ch.built_nodes()
    drop = set()
    if drop_parked:
        from item9_q1_dag import parked_roots
        drop = parked_roots(ch)
    keep = [(i, x) for i, x in bn
            if i not in drop and not any(ch.nodes[i]["name"].startswith(p) for p in drop_names)]

    buckets = defaultdict(list)
    ntri = 0
    for idx, xf in keep:
        for pri, matidx, tri in triangles_world(ch, idx, xf):
            ntri += 1
            pl = plane_of(tri)
            if pl is None:
                continue
            n, d = pl
            buckets[(quant(n, d), pri)].append((idx, tri, n, matidx))
    return ch, keep, ntri, buckets, drop


def conflict_pairs(buckets, want_detail=None):
    edges = {}
    detail = defaultdict(list)
    for key, items in buckets.items():
        nodes = {it[0] for it in items}
        if len(nodes) < 2:
            continue
        n = items[0][2]
        u, v = project_basis(n)
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
                if ov <= MIN_OVERLAP:
                    continue
                k = (min(ai, bi), max(ai, bi))
                e = edges.get(k)
                if e is None:
                    edges[k] = [ov, key]
                else:
                    e[0] += ov
                if want_detail is not None and (ai in want_detail or bi in want_detail):
                    detail[k].append((key, ov, am, bm))
    return edges, detail


def longest_chain(edges):
    succ = defaultdict(list)
    nodes = set()
    for a, b in edges:
        succ[a].append(b)
        nodes.add(a); nodes.add(b)
    best, prev = {}, {}
    for n in sorted(nodes, reverse=True):
        b, p = 1, None
        for m in succ[n]:
            if best[m] + 1 > b:
                b, p = best[m] + 1, m
        best[n], prev[n] = b, p
    if not best:
        return 0, []
    start = max(best, key=lambda k: best[k])
    chain = [start]
    while prev[chain[-1]] is not None:
        chain.append(prev[chain[-1]])
    return best[start], list(reversed(chain))


def components(edges):
    parent = {}

    def find(x):
        while parent.setdefault(x, x) != x:
            parent[x] = parent.setdefault(parent[x], parent[x])
            x = parent[x]
        return x

    for a, b in edges:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    comp = defaultdict(set)
    for n in list(parent):
        comp[find(n)].add(n)
    return comp


def run(chname, drop_parked=False):
    t0 = time.time()
    ch, keep, ntri, buckets, drop = build(chname, drop_parked)
    edges, _ = conflict_pairs(buckets)
    comp = components(edges)
    sizes = sorted((len(v) for v in comp.values()), reverse=True)
    depth, chain = longest_chain(edges)
    idx_deltas = sorted(b - a for a, b in edges)
    print(f"== {chname} ==  ({time.time()-t0:.0f}s){'  [parked-at-origin dropped]' if drop_parked else ''}")
    print(f"  built nodes {len(keep)}, TRIANGLES {ntri:,}, planar buckets {len(buckets):,}")
    print(f"  conflicting node pairs (coplanar + same priority + real area): {len(edges):,}")
    print(f"  distinct nodes involved: {len({x for p in edges for x in p}):,}")
    print(f"  components: {len(comp)}; sizes max {sizes[0] if sizes else 0}, top8 {sizes[:8]}")
    print(f"  >>> longest ordered chain (rank slots needed): {depth}  [budget {SLOT_BUDGET}]"
          f"  {'OK' if depth <= SLOT_BUDGET else 'OVER'}")
    if idx_deltas:
        n = len(idx_deltas)
        below = sum(1 for d in idx_deltas if d * NODE_ORDER_BIAS < DEPTH_FLOOR)
        print(f"  node-index deltas of conflicting pairs: min {idx_deltas[0]}, "
              f"median {idx_deltas[n//2]}, max {idx_deltas[-1]}")
        print(f"  ... pairs whose CURRENT node_bias delta is below the 1e-6 floor: "
              f"{below}/{n} ({below/n:.1%})")
    if chain:
        print("      chain: " + " -> ".join(f"{i}({ch.nodes[i]['name']})" for i in chain[:12])
              + (" ..." if len(chain) > 12 else ""))
    return dict(chapter=chname, nodes=len(keep), tris=ntri, pairs=len(edges),
                comps=len(comp), maxcomp=sizes[0] if sizes else 0, chain=depth,
                below_floor=below if idx_deltas else 0, total=len(idx_deltas))


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    dp = "--drop-parked" in sys.argv
    out = [run(c, dp) for c in (args or CHAPTERS)]
    print()
    print("chapter | nodes | tris | conflict pairs | comps | max comp | rank slots | below floor")
    for r in out:
        print(f"{r['chapter']:7s} | {r['nodes']:5d} | {r['tris']:6d} | {r['pairs']:14d} | "
              f"{r['comps']:5d} | {r['maxcomp']:8d} | {r['chain']:10d} | "
              f"{r['below_floor']}/{r['total']}")
