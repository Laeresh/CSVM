# Ordnance types — implement the decoded ordnance runtime

**ACTIVE PLAN** (written 2026-08-16). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan implements `BL-406`: the twelve ordnance types the install ships all fly as the same
generic projectile today, and the original's behaviour for every one of them is decoded in
[`org/ordnanceTypes.md`](org/ordnanceTypes.md). The plan consumes that decode. It covers projectile
motion, guidance and seeking, the blast, the four disabling weapons and their **different** effects
on a human and on an AI, the shootable flyout, and the two firing gates. Splitscreen is a first-class
requirement rather than an afterthought, because the original routes its screen wash through a single
global and that does not survive four viewers.

Out of scope, each for a stated reason and each with somewhere to go:

- **The ten keys the original parses that no shipped weapon authors.** Their **defaults** are part of
  shipped behaviour and are implemented as such; the behaviours themselves are not. See
  [Why ten keys are unauthored](#why-ten-keys-are-unauthored) — they are very likely another game's
  weapons.
- **`TORPEDO`** (`BL-411`). Its only decoded behaviour is selecting one of three force-feedback
  effects, and this project has no force feedback at all.
- **`CRATER`** (`BL-412` decode, `BL-413` implementation). Six weapons author it and it drives a
  terrain-deformation subsystem, which is a renderer feature triggered by ordnance rather than
  ordnance behaviour.
- **The Weapon Loadout screen** (`BL-353`), which is what would let a *player* fit most of these
  types in a real flight. Not needed to build or verify this plan: shipped AI carry the disabling
  weapons and fire them at the player, and `--weapon-lab=` arms any type directly.
- **`wep_24`–`wep_28`**, the world and emplacement weapons, get no items of their own. They ride the
  same code paths this plan builds.

**Backlog provenance.** `BL-406` was rewritten from research to implementation in this session, so it
is verified open by construction. `BL-290`, `BL-227`'s falloff half, `BL-293`'s `SURFACE_ANIMATION`
half and `BL-408` were all re-read and amended in this session and are folded in as items. `BL-233`,
`BL-353`, `BL-404` and `BL-405` are referenced but not drawn in.

## Milestone goal

- Every ordnance type in `weapons.zrd.json` behaves as the original's routines specify, not as a
  generic projectile with a different damage number.
- The four weapons that deal no damage (`SONIC`, `FLASH`, `BEEPER`, `TANGLER`) and the smoke screen
  disable their victims instead, with the original's split between what a human sees and what an AI
  suffers.
- Blast damage falls off on the original's curve, respects cover, and measures to the shape rather
  than the origin.
- A torpedo can be shot out of the air; a seeker follows a beeper; a smoker stuns everything behind
  the aircraft that laid it.
- All of the above is correct with two, three and four viewers on one machine.

**No key that no shipped weapon authors gets a behaviour.** Ten of them exist. Implementing them
would put another game's content into this one, and the plan's own evidence could not tell a correct
implementation from an invented one.

## Decisions (2026-08-16)

| # | Question | Decision |
|---|---|---|
| 1 | Implement the ten unauthored keys? | **No, defaults only** — their defaults are shipped behaviour; their behaviours are content this game never had, and nothing could verify them. |
| 2 | Splitscreen: follow the original's single-player anchor, or diverge? | **Diverge, per-viewer** — the original holds one wash state for the whole machine (`DAT_0064ef9c` and its four neighbours, five references, no per-player index). Reproducing that would blind viewer 1 when viewer 3 is flashed. Fidelity loses to coherence. |
| 3 | Where does the per-viewer wash live? | **A second channel inside `ScreenFlash`** — it is already per-pane with an index-aligned `ViewerSet`. The existing proximity-routed *replace* ramp keeps serving HE/AP/flak; the new victim-routed *blend* channel serves sonic, flash and smoke, composed at paint time. One module keeps owning the pixel. |
| 4 | `TORPEDO`, whose only behaviour is force feedback? | **Out, `BL-411`** — we have no force feedback, and Godot's vibration API has no direction, so the original's 0°/180° split could not be reproduced even if we built it. |
| 5 | `CRATER`, on six ground-attack weapons? | **Out, `BL-412` + `BL-413`** — terrain mesh carving with its own subsystem (`zdec_crater.cpp`, tessellate/clip/build, `MAX_CRATER_RADIUS`). Undecoded, and it would outweigh the rest of this plan. |
| 6 | How does the AI stun attach? | **Mask `AiControlLaw`'s output**, exposing a stunned flag `AiModeMachine` can read. Matches the observable behaviour and keeps the aircraft on the flight model so it falls with momentum. Mirroring the original's own state id into our machine would be cargo-culting. |
| 7 | The `TANGLER` unit mismatch | **Reproduce it exactly** — the original mixes a squared distance against a raw radius, giving a ~4.6 m full-strength zone from an authored `RADIUS [35]`. It is what every player of the shipped game experienced. Recorded as observed, so nobody corrects it later. |
| 8 | `BL-408`, the player's ordnance launch axis | **Folded in as `A5`** — it is one call site inside the same spawn path Wave A rewrites, and splitting it means touching that code twice. |
| 9 | The original's 32-object splash cap | **Model it, and log when it bites** — it is a real behavioural limit, and a silent cap reads as "covered everything" when it did not. |

## ⚠ Read this before implementing anything

Three readings produced during this plan's own decode were wrong and were corrected. They are
tabulated because each is plausible enough to be re-derived by the next person.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `TORPEDO` is parsed and never read, like `FIRING_HEAT`. | Three instruction sweeps shared a blind spot: `TEST <memory>`, `AND <memory>` and `AND <register>` were run, `TEST <register>, 0x8` was not, and that is the form the compiler used. `FUN_00480f50` reads it to pick a force-feedback effect. |
| 2 | The splash falloff is linear to zero at the radius, confirming D10's invented curve. | `+0x40` is `IMPACT_PROXIMITY` **squared** and `FUN_00538880` returns a **squared** distance, so `1 − d/R` is really `1 − d²/R²`. Traced to the parse site at `0x005add73`. |
| 3 | Guidance runs for every round; the 0.001 sentinel is what makes a rocket dumbfire. | `FUN_005af720` gates the steering step on the weapon carrying `LOCK_ON` **and** the round holding a target. A round failing either never enters it. |

**The lesson that outlives all three:** a negative result about a flag is only as good as the
instruction forms behind it, and a stored field is not necessarily the authored quantity. Both
mistakes came from reading a value without tracing it to its parse site.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1–A5, B6–B9, C10–C12, D14–D18, E19–E21 | Confirm the trace against `org/ordnanceTypes.md`, then implement. |
| **Direction sound, magnitude a judgement call** | — | none; every magnitude in this plan is an authored constant or a decoded literal. |
| **Leads only — no mechanism yet** | D13, F22 | The wash channel's composition rule has no original to copy (Decision 3), and the playtest surface is not yet written. |

## What the data actually ships

- **Twelve ordnance types** in `wep_04`–`wep_15`, plus five world weapons in `wep_24`–`wep_28`.
  Full field census: [`formats/weapons.md`](formats/weapons.md).
- **Two independent flag words.** The extension struct at ZWEP `+0x210` (built by `FUN_004ba6f0`)
  and a second word at weapon `+0x74` (built by the `.zrd` dispatcher `FUN_005ad630`). They are
  unrelated bit spaces: `0x08` means `TORPEDO` in one and "has `FLYOUT_HEALTH`" in the other.
- **The engine stores radii squared.** `+0x3c` `IMPACT_PROXIMITY` raw, `+0x40` its square, `+0x44`
  `DETONATION_DISTANCE` squared, `+0x20` `RANGE` squared, `+0x48` `DETONATION_TIME` raw.
  `FUN_00538880` returns squared distances. This is the single highest-risk fact in the plan.
- **Four types deal no damage at all** (`SONIC`, `FLASH`, `BEEPER`, `TANGLER`). The hit branch
  discards whatever pair they author, and in this install all four author zero (`ARMOR_DAMAGE 0.0`
  / `HEALTH_DAMAGE 0.0` on `wep_08`, `wep_10`, `wep_12`; `DAMAGE 0` on `wep_09` and `wep_15`), so
  the discard changes nothing here and would only bite an entry authoring a real pair.
- **Shipped AI carry the disabling weapons.** A smoker and a flash at 6 rounds each on their own
  defs, the choker on `secfury`, and eight torpedoes on `bhatwarhawk` (`BL-394`). This plan's
  player-facing half is exercised in normal play, not only in the lab.
- **Game-wide tunables in `player.zrd.json`**, not per-weapon: `smokescreen_stun_range` 600 m,
  `smokescreen_stun_angle` 170° (stored as a half-angle cosine), `smokescreen_stun_interval` 5 s.
- **`ENGINE_DEAD` is a pair of globals**, not a weapon field, set by the last-parsed `TANGLER`.

### Why ten keys are unauthored

`PITCH_RATE`, `TURN_SUSPEND_TIME`, `TETHER_GUIDED`, `REMOTE_DETONATE`, `INSTANT`, `MINE`,
`RANDOM_DEVIATION`, `MULTI_TARGET`, `EXPIRES` and `IMPACT_TYPE` appear in no entry of this install's
`weapons.zrd.json`. Six have traced behaviour and four were only ever seen parsed; the breakdown is
on [`org/ordnanceTypes.md`](org/ordnanceTypes.md).

Read as a set, they describe mines, wire-guided rounds held under a ceiling, multi-target seeking and
hitscan weapons. That is a **MechWarrior** weapon roster, not an aerial-combat one, and this is the
MechWarrior 3 engine: the extractor these decodes lean on is `mech3ax`, and `CSVM/src/Mech3/` is
named for it. The most economical explanation is that these keys are the other game's features
carried in a shared codebase and never authored here. That is the strongest argument for Decision 1:
implementing them would not be restoring cut Crimson Skies content, it would be importing
MechWarrior's.

⚠ This is an inference from the key set plus the known engine lineage, not a decode. It is recorded
because it explains the shape of the data, and it should not be cited as evidence for anything.

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

### Wave A — Projectile foundations

1. ☑ Adopt the engine's squared-radius convention in `WeaponDef`
2. ☑ Launch-velocity inheritance and its decay over `LOCK_ON`
3. ☑ `ACCELERATION` toward the speed cap, and no drag
4. ☑ The three end conditions: range, timed fuse, target proximity
5. ☑ Launch the player's ordnance along the aircraft axis, not the pylon's

### Wave B — Guidance and seeking

6. ☑ The steering step: both gates, the turn clamp, the speed penalty
7. ☑ `LOCK_ON_LEAD`: blend from bearing to intercept
8. ☑ The beeper tag: world list, countdown, expiry tail
9. ☑ `BEEPER_SEEKER`: the per-frame retarget and its selection rule

### Wave C — The blast

10. ☑ Splash falloff: quadratic, measured to the shape
11. ☑ Splash occlusion test and the 32-object cap
12. ☑ The per-weapon impact hook and `SURFACE_ANIMATION` normal orientation

### Wave D — Disabling effects

13. ☑ `ScreenFlash`: the victim-routed blend channel
14. ☑ `SONIC`/`FLASH`: the shared intensity model
15. ☑ The player's screen wash: colour, weight, duration, blending
16. ☑ The AI stun
17. ☑ `TANGLER`: the engine-dead timer
18. ☑ `SMOKE_SCREEN`: the stun trap

### Wave E — Flyout and gates

19. ☐ `TARGETABLE`: admission to the target list
20. ☐ `FLYOUT_HEALTH`: the health pair, and destruction at zero
21. ☑ `DAMAGES_ZEPPELIN` two-way gate and the AI ordnance aim threshold

### Wave F — Sign-off

22. ☐ Four-viewer splitscreen pass and the per-type playtest

## Dependency and parallelism notes

`A1` blocks everything in Waves B and C: every radius comparison in those waves reads the convention
it establishes, and landing them first would mean writing each comparison twice. `A2` → `A3` → `A4`
is a chain, all three inside `Projectile`'s integrator. `A5` touches the same spawn path and must not
run beside them.

`B6` needs `A1` and `A4`. `B9` needs `B8` (the seeker queries the list the tag item builds) and `B6`
(it writes the target the steering step reads). `B7` is independent of `B8`/`B9` and can run beside
them.

`D13` blocks `D15` and `D18`; both route their player-facing effect through its channel. `D14` blocks
`D15` and `D16`, which share its intensity. `D16` is also called by `D18`, so `D18` needs `D16`
landed.

`E19` → `E20` is a chain. `E21` is independent of everything and can land any time after `A1`.

**File contention.** `A2`, `A3`, `A4`, `A5` and `B6` all edit `Projectile` and its spawn path: never
run them in parallel worktrees. `D15` and `D18` both edit what `D13` creates. `C10` and `C11` both
edit the splash path.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

---

# Wave A — Projectile foundations

## A1 ☑ Adopt the engine's squared-radius convention in `WeaponDef`

**Verdict.** Landed. `WeaponDef` carries `RangeSqM`, `DetonationDistanceSqM` and
`ImpactProximitySqM` beside the authored fields, squared once at parse; a test over every entry of
the retail `weapons.zrd.json` asserts each square against its authored value. The audit of every
existing consumer found no squared-vs-raw comparison to fix: each one either wants a real length (a
sphere-query radius, a path cap) or measures through a geometry helper that already returns a plain
distance, so nothing was converted and no square root was added.

**Goal.** Every radius comparison in the ordnance code compares like with like, and a reader of
`WeaponDef` cannot mistake a stored square for an authored radius.

**Evidence (confidence: traced).** `FUN_005ad630` stores `IMPACT_PROXIMITY` twice, raw at weapon
`+0x3c` and squared at `+0x40` (`FLD v; FLD ST0; FMUL ST1; FSTP` at `0x005add73`), and
`DETONATION_DISTANCE` squared at `+0x44` (same idiom at `0x005adbb5`). `RANGE` gets the same
treatment at `+0x1c`/`+0x20`. `FUN_00538880` returns a squared distance with no square root. See
[`org/ordnanceTypes.md`](org/ordnanceTypes.md), "The engine stores radii squared".

**Approach.** Expose both forms on `WeaponDef` with names that cannot be confused (`ImpactProximityM`
and `ImpactProximitySqM`, `DetonationDistanceSqM`), and make every consumer take the squared form
against a squared distance. Do not add a `sqrt` to "simplify": the point is to match the engine's
comparisons exactly, and a square root introduces a rounding difference at the boundary.

**Model recommendation.** medium. Mechanical, but the naming decision propagates through four later
waves, so it wants judgement about the interface rather than a quick patch.

**Verify.** A unit test per stored field asserting `Sq == raw * raw` for every entry in
`weapons.zrd.json`, plus `BallisticsTests` still green. This is the item where an unchanged number
proves nothing, so assert the relationship rather than a value.

**⚠ Traps.** This is where wrong-claim 2 came from. Do not "tidy" a comparison into distance space;
the original never takes a square root on these paths.

## A2 ☑ Launch-velocity inheritance and its decay over `LOCK_ON`

**Verdict.** Landed, and `BL-290` is closed. A round now carries two vectors: its own
`heading × speed` and the launcher's, the second copied in only for a weapon authoring `LOCK_ON`
and blended out linearly over that window. The gate came with it as a named predicate
(`ProjectilePool.SteeringStepRuns`, which B6 reuses), so a `LOCK_ON` round holding no target keeps
its launcher's speed. `Proj.Target` carries the reference; the pylon spawn fills it from a human's
own selection or an AI's gunner quarry, since nothing plumbed a target to a round before. The
original's synthetic target for a player firing with none selected is NOT reproduced, so a player
who has selected nothing sees no decay. Guns are held outside the rule deliberately: no `CANNON`
authors `LOCK_ON`, and both the gun aim assist and the impact reticle are built on the inheriting
round, so that is the gun path's question rather than this one's.

**Goal.** A round launched from a fast aircraft leaves fast and visibly settles to its own cruise
speed; one launched from a slow aircraft does not. Closes `BL-290`.

**Evidence (confidence: traced).** At spawn (`FUN_005aef40`), a weapon carrying `LOCK_ON` has the
launcher's velocity vector copied into the round at `+0x30`–`+0x38`; a weapon without it gets a zero
vector. The steering step then sets
`velocity = heading × speed + ((LOCK_ON − age) / LOCK_ON) × inherited` each frame. `wep_14` authors
`LOCK_ON [2.5]` and `VELOCITY [60]`, so an aircraft at 120 m/s launches a torpedo that halves its
speed across 2.5 s.

**Approach.** Carry the inherited vector on the projectile and blend it out in the integrator. ⚠ The
decay lives **inside** the steering step, so it inherits `B6`'s gate: a `LOCK_ON` round with no
target keeps its inherited velocity indefinitely. Implement the gate, not just the curve. In practice
the shot routine hands a `LOCK_ON` weapon a target when the player has none, so the usual case
decays.

**Model recommendation.** medium. One well-specified formula in a hot loop.

**Verify.** `--weapon-lab=wep_14` launched at high speed with the surface in frame: the round must
visibly slow over about 2.5 s and then hold. Then the same at low speed, where it must not slow.

**⚠ Traps.** `CAP-28` was owed for this question and is **no longer needed to answer it**; its
playtest row is retired with `BL-290`. Do not measure the decay off video to contest the constant.

## A3 ☑ `ACCELERATION` toward the speed cap, and no drag

**Verdict.** Landed, and the cap turned out to be the item's real content. `FUN_005aef40` seeds the
cap from `VELOCITY` and then, **only** for a weapon authoring `ACCELERATION`, adds the launcher's own
speed to both the cap and the round's starting speed (which is otherwise 1e-4): a motor round leaves
at the speed of the aircraft that fired it and climbs to `VELOCITY` **above** that, while a weapon
without a motor is seeded at its cap and so is never accelerated at all. The addresses are on the
decode page. `Ballistics.LaunchSpeed` is that pair, seeded beside `Proj.Vel`/`Proj.Cap` at spawn and
by `Ballistics.March`, and `ApplyForces` reproduces the original's gate, which can only raise a
speed: a round already past its cap is left alone rather than clamped down. No drag term existed in
our integrator, so none was removed; what landed instead is an assertion that none appears. `GRAVITY`
is implemented as the round's own m/s² rather than a scale on world gravity, correcting the
`formats/weapons.md` gloss and the `× WorldGravity` the pool applied; it is inert at 0.0 throughout
this install either way. The behavioural consequence owed at the controls is the flak: `wep_27` off a
standing emplacement now needs 5.7 s to reach its authored 850 m/s and expires at 900 m before it
gets there.

**Goal.** Rounds that author `ACCELERATION` speed up to their cap; nothing else changes a round's
speed except a turn.

**Evidence (confidence: traced).** `FUN_005afd50` raises speed by `ACCELERATION × dt` (weapon
`+0x38`), clamped to a per-round cap. **Nothing anywhere applies drag.** `GRAVITY` (weapon `+0x54`)
has a working reader, and every entry in this install authors it as 0.0.

**Approach.** Add the acceleration term and the cap. Implement the `GRAVITY` reader too even though
it is inert in this data: it costs one line and it stops the next person concluding the original has
no gravity term at all.

**Model recommendation.** medium, low effort. Mechanical once `A1` is in.

**Verify.** `--weapon-lab` on one of the four non-zero `ACCELERATION` carriers (`wep_04`, `wep_25`,
`wep_26`, `wep_27`), timing the round over a known baseline. Assert in a test that no code path
reduces speed outside the turn penalty.

**⚠ Traps.** Do not add drag "for realism". Its absence is a decoded fact and the reason rounds carry
so far.

## A4 ☑ The three end conditions: range, timed fuse, target proximity

**Verdict.** Landed, and reading `FUN_005afd50` to the end added a fourth fact the item did not
ask for: **reaching `RANGE` does not always detonate**. The expiry branch calls the detonation only
when `(LOCK_ON && !EXPIRES) || DETONATE_AT_RANGE`, and since this install authors neither of those
two keys, carrying `LOCK_ON` is the whole rule, so the choker, the cannonball and the fake weapon
now vanish at their range where our old `IsRocket` test detonated them. `DETONATE_AT_RANGE` was not
on the decode page at all, which also means the "ten unauthored keys" count is the set that page
enumerated rather than a census; that caveat is now recorded. The three conditions resolve in the
original's own order (range wins outright, then the target fuse, then the timed fuse as the
fall-through) and all three run **before** the swept step's ray, because the original ends a round
inside its motion step and moves and collides only what survives. They leave through one
`EndRound`, so the effect, sound and splash paths are shared. `ProximityFuseTriggered` is untouched
and stays aircraft-only. Three smaller corrections landed with it: `RANGE` defaults to **500 m**,
not our 1000, `DETONATION_TIME` defaults to −1.0 so an unauthored fuse is off rather than instant,
and `MINE` halves its whole accumulator every frame rather than accumulating half a step, so it
would never reach `RANGE` at all. `RANGE_MINIMUM`'s gloss needed no change in
[`formats/weapons.md`](formats/weapons.md) (`A1` had already corrected it); what it gained is the
second element the gate demands be zero. Owed at the controls: the four clips this item's Verify
names, all of which are asserted in the `ordnance-end-conditions` suite but none of which has been
looked at.

