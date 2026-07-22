"""Cross-node subface check: does an `unk3` polygon overlap a coplanar polygon that lives in
a DIFFERENT node? (The within-model probe, cblock_probe7, only sees same-model pairs.)

Buckets every built triangle by exact plane, then for each unk3 triangle tests true
intersection against every same-plane triangle, reporting same-node vs cross-node area and
whether our current bias already puts the subface in front.
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris, area3
from cblock_probe6 import surface_ranks

MIN_OV = 1.0


def run(chname):
    ch = Chapter(chname)
    buckets = defaultdict(list)     # plane -> list of (node, u3, tex, bias, tri)
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
            bias = (max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05)
                    + min(rk.get((midx, pri), 0), SURFACE_RANK_CAP) * SURFACE_RANK_BIAS
                    + idx * NODE_ORDER_BIAS)
            u3 = bool((p.get("flags") or {}).get("unk3"))
            t = (ch.tex_name(midx) or "?").replace(".tif", "")
            for tr in tris:
                buckets[(quant(*pl), pl[0])].append((idx, u3, t, bias, tr))

    same_node = defaultdict(float)
    cross_node = defaultdict(float)
    pairs = defaultdict(float)
    for (key, nrm), items in buckets.items():
        if not any(r[1] for r in items) or len(items) < 2:
            continue
        u, v = project_basis(nrm)
        proj = []
        for r in items:
            t2 = ensure_ccw(to2d(r[4], u, v))
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
                bucket = same_node if ri[0] == rj[0] else cross_node
                if ri[3] > rj[3]:
                    bucket["subface in front (correct today)"] += ov
                elif ri[3] < rj[3]:
                    bucket["subface BEHIND (inverted today)"] += ov
                else:
                    bucket["subface TIED"] += ov
                pairs[(rj[2], ri[2], ri[0] == rj[0])] += ov
    print(f"== {chname} ==")
    for label, dd in (("same node", same_node), ("CROSS node", cross_node)):
        tot = sum(dd.values())
        print(f"  {label}: total {tot:,.0f} m2")
        for k in sorted(dd, key=lambda k: -dd[k]):
            print(f"      {k:<38} {dd[k]:>13,.0f} m2")
    for k in sorted(pairs, key=lambda k: -pairs[k])[:10]:
        print(f"      base {k[0]:<22} subface {k[1]:<22} "
              f"{'same-node' if k[2] else 'CROSS-NODE':<11} {pairs[k]:>12,.0f} m2")


if __name__ == "__main__":
    for c in (sys.argv[1:] or CHAPTERS):
        run(c)
