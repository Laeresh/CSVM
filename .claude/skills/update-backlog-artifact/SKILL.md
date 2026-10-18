---
name: update-backlog-artifact
description: Regenerate and republish the CSVM backlog Artifact (the "by theme & tag" filterable table) from the open `backlog` GitHub issues. Use after the tracker changes, or when the user asks to refresh/update/regenerate the backlog table or artifact.
---

Keep the published backlog Artifact in sync with the open `backlog`-labelled GitHub issues on
`Laeresh/CSVM` (`docs/agents/issue-tracker.md`). The artifact is a static page with a `DATA` array
baked in at publish time — it does not read the tracker live, so it goes stale the moment an issue
is filed, closed, or re-labelled. This skill's only job is: refetch, reinject, republish **to the
same URL**.

This skill is scoped to regenerating the artifact. It does not edit any issue, and it does not
decide which items belong in it: every open `backlog` issue goes in.

## 1. Build the page

Run the skill's own builder — it does steps 1–3 (fetch, serialize, inject) in one go:

```powershell
python -I .claude/skills/update-backlog-artifact/build.py <scratchpad>/backlog-table.html
```

It resolves `template.html` from its own location, so it works from any working directory and from
a worktree. It prints the issue count, theme count and theme order to stderr, and **exits
non-zero** if `gh label list` or `gh issue list` fails or if a label value falls outside the
vocabularies — so a green run *is* the sanity check. On a non-zero exit, fix the cause (`gh`
authentication, or the issue's labels) rather than building the table by hand; the failure message
names the offending issue.

⚠ `build.py` reads and writes explicit UTF-8. Edit it with Read/Edit/Write, never through a
PowerShell `Get-Content`/`Set-Content` round-trip (see `CLAUDE.md`).

Then go to step 4. **Sections 2–3 below document what `build.py` already does** — read them when
the label conventions in `docs/agents/issue-tracker.md` change and the builder has to follow.

## 2. What the builder extracts

The issues carry their tags as labels (`docs/agents/issue-tracker.md`, "Backlog labels", which is
the vocabularies' authority; `build.py` holds a copy, keep the two in step). Each open `backlog`
issue is one row:

- **id**: `#N`.
- **theme**: the *description* of its `theme:` label, which is the theme's heading. Theme order is
  the order `gh label list` returns the `theme:` labels in, keeping only themes with an open issue;
  do not hardcode the theme list, so a renamed or new theme label is picked up automatically. An
  issue with no `theme:` label goes into an `Untriaged` theme at the end, and one whose `theme:`
  label does not exist is a build failure.
- **type**: its `type:` label, capitalised (`Bug`, `Feature`, `Research`, `Tuning`, `Cleanup`,
  `Fidelity`, `Perf`, `Tooling`, `Testing`); `Issue` when it has none.
- **status**: the `owed-playtest`, `blocked` or `divergence` label, shown as `Owed-playtest`,
  `Blocked` or `Divergence`; when none of them is present, the triage label does
  (`docs/agents/triage-labels.md`); else `null`.
- **size**, **next**, **impact**, **evidence**: the `size:`, `next:`, `impact:` and `evidence:`
  labels, each `null` when the issue does not carry it. A value outside the vocabulary is a build
  failure, with the issue named. **scope** is always `null`; issues carry no scope.
- **title**: the issue title, verbatim.

Within a theme, rows are in ascending issue-number order.

## 3. Serialization and injection

Rows are serialized as a JSON array of objects
`{theme, id, type, status, size, next, impact, evidence, scope, title}`, every optional field
JSON `null` when absent, with real JSON string escaping — never hand-spliced into a JS literal,
since titles routinely contain quotes, backticks, and em dashes. The template builds its filter
chips from the values present in `DATA`, so a new vocabulary value needs no template change; a
property no item carries hides its filter group.

That JSON replaces `__BACKLOG_DATA__` in `.claude/skills/update-backlog-artifact/template.html`,
whose data line is:

    const DATA = __BACKLOG_DATA__;

Nothing else in the template changes — theme order, filters, and counts are all derived from `DATA`
at render time in the page's own script. The result goes to the output path given on the command
line (a file in this session's scratchpad directory, e.g. `backlog-table.html`); generated output
never goes back into the skill's `template.html`.

## 4. Publish to the existing URL

Read `.claude/skills/update-backlog-artifact/artifact.json` for the stored `url`, `favicon`,
`title`, and `description`.

- **File exists (the normal case):** call `Artifact` with `file_path` = the file `build.py` wrote,
  `url` = the stored URL, and the stored `favicon`/`title`/`description`. Passing `url`
  explicitly is what makes this an in-place update instead of minting a new link — required because
  this skill usually runs in a conversation that never published the artifact itself.
- **File missing or has no `url` (first run, or the prior artifact was lost):** call `Artifact`
  without `url` to publish fresh, using favicon `🗂️` and a title/description in the same spirit as
  what's already in `artifact.json` (or the defaults there, if present). Then write the returned URL
  back into `artifact.json` so every later run updates in place.

## 5. Report

State the issue count and the theme count that got baked in, the artifact URL, and whether
`artifact.json` changed (first-run publish) — if it did, mention it's worth committing so the next
session/skill run picks up the same URL. Don't commit it yourself unless asked.
