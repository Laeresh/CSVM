# UI

The launchscreen and splitscreen rig, the in-flight pause and results boards, plus the interactive debug labs. Six sub-namespaces, one folder each, beside the `UI.Menu` presentation tree (`docs/menu-presentations.md`), and one page for all of them: `UI.Boards` (the widget library every screen draws with: the composed board, its view, fit and palette, the board menu and the seat input it polls, the splitscreen rig and the canvas-layer order), `UI.Campaign` (the campaign pages, their flow and the scrapbook), `UI.Screens` (launch, boot, cinema, load, pause, results and wrap-up boards), `UI.Hangar` (the hangar pages and the plane-picking tables they share), `UI.Overlays` (the debug and HUD overlays) and `UI.Labs` (the inspection labs). Nothing else in `UI` names `Labs`, `Hangar` names only `Boards` and the shared `UI.Menu` (`CSVM.Tests/FamilyOrderTests.cs` holds these rules), and `Campaign`, `Screens`, `Overlays` and `Labs` are built from `Boards`. Every lab has a scripted `--debug-*` twin so a finding can be reproduced headlessly; see `docs/cli.md`. The module index in `docs/architecture.md` groups the entries by sub-namespace.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/UI/Screens/LaunchMenu.cs
The Built-in presentation's launchscreen: one CanvasLayer holding the whole screen graph and every
Godot control behind it. Mode leads to Chapter and Plane for Free Flight and Dogfight (whose Chapter screen also steps Dogfight's two match rules as rows below the maps), and to
Instant Action's own wizard; the join board, Options, Controls, hangar and campaign doors hang off the same
graph. The join board is the one screen a pad signs onto a seat from, through `MenuSeatDevices`' board gestures. It owns the drawing, the per-seat `MenuInput` polling, the board scan, the screenshot key and
the mouse (player 1's rows take Godot's hit test through `gui_input`, folded into the next frame's
step, Accept and Back), and nothing else: rosters, seats, picks, gates and the typed exit are
the host's features (`Menu/MenuHost.cs`), the layout is `MenuZones`, and the hangar and campaign
screens are `HangarFlow` and `CampaignFlow` drawn through `ComposedBoardView`, whose `Film` owns a frame before any screen reads it. On the campaign boards player 1's L / Y opens and closes the co-op network door, Private and asking no password, whose band and remote guests' chips ride the chip strip; a co-op guest's Continue leads to the Network screen's waiting mode. Its Ammo Selection rows stand on the flown build's own fit, and `AmmoPylons` leaves out a pylon that build never bought, since the original draws no field for one. Contract: [../menu-presentations.md](../menu-presentations.md).

## src/UI/Boards/MenuZones.cs
How the launchscreen's three bands divide a window: a header and a footer held at the heights their
own metrics ask for, the middle taking the rest, and one scale all three share, capped so the
tallest screen still fits instead of losing its footer. Pure and public, so the division rule tests
with no menu instance behind it (`CSVM.Tests/MenuZonesTests.cs`).

## src/UI/Menu/InstantActionPresets.cs
The original's Instant Action Table of Contents as data: the 19 preset scenarios, in the shared menu
namespace beside the feature that applies them. Each row is transcribed by NAME rather than by the
record's own dropdown indices, so it diffs line for line against the decode; `Resolve` does the
name-to-index step against `InstantActionFeature`'s rosters, every presentation's single source of
order. Presets fly stock airframes, so nothing here waits on the hangar. The record layout, the
offsets, the sentinel substitution for an unused wave slot and each preset's configuration are in
[../formats/instant-action.md](../formats/instant-action.md), "Table of Contents presets".

## src/UI/Hangar/PlanePickerRoster.cs
The picker roster rule behind every human plane pick, engine-free so it tests without a menu
instance: `Build(stock, customs)` lists the stock rows in their given order, then one row per saved
`CustomPlaneDef` in the store's name-sorted order, each carrying its store name and its airframe's
stock node off `Flight/Hangar/StockAirframes.cs`, skipping a campaign plane nobody has exported.
`IndexOf` is the after-build auto-select's case-blind lookup. Deliberately not `Session.Roster.HumanFieldPlanes`, which answers "which plane does player N
fly" off a `SessionSpec`: this is the menu-side list, that one the session-side read. Tests:
`CSVM.Tests/PlanePickerRosterTests.cs`.

## src/UI/Hangar/HangarFlow.cs
The Build Custom Plane flow, Built-in's walk of the shared `HangarFeature` (`Menu/HangarFeature.cs`),
engine-free the way `BoardMenu` is: the launchscreen owns the Godot controls, the feature owns the
scratch plane, the rules and the store operations, and this file owns the screen order, the cursor
and the pages. `Order` is the original's nine screens and `PageFor` maps each to its `IHangarPage`,
the mount point handing the shell rows, a detail line, a stepper, the totals line's subject, each
row's would-be cost and optional art; over a campaign the flow adds the wallet line beside the
totals and the mark on a row the funds cannot cover. Nothing is written until `Commit()`, so
cancelling is residue-free. Screens, economy and the cash note: [../org/hangar.md](../org/hangar.md).

## src/UI/Hangar/Hangar*Page.cs
The eight hangar screens the flow walks, one file each, every one a `HangarPage` editing
`HangarFlow`'s scratch plane and priced through `HangarEconomy`: airframe (eleven rows, raising the
defaults ask on a swap that changes an edited build), engine (the airframe's six plus the explicit None row), armour (four zones
on the dropdown's units-times-five scale), guns (four slots stepping the eleven-entry calibre
cycle), hardpoints (a count per wing), paint (a pattern, three colour and shade pairs and three
decals over a live preview), name (two word lists, or typed over) and purchase (the itemised bill
and the gate in the original's own words). The plane-selection screen is `HangarFlow`'s own.
Strings, dropdowns and prices: [../org/hangar.md](../org/hangar.md).

## src/UI/Hangar/PlaneNameTables.cs
The two word lists the PLANENAME screen composes a name from, and the roll across them. Authored
fiction rather than a decode, which is why it is code and not a data table: there is no original
table to diff against, and an unreadable file would leave a pad with no name to offer. Every
adjective is meant to read against every noun, so the pair needs no compatibility table.

## src/UI/Hangar/PlaneDiagrams.cs
The two plane-diagram sheets the original draws beside a fitted aircraft: a plan view and a head-on
view, each one tall PNG of eleven equal frames in airframe-id order. `Frame` slices one airframe's
frame out of a sheet, and a sheet whose height is not a whole multiple of the airframe count draws
nothing rather than a mis-sliced picture. Shared because ammo selection, the campaign's flight
check and the hangar's airframe list all want it; decodes are cached per process, misses included.

## src/UI/Hangar/PlaneFit.cs
What one campaign aircraft is carrying: the four gun slots, barrels by calibre, hardpoint count,
armour units and engine id. Resolved from its hangar build where it has one and from the stock fit
where it does not, which is the case for the profile-seeded starters and every granted reward
aircraft. Engine-free, so the screens that print it test off engine. The caller resolves the build,
never this class. The wallet and the award templates: [../org/hangar.md](../org/hangar.md).

## src/UI/Hangar/PlaneRatings.cs
The four ratings the plane selection screen prints beside an aircraft, each a 0-to-4 index into
langui 501-505, Poor to Excellent. All four are the original's own integer arithmetic over one plane
record and its airframe's stat row: the engine's power for speed, the armour units, the agility stat
and what the armament weighs for offense. The four formulas and the aircraft they reproduce:
[../org/hangar.md](../org/hangar.md), "The four rating words".

## src/UI/Campaign/CampaignFlow.cs
Built-in's campaign screen graph as one engine-free flow over the shared `CampaignFeature`
(`Menu/CampaignFeature.cs`), the same split `HangarFlow` makes over its own feature: the feature
owns the profile, the seated player and every write into the store; a page owns its rows and its
navigation; the launchscreen owns every Godot control. Screens are a stack rather than a fixed
order, since the campaign's navigation is a graph, and `Registry` maps a `CampaignScreen` to its
page factory. A page contributes pictures, strokes and captions and names which authored button
each row presses; `CampaignBoards` supplies the geometry through `Layout`, which is Built-in's
alone. `Modal` and `Message` are the dialog and the refusal band every screen shares; `OpenCabin` is every door onto the cabin, RETURN TO CABIN and the back press included, and `OpenScrapbookAfterMission` the mission end's door onto the book, each playing one of the feature's two cinemas through `Film`, the span (`Video/CinemaHandoff.cs`) a polling presentation reads before it applies a frame. The cursor walks past a row its page refuses (`Focusable`), which is how ammo selection's fieldless slots are skipped, and every cursor move closes each open drop-down but the focused row's, so a pointer that moves the focus leaves no list standing.

## src/UI/Menu/CampaignFlightField.cs
Owns a campaign sortie's humans as part of the shared `CampaignFeature` (`Feature.Field`, in
`CSVM.UI.Menu` so either presentation walks the same field): joined count, the flight check showing,
and each guest's aeroplane. Player 0 keeps the seated profile's aircraft; each later player picks
from session-scoped copies of the hangar's planes and a stock Devastator record (`StockChoice`), so
nothing a guest flies is the profile's own record. `Holder` and `HolderOf` name the seat flying a
plane, by `Session/Campaign/CoopPlanePool.cs`'s rule, and `Choose` refuses a held one. A guest with
no pick of its own opens on the first free plane; at a network guest's machine the field is its further players, picking from the host's hangar. `Advance`/`Retreat`/`Rewind` walk one reused flight-check page through the field, and
the seated player's first FLY MISSION latches `Locked`.

## src/UI/Campaign/Campaign*Page.cs
The ten campaign screens, one file each, every one an `ICampaignPage` over `CampaignFlow`: the
player roster with its name field and confirmed delete, the cabin hub, the memento chooser its wall
opens over `Session/Campaign/CampaignMementos.cs`, the previous-missions contents list, the briefing with its
revealed map and parchment note, the flight check, ammo selection, plane selection with its ratings
and export, the scrapbook and one scrap's zoom view, whose torn mount over a photograph is the
book's sepia. Each names its own `LAYOUT.CSV` script and reads geometry through `CampaignLayout`, so
a page holds rows, detail text and its own refusals and nothing about pixels. The chrome:
[../org/campaign-board.md](../org/campaign-board.md) and [../org/debrief.md](../org/debrief.md); the scripts: [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/UI/Campaign/CampaignCombo.cs
A campaign screen's drop-down field (`PS_D_PILOTPLANE`, `OL_D_AMMO0`): the authored rectangle, the
row height and visible row count the list opens at, the window that scrolls when the cursor leaves
it, and a closed field's horizontal step. It never moves its own pick; a candidate comes back and
the owning screen selects, because some screens refuse some picks.

## src/UI/Campaign/CampaignModal.cs
The dialog a campaign screen raises over the composed board, the original's `messagebox.script`: a
message, one button and the callback its answer runs. Held by `CampaignFlow` rather than by a page,
since two screens reach the same box.

## src/UI/Campaign/CampaignTextEntry.cs
A campaign screen's one-line text field, the original's `cm_e_name` edit box: typed from the
keyboard and stepped through one alphabet from a pad, so the field needs no keyboard at all. What it
accepts is the shared feature's own name rule, so a stepped or typed name is always one the feature
would seat.

## src/UI/Campaign/CampaignAidScript.cs
The input script a campaign screenshot aid's `--menu=` colon argument spells, replayed on the flow
where the walk left it: counted cursor verbs, a confirm, a back and a secondary press, joined by
`-`, plus a word naming a `BoardButton` to focus and confirm. A confirm is what lets an aid leave a
drop-down standing open, which a step count could not reach. A count with no verb after it is a run
of downs, so a bare number is the step count it always was, and `export` is a button word rather
than a case beside the language. One grammar serves both presentations, and the replay refuses a
script spelling a press the running one lacks, which is how the secondary verb fails loudly under
Original. `docs/cli.md` states the grammar for the command line.

## src/UI/Campaign/ScrapbookComposition.cs
The scrapbook's per-spread scrap layout, read from the shipped `SCRAPBOOK.CSV` rather than invented:
`Items` walks a spread from item 1 and stops at the first missing key, the way the original's reader
does; `Pictures` gates each row against the mission's merged best-to-date mask and stacks the
survivors by draw order; `Openable` narrows the same gate to the rows that open a detail view;
`ZoomFamily` reads a family's three text boxes, each with the height its row authors beside its
width, which is what the zoom page's blocks are fitted against. A player capture resolves through a caller-supplied
path rather than the asset library, is forced into the page's own 164x123 region so the smudge cut
for that rectangle lands on the print, and is skipped when no file is there. Parsed rows are cached per file behind a lock. The columns and the gate: [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/UI/Campaign/ScrapbookExport.cs
EXPORT TO DESKTOP's copy: the open scrap's own file to the desktop under its own base name,
overwriting, answering whether it landed and either the name or the OS reason, which are langui
705's and 706's arguments. Engine-free, and the folder is a parameter so a test writes elsewhere.

## src/UI/Boards/LanguiFace.cs
A langui `[FONTID]` tag read as a typeface: the family letters resolved to the Windows face they
abbreviate, the point size, and the bold and italic suffixes. `Pixels` is the size in board pixels at
96 dpi, which is also the pitch the original sets that face's wrapped lines at. Engine-free; the
renderer decides whether the machine has the face. The tag table and the size rule:
[../formats/strings.md](../formats/strings.md).

## src/UI/Boards/ListWindow.cs
A scrolled list as a pointer sees it, in the board's authored pixels: the window's box, the thumb's
box on its track, and where the list stands inside it. `TopAfterWheel` steps the window by rows and
`TopAfterDrag` maps the thumb's free run down the track onto the rows the window can move, both
clamped. `ThumbHeightFor` and `ThumbYFor` are the one rule every list draws its thumb by: the share
of the list the window shows, floored at the scroll tile's own height and capped at the track, so a
thumb's length says how much is in view and a longer one shortens the run the drag divides by. Each
list widget builds one from its own geometry and the presentation that owns the pointer decides what
a new top writes back, so the arrows and the keyboard keep their own rules.

## src/UI/Boards/SliderTrack.cs
A continuous control's track as a pointer sees it, the list window's opposite number: the slot a
thumb slides along, the thumb's own size, and the whole numbers the slot spans. `ValueAt` reads a
pointer's X as a value and `ThumbX` puts the thumb back where it read, so the drawn thumb sits under
the finger that moved it; `Stepped` moves a value sideways. Every answer is clamped into the range
and none wraps, which is what keeps a step away from an end from landing on the other one. Each
slider widget builds one from its own geometry and the presentation that owns the pointer decides
what a new value writes back. Driven by `Menu/Original/SliderControl.cs`.

## src/UI/Boards/BoardFit.cs
How the original's fixed 800x600 campaign dialog space lands on an arbitrary window: one uniform
scale on both axes, the board centred, the remainder letterboxed. A `record struct`, so every
element goes through the same mapping and only the scale changes; a viewport with no area falls back
to 1:1 rather than a scale nothing can draw at. The rejected alternatives and why the art is sampled
nearest are in [../org/campaign-board.md](../org/campaign-board.md), and bind every campaign screen.

## src/UI/Boards/AuthoredPointer.cs
A keyboard seat's mouse on a board drawn in the authored 800x600 space: the viewport pointer
mapped back through `BoardFit` into authored pixels, with the left button, and none for a pad seat
or a board out of the tree. The Original race and pause boards (`Menu/Original/OriginalRaceBoard.cs`,
`OriginalPauseBoard.cs`) hand it to `BoardMenuPointer`. `PausePreferences` and the Built-in
`PauseBoard` read their pointer their own way, raw and capture-aware.

## src/UI/Boards/ComposedBoard.cs
What a composed campaign screen is made of, engine-free: the screen's fixed backdrop, the fills a
page paints on it, pictures at authored pixel positions, connector strokes, text lines, button
plaques and flowed list widgets, each in draw order. The backdrop is its own layer so a fill can
sit over the background and stay under the page's pictures, where a selection bar goes. `BoardNote`
is a widget's entries plus its wrap box (cut at a word where the box has no room for the rest, shrunk
to a face the whole list fits in, or no box at all where the widget's own list stops nowhere), its
marks, and `BoardCaret` an edit box's cursor on the line it follows, all placed by a caller that can measure text. A `BoardLine` carrying a `Height` names the box its block is fitted to, the renderer stepping its face down until the block fits, since only the renderer can measure; one marked `Marquee` stays on one line and scrolls inside its width when wider, and one marked `KeepEnd`, an edit box's, is clipped to its width showing its end. `PlaqueFrame` and `PlaqueInk` are a plaque's states, and a plaque whose art leaves
part of its frame empty carries its label's own baseline. `BoardArt` names a file and its frame count and the renderer resolves it; one of its libraries is a movie, so a background film reaches the backdrop with no engine type here, and one is an image already in memory (`Held`), a stunt photograph's thumbnail. `BoardCrop` takes a region of the source instead of the whole frame, which is a chart sheet's own window. A picture's scale and spin act about its middle, or about its authored corner where it is marked `FromCorner`, as a script pane's `scale()` and `rotate()` do (the cabin's memento). A `BoardLine` carrying a `Glyph` names the pad button drawn where `GlyphSlot` stands in its text, the renderer owning the gap after the picture since only it can measure one, and a `BoardFill` carrying an `Ink` takes the palette's colour instead of three bytes, which is what a seat chip paints with.

## src/UI/Boards/BoardMarquee.cs
The scroll of a one-line caption wider than its box, engine-free: `Offset` is how far it has moved
at a clock reading, resting `HoldSeconds` at the start, scrolling at `PixelsPerSecond` until its end
shows, resting and scrolling back, and 0 for a caption that fits. Both numbers are TUNE, chosen to
read calmly on the KEYS AND BUTTONS page. `PinnedSeconds` holds every marquee at one phase: the
launcher pins the start under `--det` and `--run-tests` and any phase for `--debug-marquee=`, so a
capture never depends on how many frames ran. The renderer is `ComposedBoardView.cs`.

## src/UI/Campaign/CampaignBoards.cs
The fixed chrome of all eight campaign screens, plus the composer that turns a page and a cursor
into a `ComposedBoard`. Every button slot, background pane and text slot names its `LAYOUT.CSV`
section and row and reads through `CampaignLayout` with the value the board drew before the layout
existed as its fallback, so a screen composes the same with or without the file; the briefing's
chrome is `Briefing.zrd`'s own, and a slot marked pinned keeps a measured value instead. `SlotOf`
and `DialogSlot` answer a plaque's rectangle for a pointer to hit-test, `DetailSlot` and
`DetailPaned` the description panes, `DialogChrome` the messagebox widget set a box draws and
where its pane lands, and `Palette` the `BoardPalette` a screen writes in. The pinned values: [../org/campaign-board.md](../org/campaign-board.md).

## src/UI/Campaign/CampaignLayout.cs
The decoded menu layout as the campaign boards read it: one widget row's authored geometry and art
by section and key, every read taking the value the board drew before the layout existed as its
fallback. Engine-free, over `Menu/MenuLayout.cs`. `At` and `Box` answer with the whole row or the
whole fallback, never one coordinate from each, so a row missing a column cannot shift an element
half-way. `For(dataRoot)` reads the extracted layout once per data root and keeps it; a missing or
unreadable file is the `Fallback` instance with its reason logged once. The file's own sections and
keys: [../formats/menu-layout.md](../formats/menu-layout.md).

## src/UI/Screens/InstantActionWrapupPage.cs
The Instant Action wrap-up page's own content, engine-free over `CampaignLayout`: the heading, the four title/value pairs at `[@IA_WrapUp@]`'s authored rows, the magazine spread and the four
brushstrokes, the CONTINUE plaque's art and corner, and three pieces of remake furniture. `PostIts` writes the further lines the shipped page has no row for (the context naming the chapter and the
mission type, and the stunt run's splits without their total, which is the time row's figure again) onto yellow post-its: the first under the last value row and clear of the plaque, each further
one to its left, the lines shared out evenly and each post-it as tall as what it holds. `Prints` lays a stunt run's photographs out in marker order to the left of the post-its, in the grid `ShotGrid` picks for the room. `TickStrokes` draws the outcome as a box above CONTINUE, ticked on a win and empty on a loss. Every value is
read off the `IaWrapupSnapshot` the ending froze and never recomputed; the geometry is read off the page's own rows, so a layout that spaces them differently moves the furniture with them.
`Sample` and `LongSample` are the stand-in runs the `--menu=` aids and the coverage walk stand the page on. What the four numbers count:
[../formats/instant-action/wrap-up.md](../formats/instant-action/wrap-up.md).

