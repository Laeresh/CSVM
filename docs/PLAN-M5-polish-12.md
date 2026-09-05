# M5 Polish Run 12

**ACTIVE PLAN** (written 2026-09-05). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

Eleven items on the menu seam of the delivered M1 to M5 game, selected from `backlog.md` on
2026-09-05 by the criteria the author approved that day: open, unblocked, `[Impact: high]`, size
`[S]`, `[M]` or `[L]`, first step `code`, `data` or `decode`, no `[Owed-playtest]`, no
`[Research]`, nothing belonging to Milestone 6 (multiplayer, of which the backlog holds nothing),
and `BL-740` left alone because a session already holds it in its own worktree. The group is the
presentation seam run 11 deferred to "its own run": the findings of `PLAN-menu-presentations`
E44's sortie (`BL-703` to `BL-712`), the polish-8/9 sortie's menu findings (`BL-651`, `BL-655`,
`BL-691`), the difficulty row (`BL-570`) and the Built-in mouse (`BL-654`). Two exceptions to the
criteria are the author's own: `BL-654`, whose entry's premise is stale (Original has had a
pointer since its B13 commit), is kept and re-scoped to Built-in alone; and `BL-704`, tagged
`[Next: decide]`, joins as the eleventh item because its design decision was taken in the
grilling that scoped this plan (Decision 3). The items group into four waves by the system each
opens: the Instant Action screen and the export crossing, seats and joining, the campaign boards
and Game Options, and the pointer. Excluded on the same day: the play residues (`BL-714`,
`BL-700`, `BL-698`, `BL-717`, `BL-687`, `BL-739`, `BL-565`, `BL-690`, `BL-695`, `BL-335`), which
are the next run's shape; the low-impact menu findings (`BL-705`, `BL-706`, `BL-710`, `BL-711`,
`BL-723`, `BL-724`, `BL-658`, `BL-650`, `BL-696`, `BL-697`); and the AI-mode-machine group
(`BL-558`, `BL-728`, `BL-523`, `BL-550`), declined whole for five runs now on the ground that it
wants a dedicated decode run.

**Each of the eleven was re-verified still-open on 2026-09-05** against the record
(`git log --all --oneline --grep=BL-nnn` returned filings and cross-references only, with no
landing for any of them), the current `backlog.md` entry, the live plans (none), the worktree list
(one, `BL-740`'s) and the five unmerged worktree branches (each one commit behind an already-landed
plan item, none touching these). Every item's cited code was re-read in this session: nine held at
their cited lines; `BL-708`'s `enabled: false` is now the positional `false` on the same two
`AddStrip` calls (`OriginalInstantAction.cs:260`, `:294`); `BL-654`'s cited `LaunchMenu.cs` lines
drifted to `:434-468`, `:978` and `:2778-2843` and its "no mouse input at all" claim is false for
Original (`UI/Menu/Original/PointerSeat.cs`, `docs/menu-presentations.md` lines 129-140), which is
why it is re-scoped; and `BL-689`'s cited path is `UI/Menu/CampaignFeature.cs:165-173`, not
`Session/`. The scheduled entries were moved out of `backlog.md` into this plan in the same change
that created it. Remaining alternates if an item dies early, in order: `BL-689` (CM13's flight
check CHANGE PLANE, its path corrected above), `BL-705` (the scrapbook's two tabs), `BL-711` (the
refused-character sound).

## Milestone goal

- Original's Instant Action screen is a whole screen: a custom build is pickable and named for
  what it is, Weapon Loadout and Build Custom Plane open, and a campaign aircraft reaches the list
  only once exported, with an export dialog whose OK button can be seen.
- A second pilot can join under Original on every screen that launches a flight, is seen once
  joined, cannot take seat 0's pad, and picks an aircraft on a screen of their own.
- The campaign boards behave: a re-entered briefing starts over, the hangar shows the wallet while
  a plane is built, and Difficulty is a row on both presentations' options screens.
- The pointer is complete: Original's lists take the wheel and a thumb drag, and Built-in takes the
  mouse at all.
- A closing sortie judges at the controls every landed item whose acceptance needs eyes.

**Nothing here touches flight, the AI, or a mission's choreography.** Every item opens a menu
module; a finding in play during the sortie is filed, not fixed here.

## Decisions (2026-09-05)

| # | Question | Decision |
|---|---|---|
| 1 | Which items | **Run 11's criteria widened to `[L]`, the menu seam, about ten.** Eleven, because `BL-704` is pulled in on its decided design. |
| 2 | `BL-654`, half-landed | **Scheduled, re-scoped to Built-in.** Original already has the pointer; the entry's premise is corrected in this plan rather than in the backlog. |
| 3 | `BL-704`'s shape | **One screen per seat, in turn.** Seat 0 picks on the sortie screen; each joined seat then gets the campaign plane-selection screen's shape before FLY goes live. The two-press select-then-confirm walk stays. |
| 4 | `BL-703`(b), who has joined | **The seat strip over the campaign boards only once a second seat has joined.** Single-player fidelity untouched. |
| 5 | `BL-708`'s Build button | **Opens the shared wallet-free hangar and returns to the Instant Action screen**, and Original's remake-added BUILD PLANE door leaves the top level. Built-in's Mode-screen row stays. |
| 6 | `BL-570`'s row | **First row of Original's Game Options page**, the original's own position; Menu and Enhanced Graphics move down one. Built-in's Options screen gets a stepper too. |
| 7 | `BL-709`'s wingman list | **Stock only.** The Pilot Plane dropdown alone takes the customs, and names its rows `Stock <airframe>` and `<build name> <airframe>`. |
| 8 | Wave shape | **A: 691, 709, 708, 651. B: 703, 704. C: 712, 655, 570. D: 707, 654. E: the sortie.** `BL-691` first because `BL-651`'s entry names it as the possible cause of the export that "did not take". |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees; never use it in a
worktree session here. Use a local commit or a file copy.

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

