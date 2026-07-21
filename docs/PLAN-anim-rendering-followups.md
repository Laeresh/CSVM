# Animation/rendering follow-ups (post-billboard-fix backlog)

Four independent, separately-completable items found while chasing the user's C1/IA1
rendering reports on 2026-07-21 (waterfall mist, billboard axes, signal-bridge flares — all
landed the same day; see `docs/HISTORY.md`). Each item below is scoped to fit in its own
session: read its Goal/Evidence/Approach/Verify, then go. Statuses: ☐ open · ◐ in progress ·
☑ done — keep the checklist in sync as items land, per this repo's standing rule.

Ground rules carried over from every prior plan in this repo: original-game data drives
everything (read the reader/compiled JSON before writing a handler, don't guess a value);
`CLAUDE.md` + `docs/architecture.md`/`docs/formats/` are updated in the **same turn** as each
landed item; a landed item gets a dated entry in `docs/HISTORY.md`; verify every change
against a full 8-chapter `--freecam --chapter=<X>` regression (zero errors, same mesh/node
counts unless the change is expected to add coverage) before calling it done, plus a targeted
screenshot at the specific location the report came from.

## Checklist

1. ☑ `If`/`Elseif` condition evaluation + `AnimationLod` quality setting **(done 2026-07-21)**
2. ◐ `LightState` + the remaining unacted-on event kinds **(point lights + material flipbooks + `CALL_ANIMATION` targets done 2026-07-21; `OBJECT_ADD_CHILD` withdrawn, burning-object fires postponed to `backlog.md` — see below)**
3. ☑ Mission-spawned entity rosters (`hk_zep`, CTF props) **(done 2026-07-22 — the premise was wrong; it is the interp boot script, not a roster)**
4. ☐ `texture_scroll` rendering **(premise updated 2026-07-22: `Object3DSetScroll` in the boot scripts is a second, authoritative source — see item 3)**

**Dependency note:** items 1 and 2 are the two halves of one visible payoff — `AnimRuntime`
currently *skips* every `If`/`Elseif` branch (item 1), and even once a branch runs, its
payload is very often a `LightState` event the runtime doesn't act on yet (item 2). Landing
item 1 alone will not change what the refinery or lighthouse look like — the branches will
finally execute, but their `LightState` events will still no-op. Either do both in one
session, or land item 1 first and confirm via `--debug-anim`-style logging that the branches
are now taken (not skipped) before moving on, so item 2's session isn't debugging two things
at once. Items 3 and 4 are fully independent of 1/2 and of each other.

---

## 1. `If`/`Elseif` condition evaluation + `AnimationLod` quality setting

**Goal:** `AnimRuntime`'s `SequenceRunner.Advance` currently treats every `If`/`Elseif` as
"skip the branch body" (`docs/formats/anim-definitions.md` records this as the leftover
assumption that conditions are unknowable gameplay state). Replace the skip with a real
evaluation for every condition kind the data uses, so branches that should run, run.
`AnimationLod` specifically becomes a project-level quality setting rather than a fixed
answer, per the user's request (2026-07-21): default it to whatever tier makes every
LOD-gated branch in this install pass (the hardware has no reason to hide detail the original
only hid for performance), with a CLI knob to lower it later if ever wanted.

**Evidence** (surveyed 2026-07-21 across the whole install, see `docs/formats/
anim-definitions.md`'s "`IF`/`ELSEIF` conditions are all evaluable" section for the full
table):

| Count | Condition | How to evaluate |
|---:|---|---|
| 4537 | `RandomWeight` (0..1) | A dice roll — `GD.Randf() < weight`. |
| 4007 | `AnimHealth` | Object health; full/undamaged in a fresh world build. |
| 1052 | `PlayerRange` | Distance from the player/camera; live scene state, recompute per branch check. |
| 717 | `NodeActive` | Whether a named node is currently active; our own scene state (`Visible`/subtree-active). |
| 473 | `NodeUndercover` | Node + distance — needs a line-of-sight/occlusion test; lowest-value, consider stubbing false first. |
| 124 | `AnimHealthRange` | `{min,max}` health window — same as `AnimHealth`, range test. |
| 120 | `AnimationLod` | **Our own setting** — see below. |
| 120 | `PlayerFirstPerson` | Our own camera mode; `false` until a cockpit view exists (backlog). |
| 28 | `NodeBelowAlt` | Node + altitude threshold; live scene state. |
| 17 | `HwRender` | Hardware rendering — always `true`. |

Two concrete payoffs once this lands (found chasing the user's refinery/lighthouse reports):
`refinery_fire_always` and `ref_light_always1..6` wrap their **entire** light sequence in
`If{AnimationLod:2}`; `litehouse_sparking` gates its spark bursts on `If{RandomWeight:0.7}`.

**Approach:**
- `SequenceRunner`'s `"If"`/`"Elseif"` case (currently `rt.Count(...); _pc = SkipBranch(_pc);`
  unconditionally) becomes: evaluate the condition via a new `AnimRuntime.EvaluateCondition
  (AnimData condition, Node3D? anchor)` — a `switch` on the condition's tag (the `Union()`
  helper `AnimData` already exposes elsewhere is the right shape: conditions arrive as a
  one-key object, e.g. `{"RandomWeight": 0.7}`). `true` → fall through to the branch body
  (increment `_pc` past the `If`/`Elseif` event and keep going); `false` → `SkipBranch` as
  today, but then re-check the next `Elseif`/`Else` instead of jumping straight to `Endif`
  (current `SkipBranch` behavior — read it before changing; the control-flow state machine
  may need an explicit "which branch is active" flag rather than pure skip-to-Endif).
