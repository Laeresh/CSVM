---
name: plan-item
description: Explain the active plan's item in plain language — where it sits, its goal, evidence, traps, how it gets done, and how you'd know it worked; then start it, close it, or defer it. Use when the user names a plan item (`A1`, `B11`) or asks what to work on next.
---

Explain one item of the **active plan** so it can be worked cold, then start it, close it, or park it.

The sibling skill for `backlog.md` entries is [`/backlog`](../backlog/SKILL.md) — a `BL-NNN` that is
*not* scheduled into the active plan belongs there, not here.

The explanation phase is **read-only**: no builds, no `RunTests.ps1`, no `--freecam` runs. Once the
user picks *start it here*, that restriction lifts — that is the work.

⚠ **Output rule: text written between tool calls is not shown in chat.** The explanation is this
skill's entire deliverable, so it must be the **final message after the last tool call** — do all the
reading of §1–§3 first, then emit §4 and §5 together as one message and stop. Do **not** call
`AskUserQuestion` (or any other tool) to pose §5's choice: that turns the explanation into
between-calls text and the user sees only the question. §5's three options are plain text at the end
of that message.

## 1. Resolve the plan

The **active plan only**, resolved from `PROJECT_CONTEXT.md`'s "Current status / next step" section.
One authority; do not glob for candidates.

- **No active plan named** → list any `docs/PLAN-*.md` files and ask which, or if there are none say
  so and point at `/new-plan`. Never guess.
- **An explicitly named plan** (`/plan-item A1 in PLAN-m3-polish-5`, or a path) → honour it, but if
  it carries a `COMPLETE` banner or lives in `docs/plans/`, explain it **read-only** and skip §4
  entirely: retroactively starting or deferring a finished item is nonsense. Point at its
  `docs/HISTORY.md` entry instead.

## 2. Resolve the item

- An **item ID** (`A1`, `a1`, `B11`, `D31`) — exact match wins, case-insensitive.
- A **`BL-NNN` the plan carries** (`BL-051` → A1) — the two vocabularies are interchangeable here.
- A **phrase** → list the candidate items with their wave headings and **ask**. Never guess silently.
- **No argument** → the next item, by `/commit-next` step 2's rule: the first **◐ in progress**, else
  the first **☐ open** in wave/number order, **respecting the plan's "Dependency and parallelism
  notes"**. If the first open item is blocked by an unfinished dependency, take the first unblocked
  one and say in one line which item you skipped and why.
- An **ID the plan does not have** is a typo or the wrong plan — not a deleted item. List the IDs the
  plan actually has rather than offering a near-numbered substitute.

One item per invocation.

## 3. Read it, inline — three tiers

Do the reading yourself; no Explore subagent. The item text and code need to stay in context, because
*start it here* is the default handoff and works from exactly this context.

1. **The plan, always** — the item's full `### <ID>` section, plus the plan's `## Ground rules`, its
   `## ⚠ Read this before implementing anything` section if it has one, `## Decisions`, the item's
   `## Checklist` line, and `## Dependency and parallelism notes`.
2. **The backlog entry, always** — the `BL-NNN` the item cites, and its enclosing section heading
   (the heading carries status). Plan items summarise and delegate — "Full evidence: `backlog.md`
   `BL-051`" — so the counts, the rejected fixes, and the real traps live there.
3. **Only what the item names** — the code files and symbols (`FromToMotion.cs:109-111`,
   `WorldBuilder.NoCollisionNode`), the doc sections (`docs/architecture.md`, `docs/verification.md`
   rule IDs), and any `analysis/*/FINDINGS.md` it cites. Read them to confirm the claim still holds.
   Don't hunt beyond the citations. ⚠ Line numbers in plan and backlog text predate later refactors —
   re-locate symbols **by name**.

While there, check whether the item **already landed** without its checklist being flipped — a
`docs/HISTORY.md` entry, or code that already does the thing.

**Confirmation is by reading, not by running.** When a claim can only be settled by executing
something (a collider count printed at build, a per-chapter node census), say so under *The evidence,
and how far it goes* — "unverified here; needs a build" — and leave it to the work. Reporting a number
as re-confirmed when you only re-read the code that produces it is the masked-effect failure the
plan's Verify sections keep warning about.

## 4. Explain it

Plain prose, short paragraphs. Gloss every code symbol and original-game term in a clause on first
use — "`DestructibleRegistry`, the list of things a weapon can damage". No code blocks unless a
snippet is the clearest way to show a trap. Keep each section to what it needs; say "nothing stated"
rather than padding.

Use these headings, in this order:

