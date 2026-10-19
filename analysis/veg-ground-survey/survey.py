"""Per-chapter ground-texture area census + sprite-card inventory, reusing eligibility.py's loader."""
import json
import math
import os
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "bl-305-clutter-uv"))
import eligibility as E  # noqa: E402
from PIL import Image  # noqa: E402

EX = os.path.join(HERE, "..", "..", "extracted")
OUT = HERE  # survey.json is derived from the game's bytes: never commit it


def png_for(chapter, tex):
    base = os.path.splitext(tex)[0]
    p = os.path.join(EX, chapter, "texture", base + ".png")
    if os.path.exists(p):
        return p
    lower = {f.lower(): f for f in os.listdir(os.path.join(EX, chapter, "texture"))}
    f = lower.get(base.lower() + ".png")
    return os.path.join(EX, chapter, "texture", f) if f else None


def mean_rgb(path):
    if not path:
        return None
    im = Image.open(path).convert("RGB").resize((16, 16), Image.BOX)
    px = list(im.getdata())
    return tuple(round(sum(c[i] for c in px) / len(px)) for i in range(3))


def chapter_census(chapter, templ_names):
    nodes, meshes, materials, _ = E.load_chapter(chapter, EX)
    ground = {}
    for name in templ_names:
        t = E.parse_template(nodes, meshes, materials, name)
        if t is not None:
            ground[t.ground_texture.lower()] = name
    world_idx = next((i for i, n in enumerate(nodes) if n.kind == "World" and n.name.lower() == "world1"), None)
    if world_idx is None:
        return None
    area = defaultdict(float)      # XZ area of up-facing (slope cos >= 0.25) faces, layer-0 texture
    count = defaultdict(int)
    noclut = defaultdict(float)
    flat = defaultdict(float)
    other_layers = defaultdict(set)

    def walk(idx, xf):
        if idx < 0 or idx >= len(nodes):
            return
        n = nodes[idx]
        nm = n.name.lower()
        if nm in ("horizon", "dzpaths") or nm.startswith("fvol"):
            return
        if n.kind in ("Lod", "LoD") and n.lod_range_min != 0.0:
            return
        if n.local is not None:
            xf = E._compose(xf, n.local)
        if 0 <= n.model_index < len(meshes) and meshes[n.model_index] is not None:
            mesh = meshes[n.model_index]
            if mesh.model_type == "Facade":
                pass
            else:
                wv = [E._apply(xf, v) for v in mesh.vertices]
                for poly in mesh.polygons:
                    if not poly.layer_textures or poly.layer_textures[0] is None:
                        continue
                    tex = poly.layer_textures[0].lower()
                    vi = poly.vertex_indices
                    if poly.tri_strip:
                        tris = [(wv[vi[i]], wv[vi[i + 1]], wv[vi[i + 2]]) for i in range(len(vi) - 2)]
                    else:
                        tris = [(wv[vi[0]], wv[vi[i]], wv[vi[i + 1]]) for i in range(1, len(vi) - 1)]
                    a_up = 0.0
                    for a, b, c in tris:
                        bx, by, bz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
                        cx, cy, cz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
                        nx, ny, nz = by * cz - bz * cy, bz * cx - bx * cz, bx * cy - by * cx
                        mag = math.sqrt(nx * nx + ny * ny + nz * nz)
                        if mag == 0 or abs(ny) < 0.25 * mag:
                            continue
                        a_up += 0.5 * abs(ny)
                        if abs(ny) >= 0.8 * mag:
                            flat[tex] += 0.5 * abs(ny)
                    count[tex] += 1
                    area[tex] += a_up
                    if poly.subface:
                        noclut[tex] += a_up
                    for lt in poly.layer_textures[1:]:
                        if lt:
                            other_layers[tex].add(lt.lower())
        for c in n.children:
            walk(c, xf)

    w = nodes[world_idx]
    for c in w.children:
        walk(c, E.IDENTITY)
    for i in (w.world_partition_roots or []):
        walk(i, E.IDENTITY)

    rows = []
    for tex in sorted(area, key=lambda t: -area[t]):
        if area[tex] < 1.0:
            continue
        rows.append({"tex": tex, "area_m2": round(area[tex]), "polys": count[tex], "flat_m2": round(flat[tex]),
                     "noclutter_m2": round(noclut[tex]), "template": ground.get(tex),
                     "layers": sorted(other_layers[tex])[:4],
                     "rgb": mean_rgb(png_for(chapter, tex))})

    # sprite cards: Facade meshes, placed or not
    placed = defaultdict(set)
    for name in templ_names:
        t = E.parse_template(nodes, meshes, materials, name)
        if t:
            for k in t.kinds:
                placed[k.mesh_index].add(name)
    model_nodes = defaultdict(set)
    for n in nodes:
        if 0 <= n.model_index < len(meshes):
            model_nodes[n.model_index].add(n.name)
    cards = []
    for mi, m in enumerate(meshes):
        if m is None or m.model_type != "Facade" or not m.vertices:
            continue
        xs = [v[0] for v in m.vertices]; ys = [v[1] for v in m.vertices]; zs = [v[2] for v in m.vertices]
        cards.append({"mi": mi, "nodes": sorted(model_nodes[mi])[:3], "mode": m.facade_mode,
                      "tex": E.first_texture(m, materials),
                      "w": round(max(max(xs) - min(xs), max(zs) - min(zs)), 2),
                      "h": round(max(ys) - min(ys), 2), "placed_by": sorted(placed.get(mi, []))})
    return {"rows": rows, "cards": cards, "templates": ground}


def main():
    per, ok = E.load_interp_templates(EX)
    assert ok
    out = {}
    for ch in E.CHAPTERS:
        r = chapter_census(ch, per[ch])
        out[ch] = r
        print("==", ch, "templates:", r["templates"] if r else None)
        if not r:
            continue
        tot = sum(x["area_m2"] for x in r["rows"])
        print("  total up-facing area %.2f km2" % (tot / 1e6))
        for x in r["rows"][:30]:
            print("  %-26s %9d m2 %6d polys nc=%-8d %-18s %s %s" % (
                x["tex"], x["area_m2"], x["polys"], x["noclutter_m2"], x["template"] or "-", x["rgb"], x["layers"]))
        for c in r["cards"]:
            print("  CARD %-22s %-24s %-12s w=%-6s h=%-6s placed=%s" % (
                c["tex"], ",".join(c["nodes"]), c["mode"], c["w"], c["h"], c["placed_by"]))
    with open(os.path.join(OUT, "survey.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1)


if __name__ == "__main__":
    main()

