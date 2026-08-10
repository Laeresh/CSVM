r"""Is C5's city TWO COPLANAR LAYERS, and does the `no_clutter` flag select between them?
(BL-305 / PLAN-clutter-uv-placement — the cross-texture pairing `flag_geometry.py` never tested.)

The user flew C5 with `--debug-clutterflag` and `--clutter-templates=` and reported:

    "on the red areas cblock4, 5 and 6 are active, and on green cblock1, 2, 3 and 7"

i.e. wherever the visible ground is FLAGGED `no_clutter` (red), it is the `cblock4/5/6` templates
that produce clutter; wherever it is CLEAR (green), the `cblock1/2/3/7` templates do.

The hypothesis that makes that a mechanism rather than a coincidence:

    C5's city is two coplanar ground layers. A `cblock1/2/3/7` OVERLAY is drawn on top of a
    `cblock4/5/6` BASE (`analysis/item9-depth-bias/CBLOCK-LOD.md` measured that coverage at
    97-100 % of the base). The original's clutter walk (`FUN_004de2c0`) skips a polygon flagged
    `0x800`. So where the OVERLAY carries the flag, the original skips it and stamps on the
    UNFLAGGED BASE underneath instead — producing that district's low-rise `cb12a`+ buildings.
    Where the overlay is clear, the overlay stamps and you get the 59-108 m towers.

`FINDINGS-noclutter.md` measured C5's flagged polygons as overwhelmingly `cblock1/2/3/7`-textured
(only 0.2 % `cblock4`), and `FINDINGS-flag-geometry.md` compared flagged against unflagged WITHIN
one texture and found a side-by-side tiling with dY = 0. Neither contradicts the hypothesis: both
looked at one texture at a time, and the hypothesis is entirely CROSS-texture. This script tests it.

What it measures, for C5:

  1. For every FLAGGED overlay (`cblock1/2/3/7`) polygon: is there an UNFLAGGED base
     (`cblock4/5/6`) polygon coplanar with it? Reported as the TRUE XZ polygon-intersection area
     as a share of the overlay's own area, plus the signed Y difference (base minus overlay).
  2. The converse, for every CLEAR overlay polygon. The hypothesis predicts this is much rarer —
     if the base were everywhere, both districts would stamp everywhere and the correlation the
     user sees could not appear.
  3. **The 2x2 contingency table by area** over the whole `cblock` field:
     {overlay flagged, overlay clear} x {base beneath, no base}. That single table is the result.
  4. A 512 m grid map coloured by WHICH DISTRICT WOULD STAMP under the original's rule, to be
     compared against `flag_geometry.py`'s flag map of the same field.
  5. `pier`-textured and bridge-named geometry, for the user's "north of the bridge" report.

TRUE POLYGON OVERLAP, NOT BOUNDING BOXES. `flag_geometry.py` used AABBs and its own write-up
flags that as a superset; here a false positive would confirm the hypothesis wrongly, so every
polygon is triangulated exactly as `SceneBuilder.EmitPolygon` does (fan, or strip when
`tri_strip` is set — a strip's raw index list is NOT an outline), projected to XZ, and clipped
triangle-against-triangle with Sutherland-Hodgman. Bounding boxes are used only as a broad-phase
filter before the exact test.

`ClutterBuilder.BuriedClutterDistricts` exempts `cblock4/5/6` in OUR build. It must not filter
this analysis: the question is what the ORIGINAL would do, and the original registers all seven.

No engine run; static analysis over `extracted/<C>/gamez/{nodes,models,materials,textures}.json`
only. Exit code: 0 = ran clean, 2 = a self-check failed or the extraction is missing.

The world walk mirrors `ClutterBuilder.PlaceOnWorld` (`CSVM/src/Mech3/Clutter.cs`) field for
field and is lifted from `flag_geometry.py` in this directory rather than reinvented: `world1`'s
`child_indices` plus its partition grid's referenced node indices, each subtree from identity;
`WorldBuilder.SkipWorldNode` (`horizon`/`dzpaths`/`fvol*`) and non-nearest `Lod` levels skipped,
subtree included; the `active` flag NOT checked; each node's local transform composed before
recursing; WORLD space throughout (WORLD-15).

METHOD-9: `--self-check` runs first, always. It includes a pair whose BOUNDING BOXES coincide
exactly while the polygons do not intersect at all — the exact case an AABB instrument would
report as a confirmation of the hypothesis.

Run from the repo root. This worktree has no `extracted/` (git-ignored), so pass the main copy:

    python analysis/bl-305-clutter-uv/layer_pairing.py --extracted Z:/CSVM/extracted
"""
import argparse
import json
import math
import os
import sys

OVERLAY_TEXTURES = ["cblock1", "cblock2", "cblock3", "cblock7"]
BASE_TEXTURES = ["cblock4", "cblock5", "cblock6"]

# BL-305's debug pose: --pos=-9490,230,-3300 looking straight down.
POSE_X, POSE_Z = -9490.0, -3300.0

EPS_AREA = 1e-6


# ---------------------------------------------------------------- data model (from flag_geometry)

class Mesh:
    __slots__ = ("vertices", "polygons")

    def __init__(self):
        self.vertices = []
        self.polygons = []


class Polygon:
    __slots__ = ("vertex_indices", "tri_strip", "layers", "unk3", "priority")

    def __init__(self):
        self.vertex_indices = []
        self.tri_strip = False
        self.layers = []
        self.unk3 = False
        self.priority = None


class Node:
    __slots__ = ("name", "kind", "model_index", "children", "lod_range_min", "local",
                 "partition_roots")

    def __init__(self):
        self.name = ""
        self.kind = ""
        self.model_index = -1
        self.children = []
        self.lod_range_min = -1.0
        self.local = None
        self.partition_roots = None


