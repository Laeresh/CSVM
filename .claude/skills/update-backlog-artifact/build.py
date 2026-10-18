# Regenerate the backlog Artifact page from the GitHub issue tracker.
#
#   python build.py <output.html>
#
# Builds one row object per open `backlog`-labelled GitHub issue:
#   {theme, id, type, status, size, next, impact, evidence, scope, title}
# with id "#N" and the theme and tags taken from its theme:/type:/size:/next:/impact:/evidence:
# and status labels, and injects them as JSON in place of the template's __BACKLOG_DATA__
# placeholder. A failed `gh` call is a build failure.
#
# All file I/O is explicit UTF-8; edit this file with the Read/Edit/Write tools
# rather than a PowerShell round-trip (see CLAUDE.md).

import io
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
TEMPLATE = os.path.join(HERE, "template.html")

# The label vocabularies docs/agents/issue-tracker.md documents under "Backlog labels".
TYPES = ["Bug", "Feature", "Research", "Tuning", "Cleanup", "Fidelity", "Perf", "Tooling", "Testing"]
SIZES = ["S", "M", "L"]
VOCAB = {
    "Next": ["decode", "data", "code", "look", "decide"],
    "Impact": ["high", "low", "none"],
    "Evidence": ["decoded", "data", "footage", "spec", "feel", "trace"],
}

# theme:<slug> whose description is the theme's heading, type:<lower>, size:, next:, impact:,
# evidence:, and the status labels below. A triage label stands in for the status when no status
# label is present.
UNTRIAGED = "Untriaged"
# The fixed theme order (docs/agents/issue-tracker.md, "Backlog labels"); a new theme label sorts last.
THEME_ORDER = ["damage", "weapons", "flight", "environment", "effects", "audio", "cameras", "hud",
               "splitscreen", "missions", "tooling", "misc"]
STATUS_LABELS = {"owed-playtest": "Owed-playtest", "blocked": "Blocked", "divergence": "Divergence"}
TRIAGE = ["needs-triage", "needs-info", "ready-for-agent", "ready-for-human", "wontfix"]


def gh_json(args):
    out = subprocess.run(["gh"] + args, capture_output=True, cwd=REPO, shell=(os.name == "nt"))
    if out.returncode != 0:
        raise RuntimeError(out.stderr.decode("utf-8", "replace").strip() or "gh failed")
    return json.loads(out.stdout.decode("utf-8"))


def fetch_issues():
    """Open `backlog` issues as rows. Returns (rows, theme headings in label order, problems);
    raises on a gh failure."""
    labels = sorted((l for l in gh_json(["label", "list", "--limit", "500", "--json", "name,description"])
                     if l["name"].startswith("theme:")),
                    key=lambda l: (THEME_ORDER + [l["name"][len("theme:"):]]).index(l["name"][len("theme:"):]))
    themes = {l["name"][len("theme:"):]: l["description"] for l in labels}
    issues = gh_json(["issue", "list", "--state", "open", "--label", "backlog", "--limit", "500",
                      "--json", "number,title,labels"])
    rows, problems = [], []
    for issue in issues:
        iid = "#%d" % issue["number"]
        row = {"theme": UNTRIAGED, "id": iid, "type": None, "status": None, "size": None,
               "next": None, "impact": None, "evidence": None, "scope": None,
               "title": issue["title"].strip()}
        names = [l["name"] for l in issue.get("labels", [])]
        for name in names:
            group, _, value = name.partition(":")
            if not value:
                row["status"] = row["status"] or STATUS_LABELS.get(name)
            elif group == "theme":
                if value not in themes:
                    problems.append("%s: no theme label %s" % (iid, name))
                row["theme"] = themes.get(value, UNTRIAGED)
            elif group == "type":
                row["type"] = next((t for t in TYPES if t.lower() == value), None)
                if row["type"] is None:
                    problems.append("%s: unknown type label %s" % (iid, name))
            elif group == "size":
                row["size"] = value if value in SIZES else None
                if row["size"] is None:
                    problems.append("%s: unknown size label %s" % (iid, name))
            elif group.capitalize() in VOCAB:
                if value not in VOCAB[group.capitalize()]:
                    problems.append("%s: %s is not one of %s"
                                    % (iid, name, ", ".join(VOCAB[group.capitalize()])))
                row[group] = value
        row["status"] = row["status"] or next((l for l in TRIAGE if l in names), None)
        row["type"] = row["type"] or "Issue"
        rows.append(row)
    rows.sort(key=lambda r: int(r["id"][1:]))
    return rows, list(themes.values()), problems


def main():
    args = sys.argv[1:]
    if len(args) != 1:
        sys.stderr.write("usage: build.py <output.html>\n")
        return 2
    out_path = args[0]

    try:
        rows, labelled, problems = fetch_issues()
    except Exception as e:  # noqa: BLE001 - any gh failure is fatal, named
        sys.stderr.write("ISSUES: %s\n" % e)
        return 1
    if problems:
        for p in problems:
            sys.stderr.write("LABEL: %s\n" % p)
        return 1

    # Themes in theme-label order, only those with an open issue, and Untriaged last.
    themes = [t for t in labelled if any(r["theme"] == t for r in rows)]
    if any(r["theme"] == UNTRIAGED for r in rows):
        themes.append(UNTRIAGED)
    rows = [r for t in themes for r in rows if r["theme"] == t]

    tpl = io.open(TEMPLATE, encoding="utf-8").read()
    if "__BACKLOG_DATA__" not in tpl:
        sys.stderr.write("template.html has no __BACKLOG_DATA__ placeholder\n")
        return 1
    tpl = tpl.replace("__BACKLOG_DATA__", json.dumps(rows, ensure_ascii=False))
    io.open(out_path, "w", encoding="utf-8").write(tpl)

    untagged = sum(1 for r in rows if r["size"] is None)
    sys.stderr.write("%d issues across %d themes (%d without a size label) -> %s\n"
                     % (len(rows), len(themes), untagged, out_path))
    sys.stderr.write("themes: %s\n" % " | ".join(themes))
    return 0


if __name__ == "__main__":
    sys.exit(main())
