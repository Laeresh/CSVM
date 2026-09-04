# UI

The launchscreen and splitscreen rig, plus the interactive debug labs (including its `UI/Menu/` subfolder). Every lab has a scripted `--debug-*` twin so a finding can be reproduced headlessly; see `docs/cli.md`.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/UI/LaunchMenu.cs
The in-game launchscreen CanvasLayer: Mode branches two ways. Free
Flight/Dogfight go Mode → Chapter → Plane, unchanged. Instant Action (the Mode row that used to
read Stunt Flying — decision 17) instead opens its own five-step wizard: Mode → Environment →
MissionType → Waves → Wingmen → Plane, shared with the other two modes as the final step.
Dogfighting an Ace skips Waves/Wingmen entirely, both forward (`HandleAccept`'s MissionType case)
and on the way back out of Plane (its own Back handler) — the decoded setup screen's own behaviour
(A1: mission type 0 hides every enemy control). Input polled every frame through one MenuInput per
player (no input-map/focus wiring); joining is gated to the Plane screen, and with >1 player that
screen becomes real SplitScreen.PaneRect panes — pick in the pane you fly in. Three Mode rows —
Free Flight/Instant Action/Dogfight — map 1:1 onto `SessionSpec.MenuMode`'s ordinals (the enum
member stays named `Stunt`, out of this item's file-contention scope — only the row's label
changed); `Launch` now carries a fourth value, the wizard's own built `InstantActionDef?` (null
outside Instant Action) alongside the chapter/choices/mode it always carried — H16's own build
path, replacing H15's interim "map the picked mission type onto whichever of Free/Stunt's
behaviour it most resembles" (that mapping is gone; `FireLaunch` always passes `_mode` straight
through now, and `SessionSpec.FromMenu`'s `iaDef` parameter is what actually decides
Scenario/Stunt, precisely, off the wizard's own pick).
The wizard's fields are the shared `InstantActionFeature`'s (`src/UI/Menu/InstantActionFeature.cs`,
read out of the host as `_ia`): the environment, the mission type, the lives, the four waves, the
wingman count, airframe and fit, the applied preset and the confirmed environment's base def, plus
every option set (environments, mission types, airframes, militias, skills, presets). This screen
keeps only what a cursor wizard adds over a dropdown page: the Table of Contents cursor and its
14-row window (`PresetWindow`, `_presetTop`, `ScrollPresetsToCursor`), the Waves list cursor, the
edited wave and its field cursor, the Wingmen field cursor and the wingman fit row. The Environment
and Mission cursors are the feature's picks themselves (`CurrentIndex` reads
`_ia.EnvironmentIndex`/`_ia.MissionTypeIndex`), since a dropdown has no cursor apart from its
value. `Planes`, the plane picker's roster, is built off the feature's airframes so the two cannot
drift, and the public read-outs (`EnvironmentCodes`, `EnvironmentNames`, `MissionTypeKeysFor`,
`PlaneNames`, `MilitiaNames`, `AircraftFor`, `SkillKeys`, `WaveFor`) delegate to it. The screens
call the feature's operations where they used to write fields: `MenuInput.MoveX` on MissionType is
`StepLives`, on WaveEdit `StepWaveCount`/`StepWaveMilitia`/`StepWaveAircraft`/`StepWaveSkill` on the
edited wave, on Wingmen `StepWingmen`/`StepWingmanPlane`; Environment's Accept is
`ConfirmEnvironment` (the mission cursor re-fitted onto the environment's roster, the environment's
own `ia.zrd.json` loaded as the base); the ace skip forward and back reads `IsAceDuel`; `FireLaunch`
leaves as `_ia.BuildExit(seats, NominalPlaneName(player 1's pick))`, the nominal name because a
custom pick speaks its airframe's stock vocabulary. `DebugWaves(N)`/`DebugWingmen(N)`/`DebugPreset(N)`
(--debug-waves=/--debug-wingmen=/--debug-preset=) are `DebugJoin`'s own screenshot-aid pattern
writing through the same operations.
`Screen.Presets` is the Table of Contents (`BL-352`), reached from step 1 by `MenuInput.Presets`
(P / X) and nowhere else — the original picks a preset with a mouse on a list sharing its page with
the dropdowns, so both the button and "opt in from step 1 rather than open on it" are stated
divergences, not oversights. Accept calls `ApplyPreset` (the feature's, then player 1's plane
cursor mirrored from the feature's player airframe: the original has one pilot and one aircraft
dropdown, so players 2 to 4 are untouched) and returns to `Screen.Environment`, which is the
original's own page order: the contents list is page 1, and View Story opens page 2, the
configuration screen under the preset's name. `PresetCrumb` is that heading, carried through every
Instant Action breadcrumb while the feature's `PresetIndex` is not −1; nothing clears it when a
field is then changed by hand, matching `IDS_IA_STORYTITLE`'s one-time format. The list is the
file's only scrolling one, `PresetWindow` being `LAYOUT.CSV`'s decoded 14 visible rows onto 19
items; `Rebuild` draws that slice while `Row` keeps taking the absolute index.
The campaign's screens are the third layout, drawn through `ComposedBoardView` rather than rebuilt
as controls, and the shell owns the two things a page cannot: the briefing's narration player and
its reveal clock (`TickCampaignAudio`). A running reveal repaints the board every frame, because a
reveal is an animation and no property watch describes one; `campaign-briefing-repaint` measures it.
`_UnhandledInput` also takes the screenshot key (`F12`) for both layouts and marks it handled, so a
press on any menu screen writes one file through `CaptureDirector.SaveScreenshot`, into the same
`Screenshots/` folder a flight shot lands in. The menu owns that binding rather than borrowing the
launcher's, because a board is what a menu defect has to be reported from (`BL-489`);
`menu-screenshot-key` pushes a real key event through the real viewport on both layouts and fails if
the menu leaves the key to whatever is above it.

The centred layout is three bands, divided by `MenuZones`: a header (`CRIMSON SKIES`, the
breadcrumb, the join strip), the middle (heading, the hangar's totals line, the rows with the art
column beside them, two status slots), and a footer (the focused row's description, then the
controls line). Header and footer are held at the heights their own metrics ask for and the middle
takes the rest, so the rows are the only thing a screen can move. Every slot outside the rows is
drawn whether or not it has anything in it, and a refusal takes the description's own slot rather
than a line of its own, which is what keeps a longer description or an appearing status line from
shifting the screen under the cursor. `Zones` measures the three reference heights off the theme
font at the 720p metrics and `MenuZones.For` turns them into one shared scale; a band's own
separation scales with it, or the height it spends is height the metrics never budgeted. `_Process`
watches the viewport size as well as the input, since a resize or a resolution change arrives as no
press at all. `menu-zone-layout` walks the paint screen row by row and then resizes a viewport under
a real menu; `CSVM.Tests/MenuZonesTests.cs` holds the division rule itself.

The launchscreen is the Built-in presentation's screen graph and stands on the menu host
(`src/UI/Menu/MenuHost.cs`), which `Build(zrdrPath, dataRoot, host, player1)` takes: it reads the
`FreeFlightFeature` and the `PlayerSetupFeature` out of the host's feature set, player 1's
commands out of the host's first seat, plays narration through the host's audio service and
leaves only through `IMenuHost.Exit`. It has no callbacks. `player1` is the raw `MenuInput`
behind that first seat, handed over separately because the pad bookkeeping (claiming on
Mode/Chapter, joining, hotplug) binds devices through `MenuSeatDevices` over it: the seat carries
commands, the poller carries the binding, and both are the same object. The seats themselves are
the setup feature's; `_slots` is this screen's view of them (`SyncSlots`, keyed by the feature's
`Revision`), one wrapper per seat holding the poller behind it (seat 0's `player1`, a pad seat's
own, an idle one for a device-less seat) and its last `MenuCommands` frame. `_Process` is the
frame: `MenuSeatDevices.Sync`, the join scan (open on the Plane and Campaign screens only, closed
by the campaign field's lock), `SyncSlots`, then `host.Seats[0].Poll` applied onto player 1's
poller and slot (`Apply`), every other seat's `Source.Poll` into its slot's frame, `HandleInput`,
the campaign audio tick and the repaint. The Built-in presentation switches the node's own
process callback off and calls `_Process` from its `Tick`, which is also how the frame-driving
suites run it. `Drive(MenuCommands)` applies one frame of player 1's semantic commands with every
other seat idle and handles it, for the scripted journey suites; a suite drives a later seat by
joining a scripted source through the feature and running `_Process`.

`FreeFlightFeature` (`src/UI/Menu/FreeFlightFeature.cs`) owns the chapter roster it offers, the
picked chapter, the launch gate and the typed `LaunchExit`. The Chapter screen's Accept under Free
Flight hands it the cursor's code; `--menu=plane`, `selected` and `loadout` skip that Accept, so
`ShowMenu` hands it over for them. The Plane screen's every stage moves through the setup
feature: `Browse` on a cursor step (player 1's wraps over the roster plus the hangar door row,
which the feature lets a cursor park on and refuses to select), `Select` then `Confirm` on Accept,
`Back` a stage at a time (browsing, player 1's Back leaves for the feeding screen with
`ResetPicks(fits: false)`; a guest's unjoins), `OpenLoadout`/`CloseLoadout` for a pane's Ammo
Selection list; `ShowMenu` is `ResetPicks(fits: true)` plus `Select` (and `OpenLoadout`) for the
`selected`/`loadout` aids; `DebugJoin` joins `MenuIdleSource`s. `CanLaunch()` asks the Free
Flight gate in Free mode (its chapter plus the setup's seat count and confirmations) and the
setup's `CanLaunch(mode)` otherwise, which is the static `CanLaunch(mode, allLocked, joined)`
rule kept for the tests. `FireLaunch` takes the setup's `Choices(MenuSeatDevices.FlightPads)`,
one `MenuSeatChoice` per seat (the roster row's node, the pads the seat's source is bound to, its
fit edits or null for stock, and a custom row's def as the roster was read), and hands the host one
`LaunchExit`: the Free Flight feature's own in Free mode, the Instant Action feature's in Instant
Action (`_ia.BuildExit(seats, nominalPlane)`, player 1's nominal stock name riding along as the
def's label), and one built here with the chapter for Dogfight, until that mode has a feature of
its own. `RefreshRoster` sets the setup's roster (`PlayerSetupFeature.BuildRoster` over `Planes`
and the store's customs) beside this screen's own `PickerPlane` list, so a saved plane appears in
both. The cabin's FLY MISSION (`FlyCampaignMission`) leaves the same way as
a `CampaignMissionExit` (profile, story position, one seat choice per joined human with its stock
node, joined pads, stored fit and hangar build). Back on the Mode screen leaves as a `QuitExit`.
The Mode screen's last row is the Options door (`OptionsRow`, `--menu=options`): a four-row screen
whose first row steps the menu presentation between Built-in and Original (Left/Right or Accept),
whose second steps the graphics mode between Original and Enhanced the same way, whose third is the
Controls door, and whose fourth, "Apply and restart the menu", leaves as an `OptionsApplyExit`
carrying the first two; the launcher persists them and restarts the menu.
The Controls screen (`ControlsRow`, `--menu=controls`) is the rebinding screen over the shared
`ControlsFeature`: a windowed two-column list of one seat's actions and the controls on each, under
a seat stepper and a context stepper. Accept starts a capture, and the capture reads the seat's own
`MenuInput.Devices` rather than its semantic commands, because a captured control has to carry the
seat's pad identity and a `MenuCommands` frame hides the device on purpose; a capture in progress
swallows the frame, so the press being bound cannot also walk the cursor. Left/Right pick which of
an action's controls the next capture replaces, past the last one being the empty slot that adds;
L/Y unbinds it and P/X restores the seat's shipped keymap. A capture landing on a held control asks
before it takes it. Back writes the seat's keymap through the store and returns to Options. The steppers open on the saved options, read from `OptionsStore`, so
each shows back what was asked for rather than what is active. The graphics row's description says
it takes effect on the next start, since `GraphicsMode` resolves once at launch.
This door is the one Built-in change the presentation work makes; every other Built-in screen,
control, payload, aid and return keeps its behaviour.
The Chapter screen and the aircraft screen's drawing (the split panes, the join strip with each
seat's `MenuInput.DeviceLabel`, the lock colours, `JoinHint`'s texts, the same-frame order of the
per-seat loop) stay here; the seats, their stages and the gate are the setup feature's. `Chapters`
is this screen's row text zipped over `MenuChapters`, the shared roster. The `Shown*` read-outs (`ShownScreen`, `ShownHeading`,
`ShownBreadcrumb`, `ShownFooter`, `ShownDetail`, `ShownJoinHint`, `ShownRow`, `ShownRowCount`,
`ShownRowText`) say what the screen draws. `menu-free-flight-journey`
(`src/Testing/MenuJourneySuites.cs`) drives the real menu through them from Mode to the typed
exit, back out at every step, through a return and the `--menu=` aids, and pins Built-in's
present behaviour with its quirks: the chapter, airframe and mode cursors survive Back and a return
from flight while the selection does not, a bare launch's return lands on Mode, an aid re-enters
under whatever mode was last picked, a selected airframe's cursor does not move, and the launch
fires only when every joined seat has confirmed. `menu-player-setup-journey`
(`src/Testing/MenuPlayerSetupSuites.cs`) pins the seats the same way: one to four seats through
`DebugJoin`, the two-stage pick with Back at every stage, the split heading, player 1's Back
unselecting everyone, the Dogfight gate with its hint, the four-seat maximum, the aids under two
seats and a return keeping the seats; `menu-player-setup-seats` (same file) joins scripted
sources through the feature and drives them through `_Process` in Built-in and through the host's
`Tick` in Original. `menu-host-tracer` (`src/Testing/MenuHostSuites.cs`)
runs the same journey through a real `MenuHost` with the Built-in presentation registered: frames
through the host's seat, the exit through the host's sink with the presentation hidden, and a
top-level re-show that re-enters with the state the journey suite pins. Suites that stand a bare
launchscreen build it through `MenuSuiteHost` (`src/Testing/MenuSuiteHost.cs`).

The briefing's narration is no longer a node here: `TickCampaignAudio` asks the host's audio
service to begin the narration whenever the page's `NarrationStarts` moves and to end it the
moment the briefing stops showing; the service owns the player and the music duck.

## src/UI/MenuZones.cs
How the launchscreen's three bands divide a window: the two fixed heights, the middle taking the
slack, and the one scale all three share, capped so the tallest screen still fits instead of losing
its footer. Pure and public, so the rule is testable with no menu instance behind it
(`CSVM.Tests/MenuZonesTests.cs`). The property the layout rests on is that growing the middle's
reference height leaves the other two bands where they were, as long as the content still fits.

## src/UI/Menu/InstantActionPresets.cs
The original's Table of Contents, in the shared menu namespace beside the feature that applies it:
the 19 preset scenarios decoded from 19 `0x230`-byte records at `0x0061b090` and applied by
`FUN_004102c0` (docs/formats/instant-action.md, "Table of Contents presets"). The table is
transcribed by NAME, not by the record's own dropdown indices, so it diffs line-for-line against
the decode and a roster reordering cannot silently invalidate it; `Resolve` does the name-to-index
step against `InstantActionFeature`'s rosters, which are every presentation's single source of
order. Three things it does beyond copying fields. The mission-type cursor indexes the FILTERED
roster for the preset's environment, so a zeppelin run on "the clouds" is row 2, not row 3. Unused
wave slots take `FUN_004102c0`'s own sentinel substitution (Fortune Hunter / Devastator / veteran
at 0 enemies) rather than a zeroed default: it never reaches a flown mission, since `FUN_004175f0`
skips any wave at 0 enemies, but it is what a pilot inherits on raising an empty wave's count. And
a wingman aircraft is reported only where there are wingmen to fly it, `null` otherwise, because
the decode reports no value for the five presets that fly alone. An unresolvable name throws, the
same fail-loud policy `InstantActionFeature.AircraftFor` applies to wizard-only data. Presets fly
stock airframes, so nothing here waits on the hangar. Units in
`CSVM.Tests/InstantActionPresetsTests.cs`.

The hangar (`HangarFlow`, built over the host's one `HangarFeature`, so a presentation switch drops
the same scratch plane Original would have been editing) has two doors, both through `OpenHangar`,
which remembers the screen to land back on: a trailing `Build Custom Plane` row past the three
Mode rows, and the same row past the eleven airframes on the Instant Action plane pick. `Screen.Hangar`
draws through the same three bands every other centred screen uses: heading, rows, description and
controls all read off `_hangar.Page`. The rows live in a content column of their own, so
that when the page's `Art` is non-null an art column (`HangarArtColumn`) stands to its LEFT, the
side the original's own paint screen puts its preview on: the page's decoded RGBA as a
letterboxed texture with a caption, and under it the focused row's own smaller picture when
`RowArt` returns one (the paint screen's decal tile). Each texture is rebuilt only when the page
hands over a different image, and the middle band's metrics count the column only where it is taller
than the rows it stands beside, at its full height whether or not the focused row has a picture of
its own. The description label autowraps in the footer band's 560px column, which is what
keeps the airframe-defaults ask (langui 206, a two-sentence question) on a 16:9 screen instead of
stretching that column past its edges. Every hangar screen also carries the persistent totals line under its heading
(`HangarFlow.TotalsLine`), error-coloured via `TotalsOverweight`, in a slot the middle band reserves
on every screen. A page needs no change here, art included. The hangar's
screens sit behind a flow rather than behind the screen enum, so `--menu=` reaches them through
`OpenHangarAid`: `hangar` the plane list, `airframe` the airframe list, `defaults` its ask, `paint` the
preview on a Fury in Fortune Hunters colours with a nose decal chosen. A screenshot aid only.
Every human plane picker (the lone-pilot centred Plane screen and every splitscreen pane) draws
one roster, `_roster`: the eleven stock airframes then the store's saved customs
(`PlanePickerRoster.Build`), re-read by `ShowMenu` and by `CloseHangar` so a new save appears
without a menu restart. A completed build lands in `LastBuiltPlane` and `CloseHangar` puts player
1's cursor on the new plane by name (the original's index-11 after-build select). A custom pick
survives the menu layer as `PlayerChoice.CustomPlane`; its `PlaneNode` is the airframe's stock
node, and `Launcher.StartSessionFromMenu` reads the name back into a def through
`CustomPlaneStore` and carries it on `SessionSpec.MenuCustomPlanes`, one entry per pane, for
`HumanFlightAdapter.Assemble` to build the aircraft from (`CustomPlaneBuild`). A plane whose file
went away between the listing and the launch warns and flies the stock airframe rather than
refusing the session. A def carrying an exported loadout (`CustomPlaneDef.HasLoadout`) also supplies
that pane's fit through `CampaignLoadout`, so an exported campaign plane flies in Instant Action and
multiplayer with what the campaign fitted it with; a fit the player set on the loadout screen is
this sortie's explicit pick and stands instead. Wingmen stay stock-only (`Planes`), and the scripted paths
(`--plane=`, `--det`) never see the roster: they name planes by node in `SessionSpec` directly.
⚠ The plane pick's hangar row is offered only to a lone pilot under Instant Action
(`HangarRowOnPlaneScreen`): it trails the customs, a splitscreen pane never draws it, and
`RebuildPanes` clamps every cursor back into the roster, so `PlaneIndex` can never point past the
roster anywhere a plane is actually read. The row is a door, not an aircraft, so it cannot be
locked or confirmed and no launch path sees it. Outside `OpenHangarAid`'s scripted-screenshot
values, the hangar is reached only through interactive menu input — no `SessionSpec` field,
nothing a `--det` run can touch.

The campaign (`CampaignFlow`) has one door, the `Campaign` row between the three modes and the
hangar's, and `Screen.Campaign` draws as a composed board (`ComposedBoardView`/`CampaignBoards`,
below) rather than through the three bands every other screen uses: heading, rows, description and
controls all read off `_campaign.Page`, and the footer is the page's own, since a screen with an armed
text field has a different control set from the same screen with the cursor on its list. The art
column is shared with the hangar through `PageArt`/`PageRowArt`, so a campaign page that hands over
a picture needs no change here. Navigation is player 1's everywhere except a guest's own flight
check, which is that guest's screen to fill in: `CampaignDriver` picks the slot
`CampaignFlow.Field.Current` names, falling back to player 1 for a driver with no device, or
`--debug-join=`'s deviceless players could not walk the sequence at all. The driver's `Move`/`MoveX`
read normally, and while `CampaignFlow.CapturesText` is true they read `PadMove`/`PadMoveX` instead
and feed `Typed`/`Erase` into the field, because W, A, S and D are letters there. C21:
joining is not player 1's alone either — `ScanJoins` runs on `Screen.Campaign` the same as on
`Screen.Plane`, so Start on an unclaimed pad joins a guest from any campaign screen up to and
including the seated player's FLY MISSION, and every joined slot past player 1 reads its own `Back`
as a leave — `HandleCampaignInput`'s own guest loop, the same "everyone else can only drop out" rule
`HandleInput` applies elsewhere. ⚠ C22 is what closes both: FLY MISSION now opens the first guest's
check instead of leaving the screen, so `Field.Locked` is the explicit lock joining and leaving both
answer to, and while the walk runs the only `Back` on screen is the walk back through the checks. More than one
joined draws `LaunchMenu._chipStrip`, a `P1 P2 P3 P4` shell overlay in `SplitScreen.PlayerColor`
pinned to the window's top-right corner (PerfHud's `PlaceTopRight` pattern) and scaled through the
same `BoardFit` the board itself draws at — a shell overlay rather than a page contribution, because
`CampaignBoards`' authored per-screen geometry has nowhere to put a live, per-frame roster and every
page would otherwise need the same field. A solo campaign draws no strip, matching every other
composed board's "no full join strip" rule (`RebuildBoard`'s own comment). The flow's `Message`
rides the same error line the hangar's gate uses. `--menu=campaign` (`CampaignAidProfiles.PlayerDoor`,
the player's door) opens the real `user://Profiles`
roster; every other `campaign-*` value is a screenshot aid over a scratch profile directory
(`CampaignAidProfiles`, shared with the Original presentation's aids of the same names), so
those shots are the same on every machine and cannot write into a real campaign. The one exception
is `campaign-fly`, which launches a mission and therefore has to use the real store, because the
session's own director reads that one. `campaign-guestcheck[:player]` opens a guest's own page of
C22's walk; it reads its argument as a player number rather than a cursor step count, and is applied
inside `DebugJoin`, since the aid runs during `ShowMenu` and the guests it needs arrive right after.

The flow's three exits are the shell's three jobs. `Cancelled` returns to the Mode screen and
discards the feature. `OpenHangar` opens `HangarFlow` over the feature's `CampaignWallet`
(`CampaignFeature.Wallet()`, the seated profile as `IHangarWallet`) and leaves the campaign flow
standing; `CloseHangar` calls `Resume` on it, built or cancelled alike, so a purchase or a sale
shows on the cabin the moment the hangar closes. `FlyMission` hands the feature one pad list per
joined slot and leaves through the `CampaignMissionExit` `CampaignFeature.BuildExit` returns, which
saves the profile first; with guests joined the flight check only asks for it on the LAST player's
press, every earlier one advancing the walk instead: the profile, the story position, and one seat
per joined human, its stock node, its hangar build where it has one, and the fit `CampaignLoadout`
derives from its picks, seat 0 the seated pilot's own and every seat after it a guest's
`CampaignFlightField.Plane`. The wingman is deliberately not in it, since `CampaignDirector`
resolves that binding from the same profile it opens anyway. Every door onto the campaign
(`OpenCampaign`, `OpenCampaignCabin`, `OpenCampaignScrapbook`, the aids) opens the host's one
feature on its store through `NewCampaignFlow`; `CampaignProfiles` is the store the door and the two
flight returns open, `user://Profiles` unless a driven suite hands in a scratch store first.

Two things the campaign pages cannot own live here, because a page holds no Godot node and has no
frame to advance on: the briefing's reveal clock (`page.Advance(delta)` once per frame while the
briefing is the screen showing) and its narration, an `AudioStreamPlayer` of this board's own on
the Master bus, restarted whenever the page's `NarrationStarts` moves and stopped the moment the
briefing stops showing. The board redraws on a reveal only when it uncovered a row or changed its
map, so a reveal waiting on a marker costs one comparison a frame. The score is the host's, entered
on every menu and cabin entry and stopped where a launch leaves the boards.

