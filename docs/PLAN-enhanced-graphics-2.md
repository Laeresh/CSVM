# Enhanced Graphics 2, the temporal pass, lit explosions and lit clouds

**ACTIVE PLAN** (written 2026-09-16). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The opt-in Enhanced mode (`GraphicsMode.Enhanced`, `--graphics=enhanced`) today lights the world
from the authored sun and ambient, mirrors the committed world lights onto real omni lights, casts
4-split soft shadow maps, runs SSAO, blurs a screen-space reflection on water, blooms only the
glow-arm sprites and tonemaps with AgX (`CSVM/src/Session/Launcher.cs` `SetupLighting`). Clouds are
still the original's flat, unshaded sprite cards and explosions are still unlit puffer billboards.
This plan adds four things to that stack, all behind the same switch: a temporal anti-aliasing pass
plus a render-scale display setting (Wave A), lit explosions (Wave B), lit cloud cards and volumetric
cloud banks (Wave C), and an enhanced golden set with a perf reading over the finished stack
(Wave D). `BL-803`, the whole-screen dither reported under Enhanced, is folded into Wave A because
Godot resolves SSAO and soft-shadow penumbrae with screen-space noise that only a temporal pass
integrates away, so TAA is the first attempt at it and the bisect is the fallback. Session
`orch-7` was asked on 2026-09-16 not to pick `BL-803` up.

Out of scope: any change to the faithful presentation, which stays bit-identical and keeps every
existing golden (the switch is the one gate on every item here); FSR 1.0/2.2 as an upscaler for
weak GPUs (this plan spends GPU headroom, it does not save it); the parked 4x texture upscaling
(branch `upscaling`); `BL-322`'s C5 facade brightness and `BL-905`'s aircraft gloss, both cited as
context and left to their own items; menu presentation work beyond the one new VIDEO row.
Backlog items drawn in: `BL-803` was read in `backlog.md` this session and not re-verified against
the record or the code; `BL-325` (moonlit night clouds) is cited by C21 and not closed by it.

## Milestone goal

- Under Enhanced the image is temporally anti-aliased, the SSAO and penumbra noise is gone, and a
  Render Scale row on the VIDEO page lets a bored GPU render above native.
- A rocket or bomb burst lights the ground and the aircraft around it, its fireball blooms, its
  smoke reads lit by the sun, the air over it shimmers and the hit leaves a scorch.
- The cloud field reads lit: sun-side rims, shadowed undersides, no hard cut where a card meets
  terrain, and a soft volumetric bank under the cards for scatter and sun shafts.
- Speed and direction changes are felt: streaks stream past the camera with speed and G, and the
  chase camera trails the nose through a roll and widens with speed.
- A small `--graphics=enhanced` golden set pins the stack, and a perf reading shows the whole of it
  within 20% of the D31 baseline.

**Every item rides `GraphicsMode.Enhanced` and moves no faithful pixel.** The faithful path is the
project's deliverable; Enhanced is the remake-only arm that may invent. Render Scale is the one
exception, a display setting like V-Sync, and it is ignored under `--det` so no golden sees it.

## Decisions (2026-09-16)

| # | Question | Decision |
|---|---|---|
| 1 | Which features does the plan cover? | **TAA + BL-803, Render Scale, alpha-to-coverage, lit cloud cards + FogVolume banks, and all five explosion pieces** (burst light, bloom, sun-shaded smoke, heat shimmer, scorch decals). |
| 2 | Which AA and resolution stack? | **Godot TAA under Enhanced plus a bilinear Render Scale 1.0 to 2.0**; FSR 2.2 evaluated once (A5) as the alternative temporal pass and kept only if it reads better. Godot's FSR modes only upscale and FSR 2.2 replaces TAA, so they cannot supersample. |
| 3 | What gates each piece? | **TAA and alpha-to-coverage under the Enhanced switch; Render Scale a display setting on the VIDEO page in both modes**, default 1.0, ignored under `--det`. |
| 4 | How is BL-803 sequenced against TAA? | **Land TAA, the user judges, bisect only if the pattern persists** (A3). TAA is wanted regardless. |
| 5 | How far does the cloud work go? | **Cards lit first (C21), FogVolume banks second (C22), the cards stay.** Judged separately on C1 by day and C5 at night. |
| 6 | Which cloud sprites? | **The authored fvol masks first; a rendered puff set is its own follow-on item (C23)** picked by montage. |
| 7 | Which explosion pieces? | **All five**, each its own item with its own judgement. |
| 8 | How is a look item verified and closed? | **The user's eyes at the controls close each item; a separate enhanced golden set (D31) is the regression net.** No luminance metric gates a close. |
| 9 | Perf budget? | **The finished stack holds the D31 Enhanced frame time within 20%** on C4/C5/C3 at 4 panes, render scale 1.0. Render scale above 1.0 is the user's own spend. |
| 10 | Wave order and pacing? | **A, B, C, E, D. A wave halts on the user's flight, and the next wave starts as soon as it has no dependency on the halted one.** B, C and E need A1 landed (they are judged through the same temporal filter) but not A's verdict; D needs everything. |
| 11 | The feel of speed and of direction changes (Wave E)? | **Wind streaks past the camera and a chase camera that lags the nose and widens its FOV with speed, both under Enhanced.** Wingtip vapour is out (too much for a prop plane); radial motion blur is out (it competes with the temporal pass for sharpness). |
| 12 | What happens to the authored speed-cue wisps under Enhanced? | **They stay; the streaks are added over them.** The wisps are data (`speed_cue.zrd`) and `BL-867` tunes them on the faithful path; whether Enhanced dims them under the streaks is a TUNE the user judges in E41's montage, not a decision made here. |
| 13 | Does `BL-508` (blend, not scissor, for trees, rails and lattice) change the plan? | **Yes: A4 waits for it and covers only what stays scissored afterwards**, possibly nothing. Blended surfaces have no coverage edge to fix. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "FSR super-resolution can render above native for a GPU with headroom." | Godot's `scaling_3d_scale` above 1.0 is bilinear only; FSR 1.0 and 2.2 accept scales at or below 1.0, and FSR 2.2 disables Godot's TAA in favour of its own temporal pass. Supersampling here is bilinear, and FSR 2.2 is an alternative AA, not an upscaler for this plan. |
| 2 | "The BL-803 dither is a deliberate dither somewhere in the enhanced stack." | `backlog.md`'s own entry: nothing in `SetupLighting` asks for one, debanding is off, and the clutter fade dithers only its own fragments. |
| 3 | "The BL-803 pattern is temporal screen-space noise (SSAO, soft-shadow sampling) that TAA will integrate away." | The user's two C3 freecam stills (`.scratch/eg2/A3/`, freecam at x -6675 y 70 z -3037, 5120x1440, sun pitch -25 yaw 135): the pattern is a set of fine, evenly spaced, parallel bands with one world direction, still while the camera is still, present on the flat lit water where no occluder exists, and its spacing changes across straight seams on the hillside, which are the shadow cascade borders. That is the sun's shadow map self-shadowing a grazing surface (acne, smeared into bands by the 2.0 degree penumbra and the blur), a stable pattern TAA cannot remove. A3 is the bias/penumbra fix, not a wait on A1. |
| 4 | "The fireball's atlas frames are additive, so the additive emitter shader variant is where a bloom gain goes." | B12's census: 145 zrdr files name 26 puffer sprite textures and none carries the additive bit in any chapter's manifest; the install's additive textures are mesh materials, flares, rings and HUD hilites. The additive emitter layer is never built for a shipped effect. The bloom is gated by the `fire_f01`..`fire_f06` flipbook name instead, and the `puffer-fire-glow` suite pins the premise. |
| 5 | "The BL-803 bands are shadow-map acne from the bias pair, smeared by the 2.0 degree angular distance and the blur." | A3's headless sweep at the user's pose: a 32-bit map is bit-identical, every bias move deepens the pattern, halving the blur sharpens it, and the angular distance at 0.5 clears C3 but not C1's water while lightening real cast shadows. The bands are Godot's directional soft-shadow filter at its default Soft Low quality; Soft High halves them and leaves the cast-shadow control untouched. |
| 6 | "A heat-shimmer quad must draw after the fireball so it refracts the flame." | B14's measurement: Godot takes the `hint_screen_texture` copy once, before the whole transparent pass, so the copy holds the opaque world and no fireball or smoke; a quad over the flame painted bare terrain across it, and a quad drawn after the fireball erased the fire and smoke it covered. The quad stands 30 m over the burst and draws before the other transparents (render priority -1), so the fireball composites over it and stays pixel-identical. |
| 7 | "The cloud cards' transmission rim transfers to the smoke sprites." | B13's measurement: the puffer atlas packs frames side by side with no mip chain, and the smoke masks are smooth blobs, so three taps toward the sun move at most 4 of 255 levels for three extra samples per fragment. Dropped; the sun grade alone ships. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2 (where the viewports are built), B12 (the glow threshold contract) | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | A1, A4, B11, C21, D31, D32 | The *what* is settled; every energy, radius, tint and offset is TUNE, judged by the user, never invented as fact. |
| **Leads only, no mechanism yet** | A3, A5, B13, B14, B15, C22, C23, E41, E42 | Budget for investigation; A3 and A5 may end in a disproof or a park. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

- **The enhanced Environment and sun.** `Launcher.cs` `SetupLighting` (lines 1497 to 1543 at
  writing): `EnableSunShadows` (4 splits, angular distance 2.0, blur 1.0), SSAO (radius 2.5,
  intensity 2.0, power 1.5, horizon 0.06, sharpness 0.98), `EnableWaterReflections` (SSR 64 steps),
  `EnableGlowAndTonemap` (HDR threshold 1.0, bloom 0.0, intensity 0.9, strength 1.1, screen blend,
  HDR scale 2.0, luminance cap 8.0, AgX white 6.0). Every constant is `TUNE` with its own comment.
- **Anti-aliasing today.** `CSVM/project.godot` sets `anti_aliasing/quality/msaa_3d=2` (4x MSAA)
  for both modes. `CockpitOverlay.cs:133`, `SpyglassView.cs:45` and `SplitScreen.cs:259` copy that
  project setting onto their own `SubViewport`s, so any viewport-level pass has four places to
  reach. No code reads `use_taa`, `screen_space_aa` or `scaling_3d_*`.
- **The capture trap.** `Testing/CaptureDirector.cs:109`: a `--shots` image is the previous
  frame's render; `_00` is un-jittered and `_01+` carries the burst camera's dither. A dither
  verdict comes from the controls or an undithered capture, never from a burst frame.
- **World lights under Enhanced.** `Mech3/WorldLights.cs`: `Begin`/`Add`/`Commit` per frame, and
  in Enhanced mode `Commit` mirrors the committed set onto a growing pool of `OmniLight3D`
  (`ShadowEnabled = false`, `OmniAttenuationTune`), energy from the colour's peak channel. This is
  the pool a burst light joins.
