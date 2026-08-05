"""Census what a WAIT_FOR_COMPLETION call is actually asked to wait FOR.

`census.py` (2026-08-01) settled the FIELD: flag 0x10 plus an index into the caller's own
`anim_refs`, always naming the call's own callee. This instrument answers the question the
runtime needs before it can honour it — "completes" defined from the data rather than from
what makes one crash look right (D9 trap (d)):

  * Does the flagged callee TERMINATE at all? A definition whose sequences carry an infinite
    LOOP never finishes, so a caller that waits on one waits forever.
  * Is the flagged call the LAST event of its sequence? Then the wait is authored but inert —
    there is nothing after it to hold back.
  * How long would the hold be, in authored seconds?

Run from the repository root:

    python analysis/wait-for-completion/callee_shapes.py
    python analysis/wait-for-completion/callee_shapes.py --dump .scratch/wfc-shapes.json

The dump carries one row per flagged call and is temporary; the aggregate below and this
instrument are what belong in version control.
"""

import argparse
import collections
import json
from pathlib import Path

# Kinds whose payload `run_time` is a real authored duration the sequence clock waits on.
# ObjectMotionSiScript's duration lives in the .zan script pool, not in the def file, so it
# is counted separately rather than silently read as zero (DIAG-15).
SI_SCRIPT_KIND = "ObjectMotionSiScript"


def compiled_def_files(root: Path):
    """Every compiled anim-def JSON, tagged with the chapter its archive belongs to."""
    for path in sorted(root.rglob("*.json")):
        parts = path.relative_to(root).parts
        if len(parts) < 2:
            continue
        if parts[-2] not in ("cam_anim", "mis_anim"):
            continue
        yield parts[0], path


def load_def(path: Path):
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, dict):
        return None
    if not all(key in document for key in ("anim_name", "sequences")):
        return None
    return document


def event_kind(event):
    data = event.get("data")
    if isinstance(data, str):
        return data, {}
    if isinstance(data, dict) and len(data) == 1:
        kind, payload = next(iter(data.items()))
        return kind, payload if isinstance(payload, dict) else {}
    return None, {}


def sequences_of(document):
    return [s for s in (document.get("sequences") or []) if isinstance(s, dict)]


def called_sequence_names(events):
    """Sequence names this event list reaches by CALL_SEQUENCE / STOP_SEQUENCE.

    STOP_SEQUENCE counts because its decoded semantic is "halt every runner of x, or CALL it
    if none is running" (docs/formats/anim-definitions.md) — the stopper idiom, which is a
    start as often as it is a stop.
    """
    names = []
    for event in events:
        kind, payload = event_kind(event)
        if kind in ("CallSequence", "StopSequence"):
            name = payload.get("name")
            if isinstance(name, str):
                names.append(name)
    return names


def instance_closure(document):
    """The sequences an instance of this definition can be running.

    Start runs every non-OnCall ("Initial") sequence; those can call further ones by name.
    Mirrors AnimInstance: one instance, N concurrent runners, Finished when all are gone.
    """
    by_name = {}
    for seq in sequences_of(document):
        by_name.setdefault(seq.get("name") or "", seq)
    frontier = [s for s in sequences_of(document) if s.get("seq_state") != "OnCall"]
    seen = {id(s) for s in frontier}
    closure = list(frontier)
    while frontier:
        seq = frontier.pop()
        for name in called_sequence_names(seq.get("events") or []):
            target = by_name.get(name)
            if target is not None and id(target) not in seen:
                seen.add(id(target))
                closure.append(target)
                frontier.append(target)
    return closure


