# vehicle.json — aircraft definitions

Part of the [format documentation](README.md). Validated against this install's zrdr
extraction (mech3ax v0.6.1), decoded across Milestone-2 flight work and Run-2 item 10
(2026-07-19). One reader file, shared by every mission scope; the root list alternates
`defName, [properties…]`.

## Def structure & inheritance

Each def's property list is an alternating key/value-list dict. `kind_of` names the parent
def; properties resolve **nearest-first** through the chain (e.g. `pbloodhawk` →
`player_airplane` → `basic_airplane`). Player planes are the defs whose chain contains
`player_airplane`; `nodename` names the GameZ model root (`player_bhawk`). AI variants
(wingmen, pirates) are separate defs with the same shape but different numbers.

Keys the remake consumes (see `src/Flight/PlaneStats.cs`):

| Key | Meaning |
|---|---|
| `kind_of` | parent def (inheritance chain) |
| `nodename` | planes.zbd model root node |
| `engine` | engines.json row id → power factor |
| `engine_sound` / `cockpit_engine_sound` | sound-def names (SETS in sounds.json) — only `cockpit_engine_sound` unconsumed (needs a cockpit view) |
| `damaged_engine_sound` | `[[soundName, f0, f1]]` — one shared `basic_airplane` entry (`snd_damagedengine`, 0.0, 1.0) covers every plane; `f0`/`f1` are undecoded and read as a fade window over accumulated damage fraction (below) |
| `dynamics` | nested dict: `pitch_torque`, `roll_torque`, `rudder_torque`, `return_rate`, `ang_momentum_damp`, `rec_moments_inertia` (xyz), `fd_speed` (m/s), `drag_factor`, `veh_weight`, `ref_area` |
| `spin_props_anim` / `stop_props_anim` | prop-disc anim names (plane_props.json) |
| `start_anims` | anims run at spawn (`wing_lights_blink`, `reset_bulletholes`) |
| `injure_anims` | def-level damage thresholds (below) |
| `destroyable_parts` | the damage model (below) |
| `collision` | 6 collision probe points (below) |
| `bullethole_anims`, `weapons`, `turrets`, `cannon_jam`, `armor`/`health`, AI tuning | not consumed yet — [Weapons, damage & AI keys](#weapons-damage--ai-keys) below |

## Units, dynamics & engines

Units are meters/seconds: `fd_speed` 135 m/s ≈ 302 mph matches the Bloodhawk's published
top speed; `flight_ceiling` 2500 m. `player.json` holds player-global values —
`nom_gravity` = 20 m/s² (an arcade 2 g) — plus the sound curve blocks
([sounds.md](sounds.md)).

The `dynamics` block: `rec_moments_inertia` is the *reciprocal* inertia per axis
(x = pitch, y = yaw, z = roll); steady-state rotation rate = torque · recInertia /
`ang_momentum_damp` (Bloodhawk roll ≈ 1.65 rad/s). `return_rate` is extra centering
applied when the stick is released. `fd_speed` is the full-throttle level-speed
equilibrium (drag balances thrust there).

`engines.json` is a flat list of rows `[id, name, power]`; a plane def's `engine`
property picks its stock engine by id (Bloodhawk: 11 = Lvl-2, power 0.62). Engine power
scales thrust/acceleration; `fd_speed` stays the level-speed cap.

## `player.json` — the player-global blocks

One shared reader, flat alternating `KEY, [values…]`, ~45 top-level keys. Flight globals
(`nom_gravity`, `maxAOA`, `liftAOAs`, `highGs`/`lowGs`, the `yaw_*`/`turn_*` fade curves,
`stall_mag`, `drag_factor`) feed `PlaneStats`; the sound curve blocks (`engine_sound`,
`prop_sound`, `rattle`) are in [sounds.md](sounds.md). Three whole subsystems in it are
**undocumented elsewhere** — key names and values are data-confirmed, the meanings are read off the
names and are **inferred**; one of the three (the near-miss counter) is now implemented on that
inferred reading, the other two are not. None appears in the original design
document, so they are shipped-only features.

| Keys | Values | Reading |
|---|---|---|
| `sticky_bullet_catchup_rate` `_inaccuracy` `_forget_interval` `_dist_factor` | 5.0 / 1.0 / 1.5 / 0.0 | **Bullet magnetism / aim assist.** Rounds already in flight are steered toward a tracked target at `catchup_rate`, within `inaccuracy`, dropped `forget_interval` seconds after the lock is lost; `dist_factor` 0 disables any range scaling. |
| `warning_shot_max` `_dissipation` `_interval` `_sound` | 2.0 / 2.0 / 1.0 / `bullet_warning_sg` | **Near-miss feedback — implemented** (`WarningShotCue`, 2026-08-02). A counter of rounds passing close by, capped at `max`, decaying at `dissipation` per second, with the sound group re-triggering no faster than `interval`. **The units are not in the data**: the remake accrues 1.0 per pass, which makes `interval` the term a pilot hears. **Nor is the trigger distance** — nothing here says how close is close, and the sound def's `RANGE [20,200]` is the 3D falloff window, not a radius. Pairs with `bullet_hit_sound`, still unbuildable (nothing can strike an aircraft). |
| `smokescreen_stun_range` `_angle` `_interval` | 600 m / 170° / 5.0 s | **The smokescreen weapon's blind effect** — who it stuns: within 600 m, inside a 170° arc, re-evaluated every 5 s. Matches the design's stun-recovery pilot skill and the flare/sonic-rocket stun. |

Also worth naming, all data-confirmed: `crash` (`armor_damage_range`, `health_damage_range`,
`bounce_factor` — see the [hp-pair hypothesis](#the-hp-pair-armor--hit-points-hypothesis));
`autohead_turn_time`/`_max`/`_min_pitch` (the padlock/look camera's head-turn rate limits — see
the [command inventory](strings.md#the-bindable-command-table-messagesjson)); `rogue` (three
`[fameThreshold, soundName]` steps warning a player who is shooting allies);
`respawn_rad`/`respawn_el` (multiplayer respawn ring); `score_kill`/`_zep`/`_suicide`/
`_return_flag`/`_enemy_flag` (multiplayer scoring); `min_ai_active_dist` 2000 m;
`ai_skill_parameters` (the chance/factor curves the nine pilot skills index into).

## destroyable_parts (Run-2 item 10)

A list of part entries:

```
[name, hp, hp, flags…,
 "got_hit_anim", [animName, rootName],
 "injure_anims", [[fraction, animName, rootName], …]]
```

- `name`: `nose` / `tail` / `leftwing` / `rightwing` for every player plane.
- **The two `hp` values are equal in every entry** — all 88 parts across the 22 defs that carry
  `destroyable_parts` (11 player `p*` + 11 AI `r*`), measured; values 15/20/25/30/35/40. The
  remake takes the first as max HP and spends only `HEALTH_DAMAGE` against it (backlogged).
  *(An earlier version of this page said AI variants differ 25/20 — that is wrong; nothing in
  this install has an unequal pair.)*
- **Hypothesis: the pair is (armor, hit points).** See [below](#the-hp-pair-armor--hit-points-hypothesis).
- Flags: `critical` — the plane is destroyed when this part reaches 0 HP (all four player
  parts carry it); `engine` — engine damage/power loss on that part. Not tail-only: it sits on
  the tail for `pbloodhawk`/`pdevastator` but on the nose for `pautogyro`/`pbrigand`/`pfury`/
  `ppeacemaker`/`pbalmoral`/`pwarhawk` and on **both wings** for
  `pfirebrand`/`pavenger`/`pkestrel` — it
  marks the part the engine(s) physically live in. Handling penalties are unmodelled **by
  design**, not deferred: the original design states damage does not degrade an aircraft's
  performance, a plane on its last legs keeping full performance and lethality. The shipped
  flag may still drive something (sound, effects) the design text does not cover.
- `injure_anims`: **descending HP fractions**; when the part's HP fraction crosses one,
  the named anim runs. Two families interleave:
  - `<part>_damage_green` (0.72) / `_yellow` (0.46) / `_red` (0.20) — the cockpit
    damage-indicator texture cycle (unwired until a cockpit exists).
  - `<part>_damage_effects` (0.99) — **not part of that cycle, despite the neighbouring
    thresholds: this is the per-impact spark burst.** All four (`nose`/`tail`/`leftwing`/
    `rightwing`) are one-event shims calling `random_gun_impact` (anim root `player`) with no
    parameters, so the part identity is discarded by the data itself. `random_gun_impact` is an
    IF/ELSEIF `RANDOM_WEIGHT 0.4` / `0.4` pair choosing `yellow_sparks_follow WITH_NODE pdp1` or
    `pdp2`, **followed by an unconditional third call at `pdp4`** — so a hit sparks one panel or
    two, never none. `yellow_sparks_follow` (root `yellow_spark_02`) emits `trailpuffer2` +
    `chippuffer1` at its INPUT_NODE (the chosen panel) under a 50/50 pick between two
    `snd_ricochet1–4` sequences. ⚠ **Only `player_pfighter` ships the 0.99 entries** — measured
    install-wide, exactly 4 occurrences, all in the def whose `nodename` is `player_pfighter`
    (`title MSG_VEH_DEVASTATOR`), while each of the 11 planes spells its own `got_hit_anim`.
    Inheritance does not spread it: all 22 defs carrying `destroyable_parts` (11 `player_*`
    flyables + 11 lowercase AI variants) are `kind_of` a base that carries no parts list of its
    own. The other 10 aircraft have no per-impact spark at all.
    At 0.99 it fires on the *first scratch*, which is authored, not a threshold to retune.

    ⚠ **`random_gun_impact`'s real home is `weapons.json`, not here — read this entry as a probable
    authoring leftover (hypothesis, 2026-08-01).** It is the `player` **IMPACT surface animation**
    for `wep_03` (60slug) — "what a bullet does when it hits the player's aircraft"
    ([weapons.md](weapons.md)), the counterpart of the `enemy` and `default`/`buildings` classes.
    That is a general mechanism gated on being shot at, which nothing can do in M3. One plane of
    eleven ALSO firing it off a damage threshold fits a leftover better than a per-aircraft
    feature — but no capture of the original settles it, so it is a reading, not a finding, and the
    entry is shipped data either way. Do not "fix" the other ten planes by adding the entry to them;
    that would be inventing content.
  - `pdpanelN` — flips the exterior torn-skin panel `pdpN` (planes.zbd nodes; the anims
    live in the plane's own reader, e.g. player-1.json). Left wing: pdpanel5 @0.5,
    pdpanel4 @0.3, pdpanel3 @0.15; right wing: pdpanel6 @0.4, pdpanel1 @0.3,
    pdpanel2 @0.15; nose pdpanel7 / tail pdpanel8 @0.15.

The `pdpanelN` anim (`ANIMATION_DEFINITION`, ON_CALL, one shared def per panel in
player-1.json, re-rooted per plane via the injure entry's rootName) sets
`OBJECT_ACTIVE_STATE pdpN ACTIVE` **without** deactivating the healthy `pdpN_h` twin,
and calls effect anims at the panel: `gimmeflakes` debris, `yellow_sparks_follow`,
`small_fireball_follow`, `short_firetrail` / `loop_short_firetrail` — the discrete-puff
fire trail streaming from every damaged panel (clearly visible in
`OriginalScreenshots/Videos/C1 IA1 Crash.mp4`). In fact **no zrdr data ever deactivates an
`_h` node** — yet `player_destruct_reset.json` (`plane_reset`) re-ACTIVEs
`pdp2_h`/`pdp3_h` alongside setting every `pdpN`/`pcdpN` INACTIVE, so the original
engine must hide the healthy skins at damage time by an engine-side rule. Beware: the
`pdpN`↔`pdpN_h` numbering is crossed on three plane models — pair torn↔healthy by mesh
position, not by name (measurements in `gamez.md`, "Player-plane damage states").

### The hp pair: armor + hit points (hypothesis)

**Claim.** The two numbers on a `destroyable_parts` entry are that zone's **armor pool** and its
**hit-point pool**, armor spent first.

Supporting evidence, in descending strength:

1. **The HUD showed two pools.** `messages.json` `MSG_HUD_HEALTH` = `Armor: %1%% Health: %2%%` —
   the shipped in-flight readout has an armor bar *and* a health bar. Data-confirmed.
2. **Weapons carry both damage figures, and they differ.** Every one of the 46
   `weapons.json` `BALLISTICS` entries with damage carries `ARMOR_DAMAGE` **and**
   `HEALTH_DAMAGE`, and 18 of them differ — the ammo matrix is built out of the split:
   `wep_N1` (dum-dum) is armor-light/health-heavy (`wep_31` 1.5 / 4.5), `wep_N2` (AP) is the
   mirror (`wep_32` 4.5 / 1.5), `wep_N3` (magnesium) is between. A two-pool target is the only
   thing that makes those numbers mean different things. Data-confirmed.
3. **Crash damage is two-pool too.** `player.json`'s `crash` block is
   `armor_damage_range [50,300]` + `health_damage_range [50,300]` + `bounce_factor`.
   Data-confirmed.
4. **The design says so, per zone.** The original design gives an aircraft four damage zones —
   Nose, Tail, Left Wing, Right Wing, exactly the `destroyable_parts` names — each with its own
   Armor and Hit Points, damage applied to armor until it is gone; and its airframe table lists
   a per-zone "Standard Armor (N/T/W)" stat. Design-informed.

**Why it stays a hypothesis.** All 88 entries in this install have the two values *equal*, so no
measurement over the shipped data can separate (armor, hp) from (hp, hp) or (max, current). The
AI defs' separate `armor`/`health` pair is equal too on every aircraft, which is consistent but
equally non-discriminating.

**Falsification test (needs the original, at the controls).** Against one aircraft zone, count
rounds-to-destroy for a dum-dum gun versus an AP gun of the *same* caliber (`wep_31` vs
`wep_32`, or `wep_51` vs `wep_52`). Two pools depleting at different published rates must give
different counts; if the two guns kill the zone in the same number of hits, there is one pool
and the hypothesis is dead.

## Def-level injure_anims

```
"injure_anims", [[0.10, "player_smoketrail"], [0.85, "player_fuelleak"]]
```

Whole-plane effects. The fractions are read as **any part's** HP fraction (an
interpretation: a total-HP reading could never reach 0.10 before a critical part died at
75 % total). `player_smoketrail` starts the `dense_firetrail` pair (pufftrails.json)
following `prop1`: a black-smoke trail (COLORS ramp: born orange 255,164,90 → near-black
5,5,5) plus a fire trail, both `DISTANCE_INTERVAL` emitters (one puff per N meters of the
node's motion — 1.0 m smoke / 0.25 m fire). `player_fuelleak` runs a `fuel_trail` at a
random pdp panel (unwired).

## collision — 6 probe points

Each player def carries a `collision` list of six xyz points in the plane's local frame
(nose −Z, right +X, meters) — the original's own collision representation, apparently
probe points: nose, right wing, left wing, tail, top, belly. pbloodhawk:

```
(0, 0, −5.68)  nose        (0, −0.20, 4.55)  tail
(+5.71, −0.82, +1.87) right wing   (0, +0.80, −1.46) top (canopy)
(−5.71, −0.82, −1.87) left wing    (0, −1.80, +3.28) belly
```

Note the left/right pair is point-symmetric (both z signs flipped), not mirrored —
probably hand-authored. The remake does **not** use these (its swept boxes are derived
from the actual mesh, item 10a); documented for completeness.

## Effect emitters

The effect emitters these anims call (`short_firetrail`, `dense_firetrail`,
`large_fireball`, …) are `PUFFER_STATE` definitions — full schema in
[effects.md](effects.md).

## Weapons, damage & AI keys

The airframe half of the combat data — none of it consumed by the remake yet. Player defs
carry `weapons` (as a catalogue), `cannon_jam`, `turrets` and `bullethole_anims`; the
`armor`/`health` pair and the AI-tuning keys live only on the AI variant defs.

**`weapons`** — a list of 5-tuples `[weapon_id, count, ?, ?, range]`, ids into
[weapons.md](weapons.md). On `player_airplane` it is a **capability catalogue, not a
loadout**: all 39 buyable ids at once (`wep_00`–`15`, `25`/`27`/`28`, and the full
`wep_30`–`73` player matrix), each with position-5 range `10000` — a UI sentinel, since the
real per-plane loadout is executable-resident. On an AI def it is the actual armament:
`bloodhawk` = `[["wep_04",4,…,800],["wep_07",2,…,800],["wep_00",9000,…,900]]` — a carried
count, two undecoded factors, then an **engagement range** in metres (800–900 for AI
fighters, 500 for the boat/truck). Positions 3–4 are inferred, not confirmed.

**`cannon_jam`** (`player_airplane`) — `heat_safe_limit 1000`, `heat_dissipation_rate 50`,
`jam_chance 0.1`; pairs with `FIRING_HEAT` in [weapons.md](weapons.md) to model gun
overheating. Backlogged, deliberately not implemented.

**`armor` / `health`** — the AI two-pool damage model (fighters `64/64`…`100/100`, always
equal; `patrolboat`/`t_truck` `0/40`, unarmoured soft targets). Distinct from the player
planes' per-part `destroyable_parts` — **player defs have no `armor`/`health` pair**, and
`PlaneStats` does not read these. Where the pool applies, armour is spent before health.

**`turrets`** — on exactly the five turret airframes (`pavenger`, `pbalmoral`, `pbrigand`,
`pfirebrand`, `pkestrel`). A viewpoint-keyed list (`firstp`/`thirdp`) of
`[title <MSG_TUR_*>, node <turretNode>]` entries; the Balmoral is the only two-turret plane
(`balmoral_turret0`–`3`). Turrets are AI gunners that track other aircraft — **M4 scope**. A
turret entry carries a title and a node and **nothing else: no rotation limits anywhere in the
data.** Turret arcs remain undecoded.

**`gun_pitch` / `gun_yaw` are the AI's forward-gun aiming cone, not a turret arc.** Both keys
appear exactly 12 times, always together, always `[-11, 11]` (degrees), and always on an AI
airframe def — a census settles which:

- **7 of the 12 carriers have no turret at all** (`bswingman`, `bloodhawk`, `fury`, `autogyro`,
  `devastator`, `peacemaker`, `warhawk`), so presence cannot be tracking turrets.
- **60 of the 63 non-player defs resolve the cone** through `kind_of`; the 3 that do not are
  `basic_airplane` (the abstract root) and the two surface vehicles `patrolboat` / `t_truck`.
  So: every AI *aircraft*, turret or not.
- **0 of the 12 player defs carry or inherit it — including all five turret airframes**
  (`pavenger`, `pbalmoral`, `pbrigand`, `pfirebrand`, `pkestrel`). A turret arc would have to be
  on the plane that mounts the turret; this is on the plane that has an AI pilot.

±11° is the AI's fixed-gun firing tolerance. The design's gunnery model backs the reading — an
NPC's Dead Eye statistic sets the radius of a lead sphere it will shoot into.

**`bullethole_anims`** — per player plane, the ON_CALL cockpit-glass hit-decal anims
`bullet1`…`bullet5` (see [anim-definitions.md](anim-definitions.md)).

**AI-combatant tuning** (AI variant defs, M4): pilot skill/personality (`dare_devil`,
`dead_eye`, `quick_draw`, `steady_hand`, `sixth_sense`, `natural_touch`, `stun_recovery`,
`talker`, `constitution`, `accentID`); flight/behaviour (`mode`/`mode_alt`, `target_bias`,
`struct_bias`, `pursuit_range`, `attack`/`attack_dwell`/`not_pursuit_dwell`, `rates`,
`turns`, `*_damping`, `mass`, `friction`, `chas_*`, `ai_input_*` / `ai_emerg_input_*` limits
and scales, `preferred_engagement_altitude`/`return_range`, `activation` = spawn/aggro
range). The boat and truck add surface-vehicle motion keys (`platform`, `collision_d`,
`a_damping`). Paint keys (`paint_pattern`, `paint_colorN`, `paint_decalN`) set the AI
liveries — see [paint.md](paint.md). A few airframe oddballs round out the set: `fuel`,
`is_autogyro`, `rudder_tol`, `pilot`, `flight_ceiling`, `title`.
