# Issue tracker: GitHub Issues

Issues for this repo live in GitHub Issues on `Laeresh/CSVM`. Use the `gh` CLI for all
operations; it infers the repo from `git remote -v` inside a clone.

New work goes there, internal and public alike. `playtest.md` is the record of what was filed
before the switch, and stays only until each entry closes.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body-file <file>`. Write the body to a
  file first; shell quoting of a multi-line body goes wrong in both PowerShell and Bash here,
  the same reason commit messages use `git commit -F`.
- **Read an issue**: `gh issue view <number> --comments`, plus `--json labels` when the state
  matters.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`
  with `--label` and `--state` filters as needed.
- **Comment on an issue**: `gh issue comment <number> --body-file <file>`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

## Category labels

Three labels replace the three private id prefixes:

| Label | Replaces | Holds |
|---|---|---|
| `backlog` | `BL-nnn` | **Unscheduled engine work.** Blocked/deferred items, feature backlog, open fidelity questions, TUNE entries. The default for a new issue. |
| `playtest` | `PT-nn` | **An owed at-the-controls check**: what to look for, the launch command, what it blocks. |
| `capture` | `CAP-nn` | **An owed capture of the original game**, naming the `backlog` issue it unblocks. |

An issue is cited by its number, `#123`, in commits, plans and other issues. Rejected fixes
and misleading instruments go in the issue body under a `⚠ Traps` heading. A `BL-NNN` cited in
the code, the docs or `playtest.md` is a closed item of the retired backlog file; its record is
in the commits `git log --grep=BL-NNN` finds. A public bug report from the form carries `bug`
and none of these three; it is worked in its own thread as before.

## Backlog labels

A `backlog` issue carries type and property labels from fixed vocabularies, so the tracker can
be filtered the way the backlog artifact is:

| Group | Labels |
|---|---|
| Theme | one `theme:<slug>`: `damage`, `weapons`, `flight`, `environment`, `effects`, `audio`, `cameras`, `hud`, `splitscreen`, `missions`, `tooling`, `misc`. The label's description is the theme's heading, spelled exactly; the artifact groups by it. |
| Type | one `type:<type>`: `bug`, `feature`, `research`, `tuning`, `cleanup`, `fidelity`, `perf`, `tooling`, `testing` |
| Size | `size:S`, `size:M`, `size:L` |
| Next step | `next:decode`, `next:data`, `next:code`, `next:look`, `next:decide` |
| Impact | `impact:high`, `impact:low`, `impact:none` |
| Evidence | `evidence:decoded`, `evidence:data`, `evidence:footage`, `evidence:spec`, `evidence:feel`, `evidence:trace` |
| Status | `owed-playtest`, `blocked` (the body names the blocker), `divergence`; none means open and unblocked |

What the values mean:

- **Theme** is the system you would open to fix it. Splitscreen is the one cross-cutting
  exception: an item whose subject is the single-viewer or single-player assumption takes
  `theme:splitscreen`, even though the fix opens another theme's system. `misc` is for items
  with no such system; if it grows past a handful, that is a missing theme.
- **Type**: `bug` is behaviour wrong against the original or against intent; `feature` is
  something the engine does not do yet; `research` delivers an answer, not code; `tuning` is a
  hand-tuned constant needing judgement at the controls (the set of `type:tuning` issues is the
  TUNE list); `cleanup` is debt with no player-visible change; `fidelity` is a decoded
  mechanism ours differs from, with no symptom yet; `perf` is frame time or memory; `tooling`
  is scripts, hooks and the verification loop; `testing` is a missing test or test aid.
- **Size** is the fix's shape, never hours: `S` is one file, one constant, a test-only or
  doc-only change, or a close with no code; `M` is one subsystem, a few files, or a change that
  re-pins goldens; `L` is plan-sized, cross-cutting, or needs a design first.
- **Next step** is what must happen before the item can move: `decode` reads the original
  executable; `data` reads extracted game data, footage or a measurement; `code` writes code or
  tests now with nothing owed first; `look` needs a human at the controls (every
  `owed-playtest` item and every capture blocker); `decide` needs a user decision.
- **Impact** is what a player would notice once it is done: `high` shows in ordinary play,
  `low` is subtle, rare or only visible when looked for, `none` is cleanup, tooling,
  instrumentation or an answer with no visible change.
