# Milestone 5 polish, run 5: the D32 sortie follow-ups, CM04 to CM09

**ACTIVE PLAN** (written 2026-08-28). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This run takes the follow-ups the run-4 closing sortie (D32 of `PLAN-M5-polish-4.md`, flown CM03
to CM09) filed or handed back: the three partials whose remaining half came back (`BL-513`,
`BL-521`, `BL-512`, `BL-522`), the disproofs the user's eyes reopened (`BL-567`, `BL-571`,
`BL-576`), the one disproof that turned into a decode prerequisite (`BL-526`), and the fresh
filings `BL-567` to `BL-582`, together with the two CM12 items that share a cause with them
(`BL-564` with `BL-577`, `BL-578` with `BL-512`). The items are ordered by mission so that one
closing sortie can walk CM04 to CM09 and judge each fix where it was reported. The selection is
the "campaign walk" criterion again (Decision 1), at the whole-set size (Decision 2): open `[Bug]`
and `[Fidelity]` items a flown campaign mission trips over, unblocked, with a named lead.
Research-only items, `[Owed-playtest]` items, anything `[Blocked: ...]`, the plan-sized or
user-deferred items (`BL-150`, `BL-446`, `BL-463`, `BL-256`) and the AI mode machine
(`BL-523`, `BL-550`, `BL-558`, `BL-565`, `BL-566`) are out.

One item sits outside that walk: `BL-585` (Wave E), a blocker reported at the controls in CM13
(C2/M03, the air race). The enemy racers follow their net instead of locking onto the mission's
`dzpath` ribbons the way the original's AI does, so they finish the course well ahead of the
player and the mission fails around the halfway mark. It is a decode-first feature, not a
follow-up of D32, and it is judged at CM13, not by the D31 sortie.

Every item was re-verified still-open against the record in this session: `git log --grep` on
each id finds only its filing, a cross-reference or a partial (`BL-583`, the one D32 filing that
landed, is already gone from `backlog.md`; `BL-563` closed on main). The two live worktree
branches (`bl-431-cockpit-gauge-drive`, `worktree-m5-campaign`) hold no unmerged commits. Each item's section records its re-verification against the code, made by the implementing agent. Evidence grades are capped by provenance: an item whose backlog entry rests
on a log line, a data file or a cited routine is `traced`, an item resting on a controls report
alone is `lead-only`.

**Decode stays open per item (Decision 3).** Every item's Approach names the `crimson.exe`
routine or the data file that settles it, and an implementer may take that lane instead of the
code-side lead whenever the lead runs out. A decoded rule beats a plausible fix here, as in every
prior run.

## Milestone goal

- CM04 opens as authored: the buildings destroyed in CM03 stand destroyed without exploding again,
  the barrage balloons are down, the opening cutscene plays, the Pandora flies in over the first
  minute, the Barracuda drives smoothly into the bay and its fighters fly the take-off run instead
  of dying on the deck, and the broadsides leave the player alone.
- CM06 and CM07 read right: a dead gun ring and its fire ride the hull, objective markers carry
  the original's verb and proper name, the AA guns do not kill themselves, the hangar hand-over
  animates and gives the player the Blue Streak in its livery, and the rope ladder's native switch
  is decoded far enough to build.
- CM08 and CM09 play through: the Pandora holds a sane pitch along the Klondike net, the patrol
  boats and the tanker are where the data puts them, the airfield launches what the data names, a
  `[parent, child]` objective target marks one node, and the docking objective is reached once
  the Promised Land is down.
- One closing sortie, CM04 to CM09, flown by the user, with each item judged where it was
  reported.

**The AI mode machine (`BL-523`, `BL-550`, `BL-558`, `BL-565`, `BL-566`) is out of scope.** It is
one decode of `AiModeMachine`'s source routines, heavier than any item here, and it gets its own
run; an item that touches the machine on its way (A6's launch release, C22's boat AI) takes only
what its own symptom needs and hands anything deeper back to `BL-523`. E41 is the one
exception: it enters the machine's two danger-zone modes, and only those, under a decoded rule.

## Decisions (2026-08-28)

| # | Question | Decision |
|---|---|---|
| 1 | Which selection criterion | **The campaign walk, the D32 follow-ups** over "traced quick wins only", "choreography and AI" and "cross-theme": the reports come from one sortie and a mission-ordered run lets one sortie re-judge them all. |
| 2 | Size, and whether paired cause/symptom items ride as one | **About 14 items, the whole follow-up set; pairs count as one.** `BL-512`+`BL-578`, `BL-522`+`BL-527`, `BL-569`+`BL-579`, `BL-574`+`BL-575`, `BL-577`+`BL-564` each ride as one item. Sixteen items plus the sortie. |
| 3 | Weight | **Routine, with the decode lane kept open on every item.** No up-front data survey; each item names what in `crimson.exe` or the mission data settles it. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
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

### Wave A — CM04 (C3/M03)

1. ☑ `BL-513`: the persist-log replay runs the previous mission's death choreography at mission open
2. ☑ `BL-521`: the barrage balloons stand at mission start
3. ☑ `BL-567`: the Pandora's broadside cannons fire on the player (decode: `player` resolves; the report stands against it, flown original check owed)
4. ☑ `BL-568`: the Pandora starts moored in the dry dock instead of flying in
5. ☑ `BL-512` + `BL-578`: `ObjectMotion`'s `rnd_xz` makes the Barracuda and the CM08 tanker jump
6. ☑ `BL-522` + `BL-527`: a surface generator's launch does not fly its take-off run, so the fighters die on the deck
7. ☑ `BL-569` + `BL-579`: a cutscene called from a start anim fires at bootstrap with no camera (CM04 and CM09)

### Wave B — CM06 (C1C/M01) and CM07 (C1/M02)

11. ☑ `BL-571`: a carried turret's death fire, and the turret, stay where the turret died
12. ☑ `BL-572`: an objective marker labels the raw node name instead of the original's verb and proper name
13. ☑ `BL-573`: the AA guns damage themselves firing at a barrier
14. ☑ `BL-574` + `BL-575`: the hangar hand-over gives a stock Bloodhawk and plays no animation
15. ☑ `BL-526`: the rope ladder never deploys (a native per-mission switch to decode)

### Wave C — CM08 (C1B/M03) and CM09 (C1/M04)

21. ☑ `BL-576`: the Pandora pitches steeply up and down along the Klondike net
22. ☑ `BL-577` + `BL-564`: no runtime for a roster or generator surface vehicle, so the patrol boats never spawn
23. ☑ `BL-580`: `eairg32`'s launch falls back to a `player_bhawk` on a misspelt parameter block
24. ☑ `BL-582`: a `[parent, child]` objective target is flattened into two bare names
25. ☑ `BL-581`: the docking objective is never reached after the radio tower goes down

### Wave D — the sortie

31. ☐ Fly CM04 to CM09 end to end and judge every item where it was reported

### Wave E — CM13 (C2/M03), the blocker

41. ☑ `BL-585`: AI planes never lock onto a `dzpath` and fly it on rails, so the CM13 racers skip the danger zones

### Wave F — the CM02 capture (found on D31's way in)

42. ☑ `BL-588`: CM02 is lost 20 s after the player captures the last bomber, because the swap leaves group 5 empty and every holder of the old rig dangling
43. ☑ `BL-590`: the Barracuda teleports between its surfacing and its drive, because a launch was re-seated on the authored rest pose
44. ☑ `BL-591`: the Pandora fires on the player in CM04; the broadside engage flag (COMPLETED_ZEPCANNONS) decoded, closes `BL-567` and `CAP-46`
45. ☑ `BL-592`: CM06's docking cutscene hands flight back when its first callee ends and teleports the player when the row runs out
46. ☑ `BL-593`: a gun ring's death fireballs and debris stand still while the hull sails on; B11's AT_NODE/WITH_NODE split withdrawn
47. ☑ `BL-594`: CM07's launched Peacemakers strike the strip two seconds after hand-off; the take-off run ends 300 m past its last waypoint, climbing
48. ☑ `BL-596`: the hangar hand-over plays without the aeroplane, and the Blue Streak's livery is the unpainted shipped skins (yellow wingtips)
49. ☑ `BL-595`: no rope ladder, the passenger hangs in the air, no flare; the ALL_NAMES SI-script form decoded and the caboose choreography plays
50. ☑ `BL-600`: CM09's intro shows no wingman on the launch and the dive; the piratefighter prop keeps the archive's shipped state
51. ☑ `BL-599`: the Promised Land no longer burns out (regression from B14); the compiled prerequisite state is bit 0 of active_raw
52. ☑ `BL-602`: CM13's first zone marker sits at the world origin; an origin-standing group site anchors on its built meshes, the race chain pinned
53. ☑ `BL-601`: CM13's racers ram the hangar on dzpath2; an AI rig sweeps its def's one collision probe, as the original does
54. ☑ `BL-604`: CM02 is still lost during the capture, before the swap: the wing walk's 913 park counted as a deactivation for DEDG
55. ☑ `BL-615`: CM13's racers loop around zone 3 instead of going on; the run exit's net re-seat never refused the leg it entered on
56. ☑ `BL-616`: CM13 docks with an invisible Pandora; a zeppelin record is what switches its hull on
57. ☑ `BL-609`: no flare smoke and a frozen ladder in CM07's pickup, both disproved; the suite's two blind spots closed
58. ☑ `BL-611`: CM07's AA guns never fire; the chapter persist log carried the mission's own wreckage into its own replay
59. ☑ `BL-610`: CM07's hangar drop plays with no aeroplane and hands the pilot back at the world origin, because an earlier cutscene left the `player` marker parented to the train

### Wave G — the CM02 capture's two ends (D32 follow-ups)

60. ☑ `BL-607`: a cutscene episode books to the first raiser on every path but the landings one, so a parent that calls several raisers is cut short
61. ☑ `BL-608`: CM02's crew bail out as one figure at the world origin, and the docking on the Pandora never ends the mission
62. ☑ `BL-620`: CM02's ending waits out an objective wrap-up the completion code does not take
63. ☑ `BL-621`: CM07's hangar drop loses its parachutist to G61's staged-actor pool

## Dependency and parallelism notes