### Wave A — The Instant Action screen and the export crossing

1. ☑ `BL-691` The export message box draws its OK button in ink the plaque hides
2. ☑ `BL-709` Instant Action's Pilot Plane list offers the stock airframes only
3. ☑ `BL-708` Original's Instant Action screen draws Weapon Loadout and Build Custom Plane disabled
4. ☑ `BL-651` Instant Action's build list shows every campaign plane, with no working Export gate

### Wave B — Seats and joining

11. ☑ `BL-703` Original never says who has joined, and its Instant Action lets nobody join
12. ☐ `BL-704` Original's sortie screen walks every seat down one shared aircraft list

### Wave C — The campaign boards and Game Options

21. ☑ `BL-712` A briefing re-entered from the cabin resumes its reveal mid-way
22. ☑ `BL-655` The campaign hangar never shows the wallet while a plane is being built
23. ☑ `BL-570` The difficulty setting has no menu row

### Wave D — The pointer

31. ☐ `BL-707` Nothing in Original scrolls with the wheel, and no scrollbar thumb can be dragged
32. ☑ `BL-654` Built-in takes no mouse input

### Wave E — At the controls

41. ☐ Closing sortie: every landed item judged at the controls

## Dependency and parallelism notes

`A1` precedes `A4`: `BL-651`'s entry says the export that "did not take" may be the dialog whose
OK cannot be seen, so `A4` starts by re-testing the export once `A1` has landed. `A2` precedes
`A3` and `A4`: both re-read the roster `A2` builds (`A3` after a build returns, `A4` to hide the
unexported). All four Wave A items edit `OriginalInstantAction.cs` or `OriginalRosters.cs`, so the
wave runs in order in one worktree. `B11` precedes `B12`: both edit `OriginalSeats.cs` and
`OriginalPresentation.cs`'s seat handling, and `B12`'s per-seat screen is reached through the join
`B11` opens. Wave C's three items are independent and touch three different modules
(`OriginalCampaign.cs`; `HangarFlow.cs` and `OriginalHangar.cs`; `OriginalGameOptions.cs`,
`OptionsStore.cs`, `Launcher.cs` and `LaunchMenu.cs`'s Options screen), so they can run in parallel
worktrees. `D31` and `D32` are independent (`D31` is Original's `PointerSeat` and list widgets,
`D32` is `LaunchMenu.cs`), but `D32` should land after `C23` so the new Options row is hit-testable
from the start. Wave B and Wave D both touch `OriginalPresentation.cs`; run them in sequence, not
in parallel worktrees. `E41` is last and needs the author at the controls; it cannot be delegated.

---

# Wave A — The Instant Action screen and the export crossing

## A1 ☑ `BL-691` The export message box draws its OK button in ink the plaque hides

**Goal.** The one-button message box over the plane-selection screen shows its OK button as the
original does: light text on a dark plaque inside a bordered strip.

**Evidence (confidence: traced).** Reported at the controls as "OK button is missing in the
dialog" (`PT-96`(a)). The original draws it: `OriginalScreenshots/Campaign Flight Check Change
Plane Export dialog.png` shows OK as light text on a dark plaque inside a bordered strip. The box
is composed as an overlay taking the palette of the screen beneath it
(`CSVM/src/UI/CampaignBoards.cs:340-341`, `Dialog(modal.Message, new[] { new
DialogButton(DialogCenterKey, modal.Button, 2, BoardInk.LabelActivate) }, layout)`), and the
plane-selection screen maps to `Paper`, whose `LabelActivate` is `Color(0,0,0)`
(`CSVM/src/UI/BoardPalette.cs:23`, and `:35`) against `Panel`'s gold (`:57`), which is why the
delete confirm over the profile screen reads and this one does not.

**Approach.** Give the message box an ink that does not depend on the screen it covers: either a
fixed dialog ink resolved in `ComposedBoardView` (`:180` is where `BoardInk` maps to the palette)
or a `DialogButton` ink the dialog composes for itself. Check the strip frame first (see Traps).

**Model recommendation.** medium. One composition site, but the fix must be judged against the
original's shot, not only against "now visible".

**Verify.** `.\RunProbe.ps1` with `--presentation=original --menu=campaign-planeselection:<steps>
--screenshot=.scratch\bl691.png` on a scratch profile that owns a named plane, stepping onto its
Export row and pressing it, then compare the strip against the original's shot. `MenuOriginalCampaignSuites`
for the walk. No goldens pin a menu shot today.

**⚠ Traps.** **Check the strip frame against the original's shot before concluding the ink is the
whole cause.** `CampaignBoards.cs:341` chooses strip frame 2 and `BoardInk.LabelActivate` on one
line, so a wrong frame would present identically and the ink fix would leave it invisible. The
second half of the same report, "needed 2 exports but then it worked", is `A4`, not this item.

**Verified.** <pending orchestrator run>

**Outcome.** Both halves of the trap were real. The ink was the reported cause in both
presentations: Original's `ComposeDialog` took `PlaqueInk` through the plane-selection screen's
`Paper` palette (dim blue on the black rollover frame), and Built-in's `Dialog(modal)` took
`LabelActivate`, `Paper`'s black on black. The frame was wrong on the Built-in path as well: it
chose the rollover frame 2 (pure black, `0,0,0`), where the original's shot shows OK on the normal
frame 1 (charcoal, the strip's `35,35,35` reading `51,51,51` in the capture) with the pointer
elsewhere. The messagebox's inks are the layout's own globals (`G2`/`G3` white on the two dark
frames, `G4` black on the light depressed one), so `BoardInk` gained `DialogPressed` beside
`Dialog`, `ComposedBoard.DialogInk(pressed)` names the rule, both composition sites use it, and
Built-in's box draws frame 1. To shoot the box, `--menu=campaign-planeselection:export` presses
the pilot's EXPORT in both presentations over a scratch build store (`CampaignAidProfiles.Planes`),
which every scratch-store campaign aid now opens over, so no aid can write `user://Planes`. The
`campaign-layout-parity` suite covers the new aid; three unit facts pin the frame, the ink and the
scratch write. Left as found: the box's icon draws the `?` frame where the original's export
notice shows `!`, not part of this item.

## A2 ☑ `BL-709` Instant Action's Pilot Plane list offers the stock airframes only

**Goal.** Original's Instant Action Pilot Plane dropdown offers the saved custom builds after the
eleven stock airframes, each row named `Stock <airframe>` or `<build name> <airframe>`, and a
picked custom flies. The Wingman Plane dropdown stays stock (Decision 7).

**Evidence (confidence: traced).** Reported at the controls, "Instant Action: 'Pilot Plane'
selection does not let me select the exported planes". Both dropdowns are built over
`InstantActionFeature.Airframes` directly (`CSVM/src/UI/Menu/Original/OriginalInstantAction.cs:356`
the pilot, `:360` the wingman), the decoded stock table with nothing appended. The sortie screens
append the saved builds through `OriginalRosters.Roster(customs)`, which is
`PlayerSetupFeature.BuildRoster(stock, customs, PlanePickerRoster.AirframeNode)`
(`CSVM/src/UI/Menu/Original/OriginalRosters.cs:44`), re-read on every show
(`OriginalPresentation.cs:223-229`).

**Approach.** Feed the pilot dropdown the roster the sortie screens read, with the naming rule
applied at the dropdown's row text (the roster's own names stay as they are for the sortie
screens). Route the pick's node resolution through the roster rule, not the name. Leave the
wingman call site on the stock table, which means the two dropdowns are no longer built by one
expression; make the split explicit rather than a flag.

