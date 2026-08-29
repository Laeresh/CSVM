# Campaign screens

Part of the [format documentation](README.md). This page is the behavioural decode of the five GUI
scripts that drive the out-of-mission campaign flow: the player profile screen
(`CAMPAIGN.SCRIPT`), the cabin hub (`PASSENGERCABIN.SCRIPT`), the chapter-intro movie player
(`CAMPAIGNINTRO.SCRIPT`), the flight check screen (`FLIGHTCHECK.SCRIPT`) and the ammo selection
screen (`ORDINANCELAYOUT.SCRIPT`), and of the three that drive the scrapbook (`SCRAPBOOK.SCRIPT`,
`SCRAPBOOKZOOM.SCRIPT` and `SCRAPBOOK_TOC.SCRIPT`). It documents which widget triggers what, how
each list is filled, which engine callback each screen makes, and where every screen transition
goes.

The sources are the scripts in `extracted\rof\ASSETS\SCRIPTS\`, the widget table
`extracted\rof\ASSETS\LAYOUT.CSV`, the `langui` string table
([strings.md](strings.md)), and the `crimson.exe` callback handlers.
[rof.md](rof.md) holds the archive container and the script language's shape;
[instant-action.md](instant-action.md) is the sibling decode of the same machinery for
`INSTANTACTION.SCRIPT` and was the method precedent here.

The briefing screen is not a script: it is a `zrdr` dialog with its own reveal engine, decoded in
[briefing.md](briefing.md). The profile file this flow reads and writes is decoded in
[saved-games.md](saved-games.md), and the mission's objective text comes from the per-mission
choreography in [objectives.md](objectives.md).

## Contents

- [Conceptual model](#conceptual-model)
- [Screen flow](#screen-flow)
- [Player profile: `CAMPAIGN.SCRIPT`](#player-profile-campaignscript)
- [The cabin: `PASSENGERCABIN.SCRIPT`](#the-cabin-passengercabinscript)
- [Chapter intro: `CAMPAIGNINTRO.SCRIPT`](#chapter-intro-campaignintroscript)
- [Flight check: `FLIGHTCHECK.SCRIPT`](#flight-check-flightcheckscript)
- [Ammo selection: `ORDINANCELAYOUT.SCRIPT`](#ammo-selection-ordinancelayoutscript)
- [The scrapbook: `SCRAPBOOK.SCRIPT`, `SCRAPBOOKZOOM.SCRIPT`, `SCRAPBOOK_TOC.SCRIPT`](#the-scrapbook-scrapbookscript-scrapbookzoomscript-scrapbook_tocscript)
  - [Entry, exit and the table of contents](#entry-exit-and-the-table-of-contents)
- [Callback reference](#callback-reference)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence and limits](#evidence-and-limits)

## Conceptual model

A screen is three files acting together.

- **`LAYOUT.CSV`** declares each widget by key: its type letter, its art, its position, its
  `IDS_*` string id, and, for a button, the **script it launches** (the `ScriptToExe` column). A
  large part of the navigation is therefore data, not code: pressing `PC_B_RETURNMM` runs
  `MainMenu` because that name sits in its layout row, and the script's own mailbox never mentions
  it.
- **The `.SCRIPT` file** creates one object per widget key, binds engine callbacks to the lists,
  and handles the messages the widgets send back. Identifiers are obfuscated (single letters,
  `$$X$$` placeholders); widget keys, callback ids and message ids are plaintext, and they carry
  the meaning.
- **`crimson.exe`** answers the callbacks. `FUN_004075d0` registers five named handlers:
  `gosCallback` (`FUN_00407670`), `uiData` (`0x004093a0`), `uiControl` (`FUN_00404960`),
  `gosPageInit` (`0x00403680`) and `gosLocalize` (`FUN_00404630`). The obfuscated first argument
  of `callback(...)` is one of those names, so a script's `callback($$E$$, 2010, -1)` and another's
  `callback($$UB$$, 2141, 0)` are calls into two different handlers. The three these screens use
  are `uiData` (the 2000-range data ids), `gosCallback` (small ids, the campaign verbs) and
  `uiControl` (registry and validation).

**Widget classes.** The script's `@ctl@XX` class names map one to one onto `LAYOUT.CSV`'s type
letters. Established by pairing every widget these five scripts create against its layout row:

| Script class | Layout type | Widget |
|---|---|---|
| `ZJ` | `P` | pane (art, optionally multi-frame) |
| `BE` | `B` | button |
| `PE` | `T` | static text |
| `SJ` | `A` | text list (fixed rows, no scrollbar) |
| `PM` | `D` | dropdown |
| `EN` | `L` | listbox |
| `JN` | `S` | scrolling text |
| `IM` | `E` | edit box |
| `AL` | `M` | movie |
| `SK` | `W` | sound |

**Widget properties** used below: `YC` is the layout key, `AK` an art path override, `CK` the
frame index of a multi-frame pane, `TJ` the `uiData` id a list fills itself from, `WM` the shared
list sub-script, `QG` the selected index, `RM` the rollover index, `UJ` the item count, `BC` the
text buffer, `HD` the text colour. `@globals@AR` is set immediately before a list is created and
restored to 32 after: it is the item-array capacity handed to the shared list script (5 for the
ammo dropdowns, 12 for the rocket dropdowns, 8 for the flight-check lists, 25 for the 24-mission
dropdown), and it is not the visible-row count, which comes from `LAYOUT.CSV`.

**Message ids.** The widget classes in `CTL.SCRIPT` define the vocabulary these screens speak:

| Message | Meaning |
|---|---|
| `10000` / `10018` | disable / enable a widget |
| `10001` / `10002` | pointer left / pointer over |
| `10003` | clicked (the widget plays `mouseclick.wav` itself) |
| `10013` | rollover row changed in a list |
| `10015` | selection changed in a list |
| `10021` | validate an edit box's text (the box calls `uiControl` 3107) |
| `10026` | a list row was double-clicked |
| `11003` / `11004` | pause / resume a sound object (`uiControl` 2503 / 2504) |
| `11006` | a sound or movie object reached its end |

**Music.** `GLOBALS.SCRIPT` creates one sound object, `@globals@RR`, whose wav is
`music_splash.wav` at loop count 5. Every out-of-mission screen drives that one object: the cabin
and the flight check mail it `11004` on entry, the cabin mails `11003` when it is paused and again
when Next Mission is pressed, and the scrapbook screens mail `11003`. No script anywhere assigns a
second track to it, so in the shipped scripts the profile screen, the cabin and the flight check
all play the splash music and nothing else.

## Screen flow

Every transition below is stated by either the button's `ScriptToExe` layout column or the
script's own `script_run` / `script_end` / `mail`, both of which are quoted per row.

| From | Control | Goes to | Stated by |
|---|---|---|---|
| Main menu | (campaign) | `CAMPAIGN.SCRIPT` | outside these five scripts |
| Profile | `CM_B_START`, or Enter in the name box, or a double-click on a roster row | the cabin: `script_end mainmenu`, `script_end campaign`, `script_run passengercabin.script` | script |
| Profile | `CM_B_CANCEL` | `activate(@mainmenu@)`, `script_end campaign.script` | script |
| Profile | `CM_B_DELETEPLAYER` | a confirm messagebox, then stays on the profile screen | script |
| Cabin | `PC_B_NEWMISSION` | the briefing: `script_end passengercabin`, `gosCallback` 6 | script |
| Cabin | `PC_B_PREVIOUS` | `ScrapBook_TOC` | layout |
| Cabin | `PC_B_PLANEX` | `PlaneName` (the plane-construction chain's entry) | layout |
| Cabin | `PC_B_RETURNMM` | `MainMenu` | layout |
| Cabin | `PC_B_CHANGEMOMENTO` | `MomentoSelection` | layout |
| Cabin | `PC_B_SAVE` | `save.script`, but the script deactivates the button at creation | script |
| Cabin (on entry) | chapter not yet started | `script_run campaignintro.script`, `script_pause passengercabin` | script |
| Chapter intro | Esc, Space, Enter or a click | `script_continue passengercabin`, `script_end campaignintro` | script |
| Briefing | GO TO FLIGHT CHECK | `FLIGHTCHECK.SCRIPT` | briefing dialog, outside these scripts |
| Flight check | `FC_B_RETURNBRIEF` | the briefing: `script_end flightcheck`, `gosCallback` 6 | script |
| Flight check | `FC_B_FLYMISSION` | the mission: `gosCallback` 1, `script_end flightcheck.script` | script |
| Flight check | `FC_B_CHANGEPLANE` / `..._CHANGEPLANEW` | `PlaneSelection`, with `@globals@ZQ` set to -1 (pilot) or -2 (wingman) | layout + script |
| Flight check | `FC_B_CHANGEAMMO` / `..._CHANGEAMMOW` | `OrdinanceLayout`, same `ZQ` convention | layout + script |
| Ammo | `OL_B_ACCEPT` / `OL_B_CANCEL` | `FlightCheck` | layout |

⚠ **The buttons that carry a `ScriptToExe` name do not appear in their script's mailbox at all.**
`PASSENGERCABIN`'s handler names only Next Mission, Plane Construction (to clear two script
globals), Save and the memento frame; Previous Missions and Return to Main Menu are pure layout
rows. A reimplementation that reads only the scripts will find those two buttons inert.

## Player profile: `CAMPAIGN.SCRIPT`

Widgets: `cm_background` (pane), `cm_e_name` (edit box), `cm_l_players` (listbox),
`cm_b_start`, `cm_b_deleteplayer`, `cm_b_cancel`. The listbox's rows are drawn by the script's own
sub-script `VB`, not by a stock class: it fills the dark red selection bar (`0xff800000`) behind
`parent.QG` and a bright red frame (`0xffff0000`) around the row under the pointer.

**Filling the roster.** `gui_init` calls `gosCallback` 16 (zero the 792-byte roster block, then
enumerate the profile directories), then `uiData` 2099 with the current name to get the selection
index, then re-initializes the list. The list rows come from `uiData` 2102 through the list's `TJ`:
row -1 returns the roster count, any other row returns that profile's name. The roster is
**24 slots of 33 bytes** (a 32-character name plus its NUL) in the executable, which is the
capacity a reimplementation inherits, not an arbitrary limit.

**The name box.** `cm_e_name` is created with `MB.ED = 212`, `MB.KM = 707`, `MB.LM = 1`, its
`JM` (default button) pointed at `cm_b_start`, and is pre-filled from `uiData` 2101 with the last
used name. Message `10021` validates it through `uiControl` 3107, and that validator is strict
about the filesystem, because a profile name becomes a directory name:
`FUN_00406b60` rejects a fixed reserved string, rejects any of `" * / : < > ? \ |`, and then
actually creates and removes a directory of that name under `%TEMP%\RCSA\` to prove the name is
usable. A failed validation raises a messagebox and leaves the screen where it was.

**Starting.** `mail(11004, this)` is the screen's own commit path, reached from the START button,
from Enter in the name box, and from a `10026` double-click on a roster row. It calls `uiData` 2100
(store the typed name as the current profile, returning non-zero when the name changed) and, on a
change, `gosCallback` 9, which resets the in-memory profile (`FUN_004113b0`) and loads the named
one. Then it ends `mainmenu.script` and `campaign.script`, writes the name into the registry under
`UIPlayerName` (`uiControl` 2141 case 0) and runs `passengercabin.script`.

**Deleting.** DELETE PLAYER sets `@globals@OR.VR = 3`, checks the name exists with `uiData` 2099,
raises messagebox `DI = 201` with button mask `0x4`, and on the confirmed result calls `uiData`
2100 and `gosCallback` 17 (`FUN_0041a790`), then clears the name box. `@globals@OR` is the shared
messagebox parameter block declared in `GLOBALS.SCRIPT`: `DI` is the langui id of the prompt, `UR`
the button mask, `WR` an input flag, `VR` the result the box writes back, `TH` a string slot.

⚠ **Two branches of this script are unreachable as shipped.** `int TB = 0` guards the
load-a-savegame branch (`script_run load.script`), so it never runs and the following
`if (24 > IB)` roster-full test always takes the true arm; and the literal name `crashcheat!` sets
a script global and forces the name back. Do not reproduce either as behaviour.

## The cabin: `PASSENGERCABIN.SCRIPT`

**The art is flat 2D from the UI archive, not a rendered set.** The screen is
`PC_BackGround.png` at `0,0` with `AlphaType 2` (colour key) at z 4, and `pc_plane` at `46,69`
at z 1 **behind** it: the background painting has a colour-keyed hole where the hangar window is,
and the plane photo shows through it. The photo is chosen at runtime,
`"assets\graphics\pc_p_hangar" + <airframe> + ".jpg"`, where `<airframe>` is `uiData` 2010 for the
pilot slot, so eleven photographs `PC_P_HANGAR0.JPG` .. `PC_P_HANGAR10.JPG` ship, one per airframe
in the `IDS_IA_PLANES` order. `PC_PlaneTemp.png` is only the layout row's placeholder. The rest of
the scene (desk, map, radio, blueprints) is painted into the background.

Two more art layers sit on the desk. `pc_memento` is a pane whose `AK` is
`"assets\graphics\scrapbook\" + <name>`, the name coming from `uiData` 2150, which reads the
profile's current memento file name (the `UIData +0x344` field of
[saved-games.md](saved-games.md)); `pc_frame` is `PC_Mementopicframe.png` drawn over it, and
clicking the frame forwards a `10003` to the CHANGE MEMENTO button. `pc_pin0` .. `pc_pin4` are five
panes sharing `PC_mappins.png` at five fixed map positions, each taking frame index `R`; the script
creates `callback($$A$$, 7, -2)` of them, which is the **story chapter number** of the campaign's
current position (see the callback reference), so the map shows one pin per chapter reached.