**Goal.** A round ends for one of exactly three reasons, and the right one.

**Evidence (confidence: traced).** `FUN_005afd50` ends a round on distance travelled reaching `RANGE`
(weapon `+0x1c`), on age exceeding `DETONATION_TIME` (weapon `+0x48`, the flare's 2.0 s), or on
coming within `DETONATION_DISTANCE` of **its own target**. That last is a second fuse path beside the
list sweep in `FUN_004b5fb0`. `RANGE_MINIMUM` is a **visibility** gate, not an arming one: a
`FLYOUT_HEALTH` round is hidden until it has travelled weapon `+0x24`, so the torpedo is invisible
for its first 300 m.

**Approach.** All three in the integrator, all three resolving through the same detonation entry so
the effect and splash paths are shared. Keep the existing `ProximityFuseTriggered` list sweep: the
two are complementary, the sweep catching any aircraft passed near and this one catching the round's
own target. Add the reveal gate here too, since it reads the same travelled-distance accumulator.

**Model recommendation.** medium.

**Verify.** One clip per condition: a rocket flown past `RANGE` over open water; `--rocket=wep_15`
detonating 2.0 s after release with nothing near it; a round fused by its own target while another
aircraft is nearer, proving the two paths are distinct; and `--rocket=wep_14` invisible for its first
300 m.

**⚠ Traps.** `BL-233`'s aircraft-only rule for the **sweep** is decoded and must not be widened to
world geometry. The per-round target check is a different path and is not bound by it.
[`formats/weapons.md`](formats/weapons.md) glosses `RANGE_MINIMUM` as "minimum arming range", which
the decode contradicts; that gloss is corrected as part of this item.

## A5 ☑ Launch the player's ordnance along the aircraft axis, not the pylon's

**Verdict.** Landed, and `BL-408` is closed, but the item's own premise did not survive the census:
**no shipped player airframe cants a pylon marker.** All eleven airframes' `pylonN` nodes come out
of planes.zbd square to the airframe, so the marker axis and the aircraft axis coincide on every fit
a player can fly and the change is a guard rather than a visible correction. The rule is
`FlightController.OrdnanceLaunchDir`, a named static both branches read: a human's round takes the
aircraft's basis axis, negated, or as-is for a `REAR` weapon, while an AI's takes the clamped mount
aim `AiRocketeer` wrote and an AI with no rocketeer keeps the mount's own axis, which is the AI
behaviour unchanged. The mount still gives the spawn position for both. Because the data cants
nothing, the `ordnance-launch-axis` suite cants the widest rig's markers 20° itself and asserts that
census first: the human salvo then flies parallel to the nose within 0.5° from each marker's own
position, while the same airframe flown by an AI fans the full 20°. Reverting the rule was run as a
negative control and the human salvo fans 20°.

**Goal.** A player's pylon salvo flies parallel to the nose. Closes `BL-408`.

**Evidence (confidence: traced).** `FUN_004b6820`'s player ordnance branch builds the direction it
hands the spawn from the **aircraft's own basis axis**, negated (taken as-is for a `REAR` weapon),
and reads the mount for the spawn **position** only. We pass the pylon marker's full transform
(`FlightController.cs:1860-1865`, `PylonOrdnance`).

**Approach.** Take the position from the pylon marker and the direction from the aircraft basis. One
call site, no new data.

**Model recommendation.** medium.

**Verify.** Fire a full pylon salvo straight and level on an airframe whose pylon markers are canted:
the rounds must fly parallel to the nose rather than fanning.

**⚠ Traps.** The **AI** is not wrong and must not be changed to match: its branch passes the mount's
clamped aim in world space, which is what the original does for an AI. The player and the AI
genuinely differ here (`BL-404`). This is not the aim-assist question either; no ordnance round of
any shooter is aim-assisted.

# Wave B — Guidance and seeking

## B6 ☑ The steering step: both gates, the turn clamp, the speed penalty

**Verdict.** Landed as `ProjectilePool.Steer`, run per round after the seeker's retarget and before
the motion step, in `FUN_005af720`'s own order. `SteeringStepRuns` (`LOCK_ON` AND a held target)
stays the one turn gate; inside it the desired direction is the bearing (or B7's blend),
`MaxTurnRad` is `TURN_RATE × dt × ramp` times the engine's per-shot turn scalar (`DAT_00a1e1b8`,
found while reading `FUN_005af960`: initialised to 1.0 and reset to 1.0 by every spawn, so 1 on
every steering frame; the ramp is kept as a term at 1), an angle over the clamp slerps by exactly
`clamp / angle` and renormalises, otherwise it snaps, and every steering frame with a non-zero
angle multiplies the own speed by `TurnPenaltyFactor`, `0.8 + 0.2·cos` of the angle actually swung
THAT frame. Two corrections to this item's own text off the decompile: the cosine is of the
per-frame swing, not of the angle to the target, so the loss over a whole turn scales with the
frame's authority rather than "up to 20% per frame" (at 60 fps the seeker's 1.25 rad/s costs about
0.3% over a 90° swing, asserted as the exact product in the `ordnance-guidance` suite); and the
decay's clock is a separate field the step advances (`+0x63c`), not the round's age. ⚠ **One
deliberate divergence, by direction:** the inherited-velocity decay is NOT hung on the target half
of the gate here. The original's shot routine always hands a `LOCK_ON` weapon a target (a synthetic
one when the player has none, undecoded and not reproduced), so its `LOCK_ON` rounds always decay;
ours may hold none, and gating the decay on it would fly a torpedo fired with nothing selected at
launcher speed plus 60 m/s for its whole range. So `InheritedFraction` decays every `LOCK_ON` round
and only the turn stays target-gated; recorded on `Projectile.cs` in `architecture.md`, and the
`launch-velocity-decay` suite's no-target assertion now reads 60 at 4 s. `IsGuided`'s doc comment
and the `formats/weapons.md` gloss are rewritten: a label for the lab, not the flight gate. The
suite also flies the dumbfire half: the torpedo with a target turns its 0.001 rad/s and no more
(0.23° in 4 s), so the gate is on the flag. Owed at the controls: `--weapon-lab=wep_11` at a painted
manoeuvring aircraft, and a dumbfire type at a selected one.

