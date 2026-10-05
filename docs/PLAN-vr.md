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
- The rigid chase plane is free of float32 shimmer at 20 km from the chapter origin. The cockpit and
  the HUD panel shimmer far from the chapter origin (A3: 5-8 px per eye at 10-20 km at headset
  density), accepted as a known issue of the experimental release (Decision 17).
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
| 4 | float32 precision near the eye | **Spike a render-only floating origin first; if it cannot reach sub-pixel per eye without touching sim or physics, fall back to a double-precision Godot build** (editor, export templates, .NET glue, WebRTC extension). The stock engine is kept if possible.** Superseded by Decision 17. |
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
| 17 | float32 precision near the eye, after A3 found no render-only fix | **Accept the shimmer and continue on the stock single-precision engine.** The cockpit interior and the HUD panel shimmer 5-8 px per eye at 10-20 km from the chapter origin at headset density; rigid chase is unaffected. It ships as a known issue of the experimental release. The double-precision build and a game-wide floating origin stay open as later work (A3's write-up), not items of this plan. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "The cockpit panel's jitter is not float32 rounding" (PLAN-cockpit-panel C20, from a `--weapon-lab` hold at `dir=(0,0,-1)` measured with a luminance-weighted centroid) | At heading 0 the basis products are exact 0s and 1s, so the jitter cannot occur there. A flown `--det --shots=4` capture with `--direction=-0.743,0,-0.669`, registered by bezel-masked phase correlation, measured 0.02 px at the origin, 0.7 px at 10 km and 2.7 px at 20 km (1280x720). The plan closed the right fix (C21) unbuilt. Retired plan: `git show docs-archive:docs/plans/PLAN-cockpit-panel.md`. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | none | |
| **Direction sound, magnitude a judgement call** | none | |
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

1. ◐ A1 OpenXR boots in a flight session on VDXR and SteamVR
2. ☑ A2 The `--xr-sim` stand-in interface
3. ❌ A3 Precision near the eye: render-only floating origin, double-precision fallback
4. ☑ A4 Screen-space effects under multiview
5. ☑ A5 Cockpit scale per plane

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
A5 measurement only). A3 found no render-only fix, and the author chose to accept the cockpit
shimmer and continue on the stock engine (Decision 17), so Wave B follows Wave A unchanged.

B6 and B7 run in parallel (Options/Launcher versus `PlayerRig`); B8 and B9 need B7 and both touch
`Flight/Camera/CameraController.cs` and `Flight/Hud/CockpitOverlay.cs`, so run them in sequence.
C10 and C11 form a pair (the pointer needs the screen) and can run beside C12-C14. C12 and C13 both
touch `Flight/Hud/` and `FlightController.cs`'s HUD construction: run them in sequence. C14 owns
`Session/World/LensFlareRig.cs`, `WeatherRig.cs` and `UI/Boards/ScreenFlash.cs`. D15 needs C10; D16
needs B9 and C14 (the fades); D17 needs A4; D18 runs last in Wave D. Wave E needs everything and the
author at the headset.

---

# Wave A, spikes (the gate)

## A1 ◐ OpenXR boots in a flight session on VDXR and SteamVR

**Goal.** A dev launch with `--xr-mode on` puts a `--stage=empty` flight into the headset on both
runtimes, head tracking moves the view, and the flat launch is unchanged. The render-thread question
(keep `thread_model=2` or drop it under XR) has a measured answer.

**Evidence (confidence: traced to code in the Godot 4.7-stable source for the settings, the arguments
and the render path; lead-only for anything the headset shows until the author's run).**
- `--xr-mode on` overrides both XR settings for one launch. `OpenXRAPI::openxr_is_enabled` reads
  `xr/openxr/enabled` only under `default`, and the `RendererCompositor` constructor
  (`servers/rendering/renderer_compositor.cpp`) reads `xr/shaders/enabled` only under `default`
  too. Its `is_xr_enabled()` is what makes `SceneShaderForwardClustered` enable the multiview shader
  groups. So both settings stay off in `project.godot` and a headset launch still gets multiview.
- `xr/shaders/enabled=true` moved no golden: 24 of 24 hash-identical with the setting flipped
  (`RunTests.ps1 -SkipUnits -SkipEngine`). Its startup cost is direction only: three interleaved
  off/on pairs of the perf stage's `c1-flight` (`-Perf -PerfFilter flight -PerfIterations 2`, every
  launch's `[perf] startup` line, PERF-8) read a median total of 10,995 ms off against 11,905 ms on,
  and a `world` phase of 2,392 against 2,759 ms. The same-build spread was 7.7 to 15.2 s, since
  another session ran six Godots beside it at 79 % CPU (PERF-48, METHOD-2). It is kept off: Godot
  turns the variants on itself under `--xr-mode on`, so a flat run gains nothing from paying for it.
- Godot consumes `--xr-mode` and `--render-thread` before filling `OS.GetCmdlineArgs` (`main.cpp`'s
  `main_args`). `XrLaunch` reads `System.Environment.GetCommandLineArgs()` instead; a hidden-desktop
  probe confirmed it carries the engine arguments.
- `--render-thread safe|separate` exists in 4.7 (`main.cpp`) and beats `thread_model` for one launch.
  `RenderingServer.IsOnRenderThread()` on the main thread reports which one is live.
- Under XR the renderer takes the eye poses from `XRServer`'s world origin (the `XROrigin3D`) and the
  runtime, and from the current camera only its near/far planes, Environment and cull mask
  (`renderer_scene_cull.cpp`, `render_camera`). So the flat camera stays current and its zone cull
  mask keeps working; the origin follows it.
- `xr/openxr/startup_alert` defaults to a modal dialog when the runtime fails, and
  `xr/openxr/reference_space` to Stage (floor level). The project sets no dialog and Local.
- A dev launch runs the editor binary, which saves Godot's default action map to
  `CSVM/openxr_action_map.tres` the first time OpenXR starts (`OpenXRInterface::_load_action_map`
  under `TOOLS_ENABLED`). C11 owns that file; until then it is an untracked side effect.

**Approach (landed).** `project.godot` gains an `[xr]` section: OpenXR and its shaders off, Local
reference space, no startup dialog. `Launch/XrSession.cs` is built once by `Launcher._Ready` when
Godot's OpenXR interface is initialised: the main viewport's `UseXR`, `VSyncSetting.Headset` pacing
(no V-Sync, no cap, also on an Options apply), and an `XROrigin3D` with the tracked `XRCamera3D`
(`Head`) placed every frame by `Flight/Camera/XrMount.cs`: the first pane's flat camera eye at the
aircraft's attitude in Cockpit or Nose, heading only in every other view. It logs
`xr: OpenXR live runtime=... version=... system=... eye_px=WxH refresh_hz=... render_thread=...`,
the session states, and an `xr frames n= mean_ms= max_ms= late= missed= refresh_hz= gpu_ms=
render_thread=` Debug line per wall second (`Launch/XrFrameWindow.cs`; `late` is an interval of
1.5 refresh periods or more, `missed` the refreshes those skipped). A requested launch whose runtime
did not start logs a warning and flies flat. `RunGame.ps1 -EngineArgs` puts engine arguments before
the `--`. B7 grows the per-player rig out of `XrSession` and `XrMount`.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** The complete battery is green with the new settings and OpenXR off, goldens unmoved.
Owed at the headset (the author's): on VDXR and on SteamVR (`XR_RUNTIME_JSON` at
`C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json`), each with the
separate and the single render thread, a `--stage=empty --view=cockpit --no-cockpit-pass` flight
(1) shows the stage in stereo, (2) turns with the head, (3) logs `xr: OpenXR live` naming the
runtime, and (4) leaves `xr frames` lines whose `late`/`missed` and `mean_ms` against the refresh
period answer which thread model holds the headset rate. A headset launch writes its stereo variants
into the shared shader cache, so the next flat launch on the machine should print one
`br != variant_size` line (A2, LOG-25; traced, not run under OpenXR): the first engine stage after a
headset session can fail its error screen on it, and a second run is clean. A launch with the
runtime off logs the warning and flies flat. The HUD, menus, boards and the cockpit pass are canvas layers and do not
show in the headset; that is C10, C12 and B8, not a failure here.

The first run (VDXR, separate render thread) came up as `VirtualDesktopXR 1.0.10`, Meta Quest 3,
3072x3264 per eye at 120 Hz, stereo with head tracking; the earlier attempt with the headset not
yet connected logged `XR_ERROR_FORM_FACTOR_UNAVAILABLE` and flew flat as designed. At 120 Hz the
GPU took about 11 ms against the 8.3 ms period and missed refreshes every second; set the runtime
to 90 Hz (Decision 10) for the remaining runs. The cockpit defects it showed belong to B8. A1
leaves the tracking origin where the runtime's Local space puts it and never recenters; the
follow-up is `XRServer.CenterOnHmd(ResetButKeepTilt, keepHeight: false)` on the first valid head
pose after `SessionFocussed` and on `PoseRecentered`, landed with or before B7's recenter action.

**⚠ Traps.** `--xr-mode` is a Godot engine argument and goes before `--`, not among the user args.
The active runtime on the author's machine is VDXR; a SteamVR test needs the runtime switched (or
`XR_RUNTIME_JSON` set), otherwise both "runs" exercise VDXR. `XR_RUNTIME_JSON` set in a PowerShell
session stays set for every later launch from it. The `xr frames` line must stay at Debug: a console
line costs about 2 ms on the frame path (PERF-35) and would make its own late frame every second.

