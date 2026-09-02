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
2. ☑ `BL-524` CM05/CM07: a wingman whose leader leaves play stops holding a bearing to a dead enemy

### Wave B — Northwest (C1)

11. ☑ `BL-597` CM08: the Pandora halts over the tanker and its sequence there plays
12. ❌ `BL-666` CM09: a zeppelin killed by gasbags alone ends the mission one way or the other (disproven: it already ends lost 32.6 s after the kill, and a suite now pins that)
13. ☑ `BL-665` A woken roster block is re-placed where the script left it, not at its authored pose
14. ❌ `BL-656` CM10: an attack balloon's marker never rests on the water before the wave arrives (disproven: the marker tracks its geometry, and the geometry itself dips; `BL-674`)

### Wave C — Hollywood (C2)

21. ☑ `BL-635` CM11: the stunt planes carry the objective marker their roster blocks author
22. ◐ `BL-627` CM12: the Spruce Goose moves smoothly along its scripted legs (diagnosed: render rate, not the animation; the session-wide fix is `BL-676`)
23. ❌ `BL-566` CM12: the ace `hkfirebrand_9` stays above the terrain after its wake (disproven: the ram is a contact the original does not have, because it backface-culls collision per polygon and both tiles here are single-sided; `BL-678`, with `BL-669` for the authored pose)
24. ❌ `BL-618` CM13: a compiled anim addressing a `~n` dedup name resolves to the right sibling
25. ☑ `BL-640` CM14: a broadside cannon stowed behind its hatch takes no weapon damage
26. ☑ `BL-632` CM15: the capture cutscene frames its Balmoral

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

## A2 ☑ `BL-524` CM05/CM07: a wingman whose leader leaves play stops holding a stale bearing

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

**Landed.** A wingman whose leader leaves play inherits that leader's own patrol net, which the
author chose over the three options put to them: it uses only authored data, a candidate always
exists (every one of the 53 escort blocks names a netted leader), and it needs no invented
constant. `CampaignDirector.TakeLostLeadersNets` runs each `Step` over the roster and hands such a
pilot the net its leader is walking, through the same `SeatOnNet` body `SET_AI_NET` uses, so the
escort buffer is dropped and the machine's gates are re-baselined the way a scripted net
assignment does. A leader that flies no net, which is every player-led escort, leaves its wingman
exactly as it was and says so once. The behaviour reaches 32 wingmen in 17 of the 53 shipped
missions, the netless blocks whose `primary_target` is a netted `devastator`; the other 21 escort
the player and are untouched. `AiPilot` itself is unchanged: its netless arm still projects the
orders it was left with, which is what makes the hand-off necessary and is pinned as such.

**Verified.** <pending orchestrator run>

# Wave B — Northwest (C1)

## B11 ☑ `BL-597` CM08: the Pandora halts over the tanker and its sequence there plays

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

**Landed.** The sequence is `zepgetcargo` (`extracted/C1B/M03/zrdr/pzep_getcargo.zrd`), called by
`objectives.zrd`'s `OBJECTIVE17` `WAKE_ANIM ["zepgetcargo"]`: the freighter's hold doors, the
Pandora's cargo doors 8 s later, then `activate_pzep_crane` riding the crane 54 m down its chain
and back up, looped 99 times. `OBJECTIVE17` is dormant until `OBJECTIVE11` completes on
`DEDG [3, 0]`, the mission's four patrol boats, which also releases `Klondike1` stop 7 and naps
`OBJECTIVE17` by 70 s, the transit from node 5 to the cargo point at node 7. The stop the Pandora
was missing was not the arrival radius, which an armed stop point never consults: the hull was
parked on the follower's 30 m hold sphere because `ZeppelinMotion.Step` gated its dock glide on
`!Follower.Holding`, and `Holding` latches the moment that sphere is crossed. The glide now runs
through the hold, so the hull settles on the node in plan and in altitude, while a follower still
on its SEAT keeps the own-node station-keep and holds the record's own pose. Measured on the real
route: 29.9 m off node 7 before, 0.1 m after, at the node's own 93.4 m, which is 93 m over the
freighter `freighteraground` beaches at (−6246, 0, −7572) and the drop a 54 m chain off a hatch
33 m under the hull needs. Documented in `docs/formats/mission-entities.md` "Route ends and stop
points". `PT-109` flies it at the controls.

