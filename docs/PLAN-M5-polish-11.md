# M5 Polish Run 11

**ACTIVE PLAN** (written 2026-09-04). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

Eleven player-visible defects in the delivered M1 to M5 game, selected from `backlog.md` on
2026-09-04 by the criteria the author approved that day: open, unblocked, `[Impact: high]`, size
`[S]` or `[M]`, first step `code`, `data` or `decode`, no `[Owed-playtest]`, no `[Research]`, no
`[L]`, nothing belonging to Milestone 6; and the nineteen findings of the CM02 to CM24 sitting filed
as `BL-713` to `BL-731` taken first, with older high-impact items filling behind them. One older
item, `BL-688`, is scheduled because two of the sitting's findings (`BL-725`, `BL-726`) are the two
branches of the unconditional `Live` it rewrites, and its own trap says the three must land
together. The items group into four waves by the system each opens: the zeppelin's guns and bays,
the objective markers, the cutscene hand-backs, and the mission flow. Excluded on the same day:
everything `[Blocked]`, everything `[Owed-playtest]`, the `[Research]` questions (`BL-731` among
the new ones), the low-impact findings of the same sitting (`BL-718`, `BL-720`, `BL-723`,
`BL-724`), `BL-704` and every other `[L]`, and the menu items `BL-703` to `BL-712`, which belong
to the presentation seam rather than to play. `BL-728` (AI evasion) stays in the backlog because
its own entry says it must be settled together with `BL-558`, and the AI-mode-machine group has
been declined whole for four runs on the ground that it wants a dedicated run; that stands.

**Each of the eleven was re-verified still-open on 2026-09-04** against the record
(`git log --oneline --all --grep=BL-nnn`, which returned filings and cross-references only, with no
landing for any of them), the current `backlog.md` entry, the live plans (none), and the worktree
and branch list. All eleven were re-read against the cited code in this session by three read-only
verification passes; every `file:line` held, two had drifted by a few lines and are corrected in
the item (`C21`), and three entries gained or lost a fact: `A2`'s named `pzep_broadsides.zrd` does
not exist in the extraction, `C23`'s premise that the runtime ignores the camera gate is
contradicted by `AnimRuntime.Start`, and `B13`'s roster block is already flagged as an objective
carrier on team 0. The scheduled entries were moved out of `backlog.md` into this plan in the same
change that created it. Remaining alternates if an item dies early, in order: `BL-715` (the custom
Devastator's pylon cap), `BL-717` (the Pandora's turrets on the Balmoral), `BL-716` (flak's hit
effect), `BL-698` and `BL-700` (the two wreck-rest residues).

## Milestone goal

- A zeppelin's rings stop shooting through the hull they are mounted on, a destroyed broadside
  falls silent, and a sunk submarine launches nothing.
- One aeroplane is one target: the objective flag rides the aircraft's own candidate, a dead beam's
  marker leaves the cycle, and CM21's Cabbie carries its marker as a friendly.
- Control comes back from a cutscene onto a pose the terrain allows, with nothing left hanging in
  the sky and one camera on the Blacke drop.
- CM19's Black Hats launch from their hook, and every mission end fades to black over the hold the
  original authors.
- A closing sortie judges at the controls every landed item whose acceptance needs eyes.

**Nothing here touches the menus, the AI mode machine, or the Gemini's cannon-bay ladder.** The
menu findings are a presentation seam with their own run; the mode machine wants a dedicated
decode run rather than a polish item; `BL-694` and `BL-695` are mid-diagnosis, and `A2` reads the
Dante's own record rather than porting theirs.

## Decisions (2026-09-04)

| # | Question | Decision |
|---|---|---|
| 1 | Which items | **The approved criteria above, sitting first.** Eleven items, because `BL-688` is pulled in as the enabler two sitting findings depend on. |
| 2 | Filing of the sitting | **A separate filing commit before the plan's move.** It landed as `5b71d0ad` from a concurrent session; this plan's commit only moves the scheduled entries. |
| 3 | `BL-725` and `BL-726` beside `BL-688` | **One wave, `B11` first, then the two branches on top of it**, per `BL-726`'s trap that a separate fix re-touches the first. |
| 4 | `BL-728` | **Left in the backlog.** Its entry binds it to `BL-558`, which is the declined AI-mode-machine group. |
| 5 | `BL-722`'s premise | **Kept as an item, downgraded to lead-only.** The runtime honours the callee gate in `AnimRuntime.Start`; the first step is a trace that says whether both cameras start at all. |
| 6 | The Gemini's bays | **Out.** `BL-694`/`BL-695` stay their own diagnosis; `A2` must not port their answer to the Dante. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `BL-722`: "the original must pick one camera by a gate the definition carries and CSVM does not honour" | `AnimRuntime.Start` (`CSVM/src/Mech3/AnimRuntime.cs:1771-1778`) skips a definition whose node prerequisite is unmet, and the two camera definitions carry exactly that gate (`activ_prereqs` on `blk_e_marker`, `active: true` for `bdrop_ew_cam`, `active: false` for `bdrop_we_cam`). Whether the flicker comes from both starting is unproven. |
| 2 | `BL-722`: "`bdplayer` calls `blacke_drop_east` and `blacke_drop` in turn before its 951" | `player-bdplayer.json` stops and invalidates both, then calls `dropped_blacke`, then raises 951. |
| 3 | `BL-713`: "read `pzep_broadsides.zrd` against `pzep_gasbags.zrd`" | Neither file exists under `extracted/`; the only `pzep_gasbags` assets are the `mis_anim` definitions (`piratezep-all_pzep_gasbags.json`, `gasbagN-finish_pzepgasbagN.json`). |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees; never use it in a
worktree session here. Use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — The zeppelin's guns and bays

