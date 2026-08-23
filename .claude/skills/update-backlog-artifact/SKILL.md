---
name: update-backlog-artifact
description: Regenerate and republish the CSVM backlog Artifact (the "by theme & tag" filterable table) from the current backlog.md. Use after backlog.md changes, or when the user asks to refresh/update/regenerate the backlog table or artifact.
---

Keep the published backlog Artifact in sync with `backlog.md`. The artifact is a static page with a
`DATA` array baked in at publish time — it does not read `backlog.md` live, so it goes stale the
moment an item is added, closed, or re-tagged. This skill's only job is: reparse, reinject, republish
**to the same URL**.

This skill is scoped to regenerating the artifact. It does not edit `backlog.md`, and it does not
decide which items belong in it — every open item in the file goes in, full stop.

## 1. Build the page

Run the skill's own parser — it does steps 1–3 (parse, serialize, inject) in one go:

```powershell
python .claude/skills/update-backlog-artifact/build.py <scratchpad>/backlog-table.html
```

It resolves `backlog.md` and `template.html` from its own location, so it works from any working
directory and from a worktree. It prints the item count, theme count and theme order to stderr, and
**exits non-zero** if a bullet fails to parse, if an ID is duplicated, or if the parsed row count
does not match the raw `- \`BL-NNN\`` bullet count in the file — so a green run *is* the sanity
check. On a non-zero exit, fix the parser (or the file) rather than falling back to a hand parse;
the failure message names the offending item.

⚠ `build.py` reads and writes explicit UTF-8. Edit it with Read/Edit/Write, never through a
PowerShell `Get-Content`/`Set-Content` round-trip (see `CLAUDE.md`).

Then go to step 4. **Sections 2–3 below document what `build.py` already does** — read them when
`backlog.md`'s own conventions change and the parser has to follow.

## 2. What the parser extracts

Theme order comes from the file itself from the file itself — the `## ` headings in file order,
**excluding** `## Standing notes` (that section holds prose, not items). Do not hardcode the theme
list; if a theme is renamed or a new one is added, the parse should pick it up automatically.

Within each theme, find every top-level bullet matching:

    - `BL-NNN` `[Type]` [`[Status]`] **Title…** body…

For each bullet, extract:

- **id** — the `BL-NNN` token, verbatim.
- **type** — the bracketed word right after the id: `Bug`, `Feature`, `Research`, `Tuning`, or
  `Cleanup`.
- **status** — the *next* bracketed tag if one immediately follows the type tag, else `null`.
  Keep it verbatim, including the reason (`Owed-playtest`, or `Blocked: CAP-27` /
  `Blocked: M4` / etc. — whatever text is inside the brackets). Don't shorten `Blocked: X` to
  `Blocked`; the template already treats anything starting with `Blocked` as one status class and
  displays the tag text as-is.
- **title** — everything between the first `**` after the tags (skipping an optional `{SCOPE}`
  marker, as on `BL-037`/`BL-038`) and its matching closing `**`,
  even when the bold text wraps across multiple source lines. Collapse any internal newlines/runs
  of whitespace to a single space. Keep the text otherwise verbatim (inline code spans, punctuation,
  quotes) — don't paraphrase or truncate it.

Skip nothing: every bulleted item in every theme section goes into the row list, in the order it
appears in the file (themes in heading order, items in file order within a theme — which is already
ascending by ID per the file's own convention).

The parser's own guard is the count check: the row count must equal the raw bullet count in the
file (roughly 100+ as of 2026-08). A count far lower means the title regex swallowed a later bullet
by matching too greedily across items — `build.py` bounds each bullet's text at the next top-level
bullet or `## ` heading to prevent exactly that.

## 3. Serialization and injection

Rows are serialized as a JSON array of 5-element arrays: `[theme, id, type, status, title]`,
`status` being JSON `null` when absent, with real JSON string escaping — never hand-spliced into a
JS literal, since titles routinely contain quotes, backticks, and em dashes.

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

State the item count and theme count that got baked in, the artifact URL, and whether
`artifact.json` changed (first-run publish) — if it did, mention it's worth committing so the next
session/skill run picks up the same URL. Don't commit it yourself unless asked.