**Model recommendation.** medium.

**Verify.** `MenuInstantActionSuites` gains a walk that saves a custom build, opens the Pilot Plane
dropdown, finds it after the stock rows under its `<build name> <airframe>` text, picks it and
launches; assert the launch's plane spec resolves to the airframe's stock node and the build's
name. Screenshot with `--presentation=original --menu=instant-action:<steps>` for the row text.

**⚠ Traps.** A custom flies its airframe's stock node, so a name-keyed lookup lands on the wrong
def. The `Stock <airframe>` prefix is a text of this dropdown only; the sortie screens and Built-in
keep their names, or every menu suite asserting row text moves.

**Verified.** <pending orchestrator run>

**Outcome.** Landed as designed. `OriginalShell` holds a `PilotRoster` read through
`OriginalRosters.Roster(_planes.List())` by `RefreshInstantActionRoster()`, which
`OpenInstantAction()` calls on every entry and which `A3` calls when Build returns to the screen;
the wingman dropdown is built by its own `WingmanPlaneDropdown()` over the stock table. The row
text is applied in `PilotRowText` alone, the airframe found by node (`AirframeOf`), and a pick moves
the feature onto the airframe's stock row with the build kept as an overlay (`_iaPilotBuild`), so
the def's `PlayerPlane` stays the stock name `GameSession` resolves by and the seat carries the
build's def (`MenuSeatChoice.Custom`) on the airframe's stock node. `--menu=instant-action:pilot-plane`
opens the list for a shot. `menu-original-instant-action` gained the walk; a perturbation dropping
the airframe suffix failed its four row-text checks. Finding for the sortie, not fixed here: with
the author's eight builds the open list is nineteen rows and runs past the page's foot over the
button row, since the remake's open list has no window although the layout's dropdown authors
`TotalDisplayed 20`.

## A3 ☑ `BL-708` Original's Instant Action screen draws Weapon Loadout and Build Custom Plane disabled

**Goal.** Both buttons open: Weapon Loadout edits the loadout of the seat the Player/Wingman radio
names, Build Custom Plane opens the shared wallet-free hangar and Back returns to the Instant
Action screen with its Pilot Plane list re-read (Decision 5). Original's top level loses its
remake-added BUILD PLANE door.

**Evidence (confidence: traced).** Both are deliberate placeholders:
`AddStrip(screen, rows, BuildKey, OriginalRowKind.Button, false, 0)`
(`CSVM/src/UI/Menu/Original/OriginalInstantAction.cs:260`) and the same `false` for
`WeaponLoadoutKey` (`:294`); the file's summary says "Build and Weapon Loadout disabled" (`:21`).
`LoadoutTarget` (`:114`) already says whose loadout the button edits. The top-level door is
`TextButton(HangarKey, "BUILD PLANE", DoorX, HangarDoorY, ...)`
(`CSVM/src/UI/Menu/Original/OriginalShell.cs:927`), walked by `MenuHangarSuites` at `:423`, `:518`
and `:589`. Built-in offers both features, so only the wiring is missing.

**Approach.** Point Weapon Loadout at the shared loadout feature for the seat `LoadoutTarget`
names. Point Build at `OriginalHangar`'s existing door with a return destination of the Instant
Action screen, and on return re-run the roster read `A2` introduced. Remove the top-level
`HangarKey` row and rewrite the three `MenuHangarSuites` walks to enter through Instant Action.
Keep the campaign's Plane Construction door untouched: it is the wallet-bearing one.

**Model recommendation.** medium.

**Verify.** `MenuHangarSuites`' three walks rewritten to the new door; `MenuInstantActionSuites`
walk: press Weapon Loadout with the radio on Wingman and assert the loadout screen's target is the
wingman; press Build, save a plane, Back, and find it in the Pilot Plane list. Screenshot the top
level under `--presentation=original --menu` to see the door gone.

**⚠ Traps.** The radio pair decides whose loadout the button edits, so the wiring is per seat.
Removing the door changes the top level's row count and every focus index after it; the
`OriginalShell` focus table is per screen, so re-check the `TopLevelButtons` walk in
`MenuOriginalSuites`.

