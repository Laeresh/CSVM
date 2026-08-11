"""How much crash-def accuracy A2's body-granularity surface id actually costs.

A2 stamps each collider body with the polygon-count-dominant `soil` id among that body's
polygons (ties toward the lower id), rather than splitting colliders per id. Bodies are the
existing texture-class buckets from SceneBuilder.CollidersForMesh -- one per surface class
present in a mesh. So a polygon whose own id differs from its bucket's dominant id is
MISATTRIBUTED: a crash there resolves the wrong def.

This is structurally the same failure BL-204 removed on 2026-07-31, when the whole-mesh area
quorum was found to strand real water polygons on mostly-land tiles. Same direction (a dominant
vote loses the minority), bounded differently (per bucket, not per mesh). That precedent is why
this is measured before B11 rather than after.

What matters is not id accuracy but DEF accuracy. Ids 0/5/8/11/12 all resolve
`player_crash_default` (no `player_crash_<name>` def ships for fire/airstrip/buildings/dzone),
so a `dirt` polygon stranded into an `airstrip`-dominant bucket and one stranded into a
`default`-dominant bucket are the same error, while an `airstrip` polygon in a `default`-dominant
bucket is no error at all. This script therefore reports both: raw id misattribution, and the
subset of it that actually changes which def would play.

Bucketing replicates SceneBuilder.CollidersForMesh (per-polygon texture class, the shipped rule
since 2026-08-01) and triangulation matches SceneBuilder.PolygonArea, as in soil_area_by_mesh.py.
Read-only; absolute extracted/ path because it is git-ignored and this must run from a worktree.

    python analysis/surface-classification/soil_bucket_strand.py [CHAPTER ...]
"""
import collections
import json
import sys
from pathlib import Path

EXTRACTED = Path(r"Z:\CSVM\extracted")
CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]

SOIL_ID = {"Default": 0, "Water": 1, "Fire": 5, "Grass": 8, "Mech": 11, "Silt": 12, "NoSlip": 13}
NAME = {0: "default", 1: "water", 5: "fire", 8: "airstrip", 11: "buildings", 12: "dzone", 13: "dirt"}
# id -> the def FUN_0048b920's cascade actually resolves. Only _default/_dirt/_water ship.
DEF_FOR_ID = {0: "_default", 1: "_water", 5: "_default", 8: "_default",
              11: "_default", 12: "_default", 13: "_dirt"}


def classify(t):
    """SceneBuilder.ClassifySurface -- the TEXTURE-NAME classifier that decides bucketing.

    Not the surface id. Copied from class_area_share.py so the buckets here are the shipped
    ones; if that rule changes, this copy must be updated with it.
    """
    if not t:
        return None
    t = t.lower()
    if 'shadow' in t:
        return 'water' if (t.startswith('water') or 'splash' in t) else None
    if (t.startswith('water') or t.startswith('wtr') or t.startswith('srf')
            or 'wakefront' in t or 'watersquirt' in t):
        return 'water'
    if ('build' in t or t.startswith('hangar') or t.startswith('bld') or 'cblock' in t
            or 'warehouse' in t or 'roof' in t or 'filmblock' in t
            or t.startswith('empire') or t.startswith('chrysler')):
        return 'buildings'
    return None


def tri_area(vs, a, b, c):
    ax, ay, az = vs[a]["x"], vs[a]["y"], vs[a]["z"]
    bx, by, bz = vs[b]["x"], vs[b]["y"], vs[b]["z"]
    cx, cy, cz = vs[c]["x"], vs[c]["y"], vs[c]["z"]
    ux, uy, uz = bx - ax, by - ay, bz - az
    vx, vy, vz = cx - ax, cy - ay, cz - az
    wx, wy, wz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    return 0.5 * (wx * wx + wy * wy + wz * wz) ** 0.5


def polygon_area(vs, poly):
    """Matches SceneBuilder.PolygonArea: strip order for tri_strip, a fan from vertex 0 otherwise."""
    idx = poly["vertex_indices"]
    n = len(idx)
    if n < 3:
        return 0.0
    total = 0.0
    if poly["flags"]["tri_strip"]:
        for i in range(n - 2):
            total += tri_area(vs, idx[i], idx[i + 1], idx[i + 2])
    else:
        for i in range(1, n - 1):
            total += tri_area(vs, idx[0], idx[i], idx[i + 1])
    return total