## src/UI/PlanePickerRoster.cs
The picker roster rule behind `LaunchMenu._roster`, engine-free so it tests without a menu
instance. `Build(stock, customs)` lists the given stock rows first in their given order, then one
`PickerPlane` per saved `CustomPlaneDef` in the store's own name-sorted order (the original's
11+customs list sizing, `docs/org/hangar.md` count callback 1024). A custom row carries its store
name in `CustomName` (the identity every consumer distinguishes stock from custom by) and its
airframe's STOCK node in `Node`; `AirframeNode(id)` is the id 0-10 to `player_*` node table in
the stat table's row order, the Hoplite resolving to `player_autogyro` (the shipped data's
two-names aircraft). `AirframeOf(node)` is the inverse, case-insensitive, null for a node none of
the eleven names: `CampaignDirector`'s kill credit uses it to turn a roster spawn's own
`RosterSpawnPlan.PlaneNode` back into the debrief's airframe index. `IndexOf(roster, name)` is the
after-build auto-select's case-blind lookup; -1 when absent. Deliberately NOT `Session.PlaneRoster`
(which answers "which plane does player N fly" off a `SessionSpec`): this type is the menu-side
list, that one the session-side read. Tests: `CSVM.Tests/PlanePickerRosterTests.cs`.

## src/UI/HangarFlow.cs
The Build Custom Plane flow, Built-in's walk of the shared `HangarFeature`
(`src/UI/Menu/HangarFeature.cs`), engine-free the way `BoardMenu` is: the launchscreen owns every
Godot control, the feature owns the scratch plane, the rules and the store operations, and this
file owns the order, the cursor and the pages. `HangarFlow.Order` is the original's nine screens
(plane selection, airframe, engine, armour, guns, hardpoints, paint, name, purchase;
`docs/org/hangar.md`), walked over the feature's scratch `CustomPlaneDef`. Nothing is written until
`Commit()`, which is what makes cancelling from any screen residue-free by construction rather than
by an undo path: `Back()` off the first screen sets `Exit = Cancelled` and discards the feature's
build. `Commit()` is the feature's gate (`Refusal`: a name, langui 203, then `HangarEconomy.Price`'s
verdict in the original's own words, 1182 + 1227 OVERWEIGHT or 1182 + 1171 No Engine Selected, then
over a wallet availability and funds), then `CustomPlaneStore.Save`; funds are never checked on a
wallet-free door. `LaunchMenu` builds every flow over the host's one feature
(`HangarFlow(feature, store, dataRoot, campaign, rng)`); the older constructor over a private
feature serves the unit tests. Every property a page reads (`Scratch`, `Saved`, `Strings`,
`DefaultsAsk`, `EditingName`, `AirframeChosen`, `Message`, `BuiltPlaneName`, the name helpers)
forwards to the feature; `Campaign` stays the `CampaignWallet` the flow was opened with, which the
feature sees as its `IHangarWallet`.
Editing a saved plane starts from a copy made through the store's own canonical serialisation, so
abandoning an edit cannot touch what is on disk. `DeleteSaved(name)` is the plane-selection
screen's Sell Plane (`ps_b_sellp`) in a build with no economy: it removes the file, re-reads
`Saved` and clamps the cursor back into the shortened list. `HangarPlaneSelectionPage` reaches it
through a two-stage gesture rather than a stepper, since a stepper on a destructive action is one
stray nudge from losing a build: a trailing `Delete a saved plane` row (offered only while
anything is saved) opens a second list whose every row reads `Delete <name>`, plus Cancel, so the
press that removes a plane names the plane it removes. The list closes when it empties. The
launchscreen's own `RefreshRoster`, which `CloseHangar` runs on every exit, is what keeps a
picker cursor inside the shortened roster afterwards. `LoadStockWeapons` is the airframe-defaults
arm's gun and hardpoint reading, public and static because the campaign's EXPORT of a plane with no
build needs the same one; a second copy of it would let the two disagree about what an airframe
carries at rest.

**Ownership, not storage, is what separates the campaign from Instant Action.** Both doors write
into the one `user://Planes/` build store; over a campaign flow `ReadRoster` puts
`CampaignWallet.OwnedBuilds()` in `Saved` instead of the whole directory, resolving each
ownership record to its stored build, else a reward aircraft's own award template, else the
campaign's starting-Devastator spec (the two seeded starters are never hangar-built). The screen
then reads as the original's INVENTORY: a Buy row over the wallet, one row per owned plane with its
value, and a trailing sale that credits the full build cost through `DeleteSaved`. An owned row is
inert, because the decoded economy has no partial upgrade. `IsNameTaken` still spans the WHOLE
directory rather than the visible roster, so a campaign build cannot silently overwrite an Instant
Action plane of the same name; `HangarNamePage`'s roller and overwrite warning both ask through it.

`IHangarPage` is the mount point Wave C's remaining items fill: `Title`, `RowCount`, `RowText`,
`Detail`, `Step` (the launchscreen's live ←→ stepper), `Accept` (returning false hands the press
back to the flow, which advances), `OpeningRow` (the row the cursor lands on when the flow arrives,
0 for most screens and the current pick on the two pick screens), `TotalsPlane(row)` (which plane
the shell's totals row prices while that row is focused, the scratch plane by default and null to
hide the row), `Art`, an optional decoded `TgaImage` plus caption the shell
renders (null by default via `HangarPage`), and
`RowArt(row)`, the same thing again for the focused row alone (only the paint screen's decal rows
have one). All plain
text, plain indices and raw pixels, so a page is engine-free and testable and the shell needs no
change to draw one. `HangarFlow.DataRoot` (optional, null in tests) is where a page resolves its
TGAs; absence reads as no art. `HangarPage` is the base carrying the flow, the scratch plane and
the heading resolved from the screen's own langui id (1017/1004-1010/1401);
`HangarFlow.PageFor`'s switch is the single line each of C22-C26 replaces; `HangarPlaceholderPage`
(the right heading, a Continue row and a real summary of what the scratch plane carries, editing
nothing) stands only in the switch's default arm now that every screen has its own page.
`HangarPlaneSelectionPage`, `HangarAirframePage`, `HangarEnginePage`, `HangarArmourPage`,
`HangarGunsPage`, `HangarHardpointsPage`, `HangarPaintPage`, `HangarNamePage` and
`HangarPurchasePage` (each its own file) are all real; the placeholder stands only in the
switch's default arm. `HangarFlow.TotalsLine` (with its `TotalsOverweight` colour flag) is the
persistent second stats line the launchscreen draws under every hangar screen's heading
: the build's total price and weight against the airframe's capacity,
recomputed from `HangarEconomy.Price` on demand and carrying the original's OVERWEIGHT word
(langui 1227) when over. Which plane it prices is the page's answer, through `TotalsPlane`: the
plane-selection screen prices the saved plane under the cursor and hands back null on its action
rows (New Plane, the delete stage, Cancel), where the line is "" and the shell draws nothing.

