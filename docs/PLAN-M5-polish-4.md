# Milestone 5 polish, run 4: the CM03 to CM09 reports

**ACTIVE PLAN** (written 2026-08-27). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This run takes the at-the-controls reports from the CM03 to CM09 playtest (triaged into
`backlog.md` by commit `7b173cae`, with two later additions from the D18/D21 instrumentation) and
works them in mission order, so that one sortie can walk the campaign from CM03 to CM09 at the
end and judge each fix where it was reported. The selection is the "campaign walk" criterion
(Decision 1): open `[Bug]` and `[Feature]` items that a flown campaign mission trips over,
unblocked, with a named lead. Research-only items, `[Owed-playtest]` items, anything
`[Blocked: ...]`, and the plan-sized or user-deferred items (`BL-150`, `BL-446`, `BL-463`,
`BL-256`) are out. `BL-431` is excluded because it is mid-work on the live
`bl-431-cockpit-gauge-drive` worktree.

Every item was re-verified still-open against the record in this session (`git log --grep` on
each id finds only its filing or a cross-reference, never a closure; `BL-555`'s and `BL-426`'s
commits are filings). None was re-verified against the code; each item carries a
`<TODO: re-verify still-open against the code>` for that half. Evidence grades are capped by
provenance: an item whose backlog entry rests on a log file or a cited line is `traced`, an item
resting on a controls report alone is `lead-only`.

**Decode stays open per item (Decision 3).** The plan is routine in weight, but every item's
Approach names the `crimson.exe` routine or the data file that would settle it authoritatively,
and an implementer may take that lane instead of the code-side lead whenever the lead runs out.
A decoded rule beats a plausible fix here, as in every prior run.

## Milestone goal

- CM03 to CM09 each play through their reported defect: the AA turret fires, the Barracuda drives
  and launches into the bay, the Pandora holds fire on a friendly player and holds level on its
  route, the second Workers' Voyage docking waits for a docking, the rope ladder deploys, the
  Blue Streak flies its own fit, CM09's enemies fly and take off.
- The start-state script of a mission reaches the destructibles' pools, not only their visuals
  (CM04's buildings and balloons).
- Two instruments for the playtester: `--pos=` works under `--campaign=`, and a debug key kills
  the selected target so a stray enemy cannot block an objective chain.
- One closing sortie, CM03 to CM09, flown by the user, with each item judged where it was
  reported.

**The AI mode machine's patrol/pursue/lay-off cycle (`BL-523`) and the altitude-floor veto
(`BL-550`) are out of scope.** They are one decode of `AiModeMachine`'s source routines, heavier
than any item here, and they get their own run; `B12` and `D31` touch the same machine only as far
as their own symptom needs, and hand anything deeper back to `BL-523` (Decision 2).

## Decisions (2026-08-27)

| # | Question | Decision |
|---|---|---|
| 1 | Which selection criterion | **The campaign walk, CM03 to CM09** over "AI cluster first" and "quick wins only": the reports come from one playtest and a mission-ordered run lets one sortie re-judge all of them. |
| 2 | Size, and whether `BL-523`/`BL-550` ride along | **About 14 items, the whole report set; `BL-523` and `BL-550` stay in the backlog.** A paired cause and symptom (`BL-513`+`BL-521`, `BL-512`+`BL-522`, `BL-531`+`BL-532`) counts as one item. |
| 3 | Weight | **Routine, with the decode lane kept open on every item.** No up-front data survey; each item names what in `crimson.exe` or the mission data settles it. |

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

### Wave A — the instruments, then CM03 and CM04

1. ☑ `BL-548`: `--pos=` with `--campaign=` stalls the intro cutscene's completion
2. ☑ `BL-534`: a debug key that kills the player's selected target
3. ☑ `BL-516`: CM03's AA turret never fires
4. ◐ `BL-513` + `BL-521`: CM04's start-state script reaches the visual swap but not the pools
5. ◐ `BL-512` + `BL-522`: the Barracuda's drive jumps, its launch faces the wrong way, and its fighters crash at once
6. ☑ `BL-566`: the A press that skips a cutscene or resumes from the pause menu fires a rocket

### Wave B — CM05 and CM06

11. ❌ `BL-517`: the Pandora's broadside fires on a friendly player (reopened by D32 as `BL-567`)
12. ❌ `BL-524`: a friendly patrol without a net flies away after its first fight
13. ◐ `BL-525`: the second Workers' Voyage docking completes without a docking
14. ❌ `BL-514`: a shot-down carried turret keeps burning where it was (reopened by D32 as `BL-571`)

### Wave C — CM07 and CM08

21. ❌ `BL-526`: the rope ladder never deploys
22. ❌ `BL-527`: the second patrol's Peacemaker spawns under the ground
23. ❌ `BL-518`: a stripe-textured surface stands in front of the zeppelin hangar
24. ☑ `BL-528`: the Blue Streak flies with the stock Bloodhawk fit and no nitro
25. ☑ `BL-529`: the Pandora porpoises along its route and past its end (the route half reopened by D32 as `BL-576`)

### Wave D — CM09 and the sortie

31. ☑ `BL-531` + `BL-532`: CM09's first patrol hangs in the air, and no enemy takes off from the ground hangars
32. ☐ Fly CM03 to CM09 end to end and judge every item where it was reported

## Dependency and parallelism notes

A1 and A2 are instruments and go first: A1 makes a mid-mission `--pos=` launch usable for every
later item's targeted check, and A2 is the playtester's escape hatch for the D32 sortie. A4 and A5
are the same mission and A5's heading question (`BL-512`) is upstream of its crash question
(`BL-522`); settle the heading first. B12 and D31 both read `AiModeMachine`'s patrol branch and
may turn out to be one cause; run them in sequence, D31 after B12, and do not run them in parallel
worktrees. B11 and C25 both touch `ZeppelinRuntime`/`ZeppelinMotion`; the files differ
(`ZeppelinRuntime.Cannons.cs` against `ZeppelinMotion.cs`) so they may run in parallel with that
boundary stated. C24 is the only item touching `CampaignProgression.cs` and the player build path.
D32 waits for everything. Everything else is independent and may run in parallel worktrees.

File contention to respect: A4 and C21 both read the interp/anim dispatch (`AnimRuntime` event
kinds); if A4's fix lands in the `ObjectActiveState` handling and C21's in an event kind's
dispatch, they are adjacent in the same files and should not run concurrently.

---

# Wave A — the instruments, then CM03 and CM04

## A1 ☑ `BL-548`: `--pos=` with `--campaign=` stalls the intro cutscene's completion

**Goal.** A `--campaign=` launch with `--pos=` set either ignores the flag while the intro's
definition owns the session or applies it after the handoff; in both cases the intro hands off in
the same 16 to 40 s it takes without the flag.

**Evidence (confidence: direction-sound).** Six trials with `--pos=` on a `--campaign=` launch ran
to 300 s and 20000 frames without the intro's handoff, against 16 to 40 s without the flag (found
while instrumenting the auto-land prompt, `PLAN-M5-polish-2.md` D18). The mechanism is not
established. `<TODO: re-verify still-open against the code>`

**Approach.** Find what `--pos=` overrides that the intro's closing sequence waits on: the player
rig's staged pose (`AircraftStage`), the handoff's own restore, or a trigger the definition tests
against the authored spawn. Then either make the flag a no-op while a definition owns the session
or defer it to after the handoff, and say which in `docs/cli.md`. Decode lane: none needed; this is
a port-side flag.

**Model recommendation.** medium. A localised launch-arg interaction with one log to read.

**Verify.** `--campaign=<CM01> --pos=<x,y,z>` hands off in the log within the no-flag window;
`campaign-*` engine suites unchanged; no golden moves.

**⚠ Traps.** A realtime run's frame-to-wall-time ratio is not repeatable (`docs/verification.md`
INSTR-28): read the handoff off the log, never off a frame count.

**Landed.** `SpawnPicker.ChooseSpawn` withholds `--pos=`'s placement while
`CutsceneController.Playing` is already true at spawn time (`GameSession.BuildFlightRigs` sets
`WithholdOverrideForCutscene` there, after the world build has run the intro's own animation
bootstrap and settled that flag): the player starts on the mission's authored spawn, exactly as
with no flag, so whatever the intro's own progression tests the player's pose against still holds.
`GameSession` applies the withheld placement through `FlightController.Activate` the instant the
cutscene's `WorldHeld` callback goes false at the handoff (`SpawnPicker.TakeDeferredOverride`).
Chose **deferred to after the handoff**, not a no-op: a mid-mission `--pos=` launch is what makes
later items' targeted checks usable (Dependency note, A2's own text), and a no-op would strand a
`--campaign=` launch at the mission spawn instead of the position asked for.

