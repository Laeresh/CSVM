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

`ExtractRof.ps1` currently unpacks `crimson.rof` and its `crimptch.rof` overlay into
`extracted/rof/`, emits `ui_strings.json`, and additively decodes custom `.BM` images. Its own header
records 846 archive members: 588 already-common PNG/JPG/TGA/TIF files and 184 custom `.BM` files.
The archives also contain GUI scripts and `LAYOUT.CSV`, but extraction does not yet emit a structured
menu-layout artifact (`ExtractRof.ps1:1-33`; `docs/tooling.md:94-114`).

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

<TODO: A1 census every in-scope screen, transition, authored layout/script source, required asset,
optional asset, audio cue and evidence gap. Do not infer full Original coverage from archive counts.>

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

1. ☐ Inventory every in-scope menu journey and its original evidence
2. ☐ Decode menu layouts during ROF extraction and document the format
3. ☑ Add the global options store and requested/active presentation resolution
4. ☐ Define the presentation, feature, input, audio, launch and return contracts

### Wave B — Free Flight tracer

11. ☐ Characterize Built-in and extract the Free Flight feature
12. ☐ Run Built-in Free Flight through the presentation boundary
13. ☐ Deliver Original Free Flight, switching and recovery

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

## A1 ☐ Inventory every in-scope menu journey and its original evidence

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

**Verify.** <TODO: exact inventory validator or review query>; manually show that every current
`--menu=` aid and every `LaunchMenu` screen enum appears exactly once in the inventory.

**⚠ Traps.** A filename or rectangle proves composition, not interaction. Existing campaign
fidelity does not prove non-campaign coverage.

## A2 ☐ Decode menu layouts during ROF extraction and document the format

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

**Verify.** <TODO after A1: exact extractor fixture command and real-tree census>; run extraction to
`./.scratch/<descriptive-menu-layout-path>` and print that workspace-relative path.

**⚠ Traps.** Do not decode executable behaviour at runtime. `packaging/Extract.ps1` stays a dispatcher;
all extraction logic remains in `ExtractRof.ps1` or code it directly owns.

## A3 ☐ Add the global options store and requested/active presentation resolution

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

**Verified.** <pending orchestrator run>
Ran `dotnet build CSVM/CSVM.sln` and
`dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~OptionsStoreTests|FullyQualifiedName~PresentationResolutionTests|FullyQualifiedName~SessionSpecParserTests"`,
both green; the full `.\RunTests.ps1` landing gate is the orchestrator's to run.

## A4 ☐ Define the presentation, feature, input, audio, launch and return contracts

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
namespaces. <TODO: choose the enforceable dependency test after the file/assembly boundary is known.>

**⚠ Traps.** Do not create a universal row/button/picture schema. “Presentation” includes navigation,
interaction, animation and cue selection; drawing alone is not the boundary.

# Wave B — Free Flight tracer

## B11 ☐ Characterize Built-in and extract the Free Flight feature

**Goal.** Capture Built-in’s present Free Flight behaviour, then move its selections and validation
into an engine-free typed feature without changing any observable journey.

**Evidence (confidence: traced).** Free Flight currently follows Mode → Chapter → Plane and shares
the final aircraft screen with other modes (`docs/architecture.md:3901-3913`). `LaunchMenu` owns the
state and launch construction today (`CSVM/src/UI/LaunchMenu.cs:243-278`, `2248-2250`).

**Approach.** Add characterization tests for navigation, Back, chapter/aircraft choices, launch
payload, return and relevant CLI aids before extraction. Move only Free Flight state/operations and
the typed launch request; leave Instant Action, Dogfight, campaign and hangar on their old paths.

**Model recommendation.** high — this tracer must cut a safe seam through a large stateful class.

**Verify.** Run the focused unit tests plus before/after Built-in screenshots and the scripted
top-level → launch → return journey. <TODO: exact existing/new suite names and `--menu=` aid values.>

**⚠ Traps.** Do not repair existing mouse, focus or layout issues. Characterization pins current
behaviour, including quirks not explicitly changed by this plan.

## B12 ☐ Run Built-in Free Flight through the presentation boundary

**Goal.** Built-in becomes a registered presentation and completes the Free Flight tracer through
the new host with no intended visual or behavioural change.

**Evidence (confidence: lead-only).** The boundary is a negotiated design; B11 supplies its first
characterized feature and A4 its contract.

**Approach.** Split the process-lifetime menu host from Built-in controls and screen graph. Route
semantic commands, audio service calls, typed exit and semantic return through the host. Keep all
other current flows operational through temporary adapters rather than rewriting them early.

**Model recommendation.** high — lifecycle and return regressions can strand every menu path.

**Verify.** Repeat B11’s comparisons, exercise return from a real Free Flight session, and run all
existing menu/campaign capture suites. <TODO: exact targeted engine suite names.>

**⚠ Traps.** Re-entry after flight is part of the tracer. A presentation that only works on cold
startup has not proved the seam.

## B13 ☐ Deliver Original Free Flight, switching and recovery

**Goal.** Original completes Free Flight with its own decoded screen graph, 4:3 presentation,
mouse/keyboard/pad interaction, audio cues, live switching and force-Built-in recovery.

**Evidence (confidence: lead-only).** `BoardFit` and current campaign boards prove the desired
scaling/rendering pattern, but A1/A2 must establish the non-campaign screen data.

**Approach.** Build the Original shell over decoded layouts and the shared Free Flight feature.
Implement pointer hit-testing/rollover and equivalent semantic navigation. Add the minimal Built-in
Options route and Original chooser, discard unfinished setup on switch, and restart at the target
presentation’s top level. Keep Original CLI-only.

**Model recommendation.** high — first complete alternative presentation and first pointer path.

**Verify.** Compare at 4:3, wide and tall resolutions; test every input family, switching from each
Free Flight step, saved request, CLI override and force-Built-in recovery. <TODO from A1: reference
captures and exact original interaction sequence.>

**⚠ Traps.** The 800x600 space is authored coordinates, not a fixed render target. Do not stretch
wide or switch to integer-only scaling; `BoardFit` already records both rejected alternatives.

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
