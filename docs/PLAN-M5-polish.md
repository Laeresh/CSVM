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

**Battery on the fully merged tree** (build clean with 0 warnings, run centrally and serialized with
no sibling worktree building): units **2363 of 2363**, engine suites **118 of 118** with engine errors
clean, and all **16 golden shots hash-identical**, so a wave that rewrote the campaign screens, moved
the objectives readout off the flight HUD, retired an overlay into the targeting subsystem and
changed the anim runtime's instance walk moved no pixel of any flight shot. ⚠ The engine-error screen
reads a shared log every concurrent Godot writes into, so error counts taken while sibling lanes were
building are meaningless; this one was taken alone.

**Wave A's purpose is met: the first campaign mission can now be flown to its end at the controls.**
Wave F is what a complete flown run of it reported next, and it supersedes part of A1: objective
sites belong in the enemy selection cycle rather than in the standalone overlay A1 built (F53).
Wave G is different in kind from both: nothing in it was reported at the controls, and every item was
found by an item that landed, so each one names the item that filed it.

⚠ **Wave A's items were found by a human at the controls and then traced, and three of them are not
what the symptom said.** The objectives register correctly and the player simply cannot find the
sites (A1); the empty cutscene is a session that never spawns its zeppelins at all (A2); the wingman
spawns exactly where the data says and then fails to keep station (A3). Read each item's Evidence
before acting on its title. Every
other item was re-verified still-open against both the record (`git log --grep`) and the code before
it was written down, and carries the `file:line` that check produced. Two entries did not survive
that check and are closed rather than planned (E43). **Out of scope:**
`BL-463` (Change Memento) rests on `BL-256`, which the user deferred by explicit decision, so it
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

1. ☑ Objective targets and help labels are never drawn, so the sites cannot be found (`BL-468`)
2. ☑ A campaign session never spawns its zeppelins or generators (`BL-451`)
3. ☑ The campaign wingman cannot hold station on a real player (`BL-457`)
4. ☑ The music channel drowns the briefing (`BL-455`)
5. ◐ The cutscene letterbox leaks the world at its left and right edges (`BL-452`)
6. ☑ `DANGER_ZONES_COMPLETED` is never fed in a campaign mission (`BL-458`)

### Wave B — where things are shown, and where they are heard

11. ☑ The objectives readout belongs on the pause screen (`BL-466`)
12. ☑ Radio calls play positionally (`BL-465`)
13. ☑ Full-screen campaign boards with the original's buttons, worked on a pad (`BL-449`)
14. ☑ The briefing reveal, drawn as authored (`BL-464`)

### Wave C — the gaps M5 named

21. ☑ The `landings.zrd` approach trigger, which is the mid-mission cutscene gate (`BL-467`, `BL-035`)
22. ☑ The VO dialogue chain player (`BL-461`)
23. ☑ PNG on the hangar art seam, and what to do about JPEG (`BL-444`)

### Wave D — the campaign's own rough edges

31. ☑ The load screen's composed artwork (`BL-409`)
32. ☑ Spawn node names defeat `rating_biases` on the campaign path (`BL-401`)
33. ☑ World objects are hostile to everyone (`BL-407`)

### Wave E — answers and housekeeping

41. ☑ Decode the original's per-pylon ordnance id (`BL-462`)
42. ☑ Decide how the MPG cinemas would play, before any code (`BL-446`)
43. ☑ Close what is already done, fix what is merely stale (`BL-243`, `BL-427`, `BL-426`)

### Wave F — the second at-the-controls pass, once the mission could be finished

51. ☑ The cutscene camera has no gamez binding, so a mid-mission cutscene ends in one frame (`BL-470`)
52. ☑ The cutscene node reparent is unimplemented, so the intro frames nothing (`BL-471`)
53. ☑ Objective sites belong in the enemy selection cycle, and a moving site's marker must track (`BL-472`)
54. ☑ A zeppelin's authored team is unread and its wake-up has no seam (`BL-476`)
55. ☐ The wingman's formation is looser than the original's (`BL-473`), blocked on F56
56. ☑ `wingman-station` is red under main's flight plant (`BL-474`)
57. ☑ The targeting readout drops the militia name (`BL-475`)
58. ☑ Alpha-cutout geometry is solid to weapon rays (`BL-477`)
59. ☑ A net has no stop-point state, so the PANDORA never halts (`BL-478`)

### Wave G — what the first six waves left open

61. ☐ The intro cutscene stages no aircraft, because the node its definitions animate does not exist (`BL-482`)
62. ☑ A zeppelin's turrets and its damage zones carry no owning identity (`BL-476`), behind D33
63. ❌ The music channel has no 15 s refusal hold (`BL-480`), disproved: the rule is real and unreachable
64. ❌ The 26 `GRAPHICS/*.JPG` draw nothing (`BL-479`), disproved: they already draw as board pictures
65. ☑ `campaign-objectives-hud` cannot fire its own check on C3 (`BL-481`)
66. ☑ Two persist-log behaviour questions, carried out of `BL-243`'s closure
67. ☑ `BL-181`'s blocker now reads as discharged when it is not
68. ❌ Why the scaffolding read differently at the controls (`BL-477`), disproved: ours reproduces it

**Everything open is either in flight, queued behind a stated blocker, or waiting on the user.** Three
items carry over from the earlier waves rather than being restated in Wave G: A5, which is traced to
two causes and not yet built; D33, which waited on D32 and F54 and is now unblocked; and F55, which
has its instrument and needs the user's verdict against it. Inside Wave G, G62 runs behind D33 for
the reason F54 gives, F55 is the one item no amount of work here can close, and the rest are
independent of each other. G68 was blocked on the user and is not any more: their account of the
original run makes it a splash-occlusion measurement with an instrument that already exists.

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

**Wave G's shape is different from the others.** Its items were not reported at the controls; every
one of them was found by the work that closed something else, so each names the item that filed it.
D33 and G62 are one ordering and touch the same targeting files, so run D33 first and G62 behind it,
never as parallel worktrees. G64 changes the extraction pipeline and bumps `VERSION.json`, so it
cannot share a tree with anything that reads `extracted/`. G61 reaches the anim runtime and the
cutscene host, which A5 also touches, so sequence those two rather than running them together. G63,
G65, G66 and G67 are independent of everything, and G66 is a research item whose deliverable is an
answer. G68 reaches `Projectile` and the census stage in `alpha-cutout-ray-census`, which nothing
else in this wave touches. F55 cannot be scheduled at all until the user answers it.

---

# Wave A — what stops the campaign being playable

## A1 ☑ Campaign objectives do not register in a flown mission (`BL-468`)

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

## A6 ☑ `DANGER_ZONES_COMPLETED` is never fed in a campaign mission (`BL-458`)

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

**Landed in full.** The orchestrator applied the one missing field at merge, so
`GameSession`'s `Attach` call now passes `Gamez = state.Gamez` and a real flown campaign session arms
`CampaignDangerZones`. `campaign-danger-zones` is green in the full battery.

**Was not landed by the item itself, and is recorded because the handoff pattern recurs:**
`GameSession.cs`'s own `Attach` call
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

## A3 ☑ The campaign wingman cannot hold station on a real player (`BL-457`)

**Goal.** The wingman stays with the player instead of falling behind and climbing away.

⚠ **The reported defect is gone at the controls, and the item stays open for one reason only.** A
complete flown run of the mission reports the wingman near the player for the whole flight, closer
than the traced 838 m by a wide margin and never overhead, so the fall-behind-and-climb-away failure
this item names does not survive a real session. Two things remain. The formation is looser than the
original's, which is a fidelity gap and not this failure (`F55`). And `wingman-station` is red under
main's flight plant, which is a suite that must not be left failing (`F56`). A3 closes when `F56`
does; the at-the-controls verdict above is the evidence that its subject is fixed.

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

## A5 ◐ The cutscene letterbox leaks the world at its left and right edges (`BL-452`)

**Goal.** The bars cover what they are meant to cover, at every window aspect, with nothing drawn
over them.

⚠ **Traced, and it is not a flicker.** A second at-the-controls pass reframed the symptom: "some
elements are rendered before the box, on the left side I can see sometimes the environment, probably
something to do with the wider display." That is a geometry fit, not a temporal event, and it
explains why two full real runs and every suite found no visibility gap. Two causes, both traced.

