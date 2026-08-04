"""Ad-hoc: extract each cblock template's decoration local positions within one period cell,
to compare against the painted street/building pattern in the ground texture."""
import sys, os, json
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "item9-depth-bias"))
import item9_lib as lib

ch = lib.Chapter("C5")

def node_kind(n):
    return lib.node_kind(n)

def mesh_of(n):
    mi = lib.model_index(n)
    if mi is None or mi < 0 or mi >= len(ch.models):
        return None
    return ch.models[mi]

def first_with_mesh(idx, include_self=True, depth=0):
    n = ch.nodes[idx]
    if include_self:
        m = mesh_of(n)
        if m and m.get("polygons"):
            return idx
    for c in (n.get("child_indices") or n.get("children") or []):
        if 0 <= c < len(ch.nodes):
            r = first_with_mesh(c, True, depth + 1)
            if r is not None:
                return r
    return None

def report(template_name):
    root_idx = None
    for i, n in enumerate(ch.nodes):
        if (n.get("name") or "").lower() == template_name.lower():
            root_idx = i
            break
    ground_idx = first_with_mesh(root_idx, include_self=True)
    gnode = ch.nodes[ground_idx]
    gmesh = mesh_of(gnode)
    verts = gmesh.get("vertices") or []
    xs = [v["x"] for v in verts]; zs = [v["z"] for v in verts]
    minx, maxx = min(xs), max(xs)
    minz, maxz = min(zs), max(zs)
    period = max(maxx - minx, maxz - minz)
    tex = ch.tex_name(( (gmesh.get("polygons") or [{}])[0].get("materials") or [{}])[0].get("material_index"))
    print(f"== {template_name}: ground node {ground_idx}, tex={tex}, period={period:.1f}, min=({minx:.1f},{minz:.1f})")

    solids = {}
    for c in (gnode.get("child_indices") or gnode.get("children") or []):
        deco = ch.nodes[c]
        deco_mesh_idx = first_with_mesh(c, include_self=False)
        if deco_mesh_idx is None:
            continue
        dn = ch.nodes[deco_mesh_idx]
        dmesh = mesh_of(dn)
        polys = dmesh.get("polygons") or []
        # crude solid classifier: >1 polygon => "3D decoration" not a 1-poly sprite card
        loc = lib.node_local(deco) or lib.mat_identity()
        ox, oy, oz = loc[3]
        cellx, cellz = ox - minx, oz - minz
        name = deco.get("name", "?")
        solids.setdefault(name, []).append((cellx, cellz, len(polys)))
    for name, pts in sorted(solids.items()):
        xs2 = [p[0] for p in pts]; zs2 = [p[1] for p in pts]
        print(f"  {name}: n={len(pts)} polys={pts[0][2]} x[{min(xs2):.0f},{max(xs2):.0f}] z[{min(zs2):.0f},{max(zs2):.0f}]")

report("cblock1")
report("cblock4")
