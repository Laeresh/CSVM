# AI patrol nets — `ne0NNNNN.zrd` + `neindex.zrd`

Part of the [format documentation](README.md). The chapter-scoped patrol graphs the
original's AI flies: waypoint sets with an **explicit edge list**, referenced by every
AI-consuming reader family. First surveyed in `docs/PLAN-M4-ai.md` (2026-07-25); the
numbers below were re-measured against the same install on 2026-08-06 and are asserted by
`CSVM.Tests/AiNetsTests.cs`. Engine reader: `CSVM/src/Mech3/AiNets.cs`; the
`--debug-ainets` overlay (F13) renders them, and `CSVM/src/Flight/AiNetFollower.cs` (M4 B5)
flies them as a patrol behaviour (`--ai=<plane>:<net>`), traversal along the edge list, tags
and trailer preserved unacted-on.

## Where they live

Each chapter's zrdr scope (`<Cx>/zrdr.zbd`) carries:

- **`ne0NNNNN.zrd`** — one file per net; the filename encodes the net id
  (`ne000010` → id 10). **222 files across the 8 chapters**
  (C1 29, C1B 22, C1C 20, C2 34, C2B 18, C3 30, C4 29, C5 40).
- **`neindex.zrd`** — the chapter's id → name table. 222 names install-wide, a perfect
  1:1 with the files, both directions, per chapter.

## `neindex.zrd` — the name table

```
[ [ first, id0, "Name0", id1, "Name1", … ] ]
```

One flat list: alternating id/name pairs after a leading number. ⚠ **The first element is
NOT the pair count** — it is an allocation figure ≥ the count (C1 opens with 46 over 29
pairs; C2 with 35 over 34). Reading it as a count truncates or over-reads the list; parse
pairs to the end of the list instead.

The **name** is the join key everything else uses: `egen.json` `vehicle.nets`,
`zeppelins.json` `net`, and `objectives.json` reference nets by name. `aiv.json` field 0
references them by **id** ([PLAN-M4-ai.md](../PLAN-M4-ai.md), decoded slots table).

## `ne0NNNNN.zrd` — one net

```
[ [ null, 10.0, f×9, NODES, EDGES, TRAILER ] ]
```

One record per file: 14 elements, or 13 on the 8 files that omit the trailer. Element 1
is `10.0` on every file; elements 2–10 are nine floats, near-always `0.0` — both
undecoded.

### NODES — `[x, y, z]` (+ optional tags)

World-space positions in the standard frame (right-handed Y-up, metres —
[gotchas.md](gotchas.md)). **2,268 nodes** install-wide; 2–44 per net, median 8.

