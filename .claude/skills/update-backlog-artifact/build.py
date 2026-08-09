# Regenerate the backlog Artifact page from backlog.md.
#
#   python build.py <output.html>
#
# Parses every top-level `- \`BL-NNN\` ...` bullet under each `## ` theme heading
# (excluding "## Standing notes") into [theme, id, type, status, title] rows, and
# injects them as JSON in place of the template's __BACKLOG_DATA__ placeholder.
#
# All file I/O is explicit UTF-8; edit this file with the Read/Edit/Write tools
# rather than a PowerShell round-trip (see CLAUDE.md).

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SOURCE = os.path.join(REPO, "backlog.md")
TEMPLATE = os.path.join(HERE, "template.html")

# An optional {SCOPE} marker may sit between the tags and the bold title.
BULLET = re.compile(r"^- `(BL-\d+)` (.*)$")
TAGS = re.compile(r"^((?:`\[[^`]*?\]` ?)+)")
TAG = re.compile(r"`\[([^`]*?)\]`")
SCOPE = re.compile(r"^\{[^}]*\}\s*")
TITLE = re.compile(r"\*\*(.+?)\*\*")


def parse(src):
    """Return (rows, themes). rows are [theme, id, type, status|None, title]."""
    lines = src.split("\n")
    rows, themes, theme = [], [], None
    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        if line.startswith("## "):
            heading = line[3:].strip()
            theme = None if heading == "Standing notes" else heading
            if theme and theme not in themes:
                themes.append(theme)
            i += 1
            continue
        m = BULLET.match(line)
        if not (m and theme):
            i += 1
            continue
        # The bullet runs until the next top-level bullet or theme heading.
        block = [m.group(2)]
        j = i + 1
        while j < n and not (lines[j].startswith("- `BL-") or lines[j].startswith("## ")):
            block.append(lines[j])
            j += 1
        text = re.sub(r"\s+", " ", " ".join(block)).strip()

        tagstr = TAGS.match(text)
        tagstr = tagstr.group(1) if tagstr else ""
        taglist = TAG.findall(tagstr)
        typ = taglist[0] if taglist else None
        status = taglist[1] if len(taglist) > 1 else None
        rest = SCOPE.sub("", text[len(tagstr):])
        tm = TITLE.match(rest)
        title = tm.group(1).strip() if tm else None
        if typ is None or title is None:
            sys.stderr.write("PARSE FAIL %s: %s\n" % (m.group(1), text[:120]))
        rows.append([theme, m.group(1), typ, status, title])
        i = j
    return rows, themes


def main():
    if len(sys.argv) != 2:
        sys.stderr.write("usage: build.py <output.html>\n")
        return 2
    out_path = sys.argv[1]

    src = io.open(SOURCE, encoding="utf-8").read()
    rows, themes = parse(src)

    # Sanity check: every raw bullet in the file must have produced a row.
    raw = len(re.findall(r"^- `BL-\d+`", src, re.M))
    if raw != len(rows):
        sys.stderr.write("MISMATCH: %d raw bullets, %d rows parsed\n" % (raw, len(rows)))
        return 1
    ids = [r[1] for r in rows]
    dupes = sorted(set(x for x in ids if ids.count(x) > 1))
    if dupes:
        sys.stderr.write("DUPLICATE IDS: %s\n" % ", ".join(dupes))
        return 1
    if any(r[2] is None or r[4] is None for r in rows):
        return 1

    tpl = io.open(TEMPLATE, encoding="utf-8").read()
    if "__BACKLOG_DATA__" not in tpl:
        sys.stderr.write("template.html has no __BACKLOG_DATA__ placeholder\n")
        return 1
    tpl = tpl.replace("__BACKLOG_DATA__", json.dumps(rows, ensure_ascii=False))
    io.open(out_path, "w", encoding="utf-8").write(tpl)

    sys.stderr.write("%d items across %d themes -> %s\n" % (len(rows), len(themes), out_path))
    sys.stderr.write("themes: %s\n" % " | ".join(themes))
    return 0


if __name__ == "__main__":
    sys.exit(main())