class Transform:
    """basis: 3 rows (row_k . v == world component k); translate: (x, y, z)."""
    __slots__ = ("basis", "translate")

    IDENTITY_BASIS = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))

    def __init__(self, basis, translate):
        self.basis = basis
        self.translate = translate

    def xform(self, v):
        b, t = self.basis, self.translate
        return (
            b[0][0] * v[0] + b[0][1] * v[1] + b[0][2] * v[2] + t[0],
            b[1][0] * v[0] + b[1][1] * v[1] + b[1][2] * v[2] + t[1],
            b[2][0] * v[0] + b[2][1] * v[1] + b[2][2] * v[2] + t[2],
        )

    def basis_xform(self, v):
        b = self.basis
        return (
            b[0][0] * v[0] + b[0][1] * v[1] + b[0][2] * v[2],
            b[1][0] * v[0] + b[1][1] * v[1] + b[1][2] * v[2],
            b[2][0] * v[0] + b[2][1] * v[1] + b[2][2] * v[2],
        )

    def compose(self, local):
        """self * local: local applied first — Clutter.cs's `xf *= local;`."""
        new_basis = [[0.0, 0.0, 0.0] for _ in range(3)]
        for col in range(3):
            col_vec = (local.basis[0][col], local.basis[1][col], local.basis[2][col])
            out = self.basis_xform(col_vec)
            for row in range(3):
                new_basis[row][col] = out[row]
        return Transform(tuple(tuple(r) for r in new_basis), self.xform(local.translate))


IDENTITY = Transform(Transform.IDENTITY_BASIS, (0.0, 0.0, 0.0))


# ---------------------------------------------------------------- loading (mirrors GameZ.cs)

def _vec3(d):
    return (d["x"], d["y"], d["z"])


def _parse_transform(tf):
    tr = _vec3(tf["translation"] if "translation" in tf else tf["translate"])
    m = tf.get("matrix") or tf.get("original")
    if isinstance(m, dict):
        def M(legacy, unified):
            return float(m[legacy] if legacy in m else m[unified])
        a, b, c = M("a", "r00"), M("b", "r01"), M("c", "r02")
        d, e, f = M("d", "r10"), M("e", "r11"), M("f", "r12")
        g, h, i = M("g", "r20"), M("h", "r21"), M("i", "r22")
        basis = ((a, d, g), (b, e, h), (c, f, i))
    else:
        rot = tf["rotation"] if "rotation" in tf else tf["rotate"]
        rx, ry, rz = _vec3(rot)
        cx, sx = math.cos(rx), math.sin(rx)
        cy, sy = math.cos(ry), math.sin(ry)
        cz, sz = math.cos(rz), math.sin(rz)
        rx_m = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
        ry_m = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
        rz_m = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))

        def matmul(p, q):
            return tuple(tuple(sum(p[r][k] * q[k][c] for k in range(3)) for c in range(3))
                         for r in range(3))
        basis = matmul(matmul(ry_m, rx_m), rz_m)
    return Transform(basis, tuple(float(x) for x in tr))


def _base_texture(name):
    if not name:
        return None
    low = name.lower()
    return name[:-4] if low.endswith(".tif") else name


def _first_property(d):
    for k, v in d.items():
        return k, v
    raise ValueError("empty enum wrapper object")


def load_chapter(extracted, chapter):
    base = os.path.join(extracted, chapter, "gamez")
    with open(os.path.join(base, "nodes.json"), encoding="utf-8") as f:
        raw_nodes = json.load(f)
    with open(os.path.join(base, "models.json"), encoding="utf-8") as f:
        raw_models = json.load(f)
    with open(os.path.join(base, "textures.json"), encoding="utf-8") as f:
        raw_textures = json.load(f)
    with open(os.path.join(base, "materials.json"), encoding="utf-8") as f:
        raw_materials = json.load(f)

    texture_names = [_base_texture(t.get("name") or t.get("original")) for t in raw_textures]
    materials = []
    for wrapper in raw_materials:
        if "Textured" in wrapper:
            ti = wrapper["Textured"].get("texture_index")
            materials.append(texture_names[ti] if ti is not None and 0 <= ti < len(texture_names)
                             else None)
        else:
            materials.append(None)

    meshes = []
    for m in raw_models:
        if not isinstance(m, dict):
            meshes.append(None)
            continue
        mesh = Mesh()
        mesh.vertices = [_vec3(v) for v in m.get("vertices", [])]
        for p in m.get("polygons", []):
            poly = Polygon()
            poly.vertex_indices = list(p.get("vertex_indices", []))
            flags = p.get("flags", {})
            poly.tri_strip = bool(flags.get("triangle_strip") or flags.get("tri_strip"))
            poly.unk3 = bool(flags.get("unk3"))
            poly.priority = p.get("priority")
            for layer in p.get("materials") or []:
                mi = layer.get("material_index", -1)
                poly.layers.append(mi)
            mesh.polygons.append(poly)
        meshes.append(mesh)

    nodes = []
    for i, wrapper in enumerate(raw_nodes):
        data = wrapper.get("data")
        if not isinstance(data, dict):
            raise ValueError(f"{chapter} node {i}: not the unified extraction shape")
        kind, body = _first_property(data)
        n = Node()
        n.kind = kind
        n.name = wrapper.get("name", "") or ""
        mi = wrapper.get("model_index", -1)
        n.model_index = mi if mi is not None else -1
        n.children = list(wrapper.get("child_indices") or [])
        if kind == "Lod":
            n.lod_range_min = body.get("range", {}).get("min", -1.0)
        if kind == "World":
            roots, seen = [], set()
            for row in body.get("partitions") or []:
                for cell in row:
                    for ref in (cell.get("values") or cell.get("nodes") or []):
                        idx = ref.get("node_index", ref.get("index"))
                        if idx is not None and idx not in seen:
                            seen.add(idx)
                            roots.append(idx)
                    for idx in cell.get("node_indices") or []:
                        if idx not in seen:
                            seen.add(idx)
                            roots.append(idx)
            n.partition_roots = roots
        tf = body.get("transformation")
        if isinstance(tf, dict):
            n.local = _parse_transform(tf)
        else:
            tf2 = body.get("transform")
            if isinstance(tf2, dict):
                _, inner = _first_property(tf2)
                n.local = _parse_transform(inner)
        nodes.append(n)

    return nodes, meshes, materials


def skip_world_node(node):
    n = node.name.lower()
    return n == "horizon" or n == "dzpaths" or n.startswith("fvol")


# ---------------------------------------------------------------- exact XZ intersection

def tri_area2(t):
    (x0, z0), (x1, z1), (x2, z2) = t
    return abs((x1 - x0) * (z2 - z0) - (x2 - x0) * (z1 - z0)) * 0.5


def _ccw(t):
    (x0, z0), (x1, z1), (x2, z2) = t
    return t if (x1 - x0) * (z2 - z0) - (x2 - x0) * (z1 - z0) >= 0 else [t[0], t[2], t[1]]