**Goal.** Only rounds the original steers are steered, they turn no faster than authored, and turning
costs speed.

**Evidence (confidence: traced).** `FUN_005af720` gates `FUN_005af960` on the weapon carrying
`LOCK_ON` **and** the round holding a target. Inside, `maxTurn = TURN_RATE × dt × ramp` in radians;
if the angle to the desired direction exceeds it the heading slerps by exactly `maxTurn / angle` and
is renormalised, otherwise it snaps. Speed is then multiplied by `0.8 + 0.2·cos(angle)` on every
steering frame. `TURN_SUSPEND_TIME` is unauthored, so `ramp` is 1 from the first frame.

**Approach.** Implement the gate first and assert it: this is wrong-claim 3 and the most likely thing
to be got wrong. `IsGuided`'s doc comment currently says it "describes the data rather than driving
flight" and must be rewritten as part of this item, not left contradicting the code.

**Model recommendation.** high. The gate interacts with `A2`'s decay and the speed penalty compounds
per frame, so a small error is a large behavioural drift.

**Verify.** `--weapon-lab=wep_11` (the sole real `TURN_RATE`) against a manoeuvring target: it must
turn, and must lose speed while turning hard. Then a dumbfire type with a target, which must fly
effectively straight because its sentinel is 0.001, proving the gate is on the flag and not on the
rate.

