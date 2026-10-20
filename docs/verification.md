# Verifying a change in this project

This file contains transferable verification rules. Dated evidence belongs in commit messages,
analysis findings, or git history; a module constraint belongs as a comment on the member it binds,
and format or decode knowledge in `docs/formats/` or `docs/org/`.

Read **METHOD** first, then only the relevant section. Rule IDs are permanent: append new rules
and leave gaps when retiring old ones. Each rule is a bold one- or two-line imperative plus at most
one sentence of measured evidence; everything else belongs in the commit that landed it. A rule
that only holds for one suite, one flag or one module is not a rule: it is a comment on that
member, and it does not go here. Before minting a rule, search this file for one that already
covers it.

## METHOD, designing any measurement

- **METHOD-1**, **Choose a case that distinguishes the hypothesis from its alternatives.**
- **METHOD-2**, **Measure same-build variation before treating an A/B difference as signal.**
- **METHOD-3**, **Re-measure the baseline near the changed run.**
- **METHOD-5**, **Change one variable at a time and keep the baseline reproducible.**
- **METHOD-6**, **Prove which binary and inputs each side of an A/B used.**
- **METHOD-7**, **Verify independent branches before their combination.**
- **METHOD-8**, **Reproduce every “pre-existing” failure on the unchanged build.**
- **METHOD-9**, **Show that verification can fail under a deliberate perturbation.**
- **METHOD-10**, **A check that passes a no-op does not verify the change.**
- **METHOD-11**, **Sweep representative poses, times, or inputs.**
- **METHOD-12**, **State which measurements should change and which should remain invariant.**
- **METHOD-14**, **Test reconciliation metrics for degeneracy.**
- **METHOD-15**, **Confirm the intervention took effect before crediting it.**
- **METHOD-16**, **After restoring inputs, force or verify the rebuild.**
- **METHOD-17**, **Use `git diff` to prove temporary edits are restored.**
- **METHOD-20**, **Reproduce a published measurement's pose AND its statistic, not just its
  subject.** `CAP-13`'s ring brightnesses were read under the sun wash with a brightest-pixel
  statistic, and an annulus median at a clean pose reads lower.
- **METHOD-21**, **Hold the condition a capture was flown in, not just its initial value; a
  scenario entered at a speed it cannot hold is not a measurement at that speed.** The original's
  143 mph knife-edge take, replayed at full throttle, passes 290 mph inside three seconds.
- **METHOD-22**, **Re-measure a fitted constant ALONE once the mechanisms it was fitted on top of
  have been replaced; it may now push the wrong way.** Removing `ClimbGravityScale` by itself moved
  the sustained climb from 276.7 to 257.7 mph, toward the original's 163.1.
- **METHOD-23**, **"Unreachable" is a measurement, not an inference from the constants around it:
  pin the margin with an instrument and assert it against the loaded value, never the literal.**
  The Bloodhawk's peak G demand sits 0.2 % past the executable's own fallback threshold.
- **METHOD-27**, **A count over CSVM's own scene structure is not a count over the original's;
  name the parent before reading a number as a count of objects.** One flak over a C1 aagun dealt
  four splash shares because each node is carved into a `col` and a `col_buildings` body.
- **METHOD-28**, **A/B a new opt-in flag against the code it replaces, never against the flag
  switched off once that code is already deleted.**
- **METHOD-30**, **Span a hand-back with the press itself: a modal that consumes a press hands
  input back while that press is still down.** The click that skipped a campaign film fired the
  plaque under the pointer on its release.
- **METHOD-31**, **Settle a hit-rate question by rastering rays over the silhouette against both
  geometries; a convex collider only adds hit area, and a kill-time stopwatch folds in damage.**
  The airframe hulls present 1.16 to 4.39 times the model's own silhouette.
- **METHOD-32**, **Compare a deliberately delayed reconstruction at its own lag, fitted in
  fractions of a step, before reading the residual.** Fitting the replicated aeroplane's lag
  fractionally moved its reading from 0.60 m to 0.52 m mean.
- **METHOD-33**, **Judge replicated motion on two independent frame loops; lockstep lands every
  sample on a step edge and hides arrival jitter.** The arrival-stamped pose buffer passed the
  lockstep suite while a frame-looped model read 15 to 33 % rms speed error.

## DIAG, chasing a symptom

- **DIAG-1**, **Check the premise against data before writing code.**
- **DIAG-6**, **Correlation is not a mechanism.**
- **DIAG-8**, **Audit inherited claims back to their inputs and method.**
- **DIAG-10**, **Explain cases where the change is inert by construction.**
- **DIAG-11**, **Identify what moved before calling a deviation a regression.**
- **DIAG-13**, **Visible activity proves execution, not correctness.**
- **DIAG-15**, **Never silently skip unsupported or failed cases.**
- **DIAG-17**, **Distinguish “never reached” from “reached but invisible.”**
- **DIAG-18**, **Test code-derived explanations at runtime.**
- **DIAG-19**, **Scripted repros inherit identity defaults (-Z heading, near-origin spawn); repro a
  pose-dependent symptom off-axis and far from the origin, and read a passing repro as a pose gap.**
- **DIAG-20**, **An effect that never starts passes every "it ends correctly" check; prove the
  START from the production call path before diagnosing the stop.**
- **DIAG-22**, **A suite that asserts over a directive the data may not author fails vacuously;
  loop over the authored candidates, then branch on the count.**
- **DIAG-23**, **Dispatching a callback directly tests what the code does, never what the codes
  around it undo; play the definition that raises it.** CM02's 967 alone left the suite green while
  the definition's `913`/`914` pair put the hidden aeroplane back 19.25 s later.
- **DIAG-24**, **A fixed sim window shorter than an episode's own completion time reads as an
  unresolved bind, not a slow clock.**
- **DIAG-26**, **A mission's intro cutscene holds the world (`SimHeld`): no objective completes and
  no AI, turret or projectile runs while the clock and screenshots advance.** Reach a chapter
  through a mission with no intro, or give a `--campaign=` run a minute and read its `cutscene:` lines.
- **DIAG-27**, **Count a sortie's deaths off the lines that name a death, never off the absence of
  a damage line.** `DamageAt` logs only its first twelve hits, and `zep: … engines N/14` is a poll.

## SHOT, screenshots and pixel evidence

- **SHOT-1**, **Respect capture quantisation; tiny effects need another metric.**
- **SHOT-3**, **Use state logs when pixels cannot resolve an effect.**
- **SHOT-6**, **Compare decoded pixels, not encoded image bytes.**
- **SHOT-9**, **Do not combine screenshots with `--headless`.**
- **SHOT-10**, **Create the output directory, use an absolute path, and verify the screenshot exists.**
- **SHOT-12**, **Frame the time-driven surface, then perturb time.**
- **SHOT-14**, **Treat census counts as lower bounds; disable fog and reject tiny counts.**
- **SHOT-16**, **Hide capture windows; never minimize them.**
- **SHOT-17**, **Sample transitions mid-ramp.**
- **SHOT-19**, **Use a time series and isolate overlapping emitters.**
- **SHOT-20**, **Locate a saturated feature by the centroid of its plateau, not by `argmax`.**
- **SHOT-21**, **Keep radial probes clear of the frame edge.** A circular sample that runs
  off-frame reads clamped pixels as content and reports a confident zero.
- **SHOT-27**, **The true horizon is a CALIBRATED row, not the sky/terrain boundary: shoot the same
  position level and check the shift is `f·tan(pitch)`.**
- **SHOT-28**, **A nadir shot cannot tell a painted rooftop from an extruded building, and a global
  instance count cannot tell you where the instances went; a placement claim needs a low oblique.**
- **SHOT-29**, **Before pinning a golden, measure how much of the frame the subject owns; a subject
  no scripted camera can approach does not earn one.** An AI aircraft's damage ladder differed from
  pristine by 0.043 % of pixels while its fireball three seconds later differed by 25.7 %.
- **SHOT-32**, **A shot of an animated screen proves the frame it drew, never that the screen keeps
  drawing; check an animation by driving frames.** Timed briefing shots at five instants all read
  correctly while driving the live menu found 5193 stale frames of 5400.
- **SHOT-36**, **Pin how many lines a wrapped block takes at the authored 1:1 fit, not at a
  capture's resolution; wrapping is a font metric that moves with the window.** The same objective
  row wrapped to one line at 800x600 and two at 1280x720 in one build.
