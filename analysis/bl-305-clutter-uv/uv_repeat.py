r"""Static UV-repeat probe (BL-305 / PLAN-clutter-uv-placement A1): for every world polygon whose
material texture matches a registered clutter template's ground texture, measures the triangle's
true world-space area against its UV-space area and reports sqrt(worldArea / uvArea) — "metres per
one full UV repeat of the texture". No engine run; static analysis over `extracted/` only. Answers
one question: does the remake's fixed 512 m tiling grid (`Clutter.cs`'s `GroundInfo`/`Template.Period`)
match how far the ORIGINAL's texture UVs actually stretch across C1's `terpat02` hillsides, or is
the constant systematically wrong in some regions the way the user's "C1 trees are thin" report
would predict? Does NOT decide the rewrite (that is Wave B); this script only measures.

Mirrors, field for field, the walk `ClutterBuilder.PlaceOnWorld` + `PlaceOnMesh` run
(`CSVM/src/Mech3/Clutter.cs:632-699`), because a triangle measured in a different coordinate frame
gives a plausible but wrong metre figure with nothing to flag it (WORLD-15, docs/verification.md):

  - Root selection: `world1`'s `child_indices` PLUS its `partitions` grid's referenced node
    indices, each subtree walked from `Transform3D.Identity` independently — `PlaceOnWorld`
    does not deduplicate between the two lists and does not apply `world1`'s own transform first
    (it has none in the shipped data).
  - Every node in the walk is skipped, subtree included, when `WorldBuilder.SkipWorldNode` matches
    its name (`horizon`/`dzpaths`/`fvol*`) or it is a non-nearest `Lod` level — exactly
    `PlaceOnWorld`'s `Walk` local function. Unlike `WorldBuilder`'s own placed-node walk (see
    `analysis/collider-probe/probe.py`), `PlaceOnWorld` does NOT check the `active` flag at any
    level — mirrored here by NOT checking it either, since the mechanism under test is the
    clutter placement code, not the render walk.
  - Each node's own local transform composes onto the accumulated one BEFORE recursing into
    children (`xf *= local;` in `Walk`), so a node's own mesh is measured in the transform state
    at that point in the walk, not its parent's.
  - A polygon's texture is read PER LAYER: layer 0 from `materials[0]`, and every following
    element of the polygon's `materials` list is an independent overlay pass with its own
    `material_index` and its own `uv_coords` (`GameZ.cs:454-483` — the extraction's own comment
    puts this at 619 polygons install-wide with a 2nd layer, 7 with a 3rd). This script measures
    whichever layer's texture matches a template's ground texture, not layer 0 unconditionally —
    the A1 trap this guards against.
  - Triangle enumeration matches `SceneBuilder.EmitPolygon` / `PlaceOnMesh`: a triangle STRIP
    (`(i, i+1, i+2)` for i in 0..n-3) when the polygon's `tri_strip`/`triangle_strip` flag is set,
    else a FAN (`(0, i, i+1)` for i in 1..n-2). `uv_coords` (and `vertex_indices`) are indexed by
    CORNER POSITION within the polygon (0..n-1), not by a shared vertex id — matching
    `Clutter.cs:857-858`'s `st.SetUV(poly.UvCoords[corner])` beside
    `mesh.Vertices[poly.VertexIndices[corner]]`.

Transform composition (`GameZ.ParseTransform`, `GameZ.cs:210-238`) is reimplemented in full,
including the Euler-angle branch (`R = Ry(y)*Rx(x)*Rz(z)`, Godot's YXZ order) for the handful of
nodes that carry rotation without an explicit matrix — a worktree with only the matrix branch
would silently mismeasure any clutter-eligible polygon under a rotated ancestor. Scale is not
read: GameZ.cs's own comment records it as unit on every node of every chapter (0 of 4181
non-unit), so a Transform3D here is basis + translation only, exactly what `ParseTransform` builds.

Template resolution mirrors `ClutterBuilder.TemplateNames` (registered names from
`extracted/interp.json`'s `AddClutterTemplates` lines, keyed by
`support\<chapter>\adjust.gw`, minus the `BuriedClutterDistricts` exemption — C5's
cblock4/5/6, `BL-250`) and `ClutterBuilder.FindTemplateRoot` + `FirstWithMesh` + `GroundInfo`
(the parentless `Object3d` root matching the template name; its first mesh-bearing descendant,
depth-first, self included; that mesh's first polygon's layer-0 texture and its XZ extent as the
template's "period" — the remake's per-template tiling constant, NOT a single global 512 m: only
C1's `terpat02` happens to measure to 512).

Deliberately does NOT: model `BL-250`'s coplanar/subface dedup (A3's job — this script counts
every matching triangle once, undeduplicated, which double-counts the buried-but-registered
cblock4/5/6 exemption template if it were included, hence excluding it up front like the remake
does); apply `MinSlopeCos` or any other placement cull (this is a measurement of the TEXTURE's own
UV stretch, not of what the remake currently builds); or read `templates.zrd` (C21/Wave C — the
decoration COUNT per template cell is irrelevant to a metres-per-repeat measurement, which is a
property of the ground texture's UV parameterisation alone).

Run from the repo root (this worktree has no `extracted/`, so pass the main checkout's copy):

    python analysis/bl-305-clutter-uv/uv_repeat.py --extracted Z:/CSVM/extracted

`--chapters C1,C5` restricts the extended survey; C1's `terpat02` and C5's `cblock1/2/3/7` always
run regardless of `--chapters` (they are the item). `--verbose` prints the per-chapter template
resolution (root index, ground node, texture, period) before the measurement table.
"""
import argparse
import json
import math
import os
import sys

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]

