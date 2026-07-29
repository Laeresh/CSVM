# SessionSpec — one parsed, resolved value for the launch args

**ACTIVE PLAN** (written 2026-07-25). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans.md), when every item lands.

`PlaneViewer.cs` is 4,157 lines, of which `_Ready` is 571 and `StartSession` 1,350. It carries 153
private fields and 112 arg-parse branches; ~108 of those fields are launch args, and the ~183 sites
that read the nine mode bools are spread across every phase of the build. Nothing about the parse or
the resolution is reachable from `CSVM.Tests` — `PlaneViewer` is a `Node3D` whose inputs come from
`OS.GetCmdlineUserArgs()`. This plan extracts a Godot-free **`SessionSpec`** that owns **parse *and*
resolution**, deletes the arg fields, and rewrites the call sites to read it. It folds in the closed
`SessionMode` enum (candidate 02 of the 2026-07-25 architecture review) because every call site is
being edited once already, and a second pass over the same 183 sites later would not be defensible.

**Out of scope, deliberately.** The 45 runtime-state fields (`_camera`, `_plane`, `_worldRoot`,
`_clock`, `_selection`, the lab handles) stay exactly as they are — this plan is about *args*, not
about session state. `StartSession`'s body is not restructured: its 28 `AddChild` calls, the
`StartupProfile` `Mark`/`Record` pairs and the lab wiring belong to candidate 07, and sites there
change only where they read a field this plan deletes. Two fields that look like args are runtime
state and stay mutable, renamed to say so: `_screenshotPath = null` (shot taken) and `_debugJoin = 0`
(join consumed).

## Milestone goal

- One `SessionSpec.Parse(args)` produces an immutable, fully-resolved spec: raw values parsed, mode
  arbitrated, `--det` membership resolved, placement normalised, `IsScripted` / `SeedPinned` /
  `BuildsCollision` exposed as computed properties with exactly one definition each.
- `SessionMode` is a closed set — `Menu`, `Fly`, `Viewer`, `Freecam`, `AnimLab` — resolved by one
  total function, with `Stunt`/`Players`/`EmptyStage`/`NodeName`/`DamageLab` as modifiers and
  `WorldMode` derived. Precedence stops being statement order in five mutating `if` blocks.
- The launchscreen derives a new spec from the pristine CLI spec rather than overwriting fields, so
  the three carry-over patches in `StartSessionFromMenu` become construction.
- The resolution rules — today expressed only as statement order, comments, and four hand-written
  predicates — are asserted in `CSVM.Tests`, engine-free.

**`StartSession`'s build sequence is not touched.** The ordering constraints there live in comments
and are load-bearing; conflating them with an args refactor would make the equivalence gate below
meaningless, because a dirty diff could no longer distinguish a mis-resolved rule from a mis-moved
`AddChild`.

## Decisions (2026-07-25)

Settled in a grilling session before any code was written. This table is the authority where the
prose disagrees with itself.

| # | Question | Decision |
|---|---|---|
| 1 | Does the spec own just the parse, or the resolution too? | **Parse + resolve** — the four disagreeing predicates the review found are all *post*-arbitration derivations, so a parse-only spec leaves the entire bug class in place. |
| 2 | What happens to the 153 fields and ~183 call sites? | **Delete the arg fields, rewrite every site.** Rejected: get-only delegating properties (keeps a shim), and assign-once fields (keeps two sources of truth — the exact smell being removed). |
| 3 | What is the safety net for a ~1,000-line hand edit? | **A `--dump-session` probe, baselined *before* the refactor.** This change alters *resolution*, not rendering; a pixel hash is an indirect and insensitive instrument for it — two specs can resolve differently and render identically at frame 15. |
| 4 | Enum or bools on the spec? | **Fold candidate 02 in — a closed `SessionMode`.** Every site is being edited once already; a second 183-site pass later is indefensible, and the arbitration becomes a total function with a truth table. |
| 5 | What does a menu launch derive from? | **The pristine CLI spec, via `SessionSpec.FromMenu`.** The three patches in `StartSessionFromMenu` all guard against state carried over from the last session; deriving fresh makes that structural. |
| 6 | Does `--dump-session` survive? | **No — scaffolding, deleted in B8.** Accepted with a named risk (see the ⚠ table); the xUnit truth table in B7 is what makes it acceptable. |
| 7 | How deep does the test surface go? | **The full resolution truth table**, 40–60 facts, including the 10 pure parsers currently trapped behind `private`. |
| 8 | One change or a plan? | **A plan with a parallel-run equivalence gate (A4).** The risky step starts from proof rather than hope, and a divergence is caught at the rule that diverged. |

