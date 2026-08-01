import json, glob, os, collections

rows = []
for f in glob.glob('extracted/*/cam_anim/*.json'):
    try:
        d = json.load(open(f))
    except Exception:
        continue
    base = os.path.basename(f)
    chap = f.replace(os.sep, '/').split('/')[1]
    for s in (d.get('sequences') or []):
        for e in s['events']:
            k = list(e['data'].keys())[0]
            if k != 'ObjectMotion':
                continue
            v = e['data'][k]
            r = v.get('translation_range')
            if not r:
                continue
            def mm(o):
                return (o.get('min'), o.get('max')) if o else None
            rows.append(dict(
                file=base, chap=chap, node=v.get('node'),
                xz=mm(r.get('xz')), y=mm(r.get('y')),
                init=mm(r.get('initial')), delta=mm(r.get('delta')),
                rt=v.get('run_time'), g=(v.get('gravity') or {}).get('value'),
                minonly=v.get('translation_range_min_only'),
                fwd=((v.get('forward_rotation') or {}).get('Time') or {}).get('initial')))

seen, uniq = set(), []
for r in rows:
    key = (r['file'], r['node'], str(r['xz']), str(r['y']), str(r['init']), r['rt'], r['g'])
    if key in seen:
        continue
    seen.add(key)
    uniq.append(r)

print(f"{len(rows)} events, {len(uniq)} distinct shapes")
print("gravity:", collections.Counter(r['g'] for r in rows).most_common())
print("min_only:", collections.Counter(r['minonly'] for r in rows).most_common())
print("run_time:", collections.Counter(r['rt'] for r in rows).most_common(8))
print()
for r in sorted(uniq, key=lambda r: (r['file'], str(r['node']))):
    print(f"{r['file'][:36]:36s} {str(r['node'])[:13]:13s} xz={r['xz']} y={r['y']} "
          f"init={r['init']} delta={r['delta']} rt={r['rt']} g={r['g']} fwd={r['fwd']}")
