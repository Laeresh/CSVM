# Milestone 3 — Polish run 4 (impact feedback, test affordances, blast radius)

**ACTIVE PLAN** (written 2026-08-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans.md), when every item lands.

Ten items drawn from `backlog.md` under a **mixed shape**: two test affordances that make the
remaining M3 playtests possible at all, five cockpit-visible feedback fixes whose data already ships
complete, two fidelity/correctness items with a settled mechanism, and one plan-sized mechanic
(explosive radius) that is the last unimplemented half of M3's weapons model. **Every item was
re-verified still-open against both `docs/HISTORY.md` and the code** — a backlog row is not proof
the work is undone, and this pass found four rows that are already landed (see the boundary below).

**Deliberately excluded: anything blocked on a capture of the original (`CAP-nn`) or on a user A/B.**
That rules out the whole TUNE list (`BL-215`/`BL-218`/`BL-200`/`BL-201`), the camera rebuild
(`BL-149`/`BL-150`), the flight-model gaps (`BL-092`–`BL-097`, `BL-147`), the armour layer
(`BL-085`/`BL-173`), graze pushback (`BL-172`), the cloud band (`BL-118`) and the stall ramp
(`BL-148`) — each needs the original at the controls before code can be judged right. Also excluded:
future-milestone work (`BL-068` M4 AI, `BL-067` the configurator, `BL-134` the cutscene player) and
items blocked on an open question (`BL-051` on `BL-099`, `BL-050`'s undecided delta semantics,
`BL-036` zone selection).

**Pre-flight chore, landed with A1's commit:** `backlog.md` still carries rows for **`BL-014`,
`BL-026`, `BL-044`, `BL-159`** — all four landed (`PLAN-m3-polish-2` C23, `PLAN-m3-polish-quickwins`
A2/B13/C22; `docs/HISTORY.md` 2026-07-30) — plus a stale "pending merge" note on `BL-062` whose fix
merged as `bea7947`. Delete all five stale notes; their records are already in `docs/HISTORY.md`.

## Milestone goal

- A dry gun group and a dry pylon can be reached in seconds, and destructible targets can be found
  in the world by eye — so the owed cockpit tests stop being blocked on ammo attrition and on
  hunting for a water tower.
- Every moment something hits the plane, or the plane scrapes something, is seen **and** heard: a
  glancing collision sparks/dusts/splashes with its authored sound, damage sparks at a panel, and a
  hurt airframe carries a second engine loop.
- A gun round that hits the ground leaves smoke; an HE rocket tells dirt and buildings apart.
- Danger Zones score by crossing the authored gate pair, not by clipping a 15 m sphere.
- A rocket damages what is *near* the detonation, not only what its ray struck.

**No damage-number balance changes beyond what the shipped data dictates.** Blast falloff shape and
knockback magnitude are the only invented values in this plan, and both land in the TUNE list rather
than as fact. The armour/health split (`BL-085`) stays out — it is a hypothesis, not a finding.

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `got_hit_anim` is the per-impact spark burst | The defs it names only blink a `nosedamage` cockpit node eight times; the real effect is the `injure_anims` **0.99** entry → `<part>_damage_effects` → `random_gun_impact` (`BL-090` item 2) |
| 2 | The anim-def `proximity_damage` flag drives explosive radius | Measured **`false` on every def install-wide**, all 8 chapters — it is not the mechanism (`BL-086`) |
| 3 | `IMPACT_PROXIMITY` is uniformly a damage radius | Its two largest values are `FLARE` 500 m and `FLASH` 450 m, both `DAMAGE 0` specials — a naive radius × damage loop carpets the map (`BL-086`) |
| 4 | `dzpathN` polygon 0 is the route line | True on only 2 of 15 C4 zones; in the other 13 the route is polygon index 2. **Identify by material class, not index** (`BL-088`) |
| 5 | The `gunhit` smoke just needs the `!IsGun` gate removed | `gunhit`'s `blacksmokepuffer` has no `ACTIVE_STATE 0` stop, so one shared emitter emits **forever** at the last hit (`BL-061` item 1) |
| 6 | The crash fireball leads its explosion sound by 0.5 s | The authored `player_crash_dirt` choreography puts the `Sound` event **before** the fireball calls — do not add a lead (`BL-090`) |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B3, B5, B6, C8, C9 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B4, D10 | The *what* is settled; the *how much* is TUNE — add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only — no mechanism yet** | B7 | Budget for investigation; this may end in a disproof. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
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

