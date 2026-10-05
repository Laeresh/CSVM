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

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The ocean reads darker than the flat sea because its colour pipeline differs (vertex colour or texture linearisation). | With waves, foam and detail mix neutralised and roughness at the sheet's 0.25, the ocean matched the flat sea to 0.65/255 mean luminance over the sea region. The darkening came from low roughness (0.07) and a 35 % texture mix. |
| 2 | The low-pass frame cost is the grid's vertex count or the wave arithmetic. | With `--no-ssr` the low pass costs 7.8 ms against the flat sea's 6.5 ms; with SSR on it is 18.4/20.5 ms against 13.4 ms. Screen-space reflection over the wavy surface is most of it. |

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
2. ☐ Water Quality setting (flat or waves), live switch included
3. ☐ Verify the live graphics switch and the motion look

### Wave B, cost

11. ☐ Bring the low-altitude SSR cost within budget
12. ☐ Measure and budget the Deck
13. ☐ Cache or speed up the mask bake

### Wave C, coverage

21. ☐ The other chapters with a sea at y = 0
22. ☐ Ship calm zones for every hull and wake
23. ☐ Swell regularity from altitude
24. ☐ An Enhanced ocean golden

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

## A2 ☐ Water Quality setting (flat or waves), live switch included

**Goal.** An option chooses flat or waves in Enhanced, saved like the other display options, with a
`--` flag and a config key, and a change applies live.

**Evidence (confidence: lead-only).** The handoff named the setting; nothing about its shape was
settled in this session. The other Enhanced options (`--view-distance`, `--shadow-quality`) are the
pattern to copy.

**Approach.** Copy the --view-distance/--shadow-quality option pattern: a `--water-quality=flat|waves` flag, a saved option, a config key, a menu row beside the other Enhanced graphics options, and a live
apply through GameSession.FollowOcean. Default per Decision 10. <TODO: how the default tells the
Deck/Linux from the desktop; reuse whatever platform test the options already make, if any.>

**Model recommendation.** medium: follows an established option pattern.

**Verify.** <TODO: the suite or probe that proves flag, saved option and live change.>

**⚠ Traps.** Under `--det` only the flag survives, so a golden cannot read the saved option.

## A3 ☐ Verify the live graphics switch and the motion look

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

# Wave B, cost

## B11 ☐ Bring the low-altitude SSR cost within budget

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

## B12 ☐ Measure and budget the Deck

**Goal.** The ocean's cost on the Steam Deck is known for single-player and split-screen, and the
Water Quality default there follows from it.

**Evidence (confidence: lead-only).** The Deck was unreachable this session (`ssh deck@steamdeck`
timed out). The handoff notes Enhanced split-screen already runs below 60 fps there.

**Approach.** Deploy to `~/CSVM-tmp` per the Deck notes and run the B11 poses under `--det`.
<TODO: split-screen poses.>

**Model recommendation.** medium.

**Verify.** <TODO: the budget line and the default it implies.>

**⚠ Traps.** SSH screenshots on the Deck need `--det`; a `--no-det` shot captures the loading frame.

## B13 ☐ Cache or speed up the mask bake

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

## C21 ☐ The other chapters with a sea at y = 0

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

## C22 ☐ Ship calm zones for every hull and wake

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

## C23 ☐ Swell regularity from altitude

**Goal.** The swell shows no visible repeat from cruise altitude.

**Evidence (confidence: lead-only).** The foam lattice came from eight fixed swell waves, and the
user noticed it; the shading of those same waves may repeat too. Not yet judged on its own.

**Approach.** <TODO: more waves with jittered directions and lengths, or a noise-modulated amplitude
field.>

**Model recommendation.** medium.

**Verify.** The cruise montage pose, judged by the user.

**⚠ Traps.** The vertex-stage fade (full at eight grid steps per wavelength, none at four) is what
keeps waves the grid cannot carry from crawling. Keep it for any new wave.

## C24 ☐ An Enhanced ocean golden

**Goal.** A pinned `--det` shot shows the ocean, so a later change to it is caught.

**Evidence (confidence: traced).** `analysis/goldens/manifest.json` has `c1b-night-sea`
(Original, `--freecam`) and no Enhanced shot of any chapter's sea; the prototype moved no golden.

**Approach.** Add an Enhanced C1B shot through `--fly` placement, pinned on a stable frame.

**Model recommendation.** medium, low effort.

**Verify.** `.\RunTests.ps1` twice with the new hash stable, once under external GPU load.

**⚠ Traps.** A hash that flips under GPU load is a determinism defect, not noise. Pin the stable
frame and track the flip separately. The `exercises` field is rewritten on a re-pin, never appended.
