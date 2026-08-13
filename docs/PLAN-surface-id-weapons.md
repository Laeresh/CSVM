# Surface id, part two — the weapon IMPACT table and the collision overlay

**ACTIVE PLAN** (written 2026-08-13). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

`PLAN-crash-surface-id` moved the player crash and the `touchdown_*` graze onto the original's
numeric surface id and closed on 2026-08-13. It deliberately scoped weapons out, on a premise that
has since been decoded false. This plan finishes the job: the weapon IMPACT effect keys off the same
surface id the original keys it off (`BL-344`), and the collision overlay stops colouring colliders
by a name space that no longer decides anything (`BL-345`).

Two boundaries. This plan does not touch the crash or touchdown cascades, which landed and are
verified; `SurfaceDefTable` is read as the reference implementation of the id lookup, not modified.
It also does not model the `ai_crash_<name>` family (a third registry-indexed vector found at
`FUN_00478a00` while decoding `BL-344`, still unmodelled): that is a separate item and stays in
`backlog.md`, where it is now `BL-347` (minted when B12 retired `BL-344`).

**Backlog provenance.** `BL-344` and `BL-345` were both re-verified still-open **against the code**
in this session: `Projectile.ClassifySurface` (`CSVM/src/Flight/Projectile.cs:456-469`) still reads
the texture-derived `SceneBuilder.SurfaceMeta`, and `ColliderOverlay.ClassOf`
(`CSVM/src/UI/ColliderOverlay.cs:182-203`) still does the same.
Done before Wave B landed: `git log --all --grep=BL-344` and `--grep=BL-345` return only this
plan's own commits plus the sibling plan's `B12` (which cites `BL-344` as out of scope) and the
`BL-345` renumber, so neither had been quietly fixed under another item. `BL-344` was retired from
`backlog.md` by this plan's `B12`; `BL-345` closes with `C21`.

## Milestone goal

- A round or a rocket striking geometry selects its impact effect by the struck material's numeric
  surface id, through the surface registry, exactly as `FUN_005ad100` does.
- `SurfaceClass`, the six-member texture-derived enum, no longer exists as a parallel name space;
  the registry's fourteen ids are the one key.
- `--collision=show` colours each collider by the surface id that decides its behaviour, with a
  legend naming id and registry name together, and shows the id each collider *resolves to* rather
  than the raw stamp.

**This plan does not re-open the crash or touchdown families.** They landed under
`PLAN-crash-surface-id`, were playtested, and their cascade (`SurfaceDefTable`) is this plan's
reference, not its subject.

## Decisions (2026-08-13)