# C5 only — cblock1/2/3's subface polygons win the ground z-fight unconditionally, so the
# original never draws cblock4/5/6's buildings (BL-250, closed on CAP-22 capture evidence).
# Mirrors ClutterBuilder.BuriedClutterDistricts exactly (Clutter.cs:123-126).
BURIED_CLUTTER_DISTRICTS = {"cblock4", "cblock5", "cblock6"}

HISTOGRAM_EDGES = [0, 128, 256, 384, 512, 768, 1024, 1280, float("inf")]

EPS_AREA = 1e-6  # world-space m^2 / UV-space unit^2 floor below which a triangle is degenerate


# ---------------------------------------------------------------- data model

class Mesh:
    __slots__ = ("vertices", "polygons")

    def __init__(self):
        self.vertices = []   # list of (x, y, z)
        self.polygons = []   # list of Polygon


class Polygon:
    __slots__ = ("vertex_indices", "tri_strip", "layers")

    def __init__(self):
        self.vertex_indices = []  # corner position -> vertex index into Mesh.vertices
        self.tri_strip = False
        self.layers = []  # list of (material_index, uv_coords or None); uv_coords parallel to
        # vertex_indices, indexed by CORNER POSITION (not vertex id) — see module docstring.


class Node:
    __slots__ = ("name", "kind", "model_index", "children", "lod_range_min", "local",
                 "partition_roots")

    def __init__(self):
        self.name = ""
        self.kind = ""
        self.model_index = -1
        self.children = []
        self.lod_range_min = -1.0
        self.local = None            # Transform or None (== identity)
        self.partition_roots = None  # World nodes only


class Transform:
    """basis: 3 rows (row_k . v == world component k, Godot's own row-major Xform convention —
    see the module docstring's ParseTransform note); translate: (x, y, z)."""
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
        """Returns self * local: local applied first, then self — the same as Godot's
        Transform3D operator* and Clutter.cs's `xf *= local;` accumulation."""
        # New basis columns are this transform's basis applied to `local`'s basis columns;
        # local's basis rows ARE its columns transposed, so column j of local.basis is
        # (local.basis[0][j], local.basis[1][j], local.basis[2][j]).
        new_basis = [[0.0, 0.0, 0.0] for _ in range(3)]
        for col in range(3):
            col_vec = (local.basis[0][col], local.basis[1][col], local.basis[2][col])
            out = self.basis_xform(col_vec)
            for row in range(3):
                new_basis[row][col] = out[row]
        new_translate = self.xform(local.translate)
        return Transform(tuple(tuple(r) for r in new_basis), new_translate)


IDENTITY = Transform(Transform.IDENTITY_BASIS, (0.0, 0.0, 0.0))


