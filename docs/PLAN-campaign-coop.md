# Campaign co-op: splitscreen in the story campaign

**ACTIVE PLAN** (written 2026-08-29). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan makes a campaign mission playable by two to four humans in one shared world on the
existing `SplitScreen` rig, as co-op: every human sits on team id `1`, objectives count for the
whole human field with the first one to reach an objective settling it, and cutscenes play
fullscreen for everybody. It is deliberately the smallest version of that idea. One profile is
seated and written, guests bring no profile of their own, the authored AI roster is untouched, and
no campaign screen becomes multi-pane. The design was settled in a grilling session on 2026-08-29;
the Decisions table below is its record and is the authority wherever the prose here disagrees
with itself.

The plan draws no items from `backlog.md`, so no re-verification of backlog entries was needed.
Two adjacent backlog items are cited as context rather than absorbed: `BL-434` (splitscreen
cockpit interior and audio cost at four players, unprofiled) is what `D33` measures against, and
`BL-313`'s poisoned-record failure mode is the reason `D31` keeps a guest's gunnery out of the
seated profile. Neither is closed by this plan.

## Milestone goal

- A campaign mission launches with one to four humans from the campaign screens, each with a pane,
  a camera, a HUD and a pad, all on the player's side.
- Every objective condition that named one aircraft now reads the whole human field, with the
  first human to satisfy it settling the objective.
- A capture, a drop-off and every other mission trigger belong to the human who performed it: that
  human is staged, re-placed and, where the mission swaps an airframe, put in the new aeroplane.
- A cutscene fills the window rather than four panes, and any player can skip it, with the skipper
  named on screen.
- A mission ends when the objectives say so or when every human is lost, and a downed player
  watches from a spectator camera until then.
- Exactly one profile is read and written by a co-op sortie, and a guest can neither spend nor
  change anything in it.

**A guest flies but keeps no career.** Guests fly, shoot, capture and die, and nothing about them is
persisted. Every route by which a second player could touch the seated profile is closed by
construction rather than by care, which is what keeps one writer at mission end and one set of
progression rules to reason about.

## Decisions (2026-08-29)

| # | Question | Decision |
|---|---|---|
| 1 | Co-op or free-for-all with guests? | **Co-op.** Every human on `AimAssist.PlayerTeam`, no friendly fire. `--coop` already does this for free flight, and the authored AI teams assume one player side. |
| 2 | Whose campaign is a co-op sortie? | **The seated profile's, and only it.** Guests seat no profile. Progression, awards, funds and the persist log keep exactly one writer. |
| 3 | Where do guests pick an aircraft? | **Sequential flight checks.** P1's normal flight check, then `FLIGHT CHECK P2/P3/P4` in the same single window; FLY MISSION advances to the next joined player and launches after the last. No campaign screen becomes multi-pane. |
| 4 | What does a guest's aircraft record cost, and what survives? | **Nothing, and nothing.** A guest flies a session-scoped record: a stock airframe seeded from `stock_loadouts.json`, or a copy of one of the seated profile's aircraft. Ammo edits are free and discarded. The seated profile's stored record is never mutated by a guest flying it. |
| 5 | Where do guests spawn? | **`StartGrid`, anchored on the mission's `PLAYER_INIT`.** The existing abreast grid already delegates its anchor, so `--pos`, `--spawn-at` and the fallbacks keep working. Missions whose start point is too tight for the grid get fixed by playtest, not by a second placement path. |
| 6 | Do guests displace AI wingmen? | **No.** Guests are additive; the authored roster spawns whole. A balance pass is a later item, not this plan's. |
| 7 | What does "objectives count for everybody" mean per seam? | **Conditions read the human field, first one wins; the scripted player stays P1.** `TravelersMet` any-human, danger zones unioned, `GroupLiveCount` counts every live human. `ResolveLeader("player")` and `NetTrailerTargets` stay P1, because an escort and an anchored net must each follow one aeroplane. |
| 8 | Who captures the aeroplane a mission hands over? | **Whichever human triggers it.** The swap follows the episode owner instead of the hard-coded `_rigs[0]`. `wingman_4` keeps whatever airframe it resolved at mission start, so a guest capture leaves a cosmetic duplicate in two missions. |
| 9 | When does a co-op mission end on death, and what does a dead player do? | **Ends when every human is lost; a downed player gets a spectator camera.** Respawning was rejected: it makes the loss rule unreachable and puts a fresh aeroplane into an authored mission at a grid slot far from the fighting. |
| 10 | How do cutscenes play fullscreen? | **Pane 0 expands to the full window** for the duration, the other panes and the gutters hide, restored on the definition's end or on a skip. One 3D listener for the duration. Any player may skip, and the skipper is named on screen. |
| 11 | What about the other triggers that name one aircraft? | **Per human, first one wins, and the episode remembers the winner.** The ladder switch latches to one owner at a time so two qualifying players cannot thrash `drop_ladder`/`retract_ladder`. |
| 12 | Which chrome is per pane? | **`ObjectivesHud` per rig, mission text per pane, radio audio session-wide.** Pause needs no work: `PauseState` already pauses on claim and resumes only for the claiming player. |
| 13 | What does a co-op sortie record? | **The seated pilot's `Shots`, `Hits`, `Airframe` and `PlaneName`, team-aggregate `Money`.** Gunnery stays P1's because those fields feed a best-of record a later solo run is measured against; money is banked rather than merged, so the squadron gets paid. |
| 14 | Four humans, one `player` marker. | **The episode owner goes on the marker; the other humans hold `Inert` where they are.** A mission intro has no triggering human, so its owner defaults to the scripted player. |
| 15 | What does the command line look like? | **`--campaign=<profile>:<seq> --players=N`, with `--plane=a,b,c` naming guests.** Entry 0 also overrides the seated aircraft ahead of the profile, warned, so a golden needs no hand-authored profile on disk. `--coop` is redundant in a campaign session and ignored. |
| 16 | What is out of scope? | **Guest profiles, hangar and purchases, wingman displacement, AI-side airframe swap, difficulty scaling, network play, multi-pane campaign screens, per-guest debrief rows.** Instant Action, Dogfight and the stunt race are untouched. The balance pass and per-guest debrief rows become backlog items rather than refusals. |
| 17 | What is the verification bar? | **Two unit surfaces, five engine suites, two goldens, four playtest items, plus a 4P perf and hitch measurement.** The cutscene suite is required because the collapse touches the pinned splitscreen listener model, which regresses into total silence. |
| 18 | What are the words? | **Scripted player, human field, guest, seated profile, episode owner**, plus a clause on **Versus band** saying a co-op campaign does not use it. |

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

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — The seams

