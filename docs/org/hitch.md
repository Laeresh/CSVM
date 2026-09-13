# Frame hitches: the trigger, the record, the report lines and the attribution

This page is the reference for CSVM's own hitch instrument, `CSVM/src/Utils/HitchMonitor.cs`,
`HitchSidecar.cs` and `PerfSample.cs`. It holds the trigger formula, the tuning keys, the fields a
record carries, the grammar of the two report lines, and the site vocabulary a frame's named work
is reported in. Each module's purpose is its entry in
[`../architecture/Utils.md`](../architecture/Utils.md); how the resulting numbers mislead is
`docs/verification.md` PERF-12, PERF-13 and PERF-15.

## The trigger

A frame trips when

```
frame_ms > max(medianMultiple * rolling_median, floorMs)
```

and the session is past its grace window. The median is taken over the last `baselineFrames`
frames, maintained as a sorted mirror rather than re-sorted per frame. The monitor is fed a raw
wall-clock frame cost, never Godot's `delta`, which is post-processed and does not describe the
same frame as the counters read beside it.

## The tuning keys

⚠ **These five defaults are TUNE, not decoded fact.** They set how loud the instrument is, and
moving one changes how many frames are called hitches, not what the frames cost. Each is a
`hitchMonitor.*` config key over its `const` default, read in the constructor, which is also what
registers the keys for `--dump-config`.

| Key | Default | What it sets |
|---|---|---|
| `hitchMonitor.medianMultiple` | 4 | how many times the rolling median a frame must cost |
| `hitchMonitor.floorMs` | 40 | the absolute floor a frame must clear whatever the median says |
| `hitchMonitor.baselineFrames` | 120 | how many recent frames the rolling median covers |
| `hitchMonitor.ringFrames` | 120 | how many preceding frames a record carries |
| `hitchMonitor.graceMs` | 2000 | how long after a session build the detector stays quiet |

The floor is about two and a half dropped frames at the 60 Hz cap, which is where a freeze starts
being something a player feels rather than something an instrument measures. The grace window
exists because startup legitimately stalls the frame loop and `StartupProfile` already covers that
ground.

The sidecar has two more, on the same footing:

| Key | Default | What it sets |
|---|---|---|
| `hitchSidecar.queueDepth` | 16 | how many tripped records queue before the oldest is dropped |
| `hitchSidecar.flushSeconds` | 2 | how long a queued record waits before it is written |

An overflow is reported as a dropped count rather than buffered around, because a hitch storm
faster than the queue is itself worth knowing about. The flush interval also bounds what a crash
loses; `Launcher` flushes before every `Rearm`, so an ordinary relaunch never waits it out.

## What a record holds

`HitchRecord` is one preallocated instance per monitor, refilled in place, so a hitching frame
allocates nothing. A consumer that needs to keep one copies it. It carries the frame ordinal, the
frame's wall cost, the baseline and the threshold it crossed, the unaveraged script, render-CPU,
GPU and physics costs, draws, primitives, nodes and managed bytes both as absolutes and as deltas
against the previous frame, the per-generation `GC.CollectionCount` deltas and allocated bytes,
the ring of preceding frames ending with the hitching one, and the frame's `PerfSample`
attribution. Deltas read zero on the first frame after a rearm, where the previous frame belongs
to another session.

## The `[perf] hitch` line

One flat `key=value` line in the `perf` log category, in `ReportPerf`'s grammar: millisecond terms
as they are, byte counts as MB.

```
[perf] hitch frame= frame_ms= baseline_ms= threshold_ms= script_ms= render_cpu_ms= gpu_ms=
physics_ms= draws= prims= nodes= mem_mb= draws_delta= prims_delta= nodes_delta= mem_delta_mb=
gc0_delta= gc1_delta= gc2_delta= alloc_delta_mb= samples= attributed_ms= unattributed_ms=
sample_violations=
```

`samples` is the frame's named work as one space-free value, `site:callsxms` comma-separated in
site order, or `none` when nothing declared. A site with no calls is absent rather than printed as
zero, which keeps the line short on the ordinary hitch where two things ran out of eight.

## The JSON sidecar

One JSON line per record in `.scratch/logs/<mode>-<stamp>.hitches.jsonl`, sharing the main log's
stem. The keys are the log line's, spelled in full (`mem_bytes`, `mem_bytes_delta`,
`allocated_bytes_delta`), plus `"samples":[{"site","ms","calls"}]` and
`"ring":[{"frame_ms","script_ms","render_cpu_ms","gpu_ms","physics_ms"}]` oldest first. The file
opens once for the process's life, UTF-8 with a BOM and `AutoFlush`, so a line reaches disk the
instant it is written.

The JSON is hand-written rather than serialized because a record is all numeric apart from the
site names, which are compile-time `[a-z_]` constants from a closed enum and so never need
escaping. Every value is formatted through `string.Create(CultureInfo.InvariantCulture, …)` rather
than a plain interpolation, which would format under the current culture instead.

## The attribution sites

`PerfSample` declares what a frame was doing, one flat leaf scope at a time. A scope opened inside
another is suppressed and counted as a violation, so `sum(sites) + unattributed = frame_ms` holds
on every frame. The vocabulary is a closed enum, deliberately coarse: a debris burst, not one
chunk; a spawn, not one node.

| Site | What it covers | Seeded at |
|---|---|---|
| `debris_spawn` | a debris burst coming into existence | `AnimRuntime.RunDeathSequence` |
| `part_detach` | a part detaching from an aircraft | `FlightController.Crash` |
| `ai_spawn` | an AI aircraft built and added to the tree | `AiFlightAssembler` |
| `effect_checkout` | taking an effect out of its pool | `AnimRuntime.PlayEffectAt` |
| `effect_pool_miss` | a pool that had nothing to hand out | `EmitterDirector.Assert` |
| `material_create` | a material built at runtime rather than at load | `EmitterRenderer.Attach` |
| `resource_load` | a synchronous resource load on the frame path | `TextureArchive.FindImage` |
| `audio_load` | a sound loaded or decoded on the frame path | `WorldSounds` |

Adding a site is the enum plus its wire name in `PerfSample.Names`, in the same order. A record
snapshots the frame `EndFrame` closed, not the frame in progress, so the sites it reports and the
`frame_ms` it reports describe the same span.
