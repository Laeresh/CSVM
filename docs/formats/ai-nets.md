# AI patrol nets — `ne0NNNNN.zrd` + `neindex.zrd`

Part of the [format documentation](README.md). The chapter-scoped patrol graphs the
original's AI flies: waypoint sets with an **explicit edge list**, referenced by every
AI-consuming reader family. First surveyed in `docs/plans/PLAN-M4-ai.md` (2026-07-25); the
numbers below were re-measured against the same install on 2026-08-06 and are asserted by
`CSVM.Tests/AiNetsTests.cs`. Engine reader: `CSVM/src/Mech3/AiNets.cs`; the
`--debug-ainets` overlay (F13) renders them, and `CSVM/src/Flight/AiNetFollower.cs` (M4 B5)
flies them as a patrol behaviour (`--ai=<plane>:<net>`), traversal along the edge list, an anchored
trailer ridden (`BL-377`), per-node tags preserved unacted-on.

## At a glance

This page is the current reference for its documented format family.

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
references them by **id** ([PLAN-M4-ai.md](../plans/PLAN-M4-ai.md), decoded slots table).

⚠ **An anchored trailer makes the whole net RIDE its target** (decoded and implemented 2026-08-15,
`BL-377`, [`org/aiPilot.md`](../org/aiPilot.md) "The trailer"). `[nodeIndex, "name"]` is not
decoration: the named object is resolved at net build and every node position the engine hands out is
offset by it, so the graph is a PATTERN carried around a moving thing rather than a fixed route. 76 of the
222 nets are anchored this way, and their targets say what the mechanism is for: `piratezep` (25),
`workersvoyagezep` (11), **`player` (11)**, `cargozep2` (10), `sprucegoose` (5), `cargozep1` (3),
`mptrailer` (3), and one each of `train01`, `cargozep3`, `beowulfzep`, `dantezep`,
`passenger_trengine`, `tanker`, `britbalmoral_2`, `barracuda`. The anchor is the LAST node in
every shipped case, sitting off the ring the other nodes form.

⚠ **A player-anchored net is not a friendly thing.** The 11 are `M4ReinfAce` and `M2Ace` (C1),
`M4MedusaAce` / `M3BritAce` / `M5Bravo` / `M5Charlie` / `M5Postpick` (C3), `M2Blacke` (C4),
`M2STI` / `M3Bravo` / `M4Miles` (C5), four of them `*Ace*` boss flights. Census of every `aiv`
block that flies one (2026-08-15): **12 blocks, 8 of them team 2 and 4 team 1.** The enemies are
`blakebloodhawk_8` (C1/M04, on `M4ReinfAce`), `bhatbrigand_1/2/3` (C4/M02) and
`stihellhound_5_1..4` (C5/M02); the friendlies are `devastator_1/2` (C3/M05 and C5/M03). One
enemy generator also names one (C5's `M4Miles`). So the mechanism is team-blind: it is how the
original puts a flight ON the player, whether that flight is escorting or hunting.

⚠ **Two anchored nets are flown by ZEPPELINS, and neither is self-referential.** C1C's `SwanZep1`
(flown by `blackswanzep`) rides `workersvoyagezep`, and C2B's `Gemini2` (flown by `geminizep`) rides
`piratezep`. That is one zeppelin's route carried around another zeppelin, a rendezvous or an
escort. Both records are `deactivated`, so a mission script is what would wake them and nothing
flies either today. Worth knowing because a zeppelin net anchored to its OWN node would pin the
zeppelin in place, and the shipped data never does that.

⚠ **Pair order is meaningful and is not id order.** The engine builds its whole net table by
walking this list forward and indexes the nets themselves by table position (`FUN_004311c0`,
[`org/aiPilot.md`](../org/aiPilot.md)), so "the chapter's first net" (what every Instant Action
actor is given) is the first PAIR here: 10 `M4ReinfAce` (C1), 29 `Patrolboat3` (C1B),
25 `M1Defense` (C1C), 1 elsewhere. C1B and C1C open on something other than their lowest id (11
on both), so a sorted-by-id read answers the wrong net on two of the eight chapters.
`AiNets.LoadIndexPairs` is the ordered read; `AiNets.Load` sorts by id and `LoadIndex` is a map.

### Net names are mission-scoped

Nets are stored per CHAPTER but authored per MISSION, and the name says which: the `M<N>` prefix
names the mission that uses the net (`M4ReinfAce`, `M2Train`, `M5Patrol1`). Censused across all 8
chapters 2026-08-15 by resolving every `aiv` block's field 0 back to its `neindex` name: **103 of
the 222 nets are referenced by an `aiv` block, and every one of them is used by a single mission,
the one its prefix names.** The remaining 119 are referenced by `egen`, `zeppelins` or `objectives`
instead, or by nothing at all.

Two qualifications, both from the same census:

- **C2's first two missions carry each other's prefix.** `C2/M01` flies `M2First`, `M2Second`,
  `M2Patrol1`, `M2Bravo1`, `M2Charlie1` and `M2Dummy`; `C2/M02` flies `M1Security`, `M1Police`,
  `M1Knights` and `M1FilmShot`. M03 and M05 match their prefixes normally. Unexplained; recorded
  because a name-based guess about which mission owns a C2 net will be wrong on those two.
- **A few shared nets are used by two neighbouring missions**, always a `*Dummy` or a `*Bravo`:
  C4's `M1Dummy` (M01 + M02, 10 blocks) and C2's `M2Bravo1` (M01 + M02).

⚠ **Instant Action ignores all of this and takes the chapter's first net regardless**, which is
therefore some mission's asset rather than a patrol area meant for free play. The per-chapter table
of what that is (a patrol boat's route on C1B, the pirate zeppelin's own course on C2B, a net no
mission uses on C5) is in [`instant-action.md`](instant-action.md).

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
cycle over nodes 0–9 (`[0,1] … [8,9], [0,9]`), node 10 edgeless and off that cycle, trailer
`[10, "player"]`. Every node has degree 2, so the graph is one closed loop, but the GEOMETRY
crosses itself: it traces a **figure eight** roughly 550 × 1030 m, and the anchor sits at the
centre of one of the two lobes, 255 m from the cycle's centroid. C1's other player-anchored net,
`M2Ace` #23 (12 nodes at y = 350, anchor node 12), is the same shape at 585 × 1105 m; it belongs to
`C1/M02`, whose `objectives` is the only file that names it, and no `aiv` block flies it. ⚠ Do not
read "closed cycle" as "ring": a plane flying one of these passes close to its target through one
lobe and about a kilometre away through the other.

## What this is not

- **`<Cx>/<mission>/zrdr/net.zrd` is a different, unnamed, edgeless file** — node counts
  quantised by mission type (8/48/80), payloads shared across missions, coordinates
  sometimes outside the mission world. Shape says *spawn table*, not route. Undecoded —
  do not build patrol behaviour on it ([PLAN-M4-ai.md](../plans/PLAN-M4-ai.md), wrong-claim #2).
- The stunt/danger-zone route ribbons (`dzpathN` gamez meshes, [missions.md](missions.md))
  are guide *geometry*, not AI nets — a separate system with its own `--debug-dzpaths`
  overlay.
