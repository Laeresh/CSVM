# Plane selection, combo boxes and the campaign modal

**ACTIVE PLAN** (written 2026-08-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

The campaign's CHANGE PLANE button cycles the plane in place because no picker screen exists
(`CSVM/src/UI/CampaignFlightCheckPage.cs:42`). The original opens a whole screen for it,
`[@PlaneSelection@]`, and that screen is authored in full: a background, two dropdown lists, a
silhouette and a four-line rating block per crew slot, a gun and hardpoint list, an EXPORT button
per slot, and ACCEPT/CANCEL SELECTIONS. This plan builds that screen, and the two widgets it needs
that the campaign board has never had: a combo box that opens a list, and a modal dialog drawn over
the board. The ammo screen then takes the same combo box and its own authored caption and field
positions, which is what fixes the text drawn over its fields today.

Out of scope: mouse input for the menus, which every screen here would want and none of them can
have yet (`BL-654`); and decoding the original's TOP SPEED and OFFENSE rating formulas, which this
plan ships a stated stand-in for (`BL-653`).

## Milestone goal

- CHANGE PLANE opens `PLANE SELECTION` at the original's authored geometry, and the pilot's and
  wingman's aircraft are picked from a list rather than cycled.
- A combo box exists as a board widget: closed it shows its value and its arrow, open it draws a
  scrolling list, and both the plane screen and the ammo screen use the one implementation.
- A modal dialog exists at the flow level, drawing the original's messagebox art over whatever
  screen raised it and holding every input until it is dismissed.
- Picking a plane another player or crew slot is already flying is refused with that dialog and the
  pick reverts, on the seated player's check and on a guest's alike.
- EXPORT writes the campaign plane, loadout included, where Instant Action and multiplayer read it.
- The ammo screen draws its calibre captions and its ammunition fields where the layout puts them,
  so no text sits over a field.

**The screens stay keyboard and pad driven.** Mouse support is one change across every menu the
shell draws, not a rider on this screen, and it has its own item.

## Decisions (2026-08-30)

| # | Question | Decision |
|---|---|---|
| 1 | How faithful is the new screen? | **Full authored layout** including the stat block, the weapons list and EXPORT. Every coordinate is in `LAYOUT.CSV` and every asset is extracted, so a reduced screen would be a choice to look unfinished. |
| 2 | How does a combo box work without a mouse? | **Enter opens a popup list**, arrows move inside it, Enter commits, Esc closes it unchanged. A closed combo still steps on the horizontal axis, so nothing that works today stops working. |
| 3 | What does EXPORT do here? | **Writes the plane and its loadout** into `CustomPlaneStore`, creating a record for a starter or granted aircraft that has none. Anything less makes langui 702's promise a half-truth. |
| 4 | Where does the modal live? | **On the flow**, so the plane screen, the export message and any later screen share one. The roster's existing confirm stage is left as it is. |
| 5 | What happens when a guest picks a taken plane? | **The same modal refusal and revert** the pilot and wingman clash gets, replacing today's silent skip. |
| 6 | What does a guest's refusal say? | **"Each player must fly a different plane."** Langui 710 names Pilot and Wingman, which a guest's check has no concept of, so the seated player keeps 710 verbatim and the guest gets a line of ours. |
| 7 | TOP SPEED and OFFENSE have no decoded formula. | **Draw all four ratings**, with a commented stand-in for those two and `BL-653` recording that the original's reading is unknown. |
| 8 | Does the flight check keep its in-place stepper? | **No.** One way to change a plane means the duplicate rule is enforced in one place. |
| 9 | Can a guest export? | **No, the button is not drawn on a guest's check.** A guest flies a session copy carrying the owner's plane name, and exporting it would rewrite the owner's stored record. |
| 10 | How is any of this seen? | **A debug flag opens the flow on a named screen**, so a scripted `--screenshot` captures each one against the reference images. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | Nothing opens a campaign screen for a screenshot, so this plan needs a new debug flag. | `--menu=` already takes a screen name and `LaunchMenu.OpenCampaignAid` already accepts twelve campaign values, including `campaign-flightcheck` and `campaign-ammo`, each over a scratch profile (`LaunchMenu.cs:1678`). A3 shrank to registering one more value. |
| 2 | The screenshot aid's seeded profile owns two planes, so CHANGE PLANE is not drawn on it. | `AidProfileStore` flies three missions and ordinal 2 grants "Jumping Jane" (`CampaignProgression.AircraftAwards`), so it owns three and the gate passes. Nothing needed seeding. |

## What the data actually ships

`extracted/rof/ASSETS/LAYOUT.CSV`, section `[@PlaneSelection@]`, with `GX=553`, `V3=444`,
`INFOX=430`, `ITEMSDISPLAYED=13`, `STDITEMH=15` and `STDTEXTH=16` resolved from `[GLOBALVARS]`:

| Widget | Type | Position | Size |
|---|---|---|---|
| `PS_BACKGROUND` | pane | 0,0 | `PS_BackGround.jpg` |
| `PS_P_PILOTPLANE` / `PS_P_WINGPLANE` | pane | 444,138 / 444,356 | `FC_PlaneIcons.Png`, 12 frames |
| `PS_T_TITLE` | text | 132,36 | 190x24 |
| `PS_T_MISSIONINFO` | text | 136,70 | 500 wide |
| `PS_T_PILOT` / `PS_T_WINGMAN` | text | 138,102 / 138,320 | 94x20 |
| `PS_T_PILOTPLANE` / `PS_T_WINGPLANE` | text | 236,106 / 236,323 | 400 wide |
| `PS_D_PILOTPLANE` / `PS_D_WINGPLANE` | dropdown | 138,132 / 138,350 | 271 wide, 13 rows of 15 |
| `PS_T_TOPSPEEDP` .. `PS_T_OFFENSEP` | text | 430, 228/245/262/279 | 140 wide |
| `PS_T_TOPSPEEDW` .. `PS_T_OFFENSEW` | text | 430, 446/463/480/497 | 140 wide |
| `PS_A_PLANEWEAPONSP` / `...W` | list | 578,228 / 578,446 | 160 wide |
| `PS_B_EXPORTP` / `PS_B_EXPORTW` | button | 560,168 / 560,385 | `FC_B_PaperButton.Png`, label `IDS_PS_B_EXPORT` |
| `PS_B_ACCEPT` / `PS_B_CANCEL` | button | 341,553 / 551,553 | `PS_B_AcceptSelections.png` / `PS_B_CancelSelections.png` |

`PS_B_SELLP` and `PS_B_SELLW` are authored at 560,158 and 560,375 and are **not drawn**:
`PLANESELECTION.SCRIPT` ends `gui_create` with an unconditional `deactivate(IS)` and
`deactivate(UOA)` over both of them, which is why the reference screenshot shows only Export.

`PLANESELECTION.SCRIPT`, the behaviour this screen copies:

- **Message `10015`, a dropdown selection changed.** The script reads which combo sent it, and sets
  a permit flag when the two combos differ or when only one crew slot is active. Permitted, it
  stores the pick in `US[slot]`. Refused, it sets message id 710, runs `messagebox.script`, assigns
  `sender.QG = US[slot]` and mails `10016` back at the combo, which is the revert. Either way it
  then calls `uiData` 2013 with the slot and the pick.
- **Message `10003` on `PS_B_EXPORT*`.** `gosCallback` 22 with the slot, then message id 702
  through the same messagebox.
- **`PS_B_CANCEL`.** Sets its guard flag and calls `uiData` 2013 for both slots with `XOA[0]` and
  `XOA[1]`, the picks the screen opened with. So the screen edits live and restores on cancel.
- **`PS_B_ACCEPT`.** `gosCallback` 12, the profile save.

Strings, from `extracted/rof/ui_strings.json` (`langui`): 501 `Poor`, 502 `Fair`, 503 `Average`,
504 `Good`, 505 `Excellent`; 702 `Your %1!s! has been exported and is now available for Multiplayer
and Instant Action missions.`; 710 `Pilot and Wingman must fly different planes.`

Art, all present under `extracted/rof/ASSETS/GRAPHICS/`: `PS_BACKGROUND.JPG`,
`PS_B_ACCEPTSELECTIONS.PNG`, `PS_B_CANCELSELECTIONS.PNG`, `MB_BACKGROUND.PNG`, `MB_B_ICON.PNG`,
`MB_B_BUTTONS.PNG`, `GN_B_LISTBOXARROWSMALLUP.PNG`, `GN_B_LISTBOXARROWSMALLDOWN.PNG`,
`FC_B_SCROLLBAR.PNG`, `FC_B_SCROLLUP.PNG`, `FC_B_SCROLLDOWN.PNG`.

The ammo screen's own rows, from `[@OrdinanceLayout@]` with `V3=136`, `V4=410`, `DROPWIDTH=148`:
captions `OL_T_GunName0..3` at 142, 105/147/189/231; ammo combos `OL_D_AMMO0..3` at 136,
120/162/204/246, 148 wide, 5 rows; rocket combos `OL_D_ROCKETS0..3` at 136 and `OL_D_ROCKETS4..7`
at 410, both columns at y 320/348/376/404, 148 wide, 12 rows. Today
`CampaignBoards.TextSlot` puts the whole row string at the caption position and nothing at the
field position, which is the overlap this plan removes.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen, never append) and is **deleted** from `backlog.md` (not marked FIXED there). New decodes
  land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything.** The instruments here mislead; cite the
  rule that bites per item.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.
