"""Proves that mech3ax's per-material `soil` label IS the engine's surface type id.

The engine picks a crash choreography by indexing a vector with the struck material's
dword at record offset 0x20 (crimson.exe FUN_0048b920, 0x0048bac5-0x0048bb00). This
script shows that dword is exactly what mech3ax extracts as `soil`, by locating the
material block in the raw .gamez and cross-tabulating.

Method: the material record is 0x2c bytes, bulk-fread with no per-field parse
(FUN_00559e20), so the block can be located from its own contents -- find the longest
run of consecutive `Textured` materials in the extracted JSON and search the raw bytes
for their `texture_index` values as dwords at +0x10, stride 0x2c. Exactly one candidate
offset survives per chapter. Then read the dword at +0x20 of every record and tabulate
it against the JSON's `soil` label.

Absolute paths on purpose: `extracted/` and `CrimsonSkiesGame/` are both git-ignored, so
a relative path breaks when this is run from a worktree. Read-only; writes nothing.

    python analysis/surface-classification/soil_id_probe.py [CHAPTER ...]
"""
import json
import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path

INSTALL = Path(r"Z:\CSVM\CrimsonSkiesGame\ZBD")
EXTRACTED = Path(r"Z:\CSVM\extracted")
STRIDE = 0x2C
SOIL_OFFSET = 0x20
TEXTURE_OFFSET = 0x10
CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def load_json_materials(chapter):
    path = EXTRACTED / chapter / "gamez" / "materials.json"
    out = []
    for entry in json.loads(path.read_text(encoding="utf-8")):
        kind, body = next(iter(entry.items()))
        out.append((kind, body.get("soil"), body.get("texture_index")))
    return out


def longest_textured_run(mats):
    """(start index, length) of the longest run of consecutive Textured materials."""
    best = (0, 0)
    i = 0
    while i < len(mats):
        if mats[i][0] == "Textured" and mats[i][2] is not None:
            j = i
            while j < len(mats) and mats[j][0] == "Textured" and mats[j][2] is not None:
                j += 1
            if j - i > best[1]:
                best = (i, j - i)
            i = j
        else:
            i += 1
    return best


def find_block(blob, mats):
    """Offsets of material record 0, by matching a run of texture_index dwords."""
    run_start, run_len = longest_textured_run(mats)
    if run_len < 8:
        return [], run_start
    run = [mats[k][2] for k in range(run_start, min(run_start + 12, len(mats)))]

    hits = []
    needle = struct.pack("<I", run[0])
    pos = 0
    while True:
        i = blob.find(needle, pos)
        if i < 0:
            break
        pos = i + 1
        first_rec = i - TEXTURE_OFFSET
        rec0 = first_rec - run_start * STRIDE
        if rec0 < 0:
            continue
        if all(
            first_rec + k * STRIDE + TEXTURE_OFFSET + 4 <= len(blob)
            and struct.unpack_from("<I", blob, first_rec + k * STRIDE + TEXTURE_OFFSET)[0] == ti
            for k, ti in enumerate(run)
        ):
            hits.append(rec0)
    return hits, run_start


def probe(chapter):
    gamez = INSTALL / chapter / "gamez.zbd"
    if not gamez.exists():
        print(f"{chapter}: no gamez.zbd at {gamez}")
        return {}
    blob = gamez.read_bytes()
    mats = load_json_materials(chapter)
    hits, run_start = find_block(blob, mats)
    print(f"\n=== {chapter}: {len(mats)} materials, anchored on the run at #{run_start} ===")
    if len(hits) != 1:
        print(f"  {len(hits)} candidate block offsets {[hex(h) for h in hits]} -- inconclusive")
        return {}
    base = hits[0]
    if base + len(mats) * STRIDE > len(blob):
        print(f"  block at {hex(base)} would run past EOF -- rejected")
        return {}

    table = defaultdict(Counter)
    for i, (_, soil, _) in enumerate(mats):
        value = struct.unpack_from("<i", blob, base + i * STRIDE + SOIL_OFFSET)[0]
        table[soil][value] += 1
    print(f"  material block at {hex(base)}")
    mapping = {}
    for soil in sorted(table):
        counts = dict(table[soil])
        print(f"    soil={soil!r:10} -> {counts}")
        if len(counts) == 1:
            mapping[soil] = next(iter(counts))
    return mapping


def main():
    chapters = sys.argv[1:] or CHAPTERS
    seen = {}
    conflicts = []
    for chapter in chapters:
        for soil, value in probe(chapter).items():
            if seen.setdefault(soil, value) != value:
                conflicts.append((chapter, soil, value, seen[soil]))
    print("\n=== label -> surface type id, across every chapter probed ===")
    for soil, value in sorted(seen.items(), key=lambda kv: kv[1]):
        print(f"  {soil:10} = {value}")
    print("  (0/1/5 are compiled into crimson.exe as default/water/fire;")
    print("   2/3/4 are seafloor/quicksand/lava and no shipped material uses them;")
    print("   >=6 are level-supplied slots -- mech3ax's labels for those are")
    print("   MechWarrior 3 leftovers and do NOT name the Crimson Skies surface.)")
    if conflicts:
        print("\n  !! label collided across chapters:")
        for chapter, soil, value, first in conflicts:
            print(f"    {chapter}: {soil} = {value}, previously {first}")
    else:
        print("\n  No label took two different ids in any chapter.")


if __name__ == "__main__":
    main()
