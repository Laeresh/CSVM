# The menu inventory: every in-scope screen, transition and asset

The authoritative census behind `PLAN-menu-presentations`: what the original's own data ships,
what the Original presentation answers of it, and what it does not answer yet. Part 1 is the
`--menu=` aid map; part 2 is read out of the UI archive `crimson.rof` (through `RofFiles`) and the format
decodes that already cover parts of it; part 4 names the gaps as owed captures.

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
[`debrief.md`](debrief.md).

## Contents

- [Totals](#totals)
- [Part 1: the `--menu=` aid map](#part-1-the---menu-aid-map)
- [Part 2: the original as the data ships it](#part-2-the-original-as-the-data-ships-it)
  - [What the shell is made of](#what-the-shell-is-made-of)
  - [`LAYOUT.CSV`'s own shape](#layoutcsvs-own-shape)
  - [The navigation graph the layout states](#the-navigation-graph-the-layout-states)
  - [Coverage](#coverage)
  - [In scope and out of scope](#in-scope-and-out-of-scope)
  - [Screen by screen](#screen-by-screen)
  - [How the option pages ink and mark focus](#how-the-option-pages-ink-and-mark-focus)
  - [Menu audio](#menu-audio)
- [Part 3: assets, required and optional](#part-3-assets-required-and-optional)
- [Part 4: what the evidence does not cover](#part-4-what-the-evidence-does-not-cover)
  - [Owed captures](#owed-captures)
  - [Where Original and the original disagree](#where-original-and-the-original-disagree)
- [The decoded layout](#the-decoded-layout)

## Totals

| Quantity | Count |
|---|---|
| GUI scripts in `crimson.rof` | **61** (5 infrastructure, 34 single-player screens, 22 multiplayer) |
| `LAYOUT.CSV` sections | **35** (`[GLOBALVARS]` + 34 screens, one per single-player script) |
| `LAYOUT.CSV` widget rows | **636**, in **10** of the 11 documented widget types |
| `LAYOUT.CSV` macro definitions | **186** (29 global, 157 per-section) |
| Distinct art files `LAYOUT.CSV` names | **124** (122 present in the extraction, 2 absent) |
| Distinct `IDS_*` symbols `LAYOUT.CSV` names | **152**, of which **149** resolve to text |
| UI sound files | **8** |
| Original screens in this plan's scope | **28** of the 34 single-player screens (25 built; the 3 remaining Preferences pages stand behind doors drawn disabled) |
| Layout-stated navigation edges in the original | **46** (32 driven by Original, 2 realised as the wingman slot's row, 2 drawn disabled, 10 out of scope; see [Coverage](#coverage)) |

## Part 1: the `--menu=` aid map

The values below are the campaign, multiplayer, join board and board aids; Original's own screen
values (`free-flight`, `instant-action`, `options`, `video`, the `plane-*` hangar poses and the
rest) are listed in [`menu-presentations.md`](../menu-presentations.md), "The `--menu=` aid
convention".

| `--menu=` value | Opens |
|---|---|
| `campaign` | the real profile roster |
| `campaign-empty` | the roster with no profiles |
| `campaign-roster` | the roster with two profiles |
| `campaign-entry` | the roster with a name being typed |
| `campaign-cabin[:idaho[script]]` | the cabin, a leading `idaho` the typed word, the mission pull-down standing before the script runs (`idaho5da` opens its list) |
| `campaign-previous` | the previous-missions list |
| `campaign-scrapbook` | the book at the last mission flown |
| `campaign-briefing[:seconds]` | the briefing, reveal advanced |
| `campaign-flightcheck` | the flight check |
| `campaign-guestcheck[:player]` | a guest's own flight check |
| `campaign-ammo` | ammo selection |
| `campaign-planeselection[:export]` | plane selection, or its export messagebox |
| `campaign-hangar` | the hangar over the profile's wallet |
| `campaign-fly` | walks a real profile to Fly Mission and launches |
| `campaign-coop[:guests[:code\|offline]]` | the cabin with the co-op network door open over a loopback wire holding that many guests (0 by default), its band, its CLOSE NETWORK plaque and its remote chips drawn, the chips naming the host Zachary and the guests Nathan, Sheila and Lucy, and its second argument poses a master server that listed the host under `K7Q-X3M` (`code`, the band names the code and PRIVATE and no address) or one the host cannot reach (`offline`, the band says why there is no code) |
| `campaign-coop-ask` | HOST CO-OP's GAME INFORMATION over the cabin on the aids' sample answer, its Listing chooser on Private |
| `campaign-coop-guest[:board]` | a co-op guest joined to a loopback host, standing on the board the host names (`cabin` by default, `briefing`, `flightcheck`, `ready`, `debrief`), the host's rows greyed and the guest band drawn, on the first plane of the host's hangar no earlier seat flies, with its own plane and ammo pickers, its chips naming the host Zachary, itself Nathan and the third human Sheila; `ready` is the check after the guest's Ready, `debrief` the host's won result |
| `campaign-coop-ready` | a co-op host's flight check with two guests, the first Ready and the second not, so FLY MISSION is greyed and the strip's chips say Ready, named as `campaign-coop` names them |
| `connection` | the Multiplayer Connection page |
| `connection:gameinfo`, `connection:playerinfo` | the Connection page with GAME INFORMATION or PLAYER INFORMATION standing over it on the aids' sample answer (game and callsign Zachary, eight players, Gruff Male), over an in-process door |
| `connection:code` | the Connection page over an in-process door with a master server set, Join by code picked with `K7Q-X3M` in its box and the cursor there |
| `connection-games[:searching]` | the LAN games list over an in-process LAN answering with five sample games, one of another build version, or with none so the Searching box stands |
| `lobby[:host\|guest[:mission\|plane\|ammo\|rockets\|scores\|outlaw\|outlaw-rockets\|bots\|bot\|bot-scores\|race\|ctf\|seats[:code\|offline]]]`, `lobby:late` | the Multiplayer Lobby as its host or first guest, over an in-process wire with two guests, Time 5 and Limited Lives set, the first guest Ready and one chat line from each, on the named tab; `outlaw` and `outlaw-rockets` open the outlaw list on Airframes or Rockets with two airframes and All Rockets outlawed; `bots` fills the field to six with three bot rows on Mission Options, the first an ace on the Fury, callsigns drawn from the pilot names, `bot` opens Select Plane on that bot, `bot-scores` lands Game Scores on a match over those six seats that the first bot leads, and `race` turns the `bots` pose to a Stunt Race, whose bots fly the course; `ctf` turns the type to Capture the Flag on Mission Options; `seats` gives the host's machine and the first guest's a second seat each on Mission Options, the shown machine's a device-less seat of the menu's own, each second seat on a plane of its own; `lobby:late` is a guest that joined while its host flies a match, alone on its list with the In Progress line pinned over its chat; a third argument, `code` or `offline`, poses the host's master server as `campaign-coop`'s does, which the host's pinned Network rows show |
| `join-board[:pads[:bots\|bot]]` | the join board, drawing `pads` entries posed as signed on; `bots` stands five bot rows of the player setup under the articles, named from the pilot names, the second a Fury at ace and the third a novice, and `bot` opens the Edit Bot panel on that second row |
| `loadboard[:mission_type]` | the load screen's blackboard, over the menu, writing that mission type's own dialog |
| `loadboard-campaign[:CM]` | the load screen's chart sheet for that campaign mission, `CM01` by default, over the menu; it reads the memento off a `--campaign=` profile the same way |
| `loadboard-mp[:chapter[:mode]]` | the Dogfight load screen's blackboard for that chapter's lobby environment and mode letter, as `pauseboard-mp` names them, standing rather than torn down by a build |
| `pauseboard[:CM[:done]]` | the Original presentation's pause sheet for that campaign mission, `CM01` by default, with that many of its objectives marked. A `--campaign=<profile>:<seq>` beside it hangs that profile's own memento, the way a real pause does; without one the sheet hangs the seeded pin-up |
| `pauseboard-ia[:chapter[:mission_type]]` | the Original presentation's Instant Action pause blackboard for that chapter's environment and mission type, `C1` and `stunt_flying` by default |
| `pauseboard-mp[:chapter[:mode]]` | the Original presentation's Dogfight pause blackboard for that chapter's lobby environment and mode letter (`d` Deathmatch, `t` team Deathmatch, `c` Capture the Flag, `z` Zeppelin vs Zeppelin), `C1` and `d` by default |

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
| Preferences | `PF_B_GAMEOPTIONS` / `PF_B_AUDIO` / `PF_B_VIDEO` / `PF_B_CONTROLS` | GameOptions / Audio / Video / ControlsPrefs (all four live) |
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
in the data.

### Coverage

`CSVM.Tests/OriginalCoverageTests.cs` is the machine check of this page against the Original
presentation: it drives `OriginalShell` from the top level to every screen and back by three input
families (pointer clicks on the rows' rectangles, keyboard and pad cursor commands walking the
shell's own spatial step, the keyboard trying down and right first and the pad up and left), once
over the hand-authored fixture layout and once over
the install's own `menu_layout.json` and art. Every `ScriptToExe` edge of an in-scope section must
have an entry saying how Original realises it, and the entry is checked against the shell. The
tally over the install: 35 screens (every `OriginalScreen`) reached and left with no open campaign,
build or dialog behind; 58 journeys by 3 families; 46 edges of which **44 are driven** (the row is
pressed and the target screen shows, `MM_B_MULTIPLAYER` opening the Connection page) and **2 are
realised as the wingman slot's row** (`FC_B_CHANGEPLANEW` and `FC_B_CHANGEAMMOW` are the pilot's
plaques at the wingman's slot, present exactly when the mission flies a wingman); none is drawn
disabled and there are 0 dead ends. The wrap-up
page is reached the way a flown mission reaches it, by handing the shell one ended mission's frozen
numbers, since no row anywhere opens it. The exits are checked too: Quit as a `QuitExit`, ACCEPT CHANGES as a
`OptionsApplyExit`, FLY on Free Flight and Fly Mission on Instant Action as a `LaunchExit`,
FLY MISSION as a `CampaignMissionExit`, and Purchase Now returning to the top level with the plane
saved. Keyboard and pad share one semantic command vocabulary at the seat seam (Decision 25), so
the two cursor families differ in the walk they take, not in the commands the shell sees; the
device mapping behind them is the seats' own tests.

### In scope and out of scope

Decision 5 puts every screen the menu offers in scope. Mapping that onto the original's 34
single-player screens leaves **30 in and 4 out**, before the 22 multiplayer screens. Of those, the
Connection page and the LAN games list are built as the campaign co-op's way in, and the
Multiplayer Lobby with its four tab pages as the networked Dogfight's; the rest stay out.

**In scope (30):** MainMenu; Preferences, GameOptions, Audio, Video, ControlsPrefs, Keys; Credits;
InstantAction, IA_WrapUp; Campaign, PassengerCabin, MomentoSelection, FlightCheck, PlaneSelection,
OrdinanceLayout, ScrapBook, ScrapBook_TOC, ScrapbookZoom; Hangar, PlaneName, PlaneConstruction,
AirFrame, Engine, Armor, Guns, HardPoints, Paint, Purchase; MessageBox.

**Out of scope (4):** CampaignIntro and FinalCinema (MPG playback, `BL-446`); Save and Load (no
savegame system here, and the `LOAD` branch is unreachable in the shipped build).

IA_WrapUp is a menu screen in this presentation rather than a flight board: an ended Instant Action
mission leaves the world for it, and its CONTINUE returns to the Instant Action screen.

All five Preferences leaves are built. GameOptions, Audio and Video carry the shared options: the
difficulty, the default view, the automatic head turn, the targeting switch, the rumble and the two gun aids stand on the first, the
four volume levels on the second, the graphics mode, the display mode, the V-Sync choice and the
render scale on the third. ControlsPrefs and Keys stand
over the shared `ControlsFeature`, so all four page doors (`PF_B_GAMEOPTIONS`, `PF_B_AUDIO`,
`PF_B_VIDEO`, `PF_B_CONTROLS`) are live.
`MM_B_MULTIPLAYER` is live wherever the shell has a network door, and opens the Connection page
([The multiplayer screens](#the-multiplayer-screens)); a shell built without one draws it disabled.
`MM_B_CREDITS` is live and opens the credits screen, which is built
whole: the pane, both buttons, the About box and the right-button line.

### The multiplayer screens

The multiplayer scripts use the `CC` library and place every widget inline, so the corners below
are the scripts' own, in 800x600 board pixels, and `OriginalConnectionScreen` holds them as
constants rather than reading a layout.

- **Connection** (`MP_OPTIONSBACKGROUND.JPG`). The script places five `MP_B_RADIO.PNG` radios at
  x 50, y 98, 134, 171, 207 and 268 (four frames: greyed, normal, rollover, selected), MSN Gaming
  Zone, LAN IPX, LAN TCP/IP, Internet and Modem-to-Modem, each label (langui 10001 to 10005) 33 px
  right of its radio and its description (10008 to 10012) at x 83. The IP Address box stands 17 px
  under the Internet radio, 150 x 18, its label 101 px left of it, and the phone box likewise under
  Modem-to-Modem. Host (`MP_B_SMALL.PNG`) at (514, 424), Connect (`MP_B_MEDIUM.PNG`) at (610, 424),
  Exit Multiplayer at (514, 559), Build Custom Plane at (117, 468), and the panel text (10524)
  centred in x 59 to 388 at y 380. The capture draws every label and description black, a greyed
  way included, and the live address box as a black outline over the page.
- **The games list** (`MP_GAMESBACKGROUND.JPG`). The title (10067 over the way's name) at (69, 80);
  five sort radios, the first two at y 116 and x 145 and 278, the rest at y 118 and x 414, 547 and
  680, each under "Sort by:" (10068); five header columns from x 53, of widths 145, 100, 166, 145 and 115,
  their words (10069 to 10073) at y 149; up to twelve rows from y 175 at a 25 px pitch; the Auto
  refresh checkbox (`MP_B_CHECKBOXLARGE.PNG`, eight frames) at (47, 507) and Create Game, Join Game
  (`MP_B_LARGE.PNG`) and Exit (`MP_B_SMALL.PNG`) along y 507 at x 394, 529 and 665. The sorted column
  wears `MP_GAMESALPHA.PNG` cropped to that column at alpha 80, tinted F2D08B. An empty list is
  covered by the Searching box (`MP_ERRORMESSAGEBACKGROUND.JPG` at (254, 160), "Searching ..." in
  red, its Cancel at (404, 310)), and asks again every second; a filled one asks every five seconds
  only while Auto refresh is checked. A game that asks a password reads Need Password (10141) in its
  Status column unless it is full ([multiplayer-messages.md](multiplayer-messages.md), "Boot and the
  password").
- **What is ours.** Of the original's ways only LAN TCP/IP, which searches, and Internet, which
  joins the typed address, are on the page, at the script's first two radio places (y 98 and 134)
  with its pitch, the IP Address box at (184, 151) and the Internet description at y 177. MSN Gaming
  Zone, LAN IPX and Modem-to-Modem, with the phone box, are left off since the remake has no carrier
  for them. A third way of our own, Join by code (`MP_R_CODE`), joins the code a host shares through
  the master server. It stands 61 px under Internet at y 195, the script's own step from Internet
  over its box to Modem-to-Modem, with its Join code box (`MP_E_CODE`, 150 x 18) at (184, 212), the
  box label 101 px left of it and its description at y 238, all in the Internet way's faces. The
  string table has no text for it, so its words are ours: "Join by code", "Join code:", and "Join an
  internet game with the code its host shares, such as K7Q-X3M. No router setup needed." The box
  takes the code alphabet and the dash in capitals, up to seven characters, and Ctrl+V pastes into
  it; Connect reads the code with or without its dash, and a box holding none raises "The join code
  is not recognized." in the shared box. With no master server set, or no WebRTC library, its radio
  draws greyed, its box outline grey, and its description names the reason ("No master server is
  set, so joining by code is unavailable."). The cursor's walk runs LAN TCP/IP, Internet, the IP
  Address box, Join by code and the Join code box; Enter in either box is that way's Connect. Host
  and Create Game open the Multiplayer Lobby as a Dogfight's host, skipping the original's host
  box (game name and player cap). Build Custom Plane opens the wallet-free hangar, the Instant
  Action door's, on the default airframe, and CANCEL or a commit lands back on the Connection page
  with the cursor on it; the cursor's walk reaches it under the Join code box. A plane built there
  is listed on the lobby's Custom Planes tab, which is the multiplayer inventory the panel text
  (10524) names. The Mission Environment column
  names a co-op game's mission, or its shortcode when the name passes 24 characters, and a
  Dogfight's environment. A join is followed on the shared message box: connecting, waiting on the host, or why
  it ended. The multiplayer error box's errors (langui 10022, 10025) are raised in the shared box.
- **The Multiplayer Lobby** (`MP_LOBBY_BACKGROUND.JPG`). The title (10046) at (60, 22), the
  player count (10048) and the Ready header (10052) at y 54, the player list from (34, 83) at a
  20 px pitch with its `MP_B_CHECKBOX.PNG` mark 230 px right of each name, and the chat pane at
  (34, 373), 735 x 165, the speaker's name then the line 100 px right of it.
  [Evidence: script] The chat pane scrolls under its own scroll control, `MULTIPLAYERLOBBY_CHAT.SCRIPT`'s
  `KG` (`@ctl@EO`): at (757, 368), 175 tall (`XB`), the `mp_b_scrollup`, `mp_b_scrolldown` and
  `mp_b_scrollbar` art over a track of `KF` 0xff202418, holding `IO` = 175 over the line face's
  height in lines, and activated only while the lines (`FO`) outnumber that. While the view stands
  at the bottom (`RAA` = 1) a new line moves its top (`HO`) to keep the newest line in view; once
  the reader has scrolled up (`HO < FO - IO`) it stays put. The tab strip's four
  tabs (10094, 10114, 10119, 10507) stand at y 24 from x 324, 442, 548 and 656, and the picked tab's
  page (`MP_LOBBY_MISSION.PNG`, `_PLANE`, `_AMMO`, `_STATSCREEN`) at (314, 26). Game Scores
  (`MULTIPLAYERLOBBY_STATS.SCRIPT`) heads its five columns with 10542 to 10546 (TREB13B), the
  first at (+21, +43) left in 150 and the rest at (+179, +44), (+242, +44), (+303, +44) and
  (+362, +44) centred in 60, 58, 56 and 54, and lists ten rows from (+24, +69) at a 20 px pitch:
  the name in 10573's face (TREB10B) left in 154, then four figures in 10574's face centred in 62,
  61, 60 and 57. A row its own flag marks draws grey (`0xffbbbbbb`).
  [Evidence: script] `MULTIPLAYERLOBBY_STATS.SCRIPT`'s scroll control `HEA` (`@ctl@EO`) stands at
  (+419, +66), 195 tall (`XB`), over `IO` = 10 rows, with the same three arrow and thumb arts and
  `KF` 0x00000000, a clear track; it is activated only while the lines (`FO`) number more than ten,
  and the rows show from its top (`HO`). Mission Options
  carries the Environment and Type boxes (`MP_B_LISTBOXARROW.PNG`), the victory radios
  (`MP_B_RADIO8STATESSM.PNG`) with their Time and Score boxes, and the teams, lives and planes
  checkboxes (`MP_B_CHECKBOX8STATES.PNG`). Its two columns' headings stand at different heights in
  `MULTIPLAYERLOBBY_MISSION.SCRIPT`: Mission Environment (10096, TREB11B) 20 above its box at
  (+25, +63), so at +43, and Victory Conditions (10098, TREB12B) at (+241, +46), a face a point
  larger, so the right column's text sits lower; Select Plane the Default and Custom sub-tabs
  (`MP_LOBBY_TABLARGE.PNG`), the plane box and the plane's icons (`MP_PLANEICONSTOPFRONT.PNG`) and
  ratings; Select Ammo the Guns and Rockets sub-tabs (`MP_LOBBY_TABSMALL.PNG`), a box per gun slot
  or per wing cell. Boot, Create Team, the Ready box (`MP_B_CHECKBOXLARGE.PNG`) and its Ready? label,
  LAUNCH!, the chat box, Send and Leave Game stand on every tab.
- **What is ours in the lobby.** All three types fly. Capture the Flag and Zeppelin vs Zeppelin tick
  Restrict Number of Teams at two and grey it, and Capture the Flag also greys Above the Clouds and
  NW Lighthouse and shows an Own Flag Home to Capture checkbox beside the Teams heading, the
  remake's own option drawn in the checkbox art, its label a pixel under its siblings' so it ends
  inside the page, standing on the heading's baseline with the box ending on it. The team button reads Create Team, Join Team with a team row picked, or Leave Team,
  and is live while its pilot is not Ready; Create Team stands the CREATE TEAM box
  (`MP_CREATETEAMBACKGROUND.PNG`). The player list draws each team's row in its team colour with
  its members under it, then the players on no team. Restrict Number of Teams and its two count
  boxes, with the script's arrows (`MP_B_SCROLLUP.PNG`, `MP_B_SCROLLDOWN.PNG`), are live on the
  host. The victory radios arm Time, Score or both, and a press on the only armed one keeps it.
  Each player row names its pilot's plane in a column of the remake's own, since the original's
  list names none: from x 170, 76 wide, ending clear of the bot tag and the Ready mark. It reads the
  stock plane's short name, a custom plane's full name, Random on a bot's Random plane, and nothing
  on a guest's row until the host has heard its pick. The names before it end at x 166. A name or a
  plane wider than its box stays on its row, clipped to the box and scrolling through its text as
  the board's other marquee captions do.
  Boot is live on the host once it picks a guest's row in the player list
  (the picked row takes the script's fill, 209, 180, 120); the host's own row is not offered, and a
  guest's list offers none. The host's Allow Custom Planes and Outlaw Components are live, Allow
  Custom Planes opening ticked as the script's host open ticks it
  ([multiplayer-messages.md](multiplayer-messages.md), "Custom planes"), and Custom Planes lists
  the pilot's saved planes while custom planes are allowed. Select... (View...
  on a guest) is live while Outlaw Components is ticked and opens the outlaw list
  (`MP_LOBBY_OUTLAWED.PNG`) over the tab page, laid out as
  [`multiplayer-messages.md`](multiplayer-messages.md) decodes it; each tick reaches every guest at
  once, the list is read-only on a guest and on a Ready host, and a toggle of Outlaw Components
  empties it. A completed match's Exit lands every pilot back in the same lobby on Game Scores,
  which shows that match's name, points, kills and deaths best first (Hits % stays blank), with
  every Ready cleared for the next round. Past ten lines, a match's or a race's, the page scrolls
  under the script's `HEA`, drawn as its arrows and thumb with no track; the arrows stand in the
  cursor's walk, and the wheel and the thumb drag move it too. The lobby's chat scrolls the same
  way under the script's `KG` once its lines outnumber the pane, the remake's pinned Network rows
  staying above the window. The text column ends 4 px clear of `KG`, which stands over the
  scroll bar the background paints at the pane's right.
  Game Scores is greyed until a match has landed. The own
  name is drawn red in the list and the chat. The player list shows eleven rows; past them, which
  bots and team rows reach, it scrolls under the remake's own bar at x 292, inside the pane's right
  border, drawn as the chat's. Leave Game always lands on the Connection page. The host's options lock while it
  is Ready, and any option change clears every Ready. A stunt race's end-of-race board under the
  Original presentation is this screen on Game Scores, drawn in flight, since the original has no
  race: the standings on the scores page (place and callsign left and the aircraft right in the name
  column, best, gap and runs in the next three, the fifth empty), the zone key on the player
  list's lines, the splits in the chat pane, the context in the chat line, and Photo Mode, Restart
  and Back on the Create Team, Send and Leave Game plaques. A field past ten scrolls the scores page
  under its bar, and past seven the splits scroll under the chat pane's own control, the pane they
  borrow; both bars' arrows join the board's cursor after the plaques, and the board takes no wheel
  or thumb drag. The other three tabs stand unlabelled.
  Every word on it but Game Scores is the remake's own. A guest's plane and ammo picks stay live,
  and a changed pick clears that guest's own Ready. Every seat signed on at a machine's join board
  flies the match, since the original has no split-screen network play: each further seat has a row
  of its own under its machine's first, named by the name that first row shows and its pane number
  on that machine ("Gabriel P2", "Host P2" for a host with no callsign; the flight's name for the
  seat too), scrolling in its column when wider, and drawn red on its own machine. The row's place
  in the list is its seat at the launch. One Ready stands for all of a machine's
  seats, and a seat given or taken since clears it. Ready on a machine flying further seats walks
  each of them, in player order, through the per-seat aircraft screen, its WEAPON LOADOUT included,
  and the last seat's confirm is the Ready. That screen lists the eleven stock planes and, while the
  host allows custom planes, the machine's saved ones after them. A further seat's custom plane is
  named on its row and checked against Allow Custom Planes and Outlaw Components as a first seat's
  is. The player count against the cap, the team rows' member counts, the team balance
  and the launch refusals count every seat. A guest's further seats are seated only from the room
  every seated guest's first seat leaves. A host whose joined seats outnumber its chosen cap flies
  the cap's worth of them and seats no guest. A machine flying fewer seats than it joined reads the
  co-op band's line, "The game is full: P1 flies, P2 sits out", last among the pinned Network rows.
  The Lives box is greyed until Limited Lives is ticked, then reads 3 (the remake's own default) and
  takes 1..99. A host's chat pane carries remake-only rows pinned at its top under the name Network,
  in the picked sub-tab's red: the internet join code alone while the master server has listed the
  game, the wait alone while it is still answering (no address and no COPY yet, since a code may
  still come), else the address guests type and, under it, why there is no code. With no master
  server set the address stands alone. The first pinned row, the one naming the code or the address,
  is a remake-only COPY control: a 48-wide box at the row's right
  end, the row spanning the line so a click or a tap on the code copies it as the box does, and a
  cursor stop between Ready and the chat box. The line names Ctrl+C only after
  a key last moved the cursor.
- **The cabin's network door.** Remake-only: a paper plaque in the `FC_B_CHANGEPLANE` convention at
  (14, 40) reading HOST CO-OP or CLOSE NETWORK, with the host's band over a dark ground at y 14.
  The band waits for the master server's outcome: while it is answering, the second line is
  "Asking the master server for a join code ..." alone, with no address; then the code, or the
  address on a fault or with no master server set. The band line naming the code (else the address)
  carries a COPY control, a 44-wide box at the band's right end whose row spans the line, a stop
  just before the door in the cursor's walk, and absent during the wait; the line names Ctrl+C only
  after a key last moved the cursor. The original has no co-op campaign, so
  no script describes it. On every machine the seat strip calls each seat by the callsign its
  player gave Player Information, a seat with none by its player tag, and the band counts the
  guests, as the lobby's player count (10048) stands over its named list. BOOT asks about a guest
  by the same word its chip shows.

### Screen by screen

Composition means an authored rectangle, art name or string id. Interaction means a decoded
behaviour: what a press does, what a rollover changes, what is disabled when.

| Original screen | Layout | Script decoded | Capture | Interaction evidence |
|---|---|---|---|---|
| MainMenu | 9 rows | this page | **none** | composition only; script text gives the six buttons and their targets, nothing about rollover, music or the flag movie. **Built as Original's top level** from the two panes and the six `B` rows with their four-frame strips: Campaign, Instant Action, Preferences (the Options screen), Credits and Quit react, Multiplayer opens the Connection page over a network door (disabled on a shell built without one), and the movie's place is black. Quit terminates on the press with no confirm, which is `MAINMENU.SCRIPT`'s own `terminate` on `mm_b_quit` |
| (none: remake-only) | none | none | none | **Original's Free Flight and Dogfight doors, and the local lobby they open, of our design under Decision 11.** The two doors are text buttons in the `FC_B_CHANGEPLANE` paper-plaque convention beside the button frame, Free Flight level with Campaign and Dogfight under it. Both open one screen on the `[@MultiplayerLobby@]` layout with no network behind it: the lobby's background, tab strip and pages, its player list naming each joined seat by tag and device, its aircraft and its Ready mark once confirmed, and in a Dogfight each join board bot under the `BOT` tag. Every rule on it is remake-only. The title reads DOGFIGHT or FREE FLIGHT in langui 10046's place. Mission Options carries the map box in the Mission Environment box's place, listing the shared chapter roster under its Free Flight labels and opening on the first chapter, each mode keeping its own pick. Its Mission Type box stands greyed on Deathmatch (Free Flight on a Free Flight). Its rules are the lobby's own rows, `MPL_R_TIME`, `MPL_E_TIME`, `MPL_R_SCORE`, `MPL_E_SCORE`, Limited Lives with its box and Auto Respawn, live in a Dogfight and greyed in Free Flight; they open on the network lobby's values, 10 minutes armed and 40 in the Score box, so a local and a network Dogfight start in the same place. Teams, the plane rules, the bot block, Boot, the team button, Ready and chat do not stand, and Select Ammo and Game Scores draw greyed. Select Plane holds seat 0's aircraft over the whole shared roster in the Select Plane box's place, beside the lobby's ratings column. Neither box steps its value sideways, so Right from the map box crosses to the rules. FLY stands in LAUNCH!'s place under its word, live once a Dogfight has a second pilot (a seat or a bot) and always in Free Flight; with a further seat joined it walks each through the per-seat aircraft screen below, whose last confirm is the launch. BACK stands in Leave Game's place. The chat pane's top line carries the hint. Nothing on it is decoded; only the lobby's chrome is |
| (none: remake-only) | none | none | none | **Original's join flow, of our design.** A pad signs onto a seat at the join board alone (A on, B off, the captain's Start casts off, through `MenuSeatDevices`), and the local lobby, the Instant Action screen and the campaign flight check read that roster; seat 0's mouse, keyboard or pad picks the map and the aircraft and presses FLY, which is its own confirmation; with another pilot joined FLY walks the per-seat screen and its last confirm is the launch. On the campaign the walk is the flight check itself, one player's check at a time: that pilot's own device drives their check and the ammo and plane screens it opens, plus the mouse riding seat 0's source, and nothing else of seat 0's. The original has no join gesture |
| (none: remake-only) | none | none | none | **Original's per-seat aircraft screen, of our design.** Once seat 0 has pressed FLY or FLY MISSION, each joined seat in player order picks on a screen of its own, the campaign plane-selection board's shape (`PS_BACKGROUND` and its section's slots) over the shared or Pilot Plane roster: the list field, the silhouette, the ratings and weapon column, WEAPON LOADOUT over a selection (borrowing the pilot block's `PS_B_EXPORTP` geometry, which the remake's picker draws no EXPORT in), ACCEPT and CANCEL SELECTIONS. Accept selects, a second Accept confirms, and the last seat's confirm is the launch, the screen the walk came from returning instead where FLY's own gate is unmet. WEAPON LOADOUT opens the `[@OrdinanceLayout@]` chrome on that seat's own fit and returns to the picker, so each pilot's weapons ride their own seat into the launch. Back and CANCEL SELECTIONS each take back a selection and over the open list each leaves the walk with every seat kept, nothing here unjoins, and joining stays closed while it shows. Only the picking seat's own device drives it, plus the mouse, which rides seat 0's source; nothing else of seat 0's reaches the screen. The original has no such screen and nothing on it is decoded; only the board's chrome is |
| Preferences (as Options) | 14 rows | this page | **none** | layout only for the composition; `PREFERENCES.SCRIPT` says the four page doors deactivate the screen (the layout carries their targets) and the fifth button is `pf_b_mainmenu` out of flight (`activate(@mainmenu@)`) or `pf_b_returntogame` in flight. **Built as Original's Options screen over the section's chrome**: `PF_LOGO`, `PF_BACKGROUND`, `PF_T_TITLE`, the four description rows in their authored colour, the four page doors at their corners, `PF_B_GAMEOPTIONS` live in its four frames onto the Game Options page, `PF_B_AUDIO` live onto the AUDIO page, `PF_B_VIDEO` live onto the VIDEO page and `PF_B_CONTROLS` drawn disabled (no shared controls option stands behind it), and `PF_B_MAINMENU` as the way back. The page carries no content of its own: the shared options moved onto the three pages its live doors open. Remake-only: the disabled door, a state the original never shows. `PF_T_GODESC`'s decoded words describe the original's page, not ours, and stay as authored. `PF_B_RETURNTOGAME` is the in-flight variant and is not drawn; the pause board is out of scope |
| (none: remake-only) | none | none | none | **Original's wallet-free hangar entry, of our design under Decision 11.** The Instant Action screen's `IA_B_BUILD`, whose edge the layout does not state, opens the decoded name screen as the cabin's `PC_B_PLANEX` edge does, over the saved-plane store with no wallet, and CANCEL or a purchase return to the Instant Action screen with its Pilot Plane list re-read. The Multiplayer Connection page's `MP_B_BUILD` opens the same entry on the default airframe and returns to that page; the top level carries no door of its own, the original reaching plane construction only from the cabin and this button. What the cabin path would supply and this entry cannot (the wallet, the profile's ownership) is left out rather than invented: prices are never checked against funds and no row is marked. The hub's READY and CANCEL wear the export strips the Instant Action stills show (`PX_B_ReadyToExport`, `PX_B_CancelExport`), and the cash note reads the $50000 the hub's own `gui_init` writes on this door, a figure nothing on the path enforces |
| GameOptions | 13 rows | no | `GameOptions.png` | layout only for the composition; the script creates all 13 rows, sends the choices out on accept (`callback($$E$$, 2128, 1, …)`) and drops them on cancel (`callback($$E$$, 2110)`), and both plaques state `Preferences` as their edge. **Built as Original's Game Options page**, `PF_B_GAMEOPTIONS`' destination: `GO_BACKGROUND` and `GO_T_TITLE` at their corners, then the shared options in the section's own row shape (a title in the `TITLEX` column at the row's line, a control in the `DROPX` column, a description in the `DESCX` column, rows at the authored 62-pixel pitch from the first row's Y 283), with `GO_B_ACCEPTCHANGES` leaving as the options apply carrying both choices and `GO_B_CANCELCHANGES` returning to Preferences with them dropped. The page opens on the saved options and never writes them. Row one is the original's own "Difficulty", `IDS_GO_DIFFICULTY_TITLE` over the `GO_D_DIFFICULTY` dropdown reading Normal, Hard or Hardest (the three `IDS_DIFFICULTY` rows), described by `IDS_GO_DIFFICULTY_DESC` "Select the difficulty level for a solo campaign."; the setting reaches only an enemy vehicle's armour and health maxima at spawn. Rows two and three are the original's own as well, decoded from the executable rather than stated by the layout: "Default View" (`IDS_GO_VIEW_TITLE` 1085) over the `GO_D_VIEW` dropdown, described by `IDS_GO_VIEW_DESC` 1088 "Select your default view.", and "Auto Head Turn" (`IDS_GO_HEADTURN_TITLE` 1086) as a checkbox, described by 1089 "Select to turn your head automatically as your aircraft turns." **The list is exactly three items**, built by the `uiData` 2127 handler at `0x0040d124`: the count is `mov dword ptr [ebx], 3` at `0x0040d12d`, index 0 is langui 112 `IDS_VIEWCOCKPIT` "Cockpit" (`mov ecx, 0x70` at `0x0040d181`), index 1 langui 136 `IDS_VIEWNOCOCKPIT` "First Person" (`mov ecx, 0x88` at `0x0040d168`) and index 2 langui 113 `IDS_VIEWCHASE` "Exterior" (`mov ecx, 0x71` at `0x0040d14d`); the page's own get and set is `uiData` 2128 at `0x0040cc71` over `DAT_00648528` (difficulty), `DAT_0064852C` (the view index) and `DAT_00648530` (the head turn as 0 or 1). What an index does to the camera is `FUN_00419160`, which reads the settings block's `DAT_0064b64c` at `0x0041918a` and calls the camera-mode setter `FUN_004414a0` at `0x004191a4` with mode 6 for index 0, 7 for index 1 and 0 for index 2, the Cockpit, Nose and Chase modes [`cameraViews.md`](cameraViews.md) names, and which turns the autohead flag `DAT_0071dacc` on at `0x00419180` where `DAT_0064b650` reads 1; `FUN_00418d30` fills the same block back off the live camera at `0x00418d64`, `0x00418d6e` and `0x00418d83`. ⚠ **No copy between the page's own bytes and that block is in the executable**, so the two are tied by their values and their order rather than by a decoded write. The remake keeps the original's words and that order: the Default View row is a dropdown reading Cockpit, First Person or Exterior, which a flight opens in unless `--view=` names a view, and Auto Head Turn is a checkbox that leaves the `headLook.autohead` config key deciding while it has never been set. Remake-only are the five rows under them, Next Target, Rumble, Leading Aim, Lead Marker (those two are [`aim-assist.md`](aim-assist.md)'s "Remake-only aiming options") and Modding Folder, a dropdown of Off, the folders of unzbd output it offers and a closing Choose folder... item that opens the source picker's folder dialog, read from the next start ([`../modding.md`](../modding.md)); no row offers the menu presentation, which only the command line selects, and the graphics mode stands on the VIDEO page, the display setting it is. The page draws the Preferences page's `PF_LOGO`, its own section authoring no logo pane and the photograph showing one standing. **The plate is grown to hold the rows past the three it is painted with.** `GO_BACKGROUND.PNG` is 566 by 289 with three raised row panels at the authored 62-pixel pitch, dark gaps between them at local y 112 to 119, 174 to 181 and 236 to 240, and the description window down its right. It is drawn as a head crop (0, 0, 566, 116), one band crop (0, 116, 566, 62) per row band and a tail crop (0, 178, 566, 111), so every seam falls inside a dark gap and the rivets, the raised edges and the description window keep their drawn height; **tiled rather than stretched**, a vertical stretch of the one image pulling the rivet holes into ovals and thinning the panel edges. The two plaques move down one band with the plate. ⚠ **The canvas caps the growth at one band**: the plate stands at Y 215 and its own art already reaches 504, so a second 62-pixel band would run off the authored 600. The eight rows therefore scroll four at a time through a window, one row to each of the four bands the grown plate has, so no two rows share a frame. The window carries the VIDEO page's own scroll column on its right edge (the dropdown strip's arrows and thumb, the descriptions stopping short of it), and a walk off either end scrolls it a row onto the next. At the top of the scroll Difficulty and Default View take the first two bands, their open lists needing the room under the box, and Auto Head Turn and Next Target the other two; at the foot Next Target, Rumble, Leading Aim and Lead Marker stand one to a band, each two-line description beside its own row and above the moved plaques. The banding code still pairs two checkboxes in one band where a window holds more rows than bands (the lower one 29-pixel checkbox under the upper, the only way two `PF_B_CheckBoxSmall.Png` frames fit a band painted for one); a four-row window never needs it. A checkbox row's own title stands on its box's centre line in the `TITLEX` column and left-aligned there, the placement the VIDEO section authors for the same pair (`VP_T_ClutterTitle` at Y 530 against `VP_B_CLUTTER` at 523, a 29-pixel box against a 14-pixel face); the authored `GO_T_HEADTITLE` centres its words in a 112 by 40 box instead, which a page drawing one line per row has no use for. The description column is one window rather than a row of panels, so the four paragraphs the window shows are spread evenly from the first row's own line to the last row's, each still beside its own control. A page whose rows outrun even that pairing falls back to one tightened pitch between the first line and the moved plaque line |
| Audio | 19 rows | no | `Preferences Audio.png` | layout only for the composition; three preview loops are script-bound; both plaques state `Preferences` as their edge. **Built as Original's AUDIO page**, `PF_B_AUDIO`'s destination: `AP_BACKGROUND` and `AP_T_TITLE` at their corners, then four volume levels on their own authored rows (a title in the `TITLEX` 137 column at the row's line, a slider in the same column 26 pixels under it, a description in the `DESCX` 348 column at width 310), with `AP_B_ACCEPTCHANGES` leaving as the options apply carrying every saved choice and `AP_B_CANCELCHANGES` returning to Preferences with the edits dropped. Each row's line is read off its own title widget rather than off a first row and a pitch: the authored titles run 266, 324, 381 and 434, a pitch of 58, 57, 53 and 53, so one number would misplace every row under the second. The three authored sliders are identical but for their line (`MinValue 1, MaxValue 100, CurrentValue 50`, `PF_B_SliderSlot.png` under `PF_B_Slider.png`, the press region the slot grown by `Left 0, Top -10, Right 1, Bottom -10`), and each is honoured as authored: "Music Volume", "Effects Volume" and "Voice Volume" keep their titles and their descriptions. Remake-only is row one, "Master", which takes the In-Game Music checkbox's authored row with the page's own title and description "Set the overall volume. It scales the three levels below it."; its slider stands at the slider column and offset rather than at `AP_B_MUSIC`'s own corner, that widget being a checkbox authored at X 259 on the title's own line. The range every slider takes is 0 to 100 rather than the authored 1 to 100, since one step below the authored floor is what makes a category mutable at all, which is the whole of what the In-Game Music checkbox did; the checkbox is therefore not drawn. A slider answers no Accept: the level moves under the pointer or by a sideways step of 5, clamped at both ends. The page draws the Preferences page's `PF_LOGO`, its own section authoring no logo pane. **The row that does not come back** is Sound Quality: the authored dropdown tiered a 2000-era mixer's sample rate and voice count, which is why the original had to quit and restart to change it (`IDS_AP_QUALITYCHANGED`), and Godot's mixer has no equivalent tier |
| Video | 31 rows | no | `Preferences Video.png` | layout only for the composition; both plaques state `Preferences` as their edge. **Built as Original's VIDEO page**, `PF_B_VIDEO`'s destination: `VP_BACKGROUND` and `VP_T_TITLE` at their corners, then the settings on their own authored rows (a title in the `X` 18 column at the row's line, a control beside it, a description in the `X` 325 column), with `VP_B_ACCEPTCHANGES` leaving as the options apply carrying every saved choice and `VP_B_CANCELCHANGES` returning to Preferences with the edits dropped. Each row keeps its own geometry rather than a shared pitch: the authored lines run 270, 305, 342, 381, 417, 454, 494, 530, 565 and each control has its own column and width, so a fixed pitch would put no row where the artwork draws it. The rows built so far are remake-only rows, and the cursor steps between them by where each is drawn. "Resolution" stands on its own authored line, a dropdown at `VP_D_Display`'s own column and width over the sizes the chosen screen can hold, keeping the authored description "Select the screen resolution." because that line describes what the row does here. Its words are enumerated per screen rather than shipped as a list, so it cannot offer a size the monitor cannot hold; Godot exposes no video-mode list, so the enumeration is the standard desktop sizes that fit inside the screen's own reported size, plus that size and the project default. A saved size the screen no longer offers shows as the project default rather than as the nearest one, which is the rule `ResolutionSetting` applies. "Monitor" stands on the authored Graphics line, the page's first row, a dropdown at `VP_D_Device`'s own column and width over the screens the engine reports, each labelled by the index it is saved as and the size that screen reports, described "Select the monitor the game opens on." Both its title and its description are the page's own, the authored pair naming a 3D card or a software renderer and this port having neither a card choice nor a software path. A saved index no screen answers to shows as the screen the window already stands on, which is the rule `MonitorSetting` applies and is the feature rather than an error path, a monitor being unpluggable between two launches. "Display Mode" stands on the authored Viewing Range line, a dropdown at `VP_D_View`'s own column and width reading Windowed, Borderless or Fullscreen, described "Select how the window sits on the screen."; it takes that row because the two above it are the monitor and the resolution, and its authored description names a draw distance the mission data owns. "V-Sync" stands on the authored Effects Level line, a dropdown at `VP_D_Effects`' own column and width reading On, Off, or one of the 60, 120 and 144 fps caps, described "Select the frame pacing."; one control carries both the choice and the cap, a cap meaning nothing while the loop waits for the screen. It takes that row because the three authored rows above it are where the monitor, the resolution and the display mode belong, which is the order those read in, and its authored description names a tier system this port has no counterpart for. "Render Scale" stands on the authored Objects Detail line, a dropdown at `VP_D_Objects`' own column and width reading 50%, 67%, 77%, 100%, 125%, 150%, 175% or 200%, described "Render the world below native to spare the GPU, or above it for cleaner edges."; it renders each 3D viewport at that multiple of its own size, upscaled below native and resampled down above it, which is the quality knob that trades GPU headroom for frame rate in either direction, and it takes that row because the row sits under the four display settings above it and the authored Objects Detail tier is one that does not come back. "Anti-aliasing" stands on the authored Lighting Quality line under it, a dropdown at `VP_D_DLight`'s own column and width reading Off, FXAA, SMAA, TAA or FSR 2.2, the five items that dropdown authors as displayed, described "Select how edges are smoothed."; a never-set row shows the default of the page's own Enhanced Graphics choice, Off under original and TAA under enhanced. Picking FSR 2.2 moves a scale above 100% to 100% and narrows the Render Scale list to 50% through 100% while the page stands, since FSR 2.2 refuses a factor above native. It takes the Lighting Quality line because the row sits beside the render scale it constrains and the lighting tier it replaces does not come back. "Shadow Quality" stands on the authored Texture Quality line under it, a dropdown at `VP_D_Texture`'s own column and width reading Off, Low, Medium, High or Ultra, five words in a list that authors four visible items and so windows; a never-set row shows Ultra, the look Enhanced shipped with. It sets the Enhanced sun's shadow and nothing else, so while the page's own Enhanced Graphics box is clear the row draws dead, keeps the saved word, and is described "Enhanced Graphics only."; ticked, it reads "Select how the sun's shadows are drawn.", and ACCEPT CHANGES re-applies the level to the world sun at once, a flight over the pause board included. It takes the Texture Quality line because that line stands between the anti-aliasing row and the Enhanced Graphics checkbox that gates it, and the texture tier does not come back. "Enhanced Graphics" stands on the authored Shadows line, a checkbox drawn from `PF_B_CheckBoxSmall.png` at `VP_B_SHADOWS`' own corner, described "Select the lit world."; the apply switches the running world. It takes the Shadows row because the sun shadows that row would have switched already ride the enhanced gate, and because that row's authored title box is the wide one a long title needs. Two numbers the section does not state are derived: a title box stops at the control beside it (every Video title but the Graphics one is authored 162 wide whatever stands to its right, and that one's 90 already stops short of `VP_D_Device` at 105), and a description the section gives no width wraps at the plaque column, the widthless `VP_T_ClutterDESC` and `VP_T_ShadowsDESC` being the two rows the plaques stand beside. The page draws the Preferences page's `PF_LOGO`, its own section authoring no logo pane. **The Clutter Detail line** carries "Ocean Waves", Water Quality as a checkbox drawn from `VP_B_CLUTTER`'s own strip at its own corner, ticked for the wave ocean and clear for the flat sheet, described "Draw a swell on the sea." and dead under the original world as Shadow Quality is. It takes the short line because the setting has two words and the line's authored control is a checkbox. **The dropdown rows scroll.** Twelve dropdowns share the seven long lines as a window: the seven above in that order, then "View Distance" (Normal, Far, Very Far or Unlimited, how far Enhanced draws the clutter, dead under the original world), then "Vegetation" (Original or Rich, Vegetation Quality, whether Enhanced draws the remake's richer vegetation over the original's clutter, dead under the original world), then "3D Vegetation" (Off, Near or Far, remake-only, how far the Rich vegetation draws its 3D tree models, dead under the original world), then "Ground Cover" (Off, Shells or Cards, remake-only, how the Rich vegetation draws grass and crops near the camera, dead under the original world), then "VR Mode" (Off or On, remake-only, "Experimental PC VR. Applied from the menu, the game restarts in the headset.", live on Windows alone and dead elsewhere with "PC VR needs the Windows build and a headset."). The window opens on Monitor; a walk down off its bottom line, or the wheel over it, moves every row up one line, hiding Monitor and showing View Distance on the Texture Quality line, and the walk back up past its top returns it. A scrolled row stands on the line it moved to but keeps its own line's box column and width (Monitor 200, Resolution 150, every other setting 120, all ending at 306), so no box resizes as the window scrolls; the window's foot puts VR Mode on the Texture Quality line. **Under a live headset two rows change hands**, remake-only: "Comfort Vignette" (Off, Mild, Strong) takes Display Mode's place and "VR Render Scale" (50% to 125%) V-Sync's, since a headset paces its own frames and the window only mirrors it; the hidden words ride the apply unshown. The two checkboxes stay on their short lines. A scroll column stands right of the descriptions, ending on the widest one's edge (325 + 420), and the window's descriptions wrap short of it. Its foot stops above ACCEPT CHANGES. It carries the D rows' own `CM_B_ScrollUp` and `CM_B_ScrollDown` at its two ends, each live only towards more rows, and their `CM_B_ScrollBar` thumb on the track between, which a drag moves as any list's does. Every description is one sentence, the room a line gives. Remake-only: the window and its scroll column, which the section authors nothing for |
| ControlsPrefs | 12 rows | no | `still-t084-controls-page.png` | layout only for the composition; both plaques state `Preferences` as their edge and `CP_B_KEYS` states `Keys`. **Built as Original's CONTROLS page**, `PF_B_CONTROLS`' destination: `CP_BACKGROUND` and `CP_T_TITLE` at their corners, the KEYS AND BUTTONS door at its own, `CP_T_KeysDesc` as authored, and the two plaques with ACCEPT CHANGES left of CANCEL CHANGES as the section authors them. Remake-only is the first row: the seat chooser takes `CP_D_Fly`'s Controller Type box, a dropdown over the seats the shared feature holds reading "Player 1", described "Whose keymap KEYS AND BUTTONS edits. Each seat holds its own."; this port has no controller-type tier, every device being bindable on the page behind the door, and the seat is the one thing that page needs choosing. A second player's keymap stands here once that player's pad has signed on at the join board. **Mouse Sensitivity stands as authored**: `CP_S_MOUSE` is a `Z` slider row of the Audio page's family (`MinValue 1`, `MaxValue 100`, `CurrentValue 50`, `PF_B_SliderSlot.png` and `PF_B_Slider.png`), drawn with its slot and thumb and pressed through its own region (the 171x3 slot at X 142, Y 391 inset by `Left 0, Top -10, Right 1, Bottom -10`, so 170 by 23 at 142, 381). It steps the Fly scheme's sensitivity over this port's 0 to 100 scale ([../controls.md](../controls.md), "Flying with the mouse"), the original's having moved the desktop cursor. Remake-only on the same panel is the flying scheme, a two-value chooser reading "Look" or "Fly" on the `CP_T_MouseTitle` line, in the right half of the Controller Type box's column (`CP_D_Fly`'s X 134 and Width 175) so its arrow ends where the seat row's does, and stopping above the slider's region; the title reads "Mouse" and is narrowed to end where the chooser starts, and the panel's description is this port's, naming both controls. ACCEPT CHANGES writes the staged keymaps through the feature and CANCEL CHANGES drops them, both returning to Preferences; neither is the options apply, a keymap being no field of the options file |
| Keys | 17 rows | no | `Keybinds Movement/Throttle/Targeting/Weapons/Views 1/Views 2/Other.png`, `still-t096-keys-and-buttons-page.png` | composition of all seven tabs; no interaction. **Built as Original's KEYS AND BUTTONS page**, `CP_B_KEYS`' destination: `KB_BACKGROUND` and `KB_T_TITLE` at their corners, the seven `KB_B_CAT` strips down the left column with the standing one in its depressed frame, the three authored column heads, the action list in `KB_L_Controls`' own window and pitch under a heading naming the standing category, `KB_B_RESET`, and the exit pair the section authors the other way round from every other leaf's, `KB_B_CANCELCHANGES` at X 202 left of `KB_B_ACCEPTCHANGES` at X 511 on one line at Y 550, which is what the still shows. **The seven tabs are the original's action groups, not this port's three keymaps.** Movement, Throttle, Weapons, Targeting, Views 1 and Views 2 name flight actions and take them; Other takes every action those six leave over, which is the session commands plus the whole of the menu and free-camera keymaps, since the original binds neither and a faithful strip would otherwise strand them. A tab claims each action exactly once, so a new action lands on Other rather than nowhere. **Control A and Control B are positions, not fields**: this port holds an unbounded list per action, so Control A prints the first control and Control B every other one joined by " / " (a caption too wide for its cell stays on one line, clipped to it, and scrolls, the remake's `Boards/BoardMarquee.cs`), and the slot cursor walks the whole list including the empty place past its end. A click on a cell arms a capture on that cell's own action and slot, the page swallowing the frame while one runs; Escape or the pad's Back abandons it. ACCEPT CHANGES writes the staged keymaps and CANCEL CHANGES drops them, both returning to ControlsPrefs. Remake-only: the category heading over the rows, the keyboard and pad focus, and two clamps the data forces, the authored list width putting its scrollbar at X 844 and the Control B column ending at X 815 against a plate that ends at 797, so each is clamped to the plate's own right edge |
| Credits | 3 rows | this page | **none** | `CREDITS.SCRIPT` decoded: the background pane, ABOUT, and the DONE plaque whose `ScriptToExe` is MainMenu; `gui_char` ends the script and runs `mainmenu.script` on Escape. **Built as Original's credits screen** from the full-screen pane the credit names are painted into, the two buttons over it, and both ways back landing on the top level. `CR_B_Exit` wears MomentoSelection's `MS_B_Done.png`; there is no `CR_B_Exit.png`. **ABOUT** raises the one-button box the press asks for: setting `@globals@OR.XR` switches the messagebox to the `ma_` widget set over `CR_AboutMessageBox.png` (505x416, centred at 147,92) and takes the skull icon, which stays `mb_p_icon` at its own place whatever the set. Its words are `uiData` 2108 (`0x0040a2cb`), langui 1301 formatted over the product id `FUN_004073d0` reads from `HKLM\SOFTWARE\Microsoft\Microsoft Games\Crimson Skies\1.0`, value `PID`, which falls back to `???` when the key or the value is absent, and `???` is what this port always shows, reading no registry. That row's `&&` is drawn as authored: every other langui row spells a literal ampersand singly (`Pratt & Whitney`), so the text widgets eat no `&`. **The right-button line** is read: `rbutton_update` activates a text widget at 288,308 in `0xffffff00` while the right button is held inside 287..353 by 313..333, its line built by shifting each character of an obfuscated literal down by three (`CREDITS.SCRIPT:63-72`), and the transitions count only inside that region, so a hold carried out of it leaves the line standing |
| InstantAction | 48 rows | [`instant-action.md`](../formats/instant-action.md) | **none** | option sets, the ace's control hiding and the paged enemy rows are script-proven; the screen itself has never been seen. **Built as Original's Instant Action screen** from the section's rows over the shared feature: `IA_BackGround`, the `T` rows, the contents list in its 14-row window with its own scroll arrows and slider, the dropdowns on their authored lines, the enemy rows paged by `IA_B_UP`/`IA_B_DOWN`, the radio pair, View Story, Fly Mission and Exit; `IA_B_BUILD` opens the wallet-free hangar and `IA_B_CHANGEWEAPONS` the Weapon Loadout screen for the seat the radio pair names (both destinations remake readings, no layout row stating either edge). The Lives box is a remake-only row, which the section authors nothing for: a dropdown in the screen's own idiom standing with its own title on the clear line the layout leaves between the Wingmen and Mission rows (the line is found in the gaps between the authored boxes, never written down), reading Unlimited at zero and the count to nine over `InstantActionFeature.StepLives`, live where the ace duel blanks the enemy boxes and untouched by a contents row, no preset carrying a lives value. Two more remake-only rows stand in the same idiom, titled in the same column: the Race Time box on the Lives box's line, which it takes while a stunt run has more than one pilot (a race spends no lives), and the Race Bots toggle on the next clear line, over the enemy block, shown for stunt flying while the join board holds bot rows. The toggle reads Off or On and starts Off; only On lets those rows race the run, so a solo run stays solo whatever rows a Dogfight left on the board. It is kept with the screen's other settings and stored in no file. What the data does not settle is listed under Part 4 for `CAP-50` |
| OrdinanceLayout (as Instant Action's Weapon Loadout) | 31 rows | same | same | **Built as Original's Instant Action loadout screen** over the same section: the ammunition fields for the airframe's firable gun slots and the rocket fields for its pylons (left column pylons 1 to 4, right 5 to 8), each over the stock table's option list, the two diagram panes on the airframe's frame, `OL_T_PLANEINFO` naming the fitted aircraft, the focused field's description in its pane, `OL_B_ACCEPT` keeping the picks and `OL_B_CANCEL` or Back restoring the picks the screen opened on. Remake-only: the original reaches this section from the flight check alone |
| IA_WrapUp | 15 rows | [`instant-action/wrap-up.md`](../formats/instant-action/wrap-up.md) | `Instant Action End Screen Stunt Flight.png`, `Instant Action End Screen Fail.png` | the four value rows, their brushstrokes and the CONTINUE edge are layout- and script-proven. **Built as Original's wrap-up page**, where a flown Instant Action mission's hold lands: `IAWU_BACKGROUND`, the heading, the four title/value pairs at their authored rows over `IAWU_LINE0..3`, and `IAWU_B_CONTINUE` back to the Instant Action screen. The page carries no row of its own beyond that plaque, and holds one ended mission's frozen numbers, never a live read. Remake-only are the yellow post-its under the four rows, carrying the context naming the chapter and the mission type and a stunt run's splits (more than one post-it for a long run), a stunt run's Danger Zone photographs as prints to their left, and the box above CONTINUE ticked on a win, the outcome the chrome board writes as a headline; the shipped page authors no row for any of them. The original clips each row text to its authored box; the whole string is drawn here |
| Campaign (profile) | 6 rows | [`campaign-screens.md`](../formats/campaign-screens.md) | `Campaign Player Profile.png` | full: roster fill, name validator, all four exits. **Built as Original's profile screen** over the shared campaign feature and the shared board component: the name box pre-filled with the last player seated, `CM_B_START` / Enter in the box / a second click on the filled roster row starting, a first click filling the box and a click in the box itself taking the caret and nothing else, the list sub-script's own selection bar and pointer frame, the four refusals as the one-button messagebox, `CM_B_DELETEPLAYER` asking with langui 201 as the two-button box whose answers read Yes and No (langui 102 and 103, the words `MESSAGEBOX.SCRIPT` gives a `0x4` box), `CM_B_CANCEL` leaving. The box opens on Yes, the left button `MESSAGEBOX.SCRIPT` focuses for the plain `0x4` mask the campaign passes. **The hidden pilot name is built**: `crashcheat!` typed into the box and started is read before the name validator sees its punctuation, and it sets the unlock-everything mode (the wallet filled to $250000, the eleven stock airframes in slots 2 to 12, every airframe threshold bypassed for the rest of the session) while the screen stays standing with the previous player's name back in the box. Remake-only: the caret |
| PassengerCabin | 16 rows | same | `Campaign CAP-44 Cabin.png` | full for the six buttons; whether anything on the screen animates is open. **Built as Original's cabin**: the five plaques the board component draws, `PC_B_NEWMISSION` disabled once the campaign is complete, `PC_B_PLANEX` into the name screen over the profile's wallet with the cabin as the hangar's return, `PC_B_CHANGEMOMENTO` into the memento chooser, `PC_B_PREVIOUS` and `PC_B_RETURNMM` on their layout edges. **The typed cheat is built**: a left click inside the authored region 5,336 to 85,479 gives the keyboard to the word matcher, `idaho` activates `PC_D_MISSIONS` (the pull-down the script creates deactivated, 24 rows opened on the campaign's own position, drawn over the painting in the `@shareditems@BUA` list's blue paper and black words, as the plane selection screen's are), and NEXT MISSION then launches the picked row while anything stands in the matcher's buffer |
| MomentoSelection | 7 rows | same | **none** | full: `MOMENTOSELECTION.SCRIPT` and the award table behind `uiData` 2150 are decoded. **Built as Original's memento chooser** through the shared page: the scrapbook photograph under its reflection, the two arrows stepping the pictures the profile holds, ACCEPT writing the chosen name into the profile and CANCEL leaving the cabin's wall as it was |
| FlightCheck | 25 rows | same | `Campaign Flight Check.png`, `… Change Plane Button.png` | full: both slots, four lists, the wingman gate, both plane-change rules. **Built as Original's flight check**: the paper plaques per crew slot hit-tested at their rows, `FC_B_RETURNBRIEF` rewinding a co-op walk, `FC_B_FLYMISSION` advancing to the next joined human's check or leaving as the feature's launch with every seat's devices; a joined seat drives its own check |
| PlaneSelection | 27 rows | same | `… Change Plane.png`, `… Combo Box.png`, `… Unique Warning.png`, `… Export dialog.png` | full, including the rollover preview and the duplicate rule. **Built as Original's plane selection** through the shared page: the fields at their `D` rows with the open list's entries hit-tested under the box, a sideways step on the closed field, the 710 refusal and the 702 export message as Original's own messagebox. Remake-only: the rollover preview is not drawn (the page previews nothing on a highlight) |
| OrdinanceLayout | 31 rows | same | `… Change Ammo Menu.png`, `… ComboBox.png`, `Campaign Ammo Selection.png` | full. **Built as Original's ammo selection** through the shared page: the twelve fields, the description pane following the focused field, ACCEPT and CANCEL back onto the plaque that opened the screen |
| ScrapBook | 46 rows | same + [`debrief.md`](debrief.md) | `Campaign Scrapbook CM01 Story Scraps.png` | full. **Built as Original's book** through the shared page: every scrap hit-tested on its `SCRAPBOOK.CSV` region (the picture's own bounds where the row authors `0,0,0,0`), the arrows, tabs, bookmark and buttons at their rows, Back returning to the contents or the cabin, whichever opened it |
| ScrapBook_TOC | 8 rows | same | `Campaign CAP-41 Previous Mission 1/2.png` | full. **Built as Original's table of contents** through the shared page: the mission rows hit-tested inside the listbox's window, a click picking and a second click replaying, the buttons at their rows. **The typed cheat is built**: a left click inside the authored region 42,426 to 136,523 gives the keyboard to the word matcher and `ispy` lists all 24 missions and opens every spread of the book behind them, whatever the campaign has flown and whatever the capture bits say, each spread carrying the script's own white `mission: N spread: N` line at 10,570 |
| ScrapbookZoom | 83 rows | same | `Campaign Scrapbook CM02 Mission select….png` | full. **Built as Original's zoom** through the shared page: `SBZ_B_RETURN` and `SBZ_B_EXPORT`, the export's 705/706 as Original's messagebox; a `P0` scrap draws no inset and offers no export, and each text box takes its langui row's face (`strings.md`, "Font prefix") |
| (Briefing, a `zrdr` dialog, not a script) | none | [`briefing.md`](../formats/briefing.md) | `Campaign Briefing.png` | full. **Built as Original's briefing**: the three `brief_button1` plaques at the dialog's own positions with their label-face states, the reveal advanced on the presentation's clock, the narration begun through the shared audio service on entry and again on REPLAY BRIEFING and ended on RETURN TO CABIN and GO TO FLIGHT CHECK |
| Hangar | 16 rows | [`hangar.md`](hangar.md) | `CustomPlane PlaneSelection.png` | callbacks and economy decoded; navigation only from the layout. **Built as Original's INVENTORY**, the hub's SELL PLANES destination (a remake reading: `PX_B_Sell` states no edge): `HA_BACKGROUND`, the title and prompt, `HA_D_PILOTPLANE` over the shared feature's saved planes, the picked plane's `HA_P_PILOTPLANE` frame, its name and airframe as the one line callback 2011 writes on the plane selection screen too, agility, armour, value (1258) and guns, that line on `HA_T_PLANE`: `HANGAR.SCRIPT` binds both of its text objects to that key and none to `HA_T_PILOTPLANE`, so the wide 236,110 row is authored and never drawn, and a plane named for its airframe writes the one word once, `HA_B_SELLP` asking first with the two-button messagebox (langui 700 over the short airframe name and the plane's value, Yes and No) and refusing as the one-button box (704 for a reward aircraft, 701 below the two-plane floor) before selling or deleting through the feature, `HA_B_EXPORTP` live beside it, answering with langui 702 over the short airframe name and writing nothing (the remake's two presentations already share one plane store), `HA_B_DONE` back to the tab. The sell messagebox is the plane selection screen's own decoded path (`campaign-screens.md`), applied here to the remake's inventory. **The plane line is drawn on row 104, not the authored 108.** `PS_BackGround.jpg` paints its dashed box over rows 101 to 124 (the top rule on 101 and 102, the bottom rule on 122 to 124), and a board line's baseline falls one font size under its row, so the authored 108 put the 14-pixel line's baseline on that bottom rule; 104 centres its cap band on the box instead. The artwork is the reference because no still of this screen exists |
| PlaneName | 8 rows | partly | `CustomPlane Name Dialog.png`, `CustomPlane Name Dialog No Name.png` | the edit box's font and colours are authored; nothing about the validator here. **Built as Original's name screen**: the pane centred on the board with the section's rows relative to it ([`menu-layout.md`](../formats/menu-layout.md)), `PN_E_NAME` fed by seat 0's typed characters under the shared feature's character set and 32-character cap with a blinking caret in its own `CursorColor`, `PN_B_DEFAULT` (checked as authored) choosing the default configuration or a bare airframe, `PN_B_OK` always live and answering an empty box with langui 203 as the script's one-button messagebox, `PN_B_CANCEL` dropping the build; the original's `MaxChars` of 16 is not applied, the saved-plane index's 32 is |
| PlaneConstruction | 47 rows | [`hangar.md`](hangar.md) | `Campaign CAP-40 Plane Construction 1/2.png` | composition; the tab bar's own behaviour is unseen. **Built as Original's Plane Construction hub**: the background, the plane at the `PX_P_PLANE` corner (the airframe's blueprint, or the `PX_ICON` set tinted with the picked colours), the PLANE NAME title with `PX_E_NAME` as an edit box over the scratch plane's name (the script's own run from the title's end to 302), PLANE COST as the running total and red past the wallet, AIRFRAME, WEIGHT CAPACITY, CURRENT WEIGHT red over capacity, every one of them following the row under the cursor in an open list, the weight line alone going pending and plain under the airframe list, the agility and armour words with their `PX_BarGraph` segments, the cash note on both doors (the wallet's funds, else the export door's own $50000), the six `PX_Tab` tabs as siblings with the standing one drawn in its depressed frame and still hittable, `PX_B_Sell`, `PX_B_Ready` and `PX_B_Cancel`. **The typed cheat is built**: a left click inside the authored region 615,10 to 735,80 (the cash note's own corner) gives the keyboard to the word matcher and `gimme` adds $25000 while the wallet holds under $50000, the script's own ceiling; the script never clears its buffer on a match, so a second `gimme` typed straight after the first cannot fire. What the data does not settle is listed under Part 4 for `CAP-53` |
| AirFrame | 4 rows | [`hangar.md`](hangar.md) | none | the airframe stat table is decoded; the screen is four widgets over it. **Built as the hub's airframe tab**: `AF_D_AIRFRAME` picking through the shared feature and raising the langui 206 ask as a dialog, `AF_T_AIRFRAME` and `AF_S_AIRFRAMEDESC` following the focused airframe |
| Engine | 4 rows | same | none | as above. **Built as the engine tab** over `EN_D_ENGINE` (six engines then None) |
| Armor | 14 rows | same | none | as above. **Built as the armor tab** over `AR_D_POINT0..3` (None then 5 to 60 units in fives) |
| Guns | 14 rows | same | none | as above. **Built as the guns tab** over `GN_D_GUN0..3` (the eleven-row cycle) with the airframe's slot titles |
| HardPoints | 9 rows | same | none | as above. **Built as the hardpoints tab** over `HP_D_POINT0..1` |
| Paint | 27 rows | same + [`rof.md`](../formats/rof.md) | 8 `CustomPlane Paint*.png` | composition well covered by the paint stills; the pattern pickers' behaviour is not. **Built as the paint tab**: `PT_D_PATTERN` over the wearable patterns, `PT_D_COLORS0..2` as swatches in an 18-row window, `PT_D_SHADES0..2`, `PT_D_DECALS0..2` as `PT_P_DECALS` tiles in a two-row window |
| Purchase | 29 rows | same | none | the purchase gate and cost are decoded from the executable. **Built as the hub's totals page**, READY TO PURCHASE's destination: the column heads, one line per priced component at the authored lines and text lists, the totals, `PUR_T_PROBLEMS` in the commit's words, `PUR_B_PURCHASE` live while the feature can commit |
| MessageBox | 16 rows | [`campaign-screens.md`](../formats/campaign-screens.md) | two campaign dialogs | the `@globals@OR` parameter block and the button masks are decoded; the script also gives the buttons their words by mask, langui 100 (OK) on the `0x1` box, 102 and 103 (Yes, No) on the `0x4` pair and 102, 103 and 101 (Yes, No, Cancel) across all three slots of the `0x8` box, and its Escape takes the right answer of whichever mask stands. **Built as Original's dialog over any screen**: the `0x1` box on `MB_B_CENTER` for every refusal and notice (the profile screen's four, the plane screen's 710 and 702, the zoom's 705 and 706, the inventory's 701 and 704), the `0x4` box on `MB_B_LEFT`/`MB_B_RIGHT` for the delete confirm and the sell confirm, the `0x8` box across all three for the airframe swap's 206 (`hangar.md`, "When the airframe swap asks"), the answers in the script's words, a box opening on its first answer (the left button the script focuses, so a `0x4` or `0x8` box opens on Yes), Back taking the last answer, which is the one the script's Escape posts; while a box stands its answers are the only rows. **Rollover and focus are separate in the original and stay separate here.** `gui_init` focuses a button on every raise (`DX` when `0x20` or `0x40` is set, else `CX` on the `0x1` box, else `BX`) and sets no frame with it: the reference shot of a `0x1` box has its focused OK on the normal frame with the pointer elsewhere, so the strip's rollover frame answers the pointer alone. Ours draws it that way and gives the cursor the `DISABLED` focus outline the option pages already use, three pixels clear of the strip and only while the pointer is elsewhere, since one cursor here serves pad, keyboard and pointer together. The widget set is a parameter of the same composer, the `mb_` rows by default and the `ma_` rows for the credits screen's About box, with the icon row left on `mb_p_icon` in either |

⚠ **The original has no counterpart for Free Flight, Dogfight or the join board.** Those screens
are remake-only, which is what Decision 4 already allows.

### How the option pages ink and mark focus

Remake-only readings shared by the Options family (the Preferences page and the Game Options, AUDIO
and VIDEO pages behind its doors), each composed over a painted plate. The layout authors colours
per screen and a button's rollover and depressed frames, but nothing that marks the row a keyboard
or pad walk stands on, those pages being pointer-driven. The mark and the ink it takes are ours.

**The palette.** `OriginalPresentation.PaletteFor` inks these pages from the Preferences page's
authored description colour for row text and its title colour for the heading, the paper plaque's
own label tail for the chooser, and the file-wide `ACTIVE` for the focused row rather than that
title colour. The authored title is a duller grey than the description cream every unfocused row is
written in, so a row focused in it reads as the disabled one, and `ACTIVE` is what every other
Original screen already focuses with.

**The focus box.** `OriginalShell.FocusBox` outlines the focused row, shared by the slider rows and
by the dropdown rows that take `boxOnFocus`, so one outline covers every marked row on these pages.
It is drawn in the layout's own `DISABLED` grey rather than the dropdown's authored black, which on
dark paint is a dark line nobody sees. It is a mark rather than standing chrome: on every row at
once it is a permanent hard box around rectangles the layout authors at differing widths, so only
the row the cursor is on carries it.

**The box and the wash on a slider row.** A slider thumb is one frame with no focused or pressed
state, so a focused slider row is the box together with a wash under it. The box is the half that
reads: the wash lands over a press region grown around a three-pixel slot, so on its own it is a
faint band over mostly background. The wash stays because the box needs a region to enclose;
without one the outline reads as four loose lines. When neither the slot nor the thumb art
measures, both stand as rectangles instead, the way a missing plaque leaves an outlined label, so
the control still shows its level.

**Why the plate is backdrop.** A composed board draws its fills between the backdrop and the
pictures, so a page that puts its logo and its background plate among the pictures buries every
focus mark its rows compose under opaque art. Each option page adds both to the backdrop instead.

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
install's own layout it classifies **99 required** and **63 optional** files, and over a complete
extraction it reports no absence at all: the extraction copies the loose MPGs in, so the two
movies the layout names are on disk with everything else.

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
| screen backgrounds of the 23 composed sections (`MM_`, `PF_`, `GO_`, `IA_`, `CM_`, `PC_`, `FC_`, `PS_`, `OL_`, `SB_`, `PX_`, `MB_`) | 12 families | **required** | a screen with no backdrop is not a degraded screen |
| button strips of the composed sections | 122 `B` rows over ~62 PNGs | **required** | four stacked frames, disabled / normal / rollover / depressed; the words are painted in, so the state is entirely which frame draws |
| shared widget chrome (`GN_`, `FC_B_Scroll*`, `PX_B_Scroll*`, `CM_B_Scroll*`, `IA_B_Scroll*`, the dropdown arrows) | ~12 | **required** | named through `[GLOBALVARS]` macros, so one miss hits many screens |
| `ARIAL8.TGA` | 1 | **required** | the global 3D font; the string table's `[FONTID]` tags name `.ttf` faces the archive does not carry, and `FONT.TGA` is named by nothing |
| `ACTIVEPOINTERZ.PNG`, `PASSIVEPOINTERZ.PNG` | 2 | **required** | script-named and drawn on every screen |
| `PX_B_ReadyToExport.png`, `PX_B_CancelExport.png` | 2 | **required** | script-named, drawn by the plane construction hub; no layout row names them |
| `ZOOMPOINTER.PNG` | 1 | not classified | named in neither the layout nor an external-asset entry, and Original draws the two pointers above on the zoom too |
| `GO_BackGround.png`, `PF_B_AcceptChanges.png`, `PF_B_CancelChanges.png`, `PF_B_CheckBoxSmall.Png` | 4 | **required** | the Game Options page draws every art name its section carries; the dropdown's arrow and scroll set are the shared chrome above |
| `AP_`, `VP_`, `CP_`, `KB_` page art | 4 families | optional | the four remaining Preferences pages are out of scope, so no composed screen draws them |
| `GN_B_ReturnToGame.Png` | 1 | optional | `[Preferences]`' in-flight way back; the menu's page offers `PC_B_ReturnMainMenu.png` instead |
| `CR_AboutMessageBox.png` | 1 | **required** | the About box's own background, drawn by the credits screen's box |
| the Connection page's and the games list's art (`MP_OPTIONSBACKGROUND.JPG`, `MP_GAMESBACKGROUND.JPG`, `MP_ERRORMESSAGEBACKGROUND.JPG`, `MP_GAMESALPHA.PNG`, the `MP_B_` radio, small, medium, large, exit and checkbox strips) | 10 | **required** | script-named, since the multiplayer scripts carry no layout; drawn art may not be classed optional |
| the lobby's team art (`MP_CREATETEAMBACKGROUND.PNG`, `MP_B_SCROLLUP.PNG`, `MP_B_SCROLLDOWN.PNG`) | 3 | **required** | script-named: the CREATE TEAM box and the arrows of the team count boxes, which the Mission Options tab always draws |
| `MessageBox`'s `MP_*` rows, its `MA_B_LEFT`/`MA_B_RIGHT`, and the art of every section outside the 23 | 3 + 27 | optional | the multiplayer error box's errors are raised in the shared box, and the About box is the one-button box, so its left and right rows never draw |
| `ui_strings.json` + `RESOURCE.H` | 2 | optional | 152 symbols referenced, 149 resolve; `UiStrings` falls back to an empty table, so the screens draw with no words rather than not at all |
| `SCRAPBOOK.CSV` | 1 | optional | 461 rows; the book's extent is the file's extent, and without it the book lists nothing |
| `MOUSECLICK`, `MOUSEOVER`, `ENTERTEXT`, `ENTERTEXT_ERROR` | 4 | optional | a silent menu is usable |
| `MUSIC_SPLASH.WAV`, `MUSIC_LOOP`, `SFX_LOOP`, `VOICE_LOOP` | 4 | optional (the loops are not classified) | the three loop names carry no directory in the scripts, so the manifest has no path to check |
| `PC_P_HANGAR0..10.JPG` | 11 | not classified | assembled at runtime from a fragment; the cabin's window photo, and the painted cabin reads without it |
| `PX_0..10_BLUEPRINT.TGA` | 11 | not classified | assembled at runtime; `IHangarPage.Art` already returns null on a miss |
| scrapbook art | 238 files | not classified | assembled at runtime; a `Snap_*` row is already skipped when the file is not on disk |
| paint `.BM` masks | 184 | not classified | assembled at runtime per pattern; 14 pattern folders, `FORTUNE` has all 62 skins, `BLCKSWAN` 5 |
| `CrimFlag.MPG`, `Final.MPG` | 2 | optional | `LAYOUT.CSV` names both under `ASSETS/GRAPHICS/MPG/`, where `RofFiles` answers the install's loose MPGs; `CrimFlag.MPG` is the top level's and Preferences' backdrop, on an 8.0 s loop ([`../formats/cinemas.md`](../formats/cinemas.md)), and runs behind the five Preferences leaves too, which author no row of their own and show it in every leaf still; a screen whose movie is missing draws whole without it |
| `MM_BackGround.png`, `MM_SplashBackground.jpg` | 2 | not classified | neither appears in any layout row, and the shipped `MM_BackGround.png` is a placeholder reading "CS BACKGROUND" |

**Not classified** means the manifest carries no entry: the name is assembled at runtime from a
fragment rather than written out, so there is no file list to check before entry. Those degrade
where they are drawn, and the pages that draw them already handle a miss.

⚠ **The original's own top-level backdrop is a movie the extraction does not carry, and the still
beside it is a placeholder.** Original's main menu therefore has no faithful background
available. That is an accepted remake-only rule to write down, not a missing asset to hunt for.

## Part 4: what the evidence does not cover

### Owed captures

Filed in [`playtest.md`](../../playtest.md) under **Menus and front end**. The first takes of all
of them were filmed with a silent audio track, so what they settled is the picture, recorded in the
paragraphs below, and what they still owe is listed per row; `CAP-49`'s row is retired, its open
halves riding `CAP-52`.

| ID | What it still owes | What it unblocks |
|---|---|---|
| `CAP-50` | VIEW STORY pressed on the Instant Action page | what that button opens, the one Instant Action state no take holds |
| `CAP-51` | the AUDIO page with sound, and a leaf re-entered after CANCEL CHANGES | the AUDIO page's preview against the decoded script, and whether a leaf's CANCEL CHANGES reverts a setting |
| `CAP-52` | the whole menu walk with sound routed, a keyboard and pad pressed on each screen, REPLAY BRIEFING and DELETE PLAYER pressed | A4's audio and input contracts, D33's cue work, the focus walk this page still calls a remake equivalence |
| `CAP-53` | the tab bar out of order, Purchase, the cleared default box, the two refusals | C23's Original hangar navigation |

### Where Original and the original disagree

Each of these is a divergence a reader could mistake for a decode, so each is named.

- **The original's top level has six rows, and none of them is Free Flight, Dogfight or a hangar
  door.** `[@MainMenu@]` authors Campaign, Instant Action, Multiplayer, Preferences, Credits and
  Quit. Free Flight is entirely ours; Dogfight is splitscreen where the original's
  equivalent is network multiplayer, and flies that match's map, the chapter's `MP1`; Original reaches plane construction where the original does,
  from the Instant Action screen's BUILD button and from the cabin, and has no top-level door.
- **Original Free Flight has no original screen behind it.** There is no free-flight script, no
  free-flight art, and no free-flight entry in the Instant Action mission-type dropdown. The
  remake-only rule under Decision 11 stands: Original's top level carries a Free Flight door of our
  design in the decoded chrome's conventions (the paper-plaque text button, the four state colours),
  opening the local lobby on the Multiplayer Lobby's layout, marked remake-only in the census above.
- **Original Dogfight and its join flow have no original screen behind them either.** The
  original's Multiplayer branch is network play (the 22 multiplayer scripts, none with a layout),
  and no script or layout row describes a second local player joining. The same remake-only rule
  gives Original a Dogfight door under the Free Flight door onto the same local lobby, its rules
  live, and a join board where a pad signs onto a seat (A on, B
  off, the captain's Start casts off), with the seats, picks and gate shared through the
  player-setup feature; all marked remake-only in the census above.
- **The wallet-free path's own verbs are partly remake-only.** Its totals page commits with
  Export, which the stills show and the shipped table carries as `IDS_PS_B_EXPORT` (`langui` 1139)
  for the inventory's own button; there is no export label authored for `PUR_B_PURCHASE`, so the
  id is borrowed. Its inventory drops the Export row entirely (Instant Action has nowhere to
  export to, every picker reading the one build store) and calls the removal Delete
  on the button, over the page and in the confirm, which no shipped string carries for a plane:
  700 and 1256 both price and name a sale. The confirm keeps the sale confirm's shape, the `0x4`
  two-button query box with Yes and No. It also leaves the picked plane's Value row (1258) unwritten,
  that row being a sale's worth: what the cabin path supplies and this door cannot is left out
  rather than invented, which is the same rule that checks no price against the $50000 note.
- **Original's wallet-free hub entry and its Instant Action loadout screen have no original screen
  behind them.** The original reaches plane construction from the cabin (`PC_B_PLANEX`, to
  `PlaneName`) and from the Instant Action screen's `IA_B_BUILD`, whose edge no layout row states,
  as no row states `IA_B_CHANGEWEAPONS`'s; the remake-only rule under Decision 11 has `IA_B_BUILD`
  open the same name screen with no wallet, the hub then wearing the export strips and the $50000
  cash note the Instant Action stills show, and `IA_B_CHANGEWEAPONS` open the `[@OrdinanceLayout@]`
  chrome over the pilot's or the wingmen's shared fit. Marked remake-only in the census above.

### What the Original presentation implements from the data, and what the captures must confirm

The main menu's interaction is built from what the layout itself evidences: a `B` row's four
stacked frames imply a rollover state under the pointer and a depressed state while pressed, so
the plaque under the pointer draws frame 2 and draws frame 3 while the button is held; the two
pointer bitmaps the globals script names are drawn as the pointer, the active one over a live
button and the passive one elsewhere; the two wavs the control library binds play on a rollover
and on a press. ⚠ **A main-menu plaque's three lettering colours are in the art, not in a colour
tail.** The `MM_B_` rows carry no `ResID` and so no `ColorActive`/`ColorRollover`/`ColorDepressed`
("Colors should only be specified if ResID is specified", the file's own comment block), and the
words are painted into the four frames: measured over the frames that differ, `MM_B_Preferences.png`
inks its glyph cores `FEDE91` at rest, `FFA741` under the pointer and `F83F3F` while held. Only a
row whose text the engine draws, a paper plaque such as `FC_B_CHANGEPLANE`, reads its states off a
tail. The film (`CAP-49`, `CAP-52`, both silent) shows the top level this way: the
movie fills the 4:3 frame edge to edge behind the plaques, loops on a cycle near eight seconds,
keeps running in phase behind Preferences and every leaf page, and nothing but the plaque under
the pointer changes; a plaque has three states that differ only in lettering colour, gold at
rest, orange under the pointer and red while held, the rollover arriving on the frame the hot
point enters the box; a press fires on the release, never on the button-down (Quit held for 1.4 s
did nothing until released); Multiplayer is a live plaque like the other five, and no take draws
any plaque disabled, which Original matches over its network door; nothing is drawn at `mm_t_title`; Quit ends the process on the release with
no confirm, as `MAINMENU.SCRIPT` terminates on `mm_b_quit`; and the top level arrives with no
plaque lit. The pointer is the active bitmap over a live button, a plaque, an edit box and an
open dropdown's rows, the passive one over a roster row, a scrap and dead space, with the hotspot
at the bitmap's top-left; the bitmap swaps on an enter or leave event only, so a pointer already
standing inside a button when a screen is drawn keeps the bitmap it arrived with. Preferences
draws the logo, the panel, the PREFERENCES title, the four doors with all four descriptions
standing at once, CONTROLS live, and RETURN TO MAIN MENU as its only exit, no ACCEPT or CANCEL,
with the movie playing behind it; a leaf's description text stands, following neither the pointer
nor a selection; a dropdown opens under its box and draws only as many rows as it has items; the
Video page stacks ACCEPT over CANCEL, the Keys page draws CANCEL left of ACCEPT, and Escape
inside an armed rebind cell binds Escape. Original takes all four from its own data: the exit
pairs at their authored corners (`VP_B_ACCEPTCHANGES` over `VP_B_CANCELCHANGES` on one column,
`GO_B_ACCEPTCHANGES` left of `GO_B_CANCELCHANGES` on one line, `KB_B_CANCELCHANGES` left of
`KB_B_ACCEPTCHANGES` for whoever builds that page) and an open list one row per item up to the
window its own `D` row authors (`GO_D_DIFFICULTY` 4, `AP_D_SQuality` 4, `VP_D_Device` 4,
`VP_D_Display` 8), past which it windows and scrolls on its own bar as every other list does. The
film cannot speak to that case: the original's leaves carry three tiers and two sound qualities and
never reach a window, while the Resolution row offers a size per mode the screen holds. The rebind
capture is the one deliberate departure: the original writes Escape into an armed cell, CSVM keeps
Escape (and the pad's Back) as the way out of one, by the user's decision,
since a keyboard seat must be able to abandon a capture without a controller. The chooser row
is ours, and a disabled door's frame is a state the original never shows. Original reads
the main menu's film too: a press arms the plaque it lands on and draws its held frame, and only
the release still on it activates anything, so a press released elsewhere fires nothing; the
pointer's bitmap is set on an enter or a leave and never recomputed from what a new screen put
under a still pointer; and `CrimFlag.MPG` plays behind the top level and Preferences,
the extraction copying the install's loose MPGs in and every leaf still showing it still running
behind the page. Nothing the film shows of Preferences reads differently on Original today.
Still unfilmed: every sound; whether any keyboard or pad focus exists at
all (no take pressed a key outside an edit box, so Original's focus walk stays a remake
equivalence); whether a setting reverts on CANCEL CHANGES (no leaf was re-entered after one);
whether Preferences is reachable in flight; and whether a disabled button ever draws or sounds.

The campaign screens are built from what the layout, the scripts and the briefing dialog evidence:
every `B` row's position and four-frame strip, the `D` rows' boxes, the listbox's window, the
scrapbook rows' region column, the two messagebox button sets and their rows, the roster
sub-script's two colours, the edit box's pre-fill and its two bound wavs, NEXT MISSION disabled
once the campaign is complete, the briefing's narration following its script's start count.
The film (`CAP-52`, silent) shows the campaign screens this way: a campaign plaque (a cabin
button, a briefing plaque, a flight check's paper button) carries the same three lettering states
a main-menu plaque does and fires on the release; a roster row, a contents row and a scrap show no
rollover art; the passive pointer stands over a roster row and a scrap and the active one over
the name box; the profile screen's name box shows a blinking red caret and takes non-ASCII
letters; the sell confirm is the two-button box reading Yes left and No right, the refusal the
one-button OK, and neither draws a focus; and every Back-shaped button lands on the screen that
opened it. Still unfilmed: which sound plays on a plaque, a row or a scrap, and what the edit
box's keystroke and reject sounds are attached to (Original plays `MOUSEOVER` and `MOUSECLICK` on
a plaque, nothing on a row or a scrap, `ENTERTEXT` per taken character and `ENTERTEXT_ERROR` per
refused one); whether REPLAY BRIEFING restarts the narration from its start (never pressed;
Original begins the wav again) and whether leaving the briefing cuts the voice (Original ends it
and brings back the music its stay held silent); whether a mission's end starts any narration on the book (Original
starts none); whether a scrap whose region column reads `0,0,0,0` is clickable on its picture
(Original hit-tests the picture's bounds); the delete confirm (never pressed; Original focuses
Yes); and whether any keyboard or pad focus exists on these screens at all and where Back lands
it (Original keeps the focus on the plaque that opened what is being left, a remake equivalence).

The Instant Action screen is built from what the layout and the script evidence: the widgets at
their authored positions over `IA_BackGround`, the contents list's 14-row window with its own
`UpArrow`/`DownArrow`/`Slider` art in the gutter the background paints for it (a dark bar over
x 336 to 354 and y 173 to 428, inside `IA_TL_Contents`' own X 76 plus Width 277, so the 16-pixel
scroll art stands at 337 and the rows stop there), the `Slider` tile drawn stretched to the share of
the list that window shows rather than at its own 11 pixels (the film's opening frame fills the
track from its head at board y 186 down to y 356, which is 14 of the 19 presets over the 230-pixel
track between the arrows), a dropdown's box `Width` wide and `ItemHeight`
high showing its picked value with the `DropDown` arrow strip, the enemy rows on two pages keyed off `IA_B_UP`/
`IA_B_DOWN` (the script's mailbox 20002), the ace duel blanking every enemy dropdown in place (the
script's `0 == WT` branch disables them with message `10000`, which blanks a box where
`deactivate` would remove it), the wingman plane blanking at zero wingmen, a changed
militia resetting its aircraft (`AV[BA].QG = 0`), stunt flying clearing the clouds
(`FUN_004103b0`'s mask), a contents row applying its preset on select (callback 2302) and View
Story writing the preset's name as the
story title (the `IDS_IA_STORYTITLE` format). The film (`CAP-50`, silent) shows the screen this
way: the contents list and the dropdown half show together on one background; the screen opens on
preset 0 with its row selected and every field reading that preset, not on stored defaults; a
rollover previews nothing, a hover highlight tracking the pointer beside the selected row; a
selected row rewrites the whole right page at once, the preset's name under HEY PILOT! included,
so VIEW STORY writes nothing new there (VIEW STORY itself was never pressed); a changed militia
resets its aircraft to the militia's first; a dropdown opens flush under its box at the box's
width with the current value and the hovered row both highlighted, draws only as many rows as it
has items, and scrolls with its own bar once the list outgrows the authored window (the
pilot-plane list at 21 entries in a 20-row window), that bar standing INSIDE the panel's right
edge with the row bands stopping short of it; a closed box lightens under the pointer; the
ace duel empties the four enemy boxes in place with pale arrows and keeps "[continued ...]" and
the down button live; at zero wingmen the wingman-plane box blanks while the Wingmen count stays
live and the radio pair draws exactly as it does with wingmen set, its Wingmen label as dark as
the Pilot one; the up/down buttons replace the whole right page with the three later waves under
"[go back ...]", a zero-count wave's boxes blanked; BUILD CUSTOM PLANE opens the plane-name
dialog and WEAPON LOADOUT opens AMMO SELECTION for
whichever of Pilot or Wingmen the radio holds, both live; and FLY MISSION opens a loading spread
titled by the mission type. Original draws each of those states, BUILD CUSTOM PLANE and WEAPON
LOADOUT live among them; the list's paper, the band under its picked row, the lighter band under
the row the pointer is on and the cream a closed box's outline takes under the pointer are measured
off the film, no layout row authoring any of them. It opens the page and names the preset
differently by the user's decision, a deliberate departure recorded here, not a defect. Still
unfilmed: VIEW STORY; whether keyboard or pad input reaches the screen at all (Original walks the
widgets as they are drawn and steps a dropdown's value sideways, a remake equivalence); and every
sound.

The plane-construction screens are built from what the layout and the stills evidence: the
`[@PlaneConstruction@]` chrome and the six tab sections at their authored positions over
`PX_BackGround`, the seven `0x1100` edges as a tab bar (every tab a sibling, `PX_B_Ready` to
Purchase), the `PX_Tab` strip's four frames and its `ColorDisabled`/`ColorActive` label tail, a
dropdown's box `Width` wide and `ItemHeight` high with `TotalDisplayed` as its window and its
`DropUp`/`DropDown` arrows, the `PX_ICON_<airframe>_<pattern>_0..3` layers as the plate and three
region masks (the paint composite the stills show), `PT_P_DECALS`' fifty frames as the decal
tiles, the cabin's `PC_B_PLANEX` edge to `PlaneName` first, the `PN_B_DEFAULT` box authored
checked, langui 203 as the name screen's refusal and 206 as the airframe-switch ask, and the
economy and paint tables (`hangar.md`). The film (`CAP-50` from Instant Action's door, `CAP-52`
from the cabin's, both silent) shows the hub this way: the name screen is a dialog over the
blueprint page with Load Default Configuration drawn checked, and checked it opened on a Balmoral
from Instant Action and a Bloodhawk from the cabin, both complete stock builds, which the decode
settles as the stock build of whichever plane was current at the door (`hangar.md`, "What Load
Default Configuration loads"); the standing tab draws raised and pale
with dark lettering, the other five purple with white, and nothing is gated; all six write their
label on one baseline eight pixels clear of the strip's bottom edge, measured over the six tabs of
`OriginalScreenshots/CustomPlane Armor.png`, which is where the squat frames' plaque stands (the
`PX_Tab.png` frame is 36 pixels tall and its rest and rollover frames are opaque over the bottom
twenty rows alone); the running total is
the PLANE COST line on the page's top rail beside the plane name, following every pick; a
dropdown lists flush under its box, the airframe list at eleven rows with no bar, and the decal
picker is a five-wide thumbnail grid with its own arrows; the Instant Action door wears READY TO
EXPORT and CANCEL EXPORT with a `$$$ on $50000` scrap on every tab, the cabin door READY TO
PURCHASE and CANCEL PURCHASE with `$$$ on $16780`; SELL PLANES opens the `[@Hangar@]` INVENTORY
with a plane dropdown, Sell and Export both drawn live, a value block and DONE back to the tab,
where selling asks with the Yes/No box and a plane that cannot be sold refuses with the OK box;
no airframe switch raised string 206 on a fresh build, which the decode settles as the rule rather
than the take's luck (`hangar.md`, "When the airframe swap asks"); CANCEL drops every pick; and the
weight line turns red over capacity. Original draws all of that, the weight line's red included,
with the cost line reddening on the same script's other arm past the wallet, and the swap's
question as the script's own three-button box. Still
unfilmed: tabs visited out of order (both walks ran 1 to 6); Purchase and whether leaving a tab
commits (Original commits only on `PUR_B_PURCHASE`); the cleared default box (Original starts on
a bare airframe); string 203; and whether keyboard or pad input reaches the hub at all
(Original walks the page's rows and the tab bar as they are drawn and steps a dropdown's value sideways, a remake
equivalence).
- **Original's local Dogfight rules are the lobby's, placed by us.** A split-screen Dogfight has no
  authored screen, so its rules stand on the Multiplayer Lobby's own mission rows and open on that
  lobby's values; which rows stand and which grey is our choice (the census row above).
- **The cabin's Plane Construction button goes to `PlaneName` first.**

## The decoded layout

`RofFiles` builds `menu_layout.json` in memory (`MenuLayoutDecoder`): the 34 sections bound to their scripts,
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
