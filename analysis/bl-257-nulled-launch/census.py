"""BL-257: census the OBJECT_MOTION launch shape that names NEITHER `run_time` NOR
`bounce_sequence` — the third shape beside BL-240's solved bounce and BL-245's falls.

Walks every compiled ObjectMotion event in all 8 chapters' `cam_anim` and answers the three
questions `backlog.md` BL-257 makes the precondition of any fix:

  1. how many events omit both fields;
  2. do they all launch upward (i.e. does a parabola apex exist for every draw of the range);
  3. is the following event, in every one, that same node's own deactivation.

Run from the repo root:  python analysis/bl-257-nulled-launch/census.py
"""

import collections
import glob
import json
import os

EPS = 1e-6


def shape_of(v):
    """Which of the three launch shapes this ObjectMotion carries, or None if not ballistic."""
    if not (v.get("translation") or v.get("translation_range")):
        return None
    has_rt = v.get("run_time") is not None
    has_bounce = v.get("bounce_sequence") is not None
    if has_rt:
        return "run_time" if not has_bounce else "run_time+bounce"
    return "bounce" if has_bounce else "NEITHER"


def elevation_span(v):
    """(min, max) launch elevation in degrees, or None when the shape carries no elevation.

    `translation_range` is polar (the retired analysis/object-motion-range, `git show
    analysis-archive:analysis/object-motion-range/FINDINGS.md`): `y` is an ELEVATION in
    degrees, and it is applied LINEARLY -- v0y = (elev/90)*speed, not sin(elev)*speed
    (docs/org/objectMotion.md). `translation` is cartesian: its `initial.y` IS the vertical
    speed, so its sign answers 'upward?' directly and is reported as +90/-90.
    """
    r = v.get("translation_range")
    if r:
        y = r.get("y") or {}
        lo, hi = y.get("min") or 0.0, y.get("max") or 0.0
        if v.get("translation_range_min_only"):
            hi = lo  # the max fields are all 0 and meaningless: the min IS the value
        return (min(lo, hi), max(lo, hi))
    t = v.get("translation")
    if t:
        vy = (t.get("initial") or {}).get("y") or 0.0
        rnd = (t.get("rnd_xz") or {}).get("y") or 0.0
        lo, hi = vy - abs(rnd), vy + abs(rnd)
        return (90.0 if lo > 0 else -90.0, 90.0 if hi > 0 else -90.0)
    return None


rows = []
for f in sorted(glob.glob("extracted/*/cam_anim/*.json")):
    try:
        d = json.load(open(f))
    except Exception:
        continue
    base = os.path.basename(f)
    chap = f.replace(os.sep, "/").split("/")[1]
    for si, s in enumerate(d.get("sequences") or []):
        events = s["events"]
        for ei, e in enumerate(events):
            k = list(e["data"].keys())[0]
            if k != "ObjectMotion":
                continue
            v = e["data"][k]
            shape = shape_of(v)
            if shape is None:
                continue
            nxt = events[ei + 1] if ei + 1 < len(events) else None
            nk = list(nxt["data"].keys())[0] if nxt else None
            nv = nxt["data"][nk] if nxt else None
            # Everything after the launch, so "is the piece switched off downstream?" can be
            # asked of the whole tail — `pwr_engines` puts three CALL_ANIMATIONs in between.
            tail = []
            for te in events[ei + 1:]:
                tk = list(te["data"].keys())[0]
                tv = te["data"][tk]
                tail.append((tk, tv.get("node") if isinstance(tv, dict) else None,
                             tv.get("state") if isinstance(tv, dict) else None,
                             te.get("start")))
            rows.append(
                dict(
                    file=base,
                    chap=chap,
                    anim=d.get("anim_name"),
                    node=v.get("node"),
                    shape=shape,
                    elev=elevation_span(v),
                    speed=(v.get("translation_range") or {}).get("initial"),
                    g=(v.get("gravity") or {}).get("value"),
                    do_int=(v.get("gravity") or {}).get("do_intersections"),
                    next_kind=nk,
                    next_node=(nv or {}).get("node") if isinstance(nv, dict) else None,
                    next_state=(nv or {}).get("state") if isinstance(nv, dict) else None,
                    next_start=(nxt or {}).get("start"),
                    tail=tail,
                    seq_len=len(events),
                    seq_idx=si,
                )
            )


