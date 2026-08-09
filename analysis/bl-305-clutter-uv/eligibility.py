"""Clutter-eligibility census (plan item A3, docs/PLAN-clutter-uv-placement.md).

Answers, per chapter and with NO engine running: how many world polygons would the ORIGINAL
engine's clutter stamper (`crimson.exe`, `cls_clutter.cpp`) visit that the REMAKE's
`ClutterBuilder` (`CSVM/src/Mech3/Clutter.cs`) does not, and vice versa -- so Wave B's rewrite
has a baseline to compare against, per the plan's ⚠ WORLD-25 ("a bigger census is not proof of
better coverage").

Modelled on `analysis/collider-probe/probe.py`'s shape (a `CHAPTERS` list, plain `json` over
`extracted/<C>/gamez/*.json`, no engine, verdict in the exit code) but this script does NOT
share any loading code with `probe.py`, `collider-probe`'s siblings, or `item9-depth-bias`'s
`cblock_probe*.py`/`item9_lib.py` (which already implements a coplanar-coverage computation for
a DIFFERENT question -- CBLOCK-LOD's base/subface city fidelity). Two sibling agents are writing
`uv_repeat.py` (A1) and `uv_orient.py` (A2) in this same directory concurrently; this file does
not import them either. The duplication is deliberate (plan's hard constraint #5): every script
in this family re-reads the same `nodes.json`/`models.json`/`materials.json`/`textures.json`
shape from scratch rather than risk a shared-module edit colliding with a sibling's work.

THE TWO RULE SETS
------------------
Original (`crimson.exe`, addresses from `docs/PLAN-clutter-uv-placement.md`'s evidence table,
read via the Ghidra MCP 2026-08-09):

  FUN_004de460  per NODE: requires flag bit 2 at node+0x24 (mech3ax `NodeFlags::ACTIVE = 1<<2`,
                the extraction's `flags.active`), requires NOT bit 26 (0x4000000) at node+0x2c
                (mech3ax's `unk044` field -- see "WHAT THE EXTRACTION DOES NOT EXPOSE" below),
                and dispatches only on node type 5 (`Object3d`) or 6 (`LoD`) --
                `tools/mech3ax/crates/nodes/src/types.rs`.
  FUN_004de2c0  per POLYGON (stride 0x28): skip if flag 0x800 is set (this IS decoded --
                `docs/formats/gamez.md`'s `unk3`, the OpenFlight SUBFACE mark), skip if the UV
                array pointer at +0x18 is null.
  FUN_004de190  per TEXTURE LAYER of the polygon (count at +0x10, list at +0x14): look the
                template up by texture name. A two-layer polygon whose layers both name
                registered templates is stamped TWICE.

Remake (`ClutterBuilder`, `CSVM/src/Mech3/Clutter.cs`):

  PlaceOnWorld (:635-667)   walks `world.Children` + `world.PartitionNodes`, recursing through
                            EVERY child node with no active-flag check and no node-type check at
                            all -- only `WorldBuilder.SkipWorldNode` (name `horizon`/`dzpaths`/
                            `fvol*`) and a non-nearest-LOD skip.
  PlaceOnMesh (:669-699)    matches only `poly.MaterialIndex` -- i.e. texture LAYER 0 ONLY; a
                            second/third material entry (`docs/formats/gamez.md`'s "a polygon's
                            materials is a LIST" finding, BL-056) is never looked at.
  PlaceOnTriangle (:324-371) additionally applies `MinSlopeCos = 0.25` (no original counterpart
                            in the default case) and a `seen` XZ-quarter-metre dedup set (no
                            original counterpart at all -- FUN_004de190 stamps every layer).

WHAT THE EXTRACTION DOES NOT EXPOSE
------------------------------------
`node+0x2c`, the field FUN_004de460 tests against 0x4000000, is NOT `flags` (that is `node+0x24`,
already decoded as `NodeFlags` -- see `tools/mech3ax/crates/api-types/src/gamez/nodes.rs`). Node
layout in `tools/mech3ax/crates/nodes/src/mw/node.rs` (`NodeMwC`, matching the file's serialized
struct, offsets in comments): `flags` at 036 (0x24), `zero040` at 040 (0x28), `unk044` at 044
(0x2c) -- exactly the field the plan's FUN_004de460 gate tests. mech3ax's own reader ASSERTS this
field is usually 0 (`gamez/src/nodes/node/mw/read.rs`: `chk!(offset, node.update_flags == 0)`, with
a few call sites commenting out the assert with observed values `[1, 3, 5, 7]`), and the current
fork's JSON exposes it verbatim as `nodes.json`'s top-level `"update_flags"` integer -- so this
gate IS testable from the extraction, just never named as a bit flag anywhere in
`docs/formats/gamez.md`. `docs/formats/gamez.md` does NOT currently document `update_flags`'s
bit 26 (0x4000000) at all; this script tests the raw bit and reports what it finds rather than
inventing a name for it, per the plan's instruction.

`+0x18`, the per-polygon UV-array-pointer gate FUN_004de2c0 tests, is empirically NEVER null in
this install: every polygon of every chapter carries a non-empty `materials` list whose entry 0
has non-null `uv_coords` (verified by grep across all 8 chapters' `models.json`: zero hits for
`"materials": []` or `"uv_coords": null`). The polygon's own raw `uvs_ptr` field (a leftover
authoring-time pointer, like `vertex_indices_ptr`/`matl_refs_ptr` beside it) is also never 0 in
C1 or C5. So category (2) below is a genuine, checked zero, not a truncation (LOG-5).

Run from the repo root:

    python analysis/bl-305-clutter-uv/eligibility.py [--extracted PATH] [--chapters C1,C5] [--verbose]

In a worktree, extracted/ does not exist (git-ignored) -- pass
`--extracted Z:/CSVM/extracted` (never a junction/symlink into the main checkout; see CLAUDE.md).

Live-build reconciliation (METHOD-15): run

    GODOT --path CSVM res://scenes/Main.tscn --headless --log-file <log> -- --freecam --chapter=C1

then

    python analysis/bl-305-clutter-uv/eligibility.py --reconcile-log <log> --reconcile-chapter C1

which parses the engine's `clutter: N sprites ... (kind ×count, ...)` line
(`CSVM/src/Mech3/WorldSession.cs:158-170`) and diffs it against this script's own remake-side
placement simulation for that chapter, kind by kind.

Exit code: 0 if every requested chapter loaded and (when asked) the live reconciliation matched;
1 otherwise.
"""
import argparse
import json
import math
import os
import re
import sys
from collections import defaultdict

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]

