#!/usr/bin/env python3
"""When an OBJECT_ACTIVE_STATE INACTIVE stops a puffer emitter, may it stop one that
started in the same instant? (BL-229)

Read-only census over the extracted install. Two rules are on the table for the runtime's
host-deactivation stop (`EmitterDirector.EndOn`, landed for `BL-224`):

  RULE 1 "survives its own instant" — a deactivation stops every emitter under the host
          EXCEPT one that started in the same instant as the deactivation.
  RULE 2 "stops everything"          — what ships today: the host going inactive ends every
          emitter under it, whenever it started.

The symptom (`plane_big_splash`) is a sequence of three offset-less events:

    OBJECT_ACTIVE_STATE sp_1 ACTIVE
    CALL_ANIMATION      hg_splasher WITH_NODE sp_1     (starts the `splasher` puffer there)
    OBJECT_ACTIVE_STATE sp_1 INACTIVE

All three fire in one runtime instant (an absent START_TIME is "EVENT_OFFSET 0" and none of
these three kinds reports a duration — SequenceRunner.cs), so under RULE 2 the splash puffer
is stopped on the tick it started, while `hg_splasher` itself authors a 0.5 s run.

The question this settles is NOT "does that one def look wrong" — it is whether the idiom is
used install-wide, and whether RULE 1 can be adopted without breaking the population that
BL-224 built the stop for in the first place (`he_trails`' five spurt columns, whose ONLY
authored stop is their host going inactive).

FOUR MEASUREMENTS:

  1. THE IDIOM. Every (activate N ... deactivate N) pair in one sequence that has an emitter
     start on N between them, split by whether the deactivation shares the start's instant.
     Same-instant pairs are the population RULE 1 changes; later-instant pairs are the
     population BL-224 needs and RULE 1 must not touch.

  2. AUTHORED INTENT. For each same-instant pair, the run the emitter's own definition
     authors — its `PUFFER_STATE ACTIVE_STATE 0` / `STOP_SEQUENCE` time. A pair whose
     authored run is > 0 is one RULE 2 provably cuts short. A pair whose authored run is 0
     would be evidence FOR rule 2, and is reported separately and loudly.

  3. INSTANT SEPARATION. The gap, in authored seconds, between the emitter start and the
     host deactivation for the later-instant population. RULE 1 keys on the instant, not on
     a time threshold, so this only has to show the two populations do not touch.

  4. THE READER CORPUS. The same idiom counted over the authored reader source
     (`extracted/**/zrdr/*.zrd.json`), an independent front-end, to tell a hand-written
     idiom from a compiler artefact.

    python analysis/bl-229-emitter-host-deactivation/census.py [extracted_dir] [--list]

Findings: FINDINGS.md beside this file.
"""
import collections
import glob
import json
import os
import sys

# Event kinds whose dispatch reports a run time, so the NEXT event is pushed into a later
# instant even with no START_TIME of its own (AnimRuntime.Dispatch's `duration` out-param).
# A kind whose run time we cannot read is treated as nonzero — that closes the instant, which
# UNDER-counts the idiom rather than inventing it.
DURATION_KINDS = {
    "ObjectMotionFromTo": "run_time",
    "ObjectOpacityFromTo": "run_time",
    "ObjectMotion": "run_time",
    "ObjectMotionSiScript": None,
    "ObjectMotionSiScriptAllNames": None,
}

SELF_NODES = (None, "INPUT_NODE", "MAIN_ROOT_NODE")


# --------------------------------------------------------------------------- compiled load

def load_compiled(extracted):
    """Every compiled anim definition: the per-mission archives and the shared cam_anim set."""
    paths = glob.glob(os.path.join(extracted, "*", "*", "mis_anim", "*.json"))
    paths += glob.glob(os.path.join(extracted, "*", "cam_anim", "*.json"))
    defs = []
    for p in sorted(paths):
        if os.path.basename(p) == "metadata.json":
            continue
        try:
            d = json.load(open(p, encoding="utf-8"))
        except Exception:
            continue
        if isinstance(d, dict) and "sequences" in d:
            d["_path"] = p
            d["_chapter"] = os.path.relpath(p, extracted).split(os.sep)[0]
            defs.append(d)
    return defs


def kind_of(ev):
    return next(iter(ev["data"]))


