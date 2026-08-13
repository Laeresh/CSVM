# Mission entities — `zeppelins.json` and `egen.json`

Part of the [format documentation](README.md). Two readers in the **mission's own** zrdr archive
(`<chapter>/<mission>/zrdr.zbd`) that configure the mission's big live entities: the zeppelins
the player attacks or escorts, and the generators that feed fighters into the fight. Decoded
2026-07-25 from a census over the whole install (50 `zeppelins.json` → 58 instances; 53
`egen.json` → 23 generators, the other 33 files being an empty `[null]`).

**The remake reads both**: `egen.json` via `CSVM/src/Mech3/EnemyGenerators.cs` (run by
`Session/AiGeneratorRuntime.cs` behind `--generators`, M4 B6) and `zeppelins.json` via
`CSVM/src/Mech3/Zeppelins.cs` (run by `Session/ZeppelinRuntime.cs` behind `--zeppelins`, M4 F17
— the motion half; damage is F18). Both are documented because they are complete,
self-contained definitions — the data
half of the M4 combat work, and directly useful to the mech3ax fork. Which zeppelin *nodes* a
mission shows at all is a separate mechanism, the per-mission `.gw` interp script — see
[interp.md](interp.md).

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
| `targets` | node names | who it shoots at — `player`, or another zeppelin (`piratezep`, `dantezep`, …). ⚠ The 8 IA1 files author `targets, null` — key present, no list — so the "47" key census is 39 name lists + 8 nulls |
| `healthy` | `[[zoneNode, "panels"], …]` | the **critical** zones; second field is `"panels"` on all 316 entries |
| `num_healthy_required` | 2–5 | how many of those must **survive**; drop below and the zeppelin dies. Confirmed against the engine — see [below](#the-kill-threshold-counts-survivors). Defaults to **1** when a `healthy` list is present, and is clamped at load to the length of that list |
| `engines` | node names | the engine nacelles (12 or 14: `leng11`…`reng42`) |
| `gasbags` | `[[name, hp, [animName]], …]` | per-gasbag hit points (80–400) and its destruction anim |
| `cannon_fire_delay` / `cannon_fire_range` | s / m | broadside cadence (10/15/20 s) and reach (500–15000 m) |
| `left_cannons` / `right_cannons` | `[[node, deployAnim, retractAnim], …]` | the broadside guns and the animations that run them out and back in |
| `cannon_health` | see below | per-cannon damage record (24 of 58 instances) |
| `cannon_inaccuracy` | ° | 10.0, on 3 instances |
| `team` | `enemy` / `ally` / `neutral` | 16 instances. The parser accepts all three names (case-insensitively) **and** a bare integer team id; this install only authors the names, and only two of the three |
| `deactivated` | `[0]` / `[1]` | the KEY is on 9 instances but the VALUE decides: 7 author `1` (starts switched off, waiting on script), and C1/M04 + C2/M03 author `0` (active). Measured 2026-08-13; asserted in `CSVM.Tests/ZeppelinsTests.cs` |

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
| `vehicle` | 23/23 | nested: `params` (a designer label in the `aiv.json` HEADER's `(slotId, label)` pairs, e.g. `Eairg31_params`; authored on 15 of 23, see the typo note below), `nets` (one or more AI net names), `choose_nets` (`cyclic` throughout) |
| `capacity` | 23/23 | `0` throughout — a lifetime spawn budget, decremented per launch. ⚠ **`0` does not obviously mean "unbounded"** — see [the capacity puzzle](#the-capacity-puzzle) |
| `max_active` | 23/23 | concurrent live spawns (1/4/5/6/10) |
| `wave_size` | 23/23 | planes per wave (1, once 3) |
| `wave_period` | 23/23 | seconds between waves (1–20) |
| `ind_period` | 23/23 | seconds between individuals inside a wave (0.5–10) |
| `zeppelin` | 17/23 | `[1]` — marks the zeppelin-hangar variant |
| `open_anim` / `close_anim` | 17/23 | the hangar-door animations, run before and after a wave |
| `origin` | 17/23 | the node the fighters appear at — `cargobay` on 16 of 17 |
| `rotation` | 17/23 | `[-90, 0, 0]` throughout — the drop attitude. Read as **three** angles (all converted to radians), not the single value the data suggests |
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

**One authored `params` label is a shipped typo.** The linkage is by exact label: an egen
`vehicle.params` value names a designer label in the same mission's `aiv.json` header. 14 of the
15 authored labels resolve; C1/M04's egen says `Eairg32_params` while the header spells it
`Earig32_params` (a transposition), so that generator's roster lookup cannot succeed as authored.
Measured 2026-08-13; asserted in `CSVM.Tests/EnemyGeneratorsTests.cs`.

### The generator cycle

Decoded from the binary. One generator holds a timer, a next-event threshold, a per-wave counter
and a door state; each tick advances the timer by the frame delta (and stops entirely while the
game is paused).

```
if host is dead                    -> disable this generator permanently
blocked = (wave_size - spawnedThisWave) + active   > max_active
       or (wave_size - spawnedThisWave)            > capacityRemaining
       or (min_altitude set and host altitude < min_altitude)

if blocked:      if door open and timer >= 4 -> close door        # hold, do not cancel
else:
  door open  and timer >= 4 and timer + 8 < nextEvent -> close door
  door closed and timer >= nextEvent - 4               -> open door
  door open  and timer >= nextEvent                    -> SPAWN
```

On a successful spawn: `capacityRemaining--`, `active++`, timer resets to 0, and the wave counter
advances. If the wave is now complete the counter resets and
`nextEvent = ind_period + wave_period`; otherwise `nextEvent = ind_period`.

Three things that reading pins down:

- **`ind_period` and `wave_period` compose, they do not alternate.** `ind_period` is the gap between
  individuals *within* a wave; the gap *between* waves is `ind_period + wave_period`, not
  `wave_period` alone.
- **The door timings are hardcoded, not data.** The door opens **4 s before** a due spawn, stays
  open at least 4 s, and only closes early if the next spawn is more than 8 s away — so a
  fast-cycling generator simply leaves its hangar open.
- **The altitude gate holds, it does not cancel** — exactly as the design document says. The wave
  counter and the timer are untouched while blocked; only the door closes. The gate is skipped
  entirely when `min_altitude` is unset (a `-1.0` sentinel).

One value the decode does not pin: the FIRST `nextEvent` threshold after load. The remake's
implementation (`Session/GeneratorCycle.cs`) assumes the full inter-wave gap
(`ind_period + wave_period`), the conservative reading, and says so where F20 will revisit it.

**The host's death disables the generator.** For a fixed installation that is the `healthy` node
going inactive; for a zeppelin it is the zeppelin's own destroyed flag. Two further load-time
rejections: a generator whose `node` cannot be resolved is **dropped**, and so is one where **none**
of its `vehicle.nets` names resolve — a generator with no valid net does not load inert, it does not
load at all.

Smaller loader findings: `open_anim`/`close_anim` **default from the node name** when unauthored
(`<node>_open_<nn>` / `close_<nn>`), so the 6 non-zeppelin generators still get a door pair;
`choose_nets` parses only its first letter and accepts **`random`** as well as the `cyclic` every
file authors; and the `vehicle` block additionally accepts **`primary_target`** and **`title`**,
neither authored in this install.

### The capacity puzzle

⚠ **Unresolved, and it matters before anyone implements this.** `capacity` is `0` on all 23
generators, `capacityRemaining` is initialised from it, and the blocking rule above reads

```
(wave_size - spawnedThisWave) > capacityRemaining     ->  blocked
```

With `capacity` 0 and `wave_size` ≥ 1 that is `1 > 0` on the very first tick, which would hold every
generator in this install forever — yet zeppelins visibly launch fighters in the original. So one of
these must be true, and static reading cannot choose between them:

- the counter is topped up at runtime by something not yet traced (the binary carries a
  `zep_rearm_node_%d` string, which is the strongest lead);
- `capacity` is gated by a global the loader consults (`0` there forces `capacity` to `0`), and the
  retail configuration takes the other branch with a different source for the value;
- the guard's operand mapping is misread.

**Do not implement `capacity` as "0 means unlimited" on the strength of this page** — that reading is
a guess that happens to produce working behaviour. Settle it by observing a zeppelin launching in
the original, or by tracing the rearm path, before relying on it.

#### Chased through the binary, 2026-08-10 — all three candidates fail

The three explanations above were each pushed to the end in code. **None survives**, and the
contradiction is now sharper rather than resolved.

- ⚠ **The `zep_rearm_node_%d` lead is dead.** It has nothing to do with generators. The single
  function that formats it enumerates scene nodes `zep_rearm_node_1, 2, …` (and `rearm_node_N` in
  another game mode), stopping at the first index that does not resolve, and files them into a
  global list — **player rearm pads**, the counterpart of the `rearm_rad` / `rearmrad` tuning keys.
  It never touches a generator.
- **A runtime top-up does not exist** (⚠ refuted 2026-08-13, see the correction below; the claim
  was true of the generator module only). In the whole generator module there are exactly three writes
  to `capacityRemaining`: the loader's `= capacity`, the tick's decrement, and a reset routine that
  sets it *back to* `capacity` (along with `active`, the wave counter and the timer). Nothing ever
  raises it above `capacity`, so with `capacity` `0` it is `0` or negative forever. The loader has
  a single caller, so there is no second construction path either.
- **The global cannot rescue it.** Both loader branches yield `0` when the authored value is `0` —
  one assigns `0` directly, the other reads a `0` from the file. And the flag must be set in
  retail, because the same flag gates the turret loaders, which bail out entirely when it is clear
  ([turrets.md](turrets.md)).
- **The operands are not misread.** In the disassembly the test is a single unsigned compare of
  `capacityRemaining` against `wave_size − spawnedThisWave` followed by a jump-if-below. The
  polarity is unambiguous.

Re-measured from the extraction at the same time: `capacity` is **present on all 23 generators and
`0` on all 23**, and `wave_size` is `1` on 22 and `3` on one — so the blocked branch is taken on the
first tick of every generator in the install.

#### Correction, 2026-08-13 (found during the B7 `group` decode): a runtime top-up DOES exist

The "runtime top-up does not exist" bullet above searched the generator module and was wrong by
scope: the Instant Action wave sequencer (`FUN_0045b9d0`, mission-type-2 branch) **adds the next
wave group's member count to a named generator's `capacityRemaining`** and stamps the generator
with that group id. So in Instant Action a zeppelin's generator is fed capacity at runtime, wave
by wave, which is consistent with `capacity 0` in the data and zeppelins visibly launching. What
this does not settle: whether campaign missions have an equivalent feed (the script layer's
generator lookup in `FUN_00465910` reads `capacityRemaining` but was not seen writing it), so the
raw-byte read below is still worth having for the campaign case. Mechanism details:
`analysis/m4-b7-group-slot/FINDINGS.md`.

**Where that leaves it.** Every explanation that lives in the engine has been eliminated, which
points the remaining suspicion at the *value*: what the engine reads for `capacity` may not be the
`0` the extraction reports. The same value slot is read as an integer for `wave_size` and as a float
for `wave_period`, so the reader format distinguishes the two and the extractor is preserving both —
which makes this less likely, not impossible. **The discriminating step is now to read `capacity`'s
raw bytes out of the un-extracted `egen.zbd` rather than the JSON.** Until then the guidance above
stands unchanged: do not implement "0 means unlimited".

#### The remake's stand-in, 2026-08-13 (M4 B6)

The generator runtime had to ship before the raw-byte read, so
`Session/GeneratorCycle.cs` carries an explicit, documented stand-in rather than a silent
reading: **the capacity check applies the decoded rule only when `capacity > 0`, and is disabled
entirely at `capacity <= 0`.** A positive value (none ship, but a fixture authors one) gets the
decoded budget: `capacityRemaining` initialised from `capacity`, decremented per spawn, blocking
when `wave_size − spawnedThisWave > capacityRemaining`. The shipped `0` therefore neither blocks
forever (the decoded rule taken literally) nor claims to mean "unlimited" (the guess this page
forbids); the stand-in is named in the class comment and asserted as such in
`CSVM.Tests/GeneratorCycleTests.cs`. **Open follow-up, unchanged:** the raw-byte read of
`capacity` out of the un-extracted `egen.zbd` is the discriminating instrument, and the stand-in
is to be replaced by whatever it shows.