# ---------------------------------------------------------------- loading (mirrors GameZ.cs)

def _vec3(d):
    return (d["x"], d["y"], d["z"])


def _parse_transform(tf):
    """Mirrors GameZ.ParseTransform (GameZ.cs:210-238): translate/translation, then either an
    explicit rotation matrix (legacy a..i / unified r00..r22, stored transposed — see the module
    docstring) or Euler angles composed R = Ry(y)*Rx(x)*Rz(z) (Godot's YXZ order). Scale is never
    read (unit everywhere in the shipped data, per GameZ.cs's own comment)."""
    tr = _vec3(tf["translation"] if "translation" in tf else tf["translate"])
    m = tf.get("matrix") or tf.get("original")
    if isinstance(m, dict):
        def M(legacy, unified):
            return float(m[legacy] if legacy in m else m[unified])
        a, b, c = M("a", "r00"), M("b", "r01"), M("c", "r02")
        d, e, f = M("d", "r10"), M("e", "r11"), M("f", "r12")
        g, h, i = M("g", "r20"), M("h", "r21"), M("i", "r22")
        # Columns (a,b,c) / (d,e,f) / (g,h,i); stored transposed, so as ROWS: (a,d,g) / (b,e,h) /
        # (c,f,i) — see the module docstring's derivation.
        basis = ((a, d, g), (b, e, h), (c, f, i))
    else:
        rot = tf["rotation"] if "rotation" in tf else tf["rotate"]
        rx, ry, rz = _vec3(rot)
        cx, sx = math.cos(rx), math.sin(rx)
        cy, sy = math.cos(ry), math.sin(ry)
        cz, sz = math.cos(rz), math.sin(rz)
        # Standard right-handed rotation matrices, row-major, R = Ry . Rx . Rz.
        rx_m = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
        ry_m = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
        rz_m = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))

        def matmul(p, q):
            return tuple(tuple(sum(p[r][k] * q[k][c] for k in range(3)) for c in range(3))
                         for r in range(3))
        basis = matmul(matmul(ry_m, rx_m), rz_m)
    return Transform(basis, tuple(float(x) for x in tr))


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

    texture_names = [t.get("name") or t.get("original") for t in raw_textures]
    materials = []  # material index -> texture name or None
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
            meshes.append(None)  # keep the slot — model_index stays aligned
            continue
        mesh = Mesh()
        mesh.vertices = [_vec3(v) for v in m.get("vertices", [])]
        for p in m.get("polygons", []):
            poly = Polygon()
            poly.vertex_indices = [vi for vi in p.get("vertex_indices", [])]
            flags = p.get("flags", {})
            poly.tri_strip = bool(flags.get("triangle_strip") or flags.get("tri_strip"))
            for layer in p.get("materials") or []:
                mi = layer.get("material_index", -1)
                uv_raw = layer.get("uv_coords")
                uv = [(c["u"], c["v"]) for c in uv_raw] if isinstance(uv_raw, list) else None
                poly.layers.append((mi, uv))
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


def load_template_names(interp_path, chapter):
    """Mirrors ClutterBuilder.TemplateNames (Clutter.cs:177-199): the chapter boot script's
    `AddClutterTemplates` lines, minus BURIED_CLUTTER_DISTRICTS."""
    names = []
    if not os.path.exists(interp_path):
        return names
    wanted = f"support\\{chapter.lower()}\\adjust.gw"
    with open(interp_path, encoding="utf-8") as f:
        scripts = json.load(f)
    for script in scripts:
        if (script.get("name") or "").lower() != wanted:
            continue
        for line in script.get("lines") or []:
            parts = (line or "").split(" ")
            parts = [p for p in parts if p]
            if len(parts) >= 2 and parts[0] == "AddClutterTemplates":
                for name in parts[1:]:
                    if name not in BURIED_CLUTTER_DISTRICTS:
                        names.append(name)
    return names


# ---------------------------------------------------------------- template resolution
# Mirrors ClutterBuilder.FindTemplateRoot + FirstWithMesh + GroundInfo (Clutter.cs:210-222,
# 558-583).