1. ☑ `RaceGrid` becomes `StartGrid`, and answers a campaign mission's anchor
2. ☑ A campaign session accepts more than one player
3. ☑ `IObjectiveWorld` grows the human field, and the scripted player keeps its meaning
4. ☑ The episode owner: a cutscene episode knows which human started it

### Wave B — In the mission

11. ☑ Landing-approach rows, pickup sensors and the ladder switch read the human field
12. ☑ Cutscene staging places the episode owner on the `player` marker
13. ☐ A downed human spectates; the mission ends when the last one is lost
14. ☐ A cutscene fills the window
15. ☐ Per-pane campaign chrome

### Wave C — The screens

21. ☐ Joining and leaving from any campaign screen, with the player chip strip
22. ☐ Sequential per-player flight checks
23. ☐ `CampaignLaunch` carries the whole human field

### Wave D — Recording, evidence and words

31. ☐ What a co-op sortie writes to the seated profile
32. ☐ Goldens and playtest items
33. ☐ The 4P cost of a heavy campaign mission
34. ☐ The five terms in `CONTEXT.md`

## Dependency and parallelism notes

`A2` blocks everything downstream: until a campaign session can build more than one rig, no item
below can be exercised at all, and it is the first thing to land. `A1` is independent of `A2` and
can run beside it. `A3` and `A4` are independent of each other and both depend only on `A2`.

Wave B depends on Wave A entirely. Inside it, `B11` and `B12` both build on `A4`'s episode owner
and are a chain in that order, because `B12` uses the owner `B11` resolves. `B13`, `B14` and `B15`
are independent of each other and of `B11`/`B12`.

Wave C depends on `A2` for the spec shape and on `C23` as its own internal chain: `C21` then `C22`
then `C23`. It does not depend on Wave B and can run in parallel with it.

`D31` depends on `A2` and on nothing in Wave C. `D32` depends on every behaviour item landing.
`D33` depends on Wave A and `B15`. `D34` is independent of everything and can land first.

**File contention.** `GameSession.cs` is edited by `A2`, `A4`, `B13`, `B14` and `B15`; never run
those in parallel worktrees. `CampaignDirector.cs` is edited by `A3`, `B13` and `D31`, and
`CutsceneController.cs` by `A4`, `B12` and `B14`; the same rule applies to each group.
`LaunchMenu.cs` is edited by `C21`, `C22` and `C23`, which are a chain anyway.

---

# Wave A — The seams

## A1 ☑ `RaceGrid` becomes `StartGrid`, and answers a campaign mission's anchor

**Goal.** Four humans launching a campaign mission come up abreast on one heading at one altitude,
placed by the same grid the stunt race uses, and the type's name no longer claims it is only for
racing.

**Evidence (confidence: traced).** `CSVM/src/Session/RaceGrid.cs` is already the second
`IFlightStarts`: slot `i` of `n` sits `(i − (n−1)/2) × spacing` metres along the perpendicular to
the anchor heading, and its anchor is `SpawnPicker.ChooseSpawn(playerIndex: 0)` by delegation, so
the ia.json list, `objectives.json`'s `PLAYER_INIT`, the C1 last-resort fallback and `--pos` all
keep working without the grid knowing they exist (`docs/architecture.md`, `## src/Session/RaceGrid.cs`).
Its heading comes from the anchor's own position-to-look-at pair rather than the spawn's
`HeadingDeg`, and terrain arrives as an injected `Func<Vector3, float?>`. A campaign mission's
anchor is its single `PLAYER_INIT`, read by `SpawnPoints.LoadPlayerInit`
(`CSVM/src/Flight/SpawnPoints.cs`). The rename's blast radius is five files: `RaceGrid.cs`,
`Utils/Config.cs`, `CSVM.Tests/RaceGridTests.cs`, `docs/architecture.md` and `playtest.md`.

**Approach.** Rename the type, the file and `CSVM.Tests/RaceGridTests.cs` to `StartGrid`, and
rename the two self-registered TUNE keys `raceGrid.slotSpacing` and `raceGrid.groundClearance` to
`startGrid.*` in `Config.WarmTuningRegistry` so `--dump-config` lists them under the new name. Then
have the campaign build path use `StartGrid` as its `IFlightStarts` whenever `Players > 1`, exactly
as the splitscreen stunt race does. Do not add a campaign-specific spacing constant: decision 5
says a tight start point is a playtest fix, not a second placement path.

**Model recommendation.** medium, low effort. A mechanical rename over five files plus one call
site; the judgement was already made.

