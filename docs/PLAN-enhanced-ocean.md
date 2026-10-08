# Enhanced wave ocean

**ACTIVE PLAN** (written 2026-10-05). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan turns the C1B wave ocean prototype into a shipped Enhanced-mode feature. The prototype
lives on branch `enhanced-ocean-proto` (commits `44e87ff19`, the ocean, and `38c970902`, the foam
patch field) and is the starting point for A1. The user approved its look from a flat-vs-ocean
montage at cruise, low pass, the coast and the M03 freighter, after asking for less regular foam.
The plan covers a Water Quality setting, the screen-space reflection cost the prototype measured,
the other sea-level chapters, and the checks the prototype left open (live switch, motion, the
Deck). It draws no items from `backlog.md` or the issue tracker.

FFT waves (2Retr0/GodotOceanWaves) are out of scope: CSVM has no compute shader yet, and the Deck
already runs Enhanced split-screen below 60 fps. The plan assumes Original mode stays pixel-identical
throughout; every change sits behind `GraphicsMode.Enhanced` and the Water Quality setting.

## Milestone goal

- Enhanced mode draws a wave-displaced ocean in place of the flat sea-level sheet on every chapter
  with a sea, with the coast layers and ship wakes intact.
- A Water Quality option (flat or waves) chooses it and follows a live switch. It defaults to waves
  on the desktop and flat on the Deck and Linux (Decision 10).
- The ocean's frame cost at low altitude is within a stated budget on the desktop and the Deck.
- An Enhanced golden pins the ocean.

**The flat colliders at y = 0 never move.** Gameplay (projectile water hits, ship height, crash and
touchdown on water) reads them, and waves stay visual only, so a network peer and a fixed-step
capture agree through `csky_time`.

## Decisions (2026-10-05)

| # | Question | Decision |
|---|---|---|
| 1 | Tessellation? | **No.** Godot 4 spatial shaders have no tessellation stage, and the sea's 512 m quads are far too coarse to subdivide on the CPU. |
| 2 | Drop in emilje/godot-water-shader? | **No, reference only.** It is one 10 m patch with SubViewport foam history and screen-texture refraction, which would move the water into the transparent pass and lose SSR. |
| 3 | Geometry | **A camera-centred polar grid** whose spacing grows with radius, positioned in the vertex stage from `CAMERA_POSITION_WORLD`, so every split-screen pane gets its own. |
| 4 | Waves | **Gerstner sum in the vertex stage, chop in the fragment normals**, driven by `csky_time`, never `TIME`. |
| 5 | Hiding the flat sheet | **Collapse its sea-level triangles in the Enhanced shader text** behind the `csky_ocean_on` global; its colliders are untouched. |
| 6 | Shore and ships | **A mask baked from the world's water polygons**, plus calm discs at the wake sheets and roster hulls. |
| 7 | Foam | **Crest height gated by a drifting world-space noise field.** Crest height alone printed a lattice the user read as a pattern. |
| 8 | FFT waves | **Later, not this plan.** |
| 9 | Look approval | **Approved by the user from the montage** after the foam change. |
| 10 | Water Quality default | **Waves on the desktop, flat on the Deck and Linux.** B12's measurement confirms or revises it. |
| 11 | B11 budget | **At most 1.5 ms over the flat sea** at the low-pass pose, desktop 1080p; about the ocean's cost with SSR off today. |
| 12 | C21 chapter scope | **Every chapter with a sea at y = 0: C1, C1C, C2, C2B, C3, C5.** C4 keeps its flat lakes. C2's coplanar beaches, C3's shore sheets and C5's fog-gradient overlays are the item's to solve, not reasons to leave a chapter out. |
| 13 | The ocean in the spyglass disc | **None: the disc shows the flat sea.** The user's ruling. The disc's camera carries a marker layer bit the ocean grid reads to step out before its wave math, and the sheet reads to keep drawing, through `CAMERA_VISIBLE_LAYERS`; the pane cameras do not carry it. |
| 14 | A sea state per region | **In this plan after all, through E41's ocean lab (Decision 16); the user tunes the regions with it.** First ruled a follow-up after landing; the user then asked for the lab before landing. The guidance below stands for the tuning: a table per region setting wave height (`wave_scale`), swell length, chop, foam (`foam_strength`) and wind direction, starting from Northwest (C1, C1B, C1C) rough open Pacific, Hollywood (C2, C2B) moderate, Hawaii (C3) a long gentle swell with little foam, Manhattan (C5) calm harbour chop; judged from a montage of each region, today against proposed. Height alone folds the crests by about 1.8x, sooner where the sea-state field raises a group (the horizontal displacement sums to `Choppiness` times the scale times that field's gain), so a rougher sea also lengthens the swell. Re-pins `c1b-ocean-enhanced` if C1B changes. |
| 15 | A3's look findings | **Fixed in this plan, then a re-check at the controls before landing.** The user's ruling: the swell's wave lattice (D31), the foam up close (D32) and the hard cut to flat water at the coast (D33). Procedural ship wakes are issue #160, not this plan. If D33 cannot make the coast gradual, a GitHub issue takes waves to the coast with procedural shore foam in place of the coast foam textures, linked to #160. |
| 16 | An ocean lab for per-chapter seas | **Built before landing (E41).** The user's ruling: a lab in `--freecam` only, opened with Shift+F1, exposing the swell (height, length, wind, crest sharpness), the crest bending and noise detail, the foam, and the coast ramps, tint and roughness, live; its Save writes a shipped per-chapter data file the game reads at load. The user tunes the seas with it; the shipped defaults are today's values, so nothing moves until they save. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The ocean reads darker than the flat sea because its colour pipeline differs (vertex colour or texture linearisation). | With waves, foam and detail mix neutralised and roughness at the sheet's 0.25, the ocean matched the flat sea to 0.65/255 mean luminance over the sea region. The darkening came from low roughness (0.07) and a 35 % texture mix. |
| 2 | Screen-space reflection over the wavy surface is most of the ocean's low-pass cost (the prototype read 18.4/20.5 ms against the flat sea's 13.4 ms, and 7.8 against 6.5 ms with `--no-ssr`). | Those runs measured the crash splash and a shared GPU: the low-pass pose with no input flies into the sea at about sim frame 280, inside the windows read from frame 240. Held at about 22 m (`--hold=0.04,0,0,0.7`) on an idle GPU, the ocean adds 1.09 to 1.15 ms with SSR and 1.06 to 1.12 ms without (B11). |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, from prototype to feature

1. ☑ Port the prototype to a production module on C1B
2. ☑ Water Quality setting (flat or waves), live switch included
3. ☑ Verify the live graphics switch and the motion look

### Wave B, cost

11. ☑ Bring the low-altitude SSR cost within budget
12. ☑ Measure and budget the Deck
13. ☑ Cache or speed up the mask bake
14. ☑ No secondary viewport draws the ocean grid it does not need

### Wave C, coverage

21. ☑ The other chapters with a sea at y = 0
22. ☑ Ship calm zones for every hull and wake
23. ☑ Swell regularity from altitude
24. ☑ An Enhanced ocean golden
25. ☑ The ocean matches the flat sheet at the shore and in fog
26. ☑ No hole where a mission shows a node hidden at the bake
27. ☑ Calm zones for mission-animated boats with no wake sheet

### Wave D, A3's look findings

31. ☑ Break up the swell's wave lattice
32. ☑ Foam that reads as foam up close
33. ☑ A gradual fade to flat water at the coast

### Wave E, per-chapter seas

41. ☐ An ocean lab and a shipped sea state per chapter
42. ☑ A shader rewritten for Enhanced with no wearer compiles before Enhanced's first frame
43. ☐ No raised water triangle shows or shadows over the ocean in a harbour

## Dependency and parallelism notes

A1 blocks everything: every later item edits the module it produces. A2 and A3 follow A1 and both
touch `GameSession`'s follow path, so they run in sequence. B11 and B12 share the frame-cost
instrument and should run together; B12 needs the Deck reachable. C21 needs B13 only if the bake
cost grows with the chapter's polygon count beyond what C1B showed. C24 goes last, after the look
and the cost settle, because every tune moves its hash. File contention: A1, A2, B11 and C23 all
edit `Effects/Ocean.cs` (B13 edits `Effects/OceanMask.cs`); never run them in parallel worktrees.

---

# Wave A, from prototype to feature

## A1 ☑ Port the prototype to a production module on C1B

**Landed.** `Effects/OceanPrototype.cs` is now two modules: `Effects/Ocean.cs` (class `Ocean`: the
grid, the shader text, the wave tables, the foam and the calm discs) and `Effects/OceanMask.cs`
(the shore mask bake, its walk state in a nested walker instead of ref parameters). Every wave,
foam, roughness, calm-radius and mask value is unchanged. The PROTOTYPE labels are gone from the
code, `csky_ocean.gdshaderinc`, `SceneBuilder` and `docs/architecture/Effects.md`; the log lines
read `ocean: ...`. Env-var doors: `CSVM_OCEAN=0` became `--no-ocean` (an `EnhancedPasses.Ocean`
bisect door beside `--no-ssr`, parsed in `SessionSpec`), `CSVM_OCEAN_MASK_PNG` became
`--dump-ocean-mask=<path>` (writes the mask and does not end the run), and `CSVM_OCEAN_PARAMS` and
`CSVM_OCEAN_TRACE` are deleted. Both flags have `docs/cli.md` bullets and index entries. The chapter
gate (`Ocean.Covers`, C1B), the `--fly`/`--freecam` gate and the Enhanced-only build/drop in
`GameSession.FollowOcean` are as before. The wave tables stay generated shader constants.

**Verified.** The cruise pose (`--chapter=C1B --graphics=enhanced --pos=-6000,450,-10800
--direction=-0.12,0,1 --look=0,-0.45`) is pixel-identical before and after the port (decoded md5
`6E4CA61F...`); `--no-ocean` draws the flat sheet with no hole. The complete battery on the plan
tree: units 6275 passed, 3 skipped; engine 527 passed, 2 skipped, errors clean; 24 goldens
hash-identical. A first engine run lost one shard to a 300 s timeout and showed a
`shader_rd.cpp` cache-read error while another session's battery ran beside it; both went away
on a quiet machine.

**Original approach (kept for reference).**

**Goal.** C1B in Enhanced draws the approved ocean from a module named and documented as a feature,
with the prototype's debug doors removed or turned into documented flags.

**Evidence (confidence: direction-sound).** The prototype works end to end on C1B: commits
`44e87ff19` and `38c970902` on `enhanced-ocean-proto`. Measured there: about 134k grid vertices; a
1536x1536 mask at 8 m; the bake takes about 0.9-1.1 s on load, 3.3 s once in a campaign load; 24
goldens hash-identical. The hooks are `SceneBuilder.IsOceanBaseTexture` and the 524288 bias-shader
key bit, `csky_ocean.gdshaderinc`, `GameSession.FollowOcean`, and
`OceanPrototype.RegisterGlobal` in `Launcher`. Every wave, roughness, foam and calm-radius value is
TUNE.

**Approach.** Rename `Effects/OceanPrototype.cs` (for example `Effects/Ocean.cs`, with the mask bake
split into its own module if that deepens it). Drop the "PROTOTYPE" labels. Replace the env-var
doors (`CSVM_OCEAN`, `CSVM_OCEAN_PARAMS`, `CSVM_OCEAN_TRACE`, `CSVM_OCEAN_MASK_PNG`) with documented
`--` flags or delete them. Keep the chapter gate at C1B until C21. <TODO: whether the wave tables
stay generated shader constants or become a data file.>

**Model recommendation.** high: a cross-module port over the world shader generator, where a slip
changes Original-mode text.

