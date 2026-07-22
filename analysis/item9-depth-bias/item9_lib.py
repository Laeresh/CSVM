"""Shared loader for polish-4 item 9 (conflict-local depth bias) analysis.

Read-only. Reproduces enough of GameZ.cs / WorldBuilder.cs / SceneBuilder.cs to reason
about which world nodes are built, where their polygons land in world space, and what
depth bias each one receives.

Extraction shape here is the fork's "unified" one: models.json (not meshes.json),
polygon field `priority` (not unk04), node field `child_indices` (flat list positions),
`transform` == "Initial" meaning no transform.
"""
import json, math, os, sys

def _find_root(start):
    """Walk up from this file until a directory containing `extracted/` is found.
    (These scripts were written in `.scratch/` and later committed one level deeper,
    under `analysis/item9-depth-bias/`, so a fixed number of dirname() calls breaks.)"""
    d = os.path.dirname(os.path.abspath(start))
    for _ in range(6):
        if os.path.isdir(os.path.join(d, "extracted")):
            return d
        d = os.path.dirname(d)
    return os.path.dirname(os.path.dirname(os.path.abspath(start)))


ROOT = _find_root(__file__)
EX = os.path.join(ROOT, "extracted")
CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]

# SceneBuilder.cs constants
DEPTH_BIAS_PER_LEVEL = 2e-4
SURFACE_RANK_BIAS = 2e-6
SURFACE_RANK_CAP = 5
NODE_ORDER_BIAS = 5e-8
DEPTH_FLOOR = 1e-6          # measured resolution floor, fraction of view distance


# ---------------------------------------------------------------- math helpers
def mat_identity():
    # 3x4 row-major: rows are basis columns x,y,z then origin
    return ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0), (0.0, 0.0, 0.0))


def mat_mul(a, b):
    """Godot Transform3D a * b (a applied after b)."""
    ax, ay, az, ao = a
    bx, by, bz, bo = b

    def xf_vec(v):
        return (ax[0] * v[0] + ay[0] * v[1] + az[0] * v[2],
                ax[1] * v[0] + ay[1] * v[1] + az[1] * v[2],
                ax[2] * v[0] + ay[2] * v[1] + az[2] * v[2])

    nx, ny, nz = xf_vec(bx), xf_vec(by), xf_vec(bz)
    ro = xf_vec(bo)
    no = (ro[0] + ao[0], ro[1] + ao[1], ro[2] + ao[2])
    return (nx, ny, nz, no)


def mat_xform(m, v):
    bx, by, bz, o = m
    return (bx[0] * v[0] + by[0] * v[1] + bz[0] * v[2] + o[0],
            bx[1] * v[0] + by[1] * v[1] + bz[1] * v[2] + o[1],
            bx[2] * v[0] + by[2] * v[1] + bz[2] * v[2] + o[2])


def basis_from_euler_yxz(e):
    """Godot Basis.FromEuler(v, EulerOrder.Yxz) -> R = Ry(y) * Rx(x) * Rz(z).
    Returned as (col_x, col_y, col_z)."""
    x, y, z = e
    cx, sx = math.cos(x), math.sin(x)
    cy, sy = math.cos(y), math.sin(y)
    cz, sz = math.cos(z), math.sin(z)
    # Ry
    ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]
    rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]
    rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]

    def mm(a, b):
        return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]

    r = mm(mm(ry, rx), rz)
    return ((r[0][0], r[1][0], r[2][0]),
            (r[0][1], r[1][1], r[2][1]),
            (r[0][2], r[1][2], r[2][2]))


def parse_transform(tf):
    """tf is the inner object of {"RotateTranslateScale": {...}} (unified) or the legacy
    transformation object. Mirrors GameZ.ParseTransform."""
    t = tf.get("translation") or tf.get("translate")
    tr = (t["x"], t["y"], t["z"])
    m = tf.get("matrix") or tf.get("original")
    if isinstance(m, dict):
        def M(legacy, unified):
            return m[legacy] if legacy in m else m[unified]
        # stored transposed: real rotation matrix has COLUMNS (a,b,c),(d,e,f),(g,h,i)
        bx = (M("a", "r00"), M("b", "r01"), M("c", "r02"))
        by = (M("d", "r10"), M("e", "r11"), M("f", "r12"))
        bz = (M("g", "r20"), M("h", "r21"), M("i", "r22"))
        return (bx, by, bz, tr)
    r = tf.get("rotation") or tf.get("rotate")
    bx, by, bz = basis_from_euler_yxz((r["x"], r["y"], r["z"]))
    return (bx, by, bz, tr)


def node_local(node):
    """Returns the node's local Transform3D or None (the "Initial" / null case)."""
    data = node.get("data")
    body = None
    if isinstance(data, dict):
        body = next(iter(data.values()))
    if not isinstance(body, dict):
        return None
    tf = body.get("transformation")
    if isinstance(tf, dict):
        return parse_transform(tf)
    tf = body.get("transform")
    if isinstance(tf, dict):
        return parse_transform(next(iter(tf.values())))
    return None


