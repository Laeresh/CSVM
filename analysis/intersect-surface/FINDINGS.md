# `intersect_surface` is the original's per-node collision flag

The question (`BL-206`): the C3 tikicave spiderweb crashed the plane at full speed, while in the
original it only ever triggers its 0.7 s fade and the plane flies through. The plan's trap said
"do not special-case by name — find the data signal". This survey found it.

`survey.py` (run from the repo root) sweeps every chapter's `extracted/<ch>/gamez/nodes.json` and
tallies the node flag pair `(intersect_surface, intersect_bbox)` over mesh-bearing nodes.

## Result (this install, 2026-07-31)

| | count |
|---|---|
| `intersect_surface` true, `intersect_bbox` false | 25,921 |
| both false | 3,957 |
| both true | 324 |
| surface false, bbox true | **0** |

- **Every one of the 419 distinct false-flagged names is a non-solid thing**: spinning props
  (`spin`/`counterspin`/`propstill`, 475 each), wreck/debris pieces (`part1..12`, `piece1..4`,
  `pt1..12`, `b_part*`, `f_part*`, `zdtop*`), effect geometry (`fire*`, `flake*`, `ripple*`,
  `splash_polys`, `shadow`, `bflare`, `sflsh`), light glows (`lite01..11`, `ltout01..11`,
  `litebulb`, flares), ropes/reels/sinkers/studs, small `g\d+` fragments — and the C3
  `spiderweb`. No terrain, water, building or destructible *healthy* variant is ever false.
- **No false-flagged node has a true-flagged mesh descendant anywhere in the install**, so a
  builder may inherit the exemption down the subtree (the same discipline the sky/billboard
  exemptions already use).
- The reverse direction is *not* a solidity claim: a few effect-ish nodes (`dust`, `burnpl*`,
  one of the two `splash_polys`) are flagged true. The flag is only honoured in the
  false → no-collider direction.
- `intersect_bbox` is only ever true where `intersect_surface` is also true, so gating on
  `intersect_surface` alone loses nothing.

## What shipped

`GameZ.cs` reads the flag into `GameZNode.IntersectSurface` (default true when `flags` is absent,
so a legacy v0.6.1 extraction stays fully collidable); `WorldBuilder.NoCollisionNode` exempts
false-flagged subtrees from collider building. Documented in `docs/formats/gamez.md`.

Measured effect (freecam `--collision`, colliders before → after, mesh counts identical, zero
errors): C1 2477→2020, C1B 1439→1051, C1C 1733→1324, C2 1945→1533, C2B 1335→957, C3 2417→1775,
C4 2638→1855, C5 4583→3569.

The flip on the reported symptom: a scripted full-speed dead-centre run into the web
(`--pos=-4750,133,-4839 --direction=1,0,0 --hold=0,0,0,1`) crashed `CRASH into spiderweb/col` at
122 m/s with the flag ignored, and passes through mid-fade with it honoured — the fade trigger
(`EXECUTION_BY_RANGE reached - starting spiderweb_gone at 45 m`) fires either way.

Side effect worth knowing: the kkgate wreck's 12 `pt*` pieces are false-flagged too, so debris no
longer collides (closes the `BL-207` collider half — the original never made debris solid).