Read off the log, per the trap above, not a frame count. C3/M05 ("Hawaii mission 2", seq 1),
`--headless --no-pads --campaign=<profile>:1 "--pos=0,5000,0"`: the withhold logs at spawn
(`spawn [override] withheld: an intro cutscene owns the session`), `generic_intro` ends at
**t=40.19 s**, and the placement lands right after (`spawn [override] pos=(0,5000,0) …`,
`campaign: --pos= applied at the intro's handoff`). The same mission with no `--pos=` hands off at
**t=40.18 s**, so the fix reproduces the no-flag timing, both inside the documented 16 to 40 s
window. A connected pad's phantom button reads as a skip at about 12 s, so the probe needs
`--no-pads`.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

## A2 ☑ `BL-534`: a debug key that kills the player's selected target

**Goal.** One key in the F13+ debug block kills `TargetSelection`'s current target through the
normal death path, so kill-count and `DEDG` objectives see the kill; inert with nothing selected;
no shipped binding.

**Evidence (confidence: lead-only).** Asked for at the controls: strays that fly off (`BL-523`) or
hang out of reach (`BL-531`) block an objective chain. Re-verified still open: no kill key existed
anywhere in `src/UI` or the input dispatch before this item.

**Approach.** `src/UI/DebugKillTarget.cs` (F17, the user's chosen key) reads P1's
`TargetSelection.Current` and routes on the selection's source type: a `FlightController` crashes
through the existing `DebugForceCrash(killer)` — the same attributed `Crash`/`Downed` path
`--debug-scoreboard`'s scripted kill already uses, so kill-count and `GroupLiveCount`/`DEDG` see
it exactly as a real shot down; a `DestructibleRegistry.Instance` (a zeppelin gasbag, cannon or
engine) is destroyed through the existing `AnimRuntime.DamageAt`, the same call a rocket makes and
`WorldDamageLab`'s own Kill button uses. **A turret selection stays inert, disproving that half of
the Approach's assumption:** `Flight.TargetRef.Health`'s own decoded rule is that a turret
emplacement carries no `HEALTH` key at all (the retail loaders read none), and nothing in the
engine today toggles a turret's kill switch (`TurretController.Alive`'s healthy-node visibility).
Routing a kill through a turret would invent a mechanism the decoded data does not have, so the
key logs and does nothing on one instead — the correct outcome under this project's own rule
against inventing content. The two reported blockers (`BL-523`, `BL-531`) are both stray
*aircraft*, which the aircraft branch covers.

**Model recommendation.** medium, low effort. Mechanical wiring on an existing path.

**Verify.** `KillSource` (the routing switch, exposed for exactly this) is driven directly against
hand-built sources in the `targeting` suite family, with no live `AimCandidateSet` scan behind it,
since `DebugForceCrash` and `DamageAt` already carry their own coverage elsewhere. Manually: in an
Instant Action dogfight, select an enemy, press F17, and confirm the wrap-up counts the kill; press
it with no selection and confirm nothing happens; select a turret and confirm nothing happens.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

**⚠ Traps.** Kill through the damage model or objectives never fire — avoided by reusing
`DebugForceCrash`/`DamageAt` rather than freeing the node. Debug only: no entry in the shipped
keymap.

## A3 ☑ `BL-516`: CM03's AA turret never fires

**Goal.** CM03's anti-aircraft turret fires on the player in range.

**Evidence (traced).** Reported at the controls: the turret was silent through the whole mission.
CM03 (`C3/M02`) is a world-emplacement case, not a carried one: its `objectives.zrd` scripts
`WAKEUP_TURRETS ["aagun01","aagun02"]` (OBJECTIVE37, napped by OBJECTIVE36's completion),
`WAKEUP_TURRETS ["aagun30","aagun31","aagun32"]` (OBJECTIVE38, napped off OBJECTIVE2) and
`WAKEUP_TURRETS ["b_turret*"]` (OBJECTIVE40, all six balloon-mounted rings). `ai.zrd`'s
`aagun**`/`b_turret*` entries are both standalone world emplacements (`CREATE_STANDALONE` absent,
`ACTIVATED 0`, no `TEAM` key, so `TurretDefs.DefaultTeamId` = 2, enemy) built into the live
`TurretController` set on every C3 flight. The wake/acquire/fire path from
`ObjectiveGraph.WakeLive` through `CampaignDirector.WakeupTurrets`,
`TurretEmplacementRuntime.SetActivatedUnder` and `TurretController` itself was traced clean.

**Root cause: OBJECTIVE36 never completed.** Its `TRAVELERS [4, "APPROACHING", "unit03", 700.0,
1]` is the "group form" (subject = aiv roster group 4, not a named node): `CampaignDirector
.World.TravelersMet` (`CampaignDirector.cs:675`, pre-fix) bailed on any `spec.Group != null` with
`Gap("TRAVELERS", "the group form needs a spawned aiv roster")` and returned `null` unconditionally
— the check that decides whether the condition is even met was never implemented, only the node
form was. Since OBJECTIVE36 could never complete, it could never nap OBJECTIVE37, and `aagun01`/
`aagun02` never woke. `docs/formats/objectives.md`'s `TRAVELERS` row already carried the decoded
group-form shape (`FUN_00465b40`: each tick, every live group member inside/outside the radius
adds 1 to a tally, true once the tally reaches `count`) — the decode existed, the engine consumer
did not.

**Fix landed.** `CampaignDirector.cs`'s `World.TravelersMet` now implements the group form: walks
`_owner._rosterPlans` for live, non-inert members of the spec's group (the same roster walk
`GroupLiveCount` uses for `DEDG`), counts how many sit inside (or outside, for a non-`APPROACHING`
word) the radius of the resolved reference point, and returns whether that tally reaches
`spec.Count`. Null only while no roster is spawned yet, matching `DEDG`'s "not yet decidable"
convention.

**Verified live.** `--campaign=<profile>:2` headless (a copied, renamed profile, deleted after)
confirmed the fix end to end: before the fix, only OBJECTIVE1's `WAKEUP_ZEP_TURRETS ["piratezep"]`
ever printed `campaign: WAKEUP_TURRETS armed N emplacement(s)`; `aagun01`/`aagun02` never woke
(OBJECTIVE36 stuck `Awake`, unsatisfied, confirmed via the engine's own `DIAG`/`DIAGREF`
objective-state trace). After the fix, on a fresh (no prior chapter-6 persist log) profile, all
three CM03 `WAKEUP_TURRETS` calls fired (`armed 2` for OBJECTIVE37, `armed 3` for OBJECTIVE38,
`armed 6` for OBJECTIVE40) and every one of the nine ground/balloon turrets
(`aagun01`/`02`/`30`/`31`/`32`, `b_turret1`-`6`) logged `turret MSG_TUR_...: engaging (first shot,
team 2)`, several taking return fire from the escorting wingmen in the same run.

**Model recommendation.** medium.

**Verify.** `--campaign=<profile>:2` headless (a copied profile) shows all three `WAKEUP_TURRETS`
calls fire and every named turret logs `engaging`; `dotnet test` 2443/2443; `--run-tests` (146
suites, including `world-turrets` and `campaign-objectives`) and the 16-shot golden set both clean
on a re-run with no other Godot process live on the shared hidden desktop (the first two runs read
FAIL on the `engine` stage only, from stray probe processes still exiting — a known contention
hazard, not a regression: PASS on the third, uncontended run); then at the controls in D32.

**⚠ Traps.** `BL-506` is the AI-carried case (`AiFlightAssembler` never calls `BuildCarried`) and a
different path; this closes separately from it, kept apart by `TurretController.Site`. The fix is
scoped to `TravelersMet`'s group form only; it does not touch the node form, `DEDG`, or any other
objective directive.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls..

## A4 ◐ `BL-513` + `BL-521`: CM04's start-state script reaches the visual swap but not the pools

**Goal.** In CM04 (C3/M03) the buildings and barrage balloons the setup script starts destroyed
are destroyed: the balloons are down at mission open, and a hit on a pre-destroyed building finds
no healthy pool and runs no destroy sequence.

**Evidence (confidence: lead-only).** Two controls reports on one mission with a likely shared
cause: buildings starting destroyed still run their destruction sequence when hit, and the
balloons stand intact where the original opens with them down. C3/C4 drive the balloons through
`bont*`/`balloon_t*`/`tether*` state events (`docs/formats/anim-definitions.md` 116).