def node_kind(node):
    data = node.get("data")
    if isinstance(data, dict):
        return next(iter(data.keys()))
    return None


# ---------------------------------------------------------------- loading
class Chapter:
    def __init__(self, name):
        self.name = name
        d = os.path.join(EX, name, "gamez")
        with open(os.path.join(d, "nodes.json"), "r", encoding="utf-8") as f:
            self.nodes = json.load(f)
        with open(os.path.join(d, "models.json"), "r", encoding="utf-8") as f:
            self.models = json.load(f)
        with open(os.path.join(d, "materials.json"), "r", encoding="utf-8") as f:
            self.materials = json.load(f)
        try:
            with open(os.path.join(d, "textures.json"), "r", encoding="utf-8") as f:
                self.textures = json.load(f)
        except FileNotFoundError:
            self.textures = []
        self.world_idx = None
        for i, n in enumerate(self.nodes):
            if node_kind(n) == "World":
                self.world_idx = i
                break

    def tex_name(self, material_index):
        if material_index is None or material_index < 0 or material_index >= len(self.materials):
            return None
        m = self.materials[material_index]
        body = m
        if isinstance(m, dict) and len(m) == 1 and isinstance(next(iter(m.values())), dict):
            body = next(iter(m.values()))
        if "texture" in body:
            return body["texture"]
        ti = body.get("texture_index")
        if ti is not None and 0 <= ti < len(self.textures):
            t = self.textures[ti]
            if isinstance(t, dict):
                return t.get("name") or t.get("original")
            return t
        return None

    def walk_roots(self):
        """world1's children plus its partition node refs, deduped, in build order."""
        w = self.nodes[self.world_idx]
        roots = list(w.get("child_indices") or w.get("children") or [])
        part = []
        seen = set(roots)
        body = next(iter(w["data"].values()))
        parts = body.get("partitions") or []
        for row in parts:
            for cell in row:
                for key in ("nodes", "values"):
                    for e in (cell.get(key) or []):
                        idx = e.get("index") if isinstance(e, dict) else e
                        if isinstance(e, dict) and "node_index" in e:
                            idx = e["node_index"]
                        if idx is None:
                            continue
                        if idx not in seen:
                            seen.add(idx)
                            part.append(idx)
        self.partition_roots = part
        self.world_children = list(w.get("child_indices") or w.get("children") or [])
        return roots + part

    def built_nodes(self):
        """DFS every walk root exactly as SceneBuilder.BuildSubtree does, yielding
        (node_index, world_transform). Drops non-nearest LOD levels."""
        out = []
        seen = set()

        def rec(idx, xf):
            if idx in seen:
                return
            seen.add(idx)
            n = self.nodes[idx]
            k = node_kind(n)
            if k == "Lod":
                body = next(iter(n["data"].values()))
                rmin = body.get("range_near") if "range_near" in body else body.get("range_min")
                # SceneBuilder: keep only the highest-detail level (range starts at 0)
                if rmin not in (None, 0, 0.0):
                    return
            loc = node_local(n)
            cur = mat_mul(xf, loc) if loc else xf
            out.append((idx, cur))
            for c in (n.get("child_indices") or n.get("children") or []):
                if 0 <= c < len(self.nodes):
                    rec(c, cur)

        for r in self.walk_roots():
            if 0 <= r < len(self.nodes):
                rec(r, mat_identity())
        return out


def model_index(node):
    return node.get("model_index", node.get("mesh_index", -1))


def polygons_world(ch, node_idx, xf):
    """Yields (priority, material_index, [world verts]) for every polygon of this node's
    model, transformed into world space."""
    n = ch.nodes[node_idx]
    mi = model_index(n)
    if mi is None or mi < 0 or mi >= len(ch.models):
        return
    mdl = ch.models[mi]
    if not isinstance(mdl, dict):
        return
    verts = mdl.get("vertices") or []
    wv = [mat_xform(xf, (v["x"], v["y"], v["z"])) for v in verts]
    for p in (mdl.get("polygons") or []):
        idxs = p.get("vertex_indices") or []
        if len(idxs) < 3:
            continue
        pri = p.get("priority", p.get("unk04", 0))
        mats = p.get("materials")
        matidx = None
        if mats:
            matidx = mats[0].get("material_index")
        elif "material_index" in p:
            matidx = p["material_index"]
        try:
            poly = [wv[i] for i in idxs]
        except IndexError:
            continue
        yield pri, matidx, poly, p