- **The explosion sprites.** `Effects/EmitterRenderer.cs` `MultiMeshEmitterRenderer`: one shader
  per (blend, soft) pair, `unshaded`, blend per atlas column off the texture's own additive bit,
  `ALBEDO = t.rgb * srgb_to_linear(COLORS ramp)`, quad-rim fade, soft-particle depth fade,
  mission fog. The additive columns are what B12 lifts over the glow threshold; the mix columns
  are the smoke B13 shades.
- **The cloud field.** `Effects/FogVolumeClutter.cs` `ShaderCode(lit, fogged)`: one static
  MultiMesh per sprite kind, hand-billboarded, `unshaded`, `depth_draw_never`, no depth fade; the
  sprite's own polygon normal is in `INSTANCE_CUSTOM.xyz` (encoded `[0,1]`) and its fade draw in
  `.w`; colour is the authored per-vertex 240 and the texture a constant-RGB alpha mask, and the
  comment at line 225 forbids scaling it on the faithful path (`docs/org/cloudCards.md`).
- **The reference technique** (Zess-57, r/godot "Particle/Multimesh billboard clouds showcase",
  saved at `.scratch/RedditClouds/`): one MultiMesh of unshaded billboards, five rendered puff
  textures picked by `INSTANCE_ID`, sun tint `vec3(1.6 + sn * 1.2)` with `sn = dot(viewDir, sunDir)`,
  and a transmission rim: the alpha of a blurred sample (`textureLod(..., 3.0)`) at
  `UV + (sunDir · billboard right, -sunDir · billboard up) * scatter_distance` tints the albedo by
  `(1 + f2)`, `f2 = -a2 * 0.6 + (sunDir · billboard forward) * 0.4 + 0.4`. The author measured
  a 0.923 frame-time factor at a sky-filling view.
- **The perf baseline (D31, `git show 8aab7fa3`).** `RunTests.ps1 -Graphics enhanced` appends
  `--graphics=enhanced` to the perf and hitch launches only. Targeted C4, C5 and C3 probes at 4
  panes, frame_ms original vs enhanced: C4 10.40 vs 15.04, C5 11.00 vs 13.60, C3 9.15 vs 12.51.
  That table was provisional against Wave E of the first plan and is re-taken by D32 before the
  20% is applied.
- **The golden set.** `analysis/goldens/manifest.json` runs every shot under `--det --mute`, and
  under `--det` only the `--graphics=` flag reaches `GraphicsMode.Resolve`, so an enhanced shot is
  one more manifest entry with the flag in its args.
- **The speed cue as shipped.** `Flight/SpeedCue.cs` loads each chapter's `speed_cue.zrd`
  verbatim: three `cuepufferN` states picked by camera altitude, emitted 60 m ahead of the player
  and left in world space for the aircraft to pass (`docs/formats/effects.md` "Aircraft speed-cue
  wisps"); off within 50 m of the ground. `BL-867` says they read too opaque and crowd a 16:9
  frame, and is a faithful-path tuning item. The original authors no other speed cue: the
  `high_speed` shake runs only above rated max speed and the `rattle` sound is speed-keyed
  volume (`docs/org/shakes.md`).
- **The chase camera's decoded law.** `docs/org/cameraViews.md` "The external camera's distance":
  distance grows with speed and carries an acceleration transient (`dist_vary`, `dist_catch_up`),
  ported as `CameraController.ExternalRadius`/`UpdateDynamics`/`DistTransient`. Nothing in the
  decoded law lags the camera's orientation behind the nose or moves the FOV with speed; the
  external FOV is one constant (`CameraController.cs:142`).
- **A camera-centred streak field exists.** `Effects/Precipitation.cs`: one MultiMesh whose shader
  derives each quad's position from a per-instance seed, `csky_time` and the camera position,
  wrapped into a camera-centred box, with a near fade and a rim fade; rain already draws stretched
  streaks (`MakeStreakTexture`, `streak_size`). Its own header notes a plane-relative streak look
  as an unauthored TUNE follow-up.
- **The night-cloud measurement (`BL-325`, CAP-11 C1B).** Cloud cores near the moon p90 218, the
  away side p90 70, our uniform WorldLight rendered p90 102. The lit cards of C21 are the arm that
  can answer it, under Enhanced only.

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

### Wave A, the temporal pass and the render scale

1. ☑ TAA on every 3D viewport under Enhanced
2. ☑ Render Scale, a VIDEO page row applied to every 3D viewport, ignored under `--det`
3. ◐ BL-803: the shadow-map bands on grazing surfaces under Enhanced
4. ☐ Alpha-to-coverage on the cutout surfaces under Enhanced
5. ◐ FSR 2.2 tried once as the alternative temporal pass, kept or parked on the user's verdict

### Wave B, lit explosions

11. ☑ A burst omni light at each fireball, from the enhanced pool, flickering down over the burst
12. ☑ The additive fireball frames bloom
13. ☑ Sun-shaded smoke billboards
14. ☑ Heat shimmer over a fireball
15. ☐ Scorch decals at a hit

### Wave C, lit clouds

21. ☑ Lit cloud cards: sun tint, transmission rim, shadowed undersides, soft depth fade
22. ☑ FogVolume banks from the authored fvol slabs
23. ☐ A rendered puff sprite set under Enhanced, picked by montage

### Wave E, the feel of speed

41. ☑ Wind streaks past the camera, keyed to speed and G, over the authored speed cue
42. ☑ The chase camera lags the nose through a roll and widens its FOV with speed

### Wave D, the net and the reading

31. ☐ An enhanced golden set under `--det`
32. ☐ The perf reading over the finished stack, within 20% of the D31 baseline

## Dependency and parallelism notes

A1 lands before any look item is judged, since B and C are seen through the same temporal filter;
B and C do not wait for A's verdict (A3, A5). A1 and A2 both touch the four viewport construction
sites (`Launcher`, `CockpitOverlay`, `SpyglassView`, `SplitScreen`), so they run in sequence, not in
parallel worktrees; A4 touches shader code in `SceneBuilder`/`Clutter` and can run beside A2. A3
depends on A1's flight. A5 depends on A1 and A2 and is judged against them. A4 waits for
`BL-508` to land on main (session `orch-7`), since that item removes most of A4's population; do
not start A4 in a worktree forked before it.

B12 and B13 both edit `EmitterRenderer.cs`'s one shader: sequential. B11 touches `WorldLights.cs`
and the effect sink, B14 adds its own material, B15 touches the crater/decal path; those three can
run in parallel worktrees with that file ownership. C21 owns `FogVolumeClutter.cs`; C22 is a new
module and can run beside C21; C23 depends on C21's shader. E41 is a new `Effects` module and
E42 owns `CameraController.cs`; they run in parallel with each other and with Wave C, after A1.
E41 must not touch `SpeedCue.cs` or `Puffer.cs`, which `BL-867` owns. D31 and D32 depend on everything,
and D32's baseline re-take (the D31 table was provisional) is taken at Wave A's start so the 20%
has a current denominator.

Wave pacing (decision 10): a wave halts on the user's flight, the next wave starts when nothing in
it depends on the halted one.

---

# Wave A, the temporal pass and the render scale

## A1 ☑ TAA on every 3D viewport under Enhanced

**Landed.** Godot's temporal anti-aliasing is on under Enhanced on all four 3D viewports and off on
the faithful path. The four sites write it through one helper, `Utils/ViewportQuality.cs`:
`public static void Apply(Viewport viewport)`, which sets `UseTaa` from
`GraphicsMode.Enhanced` and carries A2's render-scale write beside it. Called from
`Session/Launcher.cs` on the root viewport right after `GraphicsMode.Resolve` and its log line,
from `Flight/CockpitOverlay.cs`, `Flight/SpyglassView.cs` and `UI/SplitScreen.cs` at SubViewport
construction. MSAA is untouched and `project.godot` is unchanged, so nothing reaches the faithful
path. `Testing/GroundShadowSuites.cs`'s graphics-mode gate gained the assertion that a built
viewport takes the pass under Enhanced and Godot's default under original; `docs/architecture.md`
and `docs/architecture/Utils.md` carry the helper's entry.

**Verified.** The item's tree is the first code on the plan branch, so its battery run is the
merged tree's run; the branch battery runs again at the wave's end. On the item worktree: `dotnet build` warning-free, the
complete `RunTests.ps1` PASS (units 4568 passed 0 failed, engine 348 suites passed 0 failed,
goldens **19 shots hash-identical, zero movers**), `CheckCommentCaps.ps1` / `CheckDocEntries.ps1` /
`CheckEncoding.ps1` clean. An `--graphics=enhanced` empty-stage probe renders and quits with the
flag on. Not verified here: the image itself, which is the user's flight (the goal's edge, water
specular and SSAO/penumbra stability, and whether the billboard populations ghost), and A3's
question of whether the `BL-803` pattern is gone. Also seen: an enhanced `--fly --chapter=C1`
screenshot probe does not quit inside 150 s while the faithful one takes 6.6 s, and it hangs the
same way with the TAA write forced off, so it is the enhanced chapter stack rather than this item.

**Original approach (kept for reference).**

**Goal.** Under Enhanced the world, cockpit, spyglass and split-screen panes are temporally
anti-aliased; edges, specular on water and the SSAO/penumbra noise are stable frame to frame. The
faithful path is unchanged.

**Evidence (confidence: direction-sound).** `project.godot` sets 4x MSAA for both modes, and
`CockpitOverlay.cs:133`, `SpyglassView.cs:45` and `SplitScreen.cs:259` copy that setting onto their
SubViewports; nothing reads `use_taa`. Godot's TAA is a `Viewport` property, so the same four sites
carry it. It does not remove the BL-803 bands, which are a stable shadow-map pattern (A3).

