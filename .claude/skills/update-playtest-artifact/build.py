# Regenerate the playtest Artifact page from playtest.md.
#
#   python build.py <output.html>
#
# Parses section 0 into CAP_DATA (themed capture tables) and section 1 into
# PT_DATA (flight profiles with their PT items), and injects both as JSON in
# place of the template's __CAP_DATA__ / __PT_DATA__ placeholders. The plane
# code->name map is read live from the file's "Plane model names" line.
#
# This file is UTF-8 and contains the separators playtest.md itself uses; all
# file I/O is explicit UTF-8. Edit it with the Read/Edit/Write tools only - a
# PowerShell Get-Content/Set-Content round-trip would mojibake those literals
# (see CLAUDE.md).

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SOURCE = os.path.join(REPO, "playtest.md")
TEMPLATE = os.path.join(HERE, "template.html")

MIDDOT = u" · "     # "C1 . Bloodhawk" heading separator
EMDASH = u" — "     # "... - situation" heading separator
SEC_CAP = u"## 0 · Owed captures"
SEC_PT = u"## 1 · Actionable now"
SEC_END = u"## Everything else"
AMMO_NOTE = (u"infinite ammo — enable in-game if the menu offers it, "
             u"otherwise skip")

MARK = re.compile(r"\*(Look for|Blocks|Variations):\*")
LABELLED = re.compile(r"^\s*\*[^*]+:\*")
STATE_OF = {"Look for": "look", "Blocks": "blocks", "Variations": "variations"}


def collapse(s):
    return re.sub(r"\s+", " ", s).strip()


def plane_names(src):
    """Parse the 'Plane model names' line - the source of truth for --plane=."""
    m = re.search(r"\*\*Plane model names\*\*[^:]*:(.+?)\n\n", src, re.S)
    names = {}
    for code, name in re.findall(r"`(player_\w+)`\s+([^·\n]+)", m.group(1)):
        names[code] = name.strip().rstrip(".").strip()
    return names


def parse_captures(lines):
    """Section 0 -> [{theme, detailLabel, rows:[{id, capture, detail, unblocks}]}]."""
    themes, cur = [], None
    for line in lines:
        if line.startswith("### "):
            cur = {"theme": line[4:].strip(), "detailLabel": None, "rows": []}
            themes.append(cur)
            continue
        if cur is None or not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if cells[0] == "ID":
            cur["detailLabel"] = cells[-2]
            continue
        if set("".join(cells)) <= set("-: "):
            continue
        m = re.match(r"^`(CAP-\d+)`$", cells[0])
        if not m:
            sys.stderr.write("CAP ROW SKIPPED: %s\n" % line[:80])
            continue
        cur["rows"].append({"id": m.group(1), "capture": cells[1],
                            "detail": cells[2], "unblocks": cells[3]})
    # Empty themes are "nothing owed right now" markers in the file only.
    return [t for t in themes if t["rows"]]


def segment(raw):
    """Split an item's lines into ordered ('prose'|'sub', text) segments.

    A sub-bullet's continuation lines are indented past its own '- ' marker; a
    blank line, or a line at or left of that marker, ends it.
    """
    segs, mode, sub_indent = [], None, 0
    for line in raw:
        m = re.match(r"^(\s*)- (.*)$", line)
        if m and m.group(1):
            segs.append(["sub", m.group(2)])
            mode, sub_indent = "sub", len(m.group(1))
            continue
        if not line.strip():
            mode = None
            continue
        indent = len(line) - len(line.lstrip())
        if mode == "sub" and indent > sub_indent:
            segs[-1][1] += " " + line.strip()
        elif mode == "prose":
            segs[-1][1] += " " + line.strip()
        else:
            segs.append(["prose", line.strip()])
            mode = "prose"
    return segs


def parse_item(raw):
    """One '- `PT-nn` ...' bullet (plus its sub-bullets) -> an item dict."""
    raw = list(raw)
    raw[0] = raw[0][2:]  # drop the "- " marker
    head, look_for, notes, blocks, variations = "", [], [], "", ""
    state = "head"

    def add(text, state):
        """Route a chunk of text to whichever field is currently open."""
        nonlocal head, blocks, variations, state_out
        state_out = state
        if not text.strip():
            return
        if state == "head":
            head += " " + text
        elif state == "look":
            # A labelled paragraph after *Look for:* is a note, not a check.
            if LABELLED.match(text):
                state_out = "notes"
                notes.append(collapse(text))
            else:
                look_for.append(collapse(text))
        elif state == "notes":
            notes.append(collapse(text))
        elif state == "blocks":
            blocks += " " + text
        elif state == "variations":
            variations += " " + text

    state_out = state
    for kind, text in segment(raw):
        if kind == "sub":
            add(text, state)
            state = state_out
            continue
        pos = 0
        for m in MARK.finditer(text):
            add(text[pos:m.start()], state)
            state = STATE_OF[m.group(1)]
            pos = m.end()
        add(text[pos:], state)
        state = state_out

    head = collapse(head)
    hm = re.match(r"^`(PT-\d+)`\s*`\[(.+?)\]`\s*(.*)$", head)
    if not hm:
        sys.stderr.write("ITEM HEAD UNPARSED: %s\n" % head[:120])
        return None
    pid, tagtext, after = hm.groups()
    if tagtext.startswith("A/B:"):
        tag = {"type": "A/B", "ref": tagtext[4:].strip()}
    elif tagtext == "Own":
        tag = {"type": "Own"}
    else:
        sys.stderr.write("UNKNOWN TAG on %s: %s\n" % (pid, tagtext))
        tag = {"type": tagtext}
    tm = re.match(r"\*\*(.+?)\*\*", after)
    if not tm:
        sys.stderr.write("NO BOLD TITLE on %s: %s\n" % (pid, after[:100]))
        return None
    return {"id": pid, "tag": tag, "title": collapse(tm.group(1)),
            "context": collapse(after[tm.end():]), "lookFor": look_for,
            "notes": collapse(" ".join(notes)), "blocks": collapse(blocks),
            "variations": collapse(variations)}