- **SHOT-37**, **Measure a drawn icon's own nose in its bitmap before blaming the rotation that
  turns it.** The pause chart's `singledev` is drawn along the 45-degree diagonal, which a
  per-degree mirror-symmetry scan of its alpha finds exactly.
- **SHOT-38**, **Find the pixels a population touches by rendering one pose twice with its texture
  overridden to two colours and differencing; one `--tex-override` frame undercounts blending.** At
  C1's cloud pose the fade change moved 431,238 px, all but 20 inside the 515,597 px mask.
- **SHOT-39**, **Measure a filled bar against footage by the one edge that moves, not by the extent
  you can see, which picks up the end caps and the gutter's shadow.** BL-842's Instant Action thumb
  read 173 of 230 track pixels by extent and the exact 170 by its transition.
- **SHOT-40**, **Step the pose by a rotation to test a screen-space effect for flicker, and
  difference the pair against a pair rendered with the effect off.** A 1.7 degree yaw flipped 3,641
  water texels at C1's lake where 3 m of forward travel flipped 608.
- **SHOT-41**, **Before adding a colour transform for a tint, measure the overlay's own alpha and
  read the layout's Z.** The scrapbook zoom's warm cast is its `DZ_ZOOMgrimeframe` mount (alpha
  0.137 to 0.894, Z 200 over the print), and no colour matrix exists anywhere.
- **SHOT-42**, **Measure a full-screen dither as the mean 2x2 alternation `|a-b-c+d|` over
  luminance, and bisect it by differencing the frame against each pass switched off.** At C1's
  waterfall under Enhanced the soft-shadow difference scored 2.17 and SSAO, SSR and glow 0.10 or less.
- **SHOT-43**, **Read shadow noise on an aircraft against the same pose with the sun's shadow
  off, never off the frame alone.** The airframe skins carry rivet rows that read as the same dot
  grid, so a frame-only figure scores the texture (`AirframeShadowLookupOffset`).

- **SHOT-44**, **A stereo viewport is a two-layer texture and draws no canvas layers, so a plain
  `GetImage()` of it is the left eye alone and a missing HUD in it is not a HUD defect; read each
  eye by its layer (`RenderingServer.Texture2DLayerGet`).** Godot skips 2D on any viewport whose view
  count is above one (`can_draw_2d` in its `renderer_viewport.cpp`), so under `--xr-sim` the HUD,
  the boards and the cockpit pass are absent from both eyes.

- **SHOT-45**, **Judge where a stereo eye draws the cockpit against the flat `--no-cockpit-pass`
  frame taken from that eye's own point, not against another stereo build.** On the empty stage
  `--xr-sim=eye=1280x720,fov=96.4268,ipd=20,head=0.01/0/0,shot=left,pitch=-4.70001` puts the left eye
  on the flat camera; the stereo interior registered within 0.04 px of the flat frame in every
  region at 0 and 20 km, while the earlier stereo path differed by 0.1 px through its depth bias.

- **SHOT-46**, **A canvas layer composites after the tonemap and a world quad before it, so compare a
  canvas effect with its world twin in the Original look, where the tonemap is linear; under
  Enhanced the A/B measures AgX, not the port.** The world flare matched the screen rig on C3 to 0.22 %
  of pixels over 2 levels in Original, while under Enhanced its rings read 4 to 9 levels over the sky
  against the screen rig's 6 to 21.
- **SHOT-47**, **A `Color` set on a 3D material's uniform reaches the shader already linear, whatever
  the uniform's hint, so check a drawn colour in the capture, never in the parameter it was handed.**
  The theater surround's sRGB 0.075 floor, passed as a `Color` and linearised again in its shader,
  drew 2 of 255; passed as a `Vector3` it drew 19.
- **SHOT-48**, **Measure a small marker's stereo disparity by a least-squares subpixel shift of its
  crop between the eyes, never by thresholded centroids.** On one capture the pipper's centroids
  read 0.18 px of disparity and a 0.43 px vertical offset; the shift search read 0.76 px and 0.00,
  matching its 463 m range, and gave the box the aircraft's own 2.5 px.

## GOLD, golden images

- **GOLD-1**, **Update moved hashes with the visual change, and explain each moved shot in the
  commit message.** `manifest.json`'s `exercises` field says what a shot covers *today*; do not
  append a re-pin note to it.
- **GOLD-2**, **A golden is a tripwire, not a diagnosis.**
- **GOLD-3**, **For render-path changes, sweep every golden and inspect the largest movers.**
- **GOLD-4**, **Reproduce a golden with its OWN `frame` count, or the A/B is meaningless.** An
  ad-hoc `RunProbe.ps1` repro that omits it renders a different sim frame.
- **GOLD-5**, **Which goldens move is itself evidence; check the pattern, not just the count.** A
  per-plane chase distance moved all four flown-aircraft shots and none of the nine without one.
- **GOLD-6**, **A pure TIMING change moves pixels, and a particle shot amplifies it without limit;
  judge one by what the moved shots ARE, never by the magnitude.** A 1/60 s event shift moved
  `c1-crash` by 79.7 % of its pixels and left 7 of 8 chapter captures bit-identical.
- **GOLD-8**, **When an earlier item deliberately left goldens un-repinned, a later item's "moved"
  list is about BOTH changes; recover the current item's own movers by A/B-ing hashes against a
  temporarily reverted build.**
- **GOLD-9**, **"13/13 hash-identical" proves nothing until `git diff` shows
  `analysis/goldens/manifest.json` unmodified in the working tree.** A stray `-RegenGoldens`
  re-baselines the tripwire onto the build under test.
- **GOLD-10**, **Hash the raw pixel buffer, never the saved PNG.** Encoded bytes differ between
  two byte-identical images, so a PNG hash reports encoder state.
- **GOLD-13**, **Every pinned shot is a settled frame, so a defect that lives on one transition
  frame is invisible to the whole set; settle it from the frame order in code and from a suite that
  reads the state across it.**
- **GOLD-14**, **A blend-ORDER change is invisible wherever the overlapping quads share a colour
  and an alpha, so judge one by the shots that CAN move and never by a count of unmoved ones.**
- **GOLD-15**, **Every shot renders at `project.godot`'s pinned viewport, so a rule that depends on
  the window's ratio is only ever photographed at one of them; sweep the ratio in a suite instead.**
- **GOLD-16**, **A new degree of freedom on a pinned path keeps every golden only if its neutral
  value is the EXACT identity, which constrains how the arithmetic is WRITTEN and not just what it
  computes.** A head rotation kept 18 shots byte-identical because `new Basis(axis, 0)` is exactly
  the identity and the existing term order survived.
- **GOLD-17**, **A shot whose command line names machine-local user state pins whether that state
  is present; name the assumption in its `exercises` field.** `campaign-4p-grid`'s
  `--campaign=csvm-golden:6` names a `user://Profiles/` profile no checkout carries.
- **GOLD-18**, **A golden that moves after a shader SOURCE change is not evidence of a visual
  effect; force the value to an extreme and see whether the arm owns pixels in that framing.** A mip
  bias rerouted through a shared include moved `c5-city-night` 1 pixel; +4 on those arms moved none.
- **GOLD-19**, **A decoration that fades out inside its `far_fade_range` is invisible to every shot
  flown above that range; move it in a suite and photograph it from street level.** Raising C5's
  lamp glow 4.75 m left all 19 shots hash-identical.

- **GOLD-20**, **Two blended surfaces of one mesh instance at one render priority draw in an order
  taken from the whole scene's cull output; before blaming a change for a small move, check whether
  the moved pixels sit on such a pair.** Seven invisible boxes parked 100 km below C1 moved
  `c1-rocket-hit-enhanced` by 3 pixels at the radio tower's two alpha sheets, as
  `threading/worker_pool/max_threads=2` does (`analysis/goldens/README.md`).

- **GOLD-21**, **A stereo golden that moves a few levels where the new code draws nothing is not yet
  the new code's defect; hide the new transparent and re-run before reading the move.** The HUD
  panel's quad, every fragment discarded in Cockpit view, moved `c1-cockpit-xr-sim-aft` by at most 4
  levels on the collimated reticle's lines alone, and hiding the quad restored the old hash. The
  cause was a sort tie the renderer broke differently with one more transparent in the frame
  (`docs/org/cameraViews.md`, "Draw order"), which the reticle's own sorting offset now settles.