**Verified.** <pending orchestrator run>

**Outcome.** Landed as designed, with one screen more than the approach named. Original had no
loadout screen of its own (the sortie screens launch stock fits and the campaign's ammo page runs
over `OwnedPlane`, not `LoadoutChoice`), so Weapon Loadout got one: `OriginalLoadout.cs` composes
the decoded `[@OrdinanceLayout@]` chrome over the fit the radio names, seat 0's `PlayerSeat.Fit`
for the pilot (which now rides the Instant Action exit, and drops on an airframe change as the
wingman fit does) and `InstantActionFeature.WingmanFit` for the wingmen; the section's four
ammunition and eight rocket fields map onto the airframe's firable gun slots and pylons over the
stock table's option lists, CANCEL and Back restore a snapshot of the picks, ACCEPT keeps them.
Build Custom Plane calls `OpenHangar()` with the Instant Action screen as the return, and
`ReturnFromHangar` re-runs `RefreshInstantActionRoster()` on the way back. The top-level `HangarKey`
row and constant are gone (eight rows now); `OriginalScreen.InstantActionLoadout` sits after
`InstantAction`. `--menu=instant-action:weapon-loadout` shoots the new screen. Suites: the three
`menu-original-hangar` walks enter through Build, `menu-original-instant-action` gained the loadout
and build walks, `menu-original-tracer` counts eight rows; the coverage test walks the loadout
screen by every input family and reaches the top level through Exit. Fixed on the way: A2 had left
`OriginalInstantActionTests` expecting `Autogyro` where the row now reads `Stock Autogyro`. Not
filmed: which sound plays and how the original's own loadout screen behaves under Instant Action
stay `CAP-50`'s.

## A4 ☑ `BL-651` Instant Action's build list shows every campaign plane, with no working Export gate

**Goal.** A campaign aircraft appears in the Instant Action and Free Flight pickers only once the
player has pressed Export on it, and the first press takes.

