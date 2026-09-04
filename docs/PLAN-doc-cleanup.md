# Doc cleanup: split architecture.md, trim to cap, prune verification.md, fix stale index facts

**ACTIVE PLAN** (written 2026-09-04). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The four entry docs (`CLAUDE.md`, `PROJECT_CONTEXT.md`, `docs/verification.md`,
`docs/architecture.md`) have drifted from the rules they state. This plan brings them back to
those rules: `docs/architecture.md` is split per namespace and every entry cut to its stated cap,
`docs/verification.md` loses the rules nothing cites and every remaining rule takes the stated
shape, and the index files (`PROJECT_CONTEXT.md`, `CLAUDE.md`, `AGENTS.md`, `AGENTS.override.md`,
`docs/tooling.md`) have their stale facts, dates and repeated rules corrected. The measurements
the plan rests on are in "What the data actually ships" below; the executing session does not
re-measure them.

`docs/tooling.md` and `docs/cli.md` are in scope too (Wave F): both are pointed at from the
entry docs as the description of record for the scripts and the flags, and both have grown the
same measurement narrative the entry docs have. Out of scope: `docs/org/` and `docs/formats/`
content beyond what a relocated constraint adds, and any change to code semantics. Comment
relocations into `CSVM/src` are the only code edits, and they change no behaviour.

## Milestone goal

- `docs/architecture.md` is a routing index of one line per module; the entries live in
  `docs/architecture/<Namespace>.md`, every entry within its cap, no `⚠` trap, no item id or date
  as provenance, and every `.cs` under `CSVM/src` has an index line and an entry.
- A repo check, `CheckDocEntries.ps1`, enforces the architecture caps and coverage and the
  `docs/cli.md` bullet cap, and runs in the commit content gate.
- `docs/cli.md`'s flag bullets say what a flag does, its argument shape and the modes it applies
  to, within a cap; `docs/tooling.md` says what each script does, its switches and where its output
  lands, with the measurements that justified a default gone to the commit record.
- `docs/verification.md` holds only rules something cites, each in the shape "bold imperative plus
  at most one evidence sentence", with no dangling rule reference anywhere in the repo.
- The index files state no wrong count, no date, no phantom path, and each shared rule once.

**Nothing still binding is dropped.** A constraint leaving an entry goes to exactly one new home
(a comment on the member it binds, the module's `docs/org/` or `docs/formats/` page, or a
verification rule); a deletion without a home is allowed only for history, refuted readings and
claims the target page already carries.

## Decisions (2026-09-04)

| # | Question | Decision |
|---|---|---|
| 1 | Split scheme for architecture.md | **Per namespace under `docs/architecture/`**, index stays at `docs/architecture.md`. Code comments that say "this module's entry in docs/architecture.md" stay valid through the index and are not rewritten. |
| 2 | How far to trim | **Every entry to the cap**, not only the giants. Highest-traffic modules may use 12 lines, the rest 8. |
| 3 | Uncited verification rules | **Deleted**; IDs stay retired gaps. Cited one-liners stay; paragraph rules are cut to shape. |
| 4 | `docs/tooling.md` and `docs/cli.md` | **Included** (Wave F): a 600-character cap per flag bullet, 12 lines for the three lab sections, and tooling.md cut to what each script does and how it is driven. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `docs/architecture.md` is "~110 KB" (PROJECT_CONTEXT, AGENTS.override) or "~190 KB" (its own header) | `Get-Item` reads 780,423 bytes, 8,382 lines. |
| 2 | The module map's namespace counts in PROJECT_CONTEXT | Counting `.cs` files: Flight 123 not 34, Testing 98 not 6, UI 70 not 16, Session 51 not 11, Mech3 65 not 40, Utils 19 not 9, Bindings 21 not 16, Effects 5 not 4. Only "root (3)" is right. |
| 3 | "16 pinned shots" (PROJECT_CONTEXT, tooling) and "11 goldens" (tooling) | `analysis/goldens/manifest.json` has 18. |
| 4 | `Z:\Crimson Skies` as the worktree example for `CSVM_DATA_ROOT` in tooling.md | The directory does not exist; the tree is `Z:\CSVM`. |
| 5 | `/setup-matt-pocock-skills` wrote `.claude/skills/` (PROJECT_CONTEXT, CLAUDE.md) | Not an invocable skill in this repo; the skills there are hand-authored. |
| 6 | WORLD-1, WORLD-6, WORLD-13, SHELL-1, SHELL-5, SHELL-9 are verification rules | Cited by `docs/formats/gotchas.md`, `docs/formats/gamez.md`, `analysis/surface-classification/FINDINGS.md`, `docs/cli.md`, `docs/tooling.md`; none is defined in `docs/verification.md`. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees; never use it in a
worktree session here. Use a local commit or a file copy. Other sessions commit to `main`
mid-work: put a pathspec on every `git add` and `git commit`, and re-check ids after any merge.

## What the data actually ships

`docs/architecture.md`, measured by splitting on `^## ` headings:

| Measure | Value |
|---|---|
| Headings | 351 (347 `## src/...`, plus Module index, Cross-module conventions, Rendering: enhanced graphics mode, CSVM.Tests/) |
| Entry body lines: median / mean | 14 / 24.8 |
| Entries over 12 lines / over 30 / over 100 | 204 / 67 / 11 |
| `## Module index` section | 509 lines, 112 KB (bullets of 5 to 10 sentences) |
| `⚠` in entry bodies | about 100 |
| `BL-nnn` in bodies | 108; `PLAN-` 3; dates 9; "used to" 7; "no longer" 9; "retired" 8 |
| Entries per namespace | Flight 108, UI 78, Mech3 62, Session 40, Bindings 17, Testing 17, Utils 17, Effects 5, root 3 |

The eleven entries over 100 lines: `Flight/Projectile.cs` 415, `Flight/FlightController.cs` 282,
`Session/Launcher.cs` 168, `UI/LaunchMenu.cs` 165, `Mech3/AnimRuntime.cs` 164,
`Session/CampaignDirector.cs` 144, `Session/CutsceneController.cs` 140, `Testing/*Suites.cs` 135,
`UI/Menu/InstantActionPresets.cs` 121, `Session/WorldEffectsFactory.cs` 114.

Source files with no heading, no index bullet and no mention: `Bindings/ICaptureDevices.cs`,
`Bindings/SeatCaptureDevices.cs`, `Flight/CockpitGauges.cs`, `Flight/CollisionDamage.cs`,
`Flight/HudFontTest.cs`, `Flight/ResultsBoard.cs`, `Flight/ScreenSize.cs`, `Flight/StickRamp.cs`,
`Flight/StuntSplits.cs`, `Mech3/MilitiaPaint.cs`, `Session/AirframeSwap.cs`,
`Session/CampaignDangerZones.cs`, `Session/ZeppelinRuntime.Cannons.cs`, `UI/HudLayers.cs`,
`UI/PlaneFit.cs`, `UI/PlaneNameTables.cs`, `UI/PlaneRatings.cs`, `UI/Menu/BriefingObjectives.cs`,
`UI/Menu/BriefingScript.cs`, `Utils/HoldToRepeat.cs`, `Utils/PhysicsTickCost.cs`. The
`Testing/*Suites.cs` files (89) share one wildcard heading and `Mech3/Anim/*.cs` (10) share the
`## src/Mech3/Anim/` entry; both groupings stay.

