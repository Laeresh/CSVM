# M5 Polish Run 8

**ACTIVE PLAN** (written 2026-09-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names the active plan when more than one is
present. Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to
[`plans.md`](plans/plans.md), when every item lands.

Ten player-visible defects in the delivered M1 to M5 game, selected from `backlog.md` on
2026-09-01 by the criteria the author approved that day: open, unblocked `[Bug]` items a player
meets in normal play first, with small high-value `[Tuning]`/`[Feature]` fills, excluding
everything `[Blocked]`, `[Owed-playtest]`, `[Research]`, menu-screen territory, and items
presupposing a future milestone. **Each of the ten was re-verified still-open on 2026-09-01**
against the record (`git log --oneline --all --grep=BL-nnn`), the current `backlog.md` entry,
`docs/PLAN-menu-presentations.md`, the worktree/branch list, and the cited code; where a cite had
drifted, the corrected `file:line` is in the item's Evidence below. The scheduled entries were
moved out of `backlog.md` into this plan in the same change that created it.

This plan runs beside the active [`PLAN-menu-presentations.md`](PLAN-menu-presentations.md) (Wave
A) and deliberately touches nothing that plan owns: no `LaunchMenu`, no menu screens, no campaign
board chrome. Two clusters were left in the backlog on their own recorded grounds: the
AI-mode-machine group (`BL-523`, `BL-550`, `BL-565`, `BL-566`), which three prior polish runs
each declined to split because it deserves its own run, and `BL-603`, whose first step is flying
the original game itself. First alternates if an item here dies early: `BL-618`, `BL-389`,
`BL-635`, `BL-656`.

## Milestone goal

- The campaign no longer softlocks CM09 on its met win condition, and every docking cutscene
  opens with one correct hook swing under intact letterbox bars.
- The cockpit panel's compass turns with the aircraft, nitro shows what the data authors, and a
  failed stunt run can no longer poison the persisted best time.
- Named campaign aces carry their authored durability, turrets acquire what the original's turrets
  acquire, a downed zeppelin plays its authored breakup, and a mid-flight AI spawn no longer
  hitches the frame.
- A closing sortie judges at the controls every landed item whose acceptance needs eyes.

**No menu work and no AI-mode-machine work.** Everything hosted by `LaunchMenu` belongs to
`PLAN-menu-presentations`; the `BL-523`/`BL-550`/`BL-565`/`BL-566` cluster stays whole for a
dedicated run.

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

### Wave A — Campaign flow

1. ❌ `BL-581` CM09 completes to the docking once the tower is down and every aircraft is killed
2. ❌ `BL-628` A docking cutscene's opening shot plays one hook swing, hooks parked at build
3. ☑ `BL-631` The letterbox bars draw over everything the episode flies past
4. ❌ Hook arms drawn full-size before their scale motion starts

### Wave B — Combat and AI

11. ☑ `BL-557` Roster `init_health`/`armor` overrides reach the named aces' spawns
12. ☑ `BL-626` Turrets acquire the candidate classes the original's turret picker holds
13. ☑ `BL-440` A downed zeppelin plays its authored breakup (`NodeUndercover` made real)
14. ◐ `BL-641` Introducing one AI aircraft mid-flight stays under the hitch threshold

### Wave C — Cockpit and flight feel

21. ☑ `BL-663` The cockpit panel's compass drum turns with heading
22. ☐ `BL-546` Nitro engage: reconcile the green suite with the smoke-free sortie, then settle the prop swap by decode
23. ☑ `BL-426` A failed stunt run records nothing and announces no best

### Wave D — Closing sortie

31. ☐ At-the-controls pass over every landed item that owes a judgement

## Dependency and parallelism notes

Waves group by theme, not dependency; every code item is independently landable, and D31 runs
last. Contention rules for parallel worktrees:

- **A2, B13 and C22 all reach the animation runtime family** (the reader/definition instantiation
  path, `AnimRuntime.cs`, and possibly `NameResolver`). Run them sequentially, or in one worktree.
- **B11 and B14 both edit the AI spawn/assembly path** (`AiFlightAssembler.Assemble`). Run them
  sequentially.
- B13 changes a global condition and C22 may add a resolution tier, so either can move goldens:
  take a baseline before, and every re-pin is user-reviewed.
- A1, A3, B12, C21 and C23 touch disjoint files and can run in parallel with anything above.
- **⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
  worktree session here; use a local commit or a file copy.

---

# Wave A — Campaign flow

## A1 ❌ `BL-581` CM09 completes to the docking

**Goal.** CM09 (C1/M04) goes on to the docking once the radio tower is down and every aircraft is
killed, instead of holding forever.

**Evidence (confidence: traced to code, both halves now).** The objective graph was already proven
correct and needs no change. The world half is now proven correct too, and the item's two named
leads are wrong: driven over C1/M04's own built world from the mission start to the docking,
`GroupLiveCount` reads groups 1, 2 and 5 exactly as `DEDG` needs, and `WAKEUP_ENEMIES` puts the
parked `blakebloodhawk_1/2/3/8` and the group-2 squad `blakebloodhawk_9..13` into play with the
team, group and patrol net they were built with. The chain closes: 23 (a Promised Land broadside
death) naps 24, 24 wakes the group-2 squad and 28, wiping that squad to two completes 28 and wakes
29, 29 releases the nap 19 is holding, 20 wakes the Paladin Blake squad, and with the hull down
and the three groups emptied 42/43/44 complete and nap 31, the docking, in with `pzhookpoint` on
the objective target list.

The one mechanism that produces the reported stall is the `TICK_DEPENDS_ON_OBJ 29` gate reading a
`piratezep` hull that is switched off. OBJECTIVE29 is `INACTIVE1 [piratezep]`, so it completes on
the tick it wakes if the hull is inactive, and 18 and 19 are then gated off for good, which is
exactly "the Paladin Blake squad never arrived". The mission's own intro switches that hull off for
its last shot (`playerthruclouds`) and switches it back on in the `RESET_STATE` that
`CutsceneController`'s handoff runs; a run with the intro but no handoff reproduces the stall
frame for frame. The shipped build does run that handoff (`cutscene: 'mission_intro_animation' ran
its authored RESET_STATE at the handoff`, at t=53.2 s), so the fault is not there either.

**Outcome.** Disproved as filed. Nothing on the world side needed a change, and the mission runs to
the docking in the current build. What is left is a flight question for D31: OBJECTIVE28 needs
three of the five `M4ZepAttack` bloodhawks down before Blake's squad arrives, and a player who has
cleared everything they met can be short of that with the tower down and no docking in sight. The
sortie log settles it in one line: `objective 28 completed` present means the chain moved.

**Landed.** `CSVM/src/Testing/CampaignDockingSuites.cs`, the `campaign-cm09-docking` engine suite,
which drives the whole chain over the mission's own built world and is the regression for it.

**Verify.** Reachable headless, and cheaper as a suite than as a flight:
`.\RunTests.ps1 -Suite campaign-cm09-docking -SkipUnits -SkipGoldens`. A live sortie is
`.\RunProbe.ps1 -TimeoutSec 900 --campaign=<throwaway profile>:8 --screenshot=<path>
--frames=4200`, whose `[campaign] objective N ...` lines land in `.scratch/logs/fly-*.log`; the
intro holds the graph for the first 53.2 s, so the frame count has to cover that before anything
scripted happens. Only a profile at the store's current version loads, and the win condition
itself needs a pilot, so the judgement stays with D31.

**⚠ Traps.** The Defend marker clearing (`OBJECTIVE25`) is correct. `BL-563` is a separate,
already-landed fix. The graph needs no change; re-deriving it is the recorded wasted hour. A suite
that drives this mission without hosting the intro's handoff reads a switched-off hull and
manufactures the very stall it is looking for. Cross-refs: `BL-565`,
`docs/formats/objectives.md`, the graph half's closing commit (`git log --grep=BL-581`).

**Verified.** <pending orchestrator run>
- `dotnet build CSVM/CSVM.sln`: 0 warnings, 0 errors.
- `dotnet test CSVM.Tests/CSVM.Tests.csproj`: all green.
- `.\RunTests.ps1 -Suite campaign-cm09-docking -SkipUnits -SkipGoldens`: 1 passed, 0 failed,
  engine errors clean, suite count 1 of 201.
- `.\RunProbe.ps1 --campaign=<throwaway copy>:8 --frames=4200`: the sortie log's `[campaign]`
  lines and `cutscene: … ran its authored RESET_STATE at the handoff`; the profile copy was
  deleted afterwards.
- `.\CheckCommentCaps.ps1`: clean.

## A2 ❌ `BL-628` One hook swing per docking

**Goal.** Any campaign docking's opening camera shot shows exactly one hook swing, with the hooks
parked retracted when the episode opens.

**Evidence (confidence: direction-sound; the supersede rule itself is a lead until confirmed
against the parse).** Reported at the controls and re-confirmed on the merged build in CM11's
closing autodock; airframe-independent (Devastator and Bloodhawk both show it) and present on
every docking since CM01. The hook engages correctly, so this is the animation running
repeatedly, not the dock failing. Two separate faults wear the one symptom:

- (a) The repeat, DISPROVED against the parse. `extracted/zrdr/player_hook.zrd.json` does author
  `player_extend_hook` under a plain `NAME`, and it is true that the two gates the census line
  names leave it standing. What the line does not name is the third rule: `AnimProgram.Add`
  deduplicates every surviving definition on its (`NAME`, `ANIMATION_NAME`) pair, and the compiled
  archives are loaded first, so the reader copy is dropped against
  `C*/cam_anim/player-player_extend_hook.json` and never becomes a second instance. The same rule
  disposes of the `blood_hook_extend`/`brig_hook_extend` twins. Statically, every archive names
  `player_extend_hook` exactly once, so there is no second call site either.
- (b) The pre-deployed hooks, DISPROVED. The `_startup` definitions are not what parks a hook:
  all four are `ON_CALL`, and the only definitions that call one are C4/M04's black-market hookup
  and its AI airframe states (`bhmhookup.zrd`, `bhm_warhawks.zrd`, `anim2_*-ai_*_state`,
  `player-bm_unhook_player`), which pose a hook on an AI aeroplane. What parks a player's hook is
  the aircraft archive's own inactive bit on the `<x>_hook` group, which `PlaneBuilder` copies onto
  the built group, and all eleven airframes ship that bit clear.
- (c) What is measurable and does look like the report. Each `<x>_hook_extend` switches its group
  and arm nodes on in its `state` sequence at t=0 while its `control` sequence starts the arms'
  scale motion a second later, and that motion's `from` is a collapsed scale. For that second the
  arm is drawn at the model's own full length, so the hook reads as already out before it visibly
  extends, and the collapse-then-grow that follows can read as a second swing. Measured on a driven
  docking: `pirate_hook_extend` shows `l_arm3`/`r_arm3` at `(1, 1, 1)` where the motion starts from
  `(1, 0, 0)`, `bal_hook_extend` shows `l_arm1`/`r_arm1` at `(1, 1, 1)` where it starts from
  `(1, 1, 0.5)`. Whether the original shows the same second is undecoded, so this is a reading for
  D31 rather than a fix.

Each airframe has exactly one hook definition bound to its own model (the Devastator's is
`pirate_hook_extend`, `extracted/zrdr/pirate_hook.zrd.json` authoring
`NAME1 [pirate_hook_extend, [player_pfighter, pirate_hook]]`), so no airframe is missing one.
Separately, `blood_hook_extend` and `brig_hook_extend` are each authored twice in `C*/cam_anim/`
(`…-1.json`, same `name`, same `anim_name`, one byte apart) in all eight chapters; that third
duplication is not what the Devastator hits.

**Approach.** Settle (a) first: one question about which reader definitions the compiled manifest
supersedes, and it reaches every airframe. Then (b): what parks a hook retracted at build for the
seven airframes authoring no startup. Any suite written for this counts the AIRFRAME BRANCH,
never `player_extend_hook`.

**Model recommendation.** High: a parse-rule confirmation with a high blast radius across every
mission's definition set.

**Verify.** A suite asserting the airframe branch's play count on a docking episode; then any
campaign docking watched at the controls (D31), opening shot alone: one hook swing, hooks parked
before it.

**⚠ Traps.** Do not silence it by latching "already played" on the runtime: a repeat a definition
authors is data, and a latch would hide the same defect wherever else it happens. The
player-visible hook engagement is correct today and must stay correct. Do not open this against
the node-state prerequisite again; it is enforced (ruled out two-sided, `git show 733d0218`). Do
not trust a suite that counts `player_extend_hook`: that is the measurement which reported this
closed while it was not (a PLAN-M5-polish-7 slot ruled out its candidate mechanism headless and
the next sortie re-confirmed the symptom). Cross-refs:
`docs/formats/anim-definitions/cutscenes.md`.

**Verified.** <pending orchestrator run>

Closed as disproved for both named faults, with the instrument the item asked for landed and no
production code changed. The confirmed supersede rule is three rules, not one: a mission-scope
reader definition is skipped unless the mission's compiled `mis_anim` lists its (`NAME`,
`ANIMATION_NAME`) pair; a shared- or chapter-scope `NAME1` multi-target definition is skipped
because the compiler expands it per instance; and everything that survives is then deduplicated on
that same pair by `AnimProgram.Add`, compiled first and therefore winning. Only the first two are in
the census line, which is where the plan's reading of it came from.

