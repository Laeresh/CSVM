# UI

The launchscreen and splitscreen rig, plus the interactive debug labs (including its `UI/Menu/` subfolder). Every lab has a scripted `--debug-*` twin so a finding can be reproduced headlessly; see `docs/cli.md`.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/UI/LaunchMenu.cs
The Built-in presentation's launchscreen: one CanvasLayer holding the whole screen graph and every
Godot control behind it. Mode leads to Chapter and Plane for Free Flight and Dogfight, and to
Instant Action's own wizard; the Options, Controls, hangar and campaign doors hang off the same
graph. It owns the drawing, the per-seat `MenuInput` polling, the join scan, the screenshot key and
the mouse (player 1's rows take Godot's hit test through `gui_input`, folded into the next frame's
step, Accept and Back), and nothing else: rosters, seats, picks, gates and the typed exit are
the host's features (`Menu/MenuHost.cs`), the layout is `MenuZones`, and the hangar and campaign
screens are `HangarFlow` and `CampaignFlow` drawn through `ComposedBoardView`. Contract: [../menu-presentations.md](../menu-presentations.md).

## src/UI/MenuZones.cs
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

## src/UI/PlanePickerRoster.cs
The picker roster rule behind every human plane pick, engine-free so it tests without a menu
instance: `Build(stock, customs)` lists the stock rows in their given order, then one row per saved
`CustomPlaneDef` in the store's name-sorted order, each carrying its store name and its airframe's
stock node, skipping a campaign plane nobody has exported. `AirframeNode` and `AirframeOf` are the
airframe-id to `player_*` node table and its inverse, `IndexOf` the after-build auto-select's
case-blind lookup. Deliberately not `Session.PlaneRoster`, which answers "which plane does player N
fly" off a `SessionSpec`: this is the menu-side list, that one the session-side read. Tests:
`CSVM.Tests/PlanePickerRosterTests.cs`.

## src/UI/HangarFlow.cs
The Build Custom Plane flow, Built-in's walk of the shared `HangarFeature` (`Menu/HangarFeature.cs`),
engine-free the way `BoardMenu` is: the launchscreen owns the Godot controls, the feature owns the
scratch plane, the rules and the store operations, and this file owns the screen order, the cursor
and the pages. `Order` is the original's nine screens and `PageFor` maps each to its `IHangarPage`,
the mount point handing the shell rows, a detail line, a stepper, the totals line's subject, each
row's would-be cost and optional art; over a campaign the flow adds the wallet line beside the
totals and the mark on a row the funds cannot cover. Nothing is written until `Commit()`, so
cancelling is residue-free. Screens, economy and the cash note: [../org/hangar.md](../org/hangar.md).

## src/UI/Hangar*Page.cs
The eight hangar screens the flow walks, one file each, every one a `HangarPage` editing
`HangarFlow`'s scratch plane and priced through `HangarEconomy`: airframe (eleven rows, raising the
defaults ask on a swap that changes an edited build), engine (the airframe's six plus the explicit None row), armour (four zones
on the dropdown's units-times-five scale), guns (four slots stepping the eleven-entry calibre
cycle), hardpoints (a count per wing), paint (a pattern, three colour and shade pairs and three
decals over a live preview), name (two word lists, or typed over) and purchase (the itemised bill
and the gate in the original's own words). The plane-selection screen is `HangarFlow`'s own.
Strings, dropdowns and prices: [../org/hangar.md](../org/hangar.md).

## src/UI/PlaneNameTables.cs
The two word lists the PLANENAME screen composes a name from, and the roll across them. Authored
fiction rather than a decode, which is why it is code and not a data table: there is no original
table to diff against, and an unreadable file would leave a pad with no name to offer. Every
adjective is meant to read against every noun, so the pair needs no compatibility table.

## src/UI/PlaneDiagrams.cs
The two plane-diagram sheets the original draws beside a fitted aircraft: a plan view and a head-on
view, each one tall PNG of eleven equal frames in airframe-id order. `Frame` slices one airframe's
frame out of a sheet, and a sheet whose height is not a whole multiple of the airframe count draws
nothing rather than a mis-sliced picture. Shared because ammo selection, the campaign's flight
check and the hangar's airframe list all want it; decodes are cached per process, misses included.

## src/UI/PlaneFit.cs
What one campaign aircraft is carrying: the four gun slots, barrels by calibre, hardpoint count,
armour units and engine id. Resolved from its hangar build where it has one and from the stock fit
where it does not, which is the case for the profile-seeded starters and every granted reward
aircraft. Engine-free, so the screens that print it test off engine. The caller resolves the build,
never this class. The wallet and the award templates: [../org/hangar.md](../org/hangar.md).

## src/UI/PlaneRatings.cs
The four ratings the plane selection screen prints beside an aircraft, each a 0-to-4 index into
langui 501-505, Poor to Excellent. All four are the original's own integer arithmetic over one plane
record and its airframe's stat row: the engine's power for speed, the armour units, the agility stat
and what the armament weighs for offense. The four formulas and the aircraft they reproduce:
[../org/hangar.md](../org/hangar.md), "The four rating words".

## src/UI/CampaignFlow.cs
Built-in's campaign screen graph as one engine-free flow over the shared `CampaignFeature`
(`Menu/CampaignFeature.cs`), the same split `HangarFlow` makes over its own feature: the feature
owns the profile, the seated player and every write into the store; a page owns its rows and its
navigation; the launchscreen owns every Godot control. Screens are a stack rather than a fixed
order, since the campaign's navigation is a graph, and `Registry` maps a `CampaignScreen` to its
page factory. A page contributes pictures, strokes and captions and names which authored button
each row presses; `CampaignBoards` supplies the geometry through `Layout`, which is Built-in's
alone. `Modal` and `Message` are the dialog and the refusal band every screen shares; `OpenCabin` is every door onto the cabin, RETURN TO CABIN and the back press included, and `OpenScrapbookAfterMission` the mission end's door onto the book, each where one of the feature's two cinemas plays.

## src/UI/Menu/CampaignFlightField.cs
Owns a campaign sortie's humans as part of the shared `CampaignFeature` (`Feature.Field`, in
`CSVM.UI.Menu` so either presentation walks the same field): joined count, the flight check showing,
and each guest's pick. Player 0 keeps the seated profile's aircraft; later players fly
session-scoped stock records or copies, so a guest's edits cannot persist.
`Advance`/`Retreat`/`Rewind` walk one reused flight-check page through the field, and the seated
player's first FLY MISSION latches `Locked` across that walk. `Taken` and `Choose` are the
no-duplicate rule, stock picks compared by airframe and profile picks by plane name.

