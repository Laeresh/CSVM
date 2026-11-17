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

## The city-buildings prototype against the budget

The `cblock1` prototype of the Enhanced city (14 building kinds, `CityModels`) measured against the
same build with the city off, interleaved in one batch per comparison, on the PC. A `--det` launch
with plain `--graphics=enhanced` draws the city at near (Rich and the 3D Vegetation word near, the
defaults), so the baseline's `enhanced` mode is now the city at near. `nomodels`
(`--vegetation-models=off`) and `original` (`--vegetation-quality=original`) are the city off and
reproduce the baseline's counts exactly. Each cell is the median of the launch medians and their
range. The delta is against `nomodels` in the same batch. The CPU delta is `render_cpu_ms` plus
`proc_ms`; `setup_ms` reads 0.05 to 0.06 in every case and does not move. This table is the
prototype with 512 m near cells and the vegetation band at every word; "The near cell size and
the Extreme cap" below holds the cell size and band the build uses.

| View | Mode | n | gpu_ms | delta | CPU delta | draws | prims |
|---|---|---|---|---|---|---:|---:|
| C5-cb1 | off | 3 | 4.80 (4.80-4.80) | | | 893 | 770,150 |
| C5-cb1 | near | 3 | 5.065 (5.055-5.07) | +0.265 | +0.13 | 880 | 711,975 |
| C5-cb1 | far | 3 | 4.975 (4.975-4.98) | +0.175 | +0.16 | 928 | 733,457 |
| C5-cb1 | extreme | 3 | 4.91 (4.90-4.93) | +0.11 | +0.25 | 1,173 | 821,399 |
| C5-street | off | 5 | 5.945 (5.94-5.95) | | | 744 | 695,703 |
| C5-street | near | 5 | 6.00 (5.99-6.13) | +0.055 | +0.12 | 743 | 659,425 |
| C5-street | far | 3 | 5.985 (5.98-5.99) | +0.035 | +0.155 | 785 | 677,225 |
| C5-street | extreme | 3 | 5.95 (5.95-5.96) | 0.00 | +0.29 | 1,011 | 764,215 |
| C5-alt | off | 3 | 3.435 (3.43-3.44) | | | 1,776 | 774,563 |
| C5-alt | near | 3 | 3.38 (3.38-3.385) | -0.055 | +0.06 | 1,709 | 693,194 |
| C5-alt | far | 3 | 3.39 (3.38-3.39) | -0.045 | +0.06 | 1,711 | 693,646 |
| C5-alt | veryfar | 3 | 3.40 (3.39-3.40) | -0.04 | +0.09 | 1,741 | 708,974 |
| C5-alt | extreme | 3 | 3.48 (3.48-3.48) | +0.045 | +0.14 | 1,830 | 743,038 |
| C5-cm23 | off | 3 | 3.79 (3.785-3.79) | | | 1,160 | 580,292 |
| C5-cm23 | near | 3 | 3.77 (3.765-3.78) | -0.02 | +0.025 | 1,102 | 532,963 |
| C5-cm23 | veryfar | 3 | 3.78 (3.78-3.785) | -0.015 | +0.055 | 1,118 | 542,001 |
| C5-cm23 | extreme | 3 | 3.835 (3.83-3.84) | +0.045 | +0.085 | 1,154 | 558,895 |
| C5-cb7 | off | 3 | 5.99 (5.985-5.995) | | | 945 | 612,851 |
| C5-cb7 | near | 3 | 5.98 (5.965-5.98) | -0.01 | +0.085 | 929 | 611,243 |

The street near row is from the window batch (n=5), where its delta is resolved; at n=3 it was
not. The veryfar rows are from a third batch whose off and far cases agree with the first to
0.01 ms. C5-street is `--pos=-7306,50,-11700 --lookat=-7306,40,-12600`, 45 m over the `cblock1`
street grid. C5-cb7 is not a pure control: distant `cblock1` far boxes stand in its frame, so it
draws 16 fewer calls; its GPU does not move.

**The prototype holds the GPU and CPU budget at every view and every reach.** The largest added
GPU is 0.27 ms at C5-cb1 at near, repeated across two batches. The largest added CPU is 0.29 ms
at C5-street at extreme. **The distant draw-call rule holds at near, far and veryfar and fails at
extreme**: C5-alt draws 1,830 at extreme against 1,776 with the city off, in two batches, while
the far tier lowers it to 1,709 at near.

The city adds 5,452 nodes at every view, its 5,438 near nodes among them, whether or not any is in
range. `proc_ms` rises 0.04 to 0.10 ms at every view, also where no near model is in range (C5-cb7,
C5-cm23). `render_cpu_ms` rises with the near models on screen, 0.02 to 0.06 ms at near and far and
up to 0.195 ms at extreme. At C5-cb1 extreme costs less GPU than near while drawing more. The
section below measures the node count on its own and explains the extreme figure.

**The prototype's three window modes cost the same.** The build keeps only the rolling one. At
C5-cb1 and C5-street (n=5) the fixed single windows, the fixed whole floors and the rolling mix
differ by at most 0.02 ms of GPU, inside the launch ranges, with identical counts and CPU terms.

**World build.** Over 15 launches per mode the startup total reads 7,757 ms with the city off and
8,044 ms at near (clutter +78 ms, bind +165 ms; the loaded-chapter line +210 ms). The near tier
is built whatever the reach, so far and extreme build alike.

