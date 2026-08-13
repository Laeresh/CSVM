# Milestone 3 — Weapons and Destruction

> **✅ COMPLETE — 2026-07-25.** Milestone 3 (weapons and destruction) delivered. Every checklist item
> is landed (Waves A–F) or deferred to M4 (B19/B20/E38 guided flight + lock-on) / superseded by
> [`PLAN-testing.md`](../PLAN-testing.md) (F40/F41). Exit criteria 1–6 are met at the code/data level;
> the **at-the-controls sign-off is owed** — pass 1 flown 2026-07-25, its polish/fix items in
> `backlog.md`'s "Milestone 3 Polishing" and the re-tests in `playtest.md` §1. Archived for its
> evidence and dead ends; read as history.

**⚠ This plan is preliminary.** It was written on the assumption that M2 polish 3 was still in
flight; **that plan closed during the same session** (items 4, 5, 6 landed in `b83252c` /
`0b15521` / `966ac46`; item 11 closed as disproven in `3c1e008`; item 7 folded into 11). Every
item there is now landed or closed, and it is ready to move to `docs/plans/` with a `COMPLETE`
banner once its user-owned weather-zone A/B is handed over.

**So the sequencing constraint behind decision 8 is already satisfied.** Wave A remains the
right place to start — it is what unblocks the user-owned items on the critical path — but
waves B–F are no longer gated on anything except A8. The **owed playtests** and **TUNE
constants** in `backlog.md` still gate calling Milestone 2.5 done; they do not gate this work.

Scope was settled in a grilling session on 2026-07-22. Every decision below is the user's, and
the "Decisions" table is the authority when this document contradicts itself elsewhere.

---

## Milestone goal

The player can shoot, and the world can break.

- Guns, cannons and hardpoint ordnance fire from the player's aircraft with correct ballistics.
- World destructibles take damage, run their authored damage stages, and die with the correct
  animation, effects, audio and collider removal.
- Everything is driven by the original's own data.

**Nothing fights back.** No AI aircraft, no return fire from emplacements or turrets, no PvP.
That is a deliberate boundary: the player's damage model (`PlaneDamage`, `DamageVisuals`,
`CrashBreakup`) already exists and stays collision-driven exactly as it is today.

---

## Decisions (2026-07-22)

| # | Question | Decision |
|---|---|---|
| 1 | Threat model | **Nothing fights back.** Player fires, world dies. No PvP, no AA return fire, no AI. |
| 2 | Loadout source | **A hand-authored stock-loadout table** supplied by the user, read from a data file. |
| 3 | Guided weapons | **Deferred to M4 (revised 2026-07-24).** ~~Full guided flight; lock restricted to ground destructibles.~~ The original has **no manual ground-target selection** — its auto-aim is game-handled and **enemy-plane-only** (same target-cycle as stunt-race objective selection), and M3 has no enemy planes to lock. So M3 fires **every** rocket, the **Seeker included, as dumbfire**; all homing + lock-on move wholesale to M4. |
| 4 | Gun mechanics | **Finite ammo + empty-clip warning, and cannon spread.** Heat/jam and ammo pickups → `backlog.md`. |
| 5 | HUD | **All four**: `gungauge` + `missilegauge`, weapon-name readout, ballistic impact-point reticle, lock-on indicator. |
| 6 | Collide damage | **Follow the data exactly** — 44 collide-destructibles, 2,565 weapon-only. |
| 7 | Weapon select | **Gun selector cycles mount slots — ONE group fires at a time** (the "plus ALL" in the original wording was **removed 2026-07-24 per the user: the original never fires all groups at once**); hardpoint selector cycles ordnance. Two independent buttons. |
| 8 | Sequencing | **Wave A now; waves B–F after M2 polish 3 closes** — *satisfied: that plan closed the same day. See the banner above.* |
| 9 | Configurator | **Stock loadouts only.** The gun/hardpoint configurator UI is deferred, but the loadout model is fully data-driven so it drops in later without rework. |
| 10 | Turrets | **Deferred to M4.** Turrets are AI gunners that acquire and engage other aircraft automatically — not player-aimed. With nothing to fight they have no targets. |

---

## ⚠ Read this before implementing anything

`docs/plans/PLAN-M2-polish-3.md` records that **three of its five wave-1 items had materially wrong
evidence**, and that in both bad cases the *supporting* evidence agreed while the *mechanism*
did not (`docs/verification.md` rules 6 and 7). This plan was written in one session and
**four of its author's confident readings were disproven within that same session** — two by
data, two by the user's playtests, in minutes:

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `CLUSTER_SIZE` is the volley size (rounds per trigger pull) | `wep_00` has `CLUSTER_SIZE 2800`. Nothing fires 2,800 rounds per pull. It is **rounds carried per slot** — since confirmed by playtest (A9). |
| 2 | Gun calibers are fixed hardware per airframe | User: there is a **configurator** where you place up to 4 guns. The Ammo Selection screen only picks the ammo *type* for guns already placed. |
| 3 | Slot 𝑛 maps to `firepoint(2𝑛−1), firepoint(2𝑛)` | User flew the Bloodhawk: stock is **40-cal inner wing, 30-cal outer wing** — the wing pairs. The predicted mapping put both guns in the nose. *(The correct rule — reverse index order — was found later, from the user's mount table. Note the direction was exactly backwards: an appealing rule that happens to be inverted still fails every test.)* |
| 4 | Turrets are a player-aimed gun slot | User: turrets are **AI gunners**, automatically targeting other planes. (This also explains why `extracted/zrdr/ai.zrd.json` contains nothing but `TURRET` defs.) |

**Treat every Evidence section below as a lead to verify, not a finding to implement.** Where
this plan states a hypothesis it says so explicitly, and names the instrument that settles it.
**Landing no code with a correct disproof is a success here.**

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees. Wave 1 of
M2 polish 3 had three agents pop each other's stashes, one of which manufactured a confident
result from another agent's in-flight edits. **Never use `git stash` in a worktree session
here** — use a local commit on your branch, or a file copy. Recorded in `docs/verification.md`.

---

## Wave A landed — corrections to the evidence below

Wave A's docs (`weapons.md`, `markers.md`, `destructibles.md`, `weapon-effects.md`, and the
`vehicle.md` decode) verified the survey below against the data and corrected it. The survey
text is kept as written; these override it:

- **`IMPACT` has SIX surface classes, not five.** A `quicksand` class joins
  `default`/`water`/`enemy`/`player`/`buildings` (3 entries carry it). **B15 must classify six.**
- **`AMMO_LIMIT`-absent-on-rockets is too strong.** It holds for the air-to-air rocket set, but
  the six `CRATER` ground-attack munitions (`wep_04`/`12`/`25`/`26`/`27`) *do* carry `AMMO_LIMIT`.
  Turret guns carry `AMMO_LIMIT 9999` and no `CLUSTER_SIZE`.
- **`unknown_seq` is NOT reliably the death sequence.** For the water tower it is a puffer loop;
  the death swap `destroy_h2twr` sits in the ordinary `sequences` array, and `DAMAGE_SEQUENCE`
  arrives compiled as a sequence literally named `DAMAGE_SEQUENCE`. **C24 must not key off
  `unknown_seq`.** `proximity_damage` is `false` everywhere — it does *not* encode collide mode;
  `activation` does.
- **The plan's `AnimRuntime.cs` line numbers have drifted** (code moved since it was written):
  `ANIM_HEALTH` eval is now ~`:1556` (not `:1046`), `HideUncoveredDestroyed` ~`:2813` (not
  `:1934-1947`). `AnimDefs.cs:74-95` (no `DAMAGE_SEQUENCE` case) still holds. **Re-locate by
  symbol, not line, when implementing waves B/C** — and re-check `AnimRuntime.cs:429` (C26).
- **Gun nodes `fgun`/`rgun`/`bgun0..3` are mesh-less markers** (`model_index -1`) under the
  turret subtrees, on the five turret airframes only — not wing-gun muzzles. **The Devastator
  has no own model** — `pdevastator` inherits the base `player_pfighter` (the pirate fighter).
- **Five `weapons.json` effect names resolve to nothing** in this extraction (referenced but
  undefined): `bld_damage.flt`, `rcochet1`, `call_small_flash`, `f18sparks2`, `flak_effectplayer`.
  Leads for D30/D29 — confirm each renders or is inert. The other 52 targets + all 23 sounds resolve.
- **Eight `weapons.json` keys the survey missed** are now documented in `weapons.md`: `DAMAGE`,
  `HIGH_EXPLOSIVE`, `SONIC`, `BEEPER`, `BEEPER_SEEKER`, `TANGLER`, `REAR`, `SMOKE_SCREEN`.

## What the data actually ships

A survey on 2026-07-22 found the weapon data far more complete than the docs suggest — and
almost entirely undocumented.

### `extracted/zrdr/weapons.zrd.json` — the ballistics table

**48 entries** under `BALLISTICS`, keyed `wep_00` … `wep_73`. **No page in `docs/formats/`
covers this file, and `zrdr.md`'s reader-family index does not list it at all.**
`vehicle.md:30` is the only mention, and it is a deferral: *"weapon lists — dogfight-milestone
scope, undecoded here."* Retiring that line is item A6.

Key coverage across the 48: `DESC`/`NAME`/`FIRE_RATE`/`FLYOUT` 48, `IMPACT`/`VELOCITY`/`FIRE`
47, `ARMOR_DAMAGE`/`HEALTH_DAMAGE` 46, `RANGE`/`CLUSTER_SIZE` 46, `AMMO_LIMIT` 37,
`CANNON`/`CANNON_SPREAD`/`LOOPED_SOUND_NAME` 31, `CALIBER` 29, `PRIORITY`/`ACCELERATION` 16,
`ROCKET` 15, `TURN_RATE`/`IMPACT_PROXIMITY` 14, `LOCK_ON`/`DETONATION_DISTANCE` 13,
`CRATER` 6, `GRAVITY` 5, `FIRING_HEAT` 4, plus singletons (`FLYOUT_HEALTH`, `TARGETABLE`,
`DAMAGES_ZEPPELIN`, `PROJECTILE_BBOX`, `DESTROY_ANIMATION`, `SHAKES_CAMERA`, `TORPEDO`, …).

Structure of one gun entry:

```json
"wep_00": {
  "DESC":["MSG_WEAP_30CAL_SLUG"], "NAME":["30slug"],
  "AMMO_LIMIT":[2800], "CLUSTER_SIZE":[2800], "FIRE_RATE":[10.5],
  "ARMOR_DAMAGE":[2.0], "HEALTH_DAMAGE":[2.0],
  "RANGE":[1000.0], "VELOCITY":[1000.0], "CALIBER":[30],
  "CANNON":null, "CANNON_SPREAD":[6.0], "FIRING_HEAT":[5.0],
  "LOOPED_SOUND_NAME":["snd_30cal"],
  "FIRE":["ANIMATION",["muzzle_burst_slug"]],
  "FLYOUT":["MODEL",["slug.flt"]],
  "IMPACT":["default",  ["ANIMATION",["3040slug_gunhit"],"SOUND",["snd_grnd_bullet"]],
            "water",    ["ANIMATION",["splash1.flt"],   "SOUND",["snd_water_bullet"]],
            "enemy",    null,
            "player",   [...],
            "buildings",["ANIMATION",["bld_damage.flt"]]]
}
```

**`IMPACT` is keyed by surface class** — `default` / `water` / `enemy` / `player` /
`buildings` — each naming an effect animation and a sound. Hit-testing must classify the
struck surface to pick the right variant (item B5).

Tiers within the 48:
- `wep_00`–`03` — base 30/40/50/60-cal.
- `wep_04`–`15` — ordnance: `9M` incendiary, `ARMOR` AP, `BOOM` HE, `FLAK`, `SONIC`, `FLASH`,
  `BEEPER`, `SEEKER`, `CHOKER`, `SMOKER`, `TORPDO`, `FLARE` (rear-arc).
- `wep_23`–`29` — turret guns, glidebomb, cannonball (`RANGE 3500`), AA flak, `FW` fake weapon.
- `wep_30`–`73` — the **player matrix**: 5 calibers × 4 ammo types. `X0` slug, `X1` dum-dum,
  `X2` AP, `X3` magnesium. Systematic: DD = ½ armour / 1.5× health damage, AP the inverse.
- `wep_130`–`170` — the same guns detuned for AI (velocity 600, rate 6.0, ~½ damage).

### The stock loadout (user-supplied, validated against the original's own UI)

There is **no player loadout in the extraction**. All 12 `p*` defs omit `weapons` and inherit
a single list from `player_airplane` that is provably a *capability catalogue* — all 20 gun
variants plus 1,000 rounds of each of 12 rocket types, simultaneously. Per-plane slot counts,
starting loadout and costs are executable-resident. A UI string confirms nothing is pre-filled:
*"You have not selected the necessary rockets for your plane's hardpoints."*

The user supplied the stock table, and it is corroborated by two original-game captures
(`OriginalScreenshots/Ammo Selector Hoplite.png`, `… Balmoral.png`). Those screens read
**"Stock Ford Hoplite"** / **"Stock Bristol Type 146 Balmoral"**, show `AMMUNITION (by gun
group)` as four rows with unused slots reading **"No Gun"**, and show `ROCKETS (underwing
hardpoints)` as exactly one dropdown per pylon. Balmoral renders `.50 / .50 / .30 / .30` and
eight rocket dropdowns — matching the table exactly.

| Plane | Def | W1 | W2 | W3 | W4 | Pylons |
|---|---|---|---|---|---|---|
| Hoplite | `pautogyro` | 30 | X | X | X | 2 |
| Hellhound | `pavenger` | 50 | 40 | X | **50 ᵀ** | 3 |
| Balmoral | `pbalmoral` | 50 | 50 | **30 ᵀ** | **30 ᵀ** | 8 |
| Bloodhawk | `pbloodhawk` | 40 | 30 | X | X | 3 |
| Brigand | `pbrigand` | 60 | 30 | X | **30 ᵀ** | 4 |
| Devastator | `pdevastator` | 50 | 40 | 30 | X | 4 |
| Firebrand | `pfirebrand` | 70 | 30 | X | **30 ᵀ** | 6 |
| Fury | `pfury` | 70 | 30 | X | X | 3 |
| Kestrel | `pkestrel` | 60 | 50 | X | **40 ᵀ** | 5 |
| Peacemaker | `ppeacemaker` | 50 | 40 | X | X | 3 |
| Warhawk | `pwarhawk` | 70 | 50 | X | X | 8 |

**ᵀ = turret slot, deferred to M4** (decision 10). Both screenshots ship stock as **Slug**
guns and **High explosive** rockets, so stock resolves to: caliber *N* → `wep_N0`
(`wep_30`/`40`/`50`/`60`/`70`), every pylon → **`wep_06`** (`BOOM`, the `HIGH_EXPLOSIVE` entry,
armour 40 / health 60).

**W4 is filled on exactly the five airframes carrying a turret** (`pavenger`, `pbalmoral`,
`pbrigand`, `pfirebrand`, `pkestrel` — the five with a `turrets` key in `vehicle.json`) and on
no others. The Balmoral is the only plane with two turrets (`MSG_TUR_PFRONT` +
`MSG_TUR_PREAR`, mesh nodes `fgun`/`rgun`) and the only one with both W3 and W4 filled.
Correlation is 5/5 on n=11 — strong, but still a correlation; A2 confirms it.

### Gun mounts — named positions, not index arithmetic

`extracted/rof/ui_strings.json` carries a contiguous enum at IDs **3060–3079**, headed
`IDS_AIRFRAMEGUNGROUPNAMES`. That symbol name says the exe stores a per-airframe *list of gun
group name IDs* and the configurator renders slot titles by indexing it.

```
3060 Nose Turret          3061 Inner Wing Guns      3062 Outer Wing Guns
3063 Lower Nose Guns      3064 Upper Nose Guns      3066 Right Fuselage Guns
3067 Right Wing Guns      3068 Left Wing Guns       3069 Outer Wing Guns 2
3070 Inner Wing Guns 2    3071 Nose Guns            3072 Nose Guns 2
3073 Rear Turret          3074 Low Inner Wing Guns  3075 Low Outer Wing Guns
3076 Upper Inner Wing Guns 3077 Upper Outer Wing Guns 3079 Middle Wing Guns
```

The `… 2` variants explain a finding that no index rule could: **several planes have
duplicated firepoint coordinates.** Balmoral `fp1,2 ≡ fp5,6` at ±2.62 and `fp3,4 ≡ fp7,8` at
±1.96; Brigand `fp1,2 ≡ fp3,4` at ±1.40 and `fp5,6 ≡ fp7,8` at ±2.34. Two physical positions,
each hosting two gun groups — i.e. `Outer Wing Guns` **and** `Outer Wing Guns 2`. The user
confirmed this directly for the Brigand: *"uses the same outer wing mount for W1 and W2."*

