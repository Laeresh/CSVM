---
name: commit-next
description: Commit the current plan-item's changes, then print a ready-to-paste prompt for the next open checklist item and copy it to the clipboard. Use when finishing a plan item and moving to the next (the commit → /clear → continue loop).
---

You are closing out one item of the active plan and teeing up the next. This skill runs while you still have the context of the task you just finished — use it. Do the steps in order, then stop.

⚠ **Output rule: text written between tool calls is not shown in chat.** Run *all* tool calls first (git, plan reads, the clipboard write), and deliver every piece of user-facing output — commit hash, recommendation, the prompt block, the closing line — in **one final message after the last tool call**. Never print the block and then call `Set-Clipboard` after it; that hides the block.

If an item id was passed as an argument (e.g. `B19`), treat that as the explicit choice for the "next item" in step 2.

## 1. Commit the current changes (if there are some)

First find the **active plan**: PROJECT_CONTEXT.md's "Current status / next step" section names it (none is active as of 2026-07-25 — every plan sits in `docs/plans/`). Everything below refers to that file.

Before committing, confirm the plan reflects the work you just did — per the plan's own ground rules this is part of the change, not a follow-up:
- The item you just finished is flipped to ☑ in the plan's `## Checklist`.
- PROJECT_CONTEXT.md "Current status" is refreshed (current state + next step only).
- Any docs/formats or architecture updates the item requires are in. (No `docs/HISTORY.md` entry — that file is frozen; the commit message body is the record now.)

If any of that is missing, make those edits **now**, before the commit, so they land together.

Then commit ALL current changes as one commit:
- Run `git status --short` and `git diff --stat HEAD` to see the state. If there is nothing to commit, say so and skip to step 2.
- `git add -A`, then `git commit`.
- **Message style**: subject line matches `git log --oneline -5`: `M3 Wave <X> <item(s)>: <what landed>` (e.g. `M3 Wave B B18: weapon selectors`). Name the item(s) you actually implemented this session — you know them from context; don't reverse-engineer them from the diff.
- **The message body is the durable record** (it replaced `docs/HISTORY.md`, frozen 2026-08-06): what landed, how it was verified (test counts, golden results, screenshot A/Bs), and any diagnosis dead ends worth not re-chasing. Write it while you still have the session context. Multi-line message → `Write` it to a file and `git commit -F <file>` (per CLAUDE.md).
- End the message with a `Co-Authored-By:` trailer on its own line, naming whichever agent and
  model is actually running this session (not a fixed name) — e.g.
  `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Commit to the current branch (**main**). Do NOT create a branch. Do NOT push.
- Note the resulting commit hash and subject line for the final message (don't print them yet — see the output rule above).

## 2. Find the next open item

Read the `## Checklist` section of the active plan. Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven.

- If an item id was passed as the argument (e.g. `B18`), that is the next item — use it verbatim.
- Otherwise the next item is the first **◐ in-progress** item, or if there are none, the first **☐ open** item, in wave/number order.
- Respect the plan's "Dependency and parallelism notes". If the first open item is blocked by an unfinished dependency, pick the first **unblocked** open item instead and state in one line which item you skipped and why.

## 3. Assess the context strategy — `/clear`, `/compact`, or continue

The loop's default is `/clear` between items (fresh context per task). But when the next item is strongly related to what you just did, clearing throws away context you would only rebuild — re-reading the same files, re-deriving the same findings — which burns tokens for nothing. Judge which of the three fits and recommend it with a one-line reason.

**Check the tier first.** Read the next item's `**Model recommendation.**` line in the plan (older plans may not have one — then skip this check). It names a tier (low / medium / high / max), not a specific model. Compare it against this session's tier — you know which model you're running and can place it on that scale. A mismatch weighs toward `/clear`: the whole point of continue is keeping context warm in the *same* session, and that argument collapses when the next item belongs on a different tier — a cheap item continued on an expensive model wastes money, and an item flagged for a higher tier must not be continued on a lower one. Only recommend **continue** on a mismatch if the in-session context is genuinely irreplaceable, and say so explicitly.

Weigh the just-finished item against the next one:

- **Continue (no clear)** — the next item touches the **same file(s)/module(s)** you just edited, is the **next link in a dependency chain** (e.g. B11 → B12 per the plan's dependency notes), or **relies on something established this session that isn't yet written to a doc** (a measured baseline, a hard-won mental model of a gnarly file). Clearing would re-pay exactly that cost. This is the case worth catching.
- **`/compact`** — related, but the session is **long and full of exploration or dead ends**. Compact keeps the distilled thread (what shipped, the live findings) and sheds the transcript noise — continuity at a lower token cost than carrying everything forward.
- **`/clear`** — the next item is **independent**: a different wave/module, no shared files, nothing it needs beyond the plan + docs (which a fresh context reloads cheaply). Safest against stale assumptions, and the loop's default. Because each item lands its own commit-message record + `docs/` updates, most durable context is already on disk, so a clear rarely loses anything that matters — the exception is the un-written in-session context the "continue" case is about.

State it as one line: **Recommend: `<continue | /compact | /clear>` — `<why>`.** When the plan names a tier for the next item, append it: **Next item wants `<tier>` (this session: `<tier>`).**

## 4. Compose the ready-to-paste next-task prompt, copy it, THEN print everything

Compose a self-contained prompt for the next item that stands on its own after a `/clear` or `/compact`. Assume the reader has CLAUDE.md loaded but zero memory of this session, so it must name the item and point at where the detail lives. Use this shape (fill in the `<...>`):

⚠ **The template's two body paragraphs are mirrored verbatim in `/plan-item` §5 option 1 — keep them in sync.** That skill works an item in-session from the same instructions, so a change here that isn't reflected there makes the two paths diverge silently.

~~~
Implement item <ID> — <one-line title> — from <active plan path> (the M3 weapons plan).

Before writing code: read that item's full "### <ID>" detail in the plan and the plan's "## Ground rules" and "## ⚠ Read this before implementing anything" sections, plus the docs/architecture.md entry for every module you'll touch. Verify data against the extracted JSON — never guess a value.

Land it complete in the same turn: follow the plan's Verify step, update docs/formats or docs/architecture as the item requires, flip the checklist item to ☑, and refresh PROJECT_CONTEXT.md "Current status". The verification record goes in the commit message. Commit only when I ask (with /commit-next).
~~~

First copy that exact prompt text into the clipboard with the PowerShell tool, using a single-quoted here-string (closing `'@` at column 0):

~~~
Set-Clipboard -Value @'
<the prompt text, verbatim>
'@
~~~

**Then — with no further tool calls — emit the single final message** containing, in order: the commit hash + subject from step 1 (or "nothing to commit"), the step-3 recommendation line, the prompt inside **one triple-backtick code block and nothing else inside it**, and a single closing line matched to your step-3 recommendation, noting the prompt is already in the clipboard. If the next item's tier differs from the session's current tier, name the tier and let the human switch models however their tool does that (in Claude Code, `/model <name>`):
- **continue** → *Recommended: continue — no clear needed. Say the word and I'll start `<ID>` in this context. (The block above is already in your clipboard if you'd rather clear anyway.)*
- **`/compact`** → *Recommended: run `/compact`, then paste the block above (already in your clipboard) to start `<ID>`.*
- **`/clear`** → *Recommended: `/clear` (switch to a `<tier>`-tier model per the plan, if it differs), then paste — the block above is already in your clipboard.*
