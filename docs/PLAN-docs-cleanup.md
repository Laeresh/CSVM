# Documentation cleanup — shrink CLAUDE.md back to an index

`CLAUDE.md` has grown to **162 KB (~40k tokens)**, loaded into every session. It is supposed to be
"the compact, authoritative index of project context" (its own top block), but two sections are
**77%** of the file and both have become narrative logs rather than index entries. Along the way
several claims have gone stale and now contradict other parts of the same file.

This plan cuts it to **~30 KB** without losing a single unique fact, and adds a guardrail so it
cannot regrow. Statuses: ☐ open · ◐ in progress · ☑ done — keep the checklist in sync as items
land, per this repo's standing rule.

Ground rules carried over from every prior plan in this repo: **nothing is deleted until its
content is verified to exist elsewhere** (the `git show HEAD:CLAUDE.md` copy is not "elsewhere" —
a docs file is); a landed item gets a dated entry in `docs/HISTORY.md`; and the cleanup itself is
verified by re-reading the trimmed file cold and asking whether a fresh session could still do the
work.

**Measured growth rate.** While this plan was being written — a single conversation — CLAUDE.md
went **162,669 → 173,150 bytes, +10.5 KB (+6.4%)**, from one landed plan item plus one docs
commit. Nearly all of it appended to the two sections this plan is cutting. That is the case for
item 10: without a stated budget and a shape rule, the file returns to 162 KB within roughly a
dozen landed items and every session pays for it. Trimming without item 10 buys months, not a fix.

## Measured starting state (2026-07-22, re-measured after `docs/verification.md` landed)

| Section | Size | Share |
|---|---|---|
| Godot project (module index) | 66.1 KB | **38.7%** |
| Current status / next step | 65.1 KB | **38.1%** |
| User args | 14.7 KB | 8.6% |
| Repo layout | 12.2 KB | 7.2% |
| Format gotchas | 4.8 KB | 2.8% |
| Format support status | 3.9 KB | 2.3% |
| The 6 genuinely index-like sections | 4.2 KB | 2.5% |

The two target sections are now **76.8%** of the file and both grew. Targets below are unchanged.

Two findings drove the item order below:

- **The status section is duplicated history.** 25 of its 27 bullets are titled "landed / COMPLETE
  / delivered / fixed", and every one was checked against `docs/HISTORY.md` — all 25 already have
  their own dated entry there. Removing them is deletion, not migration. Two bullets alone
  (`Run 2 COMPLETE`, `M2.5 COMPLETE`) are 22 KB and describe finished work that already has both a
  PLAN doc and a HISTORY entry.
- **The module index cannot be blind-cut.** `docs/architecture.md` covers 60 of the 63 modules.
  `src/Flight/SpectatorCamera.cs`, `src/Mech3/AnimProgram.cs` and `src/Mech3/CompiledAnim.cs` have
  **no** architecture.md bullet — CLAUDE.md is their only documentation. That is why item 1 exists
  and gates item 3.

## What `docs/verification.md` changed (2026-07-22, commit `d0ad876`)

It landed mid-plan, and it helps more than it disturbs. It distilled ~90 verification traps out of
`HISTORY.md`/`CLAUDE.md`/`architecture.md` into one page and added a **sixth routing destination**
to the top block: *"a way a MEASUREMENT can mislead → `docs/verification.md`, as a transferable
rule."*

Three consequences:

- **It is the pattern this plan argues for, already proven.** Scattered content → one purpose-built
  page → a routing line in the top block so it does not scatter again. Items 4, 5 and 9 do exactly
  this for extraction, the CLI and the fork policy; item 10 generalises it. Cite `d0ad876` as
  precedent rather than re-arguing the approach.
- **It de-risks item 2.** The 25 landed status bullets are thick with verification narrative, and
  the original justification for deleting them was only "HISTORY has it" — true, but buried in a
  1,700-line log. The transferable half now has a permanent, *findable* home. Spot-checked: the
  re-measure-baselines rule, the measured noise floor, the vsync-cap floor, `--perf`'s ~2.2×
  `script` figure, occlusion, the `git stash` baseline trap, byte-identical checks, the C3 flipbook
  non-determinism and the random `--freecam` spawn are all present in `verification.md`.
- **Item 10 must extend the top block as it now stands, not as planned.** Six destinations, and
  the budget/shape rules are added alongside them.

