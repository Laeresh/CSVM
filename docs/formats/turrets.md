# Turrets and AA emplacements - `ai.zrd`

Part of the [format documentation](README.md). The shared `ai.zrd` reader defines 42 `TURRET`
entries for fixed anti-aircraft emplacements and turrets carried by aircraft or zeppelins. This
page pairs its field schema with the engine behavior that reads each key.

## Contents

- [Turret families](#turret-families)
- [Field table](#field-table)
- [Runtime behavior](#runtime-behavior)
- [Weapons](#weapons)
- [Value census](#value-census)
- [Scope limit](#scope-limit)
## Turret families

The 42 entries divide 16 / 26, and the discriminator is `CREATE_STANDALONE`:

| | Count | Discriminator | Placed by | `ACTIVATED` |
|---|---|---|---|---|
| **Carried** | 16 | `CREATE_STANDALONE 0` | a host looking the entry up **by `TITLE`** | `1` on all 16 |
| **Standalone** | 26 | key absent | a world pass over the entry's own `NODES` | `1` on 4, **`0` on 22** |

⚠ **The split is also exactly the awake/dormant split.** Every carried turret ships awake and every
dormant turret is a world emplacement. Aircraft and zeppelin turrets therefore need no
`WAKEUP_TURRETS` and no activation path at all — they come up with their host. Only the world AA
depends on mission scripting.

The world pass walks every `TURRET` entry and **skips those whose `CREATE_STANDALONE` is present
and zero**; the rest are instantiated at the scene nodes their `NODES` list names. The carried
turrets take the other path: a host (an aircraft or a zeppelin) names a `TITLE` and the loader
scans for the entry whose `TITLE` matches, building exactly one.

⚠ The two paths partition the file perfectly, and the shipped data agrees on every count:
`CREATE_STANDALONE` 16 = `HEALTHY_NODE` 16 (carried turrets bind to a node *on the host*), and
`NODES` 26 (standalone turrets bind to nodes *in the world*). 16 + 26 = 42. Nothing is in both
sets and nothing is in neither.

⚠ **An entry with no `TITLE` at all matches the by-`TITLE` lookup unconditionally.** The name
comparison is only reached when the key is present; an entry without one short-circuits into "build
this". No shipped entry omits `TITLE`, so this never fires in retail — but a reader that reorders or
filters entries must not rely on the lookup being total.

### The 16 carried entries, and the `_G1`/`_G3` suffix

The carried titles run `MSG_TUR_{AC,BRIGAND,FRONT,REAR}_{G1,G3}` and a `P`-prefixed mirror
(`MSG_TUR_{PAC,PBRIGAND,PFRONT,PREAR}_{G1,G3}`) — 4 mount positions × 2 suffixes × 2 sets. The
`P`-prefixed eight are the **player airframes** (`pavenger`, `pbalmoral`, `pbrigand`, `pfirebrand`,
`pkestrel` — [loadouts.md](loadouts.md)); the unprefixed eight are their AI counterparts. So the
player's own turret and an enemy's are the same system with different tuning rows.

⚠ **`_G1` and `_G3` are per-viewpoint rig selectors, not difficulty tiers.** Measured across all
eight pairs, a `_G1` entry and its `_G3` twin differ in **exactly two keys — `HEALTHY_NODE` and
`PARTS`** — and in nothing else. Every behavioural field (`INACCURACY`, `FIRE_RATE`,
`DETECTION_RANGE`, the arcs, the intervals, the weapon) is identical. What the suffix selects is
named by the host data itself: a vehicle def's `turrets` block ([vehicle.md](vehicle.md)) keys its
mounts **`firstp`** / **`thirdp`**, and the `firstp` entries name the `_G1` titles while the
`thirdp` entries name the `_G3` ones — the airframe models carry two turret rigs (e.g.
`kestrel_turret2` with `hturret2`/`hgun2`/`hfirepoint2` beside `kestrel_turret1` with
`hturret`/`hgun`/`hfirepoint`), one drawn in the cockpit view and one externally. The AI and
remote-player models carry only the `thirdp` rig, which is why only the player defs reference a
gun-group slot; the `firstp`/`thirdp` keys refute that.) Reading `G3` as "grade 3" and scaling
accuracy off it invents a difficulty system the data does not have.

### How a host names its turrets

The by-`TITLE` lookup's caller is the vehicle def's `turrets` block:

```
"turrets", [ "firstp", [ ["title", ["MSG_TUR_PAC_G1"], "node", ["kestrel_turret2"]] ],
             "thirdp", [ ["title", ["MSG_TUR_PAC_G3"], "node", ["kestrel_turret1"]] ] ]
```

`title` is the `ai.zrd` row; `node` is the turret-rig subtree on the airframe model the row's
`PARTS` names resolve **inside** — the same part names (`hturret`, `hgun`, …) repeat on the other
viewpoint's rig and on every other plane of the type, so the lookup must be scoped to the mount's
subtree, never global. 16 defs carry the block: the five player turret airframes (both rigs), their
six AI variants and five `r*` remote-player variants (`thirdp` only).

⚠ **One shipped model node name carries a trailing space** — the Brigand's first-person yaw ring is
literally `"brigturret2 "` in `planes.zbd`, where `ai.zrd` names `brigturret2`. A reader that
matches names exactly silently loses that rig; trim before comparing.

`NODES` entries are **name patterns, not single names** (`["aagun**"]`, `["thug*"]`), resolved
against the scene graph by the shared wildcard rule ([README.md](README.md#shared-conventions-zrdr-readers)).
One entry therefore instantiates **as many turrets as there are matching nodes** — the count of
emplacements in a mission is a property of the world model, not of `ai.zrd`.

## Field table

All 22 keys the engine accepts. Angles are authored in **degrees** and converted to radians at load;
times are seconds; distances metres.

| Key | Shipped | Meaning |
|---|---|---|
| `TITLE` | 42 | `MSG_TUR_*` string key — also the lookup name for carried turrets |
| `ACTIVATED` | 42 | initial awake state; **22 entries ship `0`** (see below) |
| `PARTS` | 42 | `[yawNode, pitchNode, firepoint(s)]` or `[pitchNode, firepoint(s)]` |
| `WEAPON` | 42 | sub-block: `NAME`, `AMMO`, `DETECTION_RANGE`, `FIRE_RATE`, `FIRE_LIMITS` |
| `INACCURACY` | 42 | half-angle of the shot-scatter cone, degrees |
| `ATTACK_INTERVAL` | 42 | scalar or `[min,max]` — how long a firing spell lasts |
| `BORED_INTERVAL` | 42 | scalar or `[min,max]` — how long the pause between spells lasts |
| `PITCH` | 37 (+1 stray) | `[min,max]` elevation arc — see the mis-nesting note below |
| `SOUNDS` | 37 | sub-block: `ON`, `START`, `STOP`, `CANNON`; the cannon sound has a refreshed 0.5 s playback lease |
| `YAW` | 36 | `[min,max]` traverse arc |
| `NODES` | 26 | standalone placement patterns |
| `TEAM` | 20 | always `1` = **ally** where present; absent = **enemy** (see "Teams" below) |
| `HEALTH` | 17 | authored on the standalone family but **read by neither loader** (see below) |
| `CREATE_STANDALONE` | 16 | always `0`; see above |
| `HEALTHY_NODE` | 16 | the host node whose destruction kills the turret; defaults to `healthy` |
| `DEACTIVATE` | **0** | a second node whose destruction *disables* the turret |
| `EFFECT` | **0** | `[node, duration]` — an effect node shown while firing |
| `FIRE_LIMITS` | **0** | `[burst, cooldown]` duty cycle on the gun itself |
| `STICKINESS` | **0** | an angle, degrees: this turret's own gun-aim-assist cone, overriding the firing weapon's. See [`org/aim-assist.md`](../org/aim-assist.md); a value not greater than 0 is stored as "no override" |
| `SHOOT_UP_ONLY` | **0** | reject targets below the turret's own altitude |
| `CATEGORY_LABEL` | **0** | a raw (unlocalised) label string |
| `HELP_LABEL` | **0** | a string-table id, localised at load |

**Eight of the 22 keys are accepted but never authored.** They are listed because the engine
parses them and a faithful reader should not reject them — not because anything reads them in
retail. A remake needs the fourteen that ship.

### Teams

The turret's team feeds the shared target-picker's gate. The engine's team space (the zeppelin
loader's own name mapping) is **0 = neutral, 1 = ally (the player's side), 2 and up = enemy
teams**, and the turret loader's default for an **absent `TEAM` key is the first enemy team,
id 2** — which is why the 22 no-TEAM world emplacements all engage the player. Every authored
value install-wide is `1`: the 16 carried entries (whose team the host overrides anyway) and the
four `piratezep` entries — the player's own zeppelin's defensive rings are allied on purpose,
three of them also the only standalone entries shipped awake. The retail data establishes
loader's TEAM arm (an absent key takes the enemy-from-index constructor at index 0; a present
integer is stored raw) and the zeppelin parser's `enemy`/`ally`/`neutral` string mapping.

### `HEALTH` is authored but unread

⚠ **Neither turret loader reads the `HEALTH` key** — the world placement pass and the by-`TITLE`
carried pass parse the same 20-key entry routine, and `HEALTH` is not among its lookups
(no `HEALTH` string exists in the turret module's key cluster). A
standalone emplacement's real hit points are its **own node's gamez destroy definition** —
`aagun32` authors `HEALTH 8.0` in `ai.zrd` while its `destroy_aagun32` anim def carries
`health: 30`, and the 30 is what kills it. The turret dies through gate 1 above: the destroy
sequence's healthy→destroyed swap deactivates the `HEALTHY_NODE` (default `healthy`), and the
gunner goes permanently quiet. Treat the `ai.zrd` value as editor-era authoring, not a pool.

The `SOUNDS` sub-block likewise ships only `CANNON` (37 entries, always `snd_chaingun`); `ON`,
`START` and `STOP` parse and are never used. `snd_chaingun` is a `LOOPED` sound definition. A
turret does not play it as an isolated clip: the firing path owns one reusable sound handle and
refreshes its expiry to 0.5 seconds after every shot. The sound manager releases that handle only
after the lease expires.

**The handle is one slot per turret INSTANCE, not per owner.** The entry loader takes its slot id
from a global counter that only increments (`FUN_0045e460`, `DAT_0071b1fc`, called at `0x004aa1e0`
and stored at turret `+0x208`), and the lease helper keys a map on that id, so seventeen rings on
one zeppelin hull hold seventeen independent voices. The lease call (`0x004ab1d9`) hands the helper
the turret's firepoint position (`+0x188`, the same vector the projectile spawner is given) and its
velocity (`+0x194`) with the Doppler argument **zero**, so the voice is a point at the muzzle a round
just left and carries no pitch shift. Attenuation is the definition's own `RANGE` pair, silent past
1.1 x its audible distance (`FUN_00597c20`).

⚠ **Eleven of the 42 entries are silent by the data's own two gates, and that is not a defect.**
The lease call is guarded on `SOUNDS.CANNON` being present AND on the entry's weapon carrying the
`CANNON` flag; there is no fallback to the weapon's own `LOOPED_SOUND_NAME`. Five entries author no
`SOUNDS` block at all (`MSG_TUR_AAA` on `aagun**`, `MSG_TUR_THUG`, `MSG_TUR_TRAIN`, `MSG_TUR_TRUCK`,
`MSG_TUR_8_INCH`), and six more name `snd_chaingun` over a weapon that is not a cannon (`wep_27` on
`MSG_TUR_NOSE`, `MSG_TUR_HEAVY`, `MSG_TUR_MAIN`, `MSG_TUR_BALLOON_TOP` and `MSG_TUR_ZEP_CANNON`,
`wep_06` on the other `MSG_TUR_NOSE`). A turret truck's emplacement and a zeppelin's twin cannon
therefore fire with no gun voice, and a build that gives those mounts one is louder than the
original.

⚠ **That silence is the EMPLACEMENT's, and a turret truck carries a second gun that is not silent.**
The `t_truck` vehicle def arms one `wep_29` of its own, fired through the general vehicle path rather
than this one, and that path's voice is the weapon's `LOOPED_SOUND_NAME` (`snd_turretgun`) on a lease
of zero renewed every tick ([../org/weaponFire.md](../org/weaponFire.md)). The two guns sit on the
same model and answer to different routines, so neither reading tells you anything about the other.

⚠ **`PITCH` is authored at top level on 37 entries, not the raw count of 38**: the train turret
(`MSG_TUR_TRAIN`) nests its one `PITCH [20,80]` **inside its `WEAPON` block**, where the turret
parser does not read it — that turret ships with no elevation arc at all. A census that greps the
key counts 38 and hides the stray.

### `PARTS` is a kinematic chain, not a name list

The list length decides the rig:

- **3 elements** → `[yawNode, pitchNode, firepoints]`. Yaw is written to the first node's
  transform, pitch to the second.
- **2 elements** → `[pitchNode, firepoints]`. There is no traverse ring; a single combined
  rotation is written to the one node.

The last element is either a single firepoint name or a **list** of them
(`["brigturret", "hgun", ["hfirepoint", "hfirepoint1"]]`, 4 of 42 entries). Multiple firepoints are
cycled **round-robin, one per shot** — the muzzle advances by one index each time the turret fires
and wraps at the end. They are not fired together.

### The arcs, and why `[0,0]` means *free*

`PITCH` and `YAW` are `[min,max]` pairs in degrees. The engine clamps **only when `min != max`**:

- **Pitch** — a plain clamp to `[min,max]`.
- **Yaw** — a *wrap-aware* clamp. The arc is a directed interval on the circle, so
  `YAW [105, 255]` is the 150° arc running from 105° to 255°, and `YAW [-155, -5]` is a different
  arc, not the same one renamed. A target outside the arc is not clamped by shortest path: the
  engine tries the ±2π alias and, if that is still outside, snaps to whichever **end stop is
  angularly nearer**.

⚠ **`YAW [0, 0]` does not lock the turret forward — it removes the traverse limit entirely.** One
shipped entry authors it, and because the clamp is gated on `min != max` the raw solved yaw passes
through untouched. The same holds for an absent key (both limits default to zero). Reading `[0,0]`
as "fixed" points that turret permanently down its rest bearing and it will never fire.

At load the turret is posed at the **centre of its arc** — `min + (max-min)/2` on each axis — and an
axis whose limits are equal is left unrotated.

## Runtime behavior

### Being alive, and being awake

Three gates, in order:

1. The **`HEALTHY_NODE`** must be alive. If it dies the turret goes permanently quiet. A node
   under a site the mission's `.gw` switched off (`NodeSetActive off b_turret1`, C3/M03) is out
   of the world with its subtree, so the gate reads the node's visibility in the tree, never its
   own flag alone; CSVM's `TurretController.Alive` does the same (`mission-off-turrets` suite).
2. The **`DEACTIVATE`** node, if the entry names one, must *also* be alive — destroying it
   disables the turret without destroying it. (Unauthored in retail; the mechanism exists.)
3. **`ACTIVATED`** must be set. **22 entries ship `ACTIVATED 0`** and are inert until something
   wakes them: `WAKEUP_TURRETS` and `WAKEUP_ZEP_TURRETS` ([ai-nets.md](ai-nets.md) lists
   the script vocabulary), or the Instant Action mission builder (below). All 22 are standalone
   world emplacements; no carried turret is dormant. `ACTIVATED` is also savegame state, not just
   a load-time value: it round-trips through save/restore alongside `AMMO`. The field is turret
   byte `+0x6e`, written by the entry loader at `0x004aa7bb` and read as the tick's gate at
   `0x004aac16`.

⚠ **The awake-at-load set is WORLD-MODEL dependent, not per-chapter authored.** The `piratezep`
model (and its allied TEAM-1 rings) is part of every chapter's world, so C1 and C4 both census 15
awake at load; C5 adds the hostile `thug*` boats.

### Waking a whole subtree

The two script ops are two different primitives, which is why they have separate names. The
per-turret one takes a turret and sets its byte. The **subtree** one takes a *node*, recurses over
its children, looks each one up in the turret list by the turret's own node pointer, and writes the
flag on every turret it finds standing there, so one call arms (or stows) every ring on a hull.

⚠ **The subtree form is not script-only.** Instant Action's mission builder calls it directly, with
`0` for each zeppelin it switches off and `1` for the `zeppelin_run` objective
([instant-action.md](instant-action.md#the-turret-arm-is-what-arms-the-instant-action-zeppelin)).
Since the four `multiplayer1zep`/`multiplayer2zep` entries all ship dormant and Instant Action runs
no objectives script, that builder call is the only reason the zeppelin you attack shoots back.
The builder's own subtree write runs BEFORE a mission-script wake — the order the binary has — so
a stand-in for the mission-script layer applied afterward still wins on the same turrets.

Separately, **both loaders bail out entirely if a global world-state flag is clear** — the same
flag that gates the `capacity` read in the generator loader
([mission-entities.md](mission-entities/enemy-generators.md#capacity-rule-and-limit)). In retail it must be set, or no
turret would exist at all; it is noted because the two subsystems share it.

An entry with no resolvable `WEAPON` ticks no further.

### Attack and bored are a duty cycle, not two moods

The turret holds a two-state timer:

```
attacking → (attack window expires) → bored, for  uniform(BORED_INTERVAL)
bored     → (bored  window expires) → attacking, for uniform(ATTACK_INTERVAL)
```

Where the key is a `[min,max]` pair each window is redrawn uniformly at random from that range;
where it is a scalar, min and max are the same value. The windows are re-rolled every transition,
so no two turrets stay in phase.

⚠ **Bored suppresses firing only — it does not suppress tracking.** The aim solution is computed
first and the fire flag is cleared afterwards, so a bored turret keeps its gun on the player the
whole time it is holding fire. This is visible behaviour and worth reproducing: the original's
emplacements track continuously and shoot in bursts.

### Acquiring

`WEAPON.DETECTION_RANGE` (350–1000 m across the 42) is handed to the engine's shared
target-picker as the turret's search field, together with the turret's world position, its team,
and the `SHOOT_UP_ONLY` flag. The picker scores every candidate and takes the minimum — the same
minimise-a-score structure the aircraft AI uses ([ai-rosters.md](ai-rosters.md)).

`SHOOT_UP_ONLY` rejects any candidate whose altitude is below the turret's own. Unauthored in
retail.

**Line of sight is only tested against the player.** When the acquired target is the player's
aircraft the engine casts against the world from the turret (or from its host vehicle, for a
carried turret) to a point 0.2 m above the target, and **caches the result for a random 1–2
seconds** before re-testing. A failed test blocks firing. Against non-player targets there is no
occlusion test at all.

⚠ **Whatever that cast hits, it cannot be the thing the gun is bolted to.** Every standalone entry
stands on modelled geometry: a zeppelin hull, a balloon, a boat, an AA gun's own body. A test that
counted the mount as cover would silence all 26 of them from the first frame, so the mount is not
in the cast. The decode does not say by which mechanism (a partition holding only static world
geometry, or an explicit exclusion of the owning object), and the two are indistinguishable from
the data. Measured the hard way in the remake: with the mount in the cast, C1/IA1's zeppelin rings
reported blocked by their own `turret` body at 0.5 m and by their own hull panels at 19–25 m of a
150–400 m shot.

What the remake keeps out of the cast depends on what the gun stands on. Every emplacement skips
the first **1.5 m** off its own node. A gun whose site is a direct child of the world root, which
is every ground entry (`aagun`, `maagun`, `tcargun`, `t_truck`, `8igun`), also excludes its own
rig's collider bodies by RID, the same set `PlatformColliderRids` hands the hit ray as the bodies a
round it fires owns. A ring on a hull or a gun on a balloon gets the skirt alone.

The skirt alone cannot serve a ground gun. An `aagun` rig carries a 12 m by 12 m base, a pyramid
reaching 3.5 m over the gun's node and the `col_buildings` pad, and probed from a live C1/M02
session with the turrets woken, aagun32's ray toward targets at sixteen bearings and elevations
from 6 to 64 degrees struck one of its own bodies at 1.67 m to 5.69 m every time, well past the
skirt. Five guns, five `Blocked` gates, from the first frame on. The suites that had passed
on this rule were reading a physics space with two of the three shapes missing, which is the
harness gap `BL-831` names.

A node-group exclusion is still wrong for anything mounted on a hull, whichever group is chosen:
excluding a zeppelin entire lets its rings shoot straight through their own hull, excluding only
the gun's own rig blocks all 14 of them because the panel colliders engulf the ring they carry,
and excluding the **mounting section** in between leaks just as badly, because a section is a
modelling group and holds the far side of the same hull. On `piratezep` the section rule left
**422 of 517** in-arc bearings that cross 120 m or more of the hull's own body clear, and the
belly rings `ctur1` to `ctur3` fired straight through 162 to 255 m of it. The distance rule leaves
172, and every ring keeps 54 % to 79 % of its in-arc field of fire. A ground gun's site holds
nothing but the gun, so for it the own-rig exclusion is the mount and nothing wider.

The 1.5 m is measured, not decoded: a ring's own bodies engulf it out to about 1 m (`g21` and
`gun` answer at 0 to 1 m), and the nearest hull skin standing over one answers at 2 m. The
residual 172 is mostly hull that carries no collider at all, such as the gasbag ends
`doublecannon4` and `doublecannon5` look across, which is a collision-data question and not a
line-of-sight one.

⚠ **The mounting section is still what a round this gun fires owns**, for the hit ray and the
splash below. Only the line-of-sight test stopped reading it.

### Aiming

1. **Lead.** If the weapon is flagged as a leading weapon, the engine solves a true intercept from
   the muzzle position, the projectile speed, and the target's position and velocity. Otherwise it
   aims straight at the target's current position.
2. **Clamp** the solved pitch and yaw to the arcs, as above, and rebuild a direction from the
   clamped angles.
3. **Slew.** The turret's actual barrel direction is moved toward that direction at a bounded rate
   rather than snapped — the rate constant is `3.0` in the binary, and the resulting direction is
   renormalised each tick. This is what makes the original's turrets visibly swing.
4. **Write** the resulting yaw and pitch onto the `PARTS` nodes.

For a turret on a moving host (a zeppelin), the platform's own velocity is differenced from its
position across the frame and folded into the solution — with a sanity cut that discards the
estimate if it implies a platform speed over about 447 m/s.

### Firing

Two conditions gate the shot:

- The barrel must be within **15°** of the solved aim direction (the binary compares against
  `0.965926` = cos 15°). A turret that is still slewing does not fire.
- If the weapon leads and **no intercept solution exists** — the target is too fast, or outside the
  projectile's reach — the turret does not fire at all. It does not fall back to a straight shot.

`WEAPON.FIRE_RATE` is the interval between shots, redrawn as `uniform(min, max)` after each one
(the carried entries author scalars, min = max; most standalone entries author real
`[min, max]` pairs, e.g. the aagun belt's `[1.0, 1.8]`). `FIRE_LIMITS`, were it
authored, would add a duty cycle on top: a charge that drains while firing and recharges at the
same rate while not, forcing a `FIRE_LIMITS[1]`-second cooldown when exhausted.

**Projectile cadence and cannon audio have separate lifetimes.** Each elapsed `FIRE_RATE`
interval emits exactly one projectile; the referenced ballistics record's own `FIRE_RATE` does
not govern a turret. That same shot refreshes the turret's reusable `SOUNDS.CANNON` handle for
0.5 seconds. Because the carried Firebrand row has a 0.4-second interval, its next projectile
arrives before that lease expires and `snd_chaingun` remains a continuous machine-gun loop through
the firing spell. Once firing stops, the loop ends within 0.5 seconds. A playback implementation
that restarts `chaingun.wav` as a non-looped one-shot at each projectile preserves the ballistic
rate but turns the audible firing spell into isolated shots.

**A gun's own rounds do not hurt it.** The splash gather clears the round's owner node's
intersect bit for the whole gather ([org/ordnanceTypes.md](../org/ordnanceTypes.md) "Half two, the
splash"), so a flak bursting beside the emplacement that fired it reaches every neighbour and never
the shooter. Which node an emplacement's round names as its owner is not decoded; the remake uses
the gun's own mounting section for both the hit ray and the splash, which is the one thing that
set is still read for. Measured before that exclusion: a flak bursting 12 m over its own pit dealt the
gun four splash shares at once (one per collider body of the destructible, -8.18/-8.04/-8/-7.36 of
its 10), and a strike on the fort's barrier beside the gun the -10/-9.64 pair the CM07 sortie logged.

**`INACCURACY` perturbs the shot, not the barrel.** The scatter cone is applied to the fire
direction *after* the aim solution and after the model nodes have been written, so the turret is
seen to aim true and the rounds spread. It is a half-angle in degrees, 2.5–15.0 across the 42.

### Hit resolution is geometric

⚠ **There is no hit probability.** After the scatter cone is applied, the engine takes the cosine
between the perturbed fire direction and the direction to the target and compares it against the
target's angular radius at that distance. Inside, the shot is issued with a hit against that
target; outside, it is issued as a miss. What reaches the weapon system is a tracer with an
explicit hit-or-not verdict, decided by geometry.

This is the same finding as the zeppelin broadside cannons, which the design document also
described as a probability ramp ([mission-entities.md](mission-entities.md#broadside-firing)): in
both cases the shipped engine resolves ballistically and the probability language is design-era.

## Weapons

All eight distinct `WEAPON.NAME` values are `BALLISTICS` ids resolving in `weapons.json`
([weapons.md](weapons.md)); six sit in the AI-detuned `wep_1xx` tier.

| id | uses | display | armour/health dmg | range | velocity |
|---|---|---|---|---|---|
| `wep_140` | 16 | 40-cal slug (AI tier) | 2.0 / 2.0 | 1000 | 600 |
| `wep_29` | 13 | turret gun | 0.25 / 0.25 | 1000 | 450 |
| `wep_27` | 6 | AA flak rocket | 10 / 10 | 900 | 850 |
| `wep_23` | 3 | turret gun (MP) | 1.0 / 1.5 | 1000 | 400 |
| `wep_30` / `wep_60` / `wep_28` / `wep_06` | 1 each | 30-cal, 60-cal, cannonball, HE rocket | — | — | — |

⚠ `WEAPON.NAME` is a `BALLISTICS` **id**, not a display name. `30slug` is the *inner* `NAME` field
of a ballistics record and is a different thing.

`WEAPON.AMMO` is only ever `9999` or `12000` — effectively unlimited, but it is real state: it
persists across save/restore.

## Value census

`INACCURACY` 2.5–15.0 (8 distinct) · `PITCH` −60…85 · `YAW` −180…269 · `ATTACK_INTERVAL` 2.0–30.0 ·
`BORED_INTERVAL` 2.0–10.0 · `DETECTION_RANGE` 350–1000 · `FIRE_RATE` 0.15–12.0 · `AMMO` 9999 or
12000 · `TEAM` always 1 · `HEALTH` 2 / 8 / 10 / 30 · `SOUNDS.CANNON` always `snd_chaingun` ·
`TITLE` 32 distinct `MSG_TUR_*` keys, all resolving in `messages.json`.

## Scope limit

- Turret **airframes** — the five aircraft that carry a turret, and the marker rig that mounts it —
  are [markers.md](markers.md) and [vehicle.md](vehicle.md). `ai.zrd` supplies the gunner, not the
  mount.
- Zeppelin **broadside cannons** are a separate system with their own arc and fire logic, in
  `zeppelins.json` ([mission-entities.md](mission-entities.md)). `WAKEUP_ZEP_TURRETS` and
  `COMPLETED_ZEPCANNONS` are different script ops for a reason.

## Evidence & limits

The turret entry loader is `FUN_004a9df0`; it reads `FIRE_RATE` into the turret timer range and
allocates one cannon-sound slot per turret. `FUN_004aabb0` is the complete update: after its aim,
duty-cycle and fire gates, it calls the projectile spawner `FUN_005aef40` exactly once and redraws
the next-shot timestamp from the turret range. The same branch passes `SOUNDS.CANNON` and a literal
0.5-second lease to `FUN_0045e470`. That helper reuses the slot's existing sound handle and moves
its expiry to sound-clock-now + 0.5; `FUN_0045e360` stops and clears the handle only after the
sound clock passes that expiry. These executable paths settle the projectile/audio distinction;
the `snd_chaingun` `LOOPED` flag and the Firebrand's 0.4-second scalar come from the shipped reader
data.

The slot the helper keys on is minted at `0x004aa1e0` from the only writer of `DAT_0071b1fc`, which
just returns and increments, so no two turrets share one. `FUN_0045e470` creates the handle on the
FIRST shot (`FUN_00593590` at volume 1.0) and thereafter only repositions it through `FUN_00597af0`,
whose 3D branch `FUN_00597c20` writes the handed position and velocity and takes its fourth argument
as the Doppler enable, which the turret call passes as zero. The `CANNON`-flag half of the gate is
read at the call site off the weapon record's parsed flags (bit `0x40`, `FUN_004ba6f0`); which
entries that leaves silent is a census of the shipped `ai.zrd` and `weapons.zrd`, not a decode.