**Buttons.** NEXT MISSION, PREVIOUS MISSIONS, PLANE CONSTRUCTION, RETURN TO MAIN MENU, CHANGE
MEMENTO and SAVE GAME are created; SAVE GAME is deactivated immediately, which is why the reference
screenshot shows five. NEXT MISSION is disabled (`mail(10000, ...)`) when `uiData` 2600 returns 0,
which happens only when both the current mission and the completed count have passed 24, that is
when the campaign is finished.

**Next Mission** sets the mission and leaves: normally `uiData` 2104 with -2 (advance to
`completed + 1`, clamped to 24), then `mail(11003, @globals@RR)` to stop the music,
`script_end @passengercabin@`, and `gosCallback` 6 to open the briefing for the selected mission.

**The mission cheat.** The script's own click region is `5,336 to 85,479`; clicking there gives the
script keyboard focus, and typing `idaho` activates the otherwise-deactivated dropdown
`pc_d_missions` (24 rows, filled from `uiData` 2038 with langui `3450 + row`, the long mission
names). With the dropdown used, Next Mission passes `QG + 1` to 2104 instead of -2. The dropdown is
pre-selected to the campaign position, with 24 clamped to row 23.

**Chapter intro.** `@globals@XQ` is set by `CAMPAIGN.SCRIPT` and means "the cabin was entered from
the profile screen". Only then does the cabin ask `uiData` 2151 for a chapter number and, if it is
non-zero, run `campaignintro.script` and pause itself. 2151 returns the chapter number only when
that chapter's first mission has no recorded time in either half of its mission-result record, so
the movie plays once per chapter, on first arrival. When `XQ` is clear the cabin just resumes the
music. Either way the script ends with `gosCallback` 31, which joins the background loader thread
and tears the mission world down.

