"""Score a Claude Code session transcript for what a capture task actually cost.

Built for `BL-308`: the sidecar cache is only worth keeping if it makes a CAP
session measurably cheaper, and "cheaper" has to mean the same thing on both
sides of the comparison. This reads the raw `.jsonl` and counts identically
either way:

  images    image blocks arriving in tool results (contact sheets, stills).
            The reported dominant cost of a capture session.
  out_tok   output tokens -- what the model generated.
  in_tok    non-cached input tokens.
  cache_r   cache reads. Usually the largest number by far, and the one that
            shows the real shape of the cost: context accumulates and is
            re-sent every turn, so it grows with turn count, not with work.
  tools     tool calls, and the assistant turns they were spread over.

⚠ Session totals are NOT task totals. A transcript covers whatever else that
session happened to do -- the CAP-14 baseline below includes 46 Writes and 16
Edits of unrelated build work. Compare like with like, or compare two sessions
given the SAME prompt (see FINDINGS.md's A/B protocol).

Usage, from the repo root:

    python analysis/session-cost/bench.py --scan "CAP-14"   # find candidates
    python analysis/session-cost/bench.py <session-id>       # score one
    python analysis/session-cost/bench.py <id-a> <id-b>      # score and diff
"""
import json
import os
import sys

DIR = os.environ.get(
    "CSVM_TRANSCRIPTS",
    os.path.expanduser(os.path.join("~", ".claude", "projects", "Z--CSVM")))

FIELDS = ("images", "out_tok", "in_tok", "cache_r", "tools", "turns",
          "tool_result_chars")


def rows(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    yield json.loads(line)
                except ValueError:
                    continue        # a truncated tail is normal on a live session


def blocks(msg):
    c = msg.get("content")
    return c if isinstance(c, list) else []


def score(path):
    s = {k: 0 for k in FIELDS}
    used = {}
    for r in rows(path):
        m = r.get("message") or {}
        if r.get("type") == "assistant":
            s["turns"] += 1
            u = m.get("usage") or {}
            s["out_tok"] += u.get("output_tokens", 0)
            s["in_tok"] += u.get("input_tokens", 0)
            s["cache_r"] += u.get("cache_read_input_tokens", 0)
            for b in blocks(m):
                if b.get("type") == "tool_use":
                    s["tools"] += 1
                    used[b.get("name")] = used.get(b.get("name"), 0) + 1
        for b in blocks(m):
            if b.get("type") == "tool_result":
                cc = b.get("content")
                if isinstance(cc, str):
                    s["tool_result_chars"] += len(cc)
                for sub in (cc if isinstance(cc, list) else []):
                    if sub.get("type") == "image":
                        s["images"] += 1
                    elif sub.get("type") == "text":
                        s["tool_result_chars"] += len(sub.get("text", ""))
    s["top_tools"] = sorted(used.items(), key=lambda kv: -kv[1])[:6]
    return s


def resolve(a):
    return a if os.path.exists(a) else os.path.join(DIR, a + ".jsonl")


def scan(term):
    hits = []
    for fn in sorted(os.listdir(DIR)):
        if not fn.endswith(".jsonl"):
            continue
        p = os.path.join(DIR, fn)
        try:
            with open(p, encoding="utf-8", errors="ignore") as f:
                n = sum(line.count(term) for line in f)
        except OSError:
            continue
        if n:
            hits.append((n, os.path.getsize(p), fn))
    for n, sz, fn in sorted(hits, reverse=True)[:12]:
        print(f"{n:6d} mentions  {sz / 1e6:7.1f} MB  {fn}")


def fmt(s, label):
    print(f"\n=== {label}")
    print(f"  images read        {s['images']:8,d}")
    print(f"  output tokens      {s['out_tok']:8,d}")
    print(f"  input tokens (new) {s['in_tok']:8,d}")
    print(f"  cache reads        {s['cache_r']:14,d}")
    print(f"  tool calls         {s['tools']:8,d}   over {s['turns']} assistant turns")
    print(f"  tool-result text   {s['tool_result_chars']:14,d} chars "
          f"(~{s['tool_result_chars'] // 4:,d} tok)")
    print(f"  top tools          {s['top_tools']}")


def diff(a, b, la, lb):
    print(f"\n=== {la} -> {lb}")
    for k in FIELDS:
        x, y = a[k], b[k]
        pct = f"{100 * (y - x) / x:+.0f}%" if x else "  n/a"
        print(f"  {k:18s} {x:14,d} -> {y:14,d}   {pct}")


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        raise SystemExit(__doc__)
    if args[0] == "--scan":
        scan(args[1])
    elif len(args) == 2:
        pa, pb = resolve(args[0]), resolve(args[1])
        sa, sb = score(pa), score(pb)
        fmt(sa, os.path.basename(pa))
        fmt(sb, os.path.basename(pb))
        diff(sa, sb, os.path.basename(pa), os.path.basename(pb))
    else:
        p = resolve(args[0])
        fmt(score(p), os.path.basename(p))
