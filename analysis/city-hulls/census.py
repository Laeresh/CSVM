"""C5 building hull census for docs/PLAN-city-buildings.md A1.

Per C5 building decoration model: bounding box, triangle count, textures, mean texture colour
and brightness, the templates that place it (directly or through the substitute roll), its fade
distances, and whether each substitute matches its source in geometry. Then the lots per
template (decorations sharing one origin are one building) and the distinct hulls.

extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the checkout that does
(the env var the engine reads), defaulting to this one. Embeds no game data; --json writes the
derived census, which holds names and measured numbers only.

    set CSVM_DATA_ROOT=Z:\\CSVM
    py -I analysis/city-hulls/census.py [--log <game log>] [--json <out.json>]

--log takes a game log holding the engine's `[world] clutter: ...` line for C5
(`CSVM/src/Mech3/WorldSession.cs`), whose per-kind counts are the placed numbers.
"""
import argparse
import json
import math
import os
import re
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
EX = os.path.join(os.environ.get("CSVM_DATA_ROOT") or REPO, "extracted")
sys.path.insert(0, os.path.join(HERE, "..", "bl-305-clutter-uv"))
import eligibility as E  # noqa: E402

TEMPLATES = ["cblock%d" % i for i in range(1, 8)]
# Lamp cards and glows are the templates' other decorations; they are not buildings.
NOT_BUILDINGS = {"lightpole.flt", "w_lightglow.flt"}
# A shape tolerance: two copies of one model match if every vertex agrees to 5 cm.
SHAPE_TOL = 0.05


def load(name):
    with open(os.path.join(EX, "C5", "gamez", name), encoding="utf-8") as f:
        return json.load(f)


class Chapter:
    def __init__(self):
        self.nodes, _, _, _ = E.load_chapter("C5", EX)
        self.models = load("models.json")
        tex = [t.get("name") for t in load("textures.json")]
        self.mat_tex = []
        for m in load("materials.json"):
            ti = m.get("Textured", {}).get("texture_index") if "Textured" in m else None
            self.mat_tex.append(tex[ti] if ti is not None else None)
        self.by_name = defaultdict(list)
        for i, n in enumerate(self.nodes):
            self.by_name[n.name.lower()].append(i)

    def has_mesh(self, idx):
        mi = self.nodes[idx].model_index
        return 0 <= mi < len(self.models) and isinstance(self.models[mi], dict) \
            and (self.models[mi].get("polygons") or [])

    def parts(self, deco_idx):
        """Every drawn mesh under a decoration node, in the decoration's own frame: the node's
        local placed the stamp, so it is left out. Far LOD levels are skipped as the engine's
        Clutter.ExtraMeshes skips them."""
        out = []

        def walk(idx, xf, top):
            n = self.nodes[idx]
            if n.kind in ("Lod", "LoD") and n.lod_range_min != 0.0:
                return
            if not top and n.local is not None:
                xf = E._compose(xf, n.local)
            if self.has_mesh(idx):
                out.append((n.model_index, xf, idx))
            for c in n.children:
                if 0 <= c < len(self.nodes):
                    walk(c, xf, False)
        walk(deco_idx, E.IDENTITY, True)
        return out

    def geometry(self, deco_idx):
        """Triangles (world-in-decoration-frame corners, uv, texture) of every part."""
        tris = []
        for mi, xf, _ in self.parts(deco_idx):
            m = self.models[mi]
            verts = [E._apply(xf, (v["x"], v["y"], v["z"])) for v in m["vertices"]]
            for p in m["polygons"]:
                vi = p.get("vertex_indices") or []
                mats = p.get("materials") or []
                tex = self.mat_tex[mats[0]["material_index"]] if mats else None
                uvs = (mats[0].get("uv_coords") if mats else None) or [{"u": 0, "v": 0}] * len(vi)
                if p.get("flags", {}).get("tri_strip"):
                    idx = [(i, i + 1, i + 2) for i in range(len(vi) - 2)]
                else:
                    idx = [(0, i, i + 1) for i in range(1, len(vi) - 1)]
                for a, b, c in idx:
                    tris.append(([verts[vi[a]], verts[vi[b]], verts[vi[c]]],
                                 [(uvs[k]["u"], uvs[k]["v"]) for k in (a, b, c)], tex))
        return tris


