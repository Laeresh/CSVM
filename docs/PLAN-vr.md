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
- The rigid chase plane, the cockpit interior and the HUD panel are free of float32 shimmer far from
  the chapter origin: the chase plane by A2, the interior by A3b's eye feed, the panel by C12
  (0.010 px at 0 and 20 km). Decision 17's accepted shimmer no longer applies to either.
- 90 Hz on the reference rig, comfort basics (recenter, fades, optional vignette, render scale), and
  a headless `--xr-sim` stand-in that keeps the VR path inside the automated battery.
- No headset, no runtime, or the option off: the game is exactly the flat game it is today, goldens
  included.

**VR never changes the flat game.** OpenXR stays off in `project.godot`, every VR branch is gated on
the VR session being live (or `--xr-sim`), and the existing goldens must not move. A change that
needs the flat path altered to make VR work is its own item, argued on its own merits; the one such
change in this plan is the Enhanced flare (Decision 16, C14), adopted on the author's montage
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
| 17 | float32 precision near the eye, after A3 found no render-only fix | **Accept the shimmer and continue on the stock single-precision engine.** The cockpit interior and the HUD panel shimmer 5-8 px per eye at 10-20 km from the chapter origin at headset density; rigid chase is unaffected. It ships as a known issue of the experimental release. The double-precision build and a game-wide floating origin stay open as later work (A3's write-up), not items of this plan. Note: A3b draws the interior from the XR origin's frame with the render thread's own eyes and measures 0.04 px per eye at 20 km on the stand-in; the author's headset judgement is owed before the known issue is dropped. **Superseded for the cockpit by A3b:** at the headset the interior held steady at the C1 map centre and a far corner, in both render-thread modes, with no swim on head turns, so the cockpit shimmer is no known issue. The HUD panel (C12) and the near quads are to use A3b's pieces too. |

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
   ☑ A3b A precise VR cockpit built from small numbers, the flat cockpit pass's idea in stereo
4. ☑ A4 Screen-space effects under multiview
5. ☑ A5 Cockpit scale per plane

### Wave B, the VR rig

6. ◐ B6 VR mode option, relaunch, fallback and mode gating
7. ☑ B7 The XR rig in `PlayerRig`, recenter and per-plane scale data
8. ☑ B8 The cockpit interior in the main world under VR
9. ◐ B9 Views in VR: rigid chase, nose, head tracking in place of head look
   ◐ B10 Billboards and sky sprites face the eye and hold the world's up as the head turns

### Wave C, screen, HUD and input

10. ◐ C10 The virtual theater screen for menus, boards and cinemas
11. ◐ C11 `DeviceKind.Xr`, XR actions and the screen pointer
12. ◐ C12 The cockpit-locked HUD panel
13. ◐ C13 World-tied markers and the edge-marker ring
   ◐ C13b The gunsight reticle collimated at infinity in VR
14. ◐ C14 An angle-based world flare rig (VR and flat Enhanced); the other pane effects as near quads in VR

### Wave D, cutscenes, comfort and performance

15. ◐ D15 Cutscenes as a stereo 3D-cinema pair on the theater screen
16. ◐ D16 Comfort: fades, tunnel vignette, render-scale slider
17. ◐ D17 Port the screen-space effects to multiview
18. ◐ D18 Performance pass to the 90 Hz budget
19. ◐ D19 Spyglass as a binocular quad

### Wave E, headset pass and release

20. ◐ E20 Per-plane scale and eye-offset tuning at the headset
21. ☐ E21 At-the-headset playtests on both runtimes
22. ◐ E22 Experimental release: docs, shortcut, known issues

## Dependency and parallelism notes

Wave A gates everything. A1 blocks A2-A5 (they all need a booting XR session or its stand-in to
exist); A2, A3, A4 and A5 can then run in parallel, since they touch disjoint files (A2 new
`Tooling`/`Utils` stand-in code, A3 `PlayerRig`/`GameSession` anchoring, A4 the `Effects/` shaders,
A5 measurement only). A3 found no render-only fix, and the author chose to accept the cockpit
shimmer and continue on the stock engine (Decision 17), so Wave B follows Wave A unchanged. A3b
then drew the interior from the XR origin's frame on the stock engine (`Launch/XrSession.cs`,
`Flight/Hud/XrCockpit.cs`, `Launch/XrEyeFeed.cs`, the interior shader in `Mech3/SceneBuilder.cs`);
B7 builds on its contract, and B8 starts from what it already did.

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

**Verified.** The complete battery on the merged Wave A tree: build 0 warnings, units 6294 passed
of 6297, engine 527 passed, 2 skipped, goldens 25/25 hash-identical. That first run's engine stage
logged two `Uniform buffer supplied ... (128) is smaller than ... (144)` errors in one shard, the
first flat run after the author's headset session; the engine stage re-run was clean. Godot's
shader cache is `user://shader_cache`, shared by every worktree and session, so a headset run's
stereo variants reach them all once (B6 owns the fix).

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

**Verified.** The complete battery on the merged Wave A tree: build 0 warnings, units 6294 passed
of 6297, engine 527 passed, 2 skipped, goldens 25/25 hash-identical. That first run's engine stage
logged two `Uniform buffer supplied ... (128) is smaller than ... (144)` errors in one shard, the
first flat run after the author's headset session; the engine stage re-run was clean. Godot's
shader cache is `user://shader_cache`, shared by every worktree and session, so a headset run's
stereo variants reach them all once (B6 owns the fix).

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

**Verified.** The complete battery on the merged Wave A tree: build 0 warnings, units 6294 passed
of 6297, engine 527 passed, 2 skipped, goldens 25/25 hash-identical. That first run's engine stage
logged two `Uniform buffer supplied ... (128) is smaller than ... (144)` errors in one shard, the
first flat run after the author's headset session; the engine stage re-run was clean. Godot's
shader cache is `user://shader_cache`, shared by every worktree and session, so a headset run's
stereo variants reach them all once (B6 owns the fix).

## A3b ☑ A precise VR cockpit built from small numbers, the flat cockpit pass's idea in stereo

**At the headset (VDXR, Quest 3, the author):** at the C1 map centre and near a far corner, in both
render-thread modes, the gauges and canopy frame do not shimmer, the cockpit stays fixed against
the world on fast head turns and leans, and the needles sit on their dials.

**Outcome: candidate (i) works on the stand-in and at the headset.** The interior is
drawn in the main world from the XR origin's frame, as the flat pass draws it from the eye, and
each eye places it from the eye poses the render thread located for that same frame. A3's
instrument reads 0.04 px or less per eye at 0, 10 and 20 km, against A3's 5-8 px. Candidate (ii),
a stereo pass of its own, was not needed.

**Goal.** Answer the author's question, "And why can't we use the same technique like in flat?
Render the cockpit in the center?", with a VR-only interior that is sub-pixel per eye at 20 km from
the chapter origin, flat output and flat goldens unchanged. Every chapter's `area` runs from
-12288 (C3, C5: -16384) to 0 on x and z, so the chapter origin is a map corner, the map centre is
8.7 km (11.6 km) out, and almost all flying is in A3's bad range.

**Evidence (confidence: traced to the Godot 4.7-stable source for the timing, the shader
built-ins and the render-thread hook; measured on the stand-in for the precision and the
placement; direction sound, magnitude a judgement for the mismatch a game-thread head pose would
have carried).** The fetched sources are in `.scratch/A3b-src`, the instruments and captures in
`.scratch/A3b` (`a3b_burst.ps1`) and A3's `.scratch/A3`.

- **When the head and the eyes are sampled (step 1).**
  - The game thread's head. `Main::iteration` calls `XRServer::_process` before physics and
    `_process`. `OpenXRInterface::process` runs `OpenXRAPI::process`, which calls `xrWaitFrame`,
    hands its `predictedDisplayTime` to the render thread (`set_render_display_info`, a
    `call_on_render_thread`), and returns; `get_head_center` then locates the VIEW space at that
    same predicted time (`xrLocateSpace`). The `head` tracker's `set_pose` fires `pose_changed`,
    and the `XRCamera3D` takes the pose as its transform at once.
  - The render thread's eyes. `RenderingServerDefault::_draw` calls `XRServer::pre_render` first,
    and `OpenXRAPI::pre_render` calls `xrLocateViews` at `render_state.predicted_display_time`,
    the value from that same `xrWaitFrame`, then `xrBeginFrame`. `get_view_transform` refuses any
    other thread (`ERR_NOT_ON_RENDER_THREAD`).
  - `--render-thread safe`: one thread, in order: `xrWaitFrame`, head located, physics and
    `_process`, `RenderingServer::sync`, `draw`, eyes located. The same predicted time; the eyes
    are located later by the frame's game work.
  - `--render-thread separate`: `draw` queues `_draw` behind the queued display info, and `sync`
    first waits for the previous frame's draw to finish, so the render thread locates the eyes
    after the game work and that wait. The same predicted time from the same `xrWaitFrame`, not a
    frame apart; the gap is bounded in steady state by about one display period (11.1 ms at 90 Hz).
  - The mismatch a game-thread head would carry is the runtime refining one prediction: zero at a
    steady head rate, roughly `α·Δ·h` for an angular acceleration α, a gap Δ and the horizon h
    still left at the second call. At 3000°/s², 5 ms and 30 ms (a streamed headset predicts far
    ahead) that is 0.45°, about 9 px at 20 px per degree: the whole cockpit swimming against the
    world while the head speeds up or slows down. An estimate, not a measurement.
- **What a shader can reach.** `VIEW_INDEX`, `EYE_OFFSET` and a per-view `PROJECTION_MATRIX` are
  built-ins of the spatial vertex stage (`shader_types.cpp`), and under multiview the vertex stage
  runs once per view. `PROJECTION_MATRIX` is the eye's projection times the inverse of
  `view_offset`, the eye in the combined camera's frame, and `EYE_OFFSET` is that offset's
  translation (`set_multiview_camera`, `render_scene_data_rd.cpp`). Both are formed at world scale,
  so `EYE_OFFSET` carries the eyes' millimetre rounding and cannot give an eye's offset from the
  head. It is consistent with `PROJECTION_MATRIX`, though: `PROJECTION_MATRIX * (R·p + EYE_OFFSET)`,
  with R the combined frame's rotation into the eye's, is the eye's own projection of a point p
  given in the eye's frame, so the world-scale rounding cancels if the shader supplies p itself.
