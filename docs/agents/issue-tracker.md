# Issue tracker: this repo's own markdown

Single-developer repo. The author's own work is tracked in committed markdown,
in three places with distinct roles. Public GitHub Issues are a second surface
that never joins this one: see "Public reports" below.

| File | Holds |
|---|---|
| `backlog.md` | **Unscheduled work.** Blocked/deferred items, feature backlog, open fidelity questions, the TUNE list. The default landing place for a new issue. |
| `docs/PLAN-<name>.md` | **The live plan** — scheduled work as a checklist. At most one exists at a time. A completed plan is deleted in its closing commit and read back from git; there is no archive directory. |
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
  There is no history file to append to.
- Tick the live plan's checklist and **swap** the "Current status" next-step
  pointer in `PROJECT_CONTEXT.md` — that section may only get shorter, never longer.
- A way a *measurement* can mislead → a transferable rule in `docs/verification.md`.
- A still-binding module constraint → a comment on the member it binds (prohibition first, reason
  second); format or decode knowledge → `docs/formats/` or `docs/org/`. Not the architecture entry.

## Triage state

The markdown files have no label mechanism. Record a role from
`triage-labels.md` inline in the entry (e.g. a `Status: needs-info` line) rather
than inventing a new file. A public GitHub issue carries real labels instead.

## Public reports (GitHub Issues)

Issues are on, and they are the only public surface: Discussions are off, blank
issues are on so a question has somewhere to go, and the policy a reporter reads
is in `.github/`.

| File | Holds |
|---|---|
| `.github/ISSUE_TEMPLATE/bug_report.yml` | The bug form. It requires the build version, the log file, the extraction state and the graphics driver, so a report can be identified without a round trip. |
| `.github/ISSUE_TEMPLATE/config.yml` | Blank issues on, and the private security-advisory link. |
| `.github/CONTRIBUTING.md` | What happens to a report, and what a pull request may touch. |
| `.github/SECURITY.md` | Scope, and the private reporting channel. |

**A public report is worked in its own issue and never enters the machinery
above.** It gets no `BL-`/`PT-`/`CAP-` id, is not copied into `backlog.md`, and
is not scheduled into a `docs/PLAN-*.md`; the issue thread is its whole record.
That is the promise `CONTRIBUTING.md` makes to the reporter, and it is what keeps
the internal list free to say things (dead ends, half-formed suspicions, tuning
arguments) that a reply to a stranger should not.

Do not restate an internal id in a public issue as though the reporter could
follow it. Say what will happen in the issue instead.

## PRs as a request surface

**On, narrowly.** Small self-contained pull requests are accepted for
`packaging/`, the extraction scripts, documentation and typo fixes; anything
under `CSVM/src` needs an issue first, because a contributor cannot run
`RunTests.ps1`'s golden tier against a retail install. The author's own commits
still go straight to `main`, so there is no internal PR queue.
`.github/CONTRIBUTING.md` is what a contributor reads.
