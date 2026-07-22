# Player spawns & the campaign mission map

Part of the [format documentation](README.md). Covers where a mission places the player:
`ia.json` (instant action) and `objectives.json` (story missions), both in the **mission's
own zrdr archive** (`<chapter>/<mission>/zrdr.zbd` — a different archive than the shared
top-level zrdr). Decoded + wired 2026-07-15; spawn positions/headings verified byte-exact
against the data for C1/IA1 `zeppelin_run` and side-by-side in-game for C3/M01. Consumed
by `CSVM/src/Flight/SpawnPoints.cs`.

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

## Story missions — `objectives.json` `PLAYER_INIT`

```
PLAYER_INIT  [1, [x, y, z], [pitch, yaw, roll]°, throttle, speed]
```

Five elements; field[0] is `1` across all 50 missions; only the yaw of the rotation
varies; `throttle` ∈ {0.5, 0.8, 1.0}; `speed` ∈ {150, 180, 580}.

**Fields [3]/[4] are NOT the player's spawn throttle/speed.** Confirmed in-game
(2026-07-15): the original always spawns at **throttle 0.5** regardless of mission
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

Region *names* after NW/HA are inferred from the codes. So one campaign region ≈ one
geographic map, reused across its 4–5 missions and its day/night/weather variant folders
(C1/C1B/C1C etc.). Verified in-game: HA mission 1 (C3/M01) spawn position + heading
matches the original side-by-side.
