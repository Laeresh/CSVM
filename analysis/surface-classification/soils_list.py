"""Recovers the level-supplied surface-name list -- registry slots 6 and up.

crimson.exe compiles in six surface names (default/water/seafloor/quicksand/lava/fire
at ids 0-5) and appends the rest at load time from a .zrd named by the `LoadSoils`
script command (FUN_0055b7b0, sole caller at 0x005ba4ae). The filename is not in the
executable, so the list has to come from the shipped data -- it lives in the global
ZBD/zrdr.zbd, anchored by the distinctive token `opensesame`, which crimson.exe itself
resolves through the registry (FUN_004735b0 at 0x0047372c).

The append rule (FUN_0055b7b0): each name is linear-scanned against the registry and
appended only if absent, so ids run 6, 7, 8, ... in file order. The match test is
_strnicmp over strlen(registryName) accepting a trailing NUL *or digit*, so `water2`
would collapse onto `water` -- none of the shipped names hit that case.

Read-only; absolute path because CrimsonSkiesGame/ is git-ignored and this must work
from a worktree.

    python analysis/surface-classification/soils_list.py
"""
import re
from pathlib import Path

ZRDR = Path(r"Z:\CSVM\CrimsonSkiesGame\ZBD\zrdr.zbd")
ANCHOR = b"opensesame"
BUILTINS = ["default", "water", "seafloor", "quicksand", "lava", "fire"]
TOKEN = re.compile(rb"[a-z][a-z0-9_]{2,31}")
# mech3ax labels the ids it does not have names for with MechWarrior 3 leftovers.
MECH3AX_LABEL = {0: "Default", 1: "Water", 5: "Fire", 8: "Grass", 11: "Mech", 12: "Silt", 13: "NoSlip"}


def main():
    blob = ZRDR.read_bytes()
    at = blob.find(ANCHOR)
    if at < 0:
        raise SystemExit(f"anchor {ANCHOR!r} not found in {ZRDR}")

    # The names sit in one contiguous run of short lowercase tokens around the anchor.
    # Walk outward while consecutive tokens stay tightly packed (<= 24 bytes apart).
    window = blob[at - 400 : at + 400]
    base = at - 400
    toks = [(m.start() + base, m.group().decode("ascii")) for m in TOKEN.finditer(window)]
    pivot = next(i for i, (_, t) in enumerate(toks) if t == ANCHOR.decode())

    lo = pivot
    while lo > 0 and toks[lo][0] - toks[lo - 1][0] <= 24:
        lo -= 1
    hi = pivot
    while hi + 1 < len(toks) and toks[hi + 1][0] - toks[hi][0] <= 24:
        hi += 1
    names = [t for _, t in toks[lo : hi + 1]]

    print(f"soils list in {ZRDR.name} @ {hex(toks[lo][0])}: {names}\n")

    registry = list(BUILTINS)
    for name in names:
        if not any(
            name[: len(r)].lower() == r and (len(name) == len(r) or name[len(r)].isdigit())
            for r in registry
        ):
            registry.append(name)

    print("surface type id -> name")
    for i, name in enumerate(registry):
        origin = "compiled into crimson.exe" if i < len(BUILTINS) else "from the soils .zrd"
        label = MECH3AX_LABEL.get(i)
        seen = f"   [mech3ax calls this {label!r}]" if label else ""
        print(f"  {i:2} = {name:12} ({origin}){seen}")

    print("\nshipped materials use ids 0, 1, 5, 8, 11, 12, 13 (soil_id_probe.py)")
    used = [0, 1, 5, 8, 11, 12, 13]
    for i in used:
        print(f"  {i:2} = {registry[i]}")


if __name__ == "__main__":
    main()
