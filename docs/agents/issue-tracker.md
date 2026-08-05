# Issue tracker: this repo's own markdown

Single-developer repo, no PR workflow, GitHub Issues unused (`gh` is not
installed). Work is tracked in committed markdown, in three places with
distinct roles:

| File | Holds |
|---|---|
| `backlog.md` | **Unscheduled work.** Blocked/deferred items, feature backlog, open fidelity questions, the TUNE list. The default landing place for a new issue. |
| `docs/PLAN-<name>.md` | **The live plan** — scheduled work as a checklist. At most one exists at a time; there is none right now. `docs/plans/` is the *archive* of completed plans, banner-marked `COMPLETE` — read-only history, never file into it. |
| `playtest.md` | **Owed at-the-controls checks** — the user-only playtest/TUNE list: what to look for, the launch command, what it blocks. |

## When a skill says "publish to the issue tracker"

Append an entry to `backlog.md` in the style of its neighbours. If closing it
would leave follow-up work, that follow-up becomes its **own new entry** with a
`⚠ Traps` section naming rejected fixes and misleading instruments — see the
`backlog.md` rule in `PROJECT_CONTEXT.md`.

To schedule a batch of entries instead, scaffold a plan with `/new-plan`, which
writes `docs/PLAN-<name>.md`.

## When a skill says "fetch the relevant ticket"

Read the named section of `backlog.md`, or the checklist item in the live
`docs/PLAN-*.md`. The user will normally name the item.

## When work lands — the close-out is not optional

`PROJECT_CONTEXT.md` binds these, and they apply to skill output too:

- **Delete the `backlog.md` entry.** A `FIXED`/closed entry does not stay there.
- Record in the landing commit's message body: what landed, how verified, outcome.
  (`docs/HISTORY.md` is frozen 2026-08-06 — never append to it.)
- Tick the live plan's checklist and **swap** the "Current status" next-step
  pointer in `PROJECT_CONTEXT.md` — that section may only get shorter, never longer.
- A way a *measurement* can mislead → a transferable rule in `docs/verification.md`.
- A still-binding module constraint → a `⚠` one-liner in `docs/architecture.md`.

## Triage state

There is no label mechanism. Record a role from `triage-labels.md` inline in the
entry (e.g. a `Status: needs-info` line) rather than inventing a new file.

## PRs as a request surface

**Off.** Commits go straight to `main`; there is no PR queue to triage.
