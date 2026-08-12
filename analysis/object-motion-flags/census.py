"""What does the install actually author in an `OBJECT_MOTION` GRAVITY block?

The original's parser (`FUN_00508590`) reads five tokens inside `GRAVITY`: `DEFAULT`, `LOCAL`,
`COMPLEX`, `NO_ALTITUDE`, `DO_INTERSECTIONS`. Three of them set a bit in the motion's flag word;
`DEFAULT` and `LOCAL` only choose where the gravity NUMBER comes from and leave no trace in the
compiled data. The extractor therefore surfaces exactly the three booleans that survive
compilation -- see FINDINGS.md.

This census re-derives the cross-tab those three booleans take install-wide, because the published
one (`docs/formats/destructibles.md`) was carried forward from an older count and its `no_altitude`
row did not match a grep of `extracted/`.

Reports:
  Q1  the cross-tab over every ObjectMotion carrying a gravity block, ballistic and not,
      as compiled events and as distinct (file, anim, node) shapes;
  Q2  the same restricted to BALLISTIC events (a `translation` or `translation_range` block) --
      the population the older table was drawn from, and the one the launch model acts on;
  Q3  `no_altitude` in full: every event, with its chapter, def and node;
  Q4  `do_intersections` by def -- the 166 that has been checked twice and must not move;
  Q5  the authored gravity VALUES per flag combination, which is as close as the compiled data
      can get to `GRAVITY LOCAL` vs `GRAVITY DEFAULT`.

Sibling censuses: `analysis/object-motion-range/` (what the launch fields mean),
`analysis/object-motion-ground-rest/` (what happens after the flight),
`analysis/bl-257-nulled-launch/` (how a launch terminates).

Run from the repo root (the one holding `extracted/`):
    python analysis/object-motion-flags/census.py
    python analysis/object-motion-flags/census.py gunshell    # drill into one def
"""

import collections
import glob
import json
import os
import sys


def scan():
    """Every ObjectMotion the RUNTIME can reach.

    ⚠ `sequences` is not all of them. mech3ax's `unknown_seq` is the compiled destruction slot
    (AnimDefinition.DeathSlot), which a real kill dispatches -- BL-276 landed exactly that, after
    ~1,035 of large_30sec_fire's death calls had sat there unread for the project's whole life.
    It holds 15 more ObjectMotion events, every one ballistic with an all-false gravity block, so
    walking `sequences` alone under-counts the default-combination population by 15 (C7,
    2026-08-12). Both blocks are walked here.
    """
    rows = []
    for f in sorted(glob.glob("extracted/*/cam_anim/*.json")):
        try:
            d = json.load(open(f))
        except Exception:
            continue
        chap = f.replace(os.sep, "/").split("/")[1]
        blocks = list(d.get("sequences") or [])
        if isinstance(d.get("unknown_seq"), dict):
            blocks.append(d["unknown_seq"])
        for s in blocks:
            for e in (s.get("events") or []):
                if list(e["data"].keys())[0] != "ObjectMotion":
                    continue
                v = e["data"]["ObjectMotion"]
                g = v.get("gravity")
                rows.append(dict(
                    file=os.path.basename(f), chap=chap, anim=d.get("anim_name"),
                    node=v.get("node"),
                    has_gravity=g is not None,
                    value=(g or {}).get("value"),
                    complex=(g or {}).get("complex"),
                    no_altitude=(g or {}).get("no_altitude"),
                    do_int=(g or {}).get("do_intersections"),
                    ballistic=bool(v.get("translation") or v.get("translation_range")),
                    run_time=v.get("run_time"),
                    bounce=v.get("bounce_sequence") is not None,
                    impact_force=v.get("impact_force"),
                ))
    return rows


def distinct(rs):
    """One row per (file, anim, node) -- the same def compiled into N chapters is one shape."""
    seen, out = set(), []
    for r in rs:
        key = (r["file"], r["anim"], r["node"])
        if key in seen:
            continue
        seen.add(key)
        out.append(r)
    return out


def crosstab(rows, title):
    print(f"--- {title} ---")
    print(f"  {'complex':>8s} {'no_alt':>7s} {'do_int':>7s} {'events':>7s} {'distinct':>9s}")
    c = collections.Counter((r["complex"], r["no_altitude"], r["do_int"]) for r in rows)
    for key in sorted(c, key=lambda t: (-c[t], str(t))):
        sub = [r for r in rows if (r["complex"], r["no_altitude"], r["do_int"]) == key]
        print(f"  {str(key[0]):>8s} {str(key[1]):>7s} {str(key[2]):>7s} "
              f"{c[key]:7d} {len(distinct(sub)):9d}")
    print(f"  {'total':>24s} {len(rows):7d} {len(distinct(rows)):9d}")
    print()