A5 is upstream of A4, A6 and C21 in one respect only: it settles what `rnd_xz` and `ObjectMotion`
ownership mean, and A4 (a zeppelin under an `ObjectMotionSiScript`) and A5's tanker half both
touch `MotionRuntime` and the net/scripted-path followers' write to a pose, so run A5 first and A4
after it, not in parallel. A6 is the largest Wave A item and the one C23 leans on (both read
`CampaignRosterPlan.GeneratorTemplates` and `GameSession.SpawnFromGenerator`); C23 is small and
can land first, but not in a parallel worktree with A6. A7 pairs two missions on one mechanism
(the cutscene runner's registration for a start-anim camera) and lands as one change judged at
both sites. A1 and A2 share `AnimRuntime.SyncDestructiblePool` and the start-state path; A1 owns
`CampaignPersistLog.ApplyTo`, A2 owns the `.gw` switch and the reader-def resolution, and they
may run in parallel with that boundary stated.

B11 touches `AnimRuntime`'s `CallAnimation` hand-off and `EmitterDirector`; B14 reads the same
cutscene dispatch A7 changes, so B14 runs after A7. B12 and C24 both edit `TargetRef`'s display
and `ObjectiveScript`/`ObjectiveSites`; C24 (the node resolution) goes first, B12 (what the label
says on the resolved node) after it. B13 is alone in the turret fire path. B15 is a decode item
that lands a `docs/org/` page and possibly no code; it contends with nothing.

C21 is alone in `ZeppelinMotion.cs` once A4 has landed (A4 touches `ZeppelinRuntime` placement,
not the pitch law). C22 is plan-sized inside this plan: a surface-vehicle runtime over the
scripted-path follower; it owns `CampaignRoster.cs`'s `Skipped` path and a new runtime class, and
its generator half meets A6/C23 in `SpawnFromGenerator`, so it runs after both. C25 first adds
the `campaign` objective log lines and then fixes `ObjectiveGraph.Ticks`; nothing else touches
the graph's tick, but C24 edits the same `ObjectiveGraph` file, so sequence them. D31 waits for
everything in Waves A to C.

E41 is the one item in the AI pilot proper: it owns `AiNetFollower`, the danger-zone arm of
`AiPilot.FlyPatrol` and the two enum-only modes of `AiModeMachine`. Nothing else in this plan
touches those files, so it may run in its own worktree in parallel with any wave, but its decode
half (`FUN_0041d1f0`'s `dzpath_%d` branch) comes before any code, and its verification is a CM13
flight by the user, separate from D31.

---

# Wave A — CM04 (C3/M03)

## A1 ☑ `BL-513`: the persist-log replay runs the previous mission's death choreography at mission open

**Goal.** CM04 opens with the objects destroyed in CM03 already in their destroyed pose, silent:
no fireballs, no sounds, no death sequence, and a later hit on them is a no-op.

**Evidence (confidence: traced).** The sortie log of a C3/M03 open shows, on the first frame after
bootstrap, `damage: -30 on aagun30 HP 30→0 DESTROYED — death sequence run` and the same for
`aagun31`/`aagun32`/`aagun01`/`aagun02`/`g_tower1`/`g_tower3`, `-60` on `u_camp1..3`/`unit10`,
`-15` on `t_truck02`, each recycling its `large_fireball`/`sputter_fire_smoke_obj` pool.
`CampaignPersistLog.ApplyTo` replays a chapter's carried destruction through the same `DamageAt` a
weapon hit takes, by its own design. The original's carried state is a destroyed pose, not a
replayed death: the `PERSIST_LOG` reader defs (`ucamp_dest`/`tower_dest`/…) are the silent
destroyed variants a later mission opens with. Run 4's A4 (`AnimRuntime.SyncDestructiblePool`,
the `start-state-swap-pool` suite) is a real hole and stays fixed, but was not this report's
trigger. Re-verified open against the code: `ApplyTo` computed `live.Health - state.Health` and
called `runtime.DamageAt(anchor, damage)`, which runs `ApplyDamageStages` and `RunDeathSequence`
(the def's Initial sequences plus its compiled destruction slot) exactly as a weapon kill does.
The shipped death shape confirms what a silent path must reproduce: `aagun30`'s swap lives in
its destruction slot (`hit_me_now`: healthy off, destroyed on, dbase on, then `genx12`), `u_camp1`'s
in its Initial sequences (`explode_house` fades and hides `healthy`, the next sequence shows
`destroyed` and hides `shadow`, then fireballs, debris motion and sound), `g_tower1` and
`t_truck02` switch debris pieces on, fly them and hide them. Every compiled `DAMAGE_SEQUENCE`
in the install carries only `CallAnimation` puffer calls (4,134) and three `StopAnimation`s, so
a carried partial HP has no node visual to restore, only its stage counter.

**Approach.** Give `ApplyTo` a silent path: set the pool to `Destroyed`/HP 0 (or the carried
partial HP with its damage stages), apply the destroyed role swap the death sequence ends in, and
run no effects, sounds or choreography; `DamageAt` stays the weapon path. A partially damaged
carried object (`state.Health` above zero) wants its stage visuals without the stage's puffer
bursts. Decode lane: how the original's `PERSIST_LOG` reader applies a carried state
(`docs/formats/saved-games.md`, `docs/formats/destructibles.md` "Starting destroyed").

**Model recommendation.** `AnimRuntime.CarryState(inst, destroyed, health)` is the one silent
entry, and `CampaignPersistLog.ApplyTo` calls nothing else. It writes the pool first (`Health`,
`Status`, `DamageStage` from the `DAMAGE_SEQUENCE` thresholds) and for a kill runs
`ApplyDeathPose`: it walks the sequences `RunDeathSequence` would play (the def's Initial
sequences, its destruction slot, a chained swap target's sequences) and applies only the
`OBJECT_ACTIVE_STATE` events that switch a node off or switch a destroyed/`dbase` role node on.
A non-role piece switched on mid-death is debris that flies and is hidden, so it stays off
rather than parking at its rest pose. A def whose death sequences hold no role swap takes the
RESET-derived `ApplyDeathSwap`, withheld when the def authors its own visible death, the same
rule the live kill applies. `DamageAt` is untouched and stays the weapon path;
`SyncDestructiblePool` is untouched (it is the dispatch-side mirror for a scripted swap, and the
silent path never dispatches). The log line is `campaign: persist log: N of M carried object(s)
restored silently in chapter C`, on the file sink.

**Verify.** The `carried-state-silent` suite (`DestroyChoreographySuites.CarriedStateIsSilent`,
beside `start-state-swap-pool`): on the chapter world, a `CampaignPersistLog` holding one kill
and one half-HP state for two shipped `PERSIST_LOG` destructibles is applied, with
`OnInstanceStarted` watched across the call; it asserts no instance started, the killed pool at
`Destroyed`/HP 0 with its `healthy` nodes hidden and a `destroyed` node shown, the worn pool at
`Damaged` with the carried HP and `ApplyDamageStages` finding no stage owed, and a later
`DamageAt` on the killed object resolving, leaving it `Destroyed` and starting nothing. The
`campaign-persistence` suite (its profile fixture: three `PERSIST_LOG` kills in the chapter's
second-to-last mission, saved and reloaded through `CampaignProfileStore`, applied at the last
mission) now watches `ApplyTo` and a follow-up `DamageAt` on each carried object the same way,
and asserts HP 0. The watch brackets only the calls, since a death's first start is synchronous;
the frames between are advanced unwatched because the world's own ambient loops (the zeppelin
prop defs) restart there. The C3/M03 open itself is judged in D31.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** The replay must still leave the pool `Destroyed` so a later hit is a no-op; do not
fix it by skipping the replay for destroyed objects, and do not gate on the visual state alone.
Absence of a log line is not evidence unless the sink carries that line (`GD.Print` lines do not
reach the file sink).

## A2 ☑ `BL-521`: the barrage balloons stand at mission start

**Goal.** CM04's barrage balloons (`bont1..6`/`b_turret1..6`) are down at mission open and are
not in the AI gunners' target pool.

**Evidence (confidence: traced, two leads still open).** The log shows AI gunners engaging them as
live turrets on the first frames (`ai gunner: shooter 100 targets MSG_TUR_DEFENSE_BALLOON@b_turret3
at 1027 m`, then `b_turret5`, `b_turret4`) although `support\c3\m03.gw` switches them fully OFF
(`NodeSetActive off`). Later, `anim: WAIT_FOR_COMPLETION on 'balloon_downa*' had nothing to hold —
no live callee instance`: something in M03 calls the `balloon_downa*` defs, which live in
`data\common\zrdr\turrets\balloon_down.zrd`, compiled only into M02's `mis_anim` and superseded in
M03 by the mission's compiled manifest, so the call reaches nothing.

**Re-verified against the code: both leads were real, and the second lead's reading was
inverted.** The `.gw` switch does reach the nodes (`b_turret1..6` and `bont1..6` are all
`NodeSetActive off` in `support\c3\m03.gw`), and CSVM applied it, but two things undid it. First,
`TurretController.Alive` read the `HEALTHY_NODE`'s own `Visible` flag, and a subtree switch
hides only its root, so every balloon turret stayed alive, ticked and sat live in every gunner's
scan. Second, the balloon defs were not dropped by the manifest, they were loaded where they must
not be: `balloon_down.zrd` and its four siblings sit in the shared `zrdr` scope, which
`AnimProgram` loaded unconditionally into every mission, and `balloon_down`'s `RESET_STATE`
(`bont* ACTIVE`) ran in bootstrap pass 1 over the pass-0 switch, which is why the balloons stood
visible at the controls, with six `ball_kaboom*` HP pools on canopies that were not in play. The
data's own rule is the `ANIMATION_DEFINITION_FILE` lists: M02's `mis_anim.zrd` lists the five
`balloon_*.zrd` files and M03's does not, and a census over every chapter's `cam_anim.zrd` and
every mission's `mis_anim.zrd` splits the 190 shared files into 88 reachable from the shared
`anim.zrd` index (every mission), 96 listed by individual missions only (zeppelin sets, balloons,
patrol boats) and 6 listed nowhere. The `balloon_downa*` wait line is a consequence of the first
two: a gunner killed a balloon that was not in play and its death choreography ran. Nothing in the
data resolves a chapter def against a superseding manifest; the original never loads what its
lists do not name.

**Landed.** `TurretController.Alive` reads the healthy node's visibility in the tree;
`AimCandidateSet.AddStructures` skips a hidden anchor by the same rule; `AnimProgram.Load` gates
the shared scope by the listed files (`ListedSharedFiles`, the `-N` duplicate-name suffix folded
back to its listed stem so `player-1.zrd.json` stays) when a compiled manifest is present, and
reports the skipped files on the bootstrap log. The `destructible-census` goldens move with it
(C1 214→186, C1B 29→28, C2 200→192, C3 228→210, C5 176→166; C4 unchanged): the pools that leave
belong to unlisted shared files (a zeppelin set the mission does not field, the army trucks, the
AA car). `world-turrets` now shows its aagun32 and the piratezep before expecting fire, because
`support\c1\ia1.gw` switches both off. The chapter scope is left ungated and handed back (see
below). Docs: `docs/formats/anim-definitions.md` "Shared-scope files are listed per mission
too", `docs/formats/turrets.md` gate 1, the three architecture entries.

**Model recommendation.** medium.

**Verify.** The new `mission-off-turrets` suite: C3/M03 places the six balloon turrets, all six
`b_turret*`/`bont*` roots hidden, `Alive=false`, `Gate=Dead` once woken, listed dead in the
gunner scan, no `bont*` HP pool and no `balloon_downa*`/`ball_kaboom*` def in the program; C3/M02
as the control with everything standing, plus a by-hand root switch on `b_turret1`/`bont1`
proving both gates directly. Affected suites re-run green in the foreground: `world-turrets`,
`carried-turrets`, `target-pool`, `targeting-candidates`, `start-state-swap-pool`,
`wait-for-completion`, `effects-census`, `destructible-census`, `instant-action`,
`instant-action-zeppelin`, `zeppelin-damage`, `damage-hd`, `death-slot`, `campaign-submarine`,
`campaign-zeppelins`, `campaign-cutscene`, `campaign-persistence`. The golden stage alone moves
five hashes (`c3-island`, `c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`,
`c1-targeting-hud`) by 2 to 3027 pixels of 921,600 at a channel delta of at most 15, invisible
against main's renders (the anim dice draw in a different order with fewer instances); the
manifest is re-pinned on the merged tree, not here.

**Handed back (needs an id).** The chapter scope wants the same gate: C1's `clouds`, `lightning`,
`spotlights` and `train_smoke`, C2's `game_targets`/`police_*`/`security_destroy` (M01/M02 list
two) and C5's `steinmann` (M01 lists it) are chapter reader files no list names, so the original
never runs them; but C1's `cloudparent#` 0.6 opacity comes from `clouds.zrd` and the overcast
match was judged with it in place, so that half needs the user's eyes on the C1 goldens.

**⚠ Traps.** `PLAN-c3-balloon-kill-chain.md` settled the balloons' kill chain for C3/M02; that is
the live kill path, not this mission's start state, so do not reopen it. M02 still registers
twelve `bont*` pools for six balloons (the listed reader templates beside their compiled
expansions); that duplication is that plan's, not this item's.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

## A3 ☑ `BL-567`: the Pandora's broadside cannons fire on the player

**Goal.** The Pandora's broadsides engage zeppelins only, never the player's aircraft, as the
original's rule states; or, if the decode shows `player` resolves, the report is recorded as
standing against the decode and handed to a flown original-game check.

**Evidence (confidence: traced to the fire site, decode incomplete).** Reported at the controls
with the rule stated. The log shows `shot hit P1 (fuselage→nose): wep_28 armor=5.0/25 ...` and
three more `wep_28` hits; `wep_28` is the piratezep broadside's weapon.
`ZeppelinRuntime.Cannons.ResolveTarget` takes the first live authored `targets` name and fires on
the player when the record names `player`. `BL-517`'s disproof read `FUN_004bd8d0` parsing
`targets` into name pairs and `FUN_004bede0` resolving each through `FUN_004bd430`, a name match
against the live zeppelin roster; it did not settle what a non-zeppelin name resolves to.
Re-verified against the code: `ResolveTarget` still fires on `player`, and the decode below
says the original does too.

**Decode (settled).** `player` resolves, and not through the zeppelin roster. `FUN_004bd8d0`'s
`targets` parse resolves every name through `FUN_004d0280(7, name)`, the general node table 7
lookup (the same call the cutscene code uses to find the player's node, `cutscenes.md` "The name
is what resolves"), and stores the node pointer as the first half of a `(node, zeppelin)` pair;
a name naming no node is dropped at parse. `FUN_004bede0` then calls `FUN_004bd430` with `ecx =
0x71df80`, the global zeppelin roster, which walks the roster's pointer vector comparing each
entry's node (`+0x1c`) with the pair's node and returns the first match or 0. `FUN_004bfe00`
walks the pairs in authored order: `pair.zeppelin != 0` takes the gasbag branch, `pair.zeppelin
== 0` reads the node's world position through `FUN_004cf2c0(node)` and runs the same intercept
solve and `> 0.707` arc test against it, then fires. The roster is zeppelins only, but an
unresolved pair is not skipped: it is fired on at its node's position. So a record authoring
`targets [player]` fires on the player in the original, and the remake's resolver is correct.
The controls report stands against the decode and is handed to a flown original-game check
(kept in `backlog.md` under `BL-567`, rewritten to say so).

**Approach (as landed).** No candidate-set change. The resolver's walk moved into the pure
`ZeppelinBroadside.FirstLiveTarget` (`IsPlayerTarget` names the one non-zeppelin candidate),
`ZeppelinRuntime.Cannons.ResolveTarget` consumes it, and `ZeppelinBroadsideTests` pins the walk:
`player` is a candidate like any zeppelin node, authored order, an unresolved name is skipped,
no name resolving gives no target. `docs/formats/mission-entities.md` "Broadside firing" carries
the corrected chain.

**Model recommendation.** medium.

**Verify.** `ZeppelinBroadsideTests` (the four `FirstLiveTarget` facts) and the
`zeppelin-broadside` suite, whose C1/M04 leg already asserts the readied side volleys `wep_28` at
the player; that assertion is now the decoded behaviour, not a placeholder. The headless CM04
check the item first proposed (no `wep_28` hit on P1) would fail by design and is not the gate.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not add a hostility or team gate; `BL-517` found none in the engine, and this
decode found none either. The user's report is not answered by this decode: only the original
game flown into the Pandora's broadside arc in C3/M03 can rank it, and that capture is owed.

## A4 ☑ `BL-568`: the Pandora starts moored in the dry dock instead of flying in

**Goal.** Over CM04's first minute the Pandora flies from `(-11314,554,-13697)` to the dry dock
along `pzep_todrydock`'s SI script, then holds its record seat on net `M3PirateZep`.

**Evidence (confidence: traced to the data, mechanism lead-only).** M03's `NEW_GAME_START` runs
`pzep_todrydock` (`extracted/C3/M03/mis_anim/piratezep-pzep_todrydock.json`, one
`ObjectMotionSiScript` on `piratezep`), whose script
(`data-c3-m03-zrdr-zeps-pzep_todrydock-piratezep.zan.json`, 185 frames, 0 to 61.65 s) ends at the
record's seat `(-12400.9,150.3,-10355.2)`, yaw -180. CSVM spawns the zeppelin at the record
position and the script never takes its pose, or the net follower writes over it.
Re-verified against the code: both. The bootstrap registered the script's `ScriptPlayback` on
`piratezep` and posed frame 0; `ZeppelinRuntime`'s constructor then wrote the record seat over
it, and every step after that `SimStep` wrote the follower's pose (`Place`, a `GlobalTransform`
write) while `MotionSet.Tick` wrote the script's. Which write the frame showed was tree order: on
the realtime clock both run in `_PhysicsProcess` and the zeppelin runtime, added after the world
root, wrote last, so the hull stood in the dock (the report); on a parent-driven `--det` clock
the anim runtime advances in `_Process`, after `DriveSimSteps`, so the script's pose won and the
`--debug-anim` pose log showed the fly-in. Neither the scripted-path snap (`BL-531`) nor the
dead-end hold (`BL-529`) was involved: both sit in the follower's step, which had no business
running at all.

**Approach.** An `ObjectMotionSiScript` on a zeppelin node owns that zeppelin's pose for the
script's duration, starting at frame 0's base, with the net follower parked and resuming from the
script's last frame. Check `ZeppelinRuntime`'s placement against the scripted-path snap (`BL-531`'s
fix) and the dead-end hold (`BL-529`'s fix) first; neither should apply to a scripted motion.
Decode lane: `docs/org/objectMotion.md` for who owns a node's transform while an SI script runs.

**Model recommendation.** The channel rule A5 stated, applied to the one non-animation writer of
a zeppelin node: `ZeppelinRuntime` asks `MotionSet.DrivesTransform(host)` (handed in by
`GameSession` at construction, since the bootstrap has already run the start anims by then, and
adopted from the runtime in `WireDamage` for every other caller) and, while it answers yes,
neither places nor steps the hull (`Park`, logged). On the first step after the motion ends
`Resume` rebuilds the `ZeppelinMotion` seated at the hull's live pose, engines as they stand, and
re-seats the follower from there (logged as the follower's first write). `ZeppelinMotion` keeps
its start pose private and is C21's file, so the resume goes through `SeatedAt`, the record with
its start pose replaced; a `ZeppelinMotion.ResumeAt(position, yaw, pitch)` would replace that
copy once C21 has landed. The record's seat is untouched: it is the script's end pose, node 0 of
`M3PirateZep` (stop point 1, armed), so the resumed follower holds the dock there.

**Verify.** `--anim-lab --node=piratezep` on C3/M03 playing `pzep_todrydock`: the pose at t=0 is
the script's frame 0, at 61.65 s the record seat, and the net follower's first write comes after
the script ends (log it). Measured: `--anim-lab --chapter=C3 --mission=M03 --node=piratezep
--play-anim=pzep_todrydock --debug-anim --frames=3720` logs the first pose as frame 0's base
`(-11316.0, 553.1, -13694.9)` rot `(-10.3, 130.7, 0)` and the last as `(-12400.9, 150.3, -10351.5)`
rot `(0, 179.9, 0)`, the record seat within the script's own end-of-path settle. `--fly
--chapter=C3 --mission=M03 --zeppelins --det --debug-anim --frames=3900` logs `zep: 'piratezep'
pose owned by a scripted motion from (-11314,554,-13697), net follower parked` at bootstrap, then
`zep: 'piratezep' scripted motion ended, follower resumes from (-12401,150,-10355) yaw 179.9°
pitch -0°, re-seating on 'M3PirateZep'` as the follower's first write, and `holding on its stop
point` at node 0 after it. The new `zeppelin-scripted-pose` suite drives the same over C3/M03's
built world: the bootstrap's script owns the channel and has posed frame 0 before any zeppelin
runtime exists, the runtime's placement writes nothing over it, the follower's motion never steps
(closest approach to the record seat over 50 m through the first 51 s, no frame over 5 m), the
script releases the channel at 61.65 s on the record seat, the resumed motion sits at the
hand-back pose with the record's yaw, and five seconds later the follower holds node 0 with the
hull unmoved. The eleven zeppelin and net suites (`zeppelin-motion`, `zeppelin-pandora-dead-end`,
`zeppelin-scripted-pose`, `zeppelin-launch`, `zeppelin-damage`, `zeppelin-broadside`,
`campaign-zeppelins`, `campaign-zeppelin-wakeup`, `zeppelin-identity`, `instant-action-zeppelin`,
`ai-net-follow`) pass 11/11 in the foreground, engine errors clean.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not move the record's position to the path start; the record's seat is data and
the script is what flies it.

## A5 ☑ `BL-512` + `BL-578`: `ObjectMotion`'s `rnd_xz` makes the Barracuda and the CM08 tanker jump

**Goal.** The Barracuda drives smoothly into the bay with no single-frame snap and stands facing
the bay opening; the CM08 tanker sits where its `ObjectMotion` puts it, with no jump.

**Evidence (confidence: traced for the Barracuda, lead-only for the tanker).** `sub_movement`'s
three `ObjectMotion` events all carry `rnd_xz = (8.742278e-08, 0, 1.0)`, exactly the normalized
`initial`/`delta` direction; `MotionRuntime` adds `RandSym() * rnd_xz` to each start velocity, up to
±44 m of accumulated travel, which the closing absolute `ObjectMotionFromTo` snaps away in one
frame. The travel with `delta` read as acceleration lands within 1.2 m of that placement, so the
`delta` semantics are right. At the controls the hull also faces the wrong way for the bay; the def
carries no rotation term and the gamez transform is `Initial`, so the heading disproof stands on
the def alone and the report stands over it. The tanker (`freighter-freighterwavemotion.json`, two
looped `ObjectMotion` events, and `Russian`'s net trailer at `tanker@node8`) shows the same jump;
`rnd_xz` or a follower write (`BL-531`'s waypoint-0 snap) are the two leads.
Re-verified against the code: `MotionRuntime.Create`'s `translation` branch added
`RandSym() * rnd_xz` per axis, and the anim lab (`--anim-lab --chapter=C3 --mission=M03
--play-anim=sub_movement --debug-anim`, seed 1) logged the drive ending at z = -11480.0 before the
closing `ObjectMotionFromTo` snapped it to -11516.3, a 36 m jump. The tanker half is not what the
entry says: `freighterwavemotion` authors two `ObjectMotionFromTo` loops (a ±1 m bob and a ±0.5°
roll, both in sequences named `roughsea`), `freightercruise` drives the parent `freighter` by SI
script, and no `ObjectMotion` event in C1B/M03 names `tanker` or `freighter` at all; the mission
census's `ObjectMotion×1` is elsewhere.

**Approach.** Settle in `docs/org/objectMotion.md`'s routine whether `rnd_xz` is a random spread
or a cached unit direction the original reads back; the change belongs to `ObjectMotion` as a
whole, not the submarine. For the heading, compare the hull's local -Z against the bay opening and
the `bauda_aip*` path direction in the built world, not the def. For the tanker,
`--anim-lab --node=tanker` on C1B, play `freightercruise`/`freighterwavemotion`, log the pose at
each event boundary; if the discontinuity is `rnd_xz` it is the same fix, if a follower writes the
pose, exclude a node an `ObjectMotion` owns from the follower.

**Decode.** `rnd_xz` is not a spread. The parser's `TRANSLATION` block (`FUN_00508590`, the
`00508d27` flag set) reads `azimuth elevation speed delta` and compiles the launch exactly as the
ranged branch does: the direction (float `cos 90°`, 0, `sin 90°` on the Barracuda) goes to the
event's `+0x70`/`+0x74`/`+0x78` direction cache, `direction × speed` to `+0x40`…`+0x48` and
`direction × delta` to `+0x4c`…`+0x54`. The update's flag-`0x4` branch (`FUN_004e8fa0`) copies
those six floats into the live slots and calls no random source; the only reader of the cache is
the tumble. mech3ax's `ObjectMotionNgC` places `trans_rnd_xz` at struct offset 100, which is the
event's `+0x70`, so the extractor's `rnd_xz` IS the direction cache. Two consequences landed in
`MotionRuntime`: the vector form draws nothing, and its tumble turns about `rnd_xz` (the 495
vector-form tumbles were read as inert because the cache was thought unfilled). Ownership: an
`ObjectMotion` owns its target's transform channel from creation to its run time, and a second
motion on the same node's channel evicts it (`MotionSet.Add`); nothing outside `MotionSet` writes a
node an `ObjectMotion` drives. The tanker never has one.

**Model recommendation.** The heading half of `BL-512` is not settled by this decode and stays with
the sortie: in the built world the hull's node carries a 180° yaw (its nose is world +Z), the drive
runs +Z 1680 m and the `bauda_aip*` take-off path runs local -Z, i.e. world +Z over the bow, so
the data is self-consistent and the top-down shot at the end of the drive shows the hull mid-channel
pointing along it. What "the bay opening" is on this map was not identified from the node table
(no named bay or dock node within 800 m of the placement), so the sortie judges it.

**Verify.** `launch-direction-cache` (new) asserts two consecutive vector-form bodies fly the
identical path and end exactly at `initial × run_time`, and that a longer third triple moves the
body not one metre; `forward-rotation` now asserts the vector form tumbles about its compiled
direction and holds when that is zero. The anim lab drive log (seed 1) ends the cruise at the
authored 1.2 m residual after the fix. The tanker was measured on the campaign path
(`--campaign=<copy>:7 --debug-anim`, 190 s of sim covering the cruise and the aground hand-over):
1,868 one-second samples of the tanker's world position with no step above 15 m, and the `tanker`
node's own pose holding y = 0 under its ±0.5° roll. The bob never plays because the roll's
`ObjectMotionFromTo` evicts it on the same channel each loop, which is a separate, smaller finding
and not a jump.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not special-case the submarine or the tanker; do not "fix" the 1.2 m residual,
which is authored. The cargo-crane choreography between the Pandora and the tanker is untested
and belongs to this item's check.

## A6 ☑ `BL-522` + `BL-527`: a surface generator's launch does not fly its take-off run, so the fighters die on the deck

**Goal.** An aircraft launched from a surface host (`barracuda`, `eairg31`, `eairg32`, and CM09's
`eag31`/`eag32`) flies its `<base>_aip*` path as a take-off run from rest and joins its patrol net
at the last point, without the AI ram rule killing it on its own host.

**Evidence (confidence: traced).** The launch pose is decoded and landed
(`docs/formats/mission-entities/enemy-generators.md` "Launching from a surface host"). The
original keeps the path on the aircraft at `+0xc8` with the flag at `+0xcc` and flies the remaining
points, which also suppresses the net-nearest-node snap (`FUN_004b0f40`); CSVM hands the aircraft
straight to its patrol net. The sortie log: `britpeace_eg0..3` each destroyed on the launch frame
(`AI ram into sub_doors/col — destroyed outright`, `sub_runway/col`, terrain `g627/col`), and in
CM07 `blakepeace_2_eg1..eg4` each `AI ram into a5/col` at about `(-5940,165,-4164)`, `spd=104
m/s`, a structure beside `eag31`'s path. The wrecks' crash damage is what destroys the Barracuda
(`BL-515`). Re-verified against the code: `AiGeneratorRuntime.Spawn` activated the launch at the
pose and handed it straight to the net, and the AI ram rule killed it on the deck on the launch
frame, as reported.

**Approach.** Decode the consumer of `+0xc8`/`+0xcc` on the aircraft record and fly the path,
releasing to the patrol net at its last point. While the run is unbuilt, a launched aircraft
standing on its own host's colliders must not count as a ram; that interim rule is the fallback
if the decode does not fit this run. This is the fix for `BL-527`; no `CampaignRoster.cs` change.

**Decode.** The consumer is the scripted-path follower already ported for the roster taxi:
`FUN_00489ea0` reads the launch's `+0xcc` and runs `FUN_0048a110` over the generator's path at
`+0xc8` from the leg index `+0xd0` (the same fields the roster spawner fills, only with the
generator's `+0x24` path and no freeze), and `FUN_004b0f40` skips its net-nearest-node snap while
`+0xcc` is set. Two details the roster port had not needed: on reaching the last waypoint of an
aircraft-class run the follower adds 1.0 m/s to the velocity's Y before clearing the flag, and it
clears the flag and nothing else, so the aircraft drops into the flight model where it is, with
no re-activation and therefore no spawn grace. The decode fits this run; the interim ram exemption
was not needed.

**Landed.** `AiGeneratorRuntime.Spawn` starts a `TakeOffRun` after the launch pose: the aircraft
is held (`FlightController.Held`, no flight integration and no collision, so standing on its own
host's colliders is never a ram) and driven by `PathFollower` over the path nodes' live positions,
nose down the current leg with the leg's climb as pitch; at the last point
`FlightController.ReleaseHeld` (new) hands it to the flight model at the run's speed plus the
decoded 1 m/s upward, lever at the decoded 1.0, net reseated where it stands. Runs step before the
cycles so a launch first moves on the next step. `--wake-generators` (new, `docs/cli.md`) is the
headless wake: a `--campaign=` mission's script-gated generators receive the sum of the script's
`WAKEUP_GENERATOR` credits at build, logged, so a `--screenshot= --frames=3600` run launches on
the generator's own period. New `generator-takeoff-run` suite over C1/M02's `eairg31`; the
launch-pose suite (`campaign-submarine`) unchanged.

**Model recommendation.** None needed: the run is the decoded follower and its constants.

**Verify.** Headless CM07 (`--campaign=<copy>:6 --wake-generators --screenshot= --frames=3600`):
five launches, every one flies its run and completes it (`egen: ... completes its take-off run at
(-5805,161,-4130), 27.2 m/s` off `eairg31`, `(-5849,161,-4014), 27.1 m/s` off `eairg32`), and no
`AI ram` names a host collider. What remains is after the hand-off and is the AI mode machine's:
`eg2` joins its net and pursues (`patrol -> pursue (target at 1817 m)`), `eg1` likewise before it
dies, while `eg0`, `eg1` and `eg3` end within two seconds of the hand-off with a wing into the
apron (`AI ram into a5/col` / `a3/col`, `surface=8/airstrip`, `pos` 2 to 3 m above the strip,
100 m/s), the first turn toward the net node flown at ground level. That is `BL-523`'s machine
(the patrol law banks at once, the flight model reaches 100 m/s inside 170 m) and is handed
there; see this item's report. Headless CM04 (`:3`) is not a fair judge: with the generators
woken at build the Barracuda has not driven into the bay (`sub_movement` is the patrol phase's),
so the four launches run their deck path 35 m under the sea surface and dive on release; the run
itself completes on every launch (`completes its take-off run at (-12032,-31,-13080), 22.9 m/s`)
and no launch rams `sub_doors`/`sub_runway`. Judge CM04 at the controls (D31).

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** The zeppelin launch-altitude gate is an airship rule, not a general one; do not lift
the launch by borrowing it, do not add a spawn-height offset beyond the decoded 0.2 m, and do not
give the aircraft a starting speed (the zero velocity is decoded). No blanket spawn lift
(`BL-457`).

## A7 ☑ `BL-569` + `BL-579`: a cutscene called from a start anim fires at bootstrap with no camera (CM04 and CM09)

**Goal.** CM04 opens on `cgzep_camera`'s view of the cargo zeppelin going down, and CM09 opens on
`mission_intro_animation`'s hangar scene followed by the player and wingmen flying down to the
start point; both hold the world and both skip.

**Evidence (confidence: traced to the bootstrap timing, mechanism lead-only).** CM04's
`NEW_GAME_START` runs `calldestroy_the_cargozep`, calling `destroy_the_cargozep` and `cgzep_camera`
(`player-cgzep_camera.json`, objects `player`, `cockpit1`, `camera1`); the log has no cutscene line
and `one-shot SOUND 'snd_IntrosceneHAch4' positioned by out-of-tree ancestor composition ... (world
root not parented at bootstrap)` says the chain fired during bootstrap. CM09's
`mission_intro_animation` (`camera1-mission_intro_animation.json`, root `camera1`, with `player`,
`piratezep`, the hangar doors and five `bullet*` path nodes) is in the start list, has no hold or
handoff line, and its `snd_scene1` fires at `(0,0,0)` at bootstrap. `generic_intro` is not in either
start list, so the path `BL-548`/`BL-583` exercise is never entered. CM09's list also names
`pure_panic`, a C1/M02 hangar def this mission does not compile.
Re-verified against the code and two headless probes, and the two halves split. CM04 (traced to
code): `CutsceneController.Hosts` answered only for `IntroAnims` plus the landings/`cutscenes\`
closure, `cgzep_camera` is in neither, and `WorldSession.BootstrapsCutscene` read the start LIST
rather than its call closure, so C3/M03 built no `player` marker; the probe's census shows
`Callback(20)×1, Callback(11)×1, Callback(14)×1, Callback(913)×1, Callback(2)×1` counted as
"not yet acted on", the HUD up and the cockpit view. CM09 (disproved as a code defect): the
headless probe prints `cutscene: 'mission_intro_animation' has the session`, the letterbox is up
at 10 s over the pirate zeppelin's start pose and at 50 s the player's Bloodhawk is flying down
over the water on `playerthruclouds`. The sortie log carried no cutscene line because the
`cutscene:` lines are `GD.Print` and the file sink takes `Log.*` only, so their absence was not
evidence. `snd_scene1` at `(0, 0, 0)` is the at-node's pose being composed before the world root
is in the tree at bootstrap, a sound-position artefact and not a scene-timing one; CM04's
`snd_IntrosceneHAch4` composes to `cargozep1`'s real position the same way. `pure_panic` is
defined only by C1/M02's `hangar_panic.zrd`, which M04's `mis_anim.zrd` does not list, and the
same dead name sits in C1/IA1's and C1/MP1's `startanims.zrd`; the original's loader finds no
definition and plays nothing.

**Approach.** Settle how the original runs a `camera1`-object cutscene called from a start anim
(the registration sites `docs/formats/anim-definitions/cutscenes.md` lists) and route both through
the cutscene runner with the world held. Run CM09 headless with `--debug-anim` and read the def's
event log against the def. Whether the original plays `pure_panic` in CM09 is part of the question.

**Model recommendation.** Host by name, and read the start list's closure. The decode already on
record (`FUN_0046c370` starts each start anim with no `FUN_004ee160` host, and the new-mission
start enters the cutscene state imperatively) covers a called definition the same as a listed one,
so `cgzep_camera` joins `CutsceneController.IntroAnims` beside the two intros, and
`WorldSession.BootstrapsCutscene` walks `program.Subset(program.StartAnims).Defs` instead of the
list, which is what stands up `camera1`, the bars and the `player` marker for C3/M03 before the
bind. No code-based rule: `player_setup` authors the same nine codes on a `camera1` root. The
Ghidra bridge exposed no decompile tool in this session, so nothing beyond the existing decode
was re-read in `crimson.exe`.

**Verify.** Headless CM04 and CM09 opens show a cutscene hold and handoff line for the named def,
the one-shot sounds positioned in-tree, and the skip working; the `campaign-*` cutscene suites
unchanged. `campaign-cutscene` extended with `OpeningSceneCalledFromStartAnim`: over C3/M03's
built world with cutscene roots, the `player` marker and `camera1` exist, playing
`calldestroy_the_cargozep` hands the session to `cgzep_camera` with 20 first, the world held, the
player out of flight, the AI parked, the chrome off and a skip armed; `destroy_the_cargozep` is in
that same call closure, so the destruction has one call to play from; the skip ends the scene.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** The cargo zeppelin's destruction already runs (fireballs, `tntbox`/`gasbag`
deactivation); do not run it a second time under the camera. `BL-548`'s deferred `--pos=` handoff
is for `generic_intro`; these intros are their own defs.

# Wave B — CM06 (C1C/M01) and CM07 (C1/M02)

## B11 ☑ `BL-571`: a carried turret's death fire, and the turret, stay where the turret died

**Goal.** A gun ring shot off the Workers' Voyage burns and stays on the hull as it flies on.

**Evidence (confidence: traced for the fire, decoded for the turret).** The ring's destroy def
calls `large_30sec_fire` `WithNode doublecannon4 (0, 2, 0)`. `AnimRuntime`'s `CallAnimation` hands
a death's effect call to the world-effects runtime through `ExternalEffect` with a world POSITION
snapshot (`VisualOriginOf(callAnchor) + basis * offset`), and `PlayEffectAt` stages the template
root at that point with `TopLevel = true`; the site node rides along only as the callee's
`INPUT_NODE`. The log's `WAIT_FOR_COMPLETION on 'large_30sec_fire' not held — the callee is routed
to the world-effects runtime` is that hand-off. `BL-514`'s disproof examined the `PUFFER_STATE`
path, which does re-read the host; the death-call path is this one. Re-verified open against the
code: the fire's emitter host is the placed `fire_here` copy's own root, and nothing moved that
root after placement. The turret half is the same def read to its end: `destroy_dbl_cannon`
switches `healthy`, `bs_canup4`, `bs_cbase4` and `bs_candown4` OFF at t=0 and switches no
destroyed role on, so no turret model is placed anywhere. What stands at the death point is
`dblcannon_flying_parts` (`AT_NODE doublecannon4`, a library-root copy of `zep_can_dstry1.flt`
whose eight parts fly 0.4 to 4 s and hide, the `nulled-launch` suite) plus the `AT_NODE`
fireballs and smokeball, all authored as a snapshot at the point of death.

**Approach.** The data's two spellings settle the rule (`docs/org/sequences.md`: `WITH_NODE`
delivers the live node, `AT_NODE` a position taken once). `CallTargetSite` now reports the
spelling, `ExternalEffect` carries it, and `PlayEffectAt` places a `WITH_NODE` copy through
`TemplateStage.PlaceFollowing`, which re-places the root each frame at the site's live pose plus
the offset in the site's own frame (`FollowSites`, from `Advance` before `EmitterDirector.Tick`).
`AT_NODE` keeps `PlaceOn`; a world-fixed site reads identically under both. No reparenting: the
pooled copy stays the stage's own root and `TopLevel`, so the pool, prewarm keys and the reveal
ritual are untouched; a re-placement, a hide or a freed site ends the follow.

**Model recommendation.** medium.

**Verify.** `turret-death-fire-follows-hull`: a real C1/M04 piratezep `doublecannon4` ring killed
through `DamageAt` with `ExternalEffect` wired to a real `fire_here` effects stage over a
`CountingEmitterFactory`, the hull moved 500 m and yawed 35° by one `GlobalTransform` write
(`ZeppelinRuntime.Place`'s shape), the `fire_n_smoke` emitter's fed position asserted to move by
the ring's displacement. Seen FAILING with the follow flag forced off and PASSING with it on.
`TemplateStageTests` pin `PlaceFollowing`/`FollowSites` off-engine. The twelve effect and
zeppelin suites around it stay green; the 16 goldens are hash-identical.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** `trail-world-anchor` settled the opposite case (an emitter that must NOT ride its
host); keep both. Do not reopen `BL-514`'s `PUFFER_STATE` reading.

## B12 ☑ `BL-572`: an objective marker labels the raw node name instead of the original's verb and proper name

**Goal.** An objective marker reads as the original's: a category, an action verb in brackets, the
target's proper name, coloured by the action (`Zeppelin [Disable] Worker's Voyage` in red, `[Dock]
Worker's Voyage Docking Hook` in blue), with the node name kept for the debug tag only.

**Evidence (confidence: traced to the data and the reader path).** The log's `targeting hud: P1
brackets on peoplehook at 999 m` lines show `TargetRef.DisplayName` carrying the node name,
`TargetHud.LabelLines` drawing it, and `HudGreen` applying as to any friendly. The strings exist in
`extracted/messages.json` (`MSG_OBJ_DOCK` 8003, `MSG_OBJ_DISABLE` 8006, `MSG_OBJ_WVOYAGE` 8025,
`MSG_OBJ_WVOYAGEHOOK` 8027, `MSG_OBJ_KLONDIKEHOOK` 8017). Re-verified against the code: the maps
are not in any record or binary table; they are `targets.zrd`, and C1C/M01 is the one mission of
53 that ships none of its own. `MissionTargets.Load(state.MissionZrdrPath)` read the mission scope
alone, found nothing, and every site fell back to its node name with no category, which is the
team-colour rule. `C1C/zrdr/targets.zrd` (chapter scope, the only such copy) carries all three:
`workersvoyagezep` → `MSG_OBJ_WVOYAGE` / `MSG_OBJ_ZEPPELIN` / `MSG_OBJ_DISABLE`,
`[wv_tailhook, peoplehook]` → `MSG_OBJ_WVOYAGEHOOK` / `MSG_OBJ_DOCK`, `pzhookpoint` →
`MSG_OBJ_KLONDIKEHOOK` / `MSG_OBJ_DOCK`. A second defect sat under it: `MissionTargets` skipped a
nested `nodes` entry (`[[wv_tailhook, peoplehook]]`) outright, so the hook would have stayed a
node name even from the right file.

**Approach.** Decoded rather than a new display line: `targets.zrd` is opened through the reader
search path (`FUN_004a2be0` → `FUN_00579c60` → `FUN_00579710`), which `init.gw` sets as
`common\zrdr`, `<chapter>\zrdr`, `<chapter>\zrdr\nets`, `<chapter>\<mission>\zrdr`
(`RdrSetPath`/`RdrAddPath`), one file whole. `MissionTargets.Load(mission, chapter)` is that path
(mission, else chapter), a nested `nodes` entry keys `parent/child` like `ObjectiveTarget.Key`,
`ObjectiveSites.SiteFor` looks the whole key up first and the last node second, and `GameSession`
passes the chapter zrdr. The marker colour was already `Target::GetColor`'s decoded rule in
`TargetHud.MarkerColor` (Disable red, Dock blue); it only ever saw a null category. `TargetRef`
and `TargetHud.LabelLines` needed no change: the format is the original's three format strings
already ported (`Zeppelin [Disable] -` over `Worker's Voyage`), the recollection's missing ` -`
being the original's own trailing dash.

**Model recommendation.** Fix what the decode names and nothing else: the reader search path is a
data-location rule, so it lives in `MissionTargets` beside the file it opens, not in
`ObjectiveSites`, which keeps reading one table.

**Verify.** `campaign-objective-labels` (new, C1C/M01's built world): the mission table loads 0
entries, the chapter's 5; the three targets label `Zeppelin [Disable] -` / `Worker's Voyage` red,
`[Dock] -` / `Worker's Voyage Docking Hook` blue, `[Dock] -` / `Pandora Docking Hook` blue, each
keeping its key as `--target=` identity. `MissionTargetsTests` pins the nested key and the
mission-else-chapter load. Plus the flown check in D31.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** `BL-397` is the marker's bracket range rule and not this. The format is a
recollection until decoded; do not build the exact layout from it.

## B13 ☑ `BL-573`: the AA guns damage themselves firing at a barrier

**Goal.** CM07's AA guns do what the original's do when a structure blocks their line: they do not
blow themselves up.

**Evidence (confidence: traced).** Traced in the `turret-self-fire` suite on the C1/M02 world: an
emplacement's round carried no owner at all (`NoShooter`, which excludes nothing), so a flak
striking the fort's barrier beside `aagun3x` dealt the gun `-10` (its own body struck by the ray)
and `-9.64` (its burst's splash on a second collider of the same destructible), and a burst 12 m
over its own pit dealt it four splash shares at once, `-8.18/-8.04/-8/-7.36`, one per collider body
of the destructible. That is the sortie's pattern. The IA1 world reproduces none of it because the
aagun destructibles carry no colliders there; the mission world is what has to be built. The
line-of-fire gate is not the cause: the guns fire only when the world ray to the target is clear,
and the strike lands on the barrier because the target is low behind it, which the original's own
LOS test (player-scoped, cached 1 to 2 s) would allow just as well.

**Approach.** Decode rule applied: the original clears the round's owner node's intersect bit for
the whole splash gather (`FUN_005aca30`, `docs/org/ordnanceTypes.md` "Half two, the splash"), so a
shooter never takes its own burst. An emplacement's round now rides `ProjectilePool.Spawn`'s
`ownerBodies`, its own mounting section (`PlatformColliderRids`, the set its line-of-sight ray
already excludes), out of both the hit ray and the splash gather. Nothing else is exempt: a
neighbour's burst or a rocket into the pit still kills the gun. Which node the original names as
an emplacement round's owner was not read this session (the bridge's dynamic decompile tools were
not callable from this harness); the mounting section is the smallest set that separates a gun
from its neighbours.

**Model recommendation.** Routine.

**Verify.** `turret-self-fire`: C1/M02, each of the five aaguns woken alone, a hostile plane parked
low on eight bearings plus one strafing pose over the pit, zero self-attributed hits across the
sweep (was 12 on the unfixed build), then the able-to-fail pair on one pit: a flak owned by the
neighbouring gun dropped into the pit damages it, the same flak owned by the pit's own gun deals
nothing. `world-turrets`, `carried-turrets`, `blast-neighbor-shape`, `blast-curve-cover-cap` and the
units stay green.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not exclude turrets from splash wholesale; a rocket into a gun pit must still
kill it. One burst still deals a destructible one share per collider body it carries (the aagun
has ten), where the original's hit buffer holds one entry per node; that over-count is not this
item's and is left for its own entry.

## B14 ☑ `BL-574` + `BL-575`: the hangar hand-over gives a stock Bloodhawk and plays no animation

**Goal.** CM07's hangar cutscene animates (doors, lift, the drop) and hands the player the Blue
Streak build (engine 4 nitrous, twin 40 and twin 30 guns, 1/1 hardpoints) in the livery the
original draws.

**Evidence (confidence: decoded for the build and the fork, the livery open).** Case `0x3c5` of
`FUN_0047e080` is `FUN_0047fd50(pbloodhawk, player_bhawk)` followed by the Blue Streak's tables
written by hand: `DAT_0062ae28` 40/30 with both twin bytes at `DAT_0062ae58/59`, two hardpoints
of six, `FUN_0047bd90` 20 armour on all four sections, and `player+0x946` the injector, which is
the airframe-3 template at `0x0061a9b8` field for field. CSVM's swap assembled the stock fit
because `AirframeSwapRequest` carried no build. The livery is not in the executable:
`pbloodhawk` authors no `paint_pattern`, and `FUN_0047c210` skips the scheme record when the
pattern string is empty (`0x0047db0c`), so the `player_fortune` branch that copies the launched
plane's scheme from `0x0071db08` (written by `FUN_00417090` off the profile's selected plane) is
never reached; what the original draws is whatever state the airframe's key textures are in.
The animation half was a parser gap, not a superseded def or a reparented node: `hangar_drop`
calls `hdplayer1` and `hdplayer1b` (and `hdchute1`/`hdchute1b`) together, and each carries an
`ACTIVATION_PREREQUISITE` on `hdrop_direction`, one INACTIVE and its twin ACTIVE, so the
direction sensor picks one camera leg. `CompiledAnim` only read the `{"Animation"}` prerequisite
shape and dropped the `Parent`/`Object` node form, and nothing enforced it, so both legs ran two
SI scripts on `player` and `camera1` at once (the polish-4 D-item's wiring contract, now landed).

**Approach.** Parse the node-state prerequisite on both front-ends into
`AnimDefinition.PrereqNodes` and refuse `AnimRuntime.Start` while a REQUIRED entry is unmet;
give `AirframeSwapCode` an `AwardAirframe` (965 is 3) that `FlightRoster.RunSwap` resolves through
`CampaignProgression.AwardBuild` into the swap request's `Build`, which `HumanFlightAdapter`
assembles by the custom-plane path (engine override, injector, guns, pylons, armour), and a
`LiveryDef` (`blakebloodhawk`) whose `PaintScheme.ForDef` scheme the rebuild wears.

**Model recommendation.** Blake Aviation for the livery: the case sends no scheme of its own,
the hangar is Blake's, and it is the livery seen at the controls; switching it is the one
`AirframeSwapCodes.BlueStreakLiveryDef` constant if D31 says otherwise. The prerequisite gate
enforces REQUIRED entries only; optional entries and `MINIMUM_TO_SATISFY` over nodes are parsed
and left to the zeppelin runtime's own reading.

**Verify.** `campaign-hangar-handover` (new) plays `hangar_drop` over C1/M02's built world on a
realtime clock: the two legs carry opposite prerequisites, exactly one of each pair starts and it
is the one the sensor's state picks, `hdplayer2`/`hdplayer3` start, the swap lands at 4.38 s on
`pbloodhawk` with the injector, 40x2 over 30x2, two pylons, 20 armour a zone and the `blake`
scheme, and a plain swap onto the same node stays stock with no injector. Foreground:
`campaign-hangar-handover` PASS; `campaign-airframe-swap`, `campaign-cutscene-skip`,
`campaign-cutscene`, `landings-hangar-drop-gate`, `landings-hookup-airframe`,
`dropoff-chuteman-stage`, `dropoff-placement`, `intro-aircraft-stage`, `zeppelin-damage`,
`campaign-zeppelins`, `campaign-wingwalk-camera`, `landings-wingwalk-gate`, `cutscene-letterbox`
13/13 PASS; units 2518/2518. A headless CM07 flight to the hangar cannot arm the drop by itself
(`hangar3_doors` is called by the fuel depot's death in the chapter's `fueltruck.zrd`), so the
runtime path is the suite's. D31 judges the livery and the drop at the controls.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not give the stock Bloodhawk nitro, and do not touch the post-mission grant
(`BL-528`), which is correct. The swap itself works and must stay.

## B15 ☑ `BL-526`: the rope ladder never deploys (a native per-mission switch to decode)

**Goal.** The decode of the original's ladder switch is recorded under `docs/org/` far enough
that a CSVM equivalent can be built, and if it fits this run, the ladder deploys in CM07 when the
player is level and close.

**Evidence (confidence: traced in the decompile).** `drop_ladder`'s one xref is inside
`FUN_004735b0`, which is the ordinary per-mission initialiser (`FUN_00464680` calls it for every
mission under `StructsMissionInit`), not a C1/M02-only function; only the authored data is
mission-specific. It resolves `drop_ladder`/`retract_ladder` into a 20-byte heap object
(`DAT_0071c324`, stored at `0x00475517`) whose state word is 0 retracted, 1 deployed, 2 deploying,
3 retracting, and registers the object as both definitions' `CALLBACK` host (`FUN_004ee160`); the
vtable thunks at `0x00445660`/`0x00445680` accept code 123 only and write state 1 / state 0. The
world tick tail (`0x00489dc3`-`0x00489e21`) runs outside a cutscene with the player alive:
`player+0x190` is row 1, Y of the orientation matrix at `+0x180`, the aircraft's own up axis
dotted with world up (`flightModel.md` "Bank coupling"), so `0.707 <` it is "within 45 degrees of
upright" on bank and pitch together. `FUN_00471690` walks the global list at `0x0071c268` of
`{node, radius²}` entries, passing when the node is active (`+0x24 & 4`) and the player's position
is within the radius; `FUN_00471830` fills that list from `pickups.zrd` at mission load, and
nothing else in the image reads it. Deploy (`FUN_004455e0`) starts the drop only from state 0,
retract (`FUN_00445620`) only from state 1, and a transient state holds until the definition's
own `CALLBACK 123` lands it. Full decode: `docs/org/ladderSwitch.md`.

**Landed.** `LadderSwitch` (`src/Session/LadderSwitch.cs`) is the rule and the state machine,
engine-free; `LadderSwitchRuntime` (`src/Session/LadderSwitchRuntime.cs`) flies it against the
built world every frame, off the same `pickups.zrd` sensors the landings trigger already loads,
starts the definitions as mission triggers and takes the runtime's `CALLBACK` host slot, chaining
to the cutscene host. `GameSession` builds and binds it beside `LandingApproachRuntime`. Not
built: the `ladder_roll` counter-rotation that keeps the ladder plumb under a pitched or rolled
aircraft (recorded on the page under "Not built"). Unit tests `LadderSwitchTests` cover the
attitude gate, the sensor sphere, the transition order, the silent flip without definitions and
the settle callback.

**Model recommendation.** medium.

**Verify.** `LadderSwitchTests` (5). Flown: in CM07, level within 100 m of the caboose's
`ladder_pickup_sensor` after `trigger_copilot` stages it, the ladder drops (`ladder: started
'drop_ladder'` in the log); banked past 45 degrees or outside the sphere once deployed, it
retracts. Judged on D31's flown pickup.
**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** `BL-035`'s dropped kinds play no role; do not build this as an anim event. Do not
teach the cutscene host code 123: it is the switch's settle signal, and the switch answers it
first.

# Wave C — CM08 (C1B/M03) and CM09 (C1/M04)

## C21 ☑ `BL-576`: the Pandora pitches steeply up and down along the Klondike net

**Goal.** The Pandora follows `Klondike1`'s altitude steps the way the original steers pitch for
route following, not at 30° nose-down toward every lower node.

**Evidence (confidence: decoded and reproduced).** The screenshot
`Screenshots/crimsonskies_2026-08-27_23-47-46-050.png` (run-4 worktree) shows `piratezep` nose down
about 30° along the green `Klondike1` segment. Still open against the code, and the net was never
the cause: `Klondike1`'s legs are level or at most 6.3° (the altitude swing is 400 to 93 m over
seven legs of 550 to 1200 m). A `ZeppelinMotion` trace along the real net reproduced the report
exactly: the pre-fix turn law asked for `error/dt` as its rate and reached it through
`accel_pitch` 0.5°/s², an undamped second-order system that rang up within 60 s into a standing
±30° pitch swing (period about 40 s) on the level legs, pinned at the band's edges, and kept
swinging at speed 0 on the dead-end hold.

**Approach.** Decoded `FUN_004bf9d0`'s pitch term: the original steers pitch at the node's
altitude difference directly (`atan2(dy, horizontal)` from the hull to the current node), with no
smaller authored limit, no easing over the edge length and no altitude field on the net. The
difference is the steer routine (`FUN_004bf530`, yaw's `FUN_004bf620` is the same code): the rate
asked for is `max_rate` eased to `max_rate·(error/25°)²` inside 25° of error, reached at
`accel_*`, and the angle advances by that rate times `speed/max_speed`. The record's ±30 band is
a degree-valued pair compared against radians, so it bites nowhere (load clamp, pose write
`FUN_004bf950`, repick `FUN_004c0b50`), and the per-step clamp the remake carried was its own
invention. `ZeppelinMotion.Steer` is now the decoded routine for both axes, the band clamp is
gone, and `FUN_004bf360`'s inside-30 m dock glide (pose and heading decay onto the node at
`e^(−0.2·dt)`) is `Dock`, which closes the last metre onto the follower's hold sphere the dive
used to close by accident. `ZeppelinMotion.ResumeAt` replaces A4's `SeatedAt` copy.

**Model recommendation.** The decoded steer law verbatim, including the way-on factor and the
absence of a pitch band; a zeppelin's only pitch limit is the slope of its route.

**Verify.** Headless CM08 on a profile copy (`--campaign=<copy>:7 --debug-ainets=Klondike1
--screenshot= --frames=7200`): the Pandora's ten-second `zep:` lines read pitch −0.2° to −1.5°
across the two legs to the armed stop at node 5 and −1.9° in the ramp-down, against the pre-fix
±30° swing. The whole released chain in `ZeppelinMotion` alone reads −2.1° to +6.1° over 400 s
(the steepest leg is 6.3°) and holds node 0 at −1.4°. `ZeppelinMotionTests` pins the ease curve,
the accel ramp, the way-on freeze, the 45° slope followed without overshoot or band, a
Klondike-shaped level route staying under 8°, and `ResumeAt`; `zeppelin-pandora-dead-end` now
asserts the pitch never exceeds the steepest leg's slope over the real net.
**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** The initial-pitch clamp that never fires (`ZeppelinMotion.cs`) is decoded verbatim
and stays. Do not flatten the net. Do not reintroduce a per-step pitch band as a safety net: the
original has none, and the ease is what keeps the pitch on the slope.

## C22 ☑ `BL-577` + `BL-564`: no runtime for a roster or generator surface vehicle, so the patrol boats never spawn

**Goal.** CM08's `patrolboat_1..4` spawn on their nets at water height, drive them, take damage and
die as their `patrolboat-*` defs expect; CM12's `eshipg31` launches patrol boats at the pirate
ship, not Bloodhawks at the world origin.

**Evidence (confidence: traced).** `aiv.zrd` carries four enabled `patrolboat_1..4` blocks at
`y = 0` on nets `Patrolboat1a..4a` (the `a` variants; OBJECTIVE10's `SET_AI_NET` later moves them
to `Patrolboat1..4`); `objectives.zrd` wakes them (line 164) and moves them (241 to 253).
`CampaignRosterPlan.Build` reported a surface-vehicle block in `Skipped` (`'{def}' ({mode}) has
no player airframe`) and never spawned it. In CM12 `Eshipg31_params` resolves to
`patrolboat_eg0`, which after C23 was the counted empty launch (nothing built, no
`player_bhawk`). Re-verified against the code: the roster half was open as described; the
origin half of `BL-564` was already gone, since A6's take-off path places every surface launch
on `esg31_aip0` (see the decode below).

**Approach.** Landed. `Session/SurfaceVehicleRuntime.cs` builds a `mode ship` block as a copy of
the chapter's library-root hull (`SceneBuilder.BuildSubtree`, colliders included) at the block's
spot with its height read off the water by a probe carried past the host's own deck, and
indexes it through the new `AnimRuntime.IndexSpawnedCopy` (by name, never by compiled index,
since four copies share one index space), which anchors the chapter's `patrolboat` reader
definitions on every copy and registers each hull's destructible pool (`HEALTH 20`).
`Session/SurfaceVehicle.cs` drives the hull with `PathFollower`, the scripted-path law, over an
unbounded route (a generator's take-off run, then a walk of the net's edges) so the aircraft
final-leg climb-out never comes, with the height pinned to the water; `deactivated` builds it
hidden, dormant and frozen until `WAKEUP_ENEMIES`, whose wake plays the def's `start_anims`
(`emit_ptsplash1/2` on `pt_emitter1/2`); `injure_anims` play once per rung as the pool falls;
the pool reaching zero raises `Destroyed` once and stops the hull, the death sequence owning its
parts. `CampaignRosterPlan` plans a `Surface` hull for an airframe-less `ship` def (`PlaneNode` is
the def), `SpawnFor` refuses it, and `ResolveGeneratorLaunch` returns the fourth kind
`GeneratorLaunch.Surface`; `CampaignDirector.PlaceSurface` spawns it through
`RosterInputs.SpawnSurface`, keeps it in `Vessels`, and `WAKEUP_ENEMIES`, `SET_AI_NET`,
`SET_AI_TEAM`, `DEDG` and the group `TRAVELERS` all count or command a hull (a generator's
launch through `WorldInputs.SurfaceVehicles`). `AiGeneratorRuntime`'s spawn callback now returns
`LaunchedVehicle` (aircraft, hull or neither; a bare `FlightController` converts), and a hull is
booked like an aircraft, handed the host's path and net, and never enters `StartTakeOffRun`.
Decode (`FUN_00451bf0`): the host matrix (`FUN_004cf200`) is read only on the zeppelin branch;
a surface host always launches on waypoint 0 of its `<base>_aip` path, and the no-path branch is
unreachable past the loader's two-point drop, so a model-less host is no special case and the
bbox centre is read by nothing. `eshipg31`'s `esg31_aip0..5` carry the world translation
themselves, `(-5888.8, 0.02, -4408.0)` westward to `(-6141.7, -0.04, -4435.7)`. Not landed, and
named as such: the boat's `turret/gun/firepoint` nodes are built but not fired, since no `ai.zrd`
entry names a patrol boat and its `weapons` gunnery is the AI mode machine's (`BL-523`).

**Model recommendation.** Opus-sized as executed: the runtime sits at the meeting of five modules
(roster plan, director, generators, anim runtime indexing, scene building) and the decode had to
overturn half the item's own evidence.

**Verify.** New `campaign-surface-vehicles` suite: over C1B/M03's built world (collision on) the
four blocks build hulls at their authored spots with `y = 0` off the water probe, 9.4 km from the
origin, on `Patrolboat1a..4a`, deactivated and hidden with a 20 HP pool and their own
`pt_emitter1`; DEDG over group 3 reads 0 inert and 4 woken; a held hull does not move; the wake
starts 16 `wake_emit*` emitters over the four; 20 s of driving moves each 275 to 337 m at the
taxi speed with `y = 0` held; a hit through `AnimRuntime.DamageAt` destroys one, reported once,
stopped where it died, DEDG down to 3. Over C2/M01's world `Eshipg31_params` resolves
`GeneratorLaunch.Surface`, the credited generator builds `patrolboat_eg0` within 1 m of
`esg31_aip0` and 7.4 km from the origin (the ram-terrain check), asks for no aircraft, runs no
aircraft take-off run, and drives 289 m west in 20 s. `generator-roster-params` carries the
engine-free Surface resolution; `CampaignRosterPlanTests` the plan and the four-way resolve.
Headless CM12 (`--campaign=<copy>:11 --wake-generators --screenshot= --frames=1200`): three
`surface vehicle 'patrolboat_eg0..2' launched at (-5889,0,-4408) down 5 path point(s) onto net
'M2Patrol1'` lines, `water=-0.03`, no `player_bhawk_eg*`, no ram at the origin. Headless CM08
(`--campaign=<copy>:7 --screenshot= --frames=12600`, 210 s): the four hulls built at
`water=0` on their nets, `roster spawned 13 of 15 block(s) … 4 surface vehicle(s)`; the wake
itself does not show in that run because OBJECTIVE6 (dormant 180 s) kills OBJECTIVE8 (dormant
181 s) unless the player has destroyed the power hut first, so the headless open never reaches
`WAKEUP_ENEMIES`; the suite drives the wake directly. Run in the foreground:
`campaign-surface-vehicles`, `campaign-roster`, `generator-roster-params`,
`generator-takeoff-run`, `zeppelin-launch`, `instant-action-zeppelin`, `hangar-door-wake`,
`campaign-submarine`, `campaign-set-ai-net`, `campaign-squad-wakeup`, `campaign-objectives`,
`campaign-zeppelin-wakeup`, `scripted-path` all pass; `dotnet test` 2524/2524.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not spawn a boat as an aircraft with a low ceiling, and do not hand `eshipg31` a
fighter def. CM12's three Bloodhawks are group 3 and never count toward "Destroy all enemy
fighters"; killing them is not progress. A test world's puffer factory is retired with the build
(the harness's archive intent), so a hull woken in a suite needs `ctx.EmitterFactory`; a
session's textures outlive the build and the wake builds there. Do not read the hull's height
off its net: `Patrolboat*` nodes are a route, and a probe that stops at the first surface reads
the host ship's deck (17 m) over a launch point.

## C23 ☑ `BL-580`: `eairg32`'s launch falls back to a `player_bhawk` on a misspelt parameter block

**Goal.** A campaign generator whose `vehicle.params` label resolves to no roster block does what
the original does with it, and never invents a player-airframe launch.

**Evidence (confidence: traced).** `egen.zrd` names `Eairg32_params` (line 54) while `aiv.zrd`'s
label table spells it `Earig32_params` (slot 31), so `CampaignRosterPlan.GeneratorTemplates` has no
entry and `GameSession.SpawnFromGenerator` falls back to `SessionSpec.GeneratorsPlane`; the log
shows `player_bhawk_eg1` launched beside `blakepeace_2_eg0`. `eairg31`'s `Eairg31_params` matches.
Re-verified against the code: `SpawnFromGenerator`'s fallback branch took every miss, a null label
and an unresolved one alike.

**Approach.** Decoded. `FUN_00452450` is only the Instant Action arm (group-matched parked
airframes); the campaign launch is `FUN_00451bf0`, called from the cycle at `FUN_00452640`. The
record parser `FUN_00452850` stores the label's vehicle record at `+0x3c` and leaves it null on a
miss; the launch then spawns the generator's `vehicle.type` (`+0x44`, unauthored in every shipped
file) through `FUN_0047b650`, whose `_stricmp` walk finds no def for the empty name and returns
null. The launch still returns 1, so the cycle books it (`capacityRemaining--`, `active++`, timer
reset, wave counter, global `%s_eg%d` ordinal) and nothing is built; the `active` slot never frees,
so `eairg32` blocks itself after four. Neither "first block of the def" nor "the sibling's block"
exists in the code. CSVM matches it: `CampaignRosterPlan.ResolveGeneratorLaunch` returns
`GeneratorLaunch.Empty` for a label naming no block, `SpawnFromGenerator` returns null on it, and
`AiGeneratorRuntime.Spawn` books that null as the counted empty launch (ordinal advances, slot
stays taken) when the generator authored a label. The CLI airframe stays only for a generator with
no `params` key.

**Model recommendation.** Sonnet-sized once the decode is in hand; the decode itself needed the
three-function chain read together, which is where the "silent no-launch" versus "counted
no-launch" distinction lives.

**Verify.** The `generator-roster-params` suite carries the C1/M04 arm: `eairg32` resolves Empty
with no plan, `eairg31` resolves its block, a null label resolves the airframe. The headless CM09
check ("no `player_bhawk_eg*` launch") is covered by the same read, since the fallback branch is
no longer reachable with a label present; the sortie (D31) judges the airfield.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not "fix" the data spelling; the shipped file is the reference and the original
ran with it.

## C24 ☑ `BL-582`: a `[parent, child]` objective target is flattened into two bare names

**Goal.** `ADD_OBJECTIVE_TARGET [[piratezep, rock_zeppelin]]` marks the one `rock_zeppelin` under
`piratezep`, so CM09 shows one Defend marker; a bare name keeps today's global match.

**Evidence (confidence: traced).** `ObjectiveScript.ReadNames` flattens a nested list into its
strings, so `AddObjectiveTarget` holds `piratezep` and `rock_zeppelin` as two names, and
`ObjectiveGraph.IsObjectiveTarget(name)` matches any node by bare name: the Pandora's root and a
ground `rock_zeppelin` near the enemy zeppelin both light up. `REMOVE_OBJECTIVE_TARGET` and
`ADD_OTHER_TARGET` read the same way (`ObjectiveSites.Holds`); C1C/M01's `[[wv_tailhook,
peoplehook]]` is the same shape. Re-verified against the code: still open as described. A census
of every nested target argument across the 53 shipped `objectives.zrd` files finds only paths
(`[zcrane1, healthy]`, `[cargozep2, ctur1]`, `[tiedown01, healthy]`, C5/M01's three-deep
`[rfspt4, healthy, spprt]`), so `docs/formats/objectives.md`'s former "a list argument is a list
of independent names, not a node path" was wrong on the data and is rewritten. The binary's walk
of the list is not traced (the Ghidra decompile tool was not reachable from this lane).

**Approach.** `ObjectiveTarget` (in `ObjectiveScript.cs`): a string argument is a bare name, a
nested list is ONE path, any depth; `Key` joins the path with `/` and is what
`ObjectiveGraph.ObjectiveTargets`/`OtherTargets`/`HelpLabels`, `IsObjectiveTarget`, and every
`ObjectiveSite.Node` are keyed by, so a bare name's key is unchanged. `ObjectiveSites.ResolveTarget`
walks a path with `FindNodes` scoped to the node before; `targets.zrd` is looked up by the last
node, the help label by the whole key. `SET_HELP_LABEL` reads its first argument as the same
target. The C1/M04 world carries six `rock_zeppelin` nodes; the path picks the hull's own.

**Model recommendation.** Sonnet-sized: a parser shape change with every consumer in three files.

**Verify.** `ObjectiveGraphTests.A_nested_target_list_reads_as_one_path_and_a_bare_name_stays_bare`
(the `[[a, b]]` read, a three-deep path, the help-label target, and the graph holding `a/b` and
neither bare name), and the engine suite `campaign-objective-target-path` over C1/M04's built
world: `[[piratezep, rock_zeppelin]]` is held as one key, exactly one `rock_zeppelin` site is
offered, it stands on the node inside `piratezep`, and its category is the `MSG_OBJ_DEFEND` text.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** The help label applies to the same resolved node, not to the parent. A site's
identity string (`TargetRef.Name`, what `--target=` matches) is now the key, so a path-authored
site is addressed as `piratezep/rock_zeppelin`.

## C25 ☑ `BL-581`: the docking objective is never reached after the radio tower goes down

**Goal.** With the Promised Land destroyed, CM09 goes on to the docking whether the radio tower
fell inside `OBJECTIVE16`'s window or not; the graph's wake, nap, complete and kill transitions
appear in the sortie log.

**Evidence (confidence: chain traced, stall cause open).** The Defend marker clearing is
`OBJECTIVE25` and correct. The docking (`OBJECTIVE31`) is reached through 30 (Promised Land
destroyed, met in both flights) waking 42 (`DEDG [1, 0]`), then 43, then 44. The stalled flight is
the one where the Paladin Blake squad (`blakebloodhawk_1/2/3/8`, group 1, woken only by
`OBJECTIVE20`) never arrived, so `DEDG [1, 0]` cannot complete. With the tower down inside 16's
window the route is 15 kills 16, naps 17 (16 s), 17 naps 19 (3 s), 19 naps 20 for 90 s; 18 and 19
carry `TICK_DEPENDS_ON_OBJ 29`, and `ObjectiveGraph.Ticks` holds such an objective entirely while
29 is not `Awake` (29 is dormant until 28 wakes it). No `[campaign]` objective line exists in the
sink, so the sortie cannot settle it. Re-verified against the code and the binary: the graph half
is a disproof. `FUN_0046a490`'s nap countdown (state 2) sits behind the same awake gate as the
scan, and `FUN_0046b160` (the nap) sets state 2, zeroes the timer and clears the completed flag
regardless of the gate, so a nap landing on a gated objective is held, not dropped, and resumes
when the dependency wakes. That is what `ObjectiveGraph.Ticks` already does. Driven headless on
the shipped C1/M04 script, the tower-down route reaches 20 (the squad wake) 90 s after 19 wakes,
which is 3 s after 29 wakes, and the 30/42/43/44 chain naps 31 in once groups 1, 2 and 5 read
empty. The stall is therefore in the world seam, not the graph: `DEDG` never reading a group as
empty, or `WAKEUP_ENEMIES` not putting the parked `blakebloodhawk_1/2/3/8` into play.

**Approach.** The graph raises `Transitioned` on every state change and `CampaignDirector` logs
each as `[campaign] objective N woke|napped|completed|killed|slept|expired [by M] [for Ns] at Ts`,
with a `(held: TICK_DEPENDS_ON_OBJ dependency not awake)` suffix on a nap the gate is holding.
The reproduction is `ObjectiveGraphTests.C1_M04_reaches_the_squad_wake_with_the_tower_down_inside_the_distress_window`
over a fake world with settable group counts. No change to `Ticks`.

**Model recommendation.** None: the graph implements the decode as it stands.

**Verify.** The unit test above; the D31 CM09 sortie log now carries the objective lines, and the
first line to read is whether `objective 28 completed` (group 2 down to two) and
`objective 42 completed` (group 1 empty) appear at all, which points at `GroupLiveCount` if not.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** `BL-563` stays a separate fix (landed on main). The marker clearing is not the bug.

# Wave D — the sortie

## D31 ☐ Fly CM04 to CM09 end to end and judge every item where it was reported

**Goal.** Every item above is judged at the controls in the mission it was reported in, by the
user; a fix that does not read right at the controls is reopened, with the user's eyes outranking
the instruments.

**Evidence (confidence: n/a).** The run-4 sortie (D32) is the model: mission order, one profile
carried through, verdicts filed per mission.

**Approach.** Launch from the menu on a profile positioned at CM04; walk CM04, CM06, CM07, CM08,
CM09 (CM05 is out of scope but is on the path). For each mission, check the items listed under
its wave, and record the verdicts in this plan under D31. Reopen with a new id minted by
`New-ItemId.ps1` where a fix fails at the controls.

**Model recommendation.** None: the user flies.

**Verify.** Per mission, at the controls; the user's eyes outrank every instrument line below.

- CM04 (C3/M03): opens on `cgzep_camera`, the cargo zeppelin going down, letterboxed and
  skippable (A7); the objects destroyed in CM03 stand destroyed on the first frames with no
  fireball, death sound or debris flight (A1); the barrage balloons are down and no gunner engages
  `b_turret*` (A2); the Pandora flies in over the first minute to the dry dock and holds there
  instead of starting moored (A4); the Barracuda drives smoothly into the bay with no single-frame
  snap, and which way its nose faces against the bay opening is the open half (A5); its fighters
  fly the deck run and lift off instead of dying on the deck, and their first turn after lift-off
  is judged separately (A6; a wing into the apron on the first bank is `BL-523`); fly abeam the
  Pandora inside cannon range: its hatches stay shut and no round comes at you (A3, F44; the
  engage flag is never raised in this mission).
- CM06 (C1C/M01): the three objective markers read `Zeppelin [Disable] -` over `Worker's Voyage`
  in red, `[Dock] -` over `Worker's Voyage Docking Hook` in blue and `[Dock] -` over `Pandora
  Docking Hook` in blue (B12); shoot a gun ring off a moving hull: its fire, fireballs
  and debris all ride the hull (B11, F46).
- CM07 (C1/M02): no AA gun destroys itself firing at the barrier (B13); the hangar hand-over
  animates (doors, lift, the drop) and hands over the Blue Streak build (nitro, twin 40 and twin
  30, two pylons) in its shipped `blo_*` skins, unpainted, the blue-grey body with yellow
  wingtips (B14, F48; `pbloodhawk` authors no `paint_pattern`); flying
  level inside 100 m of the staged ladder sensor deploys the rope ladder and banking past 45
  degrees retracts it (B15; a ladder hanging tilted with the aircraft is the unbuilt `ladder_roll`,
  not the switch); the airfield's launches fly the strip, climb out over the field and live (A6, F47); both
  `eairg31` and `eairg32` launch Peacemakers here, since C1/M02's `egen.zrd` names
  `Eairg31_params` for both (C23's counted empty launch is C1/M04's `eairg32`, judged in CM09).
- CM08 (C1B/M03): the Pandora holds a near-level pitch along Klondike1 (C21); the tanker sits and
  rolls with no jump (A5, not reproduced headless, so re-check); destroy the power hut inside
  180 s so OBJECTIVE5 completes before OBJECTIVE6, then four patrol boats wake on the water at
  181 s and drive their nets (C22; they carry no gunnery, `BL-523`).
- CM09 (C1/M04): `mission_intro_animation` plays (the hangar scene, then the flight down over the
  water) and skips; an intro ending on its first frame is a key still held from the flight-check
  screen (A7); one Defend marker, on the Pandora's own `rock_zeppelin`, labelled with its proper
  name (C24, B12); `eairg32` launches no Bloodhawk (C23); with the Promised Land down, read the
  `[campaign] objective N woke|napped|completed|killed` lines in the log for 28 and 42 to see where
  the docking chain stalls (C25, `BL-581` open).
- CM13 (C2/M03): the enemy racers turn into `dzpath1` and `dzpath2` and fly them on rails (the
  tagged zones are 1, 2, 3, 10, 6, 7 and 9 of 13), and the race is winnable (E41).

**⚠ Traps.** A live symptom is evidence about the build that was running: confirm no testing
worktree is open before minting. Launch from the launchscreen on a COPY of the profile positioned
at CM04 (a campaign run writes mission results back). Headless equivalents for a re-check:
`.\RunProbe.ps1 --campaign=<copy>:3` (CM04), `:5` (CM06), `:6` (CM07), `:7` (CM08), `:8` (CM09),
`:12` (CM13), with `--wake-generators` where a launch matters; `[campaign]` objective lines reach
the file sink, while `cutscene:`, `zep:`, `egen:` and `surface:` lines are `GD.Print` and reach
only the console, so their absence from the sink is not evidence.
# Wave E — CM13 (C2/M03), the blocker

## E41 ☑ `BL-585`: AI planes never lock onto a `dzpath` and fly it on rails, so the CM13 racers skip the danger zones

**Goal.** An AI aircraft that reaches a net node carrying the danger-zone flag, or comes near a
`dzpathN` ribbon under whatever proximity rule the original uses, locks onto that ribbon and flies
it on rails through the zone's gate pair, then returns to its net. In CM13 the enemy racers fly
every danger zone of the course, so the race is winnable at the controls.

**Evidence (confidence: lead-only for the symptom, traced for the data).** Reported at the
controls in CM13: the enemy planes follow their net and skip the danger zones, finishing far
ahead; the mission failed with the player about halfway round. The same mechanic is missing in
the original's terms as well: the AI mode enum carries `2 approaching danger zone` and
`5 navigating danger zone` (`docs/org/aiPilot.md`, the `+0x358` mode list), the aircraft net
follower `FUN_0041d1f0` reads node fields 5 and 6 (`+0x11` flag, `+0x14` `dzpathN` index) and
starts `dzpath%d`, or the unnumbered run when the index is negative
(`docs/formats/ai-nets.md`, "The aircraft follower"), and 4 of the 222 shipped nets carry
danger-zone entries. The remake parses those tags (`AiNetNode.EntersDangerZone`,
`AiNetNode.DangerZonePath`, pinned by `AiNetFollowerTests`) and leaves them unacted-on
(`AiNetFollower.cs`), `AiModeMachine` declares `ApproachingDangerZone` and
`NavigatingDangerZone` as enum-only with a standing "do not invent an entry condition", and the
ribbon geometry is built only under `--debug-dzpaths` (`WorldBuilder.BuildDzPaths`). The roster
field `daredevil_chance` (`docs/formats/ai-rosters.md`) is the per-pilot probability of taking an
available run. Settled: CM13's six `hafury` racers (team 0) fly C2's `M3StuntCourse` #16, which
IS one of the 4 tagged nets, with seven tagged nodes naming `dzpath1, 2, 3, 10, 6, 7, 9` in walk
order out of the mission's thirteen zones. The lock-on is the node tag; the proximity roll
(`daredevil_chance`, 500 m, a free lane, `natural_touch` against the zone's difficulty) runs from
the pursue arm alone and only while the pilot carries the failed-steady-hand hit flag, so a
targetless racer never rolls it. In the original the racers fly those seven zones and skip the
other six; the report's "near a ribbon" lock-on is a hit pilot's evasion, not a racer's.

**Approach.** Decode first, in `crimson.exe`: (1) `FUN_0041d1f0`'s `dzpath_%d` branch, which
resolves the ribbon by name and hands the pilot to mode 2; (2) the mode-2 approach law (how the
pilot reaches the ribbon's first vertex, and from which end); (3) the mode-5 run (whether the
pilot is steered along the polyline by the control law or its pose is written from the ribbon,
which is what "on rails" would mean, and what the speed rule is); (4) the exit back to mode 0
and the net node it resumes at; (5) the `daredevil_chance` roll and any proximity trigger that
is independent of node tags, since the user's report describes a plane "near" a ribbon locking
on. The unread `+0x10`/`+0x20` arm of the net-assignment routine (`docs/org/aiPilot.md`, the
"one arm ahead" note) is on the same path. Land the decode as a `docs/org/aiPilot.md` section
and an `ai-nets.md` update, then implement: `AiNetFollower` reports the tag on arrival,
`AiPilot` enters `ApproachingDangerZone` and `NavigatingDangerZone` through `AiModeMachine`
under the decoded rule (replacing the "never entered" prohibition with the decoded entry
condition), and the run reads the ribbon's route polyline classified by material as
`StuntMission.TryReadGates` already does. The ribbon geometry needs a loader independent of
`--debug-dzpaths`.

**Model recommendation.** Decode-heavy (eight functions of `dzpath.cpp` and the AI update,
plus a data census across all eight chapters) with a moderate port behind it; a Claude Fable 5
session did it in one run.

**Landed.** The decode is `docs/org/aiPilot.md` "The danger-zone run" (`FUN_00445ef0` the spline
build, `FUN_00421500`/`FUN_004210e0` the two entries, `FUN_004216e0` mode 2, `FUN_00490590` mode
5, and the unread `+0x10`/`+0x20` arm, which is a goal-node routing table no shipped net carries).
Port: `Flight/DangerZoneRibbon.cs` (spline, run cursor, rail integrator), `Flight/DangerZoneRibbons.cs`
(the loader off the gamez, `dzones.zrd` `disable` applied), `AiNetFollower.ArrivedNode`,
`AiPilot`'s entry/approach/rail/exit, `AiModeMachine`'s two modes with the crash check and stun
handing back to the approach, `FlightController.SimStep` applying `RailPose`, and
`CampaignDirector.Attach` handing the ribbons to every roster pilot. Not ported: the proximity
roll (needs the hit flag), the target release at the lock, the approach solve's floor bypass.

**Verify.** `DangerZoneRibbonTests` (8 facts: spline through the vertices in metres, both run
directions, lanes, the rail closing its offset and banking, the pilot's tag entry through
approach, lock, run and back to a re-seated patrol, the negative tag's nearest-end pick, the
interrupted run resuming as an approach) plus the existing `AiModeMachineTests`,
`AiNetFollowerTests`, `AiPilotTests`; `dotnet test` 2530/2530. Engine suites `ai-modes`,
`ai-net-follow`, `campaign-danger-zones`, `campaign-roster` all PASS. Headless CM13
(`--campaign=<profile copy>:12 --frames=9000`, the copy deleted after): all six racers log
`patrol -> approaching danger zone ('dzpath1' …) -> navigating danger zone (locked at 104-105 m)
-> patrol ('dzpath1' flown, back to the net)` and then the same for `dzpath2`, in course order,
inside 150 s of sim. Still owed: the user flies CM13 and judges whether the racers fly the zones
and the race is winnable, with their eyes outranking the log.

**Verified.** On the merged plan tree, the full gate: build clean, 2541 unit tests, 164 engine
suites in four shards with the error census clean, 16 goldens with six re-pinned (`c3-island`,
`c5-city-night`, `c1-destroy-effects`, `c1-debris-rest`, `c1-targeting-hud` at a channel delta of 1 to 7;
`c1-crash` at 5748 pixels for the vector-form crash pieces that now tumble, A5). Two cross-item
regressions the per-agent suites could not see were fixed on the merged tree before this run
(the trailer pickup suite against B14's prerequisite gate; `target-pool`/`world-turrets` against
A2's in-tree liveness read).

**⚠ Traps.** Do not invent the entry condition (the prohibition on `AiModeMachine` stands until
the decode replaces it). Do not use polygon index to find the route ribbon; classify by
material (`docs/formats/missions.md`). The splitscreen race countdown `BL-314` is a different
"on rails" and stays separate. `BL-523` (the patrol/pursue cycle) is out of this plan and must
not be pulled in through the shared mode machine.

# Wave F — the CM02 capture (found on D31's way in)

## F42 ☑ `BL-588`: CM02 is lost 20 s after the player captures the last bomber

**Goal.** Capturing the last live bomber of group 5 through the 967 wing-walk swap leaves the
mission running: the player now flies that bomber and counts for it, the wingman escorting the
player follows the rebuilt rig, and a death in the rebuilt rig still ends the mission.

**Evidence (confidence: traced).** At the controls (main and the plan branch alike) CM02 (C3/M05)
is lost about 20 s after the capture. The chain: `OBJECTIVE20` (`DEDG [5, 1]`, the dormant
primary; `OBJECTIVE63` reads the same awake) then the capture;
`FlightRoster.CarryCapturedDamage` sets the captured `britbalmoral_1` `Inert`; `GroupLiveCount`
walks the roster alone and excludes inert and crashed rigs, so group 5 reads 0; `OBJECTIVE22`
(`DEDG [5, 0]`) completes and naps `OBJECTIVE25` (INSTANTLOSS) 20 s. The decode
(`cutscenes.md`, "The airframe swap codes 965, 966 and 967"): case `0x3c7` of `FUN_0047e080`
copies the captured vehicle's `+0x388` (the roster group) onto the player after the hull
scaling, and `FUN_0047fd50` walks the global target list (`DAT_0071dabc`) after the rebuild and
re-points every `TargetVehicle` in the `+0x948` and `+0x2fc` slots that held the old player
object onto the new one. Case `0x3c6` (966) does neither the copy nor the hide. CSVM did neither:
the player's rig carried no group, and after the swap `wingman_4` (`escorts 'player'`) kept its
`AiEscort.Leader` on the freed controller (`ObjectDisposedException` in `AiPilot.FlyEscort` every
physics frame) while `CampaignDirector.WirePlayerDeath` kept its `Downed` hook on the old rig.

**Approach.** `FlightController.Group` (the cohort, null outside a roster spawn);
`CampaignDirector.BuildRoster` stamps it from the plan; `FlightRoster.RunSwap` stamps the captured
rig's group on the replacement for a code where `AirframeHandover.CarriesCapturedGroup` holds
(967 alone) and runs `RepointHolders` over every AI pilot's `Escort.Leader` and `Gunner.Target`;
`GroupLiveCount` counts the human rig carrying the group while it is not crashed (inert is a
cutscene state on a human rig, not a deactivation); the director's death and damage hooks are
subscribed by controller identity and re-subscribed when `PlayerAircraft` answers a different
one. Other holders checked: `TargetHud._hostile` and `TargetSelection` belong to the human HUD
freed with the old rig, `InstantActionDirector._ace` is never the player, `CutsceneController`
reads `rig.Controller` fresh, and the aim-assist candidate lists are rebuilt each frame.

**Model recommendation.** A single session: the decode was two functions, the port four files.

**Verify.** Suite `campaign-capture-group` over C3/M05's own roster and graph: two bombers down
through the debug kill, group 5 reads 1 and the awake `OBJECTIVE63` completes with `OBJECTIVE22`
incomplete;
the swap on `britbalmoral_1` hides it, the rebuilt rig carries group 5 and `wingman_4` escorts
it; 5 s of stepping the wingman, the rig and the director throws nothing, group 5 still reads 1,
`OBJECTIVE22` stays incomplete and `OBJECTIVE25` dormant; the rebuilt rig's death ends the
mission lost at 0.02 s. `AirframeSwapTests` (2 facts) pins the group carry to 967. Foreground:
`dotnet test` 2543/2543; `campaign-airframe-swap`, `landings-hookup-airframe`,
`campaign-wingwalk-camera`, `landings-wingwalk-gate`, `campaign-hangar-handover`,
`campaign-objectives`, `campaign-roster`, `campaign-player-death`, `campaign-squad-wakeup`,
`campaign-surface-vehicles`, `campaign-capture-group` all PASS, engine errors clean. Still owed:
the user flies CM02 and captures the last bomber.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** Count the human rig by `Crashed`, never `Inert` or `InPlay`: the rig is inert under
the capture cutscene and a walk that dropped it there would complete `DEDG [5, 0]` on the frame
the hold lifts. The group carry is 967's alone; 966 reads no captured vehicle. The suite now runs
the capture through the cutscene host (F54), so the held state is exercised headless as well as
on the flown path.

## F43 ☑ `BL-590`: the Barracuda teleports between its surfacing and its drive

**Goal.** The Barracuda surfaces, drives its authored 1680 m along +Z at 40 m/s and settles in the bay with no jump.

**Evidence (confidence: traced).** At the controls the sub jumped miles away and back. `MotionRuntime.Create` re-seated every ballistic launch on the authored rest pose unless top-level, landing-resume or takeover; the barracuda's gamez node is authored at the map origin and the mission RESET_STATE moves it by `PoseTranslate`, so each drive leg seeded at (0,0,0). The original (`FUN_004e8fa0`) copies authored velocity and delta into live slots on the first tick, writes no position, and every tick adds dt x live velocity to the node's own translation (`FUN_004d27d0` / `FUN_004d1e50`) in the parent frame, unrotated by yaw; a chained ObjectMotion continues from the current position.

**Approach.** `MotionRuntime` seeds from the live transform (rest only as the non-finite fallback); the law is `LaunchLaw.Origin`. Yaw pi puts the nose on world +Z, the drive direction, so the hull faces into the bay away from the opening it entered.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `barracuda-drive` (largest step 1.33 m, end (-12032, 0, -11516.288), yaw -180); `MotionChainTests` (3 facts); headless CM04 seeds at z -13197.5, -13157.5, -11557.5, each leg from the previous end.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** The rest re-home was added for `BL-511` (a pooled copy relaunching from its last end pose); `effect-pool-reset` and `effect-pool-spawn-pose` stay green because the pool re-resets the copy on checkout. `ground-contact` relied on the re-home and now re-places its node per launch.
## F44 ☑ `BL-591`: the Pandora fires on the player in CM04

**Goal.** The broadside deploys and fires only when the mission script engages it, as the original does.

**Evidence (confidence: traced).** Flown in the original (`CAP-46`): the Pandora never opens on the aircraft. The static decode of the target chain was right too; the whole fire routine sits behind a flag it did not trace. Zeppelin byte +0xc is the COMPLETED_ZEPCANNONS flag: zeroed by `FUN_004bd460`, never written by the record parser, written only by `FUN_0046a0b0`; `FUN_004bf9d0` selects the fire pass `FUN_004c0250` when set, else `FUN_004c03a0`, which only retracts a ready cannon. Only C2B/M04, C4/M05 and C5/M04 author the directive.

**Approach.** `ZeppelinBroadside.CannonsEngaged` (default off; a ready cannon retracts while clear), `ZeppelinRuntime.SetCannonsEngaged`, `CampaignDirector.CompletedZepcannons` writes the flag. `BL-567` deleted, `CAP-46` removed.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** `zeppelin-broadside` opens on the real C3/M03 world: the player 300 m abeam for 30 s, no hatch, no `wep_28`, then the same hull engaged deploys the port six and fires; the zeppelin-vs-zeppelin legs unchanged.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** The engage flag is per zeppelin record; CM09's Promised Land is not a record (F51) and its doors are opened by anims, so the disengaged retract never touches it. `cannon_fire_range` (+0x14) has no reader on the decoded fire path while the remake still gates on it (objectives.md gap list).
## F45 ☑ `BL-592`: CM06's docking cutscene hands flight back early and teleports the player at its end

**Goal.** The player stays held from the hookup to the row's last raiser and is placed once, by the handoff code.

**Evidence (confidence: traced).** The row `anim[wv_initiate_hookup] node[wv_manual_land]` authors no CALLBACK itself; it calls wv_hookup_player (2 and 11, then a 7.3 s script), wv_drop_copilot, wv_hookup_state and last wv_unhook_player (1 then 951 after its own script). `CutsceneController` booked the episode to the first raiser and restored when that definition ended at 7.3 s; the row's trailing WAIT is dropped as a last event, so the row ended then too, and the unhook's 951 at 9.9 s opened a fresh episode whose restore staged the aeroplane onto the marker. The original's rule (cutscenes.md code table): the held flag is set by 11 and cleared by 1, and no definition ending touches it.

**Approach.** The landings slot owns the episode (`CutsceneController.Own` from `LandingApproachRuntime.Start`); the raisers are the call closure's defs that author a Callback; Tick restores only when the row has ended and none is still running; `ResumeAt` places at once when not Held.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `landings-docking-hold` (released at 9.95 s on the handoff code, 0 m off the marker, biggest step 1.52 m); 15 cutscene and landing suites.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** CM06's hookup never activates the player marker, so the aeroplane is undrawn until 951 (the `BL-482` limit); a 951 raised into an episode whose row already ended is still booked as a fresh episode, which no shipped data now reaches.
## F46 ☑ `BL-593`: a gun ring's death fireballs and debris stand still while the hull sails on

**Goal.** A death's callee rides its call site: the fireballs and debris of a ring shot off a moving hull move with the hull.

**Evidence (confidence: traced).** Reported against the original. B11 read AT_NODE as a position taken once and WITH_NODE as the live node. `FUN_004edf80` (the CALL_ANIMATION start) zeroes a world-attached template callee's root at the call and passes the site only as node +0x7c and position +0x80; the events consume those by flag bits whose mapping onto the parser's AT_NODE/WITH_NODE bits (`FUN_00515c00`, +0x2e) is unreconciled, and neither large_fireball nor dblcannon_flying_parts references INPUT_NODE, so the static read cannot place the effect either way. The controls' reading stands.

**Approach.** `AnimRuntime`: a routed ExternalEffect call follows whenever a site resolved; the un-routed relocate path places following while a death call is in flight; `TemplateStage.PlaceAt` gains a follow flag. Debris bodies are not reparented, so re-placing the root carries the pieces. The Workers' Voyage carries no doublecannon ring; the ring with debris is piratezep/blackswanzep doublecannon4/5.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `ring-death-effects-follow-hull` (hull moved 500 m and 35 degrees: the fireball's fed position, the debris root and part1 all move by the ring's displacement; red on exactly those three checks with the follows off); 16 effect and zeppelin suites. Golden `c1-destroy-effects` moves and is re-pinned in the verification commit.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** Damage-stage panel tears and other relocating calls are unchanged: only a death call in flight follows. The AT_NODE/WITH_NODE bit mapping stays open in `docs/org/sequences.md`.
## F47 ☑ `BL-594`: CM07's launched Peacemakers strike the strip two seconds after hand-off

**Goal.** A launched aircraft is handed to the AI climbing at 53 m/s, 300 m past its last waypoint, and lives.

**Evidence (confidence: traced).** `a3`/`a5` in the ram lines are the airstrip's terrain tiles; each launch struck the strip 130 to 160 m along the runway, a wing into the surface on the AI's first bank, because A6 handed over at the last waypoint at 27 m/s and 1 m AGL. `FUN_0048a110`: on the final leg the steering target is `wp[leg] + normalize(wp[leg+1] - wp[leg]) * 300` from the waypoint behind the leg plus the speed climb term; the leg-advance test is measured against that replaced target and the flag clear at 0x0048a863 follows it; the motion is pitched at the target. The docs had this as "not pinned, chosen here".

**Approach.** `PathFollower`: the final-leg target, the finish test and the pitch as decoded, pitch exposed; `AiGeneratorRuntime.TakeOffRun` uses the follower's pitch. Hand-off on the real rig 247 m past the last point at (-5553,214,-4128), 53.8 m/s. C1/M02's egen.zrd names Eairg31_params for both eairg31 and eairg32, so both launch here; the misspelt block is C1/M04's (C23).

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `generator-launch-climb-out` (30 s flown after release: lowest y 213.5, slowest 53.8 m/s, mode Patrol); headless CM07 with --wake-generators over 3600 frames: no AI ram, no _eg downed.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** The ram rule (`local_11`) was reading correctly and killing a real ground strike; do not soften it. `BL-523`'s first-turn paragraph about eg0/eg1/eg3 is superseded and removed.
## F48 ☑ `BL-596`: the hangar hand-over plays without the aeroplane; the Blue Streak's livery

**Goal.** The player rides the lift and drop in the Bloodhawk, on its undercarriage, and flies out in the shipped blo_* skins with the yellow wingtips the original draws.

**Evidence (confidence: traced).** Three stacked causes: `WorldSession` built the AircraftStage only for a mission whose start list bootstraps an intro (C1/M02 has none), so code 11 held the pilot inert and undrawn; camera1-player_setup sets `player` INACTIVE in its callback sequence and ACTIVE again in a RESET_STATE on RESET_TIME 0 that CSVM never runs; the props (anim_bloodhawk, bloodhawk_gear) were never staged and the drop's RESET_STATE 951 was suppressed. The livery: `pbloodhawk` authors no paint_pattern and `FUN_0047c210` skips the whole scheme composite on the empty string, so the original draws the shipped skins (CM07.mkv 2:56 to 3:03); no shipped scheme produces them.

**Approach.** `WorldSession` builds the stage for any mission with an intro, approach triggers or mission cutscene anims; `AircraftStage` stages the two props off; `CutsceneController.BindRigs` re-asserts the marker and `Restore` raises an authored RESET_STATE 951 before the restore codes; `AirframeSwapCode.ShippedSkins` replaces LiveryDef. B14's open livery choice is closed by the data.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** `campaign-hangar-handover` extended (rig drawn on the marker within 0.5 m with the gear under it over the 56 m lift, both props off at the handoff, Scheme and Painter null, the surfaces sample blo_wing/blo_fin/blo_fusalagetop); 14 cutscene suites.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** CSVM does not run a definition's RESET_STATE at its end on RESET_TIME 0 (1651 defs carry it); the `BindRigs` re-assert covers the `player` node only and the general schedule stays undecoded.
## F49 ☑ `BL-595`: no rope ladder, the passenger hangs in the air, no flare

**Goal.** The person stands on the caboose and rides it, waves with a lit flare in the track loop's open phases, the ladder drops on the level approach inside 100 m, and the pickup cutscene climbs him aboard.

**Evidence (confidence: traced).** `caboosepickup` had no live instance: its only motion is OBJECT_MOTION_SI_SCRIPT in the ROOT/ALL_NAMES form, never handled (count u32, then 76-byte records of an embedded e12 event with a 1-based node index and a script index); the same form drives caboosewave, waveloop, hit_the_deck, get_up and cabpkup_ladder. `SequenceRunner` marked a runner done when its cursor passed the last event, so no WAIT held. The staged copy stayed TopLevel after OBJECT_ADD_CHILD, so it hung at the call site. pickup_timing reached the switch only through the symbol table, which the by-index map never held; the flare's add-child could not build its library root from a re-entered instance. pickup_timing is started by the chapter's train_on_track at load, phased with the track loop, and the sensor restart re-phased it. CM07.mkv at 0:24 and 0:30 agrees.

**Approach.** `CompiledAnim` decodes the form (`AllNamesKind`), `PoseChannel.HandleMotionSiScriptAllNames` plays it; a sequence holds for its last event's run time; adoption clears TopLevel onto the parent's frame; `NameResolver.SoleStagedCopy` answers a claimed-but-unbuilt index with the one live copy carrying it; the timing starter is removed from the trigger. F53: a claim made from inside a staged copy narrows to that copy, since F48's stage parks the archive's pickup_cpilot figure whose hand claims the same index.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `landings-train-pickup-ride` (passenger under caboose, 94.2 m travelled with 0 m offset, waveloop live in the 12.36 s phase, ballflare visible with flaretrail emitting, drop_ladder Deployed by callback 123, caboosepickup live for the 2.2 s climb with the WAIT held); 27 anim, effect and cutscene suites.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** The `SequenceRunner` hold lengthens every instance whose last event is timed; the effect census suites are the guard. The rope-ladder rungs hang off the airframe's `ladder_pos`, which the harness rig lacks, so the suite cannot see them; that stays with the controls.
## F50 ☑ `BL-600`: CM09's intro shows no wingman on the launch and the dive

**Goal.** The archive's piratefighter prop flies the launch and the dive beside the player, as the data animates it.

**Evidence (confidence: traced).** `playerdrop` (pfighter12) and `playerthruclouds` (pfighter13) fly the `piratefighter` prop with SI scripts and never activate or parent it (only generic_intro's gi_pfighter1 does both); the archive node ships ACTIVE, so the original draws it from load. `AircraftStage.Build` built the prop switched off "until gi_pfighter1 activates it".

**Approach.** The prop keeps the archive's own state; in a mission that bootstraps an intro it sits at the origin until the first script poses it and stays where the last leaves it. The roster's Devastators are parked by callback 913 through the cutscene, in the original and here alike.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `intro-wingmen` (playerdrop: drawn, posed and framed on 118 of 118 frames, moved 85.1 m; playerthruclouds: 631 of 631, moved 1172.3 m, best angle 8 degrees off camera1); 5 cutscene and roster suites.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** It is one wingman, since the data animates one prop; the three roster Devastators appear at the handoff as before.
## F51 ☑ `BL-599`: the Promised Land no longer burns out and sinks (regression from B14)

**Goal.** Shooting the open broadside doors burns the gasbags, three finishers bring the hull down and the primary completes, as on main.

**Evidence (confidence: traced).** The hull (`hk_zep`) is not a zeppelin record; its kill is animation-authored: a door's death calls its gasbag burn, the burn calls `finished_lkgasbag0N`, three finishers satisfy `finish_locklear` (MINIMUM_TO_SATISFY 3 of 4), which calls the last gasbags and `lockleargoesdown`; OBJECTIVE30 completes off `INACTIVE1 [lkgasbag05, panelleft1]`. The finishers carry REQUIRED OBJECT_INACTIVE_LIST entries with LOCAL_NODES_ONLY; `FUN_0051d7b0` writes the leaf's word as bit 0 = active/inactive and bit 1 = local scope, so a local INACTIVE entry is raw 2, which mech3ax's `active` field reports as true. B14 read that field and refused every finisher. 726 of the 749 shipped compiled node prerequisites are raw 2. The sortie log could not show it: `AnimRuntime.DamageAt` logs its first 12 hits per session.

**Approach.** `CompiledAnim` takes `(active_raw & 1)` when the raw word is present. The doors are deployed from t=0 by the OnStartup temp_hkzep_lbroad* calls; no broadside AI is involved.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `zeppelin-cannon-burnout` over C1/M04's real world (hatches at -2.356 rad by 5 s, lbroad4 destroyed by 30 real rounds, every burn and finisher started, the hull dropped 42.8 m, OBJECTIVE30 at 42.2 s; red at the three finishers before the fix); `CompiledPrereqTests` over raw 0/1/2/3; 17 zeppelin, turret and objective suites.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** B14's gate itself stands; only the leaf read changed. Reproduce headless rather than reading the damage sink for a late kill.
## F52 ☑ `BL-602`: CM13's first zone marker sits at the world origin; zone 3 does not count

**Goal.** The first marker sits on the hangar mouth, and the race chain is pinned end to end.

**Evidence (confidence: traced).** `sghangar` is a group node with no transform whose children carry the world coordinates; `ObjectiveSites.Where` used its GlobalPosition, the origin 8 km away (`FUN_004b5fb0` confirms the marker takes the entity's own position). Zone 3: OBJECTIVE28 is `DEDG [3, 0]` on racer group 3 with KILL 9, 17..24, 42 and NAP 49 (INSTANTLOSS) 15 s, one such block per racer group; it fired correctly at 38.0 s when hafury_4 rammed the hangar (F53's `BL-601`), so the evaluator is unchanged.

**Approach.** `ObjectiveSites.SiteAnchor`: a node with its own mesh or off the origin keeps GlobalPosition; an origin-standing group that draws nothing anchors on the centre of its built meshes with a door-named leaf pair winning (the stunt mode's aperture rule). C1/M04's placed rock_zeppelin site is untouched.

**Model recommendation.** A single session per item, each in its own worktree off the plan branch.

**Verify.** Suite `campaign-race-chain` (sghangar's anchor within 150 m of dz1 and the offered site marked there; over the real graph dzpath1..3 complete 17, 18, 19 in order with none of 28..33 firing, then group 3 dead fires 28); 6 objective suites.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** The unrestricted version of the anchor rule moved the rock_zeppelin site 22 m and failed `campaign-objective-target-path`; the origin restriction is what scopes it.

## F53 ☑ `BL-601`: CM13's racers ram the hangar on dzpath2

**Goal.** The racers thread the dbase arch on dzpath2 and fly the rest of the course, so the race is winnable.

**Evidence (confidence: traced).** The rail carries the racers at y 19 to 21 m through the dbase arch, a 9.7 m slot at rail height with the centreline 3.7 m from the left post; the pfury wing probes are 10.4 m apart, so no roll short of 90 degrees clears it, and the rail's roll (`FUN_00490590`, the second derivative's lateral part normalised) is no knife-edge in the original either. The original's contact test (`FUN_0048d7f0`) sweeps the def's `collision` probe list as rays from the previous pose; vehicle.zrd authors six probes only on the p* player defs, basic_airplane carries one probe at the origin and no AI def overrides it, so every AI aeroplane collides as a single centre point and its wings pass through the posts. The rail pose does not bypass collision, and the 105 m lock is the decoded 11025 m^2 test. The remake swept the mesh-derived hull for AI too.

**Approach.** `PlaneStats.CollisionProbes` (nearest def in the damage chain); `FlightController.SweepProbes` sweeps those probes as rays for an AI rig, the human rig keeps the hull sweep and the centre-ray backstop stands. `TestHarness.WithWorld` evicts cached collidable worlds before a collidable build.

**Model recommendation.** A single session; the decode was the rail step, the probe sweep and the def parser.

**Verify.** Suite `campaign-racers` (C2/M03's roster in its collidable world with the propane tanks in dzpath1's second gate blown: all six lock dzpath1 and dzpath2, leave each at the far end, nobody rams); 18 AI, wingman and campaign suites; `collision-visibility` and `alpha-cutout-ray-census`.

**Verified.** On the merged plan tree, the full gate: build clean, 2554 unit tests, 174 engine suites in four shards with the error census clean, 16 goldens with `c1-destroy-effects` re-pinned (769 pixels, the radio tower's fireball riding its death site, F46). Two cross-item interactions the per-agent suites could not see were fixed on the merged tree before this run: the flare's symbol claim against F48's parked figure (F49's companion commit) and the climb-out suite's leaked second launch taking `roster-spawn-names`' spawn name (F47's suite).

**⚠ Traps.** The human rig still sweeps the mesh hull where the original sweeps its six probes; a player stunt through a slot the six points clear and the hull does not would differ (`BL-603`). C1's cached scenery stood inside C2/M03's airspace when `ai-wreck-fall` ran first in the shard; the harness eviction is what keeps `campaign-racers` order-independent.

## F54 ☑ `BL-604`: CM02 is still lost during the capture, before the swap

**Goal.** The last bomber of group 5 keeps counting for `DEDG [5, 0]` while the capture cutscene parks it, so `OBJECTIVE22` never completes and `OBJECTIVE25` (INSTANTLOSS) is never napped awake.

**Evidence (confidence: decoded).** At the controls after F42, CM02 still aborts: the sortie log shows `objective 22 completed` and `objective 25 napped by 22 for 20s` about 19 s before the `airframe swap` lines, during the wing walk, with `britbalmoral_1` alive. The walk's code 913 sets `Inert` on every AI aircraft and `GroupLiveCount` excluded inert roster rigs. Decode: `FUN_00465850` counts a vehicle iff `+0x91d == 0` and `+0x388 == group`; `FUN_004b0f40(1)` (deactivate, and 967's hide) sets `+0x91d`; code 913's `FUN_0041f250` sets the hold flag `+0x354` and deactivates the scene node but never touches `+0x91d`, and `FUN_0041f2e0` (914) is its inverse, so the original counts a parked vehicle and not a deactivated one.

**Approach.** `AircraftLifecycle.Parked` beside `Inert`, and `Deactivated => Inert && !Parked` (the dead byte). `CutsceneController.ParkAi`/`RevealAi` set and clear `Parked`; `Swap` clears it on the aircraft 967 hid. `CampaignDirector.GroupLiveCount` and the TRAVELERS group form read `Deactivated`, never `Inert`.

**Model recommendation.** A single session; the decode was three functions.

**Verify.** `campaign-capture-group` now plays `ww_balmoral1` through the cutscene host from the approach row's trigger (`Own` and `PlayMissionTrigger`, realtime clock, marker grafts) and samples group 5 every step to 20 s past the handoff: parked 19.22 s, min 1 throughout and while parked, `OBJECTIVE22` incomplete, `OBJECTIVE25` dormant, swap at 19.22 s, handoff 19.25 s, death ends Lost; with the old `Inert` read the same suite fails (min 0, mission ended). `AircraftLifecycleTests` (4 facts). Foreground: build clean, `dotnet test` 2558, eight campaign and landing suites PASS.

**Verified.** On the merged plan tree, the full gate: build clean, 2558 unit tests, 174 engine suites in four shards with the error census clean (engine stage 104 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** `Inert` is presence; `Deactivated` is the mission's dead byte. Any new objective or tally walk reads `Deactivated`. The park is set before `Inert` so an `InertChanged` listener reads a consistent pair. The hidden captured aircraft leaves both the parked list and `Parked`, or the player and the bomber count twice.

## F55 ☑ `BL-615`: CM13's racers loop around zone 3 and never go on to the next zone

**Goal.** A racer that finishes a `dzpathN` run rejoins its net going onward, so the six racers fly the seven tagged zones of `M3StuntCourse` in the net's own order and the race runs its full course.

**Evidence (confidence: decoded).** At the controls after F53 every danger zone clears and nobody crashes, but the racers circle zone 3. Reproduced in `campaign-racers` extended past `dzpath2`: all six run `dzpath1, 2, 3, 3, 3, 3` for 540 s and never reach `dzpath10`. The decode: `FUN_00490590`'s rail exit reads the edge the walk was on at the entry off `+0x2e8`/`+0x2ec`, snaps to the nearest node (`FUN_00431900`) and hands that edge id to the nose pick `FUN_00431e40` as its exclusion, whose candidate loop skips it; the ordinary walk step `FUN_0041d8f0` excludes the flown leg the same way. `AiNetFollower.Reseat` excluded nothing, and `dzpath3`'s exit at (-7628, 174, -2788) leaves the leg back toward the tagged node as the best-aligned edge, so the racer flew back, reached the tag and re-locked. No cooldown backs it up: `FUN_00421500` writes the `+0x8a0` retry stamp on a refusal and never reads it. E41's `docs/org/aiPilot.md` already carried the clause; it was never ported.

**Approach.** `AiNetFollower.Reseat` takes an optional edge to refuse, held as an unordered node pair and spent on the seat it applies to; `AiPilot` records the leg at the entry and hands it back at the exit. Every other `Reseat` caller keeps the unconstrained default.

**Model recommendation.** A single session; the decode was four functions of the walk and the rail exit.

**Verify.** `campaign-racers` extended from two zones to the net's whole seven-zone course with two new per-racer assertions (no zone flown twice, the tagged zones in the net's order): all six run `dzpath1, 2, 3, 10, 6, 7, 9` in 208 s of sim with no rams. `AiNetFollowerTests.ReseatRefusesTheEdgeItWasToldToAvoid`. Foreground: build clean, `dotnet test` 2559, and `campaign-danger-zones`, `ai-modes`, `ai-net-follow`, `campaign-roster`, `campaign-race-chain` PASS.

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** The exclusion is an edge, not a node: refusing the tagged node itself would strand a racer whose course runs back through it. It is spent on one seat pick, so an interrupted run that re-approaches carries no stale refusal. The other `Reseat` callers (the Instant Action activation snap, `FlightController`) must keep the unconstrained default, or a teleported wave member loses its nearest-node seat.

## F56 ☑ `BL-616`: CM13 docks with an invisible Pandora

**Goal.** The Pandora's hull draws in CM13 (and CM11), so the docking hook and approach cones the end of the race arms sit on a zeppelin the player can see.

**Evidence (confidence: decoded).** At the controls on CM13 the docking point is in position and the dock works while nothing is drawn. Not a regression: `WorldBuilder` and `GameZ` are byte-identical with main, and the defect became reachable only once F52, F53 and E41 let a player finish the race. `C2` is the only chapter shipping the `piratezep` node with `flags.active` clear (`support\c2\load.gw`: `LoadGameGen ... piratezep; NodeSetActive off`), and `WorldBuilder` honours that bit. Neither `m02.gw` nor `m03.gw` sets it back, `C2/M03`'s `zepstate.zrd` names only the vestigial `hk_zep`, and no `ObjectActiveState` in any of the mission's 200 compiled defs addresses `piratezep`. The dock still arms because arming reads poses: `pzhookpoint`, `hookbay` and both `land_on` cones all hang off `piratezep` in the gamez tree. The original's own activator is the record: Instant Action's builder re-activates its selected zeppelin with `FUN_004cca30` (`gwNodeSetActive`, bit 2 of node `+0x24`) at `0x0045b928`, mirroring the deactivation loop at `0x0045b8d6`. Two records in the whole install name a gamez-inactive node, `C2/M02` and `C2/M03`, both `piratezep`.

**Approach.** `ZeppelinRuntime`'s placement loop switches the resolved hull node on, logging a `zep:` line when it was built inactive. Dormancy is opacity, so a `deactivated` record is unaffected.

**Model recommendation.** A single session; the diagnosis is data, the fix is one line.

**Verify.** New suite `zeppelin-hull-activation` over C2/M03's real world: the gamez ships the one `piratezep` node inactive, the world builds it hidden, the record places it and switches it on, the hook, bay and both cones resolve under the hull, and after `pzhomebase` runs 12 s the hull, the hook and the hangar bay all draw (four checks red with the line removed). Foreground: build clean, `dotnet test` 2558, and `campaign-zeppelins`, `zeppelin-identity`, `campaign-race-chain`, `campaign-racers`, `campaign-objectives`, `campaign-objective-markers` PASS.

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** A working dock is not evidence that its host draws: the objective target, the stop-point release and the HUD marker all read poses and names, never visibility. `SetDormancy` writes opacity, not `Visible`, so the two switches are independent and must stay so. Both landing cones remain hidden after `pzhomebase` for a separate reason (`BL-618`, the `~n` dedup name).

## F57 ☑ `BL-609`: no flare smoke on the caboose passenger, and the ladder stops swaying in the pickup

**Goal.** The flare's authored smoke plays on the passenger's hand for the open wave phase, and the ladder's behaviour through the climb matches the original.

**Evidence (confidence: traced, and measured in a flown session).** Both reports are disproofs. The `cuepuffer2`/`cuepuffer3` stop-misses in the suite artifact belong to `camera1`/`speed_cue` (puffers on `player`), not the flare; the report line prints the runtime's global unhandled counts beside `pickup_flare=`. The flare's puffer is `flaretrail` on `pickup_agent`/`waveloop` (`at_node cp_lh`, `DISTANCE_INTERVAL 0.4`, `fire_f01..06`). A freecam probe at the caboose reports 7 active puffers and 286 live particles including `flaretrail`, following the hand at one batch per 0.4 m. The suite could not see it: `TrainPickupRide` installs a counting emitter factory whose `LiveCount` is 1 whenever it sustains, and the census row carried only the host's name, so a same-named hand on the parked library figure would have read identically. On the ladder, `lookat_copilotpkup` opens with `STOP_ANIMATION [drop_ladder]`, which ends the per-rung `ladder_loop` wind loops inside `gen_drop_ladder`, then calls `cabpkup_ladder`, which re-parents the same ladder to the caboose, zeroes `ladder_roll` and drives its root and all six rungs from one `ALL_NAMES` record. The climb replaces the sway. Full decode: `docs/org/ladderSwitch.md`, "The ladder through the pickup".

**Approach.** No behaviour change. `EmitterCensusRow` carries the host node, and `landings-train-pickup-ride` asserts the trail's host is the staged passenger's own hand, builds the mission's `flaretrail` state over a recording renderer and drives it along that hand's real poses, and measures the ladder as a point along `rung1` in the ladder root's frame.

**Model recommendation.** A single session; the answer is in the mission data and one flown probe.

**Verify.** The suite reads 22 sprites over 9.12 m against the 11 the 0.4 m cadence owes, and `rung1` swinging 0.318 m per 0.3 s hanging and 2.639 m over the 2.2 s climb with `cabpkup_ladder` live. Foreground: build clean, `dotnet test` 2558, 8 landings suites and 12 effect, emitter and puffer suites PASS.

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** A rung hinges about its own origin, so a position-only read of one sees nothing. The harness retires the real puffer factory with its build, so anything about actual particles is asserted at the `Puffer` seam, never off the director's census. During the pickup cutscene there is no flare smoke by design (`lookat_copilotpkup` stops `waveloop`), and the sprites are 0.1 to 0.5 m across, so they read as a wisp: if the smoke still looks absent at the controls, the open question is sprite scale against footage, not the runtime.

## F58 ☑ `BL-611`: CM07's AA guns never fire

**Goal.** CM07's five `aagun` emplacements wake on `OBJECTIVE1`'s `WAKEUP_TURRETS aagun**` and engage the player on every replay, as they do at the original's controls.

**Evidence (confidence: traced).** Reproduced on the flown campaign path: all five read `alive=False gate=Dead` at build and never wake. Not a turret change: `m02.gw` switches no `aagun` off, A2's tree-visibility read is correct (every site reads visible and in tree), and B13's owner exclusion never touched firing. What hides `healthy` is `CampaignPersistLog.ApplyTo` restoring the guns a previous CM07 sortie shot down: the log was keyed by chapter alone and folded the whole chapter at open, while `CampaignDirector` merges the mission's own capture into that chapter on a win, so a won CM07 wrote its wreckage into chapter 1 and the next CM07 read it back. `docs/formats/saved-games.md` already decodes the rule: the campaign object walks its mission list backwards for the most recent EARLIER entry in the same world folder (`FUN_0046b7e0` into `FUN_0046b560`, index at `+0xc18`), and CM07 is `seq` 6, the first campaign-1 entry, so the original finds no carrier for it at all. A1 (`BL-513`) is the trigger, not the cause: its silent `CarryState`/`ApplyDeathPose` path applies the destroyed pose cleanly where the old `DamageAt` route skipped what was already destroyed.

**Approach.** `PersistedObject` gains the capturing mission's story position; `CampaignPersistLog` keys chapter, then seq, then node, and `Through`/`ApplyTo` carry only positions before the opening mission (the chapter's first mission carries nothing). A state stored without a position counts as an earlier mission's, so existing profiles keep their legitimate carries and need no surgery. `CampaignDirector` resolves the cut from `CampaignSequence.PreviousInSameChapter` and stamps the mission's seq on the end-of-mission merge.

**Model recommendation.** A single session; no new decode, the reading was already committed.

**Verify.** New suite `c1-aa-guns` (C1/M02's five emplacements placed and shipped dormant, `OBJECTIVE1` arming exactly those five, a chapter-1 log holding all five wrecked applying 0 and leaving them alive, each gun acquiring a plane 250 m out and firing with no self-hits, and the able-to-fail control with a cut that does reach them reading every gun dead); `world-turrets`, `turret-self-fire`, `target-pool`, `ai-gunnery`, `campaign-objectives`, `mission-off-turrets`, `campaign-persistence`, `carried-state-silent` green; `dotnet test` 2560.

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** Guns destroyed in an earlier chapter-1 mission still carry into `C1/M04` and `C1/M05`; only a mission's own capture is refused. The turret census and the `WAKEUP_TURRETS` arm are `GD.Print`, so a sortie log cannot say whether a mission's emplacements woke, which is what made this look like a turret defect (`BL-619`).

## F59 ☑ `BL-610`: the hangar drop's aeroplane is missing and the pilot is handed back at the world origin

**Goal.** The Bloodhawk rides the lift in view and the pilot flies out at the hangar doors, in a mission whose earlier cutscenes have already run.

**Evidence (confidence: traced).** `CutsceneController.Restore` returned `camera1` to the world root but left the staged `player` marker wherever a definition's own composition had parented it. CM07 plays two landing rows before the hangar: `hooked_to_klondike` leaves the marker under `pzhookpoint` and `lookat_copilotpkup` leaves it under `caboose`, a moving train. The drop's `hdplayer1/2/3` then write their pose as a LOCAL transform in that frame: the marker travels 605 m instead of 56 m, the aeroplane rides the resulting world pose out of the shot (while `chuteman`, posed off its own correctly parented node, still parachutes into the hangar), and the authored 951 reads the same pose and flies the pilot 6075 m from the doors. CSVM has a node to strand where the original has none, its `player` being the flown vehicle itself. Disproved: the 965-rebuilt rig, a racing marker re-assert (`player_setup`'s deactivation is a t=0 bootstrap event, so F48's evidence can be stated more strongly), a 951 on a freed controller, and the prop staging.

**Approach.** `Restore` returns the marker to the runtime's world root at identity, beside the camera and after the restore codes, since the 951 must still read the pose the ending definition left.

**Model recommendation.** A single session; a CSVM scene-graph consequence with no counterpart in the original.

**Verify.** `campaign-hangar-handover` now binds the way `GameSession` does, plays the mission's earlier landing rows first, arms the drop from its own caller and lets the range gate start it: both earlier episodes hand off with the marker back under `world1`, the start anims leave it active, marker travel 55.98 m over the lift, hand-back 1.88 m off where the drop left it and 263.3 m from the hangar (red at three checks without the fix). 14 cutscene, landings and intro suites; `dotnet test` 2558.

**⚠ Traps.** A suite that reads the marker on the frame after the handoff measures its trip home, not the re-placement: remember the pose from the last playing frame (`landings-docking-hold` had the same fault). The suite also has to start from a world with earlier-cutscene history, or the stranding cannot happen at all.

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

## G60 ☑ `BL-607`: ownership is written by the landings trigger alone

**Goal.** An episode's raisers come from the started definition's closure on every path that starts a hosting definition, not the landings path alone.

**Evidence (confidence: traced).** F45 made an episode wait for every code-authoring definition in the started definition's closure, but only `LandingApproachRuntime` calls `Own`. The objective script's `WAKE_ANIM`, the ladder switch and the intro bootstrap all book the episode to the first raiser with that callee's own closure. A second gap: `Host` required the owner to be running, and the runtime is still null during the animation bind, so an intro could never have won the slot.

**Approach.** The slot moves onto the runtime (`AnimRuntime.MissionTriggerOwner`, called at the top of `PlayMissionTrigger`), so every trigger path books it; `WorldSession` hands the opening cutscene's own name to the same seam before the bind, since an intro starts inside the start-list walk; `Own` declines a definition whose closure authors no `CALLBACK`, because an ordinary `WAKE_ANIM` goes through the same call and a slot it claimed would outrank the next episode's real raiser.

**Model recommendation.** One session with `BL-608`, which shares the host.

**A refinement to the item's premise.** A census over every chapter's `objectives.zrd` `WAKE_ANIM` targets and their call closures finds six woken definitions that author a `CALLBACK` anywhere, and each has exactly one raiser: the multi-raiser shape exists on the landings path alone (CM06). C5/M02's ending is the shipped objective-path case of the shape the slot exists for, and the suite pins it.

**Verify.** `campaign-cutscene-ownership`: C5/M02's `nypd_southward` raises no code and calls `nypd_player`, which raises 11, 2 and 13; the episode books to `nypd_southward` and completes Won, while an ordinary `WAKE_ANIM` left running claims no slot.

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** An ordinary `WAKE_ANIM` must not claim the slot, or it outranks the next episode's real raiser. The intro's first code arrives while the host has no runtime to ask, so the slot wins outright in that state.

## G61 ☑ `BL-608`: CM02's bailing crew and its docking on the Pandora

**Goal.** Three parachutists appear beside the aeroplane at the end of the capture and drift for their whole scripts, and landing the captured Balmoral on the Pandora ends the mission.

**Evidence (confidence: traced).** The chutes: `ww_player` authors three `ww_chuteman` calls at `AT_NODE wingwalk_parent (0,-2,8.5)`, at `Animation+14.76`, `Event+1.0` and `Event+2.0`. `chuteman` has no node in C3's gamez at all (`AircraftStage` stages it from the aircraft archive), so the library-root resolver declined it, the figure was never moved to the call's site and played at the archive's own origin, and calls two and three hit the live-instance guard and did nothing: one figure at (0,0,0) for the whole run. The docking: the movie is not truncated (`hooked_to_klondike` runs 12.6 s on `player_balmoral`'s branch, calls `bal_wing_foldup` at 10.67 s and both wings reach the authored `1.9198622` rad, the branch's `Event+2.0` matching the fold's own `run_time`). What was missing is the last thing the definition does: `all_done` raises `Callback 13`, the mission-completion code (`FUN_0047e080` case 13 into `FUN_00463c10(1)`, then the mission-end path `FUN_00443090`), which reached no host in CSVM, so the movie ended and gave the player flight back inside the zeppelin. Nothing in the shipped objective data completes on a landing, so code 13 is the only ending the 20 missions carrying `hooked_to_klondike`, C4/M01's `carpkup_player` and C5/M02's `nypd_player` have. Disproved from the brief: a `RESET_STATE` on `RESET_TIME 0`, the `SequenceRunner` last-event rule, a dropped `WAIT`, and F54's freeing of the hidden Balmoral's children (the chutes hang off `chuteman`, not the bomber).

**Approach.** `WorldSession.ResolveLibraryRoot` serves the staged actor and keys copies on the authored call event as well as the anchor; the mission-trigger right is remembered as the trigger's closure rather than a call-stack depth, since these calls fire 14.8 s after that dispatch returned; `data/effect_pools.json` sizes `chuteman` at 3, the authored call-site count. `CutsceneController.MissionComplete` hosts code 13 into `ObjectiveGraph.NotifyDockingComplete`.

**Model recommendation.** One session with `BL-607`.

**Verify.** `campaign-capture-chutes` (chutes started at 14.75, 15.77 and 17.78 s, three visible at once, each placed at the walk frame, spans 15.2, 14.2 and 12.2 s) and `landings-balmoral-dock` (fold 10.67 to 12.68 s, completion code at 12.62 s, `rwingbend` at the authored angle, the mission ends).

**Verified.** On the merged plan tree, the full gate: build clean, 2561 unit tests, 179 engine suites in four shards with the error census clean (engine stage 103 s against its 100 s budget, awareness only), 16 goldens hash-identical.

**⚠ Traps.** A third caller of `ResolveLibraryRoot` must pass the authored event, or `null` to keep one copy per anchor. The staged actor is served only to a call that names a site; a site-less call gets the pre-pool behaviour (`BL-621` is what happens otherwise).

## G62 ☑ `BL-620`: CM02's ending waits out a wrap-up the completion code does not take

**Goal.** CM02 ends once, on the frame the docking film's last sequence raises its completion code, rather than three seconds later.

**Evidence (confidence: traced).** C3/M05 has two paths to the same ending: `OBJECTIVE19` (`ANIM_STATE [ANIM [NAME [hooked_to_klondike], STATE [EXECUTED]]]`, `INSTANTWIN`) and the `Callback 13` the definition's `all_done` raises. The code always gets there first: `all_done` is the definition's own last sequence, so the definition still reads RUNNING when the code lands (measured: state 2 at t=12.62) and the objective's condition cannot be met until a frame later. The original's case for the code (`FUN_0047e080` case 13) re-syncs the player vehicle (`FUN_00494b20`), sets the won flag (`FUN_00463c10(1)`) and calls the mission-end path `FUN_00443090` in the same breath, never touching the wrap-up timer at mission `+0xc40` that `FUN_0046ba10` runs down (0.1 s for `INSTANTWIN`, 3 s otherwise). CSVM gave it the ordinary 3 s, so the debrief opened at 15.62 s where the original opens at 12.62 s. Disproved: the EXECUTED read is not early (one def of that name, and the original reads the definition's own state byte `+0xa0`, not its call closure's), and the mission never ended twice or raced.

**Approach.** `ObjectiveGraph.NotifyDockingComplete` ends on `DockingWrapUpS = 0f`. The 0.06 s between the win and `bal_wing_foldup`'s last frame is authored: `move_camera`'s branch reaches `all_done` before `move_player`'s `Event+2.0` one, and the fold's 2.01 s `run_time` overruns the branch's 2.0 s wait in the data.

**Model recommendation.** A single session; the decode was one case of the code dispatch.

**Verify.** `landings-balmoral-dock` (the definition reads RUNNING when it raises the code, EXECUTED no earlier than that, the outcome turns Won within a frame of the code, `MissionEnded` fires once, a second completion code is refused) and a new `ObjectiveGraphTests` pin.

**Verified.** <pending orchestrator run>

**⚠ Traps.** `WonWrapUpS` is still the ordinary objective win's 3 s; the two must not be re-merged. `Ending` is never observably true on this path, so a check written as `Ending || Outcome == Won` needs a `Step` after the code before it reads.

## G63 ☑ `BL-621`: CM07's hangar drop loses its parachutist to the staged-actor pool

**Goal.** The pilot parachutes into the hangar again, while CM02's three sited chute calls keep their per-call copies.

**Evidence (confidence: traced).** `hangar_drop` calls `hdchute1` with no `AT_NODE` or `WITH_NODE` site at all, and `hdchute1` is rooted on `chuteman`, the actor `AircraftStage` stages out of the aircraft archive; its script poses `chutemanparent`, `pilot` and `stamp` with `OBJECT_MOTION_SI_SCRIPT` in world coordinates and never moves the root. G61's resolver served that staged actor to the site-less call, so the call relocated the root onto the caller's own anchor and pinned it top-level: the world-posed descent landed at (-8636, 292, -12790), 7.7 km off the hangar, and the never-started twin leg `hdchute1b` took a second pool slot, leaving a frozen duplicate drawn. Disproved: the drop places nothing by `OBJECT_ADD_CHILD`, and the mission-trigger right is not what opened the branch, the range gate having opened it already.

**Approach.** The distinguishing rule is whether the call names a site: `AnimRuntime` passes the authored event to `ResolveLibraryRoot` only for a sited call, and the pool declines the staged actor outright to a site-less caller, which also covers the add-child fallback.

**Model recommendation.** A single session; the regression is one branch of G61's own change.

**Verify.** `campaign-hangar-handover` gains a chute sampler (one `chuteman` in the world, the actor never off its staged pose, the figure drawn on all 702 leg frames, 27.4 m of descent, 63.3 m from the hangar; FAIL with the guard reverted) and `campaign-capture-chutes` is unchanged.

**Verified.** <pending orchestrator run>

**⚠ Traps.** `hdchute1b`'s prerequisite fails in this mission, so it never starts; a pool that hands it a slot anyway leaves a frozen duplicate in the world.
