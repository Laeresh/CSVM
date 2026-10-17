# C5 splitscreen on the Steam Deck under Enhanced: where the frame goes and what each lever saves

The question: can New York (C5) splitscreen under Enhanced hold 60 fps on the Steam Deck at two and
four panes, and which lever gets it there without changing what the spyglass disc shows?

Every claim below carries one label. [measured] means a number read from a run. [inferred] means a
conclusion drawn from measured numbers. [assumed] means something taken without a check.

## Answer

- No lever or combination brings four panes to 60 on this flight. The best is no disc at all with
  view distance Normal, which reads 52.7 fps. [measured]
- At two panes the disc on every 3rd frame alone clears a 60 fps mean (60.7), and so do several
  combinations. None of them holds 60 in the densest windows, where the best reads 52.5 (table
  below). [measured]
- Every lever that reaches a 60 fps mean visibly changes the disc or the panes in the crops, except
  the fewer-frames levers, whose look only a check at the controls can judge. [inferred]
- The disc's own TAA, SSAO, SSR and glow passes cost 1.6 ms of the four-pane frame together (31.6
  fps against 30.0) and 0.7 ms at two panes. [measured]
- The largest disc-only levers are fewer frames and draw cuts:
  - The disc on every 3rd frame saves 8.4 ms at four panes. [measured]
  - Leaving the clutter cells out of the disc saves 4.9 ms. [measured]
  - The second visibly changes the disc's lit city texture. [measured, crops]
  - The first holds each disc picture for three frames, a change no still shows. [inferred]

## Method

- Hardware and install [measured]: a Steam Deck LCD at 1280x800 borderless, running a Linux export
  of the measured tree (`ExportRelease.ps1 -Linux`) unpacked into `~/CSVM-tmp/i124/game`.
- User data [measured]: `CSVM_DATA_ROOT=/home/deck/CSVM`, and an isolated `XDG_DATA_HOME` seeded
  with `options.json` (render scale 67, TAA, Enhanced).
  - Nothing is saved for shadow or water quality, so the integrated-GPU fallbacks apply: shadows
    High at two panes, Off at four, water flat.
  - The log confirms `shadow_quality=off shadow_quality_source=default_integrated_gpu_panes` at
    four panes.
- The flight (`run.sh`): `--no-det --no-pads --no-vsync --chapter=C5 --vs --players=N
  --target=nearest --view=back --pos=-9256,300,-3155 --direction=0.588,0.07,-0.809
  --graphics=enhanced`.
  - The pilots fly abreast 60 m apart with no input. Each pane selects its neighbour, which sits
    off screen behind the look-back camera, so every pane's disc stays up for the whole run. A PC
    `--perf` probe of the same flight read `discs=4.00` in every window for 3600 frames. [measured]
  - The disc looks sideways at a neighbour 60 m away, over the city. A real target 200 m to 2 km
    out frames more distant city. So the disc's draw counts here are one case, not the worst case.
    [inferred]
  - The path climbs gently over the densest part of Manhattan, and the rate falls along it in
    every arm. Compare arms window by window or by mean, never against another path. [measured]
  - The issue's 57.7 and 32.8 came from a different, lighter path. This path's base reads 49.0 and
    30.0. [measured]
- The rate [measured]: the game's own `[perf] rate` windows (10 wall seconds each), read from its
  log file.
  - The first window after `live respawn` is the load stall, about 7 s of the first frames. It is
    dropped, and so is the next (settling) window.
  - Six windows (60 s) follow.
  - No compared arm runs `--perf`. The `--perf` arms read the disc counters and match the plain
    arms within 0.3 fps; their windows are not in the raw table. So `#123`'s fix holds on the Deck.
    [inferred]
- Disc-only levers go through a temporary environment switch `CSVM_I124`
  (`measurement-levers.patch`, not landed):
  - `nodisc`: the disc never renders.
  - `notaa`: the disc's TAA is off.
  - `nossao`, `nossr`, `noglow`: the disc camera gets a copy of the world Environment with that
    pass off.
  - `every2`/`every3`: the disc renders on one frame in two or three, with the panes staggered.
  - `noclutter`: every layer-1 `MultiMeshInstance3D` (the clutter cells, 5425 of them in C5) moves
    to the sun's layer, which every pane draws and no disc does.
  - `fogfar`: the disc camera's far plane is the pane's fog far (2250 m in C5) in place of the
    pane's 40000 m. A fraction of 40000 m changed no draw count on the PC, since the clutter's
    visibility ranges and the fog already end the city well short of it; the switch for that run
    is not in the patch. [measured]
  - `fogfar50`/`fogfar75`: with `fogfar`, half or three quarters of the fog far. Not measured.
  - `scale67`: forces render scale 67 even under `--det`, for the crops only.
  - Session-wide levers use the shipped flags `--no-ssr`, `--shadow-quality=` and
    `--view-distance=`.
