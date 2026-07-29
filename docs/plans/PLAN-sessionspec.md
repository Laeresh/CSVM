# SessionSpec — one parsed, resolved value for the launch args

**COMPLETE — 2026-07-30 — all 8 items.** Archived to `docs/plans/`; kept for its evidence,
measurements and dead ends. Statements below are as-written at the time — read them as history, not
as current state. In particular the `--dump-session` probe and `analysis/session-baseline/`'s
capture script, which most items verify against, were deleted by B8: `baseline.txt` survives as a
record that can no longer be regenerated, and `CSVM.Tests/SessionSpec{,Menu,Parser}Tests.cs` (83
facts) is what checks the launch surface now.

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
4. ☑ **Gate:** parallel run — probe prints `field | spec`, exits nonzero on any mismatch — **GREEN**, retired in B5

### Wave B — Migrate, then lock it in

5. ☑ Delete the arg fields; rewrite the ~183 call sites
6. ☑ `SessionSpec.FromMenu` and the launchscreen re-entry
7. ☑ The xUnit resolution truth table
8. ☑ Delete the scaffold; `architecture.md` entry; `HISTORY.md`

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

## A4 ☑ **Gate:** parallel run — probe prints `field | spec`, exits nonzero on any mismatch

**GREEN — Wave B is unblocked.** `--dump-session=compare` resolves every setting twice in one
process and fails on any disagreement; `analysis/session-baseline/compare.ps1` runs the whole
matrix. **50 of 50 rows agree, 5,700 field/spec value pairs compared, exit 0.**

**Evidence (confidence: traced).** Both resolutions can coexist: A2/A3 add the spec without removing
the fields, so one run can compute and compare both. The field side only exists inside a live
`PlaneViewer`, which is why this check is the probe rather than an xUnit test.

**Approach.** `--dump-session` gained an optional value rather than a new flag: the bare form still
prints exactly what the committed baseline holds (it must — a second column would break A1), and
`=compare` swaps in `Probes.SessionCompare`, which pairs the field rows against a `SpecRows(spec)`
map by key and renders `key = field | spec`, marking each disagreement inline and repeating it as a
`!!` line. It reuses the dump exit-code contract, so the verdict is the process exit.

**114 of 124 settings are compared; the report NAMES the 10 it does not**, on every row, rather
than quietly narrowing: the nine derived `path.*` values are PlaneViewer's own arithmetic over
`SessionPaths` and the `CSVM_DATA_ROOT` precedence (the spec records override *values*, not resolved
paths), and `tex.overrides` is `TextureDropIn`'s name grammar (extension stripped, `=colour` split
off, sorted) where the spec keeps the raw request. A spec key with no matching field row is also a
failure, so a renamed row shows up as something other than silence.

The matrix moved to `analysis/session-baseline/matrix.ps1`, dot-sourced by both `capture.ps1` and
the new `compare.ps1`, so the two instruments cannot drift into testing different command lines.

**Verify.** **50/50 rows agree, 5,700 pairs, exit 0.** Shown able to fail twice, each caught on the
exact row and named: forcing `--stunt`'s scenario to `stunt_flyingX` failed `stunt-bare` and
`stunt-2p` with `world.scenario field=stunt_flying spec=stunt_flyingX`, and clamping `Players` to 3
failed `--fly --players=4` with `plane.players field=4 spec=3`. `.\RunTests.ps1` PASS (152 units,
9/9 suites, 11/11 goldens hash-identical, exit 0); `baseline.txt` re-captured md5-identical
(`944310579BA214A0E99B801FC344098B`) through the refactored `capture.ps1`.

**⚠ Traps.**
1. A green run here proves the *rules* match; it says nothing about the call-site edits in B5, which
   is a different failure mode with a different signal. Do not let a green A4 be cited as evidence
   for B5.
2. **The bare `--dump-session` output is a committed baseline** — the compare column lives behind
   `=compare` for that reason. Anything that changes the bare form breaks A1's artifact.
3. Reverting the able-to-fail perturbation left the file OLDER than the DLL, so the confirming run
   re-measured the perturbation and reported the same two failures (new verification rule 124).

---

# Wave B — Migrate, then lock it in