**Verified.** <pending orchestrator run>

## B12 ❌ `BL-666` CM09: a zeppelin killed by gasbags alone ends the mission one way or the other

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

**Disproven.** A gasbag-only `piratezep` kill already ends CM09, and it ends it lost. The new
`campaign-cm09-gasbag-kill` suite drives the mission over its own collision world through the
intro hold, kills three gasbags and nothing else, and reads the graph: OBJECTIVE26 completes 2.6 s
after the kill, naps 27 for the authored 10 s, 27 completes on its wake, naps 41 for the authored
20 s, and OBJECTIVE41's `INSTANTLOSS` ends the mission 32.6 s after the kill. No code changed in
`ZeppelinRuntime.cs`.

Three of the Evidence paragraph's claims were wrong. The loss chain is 3 then 6 rather than six,
and it reads world node active bits rather than the runtime engine count, so driving
`Motion.AliveEngines` to zero would have changed nothing. OBJECTIVE40 carries no
`TICK_DEPENDS_ON_OBJ` at all (42 does), and nothing on the death path deactivates the `piratezep`
node, so 40 does not retire and 42 is not gated off. And what makes the engines dark is not
`killpzep` alone: gasbag N's left and right death definitions each destroy the two engines on
their side of bay N, so the three gasbags that kill the hull darken all twelve engine `healthy`
models on their own, whether or not the wreck ever reaches the water. `zeppelin-breakup` now reads
those twelve nodes rather than only counting the six dispatches, which is what settled it.

`BL-666` was never a `backlog.md` entry, only an id reserved in `PT-98`'s *Blocks:* clause for a
symptom nobody had flown. There is no symptom, so the id stays unused and that clause is rewritten.
What the measurement leaves open is the wait: 30 of the 32.6 s are the two authored naps, so a
player who quits earlier sees a mission that ends in neither direction. `PT-115` puts that
judgement at the controls.

**Verified.** <pending orchestrator run>

## B13 ☑ `BL-665` A woken roster block is re-placed where the script left it

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

**Landed.** `CampaignDirector` keeps a `_rosterPlacedPose` dictionary beside `_rosterPlans`, filled
in `BuildRoster`'s spawn loop with the pose the rig is actually placed at (the node override where
`FindNodes` resolves one, the authored spawn otherwise). `World.WakeupEnemies` re-places a woken
block from that placed pose, falling back to the plan's authored pose for a name with no placed
entry (a generator launch, which this item does not touch). The census, `CampaignRosterPlan.Build`
run over all 24 shipped missions' rosters checking each deactivated block's name against
`GameZ.IsLibraryRoot`, finds none names a placed world node: this lands as a latent fix, and no
shipped mission shows the symptom today.

**Verified.** <pending orchestrator run>

## B14 ❌ `BL-656` CM10: an attack balloon's marker never rests on the water

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

