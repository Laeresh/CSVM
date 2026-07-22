"""Budget + regression checks for a redesigned tie-break.

(a) how many (material, priority) surface groups a mesh can have -> how many surface-rank
    slots an UNCAPPED within-mesh tie-break would need
(b) whether the known regression guards (C4 `g1612` 477 m cliff, C1 `a6` airfield tile,
    C1C having no World-child mesh) are touched by a conflict-graph rule
(c) the slot arithmetic for a dense cross-node rank at several candidate step sizes
"""
import sys, os
from collections import defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from item9_lib import *
from item9_withinmesh import surface_ranks, CAP

print("=== (a) surface groups per built mesh (uncapped rank requirement) ===")
worst = {}
for c in CHAPTERS:
    ch = Chapter(c)
    seen, hist = set(), defaultdict(int)
    for idx, xf in ch.built_nodes():
        mi = model_index(ch.nodes[idx])
        if mi is None or mi < 0 or mi >= len(ch.models) or not isinstance(ch.models[mi], dict):
            continue
        if mi in seen:
            continue
        seen.add(mi)
        _, n = surface_ranks(ch.models[mi])
        hist[n] += 1
    mx = max(hist)
    over = sum(v for k, v in hist.items() if k > CAP + 1)
    worst[c] = mx
    print(f"  {c:4s} meshes {len(seen):5d}  max groups {mx:3d}  "
          f"meshes whose rank saturates the cap ({CAP}): {over:4d} "
          f"({over/max(len(seen),1):.1%})")
print(f"  -> an UNCAPPED surface rank needs up to {max(worst.values())} slots "
      f"(worst chapter {max(worst, key=worst.get)})")

print()
print("=== (b) the recorded regression guards ===")
for c, names in (("C4", ["g1612"]), ("C1", ["a6"]), ("C1C", [])):
    ch = Chapter(c)
    ch.walk_roots()
    wc = set(ch.world_children)
    meshy = [i for i in wc if model_index(ch.nodes[i]) is not None
             and model_index(ch.nodes[i]) >= 0]
    print(f"  {c}: {len(wc)} World children, {len(meshy)} of them carry a mesh"
          + (f" -> {[ch.nodes[i]['name'] for i in meshy]}" if len(meshy) <= 12 else ""))
    for nm in names:
        hit = [i for i, n in enumerate(ch.nodes) if n["name"] == nm]
        for i in hit:
            n = ch.nodes[i]
            print(f"     {nm}: flat index {i}, parent {n.get('parent_indices')}, "
                  f"model {model_index(n)}, bbox {n.get('model_bbox')}")

print()
print("=== (c) slot arithmetic ===")
print(f"  one priority level = DepthBiasPerLevel = {DEPTH_BIAS_PER_LEVEL:.0e}")
print(f"  measured chain lengths (rank slots needed), all 8 chapters:")
print(f"     with the origin-parked entity pile counted : 27 11 16 16 10 13 20 28  -> max 28")
print(f"     with it excluded (item 4 hides the unplaced): 8  3  0  5  0  5  5  3  -> max 8")
for step in (2e-6, 4e-6, 5e-6, 1e-5, 2e-5):
    slots = DEPTH_BIAS_PER_LEVEL / step
    print(f"  step {step:.0e} ({step/DEPTH_FLOOR:.0f}x the claimed 1e-6 floor): "
          f"{slots:6.0f} slots per priority level  "
          f"| 28 node ranks + 5 surface ranks = {33*step:.2e} "
          f"({'FITS' if 33*step < DEPTH_BIAS_PER_LEVEL else 'OVER BUDGET'})")
print()
print("  today, node_bias = node.Index * 5e-8 spans, per chapter:")
for c in CHAPTERS:
    ch = Chapter(c)
    mx = max(i for i, _ in ch.built_nodes())
    print(f"    {c:4s} max built node index {mx:6d} -> span {mx*NODE_ORDER_BIAS:.2e} "
          f"= {mx*NODE_ORDER_BIAS/DEPTH_BIAS_PER_LEVEL:.2f} priority levels")