## B5 ☑ Delete the arg fields; rewrite the ~183 call sites

**The arg fields are gone.** `PlaneViewer` lost ~108 of them plus the 120-branch parse loop, the
five mutating arbitration blocks and the seven parse helpers — **1,293 lines deleted for 479 added**
— and now holds one immutable `_cli` (what the user typed) and one `_spec` (what the live session
was built from), which 440 call sites read.

**Evidence (confidence: traced).** 95 of the arg fields were write-once in `_Ready`; only 13 were
written later, and those split into the menu re-entry (7), placement normalisation (4, landed in A3)
and two genuine runtime one-shots.

**Approach.** `_Ready` now parses once and then does only what a pure value cannot: the data-root
precedence, `Pads.Disabled`, `TextureDropIn`, `Log.Configure`, the clock-drawn master seed and the
`--det` announcement — plus emitting `Warnings`, which land before `Log.Configure` because that is
where the loop they replace raised them. Three fields stayed mutable because they are genuinely
runtime state, and are renamed to say so: `_pendingShot` (cleared when the last burst frame lands),
`_shotDelay` (the `--frames=` countdown) and `_pendingJoin` (consumed by the first launchscreen).
`_menuPads` stays session state — it comes from the join flow, not from args.

The menu re-entry became `SessionSpec.WithMenuSelection`, applied to `_spec` rather than to `_cli`:
that reproduces today's accumulate-and-patch exactly, so this item stays behaviour-neutral and B6
still owns the derive-fresh decision.

**The `=compare` gate was retired here, not deferred to B8.** It compared two resolutions; with one
of them deleted it compares the spec against itself, and an instrument that cannot fail is worse
than no instrument. `Probes.SessionCompare`, `SpecRows` and `compare.ps1` are gone. The bare
`--dump-session` and its committed baseline — the instrument that *can* still fail — stay until B8.

**Verify. `baseline.txt` re-captured md5-identical (`944310579BA214A0E99B801FC344098B`): 50 command
lines × 124 settings, unchanged with every arg field deleted.** That is this item's real gate — the
goldens cannot see resolution, and this can. `.\RunTests.ps1` PASS (152 units, 9/9 suites, 11/11
goldens hash-identical, exit 0).

**Hand-checked, because no golden covers them** — nine launches on a hidden desktop, each exit 0
with zero engine errors and the expected `startup mode=` line: `--anim-lab`, `--play-anim=` (implies
the lab), `--stunt`, `--stunt --players=2`, a 4-pane `--fly --plane=a,b,c,d` (eyeballed: four panes,
per-player aircraft alternating as asked), `--menu`, `--menu --debug-join=2` (eyeballed: P1 keyboard
+ two device-less joins), `--viewer --damage=…` and `--node=`. **Still owed: the menu → flight
re-entry itself**, which needs a keypress and cannot be scripted — it is in `playtest.md`, and it is
B6's subject.

**⚠ Traps.**
1. This was the ~1,000-line hand edit; a mechanical slip lands silently in any mode no golden
   exercises. The baseline caught resolution, but only the hand launches above cover the build path.
2. `Log.Warn` takes a `FormattableString`, so a held message needs `$"{note.Message}"` — a plain
   string does not compile against it.
3. A spot-check script wrote `--screenshot=<path with a space>` unquoted, which parked a PNG at
   `Z:\Crimson` and broke **every** Godot launch on the machine until it was removed (new
   verification rule 125).

## B6 ☑ `SessionSpec.FromMenu` and the launchscreen re-entry

**`StartSessionFromMenu` derives its spec from the pristine command line.** `SessionSpec.FromMenu`
takes the base as a *parameter*, so the caller reads `SessionSpec.FromMenu(_cli, …)` and nothing a
previous session settled can reach the next one.

**Evidence (confidence: traced).** The pre-B5 code wrote nine fields at `PlaneViewer.cs:2482-2515`;
after B5 that was one `WithMenuSelection` call on the live spec. Three of the nine were patches
against carry-over, each with a comment saying so.