def triangles_world(ch, node_idx, xf):
    """Yields (priority, material_index, (v0,v1,v2)) in WORLD space, triangulated exactly
    the way SceneBuilder.EmitPolygon does.

    ⚠ This, not the raw vertex_indices list, is the correct primitive: a `tri_strip`
    polygon's raw index list is NOT a polygon outline, and a Newell normal over it is
    meaningless (docs/verification.md §4). Treating a 16-index strip box as one n-gon
    manufactures a bogus horizontal plane and bogus overlaps.
    """
    n = ch.nodes[node_idx]
    mi = model_index(n)
    if mi is None or mi < 0 or mi >= len(ch.models):
        return
    mdl = ch.models[mi]
    if not isinstance(mdl, dict):
        return
    verts = mdl.get("vertices") or []
    wv = [mat_xform(xf, (v["x"], v["y"], v["z"])) for v in verts]
    for p in (mdl.get("polygons") or []):
        idxs = p.get("vertex_indices") or []
        m = len(idxs)
        if m < 3:
            continue
        pri = p.get("priority", p.get("unk04", 0))
        mats = p.get("materials")
        matidx = mats[0].get("material_index") if mats else p.get("material_index")
        fl = p.get("flags") or {}
        strip = bool(fl.get("tri_strip") or fl.get("triangle_strip"))
        try:
            if strip:
                for i in range(m - 2):
                    a, b, c = (i, i + 1, i + 2) if (i & 1) == 0 else (i, i + 2, i + 1)
                    yield pri, matidx, (wv[idxs[a]], wv[idxs[b]], wv[idxs[c]])
            else:
                for i in range(1, m - 1):
                    yield pri, matidx, (wv[idxs[0]], wv[idxs[i]], wv[idxs[i + 1]])
        except IndexError:
            continue


def newell(poly):
    nx = ny = nz = 0.0
    m = len(poly)
    for i in range(m):
        a = poly[i]
        b = poly[(i + 1) % m]
        nx += (a[1] - b[1]) * (a[2] + b[2])
        ny += (a[2] - b[2]) * (a[0] + b[0])
        nz += (a[0] - b[0]) * (a[1] + b[1])
    ln = math.sqrt(nx * nx + ny * ny + nz * nz)
    if ln == 0:
        return None
    return (nx / ln, ny / ln, nz / ln)


def plane_of(poly):
    n = newell(poly)
    if n is None:
        return None
    # canonical orientation: flip so the largest-|component| axis is positive
    ai = max(range(3), key=lambda i: abs(n[i]))
    if n[ai] < 0:
        n = (-n[0], -n[1], -n[2])
    c = poly[0]
    d = n[0] * c[0] + n[1] * c[1] + n[2] * c[2]
    return n, d


# ---------------------------------------------------------------- 2D clipping
def project_basis(n):
    """Orthonormal (u, v) spanning the plane with normal n."""
    a = (0.0, 0.0, 1.0) if abs(n[2]) < 0.9 else (1.0, 0.0, 0.0)
    ux = n[1] * a[2] - n[2] * a[1]
    uy = n[2] * a[0] - n[0] * a[2]
    uz = n[0] * a[1] - n[1] * a[0]
    ln = math.sqrt(ux * ux + uy * uy + uz * uz)
    u = (ux / ln, uy / ln, uz / ln)
    v = (n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0])
    return u, v


def to2d(poly, u, v):
    return [(p[0] * u[0] + p[1] * u[1] + p[2] * u[2],
             p[0] * v[0] + p[1] * v[1] + p[2] * v[2]) for p in poly]


def area2(poly2):
    s = 0.0
    m = len(poly2)
    for i in range(m):
        x1, y1 = poly2[i]
        x2, y2 = poly2[(i + 1) % m]
        s += x1 * y2 - x2 * y1
    return abs(s) * 0.5


def ensure_ccw(poly2):
    s = 0.0
    m = len(poly2)
    for i in range(m):
        x1, y1 = poly2[i]
        x2, y2 = poly2[(i + 1) % m]
        s += x1 * y2 - x2 * y1
    return poly2 if s >= 0 else poly2[::-1]


def clip_area(subject, clipper):
    """Sutherland-Hodgman: area of subject ∩ clipper. Clipper must be CONVEX and CCW.
    Both are 2D point lists."""
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
    return area2(out) if len(out) >= 3 else 0.0


def convex_parts(poly2):
    """Fan-triangulate so the clipper is convex (Sutherland-Hodgman needs that)."""
    if len(poly2) == 3:
        return [poly2]
    return [[poly2[0], poly2[i], poly2[i + 1]] for i in range(1, len(poly2) - 1)]


def overlap_area(a2, b2):
    """True polygon ∩ polygon area, both fan-triangulated so convexity holds."""
    total = 0.0
    for bt in convex_parts(ensure_ccw(b2)):
        bt = ensure_ccw(bt)
        for at in convex_parts(ensure_ccw(a2)):
            total += clip_area(at, bt)
    return total
