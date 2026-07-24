---
name: commit-next
description: Commit the current plan-item's changes, then print a ready-to-paste prompt for the next open checklist item. Use when finishing a plan item and moving to the next (the commit → /clear → continue loop).
---

You are closing out one item of the active plan and teeing up the next. This skill runs while you still have the context of the task you just finished — use it. Do the three steps in order, then stop.

If an item id was passed as an argument (e.g. `B19`), treat that as the explicit choice for the "next item" in step 2.

## 1. Commit the current changes

First find the **active plan**: CLAUDE.md's "Current status / next step" section names it (currently `docs/PLAN-M3-weapons.md`). Everything below refers to that file.

Before committing, confirm the plan reflects the work you just did — per the plan's own ground rules this is part of the change, not a follow-up:
- The item you just finished is flipped to ☑ in the plan's `## Checklist`.
- CLAUDE.md "Current status" is refreshed (current state + next step only).
- A dated entry is appended to `docs/HISTORY.md`, and any docs/formats or architecture updates the item requires are in.

If any of that is missing, make those edits **now**, before the commit, so they land together.

Then commit ALL current changes as one commit:
- Run `git status --short` and `git diff --stat HEAD` to see the state. If there is nothing to commit, say so and skip to step 2.
- `git add -A`, then `git commit`.
- **Message style** matches `git log --oneline -5`: `M3 Wave <X> <item(s)>: <what landed>` (e.g. `M3 Wave B B18: weapon selectors`). Name the item(s) you actually implemented this session — you know them from context; don't reverse-engineer them from the diff.
- End the message with this trailer on its own line:
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- Commit to the current branch (**main**). Do NOT create a branch. Do NOT push.
- Print the resulting commit hash and subject line.

## 2. Find the next open item

Read the `## Checklist` section of the active plan. Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven.

- If an item id was passed as the argument (e.g. `B18`), that is the next item — use it verbatim.
- Otherwise the next item is the first **◐ in-progress** item, or if there are none, the first **☐ open** item, in wave/number order.
- Respect the plan's "Dependency and parallelism notes". If the first open item is blocked by an unfinished dependency, pick the first **unblocked** open item instead and state in one line which item you skipped and why.

## 3. Assess the context strategy — `/clear`, `/compact`, or continue

The loop's default is `/clear` between items (fresh context per task). But when the next item is strongly related to what you just did, clearing throws away context you would only rebuild — re-reading the same files, re-deriving the same findings — which burns tokens for nothing. Judge which of the three fits and recommend it with a one-line reason.

Weigh the just-finished item against the next one:

- **Continue (no clear)** — the next item touches the **same file(s)/module(s)** you just edited, is the **next link in a dependency chain** (e.g. B11 → B12 per the plan's dependency notes), or **relies on something established this session that isn't yet written to a doc** (a measured baseline, a hard-won mental model of a gnarly file). Clearing would re-pay exactly that cost. This is the case worth catching.
- **`/compact`** — related, but the session is **long and full of exploration or dead ends**. Compact keeps the distilled thread (what shipped, the live findings) and sheds the transcript noise — continuity at a lower token cost than carrying everything forward.
- **`/clear`** — the next item is **independent**: a different wave/module, no shared files, nothing it needs beyond the plan + docs (which a fresh context reloads cheaply). Safest against stale assumptions, and the loop's default. Because each item lands its own `docs/HISTORY.md` + `docs/` updates, most durable context is already on disk, so a clear rarely loses anything that matters — the exception is the un-written in-session context the "continue" case is about.

State it as one line: **Recommend: `<continue | /compact | /clear>` — `<why>`.**

## 4. Print the ready-to-paste next-task prompt

Emit **one triple-backtick code block and nothing else inside it** — a self-contained prompt for the next item that stands on its own after a `/clear` or `/compact`. Assume the reader has CLAUDE.md loaded but zero memory of this session, so it must name the item and point at where the detail lives. Use this shape (fill in the `<...>`):

~~~
Implement item <ID> — <one-line title> — from <active plan path> (the M3 weapons plan).

Before writing code: read that item's full "### <ID>" detail in the plan and the plan's "## Ground rules" and "## ⚠ Read this before implementing anything" sections, plus the docs/architecture.md entry for every module you'll touch. Verify data against the extracted JSON — never guess a value.

Land it complete in the same turn: follow the plan's Verify step, update docs/formats or docs/architecture as the item requires, append a dated docs/HISTORY.md entry, flip the checklist item to ☑, and refresh CLAUDE.md "Current status". Commit only when I ask (with /commit-next).
~~~

After the code block, add a single closing line matched to your step-3 recommendation:
- **continue** → *Recommended: continue — no clear needed. Say the word and I'll start `<ID>` in this context. (The block above is only if you'd rather clear anyway.)*
- **`/compact`** → *Recommended: run `/compact`, then paste the block above to start `<ID>`.*
- **`/clear`** → *Recommended: copy the block above, then `/clear`, then paste it to start `<ID>`.*
