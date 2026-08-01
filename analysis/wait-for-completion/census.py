"""Census CALL_ANIMATION's semantic wait flag separately from its raw slot."""

import argparse
import collections
import json
from pathlib import Path


def event_records(value, trail=()):
    """Yield (event, JSON trail) pairs from every nested event-shaped object."""
    if isinstance(value, dict):
        data = value.get("data")
        if isinstance(data, dict) and len(data) == 1:
            yield value, trail
        for key, child in value.items():
            yield from event_records(child, trail + (str(key),))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from event_records(child, trail + (str(index),))


def ref_name(ref):
    if not isinstance(ref, dict) or len(ref) != 1:
        return None
    body = next(iter(ref.values()))
    return body.get("name") if isinstance(body, dict) else None


def reader_wait_ops(value):
    """Yield reader operation names whose body carries the bare wait token."""
    if isinstance(value, list):
        for index, child in enumerate(value):
            if (
                isinstance(child, str)
                and index + 1 < len(value)
                and isinstance(value[index + 1], list)
                and "WAIT_FOR_COMPLETION" in value[index + 1]
            ):
                yield child
            yield from reader_wait_ops(child)
    elif isinstance(value, dict):
        for child in value.values():
            yield from reader_wait_ops(child)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path("extracted"))
    parser.add_argument("--dump", type=Path)
    args = parser.parse_args()

    json_files_read = 0
    top_level_object_files = 0
    compiled_definition_files = 0
    call_count = 0
    semantic = collections.Counter()
    raw = collections.Counter()
    field_kinds = collections.Counter()
    reader_ops = collections.Counter()
    flagged_rows = []
    nonzero_rows = []

    for path in sorted(args.root.rglob("*.json")):
        document = json.loads(path.read_text(encoding="utf-8-sig"))
        json_files_read += 1
        reader_ops.update(reader_wait_ops(document))
        if not isinstance(document, dict):
            continue
        top_level_object_files += 1
        if not all(key in document for key in ("anim_name", "anim_refs", "sequences")):
            continue
        compiled_definition_files += 1
        refs = document.get("anim_refs") or []
        if not isinstance(refs, list):
            raise ValueError(f"{path}: anim_refs is not an array")

        for event, trail in event_records(document):
            kind, payload = next(iter(event["data"].items()))
            if isinstance(payload, dict) and "wait_for_completion" in payload:
                field_kinds[kind] += 1
            if kind != "CallAnimation":
                continue
            if not isinstance(payload, dict):
                raise ValueError(f"{path}: CallAnimation payload is not an object")
            if "wait_for_completion" not in payload or "wait_for_raw" not in payload:
                raise ValueError(f"{path}: CallAnimation is missing a wait field")

            call_count += 1
            semantic[payload["wait_for_completion"]] += 1
            raw[payload["wait_for_raw"]] += 1
            value = payload["wait_for_completion"]
            if value is None:
                continue
            if not isinstance(value, int):
                raise ValueError(f"{path}: non-integral wait_for_completion {value!r}")
            row = {
                "file": path.as_posix(),
                "definition": document.get("name"),
                "animation": document.get("anim_name"),
                "activation": document.get("activation"),
                "event_path": "/".join(trail),
                "callee": payload.get("name"),
                "wait_for_completion": value,
                "wait_for_raw": payload["wait_for_raw"],
                "anim_refs": refs,
                "anim_ref_count": len(refs),
                "zero_based_index_valid": 0 <= value < len(refs),
                "indexed_ref": ref_name(refs[value]) if 0 <= value < len(refs) else None,
            }
            row["indexed_ref_matches_callee"] = row["indexed_ref"] == row["callee"]
            flagged_rows.append(row)
            if value != 0:
                nonzero_rows.append(row)

    if call_count == 0:
        raise SystemExit("No CallAnimation events found; extraction input was not exercised")

    report = {
        "json_files_read": json_files_read,
        "top_level_object_files": top_level_object_files,
        "compiled_definition_files": compiled_definition_files,
        "call_animation_events": call_count,
        "wait_for_completion_by_event_kind": dict(sorted(field_kinds.items())),
        "reader_wait_for_completion_by_op": dict(sorted(reader_ops.items())),
        "wait_for_completion_distribution": {
            "null" if key is None else str(key): count
            for key, count in sorted(semantic.items(), key=lambda item: str(item[0]))
        },
        "wait_for_raw_distribution": {
            "null" if key is None else str(key): count
            for key, count in sorted(raw.items(), key=lambda item: str(item[0]))
        },
        "flagged_events": len(flagged_rows),
        "flagged_index_valid": sum(row["zero_based_index_valid"] for row in flagged_rows),
        "flagged_index_invalid": sum(not row["zero_based_index_valid"] for row in flagged_rows),
        "flagged_indexed_ref_matches_callee": sum(
            row["indexed_ref_matches_callee"] for row in flagged_rows
        ),
        "flagged_activation_distribution": dict(
            sorted(collections.Counter(row["activation"] for row in flagged_rows).items())
        ),
        "nonzero_flagged_events": len(nonzero_rows),
        "nonzero_index_valid": sum(row["zero_based_index_valid"] for row in nonzero_rows),
        "nonzero_indexed_ref_matches_callee": sum(
            row["indexed_ref_matches_callee"] for row in nonzero_rows
        ),
        "rows": nonzero_rows,
    }

    print(json.dumps({key: value for key, value in report.items() if key != "rows"}, indent=2))
    for row in nonzero_rows:
        valid = "valid" if row["zero_based_index_valid"] else "INVALID"
        match = "same" if row["indexed_ref_matches_callee"] else "DIFFERENT"
        print(
            f"{row['wait_for_completion']:>2} {valid:7} {match:9} "
            f"refs={row['anim_ref_count']:<2} {row['file']} :: "
            f"{row['definition']}/{row['animation']} -> {row['callee']} "
            f"(ref {row['indexed_ref']})"
        )

    if args.dump:
        args.dump.parent.mkdir(parents=True, exist_ok=True)
        args.dump.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print(f"wrote {args.dump.as_posix()}")


if __name__ == "__main__":
    main()