**Verify.** The full `.\RunTests.ps1`, with Original goldens hash-identical; the four montage poses
(see the prototype's `shots.ps1` poses in its commit message) match the approved look; the flat-sheet
equivalence check (waves off, roughness 0.25) within 1/255.

**⚠ Traps.**
- A `PowerShell` probe that passes an array of args as one element merges them, and the launcher
  then hangs on a parse error rather than quitting. Splat the args.
- `--freecam --screenshot=` hung in this session too, and the flat sea also hung, so it is not the
  ocean. Use `--fly` with `--pos`/`--direction`/`--look` for shots.
- The ocean must stay one-sided: a cutscene camera below y = 0 (C1B/M03's intro) otherwise sees the
  ocean across the sky.

## A2 ☑ Water Quality setting (flat or waves), live switch included

**Landed.** `Utils/WaterQualitySetting.cs` is a `WordSetting` over `flat`/`waves`:
`--water-quality=` beats the saved `waterQuality` option, which beats the `graphics.waterQuality`
config key, then the machine's fallback. Under `--det` only the flag survives, over `waves`, so the
goldens and the A1 cruise shot do not move. The fallback is `waves` on a desktop and `flat` on Linux
or an integrated GPU (`OperatingSystem.IsLinux()`, plus the integrated-GPU test shadow quality
already made, now `ShadowQualitySetting.IntegratedGpu`); the Deck is both. The log names it
`water_source=default_linux_or_integrated_gpu`. Built-in's Options screen has a Water Quality row
under Shadow Quality, dead under Original like that row; the Original VIDEO page has no line for it
and carries the saved word through `OptionsApplyExit` unchanged, as it does the view distance. An
apply re-resolves the word and `EnhancedLook.ApplyWaterQuality` calls
`GameSession.FollowWaterQuality`, so `FollowOcean` builds the ocean at `waves` and drops it at
`flat` (the dropped node's `_ExitTree` resets `csky_ocean_on`, so the sheet draws its sea again).
A switch to Original drops it as before. `--no-ocean` still wins. The word is announced on the
`[world] graphics mode:` line and on `water quality applied:`. `docs/cli.md` has the bullet and
index entry.

**Verified.** The complete battery on the plan tree with A2, A3's scripted half, B13 and C21 merged: units 6307
passed, 3 skipped; engine 528 passed, 2 skipped, errors clean, after one fix (`menu-backdrop` still
counted eighteen Options steppers; A2's row makes nineteen); 3 Enhanced goldens moved, all by the
ocean now covering C1 and C5 (`c1-lake-enhanced`, `c5-city-night-enhanced`,
`c1-rocket-hit-enhanced`), re-pinned after C25. `graphics-water-quality` builds, drops and rebuilds the ocean on a live change.

**Original approach (kept for reference).**

**Goal.** An option chooses flat or waves in Enhanced, saved like the other display options, with a
`--` flag and a config key, and a change applies live.

**Evidence (confidence: lead-only).** The handoff named the setting; nothing about its shape was
settled in this session. The other Enhanced options (`--view-distance`, `--shadow-quality`) are the
pattern to copy.

**Approach.** Copy the --view-distance/--shadow-quality option pattern: a `--water-quality=flat|waves` flag, a saved option, a config key, a menu row beside the other Enhanced graphics options, and a live
apply through GameSession.FollowOcean. Default per Decision 10. The default tells the Deck and Linux
from the desktop by `OperatingSystem.IsLinux()` or the integrated-GPU test the shadow-quality
fallback already makes.

**Model recommendation.** medium: follows an established option pattern.

**Verify.** `CSVM.Tests/WaterQualitySettingTests.cs` (the ladder, the platform fallback and its
source, the `--det` fallback, the flag parse, the options-file round trip); the engine suite
`graphics-water-quality` (a C1B Enhanced flight at waves builds the ocean, a live move to flat drops
it from the tree, back to waves builds it again; a flat build builds none; `--no-ocean` builds none
at waves); `display-det-guard` covers the saved word's `--det` drop; `menu-original-tracer` walks
the new Built-in row. A C1B cruise shot at `--water-quality=waves` stays pixel-identical to A1's,
and one at `flat` matches the `--no-ocean` shot.

**⚠ Traps.** Under `--det` only the flag survives, so a golden cannot read the saved option.
`RenderingServer.GlobalShaderParameterGet` errors outside the editor, so a suite cannot read
`csky_ocean_on` back; it reads the ocean node's tree membership, which is what sets it.

## A3 ☑ Verify the live graphics switch and the motion look

**Goal.** A live Original↔Enhanced switch on C1B adds and removes the ocean with no flat-sheet hole
and no double draw, and the ocean in motion shows no objectionable shimmer, crawl or SSR flicker.

**Evidence (confidence: lead-only).** Not tested in the prototype: there is no scripted door for the
switch, and stills cannot show shimmer. By code path, `FollowOcean` drops the node on Original, and
`_ExitTree` resets `csky_ocean_on`, so the sheet draws again.

**Approach.** Add a switch round-trip suite case on C1B (`Testing/GraphicsSwitchSuites.cs` builds
its own sessions). Ask the user to fly C1B at cruise and low level for the motion judgement.

**Model recommendation.** medium.

**Verify.** The suite case; the user's at-the-controls verdict, quoted in the landing commit.

**⚠ Traps.** Look judgements are the user's. Do not settle shimmer by a frame-difference metric
alone.

**Landed (scripted half).** The engine suite `graphics-ocean-switch` (`Testing/GraphicsSwitchSuites.cs`,
about 11 s) drives `EnhancedLook.Switch` on whole C1B flight sessions at `waves`. It names the base
sheet's materials through `SceneBuilder.TexturedMaterials` and `IsOceanBaseTexture` (C1B has one,
`wtr00000`, drawn in 144 surfaces under Original and 3 merged ones under Enhanced), read through a
new suite-only `GameSession.WorldScene` accessor. It proves: an Enhanced build stands one ocean and
the sheet's text carries the `csky_ocean_hides_sea` collapse; a live switch to Original takes the
ocean out of the tree and leaves the sheet the text a fresh Original build gives it, with no
collapse; a switch back builds a new ocean, exactly one under the whole test host, and the sheet's
text is a fresh Enhanced build's again; an Original-built session switched to Enhanced builds the
ocean with the same sheet text; each closed session leaves no ocean in the tree, so the sheet's
switch is back at 0; and a following Enhanced C2B session at flat water quality (a whole-map
`wtr00000` sheet that carries the hide, which a switch left on would hole) builds none. A network session's refusal
is already `net-pause-overlay`'s, and it returns before the session follows anything. Mutating
`FollowOcean` to forget the dropped ocean fails the leave and exactly-once checks.

**Verified.** The user's flight on the plan branch: "Switch between original and enhanced works
good." "Water overall looks good but can we reduce/breakup the grid pattern it still very visible
perhaps through a noise map? the foam in c1b looks good from a distance but not really like foam if
near it (kinda glitchy)". "the cut to costal flat water is really strong. especially on holywood in
the harbour where only a small stripe of waves is present." The pattern is the wave lattice (the
user's answer). The three look findings are D31, D32 and D33 (Decision 15).

# Wave B, cost

## B11 ☑ Bring the low-altitude SSR cost within budget

**Landed.** No code change: the ocean's low-pass cost is already inside Decision 11's 1.5 ms, and
SSR is not where it goes. The low-pass pose with no input flies into the sea at about sim frame
280, so windows read from frame 240 measured the crash splash (draws fall from about 1,370 to 30-57
and `gpu_ms` reaches 9-13 ms). The prototype's and C23's low-pass numbers carry that, and the
prototype's also ran on a shared GPU. With `--hold=0.04,0,0,0.7` the plane holds about 22 m.
`gpu_ms` at 1920x1080 with `--perf --frames=600 --no-vsync` and `--screenshot` (so `--det`), mean
over windows from sim frame 240, ocean minus `--no-ocean` in the same round, arm order rotated each
round. Every round counted ran with no other Godot or cargo process and the GPU under 12 % busy
before and after each run:

| Pose | Quiet rounds | SSR on | `--no-ssr` |
|---|---|---|---|
| Low pass, held | 4 | +1.09, +1.09, +1.10, +1.15 ms | +0.68, +1.06, +1.12, +1.11 ms |
| Cruise | 3 (2 with `--no-ssr`) | +0.87, +0.44, +0.47 ms | +0.54, +0.44 ms |
| C3 coast (`--pos=-10700,250,-5800 --direction=1,0,0 --look=0,-0.3`) | 3 | +0.54, +0.50, +0.56 ms | +0.55, +0.49, +0.44 ms |

The held low pass reads 2.78-2.86 ms with the ocean against 1.69-1.72 ms without. The ocean costs
the same with SSR off, so reflection rays over the wavy normals are not the cost. One bisect round
on the held low pass: the fragment wave loops off saves about 0.15 ms and the vertex wave loop off
nothing measurable, so most of the 1.1 ms is the grid itself (about 267k triangles, drawn in the
depth prepass and again in the colour pass). Under `--det` the frame loop holds 120 fps with
`--no-vsync` too, so every arm used it; a single pane draws no spyglass disc (no `[perf] spyglass`
line), and prims read 280k with the ocean against 12k without. Run-to-run noise on one arm is about
0.3 ms, so a single round does not settle a delta near the budget.

**Verified.** Measurement only; `Ocean.cs` is byte-identical to the plan branch. Every counted round passed the quiet gate before and after each run (no other godot, cargo or rustc process, GPU under 12 % busy). Rounds during the user's game and during another session's test batches were discarded; the two remaining bisect arms (sea-state field off, 192-segment grid) were skipped for that reason.

**Original approach (kept for reference).**

**Goal.** The ocean's low-pass cost over the flat sea is at most 1.5 ms (Decision 11), with no visible loss the user objects to.

**Evidence (confidence: direction-sound).** Desktop RTX 5080 at 1920x1080, with another session's
tests sharing the GPU: cruise 1.33 vs 1.03 ms; low pass 18.4/20.5 vs 13.4 ms; low pass with
`--no-ssr` 7.8 vs 6.5 ms. Measured with `--perf` `gpu_ms`, windows from sim frame 240.

**Approach.** <TODO: candidates, none tried: raise the ocean's roughness where SSR is costly, cut
`SsrMaxSteps` under the ocean, fade the wave normals SSR sees, or reflect the sky analytically and
keep SSR for near objects.>

**Model recommendation.** high: a performance bisect where the look must not regress.

**Verify.** The same `--perf` A/B on the cruise and low poses, alternating ocean and flat
(`--no-ocean`) over at least two rounds, on an otherwise idle GPU.

**⚠ Traps.** Read `docs/verification.md` PERF-1/PERF-2 first: judge `gpu_ms`, not `fps`. A shared
GPU (another session's battery) inflates both arms.

## B12 ☑ Measure and budget the Deck

**Landed.** Measured, no code change. Decision 10 holds: the Deck cannot carry waves at 60 Hz, so
Water Quality stays `flat` there by default, and a player can still choose `waves`. A Linux export of
the plan tree (`9077d2c5b`) ran in `~/CSVM-tmp/b12` on the Deck's own screen (gamescope, 1280x800,
60 Hz) under `--det --perf --no-vsync --frames=600`. That is native render scale with TAA, which is
also the user's saved Deck setup (render scale and AA unset), and `--shadow-quality` as the Deck's
own fallback (high at 1 and 2 panes, off at 4). Arms `--water-quality=waves` and `=flat`, alternated
over two rounds; the rounds agree within 0.1 ms. Single player reads `gpu_ms`; split screen reads
`frame_ms`, since `gpu_ms` there measures only the root viewport (PERF-39). Means over the windows
after sim frame 240, except the low pass (see the traps):

| Pose | Panes | Flat | Waves | Ocean cost | Prims, flat / waves |
|---|---|---|---|---|---|
| C1B cruise | 1 (`gpu_ms`) | 8.67 | 15.50 | +6.8 ms | 10k / 278k |
| C1B low pass | 1 (`gpu_ms`) | 9.38 | 17.31 | +7.9 ms | 12k / 280k |
| C3 coast | 1 (`gpu_ms`) | 9.82 | 17.27 | +7.5 ms | 17k / 284k |
| C1B cruise | 2 (`frame_ms`) | 9.98 | 22.18 | +12.2 ms | 32k / 1.10M |
| C1B low pass | 2 (`frame_ms`) | 11.14 | 25.84 | +14.7 ms | 33k / 1.10M |
| C3 coast | 2 (`frame_ms`) | 11.55 | 24.85 | +13.3 ms | 58k / 1.13M |
| C1B cruise | 4 (`frame_ms`) | 12.89 | 27.45 | +14.6 ms | 51k / 2.19M |
| C1B low pass | 4 (`frame_ms`) | 16.66 | 30.94 | +14.3 ms | 59k / 2.20M |
| C3 coast | 4 (`frame_ms`) | 16.59 | 29.78 | +13.2 ms | 103k / 2.24M |
| Low pass held, 720p | 1 (`gpu_ms`) | 8.22 | 14.86 | +6.6 ms | 12k / 280k |
| Low pass held, 720p | 2 (`frame_ms`) | 10.07 | 23.06 | +13.0 ms | 34k / 1.10M |
| Low pass held, 720p | 4 (`frame_ms`) | 16.05 | 29.20 | +13.2 ms | 60k / 2.20M |

The last three rows are the low pass held airborne over all 600 frames (`--hold=0.04,0,0,0.7`, near
22 m), measured later in the Deck's Desktop Mode, where the window came up at 1280x720 under KWin
instead of 1280x800 under gamescope. They compare with each other, not with the rows above.

The split-screen rows read high twice over: each pane's spyglass disc also drew the full grid
(fixed in B14, the disc now shows the flat sea), and the `--perf` spyglass census then read
`GetRenderInfo` on the main thread, forcing a render-thread sync on every frame a disc rendered
(B14 moved it to the render thread). They are not a measure of the Deck's split-screen cost today.

Draw calls do not move (under 10 more). Single-player waves run at 64 fps at cruise and at 57 to 58 fps
at the low pass and the C3 coast, against 101 to 114 fps flat. With two panes they run at 39 to 45 fps,
against 87 to 100 fps flat, and with four at 32 to 36 fps, against 60 to 78 fps flat. On the desktop the ocean
costs +0.45 ms at cruise and +1.45 ms at the low pass (C23, 1920x1080), so the Deck pays about 15 and
5 times that at fewer than half the pixels. In split screen the
prims are four and eight times the single-player grid, and `[perf] spyglass` reads one live disc per
pane, which fits each pane's spyglass disc drawing the grid too (inferred from the counts, not split by
viewport per PERF-44). If split-screen waves are ever wanted on the Deck, the disc's cull mask is the
first lever to test. Linux machines with a discrete GPU also
default to `flat` under Decision 10; B12 did not measure one. Shots with the ocean, all at frame 600 on
`RADV VANGOGH`, are in the plan tree's `.scratch\b12\`: `r1-cruise-1p-ocean.png`,
`g1-lowh-1p-ocean.png` (the held low pass, 720p) and `r1-c3-1p-ocean.png`, plus the 2-pane and 4-pane
`r1-*-ocean.png` shots.

**Verified.** Measurement only, no engine code changed. Every row is two alternated rounds agreeing within 0.1 ms, with no other CSVM process on the Deck at any run's start or end; the user's interruption killed one follow-up batch, which was discarded whole and re-run once the Deck was free. Decision 10 holds: the Deck keeps `flat` by default.

**Original approach (kept for reference).**

**Goal.** The ocean's cost on the Steam Deck is known for single-player and split-screen, and the
Water Quality default there follows from it.

**Evidence (confidence: lead-only).** The Deck was unreachable this session (`ssh deck@steamdeck`
timed out). The handoff notes Enhanced split-screen already runs below 60 fps there.

**Approach.** Deploy to `~/CSVM-tmp` per the Deck notes and run the B11 poses under `--det`.
<TODO: split-screen poses.>

**Model recommendation.** medium.

**Verify.** <TODO: the budget line and the default it implies.>

**⚠ Traps.** SSH screenshots on the Deck need `--det`; a `--no-det` shot captures the loading frame.
- The B11 low-pass pose with no input flies into the sea at about sim frame 280 (`[flight] CRASH into
  g28278/col_water`, every pane), so its windows after frame 240 measure the crash splash, not the
  water. The table's low-pass row reads the windows at sim frames 120 to 240, before the impact.
  `--hold=0.04,0,0,0.7` keeps it near 22 m for 600 frames; 0.02 grazes the water and 0.07 climbs to
  90 m. Desktop low-pass figures taken over frames 240 to 600 carry the same splash.
- A run on the Deck opens on its screen, so a run while someone uses the Deck interrupts them, and
  they may kill it. Check that the Deck is free first. Read the `[perf]` lines from the run's stdout:
  the `*.log` a run leaves may be the `.godot.log` mirror, which stops short of the last windows.
- In Desktop Mode a split-screen `--screenshot` run can hang after frame 600, at the capture or in
  teardown, until `timeout` kills it. Its `[perf]` windows are complete by then, but stdout is cut
  where the kill lands. Kill the process once the frame-600 window is out, rather than waiting.
- In split screen `gpu_ms` reads about 0.15 ms (PERF-39). `frame_ms` with `--no-vsync` is the
  measure, and `proc_ms` rises with the ocean only because the frame waits on the render thread
  (PERF-43).
- The first run after an install compiles the pipelines. A cold cruise run, beside another session's
  headless run, read 19 ms where warm runs read 15.5 ms. Discard it, and check `pgrep -f CSVM.x86_64`
  before each run.

## B13 ☑ Cache or speed up the mask bake

**Landed.** The `ocean: built` line now carries `bake=` and the split `walk= raster= distance=
upload= bands=`. Measured first on the C1B cruise pose (Debug build, a shared machine): the bake
took 3.0 to 4.8 s, of which `ArrayMesh.SurfaceGetArrays` over 2238 surfaces took 1.9 to 3.6 s
(about 1 ms each, the render-thread read-back), the triangle raster 0.7 s, the encode 0.15 s and
the chamfer 0.1 s. The fix follows those numbers, with no disk cache, since nothing left is worth
invalidating. `OceanMask` reads each surface through `SceneBuilder.SurfaceArrays`, the arrays the
builder already keeps (positions, indices and UVs bit-equal to the read-back; colours truncated to
RGBA8 as Godot stores them). One tree pass now finds the wakes and the surfaces together. The
compute moved to a new pure module, `Effects/OceanMaskRaster.cs`: scalar arithmetic in the old
operator order, rows clipped to the texels the test can take, and row bands on the thread pool,
the distance pass reading a 24-row halo. A built world keeps its bake, so an ocean rebuilt over it
(live switch, Water Quality) reuses it. After: `bake=` 87 to 96 ms over five loads (walk 27 to 33,
raster 26 to 36, distance 17 to 21, upload 7 to 8); a bake run after the world merge took 107 ms
and gave the same mask. Every load is a cold bake; there is no warm path besides the in-session
reuse. The rest of `ms=` (about 60 ms) is the grid and material in `Ocean.Create`, outside the mask.
Mask, tint and cruise shot are pixel-identical before and after (mask decoded md5 `75EBB314...`,
tint `8742B5D0...`, cruise BGRA md5 `6E4CA61F...`). `OceanMaskRasterTests` hold the module to the
old whole-image pass at 1, 5 and 12 bands.

**Verified.** The complete battery on the plan tree with A2, A3's scripted half, B13 and C21 merged: units 6307
passed, 3 skipped; engine 528 passed, 2 skipped, errors clean, after one fix (`menu-backdrop` still
counted eighteen Options steppers; A2's row makes nineteen); 3 Enhanced goldens moved, all by the
ocean now covering C1 and C5 (`c1-lake-enhanced`, `c5-city-night-enhanced`,
`c1-rocket-hit-enhanced`), re-pinned after C25. The bake logs 100-171 ms with `gc=` 32-45 ms on C1B, C2B and C3.

**Original approach (kept for reference).**

**Goal.** The bake adds no noticeable load time.

**Evidence (confidence: direction-sound).** About 0.9-1.1 s in `--fly` loads and 3.3 s in one
campaign load (C1B), from the `ocean: built` log line. It walks every mesh instance and
reads `SurfaceGetArrays` per surface.

**Approach.** <TODO: cache per chapter under user data keyed by the gamez hash, or bake from gamez
polygons directly instead of the built tree.>

**Model recommendation.** medium.

**Verify.** The `ms=` field of the build log line, before and after; the mask image unchanged
(`--dump-ocean-mask=<path>`).

**⚠ Traps.** The edge extender's tiles are rolling and absent at bake time; clamp-to-edge sampling
carries the border sea outward instead.

# Wave C, coverage

## C21 ☑ The other chapters with a sea at y = 0

**Landed.** `Ocean.Covers` lists C1, C1B, C1C, C2, C2B, C3 and C5; C4 keeps its flat lakes.
`IsOceanBaseTexture` takes `wtr*` and `water1` exactly, so C1's opaque `water1_trans1/2` coast
tiles stay and draw over a calm ocean. Five mechanisms, all Enhanced-only and inert at the C1B
cruise pose (decoded md5 `6E4CA61F...` before and after every step):
- The sheet hides per fragment, not by collapsing vertices. C2's channel ramp `g36353` (0 to
  1.03 m) stretched a sea triangle to its model origin, a white wedge in the harbour. The ramp's
  foot is now surf-ring water in the mask, calm and covered, and its raised part keeps the flat arm.
- The ocean draws one priority level below the LOWEST base sheet (`OceanMask.BaseLevel`): C2 and C3
  at -2 (their `p-1` sheets sit beside `terpat`, `cliff1_watertrans*` and `sand128` at -1, which
  must stay on top), C5 at -11 (its sheet is `p-10`), the rest at -1 as on C1B.
- The mask walks only the visible tree. C3's `swtr01`-`05` sheets build hidden over the crater
  floor that dips to -48.7 m (`g28608`); counting them drew the ocean across the island valleys.
  It also drops the unplaced zeppelins parked at the origin, a calm patch in every chapter.
- The ocean draws on its base sheet's zone-gate layers. C5 splits its sea between `zone_id` 3 and
  1, and the camera above the band culls the zone-1 half to black; the ocean is one grid per group
  with an R8 zone texture, seam texels belonging to neither, under C5's opaque fog strip.
- C5's fog-gradient passes (`z3_foggrad`, `foggrad8x64`) are overlay passes on the hidden `p-10`
  sheet. They are not water, so they keep drawing; at -1 the ocean would have covered them, at -11
  it sits behind. The mask counts them as solid, so the waves are flat under every strip and the
  strip blends over calm water exactly as over the sheet. They mark the zone seams, where the sea
  fades into the culled zone.
Per chapter: C2B and C1C are whole-map seas with no coast (zeppelin chapters), no rule beyond the
gate. C1 `water1` plus its coast tiles as surf-ring water. C2 the ramp fix and level -2. C5 level -11
and two zone grids. C3 level -2 and the visible-tree walk.
Coplanar shore layers (C2 `beach1`, `terpat*`, `cliff01_trans2`; C3 `shore1/2`, `shore_trans`,
`sand128`, `cliff1_*trans*`) are solid in the mask, so the waves reach zero under them. The bake
ends with a `GC.Collect`: without it a C2B shot at frame 15 crashed at exit (0xC000001D, Godot's
FATAL on live wrappers), a run 300 frames long did not. Tests: `OceanBaseTextureTests`, and
`OceanMaskRasterTests.EachTexelNamesItsSheetsZoneGroup`. Montages, flat left and ocean right, per
chapter in the plan tree's `.scratch\c21w\montage-<ch>.png`.

**Verified.** The complete battery on the plan tree with A2, A3's scripted half, B13 and C21 merged: units 6307
passed, 3 skipped; engine 528 passed, 2 skipped, errors clean, after one fix (`menu-backdrop` still
counted eighteen Options steppers; A2's row makes nineteen); 3 Enhanced goldens moved, all by the
ocean now covering C1 and C5 (`c1-lake-enhanced`, `c5-city-night-enhanced`,
`c1-rocket-hit-enhanced`), re-pinned after C25. Every chapter's montage was approved by the user (Decision 12 scope), with the
fog mismatch and the swell cross-hatch filed as C25 and C23. A short C2B run crashed on exit (0xC000001D,
451 leaked `ArrayMesh` wrappers) without the collection after the bake, and exits clean with it.

**Original approach (kept for reference).**

**Goal.** Every chapter whose sea sits at y = 0 draws the ocean, with its own coast layers intact.

**Evidence (confidence: direction-sound).** A data survey of every chapter's gamez, with world y
through the full node transform chain (scripts and outputs in the plan tree's `.scratch\c21\`,
summary in `REPORT.md` there). Sea-level water (y within 0.5 m), its base sheet, and the risks:

| Ch | Base sheet (polys, priority) | Over or beside it | Coplanar land at y = 0 |
|---|---|---|---|
| C1 | `water1` 236 (p0), a strip along z -3421..0 | opaque coast tiles `water1_trans1/2` 372 BESIDE it (p0), carrying soft `cliff01_trans*` overlay passes | none |
| C1B | `wtr00000` 695 (p0), whole map | `srf0001` 375 (p1, soft); `wakefront1` on the freighter | none |
| C1C | `water1` 576 (p0), whole map | none | none |
| C2 | `wtr00000` 327 (p0 x263, p-1 x43, p1 x18, p2 x3) | `srf0001` 86 (p1) | 110: `beach1` 58 (soft), `cliff01_trans2` 27, `terpat*` 22 |
| C2B | `wtr00000` 576 (p0), whole map | none | none |
| C3 | `wtr00000` 1565 (p0 x1182, p-1 x383), 18 km2 double sheet | none | 2090 (`shore1/2`, `shore_trans`, `cliff1_watertrans*`, `sand128`, ...), 85-94 % lying on the sheet; terrain pits to -48.7 m |
| C4 | none (lakes at y = 517 and 704 only) | | |
| C5 | `wtr00000` 700, all p-10 | 142 carry soft fog-gradient overlay passes (`z3_foggrad`, `foggrad8x64`) | none |

C3's `waterlevel` sits at y = 13.17 and C4's lakes keep the flat glossy arm. `IsOceanBaseTexture`
matches `wtr*`; C1 and C1C need `water1` matched EXACTLY, since the opaque `water1_trans1/2` coast
tiles must stay out. Inference, not measured: C5's fog-gradient passes would stay a flat layer over
the waves.

**Approach.** Scope per Decision 12. Order by risk: C2B as is; C1 and C1C with `water1` matched
exactly; then C2 (two-level sheet, coplanar beaches), C5 (fog-gradient overlay passes over the sea),
C3 (2090 coplanar shore polys, terrain pits below the sheet). Per chapter: lift the gate, check the
mask, shoot a coast and an open-sea pose against the flat sea. <TODO: how C5's fog passes and C3's
shore sheets are kept from lying flat over waves or being overrun by them.>

**Model recommendation.** high: per-chapter data reading, where a wrong classifier hides real sea or
leaves a z-fight.

**Verify.** A coast and an open-sea shot per chapter, flat vs ocean; the 8-chapter `--freecam`
regression from the ground rules.

**⚠ Traps.** Coastlines are coplanar texture blends at y = 0. Waves must reach zero under them, or
they z-fight or gap.

## C22 ☑ Ship calm zones for every hull and wake

**Landed.** The 16 round discs are 16 ship zones. A zone is a box on the water along the hull's
heading (its local +Z), grown each frame over the hull's waterline (the union, in the hull's frame,
of its meshes reaching within 2 m of its y = 0, measured once) and the world-space AABB of every
visible wake sheet it trails, so the extent is the wake geometry's own: the freighter's
`wakefront_left/right` are 54 x 23 m each and its `wakeback_*` 60 x 16 m. The waves are flat within
6 m of the box and back to full height 50 m further (`OceanCalmZone`, the shader's `ship_calm`). A
zone flattens only the geometry: the fragment normals keep the swell and chop, so the water around
the hull shades like the open sea and the deck lamps' pool (lit on the flat sheet too) breaks into
glints, where the disc showed as a smooth round patch. Sources: a visible `GameSession.OceanHulls` roster body, or the ship a wake
sheet hangs off (the sheet's mesh under its node under the ship; a wake node under the root is its
own hull), each only while its origin is within 3 m of sea level. `OceanMask`'s wake search now
takes `wakeback*` too and only flat meshes (under 20 m tall): C1's rock zeppelin wears `wakefront1`
over 180 m of its underside (`rock_zeppelin/underneath/g357`), which counted as a wake. A wake's
ship now gives up its surfaces to the mask, as the walker's comment always claimed; before, only
the wake node did, so the freighter's hull baked a 400 m calm blob at its bake pose (C1B/M03 and C1B
free flight) and the Red Cross ship one in C1/M05. Census (gamez nodes, mission anims, rosters):
- Wake sheets, C1B only in effect: the M03 freighter (`wakefront_left/right`, `wakeback_r1/r2/l1/l2`,
  three strands in the shot) and the escape boat's `eb_wakefront` (hidden on the hoisted lifeboat).
  C1/M05's Red Cross ship carries `redcross/wake` (230 x 52 m, flat), behind a generic intro over 40 s.
- Roster hulls (`SurfaceVehicle`, campaign builds only): C1B/M03's four `patrolboat_1..4`
  (deactivated and hidden until woken) and C2/M01's `eshipg31` generator launch. No wake sheets;
  their wakes are particle emitters, so each gets a zone over its waterline.
- World-animated hulls with no wake sheet and no roster block, which no source sees: C2/M01's
  `tugandbarge01..04` (path anims), C2's `yacht1..4`, C3's `barracuda` sub (`sub_movement`) and
  `leasure*` yachts, C5's `thugs` boats. The mask bakes each as solid at its build pose, so a moving
  one leaves its calm patch behind and the waves cross its hull.
Tests: `OceanCalmZoneTests` (heading box, mesh footprint under a turned transform, hull plus trailing
wake, the fade). The A1 cruise shot is pixel-identical (decoded md5 `6E4CA61F...`); `--no-ocean`
at the freighter pose is byte-identical before and after. Montages in the C22 tree's `.scratch\c22\`:
`montage-c22.png` (flat left, ocean right: before, after, after from 300 m) and
`montage-c22-closeup.png`.

**Verified.** The complete battery on the plan tree with C22 and C25 merged: units 6319 passed, 3 skipped; engine 529 passed, 2 skipped, errors clean (another session's battery ran beside it); the same three Enhanced goldens moved by the ocean, re-pinned at C24 after C23 changes the swell. The user approved the freighter montage (zone over hull and wake sheets, geometry flattened, shading kept).

**Original approach (kept for reference).**

**Goal.** Every moving hull keeps its wake sheets on the water without a calm disc that reads as an
artefact.

**Evidence (confidence: direction-sound).** In C1B/M03 the freighter is a world-animated node (start
anims `freightercruise`, `start_wakes`), not a `SurfaceVehicle`. Its `wakefront_left/right` sheets
track it, and `eb_wakefront` (the escape boat) sits hidden at y ≈ 20. Calm discs of 70-220 m cut
one wake strand; 140-320 m kept all three, at the cost of a visible calm area. The four M03 patrol
boats spawn deactivated. Sixteen disc slots.

**Approach.** <TODO: an elongated calm zone along the hull's heading instead of a disc, and the
roster hull census per chapter.>

**Model recommendation.** medium.

**Verify.** The freighter shot (`--chapter=C1B --mission=M03 --pos=-5440,90,-10050
--direction=0,0,1 --look=0,-0.3 --frames=2480`, the intro hands off at t = 40.2 s), plus a
patrol-boat shot once one is active.

**⚠ Traps.** M03's intro cutscene holds the camera for the first 40 s of any M03 session, campaign
or free flight.

## C23 ☑ Swell regularity from altitude

**Landed.** The cross-hatch was the four long swell waves (140, 91, 59 and 38 m): a shot with only
those reproduces it at the C2 350 m pose, one with only the short swell does not, and the chop is
invisible there. Two waves of similar height crossing at a fixed angle print a diamond lattice, and
the old lengths stepped by a near-constant 1.5. Two changes in `Effects/Ocean.cs`:
- The swell is twelve waves instead of eight (152 to 7.3 m, lengths stepping by about 1.3 with no
  common ratio, directions spread over plus or minus 71 degrees of the wind), with lower steepness
  on the long waves so the height and slope variance stay where they were. Chop stays six waves.
- A still world-space sea-state field (`sea_state`: three value-noise octaves of two channels, at
  500, 400 and 130 m, a sine-free hash) groups each wave's height (factor `1 + 0.8 * dot`,
  clamped 0 to 1.8) and shifts its phase (capped at 16 m of travel, 3 rad), each wave reading the
  field along its own golden-angle direction (`SWELL_MOD`, `CHOP_MOD`). The vertex stage and the
  fragment evaluate it at the same grid point; the normals leave its slope out. It has no time
  term, so the `csky_time` wrap holds; the per-wave grid-spacing fade is unchanged.
Wave counts per vertex 8 to 12, per fragment 14 to 18 (12 swell, 6 chop) plus the field. Foam keeps
its patch field and now measures crests against 3.13 standard deviations of the swell height
(`SWELL_CREST`), the old sum's ratio, so its share does not move with the wave count. Height over a
4 km square: standard deviation 1.16 to 1.24 m, 90th percentile 1.52 to 1.62 m, 99th 2.52 to 2.78 m.
`gpu_ms` at 1920x1080, ocean minus `--no-ocean` in the same round, four rounds after and two
before: cruise +0.30 to +0.42 ms, low pass +0.95 to +1.30 ms (Decision 11's budget is 1.5). The
same field passed as a varying from the vertex stage measured no cheaper within noise, so the
fragment evaluates it exactly. Montages in the C23 tree's `.scratch\c23\`: `montage-c23.png` (flat,
before, after at C2 350 m, C1B cruise, C1B low pass, C3 coast) and `montage-c23-detail.png`. The
three Enhanced goldens C21 moved move again (`c1-lake-enhanced`, `c5-city-night-enhanced`,
`c1-rocket-hit-enhanced`); every Original golden holds.

C3's remaining fine diagonal grain had two sources. In mid-distance it was the short swell (26 to
7.3 m), whose crests the 130 m phase octave left straight; a fine field (`sea_fine`, 40 m grouping
and 28 m phase, weighted fully from 10 m down and not at all from 40 m up) now bends them. Near the
plane it was the chop drawn at 2.5 to 4 pixels per wavelength, which aliases into a straight grating;
the fragment's footprint fade now runs from 4 to 9 pixels instead of 1.5 to 4, and the chop also reads
a 9 m micro field (`sea_micro`, fragment only). The fragment sums stop at the first faded wave (the
tables run longest first; `EmitWaves` throws otherwise), pixel-identical. Height statistics hold
(standard deviation 1.24 m, 99th percentile 2.81 m). `gpu_ms` over `--no-ocean` in the two quiet
rounds of four: cruise +0.45 to +0.47 ms against +0.43 to +0.44 before, low pass +1.45 to +1.46 ms
against +1.39 to +1.40, at Decision 11's 1.5 ms edge. The sun glint reads broader and smoother, as
fewer near-pixel normals break it. Montage `.scratch\c23\montage-c23-grain.png`.

**Verified.** The complete battery on the plan tree with C23 (and its grain follow-up), C26 and C27 merged: units 6337 passed, 3 skipped; engine 529 passed, 2 skipped, errors clean; goldens: the three Enhanced shots the ocean moves (re-pinned at C24), and `c1-cockpit-enhanced` flipped once to `f73c18a2...` straight after the engine stage, then held its pinned hash on two re-runs (C24 owns the flip). The user approved the swell montage and the grain follow-up (low pass +1.45 ms over `--no-ocean`, inside Decision 11's budget).

**Original approach (kept for reference).**

**Goal.** The swell shows no visible repeat from cruise altitude.

**Evidence (confidence: lead-only).** The foam lattice came from eight fixed swell waves, and the
user noticed it; the shading of those same waves may repeat too. Not yet judged on its own.

**Approach.** <TODO: more waves with jittered directions and lengths, or a noise-modulated amplitude
field.>

**Model recommendation.** medium.

**Verify.** The cruise montage pose, judged by the user.

**⚠ Traps.** The vertex-stage fade (full at eight grid steps per wavelength, none at four) is what
keeps waves the grid cannot carry from crawling. Keep it for any new wave.

## C24 ☑ An Enhanced ocean golden

**Landed.** `c1b-ocean-enhanced` pins the wave ocean at frame 120: `--freecam --chapter=C1B
--pos=-5900,120,-8350 --direction=0,-0.3,1 --det --mute --graphics=enhanced --water-quality=waves`,
hash `a1d94a8a...`. The pose looks across C1B's night islands, with open-sea swell and chop in the
foreground, the waves calming toward each shore, and the surf ring round the near island.
`--water-quality=waves` is explicit, so a change to the water-quality ladder cannot turn the
subject off. A freecam pose replaced the `--fly` placement the approach named, because the chase
camera's aircraft covered the near water. The ocean owns the frame: 67.9 % of pixels differ from
the same pose at `--water-quality=flat` (SHOT-29). Frame 120 against 121 differs in 15.68 % of
pixels, at a mean of 0.07 levels. The three Enhanced shots the ocean moves are re-pinned and their
`exercises` rewritten to name the ocean: `c1-lake-enhanced` `60cafb7f...`, `c5-city-night-enhanced`
`cbbd3e40...` and `c1-rocket-hit-enhanced` `a6fa3739...`. Their frame sensitivity was re-measured
(lake 23.60 %, rocket 89.65 %, city unchanged at 24.69 %). In the night city the ocean owns 425
pixels on the far waterline, and the shot with `--no-ocean` renders the old pinned hash.
`analysis/goldens/README.md` counts six Enhanced shots. Stability: the new hash held on all 14 of
this tree's goldens runs, each behind the quiet gate (no other godot, cargo or rustc process, GPU
under 12 % for 45 s). Four of those runs had no other session's Godot at any point, three ran
straight after an engine stage, and seven overlapped another session's battery that started
mid-run. Those seven count as the plan's external-load check.

`c1-cockpit-enhanced`'s single flip to `f73c18a2...` is one pixel at (1214, 675), one red level
off, in the haze beside the canopy frame. Its log matches a normal run's line for line. The shot
builds the ocean (C1's `water1` strip) though no sea is in view. The flip was not reproduced in 33
renders: this tree 14 goldens runs (3 after an engine stage), main's tree 13 (3 after an engine
stage), and 6 probes with a private, empty shader and pipeline cache (`APPDATA` redirected), 4 of
them beside a concurrent engine stage. The shared pipeline cache was last rewritten at the battery
that saw the flip, the first run after C23 changed the ocean shader. A cold compile is therefore
the likeliest cause, but a cold cache alone does not reproduce it.

**Verified.** A quiet goldens run on the merged plan tree: all 25 shots hash-identical to the manifest, the three re-pins and `c1b-ocean-enhanced` included. The cockpit flip did not reproduce in 27 quiet-gated runs across this tree and main (six of them with an empty shader and pipeline cache); the one flip frame differs from the pin by one pixel by one level, in the first battery after C23 changed the ocean shader.

**Original approach (kept for reference).**

**Goal.** A pinned `--det` shot shows the ocean, so a later change to it is caught.

**Evidence (confidence: traced).** `analysis/goldens/manifest.json` has `c1b-night-sea`
(Original, `--freecam`) and no Enhanced shot of any chapter's sea; the prototype moved no golden.

**Approach.** Add an Enhanced C1B shot through `--fly` placement, pinned on a stable frame.

**Model recommendation.** medium, low effort.

**Verify.** `.\RunTests.ps1` twice with the new hash stable, once under external GPU load.

**⚠ Traps.** A hash that flips under GPU load is a determinism defect, not noise. Pin the stable
frame and track the flip separately. The `exercises` field is rewritten on a re-pin, never appended.

## C25 ☑ The ocean matches the flat sheet at the shore and in fog

**Landed.** The ocean's fragment blends toward the flat sheet's shading by one weight, `sheet =
max(1 - shore amplitude, smoothstep(0, 0.25, fog amount))`: flat normals, the full texture
(`detail_mix` toward 1), no foam, `SceneBuilder.WaterRoughness` and `WaterSpecular` (now internal
constants the ocean reads). The ocean samples its texture through `csky_sample_albedo`, so C5's mip
bias reaches it as it reaches the sheet. The shore blend alone left a thin bright line on the tile
boundary: the mask's tint texture gave every texel off the base sheet the mean vertex colour, and
the shader's linear filter lightened the sheet's last half texel toward it. `OceanMaskRaster` now
gives an open texel its tinted neighbours' mean (`Dilate`, band-count independent; the test oracle
does the same, plus `WaterBesideTheSheetTakesTheSheetsTint`). At `c1-lake-enhanced`'s pose the
ocean-minus-sheet difference falls from mean 0.51 to 0.15 levels and pixels off by more than 4
levels from 59,259 to 1,475; no edge shows at the tile boundary, and the waves fade in over the
mask's 24-160 m ramp. Priority levels, zone groups and C5's level -11 are untouched (C5 coast shot
renders with no errors). C1B's cruise md5 moves to `3D0FD8B6...`: open water is identical, the
far band inside the fog ramp changed (waves no longer show through the fog there). Montages in
the C25 worktree's `.scratch\c25\montage-*.png`, flat / before / after. `-Filter graphics` on
this tree: 6 suites pass, units 6308 passed; of the goldens only C21's three Enhanced shots move
(`c1-lake-enhanced`, `c5-city-night-enhanced`, `c1-rocket-hit-enhanced`), every Original golden holds.