The airframe-defaults ask (string 206) lives on the flow: `DefaultsAsk` names the airframe whose
defaults are on offer and `DefaultsAskText` carries the formatted question (%1 the new airframe,
%2 the plane being built, its name or its previous airframe's name). `PickAirframe` raises it, so
only an explicit confirm on a row that is not already the pick asks (the switch itself stands
either way); a new plane arrives with `AirframeChosen` false, nothing ticked and nothing asked,
and `StartFromSaved` starts chosen and never asks. `AnswerDefaultsAsk(true)`
runs `LoadAirframeDefaults`: gun picks and per-wing hardpoint counts read back off the airframe's
stock fit (`StockFits`, the A3 mapping and `StockWingCounts`, D32's wing rule reversed), engine
id 1 (the stock Lvl-2 tier), and armour from the stock zone allocations (`ZrdrPath` through
`PlaneStats`, pools / 5); a missing source loads that default empty. Off-engine coverage:
`CSVM.Tests/HangarFlowTests.cs`, `CSVM.Tests/HangarAirframePageTests.cs`,
`CSVM.Tests/HangarEnginePageTests.cs`, `CSVM.Tests/HangarArmourPageTests.cs`,
`CSVM.Tests/HangarGunsPageTests.cs`, `CSVM.Tests/HangarHardpointsPageTests.cs`,
`CSVM.Tests/HangarPaintPageTests.cs`, `CSVM.Tests/HangarNamePageTests.cs` and
`CSVM.Tests/HangarPurchasePageTests.cs`.

## src/UI/CampaignFlow.cs
Built-in's campaign screen graph as one engine-free flow over the shared `CampaignFeature`
(`src/UI/Menu/CampaignFeature.cs`), the same split `HangarFlow` makes over its feature: the feature
owns the profile, the seated player, the mission named and every write into the store; a page owns
its rows and its navigation; the launchscreen owns every Godot control. Screens are a stack, not a
fixed order, because the campaign's navigation is a graph; `GoTo` returns to a screen already open
instead of stacking a second copy, and backing out of the first one ends the flow with
`CampaignExit.Cancelled`. `CampaignFlow.Registry` maps a `CampaignScreen` to its page factory and is
the wave's whole mount point: a new screen is one page file plus one line there, with
`CampaignPlaceholderPage` covering any screen not yet registered. A page may hand the shell a
`HangarArt` (the same art column the hangar draws) and a `CampaignTextEntry`; while that field is
armed, `CapturesText` tells the shell the keyboard is typing, and the flow's own cursor axes edit
the name instead of the list. `LaunchMenu` builds every flow over the host's one feature
(`CampaignFlow(feature)`, the feature already opened on a store); the older store-first constructor
opens a private feature for the unit tests and the campaign loop suite. Every property a page reads
(`Store`, `Profile`, `Roster`, `MissionSeq`, `Mission`, `AmmoSlot`, `PlaneSlot`, `ScrapbookEntry`,
`ZoomTarget`, `Field`, `Planes`, `Stock`, `DataRoot`, `Strings`) forwards to the feature, and the
pages write through the feature's operations (`ContinuePlayer`, `DeletePlayer`, `CommitLoadout`,
`CommitPlanes`, `ExportPlane`), so the flow itself never touches the store. The stack, the cursor,
`Message` and `Modal` stay here: they are how Built-in offers the operations, not the campaign's
state. `TakeModal` and `TakeMessage` hand a page's dialog or refusal to a presentation that shows
them its own way and clear them; Built-in never calls either, answering its dialogs through
`Accept` and `Back`. Original hosts the same pages in a flow of its own over the shared feature
(`OriginalCampaign.cs`) and mirrors its screen and row into it rather than walking it: the pages
are the content, the graph is Original's. The roster page's `OpeningRow` answers with the last-seated profile's row
(`docs/org/campaign-board.md`, "Which player the profile screen opens on").
⚠ A campaign page draws as a composed board, not as the shared `BoardMenu` idiom: it contributes its
`Pictures`, `Strokes` and `Captions` and names which of the screen's authored buttons each row
presses through `Button`, and `CampaignBoards` supplies the geometry, reading it through the flow's
`Layout` (`CampaignLayout.For(DataRoot)`, or the instance a constructor was handed), which is
Built-in's alone: the feature carries no presentation geometry. The `HangarArt` a page still
hands over is the hangar's own art column and is unused on the campaign path.
`CampaignFlow.Field` is the feature's sortie field (`src/UI/Menu/CampaignFlightField.cs`);
`AmmoTarget` (the feature's) is the one place that turns "whose check is showing" plus `AmmoSlot`
into the record the ammo screen edits, so neither page resolves an aircraft out of the profile by
index any more.
Off-engine coverage: `CSVM.Tests/CampaignFlowTests.cs`,
`CSVM.Tests/CampaignRosterPageTests.cs`, `CSVM.Tests/CampaignTextEntryTests.cs`,
`CSVM.Tests/CampaignCabinPageTests.cs`, `CSVM.Tests/CampaignPreviousMissionsPageTests.cs`,
`CSVM.Tests/CampaignComboTests.cs`, `CSVM.Tests/CampaignModalTests.cs`,
`CSVM.Tests/CampaignPlaneSelectionPageTests.cs`.

## src/UI/Menu/CampaignFlightField.cs
Owns a campaign sortie's humans as part of the shared `CampaignFeature` (`Feature.Field`, in
`CSVM.UI.Menu` so either presentation walks the same field): joined count, current flight check
and each guest's pick. Player 0 keeps the seated profile's aircraft; later players fly
session-scoped stock records or copies, so guest edits cannot persist.
`Advance`/`Retreat`/`Rewind` walk one reused `FlightCheck` page through the field. The seated
player's first FLY MISSION latches `Locked` across that walk; only `Rewind` to the briefing clears
it, while a physical disconnect may still shrink the committed field. The no-duplicate filter
identifies stock choices by airframe and profile choices by plane name. `Taken(player, pick)` is
the question `CampaignPlaneSelectionPage` asks; `Choose` also refuses a taken entry, so no caller
can seat two humans in one aeroplane. The picker replaced the in-place stepper, so the roster is
chosen from rather than walked. `IsStock` keeps stock records out of `CustomPlaneStore` lookups.
Off-engine coverage: `CSVM.Tests/CampaignFlightFieldTests.cs`,
`CampaignFlightCheckPageTests.cs`, and `CampaignAmmoPageTests.cs`.

## src/UI/BoardFit.cs
How the original's fixed 800x600 campaign dialog space lands on an arbitrary window: one uniform
scale on both axes, the board centred, the remainder letterboxed. A `record struct` with `X`, `Y`
and `Length`, so every element goes through the same mapping and only the scale changes. ⚠ The two
rejected alternatives (an integer scale, a fit-to-height bleed) and why the art must be sampled
nearest rather than smoothly are in `docs/org/campaign-board.md`; that decision is inherited by
every campaign screen, so changing it here changes all six. A viewport with no area falls back to
1:1 rather than a scale nothing can draw at. Off-engine coverage:
`CSVM.Tests/ComposedBoardTests.cs`.

## src/UI/ComposedBoard.cs
What a composed campaign screen is made of, engine-free: the screen's own fixed `Backdrop`, the
`Fills` a page paints on it, pictures at authored pixel positions, connector strokes, text lines,
button plaques, and flowed list widgets, each in draw order. The backdrop is a layer of its own so
a fill can sit over the painted background and still stay under the page's own pictures, which is
where a list's selection bar goes; a `BoardFill` is the `ldrawrect`/`ldrawframe` pair a list widget
draws its row states with, and `Border` picks the outline over the fill. `BoardArtLibrary.Loose`
is the one library that is not a library: the art's name is its whole path, which is how a
scrapbook capture out of a profile directory reaches the same draw path as extracted art.
⚠ Plaques are one layer over every picture, so a screen whose authored z interleaves the two (the
results card between its own Best to Date and Most Recent tabs) has to draw the under-side one as
a picture itself; `CampaignScrapbookPage` is the only page that does, through `CampaignBoards.SlotOf`.
`BoardNote` is the last of those: a widget's entries plus its authored wrap box and spacing, which
`Flow(height)` turns into placed lines. It takes the measurement as an argument because how tall a
wrapped entry drew is a font metric the engine-free half does not hold, and it is a layer of its own
rather than one `BoardLine` per entry for exactly that reason (`BL-490`). Carries the two state rules
as statics — `PlaqueFrame` picks a four-frame strip's disabled/normal/rollover/depressed frame,
`PlaqueInk` picks a one-frame plaque's label face, which is the whole of the briefing's
`BtnLabelNormal`/`Rollover`/`Activate` triad. `BoardArt` names a bitmap and how many stacked frames
it holds; the renderer, not the model, resolves it to a file, which is what keeps the JPG the screen
backgrounds ship as out of the engine-free half.

## src/UI/CampaignBoards.cs
The fixed chrome of all eight campaign screens plus the composer that turns a page and a cursor
into a `ComposedBoard`. Every button slot, background pane and text slot names its `LAYOUT.CSV`
section and row and is read from the decoded layout through `CampaignLayout`, with the value the
board drew before the layout existed standing beside the read as the fallback, so the screen
composes the same whether `menu_layout.json` is present, absent or unreadable; the briefing is the
exception, its chrome being `Briefing.zrd`'s own (`docs/formats/briefing.md`) with no layout row
behind it. A slot marked pinned (`PinnedY`, `PinnedArt`) keeps a value measured off the reference
screenshot where the row differs from it by a pixel or a bitmap: `CM_B_START` keeps `CM_B_Start.png`
where its row names `GN_B_Continue.png`, the four paper plaques keep 131/349 where their rows say
132/350, `OL_S_AMMODESC` keeps y 92 where its row says 96; `docs/org/campaign-board.md` carries each
with the row's value. A page names one of these buttons per row through `ICampaignPage.Button`;
every other row lists down that screen's own text widgets via `TextSlot` (`CM_E_NAME`, then
`CM_L_PLAYERS` at its own `ItemHeight`; `FC_T_PILOT`/`FC_T_WINGMAN`), unless the page draws that
row itself, which is what the table of contents' 80-pixel rows are. The message box (`Dialog`) reads
`[@MessageBox@]`'s rows for everything inside the 410x300 art and keeps the centred screen position
the reference puts the art at, since the section carries none; its second form takes a message and
any button set (`DialogButton`: the row key, the words, the strip frame and the label ink), which
is how Original draws the one-button box and the delete confirm's two-button box with the answer
under the pointer in its rollover frame, and `DialogSlot` answers a button row's board position for
hit-testing. `SlotOf` by `BoardButtonRef` (button and crew slot) is the same door for every plaque,
and `ComboFieldHeight` is public for the same reason: Original reads a page's button, field and
list rows back as rectangles from here rather than carrying geometry of its own. The drop-down and scrollbar strips a
`CampaignCombo` draws come through the `[GLOBALVARS]` macros (`GN_DROPDOWN`, `GN_DROPUP`, `FC_UP`,
`FC_DOWN`, `FC_SLIDER`), since a combo carries no row of its own.
⚠ The ammo screen has no `TextSlot` entry at all and must not be given one: its picks are
`ICampaignPage.Combo` drop-down fields at `[@OrdinanceLayout@]`'s own dropdown positions and its
calibre captions are the page's own `Captions`, so no row of it reaches that table.
`ObjectivesNote` is the one widget that is not a slot list: the briefing parchment's `LIST` flows its
entries, so it carries a wrap box and a spacing instead of a pitch and the renderer measures it.
⚠ `Labelled` on a slot, not the frame count, decides whether a plaque's words are drawn over it: the
generic paper buttons are four-frame strips that still carry an `IDS_*` label. `CampaignScreen.Scrapbook`
reuses the results page's own `SB_BackGround.jpg` and its `SB_B_REPLAY`/`SB_B_RETURNPC` positions;
`CampaignScrapbookPage.cs` supplies its rows and `CampaignScrapbookResults` its content.
`CampaignScreen.ScrapbookZoom` carries no static `Chrome` (its background is per-scrap, supplied by
the page's own `Pictures`), only its `CLOSE` button (`GN_B_Continue.png`). Off-engine coverage:
`CSVM.Tests/ComposedBoardTests.cs`, `CSVM.Tests/CampaignLayoutTests.cs`; in engine,
`campaign-layout-parity` (`CSVM/src/Testing/CampaignLayoutSuites.cs`) composes every campaign aid
over the hardcoded chrome and over the install's decoded layout and compares the two boards
element by element.

## src/UI/CampaignLayout.cs
The decoded menu layout as the campaign boards read it: one widget row's authored geometry and art
by section and key, every read taking the value the board drew before the layout existed as its
fallback (`At`, `Box`, `Int`, `Art`, `GlobalArt`, `Justify`, `ZoomFamily`). Engine-free, over
`MenuLayout`. `At` and `Box` answer with the whole row or the whole fallback, never one coordinate
from each, so a row missing a column cannot shift an element half-way. `Art` takes the row's own
frame count with its `ArtPath`; an arrow or slider named in another column, and a `[GLOBALVARS]`
bitmap, carry none in the layout and keep the fallback's frames. `Justify` reads the three
alignments the renderer has and falls back on the layout's 3 and 4. The load rule: `For(dataRoot)`
reads `extracted/rof/menu_layout.json` once per data root and keeps it in a static table;
a missing or unreadable file is the `Fallback` instance with `Reason` set and one `ui` warning
logged ("campaign boards draw their hardcoded chrome: ..."), so the boards draw exactly as they
did before the layout existed and nothing throws; a null data root is the `Fallback` with no
reason, since nothing was there to read. `Over(MenuLayout)` wraps an already-parsed artifact for a
test's fixture. Built-in loads the layout for itself here rather than through the Original
presentation's `OriginalAvailability` load, because that load also refuses a tree missing art the
manifest requires, a condition the campaign boards must draw through ("usable without"); the two
reads are of the same file and the same reader. `CampaignFlow.Layout` is where the pages and
`CampaignBoards` get it (`For(DataRoot)`, or the instance a constructor was handed), and
`LaunchMenu.CampaignLayoutOverride` lets a suite pin every flow it opens to the `Fallback` for a
parity comparison. Off-engine coverage: `CSVM.Tests/CampaignLayoutTests.cs` (the fallback rule over
an empty root, a malformed file and a foreign JSON; the reads over the hand-authored
`fixtures/menu-layout-original/LAYOUT.CSV`; a moved row moving the composed plaque and a pinned
value staying put).

## src/UI/ComposedBoardView.cs
The Godot half of the campaign boards: draws one `ComposedBoard` over the whole window through
`BoardFit`, with `TextureFilter` pinned to Nearest so the authored pixel grid stays hard. Owns the
texture cache and the only art resolution there is — `extracted/rimage/<name>.png` for mission art,
`extracted/rof/ASSETS/GRAPHICS/<name>` for screen chrome — loading through `Image.LoadFromFile`,
which reads the JPG backgrounds no engine-free decoder here covers. A miss is cached, so an absent
extraction is probed once per name and the screen degrades rather than throwing. Supplies the font
metric a `BoardNote` cannot take for itself (`Measure`), which is the only reason a flowed list is
not composed engine-free. Carries the one
piece of chrome that is not the original's: a two-line hint band across the top of the board with
the focused row's description and the controls line, because the original said both with a mouse
pointer and a pad has none.

## src/UI/BoardPalette.cs
The ink a campaign board writes in, one palette per background family, because the screens are
painted art and the grey the flight check's forms use is invisible on the cabin's dark hangar. The
flight check and ammo values are their layout rows' own ARGB fields; the rest are chosen to read on
their background, and `docs/org/campaign-board.md` says which is which.

## src/UI/BoardMenu.cs
A board's cursor and item list, engine-free so the selection rules test off engine the way
`PauseState` does. Holds no input source: the board polls its menu owner through `MenuInput` and
feeds one frame's result to `Handle(move, accept, back)`, which is what stops a pad steering a menu
it does not own. Returns whether the highlight moved, so a board repaints only when it has to.
Opens on the first item, and the boards order their rows so the first is the harmless one (Resume,
else **Photo Mode**) — a stray confirm on a menu that just appeared then cannot destroy a run. That
is why Photo Mode leads a results board rather than trailing it: the alternative resting row is
Restart, which throws away the run just finished (`BL-429`). Confirm
beats back in the same frame, the row having already been chosen. A results board's menu is not
`Dismissable`: dismissing it would leave the player in a halted world with no way back, so it
answers no back key and advertises none. Off-engine coverage: `CSVM.Tests/BoardMenuTests.cs`.

## src/UI/LoadBoard.cs
The load screen drawn over the whole window while a session builds: the original's own composed
artwork (`docs/org/loading-screen.md`) through `ComposedBoardView`, so it inherits the campaign
boards' authored-pixel surface and `BoardFit`'s scaling rule. Two compositions, the split the
original makes: a campaign launch gets the chart sheet (`loadframe`, the unlit scale at `90,548`),
everything else the blackboard (`loadframempt2`, its three authored photographs each centred on its
own coordinate, the unlit lamp strip and one still propeller frame). Free flight and dogfight are
ours rather than the original's and take the non-campaign screen. Populated in `_Ready` rather than
`Build`, since the view sizes itself off the viewport and a node outside the tree has none to read.
Its subject line names the chapter and the flight the way a player picked it —
`Launcher.LaunchSubject` takes an Instant Action mission's name from
`InstantAction.MissionTypeLabel` ("Attacking a Zeppelin"), never `SessionSpec.ModeName`, which is
the log file's internal tag ("fly", "stunt") and not a player's word. ⚠ The bar draws its UNLIT
strip and the propeller one still frame, and neither ever moves: a fill and an animation both need
the build decoupled from the draw, and while the build stays one synchronous block `StartupProfile`
reports its phases only after the fact, so a moving bar would be a fiction. ⚠ The two text lines are
placed by us; the decode carries no text coordinates. The Launcher owns the show/free pair; see its
entry for the deferred-build handshake.

## src/UI/BoardMenuItem.cs
The rows a board menu can offer — Resume, Restart, Exit. The board owning the menu decides which it
carries and what each does; Resume appears only on the pause board, and Exit's label follows
whether the session can return to the launchscreen or only quit.

## src/UI/CursorRow.cs
One centred list row with a ▶ cursor, shared by every menu that has one: the launchscreen's screens
(`LaunchMenu.Row`), its per-player aircraft panes, and every board menu through `BoardMenuView`.
⚠ The marker is a cell of its own, never a prefix on the row's text. Prefixing a centred label with
`"▶  "` when selected and `"     "` when not centres it on the PADDING too, and the two are not the
same width, so every unselected row drifted sideways — which is what made these menus read as
uncentred. A fixed-width marker cell plus an identical mirror cell on the right puts the label on
the panel's centre line in both states, and centring the three as a GROUP (label at its natural
width, not expanding) is what keeps the marker beside the text instead of out at the panel edge.
The marker is emptied rather than hidden when unselected: a Godot container skips invisible
children, which would collapse the cell.

## src/UI/BoardMenuView.cs
Draws a `BoardMenu`'s rows as `CursorRow`s (dim rows, the highlighted one gold behind a marker)
inside the board style all five boards already share, so the cursor reads the same wherever it
appears and a layout fix lands once — in `CursorRow`, which the launchscreen draws too. `Refresh()` recolours from the current highlight,
touching only label overrides. The footer is the button legend, since nothing else on a board
teaches the cursor; a results board's has no back key to name.

## src/UI/BoardMenuHost.cs
`BoardMenu` + `BoardMenuView` + the reader, kept together so a board wires a menu in two lines
rather than restating the poll-handle-repaint order five times. `Build` primes the reader, so a
button still held from whatever raised the board is not read as a fresh press. ⚠ `Poll` takes WALL
time: the clock this menu is holding does not advance, so auto-repeat on sim dt would never fire.
Reads `PadBack`, not `Back` — Esc and Start reach the pause toggle through `FlightController`, so
the combined back would act twice on one press.