`docs/verification.md`: 225 rules. 130 are bare one-line maxims, 95 carry an evidence paragraph.
Citation census (repo files outside the file itself, plus every commit message, word-boundary
matched) found 46 rules cited by nothing:

`METHOD-13, METHOD-19, METHOD-26, DIAG-4, DIAG-5, DIAG-7, DIAG-9, DIAG-12, DIAG-14, DIAG-16,
SHOT-4, SHOT-5, SHOT-7, SHOT-13, SHOT-25, SHOT-26, GOLD-7, DET-1, DET-4, DET-5, PERF-4, PERF-6,
PERF-10, LOG-3, LOG-4, LOG-6, LOG-9, LOG-10, LOG-11, LOG-14, LOG-15, WORLD-14, WORLD-31, WORLD-33,
SHELL-4, SHELL-6, SHELL-11, INSTR-1, INSTR-2, INSTR-4, INSTR-23, INSTR-29, INSTR-32, INSTR-34,
SRC-1, SRC-4`

Most cited: METHOD-9 (21 files, 35 commits), METHOD-10, DET-12, GOLD-9, DET-11, PERF-21,
INSTR-38, DET-8, PERF-1, GOLD-5, PERF-12, SHELL-10. Out of numeric order: GOLD-7 after GOLD-11,
INSTR-7 after INSTR-11, INSTR-15 before INSTR-14, SHELL-15 before SHELL-14. SHOT-24/25/26 carry
"renumbered at the merge" notes. SHELL-12 cites `SessionSpec.cs:586`; the property is at 507 and
the parse at 1151. Every other identifier the rules cite resolves.

`docs/cli.md` (186 KB, 550 lines): the `## Flags` section is 167 KB. 152 flag bullets, median 891
characters; 102 over 600, 36 over 1500, 7 over 3000. Largest: `--menu` 6854, `--run-tests` 5185,
`--view` 4533, `--ai` 3288, `--anim-lab` 3279, `--weapon-lab` 3143, `--pos` 3142, `--tex-census`
2985, `--destroy` 2696, `--perf` 2436. Three lab sections (world damage lab 4.2 KB, node lab
3.8 KB, shared selection 3.0 KB). The bullets carry measured startup costs, "verified by md5"
notes, the crash history of a lab, and rejected designs beside the flag's behaviour.

`docs/tooling.md` (44 KB, 78 paragraphs): twelve paragraphs over 1000 characters, all measurement
narrative: the `RunTests.ps1` stage table (2.6 KB), the rtexture tier investigation (1.6 KB), the
perf stage's metric-by-metric refusal list (1.5 KB), the `test-report.json` schema (1.5 KB), the
budget derivation with its three timings (1.5 KB), the golden-worker A/B (1.3 KB), the hidden
desktop's 700-sample probe (in the window-focus section), and the hitch stage's rationale (1.3 KB).
The budget rule, the shard-isolation rule and the vsync rule are already verification rules
(PERF-18, LOG-13, PERF-13) and are restated here in full.

Repeated rules across the index files: the "over budget is awareness only" rule is stated in
`CLAUDE.md`, `AGENTS.md`, twice in `PROJECT_CONTEXT.md` and twice in `docs/tooling.md`; the
skills junction in `CLAUDE.md`, `AGENTS.md` and `PROJECT_CONTEXT.md`; the verification-loop
paragraph verbatim in `CLAUDE.md` and `AGENTS.md`. Dates in live prose: `PROJECT_CONTEXT.md`
lines 27, 39, 213; `docs/tooling.md` lines 28, 64, 96, 399, 407, 471, 475 to 476. The banned
phrase "load-bearing" is at `docs/tooling.md:448`.

## Ground rules

- **Docs state what is.** No dates, no "was / used to / now / retired / landed", no `BL-`, `PLAN-`
  or item id as provenance in live prose. Removed narrative goes in the commit message body and
  nowhere else. An identifier inside an evidence sentence (a suite name, an analysis directory, the
  item whose measurement it is) is evidence, not provenance, and may stay.
- **Edit repo text only with the Read/Edit/Write tools.** PowerShell 5.1 mojibakes UTF-8. A script
  that writes a repo file uses `[IO.File]` with `UTF8Encoding($false)` and is itself pure ASCII.