# mech3ax NodeFlags::ACTIVE = 1 << 2 (node+0x24 bit 2) -- tools/mech3ax/crates/api-types/src/gamez/nodes.rs
NODE_FLAG_ACTIVE_BIT = 2
# The bit FUN_004de460 tests at node+0x2c (mech3ax's `unk044`/`update_flags`), raw, undecoded.
NODE_UPDATE_FLAG_GATE = 0x4000000
# Polygon flag 0x800 = docs/formats/gamez.md's `unk3` (OpenFlight SUBFACE).
POLY_FLAG_SUBFACE = 0x800
NODE_TYPES_ELIGIBLE = {"Object3d", "LoD", "Lod"}  # extraction spells it "Lod"; kept tolerant

# ClutterBuilder.cs constants (Clutter.cs:78, :123-126) mirrored for the remake-side simulation.
MIN_SLOPE_COS = 0.25
BURIED_CLUTTER_DISTRICTS = {"cblock4", "cblock5", "cblock6"}


# ---------------------------------------------------------------------------------------------
# Loading -- deliberately re-reads the raw JSON from scratch (see module docstring).
# ---------------------------------------------------------------------------------------------

class Poly:
    __slots__ = ("vertex_indices", "tri_strip", "subface", "layer_textures", "uvs_ptr", "has_uv")

    def __init__(self, vertex_indices, tri_strip, subface, layer_textures, uvs_ptr, has_uv):
        self.vertex_indices = vertex_indices
        self.tri_strip = tri_strip
        self.subface = subface
        self.layer_textures = layer_textures  # list[str|None], one per texture layer (materials[])
        self.uvs_ptr = uvs_ptr
        self.has_uv = has_uv


class Mesh:
    __slots__ = ("model_type", "facade_mode", "vertices", "polygons")

    def __init__(self, model_type, facade_mode, vertices, polygons):
        self.model_type = model_type
        self.facade_mode = facade_mode
        self.vertices = vertices  # list[(x,y,z)]
        self.polygons = polygons  # list[Poly]


class Node:
    __slots__ = ("name", "kind", "model_index", "children", "active", "update_flags",
                 "lod_range_min", "local", "world_children", "world_partition_roots")

    def __init__(self):
        self.name = ""
        self.kind = ""
        self.model_index = -1
        self.children = []
        self.active = True
        self.update_flags = 0
        self.lod_range_min = -1.0
        self.local = None  # (r00..r22, tx,ty,tz) or None (identity)
        self.world_children = None
        self.world_partition_roots = None


def _euler_yxz_basis(rx, ry, rz):
    """Matches Godot's Basis.FromEuler(v, EulerOrder.Yxz): R = Ry(y) * Rx(x) * Rz(z),
    exactly as GameZ.cs:ParseTransform builds it for the non-matrix case (radians, as stored)."""
    cx, sx = math.cos(rx), math.sin(rx)
    cy, sy = math.cos(ry), math.sin(ry)
    cz, sz = math.cos(rz), math.sin(rz)
    rz_m = ((cz, -sz, 0.0), (sz, cz, 0.0), (0.0, 0.0, 1.0))
    rx_m = ((1.0, 0.0, 0.0), (0.0, cx, -sx), (0.0, sx, cx))
    ry_m = ((cy, 0.0, sy), (0.0, 1.0, 0.0), (-sy, 0.0, cy))

    def matmul(a, b):
        return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))

    rx_rz = matmul(rx_m, rz_m)
    return matmul(ry_m, rx_rz)


def _parse_transform(tf):
    """Mirrors GameZ.cs:ParseTransform. `tf` is the JSON value under "transform" -- either the
    string "Initial" (identity, handled by the caller) or {"RotateTranslateScale": {...}}."""
    body = tf["RotateTranslateScale"]
    tx, ty, tz = body["translate"]["x"], body["translate"]["y"], body["translate"]["z"]
    original = body.get("original")
    if original is not None:
        def m(legacy, unified):
            return original[legacy] if legacy in original else original[unified]
        basis = (
            (m("a", "r00"), m("b", "r01"), m("c", "r02")),
            (m("d", "r10"), m("e", "r11"), m("f", "r12")),
            (m("g", "r20"), m("h", "r21"), m("i", "r22")),
        )
    else:
        rot = body["rotate"]
        basis = _euler_yxz_basis(rot["x"], rot["y"], rot["z"])
    return (basis, (tx, ty, tz))


def _apply(xf, v):
    basis, t = xf
    x, y, z = v
    return (
        basis[0][0] * x + basis[0][1] * y + basis[0][2] * z + t[0],
        basis[1][0] * x + basis[1][1] * y + basis[1][2] * z + t[1],
        basis[2][0] * x + basis[2][1] * y + basis[2][2] * z + t[2],
    )