**⚠ Traps.** The speed penalty applies **per steering frame**, not once per turn. At 60 fps a
sustained hard turn bleeds fast; that is the original's behaviour and not a bug to damp.

## B7 ☑ `LOCK_ON_LEAD`: blend from bearing to intercept

**Verdict.** Landed as `ProjectilePool.LeadDesired`, reusing **`AimAssist.TryIntercept`** (the
constant-velocity solver `AiGunner` and `AiRocketeer` already run) rather than a second solver: from
element 0 of age, on a target with a velocity, the solve on the round's OWN speed and the target's
velocity replaces the bearing, slerped in from the bearing at element 0 to the full solve at element
1, and a solve with no answer leaves the bearing. The parse was checked: `+0x84` is
`1 / (element1 − element0)` (`0x005ade1a`–`0x005ade52`), written only when the two differ, after
element 0 is clamped up to at most element 1; the original's `FUN_0053e56d` is the same intercept
in the same `u = 1/t` form. Two things this item's own text did not say: the lead block also
requires the round's second target field (the target's velocity pointer, `+0x74`) to be set, which
`TargetVelocity` answers for an aircraft or an emplacement and nothing else; and **no shipped entry
reaches it**. The three carriers (`wep_04`, `wep_25`, `wep_27`) all pair it with the 0.001
sentinel and `RANGE 900`, and each expires before its 4 s / 5 s onset even off a standing launcher
(`wep_04` at about 3.46 s), which the suite asserts. The blend is therefore flown on a lab def
cloned from `wep_11` against a real crossing rig: the heading is the plain bearing below element 0,
eases monotonically to the intercept between the elements with no step at either end, and is the
full solve from element 1. This item's Verify names `wep_11`, which authors no `LOCK_ON_LEAD`, so
that clip cannot be flown as written; nothing at the controls can show this item on shipped data.
⚠ Reported, not fixed (the AI files are not this item's): `AiRocketeer` solves its ordnance lead
at `RoundSpeed = VELOCITY` (`FlightController.cs`, `RoundSpeed = hp.Weapon.Velocity ??
DefaultVelocity`), while since A3 a motor round leaves at the launcher's speed and climbs to
`VELOCITY` above it, so its lead on `wep_04`/`wep_25`/`wep_27` assumes a speed the round does not
have.

**Goal.** A guided round aims where the target will be, and eases into doing so.

**Evidence (confidence: traced).** When the weapon authors `LOCK_ON_LEAD` (`+0x74` bit `0x10000`)
and the round is older than element 0 (`+0x7c`), the desired direction becomes an intercept solve
instead of a plain bearing, slerped from bearing to full lead between `+0x7c` and `+0x80`. Three
entries author it, at `[4,8]` and `[5,10]`.

**Approach.** Reuse whatever lead solver `AiGunner` already has rather than writing a second one;
name it in the landing commit so the two stay in sync.

**Model recommendation.** medium.

**Verify.** `--weapon-lab=wep_11` against a crossing target at constant speed: the round's heading
must lead the target progressively rather than stepping to full lead at the onset time.

**⚠ Traps.** `LOCK_ON` and `LOCK_ON_LEAD` are different keys doing different jobs, and `LOCK_ON` is
doing three of them (guidance ramp denominator, velocity-decay window, and the inherit-at-all flag).
Do not collapse them.

## B8 ☑ The beeper tag: world list, countdown, expiry tail

**Verdict.** The list landed as `Flight/BeeperTags.cs`: `BeeperTags<FlightController>` is built
per session beside the smoke screens, stepped after every aircraft in both step paths, and handed
to the pool as `ProjectilePool.BeeperTags`. The hit-side call landed in `ProjectilePool.Apply`: a
weapon with `BeeperTime` reaching an `AircraftBody`, ray-struck or fused, calls
`BeeperTags.TryTag(round team, victim rig, TIME)` and returns before either damage path, so the
pair is discarded structurally rather than passed as zero and an authored pair would still spend
nothing; the impact effect and sound play as usual, and `Impact`/`Apply` carry the round's own
`Proj.Team` for the gate. The `ordnance-guidance` suite fires a `wep_10` into a hostile rig's
tail: painted with about 20 s remaining, every damage zone untouched, still painted at 19.5 s and
not past 20 s, a fresh tag landing on the rig whose first is in its tail, and the tag collapsing
the step the rig dies. Owed at the controls: `--rocket=wep_10` at an AI, which shows nothing
visible until a seeker follows it. Three things this item's own text left open, settled from
the code and recorded on the decode page: the countdown does not stop at zero, it keeps running to
the −5 deletion (four seconds after a slam, five after a plain expiry), and the slam fires only
while the tag still paints, so a death in the tail does not restart it; a second beeper hit on a
live-tagged aircraft makes no tag and does not refresh the first (`FUN_004b8ca0` inside the gate
`FUN_004b8ce0`), while an aircraft in its tail takes a fresh one; and the gate also refuses a
non-hostile shooter (same team or either neutral, `AimAssist.Hostile`) and any second tag in the
same frame (the original stamps its clock on each creation), which here is one tag per sim step.
This item's trap is moot in this install: `wep_10` authors `ARMOR_DAMAGE 0.0` /
`HEALTH_DAMAGE 0.0`, so there is no real pair to spend. Pinned by `BeeperTagsTests`.

**Goal.** A beeper hit paints its target for the authored time, and the paint expires cleanly.

**Evidence (confidence: traced).** `FUN_004b9bc0` zeroes the damage and calls `FUN_004b88a0`, which
builds a tag carrying the weapon's `TIME` and pushes it onto `DAT_0071dbac`. `FUN_004b8ad0` counts
each tag down by the frame delta, **slams it to −1.0 if the tagged aircraft dies**, stops it at zero
and deletes it only below −5.0, so there is a five-second tail after expiry.

**Approach.** A small world-owned list with the same three-phase lifetime. The tail matters: it is
what stops a tag being deleted while something still holds a reference to it.

**Model recommendation.** medium.

**Verify.** Tag an AI with `--rocket=wep_10`, confirm zero damage is dealt, and confirm the tag's
lifetime matches `TIME` with the aircraft alive and collapses immediately when it dies.

**⚠ Traps.** `wep_10` authors a real damage pair that the engine discards. Do not spend it.

## B9 ☑ `BEEPER_SEEKER`: the per-frame retarget and its selection rule

**Verdict.** The selection rule landed as `BeeperTags<FlightController>.PickTarget(roundPos,
roundHeading)` over `BeeperTagRule.AlignmentDot` and `BeeperTagRule.Prefers`, with the four
literals as named constants. The per-frame retarget landed as `ProjectilePool.RetargetSeeker`:
before the steering gate is read, a `BEEPER_SEEKER` round asks `PickTarget` with its position and
unit heading and REPLACES `Proj.Target` with the answer, null included, as `FUN_00441780` writes
zeros when nothing is painted. Settled from `FUN_005af720`'s order (callback at `+0x688`, then the
health test, then the gate on `+0x70`): the shooter's spawn target is never used by a seeker, since
the callback overwrites it before it is ever read, so a seeker steers only at a painted aircraft
and holds nothing with nothing painted. The `ordnance-guidance` suite launches `wep_11` HOLDING an
unpainted rig beside two painted ones and reads its held target back every frame through
`CollectHeldTargets`: the first frame already holds `PickTarget`'s pick (the nearer rig 30° off,
range leading), every frame matches the rule, the unpainted rig is never held, and clearing the
list clears the slot. Owed at the controls: `--weapon-lab=wep_11` after a `wep_10` paint, which
is the one clip that shows the pair working as one weapon. Two corrections to this item's own text, re-read off
`0x004b8b50`–`0x004b8c91` and recorded on the decode page: both ratios are of SQUARED distances
(`FUN_00538880`, and the routine keeps `1 / bestDistanceSq`), so "up to 20% farther" is about 9.5%
as a length; and the net effect is not "alignment leads". With the inverted dot, `candDot <= 0.7`
is any tag less than about 134° off the round's nose, so a nearer tag steals outright unless it
sits well behind the round, and only then must it give up under 0.1 of alignment; a
better-aligned tag that is farther steals only inside the 1.2 squared window. The pick is a
running best in tag order, so a near-worse and a far-better pair inside that window resolves to
whichever was tagged later, which the tests pin as decoded behaviour rather than a total order.
Pinned by `BeeperTagsTests` at every threshold from both sides.

