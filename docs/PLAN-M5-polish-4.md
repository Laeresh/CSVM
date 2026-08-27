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
2. ☐ `BL-534`: a debug key that kills the player's selected target
3. ☑ `BL-516`: CM03's AA turret never fires
4. ☐ `BL-513` + `BL-521`: CM04's start-state script reaches the visual swap but not the pools
5. ☐ `BL-512` + `BL-522`: the Barracuda's drive jumps, its launch faces the wrong way, and its fighters crash at once
6. ☑ `BL-556`: the A press that skips a cutscene or resumes from the pause menu fires a rocket

### Wave B — CM05 and CM06

11. ❌ `BL-517`: the Pandora's broadside fires on a friendly player
12. ❌ `BL-524`: a friendly patrol without a net flies away after its first fight
13. ☐ `BL-525`: the second Workers' Voyage docking completes without a docking
14. ☐ `BL-514`: a shot-down carried turret keeps burning where it was

### Wave C — CM07 and CM08

21. ☐ `BL-526`: the rope ladder never deploys
22. ❌ `BL-527`: the second patrol's Peacemaker spawns under the ground
23. ❌ `BL-518`: a stripe-textured surface stands in front of the zeppelin hangar
24. ☑ `BL-528`: the Blue Streak flies with the stock Bloodhawk fit and no nitro
25. ☐ `BL-529`: the Pandora porpoises along its route and past its end

### Wave D — CM09 and the sortie

31. ☐ `BL-531` + `BL-532`: CM09's first patrol hangs in the air, and no enemy takes off from the ground hangars
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
bootstrap and settled that flag) — the player starts on the mission's authored spawn, exactly as
with no flag, so whatever the intro's own progression tests the player's pose against still holds.
`GameSession` applies the withheld placement through `FlightController.Activate` the instant the
cutscene's `WorldHeld` callback goes false at the handoff (`SpawnPicker.TakeDeferredOverride`).
Chose **deferred to after the handoff**, not a no-op: a mid-mission `--pos=` launch is what makes
later items' targeted checks usable (Dependency note, A2's own text), and a no-op would strand a
`--campaign=` launch at the mission spawn instead of the position asked for.

Read off the log, per the trap above, not a frame count. Fixed build, C3/M05 ("Hawaii mission 2",
seq 1), `--headless --no-pads --campaign=<profile>:1 "--pos=0,5000,0"`: the withhold logs at spawn
(`spawn [override] withheld: an intro cutscene owns the session`), `generic_intro` ends at
**t=40.19 s**, and the placement lands right after (`spawn [override] pos=(0,5000,0) …`,
`campaign: --pos= applied at the intro's handoff`). The same mission with no `--pos=` hands off at
**t=40.18 s** — the fix reproduces the no-flag timing, both inside the documented 16–40 s window
(the unfixed build's first probe never reached this comparison cleanly: a connected pad's phantom
button leaked through as a skip at t=12.56s before `--no-pads` was added to the probe).

**Verified.** <pending orchestrator run>

## A2 ☐ `BL-534`: a debug key that kills the player's selected target

**Goal.** One key in the F13+ debug block kills `TargetSelection`'s current target through the
normal death path, so kill-count and `DEDG` objectives see the kill; inert with nothing selected;
no shipped binding.

**Evidence (confidence: lead-only).** Asked for at the controls: strays that fly off (`BL-523`) or
hang out of reach (`BL-531`) block an objective chain. `<TODO: re-verify still-open against the
code>`

