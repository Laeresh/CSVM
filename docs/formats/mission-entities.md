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
| `num_healthy_required` | 2–5 | how many of those must survive; drop below and the zeppelin dies |
| `engines` | node names | the engine nacelles (12 or 14: `leng11`…`reng42`) |
| `gasbags` | `[[name, hp, [animName]], …]` | per-gasbag hit points (80–400) and its destruction anim |
| `cannon_fire_delay` / `cannon_fire_range` | s / m | broadside cadence (10/15/20 s) and reach (500–15000 m) |
| `left_cannons` / `right_cannons` | `[[node, deployAnim, retractAnim], …]` | the broadside guns and the animations that run them out and back in |
| `cannon_health` | see below | per-cannon damage record (24 of 58 instances) |
| `cannon_inaccuracy` | ° | 10.0, on 3 instances |
| `team` | `ally` / `enemy` | 16 instances |
| `deactivated` | `[1]` | 9 instances — starts switched off |

**`cannon_health` entry** —
`[cannonNode, "gunback", "frame", gasbagName, hp, [destroyAnim], [[frac, stageAnim], …]]`.
Fields 1 and 2 are `"gunback"` and `"frame"` on all 144 entries (sub-nodes of the cannon model);
field 3 names the **gasbag the cannon is attached to**; `hp` is 200 throughout; then the
destruction anim and a descending-fraction damage-stage list (0.6 → `60_*`, 0.3 → `30_*`) with
the same shape as `injure_anims` in [vehicle.md](vehicle.md).

`healthy` + `num_healthy_required` is the design's critical-zone threshold model — an object
dies when a stated number of its critical zones is destroyed — with the gasbags as the zones.
The design's worked example gives a zeppelin four critical gasbags and a threshold of 3;
this install ships 5–6 gasbags with `num_healthy_required` 3–5. *(Shape data-confirmed;
the threshold reading is design-informed.)*

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
