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
call and maps the return destination onto it, the top level being the Mode screen, `CabinReturn`
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
(auto-repeated cursor steps, edge presses, typed text, an optional window-pixel `MenuPointer`),
and `IMenuInputSource` is the per-seat producer (`Poll`/`Prime`/`CapturingText`). A source is not
synonymous with a pad: keyboard-plus-unclaimed-pads, one claimed pad, a mouse or a future
HOTAS/HOSAS binding all sit behind this contract, and a presentation never reads a device. The
implementations are `BuiltIn/BuiltInSeat.cs`, `Original/PointerSeat.cs` and `MenuIdleSource.cs`;
the seats themselves are the `PlayerSetupFeature`'s, claimed by source identity.

## src/UI/Menu/MenuIdleSource.cs
An `IMenuInputSource` with no device behind it: labelled "no device" and idle every frame. The
screenshot aid that seats extra players on a one-controller machine joins one per seat, so the
shot is deterministic and every seat still has a source to claim; each instance is its own claim,
since a claim is an identity. Read `MenuCommands.cs` for the seam and `PlayerSetupFeature.cs` for
the seats it is claimed by.

## src/UI/Menu/IMenuAudio.cs
The shared menu audio contract: a presentation requests a `MenuCue` by semantic name and starts
or stops narration at moments it owns; the service owns resolution, playback, volume and the
handoff into a launching session. The host implementation is `MenuAudioService`
(`src/Session/MenuAudioService.cs`); Built-in's one call site is the briefing narration.

## src/UI/Menu/MenuExit.cs
The one typed way out of the menu, handed to `IMenuHost.Exit` and consumed by `Launcher`:
`LaunchExit` (chapter, per-seat `MenuSeatChoice`, `MenuMode`, optional `InstantActionDef`),
`CampaignMissionExit` (profile, `cm_sequence` position, per-seat choices), `QuitExit` and
`OptionsApplyExit` (the `PresentationId` and the graphics-mode word an Options screen applied).
An applied choice rides the exit rather than being saved by the screen that took it, so the
options file keeps one writer; a custom plane rides it as a resolved `CustomPlaneDef`, never a
store name. Presentations never construct sessions. The return side is `MenuReturnDestination`;
the exit table and the scans holding the seam: [../menu-presentations.md](../menu-presentations.md).

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
`Commit` are the purchase gate and the write. It also owns every label the screens write, the
name rules, and `Discard`, which drops the build and touches nothing saved. The economy, the
strings and the decoded tables: [../org/hangar.md](../org/hangar.md). Read `HangarFlow.cs` next.

## src/UI/Menu/CampaignFeature.cs
The campaign as a shared engine-free feature in the host's feature set: the state and the
operations both presentations read and write, with neither one's screen shell in it. `Open` opens
a campaign over a `CampaignProfileStore`, each further dependency optional and degrading rather
than failing. The roster operations create, seat, delete and record the last-played player in the
original's own words; the mission operations settle which `cm_sequence` entry the screens after
the cabin are about, with its briefing, its wingman flag and its change-plane rules; the writes
save the loadout, the planes, an exported build and the mission exit. How a presentation offers
them, cursors and working copies included, stays the presentation's. Read `CampaignFlow.cs` next.

## src/UI/Menu/BriefingScript.cs
The briefing reveal script, engine-free: the `Briefing.zrd` reader (`BriefingDialog`,
`BriefingState`, `BriefingStep`) and `BriefingReveal`, the interpreter that runs a state's
twelve-opcode beat sheet against a caller-advanced clock, blocking on an authored wait and on the
narration's cue times and keeping each element's opacity, rotation and position as its tweens
land. Elements come out in placement order, which is draw order; with no cue points every marker
releases at once, so the map finishes under the narration rather than a timing being invented.
Opcodes and their arguments: [../formats/briefing.md](../formats/briefing.md).

## src/UI/Menu/BriefingObjectives.cs
The briefing's parchment note, read from a mission's own `objectives.zrd`: every `IDENTITY`
carrying a message key, ordered by priority ascending, which is the list an `Objective id index`
opcode indexes 0-based. Takes the reader rather than a path, so it resolves without an extraction,
and leaves an unresolved key visible as its raw `MSG_*` symbol rather than inventing English. Why
keyless entries are not lines, and why file order is not the order, are recorded on the type
itself. Decode: [../formats/objectives.md](../formats/objectives.md).

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
missions with every objective bit set. `LaunchMenu`'s `campaign-*` aids and
`OriginalPresentation`'s read it; nothing here can reach `user://Profiles`.