## src/UI/MenuInput.cs
One player's menu input source — keyboard flag (player 1 only), `Pads` binding, edge/auto-repeat
state; `Poll(dt)` fills Move/MoveX/Accept/Back/PadBack/Start out of the `Menu` binding context
(`src/Bindings/`), resolved once a tick through three readings of one seat: keyboard live,
keyboard minus the typeable keys (`TypingMap`, what `TextEntry` reads), and the pad alone. Its pad
rows sit on the seat-local `SeatPads` identity, since a seat reads a *set* of pads rather than one
device and no binding may hold a connection index; `SeatDeviceState` answers for it through
`Pads.For`, which is what keeps the focus and `--no-pads` gates. `JoinPressed` and `LastActivePad`
stay raw polls: both answer which pad acted, which an OR across a seat's bindings cannot express,
and typed text has no named action at all. `Pads` is nullable, null meaning every connected pad, which is the same binding
`FlightController.PadDevices` takes — a single-player session has no per-player assignment to hand
over. `PadBack` is the pad's B alone, for a reader whose Escape is spoken for elsewhere; a board
menu's is. Serves both the launchscreen and the in-flight board menus.
`MoveX` (Left/Right) is `Move`'s horizontal twin, added
so an Instant Action wizard screen can carry a vertical list cursor and a horizontal stepper at
once without either read starving the other: MissionType's lives, WaveEdit's four fields and
Wingmen's count/aircraft all read it — every other screen ignores it. In the menu a `MenuInput` is
the device half of a seat: `BuiltInSeat` wraps one into an `IMenuInputSource` (seat 0 over the
keyboard plus the unclaimed pads; a joined pad over a one-pad poller), and `MenuSeatDevices`
binds `Pads` and reads `Pad`, `LastActivePad` and `JoinPressed`. Nothing in the shared player setup
reads it; a seat's pad is this poller's own detail.
`Typed` (the letters, digits and spaces pressed this frame, upper case under Shift) and `Erase`
serve a screen with a text field; they are polled and edge-detected per key like everything else
here, not read off an input event, so text and navigation share one clock. `PadMove`/`PadMoveX` are
the pad's own halves of the two cursor axes: a screen whose keyboard is typing reads those instead,
since W, A, S and D are letters there and the combined axes would move the cursor as one types.

## src/UI/SplitScreen.cs
The splitscreen rig for 2–4 players (1P never constructs it, keeping that path untouched): black
gutter backdrop, one `SubViewport` pane per player sharing the main `World3D`, plus the
`PlayerColor`/`PlayerTag` identity table. Sharing the main `World3D` means every pane also shares
the one sun and WorldEnvironment, so enhanced graphics mode's lighting, shadows and per-zone updates
reach every pane with no pane-local plumbing; each pane computes its own directional shadow splits
off its own camera, and `PositionalShadowAtlasSize` is moot since no omni casts a shadow (B14).
See "Rendering: the enhanced graphics mode" above for the divergence record as a whole.
**The pinned 3D audio listener model (2026-08-15): every pane is a listener**
(`AudioListenerEnable3D`). Godot 4.7 takes the per-channel MAXIMUM over all listener-enabled
viewports of the `World3D` and culls `max_distance` per listener, so an emitter is heard at its
NEAREST pane's volume with no N-fold buildup and no manual attenuation; the cost is that panning is
unioned across panes, which share one stereo out. Without it a splitscreen session has NO listener —
the main camera stands down here and a camera is in the `World3D` listener set only while current —
and every `AudioStreamPlayer3D` in the world goes silent, uncounted and unlogged. Pinned by the
`splitscreen-listeners` suite.
**`Fill(true)` gives pane 1 the whole window for a cutscene** and takes the other panes, their
listeners and the gutter backdrop down; `Fill(false)` lays them back out. Four small copies of one
camera path is not a picture anybody framed, and every rig camera mirrors `camera1` while a
definition plays (`Session/CutsceneController.cs`), so what the other panes draw during an episode
is the same shot. Nothing is rebuilt: each pane keeps its camera, its `HudParent`, its private
visual layer and its cull mask, and the hidden panes keep their own quadrant, so the restore is a
visibility change and one rect. A hidden pane's render target is disabled with it, so an episode
costs one rendered view rather than N of the same shot. ⚠ The listener of a hidden pane is taken
down BY HAND, and exactly
one is left standing: this is the one place the pinned model above is deliberately departed from,
and leaving zero is the silent session that model exists to prevent. The main viewport's camera
stays down throughout, which is why the collapse expands a pane rather than re-arming it. Pinned by
the `campaign-coop-cutscene-fullscreen` suite.
**`NoteSkip(index)`** stands a line naming the skipping player, in that player's own `PlayerColor`,
over the window for a few seconds of wall time; the session calls it beside
`CutsceneController.Skip`, which logs the same fact once. Splitscreen only, because with one human
there is nobody else the key press could have been.