- **Where it sits** — plan, wave, checklist status; from the dependency notes: what must land first,
  what this blocks, what it must not share a commit window with. End with the tier line —
  *item wants `high` (this session: `medium`)* — from the item's `**Model recommendation.**` line
  against the tier you are running. **On a mismatch, say plainly that the item wants a different tier
  before the user picks *start it here*.** Older plans may carry no recommendation; then skip it.
- **Status** — one line: still open, or looks already landed / superseded / moot. Cite the evidence
  (a `docs/HISTORY.md` entry, the code state).
- **Goal** — what will be true when the item is done.
- **The problem** — what's wrong today, and why it matters at the controls.
- **The evidence, and how far it goes** — the item's own confidence label (traced / direction-sound-
  magnitude-TUNE / lead-only) said in plain words, plus whether the tier-3 reading confirmed it still
  holds, and what remains unverified without a run. An item flagged **lead-only** is a question, not
  a finding; treating it as a finding is this repo's most-repeated failure.
- **Traps** — the ⚠ notes from both the plan item and the backlog entry, plus anything the code
  reading revealed: wrong-mechanism "fixes", unsettled decodes, things that look like the bug.
- **How it gets done** — the Approach as an ordered route: survey → decide → implement, files named.
- **How you'd know it worked** — the Verify step, including which goldens are expected to move and
  which `docs/verification.md` rule bites here.
- **Open questions** — what only the user can settle: an unsettled TUNE, a needed original-game A/B.
- **Related** — only the `BL-NNN`s, sibling plan items, and doc sections the item itself cites. No
  adjacency guessing; don't invent links nobody authored.

If the item looks stale, already landed, or self-contradictory, say so under **Status** — and let §5's
*close it* handle it rather than editing anything now.

## 5. Offer the handoff

End the same message with the three options below as plain text — numbered, one line each, naming the
default. No tool call (see the output rule at the top; an `AskUserQuestion` here hides §4 entirely).
**Take no action until the user answers.**

### Option 1 — start it here *(the default)*

Work the item in this context. The paste-and-clear variant is `/commit-next <ID>` — do not re-add it
here.

⚠ **The two paragraphs below are mirrored verbatim from `/commit-next` step 4's next-task prompt —
keep them in sync.** They are what a fresh session pasting that prompt would be told, so working from
them makes this path start identically.

~~~
Before writing code: read that item's full "### <ID>" detail in the plan and the plan's "## Ground rules" and "## ⚠ Read this before implementing anything" sections, plus the docs/architecture.md entry for every module you'll touch. Verify data against the extracted JSON — never guess a value.

Land it complete in the same turn: follow the plan's Verify step, update docs/formats or docs/architecture as the item requires, append a dated docs/HISTORY.md entry, flip the checklist item to ☑, and refresh PROJECT_CONTEXT.md "Current status". Commit only when I ask (with /commit-next).
~~~

Tiers 1–3 already satisfied most of the first paragraph — say so in one line rather than silently
skipping it. What they did **not** cover is touched-module-dependent: **read the
`docs/architecture.md` entry for every module you are about to modify**, since dead ends are recorded
there precisely so they are not re-chased.

### Option 2 — close it

The plan bookkeeping is this skill's; the `BL-NNN` side is not.

- Flip the checklist line: **☑** if the work landed, **❌** if the explanation disproved it or it is
  moot. Ask which if it isn't obvious — the glyph is the plan's record of *why*.
- Add the one-line verdict to the item's `### <ID>` body.
- Advance `PROJECT_CONTEXT.md` "Current status" if this was the next-item pointer — **swap or delete
  only**, never add a sentence about what closed (that section's own fixed-shape rule).
- Then hand to **`/close-backlog-item <BL-NNN>`**, unmodified. It owns the closure kind, deleting the
  entry, the dated `docs/HISTORY.md` entry with its forward-pointer, retiring any `CAP-nn`/`PT-nn`,
  and the restated-caveat sweep. Do not pre-empt its history entry.
- **If that was the plan's last open item**, say so and *offer* the completion move — `COMPLETE`
  banner, move to `docs/plans/`, add its row to `docs/plans/plans.md`. Don't do it unasked; but a
  finished plan left in `docs/` still resolves as "active" for the next session.
- **No commit.** Offer it in one line and stop.

### Option 3 — defer it

**Write nothing.** The item stays `☐ open` in the plan, `backlog.md` is untouched, and
`PROJECT_CONTEXT.md` is untouched — "not now" is a session decision, and a deferral note that is
obsolete tomorrow is noise.

Say in one line that nothing was written, then resolve the **next** item by §2's no-argument rule,
excluding anything deferred earlier in this session. **Name that item and ask** whether to explain it.
Do not auto-explain it — two full three-tier reads chained on one keystroke is a lot of context for
nothing.
