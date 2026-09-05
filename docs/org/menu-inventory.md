# The menu inventory: every in-scope screen, transition and asset

The authoritative census behind `PLAN-menu-presentations`: what
Built-in draws today, what the original's own data ships, which of the two answers each other, and
what neither answers yet. Part 1 is read out of `CSVM/src/UI`; part 2 out of the extracted UI
archive at `extracted/rof/` and the format decodes that already cover parts of it; part 4 names the
gaps as owed captures.

⚠ **A filename or a rectangle proves composition, not interaction.** Every row below says which of
the two its evidence carries. The original's campaign screens are the best-evidenced family in the
archive and that says nothing about the top level, the setup screen or the options pages, which is
the reading this page exists to prevent.

**Where the other halves live.** The archive container and the GUI script language are
[`formats/rof.md`](../formats/rof.md); the campaign scripts' behaviour is
[`formats/campaign-screens.md`](../formats/campaign-screens.md); the setup screen's option sets are
[`formats/instant-action.md`](../formats/instant-action.md); the briefing dialog is
[`formats/briefing.md`](../formats/briefing.md); the string table is
[`formats/strings.md`](../formats/strings.md). How the original's 800x600 space meets a modern
window is [`campaign-board.md`](campaign-board.md), which every screen here inherits. The hangar's
own callbacks and economy are [`hangar.md`](hangar.md), the mission-end book
[`debrief.md`](debrief.md). Built-in is `CSVM/src/UI/LaunchMenu.cs`, `HangarFlow.cs`,
`CampaignFlow.cs` and the `CampaignBoards`/`ComposedBoard`/`BoardFit` trio.

## Contents

