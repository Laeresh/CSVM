"""Census of START_TIME origins over the compiled anim defs.

Sizes the reachable blast radius of resolving origin `Animation` against the animation
instance's clock instead of the sequence's own: an `Animation` offset only diverges when
the sequence did not start with the instance, i.e. when its `seq_state` is `OnCall`.

Reads <data root>/extracted/<chapter>/{cam_anim,mis_anim}/*.json (git-ignored install data).
Read-only.

    python analysis\\anim-interpreter-decode\\start_origin_census.py

Note: a null `start` encodes as `Animation + 0.0`. Only events with an explicit `start`
object are counted here, and the headline counts require a non-zero `time` — a zero time
gates identically on either clock.
"""
import json
import os
from collections import Counter

# extracted/ is git-ignored, so a worktree has none: CSVM_DATA_ROOT names the tree that does
# (the same env var the engine reads), defaulting to this checkout.
DATA_ROOT = os.environ.get("CSVM_DATA_ROOT") or os.path.dirname(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROOT = os.path.join(DATA_ROOT, "extracted")

files = 0
null_start = 0
origin_counts = Counter()             # explicit start -> offset
origin_nonzero = Counter()            # explicit start, time != 0 -> offset
anim_nonzero_by_state = Counter()     # Animation + non-zero time -> seq_state
hits = []                             # (path, seq, seq_state, event index, time, kind)


def ev_kind(ev):
    d = ev.get("data") or {}
    return next(iter(d), "?")


for chapter in sorted(os.listdir(ROOT)):
    cdir = os.path.join(ROOT, chapter)
    if not os.path.isdir(cdir):
        continue
    for sub in sorted(os.listdir(cdir)):
        sdir = os.path.join(cdir, sub)
        if not os.path.isdir(sdir) or "anim" not in sub:
            continue
        for name in sorted(os.listdir(sdir)):
            if not name.endswith(".json") or name == "manifest.json":
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

            for seq in seqs:
                state = seq.get("seq_state")
                for i, ev in enumerate(seq.get("events") or []):
                    start = ev.get("start")
                    if not start:
                        null_start += 1
                        continue
                    offset = start.get("offset")
                    time = start.get("time") or 0.0
                    origin_counts[offset] += 1
                    if time == 0.0:
                        continue
                    origin_nonzero[offset] += 1
                    if offset == "Animation":
                        anim_nonzero_by_state[state] += 1
                        hits.append((path, seq.get("name", ""), state, i, time, ev_kind(ev)))


print(f"defs scanned: {files}")
print(f"events with a null start (encoded Animation + 0.0): {null_start}\n")

print("== explicit start, by origin ==")
for k, n in origin_counts.most_common():
    print(f"{n:8d}  {k}")

print("\n== explicit start with a non-zero time, by origin ==")
for k, n in origin_nonzero.most_common():
    print(f"{n:8d}  {k}")

total = sum(anim_nonzero_by_state.values())
print(f"\n== Animation origin with a non-zero time: {total} ==")
for k, n in anim_nonzero_by_state.most_common():
    print(f"{n:8d}  seq_state={k}")

oncall = [h for h in hits if h[2] == "OnCall"]
print(f"\n== the reachable blast radius (Animation, non-zero time, OnCall sequence): {len(oncall)} ==")
for p, s, st, i, t, k in oncall[:60]:
    print(f"  {p}  seq={s!r} ev#{i} t={t} {k}")
