#!/usr/bin/env python3
"""Survey `group` (aiv roster slot 4) across every per-mission AI vehicle table.

Read-only. Run from the extraction root (the directory holding `C1/`, `C2/`, ... and `zrdr/`):

    cd extracted && python path/to/aiv_group_slot.py

Embeds no game data; every number it prints is read from the player's own install at runtime.
See FINDINGS.md in this directory for the verdict.

Question (plan item B7): is slot 4 a formation/wing grouping? Measured signals per shared
group value within a mission: shared patrol net (slot 0), spawn proximity (slot 1), shared
team (slot 3), shared primary_target (slot 6), and whether a named ace (complete 22..30
skill vector) anchors a group of mooks.
"""

import collections
import glob
import json
import math
import re
import sys

NET_SLOT = 0
POS_SLOT = 1
TEAM_SLOT = 3
GROUP_SLOT = 4
ENABLED_SLOT = 5
TARGET_SLOT = 6
TITLE_SLOT = 20
DEACTIVATED_SLOT = 21
SKILL_LO, SKILL_HI = 22, 30


def mission_tag(path):
    parts = re.split(r"[\\/]", path)
    return "/".join(parts[:2])


def is_ace(fields):
    if len(fields) <= SKILL_HI:
        return False
    vec = fields[SKILL_LO:SKILL_HI + 1]
    return all(isinstance(x, int) and 1 <= x <= 9 for x in vec)


def spread(positions):
    best = 0.0
    for i in range(len(positions)):
        for j in range(i + 1, len(positions)):
            a, b = positions[i], positions[j]
            d = math.dist(a, b)
            best = max(best, d)
    return best


def main():
    files = sorted(glob.glob("*/*/zrdr/aiv.zrd.json"))
    if not files:
        print("no aiv.zrd.json found; run from the extraction root", file=sys.stderr)
        return 1
    print(f"aiv.zrd.json files: {len(files)}")

    value_hist = collections.Counter()
    type_hist = collections.Counter()
    blocks = 0
    per_mission = {}  # tag -> group value -> list of block summaries

    for path in files:
        tag = mission_tag(path)
        doc = json.load(open(path))
        mission_groups = collections.defaultdict(list)
        for entry in doc[1:]:
            if not (isinstance(entry, list) and len(entry) == 2):
                continue
            name, fields = entry
            if not isinstance(fields, list) or len(fields) <= TARGET_SLOT:
                continue
            blocks += 1
            g = fields[GROUP_SLOT]
            type_hist[type(g).__name__] += 1
            value_hist[g] += 1
            pos = fields[POS_SLOT] if isinstance(fields[POS_SLOT], list) else None
            title = fields[TITLE_SLOT] if len(fields) > TITLE_SLOT else ""
            deact = fields[DEACTIVATED_SLOT] if len(fields) > DEACTIVATED_SLOT else None
            mission_groups[g].append({
                "name": name,
                "net": fields[NET_SLOT],
                "pos": pos,
                "team": fields[TEAM_SLOT],
                "target": fields[TARGET_SLOT],
                "title": title,
                "ace": is_ace(fields),
                "enabled": fields[ENABLED_SLOT],
                "deactivated": deact,
            })
        per_mission[tag] = mission_groups

    print(f"vehicle blocks examined: {blocks}")
    print(f"slot {GROUP_SLOT} type histogram: {dict(type_hist)}")
    print(f"slot {GROUP_SLOT} value histogram: "
          f"{dict(sorted(value_hist.items(), key=lambda kv: (str(type(kv[0])), kv[0])))}")

    # Per-mission distribution: how many distinct group values, sizes of shared groups.
    shared_groups = 0
    singleton_groups = 0
    group_size_hist = collections.Counter()
    print("\nper-mission group census (missions with any non-default group value):")
    for tag in sorted(per_mission):
        groups = per_mission[tag]
        sizes = {g: len(v) for g, v in groups.items()}
        nondefault = {g: n for g, n in sizes.items() if g not in (-1, 0, None)}
        for g, n in sizes.items():
            group_size_hist[n] += 1
            if n > 1:
                shared_groups += 1
            else:
                singleton_groups += 1
        if nondefault:
            print(f"  {tag:9s} distinct={len(sizes)} nondefault={sorted(nondefault.items())}")
    print(f"\ngroup-size histogram (per mission per value): {dict(sorted(group_size_hist.items()))}")
    print(f"shared groups (>=2 members): {shared_groups}, singletons: {singleton_groups}")

    # Correlation pass over every shared group value within a mission.
    print("\nshared-group correlation table (mission, group, n, nets, teams, targets, "
          "spawn spread m, aces/titles):")
    agree = collections.Counter()
    total_shared = 0
    for tag in sorted(per_mission):
        for g, members in sorted(per_mission[tag].items(), key=lambda kv: str(kv[0])):
            if len(members) < 2:
                continue
            total_shared += 1
            nets = sorted({m["net"] for m in members}, key=str)
            teams = sorted({m["team"] for m in members}, key=str)
            targets = sorted({m["target"] for m in members}, key=str)
            positions = [m["pos"] for m in members if m["pos"]]
            sp = spread(positions) if len(positions) >= 2 else -1.0
            aces = [m["title"] or m["name"] for m in members if m["ace"]]
            if len(nets) == 1:
                agree["same_net"] += 1
            if len(teams) == 1:
                agree["same_team"] += 1
            if len(targets) == 1:
                agree["same_target"] += 1
            if 0 <= sp <= 500:
                agree["spawn_within_500m"] += 1
            if aces and len(aces) < len(members):
                agree["ace_plus_mooks"] += 1
            print(f"  {tag:9s} g={g!r:6} n={len(members):2d} nets={nets} teams={teams} "
                  f"targets={targets} spread={sp:8.1f} aces={aces}")
    print(f"\nagreement over {total_shared} shared groups: {dict(agree)}")

    # Activation cross-tab: the binary's wave/script logic reactivates deactivated
    # members of a group, so group value vs slot 21 (deactivated) is the check.
    cross = collections.Counter()
    for tag in per_mission:
        for g, members in per_mission[tag].items():
            for m in members:
                cross[(g, m["deactivated"], m["enabled"])] += 1
    print("\n(group, deactivated, enabled) -> blocks:")
    for key in sorted(cross, key=str):
        print(f"  {key}: {cross[key]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
