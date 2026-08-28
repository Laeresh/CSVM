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
what its own symptom needs and hands anything deeper back to `BL-523`.

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

1. ☐ `BL-513`: the persist-log replay runs the previous mission's death choreography at mission open
2. ☐ `BL-521`: the barrage balloons stand at mission start
3. ☐ `BL-567`: the Pandora's broadside cannons fire on the player
4. ☐ `BL-568`: the Pandora starts moored in the dry dock instead of flying in
5. ☐ `BL-512` + `BL-578`: `ObjectMotion`'s `rnd_xz` makes the Barracuda and the CM08 tanker jump
6. ☐ `BL-522` + `BL-527`: a surface generator's launch does not fly its take-off run, so the fighters die on the deck
7. ☐ `BL-569` + `BL-579`: a cutscene called from a start anim fires at bootstrap with no camera (CM04 and CM09)

### Wave B — CM06 (C1C/M01) and CM07 (C1/M02)

11. ☐ `BL-571`: a carried turret's death fire, and the turret, stay where the turret died
12. ☐ `BL-572`: an objective marker labels the raw node name instead of the original's verb and proper name
13. ☐ `BL-573`: the AA guns damage themselves firing at a barrier
14. ☐ `BL-574` + `BL-575`: the hangar hand-over gives a stock Bloodhawk and plays no animation
15. ☐ `BL-526`: the rope ladder never deploys (a native per-mission switch to decode)

### Wave C — CM08 (C1B/M03) and CM09 (C1/M04)

21. ☐ `BL-576`: the Pandora pitches steeply up and down along the Klondike net
22. ☐ `BL-577` + `BL-564`: no runtime for a roster or generator surface vehicle, so the patrol boats never spawn
23. ☐ `BL-580`: `eairg32`'s launch falls back to a `player_bhawk` on a misspelt parameter block
24. ☐ `BL-582`: a `[parent, child]` objective target is flattened into two bare names
25. ☐ `BL-581`: the docking objective is never reached after the radio tower goes down

### Wave D — the sortie

31. ☐ Fly CM04 to CM09 end to end and judge every item where it was reported

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
everything.

---

# Wave A — CM04 (C3/M03)

## A1 ☐ `BL-513`: the persist-log replay runs the previous mission's death choreography at mission open

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
trigger. `<TODO: re-verify still-open against the code>`

**Approach.** Give `ApplyTo` a silent path: set the pool to `Destroyed`/HP 0 (or the carried
partial HP with its damage stages), apply the destroyed role swap the death sequence ends in, and
run no effects, sounds or choreography; `DamageAt` stays the weapon path. A partially damaged
carried object (`state.Health` above zero) wants its stage visuals without the stage's puffer
bursts. Decode lane: how the original's `PERSIST_LOG` reader applies a carried state
(`docs/formats/saved-games.md`, `docs/formats/destructibles.md` "Starting destroyed").

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** A headless C3/M03 open on a profile that finished C3/M02 with those objects destroyed:
no `damage:` line and no effect-pool recycle for any carried object on the first frames; the pool
reads `Destroyed` so a later `DamageAt` on `aagun30` is a no-op (assert it in the
`start-state-swap-pool` suite family). `<TODO: name the exact suite and the profile fixture>`

**⚠ Traps.** The replay must still leave the pool `Destroyed` so a later hit is a no-op; do not
fix it by skipping the replay for destroyed objects, and do not gate on the visual state alone.
Absence of a log line is not evidence unless the sink carries that line (`GD.Print` lines do not
reach the file sink).

## A2 ☐ `BL-521`: the barrage balloons stand at mission start

**Goal.** CM04's barrage balloons (`bont1..6`/`b_turret1..6`) are down at mission open and are
not in the AI gunners' target pool.

**Evidence (confidence: traced, two leads still open).** The log shows AI gunners engaging them as
live turrets on the first frames (`ai gunner: shooter 100 targets MSG_TUR_DEFENSE_BALLOON@b_turret3
at 1027 m`, then `b_turret5`, `b_turret4`) although `support\c3\m03.gw` switches them fully OFF
(`NodeSetActive off`). Later, `anim: WAIT_FOR_COMPLETION on 'balloon_downa*' had nothing to hold —
no live callee instance`: something in M03 calls the `balloon_downa*` defs, which live in
`data\common\zrdr\turrets\balloon_down.zrd`, compiled only into M02's `mis_anim` and superseded in
M03 by the mission's compiled manifest, so the call reaches nothing.
`<TODO: re-verify still-open against the code>`

