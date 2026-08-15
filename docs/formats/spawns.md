# Player spawns, the Instant Action config & the campaign mission map

Part of the [format documentation](README.md). Covers where a mission places the player and how
an instant-action mission is configured:
`ia.json` (instant action) and `objectives.json` (story missions), both in the **mission's
own zrdr archive** (`<chapter>/<mission>/zrdr.zbd` — a different archive than the shared
top-level zrdr). Spawn positions and headings are verified byte-exact
against the data for C1/IA1 `zeppelin_run` and side-by-side in-game for C3/M01. Consumed
by `CSVM/src/Flight/SpawnPoints.cs`.

## At a glance

This page is the current reference for its documented format family.

## At a glance

This page is the current reference for its documented format family.

## Instant action — `ia.json` `spawn_points`

`spawn_points` is a dict mapping scenario name → list of spawn entries
`[x, y, z, heading°]`. The original picks one entry at **random** per launch (e.g.
C1/IA1 `zeppelin_run` = 4 spawns, which are the first 4 of `dogfight_ace`). Scenario
names seen: `dogfight_ace`, `dogfight_squadron`, `stunt_flying`, `zeppelin_run`. No
throttle/speed fields — the game uses a fixed start (below).

Scenario names appear **only** in `ia.json` (spawn lists + `disallow_missions`); no
reader carries scenario-conditional world state — the world build is per-mission,
identical across scenarios (analysis in [anim-definitions.md](anim-definitions.md)).

`ia.json` also carries the stunt-mode `dzones` (fly-through Danger Zone) list — see
[missions.md](missions.md).

## `ia.json` is the whole Instant Action configuration

The setup and wrap-up **UI** built around this data — the screen's dropdowns and their option
strings, environment ↔ chapter, the thirteen militias' aircraft lists, and which wrap-up rows are
actually wired — is [instant-action.md](instant-action.md), not this page.

`spawn_points` and `dzones` are two keys of many. Every chapter's `IA1/zrdr/ia.json` is a
complete, data-driven definition of that chapter's instant-action mission: which scenario it
runs, what the player flies, how many wingmen, four enemy waves, and a named ace with a full
livery. Key census over all 8 chapters (only `player_plane`/`num_wingmen`, absent on C2B, and
`dzones`, absent on C1C/C2B, are not present in all 8):

| Key | Value | Meaning |
|---|---|---|
| `mission_type` | one scenario name | which scenario this chapter's IA1 runs |
| `disallow_missions` | scenario names | scenarios this map cannot host (C1C bars `stunt_flying`, consistent with its having no `dzones`) |
| `player_plane` | display name (`"Bloodhawk"`) | the player's aircraft — the UI name, not a `vehicle.json` def |
| `num_wingmen` | `3` in all 7 | friendly flight size |
| `group1`…`group4` | nested dict | one enemy wave each: `num_enemies` (clamped to 6), `enemy_name` (a `MSG_*` key), `enemy_plane` (display name), `enemy_skill` (`novice`/`veteran`/`ace`). ⚠ **`enemy_skill` is read by nothing**, see [instant-action.md](instant-action.md) |
| `zeppelin_type` | `"cargo"` in all 8 | which of the three zeppelin slots the scenario uses |
| `cargo_zeppelin` / `passenger_zeppelin` / `military_zeppelin` | node name | the world node each type resolves to (`multiplayer1zep` throughout this install) |
| `ace_name` | `MSG_*` key | the named ace's display name (`MSG_PALBLAKE_NAME`) |
| `ace_plane` / `ace_skill` | display name / `"ace"` | the ace's aircraft |
| `ace_stats` | 9 numbers | the ace's pilot-skill modifiers (below) |
| `ace_accentID` | integer | voice accent id, same field as `vehicle.json`'s `accentID` |
| `ace_pattern`, `ace_color1..3`, `ace_decal1..3` | livery | the ace's paint scheme |

**The ace livery keys are our `PaintScheme` fields under different names.** `ace_pattern` is a
`paint_pattern` name (`blake`, `blckswan`, `hughes`, `hollywd`, `broadway`), `ace_colorN` are
0–255 RGB triples and `ace_decalN` are numbered decal indices — the same encoding
[paint.md](paint.md) documents for `vehicle.json`, so they feed `PaintScheme`/`PlanePainter`
directly with no translation.