def _compose(a, b):
    """a * b (apply b first, then a) -- matches Transform3D's `xf *= local` accumulation order
    used by PlaceOnWorld's Walk (child transform composed onto the parent's)."""
    ab, at = a
    bb, bt = b
    newb = tuple(tuple(sum(ab[i][k] * bb[k][j] for k in range(3)) for j in range(3)) for i in range(3))
    newt = _apply(a, bt)
    return (newb, newt)


IDENTITY = (((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)), (0.0, 0.0, 0.0))


def load_chapter(chapter, extracted_root):
    base = os.path.join(extracted_root, chapter, "gamez")
    with open(os.path.join(base, "nodes.json"), encoding="utf-8") as f:
        raw_nodes = json.load(f)
    with open(os.path.join(base, "models.json"), encoding="utf-8") as f:
        raw_models = json.load(f)
    with open(os.path.join(base, "textures.json"), encoding="utf-8") as f:
        raw_textures = json.load(f)
    with open(os.path.join(base, "materials.json"), encoding="utf-8") as f:
        raw_materials = json.load(f)

    texture_names = [t.get("name") or t.get("original") for t in raw_textures]
    materials = []
    for wrapper in raw_materials:
        if "Textured" in wrapper:
            ti = wrapper["Textured"].get("texture_index")
            materials.append(texture_names[ti] if ti is not None and 0 <= ti < len(texture_names) else None)
        else:
            materials.append(None)

    meshes = []
    dropped_polys_no_verts = 0
    for m in raw_models:
        if not isinstance(m, dict):
            meshes.append(None)
            continue
        verts = [(v["x"], v["y"], v["z"]) for v in (m.get("vertices") or [])]
        polys = []
        for p in m.get("polygons") or []:
            vidx = list(p.get("vertex_indices") or [])
            if len(vidx) < 3:
                dropped_polys_no_verts += 1
                continue
            flags = p.get("flags") or {}
            subface = bool(flags.get("unk3", False))
            tri_strip = bool(flags.get("tri_strip", False))
            mats = p.get("materials") or []
            layer_tex = []
            has_uv = True
            for entry in mats:
                mi = entry.get("material_index", -1)
                tex = materials[mi] if 0 <= mi < len(materials) else None
                layer_tex.append(tex)
                if entry.get("uv_coords") is None:
                    has_uv = False
            layer_uvs_ptr = p.get("uvs_ptr", None)
            polys.append(Poly(vidx, tri_strip, subface, layer_tex, layer_uvs_ptr, has_uv))
        meshes.append(Mesh(m.get("model_type"), m.get("facade_mode"), verts, polys))

    nodes = []
    for i, wrapper in enumerate(raw_nodes):
        data = wrapper.get("data")
        assert isinstance(data, dict), f"{chapter} node {i}: not the unified extraction shape"
        kind = next(iter(data))
        body = data[kind]
        n = Node()
        n.name = wrapper.get("name", "")
        n.kind = kind
        n.model_index = wrapper.get("model_index", -1)
        if n.model_index is None:
            n.model_index = -1
        n.children = list(wrapper.get("child_indices") or [])
        flags = wrapper.get("flags") or {}
        n.active = bool(flags.get("active", True))
        n.update_flags = int(wrapper.get("update_flags") or 0)
        if kind in ("Lod", "LoD"):
            n.lod_range_min = body.get("range", {}).get("min", -1.0)
        if kind == "Object3d":
            tf = body.get("transform")
            if tf is not None and tf != "Initial" and isinstance(tf, dict):
                n.local = _parse_transform(tf)
        if kind == "World":
            roots = []
            seen = set()
            for row in body.get("partitions") or []:
                for cell in row:
                    for ref in cell.get("values") or []:
                        idx = ref.get("node_index", ref.get("index"))
                        if idx not in seen:
                            seen.add(idx)
                            roots.append(idx)
                    for idx in cell.get("node_indices") or []:
                        if idx not in seen:
                            seen.add(idx)
                            roots.append(idx)
            n.world_partition_roots = roots
        nodes.append(n)

    return nodes, meshes, materials, dropped_polys_no_verts


def load_interp_templates(extracted_root):
    """Mirrors ClutterBuilder.TemplateNames (Clutter.cs:177-199): AddClutterTemplates lines of
    support\\<chapter>\\adjust.gw, per chapter, in file order (duplicates kept -- the dict-key
    overwrite semantics live in the simulation below, not here)."""
    path = os.path.join(extracted_root, "interp.json")
    per_chapter = {c: [] for c in CHAPTERS}
    if not os.path.exists(path):
        return per_chapter, False
    with open(path, encoding="utf-8") as f:
        doc = json.load(f)
    wanted = {f"support\\{c.lower()}\\adjust.gw": c for c in CHAPTERS}
    for script in doc:
        name = (script.get("name") or "").lower()
        chapter = wanted.get(name)
        if chapter is None:
            continue
        for line in script.get("lines") or []:
            parts = (line or "").split()
            if len(parts) >= 2 and parts[0] == "AddClutterTemplates":
                per_chapter[chapter].extend(parts[1:])
    return per_chapter, True


# ---------------------------------------------------------------------------------------------
# Template resolution -- mirrors ClutterBuilder.FindTemplateRoot / FirstWithMesh / GroundInfo /
# ParseTemplate (Clutter.cs:210-222, :558-621).
# ---------------------------------------------------------------------------------------------

