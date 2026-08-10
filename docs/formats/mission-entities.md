# Mission entities — `zeppelins.json` and `egen.json`

Part of the [format documentation](README.md). Two readers in the **mission's own** zrdr archive
(`<chapter>/<mission>/zrdr.zbd`) that configure the mission's big live entities: the zeppelins
the player attacks or escorts, and the generators that feed fighters into the fight. Decoded
2026-07-25 from a census over the whole install (50 `zeppelins.json` → 58 instances; 53
`egen.json` → 23 generators, the other 33 files being an empty `[null]`).

**Neither is consumed by the remake.** They are documented because they are complete,
self-contained definitions — the data half of the M4 combat work, and directly useful to the
mech3ax fork. Which zeppelin *nodes* a mission shows at all is a separate mechanism, the
per-mission `.gw` interp script — see [interp.md](interp.md).

Both files use the standard flat alternating `KEY, [values…]` shape
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
| `targets` | node names | who it shoots at — `player`, or another zeppelin (`piratezep`, `dantezep`, …) |
| `healthy` | `[[zoneNode, "panels"], …]` | the **critical** zones; second field is `"panels"` on all 316 entries |
| `num_healthy_required` | 2–5 | how many of those must **survive**; drop below and the zeppelin dies. Confirmed against the engine — see [below](#the-kill-threshold-counts-survivors). Defaults to **1** when a `healthy` list is present, and is clamped at load to the length of that list |
| `engines` | node names | the engine nacelles (12 or 14: `leng11`…`reng42`) |
| `gasbags` | `[[name, hp, [animName]], …]` | per-gasbag hit points (80–400) and its destruction anim |
| `cannon_fire_delay` / `cannon_fire_range` | s / m | broadside cadence (10/15/20 s) and reach (500–15000 m) |
| `left_cannons` / `right_cannons` | `[[node, deployAnim, retractAnim], …]` | the broadside guns and the animations that run them out and back in |
| `cannon_health` | see below | per-cannon damage record (24 of 58 instances) |
| `cannon_inaccuracy` | ° | 10.0, on 3 instances |
| `team` | `enemy` / `ally` / `neutral` | 16 instances. The parser accepts all three names (case-insensitively) **and** a bare integer team id; this install only authors the names, and only two of the three |
| `deactivated` | `[1]` | 9 instances — starts switched off |

**`cannon_health` entry** —
`[cannonNode, "gunback", "frame", gasbagName, hp, [destroyAnim], [[frac, stageAnim], …]]`.
Fields 1 and 2 are `"gunback"` and `"frame"` on all 144 entries (sub-nodes of the cannon model);
field 3 names the **gasbag the cannon is attached to**; `hp` is 200 throughout; then the
destruction anim and a descending-fraction damage-stage list (0.6 → `60_*`, 0.3 → `30_*`) with
the same shape as `injure_anims` in [vehicle.md](vehicle.md).

### The kill threshold counts survivors

`healthy` + `num_healthy_required` is the design's critical-zone threshold model, with the gasbags
as the zones. **The polarity is settled**: the engine walks the `healthy` node list, counts the
entries still flagged active, and kills the zeppelin when

```
survivors < num_healthy_required
```

⚠ **The design document expresses the same rule as a destroy-count, which is the inverse.** Reading
it that way gives a zeppelin that will not die — a failure mode that looks like a damage bug rather
than an off-by-one, so assert the direction in a test. The design's worked example (four critical
gasbags, threshold 3) is a *destroy* count; this install ships 5–6 gasbags with a *survivor*
threshold of 2–5.

### Units and the load-time pitch clamp

`yaw`, `pitch`, `accel_pitch`, `accel_yaw`, `max_rate_yaw`, `max_rate_pitch` and
`cannon_inaccuracy` are authored in degrees and converted to radians as they are read.
**`min_pitch` and `max_pitch` are not converted** — they stay in degrees.

⚠ **Consequently the original's own initial-pitch clamp never fires.** Immediately after loading,
the engine clamps the (already radian) `pitch` against the (still degree) `min_pitch`/`max_pitch`;
with the ±30 every instance ships, the comparison is `|0.52 rad| < 30`, so the clamp is a no-op.
This is a unit bug in the original, harmless because no instance authors an out-of-range `pitch`.
Do not "fix" it into a clamp that actually bites, and do not read the ±30 as radians.

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

## `egen.json` — enemy generators

An **enemy generator** spawns AI aircraft into a live mission from a host entity. The host is
either a zeppelin (fighters dropped out of its hangar) or a fixed installation — this install
has ground airfields `eairg31`/`eairg32`, a ship `eshipg31`, and a submarine `barracuda`.

| Key | On | Meaning |
|---|---|---|
| `node` | 23/23 | the host world node |
| `vehicle` | 23/23 | nested: `params` (an `aiv.json` roster entry, e.g. `Eairg31_params`), `nets` (one or more AI net names), `choose_nets` (`cyclic` throughout) |
| `capacity` | 23/23 | `0` throughout — unbounded |
| `max_active` | 23/23 | concurrent live spawns (1/4/5/6/10) |
| `wave_size` | 23/23 | planes per wave (1, once 3) |
| `wave_period` | 23/23 | seconds between waves (1–20) |
| `ind_period` | 23/23 | seconds between individuals inside a wave (0.5–10) |
| `zeppelin` | 17/23 | `[1]` — marks the zeppelin-hangar variant |
| `open_anim` / `close_anim` | 17/23 | the hangar-door animations, run before and after a wave |
| `origin` | 17/23 | the node the fighters appear at — `cargobay` on 16 of 17 |
| `rotation` | 17/23 | `[-90, 0, 0]` throughout — the drop attitude |
| `min_altitude` | 17/23 | **100–300 m: the launch gate** |
| `healthy` | 1/23 | the node whose destruction stops the generator (`subhealthy`, the submarine) |
| `moving_path` | 1/23 | bare flag |

**`min_altitude` is confirmed by the design document by name.** The design specifies that a
zeppelin drops fighters through its hangar door and so must be high enough to do it; that the
generator gets an extra altitude parameter for this; that generation is *held* until the
altitude is reached; and it names the file — `egen.zrd`. The `open_anim` → spawn →
`close_anim` sequence is spelled out there too. This is the one place in this page where the
design document and the shipped data agree field-for-field.

33 of the 53 `egen.json` files are an empty `[null]` — most multiplayer maps have no generator.