- **A constraint leaving an entry gets exactly one new home**: a comment on the member it binds
  (under `CheckCommentCaps.ps1`'s caps, prohibition first, reason second), the module's
  `docs/org/` or `docs/formats/` page, or a rule in `docs/verification.md` in the documented shape.
  Before deleting evidence, check the target page already carries the claim; write it there in the
  same commit if it does not and the constant or decision still exists in code.
- **Every commit**: message written to a file with the Write tool, then `git commit -F`; pathspec
  on `git add` and `git commit`; `.\CheckEncoding.ps1` and `.\CheckCommentCaps.ps1 -Summary` run by
  hand first. The content gate checks every worktree, so a sibling worktree can block a commit; the
  message names which.
- **Rule IDs in `docs/verification.md` are permanent.** Deleting leaves a gap; never renumber.
- **`PROJECT_CONTEXT.md` stays under 35 KB** (32.9 KB at the start) and its "Current status" only
  swaps the next-item pointer as items land.
- **Read `docs/verification.md` before measuring anything.** For this plan that is the split
  verification and the golden stage after the comment relocations.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A: stale facts in the index files

1. ☑ A1 `PROJECT_CONTEXT.md`: counts, size claim, dates, repeated rules, skills section
2. ☑ A2 `CLAUDE.md`, `AGENTS.md`, `AGENTS.override.md`: pointer paragraphs, phantom skill, size claim
3. ☑ A3 `docs/tooling.md`: dates, phantom path, golden counts, banned phrase
4. ☑ A4 Whole-tree check that no entry doc states a date or a banned phrase

### Wave B: split architecture.md

11. ☑ B11 Mechanical split into `docs/architecture/<Namespace>.md`, index file keeps the header and bullets
12. ☐ B12 `CheckDocEntries.ps1` (caps, existence, coverage; `-Summary`), not yet gated
13. ☑ B13 Pointer sweep of the grep instructions in skills, agent docs and `AGENTS.override.md`

### Wave C: trim every entry and index bullet to the cap

21. ☐ C21 `docs/architecture/Effects.md` (5 entries; the rehearsal for the brief)
22. ☐ C22 `docs/architecture/Utils.md` (17)
23. ☐ C23 `docs/architecture/Bindings.md` (17)
24. ☐ C24 `docs/architecture/Testing.md` (17 plus the `CSVM.Tests/` section)
25. ☐ C25 `docs/architecture/Root.md` (3 entries plus Cross-module conventions and the enhanced-graphics section)
26. ☐ C26 `docs/architecture/Session.md` (40)
27. ☐ C27 `docs/architecture/Mech3.md` (62)
28. ☐ C28 `docs/architecture/UI.md` (78)
29. ☐ C29 `docs/architecture/Flight.md` (108), then the complete `.\RunTests.ps1`

### Wave D: verification.md

31. ☑ D31 Delete the 46 uncited rules (with the paragraph-rule exception check)
32. ☐ D32 Cut every remaining paragraph rule to shape; remove merge notes, dates and the stale line ref; restore numeric order
33. ☐ D33 Fix the six dangling rule references at their source; header states the shape rule

### Wave F: tooling.md and cli.md

51. ☐ F51 `docs/tooling.md`: each script's section cut to what it does, its switches and its outputs
52. ☐ F52 `docs/cli.md` flag bullets, first half of the index groups (modes through debug labs), to the 600-character cap
53. ☐ F53 `docs/cli.md` flag bullets, second half (dumps through audio), plus the three lab sections and the index prose

### Wave E: wire the gate and close

41. ☐ E41 `CheckDocEntries.ps1` into `CheckCommitContent.ps1` and its `-SelfTest`; CLAUDE.md hook clause
42. ☐ E42 `PROJECT_CONTEXT.md` names the layout and the check; closing commit deletes this plan

## Dependency and parallelism notes

A1 to A3 are independent and edit disjoint files; A4 follows them. B11 blocks everything in C
and E; B12 and B13 follow B11 and are independent of each other. C items each own one namespace
file and the `docs/architecture.md` bullets of that namespace only; they run in parallel
worktrees with that file-ownership boundary, except that two C items relocating a trap into the
same `docs/org/` page or the same `.cs` file contend, so the orchestrator merges C items one at a
time and re-runs `CheckDocEntries.ps1` after each merge. C21 runs first, alone, to prove
the brief on the smallest file before the rest fan out. C29 ends with the complete
`.\RunTests.ps1` because the wave's comment relocations touch `CSVM/src`. D31 to D33 are a chain
in one file and are independent of B and C, so Wave D may run beside Wave C in its own worktree;
D33 edits `docs/tooling.md` and `docs/cli.md`, which no C item touches. Wave F follows D33
(same two files) and is independent of Wave C; F51 owns `docs/tooling.md`, F52 and F53 split
`docs/cli.md` by index group and must not run in parallel with each other (one file, and the
index-versus-bullet reconciliation in the file header is shared). E41 needs every C and F item
merged (the gate must pass on the tree it guards); E42 is last.

---

# Wave A: stale facts in the index files

## A1 ☑ `PROJECT_CONTEXT.md`: counts, size claim, dates, repeated rules, skills section

**Goal.** The file states no count the repo contradicts, no date, and each shared rule once; it
describes the split layout of Wave B so a reader arriving between waves is not misdirected.

**Evidence (confidence: traced).** Line 27 "(decided 2026-07-21)", line 39 "(decided
2026-07-22)", line 213 "default 0 since 2026-08-05"; line 113 "16 pinned `--det` shots" against
18 in the manifest; lines 161 to 169 carry the wrong namespace counts listed above and line 159
the "~110 KB" claim; line 178 "30 of 151"; the budget rule at line 114 and lines 144 to 146; line
19 is a stray Claude-specific bullet ("use cheaper models"); lines 225 to 239 are an "Agent skills"
section with three one-pointer subsections and the phantom `/setup-matt-pocock-skills`; line 12
narrates "keeping both was 13 KB of duplication".

**Approach.** Edit in place. Delete the three dated parentheticals. Line 113: "the pinned `--det`
shots" with no count. Module map: drop every count, drop the size claim, and make the lookup line
`Grep "## src/Flight/FlightModel.cs" -A 12 docs/architecture/` with one sentence saying the
index at `docs/architecture.md` routes to the namespace file. Line 178: "the day-to-day subset;
`docs/cli.md` is the description of record" with no numbers. Keep the budget rule only inside
"Development verification loop"; the `analysis/verification-budgets.json` layout line becomes
one clause. Move the line-19 bullet to `CLAUDE.md` (A2). Collapse "Agent skills" and its three
subsections into one short list of three pointers with no claim about who wrote the skills. Line
12 states the rule ("this file carries the namespace map, never a per-module list") without the
history. Do not touch "Current status" beyond what E42 prescribes.

**Model recommendation.** medium. Mechanical edits with a clear list.

**Verify.** `(Get-Item PROJECT_CONTEXT.md).Length` under 35 KB and smaller than before;
`Select-String '\b20\d\d-\d\d-\d\d\b' PROJECT_CONTEXT.md` returns nothing;
`Select-String 'over budget' PROJECT_CONTEXT.md` returns one line; `.\CheckEncoding.ps1` clean.

**⚠ Traps.** The day-to-day flag table (lines 182 to 213) is glosses by design; A1 edits only the
one dated cell in it. The `--volume` row's "default 0" stays, its date goes.

## A2 ☑ `CLAUDE.md`, `AGENTS.md`, `AGENTS.override.md`: pointer paragraphs, phantom skill, size claim

**Goal.** The three tool-specific files point at PROJECT_CONTEXT's verification loop instead of
restating it, name no phantom skill, and give the split-layout grep instruction.

**Evidence (confidence: traced).** `CLAUDE.md` lines 7 to 13 and `AGENTS.md` lines 8 to 14 are
the same paragraph, restating PROJECT_CONTEXT lines 128 to 146. `CLAUDE.md` line 19 and
PROJECT_CONTEXT line 227 credit `/setup-matt-pocock-skills`. `AGENTS.override.md` line 16 says
"~110 KB" and gives the single-file grep.

**Approach.** In `CLAUDE.md` and `AGENTS.md` replace the paragraph with one sentence: follow
PROJECT_CONTEXT.md's "Development verification loop", and the complete `.\RunTests.ps1` is the
landing gate for any change under `CSVM/`. Drop the skill attribution; keep the junction fact in
`AGENTS.md` only (it holds the command) and have `CLAUDE.md` point there. Add the "cheaper models
for delegated exploration" bullet to `CLAUDE.md`. `AGENTS.override.md` line 16: remove the size
claim and name `docs/architecture/` in the grep. That file says other agents' files are not pi's
to simplify; the reverse holds too, so touch only that line and nothing about its rules.

**Model recommendation.** medium.

**Verify.** `Select-String 'setup-matt-pocock' *.md` returns nothing; `Select-String '110 KB'
AGENTS.override.md` returns nothing; the four hooks paragraph in `CLAUDE.md` is unchanged.

**⚠ Traps.** The CLAUDE.md "Writing style" section contains dates as examples of what not to
write; those stay.

## A3 ☑ `docs/tooling.md`: dates, phantom path, golden counts, banned phrase

**Goal.** No date in live prose, no path that does not exist, no golden count, no banned phrase.

**Evidence (confidence: traced).** Dates at lines 28, 64, 96, 399, 407, 471, 475 to 476; the
`CSVM_DATA_ROOT` example at lines 382 to 384 uses `Z:\Crimson Skies`; line 285 "the complete
16-shot manifest", line 500 "all 11 goldens hash-identical", line 512 "11/11 goldens";
line 448 "Two filters in the preset are load-bearing".