## src/UI/Boards/ComposedBoardView.cs
The Godot half of the campaign boards: draws one `ComposedBoard` over the whole window through
`BoardFit`, with texture filtering pinned to Nearest so the authored pixel grid stays hard. Owns the
texture cache and the only art resolution there is, mission art and screen chrome under their own
extraction roots, and caches a miss so an absent extraction is probed once per name. A movie resolves
to a `MovieSurface`, whose one texture the cache holds and the surface rewrites in place, so the
picture animates with nothing invalidated; a held image gets one texture per image, dropped once a shown board stops drawing it; `AdvanceMovies` runs their clocks off the caller's own step and
`AdvanceCaret` blinks a text cursor off it and `AdvanceMarquee` scrolls an overflowing marquee line, each saying whether to repaint; a marquee line is drawn through the text server with whole glyphs clipped to its box, so it keeps its place in the draw order under overlays and the pointer, and a `KeepEnd` line wider than its box is drawn the same way shifted left by `EndShift`, so the text's end and the caret after it show. A line naming a `LanguiFace` draws in that installed Windows face, cached per tag, and keeps the board's own where the machine lacks it; a pitched block honours authored line breaks and indents and justifies as a whole, its lines left-aligned under the widest. Supplies the font metric a flowed
`BoardNote` and a caret cannot take, `Fitted` shrinking a note's face until its list fits its box rather than losing a row and stepping a `BoardLine` carrying a box height down a point at a time until its wrapped block fits, `Block` being that measurement on its own, the two-line hint band a pad needs, and `ArtSize` for a caller that must clip against a bitmap's own authored width. `PresentMoving` is the one repaint a caller holding the frame loop can still make: its pictures go on a canvas item of the view's own, fitted by the same maths and re-fitted on a resize, rather than through a queued redraw callback the blocked loop would never reach, so a load screen's build can move the bar it draws. A line carrying a glyph is drawn through `ControlLine`, the composition the flight prompts already use, so the picture and the gap around it are measured in one place and a board composer never spaces them itself.

## src/UI/Screens/CinemaScreen.cs
One cinema on screen: a `CinemaPlayback`, the `ImageTexture` its pictures upload into, and the
`AudioStreamGenerator` its samples are pushed to on the Voice bus, a cinema being a narrated film
rather than score or world sound. The picture fills the same 800x600 rectangle `BoardFit` maps a
board into, so a cinema and the screen it hands off to own one area of the window. `Open` answers
null for a file that will not read, `Ended` is how a flow learns it stopped, and a `CinemaSkip`
set (`Video/CinemaHandoff.cs`) is which presses end it early: `BootKeys` here, the campaign films'
own `Keys` there. It mounts itself on `HudLayers.Cinema` and frees itself; `PlayCinema` is the seam.

## src/UI/Screens/CinemaSkips.cs
Which press ends a cinema, for every screen that offers a skip. `Skips` is the one member that
decides, and it takes a `CinemaPress` rather than a device event, so all three sets are pinned off
engine; `PressOf` is the engine's half, reading a key, a click or a pad button into one of those and
holding no policy of its own. A press is not a set: only an any-press set takes a key with no name
of its own. A pad button is in every set, because a player holding one has no other press to offer
and would otherwise sit through a 145-second film; it counts only where pad input does at all
(`--no-pads`, an unfocused window), since a pad reports its first button as it connects. What each
cinema's set is, and why they differ, is [../formats/cinemas.md](../formats/cinemas.md).

## src/UI/Screens/BootSequence.cs
`fmv.zrd`'s boot block with no engine in it: `Card` composes the copyright card in the authored
800x600 space out of the extraction's own art, message-table strings and font metrics, and `Run`
calls the block's eight actions in the reader's order over three injected delegates, a `CinemaPlay`
for a film, one that puts up a still and one that takes the card down as the first film starts. Every
name, position and duration is the reader's ([../formats/cinemas.md](../formats/cinemas.md)), which
is also where the card's one showing, the unseen fade and the films running back to back are
settled; `Held` is the one member that says how much of an authored hold reaches the screen.
`BootCard` supplies the stills, `Launch/Launcher.cs`'s `PlayCinema` the films.

## src/UI/Screens/BootCard.cs
The boot sequence's engine half, and the only file that knows a boot still is drawn at all: the
black the block runs on, a `ComposedBoardView` for the card, a countdown per hold, and the press
that ends a hold early, read through `CinemaSkips` against the films' own set so a still and a film
answer one rule. It mounts on `HudLayers.Board`, the launchscreen's own layer, so a
film at `HudLayers.Cinema` covers it; the card goes down with the first film and the black outlives
it, and a hold of no seconds runs on without a frame of its own. `Play` is the whole surface: it
mounts the node, runs a `BootSequence` over the caller's film call, and frees everything before the
handoff.

## src/UI/Boards/BoardPalette.cs
The ink a campaign board writes in, one palette per background family, because the screens are
painted art and the grey the flight check's forms use is invisible on the cabin's dark hangar. The
flight check and ammo values are their layout rows' own ARGB fields; the rest are chosen to read on
their background, and [../org/campaign-board.md](../org/campaign-board.md) says which is which.
`EscapeBlackboard` is the one crossing, the load screen's own chalk under the near-black labels the
escape strips' light plates need, which an Instant Action pause is the only screen to want both of.

## src/UI/Boards/ChromeType.cs
The type scale for chrome the original never painted, read by the join board, the in-flight
overlays and `FlightHud`'s text block, every results board, the pause board, the board menu and
Built-in's join strip and join board. It owns the face (the theme's default font, varied for italic and bold), one
size ladder (`ChromeSize`) in frame units, and metres for a printed distance. A frame unit is the
board's own authored pixel, 1/600 of the frame's height, so a composed board takes a rung as
authored and a surface stated at another reference (1440 for the HUD through `HudMetrics`, 720 for
a results board or the launchscreen) converts it through `InReference`. Painted original artwork
carries no type scale: `BoardPalette` and the composed campaign boards keep their layout's own sizes.

## src/UI/Boards/ChromeSize.cs
The rungs of `ChromeType`'s ladder, largest first: 72 for a start count's figure, 26, 22, 19, 17,
15 and 13 frame units for the boards, 11, 8 and 6 for in-flight text. Each member's comment names where the rung stands today.

## src/UI/Boards/SeatStrip.cs
The shape both presentations' player chip strip shares, so the two corners cannot drift apart: the
face, the inset from the top-right corner, the cell a chip centres in where a strip cannot measure
its own text, the ground's margin and height, and the `BoardInk` a seat's chip takes. The tag and
the colour themselves stay `SplitScreen`'s, and `ComposedBoardView` is what resolves a seat ink to
that colour. Built-in builds its chips as Godot labels (`LaunchMenu.cs`); Original composes them as
a board overlay (`Menu/Original/OriginalSeats.cs`).

## src/UI/Boards/BoardMenu.cs
A board's cursor and item list, engine-free so the selection rules test off engine. Holds no input
source: the board polls its owner through `MenuInput` and feeds one frame to `Handle`, which is what
stops a pad steering a menu it does not own, and the return says whether the highlight moved so a
board repaints only when it has to. It opens on the first item, so a board orders its rows with the
harmless one first and a stray confirm on a menu that just appeared cannot destroy a run. A results
board is not dismissable, since dismissing it would leave the player in a halted world with no way
back. `MoveTo` is how a board's pointer puts the cursor on the row under it, refusing a row outside
the list so a miss leaves the cursor alone. Off-engine coverage: `CSVM.Tests/BoardMenuTests.cs`.

## src/UI/Screens/LoadBoard.cs
The load screen drawn over the whole window while a session builds: `LoadScreens`' composition
through `ComposedBoardView`, so it inherits the authored-pixel surface and `BoardFit`'s scaling.
Populated in `_Ready`, since the view sizes itself off the viewport, and it tracks the window every
frame the way every shared board does. Holds no composition of its own, so what the screen says
tests off engine; a campaign launch hands it the `LoadSheet` its story position resolves. It is
also the pump `Utils/LoadProgress.cs` repaints through, installed for its own tree lifetime alone: it composes the board pumped and opens the moving layer on the cycle's first frame, then a step the build reports puts the bar's fill and the propeller's frame on that layer and presents there, which is how the screen moves at all, a queued redraw being no use while the build holds the loop that would flush it.
`--debug-load` stands the screen over a CLI launch and photographs each presented frame, the only way to read the bar back with nobody at the menu, and `_ExitTree` writes every reported step against the build's own wall clock as one line.

