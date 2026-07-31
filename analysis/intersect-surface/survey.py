"""Survey the gamez node flag `intersect_surface` across every chapter's extracted nodes.json.

The question (BL-206): is there a data signal marking the C3 spiderweb — and anything else the
plane should fly through — as not collision-participating? Run from the repo root:

    python analysis/intersect-surface/survey.py

Reads extracted/<chapter>/gamez/nodes.json (the fork's unified shape, a flat node array whose
`flags` block sits on the node itself). Only mesh-bearing nodes (model_index >= 0) are counted —
a flag on a meshless grouping node builds no collider either way.
"""
import collections
import json
import os
import sys

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def walk(node, visit):
    visit(node)
    for child in node.get("children") or []:
        walk(child, visit)


def main():
    grand_false = collections.Counter()
    grand = collections.Counter()
    inheritance_violations = []

    for chapter in CHAPTERS:
        path = os.path.join("extracted", chapter, "gamez", "nodes.json")
        if not os.path.exists(path):
            print(f"{chapter}: missing extraction at {path}", file=sys.stderr)
            continue
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        nodes = data if isinstance(data, list) else [data]

        counts = collections.Counter()

        def visit(n):
            flags = n.get("flags")
            # Explicit None check: `or -1` would drop the legitimate model_index 0.
            model = n.get("model_index")
            if flags is None or model is None or model < 0:
                return
            key = (flags.get("intersect_surface"), flags.get("intersect_bbox"))
            counts[key] += 1
            grand[key] += 1
            if not flags.get("intersect_surface"):
                grand_false[n.get("name")] += 1

        def any_true_mesh(n):
            flags = n.get("flags")
            model = n.get("model_index")
            if flags is not None and model is not None and model >= 0 \
                    and flags.get("intersect_surface"):
                return True
            return any(any_true_mesh(c) for c in n.get("children") or [])

        def check_inheritance(n):
            flags = n.get("flags")
            if flags is not None and not flags.get("intersect_surface"):
                for c in n.get("children") or []:
                    if any_true_mesh(c):
                        inheritance_violations.append((chapter, n.get("name")))
                        break
            for c in n.get("children") or []:
                check_inheritance(c)

        for n in nodes:
            walk(n, visit)
            check_inheritance(n)
        print(f"{chapter}: {dict(counts)}")

    print(f"\ninstall-wide (surface, bbox) counts: {dict(grand)}")
    print(f"distinct false-flagged names: {len(grand_false)}")
    for name, count in sorted(grand_false.items()):
        print(f"  {name} {count}")
    print(f"\nfalse-flagged nodes with a true-flagged mesh descendant "
          f"(would make subtree-inherited exemption unsafe): {inheritance_violations or 'none'}")


if __name__ == "__main__":
    main()