**Approach.** Delete each dated parenthetical, keeping the fact it decorates (the upstream tip sha
may stay; its date goes; "when last checked" becomes "the last time it was checked" with no
date, or the sentence goes if it says nothing without one). Example path becomes `Z:\CSVM`.
Golden mentions become count-free ("every golden hash-identical"). Line 448: "Two filters in the
preset are required". Line 515 links `analysis/hidden-desktop/`, which is retired; cite it as
`git show analysis-archive:analysis/hidden-desktop/FINDINGS.md`, the form `analysis/README.md`
prescribes. Nothing else in the file changes; its verbosity is out of scope.

**Model recommendation.** medium.

**Verify.** `Select-String '\b20\d\d-\d\d-\d\d\b|Crimson Skies|load-bearing|11 goldens|16-shot'
docs/tooling.md` returns nothing.

**⚠ Traps.** Line 403 names a backlog entry by title, not id; that is a pointer, not provenance,
and stays.

## A4 ☑ Whole-tree check that no entry doc states a date or a banned phrase

**Goal.** One command proves Wave A is complete.

**Evidence (confidence: traced).** The banned list is in `CLAUDE.md`'s "Writing style".

**Approach.** `Select-String '\b20\d\d-\d\d-\d\d\b' PROJECT_CONTEXT.md, CLAUDE.md, AGENTS.md,
AGENTS.override.md, docs/tooling.md, docs/verification.md, docs/architecture.md` must return only
`CLAUDE.md`'s rule examples and `docs/verification.md`'s two dates (Wave D removes those).
`Select-String 'load-bearing|full stop|worth stating plainly|carry the argument|the trap\b'` over
the same files returns nothing. Fix whatever surfaces in the same commit as A3 if small, else as
its own.

**Model recommendation.** medium, low effort.

**Verify.** The two commands above.

**Result.** After A1 to A3 the date sweep returns CLAUDE.md's rule examples, the two
`docs/verification.md` dates (D32) and ten `docs/architecture.md` entry bodies; the banned-phrase
sweep returns CLAUDE.md's list and two "load-bearing" in `docs/architecture.md`. Every
architecture entry is rewritten in Wave C, so those hits are C's by construction and are not fixed
twice; E41's sweep re-runs both commands and must return only CLAUDE.md.

**⚠ Traps.** None.

# Wave B: split architecture.md

## B11 ☑ Mechanical split into `docs/architecture/<Namespace>.md`

**Goal.** Entries move byte-for-byte into nine namespace files; `docs/architecture.md` keeps its
header and the Module index; `Grep "## src/<path>" -A 12 docs/architecture/` returns one entry.

**Evidence (confidence: traced).** 347 `## src/...` headings; namespace of each is the first path
segment after `src/`; the three root files are `SessionSpec.cs`, `SessionPaths.cs`, `Pads.cs`.
`## Cross-module conventions` (line 530), `## Rendering: the enhanced graphics mode` (538) and
`## CSVM.Tests/` (599) are the non-index sections.