**Re-verified against the code: the architecture bug is real and general; landed, but the exact
CM04 trigger was not pinned down.** `AnimRuntime.Dispatch`'s `ObjectActiveState` case only ever
called `Pose.HandleActiveState` (the visual swap, `SetTargetActive`); nothing synced
`DestructibleRegistry`. Any healthy/destroyed role swap dispatched outside `DamageAt`'s own kill
(a start-state script, an `ON_STARTUP` sequence, or a def's own `RESET_STATE` authored to start
destroyed) left the pool at full HP and `Healthy` while the node already read destroyed — exactly
the shape both reports describe. Fixed with `AnimRuntime.SyncDestructiblePool`, called right after
`Pose.HandleActiveState`, and Bootstrap's Pass 1 now registers a destructible before dispatching
its own `RESET_STATE` (previously after, so a def starting destroyed by its own `RESET_STATE`
found no pool yet to sync). Verified by a new engine suite, `start-state-swap-pool`, against a
real shipped def whose own Initial sequence authors the role swap directly: the pool follows to
`Destroyed`/HP 0 with no `DamageAt` call in the picture, and a later `DamageAt` on it is a no-op
rather than a second death.

**The CM04-specific mechanism was not found.** M03's own compiled `mis_anim` set carries no
`ON_STARTUP` building-destroy content, no `PERSIST_LOG` reader def (`ucamp_dest`/`tower_dest`/…)
is compiled into this mission at all, and `support\c3\m03.gw` switches `bont1..6`/`b_turret1..6`
fully OFF rather than to a destroyed variant. The one concrete "starts destroyed" content M03
does ship is `cargozep1`'s own `calldestroy_the_cargozep` → `destroy_the_cargozep`
(`startanims.zrd`'s `NEW_GAME_START` list), a scripted cutscene that deactivates `tntbox1..4`/
`gasbag1`/`gasbag5` by name rather than by the `healthy`/`destroyed`/`dbase` role convention this
fix (and `AuthorsSwap`/`ApplyDeathSwap` before it) key on — headless `--campaign=<probe>:3`
confirms it runs (the fireball/tntbox retargets log, `AnimHealth(15)` on `cargozep1` reads false
throughout), but `SyncDestructiblePool` does not reach it, since no event in that cutscene names a
role node. Whether `cargozep1` itself needs the same treatment on its ad hoc node names, and
where CM04's reported buildings/balloons actually live in the data, are open.

**Wiring contract (not landed here).** If `cargozep1`'s own HP pool turns out to be the reported
building/balloon symptom's actual carrier, widen the sync to cargozep1's own destroy cutscene (a
def-specific rule, or a generic "this def's own DAMAGE_SEQUENCE testing `ANIM_HEALTH` while its
own OBJECT_ACTIVE_STATE events never ran DamageAt" gate) rather than hardcoding cargozep1 by
name. Otherwise, find CM04's actual pre-destroyed content with a debug pass at the controls
(`--debug-anim` over a full CM04 flight, watching for any node the player sees destroyed at open)
and trace it from there; the architecture fix landed here will apply automatically once that
trigger is identified, if it is a role-named swap.

**Model recommendation.** medium.

**Verify.** `--campaign=<CM04>` headless: `cargozep1`'s scripted destruction runs clean, no new
engine errors (confirmed). `start-state-swap-pool` suite green against real shipped data
(confirmed). The balloons'/buildings' state at first frame and a scripted hit on them: not
confirmed, since their authoring was not found. D32 must judge this item at the controls with A2's
kill key or a fresh look, since the closing sortie is the only remaining way to see whether the
reported symptom still reproduces on the landed build.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

**⚠ Traps.** Do not gate the sequence on the visual state alone; a building destroyed in play and
hit again is the same symptom on another path, and the fix belongs at the pool.
`PLAN-c3-balloon-kill-chain.md` settled the live kill chain for C3/M02; that is not this mission's
start state, so do not reopen it. Do not read `cargozep1`'s untouched `AnimHealth(15)` condition
in the probe log as proof of a live bug on its own; nothing in this playtest damaged it, so the
condition never had a reason to flip regardless of the pool's state.

## A5 ◐ `BL-512` + `BL-522`: the Barracuda's drive jumps, its launch faces the wrong way, and its fighters crash at once

**Goal.** The Barracuda's `sub_movement` drive into the bay is continuous and ends looking into the
bay, and the fighters it launches fly rather than crash into its runway or the water.

**Evidence (confidence: traced for the crash, lead-only for the drive).**
`playtest/game-20260826-085720.out`, `c3/m03` leg: `britpeace_5` and three unnamed successors
(`@Node3D@3408` and on) spawn at `pos=(-12032,0,-11516)`, altitude 0, on net `M3BritInt#7`, and
crash within seconds (`CRASH into sub_runway/col (fuselage) ... spd=73 m/s`, `CRASH into
g28546/col_water (tail) ... spd=77 m/s`). The later spawns carry no roster name, its own defect in
the generator's naming. The drive's jump and the wrong final heading are a controls report;
`barracuda` begins inactive and `sub_movement` activates and moves it over 40 s
(`docs/formats/gamez.md`). `<TODO: re-verify still-open against the code>`

**Approach.** Heading first: `--anim-lab --node=barracuda` on C3, play `sub_movement`, compare the
node's transform at each event boundary against the def; a jump on a 40 s `ObjectMotionFromTo`
points at a keyframe or an activation transform applied twice, a wrong heading at the motion's
rotation term or a base transform the activation does not carry. Then the launch: decode the
original's launch from a surface host (the moving-spawner shape in `EnemyGenerators.cs`), whether
it places the aircraft airborne ahead of the host or holds it `Inert` on the deck through a
takeoff, and its heading relative to the host. Fix the generator's naming of the later spawns on
the way. Decode lane: the moving-spawner launch routine in `crimson.exe`, which is the intended
path for the launch half.

**Model recommendation.** high. Two mechanisms, one decode, and the crash may be downstream of the
heading.

**Verify.** `--anim-lab` transforms continuous and the final heading into the bay; headless CM04
with the generator log on shows each launched fighter's first seconds airborne; D32.

**⚠ Traps.** The zeppelin launch-altitude gate is a decoded rule for airships and not a general
one: do not lift the sub's launch by borrowing it, and do not add a spawn-height offset. `BL-515`
(where the Barracuda takes damage) is research and stays in the backlog.

**Outcome (◐).** The launch is decoded and landed; the drive is measured and handed back.

Landed: a surface generator resolves the decoded take-off path `<base>_aip<n>` in its host's
subtree (`barracuda` → `bauda`, `eairg31` → `eag31`), drops at load when it is shorter than two
points, and launches on point 0 plus 0.2 m with its nose on point 1, at zero velocity with the
throttle open. `moving_path`'s meaning is decoded (it keeps the path host-relative so it rides a
driving hull) and recorded on `EnemyGeneratorDef`. Launch naming is the decoded `%s_eg%d` over a
mission-global counter (`AiGeneratorRuntime.LaunchOrdinal`), which is what ends the
`@Node3D@3408` renames: those were Godot resolving a duplicate node name, because every launch
took the roster block's own name. All of it is in
`docs/formats/mission-entities/enemy-generators.md`.

**Wiring contract, back to `BL-522`.** The take-off **run** is not built. The original keeps the
path on the aircraft at `+0xc8` with the flag at `+0xcc` and flies the remaining points, which
also suppresses the net-nearest-node snap (`FUN_004b0f40`); CSVM hands the aircraft to its patrol
net from the launch pose instead, so one placed at rest on a deck has no authored way to reach
flying speed. Decoding the consumer of `+0xc8`/`+0xcc` is what closes it.

**`BL-512` is measured, not fixed, and stays in the backlog.** The drive's discontinuity is not a
keyframe or a doubled activation transform: `sub_movement` is smooth as authored, and reading
`translation.delta` as acceleration makes its three motions continuous and its travel land within
1.2 m of the closing absolute placement. The jump is `rnd_xz`, which on all three events equals
the normalized direction rather than a random amplitude, while `MotionRuntime` adds
`RandSym() * rnd_xz` to each start velocity: up to ±44 m of drift, snapped away in one frame by
the closing `ObjectMotionFromTo`. That field is non-zero across far more than this def, so it
belongs to `ObjectMotion` as a whole. The "wrong way" half is **disproven**: no event in the def
carries a rotation term, `barracuda`'s gamez transform is `Initial`, and the hull's local −Z (its
take-off run) is correct as built.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

## A6 ☑ `BL-566`: the A press that skips a cutscene or resumes from the pause menu fires a rocket

(Minted as `BL-556` during the run and renumbered twice, because main minted its own `BL-556`, the
difficulty scale, and then `BL-563` to `BL-565`, the CM12 `DEDG` items, in parallel. The landing
commit's subject still names `BL-556`.)

**Goal.** Confirming a cutscene skip or the pause menu's Resume with gamepad A (or F on the
keyboard) launches nothing; the next fresh pull of the trigger fires as before.

**Evidence (confidence: traced).** Reported at the controls, added during Wave A.
`RocketFirePressed()` (`FlightController.cs:1871`) is a level read of `JoyButton.A`, and the
press that `CutsceneController.Skip()` or `BoardMenu`'s confirm consumes is still down on the
first flight frame after the world resumes. The rocket read's own comment names the spawn-frame
form of the same defect.

**Approach.** Latch the rocket trigger when flight regains input after a skip or a resume, and
release the latch on the first frame the button is up; the keyboard F takes the same latch.
Decode lane: none; a port-side input interaction.

**Model recommendation.** medium, low effort.

**Verify.** A unit or engine test that holds A across a skip and across a Resume and asserts no
launch, then releases and pulls and asserts one launch; `cutscene-skip-*` and pause suites
unchanged; at the controls in D32.

**⚠ Traps.** Do not turn the trigger into an edge read (a held A fires once by design). B and X
are not confirm buttons and stay untouched. Cover Resume, dismiss and the skip; Photo mode and
Restart resume by other paths.

**Landed.** A new pure `RocketTriggerLatch` (`src/Flight/RocketTriggerLatch.cs`) sits behind
`FlightController.RocketFirePressed()`: it arms when `RocketButtonDown()` (F/A) reads true at the
two re-entry points flight regains input from, the `Inert` setter clearing (a cutscene's skip or
its own handoff) and `PollPauseAndHalt`'s halt-clearing edge (a pause-menu Resume, Restart, or a
dismiss, all of which route through the same `PauseState.Halted` transition), and reads the
trigger released until the physical button lets go, at which point it disarms and a fresh pull
fires normally. `FireControl`'s own edge detector (`_rocketFirePrev`) is untouched; the fix is
entirely upstream of it, so the gun trigger (B) and the selectors are unaffected. Both
`Inert=false` sites (`Activate`, the wave/respawn re-entry) and the pause halt clearing are
covered by the same two hooks, closing the spawn-frame sibling the original comment named as well.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

