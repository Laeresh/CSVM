"""PLAN-clutter-uv-placement A2: the template ground quad's UV parameterisation,
and the UV->world orientation of the world polygons those templates decorate.

Two questions, measured over the extracted gamez JSON only. No engine run, no C# touched.

  (i)  What UV range does a template's ground quad span?  `FUN_004dd230` (the original's
       `LoadTemplate`) projects each decoration's local position onto the ground quad along
       the quad normal, reads the INTERPOLATED texture UV at the hit point, and wraps both
       components into [0,1) with fmod.  That is only the same thing as "fractional position
       across the quad" if the quad's own UVs span 0..1 over its extent.  If a quad spans
       0..2 instead, the fmod folds two groups of decorations onto each other and the remake's
       metres-from-the-min-corner rule (`ClutterBuilder.ParseTemplate`, Clutter.cs:498-502)
       stops being a relabelling of the same thing.

  (ii) Do a world polygon's UV axes correspond to its world axes in a consistent, discoverable
       way?  This is the affine UV->world map `FUN_004dd6e0` builds at its step 7 and inverts
       to recover XYZ from a lattice UV.  Is +U always world +X?  Is it rotated per polygon?
       Does it flip handedness?

`analysis/bl-058-clutter-doubling/FINDINGS.md:121-127` records the earlier `match_footprints.py`
attempt as inconclusive for want of exactly this measurement, so the answers are written into
`FINDINGS-A2.md` beside this file rather than left in a terminal buffer.

UV CONVENTION.  `docs/formats/gamez.md:9` states the extraction is Godot's frame exactly --
right-handed Y-up, nose at -Z, "no mirroring, no UV V-flip".  This script does not take that on
trust: it prints the raw `uv_coords` of each ground quad against the quad's own world vertices,
so the reader can see which corner carries (0,0).  `SceneBuilder.EmitTriangle` (SceneBuilder.cs:705-706)
and `ClutterBuilder.BuildSpriteMesh` (Clutter.cs:857-858) both call `SetUV(uvs[corner])` with no
sign change anywhere, so whatever the file says is what both the remake and (given the doc's
claim) the original engine see.  Godot and Direct3D 7 agree on V-down/top-left texture origin,
so there is no flip to insert.

WHAT IT MIRRORS.  The template side follows `ClutterBuilder.FindTemplateRoot` (Clutter.cs:210-222 --
parentless `Object3d` nodes matched by name; the world also carries unrelated same-named leaf
nodes, so the parentless rule is load-bearing), `FirstWithMesh` (:558-567) and `GroundInfo`
(:569-583).  The world side follows `PlaceOnWorld` (:635-667): world1's `child_indices` then its
partition grid's referenced nodes, accumulated local transforms, `WorldBuilder.SkipWorldNode`
(horizon / dzpaths / fvol*) and the non-nearest-LOD skip.

Duplicated loading code with `uv_repeat.py` (A1) is deliberate: the two items were written
concurrently in the same worktree and neither could import the other without a merge conflict.
Wave B should consolidate the loader into one module when it touches this directory.

Run from the repo root:

    python analysis/bl-305-clutter-uv/uv_orient.py

From a git worktree, where `extracted/` does not exist (it is git-ignored, so worktrees do not
get a copy), point at the main checkout instead -- never link it in, see CLAUDE.md:

    python analysis/bl-305-clutter-uv/uv_orient.py --extracted Z:/CSVM/extracted

Options: `--extracted PATH`, `--samples N` (world triangles listed per template, default 8),
`--all` (list every sampled triangle rather than a spread).  Exit code 0 when every check
passes, 1 when a self-check or a stated invariant fails.
"""
import argparse
import json
import math
import os
import sys

# (chapter, template root name) -- one terrain template, one city template, one suburb template,
# per the plan's A2 section.  `river1` is carried along because it is C1's other registered
# template and costs nothing.
TEMPLATES = [
    ("C1", "terpat02"),
    ("C1", "river1"),
    ("C5", "cblock1"),
    ("C5", "cblock7"),
    ("C2", "resblock1"),
    ("C2", "terpat01"),
    # Added at A1's request: the two cleanest instances of the ratios A1 measured between a
    # template's quad period and the world's metres-per-UV-repeat -- C1 `terpat02` is the
    # exact-2 case (already above), C3 `cliff1_sandtrans` the sqrt(2) case, C2 `parkpat` the
    # 3.20 outlier.
    ("C3", "cliff1_sandtrans"),
    ("C2", "parkpat"),
    # The other three non-square quads, so the "sqrt(2) means a 45-degree rotation" reading can
    # be tested against every instance rather than one.
    ("C2", "filmblock1"),
    ("C2", "parklot1"),
    ("C2", "parklot2"),
]

EPS = 1e-9


# ---------------------------------------------------------------- tiny vector helpers

def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def scale(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def length(a):
    return math.sqrt(dot(a, a))


def normalize(a):
    n = length(a)
    return (0.0, 0.0, 0.0) if n < EPS else scale(a, 1.0 / n)


def fmt3(a):
    return "(%.3f, %.3f, %.3f)" % a


# ---------------------------------------------------------------- transforms

def mat_identity():
    """A 3x4 affine transform as (rows of the 3x3, translation)."""
    return ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)), (0.0, 0.0, 0.0)


def mat_apply(xf, v):
    r, t = xf
    return (dot(r[0], v) + t[0], dot(r[1], v) + t[1], dot(r[2], v) + t[2])


def mat_compose(a, b):
    """a * b -- apply b first, exactly like Godot's Transform3D operator*."""
    ra, _ = a
    rb, tb = b
    cols_b = [(rb[0][j], rb[1][j], rb[2][j]) for j in range(3)]
    r = tuple(tuple(dot(ra[i], cols_b[j]) for j in range(3)) for i in range(3))
    return r, mat_apply(a, tb)