**Approach.** Route the kill through `WeaponHit`/the crash path, not by freeing the node. A
zeppelin sub-part or turret as the selection kills that part. The key number comes from the user:
the F13+ block follows the physical rows on their keypad, so ask rather than pick the next free
one (`docs/cli.md`'s debug labs, `docs/org/targeting.md`). `<TODO: the key number, from the user>`

**Model recommendation.** medium, low effort. Mechanical wiring on an existing path.

**Verify.** In an Instant Action dogfight, select an enemy, press the key, and confirm the wrap-up
counts the kill; press it with no selection and confirm nothing happens. `targeting` suites
unchanged.

**⚠ Traps.** Kill through the damage model or objectives never fire. Debug only: no entry in the
shipped keymap.

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

**Verified.** <pending orchestrator run>.

## A4 ☐ `BL-513` + `BL-521`: CM04's start-state script reaches the visual swap but not the pools

**Goal.** In CM04 (C3/M03) the buildings and barrage balloons the setup script starts destroyed
are destroyed: the balloons are down at mission open, and a hit on a pre-destroyed building finds
no healthy pool and runs no destroy sequence.

**Evidence (confidence: lead-only).** Two controls reports on one mission with a likely shared
cause: buildings starting destroyed still run their destruction sequence when hit, and the
balloons stand intact where the original opens with them down. C3/C4 drive the balloons through
`bont*`/`balloon_t*`/`tether*` state events (`docs/formats/anim-definitions.md` 116). `<TODO:
re-verify still-open against the code>`

**Approach.** Read CM04's interp setup script for the `ObjectActiveState`/destroyed-state verbs,
check whether they run, and whether they reach the destructibles' HP pools as well as the nodes'
visual swap. If only the visual swap lands, the fix is at the pool. Decode lane: the original's
handling of a destroyed-state verb on a destructible, if the data shows the script running and
the pool untouched by design.

**Model recommendation.** medium.

**Verify.** `--campaign=<CM04>` headless: the balloons' state at first frame, and a scripted hit on
a pre-destroyed building logs no kill; a new engine suite on the start state if one fits the
harness. Then at the controls in D32.

**⚠ Traps.** Do not gate the sequence on the visual state alone; a building destroyed in play and
hit again is the same symptom on another path, and the fix belongs at the pool.
`PLAN-c3-balloon-kill-chain.md` settled the live kill chain for C3/M02; that is not this mission's
start state, so do not reopen it.

## A5 ☐ `BL-512` + `BL-522`: the Barracuda's drive jumps, its launch faces the wrong way, and its fighters crash at once

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

## A6 ☑ `BL-556`: the A press that skips a cutscene or resumes from the pause menu fires a rocket

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
two re-entry points flight regains input from — the `Inert` setter clearing (a cutscene's skip or
its own handoff) and `PollPauseAndHalt`'s halt-clearing edge (a pause-menu Resume, Restart, or a
dismiss, all of which route through the same `PauseState.Halted` transition) — and reads the
trigger released until the physical button lets go, at which point it disarms and a fresh pull
fires normally. `FireControl`'s own edge detector (`_rocketFirePrev`) is untouched; the fix is
entirely upstream of it, so the gun trigger (B) and the selectors are unaffected. Both
`Inert=false` sites (`Activate`, the wave/respawn re-entry) and the pause halt clearing are
covered by the same two hooks, closing the spawn-frame sibling the original comment named as well.
**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

## B13 ☐ `BL-525`: the second Workers' Voyage docking completes without a docking

**Goal.** CM06 (C1C/M01)'s second docking objective completes only when the player docks.

**Evidence (confidence: lead-only).** Reported at the controls. A self-completing docking points at
the trigger volume being satisfied by the airship's motion, or the first docking's state not being
cleared before the second objective arms. `<TODO: re-verify still-open against the code>`

**Approach.** Read CM06's `objectives.zrd` for the two docking objectives and their gates, then
trace `ObjectiveGraph` for what completed the second one. Decode lane: the docking objective's
completion test in the original, if the graph's reading of the gate is the question.

**Model recommendation.** medium.

**Verify.** Headless CM06 with the objectives log on, the second docking stays open until a docking;
a `campaign-objectives-*` suite row if the harness reaches it; D32.

**⚠ Traps.** `ObjectiveGraph.ScanForCompletion` resolves one objective per tick round-robin
(`BL-458`), so a completion can land frames after its cause. `BL-514` is the same airship and a
separate item.

## B14 ☐ `BL-514`: a shot-down carried turret keeps burning where it was

**Goal.** A carried turret's death fire rides the hull (or the sub-part's node) and stops when the
part is gone; a ground emplacement's fire stays world-anchored as it is.

**Evidence (confidence: lead-only).** Reported at the controls in CM06: after a Workers' Voyage
rocket turret dies, the fire stays lit at its former position while the zeppelin moves on. The
effect is parented to the world, or the part is removed and its emitter left behind. `<TODO:
re-verify still-open against the code>`

**Approach.** Anchor a carried turret's death effect to the sub-part's node or the hull, the same
`TopLevel` anchor question the trail effects went through (`trail-world-anchor` suite), keyed on
`TurretController.Site`, and stop it with the part. Decode lane: none expected.

**Model recommendation.** medium.