def clip_area(subject, clipper):
    """Sutherland-Hodgman: area of subject INTERSECT clipper. Clipper must be convex and CCW."""
    out = list(subject)
    m = len(clipper)
    for i in range(m):
        if not out:
            return 0.0
        cx1, cy1 = clipper[i]
        cx2, cy2 = clipper[(i + 1) % m]
        ex, ey = cx2 - cx1, cy2 - cy1
        inp = out
        out = []
        for j in range(len(inp)):
            cur = inp[j]
            prv = inp[j - 1]
            sc = ex * (cur[1] - cy1) - ey * (cur[0] - cx1)
            sp = ex * (prv[1] - cy1) - ey * (prv[0] - cx1)
            if sc >= 0:
                if sp < 0:
                    t = sp / (sp - sc)
                    out.append((prv[0] + t * (cur[0] - prv[0]), prv[1] + t * (cur[1] - prv[1])))
                out.append(cur)
            elif sp >= 0:
                t = sp / (sp - sc)
                out.append((prv[0] + t * (cur[0] - prv[0]), prv[1] + t * (cur[1] - prv[1])))
    if len(out) < 3:
        return 0.0
    s = 0.0
    for i in range(len(out)):
        x1, y1 = out[i]
        x2, y2 = out[(i + 1) % len(out)]
        s += x1 * y2 - x2 * y1
    return abs(s) * 0.5


def tris_overlap_area(a_tris, b_tris):
    """True XZ intersection area of two triangulated footprints."""
    total = 0.0
    for bt in b_tris:
        bt = _ccw(bt)
        bx0 = min(p[0] for p in bt)
        bx1 = max(p[0] for p in bt)
        bz0 = min(p[1] for p in bt)
        bz1 = max(p[1] for p in bt)
        for at in a_tris:
            if (max(p[0] for p in at) < bx0 or min(p[0] for p in at) > bx1
                    or max(p[1] for p in at) < bz0 or min(p[1] for p in at) > bz1):
                continue
            total += clip_area(_ccw(at), bt)
    return total


# ---------------------------------------------------------------- the record

class PolyRec:
    """One world polygon of a target texture, measured in WORLD space, with its XZ footprint
    triangulated exactly as SceneBuilder.EmitPolygon triangulates it."""
    __slots__ = ("texture", "role", "flagged", "node", "node_index", "poly_index", "priority",
                 "tris", "x0", "x1", "z0", "z1", "cx", "cz", "y_mean", "xz_area", "nverts",
                 "strip")

    def label(self):
        return f"{self.node}[{self.node_index}]#{self.poly_index}"


def polygon_records(nodes, meshes, materials, wanted):
    """Walks world1 as ClutterBuilder.PlaceOnWorld does. `wanted` maps texture -> role."""
    recs = []
    diag = {"visited_polys": 0, "dup_skipped": 0, "too_few_verts": 0, "bad_vertex_index": 0,
            "zero_xz_area": 0, "nan": 0, "strips": 0, "degenerate_tris": 0, "multi_layer_hit": 0}
    world_idx = next((i for i, n in enumerate(nodes)
                      if n.kind == "World" and n.name.lower() == "world1"), None)
    if world_idx is None:
        return recs, diag
    world = nodes[world_idx]
    seen_polys = set()

    def process(node_index, mesh, xf):
        for pi, poly in enumerate(mesh.polygons):
            hits = []
            for mi in poly.layers:
                tex = materials[mi] if 0 <= mi < len(materials) else None
                if tex is not None and tex in wanted:
                    hits.append(tex)
            if not hits:
                continue
            key = (node_index, pi)
            if key in seen_polys:
                diag["dup_skipped"] += len(hits)
                continue
            seen_polys.add(key)
            diag["visited_polys"] += 1
            if len(hits) > 1:
                diag["multi_layer_hit"] += 1
            n = len(poly.vertex_indices)
            if n < 3:
                diag["too_few_verts"] += 1
                continue
            pts = []
            bad = False
            for vi in poly.vertex_indices:
                if not (0 <= vi < len(mesh.vertices)):
                    bad = True
                    break
                pts.append(xf.xform(mesh.vertices[vi]))
            if bad:
                diag["bad_vertex_index"] += 1
                continue
            if any(not all(map(math.isfinite, p)) for p in pts):
                diag["nan"] += 1
                continue
            if poly.tri_strip:
                diag["strips"] += 1
                idx_tris = [(i, i + 1, i + 2) if (i & 1) == 0 else (i, i + 2, i + 1)
                            for i in range(n - 2)]
            else:
                idx_tris = [(0, i, i + 1) for i in range(1, n - 1)]
            tris, area = [], 0.0
            for (a, b, c) in idx_tris:
                t = [(pts[a][0], pts[a][2]), (pts[b][0], pts[b][2]), (pts[c][0], pts[c][2])]
                ta = tri_area2(t)
                if ta < EPS_AREA:
                    diag["degenerate_tris"] += 1
                    continue
                tris.append(t)
                area += ta
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            zs = [p[2] for p in pts]
            if area < EPS_AREA:
                diag["zero_xz_area"] += 1
            for tex in hits:
                r = PolyRec()
                r.texture = tex
                r.role = wanted[tex]
                r.flagged = poly.unk3
                r.node = nodes[node_index].name
                r.node_index = node_index
                r.poly_index = pi
                r.priority = poly.priority
                r.tris = tris
                r.x0, r.x1 = min(xs), max(xs)
                r.z0, r.z1 = min(zs), max(zs)
                r.cx = sum(xs) / n
                r.cz = sum(zs) / n
                r.y_mean = sum(ys) / n
                r.xz_area = area
                r.nverts = n
                r.strip = poly.tri_strip
                recs.append(r)

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes):
            return
        node = nodes[idx]
        if skip_world_node(node) or (node.kind == "Lod" and node.lod_range_min != 0.0):
            return
        if node.local is not None:
            xf = xf.compose(node.local)
        if 0 <= node.model_index < len(meshes) and meshes[node.model_index] is not None:
            process(idx, meshes[node.model_index], xf)
        for c in node.children:
            walk(c, xf)

    for c in world.children:
        walk(c, IDENTITY)
    for p in (world.partition_roots or []):
        walk(p, IDENTITY)
    return recs, diag


