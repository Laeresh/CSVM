# Mission objectives & the stunt danger zones

Part of the [format documentation](README.md). Covers how a mission names its **objectives**
and their display text — the data behind Stunt Flying's fly-through Danger Zones. Three
sources combine: `ia.json`'s `dzones` list (the zone set), the mission's `targets.json`
(node → display-string keys), and the top-level `messages.json` string table (keys →
localized text). Decoded + wired 2026-07-19 (Milestone 2.5 item 1); zone positions verified
against the C1 gamez and the assembled marker text against
`OriginalScreenshots/C1 IA1 Cloudcoverage 1.png`. Consumed by
`CSVM/src/Mech3/Messages.cs`, `CSVM/src/Flight/MissionTargets.cs`, and
`CSVM/src/Flight/StuntMission.cs`.

## The stunt objective: fly through the Danger Zones

An instant-action Stunt Flying run's goal is to fly through a fixed set of **Danger Zones**
(bridges, tunnels, hangars, arches — hence "stunt"), timed, completable in any order. Each
zone is one gamez marker node whose display name comes from `targets.json`.

### `ia.json` `dzones` — the zone list

The mission's `ia.json` (see [spawns.md](spawns.md) for the rest of that file) carries a
top-level `dzones` key: a list of `[dzpathN, dzN]` string pairs, one per zone, in objective
order.

```
"dzones", [ ["dzpath1", "dz1"], ["dzpath2", "dz2"], … ]
```

- **`dzN`** is the completion marker — a gamez `Object3d` with `mesh_index -1` (no geometry),
  sitting directly under the identity `world1` root, so its `translation` is already
  world-space (C1 `dz1` = `(-5186.2, 141.2, -6500.6)`). It sits on the tunnel/bridge/hangar
  opening the player must fly through.
- **`dzpathN`** is an untextured, vertex-coloured **polyline ribbon** mesh under the world's
  `dzpaths` group — the AI/guide route through the zone (`dzN`'s point is a vertex of it).
  Never rendered in the original; the world build skips the whole `dzpaths` subtree (see
  [world-structure.md](world-structure.md)). The remake builds it only under `--debug-dzpaths`.
  Besides the route polyline, a `dzpathN` mesh can carry **gate-outline polygons** — the
  aperture rings the zone is flown through (C2/IA1's `dzpath1` carries the hangar's
  front-aperture outline as its second polygon).

**A dzone's node is not always a `dzN` point marker** — it may name real world *geometry*:
C2/IA1's first dzone is `sghangar`, the Seaplane Hangar structure itself. Its gamez
`transform` is the no-transform string `"Initial"` (see [extraction.md](extraction.md)), so
the node's own origin resolves to the world origin, ~8 km from the building — the zone's
position must come from the subtree's mesh geometry, not the node transform. Every actual
`dzN` marker in this install (all 53, measured) is a childless `mesh_index -1` node, so the
two cases are cleanly distinguishable. The remake anchors such geometry zones on the
`door`-named leaf pair when present (the flown aperture — the hangar's `sgh_door1`/`sgh_door2`
leave a 20 m front slit), corroborated by `dzpath1`, whose second polygon outlines that front
aperture 1.9 m away.

**Read the list, not the node names.** `dzN` numbering is *not* contiguous and does not
enumerate every `dzN` in the gamez: C1B's dzones are `dz1, dz3, dz4, dz6, dz7`, and C1's
gamez contains a `dz6` that is **not** an objective. Always drive the zone set from the
`dzones` list.

Zone counts (this install): C1 5, C1B 5, C2 9, C3 4, C4 14, C5 17; **C1C and C2B have no
`dzones`** (their IA1 is a different instant-action type) — a stunt run there is empty and
falls back to free flight.

### Completion test

The original has no gate geometry — a Danger Zone is a single point. The remake completes a
zone when the plane passes within a **sphere** of the `dzN` point (radius `DzRadius`, TUNE,
15 m — approximates the opening). `help_label` distinguishes `MSG_OBJ_FLYTHROUGH` ("Fly
Through") from `MSG_OBJ_FLYOVER` ("Fly Over"); both use the same sphere test (revisit only
if a real mission reads wrong).

## `targets.json` — node → display-string keys

A mission's `targets.json` maps world-node names to their objective display text. It is a
**list of target entries**, and each entry is a **list of `[key, value]` pairs** — *not* the
flat-alternating `KEY, [values…]` reader shape (see the [shared conventions](README.md)); it
must be walked as pairs.

```
[
  [ ["description", "MSG_OBJ_TRAINTUNNEL_M"],
    ["nodes", ["dz3"]],
    ["category_label", "MSG_OBJ_DZ"],
    ["help_label", "MSG_OBJ_FLYTHROUGH"] ],
  …
]
```

| Key | Meaning |
|---|---|
| `description` | The target's own name key (`MSG_OBJ_TRAINTUNNEL_M`). |
| `nodes` | List of world-node name(s) this entry labels (usually one; the `dzN` for a zone). |
| `category_label` | The target *type* key (`MSG_OBJ_DZ` = "Danger Zone"). Optional. |
| `help_label` | The *action* key (`MSG_OBJ_FLYTHROUGH` / `MSG_OBJ_FLYOVER` / `MSG_OBJ_REFPOINT`). |

The file is generic across mission types — the same schema labels dogfight zeppelins
(`MSG_TRGT_ZEP_ENEMY` / `MSG_OBJ_DISABLEENG`) and reference points (`ap_transmitter`
radio tower). The stunt loader reads only the entries whose node is a `dzN` from `dzones`.

## `messages.json` — the string table

`MSG_*` keys resolve through the game's localized string table. **This is not a zrdr
reader** — it is a single top-level file (`extracted/messages.json`, default `--messages=`),
a plain JSON object, not a nested list:

```
{ "language_id": 1033,
  "entries": [ { "key": "MSG_OBJ_DZ", "id": 461, "value": "Danger Zone" }, … ] }
```

Look up by `key` (the `id` is the engine's numeric handle, unused here). Relevant stunt
values: `MSG_OBJ_DZ` = "Danger Zone", `MSG_OBJ_FLYTHROUGH` = "Fly Through",
`MSG_OBJ_FLYOVER` = "Fly Over", `MSG_OBJ_TRAINTUNNEL_M` = "Train Tunnel Mid",
`MSG_BRF_IASF_OBJ2` = "Fly through all the Danger Zones to win!" (the stunt intro line).
An unknown key resolves to itself (visible, not blank).

## Assembled marker text

The three sources combine into the original's marker string
(`OriginalScreenshots/C1 IA1 Cloudcoverage 1.png`):

```
<category_label> [<help_label>] - <description>   →   "Danger Zone [Fly Through] - Train Tunnel Mid"
```

The remake's marker HUD (Milestone 2.5 item 2, `src/Flight/MarkerHud.cs`) appends the relative
clock bearing (`… 7 o'clock`) — computed from the plane's heading, not stored in the data — and
renders the assembled string either as a projected on-screen marker (at the zone's screen
position) or, when the zone is off screen/behind, as a screen-edge arrow pointing toward it.