## src/UI/Campaign*Page.cs
The ten campaign screens, one file each, every one an `ICampaignPage` over `CampaignFlow`: the
player roster with its name field and confirmed delete, the cabin hub, the memento chooser its wall
opens over `Session/CampaignMementos.cs`, the previous-missions contents list, the briefing with its
revealed map and parchment note, the flight check, ammo selection, plane selection with its ratings
and export, the scrapbook and one scrap's zoom view. Each names its own `LAYOUT.CSV` script and
reads geometry through `CampaignLayout`, so a page holds rows, detail text and its own refusals and
nothing about pixels. The chrome: [../org/campaign-board.md](../org/campaign-board.md) and
[../org/debrief.md](../org/debrief.md); the scripts: [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/UI/CampaignCombo.cs
A campaign screen's drop-down field (`PS_D_PILOTPLANE`, `OL_D_AMMO0`): the authored rectangle, the
row height and visible row count the list opens at, the window that scrolls when the cursor leaves
it, and a closed field's horizontal step. It never moves its own pick; a candidate comes back and
the owning screen selects, because some screens refuse some picks.

## src/UI/CampaignModal.cs
The dialog a campaign screen raises over the composed board, the original's `messagebox.script`: a
message, one button and the callback its answer runs. Held by `CampaignFlow` rather than by a page,
since two screens reach the same box.

## src/UI/CampaignTextEntry.cs
A campaign screen's one-line text field, the original's `cm_e_name` edit box: typed from the
keyboard and stepped through one alphabet from a pad, so the field needs no keyboard at all. What it
accepts is the shared feature's own name rule, so a stepped or typed name is always one the feature
would seat.

## src/UI/CampaignAidScript.cs
The input script a campaign screenshot aid's `--menu=` colon argument spells, replayed on the flow
where the walk left it: counted cursor verbs, a confirm, a back and a secondary press, joined by
`-`, plus a word naming a `BoardButton` to focus and confirm. A confirm is what lets an aid leave a
drop-down standing open, which a step count could not reach. A count with no verb after it is a run
of downs, so a bare number is the step count it always was, and `export` is a button word rather
than a case beside the language. One grammar serves both presentations, and the replay refuses a
script spelling a press the running one lacks, which is how the secondary verb fails loudly under
Original. `docs/cli.md` states the grammar for the command line.

## src/UI/ScrapbookComposition.cs
The scrapbook's per-spread scrap layout, read from the shipped `SCRAPBOOK.CSV` rather than invented:
`Items` walks a spread from item 1 and stops at the first missing key, the way the original's reader
does; `Pictures` gates each row against the mission's merged best-to-date mask and stacks the
survivors by draw order; `Openable` narrows the same gate to the rows that open a detail view;
`ZoomFamily` reads a family's three text boxes. A player capture resolves through a caller-supplied
path rather than the asset library and is skipped when no file is there. Parsed rows are cached
per file behind a lock. The columns and the gate: [../formats/campaign-screens.md](../formats/campaign-screens.md).

## src/UI/ScrapbookExport.cs
EXPORT TO DESKTOP's copy: the open scrap's own file to the desktop under its own base name,
overwriting, answering whether it landed and either the name or the OS reason, which are langui
705's and 706's arguments. Engine-free, and the folder is a parameter so a test writes elsewhere.

## src/UI/ListWindow.cs
A scrolled list as a pointer sees it, in the board's authored pixels: the window's box, the thumb's
box on its track, and where the list stands inside it. `TopAfterWheel` steps the window by rows and
`TopAfterDrag` maps the thumb's free run down the track onto the rows the window can move, both
clamped; `ThumbYFor` places a thumb for a given window, and is the one rule every list draws its
thumb by. Each list widget builds one from its own geometry and the presentation that owns the
pointer decides what a new top writes back, so the arrows and the keyboard keep their own rules.

## src/UI/SliderTrack.cs
A continuous control's track as a pointer sees it, the list window's opposite number: the slot a
thumb slides along, the thumb's own size, and the whole numbers the slot spans. `ValueAt` reads a
pointer's X as a value and `ThumbX` puts the thumb back where it read, so the drawn thumb sits under
the finger that moved it; `Stepped` moves a value sideways. Every answer is clamped into the range
and none wraps, which is what keeps a step away from an end from landing on the other one. Each
slider widget builds one from its own geometry and the presentation that owns the pointer decides
what a new value writes back. Driven by `Menu/Original/SliderControl.cs`.

## src/UI/BoardFit.cs
How the original's fixed 800x600 campaign dialog space lands on an arbitrary window: one uniform
scale on both axes, the board centred, the remainder letterboxed. A `record struct`, so every
element goes through the same mapping and only the scale changes; a viewport with no area falls back
to 1:1 rather than a scale nothing can draw at. The rejected alternatives and why the art is sampled
nearest are in [../org/campaign-board.md](../org/campaign-board.md), and bind every campaign screen.

## src/UI/ComposedBoard.cs
What a composed campaign screen is made of, engine-free: the screen's fixed backdrop, the fills a
page paints on it, pictures at authored pixel positions, connector strokes, text lines, button
plaques and flowed list widgets, each in draw order. The backdrop is its own layer so a fill can
sit over the background and stay under the page's pictures, where a selection bar goes. `BoardNote`
is a widget's entries plus its wrap box (cut at a word where the box has no room for the rest, shrunk
to a face the whole list fits in, or no box at all where the widget's own list stops nowhere), its
marks, and `BoardCaret` an edit box's cursor on the line it follows, all placed by a caller that can measure text. `PlaqueFrame` and `PlaqueInk` are a plaque's states, and a plaque whose art leaves
part of its frame empty carries its label's own baseline. `BoardArt` names a file and its frame count and the renderer resolves it; one of its four libraries is a movie, so a background film reaches the backdrop with no engine type here. `BoardCrop` takes a region of the source instead of the whole frame, which is a chart sheet's own window.

## src/UI/CampaignBoards.cs
The fixed chrome of all eight campaign screens, plus the composer that turns a page and a cursor
into a `ComposedBoard`. Every button slot, background pane and text slot names its `LAYOUT.CSV`
section and row and reads through `CampaignLayout` with the value the board drew before the layout
existed as its fallback, so a screen composes the same with or without the file; the briefing's
chrome is `Briefing.zrd`'s own, and a slot marked pinned keeps a measured value instead. `SlotOf`
and `DialogSlot` answer a plaque's rectangle for a pointer to hit-test, `DetailSlot` and
`DetailPaned` the description panes, and `DialogChrome` the messagebox widget set a box draws and
where its pane lands. The pinned values: [../org/campaign-board.md](../org/campaign-board.md).

## src/UI/CampaignLayout.cs
The decoded menu layout as the campaign boards read it: one widget row's authored geometry and art
by section and key, every read taking the value the board drew before the layout existed as its
fallback. Engine-free, over `Menu/MenuLayout.cs`. `At` and `Box` answer with the whole row or the
whole fallback, never one coordinate from each, so a row missing a column cannot shift an element
half-way. `For(dataRoot)` reads the extracted layout once per data root and keeps it; a missing or
unreadable file is the `Fallback` instance with its reason logged once. The file's own sections and
keys: [../formats/menu-layout.md](../formats/menu-layout.md).

## src/UI/ComposedBoardView.cs
The Godot half of the campaign boards: draws one `ComposedBoard` over the whole window through
`BoardFit`, with texture filtering pinned to Nearest so the authored pixel grid stays hard. Owns the
texture cache and the only art resolution there is, mission art and screen chrome under their own
extraction roots, and caches a miss so an absent extraction is probed once per name. A movie resolves
to a `MovieSurface`, whose one texture the cache holds and the surface rewrites in place, so the
picture animates with nothing invalidated; `AdvanceMovies` runs their clocks off the caller's own step and
`AdvanceCaret` blinks a text cursor off it, each saying whether to repaint. Supplies the font metric a flowed
`BoardNote` and a caret cannot take, `Fitted` shrinking a note's face until its list fits its box rather than losing a row, and the two-line hint band a pad needs.

## src/UI/CinemaScreen.cs
One cinema on screen: a `CinemaPlayback`, the `ImageTexture` its pictures upload into, and the
`AudioStreamGenerator` its samples are pushed to on the Voice bus, a cinema being a narrated film
rather than score or world sound. The picture fills the same 800x600 rectangle `BoardFit` maps a
board into, so a cinema and the screen it hands off to own one area of the window. `Open` answers
null for a file that will not read, `Ended` is how a flow learns it stopped, and `CinemaSkip` is
which presses end it early, the per-cinema differences there being the original's own. It mounts
itself on `HudLayers.Cinema` and frees itself; `Session/Launcher.cs`'s `PlayCinema` is the seam.
The three authored sets live here as constants and `CinemaSkips` answers them.

## src/UI/CinemaSkips.cs
Which press ends a cinema, for every screen that offers a skip. `Skips` is the one member that
decides, and it takes a `CinemaPress` rather than a device event, so all three sets are pinned off
engine; `PressOf` is the engine's half, reading a key, a click or a pad button into one of those and
holding no policy of its own. A press is not a set: only an any-press set takes a key with no name
of its own. A pad button is in every set, because a player holding one has no other press to offer
and would otherwise sit through a 145-second film; it counts only where pad input does at all
(`--no-pads`, an unfocused window), since a pad reports its first button as it connects. What each
cinema's set is, and why they differ, is [../formats/cinemas.md](../formats/cinemas.md).

## src/UI/CinemaHandoff.cs
What every cinema flow shares. `CinemaPlay` is the shape of the call that puts a film on screen, which
`Session/Launcher.cs` satisfies by handing over `PlayCinema` itself. `Once` wraps the continuation a film hands off to: a
skip can land on the frame the film plays out and both paths end it, so the next screen opens once however many times the
cinema reports it stopped; the boot block, whose continuations start the next film, chains unwrapped. `CinemaFilm` is for
the screen a film stands in front of rather than a flow that chains them: `Play` spans one film, `Up` says the film owns
the frame, and `Swallows` says this frame is the tail of the press that ended it, the pointer's lasting until the button
comes up and every other press spent where it lands. A screen without it reads the release of a press it never saw go
down as a gesture of its own. Which presses end a film is `CinemaSkips.cs`'s, not this file's.

## src/UI/BootSequence.cs
`fmv.zrd`'s boot block with no engine in it: `Card` composes the copyright card in the authored
800x600 space out of the extraction's own art, message-table strings and font metrics, and `Run`
calls the block's eight actions in the reader's order over three injected delegates, a `CinemaPlay`
for a film, one that puts up a still and one that takes the card down as the first film starts. Every
name, position and duration is the reader's ([../formats/cinemas.md](../formats/cinemas.md)), which
is also where the card's one showing, the unseen fade and the films running back to back are
settled; `Held` is the one member that says how much of an authored hold reaches the screen.
`BootCard` supplies the stills, `Session/Launcher.cs`'s `PlayCinema` the films.

## src/UI/BootCard.cs
The boot sequence's engine half, and the only file that knows a boot still is drawn at all: the
black the block runs on, a `ComposedBoardView` for the card, a countdown per hold, and the press
that ends a hold early, read through `CinemaSkips` against the films' own set so a still and a film
answer one rule. It mounts on `HudLayers.Board`, the launchscreen's own layer, so a
film at `HudLayers.Cinema` covers it; the card goes down with the first film and the black outlives
it, and a hold of no seconds runs on without a frame of its own. `Play` is the whole surface: it
mounts the node, runs a `BootSequence` over the caller's film call, and frees everything before the
handoff.

## src/UI/BoardPalette.cs
The ink a campaign board writes in, one palette per background family, because the screens are
painted art and the grey the flight check's forms use is invisible on the cabin's dark hangar. The
flight check and ammo values are their layout rows' own ARGB fields; the rest are chosen to read on
their background, and [../org/campaign-board.md](../org/campaign-board.md) says which is which.
`EscapeBlackboard` is the one crossing, the load screen's own chalk under the near-black labels the
escape strips' light plates need, which an Instant Action pause is the only screen to want both of.

## src/UI/BoardMenu.cs
A board's cursor and item list, engine-free so the selection rules test off engine. Holds no input
source: the board polls its owner through `MenuInput` and feeds one frame to `Handle`, which is what
stops a pad steering a menu it does not own, and the return says whether the highlight moved so a
board repaints only when it has to. It opens on the first item, so a board orders its rows with the
harmless one first and a stray confirm on a menu that just appeared cannot destroy a run. A results
board is not dismissable, since dismissing it would leave the player in a halted world with no way
back. `MoveTo` is how a board's pointer puts the cursor on the row under it, refusing a row outside
the list so a miss leaves the cursor alone. Off-engine coverage: `CSVM.Tests/BoardMenuTests.cs`.

## src/UI/LoadBoard.cs
The load screen drawn over the whole window while a session builds: `LoadScreens`' composition
through `ComposedBoardView`, so it inherits the authored-pixel surface and `BoardFit`'s scaling.
Populated in `_Ready`, since the view sizes itself off the viewport, and it tracks the window every
frame the way every shared board does. Holds no composition of its own, so what the screen says
tests off engine; a campaign launch hands it the `LoadSheet` its story position resolves.

## src/UI/LoadScreens.cs
What the load screen is made of, engine-free. `LoadSheet` is the campaign screen's authored half,
one `Loading.zrd` dialog with its mission's objectives and the seated profile's own memento; `LoadScreens`
composes either that chart sheet, through `MissionMap` the way `PauseScreens` does, or the Instant
Action blackboard with the four texts its own `loading_i` dialog places; `DialogTexts` takes that composition by file and key, so an Instant Action pause writes its `ia_escape.zrd` dialog's texts through it. The mission type picks the
blackboard's dialog by the exe's own letter; free flight and dogfight are ours, so they write the
mode's name and nothing else. An absent extraction yields the frame and the bar rather than
throwing, since this screen is shown while everything else is still loading. The dialogs, the beat
sheet and the face mapping: [../org/loading-screen.md](../org/loading-screen.md).

## src/UI/PauseScreens.cs
What the Original presentation's pause screen is made of, engine-free: the frame behind it, the
mission's chart at its authored source crop, the pins and icons its dialog's script places, the
objectives parchment, the memento, and the labelled button strips, the block's four plus the remake's own PHOTO MODE at the place that block leaves free. An Instant Action sortie's dialog carries none of that and draws the load screen's blackboard instead, its four texts composed through `LoadScreens` and its parchment left off by the dialog's own script.
`PauseSheet` is the authored half, read once per sortie, and `PauseReadout` the live half, read afresh on every
pause: its memento is the seated profile's own picture, `Rows` marks a note line by the runtime's answer for that line's own objective number, and
`Icon` turns one world pose into the chart icon a session and a suite place alike, through the
shared `MissionMap`, which draws nothing for a pose off the window. `RowAt` is the pointer's hit
test over the five 132x28 plates, and a pointer draws the dialog's own cursor; an unreadable extraction leaves the pause to the Built-in board. Decode: [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/MissionMap.cs
The one chart drawer every screen showing a mission's map shares, engine-free: the sheet as a
cropped picture, a reveal's visible elements as pictures in placement order over two layers, its
connector lines as strokes, and one icon placed by world position through the map's own window,
turned so its drawn nose reads against the compass: the heading, less however far that bitmap's own
art is drawn off the top of the sheet. It exists as one module because the original reaches all of
it through one control class from two dialog constructors, so the briefing, the pause screen and
the campaign load screen cannot drift apart here.
Decode: [../org/pause-screen.md](../org/pause-screen.md).

## src/UI/BoardMenuItem.cs
The rows a board menu can offer: Resume, Photo, Restart, Preferences and Exit. The board owning the
menu decides which it carries and what each does; Resume appears only on a pause board, Preferences
only where a `PausePreferences` leaf stands behind it, and Exit's label follows whether the session
can return to the launchscreen or only quit.

## src/UI/CursorRow.cs
One centred list row with its cursor marker, shared by every menu that has one: the launchscreen's
screens, its per-player aircraft panes, and every board menu through `BoardMenuView`. The marker is
a cell of its own with a mirror cell opposite it, which is what puts a label on the panel's centre
line whether or not its row is selected; the rule and its failure mode sit on the marker itself.

## src/UI/BoardMenuView.cs
Draws a `BoardMenu`'s rows as `CursorRow`s inside the board style all five boards share, so the
cursor reads the same wherever it appears and a layout fix lands once. `Refresh` recolours from the
current highlight, touching only label overrides. The footer is the button legend, since nothing
else on a board teaches the cursor, and a results board's names no back key.

## src/UI/BoardMenuHost.cs
`BoardMenu` plus `BoardMenuView` plus the reader, kept together so a board wires a menu in two lines
rather than restating the poll, handle and repaint order five times. `Build` primes the reader, so a
button still held from whatever raised the board is not read as a fresh press. It reads the pad's
back button alone, Escape and Start reaching the pause toggle through `FlightController` instead.

## src/UI/MenuInput.cs
One player's menu input source: the keyboard flag, a `Pads` binding and the edge and auto-repeat
state, with `Poll(dt)` filling the cursor axes, accept, back and start out of the `Menu` binding
context (`src/Bindings/`) from three readings of one seat: keyboard live, keyboard minus the
typeable keys, and the pad alone. Its pad rows sit on the seat-local `SeatPads` identity, since a
seat reads a set of pads and no binding may hold a connection index. `Typed` and `Erase` serve a
text field, `PadMove`/`PadMoveX` are the axes such a screen reads instead, since W, A, S and D
are letters there. `TypeableKeys` is deliberately wider than any box's accept rule. Wrapped by
`Menu/BuiltIn/BuiltInSeat.cs`, bound by `MenuSeatDevices`; it also serves the in-flight boards.

## src/UI/HudLayers.cs
The canvas-layer ordering for everything drawn over the 3D view, in one place, so "does the collider
overlay draw above the cloud whiteout?" is answered by reading one file rather than nine literals.
The order is measured off the original's footage rather than chosen, except for the debug and lab
layers, which the original never had and which sit above the sun wash on purpose. The evidence for
the wash-over-HUD ordering is a verification rule; the weather decode is [../org/weather.md](../org/weather.md).

## src/UI/SplitScreen.cs
The splitscreen rig for two to four players (one player never constructs it): the black gutter
backdrop, one `SubViewport` pane per player sharing the main `World3D`, and the player colour and
tag table. Two panes stack, or stand side by side once each half would still be wider than it is
tall (`SideBySide`, true from 2:1 out); three and four are the 2x2 grid. One world means one sun
and environment per pane, so enhanced graphics need no pane-local plumbing. **Every pane is a 3D
audio listener**, or nothing positional is audible at all: Godot takes the per-channel maximum
over listener-enabled viewports. `Fill(true)` gives pane 1 the whole window for a cutscene, a
visibility change and one rect rather than a rebuild. `NoteSkip` names a skipping player.

## src/UI/ScreenFlash.cs
The full-screen colour wash, two channels over one hidden `ColorRect` per rendered view. The ramp
channel is the `FBFX_COLOR_FROM_TO` wash a close HE, AP or flak burst authors, lerped in RGBA on sim
time and then ended rather than held, replacing whatever that pane was running, and painted on every
pane whose own camera stands inside the burst def's authored player-range gate. The blend channel is
`BlendWash`, addressed by the struck aircraft's player index and painting nothing for an AI. The two
composite at paint time alone, the blend over the ramp, so a pane with no blend wash paints exactly
the ramp's own colour. One state per pane, never one global: in splitscreen each pane is its own
picture. The wash keys: [../formats/anim-definitions.md](../formats/anim-definitions.md).

## src/UI/BlendWash.cs
One pane's victim-routed wash state, the original's start and tick routines held per pane instead of
in their one global; pure state and arithmetic, no node, so the rules test off the engine. A first
hit takes its weight as the peak and starts the displayed weight at zero; a hit landing on a running
wash blends both the weights and the colour by the original's own arithmetic and restarts the
envelope without dropping what is displayed. The envelope is attack, sustain and release at fixed
fractions of the duration, stepped on sim time, with a hard cut at the end. `Composite` is the
paint-time rule `ScreenFlash` applies. Decode: [../org/ordnanceTypes.md](../org/ordnanceTypes.md).

## src/UI/ObjectivesHud.cs
The flown campaign mission's objectives readout, drawn on the pause screen and nowhere else: the
original keeps its objectives on the pause parchment and leaves the flight HUD to the gauges. Reads
`CampaignDirector`'s `ObjectiveGraph` rows directly, resolves text through the message table, and
shows every row rather than gating on its awake flag, which the member explains; a row with no
message key is dropped from the drawing but still counted. A completed row is marked with the art
(`obj_check1`, loaded once through `LoadMark`) centred on that row's origin, over its leading
characters, and keeps the colour an open row carries. Self-mounting, and one per rig where
`PerfHud` is one per window. Decode: [../formats/objectives.md](../formats/objectives.md).

## src/UI/MissionEndFade.cs
A full-screen `ColorRect` on a `CanvasLayer` at `HudLayers.MissionEndFade`, polling
`CampaignDirector.LeavingFade` every frame and painting that straight onto the rect's alpha. That
layer sits above the flight HUD and `SunWash` but under `Debug`/`Lab`, so the fade darkens the HUD
and the wash the way the original's copied framebuffer does, while the debug instruments stay
readable through it. This paints live over the running world instead of freezing a copy, since the
hold already stops the sim clock underneath it. Self-mounting, one per rig's `HudParent`, hidden
until the director's fade leaves 0. Decode: [../formats/objectives.md](../formats/objectives.md),
"The mission-end path, and what the player sees after it".

## src/UI/LiveryLab.cs
The `--viewer` livery editor (key L): a squadron stepper that loads the whole squadron livery,
per-slot RGB sliders, decal steppers, a random livery and copy-CLI-args.

## src/UI/NodeLabels.cs
Floating node-name labels (key F16) in both the static viewer and flight, cycling off, meshes and
all; `--debug-names` presets the mode at launch. In splitscreen the nearest and de-clutter pick is
player 1's viewpoint alone, while every pane still renders the resulting labels, since they are
ordinary world-space children of the root.

## src/UI/MarkerOverlay.cs
The `--viewer` marker overlay (key K, `--markers` at launch): every firepoint, pylon and target on
the parked aircraft as a coloured gizmo with a billboarded label. Reuses `MarkerRig`'s own
classification and co-location grouping, so its gizmos agree with the marker dump by construction.

## src/UI/PhotoModeHud.cs
Photo mode's only screen furniture and its way out: a hint line naming the bindings on a layer of
its own, and the Escape or pad-B read that raises `Exit` for `GameSession.ExitPhotoMode` to act on.
It decides nothing about the mode itself. The hint fades rather than persisting, since the mode
exists to compose a frame, and the fade runs on wall time because photo mode holds the clock. Pad
reads go through the seat's own device filter, so in splitscreen another player's pad cannot close a
mode that is not theirs. The mode itself is `Session/GameSession.cs`'s.

## src/UI/PerfHud.cs
The frame-cost readout (key F14, `--debug-fps` presets it): fps, the current frame's cost and the
worst recent frame, cycling off, compact and full. Built once by `Launcher`, never per session and
never per pane, since fps, frame cost and GC counts are process-wide facts; that hosting is also
what makes it work at the launchscreen, in the viewer and in flight alike. Fed the same raw
stopwatch cost `HitchMonitor` ticks on, every frame, so the worst-frame peak is already warm when
someone presses the key, and off by default so the golden screenshots stay byte-identical. Full adds
the frame's cost split, GC counts, breadcrumbs and a rolling frame-time strip, every term a second
view of data collected elsewhere rather than a new sample. What a frame number proves: PERF-1.

## src/UI/BuildStamp.cs
The build's version as `CSVM v<version>` in the menu's bottom-right corner, so a screenshot a
stranger sends carries the build it was taken on and the number is not read as the original
game's own. Built once by `Launcher` beside `PerfHud` and shown off the menu host's own "the
menu is up", which is what puts it on every presentation at once: the stamp is a fact about the
binary, not part of a presentation's screen graph, and Original draws decoded artwork with
nowhere to put one. It draws on `HudLayers.PerfReadout`, above the boards, for the same reason
that readout does. Hidden in flight, so no golden screenshot ever sees it. The number itself is
`Utils/BuildVersion.cs`.

## src/UI/NoGameDataScreen.cs
The dead end a launch with no extraction under the data root reaches instead of the menu: the
title, the sentence naming the step that produces the data, the path that was looked in, and Esc
as the way out. `Missing` is the whole test, an absent or empty `extracted` directory, and it is
engine-free so the launcher's branch and its unit read one rule; `Instruction` is the single
sentence the screen and the launcher's own log line share, worded for a release payload
(`Extract.cmd`) or a repo checkout (the two extractor scripts). Provenance is not asked about
here: `Session/ExtractionStamp.cs` owns whether an extraction is stale and stays a warning.

## src/UI/MeshLab.cs
The geometry and shading lab (key M): normal lines, the smoothing-seam wireframe, collider boxes,
light sliders with a headlight, and cull by normal-source override cyclers, all scripted by
`--debug-mesh`. Two shapes: the `--viewer` lab owns the parked plane, and the scoped lab over a
`SelectionService` attaches to the current selection and restores on a change or a deselect. Its
override shader samples through `SceneBuilder.SampleAlbedo`, so a surface under the cull override
keeps the chapter's mip LOD bias and the lab stays a diagnostic twin of the real arm.

## src/UI/WeaponLab.cs
The weapon lab's panel (`--weapon-lab`, key B): a configurator for the held aircraft's live loadout,
hosted top right in flight. It owns no weapon and fires nothing; the steppers write into the
controller's bound loadout and the aircraft's own trigger fires it. Gun steppers arm a gun group,
hardpoint steppers re-arm every pylon, and "reset to stock" restores the fit the session launched
with. A left click casts the lab's own ray, names what it hit and re-parks the held plane on that
ray at the panel's stand-off; the scripted twins all end in the same placement. V hands the rig's
camera to a spectator camera and back. In splitscreen the lab stays player 1's alone, one panel on
rig 0's aircraft, and a log line says so while the other panes fly normally.

## src/UI/PanelFocus.cs
`Strip(subtree, who)` makes every control under a panel unfocusable and logs the tally, the
invariant every panel hosted in a flight session must hold. A focused button answers Space with
"press me again", so the pilot's fire key re-fires the last stepper instead of the guns and the
arrow keys walk the focus chain instead of reaching the aircraft or the lab's orbit camera.

## src/UI/SelectionService.cs
The shared world selection in `--freecam` and `--anim-lab`: a left click picks the mesh under the
cursor, PgUp and PgDn walk its `cs_name` ancestor ladder, and a breadcrumb line and wireframe box
show the current rung. Objects are picked by box, map-scale meshes (terrain) by triangle. `Current`,
`Ladder`, `Level`, `CurrentBox` and the `Changed` event are what the other inspect tools read, and
`--debug-select` replays a click for a scripted run. A Ctrl-held pick is reported as `CtrlPicked`,
and a tool may append a line to the breadcrumb through `HudLine`, which is how the export set
attaches without this service knowing what an export is. `ExtraRoots` walks props parked beside the
world content; `SubtreeWorldAabb`, `NewBoxInstance` and `DrawBox` are shared with the other tools.

## src/UI/TargetingOverlay.cs
The targeting overlay (key F15, `--debug-targets`): a per-frame line from every turret gunner and AI
gunner to its acquired target, coloured by the gate holding the trigger, with that gate named per
shooter in the HUD. Depth test off, since the line into a hull is the one worth seeing. In
splitscreen the world-space lines draw in every pane on their own while the roll-call is drawn once
for the window, like `PerfHud`, because it is process-wide combat state.

## src/UI/DebugKillTarget.cs
The debug kill key (F17): kills player 1's currently selected target through its own death path, so
kill counts and objective bookkeeping see it exactly as a real shot would, never by freeing the
node. It routes on the selection's source type, an aircraft through the attributed crash path and a
zeppelin sub-part through the anim runtime's damage call, the same call a rocket makes; a turret
selection is inert for a reason the member states. Player 1 only, the precedent the world damage lab
and the weapon lab already set. `KillSource` is exposed so a suite can drive the routing against
hand-built sources with no live candidate scan behind it.

## src/UI/TileGridOverlay.cs
The map-edge tile-grid overlay, flag-only (`--debug-tilegrid`; no key is bound): every ground tile
tinted by repetition band, so one colour band is one block. `--map-edge-block` and `--map-edge-mode`
set the depth and the fold once at launch. This is the instrument the map-edge fold was settled
with; the measurements are in [../formats/world-structure.md](../formats/world-structure.md).

## src/UI/ColliderOverlay.cs
The collision wireframe overlay (key C, scripted by `--collision=show` and `--debug-colliders`) in
`--freecam`, `--anim-lab` and `--fly`: one immediate mesh per collider host, coloured by the surface
id its body resolves to plus the three owner keys neither surface tag decides, rebuilt from the live
tree on every show. The legend is sourced from the colour function alone, so a palette change cannot
desync it, and it is drawn only where wireframes are. The id drawn is the resolved one and the
picture is of what the engine will select, not of the material data: the id is stamped per body
while the displayed name comes from the texture-derived class, and the two can disagree.

## src/UI/AiNetsOverlay.cs
The AI patrol-net overlay (key F13, `--debug-ainets` scripts it), added to every chapter world by
the session's world stage. Draws each net in a stable id-derived colour, edges as segments off the
edge list, sphere markers per node and one label per net, all depth-tested; nets load lazily on the
first toggle and the census goes to the world log. A HUD field narrows the drawn set by name prefix.
It also draws live leashes from each AI aircraft to the node its follower is flying at, filled by a
supplier the session hands in rather than by the overlay knowing anything about aircraft. An
anchored net draws where it actually is, the trailer offset applied per frame as the root's
position, so nothing is rebuilt. The key range: [../controls.md](../controls.md).

## src/UI/ClassOverlay.cs
The colour-by-class overlay (key X, `--debug-classoverlay` scripts it) over the same modes as
`ColliderOverlay`, a findable-targets view rather than a collision one. Mixes a class colour over
every drawn mesh at half strength, so a target stays recognisable as itself: destructible through
the registry's own resolve, facade through the billboard classification, clutter as every multimesh
under the world root, everything else scenery. Rebuilt on every press rather than cached. Keyed on
neither surface tag deliberately: one decides which collider a mesh's polygons join and the other
what happens when you touch it, and neither answers "what is this object".

## src/UI/NodeLab.cs
The node lab (key N) in `--freecam` and `--anim-lab`: the world's `cs_name` tree, a search box,
per-node frame, hide and glTF export into `Exports/`, the export set's three buttons, a dependency readout for the current selection (anim defs, destructible
pools, geometry and textures, colliders) and a destructibles view with coverage columns, plus
top-level branches for props parked beside the world content. `--debug-nodelab` is the scripted
twin. A row's text and colour follow live visibility, re-read on the panel's own status cadence.