class Grid:
    """Uniform XZ bucket index (broad phase only — every reported area is an exact clip)."""

    def __init__(self, recs, cell=512.0):
        self.cell = cell
        self.buckets = {}
        for r in recs:
            for key in self._keys(r.x0, r.x1, r.z0, r.z1):
                self.buckets.setdefault(key, []).append(r)

    def _keys(self, x0, x1, z0, z1):
        c = self.cell
        for i in range(int(math.floor(x0 / c)), int(math.floor(x1 / c)) + 1):
            for j in range(int(math.floor(z0 / c)), int(math.floor(z1 / c)) + 1):
                yield (i, j)

    def near(self, r):
        out, seen = [], set()
        for key in self._keys(r.x0, r.x1, r.z0, r.z1):
            for c in self.buckets.get(key, ()):
                k = id(c)
                if k not in seen:
                    seen.add(k)
                    out.append(c)
        return out


# ---------------------------------------------------------------- the pairing measurement

class Pairing:
    __slots__ = ("rec", "covered", "best", "best_area", "dy_weighted", "partners")

    def __init__(self, rec):
        self.rec = rec
        self.covered = 0.0     # exact XZ area of `rec` covered by base polygons (capped at own)
        self.best = None       # the single largest-overlap base partner
        self.best_area = 0.0
        self.dy_weighted = None  # area-weighted mean (base Y - overlay Y)
        self.partners = 0

    @property
    def coverage(self):
        if self.rec.xz_area < EPS_AREA:
            return float("nan")
        return self.covered / self.rec.xz_area


def pair_against_base(overlays, base_recs, grid):
    """For every overlay record, the exact XZ area covered by base polygons beneath it."""
    out = []
    for r in overlays:
        p = Pairing(r)
        num_dy, den_dy = 0.0, 0.0
        for b in grid.near(r):
            if r.x1 < b.x0 or b.x1 < r.x0 or r.z1 < b.z0 or b.z1 < r.z0:
                continue
            a = tris_overlap_area(r.tris, b.tris)
            if a <= EPS_AREA:
                continue
            p.partners += 1
            p.covered += a
            num_dy += (b.y_mean - r.y_mean) * a
            den_dy += a
            if a > p.best_area:
                p.best, p.best_area = b, a
        if p.covered > r.xz_area:
            p.covered = r.xz_area   # bases can abut/overlap; never claim more than the overlay is
        if den_dy > 0:
            p.dy_weighted = num_dy / den_dy
        out.append(p)
    return out


# ---------------------------------------------------------------- self-verification (METHOD-9)

def _synthetic(tris, y, flagged, texture="synthetic", role="overlay"):
    r = PolyRec()
    r.texture = texture
    r.role = role
    r.flagged = flagged
    r.node = "synthetic"
    r.node_index = -1
    r.poly_index = -1
    r.priority = None
    r.tris = tris
    xs = [p[0] for t in tris for p in t]
    zs = [p[1] for t in tris for p in t]
    r.x0, r.x1 = min(xs), max(xs)
    r.z0, r.z1 = min(zs), max(zs)
    r.cx, r.cz = (r.x0 + r.x1) / 2.0, (r.z0 + r.z1) / 2.0
    r.y_mean = y
    r.xz_area = sum(tri_area2(t) for t in tris)
    r.nverts = 4
    r.strip = False
    return r


def _quad(x0, x1, z0, z1):
    a, b, c, d = (x0, z0), (x1, z0), (x1, z1), (x0, z1)
    return [[a, b, c], [a, c, d]]


def run_self_checks():
    msgs = []
    ok = True

    def check(label, passed, detail):
        nonlocal ok
        ok = ok and passed
        msgs.append(f"{label}: {detail} ({'OK' if passed else 'FAIL'})")

    # 1. Coincident overlay/base, base 1 m below. Must pair at 100 % with dY = -1.
    ov = _synthetic(_quad(0, 100, 0, 100), 10.0, True)
    ba = _synthetic(_quad(0, 100, 0, 100), 9.0, False, "base", "base")
    p = pair_against_base([ov], [ba], Grid([ba]))[0]
    check("coincident pair, base 1 m below",
          abs(p.coverage - 1.0) < 1e-9 and abs(p.dy_weighted + 1.0) < 1e-9,
          f"coverage={p.coverage:.4f} dY={p.dy_weighted:+.3f}")

    # 2. Disjoint. Must NOT pair — without this the script confirms the hypothesis from any input.
    far = _synthetic(_quad(5000, 5100, 5000, 5100), 9.0, False, "base", "base")
    p = pair_against_base([ov], [far], Grid([far]))[0]
    check("deliberately disjoint pair", p.covered == 0.0 and p.best is None,
          f"coverage={p.coverage:.4f} partners={p.partners}")

    # 3. Edge-touching (city blocks side by side). Must read as zero, not as a stack.
    touch = _synthetic(_quad(100, 200, 0, 100), 9.0, False, "base", "base")
    p = pair_against_base([ov], [touch], Grid([touch]))[0]
    check("edge-touching pair", p.covered <= EPS_AREA, f"coverage={p.coverage:.6f}")

    # 4. THE AABB TRAP. Two right triangles on opposite corners of the same square: identical
    #    bounding boxes, zero true intersection. An AABB instrument reports 100 % coverage here
    #    and would confirm the hypothesis from a tiling.
    tri_a = _synthetic([[(0, 0), (100, 0), (0, 100)]], 10.0, True)
    tri_b = _synthetic([[(100, 100), (100, 0.001), (0.001, 100)]], 9.0, False, "base", "base")
    p = pair_against_base([tri_a], [tri_b], Grid([tri_b]))[0]
    bbox_says = not (tri_a.x1 < tri_b.x0 or tri_b.x1 < tri_a.x0
                     or tri_a.z1 < tri_b.z0 or tri_b.z1 < tri_a.z0)
    check("AABB-coincident but polygon-disjoint pair",
          bbox_says and p.covered <= 1.0,
          f"bbox overlaps={bbox_says}, true coverage={p.coverage:.6f} (an AABB test would say 1.0)")

    # 5. Half-covered overlay: a base spanning exactly the left half. Known answer 0.5.
    half = _synthetic(_quad(0, 50, 0, 100), 9.0, False, "base", "base")
    p = pair_against_base([ov], [half], Grid([half]))[0]
    check("base covering exactly half", abs(p.coverage - 0.5) < 1e-9,
          f"coverage={p.coverage:.6f}")

    # 6. A 45-degree diamond inscribed in the overlay square: known area 5000 of 10000.
    dia = _synthetic([[(50, 0), (100, 50), (0, 50)], [(100, 50), (50, 100), (0, 50)]],
                     9.0, False, "base", "base")
    p = pair_against_base([ov], [dia], Grid([dia]))[0]
    check("rotated diamond inscribed in the overlay", abs(p.coverage - 0.5) < 1e-6,
          f"coverage={p.coverage:.6f} (exact answer 0.5)")

    # 7. Two abutting bases each covering half must not sum past 1.0.
    right = _synthetic(_quad(50, 150, 0, 100), 9.0, False, "base", "base")
    p = pair_against_base([ov], [half, right], Grid([half, right]))[0]
    check("two bases, no double counting", abs(p.coverage - 1.0) < 1e-9,
          f"coverage={p.coverage:.6f} partners={p.partners}")

    return ok, msgs