**Approach.** Write a one-off ASCII PowerShell script in the session scratchpad (not the repo):
read with `[IO.File]::ReadAllText(path, [Text.Encoding]::UTF8)`, split on `(?m)^## `, route each
`## src/<Ns>/` chunk to `docs/architecture/<Ns>.md` where `Ns` is `Mech3`, `Flight`, `UI`,
`Session`, `Bindings`, `Testing`, `Utils`, `Effects`; the three root entries plus Cross-module
conventions and the enhanced-graphics section go to `Root.md`; `CSVM.Tests/` goes to
`Testing.md`. Write every file with `New-Object Text.UTF8Encoding($false)`. Preserve entry order
within each file. Each namespace file opens with three lines: what the namespace is (take the
sentence from the index's `### src/<Ns>/` heading), "one `## src/...` entry per module, body at
most 8 lines, 12 for the highest-traffic modules", and "traps do not live here; the rule is in
`docs/architecture.md`". The index file keeps lines 1 to 20 with the size claim removed and the
lookup rewritten to name `docs/architecture/`, then the Module index unchanged (its bullets are
trimmed in Wave C). Commit with `git add docs/architecture.md docs/architecture/`.

**Model recommendation.** medium. Mechanical, but the byte-identity check must be run, not assumed.

**Verify.** `(Select-String '^## src/' docs/architecture/*.md).Count` is 347 and
`(Select-String '^## ' docs/architecture.md).Count` is 1; concatenating the chunks back in the
original order reproduces the original file's entry text (diff the joined bodies against `git show
HEAD:docs/architecture.md` with the headers stripped); `.\CheckEncoding.ps1` clean; a `Grep` for
`## src/Flight/FlightModel.cs` over `docs/architecture/` returns one hit.

**⚠ Traps.** Read the file through `[IO.File]`, never `Get-Content` without `-Encoding utf8`, or
every arrow and warning sign in 780 KB is mojibaked and the encoding check is the only backstop.
`Mech3/Anim/` and `UI/Menu/**` are subfolders of their namespace, not namespaces of their own.

## B12 ☐ `CheckDocEntries.ps1`, not yet gated

**Goal.** A repo-root script that reports every entry over cap, every index bullet over one line,
every heading naming a missing file, and every source file with neither heading nor bullet;
exit 0 only when clean.

**Evidence (confidence: traced).** `CheckCommentCaps.ps1` is the model: scans the worktree the
script lives in, bare output is `file:line` rows, `-Summary` is one line per file worst-first,
paths as arguments restrict the scan. `CheckCommitContent.ps1` runs four such scripts.

**Approach.** Pure ASCII script. Rules: body of a `## src/` entry is the lines from the heading to
the next `## ` heading, trimmed of blank lines; cap 8, or 12 for a heading in a fixed list
(`GameSession.cs`, `FlightController.cs`, `FlightModel.cs`, `SceneBuilder.cs`, `WorldBuilder.cs`,
`AnimRuntime.cs`, `Projectile.cs`, `Testing/*Suites.cs`). Index bullets in `docs/architecture.md`
under a `### src/` heading must be one physical line. Every `## src/<path>` must exist under
`CSVM/src` (a `*` wildcard heading matches by glob). Every `.cs` under `CSVM/src` except
`Testing/*Suites.cs` and `Mech3/Anim/*.cs` must appear as a heading in `docs/architecture/*.md`
and as a bullet in the index. For `docs/cli.md`: every `- \`--flag\`` bullet under `## Flags`,
measured from the bullet's first character to the line before the next bullet or heading, is at
most 600 characters, and the three lab sections are at most 12 lines each; the index-token count
must equal the count of flags with a bullet (the header's own reconciliation). `-Summary` and
path arguments as in `CheckCommentCaps.ps1`. Do not wire it into the gate yet; until Wave E it is
the progress meter and would block every commit.

**Model recommendation.** medium.

**Verify.** Run it on the freshly split tree: it must report 204 over-cap entries, the over-long
index bullets, 0 missing files, and the 21 uncovered files listed above. Run it on one namespace
path and confirm the scope narrows. `.\CheckEncoding.ps1` clean (the script must be ASCII).

**⚠ Traps.** Keep the script's own text pure ASCII, building any non-ASCII match characters from
`[char]` codes; a BOM-less `.ps1` with non-ASCII is mangled by the interpreter. Do not add the
check to `.codex/` or `.pi/` harnesses; they call `CheckCommitContent.ps1`.

## B13 ☑ Pointer sweep of the grep instructions

**Goal.** Every instruction that tells an agent how to find a module's entry names the split
layout; prose that merely says "its entry in `docs/architecture.md`" is left alone.

**Evidence (confidence: traced).** Grep instructions or path-shaped claims at
`AGENTS.override.md:16`, `.claude/skills/plan-item/SKILL.md:61,166,173`,
`.claude/skills/commit-next/SKILL.md:65`, `.claude/skills/backlog/SKILL.md:53`,
`.claude/skills/close-backlog-item/SKILL.md:81`, `docs/agents/plan-template.md:99,107`,
`docs/agents/issue-tracker.md:38`, `docs/agents/domain.md:18,45,57`. About 140 code comments say
"docs/architecture.md" as a noun; the index routes, so they stay.

**Approach.** Edit each listed line to "its entry in `docs/architecture/<Namespace>.md`, found
through the index in `docs/architecture.md`" or the grep over `docs/architecture/`, whichever the
sentence is. Skills are mirrored at `.agents/skills/` by a junction, so editing `.claude/skills/`
is enough.

**Model recommendation.** medium, low effort.

**Verify.** `Select-String 'architecture.md.*-A 12|-A 12.*architecture.md' -Path .claude/skills,
docs/agents, *.md -Recurse` shows every hit naming `docs/architecture/`.

**⚠ Traps.** Do not rewrite the code comments; that is 140 files of churn for no reader benefit
and would trip the full-run landing rule for nothing.

# Wave C: trim every entry and index bullet to the cap

Every C item runs the same brief on its own file, in a worktree, with a subagent per file (Flight
and UI may be split in halves by entry range); the orchestrator commits each file as it lands and
merges one at a time. **The brief, given verbatim to each agent:**

1. For each `## src/...` entry in your file: rewrite to purpose (1 to 2 sentences), what it owns,
   and which module to read next. Body at most 8 lines, 12 only for `GameSession.cs`,
   `FlightController.cs`, `FlightModel.cs`, `SceneBuilder.cs`, `WorldBuilder.cs`,
   `AnimRuntime.cs`, `Projectile.cs` and `Testing/*Suites.cs`.
2. For each `⚠` or binding constraint in the old body decide: still binding, then move it to a
   comment on the member it binds (under `CheckCommentCaps.ps1`'s caps, prohibition first,
   reason second), or to the module's `docs/org/` page, or to a rule in `docs/verification.md` in
   the shape "bold imperative plus at most one evidence sentence". Not binding, historical, or
   already covered at the destination, then delete. Record every relocation as `entry -> destination`
   in `.scratch/relocations-<Namespace>.md` for the commit message.
3. Measurement tables, refuted hypotheses, per-item narration, `BL-`, `PLAN-` and dates: delete
   after checking the claim is on the module's `docs/org/` or `docs/formats/` page; write it there
   if absent and the constant or decision still exists in code. If the constant is gone, the claim
   goes with it. A pointer to an `analysis/` directory that no longer exists (the entries for
   `WorldEffectsFactory` and the lens flare cite `death-effect-closure` and `bl-165-lens-flare`)
   is cited as `git show analysis-archive:analysis/<dir>/FINDINGS.md` if the pointer survives the
   trim, else it goes.
4. Rewrite your namespace's index bullets in `docs/architecture.md` to one line each, at most 160
   characters, purpose only.
5. Add an index bullet and an entry for each of your namespace's uncovered files (the list in
   "What the data actually ships"). Read the file to write it; never guess.
6. Finish with `.\CheckDocEntries.ps1 docs/architecture/<Namespace>.md` clean,
   `.\CheckCommentCaps.ps1 -Summary` clean for every `.cs` you touched, `.\CheckEncoding.ps1`
   clean, and `dotnet build CSVM/CSVM.sln` green if you touched a `///` comment.
7. Report: the relocation list, every deletion you were unsure about, and every `docs/org/` page
   you created or extended.

Model recommendation for every C item: **high**. Deciding what is still binding is the whole job;
a low tier will delete a constraint or keep a narrative.

## C21 ☐ `docs/architecture/Effects.md`

**Goal.** Five entries at cap; the brief proven on the smallest file before the fan-out.

**Evidence (confidence: traced).** `Effects/` has 5 files and 5 entries; `docs/org/puffer.md`
already carries the puffer plumbing and traps, so most of what leaves `Puffer.cs`'s entry is a
pointer.

**Approach.** The brief. The orchestrator reviews the result against the brief before starting
C22 onward and amends the brief text in this plan if a step was ambiguous.

**Verify.** Step 6 of the brief; the orchestrator reads the diff whole.

**⚠ Traps.** `Puffer` construction order re-pins every puffer-bearing golden (GOLD-11); a comment
relocated onto the constructor must say so.

## C22 ☐ `docs/architecture/Utils.md`

**Goal.** 17 entries at cap plus entries for `HoldToRepeat.cs` and `PhysicsTickCost.cs`.

**Evidence (confidence: traced).** Several Utils entries are cited from code as the description of
record (`Log.cs`, `Rng.cs`, `Config.cs`, `EffectPools.cs`, `HitchMonitor.cs`, `HitchSidecar.cs`,
`GameClock.cs`, `RenderPoses.cs`, `PerfSample.cs`, `StartupProfile.cs` each say "this module's
entry in docs/architecture.md" for a list, a grammar or a formula).

**Approach.** The brief, with one addition: where a code comment points at the entry for a
list or a formula (log categories, the Rng streams, the hitch formula, the sidecar line grammar),
that content is decode knowledge and moves to a `docs/org/` page (`docs/org/hitch.md`,
`docs/org/logging.md` or similar, one per subject, not one per file) and the code comment's
pointer is updated to that page in the same commit.

**Verify.** Step 6, plus `Select-String 'docs/architecture.md' CSVM/src/Utils/*.cs` shows every
remaining pointer naming something the trimmed entry still holds.

**⚠ Traps.** `HitchMonitor`'s TUNE constants are tune, not fact; the `docs/org/` page says so.

## C23 ☐ `docs/architecture/Bindings.md`

**Goal.** 17 entries at cap plus `ICaptureDevices.cs` and `SeatCaptureDevices.cs`.

**Evidence (confidence: traced).** `docs/org/input.md` exists and is the home for binding decode.

**Approach.** The brief.

**Verify.** Step 6.

**⚠ Traps.** The versioned per-player keymap file format is format knowledge; if its entry
describes the file, that description goes to `docs/formats/` or `docs/org/input.md`.

## C24 ☐ `docs/architecture/Testing.md`

**Goal.** 17 entries at cap; the `Testing/*Suites.cs` entry (135 lines) becomes a 12-line
orientation; the `CSVM.Tests/` section's three traps find homes.

**Evidence (confidence: traced).** The suites entry enumerates 17 suite modules with per-scenario
narration; the suite catalog is `SuiteCatalog.cs` and `test-report.json` already carry the
membership. `CSVM.Tests/` carries three `⚠` about the `SessionSpec*Tests` trio, `fixtures/`
provenance and golden-invariant skips.

**Approach.** The brief. The suites entry names the harness, the catalog, where a suite's
registration lives and how a new one is added, and stops. The three `CSVM.Tests/` traps go to
comments in the test project (a `README`-style comment at the top of the relevant fixture loader
or test class, under caps) or to `docs/verification.md` where they are measurement rules.

**Verify.** Step 6.

**⚠ Traps.** `CSVM.Tests/`-only edits do not need the full run; a `CSVM/src/Testing` comment does
count as `CSVM/`.

## C25 ☐ `docs/architecture/Root.md`

**Goal.** Three root entries at cap; "Cross-module conventions" loses its two traps to code
comments; "Rendering: the enhanced graphics mode" keeps the architecture description and loses its
proof methodology, "Recorded disproofs" and open-judgement pointers.

**Evidence (confidence: traced).** Section at old lines 538 to 598: "byte identity is proven"
(shader-key dumps, golden sweep), two recorded disproofs, a pointer to `PLAN-enhanced-graphics`'s
Open judgements (a deleted plan). `Pads.cs` carries five pointers into its entry.

**Approach.** The brief. The proof methodology becomes at most one verification rule if it is
still how a shader-key change is verified (it is: a change to a shader-key generator must leave
every golden byte-identical in original mode), else nothing. The disproofs go to the commit
message. The open judgements list is not live prose; drop the pointer.

**Verify.** Step 6; `Select-String 'PLAN-' docs/architecture/Root.md` returns nothing.

**⚠ Traps.** `Pads.cs` reads its entry for the seat-assignment rule; keep that rule in the entry
(it is orientation) or move it beside the code.

## C26 ☐ `docs/architecture/Session.md`

**Goal.** 40 entries at cap, including `Launcher.cs` (168 lines), `CampaignDirector.cs` (144),
`CutsceneController.cs` (140), `WorldEffectsFactory.cs` (114), `ZeppelinRuntime.cs` (79); plus
`AirframeSwap.cs`, `CampaignDangerZones.cs`, `ZeppelinRuntime.Cannons.cs`.

**Evidence (confidence: traced).** `docs/formats/anim-definitions/cutscenes.md`,
`docs/org/campaign-board.md`, `docs/org/sequences.md` and `docs/org/objectMotion.md` already hold
most of the cutscene and campaign decode these entries restate.

**Approach.** The brief; may run as two agents (Launcher/Campaign/Cutscene and the rest) with the
file split by entry range and merged by the orchestrator.

**Verify.** Step 6.

**⚠ Traps.** `WorldEffectsFactory`'s pool paragraph is cited from `CSVM/data/effect_pools.json`'s
`_references` field; update that pointer if the paragraph moves to `docs/org/`.

## C27 ☐ `docs/architecture/Mech3.md`

**Goal.** 62 entries at cap, including `AnimRuntime.cs` (164), `SequenceRunner.cs` (64),
`PlaneBuilder.cs` (60), `SceneBuilder.cs` (58), the `Mech3/Anim/` directory entry; plus
`MilitiaPaint.cs`.

**Evidence (confidence: traced).** `AnimRuntime.cs` and `TemplateStage.cs` carry a dozen code
pointers into their entries; `docs/org/sequences.md`, `docs/org/objectMotion.md`,
`docs/org/puffer.md`, `docs/org/clutter.md`, `docs/org/paint.md`, `docs/org/textures.md` and
`docs/formats/` hold the decode.

**Approach.** The brief; two agents by entry range.

**Verify.** Step 6, plus `Select-String 'docs/architecture.md' CSVM/src/Mech3 -Recurse` shows no
pointer to content the trimmed entry no longer holds.

**⚠ Traps.** `SceneBuilder`'s accepted corner cases (the prio-0 tie-break span, the UV clamp
rule) are cited by `analysis/` findings and `docs/formats/gotchas.md`; keep each as one clause in
the entry or move it to the format page and leave the analysis citation to find it there.

## C28 ☐ `docs/architecture/UI.md`

**Goal.** 78 entries at cap, including `LaunchMenu.cs` (165), `InstantActionPresets.cs` (121),
`HangarFlow.cs` (87), `OriginalShell.cs` (77), `OriginalCampaign.cs` (58); plus `HudLayers.cs`,
`PlaneFit.cs`, `PlaneNameTables.cs`, `PlaneRatings.cs`, `Menu/BriefingObjectives.cs`,
`Menu/BriefingScript.cs`.

**Evidence (confidence: traced).** `docs/menu-presentations.md` is the end-to-end menu contract
and says the per-module detail is in architecture; `docs/formats/instant-action.md`,
`docs/formats/menu-layout.md`, `docs/org/hangar.md`, `docs/org/menu-inventory.md`,
`docs/org/debrief.md`, `docs/org/campaign-board.md`, `docs/org/loading-screen.md` hold the decode.
`InstantActionPresets.cs`'s entry restates record counts and offsets from the format page.

**Approach.** The brief; two agents by entry range.

**Verify.** Step 6; `docs/menu-presentations.md`'s pointers still resolve.

**⚠ Traps.** The menu contract page is the place for cross-module menu rules; do not move a
menu-wide rule into one module's entry.

## C29 ☐ `docs/architecture/Flight.md`, then the complete `.\RunTests.ps1`

**Goal.** 108 entries at cap, including `Projectile.cs` (415), `FlightController.cs` (282),
`AimAssist.cs` (68), `CameraController.cs` (61); plus `CockpitGauges.cs`, `CollisionDamage.cs`,
`HudFontTest.cs`, `ResultsBoard.cs`, `ScreenSize.cs`, `StickRamp.cs`, `StuntSplits.cs`. Then
the landing gate for the whole wave.

**Evidence (confidence: traced).** `docs/org/` already has `flightModel.md`, `aim-assist.md`,
`cameraViews.md`, `ordnanceTypes.md`, `tracers.md`, `weaponFire.md`, `weaponImpact.md`,
`weaponRay.md`, `vehicleDamage.md`, `shakes.md`, `targeting.md`, `ladderSwitch.md`; the giant
entries duplicate them with `FUN_` addresses and per-weapon tables.

**Approach.** The brief; three agents by entry range (Projectile and its pool alone; FlightController,
FlightModel and camera; the rest). After the merge of the last C item, run the complete
`.\RunTests.ps1` once from the main tree (the wave's comment relocations are changes under
`CSVM/`); every golden must be hash-identical since no semantics changed.

**Verify.** Step 6; `.\CheckDocEntries.ps1` clean over the whole tree; `.\RunTests.ps1`
exits 0 with every golden identical and `git diff -- analysis/goldens/manifest.json` empty
(GOLD-9).

**⚠ Traps.** `Projectile.cs`'s entry records a deliberate remake-only rule (the trail hold) and
several "an earlier reading here" corrections; the rule stays as one line in the entry or on the
member, the corrections go to the commit message. `--tex-override` and the flight-model probes are
verification instruments; their traps belong in `docs/verification.md`, not here.

# Wave D: verification.md

## D31 ☑ Delete the 46 uncited rules

**Goal.** The 46 rules listed under "What the data actually ships" are gone; their IDs are gaps.

**Evidence (confidence: traced).** The citation census above; the table with per-rule counts
was produced by grepping each ID over the repo and one `git log --all --format=%B` dump with a
word boundary so `PERF-1` does not match `PERF-12`. Regenerate it the same way if needed.

**Approach.** The census predates the retirement of 28 `analysis/` directories (tag
`analysis-archive`), so re-run it first over the current tree plus the commit dump: a rule whose
only citation was a retired findings file joins the deletion list, and the commit message names
which. Before deleting a paragraph rule from the list (SHOT-25, SHOT-26, WORLD-33,
INSTR-23, INSTR-29, INSTR-32, INSTR-34, GOLD-7), check whether its evidence names a mechanism still
in code with no other home; if so keep it and cut it to shape in D32, and say so in the commit
message. Delete the rest outright. The deleted text goes in the commit message body as a list of
IDs with their one-line imperatives, nothing more.

**Model recommendation.** medium.

**Verify.** Every listed ID absent from the file; the ID set cited elsewhere in the repo is
unchanged (none of the 46 is cited, by construction); `.\CheckEncoding.ps1` clean.

**⚠ Traps.** Never renumber to close a gap; the header says IDs are permanent.

## D32 ☐ Cut every remaining paragraph rule to shape; merge notes, dates, stale ref; numeric order

**Goal.** Every rule is a bold one- or two-line imperative plus at most one sentence of measured
evidence; no "renumbered at the merge" notes; no dates; SHELL-12 cites the property name, not a
line number; sections are in numeric order.

**Evidence (confidence: traced).** The shape rule is PROJECT_CONTEXT line 15. Longest offenders:
SHOT-23, SHOT-27, SHOT-28, SHOT-29, SHOT-31, GOLD-6, GOLD-9, PERF-19 to PERF-25, LOG-13, INSTR-10,
INSTR-11, INSTR-33, INSTR-35, all of SRC. Out of order: GOLD-7 (if kept) after GOLD-11, INSTR-7
after INSTR-11, INSTR-15 before INSTR-14, SHELL-15 before SHELL-14. Dates in SHOT-24 and GOLD-7.
Golden counts "13/13" (GOLD-9) and "18" (WORLD-32, INSTR-33) are evidence sentences and may stay.

**Approach.** Rule by rule. The imperative keeps the mechanism ("physics_ms is the worst tick of
the last wall second, not a per-frame cost"); the evidence sentence keeps the one number and the
instrument that proved it; the narrative (how it was found, what was believed before, who
measured it) goes to the commit message body. Identifiers that locate the evidence (a suite name,
an analysis directory, the item the measurement belongs to) stay. Move the misordered rules into
numeric position. Add the shape rule to the header in one line. An evidence sentence that cites a
retired analysis directory (METHOD-20 and SHOT-20 to SHOT-22 cite `analysis/bl-165-lens-flare/`,
GOLD-6 cites `analysis/bl-135-callsequence-lag/FINDINGS.md`) cites it as
`git show analysis-archive:analysis/<dir>/FINDINGS.md` or drops the path.

**Model recommendation.** high. Deciding what the one evidence sentence is requires understanding
each rule.

**Verify.** No rule body over 4 lines except by orchestrator exception noted in the commit;
`Select-String '\b20\d\d-\d\d-\d\d\b|renumbered' docs/verification.md` returns nothing; the ID
sequence within each section is ascending.

**⚠ Traps.** METHOD-9 is cited from 21 files and 35 commits; its text must keep meaning "the
check must be able to fail" exactly. The file's final three sections (what the project cannot
verify, non-deterministic surfaces, the standing checklist) are already at shape and stay.

## D33 ☐ Fix the six dangling rule references; header states the shape rule

**Goal.** No document cites a verification ID that does not exist.

**Evidence (confidence: traced).** `docs/formats/gotchas.md:19` and `docs/formats/gamez.md:45`
cite WORLD-1; `analysis/surface-classification/FINDINGS.md:25` cites WORLD-6; `docs/cli.md:478`
cites WORLD-13; `docs/cli.md:376` cites SHELL-1; `docs/tooling.md:508,520` cite SHELL-5 and
SHELL-9. `analysis/` findings are committed records; editing one to fix a pointer is allowed,
editing its measurements is not.

**Approach.** For each: read the sentence, find the existing rule that covers the claim (the
window-focus and desktop ones look like SHELL-13's family; the gamez ones may be WORLD-8 or
WORLD-15) and cite that, or state the claim in words with no ID. If the claim is a real rule with
no home, mint it at the end of its section in the documented shape. Then one sweep:
`Select-String '\b(METHOD|DIAG|SHOT|GOLD|DET|PERF|LOG|WORLD|SHELL|INSTR|SRC)-\d+\b' -Recurse`
over `docs/`, `analysis/`, `CSVM/`, `CSVM.Tests/`, `.claude/skills/`, `*.md`, `*.ps1` against the
ID set in the file; every hit must resolve.

**Model recommendation.** medium.

**Verify.** The sweep returns zero dangling IDs.

**⚠ Traps.** `docs/cli.md` is 186 KB; grep to the line, never read it whole.

# Wave F: tooling.md and cli.md

## F51 ☐ `docs/tooling.md`: each script's section cut to what it does, its switches and its outputs

**Goal.** A reader learns from each section what the script or stage does, how it is driven, and
where its output lands; the measurements that justified a default and the rules verification.md
already holds are gone from here. Target under 20 KB.

**Evidence (confidence: traced).** The twelve paragraphs over 1000 characters listed under "What
the data actually ships". The budget rule (PERF-18), shard isolation (LOG-13) and the vsync rule
(PERF-13) are restated in full. The rtexture tier paragraph is format knowledge that belongs on
`docs/formats/textures.md` or `docs/org/textures.md` if not already there. The window-focus
section narrates the 700-sample probe and the rejected alternatives, which the retired
`analysis/hidden-desktop` findings held.

**Approach.** Section by section, keeping the file's structure (extraction workdir, the two
extractors, the dispatcher, the launch scripts, the stages, the perf stage, exporting, `tools/`,
the fork, window focus, `RunProbe.ps1`). Each `RunTests.ps1` stage row: what it runs, what it
reads its verdict from, what fails it, one line. The budget paragraph: the rule and the file
that holds the numbers; the derivation ("slowest of three warm runs plus 50 %") stays as one
clause since it is how a number is re-set, the three timings go. The perf stage: the protocol
and the A/B verdict rule in one paragraph; the metric-by-metric refusal list becomes one
sentence pointing at PERF-1, PERF-2, PERF-21 and the manifest's `notes`. `test-report.json`:
the schema is versioned and what the blocks are for, one paragraph; the field list is read from
a report. Golden workers: the default and the serial reference path; the timing table goes.
Hidden desktop and window focus: what is done and why it must be decided before the process
starts, four sentences; the measurements go. Anything that is a rule about how an instrument
misleads becomes a pointer to its verification rule, minting one only if none covers it. The
removed measurements go to the commit message body.