- Builds [measured]:
  - Batches 1 and 2 ran a build of the patch without `noclutter`, `fogfar` and `scale67`.
  - Batch 3 ran a build with `noclutter` and `fogfar`.
  - The crops ran a build with `scale67`.
  - The levers the earlier batches used are unchanged in the final patch. [measured, by diff]
- Rounds: each arm ran twice, the order reversed in the second (`batch.sh`). The mean of the two
  rounds is given. Rounds differ by more than 0.5 fps on several arms, as the raw windows show; the
  two where a single hitch window explains it are named under their tables. [measured]
- The batches, exactly as launched (`launch.sh ROUNDS SECS ARMS`, with run.sh's 300 s cap):

```
batch 1: sh launch.sh 2 70 '4:base 4:nodisc 4:notaa 4:nossao 4:nossr 4:noglow 4:nossao+nossr+noglow 4:notaa+nossao+nossr+noglow 4:every2 4:every3 4:base:--no-ssr 2:base 2:nodisc 2:notaa+nossao+nossr+noglow 2:every2 2:base:--no-ssr 2:base:--shadow-quality=medium 2:base:--shadow-quality=low 2:base:--shadow-quality=off'
batch 2: sh launch.sh 2 70 '4:base:--perf 2:base:--perf 4:base 2:base 2:every3 2:every2:--shadow-quality=off 2:every3:--shadow-quality=off 2:nodisc:--shadow-quality=off 2:base:--view-distance=normal 4:base:--view-distance=normal 4:every3:--view-distance=normal 4:nodisc:--view-distance=normal'
batch 3: sh launch.sh 2 70 '4:warm 4:base 4:noclutter 4:fogfar 4:noclutter+fogfar 4:noclutter+every2 4:noclutter+every3 2:base 2:noclutter 2:fogfar 2:noclutter+fogfar 2:noclutter+every2 2:noclutter+every3 2:noclutter:--shadow-quality=off 2:noclutter+every2:--shadow-quality=off'
crops:   systemd-run --user --unit=i124shots --working-directory=/home/deck/CSVM-tmp/i124 sh shots.sh
```

  `warm` is a base run that soaks up a new build's pipeline compiles; its rate matches the base. [measured]

## Four panes (shadows Off by the integrated-GPU pane default) [measured]

Batches 1 and 2, base 30.0 fps (33.3 ms):

| Arm | fps (mean) | ms | Saving vs base | Densest window |
|---|---|---|---|---|
| base | 30.0 | 33.3 | | 23.8 |
| disc TAA off | 30.4 | 32.9 | 0.4 | 23.7 |
| disc SSAO off | 30.4 | 33.0 | 0.3 | 23.8 |
| disc SSR off | 30.5 | 32.8 | 0.5 | 23.9 |
| disc glow off | 30.2 | 33.2 | 0.1 | 23.5 |
| disc SSAO + SSR + glow off | 31.2 | 32.0 | 1.3 | 24.4 |
| disc TAA + SSAO + SSR + glow off | 31.6 | 31.7 | 1.6 | 24.6 |
| disc every 2nd frame | 36.9 | 27.1 | 6.2 | 28.5 |
| disc every 3rd frame | 40.2 | 24.9 | 8.4 | 31.0 |
| SSR off session-wide (`--no-ssr`) | 30.8 | 32.4 | 0.9 | 24.1 |
| view distance Normal | 33.0 | 30.3 | 3.0 | 26.5 |
| disc every 3rd frame + view distance Normal | 43.9 | 22.8 | 10.5 | 34.0 |
| no disc | 48.0 | 20.9 | 12.4 | 35.9 |
| no disc + view distance Normal | 52.7 | 19.0 | 14.3 | 39.5 |

Batch 3, base 29.8 fps (33.5 ms, four runs counting `warm`):

| Arm | fps (mean) | ms | Saving vs base | Densest window |
|---|---|---|---|---|
| disc without clutter | 35.0 | 28.6 | 4.9 | 28.1 |
| disc far plane at the fog far | 34.5 | 29.0 | 4.5 | 26.3 |
| disc without clutter + far at fog far | 39.6 | 25.3 | 8.2 | 31.4 |
| disc without clutter + every 2nd frame | 40.4 | 24.8 | 8.8 | 31.9 |
| disc without clutter + every 3rd frame | 42.5 | 23.5 | 10.0 | 32.9 |

## Two panes (shadows High by the integrated-GPU default) [measured]

Batches 1 and 2, base 49.0 fps (20.4 ms):

| Arm | fps (mean) | ms | Saving vs base | Densest window |
|---|---|---|---|---|
| base | 49.0 | 20.4 | | 41.0 |
| disc TAA + SSAO + SSR + glow off | 50.9 | 19.7 | 0.7 | 42.6 |
| SSR off session-wide | 50.8 | 19.7 | 0.7 | 42.2 |
| shadows Medium (round 2 alone) | 49.0 | 20.4 | 0.0 | 41.0 |
| shadows Low | 49.2 | 20.4 | 0.0 | 41.1 |
| shadows Off | 53.0 | 18.9 | 1.5 | 43.6 |
| view distance Normal | 53.9 | 18.5 | 1.9 | 44.9 |
| disc every 2nd frame | 57.4 | 17.4 | 3.0 | 46.3 |
| disc every 3rd frame | 60.7 | 16.5 | 3.9 | 48.8 |
| disc every 2nd frame + shadows Off | 61.6 | 16.2 | 4.2 | 49.2 |
| disc every 3rd frame + shadows Off | 66.2 | 15.1 | 5.3 | 52.5 |
| no disc | 69.5 | 14.4 | 6.0 | 54.6 |
| no disc + shadows Off | 74.3 | 13.5 | 6.9 | 58.3 |

Shadows Medium's first round carried one 44.7 hitch window; its second round reads 49.0, the base.

Batch 3, base 49.3 fps (20.3 ms):

| Arm | fps (mean) | ms | Saving vs base | Densest window |
|---|---|---|---|---|
| disc without clutter | 55.5 | 18.0 | 2.3 | 46.2 |
| disc far plane at the fog far | 54.4 | 18.4 | 1.9 | 43.5 |
| disc without clutter + shadows Off | 59.6 | 16.8 | 3.5 | 49.0 |
| disc without clutter + far at fog far | 60.1 | 16.6 | 3.7 | 48.2 |
| disc without clutter + every 2nd frame | 60.4 | 16.6 | 3.7 | 48.4 |
| disc without clutter + every 3rd frame | 63.1 | 15.8 | 4.5 | 50.8 |
| disc without clutter + every 2nd frame + shadows Off | 65.6 | 15.3 | 5.1 | 52.5 |

The "without clutter + every 2nd frame" row is round 2 alone. Round 1 read 57.1, carrying one 44.3
hitch window. The second round of each batch-3 base run kept five windows, not six.

## Combinations that clear a 60 fps mean at two panes [measured]

- Disc every 3rd frame: 60.7.
- Disc every 2nd frame + shadows Off: 61.6.
- Disc every 3rd frame + shadows Off: 66.2.
- Disc without clutter + far at fog far: 60.1.
- Disc without clutter + every 2nd frame: 60.4.
- Disc without clutter + every 3rd frame: 63.1.
- Disc without clutter + every 2nd frame + shadows Off: 65.6.

None holds 60 in the densest windows of this flight; the best of them reads 52.5. Only no disc with
shadows Off comes close, at 58.3. [measured]

## What the disc costs

- The disc counters, per frame with every disc up, from the `--perf` arms (not in the raw table)
  [measured]:

  | Panes | `disc_draws` | All draws | `disc_gpu_ms` |
  |---|---|---|---|
  | 4 | 3630 | 7406 | 6.0 |
  | 2 | 1551 | 4537 | 2.7 |

- Leaving the discs out saves 12.4 and 6.0 ms, about twice their GPU time. [measured]
- The main thread spends most of the four-pane frame in the end-of-frame flush, `defer_ms` 27.4 of
  33 ms. [measured]
- From those two readings: the frame waits on the render thread's draw submission, and the discs'
  share of it is their draw count. [inferred]
- A disc's screen passes run on a 96-pixel square and are a small remainder (the passes rows
  above). [measured]