## src/UI/Screens/LoadScreens.cs
What the load screen is made of, engine-free. `LoadSheet` is the campaign screen's authored half,
one `Loading.zrd` dialog with its mission's objectives and the seated profile's own memento; `LoadScreens`
composes that chart sheet, through `MissionMap` the way `PauseScreens` does, the Instant
Action blackboard with the four texts its own `loading_i` dialog places, or a Dogfight's `loading_m` briefing as authored, keyed by `MultiplayerKey` (the original's environment number and mode letter); `DialogTexts` takes that composition by file and key, so an Instant Action pause writes its `ia_escape.zrd` dialog's texts through it. The mission type picks the
blackboard's dialog by the exe's own letter; free flight is ours, so it writes the
mode's name and nothing else. `LoadMotion` is the moving half, the fill strip and the six propeller frames the sheet's own `Cycle` beat names, `Moving` places those two at a fraction and a frame, the clipped fill and the propeller face a pump presents on their own layer, and `Painted` re-lays the same pair into `Overlays` for a caller composing a whole board, so the still composition under them is never rewritten. A pumped composition (`For`'s `pumped`) leaves the still propeller frame to that layer, the sheet's `Cycle` element excluded by identity, so a build shows one propeller; a still capture keeps the one still frame. An absent extraction yields the frame and the bar rather than
throwing, since this screen is shown while everything else is still loading. The dialogs, the beat
sheet and the face mapping: [../org/loading-screen.md](../org/loading-screen.md).

## src/UI/Screens/PauseScreens.cs
What the Original presentation's pause screen is made of, engine-free: the frame behind it, the
mission's chart at its authored source crop, the pins and icons its dialog's script places, the
objectives parchment, the memento, and the labelled button strips, the block's four plus the remake's own PHOTO MODE at the place that block leaves free. An Instant Action sortie's dialog carries none of that and draws the load screen's blackboard instead, its four texts composed through `LoadScreens` and its parchment left off by the dialog's own script; a Dogfight's `LoadMultiplayer` sheet is its mode's `escape.zrd` briefing, or `Loading.zrd`'s where that file numbers the row differently, with no propeller and `ia_escape.zrd`'s strips.
`PauseSheet` is the authored half, read once per sortie, and `PauseReadout` the live half, read afresh on every
pause: its memento is the seated profile's own picture, `Rows` marks a note line by the runtime's answer for that line's own objective number, and
`Icon` turns one world pose into the chart icon a session and a suite place alike, through the
shared `MissionMap`, which draws nothing for a pose off the window. `RowAt` is the pointer's hit
test over the five 132x28 plates, `Step` moves the pad and arrow cursor to the strip drawn in the pressed direction, and a pointer draws the dialog's own cursor; an unreadable extraction leaves the pause to the Built-in board. Decode: [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/Screens/PausePreferences.cs
The Preferences leaf over a paused mission: an `OriginalShell` of its own on the Options screen,
over the held world and drawn through `ComposedBoardView`, its display rows the `DisplaySettingRows`
both Options screens draw. Either pause board's PREFERENCES opens it, the pausing player's reader
drives it, and every door out closes it onto the sheet with an `OptionsApplyExit` already applied;
the halt is never touched. Its other features are throwaways and its `ControlsFeature` the menu's
own, whose `Accepted` it hands to the flying seats `Open` was given (`ApplyProfile`), so a rebind
or mouse scheme takes hold before the resume, as do the head turn and targeting switch it carries.
`Build` answers null with no decoded layout. Decode: [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/Overlays/MissionMap.cs
The one chart drawer every screen showing a mission's map shares, engine-free: the sheet as a
cropped picture, a reveal's visible elements as pictures in placement order over two layers, its
cycling element (`Cycle`), its connector lines as strokes, and one icon placed by world position through the map's own window,
turned so its drawn nose reads against the compass: the heading, less however far that bitmap's own
art is drawn off the top of the sheet. It exists as one module because the original reaches all of
it through one control class from two dialog constructors, so the briefing, the pause screen and
the campaign load screen cannot drift apart here.
Decode: [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/Boards/BoardMenuItem.cs
The rows a board menu can offer: Resume, Photo, Restart, Preferences and Exit. The board owning the
menu decides which it carries and what each does; Resume appears only on a pause board, Preferences
only where a `PausePreferences` leaf stands behind it, and Exit's label follows whether the session
can return to the launchscreen or only quit.

## src/UI/Boards/CursorRow.cs
One centred list row with its cursor marker, shared by every menu that has one: the launchscreen's
screens, its per-player aircraft panes, and every board menu through `BoardMenuView`. The marker is
a cell of its own with a mirror cell opposite it, which is what puts a label on the panel's centre
line whether or not its row is selected; the rule and its failure mode sit on the marker itself.

## src/UI/Boards/ControlGlyphs.cs
The per-control picture set, keyed the way `BindingControl` is: `GlyphKey` is a control's kind, its
index inside that kind and the sign an axis binding names, with deadzone and modifiers left out
because they decide when a control fires rather than what it looks like. `ControlGlyphSet` is the
swappable set (which controls it draws, how wide one is at a line's height, how to draw one) and
`ControlGlyphs.Set` the one holder every composing site reads, so replacing the whole look is one
assignment. The shipped `PromptFontGlyphs` draws pad controls as characters of PromptFont (SIL OFL 1.1, `CSVM/data/promptfont.ttf.bin`): its controller-neutral glyphs for the face buttons (the four-button cluster with the pressed one filled), the d-pad, the stick directions and clicks and the three menu buttons, and its Xbox-lettered shoulders and triggers, the font having no neutral ones. A button with no glyph, or every control when the font file is missing, draws as a lettered plaque; it declines keys, mouse buttons and hats, which is how a keyboard seat keeps `BindingLabels`' words. The file carries an extension Godot does not import and is read as bytes, so an export's `--import` leaves the tree clean and `export_presets.cfg`'s include filter packs it. The original ships no such art, so the set and its size on the line are judgements at the controls, not a decode.
`PadA`, `PadB` and `PadStart` are the three buttons named outright rather than through a binding, which is what the
join board's gestures need: a pad with no seat has no keymap for `For` to look a prompt up in.

## src/UI/Boards/ControlLine.cs
One prompt line with one control in it, and the row of them a board's footer is. `Compose` fills the
message table's `%1` slot through `Messages.Fill` with a sentinel, then splits the filled line, so
the halves either side of the control are exactly what a real fill would have written and no prompt
is ever built by concatenation; `Text` is the whole line in words, which is what a suite or a log
reads. `For` picks the binding through `ActiveDevice.PromptBinding`, so one seat names one device.
`Draw` writes a glyph-less line as a single string, the way a plain label always drew it, and only a
line carrying a glyph is drawn in parts. `ControlHintBar` lays several lines out in a row and
centres them in its own box; its items are composed one at a time so the device gate holds per item. `Around` builds the same line from a prefix, a suffix and a control named outright, for a caller writing its own words rather than filling a message template, and a line whose only content is its glyph still counts as something to draw.

## src/UI/Boards/BoardMenuView.cs
Draws a `BoardMenu`'s rows as `CursorRow`s inside the board style all five boards share, so the
cursor reads the same wherever it appears and a layout fix lands once. `Refresh` recolours from the
current highlight, touching only label overrides; `ShowsCursor` false draws no highlight while
the board's cursor stands on its photographs. A results board's footer is a `ControlHintBar`
over the seat's own Select and Confirm, composed off that seat's bindings and device rather than
off the shipped defaults, since nothing else on such a board teaches the cursor; `Relegend`
rewrites the row when the seat changes device. A pause board asks for no footer, as the original's
pause sheet carries none. `RowAt` is the pointer's hit test over the drawn rows, in canvas pixels.

## src/UI/Boards/BoardMenuHost.cs
`BoardMenu` plus `BoardMenuView` plus the reader, kept together so a board wires a menu in two lines
rather than restating the poll, handle and repaint order five times. `Build` primes the reader, so a
button still held from whatever raised the board is not read as a fresh press. It reads the pad's
back button alone, Escape and Start reaching the pause toggle through `FlightController` instead.
`Poll` can offer each frame first to a second cursor region on the board (a results board's
photographs), which the rows then do not read. A poll that reports the seat moved device relegends the view, which is the one seam that gives every
results board its control hint; `Build`'s legend flag is how the pause board declines one.

## src/UI/Boards/BoardMenuPointer.cs
The menu owner's pointer over a `BoardMenu`, engine-free, the one rule both pause boards read the
mouse by, each handing it a frame's pointer and its own hit test. Entering a row moves the shared
cursor there, so a pointer resting on a row leaves the pad free; a press takes hold of a row and
fires it through the menu's confirm when the button comes up still on it. `Prime` reads the button
as it stands, so a click still down from whatever stood over the board is not a fresh press.
`SettlesFirstSight` makes a fresh board's first pointer mark its row without entering it, since the
Built-in board's rows stand where a flight's released capture leaves the pointer.
Off-engine coverage: `CSVM.Tests/BoardMenuPointerTests.cs`.

## src/UI/Screens/ShotGrid.cs
The Danger Zone photographs' grid rule, engine-free and shared by the built-in boards'
`StuntShotStrip` and the Original page's `InstantActionWrapupPage.Prints`: `Fit` takes the fewest
rows whose pictures come within `Slack` of the widest any grid in the room allows, capped at the
thumbnail width, and the widest of those, so a few shots read as one strip and a long course wraps
before its pictures shrink. `ShotGridCursor` walks such a grid for a board: sideways in reading
order, up and down to the nearest cell by column, never onto a cell that refuses it (a frame not
yet landed), and off the grid on a step down past its last row.

## src/UI/Screens/ShotViewer.cs
One Danger Zone photograph shown large over the board that opened it, the one viewer both
presentations use: the camera's own PNG fitted to the window on a dark backdrop, with the marker,
the run clock and the way out under it, and the strip thumbnail where the file cannot be read. It
reads no device; the owner decides when it closes. A Built-in results board builds it to take
clicks and raise `Dismissed`, and the Original presentation builds it to take none, since the shell
polls its own pointer and closes it through the wrap-up page's rows.

## src/UI/Screens/ResultsBoard.cs
The shared shell every results board is built on (`StuntScoreboard`, `StuntRaceBoard`, `VersusBoard`, `IaWrapupBoard`): backdrop and centred panel, palette and label factories, the
halt-and-retire contract on the sim clock, and the standard Photo Mode, Restart and Exit menu, whose exit row reads Exit to Menu or `QuitLabel` unless a subclass hands `InitShell` its own words.
`RestartWithheld` leaves the Restart row off and draws its line over the other two. A panel taller than the window is re-centred and shrunk about its centre to fit. Photographs added
through `AddShotStrip` are the cursor's second region above the rows: up off Photo Mode (the
resting row) enters the grid, confirm opens one in a `ShotViewer` over the board, back or a click
closes it on its cell, and down out of the grid returns to Photo Mode. `PauseBoard` shares the
chrome statics but not the shell, since a held clock is not an ended run.

## src/UI/Screens/StuntScoreboard.cs
Stunt Flying's end-of-run results overlay on `ResultsBoard`'s shell: the plane and chapter heading
over a `StuntSplits` section of per-zone splits, total and best-time comparison, and under it the
pilot's `StuntShotStrip`. Wakes on `StuntMission.RunCompleted`, records through `ScoreStore.RecordIfBest` and logs the split
table so a headless run is reviewable. The one per-pane board among the results boards, which is
why it overrides the shell's whole-window placement. `FlightController` holds a finished pilot's
finish pose and takes R as a rerun only while it has this board, so an Instant Action pilot, who
has none, flies on through the ending's hold. Read `ResultsBoard` for the shared shell and
its halt contract, and `IaWrapupBoard` for the board Instant Action carries the splits on instead.

## src/UI/Screens/StuntShotStrip.cs
A stunt run's Danger Zone photographs as a board section, shared by `StuntScoreboard` and
`IaWrapupBoard`: one captioned thumbnail per shot in marker order, in the grid `ShotGrid` picks so
a long course wraps into rows. `Cursor` walks the landed cells for the board; the pointer reaches
them through `CellPointed` and `CellClicked`. It follows its `StuntCapture` in the tree: a marker
latched on the frame that completes the run arrives after the board woke and `ShotLatched` draws
the grid again with it. A pending cell is drawn empty and filled on `ShotLanded` (the first landing
under a guessed aspect lays the grid out again), and a frame that never arrived is left out.
Hidden while there is no shot.

## src/UI/Screens/StuntSplits.cs
The stunt run's split section, shared by `StuntScoreboard` and `IaWrapupBoard`: the per-zone rows
in the order flown with split and cumulative times, placeholder rows for zones never reached, the
total, and the new-best or stored-best comparison line. `Flight/Modes/StuntSummary.cs` is the value
a board hands it, one run with its total and the stored best, and its `Lines` is the same table as
flat text for the Original wrap-up page. A single flag keeps the two boards' shipped layouts apart,
since the scoreboard rules off its total and the wrap-up board runs the table straight into it.

## src/UI/Screens/StuntRaceBoard.cs
The time-attack race's shared Built-in results overlay on `ResultsBoard`'s shell: one row per pilot from `StuntRace.Standings()` with placing, callsign, plane, best time, gap to the winner and runs, a
pilot with no completed run showing their furthest run's zones and time to them (the columns' words are `RaceRows`', which the Original board shares; every column but the placing is headed), then each
pilot's best-run splits, a row per zone in course order. Whole-window, since a race ends for everybody at once, and built in Instant Action too, where the race rather than the mission ends a
multi-seat run. Wakes on `RaceCompleted` and retires once `Ended` clears, so a new window is reachable without the menu; `Rows` is the ranked text the suite reads. Its exit row is
`ExitLabel`: Back from a menu launch, which returns to the screen the race was launched from, and Quit Game from the command line, the label both race boards take; a network race's is `NetworkExitLabel`, Lobby on the host and Leave on a guest, whose board shows `WaitingForHost` in place of Restart. A pilot who left reads dim, marked by `RaceRows.NameText`. `StuntScoreboard` is the
single-pilot form; the Original presentation builds `Menu/Original/OriginalRaceBoard.cs` instead.

## src/UI/Screens/RaceRows.cs
The one reading of a stunt race's standings as board rows, engine-free: `Of(standings, zoneCount)`
gives each pilot's `RaceRow` (place, name marked when they left, aircraft, best, gap to the
winner, runs finished of started) and the `Racer` behind it for its seat and colour. `NameText`,
`BestText` and `GapText` are the column words. `StuntRaceBoard`, `Overlays/ScoresTable.cs`,
`Overlays/OriginalScoresText.cs` and `Menu/Original/OriginalRaceTable.cs` lay the rows out their own
way. It lives in `UI.Screens` because `UI.Boards` ranks below `Flight` in the family order.

## src/UI/Screens/VersusBoard.cs
The whole-window Dogfight results overlay on `ResultsBoard`'s shell: the winner in their own
`SplitScreen.PlayerColor` (`Title`, a team match's leading team, or a draw), ranked team rows,
then one ranked row per player with tag (a bot's callsign and `BotTag`, from the seat list `Build` takes),
score, kills and deaths from `VersusMatch.Standings()`; score is the ranked column. Wakes on `MatchCompleted` and retires on the rematch, its rows drawn
from that completion alone, so `Restart()` zeroing the live state never redraws them. Restart
routes through `VersusDirector.Restart`, which R and pad Y reach directly. A network guest's
board offers no Restart and reads `HostCallsTheRematch`, since that call refuses off the host;
Zeppelin vs Zeppelin keeps the row, which leaves for the lobby. `StuntRaceBoard` is its twin.

## src/UI/Screens/IaWrapupBoard.cs
Instant Action's wrap-up board on `ResultsBoard`'s shell, whole-window since the mission ends for
every human at once: four label and value rows for time to complete, enemies shot down, danger
zones completed and shot percentage. `GameSession` builds it and `InstantActionDirector` holds it as
an `IIaWrapupBoard`, handing in one `IaWrapupSnapshot`, so this class draws what it is given. Its static `FormatElapsed` is the decoded time row, which
the Original presentation's wrap-up page prints too. On a stunt mission it also grows a `StuntSplits` section and player 1's `StuntShotStrip` (the one live source, handed over at the wrap-up rather than the
ending so a marker latched after the run completed is on it), and no per-pane scoreboard is built. Its Restart reaches the Launcher's session restart
and rebuilds the world, because a mission's waves, ace and zeppelin cannot be put back in place.

## src/UI/Screens/PauseBoard.cs
The shared pause overlay, whole-window because pausing stops the game for everybody at once. Built
once by `Launch/SessionBoards.cs` on the shared board layer and wired to `PauseState.Changed` rather than a
completion event, it shows the pausing player's tag in their own colour and a Resume, Photo Mode,
Preferences, Restart and Exit menu driven by that player alone, since `PauseState` lets only the
owner resume; the Preferences row is built only where a `PausePreferences` leaf stands behind it. A fresh menu each pause, so the cursor starts on Resume and a stray confirm cannot destroy a run. Its
menu carries no control hints, the original's pause sheet having none. It shares `ResultsBoard`'s chrome but not its shell, and a campaign session's objectives readout rides
the same pause on a layer of its own. The pauser's mouse shares the cursor on `BoardMenuPointer`'s rule when they hold the keyboard seat, so a pad pauser's board reads no pointer. It writes no mouse mode, since the flight's halt releases the capture and the resume takes it again, and it is `Reprime`d when photo mode or the Preferences leaf closes.
The Original presentation puts `OriginalPauseBoard` in its place.

## src/UI/Boards/MenuInput.cs
One player's menu input source: the keyboard flag, a `Pads` binding and the edge and auto-repeat
state, with `Poll(dt)` filling the cursor axes, accept, back and start out of the `Menu` binding
context (`src/Bindings/`) from three readings of one seat: keyboard live, keyboard minus the
typeable keys, and the pad alone. Its pad rows sit on the seat-local `SeatPads` identity, since a
seat reads a set of pads and no binding may hold a connection index. `Typed` and `Erase` serve a
text field, `PadMove`/`PadMoveX` are the axes such a screen reads instead, since W, A, S and D
are letters there. `Typed` is read off `TypedText`, so each character is the one the pilot's own layout produced, and `Paste` is a Ctrl+V or Shift+Insert chord whose text a box reads through the `Clipboard` seam; `KeylessAccept` is an accept the keyboard half did not give, which a text field answers with `Utils/ScreenKeyboard.cs`; `TypeableKeys` names the US key positions text entry takes off the cursor bindings. `Device` and `DeviceMoved` come from an `ActiveDevice` over a fourth reading, the keyboard half alone, so a board hint names the side the seat last used and knows the tick it changed; `Hint` composes one such line. Wrapped by `Menu/BuiltIn/BuiltInSeat.cs`, bound by `MenuSeatDevices`; it also serves the in-flight boards. Beside all of that stand three static raw pad reads, `JoinPressed`, `SignOnPressed` and `SignOffPressed` for Start, A and B: a pad no seat owns has no keymap, so nothing bound can answer for the join gesture or the join board's two. Player 1 also reads the flight sticks, and its menu stick rows follow the active profiles (`Sticks/StickProfileSet.cs`); a joined seat never reads a stick.

## src/UI/Boards/TypedText.cs
The characters the keyboard typed as the pilot's own layout produced them, engine-free, which every
`MenuInput.Typed` reads. A polled key code names a US key position, so a German ':' (Shift and the
period key) read that way is '>'; only a key event's Unicode carries the character. The launcher
feeds `Live` from `_Input`, where no screen marks a key handled, dropping releases, auto-repeat
echoes and control codes, and counts paste chords apart as `Pastes`, since a paste is read off the
clipboard by the box it lands in. Each reader keeps its own mark, so two seats polled on one frame
both see a character, and one further behind than `Kept` is handed the newest.

## src/UI/Boards/HudLayers.cs
The canvas-layer ordering for everything drawn over the 3D view, in one place, so "does the collider
overlay draw above the cloud whiteout?" is answered by reading one file rather than nine literals.
The order is measured off the original's footage rather than chosen, except for the debug and lab
layers, which the original never had and which sit above the sun wash on purpose. The three negative
tiers are the world seen from the seat: the flare sprites and the cloud whiteout are the sky, and the
cockpit pass draws over both, since the original's whiteout is a fog term the interior never takes.
The burst wash stays above the pass, being a framebuffer effect over the whole picture. The evidence
for the wash-over-HUD ordering is a verification rule; the weather decode is [../org/weather.md](../org/weather.md).

## src/UI/Boards/SplitScreen.cs
The splitscreen rig for two to four players (one player never constructs it): the black gutter
backdrop, one `SubViewport` pane per player sharing the main `World3D`, and the player colour and
tag table. The main viewport draws no world while the rig stands (`Disable3D`). Two panes stack, or
stand side by side from 2:1 out (`SideBySide`); three and four are the 2x2 grid. **Every pane is a
3D audio listener**, or nothing positional is audible: Godot takes the per-channel maximum over
listener-enabled viewports. `Fill(true)` gives pane 1 the window for a cutscene; `NoteSkip` names a
skipper. Per seat, `OwnAirframeLayer` is dropped only by that pilot's disc and `FirstPersonLayer` only
by that pilot's pane (`SeatAirframe` writes both); no pane draws `SpyglassSunLayer`, the disc no `SunLayer`. Every camera draws `EveryCameraLayer`, the race ghost owner a seat flown elsewhere names.

## src/UI/Boards/ScreenFlash.cs
The full-screen colour wash, two channels over one hidden `ColorRect` per rendered view. The ramp
channel is the `FBFX_COLOR_FROM_TO` wash a close HE, AP or flak burst authors, lerped in RGBA on sim
time and then ended rather than held, replacing whatever that pane was running, and painted on every
pane whose own camera stands inside the burst def's authored player-range gate. The blend channel is
`BlendWash`, addressed by the struck aircraft's player index and painting nothing for an AI. The two
composite at paint time alone, the blend over the ramp, so a pane with no blend wash paints exactly
the ramp's own colour. One state per pane, never one global: in splitscreen each pane is its own
picture. The wash keys: [../formats/anim-definitions.md](../formats/anim-definitions.md).

## src/UI/Boards/BlendWash.cs
One pane's victim-routed wash state, the original's start and tick routines held per pane instead of
in their one global; pure state and arithmetic, no node, so the rules test off the engine. A first
hit takes its weight as the peak and starts the displayed weight at zero; a hit landing on a running
wash blends both the weights and the colour by the original's own arithmetic and restarts the
envelope without dropping what is displayed. The envelope is attack, sustain and release at fixed
fractions of the duration, stepped on sim time, with a hard cut at the end. `Composite` is the
paint-time rule `ScreenFlash` applies. Decode: [../org/ordnanceTypes.md](../org/ordnanceTypes.md).

## src/UI/Overlays/ObjectivesHud.cs
The flown campaign mission's objectives readout, drawn on the pause screen and nowhere else: the
original keeps its objectives on the pause parchment and leaves the flight HUD to the gauges. Reads
`CampaignDirector`'s `ObjectiveGraph` rows directly, resolves text through the message table, and
shows every row rather than gating on its awake flag, which the member explains; a row with no
message key is dropped from the drawing but still counted. A completed row is marked with the art
(`obj_check1`, loaded once through `LoadMark`) centred on that row's origin, over its leading
characters, and keeps the colour an open row carries. Self-mounting, and one per rig where
`PerfHud` is one per window. Decode: [../formats/objectives.md](../formats/objectives.md).

## src/UI/Screens/MissionEndFade.cs
A full-screen `ColorRect` on a `CanvasLayer` at `HudLayers.MissionEndFade`, polling
`CampaignDirector.LeavingFade` every frame and painting that straight onto the rect's alpha. That
layer sits above the flight HUD and `SunWash` but under `Debug`/`Lab`, so the fade darkens the HUD
and the wash the way the original's copied framebuffer does, while the debug instruments stay
readable through it. This paints live over the running world instead of freezing a copy, since the
hold already stops the sim clock underneath it. Self-mounting, one per rig's `HudParent`, hidden
until the director's fade leaves 0. Decode: [../formats/objectives.md](../formats/objectives.md),
"The mission-end path, and what the player sees after it".

## src/UI/Screens/SessionStartFade.cs
A full-screen `ColorRect` on a `CanvasLayer` at `HudLayers.SessionStartFade`, raised with the load
screen and painting `Utils/StartCover.cs`'s alpha until that ramp is spent. It shares
`MissionEndFade`'s tier, so it covers the flight HUD, the world and the sun wash while the world
assembles and while an intro's camera is still being posed, and the load screen on `Board` keeps
drawing over it. `Build` returns null under `--det`, which is the one gate: nothing stands over a
frame a golden hashes. The launcher builds it, drops it once `Finished`, and hands it the session's
own first-frame answer; `Tick` is public because a suite never yields a frame.

## src/UI/Labs/LiveryLab.cs
The `--viewer` livery editor (key L): a squadron stepper that loads the whole squadron livery,
per-slot RGB sliders, decal steppers, a random livery and copy-CLI-args.

## src/UI/Overlays/NodeLabels.cs
Floating node-name labels in both the static viewer and flight, in a meshes mode and an all mode;
`--debug-names` sets the mode at launch and is the only way in, the labels carry no key. In
splitscreen the nearest and de-clutter pick is player 1's viewpoint alone, while every pane still
renders the resulting labels, since they are ordinary world-space children of the root.

## src/UI/Overlays/DebugMarkerToggle.cs
The all-aircraft markers key (F16): writes one answer to `TargetHud.MarkAll` on every human pane,
the overlay `--debug-markers` switches on at launch. Session-level rather than one handler per
pane, because a splitscreen pane's HUD sits in a `SubViewport` that routes unhandled input to the
window and never to its own nodes. The panes are read through a closure, since a rig's HUD is built
after this node and can go away mid-session.

## src/UI/Overlays/MarkerOverlay.cs
The `--viewer` marker overlay (key K, `--markers` at launch): every firepoint, pylon and target on
the parked aircraft as a coloured gizmo with a billboarded label. Reuses `MarkerRig`'s own
classification and co-location grouping, so its gizmos agree with the marker dump by construction.

## src/UI/Overlays/ScoresOverlay.cs
One pane's held Display Scores, attached by `Launch/GameSession.cs` to every local pane of a race or
a Dogfight. Each frame it reads the pane's seat (`FlightController.ScoresShown`), so it stands only
while that seat holds the action and survives an airframe swap. Under the Original presentation it
draws `OriginalScoresText`'s lines in Courier New on the original's character cell at the decoded HUD
positions, with the flag column; under Built-in it draws `ScoresTable` as a chrome table centred in the
pane. `Flight/Hud/ChatPanel.cs` steps aside on the same reading, and so do the pane's top-centre
status lines (`StuntRunHud`'s status and leaderboard lines, `VersusHud`'s match line). Decode:
[../org/multiplayer-scoring.md](../org/multiplayer-scoring.md) "The in-flight scores".

## src/UI/Overlays/OriginalScoresText.cs
The original's in-flight scores as monospaced lines, engine-free: a Dogfight's header, team lines
and pilot lines in the decoded 21- and 7-character columns and order, the remake's kills and deaths
after them, each pilot line carrying the flag it holds, and a race's standings borrowing that grid for its own columns. `OriginalScoresWords`
reads the three header strings out of the message table. Capped at the HUD's 18 lines. Drawn by
`ScoresOverlay.cs`.

## src/UI/Overlays/ScoresTable.cs
The Built-in standings a held Display Scores shows, engine-free, in the mode's results board
columns: a race's place, pilot (marked when they left), aircraft, best, gap and runs, or a Dogfight's place, pilot, score,
kills and deaths with a team match's lines first. `ScoresSource` holds a session's race or Dogfight,
its seat names and flag carriers and the header words, and answers both looks, or nothing where no
mode keeps scores.

## src/UI/Overlays/PhotoModeHud.cs
Photo mode's only screen furniture and its way out: a hint line naming the bindings on a layer of
its own, and the Escape or pad-B read that raises `Exit` for `SessionBoards.ExitPhotoMode` to act on.
It decides nothing about the mode itself. The hint fades rather than persisting, since the mode
exists to compose a frame, and the fade runs on wall time because photo mode holds the clock. Pad
reads go through the seat's own device filter, so in splitscreen another player's pad cannot close a
mode that is not theirs. The mode itself is `Launch/SessionBoards.cs`'s.

## src/UI/Overlays/PerfHud.cs
The frame-cost readout (key F14, `--debug-fps` presets it): fps, the current frame's cost and the
worst recent frame, cycling off, compact and full. Built once by `Launcher`, never per session and
never per pane, since fps, frame cost and GC counts are process-wide facts; that hosting is also
what makes it work at the launchscreen, in the viewer and in flight alike. Fed the same raw
stopwatch cost `HitchMonitor` ticks on, every frame, so the worst-frame peak is already warm when
someone presses the key, and off by default so the golden screenshots stay byte-identical. Full adds
the frame's cost split, GC counts, breadcrumbs and a rolling frame-time strip, every term a second
view of data collected elsewhere rather than a new sample. What a frame number proves: PERF-1.

## src/UI/Screens/BuildStamp.cs
The build's version as `CSVM v<version>` in the menu's bottom-right corner, so a screenshot a
stranger sends carries the build it was taken on and the number is not read as the original
game's own. Two PromptFont icons left of it open the logs folder (the open log file's directory)
and Godot's user folder through `Utils/FolderOpener.cs`. They are mouse-only, never focusable,
and `Launcher` keeps their clicks from Original's polled pointer. Built once by `Launcher` beside
`PerfHud`, shown only while `MenuHost.OnMainMenu` holds or `NoGameDataScreen.cs` is up: deeper
screens have bottom-edge plaques it overlaps at a Steam Deck's aspect. It draws on
`HudLayers.PerfReadout`, above the boards. Pinned by `build-stamp-icons` and `build-stamp-focus`; the number is `Utils/BuildVersion.cs`.

## src/UI/Screens/ScreenKeyboardEcho.cs
A strip across the top of the screen repeating the field Steam's on-screen keyboard types into, its
label and its text with a caret, since the keyboard covers the lower half where a field such as the
lobby chat line is drawn. Built once by `Launcher` beside `BuildStamp`, on `HudLayers.KeyboardEcho`
above everything; it shows while `Utils/ScreenKeyboard.cs` names a field that asks to be echoed and
reads that field's text every frame. Original's boxes carry no label, so their text stands alone.

## src/UI/Overlays/NetReadout.cs
The `--debug-net` corner readout: a network match's desync counters as
`Net/NetInstruments.cs`'s `Describe` writes them, one section to a line. Built by `Launcher` only
under the flag, so an ordinary run and the golden sweep never build it, and fed once a wall
second from `Launcher.TickNetReadout`, which logs the same line. Hidden while no session holds a
wire. Drawn on `HudLayers.Debug`, unscaled, in the top-left corner.

## src/UI/Screens/NoGameDataScreen.cs
The screen a launch reaches instead of the menu when `ExtractionFlow.ProblemAt` names a problem:
no extraction (`Missing`, an absent or empty `extracted` directory), an unfinished run, or a stamp
naming another schema. Views follow `ExtractionFlow.View`: the install folder field with Choose folder, Extract
(focused) and Quit, plus Play anyway on stale data; the phase, bar and latest line with Cancel;
the failures with Try again and Choose another folder. Every press is a focusable button, so a
pad's d-pad and A drive it; Esc or B cancels a run and quits otherwise. A success hands back once
to `Launcher`, which re-resolves the data paths and enters the menu in the same process.
The picker's controller hint label does not wrap, since the dialog grows to its content's minimum. A pad's A or a tap on the folder field raises `Utils/ScreenKeyboard.cs`; focus alone does not.

## src/UI/Screens/ExtractionFlow.cs
The extraction screen's state, engine-free so a unit drives it with a fake runner. A stamp naming
another schema, or the `UnfinishedMarker` a run leaves until it succeeds, stops the launch; an
unstamped tree stays the boot's warning. The field starts with the remembered install while it is
still one, else the first found candidate, else the remembered path as a hint. Stale or unfinished
data re-extracts with `Force`, since the incremental rule compares file times, and adds `Unzip`
when the tree already has unpacked siblings the loaders would prefer. The run goes to a dedicated
worker thread rather than the thread pool, so a pool held by other blocking work cannot delay its
start; progress and its outcome cross only through `Tick`, once a frame. Success remembers the install.

## src/UI/Screens/InstallPicker.cs
Godot's own `FileDialog` in folder mode over the whole file system, never the native dialog, so
it stays inside the game window where a controller or touchscreen reaches it. The file-managing
extras are off because the install is only read. Godot's navigation covers the list; the left
shoulder goes up a folder and Y takes the folder shown, since focus cannot leave the list without
Tab. Hidden folders show outside Windows, where Wine and Steam prefixes live under dot folders.

## src/UI/Labs/MeshLab.cs
The geometry and shading lab (key M): normal lines, the smoothing-seam wireframe, collider boxes,
light sliders with a headlight, and cull by normal-source override cyclers, all scripted by
`--debug-mesh`. Two shapes: the `--viewer` lab owns the parked plane, and the scoped lab over a
`SelectionService` attaches to the current selection and restores on a change or a deselect. Its
override shader samples through `SceneBuilder.SampleAlbedo`, so a surface under the cull override
keeps the chapter's mip LOD bias and the lab stays a diagnostic twin of the real arm.

## src/UI/Labs/WeaponLab.cs
The weapon lab's panel (`--weapon-lab`, key B): a configurator for the held aircraft's live loadout,
hosted top right in flight. It owns no weapon and fires nothing; the steppers write into the
controller's bound loadout and the aircraft's own trigger fires it. Gun steppers arm a gun group,
hardpoint steppers re-arm every pylon, and "reset to stock" restores the fit the session launched
with. A left click casts the lab's own ray, names what it hit and re-parks the held plane on that
ray at the panel's stand-off; the scripted twins all end in the same placement. V hands the rig's
camera to a spectator camera and back. In splitscreen the lab stays player 1's alone, one panel on
rig 0's aircraft, and a log line says so while the other panes fly normally.

## src/UI/Boards/PanelFocus.cs
`Strip(subtree, who)` makes every control under a panel unfocusable and logs the tally, the
invariant every panel hosted in a flight session must hold. A focused button answers Space with
"press me again", so the pilot's fire key re-fires the last stepper instead of the guns and the
arrow keys walk the focus chain instead of reaching the aircraft or the lab's orbit camera.

## src/UI/Screens/SelectionService.cs
The shared world selection in `--freecam` and `--anim-lab`: a left click picks the mesh under the
cursor, PgUp and PgDn walk its `cs_name` ancestor ladder, and a breadcrumb line and wireframe box
show the current rung. Objects are picked by box, map-scale meshes (terrain) by triangle. `Current`,
`Ladder`, `Level`, `CurrentBox` and the `Changed` event are what the other inspect tools read, and
`--debug-select` replays a click for a scripted run. A Ctrl-held pick is reported as `CtrlPicked`,
and a tool may append a line to the breadcrumb through `HudLine`, which is how the export set
attaches without this service knowing what an export is. `ExtraRoots` walks props parked beside the
world content; `SubtreeWorldAabb` (over `Mech3/SubtreeBounds.cs`), `NewBoxInstance` and `DrawBox` are shared with the other tools.

## src/UI/Overlays/TargetingOverlay.cs
The targeting overlay (key F15, `--debug-targets`): a per-frame line from every turret gunner and AI
gunner to its acquired target, coloured by the gate holding the trigger, with that gate named per
shooter in the HUD. Depth test off, since the line into a hull is the one worth seeing. In
splitscreen the world-space lines draw in every pane on their own while the roll-call is drawn once
for the window, like `PerfHud`, because it is process-wide combat state.

## src/UI/Overlays/DebugKillTarget.cs
The debug kill key (F17): kills player 1's currently selected target through its own death path, so
kill counts and objective bookkeeping see it exactly as a real shot would, never by freeing the
node. It routes on the selection's source type, an aircraft through the attributed crash path and a
zeppelin sub-part through the anim runtime's damage call, the same call a rocket makes; a turret
selection is inert for a reason the member states. Over the wire, a target another machine owns is
claimed on its owner as one lethal hit (`../org/multiplayer-messages.md`). Player 1 only.
`KillSource` is exposed so a suite can drive the routing against hand-built sources.

## src/UI/Overlays/TileGridOverlay.cs
The map-edge tile-grid overlay, flag-only (`--debug-tilegrid`; no key is bound): every ground tile
tinted by repetition band, so one colour band is one block. `--map-edge-block` and `--map-edge-mode`
set the depth and the fold once at launch. This is the instrument the map-edge fold was settled
with; the measurements are in [../formats/world-structure.md](../formats/world-structure.md).

## src/UI/Overlays/ColliderOverlay.cs
The collision wireframe overlay (key C, scripted by `--collision=show` and `--debug-colliders`) in
`--freecam`, `--anim-lab` and `--fly`: one immediate mesh per collider host, coloured by the surface
id its body resolves to plus the three owner keys neither surface tag decides, rebuilt from the live
tree on every show. The legend is sourced from the colour function alone, so a palette change cannot
desync it, and it is drawn only where wireframes are. The id drawn is the resolved one and the
picture is of what the engine will select, not of the material data: the id is stamped per body
while the displayed name comes from the texture-derived class, and the two can disagree.

## src/UI/Overlays/AiNetsOverlay.cs
The AI patrol-net overlay (key F13, `--debug-ainets` scripts it), added to every chapter world by
the session's world stage. Draws each net in a stable id-derived colour, edges as segments off the
edge list, sphere markers per node and one label per net, all depth-tested; nets load lazily on the
first toggle and the census goes to the world log. A HUD field narrows the drawn set by name prefix.
It also draws live leashes from each AI aircraft to the node its follower is flying at, filled by a
supplier the session hands in rather than by the overlay knowing anything about aircraft. An
anchored net draws where it actually is, the trailer offset applied per frame as the root's
position, so nothing is rebuilt. The key range: [../controls.md](../controls.md).

## src/UI/Overlays/ClassOverlay.cs
The colour-by-class overlay (key H, `--debug-classoverlay` scripts it) over the same modes as
`ColliderOverlay`, a findable-targets view rather than a collision one. Mixes a class colour over
every drawn mesh at half strength, so a target stays recognisable as itself: destructible through
the registry's own resolve, facade through the billboard classification, clutter as every multimesh
under the world root, everything else scenery. Rebuilt on every press rather than cached. Keyed on
neither surface tag deliberately: one decides which collider a mesh's polygons join and the other
what happens when you touch it, and neither answers "what is this object".

## src/UI/Labs/NodeLab.cs
The node lab (key N) in `--freecam` and `--anim-lab`: the world's `cs_name` tree, a search box,
per-node frame, hide and glTF export into `Exports/`, the export set's three buttons, a dependency readout for the current selection (anim defs, destructible
pools, geometry and textures, colliders) and a destructibles view with coverage columns, plus
top-level branches for props parked beside the world content. `--debug-nodelab` is the scripted
twin, its token grammar checked at launch by `SessionSpec`. A row's text and colour follow live
visibility, re-read on the panel's own status cadence.

## src/UI/Screens/ExportSet.cs
The node lab's export set: the nodes gathered with Ctrl+click or the panel's ± set, written as one
timestamped GLB in `Exports/` at their world transforms. It rides `SelectionService.CtrlPicked`
rather than reading the mouse itself, outlines each member in a cyan box that follows that member's
transform, and puts the count on the selection's breadcrumb through `HudLine`, because a set is
gathered whether or not the panel was ever opened. A member freed under it (a destructible swapping
to its wreck) leaves on its own. Nothing is drawn until the first node joins.

## src/UI/Labs/WorldDamageLab.cs
The world damage lab (key F19) in `--freecam` and `--anim-lab`: the destructible pools of whatever
the selection holds, each with live HP, and a slider with kill and reset on the one a weapon hit
reaches, driving the anim runtime's damage and reset calls. `--debug-damage` is the scripted twin,
an ordered script rather than a token set, checked at launch by `SessionSpec`. Only the pool the registry resolves is drivable, since a
node can carry several; the rest are listed read-only with the reason, because driving a twin would
damage a pool nothing can ever hit.

## src/UI/Labs/AnimLab.cs
The `--anim-lab` debugger: a quiet world stage with a pinned seed, a fixed-dt clock, a transport
panel, a def picker, an `AnimTimeline`, a spectator freecam following the shared selection, and a
staged effect and crash anchor set so placeless on-call defs play at the camera. Interactive frames
draw each live transform-motion target interpolated between its last two sim poses, and sim poses
are restored before any step runs, so render smoothing never leaks into event held-pose seeding and
fixed stepping stays byte-identical. Puffer particle spread is unseeded, so same-step shots differ
in particle noise alone. The picker toggle is bound away from the camera's target key.

## src/UI/Labs/AnimTimeline.cs
The anim lab's authored-against-fired timeline, a custom-drawn control: authored blocks above, fired
ticks below, one lane per initial sequence, and a slanted first-firing connector meaning the
scheduler diverged. It re-derives the documented scheduling rule rather than calling the runtime's
own `SequenceRunner`, and that independence is the whole instrument.

## src/UI/Menu/PresentationId.cs
The identity a menu presentation registers under and Options persist: a value token wrapping a
non-empty string, compared ordinally, with `BuiltIn` (`built-in`) and `Original` (`original`) as
the shipped identities. The options store keeps the persisted value as a validated string and
resolves it against `PresentationRegistry.Registered`; an unknown token falls back rather than
throwing. Off-engine coverage: `CSVM.Tests/MenuSeamContractTests.cs`.

## src/UI/Menu/IMenuPresentation.cs
One menu presentation: a screen graph plus its navigation, interaction, animation and cue
selection over the shared features. `Activate(host, destination)` shows it at the screen its own
graph maps the semantic destination to, `Tick` drives it over the host's seats, `Hide` takes it
off screen with its state kept, and `Deactivate` tears it down for good. A flight is a re-`Activate`
on the same instance, so cursors survive it; a presentation switch builds a fresh instance, which
is what discards transient state. Built-in's implementation is `BuiltIn/BuiltInPresentation.cs` and
the host is `MenuHost.cs`; the seam end to end, with the checklist a further presentation follows,
is [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/PresentationRegistry.cs
Where presentations register: one factory per `PresentationId`, filled once at startup, duplicate
registration refused. `TryCreate` hands out a fresh instance and answers an unknown id with
false, so a stale persisted token degrades to the Built-in fallback instead of a throw.
Availability (the Original presentation's asset manifest) is decided before asking here; the
registry only says what exists in the build.

## src/UI/Menu/IMenuHost.cs
What the process-lifetime menu host lends the active presentation: `Features`
(`MenuFeatureSet`), `Audio` (`IMenuAudio`), `Seats` (one `IMenuInputSource` each, a live list the
join flow grows) and `Exit(MenuExit)`, the only way out. The host owns all four across
presentation switches; a presentation borrows them between `Activate` and `Deactivate` and keeps
no reference past that. The implementation is `MenuHost` below.

## src/UI/Menu/MenuHost.cs
The process-lifetime host, engine-free, one per `Launcher`: it owns the feature set, the audio
service, the seats and the exit sink across every presentation switch. `Select` settles which
presentation runs through `PresentationResolution.Resolve` over registration plus the owner's
availability answer from the two command-line flags alone, keeps the pre-availability request for the startup log, and answers a
blank, unknown or unavailable request with a fallback reason rather than a throw. `Show` creates
the selected presentation once and re-activates that instance on every later call, so a return
from flight lands on the screens as they were left; `Tick` runs it only while `Shown`, and
`Deactivate` ends it and discards the features' transient state, the first half of a switch.

## src/UI/Menu/BuiltIn/BuiltInPresentation.cs
The Built-in presentation (`CSVM.UI.Menu.BuiltIn`): `LaunchMenu` registered under
`PresentationId.BuiltIn`. `Activate` builds the launchscreen under the parent node on the first
call and maps the return destination onto it, the top level being the Mode screen, `InstantAction`
the wizard's first screen over the setup that flew, `CabinReturn` the profile's cabin and
`DebriefReturn` the scrapbook on the flown mission; the `--menu=` aid is consumed on that first
call, so a return from flight lands on Mode with the cursors kept. `Tick` runs the menu's frame,
`Hide` takes it off screen and `Deactivate` frees the node. `Menu` exposes the launchscreen;
`Planes` and `CampaignProfiles` hand it a scratch plane store and profile store. Read `LaunchMenu.cs` next.

## src/UI/Menu/BuiltIn/BuiltInSeat.cs
A pad-side `IMenuInputSource`: wraps one `MenuInput`, polls it and translates the result into a
`MenuCommands` frame (`Move`/`MoveX` to `MoveY`/`MoveX`, `Start` to `Join`, `Presets` to
`Contents`, `TextEntry` to `CapturingText`). Seat 0's wraps the keyboard plus every unclaimed pad
(the same `MenuInput` is handed to `LaunchMenu` and `MenuSeatDevices`, which bind the devices
behind the seat); a seat a pad joined on wraps a poller bound to that one pad, which is how
`MenuSeatDevices.PadOf` reads the pad back. The seat itself carries only commands.

## src/UI/Menu/IMenuFeature.cs
The contract every shared menu feature implements. A feature is typed state plus semantic
operations for one area of play (configuration, validation, persistence, player setup, launch),
exposed as its own concrete members rather than a universal row/button/picture schema, so each
presentation decides how the operations are offered. `Discard()` drops unfinished setup when the
active presentation changes; persisted data survives.

## src/UI/Menu/MenuFeatureSet.cs
The host-owned registry of shared features, fetched by concrete type (`Get<T>`/`TryGet<T>`), one
instance outliving every presentation switch. `DiscardTransient()` calls every feature's
`Discard` and is the whole of what a switch discards, so anything a feature keeps past it is by
definition persisted data.

## src/UI/Menu/MenuCommands.cs
The device-neutral input seam: `MenuCommands` is one frame of one seat's semantic commands
(auto-repeated cursor steps, edge presses with `KeylessAccept` marking an accept no key gave, typed text and a paste, `OnPad` naming the side a hint words itself for, an optional window-pixel `MenuPointer` whose
primary button arrives as a press and an edge and whose secondary as a held state driving no command
of its own), and `IMenuInputSource` is the per-seat producer (`Poll`/`Prime`/`CapturingText`). A
source is not synonymous with a pad: keyboard-plus-unclaimed-pads, one claimed pad, a mouse or a
future HOTAS/HOSAS binding all sit behind this contract, and a presentation never reads a device.
The implementations are `BuiltIn/BuiltInSeat.cs`, `Original/PointerSeat.cs` and `MenuIdleSource.cs`;
the seats themselves are the `PlayerSetupFeature`'s, claimed by source identity.

## src/UI/Menu/MenuIdleSource.cs
An `IMenuInputSource` with no device behind it: labelled "no device" and idle every frame. The
screenshot aid that seats extra players on a one-controller machine joins one per seat, so the
shot is deterministic and every seat still has a source to claim; each instance is its own claim,
since a claim is an identity. Read `MenuCommands.cs` for the seam and `PlayerSetupFeature.cs` for
the seats it is claimed by.

## src/UI/Menu/IMenuAudio.cs
The shared menu audio contract: a presentation requests a `MenuCue` by semantic name, starts or
stops narration at moments it owns, and states through `PreviewMix`/`EndMixPreview` the mix a page
that sets one stands at and which `MenuMixLevel` a frame moved; the service owns resolution,
playback, volume, the buses and the handoff into a launching session. The host implementation is
`MenuAudioService` (`src/Launch/MenuAudioService.cs`); Built-in's one call site is the briefing
narration.

## src/UI/Menu/MenuExit.cs
The one typed way out of the menu, handed to `IMenuHost.Exit` and consumed by `Launcher`:
`LaunchExit` (chapter, per-seat `MenuSeatChoice`, `MenuMode`, optional `InstantActionDef` with the wingmen's edited fit beside it, and for Dogfight or a lobby Stunt Race a `VersusRules` of kill target, minutes, lives, auto-respawn and the lobby type that an explicit `--vs-kills=`/`--vs-time=`/`--vs-lives=`/`--vs-no-respawn` beats, and a local Dogfight's join board bot rows as `Bots`),
`CampaignMissionExit` (profile, `cm_sequence` position, per-seat choices), `QuitExit` and
`OptionsApplyExit` (the graphics-mode and difficulty words, the six display settings, the four volume levels and the gameplay switches, null where never set).
An applied choice rides the exit rather than being saved by the screen that took it, so the options file keeps one writer, and a screen
hands back the settings it does not show; none of the seventeen is defaulted, so a page cannot hand back a null it never read, and a field no screen offers any more is dropped rather than left riding as a null the consumer would save. A custom
plane rides the exit as a resolved `CustomPlaneDef`, never a store name. Presentations never construct
sessions. The return side is `MenuReturnDestination`; the exit table and the scans holding the seam: [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/DisplaySettingRows.cs
How the six display settings and the Enhanced shadow quality read as rows, shared so Built-in's Options screen and Original's VIDEO page cannot disagree
about a saved value: one label per `DisplayWords` entry in that order, since a row reads and writes the store word by index,
plus the two forgiving reads (an unknown word is the vocabulary's first value, a size the screen does not offer is the
project default) and the wrap a sideways step takes. Under a `Pinned` display mode the size row reads the screen's own size
whatever is saved. An unset anti-aliasing row reads the default of the graphics word on the same page, and the render-scale
labels are built over the list the standing method offers, so the scale row narrows the moment FSR 2.2 is picked. The shadow row's description says Enhanced only while the page's graphics word is Original, where both screens hold the row dead. The
screens and the sizes are enumerated per machine by `Utils/MonitorSetting.cs` and `Utils/ResolutionSetting.cs`.
Engine-free, so the rules test without a screen (`CSVM.Tests/DisplaySettingRowsTests.cs`).

## src/UI/Menu/MenuLayout.cs
The runtime reader of `extracted/rof/menu_layout.json`, the decoded menu layout the extraction
emits, engine-free in the shared namespace. `TryLoad` answers a missing or unreadable file with
null and a reason, never an empty layout; `Parse` builds the typed model of screens, widgets, the
file-wide macros, the `ScriptToExe` navigation edges, the script-named external assets and the
missing art. A `MenuLayoutWidget` keeps every field's resolved string beside the raw macro token
and the decoder's derived readings, and its typed accessors consult the artifact's own per-type
kind table and refuse a field of another kind, so the reader carries no field table of its own.
Schema, kinds and the extraction stamp: [../formats/menu-layout.md](../formats/menu-layout.md).

## src/UI/Menu/HangarFeature.cs
The hangar as a shared engine-free feature in the host's feature set: the rules and the store
operations both presentations walk, one build at a time. `Open` takes a `CustomPlaneStore` and an
optional `IHangarWallet` (what `CampaignWallet` implements: funds, affordability, airframe
availability, the owned builds, purchase and sale); the three starts pick how the scratch plane
begins, the per-tab operations clamp and report change, and `Refusal`, `CanCommit`, `Bill` and
`Commit` are the purchase gate and the write. `LoadStockWeapons` seeds an airframe at rest from its stock fit, whose per-wing pylon counts come off the rig through `Loadout.WingCounts`, so a stock plane's cells are the ones it hangs. It also owns every label the screens write, the wallet
line, the would-be cost behind the mark on an over-priced row, the name rules, and `Discard`, which
drops the build and touches nothing saved. Economy and strings: [../org/hangar.md](../org/hangar.md).

## src/UI/Menu/HangarDescriptions.cs
What one Plane Construction tab's description box holds, engine-free and presentation-neutral: a
`HangarInfo` of the figure lines, the heading over the prose and the prose itself, one composer per
tab. Each fills the tab's own shipped figures string from the decoded economy and appends the
component's prose row where the component has one, then splits the result at the string's own blank
line, so the heading is the shipped text's and never a literal here. A component with no prose row
leaves it empty and an unpicked engine or gun slot is prose alone. The string ids, the figure
arithmetic and which tab reads which block: [../org/hangar.md](../org/hangar.md).

## src/UI/Menu/CampaignFeature.cs
The campaign as a shared engine-free feature: the state and operations both presentations read and
write, with neither one's screen shell in it. `Open` opens a campaign over a `CampaignProfileStore`;
`OpenGuest` opens a co-op guest's with no profile, on the host's mission and stock hangar, writing
nothing and standing on the pick the network door kept, `GuestCoopFit` is its ammo pick as the wire carries it, and `GuestPickOf` each of its players' picks, a further player's from the field. The roster operations create, seat, delete and record the last-played player in the
original's own words; the mission operations settle which `cm_sequence` entry the screens after the
cabin are about, with its briefing, wingman flag, per-slot change-plane rules and story aircraft;
the writes save the loadout, planes, memento (refused unless held), an exported build and the
mission exit. It carries the host's `ChapterCinema` and `ClosingCinema`. Read `CampaignFlow.cs` next.

## src/UI/Menu/BriefingScript.cs
The briefing reveal script, engine-free: the `Briefing.zrd` reader (`BriefingDialog`,
`BriefingState`, `BriefingStep`) and `BriefingReveal`, the interpreter that runs a state's
twelve-opcode beat sheet against a caller-advanced clock, blocking on an authored wait and on the
narration's cue times and keeping each element's opacity, rotation and position as its tweens
land. Elements come out in placement order, which is draw order; with no cue points every marker
releases at once, so the map finishes under the narration rather than a timing being invented.
Opcodes: [../formats/briefing.md](../formats/briefing.md). `ParseScript` is shared with
`EscapeDialog`, whose two scripts add only `Cycle`, the load screen's propeller.

## src/UI/Menu/EscapeDialog.cs
The reader behind the pause screen and the campaign load screen: one dialog's chart sheet with its
source crop and world window, its memento slot and its beat sheet, plus the block every dialog
borrows, which is the objectives parchment, the two icons the pause screen places by world position,
the four button strips and the cursor the screen is pointed at with. `escape.zrd`, `ia_escape.zrd`
and `Loading.zrd` read through it, and `Settled` runs a beat sheet out to the still either screen
draws. `EscapeMap.TryProject` is the world window: world X across, negated world Z down, false rather than a clamp for a position off it.
⚠ The `CLIP` is a rectangle in the bitmap and not on the screen, and every `WORLD` bound is
truncated toward zero, both of which the type's own members say. [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/Menu/BriefingObjectives.cs
The briefing's parchment note, read from a mission's own `objectives.zrd`: every `IDENTITY`
carrying a message key, ordered by priority ascending, which is the list an `Objective id index`
opcode indexes 0-based. Each line also carries the `OBJECTIVEn` block it was read from, the number
the objectives runtime answers about, since priority is the note's row order and nothing more.
Takes the reader rather than a path, so it resolves without an extraction, and leaves an unresolved
key visible as its raw `MSG_*` symbol rather than inventing English. Why keyless entries are not
lines, and why file order is not the order, are recorded on the type itself.
Decode: [../formats/objectives.md](../formats/objectives.md).

## src/UI/Menu/CampaignBriefing.cs
One mission's briefing as the campaign feature holds it: the `cm_sequence` entry, the
`BriefingState` its formula names, the narration wav its sound resolves to, the objectives note,
the messages the buttons are labelled from, and the running `BriefingReveal`, whose progress is
feature state. `Load` reads everything once and degrades to a briefing with no state on a broken
or absent extraction; `Advance` and `Restart` are the presentation's, called on its own clock and
on REPLAY BRIEFING, and when to draw stays the presentation's business. `BriefingScript.cs` and
`BriefingObjectives.cs` sit beside it, since a reveal is authored campaign data both presentations
run identically rather than a drawing.

## src/UI/Menu/CampaignWallet.cs
The seated campaign profile as the hangar's `IHangarWallet`, built by `CampaignFeature.Wallet()`
for the cabin's Plane Construction and null on every wallet-free door: the funds and the
affordability answer, the airframe availability threshold against campaign progress, whether a
plane is a reward and whether it may be sold, the owned builds resolved to their stored build or
their award template or the campaign's starting spec, the sale price, and the purchase and sale
writes that move the funds, the ownership record and the build together. Off-engine coverage:
`CSVM.Tests/CampaignWalletTests.cs`. The thresholds, prices and decoded comparisons:
[../org/hangar.md](../org/hangar.md).

## src/UI/Menu/TypedCheat.cs
One screen's typed-word latch, the rule the cabin, the scrapbook contents and the plane
construction hub share in the original: a left click inside the script's authored region gives it
the keyboard, each character that follows extends a buffer, and the first character that leaves the
target word's prefix empties the buffer outright so a mistyped word starts again from its first
letter. The compare is case-sensitive, arming and disarming leave the buffer alone (the script's
`focus` moves only the caret), and reopening the screen resets both. Off-engine coverage:
`CSVM.Tests/CampaignCheatTests.cs`. The words, the regions and the script lines:
[../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/UI/Menu/CampaignCheats.cs
What the original's four menu cheats leave switched on, carried by `CampaignFeature` so both
presentations read one answer: the cabin's mission pull-down and the mission it stands on, the
gallery reveal (the engine's `fViewAll`, 24 spreads whatever the campaign position), the
unlock-everything flag (`fAllowAll`) that the hidden pilot name sets, and the words and money
constants themselves. Closing a campaign drops the pull-down and its pick; the two engine globals
stay, because the original clears neither short of leaving the game. Off-engine coverage:
`CSVM.Tests/CampaignCheatTests.cs`.

## src/UI/Menu/CampaignAidProfiles.cs
The scratch profile store the campaign screenshot aids read, in the shared namespace so both
presentations' aids seat one player: `%TEMP%\CSVM\menu-aid-profiles`, emptied on every open,
seeded with two players when asked, the first progressed through the campaign's first three
missions with every objective bit set, plus the scratch build store the export aid writes into.
`LaunchMenu`'s `campaign-*` aids and `OriginalPresentation`'s read it; nothing here can reach
`user://Profiles` or `user://Planes`.

## src/UI/Menu/Original/OriginalShell.cs
The Original presentation's screen graph (`CSVM.UI.Menu.Original`), engine-free over `MenuLayout` and the shared Free Flight, player-setup,
Instant Action, hangar and campaign features, with the art measurer and the flight-devices answer injected. It owns the top level composed
from `[MainMenu]`'s own rows, the two remake-only sortie screens, the Options hub over the decoded Preferences chrome, and the messagebox
idiom every refusal and confirm goes through, whose box is the one `OriginalShellDialog` it holds beside its `OriginalCheats`; the sortie and credits screens are its own partials, below, while the campaign, hangar, Instant Action, join-board and option families stand outside them as `OriginalCampaignScreen.cs`, `OriginalHangarScreen.cs`, `OriginalInstantActionScreen.cs`, `OriginalJoinBoard.cs` (behind the top level's remake-only JOIN BOARD door) and `OriginalOptionsScreen.cs`. Each is held as one `IOriginalScreenModule` in a list and reaches back through `IOriginalScreenHost` (`OriginalScreenHost.cs`); `ModuleFor` answers which module owns the screen showing, so `BuildRows`, `Lists`, the sideways step, the dropdown close, `Activate`, `Back` and `Compose` name a module through that one lookup rather than a field and a screen-range check per family, and `Campaign`, `Hangar`, `InstantAction`, `JoinBoard` and `Options` are the typed accessors the presentation and the suites read module-specific state through, the seat walk and the shell's own hangar and seat-strip members reaching campaign state through the first of them. `Step` applies one seat's frame, `Compose` is
the screen as a `ComposedBoard` whose backdrop takes a section's `movie` row at its bottom, and every page's row kinds live here,
`OriginalSlider` among them. A pointer press arms a row and only the release still on it activates (`ArmedKey`), on an edit box taking the
caret alone where Accept in one reaches the screen's own commit; the pointer's bitmap answers an enter or leave (`PointerLive`); and a film
in front of the board takes every frame, the tail of the press that ended it included (`CinemaFilm`). `CopyWay` is the device the seat last moved, which a host's copy hint names. Screen by screen: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalShellDialog.cs
The standing messagebox as a type of its own, which the shell holds one of: the `OriginalDialog`/`OriginalDialogAnswer` pair,
the `DIALOG:*` answer keys (`OkKey` and its three siblings, which `OriginalShell` restates as `DialogOkKey` and the rest), the box
standing (`Standing`) and the focus its raise took (`FocusBefore`), `Raise`, `Take` on one answer, `Close`, the answers' `Rows`
at the messagebox slots and `Compose` over the screen's own picture. It holds no screen and no cursor: the shell's `RaiseDialog`
hands it the focus and moves the cursor onto the first answer, and its `AnswerDialog` puts the remembered focus back before the
answer runs. The rollover frame is the pointer's alone, the cursor's answer marked with an outline three pixels clear of the strip.
An answer plaque is sized through `OriginalWidgets.PlaqueSizeOf`, so the box reaches into no module; every module only raises,
and a door onto a new screen closes the box it left behind. [../org/campaign-board.md](../org/campaign-board.md).

## src/UI/Menu/Original/OriginalScreenHost.cs
The two sides of the seam between `OriginalShell` and a standalone screen module. `IOriginalScreenHost` is what a module reads off the shell and
calls back into it for: the screen showing, the per-screen focus cursor every family shares, the pointer's row and position, the device a copy hint names, whether a dialog stands,
the string table and the art measurer, the seat strip and the shell's own plate-row rule, the film a cinema plays in front of the board and the one
frame a screenshot aid replays, and the crossings into another family (the hangar a Build door opens, the walk FLY MISSION begins, a campaign resume, a roster re-read, the mission the cabin's typed cheat launches). The shell implements it explicitly, so the narrower vocabulary stays
the modules' own, and each module's tests implement it as a fake and build the module with no shell at all. `IOriginalScreenModule` is the other
side, what the shell calls on a module: `Owns` plus the seven dispatch members (`BuildRows`, `Lists`, `StepSideways`, `CloseDropdown`, `Activate`,
`Back`, `Compose`). The shell holds its modules as these alone, so a further family is one more entry in its list and no new dispatch arm.

## src/UI/Menu/Original/BoardLayers.cs
The eight lists a `ComposedBoard` is built out of (backdrop, fills, pictures, strokes, lines, plaques, notes, overlays) gathered into one collector, so
every composer in the Original presentation takes one parameter instead of the same eight in an order of its own. It is lists and nothing else: no
drawing rule, no clear, since the shell builds a fresh set per compose and hands them to the board. A composer adds to the layer its shape belongs to by
name, which is what keeps a misordered argument list from moving a shape between layers; draw order stays the board's own and is not this type's to
state. A helper that writes one layer still takes that one list, so its signature names the layer it writes and no reader has to open it to find out.
The seam that carries it across the shell/module boundary is `OriginalScreenHost.cs`, and the board it fills is `ComposedBoard.cs`.

## src/UI/Menu/Original/OriginalWidgets.cs
The layout-widget readings more than one Original screen module needs, a file-level static because a module is a sealed class of its own and a rule
two of them follow can live in neither: the slot number a numbered widget key carries (`AR_D_POINT2`, `OL_D_AMMO1`, and the same key behind a
`<key>:<index>` list row), where a section's background pane lands on the board, which for art smaller than the board is the centre its own
script sets rather than the corner it is authored at, a strip art's one-frame size, and the plaque size the shell's own messagebox measures an answer at.
The rows a screen drawn by the shared board component carries are here too, read off an `ICampaignPage`, because the campaign module and the shell's
per-seat aircraft screen build them the same way, with `ENTRY:` naming a scrapbook row the pointer only highlights. A pane that fills the board centres onto its own corner, and one authored away from the corner
keeps it. Each module binds its own measurer to the pane rule once, so no call site carries one. The open-dropdown window rule is the other such
reading, on `OriginalDropList.cs`.

## src/UI/Menu/Original/OriginalCheats.cs
The three typed cheats of the Original presentation as a type the shell holds one of, over the `gui_char` bodies of `PASSENGERCABIN.SCRIPT`,
`SCRAPBOOK_TOC.SCRIPT` and `PLANECONSTRUCTION.SCRIPT`: the authored region of each screen, its own `TypedCheat`, `Arm` for the primary-button press
the shell reads off the pointer before its row hit test (the secondary button belongs to the credits line alone), `Type` for the characters the
shell routes here instead of to a screen's edit box while `Typing` holds the keyboard, and what a completed word fires through `CampaignCheats` and
`CampaignWallet`. It is built over the two modules whose screens carry the latches, reading `Campaign.Cheats`, `Hangar.IsHub` and `Hangar.OpenWallet`;
the screen showing is an argument, never a field. The campaign module reads the cheated mission back through `IOriginalScreenHost.CheatedMission`,
which the shell answers with `Mission`: the cabin's NEXT MISSION reads and empties the buffer, so the press after a cheated launch is the ordinary
one, and the shell's every screen change calls `Reset`. Engine coverage: `menu-original-cheats`.

## src/UI/Menu/Original/OriginalDropList.cs
The one rule every open dropdown of the Original shell follows, held as the file-level `OriginalDropLists` because every page standing on it is a
standalone screen module of its own: a page hands over its key, its items, the box the list hangs
under and the layout widget behind it, and takes back the windowed rows, the `ListWindow` for the pointer and the write that scrolls it. The
window is the widget's authored `TotalDisplayed` clamped to the item count, so a short list is exactly as tall as its items and carries no
chrome. Every item is a row keyed `<key>:<index>`, the ones outside the window built but hidden, since the rows are the hit-test surface and a
dropped row would let a pointer hit what it cannot see; a scrolling list adds `<key>:up` and `<key>:down` in an arrow's width of its own right
edge and hangs the thumb between them. The Instant Action module's two screens and the Game Options and VIDEO pages (through `OriginalOptionsChrome.cs`) come through here;
`OriginalHangarScreen.cs`'s list does not, its arrows being the closed box's `DropUp`/`DropDown` art. [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/SliderControl.cs
The Original shell's continuous control: a pointer's hold-and-move over a slider row, and the
sideways step that moves one from the keyboard or the pad. It is the shell's second hold-and-move
and keeps a hold of its own, the list thumb's being the first; the shell gives the thumb first
refusal each frame and consults this one only when no list holds, so neither drag can be continued
as the other. `Drive` reports a slider holding the pointer, which is how the shell knows a click was
spent and activates nothing under it. A row declares its slider through `OriginalShell.cs`'s
`OriginalSlider`; this class knows a track and a value and nothing about the setting behind them.

## src/UI/Menu/Original/OriginalOptionsScreen.cs
The form behind the Options hub's four doors, one standalone `IOriginalScreenModule` standing five page modules (below) over the decoded
`[@GameOptions@]`, `[@Audio@]`, `[@Video@]`, `[@ControlsPrefs@]` and `[@Keys@]` sections; the hub itself stays the shell's. It keeps the frame
(the hub's logo behind every page), the page switch (the one page whose `Screen` is showing answers every dispatch member) and each page's
ACCEPT CHANGES and CANCEL CHANGES, routed to that page's `Accept` and `Cancel` before any other row reaches its `Activate`. Its `IOriginalOptionsPage`
is the page-sized sibling of the module seam, and `IOriginalOptionsForm` the narrow way back a page has: `Apply`, the one `OptionsApplyExit` read off
the three settings pages' public choices, and `Leave`, back to the hub with the edits dropped. `ScreenOpened` re-reads the saved settings into those
three pages, so a page carries the settings it does not show and only `Launcher.ApplyOptions` writes the store. The shell exposes it as `Options`, and
`OriginalOptionsTests` drives it over a hand-written host. Rows and readings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalOptionsChrome.cs
What every options page stands on, held once so no page restates it: a section's button strips at their measured size, the ACCEPT CHANGES and
CANCEL CHANGES pair as strips or as the shell's labelled plaques, the slider row and its press region, the open-dropdown rule
(`OriginalDropList.cs`) and the dark option list drawn over a page, the plate, the page title, the eight-state checkbox and the row type sizes
(14 for a title, 12 for a description or an item). It holds no page's state and reaches the shell only through `IOriginalScreenHost`, which it
also hands each page as `Host`. Its pages are `OriginalGameOptionsPage.cs`, `OriginalAudioPage.cs`, `OriginalVideoPage.cs`,
`OriginalControlsPage.cs` and `OriginalKeysPage.cs`; the form is `OriginalOptionsScreen.cs`.

## src/UI/Menu/Original/OriginalGameOptionsPage.cs
The Game Options page: the original's own Difficulty, Default View and Auto Head Turn rows (the difficulty tiers, the three views its decoded
`GO_D_VIEW` list names and the head-turn switch) plus the remake-only Next Target and Rumble rows, one table whose entry is a key, a title, a control
and the store field it reads and writes. The plate grows one whole 62-pixel band per row past the three the art is painted with, tiled from the band
between its own seams so the border art survives, the two plaques moving down with it; a dropdown takes a band to itself and the checkbox rows pair
from the top of their run where the canvas caps the growth, each checkbox title on its box's centre line, the descriptions spread evenly down their
window, and a single tightened pitch is the fallback. It holds the five settings it shows as `*Choice` and leaves through the form's `Apply`.

## src/UI/Menu/Original/OriginalAudioPage.cs
The AUDIO page: four slider rows over `Utils/AudioMix.cs`'s 0..100 on the authored pitches 58, 57, 53 and 53, Master taking the In-Game Music row
because a slider reaching zero is that checkbox in one fewer widget, and Sound Quality left out. A slider answers no Accept (`SliderControl.cs`).
`PreviewMix` is the mix the open page stands at and `TakeMoved` the level a frame moved, taken once, the host applying and sounding them; `PoseMix`
is the screenshot aid's four distinct levels. It holds the four levels as `*Choice` and leaves through the form's `Apply`.

## src/UI/Menu/Original/OriginalVideoPage.cs
The VIDEO page, the Game Options table's shape over the authored Video rows: the monitor and Resolution rows enumerated per machine
(`Utils/MonitorSetting.cs`, `Utils/ResolutionSetting.cs`, that row dead under borderless, which owns the size), Display Mode, V-Sync, Render Scale and
Anti-aliasing over `Utils/OptionsStore.cs`'s `DisplayWords`, and Enhanced Graphics on the Shadows checkbox whose gate it owns. Render Scale stands on
the Objects Detail line and Anti-aliasing on the Lighting Quality line (whose `VP_D_DLight` authors five items), picking FSR 2.2 narrowing the scale
list to 50..100 on the spot; Shadow Quality (`Utils/ShadowQualitySetting.cs`) stands on the Texture Quality line, dead while Enhanced Graphics is
clear. The Graphics row's title and description are the page's own. It holds the display settings and the view distance it carries unshown.

## src/UI/Menu/Original/OriginalControlsPage.cs
The CONTROLS page over the shared `ControlsFeature`: the seat chooser on the Controller Type row, the authored Mouse Sensitivity slider over
`Bindings/SensitivityScale.cs`'s levels, the flying-scheme chooser on the Mouse panel's title line (the right half of the seat chooser's column,
stopping above the slider's press region) and the KEYS AND BUTTONS door, which opens `OriginalKeysPage.cs`. Its edits are staged in the feature, so
ACCEPT CHANGES writes the keymaps and CANCEL CHANGES and Back drop the visit, a pending steal going first; both then leave through the form's `Leave`.

## src/UI/Menu/Original/OriginalKeysPage.cs
The KEYS AND BUTTONS page over the shared `ControlsFeature`: seven category tabs (`ControlTabs`, the Throttle tab ending with the port's Throttle
(lever) row after the original's eleven), one action list under its heading in the listbox's own window, and each row's key and pad controls in the
two authored columns (the first in Control A, the rest in Control B) with its stick controls in the port's Stick column between them
(`KeysStickColumn.cs`). A cell press arms a capture on that row's action and slot, a Stick cell a stick-only one, and the shell swallows the frame
while one runs; `ClearCell` is the clear gesture. The exit pair writes or drops the visit and returns to CONTROLS, and `SyncWindow` keeps the
window over the cursor at the end of a frame.

## src/UI/Menu/Original/OriginalCredits.cs
The credits screen, the shell's partial over the decoded `[@Credits@]` section behind the top
level's fifth row. The section is three widgets: a full-screen background pane, ABOUT and the DONE
plaque. The credit names are painted into the background art, so the pane is the whole composition
and the two buttons are drawn over it by the shell's row loop. DONE and Escape both land on the top
level, the plaque's own `ScriptToExe` and what `CREDITS.SCRIPT`'s `gui_char` does, so `Back` needs
no arm here. ABOUT raises the messagebox in its `ma_` set, centred on its own background and carrying
langui 1301 over the product id; the script's hidden line shows while the pointer's secondary button
is held in its region. The screen: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/KeysStickColumn.cs
How the KEYS AND BUTTONS page splits a row's bindings: those on a stick model's identity go to the
port's Stick column, the rest to the authored Control A and Control B. `SlotOfOther` maps Control A
and Control B to the first and second non-stick binding, so a stick bound ahead of the keys never
shifts which binding those cells replace. The Stick cell lists every stick binding's caption in the
row's order, joined by `Separator` (" / ", which Control B shares), the first listed being the one
the clear gesture drops; a line wider than its cell scrolls (`Boards/BoardMarquee.cs`). Captions
come from `Sticks/StickLabels.cs`'s `Columns`. The page placing the column:
`OriginalKeysPage.cs`; the stick-only capture it arms: `ControlsFeature.cs`.

## src/UI/Menu/Original/OriginalJoinBoard.cs
The join board, one standalone module behind the top level's remake-only JOIN BOARD door and the only screen a pad signs onto a
seat from. It draws as the open scrapbook (`SB_BackGround.jpg`, the `Album` palette): the CREW MANIFEST down the left page, one
entry per seat carrying that seat's identity chip and player tag and either the device name over "signed on" or the open seat
over the press that takes it, and the ARTICLES OF THE CREW down the right, three rules and the captain's cast-off line whose pad
buttons are drawn inline through `BoardLine.Glyph`. The gestures are `MenuSeatDevices`'s (`PrimeBoard`/`ScanBoard`), read raw off
the devices because a pad with no seat has no commands to read; CONTINUE keeps the manifest and BACK drops every sign-on. The
keyboard is never listed, since it holds seat 1 whatever the manifest says and the first pad to sign on shares that seat, and
`Pose` fills the entries for a screenshot with nobody at the controls: [../menu-presentations.md](../menu-presentations.md). Under the articles stand a Dogfight's bot rows and, in the articles' place while one is picked, the Edit Bot panel (`OriginalBotPanel.cs`); `PoseBots` stands them for a screenshot.

## src/UI/Menu/Original/OriginalBotPanel.cs
The join board's bot rows, the keyboard's and the mouse's to edit: the Bots block under the articles (ADD BOT, FILL TO and its 2-to-16 count, then a row per bot in two columns with its tier) and the Edit Bot panel (Callsign, Plane with Random and the eleven stock airframes, Skill from langui 3695 to 3697, REMOVE, ACCEPT, the stock plane's icon and ratings).
The rows are `PlayerSetupFeature.Bots`, so the Dogfight screen counts them and its launch carries them; the pilot names load from the message table on the first add. A local Dogfight has no teams, so the panel offers none.
Words, boxes and lists take the Multiplayer Lobby's bot faces through `MultiplayerBoardText`, the plaques the board's own paper strip. The lobby's twin: `OriginalLobbyScreen.cs`.

## src/UI/Menu/Original/OriginalSeats.cs
The shell's two sortie screens, Free Flight and Dogfight, over the shared player setup, plus the
seat rules every screen shares. Rows: the chapter column and BACK, then the aircraft column over
the setup's roster (an eleven-row sliding window) and FLY. Seat 0 alone drives these screens; each
joined seat then picks on its own screen (`OriginalSeatPlane.cs`). FLY is enabled once the mode's
gate is met (a Dogfight counting the join board's bots, which its strip names) and leaves as the mode's own typed exit, which the walk's last confirm reaches for it.
`JoiningOpen` is the per-screen joining rule the presentation reads, true on the join board alone, so these screens read the roster that board wrote and take no join gesture of their own. `CampaignSeatPanel` is the
seat strip the campaign boards and the Instant Action screen take as an overlay once a second seat
has joined, Built-in's chip row on `SeatStrip`'s shared shape, a co-op chip named by `NetPlayFeature.CoopSeatName`. Remake-only by design: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalSeatPlane.cs
The remake-only per-seat aircraft screen, a shell partial: once seat 0 has picked on a sortie
screen, or pressed FLY MISSION on Instant Action with a second pilot joined, each joined seat in
player order picks here. `SeatPlanePage` is an `ICampaignPage` over the sortie roster, so
`CampaignBoards.For` draws it in the plane-selection board's shape (its list field, WEAPON LOADOUT
over a selection, ACCEPT and CANCEL SELECTIONS), the seat strip over it. The picking seat's own
device drives it, and the mouse riding seat 0's source. Accept selects, a second Accept confirms,
Back and CANCEL SELECTIONS drop a selection then leave the walk with every seat kept and nothing
unjoined, and the last seat's confirm is the launch: [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/Original/OriginalInstantActionScreen.cs
The Original Instant Action screen and its Weapon Loadout, one standalone module over the shared `InstantActionFeature` and the decoded `[@InstantAction@]` and `[@OrdinanceLayout@]` sections. Its rows are
the sections' own widgets keyed by their layout keys: the contents list in its authored window with its arrows and thumb, the dropdowns at their authored boxes, the enemy rows on two pages under both
paging buttons, the radio pair and the buttons. A box no setting can fill stands blank with a pale arrow rather than leaving the page; an open list is windowed by `OriginalDropList.cs`, bands its picked
row and the row under the pointer, and a closed box redraws its outline in cream under one. The Pilot Plane list is `OriginalRosters.Roster` (stock, then the saved builds, rows named `Stock <airframe>`
and `<build name> <airframe>`), re-read on every entry and on the hangar's return; a picked build flies its airframe's stock node with its def on the seat. Build opens the wallet-free hangar
(`OriginalHangarScreen.cs`); Weapon Loadout maps the section's four ammunition and eight rocket fields onto the flown aeroplane's gun slots and the pylons it actually hangs, over the stock table's option lists, so a fill-order entry a saved build leaves empty gets no rocket field at all, with the airframe's
diagram frames, the description pane and the snapshot CANCEL and Back restore, over seat 0's fit or the wingmen's shared one by the radio pair, or the per-seat picker's own `PlayerSeat.Fit`, which is what
decides the screen its exit returns to. It is one `IOriginalScreenModule` and reaches `OriginalShell` only through the shared `IOriginalScreenHost` seam (`OriginalScreenHost.cs`), so `OriginalInstantActionTests` drives it over a hand-written host with no shell at all; the shell still owns `Rows`/`Compose`/`ApplyFrame` dispatch, routes to whichever module owns the screen showing and exposes this one whole as `InstantAction`. Its `Back` answers false where nothing is open and nothing is to cancel, which is how Instant Action's own Exit is left to the shell. Remake-only is the Lives box, which the section authors no row for: it takes the mission dropdown's column and item height on the first clear line the setup stack leaves (read off the gaps between the authored boxes, never written down as a Y), and steps the shared `InstantActionFeature.StepLives`, reading Unlimited at zero and the count to nine. The Race Time box is the second, shown only for stunt flying with more than one seat joined: it takes the next clear line, enters the walk before the first box below that line, and picks `InstantActionFeature.SelectRaceWindow`; hidden, it keeps its row index unseen, unhit and in no column, and a focus left on it lifts to the live box above. A race spends no lives, so while it shows the Lives box is hidden the same way, title included, its count kept. Option sets: [../formats/instant-action.md](../formats/instant-action.md); what the fit means at launch: `src/Flight/Weapons/LoadoutChoice.cs`.

## src/UI/Menu/Original/OriginalWrapupScreen.cs
The Original Instant Action wrap-up page, one standalone module over the decoded `[@IA_WrapUp@]` section and `InstantActionWrapupPage.cs`'s content. It stands only while it holds a snapshot, which
arrives as `InstantActionWrapupReturn` when a flown mission's hold ends and the session hands the menu its frozen numbers; `ShowWrapup` takes that run and opens the page. Its rows are the CONTINUE
plaque at its authored corner and one per print, enabled once that print's frame has landed; a print opens its photograph as `Viewing`, which the presentation shows in its `ShotViewer`, and while one is open the page is a single row covering it, which closes it as Back does, the cursor returning to the print. Otherwise both CONTINUE and Back drop the run and reopen the Instant Action screen through that screen's own door, so the sortie's roster and environment are re-read on the
way. `Compose` is the magazine spread as the backdrop, the four brushstrokes, the heading and the eight row lines, each post-it as fills under a shrinking `BoardNote` of its lines, the photographs as prints, the tick box as strokes, and the plaque. A print drawn empty because its shot has not landed is what `TakeLanded` reports once it has, which the presentation's tick reads to compose the page again. The
built-in presentation keeps its in-flight `UI/Screens/IaWrapupBoard.cs` instead and has no page here, which is why its own return lands on the Instant Action screen. It is one `IOriginalScreenModule`
reaching the shell only through `IOriginalScreenHost` (`OriginalScreenHost.cs`); the shell exposes it as `Wrapup`. The page's own decode:
[../formats/instant-action/wrap-up.md](../formats/instant-action/wrap-up.md).

## src/UI/Menu/Original/OriginalPauseBoard.cs
The Original presentation's pause screen, on `PauseBoard`'s own seam: built once by `Launch/SessionBoards.cs`
over a `PauseSheet` its mission resolves, following `PauseState.Changed`, driven by the pausing
player's reader alone. What it draws is `PauseScreens`' composition through `ComposedBoardView`, so
the screen tests off engine and this node owns the cursor, the pointer and the five actions. An Instant Action sortie's sheet is the blackboard, which it writes in `BoardPalette.EscapeBlackboard` rather than the campaign sheet's ink. That
seat's pointer shares the cursor on `BoardMenuPointer`'s rule: entering a strip moves it, a press holds the strip, the release on it fires, and the OS pointer gives way to the dialog's own. Its readout is a delegate, since the objectives follow the running mission. Preferences stands `PausePreferences` over the held world and `Reprime`s on its close, and photo mode does the same over the frozen world. It draws no control hints, since the original's sheet carries none. Decode: [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/Menu/Original/OriginalRaceTable.cs
A stunt race's standings drawn as the original's multiplayer scores page, engine-free. `Rows(standings, zoneCount)` turns `StuntRace.Standings()` into `RaceTableRow`s in `UI/Screens/RaceRows.cs`' words, and `Compose(rows, pageX, pageY, strings, layers)` writes `MP_LOBBY_STATSCREEN.PNG` into the backdrop at that page corner and the headers and up to ten rows into the lines, at `MULTIPLAYERLOBBY_STATS.SCRIPT`'s positions and faces: rows from (+24, +69) at a 20-pixel pitch, the name column 154 wide and left-justified, then cells 62, 61, 60 and 57 wide, centred. The original has no race table, so the race borrows the page: place and callsign at the name column's left and the aircraft at its right, best, gap and runs in the next three, the fifth empty, under remake-only headers. A row whose pilot left the race draws in the scores page's grey for a flagged row (`0xffbbbbbb`), and `ComposeRows` writes the headers and rows alone over a page already drawn, the lobby's own Game Scores after a race. A held scores display composes it over its own frame. Geometry: [../org/menu-inventory.md](../org/menu-inventory.md), the Multiplayer Lobby.

## src/UI/Menu/Original/OriginalRaceResults.cs
The Original presentation's end-of-race screen, engine-free. `RaceResultsSheet.Of(race, zoneNames, context, exitLabel)` freezes one ended race (standings, zone names, each pilot's splits in race order), so a restart's cleared field never redraws it, and `Compose(sheet, strings, focus, pressed)` draws it into the screen a Dogfight's end lands on, the Multiplayer Lobby on its Game Scores tab: `MP_LOBBY_BACKGROUND.JPG`, `OriginalRaceTable` at the tab page's corner, the title in the lobby's title box, the zone key down the player list's lines (two columns past eleven zones), the splits in the chat pane (zone numbers on its first line, a pilot per line, columns no wider than the scores page's), the context in the chat line, and Photo Mode, Restart and the exit on the Create Team, Send and Leave Game plaques with the lobby's strip frames and label tints. Every word but the tab's is remake-only, in the face of the lobby string at that place. A network guest's sheet carries `Withheld`, its line after the context, and leaves the Send plaque empty, so the exit keeps its slot (`Slots`); a pilot who left draws grey in the splits too. `RowAt` is the pointer's hit test, `MenuRowAt` the menu row on a plaque.

## src/UI/Menu/Original/OriginalRaceBoard.cs
The Original presentation's end-of-race board, in `UI/Screens/StuntRaceBoard.cs`'s place: `Launch/SessionBoards.cs` builds it when the presentation is Original and the install carries the lobby art and the string table. Whole-window over the panes, it wakes on `RaceCompleted` with the sim halted, freezes a `RaceResultsSheet` and draws `OriginalRaceResults` through `ComposedBoardView`, and retires once a new window clears `Ended`, so R and pad Y reach the rerun without it. Player 1's reader steps Photo Mode, Restart and the exit in turn with any arrow, resting on Photo Mode, and player 1's pointer shares the cursor on `BoardMenuPointer`'s rule; `RestartWithheld` takes Restart off a network guest's board. `Rows`, `Sheet`, `Shown` and `Menu` are what the suite reads and drives.

## src/UI/Menu/Original/OriginalHangarScreen.cs
The Original hangar, a standalone module over the shared `HangarFeature` and the decoded hangar
sections: the PLANE NAME screen, the Plane Construction hub with one of six tab sections on its
right page, the totals page and the INVENTORY, entered from Instant Action's or the Connection page's Build Custom Plane or
the cabin, the door naming the airframe a default build opens on. It is one `IOriginalScreenModule` and reaches `OriginalShell` only through `IOriginalScreenHost` (`OriginalScreenHost.cs`), the shell's own explicit-interface implementation narrowing it to the screen/cursor/dialog surface a screen family needs (`Open`, `FocusKey`, `RaiseDialog`, the focused row and the campaign plane roster), so `OriginalHangarTests` drives it over a hand-written host with no shell at all; the shell still owns `Rows`/`Compose`/`ApplyFrame` dispatch, finds this module through its own `Owns` (every screen from `PlaneName` on) and exposes it whole as `Hangar` (its typed name, open list, last build and `OriginalHangarInks`) rather than forwarding member by member. It owns the plane picture over
the blueprint panes; the hub's figures, which `HubBill` prices on the row an open list has under the cursor so they preview it and take nothing, the cost line reddening on that bill's funds verdict and the weight line on its capacity verdict, bar a previewed airframe row, whose weight line is pending and plain; the cash note on both doors (the wallet's funds, else the export door's figure), every combo row staying bare over either;
the tab bar with the standing tab latched and its labels on the strips' own baseline; the tab pages' description box, which `HangarDescriptions` fills and whose prose flows as a note inside it; every list under its box bar the decal picker, the page's own five-across grid of tiles carrying its chrome inside its right edge; the two name boxes with their
caret, the inventory's plane line on the middle of the dashed box the background paints rather than on its authored row, the airframe swap's own three-answer question as the shared messagebox (its answer keys mirroring `OriginalShellDialog`'s `OkKey`/`YesKey`/`NoKey`/`CancelKey`), and the export door's own Export, Delete and delete confirm; the shared pane rule (`OriginalWidgets.cs`) centres a small pane and this module places its rows on it. [../org/hangar.md](../org/hangar.md), [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalCampaignScreen.cs
The Original campaign, one standalone module over the shared `CampaignFeature`: the profile screen,
the cabin (with a co-op host's HOST CO-OP, BOOT and the band's COPY), the table of contents, the flight check, ammo and plane selection, the book, a scrap's
zoom and the briefing. What each screen draws is the shared board component, so the module hosts
the Built-in campaign pages in a `CampaignFlow` of its own and copies every composed layer into the
board it hands back, the cabin's painting going down as backdrop so the mission pull-down's paper stands over it; that flow is never walked, its screen and cursor mirroring this module's. The
screen graph, the rows at the rectangles the board draws them at, the pointer hit-testing, the cues and every dialog raise are this file's, as are `OpenCabin` (every door onto the cabin, which is why RETURN TO CABIN is taken here rather than mirrored off a page), `ShowScrapbook`, where the feature's two cinemas play (a co-op host's go to its guests, each guest plays the one its host names and ends it with the host's, and a launch ends one still up), and `CheckSeat`: the check and the two screens it opens stand for one player at a time, that seat's own device driving them while seat 0 keeps its pointer alone. A co-op guest's READY marks the player whose check shows and walks on to the next at its machine, and CANCEL READY takes every mark back. It is one `IOriginalScreenModule` and reaches `OriginalShell` only through `IOriginalScreenHost` (`OriginalScreenHost.cs`), which raises its messageboxes, runs a script's frames and plays its films, so `OriginalCampaignTests` drives it over a hand-written host with no shell at all; the shell dispatches through `ModuleFor` and exposes it whole as `Campaign`, which is also how the seat walk and the hangar door's wallet reach campaign state. The scrapbook's pen is the only stroke any module draws, which is why `Compose` carries a strokes layer. Read `src/UI/Campaign/CampaignFlow.cs` for the pages; the screens and
their strings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/MultiplayerBoardText.cs
The words, faces, label tints and plaques the Multiplayer Connection and Lobby pages share, one instance per page over its `IOriginalScreenHost` and data root. It loads the original's string table once, drops the leading `]` several lobby strings carry, draws every multiplayer face in regular weight as the original's capture does, and sizes a plaque strip by its art. Its static `Word` and `Regular` take a string table directly, for the race boards drawn in flight with no page host.

## src/UI/Menu/Original/OriginalConnectionScreen.cs
The original's Multiplayer Connection page and the LAN games list behind its Connect, one `IOriginalScreenModule` over `NetPlayFeature`. The multiplayer scripts place their widgets inline, so every corner is the scripts' own rather than the layout's.
Of the original's ways only LAN TCP/IP, which searches the network, and Internet, which joins the typed address, are offered. A third way of our own, Join by code, has its own box: it takes the code alphabet and the dash in capitals, Connect reads it through `MasterWire.TryCode` and joins by `NetPlayFeature.JoinByCode`, and while the door's `CodeFault` names a reason the radio and box stand greyed with that reason as its description.
Build Custom Plane opens the wallet-free `OriginalHangarScreen` on the default airframe and comes back here on CANCEL or a commit, its builds being what the lobby's Custom Planes tab lists. Host and Create Game open `OriginalLobbyScreen` as a Dogfight's host once the shell's `OriginalNetInfoBox` is answered, as every join is first. A join started here is followed on the shared messagebox over the page until it lands or fails.
A game of another build version lists in grey with its version as its status, and Join Game refuses it in a box before any socket opens. A game that asks a password reads Need Password, and its Player Information takes one. Plaques draw as pictures, over a script's labels.
Each edit box cues each typed character and each paste with the edit box's keystroke or reject sound, and keeps the script's 150 pixels as a `KeepEnd` line that scrolls to the end of an IPv6 address.
The geometry and strings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalLobbyScreen.cs
The original's Multiplayer Lobby, one `IOriginalScreenModule` over the door's `DogfightLobby`, with its four tabs (Mission Options, Select Plane, Select Ammo, Game Scores) in the scripts' own placements and art.
The host's option controls are live until it is Ready, a guest's are drawn greyed with the host's values, and all four types fly; Capture the Flag greys the two environments with no flags and the team count, and adds the own-flag-home box, Zeppelin vs Zeppelin greys the team count, and Stunt Race, the remake's fourth (no string table id for its name or its line under the box), greys Above the Clouds and every Mission Option but the Time box. A host picks a guest's row in the player list, and Boot removes that guest. The player list draws each team's row over its members; the team button creates (standing `OriginalTeamBox`), joins the picked team row or leaves, while its pilot is not Ready. Restrict Number of Teams and its count boxes are live on the host, the victory radios arm Time, Score or both, and a refused LAUNCH! raises the original's 10518 to 10520 or the remake's own line. The Lives box is live only while Limited Lives is ticked. Select... (View... on a guest) is live while Outlaw Components is ticked and stands `OriginalOutlawList` over the tab page with the tabs greyed. A toggle of Outlaw Components empties the list.
The bot controls are the remake's own, with no original layout: Add Bot, Fill to and its count box (2 to 16 pilots) stand on Mission Options under the type's description, greyed on a guest and outside a Deathmatch, where LAUNCH! with bot rows raises `BotsDeathmatchOnly`. A host's press on a bot's row opens Select Plane on that bot in place of its own picker (callsign, plane with Random first, skill with its langui words capitalised, team, Remove and Accept) in the Mission Options dropdowns' face, and any tab press lets it go; a guest sees bot rows in the list only. A bot row carries `BotTag` in the Ready column in place of a mark, Game Scores tags a bot's line, and the list's header counts the people against the cap with the bots after it.
Every player picks a stock plane, or one of its saved custom planes while the host allows them, and its ammunition, live at all times. Ready is live once the options have been heard, and a refused Ready raises the original's langui 10517 dialog with each reason. `Land` stands a completed match's peers back here on Game Scores, which is greyed until then, an ended stunt race's with its table in `OriginalRaceTable`'s columns and grey rows. LAUNCH! is live on the host once every row is Ready, and hands the shell a `LaunchExit` in the type's mode (`DogfightLobby.LaunchMode`, a stunt launch for a Stunt Race) on the environment's chapter with the lobby's rules and the door's wire; `GuestLaunch` is a guest's same exit once its host has launched.
Leave Game closes the door and lands on the Connection page. A host's chat carries `NetworkRows` pinned at its top (`CoopDoorText.HostLobbyLines`): the join code alone, the wait alone while the master server is still answering, else the address with why there is no code. Its first row is the COPY control's row, absent during the wait, so a click, a tap or a pad's Accept copies through the door. A guest that joined while its host flies a match reads `MatchInProgress` there under langui 10090's In Progress (`WaitsOnMatch`). The shell follows a Dogfight guest into this screen and out of it when the link ends. Geometry and strings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalOutlawList.cs
The lobby's outlaw list pane, which `OriginalLobbyScreen` builds, draws and answers while it is open, and `OutlawRows`, the pure map from each of its five sub-tabs' rows to a `NetPlaneRules` flag and the string naming it.
Every tick goes through `DogfightLobby.SetOutlawed`, so it starts a new round, clears every Ready and reaches every guest at once. Cancel restores the list the host opened with, and Accept only closes. The boxes are live only on a host that is not Ready, and a guest's pane has no Accept.
An ammunition or rocket row reads ticked and ignores a click while its page's Outlaw All is set. Airframes and Rockets show four rows under a scroll bar. The decode: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/UI/Menu/Original/OriginalNetInfoBox.cs
The original's GAME INFORMATION and PLAYER INFORMATION boxes in their scripts' placements and art. `OriginalShell` stands them over whatever page asked (`AskNetInfo`), as it does a messagebox: their rows are the only rows, and a refusal's messagebox stands over them.
A host answers Game Information (name, masked password, the Maximum spinner, and the remake's Listing chooser beside it, opening on the kind's default and greyed while the door has no master server) and then Player Information (callsign, the Voice drop-down, a greyed password); a join answers Player Information alone, its password live when the game may ask one. OK is greyed on an empty name, and a name of spaces raises langui 10510 or 10511.
The last OK hands a `NetPlayerInfo` to the door and the options. The Connection page's Host, Create Game and joins and the cabin's HOST CO-OP ask them. The decode: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/UI/Menu/Original/OriginalTeamBox.cs
The original's CREATE TEAM box in `MULTIPLAYERTEAMMODAL.SCRIPT`'s placements and art, which `OriginalLobbyScreen` stands over its page: while it is open its rows are the lobby's only rows. One Team Name box of 12 characters, OK greyed while it is empty, and Cancel. A name of spaces raises langui 10512 and empties the box; an accepted name goes back to the lobby, which creates the team through `DogfightLobby.CreateTeam`. The decode: [../org/multiplayer-messages.md](../org/multiplayer-messages.md), "Lobby teams".

## src/UI/Menu/Original/OriginalPresentation.cs
The Original presentation node, registered under `PresentationId.Original`: a `CanvasLayer` on the board layer holding one
`ComposedBoardView`, so every screen scales as the campaign boards do. `Activate` builds the shell and the device
bookkeeping once, refreshes the roster from the saved-plane store on every call (`user://Planes`, or the scratch store a suite sets on `Planes`), stands the shell on the top level, maps the
return destination onto it and applies the `--menu=` aid on the first show. `Tick` keeps the pads in step (the board's sign-on scan
while the shell stands on the join board, and that frame's presses dropped from seat 0's commands, since seat 0 borrows every unheld pad and would otherwise read the same A as Accept), polls every seat, maps a window-pixel pointer into the
authored space, steps the shell, requests its cues, states the AUDIO page's mix while that page is open and ends the
preview on every door out and on `Hide`, drives the briefing's reveal, and runs the board's movies on the step the host was given,
`DebugPointer` standing in for seat 0's pointer when the screenshot aid asks. A `ShotViewer` over the view follows the wrap-up page's `Viewing` photograph. The shell's art sizes come from `OriginalArtSizes`; `PaletteFor` is the inks, and `CabinPalette` writes the cabin's pull-down in the paper forms' list inks.

## src/UI/Menu/Original/OriginalArtSizes.cs
The art measurer every host of `OriginalShell` hands it, since the layout carries a widget's
position and its art name but not that art's size, and a name that does not measure leaves the row
on a fallback rectangle, which is the rectangle the pointer then hits. One name answered with the
file's pixel size, cached per name over one extraction root: a bitmap through `Image.LoadFromFile`,
a movie off its sequence header, which no bitmap loader can read. A file that is not there measures
as null once and is logged once, under the host's own name, so the presentation and the in-flight
`PausePreferences` leaf are told apart in the log. Both hosts hold their own instance, so the cache
follows the screen that is standing rather than being shared across a teardown.

## src/UI/Menu/Original/OriginalAvailability.cs
The availability answer Original is selected on: `Load(dataRoot, out reason, out degraded)` refuses
a tree stamped below `OriginalAssetManifest.StampSchema` (`ExtractionStamp.Behind`), reads the
layout through `MenuLayout`, requires a `[MainMenu]` section in it, and then checks the manifest
derived from that layout. Returns the loaded layout when Original can run, else null and the one
reason, which the host appends to its fallback reason; `degraded` is the optional half, for the
caller to log once. `ArtPath` and `RelativeArtPath` are where a layout art name resolves, in
`RofTree`'s upper case, the presentation's size read going through the first; `IsMovie` puts the movies one directory deeper,
under `MPG`, where the executable resolves them. Coverage: `CSVM.Tests/OriginalManifestTests.cs`.

## src/UI/Menu/Original/OriginalAssetManifest.cs
The versioned required/optional asset manifest, derived from the decoded layout rather than
hand-listed. `Derive` classes the art of the sections Original composes required, less two short
tables (rows it does not draw, and rows it draws whose file the screen survives the absence of,
which is where the background movies sit), everything else optional, and the files the scripts
name and Original draws anyway (the pointers, the font, the multiplayer pages and the lobby's art) required. `Check` reads no bitmap: existence plus the PNG signature
and IHDR size for a required entry, existence alone for an optional one, and one report naming every
fault with its section, row and file. `Schema` carries the rule for its own bumps; the asset policy
and the reconciliation against what the screens draw are [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/Original/OriginalRosters.cs
The two rosters the Original sortie screens list. `Chapters` is the shared `MenuChapters` roster
with a short label per code; `Airframes` is the eleven stock names in the string table's own
order, resolved to their `planes.zbd` nodes through the Instant Action decode, so the
name-to-node map has one home. `Roster(customs)` appends the saved custom planes through the
shared player setup's roster rule, which is what both presentations pick from and what the
Instant Action Pilot Plane list offers. Read `src/UI/Menu/PlayerSetupFeature.cs` next.

## src/UI/Menu/Original/OriginalCues.cs
The four cue names the Original presentation asks the shared audio service for: a button rollover,
a button press, and an edit box's keystroke and reject sounds, which are the four the original's
globals script binds. The names are semantic and the cue table owns which wav each resolves to, so
the presentation names no file. The contract is `IMenuAudio.cs` and the table is
`src/Launch/MenuCueTable.cs`.

## src/UI/Menu/Original/PointerSeat.cs
Seat 0 with a pointer: wraps the seat that polls the keyboard and the unclaimed pads and adds the
mouse as the frame's `MenuPointer` in window pixels, `Pressed` while the left button is down,
`Clicked` on the press edge, `Wheel` as the steps turned since the last poll and `RightPressed`
while the right button is down. `Prime` reads the primary button and drains the wheel, which also
drains every frame whether or not a pointer is on screen, so input from before the menu showed never
arrives as one jump. The four device reads are injected delegates, so the seat is engine-free and
`Launcher` supplies the mouse position, both button reads and the wheel it counts in `_Input`.
Built-in ignores the pointer; Original maps it into its authored space; a later pad seat has none.

## src/UI/Menu/MenuReturnDestination.cs
Where the menu stands when it comes back, said semantically: `TopLevel`, `InstantAction`, `InstantActionWrapupReturn(snapshot)`, `CabinReturn(profile)`, `DebriefReturn(profile, missionSeq)` and `LobbyReturn(scores, race)`, a completed Dogfight's or an ended stunt race's landing on the lobby's Game Scores. The host names the destination and the
active presentation maps it into its own graph at `Activate`, so no presentation-specific screen id crosses the seam. `ForLaunch(exit)` reads off a launch's own exit the screen it came from, which is
where a flight left early lands; the exit and not the session's spec, since a spec inherits the command line's `--campaign=` and would call a Free Flight launched afterwards a campaign mission. A
destination names where the player stands and never a store: the two campaign returns name a profile, the store it is re-read from is the presentation's own, and an Instant Action return names
nothing, the sortie's setup being the feature's. The one exception is the wrap-up return, which carries `IaWrapupSnapshot` (declared by the session in `Session/InstantAction/IaWrapupSnapshot.cs`, so no other presentation type crosses the seam):
the session that counted an ended Instant Action mission's numbers is freed before any page can draw them. The `--menu=` aid is not a destination either, reaching the cold start alone, so a return is
always one of these. The namespace seam this whole
folder is held to, and the two scans that enforce it, are in [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/MenuChapters.cs
The shared chapter roster: the eight chapter worlds as `MenuChapter(Code, DangerZones)` in code
order, with `For(mode)` (Stunt Flying only the six carrying Danger Zones), `Find` and
`DangerZonesFor`. The codes are separate terrain databases, not lighting variants
([../formats/spawns.md](../formats/spawns.md)); the flag says whether the chapter's `ia.json`
ships a `dzones` list, which is why Stunt Flying withholds the other two. Shared so that
`LaunchMenu`'s Chapter screen, the Free Flight feature and the Instant Action feature read one
roster. Off-engine coverage: `CSVM.Tests/FreeFlightFeatureTests.cs`.

## src/UI/Menu/FreeFlightFeature.cs
Free Flight as a shared `IMenuFeature`, the first feature cut out of `LaunchMenu`: the chapter
roster it offers, the pick (`SelectChapter`, a code outside the roster throwing), the launch gate
(`Refusal`/`CanLaunch`: a chapter picked, at least one seat, every seat confirmed), `BuildExit`
(the typed `LaunchExit` with mode Free, refusing a closed gate or a seat with no plane) and
`Discard`. Free Flight is the remake's own mode, so nothing here is decoded; the rules are the
launchscreen's, moved. The seats are the `PlayerSetupFeature`'s, so this feature never reads a
roster, a lock or a store. Owned by the host's feature set and read out of it by `LaunchMenu` and
`OriginalShell`. Off-engine coverage: `CSVM.Tests/FreeFlightFeatureTests.cs`.

## src/UI/Menu/ControlsFeature.cs
The rebinding screen as a shared `IMenuFeature`, engine-free: one seat's keymap, the context, the
row and slot cursors, the capture (given the focused row, so a stick axis binds a whole pair) and
the pending steal, which names every loser but a full axis's pair partner and waits for
`ConfirmSteal`. `BeginStickCapture`/`OfferStick` serve the original's Stick column, replacing only
the same stick model's binding. `UnbindSlot`, `ResetContext`/`ResetSeat` (player 1's stick rows
through the injected `IStickRows`), `OpenProfilesFolder`, the injected save (player 1's through
`Sticks/StickScreens.cs`) and `Accepted` are the rest; mouse settings are staged with the maps.
Model: [../org/input.md](../org/input.md).

## src/UI/Menu/PlayerSetupFeature.cs
Player setup as a shared `IMenuFeature`, device-neutral and engine-free. `Seats` are `PlayerSeat`s
in join order, each bound to the `IMenuInputSource` that claimed it: a claim is one source and one
seat, settled in arrival order, and seat 0 never leaves. `Roster` is the `MenuAircraft` list every
seat picks from, set by the presentation and built by the shared rule (the stock rows in their given
order, then one row per saved custom flying its airframe's stock node, a campaign plane nobody has
exported left out). Per seat it owns the cursor, the two stages of the pick, the loadout door and
the backing-out ladder; the gate is the mode's minimum of pilots and every seat confirmed. A local Dogfight's bot rows are its `Bots` (`DogfightBots.cs`, edited on the join board): they count toward that minimum and the 16-pilot field (`Pilots`, `FieldPilots`, `BotRoom`, `FillBots`), a seat signing onto a full field takes the newest bot's place, and they ride a Versus exit and survive a return from flight. It also holds Dogfight's two match rules, `KillTarget` and `TimeLimitMinutes` with their steppers, starting at the command line's own 5 and 5 and riding a Versus exit. `Choices`
and `BuildExit` are the typed result. Nothing here reads a pad: `src/UI/Screens/MenuSeatDevices.cs`, below.

## src/UI/Menu/NetPlayFeature.cs
The multiplayer door as a shared `IMenuFeature`, engine-free and carrier-free: the port and address a board edits (`TypeAddress`, `PasteAddress`, up to `AddressLimit`), the socket it opens, and the readouts a board draws (`Stage`, `Peers`, `Link`, `Fault`, `HostStarted`, `Advert`). `OpenJoin` opens on `JoinTarget`, the address parsed by `Net/NetEndpoint.cs`.
Carriers and the LAN socket arrive as delegates (the launcher's `Net/NetCarrier.cs`, or a suite's loopback mesh), the router as a `Net/RouterAccess.cs` (`Router`); every open wraps its carrier in a `Net/NetLobby.cs`.
`OpenHost`, `OpenCoopHost` (whose `Offer` names the mission), `OpenDogfightHost` and `OpenJoin` open; `Step` carries the link and moves `Revision` on news, which both menus repaint on; `Close` gives the router back.
In co-op the door seats guests and keeps the round of picks. A guest asks a seat per player at its machine (`LocalSeats`); every seat counts against the cap, a further one is granted only from room the first seats leave, and `CoopSeats` is what the guest got. `ShowCoop` names the host's boards through `HostFlow` (`CoopHostFlow.cs`), `CoopAllReady` holds FLY MISSION until every guest seat is Ready, `CoopLaunchDue` tells a guest to follow, and `TellSeatFits`, `TellSeatBuilds` and `TellCoopWingman` go out before the opener.
`ShowCoopFilm` and `EndCoopFilm` share the host's campaign films (`CoopFilm` is a guest's latest word). `OfferCoopHangar` hands `HostFlow` the hangar with each plane's holder and the plane each seat flies, `CoopGuests` and `CoopGuestPlanes` list every guest seat side by side, `CoopHangar` is a guest's latest hangar words, and `CoopSeatName` is any co-op seat's callsign on either end. A guest's picks are `Pick` and `PickOf` (`CoopGuestPick.cs`), one per seat, and `LeaveCoopMission` tells the host at once that it walked out.
`Dogfight` is the `DogfightLobby` either end stands in, unshown behind a Built-in host; `DogfightLaunchDue` tells a guest its host has launched, and a guest back from a match waits for the next round. A host's door in flight steps neither its wire nor its lobby but still advertises, In mission, to its peers, the LAN and its listing, so a player who joins mid-match waits in the lobby with no options until the match lands there. `Version` is `Net/NetBuildVersion.cs`: either end refuses the other's version, with both on `Fault`.
A host reads `StableIpv6` and `LanIpv4` as it opens; `GuestAddress` is what a guest types, and `CopyForGuests` hands `CopyText` (the launcher's clipboard, on Ctrl+C) the join code, else that address. Boards: `LaunchMenu.cs`. With a master server set, `Master` (`Net/MasterDirectory.cs`) adds its games to `Games`, a listed game or a typed code with its dash joins through `OpenCode`, as does `JoinByCode` with the Original Join code box's text, which leaves `Address` alone (`LinkedTo` names the code); `CodeFault` says why a guest cannot join by code (no master server, or `WebRtcReady` false). A host hands its carrier the listing (`INetListing`), unlisted while `Private`, whose `JoinCode` the boards show; `AwaitingCode` and `InternetFault` say why there is none yet.
`Take` holds what the Game and Player Information boxes answered (`NetPlayerInfo.cs`): `PlayerName` is the callsign every pick carries and a host's first seat takes, `Voice` rides every pick, and a host's `GameName`, `MaxPlayers` and `Private` are its advert's name, cap and listing. `Password` is the one a host asks before admitting a guest, or a guest's answer; it and `Private` end with the session that used them (`ForgetAnswers`). `Boot` removes a seated guest and bans its address until the door closes.

## src/UI/Menu/NetPlayerInfo.cs
What the original asks before a network game opens, engine-free for both presentations: the game's name, password and Maximum # of Players, the remake's Public or Private listing (`Private`, never remembered; `DefaultPrivate` is Private for co-op and Public for a Dogfight), and the player's callsign and voice, with the scripts' limits. `ClampPlayers` holds the cap to the spinner's floor and the kind's cap, four humans for co-op and sixteen for a Dogfight. `IsValidName` is the original's name test.
`PilotVoices` is the Voice list's seven voices with their speaker values (pilot VO ids, `SpeakerFor` reads one off a voice byte) and the pick's voice byte; `CoopHost` is the voice a co-op host's first seat speaks in. `Remembered` and `Remember` read and write the callsign, the voice and the game name in `Utils/OptionsStore.cs` for the next session.
The decode: [../org/multiplayer-messages.md](../org/multiplayer-messages.md). Coverage: `CSVM.Tests/NetPlayerInfoTests.cs`.

## src/UI/Menu/CoopHostFlow.cs
What a co-op host names to its guests, owned apart from the door: the board (`Screen`), the mission, the campaign's progress, the hangar it offers, the debrief's result (`ShowResult`) and the campaign film it shares (`FilmShown`).
It builds each seated guest's `CoopFlowMessage`, naming its first seat and how many it was given, and sends one again only when it changed. It holds the hangar and each seat's settled plane (`ShowHangar`, read back by `HangarAt` and `PlaneOf`) and sends each guest every `CoopHangarMessage` it has not heard as it stands before the flow, so a plain join with no hangar shown sends none. Every seat's callsign goes the same way, as the Dogfight lobby's `DogfightRosterMessage`.
The round of picks is the door's: `NetPlayFeature.ShowCoop` advances it on this module's answer and hands it to every send.
Wire: [../org/multiplayer-messages.md](../org/multiplayer-messages.md). Coverage: `CSVM.Tests/NetPlayFeatureTests.cs`.

## src/UI/Menu/CoopGuestPick.cs
One seat's pick at a guest, owned apart from the door: `Airframe`, `Fit` and the Ready it means, set together by `Set`, the hangar plane it names (`Plane`, set by `Choose`), and the walk-out mark `NetPlayFeature.LeaveCoopMission` sets.
It lasts the joined session across flights and starts on `StarterAirframe`, the stock fit and no plane. The door sends it under the host's round and again only when it changed; a new round clears Ready and the walk-out. A co-op host settles a guest's `Plane` against every earlier seat's and seats it on the answer, with the pick's fit while that is the plane it picked.
`NetPlayFeature.CoopReady` is Ready as the host last heard it. Coverage: `CSVM.Tests/NetPlayFeatureTests.cs`.

## src/UI/Menu/DogfightLobby.cs
The Multiplayer Lobby's state over a `Net/NetLobby.cs`, engine-free, one class for both ends.
The host owns the options (environment, mission type, Time, Score or both, Restrict Number of Teams with its bounds, the lives rule), the plane `Rules` (Allow Custom Planes ticked on its first `Show`, as the original's lobby opens) and the teams (a `Net/NetTeams.cs` book), and sends them to every guest; any option change advances the round and clears every Ready, its own included. `CreateTeam`, `JoinTeam` and `LeaveTeam` act on the host's book or ask the host, `Teams` and each row's team read the outcome, `LaunchRefusal` is the team launch check and `TeamOfPeer` a seat's team at launch.
A guest reads the options and the host's player list, and sends its plane (a custom one as its `Build`), fit and Ready under the round it heard once a lobby screen `Show`s it; a changed pick clears its own Ready. `SetReady` runs the original's Ready check, and the host counts a guest Ready only on a plane its rules admit. `Say` sends one chat line, which the host relays; `Announce` posts a host's notice under no name, and `PeerAt` names the peer on a host's row.
The host also keeps bot rows (`Bots`, each a `DogfightBot` with an id that outlives its place, held in a `DogfightBots` whose list rules the local join board shares), listed and seated after the guests and kept across matches: `AddBot` (a Random plane at veteran, a callsign drawn from `CallsignPool`, the smallest standing team at that moment and no rebalance after), `FillTo(n)` (n counts people and bots together), `RemoveBot` and the per-row setters, all refused on a guest and while the host is Ready; only a Deathmatch takes bots (`TakesBots`, `BotsGrounded`). `FieldSeats` and `BotRoom` keep people and bots within `NetSeats.MaxPlayers`, and a host's `Step` lets the newest bots go while seated people push the field past it, one per seat with a chat notice (`YieldLine`), so a person who joins a full field takes the newest bot's place and a leaver's seat is not refilled; a disbanded team's bots go teamless; `PeopleListed` and `BotsListed` count the list's people and bot rows apart; `WaitsOnMatch` is a guest held in the lobby while its host flies a match; `LaunchRefusal` and `Teamed` count bot rows; `LaunchBots` is what the launch resolves and seats.
`CanLaunch` is the host's gate, every row Ready; the type is `Spec.DogfightMissionType` (`TypeOf` reads the wire byte); `RulesOf` is the `VersusRules` a launch carries, lives clamped to 1..99 and the type with Capture the Flag's option, a Stunt Race its Time box alone, and `LaunchMode` the menu mode it leaves in; `ChapterOf` the chapter an environment flies on, the world its row's number names (Above the Clouds is `C1C`); `EnvironmentNumber` and `ModeOf` are the numbers the original's session setup writes for a row and a launch, and `Teamed` whether any pilot joined a team. Capture the Flag fixes two teams numbered 1 and 2 and offers the five environments with an `MP2` map (`Offers`); Zeppelin vs Zeppelin fixes two teams of any number (`FixesTeams`) on all seven; Stunt Race offers the six whose chapter ships Danger Zones, sets the Time box to 5, refuses every other option and launches whatever teams stand. Setters refuse on a guest and for a greyed choice. `CheckBuiltInLaunch` gates a Built-in host's launch on its lobby guests, and `Land` holds a match's `Scores` (from `ScoresOf`, each seat named by its callsign on the session's seat roster with a bot's line marked, the launch's list the fallback, a team match's team lines with their pilots under them), or a stunt race's `RaceScores` (`RaceTableRow`s), and opens the next round.
Wire: [../org/multiplayer-messages.md](../org/multiplayer-messages.md). Coverage: `CSVM.Tests/DogfightLobbyTests.cs`.

## src/UI/Menu/DogfightBots.cs
The bot rows a Dogfight host keeps and the rules every such list follows, engine-free, shared by the network lobby (`DogfightLobby.cs`) and the local join board (`PlayerSetupFeature.Bots`).
`Add` makes a row on a Random plane at veteran under a callsign drawn from `CallsignPool` that neither a row nor a person holds, else `Bot n`; `Rename` (cut to 12, refusing a blank or held name), `SetAirframe`, `SetSkill`, `SetTeam`, `Remove`, `DropNewest` (the row a joining person takes the place of) and `ClearTeam` (a disband) edit them in place by id. `Room` and `FillTo` are the 16-pilot rules, and `LaunchEntries` is what a launch resolves through `Session/Roster/BotSeats.cs`.
The owner holds the gates: who may edit, how many pilots its field holds and which team a new row joins. Coverage: `CSVM.Tests/DogfightLobbyTests.cs`, `CSVM.Tests/JoinBoardBotTests.cs`.

## src/UI/Menu/CoopDoorText.cs
The words the network door is drawn in, engine-free and built off the door alone: the host's band
(`HostBand`: the join code and Public or Private when there is one, else port, router address, guest
count, then `HostFallbackLines`, which waits for the master server's outcome: the wait alone while it answers, else `HostAddressLine` and `InternetLine`'s reason), a Dogfight host's `HostCodeLine` and its lobby's pinned rows, the code alone or `HostFallbackLines` (`HostLobbyLines`), each naming Ctrl+C only for `CopyWay.Keys`, and what a COPY control copies (`CopyTarget`), the router's answers
(`RouterStatus`, `PinholeStatus`; `HostPinholeStatus` omits an address already named), what a
guest types (`HostAddressStatus` on the board), an advert's session (`SessionName`), the join and waiting boards' status (`JoinedStatus`,
`WaitingStatus`, naming a join by code by its code), why a guest cannot join by code (`NoMasterServer`, `NoWebRtc`, `CodeJoinUnavailable`), the games list's cells with a version and a Need Password mark (`Status`), a guest's band and its line for players the cap left out (`GuestBand`, `SeatsShort`), the booted and wrong-password refusals, the refusal naming both
versions (`VersionMismatch`), and those boards' rows and presses. The mission's long name comes in as a delegate, since only the caller holds the langui table.

## src/UI/Menu/NetDoorAid.cs
The multiplayer doors the `--menu=` screenshot aids stand on: a host door over a loopback wire with
guests already on it and a router stub mapping at a documentation address, and a guest door already
joined to a loopback host advertising a campaign mission. `CoopGuest` stands a guest on a given
host flow and hangar words (`HangarWords`, a host profile's planes held as its seats settle before any guest picks), and `AnswerReady` makes a host's guest Ready, `NameGuests` gives each its callsign from `GuestNames`. `DogfightDoors` is a Dogfight host and two
guests on one wire, `PoseDogfight` sets the lobby the `lobby` aid shows, `PlayedScores` and `PlayedBotScores` (named off a seat roster, bots tagged) are the
finished matches its Game Scores page lands, and `LateDogfightGuest` a guest waiting on its host's match. The games list's sample LAN holds one game of
`OtherVersion`, which the list marks. `AidInternet` poses a host's master server: `Listed` gives a
carrier `SampleCode`, or the server is set with no carrier. `CodeGuest` is a shut guest door with a master server, so the Connection page's Join by code way stands live. No aid opens a socket or asks a router.

## src/UI/Screens/MenuSeatDevices.cs
The pad side of the shared player setup, for any presentation, over seat 0's `MenuInput` and the
feature. `P1Pad` is the captain's pad, the first to sign on. `Sync` reconciles the
seats with the connected pads: a seat whose pad vanished is unjoined, a vanished captain's pad frees
seat 0, and seat 0's poller is bound to the captain's pad or to every unclaimed one.
`PrimeBoard`, `ScanBoard` and the `BoardScan` it answers with are both presentations' join board and the only way a pad takes a seat, A signing a pad on, B signing it off and Start on the captain's pad casting off, over `SignOn`, `SignOff`, `IsCaptain` and the `IJoinRoster` each board draws its manifest from. A first sign-on takes `P1Pad` rather than a seat of its own, since the keyboard holds seat 1 whatever the manifest says, and `DropSignOns` is BACK giving every one of them back.
`PadOf` reads a joined seat's pad back off its
`BuiltInSeat`, and `FlightPads` is the binding a launch carries, the answer both presentations
hand the feature's `Choices`. Read `src/UI/Menu/PlayerSetupFeature.cs` for the seats themselves.

## src/UI/Screens/MenuControlsSeats.cs
The rebinding screen's seat bookkeeping, for any presentation. `Sync` keeps the shared
`ControlsFeature`'s player rows in step with this frame's pollers: a registration stays while the
same poller holds its number, and a seat with nothing to press gets no row. It also follows the
stick profile set into seat 1's registration (`ControlsFeature.Follow`). `Forget` drops every row
on a presentation's activation, since the pause leaf registers the same numbers. `PadOf` is the
identity a context's rows sit on, for the capture reader and the captured control alike. A seat is
staged from the menu poller's live map plus the saved flight and camera maps. Read
`src/UI/Menu/ControlsFeature.cs` for the editing itself.

## src/UI/Menu/InstantActionFeature.cs
Instant Action as a shared `IMenuFeature`, owned by the host's feature set and configured by both
presentations. The option sets are static and decoded: the environments, the mission types with
the bans a chapter and stunt flying impose, the eleven airframes, the militias with their
aircraft and wave accent, the skills and the preset table. The setup is typed state with semantic
operations: select and confirm an environment (which re-fits the mission type and loads the chapter's own base def), the mission type, the lives,
the race window (3, 5, 10 or 15 minutes, offered by `OffersRaceWindow` to a stunt run with more than one seat, the same answer that hides both presentations' lives control, since a race spends none), the four waves, the wingmen, both plane
picks and a preset. `Refusal`/`CanLaunch`, `BuildDef`, `LaunchWingmanFit` and `BuildExit` are the gate and the launch.
`Discard` resets every field. Decode: [../formats/instant-action.md](../formats/instant-action.md).

## src/UI/Boards/MovieSurface.cs
A movie as something a composition can draw: a `CSVM.Video.MoviePlayback` and the `ImageTexture`
its pixels are uploaded to, made once and updated in place. There is no node, so a caller hangs
the texture where its own layout row puts it and this surface never learns which screen that is.
`Open` answers null for a file that cannot be read or is not a movie, because a screen missing its
background still has everything else on it. `Advance` says whether the picture changed, so a caller
repaints on the frames that need it and no others. Every timing decision belongs to the playback,
which holds no engine type, so this half is the upload alone. Read `src/Video/MoviePlayback.cs`
next.