**Model recommendation.** high. Every paragraph is a judgement about what a script's user needs.

**Verify.** `(Get-Item docs/tooling.md).Length` under 20 KB; every switch in `RunTests.ps1`'s
`param()` block, `RunProbe.ps1`'s and `ExportRelease.ps1`'s is still named; no paragraph over
1000 characters; `Select-String '\d+\.\d+ s|\d+ of \d+ samples' docs/tooling.md` returns
nothing; `.\CheckEncoding.ps1` clean.

**⚠ Traps.** The `SDL_JOYSTICK_DIRECTINPUT=0` paragraph is a workaround with a removal condition
in `backlog.md`; keep the pointer. `packaging/Extract.ps1`'s "keep all extraction logic in the two
scripts" rule is binding and stays as one sentence.

## F52 ☐ `docs/cli.md` flag bullets, modes through debug labs, to the 600-character cap

**Goal.** Every bullet for a flag in the index groups "Modes and content", "Placement",
"Capture", "Determinism", "Livery and paint", "Weapons, ordnance and damage" and "Debug labs" says
what the flag does, its argument shape, the modes it applies to and the flag it pairs with or
conflicts with, in at most 600 characters. One flag, one bullet, still the description of record.

**Evidence (confidence: traced).** The size table above; `--menu`, `--view`, `--ai`, `--anim-lab`,
`--weapon-lab`, `--pos`, `--destroy`, `--ai-attack`, `--det`, `--zeppelins`, `--debug-clutterflag`
and `--debug-markers` are all in this half. Bullets carry measured startup costs (`--collision`:
"C2 freecam measured 2,462 to 3,106 ms"), verification claims ("byte-identical by md5"), a lab's
crash history (`--debug-mesh`), and rejected designs.