1. ☐ `BL-714` A zeppelin's own turrets fire through its own hull
2. ☐ `BL-713` The Dante's destroyed broadsides keep firing, and their death lights no gasbag
3. ☑ `BL-729` A destroyed submarine keeps launching fighters

### Wave B — One aeroplane, one target

11. ☐ `BL-688` The roster objective marker is a stamped flag on the aircraft's own candidate
12. ☐ `BL-726` A Destroy Support Beam marker leaves the cycle when its beam dies
13. ☐ `BL-725` CM21's Cabbie carries its objective marker as a friendly

### Wave C — Cutscene hand-backs

21. ☑ `BL-719` CM14's aircraft no longer hang in the sky during a cutscene
22. ☐ `BL-721` CM17 hands the player back onto a pose the terrain allows
23. ☐ `BL-722` CM18's Blacke drop runs one camera and hands back clear of the ground

### Wave D — Mission flow

31. ☑ `BL-730` CM19's Black Hats launch from their hook
32. ☑ `BL-727` Mission end fades to black over the hold
33. ☐ Closing sortie: every landed item judged at the controls

## Dependency and parallelism notes

`B11` blocks `B12` and `B13`: all three edit `ObjectiveSites.cs`, and `B12`'s trap says a
separate fix re-touches the first, so the wave runs in order in one worktree. `A1`, `A2` and `A3`
are independent but `A1` and `A2` both read `ZeppelinRuntime` and `A2` and `A3` both touch the
generator-and-cannon death path; run `A3` beside `A1`, then `A2`. Wave C's three items all edit
`CutsceneController.cs`; `C21` and `C22` may run in parallel worktrees only if `C22`'s change stays
in the hand-back pose path and `C21`'s in the park set, and `C23` waits for both. `D31` touches
`AnimRuntime`/`CampaignDirector` and `D32` touches `CampaignDirector` and a new overlay; they can
run in parallel with a stated ownership boundary (`D31` owns `WakeAnim`, `D32` owns `Leave` and
the hold). `D33` is last and needs the author at the controls; it cannot be delegated.

---

# Wave A — The zeppelin's guns and bays

## A1 ☐ `BL-714` A zeppelin's own turrets fire through its own hull

**Goal.** A ring on the far side of a zeppelin holds fire at a player the hull is between it and,
for as long as the hull is between them; a ring with a clear line still fires.

**Evidence (confidence: traced, cause open).** Reported at the controls as sustained fire from a
far-side ring. `TurretController.WorldRayBlocked` (`CSVM/src/Flight/TurretController.cs:702-711`)
casts a `CollisionLayers.World` ray from the turret to 0.2 m above the target, excluding
`PlatformColliderRids()` (`:557-579`), which walks only the mount section `PlatformOf` resolves
(`:351-359`, "Deliberately the section, not the whole vehicle"). An emplacement has no host, so it
never takes the `_worldQuery` path (`:333-335`), and `TurretLineOfSightTests` covers only that
host-rig path. Of the entry's three hypotheses, the code rules out the first (exclusion scope) and
the third (host-rig path) by construction, which leaves the hull's colliders not being on the
World layer at the time of the test; that is inference by elimination, not a positive read, and
the verdict is cached for a random 1 to 2 s.

**Approach.** Build the headless probe first: park the player behind a pirate zeppelin's hull
opposite its active ring, in `--stage`-less `--fly` with `--pos`/`--direction`, and log the ray's
hit list per ring. Read which layer the hull sections' colliders are built on
(`SceneBuilder`/`WorldCollision` for a zeppelin, against `CollisionLayers.World`), and whether the
hull's colliders exist at all by the time the turret steps. Fix the layer or the timing, never the
exclusion; add the probe as a suite asserting no shot from the far ring over several cache
periods.