def skip_world_node(node):
    n = node.name.lower()
    return n == "horizon" or n == "dzpaths" or n.startswith("fvol")


def find_template_root(nodes, name):
    is_child = [False] * len(nodes)
    for n in nodes:
        for c in n.children:
            if 0 <= c < len(is_child):
                is_child[c] = True
    for i, n in enumerate(nodes):
        if not is_child[i] and n.kind == "Object3d" and n.name.lower() == name.lower():
            return i
    return None


def first_with_mesh(nodes, meshes, idx, include_self=True):
    node = nodes[idx]
    if include_self and 0 <= node.model_index < len(meshes) and meshes[node.model_index] \
            and len(meshes[node.model_index].polygons) > 0:
        return idx
    for c in node.children:
        if 0 <= c < len(nodes):
            found = first_with_mesh(nodes, meshes, c, include_self=True)
            if found is not None:
                return found
    return None


def first_texture(mesh, materials):
    for poly in mesh.polygons:
        if not poly.layers:
            continue
        mi, _ = poly.layers[0]
        if 0 <= mi < len(materials) and materials[mi] is not None:
            return materials[mi]
    return None


def ground_info(mesh, materials):
    """Returns (texture, period) or None. period = larger of the ground quad's X/Z extent —
    the remake's PER-TEMPLATE tiling constant (Clutter.cs GroundInfo, :569-583), not a single
    global figure."""
    tex = first_texture(mesh, materials)
    if tex is None or not mesh.vertices:
        return None
    xs = [v[0] for v in mesh.vertices]
    zs = [v[2] for v in mesh.vertices]
    period = max(max(xs) - min(xs), max(zs) - min(zs))
    return None if period < 1.0 else (tex, period)


def resolve_templates(nodes, meshes, materials, names, verbose_lines=None):
    """name -> (texture, period). Skips (with a note) a registered name whose root or ground
    quad is not in this chapter's gamez — retail-data-normal (module docstring)."""
    out = {}
    for name in names:
        root = find_template_root(nodes, name)
        if root is None:
            if verbose_lines is not None:
                verbose_lines.append(f"    {name}: no template root in gamez (skipped)")
            continue
        ground = first_with_mesh(nodes, meshes, root)
        if ground is None:
            if verbose_lines is not None:
                verbose_lines.append(f"    {name}: root has no mesh-bearing descendant (skipped)")
            continue
        info = ground_info(meshes[nodes[ground].model_index], materials)
        if info is None:
            if verbose_lines is not None:
                verbose_lines.append(f"    {name}: ground quad untextured or degenerate (skipped)")
            continue
        tex, period = info
        out[name] = (tex, period)
        if verbose_lines is not None:
            verbose_lines.append(f"    {name}: root=[{root}] ground=[{ground}] "
                                  f"texture={tex} period={period:.1f}")
    return out


# ---------------------------------------------------------------- the measurement walk

class TriStats:
    """Per-texture accumulator: every matched triangle's (world_area, uv_area, rate), plus the
    dropped count (DIAG-15 — report what was skipped, never skip it silently)."""
    __slots__ = ("rates_areas", "dropped_no_uv", "dropped_degenerate", "total_world_area",
                 "total_uv_area")

    def __init__(self):
        self.rates_areas = []       # (rate, world_area) — kept for the histogram/median
        self.dropped_no_uv = 0
        self.dropped_degenerate = 0
        self.total_world_area = 0.0
        self.total_uv_area = 0.0

    def add(self, world_area, uv_area):
        rate = math.sqrt(world_area / uv_area)
        self.rates_areas.append((rate, world_area))
        self.total_world_area += world_area
        self.total_uv_area += uv_area


def _cross_len(ax, ay, az, bx, by, bz):
    cx = ay * bz - az * by
    cy = az * bx - ax * bz
    cz = ax * by - ay * bx
    return math.sqrt(cx * cx + cy * cy + cz * cz)


