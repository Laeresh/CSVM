#!/usr/bin/env python3
"""Which axis order does an anim-def AT_NODE / PUFFER_STATE position use? (BL-221)

Read-only census over the extracted install. The same authored field appears under several
spellings, and this collects all of them:

  compiled (extracted/<CH>/<MISSION>/mis_anim/*.json)
    CallAnimation  parameters.AtNode.position     PufferState     translate (+ at_node)
    Sound          at_node.pos                    SoundNode       translate.AtNode.pos
    LightState     translate.AtNode.pos           DetonateWeapon  at_node.pos
  reader (extracted/**/zrdr/*.zrd.json)
    AT_NODE [name, dx, dy, dz]  — any event, any nesting depth

Two candidate readings of the authored triple (a, b, c), in the engine's frame
(right-handed, Y up, nose -Z — docs/formats/gotchas.md):

  A "verbatim"  (x, y, z) = (a,  b,  c)      — what the engine implements today
  B "Z-up"      (x, y, z) = (a,  c, -b)      — authored b = forward, c = up

Only b or c being nonzero can tell them apart; a pure-X offset is blind. The first
component is spanwise under both readings, so no test here can or needs to touch it.

THREE INSTRUMENTS, weakest last:

  1. PAIRED-SIGN. Two definitions with identical bodies whose offsets differ only in the
     SIGN of component b, applied to node sets whose geometry is independently known to
     differ only in being above vs below their parent. Whichever component flips sign
     between "above" and "below" IS the vertical. Needs no notion of "sane placement".

  2. SIBLING-SPREAD. For a host carrying >=3 distinct offsets, effects are laid out over
     the object: spread along it, near-constant across the short axes. Compare, per
     reading, the SPREAD of the placed points against the host's own bbox extents. A
     spread is invariant to any constant offset, so this is immune to the "effects sit
     above their host" bias that breaks a containment test (see REJECTED below).

  3. KNOWN PLACEMENT. Hosts whose right answer comes from outside the offset — what the
     node is, what the def is named, or a rendered result already pinned by a golden.

REJECTED INSTRUMENT — bbox containment / "escape distance". Scoring each reading by how
far outside the host's bounding box it lands manufactures its own answer: effects
legitimately sit ABOVE flat hosts, and a flat host has a near-zero vertical extent, so any
per-axis-normalised score rules against the vertical reading no matter what the data says.
Run as written it reported 63 A / 101 B, led by `he_ground_effect`@`he_ring` at
escapeA = 11900 — a fireball 12 m over a ground ring whose bbox is 8.4 m wide and 0.0 m
tall. Kept here as a labelled dead end (--rejected reruns it); do not resurrect it.

    python analysis/at-node-axis-order/census.py [extracted_dir] [--rejected]

Findings: FINDINGS.md beside this file.
"""
import collections
import glob
import json
import os
import re
import sys

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def read_a(v):
    return (v[0], v[1], v[2])


def read_b(v):
    return (v[0], v[2], -v[1])


# --------------------------------------------------------------------------- extraction

def compiled_rows(extracted):
    rows = []
    for f in glob.glob(os.path.join(extracted, "*", "*", "mis_anim", "*.json")):
        try:
            d = json.load(open(f, encoding="utf-8"))
        except Exception:
            continue
        anim = d.get("anim_name")
        for s in (d.get("sequences") or []):
            for e in s["events"]:
                kind = next(iter(e["data"]))
                v = e["data"][kind]
                if not isinstance(v, dict):
                    continue
                host = pos = None
                if kind == "CallAnimation":
                    p = v.get("parameters")
                    if isinstance(p, dict) and "AtNode" in p:
                        host, pos = p["AtNode"].get("node"), p["AtNode"].get("position")
                elif kind in ("Sound", "DetonateWeapon"):
                    a = v.get("at_node")
                    if isinstance(a, dict):
                        host, pos = a.get("name"), a.get("pos")
                elif kind == "PufferState":
                    if v.get("translate"):
                        host, pos = v.get("at_node"), v["translate"]
                elif kind in ("SoundNode", "LightState"):
                    t = v.get("translate")
                    if isinstance(t, dict) and "AtNode" in t:
                        host, pos = t["AtNode"].get("name"), t["AtNode"].get("pos")
                if not pos:
                    continue
                rows.append(("compiled", anim, kind, host,
                             (pos.get("x", 0.0), pos.get("y", 0.0), pos.get("z", 0.0))))
    return rows


