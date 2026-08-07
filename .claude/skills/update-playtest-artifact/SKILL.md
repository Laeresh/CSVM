---
name: update-playtest-artifact
description: Regenerate and republish the CSVM playtest Artifact (owed captures + actionable flights, with a friend-facing HowTo) from the current playtest.md. Use after playtest.md changes, or when the user asks to refresh/update/regenerate the playtest table or artifact.
---

Keep the published playtest Artifact in sync with `playtest.md`. The artifact is a static page with
`CAP_DATA`/`PT_DATA` arrays baked in at publish time — it does not read `playtest.md` live, so it
goes stale the moment an item is added, closed, or edited. This skill's only job is: reparse,
reinject, republish **to the same URL**.

This skill is scoped to regenerating the artifact. It does not edit `playtest.md`, and it does not
decide which items belong in it — every open `CAP-nn` row and `PT-nn` item in the file goes in, full
stop. It also does not touch the artifact's preamble/HowTo copy (see step 0) — that is
friend-facing prose authored once, not derived from `playtest.md`.

## 0. What NOT to regenerate

`template.html` has a static preamble section (CSVM one-liner, CAP-vs-PT explainer, the auto-head-
turn-off validity gate, the "found something?" report line) written for external testers, not repo
contributors. It is **not** parsed from `playtest.md` — leave it alone unless the user explicitly
asks to change the HowTo copy itself.

## 1. Parse `playtest.md` into CAP rows (Section 0)

Read `playtest.md`. Find `## 0 · Owed captures`. Get theme order from the `### ` headings within
that section, in file order. Each theme heading's trailing clause (after the em dash, if present)
is descriptive only — use the heading text verbatim as the theme name.

Each theme is followed by a markdown table with a header row whose last-but-one column is either
`What must be in frame` or `What must be audible` (Audio is the only `audible` theme currently) —
capture that column's exact header text as the theme's `detailLabel`.

For each data row `| \`CAP-NN\` | Capture | Detail | Unblocks |`:
- **id** — the `CAP-NN` token, verbatim.
- **capture** — the second column, verbatim (may contain inline code/bold).
- **detail** — the third column, verbatim.
- **unblocks** — the fourth column, verbatim (may be multiple `BL-NNN` refs with parentheticals, or
  empty).