**Approach.** First confirm which nodes the `.gw` OFF switch reaches (`mission setup: ... 36
node(s) deactivated`) and whether a deactivated `b_turret*` stays in the turret target pool with
its balloon visible; then find the caller of `balloon_downa*` in M03 (a chapter-scope reader def or
the turret def itself) and settle whether the original resolves that call against the chapter's
reader set where CSVM's manifest supersession drops it. A1's pool sync applies once the trigger is
a role-named swap. Decode lane: the original's reader-def lookup order when a mission manifest
supersedes a chapter def.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** Headless C3/M03 open: no `ai gunner ... @b_turret*` line, no `WAIT_FOR_COMPLETION on
'balloon_downa*' had nothing to hold`, and the balloon nodes inactive in the node census; a
`--freecam --chapter=C3` regression with unchanged counts. `<TODO: the suite to extend>`

**⚠ Traps.** `PLAN-c3-balloon-kill-chain.md` settled the balloons' kill chain for C3/M02; that is
the live kill path, not this mission's start state, so do not reopen it.

## A3 ☐ `BL-567`: the Pandora's broadside cannons fire on the player

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
`<TODO: re-verify still-open against the code>`

**Approach.** Re-read `FUN_004bd430` for the list it walks, and `FUN_004bfe00` for what an
unresolved pair does at fire time. If the roster is zeppelins only, `ResolveTarget` drops
non-zeppelin names and `docs/formats/mission-entities.md` "Broadside firing" is corrected. If
`player` does resolve, record how.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: a headless CM04 run with the player inside broadside range shows no wep_28 hit
on P1; name the suite or the log check>`

**⚠ Traps.** Do not add a hostility or team gate; `BL-517` found none in the engine, and the
lever is the resolver's candidate set.

## A4 ☐ `BL-568`: the Pandora starts moored in the dry dock instead of flying in

**Goal.** Over CM04's first minute the Pandora flies from `(-11314,554,-13697)` to the dry dock
along `pzep_todrydock`'s SI script, then holds its record seat on net `M3PirateZep`.

**Evidence (confidence: traced to the data, mechanism lead-only).** M03's `NEW_GAME_START` runs
`pzep_todrydock` (`extracted/C3/M03/mis_anim/piratezep-pzep_todrydock.json`, one
`ObjectMotionSiScript` on `piratezep`), whose script
(`data-c3-m03-zrdr-zeps-pzep_todrydock-piratezep.zan.json`, 185 frames, 0 to 61.65 s) ends at the
record's seat `(-12400.9,150.3,-10355.2)`, yaw -180. CSVM spawns the zeppelin at the record
position and the script never takes its pose, or the net follower writes over it.
`<TODO: re-verify still-open against the code>`

**Approach.** An `ObjectMotionSiScript` on a zeppelin node owns that zeppelin's pose for the
script's duration, starting at frame 0's base, with the net follower parked and resuming from the
script's last frame. Check `ZeppelinRuntime`'s placement against the scripted-path snap (`BL-531`'s
fix) and the dead-end hold (`BL-529`'s fix) first; neither should apply to a scripted motion.
Decode lane: `docs/org/objectMotion.md` for who owns a node's transform while an SI script runs.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `--anim-lab --node=piratezep` on C3/M03 playing `pzep_todrydock`: the pose at t=0 is
the script's frame 0, at 61.65 s the record seat, and the net follower's first write comes after
the script ends (log it). `<TODO: the suite to extend>`

**⚠ Traps.** Do not move the record's position to the path start; the record's seat is data and
the script is what flies it.

## A5 ☐ `BL-512` + `BL-578`: `ObjectMotion`'s `rnd_xz` makes the Barracuda and the CM08 tanker jump

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
`<TODO: re-verify still-open against the code>`

**Approach.** Settle in `docs/org/objectMotion.md`'s routine whether `rnd_xz` is a random spread
or a cached unit direction the original reads back; the change belongs to `ObjectMotion` as a
whole, not the submarine. For the heading, compare the hull's local -Z against the bay opening and
the `bauda_aip*` path direction in the built world, not the def. For the tanker,
`--anim-lab --node=tanker` on C1B, play `freightercruise`/`freighterwavemotion`, log the pose at
each event boundary; if the discontinuity is `rnd_xz` it is the same fix, if a follower writes the
pose, exclude a node an `ObjectMotion` owns from the follower.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** The anim lab pose log for both nodes shows no discontinuity above the authored 1.2 m
residual at any event boundary; the Barracuda's -Z is within the bay opening's bearing at the end
of the drive. `<TODO: the suite asserting MotionRuntime's rnd_xz reading>`

