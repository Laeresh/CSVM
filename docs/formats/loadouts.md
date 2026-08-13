# Stock loadouts — `CSVM/data/stock_loadouts.json`

Part of the [format documentation](README.md). **Unlike every other page here, this documents a
file *we* author, not an extracted original format.** `CSVM/data/stock_loadouts.json` is
hand-authored config describing the original game's default weapon fit for the 11 player
aircraft — the same category as this reference, **not a game asset** (so the no-assets rule does
not apply; it is committed). It synthesises facts already decoded in [markers.md](markers.md)
(mounts + the slot→firepoint binding rule) and [weapons.md](weapons.md) (the `wep_*` catalogue),
and is the seed the flight engine's loadout reader (`src/Flight/Loadout.cs`, M3 wave B item B12)
consumes: it resolves each slot's `markers` against a built plane and each `caliber`+`ammo` /
`stock` id against `weapons.json`, exposing live gun groups (independent ammo counters) and
hardpoints. Inspect the binding for any plane with `--dump-loadout`.

The data is the user's stock table read off the original's Ammo Selection / configurator UI
(delivered 2026-07-22), corroborated by `OriginalScreenshots/Ammo Selector Hoplite.png` /
`… Balmoral.png`. There is **no player loadout in the extraction** — all 12 `p*` vehicle defs
inherit a capability *catalogue* (every gun + 1,000 of each rocket), and the per-plane fit is
executable-resident — so this file supplies what the data cannot.

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
    "hardpoints": {"count": 3, "stock": "wep_06"}
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
| `hardpoints.stock` | the `wep_*` id every pylon carries in stock fit (`wep_06`, HE) — one id for all pylons, see the [schema limitation](#the-uniform-he-stock-load-is-an-observation-and-a-schema-limit) |

## Two resolution rules baked into the file

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
sequentially — it takes the first `count` entries of `Loadout.PylonFillOrder = {1,5,2,6,3,7,4,8}`
(`BL-294`/`PT-31`, user-observed at the controls against the original's weapon-gauge belt lights:
a partial stock fit lands on both wings alternately, not piled onto pylon1's side). `Hardpoint.Index`
carries the resolved pylon NUMBER, not the loop position, and the weapon gauge's belt lights and
arrow-target math (`GaugeCluster`/`FlightController.UpdateWeaponGauges`) key off that number against
the dial's fixed 8-position ring — an unfitted physical position reads red, the gap the fill order
predicts (`hud.md`'s belt-light rule). A full 8-pylon loadout (Balmoral, Warhawk) is unaffected: the
fill order is a permutation covering all eight positions either way.

## Turrets are represented but inert

W4 (and the Balmoral's W3) is a **turret** on the five turret airframes (`pavenger`, `pbalmoral`,
`pbrigand`, `pfirebrand`, `pkestrel` — the Balmoral is the only two-turret airframe: Nose + Rear).
Those slots carry `"turret": true` and the loader parses them but constructs them inert — and
**they stay inert even now that the gunner is live (M4 C9a)**: the running turret is built from
the vehicle def's `turrets` block against `ai.zrd` (`TurretController`,
[turrets.md](turrets.md)), so its weapon is the `ai.zrd` row's `WEAPON.NAME` (`wep_140` on every
carried entry), **not** this file's caliber. The slot rows here remain the configurator-facing
record of the mount — note the caliber the original's Ammo Selection UI shows (50/30/40 by
plane) differs from the `wep_140` the gunner actually fires, an unreconciled original-data
tension. A turret's binding-rule firepoints are nominal (fp1,2 is a forward/wing point, not a
rear one); its real barrels are the `fgun`/`rgun`/`bgun*` gun nodes.

## The stock table

11 planes, 22 stock guns + 6 turret slots. Verified: every plane parses, every `markers` entry
exists on that plane's model, every gun matches the binding rule, and every derived `wep_*`
(guns) and `hardpoints.stock` resolves in `weapons.zrd.json`.

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

## The uniform-HE stock load is an observation, and a schema limit

**`hardpoints.stock` being a single HE id per plane is what the retail Ammo Selection UI shows**
(user-read 2026-07-22, corroborated by `OriginalScreenshots/Ammo Selector Hoplite.png` /
`… Balmoral.png`) — it is an observation of the shipped default, not a statement that a plane
*can only* carry one ordnance type. It cannot be checked against the extraction, because there
is no player loadout in the data at all (above).

The original design describes the opposite as normal: its Ordinance Loadout screen is a pop-up
menu **per hardpoint**, each listing that hardpoint's available types, so a plane's pylons could
carry a mixed load. The design's per-airframe table also gives more hardpoints than the retail
fit does on several planes — one of many places its numbers were rebalanced before release, so
the retail observation wins on *what the stock fit is* and the design wins only on *whether
mixing is possible*.

**Our runtime is already mixed-capable; only this file's schema is not.** `Loadout.Hardpoint`
carries a per-pylon `Weapon`, and `FlightController` builds `_ordnanceTypes` by collecting the
**distinct** weapon ids across the bound hardpoints in pylon order — so a plane with three
different rocket types on its pylons gets a three-entry ordnance selector with no code change.
What cannot express it is `hardpoints: {count, stock}`, which has room for exactly one id.
Expressing a mixed fit means widening that to a per-pylon list (e.g. `"pylons": ["wep_06",
"wep_06", "wep_08"]`), with `{count, stock}` kept as the shorthand. **Record this as a schema
limitation of our own config file, not a bug** — nothing is wrong with the stock table as it
stands, and the widening is only worth doing when the configurator lands.