**Verify.** `.\RunTests.ps1 -UnitFilter "FullyQualifiedName~StartGrid" -SkipEngine -SkipGoldens`
green, then `--campaign=<profile>:<seq> --players=4 --det --screenshot=` on a mission with an open
`PLAYER_INIT` and confirm four aircraft abreast. `--dump-config` lists `startGrid.slotSpacing` and
`startGrid.groundClearance` and no longer lists the old spellings. Full `.\RunTests.ps1` before
landing.

**⚠ Traps.** A user `config.json` carrying the old `raceGrid.*` keys silently stops applying after
the rename rather than failing; that is accepted, but say so in the landing commit. Do not re-read
the spawn list entry for a heading: the grid takes its heading from the anchor pair precisely so
`--direction` is not silently ignored, and that reasoning survives the rename.

## A2 ☑ A campaign session accepts more than one player

**Goal.** `--campaign=<profile>:<seq> --players=3` builds a three-pane campaign session, and the
cabin's FLY MISSION can do the same for however many players have joined.

**Evidence (confidence: traced).** `SessionSpec.FromCampaign` hard-codes `Players = 1`
(`CSVM/src/SessionSpec.cs:1199`). The CLI arbitration clamps `Players` to
`UI.SplitScreen.MaxPlayers` and then drops it back to 1 with a printed complaint unless the
resolved mode is flight (`CSVM/src/SessionSpec.cs:1535-1543`); a campaign session resolves to
`SessionMode.Fly`, so that gate already passes. `--plane=` already gives one aircraft per
splitscreen player, and a multi-entry list without an explicit `--players=` sets the player count
from its length (`:1535-1537`). `SessionSpec.Coop` puts every human on `AimAssist.PlayerTeam`.

**Approach.** Take the player count as a parameter of `FromCampaign` rather than a constant, and
have the campaign path resolve co-op unconditionally: every human on `PlayerTeam`, `--coop` ignored
without a warning because it asks for what the mode already gives. Teach the `--plane=` handling
that in a campaign launch entries 1 and up name guests and entry 0 overrides the seated aircraft
ahead of the profile's `SelectedPlane`, with a warning naming what it overrode. Leave
`SessionSpec` pure: it takes the count and the names as values, and the profile lookup stays
`CampaignDirector`'s and the launchscreen's.

**Model recommendation.** high. Small in lines and wide in consequence: every item below reads
this spec, and `SessionSpec` is the one immutable value the whole build derives from.

**Verify.** `--campaign=… --players=4 --det --screenshot=` renders four panes over a campaign
chapter. `--campaign=… --plane=player_fury,player_bhawk` builds two players with the named
airframes and prints the override warning for entry 0. Confirm `--coop` with `--campaign=` prints
nothing. A 1P campaign launch is byte-identical in behaviour to today: same log line, same single
main-viewport camera, no `SplitScreen` node constructed. Full `.\RunTests.ps1` before landing.

**⚠ Traps.** Do not let the multi-entry `--plane=` player-count inference fire on a campaign launch
that named several planes for another reason; an explicit `--players=` must still win, as
`:1535` already arranges. `SplitScreen.Build` is deliberately never constructed at one player, and
that must stay true for the campaign path so the 1P story mode is untouched.

## A3 ☑ `IObjectiveWorld` grows the human field, and the scripted player keeps its meaning

**Goal.** Every objective condition that used to ask about one aeroplane now asks about every
human and is satisfied by the first one, while the authored `player` token still resolves to a
single aircraft.

**Evidence (confidence: traced).** `CampaignDirector`'s world adapter answers
`Player() => _in.PlayerAircraft?.Invoke()` (`CSVM/src/Session/CampaignDirector.cs:1010`), and six
call sites read it: the danger-zone update (`:438`), the death wiring (`:480`), the wreck-fall
check (`:509`), the damage-ping wiring (`:605`), the `DEDG` group live count (`:853`) and the
`TRAVELERS` proximity condition (`:1017`). A seventh lives outside the adapter:
`CampaignRosterPlan.ResolveLeader(spawn.LeaderName, _roster, inputs.Player())` (`:352`).
`ObjectiveGraph` reaches the world only through `IObjectiveWorld` and is pinned off-engine by
`CSVM.Tests/ObjectiveGraphTests.cs`, so the whole condition surface is unit-testable without an
engine. `ObjectiveSites` is already offered to each player's `TargetPool` and needs nothing.

**Approach.** Add `Humans()` to `IObjectiveWorld` beside `Player()`, and fix the two meanings in
the interface's own documentation: `Player()` is the scripted player, the one aeroplane an authored
`player` token means, and it stays P1; `Humans()` is the human field, which every condition reads.
Then move the seams per decision 7: `TravelersMet` true when the nearest human satisfies it, the
danger-zone update run per human so flags union, `GroupLiveCount` counting every live human in the
group. Leave `ResolveLeader` and `NetTrailerTargets` on `Player()`, and put a `⚠` line on each
saying why, because those are the two a later reader will assume were missed.

**Model recommendation.** high. The split between the two meanings is the conceptual core of this
plan, and getting it wrong is invisible until a mission misbehaves.

**Verify.** Extend `CSVM.Tests/ObjectiveGraphTests.cs` with a fake world carrying N humans: assert
`TravelersMet` fires on the nearest human and not only the first, danger zones union across humans,
`GroupLiveCount` counts every live human, and `Player()` still answers P1 for the escort and net
paths. Then a shipped-mission engine suite drives the real director. Full `.\RunTests.ps1`.