**Verified.** The lake pose: ocean minus flat falls from a mean of 0.51 to 0.15 levels, pixels off by more than 4 from 59,259 to 1,475; the hard edge is gone. C1C at 450 m and 60 m: the neutralised ocean matches the sheet to 0.1-0.6 levels on every row, so the fog term was never the gap; the open-water tone stays darker, which the user chose to keep. C1B cruise md5 moved to `3D0FD8B6...` in the far fog band only; open water byte-identical. The orchestrator battery runs on the merged tree with C22.

**Original approach (kept for reference).**

**Goal.** Where the waves calm to nothing, the ocean draws exactly as the flat sheet beside it, so no
edge shows against an opaque coast tile; and in a fogged chapter the far ocean fogs as the flat sheet
does.

**Evidence (confidence: direction-sound).** `c1-lake-enhanced`'s re-rendered golden shows a hard
straight edge in the lake where the ocean on `water1` meets the flat `water1_trans` coast tiles:
darker and wavier on one side, lighter and flat on the other, though the mask calms the waves there.
With waves, foam and detail neutralised and roughness at the sheet's 0.25, the ocean matched the flat
sheet to 0.65/255 (the prototype's equivalence check), so the shading constants are what differ
(roughness 0.2-0.3 vs 0.25, a 60 % texture mix vs 100 %). The C1C and C2B montages show the far ocean
clearly less fogged than the flat sheet in rain; cause unknown. The user accepted C21 with both filed.

**The fog cause (confidence: traced, measured at C1C 450 m and 60 m, luminance per row over a
sea column).** The fog term is not it. The ocean's FOG equals the sheet's (`csky_fog_on` is 1, the
same `csky_fog_amount` and per-view colour, both written to FOG the same way); SSR moves either surface
by under 2 levels (`--no-ssr`). With waves, foam and detail neutralised and roughness 0.25, the ocean
matches the sheet to 0.1-0.6 levels on every row, fog band included. Turning the ocean's fog off
changes only rows past C1C's 1000 m fog start. Below it the sheet's haze toward the horizon is
specular reflection of the overcast sky: with SPECULAR 0 the neutral ocean is a flat 70.5 on every
row, with it 81 rising to 107. The gap is the ocean's own shading. `detail_mix` 0.6 toward
`base_color` makes it 7-11 levels darker and greyer everywhere (near sea RGB 56/74/81 against the
sheet's 62/86/94; at `detail_mix` 1 it is 63/86/94), and in the partly fogged band its wave normals
show through. Roughness and foam move it by 1-2 levels. Lead, not verified: `base_color` is the CPU
mean of the archive's level 0, while the sampler reads `Build`'s mip chain with the authored levels
installed.

**Approach.** Blend the ocean's look toward the flat sheet's by the same shore amplitude that calms
the waves: at zero amplitude, flat normals, roughness 0.25, the full texture mix and no foam.
Diagnose the fog gap by bisecting with `--no-ssr`, roughness and normals against the flat sheet in
C1C before changing the fog term. <TODO: the fog cause.>

**Model recommendation.** high: a look change judged by eye, where the measurement can mislead.

**Verify.** `c1-lake-enhanced`'s pose with and without `--no-ocean`: no edge at the tile boundary.
C1C and C2B open-sea montages: the far ocean's fog matches the flat sheet's. C1B cruise looks as
approved (its md5 may move; send the montage).

**⚠ Traps.** The C1 coast montage at 250 m did not show the seam; the golden's low lake pose did.
Judge the edge where an opaque coast tile meets the open-sea tile, close and low.

## C26 ☑ No hole where a mission shows a node hidden at the bake

**Landed.** The base sheet's hide (`csky_ocean_hides_sea` in `csky_ocean.gdshaderinc`) takes the
fragment's world position and steps aside only where the ocean draws. It reads three globals that
`OceanMask.Publish` sets as the ocean enters the tree and `OceanMask.Withdraw` resets as it leaves:
`csky_ocean_mask` (the mask, sea coverage in R), `csky_ocean_zone` (the R8 zone groups) and
`csky_ocean_rect` (origin, 1 / extent). Their defaults are 1x1 textures (no sea, any zone), so the
hide is a no-op with no ocean live. Coverage is fetched texel by texel and filtered in the shader as the
ocean's linear clamp-to-edge sampler reads it, so no sampler state on a global can change it, and the
hide fires at `SeaThreshold` (0.02), the ocean's own `m.r < 0.02` discard. Zones: a seam texel
(`SeamZone`, 254) is one no grid draws, so the sheet keeps it; elsewhere the grid of the texel's own
group draws. The flat colliders and Original text are untouched (the hide lives in the Enhanced-only
lit water arm). `graphics-ocean-switch` now also checks the standing ocean's mask is the live one, the
include and the grid shader share the threshold, and the defaults return when the ocean leaves.
Evidence, montages in the C26 tree's `.scratch\c26w\` (flat / ocean before / ocean after):
- The crater (`--freecam --chapter=C3 --pos=-8773,220,-5100 --lookat=-8773,0,-4656
  --debug-damage=node=floodgate_healthy,kill --frames=1200`): before, the drained floor was a grey
  hole; after, the flat sheet draws there, as with `--no-ocean` (`montage-crater.png`). `--destroy=`
  kills at build, before the bake, so it cannot reproduce the hole.
- C1B cruise (`3D0FD8B6...`) and the `c1-lake-enhanced` args (`2D3CAB1E...`) are decoded-pixel identical,
  and so are `c5-city-night-enhanced` and `c1-rocket-hit-enhanced` against the old rule; those three
  goldens move on this tree by C21's ocean, not by C26.
- At the shore (`montage-shore.png`: C2 harbour and beach, C3 coast and beach, C5 from 900 m) only
  500-2,219 far shoreline and seam pixels move, and they move toward the flat sheet (C2 harbour: mean
  distance from flat 4.23 to 0.84 levels; C5's zone seam 0.60 to 0.05): slivers past the ocean's edge
  where the sheet hid and no ocean drew now show the sheet.

**Verified.** The complete battery on the plan tree with C23 (and its grain follow-up), C26 and C27 merged: units 6337 passed, 3 skipped; engine 529 passed, 2 skipped, errors clean; goldens: the three Enhanced shots the ocean moves (re-pinned at C24), and `c1-cockpit-enhanced` flipped once to `f73c18a2...` straight after the engine stage, then held its pinned hash on two re-runs (C24 owns the flip). The C3 crater floor after the floodgate drain draws as the flat sheet, where it showed a grey hole.

**Original approach (kept for reference).**

**Goal.** A node a mission shows after the mask bake does not leave the sea without water under it.

**Evidence (confidence: traced).** C21's mask walks only the visible tree, because C3's hidden
`swtr01`-`05` sheets over the crater floor made the ocean cover the valleys. A census of every
chapter's gamez and every `OBJECT_ACTIVE_STATE`/`ADD_CHILD`/`DELETE_CHILD`/motion event in the
compiled defs and reader zrdr files (scripts in the plan tree's `.scratch\c26\`) found one base sheet
hidden at the bake and shown later: C3's `craterlake>waterbottom` (7 `wtr00000` polys at y = 0, x
-9038..-8508, z -4921..-4391). The `RESET_STATE` of `craterlake-waterlevel_down-floodgate_healthy`
hides it; sequence 5 of the same def shows it when `floodgate_healthy` is shot, while `waterlevel`
sinks from 13.17 m and fades out over 15 s (IA1, M01, M04, M05, MP1-3). Inferred, not observed: the
build then shows a hole in the crater floor. The `swtr*` sheets are set by the mission script before
the bake and never change after it; the only other hidden-then-shown water is wake sheets, which are
not base sheets. No base sheet is visible at load and hidden later.

**Approach.** Hide a base-sheet fragment only where the mask says the ocean draws: the sheet's hide
samples the mask's sea coverage through a global and uses the ocean's own discard threshold, so each
texel has exactly one owner. `waterbottom` then draws as the flat sheet once shown, as in Original,
and any case the census missed falls back to the flat sheet rather than a hole. Rejected: baking
hidden sea-level sheets as sea (brings back C21's valley cover, about 90 area-toggled `g28xxx` tiles
per C3 mission), and re-baking when a node is shown (a full bake mid-flight, cost unmeasured).

**Model recommendation.** medium.

**Verify.** C3 at the crater: shoot the floodgate (or force the def) and capture the floor after the
drain, with and without `--no-ocean`; no hole. C1B cruise unchanged; `graphics-ocean-switch` green.

## C27 ☑ Calm zones for mission-animated boats with no wake sheet

**Landed.** The hulls come from the bound program, not a name list (`OceanMovers`). A mover is the
target of a played OBJECT_MOTION_FROM_TO with a translate channel, an SI script or an
OBJECT_TRANSLATE_STATE in a sequence, resolved as the dispatch binds it (`GameSession.AnimatedMovers`
asks `AnimRuntime.TargetsOf`, the dispatch's own rule). Reset states, spins and ballistic debris are not
movers. The mask walk judges each one it reaches visible: a hull has its origin within 3 m of sea level
over the world's own sea-level water (base sheet or surf-ring triangles that no mover carries) and a
triangle within 2 m of y = 0, and carries no base-sheet water. A hull's triangles and its nested movers'
leave the bake together, triangle order kept. A mover hidden at the bake is judged by the ocean once it
shows (visible, origin on sea level, a waterline, mask sea under its origin). Static hulls stay solid in
the mask as before, which already keeps the waves out of them. Zones: roster hulls, moving hulls and wake
ships compete for the 16 slots, nearest the camera first when more float; a hull riding inside another
listed hull where it first stood there takes no slot of its own (the tugs' `bmover`, the goose's
`local_xyz`). Census, freecam per chapter and campaign mission (`.scratch\c27\census-*.txt` in the C27 tree):
- Baked solid and now left out: C2/M01's `sprucegoose` and `tugandbarge01`-`04` (`tugandbarge03` sits
  on the channel ramp's surf-ring water), C5/M03's `sprucegoose`.
- Hidden at the bake, zoned once shown: C2's `yacht1`-`4` and `sailboat1`-`3` (every C2 session; the
  startup defs show them after the bake, so they never left a calm patch, but the waves crossed their
  hulls). Inferred from the rule, not shot: C3/M03's `barracuda` once it surfaces to y = 0 (it cruises
  at y = -6, outside the origin band), and the leisure yachts' `sinker` wrecks while they float.
- Not hulls: C3's `leasure*` yachts do not move (`boats_still.zrd`; only their wreck sinks) and stay solid
  in the mask; C5's `thug*` are people, not boats. C5/M01's `stein_ship` has its origin at y = -6.7, so it
  stays solid in the mask, which keeps the waves out while it floats; when it sinks in place the mask's
  calm stays where it went down. Trains, cars, zeppelins and hangar parts fail the waterline or origin test.
- C1, C1B, C1C, C2B: no moving hull; C1B and C1C free flight bake identically.
Evidence, montage `montage-c27.png` in the C27 tree's `.scratch\c27\` (flat / before / after): C2's
`sailboat3` 600 m from its start at frame 3600 (before, the swell covers its stern; after, flat water
under the hull), C2/M01's `tugandbarge01` after its run (`--debug-damage=node=kkgate,kill` frees the goose,
which calls `placebarge1`; frame 3000), and its load position after it left. That load position lies by
the quay, whose shore fade already calms the water, so the patch the old bake left there is faint (mean
0.46 levels over the frame). `--no-ocean` is byte-identical between the trees at all three poses. The C1B
cruise shot stays `3D0FD8B6...`. Goldens: the same three Enhanced shots move as on the tree before this
item, to the same hashes. Tests: `OceanMoversTests` (event rule, waterline band, sea test, ranking).

**Verified.** The complete battery on the plan tree with C23 (and its grain follow-up), C26 and C27 merged: units 6337 passed, 3 skipped; engine 529 passed, 2 skipped, errors clean; goldens: the three Enhanced shots the ocean moves (re-pinned at C24), and `c1-cockpit-enhanced` flipped once to `f73c18a2...` straight after the engine stage, then held its pinned hash on two re-runs (C24 owns the flip). The user accepted the boats montage (C2 sailboat3, tugandbarge01).

**Original approach (kept for reference).**

**Goal.** A boat a mission animates across the sea keeps the waves out of its hull and leaves no calm
patch where it stood at load, whether or not it carries a wake sheet.

**Evidence (confidence: direction-sound).** C22's census: C2/M01's `tugandbarge01`-`04`, C2's
`yacht1`-`4`, C3's `barracuda` sub and its `leasure*` yachts and C5's `thugs` boats move by
mission animation, carry no wake sheet and have no roster entry. The mask bakes each as solid where it
stands at load, so it leaves that calm patch behind, and the waves pass through its hull once it moves.
Only hulls with wake sheets (C1B's freighter, C1/M05's Red Cross ship) and roster hulls get a zone today.

**Approach.** <TODO: find these hulls from the data (an animated world node whose meshes reach the
waterline), feed them to the ship zones as `OceanCalmZone` hulls, and leave them out of the mask
bake as wakes' ships already are.>

**Model recommendation.** medium.

**Verify.** <TODO: a pose on one tug or yacht after it has moved, with and without `--no-ocean`.>

**⚠ Traps.** C22: `--direction` turns the view the opposite way on x in this mode; a hull counts only
while its origin is within 3 m of sea level.

## B14 ☑ No secondary viewport draws the ocean grid it does not need

**Landed.** Per viewport first. In split screen the root viewport draws no 3D (`SplitScreen` sets
`Disable3D`), so the world goes through each pane camera and each pane's spyglass disc. The census
now counts disc primitives: at the 2-pane C1B cruise the two discs submit 542k of the frame's 1.11M
prims with the ocean (6.9k with `--no-ocean`), and at four panes 1.09M of 2.23M, so every disc drew
the whole grid, as B12 inferred. No other viewport shares the world on every frame: the cockpit pass
(`CockpitOverlay`) and the load warm-up viewport (`EnhancedLook.WarmAdvancedVariants`) own empty
worlds, and the Danger Zone photograph renders one frame per latch through the pane's own mask and
keeps the waves, since it is a picture of the pane's sea. The race ghost is a shader, not a viewport.

The disc's camera now carries `SplitScreen.FlatSeaLayer` (bit 5, layer 6; no geometry or light uses
it) under Enhanced (`SpyglassView.DiscMask`), and `PlayerCullMask`/`PaneCullMask` drop it.
`SceneBuilder.FlatSeaEye` is the shader test, `(CAMERA_VISIBLE_LAYERS & (FlatSea | Sun)) == FlatSea`:
the sun's bit keeps a mask with every bit set (a fresh camera, maybe a shadow pass) from reading as
a disc, and the Enhanced disc is the only camera without the sun's layer. The grid's vertex stage
puts every vertex behind the eye before any wave math; the sheet's discard reads
`!FlatSeaEye && csky_ocean_hides_sea(...)`. `CAMERA_VISIBLE_LAYERS` compiles in both stages under
Godot 4.7 (no shader error, and the disc draws the sheet). The `--perf` spyglass line gains
`disc_prims` and `disc_gpu_ms` and is now read on the render thread: its `GetRenderInfo` calls from
the main thread had waited on the render thread on every frame a disc rendered (4,752 Godot
sync warnings in one 600-frame 2-pane run), which held split-screen `--perf` frames at 27 to 49 ms
on a shared machine. `prims` and `disc_prims` do not move with the fix, since the disc still submits
the grid and collapses it; the cost is in `disc_gpu_ms`.

