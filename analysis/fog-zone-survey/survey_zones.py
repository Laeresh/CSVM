#!/usr/bin/env python3
"""All-chapter fog/sky zone survey (plan item B11, read-only).

Reads the extracted originals and prints the tables B11's prediction matrix is built on:

  1. per chapter x zone : FOG_COLOR (decoded through the reader's strict >1 rule),
     FOG_RANGES, FOG_ALTITUDE, CLIP_RANGES, SUNLIGHT_DIFFUSE / SUNLIGHT_AMBIENT,
     VIEWING_RANGE HIGH FOG_SCALE / CLIP_SCALE, plus CLOUD_COVER (TOP/BOTTOM/THICKNESS,
     TOP_COLOR/BOTTOM_COLOR);
  2. per chapter : the gamez `horizon` subtree's zone children and how many MESHED nodes
     each holds (the geometry rule that settled C1B/C2/C3), the `World` node's own engine
     fog struct, the `fvol*` volumes' altitude band and their `zone_id` values;
  3. per chapter : `fogvol.zrd`'s `fog_zone`, `distance` and (C5 only) interior-fog keys.

NOTHING here writes. Nothing here is game data - it reads the extracted/ tree in place.

Usage:  python survey_zones.py [--root <path to extracted>] [--mission IA1]
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]

# extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree that does
# (the same env var the engine reads), defaulting to this checkout.
DEFAULT_ROOT = os.path.join(
    os.environ.get("CSVM_DATA_ROOT")
    or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "extracted")


# ---------------------------------------------------------------- zrdr helpers

def alt_pairs(seq):
    """Walk an alternating key/value list, yielding (key, value)."""
    i = 0
    while i + 1 < len(seq):
        k = seq[i]
        if isinstance(k, str):
            yield k, seq[i + 1]
            i += 2
        else:
            i += 1


def alt_get(seq, key):
    for k, v in alt_pairs(seq):
        if k == key:
            return v
    return None


def parse_color(triple):
    """Weather.ParseColor: divide by 255 iff any component is strictly > 1."""
    if triple is None:
        return None, None
    vals = [float(c) for c in triple]
    scaled = any(c > 1.0 for c in vals)
    norm = [c / 255.0 for c in vals] if scaled else vals
    byte = [round(c * 255.0) for c in norm]
    return norm, byte


def fmt_color(triple):
    norm, byte = parse_color(triple)
    if norm is None:
        return "-"
    raw = "[" + ",".join(f"{float(c):g}" for c in triple) + "]"
    return f"{raw} -> {tuple(byte)}"


def fmt_pair(v):
    if v is None:
        return "-"
    return "[" + ", ".join(f"{float(x):g}" for x in v) + "]"


def fmt_scalar(v):
    if v is None:
        return "-"
    if isinstance(v, list):
        v = v[0] if v else None
    if v is None:
        return "-"
    return f"{float(v):g}"


# ---------------------------------------------------------------- weather.json

ZONE_RE = re.compile(r"^ZONE(\d+)$")


def read_weather(path):
    with open(path, "r", encoding="utf-8") as fh:
        root = json.load(fh)
    inner = root[0]

    out = {"zones": [], "cloud_cover": {}, "viewing_range": {}, "precip": None}

    vr = alt_get(inner, "VIEWING_RANGE")
    if vr:
        for level, block in alt_pairs(vr):
            if isinstance(block, list):
                out["viewing_range"][level] = {
                    "CLIP_SCALE": fmt_scalar(alt_get(block, "CLIP_SCALE")),
                    "FOG_SCALE": fmt_scalar(alt_get(block, "FOG_SCALE")),
                }

    cc = alt_get(inner, "CLOUD_COVER")
    if cc:
        # bare-scalar block: keys pair with a bare value or a list
        for k, v in alt_pairs(cc):
            out["cloud_cover"][k] = v

    for key, block in alt_pairs(inner):
        m = ZONE_RE.match(key)
        if not m or not isinstance(block, list):
            continue
        out["zones"].append({
            "name": key,
            "FOG_COLOR": alt_get(block, "FOG_COLOR"),
            "FOG_RANGES": alt_get(block, "FOG_RANGES"),
            "FOG_ALTITUDE": alt_get(block, "FOG_ALTITUDE"),
            "CLIP_RANGES": alt_get(block, "CLIP_RANGES"),
            "SUNLIGHT_DIFFUSE": alt_get(block, "SUNLIGHT_DIFFUSE"),
            "SUNLIGHT_AMBIENT": alt_get(block, "SUNLIGHT_AMBIENT"),
            "SUNLIGHT_ORIENTATION": alt_get(block, "SUNLIGHT_ORIENTATION"),
            "SUNLIGHT_ACTIVE": alt_get(block, "SUNLIGHT_ACTIVE"),
        })

    if alt_get(inner, "TYPE") is not None:
        out["precip"] = alt_get(inner, "TYPE")
    return out


def world_light(diffuse, ambient, k=0.46):
    """Weather.WorldLightFactor: clamp(AMBIENT + DIFFUSE*k, 0.15, 1)."""
    if diffuse is None or ambient is None:
        return None
    d = float(diffuse[0] if isinstance(diffuse, list) else diffuse)
    a = float(ambient[0] if isinstance(ambient, list) else ambient)
    return max(0.15, min(1.0, a + d * k))


# ----------------------------------------------------------------- fogvol.zrd

def read_fogvol(path):
    if not os.path.exists(path):
        return None
    with open(path, "r", encoding="utf-8") as fh:
        root = json.load(fh)
    out = {}
    for key in ("fog_zone", "distance", "fog_fade_dist",
                "interior_fog_fade_dist", "fog_color"):
        out[key] = alt_get(root, key)
    return out


# --------------------------------------------------------------- gamez nodes

def read_nodes(path):
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def node_name(n):
    return n.get("name", "")


def meshed_count(nodes, idx, seen=None):
    """Nodes with a model in the subtree rooted at idx, the node itself included."""
    if seen is None:
        seen = set()
    if idx in seen or idx < 0 or idx >= len(nodes):
        return 0
    seen.add(idx)
    n = nodes[idx]
    total = 1 if n.get("model_index", -1) >= 0 else 0
    for c in n.get("child_indices", []) or []:
        total += meshed_count(nodes, c, seen)
    return total


def subtree_size(nodes, idx, seen=None):
    if seen is None:
        seen = set()
    if idx in seen or idx < 0 or idx >= len(nodes):
        return 0
    seen.add(idx)
    total = 1
    for c in nodes[idx].get("child_indices", []) or []:
        total += subtree_size(nodes, c, seen)
    return total


def survey_gamez(chapter, root):
    path = os.path.join(root, chapter, "gamez", "nodes.json")
    nodes = read_nodes(path)
    by_name = {}
    for i, n in enumerate(nodes):
        by_name.setdefault(node_name(n), []).append(i)

    out = {"horizon": [], "fvol": [], "world_fog": None, "world_area": None}

    # World node's own engine fog struct
    for n in nodes:
        data = n.get("data") or {}
        if "World" in data:
            w = data["World"]
            out["world_fog"] = w.get("fog")
            out["world_area"] = w.get("area")
            break

    # horizon subtree
    for idx in by_name.get("horizon", []):
        for c in nodes[idx].get("child_indices", []) or []:
            child = nodes[c]
            out["horizon"].append({
                "zone": node_name(child),
                "model_index": child.get("model_index", -1),
                "children": len(child.get("child_indices", []) or []),
                "meshed": meshed_count(nodes, c),
                "nodes": subtree_size(nodes, c),
                "zone_id": child.get("zone_id"),
            })

    # fvol volumes
    fvre = re.compile(r"^fvol\d+$")
    for i, n in enumerate(nodes):
        if not fvre.match(node_name(n)):
            continue
        bb = n.get("model_bbox") or {}
        a, b = bb.get("a") or {}, bb.get("b") or {}
        out["fvol"].append({
            "name": node_name(n),
            "zone_id": n.get("zone_id"),
            "ymin": a.get("y"),
            "ymax": b.get("y"),
        })
    return out


# ------------------------------------------------------------------- printing

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=DEFAULT_ROOT,
                    help="the extracted/ tree (default: $CSVM_DATA_ROOT/extracted, else this checkout's)")
    ap.add_argument("--mission", default="IA1")
    args = ap.parse_args()

    print("=" * 100)
    print(f"B11 all-chapter fog/sky zone survey  (mission {args.mission}, root {args.root})")
    print("=" * 100)

    for ch in CHAPTERS:
        mdir = os.path.join(args.root, ch, args.mission)
        wpath = os.path.join(mdir, "zrdr", "weather.zrd.json")
        used_mission = args.mission
        if not os.path.exists(wpath):
            cands = sorted(
                d for d in os.listdir(os.path.join(args.root, ch))
                if os.path.exists(os.path.join(args.root, ch, d, "zrdr", "weather.zrd.json"))
            )
            if not cands:
                print(f"\n### {ch}: NO weather.zrd.json anywhere -- skipped")
                continue
            used_mission = cands[0]
            wpath = os.path.join(args.root, ch, used_mission, "zrdr", "weather.zrd.json")
            print(f"\n!!! {ch}: {args.mission} missing -- substituting {used_mission}")

        w = read_weather(wpath)
        g = survey_gamez(ch, args.root)
        fv = read_fogvol(os.path.join(args.root, ch, "zrdr", "fogvol.zrd.json"))

        print(f"\n### {ch} / {used_mission}")
        vr_high = w["viewing_range"].get("HIGH", {})
        print(f"  VIEWING_RANGE HIGH: FOG_SCALE {vr_high.get('FOG_SCALE','-')}  "
              f"CLIP_SCALE {vr_high.get('CLIP_SCALE','-')}")
        cc = w["cloud_cover"]
        cc_bits = []
        for k in ("BOTTOM", "TOP", "THICKNESS"):
            if k in cc:
                cc_bits.append(f"{k} {float(cc[k]):g}")
        for k in ("BOTTOM_COLOR", "TOP_COLOR"):
            if k in cc:
                cc_bits.append(f"{k} {fmt_color(cc[k])}")
        print(f"  CLOUD_COVER: {'  '.join(cc_bits) if cc_bits else '-'}")
        if cc.get("TOP") is not None and cc.get("BOTTOM") is not None:
            centre = (float(cc["TOP"]) + float(cc["BOTTOM"])) / 2.0
            print(f"  CLOUD_COVER centre: {centre:g}")
        if w["precip"]:
            print(f"  precipitation: {w['precip']}")

        for z in w["zones"]:
            wl = world_light(z["SUNLIGHT_DIFFUSE"], z["SUNLIGHT_AMBIENT"])
            print(f"  {z['name']}:")
            print(f"      FOG_COLOR    {fmt_color(z['FOG_COLOR'])}")
            print(f"      FOG_RANGES   {fmt_pair(z['FOG_RANGES'])}")
            print(f"      FOG_ALTITUDE {fmt_pair(z['FOG_ALTITUDE'])}")
            print(f"      CLIP_RANGES  {fmt_pair(z['CLIP_RANGES'])}")
            print(f"      SUNLIGHT     diffuse {fmt_scalar(z['SUNLIGHT_DIFFUSE'])} / "
                  f"ambient {fmt_scalar(z['SUNLIGHT_AMBIENT'])} "
                  f"(WorldLight {wl:.3f})" if wl is not None else "")
            print(f"      SUN_ORIENT   {fmt_pair(z['SUNLIGHT_ORIENTATION'])}  "
                  f"active {fmt_scalar(z['SUNLIGHT_ACTIVE'])}")

        hz = ", ".join(f"{h['zone']} meshed={h['meshed']} nodes={h['nodes']} "
                       f"zone_id={h['zone_id']}" for h in g["horizon"])
        print(f"  horizon subtree: {hz or '-'}")

        if g["fvol"]:
            ys = [v["ymin"] for v in g["fvol"] if v["ymin"] is not None]
            ye = [v["ymax"] for v in g["fvol"] if v["ymax"] is not None]
            zids = sorted({v["zone_id"] for v in g["fvol"]})
            print(f"  fvol: {len(g['fvol'])} volumes, band "
                  f"{min(ys):g} .. {max(ye):g} m, zone_id {zids}")
        else:
            print("  fvol: none")

        if fv:
            bits = [f"fog_zone {fv['fog_zone']}", f"distance {fmt_scalar(fv['distance'])}"]
            if fv["fog_fade_dist"] is not None:
                bits.append(f"fog_fade_dist {fmt_scalar(fv['fog_fade_dist'])}")
            if fv["interior_fog_fade_dist"] is not None:
                bits.append(f"interior_fog_fade_dist {fmt_scalar(fv['interior_fog_fade_dist'])}")
            if fv["fog_color"] is not None:
                bits.append(f"fog_color {fmt_color(fv['fog_color'])}")
            print(f"  fogvol.zrd: {'  '.join(bits)}")
        else:
            print("  fogvol.zrd: absent")

        wf = g["world_fog"]
        if wf:
            print(f"  World node engine fog: type {wf.get('fog_type')} "
                  f"color {wf.get('fog_color')} range {wf.get('fog_range')} "
                  f"altitude {wf.get('fog_altitude')} density {wf.get('fog_density')}")

    # ---- cross-mission check: does FOG_ALTITUDE ever vary within a chapter?
    print("\n" + "=" * 100)
    print("Per-chapter cross-mission check (every mission's weather.zrd.json)")
    print("=" * 100)
    for ch in CHAPTERS:
        cdir = os.path.join(args.root, ch)
        rows = []
        for m in sorted(os.listdir(cdir)):
            wp = os.path.join(cdir, m, "zrdr", "weather.zrd.json")
            if not os.path.exists(wp):
                continue
            w = read_weather(wp)
            sig = "; ".join(
                f"{z['name']} alt {fmt_pair(z['FOG_ALTITUDE'])} rng {fmt_pair(z['FOG_RANGES'])} "
                f"col {(parse_color(z['FOG_COLOR'])[1])}"
                for z in w["zones"])
            rows.append((m, sig))
        uniq = {}
        for m, sig in rows:
            uniq.setdefault(sig, []).append(m)
        print(f"\n{ch}: {len(rows)} missions, {len(uniq)} distinct zone signatures")
        for sig, ms in uniq.items():
            print(f"   [{','.join(ms)}]  {sig}")


if __name__ == "__main__":
    sys.exit(main())
