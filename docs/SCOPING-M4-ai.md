# Milestone 4 — Artificial Intelligence (scoping study)

> **⚠ SCOPED, NOT STARTED, NOT SCHEDULED — written 2026-07-25; premises re-checked 2026-08-04;
> roster/skill/maneuver half re-decoded from the binary 2026-08-10.**
> This is **not** a live plan. The active plan is whatever PROJECT_CONTEXT.md's "Current status"
> names (`PLAN-m3-polish-6.md` at re-check time). This file is deliberately **not** called
> `PLAN-M4-ai.md`, because this repo's convention is that a `PLAN-*.md` sitting in `docs/` *is
> live* — a second one here would read as scheduled work. It carries the plan **shape** (waves,
> IDs, per-item Goal/Evidence/Approach/Verify) so that scheduling it is a rename to
> `docs/PLAN-M4-ai.md`, a date, and a PROJECT_CONTEXT.md pointer swap — nothing else.
>
> No *engine* work in here has been implemented, and none should be until M4 is scheduled. The
> ticked checklist items are documentation and decode work that other sessions reached first —
> read the dated Delta sections below before trusting any body text.

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

**Citation drift (mechanical — not tracked separately; re-point as part of rewriting this document
when M4 is scheduled):**

- `PlaneViewer.cs` no longer exists — PLAN-planeviewer-split (2026-07-30) moved it to
  `Session/GameSession.cs`, `Launcher.cs`, `FlightRigAssembler.cs` and friends. Every
  `PlaneViewer.cs:<line>` citation below is dead; the *claims* they anchor were all re-verified
  true at the new sites. `DriveSimSteps` lives in `GameSession.cs`.
- The source root is `CSVM/src/`, not `src/`; `AnimRuntime`'s motions moved to `Mech3/Anim/`
  (`MotionRuntime.cs`, `EmitterDirector.cs`, …). All other cited files exist with line drift only.
- Archive opening is centralised in `SessionArchives.OpenFor(intent)` — semantics unchanged, but
  note `SoundsOutliveBuild` is true only for `ArchiveIntent.Lab`; B8's prewarm work happens under
  whatever intent the flight session opens with.

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

**Still not examined**, and costed as written: turret AI internals (C9 — `ai.zrd` is
self-describing anyway) and E16's trigger dispatch, though the binary does show voice lines are
gated by a numeric id behind a `talker` roll (*"Talker test passed. Play AI sound #%d."*). The three
unnamed roster slots are localised to 8–19 and are unauthored install-wide — deliberately left
unresolved.

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
| **Traced to an exact mechanism in code or data, reproducible by a committed script** | A1–A4, B5, B6, B8, C9, F17, F19, F20 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call (TUNE, not fact)** | D14, D15, E16, F18 | The *what* is settled; the *how much* goes on `backlog.md`'s TUNE list, never invented as fact. |
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

Key coverage across the 42: `TITLE` 42, `ACTIVATED` 42, `PARTS` 42, `WEAPON` 42, `INACCURACY` 42,
`ATTACK_INTERVAL` 42, `BORED_INTERVAL` 42, `PITCH` 37, `SOUNDS` 37, `YAW` 36, `NODES` 26, `TEAM` 20,
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
lists (`["brigturret", "hgun", ["hfirepoint", "hfirepoint1"]]`).

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
| `DA` | 68 | `DA-Bail-A/B`, `DA-NoBail-A/B` | the ally counterpart of `DE` *(inferred — see open question 6)* |
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
7. **What `<Cx>/<mission>/zrdr/net.zrd.json` actually is.** Undecoded. Shape says spawn table. It is
   *not* needed for patrol, so this is a curiosity, not a blocker — but do not let a future session
   waste a day on it assuming it is the route data.

---

## The build assessment

### What is reusable exactly as it stands

Verified against the tree at `CSVM/src/` on 2026-07-25.

