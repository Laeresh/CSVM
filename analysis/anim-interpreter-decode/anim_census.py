"""One-off census over the compiled anim defs, for PLAN-anim-original-match.

Reads <data root>/extracted/<chapter>/{cam_anim,mis_anim,...}/*.json (git-ignored install data)
and counts the shapes the plan's items depend on. Read-only.
"""
import json
import os
import sys
from collections import Counter, defaultdict

# extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree that does
# (the same env var the engine reads), defaulting to this checkout.
DATA_ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOT = os.path.join(DATA_ROOT, "extracted")

kind_counts = Counter()
loop_forms = Counter()
nested_if = []            # (file, seq) with an If inside an open If
reentrant_call = []       # (file, seq_target) called from >1 site in the same def
call_to_initial = []      # CALL_SEQUENCE naming a non-OnCall sequence
stop_to_parked = []       # STOP_SEQUENCE naming an OnCall sequence never called elsewhere
files = 0


def ev_kind(ev):
    d = ev.get("data") or {}
    return next(iter(d), "?")


def ev_body(ev):
    d = ev.get("data") or {}
    return next(iter(d.values()), {}) or {}


for chapter in sorted(os.listdir(ROOT)):
    cdir = os.path.join(ROOT, chapter)
    if not os.path.isdir(cdir):
        continue
    for sub in sorted(os.listdir(cdir)):
        sdir = os.path.join(cdir, sub)
        if not os.path.isdir(sdir) or "anim" not in sub:
            continue
        for name in os.listdir(sdir):
            if not name.endswith(".json") or name in ("manifest.json",):
                continue
            path = os.path.join(sdir, name)
            try:
                with open(path, encoding="utf-8") as fh:
                    doc = json.load(fh)
            except Exception:
                continue
            seqs = doc.get("sequences")
            if not isinstance(seqs, list):
                continue
            files += 1

            states = {}
            for seq in seqs:
                states[seq.get("name", "")] = seq.get("seq_state")

            call_sites = defaultdict(int)
            stop_sites = defaultdict(int)

            for seq in seqs:
                depth = 0
                for ev in seq.get("events") or []:
                    k = ev_kind(ev)
                    kind_counts[k] += 1
                    body = ev_body(ev)
                    if k == "If":
                        if depth > 0:
                            nested_if.append((path, seq.get("name", "")))
                        depth += 1
                    elif k == "Endif":
                        depth = max(0, depth - 1)
                    elif k == "Loop":
                        loop_forms[next(iter(body), "?") if isinstance(body, dict) else "?"] += 1
                    elif k == "CallSequence":
                        call_sites[body.get("name")] += 1
                    elif k == "StopSequence":
                        stop_sites[body.get("name")] += 1

            for tgt, n in call_sites.items():
                if n > 1:
                    reentrant_call.append((path, tgt, n))
                if tgt in states and states[tgt] != "OnCall":
                    call_to_initial.append((path, tgt, states[tgt]))
            for tgt in stop_sites:
                if states.get(tgt) == "OnCall" and call_sites.get(tgt, 0) == 0:
                    stop_to_parked.append((path, tgt))

out = sys.stdout
print(f"defs scanned: {files}\n")
print("== event kinds ==")
for k, n in kind_counts.most_common():
    print(f"{n:8d}  {k}")
print("\n== Loop payload forms ==")
for k, n in loop_forms.most_common():
    print(f"{n:8d}  {k}")
print(f"\n== nested If (If inside an open If): {len(nested_if)} ==")
for p, s in nested_if[:20]:
    print(f"  {p}  seq={s!r}")
print(f"\n== CALL_SEQUENCE targets called from >1 site in the same def: {len(reentrant_call)} ==")
for p, t, n in reentrant_call[:20]:
    print(f"  {p}  -> {t!r} x{n}")
print(f"\n== CALL_SEQUENCE naming a non-OnCall sequence: {len(call_to_initial)} ==")
for p, t, st in call_to_initial[:20]:
    print(f"  {p}  -> {t!r} state={st}")
print(f"\n== STOP_SEQUENCE on an OnCall sequence nothing calls: {len(stop_to_parked)} ==")
for p, t in stop_to_parked[:20]:
    print(f"  {p}  -> {t!r}")