## src/UI/ScreenFlash.cs
The full-screen colour wash: **two channels over one pixel per pane, one hidden `ColorRect` per
rendered view** (`HudLayers.WorldOverlay`, under each rig's `HudParent`, built with the rigs so
every runtime can be handed the same sinks). The **ramp channel** is the `FBFX_COLOR_FROM_TO` wash,
a close HE, AP or flak burst ramping the picture from one RGBA to another over the event's run time.
`AnimRuntime`'s handler pushes `(from, to,
run_time, origin, radius²)` into `Play`; the node lerps in RGBA on `GameClock` sim time and ends — it does NOT
hold the `to` colour, because the original re-arms its frame-buffer object for the current frame only
and a completed chain simply stops re-arming. `Play` **replaces** whatever that pane is running,
which is the original's composition rule literally: one process-wide state a second burst overwrites
(decode in `docs/formats/anim-definitions.md`).
The **blend channel** (`PlayBlend(playerIndex, colour, weight, duration, startDelay)`) is the
victim-routed wash of a sonic, flash or smoke hit, one `BlendWash` per pane (`src/UI/BlendWash.cs`,
below), addressed by the struck aircraft's `FlightController.PlayerIndex`: a human seat's index is
its pane index by construction (`FlightRigAssembler` assigns `pi`), an AI's is `ShooterIdBase + n`
and out of range, so it paints nothing. No camera is read on this channel. The two composite at
paint time only (`Apply`: `BlendWash.Composite`, the standard alpha "over" of the wash on top of the
ramp, folded into one RGBA), so a pane with no blend wash paints exactly the ramp's own colour and
the HE/AP/flak picture is unchanged by the channel's existence; `CurrentFor` reads the composite,
`BlendFor` the blend state alone. `Advance(dt)` is `_Process`'s body, exposed so a suite can step
both channels on its own clock. **Per pane, not one global**: the original holds one wash state for
the whole machine (`DAT_0064ef9c` and neighbours), which would blind viewer 1 when viewer 3 is
flashed; that divergence is deliberate. `--debug-wash=N`
addresses two overlapping scripted washes to viewer N so the channel can be seen with no weapon
firing it (`GameSession._Process`). Pinned by the `fbfx-flash` suite (routing: player 2 addressed,
pane 1 untouched, ramp composited under) and `BlendWashTests` (the rules).
**Which panes the RAMP washes: every pane whose own camera is inside the burst's authored
radius**, read off the session's `ViewerSet`, which `GameSession` hands to `Build` alongside the
same rig list the panes come from — so pane *i* and camera *i* are the same rig by construction, and
a set that does not match the pane count is not indexed at all (every pane washes, the labs' case).
The radius is the wash def's own `If PlayerRange` gate — `10000` m² = **100 m**, and all 24 shipped
wash defs (`he_ground_effect`/`ap_ground_effect`/`flak_effect` × 8 chapters) author exactly that one
gate — so the rule here is the original's own gate re-asked per player rather than a new constant.
Radius 0 means ungated and paints every pane; the intro cutscene's `gi_scene1` is the one such
carrier, and an ungated wash is not a proximity effect.

## src/UI/BlendWash.cs
One pane's victim-routed wash state, the original's `FUN_0042e9d0` (start) and `FUN_0042eb80`
(tick) held per pane instead of in their one global; pure state and arithmetic, no node, so
`BlendWashTests` covers it off the engine. `Start(colour, weight, duration, startDelay)`: a first
hit takes the weight as `Peak` and starts the displayed `Weight` at 0; a hit landing on a running
wash **blends**, `Peak' = p + w − p·w` and the colour mixed as `(old·p + new·w) / (Peak' + w)`, the
original's own arithmetic including its normalisation by the NEW peak plus the incoming weight
(white then red at full weight reads pink `(1, 0.5, 0.5)`), then restarts the envelope's clock
without dropping the displayed weight. A non-positive duration clears the pane, as the original's
routine does. The **envelope**, stepped on sim time: attack over `0.15 × duration` (the displayed
weight climbs `dt/attack` of the peak per step, capped at the peak), sustain at the peak, release
over the last `0.35 × duration` (the peak sheds `dt/release` of itself each step, so it decays toward
`1/e` of the sustain by the cut), then a hard cut at the duration. The release decays `Peak` itself,
so a re-hit late in a wash blends against a lighter one. `startDelay` holds the pane clear before
the envelope begins and is read on the first hit only; the original passes 1.0 s for the sonic/flash
wash and 0 for the smoke wash (its two callers, `FUN_004b9bc0` and `FUN_004b8fd0`), which the
consumers `D15`/`D18` author. `Composite(under, colour, weight)` is the paint-time rule
`ScreenFlash.Apply` uses: the wash "over" the ramp's RGBA, `a = a₁ + w(1 − a₁)`, colour
`(c₁·a₁·(1 − w) + c·w) / a`, clamped; weight 0 returns `under` unchanged.

## src/UI/LiveryLab.cs
The `--viewer` livery editor (key L): squadron stepper (loads the squadron's whole livery via
`LoadSquadronLivery`), per-slot RGB sliders, decal steppers, random livery, copy-CLI-args.

## src/UI/NodeLabels.cs
Floating node-name labels (key F16) in both the static viewer and flight, cycling Off → Meshes → All;
`--debug-names[=meshes|all]` presets the mode at launch. In splitscreen the nearest/de-clutter pick
is P1's viewpoint alone (rig 0's camera); every pane still renders the resulting labels, since they
are ordinary world-space children of the root.

## src/UI/MarkerOverlay.cs
The `--viewer` marker overlay (key K): draws every firepoint / pylon / target on the parked
aircraft as a coloured gizmo + billboarded label (firepoints orange, shared-mount firepoints
magenta, pylons cyan, target green); `--markers` opens it at launch. Reuses `MarkerRig.Classify`
+ `GroupCoLocated`, so its gizmos agree with `--dump-markers` by construction.

## src/UI/PhotoModeHud.cs
Photo mode's only screen furniture and its way out: a hint line naming the bindings on a
`HudLayers.Board` layer of its own, and the Escape / pad-`B` read that raises `Exit` for
`GameSession.ExitPhotoMode` to act on. Decides nothing about the mode itself.
The hint **fades** (5 s lit, 1.5 s out, both TUNE) rather than persisting or toggling: the mode
exists to compose a frame and a permanent strip would be in it, while a mode that has swallowed the
menu with no visible way back is the worst thing it could be. The fade runs on WALL time — photo
mode holds the clock, so a sim-timed fade would never start. Pad reads go through the seat's own
`Pads.For` filter, so in splitscreen another player's pad cannot close a mode that is not theirs,
and the exit press is marked handled so it cannot also reach the suspended board behind it.

Photo mode itself lives in `GameSession.EnterPhotoMode`/`ExitPhotoMode`: it sets `CameraOwned`,
hides the whole pilot HUD (`FlightController.SetPilotHudVisible`), stands up a `SpectatorCamera`
on the pane with the rig's own device filter and `LockCandidateAircraft`, and locks onto the
player's OWN aircraft — `FollowNode` seeds from the current eye, so following what the camera is
already looking at never jumps, while locking any other plane would keep the offset and teleport.
The halt is never dropped, so the world stays the still frame the board froze.
⚠ Suspending a board is hide AND `ProcessMode.Disabled`, not hide alone: a board left processing
still polls its owner's menu reader, so the cursor keys would drive an invisible menu while the
same keys fly the camera — the very collision this mode exists to remove.
⚠ `FlightController.InPhotoMode` silences that node's pause key for the duration, or one Escape
would both leave the mode and unpause the session behind it.
⚠ Leaving primes every board's `MenuInput` before the cursor comes back. `MenuInput` polls raw key
state, which bypasses the handled flag the exit press set, so an Escape still under the player's
finger would read as a fresh press on the board that just returned and dismiss the pause it was
meant to reopen — `BL-279`'s mechanism, a third time.

## src/UI/PerfHud.cs
The frame-cost readout (key **F14**): fps, current frame cost and the
worst recent frame, cycling Off → Compact → Full → Off; `--debug-fps[=compact|full]` presets the
mode at launch. Built once by `Launcher` (never per `GameSession`, never per splitscreen pane) —
fps/frame-cost/GC are process-wide facts, so one readout for the whole window is correct and a
per-pane copy would just be four identical readouts at four times the layout cost. That hosting
is also what makes it work at the launchscreen, in `--viewer`/`--freecam` and in flight for free.
Fed the same raw `Stopwatch` `frameMs` `HitchMonitor` ticks on (never Godot's `delta`), every
frame, unconditionally — the worst-frame peak has to already be warm the instant F14 is pressed,
or it would have nothing to say about the hitch that made someone look. Off by default and builds
nothing until switched on, so the 11 golden screenshots stay byte-identical.
Both its controls are placed by their **right edge only** (`PlaceTopRight`: `OffsetRight = -8`,
`OffsetLeft` derived from the width, `GrowHorizontal.Begin`), so the readout's right edge stays
8 px inside the window at every window size and font scale while the text grows leftward. A
right-anchored control's `OffsetLeft` is where its box was pinned, not where grow-left ended up
drawing it, so reading it back (or assigning `Size`, which derives `OffsetRight` from it) walks the
control a full width off the side of the window; that is what put the frame-time strip off screen,
and `--run-tests=perf-hud-layout` is the arm that holds it.
On `HudLayers.PerfReadout` (11), **above `HudLayers.Board`**: the launchscreen's background is a
full-screen opaque `ColorRect` on `Board`, and this readout has to read there too. Sized off
`HudMetrics.ReferenceHeight` through the plain window-height ratio, not `HudMetrics.Scale` —
that method's `PaneFactor` damping is exactly wrong for a control that isn't per-pane.
**Full** adds four lines under Compact's fps/frame/worst headline — the
current frame's `FrameCounters` split (script/render-cpu/gpu/physics ms), its draws/prims/nodes/
mem terms, `GC.CollectionCount` per generation (raw counts, not deltas — a live readout reads
better as "gc2 has fired 3 times" than as an almost-always-zero per-refresh delta), and C8's
breadcrumbs (`PerfSample.SnapshotInto`, the same `site:callsxms` grammar `HitchSidecar`'s log line
uses) — plus `PerfHudStrip`, a second top-level `Control` in the same file (the `PerfSample.cs`
precedent for more than one type per file) drawing a rolling bar graph of recent frame times with
the trigger threshold marked as a line. Every Full term is a SECOND VIEW of data collected
elsewhere, never a new sample: the split/count/memory terms are the same `FrameCounters` read
`HitchMonitor.Tick` was just handed, and the strip reads `HitchMonitor.CopyRing` — a new accessor
onto the monitor's own always-live ring buffer (distinct from `Last.Ring`, which only advances on
a trigger) — every draw, so the display and a hitch record can never disagree about the same
frame. `perfHud.stripFrames` (TUNE, default 120) sizes the strip, clamped to
`HitchMonitor.RingFrames` since asking for more than the ring keeps is meaningless.

## src/UI/MeshLab.cs
The geometry/shading lab (key M): normal lines, smoothing-seam wireframe, collider boxes, light
sliders + headlight, cull × normal-source override cyclers (`--debug-mesh=` scripts them). Two
shapes: the `--viewer` lab owns the parked plane; the scoped lab (`--freecam`/`--anim-lab`, over a
`SelectionService`) attaches to the current selection on M and restores on change/deselect.

## src/UI/WeaponLab.cs
The weapon lab's **panel** (`--weapon-lab`, key **B**): a configurator for the held aircraft's LIVE
loadout, hosted top-right in flight (the gauge cluster owns the bottom-right corner, the HUD the
top-left). It owns no weapon and fires nothing — the steppers write into the `FlightController`'s
bound `Loadout` and the aircraft's own trigger then fires it. GUNS arm gun groups, HARDPOINTS arm
pylons: a gun goes onto the SELECTED group with a full clip of its `CLUSTER_SIZE`; a hardpoint
weapon re-arms EVERY pylon (`ProbeRunner.ApplyRocketOverride`) and rebuilds `PylonOrdnance`. The
mount stepper drives `SelectGunGroup`/`SelectPylon`, the auto-fire toggle `AutoFire`/
`AutoFireRockets` by bank, and "reset to stock" restores the fit the session launched with (stock
as `--rocket=`/`--loadout=` left it, not as the file reads). Gun mounts are in `FirableGuns` order
because that is the order `SelectGunGroup` indexes; a plane with no loadout at all falls back to the
raw marker rig, which has nothing live to arm.
**Click to place:** a left click casts the lab's OWN physics ray from the camera, names what it hit
(`cs_name` ancestor via `SelectionService.NameOf`, surface id via `ProjectilePool.SurfaceIdOf`,
reported as `id/name`, distance) and re-parks the held plane on that same ray at the panel's
stand-off through `PlaceHeld`;
shift-click aims without moving, and an orange ball marks the aim point. The scripted twins all fire
on the first physics frame, most specific first — `--weapon-target=x,y,z`, then
`--weapon-surface=<registry name>` (nearest collider carrying that surface id — any of the
fourteen, so `dirt` means id 13 and NOT "everything untagged", which is `default` —
measured to the nearest
collision VERTEX, since a chapter's water tiles all sit at the world origin), then
`--weapon-click=x,y[,aim]` — and every one of them ends in the same `PlaceOn` as a real click, at
`--weapon-standoff=` metres. **V** hands the rig's camera to a `SpectatorCamera` and back
(`--weapon-camera=free|<frames>`), the controller standing down via `CameraOwned` in between.
`--weapon-cycle=N` steps the weapon list every N physics frames; stepping, placing and the camera
hand-off are the only things this node does per frame. In splitscreen the lab stays P1-only by
design, one panel on rig 0's aircraft, camera hand-off included; a log line says so and the other
panes fly normally.

## src/UI/PanelFocus.cs
`Strip(subtree, who)` — makes every `Control` under a panel unfocusable and logs the tally
(`N control(s), M made unfocusable, focusable_left=0`), the invariant every panel hosted in a
**flight** session must hold. A focused `Button` answers Space with "press me again", so the pilot's
fire key re-fires the last-clicked stepper instead of the guns, and the arrow keys walk the focus
chain instead of reaching the aircraft or the lab's orbit camera (user-reported on the weapon lab,
2026-08-03; the flight damage lab had it too — 11 and 5 focusable widgets respectively).

## src/UI/SelectionService.cs
The shared world selection in `--freecam`/`--anim-lab`: left-click picks the mesh under the
cursor, PgUp/PgDn walk its `cs_name` ancestor ladder, a breadcrumb HUD line + wireframe box show
the current rung. `Current`/`Ladder`/`Level`/`CurrentBox` + the `Changed` event are the state the
other inspect tools read; `Select(node)` is the programmatic entry; `--debug-select=x,y[,up]`
replays a click for scripted runs. `ExtraRoots` walks props parked beside the world content rather
than under it (the anim lab's `--plane=` prop), each also capping its own ancestor ladder.
`SubtreeWorldAabb` is the shared box measurement (`AnimRuntime.VisualOriginOf` and `NodeLab` read
it too) and walks children by index with a prebuilt `StringName` for the overlay key: `GetChildren()`
and a string-to-`StringName` conversion each allocate a finalizable wrapper per node visited, which
a walk over a world subtree cannot afford on a per-frame path.

## src/UI/TargetingOverlay.cs
The targeting overlay (F15, `--debug-targets`): a per-frame line from every turret gunner
(`TurretController.TargetPosition`) and AI gunner (`AiGunner.Target`/`GroundTarget`, D36) to its acquired target,
coloured by the gate holding the trigger (`TurretController.Gate`), with that gate named per shooter
in the HUD. Depth test off, since the line into a hull is the one worth seeing. In splitscreen the
world-space lines draw in every pane on their own (default render layer, in every camera's
`CullMask`) while the HUD roll-call is drawn once for the window, like `PerfHud`, because it is
process-wide combat state.

## src/UI/DebugKillTarget.cs
The debug kill key (F17, `BL-534`): kills P1's currently selected `Flight.TargetSelection`
target through its own death path, so kill-count and `ObjectiveGraph`/`GroupLiveCount`
bookkeeping see it exactly as a real shot would, never by freeing the node. Routes on the
selection's `Candidate.Source` type: a `FlightController` crashes through
`FlightController.DebugForceCrash(killer)` — the same attributed `Crash`/`Downed` path
`--debug-scoreboard`'s scripted kill and `--crash` already use; a
`Mech3.DestructibleRegistry.Instance` (a zeppelin gasbag, cannon or engine) is destroyed through
`AnimRuntime.DamageAt(inst.Anchor, inst.MaxHealth + 1f)`, the same call a rocket makes and
`WorldDamageLab`'s own Kill button uses. A `TurretController` selection (world emplacement or
carried) is left inert: `Flight.TargetRef.Health`'s own decoded rule is that a turret carries no
`HEALTH` key at all, and nothing in the engine toggles a turret's kill switch
(`TurretController.Alive`'s healthy-node visibility) today, so routing a kill through it would be
an invented mechanism, not a decoded one. Nothing selected is inert too. P1-only, the precedent
F5's `WorldDamageLab` and F51's weapon lab already set for a single-pane debug tool; wired into
`GameSession` beside the F15 `TargetingOverlay` build, reading P1's own `FlightController` and
the session's `AnimRuntime` through closures for the same reason `TargetingOverlay` does (both
outlive the line that builds them: waves activate and turrets die long after). The routing switch
is exposed as `KillSource` so a suite can drive it against hand-built sources with no live
`AimCandidateSet` scan behind it.

## src/UI/TileGridOverlay.cs
The map-edge tile-grid overlay, flag-only (`--debug-tilegrid`; no key is bound): every ground tile
tinted 20 % by repetition band, so one colour band is one block. `--map-edge-block=` and
`--map-edge-mode=` set the depth and the fold once at launch. This is the instrument that settled
the map-edge fold; the measurements it produced are in
[formats/world-structure.md](formats/world-structure.md).

## src/UI/ColliderOverlay.cs
The collision wireframe overlay (key C, `--collision=show`/`--debug-colliders` script it) in
`--freecam`/`--anim-lab`/`--fly`: one `ImmediateMesh` per collider host, colour-coded by the
**surface id** its body resolves to (`0/default`, `1/water`, `13/dirt` …) plus the three owner keys
neither surface tag decides (clutter / plane / other), rebuilt from the live tree on every show. A
colour→key legend (`BuildLegendText`, sourced from `ColorFor` alone so
a palette change can't desync it) sits under the summary whenever wireframes are actually shown —
never for the "no collision built" notice, an empty-legend echo of that same gap. Measured C2: 1,848
node-backed shapes + 10k–14k clutter placements. The id drawn is the RESOLVED one, not the raw
stamp (`EffectCatalogue.ResolvedSurfaceIds`), and it is a picture of what the engine will select,
not of the material data: the id is stamped per collider body, not per polygon, and the body's
displayed NAME still comes from the texture-derived class, which can disagree with the id on real
bodies (`analysis/surface-classification/FINDINGS.md`).

## src/UI/AiNetsOverlay.cs
The AI patrol-net overlay (F13; `--debug-ainets[=name,…]` scripts it) — added to every chapter
world by `GameSession.BuildWorldStage` (skipped on the `--node=` partial stage). Draws each net in
a stable id-derived colour (golden-ratio hue): edges as individual segments off the edge list,
sphere markers per node (tagged nodes bigger), one fixed-size `Label3D` per net with the trailer
(`M4ReinfAce#10 → player`), all depth-tested. Nets load lazily on first toggle; the census — one
line per net — goes to the `world` log. A HUD text field narrows the drawn set live by
case-insensitive name prefix. F13 is the first tenant of the F13–F24 debug-overlay key
range (`docs/controls.md`).
It also draws LIVE LEASHES while up: one `ImmediateMesh` line per AI aircraft, from the plane to
the node its follower is flying at, plus a short vertical tick at the plane end. The overlay knows
nothing about aircraft: `CollectLeashes` is an `Action<List<AiNetLeash>>` the session fills from
each pilot's own `AiNetFollower.CurrentTarget` (built before any AI exists, hence a supplier and
not a snapshot). `AiNetLeash.Steering` is `AiPilot.SteeringPatrol`, which the pilot REPORTS off its
own dispatch rather than the overlay re-deriving it from the mode: a leash for a plane that only
holds its node while pursuing draws dimmed, and the HUD line counts the two separately.
An ANCHORED net is drawn where it actually is, not where the file says: each net is one
`Node3D` of authored-space children, so `TrailerOffsetOf` (the session's `NetTrailerTargets`) is
applied per frame as that root's `Position` and nothing is rebuilt. Without the supplier every net
draws at its authored coordinates.

## src/UI/ClassOverlay.cs
The colour-by-class overlay (key X, `--debug-classoverlay` scripts it) — same mode set as
`ColliderOverlay` (`--freecam`/`--anim-lab`/`--fly`/`--stunt`), a findable-targets view rather than a
collision one. Mixes a class colour over every drawn mesh at 50 % (`TintStrength`), so a
target stays recognisable as itself: destructible (red, via `DestructibleRegistry.Resolve` — the
exact climb a weapon hit takes), facade (pink, via `SceneBuilder.ClassifyBillboard` on the source
`GameZMesh`, resolved back through the built node's `AnimRuntime.IndexMeta`), clutter (green, every
`MultiMeshInstance3D` under the world root — nothing else in this codebase parents one there),
everything else scenery (blue). Rebuilt on every X press rather than cached, clearing each tinted
node's `csky_tint` first. Deliberately keyed on neither surface tag: not the texture-derived
`SceneBuilder.SurfaceMeta` (decides only which collider a mesh's polygons join) and not
`SurfaceIdMeta` (answers "what happens when you touch this", the collider overlay's key) — two
unrelated objects can share either tag, and neither answers "what is this object".

## src/UI/NodeLab.cs
The node lab (N) in `--freecam`/`--anim-lab`: the world's `cs_name` tree, a search box, per-node
Frame / Hide-Show, a dependency readout for `SelectionService.Current` (anim defs, destructible
pool + DAMAGE_SEQUENCE, geometry/textures, colliders) and a destructibles view with coverage
columns, plus `ExtraRoots` top-level branches for props beside the world content (the anim lab's
`--plane=` prop, so its parts show in the tree, search and `SelectByName`).
`--debug-nodelab[=deps,dest,open,node=<cs_name>]` is the scripted twin. A row's text/colour
follow live `Node3D.Visible`, re-read on the panel's 4 Hz status cadence rather than latched off the
hide button, so a def re-showing a hidden node reads visible again on its own.

## src/UI/WorldDamageLab.cs
The world damage lab (F5) in `--freecam`/`--anim-lab`: the destructible pools of whatever
`SelectionService` holds, each with live HP, and a slider + Kill + Reset on the one a weapon hit
reaches, driving `AnimRuntime.DamageAt`/`ResetDestructible`. `--debug-damage[=node=,pool=,hp=,kill,
reset,tick=,open]` is the scripted twin (an ordered script, not a token set). Only the pool
`DestructibleRegistry.Resolve` names is drivable — a node can carry several `(def, anchor)` pools
(C1's water tower: compiled + reader wildcard) — and the rest are listed read-only with the reason,
since driving a twin would damage a pool nothing can ever hit.

## src/UI/OrbitCamera.cs
The static inspection view's orbit-camera controller (LMB-drag orbit, wheel zoom, AABB framing):
owns the orbit state and drives a `Camera3D` it does not own; `Frame` takes the eye + pivot the host
resolved, and `MergedAabb(Node3D)` merges a subtree's world-space mesh AABBs (shared with the anim
lab). `Frame`'s `lookAt` is a pivot point, not a direction — with the eye it also sets the orbit
radius the wheel and the drag then work in; `GameSession.FrameCamera` synthesizes a pivot on the
aim ray before calling in, since collapsing that back to a direction would leave the camera
spinning about its own eye.

## src/UI/AnimLab.cs
The `--anim-lab` debugger: a quiet `WorldSession` stage (`AutoStart=false`, seed pinned), fixed-dt
clock, transport button panel, def picker, `AnimTimeline`, `SpectatorCamera` freecam following the
shared selection, and a staged effect/crash anchor set so placeless on-call defs play at the camera.
Interactive (FixedAccum) frames draw each live transform-motion target interpolated between its
last two SIM poses (`GameClock.StepFraction`); sim poses are restored before any step runs, so
render smoothing never leaks into event held-pose seeding and FixedStep stays byte-identical.
Puffer particle spread is unseeded RNG (DIAG-21, docs/verification.md): same-step shots differ in
particle noise alone.
⚠ The picker toggle is `F18`, not `F` (`BL-428`). The shared `SpectatorCamera` owns `F` as its
target key, and this lab's `SetInputAsHandled` cannot protect a camera that polls raw key state,
so the two are separated by binding rather than by ordering.

## src/UI/AnimTimeline.cs
The anim lab's authored-vs-fired timeline (custom-drawn `Control`): authored blocks above, fired
ticks below, one lane per Initial sequence; a slanted first-firing connector = scheduler divergence.
`BuildLane` deliberately re-derives the documented scheduling rule independently rather than
calling the runtime's `SequenceRunner` — that independence is the whole instrument.

## src/UI/Menu/PresentationId.cs
The identity a menu presentation registers under and Options persist: a value token wrapping a
non-empty string, compared ordinally, with `BuiltIn` (`built-in`) and `Original` (`original`) as
the shipped identities. The options store keeps the persisted value as a validated string and
resolves it against `PresentationRegistry.Registered`; an unknown token falls back rather than
throwing. Off-engine coverage: `CSVM.Tests/MenuSeamContractTests.cs`.

## src/UI/Menu/IMenuPresentation.cs
One menu presentation: a screen graph plus its navigation, interaction, animation and cue
selection over the shared features. `Activate(host, destination)` shows it at whatever screen of
its own graph the semantic destination maps to, `Tick` drives it over the host's seats, and
`Hide` takes it off screen after the host consumed its exit with its state kept, and
`Deactivate` tears it down for good. A flight is not a switch: the host re-calls `Activate` on the
same instance with the return's destination, so cursors survive a flight. The host creates a
fresh instance per switch, so a switch discards transient presentation state by construction and
always lands on `MenuReturnDestination.TopLevel`. The Built-in presentation is
`src/UI/Menu/BuiltIn/BuiltInPresentation.cs`; the host is `src/UI/Menu/MenuHost.cs`. The whole
seam read end to end, with the checklist a further presentation follows, is
[`docs/menu-presentations.md`](menu-presentations.md).

## src/UI/Menu/PresentationRegistry.cs
Where presentations register: one factory per `PresentationId`, filled once at startup, duplicate
registration refused. `TryCreate` hands out a fresh instance and answers an unknown id with
false, so a stale persisted token degrades to the Built-in fallback instead of a throw.
Availability (the Original presentation's asset manifest) is decided before asking here; the
registry only says what exists in the build.

## src/UI/Menu/IMenuHost.cs
What the process-lifetime menu host lends the active presentation: `Features`
(`MenuFeatureSet`), `Audio` (`IMenuAudio`), `Seats` (one `IMenuInputSource` each, a live list the
join flow grows: the `PlayerSetupFeature`'s `Sources` once that feature is registered), and
`Exit(MenuExit)`, the only way out. The host owns all four across
presentation switches; a presentation borrows them between `Activate` and `Deactivate` and keeps
no reference past that. The implementation is `MenuHost` below.

## src/UI/Menu/MenuHost.cs
The process-lifetime host, engine-free: `Launcher` owns one. Constructed over a
`PresentationRegistry`, an `IMenuAudio` and an exit sink; the owner adds features (`Features.Add`)
and seats (`AddSeat`/`RemoveSeat`). With a `PlayerSetupFeature` registered, `Seats` is that
feature's live source list and `AddSeat` joins through it (a refused join throws: every seat
taken, or the source already seated), so the feature must be added before seat 0 and a join made
anywhere shows in `Seats`; without one the host keeps its own list, which is what the seam
fixtures use. `Select(forceBuiltIn, cliOverride, savedRequest)` settles
`Selected` through `PresentationResolution.Resolve` with registration plus the owner's
`Availability` delegate as availability (a registered presentation whose assets are missing answers
with a reason, which is appended to the fallback reason), keeps the pre-availability `Requested`
for Options to show back, returns the fallback reason or null, reads a blank or unknown request as
unavailable rather than throwing, and throws only when the resolved id (Built-in) is not
registered, a wiring error. `Show(destination)` creates a fresh instance of
`Selected` on the first call and re-activates the same instance on every later one, so a return
from flight lands on the screens as they were left; `Tick` runs the presentation only while
`Shown`; `Exit` clears `Shown`, hides the presentation and hands the exit to the sink;
`Deactivate` ends the instance and calls `Features.DiscardTransient`, the first half of a switch.
`Shown` is what the owner reads for "the menu is up". Off-engine coverage:
`CSVM.Tests/MenuHostTests.cs` over the seam fixtures; in-engine, `menu-host-tracer`.

## src/UI/Menu/BuiltIn/BuiltInPresentation.cs
The Built-in presentation (`CSVM.UI.Menu.BuiltIn`): `LaunchMenu` registered under
`PresentationId.BuiltIn`. Constructed with the parent node, the data paths, the `--menu=` aid and
the raw `MenuInput` behind the host's first seat. `Activate` builds the launchscreen under the
parent on the first call (switching its own process callback off), calls `ShowMenu(aid)` with the
aid on that first call and with "" on every later one (the aid is consumed, so a return from flight
lands on Mode with the cursors kept, as `menu-launch-return` pins), then maps the destination:
`TopLevelReturn` is the Mode screen, `CabinReturn` opens the profile's cabin, `DebriefReturn` the
scrapbook on the flown mission. `Tick` runs the menu's frame; `Hide` is `HideMenu`, called by the
host alone; `Deactivate` removes and frees the node. `Menu` exposes the launchscreen for what is
Built-in's alone (the launcher's one-shot debug aids and its failed-build note).

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
(auto-repeated cursor steps, edge presses, typed text, an optional window-pixel `MenuPointer`),
and `IMenuInputSource` is the per-seat producer (`Poll`/`Prime`/`CapturingText`). A source is not
synonymous with a pad: keyboard-plus-unclaimed-pads, one claimed pad, a mouse or a future
HOTAS/HOSAS binding all sit behind the same contract, and a presentation never reads a device.
`MenuInput` stays the raw pad-side poller; `BuiltInSeat` adapts it onto this seam for seat 0 and
for every seat a pad joins on, `PointerSeat` adds the mouse, `MenuIdleSource` stands for a seat
with no device, and the seats themselves are the `PlayerSetupFeature`'s, claimed by source
identity.

## src/UI/Menu/IMenuAudio.cs
The shared menu audio contract: a presentation requests a `MenuCue` by semantic name and starts
or stops narration at moments it owns; the service owns resolution, playback, volume and the
handoff into a launching session. The host implementation is `MenuAudioService`
(`src/Session/MenuAudioService.cs`); Built-in's one call site is the briefing narration.

## src/UI/Menu/MenuExit.cs
The one typed menu exit, handed to `IMenuHost.Exit` and consumed by `Launcher`: `LaunchExit`
(chapter, per-seat `MenuSeatChoice`, `MenuMode`, optional `InstantActionDef`),
`CampaignMissionExit` (profile, `cm_sequence` position, per-seat choices), `QuitExit` and
`OptionsApplyExit` (the `PresentationId` and the graphics-mode word an Options screen applied; the
consumer persists both, ends the active presentation and shows the selected one at its top level).
Both values ride the exit rather than being saved by the screen that took them, so the options file
keeps exactly one writer and no presentation driven through Apply can write the player's own. A
custom plane arrives as its resolved `CustomPlaneDef`, never a store name, so the consumer reads no
store. Presentations never construct sessions. `LaunchMenu` produces the first three and the apply,
`OriginalShell` the launch, the campaign launch, the quit and the apply, and `Launcher.OnMenuExit`
consumes them all; the return side is `MenuReturnDestination` alone, the `--menu=` aid reaching
only the cold start. `CSVM.Tests/MenuNamespaceDependencyTests.cs` scans the compiled metadata so
nothing under `CSVM.UI` names `GameSession`, `Launcher` or `LauncherContext`; `menu-launch-return`
(`src/Testing/MenuLaunchReturnSuites.cs`) drives every launch and every return through a real
`MenuHost` in both presentations.

## src/UI/Menu/MenuLayout.cs
The runtime reader of `extracted/rof/menu_layout.json`, the decoded menu layout `ExtractRof.ps1`
emits (`docs/formats/menu-layout.md`), engine-free in the shared namespace. `TryLoad(path, out
reason)` answers a missing or unreadable file with null and a reason, never an empty layout;
`Parse(json)` builds the typed model: `WidgetTypes` (the artifact's own per-type field table with
each field's `kind`), `Globals` (the file-wide macros, `Global`/`GlobalColor` by name), `Screens`
(`Screen(section)`, case-insensitive), each with its `Widgets` (`Widget(key)`, case-insensitive
since the scripts lowercase the layout's capitals), `Navigation` (the `ScriptToExe` edges),
`ExternalAssets` (script-named files and fragments with `Present`) and `MissingArt`. A
`MenuLayoutWidget` keeps every field's resolved string (`Field`), the raw macro token where one was
substituted (`Authored`), the decoder's derived readings (`Art`, `ResIdSymbol`, `ResId`, `Text`,
`TextSource`, `NavigateTo`, `Frames`) and typed accessors (`TryInt`/`Int`, `Bool`, `TryColor`)
that consult the type table's kind and refuse a field of another kind, so the reader carries no
field table of its own. `PathUnder(dataRoot)` is where the artifact sits. The reader is the first
consumer of the artifact, which is why the extraction stamp is schema 2. Off-engine coverage:
`CSVM.Tests/MenuLayoutReaderTests.cs`, which reads the artifact the decoder emits from the probe
fixtures, plus the install's own census under `[ExtractedDataFact]`.

## src/UI/Menu/HangarFeature.cs
The hangar as a shared feature (`CSVM.UI.Menu`, engine-free, in the host's feature set), the
rules and the store operations both presentations walk: one build at a time, opened by
`Open(store, wallet)` over a `CustomPlaneStore` and an optional `IHangarWallet` (the interface
`CampaignWallet` implements: funds, affordability, airframe availability, the special and
sellable answers, the owned builds, purchase and sale), with `Saved` the roster a presentation
offers (the store's planes wallet-free, the wallet's owned planes over one) and `IsNameTaken` over
the whole build directory either way. The scratch plane has three starts: `StartNewPlane` (bare,
nothing chosen), `StartDefaultPlane` (the `DefaultAirframe`, the Devastator, with its stock engine,
guns, hardpoints and armour loaded and chosen, what the name screen's Load Default Configuration
box means) and `StartFromSaved` (a copy through the store's canonical serialisation). `PickAirframe`
switches, snaps the paint pattern onto one the airframe may wear and raises the defaults ask
(`DefaultsAsk`, `DefaultsAskText` from langui 206, `AnswerDefaultsAsk` loading through
`LoadAirframeDefaults`: engine 1, `LoadStockWeapons` off the stock fit read on first need, the
stock armour off the zrdr scope), and returns false on the standing pick so a presentation can
advance instead. The per-tab operations clamp and report change: `SetEngine`, `SetArmour` (zone,
units), `SetGun` over the eleven-row `GunCycleRows` (`GunCycleIndex`/`GunOfCycle`),
`SetHardpoints`, `SetPattern` over `WearablePatterns`, `SetColour` (resetting the shade),
`SetShade`, `SetDecal`. `Refusal` is the gate in the original's words (203, 1182 with 1227 or
1171, then over a wallet availability and 1226), `CanCommit` its answer, `Bill` the priced plane;
`Commit` saves and then pays the wallet; `DeleteSaved` deletes, or sells through the wallet with
its two refusals (704 composed by `CannotSellText`, 701). The labels every screen writes come from
here too (`AirframeName`, `AirframeShortName`, `EngineName`, `GunName`, `GunCycleName`,
`ArmourLabel`, `HardpointsLabel`, `PatternLabel`, `TotalsLine`), as do the name rules
(`AcceptsNameChar`, `MaxNameLength`) and `Overwrites`. `Discard` drops the build, its store and
wallet, the roster and the messages, and touches nothing saved; the presentation switch calls it
through the feature set, and both presentations' cancels call it directly. Off-engine coverage:
`CSVM.Tests/HangarFeatureTests.cs` with a fake wallet; the same rules reach Built-in through
`HangarFlow` and Original through `OriginalHangar.cs`.

## src/UI/Menu/CampaignFeature.cs
The campaign as a shared feature (`CSVM.UI.Menu`, engine-free, in the host's feature set): the
state and the operations both presentations read and write, with none of Built-in's board shell in
it. `Open(store, planes, stock, dataRoot)` opens a campaign over a `CampaignProfileStore` (the
build store, the stock table and the data root optional, each degrading to absent art, stock fits
and no mission data), reading `Roster` once; `Discard` drops all of it and touches no file. The
roster operations are the original's own: `ContinuePlayer(name)` seats the named player, creating
a fresh `CampaignProfileDef.NewProfile` when it is new, and returns the refusal in the original's
words (200 empty, 707 the character rule, 212 the length, 202 the 24-slot roster) or null;
`DeletePlayer(name)` removes that profile's own directory and nothing else; `SelectProfile` and
`SeatProfile(name)` (the flight returns' door, re-reading the store) record the last-played
player; `Resume` re-reads the seated profile after the hangar. The mission the screens after the
cabin are about is `MissionSeq` (`SetMission`), with `Mission` the `cm_sequence` entry read once
per mission, `MissionHasWingman`, `NextMissionSeq`, `CampaignComplete` and `ChangePlaneAllowed`
(the two `FLIGHTCHECK.SCRIPT` rules: barred on the two story-grant missions and under three
planes). `Briefing` is the mission's `CampaignBriefing` (below), loaded once per mission and kept
with its reveal's progress. The intents between screens are `AmmoSlot`, `PlaneSlot`,
`ScrapbookEntry` (`EnterScrapbook(seq)` counts every opening so a re-entry lands on spread 1) and
`ZoomTarget`; the writes are `CommitLoadout(plane, ammo, ordnance)` (saving the profile for the
seated player's aircraft and nothing for a guest's session-scoped record), `CommitPlanes(pilot,
wingman)`, `ExportPlane(plane)` (into the build store only, refusing a stock record) and
`BuildExit(padsPerPlayer)`, which saves the profile and returns the `CampaignMissionExit` with one
seat per joined human. `SeatedPairClashes` is the plane selection's permit test by name;
`AmmoTarget` resolves whose record the ammo screen edits; `Wallet()` is the seated profile as a
`CampaignWallet` for `HangarFeature.Open`; `Field` is the sortie's `CampaignFlightField`;
`CapturePath(fileName)` resolves a scrapbook capture in the profile's directory; `ValidName` and
`AcceptsNameChar` are the name rule `CampaignTextEntry` forwards to. Not here, by classification:
the screen stack and its return-to-open-screen rule, the cursor and its settle over unfocusable
rows, the refusal line, the modal, the ammo and plane screens' working copies before ACCEPT, and
the briefing's clock, all of which are how a presentation offers the operations. Off-engine
coverage: `CSVM.Tests/CampaignFeatureTests.cs` pins every write's file contents over a scratch
store; the same operations reach Built-in through `CampaignFlow` and its pages
(`menu-campaign-journey`).

## src/UI/Menu/CampaignBriefing.cs
One mission's briefing as the campaign feature holds it: the `cm_sequence` entry, the
`BriefingState` the `brief_c<campaign><mission>` formula names, the narration wav its sound
resolves to, the `BriefingObjectives` note, the `Messages` table the buttons are labelled from,
and the running `BriefingReveal`, whose progress is feature state (`NarrationStarts`, `Complete`).
`Load(dataRoot, seq)` reads everything once and degrades to a briefing with no state on a broken or
absent extraction; `Advance(seconds)` and `Restart` are the presentation's, called on its own clock
and on REPLAY BRIEFING, and when to draw stays the presentation's business. `BriefingScript.cs`
(`BriefingDialog`, `BriefingState`, `BriefingStep`, `BriefingReveal`, `BriefingElement`) and
`BriefingObjectives.cs` live beside it in `CSVM.UI.Menu`, since a reveal is authored campaign data
both presentations run identically, not a drawing.

## src/UI/Menu/CampaignWallet.cs
The seated campaign profile as the hangar's `IHangarWallet`, built by `CampaignFeature.Wallet()`
for the cabin's Plane Construction and null on every wallet-free door: `Funds`, `CanAfford`,
`IsAirframeAvailable` (the stat table's threshold against `MissionsCompleted + 1`, the comparison
`FUN_00410120` makes), `IsSpecial` and `CanSell` (a reward aircraft is refused, and at least two
planes remain), `OwnedBuilds` (each ownership record resolved to its stored build, else the award
template, else the campaign's starting-Devastator spec), `SellPrice` (the full build cost, no
depreciation), `Purchase` (funds deducted, ownership recorded once per name, the profile saved) and
`Sell` (funds credited, the record removed, the profile saved, the build deleted). Off-engine
coverage: `CSVM.Tests/CampaignWalletTests.cs` through `HangarFlow`, and the purchase and sale
writes in `CSVM.Tests/CampaignFeatureTests.cs`.

## src/UI/Menu/CampaignAidProfiles.cs
The scratch profile store the campaign screenshot aids read, in the shared namespace so both
presentations' aids seat one player: `%TEMP%\CSVM\menu-aid-profiles`, emptied on every open,
seeded with `Zachary` and `Nathan` when asked, the first progressed through the campaign's first
three missions with every objective bit set (the story scraps are gated on the objectives that
unlock them, so a bit-0-only run would leave every scrapbook page blank). `LaunchMenu`'s
`campaign-*` aids and `OriginalPresentation`'s read it; nothing here can reach `user://Profiles`.

## src/UI/Menu/Original/OriginalShell.cs
The Original presentation's screen graph (`CSVM.UI.Menu.Original`), engine-free over
`MenuLayout`, the shared `FreeFlightFeature`, `PlayerSetupFeature`, `InstantActionFeature`,
`HangarFeature` (with the saved-plane store its door opens over) and `CampaignFeature` (with the
profile store, stock table and data root its door opens over), an injected art measurer (the
layout carries no pixel sizes; the presentation reads them off the strips) and an injected
flight-devices answer for the seat choices. The screens (`OriginalScreen`): the top level,
composed from `[MainMenu]`'s `MM_LOGO` and `BFRAME` panes and its six `B` rows at their authored
corners with their four-frame strips (disabled, normal, rollover, depressed) plus the remake-only
Free Flight, Dogfight and BUILD PLANE doors, text buttons in the paper-plaque convention beside
the frame; the two remake-only sortie screens, Free Flight and Dogfight (`OriginalSeats.cs`, one
`partial`); the Options screen, which `MM_B_PREFERENCES` opens, composed over `[Preferences]`'s own
chrome (`PF_LOGO`, `PF_BACKGROUND`, `PF_T_TITLE`, the four description rows in their authored
colour, `PreferencesInks`) with the four page doors (`PreferencesPageKeys`) at their corners, the
first live onto the Game Options page and the other three drawn disabled since no shared option
stands behind them, and the section's own `PF_B_MAINMENU` (`OptionsBackKey`) as the way back; the
Game Options page in its own partial file (`OriginalGameOptions.cs`, below), which that live door
opens; the decoded Instant Action
screen, in its own partial file (`OriginalInstantAction.cs`, below), which `MM_B_INSTANTACTION`
opens; the campaign's nine screens (the profile screen, the cabin, the table of contents, the
briefing, the flight check, ammo selection, plane selection, the book and a scrap's zoom) in their
own partial file (`OriginalCampaign.cs`, below), which `MM_B_CAMPAIGN` opens; and the hangar's
nine screens (the name screen, the hub with one of six tabs, the totals page, the inventory) in
their own partial file (`OriginalHangar.cs`, below), which the BUILD PLANE door and the cabin's
PLANE CONSTRUCTION open. The enum keeps the campaign's members together and the hangar's last,
which is what the two branches are read off. Multiplayer and Credits draw frame 0 and take no
input: the first is network play with no local counterpart, the second is out of scope. Quit
leaves as a `QuitExit` on the press with no confirm, `MAINMENU.SCRIPT`'s own `terminate`. The
messagebox is one idiom over every screen (`Dialog`, raised by the campaign partial's
`RaiseDialog`): while one stands its answers are the only rows, drawn at `[MessageBox]`'s own
button rows through `CampaignBoards.Dialog`, in the words `MESSAGEBOX.SCRIPT` gives them (langui
100 OK on the one-button box, 102 Yes and 103 No on the two-button pair), a box opening on its
first answer as `MESSAGEBOX.SCRIPT` focuses its left button, `Back` taking the declining answer,
and every refusal and confirm goes through it:
the profile screen's refusals and delete, the plane screen's and zoom's messages, and the
inventory's Sell (the sell path's own langui 700 question, then 701 or 704 as a refusal). The text
button convention is read off `FlightCheck.FC_B_CHANGEPLANE` (its paper strip and its four label
colours); the list and heading inks are the file-wide `DISABLED`/`ACTIVE` colours (`Inks`).
`Step(MenuCommands)` applies seat 0's frame: typed characters and Backspace feed the edit box
showing, the name screen's or the profile screen's (`CapturingText` says when), each taken
character cueing `menu.text` and each refused one `menu.text-error`; a pointer (already in
authored pixels) over a live, visible row takes the focus and, on a button, cues `menu.rollover`
once (an open campaign list's entry takes the list's highlight instead); a click on a live row
activates it (a click on nothing closes an open list); `MoveY` walks the enabled rows of the
focused column with wrap (visible or not), or an open campaign list's entries; `MoveX` crosses to
the nearest row of the next column, except on an Instant Action dropdown or radio, where it steps
the value, in the hangar, where it steps a dropdown or walks the tab bar, and on a campaign field,
where it steps the pick; `Accept` activates the focused row (a button cues `menu.click`);
`Back` on a sortie screen first undoes seat 0's pick a stage at a time, on the Instant Action
screen closes an open list, in the hangar closes a list or the ask, returns from the totals page
or the inventory to the tab, else cancels the build, in the campaign walks its own graph back
(below), then leaves for the top level, and quits from the top level. `StepSeat(index, frame)` is
a later seat's frame, which on the campaign's flight check drives that seat's own check.
The Options screen over `[@Preferences@]` is the four page doors drawn disabled, two paper plaques
side by side in the slot under them (the presentation chooser, then the graphics chooser), APPLY a
plaque's height below them and the section's own RETURN TO MAIN MENU; two description lines above
the plaques name them in that order and say the graphics choice takes effect on the next start.
The graphics plaque opens on the saved word, read through the optional options reader the
presentation hands the shell, and APPLY leaves as an `OptionsApplyExit` carrying both choices; the
shell itself writes nothing. `Compose()` is the screen as a
`ComposedBoard` with the pointer as the last overlay (the active pointer bitmap over a live row,
the passive one elsewhere), skipping rows outside their window; `ReturnToTopLevel` (every return
and cold start) keeps the list cursors, resets every seat's pick through the setup and closes an
open campaign. Not
decoded, so recorded as remake-only design: the doors' placement, the sortie screens, the Options
chooser's placement and words and the page doors' disabled state, keyboard and pad focus (the
original is pointer-driven), list rows taking focus under the pointer without a cue, and the
pointer's hotspot at its top-left. Off-engine coverage:
`CSVM.Tests/OriginalShellTests.cs`, `OriginalSeatsTests.cs`, `OriginalInstantActionTests.cs`,
`OriginalHangarTests.cs` and `OriginalCampaignTests.cs` over the invented
`fixtures/menu-layout-original` layout, and `OriginalCoverageTests.cs`, the inventory's machine
check: every screen reached from the top level and left back to it by pointer, keyboard and pad
with nothing left open, every in-scope `ScriptToExe` edge driven, drawn disabled or recorded out of
scope, and every exit typed, once over the fixture and once over the install's own layout
(`docs/org/menu-inventory.md`, Coverage).

## src/UI/Menu/Original/OriginalGameOptions.cs
The Game Options page, the shell's partial over the decoded `[@GameOptions@]` section;
`OpenGameOptions` reads the saved options through the shell's injected reader and opens the page on
its first row. Its content is a table (`GameOptions`): per option a key, a title, a description, the
control kind, the store field's words and how that field is read and written, so a further option is
one entry plus its field. The two shipped rows are the presentation as a dropdown over the two
registered tokens and the graphics mode as a checkbox from the section's eight-state strip; the
third authored row is left empty. `ReadGameOptionsPage` takes the row shape off the section's own
widgets (title column, first row's line and pitch, dropdown box, the checkbox's offset from its row,
description column), so a layout that moves a row moves ours. `GO_B_ACCEPTCHANGES` leaves as the
`OptionsApplyExit` carrying both choices; `GO_B_CANCELCHANGES` and Back drop the edits for
Preferences, Back closing an open list first. A screen never writes the store,
`Launcher.ApplyOptions` does. The words and control kinds are remake-only readings, in
`docs/org/menu-inventory.md`.

## src/UI/Menu/Original/OriginalSeats.cs
The shell's two sortie screens over the shared player setup, the other half of the `partial`.
Rows: the chapter column (`OriginalRosters.Chapters`, all eight for both modes) and BACK in
column 0; in column 1 the aircraft column, keyed `AIRFRAME:<index>` over the setup's roster (the
stock airframes, then the saved customs), and FLY. The aircraft column is a window of
`AirframeWindow` (11) rows: every row keeps its place in the column for the keyboard, rows outside
the window are `Visible == false` (undrawn, unhit), and the window slides so the focused row is
inside it, with scroll marks over and under it. Seat 0 picks a chapter (`PickedChapter` for Free
Flight, handed to the feature; `PickedDogfightChapter` for Dogfight, the shell's own since Dogfight
has no feature) and an aircraft (`Browse` then `Select`; another row re-picks, the same row again
changes nothing). A later seat's frame (`StepSeat`) walks its own cursor over the roster with
wrap, `Select`s then `Confirm`s on Accept, and on Back undoes a stage or, browsing, unjoins (from
any screen, as a guest may). FLY is enabled once a chapter is picked, seat 0 has selected, every
later seat has confirmed and the mode's minimum of seats is met (two for Dogfight); pressing it
confirms seat 0 and leaves as the Free Flight feature's `LaunchExit` or, for Dogfight, the setup's
`BuildExit(chapter, Versus)`, each seat's choice carrying the devices the injected answer names.
Composed beside the rows: the heading, MAP/AIRCRAFT, the seat strip under the chapters (tag,
`DeviceLabel`, choosing / the aircraft / READY), each later seat's tag at the right edge of its
row (one tick selected, two confirmed), the hint between BACK and FLY naming what is waited for,
and the controls line. Remake-only by design; the original ships no split-screen Dogfight and no
join gesture.

## src/UI/Menu/Original/OriginalInstantAction.cs
The Original Instant Action screen, the shell's partial over the decoded `[@InstantAction@]`
section and the shared `InstantActionFeature`; `OpenInstantAction` confirms the feature's
environment (so the launch's base def is the environment's own) and opens it. Its rows are the
section's widgets keyed by their layout keys: the Table of Contents (`IA_TL_Contents`, one
`ListRow` per visible preset keyed `IA_TL_Contents:<index>` in the row's authored 14-row window,
its `UpArrow`/`DownArrow` strips as the `:up`/`:down` buttons on the list's right edge, live only
while there is more list that way, its `Slider` drawn as the thumb along the track), the dropdowns
(`Dropdown` rows at their authored line, `Width` wide and `ItemHeight` high, labelled with the
picked value and carrying the `DropDown` arrow strip: the player plane over the eleven stock
airframes, the wingman count 0 to 5, the wingman plane hidden at zero wingmen, the mission type
over the environment's filtered roster, the environment with the clouds unpickable under stunt
flying, and per wave the enemy count 0 to 6, the militia, the skill and the militia's aircraft),
the enemy pages (`EnemyPage` 0 shows the pilot, wingmen, mission and environment lines with the
first wave's four fields and `IA_B_DOWN`; page 1 shows waves two to four and `IA_B_UP`, the two
pages sharing the right column's authored lines exactly as the layout authors them), the
`IA_B_PLAYER`/`IA_B_WINGMAN` radio pair (`Radio` rows from the eight-state strip, four unmarked
states then four marked, recording `LoadoutTarget`), and the buttons: `IA_B_VIEW` (View Story
writes the applied preset's name into `IA_T_STORYTITLE`, the decoded one-time format),
`IA_B_FLY` (the feature's `BuildExit` for seat 0 on the feature's player airframe, no pads, no
fit), `IA_B_Exit` (back to the top level), and `IA_B_BUILD` and `IA_B_CHANGEWEAPONS` drawn
disabled until the Original hangar and loadout screens exist. A contents row's activation applies
its preset (the list's own select callback) and re-confirms the environment; the ace duel hides
every enemy control, the text rows beside them and both paging buttons. Accept or a click on a
dropdown opens its list: the rows become the list's items alone (`<key>:<index>`, drawn as an
overlay panel under the box), Up/Down walk them, Accept or a click picks and closes, a sideways
step on the closed box picks the next allowed value, Back or a click off the list closes it.
`Compose` draws `IA_BackGround` as the backdrop, the `T` rows at their authored positions in their
authored justification (white rows as the dialog ink, the rest as the screen's text ink), the
contents rows' picked and focused states as the list widget's own fill and frame, and the strips
in their state frames. `InstantActionInks` is the screen's own colour reading (the text rows'
`Color`, the paper buttons' label tail), which `OriginalPresentation.PaletteFor` turns into the
palette this screen alone draws with. Decoded and bound: the option sets, the paged enemy rows,
the ace hiding, the wingman plane hiding at zero, the militia resetting its aircraft, the clouds
barring stunt flying, the preset applied on select, the title on View Story. Remake-only until
`CAP-50` is filmed: both halves shown together, the open list under its box and its row count,
keyboard and pad stepping, the radio pair with no wingmen, the paging buttons hiding under the
ace duel, and the two disabled buttons. Off-engine coverage:
`CSVM.Tests/OriginalInstantActionTests.cs` over the fixture's `[@InstantAction@]` section;
in-engine, `menu-original-instant-action` (`src/Testing/MenuInstantActionSuites.cs`) over the
install's own layout.

## src/UI/Menu/Original/OriginalHangar.cs
The Original hangar, the shell's partial over the shared `HangarFeature` and the decoded hangar
sections. The top level's BUILD PLANE door (remake-only: the original reaches plane construction
from the cabin alone) opens a wallet-free build over the saved-plane store and enters through the
decoded `[@PlaneName@]` screen, as the cabin's `PC_B_PLANEX` edge does: `PN_E_NAME` as a
`TextField` row fed by the seat's typed characters (the feature's character set and cap),
`PN_B_DEFAULT` as a checkbox off the eight-state strip (checked as authored), `PN_B_OK` live once a
name stands (langui 203 shown under the panel until then) and starting the build on the default
configuration or, cleared, on a bare airframe under the typed name, `PN_B_CANCEL` dropping it. OK
opens the Plane Construction hub: `[@PlaneConstruction@]`'s background, the plane at the four
`PX_P_PLANE` panes' corner (the focused airframe's `PX_<n>_BLUEPRINT.TGA` on the airframe tab or
before a pick, else the `PX_ICON_<airframe>_<pattern>_1..3` region masks tinted with the picked
colours through `BoardPicture.Tint` under the `_0` plate; a pair with no set falls back to the
blueprint), PLANE NAME with the name, PLANE COST with the bill's total (the running total, following
every pick), AIRFRAME, WEIGHT CAPACITY, CURRENT WEIGHT (Pending before a pick), the agility and
armour words with the first n of their five `PX_BarGraph` segments, `PX_T_CASHTITLE`/`PX_T_CASH`
over a wallet only, and on the right page one of the six tab sections (`AirFrame`, `Engine`,
`Armor`, `Guns`, `HardPoints`, `Paint`): its `_T_TITLE` in the title colour, its labels and
`PX_Rule` panes, its `D` rows as `Dropdown` rows at their authored boxes labelled with the standing
pick (the colour rows as swatches, the decal rows as tiles off `PT_P_DECALS`), the name line for
the focused item and the `S` scroll-text box carrying the decoded figures. A dropdown's Accept or
click opens its list under the box, every item keyed `<key>:<index>`, the row's `TotalDisplayed`
as the window following the focus with the `DropUp`/`DropDown` strips as `:up`/`:down` arrows;
a sideways step on the closed box picks the next value; a pick binds to the feature (`PickAirframe`,
`SetEngine`, `SetArmour`, `SetGun`, `SetHardpoints`, `SetPattern`, `SetColour`, `SetShade`,
`SetDecal`). A pick that changes the airframe raises the defaults ask as a dialog over the page
(`ASK:OK`/`ASK:CANCEL` plaques on a panel carrying string 206). The tab bar is the layout's seven
`0x1100` edges: the six `PX_Tab` `TextButton`s with the standing tab disabled (drawn in its
`ColorDisabled`), `PX_B_Sell` opening the `[@Hangar@]` INVENTORY (`HA_D_PILOTPLANE` over the
feature's `Saved`, the picked plane's `FC_PlaneIcons` frame, name, agility, armour, value and guns,
`HA_B_SELLP` through `DeleteSaved`, `HA_B_EXPORTP` disabled until the campaign's EXPORT is a
feature operation, `HA_B_DONE` back to the tab), `PX_B_Ready` opening the `[@Purchase@]` totals
page (the column heads, one line per priced component at the authored lines and text lists, the
totals, the problems text in the commit's words, `PUR_B_PURCHASE` live while `CanCommit`) and
`PX_B_Cancel` dropping the build; the wallet-free door wears `PX_B_ReadyToExport`/
`PX_B_CancelExport` when the extraction has them, the strips the Instant Action stills show. A
commit refreshes the shared roster from the store (`OriginalRosters.Roster`), drops the build and
returns to the entry screen; a cancel, Back off the name screen or a tab, and a presentation switch
drop it through the feature's `Discard`. Keyboard and pad: `MoveY` walks the page's rows then the
tabs and buttons, `MoveX` steps a dropdown or walks the bar. `HangarInks` is the screens' own
colour reading (the right page's text and title, the left page's white, the tab label tail), which
`OriginalPresentation.PaletteFor` turns into the hub's palette; the inventory draws in the paper
palette. Decoded and bound: the sections' rows, the tab bar's edges, the strings, the economy and
the paint tables. Remake-only until `CAP-53` is filmed: the door and the wallet-free entry, the
export wording, the standing tab drawn disabled, the open list under its box and its arrows, the
ask as a dialog, where the running total shows, nothing committing before Purchase Now, SELL PLANES
reaching the inventory, keyboard and pad focus, and what Load Default Configuration loads.
Off-engine coverage: `CSVM.Tests/OriginalHangarTests.cs` over the fixture's hangar sections;
in-engine, `menu-original-hangar` (`src/Testing/MenuHangarSuites.cs`) over the install's own
layout and the user's store, under a scratch name it removes.

## src/UI/Menu/Original/OriginalCampaign.cs
The Original campaign, the shell's partial over the shared `CampaignFeature`: the decoded profile
screen (`[@Campaign@]`), cabin (`[@PassengerCabin@]`), table of contents (`[@ScrapBook_TOC@]`),
flight check, ammo selection, plane selection, book (`[@ScrapBook@]`), zoom and the briefing
dialog. The Campaign row (`MM_B_CAMPAIGN`) opens the feature over the profile store, build store,
stock table and data root the shell was given and lands on the profile screen with the name box
pre-filled with the last player seated, `CAMPAIGN.SCRIPT`'s own pre-fill. What each screen draws
is the shared board component: the shell hosts the Built-in campaign pages in a `CampaignFlow` of
its own built over the feature and `CampaignLayout.Over(layout)`, composes through
`CampaignBoards.For(page, focus, pressed, detail, null, layout)` and copies every layer, notes and
strokes included, into its own board. That flow is never walked: `GoTo` mirrors the screen showing
and `FocusRow` the focus, so the pages' own reads of the cursor (the contents' wash, the scrap under
the pointer) follow Original's, and its `Back`, `Move`, `Message` and `Modal` are never Original's
way of doing anything. The screen graph is this file's: the profile screen's box and CONTINUE
start on the name in the box (Enter in the box included), a roster row fills the box and a second
press on the filled row starts (the double-click), DELETE PLAYER asks with langui 201 as the
two-button messagebox opening on YES, CANCEL and Back leave the campaign; the cabin's four plaques
(NEXT MISSION disabled once the campaign is complete, PLANE CONSTRUCTION opening the hangar over
`Wallet()` through `OpenHangar(wallet)` with the cabin as its return, re-read on the way back);
the briefing's three plaques (REPLAY BRIEFING restarting the reveal, whose start count the
presentation turns into narration); the flight check's CHANGE AMMO and CHANGE PLANE naming their
crew slot, RETURN TO BRIEFING rewinding the field, FLY MISSION advancing to the next joined human's
check or leaving as `BuildExit(pads)` with every seat's devices; and Back walking each screen to
the one that opened it (the briefing to the cabin or the contents, the book to the contents or the
cabin) keeping the focus on the plaque that opened what is being left. The pages whose editing
state is their own (the contents' pick and window, the ammo working copy and its fields, the plane
picks, the book's page turns and tabs, the zoom's export) take their press through
`ICampaignPage.Accept`/`Step`/`Back`, and whatever the page named on the way is read off the flow
and re-entered through this graph: a destination becomes a `ShowCampaign`, a `TakeModal` or
`TakeMessage` becomes Original's dialog. Rows: one per page row at the rectangle the board draws it
at (a button's `CampaignBoards.SlotOf` slot with its strip measured, a field's box at
`ComboFieldHeight`, a roster row at `TextSlot` and the layout's item height, a mission row at
`CampaignPreviousMissionsPage.RowBox`, a scrap at its `SCRAPBOOK.CSV` region or its picture's
measured bounds, a capture at the quarter-size 164x123 the script forces), a row the page refuses
focus on disabled, a focusable row with no rectangle (a mission row outside its window) unseen and
unhit but walkable, then an open field's visible entries (`ENTRY:<index>`), which take the list's
highlight under the pointer rather than the focus. A dialog's rows are its answers at
`CampaignBoards.DialogSlot`, composed through `CampaignBoards.Dialog` in the frame and ink under
the pointer, and Back takes the declining answer. The profile screen adds `CAMPAIGN.SCRIPT`'s own
list drawing over the board: the `0xff800000` bar behind the row the box names and the
`0xffff0000` frame around the row under the pointer, the box showing the typed name itself with a
caret while focused. Cues: a plaque, field or dialog answer cues the rollover and the click, a
roster row, mission row or scrap none, the box `menu.text`/`menu.text-error` per character. The
two flight returns come in through `ShowCabin(profile)` and `ShowScrapbook(profile, seq)`
(`SeatProfile` then `EnterScrapbook`); the aids through `OpenCampaignOver(store)`,
`ShowCabin` and `ShowMissionScreen(screen)`; `AdvanceBriefing(seconds)` is the presentation's
clock and `NarrationStarts`/`NarrationWav` its narration reads; `CloseCampaign` discards the
feature and the pages, called by every door out and by `ReturnToTopLevel`. Remake-only until
`CAP-52` is filmed: the cues on the campaign plaques and the silence on list rows and scraps, the
edit box's two sounds, which pointer bitmap shows over a scrap, the narration restarting from the
top on REPLAY and ending on RETURN TO CABIN and GO TO FLIGHT CHECK, a zero region hit-tested on
the picture's bounds, YES and NO as the delete confirm's words, keyboard and pad focus, and Back
returning onto the plaque that opened the screen. Off-engine coverage:
`CSVM.Tests/OriginalCampaignTests.cs` over the fixture's campaign sections and a scratch store;
in-engine, `menu-original-campaign` (`src/Testing/MenuOriginalCampaignSuites.cs`) over the
install's own layout and a scratch store.

## src/UI/Menu/Original/OriginalPresentation.cs
The Original presentation node, registered under `PresentationId.Original`: a `CanvasLayer` on the
board layer holding one `ComposedBoardView`, so every screen scales as the campaign boards do (one
uniform 4:3 fit, centred, letterboxed, nearest-sampled). Constructed with seat 0's `MenuInput`
(for `MenuSeatDevices`) and the `--debug-join` count. `Activate` builds the shell (over the Free
Flight, player-setup, Instant Action, hangar and campaign features, the campaign over the store
`CampaignProfiles` names, `user://Profiles` unless a suite set a scratch one, the stock table and
the data root) and the device bookkeeping on the first call,
refreshes the setup's roster from the saved-plane store on every call (`Roster(customs)`:
`OriginalRosters.Airframes` then the customs through the setup's roster rule, cursors clamped onto
it), stands the shell on the top level and then maps the destination: a `CabinReturn` reopens the
campaign and seats the named profile on the cabin (`ShowCabin`), a `DebriefReturn` seats it and
opens the book on the flown mission (`ShowScrapbook`), either landing on the profile screen with a
logged warning when the profile cannot be read; the first show alone applies the `--menu=` aid,
consumed so every later top-level show is the top level itself (`campaign`, the player's door onto
the profile screen over the presentation's store, `free-flight`, `dogfight`, `instant-action`,
`options`, the hangar's `plane-name`,
`plane-construction`, `plane-paint`, `plane-purchase` and `plane-inventory`, each on a fresh build
named Sample Plane that no aid commits, the campaign aids it shares with Built-in over
`CampaignAidProfiles`' scratch store, `CampaignAids`, the briefing's seconds argument advanced in
frame slices, and its own `campaign-delete`, the two-player profile screen with DELETE PLAYER
pressed so the two-answer messagebox stands), seats the debug players once (device-less, the last one
selected), primes every seat, syncs the pads and hides the OS pointer, since the shell draws the
original's own. `Tick` syncs the pad roster, scans the join gesture while a sortie screen or the
campaign's flight check is up (priming the edges on entering one), advances the briefing's reveal
on the frame and begins the narration through the host's audio whenever the script's start count
rises (entry, REPLAY BRIEFING, a re-entry), repainting while the reveal runs, sets seat 0's
`CapturingText` from the shell before and after
the frame (an edit box's letters are text, not cursor aliases), polls every seat of the host's
live list, maps a window-pixel pointer into the authored space through the view's own `BoardFit`,
steps the shell per seat, requests its cues through the host's audio, hands its exit to the
host, and ends the narration the frame the shell is no longer on the briefing. `Hide` takes the
layer off screen, releases the text capture, ends any narration (a launch from the briefing's
flight check ends the voice with it) and restores the OS pointer;
`Deactivate` frees it. `Measure` is the strip size the layout does not carry, read off the file
once per art name and cached, so a file that does not read leaves the row a fallback rectangle and
logs one `ui` line for that name, which is what an optional file's absence degrades to.
`PaletteFor(inks)` is the shell's inks as a `BoardPalette`; the Options
screen draws with `PaletteFor(preferencesInks, inks)`, the Preferences page's authored text and
title colours with the paper plaque's label tail; the Instant
Action screen and the hangar's inventory draw with `PaletteFor(instantActionInks)`, every text in
the screen's authored black over its paper page and the labels in the paper buttons' tail, since
the top level's white inks would not read on it; the hub draws with `PaletteFor(hangarInks)`, the
right page's black and title colour with the tab bar's white labels and the standing tab's
disabled colour; a campaign screen draws with `BoardPalette.For(screen)`, the palette its shared
board component takes under Built-in. In-engine coverage: `menu-original-tracer`
(`src/Testing/MenuOriginalSuites.cs`), `menu-original-instant-action`, `menu-original-hangar`,
`menu-original-campaign` (`src/Testing/MenuOriginalCampaignSuites.cs`) and the Original half of
`menu-player-setup-seats` over the install's own layout.

## src/UI/Menu/Original/OriginalAvailability.cs
The availability answer Original is selected on: `Load(dataRoot, out reason, out degraded)` refuses
a tree stamped below `OriginalAssetManifest.StampSchema` (`ExtractionStamp.Behind`), reads the
layout through `MenuLayout`, requires a `[MainMenu]` section in it, and then checks the manifest
derived from that layout. Returns the loaded layout when Original can run, else null and the one
reason, which the host appends to its fallback reason; `degraded` is the optional half, for the
caller to log once. `ArtPath` is where a layout art name resolves, used by the presentation's own
size read too. Off-engine coverage: `CSVM.Tests/OriginalManifestTests.cs`.

## src/UI/Menu/Original/OriginalAssetManifest.cs
The versioned required/optional manifest, derived from the decoded layout rather than hand-listed.
`Derive(layout)` classes every art name of the 23 sections Original composes required, minus a
short table of rows it does not draw (the two backdrop movies, the Preferences page's in-flight way
back, the cabin's memento and save rows, the messagebox's multiplayer and About variants); every
other section's art, those rows' art and the media a script names are optional; the five files the
scripts name and Original draws anyway (the two pointers, the font, the two export plaques) are
required. `Check(dataRoot)` reads no bitmap: existence plus the PNG signature and IHDR size for
required entries, existence alone for optional ones, and answers one `OriginalAssetReport` naming
every fault with its section, row and file. `Schema` is bumped when the derivation or the check
changes what Original needs. The classification is reconciled against what the screens actually
draw by `CSVM.Tests/OriginalCoverageTests.cs`, which fails any art it draws that is classed
optional; the required-but-undrawn side is printed, not asserted, since chrome like a scrollbar
only draws on a state the journeys do not reach.

## src/UI/Menu/Original/PointerSeat.cs
Seat 0 with a pointer: wraps the seat that polls the keyboard and the unclaimed pads and adds the
mouse as the frame's `MenuPointer` in window pixels, `Pressed` while the left button is down and
`Clicked` on the press edge; `Prime` reads the button so a click held through a screen change is
not a fresh click. The two device reads are injected delegates, so the seat is engine-free and
`Launcher` supplies the viewport's mouse position and `Input.IsMouseButtonPressed`. Built-in
ignores the pointer; Original maps it into its authored space. Later seats are pads and carry no
pointer; a source that wants one wraps itself the same way.

## src/UI/Menu/MenuReturnDestination.cs
Where the menu stands when it comes back, said semantically: `TopLevel`, `CabinReturn(profile)`,
`DebriefReturn(profile, missionSeq)`. The host names the destination and the active presentation
maps it into its own graph at `Activate`, so no presentation-specific screen id crosses the seam.
A destination names where the player stands and never a store: the two campaign returns name a
profile, and the store it is re-read from is the presentation's own (`user://Profiles`, or the
scratch store a suite sets), the same choice the `--menu=` campaign values make through
`CampaignAidProfiles.PlayerDoor`. The `--menu=` aid is not a destination either; it reaches the
cold start alone, so a return is always one of these three.
The dependency direction for this whole folder: types in the `CSVM.UI.Menu` namespace reference
no `Godot` type and no `CSVM.UI` type outside that exact namespace, enforced by
`CSVM.Tests/MenuNamespaceDependencyTests.cs` over compiled metadata (signatures and method-body
IL alike, via `AssemblyDependencyScan`); presentations live in sub-namespaces
(`CSVM.UI.Menu.BuiltIn`, `.Original`) and may depend on anything. `ComposedBoard` and the other
board types stay presentation-side.

## src/UI/Menu/MenuChapters.cs
The shared chapter roster: the eight chapter worlds as `MenuChapter(Code, DangerZones)` in code
order (C1, C1B, C1C, C2, C2B, C3, C4, C5), `For(mode)` (Stunt Flying only the six with Danger
Zones, every other mode all eight), `Find` and `DangerZonesFor`. The codes are separate terrain
databases, not lighting variants (`docs/formats/spawns.md`); the flag says whether the chapter's
`ia.json` ships a `dzones` list, which is why Stunt Flying withholds the other two. Shared so that
`LaunchMenu`'s Chapter screen, the Free Flight feature and the Instant Action feature read one
roster; `LaunchMenu.Chapters` is this screen's row text zipped over it. Off-engine coverage:
`CSVM.Tests/FreeFlightFeatureTests.cs`, which also checks the roster against
`LaunchMenu.ChapterCodesFor` for all three modes.

## src/UI/Menu/FreeFlightFeature.cs
Free Flight as a shared `IMenuFeature`, the first feature cut out of `LaunchMenu`: `Chapters`
(the roster it offers, all eight), `Chapter` (the pick, null until `SelectChapter(code)`, a code
outside the roster throwing), `Refusal`/`CanLaunch(joinedSeats, confirmedSeats)` (a chapter
picked, at least one seat, every seat confirmed; a lone seat launches), `BuildExit(seats)` (the
typed `LaunchExit` with mode Free and no Instant Action def, refusing a closed gate or a seat with
no plane) and `Discard` (drops the pick). Free Flight is the remake's own mode, so nothing here is
decoded; the rules are the launchscreen's, moved. The seats are the `PlayerSetupFeature`'s: this
feature takes the confirmed `MenuSeatChoice`s that feature builds and never reads a roster, a lock
or a store. Owned by the `MenuHost`'s feature set and read out of it by `LaunchMenu` and
`OriginalShell`. Off-engine coverage: `CSVM.Tests/FreeFlightFeatureTests.cs`, which checks the
gate against `LaunchMenu.CanLaunch(MenuMode.Free, ...)` case by case.

## src/UI/Menu/ControlsFeature.cs
The rebinding screen as a shared `IMenuFeature`, engine-free: which seat's keymap is being edited
(`Player`, one registered `BindingProfile` per seat through `AddSeat`), which of the three contexts
(`Context`), the row and slot cursors (`Focus`, `MoveSlot`), the capture in progress
(`BeginCapture`/`Poll` over the seat's own `IDeviceState`) and the steal it is about to perform.
A capture that lands on a free control binds it; one that lands on a held control raises `Pending`
naming every action that would lose it and moves nothing until `ConfirmSteal`, which is what keeps
the original's conflict rule from happening behind the player's back. `UnbindSlot` drops one
control, `ResetContext` restores the shipped keymap in the very map the polling site holds, and
`Save` writes through the injected per-player store. Editing is scoped to one seat's profile, so
two seats cannot reach each other's bindings. Coverage: `CSVM.Tests/ControlsFeatureTests.cs`.

## src/UI/Menu/PlayerSetupFeature.cs
Player setup as a shared `IMenuFeature`, device-neutral and engine-free. `Seats` are
`PlayerSeat`s in join order, each bound to the `IMenuInputSource` that claimed it (`Join(source)`:
null when the four seats, `MaxSeats`, are taken or the source already holds one, since a claim is
one source, one seat, settled in arrival order; `Unjoin` never removes seat 0 and is a no-op on a
seat already gone; `Sources` is the live source list the host lends as `Seats`; `Revision` bumps
on every join and unjoin). `Roster` is the `MenuAircraft` list every seat picks from, set by the
presentation (`SetRoster`) and built by the shared rule `BuildRoster(stock, customs,
nodeOfAirframe)`: the stock rows in their given order, then one row per saved custom flying its
airframe's stock node with the def on the row. Per seat: `Cursor` (a presentation may park it past
the roster for a row of its own; `Select` refuses such a cursor and `Choices` throws on it),
`Browse` (refused while selected; a moved cursor resets the fit), the two stages `Select` and
`Confirm`, `Back` one stage down (`SeatBack.Unconfirmed`, `Unselected`, or `Browsing` for the
presentation to decide), `OpenLoadout`/`CloseLoadout` (a selected, unconfirmed seat only;
`Confirm` closes it), `ResetPicks(fits)`. The gate: `MinimumSeats(mode)` (two for Dogfight),
`Refusal(mode)`/`CanLaunch(mode)` (every seat confirmed, the minimum met), matching the
launchscreen's static rule. `Choices(flightDevices)` is one `MenuSeatChoice` per seat (node, the
devices the presentation's answer names for the seat, fit or null, custom def); `BuildExit(chapter,
mode, flightDevices)` is the typed exit for a mode with no feature of its own (Dogfight).
`Discard` drops every seat but the first and every stage of its pick, cursor included. Nothing
here reads a pad: a seat's devices are its source's detail, read on the presentation side by
`MenuSeatDevices`. `MenuIdleSource` (`src/UI/Menu/MenuIdleSource.cs`) is the "no device" source
the screenshot aid seats extra players over. Off-engine coverage:
`CSVM.Tests/PlayerSetupFeatureTests.cs`, including the same-frame races (two claims, a lock and
an unjoin, a confirm from a source that has left) and the host lending the seat list; in-engine,
`menu-player-setup-journey` and `menu-player-setup-seats`.

## src/UI/MenuSeatDevices.cs
The pad side of the shared player setup, for any presentation, over seat 0's `MenuInput` and the
feature. `P1Pad` is the pad seat 0 claimed by steering a screen with it (`ClaimP1Pad`, from
`LastActivePad`; the keyboard claims nothing). `Sync` reconciles the seats with `Pads.Connected`:
a seat whose pad vanished is unjoined, a vanished claimed pad frees seat 0, and seat 0's poller is
bound to its claimed pad or to every unclaimed one (never `pads[0]`: phantom devices occupy the
early slots), primed when the set changes. `PrimeJoins` seeds the per-pad Start edges;
`ScanJoins` joins a `BuiltInSeat` over a one-pad poller for each unclaimed pad whose Start is a
fresh edge while a seat is free (the caller decides on which screens joining is open: Built-in's
Plane and Campaign screens, Original's two sortie screens). `PadOf(source)` reads a joined seat's
pad back off its `BuiltInSeat`; `FlightPads(seat)` is the binding a launch carries (seat 0's
poller's set, a pad seat's one device, nothing for a device-less seat), the answer both
presentations hand the feature's `Choices`.

## src/UI/Menu/InstantActionFeature.cs
Instant Action as a shared `IMenuFeature`, owned by the `MenuHost`'s feature set and configured by
both presentations. The option sets are static and decoded (`docs/formats/instant-action.md`):
`Environments` (seven, the launcher's dropdown order, C1C never among them), `AllMissionTypes`
(four, the UI dropdown order) with `MissionTypesFor(chapter)` dropping stunt flying where the
chapter bars it and `EnvironmentAllowed(row, missionKey)` clearing the clouds under stunt flying
(the two sides of `FUN_004103b0`'s mask), `Airframes` (eleven, the langui 3700 order the original
stores an aircraft as an index into, with their nodes), `Militias` (thirteen, each with the
aircraft `FUN_00410420`'s mask allows, in the airframe order), `Skills`, and `Presets` (the
`InstantActionPresets` table). The setup is typed state with semantic operations: `EnvironmentIndex`
with `SelectEnvironment` and `ConfirmEnvironment` (which re-fits `MissionTypeIndex` onto the
environment's roster and loads the chapter's own `IA1/ia.zrd.json` as `BaseDef` through the
injected loader; `ForDataRoot` is the game's loader, falling back to the built-in defaults with a
warning), `MissionTypeIndex`/`MissionType`/`IsAceDuel` with `SelectMissionType`, `Lives` with
`StepLives` (0 unlimited to `MaxLives`), `Waves` (four `InstantActionWaveSetup` cursors) with
`SetWave`, `StepWaveCount`, `StepWaveMilitia`/`SelectWaveMilitia` (a new militia resets the aircraft,
the decoded `AV[BA].QG = 0`), `StepWaveAircraft`/`SelectWaveAircraft` within `WaveAircraft(wave)`,
`StepWaveSkill`/`SelectWaveSkill`, `NumWingmen` with `StepWingmen`/`SetWingmen`, `WingmanPlaneIndex`
with `StepWingmanPlane`/`SelectWingmanPlane` (a changed airframe drops `WingmanFit`, the one fit
every wingman flies, edited in place by a presentation's loadout screen), `PlayerPlaneIndex` with
`SelectPlayerPlane`, and `PresetIndex` with `ApplyPreset` (every field but the lives and the base
def). `Refusal`/`CanLaunch(joined, confirmed)` is the gate (one seat joined, everyone confirmed;
the setup is always complete), `BuildWaves`/`BuildDef(playerPlane)` hand the fields to
`InstantAction.BuildFromWizard` over `BaseDef` (the built-in defaults when no environment was
confirmed, which only an aid that skips the confirm reaches), and `BuildExit(seats, playerPlane)`
is the typed `LaunchExit` (the environment's chapter, mode Stunt, the def), refusing a closed gate
or a seat with no plane. `WaveFor` is the one wave build rule (the empty wave at 0 enemies whatever
the cursors). `Discard` puts every field back to the screen's opening state (the first environment,
the ace duel, one life, empty waves, no wingmen, the first airframe, no preset, no base def). The
seats stay the presentation's: Built-in passes its joined seats and player 1's nominal stock name,
Original passes seat 0 on `PlayerPlane`'s node. Off-engine coverage:
`CSVM.Tests/InstantActionFeatureTests.cs`, which also checks `LaunchMenu`'s public rosters against
the feature's; the characterization of Built-in's journey over it is `menu-instant-action-journey`
(`src/Testing/MenuInstantActionSuites.cs`).