# Wave B — CM05 and CM06

## B11 ❌ `BL-517`: the Pandora's broadside fires on a friendly player

**Goal.** A friendly airship whose authored `targets` list names the player does not shoot them;
the same record on a hostile airship (C3/M03) still does.

**Evidence (confidence: traced).** `playtest/game-20260826-085720.out`, `c3/m04` leg: `zep:
'piratezep' broadside wired — 6+6 cannons, wep_28 ... targets [player]`, then `broadside right: 6
cannon(s) fire wep_28 at 'player' (range 207 m)` and six `shot hit P1` lines taking the hull from
100 to 60 in one salvo. `ZeppelinRuntime.Cannons.ResolveTarget`
(`ZeppelinRuntime.Cannons.cs:289-292`) takes the first live authored name with no hostility
check.

**Re-verified against the code: disproven.** `crimson.exe`'s broadside targeting pipeline carries
no team or hostility test at any stage. `FUN_004bd8d0` parses a record's `targets` key into
unresolved name pairs at load; `FUN_004bede0` (run once, after every mission zeppelin is placed)
resolves each pair's second field via `FUN_004bd430`, a plain name match against the live
zeppelin roster with no team read; the fire routine `FUN_004bfe00` consumes the resolved list on
arc (`> 0.707`) and intercept alone. `ResolveTarget` already matches this decoded shape: first
live authored name wins, no filter to add.

The playtest log itself disproves the "friendly" premise: `'piratezep'` logs `team unauthored` /
`no authored team` identically in both C3/M03 (the hostile leg) and C3/M04 (the reported leg):
the record carries no team distinction between the two missions, and the log has no
`SET_AI_TEAM` line for either. Nothing in the data or the decoded engine marks this zeppelin
friendly in C3/M04; the fire in both legs is the same decoded, ungated behaviour. Landed no code;
`docs/formats/mission-entities.md` "Broadside firing" carries the decode.

**Approach (as filed, superseded by the decode above).** Decode the broadside fire routine's gate:
the target's team against the airship's live team, or the `targets` list as a candidate set
filtered by hostility. No such gate exists to apply.

**Model recommendation.** medium.

**⚠ Traps.** The filed trap ("gate on the airship's live team, which `BL-502`'s `SET_AI_TEAM` can
flip mid-mission, not on the record name") does not apply here: no `SET_AI_TEAM` directive fires
for `piratezep` in either leg of this playtest, and `LiveZeppelin.Team` has no setter today — the
record's authored team is fixed at spawn. If a future mission genuinely needs a zeppelin to hold
fire on the player, the decoded lever is the record's own `targets` list (omit `player`), not an
invented engine-side hostility filter.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

## B12 ❌ `BL-524`: a friendly patrol without a net flies away after its first fight

**Goal.** In CM05 (C3/M04) and CM07 (C1/M02) the friendly patrol returns after its first fight
instead of continuing on its last heading out of the mission.

**Evidence (confidence: lead-only).** Reported at the controls in both missions. Either the
friendlies' `aiv` blocks author no net and the original's lay-off returns them to a default (the
leader, the spawn point, or a net set later by `SET_AI_NET`), or the block authors a net our
spawner drops (`BL-453`). `<TODO: re-verify still-open against the code>`

**Approach.** Read both missions' `aiv.zrd` for the friendlies' net and mode fields, then decode
what `LayOff` does with no net (`docs/org/aiPilot.md`). Decode lane: the lay-off return rule, which
is the only sound source for a default.

**Model recommendation.** high. The answer comes from the decode and touches the mode machine.

**Verify.** Headless CM05 with the AI trace on: the friendly patrol's mode and position after the
first kill stay within the mission area; D32.

**⚠ Traps.** Do not give them the player's escort law as a default (`BL-457` shows the escort
hand-off is itself unsettled). Do not add a leash constant. Anything deeper than the no-net
lay-off is `BL-523`'s and goes back to the backlog with what was learned.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

**Outcome (disproven; no code landed).** Both halves of the item's stated cause are wrong, and the
decode lane it names has no rule to give.

*The data.* The netless friendly blocks are `wingman_1/2/3` in CM05 (C3/M04) and `wingman_2/3/4` in
CM07 (C1/M02), every one of them a `mode wingman` block whose `primary_target` names a leader
(`player`, or `devastator_1`/`devastator_2` in CM05 and `devastator_2`/`devastator_3` in CM07). The
friendly aircraft that fly a route are the `devastator_N` blocks and they are netted: ids 11
(`M4Bravo`) and 12 (`M4Charlie`) in C3, 25 (`M2Charlie`) and 26 (`M2Bravo`) in C1, all four carried
by their chapter's `neindex`. Neither mission has a friendly patrol without a net, and the spawner
drops no net here, so `BL-453` is not the cause.

