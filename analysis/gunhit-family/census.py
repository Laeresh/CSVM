"""Census of the 12 gun-impact `gunhit` anim defs: which puffer each ammo family emits, and
whether the def ever turns it off again.

Run from the repo root:  python analysis/gunhit-family/census.py
Reads extracted/<CHAPTER>/cam_anim/*gunhit*.json (git-ignored extraction output). Read-only.
"""

import glob
import json
import os
import sys

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]


def events(defn):
    for seq in defn.get("sequences") or []:
        for ev in seq.get("events") or []:
            kind = next(iter(ev["data"]))
            yield kind, ev["data"][kind], ev.get("start")


def report(chapter):
    paths = sorted(glob.glob(f"extracted/{chapter}/cam_anim/*gunhit*.json"))
    if not paths:
        return False
    print(f"=== {chapter}: {len(paths)} gunhit def(s)")
    for path in paths:
        defn = json.load(open(path, encoding="utf8"))
        starts, stops, motions = [], [], []
        for kind, val, start in events(defn):
            if kind == "PufferState":
                at = f"+{start['time']}" if start else "+0"
                (starts if (val["active_state"] or 0) >= 1 else stops).append(
                    f"{val['name']}{at}")
            elif kind == "ObjectMotion":
                motions.append(f"{val['node']}/{val.get('run_time')}s")
        print(f"  {defn['anim_name']:20} root={defn['anim_root_name']:12} "
              f"on=[{', '.join(starts)}] off=[{', '.join(stops) or 'NONE'}]")
        if motions:
            print(f"  {'':20} motion=[{', '.join(motions)}]")
    return True


def main():
    if not os.path.isdir("extracted"):
        sys.exit("no extracted/ — run ExtractAssets.ps1 first")
    for chapter in CHAPTERS:
        report(chapter)


if __name__ == "__main__":
    main()