| # | Question | Decision |
|---|---|---|
| 1 | Do `BL-344` and `BL-345` land separately? | **No, one plan.** `BL-344`'s fix collapses `SurfaceClass` into the registry, which is the same edit the overlay needs to colour by id. Landing the overlay first would build it against a type about to be deleted. |
| 2 | Does `PLAN-crash-surface-id` Decision 3 ("keep the texture-name classifier, for weapons only") survive? | **No, it is disproven.** Decoded 2026-08-12: the IMPACT table is registry-indexed at parse time and soil-id-indexed at runtime. Decision 3 was taken on the premise this decode kills. |
| 3 | What happens when a weapon authors no IMPACT block for the struck id? | **Faithful: plays nothing (decided 2026-08-13, after A1).** A1 counted zero authoring weapons on `dirt`(13)/`fire`(5)/`airstrip`(8)/`dzone`(12); per-chapter silent-ground share runs C1 6.4%, C1B ~0%, C1C 0%, C2 10.6%, C2B 0%, C3 2.8%, C4 9.6%, C5 ~0% (worst: C2). B11 removes `ImpactOutcome`'s `SurfaceClass.Default` fallback rather than keeping it as a marked divergence. |
| 4 | Does the overlay show the raw stamped id, or the id it resolves to? | **The resolved one.** The empty-slot arm is the mechanism, so an overlay that hides it re-creates the confusion `PLAN-crash-surface-id` removed. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "The weapons IMPACT keys are a different name space from the surface registry." (`PLAN-crash-surface-id` scope note, Decision 3) | `FUN_005ad630` `0x005ae1ea`–`0x005ae24e`: on the `IMPACT` token the parser matches the block name against `registry[0]`, then walks `&registry[1]` to the registry count at `[0x00637b10]`. It is the same registry, walked by name, into an id-indexed array. |
| 2 | "`buildings` is not a surface-registry name at all." (same note) | `buildings` is registry id 11, from the soils list in `ZBD/zrdr.zbd` at `0xe631c`. `PLAN-crash-surface-id`'s own data section already said so. |
| 3 | "The impact path is name-keyed at runtime." | `FUN_005acf60` computes `surfaceId = hit->material ? *(int *)(material + 0x20) : 0` and calls `FUN_005ad100`, which indexes `weapon[0x15c] + surfaceId * 100`. The same material field and the same null-to-0 arm the crash and touchdown cascades use. |
| 4 | "The IMPACT fallback works like the crash cascade's." | `FUN_005ad100` gates on the row's own variant count at `+0x2c` and, when it is zero, plays nothing. There is no empty-row-to-row-0 arm. This is the difference with real consequences, and it is why A1 and A2 exist. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, B11, B12, C21 | Confirm the trace, then implement. The decode is written up in `analysis/surface-classification/FINDINGS.md` (2026-08-12, second section, with the A1 count in the 2026-08-13 section) with every address. |
| **A decision, not a finding** | A2 | Do not implement past it. It is the user's call. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy. This plan is being written in the
`crash-surface-decode` worktree.

## What the data actually ships

