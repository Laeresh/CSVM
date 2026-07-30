"""Replicates SceneBuilder.SurfaceForMesh over a chapter's gamez and compares the shipped
area-quorum vote against the count vote it replaced. The old classifier skipped unclassified
polygons entirely, so a mesh whose texture was overwhelmingly unrecognised could be tagged by
a handful of stray polys; the shipped rule weights by triangulated polygon area (strip order
for tri_strips, a fan otherwise — matching EmitPolygon) and requires the winning tag to cover
at least half the mesh's total area, unclassified area counting as abstentions."""
import json, sys, zipfile, io, os, collections

def load(root, chapter, name):
    d = os.path.join(root, 'extracted', chapter)
    p = os.path.join(d, name + '.json')
    if os.path.exists(p):
        return json.load(io.open(p, encoding='utf-8'))
    z = os.path.join(d, 'gamez.zip')
    with zipfile.ZipFile(z) as zf:
        return json.loads(zf.read(name + '.json').decode('utf-8'))

def classify(tex):
    if not tex: return None
    t = tex.lower()
    if 'shadow' in t:
        return 'water' if (t.startswith('water') or 'splash' in t) else None
    if t.startswith('water') or t.startswith('wtr') or t.startswith('srf') \
       or 'wakefront' in t or 'watersquirt' in t:
        return 'water'
    if 'build' in t or t.startswith('hangar') or t.startswith('bld') \
       or 'cblock' in t or 'warehouse' in t or 'roof' in t or 'filmblock' in t:
        return 'buildings'
    return None

def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])

def sub(a, b):
    return (a[0]-b[0], a[1]-b[1], a[2]-b[2])

def poly_area(verts, poly):
    """Triangulated exactly as SceneBuilder.EmitPolygon / PolygonArea: strip order when
    flags.tri_strip, a fan from vertex 0 otherwise."""
    idx = poly.get('vertex_indices') or []
    n = len(idx)
    if n < 3: return 0.0
    def tri(a, b, c):
        va, vb, vc = verts[idx[a]], verts[idx[b]], verts[idx[c]]
        v = cross(sub(vb, va), sub(vc, va))
        return 0.5 * (v[0]*v[0] + v[1]*v[1] + v[2]*v[2]) ** 0.5
    strip = (poly.get('flags') or {}).get('tri_strip', False)
    rng = range(n - 2) if strip else range(1, n - 1)
    return sum(tri(i, i+1, i+2) if strip else tri(0, i, i+1) for i in rng)

def vec(v):
    return (v['x'], v['y'], v['z']) if isinstance(v, dict) else tuple(v)

def main(root, chapter):
    models = load(root, chapter, 'models')
    materials = load(root, chapter, 'materials')
    textures = load(root, chapter, 'textures')
    mats = materials if isinstance(materials, list) else materials.get('materials', materials)
    meshes = models if isinstance(models, list) else models.get('models', models)
    texlist = textures if isinstance(textures, list) else textures.get('texture_names', textures)

    def texat(i):
        if i is None or i < 0 or i >= len(texlist): return None
        t = texlist[i]
        return t if isinstance(t, str) else (t.get('name') if isinstance(t, dict) else None)

    def texname(mi):
        # A material is a tagged union: {"Textured": {...}} or {"Colored": {...}}.
        if mi is None or mi < 0 or mi >= len(mats): return None
        tex = mats[mi].get('Textured') if isinstance(mats[mi], dict) else None
        return texat(tex.get('texture_index')) if isinstance(tex, dict) else None

    old_tagged = collections.Counter(); old_thin = collections.Counter()
    new_tagged = collections.Counter()
    changed = []
    for i, mesh in enumerate(meshes):
        polys = mesh.get('polygons') if isinstance(mesh, dict) else None
        if not polys: continue
        verts = [vec(v) for v in (mesh.get('vertices') or [])]
        counts = collections.Counter(); areas = collections.Counter(); total_area = 0.0
        for p in polys:
            a = poly_area(verts, p)
            total_area += a
            # The engine reads the polygon's FIRST material ref (GameZ.cs materials[0]).
            refs = p.get('materials') or []
            t = classify(texname(refs[0].get('material_index'))) if refs else None
            if t:
                counts[t] += 1
                areas[t] += a
        if not counts: continue
        # Old rule: count plurality over classified polygons only.
        old, n = counts.most_common(1)[0]
        old_tagged[old] += 1
        if n / len(polys) < 0.5: old_thin[old] += 1
        # Shipped rule: area plurality with a >=50%-of-total-area quorum.
        new, na = areas.most_common(1)[0]
        if total_area <= 0 or na < 0.5 * total_area: new = None
        if new: new_tagged[new] += 1
        if new != old: changed.append((i, old, new, n, len(polys), (na/total_area if total_area else 0)))

    print(f"=== {chapter}: {len(meshes)} meshes ===")
    print(f"old count-vote:  tagged {sum(old_tagged.values())} {dict(old_tagged)}; "
          f"tagged on a MINORITY of their own polygons: {sum(old_thin.values())} {dict(old_thin)}")
    print(f"area-quorum:     tagged {sum(new_tagged.values())} {dict(new_tagged)}; "
          f"minority-area tags: 0 by construction (quorum)")
    print(f"reclassified: {len(changed)} meshes (first 10):")
    for i, old, new, n, tp, af in changed[:10]:
        print(f"  mesh {i:5d}: {old} ({n}/{tp} polys) -> {new or 'default'} "
              f"(winner covered {af*100:.0f}% of area)")

if __name__ == '__main__':
    for ch in sys.argv[2:] or ['C2']:
        main(sys.argv[1], ch)
