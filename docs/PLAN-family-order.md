# Family order, one ranked dependency order for `CSVM/src`

**ACTIVE PLAN** (written 2026-09-28). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The thirteen top-level namespaces in `CSVM/src` form one strongly connected component: with the
root namespace ranked as a unit, 91 type-level references point up the natural order (reference
weight 218). The cause is a handful of misfiled types and `Session.Launch` filed under `Session`,
not broken documented rules. This plan ranks every namespace into one family order, enforces it with
one test over `AssemblyDependencyScan`, and lands the type moves that make the order true for all but
a short allowlist. The work runs on the `family-order` branch in `.claude/worktrees/family-order`,
and **merges to main only on the user's explicit go.**

Out of scope: the director inversions (Session.Campaign, InstantAction and Roster naming UI screens,
about 12 pairs), which need seams rather than moves, and the small leftovers (PlaneRoster to
SessionSpec, Spec to UI, Tooling to `UI.Campaign.FlightRow`). Both stay on the allowlist and are
owned by two GitHub `backlog` issues filed at merge time (G19). AnimRuntime's split is settled by
ADR-0001 and is not touched. No item is drawn from `backlog.md` or an existing issue.

## Milestone goal

- Every `CSVM.*` namespace in CSVM.dll has a rank, and a family names only families below it,
  except the pairs on one sorted, shrink-only allowlist.
- The root namespace is empty and stays empty.
- `Session.Launch` is its own top family, `CSVM.Launch`, the composition root.
- `UI.Boards` is a leaf widget library below Flight.

**This plan moves types; it does not redesign them.** An edge that needs a seam or an inversion goes
on the allowlist with an owning issue, not into this plan.

## Decisions (2026-09-28)

| # | Question | Decision |
|---|---|---|
| 1 | What does the test enforce? | **A family order plus the within-family rules the docs state**, as extra rows. |
| 2 | Landing order | **The test first, with today's allowlist; then each move as its own commit**, each removing the entries it makes stale. |
| 3 | Name of the promoted family | **`CSVM.Launch`**, folder `src/Launch/`. |
| 4 | Where SessionSpec goes | **`CSVM.Spec`** (`src/Spec/`): SessionSpec.cs, MenuMode and the small enums and records declared beside SessionSpec. `Flight.Airframe.PlaneRoster → SessionSpec` stays allowlisted. |
| 5 | Allowlist form | **Exact type pairs**, compiler-generated nested types folded into their owner; fails on a new pair and on a stale entry; a plain sorted array in the test file, no per-line reasons. |
| 6 | Plan and doc | **This file**, plus one pointer paragraph in `docs/architecture.md` replacing the "names only" sentences that become rows. |
| 7 | Scope | **The test and pure moves only.** Director inversions stay allowlisted under one follow-up issue; Tooling→Testing dispatch is a documented exception row. |
| 8 | `UI.Boards` name | **Kept.** The table ranks it below Flight by longest prefix, with a one-line note. |
| 9 | SessionPaths, Pads | **SessionPaths → `CSVM.Extraction`; Pads → `CSVM.Bindings`.** |
| 10 | ViewerSet, WeatherState, Difficulty | **ViewerSet and WeatherState → `CSVM.Effects`. Difficulty stays in `Flight.Hangar`**: its readers below Flight were name collisions in the syntax extractor, and its `AimAssist` reads are inlined consts. |
| 11 | The family table | **Utils · Extraction · Mech3 · Video · Bindings · Sticks · Effects · Net · UI.Boards · Flight · Spec · Session · Tooling · UI · Launch · Testing**, lowest first. |
| 12 | Leaf types still pointing up | **Five more moves**: TypedText and MovieSurface → `UI.Boards`, StickSplit → `CSVM.Sticks`, OrbitCamera → `Flight.Camera`, `BoardPalette.For(CampaignScreen)` → `UI.Campaign`. |
| 13 | Within-family rows | **Four rows**: Flight.Camera names only Flight.Airframe in Flight; nothing else in Flight names Flight.Hangar; nothing else in UI names UI.Labs; UI.Hangar names only UI.Boards and the shared UI.Menu (the documented rule corrected, since HangarFeature and CampaignWallet are shared menu features). The Menu test's Launcher fact becomes redundant and is deleted; its Godot fact stays. |
| 14 | Test shape | **`CSVM.Tests/FamilyOrderTests.cs`** holds the only copy of the rank table; facts `NoFamilyNamesAFamilyAboveIt`, `EveryNamespaceHasARank`, `WithinFamilyRulesHold`; only `CSVM.*` → `CSVM.*` references are in scope. |
| 15 | Doc pages | **New `docs/architecture/Launch.md`; `Root.md` becomes `Spec.md`**; Pads's entry to `Bindings.md`, SessionPaths's to `Extraction.md`; each move carries its entries in the same commit. |
| 16 | Execution | **One worktree, commits made in order by the orchestrating session**, full `RunTests.ps1` per commit; merge main in, then merge to main **only on the user's go**. |
| 17 | Follow-up issues | **Two GitHub `backlog` issues** at merge: director inversions, and the small leftovers; each lists its exact pairs from the final allowlist. |

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
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, the test

