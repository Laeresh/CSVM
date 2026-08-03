---
name: backlog
description: Explain a backlog item in plain language — its goal, the problem it solves, its traps, how it gets done, and what it's related to. Use when the user names a BL-NNN item or asks what a backlog entry means.
---

Explain one `backlog.md` item so it can be understood cold, months later, without re-reading 1800
lines of prose or chasing its references by hand.

This skill is **read-only**. Never edit `backlog.md`, never build, never run tests.

The sibling skill for **active-plan** items (`A1`, `B11`) is [`/plan-item`](../plan-item/SKILL.md) —
a `BL-NNN` already scheduled into the active plan is better explained there, since that skill reads
the plan's ground rules and dependency notes too and can start, close, or defer the item.

## 1. Resolve the item

The argument may be `BL-242`, a bare `242`, or a phrase like "rocket pylon".

- Grep `backlog.md` for the `BL-NNN` tag. An exact ID match wins outright — explain that item.
- A phrase, or an ID that isn't there: list the few candidate items with their section headings and
  **ask which one**. Never guess silently.
- IDs are assigned once and never reused or renumbered, so a missing ID means the item was
  **deleted**, not moved. Say that rather than offering a near-numbered substitute.

One item per invocation. If several IDs are given at once, explain them one after another in the
same reply.

## 2. Read it, inline

Do the reading yourself — no Explore subagent. The item text and code need to stay in context for
follow-up questions and the handoff.

- Read the entry **and its enclosing section heading** — the heading carries status (e.g. "Merged
  into `m3-polishing` — pending playtest").
- Follow the references the entry actually names: code files and symbols (`AnimRuntime.cs:214-216`,
  `FlightController.NextArmedHardpoint`) and doc sections in `docs/HISTORY.md`,
  `docs/architecture.md`, `docs/verification.md`.
- Don't go hunting beyond what the entry cites.

## 3. Explain it

Plain prose, short paragraphs. Gloss every code symbol and original-game term in a clause on first
use — "`DestructibleRegistry`, the list of things a weapon can damage". No code blocks unless a
snippet is the clearest way to show a trap. Keep each section to what it needs; say "nothing stated"
rather than padding.

Use these headings, in this order:

- **Status** — one line: still live, or looks landed/superseded. Cite the evidence (section heading,
  a `docs/HISTORY.md` entry, a merged-and-pending-playtest table).
- **Goal** — what will be true when the item is done.
- **The problem** — what's wrong today, and why it matters at the controls.
- **Traps** — the ⚠ notes plus anything the code reading reveals: wrong-mechanism "fixes", unsettled
  decodes, things that look like the bug but aren't.
- **How it gets reached** — the route to the goal: files, mechanism, order of work.
- **Size** — LARGER or localized, with the reason.
- **How you'd know it worked** — the confirm-in-the-cockpit line, or the relevant
  `docs/verification.md` procedure.
- **Open questions** — what only the user can settle: unsettled TUNEs, "needs an original-game A/B".
- **Related items** — only the `BL-NNN`s and doc sections the entry itself cites. No adjacency
  guessing; don't invent links nobody authored.

If the entry looks stale, already landed, or self-contradictory, say so under **Status** and *offer*
to fix the entry — write nothing to `backlog.md` without approval.

## 4. Offer the handoff

Close by asking (AskUserQuestion) what to do with the item:

- scaffold a plan — run `/new-plan`;
- start work — run `/grill-me` on the item first, then work from what that settles;
- stop here.

Take no action until the answer comes back.
