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
| `engine_sound` / `cockpit_engine_sound` | sound-def names (SETS in sounds.json) |
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

## destroyable_parts (Run-2 item 10)

A list of part entries:

```
[name, hp, hp, flags…,
 "got_hit_anim", [animName, rootName],
 "injure_anims", [[fraction, animName, rootName], …]]
```

- `name`: `nose` / `tail` / `leftwing` / `rightwing` for every player plane.
- The two `hp` values are identical for player defs (20 for pbloodhawk, 25 for
  pdevastator); AI variants differ (25/20) — semantics of the second value undecoded, the
  remake takes the first as max HP.
- Flags: `critical` — the plane is destroyed when this part reaches 0 HP (all four player
  parts carry it); `engine` (tail only) — engine damage/power loss on that part
  (flight-handling penalties deliberately unmodeled this run).
- `injure_anims`: **descending HP fractions**; when the part's HP fraction crosses one,
  the named anim runs. Two families interleave:
  - `<part>_damage_effects` (0.99) / `_green` (0.72) / `_yellow` (0.46) / `_red` (0.20) —
    the cockpit damage-indicator texture cycle (unwired until a cockpit exists).
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
(`balmoral_turret0`–`3`). Turrets are AI gunners that track other aircraft — **M4 scope**.
`gun_pitch` / `gun_yaw` (AI defs, e.g. `[-11, 11]`) are the aiming cone.

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
`is_autogyro`, `rudder_tol`, `damaged_engine_sound`, `pilot`, `flight_ceiling`, `title`.
