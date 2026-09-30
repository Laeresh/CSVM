# Enhanced Graphics 2, the temporal pass, lit explosions and lit clouds

**ACTIVE PLAN** (written 2026-09-16). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The opt-in Enhanced mode (`GraphicsMode.Enhanced`, `--graphics=enhanced`) today lights the world
from the authored sun and ambient, mirrors the committed world lights onto real omni lights, casts
4-split soft shadow maps, runs SSAO, blurs a screen-space reflection on water, blooms only the
glow-arm sprites and tonemaps with AgX (`CSVM/src/Session/Launch/Launcher.cs` `SetupLighting`). Clouds are
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
(branch `upscaling`); `BL-322`'s C5 facade brightness, cited as context and left to its own item;
the aircraft specular constant; menu presentation work beyond the one new VIDEO row.
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
| 12 | What happens to the authored speed-cue wisps under Enhanced? | **They stay; the streaks are added over them.** The wisps are data (`speed_cue.zrd`), judged right on the faithful path (`git log --grep=BL-867`); whether Enhanced dims them under the streaks is a TUNE the user judges in E41's montage, not a decision made here. |
| 13 | Do the trees, rails and lattice that now blend rather than scissor change the plan? | **Yes: A4 covers only what stays scissored beside them**, which is 332 of the census's 388 cut names. Blended surfaces have no coverage edge to fix. |

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
- **The capture trap.** `Tooling/CaptureDirector.cs:109`: a `--shots` image is the previous
  frame's render; `_00` is un-jittered and `_01+` carries the burst camera's dither. A dither
  verdict comes from the controls or an undithered capture, never from a burst frame.
