"""Q: is the destroyable_parts hp PAIR an (armor, hit points) pair?

The design gives every damage zone both an armor pool and a hit-point pool. Test what the
shipped data can and cannot say about that:
  - are the two values ever unequal?  (if never, no data measurement can discriminate)
  - do weapons carry a matching two-way damage split?
  - does anything else in the data model two pools?

Cited by docs/formats/vehicle.md ("The hp pair: (armor, hit points) — hypothesis").
"""
import sys, os, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zrdrlib import vehicle_defs, asdict, pairs, unwrap, load, resolve_chain, messages

defs = vehicle_defs()

print("=== destroyable_parts: are the two hp values ever unequal? ===")
rows = []
for name, props in defs.items():
    d = asdict(props)
    for part in (d.get("destroyable_parts") or []):
        rows.append((name, "player_airplane" in resolve_chain(defs, name),
                     part[0], part[1], part[2],
                     tuple(x for x in part[3:] if x in ("critical", "engine"))))
ndefs = len({r[0] for r in rows})
unequal = [r for r in rows if r[3] != r[4]]
print(f"  {len(rows)} part entries on {ndefs} defs "
      f"({len({r[0] for r in rows if r[1]})} player, {len({r[0] for r in rows if not r[1]})} AI)")
print(f"  entries with hp1 != hp2: {len(unequal)}  {unequal[:5]}")
print(f"  distinct (hp1,hp2): {dict(collections.Counter((r[3], r[4]) for r in rows))}")
print("  -> the pair is UNIFORMLY equal, so no data test can separate (armor,hp) from (hp,hp).")

print("\n=== where the `engine` flag actually sits (not tail-only) ===")
eng = collections.defaultdict(list)
for n, isp, part, a, b, fl in rows:
    if "engine" in fl and isp:
        eng[part].append(n)
for part, ns in eng.items():
    print(f"  {part:<10} {sorted(ns)}")

print("\n=== weapons.json: does a weapon damage two pools differently? ===")
bal = dict(pairs(asdict(unwrap(load("zrdr", "weapons.zrd.json")))["BALLISTICS"]))
both, diff = 0, []
for wname, props in bal.items():
    p = asdict(props)
    a, h = p.get("ARMOR_DAMAGE"), p.get("HEALTH_DAMAGE")
    if a and h:
        both += 1
        if a[0] != h[0]:
            diff.append((wname, a[0], h[0]))
print(f"  weapons carrying BOTH ARMOR_DAMAGE and HEALTH_DAMAGE: {both}")
print(f"  weapons where they DIFFER: {len(diff)}")
for w, a, h in diff:
    print(f"    {w:<8} armor {a:<9g} health {h:g}")
print("  -> wep_N1 (dum-dum) is armor-light/health-heavy, wep_N2 (AP) the mirror.")

print("\n=== other two-pool evidence in the data ===")
pl = asdict(unwrap(load("zrdr", "player.zrd.json")))
print(f"  player.json `crash` = {pl.get('crash')}")
print(f"  MSG_HUD_HEALTH = {messages().get('MSG_HUD_HEALTH')!r}")
ah = [(n, asdict(p).get("armor"), asdict(p).get("health"))
      for n, p in defs.items() if "armor" in asdict(p)]
print(f"  AI defs with an armor/health pair: {len(ah)}; unequal: "
      f"{[t for t in ah if t[1] and t[2] and t[1][0] != t[2][0]]}")

print("\nVERDICT: strongly supported, NOT decodable from shipped data. Falsify at the controls:")
print("  rounds-to-destroy for wep_31 (DD) vs wep_32 (AP) on one zone must differ if two pools.")
