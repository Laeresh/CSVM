# weapons.json — the ballistics table

Part of the [format documentation](README.md). The shared `weapons.zrd.json` reader: the
whole install's projectile catalogue — guns, rockets, ordnance — under a single
`BALLISTICS` block. Validated against this install's zrdr extraction; the allotment and
damage semantics were cross-checked against the original game.

## Structure

The file's one root object is an alternating key/list dict (the [shared conventions](README.md#shared-conventions-zrdr-readers)
apply — every scalar arrives as a float):

| Root key | Value | Meaning |
|---|---|---|
| `VERSION` | `[2]` | reader schema version |
| `NO_AMMO_WARNING` | `["snd_emptyclip"]` | the empty-clip sound def, shared by every weapon |
| `BALLISTICS` | list | the weapon entries |

`BALLISTICS` is itself an alternating dict keyed `wep_00`, `wep_01`, … — **48 entries**. The
ids are **neither contiguous nor strictly ordered** (there is a gap after `wep_15`, `wep_25`
follows `wep_26` in file order, and the AI block jumps to `wep_130`), so **drive off the
keys, never a running index**. Each entry's value is again an alternating key/list dict; a
key paired with `null` is a bare flag (`CANNON`, `ROCKET`, `HIGH_EXPLOSIVE`).

## The entry tiers

| Ids | Count | What |
|---|---|---|
| `wep_00`–`03` | 4 | base 30 / 40 / 50 / 60-cal machine guns (a distinct, higher-rate/lower-damage tuning from the player matrix; the only entries carrying `FIRING_HEAT`) |
| `wep_04`–`15` | 12 | the ordnance block: incendiary (`9M`), armor-piercing (`ARMOR`), high-explosive (`BOOM`), `FLAK`, `SONIC`, `FLASH`, `BEEPER`, `SEEKER`, `CHOKER`, smokescreen (`SMOKER`), aerial torpedo (`TORPDO`), rear-arc flash (`FLARE`) |
| `wep_23`–`29` | 7 | emplacement / world weapons: `MPTUR` & `TURRET` turret guns, glidebomb (`gb`), a multiplayer HE rocket (`BOOM`), AA flak, zeppelin cannonball (`CB`), and the non-damaging `FW` "fake weapon" |
| `wep_30`–`73` | 20 | the **player matrix**: 5 calibers (30/40/50/60/70) × 4 ammo types (below) |
| `wep_130`–`170` | 5 | the same guns **detuned for AI**: one slug entry per caliber (below) |

## Fields

Every key that appears in the file is listed here; ranges are measured across all entries
that carry the key. A gun-only key is absent on rockets and vice-versa.

### Identity

