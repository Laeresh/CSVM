# Code review of the orch-1 to orch-3 runs, the corrections

**ACTIVE PLAN** (written 2026-09-12). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan corrects what the two-axis code review of the three orchestration runs found. The range
reviewed is main before the orch-1 fast-forward (`326fc28c`) to the orch-3 fast-forward
(`7d2e9881`): 136 commits, 391 files, the main-branch work merged into the runs on the way
included. Every `path:line` below was read at `7d2e9881`; four commits (BL-830, BL-696's second
close) have landed since, so re-locate a line before editing rather than trusting the number.
The Standards axis (the documented coding and writing conventions plus the Fowler smell
baseline) is Waves A and B; the Spec axis (the backlog entries and run logs against the code) is
Wave C. Findings too large to fix here were minted as `BL-832` to `BL-841` in `backlog.md` and
are not items of this plan; `PT-143` was minted and left unused, since `BL-496`'s owed feel check
already stands as `PT-135`. The parked `BL-696` and its four-shard `world-turrets` failure were
resolved after the range by `BL-830` and are not findings.

**Which judgement calls are corrected.** All of them, by the user's answer to the review: every
duplication (B11 to B14), every structural smell (B15 to B18), the em-dash sweep repo-wide (A7)
and the pre-existing `GD.Print` census calls beside the six new ones (A3). Commit history is not
rewritten; a finding that lives only in a landed commit body (a wrong rule number, a suite
deletion the body omits, a golden re-pinned in its own commit) is recorded here and left.

## Milestone goal

- Every hard violation of `PROJECT_CONTEXT.md`'s coding conventions and `CLAUDE.md`'s writing
  style that the review found is gone from the tree.
- The fifteen baseline smells the user chose are refactored, with no behaviour change and every
  golden identical.
- The spec findings small enough to fix are fixed, each with a pin that can fail.
- The rest are backlog items with their evidence, and nothing owed is lost.

**No behaviour changes outside Wave C.** Waves A and B are text and structure only; a golden that
moves under either is a defect in the item, not a re-pin.

## Decisions (2026-09-12)

| # | Question | Decision |
|---|---|---|
| 1 | Which baseline smells are corrected? | **All fifteen.** The user chose every option offered. |
| 2 | Em dashes: only the lines this range added, or the whole repo? | **Repo-wide.** About 7,700 lines across `CSVM/src`, `CSVM.Tests`, `docs/`, `backlog.md`, `playtest.md`, `PROJECT_CONTEXT.md` and `CLAUDE.md`; a script does it, A7. |
| 3 | Convert the ~124 pre-existing `GD.Print` census calls too? | **Yes**, in the three session files the six new calls sit in; A3. |
| 4 | Where does this plan live beside `PLAN-public-release.md`? | **Its own file.** Short, runnable, deleted on completion. |
| 5 | Spec findings that are not a small fix? | **Minted**, `BL-832` to `BL-841`. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Hard standards violations

1. ☑ Two `_Avoid_` terms: "AI roster" and "deviation"
2. ☑ `<para>` in three XML doc blocks
3. ☑ `GD.Print` to `Log` in the three session files
4. ☑ History and item references out of seven comments
5. ☑ One banned phrase and two dates in live prose
6. ☑ A culture-dependent `ToString` and an empty interpolation
7. ☐ Em-dash sweep, repo-wide

### Wave B — Judgement calls

11. ☑ One stopwatch bank for the three `--perf` cost meters
12. ☑ `TemplateStage` freed-key walk shared
13. ☑ One `Play` delegate and one `Once` for the three cinemas
14. ☑ `ObjectiveSites` collects both target classes through one method
15. ☑ `CameraController` reads its tuning through `_camParams` and names its views
16. ☑ `CinemaSkips` without the repeated arms and the mirror enum
17. ☑ The export set out of `SelectionService`
18. ☐ Four small ones: the `MSG_` sniff, `SetTeam`'s hidden order, a hull's `ForAircraft`, `Messages.Parse`

### Wave C — Spec fixes

21. ☑ `New-ItemId.ps1` gets its BOM and loses its em dashes
22. ☐ `SetNet` reports a missing net as no move
23. ☐ A death choreography is only the def's death sequence
24. ☐ `KeepsTheShot` holds only the episode that raised the ending
25. ☐ A queued start past the budget still gets its zero-dt advance
26. ☑ A turret voice culls at 1.1x its range, as decoded
27. ☐ `ADD_OTHER_TARGET` and its remove reach the mode table's points
28. ☑ The goldens README count and the re-pinned `exercises` fields
29. ☐ Three stale doc lines after the landings

## Dependency and parallelism notes

A7 (the em-dash sweep) rewrites text in nearly every file: run it alone, last in Wave A, and
never beside another item in a worktree; every later item then edits swept text. A3 and B18
both touch `GameSession.cs`; A3 also touches `CampaignDirector.cs` and `HumanFlightAdapter.cs`.
B13 and B16 both touch `BootSequence.cs` and the two cinema files; run them in sequence. B15 and
C26 are independent of everything else. C23 and C25 both edit `AnimRuntime.cs`. C24 edits
`CutsceneController.cs` and `GameSession.cs`; run it after A3. C28 and C29 are docs only and can
run beside anything except A7. Items otherwise run in listed order.

---

# Wave A — Hard standards violations

## A1 ☑ Two `_Avoid_` terms: "AI roster" and "deviation"

