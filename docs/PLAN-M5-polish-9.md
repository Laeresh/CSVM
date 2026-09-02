# M5 Polish Run 9

**ACTIVE PLAN** (written 2026-09-02). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names the active plans when more than one is
present. Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to
[`plans.md`](plans/plans.md), when every item lands.

Twelve campaign defects walked in mission order, CM02 to CM15, selected from `backlog.md` on
2026-09-02 by the criteria the author chose that day: the per-mission `CMnn` campaign defects,
walked in mission order the way runs 4 and 5 were, over the run-8 default of theme-spread bugs.
Excluded on the same day: everything `[Blocked]` (`BL-639` on `CAP-47`), `[Owed-playtest]`
(`BL-545`), the `[Research]` questions that decide gameplay rather than fix a defect (`BL-515`,
`BL-637`, `BL-652`, `BL-657`), the two `[Perf]` items whose remaining legs are measurement rather
than a mission symptom (`BL-562`, `BL-606`), the `[Feature]` items, `BL-603` (whose first step is
flying the original), and anything belonging to Milestone 6 (multiplayer). Three borderline items
were put to the author and taken: `BL-566`, which run 8 had left in the AI-mode-machine cluster
but which this run's verification reads as a terrain-crossing question; `BL-524`, whose mechanism
is traced and whose remainder is a design decision; and `BL-665`, traced and small but latent.

**Each of the twelve was re-verified still-open on 2026-09-02** against the record
(`git log --oneline --all --grep=BL-nnn`: filings and plan listings only, no landing), the current
`backlog.md` entry, the worktree and branch list, and the cited code, by three read-only passes over
`CSVM/src`; where a cite had drifted the corrected `file:line` is in the item's Evidence below, and
two of the entries' own hypotheses died in that pass (see the Traps of C22 and C23). The scheduled
entries were moved out of `backlog.md` into this plan in the same change that created it.

This plan runs beside [`PLAN-menu-presentations.md`](PLAN-menu-presentations.md) (Wave A) and
touches nothing that plan owns: no `LaunchMenu`, no menu screens, no campaign board chrome. It
also runs beside [`PLAN-M5-polish-8.md`](PLAN-M5-polish-8.md)'s open `D31` sortie; D31's checks
(`PT-97` to `PT-106`) and this run's `D31` can be flown in one sitting, since both walk the
campaign in mission order. The `BL-523`/`BL-550`/`BL-565` AI-mode-machine remainder stays in the
backlog whole, on the grounds three prior runs recorded. First alternates if an item here dies
early: `BL-515` (CM04), `BL-562` leg (b) (CM11), `BL-657` (CM18).

## Milestone goal

- The Hawaii chapter's docking hook swings its authored travel, and a wingman whose leader is
  lost does something decided rather than holding a bearing to a dead enemy.
- The Northwest chapter's Pandora stops over the tanker and plays its sequence there, a
  gasbag-only zeppelin kill ends CM09 one way or the other, a woken roster block appears where the
  script left it, and CM10's balloon marker never rests on the water.
- The Hollywood chapter's stunt planes carry their authored marker, the Spruce Goose flies
  smoothly, CM12's ace stays above the terrain, the Pandora's landing cones draw on CM13, a stowed
  broadside cannon cannot be hit, and CM15's capture cutscene shows its Balmoral.
- A closing sortie judges at the controls, in mission order, every landed item whose acceptance
  needs eyes.

**No menu work, no AI-mode-machine work, and no Milestone 6 work.** Everything hosted by
`LaunchMenu` belongs to `PLAN-menu-presentations`; the patrol/pursue/lay-off cycle stays whole for
a dedicated run; networking does not exist yet and nothing here prepares for it.

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

### Wave A — Hawaii (C3)

1. ☐ `BL-630` CM02: the docking hook's two side parts swing their authored travel on the Balmoral's auto-land
2. ☐ `BL-524` CM05/CM07: a wingman whose leader leaves play stops holding a bearing to a dead enemy

### Wave B — Northwest (C1)

11. ☐ `BL-597` CM08: the Pandora halts over the tanker and its sequence there plays
12. ☐ `BL-666` CM09: a zeppelin killed by gasbags alone ends the mission one way or the other
13. ☐ `BL-665` A woken roster block is re-placed where the script left it, not at its authored pose
14. ☐ `BL-656` CM10: an attack balloon's marker never rests on the water before the wave arrives

### Wave C — Hollywood (C2)

21. ☐ `BL-635` CM11: the stunt planes carry the objective marker their roster blocks author
22. ☐ `BL-627` CM12: the Spruce Goose moves smoothly along its scripted legs
23. ☐ `BL-566` CM12: the ace `hkfirebrand_9` stays above the terrain after its wake
24. ☐ `BL-618` CM13: a compiled anim addressing a `~n` dedup name resolves to the right sibling
25. ☑ `BL-640` CM14: a broadside cannon stowed behind its hatch takes no weapon damage
26. ☐ `BL-632` CM15: the capture cutscene frames its Balmoral

### Wave D — Closing sortie