### Wave A — test affordances (these unblock the rest)

1. ☑ `BL-028` `--ammo=N` low-ammo start knob (+ the stale-backlog-row sweep)
2. ☑ `BL-029` Colour world objects by class — a findable-targets overlay

### Wave B — impact and damage feedback the data already ships

3. ☑ `BL-090` item 3 A glancing collision is silent — wire `touchdown_default/dirt/water`
4. ☑ `BL-090` item 2 Per-impact spark burst via the `injure_anims` 0.99 entry
5. ☑ `BL-090` item 1 `damaged_engine_sound` — the second engine loop nothing reads
6. ☑ `BL-045` World damage panel is oversized, with a gap above the debris line
7. ❌ `BL-019` HE rocket: buildings and dirt give identical impact effects — **disproven**: the
   lookup differentiates, and `he_ground_effect` (dirt) *calls* `large_fireball` (buildings) itself

### Wave C — fidelity with a settled mechanism

8. ☑ `BL-061` item 1 Gun-impact `gunhit` smoke — throttled plays, each bounded to 0.3 s
9. ☐ `BL-088` Danger Zones score on the authored gate pair, not one sphere

### Wave D — the last unimplemented weapons mechanic

10. ☐ `BL-086` Explosive radius + proximity fuse — rockets stop being direct-hit-only

## Dependency and parallelism notes

A1 and A2 are independent of each other and of everything else, but both **unblock playtests** for
later items (A1 for any empty-cue judgement, A2 for finding destructibles to shoot in B7/C8/D10) —
land them first. Wave B items are mutually independent except for **file contention: B3, B4 and B7
all touch the damage/impact path** — B3 and B4 both edit `FlightController.SurviveHit`/
`DamageVisuals.cs`, B7 edits `Projectile.cs` which C8 and D10 also edit. **Never run B3+B4, or
B7+C8+D10, in parallel worktrees.** B6 (`WorldDamagePanel` UI) and B5 (`FlightAudio`/`PlaneStats`)
are contention-free and can run alongside anything. C9 is isolated to `StuntMission.cs`/`GameZ`
parsing. D10 goes last: it is the largest blast radius (damage numbers, golden churn) and it wants
A2's overlay and C8's per-hit emitter pattern already in the tree.

---

# Wave A — test affordances

## A1 ☑ `BL-028` `--ammo=N` low-ammo start knob

**Goal.** A run can start with a chosen number of rounds per gun group and rockets per pylon, so a
dry group / dry pylon is reachable in seconds instead of after 2000+ rounds — and the empty-clip cue
and the pylon-drain order become testable at the controls.

**Evidence (confidence: traced).** ⚠ **`BL-028` is half-landed and the backlog row does not say so
(user, 2026-08-01).** `weapons.gunAmmoCap` already exists — `Config.GetInt` at
`FlightController.cs:434`, warmed at `Config.cs:218`, 0 = off — and caps every firable **gun**
group's `Capacity` + `Ammo` at rig build. Two gaps remain: **(1) there is no pylon/rocket
equivalent**, so the rocket empty-clip cue (`BL-026`, landed 2026-07-30, still never *heard*) and
the pylon-drain order (`BL-062`) stay unreachable; **(2) it is config-only, and `--det` drops
`config.json` entirely** (`docs/verification.md` DET-8), so no scripted or golden run can use it.
`--infinite-ammo` is the CLI shape to mirror (`SessionSpec.cs:501`, `FlightController.cs:127`,
`WeaponCursor.cs`).