def bbox(tris):
    pts = [p for t in tris for p in t[0]]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    return lo, hi


def tri_area(t):
    a, b, c = t[0]
    u = [b[i] - a[i] for i in range(3)]
    v = [c[i] - a[i] for i in range(3)]
    x = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
    return 0.5 * math.sqrt(sum(k * k for k in x))


def shape_key(tris):
    """Vertex positions only, order-free, at the shape tolerance."""
    return sorted({tuple(round(c / SHAPE_TOL) for c in p) for t in tris for p in t[0]})


def same_shape(ta, tb):
    if len(ta) != len(tb):
        return False
    pa = sorted(p for t in ta for p in t[0])
    pb = sorted(p for t in tb for p in t[0])
    return all(max(abs(x - y) for x, y in zip(p, q)) <= SHAPE_TOL for p, q in zip(pa, pb))


def same_uv(ta, tb):
    key = lambda tris: sorted((tuple(round(c, 3) for p in t[0] for c in p),
                               tuple(round(c, 3) for uv in t[1] for c in uv), t[2] or "") for t in tris)
    return key(ta) == key(tb)


def tex_png(name):
    base = os.path.splitext(name)[0]
    d = os.path.join(EX, "C5", "texture")
    p = os.path.join(d, base + ".png")
    if os.path.exists(p):
        return p
    low = {f.lower(): f for f in os.listdir(d)}
    f = low.get(base.lower() + ".png")
    return os.path.join(d, f) if f else None


_mean = {}

# The original facades paint their lit windows in as pale texels (sRGB luma above about 115, with
# a halo down to about 90) on walls below about 60. The unlit colour is the mean of the texels at
# or below this luma: the wall a building shows with no window lit.
UNLIT_LUMA = 90


def tex_mean(name, below=None):
    """The mean sRGB texel of a texture, or of its texels with luma at most `below`."""
    if (name, below) not in _mean:
        from PIL import Image
        p = tex_png(name) if name else None
        if not p:
            _mean[name, below] = None
        else:
            import numpy as np
            a = np.asarray(Image.open(p).convert("RGB"), dtype=float).reshape(-1, 3)
            if below is not None:
                a = a[luma(a.T) <= below]
            _mean[name, below] = tuple(float(x) for x in a.mean(axis=0))
    return _mean[name, below]


def luma(rgb):
    return 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]


def model_colour(tris, below=None):
    """Mean texture colour weighted by each texture's share of the model's surface area."""
    acc, tot = [0.0, 0.0, 0.0], 0.0
    for t in tris:
        m = tex_mean(t[2], below)
        if m is None:
            continue
        a = tri_area(t)
        tot += a
        for i in range(3):
            acc[i] += m[i] * a
    return tuple(round(c / tot) for c in acc) if tot else None


def zrd_blocks():
    with open(os.path.join(EX, "C5", "zrdr", "templates.zrd.json"), encoding="utf-8") as f:
        raw = json.load(f)
    blocks = {}
    for b in raw:
        kv = dict(zip(b[0::2], b[1::2]))
        name = kv["node"][0].lower()
        if name in blocks:
            continue  # first block wins (docs/formats/templates.md, the cb05det01 duplicate)
        sub = kv.get("substitute") or []
        tot = sum(w for w, _ in sub)
        blocks[name] = {"substitute": [(w / tot, m.lower()) for w, m in sub] if tot else [],
                        "fade": kv.get("far_fade_range"), "scale": kv.get("scale_range")}
    return blocks, len(raw)