def euler_yxz(rx, ry, rz):
    """R = Ry(y) . Rx(x) . Rz(z) -- Godot's EulerOrder.Yxz, the order gamez.md:8 fits."""
    cx, sx = math.cos(rx), math.sin(rx)
    cy, sy = math.cos(ry), math.sin(ry)
    cz, sz = math.cos(rz), math.sin(rz)
    ry_m = ((cy, 0.0, sy), (0.0, 1.0, 0.0), (-sy, 0.0, cy))
    rx_m = ((1.0, 0.0, 0.0), (0.0, cx, -sx), (0.0, sx, cx))
    rz_m = ((cz, -sz, 0.0), (sz, cz, 0.0), (0.0, 0.0, 1.0))
    return mat_compose(mat_compose((ry_m, (0, 0, 0)), (rx_m, (0, 0, 0))), (rz_m, (0, 0, 0)))[0]


def parse_transform(tf):
    """The node's local transform, mirroring GameZ.ParseTransform (GameZ.cs:210-238).

    "Initial" (or a missing transform) is identity.  A RotateTranslateScale whose `original`
    matrix is present uses the matrix -- stored TRANSPOSED, so its real columns are
    (r00,r01,r02), (r10,r11,r12), (r20,r21,r22).  Scale is unit on every node of the install
    and is ignored, as the C# reader ignores it.
    """
    if tf is None or tf == "Initial" or not isinstance(tf, dict):
        return mat_identity()
    body = tf.get("RotateTranslateScale") if "RotateTranslateScale" in tf else tf
    tr = body.get("translate") or body.get("translation") or {"x": 0.0, "y": 0.0, "z": 0.0}
    t = (tr["x"], tr["y"], tr["z"])
    m = body.get("original") or body.get("matrix")
    if isinstance(m, dict):
        def g(legacy, unified):
            return m[legacy] if legacy in m else m[unified]
        cols = [(g("a", "r00"), g("b", "r01"), g("c", "r02")),
                (g("d", "r10"), g("e", "r11"), g("f", "r12")),
                (g("g", "r20"), g("h", "r21"), g("i", "r22"))]
        r = tuple(tuple(cols[j][i] for j in range(3)) for i in range(3))
        return r, t
    rot = body.get("rotate") or body.get("rotation") or {"x": 0.0, "y": 0.0, "z": 0.0}
    return euler_yxz(rot["x"], rot["y"], rot["z"]), t


# ---------------------------------------------------------------- loading

class Chapter:
    def __init__(self, name, nodes, models, materials):
        self.name = name
        self.nodes = nodes
        self.models = models
        self.materials = materials  # material index -> texture name or None
        self.is_child = [False] * len(nodes)
        for n in nodes:
            for c in (n.get("child_indices") or []):
                if 0 <= c < len(nodes):
                    self.is_child[c] = True
        self.local = [parse_transform(node_transform(n)) for n in nodes]

    def kind(self, i):
        d = self.nodes[i].get("data")
        return next(iter(d)) if isinstance(d, dict) and d else ""

    def name_of(self, i):
        return self.nodes[i].get("name") or ""

    def model_of(self, i):
        mi = self.nodes[i].get("model_index")
        if mi is None or mi < 0 or mi >= len(self.models):
            return None, -1
        return self.models[mi], mi


def node_transform(n):
    d = n.get("data")
    if not isinstance(d, dict) or not d:
        return None
    body = next(iter(d.values()))
    if not isinstance(body, dict):
        return None
    return body.get("transform", body.get("transformation"))


def load_chapter(extracted, chapter):
    base = os.path.join(extracted, chapter, "gamez")
    with open(os.path.join(base, "nodes.json"), encoding="utf-8") as f:
        nodes = json.load(f)
    with open(os.path.join(base, "models.json"), encoding="utf-8") as f:
        models = json.load(f)
    with open(os.path.join(base, "textures.json"), encoding="utf-8") as f:
        textures = json.load(f)
    with open(os.path.join(base, "materials.json"), encoding="utf-8") as f:
        raw_materials = json.load(f)
    tex_names = [t.get("name") or t.get("original") for t in textures]
    materials = []
    for w in raw_materials:
        if "Textured" in w:
            ti = w["Textured"].get("texture_index")
            materials.append(tex_names[ti] if ti is not None and 0 <= ti < len(tex_names) else None)
        else:
            materials.append(None)
    return Chapter(chapter, nodes, models, materials)


# ---------------------------------------------------------------- template side

def find_template_root(ch, name):
    """ClutterBuilder.FindTemplateRoot (Clutter.cs:210-222): a PARENTLESS Object3d by name."""
    for i in range(len(ch.nodes)):
        if not ch.is_child[i] and ch.kind(i) == "Object3d" and ch.name_of(i).lower() == name.lower():
            return i
    return None


def first_with_mesh(ch, i, include_self=True):
    """ClutterBuilder.FirstWithMesh (Clutter.cs:558-567)."""
    m, _ = ch.model_of(i)
    if include_self and m is not None and m.get("polygons"):
        return i
    for c in (ch.nodes[i].get("child_indices") or []):
        if 0 <= c < len(ch.nodes):
            got = first_with_mesh(ch, c, True)
            if got is not None:
                return got
    return None


def poly_uvs(poly, pass_index=0):
    mats = poly.get("materials") or []
    if pass_index >= len(mats):
        return None
    uv = mats[pass_index].get("uv_coords")
    return None if uv is None else [(c["u"], c["v"]) for c in uv]


def poly_material(poly, pass_index=0):
    mats = poly.get("materials") or []
    return mats[pass_index]["material_index"] if pass_index < len(mats) else -1


def verts_of(model):
    return [(v["x"], v["y"], v["z"]) for v in model.get("vertices", [])]


def triangulate(poly):
    """The fan/strip rule SceneBuilder.EmitPolygon, PlaceOnMesh and AppendTriangles all share."""
    idx = poly.get("vertex_indices") or []
    n = len(idx)
    out = []
    if (poly.get("flags") or {}).get("tri_strip") or poly.get("triangle_strip"):
        for i in range(n - 2):
            out.append((i, i + 1, i + 2))
    else:
        for i in range(1, n - 1):
            out.append((0, i, i + 1))
    return out