1. ☑ `FamilyOrderTests` with the rank table, the four within-family rows and today's allowlist

### Wave B, empty the root namespace

11. ☑ `CSVM.Spec`: SessionSpec, MenuMode and their records
12. ☑ SessionPaths → Extraction, Pads → Bindings

### Wave C, promote Launch

13. ☐ `Session.Launch` → `CSVM.Launch`
14. ☐ ExtractionStamp and StampStanding → Extraction

### Wave D, UI.Boards as the leaf widget library

15. ☐ Pause* → UI.Screens, MenuInput → UI.Boards
16. ☐ TypedText and MovieSurface → UI.Boards, `BoardPalette.For` → UI.Campaign

### Wave E, the Flight residue

17. ☐ ViewerSet and WeatherState → Effects, StickSplit → Sticks, OrbitCamera → Flight.Camera

### Wave F, docs and cleanup

18. ☐ Hangar rule corrected in the docs, the architecture.md pointer paragraph, the Menu test's Launcher fact deleted, CONTEXT.md **Family**

### Wave G, merge

19. ☐ Merge main in, the user's go, merge to main, file the two follow-up issues

## Dependency and parallelism notes

Items run in listed order; no parallelism. Every item edits `FamilyOrderTests.cs`'s allowlist and
most edit `docs/architecture.md`'s index, so parallel worktrees would conflict on both. A1 blocks
everything. G19 waits for the user.

---

# Wave A, the test

## A1 ☑ `FamilyOrderTests` with the rank table, the four within-family rows and today's allowlist

**Landed.** `CSVM.Tests/FamilyOrderTests.cs` with the Decision 11 table, the four rows and a
60-pair allowlist taken from the test's own first run. The root namespace ranks as Spec and
`CSVM.Session.Launch` as Launch until B12 and C13 empty them; a row without a dot matches only
itself, and a row that matches no namespace fails, so those interim rows must go with their moves.
`AssemblyDependencyScan.Namespaces` lists the namespaces the rank check covers.

**Verified.** The three facts pass; a fake allowlist entry fails as stale, and dropping the shared
UI.Menu exemption fails the Hangar row on nine pairs. Full `.\RunTests.ps1`.

**Original approach (kept for reference).**

**Goal.** A test that fails on any new upward reference between families, on a stale allowlist entry,
on an unranked `CSVM.*` namespace, and on a broken within-family row, and passes on today's tree.

**Evidence (confidence: lead-only).** The edge counts come from a syntax-only extraction
(`.scratchKeep/class-map/umlx`), which over-reports name collisions (Difficulty's readers in Utils and
Mech3 were property names and comment text). `AssemblyDependencyScan` reads IL and metadata, so
the test's first run sets the real allowlist. `AssemblyDependencyScan` files nested types under their
outermost namespace, writes them `Outer/Inner`, and throws on a filter that matches no type; it
cannot see `const` values. `NetNamespaceDependencyTests` has a `Subject(violation)` helper that
folds compiler-generated nested types into their owner.