Every plane carries a uniform **8 firepoints + 8 pylons** marker rig under `markers`
(Kestrel is the sole exception at 7 firepoints, its `fp7` at exactly x = 0.00 — a centreline
mount). Pylons bind trivially and sequentially: **Pylons = N → `pylon1`…`pylonN`.**

`target` is a separate mesh-less marker, one per plane root (11 player + 11 AI) — the aim
point, irrelevant to M3, essential to M4.

#### The mount table (user-supplied 2026-07-22, read off the configurator)

**Every plane has all four mount positions.** The stock table's `X` means "this available
mount carries no gun in stock configuration", not "no mount here" — consistent with the
configurator letting you place up to four guns on any airframe. **Every name below is a
verbatim entry from the 3060–3079 enum.**

| Plane | W1 | W2 | W3 | W4 |
|---|---|---|---|---|
| Hoplite | Inner Wing Guns | Inner Wing Guns 2 | Outer Wing Guns | Outer Wing Guns 2 |
| Hellhound | Nose Guns | Nose Guns 2 | Inner Wing Guns | Rear Turret |
| Balmoral | Inner Wing Guns | Outer Wing Guns | Nose Turret | Rear Turret |
| Bloodhawk | Inner Wing Guns | Outer Wing Guns | Lower Nose Guns | Upper Nose Guns |
| Brigand | Outer Wing Guns | Outer Wing Guns 2 | Inner Wing Guns | Rear Turret |
| Devastator | Low Inner Wing Guns | Low Outer Wing Guns | Upper Inner Wing Guns | Upper Outer Wing Guns |
| Firebrand | Inner Wing Guns | Middle Wing Guns | Outer Wing Guns | Rear Turret |
| Fury | Outer Wing Guns | Outer Wing Guns 2 | Inner Wing Guns | Inner Wing Guns 2 |
| Kestrel | Center Guns | Center Guns 2 | Outer Wing Guns | Rear Turret |
| Peacemaker | Center Guns | Right Fuselage Guns | Right Wing Guns | Left Wing Guns |
| Warhawk | Inner Wing Guns | Inner Wing Guns 2 | Outer Wing Guns | Outer Wing Guns 2 |

#### The binding rule — **reverse index order** (confirmed in-engine, A10 2026-07-24)

Cross-referencing the mount names against measured firepoint positions gives:

> **Slot 𝑛 → `firepoint(9−2𝑛)`, `firepoint(10−2𝑛)`** — W1→`fp7,8`, W2→`fp5,6`,
> W3→`fp3,4`, W4→`fp1,2`.

⚠ **This was the third binding hypothesis in this plan; the first two were disproven** — so it was
held as strongly-supported-not-proven until **A10 confirmed it in-engine** (`--dump-loadout` ×
`--dump-markers` for all 11: every firing gun group on its named mount; the Firebrand/Kestrel soft
spot confined to the inert turret). The supporting evidence was categorically different from the
earlier attempts — four independent hard-to-fake coincidences:

- **Devastator (decisive).** The only plane whose mounts vary on two axes. `Low Inner` →
  `fp7,8` (y −0.62, x ±1.17), `Low Outer` → `fp5,6` (y −0.83, ±2.12), `Upper Inner` → `fp3,4`
  (y +0.44, ±1.00), `Upper Outer` → `fp1,2` (y +0.60, ±1.91). **4/4 correct on both axes.**
- **Peacemaker (decisive).** The only plane with left/right-asymmetric mounts. `Center` →
  `fp7,8` (x +0.56, −0.64), `Right Fuselage` → `fp5,6` (+1.96, +1.66), `Right Wing` → `fp3,4`
  (+2.91, +3.34), `Left Wing` → `fp1,2` (−3.34, −2.91). **4/4, sides correct.**
- **Kestrel.** `Center Guns` → `fp7`, which sits at **exactly x = 0.00** and has no `fp8` —
  a single centreline mount. This is the only explanation offered so far for why the Kestrel
  is the one plane with 7 firepoints instead of 8.
- **Bloodhawk.** Reproduces the user's playtest (`Inner`=40 → `fp7,8` at ±3.22; `Outer`=30 →
  `fp5,6` at ±3.66) *and* correctly splits `Lower Nose` → `fp3,4` (y −0.11) from `Upper Nose`
  → `fp1,2` (y +0.46).