- **`FlightModel` is AI-ready.** `Flight/FlightModel.cs:110` is `public void Step(FlightInput input,
  float dt)`; `FlightInput` (`:8-11`) is a plain struct of four floats — pitch, roll, yaw, throttle —
  with no device, player or camera coupling. `FlightModel` (`:29`) is a plain sealed class, not a
  Node; its only non-mathematical dependency is the `Config` tuning layer. **An AI pilot is an input
  generator and the entire flight half is free.**
- **`ProjectilePool` is already a shared-world subsystem.** `Flight/Projectile.cs:209` —
  `Spawn(WeaponDef, Transform3D muzzle, Vector3 inheritVel)`. Nothing about the firer is passed, and
  it is already fed from three unrelated sources (flight guns, flight rockets, the viewer's weapon
  lab, which has no player at all). AI fire needs no new subsystem.
- **`SpawnPoints` already reads mission zrdr.** `Flight/SpawnPoints.cs:28`/`:56` load `ia.json`
  `spawn_points` and `objectives.json` `PLAYER_INIT` into a `SpawnPoint(Vector3, float HeadingDeg)`.
  `aiv`'s position + heading are the same units and convention, and `Zrdr.CandidateNames`
  (`Mech3/Zrdr.cs:22-28`) already resolves a logical `aiv.json` to the on-disk `aiv.zrd.json`. A
  `LoadAiv` sibling is roughly thirty lines and changes nothing else.
- **The sim clock is in place.** `Utils/GameClock.cs` gives fixed-dt stepping under `--det` with an
  established consumer contract (`SimStep(float dt)` plus a `PhysicsDt`-returns-zero guard). An AI
  controller slots into `PlaneViewer.DriveSimSteps` exactly like `FlightController` and
  `ProjectilePool` already do.
- **N aircraft in one world is normal.** Splitscreen already builds up to four `FlightController`s in
  a shared `World3D` (`PlaneViewer.cs:1324-1343`), every aircraft renders in every pane, and there is
  **no mutable static state in `src/Flight/`** — no `Instance`, no `Current`, only factory methods and
  pure helpers.
- **Voice playback has most of its plumbing.** `Mech3/WorldSounds.cs:185` —
  `PlayOneShot(string name, Vector3 worldPos, Random rng)` — creates a positioned 3D one-shot with
  correct attenuation and sweeps it when done. `SoundArchive` opens `extracted/soundsh/` today
  (`PlaneViewer.cs:426`, unzipped-preferred at `:612`), so **every combat clip is already reachable**.
  `Mech3/Messages.cs` resolves the `MSG_*_NAME` keys the rosters carry.
- **`AnimRuntime` + `DestructibleRegistry` are the right shape for zeppelin parts.** Motions write
  *local* transforms (`AnimRuntime.cs:2337`, `:3199`) and attached emitters, lights and sounds
  re-sample their host's global pose every frame (`:1215-1227`, `:1583`, `:1393-1405`), so a
  definition bound to a **moving** zeppelin animates correctly today — the existing compiled zeppelin
  definitions already do this.

### The hard prerequisite — state it plainly

**Nothing can shoot an aeroplane, because the flying aircraft has no physics body.**

- `FlightController` is a `Node3D` (`Flight/FlightController.cs:37`); grepping it for every
  `CollisionObject3D` subclass returns **zero hits**.
- The aircraft model is built with `generateCollision` left at its `false` default
  (`Mech3/PlaneBuilder.cs:83` vs `Mech3/WorldBuilder.cs:279-282`), so `SceneBuilder.AttachCollision`
  never runs for a plane.
- `PlaneCollider.Part` (`Flight/PlaneCollider.cs:44`) does hold real `BoxShape3D` resources — but they
  are used **query-only**, as the *source* of `CastMotion` / `GetRestInfo` / `IntersectShape` calls in
  `FlightController` (`:1370-1376`, `:1390`, `:1318-1327`), for terrain crash sweeps and
  damage-part mapping. The plane participates in the physics world as a querier, never as a target.
- Consequently `ProjectilePool`'s raycast (`Flight/Projectile.cs:424-431`) can never strike an
  aircraft, and `ClassifySurface` (`:556-568`) can only ever return `Default`, `Water` or `Buildings`.
  **Three** of the six shipped `IMPACT` surface classes are unreachable — `Player`, `Enemy` **and**
  `Quicksand`.
