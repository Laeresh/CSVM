"""Joins materials -> polygons -> meshes by the material's `soil` field (the engine's numeric
surface type id, per FINDINGS.md 2026-08-11) and reports, per chapter, polygon count and
triangulated area per id -- plus the node names of the meshes that carry each id. Answers
PLAN-crash-surface-id A1: what would faithful `player_crash_<name>` selection actually
hit, and how much of each chapter falls back to slot 0 (`player_crash_default`)?

Distinct from class_area_share.py, which answers the analogous question for
`SceneBuilder.ClassifySurface`'s TEXTURE-NAME classifier -- a different name space that drives
the weapon IMPACT table, not the crash/touchdown table. Conflating the two is the exact mistake
this plan exists to undo (see the plan's Constraints), so this script never imports classify()
and never reads texture names.

soil label -> id comes from soil_id_probe.py's cross-chapter measurement (identical in all 8
chapters / 4,715 materials, no label ever taking two ids). Names for ids >= 6 come from
soils_list.py's recovery of ZBD/zrdr.zbd's soils list. Ids {0, 5, 8, 11, 12} name a
player_crash_<name> def that does not exist in the shipped install (only _default/_dirt/_water
ship) and so fall back to vector[0] under the original's cascade (FUN_0048b920) -- that fallback
set is what "slot 0 share" totals below.

Triangulation matches SceneBuilder.PolygonArea/EmitCollisionFaces exactly: strip order for
tri_strip polygons (successive overlapping triangles), a fan from vertex 0 otherwise -- a strip's
raw index list is not an outline, so naively fanning it (as class_area_share.py's simplified
area() does) measures the wrong shape for those polygons. This does NOT reuse that function's
code for that reason; it reuses its triangle cross-product area math only.

"Collidable area" here means every polygon of every mesh (= a models.json entry, 1:1 with
SceneBuilder's mesh index -- GameZ.cs's ParseMeshes remark) in a chapter's gamez, counted ONCE per
mesh regardless of how many nodes place it in the world. This is the same convention
class_area_share.py uses, so its water/buildings area-share numbers are directly comparable to
this script's id-1/id-13 numbers. It does not weight by node instance count, so a mesh placed by
clutter many times (e.g. a shared building model) counts once here, same as there.

Absolute extracted/ path on purpose: it is git-ignored and this must run from a worktree.
Read-only; writes nothing.

    python analysis/surface-classification/soil_area_by_mesh.py [CHAPTER ...]
"""
import collections
import json
import sys
from pathlib import Path

EXTRACTED = Path(r"Z:\CSVM\extracted")
CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]

# soil label -> surface type id (soil_id_probe.py, all 8 chapters / 4,715 materials, 1:1 and
# stable everywhere). id -> registry name (soils_list.py / FINDINGS.md 2026-08-11).
SOIL_ID = {"Default": 0, "Water": 1, "Fire": 5, "Grass": 8, "Mech": 11, "Silt": 12, "NoSlip": 13}
NAME = {0: "default", 1: "water", 5: "fire", 8: "airstrip", 11: "buildings", 12: "dzone", 13: "dirt"}
# ids naming a player_crash_<name> def that does not exist in the shipped install -- these fall
# back to vector[0] (player_crash_default) under FUN_0048b920's cascade, same as id 0 itself.
FALLBACK_TO_SLOT0 = {0, 5, 8, 11, 12}


def tri_area(vs, a, b, c):
    ax, ay, az = vs[a]["x"], vs[a]["y"], vs[a]["z"]
    bx, by, bz = vs[b]["x"], vs[b]["y"], vs[b]["z"]
    cx, cy, cz = vs[c]["x"], vs[c]["y"], vs[c]["z"]
    ux, uy, uz = bx - ax, by - ay, bz - az
    vx, vy, vz = cx - ax, cy - ay, cz - az
    wx, wy, wz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    return 0.5 * (wx * wx + wy * wy + wz * wz) ** 0.5


def polygon_area(vs, poly):
    """Matches SceneBuilder.PolygonArea: strip order for tri_strip, a fan from vertex 0 otherwise."""
    idx = poly["vertex_indices"]
    n = len(idx)
    if n < 3:
        return 0.0
    total = 0.0
    if poly["flags"]["tri_strip"]:
        for i in range(n - 2):
            total += tri_area(vs, idx[i], idx[i + 1], idx[i + 2])
    else:
        for i in range(1, n - 1):
            total += tri_area(vs, idx[0], idx[i], idx[i + 1])
    return total