**Verified.** <pending orchestrator run>

## A2 ☑ The `--xr-sim` stand-in interface

**Goal.** `--xr-sim` runs any flight with a stereo XR path and a pinned head pose, no headset and no
OpenXR, on the hidden desktop, deterministic under `--det`, and capturable as per-eye or side-by-side
images for suites and goldens.

**Evidence (confidence: traced to code in the Godot 4.7-stable source for the feature tag, the
cache misread and the stereo readback; measured for determinism, the pose law and the golden).**
- The multiview variants are turned on per launch by a **custom feature tag**, not by `--xr-mode
  on`. `ProjectSettings`' constructor (`core/config/project_settings.cpp`, `TOOLS_ENABLED`, not in
  the editor) reads custom tags from `GODOT_EDITOR_CUSTOM_FEATURES`, `OS::has_feature` honours
  them, and `RendererCompositor` reads `xr/shaders/enabled` through `GLOBAL_GET`, which applies
  feature overrides. So `shaders/enabled.csvm_xr_sim=true` in `[xr]` turns the variants on for a
  launch whose environment names the tag, and `openxr/enabled` has no override, so OpenXR never
  starts. `XR_RUNTIME_JSON` at a missing file with `--xr-mode on` was not needed and never tried.
- `override.cfg` was rejected: Godot loads it from `res://` and the executable's folder on every
  launch (`ProjectSettings::_setup`), so it is not per launch and four golden workers share it.
- **The stereo launch must not share the shader cache.** Godot 4.7's `ShaderRD::_load_from_cache`
  skips a disabled variant's size field without skipping its bytes, so a flat launch reading a cache
  file an `--xr-sim` launch wrote misreads it: after one `--xr-sim` run the next flat run printed
  `br != variant_size` for `VrsShaderRD` (one shader), which would fail the engine stage's error
  screen. `rendering/shader_compiler/shader_cache/enabled.csvm_xr_sim=false` keeps the stand-in out
  of it; three xr/flat alternations afterwards printed nothing. The C1 golden launch takes 11.1 s
  wall without the cache (LOG-25).