**⚠ Traps.** Do not special-case the submarine or the tanker; do not "fix" the 1.2 m residual,
which is authored. The cargo-crane choreography between the Pandora and the tanker is untested
and belongs to this item's check.

## A6 ☐ `BL-522` + `BL-527`: a surface generator's launch does not fly its take-off run, so the fighters die on the deck

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
(`BL-515`). `<TODO: re-verify still-open against the code>`

**Approach.** Decode the consumer of `+0xc8`/`+0xcc` on the aircraft record and fly the path,
releasing to the patrol net at its last point. While the run is unbuilt, a launched aircraft
standing on its own host's colliders must not count as a ram; that interim rule is the fallback
if the decode does not fit this run. This is the fix for `BL-527`; no `CampaignRoster.cs` change.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** Headless CM04 and CM07 runs with the generators woken: every `*_eg*` launch reaches
flying speed with no `AI ram` line on its host's colliders and joins its net; the launch-pose
suite unchanged. `<TODO: how to wake the generators headless without playing the mission
(WAKEUP_GENERATOR credits)>`

**⚠ Traps.** The zeppelin launch-altitude gate is an airship rule, not a general one; do not lift
the launch by borrowing it, do not add a spawn-height offset beyond the decoded 0.2 m, and do not
give the aircraft a starting speed (the zero velocity is decoded). No blanket spawn lift
(`BL-457`).

## A7 ☐ `BL-569` + `BL-579`: a cutscene called from a start anim fires at bootstrap with no camera (CM04 and CM09)

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
`<TODO: re-verify still-open against the code>`

**Approach.** Settle how the original runs a `camera1`-object cutscene called from a start anim
(the registration sites `docs/formats/anim-definitions/cutscenes.md` lists) and route both through
the cutscene runner with the world held. Run CM09 headless with `--debug-anim` and read the def's
event log against the def. Whether the original plays `pure_panic` in CM09 is part of the question.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** Headless CM04 and CM09 opens show a cutscene hold and handoff line for the named def,
the one-shot sounds positioned in-tree, and the skip working; the `campaign-*` cutscene suites
unchanged. `<TODO: the suite to extend>`

**⚠ Traps.** The cargo zeppelin's destruction already runs (fireballs, `tntbox`/`gasbag`
deactivation); do not run it a second time under the camera. `BL-548`'s deferred `--pos=` handoff
is for `generic_intro`; these intros are their own defs.

# Wave B — CM06 (C1C/M01) and CM07 (C1/M02)

## B11 ☐ `BL-571`: a carried turret's death fire, and the turret, stay where the turret died

**Goal.** A gun ring shot off the Workers' Voyage burns and stays on the hull as it flies on.

**Evidence (confidence: traced for the fire, lead-only for the turret).** The ring's destroy def
calls `large_30sec_fire` `WithNode doublecannon4 (0, 2, 0)`. `AnimRuntime`'s `CallAnimation` hands
a death's effect call to the world-effects runtime through `ExternalEffect` with a world POSITION
snapshot (`VisualOriginOf(callAnchor) + basis * offset`), and `PlayEffectAt` stages the template
root at that point with `TopLevel = true`; the site node rides along only as the callee's
`INPUT_NODE`. The log's `WAIT_FOR_COMPLETION on 'large_30sec_fire' not held — the callee is routed
to the world-effects runtime` is that hand-off. `BL-514`'s disproof examined the `PUFFER_STATE`
path, which does re-read the host; the death-call path is this one. Whether the turret model is
held back the same way is not traced. `<TODO: re-verify still-open against the code>`

**Approach.** An effect called `WithNode` on a node that moves must follow it: parent the staged
template root under the site node, or feed `EmitterDirector` the site's live transform each tick,
keeping `TopLevel` placement for world-fixed sites. Then read how the ring's destroyed pose is
placed and give it the same rule.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: a WorldDamageLab or headless CM06 check that the fire's position tracks
doublecannon4's world position over the 30 s while the airship moves; the golden set unchanged>`