# ---------------------------------------------------------------- reporting

def percentiles(vals, ps):
    if not vals:
        return [None] * len(ps)
    s = sorted(vals)
    out = []
    for p in ps:
        k = min(len(s) - 1, max(0, int(round((p / 100.0) * (len(s) - 1)))))
        out.append(s[k])
    return out


def contingency(pairings, out, label, threshold):
    """The 2x2 table. Reported two ways: EXACT AREA (the covered area is itself the cell value,
    so the four cells partition the overlay area with no threshold at all) and POLYGON COUNTS at
    a coverage threshold."""
    fa_cov = fa_bare = ca_cov = ca_bare = 0.0
    fn_cov = fn_bare = cn_cov = cn_bare = 0
    for p in pairings:
        r = p.rec
        cov = p.covered
        bare = max(0.0, r.xz_area - cov)
        hit = (r.xz_area > EPS_AREA and cov / r.xz_area >= threshold)
        if r.flagged:
            fa_cov += cov
            fa_bare += bare
            fn_cov += 1 if hit else 0
            fn_bare += 0 if hit else 1
        else:
            ca_cov += cov
            ca_bare += bare
            cn_cov += 1 if hit else 0
            cn_bare += 0 if hit else 1
    total = fa_cov + fa_bare + ca_cov + ca_bare
    out.append(f"-- {label} — 2x2 contingency by EXACT XZ AREA (m^2) --")
    out.append("                            base cblock4/5/6 beneath        no base beneath"
               "        row total")
    for name, cov, bare in (("overlay FLAGGED no_clutter", fa_cov, fa_bare),
                            ("overlay CLEAR             ", ca_cov, ca_bare)):
        rt = cov + bare
        out.append(f"  {name}  {cov:15,.0f} ({100.0 * cov / rt if rt else 0:5.1f}%) "
                   f"{bare:15,.0f} ({100.0 * bare / rt if rt else 0:5.1f}%) {rt:15,.0f}")
    out.append(f"  {'column total              ':26s}  {fa_cov + ca_cov:15,.0f}"
               f"          {fa_bare + ca_bare:15,.0f}          {total:15,.0f}")
    out.append("")
    out.append(f"-- same table, POLYGON COUNTS at coverage >= {threshold:.0%} --")
    out.append("                            base beneath   no base   row total   % with base")
    for name, cov, bare in (("overlay FLAGGED no_clutter", fn_cov, fn_bare),
                            ("overlay CLEAR             ", cn_cov, cn_bare)):
        rt = cov + bare
        out.append(f"  {name}  {cov:12d}  {bare:8d}  {rt:10d}   "
                   f"{100.0 * cov / rt if rt else 0:6.1f}%")
    # Odds ratio: how much likelier a flagged overlay is to have a base under it.
    if fn_bare and cn_cov:
        orat = (fn_cov * cn_bare) / float(fn_bare * cn_cov)
        out.append(f"  odds ratio (flagged has base : clear has base) = {orat:.1f}x")
    elif cn_cov == 0 and fn_cov > 0:
        out.append("  odds ratio = infinite (no CLEAR overlay polygon has a base beneath it)")
    out.append("")
    return (fa_cov, fa_bare, ca_cov, ca_bare), (fn_cov, fn_bare, cn_cov, cn_bare)


def report_texture(tex, pairings, out, threshold):
    sub = [p for p in pairings if p.rec.texture == tex]
    fl = [p for p in sub if p.rec.flagged]
    cl = [p for p in sub if not p.rec.flagged]
    out.append(f"== {tex} ==")
    for name, group in (("FLAGGED", fl), ("CLEAR  ", cl)):
        if not group:
            out.append(f"  {name}: none")
            continue
        area = sum(p.rec.xz_area for p in group)
        cov = sum(p.covered for p in group)
        hits = [p for p in group if p.rec.xz_area > EPS_AREA
                and p.covered / p.rec.xz_area >= threshold]
        covs = [p.coverage for p in group if p.coverage == p.coverage]
        pc = percentiles(covs, [5, 25, 50, 75, 95])
        dys = [p.dy_weighted for p in group if p.dy_weighted is not None]
        out.append(f"  {name}: n={len(group):5d}  area={area:13,.0f}  covered by base="
                   f"{cov:13,.0f} = {100.0 * cov / area if area else 0:5.1f}%   "
                   f">= {threshold:.0%}: {len(hits):5d} = "
                   f"{100.0 * len(hits) / len(group):5.1f}%")
        out.append(f"           coverage percentiles p5={100 * pc[0]:5.1f}% p25={100 * pc[1]:5.1f}%"
                   f" p50={100 * pc[2]:5.1f}% p75={100 * pc[3]:5.1f}% p95={100 * pc[4]:5.1f}%")
        if dys:
            d = percentiles(dys, [5, 50, 95])
            below = sum(1 for v in dys if v < -0.001)
            same = sum(1 for v in dys if abs(v) <= 0.001)
            above = len(dys) - below - same
            out.append(f"           signed dY (base - overlay), n={len(dys)}: "
                       f"min={min(dys):+.3f} p5={d[0]:+.3f} median={d[1]:+.3f} p95={d[2]:+.3f} "
                       f"max={max(dys):+.3f}   below={below} same={same} above={above}")
            # Which base textures pair with this overlay?
            by_base = {}
            for p in group:
                for_b = p.best
                if for_b is not None:
                    by_base[for_b.texture] = by_base.get(for_b.texture, 0) + 1
            out.append("           best partner's texture: "
                       + ", ".join(f"{k}={v}" for k, v in sorted(by_base.items())))
    out.append("")