- There is no collision-layer scheme to extend: a repo-wide grep for `CollisionLayer` / `CollisionMask`
  returns **zero assignments**. Everything sits on Godot's default layer 1 / mask 1.

**Until air-to-air hit detection exists, Attack mode is unobservable, turret fire cannot damage the
player, and no amount of pilot modelling can be verified.** This gates the milestone, and it is A1.

Two consequences worth knowing before starting it:

- Giving aircraft bodies **immediately** makes them visible to every *other* aircraft's `CastMotion`
  sweep, so plane-versus-plane collision appears as a side effect whether or not it is wanted. Design
  the layer scheme in the same change.
- `ProjectilePool._ray` is a **shared mutable query object**. Per-shot `Exclude` must be set *and
  reset* every round or one shooter's exclusion silently leaks into the next.

### Architecture constraints worth recording now, before the code that trips on them exists

- **`DestructibleRegistry` cannot express a multi-zone object.** It keys instances by
  `(AnimDefinition Def, ulong Anchor)` with a **single scalar `float Health`**
  (`Mech3/DestructibleRegistry.cs:64`, `:38-58`); `DamageStage` is an index into descending absolute
  thresholds **on that same single pool**, not a zone count. `Resolve` maps a struck node up to *one*
  instance and stops. A zeppelin needing N of M gasbags — and, per the design's general model, a
  battleship needing 2 of 3 hull sections — requires **`(def, anchor, zone)` plus a threshold counter
  and a parent aggregator**. **Cheaper to design now than to retrofit after M4's other items have
  added callers.** (`WeaponDef.DamagesZeppelin`, parsed at `Flight/WeaponDefs.cs:100` and dumped but
  never consumed, is the surviving evidence that the original had a separate zeppelin damage channel.)
- **Zeppelin sub-part definitions are deliberately un-anchorable today.** `AnimRuntime.Anchors`
  (`:3041-3059`) refuses multi-target `NAME1` definitions and its comment names exactly this case —
  zeppelin nacelles and turrets parse with an empty name and are treated as object-wiring scope. F18
  must change that rule deliberately, not by accident.
- **The world tree is immutable after bootstrap.** `AnimRuntime.FindAll` memoises on the stated
  contract (`:3129-3141`, repeated as a `⚠` in `docs/architecture.md`) that `_index` is built once and
  never added to, because the only runtime mutation is `SetSubtreeActive`. **Spawning an AI aircraft
  as a new node at runtime silently breaks animation resolution** unless the index is invalidated and
  rebuilt. This is the single biggest unknown in A2 and the reason A2 is in the first wave.
- **Effect templates snap to an absolute world point and do not track.** `PlaceTemplateAt`
  (`:1658-1668`) relocates one shared template, so a hit effect on a moving zeppelin stays where the
  hit happened while the zeppelin flies on, and two simultaneous gasbag hits collapse onto one site.
- **`AnimRuntime.PlayerPosition` and `ProjectilePool.Listener` are genuine player-one singletons**
  (`PlaneViewer.cs:829-834`, `:1272`). Every `PLAYER_RANGE`-gated world effect measures from player
  one's camera. With AI aircraft fighting across the map, range-gated effects will fire based on where
  the human is looking rather than where the action is. It is a `Func<Vector3>` → nearest-of-a-set
  change, and it is inherited free of charge the moment AI exists.
- **A one-shot sound does not follow its source.** `PlayOneShot` takes a `Vector3`, not a node — a
  voice line from a moving aircraft would be frozen at its firing coordinate. The host-following
  emitter path exists but is reserved for ambient `SOUND_NODE` emitters.
- **A clip that was not prewarmed silently never plays.** `WorldSounds.Loader` is valid only during
  world build (`PlaneViewer.cs:793`); every voice name must join the prewarm set.
- **The dialogue-chain sound groups are parsed and discarded.** `Mech3/SoundDefs.cs:129-131` records
  that voice-over chains nest a list where a member's own name would be, contribute no weighted member,
  and are skipped — leaving the group unregistered. `LoadGroups` needs a second entry shape before any
  comms chain is addressable by name.
