"""BL-477: the shipped-data half of the weapon-ray decode.

The decode itself is docs/org/weaponRay.md: the original's ray-vs-polygon test reads no texture
data at any point, so an alpha-cutout card is solid to weapon fire there too. This script is the
shipped-data evidence beside it. Run from the repo root with an extracted install present
(`python analysis/bl-477-weapon-ray/census.py [extracted-dir]`). Read-only; prints, writes nothing.

The censuses, and what they showed on this install:

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

5. Nothing stands between the hull underside and the tank tops. A ray straight up from each tank's
   top centre meets `g482`, the belly plate (`cargoskin2`, alpha None), at y = -48.9 to -52.5
   against tank tops at y = -61, with no polygon in between. The front truss is a lateral screen
   around the tanks and not a roof over them, which is why a rocket that strikes the belly plate
   above them has line of sight down to all four.

The engine half is the `alpha-cutout-ray-census` in-engine suite: 180 rays at `hydrogentank1` from
a built C3/M01 with the zeppelins at their authored pose. 5 of 180 aspects reach the tank, all at
level elevation on the aft quarter; `g469` takes 26 of the 36 level azimuths. Its splash half runs
the production cover ray from a burst on `g482` down to each tank.
"""
import json
import math
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


# --- 4. what stands between the hull underside and the tank tops -----------------------------
# The truss is a lateral screen around the slung tanks, not a roof over them, so a rocket that
# strikes the hull underside above them has an unobstructed drop to their tops. This resolves the
# subtree's transforms the way CSVM.Mech3.GameZ.ParseTransform does (YXZ euler, unit scale) and
# intersects real rays against fan-triangulated polygons, because a bounding box over a large card
# spans the column without anything of the card being in it.
def euler_yxz(rx, ry, rz):
    def rot_x(a):
        c, s = math.cos(a), math.sin(a)
        return [[1, 0, 0], [0, c, -s], [0, s, c]]

    def rot_y(a):
        c, s = math.cos(a), math.sin(a)
        return [[c, 0, s], [0, 1, 0], [-s, 0, c]]

    def rot_z(a):
        c, s = math.cos(a), math.sin(a)
        return [[c, -s, 0], [s, c, 0], [0, 0, 1]]

    return mat(rot_y(ry), mat(rot_x(rx), rot_z(rz)))


def mat(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


IDENTITY = ([[1, 0, 0], [0, 1, 0], [0, 0, 1]], (0.0, 0.0, 0.0))


def node_transform(n):
    obj = (n.get("data") or {}).get("Object3d")
    t = obj.get("transform") if obj else None
    if not isinstance(t, dict):
        return IDENTITY
    rts = t.get("RotateTranslateScale") or t
    r = rts.get("rotate") or {"x": 0, "y": 0, "z": 0}
    tr = rts.get("translate") or {"x": 0, "y": 0, "z": 0}
    orig = rts.get("original")
    if isinstance(orig, dict):
        m = lambda a, b: float(orig[a]) if a in orig else float(orig[b])
        basis = [[m("a", "r00"), m("b", "r01"), m("c", "r02")],
                 [m("d", "r10"), m("e", "r11"), m("f", "r12")],
                 [m("g", "r20"), m("h", "r21"), m("i", "r22")]]
    else:
        basis = euler_yxz(float(r["x"]), float(r["y"]), float(r["z"]))
    return basis, (float(tr["x"]), float(tr["y"]), float(tr["z"]))


def compose(pb, pt, cb, ct):
    return mat(pb, cb), tuple(sum(pb[i][k] * ct[k] for k in range(3)) + pt[i] for i in range(3))


def place(b, t, v):
    return tuple(sum(b[i][k] * v[k] for k in range(3)) + t[i] for i in range(3))


# Keyed by node INDEX, never by name: the four tanks' children share their names (`healthy`,
# `dbase`), so a name key merges all four subtrees into one and the extents read as the whole ship.
faces = []  # (node index, node name, triangle, texture, show_backface)


def collect(index, basis, origin):
    n = nodes[index]
    basis, origin = compose(basis, origin, *node_transform(n))
    mi = n.get("model_index")
    if mi is not None and mi >= 0:
        verts = models[mi]["vertices"]
        for p in models[mi]["polygons"]:
            vs = [place(basis, origin, (verts[k]["x"], verts[k]["y"], verts[k]["z"]))
                  for k in p["vertex_indices"]]
            mat_index = p["materials"][0]["material_index"] if p.get("materials") else p["material_index"]
            for k in range(1, len(vs) - 1):
                faces.append((index, n.get("name") or f"#{index}", (vs[0], vs[k], vs[k + 1]),
                              texture_of(mat_index), p["flags"].get("show_backface", False)))
    for c in n.get("child_indices") or []:
        collect(c, basis, origin)


def ray_triangle(orig, direction, tri):
    sub = lambda a, b: (a[0] - b[0], a[1] - b[1], a[2] - b[2])
    dot = lambda a, b: a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
    cross = lambda a, b: (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
    e1, e2 = sub(tri[1], tri[0]), sub(tri[2], tri[0])
    h = cross(direction, e2)
    det = dot(e1, h)
    if abs(det) < 1e-12:
        return None
    f = 1.0 / det
    s = sub(orig, tri[0])
    u = f * dot(s, h)
    if u < -1e-6 or u > 1 + 1e-6:
        return None
    q = cross(s, e1)
    v = f * dot(direction, q)
    if v < -1e-6 or u + v > 1 + 1e-6:
        return None
    t = f * dot(e2, q)
    return t if t > 1e-4 else None


collect(by_name["cargozep1"][0], *IDENTITY)

print()
print("--- 4. the volume between the hull underside and the tank tops ---")
for tank in ("hydrogentank1", "hydrogentank2", "hydrogentank3", "hydrogentank4"):
    own = set()
    for root in by_name.get(tank, []):
        stack = [root]
        while stack:
            i = stack.pop()
            own.add(i)
            stack.extend(nodes[i].get("child_indices") or [])
    mine = [f for f in faces if f[0] in own]
    if not mine:
        print(f"  {tank}: no mesh nodes")
        continue
    xs = [v[0] for f in mine for v in f[2]]
    ys = [v[1] for f in mine for v in f[2]]
    zs = [v[2] for f in mine for v in f[2]]
    origin = ((min(xs) + max(xs)) / 2, max(ys) + 0.01, (min(zs) + max(zs)) / 2)
    print(f"  {tank}: x[{min(xs):.1f},{max(xs):.1f}] y[{min(ys):.1f},{max(ys):.1f}] "
          f"z[{min(zs):.1f},{max(zs):.1f}]; straight up from its top centre ->")
    hits = sorted((t, f[1], f[3], f[4]) for f in faces if f[0] not in own
                  for t in [ray_triangle(origin, (0.0, 1.0, 0.0), f[2])] if t is not None)
    for t, name, tex, backface in hits[:3]:
        print(f"      y={origin[1] + t:8.2f}  {name:<14} tex={tex} alpha={alpha.get(tex)} "
              f"show_backface={backface}")
