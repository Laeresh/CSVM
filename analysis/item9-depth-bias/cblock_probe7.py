"""Q4: the subface population across all 8 chapters, ALL textures.

Same measurement as cblock_probe6 but without the cblock filter: for every coplanar
overlapping polygon pair inside one model where exactly one side carries `unk3`, where does
our current depth bias put the flagged side?
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris, area3
from cblock_probe6 import surface_ranks

MIN_OV = 1.0


def run(chname, show_pairs=0):
    ch = Chapter(chname)
    agree = defaultdict(float)
    pairs = defaultdict(float)
    nflag = 0
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
            u3 = bool((p.get("flags") or {}).get("unk3"))
            nflag += u3
            tris = poly_tris(mdl, p, verts)
            if not tris:
                continue
            pl = plane_of(tris[0])
            if pl is None:
                continue
            ms = p.get("materials") or []
            midx = ms[0]["material_index"] if ms else None
            pri = p.get("priority", 0)
            bias = max(min(pri * DEPTH_BIAS_PER_LEVEL, 0.05), -0.05) \
                + min(rk.get((midx, pri), 0), SURFACE_RANK_CAP) * SURFACE_RANK_BIAS
            recs.append([pi, u3, (ch.tex_name(midx) or "?").replace(".tif", ""), pri, bias,
                         quant(*pl), pl[0], tris])
        byplane = defaultdict(list)
        for r in recs:
            byplane[r[5]].append(r)
        for key, items in byplane.items():
            if len(items) < 2:
                continue
            if not any(r[1] for r in items):
                continue
            u, v = project_basis(items[0][6])
            proj = [(r, [ensure_ccw(to2d(t, u, v)) for t in r[7]]) for r in items]
            for i in range(len(proj)):
                ri, pi2 = proj[i]
                for j in range(i + 1, len(proj)):
                    rj, pj2 = proj[j]
                    if ri[1] == rj[1]:
                        continue
                    ov = 0.0
                    for a2 in pi2:
                        for b2 in pj2:
                            ov += clip_area(a2, b2)
                    if ov <= MIN_OV:
                        continue
                    sub, base = (ri, rj) if ri[1] else (rj, ri)
                    pairs[(base[2], sub[2])] += ov
                    if sub[4] > base[4]:
                        agree["subface in front (correct today)"] += ov
                    elif sub[4] < base[4]:
                        agree["subface BEHIND (inverted today)"] += ov
                    else:
                        agree["subface TIED (guaranteed z-fight)"] += ov
    tot = sum(agree.values())
    print(f"== {chname} ==  polygons carrying unk3: {nflag:,}")
    if tot == 0:
        print("   no subface/base overlapping pair in this chapter")
        return
    for k in ("subface BEHIND (inverted today)", "subface TIED (guaranteed z-fight)",
              "subface in front (correct today)"):
        print(f"   {k:<38} {agree[k]:>14,.0f} m2  ({100*agree[k]/tot:5.1f}%)")
    for k in sorted(pairs, key=lambda k: -pairs[k])[:show_pairs]:
        print(f"     base {k[0]:<22} subface {k[1]:<22} {pairs[k]:>13,.0f} m2")


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("-")]
    sp = 10 if "-p" in sys.argv else 0
    for c in (args or CHAPTERS):
        run(c, sp)