**⚠ Traps.** `trail-world-anchor` settled the opposite case (an emitter that must NOT ride its
host); keep both. Do not reopen `BL-514`'s `PUFFER_STATE` reading.

## B12 ☐ `BL-572`: an objective marker labels the raw node name instead of the original's verb and proper name

**Goal.** An objective marker reads as the original's: a category, an action verb in brackets, the
target's proper name, coloured by the action (`Zeppelin [Disable] Worker's Voyage` in red, `[Dock]
Worker's Voyage Docking Hook` in blue), with the node name kept for the debug tag only.

**Evidence (confidence: lead-only on the format, traced on the strings).** The log's `targeting
hud: P1 brackets on peoplehook at 999 m` lines show `TargetRef.DisplayName` carrying the node name,
`TargetHud.LabelLines` drawing it, and `HudGreen` applying as to any friendly. The strings exist in
`extracted/messages.json` (`MSG_OBJ_DOCK` 8003, `MSG_OBJ_DISABLE` 8006, `MSG_OBJ_WVOYAGE` 8025,
`MSG_OBJ_WVOYAGEHOOK` 8027, `MSG_OBJ_KLONDIKEHOOK` 8017). `objectives.zrd` only names the node, so
the node-to-name and node-to-verb maps live elsewhere. The marker format rests on the user's
recollection. `<TODO: re-verify still-open against the code>`

**Approach.** Decode where the objective marker's verb and proper name come from for a target node
(a vehicle or zeppelin record field, or a table `crimson.exe` indexes by node name) and what sets
the marker colour; give `TargetRef` an objective display line built from those message ids. Lands
after C24 so it labels the resolved node.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: the targeting suite asserting the label lines for CM06's three objective
targets, plus the flown check in D31>`

**⚠ Traps.** `BL-397` is the marker's bracket range rule and not this. The format is a
recollection until decoded; do not build the exact layout from it.

## B13 ☐ `BL-573`: the AA guns damage themselves firing at a barrier

**Goal.** CM07's AA guns do what the original's do when a structure blocks their line: they do not
blow themselves up.

**Evidence (confidence: lead-only).** The log shows `aagun32` taking four hits with no player
round near it (`-10`, `-9.58`, `-9.2`, `-10 ... DESTROYED`), then `aagun33`, `aagun34` and
`aagun36` taking the same `-10`, `-9.58` pair; identical decrements across four guns read as one
weapon's rounds bursting on the obstruction and splashing the shooter. Not traced: whether the flak
burst excludes its shooter in `crimson.exe`, and whether the gun fires at all with a structure in
its line. `<TODO: re-verify still-open against the code>`

**Approach.** Trace which shooter id lands those hits (`--debug` hit logging on the turret pool),
then decode the flak burst's damage application for a self-exclusion and the turret fire gate for
a line-of-fire test (`docs/org/weaponImpact.md`); apply what the decode says.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: headless CM07 with the turrets woken shows no self-attributed damage on
aagun32..36; name the suite>`

**⚠ Traps.** Do not exclude turrets from splash wholesale; a rocket into a gun pit must still
kill it.

## B14 ☐ `BL-574` + `BL-575`: the hangar hand-over gives a stock Bloodhawk and plays no animation

**Goal.** CM07's hangar cutscene animates (doors, lift, the drop) and hands the player the Blue
Streak build (engine 4 nitrous, twin 40 and twin 30 guns, 1/1 hardpoints) in the livery the
original draws.

**Evidence (confidence: traced to the swap, lead-only on the livery and the animations).** The
log: `EXECUTION_BY_RANGE reached - starting hangar_drop at 67 m`, `airframe swap: P1 is now flying
'player_bhawk'`, `4 call(s) retargeted onto a named node`, and no line for the hangar's own motion
or a cutscene hold. `AirframeSwapCodes` maps code 965 to `pbloodhawk`/`player_bhawk` and
`FlightRoster.SwapPlayerAirframe` assembles it through the ordinary player build with the shared
paint stream. The Blue Streak template is `docs/org/hangar.md`'s `0x0061a9b8`. Not traced: which
defs `hangar_drop` calls and whether they are among the `431 reader def(s) superseded by this
mission's compiled manifest` (A2's drop) or run on nodes the cutscene reparents.
`<TODO: re-verify still-open against the code>`