## src/UI/ExportSet.cs
The node lab's export set: the nodes gathered with Ctrl+click or the panel's ± set, written as one
timestamped GLB in `Exports/` at their world transforms. It rides `SelectionService.CtrlPicked`
rather than reading the mouse itself, outlines each member in a cyan box that follows that member's
transform, and puts the count on the selection's breadcrumb through `HudLine`, because a set is
gathered whether or not the panel was ever opened. A member freed under it (a destructible swapping
to its wreck) leaves on its own. Nothing is drawn until the first node joins.

## src/UI/WorldDamageLab.cs
The world damage lab (key F5) in `--freecam` and `--anim-lab`: the destructible pools of whatever
the selection holds, each with live HP, and a slider with kill and reset on the one a weapon hit
reaches, driving the anim runtime's damage and reset calls. `--debug-damage` is the scripted twin,
an ordered script rather than a token set. Only the pool the registry resolves is drivable, since a
node can carry several; the rest are listed read-only with the reason, because driving a twin would
damage a pool nothing can ever hit.

## src/UI/OrbitCamera.cs
The static inspection view's orbit-camera controller (drag to orbit, wheel to zoom, AABB framing):
owns the orbit state and drives a camera it does not own. `Frame` takes the eye and pivot the host
resolved, and `MergedAabb` merges a subtree's world-space mesh boxes, shared with the anim lab. The
`lookAt` argument is a pivot point rather than a direction, since with the eye it also sets the
radius the wheel and the drag work in.