**Whole city at 512 m cells, an estimate.** The 14 dressed kinds place 12,529 of the 54,194
buildings (23 %, `../city-hulls/FINDINGS.md`). Scaled by buildings, the whole city would hold about 23,500 near
nodes, and the node-driven `proc_ms` rise would reach about 0.2 to 0.4 ms at every view, so the
CPU cost at near would sit close to the 0.5 ms line and extreme would likely pass it. GPU is set
by the dressed share of the screen, not by the city's size, and stays near the C5-cb1 figure if
the small-building districts cost alike per pixel, which is unmeasured. The far tier lowers the
distant draws, while the near tier's draws at C5-alt grow with every dressed kind in range, so
veryfar's margin of 35 draws is likely to close. The near cell size (`CityModels.CellM`) and a cap
on the city's near reach are the levers.

### The near cell size and the Extreme cap

`CityModels.CellM` was measured at 512, 1024 and 2048 m. Each value's near, veryfar and extreme
cases were interleaved with the city off in one batch of 150 launches at the five C5 views, n=3.
The cell size is a constant; the batch set it per launch through a temporary hook that the build
does not carry. The near node count is 5,438, 1,877 and 768. Frames at C5-cb1 and C5-street are
pixel-identical across the three, because the shader hands over per building and a cell's
visibility range is the band's far edge plus its radius. At C5-alt, C5-cm23 and C5-cb7 a few
pixels shift (at most 596 at 2048 m, largest delta 12, on the harbour crane's truss), and the
golden pose `c5-city-night-enhanced` moves by 2 px of delta 1.

| CellM | near nodes | mean `proc_ms` added | worst `render_cpu_ms` added (C5-cb1, extreme) | C5-alt draws near / veryfar / extreme (off 1,775.5) | prims at C5-cb1 veryfar | startup added |
|---|---:|---|---|---|---:|---|
| 512 | 5,438 | +0.059 | +0.17 | 1,708.7 / 1,740.7 / 1,829.7 | 767k | +240 ms |
| 1024 | 1,877 | +0.026 | +0.09 | 1,708.7 / 1,761.7 / 1,777.7 | 832k | +111 ms |
| 2048 | 768 | +0.013 | +0.035 | 1,709.5 / 1,738.5 / 1,776.5 | 1,167k | +48 ms |

**`proc_ms` follows the near node count, not what is drawn**, about 0.006 ms plus 0.0098 ms per
1,000 near nodes. With the node count fixed at 5,438, a band that draws no near model and one that
draws every near model add the same `proc_ms` (+0.04 to +0.09 ms) while `render_cpu_ms` moves by
0.69 ms. Which part of Godot's process step walks the static nodes is not identified.

No cell size alone holds the draw rule at extreme. With the city's band stopped at veryfar's and
2048 m cells every word holds the rule at C5-alt: 1,709.5, 1,738.5 and 1,738.5, the capped extreme
reading veryfar's draws and primitives exactly. **The build does not carry that cap**: the user
chose Extreme's own reach, so extreme draws 1,776.5 at C5-alt, one over the city off, until the
whole city is measured. Larger
cells submit more off-band stamps (prims +27 to 52 % at veryfar), which costs up to 0.06 ms GPU at
C5-street and nothing measurable at C5-cb1. At 1024 m with the cap both rules also hold, with
fewer added prims (+8 to 14 %), twice the CPU and a veryfar margin of 14 draws against 37.

**Extreme is cheaper than near at C5-cb1 because of the dither band.** At C5-cb1 the GPU added
reads -0.16 ms with boxes only, -0.07 with a hard hand-over at 400 m, +0.25 with the 400 to 600 m
band, +0.11 at uncapped extreme and +0.225 with near models only. Inside the band the box and the
near model both draw each building, each discarding half, so the band at near costs about 0.32 ms.
At extreme the band covers less of the frame. A narrower band is the GPU lever and makes the
hand-over pop more.

**Whole city at 2048 m cells with the cap, an estimate.** Scaled by buildings: at most about 3,300
near nodes, about 0.04 ms `proc_ms`, and about 0.1 ms of CPU at a dressed view. Whether the 37-draw
margin at C5-alt holds for the whole city is unmeasured.

### The window filter

Windows under 4 px average nine taps of the window ids (`FilterPx`, `FilterTaps`), which removes
their flicker under TAA. Interleaved with the unfiltered look in one batch, n=3: the near city
adds 0.51 ms GPU at C5-cb1 (0.25 unfiltered) and 0.165 ms at C5-street (0.075 unfiltered). The
GPU figures in the tables above are the unfiltered look.

The Steam Deck, and the vertex cost there of the off-band stamps a large cell submits, are
unmeasured.

## Reproducing

    $env:CSVM_DATA_ROOT = "Z:\CSVM"
    .\analysis\veg-budget\measure.ps1 -Chapters C5-cb7,C5-cb1,C5-alt,C5-cm23 -Modes enhanced,vdnormal,vdunlimited -Launches 3
    .\analysis\veg-budget\measure.ps1 -Chapters C5-cb1,C5-street,C5-alt,C5-cm23,C5-cb7 -Modes nomodels,near,far,extreme,original -Launches 3
    .\analysis\veg-budget\measure.ps1 -Chapters C5-alt,C5-cm23 -Modes nomodels,far,veryfar,extreme -Launches 3

From PowerShell 7 the comma lists go through `powershell -NoProfile -Command "& '<path>' ..."`.

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
