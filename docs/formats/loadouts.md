# Stock loadouts - `CSVM/data/stock_loadouts.json`

Part of the [format documentation](README.md). This hand-authored engine configuration records
the original game's default weapon fit for the 11 player aircraft. It combines the aircraft mount
rules in [markers.md](markers.md) with the weapon catalog in [weapons.md](weapons.md); it contains
no extracted game asset data.

## Contents

- [Schema](#schema)
- [Resolution rules](#resolution-rules)
- [Turrets](#turrets)
- [Stock table](#stock-table)
- [Schema limit](#schema-limit)
## Schema

```json
"planes": {
  "pbloodhawk": {
    "model": "player_bhawk",
    "display": "Bloodhawk",
    "guns": [
      {"slot": 1, "mount": "Inner Wing Guns", "caliber": 40, "ammo": "slug",
       "markers": ["firepoint7", "firepoint8"]},
      {"slot": 2, "mount": "Outer Wing Guns", "caliber": 30, "ammo": "slug",
       "markers": ["firepoint5", "firepoint6"]}
    ],
    "hardpoints": {"count": 3, "stock": ["wep_06", "wep_06", "wep_06"]}
  }
}
```

Keyed by **vehicle def name** (`vehicle.json`), matching `PlaneStats`. Per plane:

| field | meaning |
|---|---|
| `model` | the plane's model-root node in `planes.zbd` (what the markers hang under) |
| `display` | human name |
| `guns[]` | the stock-equipped gun groups only — mounts carrying no stock gun are omitted |
| `guns[].slot` | mount slot W1–W4 (1-based) |
| `guns[].mount` | the gun-group name, verbatim from `IDS_AIRFRAMEGUNGROUPNAMES` (markers.md) |
| `guns[].caliber` | 30 / 40 / 50 / 60 / 70 |
| `guns[].ammo` | `slug` / `dumdum` / `ap` / `magnesium` — stock is always `slug` |
| `guns[].markers` | the firepoint node(s) this group fires from (see binding rule) |
| `guns[].turret` | present + `true` on turret slots — parsed but **inert in M3** (see below) |
| `hardpoints.count` | number of underwing pylons carried; **which** physical `pylonN` markers get used is `Loadout.PylonFillOrder`, not `1..count` (below) |
| `hardpoints.stock[]` | the `wep_*` id each pylon carries in stock fit, in `PylonFillOrder` order; its length equals `hardpoints.count` |

## Resolution rules

**Gun → weapon id.** A gun group's weapon is `caliber N` + `ammo k` → **`wep_{N+k}`**, where `k`
is the ammo index (`slug` 0, `dumdum` 1, `ap` 2, `magnesium` 3) — the `wep_30`…`wep_73` player
matrix stores each caliber's four ammo types as consecutive ids. Stock ammo is `slug`, so stock
resolves to `wep_N` (`wep_30`/`40`/`50`/`60`/`70`). `caliber`+`ammo` are stored rather than the
resolved id so the future configurator can re-ammo a gun without touching its mount.

**Slot → firepoint markers.** The binding rule (markers.md) is **slot _n_ →
`firepoint(9−2n), firepoint(10−2n)`** (W1→fp7,8; W2→fp5,6; W3→fp3,4; W4→fp1,2). The `markers`
arrays are the **explicit result checked into the file, not computed at runtime**, so a wrong one
is a visible data fix — as A10's in-engine confirmation may yet require. The Kestrel's W1 `Center
Guns` resolves to just `["firepoint7"]`: it is the lone 7-firepoint airframe, its fp7 a
centreline mount with no fp8.

**`hardpoints.count` → pylon numbers.** `Loadout.Bind` does **not** resolve `pylon1`…`pylonN`
sequentially. It takes the first `count` entries of `Loadout.PylonFillOrder = {1,5,2,6,3,7,4,8}`,
so a partial stock fit leaves gaps rather than filling `pylon1`'s side contiguously.
`Hardpoint.Index` carries the resolved pylon NUMBER, not the loop position, and the weapon gauge's
belt lights and arrow-target math (`GaugeCluster`/`FlightController.UpdateWeaponGauges`) key off
that number against the dial's fixed 8-position ring, so an unfitted physical position reads red
(`hud.md`'s belt-light rule). A full 8-pylon loadout (Balmoral, Warhawk) is unaffected either way:
the fill order is a permutation covering all eight positions.

**Where that sequence comes from.** It is not an algorithm in the original, but authored
per-airframe data. `crimson.exe` holds a table of 11 stock records of 204 bytes at `0x00619f58`
(selected at `0x0040c293`, guarded to index < 11; higher indices are custom planes, which take a
pointer table instead and do not use this path). Each record's eight ordnance cells sit at
record `+0xA8`, and the value `11` is the empty-slot sentinel: `FUN_004440f0` returns -1 for it by
`default:`, and `FUN_004b2550` then clears that pylon, which is what darkens its belt light. All
eleven records fill the first `ceil(N/2)` cells of `{0,1,2,3}` and the first `floor(N/2)` of
`{4,5,6,7}`, and the slot-to-node mapping is the identity: `FUN_004b2300` caches the nodes with
`sprintf(buf, "pylon%d", i)` for `i` = 1 to 8, with no permutation anywhere in the chain. Those two
facts together give the sequence `1,5,2,6,3,7,4,8`.

⚠ **One index serves three roles and they must not be split apart.** Pylon node number, loadout
slot and gauge belt position are the same number in the original: `FUN_00454ba0` caches eight belt
lights named `mgindicator0`…`mgindicator7` and `FUN_00454d40` lights position `n` and sets the
arrow to `n × (2π/8)`, with `n` taken straight from the raw ordnance slot index. Separating the
gauge order from the physical assignment would be a divergence, not a fix.

⚠ **The two interleaved halves are not the wings.** The wings are the odd and even numbers
([markers.md](markers.md)); the halves `{1,2,3,4}` and `{5,6,7,8}` are the outboard and inboard
pairs. A consequence worth stating because it looks like a bug: a 2-pylon fit takes pylons 1 and 5,
both to port, so the original hangs the Hoplite's two rockets port-heavy and CSVM reproduces that
faithfully. The odd counts are unbalanced of necessity. Do not "correct" this on geometry alone.

## Turrets

W4 (and the Balmoral's W3) is a **turret** on the five turret airframes (`pavenger`, `pbalmoral`,
`pbrigand`, `pfirebrand`, `pkestrel` — the Balmoral is the only two-turret airframe: Nose + Rear).
Those slots carry `"turret": true` and the loader parses them but constructs them inert — and
**they stay inert even now that the gunner is live**: the running turret is built from
the vehicle def's `turrets` block against `ai.zrd` (`TurretController`,
[turrets.md](turrets.md)), so its weapon is the `ai.zrd` row's `WEAPON.NAME` (`wep_140` on every
carried entry), **not** this file's caliber. The slot rows here remain the configurator-facing
record of the mount — note the caliber the original's Ammo Selection UI shows (50/30/40 by
plane) differs from the `wep_140` the gunner actually fires, an unreconciled original-data
tension. A turret's binding-rule firepoints are nominal (fp1,2 is a forward/wing point, not a
rear one); its real barrels are the `fgun`/`rgun`/`bgun*` gun nodes.

## Stock table

11 planes, 22 stock guns + 6 turret slots. Verified: every plane parses, every `markers` entry
exists on that plane's model, every gun matches the binding rule, and every derived `wep_*`
(guns) and `hardpoints.stock[]` entry resolves in `weapons.zrd.json`.

| Plane | W1 | W2 | W3 | W4 | Pylons |
|---|---|---|---|---|---|
| Hoplite | Inner Wing 30 | — | — | — | 2 |
| Hellhound | Nose 50 | Nose 40 | — | Rear Turret 50 ᵀ | 3 |
| Balmoral | Inner Wing 50 | Outer Wing 50 | Nose Turret 30 ᵀ | Rear Turret 30 ᵀ | 8 |
| Bloodhawk | Inner Wing 40 | Outer Wing 30 | — | — | 3 |
| Brigand | Outer Wing 60 | Outer Wing 2 30 | — | Rear Turret 30 ᵀ | 4 |
| Devastator | Low Inner Wing 50 | Low Outer Wing 40 | Upper Inner Wing 30 | — | 4 |
| Firebrand | Inner Wing 70 | Middle Wing 30 | — | Rear Turret 30 ᵀ | 6 |
| Fury | Outer Wing 70 | Outer Wing 2 30 | — | — | 3 |
| Kestrel | Center 60 | Center 2 50 | — | Rear Turret 40 ᵀ | 5 |
| Peacemaker | Center 50 | Right Fuselage 40 | — | — | 3 |
| Warhawk | Inner Wing 70 | Inner Wing 2 50 | — | — | 8 |

ᵀ = turret slot — inert as a gun group; the live gunner reads `ai.zrd` instead (above). Every
pylon carries `wep_06` (HE) in stock fit.

## Schema limit

**Each `hardpoints.stock[]` entry is a separate pylon's stock id.** The all-HE retail fit is
corroborated by `OriginalScreenshots/Ammo Selector Hoplite.png` / the Balmoral capture - it is an
observation of the shipped default, not a statement that a plane *can only* carry one ordnance
type. It cannot be checked against the extraction, because there is no player loadout in the data
at all (above).

The original design describes the opposite as normal: its Ordinance Loadout screen is a pop-up
menu **per hardpoint**, each listing that hardpoint's available types, so a plane's pylons could
carry a mixed load. The design's per-airframe table also gives more hardpoints than the retail
fit does on several planes — one of many places its numbers were rebalanced before release, so
the retail observation wins on *what the stock fit is* and the design wins only on *whether
mixing is possible*.

**The runtime and schema are mixed-capable.** `Loadout.Hardpoint` carries a per-pylon `Weapon`,
and `FlightController` builds `_ordnanceTypes` by collecting the **distinct** weapon ids across the
bound hardpoints in pylon order. A mixed stock fit such as `"stock": ["wep_06", "wep_06",
"wep_08"]` therefore produces a three-entry ordnance selector without another runtime change.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