**Disproven.** `SiteAnchor`/`CollectMeshBoxes` already implements the original's own rule
(`docs/org/targeting.md`, "Where a mission structure is": the midpoint of the node's active
bounding box) and reads `lifesaver11`'s live geometry with no staleness, from the tick its wave
wakes through its whole SiScript entrance. `lifesaverNM` and its inner `lifesaver` node do start
inactive while `lifeballoon` and `lifeboat` are active under them, matching the evidence, but
`CollectMeshBoxes` never reads `Visible` or `flags.active` at all (its own comment says hidden
parts are walked on purpose), so the merge already spans both the lifeboat and the balloon from
world build onward. A 70-second, per-tick drive through the shipped `OBJECTIVE10` wake trigger
never once finds the anchor outside the group's own currently built mesh bounds (worst margin
13 m over 700 sampled ticks). What the original report saw is wave 1's own scripted entrance: it
carries the whole assembly, lifeboat and balloon together, from a hidden altitude down past the
water before the rise sequence lifts it to attack height, and the anchor correctly tracks that
live pass, reading about 11.7 m above the group's own current base throughout, matching the
decoded midpoint formula rather than a stale merge. Neither candidate fix would change this: the
flagged node's own `child_bbox`, transformed only by its own unmoving transform, is the same
value `CollectMeshBoxes` already produces at rest, and deferring the offer would not move a
reading that is already live and correct. `campaign-balloon-marker` now also drives the shipped
wake trigger directly and asserts this invariant.

**Verified.** <pending orchestrator run>

# Wave C — Hollywood (C2)

## C21 ☑ `BL-635` CM11: the stunt planes carry the objective marker their roster blocks author

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