def load(chapter):
    d = EXTRACTED / chapter / "gamez"
    mats = json.loads((d / "materials.json").read_text(encoding="utf-8"))
    texs = [t["name"] for t in json.loads((d / "textures.json").read_text(encoding="utf-8"))]
    models = json.loads((d / "models.json").read_text(encoding="utf-8"))
    soil_of, tex_of = [], []
    for entry in mats:
        kind, body = next(iter(entry.items()))
        soil_of.append(SOIL_ID.get(body.get("soil")))
        ti = body.get("texture_index", -1) if kind == "Textured" else -1
        tex_of.append(texs[ti] if ti is not None and 0 <= ti < len(texs) else None)
    return models, soil_of, tex_of


def run(chapter):
    d = EXTRACTED / chapter / "gamez"
    if not d.exists():
        print(f"{chapter}: no extracted gamez at {d}")
        return None
    models, soil_of, tex_of = load(chapter)

    total_area = 0.0
    id_area = collections.Counter()          # per-polygon truth
    wrong_id_area = collections.Counter()    # truth id -> area landing in a differently-id'd bucket
    wrong_def_area = collections.Counter()   # (true def -> played def) -> area
    mixed_buckets = 0
    total_buckets = 0

    for model in models:
        vs = model["vertices"]
        buckets = collections.defaultdict(list)
        for poly in model["polygons"]:
            mats = poly.get("materials") or []
            mi = mats[0]["material_index"] if mats else -1
            sid = soil_of[mi] if 0 <= mi < len(soil_of) else None
            tex = tex_of[mi] if 0 <= mi < len(tex_of) else None
            if sid is None:
                continue
            buckets[classify(tex)].append((sid, polygon_area(vs, poly)))

        for polys in buckets.values():
            total_buckets += 1
            counts = collections.Counter(sid for sid, _ in polys)
            if len(counts) > 1:
                mixed_buckets += 1
            # A2's rule: polygon-count dominant, ties toward the lower id.
            dominant = min(counts, key=lambda s: (-counts[s], s))
            for sid, a in polys:
                total_area += a
                id_area[sid] += a
                if sid != dominant:
                    wrong_id_area[sid] += a
                    true_def, played = DEF_FOR_ID.get(sid), DEF_FOR_ID.get(dominant)
                    if true_def != played:
                        wrong_def_area[(true_def, played)] += a

    if not total_area:
        return None
    pct = lambda a: 100.0 * a / total_area
    print(f"\n=== {chapter} ===")
    print(f"  buckets: {total_buckets:,} ({mixed_buckets:,} span >1 id, "
          f"{100.0 * mixed_buckets / total_buckets:.2f}%)")
    print("  id                 area share   misattributed (wrong id)")
    for sid in sorted(id_area):
        w = wrong_id_area[sid]
        share = f"{pct(id_area[sid]):6.2f}%"
        rel = f"{100.0 * w / id_area[sid]:5.1f}% of its own area" if id_area[sid] else "-"
        print(f"    {sid:2} {NAME.get(sid, '?'):10} {share}   {pct(w):6.3f}%  ({rel})")

    dw = sum(wrong_def_area.values())
    print(f"  --> area that would play the WRONG DEF: {pct(dw):.3f}% of collidable area")
    for (true_def, played), a in sorted(wrong_def_area.items(), key=lambda kv: -kv[1]):
        print(f"        {true_def} played as {played}: {pct(a):.3f}%")
    return {"chapter": chapter, "wrong_def_pct": pct(dw),
            "dirt_stranded_pct": (100.0 * wrong_id_area[13] / id_area[13]) if id_area.get(13) else None}


def main():
    rows = [r for c in (sys.argv[1:] or CHAPTERS) if (r := run(c))]
    print("\n=== summary ===")
    print("  chapter   wrong-def area   dirt area stranded into another def")
    for r in rows:
        ds = "-- (no dirt)" if r["dirt_stranded_pct"] is None else f"{r['dirt_stranded_pct']:.1f}%"
        print(f"  {r['chapter']:5}     {r['wrong_def_pct']:6.3f}%          {ds}")
    print("\n  'wrong-def area' is the share of collidable area where A2's body-granularity id")
    print("  resolves a different crash def than a per-polygon id would. That is the fidelity")
    print("  cost of not splitting colliders per id -- the number that gates B11.")


if __name__ == "__main__":
    main()
