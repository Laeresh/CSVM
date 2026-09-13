# Original screen modules

**ACTIVE PLAN** (written 2026-09-13). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan finishes the split of the Original menu presentation's `partial class OriginalShell`
(`CSVM/src/UI/Menu/Original/`) into per-screen-family modules. The hangar family is already out:
`OriginalHangarScreen` is a sealed module constructible without the shell, talks to it through
`IOriginalHangarHost` (implemented explicitly by `OriginalShell`), and `OriginalHangarTests` build
the module over a hand-written host. That extraction is the worked example every item here copies.
The remaining families go in the order the grilling agreed: Instant Action with its Loadout leaf,
then the options leaves as one module, then Campaign. The dialog the campaign family owns moves into
the shell proper before Campaign leaves, so the Campaign module raises dialogs through the host seam
the way the hangar module already does. The shell keeps what is cross-screen by nature: the generic
input machinery (hit test, column step, drag, wheel, `_focus[]` per-screen cursors), `_planes`
(`CustomPlaneStore`, read by more than one family), the top-level and free-flight leaves, the seat
walk (`OriginalSeatPlane`, `OriginalSeats`), `DropList`, and Credits.

No item here comes from `backlog.md`; nothing needs re-verification against the record. The plan
assumes the hangar extraction commit `7fcf5a06` (branch `worktree-agent-a51704ad239506c73`) is the
base; it is not yet on `main`.

## Milestone goal

- `OriginalInstantAction.cs` and `OriginalLoadout.cs` are one sealed module with its own state and
  its own test file that never constructs `OriginalShell`.
