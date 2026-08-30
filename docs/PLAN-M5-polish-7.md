# Milestone 5 polish, run 7: the campaign plays clean

**ACTIVE PLAN** (written 2026-08-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This run takes ten open items that a player meets while flying the delivered campaign: the
animation prerequisite the parse drops, the two cutscene handoffs that lose the aeroplane or the
ground under it, CM10's attack balloons and their marker, the campaign's own hangar screen, and the
three measured frame stalls. The selection criterion is "defects a campaign flight hits, plus the
three stalls that hitch it" (Decision 1): open `[Bug]`, `[Feature]` and `[Perf]` items that are
unblocked, are not `[Owed-playtest]`, and whose deliverable is a code change rather than an answer.
Out of scope by that rule: every `[Research]` item (`BL-515`, `BL-558`, `BL-637`), every
`[Owed-playtest]` and `[Tuning]` item, everything `[Blocked: ...]` (`BL-639`'s `CAP-47`,
`BL-314`'s `PT-45`), the plan-sized or user-deferred items (`BL-150`, `BL-446`, `BL-463`,
`BL-256`, `BL-299`), and the AI mode machine (`BL-523`, `BL-550`, `BL-565`, `BL-566`), which is one
subject and deserves its own run rather than a slot here.

**Every item was re-verified still-open against the record in this session**, by `git log --grep`
on each id: every hit is a filing, a minting, a trace or a cross-reference, and none is a landing.
That check also found `BL-621` (CM07's parachutist) already fixed and merged in `e6cc93b0`
while its entry still stands in `backlog.md`; it is excluded here and the stale entry is A1's
housekeeping. Two items were additionally checked against the code and say so in their Evidence
(A1, B11). The other eight carry a `<TODO: re-verify still-open against the code>`.

**Decode stays open per item.** Each item's Approach names the data file, the decoded rule or the
instrument that settles it where one is known, and an implementer may take that lane instead of the
code-side lead whenever the lead runs out. A decoded rule beats a plausible fix here, as in every
prior run.

## Milestone goal

- A definition's authored `ACTIVATION_PREREQUISITE` decides whether its call answers, in both parse
  paths, so a docking objective completes on the docking that earned it and a hook swings once.
- A cutscene hands flight back the way it took it: the flown aeroplane is drawn from the episode's
  external camera, the cockpit the pilot chose is back on the handoff, and the aircraft is above
  the terrain rather than under it.
- CM10's attack balloons come apart when they are shot, and the objective marker sits on the
  balloon it names rather than on the water below it.
- The campaign's Plane Construction screen shows the profile's own aircraft and offers buying and
  selling, not Instant Action's build list and its verbs.
- The campaign's three measured stalls are attributed and answered: CM18's two spawn stalls, CM11's
  physics tick spikes, and the sustained rate drop CM09 and CM13 share.

**No item here judges a constant at the controls.** Every `[Tuning]` and `[Owed-playtest]` item is
deliberately out; those need the user flying rather than an implementer, and they are consolidated
in [`playtest.md`](../playtest.md). Where a fix wants a confirming flight (A2, B11, B12, C21, C22),
the code side lands first behind a headless assertion and the flight confirms it afterwards.

## Decisions (2026-08-30)

| # | Question | Decision |
|---|---|---|
| 1 | Which selection criterion | **Defects a campaign flight hits, plus the three stalls that hitch it.** M5 delivered the campaign, so its polish run is the walk itself: what a player sees go wrong, and where the frame stops. Seven slots to visible defects, three to the measured stalls. |
| 2 | Items needing a human at the controls | **Excluded.** All nineteen `[Owed-playtest]` items are out, so every item here can be finished and verified by an implementer without stopping on the user. |
| 3 | Items whose deliverable is an answer | **Excluded.** No `[Research]` item takes a slot. A polish run ends with the milestone visibly better, not with new documents. |
| 4 | Weight | **Routine, with the decode lane kept open on every item.** No up-front data survey; each item names what in the mission data, the decoded record or the instrument settles it. |
| 5 | Size and shape | **Ten items in five waves.** One wave per subject, so a wave can be taken whole by one agent, with the file contention between waves A and E written down rather than discovered. |

## ⚠ Read this before implementing anything

Two readings in this plan's neighbourhood are already dead. Do not re-derive them.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | CM11's physics step costs about 39 ms per frame and its sim runs at half wall time | Godot's `physics_ms` monitor holds the WORST tick of the last wall second and refreshes at about 1 Hz, so it was read as a per-frame cost. The bracketed instrument puts the mean tick at 1.81 ms and the sim/wall ratio at 0.9999 over 306 wall seconds (`docs/verification.md` PERF-21, and `git log --grep=BL-562`). E42 is about the spikes alone. |
| 2 | Cutting ordinary allocation shrinks the settled GC pause | Halving the allocation rate moved the collection from gen0 every 13 s at 10 to 13 ms to gen1 every 31 to 36 s at 25 to 27 ms, leaving the pause per wall second unchanged at about 0.8 ms (`git log --grep=BL-536`). Relevant to E42 and E43 if either lands on the allocator: judge on pause per second, never on per-collection pause. |

The evidence quality is uneven, which is the price of picking across the campaign rather than down
one system. Budget by the grade, not by the item's apparent size.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code or data** | A1, B11, C21, D31, E41 | Confirm the trace, then implement. |
| **Direction sound, mechanism a candidate** | A2, C22, E42 | The symptom is measured and a candidate is named. Ruling the candidate out is a result. |
| **Leads only, no mechanism yet** | B12, E43 | Budget for investigation; either may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy.

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

### Wave A — the dropped animation prerequisite

1. ☐ `BL-525` The node-active `ACTIVATION_PREREQUISITE` shape reaches both parse paths and gates the call
2. ☐ `BL-628` A campaign docking swings its hook once, not three times

### Wave B — the cutscene handoff

11. ☐ `BL-625` A cutscene entered from the cockpit frames the aeroplane, and gives the cockpit back
12. ☐ `BL-633` CM15's handoff leaves the player above the terrain, not under it

### Wave C — CM10's attack balloons

21. ☐ `BL-629` A shot attack balloon bursts and falls instead of hanging in the air
22. ☐ `BL-636` The Destroy Attack Balloon marker sits on its balloon, at any altitude

### Wave D — the campaign's own hangar

31. ☐ `BL-634` Plane Construction lists the profile's aircraft and offers buy and sell

### Wave E — the campaign's frame stalls

41. ☐ `BL-641` CM18's two spawn stalls are spread across frames
42. ☐ `BL-562` CM11's physics tick spikes are attributed and removed
43. ☐ `BL-612` CM09 and CM13 hold their rate for the whole mission

## Dependency and parallelism notes

A1 comes first in its wave and A2 depends on it: A2's most likely cause is the shape A1 parses, so
A2 begins by re-flying a docking on A1's build and is closed as fixed, or re-opened as a separate
fault, from what that shows. B11 and B12 both edit `Session/CutsceneController.cs` and run as a
chain, never in parallel worktrees. C21 and C22 are the same mission and one sortie verifies both,
but they touch different systems (the animation runtime and the marker layer) and can run in
parallel. D31 is confined to `UI/` and contends with nothing.

**File contention: A1 and E42 both edit `Mech3/AnimRuntime.cs`.** A1 adds the prerequisite predicate
at the `CALL_ANIMATION` dispatch; E42's named candidate is `NameResolver.ClearFindCache` and its
callers, which are `AnimRuntime`'s `IndexStage` / `IndexSpawnedCopy` / `IndexRebasedStage` /
`IndexPooledCopy` paths in the same file. Run one, land it, then the other. C21 may also reach
`AnimRuntime`'s `ObjectMotion` dispatch; if the diagnosis takes it there, it joins the same queue.

E41, E42 and E43 are one measuring session's worth of work and share the instrument setup
(`--det`, `--perf`, `HitchSidecar`'s attribution). Taking the wave whole is cheaper than three
separate arm-and-measure cycles, but the three fixes are independent once each is attributed.

---

# Wave A — the dropped animation prerequisite

## A1 ☐ `BL-525` The node-active `ACTIVATION_PREREQUISITE` shape reaches both parse paths and gates the call

**Goal.** A definition whose `ACTIVATION_PREREQUISITE` names a required node-active state does not
answer a `CALL_ANIMATION` until that state holds. Observably: CM06 (C1C/M01)'s second docking at the
Workers' Voyage completes on the second docking rather than on the first, and the player is not
teleported back onto the trapeze seconds after flying off.

**Evidence (confidence: traced).** `extracted/C1C/M01/zrdr/wv_tailhook.zrd.json`'s
`wv_initiate_hookup` sequence calls `wv_drop_copilot`, `wv_pickup_fassenb` and `wv_pickup_copilot`
unconditionally on every dock; only each definition's own
`REQUIRED [OBJECT_ACTIVE_LIST [[wv_tailhook, dropoff_node]]]` (respectively `pickup_node`) is
authored to keep the wrong leg from running. Both parse paths drop that shape: the reader parse
(`Mech3/AnimDefs.cs`, the `ACTIVATION_PREREQUISITE` case) reads only
`OPTIONS [MINIMUM_TO_SATISFY, ANIMATION_LIST]`, and the compiled parse (`Mech3/CompiledAnim.cs`,
`activ_prereqs`) reads only entries shaped `{"Animation": ...}`, silently dropping the
`{"Parent": ...}` / `{"Object": ...}` node-active shape. `objectives.zrd`'s OBJECTIVE15 gates on
`ANIM_STATE wv_pickup_copilot EXECUTED`, which is therefore already true when it wakes.
`ObjectiveGraph`'s own reading of `ANIM_STATE` is correct against the decode and needs no change.
The shape censuses on roughly fifty files across several chapters: zeppelin gasbag panel finishers,
`chuteman`'s drop-direction gate, `pzep_cargo_point`'s cargo stop.

**Checked in this session:** commit `4385f15e` landed the *anim-list* form of the same field (a
count over the definition's own callers, gated at the `CALL_ANIMATION` dispatch in
`Mech3/AnimRuntime.cs`, suite `anim-activation-prerequisite`). That is a different form and does not
cover this one, so the item stands. It does mean the dispatch-side gate now exists and this item is
smaller than its entry describes: the new work is the two parses plus a second predicate kind
alongside the count.

**Approach.** Parse the node-active shape on both paths into a path plus required-state list on
`AnimDefinition`: reader `REQUIRED [OBJECT_ACTIVE_LIST [[path...]]]`, compiled `Parent` plus
`Object` entry runs with the `Object` leaf's `active` field as the required state. Enforce it beside
the existing count at `AnimRuntime`'s `CALL_ANIMATION` dispatch, with the same silence when unmet.
Follow `4385f15e`'s two properties where they carry over: an explicit `Play` stays ungated, and the
gate reads the state at dispatch rather than at parse. `ZeppelinRuntime.cs` is today's only consumer
of the parsed `PrereqAnims` / `PrereqMinToSatisfy` fields and must keep working unchanged.
Housekeeping in the same commit: delete `BL-621`'s stale entry from `backlog.md`, since `e6cc93b0`
landed that fix and the entry was never struck.

**Model recommendation.** high. Two parsers, a generic runtime gate, and a blast radius of roughly
fifty definitions across several chapters; a wrong predicate here silently stops authored animation
everywhere rather than in one mission.

**Verify.** Extend `anim-activation-prerequisite` with the node-active form over C1C/M01: on a first
docking `wv_drop_copilot` runs and `wv_pickup_copilot` does not reach `EXECUTED`; on a second, with
`pickup_node` active, it does. Seen red before green, with the new predicate removed.
Then `landings-docking-hold`, `campaign-objectives`, `campaign-cutscene` and `campaign-zeppelins`
(the count form's consumer) green, and the complete `.\RunTests.ps1` before landing. Census the
node-active shape across every `mis_anim` and `zrdr` archive and report how many definitions the
change newly gates, so the blast radius is a number rather than an estimate.

**⚠ Traps.** Do not patch this at the docking. The gap is `AnimRuntime` / `CompiledAnim`'s and its
reach is the reason it is its own item rather than a mission-local fix. Do not change
`ObjectiveGraph.ScanForCompletion`, whose `ANIM_STATE` reading is correct. Keep the unmet-prerequisite
skip silent, matching the count gate, so a mission log is not flooded by the roughly fifty
definitions this newly gates. `<TODO: confirm the compiled path's `active` leaf is the required
state rather than the observed one, by reading one compiled and one reader copy of the same
definition side by side before writing the predicate.>`

## A2 ☐ `BL-628` A campaign docking swings its hook once, not three times

**Goal.** The opening camera shot of a docking cutscene shows one hook swing. The hook still engages
correctly, which it does today.

**Evidence (confidence: direction-sound).** Reported at the controls and re-confirmed on the merged
build in CM11's closing autodock. Not airframe-specific: the Devastator and the Bloodhawk both show
it, and it has been there since CM01, so every docking in the campaign is affected. The hook engages
correctly, so this is the animation running repeatedly rather than the dock failing. The candidate
mechanism is A1's: `wv_initiate_hookup` calls its legs unconditionally and only the prerequisite
keeps the wrong one from running. Three plays from one call site is a different fault from three
call sites, and nothing has yet established which this is.
`<TODO: re-verify still-open against the code>`

**Approach.** Take this item only after A1 has landed, and start by re-flying a campaign docking on
A1's build. If the triple swing is gone, close the item as fixed by A1 and record that in the
closing commit. If it survives, read the hookup definition's own call graph and count the call sites
that reach the hook leg, from the mission log, before any code moves; the log distinguishes one site
playing three times from three sites playing once.

**Model recommendation.** medium. The first half is verification, and the second half only opens up
if A1 did not cover it; the harder judgement already sits in A1.

**Verify.** `landings-docking-hold` and `campaign-cutscene` green. At the controls, any campaign
docking watched through its opening shot alone: one hook swing, and the hook still engages.
`<TODO: name the headless assertion that counts hook-leg plays in one episode, or record that the
count is only observable at the controls.>`

**⚠ Traps.** Do not silence this by latching "already played" on the runtime. A repeat that a
definition authors is data the parse is dropping, and a latch would hide the same defect wherever
else those prerequisites are ignored. The player-visible hook engagement is correct today and must
stay correct.

---

# Wave B — the cutscene handoff

## B11 ☐ `BL-625` A cutscene entered from the cockpit frames the aeroplane, and gives the cockpit back

**Goal.** A player in the cockpit view when a cutscene fires sees the flown aeroplane drawn from the
episode's external camera, with no cockpit panel over it, and is back in the cockpit when flight
returns. Both the normal end and the skip path behave the same way.

**Evidence (confidence: traced).** `ApplyPresentation` sets `CameraOwned = true`, which silences the
whole per-frame camera arm in `Flight/FlightController.cs` and with it the `Cockpit?.Apply` that arm
re-asserts every frame. Whatever visibility the last flying frame left standing therefore holds for
the episode's whole length, and in `PilotViewMode.Cockpit` that state is
`Shown(Interior: true, Body: false, ...)` (`Flight/CockpitVisibility.cs:35-38`): the airframe undrawn,
the interior still drawn by its own overlay pass in front of the cutscene camera. The episodes this
hits are the ones that stage the flown aircraft at the `player` marker (`StagePlayerAircraft`), which
is to say the ones that mean the aeroplane to be seen.

**Checked in this session:** the entry's line numbers are stale, because the co-op merge moved these
members. In today's `Session/CutsceneController.cs`, `ApplyPresentation` is declared at `:769` and
called at `:658` (present), `:683` (restore) and `:749`; `StagePlayerAircraft` is declared at `:845`
and called at `:447`, `:599` and `:803`. `LeaveFirstPerson` appears nowhere in the file, so the
mechanism is intact. Re-locate before editing rather than trusting either set of numbers.

**Approach.** The crash camera already solves the identical problem for the identical reason:
`CutToCrashView` calls `LeaveFirstPerson()` (`Flight/FlightController.cs:2447-2465`) because an
external vantage has to un-hide the body and `Deactivate()` the interior pass. `ApplyPresentation(true)`
wants that same call. `ApplyPresentation(false)` (the restore and skip paths) wants the inverse,
re-asserting the rules for the `ViewMode` the pilot actually had, so a player who entered from the
cockpit is back in it after the handoff. The crash path needs no such restore because the respawn
rebuilds the view, and the handoff does not.

**Model recommendation.** medium. The mechanism, the call to reuse and the two call sites are all
named; the judgement left is the restore's exact shape and the interaction with the skip path.

**Verify.** `campaign-cutscene`, `campaign-cutscene-skip` and `campaign-cutscene-ownership` green,
extended with an assertion that during a presented episode entered from `PilotViewMode.Cockpit` the
airframe is visible and the interior pass is inactive, and that both invert on the restore. Seen red
before green. At the controls the reported repro is CM01 (C3/M01)'s docking episode:
`.\RunGame.ps1 --campaign=<profile>:0`, cycle to the cockpit view (F8) before the docking fires, and
watch it through, then repeat with the skip key, which takes the other restore path.

**⚠ Traps.** Do not fix this by writing `ViewMode = Chase` for the duration. `MirrorCamera` owns every
rig camera's pose outright while presenting, so the view mode buys nothing there, and the mode left
behind is the state the handoff restores, quietly moving the player out of the cockpit they chose.
Only the visibility rules and the interior pass need to move. The interior lives outside `PlaneModel`
under `CockpitPass`, so un-hiding the airframe alone does not take it off screen. An intro cutscene
drawing no aircraft is a different thing: those definitions deactivate the `player` node themselves
in `ApplyOutOfFlight`.

## B12 ☐ `BL-633` CM15's handoff leaves the player above the terrain, not under it

**Goal.** When CM15 (C2/M05)'s cutscene hands control back, the aircraft is at the pose the episode
ended on and above the ground, and the player can fly on without dying.

**Evidence (confidence: lead-only).** Reported at the controls. The handoff leaves the aircraft below
the ground surface rather than at the pose the episode ended on, and since terrain colliders are
single-sided a crossing from below is silent, so dying is the only way out. No mechanism is
established. The nearest known shape is the marker-relative position that survived an episode's own
reparenting, fixed for a different episode in `BL-610` (`git log --grep=BL-610`), so the first
question is whether this episode takes that path. `<TODO: re-verify still-open against the code>`

**Approach.** Log the pose the episode restores against the pose it ended on, over CM15's episode,
before changing anything. Read `Session/CutsceneController.cs`'s restore path (`ApplyPresentation(false)`
and the reparent undo around `_cameraHome`) against `BL-610`'s fix and establish whether the same
reparenting is in play. Take B11 first: it edits the same restore path, and landing them in one file
one after the other avoids a merge against yourself.

**Model recommendation.** high. The diagnosis is open, the instrument has to be chosen, and the
restore path is shared with B11 and with every other episode in the campaign.

**Verify.** A headless assertion over CM15's episode that the restored pose matches the pose the
episode ended on, and that the aircraft's Y is above the terrain height at that XZ. Seen red before
green. `campaign-cutscene-ownership` and `campaign-cutscene` green. At the controls, CM15 flown
through the episode, then flown on.

**⚠ Traps.** Do not add a ground clamp to the handoff. It would hide a wrong restore everywhere else
it happens, and it would fight an episode that legitimately ends below a surface. The single-sided
colliders that make the symptom unrecoverable are a separate property, recorded from the air side in
`BL-566`, and are not this item's to change.

---

# Wave C — CM10's attack balloons

## C21 ☐ `BL-629` A shot attack balloon bursts and falls instead of hanging in the air

**Goal.** A shot-down attack balloon in CM10 (C1/M05) comes apart and falls, as its definition
authors, rather than hanging in the air as a destroyed model.

**Evidence (confidence: traced).** Reported at the controls. The parts of the chain that work are the
weapon hit, the model swap to `destroyed_balloon`, the death of the balloon's `bbtur` turret and the
lifeboat's drop, which falls, splashes and drives its net. The authored death is more than a swap:
`extracted/C1/M05/mis_anim/lifesaver11-lifefall11-lifeballoon.json` (`activation: WeaponHit`,
`health: 60.0`) carries eight `ObjectMotion` events, of which only the first drives `lifeboat` down
under `gravity: -10`; the rest move `b_dbase` and `b_part1` through `b_part6`, the balloon's own
bursting pieces, alongside seven `ObjectOpacityFromTo` fades and fourteen puffer states. The
balloon-side half of one definition is not taking effect while the boat-side half is. Nine balloons
author the same five definitions, so the answer applies nine times.
`<TODO: re-verify still-open against the code>`

**Approach.** Run that definition headless in the anim lab on the real C1/M05 world and report which
of its eight motions start, then follow the first one that does not. Establish whether the balloon
pieces resolve as nodes at all before asking why they do not move; a name that resolves to nothing is
the cheapest of the candidate answers and `BL-618` records that shape.
`<TODO: name the anim-lab invocation for a single mis_anim definition on a built world, from
docs/verification.md, so the measurement is reproducible.>`

**Model recommendation.** high. The diagnosis runs through the animation runtime's dispatch and the
name resolver, and the fix has to hold for all nine balloons and five definitions rather than for the
one that was watched.

**Verify.** A new suite over C1/M05 that kills one balloon and asserts the `b_part1` to `b_part6`
pieces move and fade while the lifeboat still falls and splashes, seen red before green with the fix
reverted. `campaign-objective-markers` and `campaign-objectives` green (the mission's own progress
must not move), and the complete `.\RunTests.ps1` before landing. At the controls, CM10: shoot one
balloon and watch what is left in the air. `<TODO: name the new suite.>`

**⚠ Traps.** Do not delete the balloon on death as a shortcut. The pieces are authored to fall and
fade, and a despawn would remove the wreck the original shows falling. `set_bbtur_off` and the model
swap are already doing their jobs, so neither is the suspect. If the fix reaches `AnimRuntime`, check
the file-contention queue in the dependency notes first.

## C22 ☐ `BL-636` The Destroy Attack Balloon marker sits on its balloon, at any altitude

**Goal.** CM10's Destroy Attack Balloon marker tracks the balloon it names, at whatever altitude the
balloon is at, and retires with the balloon rather than following the lifeboat that drops out of it.

**Evidence (confidence: direction-sound).** Reported at the controls, and unchanged on the merged
build after the mission's balloon and lifeboat behaviour was fixed. The marker tracks the right
balloon horizontally but hangs at the sea surface below it, which reads as a marker on the boat. Each
`lifesaverNM` group holds the balloon, its `bbtur` turret, the ropes and the lifeboat together, so a
marker anchored on the group rather than on the balloon node lands exactly there. That is a candidate
read from the data, not a confirmed trace. `<TODO: re-verify still-open against the code>`

**Approach.** Read which node the marker anchors on for these targets, in `Mech3/RosterMarkers.cs` and
`UI/MarkerOverlay.cs`, and move it to the balloon. `BL-602`'s closing commit settled the neighbouring
case, a group site anchoring on its built meshes rather than on its own origin
(`git log --grep=BL-602`), and is the first thing to read.

**Model recommendation.** medium. The anchoring layer is small, the precedent is named, and the
judgement is which node is the right anchor rather than how to move it.

**Verify.** `campaign-objective-markers`, `campaign-objective-target-path` and `hostile-marker-hud`
green, extended with an assertion that the marker's world position tracks the balloon node at two
different balloon altitudes and retires when the balloon dies. Seen red before green. At the
controls, CM10 with a wave in frame at two different heights; the same sortie confirms C21.

**⚠ Traps.** Do not offset the marker upward by a constant. The balloons descend as they attack, so a
fixed lift is right at one altitude and wrong at every other. The marker must also retire with the
balloon rather than follow the boat.

---

# Wave D — the campaign's own hangar

## D31 ☐ `BL-634` Plane Construction lists the profile's aircraft and offers buy and sell

**Goal.** A campaign profile's Plane Construction screen lists that profile's own aircraft and offers
buying and selling against its wallet. A fresh profile shows exactly its two starters, a purchase adds
one and takes the money, a sale removes it and returns the full build cost, and Instant Action's list
is unaffected by all three.

**Evidence (confidence: traced).** Reported at the controls. The cabin opens the hangar over the
global build store, `CustomPlaneStore.UserPlanes()` (`UI/LaunchMenu.cs:1866-1869`), and the flow's
first screen lists it wholesale (`Saved = store.List()`, `UI/HangarFlow.cs:150`), which is the same
`user://Planes/` directory the Instant Action Build button writes into. The profile's ownership list
is not consulted anywhere in that screen, though it exists and is already wired for money:
`HangarCampaignContext` carries `Purchase`, `Sell`, `CanSell` and `SellPrice`
(`UI/HangarCampaignContext.cs:98-122`) with the decoded rules, a sell price equal to the full build
cost, reward aircraft unsellable, and a floor of two aircraft ([`docs/org/hangar.md`](org/hangar.md),
"The sell price is the full build cost"). The rows are Instant Action's as well, a New Plane row and
a delete, where the campaign's are buy and sell. `<TODO: re-verify still-open against the code>`

**Approach.** Filter the campaign flow's plane list through `Profile.Planes` and route its rows to
`Purchase` and `Sell`. Ownership is what separates the two modes, not storage: `Purchase` already
names a build into the profile, so the builds themselves can stay in the one `user://Planes/` store.

**Model recommendation.** high. It separates two modes over one store, spends the campaign wallet,
and gets the decoded sell rules wrong in a way a profile carries forward if it is careless.

**Verify.** Unit tests over the flow and the context: a fresh profile lists exactly its two starters;
a purchase adds the build to `Profile.Planes` and debits the wallet; a sale removes it and credits the
full build cost; a reward aircraft cannot be sold; the two-aircraft floor holds; and an Instant Action
build written to the same store does not appear in the campaign list. `hangar` and `hangar-door-wake`
green, plus `campaign-persistence`, and the complete `.\RunTests.ps1` before landing.

**⚠ Traps.** Do not give the campaign its own build directory to get the separation. The two starters
a fresh profile is seeded with are never hangar-built and have no entry there at all, which is why
`SellPrice` falls back to the campaign's own Devastator spec; a per-mode store would strand them. The
delete row is not the sell row: deleting a build must not credit the wallet.

---

# Wave E — the campaign's frame stalls

## E41 ☐ `BL-641` CM18's two spawn stalls are spread across frames

**Goal.** CM18 (C4/M03) runs its early flight without the two roughly 300 ms frame stalls, at one
human and at four.

**Evidence (confidence: traced).** `analysis/campaign-coop-4p-perf/FINDINGS.md` (D33): `--det`'s
deterministic clock put the stall at sim frame 241 (about 320 ms against a 45.7 ms threshold) and
frame 482 (about 290 to 300 ms) in every one of the four 1P/4P by cockpit/external launches measured.
`HitchSidecar`'s own attribution assigned essentially the whole frame to one `ai_spawn` sample
(`attributed_ms` within 20 ms of `frame_ms` both times). Identical across player counts, so this is a
single mission-script or generator spawn cost on the main thread, not something splitscreen's pane
count multiplies. `<TODO: re-verify still-open against the code>`

**Approach.** Find what CM18's roster or generator script does at those two sim instants (a
`WAKEUP_GENERATOR`, a roster credit or a similar bulk spawn) and establish whether the work can be
spread across frames. D33 measured it and deliberately did not fix it, so the measurement is the
starting point rather than work to redo.

**Model recommendation.** high. Spreading a spawn across frames changes when entities exist, which
mission scripts and the objective graph both observe; the correctness risk is larger than the
performance work.

**Verify.** Re-run the D33 measurement under `--det` at 1P and 4P and show both frames under the
45.7 ms threshold, with the attribution no longer naming `ai_spawn`. `campaign-squad-wakeup`,
`campaign-roster` and `campaign-objectives` green, and the complete `.\RunTests.ps1` before landing.
`docs/verification.md` PERF-12 and PERF-13 apply.

**⚠ Traps.** The stall is far past `HitchMonitor`'s grace window and reproduces to the same frame under
`--det`, so it is not workstation noise. Spreading the spawn must not change the sim frame on which
the entities become observable to the objective graph, or the mission's own pacing moves with it.

## E42 ☐ `BL-562` CM11's physics tick spikes are attributed and removed

**Goal.** CM11 (C2/M02) runs no single physics tick long enough to exhaust Godot's eight-step catch-up
cap, so no sim step is discarded.

**Evidence (confidence: direction-sound).** The bracketed instrument (`Utils/PhysicsTickCost.cs`, and
`--perf`'s `phys_tick_ms` / `phys_tick_max_ms` / `phys_hz`) over 333 windows of a loaded CM11 session
puts the mean tick at 1.81 ms (p95 3.20 ms) and the tick rate at a median of 60.0 per wall second, but
`phys_tick_max_ms` hit 72, 102 and 177 ms on individual ticks. `max_physics_steps_per_frame` is at
Godot's default 8, so a 177 ms stall leaves about six sim steps undeliverable and they are discarded,
not deferred. The named candidate is `Mech3/Anim/NameResolver.cs`'s `ClearFindCache`, which drops the
whole find memo, so the next tick's roughly sixty objective name resolutions re-walk the roughly
5000-row index calling `GodotObject.IsInstanceValid` on every row; its callers are the `IndexStage` /
`IndexSpawnedCopy` / `IndexRebasedStage` / `IndexPooledCopy` spawn and warm-up paths in
`Mech3/AnimRuntime.cs`, which is the right shape for a spike that lands on a spawn rather than
steadily. `<TODO: re-verify still-open against the code>`

**Approach.** Confirm the candidate by bracketing a spiking tick rather than by inference: instrument
what a spiking tick is doing before changing the memo. If it is the find cache, the fix is an
invalidation narrower than dropping the whole memo. Check the file-contention queue: A1 edits the same
file.

**Model recommendation.** high. The instruments here have already misled once on this exact item, and
a cache invalidation that is too narrow returns a stale node rather than a slow frame.

**Verify.** Re-run the bracketed instrument over a loaded CM11 session and show `phys_tick_max_ms`
under the 16.7 ms budget across the same window count, with the mean unchanged. Compare durations in
sim seconds, never wall seconds. The complete `.\RunTests.ps1` before landing, since a memo change
reaches every name resolution in the game.

**⚠ Traps.** **Do not chase the sustained step**; see the disproven-claims table above. Do not raise
`max_physics_steps_per_frame`: it deepens the catch-up spiral rather than recovering the lost steps.
The original measurement ran under `--debug-objective=18` with nobody at the controls, so it
under-weights projectiles and destruction cascades; a re-measurement should say which mode it used.

## E43 ☐ `BL-612` CM09 and CM13 hold their rate for the whole mission

**Goal.** CM09 (C1/M04) does not fall under 60 for the rest of the mission once the Promised Land is
down and the next fighter squad spawns, and CM13 (C2/M03), which shows the same sustained drop, holds
its rate too.

**Evidence (confidence: lead-only).** Reported at the controls on the plan branch, and it is a
sustained rate drop rather than single hitches. The sortie log's late `[perf] hitch` lines carry the
shape in their baseline: 19 to 26 ms against 10 ms earlier in the same sortie, with `script_ms` about
25 and `physics_ms` 17 to 18 while `render_cpu_ms` stays near 1 and `gpu_ms` near 0.5, `nodes` about
37000 and `mem_mb` about 1300. The frame is spent on the CPU in script and physics, not on the GPU. No
culprit is named: `attributed_ms` is 0 in those lines because the sampler was not armed.
`<TODO: re-verify still-open against the code>`

**Approach.** The hitch detector reports outliers against a rolling baseline, so a whole-run slowdown
reads only in that baseline. Measure the rate itself instead (a frame-time trace over the sortie, or
the probe's own timing), then attribute the script time with the sampler armed. **Measure CM13 first:**
that mission spawns its whole field at the start and involves no zeppelin death, so if both missions
share a cause it is the number of live aircraft rather than the destruction choreography. Then read
CM09's second variable: whether the cost is the zeppelin's death choreography left running (the burn,
finisher and sink anims and their emitters persist after the hull is down), the destroyed hull's
colliders and debris still stepping, or the new squad's rigs adding AI and physics on top. The node
count says nothing was freed. Compare a CM09 run that leaves the zeppelin alive.

**Model recommendation.** high. The item is a measurement before it is a fix, the instrument has to be
chosen against a detector that answers a different question, and the outcome may be a disproof.

**Verify.** A frame-time trace over a full CM13 sortie and a full CM09 sortie showing the rate held
across the second half, with the attribution naming what changed. Take the baseline first: an
unchanged number is not evidence unless you have seen it able to fail. `docs/verification.md` PERF-12,
PERF-13 and PERF-19 apply. The complete `.\RunTests.ps1` before landing.

**⚠ Traps.** The hitch detector is opt-in (`.\RunTests.ps1 -Hitch`) and answers a different question
than this one; its lines name no culprit here because the sampler was not armed. Do not treat CM09's
zeppelin as the first variable: CM13 is the cleaner test case and comes first. If the answer turns out
to be the settled GC pause, read the second disproven claim above before optimising allocation.
