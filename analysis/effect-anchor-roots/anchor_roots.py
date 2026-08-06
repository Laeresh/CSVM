#!/usr/bin/env python3
"""Which gamez template roots does the world-effects runtime's bound name set need?

Read-only. Walks the CALL_ANIMATION closure of the impact/destruction effect names
(WorldEffectsFactory.EffectAnimNames) through extracted/zrdr/*.zrd.json, collects each
reached definition's NAME (which is the gamez node its instance anchors on), and diffs
that against the staged set. A root left out leaves every def anchored on it unanchored,
so it plays nothing — nothing renders, nothing emits, and nothing reports an error.

Also checks each needed root exists as a single parentless root in all 8 chapters.

    python analysis/effect-anchor-roots/anchor_roots.py [extracted_dir] [anim_name ...]

With no anim names it answers the original D31 question (the rocket/ordnance IMPACT half of
WorldEffectsFactory.EffectAnimNames). Name any definitions to ask the same question of another
staged set — `player_crash_dirt player_crash_water` is the per-player crash rig's.
"""
import collections
import glob
import json
import os
import sys

# WorldEffectsFactory.EffectAnimNames, rocket/ordnance IMPACT half (the gun *_gunhit family
# and the destruction/damage-stage names are bound too; they anchor on already-staged roots).
ROOT_NAMES = [
    "large_fireball", "small_fireball", "he_ground_effect", "ap_ground_effect", "flak_effect",
    "flash_effect", "sonic_ground_effect", "scatter_effect", "torpedo_ground_effect",
    "rear_flash_effect", "torpedo_water_effect",
]

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def definitions(path):
    """Every ANIMATION_DEFINITION in a reader file, as {key: [values]} dicts."""
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
    """Every CALL_ANIMATION NAME anywhere inside a definition."""
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


def main(extracted, roots=None):
    root_names = list(roots) if roots else ROOT_NAMES
    by_anim = collections.defaultdict(list)
    for path in glob.glob(os.path.join(extracted, "zrdr", "*.zrd.json")):
        for kv in definitions(path):
            anim = kv.get("ANIMATION_NAME", [[None]])[0][0]
            name = kv.get("NAME", [[None]])[0][0]
            # A def with no ANIMATION_NAME is still callable by its NAME (a CALL_ANIMATION's
            # NAME targets whichever field the def declares) — mirrors AnimDefs' `AnimName ??=
            # Name`. Keying by ANIMATION_NAME alone dropped ballflare.flt (NAME-only) into a
            # bucket no CALL_ANIMATION ever asks for.
            by_anim[anim or name].append((os.path.basename(path), name, kv))

    seen, queue = set(), list(root_names)
    anchors = collections.defaultdict(set)
    while queue:
        anim = queue.pop()
        if anim in seen:
            continue
        seen.add(anim)
        for _file, name, kv in by_anim.get(anim, []):
            anchors[name].add(anim)
            queue += called_animations([v for v in kv.values()])

    print(f"{len(seen)} animation name(s) reachable from {len(root_names)} root(s): "
          + ", ".join(root_names))
    print(f"{len(anchors)} distinct anchor root(s) needed:\n")
    for name in sorted(anchors):
        print(f"  {name:18s} {', '.join(sorted(anchors[name]))}")

    print("\nper-chapter presence (occurrences / of those, parentless roots):\n")
    for name in sorted(anchors):
        cells = []
        for ch in CHAPTERS:
            nodes = json.load(open(os.path.join(extracted, ch, "gamez", "nodes.json"), encoding="utf-8"))
            hits = [n for n in nodes if n["name"] == name]
            parentless = [n for n in hits if not n["parent_indices"]]
            cells.append(f"{ch}:{len(hits)}/{len(parentless)}")
        print(f"  {name:18s} " + "  ".join(cells))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "extracted", sys.argv[2:])