- On the PC, at sim frames 300 and 540 of the two-pane flight [measured]:
  - `noclutter` cut the two discs' draws from 1280 to 564 and 674, and their primitives from 518 k
    and 399 k to 91 k.
  - `fogfar` cut the draws to 1028 and 926, and left the primitives within 2% of the base.
- So the clutter cells are about half of what a disc draws in C5, and nearly all of its triangles.
  [inferred]
- In a daylight chapter the clutter cells are the trees, houses and props of the landscape, so
  leaving them out of the disc would change it more than in C5 at night. [assumed, not measured]

## Crops

Taken on the Deck at the shipped 67% scale with TAA, through `scale67`. The game's `--det` drops a
saved render scale, which is why the lever exists. The `[world] graphics mode` line read
`render_scale=67` on every shot. [measured]

- The shots: `--det --shadow-quality=high` (two panes) or `off` (four panes), `--water-quality=flat`,
  frame 600.
- The sheets, both 4x nearest, beside the commit message and not committed:
  - `disc-2p-67pct-cols-base-passesoff-noclutter-fogfar.png`: both two-pane discs.
  - `disc-4p-67pct-cols-base-noclutter-fogfar.png`: all four four-pane discs.
- Changed pixels per disc against the shipped disc, of 3600 [measured]:

  | Lever | Pixels changed | Largest change (levels) |
  |---|---|---|
  | TAA, SSAO, SSR and glow off (two panes) | 573 and 731 | 77 and 111 |
  | clutter out of the disc | 250 to 449 | up to 75 |
  | far plane at the fog far | 608 to 670 | up to 191 |