**Landed.** `AiSkills.RosterObjectiveTarget`/`RosterHelpLabel` read slots 37/39;
`RosterSpawnPlan.ObjectiveTarget`/`HelpLabel` carry them into the plan; `CampaignDirector`'s three
spawn paths (the roster loop, a generator launch, a surface vehicle) book a flagged block's own
name and label into a new `RosterObjectiveMarkers` map, gated on the flag rather than the label
alone (C4/M05's `blakepeace_3_1`/`_2` author a non-key slot 39 string with the flag unset).
`ObjectiveSites.CollectTargets` reads that map as a third source alongside `targets.zrd`'s own
entries and the graph's `ADD_OBJECTIVE_TARGET` adds, filtered by the same completed
`REMOVE_OBJECTIVE_TARGET` targets.zrd entries already are: CM11's own OBJECTIVE1/40/41/42/54 all
remove `secfury_5`/`secfury_6` this way without ever adding them, which an unfiltered reading would
have kept showing after the follow ends. `SiteFor`'s label priority is the graph's own
`SET_HELP_LABEL`, then the roster block's slot 39, then `targets.zrd`'s. One position source had
to be added past what the Approach anticipated: a roster aircraft with no chapter-gamez
library-root copy under its own block name (`secfury_5`/`_6` included; C2's gamez carries no
`fury`-named node at all) is never indexed on `AnimRuntime` by `RosterMarkers.Attach`, so the
existing `Resolve`/`SiteAnchor` path can never find one; `ObjectiveSites.RosterAircraftPosition`
reads the spawned `FlightController`'s own live `WorldPosition` instead. The census: 8 of 414
shipped blocks author slot 37 = 1 (CM11's pair, C2/M05 `balmoral_1`, C3/M05
`britbalmoral_1`/`_2`/`_3`, C5/M01 `autogyro_1`, C5/M04 `stihellhound_5_7`), and this fix reaches
all eight through the same path, not CM11 alone.

**Verified.** <pending orchestrator run>

## C22 ◐ `BL-627` CM12: the Spruce Goose moves smoothly along its scripted legs

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

**Diagnosed; the fix is a separate item.** Both of the entry's leads are dead and so is the
re-seat mechanism the Approach was written to confirm. What is left is measured: the Goose's
authored path is continuous, the runtime that plays it is clean per sim step, and the roughness
is a render-rate defect. `AnimRuntime` advances scripted motion once per 60 Hz physics tick
(`AnimRuntime.cs:1285-1296`, whose own comment forbids a frame-driven advance because the sim
falls behind wall time under load), so a scripted node's pose is written 60 times a second and
held between writes. The player's aircraft is not: `FlightController` draws it between its last
two sim poses at `Engine.GetPhysicsInterpolationFraction()` once per rendered frame
(`FlightController.cs:1782-1786`), and every camera mode is aimed from that interpolated pose, so
the camera moves at the render rate. That single call is the only render interpolation in the
codebase, and its own field note at `FlightController.cs:533-538` states the mechanism it was
added to remove: a raw sim pose "stutters against the smoothly-moving chase camera at any render
rate above 60 fps, in proportion to speed". The Goose runs 11 to 37 m/s, the fastest scripted
node in the game, and this machine renders CM12 well above 60 fps. Measured: a fixed-dt
`--anim-lab` trace of 2602 consecutive sim steps over `free_the_goose` and its hand-off into
`path2_decelerate` reads a worst-case per-step displacement of 0.2534 m against the 0.62 m
authored ceiling, a 0.0098 m step across the hand-off itself against 0.0088 m on the step before
it, no oscillation at any 0.667 s frame boundary and a unit rest scale throughout; the same
mission on a Realtime clock reports `phys_hz` at a 59.99 mean (58.5 to 61.7) against `fps` at a 97.9 mean
(16.1 to 122.0), above 60 in 163 of 190 `--perf` windows. So the sim rate is steady and the
render rate is not, and the two are not tied together for anything except the player's aeroplane.
The fix is a render-pose pass over the live motion set, which `AnimLab` already implements for
its own clock (`AnimLab.cs`, `_renderPoses` / `RestoreSimPoses` / `SnapshotSimPoses`); it lands in
`MotionSet.cs` plus a caller in `GameSession._Process`, neither of which this item owns, and it
is a session-wide behaviour change rather than a Goose fix. `AnimRuntime.RestOf` is untouched: the
original's decoded seed rule writes each frame's own cubic with absolute setters and reads nothing
from the node, and every Goose frame carries an absolute translate and rotate, so the seed reaches
only a channel no Goose script writes. The instrument is kept: naming a node in
`CSVM_TRACE_SISCRIPT` logs `ScriptPlayback`'s per-step pose, dt, wall delta and drawn-frame count.
`PT-114` carries the at-the-controls judgement, including the `--max-fps 60` A/B that discriminates
this reading from any remaining pose question.

**The goose chain's branch is chosen by activation prerequisites, not by a call race.** The trace
runs `free_the_goose` and then `path2_decelerate`, and `path2_continue` never starts, which is the
opposite of what an invalidate race would produce. `sprucegoose-path2_continue.json` requires the
`healthy` node under `tugandbarge01` INACTIVE and `sprucegoose-path2_decelerate.json` requires it
ACTIVE, so the two legs are the blockade's two outcomes and both are reachable; `path2_accelerate`
requires `snkchk` inactive, which is why an unflown stage parks the Goose after the decelerate leg
rather than stalling. Nothing in the chain is dead. **Separately, CSVM's `CALL_ANIMATION` ordering
does diverge from the original and that is a real defect for other missions:** `AnimRuntime.Start`
advances a callee at t=0 inside the dispatch, while the original appends the callee at the tail of
the action list its dispatcher walks (`FUN_004ed8c0` into `FUN_004d04e0`, the append at `004d050d`)
and never runs a callee event during the caller's own tick, so a callee's `INVALIDATE_ANIMATION`
cannot latch before the caller's later events. That belongs to `AnimRuntime.cs` and wants its own
item and its own decode page.

**Verified.** <pending orchestrator run>

## C23 ❌ `BL-566` CM12: the ace `hkfirebrand_9` stays above the terrain after its wake

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
exists). If the spawn is under ground, this item records it and stops. (The plan named `BL-457` as
that question's home; `BL-457` is closed and was never about spawn placement, so the finding is
recorded as `BL-669`.) If not, reproduce the dive headless with the AI trace on and log the sweep
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

**Disproven.** The item's own lead and its first replacement both died, and the defect turned out to
be on a third mechanism that no line of this entry names. Nothing lands in `AiModeMachine` or
`SweepCadence`; the fix belongs to `BL-678` and the finding to `BL-669`, with `PT-107` for the
at-the-controls half. The suite `ace-wake-terrain` pins what was measured.