**⚠ Traps.** `DANGER_ZONES_COMPLETED` counts only zones flagged while the objective was awake, and
that quirk is decoded and deliberate; unioning across humans must not quietly widen the window.
`ObjectiveGraph` completes at most one objective per tick from a rotating scan index, so a
condition that becomes true for two humans on the same tick still yields one completion, which is
correct and must not be "fixed".

## A4 ☑ The episode owner: a cutscene episode knows which human started it

**Goal.** When a human triggers a cutscene, that episode belongs to them: the airframe swap puts
*them* in the new aeroplane, and later items place *them* on the staged marker.

**Evidence (confidence: traced).** `CutsceneController` exposes one swap seam,
`Func<AirframeSwapOrder, AirframeSwapResult>? SwapAirframe` (`CSVM/src/Session/CutsceneController.cs:64`),
filled by `GameSession` at `:540` with `SwapPlayerAirframe`, which calls
`_flightRoster.RunSwap(_rigs[0], order, …)` (`CSVM/src/Session/GameSession.cs:3104`). The rig index
is a literal. The controller already holds the rig list (`CutsceneController.cs:110`) and binds it
through `BindRigs` (`:288`). Scoping is by definition name, never by authored code
(`docs/architecture.md`, `## src/Session/CutsceneController.cs`), and `Own` is the existing slot a
mission trigger uses to claim an episode before the first callback lands.