## Chapter intro: `CAMPAIGNINTRO.SCRIPT`

The whole screen is one movie widget: `cm_movie` with `BL = "chap" + <n> + ".mpg"`, sized to
`getresx()` by `getresy()` and placed one z above the movie's own layer. Escape, Space, Enter or a
left click mails `11006` to itself, and the handler (guarded by a one-shot flag so the end-of-movie
`11006` and a keypress cannot both fire) continues `passengercabin` and ends itself. The chapter
number comes from the same `uiData` 2151 call the cabin makes.

This screen is the M5 plan's out-of-scope MPG playback in its entirety: five files, one skip
gesture, no other behaviour.

## Flight check: `FLIGHTCHECK.SCRIPT`

Widgets, all prefixed `fc_`: `background`, `p_pilotplane` and `p_wingplane` (panes over
`FC_PlaneIcons.png`, frame index = the slot's airframe from `uiData` 2010), `t_title`, `t_mission`,
`t_pilot` / `t_wingman`, `t_pilotplane` / `t_wingmanplane`, `t_objtitle`, `t_objectives`, the four
`t_gunlistp/w` and `t_rocketlistp/w` text lists, and the six buttons.

**The two slots.** `-1` is the pilot and `-2` the wingman throughout the campaign callbacks. On
creation the script calls `uiData` 2014 with `(-1, -3, 0)` and `(-2, -3, 0)`, which resets each
slot's plane override to "use the profile's own selection": `FUN_0040fac0` resolves a slot to its
override, and the sentinel -3 falls through to the profile fields, `UIData +0x33c` for the pilot
and `+0x340` for the wingman.

**Text.** The mission title is `uiData` 2009 with flags 1, which is langui `3450 + mission - 1`,
the long mission name (flags 2 would give the short name at `3480 + mission - 1`). Each slot's
plane line is `uiData` 2011, the saved plane's name followed by its airframe title, which is why
the reference screenshot reads `Gypsy Magic  Hughes P21-J MKIII Devastator` (langui 511 and 512
are the default pilot and wingman plane names a new profile is seeded with). The objectives note is
`uiData` 2022, which asks `gosCallback` 28 for the mission's objective lines and concatenates them;
those lines are the localized `MSG_BRF_*` texts the mission's own `objectives.zrd` `IDENTITY`
blocks name (see [objectives.md](objectives.md)).

**The gun and rocket lists are eight rows each, and the blanks are data.** Both are filled per row
through `TJ` (`2005` and `2006` for the pilot, `2007` and `2008` for the wingman; the pilot and
wingman ids share one handler apiece, so only the slot differs). Row -1 returns the count 8.

- A gun row `r` addresses **gun group `r >> 1`** of the plane record. The group is empty when its
  gun-slot id is 5, and the odd row of a group is drawn only when bit `r >> 1` of record `+0x84` is
  set, so a group holds one or two barrels. The row text is the calibre name plus the group's
  ammunition short name (langui `3360 + ammo`).
- A rocket row `r` addresses **pylon cell `r`** of the record and is empty when the cell holds 11.
  Cells 0 to 3 are one wing and 4 to 7 the other.

**The calibre name is `IDS_GUNSHORTNAME` (langui `3320 + slot`), not the long name the hangar and
the ammo screen use.** `Campaign Flight Check.png` settles it: the row reads `1)  .50-cal. Slug`,
not `1) Barret Arms .50-cal. Slug`, and the 3320 block's strings each lead with a space, which is
the gap the screenshot draws between the row number and the calibre. Nothing is inserted between a
row's number and its text on either list, which is why the rocket rows read `1)High explosive`
tight against their number: `IDS_ROCKETSHORTNAME` carries no leading space of its own.

**The wingman half exists only when the mission has a wingman.** `callback($$A$$, 27)` is
`FUN_0041aa50`, a byte read out of the loaded campaign sequence at stride 0x34, which is
`cm_sequence.zrd`'s own per-entry `wingman` flag ([saved-games.md](saved-games.md)). When it is
clear the script deactivates the wingman's two buttons and never creates the wingman panes, texts
or lists at all.

**Plane change.** Two independent rules disable it, and both are in the shipped build.

- `switch ($$KP$$) case 13: case 17:` calls `uiData` 2021 and deactivates `fc_b_changeplane`.
  `$$KP$$` is the campaign's current mission, 1-based over the 24-entry `cm_sequence`.
- `uiData` 2018 returns the count of owned planes (`FUN_0040fd10` over the 26-slot plane array),
  **minus one when the current mission is 13 or 17**, its own test of `UIData +0x00`. The script
  disables both CHANGE PLANE buttons when that count is below 3.

So the answer to "which missions allow a plane change" is: **all of them except campaign missions
13 and 17**, and additionally none of them while the profile owns fewer than three planes. Mission
13 is `cm_sequence` seq 12, Hollywood mission 3, langui 3462 "Nathan Zachary & The Nefarious Trap";
mission 17 is seq 16, Colorado mission 2, langui 3466 "Nathan Zachary & The Pirate's Duel".

**What 2021 does there is grant the mission's story aircraft.** `FUN_00405f00` walks a table of
20-byte records at `0x0061ae80` for the entry whose first field is the current mission, and its
fourth field is an airframe id (11 meaning none). If the entry's objective field is 0, or that
objective bit is already set in the mission's result record, and the airframe has not been granted
before, it copies the stock airframe record into a new plane slot, names it from langui, and makes
it the selected plane; if it was granted before, it just selects the existing one. The shipped
entries:

| Mission | Objective gate | Payout | Airframe | Plane name |
|---|---|---|---|---|
| 1 | 12 | 900 | none | |
| 2 | 1 | 0 | Balmoral | langui 513 `Jumping Jane` |
| 5 | 1 | 20000 | none | |
| 6 | 3 | 5000 | none | |
| 7 | 1 | 0 | Bloodhawk | langui 514 `Blue Streak` |
| 12 | 1 | 10000 | none | |
| 13 | 0 | 0 | Fury | langui 515 `Red Hot Spender` |
| 17 | 0 | 0 | Autogyro | langui 516 `Minx` |
| 19 | 1 | 5000 | Warhawk | langui 517 `Accipiter Annie` |
| 24 | 1 | 100000 | none | |

Only the two entries with objective gate 0 are unconditional, and those are exactly the two
missions the screen calls 2021 on and forbids a plane change on. The other three aircraft are
awarded on completing a numbered objective, through the mission-completion recorder that reads the
same table for its payouts ([saved-games.md](saved-games.md)).

**FLY MISSION** calls `gosCallback` 1 with argument 1 and ends the script. That op takes one of two
paths on `mission - 1`: `FUN_00417090` when the mission has not been completed before
(`completed < current`), `FUN_00417410` otherwise. Both end in the same builder, which reads the
selected plane record for the pilot and, when the mission has a wingman, the wingman's record, and
registers them under the names `player` and `wingman_1`, the same two section names a
`Persist.NNN` file carries.

## Ammo selection: `ORDINANCELAYOUT.SCRIPT`

The screen serves two callers. `@globals@XQ` is read once into a local (`DKA`) and cleared: 0 means
the campaign flight check opened it, non-zero means Instant Action did. `@globals@ZQ` carries the
slot (-1 pilot, -2 wingman). `uiData` 2035 then copies the addressed plane record into a working
copy, `uiData` 2034 copies the working copy back on ACCEPT, and the `DKA` flag decides which record
is the destination: the profile's plane on the campaign path, a scratch record on the Instant
Action path. CANCEL simply leaves, and on the Instant Action path additionally continues
`instantaction`. On the campaign path ACCEPT also calls `gosCallback` 12 with 0, which writes the
profile out.

**Widgets.** `ol_background`, `ol_p_planetopicon` and `ol_p_planefrticon` (both taking the
airframe index from `uiData` 2010 as their frame), four `ol_t_gunname<N>` labels, four
`ol_d_ammo<N>` dropdowns (5 rows), eight `ol_d_rockets<N>` dropdowns (12 rows), two scrolling text
panes `ol_s_ammodesc` and `ol_s_rocketdesc`, `ol_t_planeinfo` (`uiData` 2011), ACCEPT and CANCEL.

**Ammunition is per gun group, not per barrel.** `uiData` 2030 returns group `N`'s gun-slot id and
writes the group's calibre label and its current ammunition index. A group whose id is 5 has no gun:
the script greys its label to `0xff808080` and deactivates its dropdown, which is the greyed
fourth row visible in the reference screenshot. `uiData` 2036 writes a new pick into the working
copy.

**Ordnance is per pylon, and non-existent pylons are deactivated.** `uiData` 2031 returns pylon
`N`'s current ordnance, or -1 when `N` is past that wing's hardpoint count (cells 0 to 3 are
checked against record `+0x34`, cells 4 to 7 against `+0x38` plus four), and the script deactivates
the dropdown on -1. `uiData` 2037 writes the pick.

**⚠ A rocket dropdown's row index is not the ordnance id.** The rocket list is a table of twelve
8-byte records at `0x00619efc`; an entry is offered only when its first field is at most the
current mission number, so the list grows as the campaign progresses, and the row index is a
position in the filtered list. `FUN_0040ff50` maps a row to the table index and
`FUN_0040ffa0` maps back; the ammunition list has no such filter and its row index is the
ammunition index directly. Three executable flags (`0x00647b5c`, `0x00647b68`, `0x00647b6c`, which
this decode did not identify) bypass the filter and offer all twelve; the Instant Action and
multiplayer paths are the likely users.

| Row | Ordnance | Available from mission |
|---|---|---|
| 0 | Armor-piercing rockets | 1 |
| 1 | High-explosive rockets | 1 |
| 2 | Flak rockets | 2 |
| 3 | Sonic rockets | 8 |
| 4 | Flash rockets | 7 |
| 5 | Rear flash rockets | 12 |
| 6 | Smoke screen | 7 |
| 7 | Choker rockets | 7 |
| 8 | Beeper rockets | 17 |
| 9 | Seeker rockets | 17 |
| 10 | Aerial torpedoes | 20 |
| 11 | None | 1 |

The second dword of each record (4, 4, 2, 2, 2, 2, 2, 4, 4, 1, 0, 0 in row order) is read by
nothing this decode found; it has no cross-reference in the executable.

**The table index is what a plane record stores**, and
[saved-games.md](saved-games.md#where-the-ammunition-and-ordnance-picks-live) carries the mapping
from it to the `wep_*` the pylon actually fires.

**The description strings.** Both description panes are two langui strings, a title and a body,
concatenated with a separator. All four blocks are contiguous and in list order:

| Block | Base id | Symbol |
|---|---|---|
| Ammunition list rows | 3360 | `IDS_AMMOSHORTNAME` (Slug, Dum-dum, Armor-piercing, Explosive, None) |
| Ammunition description title | 3350 | `IDS_AMMOLONGNAME` |
| Ammunition description body | 3370 | `IDS_AMMODESCRIPTION` |
| Rocket list rows | 3395 | `IDS_ROCKETSHORTNAME` |
| Rocket description title | 3380 | `IDS_ROCKETLONGNAME` |
| Rocket description body | 3410 | `IDS_ROCKETDESCRIPTION` |

The index into each block is the ammunition index (0 to 4) or the rocket **table** index (0 to 11),
never the dropdown row. `IDS_AMMOABBRNAME` at 3365 is a third ammunition spelling this screen does
not use. The description panes update on `10015` (a pick), on `10013` (the pointer moving over a
row of an open dropdown) and on `10001` (the pointer leaving, which restores the current pick), so
the panel previews what the pointer is over.

## The scrapbook: `SCRAPBOOK.SCRIPT`, `SCRAPBOOKZOOM.SCRIPT`, `SCRAPBOOK_TOC.SCRIPT`

The scrapbook is the two-page book a finished campaign mission ends on. Three scripts share it:
`SCRAPBOOK.SCRIPT` draws one spread, `SCRAPBOOKZOOM.SCRIPT` is the detail view of a single scrap,
and `SCRAPBOOK_TOC.SCRIPT` is the 25-row mission list behind VIEW ALL MISSIONS. The three hand off
by pausing rather than ending, so a spread survives a trip into a scrap or the table of contents
and back. **The cabin's PREVIOUS MISSIONS button does not open the book**: `PC_B_PREVIOUS`'s
`ScriptToExe` is `ScrapBook_TOC`, so it opens the table of contents, and the book is reached only
by picking a row there or by a mission ending. The mission-end pass that fills the results record
is [`org/debrief.md`](../org/debrief.md); the record itself is [saved-games.md](saved-games.md),
"The mission-result array".

**None of the composition is in the scripts.** `SCRAPBOOK.SCRIPT` creates 32 empty pane slots, 32
empty text slots and 11 kill-stamp slots, then asks the engine, item by item, what to put in them.
What each page holds is a fourth data file, `extracted\rof\ASSETS\SCRAPBOOK.CSV`, and the widget
geometry is `LAYOUT.CSV` as for every other screen.

### `SCRAPBOOK.CSV`

One `[SCRAPBOOK]` section of 461 rows, keyed `<mission>_<spread>_<item>` and read by
`FUN_004061d0(mission, spread, item, existsOnly, forZoom)`, which looks the key up and splits the
value on commas into engine globals the script then reads back. The file carries its own column
header as a comment, and the handler agrees with it field for field:

| # | Column | Meaning |
|---|---|---|
| 0 | `Objective` | visibility gate, below |
| 1 | `ResourceID` | langui id of the zoom **caption** |
| 2 | `ImageName` | art file, without extension |
| 3 | `ImageType` | two letters: page extension, then zoom extension |
| 4, 5 | `X`, `Y` | position of the scrap on the 800×600 spread |
| 6 | `Alpha` | alpha type for an image; for a text item, an `%x` ARGB colour |
| 7, 8 | `Width`, `Height` | unused in the shipped rows, every one is 0 |
| 9 | `DrawOrder` | z, written back one higher, and 1000 higher again for a text item |
| 10 | `"Left,Top,Right,Bottom"` | the clickable region, one quoted field |
| 11 | `Zoom` | letter `A` to `Z` selecting a zoom layout family, or `0` for no zoom |
| 12, 13 | `ZoomX`, `ZoomY` | where the inset image sits in the zoom view |
| 14, 15 | `TitleResID`, `TextResID` | langui ids of the zoom title and body |

Item numbers are contiguous from 1 in all 47 populated spreads, which the enumerator requires: it
walks upwards and stops at the first missing key.

**Mission slots and spreads.** Slots run 0 to 24, slot 0 being the not-yet-started career. Spreads
are numbered from 1, and **a mission has one, two or three of them**, not always two: 8 missions
stop at one spread (3, 4, 5, 6, 10, 14, 15, 20), 10 have two, and 6 have three (16, 17, 19, 21, 23,
24). Spread 1 carries 2 to 10 items, spread 2 carries 5 to 24, spread 3 carries 12 to 24. Nothing
declares a count anywhere: `uiData` 2402's next-page helper `FUN_00406170` probes item 1 of the
following spread and, when the key is absent, rolls to spread 1 of the next mission, so the book's
extent is exactly the file's extent.

**Spread 1 is the results page.** The script activates the stat card, the two tabs, the kill stamps
and the results rows only when `uiData` 2403, the current spread, equals 1; the remaining spreads
are story pages and show scraps alone. Both kinds draw scraps, so the results page is a story page
with the card laid over its right half.

### Resolving a row to a file

⚠ **Whether a row opens at all is the `Zoom` column alone (`!= 0`), not `ImageType`'s second
letter.** `ImageType`'s letters select extensions independently (`B` gives `.BMP`, `J` gives
`.JPG`, anything else `.PNG`), the same three-way rule applying to the page image (first letter)
and, when the row opens, the zoom inset (second letter). An earlier reading of this file blamed the
second letter for the open/closed split itself; it does not survive the data. `1_1_3`
(`SB_01_02_mag2`) ships `ImageType` `P0` and a real `Zoom` letter (`M`), and opens. What actually
splits the 461 rows: the 167 `DZ_generic_corners` photo-corner mounts all carry `Zoom=0` and never
open; the other 294 (167 captures, 127 ordinary scraps) all carry a real family letter and always
do. `ImageType` itself is `P0` (245: 161 of the mounts plus 84 ordinary scraps), `PP` (167, every
capture), `PJ` (43, ordinary scraps whose zoom inset is a distinct `.JPG`) or blank (6, the
remaining mounts, both letters absent). The page image is `Scrapbook\` plus the name, which the
script prefixes with `assets\graphics\`; the zoom background is
`Assets\Graphics\ScrapBook\SB_BG_<Zoom>.jpg` and the zoom's inset image is `Assets\Graphics\` plus
the name under its own (second-letter) extension. All 294 page images, all 43 `.JPG` zoom insets
and all 26 `SB_BG_*.jpg` backgrounds are present in the shipped install.

The `Zoom` letter also names the text layout: `SBZ_T_TITLE<letter>`, `SBZ_T_CAPTION<letter>` and
`SBZ_T_TEXT<letter>` in `LAYOUT.CSV` give each family its own box, colour and justification, 26
families in all. Two of those rows carry typos the engine will not parse as colours,
`xff000000` in `SBZ_T_CAPTIONA` and `oxff1E283C` throughout family `J`.

### The `Objective` gate

A row with `Objective` 0 always draws. Any other value first requires bit 0 of the mission's
**merged best-to-date** completion mask, meaning the mission has been won at least once; a positive
value then requires that bit of the same mask, and **a negative value requires that bit to be
clear**, so a scrap can be authored for having failed a secondary objective. Three rows use this
(`-11` and `-12`). The gate is skipped entirely while the unlock flag at `0x00647b80` is set. A
mission is readable at all only up to the campaign's current position, which is what leaves an
unreached slot showing langui 1219 `Not yet flown`.

### The danger-zone slot

A scrap whose name begins `Snap_` is a player capture, not shipped art: it resolves against the
profile directory instead of `assets\graphics\`, is skipped when the file is not on disk, is forced
to a 164×123 region, and is drawn at 25% on the page but full size in the zoom. 167 of the 461 rows
are these, named `Snap_<mission>_<objective>` and gated on that objective, with a
`DZ_generic_corners` row at identical coordinates one step higher in draw order supplying the
photo-corner mount. This is the read half of `BL-256`.

### The grime

`uiData` 2413 is a small deterministic random generator for the smudge overlay. Called with -1 it
seeds from `(mission << 8) | spread` and clears a ten-bit used-mask; called with an item index it
returns an unused value 0 to 9 and marks it used, resetting once all ten are taken; called with -2
it reseeds from the clock. The overlay is therefore stable for a given page and varies between
pages, and `SB_P_GRIME` is drawn one z above the scrap it dirties.

### The results rows, and the one that is not drawn

`uiData` 2406 returns five strings for the tab it is given: the outcome line and four values. The
script binds exactly those five to `sb_t_completetitle`, `sb_t_time`, `sb_t_hit`, `sb_t_cash` and
`sb_t_planes`. **`LAYOUT.CSV` and langui both carry a Rockets Expended row that nothing draws**:
`SB_T_ROCKETSTITLE` and `SB_T_ROCKETS` exist, `IDS_SB_ROCKETS_TITLE` is langui 1204, and
`SCRAPBOOK.SCRIPT` never creates either widget. Its y macro is the tell: the drawn rows sit at
`SLINE1` 369, `SLINE2` 412, `SLINE4` 436, `SLINE5` 460 and `SLINE6` 484, evenly spaced once Rockets
is absent, while `SLINE3` is stranded at 388 between the first two. The row was cut and the
remaining five were re-spaced over it.

The five strings are the outcome line (langui 1213 or 1214 on bit 0 of the tab's objective mask),
the run time as `mm:ss` out of the millisecond field, the gun hit ratio as a percentage of the
hit and shot words, the cash field, and the sum of the two per-airframe kill arrays. Only the last
is computed rather than read: [`org/debrief.md`](../org/debrief.md) has the arithmetic and where the
arrays come from.

### The kill stamps

The eleven stamps are fixed positions `SB_KILL0` to `SB_KILL10` with matching `SB_KILLTEXT0` to
`SB_KILLTEXT10`. The positions are scattered over the page rather than laid out in reading order
(`SB_KILL1` at `467,93` sits left of and above `SB_KILL0` at `560,109`), so a page with three stamps
puts its first in the middle, its second at the left and its third at the lower right.

The script asks `uiData` 2404 for ordinals 0 to 10 and stops at the first negative return, so the
slots fill densely from `SB_KILL0` in the order the engine reports rather than by airframe. Each
answer is a frame index for the shared art and a count string formatted through langui 520
`IDS_KILLCOUNT`. `SB_killMARKERcombined.png` is a vertical strip of **22 frames of 70x100**: frames
0 to 10 are the eleven airframes in the engine's own order (Hoplite, Hellhound, Balmoral, Bloodhawk,
Brigand, Devastator, Firebrand, Fury, Kestrel, Peacemaker, Warhawk) with the airframe name drawn
into the stamp, and frames 11 to 21 are the same eleven again over a star. The star is the ace
variant; which kills earn it is in [`org/debrief.md`](../org/debrief.md).

### Entry, exit and the table of contents

Every button that leaves the book, the zoom or the table of contents, stated the same way as the
[Screen flow](#screen-flow) table above: by its `LAYOUT.CSV` `ScriptToExe` column when it carries
one, or by the script's own `script_run` / `script_pause` / `script_continue` / `script_end` /
`mail` when it does not.

| From | Control | Goes to | Stated by |
|---|---|---|---|
| Cabin | `PC_B_PREVIOUS` | `SCRAPBOOK_TOC.SCRIPT` | layout |
| Mission end | (automatic) | `SCRAPBOOK.SCRIPT`, at the flown mission's spread 1 | `crimson.exe`, `FUN_0046fb60`; see [`org/debrief.md`](../org/debrief.md#the-pass-in-order) |
| Book | a scrap | `SCRAPBOOKZOOM.SCRIPT`, paused rather than ended | script |
| Book | `sb_b_toc`, or `sb_b_prev` at the front of the book | `SCRAPBOOK_TOC.SCRIPT`, paused rather than ended | script |
| Book | `sb_b_replay` | ends the book; Replay Mission's own fork, below | script |
| Book | `SB_B_RETURNPC` | the cabin | layout |
| Zoom | Esc, or `sbz_b_return` | back to the paused book | script |
| Zoom | `sbz_b_export` | `messagebox.script` (an export confirmation; does not return to the book) | script |
| Table of contents | `sbtoc_b_view`, `sbtoc_b_current`, or a double-clicked row | the book, at the picked mission's spread 1 | script |
| Table of contents | `sbtoc_b_replay` | ends the table of contents; Replay Mission's own fork, below | script |
| Table of contents | `SBTOC_B_RETURN` | the cabin | layout |

`SB_B_RETURNPC` and `SBTOC_B_RETURN` both carry `ScriptToExe = PassengerCabin`, the same
layout-declared-transition pattern the [Reader rules](#reader-rules-and-edge-cases) already record
for the cabin's own Return to Main Menu: neither button has a case in its script's `gui_mailbox`.

**Replay Mission forks on a session flag the scripts only read, `$$SR$$`.**
`PASSENGERCABIN.SCRIPT` zeroes it at its own `gui_create`; nothing in the three scrapbook scripts
sets it, so only the mission-end path can leave it set. The book's `sb_b_replay` handler (the table
of contents' `sbtoc_b_replay` is the same logic over its own selected row):

```
FRA = callback(uiData, 2405, 0)        // the mission the open page shows
if (FRA == $$KP$$ && $$SR$$)           // still the mission just flown, same session
    restart in place: callback(uiData, 2013, -1, ...), gosCallback 1
else
    the ordinary mission-select path: mail 11003, gosCallback 31, gosCallback 6
```

which is the same three calls (`mail` 11003, `gosCallback` 31, `gosCallback` 6) the cabin's own New
Mission button makes. Replay Mission's *availability* does not fork this way: `uiData` 2411 gates
it on the shown mission's own record alone, regardless of how the book was reached.
[`org/debrief.md`](../org/debrief.md#entering-the-book-replay-mission-and-the-table-of-contents)
has the full trace and what is still unread (which native function answers `PC_B_PREVIOUS` and
where `$$SR$$` is set).

## Callback reference

Only the ids these five scripts use. `uiData` dispatches ids 2000 to 2038 through a jump table at
`0x0040f518` and 2100 to 2413 through a byte table at `0x0040f788` plus a jump table at
`0x0040f5b4`; 2099 and 2600 are tested separately. A first argument of -1 or -2 selects the pilot
or wingman slot, and any other value is a plane index.

| id | Arguments | Behaviour |
|---|---|---|
| 2005 / 2007 | row, out | pilot / wingman gun row; count 8 |
| 2006 / 2008 | row, out | pilot / wingman rocket row; count 8 |
| 2009 | slot, flags, out | mission name, langui `3450 + m - 1` (flags 1) or `3480 + m - 1` (flags 2) |
| 2010 | slot | the slot's airframe index, 0 to 10 |
| 2011 | slot, out | the slot's plane name and airframe title |
| 2014 | slot, plane, fromIA | set the slot's plane override; -3 means "use the profile's selection" |
| 2018 | out | owned plane count, less one on missions 13 and 17 |
| 2021 | | grant and select this mission's story aircraft |
| 2022 | mission, out | the mission's objectives text |
| 2027 | row, out | ammunition list row, langui `3360 + row`; count 5 |
| 2028 | row, out | rocket list row, langui `3395 + index`; count is the available subset |
| 2030 | slot, group, out, out | gun group's gun id (5 = none) and current ammunition |
| 2031 | slot, pylon, out | pylon's current ordnance, or -1 if the pylon does not exist |
| 2032 / 2033 | index, out | ammunition / rocket description, title plus body |
| 2034 / 2035 | slot, fromIA | commit / load the 204-byte working copy |
| 2036 / 2037 | slot, index, value | write an ammunition / ordnance pick into the working copy |
| 2038 | row, out | mission dropdown row, langui `3450 + row`; count 24 |
| 2099 | name, out | roster lookup; out is the index, or the roster count when not found (returns -1) |
| 2100 | name | set the current profile name; returns non-zero when it changed |
| 2101 | out | the current profile name |
| 2102 | row, out | roster row; count is the number of profiles |
| 2104 | mission | set the current mission; -2 means `completed + 1`; clamped to 24 |
| 2106 | out | fills a messagebox string (not decoded further) |
| 2150 | mode, arg, out | read (mode 0) or write (mode 1) the profile's memento file name |
| 2151 | out | the chapter number when that chapter has not been started, else 0 |
| 2600 | | non-zero while a next mission exists |

The scrapbook's own ids, `0x0040a30f` upwards in the same dispatcher. Resolve any of them by hand
with the rule above: byte at `0x0040f788 + (id - 2100)`, then dword at `0x0040f5b4 + entry * 4`.

| id | Handler | Behaviour |
|---|---|---|
| 2401 | `0x0040a682` | out flags: whether a next spread and a Current Mission jump are available |
| 2402 | `0x0040a6a2` | 0 jumps to the current mission at spread 1; 100 steps forward, 101 back, returning 0 at the front of the book |
| 2403 | `0x0040a30f` | the current spread number; 1 is the results page |
| 2404 | `0x0040a714` | the nth non-empty kill stamp of a tab: a frame index 0 to 21 and a count string, or -1 once the tab has no more |
| 2405 | `0x0040a633` | mode 1 opens a mission at spread 1, or the campaign's current mission when given -1; any other mode reads the open mission |
| 2406 | `0x0040a7d4` | the outcome line and four result values for a tab, above |
| 2407 | `0x0040aa52` | walk to the next drawable item of this spread, returning type 5 for an image and 6 for text |
| 2408 | `0x0040a453` | the page title: mission name and area, out through `ESA.BC` |
| 2409 | `0x0040a4b8` | a table-of-contents row: given an ordinal, out a plane-icon selector and three text lines (mission name, area, plane flown) |
| 2410 | `0x0040a935` | the zoom view: background, inset image and position, layout letter, and the title, caption and text ids |
| 2411 | `0x0040a408` | is Replay Mission offered, which is true once either half of the mission's record holds a time |
| 2412 | `0x0040a3d9` | export the open scrap to the desktop |
| 2413 | `0x0040a321` | the grime generator, above |

2414 and 2502 are outside the `2100`–`2413` table and are answered by a different widget; 2414 asks
whether a name resolves outside `assets\graphics\`, which is true only for a `Snap_` capture.

`gosCallback` ops:

| op | Behaviour |
|---|---|
| 1 | fly the current mission |
| 6 | open the briefing for the current mission |
| 7 | the story chapter (1 to 5) of a mission; -2 means the campaign's current position |
| 9 | reset the in-memory profile and load the named one |
| 12 | write the profile out |
| 16 | clear and re-enumerate the profile roster |
| 17 | delete the named profile |
| 27 | does this mission have a wingman |
| 28 | the mission's objective text lines |
| 31 | join the loader thread and tear down the mission world |

`uiControl` 2141 case 0 writes the current profile name to `HKCU`'s `UIPlayerName`; case 1 to 8 of
the same id, and its read twin 2140, cover the multiplayer callsign, game name, team name, voice,
auto-refresh, IP address, phone number and connection type. 3107 is the edit-box name validator.

## Reader rules and edge cases

- **Read the transitions from `LAYOUT.CSV` first.** The `ScriptToExe` column carries roughly half
  the navigation, and the scripts are silent about those buttons.
- **A slot argument of -1 or -2 is not a plane index**, and any other value is. The same convention
  runs through every campaign `uiData` id.
- **Missions are 1-based** in every callback above, and 0-based when a `cm_sequence` entry is
  addressed directly (`gosCallback` 1, 6, 7 and 27 all take `mission - 1`).
- **A blank list row is data.** Gun rows exist per barrel and are empty when the group has no gun
  or no second barrel; rocket rows are empty when the pylon holds 11 or does not exist. Neither is
  padding to fill in.
- **A dropdown's row count in `LAYOUT.CSV` is a display window**, and `@globals@AR` is an array
  capacity. Neither is the item count, which always comes from the fill callback's row -1 answer.
  This is the same trap [instant-action.md](instant-action.md) records for `ia_d_planep`.
- **Do not derive an ordnance id from a dropdown row.** The rocket list is filtered by campaign
  progress; ammunition is not.
- **The profile name becomes a directory name.** The original rejects `" * / : < > ? \ |` and
  proves the name by creating a directory with it.
- **`brief_c<NN>` state keys are `<campaign folder index><mission number>`.**
  `cm_sequence.zrd`'s per-entry `campaign` and `mission` fields reproduce all 24 keys exactly
  (Hawaii is folder 6 with missions 1 to 5, so `brief_c61` to `brief_c65`; Northwest is folders 3,
  1, 2, 1, 1 with missions 1 to 5, so `brief_c31`, `brief_c12`, `brief_c23`, `brief_c14`,
  `brief_c15`, and so on for Hollywood, Colorado and Manhattan). The briefing narration wav's
  `m<N>` is likewise the folder mission number, not the story position:
  Hawaii's story mission 2 is folder mission 5 and plays `c1-HA-m5_briefing.wav`.
  This closes the open question in [briefing.md](briefing.md) about which shipped mission folder
  each briefing state belongs to.

## Evidence and limits

Every claim above comes from one of three places, named at the point of use: the script text, the
`LAYOUT.CSV` row, or a `crimson.exe` handler. The five reference screenshots
(`OriginalScreenshots\Campaign *.png`) were checked against every documented transition and
corroborate but do not establish them.

| Screen | Script-proven | Screenshot-corroborated | Open |
|---|---|---|---|
| Player profile | every widget, the roster fill, the validator, all four exits | the roster list's own selection colour (`0xff800000`), the four controls, the pre-filled name | what `uiData` 2106's message says; the unreachable savegame branch |
| Cabin | the six buttons and their targets, the deactivated SAVE GAME, the plane photo path, the memento source, the pin count, the `idaho` dropdown, the intro trigger | five buttons and no SAVE GAME, the painted cabin, the framed memento, the map | whether anything on the screen animates or loops, and how the pin frames read; both need the cabin capture |
| Chapter intro | the movie name, the skip gesture, the one-shot guard | not covered by any screenshot | the MPG decode itself, deliberately out of scope |
| Flight check | both slots, all four lists, the wingman gate, both plane-change rules, the grant table, both exits | the title, plane lines, six of eight gun rows filled and two blank, the objectives note, the calibre label's string block, both buttons | the `10018`/`10000` state art |
| Ammo selection | both callers, the working-copy commit, the greyed empty group, the pylon deactivation, all six string blocks | the greyed fourth group, two of four pylons per wing, both description panes, both plane diagrams | the rocket table's unread second field |
| Scrapbook | every button's transition, the composition file, the results rows, the kill stamps, the table of contents, the Replay Mission fork | the three reference spreads scrap for scrap, the stamps and total on two of them | the native side of the cabin entry and of `$$SR$$`, both in `crimson.exe`, unreached this session |

Where this decode stops:

- **`uiData` 2012, 2013, 2015, 2019 and 2020** are the plane-selection screen's ids and were not
  followed; `2000` and `2001` belong to the save and load screens.
- **The three flags that bypass the ordnance availability filter** were located, not identified.
- **The obfuscated identifiers were not recovered.** Widget keys, callback ids, message ids and
  langui ids carry the meaning here, and the letters were left alone.
- **`$$KP$$`** is read as the current mission, 1-based, because the script's own `case 13: case 17:`
  and callback 2018's test of `UIData +0x00` against 13 and 17 must name the same missions, and
  because the two aircraft those missions grant are named `IDS_HW3FURYNAME` and
  `IDS_RM2HOPLITENAME`, which match missions 13 and 17 under that reading (Hollywood mission 3, and
  Rocky Mountains mission 2, which is `cm_sequence`'s `COLORADO` area) and not under the
  alternative. The placeholder's own binding was not traced.
- **`FUN_00417090` against `FUN_00417410`.** Which of the two a mission launch takes is decoded
  (first attempt against replay); what differs between them beyond an extra loader call and the
  save-system calls is not.
