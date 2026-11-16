# The budget instrument on C5: the city baseline

The question: what does today's Enhanced city cost at fixed C5 views, so that the city-buildings
plan's new models can be held to the vegetation budget (at most 2.0 ms GPU and 0.5 ms CPU added,
and no rise in the draw-call count at distance)?

## Baseline

Enhanced at View Distance Far, the default, on the PC. The draw and primitive counts are identical
in every launch of a view; the times are the median of the launch medians, with their range.
`render_cpu_ms` and `proc_ms` are the CPU terms (PERF-1).

| View | Batch | `gpu_ms` | `render_cpu_ms` | `proc_ms` | draws | prims |
|---|---|---|---|---|---:|---:|
| C5-cb7 | 1 | 6.58 (6.53-6.80) | 1.21 (1.19-3.03) | 1.23 (1.19-2.36) | 945 | 612,851 |
| C5-cb7 | 2 | 5.98 (5.96-6.25) | 1.47 (1.22-1.70) | 1.35 (1.21-1.62) | 945 | 612,851 |
| C5-cb1 | 1 | 5.34 (5.32-5.36) | 1.31 (1.29-1.41) | 1.19 (1.15-1.19) | 893 | 770,150 |
| C5-cb1 | 2 | 4.83 (4.82-4.84) | 1.29 (1.28-1.50) | 1.18 (1.17-1.34) | 893 | 770,150 |
| C5-alt | 1 | 3.77 (3.75-3.78) | 1.68 (1.65-1.74) | 1.14 (1.13-1.20) | 1,776 | 774,563 |
| C5-alt | 2 | 3.45 (3.44-3.46) | 1.67 (1.66-1.80) | 1.18 (1.14-1.26) | 1,776 | 774,563 |
| C5-cm23 | 1 | 4.19 (4.16-4.19) | 1.61 (1.46-2.31) | 1.29 (1.17-1.49) | 1,160 | 580,292 |
| C5-cm23 | 2 | 3.80 (3.79-3.82) | 1.44 (1.40-2.34) | 1.22 (1.21-1.51) | 1,160 | 580,292 |

The distant draw-call rule is read at C5-alt: 1,776 draws at Far.

**The two batches differ by 0.3 to 0.6 ms of GPU at every view, while the launches inside a batch
agree to 0.05 ms or better** (C5-cb7 to 0.3 ms). A GPU difference between batches is drift, not
signal (METHOD-2, PERF-48), and it is a quarter of the 2.0 ms budget. A comparison against this
baseline therefore re-measures the baseline in the same interleaved batch as the change
(METHOD-3); the table is the reference for the counts and for the order of magnitude, not a
number to subtract a later batch from. The CPU terms carry single high launches (up to 3.0 ms)
where other sessions' engine shards held the CPU at 100 %; the medians sit at 1.2 to 1.7 ms.

The Steam Deck baseline is unmeasured.

### View Distance Normal

| View | Batch | `gpu_ms` | `render_cpu_ms` | `proc_ms` | draws | prims |
|---|---|---|---|---|---:|---:|
| C5-cb7 | 1 | 6.30 (6.28-6.33) | 1.20 (1.20-1.32) | 1.21 | 866 | 369,903 |
| C5-cb7 | 2 | 5.74 (5.71-5.75) | 1.38 (1.26-2.24) | 1.39 | 866 | 369,903 |
| C5-cb1 | 1 | 5.15 (5.10-5.17) | 1.25 (1.22-1.25) | 1.23 | 740 | 516,706 |
| C5-cb1 | 2 | 4.71 (4.69-4.71) | 1.23 (1.20-1.38) | 1.23 | 740 | 516,706 |
| C5-alt | 1 | 3.51 (3.50-3.52) | 1.63 (1.61-1.63) | 1.21 | 1,539 | 528,494 |
| C5-alt | 2 | 3.20 (3.19-3.21) | 1.54 (1.53-1.66) | 1.21 | 1,539 | 528,494 |
| C5-cm23 | 1 | 3.96 (3.94-4.00) | 1.33 (1.33-1.87) | 1.26 | 933 | 449,995 |
| C5-cm23 | 2 | 3.61 (3.60-3.62) | 1.33 (1.29-1.37) | 1.28 | 933 | 449,995 |

## The instrument can fail on C5

