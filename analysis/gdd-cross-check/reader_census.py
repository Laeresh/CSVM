"""Q: what do the undocumented mission readers actually carry?

Key censuses over the whole install for ia.json, dzones.json, zeppelins.json and egen.json,
plus the structural evidence for reading ia.json's ace_stats as the nine pilot-skill modifiers.

Cited by docs/formats/spawns.md, docs/formats/missions.md and docs/formats/mission-entities.md.
"""
import sys, os, json, glob, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zrdrlib import EXTRACTED, path, load, unwrap, instances, pairs, asdict, vehicle_defs, CHAPTERS


def rd(p):
    return unwrap(json.load(open(p, encoding="utf-8")))


def every(name):
    return sorted(glob.glob(os.path.join(EXTRACTED, "*", "*", "zrdr", name)))


def short(p):
    return p[len(EXTRACTED):].strip(os.sep)


print("=== ia.json — key presence over the 8 IA1 missions ===")
per, order = {}, []
for ch in CHAPTERS:
    per[ch] = dict(pairs(rd(path(ch, "IA1", "zrdr", "ia.zrd.json"))))
    for k in per[ch]:
        if k not in order:
            order.append(k)
for k in order:
    have = [c for c in CHAPTERS if k in per[c]]
    print(f"  {k:<22} {len(have)}/8" + ("" if len(have) == 8 else f"  missing: {set(CHAPTERS) - set(have)}"))

print("\n  group1 shape:", json.dumps(per["C1"]["group1"]))
print("  ace block:   ", {k: per["C1"][k] for k in per["C1"] if k.startswith("ace_")})

print("\n=== ace_stats: 9 values — is there a 9-key skill run in vehicle.json? ===")
defs = vehicle_defs()
SKILLS = {"dare_devil", "natural_touch", "sixth_sense", "dead_eye", "quick_draw",
          "steady_hand", "stun_recovery", "talker", "constitution"}
orders = collections.Counter()
for name, props in defs.items():
    ks = tuple(k for k, _ in pairs(props) if k in SKILLS)
    if ks:
        orders[ks] += 1
for ks, n in orders.items():
    print(f"  {n} defs emit: {ks}")
vals = {tuple(per[c]["ace_stats"]) for c in CHAPTERS}
print(f"  distinct ace_stats across all 8 chapters: {vals}  <- uniform, so ORDER IS INFERRED")

print("\n=== dzones.json (per-mission zone overrides) ===")
files = every("dzones.zrd.json")
keys = collections.Counter()
nums = []
for f in files:
    d = dict(pairs(rd(f)))
    keys.update(d.keys())
    nums += [e[1] for e in (d.get("objective_numbers") or [])]
print(f"  {len(files)} files; key presence {dict(keys)}")
print(f"  objective_numbers span {min(nums):g}-{max(nums):g}, {len(set(nums))} distinct values")
for f in files[:3]:
    d = dict(pairs(rd(f)))
    print(f"    {short(f):<32} objs={len(d.get('objective_numbers') or [])}"
          f" disable={len(d.get('disable') or [])} nosnapshot={len(d.get('nosnapshot') or [])}")

print("\n=== zeppelins.json ===")
files = every("zeppelins.zrd.json")
keys, order, ninst, samples = collections.Counter(), [], 0, {}
fieldconst = collections.defaultdict(collections.Counter)
for f in files:
    for inst in instances(json.load(open(f, encoding="utf-8"))):
        ninst += 1
        for k, v in pairs(inst):
            keys[k] += 1
            samples.setdefault(k, json.dumps(v)[:110])
            if k not in order:
                order.append(k)
        d = dict(pairs(inst))
        for c in (d.get("cannon_health") or []):
            fieldconst["cannon_health[1]"][c[1]] += 1
            fieldconst["cannon_health[2]"][c[2]] += 1
            fieldconst["cannon_health hp"][c[4]] += 1
        for h in (d.get("healthy") or []):
            fieldconst["healthy[1]"][h[1] if len(h) > 1 else None] += 1
print(f"  {len(files)} files, {ninst} zeppelin instances")
for k in order:
    print(f"    {k:<22} {keys[k]:>3}/{ninst}   e.g. {samples[k]}")
for k, c in fieldconst.items():
    print(f"    constant check {k}: {dict(c)}")

print("\n=== egen.json (enemy generators) ===")
files = every("egen.zrd.json")
keys, order, n, empty, samples = collections.Counter(), [], 0, 0, collections.defaultdict(collections.Counter)
for f in files:
    insts = [i for i in instances(json.load(open(f, encoding="utf-8"))) if i != [None]]
    if not insts:
        empty += 1
    for inst in insts:
        n += 1
        for k, v in pairs(inst):
            keys[k] += 1
            samples[k][json.dumps(v)[:46]] += 1
            if k not in order:
                order.append(k)
print(f"  {len(files)} files, {n} generators ({empty} empty [null] files)")
for k in order:
    print(f"    {k:<14} {keys[k]:>3}/{n}  values: {dict(samples[k].most_common(5))}")