def placed_from_log(path):
    """Per-kind counts off the engine's clutter summary, in its kind order, which is the
    template walk order then the minted substitution targets."""
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if "clutter:" in line and "cb12a.flt" in line and "3D decorations" in line:
                head = re.search(r"clutter: (\d+) sprites \+ (\d+) 3D decorations \((\d+) shared collision "
                                 r"shapes, (\d+) tris", line)
                kinds = re.findall(r"([\w.]+) \u00d7(\d+)", line)
                return {"sprites": int(head.group(1)), "solids": int(head.group(2)),
                        "shapes": int(head.group(3)), "shape_tris": int(head.group(4)),
                        "kinds": [(k.lower(), int(n)) for k, n in kinds]}
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--log")
    ap.add_argument("--json")
    args = ap.parse_args()

    ch = Chapter()
    blocks, nblocks = zrd_blocks()

    # Templates: each decoration under the ground node, its origin and model.
    templ = {}
    for t in TEMPLATES:
        root = E.find_template_root(ch.nodes, t)
        ground = E.first_with_mesh(ch.nodes, _Meshes(ch), root, include_self=True)
        g = ch.nodes[ground]
        quad = ch.models[g.model_index]["vertices"]
        size = (max(v["x"] for v in quad) - min(v["x"] for v in quad),
                max(v["z"] for v in quad) - min(v["z"] for v in quad))
        decos = []
        for c in g.children:
            n = ch.nodes[c]
            o = n.local[1] if n.local else (0.0, 0.0, 0.0)
            decos.append({"node": c, "model": n.name.lower(), "origin": o})
        templ[t] = {"decos": decos, "quad": size}

    # Every model name a C5 template reaches, directly or by one substitute roll.
    direct = defaultdict(lambda: defaultdict(int))
    for t, d in templ.items():
        for deco in d["decos"]:
            direct[deco["model"]][t] += 1
    reach = defaultdict(set)
    for m, per in direct.items():
        for t in per:
            reach[m].add(t)
            for _, tgt in (blocks.get(m) or {}).get("substitute", []):
                reach[tgt].add(t)

    # Geometry per name: the template's own decoration node for a placed model, else the first
    # gamez node of that name (the engine's global lookup, Clutter.ResolveModel).
    first_node = {}
    for t, d in templ.items():
        for deco in d["decos"]:
            first_node.setdefault(deco["model"], deco["node"])
    names = sorted(set(reach) | {b for b in blocks if b.startswith("cb")})
    rows = {}
    for m in names:
        if m in NOT_BUILDINGS:
            continue
        cands = [i for i in ch.by_name.get(m, []) if ch.parts(i)]
        node = first_node.get(m, cands[0] if cands else None)
        if node is None:
            rows[m] = {"model": m, "missing": True, "templates": sorted(reach.get(m, []))}
            continue
        tris = ch.geometry(node)
        lo, hi = bbox(tris)
        texes = sorted({t[2] for t in tris if t[2]})
        col = model_colour(tris)
        copies = [i for i in cands]
        copy_same = all(same_shape(tris, ch.geometry(i)) for i in copies)
        b = blocks.get(m)
        rows[m] = {
            "model": m, "node": node, "parts": len(ch.parts(node)), "copies": len(copies),
            "copies_same_shape": copy_same,
            "w": hi[0] - lo[0], "d": hi[2] - lo[2], "h": hi[1] - lo[1],
            "lo": lo, "hi": hi, "tris": len(tris),
            "tris_by_part": [sum(max(0, len(p.get("vertex_indices") or []) - 2)
                                 for p in ch.models[mi]["polygons"]) for mi, _, _ in ch.parts(node)],
            "textures": texes, "rgb": col, "luma": round(luma(col)) if col else None,
            "rgb_unlit": model_colour(tris, UNLIT_LUMA),
            "direct": dict(direct.get(m, {})), "templates": sorted(reach.get(m, [])),
            "fade": b["fade"] if b else None,
            "substitute": b["substitute"] if b else [],
            "has_block": b is not None,
        }

    # Substitutes against their source, geometry and texture.
    subs = []
    for m, r in rows.items():
        for share, tgt in r.get("substitute", []):
            if tgt == m:
                continue
            tr = rows.get(tgt)
            if tr is None or tr.get("missing"):
                subs.append((m, tgt, share, None, None, None))
                continue
            ga, gb = ch.geometry(r["node"]), ch.geometry(tr["node"])
            subs.append((m, tgt, share, same_shape(ga, gb), same_uv(ga, gb),
                         r["textures"] == tr["textures"]))

    # Lots: decorations of one template at one origin are one building.
    lots = []
    for t, d in templ.items():
        at = defaultdict(list)
        for deco in d["decos"]:
            if deco["model"] in NOT_BUILDINGS:
                continue
            key = tuple(round(c, 1) for c in deco["origin"])
            at[(key[0], key[2])].append(deco["model"])
        for (x, z), ms in sorted(at.items()):
            lots.append({"template": t, "x": x, "z": z, "models": sorted(ms)})

    # Distinct shapes over every building model: one class per geometry, and one per box.
    built = [m for m in sorted(rows) if not rows[m].get("missing")]
    geo = {m: ch.geometry(rows[m]["node"]) for m in built}
    classes = []
    for m in built:
        for c in classes:
            if same_shape(geo[c[0]], geo[m]):
                c.append(m)
                break
        else:
            classes.append([m])
    boxes = defaultdict(list)
    for m in built:
        r = rows[m]
        boxes[(round(r["w"] * 2) / 2, round(r["d"] * 2) / 2, round(r["h"] * 2) / 2)].append(m)

    shapes = collision_shapes(ch, templ, blocks)
    log = placed_from_log(args.log) if args.log else None
    placed = per_template_placed(log, rows) if log else None
    report(ch, templ, rows, subs, lots, blocks, nblocks, log)
    print()
    print("== shape classes (%d over %d building models)" % (len(classes), len(built)))
    for c in classes:
        if len(c) > 1:
            print("  " + " = ".join(c))
    print("== box classes (%d)" % len(boxes))
    for k, ms in sorted(boxes.items()):
        if len(ms) > 1:
            print("  %s %s" % (k, " ".join(ms)))
    print()
    print("== collision shapes reproduced: %d shapes, %d triangles (kinds %d, minted %d); "
          "template decorations only: %d shapes, %d triangles" % shapes)
    if placed:
        print()
        print("== placed (engine log): per template, the template's own kinds after the roll, and "
              "the stamps estimated as own count / own share")
        for t, p in placed["templates"].items():
            print("  %-7s kinds %s own %d stamps~%d lots~%d" % (t, p["kinds"], p["own"], p["stamps"], p["lots"]))
        print("  minted targets: %s" % placed["minted"])
        print("  total 3D decorations %d, of which a building's det half %d; triangles placed %d"
              % (placed["total"], placed["det"], placed["tris"]))
    print()
    print("== unlit colour (texels of luma <= %d, area weighted), the city models' grade targets" % UNLIT_LUMA)
    for m in built:
        if rows[m]["rgb_unlit"]:
            print("  %-12s %s (all texels %s)" % (m, rows[m]["rgb_unlit"], rows[m]["rgb"]))
    print()
    print("== hull table (markdown)")
    markdown(rows, classes, placed)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump({"rows": rows, "subs": subs, "lots": lots, "log": log, "placed": placed,
                       "classes": classes, "shapes": shapes,
                       "templates": {t: {"quad": d["quad"], "decos": [x["model"] for x in d["decos"]]}
                                     for t, d in templ.items()}}, f, indent=1, default=str)