**Landed.** "AI roster" in the session sense became "the flight roster's AI walk" (or "an AI walk
over the flight roster") in `AiStepCost.cs`, `docs/architecture.md`, `docs/architecture/Utils.md`,
`docs/cli.md` and both `analysis/perf/scenarios.json` metric notes; "deviation" in the
remake-behaviour sense became "remake-only rule" in `GeneratorCycle.cs`, `GroundShadowLaw.cs`,
`docs/formats/weapon-effects.md`, four `backlog.md` entries and
`analysis/item9-depth-bias/CBLOCK-LOD.md`. The repo-wide grep found more of both than the
review's added-line read had: `GroundShadowLaw.cs`, `weapon-effects.md`, the backlog entries and
the analysis page were not on the evidence list. Uses left standing are a different referent, not
the avoided one: `docs/formats/ai-rosters.md` and its citations describe the original's `aiv`
roster data, which genuinely holds no human, and every surviving "deviation" is either the
`DEVIATION_DISTANCE` / `RANDOM_DEVIATION` authored quantity, a measured statistical deviation, or
`CONTEXT.md`'s own `_Avoid_` entry. `docs/cli.md`'s `--perf` bullet was 4 characters under its
600-char cap, so the longer term is paid for by dropping a copula and one article.

**Verified.** <pending orchestrator run> `Select-String` over `CSVM/src`, `CSVM.Tests`, `docs`,
`analysis`, `backlog.md`, `playtest.md`, `PROJECT_CONTEXT.md` for `AI[- ]roster` and `deviation`
returns only the justified cases above. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and
`CheckEncoding.ps1` clean. `dotnet build CSVM/CSVM.sln` 0 warnings, 0 errors.
`RunTests.ps1 -SkipEngine -SkipGoldens`: PASS, units 4090 passed, 0 failed, 2 skipped of 4092.

**Original approach (kept for reference).**

**Goal.** No word from `CONTEXT.md`'s `_Avoid_` lists survives in code or docs added by the range.

**Evidence (confidence: traced).** `CONTEXT.md` lists `AI roster` under _Avoid_ for "Flight
roster" (it excludes the human field) and `deviation` under _Avoid_ for "Remake-only rule".
"AI roster": `CSVM/src/Utils/AiStepCost.cs:6` and `:27`, `docs/architecture.md:379`,
`docs/architecture/Utils.md:123`, `docs/cli.md:175`, `analysis/perf/scenarios.json`.
"deviation": `CSVM/src/Session/GeneratorCycle.cs:28` and `:126`.

**Approach.** "The flight roster's AI walk" for the first; "remake-only rule" for the second.
Grep both terms repo-wide before closing, since the review read only the range's added lines.

**Model recommendation.** medium, low effort. Mechanical.

**Verify.** `Select-String` for both phrases over `CSVM/src`, `CSVM.Tests`, `docs`, `analysis`
returns nothing; `CheckCommentCaps.ps1` clean; unit tier green.

## A2 ☑ `<para>` in three XML doc blocks

**Landed.** The three `<para>` tags are gone and their text stands as further sentences of the
summary it already sat in. In `AiStepCostTests.cs` and `ProcessPassCostTests.cs` the
ambient-statics warning follows the summary directly, with the blank `///` separator line dropped
and the warning rewrapped; in `WarningShotCue.cs` the time-not-rounds paragraph joins the summary
the same way. No prose was cut and no member moved, so the two type blocks shrink to 9 and 10
lines against the 12-line type cap and `WarningShotCue`'s stays at 11.

**Verified.** <pending orchestrator run> `Select-String '<para>'` over the three files returns
nothing (the only remaining hits for the substring are `parallel` and `<param>`);
`CheckCommentCaps.ps1 -Root <worktree>` reports all comment blocks within cap; `dotnet build
CSVM/CSVM.sln` succeeds with 0 warnings and 0 errors; `RunTests.ps1 -SkipEngine -SkipGoldens`
passes, 4090 passed, 0 failed, 2 skipped of 4092 (the two skips are the cinema tests that need
extracted game data).

**Original approach (kept for reference).**

**Goal.** No `<para>` tag in the range's XML doc; the build generates no doc file so it renders
for nobody.

**Evidence (confidence: traced).** `CSVM.Tests/AiStepCostTests.cs:14-16`,
`CSVM.Tests/ProcessPassCostTests.cs:14-16`, `CSVM/src/Flight/WarningShotCue.cs:9-11`. About 58
older uses exist repo-wide; those are outside this plan.

**Approach.** Drop the tag and let the text stand as a second sentence of the summary, or as a
`//` block above the member if the summary is the wrong place for a warning. Re-check the type
and member caps after the reflow.

**Model recommendation.** medium, low effort.

**Verify.** `CheckCommentCaps.ps1` clean; `dotnet build` free of new warnings.

## A3 ☑ `GD.Print` to `Log` in the three session files

**Landed.** All 124 calls go through `Log`: `CampaignDirector.cs` 34, `GameSession.cs` 68,
`HumanFlightAdapter.cs` 22 (the file sits under `CSVM/src/Session/`, not `CSVM/src/Flight/` as the
evidence below says). Every one keeps its text. 121 became `Log.Info`, which is the shipped console
threshold, so each line stays on the console exactly where it was and now also reaches the file
sink; `Log.Debug` was used nowhere, because these are one-shot build-time census lines rather than
per-frame diagnostics, and `Debug` would have dropped them from a default console. The two
`GD.PrintErr` calls (the session load failure and the `--dump-tilegrid` refusal) became `Log.Error`,
which is unsuppressible, so they keep their console place and gain the `ERROR [cat]` tag. The
multi-line weapon-bench report goes through `Log.Raw`, the one call that writes a formatted block
verbatim.

Categories come from `Log`'s closed nine-name vocabulary. `HumanFlightAdapter.cs` is entirely the
human's own rig, so all 22 take its existing `flight`. `CampaignDirector.cs` takes `core`, the
session spine: the file's own `campaign` string at its one pre-existing `Log.Info` is outside the
vocabulary `Log.Categories` and `docs/org/logging.md` declare closed, and reusing it 34 more times
would have multiplied that. `GameSession.cs` spans the whole session, so its lines go by subject
across the categories it already uses plus three neighbours: `world` 27 (stage, textures, horizon,
clouds, map edge, colliders, zeppelins, generators), `flight` 22 (rigs, views, panes, matches, the
damage lab), `core` 10 (clock, campaign handoff, load failure, objective-site census, the `--diag`
dump), `weapons` 5 (the weapon lab and `--incoming`), `anim` 2 (the anim lab's stage), `sound` 1,
and the one `Log.Raw` block.

No reader moved. A sweep of `CSVM.Tests`, `CSVM/src/Testing`, the repo-root `*.ps1` and `analysis/`
for every printed prefix found no suite or script reading one of these lines off stdout: the five
`PushConsoleSink` suites each filter on a marker none of these lines carry, and `TestHarness`'s
engine-error screen matches Godot's `ERROR: ` form, which `Log.Error`'s `ERROR [cat]` deliberately
misses.

Visibility. No line left the console, and none was silently gained: a `--fly --chapter=C1
--plane=player_fury --screenshot --frames=30` run printed 162 stdout lines before and 162 after,
the converted ones differing only by their new `[category]` tag. The same run's log file went from
120 to 148 lines, the 28 new ones being exactly the census this item moved. The conversion also
fixes a culture bug the `GD.Print` form carried: on a German machine `flight stats` read
`engine=0,59 torques=(3,3,7,1,2)` and now reads `engine=0.59 torques=(3.3,7.1,2)`, because
`Log.Format` renders invariantly.

**Verified.** <pending orchestrator run> `Select-String 'GD\.Print'` over the three files returns
nothing; `CheckCommentCaps.ps1` reports all comment blocks within cap; `CheckEncoding.ps1` reports
no mojibake; `dotnet build CSVM/CSVM.sln` succeeds with 0 warnings and 0 errors;
`RunTests.ps1 -SkipEngine -SkipGoldens` PASS with 4090 passed, 0 failed, 2 skipped of 4092;
`RunTests.ps1 -Filter campaign -SkipUnits -SkipGoldens` PASS with 64 passed, 0 failed, engine
errors clean; `RunTests.ps1 -Filter flight-telemetry-gate,incoming-fire-cues -SkipUnits
-SkipGoldens` PASS with 2 passed, 0 failed, the two suites that count lines reaching a console sink
and the file sink.

**Original approach (kept for reference).**

**Goal.** Every census line in `CampaignDirector.cs`, `GameSession.cs` and `HumanFlightAdapter.cs`
goes through `Log`, so the file sink and the `--log=` filter see it.

**Evidence (confidence: traced).** New in the range: `CampaignDirector.cs:1159,1164`,
`GameSession.cs:878,2459,3011,3027`, `HumanFlightAdapter.cs:450`. Pre-existing in the same files:
34, 68 and 22 calls (124 in all; 387 under `CSVM/src`, the rest out of scope). The rule is
`PROJECT_CONTEXT.md` "Log through `Log`, never `GD.Print`"; `BL-351` found a `GD.Print` crashing
the xUnit host outright, which is why the new ones matter.

**Approach.** Convert each call to `Log.Debug` or `Log.Info` under the file's existing category
string, keeping the text. Before converting, grep `CSVM.Tests`, `CSVM/src/Testing`, `*.ps1` and
`analysis/` for each printed prefix: a suite or script that reads one of these lines off stdout
changes sink with it, and that reader moves in the same commit. Memory says the file sink takes
only `Log.*` and some lines are `--debug-*` gated; pick the level that keeps the census visible
where it was.

**Model recommendation.** medium. Mechanical but 130 sites with a sink change behind them.

**Verify.** `Select-String 'GD\.Print'` over the three files returns nothing; the full
`RunTests.ps1` battery, since the engine suites read logs; a `--campaign` and a `--fly` run each
show their census lines in the log file.

**⚠ Traps.** `Log.Debug` may be gated; a census line that silently vanishes from a scripted run
is a regression nobody sees. Compare one run's log before and after.

## A4 ☑ History and item references out of seven comments

**Landed.** The five history comments state the standing rule instead of narrating a change: the
CLI net spawn says what the volume write buys, the mesh-light constant says the flare reach is not
a fade distance, the boot fade test says the hold is zero, the puffer trail says where the
sprite-darkness reading and the texture flag disagree, and `camparam.md` says a sim-converted
easing rate compares the wrong pair of numbers. The two item references are gone: the ground
shadow test names the zone's `SUNLIGHT` pair, and the turret voice guard points at
`docs/formats/sounds.md` for the no-Doppler decode instead of a capture id. No docs page needed a
new claim, since `world-structure.md` and `sounds.md` already carry the evidence behind the two
cut sentences.

**Verified.** <pending orchestrator run> `CheckCommentCaps.ps1` clean ("all comment blocks within
cap"); `Select-String 'used to|no longer|Before this|BL-\d+|CAP-\d+|PT-\d+'` over the added diff
lines in `CSVM/src` and `CSVM.Tests` returns nothing, so every remaining hit is pre-existing;
`dotnet build CSVM/CSVM.sln` 0 warnings, 0 errors; `.\RunTests.ps1 -SkipEngine -SkipGoldens` PASS
with 4090 passed, 0 failed, 2 skipped of 4092.

**Original approach (kept for reference).**

**Goal.** Comments say what and why in the present tense, with no item ids and no narration of
what the code used to do.

**Evidence (confidence: traced).** History: `CSVM/src/Session/GameSession.cs:2611-2612` ("Before
this, a CLI plane kept..."), `CSVM/src/Testing/MeshLightSuites.cs:17-18` ("the reader used to
borrow..."), `CSVM.Tests/BootSequenceTests.cs:59` ("What it no longer does..."),
`CSVM/src/Testing/PufferSuites.cs:1564` ("used to add it"), `docs/formats/camparam.md:211` ("the
engine used to carry"). Item references: `CSVM.Tests/GroundShadowLawTests.cs:101` ("BL-332's
pair"), `CSVM/src/Testing/TurretVoiceSuites.cs:272` ("the D31 trap ... CAP-09").

**Approach.** Rewrite each to the current rule. Where the removed sentence is evidence (what the
old reading was and why it fell), check the module's `docs/org` or `docs/formats` page already
carries it, and put the removed prose in the landing commit body.

**Model recommendation.** medium, low effort.

**Verify.** `CheckCommentCaps.ps1` clean; a grep for `used to`, `no longer`, `Before this` and
`BL-\d+|CAP-\d+|PT-\d+` over `CSVM/src` and `CSVM.Tests` comments shows only pre-existing hits.

## A5 ☑ One banned phrase and two dates in live prose

**Landed.** "load-bearing" is gone from `docs/formats/` (the `gamez.md` site the review named and
eight older uses in `ai-rosters.md`, `anim-definitions.md`, `destructibles.md`, `paint.md` and
`templates.md`, each restated as "matters", "honours" or "unread"). The `BL-327` parenthetical
in `backlog.md` no longer carries its minting date. The second dated line the review cited was
`BL-785`'s "oldest created" clause, which left `backlog.md` with that item's close before this
plan was written. `backlog.md` still carries about 140 older dates in entries outside the reviewed
range; sweeping those is a separate decision and not this item.

**Verified.** <pending orchestrator run> `Select-String 'load-bearing'` over `docs/`, `backlog.md`
and `playtest.md` returns nothing; `CheckItemIds.ps1` and `CheckEncoding.ps1` clean.

**Original approach (kept for reference).**

**Goal.** `docs/` and `backlog.md` carry no banned phrase and no date outside the RETIRED headings.

**Evidence (confidence: traced).** `docs/formats/gamez.md:74` "load-bearing";
`backlog.md:985` ("2026-08-09; the fifth candidate C23 raised..." in a parenthetical opening at
`:984`) and `backlog.md:3108` ("the oldest created 2026-07-25"). Line numbers in `backlog.md`
have moved with the ten entries minted for this plan.

**Approach.** State the claim without the phrase; drop the dates and the event narration, leaving
the standing fact; the dated evidence stays findable through `git log --grep`.

**Model recommendation.** medium, low effort.

**Verify.** `Select-String 'load-bearing|\d{4}-\d\d-\d\d'` over `docs/`, `backlog.md`,
`playtest.md` returns only the `docs/org` RETIRED headings; `CheckItemIds.ps1` clean.

## A6 ☑ A culture-dependent `ToString` and an empty interpolation

**Landed.** `DanteEngineFireSuites` formats both the wanted and the found position through
`CultureInfo.InvariantCulture` with the same `0.#` shape its check line already uses. The
`SelectionService` interpolation stays: `Log.Info` takes a `FormattableString`, so the `$` is the
overload's requirement, not an empty interpolation. That half of the finding is disproven.

**Verified.** <pending orchestrator run> `dotnet build` clean.

**Original approach (kept for reference).**

**Goal.** Every formatted number in the range is invariant.

**Evidence (confidence: traced).** `CSVM/src/Testing/DanteEngineFireSuites.cs:116`
`mine.LastPos.ToString()` renders through the current culture (Godot's `Vector3.ToString()`
passes a null provider). `CSVM/src/UI/SelectionService.cs` `Log.Info("ui", $"select set
cleared")` interpolates nothing.

**Approach.** Format the vector's three components with `CultureInfo.InvariantCulture`, as the
neighbouring suites do; drop the `$`.

**Model recommendation.** medium, low effort.

**Verify.** The suite runs green under a German culture (`[CultureInfo]::CurrentCulture = 'de-DE'`
in the invoking shell, then the engine tier for that suite).

## A7 ☐ Em-dash sweep, repo-wide

**Goal.** No em dash in `CSVM/src`, `CSVM.Tests`, `docs/`, `backlog.md`, `playtest.md`,
`PROJECT_CONTEXT.md`, `CLAUDE.md` or `AGENTS.md`, except where a documented form requires one.

**Evidence (confidence: traced).** Counts of lines carrying U+2014: `CSVM/src` 3514,
`CSVM.Tests` 596, `docs/` 3142, `backlog.md` 263, `playtest.md` 74, `PROJECT_CONTEXT.md` 85,
`CLAUDE.md` 8. The 15 `### ⚠ … — RETIRED (yyyy-mm-dd)` headings in `docs/org/*.md` are the one
sanctioned form and keep theirs. Scripts under `analysis/` and the skill folders also carry em
dashes in their own comments and are out of scope, as is `CSVM/data/effect_pools.json` (game
data). `CheckEncoding.ps1:47` names the code point as a decode table entry, not as text.

**Approach.** A pure-ASCII PowerShell script in `.scratch/` that reads and writes with an explicit
UTF-8 encoding (`[IO.File]` with `UTF8Encoding`, BOM preserved as found), builds the dash from
`[char]0x2014`, and rewrites by rule: ` — ` between clauses becomes `, ` or `: ` (a comma when
the right side continues the sentence, a colon when it introduces a list or a value); an em dash
that opens a parenthetical pair becomes parentheses; a heading matching the RETIRED form is
skipped. Then read the diff by hand where the rule produced a comma splice, and split into two
sentences only in prose, since a code comment block is capped at six sentences and a split can
push it over. Run the sweep in three commits (code, docs, the two lists) so a mistake is
bisectable. The user's memory rule applies: PowerShell 5.1 reads BOM-less files as ANSI, so every
read and write names its encoding, and `CheckEncoding.ps1` runs after each pass.

**Model recommendation.** high for the rule and the hand pass; the script itself is mechanical.

**Verify.** `CheckEncoding.ps1`, `CheckCommentCaps.ps1`, `CheckDocEntries.ps1`,
`CheckGoldenProse.ps1` and `CheckItemIds.ps1` all clean; the full `RunTests.ps1` battery with 23
goldens identical (a comment sweep that moves a golden changed a string literal); a `git diff
--stat` per commit with no file outside the named scope.

**⚠ Traps.** (a) String literals: a `—` inside a `"..."` in C# is user-facing text or a log
format, not a comment; the script must skip literals or the hand pass must restore them. Grep for
`"[^"]*—` before running. (b) `docs/org` RETIRED headings are the documented form; a sweep that
rewrites them breaks the dated-heading rule. (c) The two `New-ItemId.ps1` dashes are C21, which
also adds the BOM; do not let the sweep touch that file BOM-less. (d) Reflowing a comment can
change its line count and fail a cap that passed.

# Wave B — Judgement calls

## B11 ☑ One stopwatch bank for the three `--perf` cost meters

**Landed.** `CSVM/src/Utils/WallCostBank.cs` holds the meter (`_openedAt`, the banked milliseconds,
the worst span, the spans closed and a tally slot) plus `WallCostBracket`, the one bracket node,
which takes the bank it wraps, names itself from the bank's label and brackets either clock.
`AiStepCost`, `ProcessPassCost` and `PhysicsTickCost` are thin facades over one instance each: all
three test classes write the ambient statics, so all three facades stay, and each keeps only the
terms its readout prints (`AiStepCost` the plane tally, the other two the worst span). The two
bracket types are gone; `Launcher._Ready` builds the four nodes through
`PhysicsTickCost.MakeBracket` / `ProcessPassCost.MakeBracket`, which keeps the bank private.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` clean (0 warnings, 0 errors);
`CheckCommentCaps.ps1` and `CheckDocEntries.ps1` both clean; `RunTests.ps1 -UnitFilter
"FullyQualifiedName~Cost" -SkipEngine -SkipGoldens` 28 passed of 28 and the `PerfSample` filter 15
of 15; `RunTests.ps1 -Perf -PerfFilter empty -PerfFrames 200 -PerfIterations 1` printed every key
with both clocks live, `[perf] window sim_frame=120 frames=60 wall_ms=553.07 fps=108.5
frame_ms=9.22 script_ms=14.34 proc_ms=0.423 proc_max_ms=3.848 proc_passes=60 ai_ms=0.000
ai_planes=0.0 render_cpu_ms=0.27 gpu_ms=0.11 physics_ms=0.07 phys_tick_ms=0.008
phys_tick_max_ms=0.033 phys_hz=59.7 draws=118.0 prims=2037.0 nodes=407.0 mem_mb=141.66
max_ms=19.95 p95_ms=10.61`.

**Original approach (kept for reference).**

**Goal.** `AiStepCost`, `ProcessPassCost` and `PhysicsTickCost` share one bank type and the two
bracket nodes share one implementation, with the `--perf` readouts unchanged.

**Evidence (confidence: traced).** `CSVM/src/Utils/AiStepCost.cs`, `CSVM/src/Utils/ProcessPassCost.cs`
and `CSVM/src/Utils/PhysicsTickCost.cs` each carry `_openedAt`/`_accumMs`/`Open`/`Close`/`Take`/
`Reset`; `ProcessPassBracket` duplicates `PhysicsTickBracket` field for field. Duplicated Code.

**Approach.** One `WallCostBank` (name it from `CONTEXT.md`'s terms) holding the accumulator and
the open/close pair, instantiated three times with its own label; the three public statics stay
as thin facades if the tests write them (the `AiStepCostTests` summary says it is the only writer
of the ambient statics), else the facades go too. One bracket node taking the bank it wraps.

**Model recommendation.** medium.

**Verify.** `AiStepCostTests`, `ProcessPassCostTests`, `PerfSampleTests` green; a `--perf` run on
the empty stage prints the same keys (`ai_ms`, `ai_planes`, `proc_ms`, `proc_max_ms`,
`proc_passes`, `phys_tick_ms`); `docs/architecture/Utils.md` entry rewritten.

## B12 ☑ `TemplateStage` freed-key walk shared

**Landed.** `DropFreed` opens with `FreedKeys()` as its count and rebuilds both identity-keyed
maps through one generic local function, so the walk and the rebuild each exist once. The local
function skips a map with no dead key rather than rehashing it, which is what the two separate
`dropped > 0` and `gone == 0` guards did. Public behaviour is unchanged: the same total is
returned, the follows and the deferred hides are pruned as before, and no caller or suite
contract moves. `docs/architecture/Mech3.md` describes the pair by what they do, not by their
shape, so its entry needed no edit.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` clean, 0 warnings;
`CheckCommentCaps.ps1` reports all blocks within cap; `RunTests.ps1 -UnitFilter
"FullyQualifiedName~TemplateStage" -SkipEngine -SkipGoldens` 30 passed, 0 failed; the two engine
suites that read the stale count, `damage-template-freed-anchor` (stale keys after the free: 3)
and `landings-hookup-airframe`, both PASS with engine errors clean; `RunTests.ps1 -SkipUnits
-SkipEngine` 23 shots hash-identical.

**Original approach (kept for reference).**

**Goal.** `FreedKeys()` and `DropFreed()` walk the two maps once, and the rebuild block appears
once.

**Evidence (confidence: traced).** `CSVM/src/Mech3/Anim/TemplateStage.cs`: both methods walk the
same two maps with the same `_isValid` test, and `DropFreed` repeats its rebuild block twice.
Duplicated Code.

**Approach.** `DropFreed` calls `FreedKeys()` and rebuilds through one local function over both
maps. Keep the stale-key count `BL-682`'s suite reads.

**Model recommendation.** medium, low effort.

**Verify.** The `TemplateStage` unit tests and `BL-682`'s able-to-fail suite green; 23 goldens
identical.

## B13 ☑ One `Play` delegate and one `Once` for the three cinemas

**Landed.** New `CSVM/src/UI/CinemaHandoff.cs` holds the `CinemaPlay` delegate
(`void (string name, Action then, CinemaSkip skip)`) and a public static `CinemaHandoff.Once(Action)`
carrying the latch comment. `ChapterCinema`, `ClosingCinema` and `BootSequence` dropped their own
nested delegate declarations and the two copies of `Once`, and take `CinemaPlay`; `BootCard.Play`
takes it too, since its parameter was typed `BootSequence.PlayFilm`. `Launcher` builds both cinemas
with the `PlayCinema` method group instead of a forwarding lambda. The tests needed no edit: they
pass method groups, which bind to the new type unchanged. `docs/architecture/UI.md` gained the new
entry and `docs/architecture.md` its index bullet; the `BootSequence` entry and the two
`docs/architecture/Session.md` cinema entries name `CinemaPlay` and `Once`.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` clean, 0 warnings 0 errors.
`.\CheckCommentCaps.ps1` and `.\CheckDocEntries.ps1` both clean.
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~Cinema|FullyQualifiedName~BootSequence" -SkipEngine
-SkipGoldens`: 124 passed, 0 failed, 2 skipped of 126 (the two skips want extracted film data).
`.\RunTests.ps1 -Suite cinema-skip-pad -SkipUnits -SkipGoldens`: 1 passed, 0 failed, engine errors
clean.

**Evidence correction.** The three files are `CSVM/src/Session/ChapterCinema.cs`,
`CSVM/src/Session/ClosingCinema.cs` and `CSVM/src/Session/Launcher.cs`, not under `CSVM/src/UI/`;
only `BootSequence.cs` is a UI file. The `Launcher` lambda pair sits at `:1379-1380`. The evidence
missed `BootCard.cs:37`, the one other reference to a removed delegate type.

**Original approach (kept for reference).**

**Goal.** `ChapterCinema`, `ClosingCinema` and `BootSequence` share one `Play` delegate type and
one `Once(Action)`; `Launcher` passes the method group.

**Evidence (confidence: traced).** `CSVM/src/UI/ChapterCinema.cs:82-95` and
`CSVM/src/UI/ClosingCinema.cs:68-81` are a byte-identical `private static Action Once(Action
handoff)`; the delegate is declared at `ChapterCinema.cs:35`, `ClosingCinema.cs:35`,
`BootSequence.cs:100`; `CSVM/src/Launcher.cs:1357-1358` wraps `PlayCinema` in a forwarding
lambda. Duplicated Code, Middle Man.

**Approach.** A `CinemaPlay` delegate and a static `CinemaHandoff.Once` beside `CinemaSkips`;
the three types take it. Method group at the `Launcher` site.

**Model recommendation.** medium, low effort.

**Verify.** `BootSequenceTests`, the cinema unit tests and the `cinema-skip-pad` suite green;
`docs/architecture/UI.md` entries adjusted.

## B14 ☑ `ObjectiveSites` collects both target classes through one method

**Landed.** `CollectTargets` and `CollectOtherTargets` are one
`CollectFlagged(TargetFlag flag, script, graph, targets, into)`. A new public `TargetFlag` enum
(`Objective`, `OtherTarget`) opens the file, and a private static `Sources` table keyed by it holds
the three things the two passes differed in: the `MissionTarget` flag the table entry carries, the
graph store that flag's `ADD_` directive fills, and the removal list a completed objective drops a
key through. `RemovedByCompletion` takes that row instead of a `bool other`. The objective pass now
runs the same `Listed` guard the other-target pass always ran, which can skip nothing: `Collect`
clears the list first and `targets.ByNode` keys are unique, so the pass only ever sees keys it added
itself. Callers name the flag: `Collect` (both passes), `CampaignMarkerSuites.NamesOf` and the unit
tests. `docs/architecture/Session.md`'s entry names `CollectFlagged` and `TargetFlag`.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` clean, 0 warnings 0 errors.
`.\CheckCommentCaps.ps1` and `.\CheckDocEntries.ps1` both clean.
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~ObjectiveSites" -SkipEngine -SkipGoldens`:
28 passed, 0 failed, 0 skipped of 28.
`.\RunTests.ps1 -Suite "target-class-cycle,mode-target-table" -SkipUnits -SkipGoldens`: 2 passed,
0 failed, engine errors clean; `target-class-cycle` notes C3/M01's three cycles as 2/2/45.
`.\RunTests.ps1 -SkipUnits -SkipEngine`: 23 shots hash-identical.

**Evidence correction.** The item's `46` for `target-class-cycle` on C3/M01 is not a number the
suite prints: its note reads three cycles of 2/2/45, and its membership check builds the expected
Non-Aircraft count from the world (`parts.Count + liveGuns`), not from this file. `-Suite` takes
one string, so the two suites go in as `-Suite "target-class-cycle,mode-target-table"`.
`docs/org/targeting.md:943` still names `ObjectiveSites.CollectOtherTargets`; it is outside this
item's file ownership and left untouched.

**Original approach (kept for reference).**

**Goal.** One method collects a flagged class of targets, and its name says which flag.

**Evidence (confidence: traced).** `CSVM/src/Session/ObjectiveSites.cs:97-120` and `:128-152`:
`CollectTargets`/`CollectOtherTargets` are the same two loops differing in `Objective`/
`OtherTarget`, `other: false/true`, `graph.ObjectiveTargets`/`graph.OtherTargets`; `CollectTargets`
collects objective-flagged targets only. Duplicated Code, Mysterious Name.

**Approach.** `CollectFlagged(TargetFlag flag)` taking the class, the graph list and the `other`
bit from one small table; the two callers name the flag. C27 edits the same file's script-key
match; land B14 first.

**Model recommendation.** medium, low effort.

**Verify.** `target-class-cycle` (46 on C3/M01) and `mode-target-table` suites green; 23 goldens
identical.

## B15 ☑ `CameraController` reads its tuning through `_camParams` and names its views

**Landed.** The ten copied tuning fields (`_dist`, `_distFactor`, `_distMin`, `_distMax`,
`_distVary`, `_distCatchUp`, `_crashHoriz`, `_crashY`, `_backMin`, `_backMax`) are gone and every
use site reads `_camParams.<X>`, so one plane's tuning has a single copy; `CamParams` is only ever
written by its own `Load`, so the live read is the copy's value in every case. The six log
sentinels (`BackViewLog` to `DeathViewLog`) are replaced by a `CameraView` enum (Chase, Fixed,
Back, PadLook, Cockpit, Nose, Flyby, Death) that `LogView` maps to the breadcrumb's name in one
`ViewName`, with the numpad pose's row of the view table handed alongside as `fixedView`.
`FlightController` names the view each arm took instead of overwriting the fixed-view index with a
negative marker. `PinnedBackView`/`PinnedFlybyView` stay as they are: they are the `--view=`
digit space, which `SessionSpec.View`, `FlightRosterInputs`, `FlightPolicy` and
`FlightController.PinnedView` carry as an int and `SessionSpec` prints into a warning line, so
typing that identity is a separate change reaching well past this file. Every numeric value, log
name and persisted value is unchanged.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln -t:Rebuild` 0 warnings, 0
errors; `CheckCommentCaps.ps1` and `CheckDocEntries.ps1` both clean; `RunTests.ps1 -UnitFilter
"FullyQualifiedName~Camera" -SkipEngine -SkipGoldens` 64 passed, 0 failed, 0 skipped of 64; the
engine suites `blacke-drop-cameras`, `campaign-wingwalk-camera`, `death-camera`, `flyby-camera`
(selector `camera`), `look-stick`, the four `cockpit` suites and `campaign-mission-end` all pass
with engine errors clean; `RunTests.ps1 -SkipUnits -SkipEngine` reports 23 shots hash-identical.
`docs/architecture/Flight.md` needed no edit: its entry already names the `CamParams` members the
radius law reads and says nothing about the log markers.

**Original approach (kept for reference).**

**Goal.** The chase camera reads its per-airframe tuning from one record, and a view identity is
a named value.

**Evidence (confidence: traced).** `CSVM/src/Flight/CameraController.cs:131` adds `_camParams`
while ten fields (`_dist`, `_distMin`, `_distVary`, `_crashHoriz`, ...) stay copied out of the
same block, against its own comment ("two copies of one plane's tuning"); `PinnedFlybyView = 11`,
`FlybyViewLog = -6`, `DeathViewLog = -7` join an int-sentinel view identity. Data Clump,
Primitive Obsession.

**Approach.** Drop the copied fields and read `_camParams.X` at the use sites; an enum (or the
existing view enum if one is there) for the identity, with the log sentinel values mapped at the
one place they are written. No numeric change anywhere.

**Model recommendation.** medium.

**Verify.** The six chase-camera units, `campaign-mission-end`, the death and flyby suites green;
23 goldens identical, the chase shots in particular.

**⚠ Traps.** `BL-837` (the easing clock) is open on this file; do not fold it in.

## B16 ☑ `CinemaSkips` without the repeated arms and the mirror enum

**Landed.** `Skips` is one expression: a press that is not `CinemaPress.None` ends the cinema when
the set carries `AnyPress` or carries the flag that press is named by. The six arms became a private
`NamedFlag(CinemaPress)` that answers the set's flag for a named press and `CinemaSkip.None` for a
press no set names by itself, and the caller tests it with `&` rather than `HasFlag`, since
`HasFlag(None)` is true for every set. The `CinemaPress` doc now states why the two enums stay
separate, and `docs/architecture/UI.md`'s entry says it in one sentence. No behaviour change: the
truth table is identical for all seven presses against all three authored sets.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` clean, 0 warnings 0 errors.
`.\CheckCommentCaps.ps1` and `.\CheckDocEntries.ps1` both clean.
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~Cinema|FullyQualifiedName~BootSequence" -SkipEngine
-SkipGoldens`: 124 passed, 0 failed, 2 skipped of 126 (the two skips want extracted film data),
including `TheBootSequenceTakesAnyPressThereIs` over all six presses.
`.\RunTests.ps1 -Suite cinema-skip-pad -SkipUnits -SkipGoldens`: 1 passed, 0 failed, engine errors
clean.

**Evidence correction.** `CinemaPress` does not mirror `CinemaSkip` member for member, so the mirror
enum stays. `CinemaSkip` is a `[Flags]` set of any number of bits and a `CinemaPress` is the single
press that arrived, and the member lists differ at both ends: `CinemaPress.OtherKey` is a press no
set names on its own, and `CinemaSkip.AnyPress` is a set no press answers to. Collapsing them would
either make an unnamed key indistinguishable from Escape or put a wildcard in the press type.

**Original approach (kept for reference).**

**Goal.** The skip decision is one expression, and there is one enum for what a press is.

**Evidence (confidence: traced).** `CSVM/src/UI/CinemaSkips.cs:41-50`: five of six arms repeat
`|| skip.HasFlag(CinemaSkip.AnyPress)`; `CinemaPress` mirrors `CinemaSkip` member for member.
Duplicated Code.

**Approach.** Hoist the `AnyPress` test out of the arms; if `CinemaPress` and `CinemaSkip` are
the same set, the press is a `CinemaSkip` value and the mirror goes. Run after B13 since both
touch `BootSequence.cs`.

**Model recommendation.** medium, low effort.

**Verify.** `cinema-skip-pad` suite and the skip units green, including "any" on the boot
sequence meaning any input (`BL-810`).

## B17 ☑ The export set out of `SelectionService`

**Landed.** `CSVM/src/UI/ExportSet.cs` is a new `Node` holding the members, their cyan outline
boxes, the toggle, the clear and the combined glTF write (the last moved off `NodeLab`). The node
lab owns it, adding it as a child in `_Ready` so a Ctrl+click set is gathered whether or not the
panel is opened. `SelectionService` keeps pick, ladder and HUD and reports a modified pick through
a new `CtrlPicked` event; a tool appends its own breadcrumb line through the new `HudLine`
supplier and re-renders it with `RefreshHud`, which keeps the HUD text identical without the
service knowing what an export is. The wireframe helpers `NewBoxInstance` and `DrawBox` became
`internal static` there, alongside `SubtreeWorldAabb`, since both tools draw the same box.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` clean, 0 warnings;
`CheckCommentCaps.ps1` and `CheckDocEntries.ps1` both clean. Units: 20/20 passed on
`FullyQualifiedName~Export`, 49/49 on `~Selection`. Engine suites `terrain-pick-export` (the set's
own suite: two Ctrl+clicks, the merged glTF read back, the nested-member case, then clear) and
`nodelab-visibility` both PASS with engine errors clean. Log lines, HUD text and the glTF call are
unchanged by construction. A manual Ctrl+click check from a `--freecam` session would confirm the
part no suite drives: that a real Ctrl+click still reaches the set through the new event, that the
cyan box appears and rides a moving member, and that the breadcrumb's `export set N node(s)` line
reads as before.

**Original approach (kept for reference).**

**Goal.** `SelectionService` owns pick, ladder and HUD; the node lab's Ctrl+click export set
lives in its own module.

**Evidence (confidence: traced).** `CSVM/src/UI/SelectionService.cs` gained `ExportSet`,
`_setBoxes`, `ToggleInSet`, `ClearSet` with the node lab's glTF export. Divergent Change.

**Approach.** An `ExportSet` class under the node lab's namespace holding the set and its boxes;
`SelectionService` exposes the picked node it already has. The lab stays opt-in (no UI until its
key).

**Model recommendation.** medium.

**Verify.** The node lab's export unit tests green; a manual Ctrl+click set export from a
`--freecam` session produces the same glTF; `docs/architecture/UI.md` entry and cap.

## B18 ☐ Four small ones: the `MSG_` sniff, `SetTeam`'s hidden order, a hull's `ForAircraft`, `Messages.Parse`

**Goal.** Four one-site corrections with no behaviour change.

**Evidence (confidence: traced).**
- `CSVM/src/Session/SurfaceVehicleRuntime.cs:176` `return text.StartsWith("MSG_", ...) ? "" : text;`
  where `Messages.Get` echoes the key on a miss, so `text == key` is the exact test.
- `CSVM/src/Session/ZeppelinRuntime.cs:159` `FanTeamsOntoTurrets` caches `_turrets` as a side
  effect; `SetTeam` fans onto zero guns if it was never called.
- `CSVM/src/Flight/TargetPool.cs:163` routes a surface hull through `TargetRef.ForAircraft`.
- `CSVM/src/Mech3/Messages.cs` `Parse(string)` has one caller, in `CSVM.Tests`.

**Approach.** The exact key-miss test; `_turrets` resolved lazily by the method that reads it;
a `TargetRef.ForHull` (or a class-neutral factory) for the hull; `Parse` made `internal` with
`InternalsVisibleTo`, or the test reads through the public loader.

**Model recommendation.** medium, low effort.

**Verify.** `SET_AI_TEAM` suite on C4/M05 and C2/M05, `ai-vessel-targets`, the autodock prompt
unit (`BL-510`) and the `Messages` tests green.

# Wave C — Spec fixes

## C21 ☑ `New-ItemId.ps1` gets its BOM and loses its em dashes

**Landed.** The two em dashes in the header comment are a comma and a parenthesis, which leaves
the file pure ASCII (highest byte 0x7D), the stronger of the two rules `CLAUDE.md` gives, so no
BOM is added.

**Verified.** <pending orchestrator run> A byte scan of the file finds nothing over 0x7F.

**Original approach (kept for reference).**

**Goal.** The mandatory id minter is a file PowerShell 5.1 cannot mangle.

**Evidence (confidence: traced).** `Z:\CSVM\New-ItemId.ps1` starts `3c 23 0d` (no BOM) and holds
two U+2014 in comments; `BL-786`'s closing commit noted it and left it. `CLAUDE.md` says a
BOM-less `.ps1` with non-ASCII is mangled by the interpreter itself.

**Approach.** Replace the two dashes with commas, and write the file back with a UTF-8 BOM (or
keep it pure ASCII, which is the stronger rule in `CLAUDE.md`). Land before A7.

**Model recommendation.** medium, low effort.

**Verify.** `Format-Hex` shows `EF BB BF` or no byte over 0x7F; `.\New-ItemId.ps1 -Kind BL`
mints the next id and the counter file advances by one.

## C22 ☐ `SetNet` reports a missing net as no move

**Goal.** A `SET_AI_NET` naming a net the chapter does not carry is reported as not applied.

**Evidence (confidence: traced).** `CSVM/src/Session/ZeppelinRuntime.cs:259-263` returns `true`
when no such net exists, so `CSVM/src/Session/CampaignDirector.cs:1787` counts `moved++` and
`Report` prints the clause as applied while the airship keeps its route. `BL-502`.

**Approach.** Return `false` on the missing net and log the name at `Log.Warn` under the
director's category; the count then says what happened.

**Model recommendation.** medium, low effort.

**Verify.** A unit with a constructed clause naming `no_such_net` reads `moved == 0` and the
warning; the C4/M05 and C2/M05 pins unchanged.

## C23 ☐ A death choreography is only the def's death sequence

**Goal.** A `dbase` deactivation counts as the def's own death only when it is inside the death
sequence, as `BL-733`'s entry asked; an authored repair elsewhere still revives.

**Evidence (confidence: traced).** `CSVM/src/Mech3/AnimRuntime.cs:2005-2011`
`AuthoredInDeathChoreography` matches an event in any non-ON_CALL sequence. The census at close
says no shipped def authors a repair, so this is latent.

**Approach.** Match on the sequence the def's death chain names (the fourteen defs in three
families the `death-not-a-revival` suite reads), not on "not ON_CALL". If the death sequence is
not nameable from the def, record why on `docs/org/vehicleDamage.md` and close this item as
disproven.

**Model recommendation.** medium.

**Verify.** `death-not-a-revival` green on the Barracuda and a lifesaver; a constructed def with
a repair in a non-death sequence revives.

## C24 ☐ `KeepsTheShot` holds only the episode that raised the ending

**Goal.** An episode that starts after a campaign result exists can still hand off and be
skipped.

**Evidence (confidence: traced).** `CSVM/src/Session/CutsceneController.cs:721-736`
`KeepsTheShot` is gated on `EndingLanded`, which is `_campaign?.Result != null`
(`CSVM/src/Session/GameSession.cs:573`), not on this episode having raised the ending; `Begin`
clears `HeldForEnding` but not that gate, so the hand-off at `:471-478` and the skip at `:491`
are blocked for every later episode. `BL-739`.

**Approach.** Latch "this episode raised the ending" in `Begin`/the result hook, and gate
`KeepsTheShot` on the latch. Run after A3 (`GameSession.cs`).

**Model recommendation.** high. The hold is what makes CM14's docking fade copy the film's last
frame; a wrong gate either brings the flash back or freezes the next episode.

**Verify.** `campaign-mission-end` runs all three fade paths green; a new phase starts a second
episode after a result and reads it handing off; `PT-137` stays owed for the look.

## C25 ☐ A queued start past the budget still gets its zero-dt advance

**Goal.** Every instance queued by the walk receives its zero-dt first advance, whatever the
per-pass budget.

**Evidence (confidence: traced).** `CSVM/src/Mech3/AnimRuntime.cs:2160-2170`
`DrainQueuedStarts` calls `_queuedStarts.Clear()` past the 512 budget, so the overflow's first
advance is the next walk's real dt, which the same commit's warning at `:2181` forbids. `BL-677`.

**Approach.** Drain the budget and keep the remainder queued for the next pass (or advance the
remainder at zero dt without dispatching its events); pin with a queue of 600 constructed
starts.

**Model recommendation.** medium.

**Verify.** `anim-call-start-order` suite green; the new unit reads all 600 advanced at zero dt;
23 goldens identical.

## C26 ☑ A turret voice culls at 1.1x its range, as decoded

**Landed.** `GunVoice` carries a `CullMargin` constant of 1.1 with the decode pointer on it, and
the cull is `cue.RangeMax * CullMargin` rather than `RangeMax` itself. The `RANGE` pair still
drives the Godot attenuation curve (`UnitSize`, `MaxDistance`), so the two distances are now
different numbers and `Emitter()` returns both: a suite that read only `MaxDistance` could not
tell a 1.0x cull from a 1.1x one. Both voice suites hold the 1.1 figure themselves rather than
reading it off `GunVoice`, and each now places the listener twice past the audible distance, at
1.05x (still heard) and 1.15x (silent), so the margin is checked as behaviour and not only as a
field. The turret suite's old far placement at 4x the audible distance passed at either
multiplier and is replaced by that pair. The `sound` log and the suites' notes print the audible
distance and the cull separately, 200 m and 220 m for `snd_chaingun`, 400 m and 440 m for
`snd_turretgun`. `docs/architecture/Flight.md`'s `GunVoice` entry states the 1.1 and points at
the decode page.

**Verified.** <pending orchestrator run> The decode line is `docs/formats/turrets.md:159`:
"Attenuation is the definition's own `RANGE` pair, silent past 1.1 x its audible distance
(`FUN_00597c20`)", and `docs/formats/sounds.md:24` gives the pair as
`RANGE [fullVolumeDist, audibleDist]`, so the quantity multiplied is `RangeMax`.
`dotnet build CSVM/CSVM.sln` 0 warnings, 0 errors. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1`
and `CheckEncoding.ps1` clean. `RunTests.ps1 -Suite "turret-gun-voices,surface-gun-voices"
-SkipUnits -SkipGoldens`: PASS, 2 passed, 0 failed, engine errors clean. With the constant
temporarily set to 1.0 the same run is FAIL, 0 passed, 2 failed: `turret-gun-voices` loses the
emitter pin, the 1.05x hearing check and the carried gunner's matching cull, and
`surface-gun-voices` loses the emitter pin and the 1.05x hearing check on both the patrol boat
and the turret truck. No `GunVoice` unit test exists in `CSVM.Tests`.

**Original approach (kept for reference).**

**Goal.** The turret gun voice is silenced past 1.1 times the RANGE pair's audible distance, the
figure the decode and `docs/formats/turrets.md:159` state.

**Evidence (confidence: traced).** `CSVM/src/Flight/GunVoice.cs:54` `_cullSq = cue.RangeMax *
cue.RangeMax` is 1.0x; the `turret-gun-voices` and `surface-gun-voices` suites pin 200 m.
`BL-793`.

**Approach.** Multiply by 1.1 with the decode pointer on the constant; re-pin the two suites'
distance with the cause in the commit.

**Model recommendation.** medium, low effort.

**Verify.** Both suites green at the new distance and able to fail at 1.0x.

## C27 ☐ `ADD_OTHER_TARGET` and its remove reach the mode table's points

**Goal.** A script that adds or removes an `other_target` reaches the same TRAVELERS point the
objective keys do, as `BL-400`'s commit claims ("script ADD/REMOVE honoured").

**Evidence (confidence: traced).** `CSVM/src/Session/ObjectiveSites.cs:173` matches only
`Add/RemoveObjectiveTarget`, so an `ADD_OTHER_TARGET` key falls back to node bounds, the 7.9 km
failure its own warning describes. Whether any shipped script carries the key is not read.

**Approach.** Census the 53 shipped tables and the scripts for the other-target keys first. If
none ships, record that on the targeting docs page and match the keys anyway (one line). Run
after B14.

**Model recommendation.** medium.

**Verify.** A unit with a constructed `ADD_OTHER_TARGET` reads the TRAVELERS point;
`target-class-cycle` green.

## C28 ☑ The goldens README count and the re-pinned `exercises` fields

**Landed.** The README's opening sentence no longer carries a number: it reads "Pinned `--det`
captures, each reduced to one md5; `manifest.json` lists them and is where the count is read", so
adding or dropping a shot cannot make it wrong. Five `exercises` fields name what their shot now
covers. `c2-city` gains the two mesh point lights that author no fade band and are therefore not
culled at 5 m. `c1-flight`, `c1-targeting-hud` and `campaign-4p-grid` each gain the one distant C1
point light that draws under its own authored fade band instead of a blanket lens-flare reach.
`c1-stunt-marker` gains the player's ground shadow, which its low C1 pass takes by the same
mechanism as the four shots re-pinned beside it. `campaign-4p-grid` also loses "in every pane",
which was wrong: the shadow shows in three of its four panes. No hash, frame, name or argument
line was touched. Six shots keep their field because it already names the mechanism that moved
their pixels: `c1-crash` ("pooled repeat calls", "distance puffers"), `c1-debris-rest` ("its
landing's own spark puffer"), `c1-destroy-effects` ("seeded puffers", "the player's ground
shadow"), `c1-ai-wreck` ("the handover fireball", "the player's ground shadow"), `c1-cockpit`
("the same held flight") and `empty-stage` ("quaternion attitude integration, chase camera").

**Verified.** <pending orchestrator run> `CheckGoldenProse.ps1`: "golden manifest prose within
contract"; the longest field is 248 characters. `CheckEncoding.ps1`: "no mojibake".
`git diff --stat` names `analysis/goldens/README.md` and `analysis/goldens/manifest.json` only.
`git diff -U0` on the manifest yields ten changed lines, every one of them containing
`"exercises"`, and no changed line matching `"hash"`, `"frame"`, `"name"` or `"args"`. The file
read `-Raw -Encoding utf8` still parses through `ConvertFrom-Json` at 23 shots.

**Original approach (kept for reference).**

**Goal.** `analysis/goldens/README.md` states the pinned count, and every shot re-pinned in the
range carries an `exercises` field that says what the shot covers today.

**Evidence (confidence: traced).** The README's first line says "Twenty-one pinned"; the
manifest holds 23. `README.md:55`: "Rewrite it on a re-pin; never append." Twelve of the
thirteen orch-2 re-pins keep an unchanged `exercises` (the eight in `4a2be0c7`, `c2-city` in
`74c5073a`, the three in `120be7ec`); in orch-3, `c1-stunt-marker` was not rewritten on
`d3d5dc25` and `BL-443`'s eight re-pins left all eight byte-identical. `120be7ec` re-pinned in
its own commit, which the README forbids; that is history and is recorded here only.

**Approach.** Rewrite each named shot's `exercises` as a present-tense statement of what it
covers (under 250 characters, no ids, no dates: `CheckGoldenProse.ps1` enforces it); state the
count as a word the next re-pin will not break, or drop the number.

**Model recommendation.** medium, low effort.

**Verify.** `CheckGoldenProse.ps1` clean; no hash changes in the manifest diff.

## C29 ☐ Three stale doc lines after the landings

**Goal.** Live prose matches the tree after the range.

**Evidence (confidence: traced).** `docs/PLAN-public-release.md:1105` "neither has a landing
commit" (`BL-538` has one); `PROJECT_CONTEXT.md` "Next" names `BL-750` to `BL-769` with `BL-758`,
`BL-759` and `BL-769` closed in orch-3; `docs/architecture.md:196` still says the gun voice is
"moved to the muzzle each round leaves from" after `BL-820` moved it to the hull origin.

**Approach.** Correct the three lines; the "Current status" edit may only shorten that section.

**Model recommendation.** medium, low effort.

**Verify.** `CheckDocEntries.ps1` clean; `git log --grep=BL-538` shows the landing the plan line
now cites.