*The decode.* There is no no-net lay-off return rule to port. `lay off` is not a stored state: the
debug readout derives it from having a target while the AI task at `+0x2f0` is not 1
(`FUN_0041c470`). A netless non-`wingman` aeroplane has no behaviour at all in the original, since
the follower `FUN_0041d1f0` resolves its net first and indexes -1 on no match with no guard, which
is unreachable only because the shipped data never authors it. A netless `wingman` runs the escort
law `FUN_0041e760`, re-read out of `crimson.exe` for this item: it dereferences its leader at
`+0x2fc` through `+4` in the function's first block with no null check, and reads the leader's
presence byte `+0x91d` only inside the state-0 join test and the state-2 re-acquire. The engine has
no leaderless branch, and the one routine that would hand a wingman a default, `FUN_0049c920`
(assign the chapter's first net, clear `mode` to `jet`), is reached only from `FUN_0049c880`, which
Ghidra reports with zero callers and no pointer anywhere in the image.

*What is left is CSVM's own.* The only code path that produces the reported symptom is `AiPilot`'s
invented null-leader fallback (`Escort is { Leader.InPlay: true }`): when a leader leaves play the
pilot drops to `FlyPatrol`, and with `Patrol` null that arm projects `TargetHeadingDeg` and
`TargetAltitude` into an aim point. Those two fields are scratch as well as orders, and
`FlyPursuit` overwrites both every step with the bearing to its quarry and the quarry's altitude. A
netless pilot that fights and then loses its leader holds the last bearing it had to a dead enemy
for the rest of the mission, which is what "continues on its last heading out of the mission"
looks like. That is the escort hand-off's fallback rather than a lay-off rule, so it belongs with
`BL-457` and `BL-523`; both entries carry it now, and `BL-524` is rewritten around it rather than
closed.

*Confirmed at runtime.* Both missions were flown headless under `--campaign=`. CM05 spawns 15 of 16
blocks with 3 escorts, `wingman_1` on `player`, `wingman_2` on `devastator_1` and `wingman_3` on
`devastator_2`, the two leaders netted `M4Bravo#11` and `M4Charlie#12`; CM07 spawns 10 of 13 with 3
escorts and its leaders netted `M2Charlie#25` and `M2Bravo#26`. Nothing is missing a net and no
block is dropped, which settles the item's premise. The fallback's trigger is real in the same run:
`devastator_1` is shot down mid-mission, leaving `wingman_2` netless and leaderless. That run does
not show the drift itself, because `wingman_2` held a live target from then until it was shot down
too, and the drift needs a leaderless pilot to also lose its target. Which friendly the controls
report watched is therefore still unpinned, and the last step of the mechanism is read off the code
rather than observed.

## B13 ◐ `BL-525`: the second Workers' Voyage docking completes without a docking

**Goal.** CM06 (C1C/M01)'s second docking objective completes only when the player docks.

**Evidence (traced).** `objectives.zrd`'s two docking objectives both gate on `ANIM_STATE`:
OBJECTIVE11 (first hook) waits on `wv_drop_copilot` RUNNING, OBJECTIVE15 (second hook) waits on
`wv_pickup_copilot` EXECUTED. `ObjectiveGraph`'s reading of `ANIM_STATE` is correct against the
decode (`AnimStateMet` calls `IObjectiveWorld.AnimState`, backed by `AnimRuntime.AnimStateOf`,
faithfully). The bug is one level down: the hook node's own script
(`extracted\C1C\M01\zrdr\wv_tailhook.zrd.json`, `wv_initiate_hookup`) `CALL_ANIMATION`s
`wv_drop_copilot`, `wv_pickup_fassenb` and `wv_pickup_copilot` unconditionally on every dock; only
each definition's own `ACTIVATION_PREREQUISITE` (`REQUIRED [OBJECT_ACTIVE_LIST [[wv_tailhook,
dropoff_node]]]` / `[[wv_tailhook, pickup_node]]`) is authored to keep the wrong leg from running.
CSVM parses neither path of that prerequisite shape: `AnimDefs.cs`'s reader case only reads
`OPTIONS [MINIMUM_TO_SATISFY, ANIMATION_LIST]` (the zeppelin hull-death form), and
`CompiledAnim.cs Parse`'s compiled-form read of `activ_prereqs` only accepts entries shaped
`{"Animation": ...}`, silently dropping the `{"Parent": ...}`/`{"Object": ...}` node-active-state
shape this def (and roughly 50 others census-wide: gasbag panel finishers, `chuteman`'s
drop-direction gate, `pzep_cargo_point`'s cargo stop) actually carries. `ZeppelinRuntime.cs` is the
only consumer of the parsed `PrereqAnims`/`PrereqMinToSatisfy` fields, so nothing enforces the
node-active form anywhere. Net effect: `wv_pickup_copilot` reaches EXECUTED on the FIRST docking
already, so OBJECTIVE15's `ANIM_STATE` condition is already true the instant it wakes on the
second-docking nap chain (OBJ13 -> naps OBJ14 30s -> OBJ14 completes immediately (no condition) ->
naps OBJ15 30s -> OBJ15 wakes with `wv_pickup_copilot` already EXECUTED), well before any real
second hook-up.

**Approach.** Read CM06's `objectives.zrd` for the two docking objectives and their gates, then
trace `ObjectiveGraph` for what completed the second one. Decode lane: the docking objective's
completion test in the original, if the graph's reading of the gate is the question. It is not:
the gate reads correctly, and the animation it reads should not have reached EXECUTED yet.

**Model recommendation.** medium.

**Wiring contract (not landed here).** The fix is general `AnimRuntime`/`CompiledAnim` work, not an
`ObjectiveGraph` one, and its blast radius (~50 defs across several chapters) is bigger than this
item: parse the node-active `ACTIVATION_PREREQUISITE` shape on both paths (reader `REQUIRED
[OBJECT_ACTIVE_LIST [[path...]]]`; compiled `Parent`+`Object` entry runs, the `Object` leaf's
`active` field the required state) into a path/required-state list on `AnimDefinition`, and enforce
it generically at `AnimRuntime`'s `CALL_ANIMATION`/`Start` dispatch (silent skip when unmet,
mirroring the existing hull-death gate's own silence). Do not hardcode a CM06-specific exception:
the prerequisite is data-authored and general, and a docking-only patch would leave the
gasbag/cargo/chute defs carrying the same shape unfixed. Verify with the full 8-chapter freecam
regression (parsing-only defs load the same way; only when a gated def may *start* changes) plus a
targeted CM06 run confirming `wv_pickup_copilot` stays unstarted through the first docking.

**Verify.** Headless CM06 with the objectives log on, the second docking stays open until a docking;
a `campaign-objectives-*` suite row if the harness reaches it; D32.

**⚠ Traps.** `ObjectiveGraph.ScanForCompletion` resolves one objective per tick round-robin
(`BL-458`), so a completion can land frames after its cause; that round-robin is not this bug's
mechanism.

## B14 ❌ `BL-514`: a shot-down carried turret keeps burning where it was

**Goal.** A carried turret's death fire rides the hull (or the sub-part's node) and stops when the
part is gone; a ground emplacement's fire stays world-anchored as it is.

**Evidence (confidence: lead-only).** Reported at the controls in CM06: after a Workers' Voyage
rocket turret dies, the fire stays lit at its former position while the zeppelin moves on. The
effect is parented to the world, or the part is removed and its emitter left behind.

**Disproof.** Re-verified against the code: the mechanism already anchors correctly. A world
turret's `TurretController.Site` (built by `BuildEmplacements`, the only path a zeppelin's own
gun rings take; there is no zeppelin `BuildCarried` host anywhere in the source) is an ordinary
scene child of whatever it is mounted on. `ZeppelinRuntime.Place` moves a zeppelin by writing one
`GlobalTransform` on the hull root every sim step, which the scene tree propagates to every
descendant, `Site` included. A destroy sequence's `PUFFER_STATE` dispatch runs on the WORLD
runtime, whose `TemplateStage` is unpooled and un-placing in real play
(`WorldSession.Options.PlacesCalledTemplates` is true only under the anim-lab debug tool), so the
fire's `PufferEmitterFactory`-built emitter is never `TopLevel`-frozen at a placement snapshot;
`EmitterDirector.Tick` re-reads the host node's live `GlobalTransform` every frame regardless.
Nothing in the turret-death path (`TurretController`, `TurretEmplacementRuntime`,
`DestructibleRegistry.ApplyDeathSwap`) reparents or frees the site node early.

**Approach (as evaluated).** Anchor a carried turret's death effect to the sub-part's node or the
hull, the same `TopLevel` anchor question the trail effects went through (`trail-world-anchor`
suite), keyed on `TurretController.Site`, and stop it with the part. Decode lane: none expected.

**Model recommendation.** medium.

**Verify.** `turret-death-effect-world-anchor`, in the `trail-world-anchor` shape: a real
`TurretController.BuildEmplacements` turret's `Site` under a carrier posed off-axis (the same C1
spawn pose that hid the original trail-world-anchor bug), a `PUFFER_STATE` dispatched through
`AnimRuntime.Emitters` (the real `EmitterDirector`, a no-GPU `CountingEmitterFactory`), the
carrier translated and re-yawed between two `Tick`s, and the emitter's fed position asserted to
track the moved site rather than the pose it started at. Seen FAILING (able-to-fail probe:
`EmitterDirector.Tick`'s host-transform read pinned to identity) and PASSING against the landed
code.

**⚠ Traps.** The fix is on the carried case only; the world emplacement is correct today. The
report's own "carried" reads as "rides a moving zeppelin", not the code's `TurretController.Site
== null` sense (aircraft `BuildCarried`) that `BL-507` used the same discriminator for; a
zeppelin's own gun rings are `Site != null` structures exactly like a ground AA gun, and the code
already treats their moving platform correctly (`TurretController.PlatformOf`, the differenced
`PlatformVelocity` estimate).

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

# Wave C — CM07 and CM08

## C21 ❌ `BL-526`: the rope ladder never deploys

**Goal.** CM07 (C1/M02)'s pickup shows its rope ladder so the pickup step can be flown.

**Evidence (confidence: lead-only).** Reported at the controls. CM07 arms a pickup gate ahead of
its cutscene (`docs/formats/anim-definitions/cutscenes.md` 358, `C1/M02/zrdr/pickups.zrd`); the
ladder is an animated node the pickup script activates, and `BL-035` lists the event kinds the
runtime still drops, so a deploy driven by one of them is silent. `<TODO: re-verify still-open
against the code>`

**Disproof.** Re-verified against the code and the decompile: not a dropped event kind. `player`'s
`drop_ladder` def (`extracted/C1/M02/zrdr/ladder.zrd.json`) calls `gen_drop_ladder` with
`WAIT_FOR_COMPLETION`, which does the visible work unconditionally with kinds `AnimRuntime` already
handles (`OBJECT_ADD_CHILD`, `OBJECT_ACTIVE_STATE`, `OBJECT_MOTION_SI_SCRIPT`, `LOOP`,
`CALL_SEQUENCE`), then a `CALLBACK[123]` fires only afterward. Code 123 is one of the two gap codes
`docs/formats/anim-definitions/cutscenes.md` already names as reaching no case in the
mission-script host and undecoded ("nothing in the exe tells us what 14 or 123 were meant to do");
it is not gating the deploy. `BL-035`'s dropped kinds (`CALLBACK`, `OBJECT_CYCLE_TEXTURE`, one-shot
`SOUND`) play no role here and are ruled out as the cause.

The real gap: no `.zrd` file anywhere in C1/M02 or the shared chapter `landings.zrd` ever authors a
`CALL_ANIMATION[drop_ladder]` — an exhaustive search of every reader-form `.zrd.json` under
`extracted/C1/M02` (`copilot_pkup`, `ladder`, `objectives`, `startanims`, `pickups`, `dzones`,
`mis_anim`, `hangar_drop`, `hangar_panic`, `objcomplete`) turns up `drop_ladder` only as a
`STOP_ANIMATION`/`INVALIDATE_ANIMATION` target in the pickup-completion def (`lookat_copilotpkup`)
and as its own definition; nothing ever calls it. Ghidra confirms why: the string `"drop_ladder"`
(`00627b64`) has exactly one xref, inside `FUN_004735b0`, a hardcoded C1/M02-specific mission-init
function, not the generic `.zrd` reader path. It resolves both `drop_ladder` and `retract_ladder`
by name into a small heap object (`FUN_004456f0` ctor, global `DAT_0071c324`) that also resolves
node `ladder_pos` and registers its own `CALLBACK` host (`FUN_004ee160`, one of the 13
install-wide registration sites `cutscenes.md` already lists, distinct from `landings.zrd`'s). The
per-frame trigger lives in the main world tick `FUN_004897c0`: outside a cutscene, gated on an
attitude/alignment test (`0.707 < player_field[100]`, cos 45°, against the switch object's own
facing) and a proximity/membership test (`FUN_00471690` against the switch object's own list), it
deploys the ladder through the switch object's own vtable (`FUN_004455e0`) or retracts it
(`FUN_00445620`) otherwise — never through `CALL_ANIMATION`, never through `pickups.zrd` or
`landings.zrd`.

This is not an `AnimRuntime` dispatch gap; it is a bespoke, per-mission native gameplay object
CSVM has never modeled: an attitude-and-proximity-gated "ladder switch" evaluated every tick,
independent of the pickup-timing/landing-approach machinery `2aa7d77d` already landed. Building it
needs `FUN_00471690`'s membership test and `player_field[100]`'s exact meaning decoded further
first; that decode, not an event-kind handler, is the next step, and it is out of this item's file
scope (`AnimRuntime`'s event-kind dispatch, `AnimDefs`/`CompiledAnim` readers).

**Model recommendation.** medium.

**Verify.** <pending orchestrator run>

**⚠ Traps.** `BL-035` is not the cause; do not land a new event-kind handler here, it would be a
no-op. The train pickup cutscene fix (`2aa7d77d`) already covers `pickups.zrd`/`landings.zrd`;
the ladder switch is a separate, undecoded native object.

## C22 ❌ `BL-527`: the second patrol's Peacemaker spawns under the ground

**Goal.** CM07's single-Peacemaker second patrol appears above the terrain.

**Evidence (confidence: traced).** Re-verified against the code: not a roster-placement bug.
`CampaignDirector.BuildRoster`'s spawn log (extended with a terrain probe, the same
`CollisionLayers.World` downward ray the flight model's ground-blow probe uses) shows all four
enabled `blakepeace_2_1`..`_4` roster blocks landing ~122 m above the measured terrain, so the
initial-roster formation is placed correctly. CM07's only other Peacemaker-def AIV blocks,
`blakepeace_2_5`/`_2_6`, are both authored `enabled 0`: a generator-parameter template, not a
roster spawn (`docs/formats/ai-rosters.md`). CM07's `egen.zrd` runs two live generators
(`eairg31`/`eairg32`) whose `vehicle.params` names the disabled AIV block a fresh spawn configures
from, the same shape as `BL-522`'s `BarracudaPlanes`/`britpeace_5` case
(`docs/formats/mission-entities/enemy-generators.md`); CSVM's `--generators` still spawns the
placeholder `player_bhawk` there, since that `vehicle.params` chain is unbuilt. The second patrol
is therefore a generator launch, A5's domain (`BL-522`'s moving-spawner shape in
`EnemyGenerators.cs`), not `CampaignRoster.cs`'s.

**Approach.** No fix landed here. The spawn log's terrain probe is kept as a general instrument
(useful to any future roster-placement question); `BL-527` itself is redirected in `backlog.md` to
the generator launch pose decode, which needs a `vehicle.params` -> AIV template resolution plus
the ground-host launch shape `BL-522` already covers.

**Model recommendation.** medium.

**Verify.** Headless CM07 with the extended spawn log: all four `blakepeace_2_1`..`_4` land clear
of terrain (confirmed); D32.

**⚠ Traps.** No blanket spawn lift; `BL-457` shows authored spawns are otherwise exact, and the
roster formation here confirms it again.

## C23 ❌ `BL-518`: a stripe-textured surface stands in front of the zeppelin hangar

**Goal.** CM07's zeppelin hangar shows its open mouth, as the original does.

**Evidence (confidence: lead-only).** Reported at the controls. Candidates: an unresolved texture
on a hangar-door or interior-mask polygon, an alpha-blend sheet drawn opaque, or a node the setup
script should have deactivated.

**Disproof.** `--freecam --chapter=C1 --debug-names` at the Passenger Hangar location
(`extracted/C1/M02/zrdr/location.zrd.json`) shows the reported surface is the moored "Hollywood"
airship's nose (`rock_zeppelin`/`dliner1`, `dxzepskin.tif`), foreshortened head-on so its gore
stripes fill the door opening; `--debug-nodelab=node=rock_zeppelin,deps` confirms it is plain
world geometry (no destructible parent, no anim def anchored on it) with no reference anywhere in
`C1/M02`'s own compiled scripts (`egen`/`hangar_drop`/`hangar_panic`/`mis_anim`), so nothing in the
mission's own active-state handling touches it; it is baseline C1 chapter dressing, unconditionally
visible. `OriginalScreenshots/C1 IA1 Stunt Flying Zeppelin in Hangar.png` shows the same airship
filling the same doorway from a comparable angle, HUD-labelled `Danger Zone (Fly Through) -
Passenger Hangar`: the original authors this as a stunt-flying obstacle, not an open mouth, so the
"open hangar" the report expects never exists in the original either. No unresolved texture, no
opaque alpha sheet and no missing deactivation: the mission's active-state handling was never in
play, so nothing here belongs to this item's file scope.

**Model recommendation.** medium, low effort.

**Verify.** <pending orchestrator run>

**⚠ Traps.** The zeppelin hangar has door animations (`EnemyGenerators.cs:143`); confirmed the
stripe is not a door mid-animation (`C1/M02`'s two `egen` generators are the plain ground-airfield
shape, not zeppelin, and author no door state here) before reading it as the moored airship.

## C24 ☑ `BL-528`: the Blue Streak flies with the stock Bloodhawk fit and no nitro

**Goal.** CM07's player aircraft is the Blue Streak as authored, its own guns, hardpoints and
nitro, and the profile's hangar holds it after the mission.

**Evidence (confidence: lead-only).** Reported at the controls. The mission's `aiv.zrd`/roster
block for `player` is where the fit is authored (roster slot 34 is nitro, `BL-453`); our spawner
reads the profile's stock def instead. Re-verified against the code and the mission data: the
second clause holds, the first does not (see Outcome).

**Approach.** Read CM07's player block for the Blue Streak's fit; build the player's aircraft from
it for the mission (the same path `BL-453` needs for AI blocks); then check what
`CampaignProgression.cs` grants at the mission's end against the original's `CampaignProfileDef`
write. Represent the Blue Streak as a custom build carrying nitro (`CustomPlaneBuild.HasNitrous`),
never as a flag on the airframe. Decode lane: the post-mission profile write, for the second half.

**Model recommendation.** high. Touches the player build path, the roster reader and progression.

**Verify.** Headless CM07: the player's guns, hardpoints and `Nitro.Installed` match the block; a
progression unit test for the grant; D32 confirms the nitro dial and the hangar entry.

**⚠ Traps.** Do not give the stock Bloodhawk nitro.

**Outcome.** The in-mission half is disproved and the post-mission half is where the whole defect
lives. CM07's `aiv.zrd` `player` block authors no aircraft at all: field 0 (the vehicle def) is
`-1`, the nitro slot 34 is `0`, and the roster slot vocabulary carries no gun, hardpoint or engine
field, so there is no fit to read and `CampaignRosterPlan` is right to skip the block. The Blue
Streak is CM07's *award*, not its aircraft: reward record 7 (ordinal 7 = `seq` 6 = `C1/M02`) grants
airframe 3, `langui` 514.

The grant was the ownership row alone. `FUN_00405f00` copies a whole 204-byte template out of the
special-plane array at `0x0061a9b8` and only then writes the name over it, so in the original the
award IS a build; our `GrantAwards` added an `OwnedPlane` carrying a name and an airframe id and
nothing else, and the cabin's launch (`CustomPlaneStore.Load(plane.Name)`) then found no file and
flew the stock Bloodhawk with `Nitro.Installed` false. The user's own profile is the proof: it owns
`Blue Streak` (airframe 3, special) and flew it in `seq` 8, while `user://Planes/` holds one file
and it is not that plane.