def sequence_span(events):
    """(authored seconds, has-SI-script, infinite-loop, finite-loop-count) for one sequence.

    The clock walk mirrors SequenceRunner.SetDue: an "Animation"/"Sequence" offset is absolute
    against the sequence start, anything else (including an absent start) is measured from the
    previous event's COMPLETION. A LOOP is scored by running the body once and multiplying,
    which is a bound, not a simulation — this census is about shape, not exact numbers.
    """
    base = 0.0
    span = 0.0
    si_script = False
    infinite = False
    loop_count = None
    for event in events:
        kind, payload = event_kind(event)
        start = event.get("start") or {}
        offset = start.get("offset") if isinstance(start, dict) else None
        delay = float(start.get("time") or 0.0) if isinstance(start, dict) else 0.0
        due = delay if offset in ("Animation", "Sequence") else base + delay
        duration = 0.0
        if kind == SI_SCRIPT_KIND:
            si_script = True
        value = payload.get("run_time")
        if isinstance(value, (int, float)):
            duration = float(value)
        if kind == "Loop":
            count = payload.get("Count")
            count = count if isinstance(count, int) else -1
            # An AUTHORED count of 0 means INFINITE, exactly as SequenceRunner normalises it.
            if count <= 0:
                infinite = True
            else:
                loop_count = count if loop_count is None else max(loop_count, count)
        base = due + duration
        span = max(span, base)
    if loop_count:
        span *= loop_count
    return span, si_script, infinite, loop_count


def classify(document):
    """How an instance of this definition ends, and roughly when."""
    closure = instance_closure(document)
    span = 0.0
    si_script = False
    infinite = False
    for seq in closure:
        seq_span, seq_si, seq_infinite, _ = sequence_span(seq.get("events") or [])
        span = max(span, seq_span)
        si_script = si_script or seq_si
        infinite = infinite or seq_infinite
    return {
        "sequences_in_closure": len(closure),
        "span_s": round(span, 4),
        "si_script": si_script,
        "infinite_loop": infinite,
    }


# ---- reader (zrdr) front-end -------------------------------------------------------------
#
# AnimDefs.LoadArchive executes these too, so the same question has to be asked of them. The
# reader body is an alternating [KEY, value-list, KEY, value-list, …] walk in which a BARE flag
# (WAIT_FOR_COMPLETION) is a key followed by null — exactly what AnimDefs.Pairs reads.

READER_NON_EVENT_KEYS = {"NAME", "ACTIVATION"}


def reader_pairs(body):
    """AnimDefs.Pairs, in Python: (key, value-or-None), duplicates preserved."""
    index = 0
    while index < len(body):
        key = body[index]
        if not isinstance(key, str):
            index += 1
            continue
        if index + 1 < len(body) and isinstance(body[index + 1], list):
            yield key, body[index + 1]
            index += 2
        else:
            yield key, None
            index += 2 if index + 1 < len(body) and body[index + 1] is None else 1


def reader_walk(value, keys):
    """Every value list reached by the alternating-key chain (AnimDefs.Walk)."""
    frontier = [value]
    if value and isinstance(value[0], list):
        frontier = [v for v in value if isinstance(v, list)]
    for key in keys:
        nxt = []
        for lst in frontier:
            for k, v in reader_pairs(lst):
                if k.upper() == key and isinstance(v, list):
                    nxt.append(v)
        frontier = nxt
    return frontier


def reader_defs(root: Path):
    for path in sorted(root.rglob("*.json")):
        if "zrdr" not in path.parts:
            continue
        text = path.read_text(encoding="utf-8-sig")
        if "ANIMATION_DEFINITION" not in text:
            continue
        document = json.loads(text)
        for body in reader_walk(document, ["ANIMATION_DEFINITIONS", "ANIMATION_LIST",
                                           "ANIMATION_DEFINITION"]):
            yield path, body


def reader_def_name(body):
    for key, value in reader_pairs(body):
        if key.upper() == "ANIMATION_NAME" and value:
            return value[0]
    for key, value in reader_pairs(body):
        if key.upper() == "NAME" and value:
            return value[0]
    return None


