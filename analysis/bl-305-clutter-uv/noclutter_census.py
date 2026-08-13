"""Census of the per-polygon flag mech3ax spells `unk3` (raw bit 0x800 of the CS polygon
flags word), asking one question: does the flagged set look like a SUBFACE/overlay layer, or
like a NO-CLUTTER layer (streets, pavement, water)?

Background. `docs/formats/gamez.md` and `GameZ.cs` call this bit the OpenFlight SUBFACE mark.
A Ghidra read (2026-08-10) instead traced `gg_load.c`'s `strstr(name, "no_clutter")` global
into `gmod_cons.c`'s polygon builder, which packs it as `(arg13 & 1) << 0xb` — bit 11, 0x800 —
into the same word the clutter walk `FUN_004de2c0` tests. This script is the data-side check.

It reports, per chapter and for C5 in detail:
  - the flagged population (and the `in_out` population, the only other unknown polygon bit
    mech3ax exposes),
  - the texture histogram of flagged vs unflagged polygons,
  - polygon orientation (horizontal = |Ny| > 0.9 of the Newell normal) and world Y,
    which separates "ground surface" from "overlay on anything".

World placement comes from the node that owns the model (nodes.json `model_index` -> the node's
transform origin), added to the polygon's own local centroid. Nodes in this install place world
tiles with a translation-only transform, so this is adequate for a population comparison; it is
NOT a substitute for the full WorldBuilder walk.

Run from the repo root:

    python analysis/bl-305-clutter-uv/noclutter_census.py [--chapter C5]
"""
import argparse
import json
import math
import os
from collections import Counter, defaultdict

# extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree that does
# (the same env var the engine reads), defaulting to this checkout.
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EXTRACTED = os.path.join(os.environ.get("CSVM_DATA_ROOT") or REPO, "extracted")

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def load(chapter, name):
    path = os.path.join(EXTRACTED, chapter, "gamez", name)
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def texture_names(chapter):
    """material index -> texture name (or a Colored placeholder)."""
    mats = load(chapter, "materials.json")
    texs = load(chapter, "textures.json")

    def tname(t):
        if isinstance(t, dict):
            return t.get("name") or t.get("original") or "?"
        return str(t)

    tex = [tname(t) for t in texs]
    out = []
    for m in mats:
        body = m
        if isinstance(m, dict) and len(m) == 1 and "Textured" in m:
            body = m["Textured"]
        if isinstance(m, dict) and len(m) == 1 and "Colored" in m:
            out.append("<colored>")
            continue
        idx = body.get("texture_index") if isinstance(body, dict) else None
        if idx is None:
            out.append("<colored>")
        else:
            out.append(tex[idx] if 0 <= idx < len(tex) else "<oob>")
    return out


def newell(verts, indices):
    nx = ny = nz = 0.0
    n = len(indices)
    for i in range(n):
        a = verts[indices[i]]
        b = verts[indices[(i + 1) % n]]
        nx += (a["y"] - b["y"]) * (a["z"] + b["z"])
        ny += (a["z"] - b["z"]) * (a["x"] + b["x"])
        nz += (a["x"] - b["x"]) * (a["y"] + b["y"])
    mag = math.sqrt(nx * nx + ny * ny + nz * nz)
    if mag == 0.0:
        return None
    return (nx / mag, ny / mag, nz / mag)


def model_origins(chapter, model_count):
    """model index -> (x, y, z) of the first node that references it, or None."""
    nodes = load(chapter, "nodes.json")
    origin = [None] * model_count
    for node in nodes:
        mi = node.get("model_index")
        if mi is None:
            data = node.get("data") or {}
            for v in data.values():
                if isinstance(v, dict) and "model_index" in v:
                    mi = v["model_index"]
                    break
        if mi is None or not (0 <= mi < model_count) or origin[mi] is not None:
            continue
        tr = node.get("transform")
        o = None
        if isinstance(tr, dict):
            for key in ("translation", "origin"):
                if key in tr and isinstance(tr[key], dict):
                    o = tr[key]
                    break
            if o is None and "original" in tr and isinstance(tr["original"], dict):
                oo = tr["original"]
                for key in ("translation", "origin"):
                    if key in oo and isinstance(oo[key], dict):
                        o = oo[key]
                        break
        if o is not None:
            origin[mi] = (o.get("x", 0.0), o.get("y", 0.0), o.get("z", 0.0))
    return origin


def census(chapter, detail):
    models = load(chapter, "models.json")
    tex = texture_names(chapter)
    counts = Counter()
    by_tex = defaultdict(Counter)
    orient = defaultdict(Counter)
    ys = defaultdict(list)
    origins = model_origins(chapter, len(models)) if detail else [None] * len(models)

    for mi, model in enumerate(models):
        if not model:
            continue
        verts = model.get("vertices") or []
        for poly in model.get("polygons") or []:
            flags = poly.get("flags") or {}
            unk3 = bool(flags.get("unk3"))
            in_out = bool(flags.get("in_out"))
            counts["total"] += 1
            if unk3:
                counts["unk3"] += 1
            if in_out:
                counts["in_out"] += 1
            if unk3 and in_out:
                counts["unk3+in_out"] += 1
            if not detail:
                continue
            key = "unk3" if unk3 else "plain"
            mats = poly.get("materials") or []
            name = tex[mats[0]["material_index"]] if mats else "<none>"
            by_tex[key][name] += 1
            idx = poly.get("vertex_indices") or []
            if len(idx) >= 3 and max(idx) < len(verts):
                nrm = newell(verts, idx)
                if nrm:
                    orient[key]["horizontal" if abs(nrm[1]) > 0.9 else "other"] += 1
                cy = sum(verts[i]["y"] for i in idx) / len(idx)
                o = origins[mi]
                ys[key].append(cy + (o[1] if o else 0.0))
    return counts, by_tex, orient, ys


def pct(a, b):
    return (100.0 * a / b) if b else 0.0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--chapter", default="C5", help="chapter to detail (default C5)")
    args = ap.parse_args()

    print("per-chapter polygon flag census (mech3ax unified shape)")
    print("%-5s %10s %8s %8s %10s" % ("chap", "polygons", "unk3", "in_out", "unk3 %"))
    for chapter in CHAPTERS:
        c, _, _, _ = census(chapter, detail=False)
        print("%-5s %10d %8d %8d %9.2f%%" % (
            chapter, c["total"], c["unk3"], c["in_out"], pct(c["unk3"], c["total"])))

    print()
    print("detail: %s" % args.chapter)
    c, by_tex, orient, ys = census(args.chapter, detail=True)
    for key in ("unk3", "plain"):
        tot = sum(by_tex[key].values())
        print()
        print("--- %s set: %d polygons" % (key, tot))
        h = orient[key]["horizontal"]
        print("    horizontal (|Ny|>0.9): %d (%.1f%%)" % (h, pct(h, sum(orient[key].values()))))
        col = ys[key]
        if col:
            col_sorted = sorted(col)
            print("    world Y: min %.1f  p50 %.1f  p95 %.1f  max %.1f" % (
                col_sorted[0], col_sorted[len(col_sorted) // 2],
                col_sorted[int(len(col_sorted) * 0.95)], col_sorted[-1]))
        print("    top textures:")
        for name, n in by_tex[key].most_common(20):
            print("      %-28s %6d  (%.1f%%)" % (name, n, pct(n, tot)))


if __name__ == "__main__":
    main()