def menu_for(command, planes):
    """Translate the launch command into menu steps a non-dev tester can follow."""
    flags = re.findall(r"(--[\w-]+(?:=[^\s]+)?)", command)
    menu = {"chapter": None, "plane": None, "notes": []}
    for f in flags:
        if f.startswith("--plane="):
            menu["plane"] = planes.get(f.split("=", 1)[1])
        elif f.startswith("--chapter="):
            menu["chapter"] = f.split("=", 1)[1]
        elif f == "--vs":
            pass
        elif f.startswith("--players="):
            menu["notes"].append("%s players" % f.split("=", 1)[1])
        elif f == "--infinite-ammo":
            menu["notes"].append(AMMO_NOTE)
        else:
            # Unknown flag: show it literally rather than inventing a phrase.
            menu["notes"].append(f)
    if "--vs" in flags:
        menu["notes"].insert(0, "Mode: Dogfight (splitscreen)")
    return menu


def parse_profiles(block, planes):
    """Section 1 -> [{heading, chapter, planeOrPilots, situation, command, menu, items}]."""
    starts = [i for i, l in enumerate(block) if l.startswith("### ")]
    profiles = []
    for k, s in enumerate(starts):
        e = starts[k + 1] if k + 1 < len(starts) else len(block)
        body = block[s:e]
        heading = body[0][4:].strip()
        # A profile whose flight is not chapter-bound may omit the leading "<Chapter> · " part;
        # the chapter is then taken from the command's --chapter= flag (or left empty).
        if MIDDOT in heading:
            chapter, rest = heading.split(MIDDOT, 1)
        else:
            chapter, rest = "", heading
        if EMDASH in rest:
            plane_or_pilots, situation = rest.split(EMDASH, 1)
        else:
            plane_or_pilots, situation = rest, ""
        ci = next(i for i, l in enumerate(body) if l.strip().startswith("```powershell"))
        command = body[ci + 1].strip()

        item_starts = [i for i, l in enumerate(body) if re.match(r"^- `PT-\d+`", l)]
        items = []
        for j, si in enumerate(item_starts):
            ei = item_starts[j + 1] if j + 1 < len(item_starts) else len(body)
            item = parse_item(body[si:ei])
            if item:
                items.append(item)

        profiles.append({"heading": heading, "chapter": chapter.strip(),
                         "planeOrPilots": plane_or_pilots.strip(),
                         "situation": situation.strip(), "command": command,
                         "menu": menu_for(command, planes), "items": items})
    return profiles


def main():
    if len(sys.argv) != 2:
        sys.stderr.write("usage: build.py <output.html>\n")
        return 2
    out_path = sys.argv[1]

    src = io.open(SOURCE, encoding="utf-8").read()
    lines = src.split("\n")
    i_cap = next(i for i, l in enumerate(lines) if l.startswith(SEC_CAP))
    i_pt = next(i for i, l in enumerate(lines) if l.startswith(SEC_PT))
    i_end = next(i for i, l in enumerate(lines) if l.startswith(SEC_END))

    planes = plane_names(src)
    caps = parse_captures(lines[i_cap:i_pt])
    profiles = parse_profiles(lines[i_pt:i_end], planes)

    n_caps = sum(len(t["rows"]) for t in caps)
    n_items = sum(len(p["items"]) for p in profiles)
    raw_caps = len(re.findall(r"^\| `CAP-", src, re.M))
    raw_items = len(re.findall(r"^- `PT-", src, re.M))
    if (n_caps, n_items) != (raw_caps, raw_items):
        sys.stderr.write("MISMATCH: CAP %d/%d, PT %d/%d (parsed/raw)\n"
                         % (n_caps, raw_caps, n_items, raw_items))
        return 1

    tpl = io.open(TEMPLATE, encoding="utf-8").read()
    if "__CAP_DATA__" not in tpl or "__PT_DATA__" not in tpl:
        sys.stderr.write("template.html is missing a data placeholder\n")
        return 1
    tpl = tpl.replace("__CAP_DATA__", json.dumps(caps, ensure_ascii=False))
    tpl = tpl.replace("__PT_DATA__", json.dumps(profiles, ensure_ascii=False))
    io.open(out_path, "w", encoding="utf-8").write(tpl)

    sys.stderr.write("%d CAP rows in %d themes, %d PT items in %d profiles -> %s\n"
                     % (n_caps, len(caps), n_items, len(profiles), out_path))
    return 0


if __name__ == "__main__":
    sys.exit(main())
