"""Which launched pieces are still THERE when the dust settles, and which ones did the ORIGINAL
itself ground-test?

Walks every compiled ballistic ObjectMotion event in all 8 chapters' `cam_anim` and answers the
three questions PT-46 (flown 2026-08-08) turned into testable claims:

  1. which launched nodes are never switched off, i.e. stay in the world at their final pose --
     the only pieces a playtester can walk up to and look at. A piece with its own ACTIVE_STATE 0
     downstream is authored to vanish, so where it ended up is unobservable and untestable;
  2. what `do_intersections` -- the original's OWN collision test -- says per launch shape, which
     is what decides whether "the piece went through the ground" is a defect or fidelity;
  3. the intersection of the two: the pieces the original ground-tested AND left lying there.
     That short list is the strict test set.

Sibling census: analysis/bl-257-nulled-launch/ splits the same events by termination field
(RUN_TIME / BOUNCE_SEQUENCE / both / neither). This one asks what happens AFTER the flight.

Run from the repo root:
    python analysis/object-motion-ground-rest/census.py            # the three reports
    python analysis/object-motion-ground-rest/census.py pass_plane01   # drill into one def
"""

import collections
import glob
import json
import os
import sys


def shape_of(v):
    """Which termination shape this ObjectMotion carries (same split as the BL-257 census)."""
    if not (v.get("translation") or v.get("translation_range")):
        return None
    has_rt = v.get("run_time") is not None
    has_bounce = v.get("bounce_sequence") is not None
    if has_rt:
        return "run_time" if not has_bounce else "run_time+bounce"
    return "bounce" if has_bounce else "NEITHER"


def is_switched_off(events, ei, node):
    """Does this node get its own ACTIVE_STATE 0 anywhere after event ei? Asked of the whole tail,
    not just the next event: some defs put CALL_ANIMATIONs in between (see the BL-257 census's Q3).

    Returns a bool, deliberately -- a null-start deactivation carries `start: null`, so returning
    the start value instead would read "switched off on the launch tick" as "never switched off"
    and mark every vanishing piece as persistent."""
    for te in events[ei + 1:]:
        tk = list(te["data"].keys())[0]
        tv = te["data"][tk]
        if (tk == "ObjectActiveState" and isinstance(tv, dict)
                and tv.get("node") == node and tv.get("state") is False):
            return True
    return False


def scan():
    rows = []
    for f in sorted(glob.glob("extracted/*/cam_anim/*.json")):
        try:
            d = json.load(open(f))
        except Exception:
            continue
        chap = f.replace(os.sep, "/").split("/")[1]
        for si, s in enumerate(d.get("sequences") or []):
            events = s["events"]
            for ei, e in enumerate(events):
                if list(e["data"].keys())[0] != "ObjectMotion":
                    continue
                v = e["data"]["ObjectMotion"]
                shape = shape_of(v)
                if shape is None:
                    continue
                node = v.get("node")
                g = v.get("gravity") or {}
                rows.append(dict(
                    file=os.path.basename(f), chap=chap, anim=d.get("anim_name"), node=node,
                    seq=si, shape=shape, run_time=v.get("run_time"),
                    bounce=v.get("bounce_sequence") is not None,
                    do_int=g.get("do_intersections"), g=g.get("value"),
                    persists=not is_switched_off(events, ei, node),
                ))
    return rows


def distinct(rs):
    """One row per (file, anim, node, shape) -- the same def compiled into N chapters is one shape."""
    seen, out = set(), []
    for r in rs:
        key = (r["file"], r["anim"], r["node"], r["shape"])
        if key in seen:
            continue
        seen.add(key)
        out.append(r)
    return out


def report(rows):
    print(f"{len(rows)} ballistic events, {len(distinct(rows))} distinct (file,anim,node,shape)")

    print()
    print("--- Q1: stays in the world (never switched off) x launch shape ---")
    c = collections.Counter((r["shape"], r["persists"]) for r in rows)
    for (shape, p), n in sorted(c.items()):
        dn = len(distinct([r for r in rows if r["shape"] == shape and r["persists"] == p]))
        print(f"  {shape:16s} stays={str(p):5s} {n:5d} events {dn:5d} distinct")

    print()
    print("--- Q2: do_intersections (the original's own collision test) ---")
    c = collections.Counter((r["shape"], r["persists"], r["do_int"]) for r in rows)
    for key in sorted(c, key=lambda t: (t[0], t[1], str(t[2]))):
        shape, p, di = key
        print(f"  {shape:16s} stays={str(p):5s} do_intersections={str(di):5s} {c[key]:5d}")

    print()
    print("--- Q3: ground-tested AND left lying there (the strict test set) ---")
    strict = distinct([r for r in rows if r["persists"] and r["do_int"] is True])
    for r in sorted(strict, key=lambda r: (str(r["anim"]), str(r["node"]))):
        print(f"  {str(r['anim'])[:26]:26s} {str(r['node'])[:16]:16s} {r['chap']:4s} "
              f"shape={r['shape']:16s} run_time={r['run_time']}")
    print(f"  -> {len(strict)} distinct (def,node)")

    print()
    print("--- the pieces that STAY, grouped by def (what a playtester can walk up to) ---")
    by_anim = collections.defaultdict(list)
    for r in distinct([r for r in rows if r["persists"]]):
        by_anim[str(r["anim"])].append(r)
    for anim, rs in sorted(by_anim.items()):
        chaps = ",".join(sorted({x["chap"] for x in rs}))
        shapes = ",".join(sorted({x["shape"] for x in rs}))
        rts = sorted({x["run_time"] for x in rs}, key=lambda t: (t is None, t))
        nodes = ",".join(sorted({str(x["node"]) for x in rs}))
        print(f"{anim[:26]:26s} [{chaps:18s}] {shapes:24s} rt={rts} {nodes[:56]}")


def drill(rows, want):
    """Every launched node of one def, with what happens to it -- the per-part view that shows a
    single def carrying BOTH a resting shape and a sinking one (pass_plane01 is the example)."""
    rs = [r for r in rows if r["anim"] == want]
    if not rs:
        print(f"no ballistic ObjectMotion for anim {want!r}")
        return
    print(f"=== {want}  ({len(rs)} launches, chapters {','.join(sorted({r['chap'] for r in rs}))})")
    for r in sorted(rs, key=lambda r: (r["chap"], r["seq"])):
        print(f"  {r['chap']:4s} seq{r['seq']:<3d} {str(r['node'])[:14]:14s} "
              f"shape={r['shape']:16s} run_time={r['run_time']} do_int={r['do_int']} "
              f"g={r['g']} stays={r['persists']}")


if __name__ == "__main__":
    all_rows = scan()
    if len(sys.argv) > 1:
        drill(all_rows, sys.argv[1])
    else:
        report(all_rows)