**Approach.** Read `GraphicsMode.Enhanced` once where each viewport is built (the root viewport in
`Launcher` after `Resolve`, the three SubViewports at construction) and set `UseTaa = true` there;
MSAA stays as it is (Godot allows both). Do not set it in `project.godot`, which would reach the
faithful path. Watch the billboard populations (puffer, clouds, clutter's dithered fade) and the
HUD sprites for ghosting; the clutter fade's own dither is the likely victim, and if it smears,
that is a finding for A3's record, not a reason to drop TAA.

**Model recommendation.** medium: four mechanical sites plus a flight to judge.

**Verify.** `--graphics=enhanced --chapter=C1` at the controls, chase view, a low pass over the
lake and a bank over the city; then the faithful `--freecam` goldens unchanged (the sweep must
report zero movers, which proves the gate). A `--shots` capture cannot judge this (`CaptureDirector.cs:109`).

**⚠ Traps.** A burst screenshot carries the previous frame and the jitter. The cockpit pass
duplicates the Environment but not the viewport flags, so its SubViewport needs its own `UseTaa`.
`--det` runs keep the flag (it is under the mode, and D31 pins enhanced goldens through it), so an
enhanced golden must be pinned with TAA settled, not before.

## A2 ☑ Render Scale, a VIDEO page row applied to every 3D viewport, ignored under `--det`

**Landed.** `Utils/RenderScaleSetting.cs` resolves the scale once at launch beside
`GraphicsMode.Resolve`: the saved `renderScale` option, then the `graphics.renderScale` config key,
then native, with `SavedWord(bool det)` holding the `--det` drop the other display settings hold.
The words are `DisplayWords.RenderScaleChoices` (100/125/150/175/200), the field is
`OptionsDef.RenderScale` with its own validation set, and the value rides `OptionsApplyExit` like
every other setting so `Launcher.PersistOptions` stays the file's one writer. The viewport write is
one branch in `Utils/ViewportQuality.Apply`, so all four 3D viewports (root, cockpit pass, spyglass
picture, split-screen panes) take it: `Scaling3DMode = Bilinear` and `Scaling3DScale` above native,
and nothing written at native, which is what leaves a faithful `--det` run on Godot's own defaults.
`msaa_3d` is untouched. The row is a display setting rather than the Enhanced switch's, so it is
written whichever presentation won. Original's VIDEO page carries it on the authored Objects Detail
line (`VP_T_ObjectsTitle` / `VP_D_Objects` / `VP_T_ObjectsDESC`), between V-Sync and Enhanced
Graphics; Built-in carries it as the tenth stepper row. The `[world] graphics mode:` line now ends
`render_scale=<word>% source=<layer>`.

**Verified.** The complete `RunTests.ps1` on the item worktree: build PASS, units **4568 passed, 0
failed, 2 skipped of 4570**, engine **349 suites passed, 0 failed** (the new `display-render-scale`
among them), goldens **19 shots hash-identical**, exit 0. `CheckCommentCaps.ps1`,
`CheckDocEntries.ps1` and `CheckEncoding.ps1` clean. The new suite resolves the precedence, proves
`SavedWord` drops a saved 200 under `--det`, drives the VIDEO row's round trip, and reads the
viewport back: at native an `Apply`-ed SubViewport matches an untouched control, at a saved 200 it
reads `Bilinear` and `2.0`. Headless empty-stage probes (`RunProbe.ps1 --stage=empty --no-det
--perf`) measure the render resolution through `[perf] gpu_ms`: **0.11 ms at 100% on two separate
runs, 0.29 ms at 200%**, roughly the 4x pixel count, with the log line reading
`render_scale=200% source=graphics.renderScale`. The same config key under an implied-`--det`
`--screenshot` run renders at native (`render_scale=100% source=default`, `gpu_ms` back to 0.11),
which is the drop proven end to end rather than only in the suite. **Owed to the user at the
controls:** whether 200% on C5 visibly sharpens the skyline, the item's own look judgement.

**Original approach (kept for reference).**

**Goal.** A "Render Scale" row on the VIDEO page of both presentations (100% to 200%) renders the
3D viewports above native and downsamples bilinearly; the setting is saved with the other display
settings and takes effect on the next start like Enhanced Graphics.

**Evidence (confidence: traced).** The VIDEO page rows are the `OriginalOptionsScreen.cs` table at
lines 361 to 386 (Monitor, Resolution, Display Mode, V-Sync, Enhanced Graphics), each a
`DisplaySettingRows` helper over an `OptionsStore` field; `BL-783`'s close put the same four
settings on the built-in presentation (`LaunchMenu.cs`). Godot's `scaling_3d_scale` accepts up to
2.0 in bilinear mode only, per the disproven-claims table. The four viewport sites are A1's.

**Approach.** A `RenderScaleSetting` in `Utils` modeled on `VSyncSetting` (word list 100/125/150/175/200,
default 100, `Resolve` off the saved option then a `graphics.renderScale` config key), an
`OptionsStore` field, one row in each presentation's table, and `Scaling3DScale` set on the four
viewports with `Scaling3DMode = Bilinear`. Under `--det` the saved option is not passed, exactly as
`GraphicsMode.Resolve` documents for its own saved option. Announce it on the `[world] graphics
mode:` log line. `<TODO: where V-Sync is applied to the window at launch, to place the viewport
write beside it>`

**Model recommendation.** medium.

**Verify.** `MenuOriginalSuites` and `DisplaySettingsSuites` gain the row (they count the VIDEO
titles and round-trip the store); at the controls, 200% on C5 visibly sharpens the skyline and the
`[perf]` frame time rises; `--det` goldens unchanged with a saved 200%.

**⚠ Traps.** Do not touch `msaa_3d`. The spyglass and cockpit SubViewports have their own sizes;
scale them the same way or the panes disagree in sharpness. The row is a display setting and is
NOT under the Enhanced switch (decision 3).

## A3 ◐ BL-803: the shadow-map bands on grazing surfaces under Enhanced

**Landed.** The pattern is the sun's soft-shadow filter, not the bias pair, and not a cascade
artefact: with `sun.ShadowEnabled = false` it vanishes from lit ground and water, and it never
appears on sky. Godot filters a PCSS directional shadow at the project-wide
`directional_shadow/soft_shadow_filter_quality`, which ships at Godot's default (Soft Low, measured
bit-identical to the shipped build), and at a 25 degree sun that filter resolves the shadow map's
own self-occlusion into a fine screen-space lattice over every grazing lit surface rather than
dissolving it. `Session/Launcher.cs`'s `EnableSunShadows` now ends with
`RenderingServer.DirectionalSoftShadowFilterSetQuality(EnhancedSoftShadowFilter)`, a new TUNE
constant set to `RenderingServer.ShadowQuality.SoftHigh`. The call is renderer-global state, which
is why it sits inside the enhanced-only `EnableSunShadows`; nothing on the faithful path reaches it.
No other constant moved: `EnhancedShadowAngularDistance` stays at 2.0 so E48's soft edge survives,
and the bias and blur comments now state what that pair does and does not control.

**Verified.** Measured headless at the user's own C3 pose and the same pose on C1, plus a C1 town
pose as the cast-shadow control, as the band-limited RMS of the 1.6 to 4 px screen-space component
(8-bit luminance, 2560x1421, TAA off to expose the source). C3 water: shadows off 0.19, shipped
0.76, Soft High 0.42, Soft Ultra 0.39. C3 hillside: 0.30 / 1.07 / 0.58 / 0.56. C1 water at the same
pose: 0.25 / 1.04 / 0.60 / 0.47. In the shipped configuration, with A1's TAA on, Soft High still
takes C3 water 0.24 to 0.19, the hillside 0.44 to 0.30 and C1 water 0.33 to 0.25. The cast-shadow
control does not move (C1 town 5th/20th percentile and mean luminance 55.29/68.20/78.57 shipped
against 55.31/68.20/78.55). Cost on the C1 town pose at the shipped 1280x720, RTX 5080:
`gpu_ms` 0.94-0.99 shipped, 1.69-1.75 at Soft High, 2.02-2.04 at Soft Ultra, with `frame_ms` pinned
at the 120 fps cap throughout; Soft Ultra was rejected because it buys nothing measurable over Soft
High once TAA is on. The complete `RunTests.ps1` PASS: units 4568 passed 0 failed 2 skipped of
4570, engine 348 passed 0 failed 0 skipped errors clean, goldens **19 shots hash-identical**;
`CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and `CheckEncoding.ps1` clean. Montages and the full
lever table are under `.scratch/eg2/A3/`. Not verified here: the user's own eyes at the controls,
which is what decides whether the residue that Soft High leaves still reads as banding, and whether
the softer edge is worth the GPU cost on their rig. `BL-803` stays open until then.

**Original approach (kept for reference).**

**Goal.** The world-aligned banding over lit terrain and water under Enhanced is gone at C1's
25 degree sun without dissolving a hangar's or an aircraft's cast shadow, and `BL-803`'s closing
record names the mechanism and the lever.

**Evidence (confidence: direction-sound).** The user's two freecam stills on C3 (x -6675, y 70,
z -3037, 5120x1440, copies under `.scratch/eg2/A3/`; C3's authored sun is pitch -25, yaw 135,
the same elevation as C1's, on which the bias pair was tuned; the user confirms the bands on
C1 too, much finer, which fits a texel footprint: the enhanced shadow distance follows each
zone's fog ramp, so a longer fog range hands a cascade more ground per texel and the bands
widen): fine, evenly spaced, parallel bands with
one world direction, still while the camera is still, on the flat lit water where nothing can
cast a shadow, and changing spacing across straight seams on the hillside that match the four
cascade borders. `Launcher.cs`'s bias comment already records that lower bias values "put
dithered acne over every terrain triangle at C1's 25 degree sun" and that the pair trades against
hangar shadows; the shadow blur comment records that raising it "dithered the lit water". So
this is shadow-map self-occlusion of grazing surfaces, smeared into bands by the 2.0 degree
angular distance (PCSS blocker search) and the blur. It is stable, not temporal, so A1 cannot
remove it. Re-verified open before the work: no commit closes `BL-803`, and no commit in the
repository has ever touched `DirectionalSoftShadowFilterSetQuality` or
`soft_shadow_filter_quality`, so the filter the Landed paragraph names was untried.

**Approach.** Reproduce headless first: a `--freecam --chapter=C3 --pos=-6675,70,-3037` still
under `--graphics=enhanced` at the user's pose (the `_00` frame is un-jittered) shows the bands;
take the same pose on C1 to confirm the elevation, not the chapter, is the condition. Then the levers, in
order, each judged on that still and on the C1 hangar/aircraft shadow control: (1) the angular
distance back from 2.0 toward 1.0 or 0.5 (E48 of the first plan picked 2.0 for softness, so this
is a trade the user judges); (2) the normal bias up and the constant bias down (flat water at a
grazing sun is the normal-bias case); (3) `directional_shadow/size` and a tighter first split, so
a texel covers less ground; (4) if the bands persist only on water, the water arm's own
`ShadowCasterSetting.Off` or receive-shadow off, since the water plane is flat and a shadow on it
reads through SSR anyway. Add `--no-shadow` style inspection doors only if the still cannot
discriminate. Do not touch SSAO or SSR for this item.

**Model recommendation.** high: a four-way TUNE trade judged against two controls.

**Verify.** The user's freecam pose still and moving, over water and ground, plus the hangar and
aircraft shadow control at the same sun; the faithful goldens zero movers (shadows are off on the
faithful path).

**⚠ Traps.** `--shots` bursts carry the previous frame; use the `_00` frame or judge at the
controls. Debanding, SSAO and SSR are not the cause. Do not widen anything to the faithful
path, which casts no shadow map. A fix that only raises the constant bias detaches the aircraft's
shadow from its wheels (peter-panning); check the parked-aircraft shadow before accepting one.

## A4 ☐ Alpha-to-coverage on the cutout surfaces under Enhanced

**Goal.** Alpha-scissor edges (clutter, fences, lattice geometry, decals with cutout masks) resolve
through the MSAA samples instead of a hard 1-bit edge, under Enhanced only.

**Evidence (confidence: direction-sound).** MSAA does nothing for an alpha-scissored edge; Godot's
`alpha_to_coverage` render mode hands the alpha to the coverage mask. `SceneBuilder.GetBiasShader`
and `Clutter.ShaderCode` build their shader keys with an Enhanced bit already (`SceneBuilder.cs:1528`,
`:1724`, `:1819`), so an Enhanced-only render mode is one more key bit. ⚠ `BL-508` (queued in
session `orch-7`) lands the decoded finding that the original never alpha-tests, and switches the
tree, rail and lattice families (56 textures) from scissor to blend in both `TextureArchive` and
`Clutter`; those families then leave this item's population. What stays scissored after `BL-508`
is whatever its census names outside the three families, and only that is A4's.
`<TODO: after BL-508 lands, list the textures still scissored; if none, A4 closes ❌ as moot>`

**Approach.** Add `alpha_to_coverage` (or `alpha_to_coverage_and_one`) to the cutout arms of those
shaders when `GraphicsMode.Enhanced`, through the existing key bits so the faithful shader text is
byte-identical. Judge on C1's fences and the lattice radio tower.

**Model recommendation.** medium.

**Verify.** Enhanced flight past the C1 radio tower and a fence line; faithful goldens zero movers;
the alpha-cutout ray suites still pass (they test the collider, not the pixel, so they must not
move).

**⚠ Traps.** Alpha-to-coverage with TAA can double-soften an edge; judge A4 with A1 on. The
clutter fade dithers alpha on purpose; coverage on a dithered alpha reads as a different pattern,
so try it with and without the fade arm.

## A5 ◐ FSR 2.2 tried once as the alternative temporal pass, kept or parked on the user's verdict

**Landed.** The trial door is a `graphics.temporal` config key carrying `taa` or `fsr2`, default
`taa`, resolved once at launch by `Utils/TemporalPassSetting.cs` beside `GraphicsMode.Resolve` and
announced on the `[world] graphics mode:` line as `temporal=<word>`. There is no saved option and no
menu row: one of the two passes is deleted rather than kept as a second way in. The viewport write
is one branch in `Utils/ViewportQuality.Apply`, so all four 3D viewports take it. Under Enhanced,
`taa` writes `UseTaa = true` as before; `fsr2` writes `UseTaa = false` with
`Scaling3DMode = Fsr2` and `Scaling3DScale` at native, FSR 2.2 carrying a temporal pass of its own.
FSR 2.2 applies at native alone, so A2's render scale above native keeps its bilinear supersample
and Godot's TAA, which the comment on the write says; Godot refuses a factor above 1.0 in an FSR
mode. `FsrSharpness` is left at Godot's own default so the trial judges FSR 2.2 as it ships. The
faithful path writes neither pass whichever word the key carries, and a `--det` run drops the config
file like every other key, so no golden sees any of it. `Testing/GroundShadowSuites.cs`'s
`TemporalPass` step reads a freshly built viewport back per case and pins all three outcomes.

**Verified.** The complete `RunTests.ps1` on the item worktree, exit 0: build PASS, units **4574
passed, 0 failed, 2 skipped of 4576**, engine **352 suites passed, 0 failed**, goldens **19 shots
hash-identical**. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1`, `CheckEncoding.ps1`,
`CheckItemIds.ps1` and `CheckGoldenProse.ps1` clean. The ground-shadow suite's own line reads
`temporal pass: taa=True/Bilinear, fsr2=False/Fsr2, original=False/Bilinear`, each from a viewport
built for that case, so the faithful path is measured against a fresh one rather than assumed. The
`--det` drop is proven end to end as well as in the suite: with `graphics.temporal=fsr2` in
`config.json`, an implied-`--det` `--screenshot` run logs `dropped_overrides=1` and
`temporal=taa`, and the same run under `--no-det` logs `temporal=fsr2`.
⚠ `ai-wave-launch-hitch` is a wall-clock regression bar and fails on this workstation whenever
another session is loading it. In such a run its shard-6 neighbours read 2.5 to 2.8 times slow too
while that shard's own total is shorter than the base commit's, which is what tells a contention
window from a regression.