**Evidence (confidence: traced).** The mirror image of the campaign listing fixed in
`git log --grep=BL-634`. Ownership separates the campaign's roster from the shared
`user://Planes/` store, but every Instant Action and sortie list still reads the whole store
(`LaunchMenu.cs:2076`, `OriginalPresentation.cs:225`, `OriginalHangar.cs:528`). The original gates
the crossing behind Export (langui 1139, 702 and 1256, refusal 1254). The Export verb exists
(`CampaignFeature.ExportPlane`, `CSVM/src/UI/Menu/CampaignFeature.cs:418-430`, which writes the
loadout into the store's record) and the round trip works, but "needed 2 exports but then it
worked" (`PT-96`(d)). Whether the picker is not re-read after a successful export, or the first
press never confirmed because the message box drew no visible OK (`A1`), is unsettled.

**Approach.** After `A1` lands, re-test the export at the controls or through
`MenuOriginalCampaignSuites`; if the first press now takes, the "2 exports" half is closed by `A1`.
Then the gate: a marker the store record carries (a field beside `HasLoadout`,
`CSVM/src/Flight/CustomPlaneDef.cs:145`, which already distinguishes an exported record), read by
`PlayerSetupFeature.BuildRoster` and `PlanePickerRoster.Build` so a campaign-built plane without
the marker is not listed; hangar-built planes from the wallet-free doors carry the marker from
birth. Re-read the list when the marker changes (the sortie screens already re-read on every
show).

**Model recommendation.** medium.

**Verify.** A unit on `CustomPlaneStore` round-tripping the marker; a `MenuOriginalCampaignSuites`
walk that builds a plane in the campaign hangar, sees it absent from Free Flight's roster, exports
it once, and sees it present. Check that the two profile-seeded starters still resolve.

**⚠ Traps.** Do not give either mode its own build directory: the two profile-seeded starters are
never hangar-built and have no entry there, which is why the campaign roster resolves them from
the ownership record; a per-mode store would strand them. A file written before the marker existed
must read as exported, or every existing player's builds vanish from Instant Action on update.

**Verified.** <pending orchestrator run>

**Outcome.** The "2 exports" half is closed, and not by `A1` alone. `CampaignFeature.ExportPlane`
always wrote the store's record on the first press, so the store was never the second press's
doing; the suite walk asserts it by re-reading the file straight after one `PressExport`. What was
actually stale is the picker: both presentations read the build store on entry (Original at
`Activate`, Built-in at `Show`) and nothing re-read it on the way out of the campaign, so a plane
exported mid-visit was missing from the sortie lists that visit. `OriginalShell.CloseCampaign` and
`LaunchMenu`'s campaign exit now re-read it, which is the one door every way out of the campaign
passes through. `A1`'s invisible OK is why the press read as having done nothing at the controls.

The marker is `CustomPlaneDef.AwaitingExport`, written as `"awaitingExport": true` and **written
only when true**, so the absence of the field means exported and every file already on disk stays
in the pickers. `HangarFeature.Commit` sets it from the door (`Wallet != null`), so a campaign
build waits and a build from either wallet-free door never carries it; `CampaignDirector`'s awarded
aircraft are saved with it; `ExportPlane` clears it. `PlayerSetupFeature.BuildRoster` and
`PlanePickerRoster.Build` skip a def that carries it, which covers every human picker in both
presentations from two places. The two profile-seeded starters are untouched: they have no file in
the build store at all, and the campaign roster still resolves them from the ownership record.
Fixed on the way: Original's cabin opened PLANE CONSTRUCTION over the shell's own store while the
campaign feature held another, so ownership and the file it names could land in two directories
under a scratch store; the door now takes the campaign's store, as Built-in's already did.

---

# Wave B — Seats and joining

## B11 ☑ `BL-703` Original never says who has joined, and its Instant Action lets nobody join

**Goal.** Under Original a pad pressing START joins on the Instant Action screen as it does on Free
Flight, Dogfight and the flight check; seat 0's own pad can never join as an extra seat; and once a
second seat has joined, the campaign boards show the seat strip (Decision 4).

**Evidence (confidence: traced).** Three findings on one seam. (a)
`OriginalPresentation.JoiningOpen` (`CSVM/src/UI/Menu/Original/OriginalPresentation.cs:453-454`)
opens the per-pad join scan on `FreeFlight`, `Dogfight` and `CampaignFlightCheck` alone. (b)
Original's sortie screens have their own seat strip (`OriginalSeats.cs:29-33`, `:345`) but the
campaign boards carry none. (c) `MenuSeatDevices.ClaimP1Pad` (`MenuSeatDevices.cs:176`) is called
from Built-in alone (`LaunchMenu.cs:1074`), so under Original `P1Pad` stays −1 and `ScanJoins` lets
seat 0's pad join; `.scratch/logs/menu-20260903-230250.godot.log` logged `P2 joined on pad 1` then
`P3 joined on pad 0` under Original with two pads.

**Approach.** Add `InstantAction` to `JoiningOpen`; call `ClaimP1Pad` from Original each frame
before joining opens, as Built-in does at `LaunchMenu.cs:1074`; draw the sortie screens' seat strip
over the campaign boards as a `BoardLine` overlay when `setup.Seats` counts more than one joined,
at a position that clears the authored plaques (pick it from the board's free band and record it in
the code, not in the entry).

**Model recommendation.** medium.

**Verify.** `MenuOriginalSuites`: a second-seat join on the Instant Action screen takes; a walk with
seat 0 on a pad asserts that pad cannot join. Screenshot the flight check under
`--presentation=original --menu=campaign-flightcheck --debug-join=1` to see the strip, and the same
without `--debug-join` to see it absent. At the controls: `.\RunGame.ps1 --presentation=original
--menu` with two pads, one joining with START on the Instant Action screen and on a campaign flight
check, expecting the join to take in both and be visible where it happened.

**⚠ Traps.** **The Dogfight FLY gate is not a bug.** A later seat reaches Confirmed on its second
Accept (`OriginalSeats.StepSeat`, `:129`), and FLY going live only then is the design.
`--debug-join=` seats device-less players who can never confirm (`OriginalPresentation.DebugJoin`,
`:564`), so it can show the strip but cannot exercise a join. Do not lower `MinimumSeats`.

**Verified.** <pending orchestrator run>

**Outcome.** All three findings held and each landed. (a) The joining rule moved onto the shell as
`OriginalShell.JoiningOpen` (in `OriginalSeats.cs`), covering Instant Action beside Free Flight,
Dogfight and the flight check; the presentation reads it for the prime and the scan alike. (c)
`OriginalPresentation` calls `ClaimP1Pad` once on `Activate` and on every frame joining is closed,
as Built-in does off its Plane screen, so the pad seat 0 steers with is claimed before any join
screen opens. The `--run-tests` bundle disables pads, so the tracer drives the claim through
`MenuInput.LastActivePad` and reads `OriginalPresentation.Devices` back; under `--det` the claim
flaps once per frame between `Sync` (no connected pad) and the re-claim, which is why the suite
resets the field after its assertion. (b) `CampaignSeatPanel` composes the strip as a `BoardPanel`
overlay in `ComposeCampaign`, only with two or more seats, at authored (8, 6) with the sortie
strip's 14-pixel pitch and a 0.45 black scrim: the top-left desk margin, the one band no campaign
screen puts a plaque in (the book's tab sits at x 558, every other button on the bottom row); on
the briefing it lies over the top-left photo scrap, a picture rather than a plaque. The strip
carries no pick status, since the campaign's picks are the flight field's; on the flight check the
seat whose check shows draws focused. Shots: `.scratch/bl703_strip.png`,
`.scratch/bl703_nostrip.png` (byte-identical to the pre-change shot) and
`.scratch/bl703_montage.png`. Left open for `A3` and `B12`: Original's Instant Action FLY MISSION
still builds its exit for seat 0 alone (`OriginalInstantAction.cs`, the `FlyMissionKey` case), so a
seat joined on that screen is seated and kept but not flown; `MinimumSeats` is untouched.

## B12 ☐ `BL-704` Original's sortie screen walks every seat down one shared aircraft list

**Goal.** On Free Flight and Dogfight under Original, seat 0 picks on the sortie screen and each
joined seat then picks on a screen of its own in the campaign plane-selection screen's shape, in
turn, before FLY goes live (Decision 3).

**Evidence (confidence: traced, the design lead-only).** Reported at the controls beside `BL-703`:
"plane selection for multiple players in one list does not work good. should be more like the
campaign screen". The sortie screen draws one aircraft column that seat 0 and every joined seat walk
together, each seat's stage a tag on the seat strip (`OriginalSeats.cs`, `SeatStatus` at `:146`,
`SortieRows` at `:155`). The campaign's plane selection gives a guest its own screen
(`CampaignFlightField.cs:268-285`, `CampaignGuest`). Nothing on the sortie screen is decoded;
Dogfight is the remake's own mode.

**Approach.** After seat 0 confirms, each joined seat in player order gets a per-seat screen built
from the plane-selection board's shape (its list and silhouette) over the sortie roster; Accept
selects, a second Accept confirms, Back unjoins, as `StepSeat` does today. FLY on the sortie screen
goes live only when every joined seat is Confirmed, which is the gate it reads now. Reuse the
`CampaignGuest` shape where the campaign already solved the same thing.

**Model recommendation.** high. A screen the original never drew, on shared seat code, with a
walk the FLY gate depends on.

**Verify.** `MenuOriginalSuites`: two seats, seat 0 confirms, the second seat's screen appears,
selects and confirms, FLY goes live; Back on the per-seat screen unjoins and FLY stays dark.
Screenshot with `--debug-join=1` for the screen's composition, and at the controls with two pads
as part of `E41`.

**⚠ Traps.** Do not assume a pad per seat. Whatever replaces the shared list is also the route a
seat reaches Confirmed by, so keep the two-press walk. Nothing here is decoded; do not go looking
for an original layout to copy.

---

# Wave C — The campaign boards and Game Options

## C21 ☑ `BL-712` A briefing re-entered from the cabin resumes its reveal mid-way

**Goal.** Entering the briefing screen plays its reveal from the start, as the narration already
does, without reloading the briefing from disk.

**Evidence (confidence: traced).** The briefing is cached on the feature by mission
(`CampaignFeature.Briefing`, `CSVM/src/UI/Menu/CampaignFeature.cs:141-153`): rebuilt only when
`MissionSeq` changes, so leaving and returning to the same mission hands back the same
`CampaignBriefing` with its program counter, clock and revealed set. `OriginalCampaign.EnterBriefing`
(`OriginalCampaign.cs:387-391`) only shows the screen; `Restart` is called from REPLAY BRIEFING
alone (`:875`). The narration restarts on entry, so the two halves of one screen disagree.

**Approach.** Call `_campaign?.Briefing?.Restart()` in `EnterBriefing` (`BriefingScript.Restart`,
`BriefingScript.cs:417`, returns the script to a blank map with the narration starting over).
Check the Built-in campaign page does the same on its own entry.

**Model recommendation.** medium, low effort.

**Verify.** `MenuOriginalCampaignSuites`: enter the briefing, advance it, go to the cabin, return,
and assert the revealed set is empty and the narration count rose. The
`--menu=campaign-briefing:<seconds>` aid must land on the same frame after the fix; screenshot it
before and after and compare.

**⚠ Traps.** The aid depends on a reveal that can be advanced from zero, so it must still land on
the same frame or every briefing shot moves. Do not reset by clearing the cache on `MissionSeq`;
reset the script, not the load.

**Verified.** <pending orchestrator run>

**Outcome.** Landed one seam up from the Approach, in `CampaignFlow.GoTo`, which every entry of
the briefing screen in both presentations passes through (Original's `ShowCampaign` walks its
mirrored flow through it; Built-in's cabin, flight-check, contents and scrapbook pages call it
directly): opening the briefing restarts a reveal whose clock has moved and leaves a fresh one
alone, so the load stays cached on the feature, a first entry still reads one narration start, and
the `--menu=campaign-briefing:24` aid lands on the same frame (raw-pixel md5 identical before and
after under Original and under Built-in, one narration start each). REPLAY BRIEFING keeps its own
`Restart` call, since it is a press on the screen rather than an entry. `menu-original-campaign`
and `menu-campaign-journey` each drive the reveal past its first placed element after REPLAY,
return to the cabin, re-enter, and read a clock under a second, the fresh element count, no
revealed objective and a narration count of three, where the unchanged build read ten seconds and
two. The Original suite's earlier assertion that the re-entry "reopens the briefing where REPLAY
left it" was the defect pinned as expected behaviour and is replaced.

## C22 ☑ `BL-655` The campaign hangar never shows the wallet while a plane is being built

**Goal.** Every Plane Construction screen after the buy row (airframe, engine, hardpoints, armour,
guns) shows the money on hand beside the running total, and marks a row the remaining funds cannot
cover, without blocking the pick.

**Evidence (confidence: traced).** Reported at the controls on the landed Plane Construction
screen (`git log --grep=BL-634`). The wallet is drawn once, as the detail line of the plane-selection
screen's Buy row (`HangarFlow.CampaignDetail`, `CSVM/src/UI/HangarFlow.cs:736-739`, langui 1149
`$$$ on Hand:`), and no later page draws it. The running cost exists: `HangarEconomy.Price` is what
the totals row draws.

