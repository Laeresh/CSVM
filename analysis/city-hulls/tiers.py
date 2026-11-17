"""Measures a C5 building hull's tiers: its horizontal cross-section per height band.

A tower's box hides its setbacks, so the city models (CSVM/data/city_models) are built from these
measured sections, not from the box. The building's main mesh (the first part of its chain) is
sliced just above and just below every vertex level; each section is the set of cells, on the
grid of the slice's own x and z coordinates, that an even-odd ray test finds inside the slice's
outline, merged into rectangles. Between two levels a band is a prism (both sections equal) or a
slope (both one rectangle, linearly extrapolated to the levels; a rectangle of zero size is an
apex). The further meshes of the building (a cornice ring, a clock) are listed by box only.

Prints, per model, a PROFILE literal for the city spec: a list of (y, [(x0, x1, z0, z1), ...])
from the base up, in the gamez mesh's own frame (metres, Y up). Two entries at one height are a
flat roof between two footprints; two entries with one footprint are a vertical prism. Holds
measured numbers only.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/city-hulls/tiers.py cb00a cb01d cb04a ...
"""
import os
import sys

sys.dont_write_bytecode = True

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import census as C  # noqa: E402

EPS = 0.02      # slice offset from a vertex level (m)
SNAP = 0.01     # coordinate rounding (m)


def snap(v):
    return round(round(v / SNAP) * SNAP, 2) + 0.0


def mesh_tris(ch, node, part=0):
    mi, xf, _ = ch.parts(node)[part]
    m = ch.models[mi]
    verts = [C.E._apply(xf, (v["x"], v["y"], v["z"])) for v in m["vertices"]]
    out = []
    for p in m["polygons"]:
        vi = p.get("vertex_indices") or []
        idx = [(i, i + 1, i + 2) for i in range(len(vi) - 2)] if p.get("flags", {}).get("tri_strip") \
            else [(0, i, i + 1) for i in range(1, len(vi) - 1)]
        out += [[verts[vi[a]], verts[vi[b]], verts[vi[c]]] for a, b, c in idx]
    return out


def slice_segments(tris, h):
    """The outline's segments at height h, a wall drawn on both sides counted once."""
    segs, seen = [], set()
    for t in tris:
        d = [p[1] - h for p in t]
        pts = []
        for i in range(3):
            a, b = t[i], t[(i + 1) % 3]
            da, db = d[i], d[(i + 1) % 3]
            if (da < 0) != (db < 0):
                k = da / (da - db)
                pts.append((a[0] + k * (b[0] - a[0]), a[2] + k * (b[2] - a[2])))
        key = tuple(sorted((snap(p[0]), snap(p[1])) for p in pts))
        if len(pts) == 2 and key[0] != key[1] and key not in seen:
            seen.add(key)
            segs.append(pts)
    return segs


def section(tris, h):
    """The cross-section at height h as rectangles (x0, x1, z0, z1) on the slice's own grid."""
    segs = slice_segments(tris, h)
    if not segs:
        return []
    xs = sorted({snap(p[0]) for s in segs for p in s})
    zs = sorted({snap(p[1]) for s in segs for p in s})
    occ = [[False] * (len(zs) - 1) for _ in range(len(xs) - 1)]
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            # Distinct crossings: a wall drawn twice with other diagonals gives overlapping segments.
            hits = set()
            for (ax, az), (bx, bz) in segs:
                if (az > cz) != (bz > cz):
                    x = ax + (cz - az) * (bx - ax) / (bz - az)
                    if x > cx:
                        hits.add(snap(x))
            occ[i][j] = len(hits) % 2 == 1
    return merge(occ, xs, zs)


def merge(occ, xs, zs):
    """Greedy rectangles over an occupancy grid: runs along z, then grown along x."""
    used = [[False] * len(r) for r in occ]
    rects = []
    for i in range(len(occ)):
        for j in range(len(occ[i])):
            if not occ[i][j] or used[i][j]:
                continue
            j1 = j
            while j1 + 1 < len(occ[i]) and occ[i][j1 + 1] and not used[i][j1 + 1]:
                j1 += 1
            i1 = i
            while i1 + 1 < len(occ) and all(occ[i1 + 1][k] and not used[i1 + 1][k] for k in range(j, j1 + 1)):
                i1 += 1
            for a in range(i, i1 + 1):
                for b in range(j, j1 + 1):
                    used[a][b] = True
            rects.append((xs[i], xs[i1 + 1], zs[j], zs[j1 + 1]))
    return sorted(rects)


def same(a, b, tol=0.05):
    return len(a) == len(b) and all(max(abs(p - q) for p, q in zip(r, s)) <= tol for r, s in zip(a, b))


def profile(tris):
    levels = sorted({snap(p[1]) for t in tris for p in t})
    prof = []
    for a, b in zip(levels, levels[1:]):
        if b - a < 3 * EPS:
            continue
        lo, hi = section(tris, a + EPS), section(tris, b - EPS)
        if same(lo, hi):
            ends = [(a, lo), (b, lo)]
        elif len(lo) == 1 and len(hi) == 1:
            # A slope: extrapolate each edge from the two slices to the levels themselves.
            k = (b - a) / (b - a - 2 * EPS)
            r0, r1 = lo[0], hi[0]
            mid = [(p + q) / 2 for p, q in zip(r0, r1)]
            ends = [(a, [tuple(snap(m + (p - m) * k) for p, m in zip(r0, mid))]),
                    (b, [tuple(snap(m + (q - m) * k) for q, m in zip(r1, mid))])]
        else:
            raise SystemExit("band %.2f..%.2f changes a multi-rectangle section; not a prism or a slope" % (a, b))
        for y, s in ends:
            if prof and prof[-1][0] == y and same(prof[-1][1], s):
                continue
            prof.append((y, s))
    return prof


def fmt(prof):
    rows = ["    (%g, [%s])," % (y, ", ".join("(%g, %g, %g, %g)" % r for r in s)) for y, s in prof]
    return "[\n" + "\n".join(rows) + "\n]"


def main():
    ch = C.Chapter()
    for m in sys.argv[1:]:
        node = next(i for i in ch.by_name[m + ".flt"] if ch.parts(i))
        parts = ch.parts(node)
        tris = mesh_tris(ch, node, 0)
        print("# %s: main mesh %d triangles, %d further mesh(es)" % (m, len(tris), len(parts) - 1))
        for k in range(1, len(parts)):
            pt = mesh_tris(ch, node, k)
            lo, hi = C.bbox([(t, None, None) for t in pt])
            print("#   further mesh %d (%s): %d triangles, x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (
                k, ch.nodes[parts[k][2]].name, len(pt), lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        print('"%s": %s' % (m, fmt(profile(tris))))


if __name__ == "__main__":
    main()
