"""Census over the 24 shipped campaign missions: roster size, generator count,
zeppelin count. Used by PLAN-campaign-coop D33 to choose "the heaviest
shipped campaign mission" by data rather than impression.

Reads extracted/<chapter>/<mission>/zrdr/{aiv,egen,zeppelins}.zrd.json under
CSVM_DATA_ROOT (defaults to the repo root two levels up from this file).
"""
import json
import os
import sys

ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "..")
)
EXTRACTED = os.path.join(ROOT, "extracted")

# CM -> (folder, act, act_mission) from docs/formats/campaign-missions.md
MISSIONS = [
    ("CM01", "C3/M01"), ("CM02", "C3/M05"), ("CM03", "C3/M02"), ("CM04", "C3/M03"),
    ("CM05", "C3/M04"), ("CM06", "C1C/M01"), ("CM07", "C1/M02"), ("CM08", "C1B/M03"),
    ("CM09", "C1/M04"), ("CM10", "C1/M05"), ("CM11", "C2/M02"), ("CM12", "C2/M01"),
    ("CM13", "C2/M03"), ("CM14", "C2B/M04"), ("CM15", "C2/M05"), ("CM16", "C4/M01"),
    ("CM17", "C4/M02"), ("CM18", "C4/M03"), ("CM19", "C4/M04"), ("CM20", "C4/M05"),
    ("CM21", "C5/M01"), ("CM22", "C5/M02"), ("CM23", "C5/M03"), ("CM24", "C5/M04"),
]


def load(path):
    if not os.path.isfile(path):
        return None
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def roster_count(aiv):
    """Enabled (slot 5 == 1) non-player blocks -- the at-mission-start roster."""
    if aiv is None:
        return None
    body = aiv[1:]
    count = 0
    for name, fields in body:
        if name == "player":
            continue
        enabled = fields[5] if len(fields) > 5 else None
        if enabled == 1:
            count += 1
    return count


def _entry_count(root):
    """egen/zeppelins root: `[null]` for none, else one list entry per generator/zeppelin
    (each entry itself a flat key/value list starting "node")."""
    if root is None:
        return 0
    if not isinstance(root, list):
        return 0
    if len(root) == 1 and root[0] is None:
        return 0
    return len(root)


def generator_count(egen):
    return _entry_count(egen)


def zeppelin_count(zep):
    return _entry_count(zep)


def main():
    rows = []
    for cm, folder in MISSIONS:
        chapter, mission = folder.split("/")
        base = os.path.join(EXTRACTED, chapter, mission, "zrdr")
        aiv = load(os.path.join(base, "aiv.zrd.json"))
        egen = load(os.path.join(base, "egen.zrd.json"))
        zep = load(os.path.join(base, "zeppelins.zrd.json"))
        rows.append({
            "cm": cm,
            "folder": folder,
            "roster": roster_count(aiv),
            "generators": generator_count(egen),
            "zeppelins": zeppelin_count(zep),
        })

    print(f"{'CM':6}{'folder':10}{'roster':8}{'generators':12}{'zeppelins':10}")
    for r in rows:
        print(f"{r['cm']:6}{r['folder']:10}{str(r['roster']):8}{str(r['generators']):12}{str(r['zeppelins']):10}")

    scored = [r for r in rows if r["roster"] is not None]
    scored.sort(key=lambda r: (r["roster"] + r["generators"] + r["zeppelins"]), reverse=True)
    print("\nTop 5 by roster + generators + zeppelins:")
    for r in scored[:5]:
        total = r["roster"] + r["generators"] + r["zeppelins"]
        print(f"  {r['cm']} ({r['folder']}): roster={r['roster']} generators={r['generators']} zeppelins={r['zeppelins']} total={total}")


if __name__ == "__main__":
    sys.exit(main())