def distinct(rs):
    """One row per (file, anim, node, shape) — the same def compiled into N chapters is one shape."""
    seen, out = set(), []
    for r in rs:
        key = (r["file"], r["anim"], r["node"], r["shape"])
        if key in seen:
            continue
        seen.add(key)
        out.append(r)
    return out


print(f"{len(rows)} ballistic ObjectMotion events, {len(distinct(rows))} distinct (file,anim,node)")
print()
print("--- Q1: how many omit BOTH run_time and bounce_sequence ---")
for shape, n in collections.Counter(r["shape"] for r in rows).most_common():
    print(f"  {shape:16s} {n:5d} events   {len(distinct([r for r in rows if r['shape'] == shape])):5d} distinct")

neither = [r for r in rows if r["shape"] == "NEITHER"]
print()
print("--- Q2: do they all launch upward? ---")
print("  gravity:", collections.Counter(r["g"] for r in neither).most_common())
print("  do_intersections:", collections.Counter(r["do_int"] for r in neither).most_common())


def vertical_speed_span(r):
    """(min, max) of the launch's vertical speed v0y over every draw the range allows.

    Polar: v0y = (elevation/90)*speed -- a LINEAR elevation, not sin(elevation)
    (docs/org/objectMotion.md). A NEGATIVE speed flips an upward elevation into a downward
    launch; `fuelboxbreaks`' rockerarm is elevation 90 deg at speed -45..45. The two readings
    agree on every sign over [-90, 90], so the upward/no-apex split this census reports is the
    same under either. Cartesian: v0y is `initial.y +/- |rnd_xz.y|` outright.
    """
    e = r["elev"]
    if e is None:
        return None
    if r["speed"] is None:  # cartesian: elev is already the +90/-90 sign encoding
        return (-1.0 if e[0] <= 0 else 1.0, -1.0 if e[1] <= 0 else 1.0)
    lo, hi = r["speed"].get("min") or 0.0, r["speed"].get("max") or 0.0
    slo, shi = min(lo, hi), max(lo, hi)
    verticals = [a / 90.0 for a in e]
    products = [s * v for s in verticals for v in (slo, shi)]
    return (min(products), max(products))


def apex_class(r):
    """Whether FlightToLaunchHeight would solve this launch: it needs v0y > 0 and ay < 0."""
    if not r["g"] or r["g"] >= 0:
        return "no gravity -> no apex"
    span = vertical_speed_span(r)
    if span is None:
        return "no translation channel"
    lo, hi = span
    if lo > EPS:
        return "always up"
    if hi <= EPS:
        return "always level/down"
    return "straddles 0 (some draws down)"


print("  apex:", collections.Counter(apex_class(r) for r in neither).most_common())
for r in distinct([r for r in neither if apex_class(r) != "always up"]):
    print(f"    NO-APEX {r['file'][:40]:40s} {str(r['node'])[:12]:12s} "
          f"v0y span={vertical_speed_span(r)} -> {apex_class(r)}")
print()
print("--- Q3: is the next event this node's own deactivation? ---")


def next_class(r):
    if r["next_kind"] is None:
        return "no following event"
    if r["next_kind"] != "ObjectActiveState":
        return f"next is {r['next_kind']}"
    if r["next_node"] != r["node"]:
        return "ObjectActiveState on ANOTHER node"
    if r["next_state"] is not False:
        return "ObjectActiveState(true) on this node"
    return "own deactivation" + ("" if r["next_start"] is None else " (timed, not null-start)")


print("  next event:", collections.Counter(next_class(r) for r in neither).most_common())


def tail_class(r):
    """Same question asked of the whole tail: is the piece switched off anywhere after the launch,
    and does everything in between run null-start (i.e. chained behind the flight)?"""
    off = [t for t in r["tail"] if t[0] == "ObjectActiveState" and t[1] == r["node"] and t[2] is False]
    if not off:
        return "never switched off" if r["tail"] else "launch is the last event"
    timed = [t for t in r["tail"] if t[3] is not None]
    return "own deactivation downstream" + (" (a timed event intervenes)" if timed else ", all null-start")


print("  whole tail:", collections.Counter(tail_class(r) for r in neither).most_common())
print()
print("--- the distinct NEITHER shapes ---")
for r in sorted(distinct(neither), key=lambda r: (r["file"], str(r["anim"]), str(r["node"]))):
    print(
        f"{r['file'][:44]:44s} {str(r['anim'])[:22]:22s} {str(r['node'])[:12]:12s} "
        f"elev={r['elev']} spd={r['speed']} g={r['g']} next={next_class(r)}"
    )
