# Which enhanced pass lays a pattern over the screen

Question: under `--graphics=enhanced` a fine pattern covers the whole frame, and the faithful
presentation shows none. Which of the mode's screen-space passes draws it, and what removes it
without removing the pass.

## Instruments

`Measure-Hf.ps1 -Path <png...> -Rect x,y,w,h` reports three numbers over a rectangle, in luminance
units on a 0-255 scale:

- `chk2`, the mean `|L(0,0) - L(1,0) - L(0,1) + L(1,1)|` over disjoint 2x2 blocks. Only a signal
  that alternates pixel to pixel survives it, which is what a screen-space sample pattern is.
- `lap`, the mean `|4L - (left+right+up+down)|`, all high-frequency energy including real detail.
- `sd` and `mean`, the contrast and level the other two ride on. Together they are the confinement
  proof: a change that drops `chk2` while holding `mean` and `sd` removed noise, not structure.

`Diff-Shot.ps1 -A <png> -B <png> -Out <png>` writes the amplified absolute difference and reports
what fraction of the frame moved, the mean and peak luminance step, and the difference's own
`chk2`. Differencing isolates one pass's contribution, so the pass that resolves through a pattern
is the one whose *difference* alternates.

Renders go through `RunProbe.ps1 -Resolution 1920x1080 --screenshot=...`. A plain `--screenshot`
implies `--det`, which pins `--jitter` to 0; a `--shots` burst must not be used, its frames being
previous renders carrying the burst camera's dither.

## Bisect

One pose per closed door at 1920x1080, C1 waterfall (`--freecam --chapter=C1
--pos=-7720,60,-3380 --lookat=-7868,40,-3449 --frames=120`), each differenced against the full
enhanced frame:

| door | frame changed | mean abs step | chk2 of the difference |
|---|---|---|---|
| `--no-ssao` | 10.9 % | 0.61 | 0.101 |
| `--no-ssr` | 13.0 % | 7.85 | 0.069 |
| `--no-glow` | 0.0 % | 0.00 | 0.000 |
| `--no-soft-shadows` | 81.4 % | 8.17 | **2.168** |

The soft-shadow pass is the only one whose contribution alternates, and it covers most of the
frame. The C2 city pose agrees (30.0 % of the frame, chk2 2.088, every other door under 0.05).

## The lever

Whole frame at the same pose. The hard-shadow render is the floor: it is the same scene with the
penumbra filter not running.

| build | chk2 | excess over the floor | gpu_ms |
|---|---|---|---|
| faithful presentation | 0.918 | - | - |
| enhanced, hard shadow (floor) | 1.094 | 0 | - |
| angular distance 2.0, filter Medium | 2.217 | 1.123 | 2.55 |
| angular distance 1.0, filter Medium | 1.919 | 0.825 | 2.45 |
| angular distance 2.0, filter High | 1.403 | 0.309 | 4.63 |
| angular distance 2.0, filter **Ultra** | 1.252 | 0.158 | 5.78 |
| angular distance 1.0, filter Ultra | 1.160 | 0.066 | 5.26 |

Narrowing the penumbra removes a quarter of the excess and changes the judged edge width; raising
the filter quality removes 86 % of it and leaves the width alone. The filter, not the width, is
what the pattern is made of. `gpu_ms` is the `--perf` window mean on an RTX 5080 at 1920x1080; the
C2 city pose costs 1.03 ms at Medium against 1.38 ms at Ultra, far less, because far less of that
frame sits inside the shadow cascades.

## Confinement

Medium against Ultra at the same pose, 2560x1440: `chk2` 2.037 to 1.112, while `mean` moves 83.98
to 83.95 and `sd` 40.201 to 40.200. The difference image is a woven mesh over every lit surface
with no scene structure in it.

## The shadow-quality levels

Question: which cheaper sun-shadow settings keep the pattern at or under the shipped look, so the
Enhanced `Shadow Quality` row (`CSVM/src/Utils/ShadowQualitySetting.cs`) can offer them.

The terrain and the water no longer cast a sun shadow, so the waterfall pose above now shows no
caster at all: every setting renders it alike (whole-frame `chk2` 0.825 at every level). The
pattern now lives in the penumbrae of the buildings, vehicles and aircraft that still cast. The
measure is the `chk2` of the difference against the hard-shadow floor (angular distance 0, filter
Hard), at 1600x900, with anti-aliasing **off** to expose the source; the C1 town pose is
`--freecam --chapter=C1 --pos=-6620,175,-5690 --direction=0,-0.42,-0.91`, the C5 city pose
`--freecam --chapter=C5 --pos=-9256,178,-3155 --direction=-0.588,-0.1,-0.809`.

| sun (deg) / filter | C1 town chk2(dL) | C5 city chk2(dL) | town gpu_ms | city gpu_ms |
|---|---|---|---|---|
| 1.0 / Ultra (shipped) | 2.03 | 0.45 | 3.3 | 3.2 |
| 1.0 / High | 2.20 | 0.48 | 3.1 | 2.7 |
| 1.0 / Medium | 2.61 | 0.64 | 1.5 | 1.4 |
| 0.5 / High | 1.47 | 0.24 | 2.3 | 1.9 |
| 0.5 / Medium | 2.14 | 0.29 | 1.3 | 1.2 |
| 0.25 / Medium | 1.32 | 0.10 | 1.3 | 0.9 |
| 0.25 / Low | 1.57 | 0.12 | 1.0 | 0.8 |
| 0 / Low, blur 1 (four-tap PCF) | 0.42 | 0.05 | 0.6 | 0.7 |
| no sun shadow | 0.29 | 0.17 | 0.5 | 0.6 |

A lower rung under the one-degree sun raises the pattern above the shipped one, which is the
artefact `EnhancedShadowFilterQuality`'s old warning named. Each rung holds a width: Ultra 1.0, High
0.5, Medium 0.25, and a zero-width sun below that. 0.5 on Medium already matches the shipped
pattern. A lower rung under a wide sun also loses the aircraft's own shadow, the blocker search
missing a thin caster with too few samples (1.0 on Medium at the C3 plane pose).

The atlas edge is the second lever. At 4096 against 8192 (anti-aliasing on): the town pose at 0.5 /
High 3.0 to 2.0 ms, the city 2.0 to 1.3 ms, 0.25 / Medium 1.05 to 0.82 ms on the city. The
aircraft's own shadow stays visible at 4096 at both the C3 chase pose (`--chapter=C3
--pos=-2471.167,41.406,-1281.563 --direction=-0.5962,-0.02573,0.80242 --hold=0,0,0,0.6`) and the
same pose 40 m higher, at every level from 0 to 0.5 degrees; at 2048 it fades. A tighter first split (0.08 of
the shadow distance) bought nothing measurable.

Shipped levels: Off (no shadow), Low (0 / SoftLow, blur 1, 4096), Medium (0.25 / SoftMedium, 4096),
High (0.5 / SoftHigh, 4096), Ultra (1.0 / SoftUltra, 8192, the shipped look and the default).