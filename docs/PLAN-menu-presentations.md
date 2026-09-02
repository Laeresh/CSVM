# Menu Presentations

**ACTIVE PLAN** (written 2026-08-31). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan separates menu behaviour from presentation, keeps the existing Built-in presentation as
a permanent asset-independent route, and delivers a complete Original presentation over the
player's extracted menu data. It covers everything currently hosted by `LaunchMenu`: the top-level
menu, Free Flight, Instant Action, Dogfight, player joining and aircraft selection, campaign,
hangar, briefings and their modals. The existing code and docs cited below were inspected during the
2026-08-31 grilling; no backlog item was adopted by this plan, so no still-open re-verification was
performed.

The load screen, pause and results boards, flight HUD, debug labs and overlays are outside this
plan. A Modern presentation is also outside it: this plan leaves the proven contract and extension
guide it will use, but does not prototype or design its visuals.

## Milestone goal

- Built-in remains a complete, permanently supported menu presentation requiring no extracted menu artwork.
- Original is a complete alternative with its own evidenced screen graph, mouse-first interaction,
  animation and audio cues over one uniformly scaled 4:3 authored canvas.
- Typed shared feature models own configuration, validation, persistence, player setup and launch
  operations without depending on either presentation.
- A global options file persists the requested presentation; missing required assets select Built-in
  for that run without erasing the request.
- Keyboard, mouse and pad feed semantic menu commands through a device-neutral seam that can later
  accept HOTAS/HOSAS input.
- The original's menu layout is decoded before play into documented structured output; runtime reads
  that output and performs no format or executable analysis.
- Existing campaign screens retain their present appearance while their fixed chrome migrates onto
  the decoded-layout path and becomes the Original presentation's fidelity baseline.
- Built-in and Original leave through one typed launch handoff and receive semantic return destinations.

**No Modern presentation and no unrelated menu cleanup.** Built-in changes only where this plan
requires Options and presentation switching; existing menu defects remain separate work.

## Decisions (2026-08-31)

| # | Question | Decision |
|---|---|---|
| 1 | What does this plan deliver? | **The architecture, unchanged Built-in migration, and a complete Original presentation.** Modern follows later. |
| 2 | How is a presentation selected? | **A persistent player option plus a CLI override.** Built-in is the default and fallback. |
| 3 | Is Original only a visual skin? | **No.** It owns evidenced artwork, geometry, animation, pointer behaviour and screen-specific interaction. |
| 4 | May presentations navigate differently? | **Yes.** They share feature state and operations, not one screen sequence. |
| 5 | What is in scope? | **Everything hosted by `LaunchMenu`;** flight boards, load screen, HUD, labs and overlays are out. |
| 6 | Does Built-in survive Modern? | **Yes.** It is permanent and asset-independent. |
| 7 | What survives presentation switching? | **Persisted data only.** The new presentation starts at its top level; unfinished setup and transient UI state are discarded. |
| 8 | How can a player recover? | **Every presentation exposes Options, and startup has a force-Built-in override.** |
| 9 | When is Original available? | **A required/optional asset manifest is checked before entry.** Required failure activates Built-in with an explanation. |
| 10 | Does fallback rewrite the option? | **No.** Requested and active presentation are distinct. |
| 11 | What fidelity does Original promise? | **Evidence-based composition and interaction, not literal pixel identity.** Unknown behaviour blocks on evidence or an explicit remake-only rule. |
| 12 | How does Original scale? | **Every screen uses the existing `BoardFit` rule:** authored 800x600 coordinates uniformly fit and centre as 4:3; artwork stays nearest-sampled. |
| 13 | Who owns player joining? | **A shared player-setup feature owns seats, devices, choices and locks;** presentations own how those operations are offered. |
| 14 | What is the shared UI abstraction? | **Typed feature models and semantic operations, not a universal row/button/picture schema.** |
| 15 | What is the first tracer? | **Single-player Free Flight:** top level to launch and return. |
| 16 | May extraction clean up Built-in behaviour? | **No.** Preserve its screen order, controls, payloads, aids and return behaviour except for the new Options route. |
| 17 | How is Original verified without assets in git? | **Engine-free contracts, hand-authored legal fixtures, local extracted-data comparisons, campaign regression and at-the-controls input journeys.** |
| 18 | May presentations share components? | **Yes.** Built-in and Original initially share the existing campaign-board component. |
| 19 | Where does layout decoding happen? | **Before runtime.** Extraction emits a decoded menu layout; CSVM only consumes it. |
| 20 | What migrates in campaign boards? | **Fixed chrome and widget metadata move to decoded layouts;** dynamic campaign content remains in typed feature models. |
| 21 | Where do audio responsibilities sit? | **Presentations request cues and timing;** a shared service owns playback, volume, lookup and session handoff. |
| 22 | How does play launch? | **One typed menu exit/launch handoff** consumed by `Launcher`; presentations never build sessions. |
| 23 | How does play return? | **A semantic destination** is mapped by the active presentation, never a presentation-specific screen id. |
| 24 | What does this plan do for Modern? | **Document the proven contract, registration seam and extension checklist; no prototype.** |
| 25 | Which input devices must Original support? | **Mouse, keyboard and pad completely.** Future HOTAS/HOSAS plugs into the same menu-input-source contract but is not implemented here. |
| 26 | What happens to existing `--menu=` aids? | **Their values and Built-in output remain stable.** A separate presentation override selects Original aids. |
| 27 | What is selection precedence? | **Force Built-in, CLI presentation override, saved request, Built-in default;** availability then chooses the active presentation. |
| 28 | Where are options stored? | **One process-wide, version-tolerant `user://` options file, independent of profiles.** This plan initially adds only options it needs. |
| 29 | When is Original exposed normally? | **Only after every in-scope journey passes acceptance.** Incomplete work remains CLI-only. |

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, C21, D31, D32 | Confirm the cited seam and preserve it before changing structure. |
| **Leads only — no mechanism yet** | A3, A4, B12, B13, C22, C23, D33, E41, E42, E43, E44 | The grilling fixed the outcome, not the implementation; investigate before committing to types or file boundaries. |

The phrase “menu renderer” is too narrow. `CONTEXT.md` fixes **menu presentation** because screen
graph, interaction, animation and audio differ as well as drawing. Likewise, an 800x600 authored
canvas is a coordinate system, not a fixed output resolution.

## What the data actually ships

`ExtractRof.ps1` unpacks `crimson.rof` and its `crimptch.rof` overlay into `extracted/rof/`, emits
`ui_strings.json`, additively decodes custom `.BM` images, and emits the decoded
`menu_layout.json`. Its own header records 846 archive members: 588 already-common PNG/JPG/TGA/TIF
files and 184 custom `.BM` files. The GUI scripts and `LAYOUT.CSV` are also in the archives
(`ExtractRof.ps1`; `docs/tooling.md`; `docs/formats/menu-layout.md`).

Campaign composition already proves the intended display rule. `BoardFit` maps the original's
800x600 coordinate space uniformly into any viewport, centres it and letterboxes the remainder;
`ComposedBoardView` uses nearest filtering for extracted artwork (`CSVM/src/UI/BoardFit.cs:5-42`;
`docs/architecture.md:4258-4265`, `4311-4322`). `CampaignBoards` and `ComposedBoard` already describe
fixed campaign chrome and authored primitives, while `CampaignFlow` and `HangarFlow` are engine-free
flows whose pages still expose presentation-shaped content (`docs/architecture.md:4139-4151`,
`4217-4228`, `4268-4279`).

`LaunchMenu` is the current integration point and is 3,256 lines. It owns menu state, input polling,
joining, campaign and hangar hosting, audio, rebuilding, Godot controls and launch callbacks
(`CSVM/src/UI/LaunchMenu.cs:21-45`, `799-841`, `1054-2105`, `2248-3168`). There is no general player
options store today; profiles, custom planes and scores each have their own persistence.

A1's census is [`docs/org/menu-inventory.md`](org/menu-inventory.md), and the archive counts above
are not coverage. Built-in hosts **28** player-visible screens (10 launchscreen, 9 hangar, 9
campaign), **9** sub-states that are not screen enum members, and **110** transitions; **32**
`--menu=` values are accepted against **31** documented, and `Screen.WaveEdit` is the one screen no
aid can open. `crimson.rof` ships **61** GUI scripts (5 infrastructure, **34** single-player
screens and **22** multiplayer screens), and `LAYOUT.CSV`'s **35** sections are `[GLOBALVARS]` plus
one per single-player script, one to one, so **the 22 multiplayer screens have no authored layout at
all** and decoding `LAYOUT.CSV` reaches 34 screens, not 56. Those sections hold **636** widget rows
in 10 of the 11 documented types (`W`, the sound object, is declared and never used), **186** macro
definitions, **124** distinct art files (122 present, `CrimFlag.MPG` and `Final.MPG` absent) and
**152** `IDS_*` symbols of which 149 resolve. **46** navigation edges are stated by the layout's own
`ScriptToExe` column and by no script. Menu audio is **8** wav files bound in scripts, none of which
Built-in plays. Of the 34 single-player screens **27** are in scope; Preferences, Game Options,
Audio, Video, Controls and Keys have no Built-in counterpart, and the original's top level has six
rows of which **Free Flight, Dogfight, a chapter list and a top-level hangar door are none**. Those
four are ours. Five owed captures (`CAP-49` to `CAP-53`) cover the main menu, the Instant Action
setup screen, Preferences, menu audio and pointer behaviour, and the plane-construction tab bar.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Evidence and contracts

1. ☑ Inventory every in-scope menu journey and its original evidence
2. ☑ Decode menu layouts during ROF extraction and document the format
3. ☑ Add the global options store and requested/active presentation resolution
4. ☑ Define the presentation, feature, input, audio, launch and return contracts

### Wave B — Free Flight tracer

11. ☑ Characterize Built-in and extract the Free Flight feature
12. ☑ Run Built-in Free Flight through the presentation boundary
13. ☑ Deliver Original Free Flight, switching and recovery

### Wave C — Setup features

21. ☑ Extract Instant Action into a typed shared feature
22. ☑ Extract shared player setup and deliver Dogfight in both presentations
23. ☑ Separate hangar features from composition and deliver its Original screens

### Wave D — Campaign convergence

31. ☑ Separate campaign feature state from presentation descriptions
32. ☑ Migrate campaign fixed chrome to decoded layouts without visual change
33. ☑ Complete Original campaign interaction, briefing and audio integration

### Wave E — Completion and release gate

41. ☐ Complete Original top level, Options and every remaining transition
42. ☐ Enforce the required/optional Original asset manifest
43. ☐ Complete typed launch and semantic return routing across every journey
44. ☐ Run acceptance, enable Original normally and publish the Modern extension contract

## Dependency and parallelism notes

A1 is the evidence inventory and blocks Original implementation items. A2 blocks every item that
consumes decoded layouts. A3 and A4 establish process-wide contracts and block B11 onward. B11 →
B12 → B13 is the tracer chain; no later feature extraction starts until it proves launch and return.
C21 and C22 may proceed in parallel after B13 if their file ownership is separated, but both contend
on the current `LaunchMenu.cs` until its shell has been split. C23 and D31 touch the existing flow/page
boundary and should not run in parallel if either edits shared composition types. D31 → D32 → D33 is
linear. E41–E43 integrate the same presentation host and `Launcher`, so run them serially. E44 is the
only normal-availability gate.

---

# Wave A — Evidence and contracts

## A1 ☑ Inventory every in-scope menu journey and its original evidence

**Goal.** Produce the authoritative screen/transition inventory for Built-in and Original, including
the source of layout, interaction and audio evidence and every fact still requiring a capture.

**Evidence (confidence: traced).** `LaunchMenu` hosts the agreed scope and the archive ships GUI
scripts plus `LAYOUT.CSV`, but this session did not census their coverage (`CSVM/src/UI/LaunchMenu.cs:21-45`;
`ExtractRof.ps1:7-16`).

**Approach.** Trace every `LaunchMenu.Screen`, `CampaignScreen`, `HangarScreen`, modal, launch and
return edge. Match each Original screen to extracted script/layout/art and existing docs/captures.
Record missing behavioural facts as owed captures rather than filling them with assumptions. The
inventory must classify required versus optional assets for E42.

**Model recommendation.** high — this is cross-source reverse-engineering and scope control.

**Verify.** The inventory is documentation, so the check is a census read back against its sources
rather than a suite. Performed: `SessionSpec.cs`'s `--menu=` parse compared against
`LaunchMenu.ShowMenu`, `OpenHangarAid`, `OpenCampaignAid` and `Launcher.ShowLaunchMenu`, giving 32
values accepted, each appearing exactly once in the inventory's aid table, against 31 in `docs/cli.md`
(`name` is accepted and undocumented, a `cli.md` edit this item did not make). All 12
`LaunchMenu.Screen`, all 9 `HangarScreen` and all 9 `CampaignScreen` members appear exactly once in
the screen census; 11 of the 12 `Screen` members carry an aid and `WaveEdit` carries none, which the
inventory states rather than hides. The original half is a parse of
`extracted/rof/ASSETS/LAYOUT.CSV` (35 sections, 636 widget rows in 10 types, 186 macro definitions,
122 distinct art references of which 120 resolve on disk, 152 `IDS_*` symbols of which 148 resolve
in `ui_strings.json`, 46 `ScriptToExe` edges; A2's decoder corrects the hand count to 124 art
references with 122 present and 149 resolving symbols, see `docs/formats/menu-layout.md`) cross-checked against the 61 script names in
`extracted/rof/ASSETS/SCRIPTS/`, which gives the 34-to-34 section/script correspondence and the 22
multiplayer scripts with no section. `docs/verification.md` rules cited: **SRC-4** (one description
of record, so the inventory points at `formats/campaign-screens.md`, `instant-action.md` and
`rof.md` rather than restating them), **SRC-7** (a key the data authors is not a feature until a
reader is found, so the unused `W` sound-object row type and the dead `IDS_GN_1P/2P/3P` references
are recorded as such), and **SHOT-32** (a shot of an animated screen proves the frame it drew,
which is why `CAP-49`'s flag movie and `CAP-52`'s audio are filmed rather than screenshot).

**Verified.** Docs-only item; the census cross-checks above are its verification. The merged Wave A
tree passed the complete `.\RunTests.ps1` (2816 units, 200 engine suites, 18 goldens
hash-identical, 131.6s).