**Model recommendation.** medium. One mechanism, a diagnosis with three named candidates and the
first two already closed.

**Verify.** The new suite red before, green after; `.\RunTests.ps1 -Suite <new suite> -SkipUnits
-SkipGoldens` during the edit; `TurretLineOfSightTests` unchanged; the full `.\RunTests.ps1` to
land. At the controls (`D33`): any pirate zeppelin, fly along the hull on the side away from its
active ring.

**⚠ Traps.** A ring that fired once as the player crossed the hull's edge is the cache, not this
bug. Do not make aircraft cover: the decode says world geometry only. Do not widen the exclusion
to the vehicle; `PlatformOf`'s comment records that this is how rings shoot through their own hull.

## A2 ☐ `BL-713` The Dante's destroyed broadsides keep firing, and their death lights no gasbag

**Goal.** A destroyed Dante broadside leaves the volley, and whether a broadside's death should
light the gasbag above it is settled from the Dante's own record, one way or the other.

**Evidence (confidence: traced for the fire gate, lead-only for the gasbag link).** Reported at
the controls on CM24. `StepBroadside` (`CSVM/src/Session/ZeppelinRuntime.Cannons.cs:210-235`)
drops a cannon that fails `CannonAlive`, but `ZoneIsAlive` (`ZeppelinRuntime.cs:516-517`) returns
true for a cannon with no wired pool, and `WireCannons`'s own comment (`:60-61`) says "without a
pool a cannon zone never dies and never thins the volley". The Dante's record
(`extracted/C5/M04/zrdr/zeppelins.zrd.json`) authors `lbroad11`..`rbroad32` and
`num_healthy_required`; `ZeppelinSuites`' broadside suite (`ZeppelinSuites.cs:893-911`) proves
the thinning works on `piratezep` when a pool is wired, and nothing covers the Dante. No code ties
a cannon's `Destroyed` to a gasbag, and the mission's objectives name only the gasbags and
`killdtzep`. The entry's `pzep_broadsides.zrd`/`pzep_gasbags.zrd` do not exist (wrong-claim 3).

**Approach.** Run CM24 headless with `--debug-anim` and read the `WireCannons` outcome for the
twelve Dante nodes: whether each gets a pool, and if not, why the resolution misses. Fix the
wiring so the twelve die as `piratezep`'s do, and add the Dante's record to the broadside suite.
For the gasbag half, read the Dante's `mis_anim` definitions (`extracted/C5/M04/mis_anim/`,
the `finish_pzepgasbag*` and `all_pzep_gasbags` set) for anything a broadside's death triggers;
if nothing does, record it as an expectation carried over from CM14 and close that half without
code.

**Model recommendation.** medium. The fire gate is a wiring read; the gasbag half is a data read
that most likely ends in a documented disproof.

**Verify.** The broadside suite with the Dante's record, red on the unwired cannon before, green
after; full `.\RunTests.ps1`. At the controls (`D33`): CM24, kill two broadsides on one side,
watch for further muzzle flash from them and for any skin fire on the gasbag above.

**⚠ Traps.** The Gemini's cannon bays (`BL-694`, `BL-695`) are mid-diagnosis; do not port a CM14
answer here. A cannon whose `gunback` is destroyed but whose `healthy` node still answers a hit
is `BL-672`'s question. Say which half a finding is about.

## A3 ☑ `BL-729` A destroyed submarine keeps launching fighters

**Goal.** Once the Barracuda's `subhealthy` node goes inactive, its generator stops launching
after the same grace the Dante's bay takes.

**Evidence (confidence: traced).** Reported at the controls on CM04. The generator `barracuda`
authors `healthy [subhealthy]` (`extracted/C3/M03/zrdr/egen.zrd.json:6-10`); the decode says a
fixed installation's death is its `healthy` node going inactive
(`docs/formats/mission-entities/enemy-generators.md:96`). `AiGeneratorRuntime.NotifyHostDied`
(`CSVM/src/Session/AiGeneratorRuntime.cs:219-227`) matches on that name but has one caller,
`GameSession.cs:2627`, fed by `ZeppelinRuntime.ZeppelinKilled`. The destroyed transition for a
destructible zone is `AnimRuntime.DamageAt` (`CSVM/src/Mech3/AnimRuntime.cs:1531-1554`, the status
write at `:1546`), which calls nothing on the generator runtime.

**Approach.** Raise a death notification from the destructible side when a zone's `healthy` node
transitions to `Destroyed` (an event on `DestructibleRegistry` or `AnimRuntime` that
`GameSession` subscribes beside the zeppelin one), name-matched the way `NotifyHostDied` already
matches. Assert in `GeneratorCycleTests` (engine-free) and in `GeneratorLaunchCountSuites` that a
sub kill disables the bay after the 3 s grace.