- [Totals](#totals)
- [Part 1: Built-in as it stands](#part-1-built-in-as-it-stands)
  - [The three layouts](#the-three-layouts)
  - [Screen census](#screen-census)
  - [Launchscreen transitions](#launchscreen-transitions)
  - [Hangar transitions](#hangar-transitions)
  - [Campaign transitions](#campaign-transitions)
  - [Sub-states and modals](#sub-states-and-modals)
  - [The `--menu=` aid map](#the---menu-aid-map)
- [Part 2: the original as the data ships it](#part-2-the-original-as-the-data-ships-it)
  - [What the shell is made of](#what-the-shell-is-made-of)
  - [`LAYOUT.CSV`'s own shape](#layoutcsvs-own-shape)
  - [The navigation graph the layout states](#the-navigation-graph-the-layout-states)
  - [Coverage](#coverage)
  - [In scope and out of scope](#in-scope-and-out-of-scope)
  - [Screen by screen](#screen-by-screen)
  - [Menu audio](#menu-audio)
- [Part 3: assets, required and optional](#part-3-assets-required-and-optional)
- [Part 4: what the evidence does not cover](#part-4-what-the-evidence-does-not-cover)
  - [Owed captures](#owed-captures)
  - [Where Built-in and the original disagree](#where-built-in-and-the-original-disagree)
- [The decoded layout](#the-decoded-layout)

## Totals

| Quantity | Count |
|---|---|
| Built-in player-visible screens | **28** (10 launchscreen + 9 hangar + 9 campaign) |
| Built-in sub-states and modals that are not screen enum members | **9** |
| Built-in transitions catalogued below | **110** (47 launchscreen + 27 hangar + 36 campaign) |
| `--menu=` values the parser accepts | **32** (30 launchscreen screens, 2 load-screen aids) |
| `--menu=` values `docs/cli.md` documents | **31** (`name` is accepted and undocumented) |
| GUI scripts in `crimson.rof` | **61** (5 infrastructure, 34 single-player screens, 22 multiplayer) |
| `LAYOUT.CSV` sections | **35** (`[GLOBALVARS]` + 34 screens, one per single-player script) |
| `LAYOUT.CSV` widget rows | **636**, in **10** of the 11 documented widget types |
| `LAYOUT.CSV` macro definitions | **186** (29 global, 157 per-section) |
| Distinct art files `LAYOUT.CSV` names | **124** (122 present in the extraction, 2 absent) |
| Distinct `IDS_*` symbols `LAYOUT.CSV` names | **152**, of which **149** resolve to text |
| UI sound files | **8** |
| Original screens in this plan's scope | **27** of the 34 single-player screens (23 built; the 4 remaining Preferences pages stand behind doors drawn disabled) |
| Layout-stated navigation edges in the original | **46** (25 driven by Original, 2 realised as the wingman slot's row, 5 drawn disabled, 14 out of scope; see [Coverage](#coverage)) |

## Part 1: Built-in as it stands

### The three layouts

`LaunchMenu.Rebuild` chooses between three mutually exclusive surfaces, and every Built-in screen is
drawn by exactly one of them. This is the first thing the presentation boundary has to hold, because
the three take different input and different geometry.

| Surface | When | What it is |
|---|---|---|
| Centred zones (`_zones`) | every screen that is not one of the two below | header / middle / footer bands, fixed band heights, one centred content column |
| Split panes (`_paneRoot`) | `Screen.Plane` with more than one joined player | one `SplitScreen.PaneRect` panel per player plus a shared bottom strip |
| Composed board (`_boardRoot`) | `Screen.Campaign` with a live flow | a `ComposedBoard` at authored 800x600 pixels through `BoardFit` |

### Screen census

`LaunchMenu.Screen` has 12 members. Two of them, `Hangar` and `Campaign`, are host states that
delegate to a flow rather than screens of their own, so the 12 contribute 10 player-visible screens.

| # | Built-in screen | Enum | Surface | Drawn from |
|---|---|---|---|---|
| 1 | Mode | `Screen.Mode` | centred | `Modes` + the campaign and hangar door rows |
| 2 | Chapter | `Screen.Chapter` | centred | `Chapters`, filtered by `DangerZones` under Stunt Flying |
| 3 | Table of Contents | `Screen.Presets` | centred | `InstantActionPresets.All`, 19 rows in a 14-row window |
| 4 | Environment | `Screen.Environment` | centred | `Environments` (7) |
| 5 | Mission type + lives | `Screen.MissionType` | centred | `CurrentMissionTypes` (4, minus Stunt Flying where barred) |
| 6 | Waves | `Screen.Waves` | centred | four wave slots plus a Continue row |
| 7 | Wave editor | `Screen.WaveEdit` | centred | four fields over one slot |
| 8 | Wingmen | `Screen.Wingmen` | centred | count + aircraft |
| 9 | Wingman loadout | `Screen.WingmanLoadout` | centred | `FitRowsFor` over the wingman airframe |
| 10 | Aircraft select | `Screen.Plane` | centred or split | `_roster` plus the hangar door row for player 1 |
| 11-19 | the nine hangar screens | (`Screen.Hangar` host) | centred | `HangarFlow.Order` |
| 20-28 | the nine campaign screens | (`Screen.Campaign` host) | board | `CampaignFlow.Registry` |

`HangarScreen` (9, linear in `HangarFlow.Order`): PlaneSelection, Airframe, Engine, Armour, Guns,
Hardpoints, Paint, Name, Purchase.

`CampaignScreen` (9, a graph over a stack): Roster, Cabin, PreviousMissions, Briefing, FlightCheck,
Ammo, PlaneSelection, Scrapbook, ScrapbookZoom.

### Launchscreen transitions

47 edges. Trigger names use the footer's own vocabulary: Accept is Enter / A, Back is Esc / B,
Loadout is L / Y, Presets is P / X.

| From | Trigger | To |
|---|---|---|
| Mode | Accept on Free Flight | Chapter, mode Free |
| Mode | Accept on Instant Action | Environment, mode Stunt |
| Mode | Accept on Dogfight | Chapter, mode Versus |
| Mode | Accept on the Campaign row | Campaign host, `CampaignScreen.Roster` |
| Mode | Accept on Build Custom Plane | Hangar host, `HangarScreen.PlaneSelection`, return Mode |
| Mode | Back | `Quit`, and the host leaves the game |
| Chapter | Accept | Plane (`PrimeJoins`) |
| Chapter | Back | Mode |
| Environment | Accept | MissionType; loads the environment's `ia.zrd.json` as `_iaBaseDef` |
| Environment | Presets | Table of Contents, cursor on the applied preset |
| Environment | Back | Mode |
| Table of Contents | Accept | applies the preset, returns to Environment |
| Table of Contents | Back | Environment, fields untouched |
| MissionType | Accept, Dogfight an Ace | Plane, skipping Waves and Wingmen |
| MissionType | Accept, any other type | Waves, cursor on the Continue row |
| MissionType | Back | Environment |
| Waves | Accept on a wave row | Wave editor |
| Waves | Accept on Continue | Wingmen |
| Waves | Back | MissionType |
| Wave editor | Accept | Waves (the fields were edited live) |
| Wave editor | Back | Waves |
| Wingmen | Accept | Plane (`PrimeJoins`) |
| Wingmen | Loadout, with wingmen | Wingman loadout |
| Wingmen | Back | Waves |
| Wingman loadout | Back or Loadout | Wingmen, fit kept |
| Plane | Accept, browsing | that seat locks its airframe |
| Plane | Accept, locked | that seat confirms; when every seat has, `FireLaunch` |
| Plane | Accept on the door row, player 1 | Hangar host, return Plane |
| Plane | Loadout, locked and unconfirmed | that pane's Ammo Selection list |
| Plane | Start on a free pad | that pad joins as a new seat |
| Plane | Back inside a pane's list | back to the airframe, fit kept |
| Plane | Back, confirmed | back to locked |
| Plane | Back, locked | back to browsing |
| Plane | Back, browsing, player 1, mode not Stunt | Chapter |
| Plane | Back, browsing, player 1, Dogfight an Ace | MissionType |
| Plane | Back, browsing, player 1, other Stunt types | Wingmen |
| Plane | Back, browsing, a guest | that seat unjoins |
| any screen but Plane | Back, a guest | that seat unjoins |
| Hangar host | flow `Exit` set, opened from Mode | Mode, roster refreshed |
| Hangar host | flow `Exit` set, opened from Plane | Plane, cursor on the built plane |
| Hangar host | flow `Exit` set, opened from Campaign | Campaign, flow resumed and profile re-read |
| Campaign host | `CampaignExit.Cancelled` | Mode |
| Campaign host | `CampaignExit.OpenHangar` | Hangar host over the profile's wallet, return Campaign |
| Campaign host | `CampaignExit.FlyMission` | `LaunchCampaign`; screen resets to Mode behind the session |
| (a session) | a board's Exit, when the run was menu-driven | `ShowLaunchMenu` → `ShowMenu(_spec.MenuStartScreen)` |
| (a session) | the build failed | the same, plus `ShowError` |
| (a campaign mission) | the mission ended | `OpenCampaignScrapbook` → Campaign host, `Scrapbook` |

⚠ **Every return from flight replays the CLI's own `--menu=` value.** `ShowLaunchMenu` passes
`_spec.MenuStartScreen` on every call, not only the first, so a run started with
`--menu=campaign-cabin` re-enters that aid on the way back rather than the Mode screen. E43 inherits
this: a semantic return destination has to beat the startup aid, or it cannot.

⚠ **`LaunchMenu.OpenCampaignCabin` has no caller.** It is public, documented in
`docs/architecture.md` as the campaign's return door, and reached from nothing in `CSVM/src` or
`CSVM.Tests`: the mission-end path goes through `OpenCampaignScrapbook`, with the cabin behind the
book. E43 either wires it or deletes it, and that architecture line needs correcting either way.

### Hangar transitions

27 edges over `HangarFlow`. The order is linear, which is itself a divergence recorded in
[Where Built-in and the original disagree](#where-built-in-and-the-original-disagree).

| From | Trigger | To |
|---|---|---|
| any of the first 8 screens | Accept past the page's own handling | the next screen in `Order` (8 edges) |
| any of the last 8 screens | Back | the previous screen in `Order` (8 edges) |
| PlaneSelection | Back | `HangarExit.Cancelled`, the scratch plane dropped |
| PlaneSelection | Accept on New Plane | `StartNewPlane`, on to Airframe |
| PlaneSelection | Accept on a saved plane | `StartFromSaved` (a copy), on to Airframe |
| PlaneSelection | Accept on the trailing Delete / Sell row | the removal list |
| removal list | Accept on a plane row | `DeleteSaved`, list re-read |
| removal list | Accept on Cancel | back to the plane list |
| Airframe | Accept on a row that changes the airframe | raises `DefaultsAsk` |
| defaults ask | Accept on OK | loads the airframe's defaults, ask cleared |
| defaults ask | Accept on Cancel | keeps the current fit, ask cleared |
| Purchase | Accept, gate passed | `HangarExit.Built`, `CustomPlaneStore.Save` |
| Purchase | Accept, gate refused | stays, `Message` carries the refusal in the original's words |

### Campaign transitions

36 edges over `CampaignFlow`.

| From | Trigger | To |
|---|---|---|
| Roster | Accept on Continue, or on a roster row | Cabin, that profile seated |
| Roster | Accept on Cancel, or Back off the first screen | `CampaignExit.Cancelled` |
| Roster | Accept on Delete Player | the delete confirm |
| delete confirm | Accept on Delete | the profile is removed, back to Roster |
| delete confirm | Accept on Keep | back to Roster |
| Roster | Accept on the name row | arms the text field (`CapturesText`) |
| Cabin | Accept on Next Mission | Briefing |
| Cabin | Accept on Previous Missions | PreviousMissions |
| Cabin | Accept on Plane Construction | `CampaignExit.OpenHangar` |
| Cabin | Accept on Return to Main Menu, or Back | `CampaignExit.Cancelled` |
| PreviousMissions | Accept on a mission row | Scrapbook at that mission's spread 1 |
| PreviousMissions | Accept on the current-mission tab | Scrapbook at the campaign's own position |
| PreviousMissions | Accept with an empty list | Scrapbook at slot 0 |
| PreviousMissions | Accept on Replay Mission | Briefing |
| PreviousMissions | Back, or Return to Cabin | Cabin |
| Briefing | Accept on Go To Flight Check | FlightCheck |
| Briefing | Back, or Return to Cabin | Cabin |
| FlightCheck | Accept on Change Ammo | Ammo, for that crew slot |
| FlightCheck | Accept on Change Plane | PlaneSelection, for that crew slot |
| FlightCheck | Accept on Return To Briefing, or Back | Briefing |
| FlightCheck | Accept on Fly Mission, guests still to fill in | the next guest's own page of the same screen |
| FlightCheck | Accept on Fly Mission, field complete | `CampaignExit.FlyMission` |
| Ammo | Accept on a combo row | opens that drop-down |
| Ammo | Accept inside an open combo | commits the pick, closes it |
| Ammo | Accept on Accept Loadout | FlightCheck, working copy committed |
| Ammo | Accept on Cancel Loadout, or Back | FlightCheck, working copy dropped |
| PlaneSelection | Accept on a combo row | opens that drop-down |
| PlaneSelection | Accept inside an open combo | commits the pick, closes it |
| PlaneSelection | Accept on Accept Selections | FlightCheck |
| PlaneSelection | Accept on Cancel Selections, or Back | FlightCheck |
| Scrapbook | Accept on a scrap | ScrapbookZoom on that scrap |
| Scrapbook | Accept on View All Missions | PreviousMissions |
| Scrapbook | Accept on the back tab at the front of the book | PreviousMissions |
| Scrapbook | Accept on Replay Mission | Briefing |
| Scrapbook | Accept on Return To Cabin, or Back | Cabin |
| ScrapbookZoom | Accept on Continue, or Back | Scrapbook |

### Sub-states and modals

Nine states a screen can be in that are not screen enum members. Each is a separate presentation
problem, and none of them is visible to a census of the three enums.

| # | State | Owner | What changes |
|---|---|---|---|
| 1 | split-pane aircraft select | `LaunchMenu` | one panel per joined player instead of the centred column |
| 2 | a pane's Ammo Selection list | `PlayerSeat.InLoadout` (the shared player setup) | that seat reads nothing else, and nobody can launch while one is open |
| 3 | the hangar's defaults ask | `HangarFlow.DefaultsAsk` | the Airframe screen becomes a two-row question |
| 4 | the hangar's removal list | `HangarPlaneSelectionPage._removing` | the plane list becomes a Delete / Sell list plus Cancel |
| 5 | the roster's delete confirm | `CampaignRosterPage._confirming` | the whole screen becomes two rows |
| 6 | an armed name field | `CampaignFlow.CapturesText` | the keyboard types and the cursor axes come from the pad alone |
| 7 | an open drop-down | `CampaignFlow.OpenCombo` | ⚠ only while its own row is focused; a page that leaves one open behind a moved cursor strands it |
| 8 | a guest's flight check | `CampaignFlightField` | the same screen, driven by that guest's own device |
| 9 | the error line | `LaunchMenu._error` | a refusal from either flow's gate rides the focused screen |

### The `--menu=` aid map

The parser accepts **32** values; `docs/cli.md` documents **31**. `name` (the hangar's PLANENAME
screen, reached through `OpenHangarAid`) is accepted and undocumented, which is a `cli.md` edit
outside this item's fence.

Every accepted value appears exactly once below. Eleven of the 12 `LaunchMenu.Screen` members are
named in it; `WaveEdit` is the twelfth and has no aid, which is the warning under the table.

| `--menu=` value | Opens | Screen enum |
|---|---|---|
| (absent), `mode` | the Mode screen | `Mode` |
| `chapter` | the chapter pick | `Chapter` |
| `presets` | the Table of Contents | `Presets` |
| `environment` | wizard step 1 | `Environment` |
| `missiontype` | wizard step 2 | `MissionType` |
| `waves` | wizard step 3, cursor on Continue | `Waves` |
| `wingmen` | wizard step 4 | `Wingmen` |
| `wingmanloadout` | the flight's one fit, arming two wingmen if none are set | `WingmanLoadout` |
| `plane` | aircraft select, browsing | `Plane` |
| `selected` | aircraft select, player 1's airframe locked | `Plane` |
| `loadout` | player 1's Ammo Selection list | `Plane` |
| `hangar` | the hangar's plane list | `Hangar` |
| `airframe` | the hangar's airframe list | `Hangar` |
| `defaults` | the airframe list mid-ask | `Hangar` |
| `name` | the hangar's PLANENAME screen (**undocumented**) | `Hangar` |
| `paint` | the hangar's paint screen | `Hangar` |
| `campaign` | the real profile roster | `Campaign` |
| `campaign-empty` | the roster with no profiles | `Campaign` |
| `campaign-roster` | the roster with two profiles | `Campaign` |
| `campaign-entry` | the roster with a name being typed | `Campaign` |
| `campaign-cabin` | the cabin | `Campaign` |
| `campaign-previous` | the previous-missions list | `Campaign` |
| `campaign-scrapbook` | the book at the last mission flown | `Campaign` |
| `campaign-briefing[:seconds]` | the briefing, reveal advanced | `Campaign` |
| `campaign-flightcheck` | the flight check | `Campaign` |
| `campaign-guestcheck[:player]` | a guest's own flight check | `Campaign` |
| `campaign-ammo` | ammo selection | `Campaign` |
| `campaign-planeselection[:export]` | plane selection, or its export messagebox | `Campaign` |
| `campaign-hangar` | the hangar over the profile's wallet | `Campaign` → `Hangar` |
| `campaign-fly` | walks a real profile to Fly Mission and launches | `Campaign` |
| `loadboard` | the load screen's blackboard, over the menu | out of scope |
| `loadboard-campaign` | the load screen's chart sheet, over the menu | out of scope |

⚠ **`Screen.WaveEdit` has no aid.** It is the only screen enum member with no `--menu=` value, so
the wave editor is the one Built-in screen that cannot be screenshot without a hand at the controls.
B11 and C21 need one before they can pin it.

⚠ **No aid can open a drop-down.** The `:<n>` argument spends itself on `flow.Move(1)`, so the two
open-combo states above are unreachable from the CLI; this is `BL-659` and it blocks D32's
pixel comparison of the ammo and plane-selection screens in their open state.

## Part 2: the original as the data ships it

### What the shell is made of

`crimson.rof` ships **61 GUI scripts** in `ASSETS/SCRIPTS/`. Five are infrastructure:
`SCRIPTLOADER` (the entry point, which runs the other four), `CTL` (the `LAYOUT.CSV`-driven widget
class library), `CC` (a second, independent widget library), `SHAREDITEMS` (list-row renderers) and
`GLOBALS` (cursors, sounds, the 3D font, the splash music). The remaining **56 are screens**: 34
single-player and 22 multiplayer.

`LAYOUT.CSV` carries **35 sections** (`[GLOBALVARS]` plus one `[@ScriptName@]` per screen), and
those 34 screen sections are exactly the 34 single-player scripts, one to one. **The 22 multiplayer
scripts have no layout section at all**: they use the `CC` library and assign geometry inline, so
decoding `LAYOUT.CSV` reaches 34 of the 56 screens and no part of multiplayer. Do not read the
archive's 61 scripts as 61 decodable screens.

A screen is three files acting together, which
[`formats/campaign-screens.md`](../formats/campaign-screens.md) established and which holds for
every section here: the layout row declares the widget, the script creates and drives it, and
`crimson.exe` answers the callbacks. Roughly half the navigation is in the layout rather than the
script (see below), so a reimplementation reading only the scripts finds those buttons inert.

### `LAYOUT.CSV`'s own shape

56,148 bytes, 1,222 lines, ASCII, CRLF. There is no header row: the file is an INI-like sectioned
key/value file whose values are comma-separated positional records, and lines 1 to 41 are a
self-documenting comment block declaring one field list per widget type. The four line kinds are a
comment (`;`), a section header (`[NAME]`, indented cosmetically to show the designers' hierarchy),
a macro definition (`G<n>=NAME,VALUE` file-wide in `[GLOBALVARS]`, `V<n>=NAME,VALUE` per section),
and a widget row (`KEY=<type>,field,field,…`, the key optionally tab-padded before the `=`).

**636 widget rows in 10 types.** The comment block documents eleven; `W` (sound object) is declared
and never used, so the shipped file authors no audio at all.

| Type | Widget | Rows | Script class |
|---|---|---|---|
| `T` | text | 297 | `PE` |
| `B` | button | 119 | `BE` |
| `P` | pane | 106 | `ZJ` |
| `D` | dropdown | 70 | `PM` |
| `A` | text list | 16 | `SJ` |
| `S` | scrolling text | 8 | `JN` |
| `M` | movie | 6 | `AL` |
| `L` | listbox | 6 | `EN` |
| `E` | edit box | 4 | `IM` |
| `Z` | slider | 4 | `DL` |
| `W` | sound object | 0 | `SK` |

⚠ **The comment block's declared field lists do not reproduce the shipped rows, for any type.**
No shipped row carries `HelpID`, `TabOrder`, `ScriptPointer` or `Group`; removing those four from
the declared lists reproduces every row's field count in the file. The visible case is `B`, where
`ResID` and `ScriptToExe` end up adjacent at indices 5 and 6
(`MM_B_CAMPAIGN=B,MM_B_Campaign.png,279,282,0,0,Campaign,0x1000,0,…`). The established per-type
orders are in [`formats/menu-layout.md`](../formats/menu-layout.md); this page does not restate
them.

⚠ **Two substitution mechanisms, easily confused.** `<NAME>` is textual macro substitution,
resolved against the section's own `V<n>` rows first and `[GLOBALVARS]`' `G<n>` rows second; it can
expand to a number, an ARGB colour or a filename. `[@ScriptName@]` is a section header only and
never appears inline. Cross-screen navigation uses the bare script name in the button's
`ScriptToExe` field, not a bracketed macro. **Every macro token the shipped file uses resolves**,
including the five [`campaign-board.md`](campaign-board.md) measured off screenshots as unresolved.

⚠ **A widget key a script creates need not have a layout row.** `MAINMENU.SCRIPT` creates
`mm_t_title` and fills it from `uiData` 2152, and `[@MainMenu@]` has no `MM_T_TITLE` row: the text
exists with no authored geometry. A decoder keyed on `LAYOUT.CSV` alone will not know the widget is
there, let alone where to put it.

The two other authored files are `SCRAPBOOK.CSV` (35,154 bytes, one `[SCRAPBOOK]` section keyed
`<mission>_<spread>_<item>`, with a quoted comma-bearing field `"Left,Top,Right,Bottom"` a parser
must handle) and `ASSETS/SCRIPTS/RESOURCE.H`, the `IDS_*` symbol to numeric id map that joins a
layout row to the string table.

### The navigation graph the layout states

**46 edges** carry a `ScriptToExe` name. This is the half of the navigation the scripts are silent
about, and it is machine-readable as it stands.

| From | Control | To |
|---|---|---|
| MainMenu | `MM_B_CAMPAIGN` | Campaign |
| MainMenu | `MM_B_INSTANTACTION` | InstantAction |
| MainMenu | `MM_B_MULTIPLAYER` | MultiPlayerMain |
| MainMenu | `MM_B_PREFERENCES` | Preferences |
| MainMenu | `MM_B_CREDITS` | Credits |
| Preferences | `PF_B_GAMEOPTIONS` / `PF_B_AUDIO` / `PF_B_VIDEO` / `PF_B_CONTROLS` | GameOptions / Audio / Video / ControlsPrefs |
| GameOptions, Audio, Video, ControlsPrefs | `*_B_ACCEPTCHANGES` / `*_B_CANCELCHANGES` | Preferences (8 edges) |
| ControlsPrefs | `CP_B_KEYS` | Keys |
| Keys | `KB_B_ACCEPTCHANGES` / `KB_B_CANCELCHANGES` | ControlsPrefs |
| PassengerCabin | `PC_B_CHANGEMOMENTO` | MomentoSelection |
| PassengerCabin | `PC_B_PREVIOUS` | ScrapBook_TOC |
| PassengerCabin | `PC_B_PLANEX` | **PlaneName** |
| PassengerCabin | `PC_B_RETURNMM` | MainMenu |
| MomentoSelection | `MS_B_ACCEPT` / `MS_B_CANCEL` | PassengerCabin |
| FlightCheck | `FC_B_CHANGEPLANE` / `FC_B_CHANGEPLANEW` | PlaneSelection |
| FlightCheck | `FC_B_CHANGEAMMO` / `FC_B_CHANGEAMMOW` | OrdinanceLayout |
| PlaneSelection | `PS_B_ACCEPT` / `PS_B_CANCEL` | FlightCheck |
| OrdinanceLayout | `OL_B_ACCEPT` / `OL_B_CANCEL` | FlightCheck |
| PlaneConstruction | `PX_B_AIRFRAME` / `_ENGINE` / `_ARMOR` / `_GUNS` / `_HARDPOINTS` / `_PAINT` | the six build screens |
| PlaneConstruction | `PX_B_Ready` | Purchase |
| ScrapBook_TOC | `SBTOC_B_RETURN` | PassengerCabin |
| ScrapBook | `SB_B_RETURNPC` | PassengerCabin |
| InstantAction | `IA_B_Exit` | MainMenu |
| IA_WrapUp | `IAWU_B_CONTINUE` | InstantAction |
| Credits | `CR_B_Exit` | MainMenu |

⚠ **The cabin's PLANE CONSTRUCTION goes to `PlaneName`, not `PlaneConstruction`.** The name comes
first in the original's own chain.

⚠ **`PX_B_*` is a tab bar, not a sequence.** The six build screens hang off `PlaneConstruction` as
siblings, each reachable from it directly; there is no next-screen edge between Airframe and Engine
in the data. Built-in walks them linearly, which is a divergence, not a decode.

### Coverage

`CSVM.Tests/OriginalCoverageTests.cs` is the machine check of this page against the Original
presentation: it drives `OriginalShell` from the top level to every screen and back by three input
families (pointer clicks on the rows' rectangles, keyboard cursor commands walking down and right,
pad cursor commands walking up and left), once over the hand-authored fixture layout and once over
the install's own `menu_layout.json` and art. Every `ScriptToExe` edge of an in-scope section must
have an entry saying how Original realises it, and the entry is checked against the shell. The
tally over the install: 24 screens (every `OriginalScreen`) reached and left with no open campaign,
build or dialog behind; 38 journeys by 3 families; 46 edges of which **25 are driven** (the row is
pressed and the target screen shows), **2 are realised as the wingman slot's row** (`FC_B_CHANGEPLANEW`
and `FC_B_CHANGEAMMOW` are the pilot's plaques at the wingman's slot, present exactly when the
mission flies a wingman), **5 are drawn disabled** (`MM_B_MULTIPLAYER`, `MM_B_CREDITS`, and the three
`PF_B_*` page doors the Options screen has no page behind) and **14 are out of scope** (the six
ACCEPT/CANCEL returns of Audio, Video and ControlsPrefs, `CP_B_KEYS` and Keys' two returns,
MomentoSelection's two returns and the cabin's `PC_B_CHANGEMOMENTO`, `IAWU_B_CONTINUE`,
`CR_B_Exit`); 0 dead ends. The exits are checked too: Quit as a `QuitExit`, ACCEPT CHANGES as a
`OptionsApplyExit`, FLY on Free Flight and Fly Mission on Instant Action as a `LaunchExit`,
FLY MISSION as a `CampaignMissionExit`, and Purchase Now returning to the top level with the plane
saved. Keyboard and pad share one semantic command vocabulary at the seat seam (Decision 25), so
the two cursor families differ in the walk they take, not in the commands the shell sees; the
device mapping behind them is the seats' own tests.

### In scope and out of scope

Decision 5 puts everything `LaunchMenu` hosts in scope. Mapping that onto the original's 34
single-player screens leaves **27 in and 7 out**, before the 22 multiplayer screens, which are all
out (CSVM's Dogfight is splitscreen on `dogfight_ace` spawns, not the original's network play).

**In scope (27):** MainMenu; Preferences, GameOptions, Audio, Video, ControlsPrefs, Keys;
InstantAction; Campaign, PassengerCabin, FlightCheck, PlaneSelection, OrdinanceLayout, ScrapBook,
ScrapBook_TOC, ScrapbookZoom; Hangar, PlaneName, PlaneConstruction, AirFrame, Engine, Armor, Guns,
HardPoints, Paint, Purchase; MessageBox.

**Out of scope (7):** CampaignIntro and FinalCinema (MPG playback, `BL-446`); Save and Load (no
savegame system here, and the `LOAD` branch is unreachable in the shipped build); Credits;
MomentoSelection (deferred, `BL-463`); IA_WrapUp (a flight board, excluded by Decision 5).

**Of the 27, four are in the census and out of this plan:** Audio, Video, ControlsPrefs and Keys.
No shared option exists behind any of them, so Original draws their three page doors disabled; a
later plan that adds a shared audio, video or controls option owns the page it belongs on.
GameOptions is built: the two options the store does carry stand on it, and `PF_B_GAMEOPTIONS` is
live.
Multiplayer is network play with no local counterpart, so `MM_B_MULTIPLAYER` draws its disabled
frame and takes no input; `MM_B_CREDITS` does the same, Credits being out of scope.

### Screen by screen

Composition means an authored rectangle, art name or string id. Interaction means a decoded
behaviour: what a press does, what a rollover changes, what is disabled when.

| Original screen | Built-in counterpart | Layout | Script decoded | Capture | Interaction evidence |
|---|---|---|---|---|---|
| MainMenu | Mode | 9 rows | this page | **none** | composition only; script text gives the six buttons and their targets, nothing about rollover, music or the flag movie. **Built as Original's top level** from the two panes and the six `B` rows with their four-frame strips: Campaign, Instant Action, Preferences (the Options screen) and Quit react, Multiplayer and Credits draw the disabled frame and take no input (out of scope), and the movie's place is black. Quit terminates on the press with no confirm, which is `MAINMENU.SCRIPT`'s own `terminate` on `mm_b_quit` |
| (none: remake-only) | Free Flight, Chapter, Plane | none | none | none | **Original's Free Flight door and screen, of our design under Decision 11.** The door is a text button in the `FC_B_CHANGEPLANE` paper-plaque convention beside the button frame, level with Campaign; the screen is the logo over two text lists (the shared chapter roster and the shared aircraft roster, the eleven stock airframes then the saved custom planes in an eleven-row window) with BACK and FLY plaques, a seat strip under the chapters and each later seat's tag on its aircraft row. Nothing on it is decoded |
| (none: remake-only) | Dogfight, Chapter, Plane | none | none | none | **Original's Dogfight door and screen, of our design under Decision 11.** The door sits under the Free Flight door in the same plaque convention; the screen is the Free Flight screen's shape over the Dogfight gate (a second seat must join and confirm before FLY stands). The original's Multiplayer is network play and ships no split-screen Dogfight, so nothing here is decoded |
| (none: remake-only) | the join strip and per-seat picks | none | none | none | **Original's join flow, of our design.** Start on an unclaimed pad joins a seat on either sortie screen (the same gesture and pad bookkeeping as Built-in's, through `MenuSeatDevices`); the joined pad walks its own cursor on the aircraft column, selects and confirms with A, and leaves with B while browsing; seat 0's mouse, keyboard or pad picks the map and the aircraft and presses FLY, which is its own confirmation. The original has no join gesture |
| Preferences (as Options) | Options | 14 rows | this page | **none** | layout only for the composition; `PREFERENCES.SCRIPT` says the four page doors deactivate the screen (the layout carries their targets) and the fifth button is `pf_b_mainmenu` out of flight (`activate(@mainmenu@)`) or `pf_b_returntogame` in flight. **Built as Original's Options screen over the section's chrome**: `PF_LOGO`, `PF_BACKGROUND`, `PF_T_TITLE`, the four description rows in their authored colour, the four page doors at their corners, `PF_B_GAMEOPTIONS` live in its four frames onto the Game Options page and the three others drawn disabled (no shared audio, video or controls option stands behind them), and `PF_B_MAINMENU` as the way back. The page carries no content of its own: the shared options moved onto the Game Options page its first door opens. Remake-only: the three disabled doors, a state the original never shows. `PF_T_GODESC`'s decoded words describe the original's page, not ours, and stay as authored. `PF_B_RETURNTOGAME` is the in-flight variant and is not drawn; the pause board is out of scope |
| (none: remake-only) | Mode's Build Custom Plane, the Instant Action pick's door | none | none | none | **Original's wallet-free hangar entry, of our design under Decision 11.** The Instant Action screen's `IA_B_BUILD`, whose edge the layout does not state, opens the decoded name screen as the cabin's `PC_B_PLANEX` edge does, over the saved-plane store with no wallet, and CANCEL or a purchase return to the Instant Action screen with its Pilot Plane list re-read; the top level carries no door of its own, the original reaching plane construction only from the cabin and this button. What the cabin path would supply and this entry cannot (the wallet, the profile's ownership) is left out rather than invented: the hub shows no cash note, prices are never checked against funds, and the hub's READY and CANCEL wear the export strips the Instant Action stills show (`PX_B_ReadyToExport`, `PX_B_CancelExport`), without their $50000 figure, whose enforcement is undecoded |
| GameOptions | Options | 13 rows | no | `GameOptions.png` | layout only for the composition; the script creates all 13 rows, sends the choices out on accept (`callback($$E$$, 2128, 1, …)`) and drops them on cancel (`callback($$E$$, 2110)`), and both plaques state `Preferences` as their edge. **Built as Original's Game Options page**, `PF_B_GAMEOPTIONS`' destination: `GO_BACKGROUND` and `GO_T_TITLE` at their corners, then the shared options in the section's own row shape (a title in the `TITLEX` column at the row's line, a control in the `DROPX` column, a description in the `DESCX` column, rows at the authored 62-pixel pitch from the first row's Y 283), with `GO_B_ACCEPTCHANGES` leaving as the options apply carrying both choices and `GO_B_CANCELCHANGES` returning to Preferences with them dropped. The page opens on the saved options and never writes them. Row one is the original's own "Difficulty", `IDS_GO_DIFFICULTY_TITLE` over the `GO_D_DIFFICULTY` dropdown reading Normal, Hard or Hardest (the three `IDS_DIFFICULTY` rows), described by `IDS_GO_DIFFICULTY_DESC` "Select the difficulty level for a solo campaign."; the setting reaches only an enemy vehicle's armour and health maxima at spawn. Remake-only are the two rows under it: row two is "Menu", a dropdown over the two shipped presentations reading ORIGINAL or BUILT-IN, described "Select the menu presentation."; row three is "Enhanced Graphics", a checkbox drawn from `PF_B_CheckBoxSmall.Png` at the head-turn box's own offset from its row (the row it is authored on) and taking the head-turn row's narrower title box, described "Select the lit world. Takes effect on the next start." while the choice matches the running mode and "This run is original; restart to apply." (or enhanced) while it does not, since the mode resolves once at launch and a saved word the world has not picked up would otherwise read as a failed switch. Two control kinds for the remake rows is deliberate: a second dropdown reading ORIGINAL would put back the word a player could not tell from the other. The page draws the Preferences page's `PF_LOGO`, its own section authoring no logo pane and the photograph showing one standing. **The plate holds three rows**: a fourth at the authored pitch would reach the plaque row at Y 457, so a further option past Difficulty needs either a taller plate (one image, so a stretched or tiled drawing is a remake reading to record) or paged rows |
| Audio | (none) | 19 rows | no | **none** | layout only; three preview loops are script-bound; `BL-455`. **Out of this plan**: `PF_B_AUDIO` draws disabled |
| Video | (none) | 31 rows | no | **none** | layout only. **Out of this plan**: `PF_B_VIDEO` draws disabled |
| ControlsPrefs | (none) | 12 rows | no | **none** | layout only. **Out of this plan**: `PF_B_CONTROLS` draws disabled, and Keys behind it is unreached |
| Keys | (none) | 17 rows | no | `Keybinds Movement/Throttle/Targeting/Weapons/Views 1/Views 2/Other.png` | composition of all seven tabs; no interaction. **Out of this plan** with ControlsPrefs |
| InstantAction | Environment, MissionType, Waves, WaveEdit, Wingmen, Plane, Presets | 48 rows | [`instant-action.md`](../formats/instant-action.md) | **none** | option sets, the ace's control hiding and the paged enemy rows are script-proven; the screen itself has never been seen. **Built as Original's Instant Action screen** from the section's rows over the shared feature: `IA_BackGround`, the `T` rows, the contents list in its 14-row window with its own scroll arrows and slider, the dropdowns on their authored lines, the enemy rows paged by `IA_B_UP`/`IA_B_DOWN`, the radio pair, View Story, Fly Mission and Exit; `IA_B_BUILD` opens the wallet-free hangar and `IA_B_CHANGEWEAPONS` the Weapon Loadout screen for the seat the radio pair names (both destinations remake readings, no layout row stating either edge). What the data does not settle is listed under Part 4 for `CAP-50` |
| OrdinanceLayout (as Instant Action's Weapon Loadout) | `WingmanLoadout`, the plane pane's loadout | 31 rows | same | same | **Built as Original's Instant Action loadout screen** over the same section: the ammunition fields for the airframe's firable gun slots and the rocket fields for its pylons (left column pylons 1 to 4, right 5 to 8), each over the stock table's option list, the two diagram panes on the airframe's frame, `OL_T_PLANEINFO` naming the fitted aircraft, the focused field's description in its pane, `OL_B_ACCEPT` keeping the picks and `OL_B_CANCEL` or Back restoring the picks the screen opened on. Remake-only: the original reaches this section from the flight check alone |
| Campaign (profile) | `CampaignScreen.Roster` | 6 rows | [`campaign-screens.md`](../formats/campaign-screens.md) | `Campaign Player Profile.png` | full: roster fill, name validator, all four exits. **Built as Original's profile screen** over the shared campaign feature and the shared board component: the name box pre-filled with the last player seated, `CM_B_START` / Enter in the box / a second click on the filled roster row starting, a first click filling the box, the list sub-script's own selection bar and pointer frame, the four refusals as the one-button messagebox, `CM_B_DELETEPLAYER` asking with langui 201 as the two-button box whose answers read Yes and No (langui 102 and 103, the words `MESSAGEBOX.SCRIPT` gives a `0x4` box), `CM_B_CANCEL` leaving. The box opens on Yes, the left button `MESSAGEBOX.SCRIPT` focuses for the plain `0x4` mask the campaign passes. Remake-only: the caret |
| PassengerCabin | `Cabin` | 16 rows | same | `Campaign CAP-44 Cabin.png` | full for the six buttons; whether anything on the screen animates is open. **Built as Original's cabin**: the four plaques the board component draws, `PC_B_NEWMISSION` disabled once the campaign is complete, `PC_B_PLANEX` into the name screen over the profile's wallet with the cabin as the hangar's return, `PC_B_PREVIOUS` and `PC_B_RETURNMM` on their layout edges |
| FlightCheck | `FlightCheck` | 25 rows | same | `Campaign Flight Check.png`, `… Change Plane Button.png` | full: both slots, four lists, the wingman gate, both plane-change rules. **Built as Original's flight check**: the paper plaques per crew slot hit-tested at their rows, `FC_B_RETURNBRIEF` rewinding a co-op walk, `FC_B_FLYMISSION` advancing to the next joined human's check or leaving as the feature's launch with every seat's devices; a joined seat drives its own check |
| PlaneSelection | `PlaneSelection` | 27 rows | same | `… Change Plane.png`, `… Combo Box.png`, `… Unique Warning.png`, `… Export dialog.png` | full, including the rollover preview and the duplicate rule. **Built as Original's plane selection** through the shared page: the fields at their `D` rows with the open list's entries hit-tested under the box, a sideways step on the closed field, the 710 refusal and the 702 export message as Original's own messagebox. Remake-only: the rollover preview is not drawn (the page previews nothing on a highlight) |
| OrdinanceLayout | `Ammo` | 31 rows | same | `… Change Ammo Menu.png`, `… ComboBox.png`, `Campaign Ammo Selection.png` | full. **Built as Original's ammo selection** through the shared page: the twelve fields, the description pane following the focused field, ACCEPT and CANCEL back onto the plaque that opened the screen |
| ScrapBook | `Scrapbook` | 46 rows | same + [`debrief.md`](debrief.md) | `Campaign Scrapbook CM01 Story Scraps.png` | full. **Built as Original's book** through the shared page: every scrap hit-tested on its `SCRAPBOOK.CSV` region (the picture's own bounds where the row authors `0,0,0,0`), the arrows, tabs, bookmark and buttons at their rows, Back returning to the contents or the cabin, whichever opened it |
| ScrapBook_TOC | `PreviousMissions` | 8 rows | same | `Campaign CAP-41 Previous Mission 1/2.png` | full. **Built as Original's table of contents** through the shared page: the mission rows hit-tested inside the listbox's window, a click picking and a second click replaying, the buttons at their rows |
| ScrapbookZoom | `ScrapbookZoom` | 83 rows | same | `Campaign Scrapbook CM02 Mission select….png` | full. **Built as Original's zoom** through the shared page: `SBZ_B_RETURN` and `SBZ_B_EXPORT`, the export's 705/706 as Original's messagebox |
| (Briefing, a `zrdr` dialog, not a script) | `Briefing` | none | [`briefing.md`](../formats/briefing.md) | `Campaign Briefing.png` | full. **Built as Original's briefing**: the three `brief_button1` plaques at the dialog's own positions with their label-face states, the reveal advanced on the presentation's clock, the narration begun through the shared audio service on entry and again on REPLAY BRIEFING and ended on RETURN TO CABIN and GO TO FLIGHT CHECK |
| Hangar | `HangarScreen.PlaneSelection` | 16 rows | [`hangar.md`](hangar.md) | `CustomPlane PlaneSelection.png` | callbacks and economy decoded; navigation only from the layout. **Built as Original's INVENTORY**, the hub's SELL PLANES destination (a remake reading: `PX_B_Sell` states no edge): `HA_BACKGROUND`, the title and prompt, `HA_D_PILOTPLANE` over the shared feature's saved planes, the picked plane's `HA_P_PILOTPLANE` frame, name, agility, armour, value (1258) and guns, `HA_B_SELLP` asking first with the two-button messagebox (langui 700 over the short airframe name and the plane's value, Yes and No) and refusing as the one-button box (704 for a reward aircraft, 701 below the two-plane floor) before selling or deleting through the feature, `HA_B_EXPORTP` drawn disabled, `HA_B_DONE` back to the tab. The sell messagebox is the plane selection screen's own decoded path (`campaign-screens.md`), applied here to the remake's inventory |
| PlaneName | `Name` | 8 rows | partly | **none** | the edit box's font and colours are authored; nothing about the validator here. **Built as Original's name screen**: `PN_E_NAME` fed by seat 0's typed characters under the shared feature's character set and 32-character cap, `PN_B_DEFAULT` (checked as authored) choosing the default configuration or a bare airframe, `PN_B_OK` live once a name stands with langui 203 shown until then, `PN_B_CANCEL` dropping the build; the original's `MaxChars` of 16 is not applied, the saved-plane index's 32 is |
| PlaneConstruction | (no counterpart; Built-in has no hub) | 47 rows | [`hangar.md`](hangar.md) | `Campaign CAP-40 Plane Construction 1/2.png` | composition; the tab bar's own behaviour is unseen. **Built as Original's Plane Construction hub**: the background, the plane at the `PX_P_PLANE` corner (the airframe's blueprint, or the `PX_ICON` set tinted with the picked colours), PLANE NAME, PLANE COST as the running total, AIRFRAME, WEIGHT CAPACITY, CURRENT WEIGHT, the agility and armour words with their `PX_BarGraph` segments, the cash note over a wallet only, the six `PX_Tab` tabs as siblings with the standing one disabled, `PX_B_Sell`, `PX_B_Ready` and `PX_B_Cancel`. What the data does not settle is listed under Part 4 for `CAP-53` |
| AirFrame | `Airframe` | 4 rows | [`hangar.md`](hangar.md) | none | the airframe stat table is decoded; the screen is four widgets over it. **Built as the hub's airframe tab**: `AF_D_AIRFRAME` picking through the shared feature and raising the langui 206 ask as a dialog, `AF_T_AIRFRAME` and `AF_S_AIRFRAMEDESC` following the focused airframe |
| Engine | `Engine` | 4 rows | same | none | as above. **Built as the engine tab** over `EN_D_ENGINE` (six engines then None) |
| Armor | `Armour` | 14 rows | same | none | as above. **Built as the armor tab** over `AR_D_POINT0..3` (None then 5 to 60 units in fives) |
| Guns | `Guns` | 14 rows | same | none | as above. **Built as the guns tab** over `GN_D_GUN0..3` (the eleven-row cycle) with the airframe's slot titles |
| HardPoints | `Hardpoints` | 9 rows | same | none | as above. **Built as the hardpoints tab** over `HP_D_POINT0..1` |
| Paint | `Paint` | 27 rows | same + [`rof.md`](../formats/rof.md) | 8 `CustomPlane Paint*.png` | composition well covered by the paint stills; the pattern pickers' behaviour is not. **Built as the paint tab**: `PT_D_PATTERN` over the wearable patterns, `PT_D_COLORS0..2` as swatches in an 18-row window, `PT_D_SHADES0..2`, `PT_D_DECALS0..2` as `PT_P_DECALS` tiles in a two-row window |
| Purchase | `Purchase` | 29 rows | same | none | the purchase gate and cost are decoded from the executable. **Built as the hub's totals page**, READY TO PURCHASE's destination: the column heads, one line per priced component at the authored lines and text lists, the totals, `PUR_T_PROBLEMS` in the commit's words, `PUR_B_PURCHASE` live while the feature can commit |
| MessageBox | (the `_error` line) | 16 rows | [`campaign-screens.md`](../formats/campaign-screens.md) | two campaign dialogs | the `@globals@OR` parameter block and the button masks are decoded; the script also gives the buttons their words by mask, langui 100 (OK) on the `0x1` box and 102 and 103 (Yes, No) on the `0x4` pair, and its Escape takes the `0x4` box's right answer. **Built as Original's dialog over any screen**: the `0x1` box on `MB_B_CENTER` for every refusal and notice (the profile screen's four, the plane screen's 710 and 702, the zoom's 705 and 706, the inventory's 701 and 704), the `0x4` box on `MB_B_LEFT`/`MB_B_RIGHT` for the delete confirm and the sell confirm, the answers in the script's words, the answer under the pointer in its rollover frame, a box opening on its first answer (the left button the script focuses, so a `0x4` box opens on Yes), Back taking the declining answer; while a box stands its answers are the only rows |

⚠ **Built-in has no counterpart for GameOptions, Audio, Video, ControlsPrefs, Keys or
PlaneConstruction (its Options screen is Preferences' counterpart), and the original has no
counterpart for Free Flight, Dogfight, the Chapter screen or the wave editor.** The two shells are
not the same graph, which is what Decision 4 already allows; what this row adds is the size of it.

### Menu audio

`LAYOUT.CSV` authors no audio. Every menu sound is bound in a script, and the whole set is eight
files in `ASSETS/SOUNDS/`.

| File | Bound in | Role |
|---|---|---|
| `MOUSECLICK.WAV` | `GLOBALS.SCRIPT`, played by `CTL.SCRIPT` on a click | button press |
| `MOUSEOVER.WAV` | same, on a rollover | button rollover |
| `ENTERTEXT.WAV` | `GLOBALS.SCRIPT` | edit-box keystroke |
| `ENTERTEXT_ERROR.WAV` | same | edit-box reject |
| `MUSIC_SPLASH.WAV` | `GLOBALS.SCRIPT`'s one `@ctl@SK` sound object, channel 5, continuous | the shell's only music; every out-of-mission screen drives that one object |
| `MUSIC_LOOP.WAV` | `AUDIO.SCRIPT` | the Audio page's music-volume preview |
| `SFX_LOOP.WAV` | same | the effects-volume preview |
| `VOICE_LOOP.WAV` | same | the voice-volume preview |

Transport is message-driven: `11001` play, `11002` loop, `11000` stop, `11003`/`11004` pause and
resume through `uiControl` 2503/2504. There is no cue-name indirection; scripts name raw `.wav`
files.

**Built-in plays none of these.** `LaunchMenu` owns exactly two audio behaviours: it enters
`MusicState.Menu` on every screen, and it asks the shared audio service for the briefing narration,
which ducks the music. There is no click, no rollover and no keystroke sound anywhere in Built-in.
**Original requests the first four through the shared service** (`MenuCueTable`): `MOUSEOVER` on
entering a live button, `MOUSECLICK` on pressing one, `ENTERTEXT` per character an edit box takes
and `ENTERTEXT_ERROR` per character it refuses, on the top level, the Instant Action screen, the
hangar and the campaign screens alike, and nothing on a list row or a scrap; the briefing's
narration is begun on entry and on REPLAY BRIEFING and ended on every door out. Which sound the
original plays where is `CAP-52`'s to confirm.

## Part 3: assets, required and optional

**Required** means a screen Original composes cannot be drawn without it; **optional** means the
screen degrades locally and stays usable. The runtime manifest
(`CSVM/src/UI/Menu/Original/OriginalAssetManifest.cs`) derives that classification from the decoded
layout on every start, so this section is the reading and the manifest is the enforcement. Over the
install's own layout it classifies **99 required** and **63 optional** files; the two optional
absences it reports are `CrimFlag.MPG` and `Final.MPG`.

⚠ **The manifest cannot be generated from `LAYOUT.CSV` alone.** The layout names 124 distinct art
files; the archive ships 681 PNG, 143 TGA, 107 JPG and 25 TIF. The rest are named by scripts
(`ACTIVEPOINTERZ.PNG`, `PASSIVEPOINTERZ.PNG`, `ARIAL8.TGA`) or built at runtime by string
concatenation (`"assets\graphics\pc_p_hangar" + <airframe> + ".jpg"`,
`"assets\graphics\scrapbook\" + <name>`, and the per-pattern `.BM` sets). `menu_layout.json`
carries both halves, complete names and the fragments a runtime name is assembled from, and the
manifest classifies the complete names alone. `ZOOMPOINTER.PNG` and `PX_<n>_BLUEPRINT.TGA` appear
in neither half: see the closing section. Of the layout's own names, 101 are in the 23 sections
Original composes and 27 only outside them; seven of the 101 sit in rows no composed screen draws,
which is why the required count is 94 layout names plus the five the scripts name.

| Class | Files | Manifest class | Notes |
|---|---|---|---|
| `menu_layout.json` (from `LAYOUT.CSV`) | 1 | **required**, ahead of the manifest | absent means no geometry at all, and no manifest to derive; the availability check answers with the missing path |
| `extracted/VERSION.json` | 1 | **required** when it is behind the stamp schema Original reads | a tree with no stamp still runs; the check is `ExtractionStamp.Behind` |
| screen backgrounds of the 23 composed sections (`MM_`, `PF_`, `GO_`, `IA_`, `CM_`, `PC_`, `FC_`, `PS_`, `OL_`, `SB_`, `PX_`, `MB_`) | 12 families | **required** | a screen with no backdrop is not a degraded screen |
| button strips of the composed sections | 122 `B` rows over ~62 PNGs | **required** | four stacked frames, disabled / normal / rollover / depressed; the words are painted in, so the state is entirely which frame draws |
| shared widget chrome (`GN_`, `FC_B_Scroll*`, `PX_B_Scroll*`, `CM_B_Scroll*`, `IA_B_Scroll*`, the dropdown arrows) | ~12 | **required** | named through `[GLOBALVARS]` macros, so one miss hits many screens |
| `ARIAL8.TGA` | 1 | **required** | the global 3D font; the string table's `[FONTID]` tags name `.ttf` faces the archive does not carry, and `FONT.TGA` is named by nothing |
| `ACTIVEPOINTERZ.PNG`, `PASSIVEPOINTERZ.PNG` | 2 | **required** | script-named and drawn on every screen; Built-in draws no pointer at all (`BL-654`) |
| `PX_B_ReadyToExport.png`, `PX_B_CancelExport.png` | 2 | **required** | script-named, drawn by the plane construction hub; no layout row names them |
| `ZOOMPOINTER.PNG` | 1 | not classified | named in neither the layout nor an external-asset entry, and Original draws the two pointers above on the zoom too |
| `GO_BackGround.png`, `PF_B_AcceptChanges.png`, `PF_B_CancelChanges.png`, `PF_B_CheckBoxSmall.Png` | 4 | **required** | the Game Options page draws every art name its section carries; the dropdown's arrow and scroll set are the shared chrome above |
| `AP_`, `VP_`, `CP_`, `KB_` page art | 4 families | optional | the four remaining Preferences pages are out of scope, so no composed screen draws them |
| `GN_B_ReturnToGame.Png` | 1 | optional | `[Preferences]`' in-flight way back; the menu's page offers `PC_B_ReturnMainMenu.png` instead |
| `MessageBox`'s `MP_*` and `MA_*` rows, and the art of every section outside the 23 | 3 + 27 | optional | no local counterpart for the multiplayer error box, the About box, or a screen Original composes nothing from |
| `ui_strings.json` + `RESOURCE.H` | 2 | optional | 152 symbols referenced, 149 resolve; `UiStrings` falls back to an empty table, so the screens draw with no words rather than not at all |
| `SCRAPBOOK.CSV` | 1 | optional | 461 rows; the book's extent is the file's extent, and without it the book lists nothing |
| `MOUSECLICK`, `MOUSEOVER`, `ENTERTEXT`, `ENTERTEXT_ERROR` | 4 | optional | a silent menu is usable |
| `MUSIC_SPLASH.WAV`, `MUSIC_LOOP`, `SFX_LOOP`, `VOICE_LOOP` | 4 | optional (the loops are not classified) | the three loop names carry no directory in the scripts, so the manifest has no path to check |
| `PC_P_HANGAR0..10.JPG` | 11 | not classified | assembled at runtime from a fragment; the cabin's window photo, and the painted cabin reads without it |
| `PX_0..10_BLUEPRINT.TGA` | 11 | not classified | assembled at runtime; `IHangarPage.Art` already returns null on a miss |
| scrapbook art | 238 files | not classified | assembled at runtime; a `Snap_*` row is already skipped when the file is not on disk |
| paint `.BM` masks | 184 | not classified | assembled at runtime per pattern; 14 pattern folders, `FORTUNE` has all 62 skins, `BLCKSWAN` 5 |
| `CrimFlag.MPG`, `Final.MPG` | 2 | optional, and absent | `LAYOUT.CSV` names both and `extracted/rof/ASSETS/GRAPHICS/MPG/` is empty; no screen plays a movie, so they are the two absences every start reports (`BL-446`) |
| `MM_BackGround.png`, `MM_SplashBackground.jpg` | 2 | not classified | neither appears in any layout row, and the shipped `MM_BackGround.png` is a placeholder reading "CS BACKGROUND" |

**Not classified** means the manifest carries no entry: the name is assembled at runtime from a
fragment rather than written out, so there is no file list to check before entry. Those degrade
where they are drawn, and the pages that draw them already handle a miss.

⚠ **The original's own top-level backdrop is a movie the extraction does not carry, and the still
beside it is a placeholder.** Original's main menu therefore has no faithful background
available. That is an accepted remake-only rule to write down, not a missing asset to hunt for.

## Part 4: what the evidence does not cover

### Owed captures

Five minted, filed in [`playtest.md`](../../playtest.md) under **Menus and front end**.

| ID | What it films | What it unblocks |
|---|---|---|
| `CAP-49` | the main menu: composition, the flag movie, rollover and press, music, and what Quit does | E41's Original top level, E42's main-menu asset rules, `BL-654` |
| `CAP-50` | the Instant Action setup page: every drop-down open, the paged enemy rows, the ace hiding every enemy control, the Table of Contents and View Story | C21, B13, and the whole wizard's fidelity target |
| `CAP-51` | Preferences and its four leaves, each opened and changed | A3's options store, E41's Options route, `BL-570`, `BL-455` |
| `CAP-52` | menu audio and pointer behaviour across screens | A4's audio and input contracts, D33's cue work |
| `CAP-53` | the Plane Construction tab bar driven between its six screens | C23's Original hangar navigation |

### Where Built-in and the original disagree

Each of these is a divergence a reader could mistake for a decode, so each is named.

- **The original's top level has six rows, and none of them is Free Flight, Dogfight or a hangar
  door.** `[@MainMenu@]` authors Campaign, Instant Action, Multiplayer, Preferences, Credits and
  Quit. Built-in's Mode screen offers Free Flight, Instant Action, Dogfight, Campaign and Build
  Custom Plane. Free Flight is entirely ours; Dogfight is splitscreen where the original's
  equivalent is network multiplayer; Original reaches plane construction where the original does,
  from the Instant Action screen's BUILD button and from the cabin, and has no top-level door.
- **Original Free Flight has no original screen behind it.** There is no free-flight script, no
  free-flight art, and no free-flight entry in the Instant Action mission-type dropdown. The
  remake-only rule under Decision 11 stands: Original's top level carries a Free Flight door and a
  Free Flight screen of our design, composed in the decoded chrome's conventions (the paper-plaque
  text button, the four state colours, the logo) and marked remake-only in the screen census above.
- **Original Dogfight and its join flow have no original screen behind them either.** The
  original's Multiplayer branch is network play (the 22 multiplayer scripts, none with a layout),
  and no script or layout row describes a second local player joining. The same remake-only rule
  gives Original a Dogfight door under the Free Flight door, a Dogfight screen in the Free Flight
  screen's shape, and Built-in's own join gesture (Start on a free pad) with the seats, picks and
  gate shared through the player-setup feature; all marked remake-only in the census above.
- **Original's wallet-free hub entry and its Instant Action loadout screen have no original screen
  behind them.** The original reaches plane construction from the cabin (`PC_B_PLANEX`, to
  `PlaneName`) and from the Instant Action screen's `IA_B_BUILD`, whose edge no layout row states,
  as no row states `IA_B_CHANGEWEAPONS`'s; the remake-only rule under Decision 11 has `IA_B_BUILD`
  open the same name screen with no wallet, the hub then wearing the export strips the Instant
  Action stills show and no cash note, and `IA_B_CHANGEWEAPONS` open the `[@OrdinanceLayout@]`
  chrome over the pilot's or the wingmen's shared fit. Marked remake-only in the census above.

### What the Original presentation implements from the data, and what the captures must confirm

The main menu's interaction is built from what the layout itself evidences: a `B` row's four
stacked frames imply a rollover state under the pointer and a depressed state while pressed, so
the plaque under the pointer draws frame 2 and draws frame 3 while the button is held; the two
pointer bitmaps the globals script names are drawn as the pointer, the active one over a live
button and the passive one elsewhere; the two wavs the control library binds play on a rollover
and on a press. Everything else is remake-only until filmed. `CAP-49` must confirm, for the top
level: whether the movie (absent from the extraction) loops behind the buttons and what replaces
it, whether the rollover frame appears on entering the plaque or on a delay, whether a press fires
on the button-down or on the release, whether a disabled button ever draws, where `mm_t_title`
sits, that Quit ends the game on the press with no confirm (`MAINMENU.SCRIPT` terminates on
`mm_b_quit`, which is what Original does), and whether any keyboard or pad focus exists at all
(Original's keyboard and pad focus is a remake equivalence, not a decode). `CAP-51` must confirm,
for the Preferences page itself: what the page shows before any door is pressed (Original draws the
four doors, their four descriptions and RETURN TO MAIN MENU together, the layout placing them on
one panel), whether a description follows the pointer or stands (Original draws all four at once),
which button leads out of the page from the top level (Original draws `PF_B_MAINMENU`, the button
the script picks out of flight), and whether the movie plays behind the page (Original's place for
it is black, as on the top level). The chooser row is ours and the four doors' disabled frames are
a state the original never shows. `CAP-52` must confirm, for the pointer and audio: which sound
plays on a rollover and which on a press, whether a disabled button makes either, whether list rows
make a rollover sound (Original plays none on a list row), which pointer bitmap shows over a
button, over a list and over nothing, and where each bitmap's hotspot is (Original draws the
bitmap's top-left at the pointer).

The campaign screens are built from what the layout, the scripts and the briefing dialog evidence:
every `B` row's position and four-frame strip, the `D` rows' boxes, the listbox's window, the
scrapbook rows' region column, the two messagebox button sets and their rows, the roster
sub-script's two colours, the edit box's pre-fill and its two bound wavs, NEXT MISSION disabled
once the campaign is complete, the briefing's narration following its script's start count.
Everything else is remake-only until filmed. `CAP-52` must confirm, for the campaign: whether a
campaign plaque (a cabin button, a briefing plaque, a paper button) plays the rollover and click
sounds a main-menu button does (Original plays both) and whether a roster row, a mission row of the
table of contents or a scrap plays anything on entry (Original plays nothing); what the edit box's
keystroke and reject sounds are attached to (Original plays `ENTERTEXT` per taken character and
`ENTERTEXT_ERROR` per refused one in the profile screen's box); which pointer bitmap shows over a
scrap (Original draws the passive one, as over a list row); whether REPLAY BRIEFING restarts the
narration from its start (Original begins the wav again from the top) and whether leaving the
briefing by RETURN TO CABIN or GO TO FLIGHT CHECK cuts the voice (Original ends it and lifts the
music duck); whether a mission's end starts any narration on the book (Original starts none);
whether a scrap whose region column reads `0,0,0,0` is clickable on its picture (Original hit-tests
the picture's bounds); and whether any
keyboard or pad focus exists on these screens at all, and where Back lands
(Original walks each screen back to the one that opened it and keeps the focus on the plaque that
opened what is being left, a remake equivalence).

The Instant Action screen is built from what the layout and the script evidence: the widgets at
their authored positions over `IA_BackGround`, the contents list's 14-row window with its own
`UpArrow`/`DownArrow`/`Slider` art, a dropdown's box `Width` wide and `ItemHeight` high showing its
picked value with the `DropDown` arrow strip, the enemy rows on two pages keyed off `IA_B_UP`/
`IA_B_DOWN` (the script's mailbox 20002), the ace duel hiding every enemy dropdown (the script's
`0 == WT` branch), the wingman plane hidden at zero wingmen, a changed militia resetting its
aircraft (`AV[BA].QG = 0`), stunt flying clearing the clouds (`FUN_004103b0`'s mask), a contents
row applying its preset on select (callback 2302) and View Story writing the preset's name as the
story title (the `IDS_IA_STORYTITLE` format). Everything else is remake-only until filmed. `CAP-50`
must confirm: whether the contents list and the dropdown half show together or View Story reveals
the right half (Original shows both, the layout placing them on disjoint halves of one background);
what a rollover previews, if anything (Original previews nothing: a contents row changes the fields
only when selected); whether a changed militia visibly resets its aircraft field (Original resets
it to the militia's first aircraft); what BUILD CUSTOM PLANE does from this screen (Original draws
it disabled); how the screen reads before anything is chosen (Original opens on an airfield, the
ace duel, an Autogyro, no wingmen, one life and no contents row selected, the screen's stored
defaults); whether an opened dropdown lists below its box and how many rows it shows (Original
lists every item below the box, and the layout's `TotalDisplayed` is read as the window); whether
the Wingmen dropdown stays under the ace duel (Original keeps it, the script hiding only the enemy
controls, and the parser forces the count to zero at launch); whether the up/down buttons and the
"[continued ...]" text hide under the ace duel (Original hides them with the enemy rows); whether
the Player/Wingman radio hides at zero wingmen and what WEAPON LOADOUT opens (Original keeps the
radio and draws the button disabled); and whether keyboard or pad input reaches the screen at all
(Original walks the widgets in two columns and steps a dropdown's value sideways, a remake
equivalence).

The plane-construction screens are built from what the layout and the stills evidence: the
`[@PlaneConstruction@]` chrome and the six tab sections at their authored positions over
`PX_BackGround`, the seven `0x1100` edges as a tab bar (every tab a sibling, `PX_B_Ready` to
Purchase), the `PX_Tab` strip's four frames and its `ColorDisabled`/`ColorActive` label tail, a
dropdown's box `Width` wide and `ItemHeight` high with `TotalDisplayed` as its window and its
`DropUp`/`DropDown` arrows, the `PX_ICON_<airframe>_<pattern>_0..3` layers as the plate and three
region masks (the paint composite the stills show), `PT_P_DECALS`' fifty frames as the decal
tiles, the cabin's `PC_B_PLANEX` edge to `PlaneName` first, the `PN_B_DEFAULT` box authored
checked, langui 203 as the name screen's refusal and 206 as the airframe-switch ask, and the
economy and paint tables (`hangar.md`). Everything else is remake-only until filmed. `CAP-53` must
confirm: whether a tab is ever disabled (Original disables only the standing tab, drawn in its
`ColorDisabled` frame); where the running total shows (Original writes it into `PX_T_PLANECOST`
over the blueprint and updates it on every pick); whether leaving a tab commits or the whole build
commits at Purchase (Original commits only on `PUR_B_PURCHASE`, and CANCEL drops every pick); what
SELL PLANES does (Original opens the `[@Hangar@]` INVENTORY with Sell, a disabled Export and Done
back to the tab); what Load Default Configuration loads (Original starts the build on the
Devastator with its stock engine, guns, hardpoints and armour when checked, on a bare airframe when
cleared); whether the airframe switch asks with string 206 as a message box (Original raises it as
a dialog over the page with OK and Cancel); how an opened dropdown lists and scrolls (Original
lists under the box in the row's `TotalDisplayed` window with the list's own arrows); whether the
Instant Action path's hub reads READY TO EXPORT and shows `$$$ on $50000` (Original wears the
export strips on the wallet-free door and shows no figure, since no decode says what the $50000
gates); and whether keyboard or pad input reaches the hub at all (Original walks the page's rows
then the tab bar and steps a dropdown's value sideways, a remake equivalence).
- **Built-in's Chapter screen is ours.** The original picks a map through Instant Action's
  environment dropdown; there is no standalone chapter list.
- **The original's plane construction is a tab bar; Built-in's is a linear nine-screen walk.**
- **The cabin's Plane Construction button goes to `PlaneName` first.**
- **Built-in is not asset-independent today.** The milestone's first bullet says Built-in requires no
  extracted menu artwork, and the hangar already loads `extracted/rof/ASSETS/GRAPHICS/PX_<n>_BLUEPRINT.TGA`
  while every campaign screen is a composed board over `extracted/rof/ASSETS/GRAPHICS` and
  `extracted/rimage`. Both degrade rather than fail, so the accurate claim is that Built-in stays
  *usable* without the artwork, not that it does not use it. A3's fallback wording depends on which
  of the two the milestone means.
- **Built-in plays no menu sounds.** The original has four UI wavs plus the splash track.
- **Built-in takes no mouse input.** Every launchscreen `Control` is `MouseFilterEnum.Ignore`
  (`BL-654`), where the original's layout gives every button a rollover and a depressed colour and
  its listboxes frame the row under the pointer.

## The decoded layout

`ExtractRof.ps1` emits `extracted/rof/menu_layout.json`: the 34 sections bound to their scripts,
both macro mechanisms resolved, every type's field order, the 46 `ScriptToExe` edges, the button
colour tail and frame counts, the `ResID` → `RESOURCE.H` → string join with the symbol kept beside
the text, each screen's script-created keys, the art the scripts name outside the layout,
`SCRAPBOOK.CSV`, and the patch overlay's precedence.
[`formats/menu-layout.md`](../formats/menu-layout.md) is the description of record for all of it,
including what stays ambiguous. Four readings this page carried before that decode were wrong and
are corrected above and here:

- **No macro in the shipped file is unresolved.** `<GX>`, `<Y>`, `<V2>`, `<V3>` and `<V4>` are
  ordinary per-section `V<n>` definitions, and their values agree with
  [`campaign-board.md`](campaign-board.md)'s screenshot measurements to within a pixel.
- **`ScriptPri` takes `0x1000`, `0x1020` and `0x1100`.** There is no `0x1fff` anywhere in the file;
  `0x1100` is exactly the seven `PlaneConstruction` tab-bar edges.
- **149 of the 152 `IDS_*` symbols resolve**, not 148. `RESOURCE.H` aliases three ids to two
  symbols each and `ui_strings.json` keeps only one symbol per id, so a join through that field
  loses `IDS_IA_CONTINUED`; joining through `RESOURCE.H` resolves it. The three that genuinely do
  not resolve are `IDS_GN_1P/2P/3P`, which `RESOURCE.H` does not define.
- **The layout names 124 art files, not 122.** The two the earlier count missed are the volume
  slider's own `PF_B_SliderSlot.png` and `PF_B_Slider.png` on the four `Z` rows.

⚠ **`ZOOMPOINTER.PNG` and `FONT.TGA` are named by neither the layout nor any script.** Part 3 lists
them among the script-named assets; the scripts name `ACTIVEPOINTERZ.PNG`, `PASSIVEPOINTERZ.PNG`
and `ARIAL8.TGA` only. Both files ship, so something names them, but it is not the shipped data:
E42 must treat them as chosen by us until a reader is found.
