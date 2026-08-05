# 📋 PLAN TEMPLATE — copy, don't edit in place

> **This file is the skeleton, not a plan.** The normal way to start a plan is the **`/new-plan`
> skill**, which writes a `docs/PLAN-<name>.md` pre-stubbed with the right sections. This file is
> that skill's source of truth, and is here to be read as the canonical structure. To scaffold by
> hand instead: copy it to **`docs/PLAN-<name>.md`** (note: `docs/` root, *not* `docs/plans/` — a
> plan there is *live*; `docs/plans/` is only for completed ones), fill in the `<…>` placeholders,
> and **delete every `<!-- guidance -->` comment and this banner**.
>
> **Naming:** milestone plans → `PLAN-M<n>-<name>.md`; feature/refactor plans → `PLAN-<name>.md`.
>
> **Sections are marked `[core]` or `[situational]`.** `[core]` sections belong in every plan, even
> a quick 3-item one. `[situational]` sections earn their place only when they apply — prior work
> was disproven, decisions were negotiated, or the plan rests on a reverse-engineering pass. When in
> doubt, keep `[core]`, cut `[situational]`.
>
> Two reference plans show the range: [`PLAN-M3-weapons.md`](PLAN-M3-weapons.md) is a heavy plan
> with every section; [`PLAN-M2-polish-4.md`](PLAN-M2-polish-4.md) is a routine run that carries the
> `[core]` sections and thins the `[situational]` ones into per-item notes.

---

<!-- ========================= THE PLAN STARTS HERE ========================= -->

# <Milestone N — short scope name, e.g. "Milestone 3 — Weapons and Destruction">

<!-- [core] Status + location banner. While the plan is live it sits in docs/ and says so; when it
     completes you move it to docs/plans/ and swap this for a ✅ COMPLETE banner (the skill handles
     the archive step — see /new-plan). This one-liner is what tells the next session the plan is live. -->
**ACTIVE PLAN** (written <YYYY-MM-DD>). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

<!-- [core] Scope paragraph(s). State what this plan delivers, and — just as important — what it
     deliberately excludes and why. If the plan draws items from backlog.md, record that EACH was
     re-verified still-open against BOTH the record (git log --grep, and pre-2026-08-06 entries in
     docs/HISTORY.md) AND the code (a backlog entry is not proof
     the work is undone). Convert any relative dates to absolute. -->
<One or two paragraphs: the goal, the explicit boundaries ("X is out of scope — it's M<n+1>"), and
any assumptions the plan rests on.>

## Milestone goal <!-- [core] -->

<What is true when this plan is done, as a few bullets. End with the boundary line — the thing this
plan pointedly does NOT do — so scope creep has a name to bump against.>

- <capability 1>
- <capability 2>

**<The boundary, stated as a rule.>** <Why it's deliberate.>

## Decisions (<YYYY-MM-DD>) <!-- [situational] — for plans where scope was negotiated (e.g. a grilling session). The table is the authority when the prose below contradicts itself. -->

| # | Question | Decision |
|---|---|---|
| 1 | <the open question> | **<the call made>** — <one-line rationale> |

## ⚠ Read this before implementing anything <!-- [situational] — include when prior work here was disproven, or the evidence is contested. The evidence-discipline RULE itself is core and lives in Ground rules; this section is the specifics that guard it. -->

<!-- If earlier readings (in this plan or a predecessor) were disproven, tabulate them so nobody
     re-derives them. This is not blame — it is the map of the minefield. -->

| # | The wrong claim | How it died |
|---|---|---|
| 1 | <the plausible-but-false reading> | <the data or playtest that killed it> |

<!-- If the evidence quality is uneven across items, grade it — this tells the implementer how to
     budget the session. Per-item confidence is already marked in each Evidence line (core); this
     summary table is the at-a-glance roll-up, worth it only on a large mixed-confidence plan. -->

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | <ids> | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | <ids> | The *what* is settled; the *how much* is TUNE — add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only — no mechanism yet** | <ids> | Budget for investigation; this may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. <Keep this line only if any item runs in a
parallel worktree.>

## What the data actually ships <!-- [situational] — the evidence survey, for plans that rest on a reverse-engineering pass. Census tables, key coverage, the canonical worked-example file. A routine plan folds this into each item's Evidence instead. -->

<The measured facts the plan is built on: counts, histograms, the canonical worked example file, the
exact fields/keys. Cite files by path. This is the shared evidence the per-item sections lean on.>

## Ground rules <!-- [core] — carried verbatim from every prior plan here (self-contained history). Keep as-is unless a rule genuinely doesn't apply. -->

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist <!-- [core] — the canonical task list. /commit-next reads this to find the next open item, so keep the ID + status shape exact. -->

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

<!-- IDs are wave-letter + a plan-global sequence number: Wave A = A1, A2, …; Wave B continues the
     number, B11, B12, …. The letter says which wave; the number is the stable cross-reference used
     in dependency notes ("B18 needs D29") and by /commit-next. A tiny single-wave plan is just
     A1…An. -->

### Wave A — <name> <!-- group items into waves by dependency/theme -->

1. ☐ <one-line item title>
2. ☐ <one-line item title>

### Wave B — <name>

11. ☐ <one-line item title>

## Dependency and parallelism notes <!-- [core] — what blocks what, what runs in parallel, which items contend on the same file (never run those in parallel worktrees); give each concurrent agent a stated file-ownership boundary. Collapses to a single line for a linear single-wave plan. -->

<Prose: "A1 blocks everything (all items read it). B11 → B12 is a chain. File contention: B16 and C23
both edit FILE — don't run them in parallel." For a linear plan: "Items run in listed order; no
parallelism.">

---

<!-- ========================= PER-ITEM DETAIL ========================= -->
<!-- One section per checklist item. The heading carries the ID + status + title so it's greppable
     and the status matches the checklist. The five bold sub-sections are [core]; ⚠ Traps is [core]
     the moment an item has a known pitfall. -->

# Wave A — <name>

## A1 ☐ <item title>

**Goal.** <One or two sentences: the observable outcome when this item is done — a behaviour, not an
implementation.>

**Evidence (confidence: <traced / direction-sound / lead-only>).** <The facts, cited to files and
line numbers. If it's a hypothesis, say so and name the instrument that will settle it. State what
you've already ruled out so it isn't re-chased.>

**Approach.** <How to do it, concretely — the files/functions to touch, the pattern to reuse. Name
what NOT to touch if that's a live risk.>

**Model recommendation.** <Which capability tier should execute this item — **low / medium /
high / max**, tool-agnostic so any agent can map it onto its own model roster — with a one-line why:
mechanical or exploratory work goes to a lower tier, judgement-heavy or high-blast-radius work to
a higher one. Optionally suffix a reasoning-effort override when the item clearly warrants one
(e.g. "medium, low effort" for a mechanical fan-out); omit it to inherit the session default —
don't invent a tier you can't justify. **Only recommend low for exploration**  —
even mechanical work here needs at least medium-tier judgement; use low *effort* on medium instead
when you want cheap execution.>

**Verify.** <The specific check that proves it: the exact capture/pose/command, plus the regression
surface. "An unchanged number is not evidence unless you've seen it able to fail" — take a baseline
first when the change is global.>

**⚠ Traps.** <[core once known] The misleading instruments, the rejected fixes and why, the values
that are TUNE-not-fact, the adjacent bug that must stay a separate change. This is where a future
session's wasted hour gets prevented.>