All five templates are decoded into `docs/org/hangar.md`. Engine 4 on the Blue Streak is the
nitrous tier (`CustomPlaneBuild.HasNitrous`, ids 3 to 5); the other four take engine 1, so the
injector rides on that one build and no airframe gained a flag. `CampaignProgression.AwardBuild`
returns the template, `Record` hands the built defs back, `CampaignDirector` writes them to the
build store beside the profile save, and `CampaignProgression.BuildForOwned` resolves a reward
aircraft that was granted before this landed, which is what lets the D32 sortie see it on the
existing profile.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

## C25 ☑ `BL-529`: the Pandora porpoises along its route and past its end

**Goal.** CM08's Pandora holds steady pitch along its net and sits level at the route's end so
docking on it is flyable.

**Evidence (confidence: traced).** Reported at the controls; re-verified against CM08's own data
(C1B/M03, `piratezep` on net `Klondike1`): a 13-node open chain whose far end (node 0) authors NO
stop point at all, unlike C1/M04's own `PirateZep1` net, which happens to arm its far end under an
unaddressable stop-point id. `AiNetFollower.PickOnward`'s degree-1 short-circuit turns a dead end
INTO a re-pick of the node just left (the aircraft follower's own decoded rule), so a zeppelin
reaching Klondike1's bare end shuttled back and forth over the whole net's altitude swing (400 to
93 m) forever instead of stopping there, which reads exactly as "porpoises along its route and past
its end".

**Approach.** Decoded `FUN_004bf9d0` (the zeppelin's per-step law): the current node's own
no-further-edge flag calls the level/hold routine (`FUN_004bf500`, pitch 0, heading kept, speed 0)
UNCONDITIONALLY, ahead of and regardless of any armed stop point. `AiNetFollower` now reports this
same "structural dead end" condition through `StopsAt`/`Holding` for a caller observing stop points
(`ObservesStopPoints`, zeppelin-only), leaving the aircraft follower's own turn-back untouched.
Decode citations and the fix's shape: `docs/formats/mission-entities.md` "Route ends and stop
points", `docs/architecture.md`'s `AiNetFollower` entry.

**Model recommendation.** medium.

**Verify.** `zeppelin-motion`/`ai-net-follow` unit suites plus two new cases
(`AZeppelinFollowerHoldsAtAnUnarmedDeadEndInsteadOfShuttlingBack`,
`AnUnarmedDeadEndHoldsAndLevelsInsteadOfPorpoisingForever`); a new `zeppelin-pandora-dead-end`
engine suite row flies CM08's real Klondike1 net end to end, asserting pitch stays inside the
record's band throughout and the walk holds for good at the bare far end rather than shuttling
back.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

**⚠ Traps.** The original's initial-pitch clamp never fires (a unit bug kept verbatim,
`ZeppelinMotion.cs:35-37`); left untouched, as directed. The unconditional dead-end hold is opt-in
on `ObservesStopPoints`, so the aircraft net follower's own decoded "turns back at a dead end" rule
(a different, separately-decoded behaviour) is unchanged.

# Wave D — CM09 and the sortie

## D31 ☑ `BL-531` + `BL-532`: CM09's first patrol hangs in the air, and no enemy takes off from the ground hangars

**Goal.** CM09's first patrol flies, and its hangar enemies take off from the ground and reach the
fight.

**Evidence (re-verified against the code and the data; both reports are one cause).** CM09 is
C1/M04, whose `aiv` roster carries four `blakepeace_2_3`…`_6` Peacemakers authoring the taxi paths
`pp1`…`pp4`. Their authored coordinates are 300 and 400 m up, while every `ppN_aipN` waypoint of the
airfield sits at y=160, and `ScriptedPathVehicles.Place` seeded its follower from the body's spawn
pose. So the four hung motionless 140 to 240 m above the strip and hundreds of metres off it until
the mission's `START_TAXI` chain fired, and then taxied through the air: the stationary enemy patrol
of `BL-531` and the missing ground start of `BL-532` are the same four aeroplanes. The idle-patrol
theory was wrong; `AiPilot.FlyPatrol`'s netless branch flies the standing orders and was not
involved, and nothing in `AiModeMachine` was touched.

**Decode.** The original never uses the spawn record's own position for a path vehicle. Both entries
(`FUN_0047c210` at `0x0047c3a5`, the roster spawner, and `FUN_004940d0` at `0x004940f9`, the goal
that attaches a path later) read waypoint 0, add the vehicle type's ride height at `type+0x218` to
its Y, and take the attitude from the normalised leg into waypoint 1; the record's coordinates and
yaw are the branch taken only when no path resolves. Recorded in `docs/org/flightModel.md`.

**Landed.** `ScriptedPathVehicles.Place` now snaps the body onto waypoint 0 facing down the first
leg and writes that pose immediately, through one shared pose writer so the placement and the
per-tick step cannot disagree about which channel a body takes.

**Wiring contract handed to A5 (`BL-522`).** The generators themselves are a second, separate entry
into the same law and are NOT fixed here. `FUN_004518d0` names a non-zeppelin generator's own
take-off path from the host node with `sprintf("%.2s%.3s")` (first two characters, last three), so
`eairg31` resolves the host-relative `eag31_aipath` and `barracuda` resolves `bauda_aipath`; the
path is kept on the generator at `+0x24`, and a non-zeppelin generator with fewer than two waypoints
under it does not load at all. `FUN_00451bf0` then places the launch on waypoint 0 (+0.2 m Y at
`0x00451fa1`), faces it down the first leg, gives it zero velocity and a 1.0 throttle lever
(`+0x124`/`+0x128`), and sets `+0xcc = 1` leaving `+0xd4` clear, so it runs the strip at once
instead of waiting for a goal. C1/M04's two airfields each author a five-point run about 260 m long.
Until that lands, `AiGeneratorRuntime.Spawn` drops a ground launch at the host node with no run: in
the verification run below `eairg31`'s spawn flew and fought, `eairg32`'s flew into the airstrip
(`CRASH into a3/col ... surface=8/airstrip`) 120 m from its hangar.

**Verify.** Headless C1/M04 (`--campaign=<profile>:8 --debug-objective=8 --debug-spectate
--no-crash-loss`, 12000 sim frames): all four `START_TAXI` releases land, `blakepeace_2_4` takes
fire on the runway at y=161, `blakepeace_2_5` climbs out through y=198, both generators open their
doors and spawn on the deck at y=160, and the run ends with no engine error. The `scripted-path`
suite gained two checks on the snap. D32 judges it at the controls.

**Verified.** On the merged plan branch with main merged in: `RunTests.ps1` build clean, units
2458 passed / 0 failed, engine 152 suites passed / 0 failed with the error census clean, goldens
16 shots hash-identical, hitch awareness-only; D32 judges it at the controls.

**⚠ Traps.** The zeppelin launch-altitude gate does not apply to a ground start, and none was added.
`blakepeace_2_2` is authored at the world origin at y=0 with no net and no path, and our loader
spawns it there; the original reads the same record, so that is left alone rather than invented
around. Flying up to 80 km away is `BL-523` and stays there.

## D32 ☐ Fly CM03 to CM09 end to end and judge every item where it was reported

**Goal.** The user flies CM03 through CM09 on the landed build and each item above is judged at
the place it was reported; anything that still reads wrong is filed, not fixed in the sortie.

**Evidence (confidence: n/a).** The closing check every polish run has used; the user's eyes
outrank the instruments.

**Approach.** One mission at a time, with A2's kill key available for strays. Record each item's
verdict in the closing commit, close the plan, and mint follow-ups for anything new.

**Model recommendation.** medium. Orchestration and record-keeping; the flying is the user's.

**Verify.** The verdict list in the closing commit's message, one line per item, and `RunTests.ps1`
green on the final build.

**⚠ Traps.** Do not retune anything mid-sortie on one impression; file it.

### Sortie verdicts

**CM06 (C3/M03), the mission this plan's items call CM04.** Log:
`.scratch/logs/menu-20260827-221532.log` of the plan worktree, second C3/M03 run. Seven reports,
none clean:

1. No start cutscene. `calldestroy_the_cargozep` runs at bootstrap and its `cgzep_camera` call
   never takes the view; filed as `BL-569`. Not an A1 regression: `generic_intro` is not in M03's
   start list, so A1's deferred handoff is never entered here.
2. Buildings still explode at the start. The carrier A4 could not find is the persist-log replay:
   `CampaignPersistLog.ApplyTo` re-runs the C3/M02 kills through `DamageAt` at mission open
   (`aagun30..32`, `aagun01/02`, `g_tower1/3`, `u_camp1..3`, `unit10`, `t_truck02`, each with its
   death sequence). `BL-513` rewritten to that; A4's pool sync stays landed as the architecture fix.
3. Pandora already in the dry dock while the cargo zeppelin moves out. The zeppelin record seats
   `piratezep` at the END pose of the `pzep_todrydock` SI script (61.65 s), and the script never
   owns the pose; filed as `BL-568`, with the D31 snap and the C25 hold named as the first suspects.
4. Balloons not deactivated. Two leads in the log: the AI gunners engage `b_turret3/4/5` as live
   turrets on the first frames although the `.gw` switches them OFF, and a `balloon_downa*` call
   finds no callee because that reader def is compiled only into M02. `BL-521` rewritten.
5. Barracuda at the wrong position, no surfacing, then in the bay in one step, and facing the wrong
   way. `BL-512` extended with the verdict; A5's heading disproof is set against the report at the
   controls and re-judged in the built world on the next pass.
6. Fighters crash on launch and their wrecks kill the Barracuda. `britpeace_eg0..3` launch on the
   deck path and the AI ram rule destroys each on the same frame (`sub_doors`, `sub_runway`,
   `g627`); `BL-522` extended, `BL-515` cross-referenced for the crash damage.
7. Pandora's broadside fires on the player. Four `wep_28` hits on `P1` in the log; the user's rule
   is that the original's broadsides engage zeppelins only, and B11's own decode (the resolver
   matches names against the zeppelin roster) points the same way. `BL-517`'s disproof is reopened
   as `BL-567`.

A2's kill key is confirmed at the controls: `debug kill (F17): britpeace_2 crashed` and nine more
in the same log, each followed by the `downed` line.

**CM05 (C3/M04).** Log: `.scratch/logs/menu-20260827-225502.log`, first run. Two questions, no
new item: the far-off explosions at open are the persist-log replay again (`BL-513`, the same
fourteen camp objects re-killed at lines 158 to 171), and the mission has no intro cutscene in
the data (`NEW_GAME_START` is `player_setup`, `pzep_engines_start`, `flag_state_pirate`; no
`generic_intro` and no `camera1` def compiled into M04).

**CM06 (C1C/M01).** Same log, second run. Four reports:

1. A persistent flame stuck in the air, and the turret stuck with it. The death call
   `large_30sec_fire` goes through `ExternalEffect` as a world-position snapshot and
   `PlayEffectAt` stages it `TopLevel`; B14's disproof read the `PUFFER_STATE` path, which is
   not the death-call path. `BL-514`'s disproof is reopened as `BL-571`.
2. Objective markers read as green node names (`peoplehook`, `pzhookpoint`,
   `workersvoyagezep`). Filed as `BL-572`.
3. The docking ends too early and the player is teleported back onto the hook a few seconds
   after flying off. B13's mechanism (`BL-525`, the unparsed node-active
   `ACTIVATION_PREREQUISITE` letting the wrong hook-up leg run on the first docking) covers it;
   `BL-525` extended with the symptom.