**Model recommendation.** medium, low effort. One event, one subscription, the test shape exists.

**Verify.** The new assertion red before, green after; full `.\RunTests.ps1`. At the controls
(`D33`): CM04, sink the sub, wait a wave period.

**⚠ Traps.** Keep the 3 s grace unless the sub's script has its own timing; it is a deliberate
deviation for the Dante's race. Match on the `healthy` node's name, not the generator's, so the
notification reaches the same code path the zeppelin's does.

**Outcome.** `AnimRuntime.DamageAt` now raises `DestructibleKilled` with the def's own healthy-role
node name (found by scanning its Initial sequences, then its `RESET_STATE`, for the same
healthy/destroyed role words `NotifyHostDied` already matches) the instant a live kill sets a
pool's status to `Destroyed`, before `RunDeathSequence` runs the choreography. `GameSession` feeds
it into `AiGeneratorRuntime.NotifyHostDied` beside `ZeppelinRuntime.ZeppelinKilled`, so the
barracuda's `subhealthy` reaches the same disable-after-grace path the Dante's kill does; a fixed
installation is no longer a dead end for that call. The evidence's premise held: nothing but the
new event was needed, the matching and the grace timer were already correct. A pre-existing,
unrelated defect surfaced while proving this: the same live kill's own death choreography (via
`SyncDestructiblePool`) can revert a destroyed pool back to `Healthy` when its death sequence
deactivates a `dbase`-role node without a matching `destroyed`-role reactivation, as the
barracuda's own death does; the notification fires before that revert, so the generator disable is
unaffected, but the pool's own `Status`/`Health` bookkeeping is left wrong afterward.

**Verified.** <pending orchestrator run>

# Wave B — One aeroplane, one target

## B11 ☐ `BL-688` The roster objective marker is a stamped flag on the aircraft's own candidate

**Goal.** An aircraft that carries a roster objective flag is offered once, under its own name,
with Objective outranking Enemy Target where it is both; it is not selectable before it wakes or
after it dies.

**Evidence (confidence: traced).** Four reports at the controls (CM02, CM15, CM11, CM24), one
mechanism. `ObjectiveSites.CollectTargets` (`CSVM/src/Session/ObjectiveSites.cs:79-109`) merges
`targets.zrd`'s flagged entries with every `RosterObjectiveMarkers` key into one list; `Collect`
(`:166-196`) makes each a synthetic candidate with `Live = true` unconditionally (`:191`) and a
position read off the live rig with no wake or death gate (`RosterAircraftPosition`, `:287-291`),
where `CampaignDirector`'s DEDG walk gates on `!rig.Crashed && !rig.Deactivated`
(`CampaignDirector.cs:1158`). `TargetPool.Rebuild` walks vehicles then objectives with no identity
comparison (`CSVM/src/Flight/TargetPool.cs:60-93`). The aircraft's own candidate already carries
the right name through `AiSkills.RosterTitle` (`AiSkills.cs:277`) to `PlaneRoster.PlaneDisplayName`
(`TargetPool.cs:164`); the raw key shows only because the synthetic candidate sorts ahead and falls
back to `site.Target.Node` (`ObjectiveSites.cs:344`). `CampaignMarkerSuites` and
`CampaignStuntMarkerSuites` cover site resolution and the marker booking, and none asserts the
dedup against `TargetPool.Rebuild`.

**Approach.** Stamp the objective flag (and the slot-38 category line, which has no reader today)
onto the aircraft's existing vehicle candidate at roster spawn and at bay launch, delete the roster
branch out of `ObjectiveSites`, and let `TargetPool` rank Objective above Enemy Target on one
candidate. Add a suite that spawns one flagged roster aircraft and asserts one `TargetRef`, its
display name from slot 20, and no candidate before wake or after death.

**Model recommendation.** high. Cross-cutting between `ObjectiveSites`, `TargetPool` and the
roster spawn, with three missions' reports riding on one change.

**Verify.** The new suite; `CampaignMarkerSuites`, `CampaignStuntMarkerSuites` and the targeting
suites unchanged or updated with the reason stated; full `.\RunTests.ps1`. At the controls (`D33`):
CM02's Balmorals (one bracket, category and no name line), CM15's Tex, CM11's Stunt Plane, CM24's
Miles after his death.

**⚠ Traps.** CM02's three `britbalmoral_*` blocks author slot 20 empty, so the correct result is a
box with a category and no name line (`docs/org/targeting.md:668-670`). CM15's name is "Tex", not
"Balmoral". Never fall back to the `vehicle.zrd` class title (`targeting.md:686`). Miles is a
generator-launched template, so the stamp must reach an aircraft the bay launches. `BL-686` is a
feature proposing this seam and has landed no code.

## B12 ☐ `BL-726` A Destroy Support Beam marker leaves the cycle when its beam dies

