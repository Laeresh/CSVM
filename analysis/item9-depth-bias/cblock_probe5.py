"""Q3/Q4: `unk3` as the authored coplanar-overlay ("subface") mark, all textures, all chapters.

For every model, per polygon: own area, and area covered by another coplanar polygon of the
same model (true triangle clipping). Cross-tabulated against `unk3`. If unk3 means subface,
the unk3=False population should be ~never overlapped and the unk3=True population usually
overlapped.
"""
import sys, os, time
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from cblock_probe3 import quant, poly_tris, area3

MIN_OV = 1.0


def run(chname):
    ch = Chapter(chname)
    stat = {True: [0.0, 0.0, 0], False: [0.0, 0.0, 0]}
    inv = 0          # unk3 polygon that is UNDER a non-unk3 one (would break the rule)
    inv_area = 0.0
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
            recs.append([pi, bool((p.get("flags") or {}).get("unk3")), quant(*pl), pl[0],
                         tris, sum(area3(x) for x in tris), 0.0])
        byplane = defaultdict(list)
        for r in recs:
            byplane[r[2]].append(r)
        for key, items in byplane.items():
            if len(items) < 2:
                continue
            u, v = project_basis(items[0][3])
            proj = [(r, [ensure_ccw(to2d(t, u, v)) for t in r[4]]) for r in items]
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
                    ri[6] += ov
                    rj[6] += ov
        for r in recs:
            s = stat[r[1]]
            s[0] += r[5]
            s[1] += min(r[6], r[5])
            s[2] += 1
    a, b = stat[False], stat[True]
    print(f"{chname:<5} unk3=OFF polys {a[2]:>7,} area {a[0]:>15,.0f} overlapped {a[1]:>14,.0f} "
          f"({100*a[1]/max(a[0],1e-9):>5.1f}%)   |  unk3=ON polys {b[2]:>6,} area {b[0]:>14,.0f} "
          f"overlapped {b[1]:>13,.0f} ({100*b[1]/max(b[0],1e-9):>5.1f}%)")


if __name__ == "__main__":
    for c in (sys.argv[1:] or CHAPTERS):
        t = time.time()
        run(c)