def affine_uv_to_world(p0, p1, p2, uv0, uv1, uv2):
    """The map FUN_004dd6e0 builds at step 7: P(u,v) = p0 + A*(u-u0) + B*(v-v0).

    A is the world displacement of +1 in U, B of +1 in V.  Returns (A, B, det) with det the
    signed UV-space area x2 -- None when the triangle is degenerate in UV.
    """
    du1, dv1 = uv1[0] - uv0[0], uv1[1] - uv0[1]
    du2, dv2 = uv2[0] - uv0[0], uv2[1] - uv0[1]
    det = du1 * dv2 - du2 * dv1
    if abs(det) < 1e-12:
        return None, None, det
    e1, e2 = sub(p1, p0), sub(p2, p0)
    a = scale(sub(scale(e1, dv2), scale(e2, dv1)), 1.0 / det)
    b = scale(sub(scale(e2, du1), scale(e1, du2)), 1.0 / det)
    return a, b, det


def point_in_tri_uv(p, a, b, c):
    """Barycentric weights of p in the UV triangle (a,b,c); None outside."""
    d = (b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])
    if abs(d) < 1e-12:
        return None
    w0 = ((b[0] - p[0]) * (c[1] - p[1]) - (c[0] - p[0]) * (b[1] - p[1])) / d
    w1 = ((c[0] - p[0]) * (a[1] - p[1]) - (a[0] - p[0]) * (c[1] - p[1])) / d
    w2 = 1.0 - w0 - w1
    tol = -1e-6
    return (w0, w1, w2) if w0 >= tol and w1 >= tol and w2 >= tol else None


def project_onto_quad(point, tri_world, tri_uv, normal):
    """FUN_004dd230's ray-cast: shoot along the quad normal from the decoration's local
    position and take the interpolated UV at the hit.  Returns (u, v) or None."""
    p0, p1, p2 = tri_world
    denom = dot(normal, normal)
    if denom < EPS:
        return None
    # Distance along the normal from `point` to the triangle's plane.
    t = dot(sub(p0, point), normal) / denom
    hit = add(point, scale(normal, t))
    # Barycentric in the plane, via the two dominant axes.
    ax = max(range(3), key=lambda i: abs(normal[i]))
    i0, i1 = [i for i in range(3) if i != ax]
    p = (hit[i0], hit[i1])
    a = (p0[i0], p0[i1])
    b = (p1[i0], p1[i1])
    c = (p2[i0], p2[i1])
    w = point_in_tri_uv(p, a, b, c)
    if w is None:
        return None
    return (w[0] * tri_uv[0][0] + w[1] * tri_uv[1][0] + w[2] * tri_uv[2][0],
            w[0] * tri_uv[0][1] + w[1] * tri_uv[1][1] + w[2] * tri_uv[2][1])


def wrap01(x):
    """The original's fmod wrap: fmod into (-1,1), then the negative branch maps to 1 - frac.

    C's fmod keeps the sign of the dividend, so -0.25 comes back as -0.25 and the engine's
    `if (f < 0) f += 1` branch turns it into 0.75.  An exact 1.0 fmods to 0.0 already.
    """
    f = math.fmod(x, 1.0)
    if f < 0.0:
        f += 1.0
    if f >= 1.0:
        f = 0.0
    return f


class Ground:
    """A template's ground quad, resolved the way ClutterBuilder.GroundInfo does."""

    def __init__(self, ch, root_idx, name):
        self.chapter = ch.name
        self.name = name
        self.root = root_idx
        self.ok = False
        self.note = ""
        gi = first_with_mesh(ch, root_idx)
        if gi is None:
            self.note = "no meshed descendant"
            return
        self.ground_index = gi
        model, self.model_index = ch.model_of(gi)
        self.verts = verts_of(model)
        polys = model.get("polygons") or []
        if not polys or not self.verts:
            self.note = "ground node carries no polygons"
            return
        self.poly = polys[0]
        self.n_polys = len(polys)
        self.texture = ch.materials[poly_material(self.poly)] if 0 <= poly_material(self.poly) < len(ch.materials) else None
        self.uvs = poly_uvs(self.poly)
        self.corner_verts = [self.verts[i] for i in (self.poly.get("vertex_indices") or [])]
        if self.uvs is None or len(self.uvs) != len(self.corner_verts):
            self.note = "ground polygon has no usable uv_coords"
            return
        self.tris = triangulate(self.poly)
        self.normal = normalize(cross(sub(self.corner_verts[1], self.corner_verts[0]),
                                      sub(self.corner_verts[2], self.corner_verts[0])))
        xs = [v[0] for v in self.verts]
        ys = [v[1] for v in self.verts]
        zs = [v[2] for v in self.verts]
        self.extent = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
        self.min_xz = (min(xs), min(zs))
        self.period = max(self.extent[0], self.extent[2])  # GroundInfo's scalar
        self.u_span = (min(u for u, _ in self.uvs), max(u for u, _ in self.uvs))
        self.v_span = (min(v for _, v in self.uvs), max(v for _, v in self.uvs))
        # The quad's own UV->world axes, from its first triangle.
        (a, b, c) = self.tris[0]
        self.axis_u, self.axis_v, _ = affine_uv_to_world(
            self.corner_verts[a], self.corner_verts[b], self.corner_verts[c],
            self.uvs[a], self.uvs[b], self.uvs[c])
        self.ok = self.axis_u is not None

    def uv_at(self, point):
        """The interpolated UV where `point` projects onto the quad along its normal."""
        for (a, b, c) in self.tris:
            got = project_onto_quad(point,
                                    (self.corner_verts[a], self.corner_verts[b], self.corner_verts[c]),
                                    (self.uvs[a], self.uvs[b], self.uvs[c]), self.normal)
            if got is not None:
                return got
        return None