**(1) The fit solves for equality, so the margin is exactly zero at 16:9 and wider.** The bars are
world geometry: one card at `z = -7.5`, `x = ±6.6461835`, `y` half-extent `4.0474105`
(`extracted/C3/gamez/models.json` first record, node `transform: "Initial"`).
`CutsceneController.FrameBars` (`:486-509`) sets the camera's VERTICAL fov from
`Mathf.Min(halfHeight, halfWidth / aspect) / dist`. Those two terms cross at
`halfWidth / halfHeight = 1.64211`. Below it the height term binds and 4:3 keeps 19 % horizontal
margin. At or above it the width term binds and the fit becomes an identity, `tan(fov/2)·dist·aspect
= halfWidth`, so the card's outer edge lands on the frame edge with zero margin in exact arithmetic.
That covers 16:9 (including the project's own 1280x720 default, `project.godot:15-16`), 1920x1080,
21:9 and 3440x1440; 16:10 keeps only 2.6 %. Vertically the card overhangs by 8 % at 16:9, which is
why the leak is a left/right artifact and never a top/bottom one. With 4x MSAA (`project.godot:29`)
that boundary column has partial coverage and blends the bar with what is behind it, and which side
rounds inside depends on the projection at that frame, so it comes and goes as the camera moves.
⚠ This last step is lead-only, but it is the only asymmetry available: nothing in the card geometry,
the pin or the formula is left/right asymmetric.

**(2) Three canvas layers genuinely draw over the bars.** The bars carry no `CanvasLayer` at all, and
every `CanvasLayer` draws above all 3D content. During a campaign cutscene `ObjectivesHud`
(`UI/ObjectivesHud.cs:97`), `ObjectiveMarkerHud` (`:249`) and the `LensFlareRig`/`WeatherRig` overlays
are all up and untouched: `CutsceneController.ApplyPresentation` (`:417-424`) only calls
`SetPilotHudVisible(false)` on each `FlightController`'s own HUD canvas, and nothing else in
`CSVM/src` consumes `Presenting` outside the suites. This is the user's "elements rendered before the
box" as a literal fact, though it is UI rather than the environment they saw.

**Approach.** Give the fit a margin instead of solving for equality (divide the chosen half-extent by
a small safety factor, or clamp the fov a degree tighter) so the card overhangs at every aspect the
way it does at 4:3 by design; and hide those canvas layers while `Presenting`.

**⚠ Traps (this cause).** ⚠ Tightening the fov changes the framing of every cutscene at every
aspect, so it moves any golden that includes one; check `analysis/goldens/manifest.json` before
reading a hash change as a regression. ⚠ Do not scale the card node: `PoseChannel.PoseAtNode`
re-applies `rest.Basis.Scale` every tick, and `CutsceneController.BindWorld` measures `_cardBox` once
(`:159-165`) precisely so a scaled card cannot read its own answer back. ⚠ The wide-aspect fov is
already extreme (83° horizontal at 16:9) and the cutscenes doc records the framing fov as undecoded;
adding margin widens it further, so if the framing is to be revisited on fidelity grounds, do it in
this change rather than twice. ⚠ Do not infer the user's display aspect: nothing in the repo records
one, and the project default already breaks, so this needs no wide monitor.

**Goal (as originally written).** The bars hold steady for the whole cutscene.

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

## B11 ☑ The objectives readout belongs on the pause screen (`BL-466`)

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

**Landed.** The readout's layer sits with the pause board on `HudLayers.Board`, added after it so
tree order puts it above that board's backdrop, and is visible only while `PauseState.Paused`. The
flight HUD carries gauges alone, which is what the reference frame shows. Both traced causes of "the
tick was not on its line" are fixed: a row the mission gives no message key is dropped from the
DRAWING rather than shown as a mark against blank space (`BuildLines` still returns one line per
graph row, so the suite counts against the graph, and C1/M02 draws 4 of 5 while C3/M01 draws 4 of 6,
matching the original's four-line parchment), and the readout is drawn in the reference frame's
top-right corner with the tick in a column of its own, so a completed line's text starts where every
other line's does. The graph binding, the polling and the completion marking are untouched. Two
scripted-capture doors came with it, because a headless run could reach neither the pause screen nor
a completion: `--debug-pause[=frame]` and `--debug-objective=N`, the latter waking one objective and
driving the nodes its `INACTIVEn` conditions name inactive so the completion comes off the graph's
own conditions rather than being faked into the display. ⚠ `--destroy=` alone cannot do this: the
`INACTIVE` leaves are not destructible def names and a dormant objective cannot complete at all.

**Verified.** A paused C1/M02 capture shows the Objectives panel top-right with four lines and the
tick on the second one's own line; the same mission in flight shows the gauges alone with nothing of
the objectives on screen. The suite is kept and strengthened rather than deleted, which the trap
required: it now runs with a paused `PauseState` so the drawing path is exercised, and adds a check
that no drawn line is a mark against blank text, before and after the completion drive. On the merged
tree `campaign-objectives-hud` passes with errors clean and units are 2356.

⚠ **The suite fails on `--chapter=C3`, and that is pre-existing rather than caused by this move.**
The identical failure reproduces on commit `4ec47b14` from a separately built worktree (METHOD-8), so
`BL-481` carries it: the driver completes no objective at all on C3/M01, which makes the check report
a vacuum rather than a display defect. The panel's glyphs, colours, type sizes and margins are TUNE:
the reference frame fixes the corner and the four-line content, not our metrics.

## B12 ☑ Radio calls play positionally (`BL-465`)

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

**Landed.** ⚠ **The contradiction resolved against this repo's own documentation, not against the
user.** `3D` is the data's positional opt-in rather than its default: of 2766 `SETS` definitions only
124 carry it, `RANGE` occurs with nothing else, and `QUEUE` and `3D` never co-occur. Across every
`objectives.zrd` in the install, of 498 distinct callout cue names **not one** names a `3D`
definition, so a mission callout has no distance model in the data at all and the answer is the
channel. `Mech3/MissionRadio.cs` is that channel, owned by the mission layer through
`WorldSounds.Radio`, with `MusicPlayer` as its model and streams from `WorldSounds.StreamFor` so the
existing prewarm keeps a callout alive after the archive closes. `CampaignDirector.PlaySoundGroup`
now offers a cue to the radio and falls through to `PlayOneShot` for one it does not own, and
`STOP_QUEUED_SOUNDS` stops being a named no-op.

⚠ **The 15 s interval is a music-cue guard, not speech spacing, and `docs/formats/objectives.md` is
corrected.** A first cut gated every call on 15 s, which would have silenced every combat bark, since
those author `QUEUE 0.5`. `FUN_0046caf0` gates that rule on `FUN_00480460`, which is bit 3 of the
game's sound-flag word, and the keyword table at `0x4802e0` reads `NOROGUE` 1, `WINGMAN` 2, `VOICE`
4, `MUSIC` 8, `SFX` 0x10, `OPTIONAL` 0x40. So it is an is-music predicate with one call site, and it
REFUSES a cue rather than delaying it. The 1 s is a per-cue start delay rather than an inter-call
gap. Two further decodes came with it and are implemented: the parser adds 0.3 s to an authored
`QUEUE` value, and a definition with no `QUEUE` key keeps the field's initial 5.0 s. ⚠ The tolerance
clock runs only while the channel is BUSY, because `QUEUE` is how long a line waits for a channel
somebody else holds; charging the start delay against a 0.5 s bark would drop it before it spoke.

**Verified.** `mission-radio` over C1/M02 asserts that none of the mission's callout cues names a
positional definition (15 radio lines, 6 chains, 4 music, 0 positional) and that a whole queue drains
with `WorldSounds.OneShotsStarted` unmoved at zero. Re-run on the merged tree with the
`CampaignDirector` handover applied: `1 passed, 0 failed, errors=clean`, 116 suites registered.
`campaign-objectives-hud`, `voice-runtime`, `ai-voice` and `music-states` all still pass. Heard end
to end while flying away is the user's half and is not yet done.

⚠ **Named rather than invented, and left for a controls judgement:** `AiVoiceRuntime` plays the
combat voice `id<N>` clips positionally, and those definitions are `QUEUE 0.5` with no `3D` and no
`RANGE`, so that placement is the same invention this item removed from the mission callouts.
Rerouting it would move `voice-runtime` and `ai-voice`. The 15 s music guard itself belongs to
`MusicPlayer` (`BL-480`), since `mu*` cues are routed there before the radio is consulted.
`QPRIORITY` writes a 3-bit priority at bits 10 to 12, is read nowhere, and which direction is more
urgent is undecoded, so nothing acts on it. The wake/complete asymmetry is not implemented rather
than guessed, because the executor cannot tell the two directives apart.

## B13 ☑ Full-screen campaign boards with the original's buttons, worked on a pad (`BL-449`)

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

**Landed.** Every campaign screen draws as one composed board at the original's own authored pixel
positions: its painted background, its own button plaques, and its text in the widgets the layout
names. Five new modules carry it (`BoardFit` the mapping, `ComposedBoard` the engine-free model,
`CampaignBoards` the six screens' geometry, `ComposedBoardView` the renderer, `BoardPalette` the
per-background ink), and `ICampaignPage` gained `Pictures`, `Strokes`, `Captions` and `Button(row)`.
**The geometry is decoded, not guessed**: `extracted/rof/ASSETS/LAYOUT.CSV`, 56 KB and sectioned per
screen, carries every widget's art path and X,Y for the five script-driven screens, and every screen
button turns out to be a four-frame vertical strip ordered disabled / normal / rollover / depressed,
matching that file's own `ColorDisabled,ColorActive,ColorRollover,ColorDepressed` columns. Focus is
the rollover frame and a confirm the depressed one, both frames the original's art already carried.
Four rows ship with unresolved authoring macros; they were resolved by template-matching each
button's own bitmap against the reference screenshot, and what licenses reading the Y off that match
is that the X it returned equalled the layout row's own X in every case.

**The scaling decision, in writing, on `docs/org/campaign-board.md`:** one uniform scale
`min(w/800, h/600)`, board centred, remainder letterboxed, every bitmap sampled nearest-neighbour.
Both alternatives are rejected there with reasons. An integer scale floors to 1 at both 720p and
1080p, leaving an 800x600 island in a 1080p window. A fit-to-height bleed has nothing to bleed, since
the plaques anchor to the authored bottom edge and the backgrounds are exactly 800x600.
Nearest-neighbour answers the soft-upscale trap: a fractional scale duplicates rows unevenly, so it
reads chunky rather than blurred, and text is drawn as a real face at `scale * authoredSize` rather
than as scaled glyphs. Two positions are chosen rather than decoded and marked as such in code and
docs: the cabin's memento window and the profile screen's logo. A standing gap closed on the way, the
cabin's photograph, which the renderer draws because `Image.LoadFromFile` takes the shipped JPG.

**Verified.** A scripted shot of each of the six screens at 1280x720 read against its
`OriginalScreenshots\Campaign *.png` reference: backgrounds, plaque positions and plaque art match. A
four-step focus pass over the cabin shows the rollover frame following the cursor through NEXT
MISSION, PREVIOUS MISSIONS, PLANE CONSTRUCTION and RETURN TO MAIN MENU, each step being
`CampaignFlow.Move(1)`, the exact call a pad press makes; `--menu=campaign-cabin:2` was added because
`--det` disables pads, and the press frame is covered by a unit test. Build clean with 0 warnings
including StyleCop, and 2363 units on the fully merged tree.

⚠ **`BL-181` is NOT discharged and stays blocked, decided explicitly as the trap required.** Its
blocker is a menu hub with its own type scale to review `MarkerHud` and `StuntScoreboard` against;
these boards are painted original artwork with a per-background palette and no shared type scale, so
they supply nothing to review against. Its blocker wording should be made concrete, because someone
will now read "the campaign boards landed" as discharging it.

**Not reached, and named on `docs/org/campaign-board.md` so their absence does not read as a decode
gap:** the profile screen's `CrimFlag.MPG` movie, whose shipped still is a placeholder; the cabin's
map pins, where `LAYOUT.CSV` puts them at z 0 BEHIND a z 4 background, a contradiction left
unresolved with `MapPinCount` kept as the tested stand-in; per-widget chrome; and the flight check's
objectives note, blank because the page does not load objectives. ⚠ A rename came with it:
`CampaignBoard` became `ComposedBoard` and `CampaignBoardView` became `ComposedBoardView`, so D31
could reuse the surface, since the load screen is not a campaign screen. C23's own `Art` override on
the flight check page is superseded here, because the composed board draws that silhouette directly
rather than through the shell's row-art hook.

## B14 ☑ The briefing reveal, drawn as authored (`BL-464`)

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

**Landed.** `BriefingReveal` already modelled every element and nothing was drawing them. The reader
was missing two fields the drawing needs and now parses them: a step's `center` flag, which most
flags and photos use, and a `Line`'s colour and points. The page emits the map, the parchment at its
authored `[0, 295]`, then every visible element at its own position, opacity and rotation, `ToBack`
ones first, and the route line as a stroke. ⚠ The timing is untouched: the marker-sorted-by-sample-
offset rule lives in `BriefingReveal` and `WavCues` and was not gone near, and no fudge factor was
added anywhere, since every duration is the authored constant the reveal already ran.

**Verified.** A timed sequence from one scripted run at 6, 12, 24, 40 and 70 seconds reads as
`CAP-42`'s own progression does: at 6 s the sub flourish alone with no flags and an empty parchment;
at 24 s two numbered flags with cast shadows, the escort planes, the zeppelin photograph pinned
top-left over the paper stack, and two written objective lines; complete by 70 s. The flag count and
the parchment's line count grow together. ⚠ One mission only: the briefing aid always takes its
seeded profile's `NextMissionSeq`, and naming a seq would have meant a second CLI shape in a shared
parse path, so C1/M01's four-flag reveal is unshot.

# Wave C — the gaps M5 named

## C21 ☑ The mid-mission cutscene trigger (`BL-467`, `BL-035`)

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

**Landed.** `LandingApproaches` (`src/Mech3/LandingApproaches.cs`) reads a chapter's `landings.zrd`
and resolves each row against the gamez: the approach node, its arming `land_on` child, and the
condition volume, which is the single authored triangle on the node's `cone`, `half_cone` or
`sphere` child expressed in the approach node's own frame. `LandingApproachRuntime`
(`src/Session/LandingApproachRuntime.cs`) ticks that table against the flown aircraft in the
original's own order (arming gate, speed band, attitude, volume) and starts the row's definition;
an `auto` row raises `AutoLandOffered` rather than starting anything, which is the auto-land prompt.
The condition object was decoded rather than guessed: the three shape classes, their vtables and
their containment tests are written up on
[`docs/formats/anim-definitions/cutscenes.md`](formats/anim-definitions/cutscenes.md), and the
`angle` test is exact rather than approximate, a geodesic quaternion angle over the player's whole
orientation, roll included. Hosting widened by definition, never by code:
`CutsceneController.HostDefinitions` takes the names `WorldSession` computes from the resolved rows
plus their `CALL_ANIMATION` closure, and `Hosts` replaces the `IsIntro` gate on the dispatch.
Instant Action stays out two ways: a row whose animation the mission does not carry is dropped at
load (the original's own rejection), and the table is resolved only for a story mission, because
C3/IA1 does carry `hooked_to_klondike` with its approach shipped armed.

**Verified.** `dotnet build CSVM/CSVM.sln` clean, 0 warnings; `dotnet test` 2334 passed, 0 failed;
`.\CheckCommentCaps.ps1 -Summary` clean. The new `landings-approach-trigger` suite drives C3/M01's
built world: 8 of the chapter's 11 rows resolve, all 8 bind, a mission carrying none of the
animations resolves none, the six drop cones read back at a 47.3° half-angle over 250.8 m, the drop
ring starts disarmed and flying it fires nothing, the mission's own chain arms it by flying the
site, flying `do_approach1` then starts the drop, the host takes its callbacks 11 and 2, `texdrop`
reaches EXECUTED and `OBJECTIVE21` (PRIMARY 2) completes; flying `pz_manual_land` afterwards starts
`hooked_to_klondike` and completes `OBJECTIVE14` (PRIMARY 4), the condition `PLAN-M5-campaign` E41
could only satisfy by waking `INSTANTWIN` directly. 17 assertions, green on two consecutive runs.
A `--campaign=` launch arms the same 8 rows and free flight over the same chapter arms none, which
is the session wiring. ⚠ The drop itself is not reachable from a single scripted `--campaign=` run:
the mission's intro definition still holds the world at 40 s of sim time, and the trigger wants two
passes over the site, one to arm it and one to fly it. The suite is the flown evidence.
**Battery on the merged tree:** build clean, units 2334/2334, engine suites 113/113 with errors
clean, all 16 golden shots hash-identical, so widening the cutscene host by definition name reached
no flight the goldens cover.

## C22 ☑ The VO dialogue chain player (`BL-461`)

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

**Landed.** The consumer `docs/formats/sounds.md` named now exists, and it is B12's channel rather
than a second mechanism: `MissionRadio.Cue` speaks a `SoundGroup.Chains` script in order as one call,
a later cue queues behind it instead of cutting in, `Cancel` is `STOP_QUEUED_SOUNDS`, and a call that
waits past its `QUEUE` tolerance is dropped unheard. `SoundGroup.Pick` is untouched. The owner named
by the item is the mission layer, through `WorldSounds.Radio`.

**Verified.** `mission-radio` over C1/M02 counts `snd_NW2Start` starting all three of its lines in
order (`BigJohn_1`, `Tex_2`, `Ilsa_3`) against real prewarmed streams in a built world, with
`snd_c2-NW-m2_Jack_17` queued behind it speaking fourth rather than cutting in, zero dropped, and
`Cancel` removing an unstarted call. `WorldSounds.OneShotsStarted` stays at zero throughout, which is
the proof that nothing on this path became a positional emitter. ⚠ The `Chains[0]` question was
settled by census rather than by trusting the prose that says the same: of the 222 groups carrying a
chain, none carries more than one, so the index is total and not a pick.

## C23 ☑ PNG on the hangar art seam, and what to do about JPEG (`BL-444`)

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

**Landed.** The seam loads by file name rather than by decoder: `Mech3/ArtImage.cs` takes a path and
picks `PngImage` or `TgaImage` from the extension, so a screen names the file the extraction ships
and stops caring about its format. `UI/PlaneDiagrams.cs` frames the two sheets for every screen that
wants them (`OL_PLANEDIAGRAMSTOP.PNG` 204x1870 and `OL_PLANEDIAGRAMSFRONT.PNG` 245x1100, eleven equal
frames each in airframe-id order); that framing was `CampaignAmmoPage`'s two private helpers, and
three screens want the same picture now, so it moved out rather than being copied twice. The airframe
list draws the focused airframe's plan view under its blueprint, and the campaign flight check draws
the pilot's aircraft head-on over its silhouette. ⚠ **That second screen was drawing no art at all,
which nobody had reported**: the shell hangs a page's row picture off its main one
(`LaunchMenu.cs:2168` gates the whole `beside` block on `PageArt()` being non-null) and
`CampaignFlightCheckPage` overrode `RowArt` without ever overriding `Art`. `--menu=airframe` now
opens the airframe list without the defaults ask covering it. JPEG is transcoded at extract time
rather than decoded in `Mech3` (`BL-479`), so a `.JPG` returns null and the screen draws nothing,
which is the never-invent answer: `ArtImage` has no fallback path and `PlaneDiagrams.Frame` returns
null rather than slicing a sheet whose height is not a whole multiple of eleven.

**Verified.** `dotnet build` clean with 0 warnings, `dotnet test` 2346 passed (2342 before, plus four
new `ArtImage` cases, one of which pins that a `.JPG` returns null rather than a stand-in),
`dotnet format --verify-no-changes` clean, `CheckCommentCaps.ps1 -Summary` clean. `--menu=airframe`
and `--menu=campaign-flightcheck` both draw two captioned pictures where the flight check drew none,
and `--menu=campaign-ammo` is pixel-identical before and after the shared-helper move (shots under
`.scratch/c23/`). The census: of 254 `extracted/rimage` PNGs, 149 reach the briefing page's 24
mission states, 3 are hard-coded by the HUD font and the impact reticle, 2 are named in `Briefing.zrd`
chrome that nothing draws, 28 are named only by the escape and Loading dialogs which have no reader,
and 72 are referenced by nothing, so **105 of 254 reach no page today**, the largest families being
the 25 `ms_p_*` scrapbook momentos (which Previous Missions would want) and the 9 `mp-*` multiplayer
event icons. The diagrams themselves were never in `rimage`; they live in
`extracted/rof/ASSETS/GRAPHICS` with the seam's other art. The JPEG decision rests on every SOF
marker being read across the 26 files: 24 are baseline `SOF0` but `CR_BACKGROUND` and
`MP_LOBBY_BACKGROUND` are `SOF2`, so a baseline decoder would leave those two blank.

# Wave D — the campaign's own rough edges

## D31 ☑ The load screen's composed artwork (`BL-409`)

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

**Landed.** The load screen draws the original's composed artwork through B13's surface, inheriting
the same authored-pixel scaling rule. Two compositions, the split the original makes: a campaign
launch gets `loadframe` with the unlit scale at `90,548`, and everything else gets `loadframempt2`
with its three authored photographs each centred on `197,157` / `197,307` / `197,457`, the unlit lamp
strip at `564,546` and one still propeller frame at `506,549`. Free flight and dogfight are ours
rather than the original's and take the non-campaign screen, which is stated in the code and the docs.
⚠ **The progress bar is explicitly not taken on**: the unlit strips are drawn and never filled and the
propeller draws one frame and never steps. Both need the build decoupled from the draw, since
`BeginLaunch` shows the board, lets one frame render, then builds synchronously, so nothing can be
redrawn during the build at all; and separately the extraction ships only the six range endpoints of
the propeller cycle, so the authored 6 fps animation could not be reproduced even with a clock. Text
placement is ours rather than decoded, since the decode carries no text coordinates, and that is
marked in both code and docs.

**Verified.** A scripted shot of each family against the decode's own description, through a
`--menu=loadboard` / `--menu=loadboard-campaign` door added because the real screen is up for two
frames during a build and torn down before anything renders, so it cannot otherwise be photographed.
⚠ The subject line in those shots reads "C1 · Free Flight" because the aid is not a real launch. Not
reached: the campaign sheet's parchment objectives list, which the decode names without giving a
position, and for which there are no objectives to list at that point anyway.

## D32 ☑ Spawn node names defeat `rating_biases` on the campaign path (`BL-401`)

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

**Landed.** A campaign spawn's node is named for its roster block. ⚠ The item's own `FlightRoster.cs:152`
pointer was stale: the name is set at `AiFlightAssembler.cs:158`, while `FlightRoster.cs` carries the
`AiSpawn` record, which is where the seam had to open. `AiSpawn` gains an optional `NodeName` the
assembler prefers, with `ai{n}_{plane}` kept as the fallback for the spawners that have no authored
name (`--ai`, the Instant Action fan, the generators), and `CampaignRosterPlan.SpawnFor` is now the
one place a planned block becomes a spawn record, so production and the suite construct the same
thing. A human-piloted candidate answers to the `player` role in `rating_biases` as it already did in
`primary_target`, which is the other half of the same mismatch. Both were dead on the campaign path;
both are live. Every reader of the old shape was checked: the roster dictionary and `ResolveLeader`
key by BLOCK name and never by node name, which is the asymmetry that hid this;
`CampaignDirector.FindNodes` walks the world index, which a flight rig never enters, so a spawn
cannot shadow a world node; block names are unique per mission and human rigs are `player1`/`player2`,
so there is no sibling collision. Two cosmetic consequences are recorded and left alone: a campaign
spawn's `--debug-markers` tag reads `MEDKESTREL` where it read `AI1`, and `--target=` addresses a
campaign spawn by its block name while the documented `ai1_player_fury` form still holds elsewhere.

**Verified.** The census: across all 53 shipped `aiv.zrd`, 697 bias entries carry a pattern and 331
name a roster block in the same mission (157 the `player` block, 174 real AI blocks). Every one was
dead. The new `roster-spawn-names` suite drives C1/M02's shipped roster through the session's own
spawner: `wingman_4`'s authored `["bloodhawk_2", -1.0]` matches the spawned node and moves the live
pick from the candidate at 400 m (rank 1360, now 1e21) to the one at 900 m (rank 1860, unchanged),
and `bloodhawk_2`'s `["player", 1.0]` takes the human rig at 1500 m (rank 2100 to -97900) over a
wingman at 900 m. Perturbation each way, run separately: forcing the counter form back fails exactly
the three name-dependent checks, and forcing the bias name back to `fc.Name` fails exactly the role
check. ⚠ An early revision passed its biased arms vacuously, because `bloodhawk_2` and `wingman_4`
ship `deactivated` and never reached the scan; the suite now clears `Inert` and asserts `InPlay`
before measuring, which is what makes that arm mean anything. `dotnet build` clean, `dotnet test`
2342, `--run-tests=roster` 3/3, `--run-tests=target` 6/6, `--run-tests=campaign` 10/10.

⚠ **The zeppelin half of the same pattern is still dead and this fix cannot reach it.** C3/M01's
Kestrels author `[["player", 0.5], ["piratezep", -1.0]]`; the player half is now live, but a
zeppelin's destructible instances are its ZONES (`gasbag1..6`, `leng11`, `lbroad11`) and
`TargetPool.NameOf` returns the zone's anchor name, so the pattern needs the zone's owning zeppelin
identity carried alongside. That is zeppelin-identity work and belongs with F54.

## D33 ☑ World objects are hostile to everyone (`BL-407`)

**Goal.** World scenery is neutral unless the data says otherwise, as the original has it.

**Evidence (confidence: traced).** `AimAssist.AddStructures` (`AimAssist.cs:495`) defaults every
structure's `team` to `WorldTeam`, which reads as hostile to all comers; the original falls through
to neutral. Campaign missions are full of authored scenery near objectives, and D36 has just widened
AI target selection to sweep structures, so wrong hostility now mis-steers wingmen and the gun
assist rather than sitting harmlessly in a list.

⚠ **This item now has a reported symptom, and it is not a building.** A complete flown C3/M01 reports
the Medusa Kestrels attacking `cargozep1`, their own side's zeppelin. That is this fall-through:
`ZeppelinRuntime.cs:316` registers every zeppelin damage zone into the world `DestructibleRegistry`,
`FlightController.SelectRankedTarget` sweeps those structures (`:2788`, `:2852`), and team 100
differs from every real team and is non-zero, so it passes `Hostile` for a Kestrel exactly as it does
for the player. The Kestrels' own teams are correct end to end. C3/M01's `zeppelins.zrd` authors no
`team` on either record, so both fall through to neutral in the original and nobody shoots them by
team. Run `D32` then `F54` then this item, so the authored biases and the authored teams are both
live when the fall-through flips and the blast radius is visible.

**Approach.** Give the fall-through the neutral value and check the callers that assumed hostility.

**Model recommendation.** medium.

**Verify.** The `targeting-candidates` and `target-pool` suites, plus a check that a same-team and a
neutral structure are both refused where the data says they should be.

**⚠ Traps.** ⚠ D36's landed rule stands: a large nearby structure can outrank a distant fighter
because the ranking is minimised, so a change here must not be judged by "the AI stopped shooting
buildings" alone.

**Landed.** `AimCandidateSet.AddStructures` falls a pool with no authored team through to
`AimAssist.NeutralTeam` instead of `WorldTeam`, and its `team` parameter is gone, since every caller
took the default and a second fall-through would only hide the first. Neutral is symmetric and total
in `AimAssist.Hostile`, so an unauthored structure is now neither a target nor a shooter for anyone:
the gun assist's structure list and `FlightController.SelectRankedTarget`'s structure pool are the
only two readers, and both ask that one predicate. The `WorldTeam` constant stays at 100 with one
reader left, `ZeppelinRuntime.CollectTargetParts`, where it is the player SELECTION cycle's own
fall-through and keeps an unauthored zeppelin's parts on the Enemy cycle rather than moving them to
the Ally one. That divergence is deliberate and is recorded on the constant: the selection of
zeppelin sub-parts is a remake addition with no counterpart in the decode, while hostility is a port.
The precondition `BL-407` set was checked rather than assumed: the ownership field the original reads
(`FUN_004a32f0`, the two-bit slot in node `+0x28`) has no authored writer anywhere, both writers in
`crimson.exe` being runtime `OR` instructions at `0x0048490c` and `0x004807ef`, so every shipped
destructible resolves neutral in the original too and there is no data to read first.
`docs/org/targeting.md`'s "What this means for CSVM" row is rewritten to say so.

**Verified.** The `targeting-candidates` suite gains the arm the change is about, that a structure
whose pool authors no team is refused by a real-team AI, and keeps the same-team and hostile arms
either side of it, now on authored ids rather than on the fall-through. Perturbation, run
separately: restoring the hostile fall-through in `AddStructures` alone fails that new arm, so it can
fail and it measures this change. ⚠ Read the perturbation run honestly: the same-team arm failed
alongside it, but as a knock-on, the gunner having latched the structure in the arm before it rather
than as an independent control. `campaign-zeppelin-wakeup`'s gate census, which `F54` deliberately
pinned to the old fall-through, now asserts the neutral one; `F54`'s authored-team half is untouched
and still passes. D36's ranking trap is respected: nothing here is judged by an AI ceasing to shoot
buildings, and the gate is asserted directly on the candidate's team instead. `dotnet build` clean,
`dotnet test` 2363, `--run-tests=target` 6/6, `--run-tests=campaign` 11/11.

**⚠ Consequence to judge at the controls, not disproved here.** The gun aim assist is now silent
over every unauthored world object, including C3/M01's two zeppelins and Instant Action's, because
the original is silent there too. Damage is unaffected, since a round's damage path never asks about
teams (`docs/verification.md` SRC-6), and the player can still SELECT a zeppelin sub-part. Whether
the missing assist reads as a loss at the controls is a fidelity question only the user can settle.

# Wave E — answers and housekeeping

## E41 ☑ Decode the original's per-pylon ordnance id (`BL-462`)

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

**Landed.** The per-pylon ordnance id is the Ammo Selection screen's twelve-row rocket table index,
mapped to a weapon by `FUN_004440f0`: ids 0 to 4 give `wep_05` to `wep_09`, then 5 to `wep_15`, 6 to
`wep_13`, 7 to `wep_12`, 8 to `wep_10`, 9 to `wep_11`, 10 to `wep_14`, and 11 or anything else leaves
the pylon empty, which closes the vocabulary at twelve values. Ids 0 to 4 run in step with their
weapon numbers and the rest do not, so the mapping is read off the switch rather than derived from an
offset. `FUN_00443d70` settles the neighbouring groups at the same time, upgrading what was a
cross-record observation: gun-slot ids 0 to 4 are 30, 40, 50, 60 and 70 calibre, the ammunition index
is added, and a slot resolves to `wep_{caliber + ammo}`. The item stops at the answer as its scope
line required; `CampaignProfileStore` and `CampaignLoadout` are untouched, and CSVM's one-based
`PylonOrdnance` index is recorded as a deliberate stand-in, since reconciling the two vocabularies
would migrate every existing profile.

**Verified.** Two independent readers of the field agree, which is what makes this a decode rather
than a plausible reading: `FUN_00443de0`, the mission-start applier, walks the eight cells from
record `+0xa8`, passes each through `FUN_004440f0`, formats the result `wep_%02d` and looks it up by
exact name in the ZWEP catalog (`FUN_004bad90` into `FUN_005abfd0`), while the Ammo Selection
callback for `uiData` 2031 at `0x00409aec` reads the same cell and adds `0xd43`, the
`IDS_ROCKETSHORTNAME` base. Read against a real record (`Zachary\Status.dat`, `UIData` at `0x5b4`,
plane array at `+0x44c`): the shipped profile uses 1, 2, 5, 10 and 11, with 11 on exactly the cells
past each wing's hardpoint count at `+0x34`/`+0x38` and never inside it, and the eight-pylon planes
carrying no 11 at all. The torpedo respects the table's own availability gate, offered from mission
20 on a profile with 20 missions completed. Two planes carry genuinely mixed pylon loads, which
`docs/formats/loadouts.md` previously had only from the design document. The mapping, the record it
was read from and the values the shipped profile uses are on
`docs/formats/saved-games.md`, and the "ordnance id vocabulary" line is struck from that page's own
"Evidence and limits". ⚠ One asymmetry is traced but NOT confirmed in play and is recorded as such:
the wingman's pylons go through `FUN_00444300`, which formats `wep_%2d` rather than `%02d`, so ids 0
to 4 render with a leading space against an exact-match catalog lookup and would find nothing. The
original cannot be run here to see it.

## E42 ☑ Decide how the MPG cinemas would play, before any code (`BL-446`)

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

**Landed.** ⚠ **The trap did not fire: the engine will not take the files as they ship, so a
conversion step is required after all.** The decision is to transcode at extract time to `.ogv` and
play through a stock `VideoStreamPlayer`, because that uses a decoder the engine maintains, the
playback surface is identical either way, and the source is 320x240 and already lossy. The
alternative is recorded beside it because the choice is reversible: a C# `VideoStreamPlayback`
subclass decoding MPEG-1 at runtime needs no engine build and no GDExtension, and it wins if keeping
a third-party media binary out of the extraction step matters more than owning a decoder. No player
code and no pipeline were written. `BL-446` stays open as the feature it is, carrying the decision.

**Verified.** The ten shipped files are uniformly MPEG-1 system streams (pack marker `0010`, never
MPEG-2's `01`), MPEG-1 video 320x240 square-pixel at 856 to 1500 kbps with no sequence extension
anywhere, and MPEG-1 audio layer II at 44.1 kHz. ⚠ Two break the otherwise uniform profile and a
reader must not assume one: `msopen1.mpg` is 29.97 fps at 1500 kbps, and `crimflag.mpg` is mono. No
`ffprobe` or `mediainfo` exists in this environment, so the pack, sequence and audio frame headers
were parsed directly. Godot 4.7 compiles in exactly one video decoder: `VideoStreamTheora` is the
only `VideoStream` subclass in the editor binary, `VideoStream.xml` says the file should be Ogg
Theora with the `.ogv` extension, and the single `.mpg` string in the binary is the Android
exporter's already-compressed extension list. The facts, the decision and its limits are on
`docs/formats/cinemas.md`. Three findings for whoever builds the player: `fmv.zrd`'s `PLAYAVI`
actions name `MSopen1.mpg`, `zipper.mpg` and `Chap0.mpg` in a case the on-disk names do not have, so
a case-sensitive lookup fails on all three; the chapter cinemas come from a `char[9]` array at
`0x0061e68c` naming `chap1.mpg` through `chap6.mpg`, and `chap6.mpg` has no file in the install.
⚠ Nobody has judged a transcode at the controls, which is a presentation call rather than a
technical one.

## E43 ☑ Close what is already done, fix what is merely stale (`BL-243`, `BL-427`, `BL-426`)

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

**Landed.** `BL-243` and `BL-427` are closed and `BL-426` cites a line that exists
(`Session/InstantActionDirector.cs:761-764`, where the unguarded `RecordIfBest` actually lives, not
the `GameSession.cs:3083` it named). The one claim the item said to check before closing `BL-427` is
checked and holds: `extracted/rof/ui_strings.json`'s 3370 block really does carry the ammunition
description prose the entry's deliverable named, and its four rows read as the slug, dum-dum,
armour-piercing and explosive descriptions with 3374 as "No Information Available", which is what
`CampaignAmmoPage.GroupDescription` draws today. `BL-426`'s bug itself is untouched, since the
campaign plan's scope line puts it out of scope.

⚠ **`BL-243`'s two untested questions are carried here rather than dropped with the entry**, as the
trap required: whether the persist log commits at damage time or at mission completion, and whether
an Instant Action session loaded after a campaign mission in the same process picks the log up, since
`InstantActionDirector` has no apply call. Both are open behaviour questions about a mechanism that
is otherwise built and covered by `campaign-persistence`.

# Wave F — the second at-the-controls pass, once the mission could be finished

Wave A's purpose was that the first campaign mission could not be flown to its end. It can now, and
this wave is what a complete flown run of it reported next. Four of its nine items were traced
before being written down, and the traces changed three of the reports materially: the two cutscenes
that "do not play" both start correctly and end one frame later, the letterbox "flicker" is a
zero-margin geometry fit rather than anything temporal, and the Kestrels attacking their own
zeppelin is a cause an existing item (`D33`) already owns. Two reports produced disproofs that land
no code and are recorded as such (F52's net question, F51's stale-volume hypothesis).

**Sequencing.** F51 and F52 are the same subsystem and the same file pair (`AnimRuntime`,
`WorldSession`, `CutsceneController`), so run them as one piece of work or in sequence, never as
parallel worktrees; F51 first, because F52's framing cannot be judged until an episode lasts longer
than a frame. F53 supersedes part of A1 and touches the targeting subsystem, which `D32` and `D33`
also reach; check for contention. F54, `D32` and `D33` run in the order `D32` then F54 then `D33`,
for the reason F54 gives. F55 is blocked on F56. F57, F58 and F59 are independent.

## F51 ☑ A mid-mission cutscene ends one frame after it starts (`BL-470`)

**Goal.** The Jack drop and the PANDORA hookup play as cutscenes instead of completing instantly.

**Evidence (confidence: traced-to-code).** Both reported as "not playing"; both in fact start.
The flown session is on disk (`.scratch/logs/game-20260825-111629.out`): `landings: 8 approach
trigger(s) armed`, then `cutscene: 'texdrop' has the session` / `landings: 'do_approach6' flown` /
`cutscene: 'texdrop' its definition ended at t=137,4` in the same frame, and the same shape for
`hooked_to_klondike` at t=627. So C21's trigger, its arming chain, its speed band and its attitude
gate all work in a real flown session. `ObjectMotionSiScript` reports the motion's length as the
event's duration and that is what holds the sequence alive (`AnimRuntime.cs:1949-1953`), but
`PoseChannel.HandleMotionSiScript` sets `duration` only inside its per-target loop
(`Anim/PoseChannel.cs:317`), so an unresolved target yields zero. `camera1` is a bare `Node3D`
(`WorldSession.cs:405`) with no gamez index, while the letterbox six lines later goes through the
scene builder and has one; every compiled cutscene names `camera1` at index 3, so
`NameResolver.SymbolClaims` returns true with a null binding and `AnimRuntime.Targets` refuses to
name-match around a claimed-but-unbuilt index (`:2949-2967`). `generic_intro` survives only because
its own anim root IS `camera1` and the anchor-scoped rescue at `:2962` hits.

**Approach.** Bind the synthetic `camera1` to its gamez node index the way the letterbox already is.
Two hygiene defects found beside it belong in the same change: the re-trigger storm (`Restore` clears
`Playing` while the player is still inside the volume and the row is still armed, so the hookup
re-fired eight times in 0.14 s) and `CutsceneController._codes` never being cleared in `Restore`
(`:325-356`), so `Codes` accumulates across a session.

**Model recommendation.** high. It is a name-resolution guard with an explicit reason to exist, and
the wrong fix disables that guard.

**Verify.** The two cutscenes flown at the controls, plus the `landings-approach-trigger` suite
gaining an assertion that the episode outlives one frame.

**⚠ Traps.** ⚠ Do not relax `AnimRuntime.cs:2954-2966`: refusing to name-match around a claimed
index is what stops C1's `caboose` driving an unrelated `caboose.flt`. Fix the binding, not the
guard. ⚠ The same definitions name `player` (ptr 8918), and `CutsceneController.cs:427` already
records that the gamez `player` is not the airframe this engine flies, so fixing `camera1` alone may
play the camera move over an unstaged aircraft. ⚠ The re-trigger policy must land here or the hookup
plays eight times in a row the moment the episode stops being instant. ⚠ **C21 shipped green while
blind to this**: its suite asserts `AnimStateOf == 3` and objective completion, which an instantly
completing definition satisfies. An unchanged suite result is not evidence for this item.

**Landed.** The synthetic `camera1` carries the gamez node's name and flat-index metadata, so a
compiled cutscene's symbol table binds it and `AnimRuntime.Targets` stops dropping every event that
names it. `camera1` is flat-list index 3 in every chapter's `nodes.json`, and both `player-texdrop`
and `camera1-generic_intro` bind `"ptr": 3`. The guard at `AnimRuntime.cs:2954-2966` is untouched.
Two hygiene defects landed with it: an approach row fires once per entry into its volume, since the
handoff leaves the aircraft parked where the cutscene put it, and the host's code record covers one
episode rather than accumulating. ⚠ **A latent crash had to be fixed to get there**, and it was
unreachable until a cutscene survived its first frame: `AnimRuntime.Advance` indexed the live
instance list while an instance's own `STOP_ANIMATION` removes OTHER instances, so
`hooked_to_klondike` threw `ArgumentOutOfRangeException` on the first real run. The walk now runs
over a per-frame snapshot; the old `RemoveAt(i)` after a shift could also retire the wrong instance.

**The `player` trap is answered and needs no code.** Fixing `camera1` alone neither plays the camera
move over an unstaged aircraft nor poses an unrelated node: `player` binds to ptr 8918 and C3's gamez
has 5408 nodes with none named `player`, so `SymbolClaims` claims the name with a null binding and
those events drop exactly as before. The camera move plays; what it looks at is not there.

**Verified.** ⚠ The suite was blind for TWO reasons: it built a world with no `camera1` in it at all,
and it read only end state. It now builds the world the way a story-mission session does and asserts
the episode's duration. C3/M01's drop runs 4.43 s past the frame it starts on, against 0 s with the
index stamp removed, while every pre-existing assertion passes either way, which is precisely how
this shipped green. Thirty frames after the handoff the row is still armed with the aircraft still
inside it and does not re-fire; with the latch disabled the same check reports it playing again,
reproducing the flown storm of eight episodes in 0.14 s. Consecutive episodes report 3 then 2 codes
where the flown log accumulated 4, 11, 17, 23 and on to 59.

## F52 ☑ The cutscene node reparent is unimplemented, so the intro frames nothing (`BL-471`)

**Goal.** The intro cutscene shows the PANDORA and the aircraft it is composed around.

**Evidence (confidence: traced-to-code).** `camera1-generic_intro.json`'s `start_script` opens by
deleting `camera1` and `player` from `world1` and adding both as children of `piratezep`, and its
`reset_state` undoes exactly that, so the intro is composed in the airship's node frame. CSVM
handles `ObjectAddChild` only in its sound-emitter form (`AnimRuntime.cs:2374-2385`) and does not
handle `ObjectDeleteChild` at all (`:47-55`); C3 ships 9 of them. The scene scripts write local
transforms (`Anim/MotionRuntime.cs:394`), so `gi_cam1`'s first keyframe of `(-155.7, -62.0, -768.9)`
lands in world space: the camera flies at y = -62 m, under the sea, 1.5 km from the airship at
`(-1401, 500, -1412)`. A y of -62 is only sensible as an offset inside a node at y = 500. The same
gap takes out the aircraft, whose animations pose gamez roots the intro also expects reparented.

⚠ **Disproved, and not to be chased: "it is not following a net during the cutscene" is authored
behaviour.** Callback 20 sets `HoldsWorld` and `GameSession.cs:3052` returns before the zeppelin sim
step, which matches the decode's note that the original's per-frame world update returns immediately
while the cutscene slot is set. What "following" means in this data is the camera being a CHILD of
the airship, which is the defect above.

**Approach.** Implement the node-reparent form of both opcodes in `AnimRuntime.Dispatch`, moving the
child WITHOUT preserving its global transform, so the authored keyframes land in the airship's frame
unchanged.

**Model recommendation.** high. It adds a new capability to the anim runtime and reparenting a
camera and a player template mid-session reaches several subsystems that assume stable parentage.

**Verify.** A `--campaign=` screenshot early in the intro showing the airship in frame. ⚠ Not a
freecam regression: `docs/verification.md` DIAG-10 says an 8-chapter sweep is inert for
`generic_intro`.

**⚠ Traps.** ⚠ The sound-emitter case must still win; test it first and fall through. ⚠ A
`keepGlobalTransform: true` reparent is exactly wrong and would change nothing visible. ⚠
`CutsceneController.Restore` parks `camera1` at identity (`:340-346`), which after a reparent is
relative to `piratezep`; Restore must put it back under the world root first, which the definition's
own `reset_state` already asks for. ⚠ `HideUnplacedEntities`/`RestorePlacedEntities` and the `world1`
walk assume stable parentage.

**Landed.** Both opcodes take their node-reparent form in `Dispatch` once the sound-emitter form has
declined it, moving the child with its LOCAL transform kept so a definition's authored keyframes are
read in the frame of the node the shot is about. A delete detaches to the world root, and only when
the child is actually under the named parent, because the intro's `RESET_STATE` names `piratezep` for
nodes that were never there. ⚠ The move is a detach and attach rather than `Node.Reparent`, which
REFUSES when the node is not inside the tree: an intro composes itself during the animation
bootstrap, before the world root is added to the scene, which is why the first attempt was a silent
no-op for that reason rather than for a resolution failure. Reparents are dispatched for sequence
events only, since applying the undo during a bootstrap `RESET_STATE` walk would move shipped nodes
off a parent nothing has changed. `CutsceneController` takes `_cameraHome` from the runtime's world
root rather than from the camera's current parent, because `BindWorld` runs after the bootstrap by
which time the intro has already moved it.

**Verified.** A `--campaign=` screenshot early in C3/M01's `generic_intro` shows the airship
centre-frame with the camera closing on it, where the same shot before the change is bars plus empty
sky. A shot after the handoff is ordinary gameplay, chrome back and camera on the aircraft, with no
stray errors. The runtime's unhandled-event census drops from `ObjectDeleteChild` 8 and
`ObjectAddChild` 4 to 7 and 3; the one add that applies is `camera1`, and the rest are the
unresolvable `player` and the `RESET_STATE` walks. ⚠ **The intro is still missing its aircraft**, for
the reason F51 records: the gamez `player` those definitions animate has no node in C3's gamez at
all. The camera is now where it belongs, looking at a stage still missing its actors.

## F53 ☑ Objective sites belong in the enemy selection cycle (`BL-472`)

**Goal.** One objective marker at a time, selected with d-pad up the way an enemy is, and a marker
on a moving node that tracks it.

**Evidence (confidence: traced-to-code).** The report restates the decode. `TargetRef.Classify`
(`Flight/TargetRef.cs:183-194`, from `FUN_004b5cd0`) classifies an objective site as `Enemy` above
the vehicle restriction and `SortsFirst` (`:120-121`) puts it at the head of that cycle; the HUD
draws exactly one thing, `TargetHud.Selected` (`Flight/TargetHud.cs:112`). D-pad up already reaches
`NextEnemy()` (`Flight/FlightController.cs:2469-2477`). A1's overlay instead draws the whole set
(`UI/ObjectiveMarkerHud.cs:289`) and touches neither `TargetPool` nor `TargetSelection`. Counted
rather than eyeballed: `Complete Mission M02.mkv` sampled at 2 fps gives 255 frames containing
marker blue, every one with exactly one cluster and none with two, present on the nav and return legs
and absent through the combat leg. The frozen dock marker is the same overlay: `pzhookpoint` is a
node eleven levels under `piratezep` and `:222` resolves it to a `Vector3` once inside `Rebuild()`,
which runs only on a target-set change, so any moving objective node freezes.

**Approach.** Retire the overlay and register each live site as an `AimCandidate` in the player's
`TargetPool` with `objectiveTarget: true`. A pool candidate is rebuilt from its live source every
frame, so this fixes the tracking without a second mechanism.

**Model recommendation.** high. It reverses part of a landed item and moves work into the targeting
subsystem, whose selection rules are decoded and easy to break.

**Verify.** At the controls: one marker, cycled with d-pad up, and the dock marker staying on the
airship as it moves. The `campaign-objective-markers` suite rewritten, since it asserts the
all-at-once set and an unchanged pass is not evidence.

**⚠ Traps.** ⚠ `TargetPool.Offer` hard-codes `objectiveTarget` absent (`TargetPool.cs:183-189`)
behind a comment now stale, since `MissionTargets.Objective`/`OtherTarget` exist; plumb the flag,
do not fake it through the `Structure` branch, which forces `otherTarget` and lands the site on the
NonAircraft cycle under `U` rather than the Enemy cycle under d-pad up. ⚠ Selection stickiness is
`ReferenceEquals(Source, ...)` (`TargetRef.cs:219`) and the overlay uses the node NAME STRING as
`Source` (`:125`), so a fresh string per rebuild drops the selection every frame; use the resolved
`Node3D`. ⚠ `FlightRoster.SetTargetSubParts` is single-assignment and already taken by the zeppelin
runtime; compose, do not overwrite. ⚠ Objectives sorting first means they head the cycle during
combat too, which matches the decode; do not add a range or FOV gate. ⚠ Keep `PointFor`'s precedence
for bare-point sites: C3/M01's village node sits at the world origin, 7.9 km from the point its own
objective tests. ⚠ Lead-only and not to be changed on this evidence: the original's off-screen block
may end in a distance where ours writes `"N o'clock"`.

**Landed.** Each live site is offered to the player's `TargetPool` as an `AimCandidate` carrying the
mission's own `objectiveTarget` flag, so `TargetRef.Classify` files it on the Enemy cycle,
`SortsFirst` puts it at the head, d-pad up steps between the sites, and `TargetHud` draws the ONE
selection with the original's category line over the site's name. A1's overlay is retired, 333 lines
deleted, and its two decodes are kept in the new `Session/ObjectiveSites.cs`: the starting-`objective`
flag set, since C3/M01 only ever REMOVES its sites, and the bare-`TRAVELERS`-point precedence.
`MarkerDraw` and `EdgeMarker` are untouched and still shared with the stunt marker. **The frozen dock
marker falls out of the same change**: every candidate is rebuilt from its live source each frame, and
`Collect` returns the SAME `ObjectiveSite` instance per node for as long as the mission flags it,
which is what `ReferenceEquals(Source, ...)` stickiness needs. The old overlay used the node NAME
STRING as its source, so a fresh string per rebuild would have dropped the selection every frame.
Two seams stayed separate deliberately: the objective feed is its own roster channel, leaving
`SetTargetSubParts`'s single assignment to the zeppelin runtime, and `TargetPool.Offer` plumbs the
objective flag rather than standing it in through `otherTarget`.

**Verified.** The rewritten `campaign-objective-markers` drives C3/M01 against its built world
through a real `TargetSelection`: three sites on the Enemy cycle, the selected one carrying
`objective=True class=Enemy` in marker blue, next-enemy stepping `t_chamber` to `grasshut2`, the
bare-point site marked at its point rather than at its node 7.9 km away, a site tracked through a
scripted 600 m node move, and flying an approach retiring that site and leaving the rest. 11 checks,
and it fails on the right check under each of THREE deliberate perturbations: position frozen at
first resolve, a fresh site instance per rebuild, and the objective flag faked through `otherTarget`.
A live `--campaign` run logs `campaign: 3 objective site(s) on the player's target cycle` and
`target pool: enemy=3 ally=1 nonAircraft=103 class=Enemy acquired=t_chamber` and draws exactly one
blue marker; the same run on the `otherTarget` build logged `enemy=0 nonAircraft=106`, three sites
moving cycle for cycle. ⚠ Not verified: d-pad cycling at the controls, since the lane had no pad,
and the dock marker riding the moving PANDORA in a live session, since `pzhookpoint` is only ADDed
after the wreck objective, so the mechanism is pinned headlessly by the moving-node check instead.

## F54 ☑ A zeppelin's authored team is unread and its wake-up has no seam (`BL-476`)

**Goal.** A mission's hidden zeppelin stays hidden until the script reveals it, and a zeppelin that
authors a team carries it.

**Evidence (confidence: traced-to-code).** Found tracing the report that C3/M01's Medusa Kestrels
attack `cargozep1`, their own side's. ⚠ **That symptom's cause is `D33`'s, not this item's**:
`AimAssist.AddStructures` (`AimAssist.cs:495`) defaults every structure's team to `WorldTeam` (100),
the zeppelin reaches the AI pool as a structure through `ZeppelinRuntime.cs:316` and
`FlightController.cs:2788`, and team 100 differs from every real team and is non-zero, so it passes
the hostility gate for a Kestrel exactly as it does for the player. The Kestrels' own teams are
correct end to end (`aiv.zrd` slot 3 gives `player`/`wingman_1` team 1 and `medkestrel_*` team 2).
What this item owns is adjacent and is what makes `D33` safe to land: `Zeppelins.cs:130-133` decodes
an authored team into `ZeppelinDef.Team`/`TeamId` (`:313`, `:317`), authored on 16 of 58 records
install-wide and read by nothing, and `CampaignDirector.WakeupEnemies` (`:636-659`) handles only
roster aircraft while C3/M01's `OBJECTIVE39` is `WAKEUP_ENEMIES ["cargozep1"]`. `cargozep1` ships
`deactivated: 1` and `ZeppelinRuntime.cs:231-237` honours that only by holding its MOTION, so the
zeppelin the mission reveals partway through is drawn, collidable and a damage pool from t=0.

**Approach.** Read `ZeppelinDef.Team` onto the placed zeppelin and give `WakeupEnemies` a zeppelin
seam. Run `D32` first, this second, `D33` third, so that when the structure fall-through flips to
neutral the authored teams and the authored biases are both live and the blast radius is visible.

**Model recommendation.** medium.

**Verify.** The hidden zeppelin absent until its wake-up, and an authored-team zeppelin carrying it.

**⚠ Traps.** ⚠ Do not set a zeppelin's team to a literal in code: 42 of 58 records author none, and
what an unauthored one falls through to is `D33`'s question, not this one's. ⚠ `D32` is implicated
and should be cross-referenced rather than absorbed: C3/M01's Kestrels author `rating_biases
[["player", 0.5], ["piratezep", -1.0]]`, which is the original's own explicit instruction not to
target the friendly zeppelin, and it is dead in our build because spawn node names never match.

**Landed.** `ZeppelinRuntime.AuthoredTeam` reads a record's team into the one shared team space
(`neutral` 0, `ally` 1, `enemy` 2, a bare integer verbatim) and returns null for a record authoring
none, so no literal is invented; `WireZones` fans that one value onto every zone pool the way
`FUN_004bee80` fans it across the airship, and `DestructibleRegistry.Instance.Team` carries it to
`AimAssist.AddStructures`, which prefers it over the world fall-through. ⚠ That fall-through is
deliberately still `WorldTeam`, since it is `D33`'s question, and the new suite asserts an unauthored
pool still falls through to 100. A `deactivated` record now starts DORMANT rather than merely
motionless, and `CampaignDirector.WakeupEnemies` tries the roster and then the zeppelin, so
`WAKEUP_ENEMIES` is one directive over both deactivated flags. The dormant pose is opacity 0 rather
than `Visible = false` for two measured reasons: `RestorePlacedEntities` would undo a visibility
write a second later (the baseline run logs the poll restoring both airships), while opacity is a
shader parameter that poll knows nothing about, so A2's placement is not fought and `WorldBuilder`
needed no change; and the mission's own reveal IS an opacity fade, `cargozep1-fadein_cg1zep.json`
being a single `ObjectOpacityFromTo` from 0.0 to 1.0 over 6 s, so alpha 0 is the fade's own start
point rather than a guess, and `PoseChannel`'s existing rule drops the colliders with it.

**Verified.** `campaign-zeppelin-wakeup` over C3's built world reads the three parser names off
shipped records (C1/MP3 `ally` to 1, C5/M03 three `enemy` to 2, C3/M01 both unauthored), confirms an
authored pool team beats the fall-through while an unauthored one keeps 100, confirms a dormant pool
is refused outright, then drives the real `ObjectiveGraph.Wake(39)` through the real
`CampaignDirector`: target parts 18 to 37, structure candidates 230 to 249, hull collidable 0.07 s
into the 6 s reveal. A real C3/M01 run logs `team unauthored — deactivated, out of the world until
woken` and `anim: fade dropped colliders under 'cargozep1'` while the unplaced-entity poll restores
the node without revealing it; a real C5/M03 run logs `team 2 on 19 pool(s)` per authored record and
`no authored team` for `beowulfzep`. `dotnet test` 2342 in the lane and 2347 on the merged tree, all
8 chapters `--freecam` error-free, and all 16 golden hashes re-run by hand and unchanged. ⚠ Could not
be verified visually: an A/B screenshot at `cargozep1` was inconclusive, since at 500 m the fog wall
hides it in both builds and closer poses land inside the intro's letterbox, so the collider-drop log
line and the suite's collider walk are the evidence instead.

**Two remainders, carried on `BL-476` rather than dropped.** The team is NOT fanned onto the
zeppelin's turrets although the decode says it should be (`FUN_004bee80` writes `+0x8` on every
child): `TurretController.Team` is read-only and `docs/org/targeting.md` records that the original
drops a now-friendly lock when the team changes, so a setter needs that too, and it bites only an
`ally` zeppelin's own guns. And the `["piratezep", -1.0]` half of the authored bias is still dead
after `D32`, because a zeppelin's destructible instances are its ZONES and `TargetPool.NameOf`
returns the zone's anchor name, so the ranking needs the zone's owning zeppelin identity, which is
the same identity this item gives the zeppelin for its team.

## F55 ☐ The wingman's formation is looser than the original's (`BL-473`)

**Goal.** The wingman flies the original's formation distance.

**Evidence (confidence: lead-only).** Judged at the controls once A3's ceiling lift had landed: the
wingman is near the player throughout and never overhead, so A3's failure is gone, but the formation
is "not as close as original". A fidelity gap on a working mechanism, not a repeat of A3.

**Approach.** Nothing until the gap is measured against something. The decoded station offsets are
confirmed and the commanded point equals the decoded station to 0.00 m, so the divergence is in how
closely the escort TRACKS its point, not in where the point is.

**Unblocked: F56 built the instrument.** `wingman-station`'s flown leg now prints a `settled` figure,
the last-30 s mean separation on a leader flown like a player: **199 m on `player_pfighter`** and
413 m on `player_bhawk`. Quote the campaign airframe's 199 m for a campaign statement, since the
Bloodhawk's 413 m is a stern-chase artefact of a leg where leader and wingman share a top speed at
full throttle. Because the commanded point is already 0.00 m from the decoded station, any gap the
user sees IS this number, and the work is to move it toward the original's.

**Model recommendation.** medium.

**Verify.** The user's eye, since the reference is theirs.

**⚠ Traps.** ⚠ Do not re-tune the decoded station offsets or the 700 m join gate to close a visual
gap; that trades a confirmed decode for an impression. ⚠ Footage-derived separation distances are
inadmissible (`docs/verification.md`).

## F56 ☑ `wingman-station` is red under main's flight plant (`BL-474`)

**Goal.** The suite is green, or its gates are re-justified in writing against the plant that now
exists.

**Evidence (confidence: traced).** Same suite file byte for byte, same seed, `player_pfighter`: this
branch's plant reads a 296 m mean separation unfixed and 256 m with the `StationCeiling` lift; main's
reads 1096 m and 415 m. Three gates fail. The lift does MORE work under main's plant, cutting
far-field time from 26.8 % of steps to 3.0 %, which settles A3's open question toward keeping it.
Nitro is ruled out by a runtime probe (`installed=False` on both rigs). ⚠ The suite's `worst` column
is untrustworthy and must not be quoted: a sampled trace never exceeds 470 m. Trust the mean.

**Approach.** Decide which this is: a regression in main's flight model whose owner should be told,
or gates calibrated against a plant that no longer exists. ⚠ The contrast the answer has to explain
is that at the controls the wingman is reported near the player throughout (F55), so either the
scripted stick is more aggressive than a human's or the gates measure something a flown session
never reaches.

**Model recommendation.** high. It is a cross-branch regression question on a shared control law.

**Verify.** The suite green on the merged tree, or the gates changed with the reasoning recorded.

**⚠ Traps.** ⚠ Do not widen the gates until they pass: they encode the decoded 700 m join leash.

**Landed.** ⚠ **Main's flight model did not regress station-keeping. The gates were calibrated
against a plant that no longer exists, and the largest failing numbers were never separation at
all.** `7c660df6` re-declared `BodyRates` as quaternion half-angle rad/s and integrated with
`Rotated(axis, 2f * omega * dt)`, doubling every angular rate; nothing in `AiEscort`, `AiPilot` or
`AiControlLaw` moved, and `AiControlLaw` is near bang-bang with no rate feedback, so it has no gain
to be de-tuned. The suite's leader profile was authored in deflection-seconds, so its 1.5 s of 0.6
roll, written to reach about 47°, now reaches 95°, and the pull behind it digs a descent. The leader
crosses y = 0 at t = 57.6 s and again at 115.6 s, where `FlightController.UnderMapY` teleports it to
its 1200 m spawn **setting no crash flag and writing no log line**, so the per-step `Crashed` check
never fired. Those two jumps are the reported 3797 m worst and most of the 415 m mean, on a leg that
plateaued at 254 m and never read above 470 m. The profile's holds are now named constants documented
as ATTITUDES; a floor check fails the leg when an aircraft drops through the backstop; the undecoded
400 m mean gate is replaced by a settled-window test bounded by the DECODED 700 m leash, which was
not widened; and the scripted-leg A/B reads the commanded offset rather than the flown one, because
the two legs enter formation by different routes and their weaves buried the decoded 26 m difference.
No engine code changed and no goldens are affected.

**Verified.** Run both ways. Reverting the `2f` factor alone makes the UNMODIFIED suite green and
reproduces this branch's table to the metre (`player_pfighter` 256 m / 590 m, `player_bhawk`
328 m / 584 m), so the plant change is the whole cause. With the profile flying the attitudes it
names, `player_pfighter` reads 270 m mean, 199 m settled, 523 m worst and 1075 m lowest altitude
against 256 m / 590 m before the merge, so station-keeping is unchanged across plants. The instrument
still bites when it should: with A3's lift removed the campaign airframe fails at 428 m settled
against a 409 m hold mean and the Bloodhawk outright at 2070 m. `dotnet test` 2342, the full battery
114 passed 0 failed with errors clean. `docs/verification.md` gains INSTR-22 (the silent under-map
teleport entering a distance statistic) and INSTR-23 (a deflection-second stick measuring the plant
rather than the pilot).

**The crux, answered.** The scripted stick was more aggressive than any human's, by a mechanism
nobody chose: a 95° knife-edge bank held into a pull is not an input a player produces. The gates
then measured something no flown session reaches, because the leader flew into the ground and the
world teleported it home. The user's report of a wingman near them throughout is consistent with the
code rather than in tension with it.

⚠ **One number in A3's Verified prose no longer reproduces and must not be re-quoted:** with the
re-authored profile the far-field plant is entered on 0 of 7199 steps on BOTH airframes, against
A3's recorded 54.3 %. The plant is still ruled out at the image, so no conclusion changes, but the
"far-field 26.8 % to 3.0 %" contrast belongs to the old profile.

## F57 ☑ The targeting readout drops the militia name (`BL-475`)

**Goal.** A campaign AI reads as "Medusa Kestrel", the name the original shows.

**Evidence (confidence: traced-to-code).** The name is authored, not composed:
`extracted/zrdr/vehicle.zrd.json` def `medkestrel` carries `title: MSG_VEH_MEDUSA_KESTREL` and
`messages.json` resolves it to "Medusa Kestrel"; `medbrigand` carries the sibling key, so it is a
family. The roster's own slot-20 `title` is the PILOT tier ("Zachary", "Betty") and is empty on all
three `medkestrel_*` blocks. Ours calls `PlaneRoster.PlaneDisplayName` (`Flight/TargetHud.cs:188-189`,
`TargetPool.cs:143-144`), which strips a leading `p` off `stats.DefName` and title-cases it, behind
its own doc comment calling itself a placeholder. `DefName` is deliberately the PLAYER def while the
militia def sits beside it as `stats.AiDefName` (`PlaneStats.cs:486`), already used for damage and
livery. The lookup idiom exists at `MilitiaPaint.cs:32-34`.

**Approach.** Route `TargetRef.DisplayName` for an aircraft from `stats.AiDefName`'s `vehicle.json`
title through `Messages`, falling back to `PlaneDisplayName` where the def carries no title.

**Model recommendation.** medium, low effort.

**Verify.** A targeting capture naming a Medusa Kestrel, plus the existing targeting suites green.

**⚠ Traps.** ⚠ Do not change `TargetRef.Name`: `TargetRef.cs:66-76` states it is CSVM's identity
string for `--target=` and the breadcrumbs, and a golden pinned on "Fury" could not say which Fury it
meant. ⚠ Check the player's own defs before making the title path universal; a bare player def's
title is the aircraft alone, which is why `MilitiaPaint.cs:36-37` skips a name with no space in it.

**Landed.** The readout prints the name the def authors, so a militia AI reads "Medusa Kestrel".
`PlaneStats.LoadForAi` reads the AI chain's own `title` key (inherited from the airframe where a def
carries none), `AiFlightAssembler` resolves it through the session's string table, which is the one
place the loaded def and the string table meet, and `PlaneRoster.PlaneDisplayName` prefers that
authored name over its `p`-strip derivation, so both readers pick it up with no change of their own.
`TargetRef.Name` is untouched, so `--target=` and the breadcrumbs still match `ai1_player_kestrel`.
A player load carries no AI chain and so no title, which leaves a `--vs` opponent's marker as it was.

**Verified.** Four new unit cases pin the decode against the shipped install: `medkestrel` authors
`MSG_VEH_MEDUSA_KESTREL` resolving to "Medusa Kestrel", `pkestrel` and `kestrel` both carry
`MSG_VEH_KESTREL` so the player path is unchanged, and 51 of 75 defs carry a title while the `w*`
wingman and `r*` remote families carry none and inherit. A scripted `--ai=player_kestrel:def=medkestrel`
run labels the marker "Medusa Kestrel" where the unfixed build labels it "Kestrel", 287 pixels apart
and all of them in the label. The `c1-targeting-hud` golden reproduces its committed hash digit for
digit, because that shot flies the plain `kestrel` def whose title is the same word the old
derivation produced, and `manifest.json` is unmodified. ⚠ Not settled: whether the original prints a
militia name for a WINGMAN def, since those author no title at all and ours inherits the airframe's.

## F58 ☑ Alpha-cutout geometry is solid to weapon rays (`BL-477`)

**Goal.** An answer first: whether the original's weapon-ray test consults texture alpha. Then, if it
does, a rule that lets shots through a see-through truss without letting them through its girders.

**Evidence (confidence: traced-to-code for our behaviour and the geometry; lead-only for the
fidelity target, which is UNDECODED).** `hydrogentank1..4` sit at local z = -70 and -115 under
`cargozep1`; the `front` truss spans z = -260.0 to -48.2 and covers every tank, while the `rear`
truss starts at z = +56.8, 130 m aft of the rearmost tank. So the rear is the only open aspect, which
is the symptom exactly. The front truss is the transparent scaffolding: large cards with a truss
painted on them (`f_lo` 6 of 10 polygons alpha, `f_hi` 39 of 57), textured `cargotex1`/`cgcable1`,
both marked `alpha: "Full"` in the chapter's texture manifest while the tanks' own textures are
`alpha: "None"`. `SceneBuilder.EmitCollisionFaces` (`:623-650`) emits every polygon with no filter
into a `ConcavePolygonShape3D { BackfaceCollision = true }` (`:820-821`), and transparency is
computed on the render path only, from texture pixels (`:1186-1191`), never reaching
`CollidersForMesh`. ⚠ **The one decoded collision flag argues against the report**: `intersect_surface`
(`docs/formats/gamez.md:38`) is true on every truss and tank node across the 534-node subtree, only
false on propeller frames and burn effects. That establishes that a false node is skipped, not that a
true node's test ignores alpha, and whether the original samples alpha at the hit UV is decoded
nowhere.

**Approach.** Decode first. Find whether the weapon-ray polygon test consults the hit UV's alpha or a
bit in the same texture header word `BL-335` already located. The cheap instrument that turns this
from inference into measurement: place `cargozep1` at its authored pose and cast rays at
`hydrogentank1`'s centre from 36 azimuths at several elevations, logging the first collider's node
name.

**Model recommendation.** high. It begins undecoded and the wrong rule reaches every fence, railing
and tree card in eight chapters.

**Verify.** The decode's answer written down, and if code follows, the ray census before and after.

**⚠ Traps.** ⚠ A blanket "skip every polygon whose texture has alpha" rule is wrong: `cargotex1` is
48.8 % fully opaque and `cgcable1` 15.8 %, and those texels are real girders. ⚠ The blast radius is
the whole world and includes aircraft terrain collision, so a plane could start flying through
fences. ⚠ `hydrotank4`, the tank's own destroyed variant, is itself `alpha: "Full"`; the rule must be
stated so a destructible keeps its own hit volume. ⚠ Do not shrink or move the truss: the geometry is
the original's and the divergence is in the hit test.

**Landed.** ⚠ **The answer is "neither", and no collision rule follows.** `crimson.exe`'s weapon-ray
polygon test reads no texture data at any point: `FUN_0055c9c0` walks a model's whole polygon array
unfiltered and picks between two geometry-only testers on a MATERIAL bit, and the UV-computing one
(`FUN_0055db90`) returns a hit UNCONDITIONALLY, storing the UV only so `FUN_00558f80` can stamp a
bullet hole into the texture. The one place the whole path touches texture space is to write, after
the hit is decided. That selector bit is mech3ax's `MaterialFlags::UNKNOWN`, the `flag` field in
`materials.json`, set on 21 of C3's 483 materials and every one an aircraft or cockpit skin, so on a
zeppelin no hit UV is computed at all. `intersect_surface` is resolved in the flag's favour rather
than dodged: `FUN_004c9a00` gates on `ACTIVE` and `INTERSECT_SURFACE` and nothing else, with the bit
numbers pinned by the GameGen keyword path, so the original polygon-tests the truss.
`SceneBuilder.EmitCollisionFaces` now carries the prohibition against writing a rule.
Three further decodes came with it: `INTERSECT_BBOX` REPLACES the polygon test rather than
pre-filtering it (correcting `gamez.md`), a LOD node is descended into for exactly one child chosen
by `ACTIVE`, and the sole pass-through rule is `FUN_005ad330`'s water rescan, keyed on the soil id
and never on alpha.

**Verified.** `alpha-cutout-ray-census` places `cargozep1` at its authored pose in a built C3/M01 and
casts 180 rays at `hydrogentank1`'s mesh centre from 36 azimuths at five elevations: 5 of 180 reach
the tank, the front truss `g469` takes 26 of the 36 level azimuths, the hull's gasbag panels
everything above 30° and the terrain everything below −30°. So the reported asymmetry is real and its
occluders are named, and it is not a divergence. Ruled out with evidence: texture alpha, a `BL-335`
header bit, `intersect_bbox` (zero of the 534 nodes carry it), LOD mis-selection at gun range
(`f_hi`'s band is [0, 800) and our rule picks exactly it), backface (the original does honour
`show_backface` where our colliders are two-sided, but 33 of `g469`'s 39 cards set it), and the
projectile's `0x40000` mask (no shipped node carries bit 18). `docs/org/weaponRay.md` carries the
decode, `analysis/bl-477-weapon-ray/` the censuses and their `FINDINGS.md`, and `docs/verification.md`
gains INSTR-24, the trap that bit the first census: a raycast in the same call that moved a static
body reads the collider at its pre-move pose, reporting 108 clean misses through 140 live bodies.

**Two candidates remain for why it read differently at the controls, and both need the user's eye
rather than more decode.** The shot may have been beyond 800 m, where the original narrows to
`f_mid`/`f_lo` whose cards are ±28.3 m of local x against `f_hi`'s ±48.9 m; or the difference is in
aim assist and target selection rather than in the ray.

## F59 ☑ A net has no stop-point state, so the PANDORA never halts (`BL-478`)

**Goal.** An answer: what a net node's shape-A tag means. Then, if it is settled, a zeppelin that
stops where its mission expects it.

**Evidence (confidence: traced-to-code for the gap; lead-only for the tag reading).** The airship
moves, and the data says so three ways: `zeppelins.zrd` gives `piratezep` `net "M1PirateZep"` with
`max_speed 20.0`, `neindex.zrd` maps that to net id 2, and `ne000002.zrd` is an open 8-node path at
y = 500 running about 9 km southwest from the spawn position, which is net node 0. The mission
depends on it: `OBJECTIVE12` and `OBJECTIVE13` both complete on `COMPLETED_STOPPOINT
[["M1PirateZep", 1, 0]]`, and OBJECTIVE13 is the one that hands the player the dock target. No anim
moves it. Ours flies it (`ZeppelinRuntime.cs:45-63`, `:221-242`) and never halts it: node tags are
preserved unacted-on (`Flight/AiNetFollower.cs:13`), the condition is parsed and dispatched but
`CampaignDirector.cs:788-794` logs an explicit gap, and on an open path `AiNetFollower.Update`
(`:167-174`) reverses at the degree-1 end, so ours shuttles the route forever.

**Approach.** Settle the tag reading, then give `AiNetFollower` a stop-point state.

**Model recommendation.** high, because it starts as a decode with two live readings.

**Verify.** The reading stated with the evidence that chose it; the airship halting where
`OBJECTIVE13` expects it.

**⚠ Traps.** ⚠ `docs/formats/ai-nets.md:118-183` is explicit that the shape-A tags are structure and
not meaning, that two readings both fit every net, and that the runtime parser is not located.
Implementing "halt at tagged node" today would be inventing content, and it would move where the
mission's docking happens.

**Landed.** ⚠ **The parser the format page said was missing is located, and the reading is settled at
the image rather than by the census.** `FUN_004311c0` reaches it through the loader's vtable at
`0x006046fc` slot `+0x8`: `FUN_004304a0`, the shipped net deserialiser, which had no Ghidra function
and was created at that address. It widens each node into a 24-byte record and reads four optional
fields POSITIONALLY, each behind one element-count test: a stop-point id at `+0x0c` (0 = none), a
halt flag at `+0x10`, a danger-zone flag at `+0x11`, and a `dzpathN` index at `+0x14` (default −1).
**So "shape A" and "shape B" are the same four fields, not two subsystems.** The rival reading is
refuted: `COMPLETED_STOPPOINT` resolves the net by name, requires `id > 0`, finds the node BY ID
(`FUN_004319a0`, first match) and WRITES the flag onto that node's halt byte (`FUN_004319d0`), and a
boundary flag is not a settable byte addressed by id. Only the zeppelin follower reads it
(`FUN_004bf9d0`: hold on an armed node, full speed until 250 m along-facing then a linear ramp to
zero, park inside 30 m); the aircraft follower reads the danger-zone fields instead, which decodes
shape B as a bonus. `AiNetFollower` carries the live flags, ⚠ gated on an `observesStopPoints` flag
defaulted OFF because only the zeppelin follower reads them in the original.

⚠ **This item corrects its own Evidence paragraph above.** `COMPLETED_STOPPOINT` is not a condition:
it runs in the objective COMPLETION pass, where `ObjectiveGraph.RunCompletionActions` already had it
and where `docs/formats/objectives.md` already said it belongs. `OBJECTIVE12` and `OBJECTIVE13` do
not complete ON it; they complete by other means and then RELEASE the airship.

**Verified.** Census over all 8 chapters and 53 mission scripts: 23 `COMPLETED_STOPPOINT` clauses in
11 missions naming 14 nets, all 23 resolving to a node by id with none dangling, and 20 of 23
flipping the flag the file authored (the 3 that restate it all write 0 over 0). The flag is cleared
19 times and set 4, so releasing a docked airship is the normal case. 40 of 222 nets carry node
fields: 36 stop-point nets, all zeppelin routes and none a fighter flies, and 4 danger-zone nets,
the two sets disjoint; the 11 `dzpathN` indices they name all exist in their chapter's `dzones.zrd`.
Every zeppelin record checked spawns on its net's node 0, and C1/M04's node 0 IS stop point 1 armed,
so that PANDORA starts DOCKED and the script launches it. C3/M01's docks at node 2, is released by
`OBJECTIVE12`/`OBJECTIVE13`, then halts for good at node 7 under id 0, which no clause can address:
that is how an open path ends, and why our reversal at the degree-1 end was wrong. `zeppelin-motion`
flies C1/M04's own record through dock, release, traverse and terminal dock, and passes on the merged
tree with errors clean. Five unit tests pin the field decode, the hold and release, the first-match
id lookup, the inert aircraft case and the 250 m ramp. The three-part `CampaignDirector` and
`GameSession` handover was applied at merge, so `COMPLETED_STOPPOINT` stops logging its gap.

# Wave G — what the first six waves left open

Waves A through F closed on the two at-the-controls passes. This wave closes on the work itself:
every item here was found by an item that landed, and each one names the item that filed it. That
changes how they should be read. There is no symptom to trace back, so the Evidence paragraphs are
already traced, and the risk is the opposite of Wave A's: an item here can be built exactly as
written and still be the wrong thing to spend a wave on, because nobody has reported missing it.
G64 is the exception and should be judged first, since the boards Wave B built are what the user
sees on the way into every mission and fifteen of their backgrounds are currently blank.

**Three items carry over rather than being restated here.** A5 is traced to two causes and not
built. D33 is unblocked now that D32 and F54 have landed, and it is the fix for the Kestrels
attacking their own zeppelin. F55 has its instrument and needs the user's verdict against it.

## G61 ☐ The intro cutscene stages no aircraft (`BL-482`)

**Goal.** An answer first: what the original's `player` node is and where it comes from. Then, if the
answer supports it, an intro composed around the aircraft its definitions animate.

**Evidence (confidence: traced-to-code for the absence, lead-only for anything beyond it).** F52 put
the camera where it belongs and the airship is centre-frame, which is half of the user's "no Pandora
or planes in sight". The other half stands. `camera1-generic_intro.json` names `player` at ptr 8918,
C3's gamez carries 5408 nodes and none of them is named `player`, so `SymbolClaims` claims the name
with a null binding and every event that poses it drops (F51). `CutsceneController.cs:427` already
records that the gamez `player` is not the airframe this engine flies. So the camera move plays over
a stage with no actors on it, and the shipped data does not say what the actors are.

**Approach.** Decode before building. The question is narrow: the original resolves ptr 8918 to
something at runtime, so find the creation site that puts a node under that name into the node table,
and read what mesh it carries. Only then decide whether our flown airframe is the right stand-in or
whether the intro composes a separate template. ⚠ If the decode does not settle it, the item closes
as an answer with the intro left as it is, which is a success by this plan's ground rules.

**Model recommendation.** high. It starts undecoded and the wrong reading stages a wrong aircraft in
every chapter's intro.

**Verify.** The reading stated with the evidence that chose it. If code follows, a `--campaign=`
screenshot early in C3/M01's `generic_intro` showing the aircraft where the definition poses them,
against the same shot before the change.

**⚠ Traps.** ⚠ Do not bind `player` to the flown `FlightController` because the name matches: the
`CutsceneController` note above exists precisely because that inference was already made once and was
wrong. ⚠ Do not relax `AnimRuntime.cs:2954-2966` to make the name resolve; F51's trap on that guard
stands unchanged. ⚠ An intro composes itself during the animation bootstrap, before the world root is
in the scene, which is the condition that made F52's first attempt a silent no-op.

## G62 ☑ A zeppelin's turrets and its zones carry no owning identity (`BL-476`)

**Goal.** One owning-zeppelin identity on a zone pool, read by the turret team fan and by the
targeting bias, so an airship's guns and an authored `rating_biases` pattern both find it.

**Evidence (confidence: traced).** F54 built the authored team and the wake-up seam and left two
remainders, both the same missing identity. The team is not fanned onto the zeppelin's turrets
although `FUN_004bee80` writes `+0x8` on every child, turrets included, and `TurretController.Team`
is read-only. A `rating_biases` pattern naming a zeppelin still matches nothing, because a zeppelin's
destructible instances are its zones (`gasbag1..6`, `leng11`, `lbroad11`) and `TargetPool.NameOf`
(`TargetPool.cs:117`) returns the zone's anchor name, so C3/M01's Kestrels authoring
`[["piratezep", -1.0]]` get no match. D32 made the `player` half of that same pattern live and its fix
cannot reach this one.

**Approach.** Carry the owning zeppelin's identity alongside the zone in the pool, then read it in
both places. Run it behind D33, so the hostility fall-through is already neutral and the bias change
is judged on its own.

**Model recommendation.** medium.

**Verify.** The `target-pool` and `targeting-candidates` suites, plus a check that an `ally`
zeppelin's turrets carry their airship's team and that a `-1.0` bias on a zeppelin name reaches its
zones.

**⚠ Traps.** ⚠ `TurretController.Team` is read-only for a reason: `docs/org/targeting.md:230` records
that the original drops a now-friendly lock when a team changes (`0x004acb90`), so a setter needs that
too or an AI keeps shooting a friend. ⚠ Do not set a zeppelin's team to a literal; 42 of 58 records
author none and what an unauthored one falls through to is D33's question. ⚠ D33 alone already fixes
the reported Kestrel symptom, so this item must not be judged by that symptom disappearing.

**Landed.** The identity is one nullable string, `DestructibleRegistry.Instance.Owner`, written by
`ZeppelinRuntime.WireZones` beside the team it already fanned, on every zone pool and unconditionally
(a record authoring no team still owns its zones). `TargetPool.OwnerOf` reads it back beside
`NameOf`, so the targeting path keeps one source-type switch rather than growing a second, and
`AiTargetRanking.ObjectiveBiasFor` gains an owner overload where a bias entry matches on either
name. The authored order still decides which entry wins, so the decoded first-match rule is intact.
For the guns, `TurretController.SetTeam` makes the team writable, `TurretEmplacementRuntime` gains
`SetTeamUnder`, the same subtree walk as its `SetActivatedUnder`, and
`ZeppelinRuntime.FanTeamsOntoTurrets` runs it per authored record from `GameSession`, after the
emplacements are built because they do not exist before then. No literal is invented anywhere: a
record with no team fans nothing and its guns keep their authored `TURRET` default.

**The read-only trap is discharged rather than worked around.** `docs/org/targeting.md:230` records
that `FUN_004acb70` clears the turret's target pointer `+0x210` on a team change so a now-friendly
lock is not kept. CSVM's gunner holds no such pointer: `TurretController.AcquireTarget` rescans the
aircraft list every tick and returns a position, so there is no lock to drop. What does survive a
tick is the cached line-of-sight verdict, taken against the old team's target and valid for another
1 to 2 s, so `SetTeam` expires that and clears the standing aim. The prohibition on the member now
says which half applies here and why.

**Verified.** The new `zeppelin-identity` suite drives both halves off shipped records. The bias
half reads C5/M03's own roster and asserts that its authored `["cargozep*", -1.0]` reaches a pool
named `gasbag1` only through the owner `cargozep2`, returns 0 through the zone's own name, and
returns 0 for a pool owned by `beowulfzep`, which that mission does not name. The gun half builds
C1/MP3's world, wires the real `ZeppelinRuntime` and `TurretEmplacementRuntime`, and measures the
fan: 14 emplacements under the `ally` hull `multiplayer2zep` move to team 1, the 14 under the
unauthored `multiplayer1zep` stay on the `TURRET` default 2, and a second fan moves 0, which is
`SetTeam`'s own did-it-change report. `targeting-candidates` carries the end-to-end arm: a real AI
gunner with an authored `-1.0` naming the owning hull acquires nothing, while the same `-1.0` naming
a different hull leaves it holding the zone. Perturbations, one variable at a time and run
separately: ignoring the owner in `ObjectiveBiasFor` fails the exclusion arm in both suites, and
making `SetTeam` refuse every write fails the fan arms with `moved=0` against an expected 14.
⚠ Both bias arms are written from an IDLE gunner, because a standing ground target is sticky: the
first revision asserted the exclusion by dropping a pick already made, and it failed on the
stickiness rather than on the bias.

The census that says how much was dead: across the shipped install, 34 `rating_biases` entries in
20 missions name a zeppelin node of their own mission, and every one matched nothing. Ten of them
(C2/M05's two, C4/M05's four, C5/M02's one and C5/M03's three) sit on a record that authors a team,
so they are ranking terms again. The other 24, C3/M01's `["piratezep", -1.0]` among them, name a
record authoring no team, so D33 keeps those zones neutral and out of the rank pool entirely and the
term still cannot fire there. That is the original's own arrangement rather than a gap: neutral is
total, so the authored exclusion is redundant beside it. ⚠ This is also why the item is not judged
by the Kestrel symptom, exactly as its traps say.

`dotnet build` clean, `dotnet test` 2363, `--run-tests=target` 6/6, `--run-tests=zeppelin` 8/8,
`--run-tests=campaign` 11/11, `--run-tests=turret` 2/2.

## G63 ❌ The music channel has no 15 s refusal hold (`BL-480`)

**Goal.** A music cue raised inside the previous cue's 15 s hold is dropped, as the original drops it.

**Evidence (confidence: traced-to-code, decoded at the image).** Found while B12 decoded the mission
radio queue. `FUN_0046caf0` gates the rule on `FUN_00480460`, an is-music predicate with exactly one
call site reading bit 3 of the sound-flag word, and the keyword table at `0x4802e0` gives `MUSIC` 8.
The rule is `if (this+0x20 != 0 && now < this+0x24) return 0`, a refusal rather than a delay.
`MusicPlayer`'s existing rule that re-cueing the playing track never restarts it covers the common
case, which is why this is a gap rather than an audible bug today.

**Approach.** The hold on `MusicPlayer`, which is where `CampaignDirector.PlaySoundGroup` routes `mu*`
cues before the radio is consulted.

**Model recommendation.** low.

**Verify.** A unit test that a second cue inside the hold is lost and a third after it plays.

**⚠ Traps.** ⚠ It refuses, it does not defer: implementing it as a delay changes which track plays.
⚠ Do not put it on `MissionRadio`, which never sees a music cue.

**Landed as a disproof, and the approach above is the thing disproved.** The rule is at the image
exactly as the evidence states, confirmed instruction by instruction: `FUN_00480460` is the is-music
predicate, the refusal is `+0x20 != 0 && DAT_0071c470 < +0x24` returning 0 with no record made, and
an accepted cue sets `+0x24` to the clock plus the constant at `0x00603560`, which reads 15.0. What
the evidence did not carry is where the rule sits. `FUN_0046caf0` is the objectives runtime's
tracked-cue creator, and `FUN_0046cc70` sends every woken group that is not one of the three
`player.zrd`-bound sounds straight to `FUN_00593590`, so none of the 133 mission music cues in the
53 shipped `objectives.zrd` files can reach the hold. Of the three bound sounds only
`in_battle_sound` is real, holding `music_battle_sg`, which no mission cues and which the battle
timer sounds for at least its 20 s hold plus a 4 s fade before the channel frees; `FUN_0046cdf0`
clears `+0x20` when it sweeps a stopped record, so the deadline has passed before a restart is
possible. The hold therefore changes nothing that is heard.

Putting it on `MusicPlayer` would have been a regression rather than a fidelity gain, because that
is where all 133 of those cues land in this project, and a hold there would drop objective stingers
the original plays. So no behaviour changed. `docs/org/music.md` gains the decode, the three facts
that make it unreachable, and the prohibition, and `MusicPlayer.Cue` carries the prohibition on the
member itself so the next reader of the same evidence does not re-file it.

**Verified.** `dotnet build` clean and 2364 unit tests pass, which is the whole check a
documentation change and a comment need. The census behind the 133 figure was re-run over the 53
`objectives.zrd` files in `extracted/` and reproduces the table in `docs/org/music.md` exactly, and
`music_battle_sg` appears in none of them. `player.zrd`'s two placeholder bindings were read back
from `extracted/zrdr/player.zrd.json`.

## G64 ❌ The 26 `GRAPHICS/*.JPG` draw nothing (`BL-479`)

**Goal.** The cabin's plane photographs and the fifteen menu backgrounds draw.

**Evidence (confidence: traced).** C23 gave the art seam a decoder chosen by file name, so a `.PNG` or
`.TGA` draws and a `.JPG` returns null, which is the never-invent answer rather than a hole in the
seam. The decision recorded on `ArtImage`'s own type doc is to transcode at extract time rather than
write a decoder in `Mech3`, on four facts: the scope is 26 files; two of them (`CR_BACKGROUND` and
`MP_LOBBY_BACKGROUND`) are progressive `SOF2`, so a hand-written baseline decoder several times
`PngImage`'s size would still leave those two blank; `ExtractRof.ps1` already writes additive PNG
sidecars beside the 184 custom `.BM` textures; and the host decodes all 26 including the progressive
pair, verified in memory without writing to `extracted/`. B13 landed the composed boards on top of
this gap, so it is now visible on the way into every mission.

**Approach.** The additive `.JPG` to `.PNG` sidecar pass in `ExtractRof.ps1`, keeping the original.
`ArtImage` needs no new branch afterwards.

**Model recommendation.** low, but it touches the extraction pipeline, which raises the care rather
than the reasoning.

**Verify.** A re-extraction, then a shot of the cabin and of a menu background that was blank.

**⚠ Traps.** ⚠ This needs a re-extraction and bumps the `VERSION.json` stamp, which is why it was kept
out of the seam work; it cannot share a worktree with anything reading `extracted/`. ⚠ Do not return
a placeholder for an undecodable image: a wrong picture reads as a fidelity verdict.

**Landed.** No transcode, because the premise is false: the JPEG pictures already draw. Every JPEG a
screen names is a **board** picture, and `ComposedBoardView.Load` reads it through Godot's own
loader (`UI/ComposedBoardView.cs:252`), which the four recorded facts never weighed. The three edits
are the prose that said otherwise. `Mech3/ArtImage.cs:20` now carries the prohibition on its
`TryLoad`, `docs/architecture.md`'s `ArtImage` entry carries the measurement, and
`UI/CampaignCabinPage.cs` loses `PlanePhoto`, a `PC_P_HANGAR<n>.JPG` load through `ArtImage` that
could only ever return null and whose result the campaign shell never draws (`Rebuild` returns at
`LaunchMenu.cs:2130` for every campaign screen, so `Art` reaches nothing but the briefing's
change-detection). `Art` is the cabin scene alone and `Pictures` is unchanged.

**Verified.** `--menu=campaign-cabin` draws `PC_P_HANGAR5.JPG` in the window: the shot's photograph
block matches that file's own pixels at a mean absolute channel difference of 1.74/255, against 8.9
to 16.7 for the other ten photographs, so the picture is identified rather than assumed.
`--menu=campaign-flightcheck` draws `FC_BACKGROUND.JPG`. Godot's loader decodes all 26 files,
`CR_BACKGROUND` and `MP_LOBBY_BACKGROUND` included, pixel-identical to a GDI+ reference decode over
a 7,500-point grid, so the progressive pair is not a gap either. The cabin shot's raw-pixel hash is
unchanged across the edit (`29b9f2aa63d634f9b756880d9c5bf291`), which is the point: deleting a load
that returned null moves nothing. `dotnet build` clean with 0 warnings, `dotnet test` 2363 passed.

## G65 ☑ `campaign-objectives-hud` cannot fire its own check on C3 (`BL-481`)

**Goal.** The suite proves its point on any chapter it is pointed at, or says explicitly why it
cannot.

**Evidence (confidence: traced, and confirmed pre-existing).** The suite passes on C1 and fails on C3
with an empty objective name, and the artifact shows its driver completing no objective at all on
C3/M01, so it is not catching a display defect: it is reporting that its own completion driver found
nothing to complete. The identical failure reproduces on `4ec47b14`, before Wave F, on a separately
built worktree, which satisfies METHOD-8. B11's move strengthened the suite and surfaced it.

**Approach.** Make the driver find a completable objective on whatever chapter it is given, or skip
explicitly where a mission carries none.

**Model recommendation.** medium.

**Verify.** The suite green on C1 and on C3, with the skip path, if one is taken, printing what it
skipped and why.

**⚠ Traps.** ⚠ Do not relax the assertion, which is the one proof that the readout marks its own line
rather than only the graph. ⚠ Do not assume C1's shape generalises: C3/M01 builds six display rows to
C1/M02's five, and two of C3/M01's are `IDENTITY [SECONDARY, n]` fields with no message key.
⚠ `docs/verification.md` DIAG-15 forbids a silent skip.

**Landed.** The driver's filter was the whole of it. `DriveCompletionCue` considered an objective
only when it authored an `INACTIVEn` node list, and `C3/M01` authors not one `INACTIVEn` in the
file, so the loop body never ran on that chapter and the assertion reported a null objective
number. The driver now picks a forcing route per objective from whatever the mission authored
(`CampaignHudSuites.cs:411` `RouteFor`): an `INACTIVEn` node list, a `DANGER_ZONES_COMPLETED` zone
list notified through `ObjectiveGraph.NotifyDangerZoneCompleted`, or no condition at all, which
completes on its first eligible tick. `ForceRoute` (`CampaignHudSuites.cs:429`) opens the
`TICK_DEPENDS_ON_OBJ` gate before the wake and notifies zones after it, since a wake clears the
objective's own zone tally. The row check is also stricter rather than relaxed: `RowNewlyMarked`
(`CampaignHudSuites.cs:455`) requires the row at the completing objective's own identity priority
to turn over, where the old code accepted any row going green, which a woken tick dependency could
have supplied. `CheckRowMarking` (`CampaignHudSuites.cs:387`) holds the assertion hard whenever at
least one display objective is forceable, and only when none is does it print, through both the
report artifact and `ctx.Note`, how many display rows the mission has and the condition family that
put each one out of range.

**Verified.** The suite passes on `C1`, `C2` and `C3`; `C3/M01` completes `OBJECTIVE3` off
`dzpath1` and `OBJECTIVE31` off no condition, each marking its own secondary row. `C4` and `C5`
still fail, on `DriveWakeCue`'s check that a `WAKEUP_SOUND_GROUP` starts a real one-shot, which is
a different half of the suite and reproduces identically with this file reverted to its committed
state, satisfying METHOD-8. Their row-marking half passes. The skip path was exercised by forcing
`RouteFor` to return `None`, which printed all six of `C3/M01`'s rows with their condition families
and left the run reporting a note rather than a pass in disguise. `dotnet build` is clean and
`CSVM.Tests` is 2363 passing.

## G66 ☑ Two persist-log behaviour questions, carried out of `BL-243`

**Goal.** Two answers, and a test for each if the answer says our behaviour diverges.

**Evidence (confidence: lead-only, and deliberately so).** E43 closed `BL-243` because the
cross-mission persist log is built and covered by `campaign-persistence`, and carried its two untested
questions here rather than dropping them with the entry. Whether the log commits at damage time or at
mission completion is unmeasured. Whether an Instant Action session loaded after a campaign mission in
the same process picks the log up is unmeasured, and `InstantActionDirector` has no apply call, which
is a reason to think it does not.

**Approach.** Answer the second question from our own code first, since it is a one-file read, then
decide whether the first is worth a decode or whether the shipped behaviour is defensible either way.

**Model recommendation.** medium.

**Verify.** Each answer stated with the evidence that chose it, plus a `campaign-persistence`
assertion for whichever one turns out to be a divergence.

**⚠ Traps.** ⚠ This is a research item and its deliverable is an answer. Building a commit-timing
change without settling which timing the original uses would be inventing content.

**Landed.** Both questions are answered, and the first turned up a divergence beside the timing it
asked about. The original writes its world-state carrier once, in the mission-end pass
(`FUN_004174a0` into `FUN_0046b450` into `FUN_0046b490`), the same pass that writes `Status.dat`,
so the commit is at mission completion and never at damage time, which is what `OnMissionEnded`
already did. That pass is also gated on the campaign object existing (`DAT_0071bb7c`) and on its win
flag (`+0xc58`, read through `FUN_00463be0`, the flag the debrief `FUN_004194e0` and the cinema pick
`FUN_0046ba10` both branch on), so a lost or abandoned attempt writes nothing and a retry starts from
the state the last won mission left. We merged on every outcome. The rule is now
`CampaignPersistLog.CommitsOn`, which `CampaignDirector.OnMissionEnded` consults. Instant Action does
not pick the log up and should not: the original's load walks backwards from the campaign object's
`cm_sequence` index, and `cm_sequence.zrd` holds exactly the 24 campaign missions, so no Instant
Action mission has an index to walk back from. `docs/formats/saved-games.md` carries both gates, the
walk's key, and the one edge left open, which is whether a stale index can survive into a later
non-campaign launch in the same process.

**Verified.** `dotnet build` clean, 2364 unit tests pass including the three new `CommitsOn`
assertions, and `campaign-persistence` passes on C1 carrying three persisted objects from `m04` to
`m05`. The suite now also asserts the won-only rule at the point it stands in for the commit.

## G67 ☑ `BL-181`'s blocker now reads as discharged when it is not

**Goal.** The blocker names something that can actually arrive.

**Evidence (confidence: traced).** B13 raised it on landing. `BL-181` blocks the marker HUD and
scoreboard layout sign-off on a menu hub with its own type scale to review them against. The campaign
boards have landed, so someone will read "the boards are in" as discharging it, but those boards are
painted original artwork with a per-background palette and no shared type scale, so they supply
nothing to review against.

**Approach.** Rewrite the blocker to name the type scale it actually waits on, and say why the
composed boards do not provide one.

**Model recommendation.** low.

**Verify.** The entry read back by someone who has not seen this plan.

**⚠ Traps.** ⚠ Do not discharge it. The playtest verdict it records is contingent and the contingency
has not been met.

**Landed.** The blocker tag reads `[Blocked: a shared type scale]` rather than naming a milestone, and
the entry now says what the contingency actually is: the playtest verdict says these read acceptably
in isolation, and a sign-off needs them read against chrome the original did not paint. The entry
carries the reason the composed boards do not supply that, which is that they are painted original
artwork at authored pixels with a per-background ink palette, so they define no type scale, no
distance units and no shared font choice for an in-flight overlay to match. Done by the orchestrator
rather than a lane, because `backlog.md` is a single file three lanes would otherwise conflict in.

## G68 ❌ Why the scaffolding read differently at the controls (`BL-477`)

**Goal.** An answer to the user's report that a shot passes through the transparent scaffolding in the
original, given that the decode says our behaviour matches.

**Evidence (confidence: the disproof is decoded; the remaining candidates are lead-only).** F58
settled the mechanism against the report. The original's weapon-ray polygon test reads no texture data
at any point, and the census names the occluders: 5 of 180 rays reach `hydrogentank1`, the front truss
`g469` takes 26 of the 36 level azimuths. Texture alpha, a `BL-335` header bit, `intersect_bbox`, LOD
mis-selection at gun range, backface and the projectile's `0x40000` mask are each ruled out with
evidence. Two candidates remain: the shot may have been beyond 800 m, where the original narrows to
`f_mid`/`f_lo` whose cards are ±28.3 m of local x against `f_hi`'s ±48.9 m, or the difference is in
aim assist and target selection rather than in the ray.

⚠ **Unblocked, and neither candidate was it. The weapon was a rocket and what reached the tanks was
its blast.** The user's own account of the original run, given after F58 landed: the rocket was fired
above the scaffold through a small gap, detonated there, and the splash killed the tanks, which they
describe as a lucky shot. So no projectile passed through the truss in the original either, and the
original report's "shoot through the transparent scaffolding" was the blast reaching past it rather
than the round doing so. F58's disproof stands untouched and is now the whole answer for the ray.

**The question this becomes is about splash occlusion, not about the ray.** Ours already occludes:
`Projectile.BlastCovered` casts from the burst to each candidate's centre with the candidate excluded
and treats anything it meets as cover, which is the original's `FUN_004cb420` under `FUN_005aca30`'s
occlusion flag, and world geometry is cover while aircraft are not (C11). The truss is world geometry
and F58 established it is solid to a ray, so a burst on the wrong side of it is correctly blocked. The
open question is narrow: does a burst in the gap the user found have a clear ray to the tanks in ours,
the way it did in theirs?

**Approach.** Reproduce the shot's geometry rather than the shot. `alpha-cutout-ray-census` already
places `cargozep1` at its authored pose in a built C3/M01, so extend that stage: sample burst points
in the volume above the front truss, run the real `BlastCovered` test against each of
`hydrogentank1..4`, and report which burst points reach them. If some do, ours reproduces the original
and the item closes as an answer. If none do, the difference is in one of two named places and both
are already known: our colliders are two-sided (`BackfaceCollision = true`) where the original honours
`show_backface`, which 33 of `g469`'s 39 cards set, so a card the original's ray passes through from
behind is cover in ours; and `CoverRayLift` has no counterpart established for a burst with no struck
surface.

**Model recommendation.** medium. The instrument exists and the decode is settled, so this is a
measurement and a narrow comparison rather than a new decode.

**Verify.** The burst-point census, stated as a fraction reaching the tanks, and if code follows, the
same census before and after. ⚠ `alpha-cutout-ray-census` exists because of INSTR-24: a raycast in the
same call that moved a static body reads the collider at its pre-move pose. Any new stage that places
the airship inherits that trap.

**⚠ Traps.** ⚠ Do not reopen the ray rule. `SceneBuilder.EmitCollisionFaces` carries the prohibition
against writing an alpha rule, and F58's traps on why a blanket rule is wrong stand. ⚠ Do not widen
the blast radius or drop the occlusion test to make a lucky shot reproducible: the user calls it a
lucky shot through a small gap, so the correct outcome is that most burst points fail. ⚠ Footage
cannot supply the burst position; the user's account is admissible as what happened and not as a
measurement (`docs/verification.md`). ⚠ The 32-target cap on the splash gather is decoded
(`FUN_004cb420`'s hit buffer) and is not a candidate here, since four tanks cannot overflow it.

**Landed.** ⚠ **The answer is that ours already reproduces the original's shot, and no code follows.**
The shot was not through a gap in the truss at all: the rocket struck the hull underside above the
tanks, and the truss is a lateral screen around them rather than a roof over them, so the burst was
above the screen. The shipped data says the volume between the two is empty. A ray straight up from
each tank's top centre meets `g482`, the belly plate under `underneath` (`cargoskin2`, alpha None),
at y = -48.9 to -52.5 in `cargozep1`-local coordinates against tank tops at y = -61, with no polygon
in between; `g469` spans y = -81.0 to -28.3 but stands out to x = ±48.9, so no vertical ray over a
tank crosses one. Neither of the two named differences is reached, so `show_backface` and
`CoverRayLift` are both left alone. What did land is the instrument: `ProjectilePool` exposes its
own cover predicate and its own gather to a census (`BlastCoverBetween`, `BlastCoverCensus`), so the
suite tests the shipped rule rather than a copy of it, and `analysis/bl-477-weapon-ray/census.py`
gains the volume census as its fifth section.

**Verified.** `alpha-cutout-ray-census` gains the splash half and passes. It places the burst by
measurement rather than assumption, casting up off `hydrogentank1`'s top and striking `g482` 14.5 m
above the tank's centre, then runs the production cover ray from that surface down to each of
`hydrogentank1..4`: all four clear. The able-to-fail control, the same ray from 120 m abeam at tank
height with no struck surface and so no `CoverRayLift`, is stopped by `g469`. Through the production
gather at the HE rocket's `IMPACT_PROXIMITY` of 15 m, the burst reaches `hydrogentank1` uncovered at
8.5 m to its nearest surface, while `g469` and `panelrightb2` come back covered by `g482` itself,
which is the lift doing its job. The other three tanks lie 33 to 56 m from that burst, outside one
rocket's radius, so a single rocket kills one tank directly and the rest is the chain; that is a
radius result and not an occlusion one. The ray half is unchanged at 5 of 180.