**Approach.** First step is data: read what the original puts on those screens (`docs/org/hangar.md`,
"The campaign wallet", and the langui money strings) before choosing a layout. Then a wallet line
beside the totals on each hangar page over a `CampaignWallet` (`HangarFlow.Campaign`, null on the
wallet-free doors, where nothing is drawn), and a mark on any row whose price exceeds the funds
left, in both presentations (`OriginalHangar.cs`'s composers and Built-in's hangar pages).
The original's own layout, from the data: `PLANECONSTRUCTION.SCRIPT` puts `px_t_cashtitle`
(langui 1149) and `px_t_cash` on the hub chrome at 615,0 and 615,25, over the sticky-note art in
`PX_BackGround.jpg`, so the note stands on every tab and the totals page beside `PLANE COST:
$%1!d!` (1036) in the header; `OriginalScreenshots\Campaign CAP-40 Plane Construction 1.png` and
`2.png` show it on the Engine and Armor tabs (`$$$ on Hand` / `$21840`). No dropdown row is marked
in the original (`docs/org/hangar.md`, "The cash note").

**Model recommendation.** medium.

**Verify.** `MenuHangarSuites` over a campaign wallet: the wallet line is present on each page with
the profile's funds, absent over the wallet-free doors, and a part priced above the funds carries
the mark while remaining selectable. Screenshot each page under
`--presentation=original --menu=campaign-hangar`.

**⚠ Traps.** Do not block an unaffordable selection outright: the decoded flow refuses at the
purchase, not at the part, and a screen that hides parts you cannot yet afford also hides what you
are saving toward.

**Verified.** <pending orchestrator run>