def material_soil_ids(chapter):
    """Material index -> surface type id, or None for a soil label this script doesn't know."""
    mats = json.loads((EXTRACTED / chapter / "gamez" / "materials.json").read_text(encoding="utf-8"))
    ids = []
    unknown = collections.Counter()
    for entry in mats:
        _, body = next(iter(entry.items()))
        label = body.get("soil")
        sid = SOIL_ID.get(label)
        if sid is None and label is not None:
            unknown[label] += 1
        ids.append(sid)
    if unknown:
        print(f"  !! unmapped soil labels (not in soil_id_probe.py's table): {dict(unknown)}")
    return ids


def mesh_node_names(chapter):
    """model_index (== mesh index) -> every node name that places it in the world."""
    nodes = json.loads((EXTRACTED / chapter / "gamez" / "nodes.json").read_text(encoding="utf-8"))
    names = collections.defaultdict(list)
    for n in nodes:
        mi = n.get("model_index", -1)
        if mi is not None and mi >= 0:
            names[mi].append(n["name"])
    return names


def run(chapter):
    gamez_dir = EXTRACTED / chapter / "gamez"
    if not gamez_dir.exists():
        print(f"{chapter}: no extracted gamez at {gamez_dir}")
        return None
    models = json.loads((gamez_dir / "models.json").read_text(encoding="utf-8"))
    soil_of = material_soil_ids(chapter)
    node_names = mesh_node_names(chapter)

    poly_count = collections.Counter()
    area = collections.Counter()
    mesh_area = collections.defaultdict(collections.Counter)  # id -> {mesh_index: area}

    for mesh_index, model in enumerate(models):
        vs = model["vertices"]
        for poly in model["polygons"]:
            mats = poly.get("materials") or []
            mi = mats[0]["material_index"] if mats else -1
            sid = soil_of[mi] if 0 <= mi < len(soil_of) else None
            a = polygon_area(vs, poly)
            poly_count[sid] += 1
            area[sid] += a
            if sid is not None:
                mesh_area[sid][mesh_index] += a

    total_area = sum(area.values())
    print(f"\n=== {chapter}: {len(models)} meshes, {sum(poly_count.values())} polygons, "
          f"{total_area:,.0f} area units ===")
    rows = {}
    for sid in sorted(area, key=lambda s: (s is None, s)):
        label = NAME.get(sid, "?") if sid is not None else "(no material)"
        share = 100 * area[sid] / total_area if total_area else 0.0
        top = sorted(mesh_area.get(sid, {}).items(), key=lambda kv: -kv[1])[:5]
        top_desc = []
        for mesh_index, a in top:
            names = node_names.get(mesh_index) or [f"<unplaced mesh {mesh_index}>"]
            extra = f"+{len(names) - 1} more node" if len(names) > 1 else ""
            top_desc.append(f"{names[0]}{extra}(mesh#{mesh_index}, area={a:,.0f})")
        n_meshes = len(mesh_area.get(sid, {}))
        print(f"  id {sid!s:>4} {label:10} polys={poly_count[sid]:6} area={area[sid]:14,.0f} "
              f"({share:5.2f}%) meshes={n_meshes:4}  top: {', '.join(top_desc) or '-'}")
        rows[sid] = {"label": label, "polys": poly_count[sid], "area": area[sid], "share": share,
                     "meshes": n_meshes, "top": top_desc}

    slot0_area = sum(area.get(s, 0.0) for s in FALLBACK_TO_SLOT0)
    slot0_share = 100 * slot0_area / total_area if total_area else 0.0
    print(f"  -> slot 0 (player_crash_default) share: {slot0_share:.2f}% "
          f"(ids {sorted(FALLBACK_TO_SLOT0)} combined, area {slot0_area:,.0f} of {total_area:,.0f})")
    return {"total_area": total_area, "rows": rows, "slot0_share": slot0_share}


def main():
    chapters = sys.argv[1:] or CHAPTERS
    for ch in chapters:
        run(ch)


if __name__ == "__main__":
    main()