Also clean 4/4: Warhawk, Hoplite, Fury (each monotonic inner↔outer). Clean on all non-turret
slots: Balmoral, Brigand (reproducing the user's "W1 and W2 share the outer mount"), Hellhound.

**Two imperfect fits, both turret airframes.** On Firebrand and Kestrel the W3 `Outer Wing`
mount lands one pair inboard of the most extreme pair, leaving the outermost stranded —
because W4 is a `Rear Turret` that consumes a firepoint slot it cannot use. Not a
contradiction, but the place to look first if A10 finds a discrepancy.

### Ammo semantics — settled by playtest

The user confirmed: **the Balmoral's two .50-cal groups have distinct ammo counters.** So
**ammo is per gun group, not per weapon type** — two `.50` groups carry 2,000 rounds *each*.

This makes gun-slot selection meaningful (you can nurse one group while emptying another) and
explains why `MSG_HUD_GUNGAUGE` is `"GUNS: %1: %2!d!"` — the `%1` names *which* counter.

**CONFIRMED by playtest 2026-07-22** (user, stock Bloodhawk: **9 HE rockets, 3 per
hardpoint**): `CLUSTER_SIZE` is **rounds carried per slot**. It is the
only allotment field present for both weapon classes — `wep_06` (HE) has `CLUSTER_SIZE 3` and
**no `AMMO_LIMIT` at all** — and for guns it equals `AMMO_LIMIT` exactly (2800/2800,
2000/2000), so the two readings are indistinguishable on guns and only rockets can separate
them. Under this reading a stock Bloodhawk carries 3 pylons × 3 = **9 HE rockets**, and
`AMMO_LIMIT` is a purchase cap rather than a carried amount.

**Rocket capacity = pylon count × `CLUSTER_SIZE`.** Stock Bloodhawk 3 × 3 = **9** (measured);
stock Balmoral 8 × 3 = 24. Gun capacity = `CLUSTER_SIZE` **per gun group**, counters
independent (measured on the Balmoral's two .50s).

### World destructibles — complete in the data, inert in the engine

An `ANIMATION_DEFINITION` *is* the destructible object, via three keys: **`HEALTH`** (hit
points), **`DAMAGE_SEQUENCE`** (a threshold script — `IF ANIM_HEALTH n → CALL_ANIMATION …`),
and **`ACTIVATION`** = `WeaponHit` or `WeaponOrCollideHit`. `ANIM_HEALTH n` means
`health <= n` (`AnimRuntime.cs:1046`).

Census across all 61 compiled archives (14,963 defs):

| Metric | Count |
|---|---|
| defs with `health > 0` | **2,603** |
| `activation: WeaponHit` | **2,565** |
| `activation: WeaponOrCollideHit` | **44** |
| `AnimHealth`/`AnimHealthRange` conditions | 4,131 — **100 % inside `DAMAGE_SEQUENCE`** |
| defs with a non-null `unknown_seq` | 1,544 — **all** health > 0 |

Health histogram: `40.0`×1246, `10.0`×512, `60.0`×460, `20.0`×111, `30.0`×90, `15.0`×65,
`0.01`×43, `5.0`×31, `4.0`×17, singletons up to `750.0`.

**Damage stages are a shared template, not per-object authoring.** Of the reader-side defs,
**91 carry both `HEALTH` and a `DAMAGE_SEQUENCE`** (48 with two stages, 42 with three, 1 with
one). Their `ANIM_HEALTH` thresholds, expressed as a fraction of that def's own `HEALTH`, draw
from a small vocabulary:

| fraction | 0.85 | 0.75 | 0.60 | 0.50 | 0.30 | 0.25 |
|---|---|---|---|---|---|---|
| count | 20 | 17 | 43 | 43 | 41 | 39 |

Two dominant progressions: **`{0.60, 0.30}`** and **`{0.85, 0.50, 0.25}`**. The standard
two-stage escalation is `sputter_black_smoke_obj` (smoke) then `sputter_fire_smoke_obj` (fire),
48 defs each. **Implication for C22:** evaluate thresholds generically as fractions and ~90
objects behave correctly at once — do not special-case per object.

⚠ Thresholds are authored as **absolute** `ANIM_HEALTH` values, not fractions. The fractions
above are derived. Evaluate against the absolute value (`health <= n`) as
`AnimRuntime.cs:1046` already does; the fraction view is for understanding the template, not
for implementing it.

Sequence shape for a WeaponHit def is typically `('DAMAGE_SEQUENCE', 'destroyit')` (879 defs)
or `('destroyit',)` (761): **`DAMAGE_SEQUENCE` = progressive damage, `destroyit`/`unknown_seq`
= the death sequence.** Death-sequence event kinds (2,360 calls): `CallAnimation` 2360,
`ObjectActiveState` 2028, `InvalidateAnimation` 1286, `StopAnimation` 1260, `Sound` 307,
`PufferState` 148, `Loop` 86, `ObjectMotion` 16. Effects called: `large_30sec_fire` ×1035,
`great_balls_of_fire` ×432, `large_fireball` ×307, `large_black_smokeball` ×288,
`biggun_flying_parts` ×84.

**The collide-destructible set is exactly 44 objects**, and they are special:

| Chapter | Objects | Health | What |
|---|---|---|---|
| C2 | `fcpan01`–`fcpan39` | 0.01 | Hollywood facade panels — fly-through set dressing |
| C5 | `w_win01`–`w_win04` | 0.01 | Warehouse windows |
| C5 | `agyrobus` | 70.0 | The only substantial collide-destructible in the game |

Everything else is weapon-only: ramming a water tower kills *you*, and the tower is untouched.

**Engine gap.** `CompiledAnim.cs` already parses `Health` and `Activation`; a compiled
`DAMAGE_SEQUENCE` already arrives as an ordinary `AnimSequence`. But `AnimDefs.cs:74-95` (the
reader front-end) has **no `DAMAGE_SEQUENCE` case — it is silently dropped** (zero hits for the
token across all `*.cs`). `unknown_seq` and `proximity_damage` are never read. And health is
used for exactly one thing: condition evaluation against the **static authored** value
(`AnimRuntime.cs:1043-1049`), never a mutable per-instance value — so every `ANIM_HEALTH`
branch is uniformly false in a live world. Nothing ever starts a `WeaponHit` def.

### Two dead code paths this milestone wakes up

- **`AnimRuntime.cs:429`** skips `ObjectMotion`'s ballistic half with the comment that it is
  "reachable only from OnCall/WeaponHit". That is the debris physics — e.g. the water tower's
  `OBJECT_MOTION h2twr_middle {GRAVITY LOCAL -2, … RUN_TIME 5}`.
- **`backlog.md:102`**: 4,378 `OnCall` + 1,650 `WeaponHit` `Sound` events are deferred because
  they "need weapons this project does not have" (21 names are `DYNAMIC_WEIGHTS` groups needing
  a further decode).

### Effects and assets already on disk

Effect readers in `extracted/zrdr/`: `muzzle_burst`, `gunhit`, `gunshell`, `flak_control`,
`flak_trails`, `torpedo_effects`, `sonic_control`, `sonic_puffers`, `sonic_rings`,
`missile_puffers`, `flash_control`, `beeper_plug`, `scatter_control`, `scatter_trails`,
`he_control`, `he_effects`, `pufftrails`, `rear_arc`, `cockpit_bulletholes`, `8inch_cannon`,
`aa_gun`, `maa_gun`, `fbgun`, `fbgun2`, `zep_*_pieces`.

Projectile/effect prototype roots in `C1/gamez/nodes.json` (54 weapon-named): `slug.flt`,
`a_torpedo`, `ap_rocket`, `he_rocket`, `flak_rocket`, `flash_rocket`, `beeper_rocket`,
`scatter_rocket`, `aaflak`, `gunshell`, `firepoint`, `muzzle_burst`/`_slug`/`_ap`/`_dum`/`_mag`,
`gunhit`, `dum_gunhit`, `mag_gunhit`, `flak_explosion`/`_fire`/`_smoke`/`_trail1..5`,
`scat_explosion`, `flake1..7`, `planeflakes`, `two`/`three`/`four_bulletholes`,
`explode_here1..9`.

Textures in each chapter's `texture/`: `slug_muzzle1/2`, `dum_muzzle1/2`, `ap_muzzle1/2`,
`mag_muzzle1/2`, `tracer_slug`, `tracer_dumdum`, `tracer_armorpierce`, `tracer_magnesium`,
`tracer1`, `slugtip`, `shell1/2`, `atorp`.

HUD assets in `extracted/rimage/`, **neither referenced anywhere in the codebase yet**:
- **`impact_point.png`** — the reticle: four tick marks around an open centre.
- **`5pointhud.png` / `5pointhudbrite.png`** — the game's **HUD bitmap font** (a 5-pixel
  character atlas, `0123456789:;<=>?@A…z`), normal and highlighted. Not a reticle.

`gungauge` and `missilegauge` are modelled on all 11 planes inside the same `gauges` subtree
`GaugeCluster` already reads, with letter/digit texture cycles and `ggindicatorN`/`mgindicatorN`
belt lights — entirely unwired (`docs/formats/hud.md:120`).

---

## Ground rules

Carried over from every prior plan here:

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a
  handler; never guess a value.
- `CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the **same turn** as
  each landed item. **New decodes land with their docs page in the same change.**
- A landed item gets a dated entry in `docs/HISTORY.md`.
- Verify against a full 8-chapter `--freecam --chapter=<X>` regression (zero errors, same
  mesh/node counts unless the change is expected to add coverage), plus a targeted screenshot
  at the location the report came from.
- **Read `docs/verification.md` before measuring anything.**
- Scratch output goes to `./.scratch/`, never the OS temp directory.
- **Read a module's bullet in `docs/architecture.md` before modifying that module.**

---

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven.

### Wave A — research & documentation (may start now)

New files and docs only; touches no module M2 polish 3 is editing.

1. ☑ `docs/formats/weapons.md` — the ballistics table — **landed**
2. ☑ `docs/formats/markers.md` — gun mounts, markers and the airframe gun-group enum — **landed**
3. ☑ **Marker reference tool** — `--dump-markers` + labelled viewer overlay (`--viewer --markers`, key K) — **landed**
4. ☑ `docs/formats/destructibles.md` — the world-destructible model — **landed**
5. ☑ `docs/formats/weapon-effects.md` — the weapon effect-reader family — **landed**
6. ☑ `vehicle.md` — retire the "undecoded here" deferral — **landed**
7. ☑ Stock-loadout file format + seed from the user's table — **landed** (`CSVM/data/stock_loadouts.json` + `docs/formats/loadouts.md`)
8. ☑ **[USER]** mount-name column — **delivered 2026-07-22**; the binding rule fell out of it
9. ☑ **[USER]** `CLUSTER_SIZE` = rounds-per-slot — **confirmed 2026-07-22** (Bloodhawk: 9 HE, 3/hardpoint)
10. ☑ Verify the binding rule + flash appearance — **landed** (2026-07-24): `--dump-loadout` × `--dump-markers` confirms every firing gun group lands on its named mount (Devastator 3/3 two-axis, Peacemaker 2/2 sides, Bloodhawk/Brigand reproduce the user's playtests); windowed captures show the Bloodhawk flash on the wing + the flash matches `MuzzleFlash1.png`. The Firebrand/Kestrel soft spot is confined to the inert turret (M4). **Wave A complete.**

### Wave B — weapons core

11. ☑ `WeaponDefs.cs` — typed reader over `weapons.json` — **landed** (`--dump-weapons` verifies)
12. ☑ `Loadout.cs` — stock-loadout reader, slot model, marker resolution — **landed** (`--dump-loadout` verifies)
13. ☑ `Projectile.cs` — spawn and integration — **landed** (`ProjectilePool`)
14. ☑ `FLYOUT` model instancing — **landed** (2026-07-24): rockets fly the `FLYOUT` MODEL body instanced from the chapter gamez prototype root, nose-forward; guns keep the tracer path (measured)
15. ☑ Hit detection + surface classification for `IMPACT` variant selection — **landed** (world raycast + collider surface tag)
16. ☑ Gun firing — rate, per-group ammo, `CANNON_SPREAD`, empty-clip — **landed** (Space/pad-B, `--fire`)
17. ☑ Hardpoint firing — per-pylon allotment and depletion — **landed** (F/pad-A, one rocket per pull, round-robin pylons)
18. ☑ Weapon selectors — gun-group cycle (ONE at a time, no ALL) + hardpoint ordnance cycle — **landed** (G/H, D-pad L/R)
19. ⊘ Guided flight — **deferred to M4** (revised 2026-07-24; no enemy planes to lock, original has no manual ground selection). Seeker flies dumbfire in M3
20. ⊘ Ground lock-on — **deferred to M4** (same revision); acquisition is the enemy-plane target-cycle (stunt Tab-cycle reuse), M4 scope

### Wave C — destructibles

21. ☑ Per-instance mutable HP + destructible instance registry — **landed** (`DestructibleRegistry.cs`; live HP read by `ANIM_HEALTH`, provable no-op until C23)
22. ☑ `DAMAGE_SEQUENCE` — reader front-end parsing + live threshold evaluation — **landed** (`AnimDefs` parse + `AnimRuntime.ApplyDamageStages`; stages fire once per threshold, `--damage-test` verifies)
23. ☑ `WeaponHit` activation and damage application — **landed** (`AnimRuntime.DamageAt` + `Registry.Resolve`; HE 1-hit / AP 2-hit a HEALTH-60 tower, `--damage-hd` verifies)
24. ☑ Death sequence execution + healthy→destroyed swap — **landed** (`RunDeathSequence` plays the def's death via `Start`; RESET-derived swap fallback for the ~10% that author none; `--damage-hd` swap check verifies)
25. ☑ Collider removal on destruction (the doors) — **landed** (no new code: C24's swap runs `SetSubtreeActive`, which toggles colliders with visibility; `--damage-hd` `col[off,on]` census proves it, C2 doors off 1/on 8; propane→door chain confirmed)
26. ☑ Ballistic `ObjectMotion` — debris tumble — **landed** (already implemented by the M2 crash `MotionRuntime`; reached on death via C24's `Start`; the C24 "stubbed" note was a no-clock-tick harness artifact — water tower launches 2 visible pieces, buildings 7, verified by `--damage-hd` `debris[N]`)
27. ☑ The 44 `WeaponOrCollideHit` collision path — **landed** (`FlightController` collide-through + `AnimRuntime.CollideDamageAt`, gated on `ACTIVATION`; facades/windows/`agyrobus` break on contact and the plane flies through, `WeaponHit` towers/gates ignore collision — `--damage-hd` `collide[✓/✗]` verifies)
28. ☑ Destructible reset/restore (for the debug tools) — **landed** (`AnimRuntime.ResetDestructible`: Stop death + restore debris rest poses + re-apply RESET_STATE + restore HP; destroy→reset→destroy idempotent, `--damage-hd` `reset[…]` verifies across C1/C2/C5). **Wave C complete.**

### Wave D — presentation & audio

29. ☑ Muzzle flash — flash sprite at the firepoint on each shot — **landed** (billboard burst; exact `muzzle_burst_*` anim = refinement)
30. ☑ Impact effects — **landed** (2026-07-24): per-surface `IMPACT` **sound** + the named effect **model** at the hit point (the water splash `splash1.flt`/`bsplsh.flt` instance; C1B/C2B verified), + spark fallback. The **puffer** half (`gunhit` smoke, `large_fireball`) is blocked at runtime in flight (puffer factory torn down after build) → folded into D32's world-effects runtime; the 5 undefined names confirmed inert
31. ☑ The `Sound` anim-event family — **landed** (2026-07-24): `AnimRuntime.HandleSound` fires the one-shot `SOUND` (death/damage/impact audio) as a fire-and-forget `WorldSounds.PlayOneShot` at its AT_NODE; `SOUND_GROUPS` decoded (`SoundDefs.LoadGroups` + `SoundGroup.Pick`, `DYNAMIC_WEIGHTS` recency); `Sound` dropped off every chapter's unhandled list, death sounds play (switchhouse `air_mixed_exp_sg` verified)
32. ☑ Destruction **+ impact** effects wiring — the world-effects runtime — **landed** (2026-07-24): `AnimRuntime.PlayEffectAt` over a hidden template stage renders the impact/destruction puffers; rocket impacts route via `ProjectilePool.EffectSink`, deaths via the world runtime's `ExternalEffect`; `--effects-test` verifies 16/28 build a puffer (rule 76). Gun-impact `gunhit` smoke deferred (no stop event → follow-up)
33. ☑ Tracers — **landed** (velocity-aligned additive streaks; per-ammo tracer texture = refinement)
44. ☑ **Pylon ordnance visuals** — mounted rocket models that disappear as ammo depletes — **landed** (2026-07-24): `PylonOrdnance.Build` mounts one FLYOUT-model body per pylon (the round's own asset), hidden as ammo depletes; stock Bloodhawk shows 3 HE rockets → 9 pulls → third vanishes on the 9th; `--rocket=wep_08` swaps to `sonic`. **Wave D complete.**

### Wave E — HUD

34. ☑ Bitmap-font HUD text renderer (`5pointhud`) — **landed** (2026-07-24): `HudFont.cs` — printable-ASCII `0x20`–`0x7e` proportional 5px font, auto-segmented from the atlas, sized via `HudMetrics`; `--hud-font-test` proves 1P == 4P-pane (scaled)
35. ☑ `gungauge` + `missilegauge` in `GaugeCluster` — **landed** (2026-07-24): both dials render on all 11 planes (uniform subtree; the face hangs off the generic `g815`/`g819` on every plane, no Bloodhawk special case). 4-digit `4char_ammo` + 6-char `6char_type` cycles show the selected weapon's rounds + NAME; `ggindicator`/`mgindicator` belt lights step green/yellow/red per slot fraction; the arrow tracks the selected gun group / next-armed pylon. **Gun count is per-group, rocket count is per-pylon** (user-corrected — the original's Warhawk reads `BOOM 3`, not the 24-round total). Windowed captures verify the roster, per-group/per-pylon counters, and the green→yellow→red step (Bloodhawk rocket depletion). The green/yellow/red thresholds are a TUNE pending an original playtest (see `playtest.md`).
36. ☑ Selected-weapon readout (`MSG_HUD_GUNGAUGE`) — **landed** (2026-07-24): `WeaponReadout.cs` draws the selected gun group + rocket type and their live ammo in the `5pointhud` font, from the game's own `MSG_HUD_GUNGAUGE` / `MSG_HUD_MISSLES` templates (`Messages.Fill`, not hardcoded); `%1` = mount name / rocket display name, `%2` = per-group / per-pylon rounds. Replaced the interim `AmmoLine`. Verified: Bloodhawk `INNER WING GUNS: 2400`→`OUTER WING GUNS: 2800` and Balmoral twin-.50 name swap show name+count update from `messages.json`.
37. ☑ Impact-point reticle — ballistic projection — **landed** (2026-07-24): `ImpactReticle.cs` draws `impact_point.png` at the SELECTED gun group's ballistic impact point at a convergence distance (`GunConvergenceDist` TUNE = 250 m; not in the data), integrated exactly as `ProjectilePool` fires (incl. inherited velocity, sans spread), projected via `UnprojectPosition` at draw time so it never lags the chase cam. Measured: `nose→reticle 0.00°` level (rounds land where it sits), up to `0.77°` below the nose toward the velocity vector in a hard pull (visibly trails the nose). **Wave E complete.**
38. ⊘ Lock-on indicator — **deferred to M4** (with B19/B20, 2026-07-24): the lock it indicates is the enemy-plane target-cycle; M3 has no enemy planes to lock. See E38 detail.

### Wave F — debug & verification

39. ☑ Weapon lab in `--viewer` — **landed** (2026-07-24): `src/UI/WeaponLab.cs`, the fourth `--viewer` lab (key **W**, `--weapon-lab`). Mounts any of the 48 weapons on any firepoint/pylon and fires it into its own scene-less `ProjectilePool` at a tagged stand-in target wall (surface cycle exercises B15); steppers show live ballistics; auto-fire/fire-once/Space; copy-CLI-args. `--weapon-test` reports **48/48 fired OK, 0 errors** (Bloodhawk/Kestrel/Warhawk/Peacemaker); windowed captures show tracers + impact sparks (`wep_30`) and the rocket path (`wep_06`). Built hidden in every parked `--viewer` session so a plain viewer stays byte-identical.
40. ⊘ Freecam raycast pick + HP control — **superseded by PLAN-testing D31/D34** (2026-07-24)
41. ⊘ Destructible list overlay + camera jump — **superseded by PLAN-testing D32** (2026-07-24)
42. ☑ `--destroy=` CLI trigger — **landed** (2026-07-24): kills a named destructible at session build (reuses `AnimRuntime.DamageAt`) so a `--screenshot` captures its destruction controller-less; `--freecam` auto-frames it + builds the world-effects runtime (gated on `--destroy`). Destruction screenshot verified in all 8 chapters (C2B's only destructibles are the unplaced mission airship — a map fact). **Wave F complete.**
43. ☑ `--infinite-ammo` / `--loadout=` overrides — **landed** (both flags live + documented in `docs/cli.md`; found delivered on the 2026-07-24 Wave-F review — they shipped with B12/B16)

---

## Dependency and parallelism notes

**Wave A is almost fully parallel.** A1, A2, A4, A5, A6 are five independent docs pages
touching five different files — run them concurrently. A3 is code but creates a new module and
a new flag, colliding with nothing. **A3 should go first in priority order**, because it is
what lets the user fill in A8, and A8 blocks B2, which blocks most of wave B.

**A8 and A9 are both delivered, so wave B is fully unblocked and no item is user-gated.** A10
remains as in-engine verification of A8's derived binding rule, and is self-serve once D29
lands — it needs nothing further from the user.

**Within wave B**: B1 blocks everything (every other item reads `WeaponDef`). B3 → B4 → B5 is a
chain. B6/B7/B8 all need B2 for the slot model but are independent of each other. B9/B10 need
B3 but nothing else, so guided work can run alongside gun work.

**Within wave C**: C1 blocks C2, C3, C4, C7. C5, C6, C8 are independent once C1 lands. C6
(ballistic `ObjectMotion`) touches `AnimRuntime.cs` and can in fact start immediately after
wave A — it is a self-contained fix to an already-identified skipped branch.

**Wave D and E are almost entirely independent of each other** and can run concurrently with
late wave B/C, with one caveat: D1/D2 need B15's surface classification to choose an `IMPACT`
variant, and E4's reticle needs B13's ballistics to project the impact point.

**File contention to watch.** Waves B and C both reach into `FlightController.cs` (B for input
and firing, C for the collide path). D3 and C4 both edit `AnimRuntime.cs`'s event dispatch. E2
and E3 both edit `GaugeCluster.cs`. Give each concurrent agent a stated file-ownership boundary.

---

## Items

### A1 ☑ `docs/formats/weapons.md` — the ballistics table — **LANDED (2026-07-24)**

**Goal.** A complete public reference page for `weapons.zrd.json`, and a fix to `zrdr.md`'s
family index, which omits the file entirely.

**Evidence.** 48 entries, key coverage and structure as surveyed above. `docs/formats/README.md`
is the index and the shared reader conventions; this page joins it.

**Approach.** Document every key with its measured range and meaning; the `FIRE`/`FLYOUT`/
`IMPACT` binding structure including the five surface classes; the caliber × ammo-type matrix
and its damage-tradeoff rule; the player/AI tier split; the `CLUSTER_SIZE` / `AMMO_LIMIT`
relationship **confirmed by playtest (A9)**: per-slot allotment vs purchase cap. Cross-reference the `MSG_WEAP_*` display
names through `messages.json` (IDs 12124–12160). Note the CC-BY licence header used by the
other pages there.

**Verify.** Every one of the 48 entries is accounted for; every key that appears in the file
appears on the page; `zrdr.md`'s index lists `weapons.json`.

### A2 ☑ Gun mounts, markers and the airframe gun-group enum — **LANDED (2026-07-24)**

**Goal.** Document the marker rig and the mount-name enum, so the loadout binding has a
published basis.

**Evidence.** `IDS_AIRFRAMEGUNGROUPNAMES` at IDs 3060–3079 (18 names, contiguous but with gaps
at 3065 and 3078 — check whether those IDs exist and are something else). Uniform 8 firepoints
+ 8 pylons per plane; Kestrel 7 with `fp7` at x = 0.00. Duplicated coordinates on Balmoral and
Brigand, explained by the `… 2` mount names and confirmed by the user for the Brigand. `target`
marker, one per plane root. Gun mesh nodes `fgun`/`rgun`/`bgun0..3`/`hgun`/`hgun2`.

**Approach.** A per-plane marker table with positions. Document the mount-name enum with the
gap check. Document `target` as the aim point and mark it **M4 scope**. Document the 5/5
turret ↔ W4 correlation, **stated as a correlation on n=11**, and the Balmoral's two-turret
case.

**Verify.** The per-plane table regenerates from `extracted/planes/nodes.json` by script; the
duplicate-coordinate claim is reproduced for both Balmoral and Brigand.

⚠ **Do not restate the disproven index-arithmetic rule as fact anywhere on this page.**

### A3 ☑ Marker reference tool — `--dump-markers` + viewer overlay — **LANDED**

**Goal.** Let the user see and name every firepoint and pylon on a plane, so A8 is fillable.
(A8 has since been delivered by the user; A3's live value is now the committed instrument
`markers.md` regenerates from, and the overlay A10 will use for in-engine placement checks.)

**Landed.** `src/Mech3/MarkerRig.cs` extracts the rig from planes.zbd; two front-ends share it:
(a) `--dump-markers[=plane]` prints a per-plane table (name, plane-frame position, mirror pair,
co-located mounts) to stdout and `./.scratch/markers_dump.txt`, windowless under `--headless`;
(b) `src/UI/MarkerOverlay.cs` (`--viewer`, key K, opened at launch by `--markers`) draws each
marker as a coloured gizmo + label — firepoints orange, **shared-mount firepoints magenta**,
pylons cyan, target green. Every gizmo dot always shows; the labels de-clutter nearest-first
(firepoints before pylons) with co-located names stacked so both read.

**Verified.** The dump reproduces `markers.md` exactly (Bloodhawk x-values, Devastator's two-axis
triples, Peacemaker's asymmetric layout, Kestrel's lone centreline fp7, and both Balmoral
`fp1≡fp5…` and Brigand `fp1≡fp4…` co-located sets). The overlay's Bloodhawk labels agree with the
dump; Balmoral/Brigand duplicates render as visibly magenta co-located dots; screenshots of all
11 planes are in `./.scratch/`.

⚠ **Correction to markers.md folded in:** `target` is **not** always the plane origin — Bloodhawk
(0,0,−1), Warhawk (0,+0.59,+0.67), Firebrand (0,+1.08,−0.98) and Hoplite (+0.06,+0.04,+0.33)
carry offsets; the other seven sit at the origin. M4-scope, but the "identity transform" claim was
overstated.

### A4 ☑ `docs/formats/destructibles.md` — the world-destructible model — **LANDED (2026-07-24)**

**Goal.** Document the `HEALTH` / `DAMAGE_SEQUENCE` / `ACTIVATION` mechanism end to end.

**Evidence.** The census, health histogram, sequence-shape counts and death-sequence event
breakdown above. `extracted/C1/zrdr/ap_h2otwr.zrd.json` is the canonical worked example
(HEALTH 60, two damage stages at 18 and 36, a `destroy_h2twr` sequence, a ballistic
`OBJECT_MOTION` debris section, and a puffer sequence).

**Approach.** Document def→node binding via `NAME` wildcard + `ANIMATION_ROOT_NAME`, the
`healthy`/`destroyed` node convention in gamez (422 such nodes in C1 alone), the
`DAMAGE_SEQUENCE` threshold script, the `destroyit`/`unknown_seq` death sequence, and the
`ANIM_HEALTH n` = `health <= n` semantics. Include the full 44-object collide table.

**Note the existing engine bug in `HideUncoveredDestroyed()`** (`AnimRuntime.cs:1934-1947`):
its substring match on `"destroyed"` wrongly wiped C1's `ref_tank_dest`, which is the *healthy*
tank group. C4's destructible work should not re-introduce a suffix rule.

**Verify.** The census numbers regenerate by script; the worked example matches the file.

### A5 ☑ The weapon effect-reader family — **LANDED (2026-07-24)**

**Goal.** Document the ~25 effect readers and the 54 gamez projectile prototype roots that
`FIRE`/`FLYOUT`/`IMPACT` reference, so waves B and D have a map.

**Approach.** One page (or a section of A1 if it stays small) covering each reader's role and
the prototype roots it drives. `muzzle_burst.zrd.json` defines `muzzle_burst_slug` as `ON_CALL`,
`EXECUTION_PRIORITY 6`, LOD-gated via `CALL_ANIMATION muzzleburst_effects AT_NODE`.
`gunhit.zrd.json` defines per-caliber impacts with `PLAYER_RANGE 500` gating and a
`PUFFER_STATE blacksmokepuffer`. Record the muzzle/tracer texture naming axis
(`slug`/`dum`/`ap`/`mag` × `_muzzle1/2`, `tracer_*`), which maps 1:1 onto the `wep_X0..X3`
ammo-type axis.

**Verify.** Every `FIRE`/`FLYOUT`/`IMPACT` target named across all 48 weapon entries resolves
to either a documented reader or a documented gamez prototype root. **Report any that do not**
— an unresolved name is a data gap worth knowing before wave D.

### A6 ☑ `vehicle.md` — retire the deferral — **LANDED (2026-07-24)**

**Goal.** Decode what `vehicle.md:30` defers as "dogfight-milestone scope, undecoded here".

**Approach.** Document the `weapons` 5-tuple `[weapon_id, count, ?, ?, range]` — positions 3–5
are **inferred, not confirmed**; the 5th is 10000 for the player catalogue vs 800–900 for AI,
suggesting AI engagement range. Document `cannon_jam` (`heat_safe_limit 1000`,
`heat_dissipation_rate 50`, `jam_chance 0.1`, pairing with `FIRING_HEAT`) and mark it
**backlogged, not implemented**. Document the AI-only `armor`/`health` pair
(bloodhawk 64/64 … balmoral 100/100, patrolboat 0/40) which `PlaneStats` does not read.
Document `turrets` and mark it **M4**. Document `bullethole_anims`.

**Verify.** No key in any of the 75 vehicle defs remains undocumented; the "undecoded here"
line is gone.

### A7 ☑ Stock-loadout file format + seed — **LANDED**

**Landed.** `CSVM/data/stock_loadouts.json` (a new committed-config home, `CSVM/data/`, loaded via
`res://` by B12) + `docs/formats/loadouts.md`. Seeded from the user's stock table; the `markers`
arrays are the binding rule's explicit output, checked in (not runtime-computed) so a wrong one is
a visible fix. Gun `caliber`+`ammo` resolve to `wep_{N+k}` (stock slug → `wep_N`); turret slots
carry `"turret": true`, inert in M3. **Verified independently against the committed file:** all 11
parse, every `markers` entry exists on the model, every marker matches the binding rule, every
derived gun `wep_*` and each `hardpoints.stock` resolves in `weapons.zrd.json`, and the turret set
is exactly the 5 airframes (Balmoral the only two-turret one). **B12 is unblocked.**

**⚠ Corrected during authoring:** an early draft resolved stock guns as `wep_N0` (e.g. `wep_300`);
the matrix is `wep_{N+k}`, so slug caliber-30 is **`wep_30`**, not `wep_300`. The verify step
caught it — the intended "visible data fix" property working as designed.

**Original approach (kept for reference).** A committed JSON file (hand-authored config describing
the original, the same category as `docs/formats/` — **not a game asset**, so the no-assets rule
does not apply). Shape, per plane def:

```json
"pbloodhawk": {
  "guns": [
    {"slot": 1, "mount": "Inner Wing Guns", "caliber": 40, "markers": ["firepoint7","firepoint8"]},
    {"slot": 2, "mount": "Outer Wing Guns", "caliber": 30, "markers": ["firepoint5","firepoint6"]}
  ],
  "hardpoints": {"count": 3, "stock": "wep_06"}
}
```

Ammo type is separate from caliber so the configurator can later vary it: stock resolves
caliber *N* + `slug` → `wep_N0`. Turret slots are **represented but flagged inert** so the M4
work has somewhere to land. `markers` is authored, not derived (A8).

**Verify.** All 11 planes parse; every named marker exists on that plane's model; every
`wep_*` id resolves in `weapons.json`.

### A8 ☑ **[USER]** Mount-name column — **DELIVERED 2026-07-22**

**Delivered.** The full per-plane mount table is above, all 44 slots. Every name is a verbatim
entry from the 3060–3079 enum, which is complete and contiguous (the gaps this plan originally
noted at 3065 and 3078 are `IDS_CENTERGUNS` and `IDS_CENTERGUNS2`, both of which the table
uses).

**Consequence: the binding rule fell out of it.** Cross-referencing the names against measured
firepoint positions yields **slot 𝑛 → `firepoint(9−2𝑛), firepoint(10−2𝑛)`** — see the binding
section above for the four independent confirmations. So A7's `markers` field can be
**generated** rather than hand-authored, with the generated values checked into the loadout
file explicitly (not computed at runtime) so a wrong one is a visible data fix.

**Still open:** A10 verifies the rule against the muzzle-flash captures. **B12 is unblocked.**

### A9 ☑ **[USER]** `CLUSTER_SIZE` = rounds per slot — **CONFIRMED 2026-07-22**

**Result.** User flew a stock Bloodhawk: **9 HE rockets, 3 per hardpoint.** The prediction was
9 (3 pylons × `CLUSTER_SIZE 3`); a count of 3 would have meant per-plane rather than per-pylon.

**Therefore:** rocket capacity = **pylon count × `CLUSTER_SIZE`**; gun capacity =
`CLUSTER_SIZE` per gun group with independent counters; **`AMMO_LIMIT` is a purchase cap**, not
a carried amount (and does not exist at all on rocket entries).

**Consequence for B17:** capacity is settled, but *rounds released per trigger pull* is a
separate question this test does not answer — B17 still must not assume.

### A10 ☑ Verify the binding rule and the flash appearance — **LANDED (2026-07-24)**

**Landed — no code, a verification (as the plan's preamble anticipates).** The binding rule is
**confirmed**: cross-referencing `--dump-loadout` (each slot's bound firepoints) against
`--dump-markers` (their plane-frame positions) for all 11 aircraft shows **every firing gun group
lands on the mount its name says**. Decisive cases: **Devastator 3/3 on both axes** (Low/Upper × 
Inner/Outer — the only two-axis airframe), **Peacemaker 2/2 with sides correct** (Center on the
centreline, Right Fuselage at +x — the only asymmetric one), **Bloodhawk** (40-cal inner |x|=3.22 <
30-cal outer |x|=3.66) and **Brigand** (W1/W2 both at |x|=2.34, `fp7≡fp6`/`fp8≡fp5`) reproducing the
user's playtests. **Firebrand/Kestrel soft spot resolved:** their firing guns are correctly ordered
(inner<middle<outer / on-centreline); the reverse-index rule only seats the **inert `Rear Turret`** on
the outermost firepoint, which fires no flash in M3 (turret binding is M4). Windowed captures
corroborate: the Bloodhawk's muzzle flash renders on the wing at the selected group (HUD shows only
that group depleting — B18), and the Peacemaker overlay shows `firepoint7` on the centreline /
`firepoint5` on the right fuselage. **Appearance:** the game's own `slug_muzzle1`/`2` flipbook (warm
orange→yellow radial burst) matches `OriginalScreenshots/MuzzleFlash1.png`; D29 renders `slug_muzzle1`,
and stock loadouts are all-slug so `slug` is correct (the 2-frame flip + per-ammo dum/ap/mag textures
stay the documented D29 refinement). No discrepancy found for any firing gun. Docs: `markers.md` +
this plan's binding section flipped from "pending A10" to "confirmed". Screenshots in `./.scratch/`
(rendered plane frames — not committed, per the no-assets rule).

**Goal.** Confirm A8's derived binding rule puts flashes on the right mounts, and that the
flash itself looks right.

⚠ **Correction (2026-07-22).** An earlier draft of this item proposed matching
`OriginalScreenshots/MuzzleFlash1-3.png` against the marker overlay and asked the user which
plane each showed. **That was misconceived** — those three files are ~100×80 px *crops of a
single flash*, far too zoomed to show which firepoints are emitting. They are appearance
reference, not placement evidence. Do not ask the user to identify them.

**Placement** is not derivable from data, but it is already substantially verified by the
user's own playtests: Bloodhawk stock is 40-cal **inner wing** + 30-cal **outer wing**, and the
Brigand's W1 and W2 **share the outer mount**. Both are reproduced by the reverse-index rule,
which additionally agrees with measured geometry on all 11 aircraft (see the binding section).
The remaining check is in-engine and self-serve: once D29 lands, fire each plane, screenshot,
and confirm flashes appear on the mounts the table names — the Bloodhawk and Brigand against
the user's direct observations, the Devastator and Peacemaker against their distinctive
two-axis and left/right layouts.

**Appearance** *is* fully data-derived: the 8 `<ammo>_muzzle[1|2].png` textures (the `1`/`2`
pair is a two-frame flipbook — see the existing analysis at
`docs/formats/anim-definitions.md:319-320`), `muzzle_burst.zrd.json`, and the gamez prototype
roots. The three crops are a useful sanity check on colour, size and airframe-relative scale.

**Verify.** A per-plane firing screenshot set in `./.scratch/`, each flash on a named mount;
any discrepancy reported with the mount and firepoint names involved. **Look at Firebrand and
Kestrel W3 first** — both are turret airframes where one firepoint pair is stranded, and they
are the rule's two soft spots.

---

### B11 ☑ `WeaponDefs.cs` — typed reader over `weapons.json` — **LANDED**

**Landed.** `src/Flight/WeaponDefs.cs` (modelled on `PlaneStats`): `WeaponDefs.Load` → 48 typed
`WeaponDef`s keyed by `wep_*`, plus the `NO_AMMO_WARNING` empty-clip sound. Exposes ballistics,
damage, allotment, the class flags, the specials (BEEPER/TANGLER/…), and the `FIRE`/`FLYOUT`/
`IMPACT` bindings with `IMPACT` keyed by a `SurfaceClass` enum (the six classes A1 corrected —
`default`/`water`/`buildings`/`player`/`enemy`/`quicksand`). `DESC` resolves through `Messages`.
Each def carries an `UnhandledKeys` tripwire (empty for this install).

**Verified.** `--dump-weapons` (a new headless verification tool, mirroring `--dump-markers`) parses
**all 48 with NO unhandled keys**, and the dump cross-checks against A1's `weapons.md`: the
`wep_50`–`53` damage matrix (slug 6.25/6.25, dum-dum 3.125/9.375, AP 9.375/3.125, mag 6.75/5.75),
the turret gun's `AMMO_LIMIT 9999` + no `CLUSTER_SIZE`, `wep_06` HE (`CLUSTER_SIZE 3`, no
`AMMO_LIMIT`), the torpedo's `TORPEDO`/`TARGETABLE`/`DAMAGES_ZEPPELIN` flags + `FLYOUT_HEALTH`, and
DESC display names ("30-cal. slug machine gun"). **B12–B20 unblocked.**