View Distance scales the clutter fade's squared distance (`[world] clutter fade: … scale_sq=`
reads 0.25 at Normal, 0.0625 at Far and 0 at Unlimited). A scale of 0 is the same setting as
`graphics.clutterFarFade` off, the "never fades" arm of `csky_clutter_fade.gdshaderinc`; the config
key itself cannot reach a `--det` launch (DET-8), so the flag is the lever. Same batch (2), Far
against Unlimited:

| View | `gpu_ms` Far → Unlimited | `render_cpu_ms` | draws | prims |
|---|---|---|---|---|
| C5-cb7 | 5.98 → 6.82 | 1.47 → 2.06 | 945 → 1,918 | 0.61 M → 1.12 M |
| C5-cb1 | 4.83 → 5.62 | 1.29 → 2.04 | 893 → 2,634 | 0.77 M → 1.61 M |
| C5-alt | 3.45 → 5.13 | 1.67 → 2.88 | 1,776 → 4,366 | 0.77 M → 2.21 M |
| C5-cm23 | 3.80 → 5.27 | 1.44 → 2.14 | 1,160 → 2,734 | 0.58 M → 1.60 M |

Every term moves, and Far → Normal moves every count down (above). The same global scales the fog
volumes' cloud field, so part of the Unlimited rise at C5-alt and C5-cm23 is cloud sprites, not
buildings.

## The views

All four are `--freecam --chapter=C5`, with the instrument's `--det --perf --no-vsync --mute
--frames=600 --screenshot=` at `-Resolution 5120x1440` (a 5120x1421 client). The ground is at
y = 5.

| View | Pose | Why there |
|---|---|---|
| C5-cb7 | `--pos=-3328,125,-3840 --lookat=-3328,5,-3440` | 120 m over the unflagged `cblock7` cells round (-3328, -3072), outside every fog volume |
| C5-cb1 | `--pos=-7124,165,-11732 --lookat=-7424,5,-12032` | 160 m over the largest unflagged `cblock1` cells, X[-8704,-6656] Z[-12800,-11264], 100 % `cblock1` |
| C5-alt | `--pos=-5000,450,-7000 --lookat=-9000,250,-11000` | 450 m over the unflagged `cblock1` band, looking north-west across the city to the horizon |
| C5-cm23 | `--pos=-11061,400,-8370 --lookat=-11061,400,-8371` | CM23's `PLAYER_INIT` (`C5/M03` objectives, heading 0), which `--freecam --chapter=C5 --mission=M03` also starts on |

The district cells come from `../bl-305-clutter-uv/flag_geometry.py --textures cblock1,cblock7`,
with each 512 m cell kept only when its centre lies outside every `fvol*` box.

**The large `cblock7` district is under the fog volumes.** The 0 % flagged western district,
X[-16384,-12288] Z[-16384,-6144], lies inside `fvol6` and `fvol31` to `fvol34`, whose boxes reach
y = 183. A camera below that over it prints `[world] weather: camera state 3`, takes the zone-3
fog (50 to 250 m) and the near-black whiteout, and sees about ten buildings. Outside the volumes
`cblock7` is unflagged only in small patches: round (-3328, -3072) and round (-8960, -1792), each
about 1 km². A pose over the city is valid only when its log carries no `camera state 3` line.

## Reproducing

    $env:CSVM_DATA_ROOT = "Z:\CSVM"
    .\analysis\veg-budget\measure.ps1 -Chapters C5-cb7,C5-cb1,C5-alt,C5-cm23 -Modes enhanced,vdnormal,vdunlimited -Launches 3

Each launch keeps its windows after the first two (the shader compiles); nothing else prints
inside them, so the log level does not touch the times. Before reading a batch, check for other
Godot processes and GPU load beside each launch; a launch that shared the GPU with another
session's windowed run read 2 to 5 ms high and is dropped.

## Limits

- One PC: RTX 5080 on Vulkan, 7800X3D, 120 Hz display, hidden desktop at 5120x1440. The Deck is
  unmeasured.
- Static poses with no aircraft, AI or missions; CM23's flown cost is in
  `../cm23-host-perf/FINDINGS.md`.
- C5-cb7's frame takes one of two pixel hashes from launch to launch at every View Distance, while
  its counts never move. Which surface flips is not established; a golden frame at that pose would
  carry it.
