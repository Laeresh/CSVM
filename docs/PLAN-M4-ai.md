# Milestone 4 — Artificial Intelligence

**ACTIVE PLAN** (scheduled 2026-08-13; written as a scoping study 2026-07-25, premises re-checked
2026-08-04, binary decode pass 2026-08-10, full premise + citation re-check on scheduling). It sits
in `docs/`, which by this repo's convention makes it a live plan; PROJECT_CONTEXT.md's "Current
status" names it. Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to
[`plans.md`](plans/plans.md), when every item lands.

This plan keeps its scoping-study shape: the dated Delta sections are the correction history and
win over older body text — read them before trusting any paragraph they name. Every code citation
was re-verified and re-pointed to the tree at `0b2385e` on 2026-08-13; a cited line is current as
of that commit. The ticked checklist items were front-loaded by other plans (A1 via PLAN-vs-mode,
D10 and most of A3 via the 2026-08-10 decompile pass), not started here. On scheduling, backlog
items `BL-068`, `BL-069` and `BL-347` were re-verified still-open and absorbed into this plan
(deleted there); `BL-065`, `BL-222`, `BL-226`, `BL-233`, `BL-291` and `BL-343` stay in
`backlog.md`, blocked on this plan's items — see the 2026-08-13 Delta.

## Why this document exists

The M4 cost estimate changes materially once you know one thing: **the shipped AI data is already in
our extraction, largely self-describing, and nothing in the engine reads any of it.** Turret
behaviour, patrol graphs, zeppelin combat parameters, per-pilot skill vectors and ~1,300 combat voice
clips are all on disk today. That fact previously existed only in a chat transcript. This document
makes it durable, corrects the parts of it that were wrong, and states plainly the single hard
prerequisite that gates the whole milestone.

**Source note.** The behavioural half is our own engineering re-expression of the design described in
the original *Crimson Skies* Game Design Document v1.03 (Zipper Interactive, 1999-07-23), read from a
local text extraction at `tools/cs_gdd_extracted/cs_gdd.txt` (git-ignored, not redistributable). **No
prose from that document is reproduced here** — field names, enumerations and numeric values are
recorded as facts; everything else is restated in our own words. Where the source is internally
inconsistent this document says so rather than picking a reading.

---

## Premise re-check — 2026-08-04

Every claim below was re-verified against the tree at `04d2dfc` (~60 commits after this document
was written). **Every architectural premise and the wave ordering survive.** What follows is the
complete list of deltas; the body text is left as written — read it through this lens.

**Citation drift (mechanical):** discharged 2026-08-13 — the body text below was rewritten with
current paths and line numbers when the plan was scheduled (the `PlaneViewer.cs` split into
`Session/GameSession.cs` / `Launcher.cs` / `FlightRigAssembler.cs`, the `CSVM/src/` root, the
`Mech3/Anim/` move, `SessionArchives.OpenFor`). One carried note: `SoundsOutliveBuild` is true only
for `ArchiveIntent.Lab`; B8's prewarm work happens under whatever intent the flight session opens
with (`Mech3/SessionArchives.cs:85`).

**Premises that materially improved:**

- **A2's "single biggest unknown" now has a demonstrated answer.** `AnimRuntime.IndexStage`
  appends to `_index` post-bootstrap and invalidates `_findCache` — a working, gated precedent for
  exactly the invalidation A2 needs. (The `FindAll` doc comment still claims the index is never
  added to; that comment is now stale in the code itself.)
- **The A2/C9 seam is no longer hypothetical.** `Flight/IncomingFire.cs` is a non-player fire
  source registered in `DriveSimSteps` ahead of the pool, honouring the `GameClock` contract and
  passing its own `ShooterId` — the exact controller shape this document proposes.
- **`ProjectilePool.Spawn` already carries a firer:** the signature is now
  `Spawn(weapon, muzzle, inheritVel, int shooterId = NoShooter)` (BL-087). The id is used only for
  near-miss audio self-exclusion and **never reaches the physics query**, so wrong-claim #12's
  substance stands (exclusion must still be added; the shared `_ray` still assigns only
  `From`/`To`) — only the "no owner info anywhere" framing is dated.
- **B8's "unprewarmed clip silently never plays" blocker is largely closed.** `WorldSounds.Prewarm`
  now decodes every name the loaded program can reference while the archive is open. A genuinely
  novel name after build still returns null, so the voice set must still join the prewarm set —
  but the failure is no longer the silent default.
- **Effect templates gained a pooled mode** (BL-225): simultaneous hits no longer collapse onto
  one site. Still true: a placed effect snaps to an absolute world point and does not track a
  moving host — the zeppelin constraint stands.
- **`AnimRuntime.PlayerPositions`** (nearest-of-a-set) now exists, but only the
  `EXECUTION_BY_RANGE` gate uses it; `PLAYER_RANGE` conditions, `ProjectilePool.Listener` and the
  `WorldSounds` listener are still player-one singletons.

**Premises that eroded:**

- **A3 shrinks from five families to three.** `docs/formats/mission-entities.md` now documents
  `zeppelins.json` and `egen.json`. Nets (`ne`/`neindex`), `aiv` rosters and `ai.zrd.json`
  turrets remain the gap.
- The banner's active-plan pointer was stale (fixed above); treat PROJECT_CONTEXT.md as the only
  authority on what is live.

**New fact M4's damage items must absorb:** the `destroyable_parts` pair is **(hit points,
armor)**, not two identical hp values (`a499e89`), and CAP-19 observed armour-first ordering with
a per-zone cap of 60. Neither is implemented, and `PlaneStats.cs` still carries the refuted
"identical values" comment and discards the second value. D14 / A4 / F18 inherit this.

**Verified unchanged (the load-bearing set):** aircraft still have no physics body; zero
`CollisionLayer`/`CollisionMask` assignments repo-wide; `ClassifySurface` still tops out at three
classes (and gained a second consumer, `FlightController`'s touchdown pick, blocked by the same
gap); `FlightModel.Step`/`FlightInput` still headless and AI-ready; four-rig splitscreen with zero
mutable statics in `Flight/` (including the post-doc `CameraController` and weapon-lab files);
`DestructibleRegistry` still single-scalar-health with no zone concept (A4 stands);
`Anchors` still refuses empty-NAME multi-target definitions (F18); dialogue-chain sound groups
still parsed-then-discarded; `DamagesZeppelin` still parsed-never-consumed; still no
subtitle/voice-line path. All data-side measurements are untouched (`extracted/` is static;
`analysis/m4-ai-data/` intact).

## Delta — 2026-08-06: the patrol-net half of A3 landed early

A pre-M4 debug-tool session (`--debug-ainets` / F13, `UI/AiNetsOverlay.cs`) landed the
`ne`/`neindex` decode ahead of the milestone. Read §1 ("Patrol graphs") through this lens:

- **Nets are now decoded, documented, and read by the engine.** Format page
  `docs/formats/ai-nets.md`; reader `CSVM/src/Mech3/AiNets.cs` (nodes, edges, raw undecoded
  tags, trailer); this section's counts (222 nets, 2,268 nodes, 2,149 edges, the per-chapter
  split, the 1:1 index) re-measured 2026-08-06 and asserted as goldens in
  `CSVM.Tests/AiNetsTests.cs`. The headline "nothing in the engine reads any of it" is no
  longer true for this family, and B5/F17's data seam exists. **A3's remaining gap: the
  `aiv` rosters, the `ai.zrd.json` turrets, and `--dump-ai`.**
- **Trailer correction.** §1's "TRAILER — `[-1]` on 133 files, otherwise
  `[nodeIndex, "nodeName"]`" hides five exceptions: **4 files carry `[-1, "name"]`** — a
  named follow target with no attach node (C2 net 21, C4 nets 6 and 24, C5 net 25, all
  zeppelin/player targets) — and **1 carries an index-only `[3]`** (C2 net 33). The full
  `[nodeIndex, "name"]` shape is on 76 files, not 81.
- **`neindex`'s first element is not a slot count.** §1's `[[ slotCount, id0, … ]]` gloss
  misleads: the leading number is an allocation figure ≥ the pair count (C1: 46 over 29
  pairs). Parse pairs to the end of the list, never the header.

## Delta — 2026-08-10: the binary names the roster, and three items collapse to data-loads

A Ghidra pass over `crimson.exe` answered more of this document's open questions than the whole
data survey did. **The single find that does it: the retail binary embeds its editor's own text
format comment for the roster file** (`.rdata:0x00622508`), naming **every `aiv` field in order**.
Everything below follows from that plus the shipped files it points at. The decode is now a format
page — [`docs/formats/ai-rosters.md`](formats/ai-rosters.md) — and this section is only the
milestone consequences. Read §2 ("AI vehicle rosters") and the D-wave items through this lens.

**Three items stop being reverse-engineering and become reading a file:**

- **D10 (skill-slot mapping + scale) is answered, not "budget for investigation".** Slots 22–30 are
  `dare_devil natural_touch sixth_sense dead_eye quick_draw steady_hand stun_recovery talker
  constitution` — document order, exactly as §2's hypothesis had it, and every discriminating case
  the survey named lands correctly (the cabbie's two 3s on `dead_eye`/`quick_draw`, the stunt
  plane's lone 9 on `steady_hand`, the 89 mooks' lone 1 on `dead_eye`). **The two "default 5" slots
  are `talker` and `constitution`, not the signature stats** — §2's closing paragraph about
  "signature" defaults is wrong and its inference from it should be dropped.
- **The scale conflict is dead, and the constants are shipped.** `extracted/zrdr/player.zrd.json`
  carries an **`ai_skill_parameters`** block: a `[value@1, value@9]` interpolation pair per stat
  (`dead_eye_angle` 4.0°→1.45°, `quick_draw_angle` 50°→89°, `steady_hand_chance` 0.5→0.08,
  `stun_recovery_interval` 4.8 s→0.6 s, `constitution_chance` 0.35→0.95, …). The scale is **1–9
  indexing this table**; the design's `100 − DareDevil` formula is design-era and is replaced by
  `daredevil_chance` 0.35→0.99. `natural_touch` correctly has no entry — it compares directly
  against maneuver difficulty. **D14's gunnery model and D15's assist strength are now constants to
  load, not values to tune.**
- **D13's maneuver library is shipped data.** `extracted/zrdr/maneuvers.zrd.json` holds **17
  maneuvers**, each a `natural_touch` difficulty plus a `steps` list of
  `[duration, pitch, yaw, roll]` — precisely the "timed control-input program plus difficulty" this
  document proposed to author by hand from design prose. Difficulties match the design's 1–9 column
  exactly on the fourteen they share. Two are shipped-only: `nitro_evade` (difficulty 0) and
  `high_yo_yo` — and **`high_yo_yo` is a stub**: difficulty 99 against a stat capped at 9, with no
  `steps` list. Do not implement it.

**Corrections to §2's field decodes** (all confirmed against the extraction):