**Approach.** Bullet by bullet. Keep: behaviour, argument grammar, defaults, the mode set, the
pairing or precedence rule, and a `WARN`/error behaviour the user will see. Move: a way the flag's
output misleads to `docs/verification.md` (most are already rules: SHOT-13/14 for the census
flags, WORLD-9 for `--collision`); decode knowledge to the format or `docs/org/` page; measured
costs and history to the commit message. `--det`'s second "in detail" bullet folds into the first
within the cap. The two shared bullets (`--direction` with `--pos`, `--spawn-dir` with
`--spawn-at`) may stay shared if each stays under cap, else split; then the header's reconciliation
paragraph is rewritten to the new counts, in one sentence. Run `.\CheckDocEntries.ps1
docs/cli.md` for the running tally; the remaining groups are F53's and will still report.

**Model recommendation.** high. A bullet is the description of record; cutting the wrong clause
changes what the flag is documented to do.

**Verify.** Every bullet in the named groups under 600 characters; the index-token count equals
the parser's accepted-flag count (`SessionSpec.cs`) and every index token has a bullet;
`.\CheckEncoding.ps1` clean.

**⚠ Traps.** `docs/cli.md` is 186 KB; grep to a bullet, never read the file whole. A bullet that
names a key binding (`F5`, `L`, `M`, `C`, `X`) is describing `docs/controls.md`'s subject; keep the
key, drop the description of the panel.

