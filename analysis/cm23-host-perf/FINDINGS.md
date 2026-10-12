# CM23 as a network co-op host: where the frame goes

The question (GitHub #57): CM23 (C5/M03, "The Criminal Exodus", `cm_sequence` seq 22) was reported
at about 30 fps on the author's PC while hosting a two-player network co-op session under Enhanced.
Which side owns the frame: the render, the script, physics, or the host's replication of the guest?

## Verdict

**Not reproduced.** On the author's PC at the author's own display settings, CM23 holds the 120 Hz
present floor (8.3 to 9.5 ms median `frame_ms`) in every case: 1P, 2P network host and 2P
splitscreen, under Enhanced and Original. The network host reads the same as 1P within the
run-to-run spread; its replication cost is at most about 1 ms of main-thread time a frame (`proc_ms`
plus `phys_tick_ms`), and the sim clock holds 60 Hz in every window. No case's median comes within a
factor of three of a 33 ms frame, and no single window of any run reads over 14.4 ms.

The build before the C5 clutter-cell change (`8165a0983`, which landed three days after the report)
reads 12.5 to 14.4 ms in the same flight, GPU-bound on 5.6 M primitives a frame, with three to ten
times the hitches. That is slower and stutterier, and still not 30 fps; the host again reads the
same as 1P. What made the playtest read 30 fps is not in any variable this rig can split. Candidates
it cannot see: the guest running on the same PC and GPU, a later and busier part of the mission
than the opening minute measured here, and the `--perf` spyglass census synchronising with the
render thread every frame, which cost 8 to 9 ms of a two-pane Enhanced frame until `e66d2d30a`.

## Method

`measure.ps1` seeds a profile store under `.scratch\cm23-host-perf\` (schema version 3,
`missionsCompleted` 22, so `--campaign=cm23:22` flies the real mission with its 48 objectives and
roster) and launches every case through `RunProbe.ps1` at `-Resolution 5120x1440`:

```
--campaign=cm23:22 --profiles=<store> --graphics=enhanced|original --no-det --perf --no-vsync --mute
  --frames=7200 --screenshot=<png> [--hold=0.05,0,0,1]
1P:     (nothing more)
split:  --players=2 --plane=player_bhawk,player_bhawk
net:    --net-port-base=48960 --net-host=127.0.0.1:48960
        guest: --headless, its own store, --net-join=127.0.0.1:48960, the same --hold
```

`--no-det` keeps the saved display options, so every run used the author's own: Enhanced at
5120x1440 windowed (5120x1421 client), TAA, Ultra shadows, render scale 100 %, far view distance
(`[world] graphics mode:` line). The guest is headless so it draws nothing on the GPU the host is
measured on; it flew its seat for the whole host window in every run (its own `[perf] rate` lines
run past the host's quit). `summarise.ps1` takes the median of each `[perf] window` term after the
first ten windows (about five seconds), per launch. `ai_planes` reads 17 from the first window, so
no window is intro film (PERF-38).

Two flights: a slight climb at full throttle (`--hold=0.05,0,0,1`, light views, mostly sky and
horizon over the city), and hands-off level flight (heavier views, low over Manhattan). In the
hands-off flight P1 of a two-seat field spawns where it flies into `clutter_bld_-11_-13` after
about 30 s and then spectates the other seat, so that table stops at sim frame 3600 (`-Until 3600`).

Limits: one PC (RTX 5080 on Vulkan, 7800X3D, 120 Hz display), one mission, the opening minute of
it. The hidden-desktop window cannot present faster than the display's 120 Hz even with
`max_fps=0`, so `frame_ms` floors at 8.33 ms and the main thread's wait for that sits in
`defer_ms`; the cost below the floor is read off `proc_ms`, `phys_tick_ms` and `gpu_ms`. Other
agents' engine shards ran on the machine during several launches, which moves the CPU terms by a
few tenths of a millisecond. In splitscreen `gpu_ms` and `render_cpu_ms` read the empty root
viewport (PERF-39), shown in brackets.

## Raw tables

### Current build (`700a8f5d3`), climbing flight, three launches a case

| case | n | frame_ms | p95_ms | proc_ms | phys_tick_ms | phys_hz | defer_ms | render_cpu_ms | gpu_ms | draws | hitches (worst ms) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1P Enhanced | 0 | 8.35 | 8.33 | 2.00 | 2.64 | 60.0 | 4.80 | 0.57 | 2.45 | 611 | 9 (73) |
| 1P Enhanced | 1 | 8.48 | 9.09 | 2.63 | 3.42 | 60.0 | 3.74 | 0.67 | 2.45 | 606 | 3 (75) |
| 1P Enhanced | 2 | 8.33 | 8.33 | 1.94 | 2.48 | 60.0 | 4.96 | 0.46 | 2.44 | 605 | 4 (74) |
| net host Enhanced | 0 | 8.44 | 8.34 | 2.40 | 3.29 | 60.0 | 4.14 | 0.78 | 2.51 | 820 | 5 (80) |
| net host Enhanced | 1 | 8.34 | 8.33 | 2.16 | 2.66 | 60.0 | 4.62 | 0.72 | 2.45 | 815 | 7 (77) |
| net host Enhanced | 2 | 8.33 | 8.33 | 2.17 | 2.68 | 60.0 | 4.63 | 0.64 | 2.44 | 540 | 3 (49) |
| split Enhanced | 0 | 8.87 | 9.50 | 3.51 | 3.87 | 60.0 | 2.56 | (0.08) | (0.06) | 1,037 | 8 (82) |
| split Enhanced | 1 | 8.60 | 9.09 | 2.97 | 3.52 | 60.0 | 3.58 | (0.06) | (0.06) | 1,041 | 12 (108) |
| split Enhanced | 2 | 8.35 | 8.33 | 2.57 | 2.76 | 60.0 | 4.22 | (0.05) | (0.06) | 1,032 | 6 (88) |
| 1P Original | 0 | 8.46 | 9.09 | 2.14 | 3.01 | 60.0 | 4.59 | 0.85 | 2.95 | 993 | 7 (166) |
| 1P Original | 1 | 8.41 | 8.82 | 2.09 | 2.94 | 60.0 | 4.63 | 0.93 | 2.94 | 994 | 13 (110) |
| 1P Original | 2 | 8.50 | 9.09 | 2.46 | 3.21 | 60.0 | 4.10 | 0.84 | 2.95 | 992 | 7 (132) |
| net host Original | 0 | 8.76 | 9.09 | 2.94 | 3.72 | 60.0 | 3.41 | 1.39 | 2.94 | 977 | 8 (160) |
| net host Original | 1 | 8.64 | 9.09 | 2.91 | 3.75 | 59.9 | 3.26 | 1.38 | 2.97 | 976 | 11 (140) |
| net host Original | 2 | 8.49 | 9.09 | 2.56 | 3.52 | 60.0 | 4.00 | 1.24 | 3.00 | 989 | 11 (118) |
| split Original | 0 | 8.80 | 9.09 | 2.67 | 3.09 | 60.0 | 4.11 | (0.07) | (0.06) | 1,936 | 8 (110) |
| split Original | 1 | 8.82 | 9.09 | 2.86 | 3.11 | 60.0 | 3.89 | (0.08) | (0.06) | 1,932 | 8 (79) |
| split Original | 2 | 8.70 | 9.09 | 2.56 | 3.00 | 59.9 | 4.25 | (0.07) | (0.06) | 1,931 | 10 (66) |

The lowest `phys_hz` of any single window in any run is 58.2 Hz: the sim clock keeps pace with the
wall clock throughout. Net host against 1P: Enhanced +0.1 ms `proc_ms` and +0.1 ms `phys_tick_ms`,
Original +0.6 and +0.6 ms, both near the spread of three launches of one case.

### Current build, hands-off level flight, Enhanced, sim frames up to 3600, two launches a case

| case | n | frame_ms | p95_ms | proc_ms | phys_tick_ms | phys_hz | render_cpu_ms | gpu_ms | draws |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1P | 0 | 8.59 | 9.09 | 2.59 | 3.68 | 60.0 | 1.81 | 6.70 | 2,267 |
| 1P | 1 | 8.35 | 8.33 | 1.94 | 2.87 | 60.0 | 1.37 | 6.66 | 2,280 |
| net host | 0 | 8.35 | 8.33 | 2.13 | 3.26 | 60.0 | 1.43 | 6.67 | 2,269 |
| net host | 1 | 8.74 | 9.09 | 2.98 | 4.04 | 60.0 | 1.69 | 7.90 | 2,213 |
| split | 0 | 9.43 | 9.86 | 2.48 | 3.07 | 60.0 | (0.05) | (0.06) | 3,329 |
| split | 1 | 9.51 | 10.41 | 2.69 | 3.30 | 60.0 | (0.06) | (0.06) | 3,246 |

Over the whole minute of the hands-off runs, `gpu_ms` climbs from about 6 to 12.9 ms as the flight
reaches the city; the heaviest window of any run reads 14.4 ms `frame_ms` (net host) and 13.5 ms (1P).

### Before the clutter-cell change (`8165a0983^`), hands-off level flight, Enhanced

The same scripts and probe scripts run from an export of that commit, built with zero warnings.
That build has no `defer_ms`/`idle_ms` terms, so its `frame_ms` is the whole frame.

| case | n | frame_ms | p95_ms | proc_ms | phys_tick_ms | phys_hz | render_cpu_ms | gpu_ms | draws | prims | hitches (worst ms) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1P | 0 | 13.49 | 15.23 | 3.10 | 5.14 | 60.1 | 2.06 | 10.52 | 2,784 | 5.6 M | 36 (128) |
| 1P | 1 | 14.40 | 17.59 | 3.30 | 5.39 | 60.0 | 2.22 | 10.32 | 2,502 | 5.6 M | 41 (131) |
| net host | 0 | 12.55 | 13.94 | 3.04 | 5.08 | 60.0 | 1.58 | 9.33 | 2,346 | 5.6 M | 30 (135) |
| net host | 1 | 13.13 | 15.29 | 3.25 | 5.14 | 60.0 | 1.87 | 9.81 | 2,321 | 5.6 M | 66 (129) |

Against the current build's 0.6 to 0.8 M primitives in the same flight.

## Next step

A re-fly at the controls on the current build: host CM23 in network co-op under Enhanced with
`--perf`, the guest on its own machine, and keep the session's log (`.scratch/logs/` in a repo run,
`logs\` beside an export). If it reads under 60 fps, the `[perf] window` lines at that moment name
the side, and the next measurement repeats this method at that point of the mission.