**Implementation notes for the rest of wave B.** Flags are `KEY,null` in the data → `ZrdrDict`
bare-flag handling → `Has(key)`. `IMPACT` is walked as raw class/value pairs (not via `ZrdrDict`)
so a null class value (`enemy` = "no effect") is skipped, not read back as an empty binding. Added
`ZrdrDict.Keys` for the unhandled-key check.

**Original approach (for reference).** Follow `PlaneStats.cs`'s shape as the model for a typed
reader over a zrdr file. Parse the flat `BALLISTICS` alternating list; expose ballistics, damage,
ammo, the class flags (`CANNON`/`ROCKET`/`HIGH_EXPLOSIVE`/`TARGETABLE`/…), and the `FIRE`/`FLYOUT`/
`IMPACT` bindings with `IMPACT` keyed by surface class. Resolve `DESC` through `Messages`.

### B12 ☑ `Loadout.cs` — loadout reader, slot model, marker resolution — **LANDED**

**Landed.** `src/Flight/Loadout.cs` (in Flight, not Mech3 — it depends on `WeaponDefs`, so that
keeps the layering; A7's stale `src/Mech3/` pointers were corrected). Two layers: `StockLoadouts.Load`
parses `stock_loadouts.json` (default `res://data/`, not `--data-root` — it is committed engine
config) into `LoadoutDef`s; `Loadout.Bind(def, builtPlane, WeaponDefs)` resolves each gun slot's
markers to live muzzle `Node3D`s (by `cs_name`, like MarkerOverlay) and its caliber+ammo to a
`WeaponDef` (`GunWeaponId` = `wep_{N+k}`), and each hardpoint to its `pylonN`, yielding `GunGroup`s
(independent ammo from `CLUSTER_SIZE`) + `Hardpoint`s. Turret slots bind but `IsTurret` (inert).
`--loadout=<def>` overrides which def binds.

**Verified** with a new headless tool `--dump-loadout[=plane]` (builds each plane, binds, reports):
**all 11 bind with every marker resolved.** The Balmoral reports **two separate .50 counters** (slot1
+ slot2 `wep_50`, 2000 each) plus its two inert turrets; the Bloodhawk's counters differ (`wep_40`
2400 / `wep_30` 2800) and its **9 total HE rockets (3×3) match the A9 playtest**; the Kestrel's W1
resolves to the lone centreline `firepoint7`. The missing-marker path is a **loud throw**, not a
silent skip — demonstrated with `--dump-loadout=Kestrel --loadout=pbloodhawk` (the Bloodhawk
loadout wants `firepoint8`, which the 7-firepoint Kestrel lacks): `!! marker 'firepoint8' not found
on the built plane`. **B16/B17/B18 unblocked.** The `--loadout=` flight-side effect lands with the
firing code (B16).

### B13 ☑ `Projectile.cs` — spawn and integration — **LANDED (2026-07-24)**

**Goal.** A pooled projectile with the data's own ballistics.

**Approach.** Integrate `VELOCITY`, `ACCELERATION`, `GRAVITY` (5 entries only), and expire at
`RANGE`. Inherit launch-platform velocity. Pool aggressively — a 10.5/s gun over several groups
generates a lot of entities. Keep the integration step fixed and independent of frame rate:
`docs/verification.md` warns that frame numbers here are vsync-capped floors.

**Verify.** Measured muzzle-to-impact time over a known distance matches `RANGE`/`VELOCITY`;
no allocation churn in a sustained-fire `--perf` run.

### B14 ☑ `FLYOUT` model instancing — **LANDED (2026-07-24)**

**Landed.** Rockets fly the original's own projectile mesh instead of the B17 orange stand-in
streak. On a rocket `Spawn`, `ProjectilePool` resolves `weapon.Flyout.Model` (the `FLYOUT` `MODEL`
name) to a chapter-gamez prototype root via `GameZ.FindByName` and instances it with the world
`SceneBuilder.BuildSubtree` **collision-exempt** (`collisionSkip: _ => true`, so a rocket obstructs
neither another round nor the world hit-test); the pool gained the world gamez + its `SceneBuilder`,
threaded through its constructor at the `PlaneViewer` creation site. The resolved node is cached per
model name; the body is freed on impact / expiry / `Clear`. The 15 `ROCKET` entries name **12
distinct** prototype roots — `he_rocket` (BOOM/stock HE), `ap_rocket`, `incendiary` (9M/SEEKER/FW),
`flak`, `sonic`, `flash`, `beeper`, `scatter`, `smoker`, `a_torpedo`, `reararc`, `aaflak` — all
present as named nodes in every chapter's gamez.

**Orientation** is uniform: every rocket mesh is authored **nose-along-(-Z)** (measured — `he_rocket`'s
rendered LOD is mesh 66, 0.3 m dia × 1.5 m long, `z ∈ [-1.5, 0]`), matching the muzzle-forward
convention, so `Basis.LookingAt(velocityDir)` aims the nose down the round's flight.