- **World lights under Enhanced.** `Mech3/WorldLights.cs`: `Begin`/`Add`/`Commit` per frame, and
  in Enhanced mode `Commit` mirrors the committed set onto a growing pool of `OmniLight3D`
  (`ShadowEnabled = false`, attenuation 0, an authored light's range from `OmniRange`), energy
  from the colour's peak channel. This is the pool a burst light joins.
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
- **The speed cue as shipped.** `Flight/Hud/SpeedCue.cs` loads each chapter's `speed_cue.zrd`
  verbatim: three `cuepufferN` states picked by camera altitude, emitted 60 m ahead of the player
  and left in world space for the aircraft to pass (`docs/formats/effects.md` "Aircraft speed-cue
  wisps"); off within 50 m of the ground. Their opacity was judged right at the controls
  (`git log --grep=BL-867`); their lateral spread already widens with the pane's aspect over 4:3, and the opacity
  is what the item still owes. The original authors no other speed cue: the
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
3. ☑ BL-803: the shadow-map bands on grazing surfaces under Enhanced
4. ☑ Alpha-to-coverage on the cutout surfaces under Enhanced
5. ☑ FSR 2.2 tried once as the alternative temporal pass, kept or parked on the user's verdict
6. ☑ An Anti-aliasing row on the VIDEO page, and a Render Scale that follows it down to 50%
7. ◐ C5 and the last two campaign missions hold a frame budget under Enhanced

### Wave B, lit explosions

11. ☑ A burst omni light at each fireball, from the enhanced pool, flickering down over the burst
12. ☑ The additive fireball frames bloom
13. ☑ Sun-shaded smoke billboards
14. ☑ Heat shimmer over a fireball
15. ☑ Scorch decals at a hit
16. ☑ A small light in every burning fire puff, over an Enhanced light budget raised past 16

### Wave C, lit clouds

21. ☑ Lit cloud cards: sun tint, transmission rim, shadowed undersides, soft depth fade
22. ☑ FogVolume banks from the authored fvol slabs
23. ☑ A rendered puff sprite set under Enhanced, picked by montage

### Wave E, the feel of speed

41. ☑ Wind streaks past the camera, keyed to speed and G, over the authored speed cue
42. ☑ The chase camera lags the nose through a roll and widens its FOV with speed

### Wave D, the net and the reading

31. ☑ An enhanced golden set under `--det`
32. ☑ The perf reading over the finished stack, within 20% of the D31 baseline

### Verdicts at the controls, Waves A to C

| Item | Verdict | Follow-up |
|---|---|---|
| A1 | Edges smooth, no ghost trails on sprites | The sun's and moon's glint on water read too bright, the moon's most: the sun's `LightSpecular` is 0.35 by day and 0.1 at night under Enhanced (`WeatherRig.EnhancedSunSpecular`); the water material and its reflections are unchanged. The night value reads right at the controls over C5's sea, against 0.5 and 0.05 |
| A1 | The aircraft's own shadow showed only in a narrow band of camera distance and altitude | The sun's angular size is 1.0°, the first cascade 0.12 of the shadow distance, the directional shadow map 8192 (`Launcher.EnableSunShadows`); a plane 40 m up now shows its shadow from the flyby distance where it had none |
| A2 | 200% visibly sharpens the C5 skyline; its cost is the user's (decision 9) | none |
| A3 | The water bands are gone | none |
| A4 | The cockpit gauge faces are opaque with AA off | none |
| A6 | Every method looks good; the world's low detail keeps them close | The Deck crashes at four panes and 67% (A7) |
| B11 to B14, B16 | Pass | none |
| B15 | The marks read small and light | The radius factors are 1.1 and 0.455, the peak alpha 0.95 (`ScorchField`) |
| C21 | The medium strength | Ships: tint 0.10, rim 0.20, core shadow 0.45 (`FogVolumeClutter`) |
| C22 | C5's whiteout bank draws a hard horizontal edge and too prominent a skyline | The lit city takes no froxel fog, so the bank blackened only the dome behind it. An armed chapter builds no bank (`FogVolumeBanks.Create`); C5's street pose is now flat zone grey like the Original, and C1's banks are byte-identical |
| All | C5 performance is poor under every AA method | A7 |

## Dependency and parallelism notes

A1 lands before any look item is judged, since B and C are seen through the same temporal filter;
B and C do not wait for A's verdict (A3, A5). A1 and A2 both touch the four viewport construction
sites (`Launcher`, `CockpitOverlay`, `SpyglassView`, `SplitScreen`), so they run in sequence, not in
parallel worktrees; A4 touches shader code in `SceneBuilder`/`Clutter` and can run beside A2. A3
depends on A1's flight. A5 depends on A1 and A2 and is judged against them. A4 needs a tree forked
after the tree, rail and lattice families went from scissor to blend, since that change removes
most of A4's population.

B12 and B13 both edit `EmitterRenderer.cs`'s one shader: sequential. B11 touches `WorldLights.cs`
and the effect sink, B14 adds its own material, B15 touches the crater/decal path; those three can
run in parallel worktrees with that file ownership. B16 owns `WorldLights.cs` after B11 and reads
the fire columns B12 flags in `Puffer`, so it runs after both. C21 owns `FogVolumeClutter.cs`; C22 is a new
module and can run beside C21; C23 depends on C21's shader. E41 is a new `Effects` module and
E42 owns `CameraController.cs`; they run in parallel with each other and with Wave C, after A1.
E41 must not touch `SpeedCue.cs` or `Puffer.cs`, the faithful path's wisps. D31 and D32 depend on everything,
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
`Session/Launch/Launcher.cs` on the root viewport right after `GraphicsMode.Resolve` and its log line,
from `Flight/Hud/CockpitOverlay.cs`, `Flight/Camera/SpyglassView.cs` and `UI/Boards/SplitScreen.cs` at SubViewport
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

## A3 ☑ BL-803: the shadow-map bands on grazing surfaces under Enhanced

**Landed.** `BL-803` closed on main with the directional soft-shadow filter at its top rung
(`Launcher.EnhancedShadowFilterQuality`, SoftUltra), taken by the bisect route rather than the
flight after A1. The doors `--no-ssao`, `--no-ssr`, `--no-glow` and `--no-soft-shadows` landed in
`SessionSpec` and named the sun's penumbra filter: it alone resolves through a screen-space
pattern, over 81 % of the C1 waterfall frame, and the top rung removed 86 % of the excess while
holding the judged penumbra width. `analysis/screen-dither/FINDINGS.md` holds the instrument and
the numbers. This branch's own SoftHigh constant gave way to main's in the merge. The bands that
survived the top rung were the ground shadowing itself: Godot's soft filter has no receiver-plane
bias, so a flat sheet under a low sun reads its own neighbouring texels as nearer the light, in
bands at the texel pitch. The terrain and the water cast no sun shadow
(`WorldBuilder.IsShadowlessGround`), as in the original, and the rung stays SoftUltra: a 0.5
degree sun at SoftHigh bands the far water worse than the shipped pair.

**Branch evidence (kept for reference).** This branch reached the same mechanism independently.
The pattern is the sun's soft-shadow filter, not the bias pair, and not a cascade artefact: with
`sun.ShadowEnabled = false` it vanishes from lit ground and water, and it never appears on sky.
Godot filters a PCSS directional shadow at the project-wide
`directional_shadow/soft_shadow_filter_quality`, which ships at Godot's default (Soft Low, measured
bit-identical to the shipped build), and at a 25 degree sun that filter resolves the shadow map's
own self-occlusion into a fine screen-space lattice over every grazing lit surface rather than
dissolving it. The branch set the rung to SoftHigh inside the enhanced-only `EnableSunShadows`.

Measured headless at the user's own C3 pose and the same pose on C1, plus a C1 town
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
lever table are under `.scratch/eg2/A3/`. The branch's reading that Soft Ultra buys nothing over
Soft High once TAA is on did not survive: that measure never covered the far water.

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

## A4 ☑ Alpha-to-coverage on the cutout surfaces under Enhanced

**Landed.** Under Enhanced Graphics the cutout arm of every world-surface shader
(`SceneBuilder.GetBiasShader`, and the billboard and cylindrical facade shaders that share its
scissor) and of the clutter sprite shader (`Clutter.ShaderCode`) takes two new pieces, both held as
`SceneBuilder.CoverageMode` and `SceneBuilder.CoverageLines` so the two call sites cannot drift: the
render mode `alpha_to_coverage`, and the fragment pair `ALPHA_ANTIALIASING_EDGE = 0.5` with
`ALPHA_TEXTURE_COORDINATE` set from the albedo texel size. Both halves are required, and this is the
part the item's evidence did not have: the render mode alone changes no pixel at all, because Godot's
opaque pass writes alpha 1 unless the edge built-in is written, so a coverage mask fed alpha 1 is a
full mask. The edge sits at the scissor's own 0.5 threshold, which keeps the silhouette where the
faithful cut puts it and spends the samples on the ramp around it; an edge at 0.3 visibly fattens the
C5 vines and crane strands, which is the wrong picture. The plain mode ships rather than
`alpha_to_coverage_and_one` by measurement, below. Nothing is a new key bit: the Enhanced bit is
already in both shader keys, so this rides it and the faithful text is byte-identical, which is what
the goldens pin. The untextured cutout is excluded at the world-surface site, since the coverage arm
samples `albedo_tex` for its derivative. `Testing/WorldAndToolSuites.cs` gains `alpha-coverage-text`,
which builds the same world and the same clutter cards under both presentations and reads the shader
text off the built materials rather than off the generator.

**Verified.** The complete `RunTests.ps1` on the item worktree, exit 0: build PASS, units **4610
passed, 0 failed, 2 skipped of 4612**, engine **368 suites passed, 0 failed**, goldens **19 shots
hash-identical**. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and `CheckEncoding.ps1` clean. The
new suite's own counts read `enhanced: cutout shaders carrying the coverage arm=14 of 14`, `clutter
sprite shaders carrying it=1 of 1`, `blended shaders carrying any coverage token=0 of 9`, and the
faithful arm 0 of 14, 0 of 1 and 0 of 8. `alpha-cutout-ray-census` passes unmoved, as it must, since
it tests the collider rather than the pixel. The coverage is
measured, not asserted. At the C5 crane pose, Enhanced with coverage against Enhanced without changes
15150 pixels (mean |d| 12.2, max 396); at the C4 sign pose, 10093 pixels (mean |d| 32.0, max 427).
Over an 80x80 crop of one crane edge the mean ramp width across an edge falls from 3.86 px without
coverage to 3.60 px with, at the same 186 distinct luminance levels, and a row profile through a
sub-pixel strut turns a run of hard black pixels into partial coverage. `alpha_to_coverage_and_one`
was measured at the same pose and adds only 5361 pixels (mean |d| 4.3) where the plain mode adds
7033 (mean |d| 7.4), and its row profile matches the no-coverage control almost exactly, which is
the `_and_one` contract hardening the resolved alpha back toward the scissor's own step. The project
carries MSAA 4x (`anti_aliasing/quality/msaa_3d=2`) for both presentations, so the samples coverage
needs are there. The clutter fade arm changes nothing either way: the fade dither is a `discard`
rather than a partial alpha, and with the fade on against off the crops are identical at both poses,
so coverage stays on the clutter arm for consistency with the world surfaces. Judged with TAA on,
which is what Enhanced ships. The cost at the C5 crane pose over `--perf --frames=240` is 3.54 ms of
GPU time against 3.50 ms without coverage, about 0.05 ms. ⚠ Neither of the other two Enhanced doors
could be captured: a `graphics.renderScale=200` run and a `graphics.temporal=fsr2` run both come back
as a uniform dark frame through `--screenshot`, which predates this item and is not caused by it, so
what coverage does above native and under FSR 2.2 is unmeasured rather than claimed.

**Original approach (kept for reference).**

**Goal.** Alpha-scissor edges (clutter, fences, lattice geometry, decals with cutout masks) resolve
through the MSAA samples instead of a hard 1-bit edge, under Enhanced only.

**Evidence (confidence: direction-sound).** MSAA does nothing for an alpha-scissored edge; Godot's
`alpha_to_coverage` render mode hands the alpha to the coverage mask. `SceneBuilder.GetBiasShader`
and `Clutter.ShaderCode` build their shader keys with an Enhanced bit already (`SceneBuilder.cs:1528`,
`:1724`, `:1819`), so an Enhanced-only render mode is one more key bit. ⚠ The tree, rail and
lattice families (56 textures) blend rather than scissor, in `TextureArchive` and `Clutter` alike,
because the original never alpha-tests; they have left this item's population, and so have the
coastline sheets. A4 is not moot: 332 of the census's 388 scissored names are outside those
families (`analysis/alpha-classification/`), among them the crane girders (`crane05`,
`cranesteel3`), `rope_bridge`, `lightpole` and the sign cards.

**Approach.** Add `alpha_to_coverage` (or `alpha_to_coverage_and_one`) to the cutout arms of those
shaders when `GraphicsMode.Enhanced`, through the existing key bits so the faithful shader text is
byte-identical. Judge on a surface that still cuts, the C5 lamp posts and C4's crane, not on a
fence or a tree.

**Model recommendation.** medium.

**Verify.** Enhanced flight past a C5 lamp line and C4's crane; faithful goldens zero movers;
the alpha-cutout ray suites still pass (they test the collider, not the pixel, so they must not
move).

**⚠ Traps.** Alpha-to-coverage with TAA can double-soften an edge; judge A4 with A1 on. The
clutter fade dithers alpha on purpose; coverage on a dithered alpha reads as a different pattern,
so try it with and without the fade arm.

**Fix at the controls: the cockpit interior keeps the plain scissor.** In the cockpit view under
Enhanced, with the Anti-aliasing row on Off at 100, the gauge faces were see-through. The interior
draws into the transparent cockpit pass (`CockpitOverlay`), and coverage writes the resolved
sample fraction into that target's alpha, so the composite let the world through wherever a face's
alpha sat below 1. The interior's own `SceneBuilder` sets `NoAlphaCoverage` (`PlaneBuilder`), a
shader key bit of its own, and its cutouts keep the scissor; the world keeps coverage. Captured on
the hidden desktop with the user's saved options: every gauge face is opaque again, the world
outside unchanged.

**Fix at the controls: the edge is an offset, so it ships at 0.0.** On C3 at 5120x1440 the sky
dome's cloud puffs drew half transparent, some cut along a straight diagonal. Godot adds
`ALPHA_ANTIALIASING_EDGE` to `ALPHA_SCISSOR_THRESHOLD`, so the 0.5 above put the coverage edge at
1.0: an opaque texel took half coverage wherever its texture magnified (mip 0), and the two
triangles of a puff quad could fall either side of that. `CoverageLines` now writes 0.0, which is
the scissor's own 0.5, the faithful silhouette. The landed paragraph's 0.3 trial therefore sat at
an effective 0.8 and was judged against an eroded edge, not the faithful one; the vines and
strands now take the faithful cut, a look for the user at the controls. The faithful path emits
none of this. `alpha-coverage-text` now pins the value as well as the tokens.

## A5 ☑ FSR 2.2 tried once as the alternative temporal pass, kept or parked on the user's verdict

**Verdict.** Neither pass is deleted: the player chooses. The anti-aliasing method becomes a VIDEO
page row in both presentations, FSR 2.2 among its choices, and the `graphics.temporal` trial door
gives way to it. A6 builds the row.

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

## A6 ☑ An Anti-aliasing row on the VIDEO page, and a Render Scale that follows it down to 50%

**Landed.** `Utils/AntiAliasingSetting.cs` replaces `TemporalPassSetting.cs` and its
`graphics.temporal` key: the saved `antiAliasing` option, then the `graphics.antiAliasing` config key,
then the mode's default, with `SavedWord` holding the `--det` drop. `ViewportQuality.Apply` writes
the method and the scale as the approach below lays out, and writes nothing for Off at native.
Render Scale offers 50, 67, 77 and 100 to 200, and 50 to 100 under FSR 2.2; the resolvers and both
pages clamp a scale above 100 to 100 when FSR 2.2 is picked. The Original VIDEO page carries the row
on the authored but unused Lighting Quality line (`VP_T_LightTitle` / `VP_D_DLight` /
`VP_T_LightDESC`), the next line under Render Scale, whose list authors exactly five visible items;
the page does not grow. The built-in Options screen carries it as row 11. With nothing saved, the
row shows the default of the page's current graphics choice and follows a flip of it. The launch
line ends `anti_aliasing=<word> aa_source=<layer>`, with `clamped_by=fsr2` after the scale when
the clamp fired.

**Verified.** The complete `RunTests.ps1` on the plan worktree, exit 0: units **5688 passed, 0
failed, 3 skipped of 5691**, engine **442 suites passed, 0 failed**, goldens **19 shots
hash-identical**, which is the proof that Original with its defaults writes nothing on a viewport.
`display-render-scale` covers the precedence, the `--det` drop, the clamp and each method's viewport
write read back. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and `CheckEncoding.ps1` clean. **Owed
to the user:** each method's look at the controls, and on the Deck the `[perf]` figures at 100% and
67% under Enhanced, one pane and four, FSR 1 against FSR 2.2.

**Fix at the controls: the cockpit pass takes no method and no scale.** In the cockpit view the
world outside came out black under TAA, FSR 1 (any scale below 100) and FSR 2.2, and the gauge
backgrounds turned see-through under FXAA. The see-through gauges with the row on Off at 100 are
A4's, fixed there. The cockpit pass
(`CockpitOverlay`) is a `TransparentBg` SubViewport composited over the world, and Godot's temporal,
upscaling and screen-space resolves do not keep its alpha. `ViewportQuality.Apply` now returns
early on a transparent viewport, which keeps the project's MSAA 2x on the interior's edges; the
pass is small, so native resolution there costs little. `display-render-scale` checks that a
transparent viewport reads back unwritten under FXAA, SMAA, TAA at 67 and FSR 2.2 at 50. The E41
note that called the black world in its shots a capture artifact was this bug.

**Goal.** The player picks the anti-aliasing method on the VIDEO page of both presentations, and
Render Scale offers the scales that method can run at, down to 50% so a Steam Deck can run
Enhanced Graphics and splitscreen at a playable frame rate.

**Decisions (the user's).** One Anti-aliasing row with five choices: Off, FXAA, SMAA, TAA and
FSR 2.2. It is a display setting in both presentations, like Render Scale. Unset, it reads as the
mode's own default: Off under Original, which is today's image (the project's MSAA 2x and nothing
else), and TAA under Enhanced, which is A1's. MSAA stays fixed at `project.godot`'s 2x with no row
of its own; A4's coverage edges rely on it. Render Scale's choices follow the row: FSR 2.2 offers
50 to 100%, the other four offer 50 to 200%.

**Evidence (confidence: traced).** Godot 4.7 exposes, per viewport, `ScreenSpaceAA` (Disabled,
Fxaa, Smaa), `UseTaa`, and `Scaling3DMode` (Bilinear, Fsr, Fsr2) with `Scaling3DScale`; FSR 1 and
FSR 2.2 refuse a scale above 1.0 and bilinear is the only supersampler, and FSR 2.2 carries its own
temporal pass and replaces TAA. `ViewportQuality.Apply` is already the one write on all four 3D
viewports. The v0.2.0 release notes record Enhanced and splitscreen well below 60 fps on the Deck,
and the user's reading there puts the GPU above 90 percent busy with the CPU at 20 to 30 percent,
per-pixel cost that a lower render scale reduces directly.

**Approach.** An `AntiAliasingSetting` in `Utils` replaces `TemporalPassSetting`, layered the way
`RenderScaleSetting` is: the saved option, then a `graphics.antiAliasing` config key, then the
mode's default, with `SavedWord` holding the `--det` drop. `graphics.temporal` goes with the class.
`DisplayWords` gains the five words and Render Scale gains 50, 67 and 77 (FSR's own quality presets
at 1280x800 are 67 and 77). `ViewportQuality.Apply` writes: the screen-space filter for FXAA/SMAA,
`UseTaa` for TAA, and the scaling mode by scale and method (below native FSR 2.2 when that is the
method, else FSR 1; above native bilinear; at native nothing, or FSR 2.2 at 1.0 when that is the
method). The row goes on both presentations' VIDEO pages; picking FSR 2.2 while Render Scale sits
above 100% moves the scale to 100%, and `Resolve` clamps the same pair the same way.

**Model recommendation.** medium.

**Verify.** The complete battery with goldens hash-identical. A suite covers the precedence, the
`--det` drop, the scale clamp, and each method's viewport write read back. The menu suites count the
new row. On the Deck, `[perf]` at 100% and at 67% under Enhanced, one pane and four, FSR 1 against
FSR 2.2, and the user's look.

**⚠ Traps.** Nothing may be written on a viewport under Original with the defaults, or the
faithful `--det` goldens move. FSR 2.2's cost is roughly fixed per output pixel and a four-pane run
pays it once per pane, so a scale that helps at one pane can lose at four. The hand-billboarded
shaders (puffer, clouds, clutter) write no motion vectors; a lower input resolution leans harder
on them, so look for smear on smoke and clouds first. The spyglass SubViewport and the panes take
the same writes as the root viewport, or they disagree in sharpness (A2's trap); the transparent
cockpit pass takes none (see the fix above).

## A7 ◐ C5 and the last two campaign missions hold a frame budget under Enhanced

**Landed.** Seven changes, each taken from a profile on the author's machine:
- **Clutter cells, Enhanced only.** `Mech3/ClutterInstances.cs` cuts each clutter kind's MultiMesh
  into map cells about twice the kind's farthest fade across, each a node with a visibility range at
  that fade (`EffectsLevel.RegisteredScaleSq` carries the fade scale, since Godot reads a global back
  only in the editor). `KindExport.Instances` is the index map, so `ClutterActivation`, craters and
  the suites address a placement as before. C5 drew all 177,000 stamps in every view: 2.66 M
  primitives per pane. The faithful path keeps the one MultiMesh, since cells blend cards in
  another order.
- **Mission CPU.** `ObjectiveSites` lists a site's meshes once per resolved node and re-reads their
  live transforms, instead of walking a zeppelin's subtree per pane per frame; a part the site's
  own script moves still moves the marker. A turret narrows the structure pool to live, hostile, non-gasbag pools before the
  engine calls (`AimCandidateSet.AddTurretStructures`) and skips it when the aircraft-first rule
  already answered. An AI rig resolves no input bindings. `ObjectZoneGate` writes a mesh's layer
  only when its zone changes. An idle emitter layer stops re-publishing its count.
- **The four-pane crash.** Under Enhanced the spyglass picture renders at no less than 64 internal
  pixels (`SpyglassView.RenderSide`). At four panes on 1280x800 its 80 px disc fell to 54 at 67%,
  and Godot's ambient-occlusion depth chain, a quarter of that with five mips, failed to allocate.
  The faithful path runs no occlusion pass and keeps the disc's own size.

**Verified.** The complete `RunTests.ps1` on the item worktree, exit 0: units **5798 passed, 0
failed, 3 skipped of 5801**, engine **466 suites passed, 0 failed** (the new `clutter-cells` and
the spyglass floor in `display-render-scale` among them), goldens **19 shots hash-identical**.
`CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and `CheckEncoding.ps1` clean. Paired `[perf]` runs
on the author's machine (RTX 5080, 7800X3D), before the change and after it, `--det`, CM23 and CM24
with their rosters over a `--profiles=` store and their intro skipped (PERF-38):

| Run | frame_ms | proc_ms | gpu_ms | draws | prims |
|---|---|---|---|---|---|
| CM24, 1 pane, 1280x800 | 14.4 to 18.0 → 10.5 | 8.2 to 10.4 → 4.4 | 5.0 → 2.7 | 4,833 → 4,356 | 5.59 M → 0.62 M |
| CM23, 1 pane, 1280x800 | 9.8 → 8.7 | 6.2 → 3.5 | 3.9 → 1.5 | 1,854 → 1,644 | 2.67 M → 0.25 M |
| C5 flight, 1 pane, 1280x800 | capped at 8.3 | 1.7 → 1.7 | 4.8 → 2.2 | 956 → 823 | 2.66 M → 0.17 M |
| C5 flight, 4 panes, 5120x1440 | 38.5 → 20.2 | 3.7 → 3.5 | | 12,934 → 11,041 | 22.9 M → 1.4 M |
| CM24, 4 panes, 5120x1440 | 46.3 → 29.5 to 34.7 | 15.3 → 6.2 to 7.0 | | 27,258 → 21,770 to 25,216 | 25.6 M → 2.6 to 3.1 M |
| CM24 faithful, 1 pane | 13.8 → 9.7 | 8.8 → 4.3 | 2.5 → 2.5 | 3,132 → 3,132 | unchanged |

Enhanced C5 shots at a freecam pose and in flight differ from the unchunked build on 4 and 49
pixels by one level. The four-pane C5 run at 67% and at 50%, under TAA and under FSR 2.2, logs no
mipmap error where the unfixed build logged four. **Owed:** every Deck figure, since the Deck did
not answer ssh; the four-pane 60 fps target on the author's machine, which the sun's soft shadow
stands in the way of on the GPU (four-pane CM24, all panes' GPU time: 19.5 ms with Soft Ultra, 17.3
with Soft High, 12.9 with Soft Medium, 11.3 with the penumbra off, 8.9 with no sun shadow; the 8192
atlas against 4096 is 0.3 ms) and about 22,000 draw calls stand in the way of on the CPU (the
frame holds at 31.7 ms whichever shadow setting, PERF-39). The filter quality is the user's
judgement, since the comment on `EnhancedShadowFilterQuality` binds it while the sun is wider than
0.5°.

**Goal.** Under Enhanced, C5 and the campaign's last two missions, CM23 "The Criminal Exodus"
(`C5/M03`) and CM24 "Battle over Broadway" (`C5/M04`), run at 60 fps on the Steam Deck at 67% with
TAA, one pane, and at 60 fps on the author's machine at 100% with four panes. The faithful path's
frame cost does not rise and its goldens do not move.

**Decisions (the user's).** C5 performance is poor whatever the AA method. Measure GPU and CPU
(the per-frame script, AI and animation cost as well as the render passes) on C5 freeroam and on
CM23 and CM24 flown with their own rosters, and fix what dominates. Explore general wins while
there, including instancing: clutter already draws one MultiMesh per kind, and the world's placed
geometry does not. Fix the Deck crash at four panes and 67% as part of the item.

**Evidence (confidence: measured).** On the Deck (1280x800, C5 flight, the eg2 Linux export,
`[perf]` windows): Original 105 fps (GPU 8.5 ms); Enhanced at 100% 26 to 43 fps by AA method (GPU
27.6 to 45.7 ms); at 67% through FSR 1, 67 fps with TAA and 74 with AA off (GPU 13.4 to 14.8 ms);
FSR 2.2 at 67% 46 fps. C1 under Enhanced with TAA runs 80 fps at 100% and 118 at 67%. Four panes
at 100% run about 10 fps; four panes at 67% crash under both TAA and FSR 2.2 on Godot's "Too many
mipmaps requested for texture format and dimensions (5), maximum allowed: (4)", a render buffer at
a pane's roughly 430x270 internal size. On the author's machine C5 freecam at 200% gives 40 fps
with GPU 25 ms. ⚠ `script_ms` is TIME_PROCESS, the worst pass of each second, not a per-frame cost
(PERF-1); read `proc_ms`, `ai_ms` and `sim_ms`. ⚠ `gpu_ms` covers the main viewport only, so a
four-pane figure is read from `frame_ms`. Every placed world node is its own MeshInstance3D, which
is where a heavy pose's roughly 2,000 draw calls come from.

**Approach.** Profile first, per pass and per system, with a paired A/B for every change: the
Enhanced passes one at a time (`--no-ssao`, `--no-ssr`, `--no-glow`, `--no-soft-shadows`), the
omni pool (B16 raised it to 64), clutter and map-edge instancing, the draw-call count, and the CPU
phases. Then take the dominant costs in order, candidates being merging static world nodes that
share a material, instancing repeated placed models, cheaper settings per effects level, and CPU
hot paths the profile names. Fix the four-pane crash by keeping every render buffer above the size
its mip chain needs. Measure on the Deck over ssh (the `~/CSVM-eg2` export and `deckperf.sh` runner,
`XDG_DATA_HOME` redirected so the user's settings are untouched, a `config.json` beside the build
for config keys), and on the author's machine through `RunTests.ps1 -Perf` and `--perf` runs.

**Model recommendation.** high, delegated: it spans the render setup, the world build and the
simulation.

**Verify.** The `[perf]` figures before and after on the Deck and the author's machine at the poses
above; the four-pane 67% run completes; faithful goldens zero movers; the complete `RunTests.ps1`.

**⚠ Traps.** A merge or instancing change must keep every per-node behaviour the world relies on:
zone gating (`ZoneGate` layers), destructible visibility, animation-driven transforms, the
`node_bias` depth order and colliders. Perf windows taken while other probes run on the machine are
noise; repeat and pair them.

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
sine with a per-burst phase stride so a salvo does not pulse in lockstep. The `burst-light-envelope`
engine suite drives a live rocket into a plate and pins one light under Enhanced, none on the faithful
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
⚠ **The authored burst light draws on both presentations.** The world-effects runtime submits
`he_ground_effect`'s `he_light`/`he_light1` ramp (4 to 20 m at ignition, a 104 to 320 m plateau, 400 m
at its last frame) into the world's `WorldLights` through `AddSource`, so the faithful path is not
dark and the Enhanced path carries that ramp as an omni.

**Decision on merging main.** One Enhanced HE burst carried three lights at one point: the
authored `he_light` and `he_light1` plus this item's envelope. `torpedo_ground_effect` authors
`torp_light` the same way; `large_fireball` and `small_fireball` author none. The smallest change
that stops the doubling is a gate at the one decision point: `RegisterBurstLight` takes
`authorsOwnLight`, which the effect sink answered from `AnimRuntime.AuthorsLight` (any `LightState`
or `LightAnimation` event in the def), so the envelope lit only the two fireball defs and the
authored ramp was the HE and torpedo bursts' light on both presentations. Measured on the hidden desktop at `--fly --chapter=C1 --pos=-6104,300,-4420
--direction=0,-0.33,0.94 --fire-rockets --hold=0,0,0,0.3 --frames=90 --shots=12 --mute
--graphics=enhanced` (two `he_ground_effect` impacts before frame 90): ungated against gated,
about 9,400 to 10,000 pixels differ per frame, mean |d| 1.2 to 1.3 and at most 9 levels, 89 % of
them brighter ungated, centred on the bursts, and mean luminance 0.004 to 0.005 higher ungated; a
repeat of the gated run differs from it in zero pixels. C1's airfield holds the 16-light budget at
16 of 47 live throughout, so any slot the envelope took came out of a beacon's (89 % is frame 0's
share).

**Decision at the controls: the envelope replaces the authored ramp under Enhanced.** With the gate
in, a rocket burst under Enhanced showed no visible lighting; the authored ramp's wide, faint wash
does not read as a flash, and only the faithful path looked lit. The gate is gone: every burst def
registers the envelope under Enhanced, and `WorldEffectsFactory` hands the effects runtime
`LightReplacedAnimNames` (the same four names), so `LightChannel` still plays and tweens `he_light`,
`he_light1` and `torp_light` but never submits them. One light per burst again, on the envelope's
constants. The faithful path is unchanged: nothing replaced, the authored ramp committed. The
`burst-light` suite pins both (faithful: owner, `he_light`, `he_light1`; Enhanced: owner plus one
burst no wider than 180 m, the authored ramp still tracked in the snapshot). Owed at the controls:
the look against the faithful path's authored ramp.

**Decision at the controls: a shape per burst def, lifted and unattenuated.** The replacing light
still showed nothing on the ground. Enhanced world materials take the `worldLit` arm
(`SceneBuilder`), which has no per-vertex `csky_point_light` term, so only the omni lights the
terrain; an omni at ground level meets flat ground edge-on (N·L near 0), and `OmniAttenuation` 1.0
spends it within about 20 m. The burst now stands `WorldLights.BurstLift` (30 m) above the hit with
attenuation 0, so its whole range lights the ground at a usable angle. The user then asked for HE a
little stronger, the torpedo much stronger than HE, a redder colour than the authored
(1.0, 0.86, 0.29) that `he_light` and `torp_light` share, a light for the seeker's ground burst and a
bright, large, white-blue one for the flash rocket. `EffectCatalogue.BurstLightShapes` gives each
def a `WorldLights.BurstShape` (colour, peak gain, range, e-fold): HE and `large_fireball` 4.0 over
180 m in 0.29 s, `small_fireball` 2.5 over 120 m, the torpedo 9.0 over 280 m in 0.45 s,
`ballflare.flt` (the seeker's default row) 1.8 over 160 m, since four land together and 6 each
washed the whole view, and `flash_effect` 14.0 over 350 m in 0.22 s, replacing its authored white
light. The seeker and flash defs are no fireball, so `IsBurstLight` (the shimmer and scorch set)
leaves them out. The `burst-light-envelope` suite pins the two new shapes and the torpedo's lead
over HE; `burst-light` pins the lift. Captured on the hidden desktop: HE, seeker and flash light the
C1 tarmac under Enhanced; HE's range edge shows as a soft ring on flat ground. The torpedo was not
captured (the scripted pose pitches away before it arms) and is owed at the controls with the other
three.

**Decision at the controls: halfway back toward the authored colour.** The redder shapes read too
red in flight, so each warm burst colour sits midway between `he_light`'s (1.0, 0.86, 0.29) and the
redder pick: HE and both fireballs (1.0, 0.74, 0.265), the torpedo (1.0, 0.69, 0.245). The seeker
and flash colours are unchanged, and B16's fire light takes the same step, to (1.0, 0.68, 0.235).

**Decision at the controls: the authored lights take the authored reach.** The same falloff hid
every authored `LIGHT_STATE` light under Enhanced: a C1 airfield lamp authors a 7 m to 20 m linear
ramp, and `OmniAttenuation` 1.0 had it spent within a few metres. Every pooled omni now runs at
attenuation 0, and an authored light's omni range is `WorldLights.OmniRange`, the range at which
Godot's window, (1 - (d/r)^4)^2, is at half weight midway between the authored near and far range,
where the authored ramp is (18.4 m for the lamp). The omni's colour also takes the light's
ambient + diffuse scalar, which the shader's factor always carried. The faithful path is unchanged.
Captured at `--freecam --chapter=C1 --pos=-6620,175,-5690 --direction=0,-0.42,-0.91`: the parking
lot beside a lamp gains about 16 levels of red and the barracks wall 15 under Enhanced, where the
old falloff moved neither; grass beyond the lamps is unchanged. `world-lights-nearest-viewer` pins
the attenuation, the half-weight range and the scalar. The wing-tip lamps, the muzzle flashes and
the cockpit's twins of those flashes take the same rule, `WorldLights.OmniAttenuation` and
`WorldLights.OmniRange` over their authored pairs; `wing-flare-pose` and
`muzzle-light-first-person-point-term` pin it. A third-person muzzle flash keeps its rolled far
range, with the band's lower end as its near.

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
all: the varying and the `csky_sun_dir` read are absent as well, not neutralised.
`EnhancedSmokeGradient` is 0.2, a TUNE: on the C1 rocket plume one puff's sun-side to far-side
luminance ratio moves 1.046 to 1.061 for a largest pixel delta of 7 of 255, which reads as puffs
turned toward the light, while 0.45 (ratio 1.075, 12 levels) prints the same profile visibly on
every sprite in the stack, the lattice C21 warned about. A plume is a stack of overlapping
quads, so a per-card amplitude that looks gentle alone accumulates through the stack.
`EnhancedSmokeCeiling` 0.98 clamps inside the smoke branch alone, so a lifted `smoke101` (already
1.000 linear at its peak) cannot cross the 1.0 glow threshold B12 reserved for the fire flipbook,
and `magnesiumtip` and `poleflare` keep their unclamped 1.0. The sun-direction global the approach
left open is `csky_sun_dir`, declared once in `CSVM/shaders/csky_atmosphere.gdshaderinc` (main's
declaration, which the merge kept in place of C21's own include) and written by
`WeatherRig.WriteSunDirection` off the sun light's basis; this item reuses it unchanged.

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

## B15 ☑ Scorch decals at a hit

**Landed.** `Effects/ScorchField.cs` is the new module: a pool of Godot `Decal` nodes sharing one
procedural radial burn texture, built on first use from an `Image` with a soft radial falloff, a
ragged rim and mottled soot off an integer hash (no art asset, no seed to pin, the same bytes every
run). `Create` returns null unless `GraphicsMode.Enhanced`, so the faithful build holds no field, no
node and no generated texture. The hook is `Flight/Projectile.ScorchSink`, an `Action` beside the
existing `CraterSink`, raised in `Impact` after the outcome resolves and skipping both aircraft and
`SurfaceRegistry.Water`; `GameSession.RegisterScorch` is the one decision point and marks when the
hit carved a bowl or when its effect is one of `EffectCatalogue`'s fireballs, which is what gives a
crater-less rocket its mark. Size comes from the weapon's crater radius (0.85x when a bowl was cut,
0.35x for a burst that cut none), the box straddles the surface (1.5 m up, 7 m down, past the bowl's
own floor) and is oriented by the struck surface's normal, so a mark on a slope lies along the
ground. The mark holds full darkness for the first 55 percent of its 90 s life and fades out over
the rest; the field ages itself in `_Process` off `GameClock`, so it freezes with a halted clock.
The pool caps at 16, twice the eight-round stock rocket load, which is the most one aircraft can put
on the ground in a pass: the crater field keeps no count cap to copy, its own bound being the
no-overlap refusal, so the scorch borrows that refusal (a hit inside half a live mark's radius
refreshes that mark rather than stacking a second decal) and caps the node pool instead. Nothing
removes a CSVM crater, so a mark can never be orphaned by a refill; when the pool is full the oldest
mark is taken, which is the same order the field would fade them in. Every size, darkness and life
constant is TUNE. `Testing/CraterSuites.cs` gained the `scorch-decals` engine suite and
`analysis/engine-suite-weights.json` its weight; `docs/architecture/Effects.md`, the
`docs/architecture.md` index and `docs/org/craters.md` carry the entry.

**Verified.** On the item worktree: `dotnet build` warning-free, the complete `RunTests.ps1` PASS
(units 4574 passed 0 failed 2 skipped of 4576, engine 355 suites passed 0 failed with engine errors
clean, goldens **19 shots hash-identical, zero movers**), `CheckCommentCaps.ps1` /
`CheckDocEntries.ps1` / `CheckEncoding.ps1` clean. The
`scorch-decals` suite covers a rocket hit on tarmac and on grass under Enhanced (one mark each,
radius from the crater radius), the fade to hidden past the life, the cap holding at 16 and
recycling the oldest, water placing none and the faithful mode placing no field at all, with the
mode restored in a `finally`. Headless captures on the hidden desktop, three rocket hits on C1
tarmac and one on grass, faithful against enhanced, are in the item's scratch folder with a control
capture of the same deterministic frame holding the field back: the field changes nothing in the
frame but the three marks, and at the mark's core the ground reads 12 percent darker than the same
ground without it (10 to 20 percent across the marks measured). Two darkness steps either side of
the shipped value are captured beside it (7.7 percent at half alpha, 17.2 percent at full).
Not verified here: the look at the controls, which is the user's, and with it the size and darkness
pick; the montage is a grazing chase view at 150 m and the mark reads subtle there.

⚠ A decal renders on the enhanced world's terrain and tarmac (Forward+, albedo modulate through
the cluster), and Godot's clustered fragment shader applies the same mix under `MODE_UNSHADED`, so
a fullbright material left under Enhanced would take the mark too. Reading the mark on a dark
screenshot is deceptive: a bright-coloured probe texture and a scorch-off control frame are what
proved the path, not the eye.

**Original approach (kept for reference).**

**Goal.** A rocket or bomb hit on ground leaves a dark scorch under Enhanced; the faithful path
leaves the ground untouched, as the original does.

**Evidence (confidence: lead-only).** `docs/org/craters.md`: the original never carves a crater in
play, since the carve gates on the struck node's `can_modify` flag and no shipped node carries it;
no scorch texture exists in the data. Godot's `Decal` node projects onto the lit world; on the
faithful fullbright world it would not read.

**Approach.** Under Enhanced, at the impact outcome (or at the crater build in
`CraterField`/`TerrainCarve`, should an enhanced option re-enable the carve), place a `Decal` with a procedural radial scorch texture
(TUNE size from the weapon's crater radius, TUNE fade), pooled and recycled with the same count
cap the crater field keeps.

**Model recommendation.** medium.

**Verify.** `--chapter=C1 --fire` at the controls, three rocket hits on tarmac and one on grass;
decals sit on the surface and fade at the cap. Faithful goldens zero movers.

**⚠ Traps.** Decals on water read wrong; skip the water surfaces `ClassifySurface` names.
Alpha-to-coverage (A4) does not apply to decals.

## B16 ☑ A small light in every burning fire puff, over an Enhanced light budget raised past 16

**Landed.** `WorldLights` splits its two budgets. The data texture keeps `MaxActive` 16 rows, and
under Enhanced the omni pool lights up to `OmniBudget` for `graphics.effectsLevel` (64 at high, 48
at medium, 32 at low, every one TUNE). One significance sort orders both: the texture takes its
head and the omnis a longer run of the same order. `WorldLights.AddFire` is one burning emitter's
light: a warm orange (TUNE sRGB 1.0, 0.68, 0.235, see B11's colour decision) at gain 1.5 scaled down while fewer than four fire
particles live, with the B11 flicker (now one `Flicker` both lights share). Its reach is 2.5 times
the fire particles' mean grown size, clamped to 8 to 45 m, and it stands a quarter of its reach
above their centroid so the ground takes it at an angle. Its significance carries a 0.25 rank
weight, so at equal significance a fire loses to an authored or burst light. A `Puffer` built under
Enhanced keeps the fire columns `Create` already resolves; the integrate loop counts the live
particles on a fire column before the distance gate (an undrawn fire still lights its surroundings)
and publishes their count, centroid and mean size. `EffectAmbience` holds the emitters that are
burning, told only on a start or a stop, and its `SubmitFires` is the `WorldLights` source that
`GameSession` registers beside the world's lights; a freed emitter drops out there, and `Still`
ignores the call so a lab's emitters burn without a world. The faithful path builds no fire column
array, so its emitters do no fire work and its data texture is what it was.

**Verified.** The complete `RunTests.ps1` PASS (units 5688 passed 0 failed 3 skipped, engine 443
suites, goldens 19 hash-identical, zero movers). The `world-lights-enhanced-budget` suite pins the
budget per level, the faithful path at 16 rows with no omni and no fire, 32 omnis of 40 live under
a budget of 32, a beacon first against 80 equally near fires (and a control where 80 brighter
authored lights evict it), a real emitter's one light above its fire centroid dropped while its
smoke lives on, and a faithful emitter registering nothing. A forced crash 5 m above C1's airfield
at frame 150, Enhanced with the fire source off against on: the grass beside the wreck gains about
27 levels of red and 3 of green, and grass farther out does not move. A crash in the air lights
nothing, since its fires burn beyond their reach of the ground. Not verified here: the look at the
controls, which is the user's, and the Deck `[perf]` reading per effects level.

**Goal.** Under Enhanced every explosion keeps lighting its surroundings after the flash: each
burning puffer that sequences the fire flipbook carries a small flickering light while its fire
frames are live, so a wreck fire, a burning building or a fireball's trail lights the ground and
walls around it. The light budget grows under Enhanced so these lights do not evict the authored
beacons or the burst lights.

**Decisions (the user's).** Extend the lighting to every explosion and put smaller lights in the
fire puffs; raise the light budget together with it.

**Evidence (confidence: traced).** `WorldLights.MaxActive` (16) is the project's own bound, not a
Godot one: it is the row count of the data texture Original's per-vertex `csky_point_light` loop
reads, a per-vertex cost on every `lighting: true` model. Forward+ clusters its lights (512
elements per view by default), so the Enhanced omni pool has no engine limit near 16. C1's
airfield alone holds 16 of 47 live lights. B12 already flags the fire columns per puffer
(`MultiMeshEmitterRenderer.IsFireSprite`, `fire_f01`…`fire_f06`, resolved in `Puffer.Create`).
The omnis carry no inverse-distance term and a range matched to the authored ramp (B11), so a
small range reads as a pool of light rather than a point.

**Approach.** Split the two budgets: the data texture keeps `MaxActive` 16, so Original's shader
and its goldens do not move, and the omni pool takes its own `EnhancedMaxActive` from the graphics
`EffectsLevel` (TUNE, for example 32 at low and 64 at high). The significance rank decides both
sets from one sort. A puffer whose live particles include a fire column registers one pooled light
per emitter, not per particle, at the emitter's live centroid, with a TUNE warm colour, a small
range and the B11 flicker; it is dropped the frame the emitter's last fire particle ends. Lights
rank below the authored lights and the burst lights at equal significance, so a crowd of fires
never takes a beacon's slot.

**Model recommendation.** high: it changes the budget both presentations share and adds a
per-frame source from the puffer path.

**Verify.** Faithful goldens zero movers (the data texture is untouched). A suite pins the split:
Original commits at most 16, Enhanced mirrors more than 16 when more are live, a fire puffer
registers one light and drops it with its last fire frame, and a beacon survives a crowd of fires.
Captured at C5 night beside a burning wreck; then the user at the controls, and on
the Deck the `[perf]` reading with a salvo into a city block at each effects level.

**⚠ Traps.** Each omni is shaded per pixel over its whole screen footprint; the Deck is already
GPU-bound under Enhanced, so the budget must follow the effects level, and a close fire filling the
screen costs more than a far one. A per-particle light multiplies the count by the particle count;
keep one per emitter. The cockpit pass is its own `World3D`, so world omnis do not reach the
interior.

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
The sun direction arrives as the global shader uniform `csky_sun_dir`, declared once in
`CSVM/shaders/csky_atmosphere.gdshaderinc`, registered in `Launcher` beside `csky_world_light` in
both graphics modes, and written by `WeatherRig.WriteSunDirection` at lighting setup and on every
zone apply. B13 reuses it unchanged.

**Merged with main's lit cards.** Main made the field lit: a `lighting: true` card takes the
original's per-vertex `AMBIENT + DIFFUSE x max(N.L, 0)` through `CardNormal`, and main declared
`csky_sun_dir` itself, so C21's own `csky_sun.gdshaderinc` is deleted and one declaration stands.
`ShaderCode(lit, fogged, enhanced)` emits main's faithful and lit text byte for byte when
`enhanced` is false; under Enhanced the grade reads `graded` in place of `col.rgb`, over the colour
the lit arm has already shaded, so the grade layers over either variant.

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
64 x 64 x 64 over a 1,024 m length. ⚠ Geometry past that length is not outside the pass: it reads
the last froxel slice, so the horizon dome (authored `fog: false`, Godot's default fog path) takes
the whole bank. C1's look depends on that; exempting the dome takes C1's deck pose from mean 127
to 157.

**No bank where the whiteout is armed (C5).** The lit world arm writes its own `FOG`, which takes
every lit world surface out of Godot's volumetric fog. A bank over C5's city therefore reached only
the dome and the sky behind it, and the chapter's near-black `[16,16,16]` whiteout albedo turned
them to 0 while the city kept its zone fog at 18. The result was a hard horizontal edge (the far
outline of the zone-3 ground meshes, which `--no-zone-cull` moves to the horizon) and a skyline of
fogged lit buildings standing out against the black. `Create` returns nothing for an armed chapter,
so C5 runs without the froxel pass and its frame at the street pose is byte-identical to one with no
banks; C1's deck pose is byte-identical before and after.

**Boxes, not the volume's own mesh, and tiled.** Godot 4 has no mesh-shaped fog volume at all
(`FogVolumeShape` is Ellipsoid, Cone, Cylinder, Box or World), so the shape is a box over the
volume's bounds: exact for C1/C2B/C4's axis-aligned slab pieces and for C5's street prisms as boxes,
an over-estimate for C1C's twelve tapering build-up frusta. ⚠ **Measured on this engine: a
`FogVolume` box much wider than about 2,048 m contributes nothing to the froxel pass, silently and
with no error.** At C1's authored 8,192 m slab pieces the frame is pixel-identical to one with no
banks at all; at 2,048 m the bank renders whether or not the camera stands over it. So each volume's
bounds go down as a grid of tiles no wider than that, seamlessly because the edge fade is 0: C1's
nine pieces become 36 boxes, C5's seventeen prisms 57.

**Density.** There is no authored statement about the inside of a cloud, so one TUNE stands,
shipped at **0.002 per metre**: a climb straight through the 120 m deck scatters out about a fifth of what is behind it and
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
billboards that cast no shadow, so there is nothing to break the light into beams. C5 builds no
bank, for the reason above.

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

## C23 ☑ A rendered puff sprite set under Enhanced, picked by montage

**Landed.** Under Enhanced every cloud puff card draws a Blender-rendered sprite. The shared module
`Mech3/CloudPuffs.cs` and `shaders/csky_cloud_puffs.gdshaderinc` hold two pools as mipmapped
texture arrays, rendered headless by `CSVM/data/cloud_puffs/render_cloud_puffs.py`:
- The fvol deck cards draw the eight-puff veil set: translucent, lightly shaded, with strongly
  varied outlines.
- The placed `cloudparent` sprites (C1, C1B, C1C, C4, C2, C2B) draw the six-puff far set: denser
  heaps with the authored masks' soft fringe.

The user picked the veil for the deck over two opaque sets, asked for more than two textures and
less order, and after flying both picked the soft-fringed far set over a firmer detailed one
(`--set far_detail`).

Each card keys its puff, a turn of up to 20°, a mirror and a size of 0.8 to 1.25 on a hash of its own
position, never on the shared cloud Rng stream, so the placements are unchanged. The puff carries
the authored mask's colour and peak opacity as a tint, which keeps C5's dark haze dark.

⚠ Most shipped cloud cards map their image a quarter turn and flipped along the diagonal (C1 108 of
109, C4 all 274, both C5 deck templates). The shapeless authored masks hide that, but a rendered
heap drew as a sideways pill. So a pooled card samples through its own upright card UV
(`csky_puff_card_uv`), and the authored UV still drives every card that draws its mask.

The faithful shader text is unchanged, and the `cloud-puffs` suite pins both paths.

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
authored wisps, which are untouched. It ships off: the user's pick is that `graphics.windStreaks`
in `config.json` turns it on. It follows `Precipitation`: one MultiMesh of 1400 thin quads
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
should dim the wisps, which ships as no dimming. Those two shots carry `--no-cockpit-pass`.

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
Streaks through the cockpit glass read wrong at the near plane: keep the near fade. Do not edit
the wisps' opacity from here. Split screen needs the field per pane, as the
speed cue's `decorate` hook does.

## E42 ☑ The chase camera lags the nose through a roll and widens its FOV with speed

**Landed.** Under Enhanced the chase camera's pose is built from a lagged copy of the aircraft's
attitude, so a roll or a yaw leaves it trailing the nose before it springs back, and the external
FOV widens with speed. The whole arm enters `Flight/Camera/CameraController.cs` through one new call,
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

## D31 ☑ An enhanced golden set under `--det`

**Landed.** Five `--graphics=enhanced` shots join `analysis/goldens/manifest.json`, pinned with the
whole stack on and nothing turned off for stability: TAA, SSAO, the screen-space reflection, glow,
the volumetric banks and the Ultra shadow rung `--det` forces. `c1-lake-enhanced` is a freecam over
the waterfall lake (the blurred water reflection, conifer shadows on shadowless terrain).
`c5-city-night-enhanced` is the faithful `c5-city-night` pose under Enhanced (the lit facades and
the authored lights on the omni pool, no bank). `c1-cloud-deck-enhanced` is the faithful
`c1-cloud-field` pose (the rendered puffs, the lit card shading, the banks under the cards).
`c1-rocket-hit-enhanced` is B11's rocket pose at frame 90, two HE bursts on the dirt beside the
airfield with the burst light, bloom, smoke, shimmer and scorch pools live. `c1-cockpit-enhanced` is
the faithful `c1-cockpit` flight under Enhanced, the transparent cockpit pass on MSAA over a TAA
world. The C5 pose stands well outside the whiteout: C5 builds no Enhanced bank, the whiteout
overlay is the same on both presentations, and a pose inside a street volume renders a flat grey
16 to 18 that pins little. `analysis/goldens/README.md` names the set and its frame sensitivity,
and `RunTests.ps1 -Graphics`'s help says the goldens carry their own flag. Three comments that
called every golden faithful now say "the faithful goldens".

**Verified.** Each shot rendered one hash over five `RunProbe.ps1` launches on the default render
thread and the same hash over two more under `--render-thread safe`, so TAA's jitter is
deterministic under `--det` here and no fix was needed. Frame N against N+1: lake 23.53 %, C5
24.69 %, deck 5.77 %, rocket hit 94.08 %, cockpit 41.10 %. The freecam figures are the temporal
pass's own jitter: with TAA forced off in a throwaway build, the C5 pose moves 0.01 % and the lake
1.04 % (its falls scrolling), so each hash pins the jitter phase, and that phase repeats run to
run. The complete `RunTests.ps1` PASS: build, units **5973 passed, 0 failed, 3 skipped of 5976**,
engine **493 passed, 0 failed**, engine errors clean, goldens **24 shots hash-identical** at 4
workers; the goldens stage alone again on the same tree, **24 hash-identical**. The 19 faithful
hashes are unchanged, and the faithful `c5-city-night` rendered through the same probe script
matched its pinned hash. `CheckGoldenProse.ps1` clean.

**Original approach (kept for reference).**

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

## D32 ☑ The perf reading over the finished stack, within 20% of the D31 baseline

**Landed.** The finished stack costs +8.2% on C4, +13.3% on C5 and +2.0% on C3 at four panes over
the baseline, so all three budget cells sit inside the 20% envelope and no lever moves. The reading
is paired rather than sequential: each cell alternates a baseline launch and a stack launch, one
after the other, so a drift in the ambient GPU load lands on both arms in the same proportion
instead of settling on whichever arm ran during the busy stretch. Every launch waits for an empty
Godot process table first and records the ambient GPU utilisation sampled just before it, one warm
launch per arm is discarded because a tree's first launch compiles its shaders and builds its import
cache, each run drops its first perf window for the same reason, and the run's figure is the median
of the windows that remain. The baseline arm is a detached worktree pinned at the pre-Wave-A commit,
not the main checkout, because the main checkout moved twenty-eight commits forward mid-measurement
and took the baseline draw counts with it. Both arms run under `--det`, which drops config
overrides, so render scale is 1.0 by construction (`config=defaults dropped_overrides=0`,
`render_scale=100% source=default`) rather than by a saved option anyone could have changed.

Baseline, the pre-Wave-A tree, enhanced, three runs per cell:

| Cell | Panes | frame_ms | gpu_ms | render_cpu_ms | draws |
|---|---|---|---|---|---|
| C4 | 4 | 14.09 | 0.72 | 1.21 | 12798 |
| C5 | 4 | 18.41 | 2.01 | 1.31 | 15489 |
| C3 | 4 | 12.80 | 0.58 | 0.44 | 10016 |
| C4 | 1 | 8.33 | 0.89 | 0.94 | 1400 |
| C5 | 1 | 8.33 | 2.54 | 1.57 | 1944 |
| C3 | 1 | 8.33 | 0.56 | 1.20 | 1630 |

The finished stack, same cells, same three runs:

| Cell | Panes | frame_ms | gpu_ms | render_cpu_ms | draws |
|---|---|---|---|---|---|
| C4 | 4 | 15.24 | 0.81 | 1.32 | 12856 |
| C5 | 4 | 20.86 | 2.31 | 1.40 | 15910 |
| C3 | 4 | 13.06 | 0.67 | 0.45 | 10254 |
| C4 | 1 | 8.33 | 1.56 | 1.00 | 1415 |
| C5 | 1 | 8.35 | 4.05 | 1.78 | 2050 |
| C3 | 1 | 8.33 | 0.76 | 1.34 | 1647 |

The deltas, and what the envelope allows:

| Cell | Panes | frame delta | frame % | 20% budget | gpu delta | draw delta |
|---|---|---|---|---|---|---|
| C4 | 4 | +1.15 ms | +8.2% | +2.82 ms | +0.09 ms | +58 |
| C5 | 4 | +2.45 ms | +13.3% | +3.68 ms | +0.30 ms | +421 |
| C3 | 4 | +0.26 ms | +2.0% | +2.56 ms | +0.09 ms | +238 |
| C4 | 1 | pinned | pinned | outside the budget | +0.67 ms | +15 |
| C5 | 1 | pinned | pinned | outside the budget | +1.51 ms | +106 |
| C3 | 1 | pinned | pinned | outside the budget | +0.20 ms | +17 |

The one-pane rows carry no frame delta because both arms sit on this machine's 8.33 ms external
pacing, which survives `--no-vsync`; `gpu_ms` is the readable term there. It is also a per-viewport
figure rather than the whole splitscreen bill, which is why the one-pane gpu gap is the larger of
the two on every cell: at four panes `gpu_ms` reports roughly one pane's share while `frame_ms`
reports the whole frame.

The runner's own perf stage, one pane on each tree, read on `render_cpu_ms`, `gpu_ms` and `draws`
for the same reason, both arms 6/6 PASS:

| Scenario | base render_cpu / gpu / draws | stack render_cpu / gpu / draws |
|---|---|---|
| empty-stage | 0.27 / 0.33 / 216 | 0.28 / 0.47 / 216 |
| c1-flight | 0.77 / 0.52 / 1077 | 0.80 / 0.82 / 1128 |
| c2b-water | 0.44 / 0.38 / 135 | 0.60 / 0.57 / 135 |
| c4-terrain | 0.79 / 0.66 / 1071 | 0.88 / 1.03 / 1071 |
| c2m02-hollywood | 0.97 / 0.58 / 1299 | 1.14 / 0.74 / 1531 |
| c5-city | 1.44 / 2.32 / 1865 | 1.66 / 3.50 / 1865 |

A four-pane run builds the per-pane work per pane, not once. `SplitScreen` calls the same
`ViewportQuality.Apply` on each SubViewport, so TAA runs four times with four independent history
buffers, and the wind streaks report four boxes of 1400 instances against the one-pane run's single
box. Each box is still one MultiMesh, so the streaks cost four draws rather than four times the
per-pane price, and the four-pane draw deltas above (+58 on C4, +421 on C5, +238 on C3) are what
four panes of new work amounts to in the draw list.

The sum of the per-item deltas the landing commits recorded does not reconcile with the measured
total, and the reason is not one outlier item. Only four items recorded a figure at all: A3 at
+0.77 ms gpu at 1280x720, C22 at +0.07 ms gpu, B14 within 0.03 ms, and A2 at zero at scale 1.0,
with A5's FSR door off by default. A1, B11, B12, B13, B15, C21, E41 and E42 recorded none. The
recorded figures add to roughly +0.9 ms of gpu at one pane on one chapter each, which cannot be
compared against a four-pane frame delta in the first place; the eight items that recorded no
number are the whole of the gap, so the reconciliation is unavailable rather than contradicted.

Render scale above 1.0 is the user's own spend by decision 9 and is recorded outside the budget.
C5 at four panes, run under `--no-det` with the render scale option at 200%: frame_ms 30.65 to
31.09 and gpu_ms 3.56 to 3.67, against 24.02 to 24.49 and 2.69 to 2.73 for the same launch at 100%.
That is about +6.5 ms of frame and +0.9 ms of gpu, roughly +27% and +34%, which the envelope would
not hold and does not have to.

**Verified.** The three budget cells are inside the envelope with the worst at +13.3%, so no lever
is pulled and no file under `CSVM/src` changed for this item. `RunTests.ps1 -Hitch -Graphics
enhanced` on the clean stack tree is silent: zero hitch lines on the clean run, one line at
frame_ms 60.88 on the injected control, 28.1 s against the 30 s budget. The perf stage passes 6/6
on both trees. The raw logs, the paired driver and the commands are under `.scratch/eg2/D32/`.
⚠ The machine was not quiet for the whole item: another session's six-shard engine suite, a third
session's probe and a foreground game on the same GPU all overlapped earlier attempts, and the
first baseline arm drifted under the measurement. Every figure quoted above was re-taken afterwards
through the paired driver with the quiet gate armed and ambient GPU at 0 to 1 percent. The C5
one-pane cell is the one exception to three runs per cell: its third baseline launch hung and was
killed at the 600 s timeout, the same failure A1 recorded for an enhanced chapter `--screenshot`
probe, so that cell stands on two rounds whose windows agree to 0.01 ms.

**Original approach (kept for reference).**

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
