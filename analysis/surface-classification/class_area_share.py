"""How much of a chapter's collidable surface belongs to each IMPACT surface class.

Replicates SceneBuilder.ClassifySurface per polygon (the shipped rule: each polygon's own
texture decides its bucket) and reports polygon counts, triangulated area share, and which
building textures were matched. Read-only over extracted/*/gamez; run from the repo root:

    python analysis/surface-classification/class_area_share.py [CHAPTER ...]
"""
import json,collections,sys,math
def classify(t):
    if not t: return None
    t=t.lower()
    if 'shadow' in t: return 'water' if (t.startswith('water') or 'splash' in t) else None
    if t.startswith('water') or t.startswith('wtr') or t.startswith('srf') or 'wakefront' in t or 'watersquirt' in t: return 'water'
    if ('build' in t or t.startswith('hangar') or t.startswith('bld') or 'cblock' in t
        or 'warehouse' in t or 'roof' in t or 'filmblock' in t or t.startswith('empire') or t.startswith('chrysler')):
        return 'buildings'
    return None
def area(vs, idx):
    a=0.0
    for i in range(1,len(idx)-1):
        p0,p1,p2=vs[idx[0]],vs[idx[i]],vs[idx[i+1]]
        ux,uy,uz=p1['x']-p0['x'],p1['y']-p0['y'],p1['z']-p0['z']
        vx,vy,vz=p2['x']-p0['x'],p2['y']-p0['y'],p2['z']-p0['z']
        cx,cy,cz=uy*vz-uz*vy, uz*vx-ux*vz, ux*vy-uy*vx
        a+=0.5*math.sqrt(cx*cx+cy*cy+cz*cz)
    return a
CHAPTERS = sys.argv[1:] or ['C1','C1B','C1C','C2','C2B','C3','C4','C5']
for ch in CHAPTERS:
    mats=json.load(open(f'extracted/{ch}/gamez/materials.json'))
    texs=[t['name'] for t in json.load(open(f'extracted/{ch}/gamez/textures.json'))]
    models=json.load(open(f'extracted/{ch}/gamez/models.json'))
    matTex=[]
    for m in mats:
        n=None
        for k,v in m.items():
            if k=='Textured':
                ti=v.get('texture_index',-1)
                n=texs[ti] if 0<=ti<len(texs) else None
        matTex.append(n)
    cnt=collections.Counter(); ar=collections.Counter(); btex=collections.Counter()
    for md in models:
        vs=md['vertices']
        for p in md['polygons']:
            mi=p['materials'][0]['material_index'] if p.get('materials') else -1
            t=matTex[mi] if 0<=mi<len(matTex) else None
            cls=classify(t)
            cnt[cls]+=1
            try: a=area(vs,p['vertex_indices'])
            except Exception: a=0.0
            ar[cls]+=a
            if cls=='buildings': btex[t]+=1
    tot=sum(ar.values())
    print(f"{ch}: polys {dict(cnt)}")
    print(f"    area m2 " + ", ".join(f"{k}={v:,.0f} ({100*v/tot:.2f}%)" for k,v in ar.items()))
    print(f"    building textures: {dict(btex)}")