**One thing to check during item 6:** whether "Format gotchas" and `verification.md` overlap. They
should not — Format gotchas are *data-format* traps (flat child indices, euler order, `cull_front`)
and verification.md holds *measurement* traps — but the `cull_front` entry carries both a format
fact and the byte-identical-viewer check that caught it, so the boundary needs one deliberate read.
Keep the format fact in CLAUDE.md and the measurement lesson in verification.md.

## Checklist

1. ☑ Backfill the 3 undocumented modules into `docs/architecture.md` **(done 2026-07-22 — `CompiledAnim.cs`, `AnimProgram.cs`, `SpectatorCamera.cs`; set-diff now 63/63 empty both ways, so item 3 is unblocked)**
2. ☑ Status section → current state + next step only **(done 2026-07-22 — 65,166 → 2,993 bytes; 27 landed bullets deleted after per-bullet HISTORY verification, three open items migrated to `backlog.md`)**
3. ☑ Module index → one line per module **(done 2026-07-22 — 66.2 KB → 8.4 KB over 63 entries, avg 136 chars; CLAUDE.md 110,222 → 52,677 bytes. Per-module diff against `architecture.md` first: 30 CLAUDE-only facts migrated there, +17 KB. Verified by the two-stage check: 697 distinct tokens, 0 absent from the docs corpus; open-claim hand-read clean.)**
4. ☑ Format support status → `docs/formats/extraction.md` **(done 2026-07-22 — CLAUDE.md 52,677 → 49,374 bytes; matrix + round-trip notes + the two-shapes table + the plane-data inventory moved, listed in `formats/README.md`. Four stale claims fixed en route: the `cam_anim` row's "Remaining: item 7 … skipped", `formats/README.md`'s "unsupported by mech3ax", `ExtractAssets.ps1`'s own docstring, and CLAUDE.md's "still reported+skipped" — the extractor *does* run mode `anim` and the animation engine *does* read the output. Also corrected "13 pages" → 17 and added the two pages the list had never carried.)**
5. ☑ User args → `docs/cli.md`, compact table stays **(done 2026-07-22 — CLAUDE.md 49,374 → 36,662 bytes; all 43 flag entries moved verbatim as bullets, so nothing was reworded. CLAUDE.md keeps the CLI-inversion rule (a behavioural fact, not a reference entry), a 14-row table of the day-to-day set, and the in-flight keys. Verified: all 47 distinct flag names present in `docs/cli.md`, 149/149 tokens present.)**
6. ☐ Fix the stale claims and internal contradictions
7. ☐ Repo layout: collapse the completed PLAN descriptions (−4 KB)
8. ☐ Archive the completed plans into `docs/plans/`
9. ☐ Retire `docs/upstream-pr/`; record the fork-maintenance policy that replaced it
10. ☐ Guardrail: size budget + "index, not narrative" rule

---

## 1. Backfill the 3 undocumented modules into `architecture.md`

**Goal:** make `architecture.md` genuinely complete, so item 3 can cut the index without
destroying documentation.

**Evidence:** a set-diff of module paths between the two files returns exactly three entries
present in CLAUDE.md and absent from architecture.md — `SpectatorCamera.cs`, `AnimProgram.cs`,
`CompiledAnim.cs`. All three are from the 2026-07-21 animation work, i.e. written after the
2026-07-18 split that created architecture.md. The reverse diff is empty.

**Approach:** write one architecture.md bullet for each, seeded from the CLAUDE.md text (which is
substantial for all three) plus a read of the module itself to pick up anything the index never
carried. Match the depth of the neighbouring bullets, not the index's.

**Verify:** re-run the set-diff; both directions empty.

## 2. Status section → current state + next step only

**Goal:** the section does what its own preamble says — "current state + next step only. It is not
a log."

**Evidence:** 25 of 27 bullets are landed work with a confirmed HISTORY.md counterpart. The
section currently contains **three** different "Next step:" declarations, one of which
("rework `PlanePainter` onto the real masks — deferred to its own session") is contradicted by the
bullet immediately following it, which says that rework landed.

**Approach:** replace the 27 bullets with a short block:

- where the project stands (Milestone 1 delivered; M2 core + both polish runs + M2.5 complete —
  three lines, not three pages, each pointing at its PLAN doc and HISTORY);
- **the** next step (single, unambiguous);
- open TUNE items pending playtest — this list is *not* in HISTORY and is genuinely current;
  move it to `backlog.md`, which already advertises itself as holding "the pointer to the TUNE
  list", and leave a pointer;
- known issues that are diagnosed but unscheduled (C5 ground z-fighting) → keep a one-liner
  pointing at `backlog.md`;
- the `backlog.md` pointer.