**The headless evidence, and what it favours.** FSR 2.2 initialises on C1 under Enhanced with no
Godot warning or error of any kind, at 1280x720 on an RTX 5080. It is **sharper and noisier**, and
neither pass smears the hand-billboarded populations at a measurable distance. Mean Sobel gradient
over matched crops of a C1 rocket run whose temporal history was built at the flight's own frame
rate: hangar rooflines and apron seams 11.33 against TAA's 8.85 (1.28x), the whole world crop 9.77
against 7.11 (1.37x), the fireball 11.91 against 8.46 (1.41x), the cloud cards below the deck 0.642
against 0.531 (1.21x). The cost is **+0.06 to +0.13 ms of `gpu_ms`**, about 7 to 11 per cent, taken
window by window over two 900-frame runs at the same pose. Against that, the FSR 2.2 frame keeps the
clutter fade's own dither and the screen-space noise visible where Godot's TAA integrates them away,
which is plain in the montage's apron and grass. So the headless reading **favours FSR 2.2 on
resolve and TAA on cleanliness**, and it is a sharpness-against-stipple trade rather than the
ghosting defect the item expected. **The flight decides**, on C1 by day and C5 at night.

**What the measurement cannot show.** The two runs are `--no-det` (the config key is dropped under
`--det`), so no two frames are the same frame: the crops are matched poses, never matched pixels,
and every number is a texture statistic rather than a difference. A `--shots` burst writes a PNG per
frame and so runs at about 12 fps, five times the per-frame camera motion a flight gives either
pass, which flatters FSR 2.2 badly; the numbers above are from single shots at the real frame rate
for that reason, and the burst montages are kept only as the motion record. Mean gradient rewards
FSR 2.2's sharpening filter and its leftover dither alike, so it cannot separate resolved detail
from stipple. C1's daylight deck is a low-contrast card against a bright sky, so a card ghost would
not print there; C5's night deck is where that question is answerable. Montages:
`.scratch/eg2/A5/A5_rate_full.png`, `A5_rate_rocket.png`, `A5_rate_cards.png` (flight frame rate),
and `A5_fireball.png`, `A5_fireball_diff.png`, `A5_hangar_edges.png`, `A5_cloud_cards.png`,
`A5_cloud_deck.png` (the bursts).

**Original approach (kept for reference).**

**Goal.** A recorded verdict on whether FSR 2.2 at scale 1.0 reads better than Godot's TAA under
Enhanced; one of the two ships.

**Evidence (confidence: lead-only).** FSR 2.2 carries its own temporal AA and disables Godot's TAA
when selected; at scale 1.0 it is an AA pass, not an upscaler (disproven-claims row 1). Its
sharpening and its ghosting on billboards differ from TAA's and only a flight tells.

**Approach.** A `graphics.temporal=taa|fsr2` config key read once beside `GraphicsMode`, defaulting
to whichever A1 shipped; the user flies both on C1 and C5; the loser is removed, not left as a
second door. If FSR 2.2 wins, A2's Render Scale stays bilinear above 1.0 and FSR 2.2 applies only
at 1.0, which the setting's comment must say.

**Model recommendation.** medium, low effort: two flags and a flight.

**Verify.** The user's verdict on a side-by-side at the controls; the closing commit records it.

**⚠ Traps.** FSR 2.2 needs motion vectors from every billboard shader that moves its vertices by
hand (puffer, clouds, clutter); those hand-rolled billboards give wrong vectors and smear. That is
likely the deciding defect, so look for it first.

# Wave B, lit explosions

## B11 ☑ A burst omni light at each fireball, from the enhanced pool, flickering down over the burst

**Landed.** `WorldLights.AddBurst` registers a short-lived light at a fireball, and `Begin(dt)` ages
the live ones on the sim step `LightChannel.Tick` already has, submits them into `_pending` before
the frame's `LIGHT_STATE` lights and drops each one the frame its fireball stops burning. They ride
the existing distance fade, `MaxActive` rank and `OmniLight3D` pool with nothing new added to any of
the three. The decision point is `WorldEffectsFactory.RegisterBurstLight` at the effect sink, which
asks `EffectCatalogue.IsBurstLight` (the four fireball-throwing impact effects) and `GraphicsMode`,
so the faithful path registers nothing and asks for nothing. Liveness is the effect's own
`ANIM_STATE`, not a timer. The envelope is one TUNE block next to `OmniAttenuationTune`: a 3.0
ignition gain, a 0.29 s e-fold that is spent by the 1.2 s the authored frame-buffer wash runs, a
4 m to 180 m range and the authored `he_light` colour, flickering at 9 Hz against a 1.73x second
sine with a per-burst phase stride so a salvo does not pulse in lockstep. The `burst-light` engine
suite drives a live rocket into a plate and pins one light under Enhanced, none on the faithful
path, none for a gun hit, a decayed energy 30 frames on, and the light gone with its fireball.

