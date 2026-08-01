#!/usr/bin/env python3
"""One-shot probe for B5 (BL-061 item 3): dump biggun_flying_parts's CALL_ANIMATION closure
and the anchor roots it needs, then check fly_trail*/`*_trails` presence per chapter.

Read-only, scratch — not part of the standing death-effect-closure instrument.
"""
import collections
import glob
import json
import os

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def definitions(path):
    try:
        doc = json.load(open(path, encoding="utf-8"))
    except Exception:
        return []
    out = []

    def walk(x):
        if isinstance(x, list):
            i = 0
            while i < len(x):
                if x[i] == "ANIMATION_DEFINITION" and i + 1 < len(x) and isinstance(x[i + 1], list):
                    body, kv, j = x[i + 1], {}, 0
                    while j < len(body):
                        kv.setdefault(body[j], []).append(body[j + 1])
                        j += 2
                    out.append(kv)
                    i += 2
                    continue
                walk(x[i])
                i += 1
        elif isinstance(x, dict):
            for v in x.values():
                walk(v)

    walk(doc)
    return out


def called_animations(node):
    found = []
    if isinstance(node, list):
        for i, e in enumerate(node):
            if e == "CALL_ANIMATION" and i + 1 < len(node) and isinstance(node[i + 1], list):
                b = node[i + 1]
                for j in range(0, len(b) - 1, 2):
                    if b[j] == "NAME":
                        found.append(b[j + 1][0])
            found += called_animations(e)
    elif isinstance(node, dict):
        for v in node.values():
            found += called_animations(v)
    return found


def object_connectors(kv):
    """PUFFER_STATE / other AT_NODE-bearing objects, so we can see which node each rides."""
    out = []

    def walk(x):
        if isinstance(x, list):
            i = 0
            while i < len(x):
                if x[i] in ("OBJECT_ACTIVE_STATE", "PUFFER_STATE") and i + 1 < len(x) and isinstance(x[i + 1], list):
                    b, kv2, j = x[i + 1], {}, 0
                    while j < len(b):
                        kv2.setdefault(b[j], []).append(b[j + 1])
                        j += 2
                    out.append((x[i], kv2.get("NODE", [[None]])[0][0]))
                walk(x[i])
                i += 1
        elif isinstance(x, dict):
            for v in x.values():
                walk(v)

    walk(kv)
    return out


def main(extracted="extracted"):
    by_anim = collections.defaultdict(list)
    for path in glob.glob(os.path.join(extracted, "zrdr", "*.zrd.json")):
        for kv in definitions(path):
            anim = kv.get("ANIMATION_NAME", [[None]])[0][0]
            name = kv.get("NAME", [[None]])[0][0]
            by_anim[anim].append((os.path.basename(path), name, kv))

    seen, queue = set(), ["biggun_flying_parts"]
    anchors = collections.defaultdict(set)
    puffer_nodes = set()
    while queue:
        anim = queue.pop()
        if anim in seen:
            continue
        seen.add(anim)
        for file, name, kv in by_anim.get(anim, []):
            anchors[name].add(anim)
            for kind, node in object_connectors(kv):
                if node:
                    puffer_nodes.add(node)
            queue += called_animations(kv)

    print(f"biggun_flying_parts closure: {len(seen)} anim name(s): {sorted(seen)}")
    print(f"anchor roots needed: {sorted(anchors)}")
    print(f"PUFFER_STATE/OBJECT_ACTIVE_STATE NODE refs seen: {sorted(puffer_nodes)}")

    print("\nper-chapter presence of anchor roots + fly_trail1..5 (occurrences / of those, parentless):\n")
    probe_names = sorted(anchors) + [f"fly_trail{i}" for i in range(1, 6)]
    for name in probe_names:
        cells = []
        for ch in CHAPTERS:
            nodes = json.load(open(os.path.join(extracted, ch, "gamez", "nodes.json"), encoding="utf-8"))
            hits = [n for n in nodes if n["name"] == name]
            roots = [n for n in hits if not n["parent_indices"]]
            cells.append(f"{ch}:{len(hits)}/{len(roots)}")
        print(f"  {name:18s} " + "  ".join(cells))


if __name__ == "__main__":
    main()