**Approach.** New `CSVM.Tests/FamilyOrderTests.cs`:
- A sorted `(prefix, rank)` array, longest prefix wins: the table in Decision 11, with `UI.Boards`
  ranked below Flight and `UI` for the rest of UI. Today's tree needs a rank for the root `CSVM`
  namespace and `CSVM.Session.Launch` until Waves B and C remove them; <TODO: settle whether the
  interim ranks are table rows or allowlist entries>.
- `NoFamilyNamesAFamilyAboveIt`: scan every `CSVM.*` type, fold nested closures into their owner
  (reuse or share Net's `Subject` helper), compare the pairs against the allowlist, and fail on a new
  pair or a stale entry.
- `EveryNamespaceHasARank`: every namespace in CSVM.dll matches a row.
- `WithinFamilyRulesHold`: the four rows in Decision 13, with the UI.Hangar row as corrected.
- Tooling→Testing (`ProbeRunner` → `TestHarness`, `TestContext`) is an exception row, not an
  allowlist entry.
- <TODO: whether `AssemblyDependencyScan` needs a new entry point that returns every referenced
  type rather than one banned-name filter>.

**Model recommendation.** high: the test's shape decides every later item's allowlist diff.

**Verify.** The test passes on today's tree. Adding a throwaway upward reference fails it, and
deleting one allowlist entry fails it (see each able to fail before trusting the pass). Full
`.\RunTests.ps1`.

**⚠ Traps.** `const` references are inlined, so `SplitScreen.MaxPlayers` and AimAssist's team ids
never appear; do not add allowlist entries the scan cannot produce. The syntax-only class map is
not the authority for the list.

# Wave B, empty the root namespace

## B11 ☑ `CSVM.Spec`: SessionSpec, MenuMode and their records

**Landed.** `CSVM/src/Spec/SessionSpec.cs` under `CSVM.Spec`, carrying every enum and record
declared beside it; `using CSVM.Spec;` added where the name no longer resolved from a parent
namespace. `Root.md` is `Spec.md` (its enhanced-graphics section stays with `EnhancedPasses`), the
index has `### src/Spec/`, and the three links to `Root.md` point at `Spec.md`. The allowlist
entries naming SessionSpec follow its new full name; the table gains a `CSVM.Spec` row.

**Verified.** `FamilyOrderTests` 3/3; `CheckDocEntries.ps1` clean; full `.\RunTests.ps1`, see the
commit.

**Original approach (kept for reference).**

**Goal.** SessionSpec.cs and MenuMode live in `src/Spec/` under `CSVM.Spec`; no type other than
Pads and SessionPaths is left in the root namespace.

**Evidence (confidence: lead-only).** The root holds SessionSpec (67 users; 279 members, 266
public), MenuMode (17), SessionPaths (142), Pads (19), plus EnhancedPasses, SessionMode,
SessionProbe, AiPlaneEntry, ZepStageSpec and `SessionSpec.Note`, per the class-map extraction.

**Approach.** `git mv` to `src/Spec/`, change the namespace, add `using CSVM.Spec;` where needed.
`Root.md` becomes `Spec.md` with its entries; the index section "Session root and tests" becomes
`### src/Spec/`. Remove the allowlist entries this makes stale.

**Model recommendation.** medium, low effort: mechanical.

**Verify.** `FamilyOrderTests` passes with a shorter allowlist; `CheckDocEntries.ps1`; full
`.\RunTests.ps1`.

**⚠ Traps.** Path references outside `docs/architecture*` were `PROJECT_CONTEXT.md`'s namespace
map and one comment in `GroundShadowPass.cs`; `Root.md` was linked from `Utils.md` and `Mech3.md`.

## B12 ☑ SessionPaths → Extraction, Pads → Bindings

**Landed.** `src/Extraction/SessionPaths.cs` and `src/Bindings/Pads.cs`, their entries on
`Extraction.md` and `Bindings.md`, the root namespace empty. The root family row is gone, so a type
left in the root namespace now fails `EveryNamespaceHasARank`; with it went the rule that a row
without a dot matches only itself. Six allowlist pairs retired.

**Verified.** `FamilyOrderTests` 3/3 after it had failed on exactly the six stale pairs and the
unused root row; `CheckDocEntries.ps1` clean; full `.\RunTests.ps1`, see the commit.

**Original approach (kept for reference).**

**Goal.** The root namespace is empty.

**Evidence (confidence: lead-only).** Extraction names no Mech3 type, and Mech3 reads
`Extraction.ZbdTree`, so Extraction sits below Mech3 and SessionPaths can live there.

**Approach.** `git mv` each file, change the namespace; the Pads entry moves to `Bindings.md`, the
SessionPaths entry to `Extraction.md`. Remove the stale allowlist entries.

**Model recommendation.** medium, low effort: mechanical.

**Verify.** As B11.

**⚠ Traps.** <TODO: none known>.

# Wave C, promote Launch

## C13 ☐ `Session.Launch` → `CSVM.Launch`

**Goal.** The composition root is its own top family, above UI.

**Evidence (confidence: lead-only).** Of the 274 Session→UI reference weight, 250 comes from
`Session.Launch`. UI→`Session.Launch` is only ExtractionStamp/StampStanding (7) and
Extraction→`Session.Launch` is 3; C14 moves those. About 55 files mention `Session.Launch` or its
path, `CSVM/scenes/Main.tscn:3` among them.

**Approach.** `git mv CSVM/src/Session/Launch CSVM/src/Launch`, namespace `CSVM.Launch`, the
`Main.tscn` script path, every `using`. The entries move from `Session.md` to a new `Launch.md`,
the index gets `### src/Launch/`. `MenuNamespaceDependencyTests`'s Launcher fact names the old
full names; update it here, and delete it in F18.

**Model recommendation.** medium: mechanical but wide.

**Verify.** As B11, plus a cold start through `Main.tscn` inside `RunTests.ps1`.

**⚠ Traps.** `Session/Launch` is a hot path (recent multiplayer and version-stamp work), so check
`git log main -- CSVM/src/Session/Launch` right before this commit and merge main in first if it
moved.

## C14 ☐ ExtractionStamp and StampStanding → Extraction

**Goal.** Nothing below Launch names a Launch type.

**Evidence (confidence: lead-only).** From the class-map extraction, as C13.

**Approach.** Move the file into `src/Extraction/`, change the namespace, move its entry to
`Extraction.md`.

**Model recommendation.** medium, low effort: mechanical.

**Verify.** As B11.

**⚠ Traps.** <TODO: none known>.

# Wave D, UI.Boards as the leaf widget library

## D15 ☐ Pause* → UI.Screens, MenuInput → UI.Boards

**Goal.** UI.Boards names nothing in UI.Menu, UI.Overlays or UI.Screens.

**Evidence (confidence: lead-only).** PauseSheet, PauseScreens, PauseReadout, PauseObjective and
PauseWorldIcon carry UI.Boards's reads of UI.Menu, UI.Overlays and `UI.Screens.LoadScreens`;
BoardMenuHost and BoardMenuView read `UI.Screens.MenuInput`.

**Approach.** `git mv` the files, change the namespaces, move the entries between the `UI.md`
sub-namespace groups and the index.

**Model recommendation.** medium, low effort: mechanical.

**Verify.** As B11.

**⚠ Traps.** The `orch-13` branch edits BoardMenuView, ComposedBoard and ComposedBoardView; merge
main in before this item if orch-13 has landed.

## D16 ☐ TypedText and MovieSurface → UI.Boards, `BoardPalette.For` → UI.Campaign

**Goal.** UI.Boards names nothing in UI.Screens or UI.Campaign.

**Evidence (confidence: lead-only).** TypedText names nothing, read by Launcher and MenuInput.
MovieSurface names only `Video.MoviePlayback`, `Video.MpegMovie` and `Utils.Log`, read by
ComposedBoardView. `BoardPalette.For(CampaignScreen)` (`CSVM/src/UI/Boards/BoardPalette.cs:102`)
switches on the `CampaignScreen` enum, which about 25 campaign pages use.

**Approach.** Move the two types. Move `For` into UI.Campaign beside `CampaignScreen`, <TODO: which
type in UI.Campaign owns it>; its callers change from `BoardPalette.For(s)` to the new home.

**Model recommendation.** medium: one method move with callers.

**Verify.** As B11.

**⚠ Traps.** <TODO: none known>.

# Wave E, the Flight residue

## E17 ☐ ViewerSet and WeatherState → Effects, StickSplit → Sticks, OrbitCamera → Flight.Camera

**Goal.** Effects names no Flight type, UI.Boards names no Flight type, and Flight.Hud and Tooling
name no UI.Overlays type.

**Evidence (confidence: lead-only).** ViewerSet names only its nested ViewerPose. WeatherState names
only Mech3 (FogVolumeBox, HorizonZone, Zrdr, ZrdrDict) and `Utils.Log`, and Effects already reads
Mech3 with no reverse edge. StickSplit names only Bindings and `Sticks.StickModel`. OrbitCamera
names nothing; its readers are TargetHud, Launch, Tooling and AnimLab.

**Approach.** Move the four files and their nested types; entries to `Effects.md`, `Sticks.md`,
`Flight.md`.

**Model recommendation.** medium, low effort: mechanical.

**Verify.** As B11. Flight.Camera's within-family row (names only Airframe) must still hold with
OrbitCamera in it.

**⚠ Traps.** <TODO: none known>.

# Wave F, docs and cleanup

## F18 ☐ Hangar rule corrected, the pointer paragraph, the Launcher fact deleted, CONTEXT.md Family

**Goal.** The docs state the order the test enforces, and no second test duplicates it.

**Evidence (confidence: lead-only).** `docs/architecture/UI.md`'s intro and `docs/architecture.md:394`
say UI.Hangar names nothing else in UI; HangarFlow, HangarNamePage, HangarPlaneSelectionPage and
HangarArmourPage name `UI.Menu.HangarFeature` and `UI.Menu.CampaignWallet`. The Flight and UI
intros carry the sub-namespace rules the test's rows now enforce.

**Approach.** One pointer paragraph in `docs/architecture.md` stating the order in one line and
naming `FamilyOrderTests.cs` as the authority; the "names only" sentences in the Flight and UI
intros point at it; the Hangar sentence says it names only UI.Boards and the shared UI.Menu.
`docs/menu-presentations.md`'s list of scans drops the Launcher scan. Delete
`PresentationsNeverConstructASessionOrReachTheLauncher`. Add a **Family** term to `CONTEXT.md`
(a ranked top-level namespace), distinct from "Format family".

**Model recommendation.** medium.

**Verify.** `CheckDocEntries.ps1`, `CheckCommitContent.ps1`, full `.\RunTests.ps1`.

**⚠ Traps.** Docs state what is, not what was: no narration of the moves in live prose.

# Wave G, merge

## G19 ☐ Merge main in, the user's go, merge to main, file the two follow-up issues

**Goal.** The branch is on main and every remaining allowlist pair has an owning issue.

**Evidence (confidence: lead-only).** Decisions 16 and 17.

**Approach.** Merge main into `family-order`, rerun the full battery, and **wait for the user's go**.
Then merge to main, file two GitHub `backlog` issues (director inversions; small leftovers) listing
their pairs from the final allowlist, and delete this file in the closing commit.

**Model recommendation.** medium.

**Verify.** Full `.\RunTests.ps1` on the merged tree.

**⚠ Traps.** Do not merge without the user's explicit go. Sweep ids after the merge
(`CheckItemIds.ps1`).