def body_of(ev):
    return ev["data"][kind_of(ev)] or {}


def instants(events):
    """Instant index per event. Two events share an index iff the runtime fires them in one
    `Advance` pass: the later one carries no positive START_TIME and the earlier one reports
    no run time."""
    out = []
    idx = 0
    for i, ev in enumerate(events):
        st = ev.get("start")
        if i > 0:
            gap = float(st["time"]) if st else 0.0
            prev = events[i - 1]
            pk = kind_of(prev)
            if pk in DURATION_KINDS:
                field = DURATION_KINDS[pk]
                run = 1.0 if field is None else (body_of(prev).get(field) or 0.0)
            else:
                run = 0.0
            if gap > 0.0 or run > 0.0:
                idx += 1
        out.append(idx)
    return out


def event_time(events, i):
    """Authored seconds from the sequence start to event i, as far as the offsets state it.
    ANIMATION_OFFSET/SEQUENCE_OFFSET are absolute, EVENT_OFFSET accumulates."""
    t = 0.0
    for j, ev in enumerate(events[: i + 1]):
        st = ev.get("start")
        if st:
            if st["offset"] in ("Animation", "Sequence"):
                t = float(st["time"])
            else:
                t += float(st["time"])
        if j < i:
            pk = kind_of(ev)
            if pk in DURATION_KINDS:
                field = DURATION_KINDS[pk]
                t += 1.0 if field is None else (body_of(ev).get(field) or 0.0)
    return t


# ------------------------------------------------------------------- what starts an emitter

def def_index(defs):
    by_name = collections.defaultdict(list)
    for d in defs:
        if d.get("anim_name"):
            by_name[d["anim_name"].lower()].append(d)
    return by_name


def starts_at_self(d):
    """Does this definition start a puffer at the node it is CALLED with (INPUT_NODE, the
    def root, or an unnamed AT_NODE — all of which resolve to the call's anchor)?"""
    for s in (d.get("sequences") or []):
        for ev in s["events"]:
            if kind_of(ev) == "PufferState":
                b = body_of(ev)
                if b.get("active_state") == 1 and b.get("at_node") in SELF_NODES:
                    return b.get("name")
    return None


def authored_run(d, puffer_name):
    """The latest authored moment at which this definition stops `puffer_name` itself —
    its own PUFFER_STATE 0, or a STOP_SEQUENCE halting the sequence that started it.
    None when the definition authors no stop of its own (the BL-224 population)."""
    best = None
    starting_seq = None
    for s in (d.get("sequences") or []):
        for ev in s["events"]:
            b = body_of(ev)
            if kind_of(ev) == "PufferState" and b.get("active_state") == 1 \
                    and (b.get("name") or "").lower() == (puffer_name or "").lower():
                starting_seq = s["name"]
    for s in (d.get("sequences") or []):
        evs = s["events"]
        for i, ev in enumerate(evs):
            k, b = kind_of(ev), body_of(ev)
            hit = (k == "PufferState" and b.get("active_state") == 0
                   and (b.get("name") or "").lower() == (puffer_name or "").lower())
            hit = hit or (k == "StopSequence" and starting_seq is not None
                          and (b.get("name") or "").lower() == starting_seq.lower())
            if hit:
                t = event_time(evs, i)
                best = t if best is None else max(best, t)
    return best


def emitter_starts_on(d, seq, lo, hi, node, by_name, depth=0):
    """Every emitter start on `node` among events (lo, hi) of `seq`, as
    (event index, emitter name, definition that owns the emitter). Follows a CALL_SEQUENCE
    into this definition's own sequences and a CALL_ANIMATION into the called definition."""
    out = []
    evs = seq["events"]
    for i in range(lo + 1, hi):
        ev = evs[i]
        k, b = kind_of(ev), body_of(ev)
        if k == "PufferState" and b.get("active_state") == 1 \
                and (b.get("at_node") or "").lower() == node.lower():
            out.append((i, b.get("name"), d))
        elif k == "CallAnimation":
            p = b.get("parameters")
            if isinstance(p, dict):
                key = next(iter(p))
                if (p[key].get("node") or "").lower() == node.lower():
                    for callee in by_name.get((b.get("name") or "").lower(), [])[:1]:
                        name = starts_at_self(callee)
                        if name:
                            out.append((i, name, callee))
        elif k == "CallSequence" and depth < 3:
            for inner in (d.get("sequences") or []):
                if (inner["name"] or "").lower() == (b.get("name") or "").lower():
                    for (_, name, owner) in emitter_starts_on(
                            d, inner, -1, len(inner["events"]), node, by_name, depth + 1):
                        out.append((i, name, owner))
    return out