**⚠ Traps.** A filename or rectangle proves composition, not interaction. Existing campaign
fidelity does not prove non-campaign coverage.

## A2 ☑ Decode menu layouts during ROF extraction and document the format

**Goal.** `ExtractRof.ps1` emits a stable, structured decoded menu-layout artifact that runtime can
read without interpreting original formats or executable behaviour.

**Evidence (confidence: traced).** ROF extraction already owns non-ZBD UI decode and writes
`ui_strings.json`; `LAYOUT.CSV` and GUI scripts are currently only unpacked verbatim
(`docs/tooling.md:94-114`; `ExtractRof.ps1:18-33`).

**Approach.** Decode the layout/script fields established by A1 during extraction, apply the patch
overlay with the archive's precedence, write hand-authored parser fixtures, bump the extraction
schema when old trees become invalid, and document the decoded source and output in
`docs/formats/`. Runtime receives structured data only. Do not commit extracted output or game art.

**Model recommendation.** high — format work, compatibility and legal boundaries meet here.

**Verify.** Fixture tests:
`dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~MenuLayoutDecoderTests"`
(22 tests: 21 over the hand-authored `CSVM.Tests/fixtures/menu-layout/` set, plus one
`[ExtractedDataFact]` census of the player's own tree). Real-tree extraction:
`.\ExtractRof.ps1 -Source <install>\GOSDATA\ASSETS -Dest .\.scratch\menu-layout-extract`, whose
census line is the artifact's own `counts` block.

Settled: the decoder lives in `ExtractRof.MenuLayout.cs` at the repo root, `Add-Type`d from disk by
`ExtractRof.ps1` and `<Compile Include>`d by `CSVM.Tests`, so the shipped decode and the fixture
tests are one implementation; it therefore stays inside the C# 5 subset PowerShell 5.1's `Add-Type`
accepts, and writes its JSON by hand. `packaging/Extract.ps1` is unchanged.

**The extraction schema is deliberately not bumped, and the bump is owed to B13/D32.**
`ExtractionStamp.Schema`'s own rule is "bump whenever a *reader* change invalidates old
extractions". A2 adds an output and no reader, so an old tree is not yet invalid. The first item
that requires `menu_layout.json` at runtime must bump `$StampSchema` in `ExtractAssets.ps1` and
`ExtractRof.ps1` and `ExtractionStamp.Schema` in one commit, so a pre-A2 tree is reported stale
rather than read as empty. `docs/formats/menu-layout.md` carries the same statement.

**⚠ Traps.** Do not decode executable behaviour at runtime. `packaging/Extract.ps1` stays a dispatcher;
all extraction logic remains in `ExtractRof.ps1` or code it directly owns.

**Verified.** No `CSVM/` source changed, so the landing gate is the unit stage: the merged plan tree
passed `.\RunTests.ps1 -SkipEngine -SkipGoldens` (2854 units, the 22 decoder tests among them, one
of which is the census over the player's real tree). The orchestrator confirmed the three census
corrections against `LAYOUT.CSV` and `RESOURCE.H` directly before merging.

## A3 ☑ Add the global options store and requested/active presentation resolution

**Goal.** Persist process-wide Options, resolve requested versus active presentation with the fixed
precedence, and recover safely from missing or malformed state.

**Evidence (confidence: lead-only).** The repository has dedicated profile, plane and score stores
but no general options store; the desired semantics were fixed by Decisions 7–10 and 27–28.

**Approach.** Add a version-tolerant atomic `user://` JSON store, initially for the selected menu
presentation. Resolve force-Built-in → CLI override → saved request → Built-in default, then validate
availability separately. Preserve the requested value across fallback and expose a reason for the
Built-in presentation to show.

**Model recommendation.** medium — small persistence surface with important recovery semantics.

**Verify.** Unit-test missing, valid, unknown-version/value, malformed and interrupted-write cases;
test every precedence pair and requested/active separation. Settled: `OptionsStore` follows
`CampaignProfileStore`/`CustomPlaneStore`'s plain-System.IO-over-an-absolute-directory shape (so it
unit-tests without an engine) and their read tolerance (missing/malformed reads as empty, an unknown
version invalidates the whole file), but none of the three existing stores write atomically, so this
store is the first to need it: `Save` writes a sibling `options.json.tmp` then renames it over
`options.json` in one filesystem operation, single file rather than a per-item directory since there
is exactly one options record. Covered by `CSVM.Tests/OptionsStoreTests.cs` (persistence) and
`CSVM.Tests/PresentationResolutionTests.cs` (precedence and requested/active separation); ran
`dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~OptionsStoreTests|FullyQualifiedName~PresentationResolutionTests|FullyQualifiedName~SessionSpecParserTests"`
and `dotnet build CSVM/CSVM.sln`, both green.

**⚠ Traps.** Options are global and never live in a campaign profile. A temporary extraction problem
must not rewrite the saved request.

**Verified.** `dotnet build CSVM/CSVM.sln` and the focused
`OptionsStoreTests`/`PresentationResolutionTests`/`SessionSpecParserTests` filter ran green during
the item; the orchestrator re-measured the 150/150 flag census independently and ran the complete
`.\RunTests.ps1` on the merged Wave A tree: 2816 units, 200 engine suites, 18 goldens
hash-identical, all passing in 131.6s.

## A4 ☑ Define the presentation, feature, input, audio, launch and return contracts

**Goal.** Establish small typed boundaries that allow different screen graphs without allowing
presentations to own game configuration or session construction.

**Evidence (confidence: lead-only).** `CampaignFlow` and `HangarFlow` already demonstrate engine-free
state, while `LaunchMenu` combines input, drawing, audio and two launch callbacks
(`docs/architecture.md:4139-4151`, `4217-4228`; `CSVM/src/UI/LaunchMenu.cs:37-45`).

**Approach.** Specify presentation registration/lifecycle, typed feature state and operations,
per-seat semantic commands from menu input sources, shared audio playback, one typed menu exit, and
semantic return destinations. Keep `ComposedBoard` presentation-side. Record the contract and its
dependency direction in `docs/architecture.md`; add architecture tests that reject dependencies
from shared features onto Built-in or Original types.

**Model recommendation.** max — this is the high-blast-radius seam the rest of the plan depends on.

**Verify.** Compile minimal fake feature and presentation fixtures, prove two different screen graphs
can drive the same operation, and prove presentation types are absent from shared model assemblies or
namespaces. The boundary is namespaces inside the one engine assembly: shared contracts and features
live in `CSVM.UI.Menu` exactly (folder `CSVM/src/UI/Menu/`), presentations in sub-namespaces
(`CSVM.UI.Menu.BuiltIn`, `.Original`). The dependency test is `MenuNamespaceDependencyTests` over
compiled metadata: `AssemblyDependencyScan` (System.Reflection.Metadata, no assembly load) walks
every `CSVM.UI.Menu` type's base/interfaces/signatures/locals plus method-body IL tokens and fails
on any reference to `Godot.*` or to `CSVM.UI.*` outside `CSVM.UI.Menu`; scanner self-tests prove it
sees signature-level and body-only references and that an empty subject fails rather than passing.

**Verified.** Contracts, fixtures and tests landed engine-free: the 13 new
`MenuSeamContractTests` + `MenuNamespaceDependencyTests` drive one `FakeSortieFeature` to the same
`LaunchExit` through a three-screen cursor wizard and a one-screen pointer page, and reject
presentation/engine references from `CSVM.UI.Menu`. The orchestrator ran the complete
`.\RunTests.ps1` on the merged Wave A tree: 2816 units, 200 engine suites, 18 goldens
hash-identical, all passing in 131.6s.

**⚠ Traps.** Do not create a universal row/button/picture schema. “Presentation” includes navigation,
interaction, animation and cue selection; drawing alone is not the boundary.

# Wave B — Free Flight tracer

## B11 ☑ Characterize Built-in and extract the Free Flight feature

**Goal.** Capture Built-in’s present Free Flight behaviour, then move its selections and validation
into an engine-free typed feature without changing any observable journey.

**Evidence (confidence: traced).** Free Flight currently follows Mode → Chapter → Plane and shares
the final aircraft screen with other modes (`docs/architecture.md:3901-3913`). `LaunchMenu` owns the
state and launch construction today (`CSVM/src/UI/LaunchMenu.cs:243-278`, `2248-2250`).

**Approach.** Add characterization tests for navigation, Back, chapter/aircraft choices, launch
payload, return and relevant CLI aids before extraction. Move only Free Flight state/operations and
the typed launch request; leave Instant Action, Dogfight, campaign and hangar on their old paths.

**Model recommendation.** high — this tracer must cut a safe seam through a large stateful class.

**Verify.** Characterization first, on the pre-extraction code: the new engine suite
`menu-free-flight-journey` (`CSVM/src/Testing/MenuJourneySuites.cs`) drives a real `LaunchMenu`
through `LaunchMenu.Drive(MenuCommands)` and reads the `Shown*` read-outs, Mode → Chapter → Plane
→ the `Launch` callback, Back at every step, `HideMenu`/`ShowMenu` as a return from flight, the
aids `--menu=chapter`, `plane`, `selected` and `loadout`, Quit from Mode, and a second seat
holding the gate; it passed green before the extraction and unchanged after it. Then the
extraction: `dotnet build CSVM/CSVM.sln`, then
`dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build --filter "FullyQualifiedName~FreeFlightFeatureTests|FullyQualifiedName~MenuNamespaceDependencyTests|FullyQualifiedName~MenuSeamContractTests|FullyQualifiedName~SessionSpecMenuTests|FullyQualifiedName~LaunchMenuWizardTests|FullyQualifiedName~SuiteCatalogTests"`
(81 tests, the seam scan among them), the whole `dotnet test` (2832), and
`.\RunTests.ps1 -Suite "menu-free-flight-journey,menu-zone-layout,menu-screenshot-key,campaign-briefing-repaint" -SkipUnits -SkipGoldens`
(4 suites). Before/after shots through `.\RunProbe.ps1 --menu=<aid> --screenshot=<abs path>` for
`mode`, `chapter`, `plane` and `selected`, rebuilt between, compared by decoded pixels (SHA-256
over the 32bpp rows), all four identical. `docs/verification.md` rules that bit: **SHOT-9** (the
probes run windowed on the hidden desktop, never `--headless`), **SHOT-10** (absolute output paths,
the directory created first, every file checked present), **SHOT-6** (the comparison decodes the
PNGs rather than hashing their bytes), **SHOT-32** (a shot proves the frame it drew, so behaviour
is pinned by driving the real screens in the journey suite and the shots stand only for
appearance).

**Verified.** The merged plan tree with B11 landed passed the complete `.\RunTests.ps1` (2832 units,
201 engine suites across 4 shards with `menu-free-flight-journey` among them, 18 goldens
hash-identical, engine errors clean, 157.7s).

**⚠ Traps.** Do not repair existing mouse, focus or layout issues. Characterization pins current
behaviour, including quirks not explicitly changed by this plan.

## B12 ☑ Run Built-in Free Flight through the presentation boundary

**Goal.** Built-in becomes a registered presentation and completes the Free Flight tracer through
the new host with no intended visual or behavioural change.

**Evidence (confidence: lead-only).** The boundary is a negotiated design; B11 supplies its first
characterized feature and A4 its contract.

**Approach.** Split the process-lifetime menu host from Built-in controls and screen graph. Route
semantic commands, audio service calls, typed exit and semantic return through the host. Keep all
other current flows operational through temporary adapters rather than rewriting them early.

Handoff from B11: `LaunchMenu` constructs its own `FreeFlightFeature` (`_free`, exposed as
`LaunchMenu.FreeFlight`) and adapts the feature's `LaunchExit` back onto the old `Launch`
callback in `Dispatch`; the host takes over both, owning the feature in its `MenuFeatureSet` and
consuming the `LaunchExit` itself, after which `Dispatch` and the Free branch of `FireLaunch` go.
`LaunchMenu.Drive(MenuCommands)` already takes A4's frame shape for player 1, so the Built-in
presentation's `Tick` can feed it from the host's seats; `MenuInput` still polls the devices
behind it. `--menu=plane`, `selected` and `loadout` hand the feature the cursor's chapter from
`ShowMenu`, because they skip the Chapter Accept; a host-side aid path has to keep that handover.

**Model recommendation.** high — lifecycle and return regressions can strand every menu path.

**Verify.** The landed shape: `MenuHost` (`CSVM/src/UI/Menu/MenuHost.cs`) is the process-lifetime
host `Launcher` owns, holding the `MenuFeatureSet` (with the `FreeFlightFeature`), the audio
service (`CSVM/src/Session/MenuAudioService.cs` over the music channel, the sound archive and the
narration player), the first seat (`BuiltInSeat` over the launchscreen's `MenuInput`) and the exit
sink (`Launcher.OnMenuExit`); `BuiltInPresentation` (`CSVM/src/UI/Menu/BuiltIn/`) is registered
under `PresentationId.BuiltIn` and stands `LaunchMenu` up in `Activate`, hides it in `Hide`, frees
it in `Deactivate`; `MenuHost.Select` resolves through `PresentationResolution.Resolve` with
`--force-builtin`, `--presentation=` and the saved `OptionsStore` request, registration as
availability, an unknown or blank request falling back to Built-in with the reason logged;
`LaunchMenu` has no callbacks, every launch (Free, Instant Action, Dogfight, campaign) and the quit
leave as a typed exit through `IMenuHost.Exit`; the return from flight is
`Launcher.ReturnToMenu(MenuReturnDestination)` (`TopLevel` from the boards' Exit and a failed
build, `DebriefReturn` from a campaign mission's end), mapped by Built-in onto its screens with the
`--menu=` aid still applied on every top-level show. One contract addition: `IMenuPresentation.Hide`,
because a flight is not a switch and a fresh instance per return would lose the cursors B11 pins.

Checked: `dotnet build CSVM/CSVM.sln`, then the whole `dotnet test` (2839, of which 7 are the new
`MenuHostTests` over the seam fixtures and the seam scan `MenuNamespaceDependencyTests` is among
them), then `.\RunTests.ps1 -Suite "menu-free-flight-journey,menu-host-tracer,menu-zone-layout,menu-screenshot-key,campaign-briefing-repaint" -SkipUnits -SkipGoldens`
(5 suites, 5 passed) and `.\RunTests.ps1 -Filter "campaign,hangar" -SkipUnits -SkipGoldens` (44
suites, 44 passed, engine errors clean), `.\CheckCommentCaps.ps1 -Summary` and
`.\CheckEncoding.ps1`. `menu-free-flight-journey` is green with one mechanical edit: it stands its
`LaunchMenu` on a bare `MenuHost` whose sink records exits and reads `LaunchExit`/`QuitExit`
instead of the removed `Launch`/`Quit` callbacks; every check is otherwise as B11 wrote it. The new
`menu-host-tracer` (`CSVM/src/Testing/MenuHostSuites.cs`) is the tracer through the boundary: a
real `MenuHost` with the Built-in presentation registered, `Show(TopLevel)`, every press a frame
through the host's first seat (`host.Tick`), the launch arriving at the sink as one `LaunchExit`
with the presentation hidden, a frame while hidden reaching nothing, then `Show(TopLevel)` again
on the same instance re-entering with the state B11 pins for a return (Mode, chapter cursor on New
York, airframe cursor on Balmoral, selection dropped, nothing relaunched), and `Deactivate`
freeing the launchscreen. It also pins the selection rule at the host: an unknown request falls
back with a reason and keeps the request, the force flag beats a CLI override. Shots: rebuilt, then
`.\RunProbe.ps1 --menu=<aid> --screenshot=<abs path>` for `mode`, `chapter`, `plane` and `selected`
into `.scratch\b12-after\`, compared by decoded 32bpp pixels (SHA-256 over the rows) against B11's
`.scratch\b11-after\`: `mode` and `chapter` identical; `plane` and `selected` differed by one roster
row, a custom plane (`Crooked Vulture`) saved to `user://` after B11's shots, so the baseline was
re-measured near the changed run (METHOD-3) by shooting the same two aids from B11's own still-built
binary in its worktree against today's store, and against that baseline all four are identical.
`docs/verification.md` rules that bit: **METHOD-3** (the first plane/selected difference was the
store moving under the baseline, not the change), **METHOD-6** (the rebaseline names which binary
each side used: B11's worktree build against B12's), **SHOT-6** (decoded pixels, never PNG bytes,
whose sizes differed for an encoder reason as well), **SHOT-9**/**SHOT-10** (windowed probes on the
hidden desktop, absolute paths, files checked present), **SHOT-32** (a shot proves the frame it
drew; behaviour is pinned by the driven suites, the shots stand for appearance only).

What this did not prove: the harness runs a suite synchronously inside `_Ready`, before any
session builds, so no engine suite can let `Launcher` build a real Free Flight session and return
from it. `menu-host-tracer` proves the host's exit and re-show against the real launchscreen; the
launcher's side of it (`OnMenuExit` to `BeginLaunch`, the boards' Exit to
`ReturnToMenu(TopLevel)`) is the same code path the campaign debrief and the failed-build return
take. The real return is exercised at the controls: `.\RunDev.ps1`, Free Flight, a chapter, an
airframe, Select, FLY, then the pause board's Exit; the menu must re-enter on Mode with the chapter
and airframe cursors where they were and nothing selected.

**Verified.** The merged plan tree with B12 landed passed the complete `.\RunTests.ps1` (2861 units,
202 engine suites across 4 shards with `menu-host-tracer` among them, 18 goldens hash-identical,
engine errors clean, 188.4s with another agent's build running beside it). The real return from a
flown session remains owed at the controls, as the Verify paragraph states.

**⚠ Traps.** Re-entry after flight is part of the tracer. A presentation that only works on cold
startup has not proved the seam. The Plane screen lists `user://` custom planes, so a shot of it
is only comparable against a baseline taken over the same store.

## B13 ☑ Deliver Original Free Flight, switching and recovery

**Goal.** Original completes Free Flight with its own decoded screen graph, 4:3 presentation,
mouse/keyboard/pad interaction, audio cues, live switching and force-Built-in recovery.

**Evidence (confidence: lead-only).** `BoardFit` and current campaign boards prove the desired
scaling/rendering pattern, but A1/A2 must establish the non-campaign screen data. A1 established
that the original's top level has no Free Flight row and the archive ships no free-flight screen,
script or art (`docs/org/menu-inventory.md`); the user accepted an explicit remake-only rule under
Decision 11: **Original's top level gains a Free Flight row and screen of our design**, composed
from the decoded main-menu chrome and widget conventions once CAP-49 establishes them, and the
inventory marks that screen remake-only rather than evidenced.

**Approach.** Build the Original shell over decoded layouts and the shared Free Flight feature.
Implement pointer hit-testing/rollover and equivalent semantic navigation. Add the minimal Built-in
Options route and Original chooser, discard unfinished setup on switch, and restart at the target
presentation’s top level. Keep Original CLI-only.

Handoff from B12, what Original plugs into: registration is one more
`registry.Register(PresentationId.Original, () => new OriginalPresentation(...))` beside Built-in's
in `Launcher.BuildMenuHost`, and availability (the asset manifest) has to enter `MenuHost.Select`'s
availability answer, which today is registration alone. The host lends `Features`
(`Get<FreeFlightFeature>()`), `Audio` (`MenuAudioService`: `BeginNarration`/`EndNarration` are
live, `Cue` only logs since no cue table exists yet) and `Seats`, whose seat 0 is a `BuiltInSeat`
polling the keyboard and every unclaimed pad with no pointer; a mouse-first presentation needs a
seat that reports `MenuPointer`, and the join flow's later seats are still `LaunchMenu`'s own
`_slots` until C22. A switch is `host.Deactivate()` (frees the active presentation, discards
transient feature state), `host.Select(...)` and `host.Show(MenuReturnDestination.TopLevel)`;
the Options route that triggers it saves through `OptionsStore` and re-selects. Return mapping is
each presentation's `Activate(host, destination)` switch over `TopLevelReturn`, `CabinReturn` and
`DebriefReturn`; `Launcher` never names a screen, but `--menu=` is still applied by Built-in on
every top-level show (Decision 26), so an Original aid needs its own value under
`--presentation=original`. `Launcher.BuiltInMenu` is the one type-tested door onto the
launchscreen (debug aids, the failed-build note) and must stay null-safe when Original is active.

**Model recommendation.** high — first complete alternative presentation and first pointer path.

**Verify.** The landed shape: `MenuLayout` (`CSVM/src/UI/Menu/MenuLayout.cs`) is the engine-free
runtime reader of `menu_layout.json`, typed through the artifact's own kind table, and its first
consumer, so the extraction stamp is schema 2 (`$StampSchema` in `ExtractRof.ps1` and
`ExtractAssets.ps1`, `ExtractionStamp.Schema`, all in this commit). `OriginalShell`
(`CSVM/src/UI/Menu/Original/`) is the engine-free screen graph: the decoded top level (the two
panes and six `B` rows at authored corners, four-frame strips, the rows with no remake destination
drawing frame 0) plus the remake-only Free Flight door, Free Flight screen and minimal Options
screen, composed in the `FC_B_CHANGEPLANE` paper-plaque convention with the file-wide state
colours; `OriginalPresentation` draws it through `ComposedBoardView` on one `BoardFit` and maps
seat 0's pointer into the authored space; `PointerSeat` wraps the Built-in seat with the mouse;
`OriginalAvailability` is the minimal manifest (layout file, `[MainMenu]`, its art) and enters
`MenuHost.Select` through the new `Availability` delegate, a failure selecting Built-in with the
reason and leaving the saved request alone. Switching is a fourth typed exit,
`PresentationSwitchExit`, produced by Built-in's new Options door (the Mode screen's sixth row,
`--menu=options`, the one Built-in change) and by Original's Options screen, consumed by
`Launcher.SwitchPresentation` one frame later: save through `OptionsStore`, `Deactivate`
(discarding transient feature state), re-select from the saved request with the force flag still
winning and any `--presentation=` override dropped, `Show(TopLevel)`. Cues resolve through
`MenuCueTable` to `MOUSEOVER.WAV` and `MOUSECLICK.WAV` under the rof tree, played by
`MenuAudioService`. Original's `--menu=` aids are `free-flight` and `options` under
`--presentation=original` (`docs/cli.md`, flag index unchanged at 150).

Checked: `dotnet build CSVM/CSVM.sln`, the whole `dotnet test` (2883, of which 7 are
`MenuLayoutReaderTests` over the artifact the decoder emits from the probe fixtures plus the
install's census, 13 `OriginalShellTests` over the invented `fixtures/menu-layout-original`
layout covering pointer hit-testing and rollover, the disabled rows, keyboard focus by column, the
picks and the launch, list rows under the pointer, Back and Quit, the return, the Options toggle
and its switch exit, the composed frames and the pointer overlay, the inks, and a layout with no
plaque row; 2 `MenuCueTableTests` for the cue names and the pointer seat's click edge; one new
`MenuHostTests` case for the availability delegate; `MenuNamespaceDependencyTests` green with the
reader in the shared namespace and Original under it), then
`.\RunTests.ps1 -Suite "menu-original-tracer,menu-host-tracer,menu-free-flight-journey,menu-zone-layout,menu-screenshot-key,campaign-briefing-repaint" -SkipUnits -SkipGoldens`
(6 suites, 6 passed, engine errors clean), `.\CheckCommentCaps.ps1 -Summary` and
`.\CheckEncoding.ps1`. `menu-original-tracer` (`CSVM/src/Testing/MenuOriginalSuites.cs`) is the
tracer over the install's own layout: `--presentation=original` selects Original, `Show(TopLevel)`
lands on the door with the OS pointer hidden, a pointer frame in window pixels over Quit takes
focus and cues one rollover while one over the disabled Campaign plaque does neither, a click on
the door opens Free Flight, keyboard frames pick Hollywood and the Bloodhawk and Up wraps onto FLY,
whose Accept leaves as one `LaunchExit` with the presentation hidden, the return re-enters the top
level with the airframe pick dropped and a debrief return maps there too, `Deactivate` from
mid-setup discards the feature's pick and the saved request re-selects Built-in on its Mode screen
(six rows), Built-in's Options route emits a `PresentationSwitchExit` through the host, the saved
request re-selects a fresh Original, the force flag beats an Original override keeping the request,
and a data root with no layout selects Built-in with a reason naming `menu_layout.json` and the
request kept. `menu-free-flight-journey` has one edit: the Mode screen's row count is six and the
last row is the Options door. Shots: rebuilt, then Godot launched on the hidden desktop with
`--resolution WxH` ahead of the `--` (RunProbe.ps1 puts every argument after it, so a sized probe
needs its own launcher) and `--presentation=original --menu[=free-flight|options] --screenshot=`
into `.scratch\b13-shots\`: the top level at 800x600, 1024x768, 1280x720, 1920x1080 and 600x750,
the Free Flight screen at 800x600, 1280x720, 1920x1080 and 600x750, the Options screen at 1024x768,
Built-in's Mode and Options screens at 1280x720. B12's four Built-in aids (`mode`, `chapter`,
`plane`, `selected`) were re-shot at 1280x720 and compared by decoded 32bpp pixels (SHA-256 over
the rows) against `mp-b12\.scratch\b12-after\`: `chapter` identical; `mode` differs in rows
415 to 437 only, the added Options row (948 pixels); `plane` and `selected` differed across the
list because a custom plane (`Accipiter Annie`) was saved to `user://` after B12's shots, so the
baseline was re-measured near the changed run (METHOD-3) by shooting both aids from B12's own
still-built binary in its worktree against today's store, against which both are identical.
`docs/verification.md` rules that bit: **METHOD-3** (the store moved under the baseline again),
**METHOD-6** (which binary each side used is named: B12's worktree build, unchanged since its
commit, against B13's), **SHOT-6** (decoded pixels, never PNG bytes), **SHOT-9**/**SHOT-10**
(windowed probes on the hidden desktop, absolute paths, files checked present), **SHOT-32** (a shot
proves the frame it drew; the pointer sits wherever the probe's mouse was, off the board in these
runs, and the rollover, depressed and cue behaviour is pinned by the driven suites, not the shots),
**SRC-7** (the four-frame strips and the two script-bound wavs are the evidence for rollover and
press; keyboard focus, the door's placement, list-row focus and the pointer hotspot are recorded as
remake-only until CAP-49 and CAP-52 are filmed, in `docs/org/menu-inventory.md`).

What this did not prove: the harness runs a suite before any session builds, so no engine suite
lets `Launcher` fly an Original launch and return, nor perform the frame-deferred
`SwitchPresentation`; both are the same `OnMenuExit`/`ShowMenu` path the campaign debrief and
the failed-build return take, and the switch's three host calls are what the tracer performs by
hand. Sound is not heard by a suite: the cue table resolves and the service loads the wavs, which
the log records at debug level. The real switch and the sounds are exercised at the controls:
`.\RunDev.ps1`, Options, Original, Apply; the top level must appear with the pointer drawn and a
rollover sound on entering a plaque; Free Flight, a chapter, an airframe, FLY, then the pause
board's Exit must re-enter Original's top level.

**Verified.** The merged plan tree with B13 landed passed the complete `.\RunTests.ps1` (2883 units,
203 engine suites across 4 shards with `menu-original-tracer` among them, 18 goldens hash-identical,
engine errors clean, 154.2s). The landed extractor was run against the user's live
`extracted/rof` tree, which now carries `menu_layout.json` (archives up to date, nothing
re-extracted). The pointer, the rollover and depressed frames, the sounds, the real switch and the
real return from a flown session remain owed at the controls, as the Verify paragraph states.

**⚠ Traps.** The 800x600 space is authored coordinates, not a fixed render target. Do not stretch
wide or switch to integer-only scaling; `BoardFit` already records both rejected alternatives.
The Plane screen lists `user://` custom planes, so a shot of it is only comparable against a
baseline taken over the same store. The layout carries no art sizes: a widget's hit rectangle is
its strip measured and divided by `frames`, so a missing strip has a fallback rectangle and no
picture.

# Wave C — Setup features

## C21 ☑ Extract Instant Action into a typed shared feature

**Goal.** Both presentations configure the same `InstantActionDef` through typed state and
operations while retaining independent screen graphs.

**Evidence (confidence: traced).** Built-in uses a five-step wizard and skips Waves/Wingmen for an
ace; `LaunchMenu` contains decoded environments, mission types, militias, waves and presets
(`docs/architecture.md:3901-3913`; `CSVM/src/UI/LaunchMenu.cs:108-232`, `2169-2206`).

**Approach.** Characterize presets, validation, Back paths, lives, wave editing, wingmen and launch
payload. Extract renderer-neutral setup state and operations; adapt Built-in unchanged, then bind
Original’s evidenced screens to the same feature.

Handoff from B13: Original's top level draws `MM_B_INSTANTACTION` in its disabled frame and
ignores it (`OriginalShell.BuildRows`, the `enabled` argument of `Button`); C21 enables the row
and gives it a screen. The shell keys every screen's rows by the layout's widget keys, composes
through `ComposedBoard` and reads its plaque convention off `FlightCheck.FC_B_CHANGEPLANE`; an
Original Instant Action screen is another `OriginalScreen` member with its own `BuildRows` and
`Activate` branch, and its rows' sizes come from the injected art measurer, not the layout.

**Model recommendation.** high — dense decoded rules and many dependent fields.

**Verify.** Characterization first, on the pre-extraction code: the new engine suite
`menu-instant-action-journey` (`CSVM/src/Testing/MenuInstantActionSuites.cs`) drives a real
`LaunchMenu` through `LaunchMenu.Drive(MenuCommands)` and the `Shown*` read-outs: Mode to
Environment (seven rows, the region detail, the wrap), the Table of Contents (Contents opens it
from Environment and nowhere else, Back leaves the fields untouched, Accept applies Girl Trouble and
puts its name in the breadcrumb, reopening lands on the applied preset), Mission type with the
lives stepper (clamped at unlimited and at nine), the ace skip forward to the Aircraft screen and
back to Mission, Waves (cursor parked on Continue, the preset's waves read back), the wave editor
(a count step, a militia step resetting the aircraft, a skill step, Back keeping the edit), Wingmen
(the aircraft row hidden at zero, the loadout gated on wingmen, the wingman loadout's heading),
Back at every step, the `LaunchExit` with its `InstantActionDef` (C4's own ace, the edited and
preset waves, the unused slots as `InstantAction.EmptyWave`, the nominal player aircraft), the
fields surviving a `HideMenu`/`ShowMenu` return, the aids `--menu=presets|environment|missiontype|waves|wingmen|wingmanloadout`
forcing the mode, `DebugWaves(2)`/`DebugWingmen(3)`/`DebugPreset(2)` with a second launch through
the ace skip, and the clouds' three-row mission list. Green before the extraction and green after
it with one mechanical edit, the shared `MenuSuiteHost.Bare` setup line taking the data root the
feature reads environment defs from (the same line in `menu-free-flight-journey`). Then the
extraction: `dotnet build CSVM/CSVM.sln`, the whole `dotnet test CSVM.Tests/CSVM.Tests.csproj`
(2909, of which 13 `InstantActionFeatureTests` over the option sets, the rules, a preset, the gate,
the def and the discard against a fake environment loader; 13 `OriginalInstantActionTests` over the
fixture's new `[@InstantAction@]` section covering the live top-level row, the opening rows,
a preset on select and the title on View Story, the scrolling window, the dropdown list by click
and by sideways step with the clouds barred under stunt flying, the enemy pages and the ace
hiding, the radio pair, Back and Exit, Fly Mission's exit, the inks, and a layout with no section;
`InstantActionPresetsTests` moved with the table into `CSVM.UI.Menu`; `OriginalShellTests` with four
mechanical edits for the now-live Instant Action row (the enabled array and three cursor walks);
`MenuNamespaceDependencyTests` green with the feature and the presets in the shared namespace),
then `.\RunTests.ps1 -Suite "menu-instant-action-journey,menu-original-instant-action,menu-free-flight-journey,menu-host-tracer,menu-original-tracer,menu-zone-layout,menu-screenshot-key" -SkipUnits -SkipGoldens`
(7 suites, 7 passed, engine errors clean), `.\CheckCommentCaps.ps1 -Summary` and
`.\CheckEncoding.ps1`. `menu-original-instant-action` is the Original half over the install's own
layout: `--presentation=original` selects Original, a pointer click on the live `MM_B_INSTANTACTION`
plaque opens the decoded screen with the first environment's own def loaded, the contents window is
the layout's fourteen rows, the player plane dropdown stands at its authored line with the
Autogyro, the ace duel opens with no enemy row, Exit is the measured strip and Build and Weapon
Loadout are disabled, keyboard frames cross from the contents column to the dropdown column, step
the player plane sideways, open its eleven-row list and pick by Down and Accept, and show and hide
the wingman plane with the count; then four presets (Me and My Big Mouth, Girl Trouble, Sour
Grapes, The Angry Luau: an ace duel over C1B, a squadron over C4, a stunt run over C5, a zeppelin
run over C3) are each selected by a click and flown by Fly Mission, leaving as one `LaunchExit`
for seat 0 in the preset's airframe with the preset's def over the environment's own ace, from
which `SessionSpec.FromMenu` derives the chapter, the scenario and the stunt flag the launcher
would build, the host hiding the presentation on each exit and a top-level re-show re-entering
with the setup kept. Shots: `.\RunProbe.ps1 --menu=<aid> [--debug-waves=2 | --debug-wingmen=2] --screenshot=<abs path>`
for `presets`, `environment`, `missiontype`, `waves` and `wingmen` from the pre-extraction binary
into `.scratch\c21-before\` and from the extracted one into `.scratch\c21-after\`, compared by
decoded 32bpp pixels (SHA-256 over the rows): all five identical. The Plane screen was not shot,
since its roster reads `user://` customs and a shot of it is only comparable against a baseline
over the same store. Original: Godot launched on the hidden desktop with `--resolution WxH` ahead
of the `--` and `--presentation=original --menu=instant-action --screenshot=` into
`.scratch\c21-shots\` at 1024x768 and 1920x1080, plus the top level at 1024x768 with its Instant
Action plaque live; the shots stand for composition on the opening state only, and cannot show the
rollover and pressed frames, an open list, the second enemy page or a non-ace state, which the
driven suites pin. `docs/verification.md` rules that bit: **SHOT-6** (decoded pixels, never PNG
bytes), **SHOT-9**/**SHOT-10** (windowed probes on the hidden desktop, absolute paths, files checked
present), **SHOT-32** (a shot proves the frame it drew, so behaviour is pinned by the driven suites
and the shots stand for appearance), **METHOD-3** (the five compared aids avoid the store on
purpose, so no baseline had to be re-measured), **SRC-7** (the paged enemy rows, the ace hiding, the
militia reset, the preset on select and the title on View Story are the script's and the
executable's stated behaviour; the open list under its box, both halves shown together, keyboard
stepping and the two disabled buttons are recorded as remake-only until CAP-50 is filmed, in
`docs/org/menu-inventory.md` Part 4 and `playtest.md`'s CAP-50 row).

What this did not prove: the harness runs a suite before any session builds, so the four Original
launches are typed exits and derived specs, not built worlds; the build is the same
`OnMenuExit`/`StartSessionFromMenu` path Built-in's Instant Action launch takes, which
`menu-instant-action-journey` proves up to the same exit. The real flights are exercised at the
controls: `.\RunDev.ps1 --presentation=original`, Instant Action, a contents row, Fly Mission, then
the pause board's Exit must re-enter Original's top level with the setup kept; and Built-in's own
wizard flown once end to end.

**Verified.** The plan tree with C21 and C22 merged (thirteen conflicting files resolved as the
union of both items, every hunk of each side checked present) passed the complete `.\RunTests.ps1`
(2943 units, 207 engine suites across 4 shards, 18 goldens hash-identical, engine errors clean,
142.6s). The four representative Original launches reach the launcher's sink; a built world from
them is owed at the controls.

**⚠ Traps.** Shared feature state does not imply a shared wizard. Preserve the ace skip in Built-in;
Original follows its own evidenced navigation.

## C22 ☑ Extract shared player setup and deliver Dogfight in both presentations

**Goal.** One device-neutral player-setup feature owns seats, device claims, aircraft choices and
locks; Built-in remains unchanged and Original offers equivalent complete setup.

**Evidence (confidence: lead-only).** Current joining is polled per player and restricted to the
Plane screen; `MenuInput` and `LaunchMenu` currently encode pad concepts directly
(`docs/architecture.md:3908-3910`; `CSVM/src/UI/LaunchMenu.cs:916-1043`). The device-neutral boundary
and future HOTAS/HOSAS compatibility were settled in the grilling.

**Approach.** Characterize join/unjoin/lock and launch gates, extract semantic per-seat commands and
player setup, then adapt keyboard, mouse and current pads. Preserve Built-in split-pane selection;
build Original’s graph and presentation from A1 evidence. Do not implement HOTAS/HOSAS bindings.

Handoff from B13: seat 0 is a `PointerSeat` (`CSVM/src/UI/Menu/Original/PointerSeat.cs`) over the
`BuiltInSeat`, adding the mouse as `MenuPointer`; `Launcher.BuildMenuHost` keeps the inner
`BuiltInSeat` for the launchscreen's pad bookkeeping. Original polls `host.Seats[0]` only and
launches one `MenuSeatChoice` with no pads (`OriginalShell.ActivateFreeFlight`); its airframe list
is the eleven stock nodes from `OriginalRosters`, with no custom planes and no join strip. The
join flow, the per-seat pointer mapping and the roster with customs are C22's to add on the same
`OriginalScreen.FreeFlight` rows.

Handoff from C21: Original's Instant Action screen (`OriginalInstantAction.cs`) launches seat 0 on
the feature's `PlayerPlane` node with no pads and no fit, its `IA_D_PLANEP` dropdown lists the
eleven stock airframes (the layout's 20-row window is authored for stock plus the saved customs),
`IA_B_CHANGEWEAPONS` draws disabled and the Player/Wingman radio only records
`OriginalShell.LoadoutTarget`, so the customs in that dropdown, the seat's fit and the loadout
screen the button opens are C22's to add on the same rows. Built-in's `FireLaunch` still builds the
Dogfight exit itself; the Instant Action branch is `InstantActionFeature.BuildExit(seats,
nominalPlayerPlane)`, so a shared player setup that owns `SeatChoices` hands the same list to both
features.

**Model recommendation.** high — multiplayer device ownership and same-frame input races are fragile.

**Verify.** Characterization first, on the pre-extraction code: the engine suite
`menu-player-setup-journey` (`CSVM/src/Testing/MenuPlayerSetupSuites.cs`) drives a real
`LaunchMenu` through `Drive` with one to four seats (`DebugJoin` adds the device-less ones) and
pins a lone seat's two-stage pick with Back at every stage and the launch it ends in, a second seat
splitting the screen and holding the gate until it confirms, player 1's Back unselecting everyone,
the Dogfight gate waiting with `(Dogfight needs a fight — P2: press START to join)`, the four-seat
maximum, the `selected`/`loadout` aids under two seats and a return keeping the seats; it passed
green before the extraction and unchanged after it.

The landed shape: `PlayerSetupFeature` (`CSVM/src/UI/Menu/PlayerSetupFeature.cs`, engine-free,
in the host's feature set) owns the seats as `PlayerSeat`s claimed by `IMenuInputSource` identity
(`Join`/`Unjoin`/`IsClaimed`/`SeatOf`, four seats, seat 0 never leaving), the shared aircraft
roster (`MenuAircraft`, `BuildRoster(stock, customs, nodeOfAirframe)`: the stock rows then the
saved customs with their defs), each seat's cursor, `Browse`, the two stages `Select`/`Confirm`,
`Back` a stage at a time, `OpenLoadout`/`CloseLoadout`, `ResetPicks`, the gate per mode
(`MinimumSeats`, `Refusal`, `CanLaunch`), `Choices(flightDevices)` as the `MenuSeatChoice` list
and `BuildExit(chapter, mode, flightDevices)` for Dogfight; `Discard` drops every seat but the
first. It reads no pad: the devices behind a seat are asked of the presentation. `MenuIdleSource`
is the "no device" source the aids seat. `MenuSeatDevices` (`CSVM/src/UI/MenuSeatDevices.cs`,
engine-side) is the pad bookkeeping both presentations share: seat 0's claimed pad, `Sync`
(hotplug), `PrimeJoins`/`ScanJoins` (Start on an unclaimed pad joins a `BuiltInSeat` over a
one-pad poller), `FlightPads` (the binding a launch carries). `MenuHost.Seats` lends the feature's
live source list once the feature is registered, and `AddSeat` joins through it, so the feature
goes in before seat 0 (`Launcher.BuildMenuHost`). Built-in: `LaunchMenu` keeps `_slots` as a view
over the feature's seats (`Slot` wraps a `PlayerSeat`, the poller behind it and its last frame;
`SyncSlots` keyed by the feature's `Revision`); every stage on the Plane screen, `ShowMenu`'s
reset and aids, `DebugJoin`, the guest unjoin, the gate and `SeatChoices` go through the feature
and `MenuSeatDevices`; the split-pane layout, join strip, lock colours, hint texts and the
per-seat loop order are untouched, and `FireLaunch`'s Instant Action branch is untouched. Original:
`OriginalShell` is `partial`, its sortie screens in `CSVM/src/UI/Menu/Original/OriginalSeats.cs`:
a Dogfight door under the Free Flight door and a Dogfight screen in the Free Flight screen's shape
(remake-only under Decision 11, recorded in `docs/org/menu-inventory.md`); the aircraft column is
the shared roster with the customs in an eleven-row window that follows the focus (rows outside it
`Visible == false`, undrawn and unhit); each later seat's cursor is tagged on its row and listed in
a seat strip; FLY is seat 0's confirmation and the launch, enabled once a chapter and an aircraft
are picked, every later seat has confirmed and Dogfight's second seat has joined, leaving through
`FreeFlightFeature.BuildExit` or the setup's `BuildExit(chapter, Versus)`; a later seat's frame
(`StepSeat`) walks, selects, confirms and unjoins; `OriginalPresentation` polls every host seat,
maps any pointer through `BoardFit`, scans pad joins on the sortie screens, refreshes the roster
from the store on every `Activate`, and takes seat 0's `MenuInput` and the `--debug-join` count.
Original's aids gain `dogfight`; `--debug-join` seats device-less players there too (`docs/cli.md`,
two bullets reworded, no flag added, the flag index untouched).

Checked: `dotnet build CSVM/CSVM.sln`, the whole `dotnet test` (2917, of which 18 are
`PlayerSetupFeatureTests`: claims by identity, the four-seat cap, the stages and Back, browsing
and the fit, the loadout list, the gate against `LaunchMenu.CanLaunch` case by case, the choices
and exit, the roster rule against `PlanePickerRoster.Build` row for row, the same-frame races (two
claims in arrival order, a lock and an unjoin in either order, a confirm from a source that has
left), `Discard` and the host lending the seat list; 9 are `OriginalSeatsTests` over the fixture
layout: the Dogfight door and gate, a second seat walking, selecting and confirming with its tag,
FLY for both modes, Back undoing seat 0's pick first, a guest's Back, the re-pick by click, the
window over a roster with seven customs and the hint at each stage; `MenuNamespaceDependencyTests`
green with the feature and the idle source in the shared namespace), then
`.\RunTests.ps1 -Suite "menu-player-setup-journey,menu-player-setup-seats,menu-free-flight-journey,menu-host-tracer,menu-original-tracer,menu-zone-layout,menu-screenshot-key,campaign-briefing-repaint" -SkipUnits -SkipGoldens`
(8 suites, 8 passed, engine errors clean), `.\CheckCommentCaps.ps1 -Summary` and
`.\CheckEncoding.ps1`. `menu-player-setup-seats` joins scripted sources through the feature: in
Built-in a second seat picks and confirms through `_Process`, Free Flight and Dogfight launch for
both seats, a return keeps the seats, a guest's Back unjoins, four seats close the join and a fifth
and a repeat claim are refused, a lock and an unjoin land on one frame, `Deactivate` leaves one
seat; in Original the Dogfight door, FLY disabled with its hint, a joined seat's walk through the
presentation's `Tick`, both launches for two seats, a guest's Back on the top level, `Deactivate`.
Mechanical edits to existing suites: `MenuSuiteHost.Bare`, `menu-host-tracer` and
`menu-original-tracer` register the `PlayerSetupFeature` before the seat, the tracer hands
`OriginalPresentation` a `MenuInput` and counts eight top-level rows; `menu-free-flight-journey`
and the characterization suite are unchanged. `OriginalShellTests` edits are mechanical: the
Dogfight door in the row lists and focus walks, aircraft rows keyed `AIRFRAME:<index>`, the
constructor's setup argument. Shots: rebuilt, then `.\RunProbe.ps1 --menu=<aid> --screenshot=<abs path>`
for `plane`, `selected`, `loadout`, `plane --debug-join=2` and `plane --debug-join=4` before any
edit into `.scratch\c22-before\` and after the extraction into `.scratch\c22-after\`, compared by
decoded 32bpp pixels (SHA-256 over the rows): all five identical over the same `user://` store,
no custom plane appeared mid-run, so no rebaseline was needed. Original shots through a sized
launcher (`--resolution WxH` ahead of the `--`) with
`--presentation=original --menu=free-flight --debug-join=1`, `--menu=dogfight` and
`--menu=dogfight --debug-join=1` at 1024x768 and 1920x1080, plus the top level with both doors at
1024x768, into `.scratch\c22-shots\`. What a pose cannot show: the pointer sits off the board, the
join gesture and pad hotplug happen between frames, a device-less seat reads "no device" where a
pad would read `pad <n>`, and the window shows eleven of this store's eighteen rows with the scroll
mark standing for the rest; behaviour is pinned by the driven suites. `docs/verification.md` rules
that bit: **METHOD-6** (which binary each side used: the before shots from this worktree built at
the plan branch's tip before any edit, the after shots from this tree), **METHOD-3** (the store
was checked for a mid-run custom plane before trusting the identical result), **SHOT-6** (decoded
pixels, never PNG bytes), **SHOT-9**/**SHOT-10** (windowed probes on the hidden desktop, absolute
paths, files checked present), **SHOT-32** (a shot proves the frame it drew), **SRC-7** (no script
or layout row describes a local join or a split-screen Dogfight, so both are recorded as
remake-only, not decoded).

What this did not prove: no real pad was connected on the probe rig, so the claim by steering,
the Start join, hotplug and a two-pad launch's binding are exercised only through scripted
sources and the device-less aid; the mouse over Original's seat strip and aircraft tags is not
driven by a suite; and the harness cannot fly and return a two-seat launch. The hardware pass is
owed at the controls: `.\RunDev.ps1`, a pad pressing Start on Built-in's aircraft screen and on
Original's Free Flight and Dogfight screens, the joined pad walking, selecting and confirming,
Back unjoining it, FLY launching two seats with each pad flying its own pane, and a pad
unplugged mid-setup leaving its seat.

**Verified.** The plan tree with C21 and C22 merged passed the complete `.\RunTests.ps1` (2943 units,
207 engine suites across 4 shards with both player-setup suites among them, 18 goldens
hash-identical, engine errors clean, 142.6s). The hardware pass with a real pad remains owed at the
controls, as the Verify paragraph states.

**⚠ Traps.** A menu input source is not synonymous with a pad. Presentation-specific join gestures
must converge on one shared roster and launch gate. The Plane screen lists `user://` custom
planes, so a shot of it is only comparable against a baseline taken over the same store.

## C23 ☑ Separate hangar features from composition and deliver its Original screens

**Goal.** Hangar rules and scratch-plane lifecycle are presentation-neutral, while Built-in and
Original compose and navigate them independently.

**Evidence (confidence: lead-only).** `HangarFlow` is engine-free and owns order/state/rules, but its
`IHangarPage` contract exposes rows, art and detail shaped for today’s shell
(`CSVM/src/UI/HangarFlow.cs:32-97`; `docs/architecture.md:4139-4151`).

**Approach.** Characterize all nine screens, cancel residue, edit/delete and commit. Split typed
hangar feature state/operations from Built-in page composition without changing store semantics.
Bind Original screens to decoded layouts and reuse shared visual components where faithful.

Handoff from C22: the aircraft roster both presentations pick from is
`PlayerSetupFeature.Roster`, set through `SetRoster` with `PlayerSetupFeature.BuildRoster(stock,
customs, PlanePickerRoster.AirframeNode)`; Built-in refreshes it in `RefreshRoster` (on `ShowMenu`
and `CloseHangar`), Original in `OriginalPresentation.Activate` from the store, so an Original
hangar exit that saves a plane must call `SetRoster` again for the new row to appear without a
return to the top level. `OriginalShell` is `partial` (`OriginalSeats.cs` holds the sortie
screens); a hangar screen is another `OriginalScreen` member with its own `BuildRows`, `Activate`
and `Compose` branches, and `_focus` is sized from the enum.

**Model recommendation.** high — wide feature surface with persistence and dynamic preview art.

**Verify.** Characterization first, on the pre-extraction code: the engine suite
`menu-hangar-journey` (`CSVM/src/Testing/MenuHangarSuites.cs`) drives a real `LaunchMenu` through
`Drive`/`Shown*` from both doors (the Mode screen's `Build Custom Plane` row, returning to Mode, and
the Instant Action plane pick's trailing row under the ace duel, returning to Plane), through plane
selection, the airframe list (the first confirm picks and raises the langui 206 ask as two rows,
Cancel keeps the picks and lands on the ticked row, the second confirm advances), engine (opening
on the standing pick, a pick then an advance), armor, guns and hardpoints (the stepper), paint (ten
rows, the pattern stepping only onto wearable patterns, a preview composed), the name screen (a
rolled name, the adjective stepper, typing through the page as the key handler does) and the
purchase review (the totals row detailing `TotalsLine`, Purchase Now last), commits a scratch plane
named `Scratch <8 hex>` into the user's store (the plane pick then opens with the cursor on it),
edits it from the plane list and cancels with its file byte for byte unchanged, opens the
`hangar`/`airframe`/`defaults`/`name`/`paint` aids, opens the campaign wallet door through
`--menu=campaign-hangar` over the aid's scratch profile (INVENTORY, Buy a New Plane over `$$$ on
Hand:`, the sale stage listing every owned plane, Back resuming the cabin), drops an open build on
`DiscardTransient`, and deletes the scratch plane through the two-stage list; it passed green before
the extraction and, with no edit at all, after it. The scratch plane is the only write into
`user://Planes/`, checked absent before the run and removed in `finally`.

The landed shape: `HangarFeature` (`CSVM/src/UI/Menu/HangarFeature.cs`, engine-free, in the host's
feature set) owns one build at a time: `Open(store, wallet)` over a `CustomPlaneStore` and an
optional `IHangarWallet` (the interface `HangarCampaignContext` implements), the roster `Saved`,
the scratch plane's three starts (`StartNewPlane`, `StartDefaultPlane` on the Devastator's stock
configuration, `StartFromSaved` as a copy), `PickAirframe` with the defaults ask
(`DefaultsAsk`/`DefaultsAskText`/`AnswerDefaultsAsk`/`LoadAirframeDefaults`), the per-tab
operations (`SetEngine`, `SetArmour`, `SetGun` over the eleven-row gun cycle, `SetHardpoints`,
`SetPattern` over `WearablePatterns`, `SetColour`, `SetShade`, `SetDecal`), the gate `Refusal`
in the original's words with `CanCommit` and `Bill`, `Commit` (save, then the wallet's purchase),
`DeleteSaved` (delete, or the wallet's sale with its two refusals), `IsNameTaken` over the whole
build directory, `Overwrites`, the label helpers (`AirframeName`, `EngineName`, `GunName`,
`GunCycleName`, `ArmourLabel`, `HardpointsLabel`, `PatternLabel`, `TotalsLine`, `CannotSellText`),
the name rules (`AcceptsNameChar`, `MaxNameLength`) and `Discard`, which drops the build and
touches nothing saved. `HangarFlow` keeps its whole surface and its nine-screen order and delegates
every rule and every store operation to the feature (`Feature`), constructed by `LaunchMenu` over
the host's one instance and by its old constructor over a private one for the unit tests;
`HangarFlow.LoadStockWeapons` and `StockWingCounts` forward to the feature for the campaign pages.
Original: `OriginalHangar.cs` (another `partial` of `OriginalShell`) adds a BUILD PLANE door
under the Dogfight door (remake-only) opening the decoded `[@PlaneName@]` screen (the edit box fed
by the seat's typed characters while `CapturingText`, the `MP_B_CheckBox8States` Load Default
Configuration box, OK refusing an empty name with langui 203, Cancel), then the Plane Construction
hub composed from `[@PlaneConstruction@]` (the background, the plane as the focused airframe's
blueprint on the airframe tab and the paint composite of the `PX_ICON_<af>_<pattern>_0..3` set
tinted through `BoardPicture.Tint` elsewhere, PLANE NAME, PLANE COST, AIRFRAME, WEIGHT
CAPACITY, CURRENT WEIGHT reading Pending before a pick, the agility and armour words with their
`PX_BarGraph` segments, the cash note only over a wallet) with one of the six tab sections on its
right page (`AF_D_AIRFRAME`, `EN_D_ENGINE`, `AR_D_POINT0..3`, `GN_D_GUN0..3`, `HP_D_POINT0..1`,
`PT_D_PATTERN`, `PT_D_COLORS0..2` as swatches, `PT_D_SHADES0..2`, `PT_D_DECALS0..2` as tiles off
`PT_P_DECALS`, each a `Dropdown` row at its authored box whose Accept opens the list under it in the
row's `TotalDisplayed` window with the `DropUp`/`DropDown` arrows, a sideways step picking the next
value), the six `PX_Tab` tabs with the standing one disabled, `PX_B_Sell` into the `[@Hangar@]`
INVENTORY (`HA_D_PILOTPLANE` over `Saved`, `HA_B_SELLP` through `DeleteSaved`, `HA_B_EXPORTP`
disabled, `HA_B_DONE`), `PX_B_Ready` into the `[@Purchase@]` totals page (`PUR_B_PURCHASE` live
while `CanCommit`, the problems text in the commit's words) and `PX_B_Cancel`; the wallet-free door
wears `PX_B_ReadyToExport`/`PX_B_CancelExport` when the extraction has them. A commit calls
`SetRoster` from the store, so the new plane is offered without a return to the top level; a
cancel, Back off the name screen or a tab, and a presentation switch drop the scratch plane through
`Discard`. `OriginalPresentation` sets seat 0's `CapturingText` from the shell around every frame,
draws the hub in `PaletteFor(HangarInks)` and the inventory in the paper palette, and takes the
aids `plane-name`, `plane-construction`, `plane-paint`, `plane-purchase` and `plane-inventory`
(`docs/cli.md`, no flag added, the flag index untouched). `IA_B_BUILD` on the Instant Action
screen stays disabled: the decode names no edge for it.

Checked: `dotnet build CSVM/CSVM.sln`, the whole `dotnet test` (2967, of which 12 are
`HangarFeatureTests`: the roster and the bare start, the default configuration, the copy from a
saved plane, the ask and both answers, the gate's three refusals then the commit, overweight, a
fake wallet's funds and availability refusals and its purchase, ownership versus taken names, the
sale's two refusals and the deletion, the paint rules, the dropdown labels, and `Discard`; 12 are
`OriginalHangarTests` over the fixture layout, extended with the nine hangar sections under the
shipped keys on invented lines: the door and the name screen's typing and OK gate, both answers of
the defaults box, the ask as a dialog, the tabs as siblings with a dropdown stepping and picking,
the keyboard onto and along the bar, the windowed colour list and the decal tiles, the totals
page committing into a scratch store and the roster, the disabled Purchase Now with its reason,
the two cancels leaving no residue, the inventory's sell, the chrome's composition, and the
feature's discard; `MenuNamespaceDependencyTests` green with the feature and the wallet interface
in the shared namespace), then
`.\RunTests.ps1 -Suite "menu-hangar-journey,menu-original-hangar,menu-free-flight-journey,menu-player-setup-journey,menu-player-setup-seats,menu-instant-action-journey,menu-original-instant-action,menu-host-tracer,menu-original-tracer,menu-zone-layout,menu-screenshot-key,campaign-briefing-repaint,hangar-door-wake,landings-hangar-drop-gate,campaign-hangar-handover" -SkipUnits -SkipGoldens`
(15 suites, 15 passed, engine errors clean), `.\CheckCommentCaps.ps1 -Summary` and
`.\CheckEncoding.ps1`. `menu-original-hangar` drives Original through a real `MenuHost` over the
install's layout: the door, the name screen at its authored box with seat 0 capturing text, OK onto
the default configuration, Paint then Engine by click out of order, a sideways step changing the
engine with the running total following, the seven-row list, the keyboard onto the tab bar and along
it past the standing tab, READY TO PURCHASE and Purchase Now committing the scratch plane into the
user's store and the shared roster, SELL PLANES and the inventory's Sell removing it again, CANCEL
and `Deactivate` leaving no residue. Mechanical edits to existing suites and tests:
`MenuSuiteHost.AddFeatures` registers the `HangarFeature` (strings, stock fits on first need and
the data root's zrdr scope, as `Launcher.BuildMenuHost` does); `menu-original-tracer` counts nine
top-level rows; `OriginalShellTests` gains the door in the row lists and its disabled state, and one
hover index moves by one; `MenuLayoutReaderTests.OriginalLayout` defines the hangar strings and
lists the fixture's invented art. Shots: rebuilt, then `.\RunProbe.ps1 --menu=<aid> --screenshot=<abs path>`
for `hangar`, `name`, `airframe`, `defaults` and `paint` from a second worktree checked out at the
plan branch's tip and built there (the before) and from this tree (the after), into
`.scratch\c23-before\` and `.scratch\c23-after\`, compared by decoded 32bpp pixels (SHA-256 over
the rows, `CompareShots.ps1`): all five 1280x720 shots identical, zero differing pixels, over the
same `user://` store, which the suites left as they found it. Original shots through a sized
launcher (Godot's `--resolution WxH` ahead of the `--`, `RunSizedProbe.ps1`) with
`--presentation=original --menu=plane-name|plane-construction|plane-paint|plane-purchase|plane-inventory`
and the top level with the three doors, at 1024x768 and 1920x1080, into `.scratch\c23-shots\`.
What a pose cannot show: the pointer sits off the board, the typed name arrives between frames, an
open list and the defaults ask are states a click reaches, the running total moves only on a pick,
and Purchase Now's press ends the screen; behaviour is pinned by the driven suites.
`docs/verification.md` rules that bit: **METHOD-6** (which binary each side used: the before shots
from a worktree at the plan branch's tip, built there, the after shots from this tree),
**METHOD-3** (the store was checked for the scratch name before the run and for its absence after),
**SHOT-6** (decoded pixels, never PNG bytes), **SHOT-9**/**SHOT-10** (windowed probes on the hidden
desktop, absolute paths, files checked present), **SHOT-32** (a shot proves the frame it drew),
**SRC-7** (no layout row or script edge describes the top-level door, the export wording on the
wallet-free door or `IA_B_BUILD`'s destination, so the first two are recorded as remake-only and
the third stays disabled).

What this did not prove: `CAP-53` is not filmed, so whether a tab is ever disabled, where the
running total shows, whether leaving a tab commits, what SELL PLANES does and what Load Default
Configuration loads are remake readings the capture must confirm (the exact list is in
`playtest.md`'s `CAP-53` row and `docs/org/menu-inventory.md`, Part 4); the mouse over Original's
hub is driven only by scripted pointer frames; the campaign wallet door is exercised in Built-in
alone, since Original has no cabin until Wave D; and no real pad or keyboard typed on the name
screen. The hardware pass is owed at the controls: `.\RunDev.ps1 --presentation=original`, BUILD
PLANE, a name typed, the tabs clicked and walked from the pad, a plane bought and sold.

**Verified.** The plan tree with C23 landed passed the complete `.\RunTests.ps1` (2967 units, 209
engine suites across 4 shards with both hangar suites among them, 18 goldens hash-identical,
engine errors clean, 148.8s). The user's custom-plane store holds no scratch plane afterwards.
CAP-53 and the campaign-wallet door through an Original cabin remain owed, as the Verify
paragraph states.

**⚠ Traps.** Cancelling remains residue-free by dropping the scratch plane; do not replace that with
an undo path. Presentation switching also discards the scratch flow without committing.

# Wave D — Campaign convergence

## D31 ☑ Separate campaign feature state from presentation descriptions

**Goal.** Campaign profile, navigation intent and operations become typed shared features; board
primitives and authored geometry no longer form the cross-presentation contract.

**Evidence (confidence: traced).** `CampaignFlow` is engine-free and graph-based, but
`ICampaignPage` exposes `Pictures`, `Strokes`, `Fills`, text and art for today’s board shell
(`CSVM/src/UI/CampaignFlow.cs:42-115`; `docs/architecture.md:4217-4228`).

**Approach.** Characterize roster through scrapbook, then separate each feature’s state/operations
from Built-in campaign composition. Preserve stack and modal semantics where they belong to the
campaign use case; leave screen geometry and input choreography presentation-side. Keep temporary
adapters so screenshots do not change.

Handoff from C23: the campaign cabin's PLANE CONSTRUCTION plugs into `HangarFeature.Open(store,
wallet)` with a `HangarCampaignContext` as the `IHangarWallet` over the seated profile, which is
what Built-in's `OpenCampaignHangar` already hands `HangarFlow`; an Original cabin opens the same
feature through `OriginalShell.OpenHangar`'s path (the name screen first, as the decoded
`PC_B_PLANEX` edge says), with `_hangarReturn` naming the cabin screen so CANCEL and a commit land
back on it, and the hub then shows `PX_T_CASHTITLE`/`PX_T_CASH` over the wallet and the
`PX_B_ReadyToPurchase`/`PX_B_CancelPurchase` strips it authors. The INVENTORY's `HA_B_EXPORTP`
stays disabled until the campaign's EXPORT is a feature operation.

**Model recommendation.** max — largest domain/presentation separation and campaign persistence risk.

**Verify.** Characterization first, on the pre-extraction code: the new engine suite
`menu-campaign-journey` (`CSVM/src/Testing/MenuCampaignSuites.cs`) drives a real `LaunchMenu`
through `Drive`/`Shown*` and the composed board over a scratch profile store handed in through the
one pre-extraction edit, `LaunchMenu.CampaignProfiles` (the store the Campaign door and the two
flight returns open, `user://Profiles` unless a suite sets it), so no driven journey can write a
real player's progress: the Mode screen's Campaign door onto the empty roster (four rows, the name
field's "(none)", the langui 200 refusal on a nameless CONTINUE, CANCEL back to Mode), a typed
name continuing onto the cabin with the store holding exactly `Serialize(NewProfile)` and the
last-played record, the roster then standing on that player's ticked row, the field keeping the
continued name (a quirk pinned as it is), a second player created and deleted through the confirm
stage (Back keeps, the Delete answer removes only that directory and clears the last-played
record), a roster row selecting on the first confirm and continuing on the second, the empty
previous-missions contents, the cabin return over a progressed profile, the contents' three rows
with the pick, X opening the book, REPLAY MISSION into a replay's briefing, Next Mission's
briefing headed by the mission's long name running its reveal for 600 driven frames and restarting
the narration on REPLAY BRIEFING (re-entering the same mission keeps the reveal where REPLAY left
it), the flight check's row shape with and without CHANGE PLANE, ammo selection whose stepper takes
the next ammunition and whose ACCEPT writes the pick into the profile file while CANCEL leaves the
file byte for byte, plane selection's ACCEPT writing the pilot's pick and CANCEL keeping it, PLANE
CONSTRUCTION opening the hangar over the profile's wallet and Back resuming the cabin on the row
that opened it, FLY MISSION leaving as one `CampaignMissionExit` for the seated profile's next
story position with one seat (the picked airframe's node, its campaign fit, no build, no pad) and
the profile saved as it stood, the debrief return opening the scrapbook on the flown mission over
the profile as the mission wrote it with the results card reading its outcome and cash, the back
arrow turning a page and the forward arrow returning, RETURN TO CABIN then Back to the roster and
out, the guest check under `DebugJoin(3)` walking P3, P2, and forward again, and every
scratch-profile aid opening its screen. Green before the extraction and, with no edit, after it.
One finding the characterization made and pinned as it behaves: `--menu=campaign-roster` seeds two
profiles and lands on the first one's cabin, not the roster the inventory and `docs/cli.md`
describe, because the aid walker seats the seeded profile before branching on the aid's name; a
Built-in defect outside this plan's scope (Decision 16), left for the backlog.

The classification of every stack and modal operation, which decided what moved:

| Operation | Shared or presentation | Why |
|---|---|---|
| The profile store, the roster, the seated profile, last-played | shared | the campaign's persisted state; every presentation seats the same player |
| CONTINUE (create or continue) with its four refusals, DELETE PLAYER | shared | the original's own rules and the store's write points |
| The name rule (letters, digits, spaces, 32) | shared | it is what keeps a name a legal directory; `CampaignTextEntry` forwards to it |
| The mission named (`MissionSeq`), its `cm_sequence` entry, the wingman flag, Next Mission, campaign complete | shared | the position the screens after the cabin are about |
| The briefing's state, objectives, narration wav and the reveal's progress | shared | authored campaign data both presentations run identically; narration follows it |
| The reveal's clock, the per-frame repaint, when to begin and end narration | presentation | pacing and cue timing, `TickCampaignAudio` |
| CHANGE PLANE's two gates, the seated pair's same-plane rule | shared | `FLIGHTCHECK.SCRIPT`'s and `PLANESELECTION.SCRIPT`'s own rules |
| ACCEPT LOADOUT, ACCEPT SELECTIONS, EXPORT | shared | the writes into the profile and build stores |
| The ammo and plane screens' working copies before ACCEPT | presentation | editing state of one screen; the commit rule is shared |
| The sortie's field (guests, their copies, the walk's position and lock) | shared | it feeds the launch's seats and the no-duplicate rule |
| The intents between screens (`AmmoSlot`, `PlaneSlot`, `ScrapbookEntry`, `ZoomTarget`) | shared | what the next screen is about, whichever graph reaches it |
| The wallet the hangar prices against | shared | the seated profile as `IHangarWallet` |
| FLY MISSION's save and the `CampaignMissionExit` | shared | the one typed launch handoff (Decision 22) |
| The screen stack, `GoTo` returning to an open screen, Back popping, Cancel | presentation | Built-in's graph; Decision 4 lets Original navigate differently |
| The cursor, its settle over unfocusable rows, the opening row | presentation | choreography |
| The refusal line (`Message`), the modal and its one-answer dismissal | presentation | how Built-in shows a refusal; the words come from the feature |
| The pending job (`CampaignExit.OpenHangar`, `FlyMission`) | presentation | a handoff inside Built-in's shell |
| `Pictures`, `Strokes`, `Fills`, `Captions`, `Notes`, `Button`, `Combo`, art | presentation | board composition, Decisions 16 and 18 |

The landed shape: `CampaignFeature` (`CSVM/src/UI/Menu/CampaignFeature.cs`, engine-free, in the
host's feature set) owns `Open(store, planes, stock, dataRoot)`, `Roster`/`RefreshRoster`,
`LastPlayed`, `Profile`, `ContinuePlayer`, `DeletePlayer`, `SelectProfile`, `SeatProfile`,
`Resume`, `MissionSeq`/`SetMission`, `Mission`, `MissionHasWingman`, `NextMissionSeq`,
`CampaignComplete`, `ChangePlaneAllowed`, `Briefing` (a `CampaignBriefing`, the loaded state and
reveal with `Advance`/`Restart`), `AmmoSlot`, `PlaneSlot`, `ScrapbookEntry`/`EnterScrapbook`,
`ZoomTarget`, `Field` (the `CampaignFlightField`, moved into `CSVM.UI.Menu`), `AmmoTarget`,
`CommitLoadout`, `SeatedPairClashes`, `CommitPlanes`, `ExportPlane`, `Wallet()` (a
`CampaignWallet`, the former `HangarCampaignContext` moved into `CSVM.UI.Menu`), `BuildExit`,
`CapturePath`, `ValidName`/`AcceptsNameChar` and `Discard`. `BriefingScript.cs` and
`BriefingObjectives.cs` moved into `CSVM.UI.Menu` with it. One feature rather than a family: the
operations share one seated profile and one mission position, and splitting them would put the
same `Profile` behind two doors. `CampaignFlow` keeps its whole surface and its stack, constructed
by `LaunchMenu` over the host's one feature (`CampaignFlow(feature)`) and by its old constructor
over a private one for the unit tests and `campaign-loop`; every property a page reads forwards to
the feature and every write goes through it. `LaunchMenu` gets the feature from the host,
`NewCampaignFlow` opens it on the store, `OpenCampaignHangar` opens `HangarFlow` over
`Feature.Wallet()`, `FlyCampaignMission` leaves through `Feature.BuildExit`, and every door out
discards the feature with the flow. Nothing about the boards changed: `ICampaignPage`,
`CampaignBoards`, `ComposedBoard` and the pages' composition are as they were, reading their
content from the feature through the flow.

Checked: `dotnet build CSVM/CSVM.sln`, the whole `dotnet test` (2989, of which 22 are
`CampaignFeatureTests` pinning the store's write points as file text over a scratch directory:
what a created player writes and where (the profile file as `Serialize(NewProfile)`, the
last-played record), what the refusals leave untouched, an existing player continuing without a
rewrite, the full roster, what a delete removes and clears, what ACCEPT LOADOUT writes for the
seated pilot and does not write for a guest, what ACCEPT SELECTIONS writes with and without a
wingman, the seated pair rule, what EXPORT writes into the build store alone and refuses, what a
purchase and a sale write through the wallet, what FLY MISSION saves and carries per seat, what a
flown mission's record reads back as through the return's `SeatProfile`, `Resume`, the CHANGE PLANE
gates, the scrapbook entry count and capture path, absent data, `Discard` touching no file, a second
`Open`; `CampaignWalletTests` renamed from `HangarCampaignContextTests` with the type;
`MenuNamespaceDependencyTests` green with the feature, the wallet, the field and the briefing types
in the shared namespace), then
`.\RunTests.ps1 -Suite "menu-campaign-journey,menu-free-flight-journey,menu-player-setup-journey,menu-player-setup-seats,menu-instant-action-journey,menu-original-instant-action,menu-hangar-journey,menu-original-hangar,menu-host-tracer,menu-original-tracer,menu-zone-layout,menu-screenshot-key,campaign-briefing-repaint,campaign-hangar-handover" -SkipUnits -SkipGoldens`
(14 suites, 14 passed, engine errors clean) and `.\RunTests.ps1 -Filter "campaign" -SkipUnits -SkipGoldens`
(43 suites, every `campaign-*` suite with `campaign-loop` and `menu-campaign-journey` among them,
43 passed, engine errors clean),
`.\CheckCommentCaps.ps1 -Summary` and `.\CheckEncoding.ps1`. Shots: the twelve scratch-profile
aids `campaign-empty`, `campaign-roster`, `campaign-entry`, `campaign-cabin`, `campaign-previous`,
`campaign-scrapbook`, `campaign-briefing:24`, `campaign-flightcheck`,
`campaign-guestcheck:2 --debug-join=3`, `campaign-ammo`, `campaign-planeselection` and
`campaign-hangar`, each one Godot run on the hidden desktop with `--resolution 1280x720` ahead of
the `--` and `--menu=<aid> --screenshot=<abs path>`, from this worktree built at the plan branch's
tip before any edit into `.scratch\d31-before\` and from the extracted tree into
`.scratch\d31-after\`, compared by decoded 32bpp pixels (SHA-256 over the rows): all twelve
identical, zero differing pixels. `docs/verification.md` rules that bit: **METHOD-6** (which
binary each side used is named: this tree at the plan tip before the first edit against this tree
after the extraction), **METHOD-3** (the aids read a scratch store emptied on every open, so no
baseline moved; `campaign-hangar` lists the profile's own planes, not `user://Planes/`), **SHOT-6**
(decoded pixels, never PNG bytes), **SHOT-9**/**SHOT-10** (windowed probes on the hidden desktop,
absolute paths, every file checked present), **SHOT-32** (the briefing aid advances the reveal in
1/60 s slices to 24 s before it draws, so its shot is deterministic and proves that frame alone;
the reveal running and repainting is pinned by `campaign-briefing-repaint` and the 600 driven frames
in `menu-campaign-journey`; the entry aid's caret is a fixed glyph, not a blink, so it needs no
mask), **SRC-4** (the aid's cabin-not-roster finding is recorded once, here, and not restated in
the inventory).

What this did not prove: the harness runs a suite before any session builds, so the
`CampaignMissionExit` is verified at the host's sink and the debrief return is entered by calling
`OpenCampaignScrapbook` with a result recorded the way the director records one; the built world
between them is `campaign-loop`'s, which walks `CampaignFlow` over the old constructor and flies
the mission, and the real cabin-to-scrapbook return is owed at the controls: `.\RunDev.ps1`,
Campaign, a player, Next Mission, GO TO FLIGHT CHECK, FLY MISSION, the mission's end, and the
scrapbook must open on it with RETURN TO CABIN focused.

**Verified.** The plan tree with D31 landed passed the complete `.\RunTests.ps1` (2989 units, 210
engine suites across 4 shards with `menu-campaign-journey` and every `campaign-*` suite among them,
18 goldens hash-identical, engine errors clean, 141.3s). No real profile was written by any run.
The cabin to scrapbook return through a flown mission remains owed at the controls, as the Verify
paragraph states.

**⚠ Traps.** Do not replace one presentation-shaped `ICampaignPage` with a universal screen schema.
Not every current stack operation necessarily belongs in shared feature state; classify it first.

## D32 ☑ Migrate campaign fixed chrome to decoded layouts without visual change

**Goal.** Existing campaign screens source fixed chrome, geometry, artwork roles and widget metadata
from decoded layouts while retaining their current dynamic content and appearance.

**Evidence (confidence: traced).** `CampaignBoards`/`ComposedBoard` currently hardcode fixed composed
boards, and `ComposedBoardView` already resolves extracted art and nearest-samples it
(`docs/architecture.md:4268-4279`, `4311-4322`).

**Approach.** Map A2’s decoded layout into the existing composition primitives or a presentation-local
successor. Keep dynamic profiles, mission rows, aircraft, ammo and scrapbook content supplied by D31
features. Compare each screen before replacing its hardcoded fixed description.

Handoff from D31: the dynamic content a migrated board still draws is read off `CampaignFlow`'s
forwarding members over `CampaignFeature` (`Profile`, `Roster`, `Mission`, `Briefing`, `Field`,
`AmmoTarget`, the results through `CampaignProgression`), so a page rebuilt over a decoded layout
changes only its `Pictures`/`Captions`/`Fills`/`Button` composition and no read; the twelve
scratch-profile aids and `menu-campaign-journey` are the regression baseline, with
`--menu=campaign-roster` known to land on the cabin.

**Model recommendation.** high — mechanical breadth under a strict pixel-regression constraint.

What was built: `CampaignLayout` (`CSVM/src/UI/CampaignLayout.cs`), the boards' read of the
decoded layout. Every button slot, background pane and text slot in `CampaignBoards`, and every
fixed element in the seven migrated pages (cabin, previous missions, scrapbook, scrapbook zoom,
flight check, ammo, plane selection), names its `LAYOUT.CSV` section and row and reads it through
`CampaignLayout.At`/`Box`/`Int`/`Art`/`GlobalArt`/`Justify`/`ZoomFamily`, with the value the board
drew before the layout existed handed in beside the read as the fallback. `At` and `Box` answer with
the whole row or the whole fallback, never one coordinate from each. Where the decoded value and the
reference screenshot disagree the measured value is pinned and the rest of the row reads: `CM_B_START`
keeps `CM_B_Start.png` (row: `GN_B_Continue.png`), the four flight-check paper plaques keep 131/349
(rows: 132/350), `OL_S_AMMODESC` keeps y 92 (row: 96), `FC_T_TITLE`/`OL_T_TITLE` keep x 138 left
(rows: 132 centred), `FC_T_GUNLISTW`/`FC_T_ROCKETLISTW` keep the pilot pair's 17-pixel drop (rows:
400), `PS_T_WINGPLANE` keeps `106 + 218` (row: 323); `docs/org/campaign-board.md` carries the table.
The two chosen positions stay chosen (the cabin's memento window; standing `MM_LOGO` over the profile
dialog, its position now the row's). The briefing's chrome is `Briefing.zrd`'s and no layout row
describes it, so its slots carry no section. Dynamic content is untouched: every page still reads
`CampaignFlow`'s forwarding members over `CampaignFeature`, and nothing dynamic is serialized.

Availability and failure rule: Built-in loads the layout for itself through
`CampaignLayout.For(dataRoot)` (once per data root, kept in a static table, exposed as
`CampaignFlow.Layout`), not through the Original presentation's `OriginalAvailability` load, because
that load also refuses a layout whose main-menu art is missing and the campaign boards must draw
through that ("usable without"). A missing or unreadable `menu_layout.json` is the `Fallback`
instance with its `Reason`, logged once as a `ui` warning ("campaign boards draw their hardcoded
chrome: ..."); every board then draws from its fallbacks, which are the shipped rows' own values, so
the screens are the same either way and nothing throws. A null data root is the fallback with no
reason. Tested over an empty temp root, a hand-authored malformed file and a foreign JSON in
`CSVM.Tests/CampaignLayoutTests.cs`; the real file is never touched.

**Verify.** `dotnet build CSVM/CSVM.sln`; the whole `dotnet test` (3000, of which 12 are
`CampaignLayoutTests`: the fallback rule over a null root, an empty root, a malformed file and a
foreign JSON; every read over the hand-authored campaign sections added to
`CSVM.Tests/fixtures/menu-layout-original/LAYOUT.CSV` with invented geometry and art; a moved row
moving the composed plaque and the pinned art, y and description column staying put; the dialog and
the table of contents reading their rows); `.\RunTests.ps1 -Suite campaign-layout-parity -SkipUnits
-SkipGoldens` (the new suite in `CSVM/src/Testing/CampaignLayoutSuites.cs`: every campaign board aid
composed twice through a real `LaunchMenu`, once with `LaunchMenu.CampaignLayoutOverride` pinned to
`CampaignLayout.Fallback` and once reading the install's layout, the two `ComposedBoard`s compared
element by element, with the decoded side required to have loaded and the six pinned rows required
to differ from their fallbacks so the comparison is between two sources; 11 aids, 278 elements,
identical); `.\RunTests.ps1 -Filter menu -SkipUnits -SkipGoldens` (12 suites, 12 passed) and
`.\RunTests.ps1 -Filter campaign -SkipUnits -SkipGoldens` (44 suites including the new one and
`campaign-loop`, 44 passed, engine errors clean); `.\CheckCommentCaps.ps1 -Summary`;
`.\CheckEncoding.ps1`. Shots: the twelve scratch-profile aids `campaign-empty`, `campaign-roster`,
`campaign-entry`, `campaign-cabin`, `campaign-previous`, `campaign-scrapbook`,
`campaign-briefing:24`, `campaign-flightcheck`, `campaign-guestcheck:2 --debug-join=3`,
`campaign-ammo`, `campaign-planeselection` and `campaign-hangar`, each one Godot run on the hidden
desktop with `--resolution <WxH>` ahead of the `--` and `--menu=<aid> --screenshot=<abs path>`
(`.scratch\d32-shots.ps1`), at 1280x720 (the reference), 1024x768, 1920x1080 and 600x750, from
this worktree at the plan branch's tip before any edit into `.scratch\d32-before\` (taken twice,
`.scratch\d32-before-repeat\`, byte-identical, so the aids are deterministic) and from the migrated
tree into `.scratch\d32-after\`, compared by decoded 32bpp pixels (SHA-256 over the rows,
`.scratch\d32-compare.ps1`): 48 of 48 identical, zero differing pixels; `BoardFit` is unchanged, so
the three fit sizes were expected identical and are. Comparison decision: no aid needs a masked or
semantic comparison. The entry aid's caret is a fixed glyph, not a blink; the briefing aid advances
its reveal in 1/60 s slices to 24 s before it draws (SHOT-32), so the frame is deterministic; the
repeat run proved it (METHOD-2). `docs/verification.md` rules that bit: **METHOD-6** (which binary
each side used is named), **METHOD-2** (same-build variation measured before the A/B, zero),
**METHOD-10** (the parity suite refuses to pass when the decoded side did not load or the pinned
rows do not differ, so a fallback-against-fallback run cannot read as parity), **METHOD-9** (the
unit tests move a row and the composed plaque moves, so the read path can fail), **SHOT-6**
(decoded pixels, never PNG bytes), **SHOT-9**/**SHOT-10** (windowed probes on the hidden desktop,
absolute paths, every file checked present), **SHOT-32** (the briefing aid proves its frame alone).

**Verified.** The plan tree with D32 landed passed the complete `.\RunTests.ps1` (3000 units, 211
engine suites across 4 shards with `campaign-layout-parity` among them, 18 goldens hash-identical,
engine errors clean, 150.3s).

**⚠ Traps.** Fixed chrome may migrate; dynamic content may not be serialized into extracted output.
Patch-overlay precedence must match extraction, and no game asset enters git.

## D33 ☑ Complete Original campaign interaction, briefing and audio integration

**Goal.** Original completes every campaign journey with evidenced pointer behaviour, equivalent
keyboard/pad access, briefing narration and presentation-owned cues over the shared campaign features.

**Evidence (confidence: lead-only).** Existing campaign boards are already close to Original and
provide the agreed verification baseline. `LaunchMenu` currently owns briefing playback and campaign
input (`CSVM/src/UI/LaunchMenu.cs:1880-2105`).

**Approach.** Reuse the migrated board component in Built-in and Original, then put screen graph,
hit-testing, rollover, transitions and cue requests in Original. Move resource lookup/playback and
session handoff into the shared audio service. Resolve A1 capture gaps before implementing affected
behaviour.

Handoff from D31: an Original campaign is another `OriginalScreen` family over
`host.Features.Get<CampaignFeature>()`, opened with `Open(CampaignProfileStore.UserProfiles(),
CustomPlaneStore.UserPlanes(), StockLoadouts.Load(), dataRoot)` on the cabin door and discarded on
every way out; the roster is `ContinuePlayer`/`DeletePlayer` with their refusals, the cabin's
PLANE CONSTRUCTION is `HangarFeature.Open(store, campaign.Wallet())` through `OriginalShell.OpenHangar`
with the cabin as the hangar's return screen, the briefing reads `Briefing.State`/`Reveal` and
advances it on the presentation's own clock with `NarrationStarts` driving
`MenuAudioService.BeginNarration(Briefing.NarrationWav)`, the flight check and ammo screens commit
through `CommitLoadout`/`CommitPlanes`/`ExportPlane`, guests walk `Field`, and FLY MISSION is
`host.Exit(campaign.BuildExit(padsPerSeat))`. `CabinReturn` and `DebriefReturn` map onto its
screens through `SeatProfile(name)` and `EnterScrapbook(seq)`. The screen stack, the cursor, the
refusal line and the modal are Built-in's `CampaignFlow`'s and are not to be reused.

Handoff from D32: the board component Original reuses is `CampaignBoards.For(page, focusedRow,
pressed, detail, modal, layout)` over an `ICampaignPage`, composing a `ComposedBoard` that
`ComposedBoardView` draws; it takes its layout as a `CampaignLayout`, which Original builds with
`CampaignLayout.Over(layout)` from the `MenuLayout` it already holds (`OriginalShell._layout`), so
both presentations read one parsed artifact and Original never goes through `CampaignLayout.For`'s
static table. Every slot's section and row are named in `CampaignBoards.Buttons`/`Chrome`, the
pinned measurements are the ones `docs/org/campaign-board.md` tables, and `CampaignBoards.Dialog`
composes the message box for either presentation. The pages' `Pictures`/`Captions`/`Fills` read
`Flow.Layout`, so an Original campaign page over the shared feature either reuses the Built-in pages
through a `CampaignFlow(feature, CampaignLayout.Over(layout))` or composes its own rows with the
same reads.

**Model recommendation.** high — timing, narration and persistent campaign paths cross several seams.

What was built: `OriginalCampaign.cs`, the shell's partial over the shared `CampaignFeature`, nine
`OriginalScreen` members between the Instant Action screen and the hangar's. The choice the two
handoffs left open was made this way: the shared board component is reused whole (the Built-in
pages hosted in a `CampaignFlow(feature, CampaignLayout.Over(layout))` of Original's own and composed
through `CampaignBoards.For`), and the screen graph, the hit-testing, the rollover and pressed
frames, the cues, the dialogs and every door out are Original's. The flow is never walked: its
screen and row are mirrored from Original's (`GoTo`, `FocusRow`), and a page that names a destination
or raises a dialog has it read off the flow (`TakeModal`/`TakeMessage`, two additions to
`CampaignFlow`) and re-entered through Original's graph. The stack's `Back`, the cursor's `Move`,
the refusal band and the modal's dismissal are not used. Rows are read back as rectangles from the
component (`CampaignBoards.SlotOf` by `BoardButtonRef`, `ComboFieldHeight`, `TextSlot`,
`CampaignPreviousMissionsPage.RowBox`, `CampaignScrapbookPage.ScrapOf` over the new
`ScrapbookScrap.Region`), a dialog through `CampaignBoards.Dialog(message, buttons)` and
`DialogSlot`, so no campaign geometry lives in Original. The Campaign row is live; `CabinReturn` and
`DebriefReturn` reopen the campaign and map through `SeatProfile` and `EnterScrapbook`
(`ShowCabin`/`ShowScrapbook`), replacing the top-level fallback and its log line. The presentation
owns the briefing's clock (`AdvanceBriefing` per tick) and turns the script's start count into
`BeginNarration`, ending it on the frame the shell leaves the briefing and on `Hide`; the audio
service itself is unchanged, since its contract already carried replay (a begin replaces) and
interruption (an idempotent end). The cue table gained nothing: the campaign's plaques ask for the
two button cues, and the profile screen's box asks for `menu.text`/`menu.text-error`, which the
table already resolved and nothing had requested (the hangar's name box now asks too). The campaign
aids' scratch store moved into `CampaignAidProfiles` so both presentations' aids seat one player;
`OriginalPresentation.CampaignProfiles` is the suites' scratch door, as `LaunchMenu.CampaignProfiles`
is Built-in's.

Capture gaps resolved by implementing the reading the data supports and marking it unconfirmed,
each added to `CAP-52`'s clause: a campaign plaque cues the rollover and the click and a list row
or scrap cues nothing; the edit box cues its two bound wavs per character; the passive pointer over
a scrap; REPLAY BRIEFING restarts the narration from the top and RETURN TO CABIN or GO TO FLIGHT
CHECK ends it; a mission end starts no narration; a scrap with a `0,0,0,0` region is hit on its
picture's bounds; YES and NO as the delete confirm's words; Back landing on the plaque that opened
the screen.

**Verify.** `dotnet build CSVM/CSVM.sln`; the whole `dotnet test` (3012, of which 12 are
`CSVM.Tests/OriginalCampaignTests.cs` over the fixture's campaign sections, extended with
`[@PassengerCabin@]`, `[@ScrapBook@]`, `[@PlaneSelection@]` and the flight check's, contents',
ammo's and message box's button rows on invented lines, and a scratch profile store: the Campaign
row's door, the box's typing with its cues, CONTINUE creating and seating, the empty-name refusal as
the one-button dialog at `MB_B_CENTER`'s row, a roster row filling the box then starting with the
selection bar and pointer frame, the delete confirm opening on NO and deleting on YES, the cabin's
plaques under the pointer with their rollover and pressed frames and the keyboard wrapping them,
RETURN TO MAIN MENU and Back closing the campaign, the briefing's three plaques, the flight check
into ammo and back and FLY MISSION as a `CampaignMissionExit` with the profile saved, PLANE
CONSTRUCTION over the wallet with the cabin as the return, the two flight returns' mapping and a
failed seat landing on the profile screen, the book backing to the contents that opened it);
`.\RunTests.ps1 -Suite menu-original-campaign -SkipUnits -SkipGoldens` (the new suite in
`CSVM/src/Testing/MenuOriginalCampaignSuites.cs`, through a real `MenuHost` and
`OriginalPresentation` over the install's layout and a scratch store: the Campaign row clicked,
`Zachary` typed with one `menu.text` per character and Enter seating them, the contents and back,
NEXT MISSION into the briefing with 600 ticks advancing the reveal past 9.9 s and the recording
audio's `BeginNarration` called once with the mission's wav and `EndNarration` not at all, REPLAY
BRIEFING beginning it again, RETURN TO CABIN ending it once and a frame on the cabin starting
nothing, re-entry beginning it a third time where REPLAY left the reveal, GO TO FLIGHT CHECK
ending it, two `MenuIdleSource` seats joined so the field reads three humans, FLY MISSION advancing
to P2's check (headed `FLIGHT CHECK P2`), Back retreating, FLY MISSION twice more and the last
leaving through the sink as one `CampaignMissionExit` for `Zachary` at seq 0 with three seats and
the profile saved as it stood, the flown mission recorded and `Show(DebriefReturn)` landing on the
book with RETURN TO CABIN focused, `Mission Completed` on the card and no narration begun,
RETURN TO CABIN then Back to the profile screen then Back out, `Show(CabinReturn)` on the cabin,
CHANGE AMMO with a sideways step to `Dum-dum`, the list opened under the box and closed by Back,
ACCEPT LOADOUT writing the pick, a third plane making CHANGE PLANE stand, plane selection's Left onto
it and ACCEPT SELECTIONS writing `SelectedPlane` 2, PLANE CONSTRUCTION over the wallet and Back
resuming the cabin on that plaque, `Deactivate` leaving no open campaign); the same run over
`menu-original-tracer` (its Campaign-plaque expectation updated: the plaque is live and a debrief
return for a profile the scratch store lacks lands on the profile screen), `menu-original-hangar`,
`menu-original-instant-action` and `menu-player-setup-seats` (5 passed);
`.\RunTests.ps1 -Filter "campaign,menu" -SkipUnits -SkipGoldens` (56 suites, 56 passed, engine errors
clean, `menu-campaign-journey`, `campaign-layout-parity` and `campaign-loop` unchanged among them);
`.\CheckCommentCaps.ps1 -Summary` (all within cap); `.\CheckEncoding.ps1` (no mojibake). Built-in
unchanged: the twelve scratch-profile aids `campaign-empty`, `campaign-roster`, `campaign-entry`,
`campaign-cabin`, `campaign-previous`, `campaign-scrapbook`, `campaign-briefing:24`,
`campaign-flightcheck`, `campaign-guestcheck:2 --debug-join=3`, `campaign-ammo`,
`campaign-planeselection` and `campaign-hangar`, each one Godot run on the hidden desktop with
`--resolution 1280x720` ahead of the `--` and `--menu=<aid> --screenshot=<abs path>`
(`.scratch\d33-shots.ps1`), from a detached worktree at the plan branch's tip (`a5f11882`) built
there into `.scratch\d33-shots\builtin-before\` and from this worktree into
`.scratch\d33-shots\builtin-after\`, compared by decoded 32bpp pixels (SHA-256 over the rows,
`.scratch\d33-compare.ps1`): 12 of 12 identical, zero differing pixels. Original: the seven screens
`campaign-roster`, `campaign-cabin`, `campaign-briefing:24`, `campaign-flightcheck`, `campaign-ammo`,
`campaign-planeselection` and `campaign-scrapbook` under `--presentation=original` at 1024x768 and
1920x1080 into `.scratch\d33-shots\original\`, 14 shots present. What each pose cannot show: the
profile screen's shot has no pointer and an empty box (the scratch store remembers nobody), so the
selection bar, the pointer frame and a refusal dialog are not in it; the cabin's shows NEXT MISSION
in its rollover frame as the focused plaque and no other state; the briefing's is the 24 s frame
alone (SHOT-32), not the reveal running or the narration, which the suite asserts; the flight
check's, ammo's and plane selection's show the opening focus and closed fields, not an open list, a
pressed frame or a dialog; the book's shows spread 1 of the last flown mission with RETURN TO CABIN
focused, not a page turn or a scrap under the pointer. `docs/verification.md` rules that bit:
**METHOD-6** (which binary each side used is named: the plan-tip worktree against this one),
**METHOD-3** (the aids read a scratch store emptied on every open), **METHOD-9** (the unit tests
click plaques at the fixture's invented rows, so a rectangle read from the wrong place misses),
**SHOT-6** (decoded pixels, never PNG bytes), **SHOT-9**/**SHOT-10** (windowed probes on the hidden
desktop, absolute paths, every file checked present), **SHOT-32** (a shot of the briefing proves its
frame alone; the reveal running is the suite's), **SRC-4** (the unconfirmed readings are listed once,
in the inventory's Part 4, and `CAP-52`'s clause points there). Not proven here: the at-the-controls
journey (the profile screen to the cabin to the briefing to the flight check to a flown mission to
the book and back, hearing the narration start, restart and stop) is the user's and is owed, as it
was for D31; the suite's launch is verified at the host's sink and the flown mission's record is
written the way the director writes one.

**Verified.** The plan tree with D33 landed passed the complete `.\RunTests.ps1` (3012 units, 212
engine suites across 4 shards with `menu-original-campaign` among them, 18 goldens hash-identical,
engine errors clean, 173.3s). No real profile was written by any run. The at-the-controls journey
from the roster to a flown mission and back to the scrapbook remains owed, as the Verify paragraph
states.

**⚠ Traps.** Shared board components do not make the two presentations one screen graph. Briefing
state drives narration, but cue selection and transition timing remain presentation responsibilities.

# Wave E — Completion and release gate

## E41 ☐ Complete Original top level, Options and every remaining transition

**Goal.** Every A1 in-scope screen and edge is reachable and escapable in Original by mouse,
keyboard and pad, including its presentation chooser and modal behaviour.

**Evidence (confidence: lead-only).** Full normal availability was explicitly gated on complete
coverage; A1 supplies the authoritative list.

**Approach.** Close the inventory one row at a time, implementing only evidenced behaviour or an
explicitly accepted remake-only rule. Add Options to Built-in as this plan’s sole intended behaviour
addition and expose the same shared options through Original’s own composition.

Handoff from D33: Original's top level now opens Campaign (`MM_B_CAMPAIGN`, the whole campaign
family in `OriginalCampaign.cs`), Instant Action, the Options stand-in behind Preferences and Quit,
plus the three remake-only doors; what it still lacks is Multiplayer and Credits (both draw frame 0
and take no input, and Credits is out of scope) and the decoded Preferences pages (Preferences,
GameOptions, Audio, Video, ControlsPrefs, Keys), for which the minimal Options screen stands in.
Every campaign screen's Back and every RETURN plaque already lead somewhere; the two-answer
messagebox (`CampaignBoards.Dialog` over `DialogButton`s) and the one-answer box are the dialog
idiom the remaining screens' confirms should take.

**Model recommendation.** high — broad integration and fidelity review.

**Verify.** Machine-check 100% inventory coverage and run every transition forward/back with each
input family. <TODO from A1: final journey matrix and capture debts.>

**⚠ Traps.** “Looks complete” is not inventory coverage. A screen with no Back/recovery path is a
dead end even if its launch action works.

## E42 ☐ Enforce the required/optional Original asset manifest

**Goal.** Original availability is decided before entry from a versioned manifest; required failure
falls back clearly while optional absence degrades locally.

**Evidence (confidence: lead-only).** The required/optional policy and requested/active distinction
were settled in Decisions 9–10; A1 identifies the concrete asset set.

**Approach.** Generate or maintain the manifest from the decoded-layout inventory, validate structure
without eagerly loading every bitmap, report actionable missing/unreadable entries, and connect the
result to A3 resolution. Add a test-only fixture presentation for incomplete manifests.

**Model recommendation.** medium — bounded validation once A1/A2 define the data.

**Verify.** Test complete, missing-required, corrupt-required, missing-optional, stale-schema and
assets-restored-next-launch cases; locally hide one required asset and perform the agreed recovery
playtest.

**⚠ Traps.** Do not discover required absence one blank screen at a time. Do not overwrite the
requested presentation during fallback.

## E43 ☐ Complete typed launch and semantic return routing across every journey

**Goal.** All menu paths leave through one typed handoff and every flight return is expressed as a
semantic destination that each presentation maps into its own graph.

**Evidence (confidence: lead-only).** `LaunchMenu` currently exposes separate general and campaign
callbacks (`CSVM/src/UI/LaunchMenu.cs:37-45`); the unified contract was settled in Decisions 22–23.

**Approach.** Migrate Free Flight, Instant Action, Dogfight and campaign payloads to the A4 exit
contract. Convert return-to-top-level, cabin and scrapbook/debrief paths to semantic destinations.
Delete transitional callbacks only after all callers and aids use the new host.

Handoff from D33: both presentations now map `CabinReturn` and `DebriefReturn` into their own
graphs (Original through `OriginalShell.ShowCabin`/`ShowScrapbook`, seating the named profile
re-read from the store and opening the book on the flown mission) and both leave a campaign launch
as one `CampaignMissionExit`, so the semantic side of the campaign's return routing is done. What
still goes through `--menu=` is the top-level show: `Launcher.ShowMenu` applies the aid on every
top-level `Show`, so a return to the top level after a flight re-enters the aid's screen rather than
the top level itself, in both presentations; and `--menu=campaign` still names the real store while
every other campaign aid names the scratch one, a distinction the destination vocabulary does not
yet carry.

**Model recommendation.** max — session lifecycle and campaign progression are high-blast-radius.

**Verify.** Launch and return every mode in both presentations, including restart/rerun distinctions,
campaign mission completion and failure, and presentation fallback before return. <TODO: exact
session suite matrix and whether any return path needs a new deterministic aid.>

**⚠ Traps.** A semantic destination is not a shared concrete screen id. Presentations never hide
themselves and construct `GameSession` directly.

## E44 ☐ Run acceptance, enable Original normally and publish the Modern extension contract

**Goal.** Original becomes selectable in normal Options only after complete acceptance; the proven
contract and extension checklist make a future Modern presentation additive.

**Evidence (confidence: lead-only).** Normal exposure, verification layers and the no-prototype Modern
deliverable were settled in Decisions 17, 24 and 29.

**Approach.** Run the full journey matrix, legal fixture tests, local extracted-data screenshot
comparisons, campaign regression and at-the-controls pass. Add Original to normal availability only
after all required rows pass. Document presentation registration, features, input sources, options,
audio, launch, return, asset policy and the explicit things Modern does not inherit from Original.

**Model recommendation.** high — release gate and architectural audit require broad judgement.

**Verify.** Run the complete `./RunTests.ps1` landing gate, every Original/Built-in menu capture,
wide/tall/4:3 screenshots, mouse/keyboard/pad journeys, switching and missing-asset recovery. Record
the at-the-controls results. <TODO: exact final commands and golden additions after suites exist.>

**⚠ Traps.** Two presentations prove replaceability only if shared features have no dependency on
either. Do not add a token Modern screen; the extension contract is the deliverable.