**Goal.** A `targets.zrd` site whose resolved node is destroyed is no longer selectable, whether
or not its objective has completed.

**Evidence (confidence: traced).** Reported at the controls on CM21. A site candidate leaves
`ObjectiveSites.Collect` with `Live = true` unconditionally (`ObjectiveSites.cs:191`), and the only
removal is `RemovedByCompletion` (`:198-209`), which reads `graph.CompletedOf` against
`REMOVE_OBJECTIVE_TARGET` and nothing about the node. No read of `DestructibleRegistry` or a
`healthy` child exists in the file. No suite kills a beam mid-ladder.

**Approach.** After `B11`, gate a site candidate's `Live` on the resolved node's destroyed state,
the same `healthy`-inactive read `DestructibleRegistry` makes, and assert it in the targeting
suites with one of the six `rfspt*` beams killed before its objective completes.

**Model recommendation.** medium. One gate on a branch `B11` has just reshaped.

**Verify.** The new assertion; `CampaignMarkerSuites` unchanged; full `.\RunTests.ps1`. At the
controls (`D33`): CM21, destroy one beam, cycle targets.

**⚠ Traps.** This is the site branch of the same unconditional `Live` `B11` fixes on the roster
branch; do not start it before `B11` lands. `BL-400` (a curated Non-Aircraft cycle) is a feature,
not this.

## B13 ☐ `BL-725` CM21's Cabbie carries its objective marker as a friendly

**Goal.** From the mission's start the Cabbie shows an objective marker and is not offered as an
enemy target.

**Evidence (confidence: traced for the data, lead-only for the runtime).** Reported at the
controls on CM21. The roster block `autogyro_1` (`extracted/C5/M01/zrdr/aiv.zrd.json:124-169`)
authors team 0 (slot 3), title `MSG_CABBIE_NAME` (slot 20), objective flag 1 (slot 37, one of the
eight blocks in the install that author it, `docs/formats/ai-rosters.md` row 57) and help label
`MSG_OBJ_FOLLOW` (slot 39). The script names it in `OBJECTIVE20` (`TRAVELERS`), `OBJECTIVE28`
(`WAKEUP_ENEMIES`, `START_TAXI`) and `OBJECTIVE58` (`REMOVE_OBJECTIVE_TARGET autogyro_1 dz1`,
`objectives.zrd.json:1238-1248`); no `ADD_OBJECTIVE_TARGET` names it, so the marker comes only
from the slot-37 stamp `B11` rewrites. Why the marker is missing and the aircraft reads hostile at
the controls is not traced: it is either the roster branch not reaching this block or the team
resolution on spawn.

**Approach.** After `B11`, run CM21 headless with the campaign trace on and read the Cabbie's
candidate: its team on spawn, whether `RosterObjectiveMarkers` holds `autogyro_1`, and what
`B11`'s stamp gives it. Fix whichever of the two is wrong and pin CM21's block in the marker suite.

**Model recommendation.** medium. A trace on top of `B11`, one block.

**Verify.** The marker suite with CM21's block; full `.\RunTests.ps1`. At the controls (`D33`):
CM21 to the taxi's take-off.

**⚠ Traps.** `WAKEUP_ENEMIES` on a friendly is the verb's name, not its team; do not infer
hostility from it. `BL-635`'s closing commit (`git log --grep=BL-635`) is CM11's stunt planes on the
same marker path; read it before re-chasing the stamp.

# Wave C — Cutscene hand-backs

## C21 ☑ `BL-719` CM14's aircraft no longer hang in the sky during a cutscene

**Goal.** While CM14's intro or docking cutscene plays, no AI aircraft is drawn motionless in the
sky.

**Evidence (confidence: traced mechanism, lead-only cause).** Reported at the controls on CM14
("Planes hanging in the air during cutscene"; which cutscene is not recorded). Code 913 parks and
hides every AI vehicle not in the cutscene, 914 reveals it (`docs/formats/anim-definitions/cutscenes.md`,
"CALLBACK code reference"). `CutsceneController.ParkAi` (`CSVM/src/Session/CutsceneController.cs:869-888`,
the `Parked`/`Inert` writes at `:883-884`) parks what it finds; `RevealAi` (`:892-897`) un-parks
only what it parked, by its own comment. `FlightController.ApplyPresence`
(`CSVM/src/Flight/FlightController.cs:2232-2244`, the pivot hide at `:2238`) hides the pivot on
inert. So a parked aircraft should be invisible; the visible ones are outside the park set. The
cited lines had drifted by five and are corrected here.

