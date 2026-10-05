# VR support through OpenXR

**ACTIVE PLAN** (written 2026-10-05). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan adds seated PC VR to the Windows build through Godot's OpenXR interface. Flight, the
in-flight HUD and cutscenes become stereo; everything that is authored 2D (the 800x600 boards, menus,
briefings, MPEG cinemas) is shown on a floating virtual screen in the headset rather than rebuilt.
VR is one local seat in every play mode, entered through an Options toggle that relaunches the
executable with OpenXR on, and it ships in the next minor release labelled experimental. Out of
scope: standalone Quest (an Android port with a different renderer and on-device extraction), Linux
VR (Monado/SteamVR on Linux, a second runtime matrix nobody here can test), flying with motion
controllers (a grabbed virtual stick and throttle), diegetic 3D menus, asymmetric couch play, and
VR in the dev modes.

No item is drawn from `backlog.md` or a `backlog` GitHub issue: the VR requests reached the author
outside the tracker, and a search of the issues for VR/OpenXR/headset found none.
<TODO: link or quote the player requests if a record of them should live with the plan.> The plan
rests on a grilling held 2026-10-05 (the Decisions table) and a read-only survey of the code taken the
same day (What the code ships). Wave A is a gate: every later wave assumes its spikes succeed, and
A3 can redirect the whole plan to a custom engine build.

## Milestone goal

- A player with a PC VR headset turns on "VR mode" in Options, the game relaunches into the headset,
  and every flight session (campaign, Instant Action, stunt race, network play) is flown in stereo
  from the 3D cockpit, with Nose and a rigid Chase view available.
- Menus, boards, briefings and cinemas appear on a virtual screen operated by a controller laser
  pointer or the pad; campaign cutscenes play on that screen as a stereo 3D-cinema pair.
- The HUD reads in the headset: flat chrome on a cockpit-locked panel, world-tied markers at their
  real depth.
- Near-eye geometry (cockpit, HUD panel, chase plane) is free of float32 shimmer at 20 km from the
  chapter origin.
- 90 Hz on the reference rig, comfort basics (recenter, fades, optional vignette, render scale), and
  a headless `--xr-sim` stand-in that keeps the VR path inside the automated battery.
- No headset, no runtime, or the option off: the game is exactly the flat game it is today, goldens
  included.

**VR never changes the flat game.** OpenXR stays off in `project.godot`, every VR branch is gated on
the VR session being live (or `--xr-sim`), and the existing goldens must not move. A change that
needs the flat path altered to make VR work is its own item, argued on its own merits; the one such
change in this plan is the Enhanced flare (Decision 16, C14), which waits on the author's montage
judgement.

## Decisions (2026-10-05)

