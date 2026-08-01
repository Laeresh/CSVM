# Back-to-back polygon pairs and SHOW_BACKFACE density

**Question.** The Hollywood backlot's studio screens z-fight between a sky texture and a wood
framing texture. Are they double-sided sprites, and is the problem local to that prop?

**Instrument.** `scan_pairs.py` (read-only over `extracted/<CH>/gamez/models.json`).

## Result — 2026-08-01

```
chapter   models   polys  backface    pct  pairs  in models
C1          2224   18277      7386  40.4%     12          9
C1B         1293    8323      4074  48.9%      0          0
C1C         1507    8040      3643  45.3%      2          2
C2          1755   12645      4989  39.5%     71         43
C2B         1355    7008      3361  48.0%      0          0
C3          1888   16087      6126  38.1%      3          3
C4          2414   19661      7819  39.8%      6          5
C5          2824   22493      8180  36.4%    172         66
```

**Not sprites — back-to-back pairs.** C2 model 176 (`fcpan19`, one facade screen) is 4 vertices
and 2 polygons:

```
poly0: idx=[0,1,2,3]  show_backface=false  priority=0  material 154 (sky)
poly1: idx=[1,0,3,2]  show_backface=false  priority=0  material 157 (wood framing)
```

Identical vertex set, reversed winding, one material per side, and **both flagged single-sided**.
The two are exactly coplanar at every pixel, so no depth bias of any magnitude can order them —
backface culling is the only mechanism that can, and it is what the original engine used.

**266 pairs game-wide**, in 128 models, concentrated in C2 (the backlot, 71) and C5 (172). Two
chapters have none at all, so a fix scoped to one prop would have missed 6 chapters' worth.

**The data expects culling.** 36–49% of world polygons carry `SHOW_BACKFACE`; the other ~60% are
marked single-sided, which only means something to a renderer that culls.

## What landed

`WorldBuilder` now builds with `cullBackfaces: true` (aircraft already did). Beyond the panels
this also stopped the camera-anchored skydome's near wall drawing over distant terrain and cloud
banks — C4's far mountain range and Chandler mesa, and C1's cloud banks, were being occluded by it.