**If A4 shows the menu path resolving differently** under derive-fresh than under
accumulate-and-patch, stop and surface the diff. Decision 5 chose the pristine base; it did *not*
authorise absorbing behaviour changes silently — that option was offered and declined, because it
would weaken the equivalence gate exactly where pixel coverage is thinnest.

## ⚠ Read this before implementing anything

The architecture review that produced this plan got two things wrong. Both were corrected on
2026-07-25, but the report as first written is still in circulation.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "There are eight in-engine suites." | There are **nine** — `flight-envelope` landed with the thrust calibration. `Suites.cs:42-59` is the authority. |
| 2 | "`docs/architecture.md:1438` says seven suites and has drifted." | It already said nine; commit `b357e31` had fixed it. Only `docs/cli.md` had drifted, and that is now also fixed. |

**⚠ The pixel goldens do not cover the modes this plan touches most.** Measured against
`analysis/goldens/manifest.json`: `--freecam` has 8 shots, `--viewer` 1, flight 1, `--stage=empty` 1
— and **`--anim-lab` (32 field sites), `--stunt` (13), splitscreen and the whole menu re-entry path
have none at all**. A green `RunTests.ps1` is therefore *not* sufficient evidence for B5 or B6. That
is the entire reason A1 and A4 exist.

**⚠ Deleting the scaffold (B8) is a knowingly accepted risk.** `analysis/README.md` records that
`.scratch/probe_exempt.py` — the static-collider probe — no longer exists anywhere, so a backlog item
now means "rewrite the probe first". If B7's truth table gets trimmed under time pressure, reopen
decision 6 rather than shipping B8 with thin tests.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Prove equivalence before changing anything

1. ☑ `--dump-session` scaffold + the baseline resolution matrix
2. ☑ `SessionSpec` + `Parse`, raw values only, not yet consumed
3. ☑ Resolution: `SessionMode`, precedence, modifiers, `WorldMode`, the `--det` bundle, `BuildsCollision`
4. ☐ **Gate:** parallel run — probe prints `field | spec`, exits nonzero on any mismatch

### Wave B — Migrate, then lock it in

5. ☐ Delete the arg fields; rewrite the ~183 call sites
6. ☐ `SessionSpec.FromMenu` and the launchscreen re-entry
7. ☐ The xUnit resolution truth table
8. ☐ Delete the scaffold; `architecture.md` entry; `HISTORY.md`

## Dependency and parallelism notes

Strictly linear; no parallelism, and **no worktrees** — every item edits `PlaneViewer.cs`, so two
concurrent agents would contend on the one file this plan exists to change.

A1 must land before A2 or the baseline records post-refactor behaviour and proves nothing. A2 → A3
is a chain. **A4 is a hard gate: B5 does not start until it is green.** B5 → B6 (the menu factory
needs the fields gone, or it writes to both). B7 can be drafted alongside A3 — it tests
`SessionSpec` directly, not `PlaneViewer` — but it cannot be *complete* until B6 lands `FromMenu`.
B8 is last by definition: it deletes the instrument the earlier items are verified with.

---

# Wave A — Prove equivalence before changing anything

## A1 ☑ `--dump-session` scaffold + the baseline resolution matrix

**Landed.** `--dump-session` prints 124 resolved launch settings as sorted `key = value` text and
quits with `Probes.Session`'s verdict. `analysis/session-baseline/` holds `capture.ps1` (the
50-command-line matrix), the committed `baseline.txt`, and `FINDINGS.md`.

**Verified.** Two captures of the same build are md5-identical
(`944310579BA214A0E99B801FC344098B`). Able-to-fail on a perturbed build: renaming `--stunt`'s forced
scenario moved exactly the `stunt-*` rows, and a duplicate field key printed `!! duplicate key` and
**exited 1**; after reverting both, the baseline reproduced byte for byte. `.\RunTests.ps1` PASS —
152 units, 9/9 suites, 11/11 goldens hash-identical, exit 0.

**The baseline lives in `analysis/`, not `.scratch/`.** `.scratch/` is swept by `CleanScratch.ps1`
and this file has to survive to A4 and beyond; `analysis/README.md` records what that mistake already
cost once. `--dump-session` is still deleted by B8 — the matrix and the baseline are not.