The authored pose is real and is not the defect. `hkfirebrand_9` is authored at
`(-4517.72, 150.0, -6232.58)` with `deactivated` (slot 21) set, so `OBJECTIVE67`'s `WAKEUP_ENEMIES`
puts it in play in place. The terrain surface over that point is **228.92 m** on tile `g35052`
(model 617, whose vertex bounds reproduce the node's own `model_bbox`, and whose collider the built
world answers at `y 229`), so the ace begins 78.9 m below it, the only roster block in the mission
placed over land at all. Being there costs nothing by itself. Terrain is a sheet with no underside,
so the space below it is open air: a downward ray from the pose finds nothing, and `SweepProbes`
registers only where the motion crosses a face. Nor can the ace fall, because its authored range
from the player is 3867 m, which puts it on the far-field plant, and that branch computes no gravity
at all and holds `nose · (fd_speed · throttle + 5)`.

Tunnelling is dead. `SweepCadence.Advance` hands the skipped step's entry pose back as the sweep
origin, and `SweepProbes` and `CenterRayContact` both run from that origin to this step's pose, so no
span of motion goes untested. `FUN_0048d7f0` does the same: its parity gate
(`((obj[0x1af] ^ frame) & 1) != 1`) adds the step's delta into the `obj+0x6B0` accumulator and
returns, the sweeping frame subtracts it from the displacement it tests, and the tail resets it. The
20 m floor is dead too: at 150 m it is correctly silent, and the original has no AGL floor either.

The LOD transition is dead as well, on timing. A level track along the ace's authored yaw 120 meets
the sheet again 260 m out, about 5 s at the plant's 52.3 m/s hold, where a mover would need 55 s to
close the 2867 m from the wake range to the far-field boundary. The crossing is reached far-field,
so the plant flip is not involved and needs no change; its missing hysteresis is decoded and stays.

What is left is the crossing itself, and it is ours. That face is taken from behind, and the
original culls exactly that: `FUN_0055c9c0` passes polygon flag bit 0 (`SHOW_BACKFACE`, bit 10 of
the word whose low ten bits are the vertex count) to `FUN_0055d6c0`, which returns no hit when the
segment's end lies on the front side and the flag is clear, before it ever tests for a crossing.
All 37 polygons of `g35052` and all 23 of `tagged` clear that flag, and 96.8 % of C2's terrain
polygons do (60.5 % of all its polygons). CSVM sets `BackfaceCollision = true` on every world
collider, so the ace is stopped where the original lets it out, and `AI ram into tagged/col` is a
contact the original never has.

⚠ The descent readings this entry carried before, a player-piloted rig sinking from 150 m to 16 m,
measured nothing about the ace: a human rig is near-field by construction, so it is handed gravity
the far-field ace never gets. A powered aircraft on the far branch does not sink at all.

**Verified.** <pending orchestrator run>

## C24 ❌ `BL-618` CM13: a compiled anim addressing a `~n` dedup name resolves to the right sibling

**Goal.** A compiled animation that names a mech3ax dedup name such as `land_on~2` reaches the
sibling the suffix identifies, so CM13's `pzhomebase` switches both of the Pandora's landing cones
on and both draw.

**Evidence (confidence: traced to code and a driven suite).** `NameResolver` already resolves this
correctly and needs no change. `pzhomebase`'s compiled symbol table binds `land_on` and `land_on~2`
to distinct gamez indices (3618 and 3615 in C2/M03, one under `pz_manual_land` and one under
`pz_auto_land`), and `SymbolClaims`/`Targets` (`CSVM/src/Mech3/AnimRuntime.cs`) resolve each by
that index alone, before any NAME-based matching runs; the `~` suffix is never interpreted, and
does not need to be. `zeppelin-hull-activation`'s reported `cones 0/2` was the suite's own 12 s
drive window ending before the choreography's real completion: `pzhomebase` gates both cone
activations behind `pz_deploy_hook`'s `WAIT_FOR_COMPLETION`, whose longest track starts 3 s in and
runs 10 s, so the cones are not due before t=13 s. Driving the same suite to 20 s shows both cones
bound to their own distinct node and drawing together the instant the gate clears (`cones 2/2`).
`BL-616`'s closing commit recorded the `~n` name as the open follower; that follower does not
describe a real defect.