# ------------------------------------------------------------------------- measurement 1-3

def idiom_census(defs, by_name):
    """Every (activate N … deactivate N) pair in one sequence with an emitter start on N
    between them."""
    pairs = []
    for d in defs:
        for s in (d.get("sequences") or []):
            evs = s["events"]
            inst = instants(evs)
            opens = {}
            for i, ev in enumerate(evs):
                if kind_of(ev) != "ObjectActiveState":
                    continue
                b = body_of(ev)
                node = b.get("node")
                if not node:
                    continue
                key = node.lower()
                if b.get("state"):
                    opens[key] = i
                elif key in opens:
                    lo = opens.pop(key)
                    for (si, name, owner) in emitter_starts_on(d, s, lo, i, node, by_name):
                        pairs.append({
                            "chapter": d["_chapter"],
                            "def": d.get("anim_name"),
                            "root": d.get("anim_root_name"),
                            "seq": s["name"],
                            "node": node,
                            "puffer": name,
                            "owner": owner.get("anim_name"),
                            "same_instant": inst[si] == inst[i],
                            "gap": round(event_time(evs, i) - event_time(evs, si), 3),
                            "authored_run": authored_run(owner, name),
                        })
    return pairs


def shape(p):
    """One authored shape, so the eight chapters' copies of the same def count once."""
    return (p["def"], p["seq"], p["node"], p["puffer"], p["owner"])


# --------------------------------------------------------------------------- measurement 4

def reader_defs(extracted):
    """(name, animation name, [sequences]) from the reader source. A reader node is a flat
    [TAG, payload, TAG, payload, …] list; sequences hold their events the same way."""
    out = []

    def walk(node, sink):
        if isinstance(node, list):
            i = 0
            while i < len(node):
                tag = node[i]
                if isinstance(tag, str) and i + 1 < len(node):
                    payload = node[i + 1]
                    if tag == "ANIMATION_DEFINITION":
                        sink.append(payload)
                    else:
                        walk(payload, sink)
                    i += 2
                else:
                    walk(tag, sink)
                    i += 1
        return sink

    for p in sorted(glob.glob(os.path.join(extracted, "**", "*.zrd.json"), recursive=True)):
        try:
            doc = json.load(open(p, encoding="utf-8"))
        except Exception:
            continue
        for payload in walk(doc, []):
            out.append((p, payload))
    return out


# Reader spellings of the duration-bearing kinds. Same conservative rule as DURATION_KINDS:
# a run time we do not read is treated as nonzero, so it CLOSES the instant.
READER_DURATION_TAGS = {
    "OBJECT_MOTION", "OBJECT_MOTION_FROM_TO", "OBJECT_OPACITY_FROM_TO",
    "OBJECT_MOTION_SI_SCRIPT", "OBJECT_MOTION_SI_SCRIPT_ALL_NAMES",
}


def reader_pairs(defs):
    """The same idiom over the reader corpus, under the SAME instant rule as the compiled pass:
    a node set ACTIVE and then INACTIVE with an emitter-bearing event between them and nothing
    in between that opens a later instant — no positive START_TIME, no duration-bearing event.
    (Without the duration half this over-reports badly: every `part1` destruct triple looks
    offset-less until you notice the OBJECT_MOTION flying the debris for 3.5–5 s.)"""
    hits = []
    for path, payload in defs:
        anim = None
        seqs = []
        for i in range(0, len(payload) - 1, 2):
            tag, val = payload[i], payload[i + 1]
            if tag == "ANIMATION_NAME" and isinstance(val, list) and val:
                anim = val[0]
            elif tag == "SEQUENCE_DEFINITION" and isinstance(val, list):
                seqs.append(val)
        for seq in seqs:
            events = []
            for i in range(0, len(seq) - 1, 2):
                tag, val = seq[i], seq[i + 1]
                if tag == "NAME":
                    continue
                events.append((tag, val))
            opens = {}
            for i, (tag, val) in enumerate(events):
                if tag != "OBJECT_ACTIVE_STATE" or not isinstance(val, list):
                    continue
                fields = {val[j]: val[j + 1] for j in range(0, len(val) - 1, 2)
                          if isinstance(val[j], str)}
                node = (fields.get("NAME") or [None])[0]
                state = (fields.get("STATE") or [None])[0]
                if not node:
                    continue
                if state == "ACTIVE":
                    opens[node] = i
                elif node in opens:
                    lo = opens.pop(node)
                    body = events[lo + 1:i]
                    opened_later = any(
                        t in READER_DURATION_TAGS
                        or (isinstance(v, list) and "START_TIME" in v)
                        for t, v in body)
                    if not opened_later and any(t in ("PUFFER_STATE", "CALL_ANIMATION")
                                                for t, _ in body):
                        hits.append((os.path.basename(path), anim, node,
                                     [t for t, _ in body]))
    return hits