- **There is no subtitle or voice-line display path at all.** The closest existing widget is
  `Flight/MarkerHud.cs:120-135`'s one-shot banner rendering in the HUD bitmap font; that is the
  template to copy.

---

## Ground rules

Carried from every prior plan here; they apply unchanged.

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often — and this
  milestone has more room for invention than any before it.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here.** Every
  item's Evidence line carries its confidence.
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
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

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **The milestone is still not
scheduled** — the ticked items are ones other work reached first (A1 via PLAN-vs-mode, D10 and half
of A3 via the 2026-08-10 decompile pass), not a started milestone.

### Wave A — Foundations (nothing downstream is verifiable without these)

1. ☑ A1 — Air-to-air hittability: collision layers, aircraft bodies, projectile owner-exclusion
   — **front-loaded by PLAN-vs-mode (landed 2026-08-06)**: `CollisionLayers`, `AircraftBody` on
   the shared `PlaneCollider` boxes, per-shot owner exclusion, part-mapped damage, attributed
   kills, aircraft-only proximity fuse + blast falloff, all pinned by the `air-to-air` suite
2. ☐ A2 — The AI actor seam: runtime spawn, a non-player `FlightModel` driver, `GameClock` wiring
3. ◐ A3 — the AI format pages — **rosters, skills and maneuvers landed 2026-08-10**
   ([`formats/ai-rosters.md`](formats/ai-rosters.md)); nets already had
   [`formats/ai-nets.md`](formats/ai-nets.md) and zeppelins/generators
   [`formats/mission-entities.md`](formats/mission-entities.md). **Remaining: `ai.zrd` turrets
   and `--dump-ai`**
4. ☐ A4 — **Decision + design note only:** multi-zone destructibles and the kill threshold

### Wave B — Non-combat presence

5. ☐ B5 — Net following: the `ne`/`neindex` graph as a patrol behaviour
6. ☐ B6 — Generators: `egen` waves, capacity and periods
7. ☐ B7 — Formation flying — ⚠ **premise refuted 2026-08-10: there is no leader field** (slot 6 is
   `primary_target`). Re-scope onto `group` (slot 4) or drop
8. ☐ B8 — The voice runtime: prewarm, source-following one-shots, the `aiv`→voice→clip chain

### Wave C — Emplacements

9. ☐ C9 — Turret and AA AI from `ai.zrd.json`, both structural families

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

16. ☐ E16 — Trigger dispatch across the shipped taxonomy, gated by the talker stat

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

## Dependency and parallelism notes

**A1 and A2 are the only true blockers and they are independent of each other** — A1 is physics
layers plus bodies, A2 is spawn plus a driver seam. Run them concurrently in separate worktrees;
they contend on nothing (A1 owns `PlaneCollider.cs` / `Projectile.cs` / `SceneBuilder.cs`'s collision
path, A2 owns `PlaneViewer.cs` / a new controller / `AnimRuntime.cs`'s index invalidation). **A3 and
A4 are documentation and design, touch no engine code, and can run alongside anything.**

B5–B8 all need A2. B5 blocks F17 (the same net-follower serves both). B8 blocks E16. C9 needs A1 only
— it can run as soon as A1 lands, in parallel with all of wave B.

Within D: **D10 is settled, so it no longer blocks D11–D15** — they read the shipped skill vector and
`ai_skill_parameters` directly and can all start at once. D11 blocks D12 and D15. D13 is independent
of D12 and can run alongside it. D14 needs A1 but not D11.

Within F: A4's decision blocks F18. F17 needs B5. F19 needs F17 (the arc is relative to a moving
hull) and F18 (destroyed cannons weaken the volley). F20 needs B6 and F17.

**File contention to watch.** D11–D15 all reach into whatever A2 creates as the AI controller — give
each concurrent agent a named region. F18 and A4 both concern `DestructibleRegistry.cs`; A4 lands
first as a note, F18 implements it. E16 and B8 both touch the voice runtime. C9 and D14 will both want
the lead-and-scatter aiming maths — factor it once, in whichever lands first, and say so.
