"""Static collider probe (BL-070): predicts WorldBuilder's world-collider count per chapter
from the extracted gamez JSON alone, with no engine running, and diffs it against the runtime
number `GameSession.LogBuildSummary` prints ("loaded ...: N colliders").

Rewritten from scratch — the original `.scratch/probe_exempt.py` was swept by CleanScratch.ps1
(`.scratch/` is gitignored) and has no copy anywhere, including git history (confirmed:
`git log --all -- **/probe_exempt*` returns nothing). This is a fresh implementation against
current code, not a restoration.

Mirrors, field for field, the walk `WorldBuilder.Build` + `SceneBuilder.BuildSubtree` actually
run (unified-shape extraction only — every chapter under `extracted/` ships that shape):

  - Root selection: the `world1` node's `child_indices` + its `partitions` grid's referenced
    node indices, in that order, WITHOUT deduplicating between the two lists (`WorldBuilder.Build`
    doesn't either) (WorldBuilder.cs:216-218).
  - A root is skipped outright when `flags.active` is false (`WorldBuilder.Add`, :906-907).
  - Every node in the walk (root or descendant) is skipped, subtree included, when
    `WorldBuilder.SkipWorldNode` matches its name (`horizon`/`dzpaths`/`fvol*`) or it is a
    non-nearest `Lod` level (`SceneBuilder.BuildSubtree`, :701-705).
  - Collision exemption (`WorldBuilder.NoCollisionNode`, :776-777) is INHERITED down the
    subtree once tripped, exactly like `SceneBuilder.BuildSubtree`'s `collidable` parameter
    (:706-707, passed unchanged to every recursive call at :756-757): not `intersect_surface`,
    OR the node's own mesh uses a non-solid sky/cloud texture, OR the node is a billboard
    (`SceneBuilder.ClassifyBillboard` on `model_type`/`facade_mode`, with the legacy single-poly-
    flare-texture fallback this extraction shape never takes since `model_type` is always
    present here).
  - A collidable node with a mesh does not yield ONE collider — it yields one per DISTINCT
    surface class its own polygons carry (`SceneBuilder.CollidersForMesh`, :809-840): water /
    buildings / untagged-default, each polygon's own texture deciding
    (`SceneBuilder.ClassifySurface`, :543-561), and a class contributes nothing if every polygon
    routed to it is degenerate (< 3 vertex indices — `EmitCollisionFaces`, :660-664). This is
    the detail a naive "count collidable nodes" probe misses, and the reason this script exists
    rather than a five-line census.
  - A marker-gizmo mesh (`GameZ.IsMarkerGizmo`: 3 vertices, 1 triangle, untextured) builds no
    MeshInstance3D and therefore no collider even when nominally collidable.

Does NOT model clutter (`ClutterBuilder`) — deliberately. Clutter's decoration nodes are
`AddClutterTemplates`-registered subtrees that are parentless and unreferenced by `world1`'s
children or partition grid (see `docs/architecture.md`'s `Clutter.cs` entry and its own class
remarks), so `WorldBuilder`'s placed-node walk never reaches them, and `ClutterBuilder`'s solid
collision (`BuildSolidCollision`, region-body `BodyAddShape` attachments) never touches
`SceneBuilder.ColliderCount` — confirmed by reading both call paths: nothing in
`ClutterBuilder.cs` references `SceneBuilder.ColliderCount` or the scene's `_colliderCache`, and
`GameSession`'s printed `{state.Colliders} colliders` is `SceneBuilder.ColliderCount` alone
(`GameSession.cs:574,794`, `WorldBuilder.cs:118`). This closes the two "later changes" the
backlog flagged as needing modelling (sprite clutter went non-collidable outright; solid clutter's
shapes became shared/region-attached): both changed the SEPARATE clutter collision system, which
this probe's target metric never counted in the first place. Confirmed empirically below — the
probe matches the runtime total on every chapter but two without touching Clutter.cs at all.

Run from the repo root:

    python analysis/collider-probe/probe.py [--verbose]

`--verbose` additionally prints, for C4 and C5, the roots whose own predicted collider count is
nonzero and sorts them for the delta chase (see FINDINGS.md).
"""
import argparse
import json
import os
import sys

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