**Goal.** A seeker follows whatever a beeper has painted, choosing sensibly among several.

**Evidence (confidence: traced).** `FUN_00441830` installs `FUN_00441780` as the round's per-frame
retarget callback. It queries the tag list with the round's position and heading and writes the
winner into the round's target fields, which `B6`'s steering step then reads. The selection rule,
read off the disassembly at `0x004b8c03`–`0x004b8c72` with all four constants (1.2, 1.0, 0.7, 0.1):
the dot's sign is **inverted** because the vector runs from the tag toward the round, so a lower dot
is better aligned; a better-aligned tag may be up to 20% farther, and a nearer tag only wins if it
gives up less than 0.1 of alignment.

**Approach.** Implement the selection rule from the decode page's pseudocode rather than from
intuition about what a seeker "should" prefer; the sign convention makes intuition actively
misleading here.

**Model recommendation.** high. The inverted dot is exactly the kind of detail that produces a
plausible, subtly wrong implementation.

**Verify.** Two tagged targets at different ranges and bearings, confirming the pick matches the rule
at the thresholds. Then one tagged and one untagged target, confirming the seeker ignores the
untagged one entirely.

**⚠ Traps.** `wep_11` is the only `BEEPER_SEEKER` and the only weapon with a real `TURN_RATE`. The
two halves are one weapon system: neither is useful alone, so neither is testable alone.

# Wave C — The blast

## C10 ☑ Splash falloff: quadratic, measured to the shape