def reader_rows(extracted):
    rows = []

    def walk(x, anim, out):
        if isinstance(x, list):
            i = 0
            while i < len(x):
                e = x[i]
                if e == "AT_NODE" and i + 1 < len(x) and isinstance(x[i + 1], list):
                    b = x[i + 1]
                    if len(b) >= 4 and all(isinstance(c, (int, float)) for c in b[1:4]):
                        out.append(("reader", anim, "AT_NODE", b[0],
                                    (float(b[1]), float(b[2]), float(b[3]))))
                if e == "ANIMATION_DEFINITION" and i + 1 < len(x) and isinstance(x[i + 1], list):
                    body, nm = x[i + 1], anim
                    for j in range(0, len(body) - 1, 2):
                        if body[j] == "ANIMATION_NAME" and isinstance(body[j + 1], list):
                            nm = body[j + 1][0]
                    walk(body, nm, out)
                    i += 2
                    continue
                walk(e, anim, out)
                i += 1
        elif isinstance(x, dict):
            for v in x.values():
                walk(v, anim, out)

    for f in glob.glob(os.path.join(extracted, "**", "zrdr", "*.zrd.json"), recursive=True):
        try:
            d = json.load(open(f, encoding="utf-8"))
        except Exception:
            continue
        walk(d, os.path.basename(f), rows)
    return rows


def geometry(extracted):
    """name -> (lo, hi) local bbox; name -> max per-chapter occurrence count."""
    bb, count = {}, collections.Counter()
    for ch in CHAPTERS:
        p = os.path.join(extracted, ch, "gamez", "nodes.json")
        if not os.path.exists(p):
            continue
        nodes = json.load(open(p, encoding="utf-8"))
        for name, n in collections.Counter(x["name"] for x in nodes).items():
            count[name] = max(count[name], n)
        for n in nodes:
            boxes = []
            for k in ("node_bbox", "model_bbox", "child_bbox"):
                b = n[k]
                t = (b["a"]["x"], b["a"]["y"], b["a"]["z"], b["b"]["x"], b["b"]["y"], b["b"]["z"])
                if t != (0.0,) * 6:
                    boxes.append(t)
            if not boxes:
                continue
            lo = [min(x[i] for x in boxes) for i in range(3)]
            hi = [max(x[i + 3] for x in boxes) for i in range(3)]
            if n["name"] in bb:
                plo, phi = bb[n["name"]]
                lo = [min(a, b) for a, b in zip(lo, plo)]
                hi = [max(a, b) for a, b in zip(hi, phi)]
            bb[n["name"]] = (lo, hi)
    return bb, count


def placements(extracted, pattern):
    """(name, parent name, local translate) for every node whose name matches."""
    out, seen = [], set()
    for ch in CHAPTERS:
        p = os.path.join(extracted, ch, "gamez", "nodes.json")
        if not os.path.exists(p):
            continue
        nodes = json.load(open(p, encoding="utf-8"))
        for n in nodes:
            if not re.fullmatch(pattern, n["name"] or ""):
                continue
            t = ((n.get("data") or {}).get("Object3d", {}) or {}).get("transform")
            st = t.get("RotateTranslateScale") if isinstance(t, dict) else None
            if not st:
                continue
            tr = st["translate"]
            par = nodes[n["parent_indices"][0]]["name"] if n["parent_indices"] else None
            key = (n["name"], par, round(tr["x"], 2), round(tr["y"], 2), round(tr["z"], 2))
            if key in seen:
                continue
            seen.add(key)
            out.append((n["name"], par, (tr["x"], tr["y"], tr["z"])))
    return out


# ------------------------------------------------------------------------------- tests

def test_paired_sign(extracted, shapes):
    """wvutur* / wvctur*: same def body, offset differing only in the sign of component b."""
    print("=" * 78)
    print("TEST 1 — PAIRED SIGN  (wv_turrets.zrd: wvutur* vs wvctur*)")
    print("=" * 78)
    for anim in ("wvutur", "wvctur"):
        offs = sorted({r[4] for r in shapes if (r[1] or "").startswith(anim)})
        print(f"  {anim}*  AT_NODE healthy  offsets: {offs}")
    for pat, label in ((r"utur\d+", "utur*"), (r"ctur\d+", "ctur*")):
        pl = placements(extracted, pat)
        ys = [p[2][1] for p in pl]
        pars = sorted({p[1] for p in pl})
        print(f"  {label:6s} {len(pl):2d} placements, local y {min(ys):+8.2f} .. {max(ys):+8.2f}, "
              f"parents {pars}")
    print()
    print("  The two definitions are byte-identical apart from the sign of component b")
    print("  (+2 for utur*, -2 for ctur*). utur* are parented to the gasbags at y ~ +43..+57;")
    print("  ctur* hang at y ~ -30..-82 under a parent literally named `underneath`.")
    print("  Component b flips sign exactly with ABOVE vs BELOW  =>  b is the VERTICAL axis.")
    print("  Reading B would make it fore/aft, i.e. the topside turrets blow up 2 m ahead and")
    print("  the underside ones 2 m astern, which nothing in the data or the names motivates.")
    print("  VERDICT: A\n")


