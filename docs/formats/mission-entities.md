# Mission entities — `zeppelins.json` and `egen.json`

Part of the [format documentation](README.md). `zeppelins.json`, in a mission's own zrdr archive
(`<chapter>/<mission>/zrdr.zbd`), configures the zeppelins the player attacks or escorts.

The whole-install census contains 58 zeppelin instances across 50 `zeppelins.json` files. A separate
[enemy-generator reference](mission-entities/enemy-generators.md) covers `egen.json` and the 23 generators in 53 files.

`zeppelins.json` uses the standard flat alternating `KEY, [values…]` shape
([shared conventions](README.md#shared-conventions-zrdr-readers)); all numbers arrive as floats.

## `zeppelins.json` — one entry per zeppelin instance

The root is a list of instances, each an alternating property list. 15 keys are on all 58
instances; the rest are conditional.

| Key | Shape | Meaning |
|---|---|---|
| `node` | name | the world node this instance drives (`piratezep`, `multiplayer1zep`, `blackswanzep`, …) |
| `position` / `yaw` / `pitch` | xyz / ° / ° | where it starts |
| `max_speed` | m/s | 5–30 across the install |
| `max_accel`, `accel_pitch`, `accel_yaw` | | acceleration limits |
| `max_rate_yaw`, `max_rate_pitch` | °/s | turn-rate limits |
| `min_pitch` / `max_pitch` | ° | ±30 throughout |
| `net` | name | the AI "net" (roster/behaviour group) it belongs to |
| `targets` | node names | who it shoots at — `player`, or another zeppelin (`piratezep`, `dantezep`, …). ⚠ The 8 IA1 files author `targets, null` — key present, no list — so the "47" key census is 39 name lists + 8 nulls |
| `healthy` | `[[zoneNode, "panels"], …]` | the **critical** zones; second field is `"panels"` on all 316 entries |
| `num_healthy_required` | 2–5 | how many of those must **survive**; drop below and the zeppelin dies. Confirmed against the engine — see [below](#the-kill-threshold-counts-survivors). Defaults to **1** when a `healthy` list is present, and is clamped at load to the length of that list |
| `engines` | node names | the engine nacelles (12 or 14: `leng11`…`reng42`) |
| `gasbags` | `[[name, hp, [animName]], …]` | per-gasbag hit points (80–400) and its destruction anim |
| `cannon_fire_delay` / `cannon_fire_range` | s / m | broadside cadence (10/15/20 s) and reach (500–15000 m) |
| `left_cannons` / `right_cannons` | `[[node, deployAnim, retractAnim], …]` | the broadside guns and the animations that run them out and back in |
| `cannon_health` | see below | per-cannon damage record (24 of 58 instances) |
| `cannon_inaccuracy` | ° | on 3 instances: 10.0 on C2B/M04's pair, 6.0 on C4/M05's `blackhatzep`. An absent key scatters nothing — the remake reads it as 0 |
| `team` | `enemy` / `ally` / `neutral` | 16 instances. The parser accepts all three names (case-insensitively) **and** a bare integer team id; this install only authors the names, and only two of the three. The names mint ids in the one shared team space — `neutral` 0, `ally` 1, `enemy` 2 — and a bare integer is stored raw; the engine then fans that one value across the whole airship ([../org/targeting.md](../org/targeting.md), "Zeppelins carry a record override") |
| `deactivated` | `[0]` / `[1]` | the KEY is on 9 instances but the VALUE decides: 7 author `1` (starts switched off, waiting on the script's `WAKEUP_ENEMIES`, and hidden until then — C3/M01's `cargozep1` is revealed by the same objective's `WAKE_ANIM fadein_cg1zep`, an opacity 0 → 1 fade), and C1/M04 + C2/M03 author `0` (active). Asserted in `CSVM.Tests/ZeppelinsTests.cs` |

**`cannon_health` entry** —
`[cannonNode, "gunback", "frame", gasbagName, hp, [destroyAnim], [[frac, stageAnim], …]]`.
Fields 1 and 2 are `"gunback"` and `"frame"` on all 144 entries (sub-nodes of the cannon model);
field 3 names the **gasbag the cannon is attached to**; `hp` is 200 throughout; then the
destruction anim and a descending-fraction damage-stage list (0.6 → `60_*`, 0.3 → `30_*`) with
the same shape as `injure_anims` in [vehicle.md](vehicle.md).

### The kill threshold counts survivors

`healthy` + `num_healthy_required` is the design's critical-zone threshold model, with the gasbags
as the zones. It is the HULL's death and nothing more: on an Instant Action zeppelin run it is the
second of two ways to win, behind destroying every engine ([instant-action.md](instant-action.md),
"The zeppelin run is won on the ENGINES"). **The polarity is settled**: the engine walks the
`healthy` node list, counts the entries still flagged active, and kills the zeppelin when

```
survivors < num_healthy_required
```

⚠ **The design document expresses the same rule as a destroy-count, which is the inverse.** Reading
it that way gives a zeppelin that will not die — a failure mode that looks like a damage bug rather
than an off-by-one, so assert the direction in a test. The design's worked example (four critical
gasbags, threshold 3) is a *destroy* count; this install ships 5–6 gasbags with a *survivor*
threshold of 2–5.

**The data corroborates the polarity through the hull-death anim defs.** Each zeppelin ships an
`ANIMATION_DEFINITION` on its own node gated by an `ACTIVATION_PREREQUISITE` counting the gasbag
`finish_*` anims with a `MINIMUM_TO_SATISFY` — and that minimum is exactly
`len(healthy) − num_healthy_required + 1`, the destroyed count at which survivors first drop
below the threshold (piratezep: 3 of 6 finishes against required 4 of 6; multiplayer1zep: 3 of 5
against required 3 of 5). The def pops the remaining gasbags and calls the hull's own
`kill*zep` sink/breakup anim. The remake's kill (F18) is owned by the survivor count and then
plays this prerequisite-gated def, selected by its prerequisite shape, never by name.

**Where a per-part hp comes from, measured across the install**: the record and the mission's
compiled anim defs are complementary. Gasbag defs carry `HEALTH 0` everywhere, so a gasbag's
pool is always the record's `gasbags` hp. Cannon defs carry `HEALTH 60` exactly where the record
authors no `cannon_health` (the campaign zeppelins), and `HEALTH 0` where it does (the IA1/MP3
family, hp 200 in the record). Engines/turrets are def-only (`HEALTH 30–40`/`10`, no record
key). The remake seeds record-first, def where unauthored. ⚠ One shipped gap: C5/M01's
`piratezep` authors `healthy` (with `gasbag5` listed twice — count entries literally) but no
`gasbags`, and its gasbag defs are `HEALTH 0` like all others, so no hp is authored anywhere;
what the original does there is undecoded, and the remake leaves those zones undamageable and
says so in a `zep:` line rather than inventing a default.

### Units and the load-time pitch clamp

`yaw`, `pitch`, `accel_pitch`, `accel_yaw`, `max_rate_yaw`, `max_rate_pitch` and
`cannon_inaccuracy` are authored in degrees and converted to radians as they are read.
**`min_pitch` and `max_pitch` are not converted** — they stay in degrees.

⚠ **Consequently the original's own initial-pitch clamp never fires.** Immediately after loading,
the engine clamps the (already radian) `pitch` against the (still degree) `min_pitch`/`max_pitch`;
with the ±30 every instance ships, the comparison is `|0.52 rad| < 30`, so the clamp is a no-op.
This is a unit bug in the original, harmless because no instance authors an out-of-range `pitch`.
Do not "fix" it into a clamp that actually bites, and do not read the ±30 as radians.

### Route ends and stop points

The `net` a zeppelin flies is a patrol graph ([ai-nets.md](ai-nets.md)) walked by the shared
`AiNetFollower`; a zeppelin observes its nodes' STOP-POINT flags, which an aircraft on the same
net type does not. Two routines decode the halted state (`FUN_004bf9d0`'s own-node gate, called
every step): `FUN_004bf500` levels the airship at an armed stop (commanded pitch 0, heading kept,
speed 0) and `FUN_004bf360` ramps the throttle down from 250 m out to a full stop inside 30 m.
**A structural dead end — the current node's only edge is the one just flown — holds the same way,
unconditionally, ahead of and regardless of any authored stop-point id.** Some nets author their
far node as an armed stop under an unaddressable id (id 0, which the script side rejects before it
ever reaches a node lookup) so the existing stop-point mechanism already parks the airship there
for good; a net that does not author that pattern at its far node (C1B/M03's `Klondike1`, ridden by
`piratezep`) still holds there, because the dead-end rule is unconditional and does not depend on
the file authoring anything at that node. Without it a follower reaching an unarmed dead end
re-picks its only neighbour — the node it just left — and re-flies the route, which for a net whose
nodes carry real altitude changes reads as the airship porpoising along its route and never
levelling off at the end (`BL-529`).

### Broadside firing

Behaviour rather than format, but it is what the cannon keys drive, and it is decoded from the
binary rather than inferred:

- **The ammunition is hardcoded `wep_28`** (the cannonball, [weapons.md](weapons.md)) — looked up by
  name in the fire routine. No zeppelin key names a weapon.
- **The arc is a 90° cone centred on the firing side's perpendicular.** The engine builds a ±1 unit
  vector along the hull's lateral axis by the cannon's side flag, rotates it into world space, and
  requires `dot(toTarget, sideNormal) > 0.707` — a 45° half-angle.
- **A cannon fires only from its ready state.** Cannons run a small state machine; a cannon that is
  stowed triggers its deploy animation instead of firing, and cannons mid-deploy or mid-retract are
  skipped entirely. This is the design's hatch-open-then-fire sequence.
- **Re-fire is per cannon**, not per zeppelin: each sets its own next-fire time to
  `now + cannon_fire_delay`.
- **Against another zeppelin, the target is a randomly chosen gasbag** — the engine collects that
  zeppelin's gasbags with health ≥ 0 that fall inside the 0.707 arc and picks one with `rand()`.
  Against anything else it aims at the target directly.
- ⚠ **Hit resolution is ballistic, not probabilistic.** The engine runs a lead/intercept solve
  against the target from the projectile's speed and spawns a real round along the solved
  direction, scattered by `cannon_inaccuracy`; a target with no intercept solution is skipped. The
  design document instead describes a rolled hit chance ramping from 20 % at maximum range to
  100 % near 200 m. **Nothing like that roll is in the shipped fire path** — treat the design's
  curve as design-era and do not implement it.
- ⚠ **The `targets` list carries no team or hostility test, at any stage.** A record's `targets`
  names are parsed as unresolved pairs at load (`FUN_004bd8d0`), resolved once after every mission
  zeppelin is placed by matching each name against the live zeppelin roster (`FUN_004bede0` calling
  `FUN_004bd430`; the first name match wins, and a name matching no zeppelin (`player` is the only
  one shipped data uses) falls through to a general named-object position lookup instead), and
  consumed by the fire routine (`FUN_004bfe00`) on arc and intercept alone. No team, side, or ally
  field is read anywhere in that chain. A record authoring `targets [player]` fires on the player
  whenever in `cannon_fire_range` and arc, whether or not the record carries a `team` key at all;
  the remake's `ZeppelinRuntime.Cannons.ResolveTarget` matches this: the first live authored name
  wins, with no hostility filter to add.

What the remake's implementation (M4 F19, `Flight/ZeppelinBroadside.cs` +
`Session/ZeppelinRuntime.Cannons.cs`) added to the picture:

- **The deploy anims author their own timing.** Every `deployAnim`/`retractAnim` names a
  compiled per-mission `mis_anim` def (`lbroad11-deploy_pzep_lbroad11`) whose longest
  `OBJECT_MOTION_FROM_TO` `run_time` is 4 s (door swing) over a 3 s gun extension — the remake
  reads the deploy duration from the def rather than inventing one. The decode names no STOW
  trigger; the remake retracts after an invented, named 10 s without a bearing target.
- **Side alternation is geometric.** The two 45°-half-angle cones sit on opposite normals, so
  at most one side ever bears; the volley changes sides only when the target crosses the hull
  axis. No alternation schedule exists to decode.
- **Zeppelin-vs-zeppelin is live data.** Both records of a mutually-targeting pair are active
  in C1B/M03 (`vostokzep` ↔ `piratezep`, range 500) and in every chapter's MP3
  (`multiplayer1zep` ↔ `multiplayer2zep`, range 15000 — in range from spawn), so the
  gasbag-pick arm is exercisable in a shipped session, not only in tests.

### Engine loss

The engine count at load is the denominator; live engines are those whose node is still flagged
active. While any are missing, speed and acceleration are scaled by a **square root**:

```
f          = sqrt(alive / total)
max_speed' = f * max_speed
max_accel' = (0.8 * f + 0.2) * max_accel
```

So acceleration retains a 20 % floor while speed goes to zero at total engine loss. ⚠ The design
document describes a three-band model instead (the first 30 % of engines costing 10 % of
performance, the next 40 % band a further 40 %, the last 30 % the remaining 50 %). The *qualitative*
claim survives — a concave curve, so each further engine lost hurts more than the last — but the
arithmetic is the square root above, not the bands.

**The mechanism is a shrinking list, and one other system reads it.** `FUN_004bf150` holds the
engines as a vector at `+0x4c`…`+0x50` and, every frame the zeppelin is alive and active, **erases**
each entry whose node has lost its active bit; `alive` is then the vector's size and `total` the
load-time count kept at `+0x98`. Because the vector is compacted rather than flagged, "the engine
vector is empty" is a directly testable "every engine is destroyed" — and that is exactly what
Instant Action's `zeppelin_run` wins on, ahead of the hull's own death byte. See
[instant-action.md](instant-action.md), "The zeppelin run is won on the ENGINES". Nothing else in
the zeppelin module reads the vector, so a mission that is not an Instant Action zeppelin run feels
engine loss only as the curve above.

See [Enemy generators](mission-entities/enemy-generators.md) for `egen.json`, its launch cycle, the zeppelin drop and the surface hosts' take-off paths, launch naming, capacity rules, and evidence limits.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