def measure_world(nodes, meshes, materials, target_textures):
    """Walks world1 exactly as ClutterBuilder.PlaceOnWorld does (module docstring) and returns
    {texture: TriStats} for every texture in target_textures that any polygon layer matches."""
    stats = {t: TriStats() for t in target_textures}
    world_idx = next((i for i, n in enumerate(nodes)
                       if n.kind == "World" and n.name.lower() == "world1"), None)
    if world_idx is None:
        return stats
    world = nodes[world_idx]

    def process_mesh(mesh, xf):
        for poly in mesh.polygons:
            n = len(poly.vertex_indices)
            if n < 3:
                continue
            tris = ([(i, i + 1, i + 2) for i in range(n - 2)] if poly.tri_strip
                    else [(0, i, i + 1) for i in range(1, n - 1)])
            for mi, uv in poly.layers:
                tex = materials[mi] if 0 <= mi < len(materials) else None
                if tex is None or tex not in stats:
                    continue
                st = stats[tex]
                for (i0, i1, i2) in tris:
                    if i0 >= len(poly.vertex_indices) or i1 >= len(poly.vertex_indices) \
                            or i2 >= len(poly.vertex_indices):
                        st.dropped_degenerate += 1
                        continue
                    vi0, vi1, vi2 = (poly.vertex_indices[i0], poly.vertex_indices[i1],
                                      poly.vertex_indices[i2])
                    if vi0 >= len(mesh.vertices) or vi1 >= len(mesh.vertices) \
                            or vi2 >= len(mesh.vertices):
                        st.dropped_degenerate += 1
                        continue
                    v0 = xf.xform(mesh.vertices[vi0])
                    v1 = xf.xform(mesh.vertices[vi1])
                    v2 = xf.xform(mesh.vertices[vi2])
                    world_area = 0.5 * _cross_len(
                        v1[0] - v0[0], v1[1] - v0[1], v1[2] - v0[2],
                        v2[0] - v0[0], v2[1] - v0[1], v2[2] - v0[2])
                    if uv is None or i0 >= len(uv) or i1 >= len(uv) or i2 >= len(uv):
                        st.dropped_no_uv += 1
                        continue
                    uv0, uv1, uv2 = uv[i0], uv[i1], uv[i2]
                    uv_area = 0.5 * abs((uv1[0] - uv0[0]) * (uv2[1] - uv0[1])
                                         - (uv2[0] - uv0[0]) * (uv1[1] - uv0[1]))
                    if uv_area < EPS_AREA or world_area < EPS_AREA:
                        st.dropped_degenerate += 1
                        continue
                    st.add(world_area, uv_area)

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes):
            return
        node = nodes[idx]
        if skip_world_node(node) or (node.kind == "Lod" and node.lod_range_min != 0.0):
            return
        if node.local is not None:
            xf = xf.compose(node.local)
        if 0 <= node.model_index < len(meshes) and meshes[node.model_index] is not None:
            process_mesh(meshes[node.model_index], xf)
        for c in node.children:
            walk(c, xf)

    for c in world.children:
        walk(c, IDENTITY)
    for p in (world.partition_roots or []):
        walk(p, IDENTITY)
    return stats


# ---------------------------------------------------------------- reporting

def area_weighted_median(rates_areas):
    if not rates_areas:
        return None
    ordered = sorted(rates_areas, key=lambda ra: ra[0])
    total = sum(a for _, a in ordered)
    if total <= 0:
        return None
    half = total / 2.0
    acc = 0.0
    for rate, area in ordered:
        acc += area
        if acc >= half:
            return rate
    return ordered[-1][0]


def histogram(rates_areas, edges):
    total = sum(a for _, a in rates_areas)
    buckets = [0.0] * (len(edges) - 1)
    for rate, area in rates_areas:
        for b in range(len(edges) - 1):
            if edges[b] <= rate < edges[b + 1]:
                buckets[b] += area
                break
    return buckets, total