**Approach.** Decode what swap code 965 builds in the original (the template at `0x0061a9b8` or a
stock def) and which skin it draws (the swap code's own skin set, `blake*` in the faction table,
or the cutscene's captured rig); have the hand-over assemble `CustomPlaneBuild` from
`CampaignProgression.AwardBuild`'s template with `Nitro.Installed` in that livery. For the
animations, read `hangar_drop`'s call list from `extracted/C1/M02/mis_anim`, run headless with
`--debug-anim` to the hangar, and trace the first callee that does not start.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: the swap suite asserting the post-swap build and skin; the anim log showing
every hangar_drop callee starting; D31 judges the livery>`

**⚠ Traps.** Do not give the stock Bloodhawk nitro, and do not touch the post-mission grant
(`BL-528`), which is correct. The swap itself works and must stay.

## B15 ☐ `BL-526`: the rope ladder never deploys (a native per-mission switch to decode)

**Goal.** The decode of the original's ladder switch is recorded under `docs/org/` far enough
that a CSVM equivalent can be built, and if it fits this run, the ladder deploys in CM07 when the
player is level and close.

**Evidence (confidence: traced in the decompile).** `drop_ladder`'s one xref is inside
`FUN_004735b0`, a hardcoded C1/M02 mission-init function resolving `drop_ladder`/`retract_ladder`
into a heap object (`DAT_0071c324`) that the world tick `FUN_004897c0` drives every frame, gated on
an attitude test (`0.707 < player_field[100]`, cos 45°) and a proximity/membership test
(`FUN_00471690`), calling deploy or retract through the switch object's vtable; never through
`CALL_ANIMATION`, `pickups.zrd` or `landings.zrd`. Not an event-kind handler.
`<TODO: re-verify still-open against the code>`

**Approach.** Decode `FUN_00471690`'s membership test and `player_field[100]`'s exact meaning;
write them up as the ladder switch's rule. Then, if the rule is small, model it as a per-mission
native gameplay object evaluated every tick, calling the existing `drop_ladder`/`retract_ladder`
defs. A docs-only landing with the build handed back to the backlog is an acceptable outcome.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** `<TODO: depends on whether code lands; at minimum the decode page and D31's flown
pickup>`

**⚠ Traps.** `BL-035`'s dropped kinds play no role; do not build this as an anim event.

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

## C22 ☐ `BL-577` + `BL-564`: no runtime for a roster or generator surface vehicle, so the patrol boats never spawn

**Goal.** CM08's `patrolboat_1..4` spawn on their nets at water height, drive them, take damage and
die as their `patrolboat-*` defs expect; CM12's `eshipg31` launches patrol boats at the pirate
ship, not Bloodhawks at the world origin.

**Evidence (confidence: traced).** `aiv.zrd` carries four enabled `patrolboat_1..4` blocks at
`y = 0` on nets `Patrolboat1..4`; `objectives.zrd` wakes them (line 164) and moves them (241 to
253). `CampaignRosterPlan.Build` reports a surface-vehicle block in `Skipped` (`'{def}' ({mode})
has no player airframe`) and never spawns it. In CM12 `Eshipg31_params` resolves to
`patrolboat_eg0`, also skipped, so `GameSession.SpawnFromGenerator` falls back to
`SessionSpec.GeneratorsPlane`; and the host `eshipg31` is a model-less group node with a zero
local translation whose geometry sits at its bbox, so `AiGeneratorRuntime.Spawn` drops at
`(0,0,0)`. `<TODO: re-verify still-open against the code>`

**Approach.** A surface vehicle runtime for roster blocks: spawn the def on its net at water
height, drive it with the scripted-path follower's law (`docs/org/flightModel.md`), and give it
the turret and destructible wiring the `patrolboat-*` mis_anim defs (`ptboat_50damage`,
`ptboat_75damage`, `emit_ptsplash*`) expect; the generator case is the same runtime launched.
Decode the original's launch position for a model-less generator host (`FUN_00452450`: the
node's world matrix or its bbox centre). At minimum, refuse the fallback airframe for a surface
def so a boat generator launches nothing rather than fighters.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** Headless CM08 open shows four boats spawned on their nets at water height and the
wake reaching them; headless CM12 launches no `player_bhawk` from `eshipg31`. `<TODO: the roster
suite to extend and the ram-terrain check at the origin>`

**⚠ Traps.** Do not spawn a boat as an aircraft with a low ceiling, and do not hand `eshipg31` a
fighter def. CM12's three Bloodhawks are group 3 and never count toward "Destroy all enemy
fighters"; killing them is not progress.

## C23 ☐ `BL-580`: `eairg32`'s launch falls back to a `player_bhawk` on a misspelt parameter block

**Goal.** A campaign generator whose `vehicle.params` label resolves to no roster block does what
the original does with it, and never invents a player-airframe launch.

**Evidence (confidence: traced).** `egen.zrd` names `Eairg32_params` (line 54) while `aiv.zrd`'s
label table spells it `Earig32_params` (slot 31), so `CampaignRosterPlan.GeneratorTemplates` has no
entry and `GameSession.SpawnFromGenerator` falls back to `SessionSpec.GeneratorsPlane`; the log
shows `player_bhawk_eg1` launched beside `blakepeace_2_eg0`. `eairg31`'s `Eairg31_params` matches.
`<TODO: re-verify still-open against the code>`

**Approach.** Decode what `crimson.exe` does with an unresolved `vehicle.params` label
(`FUN_00452450`'s caller chain on the generator record): a silent no-launch, the first block of the
def, or the same generator's other host. Match it and drop the airframe fallback for campaign
generators.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** Headless CM09 with the generators woken: no `player_bhawk_eg*` launch; the
`generator-roster-params` suite extended with the unresolved-label case.

**⚠ Traps.** Do not "fix" the data spelling; the shipped file is the reference and the original
ran with it.

## C24 ☐ `BL-582`: a `[parent, child]` objective target is flattened into two bare names

**Goal.** `ADD_OBJECTIVE_TARGET [[piratezep, rock_zeppelin]]` marks the one `rock_zeppelin` under
`piratezep`, so CM09 shows one Defend marker; a bare name keeps today's global match.

**Evidence (confidence: traced).** `ObjectiveScript.ReadNames` flattens a nested list into its
strings, so `AddObjectiveTarget` holds `piratezep` and `rock_zeppelin` as two names, and
`ObjectiveGraph.IsObjectiveTarget(name)` matches any node by bare name: the Pandora's root and a
ground `rock_zeppelin` near the enemy zeppelin both light up. `REMOVE_OBJECTIVE_TARGET` and
`ADD_OTHER_TARGET` read the same way (`ObjectiveSites.Holds`); C1C/M01's `[[wv_tailhook,
peoplehook]]` is the same shape. `<TODO: re-verify still-open against the code>`

**Approach.** Read a nested pair as a path (`Parent`/`Child`), resolve it to the one node under
that parent (`AnimRuntime.FindNodes` scoped to the parent's subtree), and mark that node only.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** A unit test on `ReadNames` with `[[a, b]]`, and an objective-sites test on a world
with two `rock_zeppelin` nodes marking only the child of `piratezep`.

**⚠ Traps.** The help label applies to the same resolved node, not to the parent.

## C25 ☐ `BL-581`: the docking objective is never reached after the radio tower goes down

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
sink, so the sortie cannot settle it. `<TODO: re-verify still-open against the code>`

**Approach.** First route the graph's transitions through `Log.Info("campaign", ...)`. Then
reproduce headless: complete 15 inside 16's window, then 23/24/28 (a Promised Land hatch down,
group 2 to two), and assert 20 wakes 90 s after 19. Fix `Ticks`' interaction with a nap that lands
on a gated objective per the decode (`docs/formats/objectives.md`'s `TICK_DEPENDS_ON_OBJ` row):
whether a nap timer set while 29 was dormant is dropped rather than resumed, or 19's nap of 20
never starts because 19 is gated when 17 wakes it.

**Model recommendation.** `<TODO: not settled this session>`

**Verify.** The headless reproduction above as an objective-graph unit test; a headless CM09 log
carrying the objective transitions. `<TODO: the fixture that drives the graph without the
engine>`

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