## src/UI/AnimLab.cs
The `--anim-lab` debugger: a quiet world stage with a pinned seed, a fixed-dt clock, a transport
panel, a def picker, an `AnimTimeline`, a spectator freecam following the shared selection, and a
staged effect and crash anchor set so placeless on-call defs play at the camera. Interactive frames
draw each live transform-motion target interpolated between its last two sim poses, and sim poses
are restored before any step runs, so render smoothing never leaks into event held-pose seeding and
fixed stepping stays byte-identical. Puffer particle spread is unseeded, so same-step shots differ
in particle noise alone (DIAG-21). The picker toggle is bound away from the camera's target key.

## src/UI/AnimTimeline.cs
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
availability answer, keeps the pre-availability request for Options to show back, and answers a
blank, unknown or unavailable request with a fallback reason rather than a throw. `Show` creates
the selected presentation once and re-activates that instance on every later call, so a return
from flight lands on the screens as they were left; `Tick` runs it only while `Shown`, and
`Deactivate` ends it and discards the features' transient state, the first half of a switch.

## src/UI/Menu/BuiltIn/BuiltInPresentation.cs
The Built-in presentation (`CSVM.UI.Menu.BuiltIn`): `LaunchMenu` registered under
`PresentationId.BuiltIn`. `Activate` builds the launchscreen under the parent node on the first
call and maps the return destination onto it, the top level being the Mode screen, `InstantAction`
the wizard's first screen over the setup that flew, `CabinReturn`
the profile's cabin and `DebriefReturn` the scrapbook on the flown mission; the `--menu=` aid is
consumed on that first call, so a return from flight lands on Mode with the cursors kept. `Tick`
runs the menu's frame, `Hide` takes it off screen and `Deactivate` frees the node. `Menu` exposes
the launchscreen for what is Built-in's alone. Read `src/UI/LaunchMenu.cs` next.

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
(auto-repeated cursor steps, edge presses, typed text, an optional window-pixel `MenuPointer` whose
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
`MenuAudioService` (`src/Session/MenuAudioService.cs`); Built-in's one call site is the briefing
narration.

## src/UI/Menu/MenuExit.cs
The one typed way out of the menu, handed to `IMenuHost.Exit` and consumed by `Launcher`:
`LaunchExit` (chapter, per-seat `MenuSeatChoice`, `MenuMode`, optional `InstantActionDef`),
`CampaignMissionExit` (profile, `cm_sequence` position, per-seat choices), `QuitExit` and
`OptionsApplyExit` (the `PresentationId`, the graphics-mode and difficulty words, the four display settings and the four volume levels, null where never set).
An applied choice rides the exit rather than being saved by the screen that took it, so the options file keeps one writer, and a screen
hands back the settings it does not show; none of the eleven is defaulted, so a page cannot hand back a null it never read. A custom
plane rides the exit as a resolved `CustomPlaneDef`, never a store name. Presentations never construct
sessions. The return side is `MenuReturnDestination`; the exit table and the scans holding the seam: [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/DisplaySettingRows.cs
How the four display settings read as rows, shared so Built-in's Options screen and Original's VIDEO
page cannot disagree about a saved value: one label per `DisplayWords` entry in that order, since a
row reads and writes the store word by index, plus the two forgiving reads (an unknown word is the
vocabulary's first value, a size the screen does not offer is the project default) and the wrap a
sideways step takes. The size row is the one a display mode can own: under a `Pinned` one it reads
the screen's own size whatever is saved. The screens and the sizes are enumerated per machine by
`Utils/MonitorSetting.cs` and `Utils/ResolutionSetting.cs`, which is why neither is a list here.
Engine-free, so the rules test without a screen (`CSVM.Tests/DisplaySettingRowsTests.cs`).

## src/UI/Menu/MenuLayout.cs
The runtime reader of `extracted/rof/menu_layout.json`, the decoded menu layout `ExtractRof.ps1`
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
`Commit` are the purchase gate and the write. It also owns every label the screens write, the wallet
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
The campaign as a shared engine-free feature in the host's feature set: the state and the operations
both presentations read and write, with neither one's screen shell in it. `Open` opens a campaign
over a `CampaignProfileStore`, each further dependency optional. The roster operations create, seat,
delete and record the last-played player in the original's own words; the mission operations settle
which `cm_sequence` entry the screens after the cabin are about, with its briefing, wingman flag,
per-slot change-plane rules and the story aircraft its flight check grants; the writes save the
loadout, the planes, the cabin's memento (refused unless the profile holds it), an exported build and the mission exit. It carries the one `ChapterCinema` and
`ClosingCinema` the host built it with, which is how a cabin or mission-end door reaches a film. Read `CampaignFlow.cs` next.

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
from `[MainMenu]`'s own rows, the two remake-only sortie screens, the Options screen over the decoded Preferences chrome, and the messagebox
idiom every refusal and confirm goes through; the other screens are its own partials, below. `Step` applies one seat's frame, `Compose` is
the screen as a `ComposedBoard` whose backdrop takes a section's `movie` row at its bottom, and every page's row kinds live here,
`OriginalSlider` among them. A pointer press arms a row and only the release still on it activates (`ArmedKey`), on an edit box taking the
caret alone where Accept in one reaches the screen's own commit; the pointer's bitmap answers an enter or leave (`PointerLive`); and a film
in front of the board takes every frame, the tail of the press that ended it included (`CinemaFilm`). Screen by screen: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalDropList.cs
The one rule every open dropdown of the Original shell follows, a partial of it: a page hands over its key, its items,
the box the list hangs under and the layout widget behind it, and takes back the windowed rows, the `ListWindow` for the
pointer and the write that scrolls it. The window is the widget's authored `TotalDisplayed` clamped to the item count,
so a short list is exactly as tall as its items and carries no chrome. Every item is a row keyed `<key>:<index>`, the
ones outside the window built but hidden, since the rows are the hit-test surface and a dropped row would let a pointer
hit what it cannot see; a scrolling list adds `<key>:up` and `<key>:down` in an arrow's width of its own right edge and
hangs the thumb between them. The Instant Action screen, the loadout screen and the two option pages come through here;
`OriginalHangar.cs`'s list does not, its arrows being the closed box's `DropUp`/`DropDown` art. [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/SliderControl.cs
The Original shell's continuous control: a pointer's hold-and-move over a slider row, and the
sideways step that moves one from the keyboard or the pad. It is the shell's second hold-and-move
and keeps a hold of its own, the list thumb's being the first; the shell gives the thumb first
refusal each frame and consults this one only when no list holds, so neither drag can be continued
as the other. `Drive` reports a slider holding the pointer, which is how the shell knows a click was
spent and activates nothing under it. A row declares its slider through `OriginalShell.cs`'s
`OriginalSlider`; this class knows a track and a value and nothing about the setting behind them.

## src/UI/Menu/Original/OriginalGameOptions.cs
The Game Options page, the shell's partial over the decoded `[@GameOptions@]` section. Its content is a
table: per option a key, a title, a description, the control kind and how the store field is read and
written, so a further option is one entry plus its field. Row one is the original's own Difficulty dropdown
at its authored box over the three campaign tiers; under it the remake-only Menu row, the presentation as a
dropdown over the registered tokens, and under that the remake-only Next Target checkbox, the nearest-after-a-kill targeting switch off by default. The row shape is read off the section's widgets, so a layout that moves
a row moves ours, and both pages' open list is windowed and drawn from here on `OriginalDropList.cs`'s rule. ACCEPT CHANGES leaves as the `OptionsApplyExit`; only
`Launcher.ApplyOptions` writes the store. This file also holds the shell's shared `ReadSavedOptions`/`AppliedOptions`
pair, which every option page reads and hands back through, so a page carries the settings it does not show: the display ones stand on VIDEO (`OriginalVideo.cs`) and the volume levels on AUDIO (`OriginalAudio.cs`). Rows: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalAudio.cs
The AUDIO page, the shell's partial over the decoded `[@Audio@]` section, behind the Preferences page's second door. Four slider
rows over `Utils/AudioMix.cs`'s 0..100 in VIDEO's table shape: per row the authored title, slider and description widgets it stands
on, and how the store field is read and written, a never-set level reading as its shipped default. Each row's line is read off its
own title widget, the authored pitch being 58, 57, 53 and 53 rather than one number. Master takes the In-Game Music row with the
page's own words and its slider at the section's slider column, since a slider that reaches zero is that checkbox in one fewer
widget; Sound Quality is left out. A slider answers no Accept (`SliderControl.cs`); ACCEPT CHANGES leaves as the `OptionsApplyExit`
and CANCEL CHANGES drops the edits. `AudioPreviewMix` is the mix the open page stands at, null off it, and `TakeAudioMoved` the
level a frame moved, taken once: the host is what applies and sounds them. [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalVideo.cs
The VIDEO page, the shell's partial over the decoded `[@Video@]` section, behind the Preferences page's third
door. Same table as Game Options with one column more: each setting names the authored title, control and
description widgets it stands on, so a row keeps its geometry, and the table is in authored row order because
the cursor walks it. The monitor and Resolution keep the authored Graphics and Resolution rows, their words
enumerated per machine by `Utils/MonitorSetting.cs` and `Utils/ResolutionSetting.cs`; the Graphics row's title
and description are the page's own, the authored ones naming a 3D card this port has no answer to. Display Mode
and V-Sync are dropdowns on Viewing Range and Effects Level over `DisplayWords`, Enhanced Graphics takes the
Shadows checkbox whose gate it owns, and a list opens as `OriginalGameOptions.cs` does, so the Resolution row's sizes window and scroll where they outrun the eight rows that row authors, though under borderless, which owns the size, that row shows the screen's own size, draws dead and takes no press while the saved size waits untouched; ACCEPT CHANGES leaves as the `OptionsApplyExit`, CANCEL CHANGES drops the edits. [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalControls.cs
The two rebinding pages, the shell's partial over the decoded `[@ControlsPrefs@]` and `[@Keys@]`
sections behind the Preferences page's fourth door, all of it over the shared `ControlsFeature`.
The CONTROLS page carries the seat chooser on the Controller Type row and the KEYS AND BUTTONS
door; the KEYS page carries seven category tabs, one action list under a category heading in the
listbox's own window, and each row's controls in the two authored columns, the first in Control A
and the rest in Control B so nothing is hidden. A cell press arms a capture on that row's action
and slot, and the page swallows the frame while one runs. Each page's ACCEPT CHANGES commits and
its CANCEL CHANGES drops the staged edits. Rows and readings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalCredits.cs
The credits screen, the shell's partial over the decoded `[@Credits@]` section behind the top
level's fifth row. The section is three widgets: a full-screen background pane, ABOUT and the DONE
plaque. The credit names are painted into the background art, so the pane is the whole composition
and the two buttons are drawn over it by the shell's row loop. DONE and Escape both land on the top
level, the plaque's own `ScriptToExe` and what `CREDITS.SCRIPT`'s `gui_char` does, so `Back` needs
no arm here. ABOUT raises the messagebox in its `ma_` set, centred on its own background and carrying
langui 1301 over the product id; the script's hidden line shows while the pointer's secondary button
is held in its region. The screen: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalSeats.cs
The shell's two sortie screens, Free Flight and Dogfight, over the shared player setup, plus the
seat rules every screen shares. Rows: the chapter column and BACK, then the aircraft column over
the setup's roster (an eleven-row sliding window) and FLY. Seat 0 alone drives these screens; each
joined seat then picks on its own screen (`OriginalSeatPlane.cs`). FLY is enabled once the mode's
gate is met and leaves as the mode's own typed exit, which the walk's last confirm reaches for it.
`JoiningOpen` is the per-screen joining rule the presentation reads, and `CampaignSeatPanel` the
seat strip the campaign boards and the Instant Action screen take as an overlay once a second seat
has joined. Remake-only by design: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalSeatPlane.cs
The remake-only per-seat aircraft screen, a shell partial: once seat 0 has picked on a sortie
screen, or pressed FLY MISSION on Instant Action with a second pilot joined, each joined seat in
player order picks here. `SeatPlanePage` is an `ICampaignPage` over the sortie roster, so
`CampaignBoards.For` draws it in the plane-selection board's shape (its list field, WEAPON LOADOUT
over a selection, ACCEPT and CANCEL SELECTIONS), the seat strip over it. The picking seat's own
device drives it, and the mouse riding seat 0's source. Accept selects, a second Accept confirms,
Back and CANCEL SELECTIONS drop a selection then leave the walk with every seat kept and nothing
unjoined, and the last seat's confirm is the launch: [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/Original/OriginalInstantAction.cs
The Original Instant Action screen, the shell's partial over the decoded `[@InstantAction@]`
section and the shared `InstantActionFeature`. Its rows are the section's own widgets keyed by their
layout keys: the contents list in its authored window with its arrows and thumb, the dropdowns at
their authored boxes, the enemy rows on two pages under both paging buttons, the radio pair and the
buttons. A box no setting can fill stands blank with a pale arrow rather than leaving the page; an
open list is windowed by `OriginalDropList.cs`, bands its picked row and the row under the pointer, and a closed box redraws its outline
in cream under one. The Pilot Plane list is `OriginalRosters.Roster` (stock, then the saved builds, rows named `Stock <airframe>` and `<build name> <airframe>`), re-read on every entry and on the hangar's return; a picked build flies its airframe's stock node with its def on the seat.
Build opens the wallet-free hangar (`OriginalHangar.cs`), Weapon Loadout the loadout screen (`OriginalLoadout.cs`). Option sets: [../formats/instant-action.md](../formats/instant-action.md).

## src/UI/Menu/Original/OriginalLoadout.cs
The Weapon Loadout screen, the shell's partial over the decoded `[@OrdinanceLayout@]` section (the
campaign's ammo chrome) and one aeroplane's `LoadoutChoice`: the Instant Action strip's seat (seat
0's fit or the `InstantActionFeature`'s wingman fit, by the radio pair) or the per-seat picker's own
`PlayerSeat.Fit`, `_loadoutSeat` deciding which screen the exit returns to. It owns the mapping of
the section's four ammunition and eight rocket fields onto the airframe's gun slots and pylons over
the stock table's option lists, the snapshot CANCEL and Back restore, the airframe's diagram frames
and the description pane; rows and open lists reuse the Instant Action partial's dropdown machinery.
What the fit means at launch: `src/Flight/LoadoutChoice.cs`.

## src/UI/Menu/Original/OriginalHangar.cs
The Original hangar, the shell's partial over the shared `HangarFeature` and the decoded hangar
sections: the PLANE NAME screen, the Plane Construction hub with one of six tab sections on its
right page, the totals page and the INVENTORY, entered from Instant Action's Build Custom Plane or
the cabin, the door naming the airframe a default build opens on. It owns the plane picture over
the blueprint panes; the hub's figures, which `HubBill` prices on the row an open list has under the cursor so they preview it and take nothing, the cost line reddening on that bill's funds verdict and the weight line on its capacity verdict, bar a previewed airframe row, whose weight line is pending and plain; the cash note on both doors (the wallet's funds, else the export door's figure), every combo row staying bare over either;
the tab bar with the standing tab latched and its labels on the strips' own baseline; the tab pages' description box, which `HangarDescriptions` fills and whose prose flows as a note inside it; every list under its box bar the decal picker, the page's own five-across grid of tiles carrying its chrome inside its right edge; the two name boxes with their
caret, the airframe swap's own three-answer question as the shared messagebox, and the export door's own Export, Delete and delete confirm; `PaneOrigin` centres a small pane and places its rows on it. [../org/hangar.md](../org/hangar.md), [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalCampaign.cs
The Original campaign, the shell's partial over the shared `CampaignFeature`: the profile screen,
the cabin, the table of contents, the flight check, ammo and plane selection, the book, a scrap's
zoom and the briefing dialog. What each screen draws is the shared board component, so the shell
hosts the Built-in campaign pages in a `CampaignFlow` of its own and copies every composed layer
into its own board; that flow is never walked, its screen and cursor mirroring this file's. The
screen graph, the rows at the rectangles the board draws them at, the pointer hit-testing, the
cues and the dialogs are this file's, as are `OpenCabin` (every door onto the cabin, which is why RETURN TO CABIN is taken here rather than mirrored off a page), `ComposeDialog` (a standing box's answers, whose rollover frame is the pointer's alone, the cursor's own answer marked with an outline over the box instead), `ShowScrapbook`, where the feature's two cinemas play, and `CheckSeat`: the check and the two screens it opens stand for one player at a time, that seat's own device driving them while seat 0 keeps its pointer alone. Read `src/UI/CampaignFlow.cs` for the pages; the screens and
their strings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalPresentation.cs
The Original presentation node, registered under `PresentationId.Original`: a `CanvasLayer` on the board layer holding one
`ComposedBoardView`, so every screen scales as the campaign boards do. `Activate` builds the shell and the device
bookkeeping once, refreshes the roster from the saved-plane store on every call, stands the shell on the top level, maps the
return destination onto it and applies the `--menu=` aid on the first show. `Tick` keeps the pads in step (seat 0's claim
while joining is closed, the join scan while the shell opens it), polls every seat, maps a window-pixel pointer into the
authored space, steps the shell, requests its cues, states the AUDIO page's mix while that page is open and ends the
preview on every door out and on `Hide`, drives the briefing's reveal, and runs the board's movies on the step the host was given,
`DebugPointer` standing in for seat 0's pointer when the screenshot aid asks. The shell's art sizes come from `OriginalArtSizes`; `PaletteFor` is the inks.

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
caller to log once. `ArtPath` and `RelativeArtPath` are where a layout art name resolves, the
presentation's size read going through the first; `IsMovie` puts the movies one directory deeper,
under `MPG`, where the executable resolves them. Coverage: `CSVM.Tests/OriginalManifestTests.cs`.

## src/UI/Menu/Original/OriginalAssetManifest.cs
The versioned required/optional asset manifest, derived from the decoded layout rather than
hand-listed. `Derive` classes the art of the sections Original composes required, less two short
tables (rows it does not draw, and rows it draws whose file the screen survives the absence of,
which is where the background movies sit), everything else optional, and the five files the scripts
name and Original draws anyway required. `Check` reads no bitmap: existence plus the PNG signature
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
`src/Session/MenuCueTable.cs`.

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
Where the menu stands when it comes back, said semantically: `TopLevel`, `InstantAction`, `CabinReturn(profile)` and `DebriefReturn(profile, missionSeq)`. The host names the destination and the
active presentation maps it into its own graph at `Activate`, so no presentation-specific screen id crosses the seam. `ForLaunch(exit)` reads off a launch's own exit the screen it came from, which is
where a flight left early lands; the exit and not the session's spec, since a spec inherits the command line's `--campaign=` and would call a Free Flight launched afterwards a campaign mission. A
destination names where the player stands and never a store: the two campaign returns name a profile, the store it is re-read from is the presentation's own, and an Instant Action return names
nothing, the sortie's setup being the feature's. The `--menu=` aid is not a destination either, reaching the cold start alone, so a return is always one of these four. The namespace seam this whole
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
The rebinding screen as a shared `IMenuFeature`, engine-free: which seat's keymap is being edited
(one registered `BindingProfile` per seat), which of the three contexts, the row and slot cursors,
the capture in progress over the seat's own `IDeviceState`, and the steal it is about to perform.
A capture that lands on a free control binds it; one that lands on a held control raises `Pending`
naming every action that would lose it and moves nothing until `ConfirmSteal`, which keeps the
original's conflict rule from happening behind the player's back. `UnbindSlot`, `ResetContext` and
`Save` are the rest. Editing is scoped to one seat's profile, so two seats cannot reach each
other's bindings. The binding model itself: [../org/input.md](../org/input.md).

## src/UI/Menu/PlayerSetupFeature.cs
Player setup as a shared `IMenuFeature`, device-neutral and engine-free. `Seats` are `PlayerSeat`s
in join order, each bound to the `IMenuInputSource` that claimed it: a claim is one source and one
seat, settled in arrival order, and seat 0 never leaves. `Roster` is the `MenuAircraft` list every
seat picks from, set by the presentation and built by the shared rule (the stock rows in their given
order, then one row per saved custom flying its airframe's stock node, a campaign plane nobody has
exported left out). Per seat it owns the cursor, the two stages of the pick, the loadout door and
the backing-out ladder; the gate is the mode's minimum of seats and every seat confirmed. `Choices`
and `BuildExit` are the typed result. Nothing here reads a pad: `src/UI/MenuSeatDevices.cs`, below.

## src/UI/MenuSeatDevices.cs
The pad side of the shared player setup, for any presentation, over seat 0's `MenuInput` and the
feature. `P1Pad` is the pad seat 0 claimed by steering a screen with it. `Sync` reconciles the
seats with the connected pads: a seat whose pad vanished is unjoined, a vanished claimed pad frees
seat 0, and seat 0's poller is bound to its claimed pad or to every unclaimed one. `PrimeJoins`
and `ScanJoins` are the join gesture, Start on an unclaimed pad while a seat is free, the caller
deciding on which screens joining is open. `PadOf` reads a joined seat's pad back off its
`BuiltInSeat`, and `FlightPads` is the binding a launch carries, the answer both presentations
hand the feature's `Choices`. Read `src/UI/Menu/PlayerSetupFeature.cs` for the seats themselves.

## src/UI/MenuControlsSeats.cs
The rebinding screen's seat bookkeeping, for any presentation. `Sync` takes this frame's pollers,
one per joined seat in player order, and puts the shared `ControlsFeature`'s player rows in step
with them: a registration is kept while the seat behind its number is the same poller, a number
that changed hands is registered again, and a seat with nothing to press gets no row. `PadOf` is
the identity a context's rows sit on, the one function the capture reader and the captured control
both take, so neither can name a pad the other does not. The profile a seat is staged from is the
menu poller's own live map plus the saved flight and camera maps, so an accepted rebind is felt at
once. Read `src/UI/Menu/ControlsFeature.cs` for the editing itself.

## src/UI/Menu/InstantActionFeature.cs
Instant Action as a shared `IMenuFeature`, owned by the host's feature set and configured by both
presentations. The option sets are static and decoded: the environments, the mission types with
the bans a chapter and stunt flying impose, the eleven airframes, the militias with the aircraft
each allows, the skills and the preset table. The setup is typed state with semantic operations:
select and confirm an environment (which re-fits the mission type and loads the chapter's own base
def), the mission type, the lives, the four waves, the wingmen and both plane picks, and apply a
preset. `Refusal`/`CanLaunch`, `BuildDef` and `BuildExit` are the gate and the launch, `Discard`
resets every field, and the decode is [../formats/instant-action.md](../formats/instant-action.md).

## src/UI/MovieSurface.cs
A movie as something a composition can draw: a `CSVM.Video.MoviePlayback` and the `ImageTexture`
its pixels are uploaded to, made once and updated in place. There is no node, so a caller hangs
the texture where its own layout row puts it and this surface never learns which screen that is.
`Open` answers null for a file that cannot be read or is not a movie, because a screen missing its
background still has everything else on it. `Advance` says whether the picture changed, so a caller
repaints on the frames that need it and no others. Every timing decision belongs to the playback,
which holds no engine type, so this half is the upload alone. Read `src/Video/MoviePlayback.cs`
next.