31. ☐ At-the-controls pass in mission order over every landed item that owes a judgement

## Dependency and parallelism notes

Waves group by chapter in mission order, not by dependency; every code item is independently
landable, and D31 runs last. Contention rules for parallel worktrees:

- **A1, C22 and C24 all reach the animation runtime family** (`PoseChannel.cs`, `FromToMotion.cs`,
  `ScriptPlayback.cs`, `AnimRuntime.RestOf`, `NameResolver.cs`). C24 is `NameResolver.cs` alone and
  can run beside the other two; A1 and C22 may both edit `PoseChannel.cs`, so run them
  sequentially or in one worktree.
- **B11 and B12 both edit `ZeppelinRuntime.cs`.** Run them sequentially.
- **B13 and C21 both edit `CampaignDirector.cs`; B14 and C21 both edit `ObjectiveSites.cs`.** Run
  C21 after B13 and B14 have landed, or all three in one worktree.
- **C25 may edit `AnimRuntime.DamageNodeOf` while C22 may edit `AnimRuntime.RestOf`.** Different
  members of one file; merge with care or run sequentially.
- A2 (`AiPilot.cs`) and C23 (`AiModeMachine.cs`, possibly `SweepCadence.cs`) are disjoint from
  each other and from everything above. C26 (`AircraftStage.cs`, `CutsceneController.cs`) is
  disjoint from everything.
- Any item that changes a global condition can move goldens: take a baseline before, and every
  re-pin is user-reviewed.
- **⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
  worktree session here; use a local commit or a file copy.

Launch commands for the missions this plan walks, `./RunGame.ps1 --campaign=<profile>:<n>`:

| Mission | Chapter | `<n>` |
|---|---|---|
| CM02 | C3/M05 | 1 |
| CM05 | C3/M04 | 4 |
| CM07 | C1/M02 | 6 |
| CM08 | C1B/M03 | 7 |
| CM09 | C1/M04 | 8 |
| CM10 | C1/M05 | 9 |
| CM11 | C2/M02 | 10 |
| CM12 | C2/M01 | 11 |
| CM13 | C2/M03 | 12 |
| CM14 | C2B/M04 | 13 |
| CM15 | C2/M05 | 14 |

---

# Wave A — Hawaii (C3)

## A1 ☐ `BL-630` CM02: the docking hook's two side parts swing their authored travel

**Goal.** On the captured Balmoral's auto-land in CM02 (C3/M05), the hook rig's two
inward-rotating side parts stop at their authored angle.

**Evidence (confidence: lead-only).** Seen at the controls, judged by eye with no original
reference open, and the sighting predates the run-5 and run-6 merges; the mission has not been
re-flown since, so the symptom is unconfirmed on the current build. The two parts are `top_seg`
and `hoop` in `extracted/C3/M05/mis_anim/piratezep-pz_deploy_hook.json` (rotate x 1.3089969 to 0
rad and -1.3089969 to 0 rad) with the counterpart `piratezep-pz_retract_hook.json`. The runtime
path is `CSVM/src/Mech3/Anim/PoseChannel.cs:140-158` (`HandleMotionFromTo`) into
`CSVM/src/Mech3/Anim/FromToMotion.cs:42-93` (`Create`/`Seek`), which applies a rotate channel once
per event in radians; nothing in that path visibly double-applies or mis-units, so the verification
pass could not confirm a defect from the code alone. The `landings-balmoral-dock` suite
(`CSVM/src/Testing/LandingApproachSuites.cs:334-346`, driver `DriveBalmoralDock`, `CheckWingFold`)
asserts only `fold` and `rwingbend`, never `top_seg` or `hoop`.

**Approach.** First re-confirm: capture the CM02 auto-land headless with `--screenshot=` over
`--shots=` frames at the docking, or add a `top_seg`/`hoop` angle read to the Balmoral dock suite's
artifact, and compare the applied angle against the authored 1.3089969 rad (75°). If the runtime
angle matches the authored one, close this as disproven on the current build with the capture as
evidence. If it overshoots, find whether the deploy and retract defs overlap on the same node
(two channels on one frame) or a degree value is read as radians in `AnimDefs.cs`.

**Model recommendation.** Medium: a bounded comparison of an applied angle against an authored
one, ending in a fix or a disproof.

**Verify.** The extended `landings-balmoral-dock` suite asserting the two parts' angles at the
authored values; a D31 look at the CM02 auto-land.

**⚠ Traps.** Do not retune the angle to taste; it is authored, and the fold beside it is already
asserted against its authored value. `docs/architecture.md`'s `AircraftStage` entry records a
different hook artifact (an aircraft's own arm left at archive-full length for one frame after
`ParkDockingHook`), and `PT-97` covers that one; this item is the `piratezep` crane hook, not the
aircraft's arm, so do not conflate the two.

## A2 ☐ `BL-524` CM05/CM07: a wingman whose leader leaves play stops holding a stale bearing

**Goal.** In CM05 (C3/M04) and CM07 (C1/M02), a friendly wingman whose leader is shot down and
whose own target then dies no longer flies out of the mission on its last pursuit bearing; it does
whatever the author decides such a wingman does.

