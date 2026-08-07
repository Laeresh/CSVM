"""Does the sidecar cache's premise hold? Count how often a clip is REVISITED.

A decode cache only pays on the SECOND visit to a clip. If the workflow analysed
each clip once and closed the item, `BL-308` would be worth little however good
the tool is. This counts, per clip on disk, how many DISTINCT sessions mention
it across every transcript in the project.

⚠ This counts MENTIONS, not re-decodes. A clip named in `FINDINGS.md` or
`backlog.md` is counted whenever that file is read into context, so the absolute
numbers overstate re-analysis. Read it as a direction, not a rate.

Usage, from the repo root:

    python analysis/session-cost/revisit.py
"""
import os
import subprocess
import sys

DIR = os.environ.get(
    "CSVM_TRANSCRIPTS",
    os.path.expanduser(os.path.join("~", ".claude", "projects", "Z--CSVM")))


def repo_root():
    d = subprocess.run(["git", "rev-parse", "--git-common-dir"],
                       capture_output=True, text=True, check=True).stdout.strip()
    return os.path.dirname(os.path.abspath(d))


def clips(vid):
    out = []
    for dp, _, fns in os.walk(vid):
        for fn in fns:
            if fn.lower().endswith(".mp4"):
                out.append(fn[:-4])
    return out


def main():
    vid = os.path.join(repo_root(), "OriginalScreenshots", "Videos")
    if not os.path.isdir(vid):
        raise SystemExit(f"no footage at {vid} (git-ignored; absent in worktrees)")
    names = clips(vid)
    # Plain lowercased substring search. 94 names x 237 transcripts of compiled
    # regex is far too slow; `in` on a pre-lowered string is a C-level scan.
    low = {n: n.lower() for n in names}
    sessions = {n: set() for n in names}
    files = [f for f in os.listdir(DIR) if f.endswith(".jsonl")]
    for i, fn in enumerate(files):
        try:
            with open(os.path.join(DIR, fn), encoding="utf-8",
                      errors="ignore") as f:
                text = f.read().lower()
        except OSError:
            continue
        for n, needle in low.items():
            if needle in text:
                sessions[n].add(fn)
        if (i + 1) % 50 == 0:
            print(f"  ...{i + 1}/{len(files)} transcripts", file=sys.stderr)

    counts = sorted(((len(v), k) for k, v in sessions.items()), reverse=True)
    seen = [(c, k) for c, k in counts if c > 0]
    multi = [(c, k) for c, k in seen if c > 1]
    print(f"transcripts scanned:      {len(files)}")
    print(f"clips on disk:            {len(names)}")
    print(f"clips ever mentioned:     {len(seen)}")
    print(f"clips in >1 session:      {len(multi)}  "
          f"({100 * len(multi) / max(len(seen), 1):.0f}% of those ever used)")
    print(f"clips in >2 sessions:     {len([1 for c, _ in seen if c > 2])}")
    print("\ntop revisited:")
    for c, k in seen[:15]:
        print(f"  {c:3d} sessions  {k}")


if __name__ == "__main__":
    main()