Desktop RTX 5080, 1920x1080, `--det --perf --no-vsync --frames=600`, C1B cruise pose, means over
windows from sim frame 240. "Before" is this tree with the marker left off the disc (same census),
"after" the fix. Every row is two rounds with arm order rotated, each run passing the quiet gate
before and after (no other godot, cargo or rustc process, GPU under 12 %); five runs that failed
it were discarded and re-run:

| Panes | Arm | `frame_ms` | `disc_gpu_ms` (all discs) | prims / disc prims |
|---|---|---|---|---|
| 2 | before, ocean | 9.24, 8.33 | 0.407, 0.406 | 1.11M / 542k |
| 2 | after, ocean | 9.23, 8.43 | 0.267, 0.268 | 1.11M / 542k |
| 2 | before, `--no-ocean` | 9.41, 9.40 | 0.266, 0.259 | 40k / 6.9k |
| 2 | after, `--no-ocean` | 9.37, 9.58 | 0.255, 0.257 | 40k / 6.9k |
| 4 | before, ocean | 8.34, 8.36 | 0.810, 0.806 | 2.23M / 1.09M |
| 4 | after, ocean | 8.36, 8.36 | 0.539, 0.537 | 2.23M / 1.09M |
| 4 | before, `--no-ocean` | 8.33, 8.34 | 0.481, 0.478 | 85k / 23k |
| 4 | after, `--no-ocean` | 8.33, 8.34 | 0.481, 0.480 | 85k / 23k |