## DET, determinism and randomness

- **DET-2**, **Disable live input during scripted runs.**
- **DET-6**, **Scripted probes imply `--det`; use `--no-det` for realtime behaviour.**
- **DET-7**, **Deterministic results must depend only on committed inputs.**
- **DET-8**, **`--det` ignores `config.json`; use committed or CLI inputs.**
- **DET-9**, **Keep pure baselines free of clocks, absolute paths, and machine state.**
- **DET-10**, **Fixed-step captures cannot reveal realtime cadence artifacts.**
- **DET-11**, **A rate decoded from video of the original is quoted in SIM seconds (k = 1.390);
  implementing the wall figure runs it 39 % fast. It applies to a measurement, never to a rate the
  binary itself denominates on a system-clock dt, where converting is the error.** The chase
  camera's relaxation carried a converted 0.65 against an authored 1.0.
- **DET-12**, **An angle measured off footage cannot confirm a decode; at best it ranks two
  readings, and it will happily rank a third one you have not thought of.** `CAP-16`'s wing-panel
  strip was recorded as confirming a rotation the binary says never happens.
- **DET-14**, **A golden sweep cannot see a `--det` drop fail while a second guard stands in front
  of it, so test the drop where it is written and not by its pixels.**
- **DET-15**, **A rate denominated in real seconds runs at the render rate over 60 under `--det`, so
  a scripted capture pins its shape and never its pacing; judge pacing in a `--no-det` run.**
- **DET-16**, **Give work a unit test blocks on a thread of its own, never `Task.Run`: on xUnit's
  pool threads the item can sit in the blocked thread's local queue past any bound.** The extraction
  worker's cancel test failed 4 of 13 full unit passes with the item never started after 10 s.
- **DET-17**, **A lossy-loopback suite waits on every condition its checks read, since one seeded
  loss draw serves every send on the mesh and one more message anywhere reshuffles which words
  land together.**

## PERF, performance

- **PERF-1**, **`script_ms` and `physics_ms` are the WORST single pass of the last wall second,
  neither a per-frame cost nor a mean; read `proc_ms` and `phys_tick_ms` for the measured pass.**
  Over a flown C2/M02 `physics_ms` ran a 16.96 ms median against `phys_tick_ms`'s 1.81 ms.
- **PERF-2**, **Capped metrics are floors, not costs.**
- **PERF-3**, **Split broad timers before choosing what to optimize.**
- **PERF-5**, **Ignore differences below measured noise and an absolute floor.**
- **PERF-7**, **Compare startup timings under identical cache conditions.**
- **PERF-8**, **Use the engine startup report, not whole-process time.**
- **PERF-9**, **Use two unchanged pairs for noise, then measure A/B back to back.**
- **PERF-11**, **Use `--no-vsync` and metrics valid for the clock mode.**
- **PERF-12**, **A frame ordinal does not convert to wall time at an assumed refresh rate.** The
  dev machine refreshes at 120 Hz, so a frame chosen for a 60 Hz cap lands at half the time.
- **PERF-13**, **A hitch count only compares across runs in the same vsync mode; per-frame cost
  transfers, frequency does not.**
- **PERF-15**, **A flat-leaf sampler's "negligible" cost only holds outside per-particle loops; a
  scope placed inside one becomes the thing it measures.**
- **PERF-16**, **`StartupProfile`'s `boot` is engine start to build start, so on a launchscreen
  rebuild it holds the menu's idle time, and its `rest` is `build − Σ(phases)`, which on a probe run
  holds whatever the probe did before the build closed; neither is pure engine overhead.**
- **PERF-19**, **A GC capture that ends inside the first minute of a session measures the world
  build settling, not the steady state; let the session settle, and say which regime a number comes
  from.**
- **PERF-20**, **A .NET GC pause here is set by how many FINALIZABLE objects died, not by bytes
  promoted; look for the Godot wrappers a call MAKES per frame and judge the fix on `[perf] gc`'s
  `fin_per_s`, which reproduces to a tenth of a percent, not on its pause, which reproduces to a
  few.** Halving allocation stretched the collection interval and the pause together.
- **PERF-22**, **A memo whose entries are a pure function of their key belongs to the process, not
  to the builder instance; split fresh-key work from repeat-key work before optimising anything
  else.** Of a ~300 ms spawn frame, 190 ms sat in the assignments whose `Shader` was fresh.
- **PERF-23**, **A per-object diagnostic on a shared periodic boundary is a BURST, not a spread
  load, so ask the log filter at the call site before the values are formatted.** One ungated
  once-a-sim-second print cost 17.0 ms of an 18.3 ms tick with nine aircraft alive.
- **PERF-24**, **When a frame's cost is in neither physics, the render terms nor any named script
  scope, count the nodes ASKING for `_Process` before looking at what the callbacks do.** 3,814
  `Puffer` nodes cost 9.4 ms of a 12.4 ms frame while their bodies summed to 1.06 ms.
- **PERF-25**, **Deferring a block off a hitching frame moves its cost rather than removing it, and
  a block sized by its data must be split along that data's grain; name the phase with no seam of
  its own first.**
- **PERF-26**, **A process-wide "nothing to do" guard is not a fast path in a real session; read
  its state at the moment you measure, not at process start.** A no-fade baseline read 0 steps run
  alone and 44 once an earlier suite in the same process had freed a world.
- **PERF-28**, **An exact-zero allocation claim requires EVERY one of several windows to read zero,
  each opened on an emptied allocation context (PERF-29), with a negative control.** A rule that
  accepts one clean window of several passes an allocator that charges intermittently.
- **PERF-29**, **`GC.GetAllocatedBytesForCurrentThread` charges a thread for what its retired
  allocation context still held; empty it with a forced collection before a window that must read
  zero.** The net-message fuzz read a zero-allocating reader as 3,992 bytes once in about 30 passes.
- **PERF-30**, **A unit test asserts a wall-clock FLOOR, or a figure off a clock it advances
  itself, never a millisecond ceiling, which reads the scheduler on a loaded machine.** A six-walk
  mean required under 20 ms read up to 51.3 ms in three of 55 runs beside concurrent builds.
- **PERF-31**, **Report the worst frame of the whole window beside the frame under test; a
  threshold on one named frame is met by moving the block onto a neighbour (PERF-25).** An AI wave
  launch measured on its own frame read as fixed once the assembly ran a second early.
- **PERF-32**, **A "quiet frame" test beside a deferral pump reads the queue's pending count BEFORE
  the pump runs; read after, it says quiet on the frame that just carried an item.** Reading after
  left the wave-airframe refill's worst idle frame at 44.4 ms where reading first gives 24.9 ms.
- **PERF-33**, **Count gen-0 collections per measured frame beside its milliseconds, or a
  collection that lands on one frame of the window is read as the cost of whatever that frame was
  carrying.** The one wave-launch frame over 40 ms is the one with a collection; without it the
  launch costs 18 ms.
- **PERF-34**, **A GC pause lands in whichever sub-scope of a bracket is open, so read
  `GC.CollectionCount` and `GC.GetTotalPauseDuration` across a span before naming its term.** A
  split CM11 physics step put its 33 to 36 ms in a different term each run, each with `gc=1`.
- **PERF-35**, **A console-tier log line costs about 1.9 ms on the frame path: count the lines one
  event emits, time the work without them, and demote a frame-path report to `Debug`.** Nine console
  lines were 22 to 28 ms of a 29 ms CM11 part destruction whose stage starts cost 0.7 to 6.7 ms.
- **PERF-36**, **An in-engine suite that bars a per-frame cost reads the stepping thread's CPU time
  (`QueryThreadCycleTime`), never the wall clock, and keeps the wall worst in its report.** Beside
  heavy shards `ai-wave-launch-hitch` read launch medians up to 108.5 ms wall and 14 ms on-thread.
- **PERF-37**, **A large `physics_ms` in a session that is not parent-driven is the game's own C#
  step inside `GameSession._PhysicsProcess` until a split shows the physics server's step.** A CM20
  gasbag loss read 205 and 424 ms of `physics_ms` that was animation-library indexing.
- **PERF-38**, **A scripted `--campaign=` run spends its first minute in the mission's intro
  cutscene, so read `ai_planes` before crediting a window to the mission: 0 means the film, not
  the fight.** CM24 under `--det` read 8.5 ms frames for 30 sim seconds of intro, and 17 to 23 ms
  once the film was skipped and 22 AI planes flew.
