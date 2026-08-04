"""Ad-hoc check: does node-level zone_id (+ id_zone_check flag) correlate with the
cblock1/2/3 vs cblock4/5/6 split? Read-only, not committed as a durable instrument yet."""
import sys, os, json
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "item9-depth-bias"))
import item9_lib as lib

ch = lib.Chapter("C5")

# texture -> set of (zone_id, id_zone_check) seen on nodes whose model has a polygon with that texture
from collections import defaultdict
tex_zone = defaultdict(lambda: defaultdict(int))

for i, n in enumerate(ch.nodes):
    mi = lib.model_index(n)
    if mi is None or mi < 0 or mi >= len(ch.models):
        continue
    mdl = ch.models[mi]
    if not isinstance(mdl, dict):
        continue
    zid = n.get("zone_id")
    flags = n.get("flags") or {}
    izc = flags.get("id_zone_check")
    for p in (mdl.get("polygons") or []):
        mats = p.get("materials")
        matidx = mats[0].get("material_index") if mats else p.get("material_index")
        tex = ch.tex_name(matidx)
        if tex and tex.lower().startswith("cblock"):
            tex_zone[tex.lower()][(zid, izc)] += 1

for tex in sorted(tex_zone):
    print(tex, dict(tex_zone[tex]))