**Approach.** Run CM14's intro and its docking (`hooked_to_klondike`) headless with the cutscene
trace on and list every aircraft still drawn during the film against the park set. The three
candidates in order: an aircraft spawned after 913 by a wave or generator the park never touched,
a roster aircraft already `Inert` before the park and so skipped, or a rig whose visible part is
not what `ApplyPresence` hides. Extend the park to the late spawns (or gate spawning during a
film) as the data says, and add a suite that spawns a wave mid-cutscene and asserts it is hidden.

**Model recommendation.** medium. A trace with three named candidates.

**Verify.** The new suite; `CampaignSuites`, `CaptureGroupSuites` and `AirframeSwapSuites`
unchanged; full `.\RunTests.ps1`. At the controls (`D33`): CM14, fly to the Pandora with enemies
alive, dock, watch the sky during the film.

**⚠ Traps.** Check both cutscenes. Do not hide aircraft by team or by distance.

**Outcome.** Fixed for the intro; the docking cutscene reads clean. A headless trace over CM14
(C2B/M04) killed all three named candidates as the direct cause: `SessionSimulation` returns
before the generator phase whenever the world is held, so a wave cannot spawn mid-hold at all;
`ParkAi`'s own `!ai.Inert` gate correctly leaves a genuinely dormant roster aircraft alone; and
`ApplyPresence`'s pivot hide already works, proven separately by the passing wing-walk park in
`CaptureGroupSuites`. The real cause sat upstream of all three: CM14's own intro definition
(`generic_intro`, shared by twelve of the thirteen story missions) authors no `CALLBACK` 913 at
all, only the bespoke C1/M04 intro does, yet the decode says the original parks every AI vehicle
imperatively at every new-mission start regardless of what codes that mission's own intro data
authors. `CutsceneController` only parked on a literal 913 dispatch, so an aircraft already flying
when a non-C1/M04 intro opened (a Gemini fighter the mission's own generator had launched, over
CM14) stayed drawn and motionless for the whole intro. `CutsceneController.Act`'s `CodeHoldsWorld`
case now forces the park the instant an intro takes the session, whether or not 913 is later also
authored. The docking cutscene (`hooked_to_klondike`) is untouched: it authors no 913 in the
original either, and its background AI is meant to keep flying through a hookup or a drop. The new
`cutscene-ai-park-intro` suite proves the fix over CM14's own generator and roster data, red before
the change (the park assertion failed) and green after; it also proves the generator stays silent
through the hold and resumes once the intro hands off. This does not settle whether the same
imperative gap affects `player_setup`'s Instant Action bootstrap, which authors the same nine codes
outside a story mission and is out of this item's scope.

**Verified.** <pending orchestrator run>

## C22 ☐ `BL-721` CM17 hands the player back onto a pose the terrain allows

**Goal.** After CM17's intro the player is in flight at the authored spawn, not against the
terrain, and no jump or hitch follows.

**Evidence (confidence: traced mechanism, lead-only cause).** Reported at the controls on CM17
("Crash after first cutscene. Plane placed against terrain"; "Some hitches after the crash.
player plane jumped around"). `NEW_GAME_START` runs `generic_intro`, `pzep_engines_start` and
`deactivate_bmhookup_node` (`extracted/C4/M02/zrdr/startanims.zrd.json`), `location.zrd.json` is
`[null]`, and the intro raises no 951, so `CutsceneController` takes the no-951 branch
(`CSVM/src/Session/CutsceneController.cs:644-658`) and hands back through `StagePlayerAircraft`,
the pose held before the intro staged it. `BL-633` covered a 951 naming no `player`, which this
mission does not author. `CampaignBlackeSearchSuites` is scoped to CM17 but covers the later
`WARP_VEHICLE`, not the intro hand-back.

**Approach.** Run CM17 headless past the intro with the spawn and cutscene traces on; print the
pose `StagePlayerAircraft` restores and the terrain height under it, and compare with the pose the
authored spawn and `generic_intro`'s last `player` keyframe end on. Fix the restored pose (or the
spawn it was captured from), then read the post-crash jumping off the same log as the respawn
path on the same pose. Pin the hand-back pose's clearance in `CampaignBlackeSearchSuites` or a
sibling.

**Model recommendation.** medium.

**Verify.** The new assertion; full `.\RunTests.ps1`. At the controls (`D33`): CM17 from the
briefing, watch the first second after the intro.

**⚠ Traps.** CM17's Blacke search warps the player (`git log --grep=CM17`); confirm the crash is
at the intro, not after that warp. The respawn jumping is read, not fixed, until the pose is.

## C23 ☐ `BL-722` CM18's Blacke drop runs one camera and hands back clear of the ground

**Goal.** The drop cutscene holds one camera for its length, and control returns on a pose that
does not crash the player.