**Skip themes with zero data rows entirely** — do not emit an entry for them (the artifact hides
empty themes by design; `playtest.md` itself keeps the empty table as a "nothing owed right now"
marker, but that's a file-only convention).

Sanity check: row count should match `grep -c '| \`CAP-' playtest.md`.

## 2. Parse `playtest.md` into PT profiles (Section 1)

Find `## 1 · Actionable now`. Each `### ` heading within it is one **flight profile**. Parse the
heading `<Chapter> · <Plane/Pilots> — <Situation>` into:
- **chapter** — text before the first ` · ` (e.g. `C1`, `C1B`).
- **planeOrPilots** — text between ` · ` and ` — `.
- **situation** — text after ` — `.
- **heading** — the full heading text, verbatim, for display.

Immediately following the heading is one ```` ```powershell ```` block — capture its single command
line verbatim as **command**.

### Translating the command into a menu step

Parse the codes→names map live from this file's own **"Plane model names"** line near the top
(don't hardcode it — it's the source of truth): each `` `player_xxx` Name `` pair, split on `·`.

From **command**, extract flags and build **menu**:
- `--plane=player_xxx` → look up the display name → `menu.plane`. Absent (e.g. a `--vs` profile
  where each pilot picks their own) → `menu.plane = null`.
- `--chapter=X` → `menu.chapter` (verbatim, e.g. `C1B`).
- `--vs` present → prepend `"Mode: Dogfight (splitscreen)"` to `menu.notes`.
- `--players=N` → append `` "N players" `` to `menu.notes`.
- `--infinite-ammo` → append `"infinite ammo — enable in-game if the menu offers it, otherwise skip"`
  to `menu.notes`.
- any other flag → append its literal text (e.g. `--stunt`) to `menu.notes` verbatim — don't invent
  a phrase for flags not seen before; a raw flag is a safe fallback since **command** is always
  shown alongside menu steps in the template.

`menu.notes` is an array, possibly empty.

### PT items within a profile

Each top-level `- \`PT-nn\` ...` bullet is one item (sub-bullets belong to it, not to a new item).
Parse:

- **id** — the `PT-nn` token, verbatim.
- **tag** — the bracketed tag right after the id: `` `[A/B: <ref>]` `` → `{type: "A/B", ref:
  "<ref>"}` (ref verbatim, drop the surrounding backtick if the ref itself isn't code); `` `[Own]`
  `` → `{type: "Own"}`.
- **title** — the bold text immediately after the tag, collapsing internal newlines/runs of
  whitespace to a single space, verbatim otherwise (keep inline code spans, `BL-NNN` refs,
  punctuation).
- **context** — prose between the end of the bold title and the `*Look for:*` (or `*Blocks:*` if
  there's no Look for) marker, whitespace-collapsed. Empty string if there's none.
- **lookFor** — array of strings, one per lettered sub-bullet (`(a)`, `(b)`, …) under `*Look for:*`,
  each whitespace-collapsed, letter prefix kept in the text (e.g. `"(a) firing: a subtle fast roll
  buzz…"`). If `*Look for:*` has no lettered sub-bullets (a single inline check), the array has one
  entry with no letter prefix.
- **notes** — any additional labeled paragraph(s) that appear *after* `*Look for:*` and *before*
  `*Blocks:*` (e.g. a `*CAP-11 evidence (…):*` paragraph) — rare, empty string when absent. Keep the
  label text as part of the string, whitespace-collapsed.
- **blocks** — text after `*Blocks:*` up to `*Variations:*` or the item's end, whitespace-collapsed.
  Always present (mandatory field per `playtest.md`'s own convention).
- **variations** — text after `*Variations:*` to the item's end, whitespace-collapsed. Empty string
  when absent.

Items stay attached to the profile section they're physically under — do not regroup by
chapter/plane beyond what the file's own sectioning already does (an item "free to choose its plane"
already piggybacks on an existing section per the file's convention; the parser doesn't need to
infer that).

Sanity check: item count should match `grep -c '^- \`PT-' playtest.md`.

## 3. Serialize to JSON

Build two structures:

**`CAP_DATA`** — array of theme objects, themes with data rows only, in file order:

    [{ "theme": "...", "detailLabel": "...", "rows": [
      { "id": "CAP-NN", "capture": "...", "detail": "...", "unblocks": "..." }, ...
    ]}, ...]

**`PT_DATA`** — array of profile objects, in file order:

    [{ "heading": "...", "chapter": "...", "planeOrPilots": "...", "situation": "...",
       "command": "...", "menu": { "chapter": "...", "plane": "..."|null, "notes": [...] },
       "items": [
         { "id": "PT-nn", "tag": {...}, "title": "...", "context": "...", "lookFor": [...],
           "notes": "...", "blocks": "...", "variations": "..." }, ...
       ]}, ...]

Use real JSON string escaping — do not hand-splice strings into a JS literal, since titles and
context routinely contain quotes, backticks, and em dashes.

## 4. Inject into the template

Read `.claude/skills/update-playtest-artifact/template.html`. It contains exactly two lines:

    const CAP_DATA = __CAP_DATA__;
    const PT_DATA = __PT_DATA__;

Replace each placeholder with its JSON text from step 3 (`Edit`, exact string match). Nothing else
in the template changes — tabs, filters, grouping, and the preamble/HowTo copy are static or derived
from the data at render time.

Write the result to a new file in this session's scratchpad directory — e.g. `playtest-table.html`.
Don't write generated output back into the skill's `template.html`.

## 5. Publish to the existing URL

Read `.claude/skills/update-playtest-artifact/artifact.json` for the stored `url`, `favicon`,
`title`, and `description`.

- **File exists (the normal case):** call `Artifact` with `file_path` = the scratchpad file from
  step 4, `url` = the stored URL, and the stored `favicon`/`title`/`description`.
- **File missing or has no `url` (first run, or the prior artifact was lost):** call `Artifact`
  without `url` to publish fresh, using favicon `🎬` and title `CSVM Playtest — Captures & Flights`
  (or what's already in `artifact.json`, if present). Then write the returned URL back into
  `artifact.json` so every later run updates in place.

## 6. Report

State the CAP row count + theme count, the PT item count + profile count, the artifact URL, and
whether `artifact.json` changed (first-run publish) — if it did, mention it's worth committing so
the next session/skill run picks up the same URL. Don't commit it yourself unless asked.