def markdown(rows, classes, placed):
    """One row per shape class (a hull): its models, box, triangles, textures, colour, the
    templates that reach it, the fade a stamp actually uses, and the placed count."""
    src = defaultdict(list)  # target -> the sources whose roll reaches it
    for m, r in rows.items():
        for _, t in r.get("substitute", []):
            if t != m:
                src[t].append(m)
    per = placed["per_model"] if placed else {}
    short = lambda m: m.replace(".flt", "")
    print("| Hull | Models | W x D x H (m) | Tris | Textures | RGB, L | Templates (per tile) | Fade near, far (m) "
          "| Placed |")
    print("|---|---|---|---|---|---|---|---|---|")
    for c in sorted(classes, key=lambda c: c[0]):
        r = rows[c[0]]
        # A stamp fades by its SOURCE block (docs/formats/templates.md), so a target takes its source's.
        fades = set()
        for m in c:
            for s in ([m] if rows[m]["direct"] else src[m]):
                f = rows[s]["fade"]
                if f:
                    # Min pair then max pair: near is f[0][0]..f[1][0], far f[0][1]..f[1][1].
                    fades.add("%g-%g, %g-%g" % (f[0][0], f[1][0], f[0][1], f[1][1]))
        direct = defaultdict(int)
        for m in c:
            for t, n in rows[m]["direct"].items():
                direct[t] += n
        via = sorted({t for m in c for t in rows[m]["templates"]} - set(direct))
        tmpl = ", ".join(["%s x%d" % (t.replace("cblock", "cb"), n) for t, n in sorted(direct.items())]
                         + ["%s (roll)" % t.replace("cblock", "cb") for t in via])
        cols = "; ".join("%d,%d,%d L%d" % (*rows[m]["rgb"], rows[m]["luma"]) for m in c)
        texes = sorted({t.replace(".tif", "") for m in c for t in rows[m]["textures"]})
        print("| %s | %s | %.1f x %.1f x %.1f | %d | %s | %s | %s | %s | %s |" % (
            short(c[0]), " = ".join(short(m) for m in c), r["w"], r["d"], r["h"], r["tris"],
            " ".join(texes), cols, tmpl, " or ".join(sorted(fades)) or "-",
            " + ".join("%d" % per.get(m, 0) for m in c)))


