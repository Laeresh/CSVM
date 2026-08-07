# AI patrol nets — `ne0NNNNN.zrd` + `neindex.zrd`

Part of the [format documentation](README.md). The chapter-scoped patrol graphs the
original's AI flies: waypoint sets with an **explicit edge list**, referenced by every
AI-consuming reader family. First surveyed in `docs/SCOPING-M4-ai.md` (2026-07-25); the
numbers below were re-measured against the same install on 2026-08-06 and are asserted by
`CSVM.Tests/AiNetsTests.cs`. Engine reader: `CSVM/src/Mech3/AiNets.cs`; the
`--debug-ainets` overlay (F13) renders them.

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
references them by **id** ([SCOPING-M4-ai.md](../SCOPING-M4-ai.md), decoded slots table).

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
the obvious candidate. Read them raw; do not interpret.

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
  do not build patrol behaviour on it ([SCOPING-M4-ai.md](../SCOPING-M4-ai.md), wrong-claim #2).
- The stunt/danger-zone route ribbons (`dzpathN` gamez meshes, [missions.md](missions.md))
  are guide *geometry*, not AI nets — a separate system with its own `--debug-dzpaths`
  overlay.