**Verdict.** Landed. `ProjectilePool.BlastFalloff(distanceSq, radiusSq)` is `1 − d²/R²` clamped
to `[0, 1]`, fed `WeaponDef.ImpactProximitySqM` and a `DistanceSquaredTo`; the metre-form
`BlastDamage` wraps it for callers holding a distance. Both pools take the same share: destructibles
through `DamageSink`, planes through `TakeProjectileHit(damageScale)`. The distance stays the
`BL-239` nearest-collision-shape measure (0 inside a plane's boxes, so the engulf clamp is built in),
which was already a surface distance; the original's per-node bounding sphere was not adopted
because a Godot body has no such sphere and a chapter mesh's would engulf the map. Reading
`FUN_004cb420` corrected one detail of the evidence: the surface distance IS rooted there
(`FUN_005388d0`, centre to centre, minus the half-diagonal of the node's box from `FUN_004d8bd0`,
clamped, then squared), so `DistanceSquaredTo` on our nearest point is the matching quantity. The
`MINE` branch is not built. `BL-227`'s falloff half is closed; its impulse half stays open.
`blast-curve-cover-cap` measures three destructibles at 0.25R/0.5R/0.75R from one burst: 187.5,
150 and 87.5 of 200 where the linear curve gave 150, 100 and 50 (baseline seen failing on the
committed code before the change).

**Goal.** Blast damage falls off on the original's curve. Closes `BL-227`'s falloff half.

**Evidence (confidence: traced).** `FUN_005acac0` applies
`damage × (1 − d² / IMPACT_PROXIMITY²)` to **both** pools, where `d` is the distance to the target's
**bounding-sphere surface**, clamped to zero for anything the burst engulfs. Our D10 curve is linear
in distance and therefore wrong: at half the radius the original gives 0.75 where ours gives 0.5.

**Approach.** Replace the curve and the distance measure together. The nearest-collision-shape
falloff already landed under `BL-239` is the same idea and may already give the surface distance;
check before writing a second one.

**Model recommendation.** high. It changes damage numbers across every explosive weapon, so the blast
radius of the change is large even though the edit is small.

**Verify.** A cluster of destructibles at measured ranges from a single burst, comparing dealt damage
against the curve at 0.25R, 0.5R and 0.75R. Take the baseline **before** the change: an unchanged
number here is not evidence unless you have seen it able to move.

**⚠ Traps.** This is wrong-claim 2. `+0x40` is a square. A weapon carrying `MINE` skips the falloff
entirely, but nothing authors `MINE`, so that branch is unreachable and must not be built.

## C11 ☑ Splash occlusion test and the 32-object cap

**Verdict.** Landed. Planes and world bodies now share one candidate list in `ApplyDamage`, sorted
nearest first; each is cover-tested by `BlastCovered` (a ray from the burst, lifted 0.1 m along the
struck normal so the wall the round hit shields what is behind it and not its own side, to the
candidate's centre, world layer only, the candidate excluded) and at most `MaxBlastTargets` 32 take
damage per burst, with one `blast cap:` line naming the weapon, the burst and the dropped count
(Decision 9). Reading `FUN_004cb420` settled the open questions and corrected two: the ray is
centre-to-centre through the whole intersect database with the candidate's own intersect bit
cleared and the round's owner cleared for the whole gather; the cap is per burst (the buffer is
reset per query) and counts accepted objects, checked before each candidate, with one log line per
surplus candidate; the gather includes aircraft (they are nodes in the same database and the shooter
exclusion at `FUN_005acac0` is written for them), so planes get the same treatment here. ⚠ One
thing the decode did not settle: the cast runs only for a candidate carrying node flag `0x400000`,
which is not in the GameZ flags and has no immediate setter in the binary; the remake tests every
candidate. Cover is world geometry only, aircraft are not cover (`_coverRay`'s field comment).
`blast-curve-cover-cap`: a target 20 m out takes 111.1 with the way clear and 0 with a wall between
(the wall itself takes 179.9), and forty targets inside the radius leave exactly the nearest 32
damaged; the baseline dealt 66.7 through the wall and damaged all forty.

**Goal.** Cover protects against splash, and the gather has the original's limit.

**Evidence (confidence: traced).** `FUN_005aca30` passes both the distance flag and the occlusion
flag to `FUN_004cb420`, which casts from the burst centre to each candidate and drops it if something
blocks the way. The gather stops at **32** objects, logging "Database intersections array is full".

**Approach.** A ray per candidate against the same collision world the rest of the sim uses. Log when
the cap bites, per Decision 9.

**Model recommendation.** medium.

**Verify.** A burst on the far side of a building from a destructible, which must take nothing, and
the same burst with the building removed, which must take the curve's value. Baseline first.

**⚠ Traps.** Cost. This is one ray per candidate per burst; if it shows up in a profile, cap the
candidate count rather than skipping the test, because the cap is authentic and the test is the
behaviour.

## C12 ☑ The per-weapon impact hook and `SURFACE_ANIMATION` normal orientation

**Verdict.** Landed, and re-reading `FUN_005ac7a0` corrected this item's own evidence on three
points, all recorded on the decode page. The mask's bits are: 1 the row's sound; 2 the crater and
quicksand carves and the row's `EFFECT` slot, **not** the direct damage, which `FUN_005abcf0` deals
before the mask is first read, and not the splash, which is the caller's second half; 4 the row's
`ANIMATION` slot alone, the `SURFACE_ANIMATION` being spawned under no bit. The two "explosion
spawners" the evidence attributed to `ROCKET` and `TANGLER` are keyed on the `.zrd` dispatcher's
`+0x74` word and are the **`CRATER`** (`0x10`) and **`QUICKSAND`** (`0x40000`) carves, `BL-412`'s
subsystem, so nothing of them was built; a carved crater additionally suppresses both animation
slots unless `ANIMATION_ALWAYS`, unauthored here. And the one hook in the binary, the `TANGLER`
parse's `LAB_004ba660`, does not suppress the choker's damage or effect: it builds the choker's
**cloud** (D17) and returns 1, so a choker's `IMPACT` row plays no sound in the original, which
this side reproduces. The hook is `WeaponDef.ImpactHook`, an enum with one arm dispatched by
`ProjectilePool.RunImpactHook`, and the mask is `ImpactSuppression`, fed to `ImpactOutcome.Resolve`
so `Apply` still performs a resolved row and decides nothing. The orientation is
`ProjectilePool.SurfaceUpBasis`, the shortest rotation from world up (`DAT_006379c0`) onto the
struck normal, applied only to the slot `ImpactOutcome.SurfaceOriented` names, and reaching the
effect template through a new `Basis` on `EffectSink` → `AnimRuntime.PlayEffectAt` →
`TemplateStage.PlaceOn` (set when given, untouched when null, so every other placement path is
unchanged); the plain `ANIMATION` passes identity, which is BL-293's parked half left exactly as it
was. `impact-orientation` fires `wep_06` into a 30° slope (its `he_ground_effect` arrives with its
Y on the slope normal), into flat ground (identity) and `wep_12` into the slope (`scatter_effect`,
a plain `ANIMATION`, identity). Owed at the controls: a rocket into a real chapter slope, never flat
C1 terrain, where the rule changes nothing.

**Goal.** A weapon can suppress parts of its own impact, and surface effects sit on the surface.
Closes `BL-293`'s actionable half.

**Evidence (confidence: traced).** `FUN_005ac7a0` calls a per-weapon hook at weapon `+0x20c` first,
which returns a suppression mask: bit 1 silences the row's sound, bit 2 suppresses damage and
effects, bit 4 suppresses the impact animation. `FUN_005aec90` writes that slot and the `TANGLER`
parse is its one caller. The same routine spawns the `IMPACT` row's `SURFACE_ANIMATION` with an
orientation built from the struck surface's normal, where the row's plain `ANIMATION` gets none.

**Approach.** The hook is a one-entry dispatch in this data, so a simple delegate on the weapon def
is enough. Resist generalising it into an event system.

**Model recommendation.** medium.

**Verify.** A rocket into a slope: the `SURFACE_ANIMATION` must lie on the slope while the plain
`ANIMATION` keeps its fixed axis. On flat ground the rule changes nothing, so **do not verify on flat
C1 terrain**.

**⚠ Traps.** `BL-293`'s parked half stays parked: the fixed-axis upper ring is a plain `ANIMATION`
and is correct as-is. Only `SURFACE_ANIMATION` takes the normal.

# Wave D — Disabling effects

## D13 ☑ `ScreenFlash`: the victim-routed blend channel

**Verdict.** Landed: `ScreenFlash.PlayBlend(playerIndex, colour, weight, duration, startDelay)`
routes by the victim's `FlightController.PlayerIndex` (a human's pane index; an AI's is out of range
and paints nothing) into a per-pane `BlendWash` (`src/UI/BlendWash.cs`) that carries the original's
own blend arithmetic and its attack/sustain/release envelope (0.15 / 0.35 of the duration, traced in
`FUN_0042eb80` while building this; the decode page now states how the two timings act, plus the
1.0 s start delay the sonic/flash caller passes and the smoke caller does not), composited over the
untouched proximity ramp in `Apply`. `--debug-wash=N` addresses two overlapping scripted washes to
viewer N. Pinned by `BlendWashTests` (rules) and the `fbfx-flash` suite (player 2 addressed, pane 1
clear, ramp unchanged underneath). The at-the-controls `--coop --players=2` look remains owed to a
human.

**Goal.** A screen wash can be addressed to the viewer flying a given aircraft, and blends with
whatever that pane is already showing.

**Evidence (confidence: lead-only for the composition rule, traced for the wash maths).**
`ScreenFlash` is already per-pane with an index-aligned `ViewerSet`, but its existing gate selects
panes by **camera proximity** and its `Play` **replaces** a running ramp. Its own comment states the
proximity rule is "never 'the hit player'", because two of its three carriers are ground effects.
Sonic, flash and smoke are the opposite: the original routes them by who was hit, and blends on
overlap as `w + p − w·p` with the colour mixed toward the new one by the incoming weight. There is no
original to copy for how the two channels compose with each other, per Decision 3.

**Approach.** Add a second channel with its own routing (viewer-of-victim) and its own composition
(blend), composited with the ramp channel at paint time inside `Apply`. Build the channel alone, with
no weapon using it yet, so `D15` and `D18` are pure consumers. Do not touch the ramp channel's
routing or semantics.

**Model recommendation.** high. Two composition rules over one pixel, with an existing consumer that
must not regress.

**Verify.** `--coop --players=2`: a debug command addressing a wash to viewer 2 leaves viewer 1
untouched, and an HE burst still washes by proximity exactly as it did. Take the HE baseline first.

**⚠ Traps.** Do not reproduce the original's single global. It is the one place in this plan where
matching the original would produce a worse game, and Decision 2 records that as deliberate.

## D14 ☑ `SONIC`/`FLASH`: the shared intensity model

**Verdict.** Landed as `Flight/DisablingIntensity.cs`, a pure `TryResolve` on squared distances
returning the intensity and five times it, with `FLASH`'s facing test behind a flag. One correction
to this item's own text: the facing scale below a dot of 0.5 is **twice the dot**, not a halving, so
it ramps continuously to nothing at 0 and meets the unscaled value exactly at 0.5.

**Goal.** One intensity number, correct for both weapons, driving both the human and the AI effect.

**Evidence (confidence: traced).** `FUN_0042e840`: `ratio = min(1, d²/IMPACT_PROXIMITY²)`, then
`intensity = 1` while `ratio < 0.6` and `1 − (ratio − 0.6) × 2.5` from there to the radius. It is a
**plateau**, full strength out to √0.6 ≈ 77% of the radius, then a fade over the last quarter.
`FLASH` additionally requires the victim to be **facing** it: zero on a negative dot against the
victim's forward axis, halved below 0.5. `SONIC` has no such test. The routine returns the intensity
and five times the intensity.

**Approach.** One function returning both numbers, with the facing test parameterised by the flag.
The facing rule is the only behavioural difference between the two flags beyond colour.

**Model recommendation.** medium.

**Verify.** Unit-test the curve at the plateau edge and the radius, and the facing test at dot 0, 0.4
and 0.6. `wep_15` authors `IMPACT_PROXIMITY [500]`, so full strength reaches about 387 m.

**⚠ Traps.** The ratio is squared on both sides. Feeding it a plain distance moves the plateau edge
from 77% to 60% of the radius.

## D15 ☑ The player's screen wash: colour, weight, duration, blending

**Verdict.** Landed as `ProjectilePool.ApplyDisabling`, run from `Apply` on every `SONIC`/`FLASH`
burst (struck, fused or timed out), with D16's stun wired beside it. Re-reading `FUN_004b9bc0`'s
callers settled who the victims are: **every aircraft the splash gather finds inside
`IMPACT_PROXIMITY`**, not the struck one alone, because `FUN_005acac0` hands each hit-buffer entry
to the same routine with its squared surface distance and the branch runs the intensity on that;
the struck aircraft's own callback (`FUN_004b9770`) reaches it too, with the origin-to-burst square.
So the walk here is the blast's own gather, cover test and 32 cap over the registered aircraft,
each victim resolved through `DisablingIntensity.TryResolve` on its squared hull distance with the
`FLASH` facing dot from its nose to the burst; a human's `PlayerIndex` gets `WashSink` (the pool's
new sink, `GameSession` assigns `ScreenFlash.PlayBlend`) with red `(1,0,0)` for `SONIC`, white for
`FLASH`, weight the intensity, duration five times it and the literal 1.0 s start delay the branch
passes `FUN_0042e9d0`, and an AI gets `TryStunPilot`; nothing touches a human's control, and no
damage moves: `AircraftDamageDiscarded` (the four no-damage types) returns `Apply` before a struck
plane's ledger and keeps aircraft out of `ApplyDamage`'s gather, so the beeper's zero pair no
longer flashes "HIT" either. `disabling-hits` flies it on two human rigs over a real two-pane
`ScreenFlash` and two AI rigs: a `wep_08` into a human's tail washes that pane red at weight 1 for
5 s, painting only after the delay and clearing at the duration, with the other pane clear
throughout; a `wep_09` from behind washes nothing and one from ahead washes white; two humans hit
in one step each carry their own wash; an AI 14 m from one of those bursts is stunned the full 5 s;
an AI is stunned 3.8 s by a sonic fusing 29 m abeam (the fade), 5.0 s by a direct hit while
stunned, and back to 3.8 s by the next passing burst (the expiry is written, never maxed); and no
ledger moves. Owed at the controls: `--rocket=wep_08`/`wep_09` against the player for the look of
the wash, and `--vs --players=4` with two viewers hit in one second.