- **PERF-39**, **`gpu_ms` and `render_cpu_ms` measure the root viewport alone, so a splitscreen
  frame's cost is every pane's measured time summed; a `frame_ms` that stays flat while that sum
  moves says the frame is CPU-bound.** Four-pane CM24 held 31.7 ms frames while the panes' GPU sum
  ran from 11.3 to 19.5 ms across shadow settings.
- **PERF-40**, **`script_ms` (`TIME_PROCESS`) holds the draw and its present; split a frame with
  `phys_engine_ms`, `defer_ms`, `draw_ms`, `idle_ms`, `proc_ms` and ticks × `phys_tick_ms`, which sum
  to `frame_ms`.** On the Deck in CM24 `script_ms` read 20 to 60 ms beside a 4.1 ms `proc_ms`.
- **PERF-41**, **Count a `dotnet-trace` capture's samples from the `.nettrace`, never read its
  speedscope conversion as a timeline: the main thread is sampled only while it runs managed code,
  and the conversion stretches each sample to the next, charging native time to the managed stack
  before it.** The speedscope view of a CM24 trace put managed callbacks at 100 % of the main
  thread; the samples themselves covered 28 % of its wall time.
- **PERF-42**, **Judge a Deck target on the Deck: engine-native terms (the transform flush, the
  physics step, the draw) grow far more from the author's machine to the Deck than managed phases
  do, so a desktop profile ranks the Deck's costs wrongly.** In CM24 `phys_engine_ms` grew 5 to 7
  times and `defer_ms` about 3 times, while the AI and animation phases grew 1.2 to 1.4 times.
- **PERF-43**, **Under the separate render thread a `RenderingServer` getter in the frame waits for
  the previous draw and reads as `proc_ms` or `phys_tick_ms`; `draw_ms` is only the hand-off and the
  frame's own render wait is `defer_ms`.** A Godot debug build prints `causing RenderingServer
  synchronizations on every frame` for each such call.
- **PERF-44**, **Split a draw count by viewport and by pass (`ViewportGetRenderInfo`, visible and
  shadow) before crediting it to a source: the frame's total cannot show a viewport nobody sees.**
  Four-pane CM24's 20,500 draws held about 6,400 from the root viewport, which rendered the whole
  world at the window's size behind the opaque pane backdrop.
- **PERF-45**, **A process's first TAA frame stalls for every Shader alive, not for what it draws,
  because Godot builds the advanced shader group for all of them; count live shaders with
  `--debug-shaders` and read the stall as Godot's.** A first switch to Enhanced on CM24 stalled
  10.2 s over 217 shaders.
- **PERF-46**, **Under the separate render thread a per-instance `MultiMesh` getter is a synchronous
  round trip, so a loop of them over a large MultiMesh costs about a microsecond each in waits. Keep
  the state you need to read in a mirror of your own, and fill a new MultiMesh with one `Buffer`
  write.** Reading C5's 199,685 clutter placements back took 290 to 440 ms of every graphics switch.
- **PERF-47**, **Count each pane's spyglass disc as a viewport of its own, off the `[perf] spyglass`
  line: it renders the whole world through the pane's cull mask on every frame a target is off
  screen.** In four-pane C5 flight the four discs drew 5,400 of 11,700 draws.
- **PERF-48**, **Compare four-pane frame times only within one interleaved batch: the same build's
  `frame_ms` on the author's machine moves by more than a change's effect between batches an hour
  apart.** C5 four-pane with the merge off read 14.8 and 15.1 ms in one batch and 13.3 and 13.4 ms
  in a later one, against a merge effect of 0.4 to 1.8 ms.
- **PERF-49**, **On Godot 4.7.0 and 4.7.1 a soft sun shadow's cost depends on where the camera stands
  in the world, so a penumbra cost measured at one pose does not transfer to another; time it across
  positions along the sun's direction, with `--no-soft-shadows` as the control.** The PCSS search
  radius is `(range_pos - range_begin) * tan(angle)`, where the shader's `range_pos` is view-space and
  `range_begin` the cascade's world-space near plane (`renderer_scene_cull.cpp`), so the radius grows
  with the camera's world position projected on the sun; 4.7.2 subtracts it (godotengine/godot#120774).
  A C1 cockpit under the stand-in at 2112x2304 per eye read 5.5 ms at x=0, 9.8 at -6144, 14.9 at
  -12000 and 19.2 at -20000 with z held, 9.8 to 10.2 ms with x held and z swept, and 3.5 ms hard at
  every one. The flat cockpit pass draws its interior near the origin, so flat play pays far less:
  0.64, 0.72 and 0.99 ms at x=0, -6144 and -20000 at 1280x720 against 0.52 ms hard.
- **PERF-50**, **`--perf`'s spyglass census asks each live disc for its draw counts, which on the
  editor binary prints Godot's per-frame synchronisation warning with a C# backtrace, so a window
  with a disc up reads about 12 ms of `proc_ms` that is the instrument's console write.** A C1
  dogfight against six attacking Furies read `proc_ms` 14.6 to 16.0 ms flat and stereo alike, with
  every named site summing to about 2 ms and about 2,900 lines of warning and backtrace in the two windows; without the
  attack, so with no disc up, the same pose read 2.1 ms. Read `gpu_ms` there, and a CPU cost only
  from a window whose `[perf] spyglass` line is absent (PERF-35, PERF-43).
- **PERF-51**, **Under `--xr-sim` the `gpu_ms` term is the headset's stereo render alone: a playing
  cutscene's two picture viewports and the spyglass disc are viewports of their own and are not in
  it (PERF-39).** At `eye=2112x2304,fov=96` the stand-in read 11.06 ms on the C1 cockpit with the
  author's options against 10.8 ms mean at the headset at the same eye size, and 19.9 ms at
  3072x3264 against 20.4 to 23.8 ms there: close enough to rank settings, never a substitute for the
  headset's own `xr frames` line, which also carries the runtime's compositing. The cutscene pair
  added 1.3 to 1.6 ms at 1600x1200 per eye.

## LOG, logs, error censuses, and exit codes

- **LOG-1**, **An empty report may mean the mode did not build the feature.**
- **LOG-2**, **State the time, count, and lifecycle window before concluding from absence.**
- **LOG-5**, **Report caps and truncation; never infer absence from a shortened list.**
- **LOG-8**, **Run shader checks windowed; `--headless` skips the render path.**
- **LOG-12**, **Automated instruments must return their verdict in the exit code.**
- **LOG-13**, **Do not overlap engine probes.** `RunTests.ps1`'s own shards isolate their logs and
  scratch; a suite whose store sits outside `.scratch/`, such as a `user://` profile, still does not.
- **LOG-16**, **A census printed at the end of setup cannot report a runtime miss.**
- **LOG-17**, **In a worktree, `RunTests.ps1` exits 0 having run only the units; set
  `$env:CSVM_DATA_ROOT` and confirm the printed suite and golden counts are non-zero.**
- **LOG-18**, **`--debug-anim`'s motion line prints a GLOBAL position and a LOCAL rotation, so its
  `rot` is world attitude only for a placed template root.**
- **LOG-19**, **`RunProbe.ps1`'s hidden desktop is shared by every worktree on the machine, so a
  sibling agent's probe finishing mid-run kills your viewport; count the Godot processes naming
  another worktree before believing an error census.** The signature is a repeating per-frame
  `NullReferenceException` and two `viewport is null` lines.
- **LOG-20**, **Every tree of this project shares one `user://`, so a probe reads and writes the
  real saved profiles unless it names its own store; pass `--profiles=` with an absolute path under
  `.scratch/` alongside every `--campaign=`.**
- **LOG-22**, **A `*.godot.log` mirror is not that run's log: exclude it when sweeping
  `.scratch/logs/` for what a run did or did not print.** Every quit copies Godot's shared log, so
  one deliberate probe was replayed by every later mirror.
- **LOG-23**, **`Log` formats invariantly only the holes its call interpolates; give a number
  formatted OUTSIDE a `Log.*` call `Log.Format` or `FormattableString.Invariant`.** A collider
  census read `3,8×3,0×5,6 m` on a German machine, which no suite sees under the invariant culture.