def report_stamp_map(overlay_pairings, base_recs, cell, out, threshold):
    """The map the task asks for: each 512 m cell coloured by WHICH DISTRICT WOULD STAMP there
    under the original's rule.

      T = overlay CLEAR                        -> the cblock1/2/3/7 templates stamp (tall towers)
      L = overlay FLAGGED with base beneath    -> the walk skips the overlay, the cblock4/5/6
                                                  base underneath stamps (low-rise district)
      x = overlay FLAGGED with NO base beneath -> nothing stamps at all
      b = base only, no overlay above it       -> the base stamps, uncontested

    Area is distributed across the cells a polygon's bounding box covers, in proportion to the
    bbox/cell intersection — the same accounting `flag_geometry.py` uses, so the two maps are
    directly comparable. The letter is the category with the most area in the cell; lower case
    marks a cell where that winner holds less than 60 % of the cell's area (i.e. mixed)."""
    cats = {}   # (i, j) -> [T, L, x, b]

    def deposit(r, slot_index, area):
        bw = max(r.x1 - r.x0, 1e-9)
        bh = max(r.z1 - r.z0, 1e-9)
        for i in range(int(math.floor(r.x0 / cell)), int(math.floor(r.x1 / cell)) + 1):
            for j in range(int(math.floor(r.z0 / cell)), int(math.floor(r.z1 / cell)) + 1):
                ox = min(r.x1, (i + 1) * cell) - max(r.x0, i * cell)
                oz = min(r.z1, (j + 1) * cell) - max(r.z0, j * cell)
                if ox <= 0 or oz <= 0:
                    continue
                cats.setdefault((i, j), [0.0, 0.0, 0.0, 0.0])[slot_index] += \
                    area * (ox * oz) / (bw * bh)

    for p in overlay_pairings:
        r = p.rec
        if not r.flagged:
            deposit(r, 0, r.xz_area)
        else:
            covered = min(p.covered, r.xz_area)
            deposit(r, 1, covered)
            deposit(r, 2, r.xz_area - covered)
    # Base area NOT under any overlay: the base stamps there with nothing to skip.
    ov_grid = Grid([p.rec for p in overlay_pairings])
    exposed_total = 0.0
    for b in base_recs:
        cov = 0.0
        for o in ov_grid.near(b):
            if b.x1 < o.x0 or o.x1 < b.x0 or b.z1 < o.z0 or o.z1 < b.z0:
                continue
            cov += tris_overlap_area(b.tris, o.tris)
        exposed = max(0.0, b.xz_area - min(cov, b.xz_area))
        exposed_total += exposed
        if exposed > EPS_AREA:
            deposit(b, 3, exposed)

    all_recs = [p.rec for p in overlay_pairings] + base_recs
    x0 = min(r.x0 for r in all_recs)
    x1 = max(r.x1 for r in all_recs)
    z0 = min(r.z0 for r in all_recs)
    z1 = max(r.z1 for r in all_recs)
    ci0, ci1 = int(math.floor(x0 / cell)), int(math.floor(x1 / cell))
    cj0, cj1 = int(math.floor(z0 / cell)), int(math.floor(z1 / cell))
    pi, pj = int(math.floor(POSE_X / cell)), int(math.floor(POSE_Z / cell))

    out.append(f"Map: one character per {cell:.0f} m cell, X increasing rightwards, Z increasing "
               f"downwards. Character = which district the ORIGINAL's rule would stamp:")
    out.append("  'T' overlay CLEAR -> cblock1/2/3/7 towers      'L' overlay FLAGGED over a base "
               "-> cblock4/5/6 low-rise")
    out.append("  'x' overlay FLAGGED, no base -> nothing        'b' base with no overlay above it"
               " -> cblock4/5/6")
    out.append("  lower case = that winner holds < 60 % of the cell   ' ' = no cblock   "
               "'@' = BL-305's pose cell")
    out.append("")
    out.append("        " + "".join(str(abs(i) % 10) for i in range(ci0, ci1 + 1)))
    letters = "TLxb"
    for j in range(cj0, cj1 + 1):
        row = []
        for i in range(ci0, ci1 + 1):
            slot = cats.get((i, j))
            if slot is None or sum(slot) <= 0.0:
                ch = " "
            else:
                k = max(range(4), key=lambda n: slot[n])
                ch = letters[k]
                if slot[k] / sum(slot) < 0.6:
                    ch = ch.lower()
            if (i, j) == (pi, pj):
                ch = "@"
            row.append(ch)
        out.append(f"  z={j * cell:7.0f} " + "".join(row))
    out.append("")
    tot = [0.0, 0.0, 0.0, 0.0]
    for slot in cats.values():
        for k in range(4):
            tot[k] += slot[k]
    grand = sum(tot)
    out.append(f"area by category:  T(towers) {tot[0]:13,.0f} = {100 * tot[0] / grand:4.1f}%   "
               f"L(low-rise) {tot[1]:13,.0f} = {100 * tot[1] / grand:4.1f}%   "
               f"x(nothing) {tot[2]:13,.0f} = {100 * tot[2] / grand:4.1f}%   "
               f"b(bare base) {tot[3]:13,.0f} = {100 * tot[3] / grand:4.1f}%")
    out.append(f"base area not under any overlay: {exposed_total:,.0f} m^2 of "
               f"{sum(b.xz_area for b in base_recs):,.0f} m^2 total base area")
    counts = {"T": 0, "L": 0, "x": 0, "b": 0}
    for slot in cats.values():
        if sum(slot) <= 0:
            continue
        counts[letters[max(range(4), key=lambda n: slot[n])]] += 1
    out.append(f"cells by winning category: " + "  ".join(f"{k}={v}" for k, v in counts.items())
               + f"   (occupied cells {sum(counts.values())})")
    slot = cats.get((pi, pj))
    if slot:
        out.append(f"BL-305's pose cell ({pi},{pj}): T={slot[0]:,.0f} L={slot[1]:,.0f} "
                   f"x={slot[2]:,.0f} b={slot[3]:,.0f}")
    out.append("")


