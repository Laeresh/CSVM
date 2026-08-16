"""Census every AI vehicle def's `weapons` block, with the 5-tuple's decoded field names.

Reads extracted/zrdr/vehicle.zrd.json (the player's own extracted install; nothing is written).
Field order is decoded in docs/org/aiPilot/aiWeapons.md:
    [weapon_id, rounds_carried, refire_interval_seconds, min_range_metres, max_range_metres]

    python analysis/ai-ordnance-census/census.py [path-to-vehicle.zrd.json]
"""

import json
import sys

DEFAULT = r"extracted/zrdr/vehicle.zrd.json"


def keys(block):
    """A reader block is a flat [key, [value...], key, [value...]] list."""
    return {block[i]: block[i + 1] for i in range(0, len(block) - 1, 2) if isinstance(block[i], str)}


def main(path):
    top = json.load(open(path, encoding="utf-8"))[0]
    defs = [(top[i], top[i + 1]) for i in range(0, len(top), 2)]

    print(f"{'def':18s} {'kind_of':14s} weapons: [id, ammo, refire_s, min_m, max_m]")
    for name, block in defs:
        fields = keys(block)
        # player_airplane's block is the buyable catalogue, not a loadout.
        if "weapons" not in fields or name == "player_airplane":
            continue
        parent = fields["kind_of"][0] if "kind_of" in fields else "-"
        rows = " | ".join(
            f"{w[0]} n={w[1]:g} t={w[2]:g}s {w[3]:g}-{w[4]:g}m" for w in fields["weapons"]
        )
        print(f"{name:18s} {parent:14s} {rows}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT)
