# The hangar screens, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`), 2026-08-21, in two passes: the BL-354/BL-067 `/backlog` decode of the named
callbacks, then a fresh-context prospecting pass over the four customisation screens' scripts
and every callback id they invoke. Every claim below names the function or address it came
from; no decompiler output is reproduced.

**Where the other halves live.** The 204-byte saved-plane record and its field layout are
[`formats/paint.md`](../formats/paint.md) "Saved custom planes" (the file is a verbatim dump of
the in-memory record this page's screens edit). The paint system itself (region masks, colour
formula, decals) is the rest of that page. The UI scripts that lay the screens out
(`GUNS.SCRIPT`, `HARDPOINTS.SCRIPT`, `ARMOR.SCRIPT`, `PURCHASE.SCRIPT`, `AIRFRAME.SCRIPT`) are
pure layout in `extracted/rof/ASSETS/SCRIPTS/`; everything they display comes from the engine
through the callbacks below.

⚠ **This page is a decode, not a proposal.** Where it disagrees with an observation at the
controls or a screenshot, the decode wins and the disagreement is a note.

## The callback dispatchers

Screen scripts call `callback(widget, id, ...)`. Two engine routines answer:

- **The widget dispatcher at `0x004093a0`** (no function defined in the Ghidra database; a raw
  routine with a 0x1820-byte frame). It reads an argument block (`[+0]` id, `[+4]` widget arg,
  `[+8]`/`[+0xc]` value in/out). Ids 2000–2038 jump through the table at `0x40f518`; id 2099
  has a dedicated branch; ids 2100–2413 index the byte table at `0x40f788` (by `id − 2100`)
  whose entry indexes the dword jump table at `0x40f5b4`. To resolve any id in that range by
  hand: byte at `0x40f788 + (id − 2100)`, then dword at `0x40f5b4 + entry*4`.
- **The screen-flow dispatcher `FUN_00407670`**, a plain switch over ids 1–31 and 1001–1045.
  Case 22 is the save-custom-plane commit; case 0x400 (1024) is the saved-plane count, calling
  the `Planes\*.*` directory scan `FUN_00415000` over the 24-slot name index at `0x648534`
  (33-byte records, bound `0x64884c`).

**List/dropdown protocol.** A list control invokes its fill callback with the widget arg
pointing at an index; index −1 (`EDX = −1` set at `0x004093b6`) means "write the row count
back". Fixed counts: gun dropdown 11 (`0x0040be24`), hardpoint dropdown 5 (`0x0040b826`),
armour dropdown 13. The PURCHASE list's row counts are dynamic (equipped-gun count from
`FUN_0040fdf0`, non-zero armour zones, non-empty wings).

## Callback map

All four screens, id → handler (widget dispatcher unless noted):