- What each change looks like:
  - Passes off: the city's lit windows alias into hard specks and the horizon edge sharpens.
    [measured, crops]
  - Clutter out: the city floor loses most of its lit-window speckle and reads darker and flatter.
    [measured, crops]
  - Far at the fog far: the sky dome falls outside the far plane, so the disc's stars and sky turn
    black. [measured, crops]
- A disc rendered on fewer frames is identical in a still on the frames it renders, and holds each
  picture for two or three frames. Only a look at the controls can judge it. [inferred]

## Raw windows [measured]

Each run's six kept windows in order (fps), round by round. The base ran in every batch.

```
batch 1 and 2
4p base            36.7 34.5 31.8 27.6 27.6 24.5 | 33.3 33.4 31.2 28.3 26.9 23.6 | 36.1 34.0 31.3 28.4 27.0 23.6 | 36.2 33.9 31.4 28.4 27.0 23.4
4p nodisc          58.2 53.0 51.8 47.6 41.5 36.1 | 58.5 52.6 51.8 47.4 41.4 35.7
4p notaa           36.6 34.2 31.7 28.6 27.1 23.7 | 36.8 34.3 31.7 28.8 27.2 23.7
4p nossao          36.5 34.1 31.6 28.7 27.0 23.7 | 36.4 34.2 31.8 28.9 27.2 23.9
4p nossr           36.8 34.3 31.9 28.9 27.6 24.0 | 36.4 34.2 31.7 28.9 27.3 23.8
4p noglow          36.3 34.0 31.7 28.6 27.3 23.7 | 36.2 34.0 31.3 28.4 26.7 23.3
4p env all off     37.7 35.4 32.7 29.6 28.1 24.4 | 37.1 35.1 32.5 29.5 28.1 24.3
4p passes all off  38.0 35.5 32.9 30.0 28.4 24.6 | 38.1 35.4 32.8 29.8 28.2 24.6
4p every2          44.0 41.1 38.7 36.0 33.2 28.7 | 43.9 40.6 38.8 35.8 33.2 28.3
4p every3          48.7 44.2 41.9 39.1 35.7 30.8 | 48.4 44.7 42.1 38.9 35.7 31.2
4p --no-ssr        37.4 35.3 32.1 28.9 27.4 24.0 | 37.3 34.9 32.2 28.9 27.5 24.1
4p vd normal       39.8 38.0 33.2 30.4 30.2 26.5 | 39.8 37.8 33.6 30.6 30.2 26.4
4p every3 vd norm  53.6 49.9 44.9 41.8 39.7 34.1 | 53.5 49.9 44.5 41.3 39.3 33.9
4p nodisc vd norm  64.4 59.5 54.8 51.4 46.9 39.6 | 64.0 59.4 54.8 51.4 46.8 39.4
2p base            57.6 54.7 53.0 45.3 42.1 41.0 | 55.9 54.9 53.2 46.6 42.4 41.2 | 58.8 54.6 52.6 45.0 41.9 40.8 | 58.8 54.2 52.6 45.3 42.2 40.9
2p nodisc          80.6 79.6 76.8 72.9 58.6 55.0 | 82.0 75.4 77.6 64.8 55.9 54.1
2p passes all off  61.7 56.7 55.2 47.3 43.7 42.2 | 52.6 58.0 56.0 49.7 44.5 42.9
2p every2          69.2 63.7 63.3 53.5 47.7 46.1 | 69.4 63.9 63.4 54.0 47.7 46.5
2p every3          72.7 67.6 67.1 57.2 50.3 48.6 | 72.9 67.7 67.5 57.1 50.5 48.9
2p --no-ssr        61.0 56.7 54.8 46.8 43.3 42.2 | 60.8 56.8 54.9 47.0 43.3 42.1
2p shadows medium  44.7 54.1 51.7 44.1 41.7 40.5 | 59.0 54.6 52.0 45.4 42.1 41.0
2p shadows low     58.9 54.3 52.1 45.5 41.9 41.1 | 59.5 55.0 52.5 45.5 42.2 41.1
2p shadows off     64.1 58.1 56.4 49.2 45.5 43.4 | 64.1 58.8 57.1 49.6 46.0 43.8
2p vd normal       65.2 61.1 56.4 50.0 46.3 45.0 | 64.6 61.2 56.1 50.2 46.3 44.7
2p every2 shad off 74.5 68.2 67.8 58.6 51.8 49.0 | 74.3 68.1 67.5 58.2 51.7 49.3
2p every3 shad off 79.9 73.8 72.6 65.7 55.6 52.9 | 79.8 72.1 72.2 62.5 55.0 52.1
2p nodisc shad off 90.0 81.5 83.2 71.4 60.5 57.8 | 85.0 82.4 83.0 75.5 62.0 58.8

batch 3
4p warm            36.4 34.0 31.5 28.5 27.0 23.6 | 36.2 33.9 31.3 28.4 27.2 23.7
4p base            33.1 31.4 28.6 28.3 27.0 23.7 | 31.3 34.1 31.3 28.4 26.9
4p noclutter       41.9 39.0 36.1 33.0 31.7 27.9 | 41.9 38.9 36.2 33.0 31.8 28.2
4p fogfar          40.7 38.5 36.5 33.3 31.0 26.2 | 40.8 38.5 36.8 33.4 31.3 26.4
4p nocl+fogfar     46.1 43.8 42.3 38.1 35.4 31.4 | 46.7 44.0 42.1 38.2 35.5 31.4
4p nocl+every2     48.7 44.6 42.3 38.9 36.0 31.8 | 48.7 44.7 42.3 38.9 36.0 31.9
4p nocl+every3     51.2 46.9 44.7 40.9 37.3 32.7 | 51.3 47.3 45.0 41.3 38.1 33.1
2p base            59.2 54.3 52.4 45.2 42.0 41.1 | 52.2 55.0 52.7 45.8 42.1
2p noclutter       66.7 62.0 60.7 50.9 46.7 46.0 | 66.3 61.6 60.4 50.9 46.8 46.4
2p fogfar          65.9 61.1 60.3 50.5 45.5 43.5 | 65.4 60.7 60.3 50.6 45.2 43.5
2p nocl+fogfar     72.2 67.3 67.7 56.1 49.3 48.1 | 71.8 67.2 67.9 55.9 49.1 48.2
2p nocl+every2     44.3 68.9 67.8 60.8 51.4 49.5 | 72.3 67.5 67.3 56.6 50.3 48.4
2p nocl+every3     76.0 69.9 70.2 59.5 52.1 50.9 | 75.6 70.3 70.7 59.2 52.4 50.7
2p nocl shad off   71.7 66.1 64.9 55.4 50.4 48.8 | 71.8 66.1 64.8 55.3 50.5 49.2
2p nocl+e2 shd off 79.0 72.0 72.0 62.1 55.2 52.7 | 79.4 72.7 72.6 62.2 54.9 52.2
```

## Files

- `run.sh`: one run.
- `batch.sh`: the rounds.
- `launch.sh`: starts the batch as a `systemd-run --user` unit, since a process started from an
  SSH session dies with it on the Deck. [measured]
- `setup.sh`: unpacks a build, strips CR from every shipped script and seeds the user data.
- `shot.sh` and `shots.sh`: the crops.
- `options.json`: the seeded options.
- `Parse-Rates.ps1`: reads the game's log file. Stdout redirected to a file is block-buffered and
  shows no rate line until exit. [measured]
- `measurement-levers.patch`: the temporary switches.