**`ace_stats` — 9 values, inferred mapping.** Every chapter stores `[9,9,9,9,9,9,9,9,9]`, so
the data cannot discriminate the order. The structural evidence is strong: `vehicle.json`
carries exactly nine pilot-skill keys and all 26 defs that have them emit them in one identical
order — `dare_devil, natural_touch, sixth_sense, dead_eye, quick_draw, steady_hand,
stun_recovery, talker, constitution` — with `accentID` broken out separately, exactly as
`ia.json` breaks out `ace_accentID` after `ace_stats`. The original design describes the same
nine as its NPC pilot-skill modifiers, with higher = better; `9` is a maxed-out ace.
**Read as inferred, not decoded** — a chapter with a non-uniform `ace_stats` would settle it,
and this install has none. Non-uniform sets do exist inside `crimson.exe` (the record's own
`ace_stats` default and the five-row table Instant Action rolls wave pilots from, both on
[instant-action.md](instant-action.md)), but they are as unlabelled as the data is, so they
corroborate the count and the scale and leave the order where it was.

**What the whole `ia.json` is turned into** is the "setup path" section of
[instant-action.md](instant-action.md): the setup record, the mission-type ids, the synthetic roster
block every actor is spawned from, and which of these keys the engine never reads.

## Story missions — `objectives.json` `PLAYER_INIT`

```
PLAYER_INIT  [1, [x, y, z], [pitch, yaw, roll]°, throttle, speed]
```

Five elements; field[0] is `1` across all 50 missions; only the yaw of the rotation
varies; `throttle` ∈ {0.5, 0.8, 1.0}; `speed` ∈ {150, 180, 580}.

**Fields [3]/[4] are NOT the player's spawn throttle/speed.** Confirmed in-game
the original always spawns at **throttle 0.5** regardless of mission
(while PLAYER_INIT[3] varies), and the start *speed* is **plane-dependent** (while
PLAYER_INIT[4] varies per mission). Their real meaning is unidentified. Position + yaw
([1]/[2]) are confirmed correct — C3/M01's spawn matched the original side-by-side.

The remake spawns at throttle 0.5 (correct) and a fixed 53.6 m/s ≈ 120 mph placeholder;
the plane-dependent start speed is an open question (candidate: a fixed fraction of
`fd_speed` — 53.6/135 ≈ 0.4 for the Bloodhawk — needs multi-plane measurements).

## Campaign mission ↔ folder map

Each `objectives.json` carries `BRF_<REGION>M<n>` objective codes — the campaign's own
mission addressing. (The prose titles are `MSG_` keys resolved from a string table not
present in these extracts.) The region code + mission number does **not** always match
the folder's M-number:

| Region | Meaning | Missions → folders |
|---|---|---|
| `NW` | Northwest / Sea Haven (confirmed) | 1→C1C/M01, 2→C1/M02, 3→C1B/M03, 4→C1/M04, 5→C1/M05 |
| `HW` | Hollywood? | 1→C2/M02, 2→C2/M01 *(swapped)*, 3→C2/M03, 4→C2B/M04, 5→C2/M05 |
| `HA` | Hawaii / islands (confirmed) | 1–5→C3/M01–M05 |
| `RM` | Rocky Mountains / Colorado? | 1–5→C4/M01–M05 |
| `NY` | New York / Empire State? | 1–4→C5/M01–M04 |

Region *names* after NW/HA are inferred from the codes. Verified in-game: HA mission 1
(C3/M01) spawn position + heading matches the original side-by-side.

**A region's lettered folders are separate worlds, not lighting variants of one.** C1, C1B and
C1C all carry `BRF_NW*` missions, but they are three different terrain databases: the Sea Haven
airfield nodes (`ap_radiotwr`, `ap_transmitter`, `aphngr01.flt`, `apbuild01.flt`,
`ap_h2otwr.flt`, `refinery_flare`) exist in C1's gamez and in **neither** C1B's nor C1C's, and
their danger-zone name sets are disjoint — C1 is the airport and rail tunnels (Passenger
Hangar, Train Tunnel East/Mid/West, Bloodhawk Hangar), C1B is a coast of natural arches and sea
caves (Rock Archway, Mermaid's/Bootlegger's/Pirate's/Hobo's Tunnel). C1C and C2B carry no `dz*`
markers and no `dzpaths` group at all. C2 vs C2B is starker still: **zero** terrain meshes with
identical vertex data, and C2's studio-backlot landmarks (`ramses`, `sghangar`) are absent from
C2B. So one campaign region = several distinct sub-maps sharing a story region and an asset
library, reused across that region's 4–5 missions.

⚠ **`location.json` and `map.json` cannot tell chapters apart — both are stale copy-paste.**
Seven of eight chapters name the same `map_c1m04` minimap. C1, C1C and C2B ship a byte-identical
`Airport_terminal`/`Passenger_hangar`/`Crops`/`Coast` camera-preset list, and C2 ships those same
four plus a `Race Start` — Sea Haven airport bookmarks on two Hollywood maps. (C1B reuses C3's
list.) They are dev bookmarks left un-updated; use gamez node names or danger zones to identify
a world.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