def decorations(ch, ground_index):
    """The ground node's children, each with its own local origin -- ParseTemplate's loop."""
    out = []
    for ci in (ch.nodes[ground_index].get("child_indices") or []):
        if not (0 <= ci < len(ch.nodes)):
            continue
        mi = first_with_mesh(ch, ci, include_self=False)
        _, t = ch.local[ci]
        out.append((ci, ch.name_of(ci), t, mi))
    return out


# ---------------------------------------------------------------- world side

def skip_world_node(name):
    n = name.lower()
    return n == "horizon" or n == "dzpaths" or n.startswith("fvol")


def world_index(ch, world_name="world1"):
    for i in range(len(ch.nodes)):
        if ch.kind(i) == "World" and ch.name_of(i).lower() == world_name:
            return i
    return None


def partition_roots(ch, wi):
    body = ch.nodes[wi]["data"]["World"]
    roots, seen = [], set()
    for row in (body.get("partitions") or []):
        for cell in row:
            for ref in (cell.get("values") or []):
                idx = ref.get("node_index", ref.get("index"))
                if idx not in seen:
                    seen.add(idx)
                    roots.append(idx)
            for idx in (cell.get("node_indices") or []):
                if idx not in seen:
                    seen.add(idx)
                    roots.append(idx)
    return roots


class WorldTri:
    __slots__ = ("node", "node_name", "model", "poly", "tri", "pass_index",
                 "p", "uv", "axis_u", "axis_v", "normal", "det")

    def label(self):
        return ("node[%d] '%s' model %d poly %d tri %d pass %d"
                % (self.node, self.node_name, self.model, self.poly, self.tri, self.pass_index))


def collect_world_tris(ch, wanted_textures):
    """PlaceOnWorld's walk (Clutter.cs:635-667), yielding every triangle whose texture -- on ANY
    pass, since FUN_004de190 iterates every texture layer -- names one of the templates."""
    wi = world_index(ch)
    if wi is None:
        return {}, 0
    out = {t: [] for t in wanted_textures}
    dropped = {"no_uvs": 0, "zero_area_world": 0, "degenerate_uv": 0}

    def walk(i, xf):
        if not (0 <= i < len(ch.nodes)):
            return
        name = ch.name_of(i)
        if skip_world_node(name):
            return
        if ch.kind(i) == "Lod":
            rng = ch.nodes[i]["data"]["Lod"].get("range") or {}
            if rng.get("min", 0.0) != 0.0:
                return
        loc = ch.local[i]
        xf = mat_compose(xf, loc)
        model, mi = ch.model_of(i)
        if model is not None:
            vs = verts_of(model)
            world_vs = [mat_apply(xf, v) for v in vs]
            for pi, poly in enumerate(model.get("polygons") or []):
                idx = poly.get("vertex_indices") or []
                for pass_index in range(len(poly.get("materials") or [])):
                    mat = poly_material(poly, pass_index)
                    tex = ch.materials[mat] if 0 <= mat < len(ch.materials) else None
                    if tex is None or tex.lower() not in out:
                        continue
                    uvs = poly_uvs(poly, pass_index)
                    if uvs is None or len(uvs) < len(idx):
                        dropped["no_uvs"] += 1
                        continue
                    for ti, (a, b, c) in enumerate(triangulate(poly)):
                        p = (world_vs[idx[a]], world_vs[idx[b]], world_vs[idx[c]])
                        uv = (uvs[a], uvs[b], uvs[c])
                        # DIAG-15: a fan over an n-gon with collinear or repeated corners emits
                        # zero-area triangles.  They carry a nonzero UV span, so the affine map
                        # comes out finite but meaningless (it collapses both axes onto one
                        # line).  Drop them and COUNT them rather than letting them poison the
                        # bearing histogram.
                        if length(cross(sub(p[1], p[0]), sub(p[2], p[0]))) < 1e-4:
                            dropped["zero_area_world"] += 1
                            continue
                        au, av, det = affine_uv_to_world(p[0], p[1], p[2], uv[0], uv[1], uv[2])
                        if au is None:
                            dropped["degenerate_uv"] += 1
                            continue
                        w = WorldTri()
                        w.node, w.node_name, w.model, w.poly, w.tri = i, name, mi, pi, ti
                        w.pass_index = pass_index
                        w.p, w.uv, w.axis_u, w.axis_v, w.det = p, uv, au, av, det
                        w.normal = normalize(cross(sub(p[1], p[0]), sub(p[2], p[0])))
                        out[tex.lower()].append(w)
        for c in (ch.nodes[i].get("child_indices") or []):
            walk(c, xf)

    for c in (ch.nodes[wi].get("child_indices") or []):
        walk(c, mat_identity())
    for idx in partition_roots(ch, wi):
        walk(idx, mat_identity())
    return out, dropped


def heading_xz(v):
    """Compass-free bearing of a world vector projected to XZ, degrees, measured from +X
    toward +Z.  +X = 0, +Z = 90, -X = 180, -Z = -90."""
    return math.degrees(math.atan2(v[2], v[0]))


AXIS_NAMES = {0: "+X", 1: "+Z", 2: "-X", 3: "-Z"}


def quadrant(bearing, tol=5.0):
    """Snap an XZ bearing to a cardinal world axis; None when it is more than `tol` off one."""
    q = int(round(bearing / 90.0)) % 4
    if abs(((bearing - q * 90.0) + 180.0) % 360.0 - 180.0) > tol:
        return None
    return q


def orientation_class(w):
    """Which of the eight axis-aligned UV frames this triangle uses, named against the template
    ground quad's own frame (+U = world +X, +V = world +Z).  None when either axis is off-axis."""
    qu, qv = quadrant(heading_xz(w.axis_u)), quadrant(heading_xz(w.axis_v))
    if qu is None or qv is None or qu == qv or (qu + 2) % 4 == qv:
        return None
    return "U=%s V=%s" % (AXIS_NAMES[qu], AXIS_NAMES[qv])