**CM07 (C1/M02).** Log: `.scratch/logs/menu-20260827-232253.log`, first run. Five reports and
one confirmation:

1. The hangar launches come out in the air but crash at once: `blakepeace_2_eg1..eg4` each end
   on `AI ram into a5/col` at about `(-5940,165,-4164)`. C22's redirect holds; the residual is
   `BL-522`'s take-off run. `BL-527` extended.
2. An AA gun blew itself up firing at the barrier: `aagun32` takes `-10`, `-9.58`, `-9.2`,
   `-10` with no player round near it, and `aagun33/34/36` take the same pair. Filed as
   `BL-573`.
3. No animations in the hangar cutscene: `hangar_drop` starts at 67 m and the swap runs, but
   nothing of the hangar's own choreography logs. Filed as `BL-575`.
4. The Blue Streak in the mission has the Fortune Hunters livery and no nitro: the hangar
   hand-over is `AirframeSwapCodes` 965 → a stock `player_bhawk` through the ordinary player
   build. C24 closed the in-mission half on the roster block alone and did not look at this
   swap. Filed as `BL-574`; C24's award half is confirmed (the profile's Blue Streak has nitro
   after the mission).

**CM08 (C1B/M03).** Log: `.scratch/logs/menu-20260827-234016.log`; screenshot
`Screenshots/crimsonskies_2026-08-27_23-47-46-050.png`. Three reports:

