# Menu presentations: the contract and the extension checklist

How the in-game menu is split between shared features and interchangeable presentations, what a
new presentation (a Modern one, say) plugs into, and what it must not assume it inherits from the
two that ship. The per-module detail is in [`architecture.md`](architecture.md), one `##` entry per
file named below; this page is the seam read as a whole. The screen census and the evidence behind
the Original presentation are [`org/menu-inventory.md`](org/menu-inventory.md); the decoded layout
format is [`formats/menu-layout.md`](formats/menu-layout.md).

## Contents

- [What a presentation is, and is not](#what-a-presentation-is-and-is-not)
- [Registration](#registration)
- [The lifecycle and the host](#the-lifecycle-and-the-host)
- [The shared features](#the-shared-features)
- [Input sources and seats](#input-sources-and-seats)
- [Options, selection and availability](#options-selection-and-availability)
- [Audio](#audio)
- [Launch](#launch)
- [Return](#return)
- [The asset policy](#the-asset-policy)
- [The namespace seam and the scans](#the-namespace-seam-and-the-scans)
- [The `--menu=` aid convention](#the---menu-aid-convention)
- [What a new presentation does not inherit from Original](#what-a-new-presentation-does-not-inherit-from-original)
- [The extension checklist](#the-extension-checklist)
- [The verification layers](#the-verification-layers)

## What a presentation is, and is not

A menu presentation is a screen graph together with its navigation, interaction, animation and cue
selection over the shared features. Drawing alone is not the boundary: the two shipped
presentations, Built-in (`CSVM/src/UI/Menu/BuiltIn/`, the launchscreen in `CSVM/src/UI/LaunchMenu.cs`)
and Original (`CSVM/src/UI/Menu/Original/`, the decoded 800x600 screens over the player's extracted
menu data), reach the same features through different screen sequences, different input idioms
(cursor rows against a pointer-first page) and different sounds.

A presentation is not where play is configured, validated, persisted or launched. Those are the
shared features' operations, and a presentation only decides how to offer them. It never
constructs a session, never names a launcher type, never hides itself, and never reads a device: it
reads semantic commands from the host's seats and leaves through one typed exit.

Built-in is permanent. It is the fallback every unavailable request resolves to, it needs no
extracted menu artwork to be usable, and every other presentation is additive beside it.

## Registration

`PresentationRegistry` (`CSVM/src/UI/Menu/PresentationRegistry.cs`) holds one factory per
`PresentationId`, a non-empty ordinal token that Options persist as written. `Launcher.BuildMenuHost`
(`CSVM/src/Session/Launcher.cs`) fills the registry once per process:

```
registry.Register(PresentationId.BuiltIn,  () => new BuiltInPresentation(...));
registry.Register(PresentationId.Original, () => new OriginalPresentation(...));
```

Registration says what exists in the build, nothing more. A second registration of the same id
throws, since two rivals for one identity is a wiring error. The host asks the registry for a fresh
instance on every activation after a switch, which is what makes a switch discard transient
presentation state by construction. The factories run inside the first `Show`, so a factory may
read process state the launcher settled by then (the cold start's `--menu=` aid, the layout the
availability check loaded).

## The lifecycle and the host

`IMenuPresentation` (`CSVM/src/UI/Menu/IMenuPresentation.cs`) has four calls and an identity:

| Member | When the host calls it | What the presentation does |
|---|---|---|
| `Id` | never; read by the host and the tests | the token it registered under |
| `Activate(host, destination)` | on the cold start, after every `Hide`, and once after a switch (always with `TopLevel`) | builds its nodes on the first call, then stands on the screen of its own graph that the destination maps to |
| `Tick(dt)` | every frame while shown | polls the host's seats, drives its graph, requests cues, hands an exit to the host |
| `Hide()` | after the host consumed an exit | takes itself off screen and keeps its state; a flight is not a switch, and the next `Activate` lands on the screens as they were left |
| `Deactivate()` | the first half of a switch | tears down everything `Activate` built; the instance is discarded afterwards |

`IMenuHost` (`CSVM/src/UI/Menu/IMenuHost.cs`) is what a presentation borrows between `Activate` and
`Deactivate` and keeps no reference to past that: `Features` (the shared `MenuFeatureSet`), `Audio`
(the shared `IMenuAudio`), `Seats` (one `IMenuInputSource` per joined seat, a live list) and
`Exit(MenuExit)`, the only way out. The implementation is `MenuHost` (`CSVM/src/UI/Menu/MenuHost.cs`),
engine-free, owned by `Launcher` for the life of the process. `Launcher` shows it with a destination
on every entry, ticks it every frame, and receives every exit through the sink it constructed the
host with. `MenuHost.Shown` is what the owner reads for "the menu is up"; no presentation's node is
consulted.

A switch is three host calls in order: `Deactivate()` (ends the instance and calls
`Features.DiscardTransient()`), `Select(...)` (re-resolves from the saved request), then
`Show(TopLevel)`. `Launcher.ApplyOptions` performs them one frame after the
`OptionsApplyExit` that asked for them, after saving every choice it carries.

## The shared features

`MenuFeatureSet` (`CSVM/src/UI/Menu/MenuFeatureSet.cs`) holds one instance of each feature, fetched
by concrete type (`Get<T>`, `TryGet<T>`), outliving every switch. A feature implements `IMenuFeature`
(`CSVM/src/UI/Menu/IMenuFeature.cs`): typed state plus semantic operations for one area of play, and
`Discard()`, which drops unfinished setup when the active presentation changes and touches nothing
persisted. There is no universal row, button or picture schema; each presentation reads a feature's
concrete members and composes them its own way.

| Feature | Owns | Does not own |
|---|---|---|
| `FreeFlightFeature` | the chapter roster it offers, the chapter pick, the launch gate over the setup's seats, `BuildExit` | seats, aircraft, stores |
| `InstantActionFeature` | every decoded option set, the environment, mission type, lives, four waves, wingmen and their fit, the player plane, presets, the base def, `BuildExit` | which screens the fields appear on, the wizard order |
| `PlayerSetupFeature` | the seats claimed by input-source identity (four at most, seat 0 never leaving), the aircraft roster (`BuildRoster`), each seat's cursor, the two-stage pick and its fit, the per-mode gate, `Choices`, `BuildExit` for Dogfight | the pad behind a seat (asked of the presentation through `MenuSeatDevices`), the join gesture, the split-pane or seat-strip drawing |
| `HangarFeature` | one scratch build at a time over a `CustomPlaneStore` and an optional `IHangarWallet`, the three starts, the airframe pick with the defaults ask, the per-tab operations, the purchase gate in the original's words, `Commit`, `DeleteSaved`, the labels and name rules | the nine-screen walk or the tab bar, the dropdowns, the dialog idiom |
| `CampaignFeature` | the profile store, roster and seated profile, `ContinuePlayer` and `DeletePlayer` with their refusals, the mission position and its briefing state and reveal progress, the intents between screens, `CommitLoadout`, `CommitPlanes`, `ExportPlane`, the flight field, the wallet, `BuildExit` | the screen stack, the cursor, the refusal line, the modal, the working copies before ACCEPT, the reveal's clock |

The rule that makes two presentations replaceable: a feature never references a presentation, and
the dependency test rejects one that does (below). A feature also never reads a device: seat 0's
pointer, a pad's Start, the keyboard's letters all arrive as semantic commands.

Two shared readers sit beside the features in the same namespace and follow the same rule:
`MenuLayout` (`CSVM/src/UI/Menu/MenuLayout.cs`), the reader of `extracted/rof/menu_layout.json`, and
`MenuChapters`, the chapter roster. `CampaignAidProfiles` is the scratch profile store the campaign
screenshot aids read, so both presentations' aids seat one player and none can reach
`user://Profiles`.

## Input sources and seats

`IMenuInputSource` (`CSVM/src/UI/Menu/MenuCommands.cs`) is one seat's source: `Poll(dt)` returns
that frame's `MenuCommands`, `Prime()` seeds edge detection so a button held from before the menu
showed is not a fresh press, `CapturingText` says whether typed characters feed a text field, and
`DeviceLabel` names the device for a join strip. `MenuCommands` is already device-neutral: `MoveY`
and `MoveX` with auto-repeat applied, the edges `Accept`, `Back`, `Join`, `Loadout`, `Contents`,
`Erase`, the `Typed` characters, and an optional `MenuPointer` (window pixels, `Pressed`, `Clicked`
on the press edge; null when the seat's devices have none). A presentation reads meaning and never a
key, button or axis.

The shipped sources: `BuiltInSeat` wraps one `MenuInput` (seat 0's reads the keyboard plus every
unclaimed pad; a joined seat's reads its one pad); `PointerSeat` wraps a seat and adds the mouse as
its pointer through two injected delegates; `MenuIdleSource` is a seat with no device, what the
`--debug-join=` aid seats. A source is not synonymous with a pad, and a later flight-control binding
plugs in as another `IMenuInputSource` with no change to any presentation.

The seats themselves are the `PlayerSetupFeature`'s. Once that feature is registered,
`MenuHost.Seats` is its live source list, `MenuHost.AddSeat` joins through it, and a join made
anywhere shows up in every presentation's `Seats` read. The pad side (`MenuSeatDevices`,
`CSVM/src/UI/MenuSeatDevices.cs`) is presentation-side and shared by both: seat 0's claimed pad,
hotplug reconciliation, the Start-to-join scan (each presentation decides on which screens it is
open), and `FlightPads`, the binding a launch carries per seat. A presentation with a pointer maps
the window-pixel pointer into its own space; Original does it through the same `BoardFit` its board
view draws with.

## Options, selection and availability

`OptionsStore` (`CSVM/src/Utils/OptionsStore.cs`) is the one process-wide options file,
`user://options.json`, independent of any profile: version-tolerant (an unknown version invalidates
the file, an unknown value drops only that field, a field the file does not carry reads as never
set), read as empty when missing or malformed, written atomically. Its fields are
`menuPresentation` (`built-in`, `original`) and `graphicsMode` (`original`, `enhanced`); a value
outside a field's set reads as never set.

**An option is a store field plus a row in each presentation's Options screen.** Adding one means
a nullable field on `OptionsDef` with its accepted-value set, the two writes in `Serialize` and
`Deserialize`, a row in Built-in's Options screen and an entry in the table Original's Game Options
page draws its rows from, a value on `OptionsApplyExit`, and the line in `Launcher.ApplyOptions`
that saves it. The store's
`Version` does not move for a new field: a missing field already reads as never set, so a file
written before the field existed loads with everything it does have, and the version gate is
reserved for a field whose meaning or shape changed. Whichever module consumes the option decides
what "never set" falls back to and where the saved value sits among its other sources; for the
graphics mode that is `GraphicsMode.Resolve`, where the `--graphics=` flag beats the saved option,
which beats the `graphics.mode` config key (`docs/cli.md`).

A screen never writes the store. Both values ride the exit and `Launcher.ApplyOptions` is the only
writer, so the options file has exactly one, and no test or suite that drives an Options screen
through Apply can write the player's own file.

`PresentationResolution` (`CSVM/src/Utils/PresentationResolution.cs`) fixes the precedence:
`--force-builtin`, then `--presentation=<token>`, then the saved request, then Built-in. Availability
is checked after the request is picked and never rewrites it, so what Options show back
(`MenuHost.Requested`) is what the player asked for even when `MenuHost.Selected` is Built-in.

`MenuHost.Select(forceBuiltIn, cliOverride, savedRequest)` answers availability as registration plus
the owner's `Availability` delegate, a function from `PresentationId` to a reason or null. Built-in
is never asked. `Launcher.OriginalAvailable` is the shipped delegate: it loads the decoded layout
through `OriginalAvailability.Load`, which refuses a tree stamped behind
`OriginalAssetManifest.StampSchema`, a missing or unreadable layout, a layout with no `[MainMenu]`,
and any required file the manifest names as missing or unreadable, each with one reason the host
appends to its fallback reason and logs. The delegate is asked on every process start and on every
switch, so a tree repaired while the process is up is seen by the next switch; a return from flight
never re-selects.

Every presentation exposes Options, since a player must be able to leave a presentation from inside
it. Built-in's is the Mode screen's Options row (`--menu=options`); Original's is the Game Options
page behind its Preferences page's first door (`--menu=game-options` under
`--presentation=original`). Both offer the same saved options, read them from the store on entry,
and leave through an `OptionsApplyExit` carrying every choice. Both presentation choosers offer the
two shipped tokens alone, so a third presentation extends them as well as the registry (checklist
below); both graphics choosers cover `original` and `enhanced` and say in their description that
the choice takes effect on the next start, since the mode is resolved once at launch and applying
it rebuilds nothing. The
startup recovery is `--force-builtin`, which beats everything and rewrites nothing.

## Audio

`IMenuAudio` (`CSVM/src/UI/Menu/IMenuAudio.cs`): `Cue(MenuCue)` plays one semantic cue by name,
`BeginNarration(wavName)` starts spoken narration, replacing any playing and ducking the music, and
`EndNarration()` stops it, idempotent. The presentation chooses which cue to ask for and when; the
service (`MenuAudioService`, `CSVM/src/Session/MenuAudioService.cs`) owns lookup, decoding,
playback, volume and the handoff into a launching session. A cue name the table lacks, a missing
file or a failed decode is logged once and cached as silence; a presentation never learns whether a
sound exists.

The cue table (`MenuCueTable`, `CSVM/src/Session/MenuCueTable.cs`) resolves the four names the
original's globals script binds: `menu.rollover`, `menu.click`, `menu.text`, `menu.text-error`, each
to a wav under the extracted rof tree's `ASSETS/SOUNDS`. Original asks for all four
(`OriginalCues`); Built-in asks for none and uses the service for briefing narration alone. A new
presentation may request these names or add its own to the table; the wavs it resolves to must come
from the player's extraction or from assets the repository may ship. Narration is begun by
whichever presentation shows a briefing and ended by it on every door out and in `Hide`; the
service knows nothing of which screen is up.

## Launch

`MenuExit` (`CSVM/src/UI/Menu/MenuExit.cs`) is the one way out, handed to `IMenuHost.Exit` and
consumed by `Launcher.OnMenuExit`. The hierarchy is closed:

| Exit | Carries | The consumer's action |
|---|---|---|
| `LaunchExit` | chapter, one `MenuSeatChoice` per seat, `MenuMode`, an `InstantActionDef` for Instant Action | derive the session spec from the CLI plus the payload, bind the seats' pads, build |
| `CampaignMissionExit` | the profile name, the `cm_sequence` position, one `MenuSeatChoice` per joined human | the same, over the campaign's story position |
| `QuitExit` | nothing | quit the process |
| `OptionsApplyExit` | the requested `PresentationId` and the graphics-mode word | save both, then the three-call switch one frame later |

`MenuSeatChoice` is the plane node, the pad devices the seat claimed, the fit and, for a saved
custom plane, its resolved `CustomPlaneDef`; the consumer never reads a store. The features build
these exits (`FreeFlightFeature.BuildExit`, `InstantActionFeature.BuildExit`,
`PlayerSetupFeature.BuildExit`, `CampaignFeature.BuildExit`), so a presentation assembles no payload
of its own beyond handing the feature the seats. The host hides the presentation before the sink
runs, and a failed build shows the same instance again where it stood.

## Return

`MenuReturnDestination` (`CSVM/src/UI/Menu/MenuReturnDestination.cs`) is where the player stands
when the menu comes back, said semantically; the hierarchy is closed:

| Destination | Raised by | Built-in lands on | Original lands on |
|---|---|---|---|
| `TopLevel` | the cold start, the boards' Exit, a failed build, every switch | the Mode screen with its cursors kept | the top level with its list cursors kept and every pick dropped |
| `CabinReturn(profile)` | leaving a campaign screen for the cabin | the cabin over the named profile, re-read from the presentation's store | the same, through its own campaign graph |
| `DebriefReturn(profile, missionSeq)` | a campaign mission's end, won or lost | the scrapbook on the flown mission | the book on the flown mission |

A destination names where the player stands and never a store or a screen id. The store a profile
is re-read from is the presentation's own (`user://Profiles`, or the scratch store a suite sets), and
a destination a graph lacks maps to the nearest one it has, with a logged warning when a named
profile cannot be read. The `--menu=` aid is not a destination: it reaches the cold start alone (the
launcher parks it, the factory reads it, the first `Show` consumes it), so a return after a flight is
always one of the three above and never the aid's screen again.

## The asset policy

Built-in is usable with no extracted menu artwork: where it draws extracted art (the campaign
boards, the hangar's blueprints) it degrades to its hardcoded chrome or to no picture, and it never
refuses to run.

Original is gated before entry. `OriginalAssetManifest` (`CSVM/src/UI/Menu/Original/OriginalAssetManifest.cs`)
derives its classification from the decoded layout on every start: the art of the sections
Original composes is required, less a short table of rows it never draws; every other section's
art, those rows' art and the media a script names are optional; the files the scripts name and
Original draws anyway (the two pointers, the font, the two export plaques) are required. `Check`
reads no bitmap (existence plus the PNG signature and header size for required entries, existence
alone for optional ones) and answers one report naming every fault with its section, row and file.
A required fault makes the presentation unavailable and Built-in runs with the reason logged; an
optional absence is logged once and degrades where it is drawn (a row with no readable strip keeps
a fallback rectangle and no picture). The manifest's `StampSchema` is the extraction stamp's own
schema (`ExtractionStamp.Schema`, recorded in `extracted/VERSION.json`), so a tree extracted before
the layout decode is refused with the re-extract instruction rather than read as empty; the
extractor re-stamps an existing tree without re-extracting it.

The classification is reconciled against what the screens draw: `OriginalCoverageTests` collects
every art name its journeys draw and fails any the manifest classes optional. No game asset enters
the repository; every test over real data is an `[ExtractedDataFact]` that skips when the extraction
is absent, and every fixture is hand-authored.

## The namespace seam and the scans

Shared contracts, features and readers live in the namespace `CSVM.UI.Menu` exactly (folder
`CSVM/src/UI/Menu/`). Each presentation lives in a sub-namespace (`CSVM.UI.Menu.BuiltIn`,
`CSVM.UI.Menu.Original`) and may depend on anything, `Godot` included. `ComposedBoard`, `BoardFit`
and the other board types stay presentation-side in `CSVM.UI`.

Two scans over the compiled metadata enforce it (`CSVM.Tests/MenuNamespaceDependencyTests.cs`,
through `AssemblyDependencyScan`, which walks signatures and method-body IL alike without loading
the assembly):

- no type in `CSVM.UI.Menu` references `Godot.*` or any `CSVM.UI.*` type outside that exact
  namespace, which is what keeps every feature free of both presentations and of the engine;
- nothing in `CSVM.UI` or any `CSVM.UI.Menu*` namespace names `GameSession`, `Launcher` or
  `LauncherContext`, which is what keeps every presentation from building a session or reaching
  the launcher.

The scanner's own fixtures prove it sees a signature-level and a body-only reference, and a scan
matching no types fails rather than passing. A new presentation's namespace falls under the second
scan automatically; anything it adds to the shared namespace falls under the first.

## The `--menu=` aid convention

`--menu=<value>` opens the cold start on one screen for a `--screenshot`, and its values belong to
the active presentation: every value in [`cli.md`](cli.md)'s bullet is Built-in's unless
`--presentation=original` is set, in which case the same flag carries Original's own values
(`free-flight`, `dogfight`, `instant-action`, `options`, the `plane-*` hangar poses, `campaign` and
the shared scratch-store campaign poses, `campaign-delete`), and any other value opens that
presentation's top level. Built-in's values and output stay stable whatever presentation is added.

A new presentation's aids follow the same rules: they select a screen of its own graph, they never
write into `user://Profiles` or `user://Planes` (the campaign poses read `CampaignAidProfiles`'
scratch store; a hangar pose commits nothing), the one door onto the real profile store is the value
`campaign` (`CampaignAidProfiles.PlayerDoor`), each value is documented in `cli.md`'s `--menu=`
bullet under its presentation, and no flag is added, so `cli.md`'s flag index and the parser's count
stay equal. An aid reaches the cold start alone and is consumed on the first `Activate`.

## What a new presentation does not inherit from Original

Original's fidelity choices are Original's, and a presentation registered beside it starts from
the contract above, not from Original's code. In particular it does not inherit:

- **The decoded layout and the 800x600 authored space.** `MenuLayout`, the layout sections, the
  widget rows, the four-frame button strips and their measured sizes are Original's evidence base.
  A new presentation may read `MenuLayout` or ignore it entirely; nothing in the host requires a
  decoded geometry.
- **The 4:3 board fit.** `BoardFit`'s uniform fit, centring and letterboxing, and the nearest
  sampling of extracted art, are the rule for screens composed in the original's coordinate space.
  A presentation that lays out for the window's own aspect owes `BoardFit` nothing.
- **The remake-only screens and rules.** Original's Free Flight, Dogfight and BUILD PLANE doors and
  screens, the words and control kinds its Game Options rows take, its three disabled Preferences
  page doors, its keyboard and pad focus over a pointer-driven original, and its pointer hotspot are
  readings recorded in the inventory as remake-only. A new
  presentation makes its own choices for the same operations and records them the same way.
- **The pointer bitmaps and the cue names.** The two extracted pointer bitmaps and the four cue
  names are what Original draws and asks for. A presentation with a pointer draws whatever it
  likes; one with sounds asks the service for names of its own or reuses these.
- **The shared campaign board component.** Original hosts Built-in's campaign pages inside its own
  graph (`CampaignBoards.For` over a `CampaignFlow` of its own, mirroring the screen and row) so the
  campaign screens look the same in both. That is a choice, not a requirement: a new presentation
  may compose the campaign feature's state its own way, and the classification of what is shared
  (`CampaignFeature`) against what is presentation-side (the stack, the cursor, the refusal line,
  the modal, the working copies, the reveal's clock) is what it reads to decide.
- **The asset manifest's classification.** The required set is derived from what Original
  composes. A new presentation that needs extracted art supplies its own availability answer
  (below) and its own required set; one that ships no extracted art is available whenever it is
  registered, like Built-in.
- **Original's `--menu=` values.** The aid names are per presentation; a new one mints its own.

What it does inherit, and may rely on: the features and their operations, the seats and their
semantic commands, the audio service and its narration handoff, the store and the resolution rule,
the four exits and the three destinations, the debug-join seating of device-less players, and the
launcher's whole leg (spec derivation, pad binding, the build, the failed-build return, the
frame-deferred switch).

## The extension checklist

In order. Each step names the file it touches and the test that proves it.

1. **Mint the identity.** Add a `PresentationId` token beside `BuiltIn` and `Original`
   (`CSVM/src/UI/Menu/PresentationId.cs`), and add the same string to `OptionsStore`'s accepted set
   (`CSVM/src/Utils/OptionsStore.cs`), or the saved request will read as never set. Extend
   `OptionsStoreTests` with the new token round-tripping.
2. **Create the namespace.** `CSVM/src/UI/Menu/<Name>/`, namespace `CSVM.UI.Menu.<Name>`. Put every
   node, drawing type and screen graph there; put nothing in `CSVM.UI.Menu` that is not shared by
   every presentation. `MenuNamespaceDependencyTests` covers the new namespace with no edit.
3. **Write the screen graph engine-free first.** Original's shape is a shell class over the
   features (`OriginalShell`) driven by `MenuCommands` frames and composing a picture, with a thin
   node (`OriginalPresentation`) that polls the seats, maps the pointer and draws. The shell
   unit-tests with no engine; write those tests against a hand-authored fixture, never against the
   player's data.
4. **Implement `IMenuPresentation`.** `Activate` builds on the first call and maps all three
   destinations (a destination the graph lacks maps to the nearest it has, logged); `Tick` polls
   `host.Seats` every frame (the list is live), sets each seat's `CapturingText` while a text field
   shows, and hands every exit to `host.Exit`; `Hide` keeps state and ends any narration;
   `Deactivate` frees everything. Apply the cold start's aid on the first `Activate` only.
5. **Offer every shared feature's operations.** Free Flight, Instant Action, Dogfight through the
   player setup, the hangar and the campaign, each reached from the presentation's top level and
   each with a way back that lands on the top level with no open campaign, build or dialog behind
   it. Write a coverage test in `OriginalCoverageTests`' shape: every screen reached and left by
   every input family the presentation supports, every exit typed.
6. **Expose Options.** A screen that reads the saved options from `OptionsStore`, offers every
   registered token and every option the store carries, and leaves through `OptionsApplyExit`.
   Generalise the two shipped presentation choosers from their two shipped tokens to the registered
   set at the same time (Built-in's Options row and the table Original's Game Options page draws
   from), so a switch into and out of the new presentation works from both of them.
7. **Register it.** One `registry.Register(PresentationId.<Name>, () => new <Name>Presentation(...))`
   in `Launcher.BuildMenuHost`, reading `_menuAid` for the cold start's aid as the two shipped
   factories do.
8. **Answer availability.** If the presentation needs extracted files, add its check to the
   `Availability` delegate `Launcher` installs (`OriginalAvailable` is the shipped one; extend it or
   compose a second), deriving the required set from what the screens draw and reconciling it in
   the coverage test as `OriginalCoverageTests` does. If it needs nothing, registration alone is its
   availability.
9. **Add its aids.** `--menu=` values under `--presentation=<token>`, documented in `cli.md`'s
   bullet, no flag added. Keep every campaign pose on `CampaignAidProfiles`' scratch store.
10. **Add the driven suites.** One in-engine suite per journey family in `CSVM/src/Testing/`, over
    a real `MenuHost` through `MenuSuiteHost`, standing for the presentation what
    `menu-original-tracer`, `menu-original-instant-action`, `menu-original-hangar`,
    `menu-original-campaign` and the Original half of `menu-player-setup-seats` stand for Original;
    add the presentation's cases to `menu-launch-return`, which drives every exit and every
    destination at the host's sink. Register each suite in `SuiteCatalog`.
11. **Prove Built-in unchanged.** Shoot Built-in's aids before and after and compare decoded pixels;
    the only Built-in change a new presentation may make is to the Options chooser.
12. **Document it.** Its `##` entries in `architecture.md` with their index lines, its remake-only
    readings where they belong, `cli.md`'s aids, and this page's registration paragraph.
13. **Run the landing gate.** The complete `.\RunTests.ps1`, then the at-the-controls pass: each
    mode to FLY and back through the pause board's Exit, a campaign mission to its end and back to
    the scrapbook, a real pad's join and walk, the sounds, the pointer, a cold start on an aid, a
    switch into and out of the presentation from both Options screens.

## The verification layers

Five layers, each catching what the others cannot:

- **Engine-free contracts.** The seam fixtures (`MenuSeamContractTests`, two fake presentations
  driving one fake feature to the same exit), the host (`MenuHostTests`), the store and the
  resolution rule, every feature's own tests, the shell tests over hand-authored layouts, the
  coverage check, the manifest cases, and the two metadata scans. `dotnet test` runs them all;
  those over the player's data are `[ExtractedDataFact]`s.
- **Hand-authored legal fixtures.** `CSVM.Tests/fixtures/menu-layout/` for the decoder and
  `fixtures/menu-layout-original/` for the shell: invented geometry and file names in the shipped
  file's shape, so no game data is committed and every rule is still exercised.
- **Local extracted-data comparisons.** Built-in's aids shot before and after a change and compared
  by decoded pixels; Original's aids shot at 800x600, 1280x720, 1920x1080 and 600x750 to show the
  fit; the manifest's recovery probes over a scratch copy of the data root, never the player's tree.
- **Driven suites.** The `menu-*` suites and `campaign-layout-parity` through real hosts and real
  shells over the install's own layout, up to the host's sink. The harness runs a suite before any
  session builds, so no suite flies a launch or performs the frame-deferred switch.
- **At the controls.** The launcher's leg and everything a suite cannot hear or hold: the flown
  launch and its return in every mode and both presentations, the real switch both ways, a real
  pad, the sounds, the pointer's rollover and pressed frames.