def collision_shapes(ch, templ, blocks):
    """Clutter.BuildSolidCollision's shape set: one shape per distinct mesh index over every
    solid kind's parts. A kind is one (template, drawn mesh) pair (Clutter.ParseTemplate), plus
    one minted per substitution target that no template places (Clutter.ResolveModel, the first
    gamez node of that name)."""
    kinds, by_model = [], {}
    for t, d in templ.items():
        seen = {}
        for deco in d["decos"]:
            if deco["model"] in NOT_BUILDINGS:
                continue
            parts = ch.parts(deco["node"])
            if parts and parts[0][0] not in seen:
                seen[parts[0][0]] = parts
                kinds.append(parts)
                by_model.setdefault(deco["model"], parts)
    own = {mi for parts in kinds for mi, _, _ in parts}
    own_tris = sum(_mesh_tris(ch, mi) for mi in own)
    n_template = len(kinds)
    for m in list(by_model):
        for _, tgt in (blocks.get(m) or {}).get("substitute", []):
            if tgt in by_model:
                continue
            node = next((i for i in ch.by_name.get(tgt, []) if ch.parts(i)), None)
            if node is not None:
                by_model[tgt] = ch.parts(node)
                kinds.append(by_model[tgt])
    meshes = {mi for parts in kinds for mi, _, _ in parts}
    return (len(meshes), sum(_mesh_tris(ch, mi) for mi in meshes), n_template,
            len(kinds) - n_template, len(own), own_tris)


def _mesh_tris(ch, mi):
    return sum(max(0, len(p.get("vertex_indices") or []) - 2) for p in ch.models[mi]["polygons"])