| Screen | Id | Handler | Role |
|---|---|---|---|
| AIRFRAME | 2200 | `0x0040c74c` | airframe dropdown labels and count |
| AIRFRAME | 2216 | `0x0040d28d` | set airframe: dropdown index → airframe id via `FUN_00410120`, stored to record +0x2c (`0x0064cba4`) |
| AIRFRAME | 2217 | `0x0040d272` | get airframe |
| AIRFRAME | 2223 | `0x0040b89b` | airframe description text |
| GUNS | 2248 | `0x0040be1b` | gun dropdown labels (11 rows; names `langui` 3310+type, twin rows prefixed with format 506, `"(%1!d!) "`) |
| GUNS | 2249 | `0x0040bea2` | get/set a slot's gun (record +0x84/+0x88) |
| GUNS | 2250 | `0x0040bf72` | slot title: reads the airframe stat table's +0x1c+slot*4 string id, tail at `0x0040ef84` |
| GUNS | 2220 | `0x0040bbe6` | titles |
| GUNS | 2226 | `0x0040b993` | gun description text |
| HARDPOINTS | 2244 | `0x0040b81d` | dropdown labels (5 rows: `langui` 1165 "None", 1168 "1 Hardpoint", 1169 "%d Hardpoints") |
| HARDPOINTS | 2245 | `0x0040ad0f` | get/set a wing's hardpoint count (record +0x34/+0x38) |
| HARDPOINTS | 2227 | `0x0040bac6` | description text |
| ARMOR | 2246 | `0x0040b7bd` | dropdown labels (13 rows: row 0 is string 1165 "None", rows 1-12 show `row×5` units via format 1170 — the display scale is the record's own ×5, not pounds) |
| ARMOR | 2247 | `0x0040ac3d` | get/set the four zone values (record +0x74..+0x80, stored premultiplied by 5) |
| PAINT | 2229 | `0x0040d30e` | colour dropdown fill: 27 swatch chips from the table at `0x0061dd48` |
| PAINT | 2230 | `0x0040d473` | the current effective colour chip per slot |
| PAINT | 2232-2234 | `0x0040d388` | shade dropdown fill per slot (the colour's own ramp) |
| PAINT | 2235 | `0x0040d2b8` | pattern dropdown fill: availability-masked rows, labels langui 3425+pattern |
| PAINT | 2236 | `0x0040d437` | shade get/set (record +0x50 array) |
| PAINT | 2237 | `0x0040d3e2` | colour get/set (record +0x44 array; resets the slot's shade to the colour's default) |
| PAINT | 2238 | `0x0040d4ba` | pattern get/set: SET loads the pattern entry's six colour/shade defaults |
| PAINT | 2239 | `0x0040d5b7` | decal get/set (record +0x5c array; grid `row*5+col` = the flat decal index) |
| PAINT | 2240 | `0x0040d61c` | decal grid row count (`ceil(N/5)`, N = 50 shipped names at `0x0061da20`) |
| PAINT | | | the swatch/pattern-default tables: [`formats/paint.md`](../formats/paint.md) "The swatch table and the pattern defaults" |
| PURCHASE | 2251 | `0x0040af39` | airframe row (name `langui` 3000+af, cost/weight from the stat table) |
| PURCHASE | 2252 | `0x0040afd8` | engine row (name `langui` 3100+af*6+engine, or 1171 "No Engine Selected") |
| PURCHASE | 2253/2254/2255 | `0x0040b0e4` | armour rows, name/weight/cost (zone names `langui` 1191–1194) |
| PURCHASE | 2256/2257/2258 | `0x0040b1f3` | gun rows (helpers: name `FUN_0040fe40`, weight `0x004055c0`, cost `0x00405700`) |
| PURCHASE | 2259/2260/2261 | `0x0040b2b4` | hardpoint rows (names `langui` 1176/1177 "Left/Right Wing: %d") |
| PURCHASE | 2262 | `0x0040b368` | totals (record +0x28 cost, +0x3c weight) |
| PURCHASE | 2263 | `0x0040b56c` | commit the purchase |
| PURCHASE | 2264 | `0x0040b418` | the problems gate: funds, weight capacity, engine present |
| shared | 2210/2211/2213/2214/2215/2221/2265 | `0x0040d260`/`0x0040a26b`/`0x0040add0`/`0x0040ae33`/`0x0040ad9b`/`0x0040b6b3`/`0x0040b3d4` | navigation and shared chrome |

## The airframe stat table at `0x00619bb0`

11 records (airframe ids 0–10), stride 0x2c, 11 dwords each. Field meanings, each from a
reader in code:

| Offset | Meaning | Reader |
|---|---|---|
| +0x00 | cost ($) | 2251 at `0x0040af57`; total-cost `FUN_00405680` |
| +0x04 | airframe weight (lb) | 2251 at `0x0040af67`; total-weight `FUN_00405550` |
| +0x08 | weight capacity (lb) | validator 2264 at `0x0040b4bc` ("OVERWEIGHT", `langui` 1227) |
| +0x0c | agility rating base | star rating `FUN_0040faf0` case 3: `(val − 1)/4`, clamped to 4 stars |
| +0x10 | armour rating base | `FUN_0040faf0` case 2: `(val + Σ record armour dwords − 1)/0x49`, the record dwords being units premultiplied by 5; C-truncating division, clamped to 4 |
| +0x14 | availability threshold (campaign progress) | `FUN_00410120` compares `DAT_0064b678 + 1` against it when mapping dropdown index to airframe id |
| +0x18 | per-gun-slot turret bitmask (bit i = slot i is a turret) | gun cost `0x00405700` tests bit `slot` to pick the turret price column |
| +0x1c..+0x28 | four `langui` string ids, one per gun slot (slot titles) | GUNS callback 2250 tail at `0x0040ef8c` |

The values (id: name / cost / weight / capacity / agility / armour / availability /
turret mask / slot-title ids):

| Id | Airframe | Cost | Weight | Capacity | Agi | Arm | Avail | Turrets | Slot titles |
|---|---|---|---|---|---|---|---|---|---|
| 0 | Hoplite | 6800 | 1400 | 4160 | 20 | 60 | 12 | 0x00 | 3061, 3070, 3062, 3069 |
| 1 | Hellhound | 4548 | 3255 | 9525 | 12 | 95 | 16 | 0x08 | 3071, 3072, 3061, 3073 |
| 2 | Balmoral | 1870 | 5460 | 15760 | −2 | 125 | 3 | 0x0c | 3061, 3062, 3060, 3073 |
| 3 | Bloodhawk | 5568 | 2415 | 6545 | 19 | 80 | 8 | 0x00 | 3061, 3062, 3063, 3064 |
| 4 | Brigand | 3783 | 3885 | 11015 | 8 | 105 | 6 | 0x08 | 3062, 3069, 3061, 3073 |
| 5 | Devastator | 4250 | 3500 | 10100 | 10 | 100 | 1 | 0x00 | 3074, 3075, 3076, 3077 |
| 6 | Firebrand | 2763 | 4725 | 14035 | 3 | 110 | 12 | 0x08 | 3061, 3079, 3062, 3073 |
| 7 | Fury | 5015 | 2870 | 7610 | 16 | 90 | 7 | 0x00 | 3062, 3069, 3061, 3070 |
| 8 | Kestrel | 2890 | 4620 | 13780 | 4 | 110 | 2 | 0x08 | 3065, 3078, 3062, 3073 |
| 9 | Peacemaker | 5228 | 2695 | 7205 | 17 | 85 | 3 | 0x00 | 3065, 3066, 3067, 3068 |
| 10 | Warhawk | 2423 | 5005 | 14675 | 2 | 120 | 19 | 0x00 | 3061, 3070, 3062, 3069 |

Slot-title strings are `extracted/rof/ui_strings.json` (3060 Nose Turret, 3061 Inner Wing
Guns, 3062 Outer Wing Guns, 3073 Rear Turret, …). The turret bitmask lines up with which
titles are turrets: the Hellhound/Brigand/Firebrand/Kestrel slot 3 is the Rear Turret (bit 3),
the Balmoral's slots 2 and 3 are Nose plus Rear Turret (0x0c).

## There is no per-airframe slot count

The premise that airframes differ in slot **counts** is disproven at three levels:

- `GUNS.SCRIPT` creates exactly 4 gun dropdowns in a fixed `for (R = 0; R < 4; R++)` loop,
  `HARDPOINTS.SCRIPT` exactly 2 rows; no callback gates either loop.
- The get/set handlers (2249 at `0x0040bea2`, 2245 at `0x0040ad0f`) read and write all slots
  unconditionally.
- The airframe stat table's 11 dwords are fully accounted for above; none is a count. The
  purchase validator 2264 checks only funds, weight capacity and engine presence.

Every airframe has 4 gun slots and 2 per-wing hardpoint groups. What varies is the slot
*titles*, which slots are *turrets* (pricing and weight), and the weight capacity that limits
what fits. A stock plane that appears to carry fewer guns is a prebuilt record with the empty
gun id (5) in some slots.

## The economy

Total cost (`FUN_00405680`, written to record +0x28) =
airframe cost + engine cost + gun cost + armourUnits×4 + (hpLeft + hpRight)×410.
Total weight (`FUN_00405550`, written to +0x3c) =
airframe weight + engine weight + gun weight + armourUnits×4 + hardpoints×480.

**Guns: table at `0x00619e68`**, stride 0x1c, indexed by gun id 0–4 (`langui` 3310–3314:
.30 Zephyr, .40 Carver, .50 Barret, .60 Cheyenne, .70 Goliath). Per record: +0x00 cost wing,
+0x04 cost turret (chosen when the airframe's turret bit for that slot is set), +0x08 weight
wing, +0x0c weight turret, +0x10/+0x14 non-price stats (10/2800, 9/2400, 8/2000, 7/1600,
6/1200 down the calibres), +0x18 a 1000 constant. A twinned gun (record +0x84 bit set) doubles
both cost and weight (`0x00405700`/`0x004055c0`).

| Gun | Wing $ / lb | Turret $ / lb |
|---|---|---|
| .30 Zephyr | 240 / 280 | 440 / 520 |
| .40 Carver | 320 / 380 | 530 / 620 |
| .50 Barret | 410 / 480 | 610 / 720 |
| .60 Cheyenne | 490 / 580 | 700 / 820 |
| .70 Goliath | 580 / 680 | 780 / 920 |

**Engines** (`FUN_004057c0`, weight at `0x0040b01c`): a per-airframe base table at
`0x00619d98`, stride 0xc (+0x00 base cost, +0x04 base weight, +0x08 power rating). Rows for
airframes 0–10: (850, 1000, 200), (1700, 2000, 261), (2550, 3000, 126), (850, 1000, 300),
(1700, 2000, 240), (1700, 2000, 251), (2550, 3000, 207), (850, 1000, 281), (2550, 3000, 215),
(850, 1000, 290), (2550, 3000, 201). Engine id 0–5 then offsets: cost + {−425, 0, +425, +5,
+430, +855}; weight + {−500, 0, +500, 0, +500, +1000} (dword table at `0x00619e1c`). Id 6 is
"no engine" (cost and weight 0) and blocks purchase. Engine names are `langui`
3100 + airframe×6 + engineId.

**Armour**: units 0–12 per zone at record +0x74 Nose, +0x78 Tail, +0x7c Left Wing, +0x80
Right Wing. Displayed as units×5 (the record's own stored scale, labelled "units", never
pounds); priced and weighed at units×4 (handler `0x0040b0e4`,
cost `LEA EDX,[ECX*4]` at `0x0040b1a6`, weight `units*20/5` at `0x0040b188`). Zone names
`langui` 1191–1194.

**Hardpoints**: $410 and 480 lb each (handler `0x0040b2b4`; the constants resolve at
`0x0040b31d`, 0x19a and 0x1e0, and reappear in both totals functions).

**The purchase gate is the button, not the commit.** The commit callback 2263 (`0x0040b56c`)
re-checks nothing: it recomputes the derived record fields, deducts the total cost from the
funds global `0x0064b788`, copies the scratch record into its slot (`rep movsd`, 0x33 dwords)
and selects it. The block lives in `PURCHASE.SCRIPT`: `gui_init` calls the problems callback
2264 and mails `pur_b_purchase` 10000 (enable) on a clean answer or 10018 (disable) on
problems, with `pur_t_problems` carrying the text. An overweight or engineless build therefore
cannot be bought in the original; the button greys out.

## Into the mission: what the build changes on the spawned vehicle

The consumer trace past the launch bridge. Armour and engine are live combat data; total
weight and the stat-table power rating are hangar-only.

**Armour (record +0x74/+0x78/+0x80/+0x7c order nose, tail, left wing, right wing).**
`FUN_00417090` converts the four ints to floats with no rescaling (the x5 premultiply
survives) into globals `0x0071daf8..db04` (player) / `0x0071db58..64` (wingman); the
multiplayer writer `FUN_004136e0` (`0x00413acd..0x00413af1`) fills the same globals from the
MP config block at `0x00643044`. The mission loader `FUN_004735b0` patches them onto the
matched `CCEVeh` record at +0xdc/+0xe4/+0xec/+0xf4 in three name-matched branches ("player" at
`0x00474db4`, "wingman_1" at `0x00474edf`, the campaign branch at `0x0047510d`), each store
guarded by `value >= 0.0`: **a negative armour global means "leave the mission file's value
alone"**. The vehicle spawn `FUN_0047c210` (`0x0047cb67..0x0047cc63`) hands them to
`FUN_0047bd90(veh, zoneName, armour, structure)` for the four zones named `nose` / `tail` /
`leftwing` / `rightwing` (strings at `0x0062838c..0x006283a8`). Each zone (vector at
`veh+0x9c`, stride 0x58) holds two pools as (max, current) pairs: armour at zone+0x24/+0x28,
structure at +0x2c/+0x30; an argument of `-1.0` leaves that pool alone, and the hangar sets
only the armour pool (structure comes from the mission file, `CCEVeh+0xe0/+0xe8/+0xf0/+0xf8`).
Vehicle totals (`veh+0x2c4/+0x2c8` armour max/current, `+0x2cc/+0x2d0` structure) are
recomputed on every zone write as the sum over zones of values > 0, so they are derived state,
never independent. Difficulty scales an enemy's pools and totals by 0.875 / 1.0 / 1.125
(`FUN_00440710` mapped through `d*0.125 + 1.0` at `0x0047cc00..0x0047cd10`), suppressed when
`CCEVeh+0xa4` is set; the player's plane is never scaled. Both pools feed combat: the
damage-callout thresholds read `(armourCur + structCur) / (armourMax + structMax)` against
0.3/0.5/0.7 (`FUN_00498170` at `0x00498513..59`), and the totals are read across the damage
and HUD paths (`0x0047ee30`, `0x0049fa42`, `0x004b80d1..`, among others). The per-hit
application order (zone vs total, armour vs structure) is not decoded here.

**Engine (record +0x30).** The registry the trace ends in is authored data, shipped and
extracted: `extracted/zrdr/engines.zrd.json` is the `[key, name, power]` list the runtime
vector at `0x0064fb80` loads, and `FUN_00416e10`'s base ids land exactly on its per-airframe
"Lvl-1" rows (Bloodhawk 10, Peacemaker 13, Fury 16, Hellhound 19, Devastator 22, Brigand 25,
Kestrel 28, Firebrand 31, Warhawk 34, Hoplite 37, Balmoral 40; generic rows 0-4 are the
mission-side "Normal/Large/Small/Tiny/Super" engines). Power scalars run 0.23 (Balmoral
Lvl-1) to 1.28 (Warhawk and Firebrand Lvl-3). `FUN_00416ee0` decomposes the pick: engine ids
0/1/2 are three power tiers (base + 0/1/2 = Lvl-1/2/3), 3/4/5 the same tiers plus a
**nitrous injector boolean**, 6 is stock (no offset, no nitrous, no cost or weight). The
airframe maps to that base registry id via `FUN_00416e10`
(eleven-entry switch, base 0x0a stepping by 3: airframe 0 -> 0x25, 1 -> 0x13, 2 -> 0x28,
3 -> 0x0a, 4 -> 0x19, 5 -> 0x16, 6 -> 0x1f, 7 -> 0x10, 8 -> 0x1c, 9 -> 0x0d, 10 -> 0x22) and
the tier adds 0/1/2. Result and nitrous flag ride globals `0x0071daf0`/`0x0071daf4` (player,
mirrors for wingman and MP) onto `CCEVeh+0x10c`/`+0x108` (id -1 = no override), and
`FUN_0047c210` (`0x0047d4e0..17`) resolves the id in the **engine registry at `0x0064fb80`**
(entry stride 0x18, key +0x00, name +0x08, **power float +0x14**) writing the power into
`veh+0x66c`, inside the flight-tuning block `+0x654..+0x678` (spawn-jittered by
`FUN_00476250`, read in the force path `FUN_0048fc40` at `0x0048fde1` multiplied by
`veh+0x678`). Nitrous sets `veh+0x946`. Both identifications corroborated by the debug
console `FUN_0043d640` ("Choosing engine %s.", "You now have the nitrous injector").

**Total weight (record +0x3c) is hangar-only.** Written by `FUN_00405550`; its only consumer
is the overweight indicator at `0x0040b4ab..c8` comparing against the stat table's capacity.
Neither launch bridge reads +0x3c, and the plane-record array is addressed nowhere in flight
code (33 sites, all menu-band, plus `FUN_00443de0`, the in-mission weapon wiring, which reads
only +0x84 and the dword runs +0x88..+0xa4 / +0xa8..+0xc4). Weight is not mass, thrust or
drag anywhere.

**The stat-table power rating (`0x00619d98+8`) is display-only**: two readers program-wide,
the hangar's power stat line at `0x0040bb3e` and the star rating `FUN_0040faf0`. The stat
line multiplies the rating by a per-engine-id double table at `0x00619e38`: 0.9 / 1.0 / 1.1
for the three tiers, 1.197 / 1.33 / 1.463 for the same tiers with nitrous (exactly x1.33),
then truncates. The sim's engine power comes from the separate registry above.

**The type-8 spawn message is paint, not stats.** `FUN_004084a0(8, buf)` dispatches to
`FUN_00401e80`, which builds `assets\graphics\<pattern>\...` texture paths from descriptor
+0x40 (pattern, a 14-entry name table at `0x0060301c`), +0x2c and +0x68; `FUN_0041a320`
registers three 0x34-byte texture entries per plane from the +0x5c/+0x60/+0x64 picks against
the logo/decal name table at `0x0061da20`. Armour and engine ride the global block, not this
message.

## The campaign wallet

The player's money is one dword, `0x0064b788`, exposed to the GUI scripts under the name
`nPlayerCash` by the script-global binder `FUN_00401fc0` (which registers the whole
`nMission` / `fIAConstruction` / `fMultiPlayerConstruction` / `fAllowAll` / `szSaveGameDir`
family the same way). **Program-wide it has exactly four writers**, so the ledger below is
complete, not a sample.

| Site | Effect |
|---|---|
| `0x0041146b` (`FUN_004113b0`) | fresh profile: `nPlayerCash = 0` |
| `0x0041160b` (same function) | `nPlayerCash = 250000`, only when `fAllowAll` is set |
| `0x00405db9` (`FUN_00405ce0`) | mission wrap-up: `+=` the objective reward below |
| `0x0040a067` (sell handler) | `+=` the sold plane's full build cost |
| `0x0040b5f3` (commit 2263) | `-=` the scratch build's total cost |

**Starting funds are $0.** `FUN_004113b0` is the fresh-start initialiser (called from
`FUN_004112b0` at boot and from the screen-flow dispatcher `FUN_00407670`); it zeroes the
plane-slot array, resets campaign progress `0x0064b678`, and sets the wallet to zero. The
`250000` on the next branch is not a campaign figure: `fAllowAll` (`0x00647b5c`) is the flag
that also switches off every airframe availability threshold in `FUN_00410120` /
`FUN_004100d0` / `FUN_00410170`, and the same branch fills plane slots 2 to 12 with the eleven
stock airframe templates. It is the unlock-everything mode, and its budget is the only
literal money constant in the image.

**The campaign instead starts with two aircraft, not with cash.** The same initialiser copies
prebuilt records 11 and 12 (204 bytes each) out of the template array at `0x00619f58` into
plane slots 0 and 1, naming them from `langui` 511 `IDS_PILOTPLANENAME` "Gypsy Magic" and 512
`IDS_WINGPLANENAME` "The Knave". Both records carry airframe id 5 (Devastator), engine id 1
(the Lvl-2 tier) and 2 hardpoints per wing. Templates 0 to 10 in that array are the eleven
stock airframe builds, one per airframe id.

### The sell price is the full build cost

Both the confirmation prompt and the credit compute it the same way, and neither applies a
depreciation factor: `FUN_00405680(record)` (the same total-cost function the Purchase screen
displays) is taken through an `FILD` / `ftol` round trip that is arithmetically an identity, a
compiler artifact of a float-typed cost expression. The prompt is at `0x0040a1e6`, formatting
`langui` 700 `IDS_PS_QUERYSELL` ("Your %1!s! is worth $%2!d!") with the airframe's short name
(`langui` 3020 + airframe id) and that figure; the credit is at `0x0040a052`, adding the same
figure to `nPlayerCash` and clearing the slot's class dword. **So a plane is worth exactly what
it cost, and rebuilding is free of loss.**

A record whose class dword `+0x00` is `2` is a **special** plane: the sell handler branches at
`0x0040a1d9` to `langui` 704 `IDS_PS_SPECIALPLANE` ("This %1!s!, %2!s!, cannot be sold")
instead. `langui` 701 carries the separate floor, "you must keep at least two planes in your
hangar". The slot array runs 25 records (`0x0064b78c` to `0x0064cb77`), and the free-slot
finder `FUN_004111f0` refuses a purchase unless at least **six** slots are free or hold special
planes, so five slots stay reserved for the campaign awards below.

### The mission reward table at `0x0061ae80`

Ten live records of five dwords (stride `0x14`), terminated by a record whose mission id is 0.
Read by `FUN_00405ce0` (the cash half) and `FUN_00405f00` (the aircraft half).

| Mission | Objective bit | Cash | Awarded airframe |
|---|---|---|---|
| 1 | 12 | $900 | none |
| 2 | 1 | 0 | 2 Balmoral, `langui` 513 "Jumping Jane" |
| 5 | 1 | $20,000 | none |
| 6 | 3 | $5,000 | none |
| 7 | 1 | 0 | 3 Bloodhawk, `langui` 514 "Blue Streak" |
| 12 | 1 | $10,000 | none |
| 13 | 0 | 0 | 7 Fury, `langui` 515 "Red Hot Spender" |
| 17 | 0 | 0 | 0 Hoplite, `langui` 516 "Minx" |
| 19 | 1 | $5,000 | 10 Warhawk, `langui` 517 "Accipiter Annie" |
| 24 | 1 | $100,000 | none |

Field `+0x00` is the mission id in the `nMission` numbering, `+0x04` an objective bit index,
`+0x08` the cash, `+0x0c` an airframe id with **11 meaning no aircraft award**, and `+0x10` a
pointer into the dword run at `0x00646384` that **no code reads** (one slot per record; left as
an unread field, not interpreted).

The cash is paid at `0x00405db9` only when the record's mission is the mission just flown, the
run set that objective bit, and the cumulative per-mission bit mask at `0x0064cbfc + mission*0xa8`
did **not** already have it: each bonus pays once per profile, replays included. The same sum is
accumulated into the mission's own stats record, which is what the scoreboard's "Cash Earned"
(`langui` 1206 / 1211) shows. Bit 0 means "mission completed", with no separate objective gate.

The aircraft half runs in `FUN_00405f00`: on the same gate it copies the matching template out
of the special-plane array at `0x0061a9b8` (records whose class dword is `2`, stride 204 bytes,
matched on their airframe id), names it from the string above, and marks a per-airframe
already-awarded byte at `0x0064cc44 + airframeId` so it is granted once. The five names decode
their own mapping: the `langui` symbols are `IDS_HW5BALMORALNAME`, `IDS_NW2BLOODHAWKNAME`,
`IDS_HW3FURYNAME`, `IDS_RM2HOPLITENAME`, `IDS_RM4WARHAWKNAME`.

**Total campaign cash income is $140,900**, from six paying objectives, plus whatever the
awarded aircraft would fetch if they were sellable, which they are not.

### The purchase gate and what a build costs

Callback 2264 (`0x0040b477`) computes `FUN_00405680` over the scratch record at `0x0064cb78`
and reports `langui` 1226 "INSUFFICIENT FUNDS" when `nPlayerCash < cost`, alongside 1227
"OVERWEIGHT" and the missing-engine problem. The commit 2263 subtracts that same total and
writes the scratch record into a **new** slot, so a build is always paid in full: there is no
partial-upgrade price, and changing an owned plane means selling it (at full value) and
building again.

Per-unit constants, all from `FUN_00405680` / `FUN_00405550` and the two purchase-row handlers:

| Item | Cost | Weight |
|---|---|---|
| Airframe | stat table `0x00619bb0 +0x00` | `+0x04` |
| Engine | `FUN_004057c0` over `0x00619d98` plus the tier offsets | same table |
| Gun | `0x00619e68`, wing or turret column, doubled when twinned | same |
| Armour | **$4 per unit** | **4 lb per unit** |
| Hardpoint | **$410 each** | **480 lb each** |

The armour arithmetic in `FUN_00405680` is `(Σ of the four zone dwords) × 20 / 5`, and the zone
dword is the displayed unit count, so it reduces to $4 per unit. **The per-zone cap is 60
units**: the dropdown (callback 2246, `0x0040b7bd`) offers 13 rows numbered 0 to 12 and labels
row `r` as `r × 5` units. A fully armoured airframe is therefore 240 units, $960 and 960 lb,
which is the figure [`../formats/vehicle.md`](../formats/vehicle.md) reasons about against a
1900 lb `veh_weight`. Each wing hardpoint group offers 5 rows (0 to 4 hardpoints), matching the
eight rocket slots the Ammo Selection screen lays out.

### Ammunition and rockets are free

`ORDINANCELAYOUT.SCRIPT` (the Ammo Selection screen) invokes callbacks 2010, 2011, 2014, 2027,
2028, 2030 to 2037 only: plane icon, plane info line, gun names, the ammo and rocket dropdown
get/set pairs, their description panels, and accept/cancel. **No cost callback, no funds
readout and no wallet reference appear anywhere in it**, and no writer of `nPlayerCash` sits on
that path. Ammunition type and rocket type are a free per-sortie choice in the original; money
is spent in Plane Construction alone.

## Open

- The per-hit damage application order across zone/total and armour/structure pools is not
  decoded (entry points `FUN_004b9b30` / `FUN_004b9bc0` / `FUN_004b3800`).
- (Resolved elsewhere: the paint picks at +0x44..+0x64 are the colour/shade/decal index
  arrays, settled in [`formats/paint.md`](../formats/paint.md) "The swatch table and the
  pattern defaults" together with the table-shift note on pattern entries 10/12.)
- The gun table's +0x10/+0x14 stats: +0x14 matches the shipped `CLUSTER_SIZE` magazine series
  (2800/2400/2000/1600/1200) exactly; +0x10 is rate-of-fire-shaped; neither is traced to a
  combat consumer.