**Evidence (confidence: traced).** `CSVM/src/Flight/AiPilot.cs:305-306` and `:350-351` dispatch to
`FlyEscort` only while `Escort is { Leader.InPlay: true }`; once the leader is out of play the
machine falls through to `FlyPursuit` while a quarry exists (`:329-336`, `:353`) and otherwise to
`FlyPatrol`. `FlyPursuit` (`:380-385`) writes `TargetHeadingDeg` and `TargetAltitude` to the quarry
every step. `FlyPatrol`'s netless arm (`:482-497`) calls `Fly(model, dt, OrderAim(model), …)` at
`:485`, and `OrderAim` (`:582-586`) projects a point from those two fields, which nothing on that arm
updates, so the pilot holds the last bearing to the dead quarry. The commit that disproved the
original missing-net cause (`git log --grep=BL-524`) established that the original has no
reachable rule for this case: `FUN_0041d1f0` indexes -1 on an unresolved net with no guard,
`FUN_0041e760` dereferences its leader with no null check, and `FUN_0049c920`, which would hand a
wingman the chapter's first net, has no callers. Both missions flown headless confirmed the roster
shape and the trigger (`devastator_1` dies in CM05, leaving `wingman_2` leaderless). No unit test
or suite covers a leaderless netless pilot: `AiPilotTests.cs` and `WingmanSuites.cs` exercise
`FlyEscort` only.

**Approach.** Write the failing unit first: drive `AiPilot.Update` with `Escort.Leader.InPlay`
false, `Patrol == null` and the gunner's target gone, and assert the two order fields are
re-derived rather than held. Then put the decision to the author with the options the data allows:
hold a level orbit at the point of loss, join the nearest surviving friendly leader as a new
escort, or take the chapter's first net (the route the original's dead code would have taken).
Implement the chosen one on the netless arm of `FlyPatrol`. The decision is recorded in this
item's Landed line and in the landing commit.

**Model recommendation.** High: the code change is small, but the behaviour is a design decision
with no decode behind it and it moves every escort in every mission.

**Verify.** The new unit green; CM05 flown headless past `devastator_1`'s death with the AI trace
on, `wingman_2`'s heading no longer constant after its target dies; D31 flies CM05 and CM07 and
watches the escorts after the first patrol is destroyed.

**⚠ Traps.** Do not give them the player's escort law as a default; `BL-457` shows the escort
hand-off is itself unsettled. Do not add a leash constant. Do not re-decode the original for a
lay-off rule; that search is done and recorded, and the answer is that the code is unreachable.
`BL-523`'s promotion gate is a different question and stays in the backlog.

# Wave B — Northwest (C1)

## B11 ☐ `BL-597` CM08: the Pandora halts over the tanker and its sequence there plays

**Goal.** In CM08 (C1B/M03) the Pandora's armed stop lands over the tanker, and the sequence the
original plays there (the hangar door opens, a figure descends on a rope and ascends again) starts.

**Evidence (confidence: lead-only).** Reported at the controls against the original; the Pandora
holds level along Klondike1 but stops short of or past the tanker, and no sequence starts. The
stop is decided by `CSVM/src/Flight/AiNetFollower.cs` (`ArrivalRadius` at `:153-157`, floored at
`MinArrivalRadiusM` 10 m at `:23`, and the along-leg dot-product test) with the Pandora's own
floor set in `CSVM/src/Session/ZeppelinRuntime.cs:69-75` (`arrival = 1.5f * turnCircle`, passed
as `minArrivalRadius`); that is what the backlog's `max(sqrt(edge[7]), 125 m)` paraphrases, and
it makes an armed hold engage 125 m or more short of the node. `docs/architecture.md`'s
`ZeppelinMotion` entry documents the stop point and the `Dock`/`Holding` states. The tanker
sequence's file and caller are not yet found: `extracted/C1B/M03/mis_anim/tanker-start_wakes.json`
and the twelve `freightNN-tankerfreightNN.json` files match by name, none is obviously the
door-and-rope sequence, and a wider search of that mission's `mis_anim` set is the first step.
`zeppelin-pandora-dead-end` (`CSVM/src/Testing/ZeppelinSuites.cs`) covers the hold.