- **The render thread's eyes reach the shader in the same frame, so no head pose is needed.**
  `XRServer::pre_render` calls `pre_render` on every initialised interface in the order they were
  added, before the frame's culling, whose `update_dirty_instances` uploads the global shader
  uniforms (`_update_global_shader_uniforms`). A tracking-only C# `XRInterfaceExtension` added after
  the headset's interface therefore runs on the render thread right after `xrLocateViews` and can
  write the located eyes into global uniforms for that frame. A `RenderingServer` call made on the
  render thread executes directly, and the queue flush it attempts inside `_draw` is a no-op
  (`CommandQueueMT::_flush`'s thread-local `flushing`), so the next frame's queued commands cannot
  leak in. The OpenXR-specific hook, `OpenXRExtensionWrapper._on_pre_render`, is out of reach:
  `register_extension_wrapper` refuses once the OpenXR instance exists, before any game C# runs.
- **The measurement (step 3).** A3's instrument and pose: `--chapter=C1 --plane=player_bhawk
  --pos=X,300,0 --direction=-0.743,0,-0.669 --view=cockpit --no-cockpit-pass --hold=0,0,0,0.6
  --xr-sim=eye=2732x2732,fov=100,shot=left|right --frames=60 --shots=6`, worst frame-to-frame step
  in pixels (3.0 arcmin each), far-world control as A3 defines it.

  | Region | 0 km L | 0 km R | 10 km L | 10 km R | 20 km L | 20 km R |
  |---|---|---|---|---|---|---|
  | A3 worst dial | 0.22 | 0.16 | 5.06 | 5.41 | 7.23 | 5.21 |
  | A3 worst shell | 0.09 | 0.18 | 3.55 | 3.64 | 8.03 | 7.67 |
  | A3b worst dial | 0.036 | 0.030 | 0.036 | 0.030 | 0.036 | 0.030 |
  | A3b worst shell | 0.000 | 0.000 | 0.010 | 0.000 | 0.000 | 0.000 |
  | A3b far-world control | 0.05 | 0.08 | 0.05 | 0.09 | 0.04 | 0.09 |

  The A3b rows do not depend on distance. The 0.036 px is the speedometer's and the attitude
  ball's own motion through the bezel mask; the other four dials and every shell region register
  as identical frames. The panel's offset from the left eye to the right is -167.7 px at every
  distance, where A3's drifted from -174.5 to -177.2 px with the rounding and the depth bias.
  `--render-thread safe` renders the 20 km left burst bit-identical to `separate`. Enhanced reads
  0.19 px at 0 km and 0.01 px at 20 km; the 0.19 is frame-to-frame lighting noise on the dash
  (soft shadows), a brightness change with no displacement in the frames.
- **The placement is right, not merely steady.** On the empty stage the stand-in's left eye was put
  on the flat camera (`--xr-sim=eye=1280x720,fov=96.4268,ipd=20,head=0.01/0/0,shot=left,
  pitch=-4.70001`) and registered against the flat `--no-cockpit-pass` frame: every region within
  0.04 px at 0 km and at 20 km (`docs/verification.md` SHOT-45). A turned, offset head
  (`yaw=30,pitch=-12,head=0.05/0.03/0.10`) lines up with the old stereo path at the origin within
  0.13 px on the panel and the plate; the canopy struts differ by about 1 px, which is the old
  path's depth bias moving them in each eye (next bullet).
- **B8's gauge-depth defect is fixed on the way.** The interior's depth bias now pulls a vertex
  toward the eye drawing it, so its projected position stays put in both eyes; the old scale toward
  the combined camera put the needles and the lamps beside their dials in each eye
  (`.scratch/A3b/cmp_b0_p0_panel.png`). This and the precision move the two stand-in goldens; the
  24 flat goldens are hash-identical.
- **The engine-free arithmetic** is pinned by `CSVM.Tests/XrCockpitTests.cs`: at 20 km on a rotated
  heading, the shader's composition recovers a dial's eye-frame position within 20 µm, where
  Godot's own view-times-model composition misses it by more than 200 µm.

**Approach (landed, VR only).** Gated on `XrCockpit.Live` (the stereo path booted) and the interior
staying in the main world (`--no-cockpit-pass` until B8 makes that the VR default); a flat launch
builds nothing new and draws the same shader text.
- `Launch/XrEyeFeed.cs`, the tracking-only interface above. Its `_PreRender` writes
  `csky_xr_eye_left`, `csky_xr_eye_right` (each eye's offset from the XR origin in world axes, from
  the primary interface's `GetTransformForView(v, Identity)` and the render thread's
  `XRServer.WorldOrigin`) and `csky_xr_origin`. `XrSession.Build` adds it after the headset's or
  the stand-in's interface and declares the globals (`XrCockpit.DeclareEyeFeed`).
- `Flight/Hud/XrCockpit.cs`, per rig in `FirstPersonDressing.Stereo`. While the interior shows it
  is top level at the world origin with the XR origin's axes and its offset from the origin as the
  airframe holds it, composed from local transforms only (`InOrigin`: mount, wobble pivot and model
  root, minus the eye point). A custom AABB keeps it from being culled at the origin and a sorting
  offset keeps its glass after the world's. Hidden, it goes back under the airframe.
  `XrSession._Process` calls `Follow` right after placing the origin.
- `Mech3/SceneBuilder.cs`'s `XrPreciseInterior` variant (key bit 524288, set by `PlaneBuilder`'s
  `xrInterior` on the interior's builder alone). In a perspective pass it sets
  `VERTEX = mat3(VIEW_MATRIX) * (MODEL_MATRIX·v - eye) + EYE_OFFSET` and biases toward that eye; in
  the sun's orthographic shadow pass it draws the interior at its world place, unbiased. The point
  lights take `csky_xr_origin` in place of the cockpit pass's `light_origin`.

**B7's contract.** The XR origin stands at `XrCockpit.EyeInAircraft` on the aircraft with the
aircraft's attitude (the first-person arm of `XrMount.For`), and `Follow(origin, aircraft)` runs
each frame after the origin is placed, with the same transform. A per-plane seated eye offset
moves both together: add it to `XrMount`'s eye and to `EyeInAircraft` (a constructor argument).
A per-plane interior scale goes on the interior's mount before the `XrCockpit` is built, which
captures the mount. The eye feed stays added after the headset's interface, and `Live` stays set
before the first plane build. The HUD panel (C12), head-locked quads (C14, D16) and the spyglass
quad (D19) can take the same two pieces: a top-level node in the XR origin's frame and a shader
that subtracts the fed eye.

**Model recommendation.** The derivation took the High tier; the follow-ups (B7's contract, B8's
remainder, the same shader for C12) are Standard.

**Verify.** On the stand-in: the table above (`a3b_burst.ps1 -Tag p -Km 0,10,20`, then
`a3_table.py`), the flat-truth comparison, units (`XrCockpitTests`), the `cockpit` engine suites
and the goldens stage, where `c1-cockpit-xr-sim` re-pins and `c1-cockpit-xr-sim-far` (A3's 20 km
pose, both eyes) pins the precise path; the 24 flat shots are unmoved.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on','--render-thread','separate' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
.\RunGame.ps1 -EngineArgs '--xr-mode','on','--render-thread','safe' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
.\RunGame.ps1 -EngineArgs '--xr-mode','on','--render-thread','separate' --chapter=C1 --plane=player_bhawk "--pos=-11500,300,-11500" "--direction=-0.743,0,-0.669" --view=cockpit
.\RunGame.ps1 -EngineArgs '--xr-mode','on','--render-thread','safe' --chapter=C1 --plane=player_bhawk "--pos=-11500,300,-11500" "--direction=-0.743,0,-0.669" --view=cockpit
```
The first pose is the C1 map centre (8.7 km from the origin), the second a far corner (16.3 km).
Look for: (1) sitting still, the dials, needles and canopy frame hold steady with no shimmer at both
poses; (2) on fast head turns and leans, the cockpit stays fixed against the world rather than
swimming behind or ahead of it; (3) the needles and lamps sit on their dial faces at the panel's
depth. An interior with no depth of its own (both eyes alike) would mean the eye feed never ran.
B8 leaves the red plate behind the eye out and makes the stereo interior the default, so these runs
need no `--no-cockpit-pass`; before B8 the interior was culled at both poses (B8's Evidence). The first flat run after a headset session
prints one shader-cache error (B6 owns it).

**⚠ Traps.**
- Do not take an eye's offset from `EYE_OFFSET` differences or the combined camera's position:
  both are formed at world scale and carry the rounding this item removes.
- Do not place the stereo interior from the `XRCamera3D`'s pose: it is an earlier prediction of
  the same instant, and the difference shows as swim while the head accelerates.
- ⚠ Keep `XrEyeFeed` added after the headset's interface; added first, it reads the previous
  frame's eyes.
- While it shows, the interior's global transform is relative to the XR origin. A new reader of
  an interior node's world position must read it while hidden or go through the airframe.
- The shader tells the shadow pass by an orthographic projection. A shadowed omni or spot light on
  the interior would be drawn as if it were an eye; none casts on it today.
- Never `ipd=0` on a chapter world (A4's Traps).

**Verified.** The complete battery on the merged tree with A3b, B6 and C14 and the ShaderTwins
rewrite fix (LOG-26): build PASS, units 6348 passed 3 skipped, engine 533 passed 2 skipped with
clean engine errors, goldens 27/27 hash-identical. The headset checks named above are still owed.

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

**Verified.** The complete battery on the merged Wave A tree: build 0 warnings, units 6294 passed
of 6297, engine 527 passed, 2 skipped, goldens 25/25 hash-identical. That first run's engine stage
logged two `Uniform buffer supplied ... (128) is smaller than ... (144)` errors in one shard, the
first flat run after the author's headset session; the engine stage re-run was clean. Godot's
shader cache is `user://shader_cache`, shared by every worktree and session, so a headset run's
stereo variants reach them all once (B6 owns the fix).

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

**Verified.** The complete battery on the merged Wave A tree: build 0 warnings, units 6294 passed
of 6297, engine 527 passed, 2 skipped, goldens 25/25 hash-identical. That first run's engine stage
logged two `Uniform buffer supplied ... (128) is smaller than ... (144)` errors in one shard, the
first flat run after the author's headset session; the engine stage re-run was clean. Godot's
shader cache is `user://shader_cache`, shared by every worktree and session, so a headset run's
stereo variants reach them all once (B6 owns the fix).

# Wave B, the VR rig

## B6 ◐ VR mode option, relaunch, fallback and mode gating

**Goal.** Options carries "VR mode"; toggling it saves the choice and relaunches the executable with
`--xr-mode on`. With VR on, a failed runtime falls back to flat with a clear message, local
splitscreen is refused at setup, and the dev modes stay flat.

**Evidence (confidence: traced to code in the Godot 4.7-stable source for the engine-start rule and
the shader cache; measured in-engine for the argument source, the one-seat refusal under `--xr-sim`
and the flat goldens; lead-only for anything a headset shows until the author's run).**
- OpenXR and the stereo variants start only with the engine, so the saved option needs a new
  process. `RendererCompositor`'s constructor fixes `is_xr_enabled()` from `--xr-mode` before any
  script runs, and `OpenXRAPI` is created at module init. No 4.7 API turns either on later.
- A plain double-click gets OpenXR by a **relaunch at boot**: `Launcher._Ready` reads the saved
  `vrMode` right after the log opens and, with no `--xr-mode` on the command line, starts itself
  again with `--xr-mode on` first and quits. The child carries the flag, so its boot never
  relaunches again, and an explicit `--xr-mode off` keeps a launch flat by name. `override.cfg` and
  `application/config/project_settings_override` were rejected for the reason A2 gives: both are
  read by every launch, the battery's included.
- **The child's arguments** are the process's own (`System.Environment.GetCommandLineArgs()`, the
  only list that still holds Godot's engine arguments, A1) with the program name dropped and every
  argument carried verbatim, engine and user alike: a dev launch keeps `--path`, the scene and its
  `--` flags. Each `--xr-mode` pair ahead of `--` is dropped and `--xr-mode on` goes first; a menu
  relaunch adds `--skip-intro`. The `xr-relaunch-args` suite reads this in a real engine process:
  entry 0 is the executable `OS.GetExecutablePath()` names, and the rest behind `--` equals
  `OS.GetCmdlineUserArgs()`.
- **Flat-only runs** (`XrRelaunch.FlatOnly`): off Windows, `--headless`, `--run-tests`, every
  scripted and `--det` run (so the goldens, perf and hitch stages), `--xr-sim`, `--extract`,
  `--movie`, `--net-host`/`--net-join`, `--viewer`, `--freecam`, `--anim-lab`, `--weapon-lab`,
  `--debug-node-lab`, `--debug-damage`. Such a run never relaunches and never reads the saved choice;
  `--run-tests` also reads its options from scratch. Units pin every flag; the goldens stage was
  25/25 hash-identical with the change.
- **The fallback** posts `XrRelaunch.FallbackNotice` through the new `UI/Menu/MenuNoticeFeature.cs`
  when the launch requested OpenXR and no OpenXR session is live (`XrSession`'s warning stays). The
  mechanism the menu already had was Built-in's error line (`LaunchMenu.ShowError`, the failed-build
  note); Original had none at its top level, so it raises its own one-button messagebox. Suite
  `xr-fallback-notice` on both presentations. A command-line flight has no menu, so there the log
  warning is the message.
- **The refusal** shows where a second seat would be taken: the join board. With a headset or the
  stand-in live, `PlayerSetupFeature.SeatLimit` is one, both boards draw the three entries past the
  captain's as "closed seat / VR mode flies one seat" and refuse a second pad (the first pad still
  takes the captain's chair), Built-in's strip says so on every screen, and a local Dogfight's gate
  names it on both presentations; a network Dogfight is unaffected. `--players` above one is refused
  with a warning and flies one seat: `RunProbe.ps1 --stage=empty --players=2 --xr-sim --mute
  --frames=20 --screenshot=...` logged `xr: --players=2 refused` and the capture shows one aircraft
  per eye (`.scratch/B6/B6-xr-sim-two-players.png`). Suite `xr-one-seat`.
- **The shared shader cache.** `ShaderRD::_load_from_cache` (`servers/rendering/renderer_rd/
  shader_rd.cpp`, 4.7-stable) reads each variant's size and `continue`s past a disabled variant
  without skipping its bytes, so a flat launch misreads a file a stereo launch wrote, and the file
  name (`<shader>/<group sha256>/<version sha1>.<api>.cache`) does not depend on which variants are
  enabled. `RendererCompositorRD` sets the folder to `user://shader_cache` (`Engine::
  get_shader_cache_path` is set by the editor alone), before any script. No per-launch lever exists
  for an `--xr-mode on` launch: no command-line option names a cache or user directory, Godot adds
  no feature tag for XR, an exported build reads no `GODOT_EDITOR_CUSTOM_FEATURES`, and moving
  `APPDATA` would move every store with it. The fix keeps the flat path untouched:
  `Launch/XrShaderCache.cs` has a launch that requested OpenXR write a marker at boot and, as it ends
  or relaunches, delete every `.cache` file written since its process started; a boot over the marker
  of a process that is gone sweeps the same way. The flat launches' own files are never touched, so
  the next flat launch recompiles only what the headset session rewrote. Units over a scratch user
  directory; the sweep runs before a relaunch starts its child, whose renderer reads the cache before
  any game code.
- **Review follow-up (traced to code, units).** A launch started straight in the headset
  (`--xr-mode on`) used to overwrite a crashed headset launch's marker without sweeping it; every
  boot now recovers first (`XrShaderCache.Boot`). A relaunch whose `OS.CreateProcess` fails reopens
  the watch it ended (`EndThenStart`) and posts `XrRelaunch.StartFailedNotice` through
  `XrRelaunch.MenuNotice`. The log lines read `xr: restarting ...` and `xr: could not restart ...`.

**Approach (landed).** `OptionsDef.VrMode` (`vrMode`, null reads off) rides `OptionsApplyExit` like
every field; Original shows it on the VIDEO page's Clutter Detail line (`VP_B_CLUTTER`, a checkbox,
live on Windows alone) and Built-in as its Options screen's row under Shadow quality.
`Launcher.ApplyOptions` relaunches when the row changed and differs from what the running launch
asked for (`XrRelaunch.OnApply`); the pause leaf's apply saves it and no more. `Launch/XrRelaunch.cs`
holds the decisions and the arguments, `Launch/XrShaderCache.cs` the sweep, `Launcher` acts on both.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Units: `CSVM.Tests/XrRelaunchTests.cs` (the arguments with the engine flag ahead of `--`,
the dev-run exclusions, no loop, the apply rule, the fallback condition), `XrShaderCacheTests.cs`,
the store round trip, the seat limit and the VIDEO row. Suites: `xr-relaunch-args`, `xr-one-seat`,
`xr-fallback-notice`, and the Built-in Options walk in `menu-original-tracer`. The "requested but
failed" path is exercised as the pure `XrRelaunch.FellBack` and the notice it posts, so nothing here
starts a runtime. Owed at the headset (the author's), each from a fresh `.\RunGame.ps1`:
1. Options, VR mode On, ACCEPT CHANGES: the game closes and relaunches into the headset. The first
   launch's `.scratch/logs/menu-*.log` ends `xr: restarting into the headset (...)`, and the child's
   log carries `xr: OpenXR live runtime=...`.
2. In that headset launch, Options, VR mode Off, ACCEPT CHANGES: it relaunches flat, logging
   `xr: swept N shader cache files` before `xr: restarting flat`.
3. With VR mode left On, quit and run `.\RunGame.ps1` again: one window opens and closes, the
   headset launch follows (`xr: restarting into the headset (VR mode is saved on)`).
4. Fallback: with the Virtual Desktop streamer disconnected (or the headset off), `.\RunGame.ps1`
   with VR mode On: the relaunched game plays flat and the menu shows "VR mode is on, but no headset
   started..." (Original's box; `.\RunGame.ps1 --force-builtin` shows the error line).
5. After a headset session ends normally, the next flat engine and goldens stages
   (`.\RunTests.ps1 -SkipUnits`) log no `br != variant_size` and no `Uniform buffer supplied` line.
6. `.\RunGame.ps1 -EngineArgs '--xr-mode','off'` with VR mode On stays flat.
The same in an exported build (`.\ExportRelease.ps1`, then double-click `.scratch/export/CSVM.exe`).

**⚠ Traps.**
- Sweep the cache before `OS.CreateProcess`, never after: the child's renderer reads
  `user://shader_cache` before any of its game code runs, and the parent's exit is too late.
- A flat launch running *during* a headset session can still misread a file the session writes;
  the sweep covers only the launches after it. The reverse holds too: a headset launch that starts
  while flat launches are writing reads their files and logs one `br != variant_size` (seen when a
  battery ran during the author's headset session); Godot treats it as a cache miss and recompiles.
- `RunGame.ps1` waits for the process it started, so its console returns at a relaunch while the
  child plays on with no console of its own; the child's log is the next `menu-*.log`.
- The boot relaunch shows the first window for a moment: Godot creates it before any game code.
- `SeatLimit` is the process's, not setup state: `PlayerSetupFeature.Discard` keeps it.

**Verified.** The complete battery on the merged tree with A3b, B6 and C14 and the ShaderTwins
rewrite fix (LOG-26): build PASS, units 6348 passed 3 skipped, engine 533 passed 2 skipped with
clean engine errors, goldens 27/27 hash-identical. The headset checks named above are still owed.
With the review fixes (the flare's own layers, the headset boot's recovery, the start-failure notice): the
complete battery on the merged tree, build PASS, units 6531 passed 3 skipped, engine 552 passed 2
skipped with clean engine errors, goldens 37/37 hash-identical; exit 0.

## B7 ☑ The XR rig in `PlayerRig`, recenter and per-plane scale data

**At the headset (VDXR, the author):** recentering with the action returns the seated eye to the
cockpit eye ("rehoming works"). The runtime's own recenter and the dome's parallax were not
reported separately; E21 carries them.

**Outcome: built and pinned on the stand-in; the recenter at the headset is owed.** The VR seat is
`Flight/Camera/XrRig.cs`, bound to the session's first `PlayerRig`; the sky follows the tracked
head; recenter is automatic on focus and on the runtime's own recenter, and a new action on `Home`;
`CSVM/data/vr_cockpits.json` ships Decision 13's defaults for all eleven planes.

**Goal.** In a VR flight the XR origin follows the plane's first-person pose with the per-plane
interior scale and eye offset from `data/vr_cockpits.json`; the camera-anchored skydome and cloud
deck follow the XR camera; recenter snaps the seated eye back to `cockpit_camera`.

**Evidence (confidence: traced to the Godot 4.7-stable source for the recenter and the head node's
update; measured on the stand-in for the placement, the offset, the recenter and the sky; lead-only
for anything a headset shows until the author's run).**
- `XRServer::center_on_hmd` (`servers/xr/xr_server.cpp`) clears the reference frame, takes the
  **primary interface's `get_camera_transform`** (not the `head` tracker), keeps only its heading
  under `ResetButKeepTilt`, zeroes the height only with `keep_height`, stores the inverse and emits
  `reference_frame_changed`. So the head's current point becomes the origin, which `XrRig` stands
  at `XrCockpit.EyeInAircraft`: a recenter puts the eye there with no further arithmetic.
- `XRNode3D`/`XRCamera3D` (`scene/3d/xr/xr_nodes.cpp`) take the reference frame only through the
  tracker's next `pose_changed` (`XRPose::get_adjusted_transform`); nothing listens to
  `reference_frame_changed`. OpenXR reports the head every frame, so a headset's `Head` follows a
  frame later; the stand-in's pinned head never re-reported, so it now republishes its pose on that
  signal and `XrSimInterface.MoveHead` lets a suite move it (A2's finding that `CenterOnHmd` works
  under `--xr-sim` holds for the eyes, which apply the frame in `XrSimEyes.View`, and now for `Head`).
- The suite `xr-rig-seat` (C1 Bloodhawk at -6350, 300, -4144 on a rotated attitude, the stand-in
  interface live, no stereo viewport): the seated head 0 mm from the `cockpit_camera` marker and from
  the flat cockpit camera's eye, the lifted interior 0 mm from where the airframe holds it; with a
  6 cm aft offset the head lands on `EyeInAircraft` (0 mm off, 59.945 mm from the marker) and the
  interior stays put; a moved head (yaw 30, pitch -12, 5/3/10 cm) leaves the eye by 170.9 mm, the
  dome sits 0 mm from the tracked head and the deck follows it across; `Recenter` returns the head to
  `EyeInAircraft` (0 mm), facing the origin's heading with the -12° tilt kept, and the renderer's
  two eyes straddle it 0.488 mm off and 63.026 mm apart. With the rig released, the dome sits on the
  flat camera again.
- `FamilyOrderTests` forbids `Flight.Camera` naming `Flight.Hud`, so the rig reaches the interior
  through `IXrInterior` (in `XrRig.cs`, implemented by `XrCockpit`) and a resolver `XrSession`
  passes (`pilot => pilot.Dressing.Stereo`).
- A5's numbers are the file's `_proposed` notes and stay unapplied (Decision 13): interior factor
  1.0 (0.040 m per unit) on all eleven; `eyeOffsetZ` 0.06 on the Warhawk (dials at 0.32 m, under the
  0.33 m floor); factor 0.875 (0.035) to try first on the Hoplite, Hellhound and Balmoral; and, on
  every plane but the Warhawk, a forward move of 0.09-0.17 m as E20's first alternative for the head
  plate. The factor also scales the interior's depth bias (`DepthBiasScale = 1/scale`), so a priority
  level stays the same depth at any factor.

**Approach (landed, VR only).**
- `Flight/Camera/XrRig.cs`: the seat's `XROrigin3D` and `XRCamera3D`, `Bind`/`Release` of one pane
  (`PlayerRig.Xr`), `Place()` (the pane's camera and pilot, else the persistent camera) and
  `Place(eye, aircraft, attitude, interior)`, which suites call, and `Recenter`
  (`CenterOnHmd(ResetButKeepTilt, keepHeight: false)`). `XrSession` keeps the boot, the runtime
  log and the frame pacing, builds the rig, calls `Place` at priority 2000, and recenters on the
  head's first tracked pose after `SessionFocussed` and after `PoseRecentered`. `GameSession` binds
  `_rigs[0]` in `BuildRigs` and releases it at exit; `LauncherContext.Xr` replaces `XrHead`
  (`GameSession._xrHead` is now `Xr.Head`, so C14's flare and quads are unchanged).
- `PlayerRig.SkyAnchor` is the head under VR and `Camera.Position` otherwise; `WeatherRig.Tick` reads
  it for the dome, the deck, the whiteout and the weather state. The flat path reads exactly what it
  read, and the suites still receive the Launcher's `_camera`; the cull mask stays on the flat camera.
- `XrMount.For(eye, attitude, eyeOffset)` adds the offset, in the aircraft's axes, in first person
  only. `XrCockpit(interior, eyeOffset)` carries it, so `EyeInAircraft` and the origin move together.
- `Flight/Hud/VrCockpits.cs` reads the file once per session in `FlightRosterPolicy.From`, only when
  the stereo interior is built; `HumanFlightAdapter` passes the factor to `PlaneBuilder`
  (`interiorScaleFactor`, mount and depth bias) before `XrCockpit` captures the mount, and the
  offset to `XrCockpit`. Fields and ranges: docs/architecture/Flight.md on `VrCockpits`.
- `InputAction.RecenterHeadset` (appended, 93), a flight action on `Home` (free in the original's
  table) and on the pad's touchpad click and first back paddle, the only buttons no flight action
  holds; `FlightController` polls its edge beside the graphics switch and calls the session's
  recenter, null in flat play. The Original controls page lists it on Other after the graphics
  switch, and docs/controls.md has its row. C11 adds the XR controller binding. The row is visible
  in flat play too, inert there, so a player can bind it before relaunching into VR; the author
  accepted this flat-path change.

**Contract for C10, C12 and D19.** Reach the seat through `LauncherContext.Xr` (or
`GameSession`'s `_xrRig`): `Head` is the tracked head, `Origin` the tracking space, `Pane` the bound
`PlayerRig`. Anything head-locked or cockpit-locked builds A3b's two pieces (a top-level node in the
origin's frame and a shader that subtracts the fed eye) and places itself after `XrRig.Place`, at a
process priority above 2000, as `HeadLockedQuads` does. A recenter moves the eyes, not the origin,
so a cockpit-locked panel needs no recenter handling and a head-locked one follows `Head`. The
theater screen at a menu has no pane: the origin stands at the persistent camera, heading only.

**Model recommendation.** Standard; the one trap needing the source read was `center_on_hmd`'s use
of the interface camera and the head node's lag.

**Verify.** Units: `CSVM.Tests/VrCockpitsTests.cs` (the committed file at the defaults, missing and
malformed fallbacks, the offset along the tilted Z, the mount's first-person-only offset),
`DefaultBindingsTests.TheVrRecenter_IsAppendedAndShipsOnHomeAndTheSparePadButtons`. Suite
`xr-rig-seat`. The goldens stage: 27 of 27 hash-identical, the three stereo shots included (with
defaults nothing they draw moves). A first goldens run flipped `c1-flight-kill` once
(`e4984f01` to `344e2358`) with another session's Godot running; the same build's rerun was 27/27.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
```
Look for: (1) sitting off-centre when the game starts, the view still opens on the cockpit eye,
gunsight ahead, and the log has `xr: recentered (session focused)`; (2) lean 20 cm to one side and
turn the head, press `Home` (or a PlayStation pad's touchpad click): the eye returns to the cockpit
eye facing the nose with the head's tilt kept, `xr: recentered (the recenter action)`; (3) hold the
headset's system button for the runtime's recenter: the same, `xr: recentered (the runtime
recentered)`; (4) leaning about, the moon and the horizon dome show no parallax. The same on SteamVR.

**⚠ Traps.**
- The suites receive `_camera` from the Launcher (`Launch/Launcher.cs`); the flat rig keeps that
  camera and `SkyAnchor` reads it unchanged, so no flat suite reads anything new.
- Do not put the per-plane scale on `XRServer.world_scale`: it resizes the sky and every aircraft.
- After `CenterOnHmd` the `XRCamera3D` moves only on the head's next pose; read the eyes
  (`GetTransformForView`) or wait a frame, never `Head` straight after a recenter on a headset.
- A second `XrCockpit` over a lifted interior captures the lifted transform; hide it and `Follow`
  once (which lowers it) before building another.

**Verified.** The complete battery on the merged tree with B7 and the C14 follow-up: build PASS,
units 6365 passed 3 skipped, engine 534 passed 2 skipped with clean engine errors, goldens 27/27
hash-identical. The headset checks named above are still owed.

## B8 ☑ The cockpit interior in the main world under VR

**At the headset (VDXR, the author):** no red silhouette, the needles sit on their dials, and the
cockpit stays clear in the cloud band. The gunsight glass reads fainter than in the flat pass and its
reticle doubles when the eyes converge on a distant target; that moves to C13b. The target markers
are absent, as expected until C12 and C13.

**Outcome: built and pinned on the stand-in; the headset look is owed.** A stereo session builds no
cockpit pass and always draws `XrCockpit`'s interior, with or without `--no-cockpit-pass`. The
stereo interior leaves the head plate out (`Mech3/CockpitPlate.cs`), marks itself in the stencil so
the whiteout and the flare rings leave even its far wings clear, and draws its glass between the
whiteout and the flash. On the way, a culling defect from A3b was found and fixed: the lifted
interior's box shrank with the 0.04 mount, so any eye more than about 2 km from the chapter origin
and facing away from it culled the whole interior.

**Goal.** In VR, Cockpit view shows `cockpit1` in the main world, correctly lit and occluding the
world per eye, with the gauges moving, and no cockpit pass is built.

**Background.** `--no-cockpit-pass` leaves the interior in the main world. The pass carries a cloned
sun, the panel point-light term through `light_origin` and the shake mount (`WobbledMount`).

The first headset run (A1, VDXR, Bloodhawk, `--no-cockpit-pass`) showed two interior defects, both
traced to the data and the code:

- **A red "person cutout" straight ahead.** Every interior carries a flat head-and-shoulders plate
  on one z plane behind the eye (Bloodhawk: polygons #48-54 of `cockpit1/structure/g366`
  at z +0.80 iu, 3.2 cm at 0.04 m/iu, Colored `rgb(215,43,0)`, priority 5, normal aft, no
  `show_backface`; the Brigand's is grey, the Warhawk's lies 6.80 iu aft). From the authored eye it is behind
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

**Already done by A3b.** Under the stereo path with the interior in the main world, the interior is
`Flight/Hud/XrCockpit.cs`'s: drawn from the XR origin's frame with the render thread's eyes, so it
holds still far from the chapter origin, and its depth bias pulls toward the drawing eye, which is
the "true position, depth only" fix in another form (the gauge-depth defect is gone on the
stand-in). The
wobble reaches it through `XrCockpit.InOrigin`, the point lights through `csky_xr_origin` in place
of `light_origin`, and the gauge drive is untouched. Left for B8 were the main-world interior
without `--no-cockpit-pass`, the plate filter, and a look at the lighting (all below).

**Evidence (confidence: traced to code and measured on the stand-in for the default, the culling,
the plate and the stencil; measured for the lighting comparison, whose remaining differences are
look questions for the headset; direction sound for the flip's cause).** Instruments and captures
are in `.scratch/B8/` (`B8_shot.ps1`, `B8_repeat.ps1`, `B8_cmp.py`, `B8_plates.py`, `B8_reach.py`).
- **No pass under VR.** `FlightRosterPolicy.XrInterior` is `XrCockpit.Live`, and
  `GameSession.BuildCockpitPasses` builds only where `CockpitOverlay.InteriorInPass(asked, stereo)`
  (`asked && !stereo`) holds. An `--xr-sim` launch without the flag logs `stereo interior at 0.04 m
  per unit ... head plate left out (7 polygons)` and no `interior drawn in its own pass` line. The
  25 flat goldens are hash-identical.
- **The culling defect (fixed).** `XrCockpit` gave each lifted geometry a 40 km custom AABB in the
  geometry's own frame, which the 0.04 mount shrinks to 1.6 km about the world origin. The interior
  showed at `--pos` 0,300,0, -2000,600,-2000, 5000,300,5000 and 20000,300,0 (heading
  -0.743,0,-0.669, origin ahead or near) and was missing at -6350,300,-4144, -10000,700,-10000 and
  -11500,300,-11500 (origin behind). A3b's `c1-cockpit-xr-sim` golden was pinned without an interior
  for that reason, and A3b's and B7's owed headset poses would have shown none. `Everywhere(mountScale)`
  now divides the reach by the scale; `XrCockpitTests` pins an eye 7.6 km out and C3's far corner.
- **The plate filter.** `CockpitPlate.IsPlate`: every corner on one z plane (1e-3 iu), 0 < z ≤ 8 iu,
  Newell normal aft, colour material, priority 5, read in `cockpit1`'s own frame. `B8_plates.py`
  (the diagnosis walk with this predicate) selects 7 polygons on each airframe and 5 on the Balmoral,
  75 in all, each plate on one node whose mesh no other node uses; no polygon in any interior meets
  four of the five criteria. The plan's 2.5 iu bound missed the **Warhawk's plate at 6.80 iu**
  (0.27 m, the same red seven polygons on `g150`), which A5 read as the seat back. The filter keeps
  each surface group's rank, so the rest of the mesh builds exactly as before. Head 10 cm aft on the
  C1 pose: without the filter the red silhouette fills both eyes, with it the panel shows
  (`plate_off_on.png`). At the authored eye it changes no pixel in Original or Enhanced.
  `CockpitPlateTests` pins the eleven counts.
- **The whiteout and the far interior.** Every interior reaches past the 1.4 m seat clearance:
  3.7 to 15.2 m, 19 to 186 polygons per plane (wings, nose, turrets, ailerons, rudders;
  `B8_reach.py`). The interior's opaque and cutout surfaces now write stencil 1
  (`SceneBuilder.XrInteriorStencil`); the whiteout quad, still depth-tested, and the stereo flare
  rings skip it. Its blended surfaces draw at priority 110 between the whiteout (109) and the flash
  (111), the flat pass's layer order, which keeps the gunsight glass over the whitened world. On
  C1's cloud band (`--pos=-6144,1000,-6144 --direction=0,0,-1`, frame 30) at `yaw=70` the wing went
  from whitened to clear (0.55 % of pixels by more than 32 levels). With the left eye on the flat
  camera the stereo frame matches the flat pass to 0.43 % of pixels over 8 levels (the HUD pipper),
  where flat `--no-cockpit-pass` whitens the whole interior (37.8 %).
- **Near plane.** Godot projects each eye with the viewport camera's planes
  (`get_projection_for_view(v, aspect, camera->znear, camera->zfar)` in `renderer_scene_cull.cpp`),
  and the flat camera keeps Godot's default 0.05 m (the Launcher sets only `Far`). The quads at 0.25 m
  and the nearest interior geometry once the plate is gone (canopy members 0.22 m and more) are clear.
- **Lighting against the flat frames** (SHOT-45's eye, empty stage, frame 60). The stereo interior
  matches flat `--no-cockpit-pass` to 0.16 % of pixels over 8 levels in Original (the HUD pipper and
  edge AA), firing or not, and 1.04 % / 0.49 % in Enhanced: the sun, the Environment, the
  `csky_xr_origin` point term and the wobble reach it as they reach the flat in-world interior.
  Against the flat pass it differs by 0.44 % (Original) and 3.22 % (Enhanced), firing 3.91 % and
  7.98 %, all of it the pass's own: (a) the pass composites its soft alpha in the canvas, so the
  gunsight glass lifts 4.7 levels over the sky in Original and 21 in Enhanced, against 0.7 and 3.2 in
  stereo (`GammaBlendAlpha` is exact only over black); (b) the pass turns the wobble about the eye,
  the world about the airframe's pivot; (c) under Enhanced the pass's own sun shadow, clamped to its
  100 m far, self-shadows the side rail that the world's cascades leave lit, and AgX tonemaps the
  pass apart. A VR-only exact gamma mix through the screen texture was tried and dropped: it moved
  Original's glass to -4.2 levels in blue and broke stacked decals by up to 223 levels, since the
  screen texture holds no transparent layer. None is a stereo defect; the glass and the shadow are
  for the author to judge at the headset.
- **C14's gauge-face flips survive, and are not the interior's.** C3 cockpit flare pose
  (`--pos=0,1200,0 --direction=0.5,0.707,-0.5 --view=cockpit --xr-sim`, frame 120): 3 of 9 runs
  differ from the first by one level in 42-454 pixels, 1 of 6 under `--render-thread safe`, 0 of 6
  with `--no-flare`. Every flipped pixel lies on the gunsight glass over the sun's core (223-255),
  where the glass pulls the core below saturation; outside the glass the core clips at 255. The glass
  writes no depth, so no z-tie is involved, and a distinct sorting offset per interior surface left
  the flips in place (4 hashes over 9 runs; reverted). The cause is the core's own one-level
  variation, which the glass makes visible. The C3 probe still exits with an access violation after
  saving in about a third of runs (C14).

**Approach (landed, VR only).** `CockpitOverlay.InteriorInPass` and `FlightRosterPolicy.XrInterior`
as above. `Mech3/CockpitPlate.cs` finds the plate; `PlaneBuilder` hands it to the interior
builder's `SceneBuilder.OmittedPolygons` when `xrInterior` is set. The `XrPreciseInterior` shader
variant writes the stencil on its opaque and cutout arms, and its blended materials take
`XrInteriorBlendPriority`; `HeadLockedQuads`' whiteout reads the stencil and moves to 109, and
`WorldFlareRig`'s rings read it in a stereo session only. `XrCockpit.Everywhere` sizes the lifted
box through the mount scale. Flat shader text is unchanged.

**Model recommendation.** Standard; the culling and the plate's Warhawk case were found by
measuring, not by reading.

**Verify.** Units: `CockpitPlateTests` (the eleven counts, the rule's halves, `InteriorInPass`),
`XrCockpitTests` (the lifted box). Suites: the `cockpit` and `xr` filters, `head-locked-quads` (the
order with the glass, the stencil read), `world-flare-pixels`. Goldens: 28 of 28;
`c1-cockpit-xr-sim` re-pinned (it now draws the interior), the new `c1-cockpit-xr-sim-aft` (C1
12.8 km out facing away from the origin, no flag, head 10 cm aft, stable over 5 runs) pins the
default, the plate and the culling; `c1-cockpit-xr-sim-far` and `c3-flare-xr-sim` unmoved.

Owed at the headset (the author's), VDXR at 90 Hz first, then SteamVR, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit --graphics=enhanced
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_warhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,1000,-6144" "--direction=0,0,-1" --view=cockpit
```
Look for: (1) the cockpit is there at the C1 map centre facing away from the origin; (2) leaning back
and looking ahead, no red silhouette (the Warhawk's sits about 27 cm back); (3) the interior lit as
in the flat game from the same command without `-EngineArgs` (Original, then Enhanced): the
gunsight glass reads fainter in VR than in the flat pass, and under Enhanced the side rails under the
canopy frame are lit where the flat pass shades them, both to judge; (4) the needles and lamps on
their dial faces; (5) in the cloud band, the panel, the canopy and the wings when you turn to them
stay clear while the world whitens, with the glass over the white.

**⚠ Traps.**
- The flat game keeps the pass (the boundary rule); every change here is VR-only.
- Do not size the lifted interior's box in world metres: it is in the geometry's frame and shrinks
  with the mount.
- Do not select the plate by node: it shares its node with canopy structure.
- Godot accepts a stencil read only on a material in the alpha pass; keep reads on the quads.

**Verified.** The complete battery on the merged tree with B8, C10 and the head-locked quads' tint
fix: build PASS, units 6392 passed 3 skipped, engine 535 passed 2 skipped with clean engine errors,
goldens 29/29 hash-identical. The headset checks named above are still owed.

## B9 ◐ Views in VR: rigid chase, nose, head tracking in place of head look

**Outcome: built and pinned on the stand-in; the comfort of the chase and the view cycle are owed
at the headset.** A headset flight opens in Cockpit; Cockpit and Nose stand the origin on the seated
eye with the aircraft's axes; the chase is a fixed offset in the aircraft's axes with no lag; head
look is off; every other camera is level, and a static one holds its heading. Each cut raises
`XrRig.Cut`, D16's seam. The per-camera table is in `docs/org/cameraViews.md`, "Views under a
headset".

**Goal.** Cockpit is the VR default; Nose works; Chase is attached rigidly to the plane with no spring
lag, no speed FOV and no swing; head look's Snap, FreeLook and Padlock are inactive under VR and the
headset owns the head.

**Evidence (confidence: traced to code and measured on the stand-in for every placement, the input
gating, the opening view and the cuts; lead-only for comfort, which is the author's at the headset).**
The suites' reports are `.scratch/test-xr-view-*.txt`, the captures `.scratch/B9/`.
- **How the origin learns the view.** Every camera write in `CameraController` now names an
  `XrAnchor` (`Seat` for first person, `Camera` for a pose a headset write already made the
  origin's, `Level` otherwise), and `XrRig.Place` takes it once a frame (`TakeXrWrite`) after every
  writer. A camera the pilot's controller does not own (`CameraOwned`: a presenting cutscene, the
  spectator and photo-mode cameras, the weapon lab) is taken by its heading, B7's placeholder. A frame
  nothing wrote (a halt) keeps the last anchor; once the aircraft is down it is levelled, so a crash
  with no camera cut cannot hold a rolled horizon. The flat camera holds the same pose, so the
  listener and every other reader of it agree with the headset. Gated on `FlightController.Headset`,
  which `HumanFlightAdapter` sets only where the stereo interior is built (`XrCockpit.Live`, local
  seat).
- **The opening view.** `SessionSpec.WithHeadsetView` runs after the saved Default View in
  `Launcher.LaunchSession` and sets Cockpit when a headset or the stand-in is live, unless `--view=`
  names anything (a mode, a digit, `back`, `flyby`); it applies under `--det` too. An `--xr-sim`
  C1 flight with no `--view=` logged `view n=cockpit` and `xr: cut (a new aircraft) anchor=Seat`
  (`.scratch/B9/B9-open-default.png`). Under a headset the destroy callback's view reset returns
  to the opening view rather than the chase view.
- **Cockpit and Nose** (`xr-view-first-person`, C1 Bloodhawk at -6350,600,-4144 on a rotated
  heading, roll then pull, 240 frames, the aircraft turning 139.2°): the origin stays on the
  `cockpit_camera` marker with the aircraft's axes, worst 0.339 mm and 0.0000°, in both views; Nose
  hides the interior. The cycle walks Cockpit, Nose, Chase, Cockpit with anchors Seat, Seat, Camera,
  Seat and exactly three cuts.
- **The rigid chase** (`xr-view-chase`, the same turn): worst 0.318 mm and 0.0000° off the fixed
  offset, zero lag. Control (METHOD-9): the flat chase on the same turn leaves its settled pose by
  5.778 m and 18.469°. The `level` variant keeps world up to 0.0000° and its height to 0.021 mm. The
  offset is `CameraController.VrChaseOffset`, the settled authored rig at `dist` held to
  `[dist_min, dist_max]`, the flat chase's cruise distance: per airframe 14.85 m (Kestrel) to
  25.86 m (Balmoral) out, 7.57° below the level gaze (11.11° on the Balmoral, whose rig is
  0.2/0.2), so the aircraft sits a little under the natural seated line of sight at the default
  head pose. Captures: `.scratch/B9/B9-chase-rigid.png` and `B9-chase-level.png`, frame 50 of a
  roll.
- **Head look off** (`xr-view-head-look`): with a numpad snap, the look stick, free-look, smooth
  mode, the centre key, look-behind and padlock all held for 90 frames, the origin moved 0 mm and
  0° in Cockpit, Nose and Chase, the head stayed at 0/0 in snap mode. Control: the same inputs on the
  flat rig turn its camera 179.9° (first person) and 180° (chase). `StepHead` reads no input,
  `BackActive` is false, the chase takes no stick swing, no zoom and no speed widen.
- **The other cameras** (`xr-view-static`): the flyby's origin holds its spot and its level heading
  to 0 mm and 0.0000° between re-sites (3 spots, a cut each, over 600 frames). The held heading
  faces the nearest point of the track ahead (`XrMount.Watch`), so the passing aircraft swings at
  most 89.6° round from the gaze; facing the aircraft at the cut instead put it 176.9° round, behind
  the seat, in the first run of the suite. The death camera holds a level heading over 60 frames of a
  falling wreck where the flat one re-aims 16.1°; the crash cut faces the impact level. A camera
  another owner drives is taken at its eye with its heading, with a cut as it is taken and one as it
  is handed back.
- **Flat unchanged.** Every new branch reads `Headset`, null on every flat seat. Units 6402 passed,
  3 skipped; the camera suites (`chase-rig`, `chase-trail`, `death-camera`, `flyby-camera`,
  `look-stick`, `look-stick-edge`, `options-view-launch`, `death-respawn-rest-pose`, the `cockpit`
  set) and every `xr` suite pass with clean engine errors. Goldens: 29 of 29 hash-identical on the
  second run; the first moved `c1-cloud-deck-enhanced` (a `--freecam` shot no flight camera
  touches) and `c1-cockpit-enhanced` once, with another session's Godots running.
- **The chase frame, decided: attitude-locked (`--xr-chase=rigid`), heading-only as the A/B
  (`--xr-chase=level`).** Decision 3 says rigidly attached and Decision 12 has no horizon lock. The
  attitude frame is the cockpit's own, so a view switch never changes how the world moves about the
  player. A heading-only frame keeps the horizon level, which is the usual comfort argument (the rest
  frame a cockpit gives is missing in a chase view), but its heading reverses through any vertical
  flight: `XrViewsTests` pins a 180° turn of the whole view between 89° and 91° of climb, where the
  rigid frame moves 2°. Every loop would carry that cut. Comfort confidence: lead-only.

**Resolved TODOs.**
- `LookCenter` (`numpad 5`) and `LookBack` (`numpad 0`, right-stick click) are inert under VR, as are
  the numpad cluster, MMB free-look, `K`/`L`/`J`, the look stick and the chase zoom. Proposal for
  C11 (not rebound here): under VR both `LookCenter` and `LookBack` fire `RecenterHeadset`. The
  centre key already means "look forward", and the right-stick click is the one recenter a standard
  Xbox pad can reach, since it has neither the touchpad nor the back paddle B7 bound.
- The view cycle stays `CycleCockpitViews` and already walks only Cockpit, Nose and Chase; the flyby
  (`F7`) stays reachable and holds its spot.
- Cutscenes: a presenting cutscene sets `CameraOwned`, so the origin follows the cutscene camera by
  heading and nothing here writes the camera; D15 replaces that arm (`XrRig.Place`'s
  `CameraOwned` branch) with the theater screen.
- View switches are hard cuts. `XrRig.Cut` fires once per cut (a view change, a respawn, a new
  static spot, a new watched aircraft, the camera changing hands, an anchor change that moves the
  origin) with the cause, logged at Debug as `xr: cut (...)`. D16 hangs its fade on it.

**Approach (landed, VR only).** `Flight/Camera/XrMount.cs`: `XrAnchor`, `XrChase`, `For(eye,
anchor, attitude, eyeOffset)`, `Chase` and `Watch`. `CameraController.Headset`, `Shot`,
`TakeXrWrite`, `VrChaseOffset`; the VR arms in `StepHead`, `BackActive`, `FirstPersonView`, `Chase`,
`StepEnhancedCues`, `ResetToChase` and the static cuts. `FlightController.Headset` and
`CameraControl`; `XrRig.Place` by anchor, `Anchor` and `Cut`. `--xr-chase=rigid|level`
(`SessionSpec.XrChase`, `FlightRosterPolicy.XrChase`) and `SessionSpec.WithHeadsetView`.

**Model recommendation.** Standard.

**Verify.** Units: `CSVM.Tests/XrViewsTests.cs` (the chase offset and bounds, both frames over a
sweep of attitudes including inverted and past vertical, the heading reversal, `Watch`, each
anchor, the opening view, the flag). Suites: `xr-view-first-person`, `xr-view-chase`,
`xr-view-head-look`, `xr-view-static`, each with its flat control. Goldens: none new, 29 unmoved.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669"
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=chase --xr-chase=level
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=flyby
.\RunGame.ps1 -EngineArgs '--xr-mode','on'
```
Look for: (1) the first flight opens in the cockpit with no `--view=`; `F8` (or the pad's D-pad down)
walks Cockpit, Nose, Chase and back, each a hard cut, Nose with no interior; (2) in the chase the
aircraft sits a little under your straight-ahead gaze at a comfortable distance, and banks, loops
and rolls are tolerable with the world turning about you, and the offset reads right; (3) the second
line flies the level chase: the same manoeuvres with the horizon level, and the half-circle turn at
the top of a loop; say which should be the default; (4) the numpad, the right stick and its click,
`numpad 0` and the middle mouse button change nothing; (5) the flyby (third line, or `F7`) holds a
level view facing the flight path while the aircraft passes, and each new spot is a cut; (6) from
the menu (fourth line) with the saved Default View on Exterior, an Instant Action flight still opens
in the cockpit; (7) crash into the ground in each view: the crash cut faces the impact level, which
leaves the wreck about 56° below the gaze at the authored 30 m back and 45 m up, to judge.

**⚠ Traps.**
- Do not re-aim a static camera's origin per frame under a headset: a flyby passing at 6 m yaws the
  world at hundreds of degrees a second. Hold the heading from the cut.
- Do not take the flat camera's basis in first person: it carries the flat head pitch. `Seat` takes
  the aircraft's attitude, and the stereo interior is placed from exactly that pose.
- A suite's held action stands on the key and the pad halves alike, so a held `CycleCockpitViews`
  walks two stops in one frame; call `CycleCockpitViews()` directly. A suite also runs in one engine
  frame, so a held action stays stored until released.
- The suites step `FlightController.SimStep`, not `_PhysicsProcess` (the base no-op): without it
  the aircraft never moves and a lag check passes vacuously.

**Verified.** The complete battery on the merged tree with B9 and C11: build PASS, units 6417
passed 3 skipped, engine 541 passed 2 skipped with clean engine errors; goldens 28/29 with
`c1-cloud-deck-enhanced` moved while the author's headset session ran on the same GPU, then 29/29
hash-identical on two goldens re-runs (#114's pattern). The headset checks named above are still owed.

## B10 ◐ Billboards and sky sprites face the eye and hold the world's up as the head turns

**Outcome: fixed on the stand-in, VR only; the look at the headset is owed.** The author, at the
headset: "I found another bug in VR. The moon in C1 is turing with the head. Could happen to other
billboard sprites too." Then: "It did not roll but rotate when i moved my head left to right or up
down". Every stereo sprite whose quad copied the view's basis now faces the eye with world up.

**Goal.** A sprite in the world keeps its world direction and its shape as the head turns, pitches
or rolls: a sky body faces the eye with the horizon's up, a spherical particle faces the eye, a
cylindrical card keeps world up. The flat game is unchanged.

**Evidence (confidence: traced to code and measured on the stand-in, before and after, for the
moon; traced to code for the other classes, whose stereo texts the suite reads).**
- **The mechanism is rotation, not position.** `WorldBuilder.BillboardMoon` drew the moon with
  Godot's `BILLBOARD_ENABLED`, which builds the quad from the main camera's basis. Under the stereo
  path that basis is the head's combined view, so the disc lay in the head's view plane. Turned
  away from it by an angle a, the eye sees the disc a off its normal: squashed radially by cos a,
  which reads as the disc turning on its centre as the head moves. It also stayed upright to the
  eyes under a head roll. Its position was right throughout: the moon's direction rotated with the
  sky (19.80° on screen for a 20° roll), because the dome follows the head's position only. The
  flat camera is not the source: the copied basis followed the head's roll, which no flat camera
  carries.
- **Measured before** (`.scratch/B10/b10_capture.ps1`: `RunProbe.ps1 --chapter=C1
  --plane=player_bhawk --pos=-6144,1500,-6144 --direction=-0.6,0,-0.8 --view=nose --hold=0,0,0,0.6
  --xr-sim=<pose>,eye=1200x1200,fov=100,ipd=63,shot=left --det --mute --frames=120`, read by
  `.scratch/B10/b10_shape.py` and `b10_roll.py`). A disc facing the eye draws radial/tangential
  1/cos a in a rectilinear eye; one in the view plane draws 1.000. At yaw 25, -25, 40, -40 and pitch
  -5, the gaze 22 to 39° off the moon: 0.994, 0.991, 0.996, 0.991 and 1.007 against 1.08 to 1.30 for a
  facing disc. Roll 20: the moon sprite turned 0.0° while the sky turned 19.80°.
- **Measured after:** 1.076, 1.070, 1.208, 1.200 and 1.310 against 1.083, 1.080, 1.220, 1.214 and
  1.299 predicted; roll 20: the sprite turned 19.5° with the sky's 20.04°.
- **The data agrees with the fix.** C1's `moon` is model 770, `Facade`/`SphericalY` (C1C, C5
  the same; C4's moon is the same class and already took `SceneBuilder`'s facade shader). The
  original's SphericalY tracker sees only the eye's position, never its basis
  (`docs/org/cloudCards.md`), so a world-up look-at toward the eye is its rule, flat or stereo.
- **A4's billboard row** ("Billboards facing the eye: correct as is") checked the two eyes'
  disparity, which is right; it did not check the basis under head motion.

| Class | Code | Basis before | Under head turn and roll in VR | Verdict |
|---|---|---|---|---|
| Moon (C1, C1C, C5) | `Mech3/WorldBuilder.cs` `BillboardMoon` | Godot billboard, the view's basis | turned edge-on and rolled with the head | fixed: `StereoFacing.SpriteShaderCode` |
| Moon (C4), facade flares (`refinery_flare`, `gen_flare_yellow`, `docklight_flare`, `litehsflare`, `flare_red`/`flare_green`), cloud sprites, placed puffs | `Mech3/SceneBuilder.cs` billboard shader | `csky_facade_spherical`, world up toward the eye | holds | correct as is |
| CylindricalY facades and sails, CylindricalX flames | `SceneBuilder.cs` cylindrical shader | yaw about the world or model axis toward the eye | holds | correct as is |
| Clutter cards and glows | `Mech3/Clutter.cs` `FaceBasisLines` | facade or cylindrical | holds | correct as is |
| Cloud field cards | `Effects/FogVolumeClutter.cs` | facade | holds; the Enhanced grade read its gaze and sun offset off the view, so the rim and tint swung with the head | grade fixed under stereo |
| Puffer sprites (smoke, fire, splashes) | `Effects/EmitterRenderer.cs` | `INV_VIEW_MATRIX` columns | turned and rolled with the head; the smoke grade too | fixed under stereo |
| Heat shimmer column | `Effects/HeatShimmer.cs` | `INV_VIEW_MATRIX` right and up | the column tilted and turned with the head | fixed under stereo |
| Snow flakes | `Effects/Precipitation.cs` | `INV_VIEW_MATRIX` right and up | round flakes, invisible, made consistent | fixed under stereo |
| Rain streaks, wind streaks | `Precipitation.cs`, `Effects/WindStreaks.cs` | fall or stream axis, thin side to the eye | holds (the view basis only in the degenerate end-on case) | correct as is |
| Nav and wing lights | `Mech3/PlaneBuilder.cs` flare shader | facade | holds | correct as is |
| Point-sprite lights, stars | `SceneBuilder.cs` `LightShaderCode` | `POINT_SIZE` screen square | a radial glow, so no turn shows; off-axis it draws cos² a narrower in angle, 3 to 30 px | left; minor |
| Projectile tips, muzzle flashes, impacts, tracers | `Flight/Weapons/Projectile.cs` | world bases (velocity, muzzle, surface) | holds | correct as is |
| World flare rig | `Session/World/WorldFlareRig.cs` | the tracked head's view, by design | a lens artifact belongs to the eye | correct as is |
| Head-locked near quads | `UI/Boards/HeadLockedQuads.cs` | the head, by design | | correct as is |
| `Label3D` overlays (`NodeLabels`, `AiNetsOverlay`, `MarkerOverlay`) | `UI/Overlays/` | Godot billboard | dev overlays, flat only | not this item's |
| C2 `moonbackdrop`/`moonsurface`, C5 `star1` | world geometry | `Default` models, no facing | holds | correct as is |

**Approach (landed, VR only).** `Mech3/StereoFacing.cs`: `Live`, set by `XrSession.Build` before any
world, the stereo `PoseLines` and `SpriteShaderCode`. Each sprite chooses its text once from it:
`WorldBuilder.MoonMaterial`, `MultiMeshEmitterRenderer.VariantCode(..., stereo)` (the key gains
`Stereo`), `HeatShimmer.CodeFor`, `Precipitation.CodeFor`, `FogVolumeClutter.ShaderCodeFor`. A
stereo text is the flat text with the view-basis lines swapped, so every flat text is byte for byte
what it was and no flat golden can move. The stand-in gains `roll=` (`XrSimSpec.RollDeg`,
`XrSimEyes` applies it last about the head's own line of sight; `docs/cli.md`).

**Model recommendation.** Standard.

**Verify.** Units `XrSimTests` (the roll token and law), 73 passed with the emitter and fog-volume
filters. Suite `xr-sprite-facing`: 15 stereo texts pose by the facade look-at, read no view column
and compile, and their flat texts keep the view basis; the camera, puffer, cloud, clutter and `xr-`
suites, 35 in all, pass with clean engine errors. Golden `c1-moon-xr-sim` (yaw 35, pitch 28; the
moon 30-odd degrees off the gaze): `473ffb50d95b7e2fdaf5fed1c25c28ff` on 16 runs, and
`99cc878e211bf70523e5640afa1587c9` on 6 runs with the stereo pose switched off, so it goes red on
this bug. The stereo goldens re-pinned, each probe reproducing the old pin exactly with the switch
off: `c1-cockpit-xr-sim` `7dd5c505e1d4ac51a1dd4eac449eb3b6`, `-far`
`946b6b075c9340a589a79bc6b212cf6a`, `-aft` `2c7733e18689218ea104a29770db34ff` (at most 3 levels on
one pixel), `c3-flare-xr-sim` `0cc3ce1320eb32570c7bc76d7d4d38b3` (218 pixels, up to 29 levels, all
on faint puffs); `menu-original-xr-sim` unmoved.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,1500,-6144" "--direction=-0.6,0,-0.8" --view=nose
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,1500,-6144" "--direction=-0.6,0,-0.8" --view=nose --graphics=enhanced
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6104,300,-4420" "--direction=0,-0.33,0.94" --view=nose --graphics=enhanced
```
Look for: (1) the moon stands straight ahead, about 28° up, above the cloud deck; look left and right,
up and down, and tilt your head: it stays a round disc facing you, upright to the horizon, at the
same place in the sky; (2) the second line, Enhanced: the cloud field's bright sun-side rims stay
put on each cloud as you turn and tilt; (3) the third line, Enhanced, low over the ground: fire
rockets at the ground and look around the explosion, its smoke and the shimmer above it as you
turn and tilt; nothing pivots or leans with the head.

**⚠ Traps.**
- Do not give a stereo sprite Godot's billboard mode or a basis from `INV_VIEW_MATRIX` or
  `MAIN_CAM_INV_VIEW_MATRIX`: under the stereo path both are the head's view. Take `StereoFacing`.
- Do not change the flat texts to match. The flat moon copying the camera's roll contradicts the
  original's SphericalY rule (its tracker never sees the camera's roll), but correcting it moves
  the flat goldens, so it is its own item: #150.
- A stand-in probe sometimes exits 0xC0000005 or 0xC000001D after its capture, with
  `FATAL: Condition "csharp_lang && !csharp_lang->script_bindings.is_empty()"` and thousands of
  leaked references in the teardown. Interleaved at the moon pose it was 1 of 12 with the stereo pose
  off and 2 of 12 with it on, and its rate rose with the other Godots running. The golden stage reads
  the PNG and the log, not the exit code. The cause was a .NET finalizer racing Godot's teardown,
  in flat runs too, and `Launcher.SettleFinalizers` removes it (verification LOG-27).

**Verified.** The complete battery on the merged tree with C13b, B10 and C13 (the stereo goldens
re-pinned on the combined tree): build PASS, units 6443 passed 3 skipped, engine 543 passed 2
skipped with clean engine errors, goldens 32/32 hash-identical. The headset checks named above are
still owed.

# Wave C, screen, HUD and input

## C10 ◐ The virtual theater screen for menus, boards and cinemas

**Outcome: built and pinned on the stand-in; readability, size, distance, the surround, the pause
board in flight and a cinema are owed at the headset.** Every root canvas layer from
`HudLayers.Board` up (menus of both presentations, campaign boards and briefings, hangar, pause and
results boards, load and boot screens, cinemas, B6's notice) draws into the theater screen's
SubViewport, shown on a quad 2.5 m ahead of the seated eye. The controls never leave the root's
tree, so the one UI codebase serves both (Decision 2) and pad and keyboard input are the flat
game's.

**Goal.** In VR every 2D screen (launch menu, campaign boards, briefings, hangar, pause, results, load
and boot screens, MPEG cinemas) is drawn on a floating quad in front of the seated player, at a
readable size, and works with pad and keyboard focus as today.

**Evidence (confidence: traced to the Godot 4.7-stable source for the seam, the stereo root's
layout size and the colour conversion; measured on the stand-in for the hosting, the input, the
placement precision and the golden; lead-only for readability and comfort until the author's run).**
The fetched sources are in `.scratch/C10-src`, the captures and instruments in `.scratch/C10`.
- **No 2D under a stereo root, confirmed.** `RendererViewport::_draw_viewport` sets `can_draw_2d` only
  for a view count of one (`renderer_viewport.cpp`), A2's SHOT-44.
- **The seam is `CanvasLayer.custom_viewport`.** `set_custom_viewport` re-attaches only the layer's
  canvas to another viewport's RID (`canvas_layer.cpp`). A control registers as a GUI root with
  `get_viewport()`, the tree's viewport (`control.cpp`, `NOTIFICATION_ENTER_CANVAS`), anchors to
  `get_viewport()->get_visible_rect()` (`get_parent_anchorable_rect`), and is picked through its
  layer's own transform (`Viewport::gui_find_control`). So a hosted control keeps the root's layout,
  focus and mouse picking, and the board menus poll `Input` through `MenuInput` regardless. One
  engine detail binds: entry connects the parent's `child_order_changed` to the root and exit
  disconnects from the current viewport, so a layer is pointed back at the root on `tree_exiting`,
  which Godot emits before `NOTIFICATION_EXIT_TREE` (`CanvasRehost.Return`).
- **A stereo root's 2D layout is one eye's render target, not the window.**
  `Viewport::_check_xr_size` sets the root's size to the interface's `get_render_target_size()` every
  internal process, and `Window::_update_viewport_size` returns early under XR, so content scale is
  ignored. The stand-in's layout is 640x720; the author's VDXR run reported 3072x3264 per eye (A1).
  Every board lays out against that rect: `BoardFit` letterboxes the 800x600 board top and bottom in
  the 0.89-0.94 aspect, and Built-in fills it. The screen therefore shows the whole layout, sized so
  the inscribed board is the seat's height, and drawn at two texels per authored board pixel
  (1600x1800 on the stand-in, 1600x1700 for the VDXR eye). Hosted text rasterises at the root's
  oversampling (`canvas_item.cpp`), so the root's `oversampling_override` follows the surface scale.
- **The golden.** `menu-original-xr-sim` (`--menu --presentation=original --xr-sim`, frame 120):
  `9f763799` on three probe runs and the goldens stage; frame 121 moves 7.89 % of pixels, all inside
  the screen. With the theater built, `menu-original-flag` (flat) and `c1-cockpit-xr-sim` stay
  hash-identical (the screen logs `hidden in flight`); the goldens stage was 28 of 28.
- **Placement precision in flight.** The quad takes A3b's two pieces: a node at the world origin in
  the XR origin's axes, and the cockpit's eye-feed vertex stage. A scratch test card on the screen in
  a flown C1 burst (`--view=cockpit --no-cockpit-pass --hold=0,0,0,0.6
  --xr-sim=eye=1366x1366,fov=100,shot=left --frames=60 --shots=6`, card edges located subpixel,
  `.scratch/C10/C10_jitter.py`), worst frame-to-frame edge step:

  | Placement | 0 km | 20 km |
  |---|---|---|
  | eye feed (landed) | 0.000 px | 0.000 px |
  | Godot's own transform (control) | 0.053 px | 0.725 px |

  The control is about 1.2 px at 20 px per degree, so the instrument can fail (METHOD-9).
- **A `Color` handed to a 3D shader uniform arrives linear** (verification SHOT-47): the surround's
  sRGB 0.075 floor drew 2 of 255 when its shader linearised it again, 19 as a `Vector3`. C14's
  `HeadLockedQuads` hands its tint as a `Color` and linearises it again (finding recorded under C14).
- **Input, in the suite `theater-screen`.** A real Built-in pause board on a board layer moves its
  cursor on a keyboard Down and fires the row on Enter, delivered through `Input.ParseInputEvent`. A
  pointer ray from a hand off the eye through each row's middle lands on that row through
  `Hit`/`LayoutPoint`, and a Godot `Button` on the screen takes a click pushed into the root at the
  seam's point. With `CanvasRehost`'s hand-over removed the suite fails.
- **The desktop window and mouse.** OpenXR's `post_draw_viewport` blits the left eye to the window,
  Godot's default, kept: it shows the screen in its surround (the stand-in shows both eyes). The
  desktop mouse lands at window pixels on the eye-size layout from its top left (the root's final
  transform is identity under XR), so it reaches part of the layout and not where the mirror shows
  the screen. It is not a supported VR input; C11's pointer is.
- **Colour.** With no flight live the camera takes a plain environment (Linear, no glow), so board
  colours are exact under either look. In flight the world's environment still applies to the
  screen: under Enhanced the pause and results boards are tonemapped by AgX (SHOT-46), owed at the
  headset.

**Approach (landed, VR only).** Built by `Launch/Launcher.cs` right after `XrSession.Boot`, never in a
flat launch or under `--run-tests`.
- `UI/Boards/CanvasRehost.cs` takes every root canvas layer whose number a predicate accepts, as it
  enters the tree (`SceneTree.NodeAdded`) and by a sweep at attach, and hands it back on
  `tree_exiting` and at detach. Layers under another viewport (splitscreen panes, the cockpit pass)
  are left alone.
- `UI/Boards/TheaterLayout.cs`, engine-free: `Hosts` (from `HudLayers.Board` up), `Raises` (a board
  or a cinema raises the screen in flight; the readouts at 11 and 13 ride along), the seat, the quad
  size, the surface size, `Hit` and `LayoutPoint`.
- `UI/Boards/TheaterScreen.cs`, a `Node3D` at process priority 2100 (after `XrRig.Place`): the
  transparent SubViewport `Surface` (no 3D, no GUI input, the root's canvas filter and snap copied),
  the screen quad (premultiplied sRGB taken to linear, four taps over each pixel's footprint since a
  viewport texture has no mipmaps, no depth test, priority 126) and the surround sphere (priority
  125, centred on the eye in view space, the origin's axes). Flight is live once the rig has a pane
  and the session's first frame is ready; until then the screen shows in a full surround and the
  camera wears the plain environment. In flight a showing board or cinema raises the screen over the
  world dimmed to 60 %; otherwise neither draws and the surface stops updating. Its state changes log
  `theater: ...` at Debug.

**Resolved TODOs (proposed values, TUNE for the author).**
- Size and distance: the board 1.53 m tall at 2.5 m (34 degrees), its centre 0.1 m under the eye.
  An authored pixel then spans about 1/18 degree, a little over one headset pixel at 20 to 25 px
  per degree; the surface draws 35 rows per degree, so the headset minifies it about 1.4x.
- Surround: a dark two-tone room, floor sRGB 0.075 under a 0.015 ceiling with a soft horizon at eye
  level, opaque and drawn over the world whenever no flight is live, so no world frame (the last
  session's or one still loading) ever shows behind the menus.
- In flight: the world dims behind a board, to 60 % surround opacity, since a halt or a results
  board leaves nothing in the world to watch and the dim eases the stereo conflict of a screen at
  2.5 m drawn over a dash at 0.5 m.

**Contracts for C11, C12 and D15.**
- C11 (pointer): express the controller's ray in the XR origin's frame (an `XRController3D` under the
  rig's `Origin` has it as its `Transform`) and call `TheaterScreen.Hit(from, dir)`; `LayoutPoint(uv)`
  is the hosted controls' own coordinate. Push `InputEventMouseMotion`/`InputEventMouseButton`
  carrying it as `Position` and `GlobalPosition` through the root's `PushInput(event, true)`: Godot
  controls and the event readers (`LaunchMenu`'s rows) take it, as the suite shows. The polled
  readers (`AuthoredPointer`, `PauseBoard.SeatPointer`, `PausePreferences`) read the OS cursor through
  `GetMousePosition`, which cannot reach an eye-size layout; give them one pointer source that the
  XR pointer overrides. `PauseBoard.PointerSource` and Original's `DebugPointer` are the existing
  doors. The Launcher holds the screen as `_theater`; hand it to `LauncherContext` when a session
  needs it.
- C12 (HUD panel): reuse `CanvasRehost` with the panel's own SubViewport and a predicate over the
  HUD layers, place the quad with the same eye-feed vertex stage, and draw at the reserved priority
  115, under the theater's 125/126. The HUD lays out against the same eye-size root rect
  (`HudMetrics` reads its height). The root's `oversampling_override` is one value per process and
  the theater sets it to its surface scale; the panel shares it.
- D15 (stereo cutscenes): the screen's shader is the place to pick a left and a right texture by
  `VIEW_INDEX`; the screen plane at 2.5 m is the pair's zero-parallax plane. A presenting cutscene is
  not a canvas layer, so it needs its own condition beside `Raises` to bring the screen up in flight.

**Model recommendation.** Standard; the one finding that needed the source read was the stereo
root's eye-size layout.

**Verify.** Units: `CSVM.Tests/TheaterLayoutTests.cs` (hosting and raising by layer, two texels per
authored pixel on four layouts, the seat's 34 degrees, the inscribed board's height, the hit law and
its misses). Suite `theater-screen`. The golden `menu-original-xr-sim`; the 27 existing goldens
unmoved.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on'
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --force-builtin
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --movie=zipper
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit --no-cockpit-pass
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --graphics=enhanced --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit --no-cockpit-pass
```
Look for: (1) at the Original menu and a campaign board, the smallest text reads without leaning in,
and the board fits the view without turning the head; the log carries `theater: layout ...` with the
runtime's eye size; (2) the surround reads as a dark room with a floor, not a void, and no sky or
world shows behind the menus; (3) Built-in's menus read at the same size; (4) the `zipper` cinema
plays on the screen at its own aspect with sound; (5) in flight, Esc raises the pause board over a
dimmed world and Down/Enter or the pad drive it, and the board drawn over the dash is comfortable to
read, with Resume taking the screen away; (6) under Enhanced the pause board's colours against (5).
The values to tune are `TheaterLayout.Default` and `TheaterScreen.DimOpacity`, and the surround's
two tones.

**⚠ Traps.**
- Do not size the screen or the surface from the window: under XR the root's layout is the eye's
  render target, and Godot ignores the window's content scale.
- Do not hand a hosted layer back to the root after it has left the tree, or set its custom viewport
  before it enters; either breaks Godot's signal pairing and prints an engine error on exit.
- A canvas layer added under `HudLayers.Board` that should show in the headset is invisible there;
  either give it a board layer or a stereo form of its own.
- Do not depth-test the screen: in flight it stands behind the dash and would be cut by it.
- Pass colours to the screen and surround shaders as vectors (SHOT-47).

**The objectives readout in a stereo flight (D17, C12's finding; confidence: traced to code and
checked on the stand-in).** C12 feared `UI/Overlays/ObjectivesHud.cs`, on `HudLayers.Board`, would
raise the screen over a dimmed world whenever it showed. It shows only while the session is paused:
it is the Built-in pause screen's objectives list (`_Process` sets its layer visible from
`PauseState.Paused`; the Original pause sheet carries its own and the readout is not built beside
it). So in flat play it is part of a board, not a HUD readout, and on the theater it rides the pause
board, which raises the screen anyway. On the stand-in (`--xr-sim --profiles=<scratch copy>
--campaign=X1:6 --plane=player_bhawk --force-builtin --det --mute --log=ui:debug`, frame 150) a
flight logs `theater: hidden in flight` with the readout's layer among the five hosted; with
`--debug-pause=60` it logs `theater: over flight, raised by pause_board`, and the capture shows the
objectives panel at the screen's top right beside the pause board in both eyes. No code moved; the
suite `campaign-objectives-hud` now checks that the readout's layer is one the theater hosts and that
it hides on resume and shows again on the next pause. Its text size on the screen is part of check
(5) above.

**Verified.** The complete battery on the merged tree with B8, C10 and the head-locked quads' tint
fix: build PASS, units 6392 passed 3 skipped, engine 535 passed 2 skipped with clean engine errors,
goldens 29/29 hash-identical. The headset checks named above are still owed.

## C11 ◐ `DeviceKind.Xr`, XR actions and the screen pointer

**Outcome: built and pinned on the stand-in; the bindings on Touch controllers and the pointer's
feel are owed at the headset.** The two hand controllers are one device in the binding seam
(`DeviceKind.Xr`, appended), shipped on three flight actions, all on the left hand, rebindable on
both Controls pages under a headset. The right hand's laser points and clicks on the theater screen
through C10's seam, and every polled reader takes it through one pointer source. The project ships
its own OpenXR action map, `CSVM/openxr_action_map.tres`.

**Goal.** XR controllers are a device kind in the binding seam with default bindings for recenter,
pause and view cycle, rebindable in the controls screen, and a laser pointer clicks the theater
screen. Meta Touch is the first profile.

**Evidence (confidence: traced to the Godot 4.7-stable source for the action map load, the tracker
names, the input store and the pose frame; measured on the stand-in for the binding resolution, the
pointer and the goldens; lead-only for anything a headset shows until the author's run).** The
fetched sources are in `.scratch/C11-src`.
- **The action map.** `OpenXRInterface::_load_action_map` (`modules/openxr/openxr_interface.cpp`)
  loads `xr/openxr/default_action_map` (default `res://openxr_action_map.tres`) when it exists. Else
  it builds Godot's default in memory and, under `TOOLS_ENABLED` only, saves it there, which is A1's
  untracked side effect. An export without the file therefore runs on the same in-memory default.
  C11 ships the file so the action names the code reads are pinned in the repo and the editor
  binary stops writing it: generated by Godot 4.7's own `OpenXRActionMap.create_default_action_sets()`
  in a headless `-s` script (`.scratch/C11-gen-action-map.gd`), and line for line the copy A1's
  headset run wrote apart from the sub-resource ids (621 lines each). It holds four profiles: KHR
  generic controller, Meta Touch, Pico 4 and EXT hand interaction. Godot 4.7's default has no Index
  or Reverb profile, so those reach the game through the runtime's own remapping (SteamVR's binding
  screen) until a profile is added to the file onto the same action names.
- **The trackers.** OpenXR names the hands' `XRControllerTracker`s `left_hand` and `right_hand` and
  writes every action into them by its action-map name each frame (`handle_tracker`). It renames the
  pose actions (`aim_pose` is the tracker's `aim`). A hand whose interaction profile is null is
  skipped, leaving its last inputs in place, so `XrControllers` reads zero unless the tracker's
  profile is set and not `/interaction_profiles/none`. `XRPose::get_adjusted_transform` applies the
  world scale and the reference frame, which puts the aim in the XR origin's frame, the frame C10's
  `TheaterScreen.Hit` takes.
- **The defaults, against Meta Touch.** The map's Touch profile binds `menu_button` to the left
  menu click and the right system click; the right system (Meta) button belongs to the runtime
  (its long press is the runtime's own recenter, B7's `PoseRecentered` path). Pause takes the left
  menu button, the one app menu button Touch has. Recenter Headset takes the left stick click, a
  deliberate press no flight action holds. Cycle Cockpit Views takes Y, the left upper face button.
  All three sit on the left hand because the right holds the laser by default. Each row ends with
  its XR binding, after the keyboard and pad ones.
- **The binding seam, measured.** Suite `xr-pointer`: with the stand-in's hands connected
  (`XrSimInterface.ConnectHands`, `SetHandInput`), a seat's own `SeatDeviceState` resolves the left
  menu button, Y and the left stick click to Pause, Cycle Cockpit Views and Recenter Headset through
  the shipped profile, and the same held buttons read nothing once no session is live.
- **The pointer, measured.** Same suite, a Built-in pause board raised on a theater screen at a
  rotated seat 7.6 km out: the right hand's aim through Photo Mode's middle lands on it (under 1
  layout pixel), the beam and dot draw, the polled reader moves the cursor there and a trigger pull
  and release fire it. A Godot button takes the trigger as a pushed click; a left trigger pull hands
  the laser over without a click and its next pull clicks. With the pause board's override read
  removed the suite fails three checks (cursor, fire, left-hand fire). Suite
  `xr-pointer-original-menu`: on the Original presentation over the install's layout, seat 0's
  polled pointer wired as the launcher wires it, the laser alone opens PREFERENCES, CONTROLS, KEYS
  AND BUTTONS and the Other tab, whose Pause row lists "VR Menu" and Recenter Headset "VR L Stick
  Click" under a session and neither without one.
- **Flat play.** The goldens stage: 29 of 29 hash-identical. A first run with two other sessions'
  Godots running flipped `c1-cloud-deck-enhanced` once (`7bb03429` to `5bf9aeb1`, a freecam Enhanced
  shot no C11 code reaches); the same build's rerun was 29/29.

**Approach (landed; VR only at run time).**
- `Bindings/XrControls.cs`: `XrButton` and `XrAxis` (the indices an `Xr` binding carries, named by
  role: lower and upper face, menu, stick click, trigger, grip), each one action of the map on one
  hand, the Touch captions, and the two triggers marked as the pointer's. `Bindings/XrControllers.cs`:
  the `IDeviceState` over `IXrHands` (the trackers through `XRServer`), plus each hand's aim pose.
  It reads nothing until `XrControllers.SessionLive`, which `XrSession.Build` sets, so a flat launch
  makes no XR call. `SeatDeviceState` hands every `Xr` read to it on the pad half.
- `DefaultBindings`: the three rows above. `BindingStore` version 4 adds the `xr` device token
  (`xr/button:LeftMenu`, `xr/axis:RightStickY+@0.5`); a file older than 4 keeps the shipped `xr`
  binding on every row it names, since it could name none. `ControlCapture` scans the hand buttons
  and sticks bar the triggers. `BindingLabels` reads "VR Menu", "VR Y", "VR L Stick Click".
  `ActiveDevice` names an `Xr` binding last on the pad side; `ControlGlyphs` draws none for it.
- `ControlsFeature.ShowsXr` (default: `SessionLive`) leaves `Xr` bindings out of every list it hands
  a screen, so both Controls pages read as before in flat play; under a headset the rows show and
  capture.
- `UI/Boards/TheaterPointer.cs`, built by `Launcher` beside the theater screen: the pointing hand's
  aim ray through `TheaterScreen.Hit`, a 3 mm beam and a 12 mm dot (priority 127, over the screen,
  placed from the eye feed), moves and trigger clicks pushed through the root's `PushInput(event,
  true)` at `LayoutPoint`, a click starting only on a fresh pull over the screen. The other hand's
  trigger takes the laser over, swallowed; a hand that leaves hands it over too.
- `UI/Boards/ScreenPointer.cs`, the one pointer source: `AuthoredPointer` (Original pause and race
  boards), `PauseBoard.SeatPointer`, `PausePreferences`, the campaign film's held press and seat 0's
  pointer in `Launcher` (the Original menus) read it. It is the desktop mouse call for call until
  `TheaterPointer` overrides it, which it does only while a controller is present, so a headset
  with no controllers, and every `--xr-sim` launch, keeps C10's behaviour.
- `XrSimInterface.ConnectHands`, `AimHand` and `SetHandInput`: two Touch hand trackers for suites.
  A launch has none, so no golden draws a beam.
- `LauncherContext` was not needed: the pointer is process-wide like the screen and built beside it.

**Resolved TODOs.**
- The action map: shipped, as Godot's own default (above), loaded by Godot's default path; no
  project setting changes.
- The pointing hand: right; the other hand's trigger switches it, the convention of the runtimes'
  own dashboards, so no option row is needed.
- Rebinding under a headset only: a flat launch has no controllers to capture from.

**Model recommendation.** Standard; the one finding that needed the source was the stale inputs of
an unbound tracker.

**Verify.** Units: `CSVM.Tests/XrBindingTests.cs` (the appended kind, the control table, the
reader over a fake for present and absent hands, the shipped rows, a resolution, a rebind's steal,
a capture that takes hand buttons and sticks and never a trigger, the file tokens and the version
rule, the captions, the prompt order, the screen's `ShowsXr`), `TheaterLayoutTests.PointAtIsWhereAHandRayLands`,
and the updated `DefaultBindingsTests`, `BindingStoreTests`, `ThrottleLeverTests`,
`ControlsResetStickTests`. Suites `xr-pointer` and `xr-pointer-original-menu`, beside
`theater-screen`, the Controls, pause and menu-pointer suites. The goldens stage, flat ones unmoved.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, then SteamVR (`XR_RUNTIME_JSON`
at `C:\Program Files (x86)\Steam\steamapps\common\SteamVR\steamxr_win64.json`), Touch controllers on:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on'
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --force-builtin
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
```
Look for: (1) at the Original menu a thin beam from the right controller and a dot where it meets
the screen; the right trigger clicks PREFERENCES and the plaques under the dot, and nothing clicks
when the trigger is pulled off the screen and swept onto a plaque; (2) Built-in's rows highlight
under the dot and click on the trigger; (3) the left trigger moves the laser to the left hand without
clicking, and the right trigger moves it back; (4) Options, CONTROLS, KEYS AND BUTTONS, Other tab:
Pause lists "VR Menu", Recenter Headset "VR L Stick Click", and Views 1's Cycle Cockpit Views
"VR Y"; click a cell and press B: it binds "VR B", and a trigger pull binds nothing; (5) in flight,
the left menu button pauses and the laser clicks Resume; Y cycles the views; the left stick click
logs `xr: recentered (the recenter action)`; (6) the pointer's feel: beam and dot size and colour,
the dot's steadiness on the screen, the click latency (`TheaterPointer.BeamWidthM`, `DotM`, the
tints are the values to tune); (7) with both controllers off, the beam is gone and the desktop mouse
behaves as C10 left it.

**⚠ Traps.**
- Do not read a hand's inputs without its profile check: OpenXR stops writing an unbound tracker,
  so its last press would stay held.
- The tracker's pose is `aim`, not the action map's `aim_pose`; Godot renames it.
- A suite aiming a stand-in hand at the screen needs the screen's layout size first (a `Refresh`):
  a zero size divides into a NaN pose, which Godot reports as `!v.is_finite()` from
  `instance_set_transform`. The pointer now ignores a non-finite aim.
- The plan worktree holds A1's untracked `CSVM/openxr_action_map.tres`, which this item's tracked
  file collides with on merge; remove the untracked copy first.

**Verified.** The complete battery on the merged tree with B9 and C11: build PASS, units 6417
passed 3 skipped, engine 541 passed 2 skipped with clean engine errors; goldens 28/29 with
`c1-cloud-deck-enhanced` moved while the author's headset session ran on the same GPU, then 29/29
hash-identical on two goldens re-runs (#114's pattern). The headset checks named above are still owed.

## C12 ◐ The cockpit-locked HUD panel

**Outcome: built and measured on the stand-in; legibility, distance, angle and glance comfort are
owed at the headset.** A stereo session parents the flown pane's flight chrome (the HUD and its
message stack, the held scores, the chat) into a 2560x1440 surface, the HUD's own reference frame,
and shows it on a quad in the XR origin's frame. The quad is the flat first-person pane set 2 m ahead
of the seat: every surface pixel lands where the flat Nose view draws it. It rides the seat in
Cockpit and Nose and the rigid chase pose in Chase, never the head. Flat play builds none of it; the
24 flat goldens are hash-identical.

**Goal.** In VR the flat HUD chrome (gauges, compass tape, messages, chat, prompt line, scores) is
drawn on a quad fixed to the cockpit 1.5-2 m ahead and slightly low, readable with a glance.

**Evidence (confidence: traced to code and the Godot 4.7-stable source for the layout and the text
rasterisation; measured on the stand-in for placement, precision and the goldens; the sizes are a
judgement from headset density, TUNE for the author).** Tools and captures are in `.scratch/C12/`
(`C12_probe.ps1`, `C12_burst.ps1`, `C12_jitter.py`, `C12_register.py`, `C12_compass.py`,
`C12_gold.ps1`, `C12_diff.py`); the fetched sources are in `.scratch/C12-src`.
- **Reparent, not rehost.** C10's contract proposed `CanvasRehost` over the HUD layers. A rehosted
  control keeps the root's layout, and under XR the root's layout is one eye's render target
  (2112x2304 on the author's VDXR, 640x720 on the stand-in): an aspect near 0.9 that no HUD layout
  was tuned for, at a size the runtime and a render-scale change decide. Text on a rehosted layer
  also rasterises at the root's `oversampling_override`, which the theater sets to its own surface
  scale (`canvas_item.cpp` reads `get_viewport()->get_oversampling()`). The splitscreen seam already
  parents a pane's HUD into a SubViewport (`FlightController.HudParent`), so the panel takes that
  seam: `PlayerRig.ChromeParent` is the panel's `PanelSurface`, read by `HumanFlightAdapter` (the
  HUD and messages layers), `ScoresOverlay.Attach` and `SessionNet.WireChat`. The surface is its own
  viewport, so text rasterises at its own oversampling of 1, and its layout is fixed whatever the
  runtime. The world overlays (whiteout, flare, flash) stay on `HudParent`; the headset draws them
  as quads of their own (C14).
- **`HudMetrics` takes the panel as the window.** `WindowSize(control)` is the panel surface's size
  for a control whose viewport is a `XrHudPanel.PanelSurface`, else the root window, as before;
  `Scale`, `PaneFactor` and `ReadingBox` read it. On the panel the HUD lays out at scale 1, pane factor
  1, reading box the whole 2560x1440, exactly the 1440p reference frame (`xr-hud-panel`).
- **Proposed sizes (TUNE).** Distance 2 m, the far end of the decision's range and the head ring's
  own distance (C13), so a ring arrow over the panel sits within 3 % of its depth rather than in
  front of it. Axis 4.70° under the seat's: the original's first-person head tilt
  (`PlaneBuilder.HeadPitchOffsetRad`), the decision's "slightly low", and the axis the flat Cockpit
  and Nose views are centred on. Height 46.83°, the flat Nose and Chase view's
  (`CameraController.ExternalFovDeg`), so a quad 3.079 x 1.732 m, 76.7° wide. That is 30.8 surface
  rows per degree: the smallest chrome text, the 19.2-row readout rung (the dogfight status line, the
  stunt status), spans 0.62°, about 13 px at the author's 2112x2304 per eye (about 21 px per degree);
  the message stack's 30 rows span 0.97° and the prompt line's 48 rows 1.56°. Legibility sets the
  floor: a panel half that tall would put the readout rung near 6 px. The flat layout keeps its
  places, so the dial columns stand about ±28° out and 11° to 24° under the seat's axis, the heading
  tape 18° over it and the message stack 9° over it; a glance reaches the messages, a small head
  turn the dials. In Cockpit the flat HUD already drops the screen dials and tape for the interior's
  own, so the panel carries text there.
- **The panel is the flat first-person pane, measured.** The stand-in's left eye on the flat Nose
  camera (`--stage=empty --plane=player_bhawk --pos=0,300,0 --direction=-0.743,0,-0.669 --view=nose
  --hold=0,0,0,0.6 --frames=60`, `--xr-sim=eye=1280x720,fov=75.178179,ipd=20,head=0.01/0/0,shot=left,
  pitch=-4.70001`) against the flat frame of the same pose, least-squares subpixel shift per element
  (`C12_register.py`): all five dials (-0.000, -0.000) px. Control: the flat frame resampled half a
  pixel reads 0.500. The heading tape's bar edges differ by up to 0.5 px (`C12_compass.py`); its
  ticks are point-sampled and the flat 720-row frame lays the HUD out at scale 0.5, so its edges fall
  on half pixels the panel's 1440 rows draw whole. Lead, not separated further.
- **Precise far from the origin (A3's method).** The panel takes A3b's two pieces: the quad at the
  world origin in the XR origin's axes (`XrHudPanelLayout.Placement`) and the theater's eye-feed vertex
  stage. A scratch magenta card on the panel, flown C1 bursts (`--chapter=C1 --plane=player_bhawk
  --pos=X,300,0 --direction=-0.743,0,-0.669 --view=cockpit --hold=0,0,0,0.6
  --xr-sim=eye=2732x2732,fov=100,shot=left --frames=60 --shots=6`, 27 px per degree, card edges
  located subpixel by `C12_jitter.py`), worst frame-to-frame edge step:

  | Placement | 0 km | 20 km |
  |---|---|---|
  | eye feed (landed) | 0.010 px | 0.010 px |
  | Godot's own transform (control) | 0.019 px | 1.371 px |

  The 0.010 px is the instrument's own drift (left and right edges part symmetrically as the world
  behind the card changes) and is the same at both distances. The control is 0.05° at 20 km, the
  magnitude A3 measured on a world-placed HUD quad, so the instrument can fail (METHOD-9). The
  arithmetic is pinned engine-free too (`XrHudPanelLayoutTests`): at 20 km on a rotated, rolled
  origin the panel's path recovers a corner's eye-frame position within 20 µm where Godot's own
  composition misses by more than 200 µm.
- **Follows the seat, not the head** (suite `xr-hud-panel`, a rig over a live stand-in interface,
  20 km out on a rotated heading): the panel stands at its pose in the origin's frame; the stand-in
  head moved 116 mm, turned 30° and pitched 12° leaves the quad's transform unchanged; a turned aircraft turns it
  51.5° with it, at the same pose in the seat; before a rigid chase pose it stands at the same pose
  ahead of the chase eye. Control: the panel built on the head's transform fails the head check by
  1161 mm.
- **Chase, decided: the same panel relative to the origin.** B9 makes the origin the view's own
  frame in every view (the seat in Cockpit and Nose, the rigid chase pose, a level held heading for
  the static cameras), and the flat game draws one HUD overlay in every view, so the panel stays the
  view's pane: 2 m ahead of the chase eye, turning with the aircraft in the rigid chase and level in
  `--xr-chase=level`. A panel kept at the cockpit seat would hang 15-26 m ahead of the chase eye as a
  small card over the aircraft. The flat crash and static cameras hide the HUD layer, and the panel
  hides with it (it shows only while a chrome layer draws).
- **Compositing.** Each surface texel is taken from premultiplied sRGB and mixed over the frame in
  gamma space as a canvas layer is (the cards' `GammaMixComposite`); a clear texel is discarded.
  Depth test off, priority `HudPanelPriority` 115: over the world cards (112) and the interior's
  glass (110), under the ring (116), the sun wash (120) and the fade (121), as the flat HUD sits over
  the cockpit pass. Under Enhanced the AgX tonemap acts on it as on the cards (SHOT-46), owed at the
  headset with C10's boards.
- **Goldens.** New `stage-hud-panel-xr-sim` (empty stage, nose view, both 640x720 eyes, frame 60):
  `23f8dde9`, one hash over seven runs, frame-sensitive 31.06 %. Moved and re-pinned, each one hash over
  three runs: `c3-flare-xr-sim`, `c1-moon-xr-sim` and `stage-markers-xr-sim` (the nose view's dials and
  tape now draw on the panel), and `c1-cockpit-xr-sim` (1 px at 1 level) and `c1-cockpit-xr-sim-aft`
  (272 px, at most 4 levels, on the collimated reticle's lines alone). The two Cockpit shots show no
  panel texel (the surface was read back fully clear at frame 100); hiding the quad restores both
  hashes while the HUD stays on the surface, and the same move remains with the quad's screen read
  removed (verification GOLD-20, and the finding for C13b below). `c1-cockpit-xr-sim-far`,
  `stage-gunsight-xr-sim` and `menu-original-xr-sim` are unmoved; the 24 flat shots are
  hash-identical.

**Approach (landed, VR only).** `Flight/Hud/XrHudPanel.cs` (the node, its `PanelSurface`, the quad and
its shader) and `Flight/Hud/XrHudPanelLayout.cs` (the geometry, engine-free). `Launch/GameSession.cs`
builds it beside `XrMarkers` when the launcher hands it an XR rig and sets `_rigs[0].ChromeParent` to
its surface before any seat's HUD is built. `HudMetrics.WindowSize`; `PlayerRig.ChromeParent`, read by
`HumanFlightAdapter`, `ScoresOverlay` and `SessionNet.WireChat`. A flat seat's `ChromeParent` is null,
so every flat path parents exactly where it did.

**Resolved TODOs (proposed values, TUNE for the author).** The panel's pixel size 2560x1440, distance
2 m, axis 4.70° under the seat's, height 46.83° (above). The tier: Standard; the deciding facts (the
eye-size root layout, the rasterisation scale) came from reading the source.

**Model recommendation.** Standard.

**Verify.** Units: `CSVM.Tests/XrHudPanelLayoutTests.cs` (the reference frame, the pose on the
first-person axis facing the eye, every surface point on the flat Nose view's pixel, the 46.83°
span, the eye-feed placement's small numbers, the 20 km arithmetic). Suite `xr-hud-panel`. Suites
`-Filter xr`, `hud`, `scores`, `chat`, `splitscreen`, `head-locked`, `theater`, `cockpit` pass with
clean engine errors. Goldens as above. The registration and the bursts re-run with the commands above
and the `.scratch/C12/` tools; the bursts need the scratch card, which is not in the tree.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, Original then Enhanced:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=nose
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit "--ai=player_fury:n=2" --ai-attack --target=nearest
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=chase
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-11500,300,-11500" "--direction=-0.743,0,-0.669" --view=nose
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --stunt --chapter=C1 --plane=player_bhawk --view=nose
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=nose --graphics=enhanced
```
Look for: (1) legibility: in Nose (first line) the dial faces, their figures and the heading tape
read without leaning in, and in the stunt run (fifth line) the status line's small text reads; say
if the panel should be larger (`XrHudPanelLayout.Default`'s 46.83°) or nearer or farther (2 m);
(2) angle and glance comfort: the dial columns sit low left and right and the tape and messages
above the gunsight; say whether reading a dial with a glance and a small head turn is comfortable,
or the panel should drop further below the seat's axis (4.70°); (3) the panel stays fixed to the
aircraft as you turn and lean your head, and banks with the aircraft; (4) in Cockpit (second line)
the kill and crash messages appear on the panel over the dash and read at its depth, with no doubled
image against the dash; (5) overlap with the ring: a Fury below and behind puts its ring arrow and
label over the lower panel; say whether they read; (6) Chase (third line): the dials hang 2 m ahead
of the chase eye, banking with the aircraft; (7) at the far corner (fourth line) the dials hold steady
with no shimmer; (8) under Enhanced (last line) the dial colours against the flat game.

**⚠ Traps.**
- The world-tied markers (C13) must leave the panel, otherwise they are drawn at panel depth;
  `XrMarkers` sets `MarksInWorld` on the three marker controls, and the panel leaves them be.
- Do not rehost the HUD layers onto the panel through `CanvasRehost`: a rehosted control lays out
  on the eye-size root and rasterises its text at the theater's oversampling.
- Do not size anything on the panel from the root window: under XR the root is an eye's render
  target. `HudMetrics.WindowSize` is the one door.
- The panel shows whenever a chrome layer holds a visible item, which includes the chat and scores
  layers during a presenting cutscene; D15's pair cameras would draw it (finding below).

**Findings for other items.**
- D15: the panel can show during a presenting cutscene (the cutscene hides the HUD's own readouts
  through `SetPilotHudVisible`, not the chat and scores layers or the dogfight HUD), so its quad
  belongs on D15's `SplitScreen.HeadsetLayer`; this tree lacks the constant, so set
  `XrHudPanel`'s quad `Layers` to it at the merge.
- C13b: the collimated reticle's overlay and the interior glass share priority 110 and, lifted, one
  sort depth, so their order is the renderer's to choose and moves when any other transparent joins
  the frame (the HUD panel's fully discarded quad moved the reticle's lines by up to 4 levels, about
  the glass's 0.02 tint alpha times the line). A distinct priority or sorting offset for the overlay
  would pin it. Lead: the order swap is inferred, not read out of the renderer.
- C10: `UI/Overlays/ObjectivesHud.cs` draws the campaign objectives readout on `HudLayers.Board`, so
  in a stereo flight it goes to the theater and, being a board layer, raises the screen over a
  dimmed world when it shows. Not checked at the stand-in.

**Verified.** The complete battery on the merged tree with D15 and C12: build PASS, units 6462
passed 3 skipped, engine 546 passed 2 skipped with clean engine errors, goldens 34/34
hash-identical. The headset checks named above are still owed.

## C13 ◐ World-tied markers and the edge-marker ring

**Outcome: built and measured on the stand-in; the headset check is owed.** At the headset the
author reported, verbatim: "Target marker is missing", and after C13b: "But can we have the floating
reticle like in flat too? its better for gun targeting." A stereo session now draws the flat game's
gun pipper, the selected target's box and label, and every flight tag (hostile fallback, race
pilots, `--debug-markers`, dogfight opponents) on cards at their world points in both eyes, drawn by
the flat HUD's own code. A marker more than 25° off the head's view axis becomes an arrow and its
label on a ring hanging on the head. Flat play draws exactly what it drew: the five flat goldens
that carry markers are hash-identical.

**Goal.** Target boxes, the impact reticle and the versus markers appear in the world at their real
depth in both eyes; off-view targets are shown by a head-relative arrow ring.

**Evidence (confidence: traced to code for what the flat HUD draws and where; measured on the
stand-in for placement, size, disparity, precision and the ring; the look, the ring's radius and the
draw-over-the-cockpit choice are the author's at the headset).** Tools and captures are in
`.scratch/C13/` (`C13_register.py`, `C13_parts.py`, `C13_shift.py`, `C13_burst.py`,
`C13_gold.ps1`).
- **What the flat game draws.** `ImpactReticle` (the pipper at 0.5 s of the selected group's flight
  along the nose), `TargetHud` (the selection's box and label block, the off-screen arrow, label and
  spyglass disc, the hostile fallback, race tags, the debug overlay) and `VersusHud`'s opponent
  markers. There is no lead marker in the flat game: the plan's "lead" was a lead-only guess, and
  the pipper is the gun's aim point. `UI/Overlays/MarkerOverlay.cs` is `--viewer` only, a dev mode
  that stays flat (Decision 15), and already draws 3D `Label3D`s.
- **The pipper and box land where the flat game draws them** (SHOT-45's method, empty stage, nose
  view, a Fury 250 m out selected, frame 60; the left eye on the flat camera with
  `--xr-sim=eye=1280x720,fov=75.178179,ipd=20,head=0.01/0/0,shot=left,pitch=-4.70001`, the nose
  view's 46.83° vertical). Centroids against the flat frame: pipper (-0.05, -0.04) px at 720 rows and
  (+0.01, -0.13) px at 1421 rows (the flat window's real height at `-Resolution 2560x1440`); the box
  (-0.05, +0.23) px at 1421 rows with the aircraft itself at (-0.14, -0.08). Size ratio 0.99-1.04.
  The label sits 0.7 px (720) to 2.0 px (1421) lower than the flat one: its font is rasterised at
  the card's 1.5x size, whose metrics do not scale exactly (the flat frame's own label-to-box gap
  differs by 4 reference pixels between 720 and 1421 rows). Control: the stereo crop resampled half
  a pixel reads +0.49 px.
- **Disparity is the target's** (`--xr-sim=eye=2560x1440,fov=40,ipd=100,pitch=-4.70001`, both eyes,
  3517 px/rad, least-squares subpixel shift of each crop between the eyes, `C13_shift.py`): the
  Fury's spinner 2.52 px and wing 2.51 px (140 m), the box's brackets 2.49 px (141 m), the label
  2.45 px, the pipper 0.76 px (463 m; it starts at 430 m and the range smoother walks it out). A
  collimated marker reads 0. With the head turned (`yaw=8,pitch=4`, fov 50): aircraft 1.92 px, box
  1.92 / 1.95 px, label 1.88 px, pipper 0.60 px (458 m), and the box and label keep their shape
  (`c13_turn_zoom.png`). A thresholded centroid misread the pipper's disparity as 0.18 px
  (verification SHOT-48).
- **Placement precision: world space is enough.** Pipper burst on the empty stage, nose view,
  `--xr-sim=eye=2732x2732,fov=100,shot=left --frames=60 --shots=6` (27 px per degree), worst
  frame-to-frame step 0.12 px at 0 km (the range smoother's own drift) and 0.18 px at 20 km
  (an alternation of ±0.08 px in x), at most 0.007°. The cards sit hundreds of metres out, where
  2 mm of float32 rounding is 0.005 px, so A3b's eye-feed pieces are not needed for them. The ring
  cards are placed in the head's frame by their shader and carry no world coordinate.
- **The ring** (`--xr-sim=eye=1280x1440,fov=90`, a Kestrel at 3 o'clock): the arrow points right
  on the ring with "Fury / 3 o'clock" inside it (`c13_ring_right_zoom.png`). The suite pins dead
  astern pointing down, as the flat edge marker does.
- **Facing, decided: toward the head's point, with the seat's up.** A card's normal points from its
  point to the tracked head's position, so it never turns with the head's rotation (the suite turns
  the head 0.12 / -0.15 / 0.4 rad in place and the card is unchanged), unlike Godot's billboard,
  which copies the head's view basis (B10's finding). Its up is the XR origin's up: the aircraft's
  in Cockpit, Nose and the rigid chase, world up in the level chase and static cameras. That is the
  frame the flat screen is drawn in, so a box reads as it does on the monitor and stays square to
  the cockpit when the head rolls. The suite fails (0.999641 against 0.999999) when the card takes
  the head's up instead.
- **Size: the flat pane's angle.** One reference pixel spans `2 tan(fov/2) / 1440` of the flat
  camera's live vertical angle (64.37° in Cockpit, 46.83° in Nose and Chase; under a headset the
  flat camera holds the view's angle with no speed widen, B9), times the distance. A card therefore
  covers the angle the flat HUD's pixels cover at the view centre, whatever its depth.
- **Depth test and order, decided: drawn over everything, as the flat HUD is.** The cards skip the
  depth test, so a box behind a hill still shows, and they draw over the interior as the flat HUD
  draws over the cockpit pass; a target under the nose keeps its box on the dash. Priorities
  (`HeadLockedQuads`): flash 111 < world cards 112 < HUD panel 115 (C12) < ring 116 < sun wash 120.
  The stencil alternative (hidden behind the interior's opaque parts, seen through the glass) is a
  one-line change for the author to ask for.
- **Compositing.** Each card is a transparent 2D `SubViewport` at 1.5 texels per reference pixel,
  redrawn only when what it shows changes, sampled with four taps and mixed in gamma space with the
  head-locked quads' `GammaMixComposite`, so Original matches the canvas. Under Enhanced the world
  environment's AgX tonemaps the cards (SHOT-46), owed at the headset with C10's boards.
- **Goldens.** With the cards switched off the five stereo goldens return to their old hashes; with
  them on, each moves only in a 10-row band around the pipper in both eyes (0.006-0.017 % of pixels).
  `c1-cockpit-xr-sim`, `-far`, `-aft`, `stage-gunsight-xr-sim` and `c3-flare-xr-sim` are re-pinned,
  each stable over two runs; the new `stage-markers-xr-sim` (frame 45: pipper, the selected Fury's
  box, label and debug tag, the Kestrel on the ring) is one hash over three runs. Flat
  `c1-flight-kill`, `c1-stunt-marker`, `empty-stage`, `c1-cockpit`, `campaign-4p-grid` and
  `menu-original-xr-sim` are hash-identical.

**Approach (landed, VR only).** `Launch/GameSession.cs` builds `Flight/Hud/XrMarkers.cs` beside the
head-locked quads when the launcher hands it an XR rig. Each frame (priority 2100, after the XR
origin and every HUD feed) it reads the flown pane's `FlightHud`: the reticle's impact point,
`TargetHud.WorldMarks` and `VersusHud.WorldMarks` (`Flight/Hud/WorldMark.cs`, the list the flat
`_Draw` now projects too), each only while its control shows. It sets `MarksInWorld` on the three
controls, which stops their own drawing and keeps the spyglass picture idle. Geometry is
`Flight/Hud/XrMarkerLaw.cs`. The flat drawing helpers (`TargetHud.DrawInView`, `DrawBrackets`,
`DrawLabelBlock`, `DrawArrow`, `EdgeLines`, `VersusHud.DrawArrow`) became static over a
`CanvasItem`, which the cards draw into.

**Resolved TODOs (proposed values, TUNE for the author).**
- `XrMarkerLaw.RingDeg` 25°: the flat views' 5 % inset sits 29.5° above the cockpit view's centre
  and 21.3° above the chase view's.
- `XrMarkers.RingDistanceM` 2 m, about the headsets' focal distance and the HUD panel's.
- The ring arrow's shaft is 72 reference pixels, the flat inset of a 1440-tall pane; its label sits
  inside the ring on the arrow's bearing, since the ring has no pane bottom for the flat rule's flip.
- `MaxDistanceM` 10 km: a farther marker keeps its bearing and angle there, inside the far plane.
- The spyglass disc is not drawn in the headset; D19 owns a stereo spyglass.

**Contract for C12 (the HUD panel).** Rehost the HUD canvas layers as the theater does; the markers
are already off them, because `XrMarkers` sets `MarksInWorld` on `ImpactReticle`, `TargetHud` and
`VersusHud` (the dogfight status line stays on the panel). Draw the panel at `HudPanelPriority` 115,
between the world cards (112) and the ring (116). The ring hangs 2 m ahead at 25° off the view
axis, so a panel placed low may sit under its lower arrows, which then draw over it. The flat
camera's `Fov` is what sizes the cards, so a panel that changes the flat camera's angle changes the
markers too.

**Model recommendation.** Standard; the deciding facts (no lead marker, world space precise
enough) came from reading and measuring.

**Verify.** Units: `CSVM.Tests/XrMarkerLawTests.cs` (the flat pipper's pixel size from a card at any
distance, the ring cone, bearings, dead astern, the head frame, the facing and its fallback, the
label's inward step). Suite `xr-markers` (world cards on their points, facing the head with the
seat's up and the flat angle, the far clamp, three ring bearings, a turned head leaving a card
unchanged, an empty frame hiding all). Suites `-Filter xr`, `target`, `marker`, `cockpit`,
`head-locked`, `spyglass`, `versus`, `dogfight`, `race` pass. Goldens as above. The registration,
disparity and burst re-run with the commands above and the `.scratch/C13/` tools.

Owed at the headset (the author's), VDXR at 90 Hz, Original then Enhanced:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit "--ai=player_fury:n=2" --ai-attack --target=nearest
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit "--ai=player_fury:n=2" --ai-attack --target=nearest --graphics=enhanced
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit "--ai=player_fury:n=3" --debug-markers
.\RunGame.ps1 -EngineArgs '--xr-mode','on'
```
Look for: (1) the pipper floats ahead of the gunsight as in the flat game and is usable for
gunnery: rounds fired with it on a target hit it, and it reads as one image with the target when
your eyes are on the target; (2) the selected Fury carries its box (inside gun range) and its
"Fury" label, on the aircraft at its depth, and the box stays square to the cockpit as you tilt
your head; (3) a target turning out of view becomes an arrow on a ring about a quarter of the way
out, pointing toward it with its name and clock bearing, and comes back to a box as you look at it;
say whether the ring should be wider or narrower (`RingDeg`) or nearer (`RingDistanceM`); (4) a
target below the nose shows its box over the dash: say if it should hide behind the panel instead;
(5) the third line marks every aircraft at once, tags in the world and on the ring; (6) from the
menu (fourth line) an Instant Action flight shows the same; (7) under Enhanced, the marker colours
against the flat game.

**⚠ Traps.**
- Do not use Godot's billboard modes or a `Label3D` billboard for a stereo marker: they copy the
  head's view basis, so the card turns and squashes as the head yaws (B10). Face the head's point.
- Do not size a card by a fixed angle: the flat HUD's pixel spans a different angle in each view,
  and the card must follow the flat camera's live angle.
- Do not measure a stereo marker's disparity by thresholded centroids; shift the crop between the
  eyes by least squares (SHOT-48).
- A golden's size is the manifest's 1280x720: a stereo golden takes two 640-wide eyes or one
  1280x720 eye, and `--resolution` on the hidden desktop is clamped to its height (1421 rows for
  2560x1440), so a flat comparison frame needs a stand-in eye of that height.
- `TextureUploadSourceTests` allows `ImageTexture.CreateFromImage` only in `Utils/TextureUpload.cs`;
  a suite's texture goes through `TextureUpload.Create`.

**Verified.** The complete battery on the merged tree with C13b, B10 and C13 (the stereo goldens
re-pinned on the combined tree): build PASS, units 6443 passed 3 skipped, engine 543 passed 2
skipped with clean engine errors, goldens 32/32 hash-identical. The headset checks named above are
still owed.

## C13b ◐ The gunsight reticle collimated at infinity in VR

**Outcome: built and measured on the stand-in; the headset check is owed.** A stereo session lifts
the reticle off the sight glass and draws it collimated (`Mech3/GunsightReticle.cs`): both eyes put
every reticle line on the same eye-local pixel, the flat game's place from the authored eye, while
the glass tint, its frame line and the sight body stay at the glass's 0.33-0.51 m. It is added in
linear light, an illuminated reticle, and reads over the sky above the flat pass in both looks. All
eleven airframes carry it. The flat game builds none of it; its 24 flat goldens are unmoved.

**Goal.** In VR the gunsight's reticle on the reflector glass reads at infinity, as a real
collimated reflector sight does: both eyes see it in the same direction relative to the aircraft,
so it overlays a distant target without a double image, and it reads at least as clearly as in the
flat game.

**Evidence (confidence: traced to the data for the glass and the reticle's texels; measured on the
stand-in for the disparity, the placement and the levels; the look and the comfort are the author's
at the headset).** At the headset the author reported, verbatim: "fainter and looking at a point in
the distance i have a crosseyed feeling. Cant really use it to target." Tools and captures are in
`.scratch/C13b/` (`C13b_sight.py`, `C13b_classify.py`, `C13b_shot.ps1`, `C13b_levels.py`,
`C13b_disparity.py`, `C13b_planes.py`, `C13b_gold.ps1`).
- **The reticle is baked into the glass texture, not a polygon set.** Each `cockpit1` carries exactly
  one sight glass polygon (`C13b_sight.py`; a quad on ten airframes, a six-corner strip on the
  Hoplite), 0.33 m (Warhawk) to 0.51 m (Kestrel) ahead, wearing `cockpit6` or, on the Warhawk,
  `crosshair`, which is the same 128x128 picture (identical md5). Its alpha is the tint (38 of 255 on
  12852 texels), the frame line on the outermost texels, the black sight body along the bottom edge
  (alpha 255) and the reticle: ring, cross, centre cross and the red index mark. The Kestrel's and
  the Peacemaker's glasses also stand on a post drawn as ordinary opaque geometry, which stays at its
  depth.
- **The split.** Texels off the tint by more than 6 alpha levels, the outermost ring excluded, group
  8-connected; the reticle is the group nearest the centre plus every group clear of the second ring
  (the frame corners and the sight body reach it). 902 texels on the shipped texture
  (`C13b_classify.py`, the same rule as `GunsightReticle.ReticleMask`). The glass keeps every other
  texel and paints the reticle's texels with the nearest tint colour at the tint's alpha, through
  the interior builder's texture substitute (VR only).
- **The collimated overlay.** A quad in the glass's plane, 5 cm wider each way (half of a 100 mm IPD
  at the mount scale), a direct child of the interior's root at the glass's own depth bias, drawn
  at the interior's blend priority (110) with A3b's eye-feed placement. Per fragment each eye's ray
  takes the texel the authored eye sees along the same direction (`GunsightReticle.Look`, the glass's
  plane and UV map in the interior's frame), so the reticle has the authored eye's angular size and
  place in both eyes. It shows where the head centre looks through the glass along that ray (one
  window for both eyes, so leaning out of the eye box loses it as a real sight does), and the opaque
  sight body at the fragment's own place on the glass covers it in each eye. Being depth-tested at
  the glass's depth, it hides behind whatever hides the glass (the coaming, the canopy) and draws
  over the world and anything beyond the glass, the nose spinner included.
- **Why at the glass and not at the far plane.** A vertex pushed to the far plane is depth-tested
  behind the whole world; drawn with the depth test off it would show through the panel. Looking the
  texel up per fragment on the glass's own quad gives the far plane's directions with the glass's
  own occlusion, and needs no stencil and no new priority. The order against the glass does not
  matter for an additive layer (the glass's corrected tint alpha is 0.02).
- **Disparity, measured** (`C13b_disparity.py`, C1 empty stage, Bloodhawk, `--direction=0,0.34,-0.94
  --view=cockpit --hold=0,0,0,0.6`, frame 60, `--xr-sim=eye=1280x720,fov=96.4268,pitch=-4.70001,
  shot=both`, 63 mm): the reticle's vertical line at x 640.192 / 640.191 and the horizontal at y
  321.241 / 321.242 in the left / right eye, 0.001 px apart; the sight body's centroid moves 81.5 px
  between the eyes, a point 0.44 m away (0.063 x 572.08 / 81.5). The stand-in's eye axes are
  parallel (C14's sun core: 0.02 px), so a world point at infinity has zero disparity too. Before
  the change the reticle moved with the glass, 80.8 px. Every airframe (`C13b_planes.py`, same pose
  per plane): reticle disparity 0.000-0.020 px, Kestrel the largest.
- **Placement, measured.** The authored eye's projection of the centre cross's texel centroid is
  (640.15, 321.17) on SHOT-45's left eye (`--xr-sim=eye=1280x720,fov=96.4268,ipd=20,head=0.01/0/0,
  shot=left,pitch=-4.70001`); the stereo lines measure (640.19, 321.23). On all eleven airframes the
  lines sit within 0.35 px of the authored place, the flat game's.
- **Contrast, measured** (`C13b_levels.py`: every glass pixel mapped through the authored eye to its
  texel; lift in Rec. 709 levels over the sky 12-40 px beside the glass on the same row; the flat HUD
  pipper's disc left out of both). Same pose, frame 60, SHOT-45's left eye against the flat pass:

  | | flat pass | stereo before | stereo after |
  |---|---|---|---|
  | Original reticle mean / p90 / max | -0.7 / 9.5 / 13.1 | -6.8 / -1.9 / -0.8 | 16.5 / 31.6 / 44.9 |
  | Enhanced reticle mean / p90 / max | 24.7 / 44.8 / 58.6 | -3.2 / -0.1 / 0.0 | 31.3 / 49.3 / 58.2 |
  | Glass tint mean, Original / Enhanced | 1.2 / 15.3 | -1.0 / -0.1 | -1.0 / -0.1 |

  B8's +0.7 / +4.7 (+3.2 / +21) were the glass over a different sky with another estimator; this
  table's flat column is the target the reticle now passes, in both looks. Under Enhanced the add
  takes `EnhancedGain` 6.0 (2.5 read 16.6 / 29.5 / 36.3, 4.0 read 23.8 / 40.3 / 47.3); AgX holds
  the brightest pixel at 58.2 against the flat pass's 58.6. No other blended surface changed, so
  stacked decals are untouched (B8's failed gamma mix is not reopened).
- **Goldens.** `c1-cockpit-xr-sim`, `-far` and `-aft` moved (the reticle now draws collimated and
  bright in both eyes); the new `stage-gunsight-xr-sim` pins the reticle over the empty stage's sky,
  one hash over three runs. The 24 flat shots, `c3-flare-xr-sim` and `menu-original-xr-sim` are
  unmoved, 30 of 30 on the last stage run. In an earlier run with another session's Godots running,
  `empty-stage` and `c1-cloud-deck-enhanced` flipped; `empty-stage` rendered its pinned hash 3 of 3
  alone, and
  `c1-cloud-deck-enhanced` flips between the same two hashes alone too (1 of 3), the flip this plan
  already records under B9 and C11. Neither path builds anything this item adds.

**Approach (landed, VR only).** `PlaneBuilder` hands the stereo interior's builder the split glass
texture through its texture substitute and, after the build, adds one `GunsightReticle.Overlay` per
glass under the interior's root (`GunsightReticles` counts them; the stereo cockpit's log line
reports it). `SceneBuilder.PriorityBias` gives the overlay the glass's own depth bias. The flat
interior's builder takes neither, so a flat build is byte-identical.

**Model recommendation.** Standard; the deciding facts (the reticle baked in the glass texture, the
contrast numbers) came from measuring.

**Verify.** Units: `GunsightReticleTests` (each of the eleven glasses found once and its UVs
reproduced by the look, the shipped texture's split, the grouping rule, the look's parallax). Suites:
`-Filter cockpit`, `-Filter xr`, `head-locked-quads`. Goldens: 30 of 30 hash-identical with the
three re-pins and `stage-gunsight-xr-sim`. The disparity and the levels re-run with the commands
above.

Owed at the headset (the author's), VDXR at 90 Hz, Original then Enhanced:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=cockpit --graphics=enhanced
```
Look for: (1) eyes on a distant hill or cloud, the ring and cross lie on it as one image, no
crossed-eye feeling; (2) the reticle reads clearly against sky and ground, at least as well as the
flat game's; (3) the glass's frame and the black sight body under it stay at the glass, in front of
the reticle; (4) leaning sideways or back, the reticle is cut at the glass's edge the same way for
both eyes; (5) the red index mark reads pink, being added light; say if it should stay red.

**⚠ Traps.**
- Do not push the reticle to the far plane: depth-tested there the world hides it, untested the panel
  cannot. The lookup per fragment on the glass's quad gives both at once.
- Do not clip the reticle per eye at the glass's outline: the authored ring nearly fills the glass, so
  each eye kept a different half (seen on the stand-in). The window is the head centre's.
- Do not split the glass texture by a fixed rectangle; the reticle's group touches the frame line at
  the index mark, which is why the outermost ring is excluded before grouping.
- An additive reticle vanishes on a fully white picture (a whiteout at full opacity), where the flat
  pass's grey lines stay visible; partial whiteout at the C1 band reads.
- `GunsightReticle.EnhancedGain` sends the lines' linear value past 1 over a dark ground, so
  Enhanced's glow catches them slightly.

**The overlay's order against the glass (D17, C12's finding; confidence: measured on the stand-in,
the mechanism traced to code).** The overlay and the interior's blended surfaces share priority 110,
and `XrCockpit` lifts every interior geometry to the world origin with one `SortingOffset`, so their
sort depths tie (kilometres from the eye, below float32's step), and the renderer's order moved with
any other transparent in the frame. Before: `c1-cockpit-xr-sim-aft` with the HUD panel's quad drawn
against the same run with that quad on no camera's layer differed on 272 pixels, up to 4 levels, on
the reticle's lines; `c1-cockpit-xr-sim` likewise. Now the overlay carries
`GunsightReticle.SortAfterGlassM` (10 m) as its own sorting offset, which `XrCockpit` keeps under the
lift (each geometry's lifted offset is `SortAhead` plus the one it was built with), so it draws after
the glass and every other interior transparent: the nearest blended surface on its rays, drawn last.
After: `c1-cockpit-xr-sim` is one hash with the panel and without (`93318a4a`, three runs); in
`-aft` the reticle's region is identical with and without, and 8 pixels on the gauge faces still
differ by one level, a tie among the interior's other blended surfaces (a lead for B8, not the
reticle's). Re-pinned: `c1-cockpit-xr-sim` `a97a9349` to `93318a4a` (692 pixels, up to 7 levels, the
reticle's lines now after the glass) and `c1-cockpit-xr-sim-aft` `e0ddd07f` to `4f298667` (264
pixels, up to 4 levels, the same lines), each one hash over three or more runs.
`c1-cockpit-xr-sim-far` and `stage-gunsight-xr-sim` are unmoved, and the same with the panel hidden.
Suite `xr-rig-seat` pins the offset arithmetic (a lifted stand-in overlay sorts `SortAfterGlassM`
after the interior's geometry, and lowered keeps its own).

**Verified.** The complete battery on the merged tree with C13b, B10 and C13 (the stereo goldens
re-pinned on the combined tree): build PASS, units 6443 passed 3 skipped, engine 543 passed 2
skipped with clean engine errors, goldens 32/32 hash-identical. The headset checks named above are
still owed.

## C14 ◐ An angle-based world flare rig (VR and flat Enhanced); the other pane effects as near quads in VR

**Goal.** Two parts. (1) The sun's lens flare is computed in angles and drawn in the world: the core
and the three ghost rings as billboards at sky-dome distance along the line from the sun direction
to the view-forward direction, the sun wash as a quad near the eye. VR always uses it; flat Enhanced
adopts it once the author has judged it against the current rig; Original keeps today's screen-space
rig unchanged. (2) The remaining full-pane canvas effects (the cloud whiteout, `ScreenFlash`, the
mission-end fade) get a VR form as head-locked quads near the eye.

**Outcome: built and adopted in flat Enhanced; the VR ring default is the author's.** The world rig
(`Session/World/WorldFlareRig.cs`) draws every stereo session's flare and every flat Enhanced one;
flat Original stays on `LensFlareRig`, which is unchanged. The whiteout, `ScreenFlash`, the sun wash
and the mission-end fade have head-locked stereo quads (`UI/Boards/HeadLockedQuads.cs`). The
montages are in `.scratch/C14/` (below).

**Ruling on the montage: adopt the world rig in flat Enhanced, with a ring gain under Enhanced** so
the ghost rings read about as strongly as the screen rig's (the montage measured 4.2/9.1/3.9 levels
over the sky against 11.0/20.8/6.1, since AgX tonemaps the world rig and not the canvas).

**The follow-up (done).** `SkyStage.UsesWorldFlare(stereo, enhanced, screenAsked)` is
`stereo || (enhanced && !screenAsked)`. `--world-flare` is retired and replaced by `--screen-flare`,
which puts flat Enhanced back on the screen rig: the default flipped, so the A/B door now opens the
other way (it reproduces the screen rig's frame hash-identically on all six poses; `docs/cli.md`'s
counts are unchanged). `WorldFlareRig.EnhancedGain` multiplies the rings by **2.5 / 2.5 / 2.0**
whenever the live look is Enhanced, stereo included, since a stereo session runs either look and
AgX acts on its quads the same way; Original keeps 1, where the two rigs already agree. The core and
the wash take no gain. No golden moved: none of the 27 is a C2/C3 Enhanced shot, and
`c3-flare-xr-sim` runs in Original.

**Evidence (confidence: traced to code and measured for placement, size, depth and the stereo
quads; the look is the author's judgement).**
- **Placement is the screen rig's law in angles.** An element at fraction f sits at
  `(1 − f) × t` in the view's tangent plane, t being the sun's tangent point, which is the great
  circle from the sun to the view axis; the shader places it in view space at 95 % of the sun's
  depth, so it needs no CPU basis and rides the head with no lag. The suite `world-flare-pixels`
  projects all four elements through a live `Camera3D` on a rotated view at 20 km at the chase
  (46.8° V) and the cockpit (64.37° V) angle: worst offset from the screen rig's pixel 0.0001 px,
  worst width error 0.0001 px; a 0.2 % size perturbation fails it at 0.33 px.
- **Sizing: flat from the live FOV, VR from the reference mapping.** A flat pane reads
  `tan(Fov/2)` off the camera (KeepHeight, or converted under KeepWidth), so the original's pixel
  size holds in Cockpit and Chase alike, the Enhanced speed widen included. VR takes the external
  base `CameraController.ExternalFovDeg` (46.8° V) and a 16:9 frame for its in-view gate and wash
  falloff, so the stereo flare shows over the cone a flat chase pane shows it.
- **Compositing.** The canvas adds in gamma space after the tonemap, so each element adds its
  texture bytes to the opaque picture in gamma space and hands the linear blend the difference.
  With the switch forced on in Original (a scratch edit, removed), the world rig matched the screen
  rig on the C3 off-centre chase pose to 0.22 % of pixels over 2 levels, 0.06 % over 8 (the
  residual is the core's depth-tested edge). Under Enhanced the AgX tonemap acts on the world rig
  and not on the canvas: on the same pose the rings read 4.2 / 9.1 / 3.9 levels over the sky
  against the screen rig's 11.0 / 20.8 / 6.1, and the core rolls off below 255 with a glow halo
  (verification SHOT-46). That difference is the look the montage puts to the author.
- **The Enhanced ring gain (confidence: measured; the look is the author's).** Brightest-pixel lift
  over the same pose under `--no-flare`, local sky offset removed (the estimator
  `docs/org/weather.md` calibrates the rings with, and it reproduces the montage's figures: screen
  rig 11.0 / 21.0 / 6.0, world rig 4.3 / 9.0 / 3.7 on the off-centre chase pose). With gains
  2.5 / 2.5 / 2.0 the world rig reads 11.0 / 20.0 / 6.3 there (0, −5, +5 %), and 10.7 / 17.7 on
  the off-centre cockpit pose against 10.7 / 18.3 (ring C falls on the panel there). The mean lift
  over the screen rig's footprint agrees within 9 % on every ring at both poses. Per ring, because
  one gain of 2.3 left rings A and B 11-12 % under and ring C 12 % over. The core is not boosted:
  its centre reads 252 against the sprite's 255 and its glow halo runs brighter than the sprite past
  24 px, so its lower peak lift (37 against 59) is AgX rolling off a saturated disc; the author ruled
  on that core in the montage. The flat wash is the same canvas rect in both rigs and measured the
  same (16.3 levels on the chase pose). Tools: `.scratch/C14/C14b_poses.ps1` (captures),
  `C14b_peak.py` and `C14b_levels.py` (levels), `C14b_core.py` (the core's radial profile).
- **Occlusion.** The core is depth-tested, so the own airframe in chase and the C3 crater rim cut it
  partway (montage rows 2 and 6). The rings keep the screen rig's one ray to the sun's centre for
  on/off, skip depth testing, and discard pixels whose scene depth is nearer than 1.4 m (the seat
  clearance) so an in-world interior covers them as the flat cockpit pass does.
- **Stereo depth.** C3 cockpit pose under `--xr-sim=pitch=-6,eye=960x960,fov=70`
  (`--direction=0.5,0.707,-0.5 --view=cockpit --no-cockpit-pass`, frame 60): the core's plateau
  centroid sits at eye-local x 477.87 in the left eye and 477.85 in the right, 0.02 px of
  disparity at 685 px/rad, so it reads at sky depth, on the dome's own sun disc (2 px of plateau
  noise), against about 100 px for the gunsight in front of it. No sprite floats at the face.
- **The pane quads.** Each quad copies its flat layer's colour (`CanvasColour` honours a lowered
  canvas layer, so a presenting cutscene hides the stereo whiteout and flare too) and is placed in
  view space, 8 × its distance across, past any eye's frustum. Order is the flat one: flare
  100-103 < whiteout 110 < flash 111 < HUD panel 115 (kept for C12) < sun wash 120 < fade 121.
  Whiteout on C1 inside the band (`--pos=-6144,1000,-6144 --direction=0,0,-1 --view=cockpit
  --no-cockpit-pass --xr-sim`, frame 30) against the same run with `--no-fog`: 80 % of each eye's
  window region moved and 0.7 % of the panel box, in both eyes (`xr-c1-whiteout-diffmask.png`;
  `--no-fog` also lifts the world fog, which the interior never takes either).
  `--debug-wash=1` paints the flash quad full red in both eyes (`xr-c1-flash.png`). The fade quad
  shares the flash quad's shader and is checked by the suite `head-locked-quads`; no scripted
  mission end exists to photograph it.
- **The whiteout keeps the interior clear by depth, not by layer order.** Its quad alone is
  depth-tested, at the 1.4 m seat clearance: interior geometry nearer along the view axis stays
  clear, the world beyond whitens. The interior's own wings, where a plane's interior has them,
  reach past 1.4 m and whiten with the world; a stencil mark on the interior would be exact (B8).
- Flat goldens unmoved (25 of 25); the new `c3-flare-xr-sim` golden is hash-stable over 12 runs.
  With the adoption and the gain, all 27 goldens stay hash-identical.

**Montage for the author** (`.scratch/C14/`, Enhanced, frame 60, 1:1 crops around the sun under
each frame). The first montage (screen rig against the ungained world rig, `montage-C14-*.png`) is
kept beside it in the plan worktree's `.scratch/C14/`. The gain montage puts today's screen rig
(`--screen-flare`, left) against the adopted world rig with its gain (right):
`montage-C14b-overview.png` (all six, half size), and `montage-C14b-m1-chase-centred.png`,
`montage-C14b-m2-chase-occluded-by-plane.png`, `montage-C14b-m3-chase-offcentre.png`,
`montage-C14b-m4-cockpit-centred.png`, `montage-C14b-m5-cockpit-offcentre.png`,
`montage-C14b-m6-freecam-ridge-occluded.png`. Poses:
`--chapter=C3 --plane=player_bhawk --pos=0,1200,0 --hold=0,0,0,0.6` with `--direction=` 0.586,0.559,
-0.586 / 0.509,0.695,-0.509 / 0.785,0.423,-0.453, and with `--view=cockpit` 0.5,0.707,-0.5 /
0.765,0.469,-0.441; the ridge pose is `--freecam --chapter=C3 --pos=-4484,390,-6310
--direction=0.586,0.559,-0.586`. They re-run with `.scratch/C14/C14b_poses.ps1 -Variant <name>
-Extra <flags>` and `C14b_montage.py`.

**Background (the survey).** `Session/World/LensFlareRig.cs` draws four additive
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
nearer than the HUD panel (C12).

**Approach (landed).** `Launch/SkyStage.cs` builds `WorldFlareRig` in place of `LensFlareRig` when
`UsesWorldFlare` holds: a stereo session (the launcher hands `GameSession` the XR head through
`LauncherContext.Xr`, read-only from `XrRig.Head`), or a flat Enhanced session without
`--screen-flare`, chosen at the build. A stereo session places the flare for the head's pose and
builds `HeadLockedQuads`, which follows the whiteout rectangle, `ScreenFlash.CurrentFor(0)`, the
world rig's wash and the director's `LeavingFade`; the flat canvas layers are untouched. The ring
gain is read from `GraphicsMode.Enhanced` each tick, so a live switch to Original drops it to 1.
**Owed to the author:** a look at the gain montage (`montage-C14b-overview.png`) to confirm the
rings, and at the headset (E21) whether the VR flare keeps the rings, which it shows by default
now, or shows the core and wash only.

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** Original-mode goldens and every existing golden unmoved (27 of 27 hash-identical). The
montages above for the Enhanced adoption and its gain. `world-flare-pixels` and `head-locked-quads`
(engine suites) and `CSVM.Tests/WorldFlareTests.cs` (the law, the selection rule, the flag, the
gain's shape, and the spec pinned to `LensFlareRig`'s private copy by reflection). The golden `c3-flare-xr-sim` (C3, nose view,
`--xr-sim`, frame 120, frame-sensitive 15.38 %, 86 % of its pixels move over 8 levels without the
flare): the core, the three rings and the wash quad in both eyes. A review found the quads on layer 1
(or the pane's player layer in splitscreen), so the spyglass picture and the Danger Zone photograph
drew a flare placed for their own view; each pane's quads now stand on its own
`SplitScreen.WorldFlareLayer` (bits 20 to 23), which both picture masks drop. Suite
`world-flare-layers` reads C3's built quads against every pane, disc and photograph mask.

**⚠ Traps.** Do not change `LensFlareRig` itself: it is the Original path and carries the footage
calibration. An angle-sized rig drawn at a fixed angle shrinks in Cockpit view against the original's
fixed pixel size; size from the live FOV. A depth-tested ghost is wrong, only the core may be
occluded. The whiteout sits under the cockpit pass so the interior stays clear
(`docs/architecture/Session.md`); in VR the interior is in the main world (B8), so that ordering has
to be rebuilt another way. Enhanced adoption is a look judgement and needs the montage, never a
luminance distance alone. Compare the two rigs' compositing in the Original look: under Enhanced
the A/B measures AgX (SHOT-46). ⚠ Do not pin a stereo flare golden in Cockpit view: with the
flare drawn, three gauge-face pixels of the in-world interior flip by one level between runs (a
third of runs, render thread safe or separate; nine runs without the flare never flipped), which
the nose-view golden avoids and B8 inherits. A C3 `--xr-sim` probe exits with an access violation
or a C# finalizer fatal after saving its shot in about half the runs, with the flare or without it
and with this item's code switched off; on `--render-thread safe` it exited 0 in 28 of 28. The
render thread was not the cause: a .NET finalizer racing Godot's teardown was, on either thread, and
`Launcher.SettleFinalizers` removes it (verification LOG-27).
⚠ `HeadLockedQuads` hands its `tint` as a `Color`, which Godot already makes linear for a 3D
material, and its shader linearises it again (verification SHOT-47, measured by C10 on its own
surround). Black, white and pure primaries are unchanged; by the conversion, the whiteout's 0.9 grey
composites as about 200 of 255 where the flat layer shows 230. Pass a `Vector4`, or drop the shader's
conversion, and check the drawn colour in a capture.

**Verified.** The complete battery on the merged tree with A3b, B6 and C14 and the ShaderTwins
rewrite fix (LOG-26): build PASS, units 6348 passed 3 skipped, engine 533 passed 2 skipped with
clean engine errors, goldens 27/27 hash-identical. The headset checks named above are still owed.

**Verified.** The complete battery on the merged tree with B7 and the C14 follow-up: build PASS,
units 6365 passed 3 skipped, engine 534 passed 2 skipped with clean engine errors, goldens 27/27
hash-identical. The headset checks named above are still owed.
With the review fixes (the flare's own layers, the headset boot's recovery, the start-failure notice): the
complete battery on the merged tree, build PASS, units 6531 passed 3 skipped, engine 552 passed 2
skipped with clean engine errors, goldens 37/37 hash-identical; exit 0.

# Wave D, cutscenes, comfort and performance

## D15 ◐ Cutscenes as a stereo 3D-cinema pair on the theater screen

**Outcome: built and pinned on the stand-in; the window feel, the comfort of the depth and the
letterbox are owed at the headset.** A presenting cutscene under VR plays on the theater screen in
the full dark surround: two cameras on the authored camera, the runtime's IPD apart, each eye's
picture picked by `VIEW_INDEX` in the board's 4:3 rect, converged on the letterbox card so the bars
stand on the screen plane. The XR origin no longer takes the cutscene camera's heading (B9's arm);
it stands at the camera's eye on a held level heading. MPEG cinemas stay mono on the same screen.

**Goal.** In VR an in-engine cutscene plays on the theater screen with depth: two cameras on the
authored camera, one IPD apart, each eye's image picked by `VIEW_INDEX`, the authored framing and the
letterbox exactly as in the flat game.

**Evidence (confidence: traced to code and measured on the stand-in for the raise and hand-back,
the pair's placement, its convergence and the per-eye parallax; direction sound, magnitude TUNE for
the convergence and the 4:3 frame; lead-only for comfort until the author's run).** Reports
`.scratch/test-theater-cutscene.txt`, `.scratch/test-xr-view-cutscene.txt`; captures and
instruments `.scratch/D15/` (`D15_parallax.py`, `D15_disparity.py`, `D15_hashes.ps1`).
- **The condition.** A presenting cutscene is `CutsceneController` `Playing && Presenting`: code 2
  sets it with `CameraOwned` on every pilot, the handoff and the skip clear both. `MirrorCamera` (at
  priority 1000) puts `camera1`'s pose on the pane camera and `FrameBars` its framing, 55.76°
  vertical. The Launcher composes `XrRig.CutscenePlays` from `GameSession.Cutscene`, and the
  theater's cutscene source from `XrRig.Screening` and the pane camera; nothing flat reads either.
- **The seat** (`xr-view-cutscene`, C1 Bloodhawk in a banked turn, the stand-in live): with the
  cutscene presenting, the origin stands on the authored camera's eye (0 mm) on the seat's level
  heading (0.0000°) through three shots turned up to 2.8 rad apart, with one cut (`another owner took
  the camera`) and none on the shots. Control: the same owned camera with no cutscene is taken by its
  own heading (B9's arm, kept for the spectator, photo mode and the weapon lab). The hand-back
  returns the seat.
- **The pair** (`theater-cutscene`, a rotated camera at -6120, 412, -3980, IPD 64 mm): the eyes stand
  64.0 mm apart along the camera's right with its orientation; a point on the 7.5 m convergence plane
  lands at (996.61, 720.98) left, (996.59, 720.96) right and (996.60, 720.97) on a flat 4:3 perspective
  camera on the authored pose, so each eye frames exactly the flat picture there; a point 3 km out
  shows 9.654 px of disparity, the parallax law's 9.654 px, with no vertical disparity.
- **Per eye, captured** (frame 120 of `--campaign=csvm-golden:0 --plane=player_bhawk --det --mute`,
  `generic_intro` presenting): `--xr-sim=eye=1600x1200,fov=46,head=0/-0.1/0,shot=left` and
  `shot=right`, the screen square-on and filling each eye, against the flat 1280x720 frame's centre 4:3
  crop resampled to the screen's size. Far haze and horizon: left -4.59 px, right +4.63 px from the flat
  picture, mean +0.02 px, vertical under 0.12 px; the law's infinity is ±4.58 px. The zeppelin's mean
  is +0.04 px and the cloud's -0.30 px. Left against right over the same band: -38.33 px, exactly the
  screen plane's geometric -47.49 px plus the law's 9.16 px. (The screen's own rendered edges read
  47.92 px apart, 0.43 px over geometry; that is the edge's coverage, so the picture is measured
  against geometry.)
- **Why the full surround, not the dimmed world.** The origin has to stand at the cutscene camera: the
  dome, the deck and the fog take their eye from the head, so an origin held at the seat's old place
  would centre the sky kilometres from the camera the pair draws from. The world round the seat is then
  the cutscene camera's own surroundings, sliding past as it flies, which moves against the picture.
  The dark surround makes the screen a window in a dark room, Decision 8's phrase. The headset camera
  takes the plain environment as at the menu, since the pair's picture is already tonemapped by its own
  cameras (SHOT-46's double tonemap otherwise).
- **Why 4:3.** It is the frame the card was authored for and the board's rect, where the boards and the
  MPEG cinemas already play. Under VR `FrameBars` measures the eye-size root (0.89 wide on the
  stand-in), so `BarsWidthScale` is 1 and the card covers a 4:3 frame with 1.36 m to spare at 7.5 m
  (half widths 6.65 m against 5.29 m), far past the 32 mm an eye shifts. The flat game widens the same
  vertical picture to the window; a 16:9 pair would need `FrameBars` to stretch the card for the pair's
  aspect, a change to the flat path's code. TUNE (`TheaterLayout.CutsceneSize`).
- **Layers.** Bit 8 was free; `SplitScreen.HeadsetLayer` now carries the screen, the surround and the
  laser. Every pane camera's mask keeps it, so nothing the headset draws moved; the pair's cameras drop
  it, or they would draw the screen into its own picture and the surround over all of it. The
  head-locked quads stay on layer 1, so a mission-end fade over a held film darkens the picture itself.
- **Flat unchanged.** The theater is built only under XR and `CutscenePlays` is unset in a flat
  launch. Probe hashes: `menu-original-xr-sim` 9f763799, `c1-cockpit-xr-sim` 8610b367 and the flat
  `campaign-intro-fill` 280023fa, all their manifest values. The new golden `campaign-intro-xr-sim`
  hashed cf30179c on three runs; frame 121 moves 0.50 % of pixels.

**Resolved TODOs.**
- **The convergence: the letterbox card's depth, 7.5 m** (`StereoPair.ConvergenceOfCard` = 1, TUNE).
  3D-cinema practice keeps the frame at zero parallax with the scene behind it (the floating-window
  rule), and here the bars are the frame: converged on the card they stand on the screen plane. On the
  default seat (1.53 m tall at 2.5 m, framed at 55.76°) infinity lies 12.1 mm behind the screen, 0.6 % of
  its width, inside the 1-2 % a cinema allows, and appears 3.1 m away; scene 30 m out appears at
  2.92 m, 3 m out stands in front at 1.94 m. The depth is shallow and nothing diverges. The other
  candidate, converging at the screen's undistorted-window distance (1.45 m, where the screen would
  span the 55.76°), puts infinity at infinity but its far parallax equals the IPD (the eyes parallel,
  the divergence limit) and sinks the bars 13 m behind the screen, cutting nearer scene they stand
  behind. The interaxial is the runtime's IPD (Decision 8), read on the render thread from the eye
  poses (`XrEyeFeed.InterocularM`).
- Model recommendation: Standard.

**Approach (landed, VR only).**
- `UI/Boards/StereoPair.cs`: `TheaterCutscene` (the camera and the convergence) and the engine-free
  law: `Eye`, `FrustumOffsetM`, `FrustumHeightM`, `ConvergenceM`, `ParallaxM`, `ApparentDistanceM`.
- `UI/Boards/TheaterScreen.cs`: two 1600x1200 `SubViewport`s with frustum cameras sharing the root's
  world, posed each frame after the origin from the source camera (frame, planes, mask less
  `HeadsetLayer`, the pre-swap environment, attributes) and updating only while a cutscene plays; the
  screen shader samples both and picks by `VIEW_INDEX` inside `TheaterLayout.CutsceneRect`, under the
  boards; a cutscene takes the full surround and the plain environment. `Build` takes the source and
  the interocular as optional arguments.
- `Flight/Camera/XrRig.cs` `CutscenePlays`, `Screening` and `XrMount.Screened`;
  `Launch/XrEyeFeed.cs` `InterocularM`; `Launch/Launcher.cs` wires both.
- `UI/Boards/SplitScreen.cs` `HeadsetLayer`; `TheaterPointer.cs` puts its beam and dot on it.

**Verify.** Units: `CSVM.Tests/StereoPairTests.cs` (the eyes, the converging frusta, the card's
convergence, the default seat's depth, the law against a projection worked by hand),
`TheaterLayoutTests.ACutscenePlaysOnTheBoardsOwnRect`. Suites: `theater-cutscene`,
`xr-view-cutscene`; every `xr` suite, `theater-screen` and the `campaign-cutscene` set pass with clean
engine errors. Golden: `campaign-intro-xr-sim`. The rules that bite (docs/verification.md): SHOT-44,
each eye read through `shot=`; SHOT-48, disparity by a subpixel shift, never centroids; GOLD-17, the
golden names its missing-profile assumption; GOLD-3, the screen shader changed, so the whole golden
set is swept on the merged tree, not only the three probed here.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --campaign=csvm-vr:0 --plane=player_bhawk
.\RunGame.ps1 -EngineArgs '--xr-mode','on'
```
The first plays sequence 0's intro straight away (C3/M01, `generic_intro`; a profile name that does
not exist is fine, and an `--xr-sim --no-det` run of it logged the pair); the second goes through the
menu to a campaign mission, whose intro is the real path. Look for: (1) the
intro opens on the screen in the dark room and reads as a window: the scene behind the glass, the
black bars on the glass itself; the log has `theater: cutscene pair 1600x1200 per eye, <n> mm apart,
converged at 7.5 m`; (2) the depth is comfortable through the whole intro, no doubled image on the
far sky and nothing straining on near aircraft, and it reads as 3D at all; (3) turning or leaning the
head changes nothing inside the frame; (4) the framing and the bars match the flat intro; (5) Esc
raises the pause board over the playing cutscene; (6) the hand-back to the cockpit is one cut. The
values to tune are `StereoPair.ConvergenceOfCard` (below 1 deepens the scene and sinks the bars),
`TheaterLayout.CutsceneSize` and the surround. Frame pacing during the cutscene is in the
`xr frames` lines of the run's log: two world renders a frame, the pair's (D18 cut the headset's own).

**⚠ Traps.**
- Do not take the cutscene camera's heading for the origin, and do not hold the origin's position
  either: the sky centres on the head, so a held origin draws the pair's sky off-centre.
- Do not leave the theater's geometry on layer 1: the pair would draw the screen into its own picture
  and the opaque surround over all of it.
- Do not copy the pane camera's `Environment` into the pair while the plain swap holds it; take the
  one the swap saved.
- Converge with the frustum offset, not by sliding the picture on the quad, which crops an eye's edge.
- Scene nearer than 7.5 m at the left or right edge stands in front of the screen and is cut by the
  screen's edge (a window violation); no floating-window mask is drawn.
- The pair reads the pane camera's `Fov` as vertical (`KeepAspect.Height`, Godot's default).
- Do not let the headset draw the world behind the opaque surround: while a cutscene presents its
  camera's mask is cut to `SplitScreen.HeadsetLayer` for the frame, after the pair has read it (D18).

**Verified.** The complete battery on the merged tree with D15 and C12: build PASS, units 6462
passed 3 skipped, engine 546 passed 2 skipped with clean engine errors, goldens 34/34
hash-identical. The headset checks named above are still owed.

## D16 ◐ Comfort: fades, tunnel vignette, render-scale slider

**Outcome: built and pinned on the stand-in; the fade's feel and durations, the vignette's curve
and strength, and the render scale are owed at the headset.** Every cut the headset sees lands on a
black frame. A cut the game makes (`XrRig.Cut`: a respawn, a crash, a new aircraft, a static
camera's re-site, the camera taken by another owner or handed back, so a cutscene's take and
hand-back) snaps to black on its own frame and clears over 0.3 s. A view key (`F8`, D-pad down, the
XR cycle button, `F7`) darkens over 0.15 s first and the view changes on the black frame. The first
flight frame after a load lands on black. The mission end keeps C14's 2 s fade. The optional tunnel
vignette (Off, Mild, Strong; off by default) closes in with the XR origin's own turn rate, and the VR
render scale (50 to 125 %) sets OpenXR's render target multiplier, live.

**Goal.** Respawn, mission end and view switch fade through black instead of cutting; an optional
tunnel vignette scaled by angular rate is off by default; a VR render-scale slider sets the
runtime's resolution multiplier.

**Evidence (confidence: traced to code and measured on the stand-in for the fade's timing and
placement, the vignette's response and the render scale's reach; traced to the Godot 4.7-stable
source for the render scale applying live; direction sound, magnitude TUNE for every duration, the
curve and the scale's steps; lead-only for comfort until the author's run).** Reports
`.scratch/test-xr-comfort-fade.txt`, `.scratch/test-xr-comfort-vignette.txt`; captures and
instruments `.scratch/D16/` (`D16_burst.py`, `D16_hashes.ps1`).
- **A cut the game makes cannot wait at the rig.** `XrRig.Cut` is raised on the frame the camera
  has already cut: the controller has changed view, the interior's visibility and the cull mask
  follow, and for a cutscene `Screening` raises the theater in the same `Place`. Holding the origin
  at the old pose for a fade-out would draw it with the new view's dressing (the interior placed
  round a chase eye, the hull round a seat, a cockpit camera's picture on the cutscene screen). So
  those cuts land on black on their own frame and fade in. The fade-out exists where the cut can
  wait: the view keys, which `CameraController.StepViewKeys` holds under a headset until
  `XrFade.Cover` reports black, so the switch itself lands on the black frame.
- **Measured on the stand-in** (`xr-comfort-fade`, 60 Hz steps, C1 Bloodhawk in Cockpit): the rig's
  first cut, the first flight frame, the camera taken by another owner and its hand-back each read
  alpha 1, the quad shown and its opacity 1 on the cut frame, and clear in 18 frames (0.3 s) after it.
  A view key darkens monotonically for 9 frames (0.15 s) and the view changes on the 10th, which is
  black, with exactly one rig cut, then clears in 18 frames.
- **Captured** (`RunProbe.ps1 --chapter=C1 --plane=player_bhawk --pos=-6350,300,-4144
  --direction=-0.743,0,-0.669 --view=cockpit --no-cockpit-pass --hold=0,0,0,0.6 --xr-sim --det --mute
  --frames=1 --shots=30`, `.scratch/D16/burst/`): sim frames 1 and 2 are black in both eyes (mean
  0.00, max 0); the mean then rises 4.9 to 5.2 levels a frame, linear in sRGB as a canvas fade
  darkens, and reaches the settled picture (83.7 against 83.0) at frame 20.
- **Where the fade stands.** Render priority 127 on `SplitScreen.HeadsetLayer`: over the theater's
  surround (125) and screen (126), so a cutscene's take and hand-back fade with the dark room, while
  the pair's cameras leave the layer out and the film never carries it. While a board shows on the
  theater (`TheaterScreen.BoardShown`: no flight live, or a board raised in flight) it drops to 123,
  under the surround, so a fade behind the pause or results board darkens the dimmed world and never
  the board. The laser beam also stands at 127 and shows only over a board. The vignette draws at
  122, over every `HeadLockedQuads` tier (the mission-end fade at 121) and the HUD panel's 115.
- **The load screen to the world.** The rig's cut onto the new aircraft comes while the load board
  still shows, under the surround, so `ComfortQuads` makes the first frame a flight is on view (the
  theater's own condition, one shared function) a cut of its own; captured above.
- **Mission end** is C14's tier and unchanged: `CampaignDirector.LeavingFade` darkens the world over
  its 2 s hold on `HeadLockedQuads`' quad (layer 1, so it darkens a held cutscene's picture too,
  D15), and the results board or the menu then rises in the theater's surround.
- **The vignette** (`xr-comfort-vignette`): unset, it resolves off and draws nothing through a
  full-stick roll. At Strong, level cruise turns the origin at 0 deg/s and leaves the view open; a
  full-stick roll turns it at up to 181.4 deg/s, the strength reaches 1 and the quad's clear cone 28°.
  Captures `.scratch/D16/vignette-mild.png` and `vignette-strong.png` (frame 90 of that roll at 600 m,
  `--xr-vignette=`): the tunnel stands on each eye's own axis.
- **The render scale applies live** (Godot 4.7-stable, `modules/openxr/openxr_api.cpp`):
  `OpenXRInterface.render_target_size_multiplier` sets the main thread's multiplier and the render
  thread's copy, `get_recommended_target_size` multiplies the runtime's recommendation by it, and
  `pre_draw_viewport` frees and recreates the swapchains on the first frame whose size differs. It
  therefore takes effect on the next frame, from the Options apply as at boot. `Viewport::
  _check_xr_size` resizes the root to it, and `RendererViewport::_configure_3d_render_buffers` scales
  the 3D buffers inside it by the root's `scaling_3d_scale`, so the flat Render Scale multiplies with
  it. On the stand-in `--xr-render-scale=70` logged `xr: render scale 0.7 eye_px=448x504` for the
  640x720 eye and the theater relaid at 448x504; `xr-render-scale` pins 1600x1200 to 1120x840 and
  back through the stand-in's twin of the multiplier. Live under OpenXR is source-traced, not seen.
- **Flat unchanged.** Everything is built beside `XrSession` in `Launcher`, the view-key wait needs
  `CameraController.Headset` and the rig's `Fade`, null on every flat seat, and only a headset launch
  reads the two options. Under `--det` the fade steps on the fixed clock, so a capture repeats. All
  nine `xr-sim` goldens probed hash-identical one by one (`D16_hashes.ps1`); the flat set is the
  merged battery's.

**Resolved TODOs (proposed values, TUNE for the author).**
- **Fade:** 0.15 s out (view keys only) and 0.3 s in (`XrFade.DefaultOutS`, `DefaultInS`). VR titles
  sit at 0.1 to 0.3 s; the out is kept short because the view key waits on it.
- **Vignette curve:** nothing below 20 deg/s (a gentle bank), full at 90 deg/s, a smoothstep between,
  followed in 0.1 s and let go in 0.4 s so a series of short rolls does not pulse. The clear cone
  closes from 60° (past any eye's edge) to 40° (Mild) or 28° (Strong), feathered over 12°
  (`XrVignette`). The rate is the origin's own turn, so the chase view's rolls count and head turns
  never do.
- **Render scale:** OpenXR's `RenderTargetSizeMultiplier`, words 50, 60, 70, 80, 90, 100 and 125 %;
  70 % is about the author's 2112x2304 runtime setting against VDXR's 3072x3264.
- **Options:** `OptionsStore` `vrRenderScale` and `vrVignette`, riding `OptionsApplyExit` and applied
  at once (`Launcher.ApplyVrComfort`, the pause leaf's apply included). Built-in shows both rows under
  VR mode. Original's VIDEO page has no free authored line (B6 took the last), so under a live headset
  the comfort rows take the Display Mode and V-Sync lines, two settings a headset makes moot; their
  words ride the apply unshown. `--xr-render-scale=` and `--xr-vignette=` beat the saved words.
- Model recommendation: Standard.

**Approach (landed, VR only).**
- `UI/Boards/XrFade.cs` (the fade's clock) and `UI/Boards/XrVignette.cs` (the curve), engine-free;
  `UI/Boards/ComfortQuads.cs`, the two head-locked quads, built by `Launch/Launcher.cs` beside the
  theater and subscribed to `XrRig.Cut`. They sit in `UI.Boards` because the family order ranks it
  under `Flight`, which names them.
- `Flight/Camera/XrRig.cs` `Fade`, handed to the pilot's camera each `Place`;
  `Flight/Camera/CameraController.cs` `Fade` and the held view keys in `StepViewKeys`.
- `UI/Boards/TheaterScreen.cs` `BoardShown`.
- `Utils/VrRenderScaleSetting.cs`, `Utils/VrVignetteSetting.cs`; `Launch/XrSession.cs`
  `ApplyRenderScale`; `Launch/XrSimInterface.cs` `RenderTargetSizeMultiplier`; `Spec/SessionSpec.cs`
  the two flags; the rows in `UI/Screens/LaunchMenu.cs`, `UI/Menu/Original/OriginalVideoPage.cs`
  (`Headset`) and `UI/Menu/DisplaySettingRows.cs`.

**Verify.** Units: `CSVM.Tests/XrComfortTests.cs` (the fade's cut, cover, dropped cover and restart;
the curve, the rate and a cut's jump; the words, the store fields, the flags),
`OriginalOptionsTests` (the headset rows on both layouts, stepping and riding the apply). Suites:
`xr-comfort-fade`, `xr-comfort-vignette`, `xr-render-scale`; `menu-original-tracer` walks the two
Built-in rows, `menu-backdrop` counts them, `display-det-guard` names both fields as display settings
no `--det` run reads. Every menu, display, options, pause, xr, theater, cockpit and camera suite
passed (93 of 93, clean engine errors), units 6465 passed 3 skipped. Goldens: none new; the nine
`xr-sim` shots unmoved. The rules that bite (docs/verification.md): SHOT-44 (each eye is read in the
side-by-side capture), METHOD-9 (the vignette's Off and cruise runs are the controls for its roll).

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669"
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --xr-vignette=mild
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --xr-vignette=strong
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --xr-render-scale=70
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --campaign=csvm-vr:0 --plane=player_bhawk
.\RunGame.ps1 -EngineArgs '--xr-mode','on'
```
Look for: (1) first line: the first flight frame comes out of black; `F8` (D-pad down, the left
upper face) darkens briefly and each view lands on black, the delay acceptable; `F7` and each flyby
re-site, `Backspace` (pad Y) and a crash into the ground do the same; say whether the fades are too
long, too short or too abrupt; (2) second and third lines: hard rolls and tight turns in Cockpit and
Chase; whether the vignette helps, comes in late or pumps, Mild against Strong, and whether gentle
flying stays clear; (3) fourth line: sharpness, and the `xr frames` lines' `gpu_ms` against the first
line's at 100 % (the log has `xr: render scale 0.7 eye_px=...`); (4) fifth line: the intro's take and
hand-back fade through black with the dark room; (5) sixth line: Options, VIDEO under the headset
shows Comfort Vignette on the Display Mode line and VR Render Scale on the V-Sync line; in a flight,
Esc, Options, change the render scale and ACCEPT CHANGES: the picture changes resolution at once with
no more than a frame's hitch; a campaign mission's end keeps its 2 s fade. The values to tune are
`XrFade.DefaultOutS`/`DefaultInS`, `XrVignette`'s six constants, and the scale's steps.

**⚠ Traps.**
- Do not hold the origin at the old pose to fade out a cut the game makes: the view's dressing (the
  interior, the hull, the cull mask, the theater) changed with the camera on that frame.
- Do not put the comfort quads on layer 1: the cutscene's pair would draw them into the film, and the
  screen's picture would darken twice.
- Do not leave the fade over a raised board; it drops under the surround while `BoardShown`.
- `XrFade.Cover` must be called every frame while a request stands; a frame without it clears.
- The vignette reads the origin, never the head: a head turn must not close it.
- Under a VR render scale the root's 2D layout, the eye's render target, shrinks with it. Anything
  laid out from the root's size sees the smaller size: the theater keeps two texels per board pixel
  regardless; the HUD panel (C12, `HudMetrics` reads the root height) must too.
- A golden whose shot frame falls within 0.3 s of a cut would carry the fade; the nine `xr-sim`
  shots stand well clear (frame 120, or 45 for the markers).

**Verified.** The complete battery on the merged tree with D16, D17 and the exit-finalizer fix
(LOG-27): build PASS, units 6476 passed 3 skipped, engine 549 passed 2 skipped with clean engine
errors, goldens 35/35 hash-identical. A first run on the tree without the exit fix crashed one shard
at quit after a passing report. The headset checks named above are still owed.

## D17 ◐ Port the screen-space effects to multiview

**Outcome: the race ghost's dither ported and measured on the stand-in; its look at the headset is
owed.** A stereo process draws the ghost with a text whose dither pattern sits at each fragment's own
depth (`RaceGhost.StereoFragmentLine`), so the pattern carries the aircraft's disparity within a
pixel where it carried none. The four effects A4 found correct need no port, and the clutter fade
keeps its screen-locked dither. `--debug-race-ghost` makes the ghost capturable on one seat, and the
new golden `stage-race-ghost-xr-sim` pins it. The flat text is unchanged; no flat golden moved.

**Goal.** Soft particles, the fog-volume fade and heat shimmer render correctly in both eyes; the
clutter dither stays if A4 found it reads fine per eye.

**Evidence, D17 (confidence: traced to code and measured per eye on the stand-in for the disparity;
the look of a dither in the headset is the author's).** Tools and captures in `.scratch/D17/`
(`D17_shot.ps1`, `D17_gold.ps1`, `D17_blocks.py`, `D17_phase.py`, `D17_diff.py`).
- **A capturable ghost on one seat.** A4 could not capture the ghost: it arms only between race
  pilots, and VR refuses a second local seat. `--debug-race-ghost` builds each local seat's airframe
  with the ghost's shaders and stamps it with `SplitScreen.EveryCameraLayer`, so the seat's own chase
  camera draws it as a full ghost (14.9 to 25.9 m, inside the 40 m floor). It works flat and under
  `--xr-sim`, makes no race and changes nothing else.
- **The instrument.** Pose `--stage=empty --plane=player_bhawk --pos=0,300,0
  --direction=-0.743,0,-0.669 --view=chase --hold=0,0,0,0.6 --det --mute --frames=60`, 1280x1280 per
  eye at 63 mm, with and without the flag. A discarded pixel differs from the run without the flag.
  Over 16x16 blocks inside the silhouette, the aircraft's disparity is the block's registration
  between the eyes on the solid frame, and the pattern's shift is the cyclic shift mapping one eye's
  discards onto the other's (`D17_blocks.py`, absolute pixel phase).
- **Before and after** (same pose, the stereo text switched off by a scratch edit for "before"):

  | eye FOV | aircraft disparity, median | screen-locked text | stereo text |
  |---|---|---|---|
  | 60° (21 px per degree, headset density) | -4.8 px | shift 0 in 86 of 87 blocks | within 1 px of the surface in 73 of 83 blocks, median 0.26 px |
  | 90° | -2.9 px | shift 0 in 23 of 25 | within 1 px in 24 of 25, median 0.14 px |
  | 120° | -1.6 px | shift 0 in 5 of 5 clean fits | within 1 px in 7 of 7, median 0.61 px |

  Before, the pattern sat on the display 4.8 px in front of the aircraft at headset density; after,
  it sits on the aircraft. The pattern repeats every 4 px, so a disparity near a multiple of 4 aliases
  (the 60° row's screen-locked blocks read "within 1 px" modulo 4 for that reason); the 90° and 120°
  rows are the unambiguous ones.
- **The plan's anchor was measured and rejected.** A flat varying holding the instance origin's
  projected pixel (one projection in `vertex()`) puts each part's pattern at its origin's depth. In
  the chase the parts' origins stand metres deeper than the rear surfaces the eye sees: at 60° the
  pattern sat a median 1.84 px off the surface, 11 of 56 blocks within a pixel. The landed text needs
  no varying: in `fragment()` it projects the view-axis point at `VERTEX.z` through the eye's own
  `PROJECTION_MATRIX` and shifts `FRAGCOORD` by that pixel, floored, modulo 4. Two eyes then put each
  surface point on the same pattern cell. With parallel eye axes it holds for asymmetric frusta too,
  since every point at one depth then has one eye-to-eye pixel difference. The pattern slips one pixel
  along depth contours where the floored shift steps, a line or two across an aircraft at 15 m.
- **Compiles and holds.** Every stereo capture, Original and Enhanced, logs no shader error;
  `stage-race-ghost-xr-sim` (`--debug-race-ghost --xr-sim`, chase, frame 60): `63839219` on four
  runs, frame-sensitive 26.66 %.
- **The clutter fade's verdict stands.** Neither `Mech3/Clutter.cs` nor
  `shaders/csky_clutter_fade.gdshaderinc` has changed since A4 (`git log 76e1547d4..HEAD` on both is
  empty), and B10 classed the clutter cards and glows "holds, correct as is": it changed which way a
  sprite faces, never where it stands, and the dither's mismatch is a card's position's disparity
  (1.4 px at the nearest authored fade start, 0.24 px at 300 m). If E21 finds it reads as a screen
  door, the ghost's stereo line ports to `Clutter.cs`'s and `SceneBuilder`'s clutter keep as is.
- The measurements and the rejected anchor are recorded in `docs/org/clutter.md`, "The dither under a
  headset".

**Evidence, A4 (confidence: traced to code and measured per eye, A4).** Soft particles, the
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

**Approach (landed, VR only).** `Mech3/SceneBuilder.cs` keys the bias shader's stereo ghost
(`raceGhost && StereoFacing.Live`, key bit 1048576) and writes `RaceGhost.StereoFragmentLine` in
place of `FragmentLine`; the stereo path is fixed for a launch, so each key's twins carry one text in
both graphics modes and the flat text is byte for byte what it was. `--debug-race-ghost`
(`SessionSpec.DebugRaceGhost`, `FlightRosterPolicy.DebugRaceGhost`, `HumanFlightAdapter`) is the
capture aid; `docs/cli.md` carries it. Alpha-to-coverage stays the alternative if the headset finds
the dither still reads as a mesh.

**Model recommendation.** Standard; the deciding fact (the per-part origin sits off the surface) came
from measuring.

**Verify.** Units: `SessionSpecTests.DebugRaceGhostIsItsOwnFlag`. Suites `race-ghost-fade`,
`race-presence` and the `xr`, `cockpit` and `race` filters pass with clean engine errors. The
before/after table re-runs with `.scratch/D17/D17_shot.ps1` and `D17_blocks.py` (the "before" needs
the stereo text switched off). Goldens: the new `stage-race-ghost-xr-sim`; the flat shots cannot
move (the flat text is unchanged and the flag is off in all of them). The four effects A4 found
correct were not re-captured: their screen and depth reads are unchanged since A4, and B10's stereo
texts changed only their basis, under its own suite.

Owed at the headset (the author's), VDXR at 90 Hz, Original then Enhanced:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=chase --debug-race-ghost
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,300,-6144" "--direction=-0.743,0,-0.669" --view=chase --debug-race-ghost --graphics=enhanced
```
Look for: (1) the aircraft ahead reads as a see-through aircraft at its own depth, its dots on its
skin, not as a mesh standing on the lenses in front of it; (2) as you turn your head the dots move
over the skin, as the flat ghost's do whenever the view moves: say whether that swim bothers you in
the headset; (3) say whether the ghost reads well enough or wants alpha-to-coverage.

**⚠ Traps.**
- The shaders are generated in C# (`Mech3/ShaderTwins.cs` pairs per graphics mode); a port touches
  both twins.
- Do not anchor the pattern to the instance origin: a part's origin is not on the surface the eye
  sees (Evidence).
- Measure a dither's shift in absolute pixel coordinates, and pick a disparity away from a multiple
  of 4: the pattern's period aliases a 4 px disparity to none.

**Verified.** The complete battery on the merged tree with D16, D17 and the exit-finalizer fix
(LOG-27): build PASS, units 6476 passed 3 skipped, engine 549 passed 2 skipped with clean engine
errors, goldens 35/35 hash-identical. A first run on the tree without the exit fix crashed one shard
at quit after a passing report. The headset checks named above are still owed.

## D18 ◐ Performance pass to the 90 Hz budget

**Outcome: the cost is found and cut on the stand-in; the headset's own frame times are owed.** The
flat options cost a VR cockpit 20-43 ms of GPU time at the runtime's 3072x3264 per eye, almost all
of it Enhanced's soft sun shadow, whose search radius on Godot 4.7.0 grows with the camera's distance
from the world origin (a Godot bug, fixed in 4.7.2). A VR quality profile (anti-aliasing off over
the project's 4x MSAA, shadow quality `low`, foveation off) flies every headset session at 5.7-6.3 ms
there and 2.7-3.2 ms at the author's 2112x2304, and a cutscene no longer draws the world behind its
opaque surround. At 1.0 resolution the profile sits 0.2-0.7 ms over Decision 10's half frame
(5.55 ms) and well inside the full 11.1 ms; the trades that close the gap are the author's (below).

**Goal.** A VR flight on the 5080 at the runtime's 1.0 resolution with Enhanced on holds 90 Hz using
at most half of the 11 ms frame.

**Budget (GPU ms per stereo frame, `--xr-sim` at `fov=96`, `--det --perf --no-vsync`, the median of
five 60-frame windows after two dropped; no other Godot ran during any row).** "Flat" is the author's
saved options (Enhanced, TAA, Ultra, 4x MSAA) through `--xr-quality=flat`; "VR" the profile;
"Original" the faithful look on the profile. Cockpit view at 600 m unless named.

| pose | 3072x3264 flat | 3072x3264 VR | 3072x3264 Original | 2112x2304 flat | 2112x2304 VR | 2112x2304 Original |
|---|---|---|---|---|---|---|
| C1 map centre (-6144, 600, -6144) | 19.93 | 5.80 | 2.21 | 9.92 | 2.92 | 1.91 |
| C1 past the edge (-40000, 600, -40000) | 42.82 | 5.71 | 2.21 | 21.24 | 2.86 | 1.83 |
| C5 night city (-9256, 250, -3155) | 27.78 | 6.25 | 3.91 | 13.99 | 3.17 | 2.79 |
| C2/M02 spawn | 10.55 | 5.71 | 2.33 | 5.27 | 2.89 | 1.67 |
| C4 snow (-3330, 958, -9181) | 29.33 | 5.80 | 2.27 | 14.59 | 2.91 | 1.60 |
| C1 dogfight, six attacking Furies | 20.06 | 5.87 | 2.23 | 10.33 | 2.97 | 1.52 |
| C1 centre, chase view | | | | 4.64 | 2.69 | 1.71 |
| C3/M01 intro cutscene (root + pair) | | | | 0.90 + 1.6 | 0.35 + 1.3 | 0.48 + pair |

Against Decision 10's half frame of 5.55 ms at 1.0 resolution: the VR profile is 0.16-0.70 ms over
on every Enhanced pose, Original is at 40-70 % of it, and at the author's 2112x2304 the profile uses
24-29 % of the frame. The dogfight's `frame_ms` (12-16 ms) is an instrument artefact (PERF-50), not
the game's.

**Evidence (confidence: measured on the stand-in for every cost and calibrated against the headset;
traced to the Godot 4.7-stable source for the shadow mechanism and checked against the upstream fix;
lead-only for anything the runtime's compositor adds).**
- **The author's runs.** Decision 10. The first cockpit batch (VDXR, Quest 3, 3072x3264 per eye at
  120 Hz, the saved options: Enhanced, TAA, ultra shadows, far view distance; C1) ran GPU-bound at
  20.4-23.8 ms mean GPU time (p90 25.9-28.6 ms) against the 8.3 ms period, about 41-49 fps, 80-98 %
  of frames late, in both render-thread modes. A fourth run at 90 Hz at 2112x2304 per eye averaged
  10.8 ms GPU (p90 12.6 ms), about 74 fps, 9 % late. Logs: `.scratch/headset/` in this worktree.
- **The stand-in reads the headset's GPU cost closely.** The same options at C1 centre read 11.06 ms
  at 2112x2304 against the headset's 10.8 ms mean, and 19.93 ms at 3072x3264 against 20.4-23.8 ms
  (PERF-51). It has no compositor and no lens distortion, so it ranks settings; the headset's
  `xr frames` line remains the verdict.
- **Cause of the soft-shadow cost: a Godot 4.7.0 bug.** The soft sun shadow's search radius is
  `(range_pos - range_begin) * tan(angle)` (`scene_forward_clustered.glsl`). `range_pos` is the
  vertex's view-space depth along the sun; `range_begin` is the cascade's near plane in world space
  (`renderer_scene_cull.cpp` `_light_instance_setup_directional_shadow`, `range_begin = z_max`). The
  radius therefore carries the camera's world position projected on the sun. Upstream fixed it in
  godotengine/godot#120774 (`range_begin = z_max - z_vec.dot(p_cam_transform.origin)`), shipped in
  4.7.2-stable and absent from 4.7.1. Measured: with the sun at C1's 90° yaw, 25° pitch, Ultra at
  2112x2304 read 19.18 ms at x=-20000, 14.86 at -12000, 9.82 at -6144, 6.19 at -2000, 5.52 at 0 and
  8.20 at +2000 (z held at -6144), and 10.23, 9.82 and 9.80 ms at z=-20000, -6144 and 0 (x held):
  the cost follows x, the sun's direction, and bottoms out where the camera's projection on the sun
  is near zero. Hard shadows read 3.48-3.50 ms at every x. High read 5.32 ms at centre and 9.09 at
  -20000, Medium 3.82 and 6.23, Low 3.61 and 3.55: every level with a penumbra carries the bug, Low
  (no angular distance) does not. Even the minimum (x=0) leaves Ultra 2 ms over Low at 2112x2304.
- **Why VR pays it and flat play much less.** The flat cockpit pass draws its interior in its own
  world at the origin; the VR cockpit (A3b, B8) draws in the main world, where the interior fills
  the eye at kilometres from the origin. Flat Enhanced at 1280x720 read 0.64, 0.72 and 0.99 ms at
  x=0, -6144 and -20000 (cockpit), 0.71, 0.81 and 1.08 ms (chase), 0.52 ms hard. **Flat-play defect,
  recorded:** flat Enhanced pays the same growth on its main world, small at these sizes, and the
  penumbra's width there changes with where the camera stands, so Ultra's judged look varies over
  the map. The fix is the engine upgrade to 4.7.2 (editor, export templates, .NET glue); it is
  expected to move the five Enhanced flat goldens (`c1-lake-enhanced`, `c5-city-night-enhanced`,
  `c1-cloud-deck-enhanced`, `c1-rocket-hit-enhanced`, `c1-cockpit-enhanced`) and to want Ultra's
  width judged again. **Recommendation:** a separate item for the 4.7.2 upgrade, landed on its own
  merits with the goldens re-pinned in the same commit; after it, the VR profile can try Medium
  (0.25°), which cost 0.2 ms over Low at the C1 centre on 4.7.0.
- **Every other Enhanced pass is small.** At 2112x2304 on the flat options (C1 centre, 9.82 ms):
  SSAO 0.26 ms, SSR 0.36, glow 0.25, TAA 0.64 against off, FXAA 0.0, 4x MSAA 1.13 against off and
  2x 0.48, view distance `normal` 0.0. At 3072x3264 on the VR profile (C5 6.25 ms, C1 5.80 ms): 2x
  MSAA saves 0.65/0.45, MSAA off 1.93/1.67, `--no-ssr` 0.73/0.69, `--no-ssao` 0.56/0.53, `--no-glow`
  0.75/0.72. Original costs 1.5-3.9 ms everywhere.
- **The cutscene's hidden world (D15's finding), fixed.** The headset's camera drew the whole world
  behind the opaque surround. With its mask cut to `SplitScreen.HeadsetLayer` for the frame (after
  the pair has read it, put back before every physics and process pass) the root read 0.90 ms
  against 1.82 on two interleaved pairs (2112x2304, flat options), draws 1,544 to 1,087. The pair's
  own two 1600x1200 renders cost 1.6 ms flat and 1.3 ms on the profile, outside `gpu_ms`. The cut
  also changes the root's pixels under TAA: 0.24 % of the frame, the screen's edges and the picture,
  because the world's motion vectors no longer reproject the screen; Original draws no TAA.
- **Foveation.** Godot 4.7's `xr/openxr/foveation_level` is Meta's extension and is disabled on
  desktop with MSAA on; the runtime-independent path is the root viewport's VRS mode `XR`, whose
  texture `OpenXRInterface.get_vrs_texture` builds from the eye foci (`XRVRS`) on Vulkan with
  fragment shading rate (the RTX 5080 has it). The stand-in now answers the same call. At C5,
  3072x3264, VR profile: 5.38 ms against 6.23 on two interleaved pairs, under the half frame; the
  centre of each eye is pixel-identical, and at the edge the night city's window textures read
  brighter (coarser shading picks coarser mips). Whether VDXR or SteamVR offer Meta's foveation
  extension was not checked: it needs a live runtime.
- **AA under multiview.** TAA keeps a history and velocity per view (D18's earlier bursts register
  each eye's TAA frame on its AA-off frame within 0.02 px). The profile drops it anyway: 0.64 ms at
  2112x2304, a smear a head turn shows, and MSAA already covers the edges.
- **The double image past the map edge (earlier diagnosis, kept).** No per-eye lag in the engine:
  flown bursts hold the left-to-right shift within 0.01 px; the one failing run (`fly-...195216`, 120 Hz,
  3072x3264, `--render-thread safe`) is the only one that collapsed to 11-12 fps, with 80-100 ms
  main-loop steps against 6-20 ms GPU time, which under the safe thread can be the runtime's own
  wait. At 20-43 ms of GPU past the edge on the flat options, that run was GPU-starved there; the
  profile removes the growth, so a rerun past the edge answers it.

**Resolved TODOs.**
- **The instrument:** `--xr-sim=eye=WxH,fov=96 --perf --no-vsync` under `--det` through
  `RunProbe.ps1`, reading `[perf] window` `gpu_ms` (the stereo root alone, PERF-51); A/B pairs run
  interleaved, with the Godot count recorded (zero other Godots in every row above). The perf
  stage now carries two stereo scenarios, `c1-cockpit-xr-sim` (2112x2304) and `c5-city-xr-sim`
  (3072x3264), which recorded 2.95 and 6.24 ms under `-Perf -PerfFilter xr-sim -PerfLabel
  d18-vr-profile`.
- Model recommendation: Standard for the profile; the engine upgrade is its own item.

**Approach (landed, VR only).**
- `Utils/VrQuality.cs`: the profile and its selection (`Resolve`, `Current`, `Describe`), the
  `--xr-quality=vr|flat`, `--msaa=` and `--xr-foveation=` lookups. `Launch/XrSession.cs` `WillBoot`
  says before any viewport is built whether a stereo path will start; `Launch/Launcher.cs` resolves
  the plan there, feeds its anti-aliasing word to `AntiAliasingSetting.FlagWord` and its shadow word
  to the three shadow resolves (launch, `GameSession.FollowPaneShadows`, options apply), writes the
  root's MSAA and VRS mode, and logs `[world] vr quality: profile=...`.
- `Utils/AntiAliasingSetting.cs`: `--anti-aliasing=` (`FlagWord`), a door any launch can use.
- `UI/Boards/TheaterScreen.cs`: `CutWorld` and `RestoreMask`, the cutscene's world cut.
- `Launch/XrSimInterface.cs`: `_GetVrsTexture` through `Xrvrs`, so the stand-in measures foveation.
- Flat unchanged: with no headset the plan is `none` and writes nothing; no flat flag's default
  moved. The new golden `c1-cockpit-xr-sim-enhanced` pins the profile's Enhanced look (three runs,
  `a1ca3522...`, frame 121 moves 27.15 %).

**The trades for the author (decisions, not taken).** To bring the 1.0-resolution frame under half
of 11.1 ms on every Enhanced pose, any one of: (a) `--xr-foveation=on` as the default, -0.85 ms at
C5, edge textures shift toward coarser mips; (b) 2x MSAA, -0.45 to -0.65 ms, softer edges; (c) SSR
off in VR, -0.7 ms, the water keeps only its own specular; (d) glow off in VR, -0.7 ms, flares and
fire lose their bloom; (e) a VR render scale default of 90 %, about -1 ms estimated from the pixel count, not measured. The author's own runtime
setting (2112x2304, about 70 %) already runs the profile at a quarter of the frame. Separately, the
4.7.2 engine upgrade (recommended above) restores a soft shadow at little cost.

**Verify.** Units: `CSVM.Tests/VrQualityTests.cs` (flat plan empty; the headset's profile words;
`flat`; each setting's flag over the profile; MSAA words; the flags' grammar; the anti-aliasing flag
over the saved word). Suites: `theater-cutscene` (the pair keeps the camera's own layers while the
headset draws its own layer alone, twice in a frame, and the hand-back restores them),
`theater-screen`. Golden: `c1-cockpit-xr-sim-enhanced`. Perf: the two stereo scenarios. The rules
that bite (docs/verification.md): PERF-49 (a soft shadow's cost depends on the pose), PERF-50 (the
spyglass census inflates `proc_ms`), PERF-51 (what `gpu_ms` covers under the stand-in), PERF-9
(interleaved A/B), PERF-39 (viewports outside the root).

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, its resolution at the runtime's
default for 90 Hz (the log's `eye_px=` says which), from the tree this lands in:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,600,-6144" "--direction=-0.743,0,-0.669" --graphics=enhanced
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-40000,600,-40000" "--direction=-0.743,0,-0.669" --graphics=enhanced
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C5 --plane=player_bhawk "--pos=-9256,250,-3155" "--direction=-0.588,0,-0.809" --graphics=enhanced
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C5 --plane=player_bhawk "--pos=-9256,250,-3155" "--direction=-0.588,0,-0.809" --graphics=original
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C5 --plane=player_bhawk "--pos=-9256,250,-3155" "--direction=-0.588,0,-0.809" --graphics=enhanced --xr-foveation=on
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-40000,600,-40000" "--direction=-0.743,0,-0.669" --graphics=enhanced --xr-quality=flat
```
Fly each for a minute in the cockpit, then chase. Look for: (1) the log's `[world] vr quality:
profile=vr anti_aliasing=off msaa=4x shadow_quality=low foveation=off` (and `profile=flat` on the
last line); (2) the `xr frames` lines: `gpu_ms` under 5.5 ms and `late` near 0 on the first four,
against the last line's flat options past the edge (expected 20 ms or more, the bug's control);
(3) whether the hard-edged sun shadow and the absence of TAA read acceptably in the cockpit and on
the ground, and whether the fifth line's edge of view looks different; (4) past the map edge, no
double image. Pick from the trades above if the first lines miss 5.5 ms.

**⚠ Traps.**
- Do not raise the profile's shadow to a level with a penumbra on this engine: its cost grows with
  the distance from the origin (PERF-49), and the VR cockpit pays it on every pixel it covers.
- Do not read a soft-shadow cost from one pose, and never from the flat cockpit pass, which draws at
  the origin.
- Do not read `proc_ms` from a window with a spyglass disc up (PERF-50).
- Do not cut the headset camera's mask before the pair has read it, nor leave it cut into the next
  frame's passes: the zone gate rewrites that mask from its own value each frame.
- The author's rig runs the sim clock behind wall time in late chapters; the stand-in rows are
  `--det`, so their `frame_ms` is not the headset's.

**Verified.** The complete battery on the merged tree with D18: build PASS, units 6490 passed 3
skipped, engine 550 passed 2 skipped with clean engine errors, goldens 37/37 hash-identical; exit 0.
The headset check named above is still owed.

## D19 ◐ Spyglass as a binocular quad

**Outcome: built and measured on the stand-in; the size, the place and the look are owed at the
headset.** The plan's premise was a zoom that narrows the field of view, and the flat spyglass is no
such thing: it is the picture of the selected target while the target is off screen, drawn in a disc
at the edge marker (`docs/org/spyglass.md`). A stereo session now shows that same picture, rendered
once for both eyes, on a disc on the head ring inside the selection's ring arrow, with the flat rim
and drop shadow. It follows the head, as the ring does. Flat play builds none of it; flat goldens
are untouched by construction (below).

**Goal.** In VR the spyglass zoom shows as a mono picture on a binocular-style quad instead of
narrowing the stereo FOV.

**Evidence (confidence: traced to code and the decode for what the picture is and where it aims;
measured on the stand-in for placement, mono-ness, disparity and size; the size and place are a
judgement, TUNE for the author).** Captures and instruments are in `.scratch/D19/` (`d19_mono.py`,
`d19_ring.py`, `d19_rim.py`, `d19_gold.py`).
- **What the flat spyglass aims along: the target, from the aircraft.** `Spyglass.Pose` stands the
  camera on the aircraft's drawn pose and looks at the target's drawn point, rolling with the
  aircraft for an aircraft target; the field of view frames the target (`2 atan(1.1 R / d)`). It
  never narrows the main view and never reads the nose or the head. The disc shows only while the
  target is off screen, armed (Shift+S toggles, on at load) and inside the fog-derived range gate.
- **VR aims the same way, not along the gaze.** The decode makes the picture, the arrow and the
  clock tag one object driven by the selection (`docs/org/spyglass.md`, "The headline"). A picture
  along the head's gaze would show what the headset already shows in stereo at full resolution, and
  would be a new feature the original lacks. The head decides only whether the picture is up (the
  target past C13's 25-degree ring cone, the stereo meaning of "off screen") and where it hangs (the
  target's bearing on the ring).
- **Where it hangs.** On the head ring at the ring's 2 m, its centre one radius inside the ring
  point, so its outer rim meets the arrow's tail, as the flat anchor moves half a window in for the
  disc. The label steps inward past the picture. Placed in view space by its shader, it rides the
  head with no lag and carries no world coordinate, C14's head-locked approach (A3b's eye feed is not
  needed for a head-locked quad). It faces the eye by construction, so no `StereoFacing` is involved
  (B10); like the ring labels it stays upright in the head's frame.
- **Size (TUNE).** `XrMarkerLaw.SpyglassScale` 1.5 times the flat disc's angle at the flat view's
  scale (C13's rule for every ring card): 4.96 degrees across in Nose and Chase, 7.20 in Cockpit.
  At the author's 21 headset pixels per degree that is 104 and 151 pixels, against 69 and 101 at the
  flat angle and 96 on a 1440p screen. The picture renders at `SpyglassSide` 288 texels square
  (`SpyglassTexelsPerRef` 2), at least 1.9 texels per headset pixel, filtered by four taps. The
  binocular shape is the flat disc's circle and rim: no `sgwin` art ships, and the flat port draws a
  circle.
- **Mono and its disparity, measured** (`--chapter=C1 --plane=player_bhawk --pos=-6144,600,-6144
  --direction=-0.743,0,-0.669 --view=nose --ai=player_fury --target=ai1_player_fury
  --hold=0,0,0,0.6 --det --mute --frames=60 --xr-sim=yaw=-70,eye=1600x1600,fov=60,shot=left|right`).
  A least-squares shift of the disc's interior between the eyes (SHOT-48) reads 44.60 px horizontal
  and 0.00 vertical, residual 0.80 levels RMS over 10289 px; unshifted the residual is 19.1 and a
  pixel off it is 2.3 (controls). The ring's own label and arrow read 44.60 and 44.50 px, so the
  disc carries exactly the ring's 2 m disparity; the nominal `f·b/z` is 43.65 px, and the 2.2 % over
  it is common to every ring card and to the disc's radius (61.4 px against 60.0 from the flat
  angle), a stand-in projection factor rather than the disc's.
- **The picture is the flat one.** The same pose with `--view=back` in flat play shows the flat
  disc's picture: the same Fury, sky band and cloud (`D19-flat-disc-crop.png` against
  `D19-xr-big-crops.png`).
- **Layers: a picture camera must not draw what rides the head.** The head-locked quads, the ring
  cards and the HUD panel place themselves in view space or from the eye feed for whatever camera
  draws them, and the spyglass camera draws the pane's mask. On layer 1 the whiteout, flash, sun
  wash and fade would wash the picture and the disc would sample itself. The two pieces:
  `SplitScreen.HeadsetLayer` (D15's finding; panel, theater, comfort quads), and a new
  `SplitScreen.HeadOverlayLayer` (bit 5, free) for the head-locked quads, the marker cards and the
  disc, which every pane camera and the cutscene pair still draw (D15's fade over a held film
  stands). `SpyglassView.DiscMask(..., headset: true)` drops both while `TargetHud.MarksInWorld`
  holds; flat play keeps the old mask bit for bit, and nothing flat stands on either layer.
- **The gate under a headset.** `TargetHud.StepSpyglass` no longer releases under `MarksInWorld`
  (C13 made it, which left the picture idle); `XrMarkers.AnswerSpyglass` answers it once a frame
  at priority 2100 off the tracked head, so the held subject keeps its wider release gate.

**Resolved TODOs.**
- C13's contract "the spyglass disc is not drawn in the headset, and `MarksInWorld` keeps the
  spyglass viewport idle under XR": the picture shows, and `MarksInWorld` now hands its gates to the
  stereo host. The spyglass's own markers (box, tags, pipper) stay out of the picture, as the flat
  disc shows none.
- D15's finding: the picture's mask drops `HeadsetLayer`, and `HeadOverlayLayer` with it.
- Priority: `HeadLockedQuads.SpyglassPriority` 117, over the ring (116), under the sun wash (120),
  the fade (121) and the vignette (122), as the flat disc sits in the HUD layer under the wash.
- The input stays `InputAction.ToggleSpyglass` (Shift+S, the pad's Misc1); nothing new is bound.
- Model recommendation: Standard.

**Approach (landed, VR only).** `Flight/Hud/XrSpyglassDisc.cs` (the quad and its shader);
`XrMarkers.AnswerSpyglass`, `Place(..., spyglass)`, `Disc`, `SpyglassKey`; `XrMarkerLaw`
`SpyglassScale`, `SpyglassTexelsPerRef`, `SpyglassRadiusRef`, `SpyglassSide`, `SpyglassOffsetRef`,
`SpyglassDeg`; `TargetHud.MarksInWorld` (sets the picture's `Headset`), `UpdateSpyglass(..., side)`,
`StepSpyglass`; `SpyglassView.Headset`, `DiscMask(..., headset)`; `SplitScreen.HeadOverlayLayer`;
`HeadLockedQuads.SpyglassPriority` and its quads' layer; the marker cards' layer.

**Verify.** Units: `XrMarkerLawTests` (the disc's radius, side, angles and texel density, its
place inside the arrow), `SpyglassTests.UnderAHeadsetTheDiscAlsoDropsTheHeadsetsTwoLayersAndNothingElse`.
Suite `xr-spyglass` (a real `TargetHud` and `SpyglassView`: up on the ring at 288 texels, the mask,
the HUD's own frame step keeping the held picture, the disc inside the arrow with the label past it,
a head turned 10 degrees and leaning 80 mm leaving the disc at its place in the head's frame,
looking at the target, disarming and a hidden HUD each taking it down, each with a control); with
`StepSpyglass` releasing under a headset again it fails on the frame-step check (METHOD-9). Golden
`c1-spyglass-xr-sim` (the probe above at `eye=640x720,fov=70`): `e439ac87510fac5b420a722a57bc7088`
on three runs, frame-sensitive 39.14 %.

Owed at the headset (the author's), runtime at 90 Hz, VDXR first, Original then Enhanced:
```
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,600,-6144" "--direction=-0.743,0,-0.669" --view=nose --ai=player_fury --target=ai1_player_fury
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,600,-6144" "--direction=-0.743,0,-0.669" --view=cockpit "--ai=player_fury:n=2" --ai-attack --target=nearest
.\RunGame.ps1 -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,600,-6144" "--direction=-0.743,0,-0.669" --view=cockpit "--ai=player_fury:n=2" --ai-attack --target=nearest --graphics=enhanced
```
Look for: (1) first line: the Fury sits 250 m ahead; turn your head about 30 degrees or more away
from it and a round picture of it appears on the ring inside its arrow, the name and clock tag
beside it; turn back and it closes as the box returns; say whether the picture should be larger or
smaller (`SpyglassScale` 1.5) and whether it reads sharp; (2) the picture is one flat image at the
ring's depth, not doubled, and moves with your head as the ring does; (3) Shift+S (pad: Misc1, the
Share or Capture button) turns it off and on, armed at load; (4) the second line, a dogfight in
Cockpit: the picture follows a Fury past your shoulder, shows no box, tag or whiteout inside it, and
whether the place inside the ring arrow works or it should stand elsewhere; (5) under Enhanced (third
line), the picture's contrast against the world: the headset's AgX tonemaps the already tonemapped
picture once more (SHOT-46).

**⚠ Traps.**
- Do not aim the stereo picture along the head's gaze: the spyglass is the selection's picture, and
  the head only gates it and places it.
- Do not put a head-locked or eye-fed quad on layer 1: every picture camera draws that layer, and a
  view-space quad lands in its picture. `HeadOverlayLayer` for what the cutscene pair should keep,
  `HeadsetLayer` for what it should not.
- Do not release the picture in `TargetHud`'s own frame step under a headset; the stereo host
  answers the gate after it, and a release each frame resets the held subject's wider gate.
- Under Enhanced the picture is tonemapped twice, its own viewport's and the headset's (SHOT-46),
  as the HUD panel and the cards are.
- The flat camera's Fov sizes the disc through the ring's scale, so a change to the view's angle
  under a headset resizes it too.

**Verified.** The complete battery on the item's tree (the plan branch plus D19): build PASS, units
6481 passed 3 skipped, engine 550 passed 2 skipped with clean engine errors, goldens 36/36
hash-identical. D19 found that the Danger Zone photograph's camera (`OutsideCullMask` of the pane's
mask) would draw the head-locked quads, marker cards and HUD panel into the photo under VR; its mask
now drops `HeadsetLayer` and `HeadOverlayLayer` (Decision 15: the photograph reads a mono camera).
The headset check named above is still owed.

# Wave E, headset pass and release

## E20 ◐ Per-plane scale and eye-offset tuning at the headset

**Outcome: the lab is built and pinned on the stand-in; the author's per-plane judgement is owed at
the headset.** `--xr-tune` turns on live nudges of the flown plane's interior scale and seated eye
offset (fore and aft, up and down) from the right hand controller with its grip held, a readout on
the HUD panel, a save into `vr_cockpits.json` in the tree the game runs from, a reset to the file,
and a step to the next plane by the mission airframe swap, so one headset session covers all eleven.
Off by default; a normal VR launch and flat play build none of it.

**Goal.** Each of the 11 planes has an interior scale and eye offset in `data/vr_cockpits.json`
that the author has judged right at the headset. (Decision 13's "world scale" is the interior
mount's factor: the world and the hulls are metres, and `XRServer.WorldScale` stays 1.0, A5.)

**Evidence (confidence: traced to code for the live levers, the swap and the writer; measured on the
stand-in for the nudges reaching the eye and the interior; the values themselves are the author's).**
Captures are in `.scratch/E20/`.
- **The lab over editing between runs.** A relaunch per value costs a boot and a flight to the spot
  each time, eleven planes over; the mission's airframe swap (`FlightRoster.RunSwap`, codes 965-967)
  already rebuilds the flown seat on another airframe at the pose it was flying, so a plane step
  needs no relaunch. The lab sends it an order with code 0 (no captured paint, no award build) and
  `handsOver: false`, then rewires the seat as the mission path does. On the stand-in
  (`--chapter=C1 --plane=player_bhawk --view=cockpit --xr-tune --xr-sim`, a scratch hook stepping
  the plane at frame 30, not in the tree) the log reads `xr tune: swapping player_bhawk for
  player_pfighter`, a new stereo interior for `player_pfighter` (collimated reticle, head plate left
  out), `xr: cut (a new aircraft)`, and the frame shows the Devastator's cockpit with one HUD on the
  panel (`E20-swap-left.png`).
- **The scale is live without a rebuild.** `XrCockpit.SetMountScale` rescales the captured mount
  about the interior's origin, the authored eye, and the next `Follow` places it; a lifted interior
  also takes the new `Everywhere` box. The depth bias stays the build's (`SceneBuilder.DepthBiasScale`,
  baked into the materials), a fraction of view distance, so a priority level's separation scales
  with the factor in the lab and is exact again on the next launch with the saved value.
- **The eye offset is live**: `XrCockpit.EyeOffset` is read by `XrRig.Place` every frame.
- **Up and down is offered; lateral is not.** A5's trap (a vertical offset puts parallax between the
  sight and the guns) predates C13b. The collimated overlay takes, per fragment, the texel the
  authored eye sees along each eye's ray (`GunsightReticle.cs`'s shader), so the reticle stays on the
  guns from any eye and only the window the glass gives on it follows the head centre (`seen`). The
  file gains an optional `eyeOffsetY` along the mount's tilted Y, positive up, read in the same ±1 m
  range; no plane carries one until the author saves one. A lateral offset stays out: the authored
  eye is where each tub puts it (A5 on the Kestrel and the Peacemaker).
- **The save keeps the file.** `VrCockpitTuning.TryRewrite` edits the plane's number tokens in the
  file's own bytes, adds a missing field after the last present one on the entry's own layout (or a
  missing plane after the last), keeps the line endings and a byte order mark, and refuses a rewrite
  that does not read back with every other entry unchanged. Rewriting each of the eleven committed
  entries with its own values leaves the file byte-identical; a Warhawk save of (0.875, 0.06) changes
  exactly its two numbers (`VrCockpitTuningTests`). The disk path is `res://data/vr_cockpits.json`
  globalized, so a `RunGame.ps1` launch writes into its own tree; an exported build keeps the file
  in the pck and refuses the save with a readout line. The reader now skips a byte order mark, which
  `JsonDocument` refuses (a file saved by an editor that adds one used to fall back to the defaults).
- **Suite `xr-cockpit-tune`** (the Bloodhawk's interior over a rig on a live stand-in interface,
  scripted controllers, a copy of the committed file): with the grip up the stick and B move nothing;
  grip and stick forward move the head 10 mm along the tilted sight line, held past 0.45 s it repeats;
  B raises it 10 mm; the interior stays on the airframe (within 2 mm, 7 km out on a rotated
  attitude); stick right scales the lifted interior 0.04 to 0.041 m per unit about the authored eye
  (still on the marker) with the reach box following; the trigger writes the plane and leaves the
  other ten entries and the notes; the stick click resets; the left stick asks for the next plane
  once per push; a plane flown again takes its saved values and another its unsaved nudge; a pause
  holds every control; F22 to F24 drive the same. Control (METHOD-9): with the live apply removed the
  suite fails 8 checks (the eye, the repeat, B, the scale, the reach box, the reset, both re-flown
  planes).
- **The readout** sits at the top left of the panel surface (28 px text, about a degree tall at its
  31 rows per degree), one line with the grip up, the full card (values, steps, the file's values,
  the controls) with it held, clear of the gunsight on the Warhawk's tall glass
  (`E20-idle-left.png`, `E20-card-left.png`). Every change logs `xr tune: <plane> scale ... aft ... up ...`.

**Resolved TODOs.** The lab is built rather than editing the JSON between runs (above). Model
recommendation: Standard.

**Approach (landed, VR only).** `Flight/Hud/XrCockpitTuner.cs` (the node, the controller reading, the
readout, the save) and `Flight/Hud/VrCockpitTuning.cs` (engine-free: `Step`, `NextPlane`, `Format`,
`Readout`, `Summary`, `TryRewrite`, `VrTuneRepeat`); `XrCockpit.EyeOffset` settable and
`SetMountScale`; `VrCockpit.EyeOffsetY` and `VrCockpits.Names`; `SessionSpec.XrTune`;
`GameSession` builds the lab beside the HUD panel when the flag is set (`TunedSeat`,
`SwapTunedAirframe`). Controls: docs/controls.md, "The cockpit tuning lab"; flag: docs/cli.md.

**Model recommendation.** Standard.

**Verify.** Units: `VrCockpitTuningTests` (the steps on the grid, the clamps, the format, the plane
order, the repeat, the byte-identical rewrite of every committed entry, a field and an entry added on
the file's layout, a byte order mark and non-ASCII notes kept, the refusals, the readout, the flag),
`VrCockpitsTests` (all eleven planes named in range, `eyeOffsetY` read and its tilted axis, the file's
order). Suite `xr-cockpit-tune` (weight 0.42 s in `analysis/engine-suite-weights.json`). No golden:
the readout shows only under `--xr-tune`; on the item's tree the goldens stage was 37/37
hash-identical.

Owed at the headset (the author's), VDXR at 90 Hz: the per-plane sign-off. One session covers all
eleven through the plane step; the per-plane lines are the fallback if a swap misbehaves:
```
$env:CSVM_DATA_ROOT='Z:\CSVM'; & 'Z:\CSVM\.claude\worktrees\vr\RunGame.ps1' -EngineArgs '--xr-mode','on' --chapter=C1 --plane=player_bhawk "--pos=-6144,600,-6144" "--direction=-0.743,0,-0.669" --view=cockpit --xr-tune
```
Planes in the file's order: `player_bhawk` (Bloodhawk), `player_pfighter` (Devastator),
`player_fury`, `player_warhawk`, `player_autogyro` (Hoplite), `player_avenger` (Hellhound),
`player_balmoral`, `player_brigand`, `player_fbrand` (Firebrand), `player_kestrel`,
`player_peacemaker`. A5's starting points to try (the file's `_proposed` notes): Warhawk aft 0.06;
Hoplite, Hellhound and Balmoral scale 0.875. A5's forward move for the head plate is moot, since the
stereo interior leaves the plate out (B8, the `head plate left out` log line). Per plane: judge the
panel's distance and size and the tub's width with the grip up, nudge, save with the trigger, then
the left stick to the next plane; `xr tune: saved ...` in the log is the record.

**⚠ Traps.**
- Look judgements are the author's; a measured scale does not override them.
- The lab's live scale keeps the depth bias the launch built; judge a decal's z-order at the value
  only after a relaunch with it saved.
- A saved Bloodhawk scale moves the five stereo cockpit goldens (`c1-cockpit-xr-sim`, `-enhanced`,
  `-far`, `-aft`, `stage-gunsight-xr-sim`); a Bloodhawk eye offset also moves the first-person
  Nose ones (`c3-flare-xr-sim`, `c1-moon-xr-sim`, `stage-markers-xr-sim`, `stage-hud-panel-xr-sim`,
  `c1-spyglass-xr-sim`), since the offset applies in every first-person view. Re-pin them in the
  commit that lands the values.
- The save writes the tree the game runs from (`vr` for the line above); the values reach main only
  through that tree's commit.
- A vertical offset moves the window the glass gives on the collimated reticle: a few centimetres
  start cutting the ring at the glass's edge, as leaning does.
- Grip and trigger also click a theater screen that is up; the lab is silent under a pause and the
  photo mode, but another board raised in flight would take the click.

**Verified.** The complete battery on the merged tree with E20 and E22: build PASS, units 6517 passed
3 skipped, engine 551 passed 2 skipped with clean engine errors, goldens 37/37 hash-identical;
exit 0. The headset check named above is still owed.

## E21 ☐ At-the-headset playtests on both runtimes

**Goal.** The author has flown each mode in VR on VDXR and on Steam Link/SteamVR and judged comfort,
controller binding, the theater screen, the HUD and the cutscene window.

**Evidence (confidence: lead-only).** Decision 11.

**Approach.** File the checks as `playtest` GitHub issues (`docs/agents/issue-tracker.md`).
<TODO: the check list per mode.>

**Model recommendation.** <TODO: tier not settled in the session.>

**Verify.** The issues closed with the author's verdicts.

**⚠ Traps.** <TODO: none known yet.>

## E22 ◐ Experimental release: docs, shortcut, known issues

**Outcome: the docs, the launcher and the draft notes are written; publishing waits for E21 and the
author.** The zip gains `CSVM-VR.cmd`, a one-line launcher that runs `CSVM.exe --xr-mode on`;
`packaging/README.md` has a "VR (experimental)" section; `docs/release-notes-v0.4.0.md` drafts the
VR part of the notes and a provisional known-issues list. No game code changed.

**Goal.** v0.4.0 ships VR labelled experimental, with the VR shortcut in the archive, the option and
flags documented, and a known-issues list.

**Evidence (confidence: traced to code and the scripts for the launcher, the flags and the
bindings; measured with a stand-in exe for the launcher's command line; lead-only for the
known-issues list, which E21 settles).**
- **The version.** `CSVM/project.godot` says `0.3.0`, the newest tag is `v0.3.0`, and
  `docs/tooling.md`'s rule makes a release that adds a feature a minor bump, so Decision 14's v0.4.0
  holds. The bump is a release-time commit, as `65f9deba9` was for 0.3.0: `config/version`, the two
  READMEs' archive names, and a re-pin of `menu-original-flag`, whose corner stamp shows the
  version. Main also carries PLAN-bots since v0.3.0, so the notes' other sections are written then.
- **The release notes convention.** `docs/release-notes-v<version>.md`, passed to
  `PublishRelease.ps1 -NotesFile`, which appends the verification and provenance sections itself and
  refuses a file that states a checksum.
- **The shimmer is no known issue.** This item's earlier evidence named the cockpit and HUD-panel
  shimmer far from the chapter origin. A3b drew the interior from the XR origin's frame and the author
  saw it hold steady at the headset (Decision 17's last sentence); C12 measured the HUD panel at
  0.010 px at 0 and 20 km, the instrument's own drift. The Milestone goal's fourth bullet still
  states the old expectation.
- **The engine argument in an exported build.** Godot reads `--xr-mode` ahead of `--` in the release
  template too: `windows_release_x86_64.exe` (4.7 stable mono) carries the argument and its help
  text ("Select XR (Extended Reality) mode"), found by a byte search, nothing run. B6's relaunch
  already builds the same command line from the process's own arguments (`Launch/XrRelaunch.cs`).
- **Why a `.cmd` and not a shortcut.** A `.lnk` stores an absolute target, which an unzipped folder
  cannot know. The batch file runs `start "" /d "%~dp0." "%~dp0CSVM.exe" --xr-mode on %*`: its own
  folder as the working directory (as a double-click of `CSVM.exe` has), and whatever it is given
  after the engine argument. `start` is needed because cmd waits for a GUI program started from a
  script.
- **The launcher's command line, measured.** A stand-in `CSVM.exe` compiled with `Add-Type
  -OutputType WindowsApplication` (no window) that writes its own command line, beside a copy of the
  launcher in `.scratch/E22-launcher/`, run from `%TEMP%`: `CSVM.exe --xr-mode on`, working
  directory the launcher's folder. With `-- --xr-quality=flat --chapter=C1` (from cmd, and from
  PowerShell 5.1, which passes the `--` through): `CSVM.exe --xr-mode on -- --xr-quality=flat
  --chapter=C1`. From a folder named `with space (x86)`: the same, quoted correctly.
- **The docs audit.** Every XR flag the parser reads (`--xr-sim`, `--xr-chase`,
  `--xr-render-scale`, `--xr-vignette`, `--xr-quality`, `--xr-foveation`, `--msaa`,
  `--anti-aliasing`, `--shadow-quality`, `--no-soft-shadows`; the engine's `--xr-mode` and
  `--render-thread`) has its index entry and bullet in `docs/cli.md`, and their words and defaults
  match `Utils/VrQuality.cs`, `VrRenderScaleSetting.cs` and `VrVignetteSetting.cs`. `--xr-sim` has
  no keys, only spec tokens. `docs/controls.md`'s XR table and Recenter row match
  `Bindings/DefaultBindings.cs` (left menu, left stick click, Y; `Home`, touchpad, paddle 1),
  `Bindings/XrControls.cs` and the action map's four profiles. Two lines were stale: the `--xr-sim`
  bullet's "Canvas layers draw nothing in stereo" (the theater screen and the HUD panel take them
  now), and the `--xr-mode` bullet said nothing of an exported build.

**Approach (landed).**
- `packaging/CSVM-VR.cmd`, the launcher; a `$ReleaseFiles` row in `ExportRelease.ps1`; a
  `packaging/MANIFEST.md` row, and its "no script" sentence and Linux paragraph corrected (the
  tarball leaves the launcher out); `.gitattributes` checks `packaging/*.cmd` out CRLF.
- `packaging/README.md`: "VR (experimental)" (runtimes, 90 Hz, starting, playing, recentering, the
  quality profile and `--xr-quality=flat`, the comfort rows, going back to flat) and the launcher in
  the file list. `README.md`: a Status bullet pointing there. `docs/tooling.md`: the launcher and
  how to prove it without the real exe.
- `docs/cli.md`: the two stale lines above. `docs/controls.md` needed nothing.
- `docs/release-notes-v0.4.0.md`: a draft holding the VR feature section, the Enhanced flare change
  (C14, the one flat change of this plan) and the provisional VR known issues, read off every item's
  Outcome, Evidence and Traps: one runtime and one headset tried; the card it was sized on; hard
  shadows in VR (D18, the Godot 4.7.0 soft-shadow cost); Enhanced's second tonemap on the HUD panel,
  the cards, the spyglass picture and the boards (SHOT-46); one cockpit scale for every plane (E20);
  no recenter on an Xbox pad (B7, B9); the flat window's flash on a saved-on start (B6); the desktop
  mouse under a headset (C10); the cutscene window's shallow depth and edge cut (D15); Windows only.
  An HTML comment at its top says what is still to write and that the list is provisional.

**Model recommendation.** Standard: documentation and packaging, no engine code.

**Verify.** `CheckDocEntries.ps1` and `CheckCommentCaps.ps1` clean; the complete battery on the item's
tree (nothing under `CSVM/` changed, so it shows the tree is green, not this item). The launcher by
the stand-in exe above.

At release time (the author's; the sandbox opens a window, so no agent runs it):
1. Bump the version as above, finish the notes, and delete their opening comment.
2. `.\ExportRelease.ps1`, then the clean-machine run with VR off, which follows
   `packaging/README.md` on the zip with the mark of the web:
   `.\RunSandbox.ps1 -Driver sandbox\PublicRelease.ps1 -MapReadOnly <retail install> -KeepOpen`.
   The flat game must extract, reach the menu and fly C1 as in v0.3.0's run. With `-KeepOpen`,
   double-click `CSVM-VR.cmd` in the folder the driver unzipped to
   (`Downloads\CSVM-v0.4.0-win64\` in the sandbox), noting whether Windows asks first, as it does
   for `CSVM.exe` with the mark of the web. The sandbox has no OpenXR runtime, so the game must
   start flat with the notice "VR mode is on, but no headset started" on the menu, and its log must
   show the OpenXR warning. This runs the real launcher against the real exe with no headset in
   reach.
3. At the headset, B6's owed exported-build line: with VDXR running, double-click
   `.scratch\export\CSVM-VR.cmd`; the newest file in `.scratch\export\logs\` must carry
   `xr: OpenXR live runtime=` and `[world] vr quality: profile=vr`. Then turn VR Mode on in Options,
   quit, double-click `CSVM.exe`, and the game must restart into the headset.
4. `.\PublishRelease.ps1 -NotesFile docs\release-notes-v0.4.0.md -DryRun`, then the publish.

**⚠ Traps.**
- Do not prove the launcher by running it next to the real `CSVM.exe` on this machine: it starts
  OpenXR on the attached headset. The stand-in exe or the sandbox are the two safe ways.
- Do not give the launcher a `.lnk` form or a hard-coded path: the folder is wherever the player
  unzipped it.
- Do not drop `start`: a batch file waits for the GUI program it runs, which leaves a console
  window open for the whole session.
- Do not list the cockpit or HUD-panel shimmer as a known issue on the strength of A3; A3b and C12
  superseded it. Settle the rest of the list from E21's verdicts, not from this draft.
- The release notes are published verbatim: an unremoved draft comment does not render, but the
  "provisional" list does.

**Verified.** The complete battery on the merged tree with E20 and E22: build PASS, units 6517 passed
3 skipped, engine 551 passed 2 skipped with clean engine errors, goldens 37/37 hash-identical;
exit 0. The headset check named above is still owed.
