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
branches (`bl-431-cockpit-gauge-drive`, `worktree-m5-campaign`) hold no unmerged commits. None
was re-verified against the code; each item carries a `<TODO: re-verify still-open against the
code>` for that half. Evidence grades are capped by provenance: an item whose backlog entry rests
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

21. ☐ `BL-576`: the Pandora pitches steeply up and down along the Klondike net
22. ☑ `BL-577` + `BL-564`: no runtime for a roster or generator surface vehicle, so the patrol boats never spawn
23. ☑ `BL-580`: `eairg32`'s launch falls back to a `player_bhawk` on a misspelt parameter block
24. ☑ `BL-582`: a `[parent, child]` objective target is flattened into two bare names
25. ☑ `BL-581`: the docking objective is never reached after the radio tower goes down

### Wave D — the sortie

31. ☐ Fly CM04 to CM09 end to end and judge every item where it was reported

### Wave E — CM13 (C2/M03), the blocker

41. ☑ `BL-585`: AI planes never lock onto a `dzpath` and fly it on rails, so the CM13 racers skip the danger zones

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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
**Verified.** <pending orchestrator run>

**⚠ Traps.** `BL-035`'s dropped kinds play no role; do not build this as an anim event. Do not
teach the cutscene host code 123: it is the switch's settle signal, and the switch answers it
first.

# Wave C — CM08 (C1B/M03) and CM09 (C1/M04)

## C21 ☐ `BL-576`: the Pandora pitches steeply up and down along the Klondike net

**Goal.** The Pandora follows `Klondike1`'s altitude steps the way the original steers pitch for
route following, not at 30° nose-down toward every lower node.

**Evidence (confidence: seen at the controls, mechanism lead-only).** The screenshot
`Screenshots/crimsonskies_2026-08-27_23-47-46-050.png` (run-4 worktree) shows `piratezep` nose down
about 30° along the green `Klondike1` segment. `Klondike1` is a 13-node open chain swinging between
about 400 m and 93 m; the record's pitch band is -30 to 30 at `max_rate_pitch` 5. `BL-529` fixed
the dead-end shuttle; the up-and-down along the route was the report's first half.
`<TODO: re-verify still-open against the code>`

**Approach.** Read `FUN_004bf9d0`'s pitch term against `ZeppelinMotion`: whether the original
steers pitch at the node's altitude difference directly, clamps it under a smaller authored limit
for route following, eases altitude over the edge length, or whether a zeppelin net carries its
own altitude field. Then match.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: a headless CM08 pitch trace along Klondike1 against the decoded law; the
zeppelin motion suites unchanged where the law is unchanged>`

**⚠ Traps.** The initial-pitch clamp that never fires (`ZeppelinMotion.cs`) is decoded verbatim
and stays. Do not flatten the net.

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: the checklist of per-mission look-fors, assembled once the items have landed>`

**⚠ Traps.** A live symptom is evidence about the build that was running: confirm no testing
worktree is open before minting. `<TODO: the per-mission launch commands>`

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

**Verified.** <pending orchestrator run>

**⚠ Traps.** Do not invent the entry condition (the prohibition on `AiModeMachine` stands until
the decode replaces it). Do not use polygon index to find the route ribbon; classify by
material (`docs/formats/missions.md`). The splitscreen race countdown `BL-314` is a different
"on rails" and stays separate. `BL-523` (the patrol/pursue cycle) is out of this plan and must
not be pulled in through the shared mode machine.