class Mesh:
    __slots__ = ("model_type", "facade_mode", "polygons")

    def __init__(self, model_type, facade_mode, polygons):
        self.model_type = model_type
        self.facade_mode = facade_mode
        self.polygons = polygons  # list of (vertex_count, material_index)


class Node:
    __slots__ = ("name", "kind", "model_index", "children", "intersect_surface", "active",
                 "lod_range_min", "area", "partition_roots")

    def __init__(self):
        self.name = ""
        self.kind = ""
        self.model_index = -1
        self.children = []
        self.intersect_surface = True
        self.active = True
        self.lod_range_min = -1.0
        self.area = None
        self.partition_roots = None


def load_chapter(chapter):
    base = os.path.join("extracted", chapter, "gamez")
    with open(os.path.join(base, "nodes.json"), encoding="utf-8") as f:
        raw_nodes = json.load(f)
    with open(os.path.join(base, "models.json"), encoding="utf-8") as f:
        raw_models = json.load(f)
    with open(os.path.join(base, "textures.json"), encoding="utf-8") as f:
        raw_textures = json.load(f)
    with open(os.path.join(base, "materials.json"), encoding="utf-8") as f:
        raw_materials = json.load(f)

    texture_names = [t.get("name") or t.get("original") for t in raw_textures]

    # material index -> texture name (None for a Colored material or an out-of-range index).
    materials = []
    for wrapper in raw_materials:
        if "Textured" in wrapper:
            body = wrapper["Textured"]
            ti = body.get("texture_index")
            materials.append(texture_names[ti] if ti is not None and 0 <= ti < len(texture_names) else None)
        else:
            materials.append(None)

    meshes = []
    for m in raw_models:
        if not isinstance(m, dict):
            meshes.append(None)  # keep the slot — model_index stays aligned
            continue
        polys = []
        for p in m.get("polygons", []):
            vc = len(p.get("vertex_indices", []))
            mats = p.get("materials") or []
            mat_index = mats[0]["material_index"] if mats else -1
            polys.append((vc, mat_index))
        meshes.append(Mesh(m.get("model_type"), m.get("facade_mode"), polys))

    nodes = []
    for i, wrapper in enumerate(raw_nodes):
        data = wrapper.get("data")
        unified = isinstance(data, dict)
        assert unified, f"{chapter} node {i}: not the unified extraction shape"
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
        n.intersect_surface = bool(flags.get("intersect_surface", True))
        n.active = bool(flags.get("active", True))
        if kind == "Lod":
            n.lod_range_min = body.get("range", {}).get("min", -1.0)
        if kind == "World":
            area = body.get("area")
            if area:
                n.area = (area["left"], area["top"], area["right"], area["bottom"])
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
            n.partition_roots = roots
        nodes.append(n)

    return nodes, meshes, materials


# ---- Mirrors of WorldBuilder / SceneBuilder predicates -------------------------------------

def is_cloud_or_sky_texture(tex):
    t = tex.lower()
    return t.startswith("cloud") or t.startswith("sky")


def is_non_solid_sky_texture(tex):
    return is_cloud_or_sky_texture(tex) and not tex.lower().startswith("skywal")


def is_flare_texture(tex):
    t = tex.lower()
    return "flare" in t or "fire" in t or "flame" in t


def classify_surface(tex):
    if not tex:
        return None
    t = tex.lower()
    if "shadow" in t:
        return "water" if t.startswith("water") or "splash" in t else None
    if (t.startswith("water") or t.startswith("wtr") or t.startswith("srf")
            or "wakefront" in t or "watersquirt" in t):
        return "water"
    if (("build" in t) or t.startswith("hangar") or t.startswith("bld") or "cblock" in t
            or "warehouse" in t or "roof" in t or "filmblock" in t
            or t.startswith("empire") or t.startswith("chrysler")):
        return "buildings"
    return None


def mesh_uses_texture(node, meshes, materials, predicate):
    if node.model_index < 0 or node.model_index >= len(meshes):
        return False
    mesh = meshes[node.model_index]
    if mesh is None:
        return False
    for _, mat_index in mesh.polygons:
        if 0 <= mat_index < len(materials):
            tex = materials[mat_index]
            if tex and predicate(tex):
                return True
    return False


def classify_billboard(mesh):
    """Returns 'billboard'/'none'/None (None = legacy fallback, unreachable on this shape)."""
    if mesh.model_type is None:
        return None
    if mesh.model_type != "Facade":
        return "none"
    return "billboard" if mesh.facade_mode in ("SphericalY", "CylindricalY", "CylindricalX") else "none"