**Evidence (confidence: lead-only; see wrong-claims 1 and 2).** Reported at the controls on CM18
("Camera jumps back and forth really fast and plane often crashes directly after cutscene").
`blacke_marker-blacke_drop.json` calls `bdplayer`, `bdchute`, `rem_pas`, `bdrop_ew_cam` and
`bdrop_we_cam` unconditionally inside one `PlayerRange 4096` block; the gate is on the callees:
`camera1-bdrop_ew_cam.json` requires `blk_e_marker` active, `camera1-bdrop_we_cam.json` requires it
inactive. `AnimRuntime.Start` (`CSVM/src/Mech3/AnimRuntime.cs:1771-1778`) skips a definition whose
node prerequisite is unmet, citing CM07's hangar drop as the same pattern, so the flicker is not
explained by the runtime ignoring the gate. `bdplayer` stops and invalidates
`blacke_drop_east`/`blacke_drop`, calls `dropped_blacke`, then raises 951 naming `player`, so the
hand-back pose is a re-placement. No suite references `blacke_drop`, `blk_e_marker` or the two
cameras.

**Approach.** Trace first: run CM18 headless to the drop from each side with `--debug-anim` and
read whether both camera definitions start, and if only one does, what else writes `camera1`
every frame (a `dropped_blacke` leg, a second caller, a per-frame reparent). Fix what the trace
names; assert in a cutscene suite that exactly one of the pair starts per side. Then read the 951
pose against the terrain on each side, as `C22` does.

**Model recommendation.** high. The premise is already partly wrong and the fix is inside the
animation runtime.

**Verify.** The new suite for both approach sides; full `.\RunTests.ps1`. At the controls (`D33`):
CM18, approach the drop from each side.

**⚠ Traps.** Do not mute one camera by name; the pair is symmetric for a reason. `BL-699`'s
launch-frame hitch is on this mission and is not this. A trace that shows one camera starting
means the flicker is a different writer, and the item's title changes with it.

# Wave D — Mission flow

## D31 ☑ `BL-730` CM19's Black Hats launch from their hook

**Goal.** CM19's `bhatwarhawk_*` and `bhatbrigand_*` roster aircraft appear when their objectives
wake `launch_warhawk`/`launch_brigand`.