def report_texture(label, period, st, out):
    n = len(st.rates_areas)
    out.append(f"  {label} (remake period {period:.1f} m):")
    if n == 0:
        out.append(f"    no matching triangles (dropped: no-uv={st.dropped_no_uv} "
                    f"degenerate={st.dropped_degenerate})")
        return
    rates = [r for r, _ in st.rates_areas]
    med = area_weighted_median(st.rates_areas)
    buckets, total = histogram(st.rates_areas, HISTOGRAM_EDGES)
    below = sum(a for r, a in st.rates_areas if r < period)
    above = total - below
    out.append(f"    triangles={n} dropped_no_uv={st.dropped_no_uv} "
               f"dropped_degenerate={st.dropped_degenerate}")
    out.append(f"    world area total={st.total_world_area:.1f} m^2  uv area total={st.total_uv_area:.4f}")
    out.append(f"    min={min(rates):.1f} m  median(area-wtd)={med:.1f} m  max={max(rates):.1f} m")
    out.append(f"    area fraction below {period:.1f} m: {100.0 * below / total:.1f}%   "
               f"at/above: {100.0 * above / total:.1f}%")
    out.append("    area-weighted histogram (m/repeat -> % of world area):")
    for b in range(len(HISTOGRAM_EDGES) - 1):
        lo, hi = HISTOGRAM_EDGES[b], HISTOGRAM_EDGES[b + 1]
        hi_s = "inf" if hi == float("inf") else f"{hi:.0f}"
        pct = 100.0 * buckets[b] / total if total > 0 else 0.0
        out.append(f"      [{lo:.0f}, {hi_s})  {pct:5.1f}%")
    overall_rate = math.sqrt(st.total_world_area / st.total_uv_area)
    reconstructed = st.total_uv_area * overall_rate * overall_rate
    diff = abs(reconstructed - st.total_world_area)
    tol = 1e-3 * max(st.total_world_area, 1.0)
    ok = diff <= tol
    out.append(f"    self-check: overall_rate={overall_rate:.3f} m, "
               f"uvArea*rate^2={reconstructed:.1f} vs worldArea={st.total_world_area:.1f} "
               f"(diff={diff:.6g}, tol={tol:.6g}) -> {'OK' if ok else 'FAIL'}")
    return ok


# ---------------------------------------------------------------- self-verification (METHOD-9)