def report(rows):
    grav = [r for r in rows if r["has_gravity"]]
    nograv = [r for r in rows if not r["has_gravity"]]
    print(f"{len(rows)} ObjectMotion events in all 8 chapters' cam_anim "
          f"({len(distinct(rows))} distinct file/anim/node)")
    print(f"  with a gravity block: {len(grav)}   without: {len(nograv)}")
    print(f"  ballistic (translation or translation_range): "
          f"{sum(1 for r in rows if r['ballistic'])}")
    print()

    crosstab(grav, "Q1: every ObjectMotion carrying a gravity block")
    crosstab([r for r in grav if r["ballistic"]], "Q2: ballistic events only")

    print("--- Q3: no_altitude, in full ---")
    na = [r for r in rows if r["no_altitude"]]
    for r in sorted(na, key=lambda r: (r["chap"], str(r["anim"]), str(r["node"]))):
        print(f"  {r['chap']:4s} {str(r['anim'])[:22]:22s} {str(r['node'])[:14]:14s} "
              f"{r['file'][:40]:40s} run_time={r['run_time']}")
    print(f"  -> {len(na)} events, {len(distinct(na))} distinct, "
          f"{len({r['file'] for r in na})} files, {len({r['anim'] for r in na})} anims, "
          f"chapters {','.join(sorted({r['chap'] for r in na}))}")
    print()

    print("--- Q4: do_intersections, by def ---")
    di = [r for r in rows if r["do_int"]]
    by = collections.defaultdict(list)
    for r in di:
        by[(str(r["anim"]), str(r["node"]))].append(r)
    for (anim, node), rs in sorted(by.items()):
        chaps = ",".join(sorted({x["chap"] for x in rs}))
        print(f"  {anim[:26]:26s} {node[:16]:16s} x{len(rs):<3d} [{chaps}]")
    print(f"  -> {len(di)} events, {len(by)} distinct (anim,node)")
    print()

    print("--- Q5a: complex WITHOUT do_intersections, by def (what handling `complex` alone hits) ---")
    cx = [r for r in rows if r["complex"] and not r["do_int"]]
    by = collections.defaultdict(list)
    for r in cx:
        by[(str(r["anim"]), str(r["node"]))].append(r)
    for (anim, node), rs in sorted(by.items()):
        chaps = ",".join(sorted({x["chap"] for x in rs}))
        vals = ",".join(str(v) for v in sorted({x["value"] for x in rs}))
        print(f"  {anim[:26]:26s} {node[:16]:16s} x{len(rs):<3d} g={vals:12s} [{chaps}]")
    print(f"  -> {len(cx)} events, {len(by)} distinct (anim,node)")
    print()

    print("--- Q5: authored gravity values per flag combination ---")
    print("    (the compiled data keeps one float; GRAVITY DEFAULT and GRAVITY LOCAL <v> both")
    print("     land in it, so this is the closest the extract gets to that distinction)")
    c = collections.Counter(
        ((r["complex"], r["no_altitude"], r["do_int"]), r["value"]) for r in grav)
    for key in sorted(c, key=lambda t: (str(t[0]), -c[t])):
        print(f"  complex={str(key[0][0]):5s} no_alt={str(key[0][1]):5s} "
              f"do_int={str(key[0][2]):5s} value={str(key[1]):>7s} {c[key]:6d}")
    print()

    print("--- impact_force (bit 0x2) -- out of scope here, censused so it is not re-derived ---")
    imp = [r for r in rows if r["impact_force"]]
    by = collections.Counter((str(r["anim"]), str(r["node"])) for r in imp)
    for (anim, node), n in sorted(by.items()):
        print(f"  {anim[:26]:26s} {node[:16]:16s} x{n}")
    print(f"  -> {len(imp)} events, {len(by)} distinct (anim,node)")


def drill(rows, want):
    rs = [r for r in rows if r["anim"] == want]
    if not rs:
        print(f"no ObjectMotion for anim {want!r}")
        return
    print(f"=== {want}  ({len(rs)} events, chapters {','.join(sorted({r['chap'] for r in rs}))})")
    for r in sorted(rs, key=lambda r: (r["chap"], str(r["node"]))):
        print(f"  {r['chap']:4s} {str(r['node'])[:14]:14s} gravity={r['value']} "
              f"complex={r['complex']} no_altitude={r['no_altitude']} "
              f"do_intersections={r['do_int']} ballistic={r['ballistic']} "
              f"run_time={r['run_time']} bounce={r['bounce']}")


if __name__ == "__main__":
    all_rows = scan()
    if len(sys.argv) > 1:
        drill(all_rows, sys.argv[1])
    else:
        report(all_rows)