**Evidence (confidence: traced data, lead-only runtime).** Reported at the controls on CM19 ("no
enemies fighters spawn"). `egen.zrd.json` is `[null]`; the twelve roster blocks are woken by
objectives authoring `BEGIN_DORMANT -1` and `WAKE_ANIM launch_warhawk`/`launch_brigand`
(`extracted/C4/M04/zrdr/objectives.zrd.json:1948-2362`); the definitions sit on `warlaunchhook`
beside `ai_warhawk_place` and `ai_brigand_place` (`extracted/C4/M04/mis_anim/warlaunchhook-*.json`).
`ObjectiveScript` parses `WAKE_ANIM` (`CSVM/src/Session/ObjectiveScript.cs:709-714`) and
`CampaignDirector.WakeAnim` (`CampaignDirector.cs:1323-1330`) plays it through
`PlayMissionTrigger` generically, logging `campaign: WAKE_ANIM '<anim>' started N definition(s)`;
nothing ties an `ai_*_place` definition to un-dormanting a roster aircraft. No suite references
`launch_warhawk` or the place definitions.

**Approach.** Run CM19 headless with the campaign trace on, confirm the `WAKE_ANIM` fires and how
many definitions start, then read what `launch_warhawk` and `ai_warhawk_place` do to the aircraft
node (activate, reparent, place) and what `AnimRuntime` does with a roster aircraft the definition
places. Implement the missing step the definitions author (most likely the place definition's
activation of a dormant roster rig), and pin it in a campaign suite that wakes the anim and asserts
a live Black Hat.

**Model recommendation.** high. A launch-hook mechanism no other mission uses, read from the
definitions rather than from a decode.

**Verify.** The new suite; full `.\RunTests.ps1`. At the controls (`D33`): CM19 to the first
warning.

**⚠ Traps.** The mission also swaps the player onto the Warhawk (966), so "no enemies" may be
read after the swap; check the timeline. Do not fall back to spawning the roster enabled.

**Outcome.** A Black Hat now leaves the hook on the launch definition's own `CALLBACK`. The
hypothesis that the place definition activates the roster rig is DEAD: `ai_warhawk_place` only
parents the display node `anim2_warhawk` under `bmhookpoint` and translates it, and `launch_warhawk`
switches that same display node on and off around an SI-script hook run. What ties the definition
to the roster is the code it raises, 801, which the repo had already decoded
(`docs/formats/anim-definitions/cutscenes.md`: 801 to 803 reactivate the first still-deactivated
`bhatwarhawk`/`bhatbrigand`/`bhatgyro`, the primitive `WAKEUP_ENEMIES` uses) and then declined at
runtime. `CampaignDirector.BindCallbackHost` takes the `CALLBACK` slot ahead of the generator
runtime's and answers the three codes by activating the lowest-numbered still-deactivated member of
that family, through the same un-dormanting path `WAKEUP_ENEMIES` uses, so the roster still spawns
deactivated. A CM19 trace confirmed the front half before the change: `campaign: WAKE_ANIM
'launch_warhawk' started 1 definition(s)` fired twice inside 60 s of sim with all twenty roster
aircraft still `DEACTIVATED`; after it the same run logs `campaign: CALLBACK 801 launched
'bhatwarhawk_1' off the hook`, then `bhatwarhawk_2`, and the second is tracked and engages.
⚠ The evidence does not settle where a launched Black Hat should APPEAR: the original's 801 case
reactivates in place and does not re-place, so the rig comes up at its roster spawn, which C4/M04
authors 37 m below the terrain height there for all fifteen blocks. Both launched aircraft flew out
on their net without an under-map report, but whether the launch reads right at the controls is a
judgement for `D33`.

**Verified.** <pending orchestrator run>

## D32 ☑ `BL-727` Mission end fades to black over the hold

**Goal.** Every mission ending, win or loss, fades the screen to black over the two-second hold,
and the next screen opens on black.

**Evidence (confidence: traced).** Asked at the controls. The decode says every ending converges
on `FUN_00443090`, which copies the framebuffer and ramps a black overlay 0 to 1 over 2.0 s
(`docs/formats/objectives.md:272`, "The mission-end path, and what the player sees after it").
`CampaignDirector.LeavingHoldS` is 2 s (`CSVM/src/Session/CampaignDirector.cs:43`, set at `:956`,
`Leave` at `:965-970`), and no fade or overlay exists on that path (a grep of `Session/` and `UI/`
finds only unrelated fades). `BL-623` landed the hold and recorded the fade as not built. The hold
constant is used as a clock advance in `CampaignSuites`, `CampaignRosterSuites` and
`LandingApproachSuites`; none asserts a fade.

**Approach.** A full-screen black `ColorRect` overlay on the session's HUD layer, ramped on the
same clock the hold reads so a paused world fades too, started by the same call that sets
`_leaving`, for both endings; the scrapbook then opens on black. Assert the overlay's alpha at
0, 1 and 2 s in `CampaignSuites` beside the existing hold assertions.

**Model recommendation.** medium, low effort. One overlay on one clock.

**Verify.** The new assertion; a `--screenshot` at the hold's end pinned in
`analysis/goldens/manifest.json` if a golden covers a mission end; full `.\RunTests.ps1`. At the
controls (`D33`): any mission to its end, a win and a loss.

**⚠ Traps.** The original fades a copied frame, so the world need not keep rendering under it.
Do not lengthen the hold. Every suite that advances the clock by `LeavingHoldS` must still pass
unchanged.

**Outcome.** `CampaignDirector.LeavingFade` reads `Result`/the counting-down `_leaving` field and
returns 0 before any ending, the hold's own linear ramp while `_leaving` is still counting down,
and 1 once it reaches zero and stays there (`Leaving` itself goes false at that point, so the
property reads `Result` rather than that flag). `UI.MissionEndFade`, a new full-screen `ColorRect`
on `HudLayers.Hud`, polls that property every frame and is built per rig beside `ObjectivesHud` in
`GameSession`, mounted only for a campaign session; nothing in `CampaignDirector.Step` or `Leave`
changed. `CampaignSuites`' `campaign-mission-end` now asserts the fade at 0 s, 1 s into the hold and
at the hold's own end (1, halfway, fully black), and `CampaignMissionLossKeepsObjectiveBits` asserts
the loss ending lands fully black too, so both endings are covered. No golden in
`analysis/goldens/manifest.json` covers a mission end, so none needed a re-pin.

**Verified.** <pending orchestrator run>

## D33 ☐ Closing sortie: every landed item judged at the controls

**Goal.** Each landed item's *Playtest after fix* line, as written in its section above, is flown
by the author, and each answer is recorded in the closing commit; a failed judgement re-opens the
item or files a follow-up with a `⚠ Traps` section.

**Evidence (confidence: n/a).** The items' verifications are instruments; the acceptance is the
author's eyes, per `docs/verification.md`.

**Approach.** After `A1` to `D32` land: one sitting in mission order, CM04, CM14, CM17, CM18,
CM19, CM21, CM24, plus one Instant Action against a pirate zeppelin for `A1`, and any mission's
end for `D32`. Prepare the sitting's launch commands from the items' lines before the author sits.

**Model recommendation.** n/a; the author at the controls.

**Verify.** The closing commit's message carries one line per item.

**⚠ Traps.** Do not close an item on an instrument alone when its line names something to watch
for; the Gemini's bays and the AI evasion are not in this plan and a finding about them is a new
filing, not a re-opening.