| # | Question | Decision |
|---|---|---|
| 1 | Platform | **PC VR, Windows only**, OpenXR in the existing Forward+ build; reference rig is the author's Quest over Steam Link/SteamVR and over Virtual Desktop (VDXR), RTX 5080. Linux VR and standalone Quest are out. |
| 2 | How much is stereo | **Flight, the in-flight HUD and cutscenes in stereo; menus, boards, briefings and cinemas on a floating virtual screen** (a SubViewport on a quad), so there is one UI codebase and no menu redesign. |
| 3 | Views and head look | **Cockpit is the VR default, Nose stays, Chase is offered rigidly attached to the plane** (no spring lag, no speed FOV, no swing). Head tracking replaces snap, free and padlock head look; padlock may return later as an optional aid. |
| 4 | float32 precision near the eye | **Spike a render-only floating origin first; if it cannot reach sub-pixel per eye without touching sim or physics, fall back to a double-precision Godot build** (editor, export templates, .NET glue, WebRTC extension). The stock engine is kept if possible. |
| 5 | HUD | **Split: flat chrome (gauges, compass, messages, chat, scores) on a cockpit-locked quad 1.5-2 m ahead and slightly low; world-tied markers (target boxes, impact reticle, lead) as 3D billboards at real depth; edge markers as a head-relative arrow ring.** |
| 6 | Motion controllers | **Laser pointer on the virtual screen plus a few actions (recenter, pause, view cycle) through a new `DeviceKind.Xr`** in the binding seam, rebindable like any device. Flying stays on pad, stick or keyboard; a virtual stick is a follow-up. |
| 7 | Play modes | **One local seat, all modes** (campaign, Instant Action, stunt race, network play). With VR on, local splitscreen is refused. |
| 8 | Cutscenes | **Theater screen, rendered as a 3D-cinema stereo pair**: two cameras on the authored camera, IPD apart, each eye's image picked on the screen quad by `VIEW_INDEX`. Framing and letterbox stay as authored; head motion changes nothing inside the frame. The author's own phrase: "so it is like looking through a window". |
| 9 | Activation | **One build, OpenXR off in `project.godot`; an Options toggle saves the choice and relaunches with `--xr-mode on`**, and the release ships a VR shortcut with the flag. No runtime or headset falls back to flat with a clear message. |
| 10 | Performance and screen-space effects | **90 Hz floor on the 5080 at the runtime's 1.0 resolution with Enhanced on, using at most half the 11 ms frame** (headroom as a proxy for mid-range GPUs). Soft particles, the fog-volume fade and heat shimmer are ported to multiview; the clutter dither stays if it reads fine per eye. The separate render thread is part of the spike. A VR render scale and the Original look remain the lower-end levers. |
| 11 | Verification | **A `--xr-sim` stand-in interface with a pinned head pose** puts the VR path into suites and new goldens on the hidden desktop under `--det`. Comfort, feel, controller binding and runtime quirks are the author's at-the-headset playtests. |
| 12 | Comfort | **Core set**: recenter (bound to key, pad and XR button), fade to black instead of a hard cut on respawn, mission end and view switch, an optional tunnel vignette scaled by angular rate (off by default), and a VR render-scale slider. No horizon lock, no snap turn. |
| 13 | Cockpit scale | **No asset edits; a per-plane VR world scale and seated eye offset in committed `data/vr_cockpits.json`**, tuned by the author at the headset. Defaults (1.0, zero offset) ship until each of the 11 planes is judged. |
| 14 | Release | **Lands on main behind the off-by-default option and ships in v0.4.0 as "VR (experimental)"** with a known-issues list; the label drops in a later release once the playtests and community reports are clean. |
| 15 | Dev modes, spyglass, photograph (defaults proposed in the grilling, not objected to) | **`--viewer`, `--freecam`, `--anim-lab` and the labs stay flat-only; the spyglass is a mono zoom picture on a binocular-style quad; the Danger Zone photograph and the screenshot capture keep reading a mono camera.** |
| 16 | The lens flare and full-pane effects in VR | **One angle-based world flare rig, used by VR always and by flat Enhanced once the author has judged a C3 montage in Chase and Cockpit; Original keeps the calibrated screen-space `LensFlareRig`.** Whiteout, flash and fade become head-locked near quads in VR. The Enhanced adoption is the one deliberate exception to the boundary rule, argued on its own merits: partial occlusion of the core and HDR glow. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "The cockpit panel's jitter is not float32 rounding" (PLAN-cockpit-panel C20, from a `--weapon-lab` hold at `dir=(0,0,-1)` measured with a luminance-weighted centroid) | At heading 0 the basis products are exact 0s and 1s, so the jitter cannot occur there. A flown `--det --shots=4` capture with `--direction=-0.743,0,-0.669`, registered by bezel-masked phase correlation, measured 0.02 px at the origin, 0.7 px at 10 km and 2.7 px at 20 km (1280x720). The plan closed the right fix (C21) unbuilt. Retired plan: `git show docs-archive:docs/plans/PLAN-cockpit-panel.md`. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | none | |
| **Direction sound, magnitude a judgement call** | A3 (the jitter is measured; the size of the VR fix's residual is not) | The *what* is settled; the *how much* is TUNE. |
| **Leads only, no mechanism yet** | every other item | Budget for investigation. The approaches come from a grilling and a code survey, not from a working prototype; Wave A exists to turn them into evidence or disproofs. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, never use it in a
worktree session here; use a local commit or a file copy.

## What the code ships

A read-only survey of the tree taken 2026-10-05. Line numbers are from that survey and will drift;
re-read before building on one. Paths under `CSVM/` unless noted.

**Renderer and window.**
- `project.godot:15` features "4.7", "C#", "Forward Plus"; no `rendering_method` key, so Forward+.
  No `[xr]`/`openxr` section exists.
- `project.godot:19-20` 1280x720; no stretch keys. `:38` `msaa_3d=2` (4x). `:46`
  `driver/threads/thread_model=2`, a separate render thread Godot flags experimental.
- The sim runs on `Utils/GameClock.cs:19` `FixedDt = 1f/60f` with an accumulator, not Godot's physics
  tick. Vsync and fps cap are runtime (`Utils/VSyncSetting.cs`); resolution is runtime
  (`Utils/ResolutionSetting.cs:216-220`, `ContentScaleMode=Viewport` when it differs from the window).
- `Utils/ViewportQuality.cs` applies FXAA/SMAA/TAA/FSR2 and `Scaling3DScale` to the root, the cockpit
  pass, the spyglass and the splitscreen panes; it writes nothing at Off/Native so goldens read Godot
  defaults.
- The renderer floor is observed, not coded: working Vulkan or D3D12 (`docs/tooling.md`).
- Godot's own OpenXR loader is already bundled in the export (`packaging/LICENSE-thirdparty.txt`).
  The WebRTC extension is the official single-precision release, pinned by `InstallWebRtc.ps1`.

**Shaders that read screen space or depth.**
- `Effects/EmitterRenderer.cs:58, 92-93` soft particles via `hint_depth_texture` + `SCREEN_UV`,
  unprojected with `INV_PROJECTION_MATRIX`; `:150`, `:352` read `hint_screen_texture`.
- `Effects/FogVolumeClutter.cs:113-114, 483` the same depth soft-fade.
- `Effects/HeatShimmer.cs:49, 109` `hint_screen_texture` refracted via `SCREEN_UV`.
- `shaders/csky_clutter_fade.gdshaderinc:62-67` a screen-space dither on fragcoord.
- No `VIEW_INDEX` anywhere. `csky_view_index` in `csky_atmosphere.gdshaderinc:84` is the game's
  sky-view selector, unrelated to stereo.
- No full-screen post shader. Screen effects are canvas layers: lens flare sprites and sun wash
  (`Session/World/LensFlareRig.cs:141, 172`), the cloud whiteout (`Session/World/WeatherRig.cs:1163`),
  `UI/Boards/ScreenFlash.cs`, the mission-end fade. `Launch/EnhancedLook.cs:63-82` adds SSAO and glow
  in Enhanced only.
- The skydome is a mesh anchored to the camera every frame (`Mech3/WorldBuilder.cs` BuildHorizon),
  one copy per `PlayerRig` on a per-player visual layer (`Flight/Camera/PlayerRig.cs:49-62`), the
  cloud deck likewise.

**Camera.**
- `Flight/Camera/PilotViewMode.cs:12-26` Chase=0, Cockpit=6 (first person with interior), Nose=7
  (first person without).
- `Flight/Camera/CameraController.cs` owns one `Camera3D`: `ViewMode`, `Head`, `EyePosition`,
  `EyePose` (`:191-224`), `FirstPersonPose` (`:397`), `FirstPersonView` (`:409`), `Chase` (`:610`).
  FOV: base 60 H, Cockpit 80 H, Nose 60 H, an Enhanced-only speed widen up to 6 (`:94, 101, 185,
  427-479`).
- `Flight/Camera/HeadLook.cs` has Snap, FreeLook, Padlock.
- `Flight/Camera/PlaneShake.cs` rotates a pivot above the plane model; it turns the interior, never
  the view.
- `Launch/Launcher.cs:834-839` builds the main Camera3D (Far 40000). Single player has no
  SubViewport: `PlayerRig.cs:29-40` uses the main viewport's camera.

**Cockpit pass.** `Flight/Hud/CockpitOverlay.cs:8-17` re-parents `cockpit1` into a private
`World3D` in its own transparent `SubViewport` (own Camera3D, Near 0.01, Far 100, cloned sun and
Environment), composited on `HudLayers.CockpitPass = -1`, so the interior is rendered near the origin
where float32 rounds below a pixel. `Sync` (`:105-130`) re-aims it from `FirstPersonPose` plus head
az/el each frame. `--no-cockpit-pass` leaves the interior in the main world.

**HUD and UI.**
- The HUD is 2D `CanvasLayer`s built by `Flight/Airframe/FlightController.cs:1233, 1239`; layer
  order in `UI/Boards/HudLayers.cs` (flare -3, whiteout -2, cockpit pass -1, world overlay 0, HUD 1,
  sun wash 2, then fades, board, debug, lab).
- World-to-screen projection through `Camera3D.UnprojectPosition`/`IsPositionBehind`:
  `Flight/Hud/ImpactReticle.cs:74-78`, `Flight/Hud/TargetHud.cs:647-674, 788-789`,
  `Flight/Modes/VersusHud.cs:164`, `Session/World/LensFlareRig.cs:208`,
  `UI/Overlays/MarkerOverlay.cs:246`; `EdgeMarker` clamps to the pane edge.
- `Flight/Hud/HudMetrics.cs:17-80` sizes the HUD from a 1440-high 16:9 reference and the root
  window height.
- Boards and menus are 2D at 800x600, uniformly scaled and letterboxed (`UI/Boards/BoardFit.cs:8-30`).
  Cinemas are a `TextureRect` on a CanvasLayer (`UI/Screens/CinemaScreen.cs:45-118`, frames from
  `UI/Boards/MovieSurface.cs`).

**Cutscenes.** The letterbox is a 3D card pinned 7.5 m in front of `camera1`, stretched to the pane
aspect (`Session/World/CutsceneController.cs:18-48, 328, 1360-1370`); cutscene cameras are
`Dictionary<Camera3D,float>` (`:144`); `GameClock.SimHeld` holds the sim.

**Splitscreen.** `UI/Boards/SplitScreen.cs` builds one `SubViewportContainer` + `SubViewport` +
`Camera3D` per player for 2-4 players, sharing the main `World3D` (`:322`).

**Input.** `Bindings/InputAction.cs` is the named-action enum (append-only). `Bindings/IDeviceState.cs`
is the raw-state interface (`IsKeyDown`, `IsButtonDown`, `IsMouseButtonDown`, `AxisValue`,
`HatState`), implemented by `GodotDeviceState`, `SeatDeviceState`, `Sticks/StickDeviceState`.
`Bindings/DeviceId.cs:5-30` has `DeviceKind` None, Keyboard, Joypad, Mouse.

**Capture and goldens.** `Tooling/CaptureDirector.cs:150` reads one viewport's texture;
`Tooling/GoldenShot.cs` hashes raw pixels plus the adapter name.

**Rig facts.** The author's machine has an RTX 5080; the active OpenXR runtime is Virtual Desktop's
(`C:\Program Files\Virtual Desktop Streamer\OpenXR\virtualdesktop-openxr.json`), SteamVR is the
second.

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

### Wave A, spikes (the gate)

1. ☐ A1 OpenXR boots in a flight session on VDXR and SteamVR
2. ☐ A2 The `--xr-sim` stand-in interface
3. ☐ A3 Precision near the eye: render-only floating origin, double-precision fallback
4. ☐ A4 Screen-space effects under multiview
5. ☐ A5 Cockpit scale per plane

### Wave B, the VR rig

6. ☐ B6 VR mode option, relaunch, fallback and mode gating
7. ☐ B7 The XR rig in `PlayerRig`, recenter and per-plane scale data
8. ☐ B8 The cockpit interior in the main world under VR
9. ☐ B9 Views in VR: rigid chase, nose, head tracking in place of head look

### Wave C, screen, HUD and input

10. ☐ C10 The virtual theater screen for menus, boards and cinemas
11. ☐ C11 `DeviceKind.Xr`, XR actions and the screen pointer
12. ☐ C12 The cockpit-locked HUD panel
13. ☐ C13 World-tied markers and the edge-marker ring
14. ☐ C14 An angle-based world flare rig (VR and flat Enhanced); the other pane effects as near quads in VR

### Wave D, cutscenes, comfort and performance

15. ☐ D15 Cutscenes as a stereo 3D-cinema pair on the theater screen
16. ☐ D16 Comfort: fades, tunnel vignette, render-scale slider
17. ☐ D17 Port the screen-space effects to multiview
18. ☐ D18 Performance pass to the 90 Hz budget
19. ☐ D19 Spyglass as a binocular quad

### Wave E, headset pass and release

20. ☐ E20 Per-plane scale and eye-offset tuning at the headset
21. ☐ E21 At-the-headset playtests on both runtimes
22. ☐ E22 Experimental release: docs, shortcut, known issues

## Dependency and parallelism notes

Wave A gates everything. A1 blocks A2-A5 (they all need a booting XR session or its stand-in to
exist); A2, A3, A4 and A5 can then run in parallel, since they touch disjoint files (A2 new
`Tooling`/`Utils` stand-in code, A3 `PlayerRig`/`GameSession` anchoring, A4 the `Effects/` shaders,
A5 measurement only). If A3 ends in the double-precision fallback, stop and re-plan before Wave B: the
engine, templates and WebRTC builds become their own wave and every golden is re-pinned.

B6 and B7 run in parallel (Options/Launcher versus `PlayerRig`); B8 and B9 need B7 and both touch
`Flight/Camera/CameraController.cs` and `Flight/Hud/CockpitOverlay.cs`, so run them in sequence.
C10 and C11 form a pair (the pointer needs the screen) and can run beside C12-C14. C12 and C13 both
touch `Flight/Hud/` and `FlightController.cs`'s HUD construction: run them in sequence. C14 owns
`Session/World/LensFlareRig.cs`, `WeatherRig.cs` and `UI/Boards/ScreenFlash.cs`. D15 needs C10; D16
needs B9 and C14 (the fades); D17 needs A4; D18 runs last in Wave D. Wave E needs everything and the
author at the headset.

---

# Wave A, spikes (the gate)

## A1 ☐ OpenXR boots in a flight session on VDXR and SteamVR

**Goal.** A dev launch with `--xr-mode on` puts a `--stage=empty` flight into the headset on both
runtimes, head tracking moves the view, and the flat launch is unchanged. The render-thread question
(keep `thread_model=2` or drop it under XR) has a measured answer.

**Evidence (confidence: lead-only).** `project.godot` has no XR settings and runs the experimental
separate render thread (`:46`). Godot's OpenXR loader is already in the export. Whether Godot 4.7
.NET's OpenXR frame pacing works with the separate render thread is unknown. Whether enabling
`xr/shaders/enabled` (the multiview shader variants) moves any flat golden or the shader warmup cost
is unknown.

**Approach.** Add the OpenXR settings to `project.godot` with `xr/openxr/enabled=false` and
`xr/shaders/enabled=true`, start with Godot's `--xr-mode on`, and put an `XROrigin3D` + `XRCamera3D`
at the flight's first-person pose in a throwaway branch of the camera code. Find out from the XR
interface whether initialisation succeeded, and log the runtime name. Try both thread models.
<TODO: whether `xr/shaders/enabled` may stay on for flat runs depends on the golden and warmup
measurement this item takes.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Both runtimes show the stage in the headset (the author's check). The complete battery is
green with the new project settings and OpenXR off, goldens unmoved. <TODO: the frame-timing
instrument for the render-thread comparison.>

**⚠ Traps.** `--xr-mode` is a Godot engine argument and goes before `--`, not among the user args.
The active runtime on the author's machine is VDXR; a SteamVR test needs the runtime switched (or
`XR_RUNTIME_JSON` set), otherwise both "runs" exercise VDXR.

## A2 ☐ The `--xr-sim` stand-in interface

**Goal.** `--xr-sim` runs any flight with a stereo XR path and a pinned head pose, no headset and no
OpenXR, on the hidden desktop, deterministic under `--det`, and capturable as per-eye or side-by-side
images for suites and goldens.

**Evidence (confidence: lead-only).** Godot ships `MobileVRInterface`, which renders stereo side by
side into an ordinary window; C# can implement `XRInterfaceExtension`. Capture reads one viewport
(`Tooling/CaptureDirector.cs:150`), goldens hash raw pixels plus adapter (`Tooling/GoldenShot.cs`).
Neither path has been tried here.

**Approach.** First cut on `MobileVRInterface` with fixed IPD and a pose from the flags; replace with
an `XRInterfaceExtension` if MobileVR's lens distortion or pose handling gets in the way of a pinned,
undistorted pair. Wire it through the same rig code A1 uses, so the stand-in exercises the real VR
path. Add `--xr-sim` to `docs/cli.md` and to the `--det` story.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Two consecutive `--det --xr-sim --screenshot=` runs hash identically; one new golden in
the manifest. <TODO: the exact pose/chapter the golden pins.>

**⚠ Traps.** A lens-distorted stand-in output would make per-eye precision measurements (A3) read
the distortion, not the jitter.

## A3 ☐ Precision near the eye: render-only floating origin, double-precision fallback

**Goal.** In VR, the cockpit interior, a HUD quad and the plane in rigid chase show sub-pixel jitter
per eye at 20 km from the chapter origin, with simulation, physics and network still in chapter
coordinates. Or, if that cannot be done, a documented decision to move to a double-precision Godot
build.

**Evidence (confidence: direction-sound).** The jitter is measured: 0.02 px at the origin, 0.7 px at
10 km, 2.7 px at 20 km at 1280x720 with a rotated heading (⚠ table row 1). The flat game avoids it
only for the interior, through `Flight/Hud/CockpitOverlay.cs`'s private world, which cannot work in
stereo. Headsets resolve roughly 20-25 px per degree against about 16 on the flat 80° cockpit view,
so the same rounding is larger in VR and differs between eyes. That a render-only offset removes it
is a hypothesis.

**Approach.** Prototype a render-side origin shift relative to the player, keeping physics bodies and
the sim out of the shifted subtree. Measure with the bezel-masked phase-correlation rig, per eye
through A2's stand-in, `--direction=-0.743,0,-0.669`, `--shots=N`, at 0, 10 and 20 km. If it misses,
write up the double-precision path (custom editor and Windows/Linux export templates, .NET glue built
with `precision=double`, a double build of webrtc-native in place of `InstallWebRtc.ps1`'s pinned
release, a re-pin of every golden) and stop for a re-plan.
<TODO: which subtree carries the shift, and how the sky/deck anchoring in `PlayerRig` and
`GameSession` follows it.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Sub-pixel per-eye jitter at 20 km on a rotated heading with a shell control region,
measured by phase correlation over consecutive frames. Flat goldens unmoved, the full battery green.

**⚠ Traps.** A heading-0 pin cannot show the effect (⚠ table row 1). A luminance centroid moves with
shading; use phase correlation on a bezel-only mask. The scratch tool from PLAN-cockpit-panel C20 was
swept; rebuild it from `git show docs-archive:docs/plans/PLAN-cockpit-panel.md`.

## A4 ☐ Screen-space effects under multiview

**Goal.** A per-effect verdict on how each screen-space shader behaves in stereo: correct as is,
portable to `VIEW_INDEX`, or impossible, with the porting cost named.

**Evidence (confidence: lead-only).** The users are `Effects/EmitterRenderer.cs` (depth texture,
screen texture), `Effects/FogVolumeClutter.cs` (depth), `Effects/HeatShimmer.cs` (screen texture),
`shaders/csky_clutter_fade.gdshaderinc` (fragcoord dither). No shader uses `VIEW_INDEX` today.
How Godot 4.7 exposes the depth and screen textures under multiview is not checked here.

**Approach.** Run each effect under A2's stand-in and under A1's headset session, compare the two
eyes, and read the Godot multiview shader documentation for the depth/screen texture forms.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: the capture per effect and what counts as correct per eye.>

**⚠ Traps.** <TODO: none known yet.>

## A5 ☐ Cockpit scale per plane

**Goal.** For each of the 11 planes, a measured answer to whether `cockpit1` and the
`cockpit_camera` eye point are human-scale at 1:1, giving starting values for `data/vr_cockpits.json`.

**Evidence (confidence: lead-only).** Nothing documents the interior's scale. `docs/org/cameraViews.md`
holds the per-plane `cockpit_camera` offsets and the 80° flat FOV the interior was authored for.

**Approach.** Measure panel width, seat-to-panel distance and canopy dimensions per plane from the
built interiors, compare with real 1930s fighter cockpits, and propose a world scale and eye offset
for each. The author's judgement at the headset (E20) overrides the proposal.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: the measurement method and the reference dimensions.>

**⚠ Traps.** A measured number is a starting point; the decision is the author's at the headset
(Decision 13).

# Wave B, the VR rig

## B6 ☐ VR mode option, relaunch, fallback and mode gating

**Goal.** Options carries "VR mode"; toggling it saves the choice and relaunches the executable with
`--xr-mode on`. With VR on, a failed runtime falls back to flat with a clear message, local
splitscreen is refused at setup, and the dev modes stay flat.

**Evidence (confidence: lead-only).** OpenXR must start with the engine (Decision 9). Options are in
`Utils/` (config, options); splitscreen is `--players` and `UI/Boards/SplitScreen.cs`.

**Approach.** An option key, a relaunch through `OS.CreateProcess` with the engine argument, an
init-failure path that shows a board message and continues flat, and a refusal of `--players>1`
while VR is live. <TODO: where in the launch menu the refusal shows.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: a suite for the relaunch argument and the splitscreen refusal under `--xr-sim`.>

**⚠ Traps.** <TODO: none known yet.>

## B7 ☐ The XR rig in `PlayerRig`, recenter and per-plane scale data

**Goal.** In a VR flight the XR origin follows the plane's first-person pose with the per-plane
world scale and eye offset from `data/vr_cockpits.json`; the camera-anchored skydome and cloud deck
follow the XR camera; recenter snaps the seated eye back to `cockpit_camera`.

**Evidence (confidence: lead-only).** Single player uses the main viewport's camera
(`Flight/Camera/PlayerRig.cs:29-40`); the dome and deck are anchored per rig every frame by reading
`Camera.Position` (`PlayerRig.cs:49-62`, the anchoring loop in `GameSession`).

**Approach.** An XR flavour of `PlayerRig` holding the `XROrigin3D`/`XRCamera3D`, the anchoring loop
reading the XR camera, recenter as a new named action, and the data file read once per session.
Integrates A3's precision fix.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` suite pinning the eye at `cockpit_camera` and the dome centred on
the XR camera.>

**⚠ Traps.** The suites receive `_camera` from the Launcher (`Launch/Launcher.cs:849`); a rig change
that swaps the camera object must keep the flat suites reading the camera they read today.

## B8 ☐ The cockpit interior in the main world under VR

**Goal.** In VR, Cockpit view shows `cockpit1` in the main world, correctly lit and occluding the
world per eye, with the gauges moving, and no cockpit pass is built.

**Evidence (confidence: lead-only).** `--no-cockpit-pass` already leaves the interior in the main
world (`Flight/Hud/CockpitOverlay.cs:15`). The pass also carries a cloned sun, the panel point-light
term through `light_origin` (`CockpitOverlay.cs:20-33`) and the shake mount (`WobbledMount`).

**Approach.** Under VR take the `--no-cockpit-pass` path, keep `light_origin`, the shake and the
gauge drive working on the in-world interior, and rely on A3 for precision.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` golden of the cockpit at a rotated heading far from the origin.>

**⚠ Traps.** The flat game keeps the pass (the boundary rule); the change is VR-only.

## B9 ☐ Views in VR: rigid chase, nose, head tracking in place of head look

**Goal.** Cockpit is the VR default; Nose works; Chase is attached rigidly to the plane with no spring
lag, no speed FOV and no swing; head look's Snap, FreeLook and Padlock are inactive under VR and the
headset owns the head.

**Evidence (confidence: lead-only).** Views are `Flight/Camera/PilotViewMode.cs:12-26`; the chase is
`CameraController.Chase` (`:610`); head look is `Flight/Camera/HeadLook.cs`; FOV is set from the view
(`CameraController.cs:427-479`), which the headset overrides.

**Approach.** A VR branch in `CameraController` that places the XR origin per view and skips the
chase spring, the speed widen and the head-look input; the view cycle stays a named action.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` suite per view checking the origin pose against the plane.>

**⚠ Traps.** <TODO: none known yet.>

# Wave C, screen, HUD and input

## C10 ☐ The virtual theater screen for menus, boards and cinemas

**Goal.** In VR every 2D screen (launch menu, campaign boards, briefings, hangar, pause, results, load
and boot screens, MPEG cinemas) is drawn on a floating quad in front of the seated player, at a
readable size, and works with pad and keyboard focus as today.

**Evidence (confidence: lead-only).** Boards fit an 800x600 space (`UI/Boards/BoardFit.cs:8-30`);
cinemas are a `TextureRect` on a CanvasLayer (`UI/Screens/CinemaScreen.cs:45-118`). Canvas layers on
the main viewport are not shown in the headset. <TODO: confirm that fact in Godot 4.7.>

**Approach.** Render the board and cinema canvas layers into a SubViewport that a quad in the XR world
displays; a dark or neutral surround while no flight is live. <TODO: the surround, the screen size
and distance.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` golden of the launch menu on the screen.>

**⚠ Traps.** <TODO: none known yet.>

## C11 ☐ `DeviceKind.Xr`, XR actions and the screen pointer

**Goal.** XR controllers are a device kind in the binding seam with default bindings for recenter,
pause and view cycle, rebindable in the controls screen, and a laser pointer clicks the theater
screen. Meta Touch is the first profile.

**Evidence (confidence: lead-only).** `Bindings/DeviceId.cs:5-30` (`DeviceKind`),
`Bindings/IDeviceState.cs` (raw state, zero for an absent device), `Bindings/InputAction.cs`
(append-only enum), `Bindings/DefaultBindings`.

**Approach.** Append `DeviceKind.Xr`, an `IDeviceState` implementation over Godot's
`XRController3D`/OpenXR action map, defaults in the shipped keymap, and a pointer that turns a ray hit
on the screen quad into SubViewport input events.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: a units test for the binding resolution; the pointer is the author's at the
headset (E21).>

**⚠ Traps.** <TODO: none known yet.>

## C12 ☐ The cockpit-locked HUD panel

**Goal.** In VR the flat HUD chrome (gauges, compass tape, messages, chat, prompt line, scores) is
drawn on a quad fixed to the cockpit 1.5-2 m ahead and slightly low, readable with a glance.

**Evidence (confidence: lead-only).** The HUD is CanvasLayers built in
`Flight/Airframe/FlightController.cs:1233, 1239`, sized by `Flight/Hud/HudMetrics.cs` from the root
window height.

**Approach.** Under VR parent the chrome layers into a SubViewport shown on a cockpit-locked quad, and
give `HudMetrics` a panel size in place of the window height. <TODO: the panel's pixel size, distance
and angle; the author judges legibility.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` golden of the panel in flight.>

**⚠ Traps.** The world-tied markers (C13) must leave the panel, otherwise they are drawn at panel
depth.

## C13 ☐ World-tied markers and the edge-marker ring

**Goal.** Target boxes, the impact reticle, the lead marker and the versus markers appear in the
world at their real depth in both eyes; off-view targets are shown by a head-relative arrow ring.

**Evidence (confidence: lead-only).** The projection sites: `Flight/Hud/ImpactReticle.cs:74-78`,
`Flight/Hud/TargetHud.cs:647-674, 788-789`, `Flight/Modes/VersusHud.cs:164`,
`UI/Overlays/MarkerOverlay.cs:246`; `EdgeMarker` clamps to the pane edge.

**Approach.** Under VR replace each `UnprojectPosition` draw with a billboard at the world point,
scaled with distance to hold an angular size; the edge ring hangs on the XR camera.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` capture with a target at a known depth, checking per-eye disparity.>

**⚠ Traps.** <TODO: none known yet.>

## C14 ☐ An angle-based world flare rig (VR and flat Enhanced); the other pane effects as near quads in VR

**Goal.** Two parts. (1) The sun's lens flare is computed in angles and drawn in the world: the core
and the three ghost rings as billboards at sky-dome distance along the line from the sun direction
to the view-forward direction, the sun wash as a quad near the eye. VR always uses it; flat Enhanced
adopts it once the author has judged it against the current rig; Original keeps today's screen-space
rig unchanged. (2) The remaining full-pane canvas effects (the cloud whiteout, `ScreenFlash`, the
mission-end fade) get a VR form as head-locked quads near the eye.

**Evidence (confidence: lead-only).** `Session/World/LensFlareRig.cs` draws four additive
`TextureRect` sprites on `HudLayers.FlareSprites` at fractions 0, 0.5, 0.9 and 2.0 of the
sun-to-screen-centre vector (`:46-60`), sized in pixels against a 720 px reference height (`:30`),
plus a white `ColorRect` wash on `HudLayers.SunWash` above the HUD whose opacity falls off linearly
with the sun's screen distance from centre (`:35-37`, `:249-256`). Occlusion is one physics ray to
the sun's centre, so the sprites are on or off (`:215-222`); the wash ignores occlusion. The spec is
measured off C3 footage, not decoded (`docs/org/weather.md`); only C2 and C3 author a flare, and C2's
is predicted, not verified (`:17-19`). Enhanced runs HDR glow (`Launch/EnhancedLook.cs:77-82`), which
an in-world core would feed and a canvas sprite cannot. The other effects are canvas layers
(`Session/World/WeatherRig.cs:1163`, `UI/Boards/ScreenFlash.cs`), which the headset does not show.

**Approach.** (1) A new rig that takes a camera (flat) or the XR camera (VR) and places the elements
by direction each frame. Depth-test the core against the world so it slides behind a ridge or a
zeppelin edge gradually; draw the ghosts with depth test off, since they are lens artifacts a
building in their direction must not hide. Size the elements from the live camera's FOV in flat play,
so the original's fixed pixel size holds in Cockpit (80° H) as well as in chase; in VR size them from
the reference external FOV mapping. Gate selection on graphics mode and VR, leaving the Original path
on `LensFlareRig` as it is. (2) One head-locked near-quad host for the whiteout, flash and fade,
nearer than the HUD panel (C12). <TODO: whether the VR flare shows the ghost rings by default, or
the core and wash only with the ghosts as an option; the author decides at the headset (E21).>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Original-mode goldens and any C2/C3 flare capture unmoved. For Enhanced adoption, a
side-by-side montage of the current and the world rig on C3 in Chase and in Cockpit, put to the
author before the world rig replaces the screen rig in Enhanced. <TODO: an `--xr-sim` per-eye capture
of the flare on C3; the whiteout and fade checks in VR.>

**⚠ Traps.** Do not change `LensFlareRig` itself: it is the Original path and carries the footage
calibration. An angle-sized rig drawn at a fixed angle shrinks in Cockpit view against the original's
fixed pixel size; size from the live FOV. A depth-tested ghost is wrong, only the core may be
occluded. The whiteout sits under the cockpit pass so the interior stays clear
(`docs/architecture/Session.md`); in VR the interior is in the main world (B8), so that ordering has
to be rebuilt another way. Enhanced adoption is a look judgement and needs the montage, never a
luminance distance alone.

# Wave D, cutscenes, comfort and performance

## D15 ☐ Cutscenes as a stereo 3D-cinema pair on the theater screen

**Goal.** In VR an in-engine cutscene plays on the theater screen with depth: two cameras on the
authored camera, one IPD apart, each eye's image picked by `VIEW_INDEX`, the authored framing and the
letterbox exactly as in the flat game.

**Evidence (confidence: lead-only).** Cutscene cameras and the letterbox card are in
`Session/World/CutsceneController.cs:18-48, 144, 328, 1360-1370`; the card is stretched to the pane
aspect, which becomes the screen quad's aspect.

**Approach.** Two SubViewports with cameras following the active cutscene camera at plus and minus
half the runtime's IPD; the screen quad's shader samples by `VIEW_INDEX`. <TODO: the convergence
distance (where the screen plane sits in depth).>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: an `--xr-sim` capture of a known intro cutscene frame, per eye.>

**⚠ Traps.** <TODO: none known yet.>

## D16 ☐ Comfort: fades, tunnel vignette, render-scale slider

**Goal.** Respawn, mission end and view switch fade through black instead of cutting; an optional
tunnel vignette scaled by angular rate is off by default; a VR render-scale slider sets the
runtime's resolution multiplier.

**Evidence (confidence: lead-only).** Decision 12. `Utils/ViewportQuality.cs` already owns
`Scaling3DScale` for the flat viewports.

**Approach.** <TODO: the fade duration and the vignette curve are TUNE; the author sets them at the
headset.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO>

**⚠ Traps.** <TODO: none known yet.>

## D17 ☐ Port the screen-space effects to multiview

**Goal.** Soft particles, the fog-volume fade and heat shimmer render correctly in both eyes; the
clutter dither stays if A4 found it reads fine per eye.

**Evidence (confidence: lead-only).** A4's verdicts. <TODO: depends on A4.>

**Approach.** Per A4.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Flat goldens unmoved; <TODO: the per-eye check from A4.>

**⚠ Traps.** The shaders are generated in C# (`Mech3/ShaderTwins.cs` pairs per graphics mode); a
port touches both twins.

## D18 ☐ Performance pass to the 90 Hz budget

**Goal.** A VR flight on the 5080 at the runtime's 1.0 resolution with Enhanced on holds 90 Hz using
at most half of the 11 ms frame.

**Evidence (confidence: lead-only).** Decision 10. No VR frame has been timed.

**Approach.** Time the worst chapters in VR, then cut by render scale defaults, per-eye costs (MSAA,
SSAO, shadows) and the effects with the largest stereo cost. <TODO: the instrument; `--perf` is the
flat profiler stand-in.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: the measured frame time per chapter on the reference rig.>

**⚠ Traps.** Read `docs/verification.md` on performance measurement before timing; the author's rig
runs the sim clock behind wall time in late chapters.

## D19 ☐ Spyglass as a binocular quad

**Goal.** In VR the spyglass zoom shows as a mono picture on a binocular-style quad instead of
narrowing the stereo FOV.

**Evidence (confidence: lead-only).** The spyglass has its own camera (`Flight/Camera/SpyglassView.cs:62`)
and its own viewport in `ViewportQuality`.

**Approach.** Show the spyglass viewport's texture on a head-locked quad while it is up.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO>

**⚠ Traps.** <TODO: none known yet.>

# Wave E, headset pass and release

## E20 ☐ Per-plane scale and eye-offset tuning at the headset

**Goal.** Each of the 11 planes has a world scale and eye offset in `data/vr_cockpits.json` that the
author has judged right at the headset.

**Evidence (confidence: lead-only).** A5's proposals; Decision 13.

**Approach.** A dev key or lab to nudge the scale and offset live and write the file. <TODO: whether
that lab is worth building or the author edits the JSON between runs.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** The author's sign-off per plane.

**⚠ Traps.** Look judgements are the author's; a measured scale does not override them.

## E21 ☐ At-the-headset playtests on both runtimes

**Goal.** The author has flown each mode in VR on VDXR and on Steam Link/SteamVR and judged comfort,
controller binding, the theater screen, the HUD and the cutscene window.

**Evidence (confidence: lead-only).** Decision 11.

**Approach.** File the checks as `playtest` GitHub issues (`docs/agents/issue-tracker.md`).
<TODO: the check list per mode.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** The issues closed with the author's verdicts.

**⚠ Traps.** <TODO: none known yet.>

## E22 ☐ Experimental release: docs, shortcut, known issues

**Goal.** v0.4.0 ships VR labelled experimental, with the VR shortcut in the archive, the option and
flags documented, and a known-issues list.

**Evidence (confidence: lead-only).** Decision 14. The release runs through `PublishRelease.ps1`.

**Approach.** `docs/cli.md` (`--xr-sim`, `--xr-mode`), `docs/controls.md` (XR bindings), the README
and the release notes; the shortcut through `ExportRelease.ps1`'s packaging. <TODO: the known-issues
list comes from E21.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: the release zip checked in `RunSandbox.ps1` with VR off, to show the flat game is
untouched.>

**⚠ Traps.** <TODO: none known yet.>