def handedness(w):
    """+1 when (axis_u x axis_v) points the same way as the quad-convention reference
    (X x Z = -Y, i.e. DOWN for an up-facing polygon), -1 when the UV frame is mirrored.

    The reference is the template ground quad itself: its +U is +X and its +V is +Z, so
    axis_u x axis_v = -Y for an upward-facing surface.  A world triangle whose UV frame is
    mirrored relative to its own geometric normal is the case that would produce a
    self-consistent but mirrored city.
    """
    c = cross(w.axis_u, w.axis_v)
    return 1 if dot(c, w.normal) < 0 else -1


# ---------------------------------------------------------------- self-check

def self_check():
    """METHOD-9: show the instrument can fail before trusting its answer."""
    ok = True
    # A 256 m triangle with a textbook 1:1 UV map: +U along +X, +V along +Z.
    p0, p1, p2 = (0.0, 0.0, 0.0), (256.0, 0.0, 0.0), (0.0, 0.0, 256.0)
    a, b, det = affine_uv_to_world(p0, p1, p2, (0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
    ok &= abs(length(a) - 256.0) < 1e-6 and abs(length(b) - 256.0) < 1e-6
    ok &= abs(heading_xz(a) - 0.0) < 1e-6 and abs(heading_xz(b) - 90.0) < 1e-6
    print("  synthetic 1:1 quad      : |A|=%.3f |B|=%.3f  headU=%.1f headV=%.1f  -> %s"
          % (length(a), length(b), heading_xz(a), heading_xz(b), "OK" if ok else "FAIL"))
    # The same triangle with U and V swapped must NOT report the same headings -- if it does,
    # the instrument is not reading the UVs at all.
    a2, b2, _ = affine_uv_to_world(p0, p1, p2, (0.0, 0.0), (0.0, 1.0), (1.0, 0.0))
    swapped = abs(heading_xz(a2) - 90.0) < 1e-6 and abs(heading_xz(b2) - 0.0) < 1e-6
    print("  same quad, U/V swapped  : headU=%.1f headV=%.1f  -> %s (must differ from above)"
          % (heading_xz(a2), heading_xz(b2), "OK" if swapped else "FAIL"))
    ok &= swapped
    # A V-flipped map must be detected as a handedness change, not silently absorbed.
    a3, b3, _ = affine_uv_to_world(p0, p1, p2, (0.0, 1.0), (1.0, 1.0), (0.0, 0.0))
    flip = heading_xz(b3)
    flipped = abs(abs(flip) - 90.0) < 1e-6 and b3[2] < 0
    print("  same quad, V flipped    : +V world dir %s -> %s (must point -Z)"
          % (fmt3(b3), "OK" if flipped else "FAIL"))
    ok &= flipped
    # Degenerate UV triangle: must be refused, not divided by.
    a4, _, _ = affine_uv_to_world(p0, p1, p2, (0.0, 0.0), (1.0, 0.0), (2.0, 0.0))
    print("  degenerate UV triangle  : %s (must be refused)" % ("OK" if a4 is None else "FAIL"))
    ok &= a4 is None
    # The fmod wrap, including the two cases FUN_004dd230 special-cases.
    cases = [(0.25, 0.25), (1.25, 0.25), (-0.25, 0.75), (-1.25, 0.75), (1.0, 0.0), (0.0, 0.0), (2.0, 0.0)]
    bad = [(x, wrap01(x), e) for x, e in cases if abs(wrap01(x) - e) > 1e-9]
    print("  fmod wrap [0,1)         : %s" % ("OK" if not bad else "FAIL %s" % bad))
    ok &= not bad
    return ok


# ---------------------------------------------------------------- report

def describe_ground(g):
    print("\n%s / %s  (root node %d, ground node %d, model %d)"
          % (g.chapter, g.name, g.root, g.ground_index, g.model_index))
    if not g.ok:
        print("  UNRESOLVED: %s" % g.note)
        return
    print("  ground texture      : %s" % g.texture)
    print("  ground polygons     : %d (the first is the quad GroundInfo measures)" % g.n_polys)
    print("  local extent X/Y/Z  : %.3f / %.3f / %.3f   GroundInfo.Period = %.3f"
          % (g.extent + (g.period,)))
    print("  quad corners (local vertex -> uv):")
    for k, (v, uv) in enumerate(zip(g.corner_verts, g.uvs)):
        print("    [%d] %-28s -> u=%.6f v=%.6f" % (k, fmt3(v), uv[0], uv[1]))
    print("  U span %.6f .. %.6f   V span %.6f .. %.6f"
          % (g.u_span + g.v_span))
    print("  +U is world %s (%.3f m per unit U, bearing %.1f deg from +X toward +Z)"
          % (fmt3(normalize(g.axis_u)), length(g.axis_u), heading_xz(g.axis_u)))
    print("  +V is world %s (%.3f m per unit V, bearing %.1f deg)"
          % (fmt3(normalize(g.axis_v)), length(g.axis_v), heading_xz(g.axis_v)))
    print("  quad normal         : %s" % fmt3(g.normal))


def describe_decorations(ch, g, limit=6):
    decos = decorations(ch, g.ground_index)
    print("  decorations         : %d" % len(decos))
    fails = 0
    shown = 0
    for (ci, name, t, mi) in decos:
        uv = g.uv_at(t)
        if uv is None:
            fails += 1
            continue
        wu, wv = wrap01(uv[0]), wrap01(uv[1])
        # The remake's rule, for the comparison: metres from the quad's min corner / period.
        rx = (t[0] - g.min_xz[0]) / g.period
        rz = (t[2] - g.min_xz[1]) / g.period
        if shown < limit:
            print("    %-18s local %-28s -> uv (%.6f, %.6f) wrapped (%.6f, %.6f)"
                  % (name, fmt3(t), uv[0], uv[1], wu, wv))
            print("      %s remake (origin-min)/period = (%.6f, %.6f)  delta (%.2e, %.2e)"
                  % (" " * 17, rx, rz, wu - rx, wv - rz))
            shown += 1
    if fails:
        print("    %d decoration(s) do NOT project onto the quad (the original logs and skips these)"
              % fails)
    return len(decos), fails


def axis_stats(tris):
    """Bearing/length distributions of the UV axes over a set of world triangles."""
    n = len(tris)
    if n == 0:
        return None
    bu = sorted(heading_xz(w.axis_u) for w in tris)
    bv = sorted(heading_xz(w.axis_v) for w in tris)
    lu = sorted(length(w.axis_u) for w in tris)
    lv = sorted(length(w.axis_v) for w in tris)
    hands = {}
    for w in tris:
        hands[handedness(w)] = hands.get(handedness(w), 0) + 1
    # Bearings quantised to 1 degree, most common first.
    hist_u = {}
    for b in bu:
        k = int(round(b))
        hist_u[k] = hist_u.get(k, 0) + 1
    hist_v = {}
    for b in bv:
        k = int(round(b))
        hist_v[k] = hist_v.get(k, 0) + 1
    classes = {}
    for w in tris:
        c = orientation_class(w) or "(off-axis)"
        classes[c] = classes.get(c, 0) + 1
    return {
        "classes": sorted(classes.items(), key=lambda kv: -kv[1]),
        "n": n,
        "bu": (bu[0], bu[n // 2], bu[-1]),
        "bv": (bv[0], bv[n // 2], bv[-1]),
        "lu": (lu[0], lu[n // 2], lu[-1]),
        "lv": (lv[0], lv[n // 2], lv[-1]),
        "hands": hands,
        "hist_u": sorted(hist_u.items(), key=lambda kv: -kv[1])[:6],
        "hist_v": sorted(hist_v.items(), key=lambda kv: -kv[1])[:6],
    }


def print_axis_stats(title, st):
    if st is None:
        print("  %-22s (none)" % title)
        return
    print("  %-22s n=%d" % (title, st["n"]))
    print("      +U bearing  min/med/max %8.2f %8.2f %8.2f deg   metres/U %8.2f %8.2f %8.2f"
          % (st["bu"] + st["lu"]))
    print("      +V bearing  min/med/max %8.2f %8.2f %8.2f deg   metres/V %8.2f %8.2f %8.2f"
          % (st["bv"] + st["lv"]))
    print("      handedness  %s (+1 = same as the template quad's, -1 = mirrored)" % st["hands"])
    print("      +U bearing modes (deg x count): %s" % ", ".join("%d x%d" % kv for kv in st["hist_u"]))
    print("      +V bearing modes (deg x count): %s" % ", ".join("%d x%d" % kv for kv in st["hist_v"]))
    print("      UV frame (vs the template quad's U=+X V=+Z):")
    for name, count in st["classes"]:
        print("        %-16s %6d  %5.1f%%" % (name, count, 100.0 * count / st["n"]))


def sample_spread(tris, n):
    if len(tris) <= n:
        return list(tris)
    step = len(tris) / float(n)
    return [tris[int(i * step)] for i in range(n)]


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--extracted", default="extracted",
                    help="path to the extraction root (from a worktree: Z:/CSVM/extracted)")
    ap.add_argument("--samples", type=int, default=8, help="world triangles listed per template")
    ap.add_argument("--all", action="store_true", help="list every sampled triangle")
    args = ap.parse_args()

    print("=" * 96)
    print("A2 self-check (METHOD-9: the instrument must be able to fail)")
    print("=" * 96)
    ok = self_check()

    chapters = {}
    for chapter, _ in TEMPLATES:
        if chapter in chapters:
            continue
        path = os.path.join(args.extracted, chapter, "gamez", "nodes.json")
        if not os.path.exists(path):
            print("\nmissing extraction at %s" % path, file=sys.stderr)
            return 1
        chapters[chapter] = load_chapter(args.extracted, chapter)

    print("\n" + "=" * 96)
    print("(i-census) EVERY registered template in every chapter -- UV span of its ground quad")
    print("=" * 96)
    ok &= template_census(args.extracted)

    grounds = {}
    print("\n" + "=" * 96)
    print("(i) TEMPLATE GROUND QUADS -- what UV range do they span?")
    print("=" * 96)
    for chapter, tname in TEMPLATES:
        ch = chapters[chapter]
        root = find_template_root(ch, tname)
        if root is None:
            print("\n%s / %s: no parentless Object3d by that name" % (chapter, tname))
            continue
        g = Ground(ch, root, tname)
        grounds[(chapter, tname)] = g
        describe_ground(g)
        if g.ok:
            describe_decorations(ch, g)
            if not (abs(g.u_span[0]) < 1e-6 and abs(g.u_span[1] - 1.0) < 1e-6
                    and abs(g.v_span[0]) < 1e-6 and abs(g.v_span[1] - 1.0) < 1e-6):
                print("  !! this quad's UVs do NOT span exactly 0..1 -- the fmod wrap in "
                      "FUN_004dd230 changes meaning here")
                ok = False

    print("\n" + "=" * 96)
    print("(ii) WORLD POLYGONS -- where do the UV axes point in world space?")
    print("=" * 96)
    by_chapter = {}
    for (chapter, tname), g in grounds.items():
        if g.ok and g.texture:
            by_chapter.setdefault(chapter, set()).add(g.texture.lower())
    collected = {}
    for chapter, texset in by_chapter.items():
        tris, dropped = collect_world_tris(chapters[chapter], texset)
        collected[chapter] = tris
        kept = sum(len(v) for v in tris.values())
        print("\n%s: %d triangle(s) kept; dropped %d with no UVs on the matching pass, "
              "%d zero-area in world (fan artifacts), %d degenerate in UV  (LOG-5: nothing "
              "is truncated, these are the only exclusions)"
              % (chapter, kept, dropped["no_uvs"], dropped["zero_area_world"],
                 dropped["degenerate_uv"]))

    for (chapter, tname), g in grounds.items():
        if not g.ok or not g.texture:
            continue
        tris = collected[chapter].get(g.texture.lower(), [])
        print("\n-- %s / %s  (texture %s): %d world triangles" % (chapter, tname, g.texture, len(tris)))
        if not tris:
            continue
        flat = [w for w in tris if abs(w.normal[1]) > 0.999]
        gentle = [w for w in tris if 0.9 < abs(w.normal[1]) <= 0.999]
        slope = [w for w in tris if abs(w.normal[1]) <= 0.9]
        print_axis_stats("all", axis_stats(tris))
        print_axis_stats("flat (|Ny|>0.999)", axis_stats(flat))
        print_axis_stats("gentle (0.9-0.999)", axis_stats(gentle))
        print_axis_stats("sloped (|Ny|<=0.9)", axis_stats(slope))
        print("  sample triangles:")
        for w in (tris if args.all else sample_spread(tris, args.samples)):
            print("    %s" % w.label())
            print("      p0 %s  uv (%.4f, %.4f)" % (fmt3(w.p[0]), w.uv[0][0], w.uv[0][1]))
            print("      +U -> %s  |%.2f m|  bearing %7.2f deg" % (fmt3(normalize(w.axis_u)), length(w.axis_u), heading_xz(w.axis_u)))
            print("      +V -> %s  |%.2f m|  bearing %7.2f deg  normal %s  hand %+d"
                  % (fmt3(normalize(w.axis_v)), length(w.axis_v), heading_xz(w.axis_v),
                     fmt3(w.normal), handedness(w)))

    print("\n" + "=" * 96)
    print("WORKED EXAMPLE (METHOD-1): one decoration, one named world triangle")
    print("=" * 96)
    ok &= worked_example(chapters, grounds, collected)

    print("\n" + "=" * 96)
    print("VERDICT: %s" % ("all checks passed" if ok else "A CHECK FAILED -- see the '!!' lines above"))
    print("=" * 96)
    return 0 if ok else 1


ALL_CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def registered_templates(extracted, chapter):
    """The chapter's `AddClutterTemplates` names, as ClutterBuilder.TemplateNames reads them
    (Clutter.cs:177-199) -- but WITHOUT the BuriedClutterDistricts filter, since this is a
    census of the shipped data, not of what the remake chooses to build."""
    path = os.path.join(extracted, "interp.json")
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        scripts = json.load(f)
    wanted = "support\\%s\\adjust.gw" % chapter.lower()
    names = []
    for s in scripts:
        if (s.get("name") or "").lower() != wanted:
            continue
        for line in s.get("lines") or []:
            parts = line.split()
            if parts and parts[0] == "AddClutterTemplates":
                names.extend(parts[1:])
    return names


def template_census(extracted):
    """Answer (i) install-wide rather than on the plan's three samples: does EVERY template's
    ground quad span exactly 0..1?  METHOD-11 -- do not generalise from a handful."""
    ok = True
    print("  %-5s %-18s %-22s %7s %7s %7s  %-25s %-11s %s"
          % ("ch", "template", "ground texture", "extX", "extZ", "period", "U span / V span",
             "quad frame", "decos  max|wrapUV-remake|  notproj"))
    total = missing = 0
    worst = 0.0
    nonsquare = []
    mirrored = []
    for chapter in ALL_CHAPTERS:
        names = registered_templates(extracted, chapter)
        if not names:
            print("  %-5s (registers no clutter templates)" % chapter)
            continue
        if not os.path.exists(os.path.join(extracted, chapter, "gamez", "nodes.json")):
            print("  %-5s (no extraction)" % chapter)
            continue
        ch = load_chapter(extracted, chapter)
        for name in names:
            total += 1
            root = find_template_root(ch, name)
            if root is None:
                missing += 1
                print("  %-5s %-20s (no parentless Object3d by that name -- retail-data-normal, "
                      "see Clutter.cs:203-208)" % (chapter, name))
                continue
            g = Ground(ch, root, name)
            if not g.ok:
                missing += 1
                print("  %-5s %-20s UNRESOLVED: %s" % (chapter, name, g.note))
                continue
            unit = (abs(g.u_span[0]) < 1e-6 and abs(g.u_span[1] - 1.0) < 1e-6
                    and abs(g.v_span[0]) < 1e-6 and abs(g.v_span[1] - 1.0) < 1e-6)
            qu = quadrant(heading_xz(g.axis_u))
            qv = quadrant(heading_xz(g.axis_v))
            frame = "U=%s V=%s" % (AXIS_NAMES.get(qu, "?"), AXIS_NAMES.get(qv, "?"))
            # Does the remake's "(origin - min corner) / period" reproduce the wrapped UV?
            gap, fails, n = 0.0, 0, 0
            for (_, _, t, _) in decorations(ch, g.ground_index):
                n += 1
                uv = g.uv_at(t)
                if uv is None:
                    fails += 1
                    continue
                gap = max(gap, abs(wrap01(uv[0]) - (t[0] - g.min_xz[0]) / g.period),
                          abs(wrap01(uv[1]) - (t[2] - g.min_xz[1]) / g.period))
            worst = max(worst, gap)
            if abs(g.extent[0] - g.extent[2]) > 1e-3:
                nonsquare.append("%s/%s (%gx%g)" % (chapter, name, g.extent[0], g.extent[2]))
            if frame != "U=+X V=+Z":
                mirrored.append("%s/%s (%s)" % (chapter, name, frame))
            print("  %-5s %-18s %-22s %7.1f %7.1f %7.1f  %.3f..%.3f / %.3f..%.3f  %-11s %4d  %8.1e  %d%s"
                  % (chapter, name, g.texture, g.extent[0], g.extent[2], g.period,
                     g.u_span[0], g.u_span[1], g.v_span[0], g.v_span[1], frame, n, gap, fails,
                     "" if unit else "  !! NOT 0..1"))
            if not unit:
                ok = False
            if fails:
                ok = False
    print("  %d registered template names, %d of them absent from their chapter's gamez"
          % (total, missing))
    print("  every resolved quad spans exactly 0..1 in both axes: %s" % ("YES" if ok else "NO"))
    print("  NON-SQUARE quads (%d) -- GroundInfo's scalar Period cannot describe these: %s"
          % (len(nonsquare), ", ".join(nonsquare) or "none"))
    print("  MIRRORED quads (%d) -- their UVs run opposite to world +X/+Z: %s"
          % (len(mirrored), ", ".join(mirrored) or "none"))
    print("  worst disagreement between the wrapped quad UV and the remake's "
          "(origin - min)/period: %.3f  (0 would mean the remake's rule is a relabelling)" % worst)
    return ok


def worked_example(chapters, grounds, collected):
    """C1 terpat02's first firtree1 decoration, stamped onto one named C1 world triangle.

    Chosen because terpat02 is the plan's headline template and C1's forest is the density
    complaint the plan opens with.  Everything here is recomputed from the data, then checked
    against a hand-written expectation printed beside it.
    """
    key = ("C1", "terpat02")
    if key not in grounds or not grounds[key].ok:
        print("  terpat02 unresolved -- no worked example")
        return False
    ch, g = chapters["C1"], grounds[key]
    decos = decorations(ch, g.ground_index)
    pick = next((d for d in decos if d[1].lower().startswith("firtree1")), decos[0])
    ci, dname, t, _ = pick
    uv = g.uv_at(t)
    wu, wv = wrap01(uv[0]), wrap01(uv[1])
    print("  decoration      : node[%d] '%s' of %s/terpat02" % (ci, dname, g.chapter))
    print("  local position  : %s" % fmt3(t))
    print("  quad UV at it   : (%.6f, %.6f)  -> fmod-wrapped (%.6f, %.6f)" % (uv[0], uv[1], wu, wv))
    hand_u = (t[0] - (-256.0)) / 512.0
    hand_v = (t[2] - (-256.0)) / 512.0
    print("  by hand         : u = (x + 256) / 512 = %.6f ; v = (z + 256) / 512 = %.6f"
          % (hand_u, hand_v))
    agree = abs(hand_u - uv[0]) < 1e-5 and abs(hand_v - uv[1]) < 1e-5
    print("  agreement       : %s" % ("OK" if agree else "MISMATCH"))

    tris = collected["C1"].get(g.texture.lower(), [])
    if not tris:
        print("  no C1 world triangle carries %s" % g.texture)
        return False
    # A flat triangle that this decoration's (u,v) actually lands in, so the example ends at a
    # real world position rather than at "no cell hit".  Flat, so the arithmetic below is
    # checkable on paper.
    def receives(x):
        umn, umx = min(q[0] for q in x.uv), max(q[0] for q in x.uv)
        vmn, vmx = min(q[1] for q in x.uv), max(q[1] for q in x.uv)
        for ui in range(int(math.floor(umn)), int(math.floor(umx)) + 1):
            for vi in range(int(math.floor(vmn)), int(math.floor(vmx)) + 1):
                if point_in_tri_uv((ui + wu, vi + wv), x.uv[0], x.uv[1], x.uv[2]) is not None:
                    return True
        return False

    w = next((x for x in tris if abs(x.normal[1]) > 0.999 and receives(x)),
             next((x for x in tris if receives(x)), tris[0]))
    print("\n  world triangle  : %s" % w.label())
    for k in range(3):
        print("    v%d %s   uv (%.6f, %.6f)" % (k, fmt3(w.p[k]), w.uv[k][0], w.uv[k][1]))
    umin = min(x[0] for x in w.uv)
    umax = max(x[0] for x in w.uv)
    vmin = min(x[1] for x in w.uv)
    vmax = max(x[1] for x in w.uv)
    print("  UV bbox         : u %.6f..%.6f  v %.6f..%.6f" % (umin, umax, vmin, vmax))
    print("  floored lattice : uInt %d..%d  vInt %d..%d  (FUN_004dd6e0 step 4)"
          % (math.floor(umin), math.floor(umax), math.floor(vmin), math.floor(vmax)))
    print("  affine map      : P(u,v) = p0 + A*(u-u0) + B*(v-v0)")
    print("    A (per +1 U)  : %s  |%.3f m|" % (fmt3(w.axis_u), length(w.axis_u)))
    print("    B (per +1 V)  : %s  |%.3f m|" % (fmt3(w.axis_v), length(w.axis_v)))
    hits = 0
    placed = []
    for ui in range(int(math.floor(umin)), int(math.floor(umax)) + 1):
        for vi in range(int(math.floor(vmin)), int(math.floor(vmax)) + 1):
            cand = (ui + wu, vi + wv)
            bary = point_in_tri_uv(cand, w.uv[0], w.uv[1], w.uv[2])
            if bary is None:
                continue
            hits += 1
            # Step 7: recover world XYZ.  Two independent routes -- the affine map and the
            # barycentric interpolation of the vertices -- must agree, or the map is wrong.
            via_affine = add(w.p[0], add(scale(w.axis_u, cand[0] - w.uv[0][0]),
                                         scale(w.axis_v, cand[1] - w.uv[0][1])))
            via_bary = add(add(scale(w.p[0], bary[0]), scale(w.p[1], bary[1])), scale(w.p[2], bary[2]))
            d = length(sub(via_affine, via_bary))
            placed.append((cand, via_affine, d))
    print("  lattice cells hit by this decoration's (u,v): %d" % hits)
    good = True
    for cand, pos, d in placed:
        du, dv = cand[0] - w.uv[0][0], cand[1] - w.uv[0][1]
        print("    candidate UV (%.6f, %.6f)" % cand)
        print("      = p0 %s + A * %.6f + B * %.6f" % (fmt3(w.p[0]), du, dv))
        print("      -> world %s   affine-vs-barycentric %.2e m" % (fmt3(pos), d))
        if d > 1e-3:
            good = False
    if not placed:
        print("    (this decoration's UV falls outside this triangle -- expected for most "
              "decoration/triangle pairs; the stamp is per-cell, not per-triangle-guaranteed)")
    print("  affine map agrees with barycentric interpolation: %s" % ("OK" if good else "FAIL"))
    return agree and good


if __name__ == "__main__":
    sys.exit(main())