**Do not** delete anything until its HISTORY entry is confirmed by name. The 25/25 check has been
done once for this plan; redo it per-bullet at execution time, since HISTORY is append-only and
may have drifted.

**Verify:** every deleted bullet's subject is greppable in `docs/HISTORY.md`; the section is
under 3 KB and contains exactly one "next step".

## 3. Module index → one line per module

**Goal:** restore the "compact index" the top block promises — path, one-sentence role, and the
pointer to `architecture.md` that already exists.

**Evidence:** 63 bullets, 64.8 KB, averaging 1030 chars. The distribution proves the drift on its
own: `SoundDefs.cs` 103 chars and `WavFile.cs` 112 (real index entries) against `SceneBuilder.cs`
5367, `AnimRuntime.cs` 4701 and `PlaneViewer.cs` 4017 (essays). The header already tells the
reader where detail lives: "deep implementation notes … live in `docs/architecture.md`; read that
module's bullet before changing it."

**Approach:** one line per module, ~120 chars, in the shape the small bullets already use. Work
module by module, and for each one **diff the CLAUDE.md bullet against the architecture.md bullet
first** — CLAUDE.md is newer in places (it carries `WorldSounds.cs`, the `texture_scroll` material-
cache-key rule, and the 2026-07-21 billboard/facade findings). Anything the index knows and
architecture.md does not moves *into* architecture.md before the line is trimmed. This is the
slowest item in the plan and the only one that can lose information; budget it accordingly.

**Verify** — use the two-stage check item 2 arrived at, because the weak version is genuinely
misleading:

1. **Token check (automated, cheap).** Extract the distinctive tokens from each deleted span —
   backticked identifiers, paths, and multi-digit numbers — and confirm each appears somewhere in
   the docs corpus (`docs/**/*.md` + `backlog.md`, not HISTORY alone; item 2 found 14 of 450
   tokens "missing" from HISTORY that were correctly living in `formats/rof.md`, `formats/
   strings.md` and `backlog.md`). Target: zero absent.
2. **Hand-read for open claims (the one that actually matters).** The token check **cannot** catch
   the failure mode that nearly bit item 2: three items — the TUNE list, the owed playtests, and
   C3's missing `cloud1`/`cloud2` — had every token present elsewhere while the *claim that they
   were still open work* existed nowhere but the text being deleted. Token presence proves a fact
   is written down; it does not prove the same claim is made. So read each bullet for statements
   of the form "still open / not yet / pending / left to the user" and re-home those by hand
   before deleting.

Plus: index under 9 KB, and `git diff` reviewed bullet-by-bullet rather than in bulk.

## 4. Format support status → `docs/formats/extraction.md`

**Goal:** stop paying for mech3ax's internals every session. Extraction is done; what a session
needs is *that* it works and where the output lands, not how each ZBD type round-trips.