**⚠ Surfaced, as decision 5 requires: deriving fresh resolves IDENTICALLY to accumulate-and-patch.**
No behaviour changed, and the reason is worth writing down rather than discovering again. `_spec` is
assigned in exactly two places — `= _cli` in `_Ready`, and the menu factory's result — so the only
fields where the live spec can differ from the pristine one are the eight the factory writes, and
the factory writes all eight unconditionally. The base therefore cannot influence the outcome. The
one field that could have differed is `PlaneName`, whose fallback fires only on an empty pick, and
`LaunchMenu.AllLocked()` returns false when no slot exists — so the launchscreen cannot fire a
launch with an empty list. **The point of the change is not the values, it is that the equality
currently holds only because three hand-written patches happen to cover the three carry-over
fields.** Deriving fresh makes it hold structurally.

**Approach.** `FromMenu(cli, chapter, planeNodes, stunt)` is `static` and takes its base explicitly,
which is what makes the base impossible to get wrong — swapping it back to the live spec is a
*compile error* (CS0026), not a silent regression. It sets Chapter / PlaneNames / PlaneName /
Players / Stunt / Mode=Fly / WorldMode / Scenario and nothing else. The player count comes from the
list length rather than a separate argument, since one plane per player is what the pick means.
`_menuPads` stays session state on `PlaneViewer`: it comes from the join flow, not from args.

**⚠ It deliberately does not re-resolve.** Re-running arbitration would let a `--viewer` vote win a
second time and the menu would stop launching flight; re-running placement would move a `--pos` that
was routed to the camera at parse time onto the menu's plane. The launchscreen overwrites an answer,
it does not ask the question again — which is what it has always done.

**Verify.** Ten engine-free facts in `CSVM.Tests/SessionSpecMenuTests.cs` — the launchscreen's only
automated coverage anywhere, since the goldens never open it and the resolution baseline drives the
CLI only. **Each of the eight writes was shown able to fail**: dropping it individually breaks 1–3
facts (Chapter 2, PlaneNames 2, PlaneName 3, Players 3, Stunt 1, Mode 1→3, WorldMode 1, Scenario 2),
and ignoring `--scenario=` breaks 1. Expected values in the carry-over fact differ from both the
first pick and the parse default, so a dropped write cannot pass by landing on one. `.\RunTests.ps1`
PASS (162 units, 9/9 suites, 11/11 goldens hash-identical, exit 0); `baseline.txt` md5-identical
(`944310579BA214A0E99B801FC344098B`), as it must be — that matrix never enters the menu, so it
confirms the CLI path is untouched and says nothing about this item. Four launchscreen renders
headless, exit 0 and zero errors: `--menu`, `--menu --debug-join=3`, `--menu --plane= --chapter=`,
and `--menu=plane --debug-join=1` (eyeballed: two slots, P1 choosing, P2 locked in).

**The menu → flight re-entry was playtested and confirmed working** (user, 2026-07-30) — the one
part of this item nothing automated reaches, since it needs a keypress.

**⚠ Traps.**
1. The equality above holds only while the factory writes every field a previous launch could have
   changed. Adding a menu-settable field without adding its write re-opens the carry-over bug the
   old patches existed to prevent — and the pristine base would then *hide* it, because the stale
   value would come from the command line instead of the last session.
2. A perturbation that drops a write can pass by landing on a default: the second-launch fact uses
   a chapter that is neither the first pick nor `C1`.

## B7 ☑ The xUnit resolution truth table

**84 engine-free facts (296 xUnit cases) across three files**, covering mode precedence and every
vote, step order, `ShowsMenu`/`ModeName`/`IsScripted`/`ScriptedBy`, the `--det` bundle's membership
and everything it pins, `BuildsCollision`, `WorldMode`, the empty stage, the player count, the
mode-gated tools, placement normalisation, `FromMenu`, and all seven formerly-private parsers.

**Evidence (confidence: traced).** None of these rules had automated coverage. Verification rules
115 and 116 both came from this file and both describe failure modes these facts now catch.

