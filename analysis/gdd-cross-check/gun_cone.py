"""Q: are vehicle.json's gun_pitch/gun_yaw a TURRET arc or the AI's forward-gun cone?

Discriminator: if they were a turret arc they would track `turrets`. Test whether
presence correlates with "has a turret" or with "is an AI def".

Cited by docs/formats/vehicle.md ("Weapons, damage & AI keys").
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from zrdrlib import vehicle_defs, asdict, pairs, resolve_chain, resolve

defs = vehicle_defs()
rows = []
for name, props in defs.items():
    chain = resolve_chain(defs, name)
    own = {k for k, _ in pairs(props)}
    rows.append(dict(
        name=name,
        player="player_airplane" in chain,
        owns_cone="gun_pitch" in own,
        cone=resolve(defs, name, "gun_pitch")[1],
        cone_src=resolve(defs, name, "gun_pitch")[0],
        turret_src=resolve(defs, name, "turrets")[0],
        chain=chain,
    ))

carriers = [r for r in rows if r["owns_cone"]]
players = [r for r in rows if r["player"]]
ai = [r for r in rows if not r["player"]]

print(f"defs={len(rows)}  player={len(players)}  ai/other={len(ai)}")
print(f"\ndefs that OWN gun_pitch: {len(carriers)}")
for r in carriers:
    print(f"  {r['name']:<14} cone={r['cone']}  turrets={'yes' if r['turret_src'] else 'NO'}"
          f"  chain={'>'.join(r['chain'])}")

no_turret = [r for r in carriers if not r["turret_src"]]
print(f"\n-> carriers WITHOUT any turret: {len(no_turret)}/{len(carriers)}: "
      f"{[r['name'] for r in no_turret]}")

pl_cone = [r for r in players if r["cone_src"]]
pl_turret = [r for r in players if r["turret_src"]]
print(f"-> player defs resolving a cone: {len(pl_cone)}/{len(players)}")
print(f"-> player defs WITH turrets: {len(pl_turret)}/{len(players)}: "
      f"{[r['name'] for r in pl_turret]} — cones: {[r['cone'] for r in pl_turret]}")

ai_cone = [r for r in ai if r["cone_src"]]
ai_none = [r for r in ai if not r["cone_src"]]
print(f"-> AI defs resolving a cone: {len(ai_cone)}/{len(ai)}; without: {[r['name'] for r in ai_none]}")

vals = {tuple(r["cone"]) for r in carriers}
yaws = {tuple(resolve(defs, r["name"], "gun_yaw")[1]) for r in carriers}
print(f"-> distinct gun_pitch values {vals}; gun_yaw {yaws}")

# turrets carry no rotation limits at all
tkeys = set()
for r in rows:
    _, t = resolve(defs, r["name"], "turrets")
    if not t:
        continue
    for _, entries in pairs(t):
        for e in entries or []:
            tkeys |= set(asdict(e))
print(f"-> keys inside a `turrets` entry, whole install: {sorted(tkeys)}")

print("\nVERDICT: cone presence tracks 'is an AI aircraft', not 'has a turret'."
      if not pl_cone and no_turret else "\nVERDICT: claim does NOT hold — re-read above.")
