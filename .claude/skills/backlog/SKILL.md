---
name: backlog
description: Explain a backlog item in plain language — its goal, the problem it solves, its traps, how it gets done, and what it's related to. Use when the user names a BL-NNN item or asks what a backlog entry means.
---

Explain one `backlog.md` item so it can be understood cold, months later, without re-reading 1800
lines of prose or chasing its references by hand.

This skill is **read-only**. Never edit `backlog.md`, never build, never run tests. That holds for
the explanation, which is the whole of §1–§4; §5 runs only after the user picks the decode handoff,
and even there every write is offered and approved first.

⚠ **Output rule: text written between tool calls is not shown in chat.** The explanation is this
skill's entire deliverable, so it must be the **final message after the last tool call** — do the
reading of §1–§2 first, then emit §3 and §4 together as one message and stop. Do **not** call
`AskUserQuestion` (or any other tool) to pose §4's choice: that turns the explanation into
between-calls text and the user sees only the question. §4's options are plain text at the end of
that message.

The sibling skill for **active-plan** items (`A1`, `B11`) is [`/plan-item`](../plan-item/SKILL.md) —
a `BL-NNN` already scheduled into the active plan is better explained there, since that skill reads
the plan's ground rules and dependency notes too and can start, close, or defer the item. The
binary-decode rule below — §3's provenance triage and §4's decode option — is mirrored in that skill;
keep the two in sync.

## 1. Resolve the item

The argument may be `BL-242`, a bare `242`, or a phrase like "rocket pylon".

- Grep `backlog.md` for the `BL-NNN` tag. An exact ID match wins outright — explain that item.
- A phrase, or an ID that isn't there: list the few candidate items with their section headings and
  **ask which one**. Never guess silently.
- IDs are assigned once and never reused or renumbered, so a missing ID means the item was
  **deleted**, not moved. Say that rather than offering a near-numbered substitute.

One item per invocation. If several IDs are given at once, explain them one after another in the
same reply.

Once the ID is resolved, set the terminal window title to it — `$Host.UI.RawUI.WindowTitle = "BL-NNN"`
via the PowerShell tool — so the session is identifiable at a glance. Skip this for a multi-ID
invocation (no single ID to title the window with).

## 2. Read it, inline

Do the reading yourself — no Explore subagent. The item text and code need to stay in context for
follow-up questions and the handoff.

