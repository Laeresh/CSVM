# M5 Polish Run 13

**ACTIVE PLAN** (written 2026-09-06). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

Ten items on the menu seam's leftovers, selected from `backlog.md` on 2026-09-06 by the criteria
the author approved that day: the low-impact menu findings run 12 named and deferred, open and
unblocked, first step `code` or `decode`, no `[Owed-playtest]`, no `[Next: look]`, no
`[Next: decide]`, so every item can be built and verified headless and the run ends in one closing
sortie. Run 12 listed its own deferral as `BL-705`, `BL-706`, `BL-710`, `BL-711`, `BL-723`,
`BL-724`, `BL-658`, `BL-650`, `BL-696`, `BL-697`; eight of those ten are here. `BL-706` (the
off-centre PLANE NAME dialog) and `BL-696` (Original's missing door into the rebinding screen) are
dropped under the criteria, being `[Next: look]` and `[Next: decide]`, and both are named in the
closing sortie instead. Their two places go to `BL-744`, filed by run 12's own Wave A, and
`BL-659`, the screenshot aid without which `BL-658`'s changed panes cannot be pinned by a golden.

**Each of the ten was re-verified still-open on 2026-09-06** against the record
(`git log --all --oneline --grep=BL-nnn` returned filings, cross-references and run 12's own
deferral list only, with no landing for any of them), the current `backlog.md` entry, the live
plan (`PLAN-M5-polish-12`, whose twelve items are all `☑` and whose checklist names none of
these), the worktree list (one, `m5-polish-12`, already merged into main at `5c5bc2c5`) and
`playtest.md` (no `PT-` entry references any of the ten). Every item's cited code was re-read in
this session and **all ten claims held**, three of them with a correction worth carrying. The
message box loads `MB_B_Icon.Png` with a frame count and no frame *selector*
(`CampaignBoards.cs:370`), so the `?` is what every call site gets rather than a chosen default.
`DetailSlot` returns null for every screen but `Ammo` (`CampaignBoards.cs:432-441`), so `BL-658`'s
trap about the plane selection screen and the roster "sharing" it is wrong: they share nothing and
cannot regress. And the campaign aid already carves one named word out of its numeric argument
(`CampaignAidProfiles.ExportArgument` to `PressExport`, `LaunchMenu.cs:1860-1888`, landed with run
12's `BL-691`), which is the precedent `BL-659`'s fix extends rather than invents. The scheduled
entries are moved out of `backlog.md` into this plan in the same change that creates it.

Alternates if an item dies early, in order: `BL-711`'s second route if the cap overrun already
beeps and the item shrinks to nothing, then `BL-510` (the auto-land prompt's placeholder line),
then `BL-653` (the plane selection screen's stand-in TOP SPEED and OFFENSE ratings).

## Milestone goal

- The hangar's record is the original's: an armour step moves the displayed units, the cost and
  the weight by the amounts the decode actually supports, the two wings move together as one
  choice, and a profile cannot buy past the slot cap the original refuses at.
- The campaign boards fill the panes their layout authors: the ammo screen reads a gun's
  ammunition and a pylon's ordnance side by side, and a screenshot aid can open a combo box so
  both panes and every other open list can be pinned by a golden.
- Original's dialogs and rows are complete: the export notice draws the icon its own reference
  shot shows, and every non-scrap row on the scrapbook page answers the pointer, the two tabs
  first.
- The chrome around the menu behaves: a refused character beeps, more than player 1's keymap can
  be reached, and quitting shows the game rather than Godot's default sky.
- A closing sortie judges at the controls every landed item whose acceptance needs eyes, and
  answers `BL-706` and `BL-696`, the two this run dropped.

**Nothing here touches flight, the AI, or a mission's choreography.** Every item opens a menu or
input module; a finding in play during the sortie is filed, not fixed here.

## Decisions (2026-09-06)

| # | Question | Decision |
|---|---|---|
| 1 | Which items | **Run 12's deferral list, code-ready only, about ten.** The menu seam is the system this run and the last one both open, and finishing it is cheaper than carrying eight `[S]` items into a third run. |
| 2 | `BL-706` and `BL-696` | **Dropped, and named in the closing sortie.** `[Next: look]` and `[Next: decide]` both need the author, which the approved criteria exclude; `E41` is where they get answered. |
| 3 | Their two places | **`BL-744` and `BL-659`.** `BL-744` is the same message box `A1` fixed in run 12 and is one frame index; `BL-659` is what lets `B12`'s work be pinned at all. |
| 4 | `BL-723` before `BL-724` | **The scale is decided first.** Which number a row writes has to be settled before two rows are coupled to write it, or the coupling lands on a value that then moves. |
| 5 | `BL-723`'s losing reading | **The decode settles it, and the doc loses a paragraph.** `docs/org/hangar.md` carries two contradictory readings of one handler; the item is not done until one of them is deleted. |
| 5a | `BL-724`'s row count | **Four rows, coupled to each other.** The author's recall of the original is a combo box per wing where changing either changes the other, so the entry's "three rows or four" question is answered and `A2` is a code change, not a decode. |
| 6 | Wave shape | **A: 723, 724, 650. B: 659, 658. C: 744, 705. D: 711, 697, 710. E: the sortie.** Grouped by the module each opens, so no two waves contend on a file. |

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

### Wave A — The hangar record

1. ☐ `BL-723` An armour step adds 4 lb where the original's dropdown step adds 20
2. ☐ `BL-724` The ARMOR screen sets each wing on its own where the original moves both together
3. ☐ `BL-650` The campaign hangar's decoded slot cap is not enforced

### Wave B — The campaign boards' panes, and the shots that pin them

11. ☐ `BL-659` No screenshot aid can open a campaign combo box
12. ☐ `BL-658` The ammo screen shows one description pane where the original fills two

### Wave C — Original's dialogs and rows

21. ☑ `BL-744` The export notice draws the message box's `?` icon where the original draws `!`
22. ☐ `BL-705` The scrapbook's Best to Date and Most Recent tabs cannot be clicked

### Wave D — Text entry, seats, and the way out

31. ☐ `BL-711` A refused character in a name box makes no sound
32. ☐ `BL-697` Only player 1's keymap can be reached
33. ☑ `BL-710` Quitting flashes Godot's default sky before the window closes

### Wave E — At the controls

41. ☐ Closing sortie: every landed item judged at the controls, and `BL-706` and `BL-696` answered

## Dependency and parallelism notes

`A1` to `A2` is a chain and the only hard one: `A2` couples two rows to a value `A1` may move, so
coupling before the scale is settled lands on a number that then changes. `A3` is independent of
both but shares `docs/org/hangar.md` with them, so it runs after `A2` rather than beside it.
`B11` to `B12` is a soft chain: `B12` can be built without `B11`, but its **Verify** wants a golden
of the open rocket list, which only `B11` makes reachable, so running them in the other order costs
a re-pin. Wave C's two items both edit `CSVM/src/UI/Menu/Original/OriginalCampaign.cs`, so **never
run `C21` and `C22` in parallel worktrees.** Wave D's three touch three different modules
(`UI/MenuInput.cs`, `UI/LaunchMenu.cs`, `Session/Launcher.cs` plus `Session/GameSession.cs`) and
are the run's parallel opportunity, with the caveat that `D32` also reads `LaunchMenu.cs`, which
`B11` writes: give `B11` ownership of `LaunchMenu.cs` and land it before `D32` starts, or run
`D32` first and rebase `B11` onto it. `E41` needs every other item landed and merged.

Waves A, B and C are mutually independent and can run as three concurrent worktrees; wave A owns
`CSVM/src/Flight/` and `CSVM/src/UI/Hangar*`, wave B owns `CSVM/src/UI/LaunchMenu.cs` and
`CSVM/src/UI/Campaign*Page.cs`, wave C owns `CSVM/src/UI/Menu/Original/`. `CampaignBoards.cs` is
read by B and C both: **B12 writes it (the pane), C21 writes it (the icon frame)**, which is the
one cross-wave contention, and the cheaper resolution is to land `C21`'s two-line frame change
first and rebase `B12`.

---

# Wave A — The hangar record

## A1 ☐ `BL-723` An armour step adds 4 lb where the original's dropdown step adds 20

**Goal.** One press on an ARMOR row moves the displayed units, the dollars and the pounds by the
amounts the original's own dropdown moves them, and `docs/org/hangar.md` carries one reading of
that handler instead of two.

**Evidence (confidence: traced for the symptom, the resolution owed to a decode).** Reported at
the controls: "we increment by 5 units so it should add 20 not 4". `HangarEconomy` holds
`ArmourUnitCost = 4` and `ArmourUnitWeight = 4` (`CSVM/src/Flight/HangarEconomy.cs:82-83`, re-read
2026-09-06) and charges `armourUnits * ArmourUnitCost` / `* ArmourUnitWeight` at `:150-151`, while
`HangarArmourPage.Step` steps the stored row by one (`CSVM/src/UI/HangarArmourPage.cs:57-62`) and
its readout prints `units * ArmourUnitCost` and `units * ArmourUnitWeight`
(`HangarArmourPage.cs:52-54`). `HangarEconomy:174-176` separately reads the record's armour as
"units x5" for the star formula, which is the two readings colliding inside one file.
`docs/org/hangar.md` carries both: "units 0-12 per zone … displayed as units×5 … weighed at
units×4", and, in the economy table, "the zone dword is the displayed unit count, so it reduces to
$4 per unit" with a full airframe at 240 units, $960 and 960 lb. Under the second reading a
dropdown row of 5 units costs $20 and 20 lb; CSVM's full airframe is 48 rows, $192 and 192 lb.

**Approach.** Read `FUN_00405680` and the dropdown callback at `0x0040b7bd` **together**, since the
question is what scale the record dword holds and neither function answers it alone. Then correct
whichever side is wrong (the two constants, or `HangarArmourPage`'s store) and **delete the losing
paragraph from `docs/org/hangar.md`** in the same commit; an item that leaves both readings
standing has not landed. `MaxArmourUnits` and the star formula's `x5` are the two other places the
scale is spelled, so whichever reading wins, all four sites say the same thing afterwards.

**Model recommendation.** high. A decode that arbitrates between two written readings, on a value
that reaches mission-side hull points.

**Verify.** `.\RunProbe.ps1` with `--menu=campaign-hangar` on a scratch profile, stepping one
armour row and reading the dollars and pounds off the shot against `OriginalScreenshots/`'s ARMOR
captures. Unit side: `HangarEconomy` tests for a known build's total, plus the vehicle tests the
trap names. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** The mission-side armour per zone (`docs/formats/vehicle.md`'s 240-unit reasoning)
reads the same dword, **so a change here can move hull points.** Re-run the vehicle tests, and if
they move, that is a finding to file, not a number to re-baseline. Do not settle this from the
economy table alone: it is one of the two readings in dispute, not the arbiter. A full airframe at
240 units versus 48 rows is the same aeroplane under two scales, so a "correct-looking" total
proves nothing on its own.

## A2 ☐ `BL-724` The ARMOR screen sets each wing on its own where the original moves both together

**Goal.** Stepping the wing armour on the ARMOR screen moves both wings, as the original's screen
does, and the PURCHASE rows agree with it.

**Evidence (confidence: traced for ours; the original's shape is the author's own recall, which
this project treats as evidence outranking a guess).** **The original keeps a combo box for each
wing, and changing either one changes the other too** (the author, 2026-09-06). That answers the
question the entry left open: the screen has four rows, not three, and the coupling is between the
two rows rather than between one row and two dwords. On our side `CustomPlaneDef` carries
`ArmourLeftWing` and `ArmourRightWing` as independent fields
(`CSVM/src/Flight/CustomPlaneDef.cs:95-98`, re-read 2026-09-06, clamped separately at `:257-258`),
and `HangarArmourPage` maps rows 0-3 to nose, tail, left wing and right wing one for one
(`UnitsOf`/`SetUnits`, `CSVM/src/UI/HangarArmourPage.cs:66-85`), so row 2 and row 3 step alone.
The record's four zone dwords (`docs/org/hangar.md`, "Armour") are consistent with the reported
shape: both boxes are drawn, both dwords are written, and the two are held equal by the screen.

**Approach.** Keep the four rows and couple rows 2 and 3 in `HangarArmourPage.SetUnits`, so
stepping either wing writes both dwords and both combo boxes redraw. The PURCHASE rows read the
same store and follow. The coupling belongs in the page, not in `CustomPlaneDef`. No decode is
owed for the row count; if the ARMOR screen's row handlers are read anyway while `A1` is in the
binary, record what they say, but the shape is settled.

**Model recommendation.** medium. A coupling in one page's setter now that the design question is
answered, on the same file `A1` just moved.

**Verify.** `MenuHangarSuites`: step the left wing row, assert both dwords moved and both boxes
read the same; step the right wing row, assert the same in reverse; load a profile saved with
unequal wings and assert it still opens and still reads back what it stored. A shot of the ARMOR
screen showing four rows with the two wings equal. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** **The record keeps two dwords, so mission-side damage still reads per wing: couple
the UI, not the model.** A saved profile with unequal wings must still load and must not be
"repaired" on load, which means the two boxes can legitimately disagree until one of them is
stepped. Whichever way `A1` settled the scale is the value this row now writes twice, so run `A1`
first or this lands on a number that then moves.

## A3 ☐ `BL-650` The campaign hangar's decoded slot cap is not enforced

**Goal.** A campaign purchase past the profile's slot cap is refused with the original's own
message rather than silently completing, so a wealthy profile cannot grow its inventory past
anything the original would accept.

**Evidence (confidence: traced).** Found while landing the campaign Plane Construction screen
(`git log --grep=BL-634`). The original's profile holds 25 plane records and its free-slot finder
reserves six of them (`FUN_004111f0`), refusing a purchase past that with langui 204
`IDS_PN_TOOMANYPLANES`. `CampaignWallet.Purchase` (`CSVM/src/UI/Menu/CampaignWallet.cs:101-118`,
re-read 2026-09-06) debits `Profile.Funds`, adds or updates the `OwnedPlane` record and saves,
**with no count check anywhere in it**. The two refusals that do exist are in the same file
(the reward-aircraft one, and the two-plane floor at `:69`), so the shape to copy is at hand.

**Approach.** A third gate beside those two, reading `Profile.Planes.Count` against the decoded cap
and composing langui 204. **Read what the reserved six are for before choosing the number**, since
a cap of 25 and a cap of 19 are two different readings of the same decode, and the item is not done
until the plan says which and why.

**Model recommendation.** medium. One gate in the shape of two that already exist; the judgement is
in the cap's value, not the code.

**Verify.** A `CampaignWallet` unit that buys up to the cap and asserts the next purchase is
refused, funds unchanged and no record added. A `MenuOriginalCampaignSuites` walk that reaches the
refusal and shows langui 204. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** **The cap is on RECORDS, not on the build directory, which the two modes share**, so
counting files would let an Instant Action build refuse a campaign purchase. The other half of the
two-modes-over-one-store problem is the export gate, `CustomPlaneDef.AwaitingExport`, which run 12
landed; read what it does before adding a second count of the same directory.

# Wave B — The campaign boards' panes, and the shots that pin them

## B11 ☐ `BL-659` No screenshot aid can open a campaign combo box

**Goal.** A `--menu=` value can move to a row and confirm on it, so the open plane and ammo lists
can be photographed and pinned by a golden without patching a `flow.Accept()` into the walk.

**Evidence (confidence: traced).** `OpenCampaignAid` parses one colon argument and spends it as a
cursor step count, `for (…; i < (int)argument; i++) flow.Move(1)`
(`CSVM/src/UI/LaunchMenu.cs:1866-1871`, re-read 2026-09-06), while `campaign-guestcheck` reads the
same argument as a player number. Neither form can press. The reference images that matter most for
the widget are the open ones (`Campaign Flight Check Change Ammo ComboBox.png`,
`Campaign Flight Check Change Plane Combo Box.png`) and both were photographed by temporarily
patching an `Accept` into the walk. **The precedent for the fix already exists**: the same parser
carves one named word out of the numeric argument, `CampaignAidProfiles.ExportArgument`, and routes
it to `PressExport` (`LaunchMenu.cs:1859-1888`), landed with run 12's `BL-691`.

**Approach.** Generalise that carve-out into an argument form that spells a **short input script**
rather than a step count, a compact string of moves and confirms the walk replays. `PressExport`
becomes one expression in that language rather than a special case beside it. Keep the bare numeric
form working, since existing `--menu=` calls and goldens use it.

**Model recommendation.** medium. A small parser and a replay loop, with an existing special case
to fold in; the design is settled by the precedent.

**Verify.** `.\RunProbe.ps1 --menu=campaign-ammo:<script>` produces a shot with the list open;
the same for `campaign-planeselection`. A unit on the script parser, including the bare-number and
`campaign-guestcheck` forms still meaning what they meant. Confirm no existing golden's command
line changed meaning. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** `campaign-guestcheck` spends its colon on the player number and `campaign-briefing` is
excluded from the step loop entirely, and both are exceptions the new form has to keep, not clean
up. The plane selection and ammo screens' open lists are **unpinned by any golden** until this
exists, so a golden added here is new coverage, and a new golden's hash is never evidence on its
first run: look at the image.

## B12 ☐ `BL-658` The ammo screen shows one description pane where the original fills two

**Goal.** The ammo screen reads a gun's ammunition and a pylon's ordnance side by side, each under
its own heading, as `ORDINANCELAYOUT.SCRIPT` fills them.

**Evidence (confidence: traced).** `[@OrdinanceLayout@]` authors `OL_S_AMMODESC` at 566,96 and
`OL_S_ROCKETDESC` at 566,332, each under its own heading, and the script fills both at once. Our
board draws a single `CampaignBoards.DetailSlot` at 566,92 carrying whichever row the cursor is on
(`CSVM/src/UI/CampaignBoards.cs:325`), so a rocket description appears in the ammunition pane's
position and the lower pane is empty. **Correction to the entry's trap, made 2026-09-06:**
`DetailSlot` returns null for every screen but `Ammo` (`CampaignBoards.cs:432-441`) and reads
`OL_S_AMMODESC` alone, and `OL_S_ROCKETDESC` appears nowhere in `CSVM/src/UI`. The plane selection
screen and the roster therefore do **not** share the slot and cannot regress from a change to it;
the trap as filed is wrong.

**Approach.** The detail slot is keyed by screen, so the composer cannot tell which pane a row
belongs to. `ICampaignPage` needs a member naming the pane; the ammo page then answers with both
texts and the two headings can be drawn. `DetailSlot` grows a second authored box read from
`OL_S_ROCKETDESC`.

**Model recommendation.** medium. One composer seam and one interface member, with the layout data
already authored.

**Verify.** A shot of the ammo screen with a gun row focused and one with a rocket row focused,
both panes filled, against `OriginalScreenshots/`'s ammo captures. A golden pinned on the open
rocket list, which `B11` makes reachable. `MenuOriginalCampaignSuites` for the pane routing. Then
the complete `.\RunTests.ps1`.

**⚠ Traps.** **The open rocket list draws over the lower pane in the original, so the overlay order
is part of the change**, and a correct pane under a wrongly-ordered overlay looks like a broken
pane. Do not carry the filed trap about the plane selection screen and the roster; it was checked
this session and is false, and acting on it would add a pane guard nothing needs. The slot's y is
pinned at the measured 92 where the row says 96 (`CampaignBoards.cs:429-430`); find out whether the
second pane needs the same correction before assuming its authored 332 is right.

# Wave C — Original's dialogs and rows

## C21 ☑ `BL-744` The export notice draws the message box's `?` icon where the original draws `!`

**Landed.** `MESSAGEBOX.SCRIPT`'s `gui_create` (lines 35-50 of
`extracted/rof/ASSETS/SCRIPTS/MESSAGEBOX.SCRIPT`) picks the `mb_p_icon` frame off the raising
screen's button mask in `@globals@OR.UR` and nothing else: `UR & 0x0f` of `0x4` or `0x8` sets frame
0, every other mask sets frame 1, and a set `XR` overrides both with frame 2. `MB_B_ICON.PNG` is
43x126, three 43x42 frames stacked as `?`, `!`, skull, so frame 0 is the query, frame 1 the warning
and frame 2 the skull. The mask and not the button count decides: the `0x2` mask is a two-button
box that draws the warning and `0x8` is a three-button box that draws the query, which is why the
frame arithmetic is nothing like `PlaqueFrame`'s.

A `DialogIcon` enum (`Query`, `Warning`, `Death`, numbered as the sheet stacks them) now rides on
`CampaignModal` and on `OriginalDialog`, and both `CampaignBoards.Dialog` overloads pass it into the
`MB_P_ICON` `BoardPicture`. `CampaignModal`'s parameter defaults to `Warning` because both boxes
`CampaignFlow.RaiseModal` raises are the plane screen's `0x1` masks (langui 710's refusal and langui
702's export notice), so the export notice takes the `!` its reference shot shows without any change
to `CampaignFlow.cs`. `OriginalCampaign.RaiseDialog` takes the icon as a required argument, so each
of the presentation's raises states its frame rather than inheriting a default: the DELETE PLAYER
question (langui 201, `CAMPAIGN.SCRIPT`'s `0x4`) and the sell question (langui 700,
`HANGAR.SCRIPT`'s `0x4`) take `Query`; the flow's refusal band, the ContinuePlayer refusals (langui
200 and 2106's text, both `0x1`), the missing-player line and the refused sale (langui 701, `0x1`)
take `Warning`. Nothing we compose raises the skull, which only `CREDITS.SCRIPT` asks for.

`CSVM.Tests/DialogIconTests.cs` pins the sheet's stacking order, pins that the composer takes its
frame from the caller rather than from the button set it was handed, and walks a modal's icon
through to the picture; the frame each site picks is asserted at its own existing test, in
`CampaignPlaneSelectionPageTests`, `OriginalCampaignTests` and `OriginalHangarTests`. No golden
photographs a dialog, so none needs re-pinning.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The export notice over the plane-selection screen draws the `!` frame its own reference
shot shows, and each other message-box call site draws the frame the original gives it.

**Evidence (confidence: traced).** `OriginalScreenshots/Campaign Flight Check Change Plane Export
dialog.png` shows the `!` frame of `MB_B_Icon.Png`. `CampaignBoards.Dialog` loads the sheet as
`Ui("MB_B_Icon.Png", DialogIconFrames)` with `DialogIconFrames = 3`
(`CSVM/src/UI/CampaignBoards.cs:75-77`, `:370`, re-read 2026-09-06) and **passes no frame index at
all**, so every dialog on both presentations gets the same frame. Original's `ComposeDialog`
(`CSVM/src/UI/Menu/Original/OriginalCampaign.cs:386-400`) composes buttons and hands the message
straight to `CampaignBoards.Dialog`, so it has no frame to choose either. The sheet's own comment
records the three icons as "the warning, and the two the other message classes use".

**Approach.** Decode which icon frame each message-box call site selects, then carry the frame on
the modal (`CampaignModal`) and pass it through both `Dialog` overloads to the `BoardPicture`.
The delete confirm keeps `?`; the export notice takes `!`.

**Model recommendation.** medium. A frame index threaded through two overloads, once the decode
says which value each site takes.

**Verify.** `.\RunProbe.ps1 --menu=campaign-planeselection:<export>` (the existing `PressExport`
route) and compare the icon against the reference shot pixel for pixel. A second shot of the delete
confirm, unchanged. Re-pin any golden that photographs a dialog. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** **Do not key the icon on the button count.** The original's two-button confirms and
one-button notices may each use either frame, so a rule of "one button means `!`" would be right
twice by accident and wrong later. The sheet stacks three icons, not four button states, so the
frame arithmetic is not `PlaqueFrame`'s.

## C22 ☐ `BL-705` The scrapbook's Best to Date and Most Recent tabs cannot be clicked

**Goal.** Every non-scrap row on the scrapbook page answers the pointer, the two tabs first, as the
keyboard already reaches them.

**Evidence (confidence: traced).** Reported at the controls over `PLAN-menu-presentations` E44's
pointer sweep. The tabs are real rows (`RowKind.BestTab`/`MostTab`,
`CSVM/src/UI/CampaignScrapbookPage.cs:64-65`) and the keyboard reaches them, but `OriginalCampaign`'s
hit-box switch answers for the scrapbook only through `book.ScrapOf(row)`
(`CSVM/src/UI/Menu/Original/OriginalCampaign.cs:634`, re-read 2026-09-06, the line having moved
from the filed `:567`), so any row that is not a scrap gets no rectangle and the pointer passes
over it.

**Approach.** Give the scrapbook's non-scrap rows a box, the tabs first. The tab's drawn rectangle
is already known to the page, which centres the unselected tab's label in `TabWidth`
(`CampaignScrapbookPage.cs:34`). Extend the `when` arm rather than adding a case beside it.

**Model recommendation.** medium. One switch arm and a rectangle the page already computes.

**Verify.** `MenuOriginalCampaignSuites`: a pointer press on each tab switches the page, and a
press on every other non-scrap row does what the keyboard does on it. A shot of the scrapbook with
the pointer over a tab. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** **The same switch is what leaves every other non-scrap row on that page unreachable,
so fix the class of row rather than special-casing the two tabs.** Two tabs fixed and the rest
still dead is the same bug with a smaller symptom. The pointer's wheel and thumb reach the campaign
lists through `OriginalShell.Lists`, which is a window rather than a row box and does not answer
for these tabs; do not route the tabs through it. `C21` edits the same file, so do not run these
two in parallel worktrees.

# Wave D — Text entry, seats, and the way out

## D31 ☐ `BL-711` A refused character in a name box makes no sound

**Goal.** Typing a character a name box does not accept produces `ENTERTEXT_ERROR`, because the
box is given the character and refuses it.

**Evidence (confidence: traced).** Reported at the controls over `PLAN-menu-presentations` E44's
sound sweep, which expects `ENTERTEXT` per accepted character and `ENTERTEXT_ERROR` per refused
one. Both cues are wired (`OriginalCues.TextError` to `ENTERTEXT_ERROR.WAV`,
`CSVM/src/Session/MenuCueTable.cs:20`), both edit boxes cue them on the refusing branch
(`CSVM/src/UI/Menu/Original/OriginalCampaign.cs:435`, `CSVM/src/UI/Menu/Original/OriginalHangar.cs:562`),
and the WAV is present in the extraction. What never happens is the refusal:
`MenuInput.BuildTextKeys` (`CSVM/src/UI/MenuInput.cs:334-344`, re-read 2026-09-06) polls A-Z, the
digit row and Space and nothing else, as its own comment says, so a character outside the box's set
never reaches `Typed`. Pressing an unaccepted key produces neither a letter nor a beep.

**Approach.** Let the typed set be wider than the accepted set, so the box does the refusing and
the cue has something to fire on. `TextKeys` (`MenuInput.cs:104`) and the parallel edge-detection
array grow together.

**Model recommendation.** medium. A widened key table on shared input, with the parallel-array
constraint to respect.

**Verify.** The playtest the entry names: the profile name box, one accepted letter, one
punctuation key, and one key past the cap, expecting `ENTERTEXT`, `ENTERTEXT_ERROR` and
`ENTERTEXT_ERROR`. A `MenuInput` unit that a punctuation key reaches `Typed`. A cue-log assertion
in `MenuOriginalCampaignSuites`. Then the complete `.\RunTests.ps1`.

**⚠ Traps.** **The cap overrun is a second, untested route to the same cue.** A valid character
typed into a full box should already refuse and beep, so check that first; if it beeps, this item
is only about the character set and shrinks accordingly (and is the alternate list's first
casualty). Widening the polled keys **touches text entry on both presentations**, and the keys are
edge-detected per key in a fixed-length parallel array, so the array and the table have to grow
together or the detection silently reads the wrong key.

## D32 ☐ `BL-697` Only player 1's keymap can be reached

**Goal.** More than player 1's keymap can be edited on the rebinding screen, with each editable
seat having a device to capture with.

**Evidence (confidence: traced).** `OpenControls` registers one seat per entry of `_slots`
(`CSVM/src/UI/LaunchMenu.cs:2458-2478`, re-read 2026-09-06), and `_slots` mirrors
`PlayerSetupFeature`'s joined seats, which are claimed with the pad Start gesture on the aircraft
pick. The screen is reached from Options, off the main menu, where seat 0 is the only seat, so the
Player stepper offers player 1 alone. Player 2's keymap is written by `BindingStore` and read at
launch by `LaunchBindings`, so **the data path is whole and only the way in is missing**.

**Approach.** Let the screen take the join gesture itself, so a pad pressing Start there claims the
next free player for editing; or reach the screen from where seats already exist. The first is the
smaller change and is what the entry's fix shape names.

**Model recommendation.** high. Raw device reads on shared seat code, where the wrong shape offers
players a screen they cannot use.

**Verify.** `MenuOriginalSuites` and the controls suite: a second pad joining on the rebinding
screen raises player 2 on the stepper, and a capture on that seat writes player 2's `BindingStore`
file and no other. A shot of the screen with two players available. Then the complete
`.\RunTests.ps1`.

**⚠ Traps.** **A seat with no pad is not the same as a seat with a pad that has not joined.**
Capture needs a pad to press and reads the seat's own pad list, so registering four seats up front
would offer three players nothing to capture with. **Which pad a press came from is a raw device
read, not an action** (run 12's `B12` left nine deliberately raw sites, and it is why `MenuJoin` is
unbound), so the join here cannot be resolved through the seat's own bindings. `BL-696`, the
missing door from Original, is deliberately not in this run, so do not solve it on the way past;
`E41` answers it.

## D33 ☑ `BL-710` Quitting flashes Godot's default sky before the window closes

**Landed.** One `Launcher.BlankAndQuit` now serves the three quits reached from a frame that is
still drawing: Esc in the viewer (`Launcher.cs:783`), the menu's `QuitExit` sink (`:1374`) and the
boards' `ExitSession` (`:1520`). It sets the process-lifetime `WorldEnvironment`'s background to
flat black and then ends the frame, so the image held on screen through shutdown is black rather
than the engine's sky. Every other exit keeps its bare `GetTree().Quit()`.

**Which teardown uncovers it.** `MenuHost.Exit` (`CSVM/src/UI/Menu/MenuHost.cs:150-156`) sets
`Shown = false`, calls `Active?.Hide()` and only then hands the exit to the launcher's sink, so the
active presentation's opaque layer is already invisible when `case QuitExit` runs. Both
presentations hide a full-screen opaque layer: Built-in's `LaunchMenu` is a `CanvasLayer` whose
backdrop `ColorRect` carries the comment "Fully opaque backdrop so the empty 3D scene (procedural
sky) never shows through" (`CSVM/src/UI/LaunchMenu.cs:457`), and Original's `ComposedBoardView`
fills its rect with black (`:83`). Behind them, `Launcher.SetupLighting` (`:1094-1120`) keeps a
`WorldEnvironment` whose sky is a `ProceduralSkyMaterial`, and at the menu no session is in the
tree, so what the hidden layer uncovers is that sky and nothing else. A probe screenshot of the
persistent scene is the artefact itself: grey-blue above a hard horizon, brown below.

**Why the other exits are not it.** The two remaining drawing exits cannot produce the report:
`ExitSession` quits with the live session still in the tree, so the world and its own sky dome
draw, and Esc in the viewer quits on a frame that was already showing the procedural sky, so
nothing appears that was not there. The sixteen probe exits render nothing a player sees, which
`GameSession.cs:592` states for the seven inside the session build. `Launcher.cs` and
`GameSession.cs` hold nineteen `GetTree().Quit()` calls between them rather than the twenty the
entry counted, and nothing else in either file calls it.

**Cost to headless runs.** None. The three changed sites are unreachable from any probe or suite,
`BlankAndQuit` waits for nothing, and the nine probe exits in `Launcher.cs` plus all seven in
`GameSession.cs` are untouched, so no wall-time comparison against
`analysis/verification-budgets.json` is owed.

**What still needs eyes.** No headless capture can photograph the frame after `Quit()`, since it
is drawn by the engine after the last managed callback has run. At the controls, quit from the
menu in both presentations and watch the last instant before the window goes: it should be black,
and never the grey-blue-over-brown gradient above.

**Original approach (kept for reference).**

**Goal.** Quitting the game shows the game until the window is gone.

**Evidence (confidence: traced for the mechanism, lead-only for which teardown uncovers it).**
Reported at the controls over E44's pointer sweep, "Exiting the game shortly shows the godot
skybox before closing the window". `GetTree().Quit()` ends the frame rather than the process, so
anything freed on the way out leaves the engine's own environment drawing for the frames that
remain. The call has **twenty sites** across `CSVM/src/Session/Launcher.cs` and
`CSVM/src/Session/GameSession.cs` (counted 2026-09-06), most of them probe exits that never draw.

**Approach.** Confirm which teardown uncovers the environment, then either paint over the exit or
free nothing until the window is gone. Whichever is chosen, it applies to the drawing exits alone.

**Model recommendation.** medium. A small change, but only after the right one of twenty sites is
identified.

**Verify.** A capture of the final frames of an ordinary quit from the menu, showing no default
sky. Confirm the headless probe and suite exits are untouched: compare `RunTests.ps1`'s total wall
time against `analysis/verification-budgets.json` before and after. Then the complete
`.\RunTests.ps1`.

**⚠ Traps.** **Confirm which teardown uncovers it before reordering anything.** There are twenty
`Quit()` sites and most of them are probes that never draw, so a fix aimed at the wrong one changes
nothing visible and is invisible in tests. **A fix that hides the flash by delaying the quit would
slow every headless probe and every suite that ends in one**, which is a real cost measured in the
verification budgets, not a theoretical one.

**Verified.** <pending orchestrator run>

# Wave E — At the controls

## E41 ☐ Closing sortie: every landed item judged at the controls, and `BL-706` and `BL-696` answered

**Goal.** The author flies and clicks the run's ten landed items, and answers the two this run
dropped because they need eyes or a decision.

**Evidence (confidence: n/a, since this item is the judgement rather than a claim).**

**Approach.** One sitting over the menu, in the order the waves landed. Per item, the acceptance is
its own **Verify** line's at-the-controls half. Then the two dropped items:

- **`BL-706`** (the PLANE NAME dialog sits off-centre). Compare the drawn pane against the
  original's own PLANE NAME screen **before moving anything**. The authored coordinates are
  decoded, so an off-centre dialog more likely means the board fit places a sub-screen pane wrong
  than that the data is wrong; centring it by hand would hide that. The answer wanted here is
  "which of the two", not a nudged constant.
- **`BL-696`** (the Original presentation has no way into the rebinding screen). The decision
  wanted is whether Original gets a door at all, and if so where. The original had none, so a door
  is a deliberate divergence like Built-in's, not a fidelity fix.

**Model recommendation.** n/a, since the author flies it.

**Verify.** The sortie itself. Findings are filed as new `BL-` entries with `New-ItemId.ps1`, not
fixed here.

**⚠ Traps.** **A menu item that looks right in a headless shot can still be wrong under a
pointer.** Run 12's whole item list came from exactly that gap, so click every fixed row rather
than reading the shots again. `BL-723`'s armour scale is the one item here whose acceptance is a
number rather than a look: read the dollars and pounds against the original's screen, not against
what the code now says.