The discs' ocean cost falls from +0.15 to +0.01 ms at two panes and from +0.33 to +0.06 ms at four
(the residue is the collapsed vertex stage over 134k vertices, twice per disc). `frame_ms` does not
resolve it: under `--det` the frame loop holds 120 fps, and every desktop split-screen arm sits at
or near that 8.33 ms floor, with the 2-pane arms scattering by 1 ms between runs either way. The
Deck, where B12 measured +12 to +15 ms for waves in split screen, was not re-measured.

Evidence in this tree's `.scratch\b14\`: `montage-disc-low.png` (2-pane held low pass at 2560x1440,
each pane's disc before, after and `--no-ocean`, 3x crops) and `montage-disc-cruise.png` (the cruise
pose). Both discs after the fix are pixel-identical to `--no-ocean` inside the disc (0 of 2,121 pixels
differ at the low pass; before, 1,101 to 1,169 differ by about 9 levels), and the before/after frames
differ only inside the two discs (3,653 pixels, none outside), so the panes keep the waves.
Identity: the single-pane C1B cruise shot decodes to `6C876AAE...` on the plan branch build and on
this tree, and the Original cruise shot to `86BD180F...` on both. Tests: `spyglass-sun` checks each
disc carries the marker under Enhanced and no pane does; `graphics-ocean-switch` checks the grid and
the sheet read `FlatSeaEye`; `SpyglassTests.TheSunLayersAreTheirOwnAndOnlyTheDiscDrawsTheCopy` pins the
bit against every reserved band.