| slot | §2 called it | it is |
|---|---|---|
| 6 | formation leader name | **`primary_target`** — an assigned target, not a leader. The engine prints it as "Primary target: %s" |
| 31 | engagement radius, metres | **`pref_engage_alt`** — preferred engagement *altitude*; 350/1100/1500/1600 are altitudes |
| 32 | flag bitmask | **`signature_maneuvers`** — a bitmask over the 17-entry library. `2064` = `dive`+`split_s`, `2048` = `split_s` (the Black Swan's signature). ⚠ bit order is the exe's table order, **not** the JSON file's |
| 33 | target-priority list | **`rating_biases`** — `[nodeNamePattern, bias, ?]` triples with wildcards, feeding target ranking |
| 57–64 | (undecoded) | **`anose hnose atail htail aleft hleft aright hright`** — per-zone armour+health over nose/tail/left/right |
| 65 | voice id | confirmed: **`accentID`**, exactly as inferred |

⚠ **B7 loses its premise.** "Formation flying off the roster's leader field" was built on slot 6;
there is no leader field. `group` (slot 4) is the only candidate left and is unexamined. **Re-scope
or drop B7** — do not implement it as written.

**New fact A4 must absorb:** the roster's own damage model is **four zones (nose/tail/left/right),
each an (armour, health) pair** — the same shape as the player's `destroyable_parts`. That is
independent corroboration of the armour-first two-pool model and tells A4 that the aircraft case
needs no new zone vocabulary; only the zeppelin's N-of-M threshold does.

**Open questions, restated:**

- **#1 (Evade entry, pass or fail)** — **answered by the engine's own strings**: *"Absorbed %f
  damage; steady hand test **failed**. Evading."* / *"…test **passed**. Not evading."* Failure
  triggers the reaction. Same for sixth sense: a *failed* test stuns. Use this vocabulary and
  never restate it.
- **#3 (0–100 or 1–9)** — **closed: 1–9**, per `ai_skill_parameters` above.
- **#4 (skill-slot mapping)** — **closed**, per D10 above.
- **#5 (`num_healthy_required` polarity)**, **#6 (`DA-*`/`DE-*`)**, **#7 (`net.zrd`)** — untouched,
  still open.

**D11's state machine is nine modes, not five.** The engine's debug readout dispatches on one mode
field: `patrol` · `pursue` · **`lay off`** · `evade` · `evasive maneuver` · `stunned` ·
`avoid crash` · `approaching danger zone` · `navigating danger zone`. Note what is *absent*:
there is no `flee` and no `inactive` mode in that dispatch. ⚠ **`lay off` — the "let the player
catch up" behaviour — is a first-class mode**, which makes D15's `--no-assist` switch cheap and the
assist directly observable rather than inferred from steering behaviour.

**D12 gains a shape.** The same readout recomputes target ranking inline:
`rank = weight × 1200 + distance + objectiveBias`, minimised, where the weight starts at **1.0 for
any target except the player, which starts at 0.7** — the design's "rank the player last" rule as a
hard constant — then takes ±0.2 terms for bearing, altitude sign and target facing.

**Also worth knowing:** `player.zrd`'s `min_ai_active_dist` (2000 m) is the activation radius this
document was guessing at, and the roster's own volume slots (8–19) are `0.0` in all 414 blocks, so
every AI falls back to it and to `vehicle.zrd`'s `attack` / `return_range`. `vehicle.zrd`'s AI keys
were already documented in [`formats/vehicle.md`](formats/vehicle.md) — this document's §2 simply
never cross-referenced them.

### Wave F — the zeppelin half, same pass

The zeppelin loader, kill check, broadside fire routine and engine-loss curve are all decoded. The
format consequences are in [`formats/mission-entities.md`](formats/mission-entities.md); the
milestone consequences:

- **F18's threshold question (open question #5) is closed in code.** The engine counts `healthy`
  nodes still active and kills the zeppelin when `survivors < num_healthy_required`. **The data's
  survivor reading is right and the design's destroy-count is the inverse.** The field also defaults
  to **1** and is clamped at load to the length of the `healthy` list. Still assert the direction in
  a test — the failure mode is an immortal zeppelin, which reads as a damage bug.
- **F17's engine-loss model is a square root, not the design's three bands.**
  `f = sqrt(alive/total)`, `max_speed' = f·max_speed`, `max_accel' = (0.8f + 0.2)·max_accel`. The
  design's qualitative claim (each further engine hurts more) survives; its arithmetic does not.
  **Do not implement the 10/40/50 bands.**
- ⚠ **F19's probabilistic hit curve does not exist in the shipped engine.** The design's "20 % at
  maximum range ramping to 100 % at ~200 m" roll is absent: broadside fire runs a lead/intercept
  solve and spawns a real projectile scattered by `cannon_inaccuracy`, skipping targets with no
  solution. This document's §5 restated the design curve as though it were shipped behaviour —
  **it is not**, and F19 should be built as ballistic fire.
- **F19's arc is confirmed exactly as the design states it**: `dot(toTarget, sideNormal) > 0.707`,
  a 90° cone on the firing side's perpendicular. The "randomly chosen section" is confirmed too, but
  only for zeppelin-vs-zeppelin: the engine collects the *target zeppelin's* in-arc gasbags and picks
  one with `rand()`. Broadside ammunition is **hardcoded `wep_28`**, not a data key. Cannons carry
  their own state machine (stowed → deploy → ready → fire) and their own per-cannon re-fire timer.
- **A load-time unit bug worth knowing before matching behaviour**: `min_pitch`/`max_pitch` stay in
  degrees while `pitch` is converted to radians, so the original's initial-pitch clamp never fires.
  Harmless in shipped data; don't reproduce it as a working clamp, and don't read ±30 as radians.
- **E16 gains a correction.** The `ZZ` voice family (`ZZ-GasB-Dest`/`-Lost`, `ZZ-Zep-Dest`/`-Lost`)
  pairs with an engine-side sound table — `snd_Zep_GBdest`/`GBlost`/`Zep_dest`/`Zep_lost` plus
  **team-numbered `GB1`/`GB2` variants**, preloaded alongside the CTF and multiplayer
  mission-won/lost cues. **This family is multiplayer-scoped and team-relative** ("destroyed" =
  theirs, "lost" = yours), not a general zeppelin trigger. §6 treated it as the latter.
- **Mission script can retarget a zeppelin at runtime** — `SET_AI_NET` and `SET_AI_TEAM` both accept
  a zeppelin, which is the design's "retreat is expressed as a net change, not a special mode",
  confirmed. `WAKEUP_ZEP_TURRETS` and `COMPLETED_ZEPCANNONS` are further zeppelin-facing script ops.

- **F20's launch cycle is fully decoded**, and the design's "generation is *held* below the altitude,
  not cancelled" is confirmed in code — while blocked, the wave counter and timer are untouched and
  only the hangar door closes. Two corrections to §4's reading: **`ind_period` and `wave_period`
  compose** (the gap between waves is `ind_period + wave_period`, not `wave_period`), and the
  **door timings are hardcoded, not data** — open 4 s before a due spawn, minimum 4 s open, close
  early only if the next spawn is >8 s away. The host's death permanently disables its generator,
  and a generator whose node or whose entire `nets` list fails to resolve is dropped at load rather
  than loaded inert.
- ⚠ **F20 carries one unresolved discrepancy — `capacity`.** It is `0` on all 23 generators, and the
  blocking rule reads `(wave_size - spawned) > capacityRemaining`, which would hold every generator
  in this install forever — yet zeppelins visibly launch in the original. **Budget an investigation
  item**, do not assume "0 means unlimited"; the `zep_rearm_node_%d` string is the strongest lead.
  Written up in [`formats/mission-entities.md`](formats/mission-entities.md#the-capacity-puzzle).

### The mission-script surface, and what it means for M4's boundary

The binary carries the mission-script vocabulary (`D:\zipper\Crimson\mission.cpp`):
`WAKEUP_ENEMIES` · `WAKEUP_TURRETS` · `WAKEUP_ZEP_TURRETS` · `WAKEUP_GENERATOR` · `WARP_VEHICLE` ·
`SET_AI_TEAM` · `SET_AI_NET` · `SET_AI_ATTACK_RADIUS` · `ADD_/REMOVE_OTHER_TARGET` ·
`ADD_/REMOVE_OBJECTIVE_TARGET` · `START_TAXI` · `SET_HELP_LABEL` · `COMPLETED_ZEPCANNONS` ·
`COMPLETED_STOPPOINT` · `COMPLETED_SOUND_GROUP` · `WAKE_ANIM` / `SLEEP_ANIM` ·
`WAKEUP_SOUND_GROUP` · `STOP_QUEUED_SOUNDS` · `END_TIMER` · `INSTANTWIN` / `INSTANTLOSE` ·
`primary`/`secondary`/`tertiary`.

Objectives scripting is **explicitly out of M4's scope** and this does not change that. It matters
here for two reasons:

- **It is the AI's runtime control surface, and it closes the loop on the roster decode.** Six
  `aiv` slots are script-mutable rather than static: `otherTarget` (36) and `objectiveTarget` (37)
  via the `ADD_`/`REMOVE_` pairs, `helpLabel` (39) via `SET_HELP_LABEL`, `taxiPath` (40) via
  `START_TAXI`, plus the activation volumes via `SET_AI_ATTACK_RADIUS` and the net via `SET_AI_NET`.
  **Anything M4 builds on those fields must expect them to change mid-mission**, so design the AI
  controller's inputs as mutable from the start rather than read-once at spawn.
- **F17's stop nodes are confirmed to exist as a scripted concept** — `COMPLETED_STOPPOINT` is a
  *condition*, so a mission waits on a zeppelin reaching its stop point. That raises confidence
  that `ai-nets`' per-node tags encode stop points but **does not decode them**; nothing yet ties a
  tag value to the condition. Still F17's remaining open item.

### Wave C — the turrets are not self-describing after all

Decoded from `turret.cpp` in the binary; written up in full as a new page,
[`formats/turrets.md`](formats/turrets.md). The study's §3 called `ai.zrd` "fully self-describing,
needs no reverse engineering — the cheapest deliverable in the milestone." **The file is
self-describing; the behaviour it configures is not**, and four of the findings change what C9 has
to build. C9's cost goes up, from "read a table" to "read a table and implement a tracking loop",
but it stays a leaf and its ordering does not move.

- ⚠ **`YAW [0,0]` means *unrestricted*, not *fixed*.** The arc clamp is gated on `min != max`, so
  equal limits — or an absent key — remove the limit rather than lock the axis. One shipped entry
  authors `YAW [0,0]`; four omit `PITCH` and six omit `YAW`. Reading these the natural way points
  those turrets permanently down their rest bearing and they never fire. This is the single most
  likely way to get C9 visibly wrong.
- ⚠ **The yaw arc is a directed interval on the circle, and out-of-arc snaps to the nearer end
  stop** — not the shortest-path one. `YAW [105,255]` and `YAW [-155,-5]` are different arcs.
- ⚠ **Hit resolution is geometric, not probabilistic** — the same refutation already recorded for
  the zeppelin broadside cannons (F19). `INACCURACY` is a scatter cone applied to the *shot*
  direction after the model nodes have been written, and the hit test compares the perturbed
  direction against the target's angular radius. There is no roll anywhere.
- **`ATTACK_INTERVAL`/`BORED_INTERVAL` are a duty cycle, and bored suppresses firing only.** The
  aim solution is computed first and the fire flag cleared afterwards, so a bored turret keeps
  tracking the player while holding fire. That is visible behaviour and cheap to get right.

Two facts change the C9 ↔ mission-script dependency, and one changes the census:

- **22 of the 42 entries ship `ACTIVATED 0`** — over half the turret roster is inert until a script
  fires `WAKEUP_TURRETS`. Since objectives scripting is out of M4's scope, **C9 needs a stand-in
  activation path** or half the emplacements will never engage. This is a new, small dependency
  that the original costing did not carry.
- **The `CREATE_STANDALONE` split is the engine's own, and it is exact.** `CREATE_STANDALONE 0`
  (16) excludes an entry from the world placement pass; those are looked up **by `TITLE`** from a
  host. The other 26 are placed at their own `NODES` patterns. 16 = `HEALTHY_NODE` 16, 26 =
  `NODES` 26, 16 + 26 = 42. §3's two families are right; this is their mechanism.
- **§3's census has `PITCH` at 37; it is 38.** Everything else in that census re-counts clean.

Also worth having in hand, though it does not change scope: `NODES` patterns mean **one entry can
instantiate many turrets**, so emplacement counts are a property of the world model, not of
`ai.zrd`; multiple firepoints fire **round-robin, one per shot**, not together; line-of-sight is
tested **only against the player**, on a cached 1–2 s refresh; and **eight of the 22 keys the
engine accepts are never authored** (`DEACTIVATE`, `EFFECT`, `FIRE_LIMITS`, `STICKINESS`,
`SHOOT_UP_ONLY`, `CATEGORY_LABEL`, `HELP_LABEL`, and the `ON`/`START`/`STOP` sounds) — a reader
should tolerate them, an implementation needs the fourteen that ship.

### Wave E — the trigger taxonomy is 29 ids, and the binary names all of them

Decoded and written up as [`formats/combat-voice.md`](formats/combat-voice.md). E16 was graded
*"direction sound, magnitude a judgement call"* — the direction was right and **the magnitude is no
longer a judgement call**. `crimson.exe` carries the trigger table as a contiguous ordered array of
`TYPE` tokens, and the loop that fills a pilot's voice slots is bounded at `0x1d`: **29 triggers,
ids 0–28**, each naming a family the clip survey already inventoried. § 6's taxonomy and the
engine's table are the same list.

Sixteen of the 29 ids were confirmed independently at their dispatch sites, and two of those
confirmations are exact numbers the study wanted:

- **`DI-LowDmg`/`MedDmg`/`HighDmg` fire at 70 % / 50 % / 30 % of health**, tested most-severe-first.
  § 6's "distress at three zone-damage tiers" now has its thresholds.
- **The 12 `WA-Enemy` bearing call-outs are computed, not enumerated** — `id = 1 + 3*bearing +
  altitudeBand`, ordered low/level/high within each of the 12/3/6/9 o'clock bearings.

Three findings change what E16 has to build:

- ⚠ **The 15-second per-slot cooldown is armed by a *failed* talker roll exactly as by a successful
  one.** A quiet pilot does not retry on the next event — losing the roll silences that trigger for
  15 s. Re-deriving `talker` from the design prose (a chattiness stat) gets this wrong, and it is
  the difference between "sometimes quiet" and "reliably sparse".
- ⚠ **Triggers 1–12 have their talker chance halved, hardcoded** — on top of only 7 of 31 pilot ids
  owning bearing clips at all. Two independent suppressions, not one.
- **Broadcasts elect a speaker.** Several triggers address the flight, not a pilot: the engine
  collects every eligible living teammate that owns that slot, picks one at random, and **on a
  failed roll passes the line to the next candidate**, wrapping. So E16 is not N independent rolls
  — it is a speaker election, and modelling it as per-pilot rolls makes the flight either silent or
  a chorus.

Two open questions close, one narrows:

- **Open question 6 closes.** `DA` *is* the ally counterpart of `DE`, and the split is by team
  rather than by outcome: both are the dying pilot's own death cry, id 20 if the aircraft is on the
  player's team and id 21 if not. Both are dispatched with the force flag, because the speaker has
  just been marked dead.
- **The `aiv` → voice chain is no longer inference.** § 6 called it "strongly-supported inference,
  not proven fact". It is traced: `accentID` (roster slot 65) → a `voice.zrd` row → a pool of pilot
  VO ids → that pilot's clips → the 29 slots. ⚠ **`voice.zrd` is the accent table, not the trigger
  table** — its 35 rows sit suspiciously close to 29 and are a different thing entirely.
- **`TA-FailTail` is confirmed as a real engine trigger with a real dispatch site** (id 25), not an
  orphan clip family. § 6 flagged it as audio documenting a behaviour the design prose omits; that
  now has engine backing.

Left open and recorded on the page: the exact attacker/victim polarity of the three gloat triggers
(22–24), no located dispatch site for id 16 (`PR-EnemyDwn`), and how the `-A`/`-B`/`-C` and
`Bail`/`NoBail` variants are chosen below the family root.

E16 still needs B8's voice runtime and does not move in the ordering. Its **cost drops**: the
taxonomy no longer needs deriving from clip names, and the dispatch rules are constants rather than
TUNEs.

### Decisions and closures — 2026-08-10

**C9's activation question is answered by the data, and the answer is that it barely exists.** The
`CREATE_STANDALONE` split is *also* the awake/dormant split: **all 16 carried turrets ship
`ACTIVATED 1`; all 22 dormant entries are standalone world emplacements.** Carried turrets come up
with their host and need no `WAKEUP_TURRETS`, no stand-in and no decision. So C9 splits cleanly, and
the half that matters for the next playable mission is unblocked:

- **C9a — carried turrets.** The aircraft/zeppelin gunners, including the player's own turret slots
  that M3 left parsed-but-inert ([`formats/loadouts.md`](formats/loadouts.md)). Zero activation
  dependency. This is the half the zeppelin hunt exercises, and it is where the turret UI lands.
- **C9b — world emplacements.** Still wants an activation path for the 22 dormant entries. Now
  *deferrable* rather than blocking, because nothing on the playable path depends on it.

Two supporting facts, both measured:

- **The 16 carried entries are 8 AI + 8 player.** Titles run `MSG_TUR_{AC,BRIGAND,FRONT,REAR}_{G1,G3}`
  with a `P`-prefixed mirror; the `P` set is the player airframes. The player's turret and an
  enemy's are the same system with different rows — which is why C9a and the turret UI are one
  piece of work, not two.
- ⚠ **`_G1`/`_G3` are gun-group slots, not difficulty grades.** Across all eight pairs the two rows
  differ in *exactly* `HEALTHY_NODE` and `PARTS` and nothing else — same accuracy, same rate, same
  arcs, same weapon. Reading `G3` as "grade 3" invents a difficulty system the data does not have.

**F20's `capacity` — chased through the binary, and every in-engine explanation is eliminated.** The
code route was taken in preference to a capture. All three candidates fail: the `zep_rearm_node_%d`
lead is **dead** (it enumerates *player rearm-pad* scene nodes, the counterpart of `rearm_rad`, and
never touches a generator); a **runtime top-up does not exist** (three writes to the counter in the
whole module — load-time assign, decrement, and a reset that restores it *to* `capacity`; and the
loader has a single caller, so there is no second construction path); the **global cannot rescue
it** (both branches yield `0` for an authored `0`, and it must be set in retail or no turret would
load); and the **operands are not misread** (a single unsigned compare and a jump-if-below in the
disassembly). Re-measured: `capacity` present on 23/23 and `0` on 23/23.

That converts a three-way puzzle into one located suspicion — **what the engine reads for `capacity`
may not be the `0` the extraction reports** — and the next discriminating step is to read its raw
bytes out of the un-extracted `egen.zbd` rather than the JSON. Still a code route; no capture owed.
The standing instruction is unchanged: **do not implement "0 means unlimited".**

**The three unnamed roster slots are closed.** Most likely inherited padding from the engine's
earlier `mech3` lineage — recorded as a hypothesis, not asserted. What is established suffices: the
exe's own editor comment names nine where the format has twelve, all twelve are `0.0` install-wide,
and the fallback they defer to is documented. ⚠ A reader must still **parse** twelve to keep later
field indices aligned; beyond that, ignore them. Not to be reopened without a different build or an
authored non-zero value.

**F17's per-node tags — narrowed sharply, still not decoded.** Attacked directly rather than
deferred. The binary route does not reach the parser: the only code naming `ne%06d.zrd` is the
editor's text I/O and a debug dump, so the shipped loader is not reachable by string search. The
*data* route paid off instead. Cross-referencing the 40 tagged nets against `neindex` names, the
`zeppelins.json` `net` field and each node's edge-list degree:

- ⚠ **The two tag widths are two different systems on disjoint net populations** — not one
  optional-length field, which is how the format page previously read.
- **The 2-extra shape is zeppelin-exclusive.** All 36 are zeppelin routes by name, 31 directly
  referenced by a `zeppelins.json` `net`, the rest unreferenced alternates. **No fighter net carries
  one.** That is the strongest evidence yet for the stop-point reading — it is what the hypothesis
  predicted, and it could easily have come out the other way.
- **The 4-extra shape is `[0,0,1,N]` on exactly four nets** — `M3StuntCourse`, `M1FilmShot`,
  `M1Cabbie`, `M4MilesRun`. Stunt/cinematic/escort, not zeppelin: this belongs with the Danger Zone
  gate system, not with `COMPLETED_STOPPOINT`.
- Within the 2-extra shape, `b` is a flag that is **not** graph topology (it occurs on degree-1 and
  degree-2 nodes alike) and concentrates on first/last nodes; `a` is a small id **allocated
  sequentially per chapter across files** (C5's three cargo routes use 1–2, 3–4, 5–6).

**Two readings survive and the data cannot choose:** `a` = stop-point id with `b` = halt, or
`a` = segment id with `b` = boundary. So F17 keeps one open item, but it is now bounded, has a
worked example, and has a single named discriminating instrument — **locate the runtime net loader**
(not via the filename string; try the `SET_AI_NET` handler or the net-follower's node access). Until
then: parse and preserve the tags, act on neither reading. F17's cost and ordering are unchanged.

**Still not examined:** nothing in the wave list.

## Delta — 2026-08-13: scheduled, with a full premise and citation re-check

Scheduled as the active plan. Every claim and citation was re-verified against `main` at
`0b2385e`; the body text now carries current paths and line numbers. What changed since
2026-08-10, and what it does to the waves:

**A1's landing is verified in place.** `Flight/CollisionLayers.cs:11-22` (`World` layer 1,
`Aircraft` layer 2, `WorldAndAircraft`); `Flight/AircraftBody.cs:16` (an `AnimatableBody3D` per
plane, built from the same `PlaneCollider.Parts` boxes, `CollisionMask = 0`); registered at
`FlightController.cs:582-585`; hits routed at `Projectile.cs:1534`; kills attributed via
`FlightController.Downed` (`:446`, raised `:1829`); all pinned by the `air-to-air` suite
(`Testing/Suites.cs:148`, body `:2538`). Wrong-claim #12 is fully discharged: `Spawn` now carries
`int shooterId = NoShooter` (plus `muzzleAnchor` and `aimDir`, `Projectile.cs:581`), and per-shot
`Exclude` is set and reset around every ray (`:776`/`:778`).

**Surface classes are gone; the id registry is in** (PLAN-surface-id-weapons, completed
2026-08-13). The six-member `SurfaceClass` enum is deleted; the live scheme is the 14-id
`SurfaceRegistry` (`Mech3/SurfaceRegistry.cs:61-65`), and weapon IMPACT tables are keyed by
surface id (`WeaponDefs.cs:55`). `ProjectilePool.SurfaceIdOf` already answers `player` (id 6) for
a struck `AircraftBody` (`Projectile.cs:465-471`), so the `player` IMPACT row — authored on 44 of
48 weapons — fires the moment this plan fields a shooter; that is `BL-222`, which stays in backlog
and closes as a side effect of A2 + D14. The `enemy` row (id 7) is non-null on only 3 of 48.

**New item G21 — the `ai_crash_<name>` family (absorbs `BL-347`, minted 2026-08-12).** A third
registry-indexed choreography vector found at `FUN_00478a00`: surface-registry names concatenated
onto a prefix, indexed by the struck material's surface id — the AI-aircraft counterpart of the
`player_crash_*` and `touchdown_*` families that PLAN-surface-id-weapons modelled.
`Session/SurfaceDefTable.cs` is the cascade already written for those two (its `DefForSurfaceId`
reproduces `FUN_0048b920` exactly) and is probably reusable verbatim. Needs AI aircraft to be
reachable at all, so it runs after A2. ⚠ Carried with it from `BL-347`, adjacent but **not** M4
work: `FUN_004c56c0` resolves a `soil_<name>` substring (`0x0062bf98`) through `FUN_00559670` and
writes the id onto the material with `FUN_0055b0a0` — how surface ids are authored in the original,
by registry name. We read `GameZMaterial.SoilId` instead, so nothing depends on it today; it
matters only if a material's id ever looks wrong at runtime (also recorded in
`analysis/surface-classification/FINDINGS.md`).

**D14's gunnery gains landed scaffolding** (PLAN-sticky-bullets / `BL-342`). The player aim assist
landed a constant-velocity intercept solver (`Flight/AimAssist.cs`) directly reusable for the AI's
lead computation, and `FlightController.IsHumanPiloted` (default true) already carries the
engine's human-vs-AI split — the original ticks the assist only for humans, and an AI plane's
dead-eye path has no assist slots at all. A2's controller must set it false, which also makes the
AI-exclusion half of that gate testable for the first time. Two data requalifications for D14:
`CANNON_SPREAD` is an assist cone, not a dispersion term (`formats/turrets.md`, 2026-08-12), and
`gun_pitch`/`gun_yaw` (±11°) sit on AI defs only — the AI's forward-gun aiming cone, not turret
arcs (absorbed from `BL-069`).

**C9 gains a ready integration seam and a live adjacent bug.** The aim-assist candidate scan
already iterates an empty `AimCandidateSet.Turrets` (`AimAssist.cs:387`; filled via `AddTurret`,
`:485`; census logged as `turrets=… (M4)` at `FlightController.cs:1611`) — C9a registers turrets
there and the players' lock-on sees them for free. ⚠ Adjacent, and NOT C9's to fix silently:
`BL-348` (filed 2026-08-13) is a live bug in the `CallAnimation`/`NameResolver` wiring under C3's
slung balloon turrets (`b_turretN`) — verify C9 against a chapter without it, and keep its fix a
separate change.

**Wave F's destruction choreography got cheaper** (PLAN-object-motion-decode, completed
2026-08-13). `BL-245` closed: the 379 apex-less `OBJECT_MOTION` falls — zeppelin gasbags,
lifeboats, turret parts, chuteman descents — now simply land under the decoded contact model, so
F18/F19 debris needs no divergence decision. Left in backlog, adjacent but not this plan's:
`BL-343` (`IMPACT_FORCE` velocity inheritance, unmodelled; its 182 events are exclusively aircraft
wreckage, so it becomes judgeable once AI planes crash — do not fold it silently into G21).

**A2's "single biggest unknown" is now a documented contract, not a hazard.** The index comment no
longer claims the node index is never added to: a post-bootstrap subtree may `Add` rows provided
the caller also calls `ClearFindCache` (`Mech3/Anim/NameResolver.cs:25-34`, `:170-183`), with two
working precedents (`AnimRuntime.IndexStage`, `:999-1004`; `IndexPooledCopy`, `:1019`). A2 still
owns doing this correctly for a spawned aircraft; the risk grade drops.

**Smaller re-check results:**

- Effect templates: `PlaceTemplateAt` is now `TemplateStage.PlaceAt`
  (`Mech3/Anim/TemplateStage.cs:374`, absolute write `:391`). Pooled per-call copies exist
  (`BL-225`), but a placed effect still snaps to an absolute world point and does not track a
  moving host — the zeppelin constraint stands.
- `AnimRuntime.PlayerPositions` (`:117`) still has exactly one consumer (`EXECUTION_BY_RANGE`,
  `:1909`); `PLAYER_RANGE` conditions (`:3268`) and the `WorldSounds` listener
  (`WorldSession.cs:275`) are still player-one. `ProjectilePool.Listener` no longer exists — that
  singleton is gone; near-miss self-exclusion rides the shooter id instead.
- The voice-runtime blockers all still hold: `PlayOneShot` is positional-only
  (`WorldSounds.cs:188`, position written once at `:220`), `Prewarm` (`:115`) / `Loader` (`:51`,
  nulled at `WorldSession.cs:384` outside the Lab intent) unchanged, dialogue chains still
  parsed-then-discarded (`SoundDefs.cs:106-122`). B8 is unchanged.
- One correction: the session opens `extracted/soundsh.zip` (`Session/Launcher.cs:219`; `--sounds`
  overrides). `SoundArchive` uses a directory only when the configured path *is* one — there is no
  unzipped-preferred fallback for sounds, so the one-file case-collision caveat in §6 applies only
  when `--sounds` points at the unpacked folder.
- `DestructibleRegistry` is unchanged — single scalar `Health`, no zone concept
  (`Mech3/DestructibleRegistry.cs:29`, `:161`) — A4 stands. `WeaponDef.DamagesZeppelin` is still
  parsed-never-consumed (`WeaponDefs.cs:102`, `:255`).
- Still nothing reads `aiv`, `maneuvers.zrd`, `ai_skill_parameters`, `ai.zrd`, `zeppelins` or
  `egen`; `Mech3/AiNets.cs` (nets) remains the only AI-data reader, consumed only by the debug
  overlay. `Mech3/MissionSetup.cs:26-30` documents `aiv.zrd.json` without reading it.
- Stale in-code comments corrected in this change: `IncomingFire.cs`'s "no round can strike an
  aircraft" remark and `PlaneCollider.cs`'s sweep-only header, both written before `AircraftBody`.

**Absorbed from backlog on scheduling** (entries deleted there, facts preserved): `BL-068` (the
schedule-M4 item itself) and `BL-069`, whose still-valid leads are: turrets are AI gunners, and
the stock-loadout `W4` slot is filled on exactly the five turret airframes (`pavenger`,
`pbalmoral` ×2, `pbrigand`, `pfirebrand`, `pkestrel`); the mesh-less `target` marker (one per
plane root, 11 player + 11 AI) is the aim point for AI gunnery and air-to-air lock-on; air-to-air
lock-on is a targeting change on the landed guided-missile flight model, not new flight code;
`wep_14` (TORPDO) ships `FLYOUT_HEALTH 10` + `TARGETABLE`, shootable once something shoots; AI
vehicle defs carry the `armor` + `health` pair `PlaneStats` ignores (armour-first — the same model
the roster's four zones corroborate). `BL-069`'s "turret rotation limits stay undecoded" line is
superseded by [`formats/turrets.md`](formats/turrets.md). `BL-347` became G21.

---

## Milestone goal

- Enemy and allied aircraft fly, patrol, engage, evade, flee and die, driven by the original's own
  per-mission `aiv` rosters and chapter patrol graphs.
- Turrets and AA emplacements acquire and engage from the shipped `ai.zrd.json` specification.
- Zeppelins follow their nets, fire broadsides, launch fighters, and die when enough gasbags do.
- Pilots talk, from the shipped per-pilot voice bank, on the shipped trigger taxonomy.
- Everything above is driven by extracted data; nothing is invented where the data has an answer.

**Boundary: M4 makes things fight. It does not make missions.** Objectives scripting, mission
success/failure, the campaign shell, briefings, landing/rearming and the economy are out of scope and
belong to a later milestone. M4's deliverable is a *believable combat world*, exercised through the
existing free-flight / stunt / splitscreen entry points, not a playable campaign.

---

## ⚠ Read this before implementing anything

This document's first draft was assembled from a chat-era survey. **Twelve of its factual claims were
wrong or overstated and were corrected by measurement before this file was written.** They are
tabulated because each is a plausible-sounding reading that will be re-derived by the next session
otherwise.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | Patrol nets live in `<Cx>/<mission>/zrdr/net.zrd.json` as `[x,y,z,heading]` arrays, and that is "the entire data requirement for a patrol behaviour" | The real nets are **222 chapter-scoped `<Cx>/zrdr/ne0NNNNN.zrd.json` files**, named by `<Cx>/zrdr/neindex.zrd.json`. Their nodes are `[x,y,z]` and there is a **separate explicit edge list** — a graph with branching, not a point list. `net.zrd.json` is a *different*, unnamed, edgeless file (below). |
| 2 | `net.zrd.json` holds the patrol routes | 45 files, exactly one flat group each, 9 of them `null`; node counts are only ever **8, 48 or 80**, quantised by mission type (MP1→80, MP2/MP3→48, campaign→8), 23 distinct payloads shared across 45 files, one 8-node payload reused by 14 missions with coordinates outside the mission world range. Shape and distribution say *spawn table*, not route. **Undecoded — do not build patrol on it.** |
| 3 | `aiv` blocks are "~81 flat fields each" | 81 is the modal length (**307 of 414 blocks, 74 %**). The rest are **42 / 65 / 66 / 67 / 68** — trailing fields are simply omitted. A fixed-width reader will throw on a quarter of the data. |
| 4 | The `aiv` header is a pilot-id → name map | It is a positional `(slotId, designerLabel)` list with **one pair per vehicle block, in block order**; labels mix character names with param-set names (`Eairg31_params`) that `egen`'s `vehicle.params` references. The pilot/voice id lives at field **65**. |
| 5 | Most `aiv` numeric fields are undecoded and are "the prime candidate" for the 12 skill modifiers | Field **0 is the patrol-net id** (305/305 non-`-1` values resolve against `neindex`, 100 %). Of the 73 bare-number slots, **31 are constant across all 414 blocks** (dead padding). The skill candidate is a specific, narrow, 9-slot window — see "The skill vector" below. |
| 6 | `ai.zrd.json` entries all carry `TEAM`, `HEALTHY_NODE`, `PITCH`, `YAW`, `SOUNDS` | Coverage of 42 entries: `TEAM` 20, `HEALTHY_NODE` 16, `PITCH` 37, `YAW` 36, `SOUNDS` 37. Three keys were missed entirely: **`NODES` (26), `HEALTH` (17), `CREATE_STANDALONE` (16)**. There are two structural families, not one. |
| 7 | Turret `WEAPON.NAME` values are names like `30slug` | They are **`BALLISTICS` ids** (`wep_140`, `wep_29`, …), all 8 resolvable in `weapons.zrd.json`. `30slug` is the *inner* `NAME` of a ballistics record — a different field. |
| 8 | `egen.zrd.json` is the zeppelin fighter-launch generator | **33 of 53 files are `[null]`**; only **23 generators** exist, in **three** shapes — zeppelin launch (17), plain spawner (5), moving spawner (1). |
| 9 | `zeppelins.zrd.json` records carry per-cannon `cannon_health` | Present on **24 of 58 records (41 %)**. `gasbags` is missing from one. `team`, `deactivated`, `cannon_inaccuracy` are additional optional keys the survey missed. |
| 10 | Voice bearing clips are `WA-Enemy-{12,3,6,9}` × `{plain, High, Low}` | Suffixes are **`H`/`L`**, not `High`/`Low`, and **only 7 of 31 pilot ids carry the 12-clip set**. |
| 11 | `PlaneCollider` boxes are "plain math AABB/OBB structs" | They are real `BoxShape3D` **physics resources** (`Flight/PlaneCollider.cs:44`) used query-only — the plane owns physics *shapes* but no physics *body*. The distinction changes the fix (attach, don't author). |
| 12 | `ProjectilePool` needs owner/exclusion info | It has none, and neither does its raycast: `_ray` (`Flight/Projectile.cs:119`) is a shared `PhysicsRayQueryParameters3D` whose **only** assigned members are `From`/`To` (`:424-431`). Self-hit is avoided purely because the shooter has no body. Exclusion must be *added*, and the shared mutable query object must be reset per round. |

### Confidence grading

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code or data, reproducible by a committed script** | A1–A4, B5, B6, B8, C9, F17, F19, F20 | Confirm the trace, then implement. *(C9 stays in this row and its trace is now the binary's, not the data's — but the trace turned out to be a tracking loop rather than a table read, so its **cost** rose even though its confidence did not. Confidence is not effort.)* |
| **Direction sound, magnitude a judgement call (TUNE, not fact)** | D14, D15, ~~E16~~, F18 | The *what* is settled; the *how much* goes on `backlog.md`'s TUNE list, never invented as fact. *(E16 graduated out on 2026-08-10 — the 29-id trigger table, the damage thresholds and the cooldown are read from the binary, so its magnitudes are constants, not TUNEs.)* |
| **Leads only — a hypothesis with a named discriminating instrument** | D11, D12, D13 | Budget for investigation; **a correct disproof that lands no code is a success here.** *(D10 graduated out of this row on 2026-08-10 — it is now traced to shipped data.)* |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy (`docs/verification.md` METHOD-5).

---

## What the data actually ships

Every number below was measured on 2026-07-25 against the retail extraction at `extracted/`.
`analysis/m4-ai-data/aiv_skill_slots.py` is the committed instrument for the `aiv` half.

**None of these five reader families appears in `docs/formats/zrdr.md`'s family index.** They are a
complete documentation gap, which is item A3.

### 1. Patrol graphs — `<Cx>/zrdr/ne0NNNNN.zrd.json` + `neindex.zrd.json`

- **222 net files across the 8 chapters** (C1 29, C1B 22, C1C 20, C2 34, C2B 18, C3 30, C4 29, C5 40),
  and **222 names** across the 8 `neindex.zrd.json` files — a perfect 1:1. The filename encodes the
  index id: `ne000008.zrd.json` is id 8.
- `neindex.zrd.json` = `[[ slotCount, id0, "Name0", id1, "Name1", … ]]`.
- A net record is `[ null, 10.0, ×9 floats (near-always 0.0), NODES, EDGES, TRAILER ]` (14 elements;
  8 files have 13 and omit the trailer).
  - **NODES** — `[x, y, z]`, 2,268 total. 81 nodes carry two extra numbers and 12 carry four: per-node
    tags, undecoded, and the obvious candidate for the design's stop/valve nodes.
  - **EDGES** — `[i, j]` index pairs, 2,149 total. **This is the connectivity the design calls for**,
    and it branches (a node with two successors is normal).
  - **TRAILER** — `[-1]` on 133 files, otherwise `[nodeIndex, "nodeName"]` such as `[7, "piratezep"]`
    or `[10, "player"]`: the net's attach/follow target.
- Nodes per net: min 2, median 8, max 44. Edges per net: min 1, max 43.
- Worked example: `extracted/C1/zrdr/ne000010.zrd.json` = net id 10 = `M4ReinfAce` — 11 nodes at
  y = 400, 10 edges forming a closed loop (`[0,1] … [8,9], [0,9]`), trailer `[10, "player"]`.
- Nets are referenced **by name** from `egen` (`vehicle.nets`), `zeppelins` (`net`) and
  `objectives`, and **by id** from `aiv` field 0.

### 2. AI vehicle rosters — `<Cx>/<mission>/zrdr/aiv.zrd.json`

**53 files (one per mission dir), 414 vehicle blocks.** Element 0 is the header; elements 1…N are
`["name", [ …flat fields… ]]`. Field-count histogram: `{42: 20, 65: 1, 66: 33, 67: 14, 68: 39,
81: 307}`.

Decoded slots:

| idx | meaning | evidence |
|---|---|---|
| 0 | **patrol-net id** (`-1` = none; `null` on 3) | 305/305 non-`-1` values resolve against the chapter `neindex`; C1/M04's blocks resolve to `M4Charlie`, `M4Bravo`, `M4Reinf4`, `M4ZepAttack` — all M4 nets, in mission M04 |
| 1 | **spawn position** `[x,y,z]` | the only list-typed slot; present on all 414 |
| 2 | **heading**, degrees | 0 / 180 / 90 / −145 / −90 dominate |
| 6 | **formation leader name** | 6 distinct: `""` (346), `player` (27), `devastator_1/2/3`, `piratezep` |
| 20 | **`MSG_*_NAME` display key** | 29 distinct; resolves in `messages.json` (37 `*_NAME` entries, e.g. `MSG_BSWAN_NAME` = "The Black Swan") |
| 22–30 | **candidate 9-slot skill vector** — see below | |
| 31 | **engagement radius**, metres | `-1.0` on 384; otherwise 350 / 1100 / 1500 / 1550 / 1600 |
| 32 | **flag bitmask** | powers of two 1…65536 plus composites (2064 = 2048+16, 32896 = 32768+128); `0` on 174 |
| 33 | **target-priority list** or `null` | `[["fuel_truck*", -1.0], ["bloodhawk_2", -1.0], ["hk_zep", -1.0]]`; list on 321, null on 93; length 1–9 |
| 39 | objective-kind key | `MSG_OBJ_FOLLOW` / `MSG_OBJ_DESTROY` / `MSG_OBJ_DEFEND` |
| 65 | **voice id** → `extracted/zrdr/voice.zrd.json` row → `soundsh/VO_id<N>_*` | all 21 distinct values (11–33) are valid rows; consistent per character (player 11, Jack/Ilsa 12, Big John 13, Tex 14, Buck 15, Betty 16) |

**31 of the bare-number slots are constant across all 414 blocks** (8–19, 36, 43–56, 58, 60, 62, 64) —
dead padding, not signal. After the decodes above, roughly **26 slots remain genuinely undecoded**,
not ~73.

#### The skill vector (the highest-value lead in this document)

Slots **22–30** are nine consecutive integers whose only values are `-1` (unset) and **1–9**. They are
populated as a complete nine-value vector on exactly **29 blocks**, and those 29 are precisely the
**named aces**. Reproduce with `analysis/m4-ai-data/aiv_skill_slots.py`. Selected rows:

| Mission | Block | Name key | Vector (22→30) | radius | flags |
|---|---|---|---|---|---|
| C1C/M01 | `bsfury_1` | `MSG_BSWAN_NAME` | 6 6 5 6 6 6 6 5 5 | 1600 | 128 |
| C1/M05 | `bhatbrigand_2_6` | `MSG_ADIXON_NAME` | 5 5 5 6 4 5 5 5 5 | — | 256 |
| C2/M03 | `hafury_2` | `MSG_BSWAN_NAME` | 7 7 7 7 7 7 6 5 5 | — | 1024 |
| C2/M02 | `secfury_5` | `MSG_STUNT_PLANE_NAME` | 1 1 1 1 1 **9** 1 1 1 | — | 0 |
| C3/M04 | `medkestrel_5` | `MSG_MEDACE_NAME` | 5 4 5 5 4 5 5 5 5 | — | 64 |
| C4/M01 | `bswingman_1` | `MSG_BSWAN_NAME` | 9 9 8 8 8 9 9 5 5 | — | 2048 |
| C5/M01 | `autogyro_1` | `MSG_CABBIE_NAME` | 9 6 6 **3 3** 6 9 7 8 | — | 2 |
| C5/M03 | `stihellhound_5_8` | `MSG_WITCH_NAME` | 9 9 9 9 9 9 9 5 5 | — | 1024 |
| C5/M04 | `bsfury_5_1` | `MSG_BSWAN_NAME` | 9 9 9 9 9 9 9 9 9 | — | 65536 |

Five independent, hard-to-fake corroborations that this is the skill vector:

- **A second file names the same shape.** Every chapter's `<Cx>/IA1/zrdr/ia.zrd.json` carries a key
  literally called **`ace_stats`, and it holds exactly nine values** — `[9,9,9,9,9,9,9,9,9]`, in all 8
  chapters. That is a *named* key confirming both the count and that 9 is the ceiling, which is far
  stronger than any positional inference. It says nothing about the ordering.
- **It scales with pilot fame.** The Black Swan reads 6s at his first appearance (C1C/M01), 7s in
  C2/M03, 9/9/8/8/8/9/9 through C4, and **all nines** in the final C5/M04 encounter. The design states
  that skill ratings scale with fame; the recurring antagonist's vector does exactly that.
  **⚠ It is not monotonic in chapter-directory order** — C3's three aces sit at **4–5**, *below* C2's
  6–7. The chapter directories are not story order (the voice-clip naming maps directory `C2` onto the
  Hollywood chapter), so do not read the directory sequence as a difficulty curve.
- **The taxi is bad at exactly the right things.** `MSG_CABBIE_NAME` (a Manhattan cab autogyro) reads
  9 6 6 **3 3** 6 9 7 8 — the two lowest values sit in slots 25 and 26, which are positions 4 and 5 if
  the slot order matches the design's own skill order (4 = gunnery, 5 = shot-angle ferocity). A cabbie
  who cannot shoot.
- **The stunt plane is good at exactly one thing.** Both `MSG_STUNT_PLANE_NAME` blocks read all 1s
  except **9 at slot 27**. The two candidate readings sit at *different* positions, which is what makes
  this case discriminating rather than confirmatory: under document order slot 27 is position 6, the
  **composure** stat (a show plane that flies its routine unbothered), while **daredevilry is position
  1 = slot 22**, which the stunt planes leave at minimum. So a "stunt plane ⇒ daredevil" reading
  requires the slot order *not* to be document order — and the cabbie above is independent evidence
  that it is. **Unresolved; do not pick one here.** See D10.
- **Generic mooks carry one authored skill and it is the gunnery slot.** 89 blocks have `-1` in eight
  slots and **`1` in slot 25** — position 4. Generic AI can barely shoot, which is the observed
  gameplay.

**Slots 29 and 30 behave unlike the rest.** Both read `5` on 25 of the 29 fully-populated blocks, where
22–28 vary per pilot. The exceptions are not noise, though: they are the cabbie (7, 8) and the final
Black Swan (9, 9) — the two most deliberately-characterised pilots in the game. So "never tuned" is too
strong; "tuned only where a designer cared" fits better. Under document order these are positions 8 and
9, the two *signature* stats (favourite maneuvers, favourite approach) — which would explain a default
of "no strong preference" for everyone except a set-piece pilot.

**⚠ This is a lead, not a finding.** Two things must be settled before any of it is implemented (D10):
the **slot → skill mapping** (the document-order hypothesis above is unproven, and the stunt-plane case
argues against it), and the **scale conflict** below.

#### The scale conflict — flag it, do not paper over it

The design pins one stat to a 0–100 scale by giving a formula in seconds (see "Danger Zone check").
The shipped data uses **1–9**. Both readings have independent support: the maneuver table's own
difficulty column also runs **1–9**, and maneuver selection culls by comparing a maneuver's difficulty
to the pilot's handling stat — which only type-checks if both are on the same 1–9 scale. So the
shipped scale is almost certainly 1–9 and the document's seconds formula is design-era. **Implementing
the formula literally against shipped data yields a 91–99 s check interval regardless of the pilot,
i.e. a stat with no effect.** Decide the rescaling explicitly (D10), record it as a TUNE.

### 3. Turrets and emplacements — `extracted/zrdr/ai.zrd.json`

**One shared file, one `TURRET` section, 42 entries, fully self-describing, needs no reverse
engineering.** This is the cheapest deliverable in the milestone.

⚠ **The second sentence is wrong and the third overstates it.** The *file* is self-describing; the
*behaviour* is not, and four of the decoded semantics are counter-intuitive enough to get C9
visibly wrong — `YAW [0,0]` meaning unrestricted chief among them. Superseded by
[Delta § Wave C](#wave-c--the-turrets-are-not-self-describing-after-all) and
[`formats/turrets.md`](formats/turrets.md); the family split and the value ranges below survive
intact.

Key coverage across the 42: `TITLE` 42, `ACTIVATED` 42, `PARTS` 42, `WEAPON` 42, `INACCURACY` 42,
`ATTACK_INTERVAL` 42, `BORED_INTERVAL` 42, `PITCH` 38 (⚠ 37 above was a miscount), `SOUNDS` 37,
`YAW` 36, `NODES` 26, `TEAM` 20,
`HEALTH` 17, `CREATE_STANDALONE` 16, `HEALTHY_NODE` 16.

Two structural families (9 distinct key orderings in total):

- **`CREATE_STANDALONE` family (16)** — bound by `HEALTHY_NODE` to a single named node; carries
  `TEAM`, `PITCH`, `YAW`, `SOUNDS`. These are the aircraft-mounted turrets (`hturret`, `brigturret`)
  matching the five turret airframes M3 left inert.
- **`NODES` family (26)** — bound by wildcard node patterns (`["aagun**"]`, `["thug*"]`); carries
  `HEALTH`, and ranged `[min,max]` intervals instead of scalars. These are the world emplacements.

Values: `INACCURACY` 2.5–15.0 (8 distinct); `PITCH` always a 2-tuple, −60…85; `YAW` always a 2-tuple,
−180…269; `ATTACK_INTERVAL` 2.0–30.0 (scalar or `[min,max]`); `BORED_INTERVAL` 2.0–10.0 (likewise);
`DETECTION_RANGE` 350–1000; `WEAPON.FIRE_RATE` 0.15–12.0; `WEAPON.AMMO` only 9999 or 12000; `TEAM`
always 1; `CREATE_STANDALONE` always 0; `HEALTH` 2/8/10/30; `SOUNDS.CANNON` always `snd_chaingun`;
`TITLE` 32 distinct `MSG_TUR_*` keys, all resolving in `messages.json`.

`PARTS` is always `[turretNode, gunNode, firepointNode]`, though 4 of 126 elements are themselves
lists (`["brigturret", "hgun", ["hfirepoint", "hfirepoint1"]]`). ⚠ The engine reads this as a
kinematic chain — `[yawNode, pitchNode, firepoint(s)]`, and a 2-element form drops the traverse
ring — and cycles multiple firepoints round-robin, one per shot.

**All 8 distinct `WEAPON.NAME` ids resolve in `weapons.zrd.json`**, and six of the eight sit in the
AI-detuned `wep_1xx` tier that `docs/formats/weapons.md` already documents:

| id | uses | display | armour/health dmg | range | velocity |
|---|---|---|---|---|---|
| `wep_140` | 16 | 40-cal slug (AI tier) | 2.0 / 2.0 | 1000 | 600 |
| `wep_29` | 13 | turret gun | 0.25 / 0.25 | 1000 | 450 |
| `wep_27` | 6 | AA flak rocket | 10 / 10 | 900 | 850 |
| `wep_23` | 3 | turret gun (MP) | 1.0 / 1.5 | 1000 | 400 |
| `wep_30` / `wep_60` / `wep_28` / `wep_06` | 1 each | 30-cal, 60-cal, cannonball, HE rocket | — | — | — |

### 4. Generators — `<Cx>/<mission>/zrdr/egen.zrd.json`

53 files, **33 of them `[null]`**; **23 generators** in 20 files, in three shapes:

1. **Zeppelin fighter launch (17)** — `zeppelin` (always `[1]`, the discriminator), `open_anim`,
   `close_anim`, `origin` (`cargobay` / `workersvoyage_cargobay`), `rotation` (−90 or 0),
   `min_altitude` (100/150/200/250/300), plus the common block.
2. **Plain spawner (5)** — the common block only.
3. **Moving spawner (1, `barracuda`)** — adds `healthy` (`subhealthy`) and `moving_path` (`null`).

Common block: `node`, `vehicle{ params?, nets, choose_nets }`, `capacity` (always 0), `max_active`
(1/4/5/6/10), `wave_size` (1/3), `wave_period` (1/2/5/10/20 s), `ind_period` (0.5/2/3/5/10 s).
`choose_nets` is always `cyclic`; `vehicle.nets` names 14 nets from `neindex`; `vehicle.params` names
an `aiv` header label.

Worked example — `extracted/C1/M04/zrdr/egen.zrd.json`, generator 1 of 2:
`node ["eairg31"]`, `vehicle [params ["Eairg31_params"], nets ["M4Reinf4","M4Reinf3"], choose_nets
["cyclic"]]`, `capacity 0`, `max_active 4`, `wave_size 1`, `wave_period 10.0`, `ind_period 10.0`.

### 5. Zeppelins — `<Cx>/<mission>/zrdr/zeppelins.zrd.json`

50 files (**12 `[null]`**, all MP1/MP2), **58 zeppelin records**, 1–4 per file. Fully named keys.

Universal (58/58): `node`, `position`, `yaw`, `pitch`, `max_speed` (5–30), `max_accel` (always 4.47),
`accel_pitch` (0.5), `accel_yaw` (0.5/1.5/2.0), `max_rate_yaw` (5/14/15), `max_rate_pitch` (5.0),
`min_pitch` (−30), `max_pitch` (30), `net` (a name, 33 distinct, resolving against `neindex`),
`healthy`, `num_healthy_required`, `engines`.
Optional: `gasbags` 57, `cannon_fire_delay` / `cannon_fire_range` / `left_cannons` / `right_cannons`
48, `targets` 47, `cannon_health` **24**, `team` 16, `deactivated` 9, `cannon_inaccuracy` 3.

- **`healthy`** — a list of `[gasbagNode, "panels"]` pairs, 5/6/9 per zeppelin, 316 total.
- **`num_healthy_required`** ∈ {2,3,4,5}. **⚠ Note the polarity: this is how many gasbags must REMAIN
  intact**, i.e. the zeppelin dies when the surviving count drops *below* it. The design expresses the
  same rule as a destroy-count threshold. Getting the inversion wrong is a silent off-by-one that only
  shows up as a zeppelin that will not die.
- **`engines`** — flat node list, 12 / 14 / 18 entries.
- **`gasbags`** — `[name, hp, [animName]]`; hp 80–400.
- **`left_cannons` / `right_cannons`** — `[cannonNode, deployAnim, retractAnim]`, **3 or 6 per side,
  always symmetric**. The deploy/retract pair is the design's hatch-open-then-fire sequence.
- **`cannon_health`** — 7 elements: `[cannonName, "gunback", "frame", attachedGasbag, hp,
  [destroyAnim], [[0.6, anim], [0.3, anim]]]`. **The two damage stages are fractions 0.60 / 0.30** —
  the same dominant two-stage progression M3 measured across the world destructibles
  (`docs/formats/destructibles.md`). A cannon is bound to a specific gasbag, matching the design's
  rule that a hatch hit can take out its gasbag section.
- **`cannon_fire_delay`** ∈ {10, 15, 20} s — the design's slow broadside re-fire, and 20 s is the
  value it names. **`cannon_fire_range`** ∈ {500, 1000, 2000, 3000, 15000} m — the design leaves
  maximum range "to be tuned"; the data settles it per zeppelin.

Worked example — `extracted/C1/M04/zrdr/zeppelins.zrd.json`, `piratezep`: net `PirateZep1`, 6 gasbags
at hp 120 with `num_healthy_required 4`, 12 engines, 6 cannons per side, `cannon_fire_delay 20`,
`cannon_fire_range 500`, `targets ["player"]`, **no `cannon_health`**.

### 6. Combat voice — `extracted/soundsh/`

**2,520 unpacked files** (2,521 in `soundsh.zip`; the one-file difference is a Windows
case-insensitivity collision between `VO_c4-RM-m3_blacke_9.wav` and `…_Blacke_9.wav` — **the unpacked
folder is lossy by exactly one file**, worth knowing before anything audits the set). Of the 2,299
`VO_*` clips, **1,309 are combat lines** named `VO_id<pilotId>_<TYPE>.wav` (1,309/1,309 parse, zero
exceptions); the other 990 are mission-scripted dialogue named
`VO_<chapter>-<faction>-<mission>_<Character>_<n>.wav`. `soundsl/` is the low-fidelity mirror with an
identical id set and is never opened by the engine.

**31 distinct pilot ids**, sparse: 1, 2, 4, 6, 7, 11, 12, 14, 16, 18, 20–29, 31, 32, 34, 37–39, 41,
42, 44, 47, 48. Clips per id range 23–74. **Id 47 is the multiplayer announcer** — it alone carries
the `CTF`, `ZZ` and `GameOver` families and none of the pilot families.

**125 distinct TYPE tokens in 11 families.** This is the shipped trigger taxonomy, and it maps almost
one-to-one onto the design's communication-trigger list:

| Family | clips | Tokens | Design trigger |
|---|---|---|---|
| `WA` | 188 | `WA-Enemy-{12,3,6,9}{,H,L}` (12), `WA-Attack-A/B/C`, `WA-HighDmg-A/B`, `WA-Turret-A/B` | threat warning: bearing call-out, enemy threatening, player zone at 70 %, player entered a turret's arc |
| `DI` | 100 | `DI-LowDmg-A/B`, `DI-MedDmg-A/B`, `DI-HighDmg-A/B` | distress at three zone-damage tiers |
| `DE` | 99 | `DE-Bail-A/B`, `DE-NoBail-A/B` | destroyed, with / without a successful bail-out |
| `DA` | 68 | `DA-Bail-A/B`, `DA-NoBail-A/B` | the ally counterpart of `DE` — ⚠ *confirmed 2026-08-10: both are the dying pilot's own cry, split by team (open question 6)* |
| `DS` | 48 | `DS-Ally-A/B/C` | ally distress |
| `GL` | 207 | `GL-AllyDwn-A/B/C`, `GL-EnemyDwn-A/B/C`, `GL-PlyrDwn-A/B/C` | gloat, by whose plane went down |
| `PR` | 248 | `PR-EnemyDwn-A/B/C`, `PR-EngineDst-A/B/C`, `PR-ObjDst-A/B`, `PR-ZepDst-A/B/C`, `PR-DngrZn-A/B/C/D`, `PR-DangerZone-A/B` | praise |
| `TA` | 300 | `TA-FailShk-A/B/C/D`, `TA-SucShk-A/B/C/D`, `TA-FailTail-A/B/C/D` | taunt |
| `CTF` | 30 | 6 tokens × {plain, −1…−4} | multiplayer capture-the-flag |
| `ZZ` | 20 | `ZZ-GasB-Dest`, `ZZ-GasB-Lost`, `ZZ-Zep-Dest`, `ZZ-Zep-Lost`, each × {plain, −1…−4} | zeppelin / gasbag lost or destroyed |
| `GameOver` | 1 | — | — |

Three findings worth carrying forward:

- **The o'clock set is `H`/`L`, and only 7 of 31 ids have it** (ids 2, 7, 24, 26, 29, 31, 48 → 84
  clips). Bearing call-outs are a *wingman* capability, not a universal one — which is what the design
  says: how much positional detail a pilot gives is itself a per-pilot stat.
- **`TA-FailTail` is a trigger the design's taunt table does not list.** It maps cleanly onto the
  position-update rule where a pursuer that fails its tail check is stunned and flies straight. The
  shipped audio therefore documents a behaviour the prose omits.
- **`PR-DngrZn-A/B/C/D` and `PR-DangerZone-A/B` are two spellings of one concept** that coexist in the
  shipped data. A name-driven lookup must handle both.

**The `aiv` → voice chain**: `aiv` field 65 → row in `extracted/zrdr/voice.zrd.json` (35 rows; rows
0–10 are 2–3-id pools, rows 11–34 are single ids) → `soundsh/VO_id<N>_*`. All 21 observed field-65
values are valid rows. **Caveat: 5 of the 21 mapped VO ids (5, 15, 17, 36, 40) have no clips at all.**
Treat the chain as strongly-supported inference, not proven fact.

⚠ **Confirmed 2026-08-10 — it is no longer inference.** The chain is traced in the binary, and the
selected pilot's clips are loaded into 29 per-trigger slots at spawn. The table above is the
engine's own taxonomy: `crimson.exe` carries all 29 `TYPE` tokens as an ordered array, so the
families and their order are read, not derived. See
[Delta § Wave E](#wave-e--the-trigger-taxonomy-is-29-ids-and-the-binary-names-all-of-them) and
[`formats/combat-voice.md`](formats/combat-voice.md) — which also settles `DA` vs `DE` (open
question 6), gives the `DI` tiers their 70/50/30 % thresholds, and confirms `TA-FailTail`.

---

## The behavioural specification, re-expressed

Our own engineering restatement of the original design. Values and enumerations are the source's;
the wording and the structure are ours.

### Pilot skill modifiers (12)

Each modifier governs exactly one class of decision, so each is independently tunable and
independently falsifiable. Higher is better throughout. Nine slots are present in the shipped data
(above); the mapping is unproven.

| # | Stat | Governs |
|---|---|---|
| 1 | Dare Devil | Willingness to attempt a Danger Zone run. Maximum = always attempts when one is available. |
| 2 | Natural Touch | Raw handling. Gates which maneuvers the pilot may select, and sets evasion turn/climb rates and obstacle avoidance. |
| 3 | Sixth Sense | Tailing and shaking. While pursuing, it is the reaction time to a target's maneuver; while pursued, it is **how much the pilot eases off to let the player catch up**. |
| 4 | Dead Eye | Gunnery. Defines the radius of a sphere centred on the correct lead point along the target's velocity vector; maximum = every round hits. |
| 5 | Quick Draw | Shot-angle ferocity. Sets how oblique an attack the pilot will take; head-on and rear shots are equally "safe", beam shots riskiest. Maximum = will shoot from any angle. |
| 6 | Steady Hand | Composure. Compared against the last attack's damage weighted by the aircraft's accumulated damage, to decide whether to react at all. Maximum = ignores every attack. |
| 7 | Stun Recovery | How long the pilot flies straight after being flare/sonic-stunned, or after losing a tail. |
| 8 | Signature Maneuvers | A marked subset of the pilot's maneuver list, weighted up during selection so the player can learn a pilot's style. |
| 9 | Signature Approach | The same idea for the initial engagement, chosen per relative-position table. |
| 10 | Preferred Engagement Altitude | Weights maneuver selection toward maneuvers that reach or hold this altitude. |
| 11 | Talker | Comms volume and detail. Low = bare warnings; high = aircraft type, clock bearing and relative altitude. Maps directly onto the `WA-Enemy-*` set being present on only 7 pilots. |
| 12 | Constitution | Pilot health, and the bail-out probability once shot down. Aces are flagged to always bail; a catastrophic-collision kill never allows a bail. Maps directly onto the `DE-Bail` / `DE-NoBail` split. |

### The five-mode state machine

Two concentric radii per AI aircraft drive activation. The **activation volume is always larger than
the player's view range**, which is the invariant that lets Inactive be a genuine zero-cost state
without the player ever seeing a frozen aircraft. The **attack volume** is the smaller inner sphere.
`aiv` field 31 (350–1600 m) is the shipped radius candidate.

| Mode | Entered when | Does | Leaves when |
|---|---|---|---|
| **Inactive** | Flagged at mission start; or no enemy left in the activation volume while patrolling | Nothing at all — no movement, no command processing | An enemy enters the activation volume → Patrol |
| **Patrol** | Enemy enters activation volume; or flagged always-active; or attack volume empties | Flies its assigned net: first to the nearest node, thereafter along connected edges | Enemy enters attack volume → Attack; activation volume empties → Inactive |
| **Attack** | Enemy enters attack volume; or after a successful evasion; or re-initialised when the target dies or escapes | Maneuvers into a valid angle of attack, follows the target through its evasions, fires | Crash/missile avoidance scripts (return to Attack, same target); target destroyed (re-init, else Patrol); health low → Flee; ammo low → Flee; objectives complete → Flee; attacked and the composure check calls for a reaction → Evade; target evades → stunned, then re-init |
| **Evade** | Attacked, and the Steady Hand check calls for a reaction | Runs the maneuver chosen on entry (and picks another after a few seconds if it fails); generally turns out of the attacker's cone (better pilots also change altitude); periodically checks for a Danger Zone run | Crash/missile avoidance (return, same attacker); attacker destroyed → Attack if a target is in range, else Patrol; newly attacked → Evade against the new attacker; attacker shaken → Attack |
| **Flee** | Health or ammo has been very low **for a sustained period** — deliberately time-gated so damaged aircraft do not all leave at once | Maneuvers away from threats, then crosses the mission boundary and is counted destroyed for objective purposes | Attacked and the composure check calls for a reaction → Evade |

### The AI processes

- **Target selection** — take the assigned primary target if one is in the activation zone; otherwise
  rank by mission objective, then break ties toward the target whose velocity vector and altitude are
  closest to the AI's own; then **deconflict against allies** — if a peer already holds the desired
  target, drop it from the pool and re-select; exhausting the pool returns to the top of the script.
- **Target ranking** — objective weight first (lead aircraft above wingmen, **the player deliberately
  last**), then arc (front 120° highest, rear 120° next, the two 90° beam arcs equal), then targets
  moving away above targets closing, then targets below above targets above, all decaying with range.
- **Approach selection** — pick the approach table by relative position (above / level / below ×
  front / side-or-rear), take the Signature Approach from it, fall back to a random pick, then
  collision-check the chosen maneuver and cull-and-retry on a hit.
- **Angle of attack** — Quick Draw sets the half-angle of cones projecting forward and aft from the
  *target*. Outside both cones the shot is refused and the AI keeps maneuvering toward the nearer
  cone; inside, it selects an attack type.
- **Maneuver selection** — cull every maneuver whose difficulty exceeds the pilot's Natural Touch;
  rank the remainder by how well their optimal airspeed / orientation / altitude match the current
  state (dropping any that would stall or drive altitude negative); push recently-used maneuvers down;
  push maneuvers that approach the Preferred Engagement Altitude up; push Signature Maneuvers up;
  collision-check the top entry; fly it, or cull and retry.
- **Danger Zone check** — every *N* seconds while evading, poll the Danger Zones in the forward 180°
  arc, rank by distance, weight by altitude difference and by how well the current velocity vector
  matches the zone's optimal entry vector, cull any zone harder than the pilot's Natural Touch, fly
  the top survivor. **The one concrete formula the design gives is `N = 100 − DareDevil` seconds**,
  which is what pins that stat to 0–100 and makes it independently falsifiable — and which collides
  with the shipped 1–9 scale (see the scale conflict above).
- **Position update** — whenever the AI's target maneuvers, the AI rolls Sixth Sense to decide whether
  it follows; a failure leaves it stunned and flying straight for a while. When the *player* is the
  pursuer, Sixth Sense instead decides whether the AI edges back toward him.
- **Threat / attack analysis** — the AI asks whether it has been fired on since its last check and is
  handed the attacker's identity; then evaluates the last attack's damage weighted by accumulated
  damage against Steady Hand to decide whether to react. Attack analysis runs only after a threat
  analysis has fired.

### The maneuver library

15 table rows, **14 distinct maneuvers** (the lowest-difficulty row is duplicated in the source — a
document artifact, not two maneuvers). The difficulty column runs **1–9** and is the value maneuver
selection compares against Natural Touch. Each entry is implementable as a **timed control-input
program plus an optimal (speed, orientation, altitude) tuple** for ranking — that shape is what makes
the library data rather than code.

| Difficulty | Maneuver | Shape |
|---|---|---|
| 1 | Rudder turn (L/R) | Heading change on rudder alone — slow |
| 2 | Bank turn (L/R) | Heading change combining rudder, aileron and elevator — tight |
| 2 | Roll | Aileron roll holding heading and altitude |
| 3 | Climb | High-angle climb, distinct from ordinary cruise climb |
| 3 | Dive | High-angle dive, likewise |
| 4 | Jinking | Randomised stick jitter in both axes |
| 5 | Scissors / rolling scissors | Weaving across a broadly forward vector; the rolling variant repeats **2–4 times, randomly** |
| 6 | Snap roll | Hard roll-plus-turn that stalls the aircraft and sheds speed |
| 6 | Barrel roll | Corkscrew about the flight path, no rudder snap |
| 7 | Immelmann | Vertical climb resolving into a reversed heading |
| 7 | Loop | Continuous 360° in the vertical |
| 8 | Split-S | Rolling descent resolving level and reversed |
| 8 | Spiral dive | Barrel roll carried downward |
| 9 | Lag pursuit roll | Speed-advantage move: bleeds energy while setting an attack angle |

### The rubber-band assist — deliberate design, not a bug

Two separate mechanisms in the design exist purely to make AI aircraft catchable:

1. **Sixth Sense eases the stick during evasion** so a pursuing player closes.
2. **An attraction effect** pulls an engaged enemy toward the player's forward cone, adjusting the
   AI's path against the player's own vector, speed and orientation so that it "corrects the player's
   errors" and stays easy to tail and hit.

Record it as intentional. **But it is exactly the kind of mechanism that needs a switch** — it is
invisible when it works, indistinguishable from a physics bug when it misfires, and impossible to
A/B against the original without being able to turn it off. The design text itself flags the position
update as needing more work, so the shipped behaviour may not match the described behaviour.
`--no-assist` (or a `Config` key) is a required deliverable of D15, not a nicety.

### Zeppelin AI

- **Net following with stop nodes.** Zeppelins fly their assigned net by default. Individual nodes can
  be tagged as stop points and toggled on/off by mission script; a zeppelin reaching an armed stop
  node halts. Unlike aircraft, zeppelins can hold station in mid-air, which is what makes the
  phased-mission structure work. Retreat is expressed as a net change, not a special mode. Zeppelins
  never make sudden movements, always require forward motion to turn, do not bank, and pitch within a
  per-class limit for climb and descent — the shipped `min_pitch`/`max_pitch` (±30°),
  `max_rate_yaw`/`max_rate_pitch` and `accel_*` values are exactly those limits.
- **Broadside cannons.** A node can trigger fire or hold-fire. **One side fires at a time** and the
  animation is side-specific: hatch opens, muzzle burst, fly-out. The zeppelin may make a temporary
  turn to bring a target into arc, then resumes its path. Re-fire is deliberately slow —
  `cannon_fire_delay`, 20 s in the design and in the data.
- **Broadside hit resolution is probabilistic, not ballistic.** ⚠ **Refuted 2026-08-10 — the shipped
  engine fires real projectiles with a lead solve; this whole bullet is design-era. See the wave F
  delta above.** Hits are rolled against distance:
  **20 % at maximum range, ramping linearly to 100 % at roughly 200 m**, and only within a **90° arc
  centred on the perpendicular of the firing side**. Damage is per-cannon damage × the number of
  cannons still alive in the volley (so destroying cannons directly weakens the broadside), applied to
  a **randomly chosen section** of the target. `cannon_fire_range` (500–15000 m) supplies the maximum
  range the design left to tuning.
- **Fighter release.** A zeppelin launches by dropping fighters from its hangar, so it must be **at or
  above a minimum altitude**; below it, generation is held rather than cancelled. The door-open
  animation runs, the wave spawns, the door-close animation runs. This is exactly the shape of the 17
  zeppelin `egen` generators.
- **Non-linear deceleration as engines die.** ⚠ **Refuted 2026-08-10 — the shipped curve is a square
  root, not these bands. See the wave F delta above.** Speed and acceleration loss is banded by the *fraction*
  of engines destroyed, not the count: the first 30 % of engines cost 10 % total, the 30–70 % band
  costs a further 40 %, and the last 30 % costs the remaining 50 % — so the reduction per engine grows
  through each band and reaches 100 % at total engine loss. Percentages are absolute, cumulative
  across bands.
- **Gasbags are the critical zone.** Gasbags, engines, turrets, cranes and battlements are all
  independently destroyable; only gasbags are critical. Gasbags are shielded against ordinary gunfire
  and need a torpedo, or an adjacent engine/cannon fire, to breach. The zeppelin dies when the
  surviving gasbag count falls below `num_healthy_required`.

### Damage zones — the general model this all sits in

The design's object damage model is not zeppelin-specific and is worth recording once: every object
divides into **damage zones**, each either **critical** or **non-critical**; a zone dies at zero HP;
the object dies when a **threshold** number of *critical* zones have died. Its own examples: aircraft
= 1 of {tail, nose, wings} critical, radio and weapons non-critical; **zeppelin = 3 of gasbags 1–4**,
engines and turrets non-critical; battleship = 2 of 3 hull sections; bunker = 1 of 1; train = the
engine, with cars non-critical. **This is the generalisation of `num_healthy_required`, and it is the
shape our `DestructibleRegistry` does not have** (see A4).

---

## Open questions to decide before implementing

These are places the source is ambiguous, self-contradictory, or contradicted by the shipped data.
**Each is a decision to make explicitly, not a detail to resolve silently in code.**

1. **Evade entry: does passing or failing the composure check trigger the reaction?** The source says
   both, in two places about ten lines apart — the mode's activation summary says the reaction follows
   a *failed* Steady Hand check, and the prose immediately under it says a *passed* one. The
   *mechanism* is stated unambiguously elsewhere and both attack analysis passages agree on it:
   **react when the last attack's damage, weighted by accumulated damage, exceeds Steady Hand**, and a
   maximum Steady Hand ignores every attack. So the arithmetic is settled and only the pass/fail
   vocabulary conflicts. **Decide the naming once, in one place, and never restate it** — this is the
   single most likely source of an inverted-stat bug in the whole milestone.
2. **Two loop-back targets in the process step-lists point at the wrong step.** Maneuver selection's
   collision step says to remove the failing maneuver and repeat the *weighting* step, not the
   collision check — a literal implementation re-weights an already-weighted list and never re-tests.
   Approach selection has the same shape: its cull step loops back to the random-pick step rather than
   through the validity check that decides the loop. Both are almost certainly meant to be "take the
   next candidate and re-run the collision check". **Record the intended loop explicitly** rather than
   transcribing the step numbers.
3. **The stat scale: 0–100 or 1–9?** See "The scale conflict" above. The Danger Zone formula implies
   0–100; the shipped data and the maneuver difficulty column both say 1–9. Pick the rescaling, write
   it down as a TUNE, and make the Danger Zone interval a `Config` key.
4. **The skill-slot mapping.** Nine shipped slots against twelve designed stats. Which nine, in what
   order, and where (if anywhere) the other three live. D10 names the discriminating cases.
   **`ia.zrd.json`'s `ace_stats` independently fixes the count at nine**, so this is a bounded
   mapping question, not open-ended reverse engineering.
5. **`num_healthy_required` polarity.** Data counts survivors; the design counts casualties. Assert the
   direction in a test, because the failure mode (an immortal zeppelin) looks like a damage bug.
6. **`DA-*` vs `DE-*` and `DS-*` vs `DI-*`.** Two destroyed families and two distress families ship.
   The natural reading is self vs ally, and it fits the design's four destroyed rows (target bails /
   target does not / ally bails / ally does not). **Unconfirmed** — the clip contents have not been
   listened to. Cheap to settle; do it before wiring triggers.
   ⚠ **Closed 2026-08-10 — and the natural reading was half wrong.** `DA` and `DE` are both the
   *dying pilot's own* death cry, split by **team**, not by self-vs-ally: id 20 if the aircraft is
   on the player's team, id 21 if not. `DI` is the speaker's own damage (ids 17–19 at 70/50/30 %)
   and `DS` is ally distress (id 28). See
   [`formats/combat-voice.md`](formats/combat-voice.md). No listening required.
7. **What `<Cx>/<mission>/zrdr/net.zrd.json` actually is.** Undecoded. Shape says spawn table. It is
   *not* needed for patrol, so this is a curiosity, not a blocker — but do not let a future session
   waste a day on it assuming it is the route data.

---

## The build assessment

### What is reusable exactly as it stands

First verified 2026-07-25; re-verified and re-pointed 2026-08-13 against `0b2385e`.

- **`FlightModel` is AI-ready.** `Flight/FlightModel.cs:440` is `public void Step(FlightInput input,
  float dt)`; `FlightInput` (`:8-11`) is a plain struct of four floats — pitch, roll, yaw, throttle —
  with no device, player or camera coupling. `FlightModel` (`:49`) is a plain sealed class, not a
  Node; its only non-mathematical dependency is the `Config` tuning layer. **An AI pilot is an input
  generator and the entire flight half is free.**
- **`ProjectilePool` is a shared-world subsystem and now carries the firer.** `Flight/Projectile.cs:581`
  — `Spawn(WeaponDef, Transform3D muzzle, Vector3 inheritVel, int shooterId = NoShooter,
  Node3D? muzzleAnchor = null, Vector3? aimDir = null)`. The shooter id reaches the physics query:
  per-shot `Exclude` is set and reset (`:776`/`:778`) via the shooter's `AircraftBody.ExcludeSelf`
  (`:2061-2071`). It is already fed from unrelated sources (flight guns, flight rockets, the weapon
  lab, `IncomingFire`) — AI fire needs no new subsystem.
- **`SpawnPoints` already reads mission zrdr.** `Flight/SpawnPoints.cs:28`/`:56` load `ia.json`
  `spawn_points` and `objectives.json` `PLAYER_INIT` into a `SpawnPoint(Vector3, float HeadingDeg)`.
  `aiv`'s position + heading are the same units and convention, and `Zrdr.CandidateNames`
  (`Mech3/Zrdr.cs:169-175`) already resolves a logical `aiv.json` to the on-disk `aiv.zrd.json`. A
  `LoadAiv` sibling is roughly thirty lines and changes nothing else (none exists yet —
  `Mech3/MissionSetup.cs:26-30` documents the file but reads nothing).
- **The sim clock is in place.** `Utils/GameClock.cs` gives fixed-dt stepping under `--det` with an
  established consumer contract (`SimStep(float dt)` plus a `PhysicsDt`-returns-zero guard,
  `:23-28`). An AI controller slots into `GameSession.DriveSimSteps`
  (`Session/GameSession.cs:2185`, consumers registered in order at `:2205-2217`) exactly like
  `IncomingFire`, `ProjectilePool` and each rig's `FlightController` already do.
- **N aircraft in one world is normal.** Splitscreen builds up to four `FlightController`s in a
  shared `World3D` (`UI/SplitScreen.cs:118-161`; rigs assembled per player by
  `Session/FlightRigAssembler.cs:55`), every aircraft renders in every pane, and there is **no
  mutable static state in `CSVM/src/Flight/`** — no `Instance`, no `Current`, only factory methods
  and pure helpers.
- **Voice playback has most of its plumbing.** `Mech3/WorldSounds.cs:188` —
  `PlayOneShot(string name, Vector3 worldPos, Random rng)` — creates a positioned 3D one-shot with
  correct attenuation and sweeps it when done. The session opens `extracted/soundsh.zip`
  (`Session/Launcher.cs:219`, `--sounds` overrides; archive opening centralised in
  `SessionArchives.OpenFor`, `Mech3/SessionArchives.cs:53`), so **every combat clip is already
  reachable**. `Mech3/Messages.cs` resolves the `MSG_*_NAME` keys the rosters carry.
- **Aim maths for D14 and C9 is landed, not future.** `Flight/AimAssist.cs` carries a
  constant-velocity intercept solver and a per-fire candidate scan whose `Turrets` list already
  exists and iterates empty (`:387`; filled via `AddTurret`, `:485`);
  `FlightController.IsHumanPiloted` is the human-vs-AI gate the assist already honours.
- **`AnimRuntime` + `DestructibleRegistry` are the right shape for zeppelin parts.** Motions write
  *local* transforms (`Mech3/Anim/MotionRuntime.cs:591`) and attached emitters, lights and sounds
  re-sample their host's global pose every frame (`Mech3/Anim/EmitterDirector.cs:305-330`,
  `AnimRuntime.cs:3079`), so a definition bound to a **moving** zeppelin animates correctly today —
  the existing compiled zeppelin definitions already do this.

### The hard prerequisite — landed 2026-08-06

**When this study was written, nothing could shoot an aeroplane, because the flying aircraft had no
physics body.** That gated the milestone, and it is A1 — front-loaded by PLAN-vs-mode (landed
2026-08-06) and re-verified in place 2026-08-13:

- `Flight/CollisionLayers.cs:11-22` defines the layer scheme: `World` (layer 1, everything
  pre-aircraft) and `Aircraft` (layer 2), plus `WorldAndAircraft` for the queries that want both.
- `Flight/AircraftBody.cs:16` — an `AnimatableBody3D` per plane, built from the **same**
  `PlaneCollider.Parts` `BoxShape3D` resources (`:32-45`, single-sourced by design), on the
  `Aircraft` layer with `CollisionMask = 0` (a query target, never a collider). Created and
  registered at `FlightController.cs:582-585`.
- Projectiles hit planes: `Projectile.cs:1534` routes an `AircraftBody` hit to its plane — direct
  hits, the aircraft-only proximity fuse and blast falloff (`:1551`). Kills are attributed
  (`FlightController.Downed`, `:446`, raised `:1829`; scored at `GameSession.cs:1767`).
- The two consequences the study warned about were both handled there: plane-versus-plane
  visibility went in with the layer scheme, and the shared `_ray` exclusion leak is prevented by
  the per-shot set/reset pair (`Projectile.cs:776`/`:778`).
- The `air-to-air` suite pins all of it (`Testing/Suites.cs:148`, body `:2538`): part mapping,
  armour-first damage, self-hit zero, kill attribution, fuse + blast falloff.

What A1 did **not** deliver — and what stays open for this plan — is anything that *uses* the
hittability: no non-player shooter exists (`IncomingFire` is a scripted test source, not an AI),
so the `player` IMPACT row (`BL-222`) and the incoming-fire cues (`BL-226`) stay untriggerable
until A2 and the D-wave field one.

### Architecture constraints worth recording now, before the code that trips on them exists

- **`DestructibleRegistry` cannot express a multi-zone object.** It keys instances by
  `(AnimDefinition Def, ulong Anchor)` with a **single scalar `float Health`**
  (`Mech3/DestructibleRegistry.cs:29`, `:161`); `DamageStage` is an index into descending absolute
  thresholds **on that same single pool** (`:168`), not a zone count. `Resolve` (`:123-135`) maps a
  struck node up to *one* instance and stops. A zeppelin needing N of M gasbags — and, per the
  design's general model, a battleship needing 2 of 3 hull sections — requires **`(def, anchor,
  zone)` plus a threshold counter and a parent aggregator**. **Cheaper to design now than to
  retrofit after M4's other items have added callers.** (`WeaponDef.DamagesZeppelin`, parsed at
  `Flight/WeaponDefs.cs:102`/`:255` and dumped but never consumed, is the surviving evidence that
  the original had a separate zeppelin damage channel.)
- **Zeppelin sub-part definitions are deliberately un-anchorable today.** `NameResolver.Anchors`
  (`Mech3/Anim/NameResolver.cs:277-285`) refuses multi-target `NAME1` definitions and its comment
  names exactly this case — zeppelin nacelles and turrets parse with an empty name and are treated
  as object-wiring scope. F18 must change that rule deliberately, not by accident.
- **The world tree's node index is add-only, by documented contract.** `NameResolver.FindAll`
  memoises `_index` (`Mech3/Anim/NameResolver.cs:195-216`); a post-bootstrap subtree may `Add`
  rows provided the caller also calls `ClearFindCache` (`:25-34`, `:170-183`), with two working
  precedents (`AnimRuntime.IndexStage`, `:999-1004`; `IndexPooledCopy`, `:1019`). **Spawning an AI
  aircraft still silently breaks animation resolution if A2 skips that invalidation** — but as of
  2026-08-13 it is a contract to follow, not an unknown to discover.
- **Effect templates snap to an absolute world point and do not track.** `TemplateStage.PlaceAt`
  (`Mech3/Anim/TemplateStage.cs:374`, absolute write `:391`) places a pooled per-call copy
  (`BL-225` fixed the collapse-onto-one-site half), deliberately decoupled from a moving caller —
  so a hit effect on a moving zeppelin still stays where the hit happened while the zeppelin flies
  on.
- **`AnimRuntime.PlayerPosition` and the sound listener are genuine player-one singletons.**
  `PlayerPositions` (nearest-of-a-set, `AnimRuntime.cs:117`) exists but has exactly one consumer
  (`EXECUTION_BY_RANGE`, `:1909`); `PLAYER_RANGE` conditions (`:3268`, fed from player one's
  camera at `GameSession.cs:652-654`) and the `WorldSounds` listener (`WorldSession.cs:275`) still
  measure from player one. With AI aircraft fighting across the map, range-gated effects will fire
  based on where the human is looking rather than where the action is. It is a `Func<Vector3>` →
  nearest-of-a-set change, and it is inherited free of charge the moment AI exists.
- **A one-shot sound does not follow its source.** `PlayOneShot` takes a `Vector3`, written once
  (`WorldSounds.cs:188`, `:220`) — a voice line from a moving aircraft would be frozen at its
  firing coordinate. The host-following emitter path exists but is reserved for ambient
  `SOUND_NODE` emitters (`:234`).
- **A clip that was not prewarmed silently never plays.** `WorldSounds.Loader` (`:51`) is valid
  only during world build (nulled at `WorldSession.cs:384` outside the Lab intent); every voice
  name must join the prewarm set (`Prewarm`, `:115`).
- **The dialogue-chain sound groups are parsed and discarded.** `Mech3/SoundDefs.cs:106-122` records
  that voice-over chains nest a list where a member's own name would be, contribute no weighted member,
  and are skipped — leaving the group unregistered. `LoadGroups` needs a second entry shape before any
  comms chain is addressable by name.
- **There is no subtitle or voice-line display path at all.** The templates to copy are
  `Flight/MarkerHud.cs:114-120`'s one-shot banner in the HUD bitmap font and the newer dogfight
  kill banner in `Flight/VersusHud.cs` (wired at `FlightRigAssembler.cs:329-341`).

---

## Ground rules

Carried from every prior plan here; they apply unchanged.

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often — and this
  milestone has more room for invention than any before it.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here.** Every
  item's Evidence line carries its confidence.
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message and is **deleted**
  from `backlog.md`. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node/collider counts unless the change is meant to add coverage) plus a targeted capture.
- **Read the module's entry in `docs/architecture.md` before modifying it.**
- Scratch output goes to `./.scratch/`; anything `docs/` cites moves to `analysis/`.

---

## Proposed wave ordering

The ordering below **differs from the one this study was commissioned with**, in four places. The
original proposal was: air-to-air hit detection → turret AI → patrol nets → decode `aiv` numerics →
attack/evade/flee + maneuvers → comms → zeppelin AI. The changes and the reasoning:

1. **The spawn/actor seam joins hit detection in wave A.** The riskiest unknown in the milestone is
   not combat — it is whether an AI aircraft can be *added to the world at all*, given that
   `AnimRuntime` memoises its node index on the documented promise that nothing is ever added. That
   answer can change the shape of every later item, so it must come first, not third.
2. **Turret AI moves after non-combat presence, not before it.** Turret AI is a *leaf*: it attaches to
   existing static nodes, spawns nothing, flies nothing, and teaches the rest of the milestone
   nothing. It is genuinely the cheapest deliverable and it is genuinely screenshot-verifiable — but
   "cheap and verifiable" is an argument for scheduling it where it costs least, not for spending the
   first wave on it. It also is **not** independent of A1: a turret that cannot damage the player is
   as unobservable as an attacking fighter.
3. **The `aiv` decode moves much earlier — into wave A as a documentation item.** The skill vector is
   already localised to nine slots with 29 authored examples (above); finishing it is cheap *now*, it
   is pure data work that parallelises with everything, and every later tuning decision depends on
   which slot is which. Doing it after the state machine means building against invented constants and
   re-tuning afterwards.
4. **Comms splits, and the non-combat half moves earlier.** The `WA-Turret` and `PR-DngrZn` triggers
   are exercisable today with stunt mode plus turrets, and the comms subsystem has three blockers the
   original ordering would have discovered last: one-shots do not follow their source, unprewarmed
   clips are silent, and the dialogue-chain groups are parsed-then-discarded. Find that in wave B, not
   wave F.

Zeppelins staying last is right, with one carve-out: the **multi-zone damage decision** (not the
implementation) belongs in wave A, before `DestructibleRegistry` grows more callers.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items
land.** The already-ticked items are ones other work reached first (A1 via PLAN-vs-mode, D10 and
most of A3 via the 2026-08-10 decompile pass), not items started under this plan.

### Wave A — Foundations (nothing downstream is verifiable without these)

1. ☑ A1 — Air-to-air hittability: collision layers, aircraft bodies, projectile owner-exclusion
   — **front-loaded by PLAN-vs-mode (landed 2026-08-06)**: `CollisionLayers`, `AircraftBody` on
   the shared `PlaneCollider` boxes, per-shot owner exclusion, part-mapped damage, attributed
   kills, aircraft-only proximity fuse + blast falloff, all pinned by the `air-to-air` suite
2. ☐ A2 — The AI actor seam: runtime spawn, a non-player `FlightModel` driver, `GameClock` wiring
3. ◐ A3 — the AI format pages — **rosters, skills and maneuvers landed 2026-08-10**
   ([`formats/ai-rosters.md`](formats/ai-rosters.md)); nets already had
   [`formats/ai-nets.md`](formats/ai-nets.md), zeppelins/generators
   [`formats/mission-entities.md`](formats/mission-entities.md), and the `ai.zrd` turrets landed
   2026-08-10 as [`formats/turrets.md`](formats/turrets.md). **Remaining: `--dump-ai`, plus a
   check that `formats/zrdr.md`'s family index rows exist for all five families**
4. ☐ A4 — **Decision + design note only:** multi-zone destructibles and the kill threshold

### Wave B — Non-combat presence

5. ☐ B5 — Net following: the `ne`/`neindex` graph as a patrol behaviour
6. ☐ B6 — Generators: `egen` waves, capacity and periods
7. ☐ B7 — Formation flying — ⚠ **premise refuted 2026-08-10: there is no leader field** (slot 6 is
   `primary_target`). Re-scope onto `group` (slot 4) or drop
8. ☐ B8 — The voice runtime: prewarm, source-following one-shots, the `aiv`→voice→clip chain

### Wave C — Emplacements

9. ☐ C9 — Turret and AA AI from `ai.zrd.json`, both structural families — **spec complete
   2026-08-10** in [`formats/turrets.md`](formats/turrets.md): the `CREATE_STANDALONE` placement
   split, the `PARTS` kinematic chain, the wrap-aware yaw arc (⚠ `[0,0]` = unrestricted), the
   attack/bored duty cycle, rate-limited slew + the 15° fire gate, and geometric hit resolution.
   Cost is up — a tracking loop, not a table read. **Split 2026-08-10:**
    - ☑ **C9a — carried turrets** (16 entries, all `ACTIVATED 1`, 8 AI + 8 player airframes). No
      activation dependency; this is the turret-UI half and what the zeppelin hunt exercises
      — **landed 2026-08-13**: `TurretDefs` + `TurretController` drive the five player airframes'
      `thirdp` rigs as live gunners registered with `AimAssist` (the `carried-turrets` suite +
      `TurretDefsTests` pin it); `_G1`/`_G3` turned out to be the `firstp`/`thirdp` viewpoint
      rigs of vehicle.zrd's `turrets` block, not gun-group slots (turrets.md corrected); the
      AI-carried half awaits A2's hosts
    - ☐ **C9b — world emplacements** (26 entries, 22 of them dormant). Needs an activation stand-in
      for `WAKEUP_TURRETS`; deferrable, since nothing on the playable path depends on it

### Wave D — The pilot model

10. ☑ D10 — Skill-slot mapping and scale — **settled 2026-08-10** from the binary + the shipped
    `ai_skill_parameters` curves; no longer blocks D11–D15, which now load constants
11. ☐ D11 — The state machine — **nine modes, not five** (and no `flee`/`inactive` in the dispatch);
    activation radius is `min_ai_active_dist` 2000 m
12. ☐ D12 — Target selection, ranking and ally deconfliction — ranking formula recovered
13. ☐ D13 — The maneuver library — **shipped as `maneuvers.zrd`**, so this is a loader plus
    selection/culling, not an authoring job
14. ☐ D14 — Gunnery: the lead-sphere accuracy model and the shot-angle cones
15. ☐ D15 — The rubber-band assist, behind a switch

### Wave E — Communication

16. ☐ E16 — Trigger dispatch across the shipped taxonomy, gated by the talker stat — **spec complete
    2026-08-10** in [`formats/combat-voice.md`](formats/combat-voice.md): 29 trigger ids named by the
    binary, the `accentID`→`voice.zrd`→pilot chain traced, the `DI` thresholds at 70/50/30 %, the
    computed bearing index, ⚠ the 15 s cooldown armed by a *failed* roll, ⚠ the hardcoded halving on
    ids 1–12, and broadcasts as a speaker election rather than N rolls. Cost down; still needs B8

### Wave F — Zeppelins

17. ☐ F17 — Zeppelin motion: net following, pitch/rate limits, engine-loss deceleration —
    **the deceleration curve is decoded (a square root, not the design's bands)**; stop nodes are
    confirmed to exist as a scripted concept (`COMPLETED_STOPPOINT`) but their per-node tag
    encoding is still undecoded
18. ☐ F18 — Multi-zone zeppelin damage — **the survivor threshold is decoded and its polarity
    confirmed against the engine**; the multi-zone `DestructibleRegistry` work (A4) is what remains
19. ☐ F19 — Broadside cannons: side-alternating volleys and the 90° arc — ⚠ **build it ballistic:
    the probabilistic hit curve is design-era and is not in the shipped engine**
20. ☐ F20 — Zeppelin fighter launch — **the launch cycle is decoded** (hold-not-cancel confirmed,
    door timings hardcoded, `ind_period`+`wave_period` compose); ⚠ **carries the unresolved
    `capacity` discrepancy** — budget an investigation

### Wave G — AI aircraft crash choreography (added 2026-08-13)

21. ☐ G21 — The `ai_crash_<name>` surface family (ex-`BL-347`): locate the dispatch, model the
    registry-indexed vector via `Session/SurfaceDefTable.cs` (probably reusable verbatim), and
    wire it to AI aircraft terrain crashes. ⚠ Keep `BL-343` (`IMPACT_FORCE` wreckage velocity
    inheritance) a separate change

## Dependency and parallelism notes

**A1 landed 2026-08-06, so A2 is the sole remaining true blocker** — the spawn + driver seam
(`GameSession.cs` / `FlightRigAssembler.cs` / a new AI controller / `NameResolver`'s index
invalidation). **A3 and A4 are documentation and design, touch no engine code, and can run
alongside anything.**

B5–B8 all need A2. B5 blocks F17 (the same net-follower serves both). B8 blocks E16. **C9a
(carried turrets) needs only A1, which is landed — it is startable immediately**, in parallel with
A2 and all of wave B, and has no activation dependency (all 16 ship awake); it is the half the
zeppelin hunt needs, and its engine seam already exists (`AimAssist.AddTurret`). **C9b (world
emplacements)** additionally needs a stand-in for `WAKEUP_TURRETS` for its 22 dormant entries;
that is a decision, not a blocker, and it no longer gates anything playable.

Within D: **D10 is settled, so it no longer blocks D11–D15** — they read the shipped skill vector and
`ai_skill_parameters` directly and can all start at once. D11 blocks D12 and D15. D13 is independent
of D12 and can run alongside it. D14 needs A1 but not D11.

Within F: A4's decision blocks F18. F17 needs B5. F19 needs F17 (the arc is relative to a moving
hull) and F18 (destroyed cannons weaken the volley). F20 needs B6 and F17.

G21 needs A2 (an AI aircraft must exist and be able to crash); it reuses `SurfaceDefTable` and
contends with nobody.

**File contention to watch.** D11–D15 all reach into whatever A2 creates as the AI controller — give
each concurrent agent a named region. F18 and A4 both concern `DestructibleRegistry.cs`; A4 lands
first as a note, F18 implements it. E16 and B8 both touch the voice runtime. C9 and D14 both want
lead-and-scatter aiming maths — the intercept solver already exists in `Flight/AimAssist.cs`;
consume it rather than re-deriving it, and say so in whichever lands first.