- **`MobileVRInterface` is disqualified, from its source and a capture.** Its `post_draw_viewport`
  applies lens distortion to the window unconditionally; its eye size is the window's half times a
  1.5 oversample (a 1920x1080 pair from the 1280x720 window, which breaks the manifest's size); its
  projection comes from phone geometry (`set_for_hmd`, display width and lens distance, no angle);
  and its head orientation comes only from device sensors on wall-clock ticks, so no yaw or pitch can
  be pinned. The capture (`.scratch/A2/mobilevr.png`, scratch code since removed) drew a far wider,
  off-centre pair than the cockpit eye.
- **The stand-in is a C# `XRInterfaceExtension`** (`Launch/XrSimInterface.cs`): symmetric
  per-eye frusta at a horizontal angle, eyes IPD apart, a pinned `head` tracker, the view transform
  composed in OpenXR's order, and an undistorted side-by-side blit to the window. Its render-thread
  callbacks read only `XRServer`'s render-thread copies and immutable eyes. Godot calls them from the
  separate render thread without complaint.
- **Pose law checked against the flat camera.** `--xr-sim=eye=1280x720,fov=96.4268,ipd=0,shot=left,
  pitch=-4.70001` (the flat cockpit's 64.37° vertical angle at 16:9, and its fixed head pitch) against
  the flat `--view=cockpit --no-cockpit-pass` frame on the empty stage at `--direction=-0.743,0,
  -0.669`: phase correlation 0, 0 px, and 0.036 % of pixels over 32 levels (the flat HUD reticle).
- **Capture.** Under XR the root viewport's texture is a two-layer array: `GetImage()` returns layer 0
  (`TextureStorage::texture_2d_get`), and `RenderingServer.Texture2DLayerGet` reads either eye.
  `CaptureDirector` saves both side by side (default) or one eye (`shot=left|right`). Canvas layers
  draw nothing in stereo (`can_draw_2d` requires one view): no HUD, boards or cockpit pass (SHOT-44).
- **Determinism.** Two consecutive `RunProbe.ps1 --stage=empty --plane=player_bhawk "--pos=0,300,0"
  "--direction=-0.743,0,-0.669" --view=cockpit --no-cockpit-pass "--hold=0,0,0,0.6" --xr-sim --det
  --mute --frames=60 --screenshot=...` runs: `3fe1549cdeee62f088a18aeff3784c78` both (four runs in all,
  with and without the shader cache). The golden pose below: `cc3ee9a3916db3c79a8d007d6c5dc5b2` on
  three probe runs and the golden stage; frame 121 moves 59.24 % of its pixels, and `ipd=64` against
  63 moves 0.62 % of a chase frame, so the hash can fail.

**Approach (landed).** `--xr-sim[=spec]` (`Spec/XrSimSpec.cs`; tokens `yaw=`, `pitch=`, `head=x/y/z`,
`ipd=`, `eye=WxH`, `fov=`, `shot=both|left|right`, defaults the seated eye at the mount looking
ahead, 63 mm, two 640x720 eyes at 90° horizontal, side by side) boots `Launch/XrSimInterface.cs`
through `XrSession.Boot`, which builds the same `XROrigin3D`, `XRCamera3D` and `XrMount` origin law
as the OpenXR path; OpenXR wins when it is live, and the stand-in keeps the flat game's pacing
(`XrSession.PacesFrames`). The eye law is `Flight/Camera/XrSimEyes.cs`. Without the tag the stand-in
logs an error and flies flat rather than draw with no variants. `RunTests.ps1`, `RunProbe.ps1` and
`RunGame.ps1` set `GODOT_EDITOR_CUSTOM_FEATURES` around the one process start whose user args carry
`--xr-sim` (`HiddenDesktop.ps1`'s `Invoke-WithXrSimFeature`), so the golden runner needed no change.
The new golden is `c1-cockpit-xr-sim`: `--chapter=C1 --plane=player_bhawk --pos=-6350,300,-4144
--direction=-0.743,0,-0.669 --view=cockpit --no-cockpit-pass --hold=0,0,0,0.6 --xr-sim`, frame 120.

**How A3 and A4 take per-eye captures.** Always through `RunProbe.ps1` (or a manifest shot), never a
direct launch. One eye: `--xr-sim=shot=left` or `shot=right`; both at once: the default side-by-side
image, left eye in the left half. A headset-like pixel density: `eye=WxH` with `fov=`, whose centre
density is `W / (2 tan(fov/2))` px per radian (`eye=2732x2732,fov=100` is about 20 px per degree at
the centre; the default 640 at 90° is 5.6). A burst for jitter: `--shots=N` writes `_00`.. per
frame of the chosen eye. A3's rotated pose: `--pos=` at 0, 10 and 20 km with
`--direction=-0.743,0,-0.669`, `--view=cockpit --no-cockpit-pass` (the private cockpit pass does not
draw in stereo). A single-eye control uses `ipd=20` with the head moved onto that eye (A4's Verify),
never `ipd=0`, which is degenerate on a chapter world (A4's Traps). Each eye's
HUD is absent, so the shell-control mask is the bezel alone.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Two consecutive `--det --xr-sim --screenshot=` runs hash identically (above); the golden
`c1-cockpit-xr-sim` pinned with the 24 existing shots unmoved; units for the flag grammar, the eye law
and the project switch (`CSVM.Tests/XrSimTests.cs`).

**⚠ Traps.** A lens-distorted stand-in output would make per-eye precision measurements (A3) read
the distortion, not the jitter; the stand-in has none, and the window's blit is not what a capture
reads. A shell that keeps `GODOT_EDITOR_CUSTOM_FEATURES=csvm_xr_sim` set gives every later launch from
it the stereo variants and no shader cache; the scripts set it per process only.

**Verified.** <pending orchestrator run>

## A3 ❌ Precision near the eye: render-only floating origin, double-precision fallback

**Outcome: no render-only remedy exists in the stock single-precision engine.** The VR jitter is
measured and is worse than flat. Candidate (a) is measured to change nothing, (b) is not render-only
in this tree, and the double-precision path is written up below and not started. **Ruling (Decision
17): the author accepts the shimmer as a known issue and the plan continues on the stock engine;**
the double-precision build and a game-wide floating origin are later work outside this plan.

**Goal.** In VR, the cockpit interior, a HUD quad and the plane in rigid chase show sub-pixel jitter
per eye at 20 km from the chapter origin, with simulation, physics and network still in chapter
coordinates. Or, if that cannot be done, a documented decision to move to a double-precision Godot
build.

**Evidence (confidence: measured for the jitter and for candidate (a); traced to the Godot
4.7-stable source for where the rounding happens; traced to this tree for the cost of (b)).**

- **The baseline, per eye at headset density.** `RunProbe.ps1 --chapter=C1 --plane=player_bhawk
  "--pos=X,300,0" "--direction=-0.743,0,-0.669" --view=cockpit --no-cockpit-pass "--hold=0,0,0,0.6"
  "--xr-sim=eye=2732x2732,fov=100,shot=left|right" --frames=60 --shots=6 --screenshot=...`, with X
  at 0, 10000 and 20000. That is 20.0 px per degree at the centre, so 1 px is 3.0 arcmin. Each
  region is registered frame to frame by subpixel phase correlation: the six dials on a bezel ring
  with the needle masked out, and the canopy strut, a canopy frame corner and the dash wood on a
  Hann-windowed crop. The table gives the worst step over five consecutive pairs. The far-world
  control (the cloud deck ahead) gives its worst deviation from the burst's mean step, since the
  flight's own motion is a steady step there.

  | Region | 0 km L | 0 km R | 10 km L | 10 km R | 20 km L | 20 km R |
  |---|---|---|---|---|---|---|
  | Worst dial | 0.22 px (0.7') | 0.16 px (0.5') | 5.06 px (15.2') | 5.41 px (16.2') | 7.23 px (21.7') | 5.21 px (15.6') |
  | Worst shell (strut, frame, dash) | 0.09 px (0.3') | 0.18 px (0.5') | 3.55 px (10.6') | 3.64 px (10.9') | 8.03 px (24.1') | 7.67 px (23.0') |
  | Far-world control | 0.05 px | 0.09 px | 0.05 px | 0.08 px | 0.04 px | 0.10 px |

  The growth with distance reproduces the flat measurement (⚠ table row 1: 0.02, 0.7 and 2.7 px
  at 13.3 px per degree, or 0.1', 3.2' and 12.2'), and in angle it is two to five times larger.
  The eyes jitter differently: at 20 km the rockets dial steps 7.2 px in the left eye and 1.5 px in
  the right, which is a disparity jitter as well as a positional one. The shell moves as much as
  the dials, where the flat C20 capture held the struts at 0.004 px. The interior now jumps as a
  block, so the eye itself rounds.
- **Chase is not affected.** The same capture in `--view=chase --frames=120` registers the plane
  at 0.11 px at 0 km and 0.13 px at 20 km, with a far-world control of 0.08 and 0.07 px. A
  millimetre at chase distance is a small fraction of a pixel. A HUD quad at 1.5-2 m would sit
  between the two: scaling the interior's 8 px at about 0.45 m by distance gives roughly 2-2.5 px,
  an estimate and not a capture.
- **Where the rounding happens (Godot 4.7-stable).** Every step that places an eye composes
  chapter-scale coordinates in float32:
  - `Node3D` composes each mesh's global transform on the CPU. Each dial rounds its own
    translation, which is C20's per-dial jitter.
  - `OpenXRInterface::get_transform_for_view` (and the stand-in's `XrSimEyes.View`) returns
    `origin * reference * eye`, so the eye position rounds at the origin's magnitude.
  - `RendererSceneRender::CameraData::set_multiview_camera` builds the combined camera by
    intersecting the two eyes' frustum planes in world space. The planes' distances are about
    20,000, so its origin carries several ulps of error. It then stores each eye as
    `main_inv * eye`, a second subtraction of world-scale values.
  - `scene_forward_clustered.glsl` forms `MODELVIEW_MATRIX = view * model` on the GPU in float32.
    Under `USE_DOUBLE_PRECISION` it instead adds the two translations with an emulated double
    (`double_add_vec3` with `model_precision` and `inv_view_precision`).

  The camera-side steps explain the block motion that flat does not show. They also rule out
  every fix that only moves the near subtree: the eyes are wrong by about a millimetre before the
  interior is drawn at all.
- **Candidate (a), the interior placed in the XR origin's own frame, is measured to change
  nothing.** A scratch build re-parented `cockpit1` under the `XROrigin3D` with only its mount
  basis. Its bursts at 0 and 20 km, both eyes, were bit-identical to the baseline, pixel for
  pixel. The reason is that `XrMount` already puts the origin at the flat eye. That eye is composed
  exactly as the interior's mount is: the same plane transform times the `cockpit_camera` offset.
  The interior and the origin therefore already share their float values. What is left is each
  child mesh's own rounding and the camera-side rounding, and neither is the interior root's.
- **Candidate (b), a render-side origin shift, is not render-only in this tree.** Godot 4.7 has no
  render origin. A visual instance's transform is its node's global transform, so a shift has to
  move a scene subtree. Physics cannot be kept out of that subtree:
  - Colliders are `StaticBody3D` children of the meshes they collide for (`Mech3/SceneBuilder.cs`
    `body = new StaticBody3D`, clutter regions, `MapEdgeExtender`) and of each aircraft
    (`Flight/Airframe/AircraftBody.cs`), so the shift moves the physics space with the visuals.
  - Outside the suites, the game reads or writes node world transforms on about 320 lines in 80
    files, and touches physics bodies and queries on about 115 lines in 24 files
    (`Flight/Weapons/Projectile.cs` alone has 24). All of them work in chapter coordinates.

  So the shift is a game-wide floating origin, with physics, sim reads and the network boundary
  translated, and rebasing every static collider whenever the origin moves. That is the
  "touches sim or physics" case Decision 4 sends to the fallback. If the re-plan takes it anyway,
  the answer to the shift question is the whole session root. The skydome and the cloud deck then
  follow it for free, since `PlayerRig.Horizon` and `Deck` are re-centred on the rig camera's
  position every frame.
- **A near pass drawn from the game's own eye pose was considered and rejected.** That candidate
  draws the near subtree with a custom vertex path, from small origin-relative transforms and an
  eye pose the game supplies. Under the stand-in it would be exact, because the head is pinned.
  Under OpenXR the eye poses the frame draws with are located on the render thread
  (`OpenXRAPI::pre_render` calls `xrLocateViews`), and `get_view_transform` refuses any other
  thread (`ERR_NOT_ON_RENDER_THREAD`). The main thread's `XRCamera3D` pose is an earlier
  prediction, so the cockpit would swim against the world during head motion by the difference.
  Only a headset could judge that, and every interior mesh's transform would have to be re-supplied
  each frame.
- **0 km is the bound a precise path reaches**: 0.22 px (0.7') worst on the dials, which carries
  the gauges' own motion, and 0.18 px on the shell.

**The double-precision path, written up and not started.** It fixes the class outright: every CPU
transform above becomes `double`, and the GPU's model-view translation goes through the emulated
add. It takes these pieces:

1. **The engine.** The Godot 4.7-stable editor built with
   `scons platform=windows target=editor precision=double module_mono_enabled=yes`, then
   `--headless --generate-mono-glue modules/mono/glue`, then
   `modules/mono/build_scripts/build_assemblies.py --godot-output-dir=bin --precision=double
   --push-nupkgs-local <feed>`. `tools/godot/` then holds the custom build in place of the
   downloaded one. A local NuGet feed carries `Godot.NET.Sdk` and `GodotSharp` built
   `REAL_T_IS_DOUBLE`, and the project restores from it.
2. **The export templates.** Windows and Linux, `target=template_release` (and `template_debug`)
   with `precision=double module_mono_enabled=yes`. Linux is included because the C# assembly is
   compiled against one precision and ships to both platforms, so the flat Linux build goes double
   too. The Linux toolchain is the one `ExportRelease.ps1 -Linux` already uses.
3. **The C# port.** Under `REAL_T_IS_DOUBLE` every `Vector3`, `Basis` and `Transform3D` component
   is `double`, and every implicit narrowing to `float` stops compiling. The tree has about 9,000
   lines that name `float` in about 990 C# files. Most of them read a vector component, so this is
   the largest single piece of work. The engine-free sim math has to choose whether to stay `float`
   (it is chapter-scale and deterministic today) or follow the engine.
4. **WebRTC.** The GDExtension is built against godot-cpp with `precision=double` (godot-cpp names
   those libraries `.double`) in place of the official single-precision release `InstallWebRtc.ps1`
   pins. A single-precision extension does not load into a double engine.
5. **Goldens and suites.** Every golden is re-pinned, the 24 flat shots and `c1-cockpit-xr-sim`.
   Every `--det` capture changes at world scale, and physics results can differ in the last bits,
   so the engine suites' hit and contact expectations are re-run. This breaks the boundary rule
   for the flat game by construction, so the switch is the author's decision on its own merits,
   not a VR item.
6. **Shader caveats.** The emulated add covers `MODELVIEW_MATRIX`, which the world shaders use
   under `skip_vertex_transform` (`SceneBuilder.cs`'s `VERTEX = (MODELVIEW_MATRIX * ...)`). Any
   shader that rebuilds the model-view from `VIEW_MATRIX * MODEL_MATRIX` itself bypasses it, as
   the billboard path does (`MODELVIEW_MATRIX = VIEW_MATRIX * mat4(...)`). Those are far-world
   sprites today, but a near one would need the same care. Multimesh instances under
   `skip_vertex_transform` also skip the emulation (`scene_forward_clustered.glsl`, the
   `sc_multimesh` branch).
7. **Verification.** This item's instrument at 20 km should then read near the 0 km row.

**Approach (what was done).** Measured the baseline per eye, read the renderer's XR path, built
(a) as a scratch change and measured it, sized (b) against the tree, and wrote up the fallback.
The scratch change is reverted. The instruments are `.scratch/A3/a3_reg.py` (subpixel phase
correlation by an upsampled DFT, bezel rings with the needle masked, checked against synthetic
shifts of 0.04-3.6 px to 0.01 px) and `a3_table.py` (the table above, right eye registered with the
panel's L-to-R offset). The fetched engine sources are in `.scratch/A3-src/`. A worktree loses its
`.scratch` on removal, so the table above is the record.

**Model recommendation.** Measurement and source reading; closed without code. The re-plan
(engine build, C# port) is a High-tier item.

**Verify.** Goldens stage on the reverted tree: 25 of 25 hash-identical, `c1-cockpit-xr-sim`
included (`RunTests.ps1 -SkipUnits -SkipEngine -SkipHitch`). The complete battery was not run
here, because a sibling item shared the hidden desktop.

**⚠ Traps.** A heading-0 pin cannot show the effect (⚠ table row 1). A luminance centroid moves with
shading; use phase correlation on a bezel-only mask. ⚠ Do not judge a near-subtree fix by its
shell regions alone. Under multiview the shell jumps with the dials, because the eyes round, so a fix
that only steadies the dials against the shell has not steadied either against the eye. ⚠ A
re-parent that keeps the same float operations (the interior under the XR origin) renders
bit-identical frames. Check a candidate with a pixel diff against the baseline before measuring it.

**Verified.** <pending orchestrator run>

## A4 ☑ Screen-space effects under multiview

**Goal.** A per-effect verdict on how each screen-space shader behaves in stereo: correct as is,
portable to `VIEW_INDEX`, or impossible, with the porting cost named.

**Evidence (confidence: traced to code in the Godot 4.7-stable source for the compiler rewrite and
the matrices; measured per eye on the stand-in for every effect but the race ghost, whose verdict
is arithmetic).**
- **Godot rewrites the screen-reading samples itself.** For a spatial shader the forward-clustered
  compiler sets `check_multiview_samplers` (`scene_shader_forward_clustered.cpp`), and
  `shader_compiler.cpp` turns every `texture()`-family call on a `hint_screen_texture`,
  `hint_depth_texture` or `hint_normal_roughness_texture` uniform (or a function argument carrying
  one) into a `multiviewSampler` read at `multiview_uv(uv)`, which the scene shader defines as
  `vec3(uv, ViewIndex)` under `USE_MULTIVIEW` and as the plain `uv` otherwise. So the same shader
  text reads its own eye's layer in stereo and the flat texture in mono, and no game shader needs
  `VIEW_INDEX` for these reads.
- **The other built-ins, per view.** `SCREEN_UV` is `gl_FragCoord.xy * screen_pixel_size`, the
  eye's own layer. `PROJECTION_MATRIX` and `INV_PROJECTION_MATRIX` are
  `projection_matrix_view[ViewIndex]`, each eye's projection times its offset from the combined
  camera (`RendererSceneRender::CameraData::set_multiview_camera`). `VIEW_MATRIX`,
  `INV_VIEW_MATRIX`, `VERTEX` and `CAMERA_POSITION_WORLD` stay in that one combined frame, whose
  origin is the apex of a frustum enclosing both eyes, behind them: 31.5 mm behind the eye midpoint
  for the stand-in's default 90° eyes, 118 mm at `fov=30`. Godot's documentation calls it the
  midpoint; the source does not. A soft-particle compare of `VERTEX.z` against a depth unprojected
  through `INV_PROJECTION_MATRIX` therefore stays in one frame and is correct in both eyes.
- **Measured per eye.** Each stereo eye was compared with a capture whose own view stands exactly
  where that eye stood (Verify). Across five poses every eye matched its truth to 0.064 % of pixels
  or less over 2 levels, against 1.2 to 23.5 % for the opposite eye's truth. Every stereo run's
  engine log is free of shader errors.

| Effect | File (member) | What it reads | Per eye, observed | Verdict | Port cost |
|---|---|---|---|---|---|
| Soft particles | `Effects/EmitterRenderer.cs` (`ShaderCode`, `SOFT_EXPR`) | depth texture at `SCREEN_UV`, `INV_PROJECTION_MATRIX`, `VERTEX.z` | rocket, crash and lake poses: each eye matches its truth (0.003 to 0.064 % over 2 levels) | correct as is | none |
| Gamma-space mix composite | `Effects/EmitterRenderer.cs` (`GammaMixComposite`) | screen texture at `SCREEN_UV` | the mixed smoke and the crash plume match per eye; a wrong-layer or stale copy would differ by the parallax (23.5 %) | correct as is | none; the colour copy is made per eye (D18) |
| Fog-volume soft fade (Enhanced) | `Effects/FogVolumeClutter.cs` (`EnhancedGradeTemplate`) | the same depth term over 12 m | deckfly pose, plane inside the C1 deck: 0.21 to 0.27 % in the box around the plane against 9.3 % cross | correct as is | none |
| Heat shimmer (Enhanced) | `Effects/HeatShimmer.cs` (`ShaderCode`) | screen texture at `SCREEN_UV` + offset; `POSITION` from `PROJECTION_MATRIX` | rocket pose shimmer box: 0.052 / 0.059 % against 9.0 % cross; the noise is in quad UV, so it sits at the quad's depth | correct as is | none |
| Clutter fade dither | `shaders/csky_clutter_fade.gdshaderinc` (`csky_clutter_dither_keep`), `Mech3/Clutter.cs`, `Mech3/SceneBuilder.cs` clutter variant | `FRAGCOORD.xy` 4x4 ordered dither | identical screen phase in both eyes (`dither-crop.png`). The pattern carries no disparity while the card carries `IPD x f / d`: at about 20 px per degree (1146 px/rad) that is 1.4 px at the nearest authored fade start (50 m) and 0.24 px at C1 firs' 300 m | keep; reads at the card's depth within a pixel. The author's eye at E21 decides | none |
| Race ghost dither | `Mech3/RaceGhost.cs` (`FragmentLine`) through the same keep | the same `FRAGCOORD` dither over a whole aircraft, 35 % (55 % Enhanced) within 40 m, solid by 80 m | not capturable (Traps); arithmetic: 1.8 px at 40 m, 7 px at 10 m, a screen-door that stays on the display and fuses at infinity | port (D17) | small: a VR-only text variant that offsets `FRAGCOORD` by the instance origin's projected pixel per view (one flat `vec2` varying, one projection in `vertex()`), so the pattern carries the aircraft's disparity |
| Rain and snow, wind streaks | `Effects/Precipitation.cs`, `Effects/WindStreaks.cs` | `POSITION = PROJECTION_MATRIX * VIEW_MATRIX * world`, centre from `CAMERA_POSITION_WORLD` | rain pose: max 2 levels per eye against 2.75 % cross; streaks in the rocket pose | correct as is | none |
| Billboards facing the eye | `EmitterRenderer`, `FogVolumeClutter`, `Clutter`, `SceneBuilder` billboard and cylindrical shaders, `PlaneBuilder` flare | `CAMERA_POSITION_WORLD`, the combined basis | one world quad seen by both eyes, as Godot's own billboard does under multiview | correct as is | none |
| Point-light sprites | `Mech3/SceneBuilder.cs` (`LightShaderCode`) | `PROJECTION_MATRIX[1][1]`, `VIEWPORT_SIZE` | the same in both eyes (symmetric vertical term, per-eye viewport) | correct as is | none |
| Depth bias | `Mech3/SceneBuilder.cs` (`VERTEX *= 1.0 - (depth_bias + node_bias)`) | `VERTEX` in the combined frame | world materials: the bias is at most 0.0098 (49 levels), a disparity error of `b x IPD / d`, 0.7 px at 1 m and 0.07 px at 10 m at 20 px per degree, and a depth pull of 1 % of distance, below stereo acuity | world: correct enough as is; the interior (25x) is B8's | B8 |
| Fog, world light, view table | `shaders/csky_atmosphere.gdshaderinc` | `CAMERA_POSITION_WORLD` | the combined camera sits a few cm from the flat camera; the view table only matters across zones, and one VR seat publishes one zone per rig | correct as is | none |
| Sky dome, cloud deck, ground shadow, cloud puffs | `WorldBuilder.BuildHorizon`, `WeatherRig`, `GroundShadowPass`, `csky_cloud_puffs.gdshaderinc` | world geometry, km-scale dome | no screen or depth read | correct as is | none |
| Engine passes (Enhanced) | `Launch/EnhancedLook.cs` (SSAO, SSR, glow, TAA) | Godot's own multiview paths | rocket and lake poses (SSR water) match per eye | correct as is | none here; cost is D18's |

Not drawn in stereo, so not this item's: every canvas layer (lens flare, sun wash, whiteout,
`ScreenFlash`, fades) is C14's, the cockpit pass B8's, the spyglass picture D19's. `UI/Labs/MeshLab.cs`
is a flat-only lab. No ocean shader exists on this branch; one landed later needs the same review.

**Approach (investigation, no code).** Read the Godot 4.7-stable compiler, scene shader and camera
sources (`.scratch/A4-src`), sweep every generated and shipped shader for screen, depth, projection
and view-space reads, and capture each effect per eye under A2's stand-in with a per-eye truth. D17
inherits one port, the race ghost's dither. The headset look is the author's (E21).

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Per pose, three `RunProbe.ps1` captures at frame 120 (scratch scripts
`.scratch/A4/a4_triple.ps1` and `a4_eyes.py`): the stereo pair (`--xr-sim=pitch=P,eye=960x960,
fov=F`), and two truths at `ipd=20` with the head moved to `head=-0.0215/0/0` (`shot=left`) and
`head=0.0215/0/0` (`shot=right`), so the saved view stands exactly where the stereo eye stood. Correct
per eye means the stereo eye matches its truth within the residual of the combined camera's few cm
(billboard facing, fog distance, TAA); a wrong-eye or stale layer read would reach the difference
against the opposite eye's truth. The poses, with stereo-vs-truth then cross, as the share of pixels
over 2 levels:
- `rocket`: `--chapter=C1 --plane=player_bhawk --pos=-6104,300,-4420 --direction=0,-0.33,0.94
  --fire-rockets --hold=0,0,0,0.3 --graphics=enhanced`, `pitch=-22,fov=30`: 0.064 / 0.060 % against
  10.9 %; the shimmer box 0.052 / 0.059 % against 9.0 %.
- `crash`: `--chapter=C1 --plane=player_bhawk --crash=5 --hold=0,0,0,0.6`, `pitch=-15,fov=70`:
  0.003 / 0.003 % against 23.5 %.
- `deckfly`: `--chapter=C1 --plane=player_bhawk --pos=-6144,1170,-6144 --direction=0,0,-1
  --hold=0,0,0,0.6 --graphics=enhanced`, `pitch=-12,fov=50`: 0.046 / 0.058 % against 2.0 %.
- `lake`: `--freecam --chapter=C1 --pos=-7620,140,-3240 --direction=-0.765,0,-0.645
  --graphics=enhanced`, `pitch=-17,fov=70`: 0.002 / 0.004 % against 1.2 %.
- `rain`: `--freecam --chapter=C1C --pos=-3843,700,-1101 --direction=-0.052,0,-0.999`,
  `pitch=-14,fov=70`: at most 2 levels against 2.75 %.
- The dither: `--freecam --chapter=C1 --pos=-7400,250,-3000 --direction=-0.4,0,-0.9`,
  `--xr-sim=eye=960x960,fov=40,pitch=-12`, a fir in its fade band cropped from both eyes.
All with `--det --mute --frames=120`. Flat goldens unmoved: no code changed.

**⚠ Traps.**
- **Do not port these reads by hand.** A `sampler2DArray` or an explicit `VIEW_INDEX` layer in the
  shader text would break the flat twin, which Godot compiles without `USE_MULTIVIEW`; the compiler
  already picks the layer.
- **`CAMERA_POSITION_WORLD` and `VERTEX` live in the combined frame behind the eyes, not at either
  eye.** A shader that needs the eye itself adds `EYE_OFFSET` (view space). Nothing in the game needs
  it today.
- **The stand-in's `ipd=0` is degenerate.** `set_multiview_camera` cannot build the combined
  frustum from two coincident eyes: the log fills with `p_right <= p_left`, culling breaks and whole
  C1 terrain tiles vanish. At 1 to 4 mm the light culler still logs `create_frustum_points` `!res`
  under the 40 km far plane (and 4 mm left a freecam frame flat grey). A single-eye control takes
  `ipd=20` and moves the head by the remaining half IPD, as Verify does. A2's empty-stage control
  at `ipd=0` happened to survive; a chapter world does not.
- **A dither cannot be judged by the truth comparison**, which is screen-locked on both sides; its
  verdict is the disparity arithmetic and the author's eye.
- **The race ghost cannot be captured on the stand-in.** It arms only between race pilots, and two
  local seats under `--xr-sim` leave the root viewport without a 3D camera (a flat grey frame,
  `ghost-try.png`). D17 checks it on a networked race or with a suite that arms the stamp.

**Verified.** <pending orchestrator run>

## A5 ☑ Cockpit scale per plane

**Goal.** For each of the 11 planes, a measured answer to whether `cockpit1` and the
`cockpit_camera` eye point are human-scale at 1:1, giving starting values for `data/vr_cockpits.json`.

**Evidence (confidence: the measurements are traced to the data; the proposal is
direction-sound, magnitude TUNE).**

- **The world and the hulls are already metres.** World units are metres (`docs/org/flightModel.md`,
  identified from `nom_gravity`'s 9.82 reference divisor), and the exterior `healthy` hulls built at
  scale 1 measure as aircraft: 5.6-26.0 m span, 6.1-17.9 m long (Bloodhawk 11.6 × 10.3 m). So the
  XR world scale has no reason to leave 1.0 on any plane.
- **The interior is NOT in metres, and 1:1 is not on offer.** `cockpit1` is authored in its own
  units (iu) with the eye at its origin looking down −Z, and `PlaneBuilder.InteriorScale` mounts it
  at 0.04 m/iu, a port TUNE chosen for the flat view, where the cockpit pass makes framing
  scale-invariant. At "1:1" the panel would sit 11 m from the eye. In VR the mount scale stops being
  free: stereo and head motion read it as the cockpit's real size.
- **No uniform scale maps the interior onto its own hull.** The scale that matches the interior's
  own wings to the exterior span runs 0.030 (Bloodhawk) to 0.078 (Balmoral); the Hoplite's interior
  has no wings. At 0.04 the Balmoral's interior wings read at half the hull's span.
- **No uniform scale makes the interior human either; its depth and width are distorted in
  opposite directions.** The eleven interiors share one instrument assembly (main dials 2.0 iu
  across, centred 10.6-12.7 iu from the eye) inside tubs 22-39 iu wide. Each human reference
  implies its own scale: the dials want 0.040 on every plane, the headroom 0.027-0.043, the panel
  distance 0.058-0.093 (the panel is pulled in toward the eye) and the tub width 0.016-0.028 (the tub
  is widened). The 80° flat view these were framed for explains both: a near panel fills the bottom
  of the picture and a wide tub keeps the rails out of it.
- **The median of the cues lands on the shipped mount.** Per plane the four cues spread 3-4×, while
  their medians across planes stay within 0.033-0.041, and 0.040 is within 20% of every one. A
  per-plane deviation smaller than the disagreement inside one plane is not a measurement, so the
  proposal is 0.040 for all eleven, with a first alternative named where the median sits low.
- **A flat head-and-shoulders plate sits just behind the eye** in 10 of 11 interiors (all but the
  Warhawk), 0.16-1.8 iu aft, about 9 iu wide at the head and 20 iu at the shoulders, under
  `structure` (`g366`, `canopy2`, …). At 0.04 it is 0.6-7 cm behind the eye, where a real head
  extends about 0.18 m. Whether it is a headrest or the pilot's own silhouette, and whether it
  faces the eye, is not settled here; B8 should check it when a VR head turns round.
- **The eye sits on the sight line, and that pins the offset.** `PlaneBuilder` mounts the interior
  at the −4.70° head-pitch tilt so the gunsight lies on the guns from the origin. Any lateral or
  vertical eye offset puts parallax between the sight ring and the pipper; an offset along the
  interior's own Z axis does not. The Kestrel's and Peacemaker's eyes sit 1.8-2 iu off their tub's
  centre, which is authored and left alone for the same reason.
- **`instruments.zrd` is not the panel's geometry.** It lists the seven gauge assemblies at
  z −17.5 and x ±9; the built meshes put the main dials at z ≈ −10.6 and x ±5.0. The archived
  cockpit-view plan's "0.04 puts the panel ~0.7 m ahead" came from the zrd; the meshes give 0.42-0.51 m.

Measured interior (iu, eye at origin; metres at the shipped 0.04 in brackets). Dials: main dial
backplate width. Eye→dials: eye to the altimeter/speedometer centres. Width: wall to wall 8 iu below
the eye at the eye station. Rim: top of the side wall below the eye. Headroom: lowest overhead
canopy member at z 0 or +6. Plate: the head-and-shoulders plate's distance aft of the eye.

| Plane | `cockpit_camera` (m) | Dials | Eye→dials | Width | Rim below eye | Headroom | Floor below eye | Plate aft |
|---|---|---|---|---|---|---|---|---|
| Bloodhawk | (0, 0.75, −0.20) | 2.0 (80 mm) | 11.4 (0.46) | 28.6 (1.14) | 6.4 (0.26) | 5.9 (0.24) | 23.8 (0.95) | 0.80 (0.03) |
| Devastator | (0, 0.75, −0.20) | 2.0 (80 mm) | 11.4 (0.46) | 28.7 (1.15) | 6.3 (0.25) | 5.9 (0.24) | 25.9 (1.04) | 0.80 (0.03) |
| Fury | (0, 0.75, −0.20) | 2.0 (80 mm) | 10.8 (0.43) | 27.8 (1.11) | 3.8 (0.15) | 6.9 (0.28) | 25.4 (1.02) | 1.30 (0.05) |
| Warhawk | (0, 2.31, 3.30) | 2.0 (80 mm) | 7.9 (0.32) | 29.8 (1.19) | 7.4 (0.30) | none | 36.4 (1.46) | none |
| Hoplite | (0, 0.75, −0.20) | 2.0 (80 mm) | 11.5 (0.46) | 39.2 (1.57) | 5.3 (0.21) | 7.7 (0.31) | 19.6 (0.78) | 0.80 (0.03) |
| Hellhound | (0, 0.75, −0.20) | 2.0 (80 mm) | 11.4 (0.46) | 30.5 (1.22) | 3.6 (0.14) | 8.9 (0.36) | 25.9 (1.04) | 0.80 (0.03) |
| Balmoral | (0, 0.70, −0.34) | 2.0 (80 mm) | 11.4 (0.46) | 35.9 (1.44) | 6.9 (0.27) | 8.1 (0.32) | 33.8 (1.35) | 0.32 (0.01) |
| Brigand | (0, 0.83, 0.72) | 2.0 (80 mm) | 11.4 (0.46) | 28.8 (1.15) | 6.7 (0.27) | 6.8 (0.27) | 26.2 (1.05) | 0.40 (0.02) |
| Firebrand | (−0.08, 1.17, −0.91) | 2.0 (80 mm) | 11.1 (0.44) | 30.9 (1.24) | 5.2 (0.21) | 7.1 (0.28) | 24.5 (0.98) | 0.89 (0.04) |
| Kestrel | (1.98, 0.75, −1.21) | 2.0 (80 mm) | 12.7 (0.51) | 21.7 (0.87) | 7.7 (0.31) | 5.6 (0.22) | 27.0 (1.08) | 0.16 (0.01) |
| Peacemaker | (−1.80, 0.95, −0.44) | 2.0 (80 mm) | 10.6 (0.42) | 22.3 (0.89) | 5.7 (0.23) | open | 26.1 (1.04) | 1.80 (0.07) |

Implied scale per cue (reference metres ÷ measured iu) and the proposal. "Hull" is the scale that
matches the interior's own wings to the exterior span, given for comparison, not as a cue. The
proposed interior scale is the `cockpit1` mount under VR; the XR world scale is 1.0 on every row.
The eye offset is in the interior mount's own frame (+Z aft along the sight line).

| Plane | Dials | Eye→dials | Width | Headroom | Median | Hull | Proposed interior scale | Eye offset (m) | Reasoning |
|---|---|---|---|---|---|---|---|---|---|
| Bloodhawk | 0.040 | 0.065 | 0.021 | 0.041 | 0.040 | 0.030 | 0.040 | (0, 0, 0) | Median on the mount. |
| Devastator | 0.040 | 0.065 | 0.021 | 0.041 | 0.040 | 0.048 | 0.040 | (0, 0, 0) | Median on the mount. |
| Fury | 0.040 | 0.068 | 0.022 | 0.035 | 0.037 | 0.040 | 0.040 | (0, 0, 0) | Within 8%. |
| Warhawk | 0.040 | 0.093 | 0.020 | - | 0.040 | 0.052 | 0.040 | (0, 0, +0.06) | Dials at 0.32 m, under MIL-STD-1472F's 0.33 m floor; 6 cm aft gives 0.37 m and leaves 0.21 m to the seat back (6.8 iu). No plate in the way. |
| Hoplite | 0.040 | 0.064 | 0.016 | 0.031 | 0.035 | - | 0.040 | (0, 0, 0) | Open tub 1.57 m wide; try 0.035 first if it reads big. |
| Hellhound | 0.040 | 0.065 | 0.020 | 0.027 | 0.033 | 0.034 | 0.040 | (0, 0, 0) | Lowest median; try 0.035 first if it reads big. |
| Balmoral | 0.040 | 0.065 | 0.017 | 0.030 | 0.035 | 0.078 | 0.040 | (0, 0, 0) | Tub 1.44 m wide; try 0.035 first. Its interior wings are half the hull's whatever the mount. |
| Brigand | 0.040 | 0.065 | 0.021 | 0.035 | 0.038 | 0.042 | 0.040 | (0, 0, 0) | Within 7%. |
| Firebrand | 0.040 | 0.066 | 0.020 | 0.034 | 0.037 | 0.040 | 0.040 | (0, 0, 0) | Within 8%. |
| Kestrel | 0.040 | 0.058 | 0.028 | 0.043 | 0.041 | 0.039 | 0.040 | (0, 0, 0) | Narrowest tub, closest to human. |
| Peacemaker | 0.040 | 0.070 | 0.027 | - | 0.040 | 0.033 | 0.040 | (0, 0, 0) | Open cockpit, median on the mount. |

The eye-offset alternative E20 should try first on every plane but the Warhawk is a forward move
(−Z) of 0.09-0.17 m: that clears a real head out of the plate but pulls the dials to the
0.33 m minimum, which is why it is not the default. Raising the interior scale instead to push the
panel out is the wrong lever: at the panel's own 0.065 the dials grow to 130 mm and the tub to 1.9 m.

**Approach.** Read `extracted/planes/nodes.json` and `models.json` with GameZ's own rules (flat
child indices, column basis or YXZ Euler, scale ignored, LOD range-min 0 only), accumulate each
`cockpit1` subtree in its own frame with the bullet-hole quads and inactive nodes left out, slice
the triangles through the eye (sections x=0, z=0, z=+6, y=−6), and read the dimensions off the
sections and the dial meshes. Compare with these references:

- Eye to instrument panel: 0.737 m nominal, 0.254-1.016 m range (FAA human factors design
  guidance, [hfcc.dot.gov chapter 2.3](https://hfcc.dot.gov/publications/docs/GeneralGuidance/zz_FAA_GeneralGuidanceDoc_Chapter_02_Section_03.pdf));
  at most 0.64 m (0.76 m with ejection seats), at least 0.33 m and preferably 0.51 m (MIL-STD-1472F,
  [denix.osd.mil](https://www.denix.osd.mil/soh/denix-files/sites/21/2016/03/02_MIL-STD-1472F-Human-Engineering.pdf)).
- Cockpit width 0.61 m (2 ft 0 in), length panel to seatback 0.99 m (3 ft 3 in), height seat to
  canopy 1.04 m (3 ft 5 in): P-51D Mustang, [AOPA](https://www.aopa.org/news-and-media/all-news/2007/august/01/north-american-aviation-p-51d-mustang).
  A 1940s fighter, the nearest era with a published cockpit box; the game's types are fictional, so
  the question is only whether a seated adult fits.
- Seated eye height about 0.80 m (ANSUR II male, [DTIC ADA611869](https://apps.dtic.mil/sti/tr/pdf/ADA611869.pdf)),
  so headroom above the eye is about 1.04 − 0.80 = 0.24 m; eye to back of head about 0.18 m
  (head length ≈ 0.19-0.20 m, same survey; not read off its table).
- Instrument dial: the 3-1/8 in (79 mm) standard instrument case.

**Model recommendation.** Measurement only; closed without code.

**Verify.** The scratch instruments are `.scratch/A5_measure.py` (node walk, group bboxes),
`A5_slices.py` (section plots, `A5_<plane>.png`) and `A5_table.py` (the tables above), run with the
system Python against `Z:\CSVM\extracted\planes`. The Kestrel's headroom was read off its z=+6
section by hand (the sloped canopy side tripped the automatic pick). Re-run: `python A5_table.py`
from `.scratch\`. The proposal is checked by the author at the headset (E20), not by a suite.

**⚠ Traps.** A measured number is a starting point; the decision is the author's at the headset
(Decision 13). ⚠ The per-plane scale belongs on the interior mount, not on `XRServer.world_scale`:
the world and the hulls are metres, so a world scale other than 1.0 resizes the sky, the terrain
and every other aircraft along with the cockpit. ⚠ Keep the eye offset on the interior's own Z
axis; a lateral or vertical one breaks the gunsight's alignment with the guns.

**Verified.** <pending orchestrator run>

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
`Camera.Position` (`PlayerRig.cs:49-62`, the anchoring loop in `GameSession`). A5 measured the world and the hulls as
metres and the interior as a separately scaled set piece, so the file's per-plane scale is best
applied to the `cockpit1` mount (`PlaneBuilder.InteriorScale`, 0.04 today) with
`XRServer.world_scale` left at 1.0, and the eye offset in the mount's own frame along its Z axis.

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

The first headset run (A1, VDXR, Bloodhawk, `--no-cockpit-pass`) showed two interior defects, both
traced to the data and the code:

- **A red "person cutout" straight ahead.** Ten interiors carry a flat head-and-shoulders plate on
  one z plane 0.16-1.80 iu aft of the eye (Bloodhawk: polygons #48-54 of `cockpit1/structure/g366`
  at z +0.80 iu, 3.2 cm at 0.04 m/iu, Colored `rgb(215,43,0)`, priority 5, normal aft, no
  `show_backface`; the Brigand's is grey, the Warhawk has none). From the authored eye it is behind
  the camera and back-facing, so the flat game never draws it; a tracked head more than a few cm
  aft of the eye sees its front face between the eye and the panel. It shares its node with canopy
  structure, so it cannot be dropped by node name. Its role in the original is undecoded.
- **Gauges at a different depth from the panel.** The interior's depth bias is a real view-space
  displacement, `VERTEX *= 1.0 - (depth_bias + node_bias)` (`Mech3/SceneBuilder.cs:2040`), scaled
  by `DepthBiasScale = 1/InteriorScale` = 25 (`Mech3/PlaneBuilder.cs:131`), 2e-4 per priority
  level, clamped at 0.25. In mono the pull lies along the eye ray and changes only depth order; in
  Godot 4.7 multiview `VERTEX` is in the combined view and scales toward a point behind the eyes,
  so the pull is a real stereo depth step. The authored dial faces are coplanar with the dashboard;
  the bias puts the main needles (priority 8-9) about 2 cm in front of it, the green missile lamps
  (30) about 7 cm, the gun and missile arrows (49) about 11.5 cm, and the compass bezel (-10)
  2.9 cm behind, at a 0.47-0.50 m panel.

**Approach.** Under VR take the `--no-cockpit-pass` path, keep `light_origin`, the shake and the
gauge drive working on the in-world interior. There is no precision fix (A3, Decision 17): the
interior shimmers far from the chapter origin and that is a known issue, not a B8 defect. Under VR
only: leave the
plate out of the interior build with a polygon filter (all vertices on one z plane with
0 < z ≤ 2.5 iu, normal aft, Colored, priority 5; on the diagnosis scan this matches exactly the 5-7
plate polygons per interior and nothing else), and give the interior a shader variant that keeps the
true position and biases depth only:
`vec4 c = PROJECTION_MATRIX * vec4(VERTEX, 1.0); vec4 p = PROJECTION_MATRIX * vec4(VERTEX * (1.0 - bias), 1.0); POSITION = vec4(c.xy, p.z / p.w * c.w, c.w);`
(`PROJECTION_MATRIX` is per view under multiview). The cheaper fallback, `DepthBiasScale = 1` under
VR, leaves about 0.5 cm on the arrows and needs a z-fighting check. The world materials carry the
same unscaled bias, negligible in stereo beyond a few metres.

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

**Evidence (confidence: traced to code and measured per eye, A4).** Soft particles, the
gamma-space mix composite, the fog-volume soft fade and heat shimmer are already correct in both
eyes: Godot 4.7's compiler turns every read of a `hint_screen_texture` or `hint_depth_texture`
uniform into a read of the drawing eye's layer, and `INV_PROJECTION_MATRIX` is per eye while
`VERTEX` stays in the combined frame, so the depth compare holds. Each stereo eye matched a
capture taken from that eye's own position to 0.064 % of pixels or less, against 1.2 to 23.5 % for
the other eye's. The clutter fade dither stays: its screen-locked pattern is off the card's
disparity by at most 1.4 px at the nearest authored fade start (50 m) at headset density. The one
port is the **race ghost's dither** (`Mech3/RaceGhost.cs`, `FragmentLine`), a 35 % (55 % Enhanced)
screen-locked 4x4 pattern over an aircraft within 40 to 80 m, 1.8 to 7 px off its disparity: a
screen-door fixed to the display.

**Approach.** Under VR only, give the ghost's keep test a pattern anchored to the aircraft: a
`varying flat vec2` set in `vertex()` to the pixel the instance origin projects to in this view
(`PROJECTION_MATRIX * VIEW_MATRIX * MODEL_MATRIX[3]`, scaled by `VIEWPORT_SIZE`, floored), and
`csky_clutter_dither_keep(FRAGCOORD.xy - anchor, alpha)`, so the pattern carries the aircraft's
disparity. VR is fixed for a launch (an Options apply relaunches), so the generator can read a
launch-time flag beside `GraphicsMode.Enhanced` and emit the anchored text only then, leaving the
flat text byte-identical. Alpha-to-coverage under the 4x MSAA is the alternative if the anchored
pattern still reads as a mesh. The clutter dither keeps its text unless the author's headset pass
(E21) finds it reads as a screen-door.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Flat goldens unmoved. The race ghost cannot be captured on the `--xr-sim` stand-in
(two local seats leave the root viewport without a 3D camera, A4 Traps), so the check is a suite
that arms the stamp on a single-seat stereo session, or a networked stunt race in the headset, with
the ghost's dither phase measured against the aircraft in each eye. A4's per-eye truth method
(`ipd=20` with the head moved onto the eye) re-checks the four effects that need no port.

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
The known-issues list carries at least the cockpit and HUD-panel shimmer far from the chapter origin
(Decision 17, A3's measurements).

**Approach.** `docs/cli.md` (`--xr-sim`, `--xr-mode`), `docs/controls.md` (XR bindings), the README
and the release notes; the shortcut through `ExportRelease.ps1`'s packaging. <TODO: the known-issues
list comes from E21.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** <TODO: the release zip checked in `RunSandbox.ps1` with VR off, to show the flat game is
untouched.>

**⚠ Traps.** <TODO: none known yet.>
