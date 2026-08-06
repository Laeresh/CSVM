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

## 1. Parse `backlog.md` into rows

Read `backlog.md`. Get the theme order from the file itself — the `## ` headings in file order,
**excluding** `## Standing notes` (that section holds prose, not items). Do not hardcode the theme
list; if a theme is renamed or a new one is added, the parse should pick it up automatically.

Within each theme, find every top-level bullet matching:

    - `BL-NNN` `[Type]` [`[Status]`] **Title…** body…

For each bullet, extract:

- **id** — the `BL-NNN` token, verbatim.
- **type** — the bracketed word right after the id: `Bug`, `Feature`, `Research`, `Tuning`, or
  `Cleanup`.
- **status** — the *next* bracketed tag if one immediately follows the type tag, else `null`.
  Keep it verbatim, including the reason (`Owed-playtest`, or `Blocked: CAP-28` /
  `Blocked: M4` / etc. — whatever text is inside the brackets). Don't shorten `Blocked: X` to
  `Blocked`; the template already treats anything starting with `Blocked` as one status class and
  displays the tag text as-is.
- **title** — everything between the first `**` after the tags and its matching closing `**`,
  even when the bold text wraps across multiple source lines. Collapse any internal newlines/runs
  of whitespace to a single space. Keep the text otherwise verbatim (inline code spans, punctuation,
  quotes) — don't paraphrase or truncate it.

Skip nothing: every bulleted item in every theme section goes into the row list, in the order it
appears in the file (themes in heading order, items in file order within a theme — which is already
ascending by ID per the file's own convention).

Sanity check before moving on: the row count should be in the same ballpark as
`grep -c` for the bullet pattern across the file (roughly 100+ as of 2026-08). A count far lower
usually means the title regex swallowed a later bullet by matching too greedily across items —
re-check with a narrower per-line pass if so.

## 2. Serialize to JSON

Build the row list as a JSON array of 5-element arrays: `[theme, id, type, status, title]`, `status`
being JSON `null` when absent. Use real JSON string escaping (`"` → `\"`, backslashes doubled) — do
not hand-splice the strings into a JS literal, since titles routinely contain quotes, backticks, and
em dashes that would break unescaped interpolation.

## 3. Inject into the template

Read `.claude/skills/update-backlog-artifact/template.html`. It contains exactly one line:

    const DATA = __BACKLOG_DATA__;

Replace `__BACKLOG_DATA__` with the JSON array text from step 2 (`Edit`, exact string match on that
placeholder). Nothing else in the template changes — theme order, filters, and counts are all
derived from `DATA` at render time in the page's own script.

Write the result to a new file in this session's scratchpad directory (path given in your system
prompt) — e.g. `backlog-table.html`. Don't write generated output back into the skill's
`template.html`.

## 4. Publish to the existing URL

Read `.claude/skills/update-backlog-artifact/artifact.json` for the stored `url`, `favicon`,
`title`, and `description`.

- **File exists (the normal case):** call `Artifact` with `file_path` = the scratchpad file from
  step 3, `url` = the stored URL, and the stored `favicon`/`title`/`description`. Passing `url`
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