**Measure-before-choosing (the plan's caveat), resolved: guns keep the tracer quad, only rockets get
a mesh.** A rocket lives ~0.83 s at `FIRE_RATE` 1/s (≤1 alive per player); a gun fires ~10/s living
~1 s (dozens alive), so a mesh per gun round would be wasteful against the existing MultiMesh tracer.
A rocket with a body trails a slim exhaust streak (`RocketExhaustScale`); the old chunky
`RocketStreakScale` is now only the fallback for a chapter missing the prototype. The `FLYOUT`
`MODEL_ANIMATION` smoke trail stays deferred to the D-wave.

**Verified.** Build clean; an 8-chapter headless `--fire-rockets` regression instances `he_rocket`
(breadcrumb: 1 mesh) in every chapter with **zero** real errors and unchanged node counts; a runtime
breadcrumb reading the model's applied world basis back reports **`nose·velocity = 1.000`**
(nose-forward, non-circular). Windowed C1 captures (`./.scratch/`) show rockets leaving the pylons,
flying forward, and impacting terrain ahead. **A pixel-crisp in-flight close-up remains the owed
at-the-controls playtest (shared with B17)** — a 1.5 m round at ~1260 m/s is not chase-cam-photographable
without its (deferred) smoke trail.

**Original approach (for reference).** `FLYOUT` names either a `MODEL` (`slug.flt`, `ap_rocket`,
`a_torpedo`) or a `MODEL` + `MODEL_ANIMATION` pair. Instance from the gamez prototype roots via
`SceneBuilder`. Guns need the cheap path (tracer quad, item D33) rather than a mesh per round —
measure before choosing.

### B15 ☑ Hit detection + surface classification — **LANDED (with the B13/B16/D29/D30/D33 batch, 2026-07-24)**

**The whole "guns fire → tracers fly → hit → impact effect" batch landed together** (B13, B15, B16,
D29, D30-partial, D33), since no piece is testable alone:
- `src/Flight/Projectile.cs` (`ProjectilePool`, B13): a shared-world pool integrating the data's
  ballistics (VELOCITY/ACCELERATION/GRAVITY, expire at RANGE), inheriting launch velocity; fixed
  array, no per-round alloc. `Spawn` applies the CANNON_SPREAD cone and flashes the muzzle.
- **Hit detection (B15):** a per-step world raycast. The flying plane has **no physics body**, so a
  round never hits its own launcher and `player`/`enemy` are unreachable in M3 — only
  `default`/`water`/`buildings` occur. Surface class comes from the struck collider's
  `SceneBuilder.SurfaceMeta`, **stamped at build time** from the mesh's dominant material texture
  (`AttachCollision`), so the classification is data-driven, not a runtime name heuristic.
- **Gun firing (B16):** Space / pad-B (`--fire` for scripted runs). Each firable group runs its own
  FIRE_RATE clock, alternating muzzles so the group's total rate = FIRE_RATE, drawing from its own
  `CLUSTER_SIZE` ammo counter; a dry group sounds `snd_emptyclip` once; refill on respawn;
  `--infinite-ammo`. Turrets excluded (inert). An interim HUD ammo line stands in for E36.
- **Muzzle flash (D29) + tracers (D33):** additive billboard bursts at the firepoints; velocity-
  aligned (non-billboard) tracer streaks. **Impact (D30):** the per-surface `IMPACT` **sound** +
  a stand-in spark sprite.

**Verified** (C1, screenshots + logs in `./.scratch/`): guns fire; ammo depletes per group; the
Fury's 70-cal and 30-cal deplete in a **6:8 ratio = their FIRE_RATEs**; `--infinite-ammo` shows `∞`
and never drops; tracers + muzzle flash render; rounds hit **terrain → `Default`** and **sea →
`Water`** (correct collider surface tags); impact sprites + sounds fire. **Remaining:** the exact
named `IMPACT`/muzzle effect animations (D30/D32 depth), per-ammo tracer textures, `buildings`
confirmed only by the shared code path (same as water), and the empty-clip drain (2000+ rounds —
a playtest check). B17 (rockets) and B18 (selectors) are next.

**Goal.** Register hits and pick the right `IMPACT` variant.

**Approach.** Raycast or swept test per projectile step against world geometry and plane
colliders. Classify the struck surface into the data's six classes — `default`, `water`,
`enemy`, `player`, `buildings`, `quicksand` — since `IMPACT` is keyed by them (Wave A found the
sixth; the plan's earlier "five" is corrected above). Water is identifiable from
existing material/texture classification; `buildings` likely from the destructible registry
(C21). **`enemy` has no meaning in M3** (nothing to hit) — leave it wired but unreachable.

**The collision world this lands into changed on 2026-07-22 — read these two before designing
the hit test.** M2 polish 3 item 5 landed: `SceneBuilder.ClassifyBillboard` now serves all four
call sites, and **clutter plus every gamez billboard lost collision**, so projectiles will not
stop on tree sprites. It also fixed a live bug worth knowing — `skywal*` is a *building*
texture that `IsCloudOrSkyTexture` had been exempting, leaving C4's sky-city pods and `g74`
(395×135×275 m) flyable-through. Item 6 then added **79,306 buildings to C5 and 10,261 to C2**,
colliding as a merged trimesh per 1024 m region (C5 flight load +3.5 s, 2.55 M collision
triangles). That is a large amount of new collision geometry for every projectile step to test
against — **measure before choosing a broadphase.**

**Verify.** A shot into water plays the water variant, into terrain the default, into a
building the buildings variant; a scripted `--screenshot` run captures each.

### B16 ☑ Gun firing — rate, ammo, spread, empty-clip — **LANDED (2026-07-24)**

**Goal.** Guns that fire like the original's guns.

**Approach.** `FIRE_RATE` (rounds/s, 8.0–10.5 for the player calibers) gates spawning.
**One ammo counter per gun group** (playtest-confirmed), initialised from `CLUSTER_SIZE`
(A9-confirmed). `CANNON_SPREAD` (6.0 on the 30-cal) as a dispersion cone, scaled by `CALIBER`.
`LOOPED_SOUND_NAME` for the firing loop; `NO_AMMO_WARNING` → `snd_emptyclip` when dry. Refill on
respawn (R). `--infinite-ammo` (item F43) for frictionless testing.

⚠ `FIRING_HEAT` and `cannon_jam` are **explicitly out of scope** (decision 4) — parse and
ignore, and add the backlog entry rather than implementing them opportunistically.

**Verify.** Rounds/second matches `FIRE_RATE` over a timed burst; dispersion at a fixed range
matches the `CANNON_SPREAD` cone; the counter empties at the expected round count and the
empty-clip sound plays exactly once.

### B17 ☑ Hardpoint firing — **LANDED (2026-07-24)**

**Landed.** `FlightController.UpdateRockets` + `NextArmedHardpoint`: the rocket trigger (**F** /
gamepad **A**, `--fire-rockets` for scripted runs) launches **one rocket per discrete pull** — a
human pull fires once; only `--fire-rockets` auto-repeats — drawn from the next pylon that still
holds ordnance, **round-robin across the pylons**, gated by the weapon's `FIRE_RATE` (1.0/s for
every rocket, i.e. one launch per second). Each launch depletes that pylon's own `CLUSTER_SIZE`
counter; a pull with every pylon empty sounds the empty-clip cue once. Refill on respawn.
The pad-A binding does not collide with pad-A respawn: respawn only fires from the crashed /
run-complete screens, which this live-flight path early-returns before reaching. Rockets reuse the
B13 `ProjectilePool` via the same `Spawn` (their VELOCITY 1200 / RANGE 1000 / no accel-or-gravity
need no special integration path); `IsRocket` already tints them orange, plus a chunkier streak
(`RocketStreakScale`) as a stand-in until the `FLYOUT` `he_rocket` MODEL mesh lands (B14, still ◐).

**Verified.** A headless stock-Bloodhawk soak (`--fire-rockets`, finite ammo) launched **exactly
9 `wep_06` (HE) rockets** — 3 pylons × `CLUSTER_SIZE 3`, matching the A9 playtest — cycling
`pylon1 → pylon2 → pylon3 → pylon1 …` and depleting each 3→2→1→0 independently, then stopped (dry).
The infinite-ammo run confirmed the 1 s cadence holds. (The headless framebuffer capture is
unavailable in this build, so the in-flight rocket screenshot is deferred to the owed playtest;
per-pylon origin is proven by the launch log naming each `pylonN`.)

**⚠ TUNE / playtest.** The 1.0 s cooldown is the data's `FIRE_RATE`, not a measured feel; the
F / pad-A binding is a design choice (guns=B, rockets=A is the natural two-weapon pad layout).
Both are flagged in `backlog.md` for the owed firing playtest.

**Goal.** Rockets launch from pylons and deplete correctly.

**Approach.** Each hardpoint holds one ammo type in `CLUSTER_SIZE` quantity (A9-confirmed:
stock Bloodhawk = 3 pylons × 3 = 9 HE). **One trigger pull releases ONE rocket from ONE
hardpoint** (user-confirmed 2026-07-22) — not a ripple across pylons and not a slot salvo, so a
stock Bloodhawk takes 9 pulls to empty. Cycle which hardpoint sources the next round.
Launch from that pylon's marker. Decide and document whether a trigger pull releases one round
**Cooldown:** every rocket entry has `FIRE_RATE 1.0` against 8.0–10.5 for guns, i.e. one
launch per second. The user did not confirm a cooldown by observation, so treat 1.0 as the
data's answer and a TUNE candidate rather than a measured fact.

**Verify.** A stock Bloodhawk fires exactly 9 HE rockets over 9 trigger pulls; each launch originates
at the correct pylon (screenshot against the A3 overlay).

### B18 ☑ Weapon selectors — **LANDED (2026-07-24)**

**⚠ Design corrected during implementation (user, 2026-07-24):** the original **fires only ONE gun
group at a time — there is no ALL**. Decision 7's "(plus ALL)" is struck; the gun selector cycles
through the firable groups and exactly one is active. (This also means the guns-batch behaviour of
all groups firing at once — never playtested — was wrong; it is fixed here.)

**Landed.** `FlightController.CycleWeaponSelectors` + the `_gunSel` / `_rocketSel` state. Two
independent selectors, edge-detected, both keyboard + pad and both respecting `--no-pads` (they
route through `PadPressed` → `Pads.For`):
- **Gun selector** — **G** / gamepad **D-pad Left** cycles the firable groups (turrets excluded);
  `UpdateGuns` fires only the selected one. Default = the first group. `--gun-select=N` (0-based)
  is a headless testing hook for the initial group.
- **Hardpoint selector** — **H** / gamepad **D-pad Right** cycles the distinct loaded ordnance
  types; `NextArmedHardpoint` launches only the selected type. Stock loadouts carry one type (all
  HE), so it is a no-op until mixed loadouts land — the mechanism is data-driven and in place.

Both selectors survive a respawn (a player's pick is not ammo). The interim HUD ammo line brackets
the selected gun group. Feeds E36's readout when that lands.

**Verified.** Headless Balmoral soaks (two `wep_50` groups) using the per-group first-shot log:
**default → only "gun group 1 (Inner Wing Guns)" fires**; **`--gun-select=1` → only "gun group 2
(Outer Wing Guns)" fires** — exactly one group at a time, and the selector picks the right one. The
selector-cycle button itself is simple modular arithmetic (playtest-checkable); the fire *filter*
is what these runs prove.

**Goal.** Two independent selectors, per decision 7 (as corrected above).

### B19 ⊘ Guided flight — deferred to M4

**Revised 2026-07-24 (user).** The original has **no manual ground-target selection**; its auto-aim
is game-handled and can only be pointed at **enemy planes** (the same target-cycle as stunt-race
objective selection). M3 has no enemy planes, so nothing a guided missile could authentically lock
exists. Rather than ship an inauthentic ground-lock selector, **M3 fires every rocket — the Seeker
included — as dumbfire** (the ballistic pool it already flies through; the firing path needed no
change). All homing moves to M4.

**What stays true of the data, for M4.** Guidance is `TURN_RATE`, **not a flag** — only the Seeker
`wep_11` (1.25) homes; the other 13 carry the 0.001 sentinel (`WeaponDef.IsGuided` encodes this).
`LOCK_ON` is **universal** (even dumbfire HE carries 1.3) because it is the auto-aim / lead-solution
convergence time for *every* weapon, not a steering promise — so it is not the discriminator. The
ballistic profile (`ACCELERATION` 16, `GRAVITY` 5) is **already integrated** by `ProjectilePool`.
When M4 adds enemy planes: steer the Seeker at `TURN_RATE`, fuze at `DETONATION_DISTANCE` (13) /
`IMPACT_PROXIMITY` (14) / `DETONATION_DOT_PRODUCT` (3) / `DETONATION_TIME`, honour the torpedo's
`RANGE_MINIMUM` arming distance, and drive acquisition off the plane target-cycle (the `target`
aim-point marker A2, reusing `MissionTargets`/`MarkerHud`). `FLYOUT_HEALTH 10` + `TARGETABLE` also
make the torpedo itself shootable — M4.

### B20 ⊘ Ground lock-on — deferred to M4

Folded into the B19 revision above. Acquisition in the original is the **enemy-plane** target-cycle,
which is M4 scope; there is no authentic ground lock-on to build. `LOCK_ON_LEAD` (3 entries) is the
lead computation M4 will need.

---

### C21 ☑ Per-instance mutable HP + destructible registry — **LANDED (2026-07-24)**

**Landed.** `src/Mech3/DestructibleRegistry.cs` holds one `Instance` (current HP, max HP,
healthy/damaged/destroyed state) per `(def, anchor)` pair — every `AnimDefinition` with
`HEALTH > 0`, resolved to each world node its wildcard `NAME` binds — built in AnimRuntime's
bootstrap pass 1 beside RESET_STATE. `EvaluateCondition`'s `AnimHealth`/`AnimHealthRange` read the
live value via `HealthOf(def, anchor)`, falling back to the static `def.Health` for any unregistered
pair. **Keyed per `(def, anchor)`, not per def** (the ⚠ below): a wildcard binds many node groups,
each an independent pool. No damage applied yet (C23), no death sequence (C24) — so a fresh world is
a **provable no-op** (`HealthOf` == `def.Health` everywhere).

**Verified.** Full 8-chapter `--freecam` regression clean (all exit 0, no exceptions, screenshots
saved), registry count reported per chapter: C1 267/196, C1B 108/108, C1C 107/107, C2 574/201,
C2B 104/104, C3 501/202, C4 225/194, C5 568/292 (instances / node groups) — sane against A4's census.
Instances exceed node groups where the reader's wildcard def and the compiler's per-instance defs
both bind the same nodes (C2 `fcpan**`/`grasshut#`/`sign*`/`police*` + compiled twins, object-specific
— not over-matching); node/mesh counts unchanged. **⚠ Handoff to C23:** one struck node can map to
several instances — C23 must resolve it to ONE authoritative instance (prefer the compiled def);
recorded on the `DestructibleRegistry` architecture entry.

**Goal.** Replace the static `def.Health` read with live per-instance state. **Blocks C22, C23,
C24, C27.**

**Evidence.** `AnimRuntime.cs:1043-1049` evaluates `AnimHealth` against the def's authored
value, so every threshold branch is uniformly false in a live world
(`docs/formats/anim-definitions.md:208`).

**Approach.** A registry of live destructible instances keyed by the nodes a def binds to
(`NAME` wildcard + `ANIMATION_ROOT_NAME`), each with current HP, max HP, and state
(healthy / damaged-stage-*n* / destroyed). Condition evaluation reads the instance, not the def.
Keep the def's authored health as the max.

⚠ **One def can bind to many instances** (`NAME` is a wildcard — `ap_h2otwr*`). Per-instance
means per *node group*, not per def. Getting this wrong makes one tower's damage break all of
them.

**Verify.** An 8-chapter freecam regression shows identical rendering to before (no behaviour
change yet); the registry's instance count per chapter is reported and sane against A4's census.

### C22 ☑ `DAMAGE_SEQUENCE` parsing + threshold evaluation — **LANDED (2026-07-24)**

**Landed.** Two pieces: `AnimDefs.cs`'s reader front-end now parses the `DAMAGE_SEQUENCE` block into
a sequence named `DAMAGE_SEQUENCE` (matching the compiled twin, closing the silent-drop gap); and
`AnimRuntime.ApplyDamageStages(instance)` runs that IF/ELSEIF `ANIM_HEALTH` cascade against the
instance's live HP (C21's `HealthOf`), firing the one stage effect for the crossed threshold. It
escalates via a per-instance `DamageStage` — running the cascade only when a **deeper** threshold is
crossed — so each stage's effect fires exactly once whether the effect is a sustained smoke loop or
a one-shot (the gate is required: `CALL_ANIMATION`'s live guard alone does NOT stop a finishing
one-shot like C5's `damage3_mp1zreng11` from re-firing). Nothing calls it in normal play yet — C23's
`WeaponHit` will; `--damage-test` drives it today.

**Verified.** New headless `--damage-test[=name]` (the C22 verifier until F40) sweeps a
destructible's HP full→zero and logs which stage effect fires at which health. Water tower (HEALTH
60, compiled **and** reader-parsed twin): black smoke at HP≤36, fire smoke at HP≤18 (0.60/0.30).
C1 HEALTH-30 AA guns fire at 18/9, HEALTH-60 buildings at 36/18 — thresholds derived per-def from
each object's own `HEALTH`, evaluated against live HP. C5 `reng11` (HEALTH 40, three-stage
{0.85,0.50,0.25}): damage3→damage2→damage1 once each at HP≤34/20/10, re-fire gone. The 8-chapter
`--freecam` regression is byte-identical to the C21 baseline (same counts, no errors) — C22 is a
no-op at world build (`ApplyDamageStages` runs only under `--damage-test`; reader `DAMAGE_SEQUENCE`s
are inert because `WeaponHit` defs never bootstrap).

**Goal.** Progressive damage stages run as authored.

**Evidence.** `AnimDefs.cs:74-95` has no `DAMAGE_SEQUENCE` case — the reader front-end drops it
silently. The compiled front-end already delivers it as an ordinary `AnimSequence`.

**Approach.** Add the case to the reader switch (roughly one line plus a handler). Evaluate
`AnimHealth`/`AnimHealthRange` against C21's live value. Worked example: `ap_h2otwr` at HEALTH
60 calls `sputter_black_smoke_obj` at ≤36 and `sputter_fire_smoke_obj` at ≤18.

**Verify.** Chipping a water tower's HP through 36 and 18 (via F40) starts each effect at the
right threshold and only once.