**Approach.** `SessionSpecTests` is the resolution table, `SessionSpecParserTests` the value
grammars, `SessionSpecMenuTests` (landed in B6) the launchscreen. The parser file exists separately
because those seven are the launch surface most likely to be wrong in a way nothing downstream
notices — a mis-parsed vector still produces a perfectly valid session. The two deliberate defects
are asserted **as they are** and labelled `⚠ KNOWN DEFECT` at the fact: `ModeName` omits
`--dump-flight` from its "dump" arm, and `ShowsMenu` is true under `--run-tests`. A third, milder
drift got its own fact once the table made it visible: `--dump-flight` turns `--det` on through
`ScriptedBy` yet is not a term of `IsScripted`, so its window still asks for focus.

**Verify. Every rule shown able to fail: 32 perturbations, one rule each, 32 caught.** Each edits a
single term or block in `SessionSpec`, runs the filtered suite, and is reverted with a forced
timestamp (rule 124). `.\RunTests.ps1` PASS — 296 units, 9/9 suites, 11/11 goldens hash-identical,
exit 0.

**⚠ The sweep found a hole in the tests themselves, which is the point of running it.** Disabling
the freecam-beats-fly/viewer block broke *nothing*: the case carried `--viewer` too, so the
viewer-beats-fly rule cleared the same modifiers and the mode ternary reached `Freecam` either way —
the fact was passing for a different reason than it claimed. It now also asserts `--freecam --stunt`
and `--freecam --damage`, where no other rule can clear the modifier. **A fact that passes is not
the same as a fact that tests the rule it names**, and only the perturbation tells them apart.

**⚠ Traps.**
1. This item is what pays for B8. It is not trimmed: 84 facts against the plan's 40–60, every one
   shown able to fail. If a future change trims it, reopen decision 6 rather than shipping B8 with
   thin tests.
2. An assertion whose expected value equals the parse default passes when the write it tests is
   deleted. Expected values here are chosen away from the defaults.
3. The sweep script wrote the source back with a BOM the committed file did not have, leaving a
   one-line diff after a "clean" restore. Diff the perturbed file against `HEAD`, not just by eye,
   before believing a revert (new verification rule 126).

## B8 ☑ Delete the scaffold; `architecture.md` entry; `HISTORY.md`

**`--dump-session` is gone**, with `Probes.Session`/`SessionResult`/`SessionValues`, `DumpSession`,
and `analysis/session-baseline/capture.ps1`. **The CLI index is back to 89 flags.**

**Evidence (confidence: traced).** Decision 6, and B7 paid for it: 83 facts, each shown able to
fail by perturbing its own rule. The table was not trimmed, so decision 6 stands unreopened.

**Approach.** The probe's three vacuous survivors went too: with the flag deleted, the two
`--dump-session` `InlineData` rows and `DumpSessionIsATermOfNothingItReports` would have passed
against an unrecognised argument — tests that cannot fail, which is the thing this plan spent eight
items avoiding.

**⚠ What is kept, and what it is now.** `FINDINGS.md`, `matrix.ps1` and `baseline.txt` stay, as A1
decided when it put them outside `.scratch/`. They are a **record, not a tripwire**: the baseline
can no longer be regenerated or fail, and `FINDINGS.md` now says so at the top, because an artifact
that cannot fail reads exactly like one that passed. `capture.ps1` went because a script that cannot
run is a trap for whoever tries it.

**⚠ State the loss plainly.** The truth table asserts rules one at a time; the baseline asserted 50
whole command lines end to end. **No per-rule fact catches a rule nobody thought to write a fact
for**, and that is exactly what the baseline could do. The trade was made with open eyes.

**Verify.** `.\RunTests.ps1` PASS — 293 units, 9/9 suites, 11/11 goldens hash-identical, exit 0.
`--dump-session --dump-config` now falls through to `--dump-config`, so the flag is genuinely
unparsed rather than silently honoured. The flag is gone from `cli.md`'s index and its bullet.

**⚠ The flag count was stale, not stable.** B8's goal said the count "is unchanged at 89" — but A1
added `--dump-session` without bumping it, so CLAUDE.md had read 89 against a real 90 ever since.
Deleting the flag makes 89 true again; the number was right today by accident. Counting also found
that `cli.md` documents **89 flags while the parser accepts 92**: `--debug-colliders`, `--jitter`
and `--sky-zone` appear in neither the index nor a bullet, and `--direction`/`--spawn-dir` are
indexed with no bullet of their own. Recorded in `backlog.md` — out of scope to author here.
