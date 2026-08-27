# Milestone 5 polish, run 2: the campaign flown as a game

**ACTIVE PLAN** (written 2026-08-26). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan is the second polish run over Milestone 5, and it carries eleven open campaign items out
of `backlog.md`, chosen playable-first: the defects a human meets while flying a campaign mission
rank ahead of presentation, and presentation ranks ahead of research. Two items were named by the
user rather than by that ordering, `BL-503` (the airframe swap's missing hand-over) and `BL-482`
(the intro cutscene stages no aircraft), and both are in Wave B. The plan opens by repairing the
record itself, because the sweep that selected these items found three stale pointers in it (A1).

**Every item here was re-verified still-open in this session against both the record
(`git log --grep=<ID>`) and the backlog entry's own cited `file:line`.** One candidate did not
survive that check: `BL-506` (an AI aircraft's defensive turrets are never built) was closed by
`43b47435`, whose message reads "Closes BL-506 and files BL-507", and its entry is stale in
`backlog.md`. It is not planned as work; deleting the entry is part of A1. The six other items whose
`git log --grep` hits looked like closures (`BL-507`, `BL-503`, `BL-502`, `BL-501`, `BL-497`,
`BL-457`) were each read at the commit message and are filed-by, not closed-by, that commit;
`BL-457` is explicitly held open by `84d66553` ("A3 partial with BL-457 open").

**Deliberately out of scope.** `BL-446` (the MPG cinemas) is plan-sized on its own: it needs a
transcode step in the extraction pipeline plus a presentation judgement nobody has made, and the
user excluded it from this run. `BL-463` (Change Memento) rests on `BL-256`, deferred by explicit
user decision, so it cannot be scheduled without reopening that decision. `BL-502` (`SET_AI_NET`
and `SET_AI_TEAM` reach no zeppelin) is out on the plan's own ordering rather than by request: its
entry records that no mission the player is currently trying to finish depends on it. `BL-299`
(multiplayer spawn decode), `BL-314` (race countdown, blocked on `PT-45`), `BL-301`, `BL-300`,
`BL-256`, `BL-426`, `BL-469`, `BL-501` and `BL-455` are open campaign-adjacent items left in the
backlog for a later run.

## Milestone goal

- An AI aircraft's carried turret is a gun on a silhouette, not a second target beside it, so the
  campaign's newly-crewed bombers read as one aeroplane each.
- A campaign pilot's authored `talker` and constitution ratings reach the voice runtime, so an ace
  and a mook do not chatter identically.
- CM02's second Peacemaker squad sleeps until its authored gate opens, and comes for the player when
  it wakes.
- The campaign wingman holds station well enough that it reads as flying with the player rather than
  as having spawned in the wrong place.
- The chapter intro cutscene stages the two aircraft it animates, so it is not an empty sky.
- The mid-mission airframe swap hides the captured aircraft, carries its damage into the player's
  hull, and hands the outgoing aeroplane to `wingman_4`.
- The auto-land the approach table offers has a prompt and a button behind it.
- `campaign-objectives-hud` is green on all five extracted chapters, without any assertion having
  been weakened to get there.

**This plan does not touch the flight model, the netcode, or anything blocked on a capture that does
not exist.** The flight model's own parity plan just closed, and re-opening it from a polish run is
how a polish run becomes a milestone. `BL-457` is the one item that brushes against it, and its
Approach is deliberately scoped to the escort law's hand-off rather than to the plant.

## Decisions (2026-08-26)

| # | Question | Decision |
|---|---|---|
| 1 | What weights the ten-item selection? | **Playable-first.** Defects a human hits while flying a campaign mission rank first, presentation second, research last. Chosen by the user over fidelity-first, completeness-first and mixed-by-section. |
| 2 | Are the two plan-sized items in? | **`BL-482` in, `BL-446` out.** The user took the intro cutscene (decoded, so it is scoped work) and left the MPG cinemas (a pipeline change plus an unmade presentation call). |
| 3 | How are at-the-controls items handled? | **In, and the plan stops for them.** Items ending in a human judgement are planned, and the plan halts at an explicit stop rather than a suite standing in for the sortie. |
| 4 | Is `BL-503` in, given it was not top of the playable ordering? | **In, by user request.** Added to the playable-first set explicitly. |
| 5 | Does `BL-491` reopen a deliberate deferral? | **Yes, and A6 asks before it builds.** The entry is `[Deferred: useful while debugging]` by the user's own decision, and its own text says that reason decays once the campaign is judged as a game rather than debugged. This plan is that judgement, so the deferral is put back to the user at the top of A6 rather than assumed lifted. |
| 6 | Does `BL-458` get its own item? | **Yes, as the flown close-out (C12).** It is code-complete and open only pending one thing no suite can show, so it rides Wave C's sortie rather than opening a build item. |

## ⚠ Read this before implementing anything

These claims were made about items in this plan and are dead. They are recorded here rather than
only inside their item, because each one is plausible enough to be re-derived by a reader who opens
a different item first.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The campaign wingman spawns in the wrong place (`BL-457`'s reported symptom). | `C3/M01`'s `aiv.zrd` authors `player` at `[-1426, 150, -1813]` yaw 40 and `wingman_1` at `[-1378, 150, -1706]` yaw 40, 117 m apart at one altitude and heading, and the spawner puts it exactly there (`CampaignRoster.cs:191-192`, confirmed live). The separation opens over the following 26 s. |
| 2 | Nitro asymmetry explains `BL-457`'s drift. | A runtime probe on both rigs reads `leader installed=False everBoosted=False, wingman installed=False`, and the suite file is unchanged across the merge. **Must not be re-chased.** The separate nitro case is `BL-469`, which is not in this plan. |
| 3 | An airframe mismatch between pilot and wingman explains `BL-457`. | The reporting profile carries `selectedPlane 0` and `wingmanPlane 1`, both airframe 5, the Devastator. The probe that flew a Bloodhawk against a Devastator was a test-rig artefact. |
| 4 | Main's deletion of `AiControlLaw`'s far-field branch caused `BL-457`'s regression. | That branch keyed off `AiPilot.PlayerPosition`, which the `wingman-station` suite never assigns, so it was already dead in this measurement. |
| 5 | A joined escort re-enters `Joining` past 700 m (`BL-457`'s own earlier line). | The law has no such transition; nothing inside the formation state leaves it (`AiEscort.cs:233-235`). |
| 6 | The 4-per-world bootstrap errors belong to a sound bind (`BL-484`). | The harness allowlist attributed them to one; they are `PoseChannel.PoseAtNode` (`PoseChannel.cs:383,386,391`) reading and writing global transforms out of tree. The allowlist entry and cap are corrected to name this. |
| 7 | `campaign-objectives-hud`'s C4/C5 failures were caused by the completion-driver work (`BL-483`). | `CampaignHudSuites.cs` was reverted to its committed state, rebuilt, and both failures reproduced identically (METHOD-8). They are pre-existing. |
| 8 | `docs/formats/combat-voice.md` states the campaign voice ratings already work (`BL-497`). | Corrected by G75. Read the doc's current claim, not an older one. |
| 9 | `BL-506` (an AI aircraft's defensive turrets are never built) is open. | Closed by `43b47435`. The `backlog.md` entry is stale, and A1 deletes it. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2, A3, A4, B7, B8, B9, C10, C12 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | A5 | The escort's hand-off is the place to look and the numbers are measured; what the escort should do there is not settled. |
| **Leads only, no mechanism yet** | A6, C11 | Budget for investigation. C11 in particular may end in a disproof rather than a fix. |

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
  frozen, never append) and is **deleted** from
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

### Wave A — the record, then the flown mission

1. ☑ Retire the record's three stale pointers (`BL-506`, both M5 plan banners, "Current status")
2. ☑ An AI pilot sees an enemy aircraft's own turret as a target beside the aircraft (`BL-507`)
3. ☑ A campaign spawn's talker and constitution ratings never reach `AiVoiceRuntime` (`BL-497`)
4. ❌ CM02's second Peacemaker squad is awake from the start and attacks the Pandora (`BL-499`)
5. ☑ The campaign wingman ends up high and far behind (`BL-457`)
6. ☑ Crashing the player's aircraft does not end a campaign mission (`BL-491`, deferral reopened first)
13. ◐ The mode machine's `avoid crash` pre-empts a joined escort (`BL-509`, minted by A5)

### Wave B — the intro and the swap

7. ☑ A bootstrap `AT_NODE` pose lands at the world origin (`BL-484`)
8. ☑ The intro cutscene stages no aircraft (`BL-482`)
9. ☑ An airframe swap leaves the captured aircraft flying, and never hands the outgoing one over (`BL-503`)

### Wave C — offered but inert, and the red suite

10. ☑ The auto-land the approach table offers has no button (`BL-460`)
11. ☑ `campaign-objectives-hud` fails on C4 and C5 on its wake-cue check (`BL-483`)
12. ☑ Fly a campaign mission end to end: the Wave A/B/C sortie, closing `BL-458`

### Wave D — what the sortie opened

14. ☐ The cutscene's world hold does not reach an aircraft's own realtime tick (`BL-457`, minted by C12)
15. ☐ The captured Balmoral is not hidden by the swap in a flown session (`BL-541`)
16. ☐ The captured aircraft keeps its British livery after the capture (`BL-543`)
17. ☐ CM02's capture cutscene camera sits over the water (`BL-542`)
18. ☐ The auto-land prompt is not drawn in a flown session (`BL-544`)
19. ☐ The landing animation: no hook, too high, wings not folded (`BL-545`)
20. ☐ CM01's drop-off cutscene shows no parachutist (`BL-540`)
21. ☐ The low-terrain break-off, judged once the wingman is there (`BL-509`, A13's open half)

## Dependency and parallelism notes

A1 runs first and alone; it is a documentation edit and every other item's landing commit touches
the same "Current status" block, so landing it first stops eleven items each re-deciding what that
block should say.

**B7 blocks B8.** `BL-482` needs aircraft-archive subtrees posed by the animation runtime at the
bootstrap, before the world root is in the scene, which is exactly the condition under which
`PoseChannel.PoseAtNode` returns identity today (`BL-484`). Building B8 on the broken path would
stage two aircraft at the world origin and read as a different bug.

A2, A3 and A4 are independent of each other and of Wave B, and can run in parallel worktrees. A5 is
independent in subject but is the item most likely to want a merge from main mid-flight, so give it
its own lane.

**File contention.** A2 and A4 both reach the AI target/roster path (`FlightController.AddRankedNonAircraft`
for A2, the roster spawn block for A4); do not run them in parallel worktrees without a stated
ownership split. B8 and B9 both reach the animation callback host, B8 for the node table and B9 for
the root vehicle name, so the same applies. C11's investigation reads `CampaignHudSuites.cs`, which
nothing else here writes.

C12 runs last by construction: it is the flown sortie that judges A5, B8, B9 and C10 at the controls
and closes `BL-458`.

**Wave D ordering.** D14 runs first and alone: it is the realtime gap behind `BL-457` and, on the
evidence, behind `BL-541` (an aircraft stepping itself past `Inert`) and `BL-544` (a rig fed on the
parent-driven path only), so D15 and D18 are re-measured after it lands before anything is built. D16
and D17 both reach the capture definition and `FlightRoster.RunSwap`, so they share one lane after
D15. D19 and D20 are independent animation reads and can run in parallel worktrees. D21 is judged at
the controls after D14, and is a stop like C12.

---

# Wave A — the record, then the flown mission

## A1 ☑ Retire the record's three stale pointers

**Goal.** The record says what is true: `backlog.md` carries no closed item, a completed plan does
not call itself active, and "Current status" names this plan.

**Evidence (confidence: traced).** Three findings from this session's sweep. (a) `BL-506` is closed
by `43b47435` ("Closes BL-506 and files BL-507") and its entry is still in `backlog.md`'s
"Missions, modes & campaign" section, against the convention that a landed item is deleted rather
than marked fixed. (b) `docs/PLAN-M5-polish.md:3` still reads **ACTIVE PLAN** although
`5c1af20f` merged it as "PLAN-M5-polish complete, 48 items landed and 3 disproved", and it sits in
`docs/` rather than `docs/plans/`. `docs/PLAN-M5-campaign.md` is in the same position and needs the
same check. (c) `PROJECT_CONTEXT.md`'s "Current status" reads "Active plan: none; the last completed
is `docs/plans/PLAN-m4-polish.md`", which predates both M5 plans.

**Approach.** Delete the `BL-506` entry from `backlog.md`. For each of the two M5 plans, confirm
against its own checklist and its merge commit whether every item landed; if so, swap the banner for
a `✅ COMPLETE` one, move the file to `docs/plans/`, and append its row to `docs/plans/plans.md`. If
`PLAN-M5-campaign.md` still has open items, leave it live and say so in the commit rather than
archiving it to tidy up. Then swap "Current status" to name this plan. Do not lengthen that section:
its own rule is that it must be no longer after the edit than before.

**Model recommendation.** medium, low effort. Mechanical record-keeping against facts already
established in this plan's scope paragraph; the one judgement is whether `PLAN-M5-campaign.md` is
actually complete.

**Verify.** `git log --grep=BL-506` still shows the closure and `backlog.md` no longer defines the
ID; the duplicate-item-ID pre-commit hook passes; `docs/plans/plans.md` has a row per archived plan.
No build or suite surface is touched.

**⚠ Traps.** ⚠ Do not archive `PLAN-M5-campaign.md` without checking its checklist. Its own scope
paragraph carries an unresolved `<TODO: re-verify each absorbed BL still-open ...>`, so it is not
obviously finished merely because the polish plan that followed it is. ⚠ `BL-506`'s closure record
lives in `43b47435`'s message body, so the deletion commit should cite that hash rather than restate
the outcome, per the docs-state-what-is rule.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

## A2 ☑ An AI pilot sees an enemy aircraft's own turret as a target beside the aircraft

**Goal.** An enemy aircraft carrying a defensive turret presents one target to an AI pilot, not two;
a world emplacement still presents one.

**Evidence (confidence: traced).** `BL-507`, found by G83. `FlightController.AddRankedNonAircraft`
walks the gunner scan's turrets and files them into the AI's ranked pool with no discriminator on
`TurretController.Site`, so a carried turret is offered as a target in its own right and one
silhouette carries two entries. The player's own `TargetPool.Rebuild` guards exactly this, and the
`carried-turrets` suite pins that guard, so the rule is known and the AI path does not apply it. The
defect predates G83, since the player's own mounts were always in that pool, but `43b47435` gave
every AI Kestrel, Avenger, Brigand, Firebrand and Balmoral a crewed rear mount, which multiplies how
often it is reached.

**Approach.** Give the AI path the same `Site` discriminator the player's pool already uses. This is
a guard and its test arm, not new turret code; the shape to copy is `TargetPool.Rebuild`.

**Model recommendation.** medium. A small scoped guard with an existing correct implementation to
mirror, but the discriminator's two cases must be got right or the AI stops attacking ground
emplacements.

**Verify.** A suite arm asserting that an AI pilot's ranked pool holds one entry for a turret-carrying
enemy aircraft and still holds an entry for a world emplacement in the same scene. Take the
two-entry baseline first: an unchanged count is not evidence unless it has been seen able to fail.
Then the `carried-turrets` suite and the golden set, since `c1-targeting-hud` was re-pinned by G83
and is the shot most likely to move again.

**⚠ Traps.** ⚠ The two kinds of turret share one controller and only `Site` tells them apart, so a
fix that drops all turrets from the AI pool would stop AI aircraft attacking ground emplacements.
That is a different behaviour and not this. ⚠ `c1-targeting-hud`'s `exercises` field is rewritten on
a re-pin, never appended to.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

## A3 ☑ A campaign spawn's talker and constitution ratings never reach `AiVoiceRuntime`

**Goal.** A campaign pilot chatters according to the `talker` and constitution ratings its own block
authors, so an ace rated 9 is audibly different from a mook rated 1.

**Evidence (confidence: traced).** `BL-497`, found by G75. `CampaignDirector` passes null where the
ratings would go, so every campaign pilot is equally talkative regardless of its block. The ratings
are parsed and carried as far as the plan, so the gap is the last hop rather than the decode.

**Approach.** Thread the spawn's ratings through to the voice runtime the way the roster now threads
the pilot name (G75 landed that path, so the plumbing shape exists and is the one to follow).

**Model recommendation.** medium. Mechanical threading along a path that landed one field ago; the
judgement is only in choosing the same seam rather than a parallel one.

**Verify.** No existing voice suite spawned a campaign block through `CampaignDirector.BuildRoster`
with a real `AiVoiceRuntime` behind it, so a new arm, `roster-voice-ratings`, was the smallest way to
settle the question: it builds C5/M01's roster (no chapter world needed) and reads the resolved
`Speaker.TalkerChance`/`ConstitutionChance` for `autogyro_1`, the one shipped block that authors both
slots (7, 8) and an accent. Failing baseline confirmed first: both chances read the session's flat
rating-5 fallback (0.639 / 0.683). After widening `RegisterVoice` to carry one override per stat and
threading `spawn.Skills.Talker`/`spawn.Skills.Constitution` through the hand-off, talker reads its own
rating (7: 0.794) and constitution reads its own, independent rating (8: 0.883), and `campaign-roster`,
`roster-spawn-names`, `campaign-bomber-formation`, `voice-runtime`, `ai-voice` and `instant-action`
stay green. Audibly, the ratings' effect is still judged in C12's sortie.

**⚠ Traps.** ⚠ `docs/formats/combat-voice.md` carried a stale note claiming this already worked,
corrected by G75; check the doc's current claim rather than an older reading. ⚠ A pilot whose accent
resolves to a VO id with no WAVs stays silent whatever its `talker` rating is, so this fix will not
be visible on those eight named aces, and their silence is not a failed verification.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical.

## A4 ❌ CM02's second Peacemaker squad is awake from the start and attacks the Pandora

**Goal.** CM02's second Peacemaker squad, the one carrying the ace, is asleep at mission start,
wakes when its authored gate opens, and comes for the player.

**Evidence (confidence: traced).** `BL-499`, reported at the controls against the original. Both
halves are authored. `OBJECTIVE8` is dormant and carries
`WAKEUP_ENEMIES [britpeace_7, britpeace_8, britpeace_9]`; completing it also wakes `OBJECTIVE9` (the
`snd_HA5Wave2` Winthrop chain), `OBJECTIVE10` (the SECONDARY that kills the ace) and `OBJECTIVE68`
(`SET_AI_NET M5Escort` on the same three, two seconds later). The gate is `OBJECTIVE5`'s
`DEDG [1, 0]`, and group 1 is `britpeace_1/2/3`, the first Peacemaker squad; on completion it naps
`OBJECTIVE8` awake after 15 s. Targeting is authored too: `britpeace_8` and `britpeace_9` carry
`rating_biases [piratezep, -0.8] [player, 1.0]`, and `britpeace_7` carries `[player, 1.0]` alone.

**Approach.** Two questions in order. First, whether `WAKEUP_ENEMIES` reaches an aircraft roster
block at all, since a squad that spawns at mission start has had its dormancy dropped rather than
its targeting broken. Only then, whether those biases reach the pick for a woken spawn, which
`roster-spawn-names` already proves they do for a spawn present from the start.

**Model recommendation.** high. Two coupled subsystems (the objective graph's wake path and the AI
target pick) and a reported symptom whose stated cause is the second when the evidence points at the
first; ordering the investigation wrongly here costs a session.

**Verify.** A headless arm on CM02's own data asserting the three `britpeace_7/8/9` spawns are absent
at t=0, present after `OBJECTIVE5` completes plus its 15 s nap, and that their bias table resolves
the player above the Pandora. ⚠ `ObjectiveGraph.ScanForCompletion` resolves one objective per tick
round-robin, so step several seconds after the notify rather than a single tick.

**⚠ Traps.** ⚠ The user's recollection is "after 2 Balmoral kills" and the authored gate is the first
Peacemaker squad being wiped out; both may be true of one playthrough, so treat the recollection as
the lead and the `DEDG` as the specification. ⚠ Group 5 down to one IS a real gate in this mission
(`OBJECTIVE20`, `55`, `63`), which is what makes the two easy to conflate. ⚠ Do not hand the squad a
hardcoded player target: the bias table expresses this and it already ships.

**Closed as already answered; no code changed.** Neither of the two questions holds a defect, and
the item was answered in full before this plan was written. The first question answers no:
`WAKEUP_ENEMIES` reaches an aircraft roster block end to end, `aiv` slot 21 into
`RosterSpawnPlan.Inert` (`CSVM/src/Session/CampaignRoster.cs:201`), carried by `SpawnFor` (`:256`),
applied at `CSVM/src/Session/AiFlightAssembler.cs:115` through `CSVM/src/Flight/FlightControllerBuild.cs:79`,
and re-homed by name in `CampaignDirector.WakeupEnemies` (`CSVM/src/Session/CampaignDirector.cs:666-694`).
The second answers no as well: `britpeace_8`'s authored `[player, 1.0]` moves its live pick off a
nearer same-side candidate onto the human. What the report saw was a third mechanism, `SET_AI_NET`
being a named no-op, so `OBJECTIVE68` never moved the woken three onto `M5Escort` and they flew
their roster block's route. That was filed as `BL-500` and landed by `PLAN-M5-polish.md` G80; the
`piratezep` arm of the same bias list is unreachable in this mission for a reason `BL-407` settled
(an unauthored record stays neutral and neutral is nobody's target). The plan's own re-verification
missed this because G78 and G80 name `BL-500` rather than `BL-499` in their messages, so
`git log --grep=BL-499` saw only the filing commit. The `BL-499` entry is deleted from
`backlog.md`; there is no follow-up to rewrite, both threads it left being closed.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical. In the lane, on the unchanged build:
`--run-tests=campaign-squad-wakeup` PASS (1 passed, 0 failed, 0 skipped, errors clean), driving
C3/M05's own roster through the session's `FlightRoster` and its own objective graph:
`britpeace_7/8/9` inert and out of play at t=0, in the world 15.03 s after group 1 goes down, all
three on `M5Escort#21` once `OBJECTIVE68` has fired, and the pick moving from `devastator_1` at
400 m to the human at 900 m when the block's own list is armed. `--run-tests=campaign-set-ai-net`
PASS beside it. The only edits are this plan, the `backlog.md` deletion, and one stale sentence in
`docs/architecture.md` that still called the three `SET_AI_*` verbs named no-ops.

## A5 ☑ The campaign wingman ends up high and far behind

**Goal.** The campaign wingman reads at the controls as flying with the player out of the intro,
rather than as having spawned above the island flying towards them.

**Evidence (confidence: direction-sound; the measurement is real, what the escort should do is not
settled).** `BL-457`, seen at the controls: "in the original the wingman spawns beside me. here he
spawns above the island flying towards me." The spawn is correct (see ⚠ table row 1). Traced from
the intro's end, separation runs 117 m, 95 m, 102 m, 133 m at t=0/1/3/5 s, then 289 m at 10 s, 597 m
at 20 s and 838 m at 26 s, ending 130 m above the player. One contributing cause is fixed and does
not account for that magnitude: the decoded 250 mph desired-speed ceiling (`AiControlLaw.SpeedCeiling`,
`def+0x1e8`, written by `FUN_00478a00` at `0x478d52`) sits 1.24 m/s below the Devastator's own
113 m/s cruise, so an escort holds no closure margin; `AiPilot.FlyEscort` now lifts it through
`AiControlLaw.StationCeiling`. ⚠ **Merging main's flight model made this materially worse and
`wingman-station` is red on the merged tree**: same suite file, same seed, `player_pfighter`, our own
plant read mean 296 m / worst 702 m without the ceiling lift and 256 m / 590 m with it; main's plant
reads mean 1096 m without the lift and 415 m with it. The `player_bhawk` arm moves the same way
(1023 m to 267 m). Two conclusions: the `StationCeiling` lift is doing more work under main's plant,
not less (it collapses far-field time from 26.8 % of steps to 3.0 %), so the open question of whether
to keep it resolves toward keeping it; and the remaining defect has a second, larger contributor in
`FlightModel` itself.

**Approach.** Look at the **hand-off at the intro's end**, not the cruise. The escort law never
leaves the formation state once joined (`AiEscort.cs:233-235`, and a joined escort 5 km out still
reads `Station` in `wingman-station`), so the 200 m overfly the report describes belongs to a wingman
that never joined. The join gate is a range AND a speed, `< 700 m` and `> 20.576 m/s`, so establish
what the wingman's speed and range actually are on the first frame it is stepped after the cutscene.
One untested lead remains: `WithAiSpawnJitter` scales a spawned wingman's `fd_speed` by up to 5 %
(`PlaneStats.cs:95`, applied at `FlightRoster.cs:63`), worth up to 5.6 m/s on the same airframe,
which the suite legs do not apply.

**Model recommendation.** high. The item has four retired leads, a red suite, a contributor in a
subsystem this plan will not open, and a symptom that has already misdirected one session.

**Verify.** `wingman-station`'s `[flown]` leg on `player_pfighter`, mean column, against the numbers
above; the three currently-failing gates green. Then the sortie in C12, which is what the report came
from and the only instrument that judges "reads as flying with me".

**⚠ Traps.** ⚠ **Measure on `player_pfighter`.** The Devastator is 113 m/s against the ceiling's
111.76; the Bloodhawk is 135 m/s, which overstates the ceiling's share about eighteenfold, and a
number quoted off it is not a statement about the campaign. ⚠ **Trust the `mean` column, not
`worst`.** The sampled trace never exceeds 470 m over a 120 s run while `worst` reads 3797 m, so that
excursion is a between-samples transient and possibly a position discontinuity (`INSTR-18`'s
altitude-cap teleport is a candidate); it needs a finer trace before anyone tunes against it. ⚠ The
`1.50` lever in the trace is the WINGMAN's, capped by `AiLawParams.Wingman`'s own `SpeedCap`, not the
leader's. ⚠ The far-field plant is not the original's answer to a fast leader and must not be
re-derived as one. ⚠ The two SCRIPTED `wingman-station` legs fly a cruise lever, a speed any escort
matches, so their leashes are not evidence here; the `[flown]` leg is. ⚠ Do not re-tune the decoded
station offsets, the 700 m join gate, or `SpeedCeiling`. ⚠ Do not fold `BL-469` (the nitro
asymmetry) into this; it is a different pairing and is not in this plan. ⚠ See the ⚠ table above:
nitro, the airframe mismatch, the far-field branch and the `Joining` re-entry are all dead.

**Verified.** `wingman-station` is GREEN on this tree at the baseline, before any change: `[flown
player_pfighter]` reads mean 270 m, settled 199 m, worst 523 m against the 700 m leash, and the
far-field plant is entered on 0 of 7199 steps. The Evidence above describes an earlier tree; the
three gates it calls failing are not failing now, and no number in this item should be quoted off
it. The hand-off itself is not a join problem: on the first frame the wingman is stepped after
C3/M01's intro it reads range 117.6 m and speed 53.6 m/s against the `< 700 m` / `> 20.576 m/s`
gate, and is in `Station` on the next frame; nothing runs during the intro at all, because
`GameSession.DriveSimSteps` returns while `CutsceneController.HoldsWorld`. The untested lead HELD
and is landed: `WithAiSpawnJitter` was reaching the wingman, which the original's own gate excludes
(vehicle classes `jet` and `heli` only, and the `w*` family authors `mode wingman`), and C3/M01's
`wingman_1` spawned at `fd 108.5` against its leader's 113.0. It now spawns at 113.0 and holds
94–98 m out of the intro. What remains is a break-off, not a drift: nine seconds in, at 109 m over
the island, the mode machine enters `avoid crash`, whose climb-out runs ahead of the escort in
`AiPilot.Next`'s fork, and the wingman ends 128 m above and 302 m behind before recovering. That is
left for the sortie and for its own item rather than re-tuned here, because the fork order is
decoded while the climb-out's geometry is a named invention. Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: units 2406/2406, engine suites 135/135 with errors clean, goldens 16/16 hash-identical.

## A6 ☑ Crashing the player's aircraft does not end a campaign mission

**Goal.** Losing the aircraft loses the mission, so the campaign can be played as a game rather than
debugged.

**Evidence (confidence: lead-only for the fix; traced for the absence).** `BL-491`, reported at the
controls. A campaign mission ends through `ObjectiveGraph.End`, and its three endings are the
authored end, an `INSTANTLOSS` objective, and the countdown expiring (`ObjectiveGraph.cs:354`); none
of them is the player dying, and nothing in `CampaignDirector` watches the player's own crash state.
`GameSession` subscribes to the graph's `MissionEnded` alone. What a player death should map to is
**not decoded**: the graph's own three endings are all authored and a fourth is not.

**Approach.** ⚠ **Start by putting the deferral back to the user.** This entry is
`[Deferred: useful while debugging]` by the user's own decision, recorded with its date in
`git log --grep=BL-491`, on the grounds that flying on after a crash is convenient while the campaign
is being built. Its own text says to re-open it when the campaign is judged as a game rather than
debugged, and says the reason decays. This plan is that judgement, which is why the item is here, but
lifting a decision is the user's call and not the plan's. If lifted: whatever ends the mission on a
player death has to name which of the original's endings it is, so the first work is decode, not
code. If not lifted, close this item as deferred and say so.

**Model recommendation.** high. The first half is a decode question against the exe with no known
answer, and the blast radius (what a lost mission writes, and what a retry starts from) reaches
persistence.

**Verify.** The deferral is lifted, and the decode named a fourth ending rather than one of the
three: a player death closes a gate that stops the objectives runtime dead, and the mission is over
where the wreck comes to rest, with the outcome read off the won flag alone
(`docs/formats/objectives.md`, "Win and loss"). The `campaign-player-death` suite is the check,
over C3/M01's built world and shipped script, that mission being one of the four that author no
loss at all so a Lost outcome there can only be the death. Four legs: the crash ends the mission
lost, hands the player back to the cabin and commits nothing to the persist log; `--no-crash-loss`
leaves the same crash flying; a mission nobody crashes in runs on; and an aircraft the under-map
backstop teleported ends nothing. Three off-engine tests in `CSVM.Tests/ObjectiveGraphTests.cs` pin
the graph's own rule, including that a mission already won when the player dies stays won. The
baseline is the same suite with the death hook neutralised, which reproduces today's behaviour: the
crashed leg reads still-running and the suite fails.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

**⚠ Traps.** ⚠ `FlightController.UnderMapY` teleports an aircraft that goes below the map WITHOUT
setting a crash flag and without logging (`docs/verification.md` INSTR-22 records what that cost
once), so "the player crashed" is not a state that can be read casually. ⚠ `BL-486`'s persist-log
gate means a lost mission writes no world state, so making crashes lose changes what a retry starts
from; decide that explicitly rather than discovering it. ⚠ Do not invent a fourth ending and present
it as decoded.

## A13 ◐ The mode machine's `avoid crash` pre-empts a joined escort

**Goal.** A campaign wingman low over terrain does what the original's escort does when its
avoid-crash test fires, rather than flying an invented 1000 m climb-out that leaves it 128 m
above and 302 m behind the player.

**Evidence (confidence: traced for the mechanism, lead-only for the fix).** `BL-509`, found by A5.
On C3/M01 with both aircraft on `player_pfighter` the wingman joins on the first stepped frame and
holds 94-98 m for nine seconds; then `avoid crash` arms at 109 m over the island and, because that
mode runs ahead of the escort in `AiPilot.Next`'s fork (`AiPilot.cs:269-272`), the escort law is
dropped for the climb-out. The fork ORDER is decoded; the climb-out's geometry inside the middle
altitude band is a named invention (`docs/architecture.md` on `AiModeMachine.cs`).

**Approach.** Decode first: what the original's mode 4 does when its avoid-crash test fires with a
live leader (the altitude band it tests, the pull-up it flies, whether it returns to station or
re-joins), then re-shape the climb-out from that. Added by user decision after A5; the user chose
a plan item over leaving it to the sortie.

**Model recommendation.** high. A decode against the exe with the escort and the mode machine
both in play, and an invented constant to retire.

**Verify.** The decode is landed and the invented constants it contradicts are retired, measured on
a `--campaign=` C3/M01 trace (`player_pfighter`, both aircraft airframe 5, a per-second print in
`AiPilot.Next` removed afterwards): the second and third break-offs are gone and the recovery is
monotonic, but the wingman is not inside 150 m through the low pass and the altitude excursion grew,
so the item is partial. `wingman` 2/2, `ai` 36/36, `dotnet test` 2407/2407, comment caps clean.
`wingman-station` cannot see this (INSTR-25).

**⚠ Traps.** ⚠ Do not suppress `avoid crash` for an escort as a shortcut; the short-circuit is the
original's. ⚠ Measure on `player_pfighter`. ⚠ Everything `BL-457`'s traps retire stays retired.
⚠ A `--campaign=<profile>:N` run WRITES BACK to that profile's `latest` mission record; run a copy.

**Verified.** The escort's avoid-crash arm is decoded whole (`FUN_0041e760` `0x0041e7bd`–`0x0041e814`:
own position with `DAT_00603464` = 1000.0 added to Y, X and Z untouched, the zero aim velocity
`0x0075d1b8`, the wingman table `0x0061fb28`, `emergency` set, and a `return` that never touches the
escort state at `+0xd8`, so it resumes station and never re-joins). Neither law carries a lateral
term, so `AiPilot.ClimbOutBreakM` is retired for an escorting pilot and kept for a netted one, whose
furball measurement is the only place it was ever taken; the invented second, deck-slanted probe ray
is retired outright, the original casting one. Measured, C3/M01, per second: baseline holds 94–98 m
to 8.1 s, arms on `player1/airframe` at a 511 m reach, peaks 303.0 m / 128.0 m above at 14.2 s,
re-breaks on the deck ray at 24.3 s and ends 354.3 m / 128.9 m; after, one break-off on the same
obstacle, peak 294.7 m / 174.8 m at 13–14 s, then monotonic to 236.4 m / 50.5 m and still closing.
The item is ◐ because the excursion is reshaped, not removed: the wingman is not inside 150 m, and
the vertical excursion is 47 m larger by construction. What remains is not decodable from the
climb-out at all — the wingman sits 94.5 m DEAD ASTERN because `SpeedCeiling` caps it below the
leader, which is what puts the leader on its ray, and re-tuning that is out of this plan by its own
traps. Full `RunTests.ps1` battery on the merged plan tree at `bb2d1a81`: units 2407/2407, engine suites 135/135 with errors clean, goldens 16/16 hash-identical.

---

# Wave B — the intro and the swap

## B7 ☑ A bootstrap `AT_NODE` pose lands at the world origin

**Goal.** A pose run during the animation bootstrap lands where the same pose lands a frame later,
and the per-world error rate goes to zero.

**Evidence (confidence: traced).** `BL-484`. `PoseChannel.PoseAtNode` (`PoseChannel.cs:383,386,391`)
takes the host's `GlobalTransform` and assigns the target's `GlobalBasis`/`GlobalPosition`. During
the bootstrap the world root is not yet parented, a condition `AnimRuntime.WorldTransform`'s own
comment records and works around for sound positions. The pose path has no such fallback, so Godot's
`!is_inside_tree()` guard fires and both reads return `Transform3D()`. Reached through
`AnimRuntime.Start` into `SequenceRunner.Advance` into `HandleTranslateState`, and measured at
exactly **4 per world built with `CutsceneRoots`**, from `landings-approach-trigger` and
`cutscene-letterbox`.

**Approach.** Give `PoseAtNode` the composition `AnimRuntime.WorldTransform` already implements, and
write the target's LOCAL transform when it is out of tree.

**Model recommendation.** medium. The correct implementation already exists in a sibling method; the
judgement is the local/global branch and the framing check below.

**Verify.** The 4-per-world count goes to 0 on both `landings-approach-trigger` and
`cutscene-letterbox`, and the allowlist entry and its cap are removed rather than lowered. Then
`cutscene-letterbox` plus a `--campaign=` shot, because framing is what actually moves.

**⚠ Traps.** ⚠ The letterbox bars and `camera1` are both world roots posed through this path, so a
change here moves cutscene framing and must be judged against `cutscene-letterbox` and a
`--campaign=` shot rather than against the error count alone. ⚠ Do not raise the allowlist cap again
instead of fixing this: the cap is the rate times the number of suites, so a third such suite is
meant to trip it.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

## B8 ☑ The intro cutscene stages no aircraft

**Goal.** The chapter intro cutscene stages the two aircraft it animates, so it plays over aeroplanes
rather than over an empty sky.

**Evidence (confidence: traced; decoded, so this is scoped work and no longer an open question).**
`BL-482`. A definition's symbol table addresses two node tables. A chapter node's `ptr` is its
position in that chapter's `nodes.json`; a node from the shared aircraft archive is its position in
`planes/nodes.json` plus a base, and that base is the chapter's own node count rounded up to the
next multiple of 2500 (holds for all eight chapters: C2/C2B 5000, C1/C1B/C1C/C3 7500, C4 10000,
C5 12500, and for all nine cross-archive pointers in C3's intro). So `camera1-generic_intro.json`'s
ptr 8918 is aircraft-archive node 1418, named `player`, a parentless `Object3d` whose one child
`player_pfighter` the airframe name table at `0x00620cc0` identifies as the Devastator's player-model
node. **`player` is the flown aircraft**, established by decode rather than by the name:
`FUN_004d0280(7, "player")` resolves it by string comparison over node table 7, `FUN_0042e5e0`
switches it off around a render pass, and mission setup passes the same literal with the player's
loadout record (`FUN_004136e0` into `FUN_00414f40`). The node ships wrapping a Devastator because
that is what sat in the slot when the archive was built, not because the intro is about one. Two
limits are stated rather than closed: `FUN_0041a320`'s use of the pair was not read, and no exe site
computing the 2500 rounding was traced.

**Approach.** The intro stages two aircraft: `piratefighter` (activated, `wing_lights_blink` and
`spinprops`, reparented under `piratezep`, flown by `gi_pfighter1`/`gi_pfighter2`) and `player`
(activated with `healthy` on and `cockpit1` off, flown by `gi_player1`, launched by
`gi_playerdrop`/`gi_player2` with `snd_droplaunch`). Building it needs aircraft-archive subtrees in
the animation runtime's node table at the bootstrap, before the world root is in the scene; needs the
flown `FlightController`'s model posed by the runtime while it is `Held`/`Inert`, which is the state
callback 11 puts it in; and needs a second Devastator staged as an AI-less prop. **Do B7 first**, because posing at
the bootstrap is exactly the broken path.

**Model recommendation.** max. It reaches the world build, the flight roster and the anim runtime at
once, it rests on a decode with two stated open limits, and a wrong reading puts a wrong aircraft in
every chapter's intro.

**Verify.** A `--campaign=` capture of the intro on more than one chapter showing both aircraft
staged and the launch cue firing, plus the full 8-chapter `--freecam --chapter=<X>` regression (zero
errors, node counts moving only where this adds coverage). Take the node-count baseline first.

**⚠ Traps.** ⚠ **Do not relax `AnimRuntime.cs:2954-2966`.** `player` drops because the node is
missing, not because the guard refuses to name-match, and relaxing it would bind `player` to any
unrelated node sharing the name. ⚠ A wrong reading of which airframe fills the slot puts a wrong
aircraft in every chapter's intro, and the Devastator in the slot is an artefact of when the archive
was built. ⚠ The two open limits above are limits, not invitations to assume; if the 2500 rounding
has to be relied on beyond the eight measured chapters, say so.

*Cross-refs:* `docs/formats/anim-definitions/cutscenes.md` ("`player`, and the two pointer spaces a
definition addresses") carries the decode; `BL-470` and `BL-471` were closed by the work that filed
this.

**Landed.** `Mech3/AircraftStage.cs` builds both aircraft under the world root before the animation
bind, for a mission whose start-anims name an intro and for no other session. `piratefighter` comes
off the aircraft archive on its own `SceneBuilder`, switched off until `gi_pfighter1` activates it;
`player` is a bodiless marker, because the aeroplane it stands for is the one the pilot flies. Every
staged node's stamped gamez index is rebased by `PointerBaseOf` (`(count / 2500 + 1) * 2500`), which
is what makes the compiled intro's symbol table bind the names instead of claiming a symbol with a
null binding. The pose half is `CutsceneController.StagePlayerAircraft` into the new
`FlightController.StageAt`: while callback 11's out-of-flight state holds and the marker is active,
the flown airframe is drawn on the marker's world pose; the handoff hands back the pose it held when
the staging began, so the mission still starts from its authored spawn rather than from the drop's
end pose. The name guard was not relaxed. The 2500 rounding is relied on only for the eight measured
chapters, and `docs/formats/anim-definitions/cutscenes.md` now says so, including that an exact
multiple of 2500 does not occur in this install and its rounding is therefore undecided.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical. In the lane: `--run-tests=intro-aircraft-stage` PASS
(1 passed, 0 failed, errors clean) over C3/M01's built world, base 7500 over 5408 chapter nodes,
`player` ptr 8918 and `piratefighter` ptr 9824 both answered by the staged node in the runtime's
node table, the prop posed 125.7 m off `piratezep` at 15.9 s and reparented under it, the marker
posed 63.5 m off it at 28.3 s, the flown airframe tracking the marker from the same instant, three
one-shots over the window. Neutralising the archive path in `TestHarness` makes it fail on the first
check (METHOD-9). `cutscene-letterbox`, `campaign-cutscene`, `landings-approach-trigger`,
`campaign-airframe-swap` and `campaign-objectives-hud` PASS beside it; units 2401/2401 with the
suite-count assertion moved 130 → 131. The 8-chapter `--freecam` regression is unchanged in every
count and bit-identical in all 8 screenshots, and the 16 goldens are 16/16 hash-identical with
`manifest.json` unmodified in the tree (GOLD-9). `--campaign=` captures on two chapters, against a
neutralised-build before-image that reproduces HEAD's own counts: C3/M01 shows the Devastator prop
flying beside the airship and the pilot's own Bloodhawk in the launch bay with the doors open, C5/M01
shows the prop alongside its airship at night, and both read as an empty sky on the before-image.

**Note for the next reader.** The `⚠ Traps` above cite `AnimRuntime.cs:2954-2966` for the name
guard; that guard is the claimed-but-unbuilt branch in `AnimRuntime.Targets`, which B7's edit moved.

## B9 ☑ An airframe swap leaves the captured aircraft flying, and never hands the outgoing one over

**Goal.** The mid-mission airframe swap hides the aircraft the capture animation belongs to, carries
that aircraft's damage into the player's new hull, and hands the player's outgoing aeroplane to
`wingman_4`.

**Evidence (confidence: traced).** `BL-503`, found by G76, which wired the swap itself and reported
this rather than folding it in. The original's 967 case does two more things than the swap. It hides
the aircraft the capture animation belongs to and scales the new airframe's four hull sections by
that aircraft's own armour and structure fractions, so a Balmoral shot half to pieces is the one the
player inherits. And it hands the aircraft the player just left to `wingman_4`: the exe resolves that
name only in `c3`/`m05` and `c4`/`m04`, gives a record of that name the player's own aircraft type
and livery, places it 100 m off the nose at 45° with the outgoing airframe's armour and structure
sums, and reveals it. In CM02 that is the wingman flying off in your old plane while you fly the
Balmoral.

**Approach.** The anim's root vehicle has to be reachable from the callback, which needs the
definition's root node name plumbed through `CallbackHost`; it passes only the anim name today, and
that is the whole of what blocks the first half.

**Model recommendation.** high. Two behaviours keyed on hardcoded exe strings with no data trigger,
plus a damage-carry that changes difficulty if got wrong.

**Verify.** The existing `campaign-airframe-swap` suite CAN assert the hand-over headless, and was
extended rather than replaced: it already builds C3/M05's own world and drives the mission's own
authored 967 through the real host. Two arms were added on that real data. A pure one over the
mission's `aiv` blocks, which reads `wingman_4` as authored deactivated and flying
`player_pfighter` on its own def and the player's `player_bhawk` under the hand-over, and asserts
the mission gate opens for C3/M05 and not for C3/M01. And a driven one that stages the capture
aircraft and the hand-over block, damages the capture, raises 967 with the definition's real root
name, and reads back: the captured aircraft hidden, the player's new hull carrying its armour and
structure fractions, and `wingman_4` revealed 100 m off the old nose at −45° on the player's own
heading with the sums measured off the aeroplane it was given. The able-to-fail control is
recorded: with the two new halves of `FlightRoster.RunSwap` bypassed, those seven checks fail and
nothing else does. The visible half is judged in C12's sortie: fly CM02's wing walk to the capture
and watch for three things at the cut back to flight. The Balmoral you inherit should read damaged
on the HUD's DMG line rather than pristine, by roughly what the Balmoral you shot at had left. The
Balmoral the capture animation belongs to should be gone from the sky, not still flying beside you.
And your old Bloodhawk should be visible about 100 m ahead and to the right, in your own livery,
flying your heading rather than turning across you.

**⚠ Traps.** ⚠ Neither behaviour is asked for by anything in the data. Both are keyed on chapter and
mission strings inside the exe, so nothing in the shipped files will tell a reader they should
happen, and a search of the data for a trigger comes back empty. **Do not read that emptiness as the
behaviour not existing.** ⚠ The captured aircraft's damage carries into the player's hull, so wiring
the handover without the scaling gives the player a pristine Balmoral and makes the ending easier
than the original's. ⚠ Only 967 places `wingman_4`; 966 hands over the sums and reveals it where it
stands. ⚠ The original writes the handed-over sums with no cap because its `wingman_4` flies the
player's own airframe; CSVM caps them at the receiver's own maxima, because one airframe's pools
read as a zone sum on a human rig and as the AI def's authored pair on an AI one.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

---

# Wave C — offered but inert, and the red suite

## C10 ☑ The auto-land the approach table offers has no button

**Goal.** When the approach table offers an auto-land, the player is prompted and can take it.

**Evidence (confidence: traced).** `BL-460`. Every chapter's `landings.zrd` carries one `auto` row, a
500 m sphere around `pz_auto_land` with no attitude cone and no speed band. The original does not
start the animation on it: `FUN_0045df60` raises `DAT_00719109`, and `FUN_0045e120` turns that into
an on-screen prompt (message `0xb5`, or `0xb6` when the binding is a pad button, over key binding
`0x6a`) that the player then presses to start the same hookup the manual row starts. CSVM decodes and
ticks the row (`LandingApproachRuntime.AutoLandOffered` goes true exactly when the original lights
the prompt), but nothing draws the prompt or reads a key off it, so the row is observable and inert.

**Approach.** A HUD line off `AutoLandOffered` plus a binding that calls `Play` on the row's
animation, which is the same call the manual row already makes.

**Model recommendation.** medium. The runtime half is already correct and tested; this is a HUD line
and an input binding, with one double-fire guard to respect.

**Verify.** A `--campaign=` run that enters the sphere, shows the prompt, and lands on the button;
plus a check that the manual row still fires exactly once when both are reachable.

**⚠ Traps.** ⚠ The manual and auto rows name the SAME animation, so a session that starts it from
both would double-fire; the button check sits inside `LandingApproachRuntime.Tick()`'s own top-level
cutscene guard, the same one the manual row's return already relies on, so nothing outside that
method has to re-derive it. ⚠ The prompt's message ids are `langui` ids CSVM has not resolved to
their text. The extraction pipeline that reads `langui.dll`'s STRINGTABLE already exists
(`ExtractRof.ps1`, `UiStrings.cs`), so this is not the missing pipeline `BL-427` once was: that entry
is closed, its premise already stale, and the gap here is narrower, that ids `0xb5`/`0xb6` are not
present in today's extracted `langui` table under that numbering. **Decision: ship a plain-English
placeholder** (`AUTO-LAND AVAILABLE` plus the bound key), marked in `FlightHud.cs` at its declaration
as a stand-in for the unresolved original string, rather than pulling in a fresh string-mapping
investigation here.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

## C11 ☑ `campaign-objectives-hud` fails on C4 and C5 on its wake-cue check

**Goal.** `campaign-objectives-hud` is green on all five extracted chapters, with the wake-cue
assertion intact.

**Evidence (confidence: lead-only for the cause; traced for the localisation).** `BL-483`, found
while fixing the same suite's completion driver, which now passes on C1, C2 and C3. What fails on C4
and C5 is `DriveWakeCue`'s assertion that at least one `WAKEUP_SOUND_GROUP` the mission authors
started a real one-shot player; the row-marking half of the suite passes on both. Confirmed
pre-existing rather than caused by the driver work (METHOD-8): `CampaignHudSuites.cs` was reverted to
its committed state, rebuilt, and both failures reproduced identically.

**Approach.** Trace the wake cue on those two chapters. The question is whether the mission authors a
group our wake path never reaches, or whether the one-shot is started somewhere the suite does not
look. The fix shape is unknown until that is answered, and **this item may correctly end in a
disproof** (the suite looking in the wrong place) rather than in engine code.

**Model recommendation.** high. An investigation with two candidate causes in different subsystems
and an explicit prohibition on the easy way out.

**Verify.** The suite green on C1 through C5 with the wake-cue assertion unchanged in strength. If
the answer is that the suite looked in the wrong place, the change is to the suite and the assertion
must end up stronger, not weaker.

**⚠ Traps.** ⚠ **Do not weaken the wake-cue assertion to make two chapters green**, which is the same
move `BL-481` forbade for the readout check. ⚠ C6 and beyond have no extracted data, so a chapter
sweep stops at C5. ⚠ `docs/verification.md` METHOD-8 and DIAG-15 apply.

**Outcome: neither candidate cause. The suite failed vacuously.** `c4/m01` and `c5/m01` author not
one `WAKEUP_SOUND_GROUP` between them (0 occurrences in either `objectives.zrd.json`, against 18 and
33 `COMPLETED_SOUND_GROUP`), so `DriveWakeCue`'s loop body never ran and it asserted `false` over an
empty candidate set. Those chapters' objective audio rides the completion surface and plays there:
C4's one-shots run 1 to 7 on `snd_RM1Lastgun`, C5's 1 to 17 on `snd_c5-MN-m1_Zachary_22`. The fix is
in the suite, and the assertion is stronger rather than weaker: the wake check is unchanged where a
mission authors the directive, gated on the authored count where it does not, and a new check that
runs on all five chapters requires one of the mission's own objective sound groups, whichever
surface it chose, to reach a real player. `docs/verification.md` DIAG-22 records the rule.

**Verified.** Full `RunTests.ps1` battery on the merged plan tree at `fbe97195`: build clean, units 2406/2406, engine suites 135/135 with engine errors clean, goldens 16/16 hash-identical

## C12 ☑ Fly a campaign mission end to end: the Wave A/B/C sortie, closing `BL-458`

**Goal.** One campaign mission is flown to its end at the controls, judging this plan's visible items
together, and `BL-458`'s secondary is seen to complete.

**Evidence (confidence: traced for the code, open for the sighting).** `BL-458`. A campaign mission's
danger zones are the same `dzpathN` gate geometry `--stunt` reads, authored from a different surface:
no `ia.json` `dzones` list, but the mission's own `objectives.zrd` names `dzpathN` directly inside
`DANGER_ZONES_COMPLETED` (C3/M01's `OBJECTIVE3` on `dzpath1`, `OBJECTIVE11` on `dzpath4`), narrowed by
`dzones.zrd`'s `disable` list. `CampaignDangerZones` reads that surface and calls the existing
`CampaignDirector.NotifyDangerZoneCompleted` (`:345`); `Attach`/`Step` wire it off a `WorldInputs.Gamez`
field. Proven against real C3/M01 data (`campaign-danger-zones` suite): gate-crossing completes the
zone, and `OBJECTIVE3`/`OBJECTIVE11` complete off the real notify path. **Still open** until the
secondary is seen to complete in a mission flown at the controls, which is the one thing no suite can
show.

**Approach.** ⚠ **This is a stop, not a task an agent completes.** Hand the build to the user with a
named mission and a list of what to watch: `BL-458`'s secondary completing (C3/M01), the wingman
holding station out of the intro (A5), the intro staging two aircraft (B8), the swap's hand-over and
inherited damage (B9), the auto-land prompt (C10), pilot chatter varying by rating (A3), the wingman breaking off to climb low over the island (A13), and a deliberate crash ending the mission with the debrief reached once the wreck is down (A6). Record
what the sortie reports; findings that are not these items become new `backlog.md` entries with their
own IDs from `New-ItemId.ps1`.
**Sortie brief (the stop).** Build: branch `worktree-m5-polish-2` at its head, `dotnet build CSVM/CSVM.sln`, then launch from the menu with a campaign profile flying the Devastator (`selectedPlane` and `wingmanPlane` both on `player_pfighter`, which is what every A5/A13 number was measured on). Mission one: C3/M01 (the campaign's first mission). Watch, in order: (1) the intro stages the prop Devastator beside the airship and your own aeroplane in the launch bay, and the drop launches from it (B8); (2) out of the intro the wingman sits about 95 m off and stays with you over the island, or breaks off into a vertical climb low over terrain and comes back to station (A13, the open question, `BL-509`); (3) the last objective wants TWO crossings, a fly-by and then the drop-off, before the secondary completes (`BL-458`; the user's reading of the original, which outranks the trap below): if one pass completes both `dzpath1` and `dzpath4` at once, that is a finding, not correct behaviour; (4) pilots chatter unevenly by rating (A3), the eight named aces on dead accents stay silent; (5) fly into the sea once: the objectives stop, and the debrief arrives once the wreck is down (A6; `--no-crash-loss` restores the old behaviour); (6) any AI flying into a hillside, since the retired second probe ray applies to every AI (A13). Mission two: C3/M05 (CM02). Watch: (7) the captured Balmoral disappears at the swap, your new hull reads damaged on the DMG line, and your old aeroplane appears about 100 m ahead-right in your livery flying your heading (B9); (8) inside the `auto` approach sphere the HUD line `AUTO-LAND AVAILABLE` appears and `F9` (left-stick click) starts the hookup once (C10); (9) an AI bomber's rear turret is never a second target beside it, while ground emplacements still are (A2). Findings outside these become new `backlog.md` entries with ids from `New-ItemId.ps1`.
**Flown.** CM01 and CM02 at the controls on the plan branch. Confirmed: the intro stages both
aircraft (B8); the last objective's secondary completes on the second crossing, fly-by then drop-off
(`BL-458` closed); pilot chatter varies (A3); being shot down ends the mission (A6); no AI flew into
terrain after the probe-ray retirement (A13); Jack flies the player's old aeroplane after the swap
(B9); `F9` starts the auto-land (C10); a bomber's turret is never a second target (A2). Findings:
the wingman is about 4 km away after the intro because an aircraft's own realtime tick ignores the
cutscene's world hold (`BL-457` rewritten; `BL-509` unjudged); the drop-off's parachutist is
invisible (`BL-540`); the captured Balmoral is not hidden (`BL-541`), the capture camera sits over
the water showing only the player (`BL-542`), and the captured aircraft should keep its British
livery (`BL-543`); the auto-land prompt is not drawn (`BL-544`); the landing animation lacks the
hook, sits too high and does not fold a Balmoral's wings (`BL-545`). The damage carry-over went
untested (no damage taken).


**Model recommendation.** medium. The agent's work is preparing the build, the watch-list and the
write-up; the instrument is the user.

**Verify.** The user's own report. Their eyes outrank the instruments here by standing rule.

**⚠ Traps.** ⚠ `ObjectiveGraph.ScanForCompletion` resolves one objective per tick round-robin, so a
single step after a notify is not enough to see a completion; this matters for the suite, not the
sortie, but do not re-derive it. ⚠ The suite shows C3's gate pairs sit close enough that one crossing CAN complete both zones;
the user reports the original wants a fly-by and then the drop-off as two crossings, so "both
completed at once" at the controls is a finding to file, not a pass. ⚠ Do not close `BL-458` on the suite alone; the suite already
passes and that is exactly why the item is still open.

---

# Wave D — what the sortie opened

## D14 ☐ The cutscene's world hold does not reach an aircraft's own realtime tick

**Goal.** During a cutscene that holds the world, no aircraft moves, on a realtime clock as well as
on the parent-driven one, so the campaign wingman is beside the player when the intro ends.

**Evidence (confidence: traced).** `BL-457`, seen at the controls on CM01: skipping the intro leaves
the wingman beside the player, playing it puts it about 4 km away. Callback 20's hold is honoured by
`GameSession._PhysicsProcess` and `GameSession.DriveSimSteps`, but `FlightController._PhysicsProcess`
steps the aircraft itself on any realtime tick with no hold check; callback 11 holds only the
player. A probe (`--det`) steps through `DriveSimSteps`, which is why A5's trace read 117 m.

**Approach.** Put the hold where every consumer already reads the clock: a `GameClock` hold that
makes `PhysicsDt` answer zero while a definition owns the session, so every aircraft's own tick
returns the way it does on a parent-driven frame. Do not add a per-class guard.

**Model recommendation.** high. One seam, but it is the clock every physics consumer reads, and the
realtime path has no suite; the verification needs a realtime instrument.

**Verify.** A realtime `--campaign=` run (no `--det`) on CM01 with a wingman-separation print at the
handoff reading under 150 m, then `wingman`, `cutscene` and `campaign` suites green, and the full
battery. ⚠ A `--det` or suite run cannot show this (INSTR-25 and `BL-457`'s traps).

**⚠ Traps.** ⚠ The animation runtime must stay OUTSIDE the hold: the movie is animation. ⚠ Measure in
a realtime session. ⚠ `BL-457`'s retired leads stay retired.

## D15 ☐ The captured Balmoral is not hidden by the swap in a flown session

**Goal.** At CM02's capture the Balmoral the animation belongs to leaves the sky, and the player's
new hull carries its damage.

**Evidence (confidence: traced for the suite, lead-only for the live gap).** `BL-541`. The swap
suite passes on C3/M05's own world; at the controls the captured Balmoral sat in front of the
player. Two candidates: `CallbackHost`'s root name does not resolve to the live aircraft
(`FlightRoster.AiNamed` null, so `CarryCapturedDamage` does nothing), or `Inert = true` does not stop
an aircraft that steps itself on a realtime clock (D14's gap).

**Approach.** Re-measure after D14 in a realtime session with the swap's own log lines
(`airframe swap: '...' hidden`); if the line is absent the root name is the gap, if present the hide
is. Then fix the one that shows. The damage carry-over is untested at the controls and is judged in
D21.

**Model recommendation.** medium.

**Verify.** The log line and the Balmoral gone in a realtime CM02 run; `campaign-airframe-swap`
green.

**⚠ Traps.** ⚠ Do not weaken the suite to match the live run; extend it with a realtime-shaped arm
if the gap is the clock.

## D16 ☐ The captured aircraft keeps its British livery after the capture

**Goal.** After the capture the player flies the Balmoral in the livery it was captured in.

**Evidence (confidence: traced).** `BL-543`, seen at the controls: the rebuilt Balmoral wears the
Fortune Hunters paint. `FlightRoster.RunSwap` rebuilds the rig with the player's own scheme; the
original hands over the captured aircraft's.

**Approach.** `RunSwap` already holds the captured rig (`AiNamed(order.CaptureRoot)`); carry its
`PaintScheme` onto the rebuilt rig alongside its damage.

**Model recommendation.** medium.

**Verify.** A swap-suite check that the rebuilt rig's scheme equals the captured rig's; a CM02 shot.

**⚠ Traps.** ⚠ The hand-over to `wingman_4` keeps the PLAYER's old scheme, which is decoded and
correct; only the player's new hull changes.

## D17 ☐ CM02's capture cutscene camera sits over the water

**Goal.** The capture cutscene shows the Balmoral and the player's wing-walk, framed as authored.

**Evidence (confidence: lead-only).** `BL-542`, seen at the controls: the camera sat directly above
the water showing only the player's aeroplane. The definition poses its camera off a node or an
absolute pose; which, and whether the node it wants is the captured aircraft's own, is not read.

**Approach.** Read the capture definition's camera events (`docs/formats/anim-definitions/cutscenes.md`
census), find the host node, and check whether it resolves in a live session (B8's cross-archive
staging is the precedent for a node that resolves in the data and not in the tree).

**Model recommendation.** high. A decode against the definition with a realtime-only symptom.

**Verify.** A `--campaign=` capture of CM02's capture cutscene with both aircraft in frame, plus
`campaign-cutscene` and `cutscene-letterbox` green.

**⚠ Traps.** ⚠ B7's pose path is the one this camera goes through; do not touch `PoseAtNode` for
one definition.

## D18 ☐ The auto-land prompt is not drawn in a flown session

**Goal.** The player sees the auto-land prompt whenever the button would do something.

**Evidence (confidence: traced for the unit, lead-only for the live gap).** `BL-544`. `F9` worked,
so `AutoLandOffered` was true, but the HUD line never appeared. `ComposeTextLines` is unit-tested;
what is not is whether the text block draws in the player's HUD mode (`DrawsTextBlock`) and whether
`GameSession` feeds the flag to the rig on a realtime clock (the feed sits in `_Process`).

**Approach.** Re-measure after D14; then draw the prompt on the HUD surface the player actually sees
rather than the text block if that is the gap.

**Model recommendation.** medium.

**Verify.** A realtime CM02 run with the prompt on screen (a shot), the unit and
`landings-auto-land-button` green.

**⚠ Traps.** ⚠ The placeholder wording stays until `BL-510` resolves the langui string.

## D19 ☐ The landing animation: no hook, too high, wings not folded

**Goal.** The hookup animation shows the hook deployed, the aeroplane at the trapeze's height, and a
Balmoral's wings folded, as the original does.

**Evidence (confidence: lead-only).** `BL-545`, seen at the controls on CM02's auto-land. Three
separate reads: the hook and the wing fold are per-airframe animated parts, the height is the
`AT_NODE` pose's offset.

**Approach.** Read the hookup definition and the airframe's own nodes (`planes/nodes.json`) for the
hook and fold parts, then the pose offset; land each half on its own evidence.

**Model recommendation.** high. Three mechanisms in one symptom, two of them per-airframe.

**Verify.** `--campaign=` captures of the hookup on a Balmoral and on a Devastator, plus the
`landings` suites green.

**⚠ Traps.** ⚠ Do not scale the pose to look right; the offset is authored and the airframe's node
frame is what to check.

## D20 ☐ CM01's drop-off cutscene shows no parachutist

**Goal.** The drop-off plays with the parachutist visible.

**Evidence (confidence: lead-only).** `BL-540`, seen at the controls. Which node the definition
activates for him is not read; a node from the shared archive that never reaches the tree is the
first suspect (B8's `AircraftStage` is the precedent).

**Approach.** Read the drop-off definition's `ObjectActiveState` list, find the node, and stage it
the way B8 staged the aircraft if it is cross-archive.

**Model recommendation.** medium.

**Verify.** A `--campaign=` capture of the drop-off with the parachutist in frame.

**⚠ Traps.** ⚠ If the node is chapter-local and merely switched off, this is an activation bug, not
a staging one; do not stage what is already there.

## D21 ☐ The low-terrain break-off, judged once the wingman is there

**Goal.** With the wingman beside the player out of the intro (D14), decide at the controls whether
its avoid-crash break-off over the island reads as the original.

**Evidence.** `BL-509`, A13's open half: the decoded vertical climb lands, the wingman is not yet
inside 150 m through the low pass, and C12 could not judge it because the wingman was 4 km away.

**Approach.** ⚠ **A stop, not a task an agent completes.** After D14, fly CM01 on `player_pfighter`
with the wingman on the same airframe and watch the first low pass over the island.

**Model recommendation.** medium (the brief and the write-up; the instrument is the user).

**Verify.** The user's report.

**⚠ Traps.** ⚠ Everything `BL-457` and `BL-509` retire stays retired.