- **LOG-24**, **Take a socket suite's port from `Testing/SuitePorts.cs`: beside a sibling shard, a
  walk past a busy port still leaves the bind's engine `ERROR` line and fails the run.**
- **LOG-25**, **A launch that compiles a different set of shader variants must not write the shader
  cache the other launches read: Godot 4.7 misreads a cache file holding a variant the reader has
  disabled, and the `br != variant_size` line it prints fails the engine stage's error screen.**
  One `--xr-sim` launch with the cache on made the next flat launch print it for `VrsShaderRD`.
- **LOG-26**, **Keep a material on a Shader whose text is rewritten in place until Godot recompiles
  it: Godot 4.7 recompiles a rewritten shader only when a material on it updates, and enabling the
  advanced group first (a process's first TAA draw, PERF-45) marks it built from the old text.**
  Every later material on it then prints `Uniform buffer supplied (binding: 0) size (128) is smaller
  than size of shader uniform: (144)`. Which rewritten shaders had lost their last material varied
  from run to run, so the same ordered suites failed 1 to 3 of 6 concurrent processes.
- **LOG-27**, **A process that crashes after its report or shot (`0xC0000005`, `0xC000001D`,
  `FATAL ... script_bindings`, `!rc_owner`) died of a .NET finalizer racing Godot's teardown, which
  `Launcher.SettleFinalizers` prevents; a new exit path must run it too.** Under a forced race a
  one-suite run crashed 12 times in 16 without the settle and 0 with it.

## WORLD, world data and runtime traps

- **WORLD-8**, **Resolve objects by source identity, not normalized node names.**
- **WORLD-9**, **Verify that the selected mode builds the product being measured.**
- **WORLD-10**, **Report opposing transitions separately; a net can hide both.** A death both
  disables the healthy collider and enables wreck colliders, so count OFF and ON separately.
- **WORLD-11**, **Match sampling time to lifecycle.**
- **WORLD-12**, **A started definition is not proof of output; verify its runtime product.**
- **WORLD-15**, **Establish each subtree’s coordinate frame before applying transforms.**
- **WORLD-20**, **For rare classes, census first and aim at named geometry.**
- **WORLD-21**, **Route equivalent lookups through one resolver.**
- **WORLD-22**, **Use explicit subsystem state when hosts are hidden by design.**
- **WORLD-23**, **Range-test decoded fields and corroborate their units.**
- **WORLD-24**, **Read authored range and condition gates before placing a probe.**
- **WORLD-25**, **A registry total counts bindings, not coverage: a larger census can mean one
  definition claimed objects it does not describe.**
- **WORLD-26**, **Anchor an effect to the object it decorates, not to a parameter that merely
  describes it.** C3 authors a sun pitch of −25 while its `sun` node sits 34.4° above the horizon.
- **WORLD-27**, **`--play-anim` proves a definition RUNS; it says nothing about whether the game
  ever reaches it, so drive the real entry point before concluding the definition is at fault.**
- **WORLD-28**, **A suite world built without collision, or with no `ContactMask` wired, poses
  every untimed `OBJECT_MOTION` at rest and can play a whole breakup while saying nothing about
  where any piece comes to rest; a suite that must see a contact wires both.** A lifeboat drop
  read 0.0 m fallen without a mask and 6.2 m with one.
- **WORLD-29**, **A term you meant to redirect can leave instead, and the frame looks like a win
  either way; prove the new source arrives with a control colour.** A background colour set as its
  own reflected light gave Godot no radiance map, and the vanished specular read as a success.
- **WORLD-32**, **A Godot property that accepts a write is not a property the renderer reads:
  prove a lighting knob is live by driving it to an extreme and watching the goldens move.** Under
  a sky-sourced ambient, `AmbientLightEnergy` 0.9 to 0.0 moved no shot.
- **WORLD-33**, **An upward ray reports open air under a one-sided collider, so read a column
  DOWNWARD from above instead.** A downward ray answers in 1,376 of 1,376 partition cells and an
  upward one in 24.
- **WORLD-36**, **Every vehicle a mission never placed stands at the world origin, one hull inside
  another, so scope a ray's answer to the subtree you asked about.**
- **WORLD-38**, **A boot-script setting counts only if a retail run reaches the script that sets
  it.** `load.gw` is the data-compile path and never executes at retail; `adjust.gw` always does.
- **WORLD-40**, **Two numberings of one thing coincide on some subjects, so a readout keyed on the
  wrong one passes every pin taken where they agree; pin on a subject where they disagree.** CM05's
  objective priorities and `OBJECTIVEn` numbers differ while CM01's agree, which is why its pin held.
- **WORLD-41**, **Give a new per-instance draw its own `Rng` subsystem; a draw on a shared stream
  reseeds every later consumer of it in the process.** One extra `Rng.Clouds` draw moved C5's
  pinned sprite count from 16,170 to 16,185 when a suite built C1's field first.
- **WORLD-42**, **A D3D render state the original sets once is one global here that every arm
  reads; count the arms from the shader generators.** `MipBias` sits behind the single
  `csky_sample_albedo`, and `chapter-census` fails any mip-mapped sampler outside it.
- **WORLD-43**, **A census that walks a built world sees only what the world build makes; an arm
  the session adds afterwards passes it vacuously.** `FogVolumeClutter` is built by `GameSession`,
  so its sampler is asserted in `cloud-field-fade`.
- **WORLD-44**, **Read the per-polygon skip flag before predicting a scatter surface from a mesh's
  polygons.** Every shipped `fvol` volume's walls and floor carry `no_clutter` (`0x800`), so its
  scatter surface is the upward skin alone.
- **WORLD-45**, **Hand a `CollisionShape3D` the EMPTY shape and fill it afterwards; a shape assigned
  already carrying faces reaches the body empty for the rest of the frame.** A crater carve assigned
  that way left the struck terrain node answering nothing while it read 216 faces.
- **WORLD-46**, **Subtract a cut region from each face and re-emit the remainder; a centroid-in-region
  test removes nothing from terrain, whose triangles dwarf any weapon's cut.** A centroid filter left
  a bombed collider at 147 faces with the bowl under solid ground.
- **WORLD-47**, **Read a dark surface's baked vertex colour and its sheet's mean texel before
  suspecting a light; the fullbright world draws their product times `csky_world_light`.** C1's
  refuel-tank debris carries vertex colour 119 on a sheet of mean (63, 61, 64), the whole 37 %.
- **WORLD-48**, **Read the `lighting` flag and the normal array on the actual nodes before
  attributing a directional look to the sun.** Every placed `cloudparent` card in C1, C1B, C1C and
  C4 is `lighting: false` with an empty normal array.
- **WORLD-49**, **A node table that keeps the first claimant of a name must drop the OUTGOING
  subtree, not only freed ones, or two live subtrees resolve to whichever was indexed first.** With
  two humans flying, the zeppelin hookup kept posing the seat staged at bind time.
- **WORLD-50**, **Read a gamez node's `parent_indices` before calling it missing from the built
  world: an empty list is a library root, which the world build never creates and no lookup finds.**
- **WORLD-51**, **A layering census over `nodes.json` counts geometry the session hides; before
  reading "X lies under Y" as what draws, check it against a flat render with the suspect texture
  overridden.**
- **WORLD-52**, **A Godot `Color` set on a shader's `vec4` uniform arrives already linearised,
  with or without a `source_color` hint; test a colour path on the darkest authored value, never on
  a white one.** A second conversion took C5's 25-grey fog-bank tint to about 2 while every white
  cloud deck read normal.

## SHELL, Windows, PowerShell, and processes

- **SHELL-2**, **Identify stray Godot processes by worktree and probe flag.**
- **SHELL-3**, **After bulk rewrites, run Godot as well as the compiler.**
- **SHELL-7**, **On PowerShell 5.1, read BOM-less UTF-8 through an explicit UTF-8 API.**
- **SHELL-10**, **Launch scripted Godot probes through `RunProbe.ps1`.**
- **SHELL-12**, **Give every scripted probe an exit condition, and check the flag you chose actually
  is one.** `--frames=N` terminates a run only alongside `--screenshot`; alone it runs until killed.
- **SHELL-13**, **A scripted run must not steal desktop focus.**
- **SHELL-14**, **Test window-focus handling by alt-tabbing, not minimising.** Minimising delivers
  only the mouse enter/exit pair, never a focus notification.
- **SHELL-15**, **A BOM a PowerShell write left on a source file comes BACK the next time the Edit
  tool touches that file, so strip it as the LAST step and confirm with `git diff`.**
- **SHELL-16**, **An A/B built by swapping file content needs `--no-incremental` or a fresh
  `LastWriteTime`: a build after a byte-identical restore can no-op, and `[IO.File]::Copy` keeps the
  source's timestamp.** A limiter margin table taken with a stale swap agreed on every airframe.
- **SHELL-17**, **A census script must not name its accumulator after one of its own parameters:
  PowerShell variable names are case-insensitive, so `$nodes = @()` under `param([string]$Nodes)`
  writes into the TYPED parameter and turns every later `+=` into string concatenation.**
- **SHELL-18**, **Quote a comma-bearing command-line value in PowerShell, or the unquoted comma
  splits it into an array and the run fails.**
- **SHELL-19**, **Quote a launched argument that carries a space; `-ArgumentList` quotes nothing
  itself.**
- **SHELL-20**, **Under `$ErrorActionPreference = 'Stop'`, redirecting a native command's stderr
  makes its failure terminating, so a probe whose failure is the answer must lift the preference and
  read the exit code instead.**
- **SHELL-21**, **A run that saves its shot and never exits is Godot's worker pool deadlocking at
  exit over a queued low-priority task, which `Launcher._ExitTree` drains; read a "broken" golden's
  own `pixmd5=` line against the manifest before calling it a move.**
- **SHELL-22**, **A kill-on-close job object must hold neither the launching shell nor `dotnet
  build`'s shared MSBuild nodes and compiler server: let build children break away, and make a
  `dotnet test` run's nodes private (`MSBUILDDISABLENODEREUSE=1`).**

## INSTR, building instruments

- **INSTR-3**, **Share derived predicates with production code.**
- **INSTR-5**, **Log resolved outputs as well as lookup inputs.**
- **INSTR-6**, **An able-to-fail control over randomised state must sweep seeds, not pin one.** A
  recorded probe reports 0 misses at seed 1 and 2/1/1/1 at seeds 4/7/9/10.
- **INSTR-7**, **"Not decidable from this data" is a fact about the instrument, not the question:
  when a census comes back uniform, ask what else varies the quantity.**
- **INSTR-8**, **Z-fighting is instability, not appearance: measure it as pixels that SWAP WINNER
  between captures a millimetre apart, never by looking at one frame.**
- **INSTR-9**, **A score normalised per axis cannot judge a question about which axis is which;
  prefer a statistic invariant to the thing you are not testing.** An axis-order census was led by
  a division by a ground ring's 0.0 m thickness.
- **INSTR-10**, **An assertion keyed on a field that is not unique reports on whichever subject it
  reaches first; qualify the key, or the check is about something else.** Puffer names repeat
  across definitions, so a `Census.Any(name)` was answered by a fireball's row.
- **INSTR-11**, **A probe that reports one half of a compound thing reads as a full pass on the half
  it can see, so sample over the window rather than at its end, and ask what is left behind.**
- **INSTR-13**, **An in-engine suite runs inside ONE frame, so the physics space never sees a body
  moved, shown or enabled after it was built (a re-enabled shape only queues its rebuild): call
  `TestContext.SyncPhysics()` (`ForceUpdateTransform()` for one node) between the change and the
  cast.** An `AnimatableBody3D` is worse: the server reads a transform write as a kinematic target
  and no repair commits it.
- **INSTR-14**, **Every automated session check runs on a PARENT-DRIVEN clock, so verify the shared
  step owner rather than either clock adapter alone.** A wave sequencer lived only in that path and
  waves 2 to 4 never arrived at the controls while every scripted check stayed green.
- **INSTR-16**, **A capture taken on the very first rendered frame can beat the first per-frame
  publish, which is not evidence the per-frame effect is broken.**
- **INSTR-19**, **The flight-dump hash agrees across the Godot runtime and the `dotnet test` host
  only because the print is rounded past where they diverge; prefer a same-host A/B, and treat a
  cross-host mismatch as a rounding boundary before a plant change.**
- **INSTR-20**, **A negative test that also steps the mission script can arm the very gate it is
  asserting stays shut; drive only the subsystem under test through the control leg.**
- **INSTR-21**, **An animation that completes instantly satisfies every end-state assertion, so
  assert the episode's DURATION too, and build the world the way the session that runs it does.**
  With `camera1` unbound a drop completed in the frame it started; bound, it runs 4.43 s.
- **INSTR-22**, **An aircraft that sinks below `FlightController`'s under-map backstop is teleported
  to its spawn with no crash flag, so gate a flown leg on an altitude floor.**
- **INSTR-26**, **Model realtime at the clock-adapter boundary, not by giving each consumer a
  private callback again.** Install a Realtime `GameClock`, confirm it is not `ParentDriven`, and
  step `SessionSimulation` with the physics delta.
- **INSTR-27**, **On a realtime leg a stand-in flies, so read the STATE under test, never a presence
  flag that its flying can also answer.** `InPlay` folds `Inert` with `Crashed`, so a stand-in
  flown into the sea reports exactly as a correct hide does.
- **INSTR-28**, **A `--screenshot=` on a Realtime (`--no-det`) clock cannot pick its `--frames=` to
  land on a transient live state; prefer a log line, or drive the state through a suite's own
  `_Process` loop.**
- **INSTR-30**, **A world-coordinate precision effect cannot be measured at heading 0, nor with a
  luminance centroid: fly it, rotate it, take consecutive frames, and keep a control region that
  shares the transform but carries no drive.** A pinned heading-0 nudge read 0.03 to 0.12 px where
  a flown phase correlation read 0.7 px at 10 km.
- **INSTR-31**, **A cache or fixture that hands a suite the WRONG subject is invisible to any
  assertion that holds on both subjects; prove the key with an identity test, never with the
  catalog.** Keying a chapter cache on a file name every chapter shares resolved all eight to C1.
- **INSTR-33**, **A smoothness complaint about a sim-driven object cannot be reproduced under
  `--det`, which every capture flag implies, so read `fps` against `phys_hz` on a `--no-det` run
  first.**
- **INSTR-36**, **A golden that moves on your branch is not yours until you have run it without your
  change, which costs one `git worktree add --detach <merge-commit>` and one golden stage.**
- **INSTR-37**, **`Node3D.Scale` does not read back axis for axis off a basis carrying real
  rotation, so compare sorted magnitudes or the whole basis.**
- **INSTR-38**, **A fault that needs a hash collision fires on a minority of runs, so re-running a
  suite measures the collision rate and not the fault; assert the stale state directly.** A filter
  threw on 1 run in 14 while the same drive left hundreds of stale rows every time.
- **INSTR-39**, **A "did it move" distance is scored by the defect, so it passes hardest on the
  worst behaviour: read the DISPATCH for "did the event fire" and the RESTING POSITION for "did it
  end in the right place".** A 1 m threshold passed four gasbags falling 1,228 m through the sea.
- **INSTR-40**, **A self-test whose rows all call helpers proves nothing about the entry point the
  caller actually uses, and a gate's trigger belongs to the gate, not to each harness that calls
  it: drive the harness's own command text against a fixture carrying a known fault.** Three
  `PreToolUse` copies of a `git commit` trigger skipped `git -C <tree> commit` while all 21
  self-test rows passed.
- **INSTR-42**, **A golden that produces NO image failed differently from one whose hash moved:
  read `broken` as a tooling fault and `moved` as a content change, and look for the `shot pixmd5=`
  line in `.scratch/goldens/<name>.log` first.** Two of 19 shots reported no hash in one four-worker
  pass while both logs carried one.
- **INSTR-45**, **A screenshot proves nothing about audio: read the `sound` log's pairing instead,
  one line per aircraft at build naming what each slot resolved to, then one per cull transition.**
- **INSTR-46**, **An unpacked developer tree cannot reproduce a zip-only asset bug: verify a
  release's asset shape with `--zip-assets`.**
- **INSTR-47**, **A body's net world displacement is zero for as long as something is holding it in
  place, so it cannot answer "is this body still travelling"; read the velocity of the frame it is
  solved in.**
- **INSTR-48**, **A sweep that stops at the FIRST obstruction it finds tests the geometry nearest
  the instrument, not the geometry the report is about; choose the bearing by how much of the
  subject the segment passes through.**
- **INSTR-49**, **A synchronous in-engine suite reads `GameClock.Current.Time` as a CONSTANT, so
  anything behind a time-expiring cache runs its first verdict for the whole leg; install a clock
  and call `BeginFrame(dt)` per step, then assert the elapsed game time.**
- **INSTR-52**, **Reading the two writers you expected does not prove a property is never
  written: grep every assignment of the member before filing "it is never copied".**
- **INSTR-53**, **Stubbing out a MEMOISED lookup does not turn a fix off: the first call still
  fills the cache and every later one reads the answer back. Stub the cache fill as well, or the
  red check passes and pins nothing.**
- **INSTR-55**, **Time a lease from the event that renewed it, never from wherever the previous
  check ended: a step count started mid-lease measures the remainder and reads a working lease as an
  expired one.**
- **INSTR-56**, **One `GD.Print` on a code path makes that path untestable in `dotnet test`, and it
  fails as a crashed HOST rather than as a failing test: read the `.trx` before blaming the
  harness.** A breadcrumb goes through `Log` at DEBUG, which makes no engine call under the default
  threshold.
- **INSTR-59**, **A positional-audio check must read the node the CULL measures from, not only
  whether the emitter is playing: an audible verdict passes while the two are different nodes, as
  long as the wrong one happens to stand near the listener.**
- **INSTR-60**, **Walk a menu screen to the row's own text or key, never by a counted number of
  cursor steps: a row added above it silently redirects every later press.**
- **INSTR-63**, **Assert WHICH INSTANCE a built subsystem reads (`ReferenceEquals` against the
  session's, with a null-object control): a seam defaulting to a null object hides missing wiring.**
  Every world emitter read the null ambience while every puffer suite passed against a fake renderer.
- **INSTR-64**, **Verify a changed rebinding DEFAULT on a fresh profile and test the default table
  itself: a saved keymap lists every action of its context and loads whole.** The keymap store
  versions its schema, not its action list.
- **INSTR-65**, **Enter a state through its scripted seam too, and assert there that a flag the
  state's ENTRY sets is clear; a suite reaching the state only through the cause under test cannot
  see it.** Every evade arrived by damage, so a mode transition stamping the evade flag went unseen.
- **INSTR-66**, **Scope a modifier rule to the binding table that names the modified key, never the
  whole session, and assert it where a map contests the key and where none does.** A global
  held-Shift rule would also silence the free camera's Shift boost and every menu key.
- **INSTR-68**, **A suite passing on a space that is missing colliders is passing on geometry the
  game does not have, so re-read every verdict the sync changes rather than only the red one.**
  The armed zeppelin leg engaged only while the hull's own shapes were absent.
- **INSTR-69**, **An input read off a device the test host lacks needs a pinned seam beside the
  live read, or the mechanism is reachable only by hand.** `SeatMouse.StickForTest` supplies the
  cursor offset every headless suite reads as zero.
- **INSTR-71**, **Install a wall-clock animation's draw from the interactive path alone; installed
  from the build, every scripted and golden run inherits the wobble.** The load screen's pump is
  installed by the load board's `_Ready`, so a CLI launch reports into a null ambient.
- **INSTR-72**, **A check taken well past a threshold passes for every value of that threshold;
  straddle it instead, one reading just inside and one just outside.** An AI gun cull read at 4x
  its audible distance passed at 1.0x and 1.1x alike; 1.05x and 1.15x separate them.
- **INSTR-73**, **A line a readout composes is not a line the player reads: assert the control it
  reaches and the layer that control sits on.** The crashed pilot's respawn line passed its unit
  test while the crash camera hid the HUD layer it hangs from.
- **INSTR-74**, **Suppressing a definition's `OBJECT_MOTION` can shorten it, so a suite must not
  read its slot off `AnimStateOf`.** `spinprops`, whose only sustain is a suppressed
  `XYZ_ROTATION`, reads EXECUTED where unsuppressed it would run forever.
- **INSTR-75**, **Name a rig off the format string that prints its result, never off the function
  next to it.** `FUN_00491d90`, taken for the sustained-climb rig by position, feeds the
  `Max turn ... deg/sec` print.
- **INSTR-76**, **Two instruments that share only the mechanism are a real check on each other; two
  that share the rig are one instrument.** The sustained climb flown free at 60 Hz and integrated
  pinned from rest at 100 Hz agree to 0.06 %.
- **INSTR-77**, **Place a remake-only widget on a data-authored screen from the authored rows, not
  a written position, which lands on an authored control in another layout.** The lives box at a
  written Y 260 sat on the mission dropdown in the unit fixture's tighter layout.
- **INSTR-78**, **When the parameter under test also moves the control render, report the control's
  own count beside the instrument's.** Raising water roughness from 0.1 to 0.4 grew the
  reflection-off control from 7,361 to 48,347 texels and biased the flicker count optimistically.
- **INSTR-79**, **Guard a session-wide device mode a run can change on nobody being at the controls
  (`SessionSpec.IsScripted`, `Det`), and assert the mode untouched rather than exercising it.** The
  hidden test desktop is a real display, and a positive-arm suite would warp the user's own cursor.
- **INSTR-80**, **A `--no-det` screenshot near a session start photographs the start cover; shoot it
  under `--det` or past the fade, and read `[ui] start cover:` for how long it held.** A `--no-det
  --frames=3` C1 shot read mean RGB 27/28/31 against 125/126/112 under `--det`.
- **INSTR-81**, **Judge an effect that lives a couple of frames by its gap to the node that spawned
  it, logged when it expires, never by eye in a still.** A muzzle light placed once in world space
  ended 1.65 m astern of its firepoint at 100 m/s, against 0.00 m anchored.
- **INSTR-82**, **A straight formation leg measures a loss but never a recovery, since aircraft on
  one airframe share a top speed; script the leader to turn before reading a re-form time.** A
  leader's nitro burn left its escort 805 m back for as long as it flew straight.
- **INSTR-83**, **A consumer that compares a value between two of its own calls must run on the
  clock that moves the value; driven per rendered frame it is green when scripted and dead at the
  controls.** A per-frame throttle gate at 120 fps over a 60 Hz step read the lever flat every other
  frame (INSTR-14 one level up).
- **INSTR-85**, **Read an effect's placement against its anchor at the same instant: it is placed
  on the interpolated DRAWN pose, so a reading on the simulation step reports one frame of travel.**
  A muzzle light that read 0.00 m under `--det` printed 1.11 m at 158 mph with nothing wrong.
- **INSTR-86**, **Order an anchored effect's draw after every node that writes a drawn pose in its
  callback, and assert the `ProcessPriority` as well as the placement.** `ProjectilePool` drew
  before the flight rigs, so every anchored flash sat a frame astern of the gun.
- **INSTR-87**, **Drive an `AudioStreamPlayer3D`'s level yourself (attenuation `Disabled`,
  `MaxDistance` 0); any other setting adds its own curve, a low-pass and a fade to zero.** Mapping
  `snd_turretgun`'s `RANGE [30, 400]` onto inverse-distance silenced a hull's gun at 400 m.
- **INSTR-88**, **A frame forced inside a synchronous block presents only the commands the server
  already holds, so a repaint there issues its own; read the presented frames back to prove they
  differ.** A load screen's 12 `ForceDraw()` calls wrote 12 byte-identical captures.
- **INSTR-89**, **Read the state a setting writes after the screen that applied it has closed, not
  its apply line; a live-preview screen can restore its opening state over the accept.** The pause
  AUDIO page logged `effects=0` on accept and the bus was back at -6.021 dB a moment later.
- **INSTR-90**, **Moving a control between actions in a default table puts it on BOTH for a saved
  keymap naming only one, so a saved row takes its control off every action the file does not
  name; test the partial file.** A file naming the free-look row alone would have held the free
  look and the rockets on one mouse button.
- **INSTR-91**, **A suite's own `SubViewport` misreads 3D three ways: a `MeshInstance3D` renders at
  the identity until `ForceUpdateTransform`, a `MultiMesh` buffer reaches the GPU a frame late, and
  the readback is `Rgb8`, three bytes a pixel.**
- **INSTR-92**, **A positional voice that is not culled can still be 70 dB down; read the level the
  line prints, never the cull distance beside it.** An AI `snd_30cal` at 398 m of its 413 m cull
  measured -72 dB.
- **INSTR-93**, **A suite that drives `_PhysicsProcess` alone never advances `GameClock.Time`, which
  only `BeginFrame` in `_Process` moves, so every timestamp two harness sessions exchange reads
  zero.** One `_Process(6.0)` on the host moved the guests' `NetClockSlew` target to 6.000 s.
- **INSTR-94**, **Hand a suite that reads a `user://` store a scratch store, and prove nothing reads
  the production one by making its accessor throw for one run.** `menu-original-tracer-core`'s
  sortie-screen aircraft column scrolled only on a profile holding a saved custom plane.
- **INSTR-95**, **Measure a draw-order overpaint against the same pose with the suspect surface
  forced first, never by counting pixels equal to the backdrop: a sub-one alpha paints over an edge
  without erasing it.** A C1 ground strip read 0 erased pixels and 1125 reordered ones.
- **INSTR-96**, **Count leaked nodes as orphans left AFTER the harness flushes queued frees, listed
  through `ClassDB.ClassCallStatic("Node", "get_orphan_node_ids")`, whose C# binding truncates the
  ids; free a test node with `Free()`, since `Dispose()` drops only the wrapper.**
- **INSTR-97**, **A session suite stepping only `_PhysicsProcess` never plays a film out or runs a
  death's staggered `CALL_ANIMATION` chain; advance the world runtime and tick the cutscene beside
  each step.** With both added, CM01's fuel-truck kill went from 1 destroyed pool to 8.
- **INSTR-98**, **Before calling a scripted trace a fault, read the same definition's own timed
  events against it.** C1/M05's attack balloon reaches the sea at 57 s, where its definition calls
  `sm_splash` at the lifeboat.
- **INSTR-99**, **The engine must not reach a RefCounted object whose only owner was a dropped C#
  wrapper between that wrapper's collection and its finalizer, or the finalizer logs `Handle is not
  initialized`; hunt it with `--debug-finalizers`.**
- **INSTR-100**, **Prove an operation frees what it made on those objects' own instance ids, never
  on Godot's global `ObjectCount`, which other suites' finalizers and threads move.** Late in a
  shard, successive finalizer drains freed 308, then 84, then 0 objects.
- **INSTR-101**, **A `--hold=` script flies every seat its launch builds, a bot's included, because
  the scripted input outranks the AI pilot; keep it off any session that reads a bot's flight.**
- **INSTR-102**, **Reproduce a race with Godot's background work with its worker pool held busy;
  neither `RenderingServer.ForceSync()` nor a drawn frame waits for that pool.** An idle pool
  printed no free_rid line where a busy one printed 50 after a `ForceSync` and after a drawn frame.
- **INSTR-103**, **Judge a free's saving over several rounds against a control that keeps the same
  objects, never on the first drop, which returns only part.**
- **INSTR-104**, **Before a suite aims a round at a destructible, ray-test a point whose collider
  resolves to that pool.** On C3's `susp_bridge` only `rope1` resolves to the pool, and rounds aimed
  along the cable killed `bridge_truck01` instead.
- **INSTR-105**, **When no ray reaches a pool whose colliders resolve to it, look for a hidden
  ancestor before calling it a collision defect: a mission's boot script switches whole areas
  off.** C3/M01's `m01.gw` hides C3's guard towers with `WorldPartitionSetActive`.

## SRC, sources and documents

- **SRC-3**, **Use design documents for intent; retail evidence decides shipped details.**
- **SRC-5**, **A field you don't read may be REDUNDANT, not dropped; try to derive it from the
  fields you already read before deciding what it means.** All 51 `*_delta` vectors are exactly
  `(to − from) / run_time` of the sibling channel.
- **SRC-6**, **A test the binary computes is not a rule until you find what reads its result.** The
  take-hit body computes "same team or neutral" into a byte whose only read is an argument the
  callee never touches, so the original applies friendly damage.
- **SRC-7**, **A key the data authors, a name it carries, or a threshold the art separates on is
  not a feature until you find its reader in the executable.** `enemy_skill` exists in no string of
  `crimson.exe`.
- **SRC-8**, **A branch's effect is only half its meaning; the other half is what it is an
  alternative to, so record the jump it skips.** One arm read as a capacity top-up had the entire
  wave teleport as its `else`.
- **SRC-9**, **A negative about a site ("the flag is not tested") needs the control flow around it
  read, not a search for the immediate; read on past the write.** A flag hoisted into a register at
  the loop top read as unfiltered.
- **SRC-12**, **Which body axis a matrix row holds is a claim to re-derive at a point of use, not
  to take from another page; a wrong row sign reverses a ported direction without failing
  anything.** Row 2 of the orientation matrix is minus the nose.
- **SRC-14**, **A census over the shipped files counts records, not live things; resolve the record
  in the world that reads it before saying a mode offers it.** Six `IA1` files flag a radio tower
  target and only C1's world has a node of that name.
- **SRC-16**, **A named rectangle in a dialog primitive says nothing about whether it crops the
  source or clips the output; settle it by aligning a reference still, not by reading the name.**
- **SRC-17**, **Two definition files keyed alike are not copies of each other; compare the bodies
  before reading one screen's content out of the other's file.** Every `LOADING_SCRIPT` is a
  superset of the matching `ESC_SCRIPT` under identical keys.
- **SRC-18**, **A decoded per-SLOT constant stops being decoded once the port feeds that slot from
  a different device; ask which source a threshold was measured on before keeping it.** The
  original's 0.3 mouse yaw gate is its third mouse axis's dead band, which this two-axis port lacks.
- **SRC-19**, **"That file carries no such id" is a claim about a file, so open the file across the
  whole install before routing the binding through somewhere else.** `dzones.zrd`'s
  `objective_numbers` carries the danger-zone objective id once said to be missing.
- **SRC-20**, **Evaluate a decoded compare at the shipped option and data values before saying what
  the player hears; a threshold on a product of scales can be partial or inert at the defaults.**
  The engine voice duck lowers the engines by about 1.9 dB at the defaults, not by half.
- **SRC-21**, **A constant a config reader stores when a key is missing is a fallback, not the value
  in play; open the file the reader loaded before citing it.** The multiplayer score table was read
  as kill +1 and lost hull +100, and the shipped `player.zrd` authors 2 and 10.
- **SRC-22**, **When a reader leaves a global alone on a missing key, the value in play is the
  global's initialised bytes; read them from `.data` before quoting a default.** The rearm radius
  was written up as 624.0, and `0x628f10` holds 625.0, 25 m squared.
- **SRC-23**, **An angle added to a rotation is not the pose; read the vector the rotation turns
  before saying where the result points.** `thirdp_pitch` was written up as the chase camera's
  0.29° elevation, and the vector it swings puts the camera 7.57° above the tail.

## What this project cannot verify itself

The user must verify audio and control feel, multi-controller behaviour, skilled flying, live-input
flows, and fidelity claims requiring the original. Automated checks must state these gaps.

## Known non-deterministic surfaces

Scripted probes are pinned by `--det`; `--no-det` restores live input, clocks, spawn, and
randomness. Expect variation in flight framing, weather, water, particles, random animation,
damage effects, and liveries. Measure a fresh same-build floor for the exact scenario.
A stereo run is pinned only through `--xr-sim`, whose head pose comes from the flag; a headset's
head pose is live input, so a capture from a real session is a sample, never a baseline.

## Known environmental reds

A battery red that matches an entry here comes from the machine, not the change, and has an open
owner. Rerun it alone with the entry's command: red alone, it is the change's until it reproduces
on the unchanged build (METHOD-8); green alone, the landing carries a `Waiver:` line naming that
owner. An entry leaves when its owner closes. A `DEFERRED` result (`memory`, `rebuild`, `quiet`, `gaming mode`) is
not a red: re-run, with `-WaitQuiet` when other sessions' Godots are live (`docs/tooling.md`).

## The standing checklist

- [ ] `.\RunTests.ps1` exits 0; every `SKIP` is understood as not checked.
- [ ] The check can fail and the case exercises the changed mechanism.
- [ ] Inputs, pose, clock mode, binary, and output files are confirmed.
- [ ] Same-build noise and a current baseline were measured where relevant.
- [ ] Complete logs were checked and “pre-existing” failures reproduced on the baseline.
- [ ] Every moved golden is explained and updated in this commit.
- [ ] Relevant modes, chapters, branches, times, and viewpoints were sampled.
- [ ] Anything requiring the user or original game is stated as unverified.