1. The Pandora still pitches up and down along the Klondike net (nose down about 30 degrees in
   the screenshot, mid-route). C25's dead-end hold stands; the route-following half of
   `BL-529`'s report is reopened as `BL-576`.
2. The patrol boats never spawn: `patrolboat_1..4` are surface-vehicle roster blocks that
   `CampaignRosterPlan` skips, and CSVM has no surface-vehicle runtime. Filed as `BL-577`.
3. The tanker jumps and sits at the wrong position. Filed as `BL-578` (the `ObjectMotion`
   `rnd_xz` drift of `BL-512`, or a follower writing over the motion); whether the Pandora and
   the tanker play the cargo-crane choreography is still to be checked and is noted there.

**CM09 (C1/M04).** Log: `.scratch/logs/menu-20260828-002650.log`, three runs. Three reports:

1. The intro: the original plays the generic intro and then the player and wingmen dive out of
   the sky to the start point (`mission_intro_animation`'s `player` motion); CSVM opens at the
   start point with neither, the def's sound firing at bootstrap. Filed as `BL-579`.
2. Two Defend markers, one on the Pandora and one on a ground `rock_zeppelin` near the enemy
   zeppelin. `OBJECTIVE23`'s target is the path `[piratezep, rock_zeppelin]`, which
   `ObjectiveScript.ReadNames` flattens into two bare names matched globally. Filed as
   `BL-582`; the node-name text is `BL-572`.
3. With the Promised Land destroyed in both flights, the flight with the tower down never got
   the Paladin Blake squad (`OBJECTIVE20` via 17 → 19's 90 s nap under `TICK_DEPENDS_ON_OBJ
   29`) and so stalled on `DEDG [1, 0]` with four parked members. Filed as `BL-581` with the
   chain decoded, `BL-563` as the companion fix, and the objective graph's transitions absent
   from the file log as the tooling gap to close first. The extra Bloodhawk `player_bhawk_eg1`
   is `eairg32`'s launch falling back on a data typo (`Earig32_params`); filed as `BL-580`.

D31's four Peacemakers are in the mission: `blakepeace_2_3..6` are all downed in the log (three
by F17 in the air) and `blakepeace_2_eg0` launches off `eag31`; the user did not report them
hanging, which was the D31 symptom.
