"""BL-477: the shipped-data half of the weapon-ray decode.

The decode itself is docs/org/weaponRay.md: the original's ray-vs-polygon test reads no texture
data at any point, so an alpha-cutout card is solid to weapon fire there too. This script is the
shipped-data evidence beside it. Run from the repo root with an extracted install present
(`python analysis/bl-477-weapon-ray/census.py [extracted-dir]`). Read-only; prints, writes nothing.

Three censuses, and what they showed on this install:

1. The material `flag` bit (mech3ax MaterialFlags::UNKNOWN) is the decal-receiving mark, not a
   transparency mark. It selects the ray-vs-polygon routine that also interpolates the hit UV, and
   gates the bullet-hole stamper that blits a decal at that UV. 21 of C3's 483 materials carry it
   and every one is an aircraft or cockpit skin (bal_fuslage, bldhwk_cowling, cphead, cockpit10,
   damage1, gun_barrel). No world material does, so on a zeppelin no hit UV is ever computed.

2. The truss is alpha and so is the tank's own destroyed variant. Front truss high level `g469` is
   57 polygons, 39 of them `cgcable1` (alpha Full); low level `g503` is 10, 6 of them `cargotex1`
   (Full). The tank's `dbase` is 5 polygons of `hydrotank4`, Full throughout. A blanket
   "skip every alpha polygon" collision rule would therefore delete a destructible's own hit
   volume, quite apart from diverging from the original.

3. Backface does not explain the asymmetry. The original's ray test honours `show_backface` (a
   polygon without it is solid from its front only) where our colliders are two-sided, but 33 of
   `g469`'s 39 `cgcable1` cards set it, so the truss is double-sided there as well.

4. The LOD bands say our static level choice is right at gun range and wrong past it. `f_hi`
   covers [0, 800), `f_mid` [800, 1400), `f_lo` [1400, inf), the rear truss the same with a 770 m
   first break. SceneBuilder keeps the range.min == 0 level and collides it at every distance, so
   inside 800 m we present exactly the level the original intersects; beyond it the original
   narrows to +/-28.3 m of local x while we stay on f_hi's +/-48.9 m.

The engine half is the `alpha-cutout-ray-census` in-engine suite: 180 rays at `hydrogentank1` from
a built C3/M01 with the zeppelins at their authored pose. 5 of 180 aspects reach the tank, all at
level elevation on the aft quarter; `g469` takes 26 of the 36 level azimuths.
"""
import json
import os
import sys

EXTRACTED = sys.argv[1] if len(sys.argv) > 1 else "extracted"
CHAPTER = "C3"


def load(*parts):
    return json.load(open(os.path.join(EXTRACTED, *parts), encoding="utf-8"))


gz = lambda n: load(CHAPTER, "gamez", n)
nodes, models, mats, texs = gz("nodes.json"), gz("models.json"), gz("materials.json"), gz("textures.json")
alpha = {t["name"]: t["alpha"] for t in load(CHAPTER, "texture", "manifest.json")["texture_infos"]}


def material(index):
    m = mats[index]
    kind = next(iter(m))
    return kind, m[kind]


def texture_of(index):
    kind, d = material(index)
    if kind != "Textured":
        return None
    # Material texture names carry a '.tif' the manifest does not.
    return texs[d["texture_index"]]["name"].rsplit(".", 1)[0]


print(f"--- 1. the material `flag` bit in {CHAPTER} ---")
flagged = [(i, texture_of(i)) for i, _ in enumerate(mats) if material(i)[1].get("flag")]
textured = sum(1 for i, _ in enumerate(mats) if material(i)[0] == "Textured")
print(f"{len(mats)} materials, {textured} textured, {len(flagged)} with flag=true")
print("  " + ", ".join(t or f"colored#{i}" for i, t in flagged))

print()
print("--- 2. cargozep1 truss + tank polygons ---")
by_name = {}
for i, n in enumerate(nodes):
    by_name.setdefault(n.get("name") or "", []).append(i)


def polygon_census(model_index):
    per = {}
    for p in models[model_index]["polygons"]:
        mi = p["materials"][0]["material_index"] if p.get("materials") else p["material_index"]
        t = texture_of(mi)
        key = (t, alpha.get(t), p["flags"].get("show_backface", False))
        per[key] = per.get(key, 0) + 1
    return per


def descend(index, out, depth=0):
    n = nodes[index]
    out.append((depth, index, n.get("name"), n.get("model_index")))
    for c in n.get("child_indices") or []:
        descend(c, out, depth + 1)


for label in ("front", "rear", "hydrogentank1"):
    for root in by_name.get(label, []):
        out = []
        descend(root, out)
        print(f"{label}:")
        for depth, i, name, mi in out:
            if mi is None or mi < 0:
                continue
            print(f"  {name} ({len(models[mi]['polygons'])} polys)")
            for (t, a, backface), count in sorted(polygon_census(mi).items(), key=lambda kv: -kv[1]):
                print(f"      {count:4d} {t} alpha={a} show_backface={backface}")

print()
print("--- 3. truss LOD bands ---")
for label in ("f_lo", "f_mid", "f_hi", "b_lo", "b_mid", "b_hi"):
    for i in by_name.get(label, []):
        r = nodes[i]["data"]["Lod"]["range"]
        print(f"  {label}: range [{r['min']}, {r['max']})")