def find_template_root(nodes, name):
    is_child = [False] * len(nodes)
    for n in nodes:
        for c in n.children:
            if 0 <= c < len(is_child):
                is_child[c] = True
    lname = name.lower()
    for i, n in enumerate(nodes):
        if not is_child[i] and n.kind == "Object3d" and n.name.lower() == lname:
            return i
    return None


def first_with_mesh(nodes, meshes, idx, include_self=True):
    n = nodes[idx]
    if include_self and 0 <= n.model_index < len(meshes) and meshes[n.model_index] is not None \
            and len(meshes[n.model_index].polygons) > 0:
        return idx
    for c in n.children:
        if 0 <= c < len(nodes):
            found = first_with_mesh(nodes, meshes, c, include_self=True)
            if found is not None:
                return found
    return None


def first_texture(mesh, materials):
    for poly in mesh.polygons:
        if poly.layer_textures and poly.layer_textures[0]:
            return poly.layer_textures[0]
    return None


def classify_billboard(mesh):
    if mesh.model_type is None:
        return None
    if mesh.model_type != "Facade":
        return "none"
    return "billboard" if mesh.facade_mode in ("SphericalY", "CylindricalY", "CylindricalX") else "none"


def ground_info(nodes, meshes, materials, ground_idx):
    n = nodes[ground_idx]
    mesh = meshes[n.model_index]
    tex = first_texture(mesh, materials)
    if tex is None or not mesh.vertices:
        return None
    xs = [v[0] for v in mesh.vertices]
    zs = [v[2] for v in mesh.vertices]
    period = max(max(xs) - min(xs), max(zs) - min(zs))
    if period < 1.0:
        return None
    return {"texture": tex, "period": period, "min_x": min(xs), "min_z": min(zs)}


class Kind:
    __slots__ = ("mesh_index", "label", "solid", "cells")

    def __init__(self, mesh_index, label, solid):
        self.mesh_index = mesh_index
        self.label = label
        self.solid = solid
        self.cells = []  # list[(ox, oz)]


class Template:
    __slots__ = ("ground_texture", "period", "kinds")

    def __init__(self, ground_texture, period):
        self.ground_texture = ground_texture
        self.period = period
        self.kinds = []


def parse_template(nodes, meshes, materials, name):
    root = find_template_root(nodes, name)
    if root is None:
        return None
    ground = first_with_mesh(nodes, meshes, root, include_self=True)
    if ground is None:
        return None
    info = ground_info(nodes, meshes, materials, ground)
    if info is None:
        return None
    template = Template(info["texture"], info["period"])
    kinds_by_mesh = {}
    gnode = nodes[ground]
    for child_idx in gnode.children:
        deco = nodes[child_idx]
        deco_mesh_idx = first_with_mesh(nodes, meshes, child_idx, include_self=False)
        if deco_mesh_idx is None:
            continue
        mesh = meshes[nodes[deco_mesh_idx].model_index]
        local = nodes[deco_mesh_idx].local if nodes[deco_mesh_idx].local else IDENTITY
        # NOTE: the C# reads deco.Local (the CHILD-of-ground node's own transform), not the
        # transform of the node that carries the mesh. In this install's template shape the
        # decoration mesh sits on the deco node itself (FirstWithMesh usually returns child_idx
        # unchanged), so this coincides; flagged in FINDINGS.md if that assumption is ever wrong.
        deco_local = nodes[child_idx].local if nodes[child_idx].local else IDENTITY
        ox = deco_local[1][0] - info["min_x"]
        oz = deco_local[1][2] - info["min_z"]

        mesh_index = nodes[deco_mesh_idx].model_index
        kind = kinds_by_mesh.get(mesh_index)
        if kind is None:
            billboard = classify_billboard(mesh)
            tex = first_texture(mesh, materials)
            if billboard == "billboard" and tex is not None and mesh.vertices:
                kind = Kind(mesh_index, tex, solid=False)
            elif billboard is not None and mesh.polygons and mesh.vertices:
                # IsSolidDecoration: not a sprite card, has real geometry. (SceneBuilder assumed
                # non-null, as in a live collidable build.)
                kind = Kind(mesh_index, deco.name, solid=True)
            else:
                continue  # unusable decoration -- skipped exactly as ParseTemplate skips it
            kinds_by_mesh[mesh_index] = kind
            template.kinds.append(kind)
        kind.cells.append((ox, oz))
    return template if template.kinds else None


# ---------------------------------------------------------------------------------------------
# The remake-side placement SIMULATION -- ports PlaceOnWorld/PlaceOnMesh/PlaceOnTriangle
# (Clutter.cs:324-371, :635-699) faithfully, so its per-kind output is directly comparable to
# ClutterBuilder.Summary from a live build (this script's --reconcile-log check).
# ---------------------------------------------------------------------------------------------

def _round_half_to_even(x):
    # Mathf.RoundToInt wraps .NET's MathF.Round, whose default midpoint rule is round-half-to-
    # EVEN (banker's rounding), not away-from-zero. Python's builtin round() on a float uses the
    # same IEEE 754 round-half-to-even rule, so this is just that -- named explicitly because an
    # away-from-zero version was tried first and reconciliation against a live C5 build (61,326
    # solids matched exactly; sprites were off by exactly 1 in lightpole.tif/poleflare.tif, always
    # together -- one lightpole+poleflare pair shares one grid cell) pointed straight at this.
    return round(x)


