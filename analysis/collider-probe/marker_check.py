"""One-off diagnostic (not part of the probe): counts marker-gizmo nodes that the collision
walk reaches while still collidable (i.e. NOT already exempted by NoCollisionNode for an
unrelated reason, such as being classified a billboard). Checks the hypothesis that the
2026-07-22 off-by-6/11 gap (archived development log) was these nodes still building a collider,
closed by 7c82b80 (2026-08-01, "Level-editor marker gizmos no longer render")."""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from probe import (CHAPTERS, load_chapter, no_collision_node, is_marker_gizmo, skip_world_node)


def walk_markers(node_idx, nodes, meshes, materials, collidable, hits):
    if node_idx < 0 or node_idx >= len(nodes):
        return
    node = nodes[node_idx]
    if skip_world_node(node):
        return
    if node.kind == "Lod" and node.lod_range_min != 0:
        return
    if collidable and no_collision_node(node, meshes, materials):
        collidable = False
    if collidable and is_marker_gizmo(node, meshes, materials):
        hits.append(node.name)
    for child in node.children:
        walk_markers(child, nodes, meshes, materials, collidable, hits)


def main():
    for chapter in CHAPTERS:
        nodes, meshes, materials = load_chapter(chapter)
        world_idx = next(i for i, n in enumerate(nodes) if n.kind == "World" and n.name.lower() == "world1")
        world = nodes[world_idx]
        roots = list(world.children) + list(world.partition_roots or [])
        hits = []
        for idx in roots:
            if idx < 0 or idx >= len(nodes) or not nodes[idx].active:
                continue
            walk_markers(idx, nodes, meshes, materials, True, hits)
        print(f"{chapter:6} {len(hits):3}  {hits}")


if __name__ == "__main__":
    main()