**Verified.** On the item worktree: rebuild warning-free; the new `burst-light` engine suite and
its three neighbours PASS (the faithful path commits 0 lights, an enhanced gun hit registers 0, an
enhanced rocket exactly 1 committed light and 1 pooled omni, below half energy after 30 frames,
dropped the frame the fireball's liveness goes false, the mode restored at the end); goldens
**19 shots hash-identical, zero movers**; comment caps, doc entries and encoding checks clean. The
C5 night captures under `.scratch/eg2/B11/` show the tower facades lit warm around two fireballs
under Enhanced and dark on the faithful path; C1 daylight cannot separate the flash from the mode's
other differences. Owed to the user at the controls: fire or lamp (peak 3.0, 9 Hz flicker depth,
0.29 s decay, 180 m reach), and whether a salvo into a city block reads as several bursts. C1's
airfield already fills the 16-light budget, so a burst there evicts the least significant beacon.
The branch battery runs again at the wave's end.

**Original approach (kept for reference).**

**Goal.** A rocket or bomb burst lights the terrain, buildings and aircraft around it for the
fireball's life, brightest at ignition and flickering down; the faithful path draws no light.

**Evidence (confidence: direction-sound).** `WorldLights.cs` already mirrors committed lights onto
an `OmniLight3D` pool in Enhanced mode (`Begin`/`Add`/`Commit`, energy from the colour's peak
channel, `OmniAttenuationTune`). The effect chain that spawns the fireball is
`EffectCatalogue`/`AnimRuntime.PlayEffectAt` through `EffectSink` (`docs/org/ordnanceTypes.md`),
and the upper-ring rule (`Projectile.cs:601`) shows how an Enhanced-only rule is threaded through
that sink without touching the faithful spawn.

**Approach.** An `EffectSink` hook at the fireball spawn that, under Enhanced, registers a short-lived
light source (position, colour off the fireball's authored ramp, range TUNE, life equal to the
fireball's) with `WorldLights` so it rides the same `Add`/`Commit` and the same pool; the energy
envelope is a TUNE curve (ignition peak, a few Hz flicker, decay to zero) kept in one constant
block with the other Enhanced TUNE values. No new node type; the pool grows as it does today.

**Model recommendation.** high: it crosses the effect sink, the light pool and the per-frame commit.

**Verify.** `--chapter=C1 --graphics=enhanced --fire` at dusk over tarmac at the controls, a rocket
hit beside a hangar: the hangar wall and the ground flash and settle. Faithful goldens zero movers;
`WorldLights.LogOnce` count rises by the live bursts only.

**⚠ Traps.** The pool has no shadows and must stay that way (cost). A light that outlives its
fireball reads as a bug; tie the life to the emitter's, not a timer. Do not touch the fireball's
own sprite here; that is B12.

## B12 ☑ The additive fireball frames bloom

**Landed.** The item's premise is wrong and the disproof is part of the change. An inventory of
every `PUFFER_STATE` block in the install (145 `zrdr` files, 26 distinct sprite textures) against
each chapter's texture manifest shows **not one puffer sprite carries the additive bit**, so
`MultiMeshEmitterRenderer`'s additive layer is never built for any shipped effect and a gain on
the additive variant would have moved nothing. The additive `fire101`…`fire112` flipbook that the
census does hold belongs to `effects.zrd`'s `EFFECTS` table, which `EffectCycles` installs on mesh
materials rather than on a puffer. The item's own trap sanctions the alternative, so the gate is
the texture name: `MultiMeshEmitterRenderer.IsFireSprite` names `fire_f01`…`fire_f06`, the flipbook
every explosion puffer sequences, and `Puffer.Create` resolves it per atlas column beside the blend
verdict, keeping this seam free of `TextureArchive`. The column's gain rides `INSTANCE_CUSTOM.z`
into a new `v_gain` varying and multiplies `ALBEDO` before the fog mix, so a fogged fireball dims
instead of blooming through the wall. `EnhancedFireGain` is 2.0, a TUNE: the flipbook's
alpha-weighted linear peaks run 0.814, 0.714, 0.540, 0.429, 0.292, 0.268, so at 2.0 the first two
frames cross the 1.0 threshold and the tail stays under it, and the flipbook's own authored falloff
decides which frames halo. Luminance cannot be the gate (`smoke101` peaks at 1.000 against
`fire_f03`'s 0.540), and the sprites left out of the set reach 1.0 unaided, so a wider set would
halo every gun strike and pole lamp. The shader-variant key gained the mode, so the faithful path
compiles text with no gain term at all rather than a gain of one.

**Verified.** On the item worktree: rebuild warning-free; the new `puffer-fire-glow` engine suite
PASSes over the shipped C1 archive (no puffer sprite additive, the named set case-insensitive and
exclusive, the fire column's gain 2.0 and the smoke column's 1.0 read back off the MultiMesh under
Enhanced, both 1.0 and no `v_gain` in the compiled shader on the faithful path); the full battery
runs `4568 passed, 0 failed, 2 skipped of 4570` units, `350 passed, 0 failed` engine suites and goldens
**19 shots hash-identical, zero movers**; comment caps, doc entries and encoding checks clean.
Headless captures under `.scratch/eg2/B12/` (`b12-c1-tarmac.png`, `b12-c5-night.png`) put faithful,
enhanced without the gain and enhanced with it side by side on a rocket hit. Measured on the C1
tarmac hit: the fireball core's peak linear luminance rises 0.4365 to 0.6398 and the flame above it
0.4205 to 0.5986, the halo reaches about 80 px past the sprite, and the smoke plume and the HUD are
bit-identical between the two enhanced runs. The C5 night hit measures 0.3861 to 0.5726 in the core
with 9 pixels of the neighbouring smoke moving by 1/255 of bloom bleed. Owed to the user at the
controls: whether 2.0 is the right lift, whether the halo should reach further, and whether the
first two frames alone crossing the threshold reads as a flash or as a sustained glow.

**Original approach (kept for reference).**

**Goal.** The fireball's additive frames exceed the glow threshold and bloom under Enhanced; smoke
and every mix-blend frame stay below it.

**Evidence (confidence: traced).** `Launcher.cs` `EnableGlowAndTonemap`: threshold 1.0, and its
comment states the contract that only the glow-arm sprites (C21 of the first plan) exceed 1.0.
`EmitterRenderer.cs` picks the blend per atlas column off the texture's additive bit and writes
`ALBEDO = t.rgb * srgb_to_linear(ramp)`, which never exceeds 1.0, so today no fireball blooms.

**Approach.** In the additive shader variant only, under Enhanced (a shader key bit as the
world shaders do), multiply `ALBEDO` by a TUNE `enhanced_additive_gain` so the bright core crosses
1.0 and the falloff does not; the HDR scale and luminance cap already bound it. Extend the
threshold comment's contract to name the additive puffer columns.

**Model recommendation.** medium.

**Verify.** The same C1 hit as B11 at the controls: the core halos, the smoke does not. Faithful
goldens zero movers (the gain is under the mode).

**⚠ Traps.** A gain on the whole additive column blooms the tracer and muzzle sprites too if they
share the additive bit; check the `Puffer` population first and gate by texture if they do. Screen
blend keeps the halo additive; do not switch the blend mode to get more glow.

## B13 ☑ Sun-shaded smoke billboards

**Landed.** Under Enhanced the mix-blend variant of `MultiMeshEmitterRenderer`'s shader grades each
smoke puff by the sun. `vertex()` already builds the quad from the camera's right and up, so the sun
projects into the sprite's own UV with two dots against `INV_VIEW_MATRIX[0]` and `[1]`, and that
vector against `UV - 0.5` is the gradient across the card. The gate is the texture name the way
B12's fire lift is: `IsSmokeSprite` names `smoke101`…`smoke103` and `thickblksmoke01`…`03`,
`Puffer.Create` resolves it per atlas column beside the blend and fire verdicts, and the mark rides
`INSTANCE_CUSTOM.w` into a `v_shade` varying, so the fire flipbook, the water splashes, the flares
and the gun sparks are untouched and the ground smokeball keeps its soft-particle exemption. The
shader-variant key carries the mode, so the faithful path compiles text with no gradient term at
all: the `csky_sun.gdshaderinc` include and the varying are absent as well, not neutralised.
`EnhancedSmokeGradient` is 0.2, a TUNE: on the C1 rocket plume one puff's sun-side to far-side
luminance ratio moves 1.046 to 1.061 for a largest pixel delta of 7 of 255, which reads as puffs
turned toward the light, while 0.45 (ratio 1.075, 12 levels) prints the same profile visibly on
every sprite in the stack, the lattice C21 warned about. A plume is a stack of overlapping
quads, so a per-card amplitude that looks gentle alone accumulates through the stack.
`EnhancedSmokeCeiling` 0.98 clamps inside the smoke branch alone, so a lifted `smoke101` (already
1.000 linear at its peak) cannot cross the 1.0 glow threshold B12 reserved for the fire flipbook,
and `magnesiumtip` and `poleflare` keep their unclamped 1.0. The sun-direction global the approach
left open is `csky_sun_dir`, declared in `CSVM/shaders/csky_sun.gdshaderinc` and written by
`WeatherRig.WriteSunDirection` off the sun light's basis; C21 added it and this item reuses it
unchanged.

**The transmission rim was tried and dropped.** The puffer atlas is baked without mipmaps and
sampled `filter_linear`, so C21's `textureLod(…, 3.0)` blur has no chain to read and a blur that
wide would bleed across the neighbouring atlas column. Built instead as three unblurred taps
stepped toward the sun and clamped inside the column, it moves at most 3 of 255 levels with the sun
to one side and 4 at the backlit pose it exists for, against three extra texture samples per smoke
fragment. The smoke masks are smooth blobs rather than the cloud cards' ragged edges, so the
brightening lands where alpha is already too low to reach the frame. The term is out of the shader
and the disproof is recorded in `docs/org/textures.md` beside B12's note.

**Verified.** On the item worktree: rebuild warning-free; the new `puffer-smoke-sun` engine suite
PASSes (the named set case-insensitive and exclusive against the fire flipbook, the flares, the
splashes, the gun bits and the cloud cards; the smoke column's mark read back off the MultiMesh as
1 under Enhanced and 0 on the faithful path, the fire column 0 in both; `v_shade` and
`csky_sun_dir` in the compiled mix shader under Enhanced alone, and absent from the additive
variant even with a marked column); the full battery runs `4568 passed, 0 failed, 2 skipped of
4570` units, `352 passed, 0 failed` engine suites and goldens `19 shot(s) hash-identical`; comment
caps, doc entries and encoding checks clean. Headless captures under `.scratch/eg2/B13/` put
faithful, enhanced with the term neutralised, and enhanced at 0.10, 0.20 and 0.45 side by side on a
C1 rocket hit's standing smoke column, once with the sun to one side
(`b13-c1-rocket-plume-sun-aside.png`) and once flying into it
(`b13-c1-rocket-plume-into-sun.png`), with the whole frame at
`b13-c1-rocket-full-frame.png` and the dropped rim in isolation at `b13-rim-dropped-sun-aside.png`
and `b13-rim-dropped-into-sun.png`. Between the term off and the shipped 0.2, 3.95% of the frame
moves and all of it is in the plume. Nose into the sun the grade reads as nothing at any amplitude
(ratio 0.932 throughout), because the sun then projects to almost nothing in the quad's plane; that
is a limit of grading a camera-facing card rather than a defect, and only a volumetric treatment
would answer it. Owed to the user at the controls: whether 0.2 is the right amplitude, whether the
stack of overlapping quads reads as a lit plume or as a lattice of identical sprites, and whether
the backlit pose wants anything at all.

**Original approach (kept for reference).**

**Goal.** Mix-blend smoke reads lit by the sun: brighter on the sun side of each puff, darker on the
far side, under Enhanced.

**Evidence (confidence: lead-only).** The smoke columns are the mix-blend variant of
`EmitterRenderer.cs`'s shader, `unshaded`, one colour ramp per emitter. The reference technique
(data survey) grades an unshaded billboard by the sun direction projected into the billboard's own
right/up axes; the emitter shader already has the billboard basis in `vertex()`.

**Approach.** In the mix variant under Enhanced, a gradient across the quad from the sun direction
projected into billboard space (a TUNE amplitude), plus the same transmission rim as C21 if it
reads well on the smoke masks. The sun direction global is the one `WeatherRig` already writes for
the lit world (`csky_sun_dir`, added by C21 in `CSVM/shaders/csky_sun.gdshaderinc`).

**Model recommendation.** high: shader look work judged by eye.

**Verify.** A C1 bomb hit at low sun at the controls, smoke column drifting; a montage of gradient
amplitudes for the user to pick.

**⚠ Traps.** Sequential with B12 (same shader). The smokeball sits on the ground with soft
particles disabled; keep that arm's exemption.

## B14 ☑ Heat shimmer over a fireball

**Landed.** `Effects/HeatShimmer.cs` is a pool of eight billboard quads in ONE MultiMesh, so every
live burst shares one draw call and, more to the point, one colour-buffer copy rather than one each.
The spatial shader billboards each quad in the vertex stage, samples `hint_screen_texture` at a UV
offset from two octaves of value noise scrolled by `csky_time`, and scales that offset by the quad's
own screen half-width, so a burst two hundred metres off wobbles the few pixels it covers instead of
the same screen fraction a near one does. The sample is written back with no tint and no gain: a
blend between two samples of the same buffer cannot exceed the brighter of them, which is what keeps
B12's fire flipbook from being lifted over the glow threshold a second time. The quad writes no
depth and casts no shadow. The decision point is `WorldEffectsFactory.RegisterHeatShimmer` beside
B11's `RegisterBurstLight`, holding the mode gate and the name gate together, and the sink calls
both unconditionally so one place decides each. It reuses `EffectCatalogue.BurstLightAnimNames`
rather than a sibling list, on the ground that a fireball big enough to light what stands around it
is the fireball that heats the air over it; the reasoning sits on `IsBurstLight`. Liveness is the
same closure the burst light reads, the fireball's own `ANIM_STATE`, so a quad is retired the frame
the fireball ends, and the amplitude decays exponentially under it. ⚠ Godot takes the screen copy
a transparent material reads ONCE, before the transparent pass, so it holds the opaque world and
neither the fireball nor its smoke. That has two consequences, both measured rather than assumed.
The quad's
mask tapers to zero at its lower end and its centre stands 30 m over the burst, because drawn across
the flame it paints bare terrain over it: at a 16 m lift that cost 16949 changed pixels against the
5301 the same amplitude changed once the quad was clear. And this item's own instruction to draw the
quad after the fireball is what the copy makes wrong, so the material carries render priority -1 and
draws BEFORE the other transparents: the fireball and its smoke then composite over the shimmer
instead of being erased by it, which took the changed pixels from 5845 to 2191 at the same amplitude
and left the burst itself untouched. The `heat-shimmer` engine suite drives a live rocket into a
plate and pins the pool, the cap and the drop.

**Verified.** On the item worktree: rebuild warning-free; the complete `RunTests.ps1` runs units
4574 passed of 4576, engine 353 of 354 and goldens **19 shots hash-identical, zero movers**, engine
errors clean. The one engine failure is `ai-wave-launch-hitch`, which times a spawn frame on the
wall clock and fails on a machine sharing its GPU with several other sessions; run alone on this
tree it PASSES (median launch frame 17.7 ms at one rig, 16.6 ms at four, both under their bars). The
new `heat-shimmer` suite PASSES (the faithful path builds no pool and adds no node, a gun hit builds
nothing, an enhanced rocket takes exactly one quad that draws while the fireball runs and is gone
the frame liveness goes false, a salvo of cap+2 holds `LiveCount` at 8 with 2 recycles and one node,
and the mode is restored in a finally); comment caps, doc entries and encoding checks clean. Cost at
the C1
rocket-hit pose, `--perf --no-vsync`, gpu_ms per 60-frame window, shimmer off against on: one rocket
3.00/2.00/2.35/2.61 against 2.43/2.01/2.35/2.61, a twelve-rocket salvo 2.16/1.91/2.19/2.53 against
2.76/1.90/2.20/2.56. Over the three windows the bursts live in, the largest difference either way is
0.03 ms, and `draws` rises by the one instanced draw the pool submits while a quad lives; the first
window, which carries the world build and the shader compile, swings further than the effect does.
The montage under `.scratch/eg2/B14/` stands at `--direction=0.08,-0.40,0.91` off B11's pose, where
the burst goes off beside the apron edge and a checkered crate: faithful, enhanced without the
shimmer, enhanced with it, and a crop of the same crate at three amplitudes. Against the no-shimmer
frame the change is confined to the quad's footprint, about 70 by 110 px, and grows with the
amplitude (1849, 2191, 2507 changed pixels at 0.16, 0.32, 0.80); the displacement is sub-pixel at
the median
with peaks of 2.2 px by a sub-pixel edge fit, which at this range is a crate outline that bends
rather than jumps. A still can only show a displaced edge; the wobble itself, which is the point of
the effect, is owed to the user at the controls. So is the amplitude: 0.32 ships, 0.16 is nearly
invisible at a still and 0.80 visibly bends the crate's checkers.

**Original approach (kept for reference).**

**Goal.** The air over and behind a fireball refracts for its life under Enhanced.

**Evidence (confidence: lead-only).** No refraction pass exists; Godot's `screen_texture` sampling
in a spatial shader with a noise-driven UV offset is the ordinary way and costs one extra
transparent quad per burst.

**Approach.** One billboard quad per fireball spawn under Enhanced, a shader that samples the
screen texture with an animated noise offset scaled by a radial mask, life tied to the fireball's;
spawned through the same sink hook as B11.

**Model recommendation.** medium.

**Verify.** The C1 hit at the controls against a hangar edge: the edge wobbles for a second.

**⚠ Traps.** Screen-texture reads force a copy of the colour buffer; with several bursts live that
is several copies, so measure under D32. The quad must draw after the fireball and be excluded from
the glow pass.

## B15 ☐ Scorch decals at a hit

**Goal.** A rocket or bomb hit on ground leaves a dark scorch under Enhanced; the faithful path
keeps the crater carve and nothing else.

**Evidence (confidence: lead-only).** `docs/org/craters.md`: the original carves a crater from the
weapon's `CRATER` block and destroys clutter; no scorch texture exists in the data. Godot's
`Decal` node projects onto the lit world; on the faithful fullbright world it would not read.

**Approach.** Under Enhanced, at the crater build (`CraterField`/`TerrainCarve`) or the impact
outcome for a weapon with no crater, place a `Decal` with a procedural radial scorch texture
(TUNE size from the weapon's crater radius, TUNE fade), pooled and recycled with the same count
cap the crater field keeps.

**Model recommendation.** medium.

**Verify.** `--chapter=C1 --fire` at the controls, three rocket hits on tarmac and one on grass;
decals sit on the surface and fade at the cap. Faithful goldens zero movers.

**⚠ Traps.** Decals on water read wrong; skip the water surfaces `ClassifySurface` names.
Alpha-to-coverage (A4) does not apply to decals.

# Wave C, lit clouds

## C21 ☑ Lit cloud cards: sun tint, transmission rim, shadowed undersides, soft depth fade

**Landed.** `FogVolumeClutter.ShaderCode` takes a third argument, `enhanced`, passed
`GraphicsMode.Enhanced` at the one `Build` call site. Every enhanced hole in the shader template is
empty and at end of line on the faithful path, so the faithful shader text is byte for byte the one
it emitted before. The enhanced arm grades the shipped colour by four terms: a sun tint from
`dot(view_dir, csky_sun_dir)`, a transmission rim from the mask's blurred alpha sampled one step
toward the sun in billboard UV space (brightening the thin sun-side edge, darkening texels behind
the card's own bulk), an underside darkening from the card's authored face normal on a half-lambert
ramp, and the soft-particle depth fade copied from `EmitterRenderer.cs`. The graded albedo is
clamped to 0.98 so it never crosses the enhanced glow threshold reserved for the glow-arm sprites.
The sun direction arrives as one new global shader uniform, `csky_sun_dir`, declared in
`CSVM/shaders/csky_sun.gdshaderinc`, registered in `Launcher` beside `csky_world_light` in both
graphics modes, and written by `WeatherRig.WriteSunDirection` at lighting setup and on every zone
apply. B13 reuses it unchanged.

Shadow-map darkening is **skipped**. The field is `unshaded` with `shadows_disabled`, so Godot's
`light()` path is unreachable from it, and dropping `unshaded` would put a billboard that exists to
match the faithful field through the whole PBR pipeline. That is not cheap, so the item's own
condition for including it is not met.

The amplitudes are gentle on purpose. The scatter lays one repeated card on a staggered lattice, so
a strong per-card grade paints that lattice as a visible grid of identical puffs on the near deck;
that shows plainly at tint 0.15 / rim 0.30 / core 0.70 and is already visible at 0.10 / 0.20 / 0.45.
The shipped pair is tint 0.10, rim 0.15, core shadow 0.25, underside 0.35, rim offset 0.2 UV at mip
3, fade 12 m.

The night cards do **not** read directional, and `BL-325` stays open. C1B ships no `fvol*` volumes
at all, so its night clouds are the placed `cloudparent` facades, a different population this
shader never reaches. C5's fvol field is a low ground-haze population (its instance bounds run from
about -24 m to 250 m altitude) of upward-facing cards, and the authored angled clutter fade drops
them at any near-level view, so no C5 night pose found here draws one. The item's shader is correct
and lands; the night measurement `BL-325` records is about a population it does not touch.

**Verified.** On the item worktree: build warning-free; units 127 passed 0 failed over the
FogVolume, Clutter and Weather filters; engine suites 348 passed 0 failed; goldens **19 shots
hash-identical, zero movers** (`c1-cloud-field` and `c5-city-night` unchanged); comment caps, doc
entries and encoding checks clean. Grade isolation at the C1 poses (enhanced with the grade
neutralised against enhanced as shipped) moves about a quarter of the pixels by at most 13 levels,
a soft reshaping rather than a wash. The soft depth fade is wired (confirmed by instrumenting the
scene depth) but fires at no pose found, since the cards sit against sky or fog. The night cards do
not read directional: C1B ships no fvol volumes and C5's fvol field is ground haze the angled fade
drops at level views, so `BL-325` stays open and this item does not reach its population. Owed to
the user: the strength (shipped, medium or strong, from the sweep under `.scratch/eg2/C21/`) and
the look in flight. The branch battery runs again at the wave's end.

**Original approach (kept for reference).**

**Goal.** Under Enhanced the fvol cloud cards read as lit puffs: brighter toward the sun, a bright
rim on the sun side, darker on the underside and where the shadow map covers them, and no hard cut
where a card meets terrain or an aircraft. The faithful field is untouched.

**Evidence (confidence: direction-sound).** `FogVolumeClutter.cs` `ShaderCode(lit, fogged)`: one
static MultiMesh per kind, hand-billboarded, `unshaded`, `depth_draw_never`, no depth fade; the
sprite's face normal is in `INSTANCE_CUSTOM.xyz`. The reference technique (data survey) is the
same architecture with three terms this shader lacks: a sun tint from `dot(viewDir, sunDir)`, a
transmission rim from a blurred alpha sample offset along the sun direction in billboard space,
and a per-instance tint. The `BL-325` measurement (p90 218 near the moon vs 70 away) says the
original itself lights night clouds directionally; this item is the arm that can answer it under
Enhanced, and `BL-325` stays open for the faithful path.

**Approach.** A third shader variant, `ShaderCode(lit, fogged, enhanced)`, selected by
`GraphicsMode.Enhanced` so the faithful text is byte-identical: (1) the sun tint and transmission
rim as the reference formulas, amplitudes TUNE; (2) an underside darkening from the card's own
face normal against the sun; (3) the soft-particle depth fade copied from `EmitterRenderer.cs`
(the `hint_depth_texture` sample and the last-metres fade); (4) shadow-map darkening only if
Godot's `light()` path can be reached from an unshaded billboard cheaply, else skipped and
recorded. The sun direction comes from the same global B13 uses. The authored 240 colour and the
authored masks stay (decision 6).

**Model recommendation.** high: shader look work with four interacting terms, judged by montage.

**Verify.** `--chapter=C1 --graphics=enhanced` at the controls by day through the cloud deck, then
C5 and C1B at night; a montage of tint/rim amplitudes for the user to pick. Faithful goldens zero
movers; the cloud-card golden (C1 at 1208 m looking down the deck) is the faithful control.

**⚠ Traps.** The comment at `FogVolumeClutter.cs:225` forbids scaling the authored colour and it
still holds on the faithful path; the enhanced variant is the one place a tint is allowed, and the
comment must say so. The transmission offset is in billboard UV space, so it needs the billboard's
right/up, which the vertex function already builds. Depth fade on a `depth_draw_never` field is
fine, but the depth texture excludes the field itself, so cards do not fade against each other.

## C22 ☑ FogVolume banks from the authored fvol slabs

**Landed.** A new module, `Effects/FogVolumeBanks.cs`, builds under Enhanced Graphics alone and only
where the chapter ships `fvol*` volumes: one bank per authored volume, laid over that volume's own
bounds, all of them sharing one `FogMaterial`. `GameSession` creates it beside the cloud field, hands
it to `WeatherRig` so the rig's zone apply writes the scattering colour, and calls the module's
`ApplyFroxelFog` on the one long-lived Environment either way, so a world that builds no bank
actively clears `VolumetricFogEnabled` rather than inheriting the last chapter's. `--no-fog` covers
the banks as it covers the zone fog and the whiteout.

**Zero global density, the banks carrying all of it.** A global density is fog everywhere, including
the metres the authored `csky_fog_*` ramp already grades, so the two would haze the same air twice;
with `VolumetricFogDensity` at 0 the froxel pass exists only where a volume stands. `SkyAffect` stays
at 1: Godot applies it to `FogVolume`s as well as to the background, so a lower value would fade the
banks out exactly where this item is judged, against the sky. The froxel buffer stays at Godot's own
64 x 64 x 64 over a 1,024 m length, which also keeps the camera-anchored horizon dome (kilometres
out) outside the pass, so its own fog arm is never fogged a second time.

**Boxes, not the volume's own mesh, and tiled.** Godot 4 has no mesh-shaped fog volume at all
(`FogVolumeShape` is Ellipsoid, Cone, Cylinder, Box or World), so the shape is a box over the
volume's bounds: exact for C1/C2B/C4's axis-aligned slab pieces and for C5's street prisms as boxes,
an over-estimate for C1C's twelve tapering build-up frusta. ⚠ **Measured on this engine: a
`FogVolume` box much wider than about 2,048 m contributes nothing to the froxel pass, silently and
with no error.** At C1's authored 8,192 m slab pieces the frame is pixel-identical to one with no
banks at all; at 2,048 m the bank renders whether or not the camera stands over it. So each volume's
bounds go down as a grid of tiles no wider than that, seamlessly because the edge fade is 0: C1's
nine pieces become 36 boxes, C5's seventeen prisms 57.

**Density.** Where `fogvol.zrd` arms `fog_zone`, the bank reaches an optical depth of 1 over the same
`interior_fog_fade_dist` metres the whiteout curtain hands off in, so the two agree instead of
carrying two unrelated numbers: C5's 16 m gives 0.0625 per metre. Everywhere else there is no
authored statement about the inside of a cloud and one ambient TUNE stands, shipped at **0.002 per
metre**: a climb straight through the 120 m deck scatters out about a fifth of what is behind it and
the cards keep their shape. At 0.004 the underside of the deck washes to a flat tint of the sun's own
colour, which is the bank drawing itself instead of the cloud it sits in; the montage carries 0.002,
0.004 and 0.01 at two poses for the user to overrule this.

**Verified.** On the item worktree, complete `RunTests.ps1`: **goldens 19 shots hash-identical, zero
movers**; units 4,568 passed, 0 failed, 2 skipped of 4,570; engine 351 passed, 1 failed, engine
errors clean. The one engine failure is `ai-wave-launch-hitch`, a wall-clock hitch measurement, and
it is not this item: it fails the same way on the untouched main tree standalone and passes on this
tree standalone, while three other agents are building and probing on the same machine. Comment
caps, doc entries and encoding checks clean. A new engine suite, `fogvol-banks`, pins the arm from
both sides: C1 builds 9 banks over 36 boxes at 0.002 per metre and C5 17 over 57 at 0.0625, each
volume's tiles union back to exactly its authored bounds with no box wider than the engine renders,
the scattering colour follows the applied zone unless the chapter authored its own whiteout colour,
and on the faithful presentation `Create` returns nothing and the Environment flag is left off.

**Cost**, `--perf --det` at 1280x720 on the C1 into-sun pose, steady windows after warm-up: **0.42 ms
gpu without the banks, 0.49 ms with**, so the froxel pass plus 36 boxes is about **+0.06 ms**, which
is what D32 gets to hold against the rest of the stack.

**Owed to the user: the density and the look in flight.** The montage is under
`.scratch/eg2/C22/`, three presentations at each of four poses plus the density steps. What it can
show: from under the deck the bank is unmistakable, it deepens the underside and puts a soft
sun-scatter glow where the sun is, and the density steps separate cleanly there. What it cannot
show: from above the deck the cards hide the bank almost entirely (decision 5, and the shots differ
by about one level of 255), and no shafts appear anywhere, because the cloud cards are alpha-blended
billboards that cast no shadow, so there is nothing to break the light into beams. ⚠ C5's bank is
nearly a pure absorber, since the chapter's authored whiteout colour is `[16,16,16]`: through the
armed zone at night it takes the frame mean from 25 to 14. That is the agreement the item asked for,
and it is also the one reading most likely to be judged too strong.

**Original approach (kept for reference).**

**Goal.** Under Enhanced a soft volumetric bank sits under the cards inside each authored fvol
volume, scattering the sun and casting shafts; the whiteout band in C5 reads as being inside a
cloud rather than a flat fog colour.

**Evidence (confidence: lead-only).** `fogvol.zrd` (`docs/formats/fogvol.md`) gives every `fvol*`
volume's polygons; `FogVolumeClutter` walks them. Godot's `FogVolume` nodes fill the Environment's
froxel volumetric fog, which the enhanced Environment does not enable today. The whiteout is
`WeatherRig`'s `FogVolumeWhiteout`, armed only where `fogvol.zrd` arms `fog_zone`.

**Approach.** Enable `VolumetricFogEnabled` on the enhanced Environment with a low global density,
and place one `FogVolume` (box or the volume's own mesh as `Shape = LocalMesh`) per authored fvol
volume with a TUNE density and the zone's fog colour; tie its density to the whiteout band so the
two agree. Off in the faithful path.

**Model recommendation.** high.

**Verify.** C5 at night through the whiteout and C1 by day looking along the deck at the sun;
montage of densities. D32 measures the froxel cost.

**⚠ Traps.** Froxel fog is low resolution and swims with TAA off, so judge after A1. The sky dome
is gamez geometry drawn over the background; volumetric fog in front of it is fine, but the dome's
own fog arm must not double-fog. The cards stay (decision 5); the bank is under them.

## C23 ☐ A rendered puff sprite set under Enhanced, picked by montage

**Goal.** A small set of rendered cloud puff sprites replaces the authored masks per fvol kind
under Enhanced, if the user picks them over the authored masks on a montage.

**Evidence (confidence: lead-only).** The reference thread's look comes as much from its five
Blender-rendered puffs as from its shader; the authored masks are small constant-RGB alpha masks.
No rendered set exists yet.

**Approach.** Render a handful of puffs (Blender is available through the MCP rig; volume
scatter, a sun key, alpha over white) at the authored masks' aspect, map each fvol kind to one by
name in a small table read only under Enhanced, and put the swap behind C21's variant. Montage
authored vs rendered on C1 for the user.

**Model recommendation.** high for the montage judgement; medium for the render pipeline.

**Verify.** The user's pick; faithful goldens zero movers.

**⚠ Traps.** Remake-only art, so it lives beside the parked upscaling under the same rule: the
faithful path never reads it. Downscaled or lossy sprites read as blur; keep PNG at the render
size.

# Wave E, the feel of speed

## E41 ☑ Wind streaks past the camera, keyed to speed and G, over the authored speed cue

**Landed.** `Effects/WindStreaks.cs` draws a camera-local streak field under Enhanced only, over the
authored wisps, which are untouched. It follows `Precipitation`: one MultiMesh of 1400 thin quads
whose positions come out of a per-instance seed and `CAMERA_POSITION_WORLD`, wrapped into a
camera-centred 45 m box, with the same near fade (6 m, which is what keeps a streak off the cockpit
glass at the near plane) and rim fade. Each quad is an axial billboard whose long axis is the
aircraft's world velocity, and the sprite is procedural, a gaussian across the quad tapering to
nothing at both ends, so there is no texture. `Create` returns null unless `GraphicsMode.Enhanced`;
`HumanFlightAdapter` builds one per player pane beside the speed cue and stamps the pane's visual
layer on it, and `FlightController` drives it from the same block that drives the cue, resets it on
respawn and on the crash cut, and frees it at teardown. `Update` writes four uniforms a frame:
opacity is zero at or below a TUNE 0.80 of `PlaneStats.FdSpeed`, ramps to a 0.45 master over the
rest of the envelope, and adds 0.6 × the excess of `FlightModel.LoadFactorDemand` over 1 G scaled
to 5 G; length is airspeed × 0.035 s, at least 1.5 m; and the field's drift along the flight path
(a 0.25 exaggeration of the aircraft's own motion past a world-fixed field) is accumulated on the
CPU and wrapped into the box cell. That accumulation is deliberate and is the one place this
diverges from the rain: the drift RATE changes with airspeed, so any `rate × clock` form, `TIME`
or `csky_time` alike, teleports the whole population the moment the throttle moves. `Rng.WindStreaks`
is its own stream so no faithful draw depends on which presentation ran, and
`WindStreaks.AuthoredWispAlphaScale` (1, no dimming) is where the wisp-dimming TUNE would go.

**Verified.** The complete `RunTests.ps1` on the item worktree: build PASS, units **4568 passed, 0
failed, 2 skipped of 4570**, engine **351 passed, 0 failed, 0 skipped**, errors clean, goldens **19
shots hash-identical**, 227.1 s total, exit 0. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and
`CheckEncoding.ps1` clean. The new `wind-streaks` engine suite proves the gate and the drive: the
faithful presentation builds no field and the enhanced one does, the shader reads no clock at all,
cruise at 0.76 of rated max writes zero alpha and leaves the draw off while full throttle raises
both, alpha never falls as the speed fraction rises, a 4 G pull at one speed thickens the field
while the same pull below the cruise gate still draws nothing, the streak axis tracks the velocity,
the length grows with airspeed, and 100 s at 1.3× rated max leaves the accumulated drift inside the
90 m cell. C1 probes at `--pos=-6144,1100,-6144` under `--graphics=enhanced` confirm it renders:
nothing at 228 mph (0.76 of the Bloodhawk's 135 m/s `fd_speed`) in all three views, and streaks in
chase, cockpit and nose at 326 mph in a shallow dive, with the wisps emitting in both.
**Owed to the user:** the look at the controls, where the motion is most of the cue a still cannot
show; the density and length pick off the four pairs in `.scratch/eg2/E41/`; and whether Enhanced
should dim the wisps, which ships as no dimming. A cockpit-view `--screenshot` composites a black
world through the cockpit pass, so those two shots carry `--no-cockpit-pass`; that is a capture
artifact of the pass, not of this item.

**Original approach (kept for reference).**

**Goal.** Under Enhanced, faint streaks stream past the camera along the aircraft's velocity,
invisible at cruise, rising with speed toward rated max and thickening in a hard turn or a dive,
so speed and a direction change are felt in every view. The authored wisps keep emitting.

**Evidence (confidence: lead-only).** Other flight games (Ace Combat, Project Wingman, Star Fox)
carry the sense of speed with a camera-local streak field whose density and length scale with
speed and G; the original's own cue is the world-space wisp field (`docs/formats/effects.md`),
whose visible duration falls with airspeed, which is the low-fidelity form of the same idea.
`Precipitation.cs` already draws a camera-centred, self-animating streak field with a near fade
and a rim fade, so the rendering pattern exists; what is new is the drive (velocity, G) and the
gate (Enhanced).

**Approach.** A new `Effects/WindStreaks.cs` on the `Precipitation` pattern: one MultiMesh in a
camera-centred box, each instance a thin streak quad aligned to the aircraft's world velocity,
positions derived in-shader from a seed and `csky_time` so the CPU writes only a handful of
uniforms per frame (velocity, speed fraction of the airframe's rated max, a G or turn-rate term,
camera position). Alpha is zero below a TUNE cruise fraction, rises with the speed fraction, and
adds a TUNE G term; streak length scales with speed. Built by the rig only when
`GraphicsMode.Enhanced`; per player pane like the speed cue. Whether Enhanced dims the authored
wisps beneath it is a TUNE the user picks in the montage (decision 12), not a change to
`SpeedCue.cs`.

**Model recommendation.** high: a look item with three interacting TUNE terms and a G reading off
the flight model. `<TODO: where the flight model exposes rated max speed and a turn-rate or load
factor the streaks can read>`

**Verify.** A C1 flight at the controls: level cruise shows nothing, full throttle and a dive
show streaks, a hard bank thickens them; cockpit, nose and chase views all read. A montage of
density and length pairs for the user. Faithful goldens zero movers; `--det` enhanced golden
(D31's rocket-hit pose) either excludes the field or pins it, decided at D31.

**⚠ Traps.** The rain field's header warns never to use Godot's `TIME`; drive from `csky_time`.
Streaks through the cockpit glass read wrong at the near plane: keep the near fade. Do not close
`BL-867` or edit the wisps' opacity from here. Split screen needs the field per pane, as the
speed cue's `decorate` hook does.

## E42 ☑ The chase camera lags the nose through a roll and widens its FOV with speed

**Landed.** Under Enhanced the chase camera's pose is built from a lagged copy of the aircraft's
attitude, so a roll or a yaw leaves it trailing the nose before it springs back, and the external
FOV widens with speed. The whole arm enters `Flight/CameraController.cs` through one new call,
`StepEnhancedCues(dt, attitude, speedFraction)`: it eases the lagged attitude toward the live one
at `TrailRate` through the exponential shape `dist_catch_up` uses, and stores what `SpeedFovWiden`
returns. `Chase` builds its offset direction, its image up and its look-ahead point from that
lagged attitude under Enhanced and from the live attitude otherwise; `RestoreExternalFov` adds the
widening, while the crash, death and flyby cuts take the built-in angle through the new private
`ApplyDecodedExternalFov`, since a cut holds a framing rather than riding the aeroplane. `Snap`
re-seeds both, so a respawn opens on the settled pose. `FlightController` makes the one call per
flown frame whichever view draws, handing it `_model.Speed / _model.Stats.FdSpeed`: `fd_speed` on
`PlaneStats` is where the flight model carries rated max speed, the airframe-independent scale the
authored figures are quoted on. `ExternalRadius`, `UpdateDynamics`, `DistTransient` and the
look-behind arm are untouched. The three TUNE constants sit in one block with their reasons: a
lag rate of 4/s (a 0.25 s time constant), 6° of widening at rated max, and nothing at or below 0.6
of it, held rather than growing in a dive past the rating. `Testing/ChaseTrailSuites.cs`
(`chase-trail`) and `CSVM.Tests/CameraControllerTrailTests.cs` pin it; `docs/org/cameraViews.md`
carries a remake-only subsection and `docs/architecture/Flight.md` the entry line.

**Verified.** On the item worktree the complete `RunTests.ps1` passes: build warning-free, units
4574 passed 0 failed 2 skipped of 4576, engine **350 suites passed 0 failed** with engine errors
clean, goldens **19 shots hash-identical, zero movers**. `CheckCommentCaps.ps1`,
`CheckDocEntries.ps1` and `CheckEncoding.ps1` are clean. The `chase-trail` suite flies one
90°/s roll per presentation and reads the camera it left: under the faithful presentation the cue
step moves **0 of 60** frames of camera pose and the external FOV is 75.0000° at rated max, at
cruise and at the crash cut, while the enhanced arm moves all 60, trails the nose by 8.33° against
the faithful 2.8° at the end of the roll, converges to 1.45° after half a second and 0.0037° after
two, and carries 81.0000° at rated max, 75.0000° at cruise and 75.0000° through the crash cut.
`CameraControllerTrailTests` pins the easing as a per-real-second time constant (the same second
leaves the same angle at 60 and at 240 frames a second) and the ramp's ends. Headless evidence for
the user is in `.scratch/eg2/E42/`: `montage_roll.png` (the same C1 sortie mid-roll, faithful left
and enhanced right, from `--hold=0,0,0,1@2.5;0,1,0,1@2 --frames=170 --shots=16`) and
`montage_speed.png` (the same pose at 302 MPH, the airframe's rated max, with the widening open),
plus the 38 raw frames they were cut from. Not verified here: the judgement itself, whether a
0.25 s lag and 6° read right at the controls, which is the user's flight. Also seen: the first
battery run failed `ai-wave-launch-hitch` on a 220 ms four-pane launch frame against its 110 ms
bar and passed on the re-run, a timing suite on a loaded machine rather than anything this item
touches.

**Original approach (kept for reference).**

**Goal.** Under Enhanced the chase camera's orientation trails the aircraft's nose on a roll or
yaw and springs back, and its FOV widens a few degrees toward rated max speed; the decoded
distance law and the faithful chase camera are untouched.

**Evidence (confidence: lead-only).** `docs/org/cameraViews.md`: the decoded external camera has
a speed-and-acceleration distance law (ported) and no orientation lag or speed FOV; the external
FOV is one constant carried at construction (`CameraController.cs:142`,
`RestoreExternalFov`). Ace Combat and Project Wingman use both cues, and the original's own
distance transient already moves the camera with the throttle, which E42 extends rather than
replaces.

**Approach.** In `CameraController`, under Enhanced only: (1) a lagged copy of the aircraft's
attitude eased toward the live one at a TUNE rate (the same exponential shape the decoded
`dist_catch_up` uses), used for the chase camera's orientation while the distance law keeps
reading the live state; (2) `_externalFovDeg` plus a TUNE widening scaled by the speed fraction,
applied wherever `RestoreExternalFov` writes the external FOV, so the first-person views and the
death and crash cuts keep their decoded FOV. A `--graphics=original` run must produce the same
camera pose to the bit.

**Model recommendation.** high: the camera is decoded and every frame of it is pinned; the
Enhanced arm has to be provably inert on the faithful path.

**Verify.** At the controls on C1: a snap roll shows the camera trailing then settling, full
throttle widens the view; the faithful goldens and the camera suites zero movers; a golden with
the chase camera under Enhanced pinned at D31.

**⚠ Traps.** The decoded easing rates are per real second, not per sim step
(`cameraViews.md` "The easing dt is WALL time"); the lag rate here is TUNE but the same clock
rule applies, or the lag changes with the frame cap. The look-behind arm hard-codes its own
direction factor; leave it out of the lag. Do not touch `ExternalRadius`/`DistTransient`.

# Wave D, the net and the reading

## D31 ☐ An enhanced golden set under `--det`

**Goal.** A small `--graphics=enhanced` golden set pins the finished stack so later changes cannot
silently move it.

**Evidence (confidence: direction-sound).** `analysis/goldens/manifest.json` runs every shot under
`--det --mute`; under `--det` only the `--graphics=` flag reaches `GraphicsMode.Resolve`
(`docs/cli.md`), so an enhanced shot is one more entry with the flag. `RunTests.ps1 -Graphics`
today reaches only the perf and hitch stages (D31 of the first plan), by design.

**Approach.** Five entries: C1 lake (water, shadows), C5 night city (omni pool, whiteout), C1 deck
at 1208 m (clouds), the C1 rocket hit pose (explosions, a `--hold` that lands a burst in frame),
and the cockpit view. Each `exercises` field states what the shot covers today, under 250 chars,
no item ids (`CheckGoldenProse.ps1`). Pin them after A5's verdict so the temporal pass is settled.

**Model recommendation.** medium.

**Verify.** The golden stage passes twice in a row on the same tree (TAA's jitter must be
deterministic under `--det`; if it is not, the set pins with TAA off and the record says so).

**⚠ Traps.** A burst frame carries the previous frame; the manifest's single-shot path is the
un-jittered one. Do not append to an `exercises` field; rewrite it on a re-pin.

## D32 ☐ The perf reading over the finished stack, within 20% of the D31 baseline

**Goal.** A current frame-time table for the enhanced stack, and every new pass inside a 20%
envelope over the re-taken baseline, or a lever recorded for the one that is not.

**Evidence (confidence: direction-sound).** `git show 8aab7fa3`: C4 15.04, C5 13.60, C3 12.51 ms
enhanced at 4 panes, provisional against Wave E of the first plan. `docs/verification.md`
`PERF-13` and `PERF-25` bind how a hitch count and a moved cost may be read.

**Approach.** At Wave A's start, re-take the baseline on the current tree with
`RunTests.ps1 -Graphics enhanced` and the three targeted probes, three runs per cell, render scale
1.0; at the end, the same table over the finished stack. Each Wave B/C item also records its own
delta in its landing commit. If the total exceeds 20%, the ordered levers are FogVolume density
and froxel resolution (C22), heat-shimmer copies (B14), then SSAO quality.

**Model recommendation.** medium, low effort.

**Verify.** The two tables in the closing commit; `-Hitch` silent on the clean run.

**⚠ Traps.** Render scale above 1.0 is excluded from the budget (decision 9); make sure the saved
option is 100% or the run is under `--det`. The sim clock lags wall on the user's rig on some late
missions; read frame_ms from the perf probes, not from a flight's feel.