def place_on_triangle(template, a, b, c, seen, kind_counts):
    area2 = (b[0] - a[0]) * (c[2] - a[2]) - (c[0] - a[0]) * (b[2] - a[2])
    xz_area = 0.5 * abs(area2)
    if xz_area < 0.5:
        return
    bax, bay, baz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    cax, cay, caz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    cross = (bay * caz - baz * cay, baz * cax - bax * caz, bax * cay - bay * cax)
    true_area = 0.5 * math.sqrt(cross[0] ** 2 + cross[1] ** 2 + cross[2] ** 2)
    if xz_area < true_area * MIN_SLOPE_COS:
        return

    p = template.period
    min_x, max_x = min(a[0], b[0], c[0]), max(a[0], b[0], c[0])
    min_z, max_z = min(a[2], b[2], c[2]), max(a[2], b[2], c[2])
    gx0, gx1 = math.floor(min_x / p), math.floor(max_x / p)
    gz0, gz1 = math.floor(min_z / p), math.floor(max_z / p)
    for gx in range(gx0, gx1 + 1):
        for gz in range(gz0, gz1 + 1):
            for kind in template.kinds:
                for ox, oz in kind.cells:
                    px, pz = gx * p + ox, gz * p + oz
                    if px < min_x or px > max_x or pz < min_z or pz > max_z:
                        continue
                    w0 = (b[0] - px) * (c[2] - pz) - (c[0] - px) * (b[2] - pz)
                    w1 = (c[0] - px) * (a[2] - pz) - (a[0] - px) * (c[2] - pz)
                    w2 = (a[0] - px) * (b[2] - pz) - (b[0] - px) * (a[2] - pz)
                    if area2 > 0:
                        if w0 < 0 or w1 < 0 or w2 < 0:
                            continue
                    else:
                        if w0 > 0 or w1 > 0 or w2 > 0:
                            continue
                    key = (kind.mesh_index, _round_half_to_even(px * 4.0), _round_half_to_even(pz * 4.0))
                    if key in seen:
                        continue
                    seen.add(key)
                    kind_counts[(kind.label, kind.solid)] += 1


def simulate_remake(nodes, meshes, materials, chapter_template_names, world_name="world1"):
    """Full port of ClutterBuilder.Build -> PlaceOnWorld -> PlaceOnMesh -> PlaceOnTriangle.
    Returns (sprite_count, solid_count, per_kind_counts dict[(label,solid)->int]) or None if the
    chapter registers no templates that resolve."""
    templates = {}
    for name in chapter_template_names:
        t = parse_template(nodes, meshes, materials, name)
        if t is not None:
            templates[t.ground_texture.lower()] = t
    if not templates:
        return None

    world_idx = None
    for i, n in enumerate(nodes):
        if n.kind == "World" and n.name.lower() == world_name.lower():
            world_idx = i
            break
    if world_idx is None:
        return None
    world = nodes[world_idx]

    seen = set()
    kind_counts = defaultdict(int)

    def skip_world_node(n):
        nm = n.name.lower()
        return nm in ("horizon", "dzpaths") or nm.startswith("fvol")

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes):
            return
        n = nodes[idx]
        if skip_world_node(n):
            return
        if n.kind in ("Lod", "LoD") and n.lod_range_min != 0.0:
            return
        if n.local is not None:
            xf = _compose(xf, n.local)
        if 0 <= n.model_index < len(meshes) and meshes[n.model_index] is not None:
            mesh = meshes[n.model_index]
            wverts = [_apply(xf, v) for v in mesh.vertices]
            for poly in mesh.polygons:
                if not poly.layer_textures:
                    continue
                tex = poly.layer_textures[0]  # remake reads ONLY the base material -- BL-305/A3 gap
                if tex is None:
                    continue
                template = templates.get(tex.lower())
                if template is None:
                    continue
                vidx = poly.vertex_indices
                nv = len(vidx)
                if poly.tri_strip:
                    tris = [(wverts[vidx[i]], wverts[vidx[i + 1]], wverts[vidx[i + 2]]) for i in range(nv - 2)]
                else:
                    tris = [(wverts[vidx[0]], wverts[vidx[i]], wverts[vidx[i + 1]]) for i in range(1, nv - 1)]
                for a, b, c in tris:
                    place_on_triangle(template, a, b, c, seen, kind_counts)
        for c in n.children:
            walk(c, xf)

    for c in world.children:
        walk(c, IDENTITY)
    for idx in (world.world_partition_roots or []):
        walk(idx, IDENTITY)

    sprite_count = sum(v for (label, solid), v in kind_counts.items() if not solid)
    solid_count = sum(v for (label, solid), v in kind_counts.items() if solid)
    return sprite_count, solid_count, dict(kind_counts)


# ---------------------------------------------------------------------------------------------
# Category (1)/(2): original-side polygon-layer eligibility census (FUN_004de460/de2c0/de190).
# ---------------------------------------------------------------------------------------------

def original_eligible_walk(nodes, world_idx):
    """Every node reached from world.children + world.partition_roots (recursive, undeduplicated
    -- matches WorldBuilder's own root gathering, `analysis/collider-probe/probe.py`). Yields
    (node_index, gate_passed) for every node visited; the gate is FUN_004de460 applied to THAT
    node only -- recursion continues into children regardless of whether the node itself passes,
    since the decompile describes node eligibility, not subtree pruning, and no evidence pins down
    whether the original also prunes recursion on a failed gate (documented limitation, see
    FINDINGS.md)."""
    world = nodes[world_idx]
    roots = list(world.children) + list(world.world_partition_roots or [])
    visited_order = []

    def walk(idx):
        if idx < 0 or idx >= len(nodes):
            return
        n = nodes[idx]
        gate = (n.kind in NODE_TYPES_ELIGIBLE
                and (n.active)  # bit 2 of node+0x24, exposed as flags.active
                and not (n.update_flags & NODE_UPDATE_FLAG_GATE))
        visited_order.append((idx, gate))
        for c in n.children:
            walk(c)

    for r in roots:
        walk(r)
    return visited_order