def test_sibling_spread(shapes, bb, count):
    """Spread of a host's own offsets vs that host's bbox extents. Constant-offset immune."""
    print("=" * 78)
    print("TEST 2 — SIBLING SPREAD  (hosts carrying >=3 distinct discriminating offsets)")
    print("=" * 78)
    groups = collections.defaultdict(list)
    for r in shapes:
        groups[(r[1], r[3])].append(r[4])
    tally = collections.Counter()
    rowsout = []
    dedupe = set()
    for (anim, host), offs in sorted(groups.items()):
        offs = sorted(set(offs))
        if len(offs) < 3 or count.get(host, 0) != 1 or host not in bb:
            continue
        lo, hi = bb[host]
        # One authored shape replicated per instance (tankerfreight01..12, barge_destroy01..04)
        # is ONE vote, not twelve: dedupe on the offset set and the host's own extents.
        key = (tuple(offs), tuple(round(hi[i] - lo[i], 2) for i in range(3)))
        if key in dedupe:
            continue
        dedupe.add(key)
        ext = [hi[i] - lo[i] for i in range(3)]
        if min(ext[1], ext[2]) < 1.0:      # a degenerate axis cannot bound anything
            continue
        scores = {}
        for name, fn in (("A", read_a), ("B", read_b)):
            pts = [fn(o) for o in offs]
            spread = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
            scores[name] = max(spread[1] / ext[1], spread[2] / ext[2])
        v = "tie" if abs(scores["A"] - scores["B"]) < 1e-6 else min(scores, key=scores.get)
        tally[v] += 1
        rowsout.append((abs(scores["A"] - scores["B"]), v, anim, host, len(offs),
                        scores["A"], scores["B"], ext))
    print(f"  {sum(tally.values())} testable groups: "
          f"A {tally['A']}   B {tally['B']}   tie {tally['tie']}")
    print("  (score = worst spread / host extent on that axis; lower fits the host better)\n")
    for gap, v, anim, host, n, sa, sbv, ext in sorted(rowsout, reverse=True):
        print(f"    {v}  {anim[:26]:26s} @{host[:16]:16s} n={n:2d}  "
              f"A={sa:6.2f} B={sbv:6.2f}   host y/z extent {ext[1]:7.1f}/{ext[2]:7.1f}")
    print()


KNOWN = [
    ("muzzle_burst.zrd.json", "muzzle_burst_ap", (0.0, -0.2, -1.0),
     "a gun muzzle's flash. Host bbox is 0.9 x 0.8 x 0.8 m. A puts it 1 m along -Z, which "
     "IS the nose direction, and 0.2 m under the bore. B puts it 1 m straight DOWN and "
     "0.2 m astern — a muzzle flash below the barrel."),
    ("he_control.zrd.json", "he_ground_effect", (0.0, 12.0, 0.0),
     "the HE ground burst: `call_he_ring` and the trails at (0,0,0), then `large_fireball` "
     "and a second ring `call_he_ring1` at this offset. Host `he_ring` is a FLAT disc "
     "(bbox y = 0.10, radius 4.2) with no meaningful facing. A lifts the fireball 12 m over "
     "the crater; B slides it 12 m sideways along an axis the disc is symmetric about."),
    ("cghookup.zrd.json", "cghookup", (0.0, -8.0, 0.0),
     "a crane's hook-up point. `zcrane`'s bbox runs y -50.4 .. 0.0 — the jib is modelled "
     "downward from its origin, so A hangs the hook 8 m below the arm; B swings it 8 m aft."),
    ("ship.zrd.json", "freighterlite", (0.0, 9.27, 0.0),
     "a freighter's masthead lamp on `freighterlight2`. A raises it 9.3 m up the mast; "
     "B moves it 9.3 m astern at deck level."),
    ("waterfalls.zrd.json", "splashpuffer2/3 @waterfall01", (11.0, 8.0, -8.0),
     "the C1 waterfall's three splash puffers (the `c1-waterfall` golden renders these "
     "today under reading A, and it is pinned). Host bbox spans y -0.0 .. 175 — a tall "
     "falls. A spreads them +-11 m across the fall and 8 m up its face; B drops them 8 m "
     "below the base and 8 m out from the cliff."),
    ("goosepath.zrd.json", "goose_splashleft*/right*", (2.0, 0.2, 0.0),
     "wake spray beside the Goose, the sign of component a set by the def NAME "
     "(left = -2, right = +2). Component b is 0.2: A floats the spray 20 cm above the "
     "waterline, B pushes it 20 cm astern and leaves it exactly at the emitter's height."),
    ("lightning.zrd.json", "lightning @lstage1", (0.0, 200.0, 0.0),
     "the storm's lightning stage. A puts it 200 m up; B puts it 200 m north at ground level."),
    ("volcanosmoke.zrd.json", "volcano1 @world1", (-4480.0, 390.0, -6400.0),
     "the volcano plume, offset from the WORLD ROOT — so the triple is a world position. "
     "A reads it as (x, altitude 390 m, z); B reads it as 6.4 km BELOW sea level."),
]