**Approach.** Two small additions, no rework of what exists. (1) Add `weapons.ordnanceCap` beside
`weapons.gunAmmoCap`, applied to each pylon's capacity + load at rig build, following the existing
constant's exact pattern and comment. (2) Add `--ammo=N` to `SessionSpec` as the CLI form that sets
**both** caps and therefore survives `--det`; mutually exclusive with `--infinite-ammo` (last one
wins, log which). Document the flag in `docs/cli.md` (the description of record) and keep the
parser's accepted-flag count and `docs/cli.md`'s index count equal (94 → 95); CLAUDE.md's table gets
a row only if the gloss would otherwise be wrong.

**Model recommendation.** sonnet — mechanical, with an existing flag to mirror end to end.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --ammo=3` and confirm from
`.scratch/logs/` that each group starts at 3 and the `pylon ordnance: pylonN dry` breadcrumb appears
after the third rocket. Full `.\RunTests.ps1` (build → units → in-engine suites → goldens). Golden
hashes must be **unchanged** — `--det` does not imply `--ammo`, so no pinned shot may move.

**⚠ Traps.** (a) **Do not rebuild `gunAmmoCap`** — it works; this item adds the pylon half and a
CLI front door. (b) Match its deliberate choice to cap `Capacity` **as well as** `Ammo` (the gauge
then reads full at the cap and drains from there, and `RefillWeapons` refills to it on respawn) —
capping only the load would desync the gauge, and diverging between guns and pylons would make
`BL-142`'s open `IndicatorLowFrac` question unanswerable. (c) Do not add an ammo default to
`config.json`: it overrules interactive runs while `--det` drops it (DET-8), which is gap (2) above,
not a fix for it. (d) Land the stale-backlog-row sweep (`BL-014`/`BL-026`/`BL-044`/`BL-159`, and
`BL-062`'s pending-merge note) in this item's commit.

## A2 ☑ `BL-029` Colour world objects by class

**Goal.** A debug overlay tints world objects by class — destructible / facade / tower / clutter /
plain scenery — so a target named in a backlog item can actually be found at the controls. The user
could not locate a C2 water tower or the storefront facades and shot filmset panels instead.

**Evidence (confidence: traced).** `BL-029`. The classification inputs all exist already:
`DestructibleRegistry` knows every def with `HEALTH > 0` (`AnimRuntime.cs:214-216`),
`SceneBuilder.SurfaceMeta` carries the per-mesh surface tag, `ClassifyBillboard` separates billboard
kinds, and `ClutterBuilder.Kind` knows clutter. `docs/plans/PLAN-testing.md`'s **D32 Node Lab**
already has a destructibles view to extend rather than a blank slate.

**Approach.** Prefer extending the Node Lab's destructibles view with a colour-by-class mode over a
standalone overlay — one panel, one key, no new lab. Drive it from an unlit override material per
class (the `--tex-census` per-name flat-colour path is the closest existing pattern; reuse its
colour assignment rather than inventing a second one). Gate it behind the existing freecam/anim-lab
availability, print the class→colour legend to the log once, and leave flight untouched.

**Model recommendation.** sonnet — additive debug UI over existing classification data.

**Verify.** `./RunGame.ps1 --freecam --chapter=C2` with the mode on: the SeaHangar, the water tower
and the storefront facades must each read as a distinct colour, and a scripted
`RunProbe.ps1 --screenshot=./.scratch/classoverlay_c2.png` confirms it renders headless. 8-chapter
freecam regression: zero errors, unchanged mesh/node counts. Goldens unchanged (debug-only path).

**⚠ Traps.** (a) A destructible is `HEALTH > 0` — the C2 SeaHangar doors are **not** one
(`BL-009`), so an overlay that shows them as destructible would be lying; colour them as whatever
they are (animated scenery) and let that be the finding. (b) Do not colour by *texture* class —
`SurfaceMeta` answers "what does a bullet do here", not "what is this object". (c) Counting what the
overlay tints is not evidence it drew — check a pixel census or a screenshot, not a log line
(`docs/verification.md` LOG-2 family).

---

# Wave B — impact and damage feedback the data already ships

## B3 ☑ `BL-090` item 3 — a glancing collision is silent

**Goal.** Scraping a surface produces its authored per-surface reaction: sparks off hard surfaces,
dust off dirt, a splash off water, each with its own sound — instead of today's silent, effectless
slide.

**Evidence (confidence: traced).** `touchdown.zrd.json` ships `touchdown_default` /
`touchdown_dirt` / `touchdown_water` (sequences `glance_spark` / `glance_dust` / `glance`),
activating `spark_touchdown` / `dust_touchdown` / `splash_touchdown` with `snd_exp_ground_b` /
`snd_exp_water_b`. **`grep -ri touchdown CSVM/src` returns zero hits** (re-verified 2026-08-01), and
`FlightController.SurviveHit` plays no audio and shows nothing — only the fatal `Crash()` sounds.
The consumer already exists: the D32 world-effects runtime plays a named effect at a point.

**Approach.** In `SurviveHit`, classify the struck collider with the same
`SceneBuilder.SurfaceMeta` read that `BL-059`'s water-crash work needs (`Projectile.ClassifySurface`
at `Projectile.cs:782` is the existing per-surface classifier — reuse it, don't write a third one),
pick the matching `touchdown_*` def, and play it at the contact point through the world-effects
runtime plus its authored sound. Rate-limit to one per contact event so a long scrape does not
restart the def every frame.

**Model recommendation.** opus — it touches the collision path and picks the surface-classification
seam that a later water-crash item will inherit.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1B` — graze dirt, a building and the sea
and confirm three visibly different reactions with sound. Scripted: a `--det` screenshot at a scripted
graze pose, plus `.\RunTests.ps1` green. 8-chapter freecam regression unchanged.

