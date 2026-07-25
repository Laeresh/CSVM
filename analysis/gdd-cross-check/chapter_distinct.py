"""Q: are C1/C1B/C1C (and C2/C2B) day/night variants of one terrain, or separate worlds?

Three instruments, because the obvious ones lie (see docs/verification.md rule 81):
  1. landmark node presence      — decisive, cheap
  2. terrain-mesh vertex identity — conservative: identical geometry hashes identically
  3. danger-zone display names    — human-legible corroboration
Plus a demonstration that location.json / map.json cannot discriminate at all.

Cited by docs/formats/spawns.md ("A region's lettered folders are separate worlds").
"""
import sys, os, json, hashlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zrdrlib import path, load, unwrap, pairs, messages, CHAPTERS

PAIRS = [("C1", "C1B"), ("C1", "C1C"), ("C1B", "C1C"), ("C2", "C2B"), ("C1", "C2"), ("C1", "C3")]
LANDMARKS = ["ap_radiotwr", "ap_transmitter", "aphngr01.flt", "apbuild01.flt", "ap_h2otwr.flt",
             "refinery_flare", "litehouse", "ramses", "sghangar", "dz1", "dzpaths"]


def names(ch):
    return {n.get("name") for n in json.load(open(path(ch, "gamez", "nodes.json"), encoding="utf-8"))
            if n.get("name")}


def terrain_hashes(ch):
    """Positional hash of every terrain-flagged mesh. Positions do not change with lighting."""
    nodes = json.load(open(path(ch, "gamez", "nodes.json"), encoding="utf-8"))
    models = json.load(open(path(ch, "gamez", "models.json"), encoding="utf-8"))
    out = set()
    for n in nodes:
        mi = n.get("model_index", -1)
        if mi < 0 or not n.get("flags", {}).get("terrain"):
            continue
        vs = models[mi].get("vertices") or []
        if vs:
            out.add(hashlib.md5(json.dumps(vs).encode()).hexdigest()[:16])
    return out


print("=== 1. landmark node presence ===")
S = {c: names(c) for c in ("C1", "C1B", "C1C", "C2", "C2B")}
print(f"{'node':<18}" + "".join(f"{c:>7}" for c in S))
for p in LANDMARKS:
    print(f"{p:<18}" + "".join(f"{'YES' if p in S[c] else '-':>7}" for c in S))

print("\n=== 2. terrain meshes with byte-identical vertex data ===")
H = {c: terrain_hashes(c) for c in ("C1", "C1B", "C1C", "C2", "C2B", "C3")}
for a, b in PAIRS:
    i = H[a] & H[b]
    print(f"  {a:>3} vs {b:<4} |A|={len(H[a]):>3} |B|={len(H[b]):>3} shared={len(i):>3}"
          f"  ({100 * len(i) / min(len(H[a]), len(H[b])):.1f}% of the smaller)")

print("\n=== 3. danger-zone display names ===")
msgs = messages()
for ch in CHAPTERS:
    p = path(ch, "IA1", "zrdr", "ia.zrd.json")
    d = dict(pairs(unwrap(json.load(open(p, encoding="utf-8")))))
    if "dzones" not in d:
        print(f"  {ch}: no dzones")
        continue
    tg = json.load(open(path(ch, "IA1", "zrdr", "targets.zrd.json"), encoding="utf-8"))
    tg = tg[0] if len(tg) == 1 and isinstance(tg[0], list) else tg
    lookup = {}
    for entry in tg:
        e = {kv[0]: kv[1] for kv in entry if isinstance(kv, list) and len(kv) == 2}
        for n in (e.get("nodes") or []):
            lookup[n] = e.get("description")
    out = []
    for pr in d["dzones"]:
        node = pr[1] if len(pr) > 1 else pr[0]
        out.append(msgs.get(lookup.get(node), "?"))
    print(f"  {ch} ({len(out)}): " + "; ".join(out))

print("\n=== 4. the instruments that CANNOT discriminate ===")
for f in ("location.zrd.json", "map.zrd.json"):
    seen = {}
    for ch in CHAPTERS:
        p = path(ch, "IA1", "zrdr", f)
        raw = json.dumps(json.load(open(p, encoding="utf-8"))) if os.path.exists(p) else "MISSING"
        seen.setdefault(hashlib.md5(raw.encode()).hexdigest()[:8], []).append(ch)
    print(f"  {f}: {len(seen)} distinct contents over 8 chapters -> "
          + " | ".join(",".join(v) for v in seen.values()))
