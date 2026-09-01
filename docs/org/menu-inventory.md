# The menu inventory: every in-scope screen, transition and asset

The authoritative census behind [`PLAN-menu-presentations.md`](../PLAN-menu-presentations.md): what
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
| Original screens in this plan's scope | **27** of the 34 single-player screens |
| Layout-stated navigation edges in the original | **46** |

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
| `campaign-planeselection` | plane selection | `Campaign` |
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

### Screen by screen

Composition means an authored rectangle, art name or string id. Interaction means a decoded
behaviour: what a press does, what a rollover changes, what is disabled when.

| Original screen | Built-in counterpart | Layout | Script decoded | Capture | Interaction evidence |
|---|---|---|---|---|---|
| MainMenu | Mode | 9 rows | this page | **none** | composition only; script text gives the six buttons and their targets, nothing about rollover, music or the flag movie. **Built as Original's top level** from the two panes and the six `B` rows with their four-frame strips; rows with no remake destination yet draw the disabled frame, Quit and Preferences (the Options door) react, and the movie's place is black |
| (none: remake-only) | Free Flight, Chapter, Plane | none | none | none | **Original's Free Flight door and screen, of our design under Decision 11.** The door is a text button in the `FC_B_CHANGEPLANE` paper-plaque convention beside the button frame, level with Campaign; the screen is the logo over two text lists (the shared chapter roster and the shared aircraft roster, the eleven stock airframes then the saved custom planes in an eleven-row window) with BACK and FLY plaques, a seat strip under the chapters and each later seat's tag on its aircraft row. Nothing on it is decoded |
| (none: remake-only) | Dogfight, Chapter, Plane | none | none | none | **Original's Dogfight door and screen, of our design under Decision 11.** The door sits under the Free Flight door in the same plaque convention; the screen is the Free Flight screen's shape over the Dogfight gate (a second seat must join and confirm before FLY stands). The original's Multiplayer is network play and ships no split-screen Dogfight, so nothing here is decoded |
| (none: remake-only) | the join strip and per-seat picks | none | none | none | **Original's join flow, of our design.** Start on an unclaimed pad joins a seat on either sortie screen (the same gesture and pad bookkeeping as Built-in's, through `MenuSeatDevices`); the joined pad walks its own cursor on the aircraft column, selects and confirms with A, and leaves with B while browsing; seat 0's mouse, keyboard or pad picks the map and the aircraft and presses FLY, which is its own confirmation. The original has no join gesture |
| (none: remake-only) | Options | none | none | none | **Original's minimal Options screen**, opened by the Preferences row until the decoded Preferences screens exist: the presentation toggle, APPLY, BACK, in the same plaque convention |
| Preferences | (none; Options is E41's addition) | 14 rows | no | **none** | layout only |
| GameOptions | (none) | 13 rows | no | **none** | layout only; `BL-570` wants the difficulty row |
| Audio | (none) | 19 rows | no | **none** | layout only; three preview loops are script-bound; `BL-455` |
| Video | (none) | 31 rows | no | **none** | layout only |
| ControlsPrefs | (none) | 12 rows | no | **none** | layout only |
| Keys | (none) | 17 rows | no | `Keybinds Movement/Throttle/Targeting/Weapons/Views 1/Views 2/Other.png` | composition of all seven tabs; no interaction |
| InstantAction | Environment, MissionType, Waves, WaveEdit, Wingmen, Plane, Presets | 48 rows | [`instant-action.md`](../formats/instant-action.md) | **none** | option sets, the ace's control hiding and the paged enemy rows are script-proven; the screen itself has never been seen |
| Campaign (profile) | `CampaignScreen.Roster` | 6 rows | [`campaign-screens.md`](../formats/campaign-screens.md) | `Campaign Player Profile.png` | full: roster fill, name validator, all four exits |
| PassengerCabin | `Cabin` | 16 rows | same | `Campaign CAP-44 Cabin.png` | full for the six buttons; whether anything on the screen animates is open |
| FlightCheck | `FlightCheck` | 25 rows | same | `Campaign Flight Check.png`, `… Change Plane Button.png` | full: both slots, four lists, the wingman gate, both plane-change rules |
| PlaneSelection | `PlaneSelection` | 27 rows | same | `… Change Plane.png`, `… Combo Box.png`, `… Unique Warning.png`, `… Export dialog.png` | full, including the rollover preview and the duplicate rule |
| OrdinanceLayout | `Ammo` | 31 rows | same | `… Change Ammo Menu.png`, `… ComboBox.png`, `Campaign Ammo Selection.png` | full |
| ScrapBook | `Scrapbook` | 46 rows | same + [`debrief.md`](debrief.md) | `Campaign Scrapbook CM01 Story Scraps.png` | full |
| ScrapBook_TOC | `PreviousMissions` | 8 rows | same | `Campaign CAP-41 Previous Mission 1/2.png` | full |
| ScrapbookZoom | `ScrapbookZoom` | 83 rows | same | `Campaign Scrapbook CM02 Mission select….png` | full |
| (Briefing, a `zrdr` dialog, not a script) | `Briefing` | none | [`briefing.md`](../formats/briefing.md) | `Campaign Briefing.png` | full |
| Hangar | `HangarScreen.PlaneSelection` | 16 rows | [`hangar.md`](hangar.md) | `CustomPlane PlaneSelection.png` | callbacks and economy decoded; navigation only from the layout |
| PlaneName | `Name` | 8 rows | partly | **none** | the edit box's font and colours are authored; nothing about the validator here |
| PlaneConstruction | (no counterpart; Built-in has no hub) | 47 rows | [`hangar.md`](hangar.md) | `Campaign CAP-40 Plane Construction 1/2.png` | composition; the tab bar's own behaviour is unseen |
| AirFrame | `Airframe` | 4 rows | [`hangar.md`](hangar.md) | none | the airframe stat table is decoded; the screen is four widgets over it |
| Engine | `Engine` | 4 rows | same | none | as above |
| Armor | `Armour` | 14 rows | same | none | as above |
| Guns | `Guns` | 14 rows | same | none | as above |
| HardPoints | `Hardpoints` | 9 rows | same | none | as above |
| Paint | `Paint` | 27 rows | same + [`rof.md`](../formats/rof.md) | 8 `CustomPlane Paint*.png` | composition well covered by the paint stills; the pattern pickers' behaviour is not |
| Purchase | `Purchase` | 29 rows | same | none | the purchase gate and cost are decoded from the executable |
| MessageBox | (the `_error` line) | 16 rows | [`campaign-screens.md`](../formats/campaign-screens.md) | two campaign dialogs | the `@globals@OR` parameter block and the button masks are decoded |

⚠ **Built-in has no counterpart for Preferences, GameOptions, Audio, Video, ControlsPrefs, Keys or
PlaneConstruction, and the original has no counterpart for Free Flight, Dogfight, the Chapter
screen or the wave editor.** The two shells are not the same graph, which is what Decision 4
already allows; what this row adds is the size of it.

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
`MusicState.Menu` on every screen, and it plays the briefing narration itself while ducking the
music. There is no click, no rollover and no keystroke sound anywhere in the menu today, so A4's
audio contract and D33's cue work start from nothing rather than from a rewiring.

## Part 3: assets, required and optional

For E42. **Required** means the screen cannot be drawn or read without it; **optional** means the
screen degrades locally and stays usable.

⚠ **The manifest cannot be generated from `LAYOUT.CSV` alone.** The layout names 124 distinct art
files; the archive ships 681 PNG, 143 TGA, 107 JPG and 25 TIF. The rest are named by scripts
(`ACTIVEPOINTERZ.PNG`, `PASSIVEPOINTERZ.PNG`, `ARIAL8.TGA`) or built at runtime by string
concatenation (`"assets\graphics\pc_p_hangar" + <airframe> + ".jpg"`,
`"assets\graphics\scrapbook\" + <name>`, and the per-pattern `.BM` sets). `menu_layout.json`
carries both halves, complete names and the fragments a runtime name is assembled from, which is
what E42 validates against. `ZOOMPOINTER.PNG` and `PX_<n>_BLUEPRINT.TGA` appear in neither half:
see the closing section.

| Class | Files | Required or optional | Notes |
|---|---|---|---|
| `LAYOUT.CSV` | 1 | **required** for every Original screen | absent means no geometry at all |
| `ui_strings.json` + `RESOURCE.H` | 2 | **required** | 152 symbols referenced, 149 resolve; `IDS_GN_1P/2P/3P` are dead layout references |
| `SCRAPBOOK.CSV` | 1 | **required** for ScrapBook and ScrapbookZoom, optional elsewhere | 461 rows; the book's extent is the file's extent |
| screen backgrounds (`MM_`, `PF_`, `GO_`, `AP_`, `VP_`, `CP_`, `KB_`, `IA_`, `CM_`, `PC_`, `FC_`, `PS_`, `OL_`, `SB_`, `PX_`, `MB_`) | 16 families | **required** per screen | a screen with no backdrop is not a degraded screen |
| button strips | 119 `B` rows over ~60 PNGs | **required** per screen | four stacked frames, disabled / normal / rollover / depressed; the words are painted in, so the state is entirely which frame draws |
| shared widget chrome (`GN_`, `FC_B_Scroll*`, `PX_B_Scroll*`, `CM_B_Scroll*`, the dropdown arrows) | ~12 | **required** wherever a list or combo is drawn | named through `[GLOBALVARS]` macros, so one miss hits many screens |
| `ARIAL8.TGA`, `FONT.TGA` | 2 | **required** | the global 3D font; the string table's `[FONTID]` tags name `.ttf` faces the archive does not carry |
| `ACTIVEPOINTERZ.PNG`, `PASSIVEPOINTERZ.PNG`, `ZOOMPOINTER.PNG` | 3 | **required** for a mouse-first presentation | Built-in draws no pointer at all today (`BL-654`) |
| `MOUSECLICK`, `MOUSEOVER`, `ENTERTEXT`, `ENTERTEXT_ERROR` | 4 | optional | a silent menu is usable |
| `MUSIC_SPLASH.WAV` | 1 | optional | |
| `MUSIC_LOOP`, `SFX_LOOP`, `VOICE_LOOP` | 3 | optional | Audio page previews only |
| `PC_P_HANGAR0..10.JPG` | 11 | optional | the cabin's window photo; the painted cabin reads without it |
| `PX_0..10_BLUEPRINT.TGA` | 11 | optional | the hangar's art column; `IHangarPage.Art` already returns null on a miss |
| `FC_PlaneIcons.png`, `SB_killMARKERcombined.png` and the other frame strips | ~6 | optional | a missing silhouette leaves a hole, not a broken screen |
| scrapbook art | 238 files | optional per scrap | a `Snap_*` row is already skipped when the file is not on disk |
| paint `.BM` masks | 184 | **required** for the Paint screen, optional elsewhere | 14 pattern folders; `FORTUNE` has all 62 skins, `BLCKSWAN` 5 |
| `CrimFlag.MPG`, `Final.MPG` | 2 | **absent** | `LAYOUT.CSV` names both and `extracted/rof/ASSETS/GRAPHICS/MPG/` is empty; `BL-446` |
| `MM_BackGround.png`, `MM_SplashBackground.jpg` | 2 | **unreferenced** | neither appears in any layout row, and the shipped `MM_BackGround.png` is a placeholder reading "CS BACKGROUND" |

⚠ **The original's own top-level backdrop is a movie the extraction does not carry, and the still
beside it is a placeholder.** E41's Original main menu therefore has no faithful background
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
  equivalent is network multiplayer; the hangar's top-level door is ours, since the original reaches
  plane construction from the Instant Action screen's BUILD button and from the cabin.
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
sits, and whether any keyboard or pad focus exists at all (Original's keyboard and pad focus is a
remake equivalence, not a decode). `CAP-52` must confirm, for the pointer and audio: which sound
plays on a rollover and which on a press, whether a disabled button makes either, whether list rows
make a rollover sound (Original plays none on a list row), which pointer bitmap shows over a
button, over a list and over nothing, and where each bitmap's hotspot is (Original draws the
bitmap's top-left at the pointer).
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
