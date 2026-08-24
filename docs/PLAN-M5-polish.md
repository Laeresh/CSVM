# Milestone 5 polish: the campaign at the controls

**ACTIVE PLAN** (written 2026-08-24). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan finishes what [`PLAN-M5-campaign.md`](PLAN-M5-campaign.md) started. It carries two kinds
of item: the corrective items from that plan's E42 at-the-controls pass, and the gaps the milestone
deliberately named rather than built. The loop is walkable and the `campaign-loop` suite pins it
headless, but the pass found that a mission cannot in fact be flown to its end at the controls, so
Wave A is about play and not about polish; the presentation items the pass also raised sit behind
it in Wave B.

⚠ **Wave A's items were found by a human at the controls and then traced, and three of them are not
what the symptom said.** The objectives register correctly and the player simply cannot find the
sites (A1); the empty cutscene is a session that never spawns its zeppelins at all (A2); the wingman
spawns exactly where the data says and then fails to keep station (A3). Read each item's Evidence
before acting on its title. Every
other item was re-verified still-open against both the record (`git log --grep`) and the code before
it was written down, and carries the `file:line` that check produced. Two entries did not survive
that check and are closed rather than planned (E43). **Out of scope:**
`BL-447` (Change Memento) rests on `BL-256`, which the user deferred by explicit decision, so it
cannot be scheduled without reopening that decision; multiplayer and netcode; the flight model,
whose own parity plan just closed; anything blocked on a capture that does not exist, including the
`ObjectivesHud` styling TUNE, which waits on `CAP-45`.

## Milestone goal

- The first campaign mission can be flown to its end at the controls, with its objectives
  registering, its world populated and its wingman where the original puts it.
- The campaign screens fill the window and carry the original's own buttons, worked with a
  controller.
- The briefing plays: flags planted, photos changing, objective lines written onto the parchment,
  all on the narration's own cue points.
- A mid-mission cutscene can start, which is the one missing trigger behind two open items.
- The remaining named gaps are either built, answered, or closed as already-done.

**The fidelity gate stays Chapter 1.** Mechanisms ship for every chapter, exactly as the campaign
plan's Decision 6 set it; per-mission choreography beyond C1 is not this plan's business either.

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

### Wave A — what stops the campaign being playable

1. ☑ Objective targets and help labels are never drawn, so the sites cannot be found (`BL-456`)
2. ☐ A campaign session never spawns its zeppelins or generators (`BL-451`)
3. ☐ The campaign wingman cannot hold station on a real player (`BL-457`)
4. ☑ The music channel drowns the briefing (`BL-455`)
5. ☐ The cutscene letterbox flickers once (`BL-452`)
6. ☐ `DANGER_ZONES_COMPLETED` is never fed in a campaign mission (`BL-458`)

### Wave B — where things are shown, and where they are heard

11. ☐ The objectives readout belongs on the pause screen (`BL-454`)
12. ☐ Radio calls play positionally (`BL-453`)
13. ☐ Full-screen campaign boards with the original's buttons, worked on a pad (`BL-449`)
14. ☐ The briefing reveal, drawn as authored (`BL-450`)

### Wave C — the gaps M5 named

21. ☐ The `landings.zrd` approach trigger, which is the mid-mission cutscene gate (`BL-448`, `BL-035`)
22. ☐ The VO dialogue chain player (`BL-443`)
23. ☐ PNG on the hangar art seam, and what to do about JPEG (`BL-444`)

### Wave D — the campaign's own rough edges

31. ☐ The load screen's composed artwork (`BL-409`)
32. ☐ Spawn node names defeat `rating_biases` on the campaign path (`BL-401`)
33. ☐ World objects are hostile to everyone (`BL-407`)

### Wave E — answers and housekeeping

41. ☐ Decode the original's per-pylon ordnance id (`BL-445`)
42. ☐ Decide how the MPG cinemas would play, before any code (`BL-446`)
43. ☐ Close what is already done, fix what is merely stale (`BL-243`, `BL-427`, `BL-426`)

## Dependency and parallelism notes

**A1 comes first and alone.** Until objectives register, no mission can be flown to its end, which
blocks the user's own verdict on the wallet (`PLAN-M5-campaign.md` E42 line 7) and on everything
downstream of a completed mission. Nothing else in this plan is worth starting ahead of it.

A2 and A3 both sit in the campaign spawn path (`CampaignDirector`, `GameSession`, the roster
spawner) and are being traced together, so run them as one piece of work or in sequence, never as
parallel worktrees. A5 is small and touches the cutscene host alone. A4 has landed.

B11 and B14 both touch a campaign page, and B13 touches all six of them, so **B13 and B14 run in
sequence with B13 first**: B14's drawing needs the board surface B13 builds. B11 moves a readout off
the HUD and onto the pause screen, which is nobody else's file. B12 is a routing question that
should be settled from the decode before any code moves, and it will decide the channel C22
inherits, so run B12 before C22 or accept that C22 may have to move again.

C21 unblocks two backlog items at once and nothing else depends on it. C23 is independent.
D32 and D33 both touch AI-adjacent files (`FlightRoster`, `AimAssist`) but different ones; check for
contention with any live flight worktree before starting either. D31 touches `Launcher`, which B13
may also touch for the board presentation, so sequence D31 after B13 or agree a file boundary first.

E41 and E42 are research items whose deliverable is an answer, and E43 is housekeeping; all three
can run at any point. E43 is the cheapest thing in the plan and closing two stale entries early
keeps the backlog honest while the rest runs.

---

# Wave A — what stops the campaign being playable

## A1 ☑ Campaign objectives do not register in a flown mission (`BL-456`)

**Goal.** A campaign mission's objectives complete when the player does what they ask, so the first
mission can be flown to its end at the controls.

**Evidence (confidence: lead-only on the mechanism, certain on the symptom).** From the E42 pass on
the first campaign mission: only one objective ever completed, and "I couldn't drop Jack at the
wreck". The player could not tell which objective had completed, because the readout's tick was not
on its line (that display half is B11). What makes this sharp rather than vague is the contrast: the
headless `campaign-loop` suite completes C3/M01's primary by flying its `TRAVELERS` approach and
passes three runs in a row, so the runtime works in at least one scripted case and the divergence
lives between that case and a real flown session.

⚠ **Traced, and the conditions are not the bug: the player is simply never told where to go.** All
six of `C3/M01`'s `TRAVELERS` references resolve, and placing the aircraft at each site completes
that site's objective on the first unheld tick (tunnel `t_chamber` r=200 at d=75, the village's bare
point r=200 at d=147, the wreck `shipwreck` r=200 at d=115). The kind is implemented at
`CampaignDirector.cs:582` / `ObjectiveGraph.cs:518` and works. What is missing is presentation:
`ObjectiveGraph` maintains `ObjectiveTargets` (`:205`), `OtherTargets` (`:208`) and `HelpLabels`
(`:211`) and raises `TargetsChanged` (`:171`), and nothing in `CSVM/src` consumes any of them
outside one suite assertion, so `ADD_OBJECTIVE_TARGET`/`SET_HELP_LABEL` is a write-only store. With
the briefing map also not drawing its flags (B14), the sites are unlearnable before takeoff too. The
user found one site of three by luck, which is the whole symptom.

