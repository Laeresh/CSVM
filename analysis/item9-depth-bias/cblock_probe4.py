"""Q3 refined: is `unk3` exactly the "I am the coplanar overlay" mark?

Per polygon, compute how much of its own area is covered by ANOTHER coplanar polygon of
the same model, split by whether the coverer is the cblock day-set (4/5/6) or not, and
cross-tabulate against `unk3`.
"""
import sys, os
from collections import defaultdict, Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris, area3

MIN_OV = 1.0


def run(chname, only_cblock=True):
    ch = Chapter(chname)
    # per (texture, unk3): total area, area covered by another poly, area covering another
    stat = defaultdict(lambda: [0.0, 0.0, 0.0, 0])
    for mi, mdl in enumerate(ch.models):
        if not isinstance(mdl, dict):
            continue
        polys = mdl.get("polygons") or []
        if len(polys) < 2:
            continue
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
            t = ch.tex_name(ms[0]["material_index"]) if ms else None
            t = (t or "?").replace(".tif", "")
            if only_cblock and not t.startswith("cblock"):
                continue
            recs.append([pi, bool((p.get("flags") or {}).get("unk3")), t,
                         quant(*pl), pl[0], tris, sum(area3(x) for x in tris), 0.0, 0.0])
        byplane = defaultdict(list)
        for r in recs:
            byplane[r[3]].append(r)
        for key, items in byplane.items():
            if len(items) < 2:
                continue
            u, v = project_basis(items[0][4])
            proj = [(r, [ensure_ccw(to2d(t, u, v)) for t in r[5]]) for r in items]
            for i in range(len(proj)):
                ri, pi2 = proj[i]
                for j in range(i+1, len(proj)):
                    rj, pj2 = proj[j]
                    ov = 0.0
                    for a2 in pi2:
                        for b2 in pj2:
                            ov += clip_area(a2, b2)
                    if ov <= MIN_OV:
                        continue
                    ri[7] += ov      # overlapped area for i
                    rj[7] += ov
        for r in recs:
            s = stat[(r[2], r[1])]
            s[0] += r[6]
            s[1] += min(r[7], r[6])
            s[3] += 1
    print(f"== {chname}: cblock polygons, area vs coplanar overlap vs unk3 ==")
    print(f"  {'texture':<10} {'unk3':<6} {'polys':>6} {'own area m2':>15} {'overlapped m2':>15} {'%':>7}")
    for k in sorted(stat):
        s = stat[k]
        print(f"  {k[0]:<10} {str(k[1]):<6} {s[3]:>6} {s[0]:>15,.0f} {s[1]:>15,.0f} "
              f"{100*s[1]/max(s[0],1e-9):>6.1f}%")


if __name__ == "__main__":
    for c in (sys.argv[1:] or ["C5"]):
        run(c)