**Verified.** The complete battery on the plan tree with B14 merged: units 6337 passed, 3 skipped; engine 529 passed, 2 skipped, errors clean; goldens: the three ocean shots C24 re-pins, at the same hashes as before B14, and `c1-cockpit-enhanced` held. Inside each disc the frame matches `--no-ocean` pixel for pixel, the panes keep their waves, and the single-pane cruise shot is unchanged.

**Original approach (kept for reference).**

**Goal.** The ocean grid draws once per pane camera, not again in each pane's spyglass disc or any
other secondary viewport that does not show the sea.

**Evidence (confidence: lead-only).** B12 on the Deck: primitives go from about 10k flat to 280k with
waves at one pane, 1.1M at two and 2.2M at four, with draw calls unchanged. 4x and 8x fit each pane's
spyglass disc (its own viewport, `SpyglassView`) also drawing the 134k-vertex grid. Inferred from the
counts, not from a per-viewport split. Split screen on the Deck costs +12 to +15 ms with waves.

**Approach.** Decision 13: confirm per viewport first (`--perf`'s spyglass line, PERF-47). Then give the
spyglass disc camera a marker layer bit no geometry uses; the ocean grid's vertex stage collapses and
returns before its wave math when `CAMERA_VISIBLE_LAYERS` carries it, and the base sheet's hide does not
hide when it does, so the disc shows the flat sea and no hole. Re-measure B12's split-screen rows on the
desktop (`frame_ms`).

**Model recommendation.** medium.

**Verify.** <TODO: the per-viewport prim counts before and after, and B12's 2- and 4-pane rows.>

**⚠ Traps.** In split screen `gpu_ms` measures the root viewport alone (PERF-39): read `frame_ms`.

---

# Wave D, A3's look findings

D31 and D32 both edit the fragment stage of `Effects/Ocean.cs`'s generated shader and run in
sequence. D33 edits the shore ramp (`Effects/OceanMaskRaster.cs`) and the shore weight in the same
shader, so it runs after D32 or in its own tree with a hand-merged shader. Each comes back as a
montage for the user; C24's four Enhanced goldens move and are re-pinned once, after the last.

## D31 ☑ Break up the swell's wave lattice

**Landed.** Three patterns, each found by shots at 1920x1080 with parts of the sea switched off:
- At C1B cruise the criss-cross is the original's water texture (`wtr00000`, 32x32 texels) repeating
  every 64 m tile. It survives with every subset of the swell and with the chop alone, and goes with
  the texture off; the flat sheet shows the same grid, and on the ocean it swayed with the swell's
  horizontal displacement.
- At C2 at 350 m the middle swell (57 to 26 m) alone prints diamonds, the long four straight bands.
- In the open water beside the plane at C2 at 350 m (the region the user marked), the fine woven
  cross-hatch is the short swell (19.5 to 7.3 m): a shot with only those four waves reproduces it,
  the chop alone draws nothing there (its footprint fade has removed it), and the chop off, the
  chop's micro field off and `--no-ssr` each leave it unchanged.

Changes in `Effects/Ocean.cs`:
- Open water takes the texture's last-mip mean times the tint, and only the light varies it. The
  sheet's own texture mapping returns as `sheet` goes to 1 (calmed shore, fog), so C25's seam reads
  the sheet's texture. The user chose this over a broken-up texture; `detail_mix`, `base_color` and
  its CPU mean (`MeanColor`, darker than what the GPU samples) are gone.
- The sea-state phase channels are a warp with an analytic slope (`sea_warp`, `sea_noise_d`): 360
  and 90 m octaves, the fine one half the weight (were 400 and 130 m at 0.4), up to 60 m of crest
  shift (was 16) and 14 rad (was 3). Each wave's crests bend along its own golden-angle projection,
  so crossing crests drift apart; the middle swell, which C23's caps held to 12 to 16 m of shift,
  now moves 58 to 60 m. Each swell wave's local wavenumber (`sea_wavevector`, its own plus the
  warp's slope) drives the vertex grid-spacing fade, the fragment footprint fade and its normals,
  capped at 1.5 times the wave's length (`WarpStretchMax`) so the longest-first early exit stays
  exact. The vertex stage and the fragment evaluate the same field at the same grid point, with no
  time term. 80 m and 20 rad read as swirls and rings.
- The swell is the eight waves from 152 to 26 m. The four short waves and the six-sine chop are
  replaced in the fragment normals by five gradient-noise layers (`DetailLayers`: cells of 6, 3.4,
  1.9, 1.05 and 0.58 m, each drifting along its own direction at 0.7 to 1.9 m/s; `detail_noise`
  with an analytic gradient; slope 0.08 per layer, varied by the grouping field). Noise has no fixed
  crests, so crossing layers print no weave. Each layer fades with the footprint as the chop did (4
  to 9 pixels over two cells), and the sum stops at the first faded layer (`EmitDetail` checks the
  order). The micro field and the `CHOP_*` tables are gone.
- The drift holds the `csky_time` wrap: a layer's hash takes the cell's x modulo 1024
  (`DetailPeriod`) along its drift axis, and its drift is rounded to a whole number of those periods
  per 3600 s (1024, 2048, 2048, 3072 and 4096 cells for the five layers), so the rollover lands on
  the same field. The shortest period is 594 m along one axis only; the literal's rounding leaves
  under 0.002 cells of jump.

C25's seam at `c1-lake-enhanced`'s pose (1280x720, ocean minus `--no-ocean`): before mean 0.15 levels,
759 pixels off by more than 4; now 0.14 and 616. Height over a 4 km square (`heights.ps1`, 40 000
samples): standard deviation 1.23 m, 90th percentile 1.61 m, 99th 2.70 m, against 1.25, 1.64 and
2.81 m for C23's field on the same samples; the four short waves carried about 2 % of the variance.
`gpu_ms` at 1920x1080 over `--no-ocean` in the same round, two quiet-gated rounds (`.scratch\d31\ab.ps1`): low-pass hold before +0.95 and +0.92 ms, now +1.19 and +1.17 ms (Decision 11's budget is 1.5); cruise before +0.42 and +0.42 ms, now +0.49 and +0.58 ms. Near the water all five noise layers are live, which costs about 0.25 ms more than the six chop sines did.
Montages in the D31 tree's `.scratch\d31\`: `montage-d31-r3.png` (flat, before, round 2's no
texture and round 3 at C2 350 m, C1B cruise, the C1B low-pass hold and the C3 coast),
`montage-d31-r3-mark.png` (the user's marked region at C2 350 m, 3x) and
`montage-d31-r3-cruise.png` (a 640x360 crop of C1B cruise, contrast-stretched).

**Verified.** The user chose no texture over the stronger texture breakup, marked the remaining fine weave on round 2, and accepted round 3; the motion judgement comes at the Wave D re-check flight. In the D31 tree: units 6337 passed, 3 skipped; `graphics-ocean-switch` and `graphics-water-quality` pass, engine errors clean. Owed: the complete battery with the Wave D re-pins.

**Original approach (kept for reference).**

**Goal.** The swell shows no regular crossing pattern at the user's flight altitudes; the crests read
as an irregular sea.

**Evidence (confidence: the user's eyes).** A3: "can we reduce/breakup the grid pattern it still very
visible perhaps through a noise map?", and the pattern is the wave lattice (the user's answer). C23
already moved to twelve swell waves with no common length ratio and a still sea-state field that
groups each wave's height and shifts its phase (capped at 16 m and 3 rad) along its own direction,
plus the fine field for the short swell. The crests still run straight between those modulations,
so two long waves crossing still print diamonds wherever the field is near neutral.

**Approach.** Find which waves print it at C1B cruise and the low pass, as C23 did (shots with
subsets of the swell). Then bend the crests rather than only regroup them: a world-space domain warp
of the position the long waves read (a still, low-frequency noise displacement of tens of metres,
the "noise map"), stronger phase modulation, or direction jitter per region. Keep the field still
(no time term) so the `csky_time` wrap holds, and the vertex stage and the fragment on the same
evaluation.

**Model recommendation.** high: a look change judged by eye.

**Verify.** A montage, before and after, at C1B cruise, C1B low pass (`--hold=0.04,0,0,0.7`) and
C2 at 350 m, judged by the user. `gpu_ms` over `--no-ocean` inside Decision 11's 1.5 ms at the low pass
(C23 left it at +1.45 to +1.46). Height statistics over a 4 km square near C23's.

**⚠ Traps.** The vertex-stage fade (full at eight grid steps per wavelength, none at four) keeps the
grid from crawling; a warp changes the effective wavelength locally, so keep the fade on the warped
length. A warp evaluated differently in the vertex and fragment stages swims the normals against
the surface.

## D32 ☑ Foam that reads as foam up close

**Landed.** The glitch was the foam's hash. `foam_hash` was `fract(sin(dot(c, k)) * 43758.5453)` on
world-scale cells (dot products of 5e4 to 4e5 at C1B), so the GPU's rounding of a corner computed from
its two neighbouring cells differed, and the 43758 gain turned that into a different value: the value
noise jumped at cell boundaries. Foam alone at the low-pass hold showed straight-edged quads and cut
blobs from the 230 m and 71 m patch octaves (they stay with the 9 m breakup off); the same frame with a
sine-free hash has none. In motion those blocks slide through the view on the patch drift
(`.scratch\d32\strip-d32-low-foam.png`, `strip-d32-low-before-consecutive.png`); they are also D33's
blocky whitecaps beside the C1B rocks. No frame-to-frame flicker was found: consecutive frames match.

Changes in `Effects/Ocean.cs`'s fragment stage:
- Foam forms where the swell bunches the surface. The swell loop sums the horizontal displacement's
  slope (`bx`, `bz`, from each wave's local wavevector and its footprint fade) and the foam reads the
  Jacobian's drop below 1 in standard deviations (`FOAM_JAC_SIGMA` = Choppiness / sqrt(2 x waves)),
  gated between 1.3 and 2.3 of them (`FoamFromSigmas`, `FoamFullSigmas`), so a whitecap grows and
  fades with its crest. Crest height (`SWELL_CREST`, `CrestSigmas`) is gone.
- All foam noise is gradient noise on `sea_hash` (`foam_noise`): the patch field (230 m and 71 m,
  weights 0.65 and 0.35, shifting the threshold by up to 1.2 sigmas) and a 12 m breakup. The patch
  field drifts along the wind by one whole period of cells per `csky_time` wrap (23 and 76 cells,
  1.47 and 1.50 m/s; `FoamPatches`, `FoamPatchTerms`), its modulo offset by half a cell so no corner
  hashes differently in two cells. The breakup and the near shape ride the grid parameter, the water.
- Up close (cell of 1.4 m at 4 to 9 pixels, the detail layers' footprint fade) a whitecap becomes a
  sheet with round holes (`foam_holes`, one hole per jittered cell, radii 0.35 to 0.75 cells), warped
  and thinned by 3.2 m noise stretched 1.8 times along the wind. It covers about 30 % of the crest's
  foam at `foam_strength` / 0.3 (`FoamCover`), so its mean is the far foam. Foam takes roughness 0.6
  in the share it covers. It stays off at the shore look ramp (`1 - sheet`).

Far share: foam alone over the C1B cruise frame's upper half averages 2.92 levels, against 2.66 before
and 3.03 before D31, with brighter, larger flecks than before. `gpu_ms` at the low-pass hold,
1920x1080, ocean minus `--no-ocean` in the same round, two quiet-gated rounds (`.scratch\d32\ab.ps1`):
before +0.98 and +0.99 ms, after +1.03 and +1.02 ms (Decision 11's budget is 1.5). Montages in the D32
tree's `.scratch\d32\`: `montage-d32.png` (flat, before, after at the low-pass hold, 10 m over the
water, C1B cruise and the C1B coast), `montage-d32-near.png`, `montage-d32-cruise.png` (with the
pre-D31 foam), `montage-d32-coast.png` and `montage-d32-coast-foam.png`; strips `strip-d32-near.png`
(every sixth frame over half a second) and `strip-d32-near-consecutive.png`. Noticed, not changed: at
10 m a nearer crest's faceted silhouette cuts the foam behind it along a grid edge.

**Verified.** The user accepted the montage; the motion judgement comes at the Wave D re-check flight. In the D32 tree: units 6338 passed, 3 skipped; `graphics-ocean-switch` and `graphics-water-quality` pass, engine errors clean. Owed: the complete battery with the Wave D re-pins.

**Original approach (kept for reference).**

**Goal.** Close to the water the foam reads as foam (broken, streaky whitecaps on the crests) and
does not flicker or pop; from a distance it keeps the look the user approved.

**Evidence (confidence: the user's eyes, cause lead-only).** A3: "the foam in c1b looks good from a
distance but not really like foam if near it (kinda glitchy)". The foam today (`Ocean.cs`, about
line 611) is a flat grey `mix` toward (0.6, 0.65, 0.68) at strength 0.12, gated by three bilinear
value-noise fields (230 m and 71 m patches drifting, a 9 m breakup) and a smoothstep of the crest
height. Leads, not verified: bilinear value noise shows square cells up close; the hard smoothstep
on the crest height pops as a crest crosses it; there is no detail below 9 m at all.

**Approach.** First find what "glitchy" is, in motion, at the low-pass hold: a frame strip with foam
alone, `--no-ssr`, and the breakup field off. Then give the foam a near-field look: small-scale
structure (a cellular or streaked noise along the wave direction, fading in with proximity as the
chop's footprint fade does), a softer crest gate, and coverage that holds still relative to the
crest it rides. Far foam unchanged.

The reference is the inspiration's foam (emilje/godot-water-shader, `foam_calc.gdshader`; frames of
the author's latest video in `Z:\CSVM\.scratch\ocean-proto\reference\`): foam where the waves bunch
up, from the displacement's Jacobian, broken by a foam texture at two scales and noise. Our Gerstner
sum gives the Jacobian analytically per fragment. The user's ruling: the foam does not need to be as
finely structured as the reference's lace; coarser patches that read as foam up close are enough.
Lingering foam (the reference's history buffer, decayed on wall time over a fixed 10 m patch) does
not carry to a camera-centred ocean on the sim clock and is not this item's.

**Model recommendation.** high: a look change judged by eye.

**Verify.** A montage at the low-pass hold and closer (about 10 m), before and after, with a frame
strip showing the motion; the C1B cruise pose unchanged to the eye. `gpu_ms` at the low pass.

**⚠ Traps.** Stills cannot show flicker: judge with consecutive frames. Foam is on `csky_time`, never
`TIME`.

## D33 ☑ A gradual fade to flat water at the coast

**Landed.** The cut was the look, not the height. The fragment scaled the swell and chop normals by
the mask's height weight and then blended toward the sheet by one minus the same weight, so the
visible waves rose roughly with its square: flat out to about 90 m from any shore, surf or solid
texel and nearly full only past 130 m. In C2's harbour (a channel about 190 m wide, with the Eiffel
plinth, the ramp `g36353`'s foot and small solids inside it) no texel reaches full height, and the
breakwaters left a flat band across the whole mouth. The mask's G is now the shore distance over
`OceanMaskRaster.ShoreReach` (160 m, saturating), and the shader reads two ramps from it
(`Ocean.cs`'s `ShoreCalm`/`ShoreFull`, `LookCalm`/`LookFull`): the swell's height over the same
24-160 m as before, and the look over 12-64 m. The fragment's swell and chop normals run at full
amplitude, as they already did in a ship's zone; `sheet = max(1 - look, fog)` alone calms them.
LookCalm clears the texel beside a coast texel, which the linear filter blends into the boundary.
The mask stays RG8 and band-count independent (`OceanMaskRasterTests`, the oracle encodes the
distance; `TheShoreChannelIsTheDistanceFromASolidCorner` holds 0, 128 at 80 m, 255 past the reach).
Re-encoding alone is near byte-exact: the old ramps applied to the distance reproduce the old C2
150 m shot to 0.005 levels (2 pixels off by more than 4). A shorter height ramp (24-96 m) was shot
as a second variant and reads the same as the look ramp alone at both harbour heights, so the
height ramp is unchanged and no displaced crest reaches the coast layers any closer than before
(low beach shots at C2 and C3 match). C25's seam at `c1-lake-enhanced`'s pose: the ocean-minus-sheet
profile across the coast tile boundary is unchanged within 20 px of it (-0.42 levels on the ocean
side, 0.00 on the tile, before and after), so no edge; the whole-frame difference rises from mean
0.145 to 0.666 levels (1,329 to 52,871 pixels off by more than 4) because the lake's open water now
shows waves. C5's golden pose is byte-identical. Open-sea whitecaps now form nearer small rocks,
where the swell ramp used to suppress them; their blocky edges are D32's value noise. `gpu_ms`
at the low-pass hold is not measured yet: the quiet gate never cleared (external GPU load at 55-69 %
with no Godot running). The change adds one smoothstep per vertex and per fragment and leaves the
wave loops' lengths as they were (they end on the footprint fade, not the amplitude). Montage, flat / before / after: C2 harbour at 30 m and 150 m, C1B coast, C3 shore at 120 m
and 250 m, C1 lake, in the D33 worktree's `.scratch\d33\montage-d33.png`.

**Verified.** The user accepted the montage; the motion judgement comes at the Wave D re-check flight. In the D33 tree: units 6337 passed, 3 skipped; `graphics-ocean-switch` and `graphics-water-quality` pass, engine errors clean. `gpu_ms` at the low-pass hold, 1920x1080, ocean minus `--no-ocean` in the same round, two quiet-gated rounds alternating the trees: before D33 +0.59 / +0.63 ms, after +0.56 / +0.55 ms, so the second ramp costs nothing measurable. Owed: the complete battery with the Wave D re-pins.

**Original approach (kept for reference).**

**Goal.** The waves calm toward the coast gradually, with no visible cut, and a narrow harbour keeps
waves across most of its width rather than a thin stripe.

**Evidence (confidence: the user's eyes, cause traced).** A3: "the cut to costal flat water is
really strong. especially on holywood in the harbour where only a small stripe of waves is
present." The mask's wave height is 0 within `ShoreCalm` (24 m) of any shore or surf texel and full
at `ShoreFull` (160 m) (`OceanMaskRaster.cs`), so water narrower than about 320 m never reaches full
height. C25 then blends the whole look toward the flat sheet by the same amount (`sheet = max(1 -
m.g, ...)`, about `Ocean.cs:602`): normals, chop, foam and texture mix go flat together with the
height, which makes the band read as a cut.

**Approach.** Separate what must be flat at the shore from what need not be. The C25 seam only needs
the look to match the sheet where an opaque coast tile or the surf ring meets the ocean, which is a
short distance; the swell height needs a ramp long enough that displaced waves do not cut through
the shore. Candidates: a short look ramp and a separate height ramp; a shore ramp scaled to the
local water width (distance to the farther shore), so a harbour channel reaches its own peak; chop
and foam surviving closer in than the swell. Measure C25's tile-boundary edge at `c1-lake-enhanced`'s
pose again after the change.

**Model recommendation.** high: a look change judged by eye, with C25's seam to keep.

**Verify.** A montage, before and after, of C2's harbour (low and at about 150 m), a C1B coast and
C3's shore, judged by the user; `c1-lake-enhanced`'s pose shows no edge at the coast tile boundary.

**⚠ Traps.** If the coast cannot be made gradual without the C25 seam or waves cutting through the
shore, stop and report: Decision 15 then files the procedural-coast issue rather than this item
forcing it.

---

# Wave E, per-chapter seas

## E41 ☐ An ocean lab and a shipped sea state per chapter

**Landed.** The ocean's tunables are one record, `Effects/SeaState.cs`, its defaults the constants
it replaces: swell height (`wave_scale`), a length scale on every wave, wind, crest sharpness, the
bend's metres and radian cap, detail slope and drift, the foam's threshold, strength, patch size and
near cover, the swell and look ramps, an open-sea tint and both roughnesses. `Clamped` holds each in
range and is the one path into the shader. Folding depends on sharpness times height, not on the
length: each wave bunches the surface by Q k A, which a length scale leaves alone. So the bound is
sharpness times height at 1.0 (1.82x at the default sharpness), and the slider says so. The ramps
stay within `ShoreReach` and at least 4 m wide. The shader text moved out of `Ocean.cs` into
`Effects/OceanShader.cs`, which writes it from a sea; with the defaults it is byte-identical to the
constants' text for levels -1 to 3, zoned and not (`OceanShaderTests`, hashes taken from the
unchanged generator, plus a fixture), and all 25 goldens are hash-identical. A length scale
re-rounds each omega to whole cycles per wrap; detail and patch drifts stay whole periods per wrap
(a held drift may reach zero). `SceneBuilder.FlatSeaEye` became a property so the generator runs in
the unit host. `CSVM/data/ocean_seas.json` ships an empty entry per sea chapter
(`Effects/OceanSeas.cs`); a mistake is a `world` warning. The lab (`UI/Labs/OceanLab.cs`, Shift+F1)
is built in `--freecam` on a sea chapter only, applies on release or a quarter second after a click,
hands each edit to the standing ocean and to any ocean a switch rebuilds, and saves only the
chapter's differing fields, in field order, from the source tree. `--debug-ocean` is the scripted
twin. The `ocean-lab` suite opens the lab in a C1B freecam, finds a length edit rewriting the text,
a height edit setting only the uniform, one ocean throughout, the edited sea surviving a switch
round trip, and no lab in a `--fly` session. `gpu_ms` at the low-pass hold, 1920x1080, two
quiet-gated rounds alternating the trees: the plan tree 3.429, 3.423 and 3.416, 3.417 ms, this
change 3.423, 3.419 and 3.416, 3.417 ms (`--no-ocean` 2.093 and 2.129), so it does not move. Montage, defaults / rough / calm at C1B cruise
and at the golden's 120 m pose, and the panel open, in the E41 worktree's `.scratch\e41\`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The user tunes each chapter's sea live in `--freecam` and saves it; the game loads each
chapter's saved sea. With nothing saved, every chapter draws exactly today's ocean.

**Evidence (confidence: traced).** Every wave, detail and foam value is a constant written into the
generated shader text (`Ocean.ShaderCode`, `EmitWaves`, `EmitDetail`, the `Foam*`, `Phase*`,
`Detail*`, `Shore*`/`Look*` constants), plus three uniforms nothing sets (`wave_scale`,
`foam_strength`, `rough_near`/`rough_far`). Nothing reads a per-chapter value. The user: "How can i
change the parameters for the chapters myself? Could you make me an ocean lab for the --freecam
mode so that i can test it live?" Decision 16 holds their answers.

**Approach.**
- A sea-state record (one value per tunable) with today's constants as its defaults, and a shipped
  data file (`CSVM/data/ocean_seas.json`, read through `res://data/` as the stock loadouts are) with an
  entry per sea chapter (C1, C1B, C1C, C2, C2B, C3, C5); a missing entry or field takes the default.
  `Ocean.Create` takes the chapter's sea state and `ShaderCode` writes from it. With the defaults,
  the generated text is byte-identical to today's, so no golden moves (a unit test holds it).
- The parameters, per Decision 16: swell (height, a length scale on every wave, wind direction,
  crest sharpness `Choppiness`), crest bending (the warp's metres and radians) and noise detail
  (slope, drift speed), foam (threshold, strength, patch size, near cover), coast (the swell and
  look ramps, within the mask's `ShoreReach`, or a rebake), tint and roughness. Clamp each to a
  range that keeps the invariants below.
- The lab: `--freecam` only, Shift+F1 toggles a panel of sliders, built like the existing labs
  (`UI/Labs/`). A change rebuilds the ocean's shader from the edited state (a brief compile is
  fine in a lab); Reset to defaults, Revert to saved, and Save for this chapter, which writes the
  project's data file when running from the source tree and says where it wrote. The scripted twin
  `--debug-ocean=<field>:<value>,...` applies the same overrides at launch, for screenshots.

**Model recommendation.** high: a new lab plus a data path through the shader generator, with
invariants that a slider can break.

**Verify.** The defaults' shader text byte-identical to today's (unit test) and the goldens
unmoved; an engine suite that opens the lab in `--freecam`, moves a value, and finds the ocean's
shader changed and the session intact; Save round-trips through the file; the lab is absent outside
`--freecam`. A montage of two lab settings at C1B cruise for the user, and the user's own tuning at
the Wave D re-check flight.

**⚠ Traps.** The 3600 s `csky_time` wrap: a swell length change moves omega, which must be re-rounded
to whole cycles per wrap (`EmitWaves` does it), and a detail or foam drift speed must stay a whole
number of hash periods per wrap. Height alone folds the crests near 1.8x (Decision 14); clamp height
against the length scale or say so on the slider. Shore ramps past `ShoreReach` (160 m) need the mask
rebaked. The values are shipped data, so a network peer and a `--det` capture agree as long as both
load the same file. Shift+F1 sits beside `ControlCapture`'s bindable F1 to F12; the lab exists only in
`--freecam`, where no flight controls are read.

## E42 ☑ A shader rewritten for Enhanced with no wearer compiles before Enhanced's first frame

**Landed.** `ShaderTwins.RetextInPlace` puts every shader it rewrites on a `ShaderMaterial` of its
own (`Pin`, which asks for the material's RID so the server material exists), and `Regenerate` and
`ReleaseUnused` drop the pins once `Engine.GetFramesDrawn()` has moved by 2. Godot's `_draw` runs
the scene update (`update_dirty_instances`, then `update_dirty_resources`, then
`_update_queued_materials`) before `draw_viewports`, and every advanced-group enable sits inside a
viewport's render. So the pin's queued update reaches `version_get_shader` on the dirty version
first, which rebuilds the base variants from the new text; the enable then builds the advanced ones
from the same text. No `RenderingServer` call reaches `version_is_valid` or `version_get_shader`
synchronously, so a wearer is the only way to force the compile. A shader never given an RID needs
no pin, but C# cannot tell, so every rewritten shader gets one. The `graphics-retext-compiles`
suite builds a synthetic key in Original (a 48-byte uniform block), compiles it, frees its only
material, switches to Enhanced with `EnhancedDrawn` false, draws one TAA frame, puts a fresh
material on the key and reads it green (Enhanced, 16 bytes), the rewritten shader pinned, the
Original text on a shader of its own reading red, and no new engine error line in the run's log
(`TestHarness.EngineErrorsSoFar`). With the pin disabled it fails alone: not pinned, the pixel the
clear grey, and 6 engine errors, the same "Uniform buffer supplied (binding: 0) size (16) is smaller
than size of shader uniform: (48)" then "Parameter "us" is null". It can show the draw fail only in
a process that has drawn no TAA frame before it; the pin check fails in any. The suite draws nothing
before the rewrite. `ShaderData::set_code` marks the version dirty before `clear_pipelines` waits for
the shader's background pipeline compiles, so a compile still in flight calls `version_get_shader`
on a worker thread, rebuilds the version there, and its `RD::free_rid` calls fail the render-thread
guard (50 "free_rid can only be called from the render thread" lines in a shard-4 run, all printed
inside the rewriting `Regenerate`, when the suite drew a red control just before it). The minimal set
went from 16 engine-error lines to 0 (7/7 pass) and the 62-suite shard-3 list from 20 to 0 (62/62);
the 40-suite shard-4 prefix ran clean twice. Complete battery: units 6517 passed, 3 skipped; engine
545 passed, 2 skipped, engine errors clean on all six shards (the suite in s4, 17.6 s); goldens
25/25 hash-identical. The suite takes 0.3 s alone and about 15 to 18 s in a
shard, where its TAA frame builds the advanced group for every shader the shard has made (PERF-45);
its weight is the shard figure.

**Verified.** The complete battery on the plan tree is green: units 6517 passed, 3 skipped; engine
545 passed, 2 skipped, engine errors clean on all six shards, `net-pause-overlay`'s shard included
and `graphics-retext-compiles` passing in s4 (17.5 s); goldens 25/25 hash-identical. The
in-place rewrite racing a pipeline compile still in flight is not fixed in production code; any
`Shader.Code` edit can meet it, the ocean lab's text edits included, and its cost is
`free_rid` error lines and leaked variants. It is tracked in #164.

**Original approach (kept for reference).**

**Goal.** No world shader keeps its Original compiled code after a live switch to Enhanced, so no
material moved onto it later draws stale code or fails its uniform buffer.

**Evidence (confidence: proven by probe and experiment; predates this branch, on main too).** The
complete battery went red on 20 engine-error lines in `net-pause-overlay` after its live graphics
switch: "Uniform buffer supplied (binding: 0) size (128) is smaller than size of shader uniform:
(144)" then "Parameter "us" is null". Minimal set: `gasbag-ordnance-gate, gemini-gasbag-bays,
graphics-ocean-switch, landings-docking-hold, landings-train-pickup-gate, load-progress,
net-pause-overlay` (the extra suites only decide whether gemini's materials are freed by the first
switch). It reproduces at a4ebec53 and on main (66c22e53b, `graphics-live-switch` in place of the
ocean suite). Cause: `ShaderTwins.RetextInPlace` (`Mech3/ShaderTwins.cs`, `from.Code = text`, called
from `Regenerate`) rewrites an Original shader to its Enhanced text on a first switch before an
Enhanced frame has drawn. Godot 4.7 sizes the uniform block at once
(`scene_shader_forward_clustered.cpp` 231-233) but only marks the code dirty; the recompile waits for
a material update (`version_get_shader`), and the first Enhanced frame's advanced-variant enable
(`ShaderRD::enable_group`, `_compile_version_start`, `shader_rd.cpp` 720) clears `dirty` without
rebuilding the base variants. A rewritten shader nobody wears that frame keeps its Original code for
good. Probe: 60 shaders rewritten at the first switch, one (`world:8047`) worn by nobody; the later
switch moved 8 materials onto it and 8 error pairs followed. With the in-place rewrite gated off the
minimal set ran clean. Where the two uniform blocks match in size, the stale shader draws Original
code in Enhanced with no error at all. No existing issue tracked it.

**Approach.** Pin every rewritten shader on a throwaway `ShaderMaterial` held until
`Engine.GetFramesDrawn()` has advanced by at least 2, so Godot's material update recompiles it before
the advanced-variant step; release the pins in `Regenerate` or `ReleaseUnused`. (A fresh twin for
unworn shaders instead of the in-place rewrite still depends on wearers surviving a frame, so it is
the weaker option.)

**Model recommendation.** high: an engine-level ordering bug with a silent failure mode.

**Verify.** A regression engine suite: build a key in Original, release its materials, switch to
Enhanced with `EnhancedDrawn` false, draw one frame, put a fresh material on the key, and find no
engine error and the material's shader compiled from the Enhanced text. The minimal set and the full
shard-3 list clean; the complete battery green, goldens 25/25.

**⚠ Traps.** The minimal set is GC-timing dependent; prove the fix on the exact failing list, not on a
smaller one that happens to pass. Original graphics output must not change.

## E43 ☐ No raised water triangle shows or shadows over the ocean in a harbour

**Goal.** In an Enhanced waves session, no original water surface shows through or above the ocean
as a flat patch, and no water surface casts a shadow onto the ocean.

**Evidence (confidence: the user's screenshot and the freecam log; the cause is a reading, not
measured).** A C2 (Hollywood) freecam at the harbour, defaults sea: a large V-shaped patch in the
harbour draws the flat original water look with a sun streak, and a dark wedge lies on the ocean
beside it, on the side away from the sun. The user's words: "there seems to be tiles that have some
part of water texture that is not detected. and the triangular field (consisting of two nodes)
throws a shadow somehow". The clicked nodes: `g36352` (centre (-6400, 10.1, -3968), size 512.6 x 20.3
x 256) and `g36353` (centre (-6272, 12.0, -3714), size 256 x 24 x 260), with `g36351` beside them;
clicks on the patch hit at y 0.5, clicks on the ocean beside it at y 0.0. The likely cause is a water
triangle sloping from the sea up toward the quay. `OceanMask.Read` takes a water triangle as base
only within 0.5 m of y = 0, and as edge only when its foot is; the hide drops the old sheet only
below 0.25 m; so the rising part draws its original material above the swell. A raised water
surface then casts onto the ocean (the "sun shadow: 155 terrain/water mesh instance(s) cast none"
rule may miss merged or mixed instances).

**Approach.** Measure first: the triangles, textures, heights and shadow settings of `g36351` to
`g36353` as built in an Enhanced session, and whether the shadow comes from them. Then fix at the
cause, for example treating a water ramp rising off the sea as sea out to where it meets solid
ground, and keeping every water surface from casting onto the ocean. Find every such harbour in the
sea chapters (C1, C1B, C1C, C2, C2B, C3, C5) with a scan, not by eye.

**Model recommendation.** high: geometry classification against authored data, and a visible
result the user judges.

**Verify.** A before/after montage at the user's pose (camera near (-6200, 280, -3600) looking
at the harbour) and at any other harbour the scan finds, for the user to judge. An engine
assertion that no water surface over the sea casts a shadow, and that the classified ramp counts as
sea. The complete battery green; golden re-pins only for an intended change, each named.

**⚠ Traps.** Colliders at y = 0 never change. Original graphics output must not change. A quay
wall or land triangle must not become sea.