### C23 ☑ `WeaponHit` activation and damage application — **LANDED (2026-07-24)**

**Landed.** `ProjectilePool.Impact` (B15's raycast reports the struck collider) invokes a new
`DamageSink`, wired in flight to `AnimRuntime.DamageAt(struck, healthDamage)`. `DamageAt` resolves
the collider to its destructible (`DestructibleRegistry.Resolve` — walks the whole parent chain and
takes the nearest **compiled** anchor, because a reader wildcard grabs an inner node the compiled def
does not: the tower's `ap_h2otwr*` matches `ap_h2otwr.flt`, between the collider and the compiled
`ap_h2otwr1` root), spends `HEALTH_DAMAGE` (world objects carry HEALTH only — no armour pool, so
`ARMOR_DAMAGE` is inert against them), runs `ApplyDamageStages`, and marks the instance `Destroyed`
at zero. The death **sequence** (the visible swap + debris) is C24. **Patrol-boat ⚠ resolved:** its
anim def is HEALTH 20 `WeaponHit` (mission archives only), so M3 damages it as scenery through that
path; the AI-vehicle armour+health model (HP 40) stays M4. (Confirmed correct by the 2026-08-13
executable decode: the two models are selected per instance by how it was created, so a placed boat
really is a HEALTH-20 destructible. See the settled note below and
[`docs/org/vehicleDamage.md`](../org/vehicleDamage.md).)

**Verified.** New `--damage-hd=<n>` mode of `--damage-test` (discrete weapon hits via `DamageAt`,
counting hits to destruction): a HEALTH-60 tower dies in **1** hit at HD 60 (HE), **2** at 40 (AP —
worse against buildings), **14** at 4.5 (40-cal), stages at hits 6/10 (HP 33/15); a HEALTH-30 AA gun
in **10** at 3.0, stages 18/9. A `resolve✓` check (from a deep descendant, the collider's node path)
passes on all 16 C1 destructibles. 8-chapter freecam regression byte-identical to the C21 baseline
(no-op at world build — `DamageAt` fires only on real hits). An in-flight `--fly --fire` run confirmed
`DamageSink` is invoked on every impact and correctly no-ops terrain. **Owed:** the in-flight visual
of a specific destructible dying, which pairs with C24's death swap (playtest.md).

**Goal.** Projectile hits actually damage destructibles.

**Approach.** B15's hit reports the struck node; resolve it to a registry instance; apply
damage from the weapon def.

**The damage model — resolved 2026-07-22.** Measured: **world destructibles carry `health`
only.** Across all 16,114 compiled anim defs the schema has exactly one damage field and
**zero** armour-ish keys; `armor` appears only in `vehicle.json`, `weapons.json`
(`ARMOR_DAMAGE`) and `player.json`. **So only `HEALTH_DAMAGE` applies to world objects** —
there is no armour pool to spend `ARMOR_DAMAGE` against.

**The two-pool model, where it does apply, is armour-then-health** (not parallel depletion, and
not damage reduction). The proof is a dominance argument rather than a stated rule: the 50-cal
AP does `9.375` armour / `3.125` health and dum-dum the exact inverse. Under parallel pools
with death on health, **AP would be strictly dominated by dum-dum** — faster at draining a pool
that cannot kill, slower at the one that can — making an entire purchasable ammo tier useless.
Armour-first makes the tradeoff real, and matches the in-game ammo text *"damage equally well
to both armor and internal components."* Corroborating: every aircraft has `armor == health`
exactly, while `patrolboat` and `t_truck` have `armor 0 / health 40` — unarmoured soft targets
taking damage straight to health, which is what armour-first predicts.

**This game therefore has three damage models**, and M3 touches two:
1. **Player planes** — per-part `destroyable_parts` HP. No armour/health pair. Already built.
2. **AI vehicles** — the armour+health pair. Mostly M4, **but `patrolboat` and `t_truck` are
   shootable in M3.**
3. **World destructibles** — `health` only, `HEALTH_DAMAGE` only.

⚠ **Open edge case, now precisely stated (2026-07-22).** The patrol boat is described by **two
systems that agree on shape and disagree on magnitude**:

| Source | Total HP | Stage 1 | Stage 2 | Effects |
|---|---|---|---|---|
| `patrolboat` vehicle def | **40** | `0.60` | `0.30` | `ptboat_50damage`, `ptboat_75damage` |
| `C1/patrol_boat` anim def | **20** | `ANIM_HEALTH 12` (=0.60) | `ANIM_HEALTH 6` (=0.30) | `sputter_black_smoke_obj`, `sputter_fire_smoke_obj` |

Identical 60 % / 30 % thresholds, **2× different total HP**, and different effect animations —
the vehicle def naming a boat-specific pair, the anim def using the generic shared template.
The likeliest reading is that the vehicle def governs the boat **as an AI combatant** and the
anim def governs it **as placed world scenery**, but that is a hypothesis, not a finding.

**Resolve before implementing C23.** M3 only ever sees the boat as scenery, so the anim def
(HP 20) is the probable answer — but verify by shooting one in the original and counting hits,
rather than assuming. Check whether `t_truck` (`armor 0 / health 40`, **no injure_anims**) and
`fueltruck` / `armytruck_destruct` show the same duplication.

⚠ **Settled 2026-08-13, and the reading above was right: both, on different boats.** Executable
decode in [`docs/org/vehicleDamage.md`](../org/vehicleDamage.md). The two models are selected by how
an instance was created and by nothing else, so a placed boat (C1's `ptboat1`–`3` at the refinery,
compiled into `C1/cam_anim` at `health 20.0` / `WeaponHit` from the wildcard def `ptboat*`) is a
destructible on 20, while a boat spawned from an `aiv` roster is a vehicle on 40 with the 0.60/0.30
`injure_anims`. **M3's scenery-only choice of 20 is correct** and needs no revisit. The count-hits
verification proposed here was never needed. `t_truck` is the same shape; `fueltruck` and
`armytruck_destruct` have no vehicle def at all, so they are destructibles only.

⚠ **Note the animation-name trap.** `ptboat_50damage` fires at **60 %** remaining and
`ptboat_75damage` at **30 %** — the names lag their trigger, exactly as
`docs/formats/hud.md` records for the cockpit damage dial ("the anim names lag their effect by
one state"). **Never infer a threshold from an animation's name.**

**Verify.** A tower with HEALTH 60 dies after the expected number of `HEALTH_DAMAGE` hits from
a known weapon; an AP rocket is measurably *worse* than HE against a building, which is the
observable signature of the model being right.

### C24 ☑ Death sequence execution + healthy→destroyed swap — **LANDED (2026-07-24)**

**Landed.** `AnimRuntime.DamageAt`, on the transition to `Destroyed`, calls `RunDeathSequence` which
plays the def's death via `Start(def)` — the def's own Initial sequences ARE the destruction (swap +
debris + puffer calls). **The death swap has no fixed name** (census of ~100 destructibles: `destroyit`
25, `destroy_h2twr` 4, `destroy_twr`, `litehouse_des`, unnamed 56 — never reliably `unknown_seq`, per
A4), but is always `Initial`, so `Start` reaches every case without keying on a name. **~10 defs (the
C1 AA guns) declare the healthy/destroyed pair but author NO swap**, so `ApplyDeathSwap` derives it
from the def's own RESET_STATE (flip the healthy/destroyed/dbase roles it explicitly named), applied
only when RESET declares a `destroyed` node — so `noseballgun` (no destroyed variant) and the fuel
trucks (empty RESET) are left intact, not blanked. Uses the def's explicit OBJECT_ACTIVE_STATE targets
(A4's method), not a world scan; runs per-instance on real death, so it does not fight the bootstrap
safety net.

**Verified.** `--damage-test --damage-hd=` gained a `swap[healthy…, destroyed…]` check on the killed
instance: all **16 C1 destructibles** end `healthy 0/1, destroyed 1/1` (healthy hidden, wreck shown) —
tower via its explicit `destroy_h2twr`, AA gun via the RESET fallback; `reng11` hides healthy and
stages its separate `mp1reng_destroyed.flt` wreck + `large_fireball`; `noseballgun` dies un-blanked;
broad C1 sweep kills all 16 with zero errors. 8-chapter freecam regression byte-identical to the C21
baseline (no-op at world build). **Still stubbed:** debris ballistic `OBJECT_MOTION` (C26) + death
`Sound` (D31), so the wreck shows and smokes but pieces don't tumble and the explosion is silent.

**Goal.** Objects die correctly.

**Approach.** Run `destroyit`/`unknown_seq` on reaching zero. Its event kinds are all already
implemented (`ObjectActiveState` 2028, `InvalidateAnimation` 1286, `StopAnimation` 1260,
`CallAnimation` 2360) except `Sound` (D31) and ballistic `ObjectMotion` (C26). The
`healthy` node deactivates and `destroyed` activates.

⚠ Coordinate with `HideUncoveredDestroyed()` (`AnimRuntime.cs:1934-1947`), the bootstrap safety
net that hides `destroyed` subtrees — once real destruction exists, that pass must not fight it.
Its known substring-match bug (`ref_tank_dest`) is documented in A4.

**Verify.** `--destroy=` (F42) on a representative object in each of the 8 chapters produces
the correct visual swap; the 8-chapter regression is otherwise unchanged.

### C25 ☑ Collider removal on destruction — **LANDED (2026-07-24)**

**Landed — no new runtime code.** C24's death swap already does it: the healthy→destroyed
`OBJECT_ACTIVE_STATE` swap runs `SetSubtreeActive`, which toggles `CollisionShape3D.Disabled`
(`SetCollidersEnabled`) alongside `Visible`, so the death that hides the healthy geometry un-solids
it and the wreck it shows becomes solid. C25's deliverable is the **proof + traps**: the `--damage-hd`
harness gained a `col[off N, on M]` census (world colliders switched off vs on by a kill). Measured:
C2 (Hollywood) `gate1`/`gate2` doors off 1/on 8, `kkgate` off 4/on 12, C1 `m_build01` off 1/on 10, AA
gun off 2/on 1 — every destructible removes its healthy collision on death. **The propane→door chain
is confirmed handled** (`kkgate`'s root is the collidable, shootable `propane` tank, HEALTH 10;
shooting it swaps the gate and chains `genx12`/`tbridg1_fire`/`tbridg2_fire`/`free_the_goose`; the
door is not directly damageable — exactly the original). Two measurement traps → `verification.md`
72/73: (1) collision exists ONLY in the flight build (`Collision = _fly`), so a freecam census reads
zero and lies — the harness forces `|| _damageTest`; (2) a *net* collider delta hides the healthy
removal behind the wreck it adds, so split off/on. 8-chapter freecam regression byte-identical
(`Collision` change gated on `_damageTest`).

**Verified.** `--damage-hd` `col[off,on]` per kill across C1/C2. **Owed playtest:** the destroyed
variant re-adds colliders, so whether a blown-open door leaves a clear passage is the original data's
call — fly through a killed door to confirm (same in-flight aim the C23 playtest owes).

**Goal.** Destroyed doors stop blocking flight.

**Approach.** When a destructible dies, remove or disable the collision bodies belonging to its
`healthy` subtree, and add any the `destroyed` subtree needs. Named cases: `sghangar_doors`,
`studiogate_doors` (C2).

⚠ M2 polish 3 items 5 and 6 (landed 2026-07-22) reshaped which nodes get colliders — clutter
and billboards lost theirs, and city-block decorations gained merged per-region trimeshes.
Read `docs/architecture.md` on `SceneBuilder`/`WorldBuilder` before assuming where a
destructible's collision body lives.

**Verify.** Fly through a destroyed hangar door without a collision; fly into the intact one and
collide.

### C26 ☑ Ballistic `ObjectMotion` — debris — **LANDED (2026-07-24)**

**Landed — no new runtime code, and the Evidence below was stale.** The M2 crash Layer 1 work
(`ec8a731`) already generalized `ObjectMotion`'s ballistic half into `MotionRuntime` (gravity,
`translation_range` arc, `forward_rotation` tumble, `scale` ramp, `run_time` — the exact Approach
list). It is REACHED on a weapon-hit death because the death's `OBJECT_MOTION` events are `Initial`,
so C24's `Start` runs them. **The C24 "debris still stubbed" note was a measurement artifact:** the
launch is SCHEDULED mid-sequence (the water tower's at t=2.2 s), and the kill-and-check harness never
advanced the animation clock, so it saw `debris[0]`. Advancing the death proves it fires — the water
tower launches **2** visible pieces (`h2twr_middle` arcs y≈5→19 in 0.8 s, tumbling, run 5 s), C1
buildings **7** each, passenger planes **2**; no-`OBJECT_MOTION` deaths (`air_gen`, AA guns) launch
**0**. Instrument: `AnimRuntime.BallisticMotionsLaunched` + the harness `debris[N launched]` (which
adds the world to the tree with `ManualAdvance` and ticks past the schedule; `verification.md` 75).
**Ground-rest deferred:** `do_intersections`/`bounce_sequence` (a physics-ray Layer-1.5 follow-up) is
not simulated — the pieces arc and tumble, then the sequence's own `OBJECT_ACTIVE_STATE` hides them.

**Verified.** `--damage-hd` `debris[N]` across C1; 8-chapter freecam regression byte-identical.
**Owed playtest:** the on-screen tumble (needs a rendered death — the same aim the C23 playtest owes).

**Goal.** Turn on a code path skipped as unreachable.

**Evidence.** `AnimRuntime.cs:429` implements only `ObjectMotion`'s spin half and explicitly
skips the ballistic half as "reachable only from OnCall/WeaponHit". 16 death sequences use it.
`ap_h2otwr` is the worked example: `OBJECT_MOTION h2twr_middle {GRAVITY LOCAL -2,
TRANSLATION_RANGE_MIN/MAX, FORWARD_ROTATION, SCALE -0.1, RUN_TIME 5}`.

**Approach.** Implement gravity, translation range, forward rotation, scale-over-time and run
time. `CrashBreakup.cs` already hand-simulates ballistic wreck pieces — **read it first**; the
integration and ground-rest logic may be directly reusable.

**Note.** This item is independent of C21 and can start right after wave A.

**Verify.** The water tower's middle section tumbles and settles; run time matches 5 s.

### C27 ☑ The 44 `WeaponOrCollideHit` collision path — **LANDED (2026-07-24)**

**Landed.** `SweepAirframe`/`HitWorld` now also out the struck `Node`; before the crash/graze
decision `FlightController` offers the hit to `CollideDamageSink` → `AnimRuntime.CollideDamageAt`,
which gates on `def.Activation`. A `WeaponOrCollideHit` object (the **44** — C2 `fcpan01`–`39`, C5
`w_win01`–`04` at 0.01, C5 `agyrobus` at 70) takes `vn × 8` HEALTH_DAMAGE through the same `DamageAt`
a weapon uses (so its death — swap, debris, collider removal — is identical), and the plane flies
**through** it; every `WeaponHit` object (towers, gates, signs) returns false and stays solid, so
ramming it crashes the plane and leaves it intact (⚠ decision 6 upheld — collision damage is NOT
extended to `WeaponHit`). Data confirmed: exactly 44 `WeaponOrCollideHit` defs across cam_anim.

> ⚠ **The "leaves it intact" half is REFUTED by original-game tests (2026-08-07, `BL-302`).** In
> the original, every destructible takes severity-scaled collision damage — a rammed C1 hangar
> (plain `WeaponHit`) dies alongside the plane crash, and a survivable graze advances its damage
> stages. The `ACTIVATION` enum gates the *plane's* fate (solid vs fly-through), not the object's.
> Decision 6's "follow the data exactly" was an inference from the census, tested against the
> original only now. The 44-def fly-through set and this landing's mechanism stay correct.

**Verified.** `--damage-hd` gained a `collide[✓/✗, ACTIVATION]` probe: the facades, windows and
`agyrobus` `collide[✓ broke]`; the C2 signs and `kkgate` `collide[✗ ignored]`. 8-chapter freecam
regression byte-identical. **Owed playtest:** the in-flight feel — flying through a C2 facade panel
(it breaks, plane survives) vs. flying into a water tower (plane dies, tower stands).

**Goal.** The Hollywood facades and warehouse windows break on contact — and nothing else does.

**Evidence.** The 44-object table above. 43 at health 0.01 (break on any touch), `agyrobus` at
70.0.

**Approach.** `FlightController.SurviveHit` already computes impact point, normal and severity
`vn` — reuse it. On collision with a `WeaponOrCollideHit` instance, apply damage; on
`WeaponHit`, do nothing to the object (the plane still takes its normal collision damage).

⚠ **Do not extend collision damage to `WeaponHit` objects** (decision 6). The 0.01 health tells
you these were authored as fly-through set dressing.

**Verify.** Flying through a C2 facade panel destroys it and the plane survives; flying into a
C2 water tower kills the plane and leaves the tower intact.

### C28 ☑ Destructible reset/restore — **LANDED (2026-07-24)** — Wave C complete

**Landed.** `AnimRuntime.ResetDestructible(inst)` is the death's inverse (feeds the debug tools F40/F41
and respawn): `Stop` the def's live death (tearing down its motions/puffers/fires); `RestoreRestPoses`
— put any node the death physically MOVED back to its authored pose (the ballistic debris pieces:
`Stop` removes the motion but leaves the piece wherever it flew, so a re-destroy would launch from the
wrong place; `_rest` holds each moved node's rest transform); re-apply `RESET_STATE` (its
`OBJECT_ACTIVE_STATE` base states restore the healthy subtree visible+collidable and hide the destroyed
one — `SetSubtreeActive` restores colliders with visibility, undoing both the swap and the
`ApplyDeathSwap` fallback); and restore the instance's HP/Status/DamageStage.

**Verified.** `--damage-hd` gained a `reset[…]` check — after the kill, reset then re-kill and compare.
Idempotent across **C1/C2/C5**: every type returns `healthy=✓` (healthy shown, destroyed hidden) and
re-kills in the same hit count — buildings/towers (with debris), passenger planes, `air_gen`, the AA
gun `aagun32` (RESET-derived swap), the doors `gate1`/`gate2` (rotated leaves restored), the propane
`kkgate`, the C2 facades, and C5's `agyrobus`. 8-chapter freecam regression byte-identical.

**Goal.** Return an instance to healthy, for the debug tools and for respawn.

**Approach.** Re-apply `RESET_STATE`, restore HP, stop running damage effects, restore
colliders. Feeds F40 and F41.

**Verify.** Destroy → reset → destroy again produces identical results both times.

---

### D29 ☑ Muzzle flash — **LANDED (2026-07-24)**

**Approach.** `FIRE` → `ANIMATION` (`muzzle_burst_slug`/`_ap`/`_dum`/`_mag`) at the firing
group's markers. The reader is `ON_CALL`, `EXECUTION_PRIORITY 6`, LOD-gated via
`CALL_ANIMATION muzzleburst_effects AT_NODE`. Textures `{slug,dum,ap,mag}_muzzle1/2`.
Reference captures: `OriginalScreenshots/MuzzleFlash1-3.png`.

**Verify.** Flash appears at every marker of the firing group and nowhere else; A10's
comparison passes.

### D30 ☑ Impact effects — **LANDED (2026-07-24)**

**Landed.** `ProjectilePool.Impact` plays the struck surface's `IMPACT` `SOUND` (already landed)
and, for the effect **animation**, splits by what the bound name resolves to: a **gamez model root**
(the name IS a `nodes.json` root) is instanced at the hit point via `SpawnImpactModel` (reusing the
flyout `GameZ`/`SceneBuilder`, collision-exempt, freed after 0.4 s), suppressing the spark; a
reader/control def or undefined name instances nothing and the stand-in spark shows. In practice the
model path is the **water splash** — gun `splash1.flt` + HE `bsplsh.flt`, 2 meshes each, verified
reproducibly on C1B/C2B. The 5 undefined names (`bld_damage.flt`, `rcochet1`, `call_small_flash`,
`f18sparks2`, `flak_effectplayer`) confirmed **inert** on C4/C5 building hits.

**⚠ Premise corrected.** This item assumed "`Puffer.cs` already implements puffer emission" ⇒ just
call the effect. It doesn't hold at runtime in flight: the puffer factory + `TextureArchive` are torn
down after the world build (`KeepArchivesOpen` is `--anim-lab`-only), so a runtime `PUFFER_STATE`
builds nothing (`verification.md` rule 76). So the **puffer/particle** half of the named effects (the
`gunhit` `blacksmokepuffer` smoke, the fireball puffs) is **not** rendered here — it needs the
world-effects runtime that keeps textures open and relocates templates onto the hit point (the
`BuildFlightCrashRuntime` pattern), which is **D32's** shared machinery. D30 delivers the model-based
effects + sound + spark; the impact puffers fold into D32.

**Verify (met, as reconciled).** Water surfaces instance their authored splash model + play the
sound; the reader/undefined-name surfaces play the sound + spark with no crash; on-screen splash look
is an **owed playtest** (`playtest.md`). The five undefined names render nothing (inert).

### D31 ☑ The `Sound` anim-event family — **LANDED (2026-07-24)**

**Goal.** Unblock the ~6,000 one-shot `SOUND` events deferred for want of weapons/deaths.

**Landed.** `AnimRuntime.HandleSound` dispatches the one-shot `SOUND` (previously it fell through
the `default` case and was only counted) as a fire-and-forget `WorldSounds.PlayOneShot` at the
event's AT_NODE — `{name,pos}` compiled / flat `at_node`+`translate` reader (normalized in
`AnimDefs`), or the anchor. The NAME is a sound *definition* or a `SOUND_GROUPS` name, never a gamez
node (the recorded C3 gotcha confirmed: the lone reader-scope one-shot names `snd_waterfall`,
`targets=0`). **`DYNAMIC_WEIGHTS` decoded** — `SoundDefs.LoadGroups` parses `SOUND_GROUPS` into
`SoundGroup`s; `Pick(rng)` is weighted-random with a recency scalar (the bare `0.5` after the token
halves the last pick's weight), through the runtime's seedable `_rng`. Prewarm now covers one-shot
names (`AnimProgram.OneShotSoundNames`, groups expanded to members) and decodes quietly
(`SoundArchive.Find(…, warn:false)`) so a chapter archive lacking a WAV (`hanger_door.wav`) is silent
until the point of use. One-shot players self-sweep in `WorldSounds.Tick`; `FlushOneShots` covers the
frameless damage-test harness. Ownership: C24 owns the death *sequence*, D31 owns the `Sound` *event*
inside it — no dispatch-table collision (separate `case`).

**Verified.** Build clean. 8-chapter `--damage-test`: `Sound` off every unhandled list; death sounds
play (switchhouse `air_mixed_exp_sg` → `snd[2]`, C1 52 / C2B 73 / C3 4 / C5 136); `--debug-anim` shows
`air_mixed_exp_sg → snd_exp_hit1/2/3` (recency-diversified) at `dbase`; leak-free (C4 26+ plays → 0
ObjectDB leak). Docs: `sounds.md` (`SOUND_GROUPS`), `anim-definitions.md`, `architecture.md`.

### D32 ☑ Destruction + impact effects wiring (the world-effects runtime) — **LANDED (2026-07-24)**

**Landed.** `PlaneViewer.BuildWorldEffectsRuntime` builds one world-scoped `AnimRuntime` (the
generalization of `BuildFlightCrashRuntime`): a **hidden** `world_effects` stage of the effect
template roots (`EffectStageRoots` — `gunhit`/`flame_ball_01`/`he_ring`/… , all present in every
chapter's gamez), a live `PufferFactory` over the session textures (kept open for the crash runtime
already), `PlaceCalledTemplates`/`NameResolveFallback` on, bound to the closure of the 28
`EffectAnimNames`. `AnimRuntime.PlayEffectAt(name, worldPoint)` relocates the effect's template root
onto the point and `Start`s the def — the puffers ride the relocated root and parent at world level,
so they render even though the stage is hidden (the template **meshes** — the `gunhit` debris bits,
the `he_ring`/splash models — stay hidden: a documented mesh follow-up). Two callers:
`ProjectilePool.EffectSink` on a **rocket/ordnance** impact, and the world runtime's `ExternalEffect`
routing a **death** sequence's `CALL_ANIMATION` of a curated effect here. `EffectTtl` (32 s) bounds a
stop-less sustained emitter (`large_30sec_fire`); `SoundHandledElsewhere` no-ops its SOUND events
(D30/D31 own that audio).

**Two decodes fixed on the way.** (1) A `PUFFER_STATE` whose `AT_NODE` is `INPUT_NODE`/
`MAIN_ROOT_NODE` now resolves to the anchor (`IsSelfNodeRef`, the same sentinel rule `ConditionNode`
already applied) — before, `ResolveOne`→null→no host, so `large_30sec_fire`'s `fire_n_smoke` emitted
nowhere. (2) `AnimProgram.Subset` gained a multi-root overload for the effect closure.

**⚠ Guns deferred (stronger than the plan's singleton note).** The plan expected the `gunhit` smoke
to *collapse onto one puff*; in fact `gunhit`'s `blacksmokepuffer` has **no `ACTIVE_STATE 0` stop**,
so a per-round shared emitter would emit **forever** at the last hit. So gun impacts are **not**
routed (`ProjectilePool.EffectSink` is gated `!weapon.IsGun`); the gun `*_gunhit` names are still
bound + testable. A guns pass needs per-hit copied/expiring emitters. Rockets/ordnance (≤1/s) route.

**Verified.** `--effects-test` (new; seeded + `StopAll` between names → reproducible across chapters
C1/C2/C4/C5): 28/28 resolve, **16 build a puffer** — `large_fireball`/`small_fireball`/
`large_30sec_fire`/`great_balls_of_fire`/`large_black_smokeball`/`big_splash` + the gun `*_gunhit`
smoke + the `ap`/`sonic`/`flak`/`scatter`/`torpedo` ground bursts; the 12 that don't are point-light/
model effects (`he_ground_effect`/`flash_effect`), the `RANDOM_WEIGHT`-gated gun variants, and
`biggun_flying_parts` (a zeppelin container whose puffers ride unstaged `fly_trail*` sub-trails).
Flight: a C1 rocket run routes impacts through `PlayEffectAt` with no crash/noise. Regression: 8-chapter
`--damage-test` 0 errors (16 defs each, unchanged); `--freecam` ambient puffer census unchanged. The
**on-screen** fireball look is an owed playtest (`playtest.md`), like D30's splash.

### D33 ☑ Tracers — **LANDED (2026-07-24)**

**Approach.** `tracer_slug` / `tracer_dumdum` / `tracer_armorpierce` / `tracer_magnesium` /
`tracer1`, matched to the ammo type. Cheap billboard/quad per round rather than a mesh.
Establish the visual-length and frequency rule against reference footage in
`OriginalScreenshots/Videos/`.

**Verify.** `--perf` shows no meaningful GPU cost at sustained fire from all groups.

### D44 ☑ Pylon ordnance visuals — **LANDED (2026-07-24)**

**Landed.** `src/Flight/PylonOrdnance.cs`: `Build(loadout, pool)` instances ONE FLYOUT `MODEL` body
per loaded pylon via the new public `ProjectilePool.BuildFlyoutBody` — the SAME gamez prototype the
round flies (`he_rocket`, `sonic`, …) — and parents it to that pylon marker at identity local
transform, so the mounted body sits nose-forward at the exact pose the round launches in. `Update`
(driven by `FlightController` after `UpdateRockets`) shows/hides each body per its live
`Hardpoint.Ammo`; a respawn refill re-shows it. `ProjectilePool.BuildFlyoutModel` was refactored to
call the shared `BuildFlyoutBody` (the in-flight round parents it under the pool; the wing keeps its
own copy). `--rocket=<wep_id>` swaps every hardpoint's ordnance for testing (`docs/cli.md`).

**Both traps handled.** *One model per pylon, not per round:* the body shows while `Ammo > 0`, so a
3-round HE pylon still shows a single rocket. *No double-up with airframe geometry:* a name search of
the plane `nodes.json` for rocket/missile/bomb/torpedo/ordnance/munition geometry is **empty** — the
only pylon-named nodes (`pylon1..8`, `lpylon*`/`rpylon*`) are all `model_index -1` mesh-less markers,
so instancing the FLYOUT body adds ordnance where there was none rather than duplicating it.

**Verified** (headless, `--quit-after`, no `--screenshot` — the framebuffer-capture path floods a
headless build with `Parameter "t"` errors, `verification.md`; the clean runs report **zero** errors
and are the authoritative check): a stock Bloodhawk over C1 logs `pylon ordnance: 3 mounted rocket
model(s)` and `flyout model 'he_rocket' (wep_06) instanced: 1 mesh(es)`; a `--fire-rockets` soak
fires 9 HE round-robin (pylon1/2/3, 2→1→0 each) and hides `pylon1` on the 7th pull, `pylon2` on the
8th, **`pylon3` on the 9th**. (That round-robin *order* is B17's, and the user has since flagged it as
wrong — the original drains the selected hardpoint first, pylon1→3rd/pylon2→6th/pylon3→9th; backlogged
as an M3-polish `NextArmedHardpoint` fix. D44's visual needs no rework — it hides each pylon the instant
*that* pylon empties, so the wing will simply empty in whatever order B17 fires.) `--rocket=wep_08`
builds `sonic` bodies instead (`flyout model 'sonic' (wep_08) instanced`). The 8-pylon Warhawk mounts 8; C5 resolves the prototype too. The pixel-level
z-fighting check is the owed at-the-controls playtest (shared with B14/B17 — a mounted rocket is not
headless-screenshottable in this build). **Docs:** `architecture.md` (`PylonOrdnance`, `Projectile`,
`FlightController`), `CLAUDE.md` index, `cli.md` (`--rocket=`).

**Goal.** Mounted ordnance is visible under the wings, and disappears as it is used.

**Evidence (user, 2026-07-22).** *"Visible rocket at pylon below as long as there is ammo left
on the hardpoint. Different models for different rocket types."* So each loaded pylon renders
its ordnance, the model varies by rocket type, and it is hidden once that hardpoint is dry.

**Approach.** Instance the weapon's `FLYOUT` `MODEL` at the pylon marker — the same gamez
prototype roots the projectile uses (`ap_rocket`, `he_rocket`, `flak_rocket`, `flash_rocket`,
`beeper_rocket`, `scatter_rocket`, `a_torpedo`), so the thing hanging on the wing and the thing
that flies off it are the same asset. Bind visibility to the hardpoint's remaining count from
B17.

⚠ **A pylon holds `CLUSTER_SIZE` rounds but shows one model** — the user reports a single
visible rocket per pylon, not a stack of three. Do **not** render one model per remaining
round; render one while `count > 0`. This asymmetry is the whole reason the item needs stating.

⚠ Check whether the plane models already carry static ordnance geometry under the pylons that
would double up with an instanced model — several airframes have detailed underwing nodes.
`--dump-markers` (A3) and the `--viewer` overlay make this visible.

**Verify.** A stock Bloodhawk shows 3 HE rockets before firing and none after 9 pulls, with
the third pylon's model vanishing on the 9th; swapping rocket type via `--loadout=` changes
the model; no z-fighting or duplication against existing airframe geometry.

---

### E34 ☑ Bitmap-font HUD text renderer — **LANDED (2026-07-24)**

**Goal.** HUD text in the game's own font. Nothing in the codebase used it yet.

**Landed.** `src/Flight/HudFont.cs` — a reusable renderer over the two `extracted/rimage/` atlases
(`5pointhud.png` normal + `5pointhudbrite.png` highlight). Pixel-probing corrected the atlas
description: it is **463×6, a proportional 1-bit font, five px tall (rows 0–4), covering printable
ASCII `0x20`–`0x7e`** — not just `0123456789:;<=>?@A…z`; space is a blank leading cell so the 94 ink
glyphs map one-per-code `0x21`–`0x7e` in code order (`glyph(code) = run[code−0x21]`), letters
uppercase-only. Two green levels on black (normal core (0,150,0), highlight (0,255,0), (0,32,0) edge).
The reader auto-segments source rects at load (maximal inked-column runs — exact because no glyph has a
blank interior column, 94 runs = 94 codes; warns if the count drifts), keys black transparent (green
kept), draws with `DrawTextureRectRegion` under a Nearest filter (1 px tracking, 3 px space), and sizes
through `HudMetrics`. Full decode in `docs/formats/hud.md`; the E35/E36 items draw with this.

**Verified.** `--hud-font-test` (a flag-gated per-pane proof overlay, `src/Flight/HudFontTest.cs`):
`GUNS 30: 2000  ROCKETS 06: 9` renders in both variants at 1P and in a 4-way splitscreen pane, glyphs
identical, size differing only by the HudMetrics factor (1P Scale 0.50 vs 4P pane 0.354 — the
sqrt-damped 0.707 ratio, not a naive 0.50), `Measure()`'s underline ending exactly at the last glyph.
Screenshots run **windowed** (verification.md rule 71). Additive + gated: flag off ⇒ font not loaded,
flight HUD unchanged.

### E35 ☑ `gungauge` + `missilegauge` — **LANDED (2026-07-24)**

**Approach.** Extend `GaugeCluster`, which already extracts `altimeter`/`speedometer`/
`damageindicator` from the same `gauges` subtree. Both gauges exist on all 11 planes with
letter/digit texture cycles and `ggindicatorN`/`mgindicatorN` belt lights.

**Landed.** `GaugeCluster.ExtractWeaponGauge` reads both dials; `FlightController.UpdateWeaponGauges`
feeds a `WeaponGauge` (count / type / selected slot / per-slot fractions) each frame from the live
loadout. The `4char_ammo` digit cycle (`zero.tif`…`SPACE.tif`) is drawn right-aligned; the
`6char_type` cycle (`A`…`Z`,`zero`…`nine`,`SPACE`) shows the weapon `NAME` upper-cased; the
`ggindicator`/`mgindicator` belt lights step green/yellow/red by that slot's fraction; the
`gg`/`mgarrow` pointer rotates to the selected gun group / next-armed pylon. Full decode +
`cockpit.gw` drive in `docs/formats/hud.md`. **Guns read per-group, rockets per-pylon** (the arrow's
pylon), not a total — the user's correction, matching the original's `BOOM 3` Warhawk readout.

⚠ **The per-plane parenting warning turned out NOT to apply to these two gauges.** Verified across
the whole roster: the `gungauge`/`missilegauge` node is mesh-less on every plane and the face hangs
off the generic child (`g815`/`g819`) uniformly — there is **no Bloodhawk special case** here (that
was the `damageindicator`). The extraction still uses the safe "any unrecognised child = face" rule,
so it is robust either way.

**Verified.** All 11 planes render both gauges (including the Devastator's inherited
`player_pfighter`); firable-group counts exclude turrets (Balmoral/Kestrel/Firebrand/Brigand/Hellhound
show only their non-turret groups). Counters track B16/B17 ammo: each plane's W1 caliber → capacity
(70→1200 … 30→2800) on the gun gauge, `BOOM 3` per full HE pylon on the missile gauge. Belt stepping
proven by Bloodhawk rocket depletion — full = green, `1` remaining = **yellow**, `0` = **red**, the
arrow tracking the yellow (next-to-fire) pylon. Captures windowed (verification.md rule 71).

**TUNE (→ `playtest.md`).** The green/yellow/red thresholds are inferred (yellow ≤ 0.34); the user
will confirm against the original that ammo gauges show a yellow state at all, and at what fraction.

**Verify.** All 11 planes render both gauges; the counters track B16/B17's ammo; belt lights
step correctly.

### E36 ☑ Selected-weapon readout — **LANDED (2026-07-24)**

**Approach.** `MSG_HUD_GUNGAUGE` = `"GUNS: %1: %2!d!"` — `%1` names the gun group (which is why
it exists: per-group counters), `%2` the count. Resolve through `Messages`, render with E34.

**Landed.** `src/Flight/WeaponReadout.cs` — a bottom-centre two-line `Control` in the `5pointhud`
font. Also renders the parallel `MSG_HUD_MISSLES` (id 189, `"MISSILES: %1: %2!d!"` — the table's
misspelling) since E35 added both gauges. Added `Messages.Fill` for the `%N` / `!d!` / `%%`
placeholder grammar. `%1` = the gun group's mount name (`Inner Wing Guns`) or the rocket's display
name (`High-explosive rocket`); `%2` = the selected group's per-group rounds / the next-to-fire
pylon's per-pylon rounds (matching the E35 gauge). Replaced the interim `AmmoLine`. Decode in
`docs/formats/hud.md`.

**Verified.** Cycling gun groups updates both name and count — Bloodhawk `INNER WING GUNS: 2400` →
`OUTER WING GUNS: 2800` (distinct 40-/30-cal capacities) and the Balmoral twin-.50 name swap
(`INNER`→`OUTER`, both 2000). Missile line `HIGH-EXPLOSIVE ROCKET: 3` (per pylon). The text resolves
from `messages.json` (a missing table renders the raw `MSG_HUD_GUNGAUGE` key). Captures windowed
(verification.md rule 71).

**Verify.** Cycling groups on the Balmoral updates both name and count; the string comes from
`messages.json`, not a hardcoded literal.

### E37 ☑ Impact-point reticle — **LANDED (2026-07-24)**

**Landed.** `src/Flight/ImpactReticle.cs` — a per-pane `Control` drawing the game's own pipper
`extracted/rimage/impact_point.png` (a 32×32 RGBA warm-white disc with a cross-notch centre;
authored alpha, no colour-keying) at the SELECTED gun group's ballistic impact point.
`FlightController.UpdateReticle` averages the group's muzzle poses and marches a round through
`BallisticImpactPoint` — the same `VELOCITY`/`ACCELERATION`/`GRAVITY` integration `ProjectilePool`
fires with, plus the plane's inherited velocity, dropping only the random `CANNON_SPREAD` — to
`GunConvergenceDist`; the world point projects through the live camera at `_Draw` time (as
`MarkerHud`), fixed screen size scaled by `HudMetrics`. `ProjectilePool.WorldGravity` is now
`internal` so reticle and rounds share one constant. The pipper is NOT pinned to screen centre; it
is hidden while crashed or with no firable gun.

**Convergence distance = 250 m, a TUNE** (`GunConvergenceDist` in `FlightController`) — `weapons.json`
carries no convergence field. Open question 4 is now chosen, pending an original-game A/B (playtest.md).

**Verified** (windowed, verification.md rule 71): steady level flight — tracers stream through the
reticle, `nose→reticle = 0.00°` (rounds land on it); hard pull — as AoA grew (`nose→vel` 7.8°→15.0°)
the reticle deflected `0.46°→0.77°` **below** the nose toward the velocity vector (trails the nose).
The trailing angle is set by the velocity/bullet-speed ratio, so it is small (fast bullets) and
essentially independent of the convergence distance. Clean cross-chapter (C4) + 2-player splitscreen.

**Goal.** The aiming reticle, with the original's behaviour.

**Evidence (user, 2026-07-22).** The reticle is **not fixed to the plane's flight direction**.
It is a computed impact point at *x* metres, so it **lags behind the plane's rotation** during a
roll or pull rather than sitting locked at screen centre. Texture: `extracted/rimage/impact_point.png`
— four tick marks around an open centre.

**Approach.** Project the selected gun group's ballistics forward to a convergence distance and
draw the reticle at that world point's screen projection, using the same integration as B13 so
the reticle and the rounds agree. Choose and document the convergence distance — **not stated in
the data**; a TUNE constant pending playtest.

**Verify.** In a hard turn the reticle visibly trails the nose; rounds land where the reticle
sits in steady flight.

### E38 ⊘ Lock-on indicator — deferred to M4

**Deferred with B19/B20 (2026-07-24).** The lock-on it indicates is the enemy-plane target-cycle,
which is M4 scope (M3 has no enemy planes and no guided flight). When M4 builds it: target box plus
acquisition progress reusing `MarkerHud`'s existing reticle and screen-edge arrow.

**Verify (M4).** Acquisition takes `LOCK_ON` seconds; the indicator clears when lock breaks.

---

### F39 ☑ Weapon lab in `--viewer` — **LANDED (2026-07-24)**

**Landed.** `src/UI/WeaponLab.cs` — the fourth lab beside `DamageLab` (H), `LiveryLab` (L),
`MeshLab` (M), toggled with **W** (`--weapon-lab[=wep_id]` opens it at launch). It mounts a weapon
and fires it, driving its OWN `ProjectilePool` so a round runs the identical ballistics flight
fires. **Refined 2026-07-24 (user feedback):** weapons split into two banks matching the game —
GUNS fire from the plane's named **gun groups** (bound from the stock `Loadout` — "Inner Wing
Guns" …), HARDPOINTS fire from its **pylons**; the bank filters both the weapon list and the mount
list, so a gun can only fire from a gun group and a rocket only from a pylon. The panel
(bottom-right — the one free corner) steppers pick bank / weapon (with live ballistics) / mount /
target surface; a slider parks the stand-in target wall **15–1100 m** ahead; auto-fire +
fire-once + **Space**; and a copy-CLI-args button emits
`--weapon-lab=<id> [--weapon-mount=g<slot>|pylon<n>] [--weapon-fire]`.

**The viewer has no world**, so the pool is built scene-less: rockets fly streak-only (no `FLYOUT`
prototype), gun impacts show the stand-in spark and **hardpoint impacts show a stand-in explosion
burst** (`ProjectilePool.SpawnExplosion`, added when `EffectSink` is null — no real puffer runtime),
and there is no `DamageSink`. To give a round something to hit (the pool's hits come from a per-step
world raycast), the lab parks a `StaticBody3D` target wall ahead of the nose, tagged
(`SceneBuilder.SurfaceMeta`) so the pool's classifier picks the matching `IMPACT` variant; shown
only while the lab is engaged, so an unadorned `--viewer` screenshot is byte-identical. Built in
every parked `--viewer` session so W always toggles it.

**Verify — done.** `--weapon-test` fires every one of the 48 entries once, each from a mount of its
class, and reports **48/48 fired OK, 0 errors, 0 skipped** (verified on the Bloodhawk, the Kestrel's
7-firepoint centreline rig + turret group, Warhawk and Peacemaker; `./.scratch/weapon_test.txt`).
Windowed captures show a gun fired from a named gun group (`wep_40` from "Inner Wing Guns") with
tracers + impacts, and the `wep_06` HE-rocket **explosion** on the target; the impact log confirms
the raycast hits the target and classifies the surface. A plain `--viewer` screenshot renders clean
(no target/tracers/panel).

### F40 ⊘ Freecam raycast pick + HP control — superseded by PLAN-testing

**Superseded (2026-07-24).** `docs/PLAN-testing.md` D31 + D34 deliver a strictly richer shape:
click-selection with an ancestor-ladder breadcrumb (D31) plus an HP slider with kill/reset on
the selected destructible (D34), integrated with the node/mesh labs, instead of this item's
crosshair raycast + chip/kill/reset keys. `--damage-test`/`--damage-hd` remain the headless
verification tools in the meantime (cli.md points at F40 as their sunset — that pointer now
means D31/D34).

**Verify (moved).** D34 carries the reset-idempotency and all-chapters checks.

### F41 ⊘ Destructible list overlay + camera jump — superseded by PLAN-testing

**Superseded (2026-07-24).** `docs/PLAN-testing.md` D32 (the Node Lab) absorbs this whole item:
its destructibles-filtered view lists every destructible with camera-jump, **including this
item's coverage instrument** (per entry: does `ANIMATION_ROOT_NAME` resolve to real nodes, do
its sequences reference only implemented event kinds — unresolved entries shown loudly), and
the census-totals check also stands as PLAN-testing B12's `destructible-census` suite.

**Verify (moved).** D32 asserts the per-chapter totals against A4's census.

### F42 ☑ `--destroy=` CLI trigger — **LANDED (2026-07-24)**

**Landed.** `--destroy=<name>` kills a named destructible at session build so a `--screenshot`
captures its death with nobody at the controls. `<name>` matches (case-insensitive substring) any
live destructible's def / animation / anchor-`cs_name`; every distinct match is killed, resolved to
its authoritative `DestructibleRegistry` instance and de-duped by anchor (capped 64). It **reuses the
weapon-hit path** — `AnimRuntime.DamageAt(anchor, MaxHealth+1)`, so the healthy→destroyed swap fires
synchronously and the debris/effects/audio are identical to a rocket kill — and self-ticks the death
out during the `--screenshot` warm-up. A pure modifier (forces no mode); in `--freecam` it auto-frames
the killed object and builds the world-effects runtime so the fire renders, both gated on `--destroy`
so a plain `--freecam` build stays byte-identical. `PlaneViewer.cs` only (+135). Full flag doc:
`docs/cli.md`; module notes: `docs/architecture.md` PlaneViewer.

**Verified.** A destruction screenshot in each of the 8 chapters, names from `--damage-test` + the
compiled-anim data (`m_build01` C1, `bhf_heliumtank1` C4, `g_tower1` C3, `agyrobus` C5, `s_build` C2,
`--mission=MP1 --destroy=patrolboat` C1B, `--mission=M01 --destroy=leng11` C1C). **C1B/C1C/C2B's IA1
worlds carry no placed ground destructible — the only destructibles are the mission airship (parked at
origin, hidden in a controller-less freecam)**; C1B/C1C recover a placed target via an MP/story mission,
while C2B's every destructible (47/47) is airship-mounted — a map-content fact. Plain 8-chapter
`--freecam` regression clean (node counts unchanged; one pre-existing C1 late-sound PushWarning).

**Original approach (kept for reference).** `--destroy=<def-or-node>` triggers a named destructible at
startup so a `--screenshot` run captures its death with nobody at the controls.

**Verify.** A scripted run in each of the 8 chapters produces a destruction screenshot.

### F43 ☑ `--infinite-ammo` / `--loadout=` overrides — **LANDED (found delivered 2026-07-24)**

**Found already delivered on the Wave-F review:** both switches shipped alongside B12/B16 and
are documented in `docs/cli.md` (`--infinite-ammo`: guns/hardpoints never deplete, HUD shows
`∞`, `PlaneViewer.cs:467`/`FlightController.InfiniteAmmo`; `--loadout=`: binds a named def
instead of the plane's own, in flight and under `--dump-loadout`). Nothing remained to build —
the item predates their landing and was never ticked.

**Original approach (kept for reference).** Two testing switches. `--infinite-ammo` suppresses
depletion; `--loadout=` overrides a plane's stock entry. Document both in `docs/cli.md` —
CLAUDE.md keeps only the day-to-day table.

---

## Exit criteria

Milestone 3 is done when:

1. Every weapon in the stock table fires with correct ballistics, effects and audio, on all 11
   aircraft (turret slots excepted — M4).
2. A scripted `--destroy=` run kills a representative destructible in **each of the 8 chapters**
   with correct animation, audio and collider removal.
3. The 44 collide-destructibles break on contact, and no `WeaponHit` object does.
4. The 8-chapter `--freecam` regression is clean — zero errors, node/mesh counts unchanged
   except where coverage was deliberately added.
5. The HUD shows per-group ammo, selected weapon, impact-point reticle and lock state.
6. `docs/formats/` covers `weapons.json`, the destructible model, the effect readers and the
   marker rig; `vehicle.md`'s deferral is gone; `zrdr.md`'s index is complete.

**Not exit criteria** (deliberately): the configurator UI, turrets, air-to-air anything,
firing heat / cannon jam, ammo pickups.

---

## Added to `backlog.md` (done 2026-07-22, when this plan was committed)

- **Firing heat + cannon jam** — data ships exact constants: `FIRING_HEAT` on 4 weapons,
  `cannon_jam` on `player_airplane` (`heat_safe_limit 1000`, `heat_dissipation_rate 50`,
  `jam_chance 0.1`). Deliberately excluded from M3 (decision 4) as friction with no combat
  pressure to justify it. Record the constants so nobody re-derives them.
- **Ammo pickups** — `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist, implying world
  pickups. Needs the pickup entities located first; they may be mission-scripted rather than
  placed, so this carries research risk.
- **The gun/hardpoint configurator UI** — `GUNS.SCRIPT` (4 slots, `gn_d_gun0..3`) and
  `HARDPOINTS.SCRIPT` (2 slots, `hp_d_point0..1`) are pure UI layout reading engine callbacks
  2249/2250 and 2245. Per-plane slot counts, costs and the economy are executable-resident.
- **M4 dependencies discovered here** — turrets as AI gunners (`ai.zrd.json` is TURRET-only;
  `turrets` in `vehicle.json`; `gun_pitch`/`gun_yaw` cones); the `target` aim-point marker;
  air-to-air lock-on; `FLYOUT_HEALTH`/`TARGETABLE` shootable torpedoes; AI-only `armor`/`health`
  pairs that `PlaneStats` does not read.

## Open questions the data cannot settle

Carried here rather than guessed at:

1. ~~**`CLUSTER_SIZE` semantics**~~ — **resolved 2026-07-22**: rounds per slot, measured (A9).
2. ~~**Gun slot → mount name per plane**~~ — **resolved 2026-07-22**, user-supplied (A8), and
   the slot→firepoint binding rule fell out of it. A10 remains as verification.
3. ~~**`ARMOR_DAMAGE` vs `HEALTH_DAMAGE` against world objects**~~ — **resolved 2026-07-22**:
   destructibles carry `health` only (zero armour keys in 16,114 defs), so only `HEALTH_DAMAGE`
   applies. The two-pool model, where it applies, is armour-then-health (see C23). **New open
   question in its place:** the patrol boat and trucks have both a vehicle armour/health def
   and anim destructible defs — which governs? (C23)
4. **Reticle convergence distance** — not in the data; **chosen 2026-07-24 as `GunConvergenceDist`
   = 250 m** (E37), a TUNE pending an original-game A/B (playtest.md). The on-screen trailing angle
   is set by the velocity/bullet-speed ratio, so it barely depends on this value.
5. ~~**One trigger pull = one rocket or the whole slot?**~~ — **resolved 2026-07-22**: one
   rocket from one hardpoint. Cooldown (`FIRE_RATE 1.0`) is data-derived, not observed.
6. **`vehicle.json` `weapons` tuple positions 3–5** — inferred, not confirmed (item A6).