- **Every screen stays engine-free.** A page is plain text, plain indices and authored coordinates
  (`ICampaignPage`), the shell owns every Godot control, and that is what makes these rules testable
  without the engine. A new widget that needs the engine to decide anything has been designed wrong.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the machinery

1. ☑ A1 A combo-box widget on the campaign board, closed field and scrolling popup list
2. ☐ A2 A flow-level modal dialog over the composed board
3. ☐ A3 Register `campaign-planeselection` with the existing `--menu=` aid (runs after B4)

### Wave B — the plane selection screen

4. ☐ B4 `CampaignScreen.PlaneSelection`, drawn at its authored geometry
5. ☐ B5 The duplicate-plane refusal and its revert
6. ☐ B6 EXPORT writes the plane and its loadout where Instant Action reads it

### Wave C — the screens that change around it

7. ☐ C7 The flight check opens the picker and loses its in-place stepper
8. ☐ C8 A guest's check gets the picker, with no EXPORT and a refusal of its own
9. ☐ C9 The ammo screen's captions and fields take their authored positions

### Wave D — the record

10. ☐ D10 `docs/formats/campaign-screens.md` gains its Plane selection section

## Dependency and parallelism notes

A1 and A2 are independent of each other and both block Wave B. A3 is the exception to its own wave:
the aid cannot open a screen that does not exist, so it runs after B4 even though its machinery is
Wave A's. B4 needs A1; B5 needs A2 and B4; B6 needs A2 and B4. C7, C8 and C9 all need Wave B done,
and C9 additionally needs A1 alone.