**Outcome.** Original already composed the two cash rows over a wallet (`OriginalHangar.cs`,
`ComposeHubChrome`), but no `--menu=` aid could reach the hub over one and nothing marked a row;
Built-in drew the wallet on the buy row alone. Now `HangarFlow.WalletLine` puts langui 1149 with
the funds beside the totals on every page after the buy row (empty over the wallet-free doors and
on the inventory, whose buy row keeps it), `IHangarPage.CostWith` gives each row the total its
pick would leave and `HangarFlow.RowUnaffordable` / `RowText` prefix `HangarFeature.UnaffordableMark`
(`✕ `) where the funds fall short, with the line error-coloured; Original bakes the same mark
into its priced dropdown lists (airframe, engine, armour, guns, hardpoints) and draws the cash
figure in the problems ink once the build outruns it. Paint, name and the inventory rows take no
mark, Purchase Now keeps its own flag, and no pick is refused. Original gained the shared
`campaign-hangar` aid with a `:tab` argument (`docs/cli.md`), which is what the eight
`.scratch/bl655_*.png` shots and `bl655_montage.png` were taken with; the suites read the aid
profile's $900 against a $9610 Devastator. The decode: `docs/org/hangar.md`, "The cash note".

## C23 ☑ `BL-570` The difficulty setting has no menu row

**Goal.** Difficulty (Normal / Hard / Hardest) is the first row of Original's Game Options page
and a stepper on Built-in's Options screen, persisted in the options file, read by a normal launch,
with `--difficulty=` still winning for a scripted run (Decision 6).

**Evidence (confidence: traced).** The scale is live (`Flight/Difficulty`, `--difficulty=`, parsed
at `SessionSpec.cs:990` into `SessionSpec.Difficulty`, `:364`), but the flag is the only way to set
it. The original puts it on the game-options screen: `IDS_GO_DIFFICULTY_TITLE` "Difficulty",
`IDS_GO_DIFFICULTY_DESC` "Select the difficulty level for a solo campaign", over the three
`IDS_DIFFICULTY` rows (`rof/ui_strings.json` ids 109-111). Original's page holds two options in a
table (`OriginalGameOptions.cs:60-69`, `GameOptions`), a dropdown and a radio, over a three-row
plate; the options file is `OptionsDef` (`Utils/OptionsStore.cs:12-17`), whose one writer is
`Launcher.ApplyOptions` (`Launcher.cs:1285`); Built-in's Options screen is four rows
(`LaunchMenu.cs:3053`, `:3109-3115`).

**Approach.** A `Difficulty` field on `OptionsDef` (no version bump: a missing field reads as never
set, `OptionsStore.cs:31-34`), validated against `Flight.Difficulty.Parse`'s words. A `GameOption`
entry at the head of the table drawn as a dropdown over the three `IDS_DIFFICULTY` words, and a
stepper row on Built-in's screen ahead of the two existing ones. `ApplyOptions` writes it; the
launch reads it into `SessionSpec.Difficulty` only when no `--difficulty=` flag was given and the
run is not `--det`, the same rule the saved graphics mode follows (`Launcher.cs:583`).

**Model recommendation.** medium.

**Verify.** A unit on `OptionsStore` round-tripping the field and rejecting an unknown word;
`MenuOriginalSuites`' Game Options walk (`:291-322`) updated for the new first row, with the
`game-options` aid's checked pose re-checked; a `--run-tests` case that a saved `hard` reaches
`FlightRosterInputs.Difficulty` on a plain launch and does not under `--difficulty=normal`.

**⚠ Traps.** It is a campaign-scope setting and Instant Action does not read it: an IA wave's own
skill stands in for that spawn (`InstantActionDirector.cs:196`). Do not wire the row into the IA
path or a wave flies at two difficulties. It selects a hit-point tier only; the description must not
say it changes how well the enemy flies or shoots. The Game Options screenshot aid's row index
moves by one.

**Verified.** <pending orchestrator run>

**Outcome.** `OptionsDef.Difficulty` carries one of the three campaign words (`Flight.Difficulty.Word`),
validated on read against that set alone, no version bump. Original's Game Options table opens with
the Difficulty dropdown at the authored `GO_D_DIFFICULTY` box over Normal / Hard / Hardest, with
Menu and Enhanced Graphics one row down each (the checkbox now sits on the head-turn row its offset
was read from); Built-in's Options screen is five rows with a wrapping Difficulty stepper first.
`OptionsApplyExit` carries the word and `Launcher.ApplyOptions` saves it. `Launcher.LaunchSession`
folds the saved word into the spec through `SessionSpec.WithSavedDifficulty` at every launch: a
parsed `--difficulty=` flag (`DifficultyExplicit`) wins, a `--det` run reads no saved option, and a
missing or refused word changes nothing, so the tier reaches `FlightRosterPolicy.Difficulty` on a
plain launch and an Options apply reaches the next flight without a restart. Instant Action is
untouched; a wave's own skill still outranks the session's per spawn. The `game-options:checked`
aid steps down twice now. New suite `options-difficulty-launch`; `OptionsStoreTests`,
`OriginalShellTests`, `OriginalCoverageTests`, `menu-original-tracer` and `menu-launch-return`
updated for the new first row.

---

# Wave D — The pointer

## D31 ☐ `BL-707` Nothing in Original scrolls with the wheel, and no scrollbar thumb can be dragged

**Goal.** Every list under Original scrolls with the mouse wheel and its thumb can be dragged; the
arrows keep doing what they do.

**Evidence (confidence: traced).** No menu code reads a wheel: `MouseButton.WheelUp`/`WheelDown`
appear only in `UI/OrbitCamera.cs:111-115` and `Flight/SpectatorCamera.cs`. The pointer carries a
position with a held and a just-clicked flag (`MenuPointer`, `MenuCommands.cs:29`, produced by
`PointerSeat.Poll`), and no wheel field. The thumb is drawn but is not a control: the
previous-missions page draws the arrows and thumb (`CampaignPreviousMissionsPage.cs:376-398`) and
hands out boxes for mission rows alone (`RowBox`, `:448`).

