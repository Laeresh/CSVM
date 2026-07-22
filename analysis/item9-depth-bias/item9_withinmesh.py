"""The hypothesis section C turned up: the C5 repro's coplanar stack is WITHIN ONE MESH,
between DIFFERENT (material, priority) surfaces — and `SurfaceRankCap` (5) collapses every
surface from rank 5 onward onto ONE depth bias.

Total bias of a world surface (SceneBuilder.cs):
    depth_bias = clamp(priority * 2e-4, +-0.05) + min(rank, 5) * 2e-6      (per material)
    node_bias  = node.Index * 5e-8                                          (per instance)
Two surfaces of the SAME node therefore separate only through `rank`, and two surfaces whose
ranks are both >= 5 do not separate AT ALL.

This script reports, per chapter:
  - meshes with more than 6 (material, priority) surface groups (i.e. rank saturates)
  - coplanar, same-priority, genuinely OVERLAPPING triangle pairs from two DIFFERENT
    surfaces of one mesh, split by whether their capped ranks differ
  - the same for pairs inside ONE surface (rank identical by construction)
"""
import sys, os, time
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *

NORM_TOL = 1e-4
D_TOL = 1e-3
MIN_OVERLAP = 1.0
CAP = SURFACE_RANK_CAP = 5


def quant(n, d):
    return (round(n[0] / NORM_TOL), round(n[1] / NORM_TOL), round(n[2] / NORM_TOL),
            round(d / D_TOL))


def surface_ranks(mdl):
    """Reproduce BuildMesh's grouping. The world builder passes cullBackfaces:false, so
    `doubleSided` is always true and the key is (material, priority)."""
    order, rank = [], {}
    for p in (mdl.get("polygons") or []):
        mats = p.get("materials")
        mi = mats[0].get("material_index") if mats else p.get("material_index")
        key = (mi, p.get("priority", 0))
        if key not in rank:
            rank[key] = len(order)
            order.append(key)
    return rank, len(order)


def run(chname, top=8):
    ch = Chapter(chname)
    bn = ch.built_nodes()
    saturated = 0
    meshes_seen = set()
    cross_same_bias = defaultdict(float)   # (node, plane, pri) -> area, ranks tie AFTER cap
    cross_sep = defaultdict(float)
    within_surface = defaultdict(float)
    grp_hist = defaultdict(int)

    for idx, xf in bn:
        n = ch.nodes[idx]
        mi = model_index(n)
        if mi is None or mi < 0 or mi >= len(ch.models):
            continue
        mdl = ch.models[mi]
        if not isinstance(mdl, dict):
            continue
        if mi not in meshes_seen:
            meshes_seen.add(mi)
            rk, ngroups = surface_ranks(mdl)
            grp_hist[min(ngroups, 40)] += 1
            if ngroups > CAP + 1:
                saturated += 1
        rk, ngroups = surface_ranks(mdl)

        buck = defaultdict(list)
        for pri, matidx, tri in triangles_world(ch, idx, xf):
            pl = plane_of(tri)
            if pl is None:
                continue
            buck[(quant(*pl), pri)].append((matidx, tri, pl[0]))
        for key, items in buck.items():
            if len(items) < 2:
                continue
            u, v = project_basis(items[0][2])
            recs = []
            for (matidx, tri, _n) in items:
                t2 = ensure_ccw(to2d(tri, u, v))
                xs = [p[0] for p in t2]; ys = [p[1] for p in t2]
                recs.append((matidx, t2, min(xs), min(ys), max(xs), max(ys)))
            recs.sort(key=lambda r: r[2])
            for i in range(len(recs)):
                am, a2, ax0, ay0, ax1, ay1 = recs[i]
                for j in range(i + 1, len(recs)):
                    bm, b2, bx0, by0, bx1, by1 = recs[j]
                    if bx0 >= ax1:
                        break
                    if ay1 <= by0 or by1 <= ay0:
                        continue
                    ov = clip_area(a2, b2)
                    if ov <= MIN_OVERLAP:
                        continue
                    ra = min(rk.get((am, key[1]), 0), CAP)
                    rb = min(rk.get((bm, key[1]), 0), CAP)
                    k = (idx, key, ch.tex_name(am), ch.tex_name(bm), ra, rb)
                    if am == bm:
                        within_surface[k] += ov
                    elif ra == rb:
                        cross_same_bias[k] += ov
                    else:
                        cross_sep[k] += ov

    print(f"== {chname} ==")
    print(f"  distinct meshes built {len(meshes_seen)}; with >6 surface groups "
          f"(rank saturates at the cap): {saturated} ({saturated/max(len(meshes_seen),1):.1%})")
    print(f"  group-count histogram (groups:meshes): "
          + " ".join(f"{k}:{v}" for k, v in sorted(grp_hist.items()))[:300])
    for label, dd in (("SAME-MESH, DIFFERENT surfaces, EQUAL capped rank -> ZERO separation",
                       cross_same_bias),
                      ("SAME-MESH, DIFFERENT surfaces, rank differs -> 2e-6+ separation",
                       cross_sep),
                      ("SAME SURFACE (identical material+priority) -> ZERO separation",
                       within_surface)):
        tot = sum(dd.values())
        print(f"  {label}")
        print(f"      pairs {len(dd)}, total overlap {tot:,.0f} m^2, "
              f"nodes {len({k[0] for k in dd})}")
        for k, a in sorted(dd.items(), key=lambda kv: -kv[1])[:top]:
            idx, key, t1, t2, ra, rb = k
            nrm = (key[0][0]*NORM_TOL, key[0][1]*NORM_TOL, key[0][2]*NORM_TOL)
            print(f"        {idx}({ch.nodes[idx]['name']}) pri {key[1]} "
                  f"n=({nrm[0]:.2f},{nrm[1]:.2f},{nrm[2]:.2f}) d={key[0][3]*D_TOL:.1f} "
                  f"ranks {ra}/{rb}  {t1} vs {t2}  {a:,.0f} m^2")
    return


if __name__ == "__main__":
    for c in (sys.argv[1:] or CHAPTERS):
        run(c)