- Read the entry **and its enclosing section heading** — the heading carries status (e.g. "Merged
  into `m3-polishing` — pending playtest").
- Follow the references the entry actually names: code files and symbols (`AnimRuntime.cs:214-216`,
  `FlightController.NextArmedHardpoint`) and doc sections in `docs/HISTORY.md` (frozen 2026-08-06 —
  pre-freeze evidence only; later evidence lives in commit messages, `git log --grep=BL-NNN`),
  `docs/architecture.md`, `docs/verification.md`.
- Don't go hunting beyond what the entry cites.

## 3. Explain it

Plain prose, short paragraphs. Gloss every code symbol and original-game term in a clause on first
use — "`DestructibleRegistry`, the list of things a weapon can damage". No code blocks unless a
snippet is the clearest way to show a trap. Keep each section to what it needs; say "nothing stated"
rather than padding.

### The binary outranks footage and observation

`crimson.exe` is readable through the ghidra-mcp tools, so a quantity the original engine holds — a
constant, a rate, a threshold, a ramp, a curve — is **decodable**, not a TUNE and not a taste call.
Two consequences while writing §3, both of them *routing only*: this skill still calls no MCP tool of
its own, and never substitutes a guess for the decode it did not run (§5).

- An open question the binary could settle is named as decodable **where the entry raises it**, in
  the words the entry got wrong: "the entry files this as an unsettled TUNE; it is a constant in
  `crimson.exe`."
- A number the entry asserts as **settled**, whose stated provenance is footage, a screenshot A/B or
  how it felt at the controls, is flagged as decodable-and-unverified. This project has been wrong
  that way repeatedly, and such an entry reads as finished — which is exactly why nobody re-checks it.

Bound the flag to quantities the binary plausibly holds. Art direction, whether a mission *feels*
right, whether a texture looks right — the executable has nothing to say about those, and flagging
them is noise that trains the reader to skip the flag.

Use these headings, in this order:

- **Status** — one line: still live, or looks landed/superseded. Cite the evidence (section heading,
  a landing commit or pre-freeze `docs/HISTORY.md` entry, a merged-and-pending-playtest table).
- **Goal** — what will be true when the item is done.
- **The problem** — what's wrong today, and why it matters at the controls.
- **Traps** — the ⚠ notes plus anything the code reading reveals: wrong-mechanism "fixes", unsettled
  decodes, things that look like the bug but aren't. **Plus the provenance flags** — every number the
  entry states as settled that came from footage, a screenshot A/B or feel, and sits in a slot
  `crimson.exe` could answer. Name the quantity, name its stated source, and say the binary holds the
  real one.
- **How it gets reached** — the route to the goal: files, mechanism, order of work.
- **Size** — LARGER or localized, with the reason.
- **How you'd know it worked** — the confirm-in-the-cockpit line, or the relevant
  `docs/verification.md` procedure.
- **Open questions** — what neither the code nor the binary can settle: a genuine taste call, a
  live-cockpit feel judgement. **Not** the decodable ones — those were named as decodable above. If
  the entry's own "unsettled TUNE" or "needs an original-game A/B" line is really a constant in
  `crimson.exe`, say that instead of repeating it.
- **Related items** — only the `BL-NNN`s and doc sections the entry itself cites. No adjacency
  guessing; don't invent links nobody authored.

If the entry looks stale, already landed, or self-contradictory, say so under **Status** and *offer*
to fix the entry — write nothing to `backlog.md` without approval.

## 4. Offer the handoff

End the same message by asking what to do with the item — numbered plain text, one line each, no tool
call (see the output rule at the top; an `AskUserQuestion` here hides §3 entirely):

- decode it in `crimson.exe` — read the flagged quantity out of the binary (§5);
- scaffold a plan — run `/new-plan`;
- start work — run `/grill-me` on the item first, then work from what that settles;
- stop here.

**Rank the decode option by what §3 found.** If §3 flagged anything decodable — an open question the
binary can settle, or a footage-sourced constant in a binary-decodable slot — list it **first, marked
recommended, with a one-line reason naming the specific quantity**: "the stall onset rate is a
constant in `crimson.exe`; today's value came from a video." A bare "consider decoding this" is
ignorable; naming the number you are about to build against is not. If §3 flagged nothing, the option
sits last, unranked, unremarked.

It is an offer, never a gate. Do not refuse to recommend *start work* because something is
undecoded — a skill that blocks you is a skill you stop running.

If scaffold a plan or start work is chosen, ask if a worktree should be used.
Take no action until the answer comes back.

## 5. If the decode is picked

**The Ghidra project is strictly read-only.** No `rename_function`, no `set_comment`, no structs, no
prototypes, no `save_program` — not once, not "just this label". The database is not in git, so a bad
write is unreviewable and unrevertable, and this project already gets bitten by concurrent sessions
sharing state. Knowledge accumulates in the repo (below), never in the database. A deliberate
annotation pass is a different job with its own plan; it is not this.

Connection facts — server, port, project name, program path — live in the MCP configuration and
**are not restated here**; that config is the single source of truth. If the MCP is unreachable, say
so plainly and fall back to §3's routing half. Never fill the gap with a footage measurement: an
unrun decode is an open question, not a licence to guess.

**Size it before starting**, on one tell — does the entry name its own addresses or symbols?

1. **It does** (`FUN_0042ee40`, `0071c2d0`, `gwNodeSetActive`) → **decode inline**, here, bounded to
   what the entry named. No prospecting outward from it.
2. **It does not** → hand to a **fresh-context subagent**. It prospects; it **reports and never
   writes**. Require every constant back **with the address it came from** and the condition it
   applies under, so the write-up carries provenance and any number can be re-checked at source.
3. **It is genuinely large** — a body of work the size of `PLAN-weather-decompile-match.md`, not a
   "what is this constant" question → `/new-plan`. Rare; most decodes are not plan-sized.

### What a decode reports: numbers, then formulas, then rules

The MCP is for **understanding** the original game, not for re-expressing it in another language.

- A scalar is the answer: state it, with its address.
- A closed-form relationship is the answer: state it as a formula (`degrees × 0.017453292`).
- When the value is conditional — a state machine, a piecewise ramp, a per-state table — state it as
  a **rule in prose with its constants**: "below the altitude precomputed into `0071c2d0`, state 2;
  the whiteout opacity remaps 0→1 across the top of the core." Control flow **described**, never
  transcribed. Dropping the condition is how a number ends up right in one state and wrong in the
  other, so the condition is part of the answer, not decoration.
- Decompiler output or pseudocode only when a genuine multi-step algorithm *is* the answer — an
  interpreter dispatch, a hash — and nothing shorter is faithful. That is a fallback of last resort,
  not a default.

### Writing it down — offered, never automatic

This skill's read-only charter holds: **ask before writing anything.** When the answer is wanted on
disk, it goes to whichever fits:

- **`docs/org/<topic>.md`** when the decode falls inside an existing decode page's topic (weather,
  clutter, flightModel, tracers, puffer, sequences, aim-assist). Match those pages' contract —
  behaviour and constants, every claim naming the function it came from, no decompiler output
  reproduced.
- **`analysis/<slug>/FINDINGS.md`** when it does not — dated, with the method stated.

**In either case, also offer the one-line amendment to the `backlog.md` entry**: the number, its
address, and the pointer to the write-up. No date — `backlog.md` is live prose, so the date of the
decode belongs in the commit message that lands it. That line is load-bearing. Without it the next
`/backlog` on this item re-reads the stale footage number, re-flags it under **Traps**, and
re-recommends the decode that was already done.
