"""Minimal zrdr reader, mirroring CSVM/src/Mech3/Zrdr.cs ZrdrDict semantics.

A reader file extracts to nested JSON lists. The dominant shape is a flat list
alternating "KEY", [values...]; a string followed by another string (or by the list
end) is a bare flag. Duplicate keys can be meaningful, so pairs() keeps source order
and asdict() collapses (last wins) only where that is safe.

Reads extracted/** at runtime; embeds no game data.
Override the extraction root with CS_EXTRACTED.
"""
import json
import os

_HERE = os.path.dirname(os.path.abspath(__file__))


def _find_extracted():
    """Nearest `extracted/` at or above this script. Walking up rather than assuming
    ../../extracted matters because a git worktree has no extraction of its own —
    it sits under the main tree's .claude/worktrees/, so the root is further up."""
    d = _HERE
    while True:
        cand = os.path.join(d, "extracted")
        if os.path.isdir(cand):
            return cand
        parent = os.path.dirname(d)
        if parent == d:
            raise SystemExit(
                "no `extracted/` directory found above this script — run ExtractAssets.ps1, "
                "or set CS_EXTRACTED to the extraction root")
        d = parent


EXTRACTED = os.environ.get("CS_EXTRACTED") or _find_extracted()


def path(*parts):
    return os.path.join(EXTRACTED, *parts)


def load(*parts):
    with open(path(*parts), "r", encoding="utf-8") as f:
        return json.load(f)


def unwrap(root):
    """Several readers extract as [[...]] — outer lists wrapping the alternating list."""
    while (isinstance(root, list) and len(root) == 1 and isinstance(root[0], list)
           and any(isinstance(x, str) for x in root[0])):
        root = root[0]
    return root


def instances(root):
    """For readers whose root is a LIST OF records (zeppelins.json, egen.json) rather than
    one alternating list. Descends through wrapper lists — a wrapper holds no bare strings,
    a record does — and returns the record list. Empty files extract as [null].

    ⚠ unwrap() alone is wrong here: it stops at the wrapper (which contains no strings), so
    the caller ends up walking the wrapper as if it were one record and reads zero keys —
    a silent count of N records with no fields, not an error."""
    while (isinstance(root, list) and len(root) == 1 and isinstance(root[0], list)
           and not any(isinstance(x, str) for x in root[0])):
        root = root[0]
    if isinstance(root, list) and any(isinstance(x, str) for x in root):
        return [root]                       # a bare single record
    return [r for r in root if isinstance(r, list) and r]


def pairs(lst):
    """[(key, value_list_or_None), ...] in source order; None value = bare flag."""
    out = []
    i = 0
    while i < len(lst):
        k = lst[i]
        if isinstance(k, str):
            if i + 1 < len(lst) and isinstance(lst[i + 1], list):
                out.append((k, lst[i + 1]))
                i += 2
            else:
                out.append((k, None))
                i += 1
        else:
            i += 1
    return out


def asdict(lst):
    return {k: v for k, v in pairs(lst)}


def vehicle_defs():
    """vehicle.json root alternates defName, [properties...]. Returns {name: proplist}."""
    return dict(pairs(unwrap(load("zrdr", "vehicle.zrd.json"))))


def resolve_chain(defs, name):
    """The kind_of chain nearest-first, starting at `name`."""
    chain, cur, seen = [], name, set()
    while cur in defs and cur not in seen:
        seen.add(cur)
        chain.append(cur)
        ko = asdict(defs[cur]).get("kind_of")
        cur = ko[0] if ko and isinstance(ko[0], str) else None
    return chain


def resolve(defs, name, key):
    """(sourceDefName, value) for `key` resolved nearest-first through kind_of."""
    for c in resolve_chain(defs, name):
        d = asdict(defs[c])
        if key in d:
            return c, d[key]
    return None, None


def messages():
    """messages.json key -> value."""
    return {e["key"]: e["value"] for e in load("messages.json")["entries"]}


CHAPTERS = ("C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5")