def reader_blocks(body):
    """(block label, event pairs) for every block AnimDefs turns into a sequence."""
    for key, value in reader_pairs(body):
        upper = key.upper()
        if upper in ("SEQUENCE_DEFINITION", "RESET_STATE", "DAMAGE_SEQUENCE") and value:
            events = [(k, v) for k, v in reader_pairs(value)
                      if k.upper() not in READER_NON_EVENT_KEYS]
            yield upper, events


def reader_infinite(body):
    """Does any block of this reader definition carry an infinite LOOP?"""
    for _, events in reader_blocks(body):
        for key, value in events:
            if key.upper() != "LOOP":
                continue
            count = value[0] if value else None
            if not isinstance(count, (int, float)) or count <= 0:
                return True
    return False


def reader_census(root: Path):
    defs = list(reader_defs(root))
    by_name = {}
    for _, body in defs:
        name = reader_def_name(body)
        if name:
            by_name.setdefault(name, []).append(body)
    rows = []
    call_sequence_tokens = 0
    for path, body in defs:
        for block, events in reader_blocks(body):
            for index, (key, value) in enumerate(events):
                upper = key.upper()
                if value is None:
                    continue
                flagged = any(isinstance(v, str) and v == "WAIT_FOR_COMPLETION" for v in value)
                if not flagged:
                    continue
                if upper == "CALL_SEQUENCE":
                    # Decoded on CALL_ANIMATION only — the compiled form carries the field on
                    # no other event kind (census.py: 56,750/56,750 CallAnimation). Counted,
                    # not implemented (DIAG-15).
                    call_sequence_tokens += 1
                    continue
                if upper != "CALL_ANIMATION":
                    continue
                callee = next((v[0] for k, v in reader_pairs(value)
                               if k.upper() == "NAME" and v), None)
                targets = by_name.get(callee) or []
                rows.append({
                    "file": path.as_posix(),
                    "caller": reader_def_name(body),
                    "block": block,
                    "is_last_event": index == len(events) - 1,
                    "callee": callee,
                    "callee_defs": len(targets),
                    "callee_infinite": any(reader_infinite(t) for t in targets),
                })
    return rows, call_sequence_tokens, len(defs)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path("extracted"))
    parser.add_argument("--dump", type=Path)
    args = parser.parse_args()

    by_chapter_anim = collections.defaultdict(list)
    documents = []
    for chapter, path in compiled_def_files(args.root):
        document = load_def(path)
        if document is None:
            continue
        documents.append((chapter, path, document))
        by_chapter_anim[(chapter, document.get("anim_name"))].append((path, document))

    if not documents:
        raise SystemExit("No compiled anim definitions found; extraction input was not exercised")

    rows = []
    for chapter, path, document in documents:
        blocks = [("reset_state", document.get("reset_state"))]
        blocks += [(s.get("name") or "", s) for s in sequences_of(document)]
        for block_name, block in blocks:
            if not isinstance(block, dict):
                continue
            events = block.get("events") or []
            for index, event in enumerate(events):
                kind, payload = event_kind(event)
                if kind != "CallAnimation":
                    continue
                if payload.get("wait_for_completion") is None:
                    continue
                callee = payload.get("name")
                candidates = by_chapter_anim.get((chapter, callee)) or []
                shapes = [classify(doc) for _, doc in candidates]
                rows.append({
                    "chapter": chapter,
                    "caller_file": path.as_posix(),
                    "caller_def": document.get("name"),
                    "caller_anim": document.get("anim_name"),
                    "caller_activation": document.get("activation"),
                    "block": block_name,
                    "is_last_event": index == len(events) - 1,
                    "trailing_events": len(events) - 1 - index,
                    "callee": callee,
                    "callee_defs_in_chapter": len(candidates),
                    "callee_infinite": any(s["infinite_loop"] for s in shapes),
                    "callee_si_script": any(s["si_script"] for s in shapes),
                    "callee_span_s": max([s["span_s"] for s in shapes], default=None),
                })

    resolved = [r for r in rows if r["callee_defs_in_chapter"] > 0]
    infinite = [r for r in resolved if r["callee_infinite"]]
    terminating = [r for r in resolved if not r["callee_infinite"]]
    inert = [r for r in rows if r["is_last_event"]]
    effective = [r for r in terminating if not r["is_last_event"]]

    spans = sorted(r["callee_span_s"] for r in effective if r["callee_span_s"] is not None)

    def pct(values, fraction):
        if not values:
            return None
        return round(values[min(len(values) - 1, int(len(values) * fraction))], 4)

    report = {
        "compiled_definitions": len(documents),
        "flagged_calls": len(rows),
        "flagged_calls_unresolved_callee": len(rows) - len(resolved),
        "flagged_calls_last_event_in_block": len(inert),
        "callee_terminates": len(terminating),
        "callee_never_terminates": len(infinite),
        "effective_holds": len(effective),
        "distinct_callees": len({r["callee"] for r in rows}),
        "distinct_callees_never_terminating": len({r["callee"] for r in infinite}),
        "distinct_effective_pairs": len({(r["caller_anim"], r["callee"]) for r in effective}),
        "by_caller_activation": dict(
            sorted(collections.Counter(r["caller_activation"] for r in rows).items())),
        "effective_by_block_is_reset_state": sum(
            1 for r in effective if r["block"] == "reset_state"),
        "effective_span_s": {
            "min": spans[0] if spans else None,
            "p50": pct(spans, 0.5),
            "p90": pct(spans, 0.9),
            "max": spans[-1] if spans else None,
            "zero": sum(1 for s in spans if s == 0.0),
            "si_script_unknown": sum(1 for r in effective if r["callee_si_script"]),
        },
        "top_effective_pairs": collections.Counter(
            f"{r['caller_anim']} -> {r['callee']}" for r in effective).most_common(15),
        "never_terminating_callees": sorted({r["callee"] for r in infinite}),
        # The cross-tab is the whole design question. If the never-terminating callees were
        # ALSO the ones with events behind them, honouring the wait would wedge those
        # sequences permanently; if they are all last-event, a runner-lifetime hold is the
        # only thing that could wedge, and a next-event hold cannot.
        "crosstab_lastevent_x_infinite": {
            f"last_event={last} infinite={inf}": sum(
                1 for r in resolved if r["is_last_event"] == last and r["callee_infinite"] == inf)
            for last in (True, False) for inf in (True, False)
        },
        "effective_by_callee": dict(sorted(
            collections.Counter(r["callee"] for r in effective).items())),
        "effective_spans_by_callee": dict(sorted(
            {r["callee"]: r["callee_span_s"] for r in effective}.items())),
    }

    reader_rows, reader_call_sequence, reader_def_count = reader_census(args.root)
    report["reader"] = {
        "definitions": reader_def_count,
        "flagged_call_animation": len(reader_rows),
        "flagged_call_sequence_counted_not_implemented": reader_call_sequence,
        "last_event_in_block": sum(1 for r in reader_rows if r["is_last_event"]),
        "callee_unresolved": sum(1 for r in reader_rows if r["callee_defs"] == 0),
        "crosstab_lastevent_x_infinite": {
            f"last_event={last} infinite={inf}": sum(
                1 for r in reader_rows
                if r["callee_defs"] and r["is_last_event"] == last and r["callee_infinite"] == inf)
            for last in (True, False) for inf in (True, False)
        },
        "effective_pairs": sorted({
            f"{r['caller']} -> {r['callee']}" for r in reader_rows
            if not r["is_last_event"] and r["callee_defs"] and not r["callee_infinite"]}),
    }

    print(json.dumps(report, indent=2))

    if args.dump:
        args.dump.parent.mkdir(parents=True, exist_ok=True)
        args.dump.write_text(json.dumps({"report": report, "rows": rows}, indent=2) + "\n",
                             encoding="utf-8")
        print(f"wrote {args.dump.as_posix()}")


if __name__ == "__main__":
    main()