- The host seam has two adapters (the shell and the tests' fake) and a general name; the shell
  dispatches to modules through one screen-to-module lookup instead of per-family range checks and
  a hardcoded field per module.
- `OriginalGameOptions.cs`, `OriginalAudio.cs`, `OriginalVideo.cs` and `OriginalControls.cs` are one
  options module carrying the saved-settings fields that today sit on the shell.
- The shell owns the standing dialog and `RaiseDialog`; no family carries its own dialog state.
- `OriginalCampaign.cs` is a module over the same seam, with `OriginalCampaignTests` retargeted.
- `OriginalShell.cs` shrinks with each item; the public surface it adds per module is one accessor.

**A partial file is not a module.** Each item ends with a sealed class that a test constructs
without the shell, or it has not landed. Forwarding properties on the shell that exist only to
reach a module's internals are the shape the hangar work had to undo, and are rejected here too.

## Decisions (2026-09-13)

| # | Question | Decision |
|---|---|---|
| 1 | Which family first, and in what order after | **Hangar (landed), then Instant Action + Loadout, then options leaves, then Campaign**, each is a smaller copy of the hangar pattern and Campaign is the largest and the only one carrying a dialog. |
| 2 | What stays in the shell | **Generic input machinery (hit test, column step, drag, wheel, focus), `_planes`, the seat walk, `DropList`, Credits, top-level and free-flight leaves**, the seat walk is cross-screen state and the input machinery is what every module leans on. |
| 3 | Module construction | **Constructible without `OriginalShell`**, over a host interface the shell implements explicitly; the tests supply their own host. |
| 4 | Dispatch | **A single field per module now; a screen-to-module lookup once there are two modules** (item A2 introduces it), a registry was rejected as YAGNI while there was one module. |
| 5 | Dialogs | **Raised through the host seam (`RaiseDialog`), owned by the shell**, the campaign family's `_dialog` moves to the shell first so Campaign leaves as a consumer of the seam. |
| 6 | Tests | **Retargeted onto each module directly, with a thin wiring set in `OriginalShellTests`** proving the shell reaches the module (door opens, focus walk lands on the module's rows). |
| 7 | Landing | **One commit per item**, each with the `docs/architecture/UI.md` entries updated in the same commit. |
| 8 | Host interface generality | **Rename `IOriginalHangarHost` to `IOriginalScreenHost` once the Instant Action module fits it**, one adapter is a hypothetical seam, two is a real one; the rename waits for the second. |

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

### Wave A, second module and the dialog

1. ☑ Instant Action + Loadout module (`OriginalInstantActionScreen`) over the hangar host seam
2. ☑ The standing dialog and `RaiseDialog` move from the campaign partial into the shell proper

### Wave B, general seam and the options leaves

11. ☐ Rename the host seam to `IOriginalScreenHost`; one screen-to-module lookup replaces the per-family field and range checks
12. ☐ Options module (`OriginalOptionsScreen`) over the game options, audio, video and controls leaves, carrying the saved-settings fields

### Wave C, campaign

21. ☐ Campaign module (`OriginalCampaignScreen`) over the seam; `OriginalCampaignTests` retargeted

## Dependency and parallelism notes

A1 and A2 run in parallel: A1 owns `OriginalInstantAction.cs`, `OriginalLoadout.cs`, the new module
and test files, and the Instant Action dispatch sites in `OriginalShell.cs` (`Rows`/`BuildRows`,
`Lists`, `ApplyFrame` MoveX cascade, `Compose`, `Activate`/`Back`); A2 owns `OriginalCampaign.cs`
lines 80 to 91 and the dialog block, `DialogRows`, `RaiseDialog`, and the `IOriginalHangarHost`
explicit members for dialogs at the end of `OriginalShell.cs`. Both edit `OriginalShell.cs`, in
different regions; the orchestrator merges A1 first and re-applies A2 on top, resolving by region.
B11 needs A1 (the second adapter is what justifies the rename) and A2 (the lookup dispatches dialog
rows too). B12 needs B11 (it registers in the lookup rather than adding a fourth range check).
C21 needs A2, B11 and B12; it runs alone. Every item edits `OriginalShell.cs` and
`docs/architecture/UI.md`, so no two items beyond A1/A2 run concurrently.

---

# Wave A, second module and the dialog

## A1 ☑ Instant Action + Loadout module over the hangar host seam

**Landed.** `CSVM/src/UI/Menu/Original/OriginalInstantActionScreen.cs` is the sealed module over
both screens (`Owns`, `OpenInstantAction`, `RefreshRoster`, `OpenDropdownOn`, `OpenLoadout`,
`OpenSeatLoadout`, `DropLoadout`, `ClearLoadoutSeat`, `BuildRows`, `Lists`, `StepSideways`,
`CloseDropdown`, `Activate`, `Back`, `Compose`, its own `OriginalInstantActionInks`), constructed
over `InstantActionFeature`, `PlayerSetupFeature`, the store, the layout, the measurer and the
host. `OriginalInstantAction.cs` and `OriginalLoadout.cs` are deleted. The shell holds one
`_instantActionModule` field and one `InstantAction` accessor; `IsInstantActionFamily` is
`Owns`. `IOriginalHangarHost` grew `HoveredRow`, `Pointer`, `MenuStrings`, `CanBuildPlane`,
`OpenHangar`, `BeginSeatWalk` and `SeatPanel`. `OriginalInstantActionTests` is rewritten over a
hand-written `InstantActionHost` (20 facts, no shell); `OriginalShellTests` keeps the door and
column-walk wiring facts. Deviations B11 picks up: `Indexed`, `PaneOrigin` and `AddPane` were
deleted from the shell and both modules hold private copies (hoist into one shared static
helper); `OriginalDropList.cs` now exposes the window rule as `OriginalDropLists` for non-partial
modules; the shared plate-row composer is `ComposePlateRow` on the shell.

**Verified.** Full `.\RunTests.ps1` on the plan tree with A1 squash-merged over A2: PASS, exit 0
(4414 units passed, 2 skipped for missing media; 332 engine suites, errors clean; 19 goldens
hash-identical, so no Original board composes differently). `CheckDocEntries.ps1`,
`CheckCommentCaps.ps1` and `CheckEncoding.ps1` clean on the merged tree. `OriginalInstantActionTests`
names no `OriginalShell`; the module file is 1695 lines against the two partials' 1590, the
difference the module's own inks, key constants and helper copies.

**Original approach (kept for reference).** `OriginalInstantActionScreen` is a sealed class
owning the Instant Action and Loadout screens' state and rows, constructed without
`OriginalShell`; `OriginalInstantActionTests` exercise pilot roster, spare fit, page, dropdown and
loadout node walks over a hand-written host.

**Evidence (confidence: traced).** `OriginalInstantAction.cs:104-113` holds `_instantAction`,
`_iaPilotRoster`, `_iaPilotBuild`, `_iaSpareFit`, `_iaPage`, `_iaOpen`, `_iaListTop`,
`_iaContentsTop`, `_iaRadio`, `_iaStoryTitle`; `OriginalLoadout.cs:52-62` holds `_loadoutBefore`,
`_loadoutFit`, `_loadoutDef`, `_loadoutOptions`, `_loadoutNode`, `_loadoutName`, `_loadoutSeat`.
The two partials are 968 and 415 lines. The shell's `ApplyFrame` MoveX cascade calls
`StepInstantActionValue` and `Compose()` switches on `OriginalScreen.InstantAction` and
`InstantActionLoadout` (`OriginalShell.cs`, enum at lines 57 and 61). The dropdown and list-row
idiom (`_iaOpen`/`_iaListTop`) is the same shape the hangar module moved (`_hangarOpen`/
`_hangarListTop`, `OriginalHangarScreen.cs:269-270`). `OriginalLoadout.cs` shares `PaneOrigin`,
`AddPane` and `Indexed` with the hangar module through the shell.

**Approach.** Copy the hangar shape: a sealed `OriginalInstantActionScreen(InstantActionFeature,
CustomPlaneStore?, MenuLayout, Func<string, ...> measure, IOriginalHangarHost host)` in a new file
that absorbs both partials (delete them), `Owns(OriginalScreen)` covering `InstantAction` and
`InstantActionLoadout`, and the moved `BuildRows`/`Lists`/`Step...Sideways`/`Compose`/`Activate`/
`Back` methods. On the shell: one `_instantActionModule` field, one `InstantAction` accessor, and
the dispatch sites call it the way they call `_hangarModule`. If the module needs a host member the
hangar seam lacks, add it to `IOriginalHangarHost` and to the tests' `HangarHost` fake; do not add
forwarding members to the shell. Retarget the Instant Action facts in `OriginalShellTests`,
`OriginalCoverageTests` and `CSVM/src/Testing/MenuInstantActionSuites.cs` onto the module or the
accessor. `_planes` stays on the shell and is passed in. Update the `UI.md` entries for the shell
and the new module.

**Model recommendation.** high. The move is mechanical but the dispatch sites are wide switches
and the loadout seat handoff crosses into the seat walk that stays in the shell; judgement is
needed on every line that touches `_loadoutSeat`.

**Verify.** `.\RunTests.ps1` full battery passes on the merged tree; `OriginalInstantActionTests`
construct the module with no `OriginalShell` type in the file; `OriginalShellTests` keeps a wiring
fact that the Instant Action door opens the module's first screen and a keyboard column walk lands
on its rows. Public member count of `OriginalShell` does not grow beyond the one accessor.

**⚠ Traps.** Dialog answer keys are the shared `DIALOG:*` constants in `OriginalCampaign.cs`
(`DialogOkKey`/`DialogYesKey`/`DialogNoKey`/`DialogCancelKey`); the first hangar cut invented its
own and broke the dialog walk. `PaneOrigin`/`AddPane`/`Indexed` are shared with the hangar module;
leave them on the shell, do not copy them. `_focus[]` is indexed per screen and lives on the shell;
the module reads and writes the cursor through the host's `FocusedRow`. `_loadoutSeat` is set by the
seat walk; do not move the seat walk.

## A2 ☑ The standing dialog and `RaiseDialog` move into the shell proper

**Landed.** `CSVM/src/UI/Menu/Original/OriginalShellDialog.cs` is the shell's own dialog partial:
the `OriginalDialog`/`OriginalDialogAnswer` records, the `DIALOG:*` keys, `_dialog`,
`_focusBeforeDialog`, the answer builders, `DialogRows`, `ComposeDialog`, both `RaiseDialog`
overloads and `AnswerDialog`, moved verbatim out of `OriginalCampaign.cs` (158 lines out; it now
only raises). `OriginalShell.cs` is untouched; every consumer already spelled the keys
`OriginalShell.DialogOkKey`, so no reference changed. `PlaqueSizeOf`, which `DialogRows` calls,
still lives in the campaign partial and comes to the shell with C21.

**Verified.** Full `.\RunTests.ps1` on the plan tree with the relocation applied: PASS, exit 0
(4410 units passed, 2 skipped for missing media; 332 engine suites, errors clean; 19 goldens
hash-identical). `CheckDocEntries.ps1`, `CheckCommentCaps.ps1` and `CheckEncoding.ps1` clean.
`git diff --stat` on the item: `OriginalCampaign.cs` 162 lines down, one comment line in
`OriginalHangarScreen.cs`, no change to `OriginalShell.cs`, no test file touched.

**Original approach (kept for reference).** `_dialog`, `_focusBeforeDialog`, `DialogRows` and
`RaiseDialog` become shell members in `OriginalShell.cs` (or a small `OriginalShellDialog.cs`
partial of the shell, not of a family), and `OriginalCampaign.cs` raises dialogs only by calling
`RaiseDialog`.

**Evidence (confidence: traced).** `OriginalCampaign.cs:88-89` declares `_dialog` and
`_focusBeforeDialog` on the campaign partial while `OriginalShell.cs` already reads them in
`Rows => _dialog != null ? DialogRows() : BuildRows()` and implements `IOriginalHangarHost.
RaiseDialog` and `DialogOpen` for the hangar module. The dialog is shell-wide state stored in a
family's file.

**Approach.** Move the two fields, `DialogRows`, `RaiseDialog(message, DialogIcon, params
OriginalDialogAnswer[])` and the `DIALOG:*` key constants out of `OriginalCampaign.cs` into the
shell's own file; leave the campaign call sites untouched apart from the relocation. No behaviour
change; no test change beyond namespace or nesting fixes.

**Model recommendation.** medium. A relocation with no behaviour change; the only judgement is
keeping the `DIALOG:*` constants reachable from the hangar module and the tests.

**Verify.** Full battery passes; `git diff --stat` shows `OriginalCampaign.cs` shrinking by the
dialog block and no new public members on the shell. `OriginalHangarTests` and
`OriginalCampaignTests` unchanged in intent.

**⚠ Traps.** The hangar module and its tests reference the `DIALOG:*` constants by their current
declaring type; keep that name or update every reference in the same commit.

# Wave B, general seam and the options leaves

## B11 ☐ Rename the host seam and dispatch through one lookup

**Goal.** `IOriginalScreenHost` is the one host interface; the shell holds the modules in one
screen-to-module lookup, and `IsHangarScreen`, `IsInstantActionFamily` and the `Compose`/`Lists`/
`ApplyFrame`/`BuildRows` switch arms for module-owned screens collapse into a single
`ModuleFor(_screen)` call per site.

**Evidence (confidence: traced).** `OriginalShell.cs:349` holds `_hangarModule`; `IsHangarScreen`
is `_hangarModule?.Owns(_screen) ?? false`. After A1 there is a second field and a second range
check. Each dispatch site (`Rows`, `Lists`, `ApplyFrame` MoveX cascade, `Compose`, `Activate`,
`Back`, `CloseDropdown`) repeats the same null-conditional pattern per module.

**Approach.** Introduce a small module interface (`IOriginalScreenModule`: `Owns`, `BuildRows`,
`Lists`, `StepSideways`, `CloseDropdown`, `Compose`, `Activate`, `Back`) that both sealed modules
implement, an array or list of modules on the shell, and `ModuleFor(OriginalScreen)` returning the
owner or null. Rename the host interface and the tests' fakes in the same commit. Keep the per-module
accessors (`Hangar`, `InstantAction`) since `OriginalPresentation.cs` and the suites read
module-specific members through them. Also from A1: hoist the `Indexed`/`PaneOrigin`/`AddPane`
copies both modules carry into one shared static helper (a small `OriginalPanes` class or a
static on `OriginalDropLists`), and review whether `HoveredRow` and `Pointer` belong on the seam or
whether the module should take the pointer state as a parameter of the calls that need it.

**Model recommendation.** high. A rename across three test files, two modules and the suites, with
a dispatch rewrite in the shell's widest switches; the blast radius is the whole Original
presentation.

**Verify.** Full battery passes; `Grep IsHangarScreen|IsInstantActionFamily` finds no remaining
range check; each dispatch site in `OriginalShell.cs` names a module only through `ModuleFor`.

**⚠ Traps.** `IsHangarScreen` is read by `OriginalPresentation.cs` for the hangar palette; keep a
shell-level query (`ModuleFor(_screen) == Hangar`) rather than a per-family bool. The `_focus[]`
cursor reset on screen change must keep firing for module-owned screens after the switch arms go.

## B12 ☐ Options module over the four options leaves

**Goal.** `OriginalOptionsScreen` is a sealed module owning `GameOptions`, `Audio`, `Video`,
`ControlsPrefs` and `Keys`, carrying the saved-settings fields and the `*Choice` properties, with
`OriginalOptionsTests` over a hand-written host.

**Evidence (confidence: traced).** The four partials are `OriginalGameOptions.cs` (476 lines,
`_goOpen`/`_goListTop` at 94-95), `OriginalAudio.cs` (291, `_audioMoved` at 89), `OriginalVideo.cs`
(427, `_vpOpen`/`_vpListTop` at 87-88) and `OriginalControls.cs` (754, `_keysTab`/`_keysTop` at
136-137). The saved settings they read and write sit on the shell: `_choice`, `_graphics`,
`_difficulty`, `_nearestAfterKill` (`OriginalShell.cs:369-375`), `_monitorIndex`, `_resolution`,
`_displayMode`, `_vsync` (379-382), `_audioMaster`..`_audioVoice` (385-388), fed by `_options`,
`_screenSizes`, `_screens`, `_controls` (344-347) and `ReadSavedOptions`. The `ApplyFrame` MoveX
cascade calls `StepGameOptionValue`, `StepVideoValue` and `StepControlsValue`, and the shared
`SliderControl _slider` (350) serves the audio sliders.

**Approach.** One module over all five screens (decision 1: the leaves share the saved-settings
block, splitting them would spread it). Constructor takes the option and screen-list readers, the
controls feature and the layout; the module owns the settings fields, `ReadSavedOptions` and the
`*Choice` properties, and registers in the B11 lookup. The `Options` hub screen itself stays on the
shell (it is a plain button column). Decide whether `_slider` moves with the audio leaf or stays a
shell service; `<TODO: settle where SliderControl lives, the session did not discuss it>`.

**Model recommendation.** high. Four partials and a settings block that `OriginalPresentation.cs`
reads on apply; a missed reader silently loses a saved option.

**Verify.** Full battery passes; `OriginalOptionsTests` construct the module without the shell;
every `*Choice` reader in `OriginalPresentation.cs` and the suites resolves through the `Options`
accessor; the options goldens in `analysis/goldens/manifest.json` are unchanged.

**⚠ Traps.** `_choice` (the presentation choice) is read by the presentation switch on apply, not
only by the game options leaf; keep the accessor path. Keys tab state (`_keysTab`/`_keysTop`) is a
scroll window like the hangar `_descWindow`; copy that shape.

# Wave C, campaign

## C21 ☐ Campaign module over the seam

**Goal.** `OriginalCampaignScreen` is a sealed module owning the ten campaign screens
(`CampaignRoster`..`CampaignScrapbookZoom`) and their state, constructed without the shell;
`OriginalCampaignTests` are retargeted onto it with a thin wiring set left in `OriginalShellTests`.

**Evidence (confidence: traced).** `OriginalCampaign.cs` is 1233 lines; after A2 its state is
`_campaign`, `_profiles`, `_stock`, `_dataRoot`, `_campaignLayout`, `_flow`, `_briefingReturn`,
`_bookReturn` (`OriginalCampaign.cs:80-91`). The `ApplyFrame` MoveX cascade calls
`StepCampaignSideways`. The hangar module already reaches campaign through the host
(`CampaignPlanes`, `ResumeCampaign` on `IOriginalHangarHost`), so the campaign module and the hangar
module talk through the shell, never directly.

**Approach.** Same shape as A1 and B12 over the B11 seam. `CampaignPlanes` and `ResumeCampaign`
on the host are implemented by the shell delegating to the campaign module; keep them on the host
so the hangar module does not learn the campaign module's type. Retarget the campaign facts and
`CSVM/src/Testing/MenuOriginalCampaignSuites.cs`.

**Model recommendation.** high. Largest family, the only one with return-screen state across two
sub-flows (`_briefingReturn`, `_bookReturn`) and a flow object (`CampaignFlow`) the suites drive.

**Verify.** Full battery passes; `OriginalCampaignTests` has no `OriginalShell` construction;
`OriginalShell.cs` non-blank line count is below its pre-hangar 1493 (it is 1612 after the hangar
commit because the host members were added; the campaign dispatch is the last big block).

**⚠ Traps.** The campaign dialog walks (`_focusBeforeDialog` restore) now live on the shell after
A2; the module must not grow a second dialog. `_bookReturn` and `_briefingReturn` are `OriginalScreen`
values that can name a hangar screen; they stay `OriginalScreen`, not module-local enums.