File contention, so these must not run as parallel worktrees: A1, B4 and C9 all edit
`CampaignBoards.cs`; A2, B4 and C8 all edit `CampaignFlow.cs`; C7 and C8 both edit
`CampaignFlightCheckPage.cs`. Items run in listed order, one at a time.

---

# Wave A — the machinery

## A1 ☑ A combo-box widget on the campaign board, closed field and scrolling popup list

**Goal.** A page can declare a combo box: a value, a list of entries and an authored rectangle.
Closed it draws the current entry and the down arrow. Confirming on it opens a list at the layout's
row height and row count, which the vertical axis moves through and scrolls, the confirm commits and
back closes unchanged. One implementation serves the plane screen and the ammo screen.

**Evidence (confidence: traced).** The two screens' dropdowns are authored widgets with resolved
geometry: `PS_D_PILOTPLANE` at 138,132, 271 wide, `STDITEMH` 15, `ITEMSDISPLAYED` 13;
`OL_D_AMMO0` at 136,120, `DROPWIDTH` 148, 5 rows. The arrow and scrollbar art the layout names
resolves through `[GLOBALVARS]` to `GN_B_ListboxarrowSMALLup/down.png` and
`FC_B_ScrollBar/Up/Down.png`, all four extracted. The board already draws rectangles
(`ICampaignPage.Fills`, used for a list's selection bar and its scrollbar track), text
(`BoardLine`) and framed art (`BoardPicture`), so nothing new is needed from the shell's renderer
beyond drawing this set in the right order.

**Approach.** Add a `BoardCombo` record to `CampaignBoards.cs` describing one combo (rectangle, row
height, rows displayed, entries, selected index, open state, focus state) and an
`IReadOnlyList<BoardCombo> Combos` member on `ICampaignPage` defaulted to empty on `CampaignPage`.
`CampaignBoards.For` composes an open combo last so its list draws over everything. The open list's
window is `[first, first + displayed)` with `first` tracked by the page, the scrollbar drawn only
when entries exceed the displayed count. Input goes through the existing seam: a page whose combo is
open answers `Move`, `Accept` and `Back` from the combo rather than from its row list, so the flow
needs no new verb. Do not add a mouse path, and do not give the combo its own focus stack: the
focused row owns which combo is addressable, exactly as `Step` is addressed today.

**Model recommendation.** high. It is the widest-blast-radius item in the plan, every later screen
sits on it, and the input seam it picks is hard to change once three screens use it.

**Verify.** New engine-free tests over the widget: opening and closing, the vertical axis wrapping
inside an open list, the window scrolling when the selection leaves it, the scrollbar appearing only
past the displayed count, and back closing without committing. Then A3's flag plus `--screenshot` on
the ammo screen once C9 lands, against `OriginalScreenshots/Campaign Flight Check Change Ammo
ComboBox.png`.

**⚠ Traps.** The list must draw over the screen's own text and art, including the ammo screen's
description panel, so composition order is part of the contract and not an accident of the renderer.
The plane list can hold 26 entries against 13 displayed, so a fixed-height popup is wrong. Keep the
closed combo's horizontal stepper working: the flight check's own stepper goes away in C7, but the
ammo screen's does not, and its existing tests assert it.

## A2 ☐ A flow-level modal dialog over the composed board

**Goal.** Any page can raise a modal carrying a message and one button, drawn as the original's
messagebox over whatever screen raised it, holding every input until it is dismissed, after which
the screen underneath is exactly as it was.

**Evidence (confidence: traced).** `[@MessageBox@]` authors `MB_P_BACKGROUND` at 0,0,
`MB_P_ICON` at 36,65, `MB_T_MESSAGE` at 94,70 with a 282x140 wrap box, and three button positions
of which this plan needs the centre one, `MB_B_CENTER` at 174,254. All three assets are extracted.
`PLANESELECTION.SCRIPT` reaches the box the same way from two different places, setting a message id
into a global and running `messagebox.script`, which is what makes this a facility rather than a
page's private state.

**Approach.** A `CampaignModal` record on `CampaignFlow`: message text, button label, and the
callback the confirm runs. `Flow.RaiseModal(...)` sets it, `Flow.Modal` exposes it, and `Move`,
`Step`, `Accept` and `Back` all route to the modal first and return without touching the page while
one stands. The shell draws it after the composed board. Leave `CampaignRosterPage`'s confirm stage
alone: it is a row-swap that works, and converting it is a separate change with its own tests.

**Model recommendation.** high. The input routing is the part that will bite, and a modal that lets
a keypress through to the screen underneath is a bug nobody will reproduce on purpose.

**Verify.** Engine-free tests: a raised modal swallows the vertical axis, the stepper and back; the
confirm runs the callback and clears it; the page's row and state are unchanged afterwards. Visually
through A3 against `OriginalScreenshots/Campaign Flight Check Change Plane Unique Warning.png`.

**⚠ Traps.** `Flow.Message` already exists as a one-line refusal band and is cleared by every
navigation. The modal is not that, and the two must not be wired together: a refusal that also
raises a modal would show the same words twice. A modal raised from inside `Accept` must not have
its own confirm consumed by the same press that raised it.

## A3 ☐ Register `campaign-planeselection` with the existing `--menu=` aid

**Goal.** `--menu=campaign-planeselection --screenshot` photographs the new screen over the same
scratch profile every other campaign aid uses.

**Evidence (confidence: traced. ⚠ This item's first reading was wrong and is recorded below.)** The
flag this plan set out to add already exists. `--menu=` takes a screen name
(`SessionSpec.MenuStartScreen`, `Launcher.cs:902`) and `LaunchMenu.OpenCampaignAid` already accepts
`campaign-flightcheck`, `campaign-ammo`, `campaign-guestcheck[:player]` and nine more, each walking
a seeded scratch profile under `%TEMP%\CSVM\menu-aid-profiles` so no aid can write into a real
campaign (`LaunchMenu.cs:1678-1812`). The seeded profile is also already rich enough for this
screen: `AidProfileStore` flies three missions, and ordinal 2's award grants airframe 2 as
"Jumping Jane" (`CampaignProgression.AircraftAwards`), so the profile owns three planes and
`ChangePlaneAllowed`'s three-plane gate passes.

**Approach.** One value in `OpenCampaignAid`'s accepted set, one `case` in `WalkCampaignAid` that
sets the mission, the slot and goes to the new screen, and the value added to `docs/cli.md`'s
`--menu=` list. The cursor-step argument the other campaign values take applies unchanged, which is
how a shot lands focus on EXPORT or on the wingman combo.

**Model recommendation.** medium. Three lines along a path with a dozen worked examples beside them.

**Verify.** `--menu=campaign-planeselection --screenshot` lands on the screen, and
`--menu=campaign-planeselection:3` moves the focus.

**⚠ Traps.** This item now runs after B4, since the aid cannot open a screen that does not exist.
An unrecognised value returns silently from `OpenCampaignAid` and leaves the launchscreen on its
mode page, so a typo looks like a screenshot of the wrong screen rather than an error.

# Wave B — the plane selection screen

## B4 ☐ `CampaignScreen.PlaneSelection`, drawn at its authored geometry

**Goal.** CHANGE PLANE opens a screen that looks like
`OriginalScreenshots/Campaign Flight Check Change Plane.png`: the mission line, a PILOT block and,
where the mission carries one, a WINGMAN block, each with its combo, its silhouette, its four
ratings and its gun and hardpoint list, over `PS_BackGround.jpg`, with EXPORT per slot and
ACCEPT/CANCEL SELECTIONS at the bottom.

**Evidence (confidence: traced, except the ratings).** Every coordinate is in the table above. The
combo entries read `"<plane name> - <airframe title>"`, which is what the reference screenshot's
list shows (`Gypsy Magic - Devastator`, `Blue Streak - Bloodhawk`). The gun and hardpoint list is
the same build-or-stock resolution the flight check already does
(`CampaignFlightCheckPage.ResolveGuns` and `.ResolveHardpoints`), rendered as the reference's
`(2) .50-cal.` and `(4) Hardpoints` rows. AGILITY and ARMOR come from `HangarEconomy.Bill`'s decoded
`AgilityStars` and `ArmourStars` mapped onto langui 501 to 505. TOP SPEED and OFFENSE have no
decoded formula and ship as a stand-in, recorded in `BL-653`.

**Approach.** A new `CampaignPlaneSelectionPage` beside the other pages, one line in
`CampaignFlow.Registry`, one `Buttons` entry and one `Chrome` entry in `CampaignBoards`. Follow the
ammo screen's working-copy model: the page holds the picks it opened with, edits live so the stat
block and silhouette follow the combo, and CANCEL restores what it opened with, which is what
`PS_B_CANCEL` does. ACCEPT writes `SelectedPlane` and `WingmanPlane` and saves the profile. Reuse
`SilhouetteFor`'s blueprint-TGA path rather than authoring new art. Do not touch the ammo screen or
the flight check in this item.

**Model recommendation.** high. It is the plan's centre, it holds the working-copy semantics, and it
is where a wrong reading of the layout becomes visible.

**Verify.** Engine-free tests over the page's rows, its combo entries, the wingman block appearing
only with the mission's wingman flag, and cancel restoring both picks. Then A3 plus `--screenshot`
against the reference image, checking the widget positions against the table above rather than by
eye alone.

**⚠ Traps.** The stat block is per selected plane and must follow the combo before ACCEPT, since
that is the whole point of picking with a preview. `ChangePlaneAllowed`'s two gates (missions 13 and
17, and fewer than three owned planes) stay exactly where they are on the flight check; this screen
is unreachable when they bite and must not re-implement them. The mission-title line is
`PS_T_MISSIONINFO` at 136,70 and is the mission's own name, not the screen title.

## B5 ☐ The duplicate-plane refusal and its revert

**Goal.** On the seated player's screen, moving the pilot's combo onto the plane the wingman is
flying, or the reverse, raises the modal reading langui 710 and leaves both picks as they were.

**Evidence (confidence: traced).** `PLANESELECTION.SCRIPT` message `10015` permits the change when
the two combos differ or when only one crew slot is active, and otherwise sets message id 710, runs
the messagebox, assigns `sender.QG = US[slot]` and mails `10016` at the combo. So the check is at
pick time, the refusal is modal, and the combo reverts rather than the pick being deferred to
ACCEPT.

**Approach.** The permit test sits in the page, against the other slot's current working pick, and
runs on commit from the combo rather than on every movement inside the open list, matching `10015`.
Refused, raise A2's modal with `Flow.Strings.Text(710, ...)` and restore the combo's selection.
A single-slot screen (no wingman) permits everything, which is the script's `!POA || !QOA` arm.

**Model recommendation.** medium. The rule is small and fully decoded; the care is in where it is
tested from.

**Verify.** Engine-free tests: a clash raises the modal and leaves both picks unchanged; the same
pick on a wingman-less mission is permitted; dismissing the modal returns to the screen with the
combo closed on its old value. Visually against
`OriginalScreenshots/Campaign Flight Check Change Plane Unique Warning.png`.

**⚠ Traps.** Refuse on commit, not on movement, or scrolling past the wingman's plane inside an open
list pops a dialog at every step. Compare the same way `CampaignFlightField.KeyOf` does, by name for
an owned plane, since two profile aircraft can share an airframe and that is legal.

## B6 ☐ EXPORT writes the plane and its loadout where Instant Action reads it

**Goal.** Pressing EXPORT on the seated player's screen makes that aircraft, with the ammunition
and ordnance the campaign has fitted, available in Instant Action and multiplayer, and says so with
langui 702.

**Evidence (confidence: traced).** `LaunchMenu.cs:2121` builds the Instant Action picker from
`PlanePickerRoster.Build(Planes, CustomPlaneStore.UserPlanes().List())`, so a hangar-built plane is
already offered there. What is not carried is the loadout: `CustomPlaneDef` has no ammunition or
ordnance field (`CSVM/src/Flight/CustomPlaneDef.cs`), those live on `OwnedPlane` in the profile. And
the two profile-seeded starters and every granted reward aircraft have no `CustomPlaneStore` record
at all, which is why `CampaignFlightCheckPage.BuildFor` falls back to the airframe's stock fit. The
script's own path is `gosCallback` 22 with the slot, then message id 702, whose text is
`Your %1!s! has been exported and is now available for Multiplayer and Instant Action missions.`

**Approach.** Add ammunition and ordnance to the stored plane record as optional fields, so an
existing file without them still loads and reads as it does today. EXPORT writes the flight check's
resolved build plus the plane's current picks under the plane's name and raises A2's modal with 702
formatted through `UiStrings`'s composite-format conversion. Then make the Instant Action side read
those fields when it flies a custom plane, or the export is a write nobody reads.

**Model recommendation.** high. It is the one item that changes a persisted format and reaches
outside the campaign screens.

**Verify.** A round-trip test: a record written without the new fields loads unchanged; one written
with them reads them back. An export test asserting the store holds the campaign's picks afterwards.
Then by hand: export a plane in the campaign, leave, start an Instant Action mission with it and
confirm the fitted ammunition is what flies.

**⚠ Traps.** Do not write under a name that collides with a hangar plane: a guest's record is named
for its airframe, which is exactly the collision `CampaignFlightField.IsStock` exists to prevent,
and it is why C8 draws no EXPORT at all. Do not silently overwrite a hangar-built plane's paint,
armour or engine when exporting a plane that already has a record; export sets the loadout fields
and leaves the build alone.

# Wave C — the screens that change around it

## C7 ☐ The flight check opens the picker and loses its in-place stepper

**Goal.** CHANGE PLANE is a plain button that opens the picker on the slot it belongs to. The
horizontal axis does nothing on that row, the row label carries no plane name, and the footer stops
offering a change the screen no longer makes.

**Evidence (confidence: traced).** `CampaignFlightCheckPage.Step` cycles `SelectedPlane` and
`WingmanPlane` in place and saves the profile on every step (`:274-305`); `Footer` advertises
`←→ Change Plane` on that row (`:141`); `AddSlot` appends `": <name>"` to the label (`:400`). The
original's flight check has no stepper: its two CHANGE PLANE buttons carry a `ScriptToExe` column
naming `PlaneSelection` with `@globals@ZQ` set to -1 or -2, which is the slot
(`docs/formats/campaign-screens.md`, the screen flow table).

**Approach.** `Accept` on a `ChangePlane` row sets the slot and goes to `PlaneSelection`; `Step`
stops handling that row. Rewrite the tests that assert the cycling behaviour rather than deleting
them: what they were protecting, that a plane change lands on the profile and saves, is still true,
just through the picker now.

**Model recommendation.** medium. A small, well-understood edit whose risk is in the tests it
invalidates.

**Verify.** The flight check's existing suite, with the stepping tests rewritten against the picker.
A3 plus `--screenshot` on the flight check to confirm the row labels and footer changed.

**⚠ Traps.** Both CHANGE PLANE rows open the same screen; the slot decides which combo opens
focused, not which screen is drawn. Do not remove `Step` from the page: the ammo rows do not use it,
but leaving the override in place with a narrower guard is the smaller change than reshaping the
base contract.

## C8 ☐ A guest's check gets the picker, with no EXPORT and a refusal of its own

**Goal.** On a guest's flight check, CHANGE PLANE opens the same screen with a single slot, listing
every stock airframe and every copy of the seated profile's aircraft. Picking one another player is
flying raises the modal reading "Each player must fly a different plane." and reverts. No EXPORT
button is drawn.

**Evidence (confidence: traced).** A guest's pick moves through `CampaignFlightField.Step`, which
walks the roster skipping anything `Taken` reports, so a taken plane is silently jumped rather than
refused (`CampaignFlightField.cs:151-179`). The roster itself is already built per guest, stock
airframes first, with the profile's aircraft copied rather than referenced (`NewGuest`), and
`Taken` already implements the comparison this refusal needs.

**Approach.** The page reads `Flow.Field.Current`: at zero it draws the seated player's one or two
slots, above zero a single PILOT slot over `Guest.Choices` with `Guest.Choice` as the pick and no
EXPORT. The refusal calls the same `Taken` logic rather than a second copy of it. `Field.Step`'s
skip-taken loop is what the picker replaces, so it goes with C7's stepper.

**Model recommendation.** high. Guest records are session-scoped copies with deliberate aliasing
rules, and the comments in `CampaignFlightField` name the exact bug that a careless read reintroduces.

**Verify.** Engine-free tests: a guest's picker lists stock airframes and profile copies; picking a
plane the seated player or another guest flies is refused and reverts; no EXPORT row exists on a
guest's screen; a guest's accepted pick does not touch the seated profile.

**⚠ Traps.** Never look a guest's build up by name in `CustomPlaneStore`. A stock record is named
for its airframe, and a hangar plane called "Devastator" would fit that guest with somebody else's
build; `IsStock` exists for this and both existing screens already ask it.

## C9 ☐ The ammo screen's captions and fields take their authored positions

**Goal.** The ammo screen reads like `OriginalScreenshots/Campaign Flight Check Change Ammo
Menu.png`: a calibre caption above each field, the ammunition name inside the field, and no text
crossing a field's edge. The four ammunition combos and the eight rocket combos open the same popup
list as the plane screen.

**Evidence (confidence: traced).** `CampaignBoards.TextSlot` draws the ammo screen's row text at
`(142, 105 + index*42)`, which is `OL_T_GunName0..3`, the caption slot, while `OL_D_AMMO0..3` sit
15 pixels lower at x 136 and are 148 wide. The row text itself is `"<slot title>: <ammo>"`
(`CampaignAmmoPage.GroupRowText`), which is wider than the field and belongs in two widgets. The
reference screenshot's captions are the calibres, `.50-cal.`, `.40-cal.`, `.30-cal.`, with a greyed
`No Gun` under them for an empty group.

**Approach.** The captions become `Captions` entries carrying the calibre for that group, from the
resolved build that `ResolveBuild` already computes. The picks become A1 combos at the dropdown
positions. The rockets take the two authored columns at x 136 and 410. Keep the horizontal stepper
on a closed combo, and keep ACCEPT/CANCEL and the working-copy model exactly as they are.

**Model recommendation.** medium. The layout is fully decoded and the page's model is already right;
this is repositioning plus adopting A1.

**Verify.** A3 plus `--screenshot` against both ammo reference images, closed and with a rocket
list open. The ammo page's existing suite must still pass unchanged apart from row-text assertions.

**⚠ Traps.** The description panel at 566,92 is a `DetailSlot`, not a row, and the open rocket list
draws over it in the reference image. The calibre caption is not the slot title: dropping the slot
title loses the `Inner Wing Guns` wording our screen shows today, which is the deliberate trade for
matching the original.

# Wave D — the record

## D10 ☐ `docs/formats/campaign-screens.md` gains its Plane selection section

**Goal.** The screen's decode is written where every neighbouring screen's is, so the next reader
finds it in the same place as the flight check and the ammo screen.

**Evidence (confidence: traced).** `campaign-screens.md` carries a section per script
(`CAMPAIGN`, `PASSENGERCABIN`, `CAMPAIGNINTRO`, `FLIGHTCHECK`, `ORDINANCELAYOUT`, the three
scrapbook scripts) and none for `PLANESELECTION`, though its flow table already names
`PlaneSelection` as CHANGE PLANE's destination.

**Approach.** One section beside the flight check's, carrying the widget table, the `10015` permit
rule with its revert, the export path through `gosCallback` 22 and langui 702, the cancel restore,
and the fact that both SELL buttons are deactivated unconditionally. State what the shipped build
does; the two undecoded ratings get a sentence pointing at `BL-653`.

**Model recommendation.** medium. Writing, over a decode this plan already did.

**Verify.** The section's claims re-checked against `PLANESELECTION.SCRIPT` and `LAYOUT.CSV` line by
line, and the file's own reader rules honoured (no dates, no event narration, evidence cited).

**⚠ Traps.** Docs state what is, not what was. No dates and no account of this plan's work in the
prose; the landing record belongs in the commit message.