**Approach.** Give the episode a rig alongside the definition it already tracks: `Own` takes the
triggering rig, and an episode nobody claimed defaults to the scripted player's rig. Widen
`AirframeSwapOrder` (or the seam's signature) so `GameSession.SwapPlayerAirframe` receives the
owning rig instead of reading `_rigs[0]`. Nothing else about the swap changes: the removal still
precedes the build for the reason `FlightRoster`'s entry gives, and the livery stream is still
captured and restored around it.

**Model recommendation.** high. The swap path carries a documented ordering constraint whose
violation silently drops near-miss registrations, so this needs a reader who takes that entry
seriously.

**Verify.** A `campaign-coop-episode-owner` engine suite: with two rigs, drive the trigger from rig
1 and assert the replacement aircraft is rig 1's, that rig 0's aircraft is untouched, and that the
shooter id and near-miss registrations follow the swapped pilot. The existing `AirframeSwapSuites`
and `CutsceneSkipSuites` must stay green unchanged, since they drive one rig. Full `.\RunTests.ps1`.

**⚠ Traps.** `⚠` The removal has to precede the build in `SwapPlayerAirframe`, because
`DetachRosterBindings` drops every near-miss registration carrying that pilot's shooter id and the
replacement registers its own under the same id. Do not generalise `_rigs[0]` by passing the rig of
whichever aircraft is nearest the definition's camera; the owner is the human who satisfied the
trigger, resolved in `B11`, and nothing else.

---

# Wave B — In the mission

## B11 ☑ Landing-approach rows, pickup sensors and the ladder switch read the human field

**Goal.** Any human can fly the cone that starts a mid-mission cutscene, sit in a pickup sensor, or
hover the ladder into place, and the mission behaves as it does today for whichever human got there
first.

**Evidence (confidence: traced).** `LandingApproachRuntime` ticks a story mission's resolved
`LandingApproaches` against the flown aircraft over four gates (arming, speed band, attitude cone,
condition volume) and starts the row as an explicit mission trigger, handing the row's definition
to `CutsceneController.Own` first; a row fires once per entry into its volume, and `AutoLandOffered`
is the HUD prompt with `FlightController.AutoLandPressed` behind it. `LadderSwitchRuntime` reads the
flown aircraft's attitude and position against the mission's pickup sensors every frame outside a
cutscene, and `LadderSwitch` holds a transient state until `CALLBACK 123` settles it
(`docs/architecture.md`, both entries; `docs/org/ladderSwitch.md` for the decode).

**Approach.** Tick each of the three per human rather than against one aircraft, take the first
human that satisfies the gates, and hand that rig to `CutsceneController.Own` as the episode owner
from `A4`. Keep `AutoLandOffered` per pane so only the qualifying human sees the prompt and only
they can press it. Give the ladder a single-owner latch: the first qualifying human holds it, and
the switch does not re-evaluate to another human until that one stops qualifying.

**Model recommendation.** high. The ladder latch is a state machine holding a transient state
across a callback, and a naive per-human loop produces a thrash that is hard to see in a log.

**Verify.** A `campaign-coop-ladder` engine suite with two qualifying humans asserting exactly one
`drop_ladder` start and no `retract_ladder` while both qualify. A `campaign-coop-episode-owner`
extension asserting the row fires once when two humans enter the volume on the same tick and that
the owner is the one that satisfied it. Then a shipped mission at the controls (`D32`). Full
`.\RunTests.ps1`.

**⚠ Traps.** CM07's train-pickup cone is opened and closed by the train's own `pickup_timing`, and
the trigger deliberately starts nothing off the pickup sensor: restarting that timing on entry
re-phased the switch the passenger's wave-or-drop fork reads. A per-human loop must not become a
per-human *restart*. CM02 grafts its three Balmoral cones with the roster, so rows can bind after
the humans exist; the per-human tick must tolerate a row arriving late.

## B12 ☑ Cutscene staging places the episode owner on the `player` marker

**Goal.** A drop-off, a hookup or a hand-back poses the human who earned it on the mission's staged
`player` marker, and leaves the other humans held where they were rather than stacked on the same
point.

**Evidence (confidence: traced).** Code `11` takes the player out of flight and poses the airframe
on the staged `player` marker through `FlightController.StageAt`, asserted in the same dispatch
rather than on the next tick because the definition goes on posing the aircraft. Code `951` is the
re-placement: it reads that marker's world pose and moves the hand-back target through
`FlightController.ResumeAt`, so a mid-mission drop or hookup leaves the pilot where its own
definition parked that node. Code `20` holds the world and the objectives session-wide, so no human
is flying during an episode. There is exactly one `player` marker.
(`docs/architecture.md`, `## src/Session/CutsceneController.cs`.)

**Approach.** Apply `11` and `951` to the episode owner's rig from `A4`. Hold every other human
`Inert` where it stands for the duration and resume it there, adding no second staging geometry.
An episode with no triggering human, which is every mission intro, owns the scripted player's rig
and behaves exactly as it does today.

**Model recommendation.** high. The same-dispatch assertion in code `11` is a decoded ordering
subtlety, and a per-human generalisation is where it would get lost.

**Verify.** A two-rig engine suite driving a shipped mission's drop-off: assert the owner is posed
on the marker, the non-owner is `Inert` at the pose it held before the episode, and both are back
in play after the hand-back with the non-owner at its own position. A 1P run of the same mission is
unchanged. Full `.\RunTests.ps1`.

**⚠ Traps.** Codes 913/914 park and reveal the *AI*, and only what the controller parked comes
back; do not route a held human through that path. Nothing in this item ends the presentation, and
the definition ending is still what does that.

## B13 ☐ A downed human spectates; the mission ends when the last one is lost

**Goal.** Losing one aeroplane in a four-player sortie costs that player their aircraft and nothing
else. The mission ends only when every human is down, and a dead player watches from a camera they
control.

**Evidence (confidence: traced).** `WirePlayerDeath` subscribes `Downed` on the first step that
finds an aircraft (`CampaignDirector.cs:478-486`); `OnPlayerDown` gates on `EndsOnPlayerDeath` and
calls `Graph.NotifyPlayerLost()` (`:493-500`); `StepPlayerLost` waits for `WreckFalling` to clear
and then calls `graph.EndAfterPlayerLost()` (`:506-515`). `--no-crash-loss` is the existing opt-out
(`SessionSpec.cs:210`). `SpectatorCamera` is pad-driven with `X` cycling the orbit lock, and
`GameSession.LockCandidateAircraft` already supplies every `InPlay` aircraft, AI and human, **to
all four spectator cameras** (`docs/architecture.md`, `## src/Flight/SpectatorCamera.cs`).

**Approach.** Subscribe `Downed` per human rather than on one aircraft, and make the loss condition
"every human has gone down and every wreck has stopped falling" before `EndAfterPlayerLost` runs.
On a single human's death, swap that rig's camera for a `SpectatorCamera` fed by
`LockCandidateAircraft`, after the crash cam has run as it does today. `--no-crash-loss` keeps its
meaning, disabling the end rather than the spectator swap.

**Model recommendation.** high. The end condition sits at the join of `A3`'s human field and the
graph's own loss path, and getting it wrong either ends a co-op mission on the first death or never
ends it at all.

**Verify.** A `campaign-coop-death` engine suite: with two rigs, down rig 1 and assert the graph is
still running and rig 1 holds a spectator camera; then down rig 0 and assert `NotifyPlayerLost`
followed by `EndAfterPlayerLost` once both wrecks have settled. A 1P campaign death is unchanged.
Full `.\RunTests.ps1`.

**⚠ Traps.** `StepPlayerLost` deliberately waits on `WreckFalling`, so the mission ends where the
wreck does; with N humans that becomes N wrecks and the wait is for the last one. Do not end on the
last `Downed` event. `AircraftLifecycle`'s `AutoRespawnAfter` exists and is the Dogfight path;
decision 9 rejects respawning here, so leave it unarmed for a campaign session.

## B14 ☐ A cutscene fills the window

**Goal.** A cutscene in a splitscreen campaign session plays across the whole window rather than in
four small copies, and the player who skips it is named.

**Evidence (confidence: traced).** `CutsceneController.BindRigs` re-applies the cutscene state to
the session's rigs, and every rig camera takes the pose the definition put `camera1` in, each
frame; the controller ticks last (`ProcessPriority` 1000) so the bars, posed inside that advance,
never sit against a camera one frame behind them. So all four panes already show the same cutscene.
`SplitScreen` is a `CanvasLayer` at `HudLayers.WorldOverlay` holding a black backdrop and one
`SubViewportContainer` per player, laid out by the public static `PaneRect`; every pane is an
`AudioListenerEnable3D` listener, and Godot 4.7 takes the per-channel maximum over all
listener-enabled viewports of the `World3D`. The letterbox card is fitted to the pane it covers with
a deliberate overhang margin (`CutsceneController.cs:38`, `:192`).

**Approach.** For the duration of an episode, set pane 0's container to the full window rect and
hide panes 1 to N−1 and the gutter backdrop; restore on the definition's end and on a skip, which
are already the two exits. Pane 0 keeps its own camera, `HudParent`, listener and per-player visual
layer, so nothing is rebuilt and the skydome copy it culls to is still its own. The letterbox
re-fits to the new aspect through the existing `cardBox` computation. Any player's skip input
force-stops the definition as the player's skip does today, and the collapse prints a line naming
the skipper in that player's `SplitScreen.PlayerColor`, plus one `Log.Info` line.

**Model recommendation.** high. The collapse touches the pinned listener model, whose failure mode
is a session with no listener at all, in which every `AudioStreamPlayer3D` goes silent, uncounted
and unlogged.

**Verify.** A `campaign-coop-cutscene-fullscreen` engine suite asserting pane 0 at the full window
rect during an episode, panes 1 to N−1 hidden, the layout restored on both exits, and **exactly one
listener-enabled viewport** for the duration. A golden `--det --screenshot=` of the collapsed frame
(`D32`). A run with `--volume=0` rather than `--mute`, so audio still loads, counts and logs and
`.scratch/logs/` can be read for emitter counts across the collapse. Full `.\RunTests.ps1`.

**⚠ Traps.** `--mute` is a load-time switch, so a muted baseline is blind to exactly the audio
failure this item risks; use `--volume=0`. Do not restore the panes by rebuilding the rig; the
per-player visual-layer band and the pane cull masks are allocated once at build. The main-viewport
camera deliberately stands down in splitscreen and must stay down, which is why the collapse
expands a pane rather than re-arming it.

## B15 ☐ Per-pane campaign chrome

**Goal.** Every player reads the objective list and the mission text in their own pane.

**Evidence (confidence: traced).** `ObjectivesHud` is built once and added to `_worldRoot`
(`CSVM/src/Session/GameSession.cs:2622`), while every other HUD lives under its rig's own
`HudParent` inside that pane's `SubViewport` (`docs/architecture.md`, `## src/Flight/PlayerRig.cs`).
`SplitScreen` is a `CanvasLayer` at `HudLayers.WorldOverlay` with a black backdrop, so a
main-viewport overlay in a splitscreen session is drawn behind the panes. `ObjectiveSites` is
already offered to each player's `TargetPool`. `PauseState` already carries `OwnerPlayerIndex` and
"pauses and claims ownership; resumes only if the same player index"
(`CSVM/src/Flight/PauseState.cs:29-46`), so pause needs no work at all.

**Approach.** Build one `ObjectivesHud` per rig under its `HudParent`, reading the same director,
and do the same for on-screen mission text. Leave the radio and `PlaySoundGroup` session-wide: one
voice for the room. Touch `PauseState` for nothing.

**Model recommendation.** medium. Mechanical once the seam is understood, with the existing
per-rig HUD construction as the pattern to copy.

**Verify.** `CampaignHudSuites` extended to two rigs: both HUDs carry the same display rows from one
graph, and a completion updates both. `--campaign=… --players=4 --det --screenshot=` shows the
objective list in every pane. 1P is unchanged, including the node the HUD is parented to. Full
`.\RunTests.ps1`.

**⚠ Traps.** `ObjectivesHud` polls for the graph in `_Process` rather than at construction, which
`CampaignHudSuites` documents; N instances must each poll rather than one instance broadcasting.
Do not fold the objectives list into the existing per-rig HUD node: it has its own draw and its own
suite, and merging them would put a campaign-only surface into every session shape.

---

# Wave C — The screens

## C21 ☐ Joining and leaving from any campaign screen, with the player chip strip

**Goal.** A second, third or fourth player presses Start on any campaign screen and is in; they
press B and they are out; and whenever more than one is joined, a coloured `P1 P2 P3 P4` strip in
the top right says so.

**Evidence (confidence: traced).** `LaunchMenu` already owns a joined-player list of `Slot`
(`CSVM/src/UI/LaunchMenu.cs:223`), each with its own `MenuInput`, plane index, lock, confirm and
per-slot `LoadoutChoice`. `ScanJoins` reads `MenuInput.JoinPressed(pad)` per pad and refuses a
claimed pad or a full field (`:942-952`). A join strip is drawn on every non-board screen
(`:2770-2809`), and boards record `_stripText` rather than drawing it (`:2287-2290`) because a
composed board draws no strip. `ShowMenu` documents that joined players survive a return from
flight while their plane locks do not (`:551`). `DebugJoin` adds deviceless players for
screenshots (`:663`). `Screen.Campaign` is one of twelve screens (`:323`), and campaign pages draw
as composed boards, not the shared `BoardMenu` idiom.

**Approach.** Allow `ScanJoins` to run while `_screen == Screen.Campaign`, up to and including the
seated player's FLY MISSION, after which the field is locked. Add a chip strip drawn over the
composed board in the top right, one chip per joined slot in `SplitScreen.PlayerColor(index)`,
shown only when more than one player is joined so a solo campaign looks exactly as it does today.
Handle B as leave for a guest slot; B on the seated player's slot keeps its existing screen-back
meaning. <TODO: the chip strip's geometry over a composed board is unspecified. `CampaignBoards`
supplies board geometry and a page contributes `Pictures`/`Strokes`/`Captions`; settle whether the
strip is a page contribution or a shell overlay before drawing it.>

**Model recommendation.** medium. Additive UI over an existing join system, with the composed-board
overlay as the one new drawing surface.

**Verify.** `--menu=campaign --debug-join=3 --det --screenshot=` shows the chip strip over the
cabin, the briefing and the flight check. A join attempted after FLY MISSION is refused. A guest
leaving removes their chip and their flight check. A solo campaign draws no strip. Full
`.\RunTests.ps1` is not required for a `CSVM/src/UI` change only if nothing under `CSVM/` moved,
which it has, so run it.

**⚠ Traps.** `PrimeJoins` exists so a Start still held from the transition does not fire
immediately, and every new entry point into joining needs the same priming. A composed board draws
no join strip by design, so the chip strip is a deliberate exception and must not resurrect the
full strip on a board.

## C22 ☐ Sequential per-player flight checks

**Goal.** After the seated player's flight check, each joined guest gets the same window headed
`FLIGHT CHECK P2` and so on, where they choose an aircraft and its ammunition, and FLY MISSION
advances to the next guest and launches after the last.

**Evidence (confidence: traced).** `CampaignFlightCheckPage` already carries the shape: rows are
`FlightRowKind` values including `ChangeAmmo` (into `CampaignScreen.Ammo`) and `ChangePlane`, and a
row names the slot it acts on, today `0` pilot and `1` wingman
(`CSVM/src/UI/CampaignFlightCheckPage.cs:10-33`). `CHANGE PLANE` cycles in place with no picker
screen. `CampaignLoadout.For(plane, fits)` turns one `OwnedPlane`'s stored indices into the
`LoadoutChoice` a launch hands the session, and `PylonRow` is the one decoder of the stored
one-based ordnance value. The stock fit for an airframe with no build on file comes from
`stock_loadouts.json` (`docs/formats/loadouts.md`), which the page already falls back to for the two
starter Devastators and a granted reward aircraft. The campaign ammo screen reads `HangarEconomy`
only for slot titles and charges nothing.

**Approach.** Generalise the page's slot from "pilot or wingman" to "which player", keeping the
wingman row on the seated player's page. A guest's roster is the eleven stock airframes plus the
seated profile's owned aircraft, minus any aircraft another player has already taken. A guest's
record is a session-scoped `OwnedPlane`: seeded from the stock fit for a stock airframe, or copied
from the seated profile's record for a campaign aircraft, and never written back. FLY MISSION on
the last joined player launches; on any earlier one it advances. `CampaignFlow`'s screen stack is
the mechanism, since a new screen there is one page factory plus one `Registry` line.
<TODO: settle whether a guest flight check is a new `CampaignScreen` value with its own `Registry`
entry, or the existing `FlightCheck` screen re-entered with a player index. Read
`CampaignFlow.Registry` and `ICampaignPage.OpeningRow` before choosing; the stack returns to an
already-open screen rather than stacking a second copy, which decides it.>

**Model recommendation.** high. The no-duplicate filter and the copy-not-reference rule for a
guest's record are where decision 4 is actually enforced, and a reference where a copy belongs
mutates the seated profile silently.

**Verify.** With three joined players, walk all three flight checks: an aircraft taken by P2 is
absent from P3's list; a guest editing ammunition on a copy of the seated profile's Bloodhawk
leaves `user://Profiles/<name>/profile.json` byte-identical afterwards; the seated player's own
page is unchanged from today, wingman row included. `--menu=campaign --debug-join=3
--det --screenshot=` for each page. Full `.\RunTests.ps1`.

**⚠ Traps.** `PylonRow` is the one decoder of the stored one-based ordnance value, and every screen
that reads the field calls it rather than subtracting one itself; a guest page that re-reads the
field its own way puts a different rocket on the flight check than the ammo screen just committed.
The seated profile is saved by `FlyCampaignMission` before launch (`LaunchMenu.cs:1886`); confirm
that save cannot capture a guest's edits.

## C23 ☐ `CampaignLaunch` carries the whole human field

**Goal.** The launchscreen hands the session one entry per player rather than one aircraft, and a
campaign session built from the menu is identical to the same session built from the command line.

**Evidence (confidence: traced).** `FlyCampaignMission` builds a `CampaignLaunch` from the profile
name, the mission sequence, one airframe node, one `CustomPlaneDef` and one `LoadoutChoice`
(`CSVM/src/UI/LaunchMenu.cs:1878-1902`), and `SessionSpec.FromCampaign` is its counterpart on the
spec side (`SessionSpec.cs:1183-1199`). A reward aircraft with no file in the build store falls
back to its own award template through `CampaignProgression.BuildForOwned`.

**Approach.** Make the aircraft half of `CampaignLaunch` a list, one entry per joined player in
player order, with entry 0 the seated player's exactly as today. Pass the count through to
`FromCampaign` from `A2`. Keep the profile name and mission sequence scalar: there is one seated
profile and one story position.

**Model recommendation.** medium. A shape change with a single producer and a single consumer.

**Verify.** A menu launch with three players and a command-line launch with the same three
airframes produce the same session: same `--dump-loadout` output per player, same spawn placement,
same log line. Full `.\RunTests.ps1`.

**⚠ Traps.** The reward-aircraft fallback must apply per entry, not only to entry 0, since a guest
may pick a granted aircraft from the seated profile.

---

# Wave D — Recording, evidence and words

## D31 ☐ What a co-op sortie writes to the seated profile

**Goal.** A co-op sortie records the seated pilot's own gunnery and the squadron's money, and
changes nothing else about progression.

**Evidence (confidence: traced).** `MissionAttempt` is
`(Seq, CompletedMask, TimeMs, Shots, Hits, Money, Airframe, PlaneName)`
(`CSVM/src/Session/CampaignProgression.cs:11-13`), fed into the original's best-of merge.
`CampaignProgression` also owns the monotonic position, the replay rule and the five aircraft
awards with their `AwardBuild`s. `CampaignDirector` records the attempt at mission end, merges
`CampaignPersistLog.Capture` into the profile, saves it, writes any award's build into
`CustomPlaneStore`, then starts the leaving hold.

**Evidence for the money split (confidence: lead-only).** That team-aggregate money is the right
call rests on discussion, not on a decode of what the original does with a second pilot, because
the original has none. It is a remake-only rule.

**Approach.** Aggregate `Money` across the human field and leave `Shots`, `Hits`, `Airframe` and
`PlaneName` reading the seated pilot alone. Change nothing about the merge, the position, the
awards or the persist log. Put a `⚠` line on the attempt construction saying why the gunnery fields
are deliberately not aggregated.

**Model recommendation.** medium. Small and local, with the reasoning already settled.

**Verify.** A two-rig engine suite: complete a mission with both humans scoring, and assert the
recorded attempt carries rig 0's shot and hit counts and the sum of both pilots' money. A 1P sortie
records exactly what it records today. Full `.\RunTests.ps1`.

**⚠ Traps.** `Shots`/`Hits` feed a best-of accuracy record that a later solo run is measured
against, and the store is in `user://`, so a poisoned record is not repaired by a later code
change. `BL-313` documents that same failure mode for the stunt scoreboard.

## D32 ☐ Goldens and playtest items

**Goal.** The two visual states this plan introduces are pinned against silent regression, and the
judgements only a person can make are queued as owed playtests.

**Evidence (confidence: traced).** `analysis/goldens/manifest.json` is the golden tripwire, pinned
`--det` shots as a command line plus a raw-pixel md5, with an `exercises` field that says what a
shot covers today, is capped at 250 characters and is rewritten on a re-pin rather than appended to
(a pre-commit hook enforces this). `New-ItemId.ps1` mints every `PT-`/`BL-` id under a shared lock.

**Approach.** Pin two goldens: a four-pane campaign mission, and the collapsed fullscreen cutscene
frame from `B14`. Mint four playtest items with `New-ItemId.ps1`: the join and sequential
flight-check flow at the controls; a capture performed by a guest, confirming the guest ends up in
the captured aeroplane; the spectator camera after a death; and skip attribution naming the right
player. Each names the plan item it judges. <TODO: which shipped mission each golden pins is
unchosen. The four-pane shot wants a mission with an open `PLAYER_INIT` (so `A1`'s grid is not the
subject) and the cutscene shot wants one of the nine story missions shipping a `cutscenes\`
directory; pick both from `docs/formats/campaign-missions.md` and name them here.>

**Model recommendation.** medium, low effort. Mechanical once the behaviours land.

**Verify.** `.\RunTests.ps1` with the goldens stage green on a clean tree, and each new golden shown
able to fail by perturbing the thing it covers. The playtest items appear in `playtest.md` with ids
from the script and no duplicates, which the pre-commit hook checks.

**⚠ Traps.** Run `New-ItemId.ps1` for every id rather than once and adding one; the counter falls
behind the file otherwise and the invented number is handed out again. A golden's `exercises` field
carries no item id, no date and no "also exercises" clause.

## D33 ☐ The 4P cost of a heavy campaign mission

**Goal.** The frame cost and hitch behaviour of four panes over a full campaign mission is a known
number rather than a surprise at the controls.

**Evidence (confidence: lead-only).** Four panes of a campaign chapter world with a complete roster,
generators, zeppelins, weather and world animation is the heaviest render this engine will have
attempted, and nothing has measured it. `BL-434` already records that per-viewport cockpit interior
cost at four players is unprofiled and unjudged, along with the per-pilot `cockpit_engine_sound`
swap against splitscreen's mix. `--perf` logs the frame-cost and draw-count split every 60 frames,
and `RunTests.ps1`'s hitch stage is opt-in via `-Hitch`.

**Approach.** Measure, do not fix. Take `--perf` and `-Hitch` readings on the heaviest shipped
campaign mission at one and four players, with and without cockpit view, and write the numbers into
`analysis/`. <TODO: "the heaviest shipped campaign mission" is not identified. Choose it by
draw-count and roster size rather than by impression, and record how it was chosen.> The readings
go in as an instrument whose result docs can cite. If the readings are bad, the outcome of
this item is a new `backlog.md` entry with the measurements in it, not an optimisation inside this
plan.

**Model recommendation.** medium. Measurement and reporting, with the judgement about what to do
next deliberately deferred.

**Verify.** The readings exist in `analysis/` with the exact command lines that produced them, and
`docs/verification.md` is consulted first for how these instruments mislead. An unchanged number is
not evidence unless it has been seen able to fail, so take a 1P baseline on the same mission.

**⚠ Traps.** `--mute` is a load-time switch and a muted run is blind to audio work, so it changes
what a perf reading means; state which switch each reading used. A busy workstation moves these
numbers, and the `over budget` marker in `RunTests.ps1` is awareness only and never changes the
exit code.

## D34 ☐ The five terms in `CONTEXT.md`

**Goal.** The three different things this plan calls "the player" have three different names, fixed
in the glossary.

**Evidence (confidence: traced).** `CONTEXT.md` is the glossary and nothing else; `docs/adr/` does
not exist and is not used (`docs/agents/domain.md`). The existing **Versus band** entry describes
where the extra humans of a splitscreen `--vs` session sit, kept clear of the ids world data
authors. **Flight roster**'s entry already leans on the phrase "the human field" without fixing it
as a term.

**Approach.** Add five entries, each with its `_Avoid_` line: **scripted player** (the one aircraft
an authored `player` token resolves to, always P1), **human field** (every human aircraft in the
session, which an objective condition reads), **guest** (a human pilot who seats no profile),
**seated profile** (the one campaign profile a session reads and writes), **episode owner** (the
human whose trigger started a cutscene episode). Add one clause to **Versus band** saying a co-op
campaign does not use it, because every human sits on team id `1`.

**Model recommendation.** medium, low effort. The wording is settled; this is transcription.

**Verify.** The five terms are used as written in the code comments and commit messages of every
other item in this plan, and no item's prose uses a word its `_Avoid_` line forbids.

**⚠ Traps.** `CONTEXT.md` holds no implementation detail; the mechanism for each term lives in
`docs/architecture.md` and in this plan, not there.