**⚠ Traps.** (a) Confirming a def *started* is not confirming it rendered — check a `Puffer` was
built (`docs/verification.md` WORLD-12 / `BL-046`'s closure limit: an effect name outside the
world-effects runtime's bound set starts, logs, and draws nothing). (b) Do not fold the water-crash
`ClassifySurface` fix (`BL-059`) into this item — same classifier, different call site, and the crash
path has its own regression surface. (c) One live instance per effect def is a known limitation
(`BL-061`) — a scrape and a simultaneous second player's scrape will collapse; note it, don't fix it
here.

## B4 ☑ `BL-090` item 2 — the per-impact spark burst

**Goal.** Taking damage sparks visibly at a panel on the airframe, at the moment of the hit.

**Evidence (confidence: direction sound, magnitude a judgement call).** The effect is the
**`injure_anims` 0.99 entry** → `<part>_damage_effects` → `random_gun_impact` →
`yellow_sparks_follow` at a randomly chosen `pdpN` panel. `DamageVisuals.cs:194-195` explicitly skips
both. `PlaneStats.cs:192-197` already parses def-level `injure_anims` as `[frac, animName]` pairs.
It is **not** `got_hit_anim` (parsed and unread at `PlaneStats.cs:36,232`) — those defs only blink a
`nosedamage` cockpit node, which `GaugeCluster.OnPartDamage` already approximates.

**Approach.** Stop skipping the 0.99 entry in `DamageVisuals`, resolve the named def through the
world-effects runtime, and anchor it at the randomly chosen `pdpN` panel node on the player rig. The
random panel choice must derive from the master seed (`--seed`, pinned to 1 by `--det`) so scripted
captures reproduce — `docs/verification.md`'s seeded-RNG rule.

**Model recommendation.** opus — the threshold semantics and the seeded anchor choice are judgement,
not transcription.

**Verify.** `./RunGame.ps1 --viewer` damage lab (**H**), slide HP just under 0.99 and confirm sparks
at a `pdpN` panel; then a live graze in flight. `--det` screenshot for the golden surface;
`.\RunTests.ps1` green.

**⚠ Traps.** (a) 0.99 fires on the *first scratch* — do not re-tune the threshold to make it fire
less; it is authored data. (b) An unseeded run picks a different panel each time and will look like
a bug (`BL-061`'s `RANDOM_WEIGHT` census trap, same family). (c) Do not let this and
`GaugeCluster.OnPartDamage`'s cockpit blink drift into one abstraction — different data, different
surface.

## B5 ☐ `BL-090` item 1 — `damaged_engine_sound`

**Goal.** A hurt airframe sounds hurt: `snd_damagedengine` blends in over the healthy engine loop as
damage accumulates.

**Evidence (confidence: traced).** `basic_airplane` carries
`damaged_engine_sound: [["snd_damagedengine", 0.0, 1.0]]`, so **every** plane inherits it, and
`snd_damagedengine` is a LOOPED 3D loop (`RANGE [130,420]`). `PlaneStats.Load` reads only
`engine_sound` (`PlaneStats.cs:190`); `grep -ri damaged_engine CSVM/src` returns zero hits
(re-verified 2026-08-01). Same evidence as `BL-161`, which also names the unparsed
`cockpit_engine_sound` — that one stays out (it needs a cockpit view).

**Approach.** Parse the field in `PlaneStats.Load` beside `engine_sound`, and blend it in
`FlightAudio` as a second loop whose gain rises with accumulated damage. **The two trailing floats
are unlabelled and undecoded** — plausibly a health-fraction fade window; state which reading you
adopt in the code comment and add the mapping to `backlog.md`'s TUNE list rather than asserting it.
Wire the mix gain through `Config` (`Config.GetFloat("flightAudio.…", …)`) the way `BL-159` wired
`WhineMixGain` — so it can be tuned without a rebuild.

**Model recommendation.** sonnet — the parse and the blend both have an exact in-repo pattern to
copy; the undecoded floats are the only judgement, and the plan already says to park them as TUNE.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1` — take damage and confirm the second
loop rises audibly. `--dump-config` must show the new gain key. `.\RunTests.ps1` green.

**⚠ Traps.** (a) `Config.WarmTuningRegistry` does not touch `FlightAudio`, so the key may not appear
in `--dump-config` for free — `BL-159`'s trap (a) documents the fix. (b) Delete any `config.json`
override after tuning: it overrules interactive runs while `--det` drops it (DET-8), so a session
would quietly diverge from every golden while looking identical on paper. (c) `snd_damagedengine` is
3D-ranged in the data but own-ship engine audio is deliberately non-positional
(`FlightAudio.cs`) — keep it non-positional and say why, rather than "fixing" it to 3D.

## B6 ☑ `BL-045` World damage panel layout

**Goal.** The world damage panel reads as one compact block, with no gap between the no-controls
notice and the debris line.

**Evidence (confidence: traced).** `BL-045`, from the Wave D playtest 2026-07-25: layout only — the
readout itself was called comprehensible. The panel is larger than it needs to be and carries a gap
above the debris line.

**Approach.** Pure layout in the world damage panel: tighten container margins/separation and remove
the empty row. Change no strings and no readout content.

**Model recommendation.** sonnet, low effort — cosmetic layout with a stated target shape.

**Verify.** `./RunGame.ps1 --freecam --chapter=C1`, click a destructible, **H** — one compact block,
no gap. A `--det` screenshot before/after for the record. `.\RunTests.ps1` green; goldens unchanged
(no golden frames the panel).

**⚠ Traps.** Do not "improve" the readout's wording or fields while in there — the user called the
content comprehensible, so any content change is unrequested scope and loses the one signal this
item has.

## B7 ❌ `BL-019` HE rocket: buildings vs dirt impacts are identical — disproven

**Goal.** An HE rocket hitting a building gives a fireball; hitting dirt gives the light flash — the
two visibly telling apart, as in the original.

**Evidence (confidence: lead only — this may end in a disproof).** `BL-019`: the original uses
`large_fireball` for buildings and `he_ground_effect` (a light flash, no puff) for dirt. The
per-surface lookup **exists** (`Projectile.cs:490`) and is not differentiating. Two candidate causes,
neither confirmed: the weapon's `Impact` may not carry distinct `buildings` vs `default` entries, or
terrain may not tag such that dirt classifies `Default` while a building classifies `Buildings`
(`ClassifySurface`, `Projectile.cs:782`). Note `Projectile.cs:1139` already special-cases
`SurfaceClass.Buildings` **for guns** — so the classifier does distinguish them on at least one path,
which sharpens the question to the rocket's own `Impact` table.

**Approach.** Investigate first, in this order: (1) dump the HE rocket's `Impact` entries and check
whether `buildings` and `default` are actually distinct in the data; (2) if they are, trace why the
lookup collapses; (3) if they are not, the finding is that the data does not differentiate and this
item closes ❌ with a `docs/formats/weapon-effects.md` note. Only write code after (1) answers.

**Model recommendation.** opus — it is a diagnosis with a real chance of ending in a disproof, and
the wrong move (hand-assigning effects per surface) would be content invention.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets` — a building fireball
and a dirt flash, visibly different. Scripted: `--det --screenshot` at a scripted rocket impact on
each surface, paired with `--no-fog`. `.\RunTests.ps1` green.

**⚠ Traps.** (a) Do not hand-map effect names onto surfaces to force a difference — if the data does
not differentiate, that is the answer. (b) `BL-019` is explicitly a *separate* item from
`PLAN-m3-polish-2`'s impact work and from `BL-203`'s gun-impact looks; do not re-open either. (c) An
effect name outside the world-effects runtime's bound set starts, logs and draws nothing
(`BL-046`/WORLD-12) — confirm a `Puffer` was built before concluding the lookup is at fault.

**Outcome (2026-08-01): ❌ closed as a disproof — no behaviour change.** Step (1) answered against
the item: `wep_06`'s `IMPACT` **does** carry distinct entries (`buildings` → `ANIMATION
large_fireball`, `default` → `SURFACE_ANIMATION he_ground_effect`), and step (2) found the lookup
does not collapse either — a C1 hit on the `g306` hangar wall logs `-> Buildings … fx=large_fireball`
and a terrain hit 60 m away logs `-> Default … fx=he_ground_effect`. The premise that dies is the
*expected* difference: `he_ground_effect` **`CALL_ANIMATION`s `large_fireball` itself** at
`AT_NODE he_ring, 0, 12, 0`, plus the ring stack, two trail columns, two flash lights and a
framebuffer flash. Dirt is a **superset** of buildings, not the lighter alternative `BL-019`
assumed, so "both a fireball puff" is the authored behaviour and the requested asymmetry could only
be produced by deleting a call the data makes — trap (a). Landed: the `fx=`/`snd=` fields on the
impact breadcrumb (the missing instrument, INSTR-5), `docs/formats/weapon-effects.md`'s finding,
`analysis/surface-classification/class_area_share.py` + the per-chapter class-area table, and
WORLD-20. `.\RunTests.ps1` green (320 units, 14 suites, 13 goldens hash-identical).

---

# Wave C — fidelity with a settled mechanism

## C8 ☑ `BL-061` item 1 — gun-impact `gunhit` smoke

**Goal.** Gun rounds striking a surface leave the authored `gunhit` smoke, and it stops — no
permanent emitter parked at the last hit.

**Evidence (confidence: traced).** `ProjectilePool.EffectSink` is gated `!weapon.IsGun`
(`Projectile.cs:1127`), so gun hits never reach the effect sink. The reason the gate exists is the
real blocker: `gunhit`'s `blacksmokepuffer` has **no `ACTIVE_STATE 0` stop**, so one shared emitter
(`_puffers` keyed `(name, host)` over a single `gunhit` template root) would emit **forever** at the
last hit. The plan's original assumption — that the shared per-round emitter would collapse onto one
puff — was wrong.

**Approach.** Give gun hits a **per-hit** emitter that copies (not relocates) the template and
self-expires: either a small pool of `gunhit` roots cycled per hit, or a burst-mode puffer with a
bounded life. Prefer whichever composes with the existing `DefScopedPufferKeys` work (A1,
2026-07-31) rather than adding a third keying scheme. Rate-limit per firing group so a held trigger
does not spawn an emitter per round.

**Model recommendation.** opus — the lifetime/pooling design is the whole item, and getting it wrong
leaks emitters at the game's highest event rate.

**Verify.** `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo --fire` — hold the
trigger on a building, confirm smoke appears at each hit and **fades**, and that emitter count
returns to baseline after firing stops (log the count; a leak is the failure mode). `--effects-test`
census **seeded** — several gun `*_gunhit` variants gate their puffer behind `RANDOM_WEIGHT`, so an
unseeded run reports a different set each time. `.\RunTests.ps1` green.

**⚠ Traps.** (a) Do not simply drop the `!IsGun` gate — that is the exact fix the evidence rules
out. (b) The WORLD runtime deliberately keeps the collapsed `(name, host)` key (def-scoping it
stacked C5's six `m_crane_go` spark defs on one node and **moved the c5 golden**) — do not
"unify" the two keying schemes as a cleanup. (c) An unseeded `--effects-test` manufactures its own
answer; seed it or the census is noise.

**Outcome (2026-08-01).** Landed, but not as a per-hit copy — the census that had to run first
changed the shape. Of the **12** `gunhit` defs (caliber 3040/5060/70 × ammo slug/dum/ap/mag), **9
stop their own emitter**: ap/dum at `EVENT_OFFSET` +0.1 s, mag at +0.3 s. Only the three `*slug_gunhit`
defs ship no `ACTIVE_STATE 0` — and slug is the stock ammo on every gun, so the evidence above is
right about the case that matters and wrong about the family. With the puffer particles already
world-space (`TopLevel`), a relocated shared template gives a per-hit puff on its own; what was
missing was a bound. So: a per-call `ttl` on `PlayEffectAt` (guns pass **0.3 s** — the family's own
longest authored stop, and below `blacksmokepuffer`'s 1.1 s `TIME_INTERVAL`, so a hit is one puff)
plus a **0.1 s** per-effect-name throttle, which is per firing group. Per-call template instancing
stays `BL-225`, unchanged in scope. Verified: `--effects-test` 30/33 build a puffer (all 12
variants); a C1 strafing run logs `blacksmokepuffer` active while firing and **zero** emitters a
second after it stops, against an able-to-fail control (TTL 60 s → 2 emitters still growing at
t=3 s). `.\RunTests.ps1` green, 13 goldens hash-identical. New trap: WORLD-24 — the effect's own
`PLAYER_RANGE 500` gate makes a probe flown at normal standoff show nothing at all.

## C9 ☐ `BL-088` Danger Zones score on the gate pair

**Goal.** A Danger Zone is cleared by crossing its two authored gate apertures in order — so flying
*around* the danger no longer scores, and a tangential clip cannot count.

**Evidence (confidence: traced).** `StuntMission.Update` tests one point against `DzRadius` 15 m,
order-free (`StuntMission.cs:65,209`). The `dzpathN` mesh's structure is measured across all 54 zones
in C1/C1B/C2/C3/C4/C5: 3 polygons, of which **two share one material and are the entry/exit
apertures** (C4: material index 427, solid red 243/0/0) and the odd one out is the route line
(material 84, solid white). The spec confirms both apertures must be crossed. `DzRadius` 15 m is the
user's hand-tuned value and is reported as too tight at some zones and too loose at others — which
is the symptom a gate test removes rather than retunes.

**Approach.** Replace the sphere test with an ordered pair of polygon-plane crossings, identifying
the gate polygons **by material class, not by index** (the route is polygon 0 on only 2 of 15 C4
zones). Keep the `dzN` marker as a HUD anchor only — `MarkerHud.cs:144,145,158,212` and the
scoreboard still consume the marker point, and the 826 m C1-dz2 marker↔gate discrepancy **stops
mattering** under a gate test. Delete `DzRadius`'s docstring line claiming "the original has no gate
geometry" — it is false.

**Model recommendation.** opus — geometric test design plus a scoring-rule change that a
splitscreen race depends on.

**Verify.** `./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury` — gates trigger where the danger
is, a clean miss scores nothing, and no zone is ungatable. Add an in-engine assertion suite over a
scripted flight path through and beside a known zone, so the "flew around it" case is machine-checked.
`.\RunTests.ps1` green.

**⚠ Traps.** (a) Do not retune `DzRadius` as part of this — 15 m is the user's hand-tuned value and
the gate test is what replaces the need for it. (b) Do not "fix" the `dzN` marker onto the gates; its
consumers are listed above and it is hand-placed by design. (c) **Ordering across zones is a
separate, unsupported case** — shipped `dzones` is a bare `[dzpathN, dzN]` pair list with no order
field, so any mission-level ordering was engine-side; our model stays order-free *between* zones.
The order this item adds is only entry-then-exit *within* one zone.

---

# Wave D — the last unimplemented weapons mechanic

## D10 ☐ `BL-086` Explosive radius and proximity fuse

**Goal.** A rocket damages everything inside its blast, with falloff, and detonates at its authored
fuse distance rather than only on contact — so rockets stop being direct-hit-only.

**Evidence (confidence: direction sound, magnitude a judgement call).** `ProjectilePool.SimStep`
does one `IntersectRay` per round per step and `Impact` spends `weapon.HealthDamage` on the single
struck collider (`Projectile.cs:428-431,534`). `ImpactProximity` (14 entries, 15–500 m) and
`DetonationDistance` (13 entries, 1–50 m) are parsed (`WeaponDefs.cs:87-88,244-246`) and only
**printed** — `Probes.cs:158` and the weapon lab are their sole consumers. The two fields are
independent: the torpedo is a 1 m fuse with a 30 m blast. Falloff shape and knockback magnitude are
spec, not data.

**Approach.** At detonation, query bodies within `IMPACT_PROXIMITY` and apply `HealthDamage` scaled
by linear falloff to **every damage zone inside the sphere**, plus knockback on the struck body. Add
a proximity fuse that detonates at `DETONATION_DISTANCE` before contact, gated by
`DETONATION_DOT_PRODUCT` on the three entries that carry it. **Exclude the zero-damage specials** —
`FLARE` (500 m) and `FLASH` (450 m) carry `DAMAGE 0` instead of the armour/health split, and a naive
radius × damage loop over them carpets the map. Record the falloff shape and knockback magnitude in
`backlog.md`'s TUNE list; they are invented values, not data.

**Model recommendation.** opus — the largest blast radius in this plan, on damage numbers that
goldens and the damage suites both observe.

**Verify.** Take a baseline first: an unchanged number proves nothing unless you have seen it able to
fail (`docs/verification.md`). Then `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets`
— a near-miss on a cluster of destructibles must damage several, and a torpedo must fuse at 1 m and
blast at 30. Add in-engine assertions for the falloff curve and the specials exclusion.
`.\RunTests.ps1` green; **expect and explain any golden movement** rather than regenerating silently.

**⚠ Traps.** (a) The anim-def `proximity_damage` flag is **`false` on every def install-wide** — it
is not the mechanism and must not be wired as one. (b) World destructibles carry **health only**
(measured over 16,114 defs) — this item does not touch them with an armour concept. (c) Do **not**
implement the armour layer (`BL-085`) here: where the armour pool comes from is a hypothesis
(`destroyable_parts`' second value is equal to the first on all 22 defs), and adopting it doubles
every part's effective HP — a balance change, not a drop-in. (d) `IMPACT_PROXIMITY` is not uniformly
a damage radius (trap 3 in the table above) — gate on the entry carrying real damage.