def census_chapter(chapter, nodes, meshes, materials, dropped_polys, template_ground_textures):
    """template_ground_textures: set of lowercase texture names any REGISTERED template resolves
    to (the full original set -- BuriedClutterDistricts is a remake-only exemption, Clutter.cs
    :108-126, and the original stamps cblock4/5/6 same as any other registered template)."""
    world_idx = None
    for i, n in enumerate(nodes):
        if n.kind == "World" and n.name.lower() == "world1":
            world_idx = i
            break
    if world_idx is None:
        return None

    visited = original_eligible_walk(nodes, world_idx)

    layer_hits = defaultdict(int)     # layer index -> polygon-layer match count
    no_uv_polys = 0
    checked_polys = 0
    gated_out_nodes = 0
    eligible_mesh_polys = 0           # polys actually reached (node gate passed, mesh present)
    gate_eligible_polys = []          # (node_idx, poly): pass de2c0 (not subface, has UV) AND template-textured
    template_textured_any = []        # (node_idx, poly): template-textured on any layer, IGNORING the
                                       # subface gate -- diagnostic only, to prove the geometry pipeline
                                       # finds real coplanar overlaps (it does -- see FINDINGS.md) and
                                       # that the flag gate, not a script bug, is what zeroes category 3

    # NOT deduplicated by node index, on purpose: neither FUN_004de4d0's walk nor
    # ClutterBuilder.PlaceOnWorld's Walk carries a "visited" set, so a node reachable through two
    # paths (present in both world.Children and the partition grid, or shared by two parent
    # subtrees) is genuinely revisited and its polygons genuinely re-processed by both the
    # original and the remake -- confirmed by comparing this count against `remake_layer0_polys`
    # in main(), which is built the same undeduplicated way: they agree exactly wherever the two
    # rule sets' template-texture sets agree (C2, C4 -- no BuriedClutterDistricts exemption), and
    # differ only where the texture sets themselves differ (C5). A per-node dedup here was tried
    # first and produced a lower, WRONG count that could never reconcile against a live build.
    for node_idx, gate in visited:
        n = nodes[node_idx]
        if n.model_index < 0 or n.model_index >= len(meshes) or meshes[n.model_index] is None:
            continue
        if not gate:
            gated_out_nodes += 1
            continue
        mesh = meshes[n.model_index]
        for poly in mesh.polygons:
            checked_polys += 1
            any_layer_textured = any(
                tex is not None and tex.lower() in template_ground_textures
                for tex in poly.layer_textures)
            if any_layer_textured:
                template_textured_any.append((node_idx, poly))
            if poly.subface:
                # FUN_004de2c0 gate: flag 0x800 set -> skip. A subface polygon can NEVER pass
                # this gate, so it can never enter layer_hits/gate_eligible_polys below -- which
                # is exactly what forecloses category (3)'s double-stamp shape structurally (see
                # FINDINGS.md): the SAME bit that marks "subface" is the bit this gate excludes on.
                continue
            if not poly.has_uv:
                no_uv_polys += 1
                continue
            eligible_mesh_polys += 1
            matched_any_layer = False
            for li, tex in enumerate(poly.layer_textures):
                if tex is not None and tex.lower() in template_ground_textures:
                    layer_hits[li] += 1
                    matched_any_layer = True
            if matched_any_layer:
                gate_eligible_polys.append((node_idx, poly))

    return {
        "chapter": chapter,
        "checked_polys": checked_polys,
        "gated_out_nodes": gated_out_nodes,
        "no_uv_polys": no_uv_polys,
        "layer_hits": dict(layer_hits),
        "gate_eligible_polys": gate_eligible_polys,
        "template_textured_any": template_textured_any,
        "dropped_degenerate_polys": dropped_polys,
    }


# ---------------------------------------------------------------------------------------------
# Category (3): coplanar subface/base pairs where BOTH members are template-texture-eligible.
# ---------------------------------------------------------------------------------------------

def _poly_world_tris(nodes, meshes, node_idx, poly, xf_cache):
    n = nodes[node_idx]
    mesh = meshes[n.model_index]
    xf = xf_cache.get(node_idx)
    if xf is None:
        return []
    wverts = [_apply(xf, v) for v in mesh.vertices]
    vidx = poly.vertex_indices
    nv = len(vidx)
    if poly.tri_strip:
        return [(wverts[vidx[i]], wverts[vidx[i + 1]], wverts[vidx[i + 2]]) for i in range(nv - 2)]
    return [(wverts[vidx[0]], wverts[vidx[i]], wverts[vidx[i + 1]]) for i in range(1, nv - 1)]


def _plane_key(tri, quant=1000):
    a, b, c = tri
    ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    length = math.sqrt(nx * nx + ny * ny + nz * nz)
    if length < 1e-9:
        return None
    nx, ny, nz = nx / length, ny / length, nz / length
    d = nx * a[0] + ny * a[1] + nz * a[2]
    # Sign-normalise the normal so a plane and its flip bucket together.
    if (nx, ny, nz) < (0.0, 0.0, 0.0):
        nx, ny, nz, d = -nx, -ny, -nz, -d
    return (round(nx * quant), round(ny * quant), round(nz * quant), round(d * quant))


