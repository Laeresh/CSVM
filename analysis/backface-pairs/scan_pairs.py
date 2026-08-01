"""Census of back-to-back polygon pairs and SHOW_BACKFACE density in the chapter worlds.

A back-to-back pair is two polygons of one model over the SAME vertex indices in opposite
winding order, each with its own material — how the data gives a single-quad prop a front
texture and a back texture (the Hollywood backlot facade screens). Being exactly coplanar,
such a pair cannot be separated by depth bias; only backface culling resolves it, which is
why the world renders with `cullBackfaces: true`.

Read-only: parses `extracted/<CH>/gamez/models.json`. Run from the repo root:
    python analysis/backface-pairs/scan_pairs.py
"""

import json
import os

CHAPTERS = ['C1', 'C1B', 'C1C', 'C2', 'C2B', 'C3', 'C4', 'C5']


def reversed_pairs(polys):
    """Count polygon pairs sharing a vertex set traversed in opposite directions."""
    by_vertex_set = {}
    for poly in polys:
        vi = tuple(poly.get('vertex_indices') or [])
        by_vertex_set.setdefault(frozenset(vi), []).append(vi)

    found = 0
    for vertex_set, loops in by_vertex_set.items():
        if len(loops) < 2 or len(vertex_set) < 3:
            continue
        for a in range(len(loops)):
            for b in range(a + 1, len(loops)):
                first, second = loops[a], loops[b]
                if len(first) != len(second):
                    continue
                # Same cyclic loop reversed: any rotation of the reversal matches.
                rev = tuple(reversed(second))
                if any(rev[i:] + rev[:i] == first for i in range(len(rev))):
                    found += 1
    return found


print(f'{"chapter":8} {"models":>7} {"polys":>7} {"backface":>9} {"pct":>6} '
      f'{"pairs":>6} {"in models":>10}')
for chapter in CHAPTERS:
    path = f'extracted/{chapter}/gamez/models.json'
    if not os.path.exists(path):
        print(f'{chapter:8} (no extraction)')
        continue

    models = json.load(open(path))
    n_models = n_polys = n_backface = n_pairs = n_pair_models = 0
    for model in models:
        polys = model.get('polygons') or []
        if not polys:
            continue
        n_models += 1
        n_polys += len(polys)
        n_backface += sum(1 for p in polys if p['flags'].get('show_backface'))
        pairs = reversed_pairs(polys)
        if pairs:
            n_pairs += pairs
            n_pair_models += 1

    pct = 100 * n_backface / n_polys if n_polys else 0
    print(f'{chapter:8} {n_models:7d} {n_polys:7d} {n_backface:9d} {pct:5.1f}% '
          f'{n_pairs:6d} {n_pair_models:10d}')