`landings-hookup-airframe` now counts the airframe BRANCH as well as the shared fork, reads how many
definitions the loaded program holds for each, checks all eleven airframes' hook groups against the
aircraft archive's active bit, and records each arm's pose at the instant its definition starts
beside the motion's authored `from`. Ran green over C3/M01 in the Balmoral and the Devastator: all
eleven groups ship inactive, one definition loaded for `player_extend_hook` and for each branch, and
`player_extend_hook`, `bal_hook_extend` and `pirate_hook_extend` each play exactly once. The only
repeats anywhere in the episode are `random_prop x12` and `wing_lights_blink x2`, neither in a
docking closure.

What is owed to D31: the reported repeat is not reproducible on any headless path and is not
authored by the data, so the remaining candidate is (c) above, the second in which the arm is drawn
at full length before its scale motion begins. The judgement to make at the controls is whether the
opening shot's hook is out before it extends, and whether what reads as a repeat is that
collapse-then-grow rather than a second play.

## A3 ☑ `BL-631` Letterbox bars are unoccludable

**Goal.** Nothing the episode flies past draws over the cutscene letterbox bars.

**Evidence (confidence: traced).** Seen at the controls in CM10 (C1/M05)'s closing docking
cutscene, where a walkway passes between the camera and the bars and is drawn on top of them;
re-confirmed on the merged build. The bars are world geometry, not a screen overlay:
`CutsceneController` binds the definition's own `letterbox` node (`BarsNode`,
`Session/CutsceneController.cs:26`), pins it to the cutscene camera by transform copy, and picks
each camera's field of view as the widest one the card still covers (near `:972-977`). Anything
closer than the card is in front of it, and depth decides the rest. (The entry's original cites
`:25`/`:846-863`, then `:916-933`, had drifted; symbols and mechanism are unchanged.)

**Approach.** Keep the card as data and make it unoccludable, by render priority or by taking it
out of the depth test, so its coverage no longer depends on what the episode flies past. Check
`OriginalScreenshots/CM10.mkv` first for whether the original occludes it.