## F53 ☐ `docs/cli.md` flag bullets, dumps through audio, plus the lab sections and the index prose

**Goal.** The same cap for the groups "Dumps and the test harness", "Logging and profiling",
"Rendering probes", "Map-edge continuation", "Scripted input", "Data paths" and "Audio"; the three
lab sections (shared selection, node lab, world damage lab) at most 12 lines each; the file's
opening prose and the reconciliation paragraph at most one screen.

**Evidence (confidence: traced).** `--run-tests` (5185 characters), `--tex-census`, `--perf`,
`--debug-anim`, `--hitch-inject`, the `--dump-*` family and `--debug-damage`'s section are in this
half. The header's "Written exceptions to one flag, one bullet" paragraph (lines 82 to 93)
narrates a count reconciliation with history.

**Approach.** As F52 for the bullets. `--run-tests` keeps the selector grammar and the exit-code
contract and drops the stage narrative (that is `docs/tooling.md`'s). The lab sections keep the
key map and the flag's token grammar; the design rationale and the "able-to-fail control"
arguments are verification rules or go. The header keeps: flight is the default, the index is
the lookup, one flag one bullet with the named exceptions, and the counts as a single sentence
the check script verifies. After the last bullet, `.\CheckDocEntries.ps1 docs/cli.md` must be
clean.

**Model recommendation.** high.

**Verify.** `.\CheckDocEntries.ps1 docs/cli.md` clean; `(Get-Item docs/cli.md).Length` under
100 KB; PROJECT_CONTEXT's day-to-day table still glosses only flags that exist;
`.\CheckEncoding.ps1` clean.

**⚠ Traps.** `--det`'s constituents list is the behaviour, not narrative; it stays. A flag the
parser accepts but the index lacks is a defect this item must not paper over: add its bullet.

# Wave E: wire the gate and close

## E41 ☐ `CheckDocEntries.ps1` into the content gate

**Goal.** A commit that pushes an architecture entry over cap, an index bullet past one line, a
new `.cs` without an entry, or a `docs/cli.md` bullet past 600 characters is refused with a
message naming the file and the rule.

**Evidence (confidence: traced).** `CheckCommitContent.ps1` runs four scripts with worktree
resolution and a `-SelfTest`; `CLAUDE.md` documents them in the hooks paragraph. `.codex/` and
`.pi/` call `CheckCommitContent.ps1`, so nothing else changes.

**Approach.** Add the fifth call beside the four, same root resolution. Extend `-SelfTest` with a
fixture that writes a 13-line entry into a scratch copy and expects a failure. Add one clause to
the `CLAUDE.md` hooks paragraph naming the script and what it checks. Requires every C item
merged first; run the script over the tree before wiring it.

**Model recommendation.** medium.

**Verify.** `.\CheckCommitContent.ps1 -SelfTest` passes; a deliberate 13-line entry in a scratch
worktree is refused at commit; `.\CheckDocEntries.ps1` over the main tree is clean.

**⚠ Traps.** The gate checks every worktree; a stale plan worktree left over from Wave C with an
untrimmed file blocks main's commits. `CleanScratch.ps1` sweeps finished worktrees.

## E42 ☐ `PROJECT_CONTEXT.md` names the layout and the check; closing commit deletes this plan

**Goal.** The index file's "Keep the index files up to date" rules name `docs/architecture/` and
the check script; this plan is deleted and "Current status" cleared.

**Evidence (confidence: traced).** PROJECT_CONTEXT lines 11 to 12 describe the single-file layout;
line 117 describes `docs/architecture.md`.

**Approach.** Rewrite those three lines for the split layout in one sentence each. Then the
closing commit per convention: delete `docs/PLAN-doc-cleanup.md`, set "Active plan: none", swap
the "Next" pointer, and record in the message that the plan completed and what its last item was,
plus the summary of relocations gathered from the C commits.

**Model recommendation.** medium.

**Verify.** `Select-String 'PLAN-doc-cleanup' -Recurse *.md docs` returns nothing; PROJECT_CONTEXT
under 35 KB; "Current status" no longer than before.

**⚠ Traps.** Do not add a sentence about the landed work to "Current status"; the closing commit
message is the record.