| Key | n | Range / form | Meaning |
|---|---|---|---|
| `DESC` | 48 | `MSG_WEAP_*` | string-table key → the display name (see [Display names](#display-names)) |
| `NAME` | 48 | string | short internal handle (`30slug`, `BOOM`); not localized |
| `CALIBER` | 29 | 30–70 | gun bore; gun entries only |
| `PRIORITY` | 16 | 4–5 | ordnance targeting/selection priority; rocket entries only |

### Allotment

| Key | n | Range | Meaning |
|---|---|---|---|
| `CLUSTER_SIZE` | 46 | 1–2800 | rounds carried **per slot** — per gun group for guns, per pylon for ordnance |
| `AMMO_LIMIT` | 37 | 100–9999 | a **purchase cap**, not a carried count |

See [CLUSTER_SIZE vs AMMO_LIMIT](#cluster-size-and-ammo-limit) for which entries carry which.

### Ballistics

| Key | n | Range | Meaning |
|---|---|---|---|
| `FIRE_RATE` | 48 | 0.3–10.5 | shots per second (guns 6–10.5; rockets ≈1.0) |
| `VELOCITY` | 47 | 1.0–1200 | muzzle / flyout speed, m/s — with `ACCELERATION` it is the speed the motor climbs to **above the launcher's own**, not the launch speed |
| `ACCELERATION` | 16 | 0–150 | rocket-motor acceleration, m/s² (0 = constant velocity). A round carrying one leaves at its launcher's speed and climbs from there; the cap is `VELOCITY` plus that speed. Decoded in [`org/ordnanceTypes.md`](../org/ordnanceTypes.md) |
| `RANGE` | 46 | 900–10000 | path length a round may fly before it ends, m (Seeker 10000). **Defaults to 500** when unauthored, which is what the smoke screen and the rear-arc flare fly. Reaching it detonates a round only if the weapon carries `LOCK_ON`, so the choker, the cannonball and the fake weapon vanish instead ([`org/ordnanceTypes.md`](../org/ordnanceTypes.md)) |
| `RANGE_MINIMUM` | 1 | `[300, 0]` | **a hittability gate, neither an arming range nor a visibility one** (torpedo): the round's intersect bit (node flag `0x10`) stays clear until it has travelled element 0, so nothing can shoot it down over that leg; it is drawn, with its `MODEL_ANIMATION`, from the spawn frame. Element 1 is stored too and the gate demands it be zero, which the sole entry authors; what a non-zero one would mean is unknown. Decoded in [`org/ordnanceTypes.md`](../org/ordnanceTypes.md); nothing on that path gates arming or hides anything |
| `GRAVITY` | 5 | 0.0 | the round's own downward acceleration, m/s² — an absolute rate, not a scale on world gravity (0 throughout this install, so inert as shipped) |
| `CANNON_SPREAD` | 31 | 6.0 | **not a dispersion cone** — the gun aim assist's acceptance-cone half-angle, degrees (constant). See [`org/aim-assist.md`](../org/aim-assist.md) |
| `FIRING_HEAT` | 4 | 5.0 | nominally heat added per shot; only the base guns `wep_00`–`03`. **Parsed but never consumed by the original** : `FUN_004ba6f0` stores it at `+0x14` of the game-side weapon-extension struct (0x38 bytes, hung off the ZWEP record at `+0x210`), defaulting to 0 when the key is absent, and no consumer of that struct reads the field. Its partner `cannon_jam` is dead data too, see [vehicle.md](vehicle.md) |
| `TURN_RATE` | 14 | 0.001–1.25 | guidance turn rate; 0.001 is effectively straight-flying — only the Seeker's 1.25 actually homes |

Of the 16 `ACCELERATION` carriers, only `wep_04`/`25`/`26`/`27` are non-zero, and no gun group can
resolve any of the four (a gun is caliber + ammo → `wep_30`–`73`). With `GRAVITY` zero throughout,
every round the gun aim assist's reticle marches (`Ballistics.March`) travels a straight line, so
its fixed `1/120 s` step cannot move the endpoint — `BallisticsTests` guards the census.

### Damage

| Key | n | Range | Meaning |
|---|---|---|---|
| `ARMOR_DAMAGE` | 46 | 0–200 | damage to a part's armor pool |
| `HEALTH_DAMAGE` | 46 | 0–200 | damage to a part's health pool |
| `DAMAGE` | 2 | 0.0 | a single combined value used *instead of* the armor/health split on the two non-damaging specials (`FLASH` `wep_09`, `FLARE` `wep_15`) |

`ARMOR_DAMAGE`/`HEALTH_DAMAGE` feed the `destroyable_parts` armor+health model in
[vehicle.md](vehicle.md#armor-and-hit-points) — two sequential pools per damage zone,
armor spent first. **Both figures are absolute per-hit damage, not multipliers**: there is no
multiplier field anywhere in `BALLISTICS`, and the 0.5×/1.5× ammo pattern below is a derived ratio
against each caliber's slug, not something the engine computes.

### Guided flight & detonation (rocket block)

| Key | n | Range | Meaning |
|---|---|---|---|
| `ROCKET` | 15 | flag | marks a self-propelled projectile |
| `LOCK_ON` | 13 | 1.3–3.0 | inherit-launch-velocity flag and its decay window, s; also the guidance ramp's denominator (not a lock-acquisition time, see below) |
| `LOCK_ON_LEAD` | 3 | `[4,8]` / `[5,10]` | round age at which the intercept lead starts blending in, and the age it is full, s |
| `DETONATION_DISTANCE` | 13 | 1–50 | proximity-fuse trigger distance, m (**stored squared**, see below) |
| `IMPACT_PROXIMITY` | 14 | 15–500 | blast / effect radius, m (**stored raw and squared**, see below) |
| `DETONATION_DOT_PRODUCT` | 3 | 0.1 / 0.3 | cone-alignment threshold for a proximity detonation |
| `DETONATION_TIME` | 1 | 2.0 | timed fuse, s (rear-arc flare). Defaults to −1.0 and the fuse demands a positive value, so an unauthored one is off rather than instant |
| `CRATER` | 6 | 0 | ground-crater flag/scale; marks the ground-attack munitions |

**Whether a round is steered is a gate, and how hard it turns is `TURN_RATE`.** There is no `GUIDED`
boolean. The engine enters its steering step only for a weapon carrying `LOCK_ON` whose round holds
a target, and only then reads `TURN_RATE` for the turn authority
([`org/ordnanceTypes.md`](../org/ordnanceTypes.md#guidance)). 13 of the 14 `TURN_RATE` carriers
hold the sentinel **0.001** (the HE "BOOM" rocket `wep_06`/`wep_24`, AP, FLAK, incendiary, torpedo,
…), so with a target they are steered by a thousandth of a radian per second, which is dumbfire in
effect; **only the Seeker `wep_11` at 1.25 visibly homes.** `LOCK_ON` is present on the dumbfire
rockets too (the HE rocket carries `LOCK_ON 1.3`): it is the inherit-launch-velocity flag and that
decay's window, and the guidance ramp's denominator, not a lock-acquisition time. The Seeker is also
the sole `BEEPER_SEEKER`. Reader convenience: `WeaponDef.IsGuided` (`TURN_RATE > 0.01`), a label
for the lab and the probes rather than the flight gate. `LOCK_ON_LEAD`'s three carriers all pair it
with the sentinel and expire before its onset, so the shipped data never shows it.

**Three radii are stored squared.** `FUN_005ad630` keeps `IMPACT_PROXIMITY` twice, raw at weapon
`+0x3c` and squared at `+0x40`, and keeps `DETONATION_DISTANCE` squared at `+0x44` and `RANGE`
squared at `+0x20`; every distance the original compares them against comes from `FUN_00538880`,
which returns a squared distance with no square root. Read as plain radii they give the wrong
falloff curve and the wrong trigger range, so `WeaponDef` exposes both forms
(`ImpactProximitySqM`, `DetonationDistanceSqM`, `RangeSqM` beside the authored fields). The raw
`+0x3c` is the splash gather's sphere radius and the square at `+0x40` is the falloff's denominator,
`1 − d²/IMPACT_PROXIMITY²` over both damage figures, with `d` the distance from the burst to the
target's surface. The full decode, including which offsets stay raw, is in
[`org/ordnanceTypes.md`](../org/ordnanceTypes.md#the-engine-stores-radii-squared).

### Class flags & specials

Each selects a special behaviour; most are one bare flag or a tiny struct.

| Key | Entries | Form | Meaning |
|---|---|---|---|
| `HIGH_EXPLOSIVE` | `wep_06`, `wep_24` | flag | HE warhead |
| `SONIC` | `wep_08` | flag | sonic shockwave |
| `FLASH` | `wep_09`, `wep_15` | flag | blinding flash |
| `BEEPER` | `wep_10` | `TIME [20]` | tags the target for 20 s |
| `BEEPER_SEEKER` | `wep_11` | flag | homes on a beeper-tagged target |
| `TANGLER` | `wep_12` | `TIME [2]`, `RADIUS [35]`, `ENGINE_DEAD [5,13]` | choker: the burst leaves a cloud that lives `TIME` seconds and kills the engine of every aircraft whose origin sits inside `RADIUS` (the shooter's included) for `ENGINE_DEAD_max × (1 − d²/RADIUS)` floored at `ENGINE_DEAD_min`, the raw radius against a squared distance being the original's own mismatch; the pair is a global the last-parsed `TANGLER` sets ([`org/ordnanceTypes.md`](../org/ordnanceTypes.md) "The choker, settled"). Parsing it also installs the one impact hook in the binary, which silences the row's `SOUND` |
| `SMOKE_SCREEN` | `wep_13` | `TIME [8]` | lays an 8 s smoke screen |
| `REAR` | `wep_13` | flag | fires rearward |
| `TORPEDO` | `wep_14` | flag | aerial torpedo |
| `TARGETABLE` | `wep_14` | flag | admits the round in flight to the player's target list, on the Enemy/Ally cycle by the round's own team. It does NOT make the round shootable; that is `FLYOUT_HEALTH`. Implemented E19 |
| `FLYOUT_HEALTH` | `wep_14` | `[10]` | the round's own health pool, spent by a hit through the armour pool first (an armour pool the parser only ever writes as 0). Without the key both pools take the −1.0 not-shootable sentinel. Implemented E20 |
| `PROJECTILE_BBOX` | `wep_14` | `[0]` | ⚠ not a selector and not a size: bit 0 of def `+0x78`, which becomes node flag `0x20` on the pooled round node. **Absent sets the bit**, so the shipped default is ON and `wep_14`'s authored `0` is the one entry that turns it off. See [org/ordnanceTypes.md](../org/ordnanceTypes.md) |
| `DESTROY_ANIMATION` | `wep_14` | anim | effect played where the flyout is shot down. It replaces the detonation rather than accompanying it: a round dying on zero health with this authored never spends its warhead |
| `DAMAGES_ZEPPELIN` | `wep_14`, `wep_28` | flag | may damage a zeppelin hull. The remake consumes it as the gasbag routing gate (M4 F18): a weapon without it cannot damage a zeppelin's critical `healthy` zones, while engines/turrets/cannons stay ordinary destructibles any weapon hurts |
| `SHAKES_CAMERA` | `wep_26` | flag | the detonation shakes the camera. Sole carrier is the zero-damage scripted fake weapon, so it is NOT the player-gunfire shake mechanism — see [shakes.md](shakes.md) |

**`TANGLER` is decoded, and the recollection that it stalls a plane instantly is disproven.** The
hit branch zeroes the damage and sets the engine-dead bit with its timer, nothing else: no airspeed
clamp, no control authority touched ([`org/ordnanceTypes.md`](../org/ordnanceTypes.md) "The choker,
settled"). Whatever the felt instantaneity was, it is the thrust cutout plus the flight model. The
remake is `TanglerChoke` (the duration) and `ProjectilePool`'s cloud (the catch), the timer through
`FlightController.TryChokeEngine`; a human is choked exactly as an AI is, since the branch has no
player guard.

### Bindings

| Key | n | Meaning |
|---|---|---|
| `CANNON` | 31 | flag: hitscan-style gun (pairs with `LOOPED_SOUND_NAME`). It is bit `0x40` of the parsed flags dword (`FUN_004ba6f0` at `0x004ba9ba`), and it is also **the filter on both halves of the Instant Action wrap-up's Shot %**, which counts cannon hits over cannon rounds fired and ignores ordnance ([instant-action.md](instant-action.md)) |
| `LOOPED_SOUND_NAME` | 31 | looped firing sound def (`snd_30cal`, `snd_turretgun`) |
| `FIRE` | 47 | muzzle event (below) |
| `FLYOUT` | 48 | projectile model / anim / sound (below) |
| `IMPACT` | 47 | per-surface hit effect (below) |

## Cluster size and ammo limit

Two allotment numbers with different meaning, long indistinguishable because on guns they
are equal:

- **Guns** (`CANNON`) carry `CLUSTER_SIZE == AMMO_LIMIT` (e.g. 2800/2800, 1200/1200).
- **Air-to-air rockets/missiles** (HE, AP, FLAK, SONIC, FLASH, BEEPER, SEEKER, SMOKER,
  TORPDO, FLARE — `wep_05`–`11`, `13`–`15`, `24`) carry **`CLUSTER_SIZE` and no
  `AMMO_LIMIT`**: they are allotted per pylon only. `wep_06`, the HE rocket, is the model
  case — `CLUSTER_SIZE [3]`, no `AMMO_LIMIT`. Its anti-armour counterpart `wep_05` carries
  **`CLUSTER_SIZE [4]`** — you rack one *more* AP rocket than HE, on the same
  `IMPACT_PROXIMITY [15]`, with the damage pair mirrored (60/40 against HE's 40/60). The AP
  rocket's near-absence from community loadout advice is not a numbers problem.
- **Ground-attack / emplacement munitions** (the six `CRATER`-carrying entries: incendiary
  `wep_04`, choker `wep_12`, glidebomb `wep_25`, fake `wep_26`, AA-flak `wep_27`, cannonball
  `wep_28`) *do* carry `AMMO_LIMIT` (100, or 9999 for AA flak) — so "no `AMMO_LIMIT` on
  rockets" holds for the air-to-air set but **not** for these.
- **Turret guns** (`wep_23`, `wep_29`) carry `AMMO_LIMIT [9999]` and **no** `CLUSTER_SIZE`.

## The player damage matrix (`wep_30`–`73`)

Twenty entries: five calibers × four ammo types. The last digit of the id is the ammo
type — `X0` slug, `X1` dum-dum, `X2` armor-piercing, `X3` magnesium — and the caliber
blocks are 30 → `wep_30`–`33`, 40 → `wep_40`–`43`, 50 → `wep_50`–`53`, 60 → `wep_60`–`63`,
70 → `wep_70`–`73`. Within a caliber the four rounds share fire rate, velocity, and ammo;
only the damage split differs, on an exact rule (verified on all five calibers):

| Round | Armor | Health |
|---|---|---|
| slug (`X0`) | 1.0× (balanced, armor = health) | 1.0× |
| dum-dum (`X1`) | **0.5×** | **1.5×** |
| armor-piercing (`X2`) | **1.5×** | **0.5×** |
| magnesium (`X3`) | **slug + 0.5** | **slug − 0.5** |

Magnesium is an **additive** offset, not a ratio: exactly +0.5 armor and −0.5 health against that
caliber's slug, on all five (30: 3.5/2.5 vs 3.0/3.0 … 70: 12.25/11.25 vs 11.75/11.75). It is
therefore a mild armor-leaning round whose *relative* bias shrinks as caliber climbs, and it is
**not** an all-round upgrade over slug — a common secondary-source claim that the data refutes,
since it trades health damage away one-for-one.

Example (50-cal, `wep_50`–`53`): slug 6.25/6.25, dum-dum 3.125/9.375, AP 9.375/3.125,
magnesium 6.75/5.75. Base slug damage climbs with caliber (30→3.0, 70→11.75) while fire
rate falls (30-cal 8.0/s → 70-cal 6.0/s) and velocity drops (1000 → 750 m/s).

**The ammo type costs nothing but damage split.** Within a caliber all four rounds share
`FIRE_RATE`, `VELOCITY`, `RANGE`, `CANNON_SPREAD` and `CLUSTER_SIZE`/`AMMO_LIMIT` (30-cal: 8.0/s,
2800 rounds, for every one of the four). There is no rate-of-fire or magazine penalty on magnesium
or any other type — another secondary-source claim the data refutes. The in-game descriptions are
`ui_strings.json` ids 3370 (slug), 3371 (dum-dum), 3372 (AP), 3373 (magnesium/"EX"); 3372's
"AP rounds tend to punch clean through unarmored surfaces, inflicting very little damage" is retail
confirmation that armor is a **gate**, not a damage reducer — see
[vehicle.md](vehicle.md#armor-and-hit-points).

## The AI detune (`wep_130`–`170`)

One slug entry per caliber (no dum-dum/AP/magnesium variants). Every AI gun is clamped to
**velocity 600** and **fire rate 6.0** regardless of caliber, and its damage is roughly
**half** the player slug of the same caliber:

| Entry | Armor/Health | vs player slug |
|---|---|---|
| `wep_130` (30) | 1.5 / 1.5 | ½ of `wep_30`'s 3.0 |
| `wep_150` (50) | 3.0 / 3.0 | ≈½ of `wep_50`'s 6.25 |
| `wep_170` (70) | 5.0 / 5.0 | ≈0.43× of `wep_70`'s 11.75 |

## `FIRE` / `FLYOUT` / `IMPACT` bindings

Three keys bind a weapon to its effect and sound assets. Each is an alternating dict whose
slot values may be `null`.

**`FIRE`** — the muzzle event, over slots `ANIMATION` / `EFFECT` / `SOUND`. Guns give just
`ANIMATION ["muzzle_burst_slug"]`; rockets give `SOUND ["snd_missile_sm"]` with null anim.

**`FLYOUT`** — the projectile itself, over slots `MODEL` (the `.flt` handle), `MODEL_ANIMATION`
(the def the round runs from its spawn frame on its own anim clock: the trail, and on the torpedo
the whole launch look, its 3.5 s switch and its sounds — decoded per type in
[weapon-effects.md](weapon-effects.md#bullet-impacts) and for `torpedo_trail` in
[`org/ordnanceTypes.md`](../org/ordnanceTypes.md)), and `SOUND` (`snd_torpedo_loop` on the
torpedo alone; parsed into the def, but no reader of the slot exists in this build, so nothing plays
it). Present on all 48 entries.

**`IMPACT`** — keyed by **surface name**, one value per name. The names are the game's global
surface registry (the fourteen `soil` types a material can carry, [gamez.md](gamez.md)), so the
block is really an array indexed by surface **id**, and the struck material's own `soil` id selects
the row. Six of the fourteen names appear anywhere in this install:

| id | Name | On n entries | The struck surface |
|---|---|---|---|
| 0 | `default` | 47 | material carrying no distinguishing soil type — most terrain, and almost every building |
| 1 | `water` | 47 | water |
| 3 | `quicksand` | 3 | quicksand (no material in the shipped chapters carries it) |
| 6 | `player` | 44 | the player's own aircraft |
| 7 | `enemy` | 31, populated on 3 | an enemy aircraft |
| 11 | `buildings` | 47 | material tagged the `buildings` soil type, which is a handful of C1 polygons — NOT "geometry that looks like a building" |

Each row value is again an alternating dict over `ANIMATION` / `SURFACE_ANIMATION` /
`EFFECT` / `SOUND` (any may be null; a whole row may be null — no effect on that surface, which is
how 28 entries author `enemy`). `SURFACE_ANIMATION` is the surface-oriented variant of `ANIMATION`:
the engine spawns it with world up rotated onto the struck surface's normal, where a plain
`ANIMATION` keeps its fixed axis, so on flat ground the two read alike and on a slope only the first
lies on it ([`org/ordnanceTypes.md`](../org/ordnanceTypes.md) "Half one, the direct impact"; the
remake's `ProjectilePool.SurfaceUpBasis`). No row authors both slots. **Hit-testing reads the struck
material's surface id** to select the row.

A row is only reachable if some material carries its id: `quicksand`(3), `player`(6) and `enemy`(7)
are carried by no chapter material at all. A block named something outside the registry would be
parsed into nothing; the shipped data contains no such name. Counted per weapon and per id in
`analysis/surface-classification/FINDINGS.md`.

### An id the weapon never names inherits the `default` row

The table is built by walking the registry, and an id the weapon has no block for takes the
`default` row whole — its effect names and its sound list together. So the eight ids **no** weapon
names (`seafloor`(2), `lava`(4), `fire`(5), `airstrip`(8), `opensesame`(9), `death`(10),
`dzone`(12), `dirt`(13)) are not silent: a round striking them plays the weapon's ordinary
`default` impact.

**Naming an id and binding nothing on it is the opposite case, and plays nothing.** The block is
found, so it is read rather than inherited, and it yields a row with no bindings. That is `enemy` on
the 28 entries whose value is null, and the 30 cal slug's own `player`, whose slots are all authored
empty. The distinction is the whole behavioural content of the mechanism: nobody names `dirt`, so
every weapon's impact reaches it, while the guns name `player` and leave it empty, so a gun round on
an aircraft draws nothing — and a rocket, which never names `player` at all, throws its ground
burst there.

The engine-side mechanism (the parse loop, the row layout, the runtime index) is decoded in
[`../org/weaponImpact.md`](../org/weaponImpact.md).

The **`player` row is where the got-shot feedback on your own airframe is authored** — the 44
entries carrying it name the caliber's own `*_gunhit`, or `f18sparks2`, or (on `wep_03`, 60slug,
whose `enemy` row draws `5060slug_gunhit`) `SURFACE_ANIMATION: random_gun_impact` — the spark
burst at a `pdpN` panel documented in [vehicle.md](vehicle.md). Unreachable while nothing shoots
back: it needs an enemy aircraft firing at the player, so the whole row is untriggered in M3.

```json
"IMPACT": [
  "default", ["ANIMATION", ["3040slug_gunhit"], "SOUND", ["snd_grnd_bullet"]],
  "water",   ["ANIMATION", ["splash1.flt"],     "SOUND", ["snd_water_bullet"]],
  "enemy",   null,
  "player",  ["ANIMATION", null, "EFFECT", null, "SOUND", null],
  "buildings", ["ANIMATION", ["bld_damage.flt"]]
]
```

The animation/effect names (`large_fireball`, `flak_effect`, `rcochet1`, `bld_damage.flt`,
…) and sound defs (`snd_grnd_bullet`, …) point into the effect and sound reader families —
documented on the sibling pages **[weapon-effects.md](weapon-effects.md)** (the muzzle/flyout/
impact effect readers), [effects.md](effects.md) (the `PUFFER_STATE` particle system), and
[sounds.md](sounds.md) (the sound defs). They are not repeated here.

## Display names

`DESC` is an `MSG_WEAP_*` key resolved through the game's string table (the same
`messages.json` mechanism as mission text — see [missions.md](missions.md)); the
`MSG_WEAP_*` block runs ids 12124–12160. A sample:

| `NAME` | `DESC` key | In-game name |
|---|---|---|
| `30slug` | `MSG_WEAP_30CAL_SLUG` | 30-cal. slug machine gun |
| `30 DD` | `MSG_WEAP_30CAL_DUMDUM` | 30-cal. dum-dum machine gun |
| `30 AP` | `MSG_WEAP_30CAL_APIERCING` | 30-cal. armor-piercing machine gun |
| `30 EX` | `MSG_WEAP_30CAL_MAGNESIUM` | 30-cal. magnesium machine gun |
| `BOOM` | `MSG_WEAP_HEXPLOSIVE_ROCKET` | High-explosive rocket |
| `SEEKER` | `MSG_WEAP_SEEKER_ROCKET` | Seeker rocket |
| `TORPDO` | `MSG_WEAP_AERIAL_TORPEDO` | Aerial torpedo |
| `CB` | `MSG_WEAP_CANNONBALL` | Cannonball |

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
