#!/usr/bin/env python3
"""Census the per-mission AI vehicle tables (`aiv.zrd.json`) and locate the pilot skill vector.

Read-only. Run from the extraction root (the directory holding `C1/`, `C2/`, ... and `zrdr/`):

    cd extracted && python path/to/aiv_skill_slots.py

Embeds no game data — every number it prints is read from the player's own install at runtime.
See FINDINGS.md in this directory for the verdict.
"""

import collections
import glob
import json
import re
import sys

SKILL_LO, SKILL_HI = 22, 30  # inclusive candidate skill-vector slot range
RADIUS_SLOT = 31
FLAGS_SLOT = 32
TARGETS_SLOT = 33
NAMEKEY_SLOT = 20
LEADER_SLOT = 6
POS_SLOT = 1
HEADING_SLOT = 2


def mission_tag(path):
    parts = re.split(r"[\\/]", path)
    return "/".join(parts[:2])


def main():
    files = sorted(glob.glob("*/*/zrdr/aiv.zrd.json"))
    if not files:
        print("no aiv.zrd.json found — run from the extraction root", file=sys.stderr)
        return 1
    print(f"aiv.zrd.json files: {len(files)}")

    block_lengths = collections.Counter()
    slot_types = collections.defaultdict(collections.Counter)
    slot_values = collections.defaultdict(collections.Counter)
    target_lengths = collections.Counter()
    full_vectors = []
    blocks = 0

    for path in files:
        tag = mission_tag(path)
        doc = json.load(open(path))
        for entry in doc[1:]:
            if not (isinstance(entry, list) and len(entry) == 2):
                continue
            name, fields = entry
            if not isinstance(fields, list):
                continue
            blocks += 1
            block_lengths[len(fields)] += 1
            for i, v in enumerate(fields):
                slot_types[i][type(v).__name__] += 1
                if isinstance(v, (int, float)) and not isinstance(v, bool):
                    slot_values[i][v] += 1
            if len(fields) > TARGETS_SLOT and isinstance(fields[TARGETS_SLOT], list):
                target_lengths[len(fields[TARGETS_SLOT])] += 1
            if len(fields) > FLAGS_SLOT:
                vec = fields[SKILL_LO:SKILL_HI + 1]
                if all(isinstance(x, int) and 1 <= x <= 9 for x in vec):
                    full_vectors.append(
                        (tag, name, fields[NAMEKEY_SLOT], tuple(vec),
                         fields[RADIUS_SLOT], fields[FLAGS_SLOT])
                    )

    print(f"vehicle blocks: {blocks}")
    print(f"block field-count histogram: {dict(sorted(block_lengths.items()))}")
    print(f"target-list length histogram: {dict(sorted(target_lengths.items()))}")

    print(f"\ncandidate skill slots {SKILL_LO}..{SKILL_HI} — value histograms")
    for i in range(SKILL_LO, SKILL_HI + 1):
        print(f"  [{i}] {dict(sorted(slot_values[i].items(), key=lambda kv: kv[0]))}")
    print(f"  [{RADIUS_SLOT}] radius {dict(sorted(slot_values[RADIUS_SLOT].items()))}")
    print(f"  [{FLAGS_SLOT}] flags  {dict(sorted(slot_values[FLAGS_SLOT].items()))}")

    print(f"\nblocks carrying a COMPLETE 9-slot 1..9 vector: {len(full_vectors)}")
    for row in sorted(full_vectors):
        tag, name, key, vec, radius, flags = row
        print(f"  {tag:9s} {name:20s} {key:24s} {vec} radius={radius} flags={flags}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