- `AnimationLod`: add a settable `AnimRuntime.QualityLod` (int, default = the max value seen
  in a survey like the one above — confirm the actual max across all 8 chapters before
  hardcoding it, don't assume 2). `EvaluateCondition`'s `AnimationLod` case is
  `QualityLod >= requested`. Wire a `--anim-lod=N` CLI arg in `PlaneViewer.cs` (optional,
  defaults to max) purely so a screenshot can force a lower tier for A/B comparison — not a
  real settings-menu feature this pass.
- `RandomWeight` needs a stable RNG the runtime already owns (check if one exists; if not, a
  single `System.Random` field, not `GD.Randf()`, so `--debug-anim` runs are reproducible
  with a seed if that's ever wanted).
- `NodeActive`/`NodeBelowAlt`/`PlayerRange` need a "resolve this name in the current scene"
  path — `ResolveOne`/`ResolvePath` already exist for event targets; reuse them rather than
  inventing a second resolver.

**Verify:** `--debug-anim`-style log line when a branch's condition is evaluated (kind + value
+ result) for a session or two while landing this, then remove/gate it behind an env var like
the puffer diagnostics used this session (temporary, reverted before done). Confirm via that
log that `refinery_fire_always`'s `AnimationLod` branch is now taken. Full 8-chapter
regression (zero errors; op-count increases are expected and fine here, unlike the mission-
roster fix — document the new counts). `--debug-anim` pose log unaffected for the train/cars/
doors (this item touches only control flow, not the transform/roster fixes already landed).

**LANDED 2026-07-21.** All ten condition kinds evaluate (`AnimRuntime.EvaluateCondition`),
`--anim-lod=N` added (default 2 = the reader's `HIGH`, the only tier the data asks for).
The condition-evaluation log was kept rather than reverted, in a shape that does not spam:
per-kind true/false tallies after the bootstrap, plus `--debug-anim` lines on the first
evaluation and every verdict FLIP. Verification, full detail in `docs/HISTORY.md`:
`If(skipped branch)` disappears from the unhandled report in every chapter; C1 reports
`AnimationLod 33✓/0✗` (refinery + 6 docklights + 6 reflights + police, taken) and
`--anim-lod=0` flips it to `0✓/33✗` with `LightState` dropping 520→518 — the knob works both
ways; all 8 chapters build with **zero errors** and their before/after screenshots differ
only within a *measured* run-to-run noise floor (C1/C1B/C3 byte-identical both ways; C1C/C2B/C4
~5–6% from self-animating precipitation, same magnitude same-build-vs-same-build; C2/C5 a
handful of px in the identical bbox); the full mode battery (fly/stunt/viewer/damage/2P/4P
race/menu, plus a static C4 weather view) is error-free.

Three findings, all now in `docs/formats/anim-definitions.md`: compiled `PlayerRange` is
metres **squared** while the reader's is metres (270 ↔ 72900) and compiled `ANIMATION_LOD` is
`2` while the reader's is the token `HIGH`, both converted once in `AnimDefs.ReaderCondition`;
condition node references are **1-based indices into the def's own `nodes` array** (not gamez
indices, not names) with -100/-200 sentinels meaning "the anchor"; and evaluating conditions
is what first made the data's poll idiom live (`If <cond> → CallAnimation; Endif; Loop{-1}`),
which forced a second semantic — **`CALL_ANIMATION` must not restart a running animation**,
or C1/MP1's rearm-bay door stays pinned at frame 0 for as long as the player hovers within
25 m. That was verified by parking the free camera on the pad and watching the condition flip
`false → TRUE → false` while `rabdr` cycled 163 → 177 → 167 → 154 m.

**Follow-up left open:** `NodeUndercover` (the reader's `NODE_NEAR_GROUND`) is stubbed false —
it needs a ground/occlusion probe `AnimRuntime` has no access to. All 473 uses sit in
`ON_CALL` definitions the bootstrap never reaches, so nothing is currently affected.

## 2. `LightState` + the remaining unacted-on event kinds

**Goal:** Act on the event kinds `AnimRuntime.Dispatch` currently only counts
(`Count(ev.Kind)` in the `default:` case) and drops. `LightState` is the highest-value one —
it's the payload behind the refinery/lighthouse branches item 1 unlocks, and 518 events in C1
alone. The rest (`Sound`/`SoundNode`, `ObjectOpacityState`/`ObjectOpacityFromTo`,
`ObjectCycleTexture`, `ObjectAddChild`, `FbfxColorFromTo`, `CameraState`, `ObjectMotion`) are
independent, much smaller additions — pick off however many fit a session.

**Evidence:** the dispatch table's `default:` comment already states the intent ("Adding a
handler is a case above and nothing else") — this item is exactly that, event kind by event
kind. Payload shapes are in the compiled JSON dumps already taken this session (e.g.
`refinery-refinery_fire_always.json`'s `LightState` events carry `name`, `type_`
(`PointSource`), `translate` (`AtNode` + local offset), `range: {min,max}`, `directional`,
`saturated`, etc. — dump a few more `LightState` events across chapters before committing to
a field list, the refinery sample alone may not cover every variant). `ObjectCycleTexture`'s
one known use is the cockpit damage-indicator hilite cycle (`docs/formats/gamez.md`'s
`damageindicator` bullet) — already implemented as a *build-time* material swap in
`GaugeCluster.cs`; check whether the anim-driven case is the same mechanism or a distinct one
before assuming it's redundant.

**Approach:** one `case` per kind in `AnimRuntime.Dispatch`, same pattern as the existing
`ObjectActiveState`/`ObjectRotateState` handlers — resolve the target node(s) via
`Targets(ev, def, anchor)`, apply the state. `LightState` likely wants an actual
`OmniLight3D`/`SpotLight3D` node (or, if performance matters at the density these fire — 518
in C1 — a cheaper approach reusing `SceneBuilder`'s existing point-sprite glow shader
(`GetLightPoints`/`LightShaderCode`) rather than real dynamic lights, since the world already
renders fullbright and doesn't consume dynamic lighting anywhere else). Decide which BEFORE
writing code — a real light per event at this count could be a measurable perf regression on
C4/C5; the sprite-glow route reuses proven, cheap infrastructure. `Sound`/`SoundNode` should
reuse `FlightAudio`/`SoundArchive`'s existing one-shot/loop machinery, not a new audio path.

**Verify:** per kind landed, a targeted screenshot/listen at a known location (refinery
lights, lighthouse sparking once item 1 + this land together; a `Sound` event's location for
audio — note audio can't be screenshot-verified, describe what should be audible and let the
user confirm). Full 8-chapter regression after each kind. Update the "Playback ops seen and
deferred to part 2+" list in `docs/formats/anim-definitions.md` — remove each kind as it
lands, don't leave the doc claiming something is deferred once it isn't.

**PARTIALLY LANDED 2026-07-21 — `LIGHT_STATE` + `LIGHT_ANIMATION`.** Details in
`docs/HISTORY.md`; decode in `docs/formats/anim-definitions.md`.

The plan's tentative "sprite-glow route" was **wrong**, and the evidence for that is worth
keeping: the flare a player sees at a light's position is already gamez geometry
(`docklight_flare` = `Facade`/`SphericalY` on `dock_liteflare.tif`, `flame01` =
`Facade`/`CylindricalY` on `fire101.tif`), so a glow sprite would double-draw it. And a real
`OmniLight3D` is equally wrong, because the world renders `unshaded`. What a `PointSource`
supplies is the spill onto surrounding geometry, which is what landed. Also settled: the
per-fragment cost the plan worried about is a non-issue (0.27 ms viewport GPU with 16 lights);
the real cost was C# re-resolving `AT_NODE` ~1,740×/second through a full-world scan.

**Re-scoped by the user, 2026-07-21** — what they meant by "the lights animate" is not point
lighting but **animated light sprites**, and both mechanisms are still unimplemented:

- **Material texture cycles - DONE 2026-07-21** (`src/Mech3/TextureCycler.cs`): the gamez
  material `cycle` block now plays, animating C1B's sea (695 polys of `wtr00000`, 375 of
  `srf0001`), boat wakes, turbulence, splashes and the walking crowd. `ObjectCycleTexture`
  remains unimplemented, so cycles run free rather than being started/reset by animation.
- **`EFFECTS` reader** (`extracted/zrdr/effects.zrd.json`) — named texture flipbooks bound to
  nodes: `["fire1.flt", NAME "fire1", SPEED 10.0, LOOPING ON, MAPS [fire101.tif … fire112.tif]]`
  and `fire2` (6 maps @ 5 fps). Compiled anim defs carry an `effects` support array that
  references these. The project has **no code and no docs page** for the mechanism (the
  *textures* are already described in `docs/formats/effects.md` as puffer flipbooks — it is the
  node-bound EFFECTS animation that is missing). This is the flame flicker the user described.
- **Material texture cycles** — gamez materials carry a `cycle` field
  (`texture_indices`/`speed`/`looping`); 3 in C1 (`splash01`→3 frames, two walking-man sprites),
  5 in C1B (water, surf, wake fronts, turbulence). `ObjectCycleTexture{name, reset}` (144
  events, no texture list of its own) is what triggers them. Also unimplemented.
- **Lighthouse "circling light" — FIXED 2026-07-21.** It is not an animation: a billboard
  rotating about its axis with an offset from the tower centre, so it stays visible from every
  direction. `SceneBuilder` was recentering cylindrical facades on their quad centroid, which
  destroyed exactly that offset. Now only camera-*facing* sprites are recentered. See
  `docs/HISTORY.md`; the same fix restores the `fireflare1` lamps and `nosegun1` muzzle flashes.

Remaining kinds after this pass (C1 bootstrap counts): `ObjectMotion`×73,
`ObjectOpacityState`×58, `SoundNode`×38, `ObjectAddChild`×38, `Callback`×8,
`ObjectCycleTexture`×1.

**`EFFECTS` is node-keyed, not texture-keyed — settled 2026-07-21 (user observation).** This
decides the design and was worth the check: `flame01` (the refinery gas flare, node 2998 under
`vent1` → `refinery.flt`) and `mb_spinflame` (the 3-poly muzzle burst) both render **material
88 = `fire101.tif`**, which is frame 1 of the `fire1` flipbook. If EFFECTS animated by
texture/material, both would flicker for free and the refinery would cost us nothing. The user
confirmed a **sustained** muzzle flash never changes texture — always `fire101`, rotating and
flashing but no frame advance — which rules that out (a brief flash would have been weak
evidence; a held one is not). So the flipbook binds specifically to the `fire1`/`fire2` template
nodes, `flame01` is a separate static base flame, and the animated fire the user sees at the
refinery is a **placed `fire2` instance** — 6 frames @ 5 fps, matching their independent
"looks like only 6 states" read.

### ~~Next up: `OBJECT_ADD_CHILD`~~ — WITHDRAWN 2026-07-21, both premises were false

`OBJECT_ADD_CHILD` was scoped here as the next piece of item 2 "because it unblocks a whole
family (the `EFFECTS` fire flipbooks and burning objects generally)". A survey of all 1,152
`ObjectAddChild` + 192 `ObjectDeleteChild` events before writing any code disproved that, and
disproved the supporting claim too. **Do not implement it as a means to the fire flipbooks.**

| Claim made here | What the data says |
|---|---|
| "unblocks the `EFFECTS` fire flipbooks" | `fire1.flt`/`fire2.flt` are **never** an `ObjectAddChild` child — 0 of 1,152. Nothing reparents them. The linkage does not exist. |
| "293 of the defs using it are `OnStartup` against 822 `OnCall`" | **Zero** `OnStartup` defs contain `ObjectAddChild`, install-wide. It is 838 `ByRange{0,90000}`, 240 `OnCall`, 9 `ByRange{0,40000}`. |
| "the `apassengers` defs add passengers to a `pass_st` station node" | `pass_st` is **not a gamez node in any chapter** — the parent cannot resolve. Those 9 defs are `OnCall` and the bootstrap never reaches them. |

What the 1,152 events actually are: **865 (75%) attach sound *definitions*, not nodes**
(`snd_zepengine`→`spin` alone is 849; `snd_zepengine`/`snd_police`/`snd_waterfall` are entries
in `sounds.zrd.json`, not `nodes.json`), so they are inert until `Sound`/`SoundNode` lands;
~148 are cutscene machinery (`camera1`/`player`/`cpilot`) for cutscenes this project does not
have; the rest are mission-cutscene entities in M02/M04/M05/MP3, plus 20 CTF flag lights that
belong to item 3. **Implementing it today is a provable no-op.** The real dependency runs the
other way: `ObjectAddChild` is mostly the positioning layer for sound emitters, so it should
follow `Sound`, not precede it.

Both of its "decode questions to settle first" are answered as a side effect. **Clone, not
move**: `snd_waterfall` goes to `waterfall01/02/03` and `snd_police` to four cars concurrently
(15 of 40 distinct children have multiple parents). **The template-pool question is moot** for
the fire nodes, since nothing targets them.

### Next up instead: template instancing — `CALL_ANIMATION` targets **(the runtime half LANDED 2026-07-21)**

Chasing the above surfaced the actual mechanism, and a real bug. See the item-2 re-scope
above for the fire findings; the short version is that effect templates are placed by
`CALL_ANIMATION`'s target parameter, not by reparenting.

**Landed:** `CallAnimation` was dropping its target and running every callee on the *caller's*
anchor. Fixed in `AnimRuntime.CallTargetAnchor` + an `AnimDefs` normalizer case — see
`docs/HISTORY.md` and the `anim-definitions.md` "CALL_ANIMATION carries a target node" section.

**Postponed 2026-07-21 (user decision): the remaining fire work is a minor detail and its
trigger is unrecoverable** — the user searched the disassembly and found no xref either. Moved
to `backlog.md` under "Blocked / deferred" with the full decode, so nothing needs re-deriving
if it is ever picked up. What was left, in dependency order:

1. **Build the effect-template pool.** `fire1`/`fire2` (and `large_firetrail`, `short_firetrail`,
   `lg_fireball`, … — the parentless roots at gamez indices ~74–150) are real geometry that
   `WorldBuilder` never builds, because it builds only World children plus partition-referenced
   subtrees. Until they exist in the scene, a retargeted call to a fire animation has nothing to
   pose. Decide where the pool lives and whether instances are per-site copies.
2. **The `EFFECTS` flipbook.** `effects.zrd.json` binds `fire1` → 12 maps @ 10 fps and `fire2` →
   6 maps @ 5 fps. `TextureCycler` already plays frame lists, so this is small **once the
   templates are built**. Note the maps resolve **by filename from the texture archive**, not
   through the gamez texture table: `textures.json` registers only `fire101`/`fire102` in every
   chapter, while `extracted/<ch>/texture/` holds all twelve `fire1NN.png`.
3. **The trigger is not in the data.** All four `fire.zrd.json` animations (`timed_big_fire`,
   `persistent_big_fire`, `persistent_small_fire`, `timed_small_fire`) appear in **exactly one
   file — their own**. Nothing in any compiled archive or reader calls them, and
   `CALL_ANIMATION` references animations by name string only (no index form exists anywhere).
   So the original invokes them engine-side. Reproducing a persistent fire means choosing our
   own trigger; **user is checking the disassembly** for xrefs to those strings.

## 3. Mission-spawned entity rosters (`hk_zep`, CTF props)

**Goal:** `hk_zep` (the Hollywood Knights zeppelin) sits on the C1/IA1 field when it should be
absent — confirmed against the user's M04 reference screenshot, where it correctly *is*
present. C1's CTF props (`ctf_1`/`ctf_2` gate posts, `cs_flag_1`/`cs_flag_2` flags) need the
same treatment: visible only in Capture the Flag, invisible everywhere else (user-confirmed
against the original), and flag sprites carry no collider in the original.

**Evidence** (this session, `docs/formats/anim-definitions.md`'s "Mission-spawned entities
(open)" section): scenery visibility is governed by compiled `zepstate` (landed — see the
`AnimProgram.cs` mission-scope-library fix). *Entities* are the opposite: absent unless a
roster spawns them.
- `aiv.zrd.json` (AI vehicles) — C1/IA1 lists only the player; C1/M02 lists `hk_zep`; C1/M04
  lists `hk_zep` + `piratezep`.
- `zeppelins.zrd.json` (flyable zeppelins, with position/yaw/engines/cannons/gasbags) —
  C1/IA1: `multiplayer1zep`. C1/M04: `piratezep`. MP1/MP2: none. MP3: two.
- CTF: `ctf_1`/`ctf_2`/`cs_flag_1`/`cs_flag_2` are referenced only by `C1/MP2/zrdr/
  targets.zrd.json` — the CTF mission's own objective list, which is presumably the roster
  signal for this family (worth confirming there isn't a more specific CTF roster file before
  committing to `targets.zrd.json` as the mechanism — it may just be the *coincidental* only
  reference, not the actual spawn gate; the mission-type check might belong on `net.zrd.json`
  or a scenario-name check instead).

**Approach:** do NOT hide-by-name-pattern (`dliner1` is also zeppelin-shaped and must stay
visible — it's already correctly handled by the zepstate fix, a different mechanism from this
one). Build the entity visibility set from the rosters themselves: parse `aiv.zrd.json`/
`zeppelins.zrd.json` (new small readers, or extend `AnimDefs.cs`/a sibling file — check
whether `Zrdr.LoadFile`'s existing alternating-list walker already covers this shape before
writing a new parser) into a name set, then in `WorldBuilder`/`AnimRuntime`'s bootstrap, any
node matching a *known entity-family name pattern* (start with `hk_zep`, `piratezep`,
`multiplayer1zep`, `multiplayer2zep`, `cargozep1` — the names already surveyed as
zeppelin-roster entries) is active only if it's in that mission's roster set. For CTF: first
determine the actual roster mechanism (see the open question above) before writing the hide
rule — don't guess `targets.zrd.json` is authoritative without checking whether a mission-type
signal exists elsewhere (e.g. does `net.zrd.json` or the scenario name distinguish CTF from
other MP modes cleanly?). Flag collider removal is a one-line addition once the flag entity is
built — mirror `WorldBuilder.IsFlareSpriteNode`'s collision-exemption pattern (a predicate
`NoCollisionNode` already OR's in), not a new mechanism.

**Verify:** C1/IA1 field is empty where `hk_zep` used to sit (screenshot at
`--campos=-5466.595,284.452,-5136.92 --lookat=-5376.862,244.637,-5155.967`, the coordinate
that first surfaced this); C1/M04 still shows `hk_zep` on the field (regression — this is the
mission that SHOULD show it); MP3 shows both `multiplayer1zep` and `multiplayer2zep`; MP1/MP2
show neither. CTF props hidden outside MP2 (or whatever the confirmed real gate turns out to
be), visible inside it, with flags non-collidable. Full 8-chapter regression.

**LANDED 2026-07-22 — but the roster premise above is FALSE.** Checking it before writing
code (the standing rule that already killed `OBJECT_ADD_CHILD`) disproved both candidates:
`aiv.zrd.json` is the AI vehicle table whose *only* mention of `hk_zep` anywhere is inside a
wingman's target-priority list in C1/M02, and `zeppelins.zrd.json` is the flyable-zeppelin
gameplay config — it names `multiplayer1zep` for C1/IA1 (a node that mission actually hides)
and never names `hk_zep` in C1/M04, the one mission that shows it. Neither could gate
anything. The "entities are absent unless spawned" polarity was wrong too: C1/M02's
`zepstate` explicitly *hides* `hk_zep`, which a default-absent entity would never need.

**The real mechanism is the per-mission interp boot script**, `support\<chapter>\<mission>.gw`
in `extracted/interp.json` — 53 of them, ~1,215 statements, and C1/IA1's contains
`FindNode hk_zep` / `NodeSetActive off` outright. C1/M04's does not. Every mission script
except `mp2.gw` switches off `ctf_1`/`ctf_2`/`cs_flag_1`/`cs_flag_2`, which is the CTF gate
the user described, stated by the data rather than inferred. New module
`src/Mech3/MissionSetup.cs` + `AnimRuntime` bootstrap pass 0; decode in
`docs/formats/interp.md`, which the format README now indexes and
`anim-definitions.md`'s corrected section points to.

**Verified:** the reported camera shows the Blake Aviation zeppelin gone from C1/IA1 leaving
the bare tether tower, while the same camera in C1/M04 still shows it; the CTF flag renders
in C1/MP2 and the whole gate structure is absent in C1/IA1; all 8 chapters build with **zero
errors** and deactivate exactly the counts the data survey predicted (29/11/2/19/4/41+1/28+1/15);
the static plane viewer is **byte-identical** (md5); the full mode battery (fly/stunt/viewer/
damage/2P/4P race/menu) is clean. Scale: 3.5k–15k polygons leave each chapter's Instant
Action, and *every* chapter had both `multiplayer1zep` and `multiplayer2zep` parked in it.

**Deliberately not implemented, counted and reported instead:** `Object3DTranslate`×14,
`Object3DRotate`×11, `WorldPartitionSetActive`×25, `Object3DSetScroll`×68. No mission this
project defaults to uses translate/rotate, and `Object3DRotate`'s angle unit is genuinely
ambiguous (nine integer uses read as degrees, two high-precision ones as radians) — worth
settling before acting on it. `WorldPartitionSetActive` is C3-only and every `off` is in a
story mission, so IA1 is unaffected in all 8 chapters.

**Feeds item 4:** `Object3DSetScroll on 0.0 -0.4` on `wf01_water`/`wf01_edge` is in C1's own
`ia1.gw` — so the C1 waterfall *does* scroll, at −0.4 v/s, despite its gamez `texture_scroll`
being `{0,0}`. Item 4's evidence section below says the opposite; the boot script is a second
and apparently authoritative source that any scroll work must read.

## 4. `texture_scroll` rendering

**Goal:** Wire the parsed-but-unused `GameZMesh.TextureScroll` (u/v units/second) into the
renderer so the 5 models in this install that use it actually scroll: a hangar glass-roof sky
reflection (`h_zone1scroll`, `sky2.tif`), an oil-dock texture (`con_scroll`), and three boat
wake fronts (`wakefront1.tif`, C1B). Low priority — nothing currently reported needs it (the
waterfall, which is what surfaced this field, turned out to scroll at `{0,0}`; its motion is
entirely the splash-puffer mist, already landed).

⚠ **Re-scoped 2026-07-22 by item 3's findings — the gamez field is not the only source, and
the waterfall claim above is wrong.** The interp boot scripts set scroll rates at load time
via `Object3DSetScroll on <u> <v>`: 68 uses in the 53 mission scripts plus 7 in the chapter
`tex_fx.gw` scripts. C1's own `ia1.gw` ends with `FindNode wf01_water` /
`Object3DSetScroll on 0.0 -0.4` and the same for `wf01_edge`, so the C1 waterfall **does**
scroll at −0.4 v/s — the `{0,0}` in its gamez material is simply not where the answer lives.
`MissionSetup` already parses and counts these statements (reported as "not acted on"), so
this item now has two jobs: the gamez field *and* the script verb, which likely share one
renderer path. Do the collision check below for both sources. See `docs/formats/interp.md`.

**Evidence:** `docs/formats/gamez.md`'s `texture_scroll` bullet has the full field survey.
The blocker isn't parsing (done) — it's that `SceneBuilder`'s material cache is keyed by
`(Material, Priority, Rank, DoubleSided)` only. `sky2.tif` (used by `h_zone1scroll`) is almost
certainly also used by ordinary non-scrolling sky geometry elsewhere in the same chapter — two
meshes sharing a `materialIndex` but wanting different scroll rates would silently share
whichever `ShaderMaterial` instance got cached first. Confirm this collision actually occurs
(grep how many *other* nodes reference the same `materialIndex` as each of the 5 scrolling
models) before designing around it — it may turn out none of the 5 collide in practice, which
would simplify the fix considerably.

**Approach (pending the collision check above):** if collisions exist, extend the material
cache key to include a scroll-rate bucket (or, simpler, key scrolling materials by
`(materialIndex, scrollU, scrollV)` and only fall into that keyspace when `TextureScroll !=
Vector2.Zero`, leaving the common non-scrolling path's cache key untouched). Shader change:
`GetBiasShader`'s fragment `UV` becomes `UV + scroll_rate * TIME` (a new `uniform vec2
scroll_rate` per material instance, not a new shader-code variant — `TIME` is a Godot spatial
shader built-in, no extra plumbing needed). Verify `repeat_enable` is already set on the
textured sampler (it is, per the existing anisotropic-filtering uniform line) so the scroll
wraps instead of clamping at the UV edge.

**Verify:** screenshot the hangar glass roof and the C1B oil dock over a `--shots=N`-style
multi-frame burst (like the z-fighting debug burst, repurposed here to show motion across
frames) — confirm the reflection/texture visibly shifts frame to frame at the expected rate.
Confirm NO other scrolling regression: every other textured surface in a full 8-chapter
regression must render motionless across the same multi-frame burst (a UV scroll leaking onto
a shared non-scrolling material would show up exactly this way).