def test_known(shapes):
    print("=" * 78)
    print("TEST 3 — KNOWN PLACEMENT  (the answer comes from outside the offset)")
    print("=" * 78)
    for src, what, off, why in KNOWN:
        a, b = read_a(off), read_b(off)
        print(f"  {what}   ({off[0]:g}, {off[1]:g}, {off[2]:g})   [{src}]")
        print(f"      A -> ({a[0]:g}, {a[1]:g}, {a[2]:g})    B -> ({b[0]:g}, {b[1]:g}, {b[2]:g})")
        for line in _wrap(why, 68):
            print(f"      {line}")
        print()


def _wrap(s, w):
    out, cur = [], ""
    for word in s.split():
        if len(cur) + len(word) + 1 > w:
            out.append(cur)
            cur = word
        else:
            cur = f"{cur} {word}".strip()
    if cur:
        out.append(cur)
    return out


def test_rejected(shapes, bb, count):
    """The containment/escape test, kept only so its failure mode stays on the record."""
    def escape(pt, box):
        lo, hi = box
        return max(max(lo[i] - pt[i], pt[i] - hi[i], 0.0) / max(hi[i] - lo[i], 1e-3)
                   for i in range(3))

    print("=" * 78)
    print("REJECTED INSTRUMENT — bbox escape distance (biased; do not use)")
    print("=" * 78)
    tally, detail = collections.Counter(), []
    for r in shapes:
        if count.get(r[3], 0) != 1 or r[3] not in bb:
            continue
        ea, eb = escape(read_a(r[4]), bb[r[3]]), escape(read_b(r[4]), bb[r[3]])
        v = "tie" if abs(ea - eb) < 1e-6 else ("A" if ea < eb else "B")
        tally[v] += 1
        detail.append((abs(ea - eb), v, r, ea, eb))
    print(f"  {sum(tally.values())} rows: A {tally['A']}   B {tally['B']}   tie {tally['tie']}")
    print("  Its top rows are all flat hosts, where the vertical extent is ~0 and the score")
    print("  explodes for whichever reading is vertical — the bias, not a finding:")
    for gap, v, r, ea, eb in sorted(detail, reverse=True, key=lambda t: t[0])[:4]:
        print(f"    {v}  {str(r[1])[:24]:24s} @{str(r[3])[:14]:14s} "
              f"({r[4][0]:g},{r[4][1]:g},{r[4][2]:g})  escapeA={ea:9.2f} escapeB={eb:8.2f}")
    print()


# --------------------------------------------------------------------------------- main

def main(extracted="extracted", rejected=False):
    rows = compiled_rows(extracted) + reader_rows(extracted)
    bb, count = geometry(extracted)

    zero = [r for r in rows if r[4] == (0.0, 0.0, 0.0)]
    nz = [r for r in rows if r[4] != (0.0, 0.0, 0.0)]
    blind = [r for r in nz if r[4][1] == 0.0 and r[4][2] == 0.0]
    disc = [r for r in nz if not (r[4][1] == 0.0 and r[4][2] == 0.0)]

    print(f"{len(rows)} AT_NODE positions "
          f"({len([x for x in rows if x[0] == 'compiled'])} compiled, "
          f"{len([x for x in rows if x[0] == 'reader'])} reader)")
    print(f"  {len(zero):5d} exactly zero       — no offset, both readings agree")
    print(f"  {len(blind):5d} pure X (b = c = 0) — blind: A and B are the same point")
    print(f"  {len(disc):5d} discriminating     — A and B differ")
    print("  discriminating by event kind:",
          dict(collections.Counter(r[2] for r in disc).most_common()))

    seen, shapes = set(), []
    for r in disc:
        k = (r[1], r[2], r[3], r[4])
        if k not in seen:
            seen.add(k)
            shapes.append(r)
    print(f"  {len(shapes):5d} distinct (anim, kind, host, offset) authored shapes\n")

    test_paired_sign(extracted, shapes)
    test_sibling_spread(shapes, bb, count)
    test_known(shapes)
    if rejected:
        test_rejected(shapes, bb, count)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    main(args[0] if args else "extracted", "--rejected" in sys.argv)
