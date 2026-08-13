# Print the candidate magnitude inputs for the player 40-cal slug gun (wep_40)
# and its base-gun cousin wep_01, straight from extracted weapons.zrd.json.
import json
import os

# extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree that does
# (the same env var the engine reads), defaulting to this checkout.
DATA_ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PATH = os.path.join(DATA_ROOT, "extracted", "zrdr", "weapons.zrd.json")


def blocks(node, out):
    # zrdr readers: alternating name, properties lists
    if isinstance(node, list):
        for i in range(len(node) - 1):
            if isinstance(node[i], str) and node[i].startswith("wep_") and isinstance(node[i + 1], list):
                out[node[i]] = node[i + 1]
            blocks(node[i], out)
        if node:
            blocks(node[-1], out)
    return out


def props(block):
    d = {}
    for i in range(len(block) - 1):
        if isinstance(block[i], str) and isinstance(block[i + 1], list):
            d.setdefault(block[i], block[i + 1])
    return d


def main():
    data = json.load(open(PATH))
    weps = blocks(data, {})
    print("entries:", len(weps))
    for name in ("wep_40", "wep_01", "wep_30", "wep_50"):
        if name not in weps:
            print(name, "MISSING")
            continue
        p = props(weps[name])
        keys = ["NAME", "CALIBER", "DAMAGE", "VELOCITY", "MUZZLE_VELOCITY", "SPEED",
                "FIRE_RATE", "RELOAD_TIME", "AMMO", "MASS", "RANGE",
                "ARMOR_DAMAGE", "HEALTH_DAMAGE", "CANNON_SPREAD"]
        present = {k: p[k] for k in keys if k in p}
        print(name, present)
        others = sorted(set(p) - set(keys))
        print("  other keys:", others)


if __name__ == "__main__":
    main()