**Goal.** A human hit by a sonic or flash round loses their view for up to five seconds, and
overlapping hits stack sensibly.

**Evidence (confidence: traced).** `FUN_0042e9d0` runs a full-screen colour wash: `SONIC` red
`(1,0,0)`, `FLASH` white `(1,1,1)`, at a weight equal to the intensity and a duration of **five
times** the intensity. Two derived timings at 0.35 and 0.15 of the duration split it into phases.
Overlapping washes blend as described in `D13`. **Nothing on the player path touches the controls.**

**Approach.** Through `D13`'s channel, addressed to the viewer flying the struck aircraft.

**Model recommendation.** medium.

**Verify.** `--rocket=wep_08` and `--rocket=wep_09` against the player: red and white respectively,
about five seconds at point blank, and two overlapping hits neither saturating nor replacing. Then
`--vs --players=4` with two viewers hit in the same second, confirming each pane carries its own
wash.

**⚠ Traps.** No input lockout for the player, ever. The asymmetry with `D16` is the design: the same
round blinds a human and disables an AI.

## D16 ☑ The AI stun

**Verdict.** Landed as `AiPilot.Stun(seconds)` behind `FlightController.TryStunPilot(seconds)`, the
entry the hit path and the smoke screen call on a struck aircraft; the guards (human, dead or inert,
no AI pilot) live in that method. The mask is neutral stick and rudder with the throttle lever left
alone, so the aircraft stays on the flight model. Two corrections to this item's text: "state 0 or
4" is the **vehicle dispatch class** at `+0x67c` (`jet`/`wingman`), not the AI mode, so the routine
accepts a pilot in any mode including its own stun; and `+0x978` is the pilot's
`stun_recovery_interval`, **display-only** in this routine (the debug line prints it whatever
seconds were passed; only the sixth-sense caller passes it as the duration). The expiry is
overwritten, not maxed. Decision 6 was applied against a machine that already carried the original's
state 4 as `AiMode.Stunned` (the sixth-sense fail, D11), so no mode was added: `AiModeMachine.Stun`
is the one entry both stuns share, and a machine-less pilot keeps its own countdown. The hit-side
wiring is D15's `ApplyDisabling`, and `disabling-hits` asserts the stun, the re-stun and the
overwrite on a live AI rig. Owed at the controls: an AI going limp for about five seconds under
`--rocket=wep_08` and resuming.

**Goal.** An AI hit by a sonic or flash round stops flying for up to five seconds.

**Evidence (confidence: traced).** `FUN_004200d0` sets AI mode 4, zeroes the three stick channels raw
and copied, and writes an expiry of clock + seconds, the caller's seconds being intensity × 5. It
refuses a dead victim, the player, a victim with `+0xf8` set, and any vehicle whose class is not
`jet` or `wingman`. `+0x978` in its debug line is `stun_recovery_interval`, traced to the skill
loader and the roster spawn; it multiplies nothing here ([`org/ordnanceTypes.md`](org/ordnanceTypes.md)).

**Approach.** Per Decision 6: mask `AiControlLaw`'s output while stunned and expose an `IsStunned`
flag for `AiModeMachine` to read. Do **not** add a mode. The aircraft stays on the flight model, so a
stunned pilot keeps its momentum and falls convincingly.

**Model recommendation.** high. It touches the AI input path, where a wrong guard strands a pilot
permanently.

**Verify.** `--ai=… --ai-attack=…` with a sonic round: the AI must go limp for about five seconds and
resume. Confirm it can be re-stunned while already stunned, since the original accepts its own stun
state as an input state.

**⚠ Traps.** Re-entrancy is required, not accidental: `D18` refreshes the stun every frame a pilot
stays in smoke. Do not make it single-shot.

## D17 ☑ `TANGLER`: the engine-dead timer

**Verdict.** The mechanism landed first (below), and the hit side landed with C12/D15, where
re-reading the `TANGLER` hook corrected the catch: **the choker leaves a cloud.** `LAB_004ba660`
builds an object at the burst holding the weapon's `RADIUS` squared and its `TIME` (`wep_12`
authors 2 s) and pushes it onto a world list; every frame `FUN_004b96d0`/`FUN_004b9590` count each
cloud down and hand every alive aircraft whose **origin** sits inside the squared radius to
`FUN_004b9bc0` with that squared distance, so the `TANGLER` branch runs and `FUN_004b1690`'s
extend-only timer is refreshed on every frame the aircraft stays inside. So `RADIUS` is both the
catch (35 m is 35 m) and the duration's denominator (raw), the round's `DETONATION_DISTANCE`
decides only where the cloud forms, nothing excludes the shooter, and a victim inside is held at
the formula's value until the cloud is gone and only then runs down. The remake is
`ProjectilePool.SpawnTanglerCloud` (the hook's arm) and `StepTanglerClouds` (per `SimStep`, origin
distance squared through `TanglerChoke.Duration` into `TryChokeEngine`), with `EngineDeadBounds`
assigned by `GameSession` from `TanglerChoke.EngineDeadBounds`. The same hook returns 1, so a
choker's `IMPACT` row plays no sound in the original and none here. `disabling-hits` reads a 12.7 s
choke on the AI a `wep_12` struck (about 1.1 m from its origin), the 5 s floor on a human 20 m out,
both held while the cloud lives, the cloud gone at 2 s, and the timer running down after. Owed at
the controls: `--rocket=wep_12` against an AI, the aircraft bleeding speed on drag, and the missing
engine-loop swap.

The mechanism, as first landed. `TanglerChoke.Duration(distanceSq, radiusRaw, min, max)` is the curve, with the
unit mismatch reproduced as Decision 7 asks, and `TanglerChoke.EngineDeadBounds(WeaponDefs)` resolves
the `ENGINE_DEAD` pair the way the original's globals do: every `TANGLER` parse overwrites them, so
the last entry carrying one wins. They are taken as arguments rather than added to `WeaponDefs`,
which this item does not own; this install authors exactly one `TANGLER` (`wep_12`), so the rule is
unobservable in play either way. The cutout is `FlightModel.ChokeEngine`, an extend-only timer that
zeroes the thrust term and nothing else, with `FlightController.TryChokeEngine` holding the victim
guards and `Respawn` clearing it. Two things this item's own text left open: the choker applies to a
human exactly as to an AI (the branch has no player guard, unlike the stun), and what the original
does beyond thrust is a **sound** swap, the rising edge exchanging the vehicle def's engine loop for
a second authored loop (`FUN_004b15c0`, vehicle def `+0x18c`/`+0x190`), which our `PlaneStats` has no
slot for; our side cuts thrust only, so a choked aircraft still sounds and looks like it is running.

**Goal.** A choker cuts the target's engine for a distance-scaled time, and does nothing else.

**Evidence (confidence: traced).** `duration = ENGINE_DEAD_max × (1 − d² / TANGLER_RADIUS)`, floored
at `ENGINE_DEAD_min`, setting a disabled-systems bit with a timer that only ever extends. The bounds
are **globals** set by the last-parsed `TANGLER` weapon, not weapon fields. **There is no airspeed
clamp**: the routine zeroes the damage, sets the bit, and touches nothing else.

**Approach.** A timed thrust cutout plus the entangle time and catch radius. Per Decision 7,
reproduce the unit mismatch exactly: the numerator is a squared distance and `RADIUS` is stored raw,
so with `wep_12`'s `[35]` the floor is reached at √21.5 ≈ 4.6 m while the catch radius stays 35 m.

**Model recommendation.** medium.

**Verify.** `--rocket=wep_12` against an AI: engine out for 13 s at the centre and 5 s beyond about
4.6 m, with the aircraft bleeding speed on drag rather than snapping to stall.

**⚠ Traps.** The recollection that the choker stalls a plane **instantly** is not what the code does
and is recorded as disproven on the decode page. Do not add an airspeed clamp to match a memory. No
AI code reads the disabled-systems mask, so a choked AI is not told it has been choked and gets no
evasive reaction; that is correct.

## D18 ☑ `SMOKE_SCREEN`: the stun trap

**Verdict.** The mechanism landed as `Flight/SmokeScreens.cs`: `SmokeScreens.Lay(layer, TIME)` is
the fire path's entry, the session steps the registry after every aircraft, and each running screen
walks the roster about the layer's LIVE backward axis (`-NoseDirection`), washing a human on its own
pane through `ScreenFlash.PlayBlend` and refreshing `TryStunPilot(smokescreen_stun_interval)` on
an AI every step. Nothing lays a screen yet: the launch-side hook (the pylon fire path spawning no
round for a `SMOKE_SCREEN` weapon and calling `Lay`) is the spawn lane's, and until it lands
`--rocket=wep_13` still fires the ordinary round. Three things this item's own text left open,
settled from `FUN_004b8fd0` and recorded on the decode page: the pose is read live each frame, not
captured at the lay; a layer that dies ends its screen on the spot; and the "2 s per-victim
cooldown" is a 2 s timer that re-arms below 0.5 s, so a victim inside is re-washed every 1.5 s at
0.9, and 0.97 only when the timer had fully run down. The stun interval's loader default is 3 s.
Pinned by `SmokeScreenTests` and the `smoke-screen` suite.

