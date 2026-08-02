---
name: close-backlog-item
description: Close a resolved backlog.md item (and any playtest.md capture it owned) — retire the IDs, log the outcome in docs/HISTORY.md, and strike the caveat everywhere it was restated. Use when a BL-NNN is done, answered, superseded, or dropped.
---

Retire one `backlog.md` item now that it is settled. The work is bookkeeping, and the whole risk is
**leaving the dead claim alive somewhere else** — an entry's doubt is usually restated in a format
doc and in a code comment, neither of which cites the `BL-NNN`, so neither turns up if you only grep
the ID.

This skill edits docs and comments. It does **not** implement anything, and it does **not** commit
unless asked.

## 1. Resolve the item and its closure kind

Argument may be `BL-238`, a bare `238`, or a phrase. Same resolution rule as `/backlog`: an exact ID
match wins; a phrase or missing ID means **ask**, never guess. A missing ID means the item was
already deleted — say so rather than offering a near-numbered substitute.

Read the entry and its enclosing section heading before touching anything. Then name which kind of
close this is, because it changes what the history entry has to say:

- **Fixed** — code landed. The entry's *How you'd know it worked* line must have actually been run.
- **Answered** — an open question settled by a capture, a measurement, or the user's call. Nothing
  in the build changes; what changes is that a constant/decode stops being provisional.
- **Superseded** — another item swallowed it. Name the survivor.
- **Won't do** — out of scope by decision. Record the reason, not just the verdict.

⚠ **The user's decision is authoritative, but the evidence still gets written down.** If they close
an item on a partial measurement, close it — and record in the history entry both what the evidence
does establish and the hypothesis it cannot exclude, plus why that residue does not matter. An
undocumented close reads months later as an unexplained disappearance.

## 2. Delete the entry from `backlog.md`

Delete it outright. **Do not** mark it DONE, strike it through, or move it to a "closed" section —
there is no such section, and the file is the live list. IDs are assigned once and never reused or
renumbered, so the gap is the record.

Then check what the deletion breaks:

- **Sibling entries that cite the ID.** Grep it. A citation inside a *narrative* ("split out of
  `BL-236`, now landed") is history and stays. A citation that sends a future reader to go *read*
  the deleted entry for evidence or traps must be rewritten to carry the fact itself or point at
  the `docs/HISTORY.md` entry instead.
- **A `*Playtest after fix:*` line** on the entry means an owed test that may now be actionable —
  if the fix landed and the test was not run, it moves to `playtest.md` rather than vanishing.

## 3. Retire what the item owned in `playtest.md`

If a `CAP-nn`/`PT-nn` existed **solely** for this item, delete it — the row, its section heading if
that leaves the section empty, and any ⚠ prose block written for it. IDs retire with the item and
are never reused; gaps are expected.

If it unblocks other `BL-NNN`s too, **keep it** and only drop this item from its Unblocks column.

Captures staged under `playtest/<ID>/` are git-ignored and **not** swept by `CleanScratch.ps1` —
delete the folder with the item, or say you left it.

## 4. Log the outcome in `docs/HISTORY.md`

**Append a dated `##` entry at the bottom** (`## YYYY-MM-DD — <what is now true>`). History is a
chronological record: never rewrite an old entry to make it retroactively correct.

Cover, in prose: what settled it and how it was measured or decided; what that rules out; the honest
limit of the evidence and why it does not reopen the question; what changed in the build (often
"nothing — the constant it confirms was already shipping") and whether goldens moved.

⚠ **Staple a forward-pointer onto the older entry that filed the item**, in bold, in place — a
reader who lands on the original caveat must not stop there believing it still stands. This is the
one edit to old history that is allowed, because it adds a pointer rather than revising the account.

## 5. Strike the caveat where it was *restated* — the step that gets missed

Grep the ID first, then grep the **wording of the doubt** (a distinctive phrase from the entry: a
symbol name, `not a decode`, `TODO`, the TUNE constant's name). Doc bullets and code comments
routinely restate an open question in full without ever naming the `BL-NNN`, and those are what
survive an ID-only sweep.

Everywhere it turns up — `docs/formats/*.md`, `docs/architecture.md`, `docs/verification.md`,
`CLAUDE.md`/`PROJECT_CONTEXT.md`, and XML doc comments in `CSVM/src` — replace the hedge with the
settled fact and the evidence that settled it. Keep any ⚠ that is still true (a trap about *how* the
mechanism works outlives the question of whether it was right).

Finish with a grep of the ID across the repo: the only hits left should be in `docs/HISTORY.md`.

## 6. Verify and report

- Touched a code comment → `dotnet build CSVM/CSVM.sln`. Clean build, zero warnings (StyleCop is
  hook-enforced anyway).
- Touched code, not just comments → `.\RunTests.ps1`, and quote the real result.
- Docs only → no build, and say so rather than implying one ran.

Report: the closure kind and what settled it, the files touched with a one-line why each, the
verification actually run, and the residual limit if there is one. Then stop — **do not commit**
unless the user asks; offer it in one line.
