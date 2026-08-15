# Mission objectives & the stunt danger zones

Part of the [format documentation](README.md). Covers how a mission names its **objectives**
and their display text — the data behind Stunt Flying's fly-through Danger Zones. Three
sources combine: `ia.json`'s `dzones` list (the zone set), the mission's `targets.json`
(node → display-string keys), and the top-level `messages.json` string table (keys →
localized text); a fourth, `dzones.json`, carries per-mission overrides the remake does not
read. Decoded + wired 2026-07-19 (Milestone 2.5 item 1); zone positions verified
against the C1 gamez and the assembled marker text against
`OriginalScreenshots/C1 IA1 Cloudcoverage 1.png`. Consumed by
`CSVM/src/Mech3/Messages.cs`, `CSVM/src/Flight/MissionTargets.cs`, and
`CSVM/src/Flight/StuntMission.cs`.

## At a glance

This page is the current reference for its documented format family.

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
  Besides the route polyline it carries **two gate-outline polygons** — the entry and exit
  apertures (below).

#### A `dzpathN` mesh is always route + exactly two gates

**Measured over every `dzpathN` in this install: 80 of 80 meshes carry exactly three
polygons.** One is the route ribbon (3–124 vertices, up to 6.5 km long); the other two are
closed outline rings, and they are a *matched pair* — 64 of 80 agree in area within 10 %, 29
of them bit-equal. Their centroids sit a median 11.7 m apart along the route (0 m where the
aperture is a thin slit, up to 1.65 km where the zone is a long tunnel or valley run). C2/IA1's
`dzpath1` pair outlines the Seaplane Hangar's front aperture, 1.9 m from the `door`-leaf slit.

The original design specifies a Danger Zone as an **entry volume and an exit volume, both of
which must be crossed** — deliberately two, so that clipping one volume tangentially does not
score. The matched polygon pair is that entry/exit pair: the three-polygon shape is
data-confirmed, the entry/exit reading is design-informed and matches it exactly.

Which polygon index is which is *not* fixed — the route is usually index 0 but not always
(C4's `dzpath14` has the pair at indices 0 and 1). Classify by **material**: the two gate
outlines share one material and the route has the odd material; never use polygon index.

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

**The original does have gate geometry** — the `dzpathN` entry/exit polygon pair above — and
its completion rule is a crossing of both. The remake reads the material-matched pair and requires
a segment crossing inside each polygon, in either order. `dzN` remains the HUD anchor; `DzRadius`
is retained for its existing non-scoring consumers. A tangential touch or a plane crossing outside
the polygon aperture does not score.

`help_label` distinguishes `MSG_OBJ_FLYTHROUGH` ("Fly Through") from `MSG_OBJ_FLYOVER` ("Fly
Over"); both use the same authored gate test (revisit only if a real mission reads wrong).

## `dzones.json` — the per-mission zone overrides

A **second, separate** file, in the mission's own zrdr archive, keyed on `dzpathN` rather than
`dzN` (23 files: story missions plus C5/IA1). It is what makes one chapter's fixed zone set
behave differently per mission. **Nothing in the remake opens it** — it is decoded here, not
consumed. Flat alternating `KEY, [values…]`; all three keys are optional.

| Key | Value | Meaning |
|---|---|---|
| `objective_numbers` | `[[dzpathN, n], …]` | the zone's objective **slot index** in this mission |
| `disable` | `[dzpathN, …]` | zones switched off for this mission |
| `nosnapshot` | `[dzpathN, …]` | zones that score but capture no scrapbook snapshot |

- **`objective_numbers`** values occupy a fixed **18–31** band across the whole install (14
  distinct values, contiguous within a mission) — a reserved slot range for danger zones in the
  mission's objective list, not a zone id. C5/M01 uses all 14.
- **`disable`** is how a story mission narrows the chapter's zone set: C1/M05 disables five of
  six, leaving one; C2/M05 and C4/M05 disable **every** zone, so those missions have none. It
  names `dzpathN`, so a zone is disabled by its path, not by its `dzN` marker.
- **`nosnapshot`** corresponds to the design's per-zone capture: navigating a Danger Zone was
  meant to grab a still or video for the pilot's scrapbook, and this list opts a zone out.
  C5 sets it on 20 of 34 paths in every story mission (measured); C5/IA1's file carries
  `nosnapshot` alone. *(Key name and membership are data-confirmed; the scrapbook-capture
  reading is design-informed.)*

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