def report_near(pairings, base_recs, x, z, radius, out, title):
    out.append(f"== within {radius:.0f} m of ({x:.0f}, {z:.0f}) — {title} ==")
    rows = []
    for p in pairings:
        r = p.rec
        dx = max(r.x0 - x, 0.0, x - r.x1)
        dz = max(r.z0 - z, 0.0, z - r.z1)
        d = math.hypot(dx, dz)
        if d <= radius:
            rows.append((d, p, None))
    for b in base_recs:
        dx = max(b.x0 - x, 0.0, x - b.x1)
        dz = max(b.z0 - z, 0.0, z - b.z1)
        d = math.hypot(dx, dz)
        if d <= radius:
            rows.append((d, None, b))
    rows.sort(key=lambda t: t[0])
    if not rows:
        out.append("  nothing")
        out.append("")
        return
    out.append("    dist  flag  texture   node                     y      xzArea   baseCov  "
               "stamps")
    for d, p, b in rows:
        if p is not None:
            r = p.rec
            cov = p.coverage
            stamps = ("cblock4/5/6 (low-rise)" if r.flagged and cov == cov and cov >= 0.5
                      else "NOTHING" if r.flagged
                      else "cblock1/2/3/7 (towers)")
            out.append(f"    {d:6.0f}  {'F' if r.flagged else '.'}     {r.texture:9s} "
                       f"{r.label():24s} {r.y_mean:6.1f} {r.xz_area:9,.0f} "
                       f"{100 * cov if cov == cov else -1:6.1f}%  {stamps}")
        else:
            out.append(f"    {d:6.0f}  {'F' if b.flagged else '.'}     {b.texture:9s} "
                       f"{b.label():24s} {b.y_mean:6.1f} {b.xz_area:9,.0f} "
                       f"    --   (base layer)")
    out.append("")


def named_node_bounds(nodes, meshes, keywords):
    """World-space bounds of every node whose name contains one of `keywords`, measured by the
    SAME walk the clutter placement uses so the coordinates are directly comparable. Returns
    (index, name, x0, x1, y0, y1, z0, z1, vertex_count) — a node with no mesh of its own is
    measured over its whole subtree."""
    world_idx = next((i for i, n in enumerate(nodes)
                      if n.kind == "World" and n.name.lower() == "world1"), None)
    if world_idx is None:
        return []
    found = {}

    def subtree_points(idx, xf, sink):
        if idx < 0 or idx >= len(nodes):
            return
        node = nodes[idx]
        if skip_world_node(node) or (node.kind == "Lod" and node.lod_range_min != 0.0):
            return
        if node.local is not None:
            xf = xf.compose(node.local)
        if 0 <= node.model_index < len(meshes) and meshes[node.model_index] is not None:
            for v in meshes[node.model_index].vertices:
                sink.append(xf.xform(v))
        for c in node.children:
            subtree_points(c, xf, sink)

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes):
            return
        node = nodes[idx]
        if skip_world_node(node) or (node.kind == "Lod" and node.lod_range_min != 0.0):
            return
        if node.local is not None:
            xf = xf.compose(node.local)
        if any(k in node.name.lower() for k in keywords) and idx not in found:
            # `xf` already includes this node's own local, so gather from here without
            # re-applying it: own mesh at `xf`, then each child through `subtree_points`.
            pts = []
            if 0 <= node.model_index < len(meshes) and meshes[node.model_index] is not None:
                for v in meshes[node.model_index].vertices:
                    pts.append(xf.xform(v))
            for c in node.children:
                subtree_points(c, xf, pts)
            if pts:
                found[idx] = (idx, node.name,
                              min(p[0] for p in pts), max(p[0] for p in pts),
                              min(p[1] for p in pts), max(p[1] for p in pts),
                              min(p[2] for p in pts), max(p[2] for p in pts), len(pts))
            else:
                found[idx] = (idx, node.name, None, None, None, None, None, None, 0)
        for c in node.children:
            walk(c, xf)

    world = nodes[world_idx]
    for c in world.children:
        walk(c, IDENTITY)
    for p in (world.partition_roots or []):
        walk(p, IDENTITY)
    return [found[k] for k in sorted(found)]


