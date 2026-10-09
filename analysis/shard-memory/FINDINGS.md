# What an engine-test shard and a capture hold in memory

**Question** (GitHub #159): an engine-test shard was seen at about 4 GB and an XR capture at about
8 GB private. Does a shard grow with the suites it runs or plateau early, how much of a capture is
render targets, the Enhanced path and the world, and which holder (managed, Godot static, texture,
buffer, other native) is the bulk?

**Answer.** A shard grows with the suites it runs, in steps, and never returns: the first world
build, every new shader the process compiles, a graphics-mode switch, and the network suites that
run two to four sessions in one process each add to it. Measured on the current catalog the six
shards peaked at 6.7 to 13.4 GB, far above the 4 GB in the issue. The dominant holder in every
shard and every Enhanced capture is **native memory no Godot monitor counts**: private bytes minus
the managed heap, Godot's static allocations, textures and buffers. It tracks the number of
shaders the process has compiled (about 10 MB each, an upper bound) and jumps by about 25 MB per
live shader the first time a TAA frame draws, when Godot builds every shader's advanced variants.
Managed memory never exceeds 1.1 GB, and Godot's static allocations reach 2.9 GB only transiently
inside the network suites.

Two holders were localized. Dead C# wrappers kept their Godot objects' native memory across
suites until the GC happened to finalize them; the harness now collects and finalizes after a suite
that grew Godot's heap, which cuts the six shards' summed peak from 48 to 50 GB down to 42 GB. The
shader cache also keeps every material any suite's world made, which accounts for about 3 GB more,
but releasing those materials is unsafe while a cached world crosses suites, which it does in
every shard; that release and the rest of the shader cache's memory are filed as a new issue.

## Instrument

`--debug-mem` (`CSVM/src/Utils/MemoryCensus.cs`, `docs/cli.md`). One line per suite under
`--run-tests`, one per wall second otherwise, and one at exit:

- `priv_mb`, `peak_priv_mb`: `Process.PrivateMemorySize64` and `PeakPagedMemorySize64` (the
  process's peak commit charge, the figure a memory ledger reserves against).
- `gc_mb`, `gc_committed_mb`: `GC.GetTotalMemory(false)` and the GC's committed bytes.
- `static_mb`: Godot `MEMORY_STATIC`. `tex_mb`, `buf_mb`, `vid_mb`: `RENDER_TEXTURE_MEM_USED`,
  `RENDER_BUFFER_MEM_USED`, `RENDER_VIDEO_MEM_USED`.
- `pipelines`: the sum of Godot's five `PIPELINE_COMPILATIONS_*` monitors. `shaders` (harness
  only): `ShaderTwins.Made`, the shader cache's count of compiled texts.
- "Other native" in the tables is `priv - gc_committed - static - tex - buf`.

Every launch went through `RunProbe.ps1` on the hidden desktop, one Godot at a time, with the
shard's own `--run-tests=shard:k/6` term and a net port slot held for the run. Machine: RTX 5080,
Vulkan 1.4.351, Forward+, separate render thread, 63 GB RAM. MB is 2^20 bytes and GB is 2^30.

⚠ Under `--run-tests` the renderer's texture and buffer monitors stay at 0 until some suite forces
a drawn frame, because the whole run happens inside one `_Ready`. A world's GPU textures and meshes
are therefore inside "other native" in the shard tables until the first suite that draws.

⚠ On this driver, GPU memory the renderer allocates is charged to the process's private bytes
roughly one to one: the Enhanced capture at 5120x1440 held 1.76 GB more `tex_mb` than at 1280x720
and 2.05 GB more private bytes. A ledger reading private bytes therefore sees render targets.

## A shard against the suites it has run

Shard 1 of 6 before the fix (the shard holding `graphics-retext-compiles`):

| after suite | suite | private MB | peak MB | managed MB | static MB | tex MB | buf MB | shaders |
|---|---|---|---|---|---|---|---|---|
| 0 | (harness start) | 812 | 812 | 39 | 65 | 0 | 0 | |
| 1 | ai-wave-launch-hitch | 2279 | 2279 | 473 | 453 | 0 | 0 | 72 |
| 7 | campaign-balmoral-hidden | 2729 | 2729 | 431 | 521 | 0 | 0 | 108 |
| 15 | campaign-coop-episode-owner | 3550 | 3643 | 500 | 606 | 0 | 0 | 121 |
| 22 | campaign-surface-vehicles | 3590 | 3921 | 537 | 661 | 0 | 0 | 131 |
| 36 | generator-roster-params | 3666 | 3921 | 652 | 706 | 0 | 0 | 131 |
| 37 | graphics-retext-compiles | 9183 | 9183 | 655 | 834 | 388 | 133 | 220 |
| 38 | graphics-shader-twins | 9353 | 9491 | 469 | 847 | 388 | 133 | 227 |
| 50 | menu-original-screen-keyboard | 9468 | 9762 | 573 | 760 | 388 | 133 | 229 |
| 56 | net-crash-respawn | 10975 | 10975 | 891 | 1796 | 388 | 133 | 240 |
| 59 | net-lobby-stunt-race | 11504 | 11764 | 471 | 1115 | 388 | 133 | 278 |
| 64 | net-swap-rewire | 10778 | 11764 | 490 | 1173 | 388 | 133 | 278 |
| 80 | stunt-respawn-tap-hold | 11129 | 11764 | 841 | 1077 | 388 | 133 | 281 |
| 92 | zeppelin-motion | 10840 | 11764 | 507 | 1164 | 388 | 133 | 281 |

Shard 5 of 6, the tree before the fix (run twice: 6.75 and 6.81 GB peaks), then with the material
release alone:

| after suite | suite | before: private MB | before: static MB | release: private MB | release: static MB | shaders |
|---|---|---|---|---|---|---|
| 0 | (harness start) | 826 | 65 | 808 | 65 | |
| 1 | ai-crash-rig-deferral | 1749 | 224 | 1658 | 222 | 76 |
| 10 | called-death-chain | 2343 | 304 | 2031 | 329 | 86 |
| 20 | campaign-persistence | 3386 | 567 | 2887 | 395 | 138 |
| 30 | dogfight-display-scores | 3699 | 509 | 3109 | 344 | 150 |
| 50 | menu-original-controls | 4266 | 574 | 3237 | 352 | 152 |
| 60 | net-flight-chat | 5794 | 1674 | 4760 | 1439 | 157 |
| 61 | net-player-voice | 6210 | 1951 | 5419 | 1705 | 157 |
| 63 | ocean-lab | 6273 | 651 | 5605 | 568 | 256 |
| 80 | stunt-target-cycle | 5671 | 539 | 5245 | 441 | 258 |
| 92 | zeppelin-pandora-dead-end | 6286 | 688 | 5453 | 605 | 260 |

The release is not kept (see below). With the landed fix, the collection alone, shard 5 peaks at
5.90 GB; with the release and the collection together it peaked at 5.11 GB.

Shard 2 of 6 shows the same shape: 1.8 GB after its first world, 3.9 GB by suite 38, +1.45 GB at
`graphics-live-switch` (the shader count 154 to 245), and 6.6 to 7.4 GB through the network block.

The steps, each reproduced across shards:

- **First world build**: 0.8 GB at harness start to 1.7 to 2.3 GB after the first suite that
  builds a world (the decode store, the world, its first 60 to 76 shaders).
- **Every new shader**: plateaus hold while the shader count holds and rise when it rises. At shard
  end "other native" is 2.9 to 4.6 GB for 258 to 286 shaders, about 10 to 16 MB a shader, an upper
  bound since it also carries the cached chapter world's GPU data the monitors cannot see.
- **A graphics-mode switch**: a suite that makes the other mode's twin of every cache key roughly
  doubles the shader count (`graphics-live-switch` +1.45 GB, `ocean-lab` +0.1 to 0.7 GB,
  `graphics-retext-compiles` below).
- **The first TAA frame**: `graphics-retext-compiles` draws one TAA frame to have Godot build its
  advanced shader variants, which Godot does for every live shader. Split by step on a five-suite
  run: the rewrite took private bytes from 2.40 to 2.45 GB, the TAA frame from 2.45 to 5.89 GB with
  136 shaders alive (0.24 GB of it textures and buffers), and restoring Original made 73 more
  shaders, each now with its advanced variants, to 7.52 GB. With the cached world released first
  the TAA frame still cost 3.4 GB and compiled no pipeline, so the cost is per shader, about 25 MB
  each. Alone in its process the suite costs 0.3 GB.
- **Network suites**: two to four `GameSession`s in one process raise static to 1.4 to 2.9 GB.
  Before the fix the next suite started while the last one's wrappers were unfinalized, so static
  carried between suites and the peak varied from run to run (shard 1: 11.5 and 13.4 GB; shard 2
  with the material release alone peaked at 7.9 GB inside its network block, above its 7.2 GB baseline).

## The harness fix, the release that was withdrawn, and what each bought

1. **Collect and finalize after a suite that grew Godot's heap** (landed). A Godot object a dead C#
   wrapper references is freed only when the wrapper is finalized, so a network suite's sessions
   rode into the next suites and set each shard's peak. The harness now runs `GC.Collect()` and
   `GC.WaitForPendingFinalizers()` at a suite's end when Godot's `MEMORY_STATIC` grew by more than
   32 MB over that suite (`TestHarness.CollectAfterStaticGrowth`), skipped under
   `--debug-finalizers`, whose parked finalizer thread would never finish the wait.
2. **`ShaderTwins.ReleaseUnused()` between suites** (withdrawn). The shader cache's `Tracked` table
   holds every material a cache shader goes on until a `GameSession` build calls `ReleaseUnused`,
   and a suite's `WithWorld` builds a `WorldSession`, so every material any world in the shard made
   stays tracked with its textures. Releasing them between suites is unsafe: `ReleaseUnused` drops
   every tracked material with a reference count of 1, which includes a cached world's unworn fade
   twins held only from C#, so a later graphics-switch suite would leave them on the old mode's
   shader. Its contract is "when the last world is gone", and a cached chapter world crosses every
   suite boundary in every shard once the first suite has built it: guarded by "no cached world",
   the release fired 0 times in shard 5. It bought about 3 GB across the six shards; a safe form is
   in the new issue.

| shard | peak before (GB) | release + collect every suite (withdrawn) | landed: collect after heap growth | final before | final landed |
|---|---|---|---|---|---|
| 1/6 | 11.49 and 13.43 (two runs) | 10.60 | 11.14 | 10.59 | 10.81 |
| 2/6 | 7.19 | 6.15 | 6.34 | 6.21 | 5.84 |
| 3/6 | 8.36 | 6.27 | 6.54 | 6.97 | 5.68 |
| 4/6 | 7.54 | 5.38 | 5.98 | 5.72 | 5.07 |
| 5/6 | 6.75 and 6.81 (two runs) | 5.11 | 5.90 | 6.14 | 5.41 |
| 6/6 | 6.73 | 5.30 | 5.97 | 5.82 | 5.55 |
| sum of peaks | 48.1 to 50.1 | 38.8 | 41.9 | | |

All six shards pass with the landed fix (553 suites, the two skips the baseline also had, engine
errors clean), and so do the graphics and fade suites (`-Filter graphics`, `-Filter fade`).

**What the collection costs**, shard 5, back to back, one Godot at a time (machine quiet for the
first pair; another session's Godot started during the second pair):

| collection | peak private GB | shard wall (s) | collections | time inside them (s) |
|---|---|---|---|---|
| none | 6.88 / 6.99 | 189.3 / 182.4 | 0 | 0 |
| after every suite | 5.82 / 5.91 | 216.4 / 219.0 | 92 | 13.9 (mean 0.15, max 0.62) |
| after a suite that grew Godot's heap by 32 MB (landed) | 6.07 / 5.90 | 201.3 / 185.7 | 30 | 4.8 |

Each collection lands in its suite's rest time and so in that suite's weight; the landed gate puts
it on the 30 suites that build sessions or worlds, not on the 62 that do not. Collecting after every
suite bought another 0.2 GB of peak for three times the time.

Per-holder figures across a shard, before and with the landed fix (MB):

| shard | max managed heap | max GC committed | max static | tex at end | buf at end | other native at end |
|---|---|---|---|---|---|---|
| 1/6 before | 891 | 1179 | 1796 | 388 | 133 | 8412 |
| 1/6 landed | 903 | 1075 | 1148 | 388 | 126 | 8523 |
| 3/6 before | 1132 | 1606 | 2869 | 510 | 140 | 4097 |
| 3/6 landed | 944 | 1209 | 1288 | 332 | 138 | 3612 |
| 5/6 before | 1105 | 1273 | 1953 | 0 | 0 | 4633 |
| 5/6 landed | 896 | 1074 | 879 | 0 | 0 | 4199 |

What remains is the shader cache: its process-lifetime compiled variants, the materials it keeps
tracked under a cached world, and in shard 1 the TAA frame's advanced variants on top (shard 1 sits
about 5 GB above the others for that alone). The variants are process-lifetime by design in the
game, where the cache saves a compile hitch on a switch or a chapter revisit; bounding them in a
test process is a change to `ShaderTwins` or to where that suite runs, filed separately.
## Captures: render targets, Enhanced, the world

C1, `player_bhawk`, cockpit view, `--det --perf --frames=420 --no-soft-shadows`, one sample a
second plus one at exit. Every capture is flat after its first drawn frame; nothing grows over the
run (a 60,000-frame run without `--screenshot` held 7.15 to 7.35 GB throughout).

| launch | pixels drawn | peak private GB | managed MB | static MB | tex MB | buf MB | pipelines | other native MB |
|---|---|---|---|---|---|---|---|---|
| any, before the first frame | | 2.1 to 2.2 | 220 | 340 to 380 | 0 | 0 | 0 | about 1500 |
| Original, 1280x720 | 0.9 M | 2.77 | 209 | 386 | 224 | 78 | 327 | 1750 |
| Original, 4224x2304 | 9.7 M | 3.49 | 225 | 356 | 1094 | 188 | 328 | 1630 |
| Enhanced, 1280x720 | 0.9 M | 5.18 | 232 | 437 | 475 | 95 | 568 | 4080 |
| Enhanced, 4224x2304 | 9.7 M | 7.39 | 234 | 450 | 1882 | 205 | 575 | 4300 |
| Enhanced, 5120x1440 window (no `--screenshot`) | 7.4 M | 7.51 | 249 | 504 | 2236 | 237 | 611 | 4180 |

- **The world and runtime**: about 2.1 GB before anything draws, of which 0.2 GB is managed and
  0.4 GB Godot static; the rest is the engine, the .NET runtime, the driver and the world's GPU data.
- **The Enhanced path**: 2.4 GB over Original at the same size, almost none of it textures. It is
  the Enhanced shader set with its advanced variants (568 pipelines against 327), in "other native".
- **Render targets**: Enhanced costs about 0.25 GB private per million pixels drawn, Original about
  0.08. Two 2112x2304 eyes are 9.7 M pixels, the 4224x2304 rows.
- **The issue's 8.06 GB XR capture** decomposes as about 2.1 GB world and runtime, 2.4 GB of
  Enhanced shaders, and 2.3 to 3 GB of render targets for the two eyes and the desktop window. The
  `--xr-sim` flag exists only on the `vr` branch, so the 4224x2304 Enhanced row (7.39 GB) is the
  measured stand-in: the same pixel count, one viewport instead of two.

## Seed estimates for a memory ledger

Measured peak private bytes per launch kind, engine shards with the landed fix. The recommended
seed is keyed by `MemoryLedger.ps1`'s `$MemSeedGB` names: the largest peak the key can launch,
rounded up to the next half GB, so the first launches of a kind are not under-reserved before the
learned history (highest of the last 20, plus 25 percent) takes over.

| kind | launch measured | peak private GB | recommended seed (GB) |
|---|---|---|---|
| engine shard, the one holding `graphics-retext-compiles` | `--run-tests=shard:1/6` | 11.1 | `engine-shard` 11.5 |
| engine shard, the other five | `--run-tests=shard:k/6` | 5.9 to 6.5 | (covered by `engine-shard`) |
| engine shard, before the fix | | 6.7 to 13.4 | |
| golden shot, Original | `viewer-bhawk`, `c2-city`, `c5-city-night`, `c1-flight-kill` | 1.5 / 1.7 / 2.1 / 2.4 | `golden-shot` 5.5 |
| golden shot, Enhanced | `c5-city-night-enhanced`, `c1-cockpit-enhanced` | 4.6 / 5.2 | (covered by `golden-shot`) |
| perf scenario, Original | `c2m02-hollywood`, `c5-city`, 300 frames | 2.3 / 2.1 | `perf` 5.5 |
| perf scenario, Enhanced (`-Graphics enhanced`) | `c2m02-hollywood` | 5.3 | (covered by `perf`) |
| hitch | the clean run, 180 frames; `-Graphics enhanced` not measured, the Enhanced perf run stands in | 2.3 | `hitch` 5.5 |
| probe/capture, Original | cockpit capture, 420 frames, 720p / 4224x2304 | 2.8 / 3.5 | `probe` 3.5 |
| enhanced capture | the same at 720p / 5120x1440 | 5.2 / 7.5 | `capture-enhanced` 8.0 |
| XR capture | Enhanced at the two eyes' pixel count (stand-in) / the issue's own measurement | 7.4 / 8.1 | `capture-xr` 8.5 |

A ledger that would rather not hold 11.5 GB for each of the five lighter shards needs shard 1 as a
kind of its own (6.5 GB for the others); with one key, the seed has to cover shard 1.
Not measured: the four-player golden shots (`campaign-4p-grid`, `campaign-intro-fill`), which need
a profile store, and an engine run of the whole catalog in one process (`-Shards 1`), which would
carry every shard's shaders at once.
