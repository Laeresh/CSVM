"""Check gunshell/muzzle_burst node shape (model_index, children) across all 8 chapters.

BL-140: weapon-effects.md claimed both names "confirmed in C1" as meshless prototype
roots alongside genuinely-meshed roots. This verifies the claim holds (or doesn't) in
every chapter's nodes.json, not just C1.
"""

import json
from pathlib import Path

CHAPTERS = ["C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5"]
NAMES = ["gunshell", "muzzle_burst"]

root = Path(__file__).resolve().parents[2]

for name in NAMES:
    print(f"== {name} ==")
    for chapter in CHAPTERS:
        gamez = root / "extracted" / chapter / "gamez"
        with open(gamez / "nodes.json", encoding="utf-8") as f:
            nodes = json.load(f)
        matches = [n for n in nodes if n["name"] == name]
        if not matches:
            print(f"  {chapter}: NOT FOUND")
            continue
        for n in matches:
            children = n.get("child_indices", [])
            child_desc = []
            for ci in children:
                child = nodes[ci]
                mesh_note = "meshless" if child["model_index"] == -1 else f"model_index={child['model_index']}"
                child_desc.append(f"{child['name']}({mesh_note})")
            print(
                f"  {chapter}: root model_index={n['model_index']} "
                f"children={len(children)} -> {', '.join(child_desc) if child_desc else '(none)'}"
            )
    print()