# --------------------------------------------------------------------------------- report

def main():
    extracted = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("-") \
        else "extracted"
    show = "--list" in sys.argv

    defs = load_compiled(extracted)
    by_name = def_index(defs)
    print(f"compiled definitions: {len(defs)} over "
          f"{len({d['_chapter'] for d in defs})} chapters, "
          f"{len(by_name)} distinct animation names")

    pairs = idiom_census(defs, by_name)
    same = [p for p in pairs if p["same_instant"]]
    later = [p for p in pairs if not p["same_instant"]]
    print(f"\n1. THE IDIOM — activate/emit/deactivate pairs on one node in one sequence")
    print(f"   {len(pairs):5d} pairs  ({len({shape(p) for p in pairs})} distinct authored shapes)")
    print(f"   {len(same):5d} same instant as the emitter start  "
          f"({len({shape(p) for p in same})} shapes)   <- RULE 1 changes these")
    print(f"   {len(later):5d} a later instant                    "
          f"({len({shape(p) for p in later})} shapes)   <- BL-224's population, untouched")

    print(f"\n2. AUTHORED INTENT of the same-instant population")
    buckets = collections.Counter()
    for p in same:
        r = p["authored_run"]
        buckets["run > 0 (RULE 2 cuts it short)" if r and r > 0
                else "run == 0 (evidence FOR rule 2)" if r == 0
                else "no stop of its own (host is the only stop)"] += 1
    for k, n in buckets.most_common():
        print(f"   {n:5d}  {k}")
    seen = set()
    print("   distinct shapes:")
    for p in sorted(same, key=lambda q: (q["def"] or "", q["seq"] or "")):
        if shape(p) in seen:
            continue
        seen.add(shape(p))
        print(f"     {p['def']:<24} seq {p['seq'] or '(unnamed)':<20} node {p['node']:<12} "
              f"puffer {p['puffer']:<20} by {p['owner']:<14} "
              f"authored run {p['authored_run']}")

    print(f"\n3. INSTANT SEPARATION — authored seconds from start to deactivation")
    for label, group in (("same instant", same), ("later instant", later)):
        if group:
            gaps = sorted(p["gap"] for p in group)
            print(f"   {label:<14} n={len(group):4d}  min {gaps[0]:8.3f}  "
                  f"median {gaps[len(gaps) // 2]:8.3f}  max {gaps[-1]:8.3f}")
        else:
            print(f"   {label:<14} n=0")

    rd = reader_defs(extracted)
    rp = reader_pairs(rd)
    print(f"\n4. THE READER CORPUS — {len(rd)} authored definitions")
    print(f"   {len(rp)} offset-less activate/emit/deactivate sequences "
          f"({len({(a, n) for _, a, n, _ in rp})} distinct definition+node)")
    for f, a, n, body in sorted({(f, a, n, tuple(b)) for f, a, n, b in rp}):
        print(f"     {f:<28} {a or '(unnamed)':<22} node {n:<16} between: {', '.join(body)}")

    if show:
        print("\nALL PAIRS")
        for p in sorted(pairs, key=lambda q: (not q["same_instant"], q["def"] or "")):
            print(f"   {'SAME ' if p['same_instant'] else 'later'} {p['chapter']:<4} "
                  f"{p['def']:<24} {p['seq'] or '-':<18} {p['node']:<12} "
                  f"{p['puffer']:<20} gap {p['gap']}")


if __name__ == "__main__":
    main()