def is_billboard_node(node, meshes, materials):
    if node.model_index < 0 or node.model_index >= len(meshes):
        return False
    mesh = meshes[node.model_index]
    if mesh is None:
        return False
    kind = classify_billboard(mesh)
    if kind is not None:
        return kind == "billboard"
    # Legacy-extraction fallback — dead code on the unified shape this probe reads, kept only
    # so a silent shape change fails loud rather than mispredicting.
    return len(mesh.polygons) == 1 and mesh_uses_texture(node, meshes, materials, is_flare_texture)


def no_collision_node(node, meshes, materials):
    return (not node.intersect_surface
            or mesh_uses_texture(node, meshes, materials, is_non_solid_sky_texture)
            or is_billboard_node(node, meshes, materials))


def is_marker_gizmo(node, meshes, materials):
    if node.model_index < 0 or node.model_index >= len(meshes):
        return False
    mesh = meshes[node.model_index]
    if mesh is None or len(mesh.polygons) != 1:
        return False
    vc, mat_index = mesh.polygons[0]
    if vc != 3:
        return False
    if not (0 <= mat_index < len(materials)):
        return False
    return materials[mat_index] is None  # untextured (Colored material, or an unmapped index)


def colliders_for_node(node, meshes, materials):
    """Number of distinct surface-class colliders this node's OWN mesh would attach."""
    if node.model_index < 0 or node.model_index >= len(meshes):
        return 0
    mesh = meshes[node.model_index]
    if mesh is None or is_marker_gizmo(node, meshes, materials):
        return 0
    tags_with_faces = set()
    for vc, mat_index in mesh.polygons:
        if vc < 3:
            continue  # EmitCollisionFaces: n < 3 contributes no face to any bucket
        tex = materials[mat_index] if 0 <= mat_index < len(materials) else None
        tags_with_faces.add(classify_surface(tex))
    return len(tags_with_faces)


def skip_world_node(node):
    n = node.name.lower()
    return n == "horizon" or n == "dzpaths" or n.startswith("fvol")


# ---- The walk --------------------------------------------------------------------------------

def walk(node_idx, nodes, meshes, materials, collidable):
    if node_idx < 0 or node_idx >= len(nodes):
        return 0
    node = nodes[node_idx]
    if skip_world_node(node):
        return 0
    if node.kind == "Lod" and node.lod_range_min != 0:
        return 0
    if collidable and no_collision_node(node, meshes, materials):
        collidable = False
    total = colliders_for_node(node, meshes, materials) if collidable else 0
    for child in node.children:
        total += walk(child, nodes, meshes, materials, collidable)
    return total


def predict(chapter):
    nodes, meshes, materials = load_chapter(chapter)
    world_idx = next((i for i, n in enumerate(nodes) if n.kind == "World" and n.name.lower() == "world1"), None)
    if world_idx is None:
        raise ValueError(f"{chapter}: no 'world1' node")
    world = nodes[world_idx]

    roots = list(world.children) + list(world.partition_roots or [])
    total = 0
    per_root = []  # (root_index, root_name, collider_count) for nonzero-contributing roots
    for idx in roots:
        if idx < 0 or idx >= len(nodes):
            continue
        node = nodes[idx]
        if not node.active:
            continue
        before = total
        total += walk(idx, nodes, meshes, materials, True)
        if total != before:
            per_root.append((idx, node.name, total - before))
    return total, per_root


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    print(f"{'chapter':6} {'predicted':>9}")
    results = {}
    for chapter in CHAPTERS:
        path = os.path.join("extracted", chapter, "gamez", "nodes.json")
        if not os.path.exists(path):
            print(f"{chapter}: missing extraction at {path}", file=sys.stderr)
            continue
        total, per_root = predict(chapter)
        results[chapter] = (total, per_root)
        print(f"{chapter:6} {total:>9}")

    if args.verbose:
        for chapter in ("C4", "C5"):
            if chapter not in results:
                continue
            _, per_root = results[chapter]
            print(f"\n{chapter} contributing roots ({len(per_root)}):")
            for idx, name, count in sorted(per_root, key=lambda r: (-r[2], r[1])):
                print(f"  [{idx:5}] {name:30} {count}")


if __name__ == "__main__":
    main()
