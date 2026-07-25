"""Replicates SceneBuilder.SurfaceForMesh over a chapter's gamez and reports how thin each
mesh's winning vote is. The classifier skips unclassified polygons entirely, so a mesh whose
texture is overwhelmingly unrecognised can be tagged by a handful of stray polys."""
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

def main(root, chapter):
    models = load(root, chapter, 'models')
    materials = load(root, chapter, 'materials')
    textures = load(root, chapter, 'textures')
    mats = materials if isinstance(materials, list) else materials.get('materials', materials)
    meshes = models if isinstance(models, list) else models.get('models', models)

    # textures.json is the name table; a material points into it by index.
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

    thin = collections.Counter(); tagged = collections.Counter(); rows = []
    for i, mesh in enumerate(meshes):
        polys = mesh.get('polygons') if isinstance(mesh, dict) else None
        if not polys: continue
        counts = collections.Counter(); total = len(polys)
        for p in polys:
            # A polygon carries a LIST of material refs, one per UV set.
            for ref in (p.get('materials') or []):
                t = classify(texname(ref.get('material_index')))
                if t: counts[t] += 1
        if not counts: continue
        best, n = counts.most_common(1)[0]
        tagged[best] += 1
        frac = n / total
        rows.append((frac, i, best, n, total))
        if frac < 0.5: thin[best] += 1

    rows.sort()
    print(f"=== {chapter}: {len(meshes)} meshes, {sum(tagged.values())} tagged ===")
    print(f"tagged: {dict(tagged)}")
    print(f"tagged on a MINORITY of their polygons (<50%): {dict(thin)}  total {sum(thin.values())}")
    print("\nthinnest 12 votes (fraction, mesh, tag, votes/polys):")
    for r in rows[:12]:
        print(f"  {r[0]*100:6.2f}%  mesh {r[1]:5d}  {r[2]:<10} {r[3]}/{r[4]}")

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
