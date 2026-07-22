"""Q3: does the polygon flag `unk3` mark the coplanar overlay ("subface")?

For every model in a chapter, find coplanar overlapping polygon pairs inside that model
(true triangle clipping via item9_lib) and cross-tabulate `unk3` against which side is the
smaller / contained one. If `unk3` means "this face is a subface drawn over the coplanar
face beneath it", the covering face should carry it and the base face should not.

Also reports, per chapter, how many polygons carry `unk3` and whether they overlap anything.
"""
import sys, os
from collections import defaultdict, Counter
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NORM_TOL, D_TOL, MIN_OV = 1e-4, 1e-3, 1.0


def quant(n, d):
    return (round(n[0]/NORM_TOL), round(n[1]/NORM_TOL), round(n[2]/NORM_TOL), round(d/D_TOL))


def poly_tris(mdl, p, wv):
    idxs = p.get("vertex_indices") or []
    m = len(idxs)
    fl = p.get("flags") or {}
    strip = bool(fl.get("tri_strip"))
    out = []
    try:
        if strip:
            for i in range(m-2):
                a, b, c = (i, i+1, i+2) if (i & 1) == 0 else (i, i+2, i+1)
                out.append((wv[idxs[a]], wv[idxs[b]], wv[idxs[c]]))
        else:
            for i in range(1, m-1):
                out.append((wv[idxs[0]], wv[idxs[i]], wv[idxs[i+1]]))
    except IndexError:
        return []
    return out


def area3(t):
    a, b, c = t
    u = (b[0]-a[0], b[1]-a[1], b[2]-a[2]); v = (c[0]-a[0], c[1]-a[1], c[2]-a[2])
    cx, cy, cz = u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]
    return 0.5*(cx*cx+cy*cy+cz*cz)**0.5


def run(chname):
    ch = Chapter(chname)
    tally = Counter()          # (unk3_of_over, unk3_of_under) -> area
    npairs = Counter()
    tex_pairs = defaultdict(float)
    unk3_total = Counter()
    for mi, mdl in enumerate(ch.models):
        if not isinstance(mdl, dict):
            continue
        polys = mdl.get("polygons") or []
        if len(polys) < 2:
            continue
        verts = [(v["x"], v["y"], v["z"]) for v in (mdl.get("vertices") or [])]
        recs = []
        for pi, p in enumerate(polys):
            u3 = bool((p.get("flags") or {}).get("unk3"))
            unk3_total[u3] += 1
            tris = poly_tris(mdl, p, verts)
            if not tris:
                continue
            pl = plane_of(tris[0])
            if pl is None:
                continue
            ms = p.get("materials") or []
            t = ch.tex_name(ms[0]["material_index"]) if ms else None
            recs.append((pi, u3, p.get("priority", 0), t, quant(*pl), pl[0], tris,
                         sum(area3(x) for x in tris)))
        byplane = defaultdict(list)
        for r in recs:
            byplane[r[4]].append(r)
        for key, items in byplane.items():
            if len(items) < 2:
                continue
            u, v = project_basis(items[0][5])
            proj = [(r, [ensure_ccw(to2d(t, u, v)) for t in r[6]]) for r in items]
            for i in range(len(proj)):
                (ri, pi2) = proj[i]
                for j in range(i+1, len(proj)):
                    (rj, pj2) = proj[j]
                    ov = 0.0
                    for a2 in pi2:
                        for b2 in pj2:
                            ov += clip_area(a2, b2)
                    if ov <= MIN_OV:
                        continue
                    # "over" = the smaller-area polygon (the patch), "under" = the larger
                    if ri[7] <= rj[7]:
                        over, under = ri, rj
                    else:
                        over, under = rj, ri
                    tally[(over[1], under[1])] += ov
                    npairs[(over[1], under[1])] += 1
                    if over[3] and under[3]:
                        tex_pairs[(under[3].replace('.tif', ''), over[3].replace('.tif', ''))] += ov
    print(f"== {chname} ==  polygons with unk3: {unk3_total[True]:,} / "
          f"{unk3_total[True]+unk3_total[False]:,}")
    print("  coplanar overlapping pairs INSIDE one model, keyed (unk3 on smaller, unk3 on larger):")
    for k in sorted(tally, key=lambda k: -tally[k]):
        print(f"    smaller_unk3={str(k[0]):<5} larger_unk3={str(k[1]):<5}  "
              f"{npairs[k]:>6,} pairs  {tally[k]:>14,.0f} m2")
    print("  top (larger tex -> smaller tex) overlap pairs:")
    for k in sorted(tex_pairs, key=lambda k: -tex_pairs[k])[:14]:
        print(f"    under {k[0]:<22} over {k[1]:<22} {tex_pairs[k]:>14,.0f} m2")


if __name__ == "__main__":
    for c in (sys.argv[1:] or ["C5"]):
        run(c)
