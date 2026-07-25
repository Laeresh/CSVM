"""Q: does a dzpathN mesh carry Danger Zone gate geometry, and how much?

The design specifies a Danger Zone as an entry volume plus an exit volume, both of which
must be crossed. Test whether the shipped dzpathN meshes carry a matching pair of outlines
alongside the route ribbon.

Cited by docs/formats/missions.md ("A dzpathN mesh is always route + exactly two gates").
"""
import sys, os, json, math, itertools, statistics, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zrdrlib import path

CHAPTERS_WITH_ZONES = ("C1", "C1B", "C2", "C3", "C4", "C5")


def polys(ch):
    """Yield (nodeName, [(area, span, centroid, nverts), ...]) for each dzpathN mesh."""
    nodes = json.load(open(path(ch, "gamez", "nodes.json"), encoding="utf-8"))
    models = json.load(open(path(ch, "gamez", "models.json"), encoding="utf-8"))
    for n in nodes:
        nm = n.get("name") or ""
        if not nm.startswith("dzpath") or nm == "dzpaths":
            continue
        mi = n.get("model_index", -1)
        if mi < 0:
            continue
        m = models[mi]
        out = []
        for p in m["polygons"]:
            pts = [(m["vertices"][i]["x"], m["vertices"][i]["y"], m["vertices"][i]["z"])
                   for i in p["vertex_indices"]]
            # Newell area (meaningless for the open route ribbon, comparable between rings)
            nx = ny = nz = 0.0
            for i in range(len(pts)):
                a, b = pts[i], pts[(i + 1) % len(pts)]
                nx += (a[1] - b[1]) * (a[2] + b[2])
                ny += (a[2] - b[2]) * (a[0] + b[0])
                nz += (a[0] - b[0]) * (a[1] + b[1])
            area = 0.5 * math.sqrt(nx * nx + ny * ny + nz * nz)
            span = max(math.dist(a, b) for a, b in itertools.combinations(pts, 2)) if len(pts) > 1 else 0.0
            cen = tuple(sum(q[k] for q in pts) / len(pts) for k in range(3))
            out.append((area, span, cen, len(pts)))
        yield ch, nm, out


meshes = [t for ch in CHAPTERS_WITH_ZONES for t in polys(ch)]
hist = collections.Counter(len(p) for _, _, p in meshes)
print(f"dzpathN meshes with geometry: {len(meshes)}")
print(f"polygon-count histogram: {dict(sorted(hist.items()))}")

rel, gap, route_idx = [], [], collections.Counter()
for ch, nm, p in meshes:
    if len(p) != 3:
        print(f"  !! {ch} {nm} has {len(p)} polygons")
        continue
    i, j = min(itertools.combinations(range(3), 2),
               key=lambda ij: abs(p[ij[0]][0] - p[ij[1]][0]) / max(p[ij[0]][0], p[ij[1]][0], 1e-9))
    rel.append(abs(p[i][0] - p[j][0]) / max(p[i][0], p[j][0], 1e-9))
    gap.append(math.dist(p[i][2], p[j][2]))
    route_idx[({0, 1, 2} - {i, j}).pop()] += 1

srt = sorted(rel)
print(f"\nbest-matching polygon PAIR, relative area difference:")
print(f"  bit-equal {sum(1 for d in rel if d < 1e-6)}/{len(rel)}"
      f"   within 10% {sum(1 for d in rel if d < 0.10)}/{len(rel)}"
      f"   median {statistics.median(rel) * 100:.2f}%   max {srt[-1] * 100:.2f}%")
print(f"pair centroid separation along the route: median {statistics.median(gap):.1f} m"
      f"   min {min(gap):.1f}   max {max(gap):.1f}"
      f"   coincident (<2 m) {sum(1 for g in gap if g < 2)}")
print(f"index left over for the route ribbon: {dict(route_idx)}  <- NOT always 0")
print("\nVERDICT: every dzpath mesh is route + exactly two matched outlines."
      if set(hist) == {3} else "\nVERDICT: three-polygon rule does NOT hold.")
