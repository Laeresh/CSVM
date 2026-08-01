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

See [CLUSTER_SIZE vs AMMO_LIMIT](#cluster_size-vs-ammo_limit) for which entries carry which.

### Ballistics

| Key | n | Range | Meaning |
|---|---|---|---|
| `FIRE_RATE` | 48 | 0.3–10.5 | shots per second (guns 6–10.5; rockets ≈1.0) |
| `VELOCITY` | 47 | 1.0–1200 | muzzle / flyout speed, m/s |
| `ACCELERATION` | 16 | 0–150 | rocket-motor acceleration, m/s² (0 = constant velocity) |
| `RANGE` | 46 | 900–10000 | max effective / despawn range, m (Seeker 10000) |
| `RANGE_MINIMUM` | 1 | `[300, 0]` | minimum arming range (torpedo) |
| `GRAVITY` | 5 | 0.0 | projectile-gravity scale (0 throughout this install) |
| `CANNON_SPREAD` | 31 | 6.0 | gun dispersion cone, degrees (constant) |
| `FIRING_HEAT` | 4 | 5.0 | heat added per shot; only the base guns `wep_00`–`03` |
| `TURN_RATE` | 14 | 0.001–1.25 | guidance turn rate; 0.001 is effectively straight-flying — only the Seeker's 1.25 actually homes |

### Damage

| Key | n | Range | Meaning |
|---|---|---|---|
| `ARMOR_DAMAGE` | 46 | 0–200 | damage to a part's armor pool |
| `HEALTH_DAMAGE` | 46 | 0–200 | damage to a part's health pool |
| `DAMAGE` | 2 | 0.0 | a single combined value used *instead of* the armor/health split on the two non-damaging specials (`FLASH` `wep_09`, `FLARE` `wep_15`) |

`ARMOR_DAMAGE`/`HEALTH_DAMAGE` feed the `destroyable_parts` armor+health model in
[vehicle.md](vehicle.md).

### Guided flight & detonation (rocket block)

| Key | n | Range | Meaning |
|---|---|---|---|
| `ROCKET` | 15 | flag | marks a self-propelled projectile |
| `LOCK_ON` | 13 | 1.3–3.0 | lock-acquisition time, s |
| `LOCK_ON_LEAD` | 3 | `[4,8]` / `[5,10]` | target-lead parameters |
| `DETONATION_DISTANCE` | 13 | 1–50 | proximity-fuse trigger distance, m |
| `IMPACT_PROXIMITY` | 14 | 15–500 | blast / effect radius, m |
| `DETONATION_DOT_PRODUCT` | 3 | 0.1 / 0.3 | cone-alignment threshold for a proximity detonation |
| `DETONATION_TIME` | 1 | 2.0 | timed fuse, s (rear-arc flare) |
| `CRATER` | 6 | 0 | ground-crater flag/scale; marks the ground-attack munitions |

**Guided vs unguided is `TURN_RATE`, not a flag.** There is no `GUIDED` boolean. 13 of the 14
`TURN_RATE` carriers hold the sentinel **0.001** (fly straight = dumbfire — the HE "BOOM" rocket
`wep_06`/`wep_24`, AP, FLAK, incendiary, torpedo, …); **only the Seeker `wep_11` at 1.25 homes.**
`LOCK_ON` is *not* the discriminator — it is present on dumbfire rockets too (the HE rocket carries
`LOCK_ON 1.3`), because it is the aiming/lead acquisition time, not a steering promise. The Seeker is
also the sole `BEEPER_SEEKER`. Reader convenience: `WeaponDef.IsGuided` (`TURN_RATE > 0.01`).

### Class flags & specials

Each selects a special behaviour; most are one bare flag or a tiny struct.

| Key | Entries | Form | Meaning |
|---|---|---|---|
| `HIGH_EXPLOSIVE` | `wep_06`, `wep_24` | flag | HE warhead |
| `SONIC` | `wep_08` | flag | sonic shockwave |
| `FLASH` | `wep_09`, `wep_15` | flag | blinding flash |
| `BEEPER` | `wep_10` | `TIME [20]` | tags the target for 20 s |
| `BEEPER_SEEKER` | `wep_11` | flag | homes on a beeper-tagged target |
| `TANGLER` | `wep_12` | `TIME [2]`, `RADIUS [35]`, `ENGINE_DEAD [5,13]` | choker: entangles + kills the engine for 5–13 s |
| `SMOKE_SCREEN` | `wep_13` | `TIME [8]` | lays an 8 s smoke screen |
| `REAR` | `wep_13` | flag | fires rearward |
| `TORPEDO` | `wep_14` | flag | aerial torpedo |
| `TARGETABLE` | `wep_14` | flag | the in-flight projectile can itself be shot down |
| `FLYOUT_HEALTH` | `wep_14` | `[10]` | HP of the flyout projectile (pairs with `TARGETABLE`) |
| `PROJECTILE_BBOX` | `wep_14` | `[0]` | projectile bounding-box selector |
| `DESTROY_ANIMATION` | `wep_14` | anim | effect played when the flyout is destroyed |
| `DAMAGES_ZEPPELIN` | `wep_14`, `wep_28` | flag | may damage a zeppelin hull |
| `SHAKES_CAMERA` | `wep_26` | flag | camera shake on fire/impact |

### Bindings

| Key | n | Meaning |
|---|---|---|
| `CANNON` | 31 | flag: hitscan-style gun (pairs with `LOOPED_SOUND_NAME`) |
| `LOOPED_SOUND_NAME` | 31 | looped firing sound def (`snd_30cal`, `snd_turretgun`) |
| `FIRE` | 47 | muzzle event (below) |
| `FLYOUT` | 48 | projectile model / anim / sound (below) |
| `IMPACT` | 47 | per-surface hit effect (below) |

## `CLUSTER_SIZE` vs `AMMO_LIMIT`

Two allotment numbers with different meaning, long indistinguishable because on guns they
are equal:

- **Guns** (`CANNON`) carry `CLUSTER_SIZE == AMMO_LIMIT` (e.g. 2800/2800, 1200/1200).
- **Air-to-air rockets/missiles** (HE, AP, FLAK, SONIC, FLASH, BEEPER, SEEKER, SMOKER,
  TORPDO, FLARE — `wep_05`–`11`, `13`–`15`, `24`) carry **`CLUSTER_SIZE` and no
  `AMMO_LIMIT`**: they are allotted per pylon only. `wep_06`, the HE rocket, is the model
  case — `CLUSTER_SIZE [3]`, no `AMMO_LIMIT`.
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
| magnesium (`X3`) | ≈1.04–1.17× | ≈0.83–0.96× (a mild armor-leaning round, converging to balanced at higher caliber) |

Example (50-cal, `wep_50`–`53`): slug 6.25/6.25, dum-dum 3.125/9.375, AP 9.375/3.125,
magnesium 6.75/5.75. Base slug damage climbs with caliber (30→3.0, 70→11.75) while fire
rate falls (30-cal 8.0/s → 70-cal 6.0/s) and velocity drops (1000 → 750 m/s).

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
(spin/trail while in flight — decoded per type in
[weapon-effects.md](weapon-effects.md#flyout-model_animation--the-in-flight-smoke-trails)), and
`SOUND` (looped in-flight sound, e.g. the torpedo). Present on all 48 entries.

**`IMPACT`** — keyed by **surface class**, one value per class:

| Class | On n entries | The struck surface |
|---|---|---|
| `default` | 47 | terrain / anything unclassified |
| `water` | 47 | water |
| `buildings` | 47 | building geometry |
| `player` | 44 | the player's own aircraft |
| `enemy` | 31 | an enemy aircraft |
| `quicksand` | 3 | quicksand |

Each class value is again an alternating dict over `ANIMATION` / `SURFACE_ANIMATION` /
`EFFECT` / `SOUND` (any may be null; a whole class may be null — no effect on that surface).
`SURFACE_ANIMATION` is the surface-oriented variant of `ANIMATION`. **Hit-testing must
classify the struck surface** to select the right variant.

The **`player` class is where the got-shot feedback on your own airframe is authored** — the 44
entries carrying it name the caliber's own `*_gunhit`, or `f18sparks2`, or (on `wep_03`, 60slug,
whose `enemy` class draws `5060slug_gunhit`) `SURFACE_ANIMATION: random_gun_impact` — the spark
burst at a `pdpN` panel documented in [vehicle.md](vehicle.md). Unreachable while nothing shoots
back: it needs an enemy aircraft firing at the player, so the whole class is untriggered in M3.

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