def report_bridge(nodes, meshes, materials, out):
    """The user's report is 'north of the bridge'. Try to locate a bridge from the geometry:
    bridge-named nodes and `pier`-textured polygons. If neither is conclusive, SAY SO."""
    out.append("== locating C5's bridge ==")
    named = [(i, n.name) for i, n in enumerate(nodes)
             if any(k in n.name.lower() for k in ("bridge", "span", "trestle"))]
    out.append(f"  nodes whose name contains bridge/span/trestle: {len(named)}"
               + ("" if not named else "  " + ", ".join(f"{nm}[{i}]" for i, nm in named[:20])))
    out.append("  the ones REACHABLE from the world walk, in world space:")
    any_reached = False
    bounds = named_node_bounds(nodes, meshes, ("bridge", "span", "trestle"))
    for (idx, name, x0, x1, y0, y1, z0, z1, nv) in bounds:
        any_reached = True
        if nv == 0:
            out.append(f"    {name}[{idx}]: reachable but carries no geometry")
            continue
        out.append(f"    {name}[{idx}]: X[{x0:9.0f},{x1:9.0f}] Y[{y0:7.1f},{y1:7.1f}] "
                   f"Z[{z0:9.0f},{z1:9.0f}]  centre ({(x0 + x1) / 2:.0f}, {(y0 + y1) / 2:.1f}, "
                   f"{(z0 + z1) / 2:.0f})  {nv} vertices")
    if not any_reached:
        out.append("    NONE — every bridge-named node is outside the world1 walk.")
    recs, _ = polygon_records(nodes, meshes, materials, {"pier": "pier"})
    if not recs:
        out.append("  no `pier`-textured polygon is reachable from the world walk.")
        out.append("")
        return bounds, []
    out.append(f"  `pier`-textured world polygons: {len(recs)}  "
               f"({sum(1 for r in recs if r.flagged)} flagged)")
    # Cluster them by proximity so a two-ended bridge does not read as one centroid in the water.
    clusters = []
    for r in sorted(recs, key=lambda r: (r.cx, r.cz)):
        for c in clusters:
            if any(math.hypot(r.cx - q.cx, r.cz - q.cz) < 400.0 for q in c):
                c.append(r)
                break
        else:
            clusters.append([r])
    zero = sum(1 for r in recs if r.xz_area < EPS_AREA)
    out.append(f"  {zero} of them have ZERO plan area — they are VERTICAL faces, i.e. `pier` is "
               f"C5's quay/seawall texture, not bridge decking. It does not locate a bridge.")
    out.append(f"  they form {len(clusters)} cluster(s) at a 400 m linkage distance "
               f"(largest 8 by area shown):")
    for c in sorted(clusters, key=lambda c: -sum(r.xz_area for r in c))[:8]:
        ta = sum(r.xz_area for r in c)
        cx = sum(r.cx * max(r.xz_area, 1) for r in c) / sum(max(r.xz_area, 1) for r in c)
        cz = sum(r.cz * max(r.xz_area, 1) for r in c) / sum(max(r.xz_area, 1) for r in c)
        cy = sum(r.y_mean for r in c) / len(c)
        out.append(f"    n={len(c):3d}  centre ({cx:8.0f}, {cy:6.1f}, {cz:8.0f})  "
                   f"X[{min(r.x0 for r in c):8.0f},{max(r.x1 for r in c):8.0f}] "
                   f"Z[{min(r.z0 for r in c):8.0f},{max(r.z1 for r in c):8.0f}]  "
                   f"area {ta:10,.0f}  nodes "
                   + ",".join(sorted({r.node for r in c})[:6]))
    out.append("")
    return bounds, clusters


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--extracted", default="extracted")
    ap.add_argument("--chapter", default="C5")
    ap.add_argument("--threshold", type=float, default=0.5,
                    help="Coverage share of an overlay polygon that counts as 'a base beneath it' "
                         "for the polygon-count table. The AREA table needs no threshold.")
    ap.add_argument("--cell", type=float, default=512.0)
    ap.add_argument("--radius", type=float, default=800.0)
    args = ap.parse_args()

    print("== self-checks (METHOD-9): the instrument must be able to report 'no pairing' ==")
    ok, msgs = run_self_checks()
    for m in msgs:
        print(f"  {m}")
    if not ok:
        print("SELF-CHECK FAILED - refusing to trust the measurement below.", file=sys.stderr)
        return 2
    print()

    path = os.path.join(args.extracted, args.chapter, "gamez", "nodes.json")
    if not os.path.exists(path):
        print(f"{args.chapter}: missing extraction at {path}", file=sys.stderr)
        return 2

    nodes, meshes, materials = load_chapter(args.extracted, args.chapter)
    wanted = {t: "overlay" for t in OVERLAY_TEXTURES}
    wanted.update({t: "base" for t in BASE_TEXTURES})
    recs, diag = polygon_records(nodes, meshes, materials, wanted)

    print(f"== {args.chapter} walk diagnostics (DIAG-15: nothing skipped silently) ==")
    print(f"  cblock1-7 polygons visited: {diag['visited_polys']}   records: {len(recs)}")
    print(f"  dropped: <3 verts={diag['too_few_verts']} bad_vertex_index="
          f"{diag['bad_vertex_index']} non-finite={diag['nan']}")
    print(f"  reachable twice (deduped by node+poly): {diag['dup_skipped']}")
    print(f"  polygons matching a target texture on TWO material layers: "
          f"{diag['multi_layer_hit']}")
    print(f"  triangle-strip polygons (triangulated as strips, not fans): {diag['strips']}")
    print(f"  degenerate triangles dropped from footprints (zero XZ area): "
          f"{diag['degenerate_tris']}")
    print(f"  polygons with ZERO total XZ area (vertical faces; kept, they simply cannot "
          f"overlap anything in plan): {diag['zero_xz_area']}")
    overlays = [r for r in recs if r.role == "overlay"]
    bases = [r for r in recs if r.role == "base"]
    print(f"  overlay cblock1/2/3/7: {len(overlays)} "
          f"({sum(1 for r in overlays if r.flagged)} flagged)   "
          f"base cblock4/5/6: {len(bases)} ({sum(1 for r in bases if r.flagged)} flagged)")
    print()

    base_unflagged = [b for b in bases if not b.flagged]
    base_flagged = [b for b in bases if b.flagged]
    if base_flagged:
        print(f"  NOTE: {len(base_flagged)} base polygon(s) carry the flag themselves and are "
              f"EXCLUDED from the 'unflagged base beneath' test:")
        for b in base_flagged:
            print(f"    {b.texture} {b.label()} y={b.y_mean:.1f} area={b.xz_area:,.0f}")
        print()

    grid = Grid(base_unflagged)
    pairings = pair_against_base(overlays, base_unflagged, grid)

    out = []
    out.append("== THE RESULT: does a flagged overlay have an unflagged base beneath it? ==")
    out.append("")
    contingency(pairings, out, "all four overlay textures", args.threshold)
    for line in out:
        print(line)

    out = ["== per overlay texture =="]
    for tex in OVERLAY_TEXTURES:
        report_texture(tex, pairings, out, args.threshold)
    for line in out:
        print(line)

    out = ["== the contingency table, per overlay texture =="]
    for tex in OVERLAY_TEXTURES:
        sub = [p for p in pairings if p.rec.texture == tex]
        if sub:
            contingency(sub, out, tex, args.threshold)
    for line in out:
        print(line)

    out = ["== the map: which district would stamp, under the original's rule =="]
    report_stamp_map(pairings, base_unflagged, args.cell, out, args.threshold)
    for line in out:
        print(line)

    out = []
    report_near(pairings, base_unflagged, POSE_X, POSE_Z, args.radius, out, "BL-305's pose")
    for line in out:
        print(line)

    out = []
    bounds, _clusters = report_bridge(nodes, meshes, materials, out)
    for line in out:
        print(line)

    # What the rule predicts around each located bridge. North is -Z in this project
    # (docs/architecture.md, "North = -Z is confirmed"), so "north of the bridge" is the -Z
    # probe; the other three are printed as controls so the reading is not cherry-picked.
    for (idx, name, x0, x1, y0, y1, z0, z1, nv) in bounds:
        if nv == 0:
            continue
        bx, bz = (x0 + x1) / 2.0, (z0 + z1) / 2.0
        for label, dx, dz in (("at the bridge", 0.0, 0.0),
                              ("1000 m toward -Z = NORTH", 0.0, -1000.0),
                              ("2000 m toward -Z = NORTH", 0.0, -2000.0),
                              ("1000 m toward +Z = south", 0.0, 1000.0),
                              ("1000 m toward -X = west", -1000.0, 0.0),
                              ("1000 m toward +X = east", 1000.0, 0.0)):
            out = []
            report_near(pairings, base_unflagged, bx + dx, bz + dz, 700.0, out,
                        f"{name}[{idx}], {label}")
            for line in out:
                print(line)

    return 0


if __name__ == "__main__":
    sys.exit(main())