**Approach.** A wheel delta on `MenuPointer` produced by `PointerSeat`'s injected read, mapped by
the presentation into list steps; a thumb box from each list widget plus a held-pointer delta for
the drag. Land it for every list at once: the scrapbook, the previous-missions page, the decals in
Plane Construction, the Instant Action dropdowns and contents window, and the aircraft column on
Free Flight and Dogfight (`B12`'s per-seat screen included once it exists).

**Model recommendation.** medium.

**Verify.** `MenuOriginalSuites` and `MenuOriginalCampaignSuites` with a test-driven pointer: a
wheel step over each list moves its window by one row; a drag on the thumb moves it in proportion;
the arrows' behaviour is unchanged. At the controls in `E41`.

**⚠ Traps.** The wheel is a comfort the original never had, so it is an addition on top of the
decoded screens and must not change what the arrows do. A wheel that works on one screen and not the
next reads as broken. Built-in's wheel is `D32`'s, not this item's.

## D32 ☑ `BL-654` Built-in takes no mouse input

**Goal.** Built-in's launchscreen takes the mouse: a pointer over a row focuses it, a press and
release on the same row confirms, a wheel over a list scrolls it. Original is unchanged (it already
has all three after `D31`).

**Evidence (confidence: traced).** Every `Control` the launchscreen builds is
`MouseFilterEnum.Ignore` (`CSVM/src/UI/LaunchMenu.cs:434-468`, `:978`, `:2778-2843`), so a click
reaches nothing. The entry's original claim that no menu takes a mouse is false for Original since
its B13 commit (`UI/Menu/Original/PointerSeat.cs`; `docs/menu-presentations.md` lines 129-140 say
Original maps the window-pixel pointer through the same `BoardFit` its board is drawn with), which
is why this item is Built-in alone (Decision 2).

**Approach.** Built-in is drawn from Godot `Control`s rather than a composed board, so the hit test
is Godot's own: switch the row controls to `MouseFilterEnum.Stop`, route their `gui_input` into the
same `MenuCommands` the keyboard produces (a hover is a focus move to that row, a click is Accept,
a wheel is a cursor step), and keep focus one thing so a pointer move and a pad press cannot each
own a different row. Reuse `PointerSeat` for the read where the seat model allows it.
Every centred-layout screen (Mode, Chapter, the wizard, Options, Controls, the hangar pages, a
lone pilot's aircraft list and loadout) draws one `CursorRow` control per row, and the split
aircraft screen one per roster row per pane; only the campaign screens draw no row controls, being
one `ComposedBoardView` surface.

**Model recommendation.** high. An `[L]` item over the largest UI file in the repo, with every
menu suite's walk as its regression surface.

**Verify.** Every `Menu*Suites` walk still passes unchanged (they drive the keyboard); a new
`MenuHostSuites` case drives a click on a row and a wheel over the plane list. At the controls in
`E41`.

**⚠ Traps.** Focus must stay one thing. A `MouseFilter` change on a container can swallow input
meant for the panes behind it (the splitscreen rig sits under the same layer set); check
`SplitScreen.cs:197-270`'s `Ignore` controls stay ignored. Do not add mouse to the flight HUD.

**Verified.** <pending orchestrator run>

**Outcome.** Landed as Godot's own hit test, no `MenuPointer` read: `LaunchMenu.Pointable` sets
player 1's row controls to `MouseFilterEnum.Stop` and connects their `gui_input` and mouse-enter
and mouse-exit signals to `PointerEvent`, which holds the mouse's commands for the frame
(`WithPointer` folds them into player 1's frame in `_Process` and `Drive`) because `Rebuild` frees
the very control an event is dispatched through. A motion is the cursor step onto the row, a press
and release on one row is that step plus Accept in the same frame (the release rule is
`BaseButton`'s: it confirms only while the pointer is still inside the pressed control, so a drag
off cancels), a wheel notch is a step, with the list column passing the wheel between rows. The
frame's own step outranks a hover, so a pad and the mouse cannot each own a row; a click off a
locked airframe is refused rather than confirming the locked one. The campaign boards under
Built-in stay on the keys (no row controls; the TODO's answer above); the split aircraft screen
takes the mouse in player 1's pane alone, the mouse being seat 0's device. `SplitScreen.cs` and
`PointerSeat` are untouched; the containers under the rows keep their filters. New suite
`menu-host-pointer` in `MenuHostSuites.cs` injects the events through the row controls' signals
(`LaunchMenu.RowControl`), red with the wiring off, green with it on. A hover cannot be posed
headless (the aid cannot move the mouse, and the focused row is the one cursor either device
moves), so `.scratch/bl654_hover.png` shows the aircraft list as the row controls now draw it.

---

# Wave E — At the controls

## E41 ☐ Closing sortie: every landed item judged at the controls

**Goal.** Each landed item is judged by the author at the controls in one sitting, its verdict
recorded one line per item in the closing commit, and every finding filed.

**Evidence (confidence: n/a).** The pattern of runs 10 and 11 (`git log --grep=PLAN-M5-polish-11`).

**Approach.** `.\RunGame.ps1 --presentation=original --menu` with two pads and a mouse, walking:
the top level (no BUILD PLANE door), Instant Action (a custom in the Pilot Plane list with its
names, Weapon Loadout, Build and back, a join on START), Free Flight with a second seat picking on
its own screen, a campaign flight check with a second seat joined and seen, an export whose OK
reads and takes first time, a briefing left for the cabin and re-entered, Plane Construction with
the wallet on every page, Game Options with Difficulty first, and the wheel and thumb drag on the
scrapbook and the previous-missions page. Then Built-in with the mouse. `<TODO: the order of the
walk and which items share a screen; settle it when the last item lands.>`

**Model recommendation.** n/a, the author at the controls.

**Verify.** The sitting itself. A FAIL re-files the item to `backlog.md` with what was seen.

**⚠ Traps.** The user's eyes outrank the suites: a walk that passes headless and reads wrong at
the controls is a fail. Film nothing of the original for this; every item here is a remake reading.
