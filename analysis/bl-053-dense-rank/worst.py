"""The largest cross-node pairs that resolve the WRONG way round today, with a camera pose.

Prints, per chapter, the biggest-area conflicting pairs whose loser is the node the original
draws later — the ones a capture can be aimed at. The pose is the overlap's own centroid with
the camera backed off along +Y, ready to paste into `--pos=` / `--lookat=`.

Usage:  python worst.py [CHAPTER ...] [--top 3]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dense_lib import *  # noqa: F401,F403
from hierarchy import intended_front, total_bias  # noqa: E402


def overlap_centroid(ch, sa, sb, xf_of):
    """Where the two surfaces actually overlap, area-weighted — the point a capture must be
    aimed at. A node's bounding-box centre is not it: these are map-sized tiles whose centres
    can be a kilometre from the contested patch."""
    def tris_of(s):
        return [tri for k, _r, tri in triangles_with_surface(ch, s[0], xf_of[s[0]])
                if k == s[1]]

    ta, tb = tris_of(sa), tris_of(sb)
    total = 0.0
    acc = [0.0, 0.0, 0.0]
    for a in ta:
        pl = plane_of(a)
        if pl is None:
            continue
        u, v = project_basis(pl[0])
        a2 = ensure_ccw(to2d(a, u, v))
        for b in tb:
            ov = clip_area(a2, ensure_ccw(to2d(b, u, v)))
            if ov <= MIN_OVERLAP:
                continue
            for i in range(3):
                acc[i] += ov * sum(p[i] for p in a) / 3.0
            total += ov
    if total == 0:
        return [sum(p[i] for p in ta[0]) / 3.0 for i in range(3)] if ta else [0, 0, 0]
    return [acc[i] / total for i in range(3)]


def run(chname, top):
    ch = Chapter(chname)
    nodes, _inactive, by_root = built_nodes_today(ch)
    parked = {i for r in parked_at_origin_roots(ch, by_root) for i, _ in by_root[r]}
    xf_of = dict(nodes)
    pairs, _ntri = coplanar_pairs(ch, nodes)
    bad = []
    for (a, b), surfs in pairs.items():
        if a in parked or b in parked:
            continue
        for (sa, sb), area in surfs.items():
            if not is_conflict(sa, sb):
                continue
            want = intended_front(sa, sb)
            got = total_bias(sb, sb[0] * NODE_ORDER_BIAS) - total_bias(sa, sa[0] * NODE_ORDER_BIAS)
            if got != 0 and (got > 0) != (want > 0):
                bad.append((area, sa, sb))
    bad.sort(key=lambda t: -t[0])
    print(f"== {chname} == {len(bad)} inverted conflicting surface pair(s)")
    for area, sa, sb in bad[:top]:
        c = overlap_centroid(ch, sa, sb, xf_of)
        print(f"  {area:12,.0f} m2  "
              f"{ch.nodes[sa[0]]['name']}#{sa[0]}/{ch.tex_name(sa[1][0])} rank {sa[2]} "
              f"vs {ch.nodes[sb[0]]['name']}#{sb[0]}/{ch.tex_name(sb[1][0])} rank {sb[2]} "
              f"(pri {sa[1][1]}, subface {sa[1][2]})")
        print(f"      --pos={c[0]:.1f},{c[1] + 250:.1f},{c[2]:.1f} "
              f"--lookat={c[0]:.1f},{c[1]:.1f},{c[2]:.1f}")


if __name__ == "__main__":
    top = 3
    args = []
    rest = sys.argv[1:]
    while rest:
        a = rest.pop(0)
        if a == "--top":
            top = int(rest.pop(0))
        elif not a.startswith("-"):
            args.append(a)
    for c in (args or CHAPTERS):
        run(c, top)