⚠ **Making the targets visible does not by itself finish the mission.** Of the mission's six display
rows, one is reachable today, three are blocked on C21's `landings.zrd` trigger, one on the
danger-zone feed (A6) and one is reachable. This item owns the targets; it must not absorb the rest.

**The mission's authored shape is on film**, `OriginalScreenshots\Videos\Complete Mission M02.mkv`
(5:01, a complete run of the original by the user, key frames kept under
`playtest\M02-complete-run\`): fly to the tunnel, the mountain village and the wreck; fly to the
wreck again to drop Jack, which plays a short cutscene of him parachuting whose staging depends on
the approach direction; shoot down a cargo zeppelin (destroyable tanks slung below) and three
Kestrels; fly back to the PANDORA and dock, which the film shows as flying into the airship's lit
hangar bay, with an auto-land button as the alternative. Two of those steps reach past this item:
the Jack cutscene is very likely gated on the trigger C21 owns, and docking inside the PANDORA is a
mission ending nothing in our build has been shown to do.

**The marker is referenced, and the mechanism already exists.** The user, from the original: the
objective sites are marked "same as enemies but in blue". `Complete Mission M02.mkv` at t=5 s shows
it: a two-line blue label over the site, a name line above a range line, drawn in world over the
terrain (magnified frame kept with the run's stills). Our `Flight/MarkerHud.cs` is already "the
original's Stunt Flying objective marker, rebuilt as a HUD Control" and already draws in `HudBlue`
(`MarkerHud.cs:36`), with `EdgeMarker` for the off-screen case. So this item is wiring an existing
blue marker to a store that nobody reads, not designing a marker.

**Approach.** Consume the target and label store: a marker for each `ObjectiveTargets` entry with
its `HelpLabels` text, redrawn on `TargetsChanged`, through `MarkerHud`'s existing idiom rather than
a new overlay. Check what `MarkerHud` assumes about being stunt-owned before reusing it, and if it
is too tangled to share, follow it rather than inventing a second look.

**Model recommendation.** high. It is the plan's blocker and it reaches into the flight HUD, but the
presentation question is now settled, so the risk is integration rather than judgement.

**Verify.** The three sites findable at the controls without foreknowledge, plus an in-engine test
over `ObjectiveTargets`/`HelpLabels` that fails before the fix. `campaign-loop` passing throughout
proves the existing suites cannot catch this class, so an unchanged suite result is not evidence.

**⚠ Traps.** ⚠ Do not touch the conditions or widen a radius: they are proved correct, and widening
one would bury the real bug. ⚠ Do not absorb the three rows C21 blocks or the one A6 blocks; this
item ends at making the targets visible. ⚠ Do not invent a marker style: the original's is the
enemy marker in blue, `MarkerHud` already draws that, and the film shows the two-line name-over-range
label it composes.

**Landed.** A flown campaign mission marks every objective site the mission flags, in world, in the
marker blue: a reticle at the site with the original's category line over the site's own name
(`[Examine] -` over `Site #1`), and off screen the edge arrow with the clock bearing. The set is
`targets.zrd`'s own `objective` entries, edited by `objectives.zrd`'s `ADD_`/`REMOVE_OBJECTIVE_TARGET`
as objectives complete, so flying a site's approach retires that site's marker and leaves the rest
standing. `ObjectiveGraph.ObjectiveTargets` is not the whole store: C3/M01 never ADDs its three
sites, it only REMOVES them, so a set built from the script's adds alone stays empty for the whole
mission, and `MissionTargets` now reads the valueless `objective` flag that seeds it. A site the
mission names by a bare `TRAVELERS` point is marked at that point rather than at the world node of
the same name, because C3/M01's village node stands at the world origin, 7.9 km from the point its
own objective tests. `MarkerHud` stays stunt-owned; its drawing primitives moved to `MarkerDraw`, so
the two markers share one look rather than a copy, and the label and colour are `TargetRef`'s
already-decoded ones. `ObjectiveMarkerHud` self-mounts the way `ObjectivesHud` does, one line in
`GameSession`. The conditions, the radii and the three rows C21 blocks are untouched.

**Verified.** `dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors; `CSVM.Tests` 2334 passed, 0
failed (111 suite names, `campaign-objective-markers` last); `CheckCommentCaps.ps1 -Summary` reports
all comment blocks within cap. The new in-engine suite `campaign-objective-markers` drives C3/M01's
real director against its BUILT world: 9 checks green, and with the marker set reduced to
`ObjectiveGraph.ObjectiveTargets` alone it fails on the first one (`0 passed, 1 failed`), which is
the bug it exists to catch. A scripted `--campaign=<profile>:0 --screenshot` run over C3/M01 shows
the three sites marked in blue over the island with their labels
(`.scratch/a1-objective-marker.png`). **Battery on the merged Wave A tree:** build clean, units
2334/2334, engine suites 112/112 with errors clean, all 16 golden shots hash-identical, so lifting
`MarkerHud`'s drawing into `MarkerDraw` moved no stunt marker.

## A2 ☑ A campaign session never spawns its zeppelins or generators (`BL-451`)

**Goal.** A campaign mission's zeppelins are in the world, so the intro camera has something to
show and the mission's zeppelin objectives and turrets exist at all.

**Evidence (confidence: traced).** The symptom was reported as an empty cutscene, but the cause is
not the cutscene: `SessionSpec.FromCampaign` (`SessionSpec.cs:1136-1153`) sets `Mode`, `WorldMode`,
`Players` and `PlaneNames` and never sets `Zeppelins` or `Generators`, which gate placement at
`GameSession.cs:2309` and `:2369`. A live run logs `world: 2 unplaced entit(y/ies) left at the
origin, switched off: piratezep, cargozep1` (`GameSession.cs:1190-1193`), so both sit at the origin
deactivated for the whole mission. `generic_intro`'s own `ObjectActiveState piratezep = true` runs
during the bootstrap and is then undone by `HideUnplacedEntities` later in `BuildWorldStage`. The
same gap takes out `OBJECTIVE20` (a `TRAVELERS` within 500 m of `piratezep`), the
`WAKEUP_ZEP_TURRETS [piratezep]` on `OBJECTIVE1`, and the film's whole "shoot down the cargo
zeppelin" leg.

**Landed.** `GameSession`'s constructor resolves `CampaignDirector.ResolveSpec` (which settles
Chapter/Mission for a `--campaign=` launch) and then calls the new internal
`GameSession.ResolveCampaignZeppelins`, which peeks `Zeppelins.Load`/`EnemyGenerators.Load` against
the resolved mission's own zrdr scope and ORs a non-empty read into `SessionSpec.Zeppelins`/
`Generators` through the new `SessionSpec.WithCampaignZeppelins`. Peeking rather than setting both
flags unconditionally means a mission whose generator file is authored empty (`[null]`) never spins
up an `AiGeneratorRuntime` with nothing to generate, and an explicit `--zeppelins`/`--generators` on
the command line survives (the OR, not an overwrite). `HideUnplacedEntities` still runs earlier in
`BuildWorldStage` than the `--zeppelins` placement block and still logs its one transient `world: N
unplaced entit(y/ies)` line at build time (that check is generic and runs before any
mission-specific placement, campaign or otherwise), but the placement block now actually runs
afterward, and the existing `RestorePlacedEntities` poll (`GameSession.cs`, `_unplacedWatch`) picks
up the moved node within its next 1 s tick and switches it back on for good, closing `OBJECTIVE20`
and the `WAKEUP_ZEP_TURRETS [piratezep]` gap along with it. `ResolveCampaignZeppelins` is `internal`
rather than `private` specifically so the `campaign-zeppelins` suite exercises the exact code the
constructor runs.

**Model recommendation.** medium. The change is small; the care is all in the regression surface.

**Verified.** `dotnet build CSVM/CSVM.sln`: 0 warnings, 0 errors.
`dotnet test CSVM.Tests/CSVM.Tests.csproj`: 2334 passed, 0 failed, 0 skipped.
`.\CheckCommentCaps.ps1 -Summary`: all comment blocks within cap. **Battery on the merged Wave A
tree:** build clean, units 2334/2334, engine suites 112/112 with errors clean, all 16 golden shots
hash-identical, so placing a campaign's zeppelins moved nothing in world build.
The `campaign-zeppelins` suite (registered last in `SuiteCatalog.cs`, `CSVM.Tests/SuiteCatalogTests.cs`
updated to 110) and the 16 goldens run as part of the orchestrator's centralized, serialized battery
rather than here, since parallel `RunTests.ps1` runs across sibling worktrees kill each other's
stray Godot processes. A targeted `--campaign=<profile>:0` proof run on C3/M01, taken separately
from that battery, logs `zep: 'piratezep' placed at (-1401,500,-1413) ...`, `zep: 'cargozep1' placed
at (-6288,300,-7701) ...`, `zep: 2 of 2 zeppelin(s) placed for C3/M01`, and `world: 2 entit(y/ies)
moved off the origin after all, restored: cargozep1, piratezep`, none of which appeared before this
fix since the placement block never ran. A screenshot taken after the intro cutscene hands off, with
the player aircraft placed beside `piratezep`'s authored position, shows the zeppelin's hull, gondola
and broadside cannons rendered in the world.

**⚠ Traps.** ⚠ **913/914 is disproved as the cause and must not be re-chased here:**
`camera1-generic_intro.json` raises `20, 2, 11, 14` with `1, 10, 914, 667` in its reset state only,
never `913`, and a traced run shows `AiParked=false` throughout. ⚠ The parking mechanism is real and
latent for a mission whose intro does raise 913, since `Inert` un-draws an aircraft
(`FlightController.cs:583`); file that separately if it appears rather than folding it in here.

## A6 ◐ `DANGER_ZONES_COMPLETED` is never fed in a campaign mission (`BL-458`)

**Goal.** An objective gated on danger zones can complete.

**Evidence (confidence: traced).** The condition reads correctly (`ObjectiveGraph.cs:494`) and is
fed by `CampaignDirector.NotifyDangerZoneCompleted` (`:345`), which has no caller in a campaign
session: the danger-zone module belongs to `--stunt`. C3/M01's `OBJECTIVE3` (`SECONDARY 11`) and
`OBJECTIVE11` use it, so that row is unsatisfiable. Found while tracing A1.

**Landed.** A campaign mission's danger zones are the same physical mechanism `--stunt` reads (the
`dzpathN` route-ribbon-plus-two-gate-polygon mesh under the chapter world's `dzpaths` subtree,
crossed in either order), fed from a different authoring surface: a story mission carries no
`ia.json` `dzones` list at all, and instead names `dzpathN` directly inside its own
`objectives.zrd` `DANGER_ZONES_COMPLETED` conditions (C3/M01's `OBJECTIVE3` on `dzpath1`,
`OBJECTIVE11` on `dzpath4`), narrowed by the mission's own `dzones.zrd` `disable` list
(`docs/formats/missions.md` "Zone overrides" — previously decoded there as data the remake reads
nowhere). `CampaignDangerZones` (`CSVM/src/Session/CampaignDangerZones.cs`) is a standalone reader
of that surface: it collects every `DANGER_ZONES_COMPLETED` name the script authors, drops any
`dzones.zrd` disables, resolves gates against a real `GameZ`, and calls back with a crossed zone's
name. `CampaignDirector.Attach` arms it from a new `WorldInputs.Gamez` field and `Step` drives it
off the live player position, feeding the exact `NotifyDangerZoneCompleted` entry point the graph
already had. Kept as its own module rather than reusing `Flight.StuntMission`: the two mechanisms
share their gate math by construction (this module mirrors `TryReadGates`/`GateCrossing` rather
than importing them) but read different authoring surfaces and belong to different sessions, so a
shared notify would have been a design choice, not a repair.

**Not landed: the live wiring's last field.** `GameSession.cs`'s own `Attach` call
(`_campaign?.Attach(new CampaignDirector.WorldInputs { … })`, around line 2473) does not set the
new `Gamez` field — it is out of this item's file ownership (`GameSession.cs` is off-limits here).
Until that one field is added (`Gamez = state.Gamez,`, alongside the `PlayerAircraft` delegate
already there), a real flown campaign session still never arms `CampaignDangerZones`, and C3/M01's
SECONDARY still cannot complete at the controls. Everything else — the decode, the module, the
director's own wiring, and the objective completing off the real `NotifyDangerZoneCompleted` path
— is built and proven against real C3/M01 data; only that call site is missing.

**Verify.** `campaign-danger-zones` (new suite, registered last in `SuiteCatalog.cs`) builds
C3/M01's real world, confirms `CampaignDangerZones.Load` arms exactly the two `dzpathN` names
`objectives.zrd` references (`dzpath1`, `dzpath4` — `dzones.zrd`'s `disable` list holds three other
paths the script never names), drives a scripted crossing of both authored gates to completion for
each, and confirms `CampaignDirector.Attach` arms the same count from a real `Gamez`. It then drives
a real `CampaignDirector` over C3/M01 through `NotifyDangerZoneCompleted` and confirms `OBJECTIVE3`
(the SECONDARY) and `OBJECTIVE11` both complete, which is BL-458's condition satisfied through the
exact path `CampaignDirector.Step` will call once wired. `--run-tests=campaign-danger-zones`: 1
passed, 0 failed, engine errors clean.

**⚠ Traps.** ⚠ Confirmed: the campaign's zones ARE the `--stunt` module's gate geometry — do not
build a second, different completion rule for them. ⚠ A `dzpathN` gate pair can sit only metres
apart (a "thin slit" aperture): a probe built to cross one gate can legitimately cross both in the
same segment, which is completion, not a test bug.

## A3 ☐ The campaign wingman cannot hold station on a real player (`BL-457`)

**Goal.** The wingman stays with the player instead of falling behind and climbing away.

**Evidence (confidence: traced).** The reported symptom was a spawn in the wrong place; **the spawn
is correct**. `C3/M01`'s `aiv.zrd` authors `player` at `[-1426, 150, -1813]` yaw 40 and `wingman_1`
at `[-1378, 150, -1706]` yaw 40, 117 m apart at the same altitude and heading, and the spawner
places it exactly there (`CampaignRoster.cs:191-192`, confirmed in a live run). The failure is
station-keeping after the handoff: traced four times a second with the escort in `Station` from the
first frame and computing a correct 19 m station point, the separation runs 117 m, 95 m, 102 m,
133 m at t=0/1/3/5 s, then 289 m at 10 s, 597 m at 20 s and 838 m at 26 s, ending 130 m above the
player. ⚠ Past 700 m the escort re-enters `Joining`, whose commanded point is
`AiEscort.LeaderOverflyM = 200f` **directly above the leader**, which is the reported symptom word
for word and is self-reinforcing once entered.

**Approach.** The driver, `AiPilot` through `AiControlLaw` on the `Wingman` table. Not the escort
law's geometry, which computes the right point throughout, and not the spawner.

**Partial: one contributing cause fixed, the reported magnitude still unexplained.**
`AiControlLaw.Steer` clamps an AI's desired speed to `SpeedCeiling`, 111.76 m/s (250 mph), which the
def initialiser writes into every airframe's `def+0x1e8` (`FUN_00478a00` at `0x478d52`,
`MOV dword ptr [EBP+0x1e8], 0x42df851e`, re-read out of the image; nothing else writes the slot but
the `kind_of` copy). The campaign's own airframe, `player_pfighter`, has `fd_speed` 113 m/s, so an
escort's demand is capped 1.24 m/s BELOW what its leader cruises at and it holds no closure margin:
any ground lost in a turn is never regained. `AiPilot.FlyEscort` now passes `stationKeeping`, which
swaps the clamp for `AiControlLaw.StationCeiling`, the decoded ceiling or the leader's own speed
plus the law's own decoded `DesiredSpeedBand` when the leader is faster. The lift is bounded by
decoded quantities on both ends, still capped by `fd_speed · SpeedCap`, and is the escort
dispatch's alone, so pursue, patrol, evade, lay off and the climb-out read the decoded ceiling
unchanged. Nothing in `AiEscort` moved.

**What this does NOT explain, and what the evidence now says instead.** On the campaign airframe the
ceiling is a secondary effect: unfixed, the wingman recovers to about 105 m and holds, so it cannot
produce the traced 838 m at 26 s. Two further findings redirect the remainder. The far-field plant
is not the original's answer to a fast leader: its target is `fd_speed · lever + 5` reading the
lever at `[obj+0x128]` (`0x48c593`-`0x48c5ae`), that lever slews toward the commanded `[obj+0x124]`
(`FUN_0048e580` at `0x48e58f`/`0x48e59b`), and `[obj+0x124]` is exactly what the ceiling clamps, so
the cap reaches both plants and a far-field escort holds at most `ceiling + 5`. And the escort law
never leaves the formation state once joined, in the decode and in the port, so the 200 m overfly
the report describes is reachable ONLY by a wingman that never joined. The join gate is a range AND
a speed, `< 700 m` and `> 20.576 m/s`, which points the remainder at the handoff at the intro's
end rather than at the cruise.

**Model recommendation.** high. It is a control-law failure on a table shared with other AI
behaviour, so the blast radius reaches beyond the campaign.

**Verified.** `wingman-station` gained a `[flown]` leg that flies the leader the way a player does:
a human stick on a `ScriptedInputSource` at full throttle from the spawn lever, a right turn, a
climb and a left turn, the wingman on the AI force path and against the human field a live session
binds. It runs on two airframes, and the pair is what makes the size of the effect readable.

| leg | margin over the ceiling | unfixed mean / worst | fixed mean / worst |
|---|---|---|---|
| `player_pfighter` (the campaign's) | 1.2 m/s | 296 m / 702 m | 256 m / 590 m |
| `player_bhawk` (the mechanism's control) | 23.2 m/s | 1200 m / 2064 m | 328 m / 584 m |

Both fail unfixed against the 700 m leash, the campaign airframe by 2 m and the control outright,
and both pass fixed. On the campaign airframe the unfixed wingman recovers to about 105 m and holds,
so the ceiling is the difference between touching the join gate and not, and nothing larger. The
far-field plant was measured rather than argued: with the human field bound and the lift off, the
control leg is on that plant for 54.3 % of its run (3911 of 7199 steps) and its fastest speed there
is 114.0 m/s, the ceiling plus the plant's own 5 m/s. Two geometry checks pin the state machine: an
escort below the join speed commands exactly 200 m above its leader, and a joined one 5 km out is
still in the formation state. `dotnet build` carries no new warning and `dotnet test` is green at
2334. A live `--campaign=<profile>:0` session builds `c3/m01` and logs
`campaign: roster 'wingman_1' … escorts 'player'`. The engine suites and the goldens ride the
orchestrator's serialized battery.

**⚠ Traps.** ⚠ **The airframe decides the magnitude, so measure on `player_pfighter`.** The
Devastator's `fd_speed` is 113 m/s against the ceiling's 111.76, a 1.24 m/s deficit; the suite's
default Bloodhawk is 135 m/s, a 23 m/s one, which overstates the ceiling's share about eighteenfold.
A result quoted off the default is not a statement about the campaign. ⚠ The escort law NEVER leaves
the formation state once joined, so `BL-457`'s own line that it "re-enters `Joining`" past 700 m is
wrong: the 200 m overfly belongs to a wingman that never joined, and the remainder should be looked
for at the handoff, not in the cruise. ⚠ The far-field plant is ruled out at the image and by
measurement; do not re-derive it. ⚠ The two SCRIPTED legs fly a leader on a cruise lever, a speed
any escort can match, so their leashes say nothing here and neither does D34's note that the
commanded point equals the decoded station to 0.00 m. ⚠ `AiControlLaw.StationCeiling` is a
remake-only lift and the escort dispatch's alone; extending it to pursue or patrol would make every
AI in the game faster than the decode allows. ⚠ Do not re-tune the decoded station offsets, the
700 m join gate, or `SpeedCeiling` itself, which is confirmed against the image.

## A4 ☑ The music channel drowns the briefing (`BL-455`)

**Goal.** Music sits under the briefing narration instead of over it.

**Evidence (confidence: certain).** From the E42 pass: music "works but too loud. Especially in
briefing", with the user's own instruction to set it to 0.2 until an options menu exists.

**Landed.** `MusicPlayer.ChannelLevel` (0.2) multiplies into the mixer in `SetGain` alone.
`MusicPlayer.Gain` deliberately keeps the fade's own 0..1 value, so every assertion about the
decoded ramp still reads what the decode describes and the `music-states` suite is unaffected. The
constant carries a doc comment saying it is a stand-in for a control that does not exist and is to
be removed, not re-tuned, once an options menu can hold a music level. `BL-455` is filed for the
options menu itself.

**Model recommendation.** medium, low effort.

**Verify.** The briefing heard at the controls with the narration intelligible over the score.
<TODO: the user's confirmation at the controls; the value is theirs, so their ear is the check.>

**⚠ Traps.** ⚠ `ChannelLevel` is a placeholder, not a fidelity constant. Do not tune it as though
the original's mix were being matched, and do not fold it into `Gain`, which would move the decoded
fade assertions.

## A5 ◐ The cutscene letterbox flickers once (`BL-452`)

**Goal.** The bars hold steady for the whole cutscene.

**Evidence (confidence: lead-only, certain on the symptom).** From the E42 pass: the bars are right
from the first frame, then "flickers at a point shortly then goes back". The shipped definition
switches the bars on outright and re-asserts the cutscene camera's frame onto them every tick, so a
one-frame gap points at a single beat that re-runs a base state or re-parents the card.

**Tried, and ruled out.** The one named candidate was `CutsceneController.Tick`
(`CutsceneController.cs`): `_runtime.AnimStateOf(Anim) != AnimRunning` reading a one-frame gap
between two instances of the intro definition and calling `Restore` early, retracting the bars
while the separately-called `letterbox` definition puts them back. Two full, real, windowed engine
runs under `--det` (a throwaway campaign profile, deleted after), each with the controller
instrumented to log `_bars.Visible` on every change and the `AnimStateOf` verdict on every tick,
found no such gap:

- The shared `generic_intro` path (Hawaii mission 1, `c3/m01`, `--campaign=<profile>:0`): bars go
  visible once at `t=0.017`, stay visible for the whole run, and come down exactly once at the
  natural handoff, `t=40.2 s`, after a single `'generic_intro' has the session` line and a single
  `its definition ended` line, matching the earlier 75 s / 40.2 s finding exactly.
- The bespoke `mission_intro_animation` path (C1/M04, `--campaign=<profile>:8`, five separate
  `CALL_ANIMATION letterbox` sites across its scene beats, not one): same shape, one visibility
  transition at `t=0.017`, one natural handoff at `t=53.22 s`, no gap in between.

A synthetic in-suite reproduction (`AnimRuntime` on a bare `camera1`/`letterbox` stage, driven
`Advance`-then-`Tick` like the game, no real render) was also tried, extending
`cutscene-letterbox`. It gets the real duration right (3193 simulated frames, 53.2 s, matching the
live run exactly) but the bars never turn on at all: the synthetic world carries no player/rig, so
whatever conditions gate the intro's own scene1/scene2 `CALL_ANIMATION letterbox` calls evaluate
false with nobody flying, and the letterbox def is simply never called. A pass or fail from that
harness would not be evidence either way, so it was not kept. The two real runs above are the
reproduction attempt that counts.

**Kept.** `CutsceneController.WatchBars`, called from `Tick` after `FrameBars`: while `Playing`,
the bars should transition at most once (off to on, from the letterbox call site); it counts every
`_bars.Visible` flip since the episode started and logs a `GD.PrintErr` naming the frame's sim time
if a second flip happens, or if any flip turns the bars off before `Restore` does. Silent in both
runs above and in every existing suite (`campaign-cutscene`, `campaign-loop`,
`cutscene-letterbox`). `GD.PrintErr` reaches `RunTests.ps1`'s engine-error screening, so the next
time this actually happens at the controls it fails CI on its own rather than needing another
manual pass.

**What would settle it.** A capture at the controls of the exact moment the flicker recurs (which
mission, roughly how many seconds in) would give the frame `AnimStateOf`/`WatchBars` can be read
against directly, replacing this open-ended two-path search with a one-mission repro. Short of
that, `WatchBars` firing on a future engine-error-screened `RunTests.ps1` run is the next signal.

**Approach.** Find the beat. The `cutscene-letterbox` suite already pins the base state and the
per-tick re-assert and does not catch this, so whatever it is happens between those two facts.

**Model recommendation.** medium.

**Verify.** A frame-stepped capture across the beat that flickers, plus the suite extended to cover
it, since its current coverage demonstrably misses it.

**⚠ Traps.** ⚠ Do not fix a flicker by tweening the bars in: that contradicts both the decode ("no
reveal") and the user's own verdict that the bars are present the instant the load ends.

# Wave B — where things are shown, and where they are heard

## B11 ☐ The objectives readout belongs on the pause screen (`BL-454`)

**Goal.** The objectives are read on the pause screen, as in the original, and not on the flight
HUD.

**Evidence (confidence: traced to the user's knowledge of the original, which is the authority this
question was waiting on).** From the E42 pass: "in the original the in-flight objectives are only
seen in the pause screen. but the targets are selectable in world." That answers the question
`CAP-45` was minted to film, so the capture is re-pointed at the pause screen's own presentation,
which still has no reference. Ours mounts `ObjectivesHud` on the world root for every campaign
session (D33's `GameSession` mount), so it is up for the whole flight. The presentation is now
referenced from `Complete Mission M02.mkv` at t=12 s (kept at
`playtest\M02-complete-run\pause-screen-objectives.png`): the mission map fills the left two thirds
with its `?` flags planted, an `Objectives` parchment sits top-right, a photo bottom-right, a
compass rose bottom-left, and four plaques run centre-bottom, `Resume`/`Restart` over
`Preferences`/`Quit`. The same film at t=5 s shows the flight HUD carrying gauges alone.

**Why the tick was not on the line, traced.** Two independent causes, both cheap. (1) C3/M01 builds
**six** display rows, not the four the pause screen lists: `OBJECTIVE3` authors
`IDENTITY [SECONDARY, 11]` and `OBJECTIVE31` authors `IDENTITY [SECONDARY, 12]`, two fields with no
message key, so `ObjectivesHud.cs:65` asks `Messages.Get(null)`, gets `""` (`Messages.cs:88`), and a
completed secondary renders as a bare tick with no text (`ObjectivesHud.cs:117`). (2) The readout
sits at `(24, 64) * windowH/1080` (`ObjectivesHud.cs:123`), which on a 1280x720 window puts line 1
at y=43, over the flight HUD's own `SPD / ALT / THR` strip.

**Approach.** Move the readout to the pause screen and take it off the HUD, keeping the graph
binding and the completion marking, which are D33's and are not in question. Decide what an unkeyed
row shows, and fix the mark's alignment while moving it.

**Model recommendation.** medium.

**Verify.** A capture of the pause screen mid-mission with a completed objective marked on its own
line, and a flight capture showing nothing on the HUD.

**⚠ Traps.** ⚠ `campaign-objectives-hud` asserts the readout marks its own completed line rather
than only the graph, which is the one proof that this half works; the move must keep that suite
meaningful, not delete it. ⚠ Do not also remove in-world target selection: the same verdict says
targets are selectable in the world, and that is a different subsystem.

## B12 ☐ Radio calls play positionally (`BL-453`)

**Goal.** A mission callout is heard in full wherever the player flies.

**Evidence (confidence: traced for the routing, contested for the intent).** From the E42 pass, the
lines "are not 3D placed but directly played... if they are 3d i'm gone before they are finished.
They are radio calls so no location is needed." The routing today is positional: an objective's
`WAKEUP_SOUND_GROUP`/`COMPLETED_SOUND_GROUP` goes through `CampaignDirector`'s one sound-group
executor to `WorldSounds.PlayOneShot`, which starts an `AudioStreamPlayer3D` (D33's suite counts
exactly that). ⚠ The decode as written disagrees with the conclusion: `docs/formats/sounds.md`
records the original's channel split as `MUSIC`-flagged definitions and `mu`-prefixed groups to the
streaming channel and **everything else to the positional path**, which would put these lines in 3D.

**Approach.** Settle the contradiction from the data before moving any code. If the original really
routes callouts positionally, the finding is that it places them where distance never matters, and
the fix is the placement rather than the channel. If a second non-positional class exists in the
decode, route to it. `MusicPlayer` is the precedent for a channel that sits beside `WorldSounds`
rather than inside it.

**Model recommendation.** high. It is a decode question first, and the wrong call moves every
mission's audio.

**Verify.** A callout heard end to end while flying away from wherever it started, plus whichever
in-engine counter the settled routing makes assertable.

**⚠ Traps.** ⚠ Do not change both the channel and the placement; one of them is the answer. ⚠ C22
(the VO chain player) will inherit whatever channel this settles on, so run this first or accept
that C22 may have to move again.

## B13 ☐ Full-screen campaign boards with the original's buttons, worked on a pad (`BL-449`)

**Goal.** Each campaign screen fills the window as one composed board, with its art at the size the
original draws it and the original's own button plaques along the bottom, and the pad moves focus
between those buttons and presses them.

**Evidence (confidence: traced).** Every campaign page (`CampaignRosterPage`, `CampaignCabinPage`,
`CampaignPreviousMissionsPage`, `CampaignBriefingPage`, `CampaignFlightCheckPage`,
`CampaignAmmoPage`) renders through the shared `BoardMenu` idiom: a centred title, a stack of text
rows, an art thumbnail beside them, a keyboard hint line. That is `PLAN-M5-campaign.md`'s Decision 7
("new screens follow the existing `src/UI/` board/menu idiom") working as decided, and it is what
the E42 pass rejected. The A/B is `.scratch/ours-briefing.png` against
`OriginalScreenshots\Campaign Briefing.png`: ours draws a roughly 220 px map thumbnail to the left
of a text list; the original fills the window with the map and hangs three plaques off the bottom
edge. The chrome is decoded, not guesswork: `docs/formats/briefing.md` reads the dialog's
`BACKGROUND` (`POSITION`, `BITMAP`) and its `BUTTONS` section, whose entries share bitmap
`brief_button1` with a normal/rollover/activate label set, and `extracted/rimage/brief_button1.png`
ships, as do `escape_button1..3.png`. The art is PNG and `PngImage` already loads it: the campaign
pages call it today (`UI/CampaignCabinPage.cs:151`, `UI/CampaignBriefingPage.cs:273`,
`UI/CampaignAmmoPage.cs:187`), so no new decoder is needed for this item.

**Approach.** A board presentation for campaign pages that places elements at their authored pixel
positions over a full-window background, and draws the authored button art with its three label
states, rather than composing a `BoardMenu`. Reuse the existing `PngImage` art seam. The pad already
drives menus through `MenuInput`; what changes is what focus looks like (a plaque in its rollover
state instead of a highlighted text row).

⚠ **The authored coordinates are a fixed-size dialog, so how that maps onto an arbitrary window is a
real decision and must be made in this item, in writing, not left to the reader.** The three
candidates are an integer scale of the authored resolution, a fit-to-height scale with the
background bled or cropped horizontally, and the authored resolution letterboxed. Whichever is
picked, record it and why on `docs/formats/briefing.md` or a new `docs/org/` page, because every
later screen inherits it.

**Model recommendation.** high. It reverses a standing decision, it is the largest blast radius in
the plan (six pages), and the scaling choice is a judgement call that outlives the item.

**Verify.** A scripted shot of each of the six screens (`--menu=campaign-cabin`,
`campaign-previous`, `campaign-briefing:24`, `campaign-flightcheck`, `campaign-ammo`, and the roster
page) at the same window size, each read against its `OriginalScreenshots\Campaign *.png` reference
where one exists, plus one pad-driven pass proving focus moves between plaques and presses them.
The 16 golden shots must stay hash-identical, since no campaign screen is in the golden set and a
moved golden means the change leaked into flight.

**⚠ Traps.** ⚠ Do not drag the Instant Action and hangar boards along with the campaign screens:
those are their own fidelity questions and nobody has judged them yet. ⚠ Do not scale a bitmap past
its authored size to fill a 4K display without settling what the original's pixel grid means at that
size; a soft upscale of authored art reads as a bug at the controls. ⚠ `BL-181` is a `[Tuning]`
entry blocked on "the menu hub", and its blocker is arguably discharged by this work; decide that
explicitly rather than leaving the tag stale.

## B14 ☐ The briefing reveal, drawn as authored (`BL-450`)

**Goal.** The briefing plays the way the original's does: flags planted on the map one at a time,
the photos changing through the narration, each objective line written onto the parchment as the
voice reaches it.

**Evidence (confidence: traced, against both the footage and the decode).** `CAP-42`
(`OriginalScreenshots\Videos\CAP-42.mkv`, 1920x1080, 107 s) shows the original, and every beat in it
maps to an opcode already censused in `docs/formats/briefing.md`. At t=46 s the screen carries a
portrait photo pinned top-left over a paper stack, an `Objectives` parchment lower-left with one
written line ("1) Find the main treasure site."), three red `?` flags planted on the map with cast
shadows, and the three plaques along the bottom. At t=96 s the same screen carries a **different**
portrait, four written objective lines, a fourth flag, and a zeppelin sprite that arrived for the
`Dock with the PANDORA` line. So the photos are a slideshow, the objective lines are written one at
a time, and each flag is planted in step with its line. The opcodes for all of it are decoded:
`Pict` (id, bitmap, `at [x, y]`, `center`), `Fade`, `Spin`, `Move`, `Line`, `On`/`Off`, `Objective`
(binds a screen element to an objectives-list entry), `ToBack`, with timing from `PlaySound` +
`WaitForMarker` against the narration wav's RIFF `cue ` chunk. Ours plays the narration and uncovers
text rows in the list; it draws none of the elements. The art ships: per-mission maps
(`extracted/rimage/ha-m1map.png` and siblings), the pinups (`ms_p_*pinup*.png`), the flags.

**Approach.** Execute the reveal script as authored, placing each `Pict` at its own coordinates on
B13's board surface and running the `Fade`/`Spin`/`Move` tweens over their authored durations. The
durations are authored constants in the data and `docs/formats/briefing.md` states explicitly that
they are not a TUNE gap to invent. The marker timing already works and is not this item's subject.

**Model recommendation.** medium. The mechanism is fully decoded and the work is faithful
execution of an opcode list, not judgement.

**Verify.** A timed sequence of shots from one scripted briefing run (C1/M01 and one other mission)
showing the flag count and the parchment line count growing together, read against `CAP-42`'s own
progression; plus the existing suites staying green, since the briefing page is already covered.

**⚠ Traps.** ⚠ A marker number indexes the wav's cue points **sorted by sample offset, never by cue
id** (13 of the 24 wavs store them out of time order, and reading the id as the index plays the
briefing backwards). The shipped page gets this right; do not regress it while moving the drawing.
⚠ This item is drawing, not timing: if a beat lands at the wrong moment, that is a marker bug and
belongs to whoever owns the timing, not to a fudge factor here.

# Wave C — the gaps M5 named

## C21 ☐ The mid-mission cutscene trigger (`BL-448`, `BL-035`)

**Goal.** A mission can start a cutscene while it is being flown, which makes an objective gated on
that definition satisfiable and gives the remaining mission-script callback codes somewhere to land.

**Evidence (confidence: traced).** The runtime half already works: `AnimRuntime.AnimStateOf` returns
EXECUTED and `ObjectiveGraph.AnimStateMet` consumes it, so an `ANIM_STATE <def> EXECUTED` condition
would be satisfied if anything ever played the definition. Nothing does:
`CutsceneController.IsIntro` (`Session/CutsceneController.cs:118`) answers only for
`mission_intro_animation`/`generic_intro`, and the sole construction site is `GameSession.cs:574` at
session build. `PLAN-M5-campaign.md` E41 found the consequence: C3/M01's authored route to its own
`INSTANTWIN` runs through `OBJECTIVE14`'s `ANIM_STATE hooked_to_klondike EXECUTED`, so the loop
suite has to wake the ending through the graph instead. `BL-035` is blocked on the same trigger for
the `landings.zrd` codes (3/12/13/86/701/702/800-803/950/951/965-968). The trigger's own condition
object is undecoded, which is what `PLAN-M5-campaign.md` D32 recorded when it left this open.

**Approach.** Decode what starts a `landings.zrd` cutscene (the approach-node condition object), then
let `CutsceneController` host a definition that is not an intro. D32's scoping trap applies in
reverse here: the controller is not intro-only by construction, only its `IsIntro` gate is.

**Model recommendation.** high. It begins with an undecoded condition object, and a wrong trigger
fires cutscenes mid-dogfight.

**Verify.** C3/M01 flown to the point where `hooked_to_klondike` should play, with the objective
graph reaching `INSTANTWIN` by the authored route rather than a direct wake; the `campaign-loop`
suite then updated to take that route and still green twice.

**⚠ Traps.** ⚠ Do not satisfy an `ANIM_STATE ... EXECUTED` condition by treating an unplayed
definition as executed: every such objective would fire at mission start. ⚠ D32's finding stands,
that authored callback codes do not identify a cutscene (Instant Action's `player_setup` raises the
same nine), so scope by definition, never by code.

## C22 ☐ The VO dialogue chain player (`BL-443`)

**Goal.** A cue naming a VO dialogue chain plays the chain instead of silence.

**Evidence (confidence: traced).** `SoundGroup` (`Mech3/SoundDefs.cs:162-214`) keeps `Chains`
separate from the weighted `Members`, and `Pick` returns only a weighted member, so a chain resolves
to null and plays nothing. The only consumer of `.Chains` anywhere is the prewarm decode
(`Mech3/WorldSounds.cs:114`): no sequencer, no queue, no chain state exists. C1/M02 carries both
shapes in one mission, which is the ready-made A/B: `OBJECTIVE8`'s `WAKEUP_SOUND_GROUP` and
`OBJECTIVE16`'s `COMPLETED_SOUND_GROUP` are weighted and audibly play, while `OBJECTIVE1`'s
`snd_NW2Start` and `OBJECTIVE10`'s `snd_NW2Prim2Suc` are chains and play nothing.

**Approach.** A chain player: what sequences the lines, what spaces them, whether a second chain
interrupts or queues behind one already speaking, and which layer owns it.
`docs/formats/sounds.md` says the chains are kept "so the comms/mission layer can consume them", a
consumer that does not exist, so naming that owner is part of the item.

**Model recommendation.** medium. The data shape is decoded; the open questions are sequencing
policy rather than reverse engineering.

**Verify.** An in-engine suite over C1/M02 counting real `AudioStreamPlayer3D` starts for a chain
cue, with the existing weighted-cue proof beside it unchanged (`WorldSounds.OneShotsStarted` is the
counter D33 added for exactly this kind of proof).

**⚠ Traps.** ⚠ This is not a prewarm gap: `ExtraPrewarmNames` already expands a chain to its
members, so adding chains to a prewarm list changes nothing. ⚠ Do not make `SoundGroup.Pick` return
a random chain member; a chain is a script, not a draw, and that change would move every existing
weighted-sound suite.

## C23 ☐ PNG on the hangar art seam, and what to do about JPEG (`BL-444`)

**Goal.** The hangar's art seam draws the PNG art that ships, and the JPEG-only art has a recorded
decision rather than a silent blank.

**Evidence (confidence: traced).** `PngImage` exists (`Mech3/PngImage.cs`) but only the campaign
pages call it (`UI/CampaignCabinPage.cs:151`, `UI/CampaignBriefingPage.cs:273`,
`UI/CampaignAmmoPage.cs:187`). The hangar seam is still TGA-only: `HangarArt(TgaImage Image, …)`
(`UI/HangarFlow.cs:99`), with `UI/HangarAirframePage.cs:114` and `UI/CampaignFlightCheckPage.cs:469`
calling `TgaImage.TryLoad` alone. So `OL_PLANEDIAGRAMSTOP.PNG` / `OL_PLANEDIAGRAMSFRONT.PNG` return
null and draw nothing. No JPEG decoder exists anywhere, which is what keeps `PC_P_HANGAR<n>.JPG` out.

**Approach.** Widen the seam's art type past `TgaImage` and route it through `PngImage`, then census
`extracted/rimage/*.PNG` for other art no page draws yet. JPEG is a separate decision (a decoder, a
transcode at extract time, or leave it), and recording that decision is part of this item.

**Model recommendation.** medium, for mechanical routing plus one small scoping decision.

**Verify.** A scripted shot of the airframe and flight-check pages showing the top and front plane
diagrams drawn, plus the census result written down.

**⚠ Traps.** ⚠ Returning a placeholder image is worse than returning null: a wrong picture reads as
a fidelity verdict. Keep the never-invent behaviour for anything still undecodable.

# Wave D — the campaign's own rough edges

## D31 ☐ The load screen's composed artwork (`BL-409`)

**Goal.** The load screen draws the original's composed artwork instead of a plain panel.

**Evidence (confidence: traced).** The decode is complete in `docs/org/loading-screen.md`, and
`Launcher.cs:736` still builds a `UI.LoadBoard` with a plain caption string. Every campaign mission
launch draws this screen, which makes it the last un-restyled surface in a loop whose other screens
M5 just built. `BL-409`'s own text notes the progress bar needs the build decoupled first, so the
artwork is the shippable slice and the bar is not this item.

**Approach.** Draw the composed artwork per the decode; leave the progress bar's threading question
alone and say so on the entry.

**Model recommendation.** medium.

**Verify.** A scripted shot of the load screen on a campaign launch against the decode's own
description; goldens unchanged.

**⚠ Traps.** ⚠ Do not take the progress bar on as a bonus: it needs the build decoupled from the
draw, which is a different item with its own blast radius.

## D32 ☐ Spawn node names defeat `rating_biases` on the campaign path (`BL-401`)

**Goal.** An AI spawned into a campaign mission matches its roster's `rating_biases` patterns, so
the authored bias term does something.

**Evidence (confidence: traced).** `FlightRoster.cs:152` names spawns `ai{n}_{plane}` (for example
`ai1_player_fury`, `player1`), while the roster's patterns are of the form `hafury*`,
`bswingman_1`, `player`, so a match never happens and the term is dead. This became a campaign
problem when the roster spawner landed: `CampaignDirector.cs:241` now feeds
`gunner.RatingBiases = spawn.Biases` for every campaign spawn, so the dead term sits directly on the
campaign path.

**Approach.** Make the spawned node's name the roster block's own name, which is what the patterns
are written against, and check what else keys off the current shape before changing it.

**Model recommendation.** medium.

**Verify.** An in-engine assertion that a campaign spawn's name matches its authored pattern and
that a bias actually changes a ranking, plus the existing `campaign-roster` and targeting suites
green.

**⚠ Traps.** ⚠ Node names are used for more than bias matching (the wingman binding and the
`primary_target` resolution read names too); change the name in one place and check every reader.

## D33 ☐ World objects are hostile to everyone (`BL-407`)

**Goal.** World scenery is neutral unless the data says otherwise, as the original has it.

**Evidence (confidence: traced).** `AimAssist.AddStructures` (`AimAssist.cs:495`) defaults every
structure's `team` to `WorldTeam`, which reads as hostile to all comers; the original falls through
to neutral. Campaign missions are full of authored scenery near objectives, and D36 has just widened
AI target selection to sweep structures, so wrong hostility now mis-steers wingmen and the gun
assist rather than sitting harmlessly in a list.

**Approach.** Give the fall-through the neutral value and check the callers that assumed hostility.

**Model recommendation.** medium.

**Verify.** The `targeting-candidates` and `target-pool` suites, plus a check that a same-team and a
neutral structure are both refused where the data says they should be.

**⚠ Traps.** ⚠ D36's landed rule stands: a large nearby structure can outrank a distant fighter
because the ranking is minimised, so a change here must not be judged by "the AI stopped shooting
buildings" alone.

# Wave E — answers and housekeeping

## E41 ☐ Decode the original's per-pylon ordnance id (`BL-445`)

**Goal.** An answer: what the original writes into a plane record's ordnance field, and how it maps
to weapon defs.

**Evidence (confidence: lead-only for the decode; traced for the stand-in).**
`CampaignProfileStore.cs:27` declares `Ordnance = new int[8]` serialized as raw ints (`:174`,
`:263`), and `CampaignLoadout.cs:54` reads `plane.Ordnance[cell] - 1` as a table index. That is a
deliberate CSVM-side stand-in chosen so the Ammo Selection screen could ship, not a recovery of the
original's vocabulary. Nothing depends on the stand-in outside those two files.

**Approach.** Read the field out of a real plane record and map it to weapon defs. The deliverable
is the answer written onto `docs/formats/saved-games.md`; changing what CSVM stores is a separate
call, since it would migrate every existing profile.

**Model recommendation.** medium, a bounded decode with a documented landing.

**Verify.** The mapping stated for every value the shipped data uses, with the record it was read
from cited.

**⚠ Traps.** ⚠ The user's `CrimsonSkiesGame\SavedGames\` is read-only evidence and never a write
target. ⚠ Decision 1 of the campaign plan puts writing the original save format out of scope, so
this item stops at the answer.

## E42 ☐ Decide how the MPG cinemas would play, before any code (`BL-446`)

**Goal.** A recorded decision: whether Godot's own video playback can take the shipped files, or
whether they need transcoding at extract time.

**Evidence (confidence: traced for the absence).** Nothing plays video today: the only MPG mention
in `CSVM/src` is a directory name in `Mech3/PatternLibrary.cs:100`, and there is no
`VideoStreamPlayer` and no transcode path. Decision 2 of the campaign plan put the cinemas out of
scope and said to file the item when M5 closed, which is this.

**Approach.** Probe what the shipped files actually are, check them against what the engine will
accept, and write the decision down. No player code until the decision exists.

**Model recommendation.** medium, low effort. The deliverable is a short answer.

**Verify.** The decision recorded on the entry with the container and codec facts that drove it.

**⚠ Traps.** ⚠ Do not start with a transcode pipeline; if the engine plays the files as they ship,
the pipeline is the expensive wrong answer.

## E43 ☐ Close what is already done, fix what is merely stale (`BL-243`, `BL-427`, `BL-426`)

**Goal.** Three backlog entries stop lying: two are closed because the work landed, one keeps its
bug and loses its wrong file reference.

**Evidence (confidence: traced).** **`BL-243`** (cross-mission persist log) was built by this
milestone: `Session/CampaignPersistLog.cs` captures, merges and applies; `AnimDefs.cs:152` parses
`PersistLog`; `CampaignDirector.cs:317` applies and `:461` merges; the state persists as schema v2
(`CampaignProfileStore.cs:97,193,467`); the `campaign-persistence` suite covers it. Commit
`9775378a` said the item "stays open until D31 wires the layer into a session", and D31 is now ☑, so
that condition is met and the entry's own "there is no campaign flow yet, so today this is
unobservable" is stale. **`BL-427`** (extract `langui.dll`'s string table) rests on a premise that is
no longer true: `ExtractRof.ps1:366` already pulls the `langui.dll` and `language.dll` STRINGTABLEs
and emits `extracted/rof/ui_strings.json` (`:391`), read through `Mech3/UiStrings.cs`, and the
specific deliverable it named (the ammo description pane) is drawn from it today at
`CampaignAmmoPage.GroupDescription:349-351`. **`BL-426`** (a failed stunt run records NEW BEST) is a
real open bug whose evidence is stale: the unguarded `RecordIfBest` now lives at
`Session/InstantActionDirector.cs:761-764`, not the `GameSession.cs:3083` the entry cites.

**Approach.** Close `BL-243` and `BL-427` through `/close-backlog-item` so the evidence lands in the
closing commit; correct `BL-426`'s body in place. Before closing `BL-427`, confirm the 3370 string
block really carries the description prose in the extracted data, since that is the one claim not
yet checked.

**Model recommendation.** medium, low effort.

**Verify.** The two entries gone from `backlog.md` with their evidence in the closing commit
message, and `BL-426` citing a line that exists.

**⚠ Traps.** ⚠ `BL-243`'s closure should carry its two untested A/B questions forward in the closing
commit rather than dropping them: whether the log commits at damage time or at mission completion,
and whether an Instant Action session loaded after a campaign mission in the same process picks the
log up (`InstantActionDirector` has no apply call). ⚠ `BL-426` is named out of scope by the campaign
plan's own scope line, so fixing the entry's body is this plan's business and fixing the bug is not.