- **Evidence** is the strongest source the body rests on, in order of trust: an executable
  decode, extracted data files, a capture under `OriginalScreenshots/`, the pre-release design
  spec (mechanism only, `docs/verification.md` SRC-3), or a feel report or estimate. `trace` is
  the case with no original-game source at all: a read of CSVM's own code, a log or a profile.
- **Status**: `owed-playtest` means the code or constant side is done and a human at the
  controls is missing; `blocked` means it cannot start regardless of priority (a capture, a
  milestone, another issue, an upstream release, a user decision).

Apply them when filing (`--label backlog,theme:audio,type:bug,...`) and change them when the body
changes what they say. A new theme is a new `theme:` label whose description is the new heading.
`/update-backlog-artifact` reads these labels and rejects a value outside the vocabularies.

## Scheduling

`docs/PLAN-<name>.md` is still the live plan, a checklist of scheduled work with at most one
existing at a time, deleted in its closing commit and read back from git. A checklist item
that came from an issue cites the issue number. Scaffold one with `/new-plan`.

## The entries filed before the switch

`playtest.md` holds the `PT-` and `CAP-` entries still open. Closing one deletes it, as
`PROJECT_CONTEXT.md` requires, and no new entry is added. Do not migrate them in bulk; an
entry moves to an issue only when it is reshaped or scheduled anyway, and then the entry is
deleted in the same commit and the issue body says which id it was. `New-ItemId.ps1` and
`CheckItemIds.ps1` keep guarding the old ids until the file is empty.

## When a skill says "publish to the issue tracker"

Create a GitHub issue with the fitting category label. If closing an issue would leave
follow-up work, that follow-up becomes its own issue with a `⚠ Traps` section, never a
comment on the closed one.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --comments`. If the user names a `PT-`/`CAP-` id instead,
read that entry of `playtest.md`, or the checklist item in the live `docs/PLAN-*.md`; a
`BL-` id is closed history, read with `git log --grep=<id>`.

## When work lands, the close-out is not optional

`PROJECT_CONTEXT.md` binds these, and they apply to skill output too:

- Close the issue (or delete the `playtest.md` entry) in the landing commit; a fixed item
  does not stay open.
- Record in the landing commit's message body: what landed, how verified, outcome, and the
  issue number so `git log --grep='#123'` finds it. There is no history file to append to.
- Tick the live plan's checklist and **swap** the "Current status" next-step pointer in
  `PROJECT_CONTEXT.md`; that section may only get shorter, never longer.
- A way a *measurement* can mislead → a transferable rule in `docs/verification.md`.
- A still-binding module constraint → a comment on the member it binds (prohibition first,
  reason second); format or decode knowledge → `docs/formats/` or `docs/org/`. Not the
  architecture entry.

## Triage state

Apply the label strings in `triage-labels.md` to the issue.

## Public reports

Issues are the only public surface: Discussions are off, blank issues are on so a question
has somewhere to go, and the policy a reporter reads is in `.github/`.

| File | Holds |
|---|---|
| `.github/ISSUE_TEMPLATE/bug_report.yml` | The bug form. It requires the build version, the log file, the extraction state and the graphics driver, so a report can be identified without a round trip. |
| `.github/ISSUE_TEMPLATE/config.yml` | Blank issues on, and the private security-advisory link. |
| `.github/CONTRIBUTING.md` | What happens to a report, and what a pull request may touch. |
| `.github/SECURITY.md` | Scope, and the private reporting channel. |

A public report is worked in its own issue and is not copied into a `backlog` issue; the
thread is its whole record, which is the promise `CONTRIBUTING.md` makes. Internal issues
may reference a public one by number when a fix covers both. The internal issues are
public too, so their bodies are written for a reader who did not run the session: no
session-only shorthand, no user-only recall stated as fact without saying so.

## PRs as a request surface

**PRs as a request surface: no.** `/triage` reads this flag. A pull request is a contribution,
not a feature request; a request goes to an issue.

Small self-contained pull requests are accepted for `packaging/`, the extraction scripts,
documentation and typo fixes; anything under `CSVM/src` needs an issue first, because a
contributor cannot run `RunTests.ps1`'s golden tier against a retail install. The author's
own commits still go straight to `main`, so there is no internal PR queue.
`.github/CONTRIBUTING.md` is what a contributor reads.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either;
resolve with `gh issue view 42` and fall back to `gh pr view 42`.