**Outcome.** Disproven as filed. `CSVM/src/Mech3/Anim/NameResolver.cs` is unchanged: a `~n` dedup
name is already routed through the compiled symbol table's per-index binding, never through the
NAME matcher, so no sibling is ever picked ambiguously.

**Landed.** `CSVM/src/Testing/ZeppelinHullActivationSuites.cs`: the drive window is long enough to
reach `pzhomebase`'s own completion, and the cone count is now an assertion (`cones 2/2`) rather
than a recorded-not-asserted line. `CSVM.Tests/NameResolverTests.cs`:
`SymbolLookupResolvesADedupSuffixToItsOwnSibling`, a hand-authored fixture with two same-named
siblings under different parents, locks in that `SymbolClaims` picks the sibling the compiled
index names.

**Verify.** The new unit; `zeppelin-hull-activation` at `cones 2/2`; D31 looks at the Pandora's
landing cones in CM13. No resolver change lands, so the 8-chapter freecam regression does not
apply.

**⚠ Traps.** Stripping `~n` and taking the first hit would still be wrong if a case ever turns up
where the compiled index is not built; that fallback path was not exercised here. C24 shares the
`Anim/` folder with A1 and C22 but is `NameResolver.cs` alone.

**Verified.** <pending orchestrator run>

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

## C26 ☑ `BL-632` CM15: the capture cutscene frames its Balmoral

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

**Landed.** `balmoral` is aircraft-archive node 2381 (a parentless `Object3d`, model-less, five
children, shipped ACTIVE, no `RESET_STATE`), the same archive `piratefighter`/`chuteman` come
from, confirmed absent from `planes.zip`'s node table under no other name and absent from C2's own
chapter gamez entirely. `AircraftStage` now stages it beside `piratefighter`, built ACTIVE and
rebased the same way, so the drop's own `OBJECT_ADD_CHILD`/`OBJECT_MOTION_FROM_TO`/
`OBJECT_DELETE_CHILD` triple (authored inside `drop_paratroopers` itself, no intermediate caller)
finds a node instead of a null binding. The `campaign-cm15-capture` suite drives that definition
over C2/M05's built world and reads it drawn, reparented onto `cargozep2`, moved off the archive's
own origin and inside the cutscene camera's frustum through the shot (best 1.7 deg off axis), then
handed back to the world root. Neither `BL-596`'s nor `BL-621`'s shape applied directly: the actor
is not a roster-spawned vehicle (`RosterMarkers.cs`'s resolution never enters this def, which is
started by `PlayMissionTrigger` and reaches its anchor through the general name index the moment
`AircraftStage` puts a node under that name), and the site-naming distinction `BL-621` decoded does
not arise here since the drop's own `OBJECT_ADD_CHILD` always names its site (`cargozep2`)
explicitly. `BL-633`'s guard is unexercised by this change: `CutsceneController.cs` was not
touched, and the `cutscene-handoff-unposed` suite's own numbers are what would show a regression.

**Evidence correction.** No suite named or shaped `chuteopen` exists for C2/M05, checked directly:
neither `hooked_to_klondike` (the mission's other cutscene, also compiled for C3/M05 under the
same shared name) nor its `cutscenes/chuteopen/` SI-script trio (`chutemanparent`/`pilot`/`stamp`)
is driven by any suite in `CSVM/src/Testing/`. The plan's "the chuteopen suites cover the
mission's other cutscene" does not hold; that cutscene remains unverified by any suite, mine
included, and is out of this item's scope.

**Verified.** <pending orchestrator run>

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