**Evidence:** user's call, and the section is actively stale — its `cam_anim`/`mis_anim` row
history and the Run-2-item-9 text elsewhere in the file ("`cam_anim.zbd`, which mech3ax doesn't
extract — waits for a mech3ax extension") are contradicted by the fork, which extracts it, and by
the landed animation playback that drives the train from that data.

**Approach:** move the support matrix, the round-trip verification notes and the extracted-plane-
data paragraph into a new `docs/formats/extraction.md`; fix the stale rows during the move; add it
to the `docs/formats/README.md` index. CLAUDE.md keeps two or three lines: extraction is complete,
`extracted/` is fork-produced, both extraction shapes are readable, pointer.

**Verify:** `docs/formats/README.md` lists the new page; no claim in CLAUDE.md about a ZBD type's
round-trip status survives outside it.

## 5. User args → `docs/cli.md`

**Goal:** keep the flags used constantly, move the long tail.

**Approach:** full per-flag prose to `docs/cli.md`. CLAUDE.md keeps a compact table (~4 KB) of the
day-to-day set — `--fly`, `--viewer`, `--stunt`, `--freecam`, `--chapter`, `--plane`, `--players`,
`--screenshot`/`--frames`/`--shots`, `--debug-anim`, `--perf`, `--no-pads`, `--mute` — plus the
CLI-inversion rule (flight is the default; `--viewer` asks for the static view) which is a
behavioural fact, not a reference entry, and must not move.

**Verify:** every flag in the old section appears in `docs/cli.md`; the scripted commands used for
screenshot verification still resolve against the table or the new page.

## 6. Fix the stale claims and internal contradictions

**Goal:** no two places in the repo disagree about the same fact.

**Evidence — confirmed against the code and the files:**

| Claim | Location | Reality |
|---|---|---|
| Paint regions are "a hand-authored hue-window table — **superseded but not yet reworked**" | Format gotchas | `PlanePainter.cs:24`: "This **replaced** a hand-authored table of per-aircraft hue windows". Landed 2026-07-20 with `PatternLibrary.cs`. |
| "the paint region table is hand-authored rather than extracted" | `LiveryLab.cs` index bullet ("why it exists") | Same — stale for the same reason. |
| "`cam_anim.zbd`, which mech3ax doesn't extract — waits for a mech3ax extension" | Run-2 item 9 text | The fork extracts it; playback landed. |
| "**Next step:** rework `PlanePainter` onto the real masks — deferred to its own session" | `.rof` status bullet | The next bullet says it landed. |
| "**All four items are landed. This plan is complete.**" while the checklist marked item 2 `◐` | `PLAN-anim-rendering-followups.md:24` | **Self-resolved 2026-07-22** — item 2 landed, making the line true. Fixed in place, along with the plan's stale *present-tense* body, now banner-marked as history. |
| "that plan is now complete, all four items done" | CLAUDE.md `texture_scroll` bullet | Now correct, but CLAUDE.md's Repo-layout entry still says item 2 "is all that is left of this plan" — **that one is now the stale half** and is fixed by item 7. |

Note that the last two are why this plan initially mis-read that plan as complete. Fix the plan
file's summary line as part of this item.

**Approach:** correct each in place. Where a fact is stated in more than one file, pick the one
authoritative home (per the top block's routing rules) and make the others point at it.

**Verify:** grep for `hue-window`, `not yet reworked`, `doesn't extract`, `deferred to its own
session` — every surviving hit is a historical statement inside `docs/HISTORY.md`, where past
tense is correct.

## 7. Repo layout: collapse the completed PLAN descriptions

**Goal:** Repo layout describes the *repo*, not the contents of six finished plans.

**Approach:** one line each — name, what it covered, status, date. The detail is in the plan files
and in HISTORY.

**Verify:** section under 7 KB; each plan still discoverable by name.

## 8. Archive the completed plans into `docs/plans/`

**Goal:** make the active plan obvious.

**Evidence — corrected checklist audit** (the first pass over-counted, because the legend line
`☑ done` matches a naive `☑` grep; count only `^[0-9]+\.` entries):

| Plan | Items | Status |
|---|---|---|
| `PLAN-M2-polish.md` | 8/8 ☑ | complete → archive |
| `PLAN-M2-polish-2.md` | 13/13 ☑ (item 9 deferred to backlog by decision) | complete → archive |
| `PLAN-M2.5-prototype.md` | 7/7 ☑ | complete → archive |
| `PLAN-anim-playback.md` | 7/7 ☑ | complete → archive |
| `PLAN-anim-rendering-followups.md` | 4/4 ☑ (item 2 landed 2026-07-22) | complete → archive |
| `PLAN-mech3ax-cs-revival.md` | item 14 resolved 2026-07-22 | complete → archive |

**All six plans are now complete**, so `docs/` is left holding no active plan but this one.
That is the correct state to arrive at — it makes the next plan, whenever it is written, the
obvious single active document — but it also means the archive move is all-or-nothing and the
"which plan is live?" signal now rests entirely on what is *not* in `docs/plans/`.

**Item 14 is cleared (user, 2026-07-22).** The user spoke with the mech3ax developer: upstream
removed Crimson Skies support because **they could not maintain it** — which answers the question
`pr-0-discussion.md` was drafted to ask (bandwidth, not architecture). The agreed outcome is that
the CS work **stays in the user's fork** (`Laeresh/mech3ax`), and upstream commits get merged
*into* the fork if and when they appear. Upstream is dormant: its latest commit is `cbb838f`
(rc3, 2025-11-17) and `upstream/main` is currently **0 commits ahead** of the fork's `main`.

Mark item 14 `☑ resolved by decision — CS support stays in the fork; see item 9`, and archive the
plan with the other four.

**Approach:** move the four complete plans to `docs/plans/`, each with a `**COMPLETE — <date>.**`
banner under the title. Update every reference by path — `CLAUDE.md`, `docs/HISTORY.md` and
`backlog.md` all cite `docs/PLAN-*.md`. This is the one item that breaks links, so grep first.

**Verify:** `grep -rn "docs/PLAN-" ` returns no path that does not exist.

## 9. Retire `docs/upstream-pr/`; record the fork-maintenance policy

**Goal:** the whole `docs/upstream-pr/` package (4 files, 22 KB) was written on a premise that no
longer holds — that these branches get opened as PRs against TerranMechworks. Replace it with the
policy that actually governs the fork now.

**Evidence:** user decision 2026-07-22 (see item 8). Upstream removed CS support for maintenance
reasons and is dormant (`cbb838f`, rc3, 2025-11-17; `upstream/main` 0 ahead of fork `main`). The
fork already has the right plumbing: `origin` = `git@github.com:Laeresh/mech3ax.git`, `upstream` =
`https://github.com/TerranMechworks/mech3ax.git`, with `pr-cs-anim` and `pr-cs-gamez` both pushed.

**Approach:**

- Move `docs/upstream-pr/` to `docs/plans/upstream-pr/` and add a status banner recording the
  outcome: what was prepared, why it was not pursued, and the conversation that settled it. The PR
  bodies are **not** deleted — they are the best existing description of what each branch contains,
  which is exactly what a future upstream revival (or a fork reader) would need.
- Write the replacement as a short **fork maintenance** section — the sync procedure
  (`git fetch upstream && git merge upstream/main` into the fork, then rebuild the release binary
  the extraction pipeline uses), the branch roles (`cs-anim` = integration/build, `pr-cs-*` = the
  split-by-concern branches, `main` = upstream mirror), and the fact that upstream is dormant so
  this is a check-occasionally, not a routine.
- **Preserve the standing disclosure rule.** "Every outward-facing communication about this work
  discloses that it was done with the help of Claude Code" currently lives inside the
  `docs/upstream-pr/` bullet, which is about to stop existing. It is a project-wide rule and must
  move to the top block of `CLAUDE.md`, not vanish with its host.
- Rewrite CLAUDE.md's `docs/upstream-pr/` Repo-layout entry (currently "the **ready-to-open**
  upstream contribution package" plus a held-PR status) to one line pointing at the archive.

**Risk surfaced while checking this — now resolved (2026-07-22).** `cs-anim`, which CLAUDE.md
records as what the local release `unzbd.exe` is built from, existed only on this machine: 5
commits, unpushed. Acceptable while the fork was a PR staging area, but under the new policy the
fork *is* the home of this work, so the whole extraction pipeline rested on an unbacked local
branch. The user pushed it; `origin/cs-anim` now exists and matches local. The branch-roles note
this item writes should state that all three CS branches are on `origin`, so the fork is the
authoritative copy rather than this workstation.

**Verify:** no file claims a PR is pending or held; the disclosure rule is greppable in CLAUDE.md's
top block; the sync procedure names the actual remotes.

## 10. Guardrail: size budget + "index, not narrative"

**Goal:** stop the regrowth. This is the item that makes the other eight stick — the file grew to
162 KB precisely because the standing rule says to update it in the same turn as each change, and
every session appended without anyone owning the total.

**Precedent:** `docs/verification.md` (commit `d0ad876`, 2026-07-22) already did the routing half
of this — it added a sixth destination to the top block so that measurement traps stop landing only
in dated entries. That approach is settled; this item adds the two rules it does not cover, and
must extend the block **as it now stands (six destinations)**.

**Approach:** extend the top block ("⚠ Keep this file + `docs/` up to date") with:

- a stated budget — **CLAUDE.md stays under ~35 KB**; if a change would exceed it, the content
  belongs in `docs/` and the index gets a pointer instead;
- an explicit shape rule — a module index entry is **one line**; if it needs a second sentence it
  is an `architecture.md` edit, not a CLAUDE.md edit;
- the status-section rule restated as a hard one — landed work goes to `docs/HISTORY.md` and is
  **removed** from "Current status", not summarised there as well.

**Verify:** re-read the trimmed CLAUDE.md cold and confirm a fresh session could still find its
way to every subsystem; check the byte count against the budget.

---

## Risk notes

- **Item 3 is the only one that can lose information**, because CLAUDE.md is newer than
  architecture.md in several places. It is gated on item 1 and requires a per-module diff, not a
  bulk cut.
- **Items 8 and 9 break paths.** `docs/PLAN-*.md` and `docs/upstream-pr/*` are cited from
  `CLAUDE.md`, `docs/HISTORY.md` and `backlog.md`. Grep every reference before moving.
- **Item 9 must not drop the disclosure rule** — it is project-wide but currently lives inside the
  bullet being retired.
- Items 2, 4, 5, 7 are near-zero risk: the content is either duplicated already or moving intact.
- No code changes anywhere in this plan, so the usual 8-chapter regression is not required. The
  meaningful verification is the cold re-read in item 9.