**Three instrument properties had to be established before the baseline meant anything**, each a
defect in the first draft found by reading output rather than by reasoning: a clock-derived master
seed made the baseline differ from itself (now `<clock>` unless pinned, rule 118); absolute paths
made it one machine's (now `{data}`/`{repo}`); and the report must be ASCII + LF, because the
PowerShell 5.1 harness turns an em dash into mojibake that reads as a diff on every row.

**⚠ For A3 — two latent defects the baseline exposed.** Both recorded, neither fixed, because A1
records current behaviour including its warts:
1. `_dumpFlight` is missing from the `_mode` "dump" chain, so a `--dump-flight` run logs as
   `menu-*.log` (row `probe-dump-flight`: `mode.name = menu`). A fifth copy of the rule-116 drift.
2. `--run-tests` resolves `mode.showsMenu = true` (row `probe-run-tests`) — it is saved only by
   returning before the menu branch. The harness's correctness rests on statement order in `_Ready`,
   which is precisely what A3 replaces.

**⚠ The observer must not be a term of what it observes** (`docs/verification.md` rule 117).
`--dump-session` meets the `--det` membership rule *and* the `_mode` naming rule, and obeying either
destroys the instrument: implying `--det` prints `det.on = true` on every row of a matrix whose
subject is which command lines turn the bundle on. It is the documented exception to both, and the
window-focus concession is taken outside the predicate rather than by adding a term to it. **A3 must
not "tidy" it back in.**

**Original approach (kept for reference).** Write the probe against today's fields, not against any
new type; run it after the whole of `_Ready`'s resolution and before `StartSession`, or it records
half-resolved values.

## A2 ☑ `SessionSpec` + `Parse`, raw values only, not yet consumed

**Landed.** `CSVM/src/SessionSpec.cs` — a `sealed record` with `{ get; private set; }` properties
grouped by the A1 dump's prefixes, plus `Parse(IEnumerable<string>)`. Referenced by nothing.

**Evidence (confidence: traced).** `CSVM.Tests` project-references `CSVM.csproj` and already tests
Godot-free types that use `Vector3`/`Basis` (`GameZTests.cs:38`), so no new assembly is needed;
`SessionPaths.cs` is the proven precedent for extracting pure logic out of `PlaneViewer`.