def build_transform_cache(nodes, world_idx):
    """xf per visited node (world-space, accumulated exactly as PlaceOnWorld's Walk does)."""
    world = nodes[world_idx]
    roots = list(world.children) + list(world.world_partition_roots or [])
    cache = {}

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes) or idx in cache:
            return
        n = nodes[idx]
        nm = n.name.lower()
        if nm in ("horizon", "dzpaths") or nm.startswith("fvol"):
            return
        if n.kind in ("Lod", "LoD") and n.lod_range_min != 0.0:
            return
        if n.local is not None:
            xf = _compose(xf, n.local)
        cache[idx] = xf
        for c in n.children:
            walk(c, xf)

    for r in roots:
        walk(r, IDENTITY)
    return cache


def census_coplanar_pairs(nodes, meshes, node_poly_list, xf_cache):
    """Buckets every template-matched polygon (from census_chapter's `template_matched_polys`)
    by quantized plane. A pair inside one bucket where one member is `unk3` (subface) and the
    other is not is the BL-250 shape -- both are template-texture-eligible AND coplanar, so the
    original would stamp both. Reports pairs, not just bucket sizes, and separately tags whether
    either side's node/mesh falls in a template root whose NAME (not texture) is in
    BuriedClutterDistricts, to avoid the exact name-range mis-attribution BL-250's first census
    made (cblock7 places cb12a/13a/14a -- names in the exempted range, texture root cblock7)."""
    buckets = defaultdict(list)
    for node_idx, poly in node_poly_list:
        tris = _poly_world_tris(nodes, meshes, node_idx, poly, xf_cache)
        if not tris:
            continue
        key = _plane_key(tris[0])
        if key is None:
            continue
        buckets[key].append((node_idx, poly))

    pairs = 0
    subface_polys_in_pairs = 0
    for key, items in buckets.items():
        subs = [it for it in items if it[1].subface]
        bases = [it for it in items if not it[1].subface]
        if subs and bases:
            pairs += len(subs) * len(bases)
            subface_polys_in_pairs += len(subs)
    return pairs, subface_polys_in_pairs


# ---------------------------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------------------------

def resolve_template_ground_textures(nodes, meshes, materials, names):
    """The full set of ground textures every registered template name resolves to, lower-cased.
    Used for the ORIGINAL side of the census (no BuriedClutterDistricts filtering -- the original
    has no such exemption; see Clutter.cs:108-126)."""
    out = set()
    for name in names:
        t = parse_template(nodes, meshes, materials, name)
        if t is not None:
            out.add(t.ground_texture.lower())
    return out


