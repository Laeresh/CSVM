"""Falsifiable prediction of the subface reading: a subface should be CONTAINED in (or
coincident with) a coplanar base polygon, not merely clipping it.

Per unk3 polygon (C5 cblock family), what fraction of its own area is covered by coplanar
non-unk3 polygons, counting same-node AND cross-node bases?
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris, area3


def run(chname, texfilter="cblock"):
    ch = Chapter(chname)
    buckets = defaultdict(list)
    for idx, xf in ch.built_nodes():
        n = ch.nodes[idx]
        mi = model_index(n)
        if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
            continue
        mdl = ch.models[mi]
        wv = [mat_xform(xf, (v["x"], v["y"], v["z"])) for v in (mdl.get("vertices") or [])]
        for pi, p in enumerate(mdl.get("polygons") or []):
            tris = poly_tris(mdl, p, wv)
            if not tris:
                continue
            pl = plane_of(tris[0])
            if pl is None:
                continue
            ms = p.get("materials") or []
            t = (ch.tex_name(ms[0]["material_index"]) if ms else "?") or "?"
            buckets[(quant(*pl), pl[0])].append(
                (idx, pi, bool((p.get("flags") or {}).get("unk3")), t.replace(".tif", ""),
                 tris, sum(area3(x) for x in tris)))
    hist = defaultdict(lambda: [0, 0.0, 0.0])
    for (key, nrm), items in buckets.items():
        subs = [r for r in items if r[2] and (not texfilter or r[3].startswith(texfilter))]
        if not subs:
            continue
        bases = [r for r in items if not r[2]]
        if not bases:
            continue
        u, v = project_basis(nrm)
        bproj = [[ensure_ccw(to2d(t, u, v)) for t in r[4]] for r in bases]
        for r in subs:
            sp = [ensure_ccw(to2d(t, u, v)) for t in r[4]]
            cov = 0.0
            for b in bproj:
                for a2 in sp:
                    for b2 in b:
                        cov += clip_area(a2, b2)
            frac = min(cov / max(r[5], 1e-9), 1.0)
            b = int(frac * 10 + 1e-9)
            b = min(b, 10)
            hist[b][0] += 1
            hist[b][1] += r[5]
            hist[b][2] += frac
    print(f"== {chname}: coverage of each unk3 '{texfilter}' polygon by coplanar non-unk3 polygons ==")
    tp = sum(h[0] for h in hist.values())
    for b in sorted(hist):
        lo, hi = b * 10, min(b * 10 + 10, 100)
        print(f"   covered {lo:>3}-{hi:<3}%  polys {hist[b][0]:>4}  area {hist[b][1]:>13,.0f} m2")
    print(f"   total {tp} unk3 polygons considered")


if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) > 1 else "C5",
        sys.argv[2] if len(sys.argv) > 2 else "cblock")