**Approach — the raw/resolved split, drawn where A3 needs it.** A flag records only itself. The
parse-time implications in today's loop are all resolution and were deliberately NOT carried over:
`--markers`/`--damage`/`--weapon-*` no longer set `Viewer`, `--damage-test`/`--effects-test` no
longer set `Freecam`, `--weapon-test` no longer sets `Viewer` (A3's `Probe?` trap), `--play-anim=`
and `--debug-anim-ui` no longer set `AnimLab`. `_camDir` has no raw twin at all — it only ever
exists as routed `--direction`. Path flags are override values, null when unset. Of the A1 dump's
124 settings, the ~40 that are derivations (`mode.name`, `mode.world`, `mode.showsMenu`,
`run.scripted`, `det.on`/`via`/`scriptedBy`/`masterSeed`/`seedPinned`, the resolved
`place.*`, `collision.builds`, the `path.*Overridden` pairs) are absent by construction.

**Seven parsers moved**, all `public static` so B7's truth table reaches them: `ParseVec3`,
`ParsePlanes` (now returns the list instead of writing two fields), `ParseView` (returns 0 for a
bad digit; the caller reports it), `ParseHold`, `ParsePaintColors`, `ParsePaintDecals`,
`ParseDamagePreset` (skipped pairs go to an optional `rejected` list instead of `GD.Print`).
`Deprecated()` became a local dedup inside `Parse`. The two lab grammars — `UI.NodeLab` and
`UI.WorldDamageLab.ParseDebugSpec` — stayed put: they are their lab's vocabulary and they
`Log.Warn` as they filter, so importing them would import the logging.

**Verify.** `.\RunTests.ps1` PASS — 152 units, 9/9 suites, 11/11 goldens hash-identical, exit 0;
`dotnet test` 152/152. A1's 50-row matrix re-captured and md5-identical to the committed baseline
(`944310579BA214A0E99B801FC344098B`), which is the load-bearing check: no call site changed, so no
resolution could.

**⚠ Traps.**
1. **`Parse` must stay pure**, or B7 cannot call it: `CSVM.Tests` has no Godot runtime, and
   `Log.*` ends in `GD.Print`. The three side-effecting branches are recorded, not performed —
   `--no-pads` does not touch `Pads.Disabled`, `--tex-*` does not touch `TextureDropIn`, `--log=`
   does not `Log.Configure` — and warnings accumulate in `Warnings` as `(category, message)`.
   A3/B5 must apply and emit them; **do not "fix" this by logging from the spec.**
2. `record` + `private set` still allows `with` **inside** the type, which is what B6's `FromMenu`
   needs; from outside the spec is closed.
3. The parse-time warnings (`--view=`, `--collision=`, `--damage=`) will be emitted later in the run
   than they are today once B5 lands. Log ORDER moves; no resolved value does.

## A3 ☑ Resolution: `SessionMode`, precedence, modifiers, `WorldMode`, the `--det` bundle, `BuildsCollision`

**Landed.** `SessionSpec.Parse` now returns a **resolved** spec: `SessionMode` (`Menu`/`Fly`/
`Viewer`/`Freecam`/`AnimLab`) plus `SessionProbe`, arbitrated in one private `Resolve()`. Still
referenced by nothing.

**Evidence (confidence: traced).** Precedence today is statement order at `PlaneViewer.cs:748-783`
(`AnimLab > Freecam > Viewer > Fly`), with `--stunt` forcing fly, `--node=` forcing viewer unless
anim-lab, and `--stage=empty` rejected in viewer/anim-lab/node. `_worldMode` is derived at `:827`.
`BuildsCollision` was consolidated on 2026-07-25 and has four sources. `--det` membership was
corrected the same day and now covers every flag that drives and ends a session by itself.

**Approach — votes in, one answer out.** Every flag that names a mode is a *vote*, gathered before
anything is arbitrated: the viewer's are `--viewer`/`--damage`/`--markers`/`--weapon-*`, the
freecam's `--freecam`/`--damage-test`/`--effects-test`, the anim lab's `--anim-lab`/`--play-anim=`/
`--debug-anim-ui`. That is where the parse-time implications A2 refused to carry now live, so the
five mutating `if` blocks operate on locals and end in one `Mode` assignment. `Fly`/`Viewer`/
`Freecam`/`AnimLab` are computed from it — one definition each — and eight more predicates are
computed rather than stored: `ShowsMenu`, `ModeName`, `IsScripted`, `ScriptedBy`, `Det`, `DetVia`,
`SeedPinned`, `PadsDisabled`, `BuildsCollision`. Only what resolution genuinely *overwrites* is
stored (mode, its modifiers, `WorldMode`, `Scenario`, `Players`, `ChapterGiven`, `View`, the three
`--freecam`/`--anim-lab`-only debug tools, `SpawnIndex`, `JitterDeg` and the placement quintet).

`PinnedSeed` is `null` when the seed is unpinned rather than drawing `Rng.TimeSeed()`: a spec that
read the clock would not be a function of its args, which is the defect A1 had to fix in the probe
(rule 118). The clock draw stays with the caller. Likewise `PadsDisabled` is a value, not a write to
`Pads.Disabled`. The two lab spec grammars gained an optional `rejected` list — supplied, they hand
tokens back as data instead of `Log.Warn`ing them — so `--debug-nodelab=`/`--debug-damage=` can be
normalised here without a Godot runtime under it.

**Verify.** `.\RunTests.ps1` PASS — 152 units, 9/9 suites, 11/11 goldens hash-identical, exit 0;
A1's matrix re-captured md5-identical (`944310579BA214A0E99B801FC344098B`). Nothing calls the spec,
so neither could have moved — the real evidence is a throwaway xUnit check that replayed all 50
baseline command lines through `SessionSpec.Parse` and compared every row the spec resolves:
**2,400 values across 50 rows, 0 mismatches**, engine-free (which independently proves `Parse` stays
GD-free). Shown able to fail: inverting `ShowsMenu` failed the run. The check was deleted rather
than kept — A4 owns the field-vs-spec gate and B7 owns the truth table — but it means **A4 starts
knowing the spec side already agrees with the frozen field side on 2,400 values**, and can
concentrate on the ~1,300 raw pass-through rows and on running both sides in one process.

**⚠ Traps.**
1. Three flags coerce the mode at *parse* time, before arbitration can see them — `--damage-test`
   and `--effects-test` force `_freecam`, `--weapon-test` forces `_viewerMode` (`:646-666`). They
   are probes wearing a mode as a disguise. Modelled as votes plus a `SessionProbe` that names the
   coercion, so the enum does not inherit the lie.
2. **Step order is the behaviour.** `--stunt` moves `Scenario` *before* arbitration can clear
   `Stunt`, so `--anim-lab --stunt` still resolves to the stunt spawn list; and the
   `--freecam`/`--anim-lab`-only debug tools are dropped *after* `--node=` has forced the viewer, so
   `--node= --debug-select=` loses the tool. Both are load-bearing and both look like tidying
   opportunities. The baseline replay covers them.
3. **Two known defects are reproduced deliberately** (A1's ⚠ list): `ModeName` omits
   `--dump-flight` from its "dump" arm, and `ShowsMenu` is true under `--run-tests`. Fixing either
   is a behaviour change that would fail A4; it needs its own item.

## A4 ☐ **Gate:** parallel run — probe prints `field | spec`, exits nonzero on any mismatch

**Goal.** For every line of the A1 matrix, the value resolved by the existing fields and the value
resolved by `SessionSpec` are proven identical, mechanically.

**Evidence (confidence: traced).** Both resolutions can coexist: A2/A3 add the spec without removing
the fields, so one run can compute and compare both. The field side only exists inside a live
`PlaneViewer`, which is why this check is the probe rather than an xUnit test.

**Approach.** `<Two-column output; a mismatch is a nonzero exit, reusing the dump exit-code contract
landed 2026-07-25.>`

**Verify.** `<All ~30 matrix lines agree. This is the gate for Wave B — record the run in the item
note when it goes green.>`

**⚠ Traps.** A green run here proves the *rules* match; it says nothing about the call-site edits in
B5, which is a different failure mode with a different signal. Do not let a green A4 be cited as
evidence for B5.

---

# Wave B — Migrate, then lock it in

## B5 ☐ Delete the arg fields; rewrite the ~183 call sites

**Goal.** The ~108 arg fields are gone; every site reads the spec; `PlaneViewer` holds one immutable
`_cli` spec and one `_spec` for the live session.

**Evidence (confidence: traced).** 95 of the arg fields are write-once in `_Ready`; only 13 are
written later, and those split into the menu re-entry (7, → B6), placement normalisation (4, → A3)
and two genuine runtime one-shots that stay.

**Approach.** `<…>`

**Verify.** `<A1's matrix re-run and diffed clean, plus RunTests.ps1. Name the uncovered modes
spot-checked by hand and say so plainly — the goldens cannot see anim-lab, stunt, splitscreen or the
menu.>`

**⚠ Traps.** This is the ~1,000-line hand edit. A mechanical slip lands silently in any mode no
golden exercises. `<…>`

## B6 ☐ `SessionSpec.FromMenu` and the launchscreen re-entry

**Goal.** `StartSessionFromMenu` builds a new spec from the pristine CLI spec instead of writing nine
fields.

**Evidence (confidence: traced).** `PlaneViewer.cs:2482-2515` writes `_chapter`, `_planeNames`,
`_planeName`, `_players`, `_menuPads`, `_stunt`, `_fly`, `_worldMode`, `_viewerMode` and conditionally
`_scenario`. Three of those are patches against carry-over, each with a comment saying so.

**Approach.** `<…>`

**Verify.** `<…>`

**⚠ Traps.** If resolution differs from today's accumulate-and-patch, **stop and surface it** — see
the note under Decisions. `_menuPads` comes from the join flow, not the CLI, so it is session state
the factory takes as an argument rather than a spec field derived from args.

## B7 ☐ The xUnit resolution truth table

**Goal.** 40–60 engine-free facts covering mode precedence, `--det` membership, `BuildsCollision`,
`WorldMode`, `FromMenu`, placement normalisation and the 10 formerly-private parsers.

**Evidence (confidence: traced).** None of these rules has any automated coverage today. Verification
rules 115 and 116, added 2026-07-25, both came from this file and both describe failure modes these
tests would have caught.

**Approach.** `<…>`

**Verify.** `<dotnet test; and each rule shown able to fail — a truth table nobody has seen break is
not yet evidence.>`

**⚠ Traps.** This item is what pays for B8. If it is trimmed, reopen decision 6.

## B8 ☐ Delete the scaffold; `architecture.md` entry; `HISTORY.md`

**Goal.** `--dump-session` is removed, the CLI flag count is unchanged at 89, and the new modules are
documented.

**Evidence (confidence: traced).** Decision 6.

**Approach.** `<A `## src/SessionSpec.cs` entry in architecture.md with its ⚠ constraints; update the
PlaneViewer entry; a dated HISTORY.md entry recording the matrix and the equivalence result.>`

**Verify.** `<RunTests.ps1 green; the flag is gone from cli.md and the CLI index; CLAUDE.md's flag
count still reads 89.>`

**⚠ Traps.** The equivalence evidence must survive the instrument's deletion — record the matrix size
and result in `HISTORY.md`, not just "verified".