**The registry, fourteen ids in slot order** (`CSVM/src/Mech3/SurfaceRegistry.cs:51-55`, with its
full provenance in that file's doc comment):

```
0 default   1 water    2 seafloor   3 quicksand  4 lava     5 fire     6 player
7 enemy     8 airstrip 9 opensesame 10 death     11 buildings 12 dzone  13 dirt
```

Ids 0 to 5 are compiled into `crimson.exe` (count at `0x00637b10`, `char*` array at `0x00637b14`);
ids 6 to 13 are appended at load by `FUN_0055b7b0` from `ZBD/zrdr.zbd` at `0xe631c`.

**What we have instead, today.** `SurfaceClass` (`CSVM/src/Flight/WeaponDefs.cs:10`) is a six-member
enum: `Default, Water, Buildings, Player, Enemy, Quicksand`. `WeaponDef.Impact`
(`WeaponDefs.cs:55`) is a `Dictionary<SurfaceClass, WeaponEffect>`, filled by `ParseImpact`
(`WeaponDefs.cs:333`) through a name-to-enum map (`Surfaces`, `WeaponDefs.cs:163`). At runtime
`Projectile.ClassifySurface` (`Projectile.cs:456`) derives that class from the texture-derived
`SceneBuilder.SurfaceMeta` tag, and `ImpactOutcome.Resolve`
(`CSVM/src/Flight/ImpactOutcome.cs:52`) falls back to `SurfaceClass.Default` when the struck class
has no entry (`ImpactOutcome.cs:57`). That fallback is the unfaithful arm Decision 3 covers.

**What the shipped weapons author (A1, counted).** Source: `extracted/zrdr/weapons.zrd.json` (48
`BALLISTICS` entries; `CSVM.Tests/fixtures/zrdr/weapons.json` is a synthetic unit fixture, not this
data). Six registry names appear as `IMPACT` keys anywhere in the data — `default`(0) and
`water`(1) on 47/48 weapons, `buildings`(11) on 47/48, `player`(6) on 44/48, `quicksand`(3) on 3/48
(`wep_04`/`wep_25`/`wep_27`), `enemy`(7) on 31/48 but populated (non-null) on only 3
(`wep_01`/`wep_02`/`wep_03`) — the other 28 author an empty `enemy` row, which parses into nothing
by both sides. **The claimed dead `fault` block does not exist in the shipped data at all** (zero
occurrences of the literal, word-bounded); the eight ids no weapon authors in any form are
`seafloor`(2), `lava`(4), `fire`(5), `airstrip`(8), `opensesame`(9), `death`(10), `dzone`(12),
`dirt`(13). Full per-id and per-weapon tables, and the correction to the `fault` claim, are in
`analysis/surface-classification/FINDINGS.md`'s 2026-08-13 section.

**The blast radius of deleting `SurfaceClass`.** Beyond `Projectile` and `ImpactOutcome`, it is
referenced by `WeaponLab` (`CSVM/src/UI/WeaponLab.cs:558, 595-621`, which has its own
name-to-class map for target selection), `WorldEffectsFactory.cs:393` and `GameSession.cs:728`
(both a water predicate, `ClassifySurface(body) == SurfaceClass.Water`), and `Suites.cs:2091` and
`:4383`.

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

### Wave A — count the data, then take the decision

1. ☑ Survey which IMPACT blocks the shipped weapons actually author, per id
2. ☑ Settle the empty-row rule with the user (plays nothing vs falls back to row 0)

### Wave B — move the impact key onto the surface id

11. ☑ Key the IMPACT table by surface id at parse time and at impact
12. ☑ Retire `SurfaceClass` at its remaining call sites

### Wave C — the overlay

21. ☑ Colour the collision overlay by resolved surface id (`BL-345`)

## Dependency and parallelism notes

Strictly linear at the wave level. A1 measures what A2 decides; A2 gates B11, which is the only item
whose *shape* the decision changes. B11 → B12 is a chain (B12 removes the type B11 stops using), and
C21 must follow B12 or it will be written against a deleted enum.

File contention: B11 and B12 both edit `Projectile.cs` and `WeaponDefs.cs`, so they never run in
parallel worktrees. C21 owns `ColliderOverlay.cs` alone, but it depends on whatever registry-lookup
helper B11 introduces, so it still runs last rather than concurrently.

---

# Wave A — count the data, then take the decision

## A1 ☑ Survey which IMPACT blocks the shipped weapons actually author, per id

**Goal.** A table, per weapon, of which registry ids have an authored IMPACT row, so the size of the
"plays nothing" change is a number rather than a fear.

**Evidence (confidence: traced and counted).** `FUN_005ad630`
`0x005ae1ea`–`0x005ae24e` parses each IMPACT block by matching its name against the registry and
writing into `weapon + 0x15c + i*100`, so a block whose name is not a registry name is parsed into
nothing. The decode write-up (`analysis/surface-classification/FINDINGS.md`, 2026-08-12, the second
section) stated the shipped set as `default`/`water`/`quicksand`/`player`/`buildings` plus a dead
`fault`, and explicitly said to verify that against the extracted data before building on it. Done:
the extracted data (`extracted/zrdr/weapons.zrd.json`, 48 weapons) confirms `default`, `water`,
`quicksand`, `player` and `buildings`, plus a sixth name (`enemy`) the decode's claim missed — and
disproves the `fault` half: no weapon authors an `IMPACT` block by that name; it appears nowhere in
the shipped data. Full tables in `FINDINGS.md`'s 2026-08-13 section.

**Approach.** Read the extracted weapons data directly and count. Report per weapon and per id, and
separately report the ids that are authored by **no** weapon: those are the ids where the faithful
implementation goes silent. Cross-check against the A1 area measurement in
`PLAN-crash-surface-id` (slot 0 at 63.9 to 96.8 % of collidable area) to convert "which ids" into
"how much of the ground you actually shoot at".

**Model recommendation.** medium, low effort. It is a counting pass over one JSON file with a
clearly stated question; the judgement is all in A2.

**Verify.** The count is the deliverable, so the check is internal consistency: every name the data
authors either maps to a registry id or is reported as dead, with no third category. Done: all six
names the data uses (`default`/`water`/`quicksand`/`player`/`enemy`/`buildings`) map to registry
ids, and there are zero unmapped names — so the planned "dead list" the `fault` anchor was meant to
populate is empty by construction. `fault` does not appear anywhere in `weapons.zrd.json`; the known
anchor for the counting pass is instead the eight ids with zero authoring weapons at all
(`seafloor`, `lava`, `fire`, `airstrip`, `opensesame`, `death`, `dzone`, `dirt`), four of which
(`fire`/`airstrip`/`dzone`/`dirt`) match the 2026-08-12 section's independent claim about where a
faithful build goes silent.

**⚠ Traps.** Do not stop at "which names appear". The question is which *ids* have rows, and a name
that appears on only one weapon still leaves every other weapon silent on that id. Report per
weapon, not as a union. Done: both the per-id and per-weapon tables are in `FINDINGS.md`.

## A2 ☑ Settle the empty-row rule with the user (plays nothing vs falls back to row 0)

**Goal.** A recorded decision, in this file's Decisions table, on whether a round striking geometry
whose id has no authored row plays nothing (faithful) or plays the `default` row (today's
behaviour).

**Evidence (confidence: a decision, not a finding).** The mechanism is decoded and not in question:
`FUN_005ad100` reads the row's variant count at `+0x2c` and plays nothing when it is zero, and there
is no empty-row-to-row-0 arm, unlike `FUN_0048b920`'s crash cascade which resolves slot 0. What is
in question is whether to ship that, given A1's numbers. Ours currently resolves `default`
(`ImpactOutcome.cs:57`).

**Approach.** Put A1's table in front of the user with the consequence stated in cockpit terms: on
`dirt`(13), `fire`(5), `airstrip`(8) and `dzone`(12) geometry, a faithful build draws no impact
effect at all where today it draws the `default` one. Anything kept for looks goes to `backlog.md`
as a marked, deliberate improvement, never as an unmarked divergence, which is the rule
`PLAN-crash-surface-id` Decision 7 set.

**Model recommendation.** medium. Presenting a measured trade-off, not making it.

**Verify.** Not applicable; the deliverable is a row in the Decisions table above, dated, before
B11 starts. Done: Decision 3 recorded 2026-08-13, faithful (plays nothing).

**⚠ Traps.** Do not treat this as an implementation detail and pick the faithful path silently. The
sibling plan hit the same shape at its Decision 6 and it was that plan's largest behavioural change.
Done: put to the user directly with A1's per-chapter numbers; not picked silently.

# Wave B — move the impact key onto the surface id

## B11 ☑ Key the IMPACT table by surface id at parse time and at impact

**Goal.** `ImpactOutcome` selects its effect by the struck body's `SceneBuilder.SurfaceIdMeta`,
resolved through `SurfaceRegistry`, with the fallback A2 chose.

**Evidence (confidence: traced).** Parse time: `FUN_005ad630` builds an array indexed by surface id,
stride 100 bytes, by walking the registry (`0x005ae1ea`–`0x005ae24e`). Runtime: `FUN_005acf60`
computes `surfaceId = hit->material ? *(int *)(material + 0x20) : 0` and `FUN_005ad100` indexes
`weapon[0x15c] + surfaceId * 100`, then picks a variant from `+0x30[k]` by `rand()` over the count
at `+0x2c`; `FUN_005ad160` does the same over a second list at `+0x40`/`+0x44`. `FUN_005ad330`
independently tests `*(material + 0x20) == 1` (water) on the impact path, so the weapon code reads
the soil id in two places. Our side: `WeaponDef.Impact` is
`Dictionary<SurfaceClass, WeaponEffect>` (`WeaponDefs.cs:55`), filled through the name map at
`WeaponDefs.cs:163`, and the id is already stamped on every collider body by
`SceneBuilder.SurfaceIdMeta` (`CSVM/src/Mech3/SceneBuilder.cs:41, 950`).

**Approach.** Parse `IMPACT` into a registry-indexed structure instead of the enum-keyed dictionary,
matching names through `SurfaceRegistry` so an unmatched name (`fault`) is discarded exactly as the
original discards it. At impact, read `SurfaceIdMeta` off the struck body the way
`FlightController` already does (`CSVM/src/Flight/FlightController.cs:1245-1246`), including the
null-material-to-0 arm. Reuse `SurfaceDefTable`'s cascade as the reference for the id bounds test
(signed lower, unsigned upper) but **not** for the fallback, which differs by design: read
`CSVM/src/Session/SurfaceDefTable.cs`'s doc comment first, it spells out exactly where the two
families diverge. Do not touch `SurfaceDefTable` itself.

**Model recommendation.** high. It changes what every gun and rocket draws on most of the ground,
across a parser, a runtime path and a data model, with a faithfulness rule that differs by one arm
from the neighbouring family that just landed.

**Verify.** A before/after at the same location and weapon, on ground of each authored id and at
least one unauthored id, so the A2 decision is visible rather than inferred. Take the "before"
capture first: an unchanged effect is not evidence unless you have seen it able to change. Plus the
8-chapter `--freecam` regression from the ground rules.

**Done, 2026-08-13.** The instrument is the weapon lab's scripted placement, headless, reading the
`impact:` breadcrumb (which now prints `id/name`) out of `.scratch/logs/game-*.out`. That is
`docs/verification.md` SHOT-3 (a state log where pixels cannot resolve the effect: the difference
here is an absent sprite and an absent sound), and it is why the runs are headless at all, since
SHOT-9 rules out combining a screenshot with `--headless`. The `--screenshot=` below only bounds
the run length:

```
.\RunGame.ps1 --headless --det --chapter=C4 --weapon-lab=wep_00 \
  --weapon-target=-3688.6,617.1,-2598.1 --weapon-fire --screenshot=./.scratch/b11.png --frames=90
```

The target is the vertex centroid of C4 mesh#722's `dirt` polygons (node `g1588`, 35 of 40 polygons
`NoSlip`=13, so the body's dominant id is 13), derived from the extracted gamez the same way
`soil_area_by_mesh.py` does. The "before" run was the same command in a throwaway
`git worktree` at the pre-B11 commit (never `git stash` — the worktree hazard above).

| capture | before | after |
|---|---|---|
| C4 `g1588/col`, id 13 | `-> Default … fx=3040slug_gunhit snd=snd_grnd_bullet standin=DirtDebris` | `-> 13/dirt … fx=- snd=- standin=DirtDebris` |
| C4 `g2/col`, id 0, same burst | `-> Default … fx=3040slug_gunhit snd=snd_grnd_bullet` | `-> 0/default … fx=3040slug_gunhit snd=snd_grnd_bullet` (unchanged) |
| C3 `g28683/col_water` (`--weapon-surface=water`) | — | `-> 1/water … fx=splash1.flt snd=snd_water_bullet standin=None` (the sea splash still instances) |
| C2 `g36350/col_buildings` (`--weapon-surface=buildings`) | `-> Buildings … fx=bld_damage.flt snd=- standin=Ricochet` | `-> 0/default … fx=3040slug_gunhit snd=snd_grnd_bullet standin=DirtDebris` |
| C1 `g306/col_buildings`, `wep_06` at `(-4258,172,-6405)` | `-> Buildings … fx=large_fireball` (recorded in `weapon-effects.md`) | `-> 0/default … fx=he_ground_effect snd=snd_missile_explode` |

The last two rows are the item's own surprise and are faithful: the shipped towers and hangar walls
carry soil `default`(0), not `buildings`(11), so the `buildings` row is unreachable on them —
`weapon-effects.md`'s two ⚠ notes were corrected in the same turn, since that page asserted the
opposite keying. Regression: `RunTests.ps1` green — 964 units, 37 in-engine suites, engine errors
clean, and all 13 goldens hash-identical, which includes the eight `--freecam` chapter shots.

**⚠ Traps.** The IMPACT fallback is not the crash cascade's, and copying `SurfaceDefTable`'s
slot-0 arm wholesale is the specific mistake this item is most likely to make. The water special
case at `FUN_005ad330` is a second, independent read of the same field, so a refactor that routes
everything through one lookup must not quietly drop it. `fault` is authored but unreachable in the
original; keeping it reachable in ours is a divergence, not a kindness.

Done: no slot-0 arm — `WeaponDef.ImpactFor` answers null for an unauthored or out-of-range id, with
the bounds test alone borrowed from the cascade. The water tint stayed its own read of the same id
(`Projectile.Apply`, `surface == SurfaceRegistry.Water`) rather than being folded into the table
lookup. `fault` never arose: A1 disproved it in the shipped data, and `SurfaceRegistry.IdForName`
discards any unmatched name by construction. One trap this list did not name and the item hit
anyway: the stand-in ladder's arms are OURS, not the original's, and keying them by id would have
narrowed the debris arm from "terrain" to id 0 alone — it now covers every id that is not water, a
building or an aircraft, which is exactly the set the deleted `Default` class covered.

## B12 ☑ Retire `SurfaceClass` at its remaining call sites

**Goal.** The six-member enum no longer exists, and nothing reads
`SceneBuilder.SurfaceMeta` to decide weapon behaviour.

**Evidence (confidence: traced).** The remaining readers, found by grep this session:
`Projectile.ClassifySurface` (`Projectile.cs:456-469`) and its call site (`:1406`);
`ImpactOutcome.Resolve` / `StandInFor` (`ImpactOutcome.cs:52, 57, 73, 80, 82`), including a
`SurfaceClass.Buildings && weapon.IsGun` special case at `:82`; `WeaponLab.cs:558` and its own
name-to-class map at `:595-621`; the water predicate at `WorldEffectsFactory.cs:393` and
`GameSession.cs:728`; and `Suites.cs:2091` and `:4383`.

**Approach.** Convert each site to the id, one at a time. The two water predicates become an id
`== 1` test, which is what `FUN_005ad330` does. `WeaponLab`'s name-to-class map becomes a registry
lookup and gains the ids it could not previously name. Keep `SceneBuilder.SurfaceMeta` itself: it
still feeds non-weapon consumers (`MapEdgeExtender.cs:928, 1075`), and `ClassOverlay` (key X)
deliberately reads neither tag.

**Model recommendation.** medium. Mechanical once B11 has established the lookup, but it touches a
test suite and two session-wiring sites, so it is not a blind find-and-replace.

**Verify.** The suites at `Suites.cs:2091` and `:4383` pass with their assertions rewritten in id
terms rather than deleted, plus the 8-chapter regression.

**Done, 2026-08-13.** The two suites are **`air-to-air`** (`:2091`, the two-rig kill-attribution
suite) and **`ground-contact`** (`:4383`, whose `waterHook` stub stands in for the session binding;
only its comment named the classifier, so nothing there was rewritten in id terms). `air-to-air`'s
check became `SurfaceIdOf(target.Body) == SurfaceRegistry.Player`; both pass. `RunTests.ps1` green:
964 units, 37 in-engine suites, errors clean, 13 goldens hash-identical, which carries the
8-chapter `--freecam` regression.

The flag `--weapon-surface` gained the whole registry, so it doubles as the in-engine check that
the lab now reads ids (C4, `--weapon-lab=wep_00 --weapon-fire`, headless):

| argument | before | after |
|---|---|---|
| `dirt` | the `Default` class, i.e. every untagged body | `nearest of 133 13/dirt bodies (1853 scanned)`, and the burst lands on one: `-> 13/dirt … fx=- snd=-` |
| `lava` | rejected by `SessionSpec` as an unknown class | `this chapter has no collider carrying 4/lava among 1853 scanned` |
| `nonsense` | `is not water/buildings/dirt` | `is not a surface-registry name (default/water/…/dirt)` |

⚠ **`dirt` changed meaning** and any older capture recipe using it is now aiming somewhere else:
it means id 13, real dirt-tagged ground, where it used to mean "everything untagged", which is
`default`. `docs/cli.md`, `docs/controls.md` and the lab's own doc comments say so.

**⚠ Traps.** Do not delete `SceneBuilder.SurfaceMeta` along with the enum. The tag has consumers
outside the weapon path, and `ClassOverlay`'s doc comment explains why it uses neither tag. Read
both overlays' doc comments before touching either.

Done: `SceneBuilder.SurfaceMeta` and `SceneBuilder.ClassifySurface(string?)` both stay — the tag
still splits colliders per texture class (which is what gives a coastal tile separate `col` and
`col_water` bodies at all) and still feeds `MapEdgeExtender` and, until `C21`, `ColliderOverlay`.
What went is the six-member `SurfaceClass` enum and `ProjectilePool.ClassifySurface`; the water
predicate both session sites bound is now `ProjectilePool.SurfaceIsWater`, an id `== 1` test, which
is `FUN_005ad330`'s own test. One consequence worth knowing before `C21` measures it: a body named
`col_water` carrying soil `0` (C4's doubled water sheet) is no longer water to the world runtime's
bounce branch either, which is the same body-granularity effect `C21` exists to make visible.

# Wave C — the overlay

## C21 ☑ Colour the collision overlay by resolved surface id (`BL-345`)

**Goal.** `--collision=show` (key C) colours each collider by the surface id that decides what
happens when you touch it, with a legend naming id and registry name together (`13/dirt`), showing
the id each body *resolves* to rather than the id stamped on it.

**Evidence (confidence: traced).** `ColliderOverlay.ClassOf`
(`CSVM/src/UI/ColliderOverlay.cs:182-203`) keys off `SceneBuilder.SurfaceMeta` and collapses
everything to `water`/`buildings`/`world`/`clutter`/`other`. Since `PLAN-crash-surface-id` A2 every
collider body also carries `SceneBuilder.SurfaceIdMeta` (`SceneBuilder.cs:41, 950`), and B11/B12 of
that plan put both the crash def and the `touchdown_*` graze def on it
(`CSVM/src/Session/SurfaceDefTable.cs`). So `dirt`(13) and `default`(0) currently draw as one
colour though one raises dust and the other sparks, and a body named `col_water` can carry soil `0`
(C4's doubled water sheet, `g1708` vs `g2109`, recorded in B11's landing commit), which is exactly
the case someone opens the overlay to diagnose. The legend is a fixed six-name palette
(`LegendClasses`, `ColliderOverlay.cs:70`) fed by `ColorFor` (`:205`).

**Approach.** Key `ClassOf` off `SurfaceIdMeta` through `SurfaceRegistry.NameForId`
(`CSVM/src/Mech3/SurfaceRegistry.cs:60`), drawing each id as what it actually resolves to per
Decision 4: ids with no def of their own render as slot 0, not as themselves. Replace the fixed
palette with an id-plus-name legend, since fourteen ids do not have fourteen readable colours.
Keep the `clutter` and `plane` arms as they are: those are owner classes, not surfaces, and neither
tag decides them.

**Model recommendation.** medium. One file, a key swap and a legend format, but the resolve-not-raw
rule is the point of the item and getting it backwards silently re-creates the bug.

**Verify.** `--collision=show` on **C4**, which serves both halves: it has the clearest dirt/default
split of the eight chapters after C2 (9.57 % `dirt` by area, 133 dirt-tagged collider bodies of
1,853 by the weapon lab's own census), and it is the chapter the doubled water sheet lives in. Plus
the 8-chapter `--freecam` regression from the ground rules.

**Done, 2026-08-13.** Two instruments, an A/B against a throwaway `git worktree` at `6adb6ce`
(never `git stash` — the worktree hazard above).

*Pixels* — `--freecam --chapter=C4 --collision=show --no-fog --det --mute
--pos=-3688,800,-2350 --direction=0,-0.62,-0.78 --screenshot=… --frames=120`, windowed
(`docs/verification.md` SHOT-9). Before: every terrain tile one blue `world`, legend
`water buildings clutter plane world other`. After: the dirt ridge separates in tan while the
tiles beside it stay blue, legend `0/default 1/water 13/dirt clutter plane other`. Same pose, same
113,739 lines, so the geometry is identical and only the key moved.

*Census* — the same `--collision=show` run headless per chapter, before vs after (`lines` identical
in all eight, i.e. one population re-keyed):

| chapter | before | after |
|---|---|---|
| C1 | buildings 72 · water 50 · world 1869 | 0/default 1843 · 1/water 48 · 13/dirt 100 |
| C1B | buildings 10 · water 169 · world 889 | 0/default 902 · 1/water 166 |
| C1C | water 202 · world 1180 | 0/default 1180 · 1/water 202 |
| C2 | buildings 80 · water 100 · world 1083 · clutter 14582 | 0/default 1082 · 1/water 100 · 13/dirt 81 · clutter 14582 |
| C2B | water 202 · world 813 | 0/default 813 · 1/water 202 |
| C3 | buildings 64 · water 398 · world 1319 | 0/default 1337 · 1/water 399 · 13/dirt 45 |
| C4 | buildings 91 · water 12 · world 1789 | 0/default 1708 · 1/water 12 · 13/dirt 172 |
| C5 | buildings 679 · water 175 · world 2745 · clutter 71993 | 0/default 3424 · 1/water 175 · clutter 71993 |

Every chapter's `buildings` count goes to `0/default`, which is the empty-slot arm made visible:
`buildings`(11) carries measurable area in one chapter only, so those bodies are named by their
texture and behave as slot 0. Zero engine errors in all sixteen runs.

**Two corrections to this item's own evidence, both measured.** (1) **C4's `col_water`-carrying-
soil-0 case does not exist.** The doubled sheet is `g1708` (mesh#845, 2 polys, `wtr00000.tif`,
soil `Water`) and `g2109` (mesh#860, 22 polys, **`shore1.tif`**, soil `Default`) — the second is
not water-textured, so it is a plain `col` body, and C4's `col_water` count is 12 before and 12
after. The `PLAN-crash-surface-id` B11 note this item cited only said the two meshes share a
footprint. (2) **The name/id disagreement is real, just elsewhere**, and the census above finds it
both ways: C1 50→48 and C1B 169→166 `col_water` bodies drop off the water colour, and C3 gains one
(398→399) — a bucket whose texture is not water-classified but whose material soil is `Water`. The
per-bucket source is in the census run this session (`soil_bucket_strand.py`'s bucketing, re-run
for the name-vs-id question): C1 mesh#1260 (25 polys) and mesh#1313 (18 polys) are `water`-class
buckets carrying soil `Default`; C3 mesh#765 (2 polys, untagged texture, soil `Water`) is the
inverse. A third C1 candidate, the 1-poly mesh#33 bucket every chapter carries, does not reach a
built collider — inferred from the counts moving by 2 rather than 3, not measured directly.

**A number that moves for a legitimate reason.** The overlay's `13/dirt` count is not the lab's
133: it walks the tree *after* `MapEdgeExtender` has built its 40 border cells, whose mirrored
tiles are real rebuilt subtrees and carry the ids of the tiles they copy. Same C4 session, both
instruments: lab 133 of 1,853 bodies (scanned at build), overlay 172 of 1,892 shapes; with
`--map-edge-block=1` the overlay reads 173 and the lab still 133, and the freecam pose above reads
182 because the extension's rolling window follows the camera. The base world's dirt count is the
fixed one.

Regression: `RunTests.ps1` green with `CSVM_DATA_ROOT` set (`docs/verification.md` LOG-17) — units,
37 in-engine suites, engine errors clean, 13 goldens hash-identical with `manifest.json` unmodified
in the working tree (GOLD-9). No golden shoots the overlay, so an unmoved hash is the expected
result, not the evidence; the before/after captures above are.

**⚠ Traps.** The overlay reads the tag off the collider **body**, and A2 stamped the id at body
granularity, not per polygon: a mesh whose polygons carry different ids reports one id for the whole
body, up to 16.4 % of C1's dirt-tagged ground
(`analysis/surface-classification/FINDINGS.md`, the A2 stranding table). The overlay therefore shows
what the engine will actually select, which is the right thing, but it is not a picture of the
source data. Say so wherever the legend is documented. Second trap: do not delete the class read to
"fix" this. `ClassOverlay` (key X) deliberately uses neither tag, and `SurfaceMeta` keeps non-weapon
consumers after B12.
