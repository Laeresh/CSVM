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

### Wave C, screen, HUD and input

10. ◐ C10 The virtual theater screen for menus, boards and cinemas
11. ◐ C11 `DeviceKind.Xr`, XR actions and the screen pointer
12. ☐ C12 The cockpit-locked HUD panel
13. ☐ C13 World-tied markers and the edge-marker ring
   ◐ C13b The gunsight reticle collimated at infinity in VR
14. ◐ C14 An angle-based world flare rig (VR and flat Enhanced); the other pane effects as near quads in VR

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
   launch's `.scratch/logs/menu-*.log` ends `xr: relaunching into the headset (...)`, and the child's
   log carries `xr: OpenXR live runtime=...`.
2. In that headset launch, Options, VR mode Off, ACCEPT CHANGES: it relaunches flat, logging
   `xr: swept N shader cache files` before `xr: relaunching flat`.
3. With VR mode left On, quit and run `.\RunGame.ps1` again: one window opens and closes, the
   headset launch follows (`xr: relaunching into the headset (VR mode is saved on)`).
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

**Verified.** <pending orchestrator run>

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
flare): the core, the three rings and the wash quad in both eyes.

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
and with this item's code switched off; on `--render-thread safe` it exited 0 in 28 of 28.
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

**Evidence (confidence: measured, three runs).** Decision 10. The author's first cockpit batch
(VDXR, Quest 3, 3072x3264 per eye at 120 Hz, the saved options: Enhanced, TAA, ultra shadows, far
view distance; C1) ran GPU-bound at 20.4-23.8 ms mean GPU time (p90 25.9-28.6 ms) against the
8.3 ms period, about 41-49 fps, with 80-98 % of frames late, in both render-thread modes; the author
saw it as "performance problems". The runtime was at 120 Hz, not Decision 10's 90; the per-second
`xr frames` lines in each `fly-*.log` are the instrument; they carry the seat's `pos=`, ground
speed `mps=` and `main_max_ms=`. A fourth run at 90 Hz with the runtime's
resolution lowered to 2112x2304 per eye (same options) averaged 10.8 ms GPU time (p90 12.6 ms)
against the 11.1 ms period, about 74 fps, 9 % late: at the budget, still twice Decision 10's half
frame.

**The "one eye lagged behind" double image past the map edge (diagnosis, confidence: measured on
the stand-in; the runtime's share is the headset's to decide).**
- **No per-eye lag in the engine.** Flown `--xr-sim` bursts (`--frames=240 --shots=4`, nose view at
  600 m, C1 in-map and at -40000, TAA against AA off) hold the L-to-R shift constant within 0.01 px
  and both eyes' frame-to-frame world motion equal within 0.01 px. Each eye's TAA frame registers on
  its AA-off frame within 0.02 px. Cockpit bursts at -50000 skimming terrain at frame 480 register
  noisily (near parallax), equally with TAA and without, and TAA against AA off stays within
  0.08 px. TAA keeps a history and a velocity slice
  per view (`taa.cpp` `TAA::process`). World-scale eye rounding is A3's millimetre, which is
  sub-pixel past a few metres.
- **The failing run is the only one that collapsed to 11-12 fps.** `fly-...195216` (120 Hz,
  3072x3264, `--render-thread safe`) ends with about 45 s at 11-12 frames a second, with 80-100 ms
  main-loop steps against a GPU time of 6-20 ms and about 217 draws. The other three runs have no
  such stretch. `main.cpp` counts `RenderingServer::draw` in the process time, so under the safe
  thread the stall can be the runtime's swapchain or end-frame wait as well as game code.
- **Past the map edge, Enhanced's soft sun shadow costs more than twice as much.** At 2112x2304 per
  eye in cockpit view at 5000 m, GPU time was 8.8 ms at -6000 and 20.1 ms at -40000. With
  `--no-soft-shadows` it was 2.8 ms, and with `--shadow-quality=off` 2.7 ms. Along -x/-z at
  2000 m it was 13.0 ms at -11500, 14.5 ms at -13000, 17.7 ms at -20000 and 18.5 ms at -50000.
  This item owns that cost.


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
