"""Directionality: for every coplanar overlapping pair of DIFFERENT cblock textures inside
one model, tabulate area by (texture, unk3) on each side, and check the rule
"the subface (unk3) is the one that must win".

Also: does our SceneBuilder bias put the unk3 side in front today?
Surface rank = first-appearance order of (material, priority) in the polygon list; the
higher rank gets the larger depth_bias, i.e. is drawn nearer.
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris, area3

MIN_OV = 1.0


def surface_ranks(mdl):
    order, rank = [], {}
    for p in (mdl.get("polygons") or []):
        ms = p.get("materials")
        mi = ms[0].get("material_index") if ms else p.get("material_index")
        key = (mi, p.get("priority", 0))
        if key not in rank:
            rank[key] = len(order)
            order.append(key)
    return rank


def run(chname):
    ch = Chapter(chname)
    direction = defaultdict(float)   # (texA+unk3A, texB+unk3B) -> area, A sorted first
    agree = defaultdict(float)       # 'subface wins today' / 'base wins today' / 'no subface'
    for mi, mdl in enumerate(ch.models):
        if not isinstance(mdl, dict):
            continue
        polys = mdl.get("polygons") or []
        if len(polys) < 2:
            continue
        rk = surface_ranks(mdl)
        verts = [(v["x"], v["y"], v["z"]) for v in (mdl.get("vertices") or [])]
        recs = []
        for pi, p in enumerate(polys):
            tris = poly_tris(mdl, p, verts)
            if not tris:
                continue
            pl = plane_of(tris[0])
            if pl is None:
                continue
            ms = p.get("materials") or []
            midx = ms[0]["material_index"] if ms else None
            t = (ch.tex_name(midx) or "?").replace(".tif", "")
            if not t.startswith("cblock"):
                continue
            pri = p.get("priority", 0)
            bias = max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05) \
                + min(rk.get((midx, pri), 0), SURFACE_RANK_CAP) * SURFACE_RANK_BIAS
            recs.append([pi, bool((p.get("flags") or {}).get("unk3")), t, pri, bias,
                         quant(*pl), pl[0], tris])
        byplane = defaultdict(list)
        for r in recs:
            byplane[r[5]].append(r)
        for key, items in byplane.items():
            if len(items) < 2:
                continue
            u, v = project_basis(items[0][6])
            proj = [(r, [ensure_ccw(to2d(t, u, v)) for t in r[7]]) for r in items]
            for i in range(len(proj)):
                ri, pi2 = proj[i]
                for j in range(i + 1, len(proj)):
                    rj, pj2 = proj[j]
                    if ri[2] == rj[2]:
                        continue
                    ov = 0.0
                    for a2 in pi2:
                        for b2 in pj2:
                            ov += clip_area(a2, b2)
                    if ov <= MIN_OV:
                        continue
                    ka = f"{ri[2]}{'*' if ri[1] else ' '}"
                    kb = f"{rj[2]}{'*' if rj[1] else ' '}"
                    direction[tuple(sorted((ka, kb)))] += ov
                    if ri[1] == rj[1]:
                        agree["no subface mark (or both marked)"] += ov
                    else:
                        sub, base = (ri, rj) if ri[1] else (rj, ri)
                        if sub[4] > base[4]:
                            agree["subface IS in front today"] += ov
                        elif sub[4] < base[4]:
                            agree["subface is BEHIND today (wrong)"] += ov
                        else:
                            agree["subface exactly TIED today (z-fight)"] += ov
    print(f"== {chname}: cross-texture cblock overlaps, '*' = unk3 set ==")
    for k in sorted(direction, key=lambda k: -direction[k]):
        print(f"    {k[0]:<10} x {k[1]:<10} {direction[k]:>14,.0f} m2")
    print("  where our current depth bias puts the subface:")
    for k in sorted(agree, key=lambda k: -agree[k]):
        print(f"    {k:<38} {agree[k]:>14,.0f} m2")


if __name__ == "__main__":
    for c in (sys.argv[1:] or ["C5"]):
        run(c)