def per_template_placed(log, rows):
    """Splits the engine's per-kind counts into the seven templates (each template's kinds end
    with its lamp card and glow) and the minted substitution targets after them."""
    groups, cur, lamps = [], [], 0
    for k, n in log["kinds"]:
        if k.endswith(".tif"):
            lamps += 1
            if lamps == 2:
                groups.append(cur)
                cur, lamps = [], 0
            continue
        cur.append((k, n))
    minted = cur
    out = {}
    for t, g in zip(TEMPLATES, groups):
        own = sum(n for _, n in g)
        stamps = 0
        lots = 0
        for k, n in g:
            share = next((s for s, tgt in rows[k]["substitute"] if tgt == k), 1.0)
            stamps += n / share
            if "det" not in k:
                lots += n / share
        out[t] = {"kinds": len(g), "own": own, "stamps": round(stamps), "lots": round(lots)}
    allk = [kv for g in groups for kv in g] + minted
    total = sum(n for _, n in allk)
    det = sum(n for k, n in allk if "det" in k)
    tris = sum(n * rows[k]["tris"] for k, n in allk)
    return {"templates": out, "minted": minted, "total": total, "det": det, "tris": tris,
            "per_model": {k: sum(n for kk, n in allk if kk == k) for k, _ in allk}}


class _Meshes:
    """List-like adapter so eligibility.first_with_mesh can test the raw models."""

    def __init__(self, ch):
        self.ch = ch

    def __len__(self):
        return len(self.ch.models)

    def __getitem__(self, i):
        m = self.ch.models[i]
        return _M(m.get("polygons") or []) if isinstance(m, dict) else None


class _M:
    def __init__(self, polys):
        self.polygons = polys


def report(ch, templ, rows, subs, lots, blocks, nblocks, log):
    print("templates.zrd: %d blocks, %d distinct models; cb* blocks %d" % (
        nblocks, len(blocks), sum(1 for b in blocks if b.startswith("cb"))))
    print()
    print("== templates")
    for t, d in templ.items():
        b = [x["model"] for x in d["decos"] if x["model"] not in NOT_BUILDINGS]
        lamps = sum(1 for x in d["decos"] if x["model"] == "lightpole.flt")
        glows = sum(1 for x in d["decos"] if x["model"] == "w_lightglow.flt")
        print("%s quad %.0fx%.0f m: %d buildings from %d models, lamps %d, glows %d, other %d" % (
            t, d["quad"][0], d["quad"][1], len(b), len(set(b)), lamps, glows,
            len(d["decos"]) - len(b) - lamps - glows))
    print()
    print("== models")
    for m in sorted(rows):
        r = rows[m]
        if r.get("missing"):
            print("%-14s MISSING from gamez, templates %s" % (m, r["templates"]))
            continue
        print("%-14s %5.1f x %5.1f x %5.1f  tris %3d %-10s parts %d copies %d same=%s rgb %s L %s tex %s "
              "direct %s via %s fade %s sub %s" % (
                  m, r["w"], r["d"], r["h"], r["tris"], r["tris_by_part"], r["parts"], r["copies"],
                  r["copies_same_shape"], r["rgb"], r["luma"], ",".join(r["textures"]),
                  r["direct"], r["templates"], r["fade"],
                  [(round(s, 2), t) for s, t in r["substitute"]]))
    print()
    print("== substitutes (source -> target share: same shape, same uv+tex, same texture set)")
    for s in subs:
        print("  %-14s -> %-14s %.2f shape=%s uv=%s texset=%s" % s)
    print()
    print("== lots")
    for lot in lots:
        print("  %-7s (%7.1f, %7.1f) %s" % (lot["template"], lot["x"], lot["z"], " + ".join(lot["models"])))
    if log:
        print()
        print("== log", log["sprites"], log["solids"], log["shapes"], log["shape_tris"])


if __name__ == "__main__":
    main()
