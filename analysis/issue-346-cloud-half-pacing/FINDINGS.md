# Issue 346: half-resolution cloud pacing

Date: 2026-11-18

Issue: [#346, Volumetric clouds at half pace frames at 16.67 ms with vsync
off](https://github.com/Laeresh/CSVM/issues/346)

## Verdict

The current Half path has no mode-specific 60 Hz cap. It exceeds 160 fps at 1280x720, and at
3444x1410 its frame time rises above 16.67 ms as the scene's GPU work grows. The reproduced
16.67 ms window is where the root viewport plus the clouds' reduced viewport approach one 60 Hz
frame of GPU work, not evidence of a once-per-frame CPU wait.

The issue's original `setup_ms` near 15 ms does not reproduce. Current `setup_ms` stays between
0.05 and 0.34 ms in the controls below, while `defer_ms` carries the render-thread and GPU
back-pressure.

The investigation did find a reporting defect. `FrameInstruments.BeginFrame` measures raw wall
time with QPC, but the opt-in `--perf` window used Godot's processed `_Process` delta for
`wall_ms`, `fps`, `frame_ms`, `p95_ms`, and `max_ms`. The change on this branch passes the existing
raw `frameMs` into `EndFrame` and uses it for that window. It changes instrumentation only.

## Method

The investigation used three independent checks:

1. The cloud implementation at the exact first-parent revision current when #346 was created,
   `c6c0a471a976b712a0c49bfac8c5fc3670b28473`.
2. Godot 4.7 source at `c230cbeb6c3f4032055c7520db1747103157ebe1`.
3. The published CSVM v0.7.1 Windows binary at
   `c98858263bb115c9951dd63d190278fe85a37784`, run against the player's installed game.

Runtime controls used Vulkan on an NVIDIA GeForce RTX 5070, TAA, Enhanced options, C1C/M01,
`--perf --no-vsync`, and the same initial flight. Both the visible desktop and the repository's
hidden-desktop launcher were tested. The active display reports 60 Hz.

A requested 5120x1440 window is clamped by this desktop to 3444x1410. These measurements therefore
do not reproduce the issue's exact resolution or its RTX 5080.

## Implementation chronology

#346 was created at 2026-11-12T05:05:28Z. The first-parent revision at that instant was
`c6c0a471a976b712a0c49bfac8c5fc3670b28473`.

At that revision, Half had one reduced `SubViewport`. `FollowCamera` set its
`RenderTargetUpdateMode` to `Always` while active. There were no temporal resolve viewports and no
use of `UpdateMode.Once`, so the current one-shot resolve cannot have caused the original report.

The temporal path arrived later:

- `e428c29c7b9a7c967849fc2b7ac084ec44b1a618` prototyped the alternating resolve for Full.
- `02e21ed3417f9a8160fcd6bc05b2d28a8f701e11` added the Half temporal path.
- `d6e674f23408dd5aa1e792c0eefd763d6ad2c4dd` merged the current Full and Half path.

The current Half mode marches one quarter of the view's pixels into `_marchView`, then resolves to
half resolution through two alternating viewports. The due resolve viewport is set to
`UpdateMode.Once` in `StepTemporal`.

## Godot source trace

Godot's `VIEWPORT_UPDATE_ONCE` is scheduling state, not a synchronization primitive:

- `RendererViewport::draw_viewports` treats `ALWAYS` and `ONCE` as visible for the current draw.
- After that viewport is drawn, it changes `ONCE` to `DISABLED`.
- `RenderingServerDefault::viewport_set_update_mode` is generated through the asynchronous
  `FUNC2` wrapper. On the separate render thread, that wrapper uses `command_queue.push`, not
  `push_and_sync`.
- `RenderingServerDefault::draw` emits `frame_pre_draw` before it queues `_draw`, so CSVM's mode
  update is queued before the draw it schedules.

Godot's `frame_setup_time` also cannot directly time the cloud viewport draw. In
`RenderingServerDefault::_draw`, the timer covers `scene->update` and `canvas->update`, then stops
before particles, probes, `viewport->draw_viewports`, and presentation.

CSVM's `MeasuredRenderTime.Read` does not add a synchronous getter to the frame path. On the
separate render thread it queues `Publish` with `CallOnRenderThread` and returns the latest
published pair. The current wait therefore appearing in `defer_ms`, rather than `setup_ms` or
`proc_ms`, matches the instrument's intended model.

## Runtime results

All ranges below exclude the first 60-frame startup window.

| Clouds | Desktop | Actual size | `frame_ms` / fps | Root `gpu_ms` | Cloud viewport GPU | `setup_ms` |
|---|---|---:|---:|---:|---:|---:|
| Half | visible | 1280x720 | 4.66-5.13 / 195-215 | 3.03-3.12 | 1.01-1.03 | 0.08-0.09 |
| Half | hidden | 1280x720 | 5.85-6.09 / 164-171 | 3.64-3.76 | 1.36-1.45 | 0.06-0.09 |
| Off | hidden | 3444x1410 | 6.56-6.67 / 150-152 | 5.92-6.10 | none | 0.05-0.06 |
| Quarter | hidden | 3444x1410 | 14.02-15.31 / 65-71 | 10.54-11.94 | 2.85-2.99 | 0.08-0.10 |
| Half | visible | 3444x1410 | 13.33-13.46 / 74-75 | 9.64-9.80 | 3.08-3.22 | 0.08-0.09 |
| Half | hidden, early | 3444x1410 | 16.67-17.31 / 58-60 | 12.25-12.98 | 3.01-3.19 | 0.10-0.11 |
| Half | hidden, later | 3444x1410 | 19.13-20.35 / 49-52 | 14.75-16.19 | 2.90-3.14 | 0.11-0.15 |
| Half | hidden, heavy scene | 3444x1410 | 35.76-38.54 / 26-28 | 31.44-34.19 | 2.95-3.10 | 0.15-0.34 |

The high-resolution hidden run first reproduced the exact reported rung:

- sim frame 120: `frame_ms=16.67`, root `gpu_ms=12.73`, cloud viewport `3.014`,
  `setup_ms=0.10`, and `defer_ms=16.270`.
- As the flight became heavier, the frame followed GPU load through 19 ms and then 38 ms rather
  than remaining at 16.67 ms.

The Quarter control is the same reduced march without Half's temporal resolve. It ran above 60 fps
at the clamped high resolution. The additional resolve work is enough to move Half across the
16.67 ms region on the hidden desktop, without requiring a wait in the update-mode setter.

The raw ten-second QPC rate agrees with the processed-delta window on sustained rate:

- Raw: 54.3 fps and 18.42 ms. Adjacent `--perf` windows: 50-52 fps and 19.12-19.85 ms.
- Raw: 52.6 fps and 19.01 ms. Adjacent `--perf` windows: 49-52 fps and 19.44-20.35 ms.

The windows have different boundaries, so they should not match sample for sample. They move
together and do not support a delta-only false 60 fps cap. Processed delta still quantizes
individual samples and percentiles, which is why the `--perf` window now uses the raw QPC duration
already measured for the other frame instruments.

## Change

- `Launcher._Process` passes the `frameMs` returned by `FrameInstruments.BeginFrame` to
  `FrameInstruments.EndFrame`.
- `FrameInstruments.ReportPerf` accumulates that value directly in milliseconds and derives its
  wall clock from it.
- Verification rule PERF-52 records the lower-load and raw-rate controls required before treating
  an exact refresh-period frame as a cap.

No cloud shader, viewport update mode, pacing option, or rendering behavior changed.

## Verification

- Format gate and StyleCop rebuild: passed.
- Build: passed with 0 warnings and 0 errors.
- Quick units: 1,557 passed, 0 failed, 26 skipped because extracted test data is absent.
- Quick engine tier: 13 passed, 0 failed, 0 skipped, with clean engine errors.
- `git diff --check`: passed.
- Patched source probe: exited 0 after 1,500 Half frames at 1280x720. Its 60-frame windows ran
  4.95-6.56 ms (152-202 fps), with raw-QPC p95 values from 5.97 to 7.65 ms instead of refresh-period
  constants. The saved shot reported sim frame 1500 at 9.961 seconds.

The source probe and the quick engine tier ran the patched assembly. Its stderr retained Godot
4.7's `RenderingDevice::finalize` warning and two leaked `ObjectDB` instances at exit; the exact
v0.7.1 controls emit the same lines, and both binaries exit 0.

## Remaining limitation

This machine cannot reproduce the report's exact 5120x1440 resolution or RTX 5080. The desktop
clamps a requested 5120x1440 window to 3444x1410, so the original hardware and resolution remain
the only untested combination.
