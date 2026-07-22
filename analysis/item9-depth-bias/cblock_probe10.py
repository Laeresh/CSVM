"""Sizing the subface fix: with subface bias = +N priority levels, does the subface actually
end up in front for every measured subface/base overlap (same-node AND cross-node)?

The worry is cross-node pairs: node_bias = index x 5e-8 already spans up to 2.86 priority
levels in C5, so a +1-level bump can be out-bid by a large node-index gap.
Also reports the priority histogram, so we can see whether +1 level collides with real
authored priorities.
"""
import sys, os
from collections import defaultdict, Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris
from cblock_probe6 import surface_ranks

MIN_OV = 1.0
OFFSETS = [0.0, 0.5, 1.0]


def run(chname):
    ch = Chapter(chname)
    buckets = defaultdict(list)
    prio = Counter()
    for idx, xf in ch.built_nodes():
        n = ch.nodes[idx]
        mi = model_index(n)
        if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
            continue
        mdl = ch.models[mi]
        rk = surface_ranks(mdl)
        wv = [mat_xform(xf, (v["x"], v["y"], v["z"])) for v in (mdl.get("vertices") or [])]
        for p in (mdl.get("polygons") or []):
            tris = poly_tris(mdl, p, wv)
            if not tris:
                continue
            pl = plane_of(tris[0])
            if pl is None:
                continue
            ms = p.get("materials") or []
            midx = ms[0]["material_index"] if ms else None
            pri = p.get("priority", 0)
            prio[pri] += 1
            base = (max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05)
                    + min(rk.get((midx, pri), 0), SURFACE_RANK_CAP) * SURFACE_RANK_BIAS
                    + idx * NODE_ORDER_BIAS)
            u3 = bool((p.get("flags") or {}).get("unk3"))
            for tr in tris:
                buckets[(quant(*pl), pl[0])].append((idx, u3, base, tr))

    res = {o: defaultdict(float) for o in OFFSETS}
    for (key, nrm), items in buckets.items():
        if not any(r[1] for r in items) or len(items) < 2:
            continue
        u, v = project_basis(nrm)
        proj = []
        for r in items:
            t2 = ensure_ccw(to2d(r[3], u, v))
            xs = [p[0] for p in t2]; ys = [p[1] for p in t2]
            proj.append((r, t2, min(xs), min(ys), max(xs), max(ys)))
        for i, (ri, a2, ax0, ay0, ax1, ay1) in enumerate(proj):
            if not ri[1]:
                continue
            for j, (rj, b2, bx0, by0, bx1, by1) in enumerate(proj):
                if i == j or rj[1]:
                    continue
                if bx0 >= ax1 or ax0 >= bx1 or by0 >= ay1 or ay0 >= by1:
                    continue
                ov = clip_area(a2, b2)
                if ov <= MIN_OV:
                    continue
                cross = ri[0] != rj[0]
                for o in OFFSETS:
                    d = (ri[2] + o * DEPTH_BIAS_PER_LEVEL) - rj[2]
                    verdict = "front" if d > 0 else ("behind" if d < 0 else "tied")
                    res[o][(("cross" if cross else "same"), verdict)] += ov
    print(f"== {chname} ==  priority histogram: {dict(prio.most_common(8))}")
    for o in OFFSETS:
        parts = "  ".join(f"{k[0]}/{k[1]}={v:,.0f}" for k, v in sorted(res[o].items()))
        print(f"   subface offset {o:>4} levels -> {parts if parts else '(no subface overlaps)'}")


if __name__ == "__main__":
    for c in (sys.argv[1:] or CHAPTERS):
        run(c)
