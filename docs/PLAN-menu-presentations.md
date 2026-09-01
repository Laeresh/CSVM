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

21. ☐ Extract Instant Action into a typed shared feature
22. ☐ Extract shared player setup and deliver Dogfight in both presentations
23. ☐ Separate hangar features from composition and deliver its Original screens

### Wave D — Campaign convergence

31. ☐ Separate campaign feature state from presentation descriptions
32. ☐ Migrate campaign fixed chrome to decoded layouts without visual change
33. ☐ Complete Original campaign interaction, briefing and audio integration

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

## C21 ☐ Extract Instant Action into a typed shared feature

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

**Verify.** Run existing Instant Action wizard/preset units, add equivalent-operation tests across
both presentation graphs, and launch representative ace, squadron, stunt and zeppelin sessions.
<TODO: exact test classes/commands and Original reference journeys.>

**⚠ Traps.** Shared feature state does not imply a shared wizard. Preserve the ace skip in Built-in;
Original follows its own evidenced navigation.

## C22 ☐ Extract shared player setup and deliver Dogfight in both presentations

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

**Model recommendation.** high — multiplayer device ownership and same-frame input races are fragile.

**Verify.** Engine-free claim/lock race tests, 1–4 player scripted paths, per-device ownership, Back
and force-switch cleanup; at-the-controls pad and mouse pass. <TODO: hardware-dependent cases and
exact engine aid commands.>

**⚠ Traps.** A menu input source is not synonymous with a pad. Presentation-specific join gestures
must converge on one shared roster and launch gate.

## C23 ☐ Separate hangar features from composition and deliver its Original screens

**Goal.** Hangar rules and scratch-plane lifecycle are presentation-neutral, while Built-in and
Original compose and navigate them independently.

**Evidence (confidence: lead-only).** `HangarFlow` is engine-free and owns order/state/rules, but its
`IHangarPage` contract exposes rows, art and detail shaped for today’s shell
(`CSVM/src/UI/HangarFlow.cs:32-97`; `docs/architecture.md:4139-4151`).

**Approach.** Characterize all nine screens, cancel residue, edit/delete and commit. Split typed
hangar feature state/operations from Built-in page composition without changing store semantics.
Bind Original screens to decoded layouts and reuse shared visual components where faithful.

**Model recommendation.** high — wide feature surface with persistence and dynamic preview art.

**Verify.** Run all hangar unit suites, compare every existing Built-in aid, execute both doors and
the campaign-wallet path, then complete Original mouse/keyboard/pad journeys. <TODO: exact aid list
and evidence gaps from A1.>

**⚠ Traps.** Cancelling remains residue-free by dropping the scratch plane; do not replace that with
an undo path. Presentation switching also discards the scratch flow without committing.

# Wave D — Campaign convergence

## D31 ☐ Separate campaign feature state from presentation descriptions

**Goal.** Campaign profile, navigation intent and operations become typed shared features; board
primitives and authored geometry no longer form the cross-presentation contract.

**Evidence (confidence: traced).** `CampaignFlow` is engine-free and graph-based, but
`ICampaignPage` exposes `Pictures`, `Strokes`, `Fills`, text and art for today’s board shell
(`CSVM/src/UI/CampaignFlow.cs:42-115`; `docs/architecture.md:4217-4228`).

**Approach.** Characterize roster through scrapbook, then separate each feature’s state/operations
from Built-in campaign composition. Preserve stack and modal semantics where they belong to the
campaign use case; leave screen geometry and input choreography presentation-side. Keep temporary
adapters so screenshots do not change.

**Model recommendation.** max — largest domain/presentation separation and campaign persistence risk.

**Verify.** Run every campaign page/flow unit and campaign-loop engine suite, then compare all
existing campaign aids before and after. <TODO: exact command set and semantic ownership review for
each navigation edge.>

**⚠ Traps.** Do not replace one presentation-shaped `ICampaignPage` with a universal screen schema.
Not every current stack operation necessarily belongs in shared feature state; classify it first.

## D32 ☐ Migrate campaign fixed chrome to decoded layouts without visual change

**Goal.** Existing campaign screens source fixed chrome, geometry, artwork roles and widget metadata
from decoded layouts while retaining their current dynamic content and appearance.

**Evidence (confidence: traced).** `CampaignBoards`/`ComposedBoard` currently hardcode fixed composed
boards, and `ComposedBoardView` already resolves extracted art and nearest-samples it
(`docs/architecture.md:4268-4279`, `4311-4322`).

**Approach.** Map A2’s decoded layout into the existing composition primitives or a presentation-local
successor. Keep dynamic profiles, mission rows, aircraft, ammo and scrapbook content supplied by D31
features. Compare each screen before replacing its hardcoded fixed description.

**Model recommendation.** high — mechanical breadth under a strict pixel-regression constraint.

**Verify.** Raw-pixel hashes for all existing campaign aids at the pinned reference resolution,
plus 4:3/wide/tall fit checks and malformed/missing decoded-layout cases. <TODO: decide whether any
known nondeterministic campaign aid needs a masked or semantic comparison after reading
`docs/verification.md`.>

**⚠ Traps.** Fixed chrome may migrate; dynamic content may not be serialized into extracted output.
Patch-overlay precedence must match extraction, and no game asset enters git.

## D33 ☐ Complete Original campaign interaction, briefing and audio integration

**Goal.** Original completes every campaign journey with evidenced pointer behaviour, equivalent
keyboard/pad access, briefing narration and presentation-owned cues over the shared campaign features.

**Evidence (confidence: lead-only).** Existing campaign boards are already close to Original and
provide the agreed verification baseline. `LaunchMenu` currently owns briefing playback and campaign
input (`CSVM/src/UI/LaunchMenu.cs:1880-2105`).

**Approach.** Reuse the migrated board component in Built-in and Original, then put screen graph,
hit-testing, rollover, transitions and cue requests in Original. Move resource lookup/playback and
session handoff into the shared audio service. Resolve A1 capture gaps before implementing affected
behaviour.

**Model recommendation.** high — timing, narration and persistent campaign paths cross several seams.

**Verify.** Run every campaign aid in both presentations, the campaign-loop suite, narration replay
and interruption cases, and an at-the-controls roster → cabin → briefing → flight check → mission →
scrapbook → cabin journey. <TODO: reference captures and exact audio assertions.>

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