A node list may carry extra numbers past `z`: **81 nodes carry two extra values and 12
carry four**. These per-node tags are **undecoded**; the design describes stop/valve
nodes on patrol routes (zeppelins halt at script-armed stop nodes), and these tags are
the obvious candidate. Read them raw; do not interpret. ⚠ The two widths are **two different
systems on disjoint sets of nets** — see [below](#the-tags-are-two-systems-not-one-measured-2026-08-10).

**Stop points are real, and they are scripted.** The binary's mission-script vocabulary
(`D:\zipper\Crimson\mission.cpp`) includes a **`COMPLETED_STOPPOINT`** condition, in the same
family as `COMPLETED_ZEPCANNONS` and `COMPLETED_SOUND_GROUP` — so a mission waits on a zeppelin
reaching its stop point, exactly as the design describes. That raises the confidence that the
per-node tags above encode stop points, but it does **not** decode them: nothing yet ties a
specific tag value to the condition. Still a lead, not a finding.

#### The tags are two systems, not one (measured 2026-08-10)

**40 nets of 222 carry tagged nodes**, and cross-referencing them against `neindex` names, the
`zeppelins.json` `net` field, and each node's degree in the edge list splits them cleanly:

| Shape | Nets | Which nets | Value pattern |
|---|---|---|---|
| **A — 2 extras** `[a, b]` | 36 | **zeppelin routes, exclusively** | `b ∈ {0,1}`, `a ∈ 0…8` |
| **B — 4 extras** `[0, 0, 1, N]` | 4 | stunt / cinematic / escort routes | `N ∈ 1…10, 33, 34` |

⚠ **Do not read the two shapes as one optional-length field.** They occur on disjoint net
populations and almost certainly belong to different subsystems.

**Shape A is a zeppelin feature.** All 36 are zeppelin routes by name; 31 are directly referenced by
a `zeppelins.json` `net`, and the remaining five are unreferenced alternates or variants sitting
beside referenced twins (`M1Cargo` beside `M1CargoAlt`, `SwanZep2` beside `SwanZep1`, plus
`Gemini1`, `M5Bombrun`, `M4PZRetreat`). Nothing that a fighter flies carries a shape-A tag. That is
exactly what the stop-point reading predicts, and it is the strongest evidence yet for it.

Structure within shape A:

- **`b` is a flag, and it is not graph topology.** It appears on both degree-1 and degree-2 nodes,
  so it does not mean "end of the path" — the edge list already says that. It concentrates on a
  net's **first and/or last** node, with long runs of `b = 0` between.
- **`a` is a small id allocated sequentially per chapter, across files.** C5's three cargo routes
  make this plain: `M3Cargo1` uses 1 and 2, `M3Cargo2` uses 3 and 4, `M3Cargo3` uses 5 and 6 — a
  chapter-wide counter, not a per-net index. `a = 0` recurs on terminal nodes.

Worked shape — `C5` `M3Cargo2`, 9 nodes:

```
node 0  [3, 1]      node 1–7  [4, 0]      node 8  [0, 1]
```

**Two readings survive this evidence and the data cannot choose between them:** `a` is a
*stop-point id* with `b` marking a halt, or `a` is a *segment id* with `b` marking a segment
boundary. Both fit every net. Settling it needs the runtime parser.

**Shape B is not a zeppelin thing at all.** It occurs on exactly four nets — `M3StuntCourse` (7
tagged nodes, distinct `N`), `M1FilmShot` (3), `M1Cabbie` (1), `M4MilesRun` (1) — i.e. the stunt
course, a camera/cinematic route, and two escort routes. The constant `0, 0, 1` prefix plus a
varying `N` reads as a reference to some other table by id. Related to the Danger Zone / stunt
gate system ([missions.md](missions.md)) rather than to `COMPLETED_STOPPOINT`.

⚠ **The runtime parser has not been located.** The only code in `crimson.exe` that names
`ne%06d.zrd` is the editor's text-file I/O and a debug dump routine, so the string-search route does
not reach the shipped loader. Until it is found, the above is *structure*, not *meaning*: read the
tags raw, preserve them, and do not act on either reading.

Two neighbouring script ops retarget net-followers at runtime — **`SET_AI_NET`** (accepts a vehicle
*or* a zeppelin) and **`SET_AI_TEAM`** — which is the design's "retreat is expressed as a net
change, not a special mode", confirmed.

### EDGES — `[i, j]` node-index pairs

**2,149 edges** install-wide; 1–43 per net. **This list is the connectivity** — the graph
branches (a node with two successors is normal) and is *not* necessarily a closed loop or
a sequential path. Never connect nodes in list order; only the edge list is the route
structure. All indices are in range on every shipped net (asserted by the golden test).

### TRAILER — the attach/follow target

Five shipped shapes:

| Shape | Files | Meaning |
|---|---|---|
| `[nodeIndex, "name"]` | 76 | target + the net node it attaches at (`[10, "player"]`, `[7, "piratezep"]`) |
| `[-1, "name"]` | 4 | a named target with **no** attach node (C2 net 21, C4 nets 6/24, C5 net 25) |
| `[nodeIndex]` | 1 | attach node, no name (C2 net 33: `[3]`) |
| `[-1]` | 133 | no target |
| *(absent — 13-element record)* | 8 | no target |

The 2026-07-25 survey recorded only `[-1]` vs `[nodeIndex, "name"]`; the middle two rows
are the shapes it missed. Target names seen: `player`, zeppelin node names
(`piratezep`, `dantezep`, …).

## Worked example

`C1/zrdr/ne000010.zrd` = id 10 = `M4ReinfAce`: 11 nodes at y = 400, 10 edges closing a
loop over nodes 0–9 (`[0,1] … [8,9], [0,9]`), node 10 sitting off the ring, trailer
`[10, "player"]` — a patrol ring whose trailer names the player via that off-ring node.

## What this is not

- **`<Cx>/<mission>/zrdr/net.zrd` is a different, unnamed, edgeless file** — node counts
  quantised by mission type (8/48/80), payloads shared across missions, coordinates
  sometimes outside the mission world. Shape says *spawn table*, not route. Undecoded —
  do not build patrol behaviour on it ([PLAN-M4-ai.md](../PLAN-M4-ai.md), wrong-claim #2).
- The stunt/danger-zone route ribbons (`dzpathN` gamez meshes, [missions.md](missions.md))
  are guide *geometry*, not AI nets — a separate system with its own `--debug-dzpaths`
  overlay.
