# Armour layer — two-pool zone damage (`BL-085`)

**ACTIVE PLAN** (drafted 2026-08-04, activated 2026-08-04). It sits in `docs/`, which by this
repo's convention makes it a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to
`docs/plans/` with a `COMPLETE` banner, and add its row to [`plans/plans.md`](plans/plans.md), when
every item lands.

This plan lands `backlog.md`'s `BL-085`: the receiving side of damage grows a second pool. Every
damage zone carries (armour, health), incoming damage spends **armour first with 1:1 overflow into
health within the same shot**, and every consumer of the old single `Hp` (grazes, the damage lab,
the gauge cluster, the damaged-engine audio, the HUD DMG line) reads the two-pool model coherently.
It also carries the corrected form of `BL-173` (closed 2026-08-04, see `docs/HISTORY.md`) as C21 —
the gauge's zone colour finally has both pools to derive from (the manual's four-band mapping,
Decision 5), though **not** as the ring split that entry proposed (⚠ table #5). Verified still open 2026-08-04: `PlaneDamage.cs` is a
single `float Hp` with a flat subtract (`CSVM/src/Flight/PlaneDamage.cs:52-58`), `PlaneStats`
drops the pair's second float (`PlaneStats.cs:253-255`), and `GaugeCluster` feeds one fraction to
both rings — `docs/HISTORY.md` 2026-08-03 records `BL-085` as "unblocked, schedulable and
unscheduled".

**Assumption this plan rests on** (settled 2026-08-03, `docs/HISTORY.md` "the `destroyable_parts`
pair is (hit points, armor)"): the armour pool is the **second** value of each `destroyable_parts`
pair, in armour points 1:1 with the original's armory units (stock Bloodhawk ~20/zone ==
`pbloodhawk` 20/20/20/20, observed at the controls; per-zone cap 60, uniform, `CAP-19`).

## Milestone goal

- `DestroyablePart` carries both floats; `PlaneStats` reads them; nothing anywhere folds armour
  into hp.
- `PlaneDamage` holds two pools per zone; `Apply` takes (healthDamage, armorDamage) and spends
  armour first, overflowing 1:1 into health within the same shot.
- The graze path, the damage lab, the gauges, the audio fraction and the HUD readouts all consume
  the two-pool model; the gauge's zone colour follows the manual's four-band mapping over the
  combined armour→health progression (green / yellow / orange / red — both rings stay the same
  colour, by design), closing `BL-173` in its corrected form.
- The intended consequence is recorded as faithful: a stock zone's effective pool **doubles**
  against a balanced round. That is the original's behaviour, not a regression.

**No new damage *sources*.** Nothing here makes guns hit aircraft (that is `BL-226`(a): planes are
`CastMotion` query shapes, not bodies — a prerequisite plan of its own), and nothing binds
`player.json`'s `crash` block (`armor_damage_range`/`health_damage_range`/`bounce_factor`) — that
lands with the graze-pushback rework (`BL-172`), which wants the pushback and the armour-aware
crash damage as one coupled change. This plan builds the receiving model and routes the paths that
exist today. World destructibles stay health-only (measured over 16,114 defs) — untouched.

## Decisions (2026-08-03, inherited from the unblocking pass; 2026-08-04 for scope)

| # | Question | Decision |
|---|---|---|
| 1 | Depletion order | **Armour first, 1:1 overflow into health within the same shot** — retail string 3372 states it outright; C23's dominance argument agrees; `CAP-19` observed armour depleting before health. |
| 2 | Where the armour pool comes from | **`destroyable_parts`' second float** — armory units are armour points 1:1; stock airframes read the zrdr numbers back (2026-08-03). |
| 3 | The 2× effective pool at stock | **Intended — record as faithful, do not tune away, do not file a capture to "measure" it** — it follows arithmetically from armour==hp at stock plus armour-first 1:1 overflow (decided 2026-08-03). |
| 4 | `crash` block binding (grazes/crashes spending `armor_damage_range`/`health_damage_range`) | **Out of scope — deferred to `BL-172`** — the pushback (`bounce_factor`) and the armour-aware crash damage are one coupled change; grazes here keep the hand-authored `GrazeMaxDamage` magnitude but spend it through the new two-pool `Apply`. |
| 5 | How the gauge shows two pools | **The rings are NOT split — both always show the same colour** (user, 2026-08-04, from the game manual's Crispen Mark V description). Zone colour comes from the combined sequential progression: green = untouched; yellow = up to 50% of the zone's armour destroyed; orange = 50–100% of armour destroyed and 0–25% of airframe destroyed; red = 25–100% of airframe destroyed. |
| 6 | What drives the damage-anim thresholds (`injure_anims`: pdpanel flips, smoke/fire, the cockpit colour cycle) | **Settled in A2 (2026-08-04): the combined sequential fraction — (armour+health remaining) / (armour+health max)**, and `PartState.Fraction` is that. The colour-cycle anims and the torn-skin flips share one undifferentiated `injure_anims` list, so the original fed them one scalar; the manual's yellow band (armour damage only) confirms visuals react before health is touched. **Reconciliation of the shipped 0.72 / 0.46 / 0.20 (`docs/formats/hud.md`, "Thresholds") with the manual: they agree.** The earlier worry compared the manual's figures to the shipped fracs as if both were boundaries. They are not — the manual gives each band's *envelope*. On the combined scale at stock, 0.72 falls at 56 % of armour gone (manual yellow: ≤ 50 % armour — the one near-boundary, prose vs data), 0.46 just past armour zero at 8 % of the airframe (manual orange: armour half-to-fully gone, ≤ 25 % airframe ✓), 0.20 at 60 % of the airframe (manual red: 25–100 % airframe ✓). Every shipped frac lands inside its manual band, so C21 can feed the mined thresholds the combined fraction with no hand-authored constants. On a health-only scale the manual's yellow band is unreachable — that reading is dead. ⚠ Left standing for B11's eyeball: the def-level `player_fuelleak` at 0.85 now fires while only armour is spent (30 % of the armour gone, airframe untouched). Combined-scale thresholds coincide with the manual's piecewise definition only while armour == health max; diverges once the armory (`BL-067`) exists — moot at stock, recorded. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | C23's model 1: "player planes — per-part HP, no armour/health pair" | The armory observation, 2026-08-03: armour varies independently of health; the pair is (hp, armour). `BL-085` supersedes it. |
| 2 | The second `destroyable_parts` float is a duplicate / a (max, current) pair | Same pass: all 88 pairs are equal because they are *stock loadouts* — armour is purchasable (`BL-067`), not duplicated. |
| 3 | `ARMOR: Standard (N/T/W)` blurb triple is the per-zone cap | `CAP-19`: the cap is a uniform **60**/zone, far smaller than any blurb triple. What the triple means went back to open — nothing here depends on it. |
| 4 | "The 2× needs a capture to measure it" | It is **entailed**: armour==hp at stock (all 88 entries) + armour-first 1:1 overflow (observed) → 2× arithmetically. A live sortie moves ammo type, hit distribution, graze damage and pilot skill at once and cannot isolate it. |
| 5 | `BL-173`'s fix shape: "drive `Border` from the armour pool's fraction and `Fill` from health's" | The game manual's Crispen Mark V description (user, 2026-08-04): the rings always show the **same** colour; the four colour bands are thresholds over the combined armour→health progression, not two independent ring bindings. The backlog entry's *problem* stands (one number where two belong); its proposed mechanism is dead. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, B12 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | C21 (band boundaries at the edges; readout normalisation) | The mapping is the manual's (Decision 5); which side of a boundary the exact 50%/25% values fall on, and the 100%-vs-cap normalisation, get confirmed on screen, not invented. |

## What the data actually ships

- **88 `destroyable_parts` pairs, all equal at stock** (`analysis/gdd-cross-check/damage_pools.py`:
  88 entries, 0 unequal). Equal because stock loadouts, not because duplicated.
- **18 of 48 `BALLISTICS` entries carry `ARMOR_DAMAGE != HEALTH_DAMAGE`** — the player ammo-type
  system (`docs/formats/weapons.md`, "The player damage matrix"). With one pool, AP is strictly the
  worst round in every calibre (30 AP 1.5 health vs 30 DD 4.5) — the tier is inverted today.
- **`CAP-19` (flown, discharged 2026-08-03):** armour depletes before health; per-zone cap 60,
  uniform across a plane's four zones; a stripped zone visibly falls faster.
- `WeaponDef.ArmorDamage` is parsed (`WeaponDefs.cs:80,240`) and consumed only by display strings
  (`Probes.cs:156`, `WeaponLab.cs:369`).
- `player.json`'s `crash` block (`armor_damage_range [50,300]` / `health_damage_range [50,300]` /
  `bounce_factor 0.6`) is unconsumed on every axis — weapons, grazes, crashes. Stays that way here
  (Decision 4).
- Canonical worked example: `pbloodhawk` — nose/tail/leftwing/rightwing at 20/20 each; see
  `docs/formats/vehicle.md`, "The hp pair: armor + hit points".

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
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

### Wave A — the data reads both floats

1. ☑ `DestroyablePart` gains `MaxArmor`; `PlaneStats` reads both values of the pair
2. ☑ `PlaneDamage` becomes two pools with armour-first 1:1-overflow `Apply` (unit-tested)

### Wave B — every existing consumer speaks two-pool

11. ☑ Graze path spends through the new `Apply`; flash/log/gauge-blink stay coherent
12. ☑ Damage lab drives and reads both pools; probes/summaries print both

### Wave C — the gauge colour bands (`BL-173`, corrected)

21. ☐ Zone colour follows the manual's four-band armour→health thresholds; rings stay in lockstep

## Dependency and parallelism notes

A1 → A2 → B11/B12 → C21 is the chain. B11 and B12 touch different files (`FlightController.cs` vs
`DamageLab.cs`/`Probes.cs`) and could run in parallel worktrees, but both depend on A2's `Apply`
signature — simplest is listed order, no parallelism. C21 additionally waits on B12 so the lab can
drive the two pools independently while confirming the colour ladder.

---

# Wave A — the data reads both floats

## A1 ☑ `DestroyablePart` gains `MaxArmor`; `PlaneStats` reads both values of the pair

**Goal.** Every parsed part carries (MaxHp, MaxArmor) from the `destroyable_parts` pair; no
consumer change yet — the second float stops being dropped.

**Evidence (confidence: traced).** `PlaneStats.cs:253-255` takes the first float via
`case float hp when !hpSet` and skips the second; the block comment at `:235-238` even documents
"the two hp values are identical for the player defs (AI variants differ); the first is taken as
max HP". `docs/formats/vehicle.md` ("The hp pair: armor + hit points") settles the pair's meaning —
**(hit points, armour)**; confirm which position is which against that section before wiring, don't
trust this plan's memory of the order.

**Approach.** Add `MaxArmor` to `DestroyablePart` (`PlaneStats.cs:30-39`); extend the parse loop to
capture the second float; update the `:235-238` comment (it currently states the drop as intended).
Update `docs/formats/vehicle.md:93,190` — both cite the dropped float as "a known gap, `BL-085`".

**Model recommendation.** medium, low effort — mechanical parse extension with an exact trace.

**Verify.** `.\RunTests.ps1` full pass; a unit test asserting `pbloodhawk` parses 20/20 on all four
parts; goldens hash-identical (no behaviour change yet).

**⚠ Traps.** Do **not** default `MaxArmor` from `MaxHp` when a def carries only one value — a
one-float def means no armour, not equal armour. AI `r*` variants carry *unequal* pairs; they're the
regression case proving the two fields are read independently.

## A2 ☑ `PlaneDamage` becomes two pools with armour-first 1:1-overflow `Apply` (unit-tested)

**Landed 2026-08-04.** The overflow arithmetic settled as the **share of the round armour could
not absorb, carried into health at the round's health magnitude** — `unabsorbed = (armorDamage −
armourSpent) / armorDamage`, `Hp -= healthDamage * unabsorbed`. Point-for-point 1:1 whenever the
two magnitudes are equal (every collision, 30 of 48 ballistics entries), which is what the "1:1"
in `BL-085` and the spec describes; for an unbalanced round it is 1:1 in *shot share*, the only
generalisation that keeps retail string 3372 true — a bare zone takes exactly `HEALTH_DAMAGE`, so
AP (4.5 armour / 1.5 health) strips armour fast and does little to airframe, while the rejected
"leftover armour points spill as health points" reading would have made AP the *heaviest* round
against a stripped zone. No shipped entry has `ARMOR_DAMAGE == 0` with a nonzero `HEALTH_DAMAGE`
(checked over all 48), so the armour-less-round case is a definition, not a data case: it passes
armour untouched. Decision 6 settled above — `Fraction`/`WorstFraction` are the combined pool.

**Goal.** `PartState` holds independent `Hp` and `Armor`; a single `Apply` call spends armour
first, overflows 1:1 into health **within the same shot**, and never wastes damage on a
nearly-stripped zone. `Fraction`/`WorstFraction` semantics are decided and documented.

**Evidence (confidence: traced).** `PlaneDamage.cs:52-58` is a flat single-pool subtract;
`:69-74`'s `PartState` is one float. Depletion order is settled twice over (Decision 1). The
overflow rule is the spec's, corroborated by `CAP-19`'s stripped-zone observation.

**Approach.** `Apply(partName, healthDamage, armorDamage)`: spend `armorDamage` against `Armor`;
overflow (the part of `armorDamage` beyond remaining armour) plus `healthDamage` against `Hp` —
⚠ confirm the exact overflow arithmetic against the spec wording cited in `BL-085` before coding;
the one-line summary here is a draft, not the source. Keep a single-argument overload for callers
with one magnitude (grazes, until `BL-172`) — decide and document what it means (draft: spends as
health damage with armour still shielding first per the crash block's equal ranges).
`WorstFraction`/`Fraction` are defined as the **combined sequential fraction** — (armour+health
remaining)/(armour+health max) — per Decision 6: the shipped `injure_anims` thresholds (torn-skin
flips, smoke/fire, the cockpit colour cycle) all key off one scalar, and the combined fraction is
the one that makes the manual's yellow band (armour damage only) reachable. **Include Decision 6's
reconciliation in this item:** the shipped fracs are 0.72 / 0.46 / 0.20 (`docs/formats/hud.md`,
"Thresholds") against the manual's combined-scale 0.75 / 0.375 — 0.72 fits, the lower two don't
cleanly. Settle what scale the fracs are on (and whether the manual's prose bands are merely
approximate) and record the verdict in Decision 6 before B11 consumes `Fraction`.

**Model recommendation.** high — small file, but every damage consumer inherits these semantics;
the overflow and fraction definitions are the plan's core judgement calls.

**Verify.** Unit tests: armour-first order; exact 1:1 overflow at the boundary (shot larger than
remaining armour); zero-armour part takes full health damage; `Reset` restores both pools;
`Summary` shows both. `.\RunTests.ps1` full pass.

**⚠ Traps.** (a) The 2× effective pool at stock is **intended** (Decision 3) — do not "fix" it.
(b) Do not touch world destructibles — health-only, out of scope. (c) `Critical` still triggers on
`Hp <= 0`, not on armour — armour at 0 is a stripped zone, not a dead one (`CAP-19`: it falls
*faster*, it doesn't fall *off*).

# Wave B — every existing consumer speaks two-pool

## B11 ☑ Graze path spends through the new `Apply`; flash/log/gauge-blink stay coherent

**Landed 2026-08-04.** `SurviveHit` already called A2's single-magnitude `Apply(dataPart, dmg)`
overload (A2 replaced the flat-subtract call in place) and the flash text already read
`state.Fraction` — the combined armour+health progression — so both were two-pool-coherent before
this item started. The one stale readout was the graze log line, which still printed only
`hp={state.Hp}/{state.Def.MaxHp}`; it now prints `armor={state.Armor}/{state.Def.MaxArmor}
hp={state.Hp}/{state.Def.MaxHp}`. `Gauges?.OnPartDamage(dataPart)` takes no pool value (it's a
by-name 5 s blink trigger) and `FlightAudio`'s `damageFrac` already reads `Damage.WorstFraction` —
neither needed a change. `Crash()` still never consults `PlaneDamage` (unchanged, per Decision 4).

**Goal.** `SurviveHit` spends its severity-scaled damage through the two-pool model; the impact
flash, the graze log line and the gauge blink report something truthful about a zone that now has
two numbers.

**Evidence (confidence: traced mechanism; graze magnitude is TUNE).** `FlightController.cs:1628-1644`
computes `GrazeMaxDamage * (vn/CrashSpeed)²` and calls the single-pool `Apply`; the log prints
`hp={state.Hp}/{state.Def.MaxHp}`. `Crash()` (`:1462-1507`) never consults `PlaneDamage` — leave it
that way (hard hit = boolean destroy is current behaviour; the crash block is `BL-172`'s).

**Approach.** Route the graze magnitude through A2's single-magnitude overload; extend the log line
and `_damageFlashText` to show both pools (or the combined fraction — match whatever A2 defined).
`Visuals?.OnPartDamage(dataPart, state.Fraction)` and `FlightAudio`'s `damageFrac`
(`FlightAudio.cs:177`) consume `Fraction`/`WorstFraction` and inherit A2's combined-fraction
definition **unchanged — that is the point of Decision 6**: `DamageVisuals.OnPartDamage`
(`DamageVisuals.cs:99-141`) fires each `injure_anims` threshold the fraction crosses (pdpanel
torn-skin flips, then the whole-plane `VehicleInjureAnims` smoke/fire), and under the combined
fraction those thresholds now fire during armour depletion too, exactly as the manual's yellow
band implies. Eyeball two things: the damaged-engine loop still ramps sensibly with the 2× stock
pool, and the first torn-skin flip appearing while only armour is spent reads right against the
original (if it doesn't, that's evidence against Decision 6 — report, don't retune).

**Model recommendation.** medium — the semantics landed in A2; this is routing plus readout
coherence in a file that demands care (`FlightController.cs` — read its `docs/architecture.md`
entry first).

**Verify.** `.\RunGame.ps1 --fly`, graze a building: flash and log show the two-pool state; repeated
grazes strip armour before health; a zone at 0 armour degrades visibly faster per graze. Goldens:
baseline first — graze damage per hit is unchanged in magnitude, but zone *lifetime* doubles; any
golden that grazes will move, and that movement is the intended 2× (Decision 3), not a regression.

**⚠ Traps.** Do not re-tune `GrazeMaxDamage`/`GrazeStopSpeed`/`GrazeFriction`/`GrazeKick` here to
compensate for the doubled lifetime — the 2× is faithful; those constants are `BL-172`'s to revisit
alongside `bounce_factor`.

## B12 ☑ Damage lab drives and reads both pools; probes/summaries print both

**Landed 2026-08-04.** `PlaneDamage.Summary()` and the HUD DMG line already printed both pools —
that landed as part of A2/B11 (`PoolText`'s "a{armor%} h{health%}" and `FlightController.cs:949`'s
`Damage?.Summary()`), so the only gap was the lab itself: one slider per part represented the
*combined* fraction, spent through the armor-first shot model, which structurally cannot reach
"armor 0, health full" or the reverse (armor absorbs everything below its own max first). Split
each part into an armor slider (parts the data gives an armor pool) and a health slider, both
independent; `DamageLab` derives the combined fraction the injure_anims thresholds and the gauge
dial key off from the two, and `IDamageLabTarget.Apply`/`Fraction` now carry a `PartFrac`
(Health, Armor, Combined) triple instead of one float. `FlightDamageTarget.Apply` spends each pool
through its own single-pool `PlaneDamage.Apply(part, healthDamage, armorDamage)` call after
`Reset` — armor's call with healthDamage=0, health's with armorDamage=0 — which is what reaches
either extreme. `--damage=part:frac` presets both of a part's sliders to the same fraction; there
is no CLI syntax yet for the two pools independently, recorded as a TUNE, not chased here.

**Goal.** The lab's sliders write and read the two-pool state (armour and health independently per
zone) so C21 can be confirmed on screen; `PlaneDamage.Summary` and any probe dump show both pools.

**Evidence (confidence: traced).** `DamageLab.cs:12,66,118` — sliders are the state's front-end,
writing P1's real `PlaneDamage` in `--fly`/`--stunt`; `DamageLab.cs:66` documents "PlaneDamage only
spends HP". `GameSession.cs:1329` wires the dialled-in state to the HUD DMG line.

**Approach.** Per-zone armour control alongside the health one (or a two-pool readback of the
existing slider — pick whichever keeps the lab's UI conventions; read `DamageLab`'s architecture
entry). Update `Summary()` and the HUD DMG line to a both-pools format.

**Model recommendation.** medium, low effort — UI plumbing over settled semantics.

**Verify.** `.\RunGame.ps1 --fly`, open the damage lab: drive armour to 0 with health full and vice
versa; HUD DMG line and `Summary` distinguish the two states; gauge blink still fires.

**⚠ Traps.** The lab writes the *real* `PlaneDamage` in flight modes — a lab-driven state must
produce exactly the state a graze would (no parallel bookkeeping).

# Wave C — the gauge colour bands (`BL-173`, corrected)

## C21 ☐ Zone colour follows the manual's four-band armour→health thresholds; rings stay in lockstep

**Goal.** Each zone's colour is derived from **both** pools per the manual's Crispen Mark V table
(Decision 5): green = untouched; yellow = up to 50% armour destroyed; orange = 50–100% armour
destroyed and 0–25% airframe destroyed; red = 25–100% airframe destroyed. `Border` and `Fill` keep
receiving the **same** colour index — that part of today's code is faithful, not the bug.

**Evidence (confidence: mapping settled by the manual; edge details direction-sound).**
`GaugeCluster.cs:263-268` computes one `frac = PartFraction?.Invoke(z.Part)` and feeds the same
colour to both rings — the *structure* is right; what's wrong is that `frac` is one pool's
fraction, so the colour thresholds today are bands over health alone instead of over the
sequential armour→health progression. Better still, `GaugeCluster.cs:442-450` already **mines the
band boundaries from the data** — it reads the `*_damage_green/yellow/red` fracs out of each
part's `injure_anims` ("each anim threshold steps to the NEXT color"). If A2's data check
confirms Decision 6, this item may reduce to feeding the mined thresholds the combined fraction —
the manual's bands fall out of the shipped numbers with no hand-authored constants. The band table is the game manual's (Crispen Mark V damage
indicator; recorded in Decision 5). Note the sequential model makes the bands well-defined:
armour-first depletion (A2) means "50% armour gone" and "25% airframe gone" are strictly ordered
states. Normalisation: `BL-085` carries the unrecorded `CAP-19` sub-question — whether the
in-flight readout shows 100% regardless of units bought or scales against the 60-unit cap. **At
stock this is moot** (stock armour == the zrdr value == the pool max), so land fractions of each
pool's own max and record the cap question as the open TUNE it is — settle it whenever the armory
is next on screen, no capture owed.

**Approach.** Replace the single-fraction→colour mapping with a band function over the pair
(armourFrac, healthFrac) — either pass both fractions through `PartFraction`-style callbacks or
one callback returning the pair; match `GaugeCluster`'s binding style, bind in
`FlightRigAssembler`/the lab. Boundary conditions (is exactly-50%-armour yellow or orange?) are a
judgement call — pick, document in code, don't present as the manual's.

**Model recommendation.** medium — contained, but read the module docs first (see trap b).

**Verify.** Damage lab (B12): drive the pools through the sequence and watch the single zone
colour step green → yellow (armour <50% spent) → orange (armour ≥50% spent through health 25%
spent) → red; both rings change together at every step. `.\RunGame.ps1 --fly` graze sequence walks
the same ladder. That ladder matching the manual's table is the `BL-173` sign-off.

**⚠ Traps.** (a) `BL-173`(a) still applies: never a synthetic split from one pool — the inputs are
the two real pools from A2. (b) `GaugeCluster.cs` is at its 3-⚠ cap in `docs/architecture.md` —
`BL-173`(b) records that constraint; check the current cap state before adding any doc note there.
(c) `BL-173` was already closed 2026-08-04 (superseded by this item; `docs/HISTORY.md` records the
refuted ring-split fix shape) — landing C21 needs no backlog bookkeeping, only its HISTORY entry.
(d) Do not "split the rings anyway" as an improvement — lockstep rings are the original's design.