**Approach.** Two halves. (1) Find the sequence: search `extracted/C1B/M03/mis_anim/` for defs
whose objects include a door node and a figure on a rope, then find what calls it (an objective
completion action in `objectives.zrd`, or a node-arrival call on the zeppelin's path) and log
whether the caller ever fires on a headless run. (2) The stop: compare where the original halts
(the node itself, or the arrival radius short of it) against `ZeppelinRuntime.cs:69-75`; if the
sequence's caller is a node arrival, the two halves are one bug.

**Model recommendation.** High: a search through authored data for an unnamed caller, and a
motion rule whose decode is partial.

**Verify.** `zeppelin-pandora-dead-end` extended to assert the stop position against the tanker
node and the sequence's start; D31 flies CM08 to the hold.

**⚠ Traps.** The backlog cited `git log --grep=C21` for the capture rule; that grep resolves to
several unrelated commits. The decode commit is found with `git log -S "along-leg"` or
`--grep="arrival radius"`. Do not move the arrival floor to make the stop land; it is decoded, so
if the original halts on the node the difference is in how the hold is armed, not in the radius.

## B12 ☐ `BL-666` CM09: a zeppelin killed by gasbags alone ends the mission one way or the other

**Goal.** In CM09 (C1/M04) a `piratezep` kill by gasbag count reaches either the loss chain or
the docking, whichever the original does, instead of leaving the mission ending in neither
direction.

**Evidence (confidence: traced).** `CSVM/src/Session/ZeppelinRuntime.cs:718-726` kills on
survivor count alone (`if (!zep.Dead && damage.IsDead(zep.ZoneAlive))`) and fires `PlayHullDeath`
and `ZeppelinKilled` without driving the engines to zero; engine disable is a separate branch at
`:675-684`. The 26/27/41 loss chain waits on six engines lost and never fires, while OBJECTIVE40,
gated on the hull through `TICK_DEPENDS_ON_OBJ`, retires and 42 stays gated off.
`docs/formats/objectives.md:103-118` records the gate rule: OBJECTIVE29 is `INACTIVE1 [piratezep]`,
and a gate whose own condition reads true is a chain that stops for good. The
`campaign-cm09-docking` suite (`CSVM/src/Testing/CampaignDockingSuites.cs:56`) drives the docking
chain from the tower-down route.

**Approach.** Decide from the original which path a gasbag kill takes: read the zeppelin death
handler in `crimson.exe` for whether a hull death also marks the engines lost (which would fire
the loss chain) or activates the docking condition directly; `docs/org/` holds the zeppelin damage
decode to start from. Then route the hull's death in `ZeppelinRuntime.WireDamage` through that
path. Add a `campaign-cm09-gasbag-kill` variant of the docking suite that kills by gasbags and
asserts the mission ends.

**Model recommendation.** High: which chain the death reaches is a mission outcome, and the
decode decides it.

**Verify.** The new suite variant asserting one of the two endings after a gasbag-only kill;
`campaign-cm09-docking` unchanged; D31 kills the zeppelin by gasbags in CM09 and watches the
ending.

**⚠ Traps.** Do not touch the objective graph or `ObjectiveGraph.cs`; the gate rule is the
original's mechanism and is correct. `BL-440`'s breakup runs on the same kill path; keep the
breakup playing.

## B13 ☐ `BL-665` A woken roster block is re-placed where the script left it

**Goal.** A deactivated roster block that a mission's script placed or moved before its wake
appears at that pose on the wake, not at its authored one.

**Evidence (confidence: traced).** `CSVM/src/Session/CampaignDirector.cs:339-366`
(`BuildRoster`) computes the rig's pose from a world node of the block's name when `FindNodes`
resolves one (`:341-346`) and spawns the rig there (`:360`), then stores the authored `spawn`
into `_rosterPlans[spawn.Name]` at `:366`. `World.WakeupEnemies` (`:1163-1195`) re-places a woken
block with `rig.Activate(plan.Position, plan.Position + plan.Forward)` at `:1174`, reading the
authored pose. No shipped C1/M04 block has a world node, and `campaign-squad-wakeup`'s CM02
fixtures (`britpeace_7/8/9`) show none either, so no shipped mission is known to show the symptom;
the other missions are uncensused. Neither `campaign-squad-wakeup`
(`CampaignSquadWakeSuites.cs:53`) nor `campaign-roster` (`CampaignRosterSuites.cs:80`) asserts
node-versus-plan pose equality after a wake.

**Approach.** Store the placed pose in the plan at `:366` (or re-resolve the node at the wake),
and add a unit or suite fixture where a block has a world node away from its authored pose,
asserting the wake lands on the node. Census the shipped missions once for blocks whose name
matches a world node, and record the count in the landing commit.

**Model recommendation.** Medium: a one-site fix with a fixture to make it fail first.

**Verify.** The new fixture red before, green after; `campaign-squad-wakeup` and
`campaign-roster` unchanged; a block placed by pose and never animated lands exactly where it does
today (assert the CM09 `M4ZepAttack` bloodhawks' wake positions in the docking suite's artifact
before and after).

**⚠ Traps.** A block placed by pose and never animated must not move. If the census finds no
shipped mission with such a node, the item still lands as a latent fix, and its Landed line says
so.

## B14 ☐ `BL-656` CM10: an attack balloon's marker never rests on the water

**Goal.** In CM10 (C1/M05) the objective marker for an attack balloon appears on the balloon from
the first frame it is offered, never on the water beneath it.

**Evidence (confidence: traced).** `CSVM/src/Session/ObjectiveSites.cs:135-146` (`SiteAnchor`)
takes the centre of the boxes `CollectMeshBoxes` (`:222-244`) merges, and that walk deliberately
includes hidden parts (comment at `:222-224`). Each `lifesaverNM` builds with its outer group and
inner `lifesaver` node off while `lifeballoon` and `lifeboat` are visible under them, and
`attack_waveN` switches the wave on later, so on the early frames the merged set includes the boat
at water level and the centre sits low. The anchor rule itself is the original's (`FUN_004cf2c0`,
midpoint of the node's active bounding box; `docs/org/targeting.md`, "Where a mission structure
is") and was landed by `BL-636`. `campaign-balloon-marker` (`CampaignMarkerSuites.cs:219`)
asserts the settled anchor, not the first frames.

**Approach.** Log the anchor and the visible mesh set per frame from the first offer to the wave's
arrival on a headless CM10 run, to see which set the early frames read. Then either defer offering
the site until its subtree is up, or read the node's authored `child_bbox` instead of the built
meshes, whichever the original's "active bounding box" turns out to mean; the decode's word
"active" is the lead. Extend `campaign-balloon-marker` to assert the anchor on the first offered
frame.

**Model recommendation.** Medium: a per-frame log and a bounded choice between two fixes named by
the decode.

**Verify.** The extended `campaign-balloon-marker` suite asserting the first-frame anchor is on
the balloon; D31 watches a wave arrive in CM10 with the marker already in frame.

**⚠ Traps.** Do not reintroduce a node-origin fallback or an upward offset; both were removed on
decoded evidence, and the balloons descend as they attack, so no constant is right at two
altitudes.

# Wave C — Hollywood (C2)

## C21 ☐ `BL-635` CM11: the stunt planes carry the objective marker their roster blocks author

**Goal.** In CM11 (C2/M02) both stunt planes, `secfury_5` and `secfury_6`, carry the objective
marker and the `MSG_OBJ_FOLLOW` label their roster blocks author.

**Evidence (confidence: traced).** `docs/formats/ai-rosters.md:54-59` documents roster slot 37
(`objectiveTarget`) and slot 39 (`helpLabel`, the `MSG_OBJ_*` key) as authored fields.
`CSVM/src/Mech3/AiSkills.cs:179-334` has accessors for slots 0 to 33, 40 and 65 to 67 only, so
neither slot is read anywhere; a repo-wide search for `MSG_OBJ_FOLLOW` and `MSG_STUNT_PLANE_NAME`
finds no code. Marker sites are driven only by `ObjectiveSites.cs` from `targets.zrd` and the
`ADD/REMOVE_OBJECTIVE_TARGET` directives, with no input path from a roster block. No suite
references `secfury`, `CM11` or a stunt marker.

**Approach.** Add the two slot readers to `AiSkills.cs`, plumb them through `CampaignRoster.cs`
and `CampaignDirector.cs` into the objective-target and help-label sets `ObjectiveSites.cs` reads,
scoped to the block's own aircraft, and census the shipped rosters for every block that authors
slot 37 or 39 so the same path is checked where it matters elsewhere. A new
`campaign-cm11-stunt-marker` suite asserts both aircraft carry a site and the label from the
objective that starts the follow.

**Model recommendation.** Medium: a data plumbing job along a known path, with a census to size
it.

**Verify.** The new suite; `campaign-balloon-marker` and the other marker suites unchanged; D31
flies CM11 from the objective that starts the follow and sees both markers.

**⚠ Traps.** The label is the block's, not `targets.zrd`'s; a fix that adds a target entry for
these aircraft invents data the mission does not author. Two aircraft share the key, so a marker
on only one is not a pass. C21 edits `CampaignDirector.cs` and `ObjectiveSites.cs`, which B13 and
B14 also edit; see the contention notes.

## C22 ☐ `BL-627` CM12: the Spruce Goose moves smoothly along its scripted legs

**Goal.** In CM12 (C2/M01) the Spruce Goose flies its legs without visible jitter while the player
formates on it.

**Evidence (confidence: lead-only, with the ownership traced).** Reported and re-confirmed at
the controls. The Goose is a world node, not a vehicle: no roster block, `VehicleList` entry or
`ZeppelinRuntime` reference names `sprucegoose`. Its pose is written only through
`extracted/C2/M01/zrdr/goosepath.zrd.json` and the `extracted/C2/M01/mis_anim/sprucegoose-*.json`
chain (`fly_the_goose`, `path2_accelerate` to `path2_continue` to `path2_decelerate`, `path3_*`),
chained `OnCall` through `CallAnimation`, into `CSVM/src/Mech3/Anim/PoseChannel.cs:303-329`
(`HandleMotionSiScript`) and `CSVM/src/Mech3/Anim/ScriptPlayback.cs`. So the "two owners" half of
the entry's fix shape has no candidate. The lead is the other half: each chain link is its own
definition, so each instantiates a new `ScriptPlayback` (`ScriptPlayback.cs:24-33`) that seeds
its rotation, scale and origin from `AnimRuntime.RestOf(target)` (`AnimRuntime.cs:1838-1843`),
the node's authored rest pose, not the pose the previous link left. If successive scripts are not
authored perfectly continuous from that shared reference, each hand-off re-seats. This is a
reading of the code, not a measurement. No suite references the Goose's defs.

**Approach.** Instrument first, as the entry demands: log the Goose's world position and rotation
per sim step over a straight leg on a headless CM12 run (`--debug-anim` or a temporary trace in
`ScriptPlayback`), and look for a discontinuity at each link hand-off versus a per-step
oscillation. A hand-off step confirms the re-seat lead; the fix then seeds a new playback from the
live pose where the original does so (find the script-motion seed rule in the decode before
changing the seed, since a per-definition rest seed may be what the original does and the
authored scripts may simply be continuous). A per-step oscillation points elsewhere, and the log
says where.

**Model recommendation.** High: the diagnosis decides the fix, and the seed rule touches every
scripted motion in every mission.

**Verify.** A per-step position trace with no discontinuity above the authored step; a
`--shots=` capture flying alongside the Goose at heading not zero, per INSTR-30; D31 formates on
the Goose from the harbour leg.

**⚠ Traps.** Do not judge from a still or a held probe, and do not smooth the pose. Take the
reading flying, over consecutive frames, with the camera not the source of the motion. INSTR-30
(`docs/verification.md`, float32 world-coordinate rounding) shows at 10 to 20 km with a rotated
attitude; the Goose's legs run at about 5 to 6 km from the origin, so float rounding is a possible
contributor to measure at non-zero heading, not an assumption. Any change to the script seed
moves other scripted motion and may move goldens; baseline first.

## C23 ☐ `BL-566` CM12: the ace `hkfirebrand_9` stays above the terrain after its wake

**Goal.** In CM12 (C2/M01) the ace, once OBJECTIVE67 wakes it, never flies inside the hills and
never dies by ramming a tile from below.

**Evidence (confidence: direction-sound).** Seen at the controls and in
`.scratch/logs/menu-20260828-001548.log`: the ace alternates `pursue` and `avoid crash (below the
20 m floor)` a dozen times beside terrain tile `tagged` (x -5120 to -4096, z -6144 to -5120, rising
to 215 m) and dies as `AI ram into tagged/col`. The floor is the decoded absolute one:
`CSVM/src/Flight/AiModeMachine.cs:84` (`AltitudeFloorM = 20f`) and `UpdateAvoidCrash`
(`:548-557`, `if (pos.Y < AltitudeFloorM)`), with a single forward probe along velocity on a
cooldown (`ProbeBlocked`, the comment at `:542-547` forbids a second deck-slanted ray). **The
entry's "terrain colliders are single-sided" premise is wrong:** `SceneBuilder.CollidersForMesh`
(`CSVM/src/Mech3/SceneBuilder.cs:894-897`) builds terrain with `BackfaceCollision = true` because
the source winding is inconsistent. The surviving lead is tunnelling: `SweepCadence`
(`CSVM/src/Flight/SweepCadence.cs`) sweeps aircraft against the world on the original's
alternating-frame parity gate, so a fast dive can skip the frame that would have caught the
crossing and the aircraft is then inside a tile whose surface sits above Y 20 m, where the floor
never fires. The wake places the ace at its authored (-4518, 150, -6233), as the original does.
Whether that point is already under our terrain is unmeasured.

**Approach.** Answer the placement question first: sample the terrain height at the authored
spawn with a `--freecam --pos=` capture and a downward probe (add a `--dump-` height read if none
exists). If the spawn is under ground, that is `BL-457`'s spawn-placement question and this item
records it and stops. If not, reproduce the dive headless with the AI trace on and log the sweep
parity, the aircraft's height above the tile surface and the probe result on the frames around
the crossing, to show whether the crossing lands on a skipped sweep frame. The fix then belongs to
the contact test at the crossing (a second sweep on the skipped parity when the step is longer
than the terrain's thickness, or whatever the original does on its own parity gate), not to the
floor.

**Model recommendation.** High: a diagnosis with two branches, one ending in a different item.

**Verify.** A headless CM12 run past OBJECTIVE67 with no `avoid crash` oscillation and no
`AI ram into tagged` death for the ace; the flight envelope and contact suites unchanged; D31
watches the ace after its wake.

**⚠ Traps.** Do not replace the absolute 20 m floor with an AGL floor; it is decoded
(`DAT_0071c3f0`) and the original has none either. Do not add a spawn lift. Do not "fix" the
single-sided collider premise; it is already double-sided. The ace's ram death did complete
primary 3, so this is not an objective bug. `SweepCadence` is a faithful port of `FUN_0048d7f0`'s
parity gate, so a change there needs the decode beside it.

## C24 ☐ `BL-618` CM13: a compiled anim addressing a `~n` dedup name resolves to the right sibling

**Goal.** A compiled animation that names a mech3ax dedup name such as `land_on~2` reaches the
sibling the suffix identifies, so CM13's `pzhomebase` switches both of the Pandora's landing cones
on and both draw.

**Evidence (confidence: traced).** `zeppelin-hull-activation`
(`CSVM/src/Testing/ZeppelinHullActivationSuites.cs`) records `cones 0/2` after `pzhomebase` has
run for 12 s. C2's gamez carries four `land_on` nodes, two under `piratezep`; the extraction
renames the second sibling `land_on~2`, and `CSVM/src/Mech3/Anim/NameResolver.cs` (`Resolve` at
`:207`, `FindAll` at `:173`, the matcher and `ResolveScoped` chain documented in
`docs/architecture.md`'s `NameResolver` entry) contains no handling of `~` at all, so the bare
name is ambiguous and the suffixed one matches nothing. `BL-616`'s closing commit recorded this as
the follower, not fixed.

**Approach.** In `NameResolver`, resolve a `~n` suffix scoped by the calling definition's own node
list: `land_on` roots on `pz_manual_land` and `land_on~2` on `pz_auto_land`, so the suffix picks
the nth sibling in the def's node order. Add a unit in `CSVM.Tests` over a hand-authored fixture
with two same-named siblings, then flip `zeppelin-hull-activation`'s expectation to `cones 2/2`.

**Model recommendation.** Medium: a bounded resolver rule with a fixture and a suite that already
fails.

**Verify.** The new unit; `zeppelin-hull-activation` at `cones 2/2`; the 8-chapter freecam
regression with unchanged node counts; D31 looks at the Pandora's landing cones in CM13.

**⚠ Traps.** Stripping `~n` and taking the first hit is wrong; the suffix identifies which sibling.
C24 shares the `Anim/` folder with A1 and C22 but is `NameResolver.cs` alone.

## C25 ☑ `BL-640` CM14: a broadside cannon stowed behind its hatch takes no weapon damage

**Goal.** In CM14 (C2B/M04) a round fired at a Gemini broadside cannon whose hatch is shut does
not damage the cannon; a deployed cannon takes damage as before.

**Evidence (confidence: traced).** Reported and re-confirmed at the controls.
`extracted/C2B/M04/mis_anim/lbroad11-destroy_gmzep_lbroad11-gunback.json` is
`activation: WeaponHit`, `health: 60`, `proximity_damage: false`, `activ_prereqs: null`,
`anim_root_name: "gunback"`, so the destructible's damage node is `gunback`, not the hatch
`upper_br_door`. `CSVM/src/Mech3/DestructibleRegistry.cs:129-145` (`Resolve`) attributes a hit by
climbing the node-parent chain from the collider the ray struck to the nearest node a pool claims,
with no reference to any door state, and `AnimRuntime.DamageNodeOf` supplies that node from the
root name. `docs/org/weaponRay.md:59-60` records the original's `INTERSECT_BBOX` node-flag mode,
where a bounding-box test replaces the polygon test. The nearest suite,
`zeppelin-cannon-burnout` (`CSVM/src/Testing/ZeppelinCannonBurnoutSuites.cs:29-37`), covers
C1/M04's `hk_zep` doors deployed from t=0; nothing references `lbroad11` or the Gemini.

**Approach.** Establish what a round strikes when the cannon is stowed: fire a scripted weapon test
at a stowed `lbroad11` headless and log the collider the ray met and the node `Resolve` climbed to.
The candidates are a retracted gun keeping a collider outside the hull, a door carrying no
collider, and a hit attributed by an ancestor's bounding box rather than the geometry met. Fix at
the site the log names, which is expected to be the attribution walk or the stowed gun's collider,
and add a `zeppelin-cannon-stowed` suite that fires at a shut hatch and asserts no damage, then
deploys and asserts damage.

**Model recommendation.** High: a hit-routing change on a path every destructible shares, in a
mission whose pacing the cannons' deaths set.

**Verify.** The new suite; `zeppelin-cannon-burnout` and the damage suites unchanged; D31 fires
at a stowed cannon and then at the same one deployed in CM14.

**⚠ Traps.** Do not add a "hatch open" test to the definition's activation; the data authors no
such prerequisite, and it would leave whatever lets a round reach an interior part free to do so
elsewhere. The `deploy_gmzep_lbroadNN` animations are the mission's progress counter (five of six
`INVALID` completing primary 3), so anything changing how easily a cannon dies moves the pacing.
`BL-629`'s routing fix chose between two definitions on one anchor; it is a precedent for the walk,
not this bug. `BL-639` (the gasbag burn-out) stays blocked on `CAP-47`.

**Landed.** A round that meets a shut hatch damages nothing. `DestructibleRegistry.Resolve` now
tells an own-damage-node claim from an anchor-fallback claim, and a climb that arrives at a live
pool through its anchor alone, while that pool's own damage node is switched off, answers with
nothing and stops climbing rather than passing the hit up to the airship's gasbag. A destroyed pool
is exempt, so a hit on a wreck still finds the pool that owns it. The definition's activation is
untouched, and no door state is read anywhere.

What a round actually strikes was established before anything was changed. `gunback`'s and `gun1`'s
colliders are already gone while stowed, since the deploy definition's `RESET_STATE` switches both
off and `WorldCollision` derives every collider from visibility, and `frame` never had one
(`intersect_surface` is clear on it in the gamez). The hatch is the only solid geometry left, and
`Resolve` climbed `upper_br_door` → `lbroad11` to the cannon's pool: 30 rounds into a shut hatch
destroyed a HEALTH 60 cannon. So this was the attribution walk, not a stray collider. The original
says the same thing in its own briefing text, `MSG_BRF_HWM4_OBJ3`: "Destroy the GEMINI by shooting
the open cannon hatches."

The pacing moves, in the direction the data authors. The six `deploy_gmzep_lbroadNN` INVALID states
feed a 1/3/5 objective ladder ending in PRIMARY 3, and the Gemini's cannons now cannot be hurt until
they deploy, which needs `OBJECTIVE25`'s `COMPLETED_ZEPCANNONS` (the two airships closing to 1000 m)
and the player inside the firing arc. No mission `startanims` deploys them early, so before that
gate the cannons are immune where they were previously killable through their hatches.

**Verified.** <pending orchestrator run>

## C26 ☐ `BL-632` CM15: the capture cutscene frames its Balmoral

**Goal.** In CM15 (C2/M05) the capture cutscene shows the Balmoral it is filmed around.

**Evidence (confidence: traced).** Reported at the controls with the pilot in a Bloodhawk in the
chase view, which rules out the cockpit-view visibility rules. The episode's definition is
`extracted/C2/M05/mis_anim/balmoral-drop_paratroopers.json` (`anim_root_name: "balmoral"`,
objects `camera1` and `balmoral`), the same definition `BL-633`'s paratrooper drop fix touched.
`CSVM/src/Mech3/AircraftStage.cs` stages no Balmoral: its staged lists are `PropNode`
`piratefighter` (`:28`), `FigureNodes` `rope_ladder` and `pickup_cpilot` (`:47`) and `PropNodes`
`anim_bloodhawk` and `bloodhawk_gear` (`:54`), and the string `balmoral` does not occur in the
file, so the actor is never served. `docs/architecture.md`'s `AircraftStage` entry lists the
staged subtrees. No suite covers this episode; the `chuteopen` suites cover the mission's other
cutscene.

**Approach.** Read `BL-596` and `BL-621`'s closing commits first (a hand-over that played without
its aeroplane, and a staged actor served only to a call naming a site), since either answer has
been written once. Then decide how the original serves this actor: a staged subtree from the
aircraft archive (extend `AircraftStage`'s lists, with the pose half in
`Session/CutsceneController.cs`) or a spawned roster vehicle resolved through `RosterMarkers.cs`.
Add a `campaign-cm15-capture` suite asserting the Balmoral node is built and visible inside the
episode's frame.

**Model recommendation.** Medium: the mechanism is known and two precedents show the shape.

**Verify.** The new suite; the `chuteopen` suites unchanged; a `--screenshot=` inside the
episode; D31 flies CM15 to the capture.

**⚠ Traps.** Do not close this against `BL-625`'s cockpit-view fix; that is presentation code and
cannot reach an NPC actor that was never staged. `BL-633` fixed the same definition's other
defect (the pilot left on the world root) and its change must survive.

# Wave D — Closing sortie

## D31 ☐ At-the-controls pass in mission order over every landed item that owes a judgement

**Goal.** Every landed item whose acceptance needs eyes gets them, in mission order, and every
finding becomes a same-day fix, a follow-up `BL`, or a recorded verdict. Each landed code item
mints its `PT` row in `playtest.md` when it lands; this item consumes them.

**Evidence (confidence: n/a; this item consumes the others' playtest lines).** The checks owed,
by mission: CM02 the auto-land's hook side parts (A1); CM05 and CM07 the escorts after the first
patrol dies (A2); CM08 the Pandora's stop over the tanker and the sequence (B11); CM09 a
gasbag-only kill's ending (B12); CM10 a wave arriving with the marker in frame (B14); CM11 both
stunt markers from the follow objective (C21); CM12 the Goose from the harbour leg and the ace
after OBJECTIVE67 (C22, C23); CM13 the landing cones (C24); CM14 a stowed then a deployed cannon
(C25); CM15 the capture (C26). B13 owes nothing at the controls unless the census finds a shipped
block with a world node. `PLAN-M5-polish-8.md`'s D31 checks (`PT-97` to `PT-106`) cover CM02,
CM09, CM10, CM12 and CM18 and can be flown in the same sitting.

**Approach.** One session, mission order; the session reads the sortie logs beside the author's
reports, lands unambiguous corrections the same day, and mints `BL` items for anything larger,
per the standing rule that the author's eyes outrank the instruments.

**Model recommendation.** High: reading reports against logs and deciding fix-now versus file is
judgement work.

**Verify.** Every code item's plan section carries a **Verified.** line naming what was seen; any
overturned reading is recorded in the item it overturns.

**⚠ Traps.** A live symptom is evidence about the build that was running: confirm which build and
worktree flew before minting anything, and check for testing worktrees left over from run 8. The
sim clock can lag wall time on physics-bound late-C2 missions (CM11, CM12); check the hitch lines
before blaming an authored rate.