## src/UI/Menu/Original/OriginalShell.cs
The Original presentation's screen graph (`CSVM.UI.Menu.Original`), engine-free over `MenuLayout`
and the shared Free Flight, player-setup, Instant Action, hangar and campaign features, with the
art measurer and the flight-devices answer injected. It owns the top level composed from
`[MainMenu]`'s own rows, the two remake-only sortie screens, the Options screen over the decoded
Preferences chrome, and the messagebox idiom every refusal and confirm goes through; the Game
Options, Instant Action, campaign and hangar screens are its four partials, below. `Step` applies
one seat's frame (pointer, typed text, cursor walk, accept and back) and `Compose` is the screen
as a `ComposedBoard`. Screen by screen: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalGameOptions.cs
The Game Options page, the shell's partial over the decoded `[@GameOptions@]` section. Its content
is a table: per option a key, a title, a description, the control kind and how the store field is
read and written, so a further option is one entry plus its field. The two shipped rows are the
presentation as a dropdown over the registered tokens and the graphics mode as a checkbox off the
section's own strip. The row shape is read off the section's own widgets, so a layout that moves a
row moves ours. ACCEPT CHANGES leaves as the `OptionsApplyExit`; a screen never writes the store,
`Launcher.ApplyOptions` does. The words and control kinds are remake-only readings, recorded in
[../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalSeats.cs
The shell's two sortie screens, Free Flight and Dogfight, over the shared player setup, the other
half of the `partial`. Rows: the chapter column and BACK, then the aircraft column over the
setup's roster and FLY. The aircraft column is an eleven-row window: every row keeps its place for
the keyboard, rows outside the window are invisible and unhit, and the window slides to keep the
focused row inside it. Seat 0 picks a chapter and an aircraft; a later seat walks its own cursor,
selects, confirms, and on Back undoes a stage or unjoins. FLY is enabled once the mode's gate is
met and leaves as the mode's own typed exit. Remake-only by design, the original shipping no
splitscreen Dogfight and no join gesture: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalInstantAction.cs
The Original Instant Action screen, the shell's partial over the decoded `[@InstantAction@]`
section and the shared `InstantActionFeature`. Its rows are the section's own widgets keyed by
their layout keys: the Table of Contents list in its authored window with its arrows and thumb,
the dropdowns at their authored boxes, the enemy rows on two pages, the loadout-target radio pair
and the screen's buttons. A contents row applies its preset on select, the ace duel hides every
enemy control, and `InstantActionInks` is the colour reading the presentation turns into this
screen's own palette. Option sets: [../formats/instant-action.md](../formats/instant-action.md);
the screen and what its capture must confirm: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalHangar.cs
The Original hangar, the shell's partial over the shared `HangarFeature` and the decoded hangar
sections: the PLANE NAME screen, the Plane Construction hub with one of six tab sections on its
right page, the totals page and the INVENTORY, each composed from its own layout section. It owns
the plane picture over the four blueprint panes (the airframe's blueprint, else the picked
pattern's region masks tinted under its plate), the running total, the tab bar read off the
layout's own edges, every dropdown's list under its box, and the airframe-switch ask as a dialog
over the page; every pick binds straight to the feature. The economy and the paint tables:
[../org/hangar.md](../org/hangar.md); the screens: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalCampaign.cs
The Original campaign, the shell's partial over the shared `CampaignFeature`: the profile screen,
the cabin, the table of contents, the flight check, ammo and plane selection, the book, a scrap's
zoom and the briefing dialog. What each screen draws is the shared board component, so the shell
hosts the Built-in campaign pages in a `CampaignFlow` of its own and copies every composed layer
into its own board; that flow is never walked, its screen and cursor mirroring this file's. The
screen graph, the rows at the rectangles the board draws them at, the pointer hit-testing, the
cues and the dialogs are this file's. Read `src/UI/CampaignFlow.cs` for the pages; the screens and
their strings: [../org/menu-inventory.md](../org/menu-inventory.md).

## src/UI/Menu/Original/OriginalPresentation.cs
The Original presentation node, registered under `PresentationId.Original`: a `CanvasLayer` on the
board layer holding one `ComposedBoardView`, so every screen scales as the campaign boards do.
`Activate` builds the shell and the device bookkeeping once, refreshes the roster from the
saved-plane store on every call, stands the shell on the top level, maps the return destination
onto it and applies the `--menu=` aid on the first show alone. `Tick` polls every seat, maps a
window-pixel pointer into the authored space, steps the shell, requests its cues and drives the
briefing's reveal and its narration. `Measure` is the strip size the layout does not carry, read
off the file once per name; `PaletteFor` is each screen's own inks as a `BoardPalette`.

## src/UI/Menu/Original/OriginalAvailability.cs
The availability answer Original is selected on: `Load(dataRoot, out reason, out degraded)` refuses
a tree stamped below `OriginalAssetManifest.StampSchema` (`ExtractionStamp.Behind`), reads the
layout through `MenuLayout`, requires a `[MainMenu]` section in it, and then checks the manifest
derived from that layout. Returns the loaded layout when Original can run, else null and the one
reason, which the host appends to its fallback reason; `degraded` is the optional half, for the
caller to log once. `ArtPath` is where a layout art name resolves, used by the presentation's own
size read too. Off-engine coverage: `CSVM.Tests/OriginalManifestTests.cs`.

## src/UI/Menu/Original/OriginalAssetManifest.cs
The versioned required/optional asset manifest, derived from the decoded layout rather than
hand-listed. `Derive` classes the art of the sections Original composes required, minus a short
table of rows it does not draw, everything else optional, and the five files the scripts name and
Original draws anyway required. `Check` reads no bitmap: existence plus the PNG signature and IHDR
size for a required entry, existence alone for an optional one, and one report naming every fault
with its section, row and file. `Schema` carries the rule for its own bumps. The classification is
reconciled against what the screens draw by `CSVM.Tests/OriginalCoverageTests.cs`. The asset
policy: [../menu-presentations.md](../menu-presentations.md).

## src/UI/Menu/Original/OriginalRosters.cs
The two rosters the Original sortie screens list. `Chapters` is the shared `MenuChapters` roster
with a short label per code; `Airframes` is the eleven stock names in the string table's own
order, resolved to their `planes.zbd` nodes through the Instant Action decode, so the
name-to-node map has one home. `Roster(customs)` appends the saved custom planes through the
shared player setup's roster rule, which is what both presentations pick from. Read
`src/UI/Menu/PlayerSetupFeature.cs` next.

## src/UI/Menu/Original/OriginalCues.cs
The four cue names the Original presentation asks the shared audio service for: a button rollover,
a button press, and an edit box's keystroke and reject sounds, which are the four the original's
globals script binds. The names are semantic and the cue table owns which wav each resolves to, so
the presentation names no file. The contract is `IMenuAudio.cs` and the table is
`src/Session/MenuCueTable.cs`.

## src/UI/Menu/Original/PointerSeat.cs
Seat 0 with a pointer: wraps the seat that polls the keyboard and the unclaimed pads and adds the
mouse as the frame's `MenuPointer` in window pixels, `Pressed` while the left button is down and
`Clicked` on the press edge; `Prime` reads the button so a click held through a screen change is
not a fresh click. The two device reads are injected delegates, so the seat is engine-free and
`Launcher` supplies the viewport's mouse position and `Input.IsMouseButtonPressed`. Built-in
ignores the pointer; Original maps it into its authored space. Later seats are pads and carry no
pointer; a source that wants one wraps itself the same way.

## src/UI/Menu/MenuReturnDestination.cs
Where the menu stands when it comes back, said semantically: `TopLevel`, `CabinReturn(profile)`
and `DebriefReturn(profile, missionSeq)`. The host names the destination and the active
presentation maps it into its own graph at `Activate`, so no presentation-specific screen id
crosses the seam. A destination names where the player stands and never a store: the two campaign
returns name a profile, and the store it is re-read from is the presentation's own. The `--menu=`
aid is not a destination either, reaching the cold start alone, so a return is always one of these
three. The namespace seam this whole folder is held to, and the two scans that enforce it, are in
[../menu-presentations.md](../menu-presentations.md).

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
seat picks from, set by the presentation and built by the shared rule (the stock rows in their
given order, then one row per saved custom flying its airframe's stock node). Per seat it owns the
cursor, the two stages of the pick, the loadout door and the backing-out ladder; the gate is the
mode's minimum of seats and every seat confirmed. `Choices` and `BuildExit` are the typed result.
Nothing here reads a pad: that is `src/UI/MenuSeatDevices.cs`, below.

## src/UI/MenuSeatDevices.cs
The pad side of the shared player setup, for any presentation, over seat 0's `MenuInput` and the
feature. `P1Pad` is the pad seat 0 claimed by steering a screen with it. `Sync` reconciles the
seats with the connected pads: a seat whose pad vanished is unjoined, a vanished claimed pad frees
seat 0, and seat 0's poller is bound to its claimed pad or to every unclaimed one. `PrimeJoins`
and `ScanJoins` are the join gesture, Start on an unclaimed pad while a seat is free, the caller
deciding on which screens joining is open. `PadOf` reads a joined seat's pad back off its
`BuiltInSeat`, and `FlightPads` is the binding a launch carries, the answer both presentations
hand the feature's `Choices`. Read `src/UI/Menu/PlayerSetupFeature.cs` for the seats themselves.

## src/UI/Menu/InstantActionFeature.cs
Instant Action as a shared `IMenuFeature`, owned by the host's feature set and configured by both
presentations. The option sets are static and decoded: the environments, the mission types with
the bans a chapter and stunt flying impose, the eleven airframes, the militias with the aircraft
each allows, the skills and the preset table. The setup is typed state with semantic operations:
select and confirm an environment (which re-fits the mission type and loads the chapter's own base
def), the mission type, the lives, the four waves, the wingmen and both plane picks, and apply a
preset. `Refusal`/`CanLaunch`, `BuildDef` and `BuildExit` are the gate and the launch, `Discard`
resets every field, and the decode is [../formats/instant-action.md](../formats/instant-action.md).