**The launch hook has since landed with `A5`.** A pylon whose weapon carries `SmokeScreenTime` calls
`SmokeScreens.Lay(this, TIME)` from `FlightController.ApplyFireOutcome` and spawns nothing, which is
what `FUN_004b6820`'s `0x10000` branch does on both the player's and the AI's side; the registry
reaches every aircraft on `FlightController.SmokeScreens`, assigned to each AI as it spawns and to
each rig's controller as the assembler builds it, so a shipped AI smoker lays through the same path.
One thing this item did not state, settled from the branch: the weapon's `FIRE` row does **not**
sound or animate on a smoke launch, because both live inside the spawn `FUN_005aef40` the branch
skips; only the screen object's own emitter is created. Ammo, the fire clock and the rate limit sit
after the branch and are shared, so the smoker spends a round like any other pylon weapon. The
`ordnance-launch-axis` suite pins all three (one screen laid, zero rounds in the pool, one round of
ammo gone). Owed at the controls: the 1v1 and the coop pair below.

**The screen's emitter is started and stopped with the screen.** `FUN_004b8d50` instances the
`generate_smokescreen` effect and attaches it to the layer's node, so it follows the aircraft, and
`FUN_004b8f60` releases it down both of `FUN_004b8fd0`'s end branches; our side is the
`ISmokeEmitter` seam `SmokeScreens.Lay` starts and `SimStep` stops, with `SmokeScreenEmitters`
running the definition's two DISTANCE_INTERVAL puffers at the layer's live pose. The cloud now
draws as authored (four puffs per 0.65 m with the 10 m/s astern `LOCAL_VELOCITY`, and the
`53,74,37` ramp linearised so it lands on the reference's `50,68,35`), the two gaps that had left it
a thin pale ribbon being the trail path's single-puff spawn and the unlinearised ramp, both in
`Puffer`/`EmitterRenderer` and shared by every distance trail and every ramped puffer.

**Goal.** A smoke screen laid by an aircraft stuns AI and blinds humans behind it, and spawns no
projectile.

**Evidence (confidence: traced).** The launch path spawns **no round**: it builds a world object
carrying `TIME [8]` and pushes it onto a world list. Every frame while that timer runs,
`FUN_004b8fd0` walks the aircraft list and hits anything alive, not the layer, within
`smokescreen_stun_range` and inside a cone `dot > cos(smokescreen_stun_angle / 2)` about the layer's
`+0x198` axis. That axis is the **backward** one: the player's forward launch negates the same field.
The player gets a grey-green `(0.2, 0.29, 0.145)` wash at weight 0.9 (0.97 on the first hit) for 2 s
on a 2 s per-victim cooldown; an AI gets `D16`'s stun for `smokescreen_stun_interval`, refreshed
every frame it stays inside.

**Approach.** A world-owned volume that tracks the laying aircraft, reusing `D16` for the AI branch
and `D13`'s channel for the human branch. It is **not** an occluder and must not be given a collision
or visibility role.

**Model recommendation.** high. It is the most powerful weapon in the table and the per-frame refresh
makes it easy to make either useless or unescapable.

**Verify.** `--rocket=wep_13` in a 1v1: confirm no projectile spawns, that a pursuing AI is stunned
while inside and recovers on leaving, and that the cone excludes an aircraft off to the side beyond
85°. Then `--coop --players=2` with one viewer laying and the other pursuing.

**⚠ Traps.** 600 m across a 170° cone is close to everything behind the layer. The instinct on seeing
that in play will be that it is a bug; it is the authored value. The angle is a **half**-angle
cosine, so 170° means 85° off-axis, and reading it as a full cone halves the weapon's reach.

# Wave E — Flyout and gates

## E19 ☐ `TARGETABLE`: admission to the target list

**Goal.** A torpedo in flight can be selected and shot at, by the player and by AI.

**Evidence (confidence: traced).** `FUN_00441830` wraps a `TARGETABLE` round in a `TargetProjectile`
and pushes it onto the target list, setting the admission byte that
[`org/aiPilot.md`](org/aiPilot.md) names. A round carrying only a fuse distance gets the same wrapper
with the byte clear, so it is fused but not targetable.

**Approach.** Extend the existing target registry rather than adding a parallel one; `aiPilot.md`
already documents the four list types.

**Model recommendation.** medium.

**Verify.** Fire `--rocket=wep_14` and confirm it appears as a cyclable target, and that an ordinary
rocket does not.

**⚠ Traps.** `TARGETABLE` and `FLYOUT_HEALTH` are different halves of "shootable" and the torpedo
carries both. Admission alone does not make it destructible; that is `E20`.

## E20 ☐ `FLYOUT_HEALTH`: the health pair, and destruction at zero

**Goal.** A torpedo can be shot down, and its destruction plays the authored effect.

**Evidence (confidence: traced).** The spawn seeds the round's pair from weapon `+0x8c`/`+0x90`;
without `FLYOUT_HEALTH` both take the **−1.0** not-shootable sentinel. `FUN_005abcf0` spends the pair
armour-then-health, and `FUN_005af720` checks health `== 0.0` every frame before anything else,
destroying the round and playing `DESTROY_ANIMATION`. **The armour pool is always zero**, since
`+0x8c` is only ever written as a literal 0, so the first hit spends health directly.

**Approach.** Reuse the aircraft zone's two-pool spend so the semantics match exactly. Keep the −1.0
sentinel rather than a bool: it is what makes the equality test safe.

**Model recommendation.** medium.

**Verify.** Shoot a `wep_14` in flight with guns: 10 points of health, then destruction playing
`torpedo_destroy_effect`. Confirm an ordinary rocket is unaffected by gunfire.

**⚠ Traps.** The two-pool structure is real but inert as shipped. Implement both pools so the spend
order is right, but do not invent an authored armour value to make the first pool meaningful.

## E21 ☑ `DAMAGES_ZEPPELIN` two-way gate and the AI ordnance aim threshold

**Verdict: already correct, and now pinned.** Every claim in this item was confirmed against the
code and lands no behavioural change: `AiRocketeer` runs the two-way match, its 5° gate is the
ordnance threshold (`AimQualityCos` 0.9962) and is a separate constant from the gun's 10°
(`AiGunner.AimQualityCos` 0.9848), and the player skips both by construction because neither class
runs for a human pilot. What landed is the evidence: the threshold pair is pinned as angles, and the
match is exercised on the Black Hat Warhawk's authored fit read out of `vehicle.zrd.json`. The
at-the-controls half is blocked elsewhere: the AI acquisition admits aircraft alone (`BL-363`) and
no stock loadout carries `wep_14` because the vehicle def's `weapons` tuple is unparsed (`BL-394`).

**Goal.** The AI fires the right weapon at the right target class, and aims ordnance more precisely
than guns.

**Evidence (confidence: traced).** `FUN_004b6820` drops the trigger both when a weapon **without**
`DAMAGES_ZEPPELIN` is pointed at a zeppelin and when a weapon **with** it is pointed at anything
else, so the torpedo is restricted to zeppelins rather than merely permitted against them. The aim
gate threshold is **cos 5° for ordnance against cos 10° for guns**, and the whole gate is skipped
when the shooter is the player.

**Approach.** Both are conditions on the existing AI fire path. `AiRocketeer` already has a 5° gate;
confirm it is the ordnance threshold and not a shared constant with the gun path.

**Model recommendation.** medium.

**Verify.** A Black Hat flight, whose Warhawk carries eight torpedoes: they must stay on the rail
against aircraft and launch at a zeppelin. Then an ordinary rocket AI, which must refuse a zeppelin.

**⚠ Traps.** The player skips this gate entirely. Do not apply the thresholds to player fire.

# Wave F — Sign-off

## F22 ☐ Four-viewer splitscreen pass and the per-type playtest

**Goal.** Every landed behaviour is correct with four viewers, and each type has been seen doing the
right thing at the controls.

**Evidence (confidence: lead-only).** No original reference exists for splitscreen behaviour of these
weapons, per Decision 2. This item is judgement at the controls against the plan's own decoded rules.

**Approach.** One pass per type under `--coop --players=4` and `--vs --players=4`, plus a re-run of
the single-viewer clips from each item's Verify to confirm the wash channel did not regress them.

**Model recommendation.** <TODO: this is a human-at-the-controls item; a model tier applies only to
the harness work around it.>

**Verify.** <TODO: write the playtest rows. They belong in `playtest.md` as an `[Owed-playtest]` set
once the first Wave D item lands, not before.>

**⚠ Traps.** `BL-389` already reports the splitscreen weapon mix needs a retune, with rockets too
quiet against guns. Do not conflate a mix problem with a behaviour problem while judging these.