**Model recommendation.** Medium: a narrow render-state change on one bound node.

**Verify.** CM10's docking, watching the walkway cross the frame (D31); goldens unchanged (no
cutscene golden exists, so the check is the full suite plus the sortie).

**⚠ Traps.** Do not solve it by moving the camera or narrowing the field of view: both are
computed from the card's own extent, so a change there moves the framing the definition authored.
The card is the original's geometry too, so the film is the check on whether the original
occludes it before any depth behaviour changes.

**Verified.** <pending orchestrator run>

`OriginalScreenshots/Videos/CM10.mkv` (3:58, 60 fps) was extracted with ffmpeg and read frame by
frame from t=205s onward. The letterbox bars first appear at t≈234s as the aeroplane closes on
Pandora's docking hook, and the recording ends at t≈238.6s, mid-approach under the hook, before
any walkway or other geometry crosses between the camera and the card. The film does not show the
occlusion the evidence describes; it is inconclusive rather than a disproof, since the capture
simply stops short of the moment in question. Proceeded on the evidence's own reading (world
geometry with no depth guarantee) rather than treating the film as a clearance.

`CutsceneController.BindWorld` now overrides the card mesh's material once it measures the card's
extent: `NoDepthTest = true`, `Transparency = Alpha` (moves the card into the sorted-transparent
pass, where render priority is honoured), `RenderPriority = 127` (the engine's ceiling). A first
pass duplicated the card's existing material, which review caught as wrong: every world mesh
carries `SceneBuilder.BiasMaterial`'s `ShaderMaterial`, which has neither property, so the
duplicate-then-set fell through to `?? new StandardMaterial3D()` and rendered the bars white. The
override is instead built fresh, reading the source shader's `albedo_color` parameter (the gamez
material the card's polygons reference, `Colored { color: {0,0,0}, alpha: 255 }` in every
chapter's `materials.json`) so it keeps the card's authored black, unshaded and both-sided
(`CullMode = Disabled`) so neither lighting nor the source polygon's authored sidedness changes
how it reads. The camera pose, the card's position and scale, and the field of view `FrameBars`
computes from the card's extent are all untouched. The `letterbox` definition only ever toggles
the node's `ACTIVE` state and its pose, never a colour or an opacity, so replacing the material
outright authors no fade the override could fight.

`dotnet build CSVM/CSVM.sln` and `dotnet test CSVM.Tests/CSVM.Tests.csproj` both pass (2785 unit
tests). The `cutscene-letterbox` suite's `LetterboxCardIsUnoccludable` check now reads the card's
own surface material (untouched by `MaterialOverride`) alongside the override, and asserts the
override's colour matches the authored one (or is opaque black if the source read fails),
`ShadingMode == Unshaded` and `CullMode == Disabled`, on top of the render-state flags; a
temporary reintroduction of the reviewed bug (duplicate-or-`new StandardMaterial3D()`) was
confirmed to fail this check before being reverted. `.\RunTests.ps1 -Suite cutscene-letterbox
-SkipUnits -SkipGoldens` passes. No cutscene golden exists, so no golden is expected to move; a
golden covering this shot would very likely be unaffected too, since the override only changes
what wins the pixel when something else claims it, not the card's own appearance.

## A4 ❌ Hook arms drawn full-size before their scale motion starts

**Goal.** Settle whether the second in which a docking hook's arm draws at full length, before its
`OBJECT_MOTION_FROM_TO` scale motion collapses and grows it, is a CSVM behaviour or the original's.
A2 found this window to be the only measurable match to the sortie report of a repeated hook swing,
and left when the original applies a scheduled motion's `from` pose undecoded.

**Evidence (confidence: traced to code for the runtime rule, traced to data for the poses).** The
original applies the `from` pose at the event's own start time, which is candidate (B), CSVM's
current behaviour. Three functions settle it.

- `FUN_004e9ee0` is `OBJECT_MOTION_FROM_TO`, dispatch slot 11 of the 47-slot table at
  `DAT_00727de0` (`FUN_004ee1a0` populates it; slot 10 is `OBJECT_MOTION`'s known `FUN_004e8fa0`).
  It writes the `from` pose only when the sequence run state at `run+0x20` is zero, which is its
  first dispatched tick, and integrates one frame of the authored `*_delta` in the same call. The
  channel flag word at `event+0xc` is `0x1` translate, `0x2` rotate, `0x4` scale, `0x8` morph; the
  `to` pose is snapped on the call where `run+0x28` reaches `RUN_TIME` at `event+0x8c`.
- `FUN_004ecbb0`, the sequence stepper, is a cursor and not a scheduler. Between events it reads the
  next event's start-time mode at `event+1` and value at `event+8` and compares it against the
  animation instance clock (`inst+0xb0`), the sequence clock (`run+0x24`) or the per-event clock
  (`run+0x28`). Until the gate opens it returns without dispatching anything, and the events behind
  the cursor wait with it. `pirate_hook_extend`'s `control` gives `CALL_SEQUENCE scale_larm3` a
  `start {offset Event, time 1.0}`, so the `scale_larm3` sequence does not exist as a run until one
  second in, and its `from` of `(1, 0, 0)` cannot reach `l_arm3` before then. Candidate (A) is
  disproved: there is no run list a pending event could be posed from ahead of time.
- Candidate (C) is disproved on both of its named routes. `OBJECT_ACTIVE_STATE` is slot 6,
  `FUN_004e8f40`, which calls `gwNodeSetActive` (`FUN_004cca30`, named by its own error string) and
  that function only toggles bit `0x4` of `node+0x24`; it writes no pose. `pirate_hook_extend`'s
  `reset_state` is empty and its `auto_reset_node_states` is false, so the definition establishes no
  base pose either.

What the node holds during that second is therefore the aircraft archive's own pose, and
`extracted/planes/nodes.json` authors `l_arm3` at scale `(1, 1, 1)` under a `pirate_hook` group the
archive ships inactive. The authored parked pose of the arm is the collapsed scale instead:
`pirate_hook_retract` takes `l_arm3` and `r_arm3` to exactly the `(1, 0, 0)` its extend starts from.
Nothing in a chapter's compiled set calls a retract or an `<x>_hook_startup` at mission load, so
every mission's first docking opens on a full-length arm that then collapses and grows, and every
docking after a retract in the same mission does not. The original's data and runtime produce that
same first-docking window.

**Approach.** No CSVM change. `FromToMotion.Create` is called from
`PoseChannel.HandleMotionFromTo` when the event is handled, which `SequenceRunner` gates on the
authored `start`, and the first `Tick` writes `from` advanced by one frame. That is the original's
order of operations. A change here would be a deliberate divergence from the data, and this plan's
ground rules make a correct disproof the deliverable.

**Model recommendation.** High, for the decode; nothing to implement.

**Verify.** No code changed, so no suite run is owed. The reading is recorded in
`docs/formats/anim-definitions.md` and `docs/formats/anim-definitions/cutscenes.md`, and D31 carries
the watch note that would overturn it.

**Verified.** `<pending orchestrator run>` Decode only: `crimson.exe` in Ghidra
(`FUN_004e9ee0`, `FUN_004ecbb0`, `FUN_004e8f40`, `FUN_004cca30`, `FUN_004ebfd0`, `FUN_004ee1a0`)
plus the shipped `C1/cam_anim` hook definitions and `extracted/planes/nodes.json`. No code touched,
so `CheckCommentCaps.ps1` has nothing to read; `CheckEncoding.ps1` covers the three edited documents.

**⚠ Traps.** Do not "fix" this by posing the node at the definition's start: that writes a pose the
data does not author and would break every other gated `FROM_TO`, whose `from` is meant to be picked
up at its own start time. Do not read the one-second window as the reported repeated swing without
the archive check first, because the window is invisible on any docking that follows a retract. A
`LOOP` rewind is the one re-entry that does not rewrite `from` (`FUN_004ebfd0` returns state 4,
which the stepper stores and the gate block treats like 0 without clearing), but no shipped hook
definition loops. Cross-refs: A2 (`git log --grep=BL-628`),
`docs/formats/anim-definitions/cutscenes.md`.

# Wave B — Combat and AI

## B11 ☑ `BL-557` Roster durability overrides applied

**Goal.** The mission roster's `init_health` and `armor` overrides reach the named aces' spawns,
so `hafury_1`-`_6`, `hkfirebrand_9`, the Black Hat Brigands and the rest fight at their authored
durability.

**Evidence (confidence: traced).** The original's roster spawn applies slot 7 `init_health` when
greater than zero and slot 66 `armor` when greater than or equal to zero, then the difficulty
scale (`docs/formats/ai-rosters.md`, `docs/org/vehicleDamage.md`). `RosterSpawnPlan` carries
neither value, `CampaignRosterPlan.Build` does not read them, `SpawnFor` cannot forward them, and
the assembler seeds airframe defaults; no `InitHealth` symbol exists in the tree. The census
(`analysis/aim-assist-ttk/Census-RosterDurability.ps1`) over the 414 extracted blocks finds,
among the 251 enabled non-player-team ones, 25 authoring a positive `init_health` and 20 a
non-negative `armor`, every armour value between 90 and 132 and every one an override that RAISES
durability above the airframe default. The insertion point now exists on main: `BL-556`'s close
(`git show 308784a8`) landed the difficulty scale at `PlaneStats.WithEnemyDurability` in
`AiFlightAssembler.Assemble`, exactly where these overrides apply, before the scale and the
jitter.

**Approach.** Add the two fields to `RosterSpawnPlan`, read them in `CampaignRosterPlan.Build`,
forward them through `SpawnFor`, and apply them in the assembler before the difficulty scale and
the jitter, at the `WithEnemyDurability` call.

**Model recommendation.** Medium: precise data plumbing along one named chain, with the gates
already specified.

**Verify.** Unit fixtures for both gates and the absent-slot case (a block stopping at 66 fields
leaves armour unset); the census script re-run as the ground truth; then the full `RunTests.ps1`.
TTK judgement, if wanted, happens in D31 at a known `--difficulty=`.

**⚠ Traps.** **A missing slot is not a zero.** Blocks are not fixed-width (field-count histogram
42/65/66/67/68/81) and 33 of the 414 stop at 66 fields, so slot 66 does not exist on them; a
reader that maps absent to `0.0` invents 18 armour-stripped hostiles in C2/M05, C2B/M04 and
C3/M01 that the data does not author. No shipped hostile authors `armor 0`. The two gates differ
and both matter: `init_health` only when `> 0`, `armor` when `>= 0`. The eight per-zone roster
slots are `-1` on all 414 blocks and stay parsed-and-ignored; do not revive that path. Direction:
this makes those enemies TOUGHER, lengthening time-to-kill on exactly the fights that should be
hard. Cross-refs: `Flight/Difficulty` (the scale this lands in front of), `BL-561` (open
research; needs this settled first, and any TTK reading taken at a known `--difficulty=`);
`analysis/aim-assist-ttk/FINDINGS.md`'s census of this field is superseded by the script beside
it, so quote the script.

**Verified.** <pending orchestrator run> Reading confirmed against `docs/org/vehicleDamage.md`
("Where the numbers come from at spawn", step 2): both slots are absolute whole-vehicle pool
values, not fractions or divisors. `init_health` (block`+0x28`) writes the health-pool max
outright when greater than zero; `armor` (block`+0x2c`) writes the armour-pool max outright when
zero or greater. Neither scales an existing pool or divides incoming damage; both stand in for
`PlaneStats.VehicleHealth`/`VehicleArmor` exactly where the airframe def's own `health`/`armor`
keys would otherwise land, ahead of the difficulty scale and the per-spawn jitter that then
multiply whatever they leave behind. Implemented as `PlaneStats.WithRosterDurability`, chained
before `WithEnemyDurability` in `AiFlightAssembler.Assemble`. Ran: `dotnet build CSVM/CSVM.sln`
(clean); `dotnet test CSVM.Tests/CSVM.Tests.csproj` (2791 passed, 0 failed, including the 6 new
`RosterDurabilityOverrideTests`); `dotnet format CSVM/CSVM.sln --verify-no-changes` (clean);
`.\CheckCommentCaps.ps1` (all blocks within cap); `analysis/aim-assist-ttk/Census-RosterDurability.ps1`
re-run against `Z:\CSVM\extracted` (414 blocks/53 files; 25 enabled non-player blocks author a
positive `init_health`, 20 of those also author a non-negative `armor` between 90 and 132; 0 author
`armor 0`; 18 enabled non-player blocks carry no slot 66 at all), matching the Evidence above with
no drift.

## B12 ☑ `BL-626` Turrets acquire the original's candidate classes

**Goal.** Boat, balloon and emplacement guns fire at the targets the original fires at, ships
included: CM10's patrol boats and lifeboat guns engage the hospital ship, CM12's patrol boats
engage the Spruce Goose.

**Evidence (confidence: traced on the CSVM side; the original's candidate-class list is the
item's own decode step).** Reported at the controls in two missions. In CM10 (C1/M05) the
lifeboats a downed attack balloon drops reach the water, drive their nets and shoot at nothing
while the Red Cross hospital ship sits in front of them; in CM12 (C2/M01) the `eshipg31`
generator's patrol boats never attack the Spruce Goose they are launched against.
`TurretController.AcquireTarget` takes the nearest live hostile **aircraft** inside
`DETECTION_RANGE` (`Flight/TurretController.cs:628`), and the scan set it fills was an aircraft
list (`:53`), so a vessel was not a candidate at any range and no later gate in the tick could
rescue it. The mission data expects otherwise: CM10 authors twelve `patrolboat_1..12` blocks in
`aiv.zrd` at `y = 0` and nine `bbtur` balloon turrets, and each `lifesaverNM` carries its own
gun. The original's behaviour is on film in `OriginalScreenshots/CM10.mkv`, where patrol boats
attack the hospital ship shortly after the start.
⚠ Corrected by the decode: the two named victims are not vehicles. CM10's Red Cross ship is a
world node (`shipshape`, animated by `redcross_ship.zrd`) with no `aiv.zrd` block and no
`targets.zrd` entry, and CM12's Spruce Goose is the `sprucegoose` world node carried by
`goosepath.zrd` and named by `targets.zrd`. Both are mission structures in the original's third
pool, not `VehicleList` entries, so widening the vehicle scan does not by itself reach either.

**Approach.** Settle from the decode which candidate classes the original's turret picker holds
(`docs/org/targeting.md` is the starting point; the aim assist already keeps a vehicle list
beside its aircraft list, `docs/org/aim-assist.md` "The four lists", so the classes exist to draw
on), then widen the acquisition to match. Scope this slot to turret acquisition only.
The turret candidate scan is **`FUN_0041f9c0`**, the shared range-gated picker, reached from the
turret gun update `FUN_004aabb0` at `0x004aaf9b` over a query `FUN_00422850` builds at
`0x004aaf5c`. It walks four pools against one running best cost: `VehicleList` (`DAT_0071dabc`,
every entry, unconditionally, and the list holds AI ground and sea vehicles beside the aircraft),
the turret list (`DAT_0071d914`, every entry), `MStructList` (`DAT_0071d33c`, gated on the
picker's 4th argument and then on each entry's `+0x8d` with gasbags excluded), and live fused
ordnance (`DAT_0064f78c`). The full reading, including the C2/M05 special case behind that 4th
argument and where `+0x8d` is written, is `docs/org/targeting.md` "What a turret's candidate set
holds".

**Model recommendation.** High: the decode step decides the fix, and a wrong class list changes
combat balance across missions.

**Verify.** CM10 with a dropped lifeboat and the hospital ship in frame, then CM12 as the Goose
passes the boats, both against the film (D31); the full `RunTests.ps1`.

**⚠ Traps.** Do not reach the behaviour by making a ship an aircraft-class entity so the existing
list picks it up. The aim assist's own vehicle list is a separate mechanism, its membership
settled and its candidates already carrying each hull's velocity; nothing here should reach into
it. A patrol boat's own gunnery runs through the AI mode machine (`BL-523`), so a turret-side fix
by itself does not make a boat shoot; that boundary is deliberate. Cross-refs: `BL-523`,
`docs/org/targeting.md`.

**Verified.** <pending orchestrator run> `dotnet build CSVM/CSVM.sln` (0 warnings, 0 errors),
`dotnet test CSVM.Tests/CSVM.Tests.csproj` (2785 passed, after adding the new suite's measured
weight to `analysis/engine-suite-weights.json`, which `SuiteShardsTests` requires), and
`.\RunTests.ps1 -Suite <name> -SkipUnits -SkipGoldens` over the new `turret-vessel-targets` plus
`carried-turrets`, `world-turrets`, `mission-off-turrets`, `turret-self-fire`,
`campaign-surface-vehicles`, `targeting-candidates` and `ranked-pool-carried-turret-dedup`: 8 of 8
pass, engine errors clean. The new suite builds C1B/M03's four woken patrol boats with the
mission's nine aircraft blocks left unbuilt, so the pool's `VehicleList` is the four hulls alone,
and a hand-placed emplacement on the player's team 84 m away locks the nearest hull to within a
millimetre; the same gun moved onto the hulls' team acquires nothing.
⚠ Landed as a partial. The scan now holds the whole `VehicleList`, which is the pool the decode
shows ships and boats ride. The two symptoms in the Evidence are not cleared: both name mission
structures, and no CSVM channel can offer one as hostile, since
`AimCandidateSet.AddStructures` and `ObjectiveSites.Collect` both stamp `AimAssist.NeutralTeam`
and the shared predicate refuses a neutral on either side. A teamed mission-structure candidate
source is the missing piece, and it is not turret-side. The picker's turret and ordnance pools are
decoded and still unscanned.

## B13 ☑ `BL-440` The zeppelin breakup plays

**Goal.** A downed zeppelin pitches over, sheds its six gasbags with splashes, and drops the
gondola, as the authored `killpzep` choreography specifies.

**Evidence (confidence: traced; the symptom is read from the data and the stub, never yet
watched at the controls).** The kill already plays the authored hull-death def
(`ZeppelinRuntime.PlayHullDeath` plays `killpzep`, e.g.
`extracted/C1/M04/mis_anim/piratezep-killpzep.json`), and that def's `main_altitude_check`
sequence is `Initial`, so it runs from the moment the def starts: it tests
`If NodeUndercover(gasbag4, −65 m)` (the condition's `node_index` 1 is the def's own first
support node, `gasbag4`, not `rock_zeppelin`) and, on the else branch, loops forever (`Loop -1`).
`AnimRuntime.cs` stubbed `NodeUndercover` to a constant `false`. The gate never opened,
`rotatezep` and `breakupzep` were never called, and the wreck sank intact.
Correction to the last clause: reaching the `StopSequence` does NOT end `floatdown`'s −3.5
gravity descent, because halting a runner never retracts what it already launched
(`docs/org/sequences.md`); what ends the descent is `MotionRuntime`'s contact tier against the sea.
What the data authors: `rotatezepdown` pitches `rock_zeppelin` 0 to −15° over 8 s and
`rotatezep` eases it −15° to −7° over 0.5 s at the break; `breakupzep` then fans out 13 same-tick
CALLs (the deepest authored fan-out in the game, the one that sized `SequenceRunner`'s cap):
`break1`…`break6` drop each gasbag under −9.8 gravity with a slow forward tumble and a
`bounce_sequence.water` of `hit_waterN` (a `huge_splash`, then `huge_ripple` 0.5 s later at that
gasbag), `breakunder` translates `underneath` −35 m over 2 s and deactivates it, and six
`break_[lr]eng[123]1` each gate on their own `NodeUndercover` before calling `destroy_pz…`.

**Approach.** The gate is the whole feature: a real ground/occlusion probe behind
`NodeUndercover`, since the motions themselves are `ObjectMotion`/`ObjectMotionFromTo`, which
`MotionRuntime`/`PoseChannel` already run. Decode the condition's `distance` operand first: it
arrives as a raw u32 (`3263299584` on the hull test, `3229614080` on the engines) and is not a
length until decoded. Correct the stub's own comment with the fix (its "all 473 uses sit in
`OnCall` definitions the bootstrap never reaches" premise is already broken by `killpzep`).

**Model recommendation.** High: a global condition change whose blast radius is every def that
reaches it, plus a u32 decode.

**Verify.** The goldens are the check for the global change: baseline before, compare after,
every moved shot attributed. Then an Instant Action `zeppelin_run`, torpedo the hull down, and
watch it pitch over, shed six gasbags with splashes, and drop the gondola (D31). `BL-291`'s
closed harness (`zeppelin_run` plus a `wep_14` pylon, `git log --grep=BL-291`) is the
spawn-and-kill rig.

**⚠ Traps.** (a) The stub is global: making the condition real changes every other def that
reaches it, so the goldens are the check. (b) The distance operand is not a length until decoded.
(c) Effect templates snap to absolute world points and never track a moving host
(`ZeppelinRuntime.cs:732`; the entry's `:524` had drifted), so a splash authored at a falling
gasbag has to be placed from that gasbag's position at the moment of the call. That trap needed no
code: the `CALL_ANIMATION` arm already resolves an `AT_NODE` site through `CallTargetSite` and
sites the effect from that node's live transform when the call fires, which is what
`hit_waterN`'s `AtNode gasbagN` reaches. Cross-refs:
`BL-291` (closed), `docs/architecture.md`'s `ZeppelinDamage.cs` bullet (the survivor-count kill
that fires the def).

**Decode.** `NODE_UNDERCOVER` is a signed vertical ray, not an occlusion cone. `FUN_004ec410`
takes the node's world position (`FUN_004cf200`), clears that node's own collidable bit at `+0x24`
for the cast (`FUN_004cd260`/`FUN_004cd210`), sets the query filter `0x40000` and the
stop-at-first-hit flag (`FUN_004c7620`/`FUN_004c75e0`), and casts from `(x, y, z)` to
`(x, y + d, z)` through the world segment query `FUN_004c8f70`, which reports the first cell node
carrying both the visible bit `0x4` and the collidable bit `0x10`. The `0x10` branch of the
condition evaluator `FUN_004ec080` reads the hit flag and answers false on a missing node or a
failed query. `d` is the record's own `+0x14` slot, a raw u32 holding the IEEE-754 bit pattern of a
signed length in metres: `3263299584` is `0xC2820000` = −65.0 and `3229614080` is `0xC0800000` =
−4.0. The sign is the direction, negative down and positive up, which is why the reader spells the
same token `NODE_NEAR_GROUND`. All 473 shipped operands decode to round values; `chuteman` is the
only carrier of the eight positive ones and shows both directions in one chain (ground within 15 m
below, or something within 32 m above, and only otherwise deploy the chute).

**Verified.** <pending orchestrator run>
`dotnet build CSVM/CSVM.sln` (0 warnings), `dotnet test CSVM.Tests/CSVM.Tests.csproj` (2791
passed), `.\RunTests.ps1 -Filter zeppelin -SkipUnits -SkipGoldens` (13/13, including the new
`zeppelin-breakup`), `-Filter anim`, `-Filter chute` and `-Filter motion` (5/5), and the goldens
comparison `.\RunTests.ps1 -SkipUnits -SkipEngine` with `CSVM_DATA_ROOT=Z:\CSVM`: 18 shots,
all hash-identical, no golden moved. That is structural rather than lucky: the probe answers false
with no `ContactMask` wired, and no golden or lab wires one.

## B14 ◐ `BL-641` The AI-spawn hitch reaches the threshold

**Goal.** Introducing one AI aircraft mid-flight no longer trips the hitch monitor: CM18
(C4/M03)'s generator launches stay under each frame's printed threshold.

**Evidence (confidence: traced, measured).** The two CM18 stalls are `cargozep1`'s first two
generator launches, on the authored `ind_period` 3 + `wave_period` 1 cycle (4.017 and 8.033 sim s
under `--det`'s 1/60 s step), each building a `Cargo_params` Black Swan through
`AiFlightAssembler.Assemble`. The shader-memo half already landed (`git show 8563f465`, merged
`ed035fd8`): sharing the generated shaders across builders took the frames from 363/325 ms to
156/117 ms at 1P and 337/323 ms to 135/117 ms at 4P, paired A/B, two kept launches a side. What
remains is genuine per-aircraft construction, attributed by stopwatch inside the `ai_spawn`
scope: about 50 ms crash rig (template staging, the wreck subtree, and 194 to 198 pre-warmed
emitters at about 19 ms), 22 ms `FlightController.Bind` (prop, wing-light and control-surface
animators, collider), 10 ms model build, 5 ms loadout, turrets and damage visuals. Re-attributed on
the current tree, where the same terms read higher and the pre-warm is what dominates the rig: 43 to
126 ms crash rig (22 to 88 ms of it the emitter pre-warm over 194 emitters, about 35 ms the template
stage), 29 to 31 ms `Bind`, 20 to 62 ms model build (the higher figure is the first launch, which
also pays the livery-pattern load), 7 ms `startprops`, 4 to 9 ms engine audio, and about 11 ms across
loadout, turrets, damage visuals, `Setup` and the world-root add.

**Approach.** The crash rig is the only block not needed for the aircraft to be observable
(`_worldRoot.AddChild(controller)` already runs before it), so build it a frame or more later
behind a "build it now if this aircraft dies first" guard. That alone leaves about 90 ms, so
reaching the threshold means spreading the whole assembly over three or more frames.

**Model recommendation.** High: spreading construction across frames without changing observable
spawn behaviour or `--det` byte-identity is judgement-heavy.

**Verify.** CM18 under `--det` at 1P and 4P, the same paired A/B discipline, frames 241 and 482
compared against their own printed thresholds; `HitchMonitor` quiet on the launches; the full
`RunTests.ps1` (the goldens guard the spawn's visible outcome).

**⚠ Traps.** The stall is far past `HitchMonitor`'s grace window and reproduces to the same sim
frame under `--det`, so it is not workstation noise (`docs/verification.md` PERF-12/13). The 4P
threshold reads about 75 ms rather than 46 ms because the relative term rides a slower rolling
median: compare frames against their own printed threshold. Do not re-derive the shader-compile
term; it is gone, and PERF-22 records it. How OFTEN CM18 pays this is a separate open question
(`GeneratorCycle` switches the capacity check off at authored `capacity <= 0` unless a
`WAKEUP_GENERATOR` has armed it, CM18 authors none, so `cargozep1` launches every 4 s up to
`max_active` 10 unasked; `docs/formats/mission-entities/enemy-generators.md` "Capacity rule and
limit" says the data does not establish that reading). It stays out of this slot. Cross-refs:
`BL-434` (the per-viewport splitscreen cost the same measurement pass profiled).

**Verified.** <pending orchestrator run> Landed the crash-rig half and stopped there, because the
rest cannot move without delaying the aeroplane's entry into the world.
`WorldEffectsFactory.BeginFlightCrashRuntime` opens the same build `BuildFlightCrashRuntime` runs, as
a `CrashRigBuild` a caller advances a step at a time; `AiFlightAssembler` hands it to a new
`CrashRigQueue` that `GameSession._Process` pumps once a frame through
`FlightRoster.PumpDeferredCrashRigs`, and `FlightController.ArmPendingCrashRig`/`EnsureCrashRig`
build it in place for any reader of `CrashRuntime`, `CrashAnchor` or `CrashDefs` and at the head of
`TakeProjectileHit`, `TakeCollisionHit` and `Crash`, so no aeroplane is ever shot at, flown into the
ground or destroyed while its rig is out of reach. The crash RNG stream is drawn at the request
rather than at the bind, so a deferred rig's seed follows introduction order and not pump order; the
queue drains strictly head-first for the same reason. The steps follow the build's own joints, and
the template stage is split one authored `effect_pools.json` pool slot at a time, since a phase whose
size is data-driven does not fit a frame budget as one block.

⚠ The deferral is armed by the first pump, not by construction, so the aeroplanes a session builds
BEFORE its first frame keep their rigs built in place. That rule is not tidiness: deferring them
moved `c1-targeting-hud` and `c1-ai-wreck`, the two `--ai=` goldens, and with it in place all 18 are
hash-identical again. There is no frame to spare during a build, and those aircraft are what the
first drawn frame shows.

Paired A/B under `--det` at 1P and 4P, two kept launches a side, the before side being this tree with
the queue handed to the assembler as `null` and rebuilt `--no-incremental`, each frame against its
own printed threshold:

      1P  frame 241: 249.8 / 170.9 ms  ->  131.1 / 132.0 ms   (threshold 40.0)
      1P  frame 482: 176.1 / 174.9 ms  ->   72.6 /  67.8 ms   (threshold 40.0)
      4P  frame 241: 169.2 / 175.0 ms  ->  133.7 / 140.8 ms   (threshold 40.0)
      4P  frame 482: 105.7 / 147.2 ms  ->   69.2 /  68.8 ms   (threshold 40.0)

`ai_spawn` attribution falls from 215.7/142.0 to 99.9/100.9 at 1P frame 241, from 160.9/159.5 to
52.4/49.1 at 1P frame 482, from 141.1/145.8 to 103.9/110.0 at 4P frame 241 and from 90.8/124.7 to
51.3/49.4 at 4P frame 482. Both launches still trip, so the item is ◐.

What remains, precisely. The launch frame still carries the model build and `FlightController.Bind`,
about 50 to 90 ms together, and neither can move behind the frame that puts the aeroplane in the
world without the aeroplane arriving late, which is the observable-behaviour constraint this slot was
given. The deferred frames are quiet except the emitter pre-warm, one `AnimRuntime.PrewarmEmitters`
call over 194 emitters costing 15 to 103 ms with no seam of its own; splitting it needs
`AnimRuntime.cs`, which A2/B13 own, and its `material_create` term (194 `ShaderMaterial` +
`MultiMesh` builds in `Effects/EmitterRenderer.cs`) is the PERF-22-shaped follow-up. Reaching the
threshold outright wants the assembly built AHEAD of the launch rather than after it, off the
generator's own authored cycle, which is a change to `AiGeneratorRuntime`'s launch declaration and
to spawn-index allocation, not to this file set.

Ran `dotnet build CSVM/CSVM.sln` (clean, StyleCop included), `dotnet format CSVM/CSVM.sln`,
`.\CheckCommentCaps.ps1` and `.\CheckEncoding.ps1` (both clean), `dotnet test
CSVM.Tests/CSVM.Tests.csproj` (2791 passed, after adding the new suite's weight to
`analysis/engine-suite-weights.json`, which `SuiteShardsTests` requires), the whole engine tier
(`.\RunTests.ps1 -SkipUnits -SkipGoldens`, 201 of 201 passing) and the goldens
(`-SkipUnits -SkipEngine`, 18 of 18 hash-identical, manifest unmodified). The catalog gains one
suite, 200 to 201: `ai-crash-rig-deferral`. Two suites needed a forcing call where they read
rig-owned state straight off a fresh spawn (`ai-damage-stages` reads the sink phase 2 wires,
`ai-wreck-fall`'s control arm nulls `DestroyDef`). ⚠ The new suite has to free both aeroplanes it
spawns: suites share one host node, and leaving one under it takes the node name a later suite's own
spawn wants, which Godot then renames out from under that suite's identity checks. That is what
`roster-spawn-names` and `flight-roster-transaction` failed on until the `finally` freed them, and it
only ever showed in a shared run.

# Wave C — Cockpit and flight feel

## C21 ☑ `BL-663` The compass drum turns

**Goal.** The 3D cockpit panel's compass drum turns with the aircraft's heading and agrees with
the screen-space heading tape.

**Evidence (confidence: traced).** The authored `gauges` subtree carries a compass drum the
original drives as `FUN_004d1a30(node, 0, -heading, 0)` at `0049f8fe` (`docs/formats/hud.md`,
"Cockpit gauges"); `GaugeCluster.HeadingDeg` now carries the same value `CompassTape` reads, and
`CockpitGauges` binds it onto the drum (found by the binary's own name, `compass`, falling back to
the data's own container name, `comp`) alongside `hundreds`, `thousands`, `speed`, `nitro_boost`,
`nitro_charge`, `ggarrow`, `mgarrow` and `pfhorizon` (`Flight/CockpitGauges.cs`). The screen-space
tape was never at fault: `CompassTape.HeadingDeg` is fed every frame from the nose
(`Flight/FlightController.cs:1810`, `Flight/FlightHud.cs:322`) and the control repaints
unconditionally in `_Process`.

**Approach.** Heading has to reach `CockpitGauges.Apply`, which today takes only `GaugeCluster`;
carry it on the cluster, keeping the rule that the 3D panel and the screen-space dials read one
already-computed state, after which the drum takes its angle the way the altimeter needles take
theirs.

**Model recommendation.** Medium: one plumbing step and one bind, with the traps already named.

**Verify.** Extend the gauge coverage so the drum's rotation is asserted against a driven heading
(the panel suites that pinned the altimeter needles are the pattern); then D31: fly a circle in
the cockpit view and watch the drum against the heading tape at the top of the screen; the two
show the same card and turn together, headings increasing to the left.

**⚠ Traps.** The node's name in the DATA is `comp` while the binary's own lookup string is
`compass`, so a bind by either name alone misses on some airframes (the same split that forced
`pfhorizon` over `horizn`). The needle writes are negated because a +Z node rotation is
counter-clockwise-positive, which the decoded `-heading` argument may already account for: the
sign is one to confirm on the panel rather than to derive. Cross-refs: `BL-113` (compass tape
tuning) shares the tape but not this node; `docs/formats/hud.md`'s "Known uncertainty" still has
compass north unverified as world −Z, and a wrong axis there would move both readouts together
rather than one.

**Verified.** <pending orchestrator run> Bound the drum by `compass` first, `comp` second
(`extracted/planes/nodes.json` carries `compass` as the mesh-bearing node on all 11 player
airframes and `comp` only as its non-mesh container, matching the trap's own binary-vs-data split).
Carried `GaugeCluster.HeadingDeg` from `FlightHud.Draw` alongside the existing horizon feed, and
turn the drum about its own Y axis by `-HeadingDeg` taken directly, the same "engine's own value,
not a re-derived screen angle" rule `pfhorizon` already follows, rather than through the
needle/altimeter path's extra negation. Settled the sign by reasoning rather than a probe: a card
that stays fixed to true north while its parent (the cockpit, riding the plane) yaws needs exactly
a `-heading` rotation in the parent's own frame to cancel that yaw and hold its world orientation,
which is the same physical behaviour `CompassTape` already documents as "headings increase to the
left". Extended `cockpit-interior`'s `DrivenPanel` (`WorldAndToolSuites.cs`) with a driven-heading
block pinning the drum's basis at 0° and at 40°, checking the authored translation is untouched and
a second write replaces rather than accumulates the angle. Ran `dotnet build CSVM/CSVM.sln`
(clean), `dotnet test CSVM.Tests/CSVM.Tests.csproj` (2785 passed), and
`.\RunTests.ps1 -Suite cockpit-interior -SkipUnits -SkipGoldens` (PASS, engine errors clean). No
suite or test count changed, so `SuiteCatalogTests` needs no edit. Owed to D31: an eyes-on flight
watching the drum against the heading tape, which is the check that would catch a wrong world axis
for north (the "Known uncertainty" entry) rather than a wrong sign on this rotation.

## C22 ☐ `BL-546` Nitro: the smoke contradiction, then the prop swap by decode

**Goal.** A nitro engage shows the exhaust smoke the data authors in a real flown session, and
the prop-swap half is settled either as a fix or as a decode-confirmed disproof.

**Evidence (confidence: traced for the smoke contradiction; the prop swap is decode-gated).**
The call-shape half already landed (`git show 4a567bdb`): `nitro_boost`/`nitro_decay`
(`plane_props.zrd`) author their anchor as NAME `warhawk`, which never resolves inside a
per-plane crash rig's own index, and `FlightController.AdvanceNitro` called `PlayWithin`, which
has no fallback for a NAME that fails to resolve; `AdvanceNitro` now uses the
`Play(name, PlaneModel, applyReset: false)` fallback the working `startprops`/`stopprops` call
site uses, and the `nitro-boost-anchors` suite (builds the real `player_warhawk` rig, plays
`nitro_boost` through the production call shape, asserts the puffer sustains) is green.
⚠ A sortie on the merged build contradicts that half: an engage flown at the controls shows no
exhaust smoke at all, so the suite is green while the effect it guards is invisible in a real
session. The suite builds its own `player_warhawk` rig, so the difference between that rig and a
flown aircraft's is the first place to look.

The prop swap (`OBJECT_ACTIVE_STATE`/`OBJECT_OPACITY_FROM_TO nitropropN`, `spin_nitrorotorN`,
`snd_nitrostart AT_NODE nitroprop1`) is still not visible, and the open question is why:
`extracted/planes/nodes.json` declares exactly 34 `nitropropN` nodes and carries BOTH a
bare-named root and a `player_*` root for nearly every aircraft (`warhawk`/`player_warhawk`,
`fury`/`player_fury`, and so on), yet building all eleven `player_*` rigs through `PlaneBuilder`
turns up only `staticpropN`; no `nitropropN` node is reachable from any flyable airframe's own
subtree. Reading A: the original also resolves the `NAME warhawk` anchor per-plane, the disc
never showed on a flyable aircraft there either, and the prop-swap half closes as a disproof.
Reading B: the original resolves `NAME` globally against the world/library set, finds the bare
`warhawk` root (which carries the discs), and DID show a prop swap that a per-plane-scoped
resolution structurally cannot reproduce.

**Approach.** First reconcile the suite with the sortie (suite rig vs flown rig; a fix that only
satisfies the suite again leaves the same gap). Then decode `FUN_004b2110`'s "play nitro_boost
def on the plane node" call (cited in `docs/org/flightModel.md` "Nitro") for whether the
original's anchor resolution is scoped to the calling vehicle or is a global NAME search; that
answer alone separates reading A from B, and it comes before any prop-swap code. If B, the fix is
a CSVM resolution change (a second NAME-search tier for this class of def, or a bespoke bare-root
lookup), not a call-site swap like the smoke fix was.

**Model recommendation.** High: a suite-vs-reality contradiction plus a decode whose answer may
turn half the item into a disproof.

**Verify.** A probe of a flown session (not the suite's own rig) showing the exhaust puffers
sustaining on engage; the decode's answer written to `docs/org/flightModel.md` "Nitro"; smoke
judged at the controls in D31. If reading A holds, the prop-swap half lands as a recorded
disproof with no code.

**⚠ Traps.** The suite's green is the misleading instrument here; settle which of suite and
sortie describes the shipped path before touching the prop swap. The same sortie reports no shake
on the engage: `BL-447`'s ledger edge there is the AI's `medium_aishake`, so whether the player's
own engage is meant to shake at all is an open question recorded in this item, not that one.
Cross-refs: `BL-447` (also the AI's `snd_nitro` blip, and the decay lockout `_nitroDecayLeftS`
stands in for), `docs/org/flightModel.md` "Nitro", the `nitro-boost-anchors` suite.

## C23 ☑ `BL-426` A failed stunt run records nothing

**Goal.** Losing an Instant Action stunt run neither records a time nor announces NEW BEST; a
completed run still records.

**Evidence (confidence: traced; cites re-verified this session).** The wrap-up path is
`InstantActionDirector.StuntSummary` (`Session/InstantActionDirector.cs:744-753`), which called
`store.RecordIfBest(key, run.Elapsed)` behind two guards and no third: the objective is
`ZonesFlown`, and the pilot has a `Stunt` run at all. Neither asked whether the run was finished,
so every end of a stunt mission recorded a time, a loss included, and a failed run won the
comparison almost every time because it ended early (`ScoreStore.RecordIfBest` takes any lower
total). The write persisted immediately to `user://stunt_scores.json`, so a bogus time became the
record a later honest run was measured against and, being unbeatably short, could never be
displaced by real flying. The fix's predicate already existed in the same file: the solo path is
correct by construction (`StuntScoreboard.OnRunCompleted`, `StuntScoreboard.cs:58`, only runs on
completion, using `StuntMission.AllComplete`, `:101`), and `pilot.Stunt is { AllComplete: true }`
is already used at `InstantActionDirector.cs:732`. `prevBest` was read before the record, in the
same method.

**Approach.** Gate the record per pilot on that pilot's run being complete, and fix the displayed
previous-best alongside (trap (b)). Two calls belong to the author and go to D31: whether a
failed run shows its elapsed time without the NEW BEST flag or shows no time (the original's own
behaviour is not decoded; the working default until judged is elapsed shown, no flag), and
whether to invalidate existing `user://stunt_scores.json` entries, since machines already hit
carry a poisoned file no code change repairs.

**Model recommendation.** Medium: the predicate exists; the care is per-pilot gating and the
display read-order.

**Verify.** Fail a stunt mission deliberately: the wrap-up claims no best and
`user://stunt_scores.json` is byte-identical afterwards; complete one: it records. Cover the
split splitscreen end (one pilot complete, one not) in whatever harness the gate lands in; then
the full `RunTests.ps1`.

**⚠ Traps.** (a) Do not gate on the mission's win/loss flag: Decision 10 of
`docs/plans/PLAN-instant-action.md` has every player fly their own zone set with the mission
ending when all of them are done, so a splitscreen mission can end with one pilot complete and
another not; the test belongs on the run, per pilot. (b) `prevBest` is read before the record, so
a fix that stops the write without touching the display would still show a stale figure. (c) The
store is in `user://`, not the repo; decide the poisoned-file question explicitly rather than
silently.

**Verified.** <pending orchestrator run> `InstantActionDirector.StuntSummary` gates the record on
`run.AllComplete` (this pilot's own `StuntMission`), never the mission's win/loss flag; `prevBest`
stays read unconditionally, before the gated `RecordIfBest`, per trap (b). The gate and read-order
are split into `internal static BuildStuntSummary(StuntMission, ScoreStore, string)` so a suite can
drive them directly. `ScoreStore` gained an `internal Load(string storePath)` overload so a suite
can point at a throwaway file rather than the player's own `user://stunt_scores.json`. Ran: `dotnet
build CSVM/CSVM.sln` clean; `dotnet test CSVM.Tests/CSVM.Tests.csproj` 2785 passed; `.\RunTests.ps1
-Suite instant-action-stunt-summary,instant-action,instant-action-end,instant-action-wrapup,
instant-action-zeppelin,results-board-shell -SkipUnits -SkipGoldens` all 6 PASS (new suite
`instant-action-stunt-summary` covers a lost run recording nothing over an empty store, a completed
run recording and surviving a reload, a shorter-but-incomplete run leaving a real stored best byte-
identical on disk, and the split splitscreen end via two independent `StuntMission`s); `dotnet
format CSVM/CSVM.sln --verify-no-changes` clean; `.\CheckCommentCaps.ps1 -Summary` clean;
`.\CheckEncoding.ps1` clean. The poisoned-`user://stunt_scores.json` question (trap (c)) is left
open per the Approach, for the author/D31. The full `RunTests.ps1` landing gate is owed to the
orchestrator.

# Wave D — Closing sortie

## D31 ☐ At-the-controls pass over the landed items

**Goal.** Every landed item whose acceptance needs eyes or a judgement gets both, and every
finding becomes a fix, a follow-up `BL`, or a recorded verdict.

**Evidence (confidence: n/a; this item consumes the others' playtest lines).** The checks owed,
by item: A1 CM09 flown to the docking if the headless run could not prove it; A2 any campaign
docking, opening shot alone, one hook swing from a parked start; A3 CM10's docking, the walkway
crossing the frame under intact bars; B11 optionally the feel of a named-ace fight at a known
`--difficulty=`; B12 a gun with a hostile hull in range, since the hospital ship and the Goose are
mission structures no CSVM channel can offer as hostile and the flight would only re-observe that;
B13 an Instant Action `zeppelin_run`, torpedo
the hull, watch the pitch-over, six gasbag drops with splashes, and the gondola; B14 CM18 flown
without a felt hitch on the generator launches; C21 a circle flown in cockpit view, drum against
tape; C22 an engage showing exhaust smoke, and the prop-swap verdict as decoded; C23 a deliberate
failed run then a completed one, plus the two author calls (failed-run time display, poisoned
`stunt_scores.json` invalidation).

**A4's watch note, for the opening shot of a mission's FIRST docking.** The decode says the
original opens on a full-length hook arm for one second, then the arm snaps to its collapsed scale
and grows back over the next second. If that is what the shot shows, the reading holds and the
remaining half of the reported repeated swing is somewhere else. If instead the arm is collapsed
from the moment the hook appears and only grows, the original parks it by a route the decode did not
find, and A4 reopens: the candidates then are a pose write on the aircraft archive's own load path
or a retract called at mission setup, not the animation runtime. Judge a mission's first docking
only, because any docking after a retract in the same mission cannot show the difference.

**Approach.** One session, mission order where missions are involved; the session reads the
sortie logs beside the author's reports, lands same-day corrections that are unambiguous, and
mints `BL` items for anything larger, per the standing rule that the author's eyes outrank the
instruments.

**Model recommendation.** High: reading reports against logs and deciding fix-now vs file is
judgement work.

**Verify.** Every code item's plan section carries a **Verified.** line naming what was seen; any
overturned reading is recorded in the item it overturns.

**⚠ Traps.** A live symptom is evidence about the build that was running: confirm which build and
worktree flew before minting anything. The sim clock can lag wall time on physics-bound late-C2
missions; check the hitch lines before blaming an authored rate.