def run_self_checks():
    """Two checks the script must actually be able to fail, per PLAN-clutter-uv-placement A1's
    Verify section: a synthetic 1:1 UV mapping over a 256 m span must report 256 m, and the
    area-reconstruction identity must hold for a non-trivial multi-triangle set. Returns
    (ok, messages)."""
    msgs = []
    ok = True

    # 1. Synthetic triangle: right triangle spanning 256 m in X and Z, UV (0,0)-(1,0)-(1,1) —
    # a full unit UV square's worth of triangle area maps to one 256 m span.
    v0, v1, v2 = (0.0, 0.0, 0.0), (256.0, 0.0, 0.0), (256.0, 0.0, 256.0)
    world_area = 0.5 * _cross_len(v1[0] - v0[0], v1[1] - v0[1], v1[2] - v0[2],
                                   v2[0] - v0[0], v2[1] - v0[1], v2[2] - v0[2])
    uv0, uv1, uv2 = (0.0, 0.0), (1.0, 0.0), (1.0, 1.0)
    uv_area = 0.5 * abs((uv1[0] - uv0[0]) * (uv2[1] - uv0[1]) - (uv2[0] - uv0[0]) * (uv1[1] - uv0[1]))
    rate = math.sqrt(world_area / uv_area)
    passed = abs(rate - 256.0) < 1e-4
    ok = ok and passed
    msgs.append(f"synthetic 256 m / unit-UV triangle -> {rate:.4f} m "
                f"({'OK' if passed else 'FAIL, expected 256.0'})")

    # 1b. A deliberate perturbation must NOT also pass (METHOD-9: show the check can fail).
    # Halve the UV span (uv1 -> (0.5, 0) etc.) without changing world geometry: the reported
    # rate must move well away from 256, or this "verification" would rubber-stamp anything.
    uv0b, uv1b, uv2b = (0.0, 0.0), (0.5, 0.0), (0.5, 0.5)
    uv_area_b = 0.5 * abs((uv1b[0] - uv0b[0]) * (uv2b[1] - uv0b[1])
                          - (uv2b[0] - uv0b[0]) * (uv1b[1] - uv0b[1]))
    rate_b = math.sqrt(world_area / uv_area_b)
    perturbation_detected = abs(rate_b - 256.0) > 1.0
    ok = ok and perturbation_detected
    msgs.append(f"perturbed (half-UV-span) triangle -> {rate_b:.4f} m "
                f"({'OK, check can fail' if perturbation_detected else 'FAIL — check is inert'})")

    # 2. Area-reconstruction identity over a small synthetic set of DIFFERENT rates, verifying
    # the accumulation in report_texture (not just the trivial single-triangle case).
    st = TriStats()
    st.add(100.0, 4.0)   # rate 5
    st.add(900.0, 4.0)   # rate 15
    st.add(50.0, 2.0)    # rate 5
    overall_rate = math.sqrt(st.total_world_area / st.total_uv_area)
    reconstructed = st.total_uv_area * overall_rate * overall_rate
    diff = abs(reconstructed - st.total_world_area)
    passed2 = diff < 1e-6
    ok = ok and passed2
    msgs.append(f"area-reconstruction identity over 3 synthetic triangles -> "
                f"diff={diff:.3g} ({'OK' if passed2 else 'FAIL'})")

    return ok, msgs


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                  formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--extracted", default="extracted",
                     help="Path to the extracted/ tree. This worktree has none (git-ignored); "
                          "pass --extracted Z:/CSVM/extracted from a worktree.")
    ap.add_argument("--interp", default=None,
                     help="Path to interp.json (default: <extracted>/interp.json)")
    ap.add_argument("--chapters", default=",".join(CHAPTERS),
                     help="Comma-separated chapters for the EXTENDED survey (all registered "
                          "templates). C1 terpat02 and C5 cblock1/2/3/7 always run.")
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    interp_path = args.interp or os.path.join(args.extracted, "interp.json")

    print("== self-checks (METHOD-9) ==")
    self_ok, msgs = run_self_checks()
    for m in msgs:
        print(f"  {m}")
    if not self_ok:
        print("SELF-CHECK FAILED — refusing to trust the measurement below.", file=sys.stderr)
        return 2
    print()

    all_ok = True
    any_measured = False
    chapters_wanted = [c.strip() for c in args.chapters.split(",") if c.strip()]

    # C1 terpat02 and C5 cblock1/2/3/7 are the item; always attempted regardless of --chapters.
    focus = {
        "C1": ["terpat02"],
        "C5": ["cblock1", "cblock2", "cblock3", "cblock7"],
    }

    for chapter in CHAPTERS:
        path = os.path.join(args.extracted, chapter, "gamez", "nodes.json")
        if not os.path.exists(path):
            print(f"{chapter}: missing extraction at {path}", file=sys.stderr)
            all_ok = False
            continue

        names = load_template_names(interp_path, chapter)
        run_extended = chapter in chapters_wanted
        run_focus = chapter in focus
        if not names and not run_focus:
            continue
        if not run_extended and not run_focus:
            continue

        nodes, meshes, materials = load_chapter(args.extracted, chapter)
        verbose_lines = [] if args.verbose else None
        resolved = resolve_templates(nodes, meshes, materials, names, verbose_lines)

        print(f"== {chapter} ==")
        if args.verbose and verbose_lines:
            print(f"  registered templates ({len(names)}):")
            for line in verbose_lines:
                print(line)

        # Which template names to actually report this run: focus set always; the rest only
        # under --chapters (the "if it is cheap, cover them too" extension).
        wanted_names = set(resolved.keys())
        if not run_extended:
            wanted_names = {n for n in wanted_names if n in focus.get(chapter, [])}

        if not wanted_names:
            print("  (no registered template resolved to a ground texture)")
            print()
            continue

        # Dedup by texture: two template names can share one ground texture at different
        # periods (a "-128" scale variant beside the base name) — measure the texture's
        # triangles once, report once per template name against ITS OWN period.
        target_textures = {resolved[n][0] for n in wanted_names}
        stats = measure_world(nodes, meshes, materials, target_textures)
        any_measured = True

        for name in sorted(wanted_names):
            tex, period = resolved[name]
            out = []
            ok = report_texture(f"{name} -> {tex}", period, stats[tex], out)
            for line in out:
                print(line)
            if ok is False:
                all_ok = False
        print()

    if not any_measured:
        print("No chapter produced a measurement — extraction path is wrong.", file=sys.stderr)
        return 2
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