def parse_reconcile_log(path):
    """Extracts the per-kind counts from WorldSession.cs's
    `GD.Print($"clutter: {N} sprites ... ({clutterBuilder.Summary})")` line."""
    with open(path, encoding="utf-8", errors="replace") as f:
        text = f.read()
    m = re.search(r"^clutter: (.*)$", text, re.MULTILINE)
    if not m:
        return None
    line = m.group(1)
    summary_m = re.search(r"\((.*)\)\s*$", line)
    if not summary_m:
        return None
    parts = [p.strip() for p in summary_m.group(1).split(",") if p.strip()]
    counts = {}
    for p in parts:
        pm = re.match(r"^(.*)\s*×\s*(\d+)$", p)
        if pm:
            # Summed, not overwritten: the SAME label can appear more than once (one entry per
            # TEMPLATE that carries a kind of that texture, e.g. C1's river1/river2 both have a
            # bush1.tif kind) -- this script's own simulation aggregates by label globally too
            # (see simulate_remake's kind_counts), so both sides must use the same reduction.
            label = pm.group(1).strip()
            counts[label] = counts.get(label, 0) + int(pm.group(2))
    return counts


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--extracted", default="extracted",
                     help="path to the extracted/ tree (worktree: pass an absolute path, e.g. Z:/CSVM/extracted)")
    ap.add_argument("--chapters", default=",".join(CHAPTERS))
    ap.add_argument("--verbose", action="store_true")
    ap.add_argument("--reconcile-log", default=None, help="engine log to check against the live Summary line")
    ap.add_argument("--reconcile-chapter", default=None)
    args = ap.parse_args()

    chapters = [c.strip() for c in args.chapters.split(",") if c.strip()]
    interp_per_chapter, interp_ok = load_interp_templates(args.extracted)
    if not interp_ok:
        print(f"WARNING: no interp.json at {os.path.join(args.extracted, 'interp.json')}"
              f" -- no chapter registers any template, every count below will be 0", file=sys.stderr)

    ok = True
    results = {}
    for chapter in chapters:
        nodes_path = os.path.join(args.extracted, chapter, "gamez", "nodes.json")
        if not os.path.exists(nodes_path):
            print(f"{chapter}: missing extraction at {nodes_path}", file=sys.stderr)
            ok = False
            continue
        nodes, meshes, materials, dropped = load_chapter(chapter, args.extracted)
        names_full = interp_per_chapter.get(chapter, [])
        names_remake = [n for n in names_full if n.lower() not in BURIED_CLUTTER_DISTRICTS]

        template_textures_original = resolve_template_ground_textures(nodes, meshes, materials, names_full)

        census = census_chapter(chapter, nodes, meshes, materials, dropped, template_textures_original)
        if census is None:
            print(f"{chapter}: no 'world1' node", file=sys.stderr)
            ok = False
            continue

        world_idx = next(i for i, n in enumerate(nodes) if n.kind == "World" and n.name.lower() == "world1")
        xf_cache = build_transform_cache(nodes, world_idx)
        # The REAL answer: pairs where BOTH members pass FUN_004de2c0 (not subface, has UV) AND
        # are template-textured. Structurally forced to 0 -- see census_chapter's comment -- but
        # computed, not assumed (the plan's own instruction), and cross-checked against the raw
        # diagnostic pass below so a 0 here reads as "the gate did it" and not "the script is
        # broken".
        pairs, subface_in_pairs = census_coplanar_pairs(nodes, meshes, census["gate_eligible_polys"], xf_cache)
        # Diagnostic: the same computation with the subface gate IGNORED, i.e. every polygon
        # that is template-textured on any layer, subface or not. Proves the geometry pipeline
        # finds real coplanar overlaps in this data (cblock1/2/3/7 subfaces over cblock4/5/6
        # bases, CBLOCK-LOD.md) -- if this were also 0, the gated 0 above would be unproven.
        pairs_raw, subface_in_pairs_raw = census_coplanar_pairs(
            nodes, meshes, census["template_textured_any"], xf_cache)

        # Remake-side polygon-eligibility figure (layer 0 only, no active/type gate, but SAME
        # world walk + SkipWorldNode + LOD rule) -- directly comparable to layer_hits[0].
        remake_template_textures = resolve_template_ground_textures(nodes, meshes, materials, names_remake)
        remake_layer0_polys = 0
        for node_idx, gate in original_eligible_walk(nodes, world_idx):  # gate ignored on purpose here
            n = nodes[node_idx]
            if n.model_index < 0 or n.model_index >= len(meshes) or meshes[n.model_index] is None:
                continue
            mesh = meshes[n.model_index]
            for poly in mesh.polygons:
                if not poly.layer_textures:
                    continue
                tex = poly.layer_textures[0]
                if tex and tex.lower() in remake_template_textures:
                    remake_layer0_polys += 1

        sim = simulate_remake(nodes, meshes, materials, names_remake)

        results[chapter] = {
            "registered": names_full,
            "checked_polys": census["checked_polys"],
            "gated_out_nodes": census["gated_out_nodes"],
            "no_uv_polys": census["no_uv_polys"],
            "layer_hits": census["layer_hits"],
            "dropped_degenerate_polys": census["dropped_degenerate_polys"],
            "coplanar_pairs": pairs,
            "coplanar_subface_polys": subface_in_pairs,
            "coplanar_pairs_raw": pairs_raw,
            "coplanar_subface_polys_raw": subface_in_pairs_raw,
            "remake_layer0_polys": remake_layer0_polys,
            "sim": sim,
        }

    print(f"{'chapter':6} {'templates':9} {'polys_chk':9} {'no_uv':6} {'l0':6} {'l1+':6} "
          f"{'remake_l0':9} {'coplan_gated':12} {'coplan_raw':10} {'sim_sprites':11} {'sim_solid':9}")
    for chapter in chapters:
        r = results.get(chapter)
        if r is None:
            print(f"{chapter:6} MISSING")
            continue
        l0 = r["layer_hits"].get(0, 0)
        l1plus = sum(v for k, v in r["layer_hits"].items() if k != 0)
        sim = r["sim"]
        sim_sprites = sim[0] if sim else 0
        sim_solid = sim[1] if sim else 0
        print(f"{chapter:6} {len(r['registered']):<9} {r['checked_polys']:<9} {r['no_uv_polys']:<6} "
              f"{l0:<6} {l1plus:<6} {r['remake_layer0_polys']:<9} {r['coplanar_pairs']:<12} "
              f"{r['coplanar_pairs_raw']:<10} {sim_sprites:<11} {sim_solid:<9}")

    if args.verbose:
        for chapter in chapters:
            r = results.get(chapter)
            if r is None:
                continue
            print(f"\n{chapter}:")
            print(f"  registered templates: {r['registered']}")
            print(f"  layer hits: {r['layer_hits']}")
            print(f"  gated-out nodes (active/type/update_flags gate failed): {r['gated_out_nodes']}")
            print(f"  degenerate polys dropped (<3 vertex indices): {r['dropped_degenerate_polys']}")
            print(f"  coplanar pairs, BOTH members passing FUN_004de2c0 (the real answer): {r['coplanar_pairs']}"
                  f" ({r['coplanar_subface_polys']} subface polys involved)")
            print(f"  coplanar pairs, subface gate ignored (diagnostic only): {r['coplanar_pairs_raw']}"
                  f" ({r['coplanar_subface_polys_raw']} subface polys involved)")
            if r["sim"]:
                _, _, kc = r["sim"]
                parts = ", ".join(f"{label} ×{n}" for (label, solid), n in sorted(kc.items()))
                print(f"  simulated remake Summary-equivalent: {parts}")
            else:
                print("  simulated remake Summary-equivalent: (no templates resolved)")

    # ---- live reconciliation (METHOD-15) ----------------------------------------------------
    if args.reconcile_log:
        chapter = args.reconcile_chapter
        if chapter is None or chapter not in results:
            print(f"--reconcile-chapter must name one of {list(results)}", file=sys.stderr)
            ok = False
        else:
            live = parse_reconcile_log(args.reconcile_log)
            if live is None:
                print(f"RECONCILE FAIL: no 'clutter: ... (...)' line found in {args.reconcile_log}", file=sys.stderr)
                ok = False
            else:
                sim = results[chapter]["sim"]
                sim_kc = {}
                if sim:
                    _, _, kc = sim
                    for (label, solid), n in kc.items():
                        sim_kc[label] = sim_kc.get(label, 0) + n
                mismatches = []
                for label in set(live) | set(sim_kc):
                    lv, sv = live.get(label, 0), sim_kc.get(label, 0)
                    if lv != sv:
                        mismatches.append(f"{label}: live={lv} sim={sv}")
                if mismatches:
                    print(f"RECONCILE FAIL ({chapter}): " + "; ".join(mismatches))
                    ok = False
                else:
                    print(f"RECONCILE OK ({chapter}): live and simulated per-kind counts agree "
                          f"({len(live)} kind(s))")

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