**Verify.** A suite in the `trail-world-anchor` shape for a carried turret's death effect; a
headless CM06 kill shows the effect's position tracking the hull; D32.

**⚠ Traps.** The fix is on the carried case only; the world emplacement is correct today.

# Wave C — CM07 and CM08

## C21 ☐ `BL-526`: the rope ladder never deploys

**Goal.** CM07 (C1/M02)'s pickup shows its rope ladder so the pickup step can be flown.

**Evidence (confidence: lead-only).** Reported at the controls. CM07 arms a pickup gate ahead of
its cutscene (`docs/formats/anim-definitions/cutscenes.md` 358, `C1/M02/zrdr/pickups.zrd`); the
ladder is an animated node the pickup script activates, and `BL-035` lists the event kinds the
runtime still drops, so a deploy driven by one of them is silent. `<TODO: re-verify still-open
against the code>`

**Approach.** Find the ladder's def and the event that shows it, then check the runtime's dispatch
for that event kind. Decode lane: the dropped event kind's handler in the original's dispatch table
(`PLAN-anim-original-match.md`'s 47-slot table), if it is one of `BL-035`'s.

**Model recommendation.** medium.

**Verify.** `--anim-lab` on the pickup def shows the ladder node active; D32.

**⚠ Traps.** If the fix is a new event kind, land it as such with its `docs/formats/` entry and
strike it from `BL-035`, not as a special case for the ladder. The train pickup cutscene fix
(`2aa7d77d`) is adjacent history.

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

**Verified.** <pending orchestrator run>

## C25 ☐ `BL-529`: the Pandora porpoises along its route and past its end

**Goal.** CM08's Pandora holds steady pitch along its net and sits level at the route's end so
docking on it is flyable.

**Evidence (confidence: lead-only).** Reported at the controls. `ZeppelinMotion` pitches toward
each node's altitude under the record's rate and accel limits and levels off only while holding
(`ZeppelinMotion.cs:126-131`); the follower's `Holding` state asks for pitch 0 (`FUN_004bf500`),
and a follower that re-targets the last node from past it, or wraps the net, never enters it.
`<TODO: re-verify still-open against the code>`

**Approach.** Log pitch and node altitude per step along CM08's net for the Pandora against the
record's limits (`docs/formats/mission-entities.md`); if the nodes are level and the pitch still
swings, the turn-rate law overshoots. Trace `Follower.Holding` at the route's end in the same run.
Decode lane: `FUN_004bf500` and the follower's end-of-net rule.

**Model recommendation.** medium.

**Verify.** A `zeppelin-*` suite row on CM08's net asserting pitch bounds and `Holding` at the end;
D32.

**⚠ Traps.** The original's initial-pitch clamp never fires (a unit bug kept verbatim,
`ZeppelinMotion.cs:35-37`); do not "fix" it here.

# Wave D — CM09 and the sortie

## D31 ☐ `BL-531` + `BL-532`: CM09's first patrol hangs in the air, and no enemy takes off from the ground hangars

**Goal.** CM09's first patrol flies, and its hangar enemies take off from the ground and reach the
fight.

**Evidence (confidence: lead-only).** Two controls reports. An aircraft spawned with no net and no
target may leave the stick centred with the throttle closed where the original's idle patrol still
flies. The hangar enemies are either a plain or moving spawner in `EnemyGenerators.cs` or roster
spawns released by a hangar-door animation; whether the generator triggered, the aircraft spawned
inside the geometry and crashed, or they spawned airborne elsewhere is open. `<TODO: re-verify
still-open against the code>`

**Approach.** Log the first patrol's mode, net and lever on spawn and compare with `PT-56`'s plant
test of patrol nets. Read CM09's `egen.zrd`/`aiv.zrd` for the hangar aircraft's spawn shape and
position, run the mission headless with the generator log on and follow each spawn's first
seconds. Decode lane: the idle-patrol behaviour with no net, and the ground-start launch shape.

**Model recommendation.** high. Two questions on the mode machine and the generators, after B12
and A5 have narrowed both.

**Verify.** Headless CM09 with the AI trace and generator log on: the first patrol's speed above
its stall floor within seconds of spawn, and each hangar spawn airborne and alive after its first
minute; D32.

**⚠ Traps.** The zeppelin launch-altitude gate does not apply to a ground start. An idle rule has
to come from the decode, not from a fallback net. Flying up to 80 km away is `BL-523` and stays
there.

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
