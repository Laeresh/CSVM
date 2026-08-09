# BL-317 — where the apparent plane-local cloud wisps are spawned

Date: 2026-08-10

Binary: `crimson.exe`, Ghidra project `CSVMCrimsonExe` (`/crimson.exe`)

Method: read-only decompile and cross-reference census; the Ghidra project was not modified

## Result

No dedicated cloud-wisp spawner was found in the executable's enumerated puffer, world-card,
camera-cloud or weather systems. This does **not** prove that no other render/compositing path
exists.

The one previously missed aircraft-attached sprite emitter is a hard-coded **engine-exhaust
puffer**. It is constructed for each model locator found with `exhaust%d`, emits the ordinary
`smoke101`/`smoke102`/`smoke103` particle pool, and is driven by a positive
commanded-versus-current throttle gap. Its particles detach into world space after emission, so
they pass the aircraft, but the decoded size, trigger and lifetime do not match an ambient cloud
population that is visible continuously.

This is therefore a negative finding for the systems traced so far, not an address for a third
cloud population. **BL-317 remains incomplete:** none of the required constructor/allocation,
per-frame placement/recycling, or final render-submission links has been identified for the
observed wisps themselves. The observation still needs a controlled original capture which
separates the already-known world cloud populations and this exhaust effect. Until then, it should
not be implemented as a new renderer.

## Complete hidden aircraft-puffer chain

### Construction and attachment

Aircraft open/init is `FUN_00476250`. It searches the loaded aircraft model for sequential
`exhaust%d` locators (yielding model nodes `exhaust1`, `exhaust2`, …) and constructs one
12-byte wrapper per locator through `FUN_004af9e0` →
`FUN_004afa20`. The wrappers are retained in the aircraft vector at `+0x2b0..+0x2b4`.

`FUN_004afa20` calls `FUN_00550100`, which allocates a 0xec-byte emitter and links it into the
global emitter list headed by `DAT_00763d8c`. It stores the exhaust node at emitter `+0x74`
through `FUN_00550370`, with zero local translation through `FUN_00550380`.

The allocator's direct callers were enumerated. Apart from this path, they are the authored
`PUFFER_STATE` path (`FUN_004eeb10`) and two animation load/copy paths (`FUN_00520910`,
`FUN_00521b80`). There is no second hard-coded aircraft or camera emitter.

### Emission, placement and lifetime

The global puffer update callback is `FUN_0054ee10`, registered on a type-5 engine node by
`FUN_0054f0d0`. It walks every emitter and calls `FUN_0054f8b0`. That function resolves the
attached locator transform (`FUN_004cf410`), compares its current world position with the previous
position at emitter `+0x78..+0x80`, and emits along the travelled segment. Spawned particles are
independent world-space records in the list headed by `DAT_00763d9c`; later frames integrate
position, velocity, wind/friction and age, then remove each particle at its randomized lifetime.
This is why a puff can slide past the aircraft after leaving an exhaust point.

The aircraft updates each wrapper through `FUN_004afbc0`. In normal flight (`FUN_0048e580`) its
input is the positive difference between commanded throttle (aircraft `+0x124`) and smoothed/current
throttle (`+0x128`). Positive input charges an intensity accumulator; `FUN_00460470(..., 1.5)`
decays it. At `<= 0.01` the emitter is deactivated. Otherwise the function installs the colour
ramp and activates it. Scripted/path flight (`FUN_00490590`) passes zero.

| Property | Value | Evidence |
|---|---:|---|
| mode | distance interval | `FUN_00550440(..., 1)`; `FUN_0054f8b0` accumulates locator travel |
| interval | 0.4 m | `FUN_00550460` → emitter `+0x40` |
| count | 1 per interval | allocator default `+0x04 = 1` |
| random velocity | each axis −0.1..+0.1 m/s | `FUN_005503e0` / `FUN_00550400` |
| friction | 1.2 | `FUN_005505a0` → `+0x68`; consumed by `FUN_0054ee10` |
| initial size | 0.2..0.3 m | `FUN_00550490` / `FUN_005504a0` |
| lifetime | 0.5..1.5 s | `FUN_005504b0` / `FUN_005504c0` |
| scale over age | 1.0 at 0 → 3.45 at 1 | two `FUN_00550670` calls |
| deviation | 0.001 m | `FUN_005504f0` |
| distance fade | 200..300 m | `FUN_00550550` |
| textures | random static `smoke101`, `smoke102`, `smoke103` | three `FUN_005505f0` calls |
| origin | `(0,0,0)` at each `exhaust%d` node | `FUN_00550370` / `FUN_00550380` |

`FUN_004afbc0` supplies a two-stop alpha colour ramp. At normalized age 0.2 the RGB is
approximately `(0.02745, 0.02745, 0.03529)` with alpha equal to the clamped intensity; at age 1
it is transparent black. These near-black colours, sub-metre initial sprites and throttle-rise
trigger identify engine smoke, not pale ambient cloud wisps.

### Render submission

`FUN_0054f8b0` chooses one of the three handles stored by `FUN_0054f300`, allocates a 0x78-byte
particle, copies the emitter's size/lifetime/scale/colour state into it, and links it to
`DAT_00763d9c`. The particle retains the selected texture/state object through `+0x74`. The
explicit colour ramp ends at alpha zero, and no `cloud1`/`cloud2` material occurs in this chain.
This is the same particle-state path used by authored puffers, not the world facade/clutter path
used by `cloudsprite1/2` and `cloudparent`.

**Partial link:** the trace reaches the live-particle submission record and its complete
texture/material state, but the final dynamically dispatched draw leaf is not resolved. That
missing leaf does not change the exhaust-vs-cloud identification, but it means the ruled-out
candidate's render link does not meet BL-317's original completion bar.

## Why the known cloud mechanisms do not provide a third spawner

- `fogvol.zrd` is loaded once by `FUN_0044e010`. `FUN_0044dfa0` → `FUN_0044dc90` →
  `FUN_0044c780` scatters cards and inserts them into the world spatial database through
  `FUN_004db010`. `FUN_0044c1c0` computes a camera-facing sort value; it does not relocate or
  recycle a card. The field is world-fixed and uses `cloudsprite1/2`.
- `FUN_004db010` has only the fog-volume scatter and generic GameGen load call sites. There is no
  camera-local card-insertion caller.
- `cloudparent` clusters are authored world nodes. They share `cloud1`/`cloud2` textures with
  fog-volume cards, so texture appearance alone cannot distinguish them.
- `FUN_0042ee40` (`ZBT_CAMDYN_CLOUDHACK`) computes cloud-cover whiteout/flicker and changes no
  sprite population.
- Weather precipitation is not common to both observations: C4 IA1 authors snow; C1 IA1 has no
  precipitation `TYPE`.

## Confidence and remaining discriminator

**High confidence** that the enumerated puffer and world-card systems contain no third spawner
attached to the aircraft/camera: all direct puffer allocations, direct world-card insertions,
aircraft init/update paths, the camera-dynamic cloud function and weather constructors were
censused. Indirect allocation, a static pool, or a non-sprite compositing path is not ruled out.

**Unresolved visual identification:** the footage record does not isolate one alleged wisp while
holding throttle steady and excluding both authored cloud families by position. The next capture
should use clear air, hold throttle unchanged for at least five seconds, then make one large
increase. If puffs appear only after the increase and trail from `exhaust%d`, they are this
effect. If they persist at steady throttle, keep a landmark and altitude/position visible long
enough to establish whether each puff is a world object. A persistent steady-throttle puff would
contradict the executable census and justify looking for a non-sprite compositing artifact rather
than another spawn system.
