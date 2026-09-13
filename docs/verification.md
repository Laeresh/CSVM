# Verifying a change in this project

This file contains transferable verification rules. Dated evidence belongs in commit messages,
analysis findings, or git history; a module constraint belongs as a comment on the member it binds,
and format or decode knowledge in `docs/formats/` or `docs/org/`.

Read **METHOD** first, then only the relevant section. Rule IDs are permanent: append new rules
and leave gaps when retiring old ones. Each rule is a bold one- or two-line imperative plus at most
one sentence of measured evidence; everything else belongs in the commit that landed it. A rule
that only holds for one suite, one flag or one module is not a rule: it is a comment on that
member, and it does not go here.

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
  switched off once that code is already deleted.** Turning `applyActive` off in a tree whose old
  root-level visibility rule had gone measured a third state nobody ships.
- **METHOD-30**, **Span a hand-back with the press itself: a modal that consumes a press hands
  input back while that press is still down, so a check that presses only after the hand-back
  cannot see the new owner read its tail.** The click that skipped a campaign film fired the
  plaque under the pointer on its release, and the hand-off's tests pressed after the stop.

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
- **DIAG-19**, **Scripted repros inherit identity defaults (-Z heading, near-origin spawn); a
  pose-dependent symptom needs an off-axis heading AND a far-from-origin placement, and a repro that
  works where the report fails means the poses differ, not that the report is wrong.**
- **DIAG-20**, **An effect that never starts passes every "it ends correctly" check; prove the
  START from the production call path before diagnosing the stop.** A `stop-sequence` suite and two
  cockpit passes all read an unparsed block's absence as a working stop.
- **DIAG-22**, **A suite that asserts over a directive the data may not author fails vacuously;
  loop over the authored candidates, then branch on the count.** Two chapters read as a sound
  defect because a wake cue asserted a one-shot no mission authors.
- **DIAG-23**, **Dispatching a callback directly tests what the code does, never what the codes
  around it undo; play the definition that raises it.** CM02's 967 alone left the suite green while
  the definition's `913`/`914` pair put the hidden aeroplane back 19.25 s later.
- **DIAG-24**, **A fixed sim window shorter than an episode's own completion time reads as an
  unresolved bind, not a slow clock.** A 12 s window read `cones 0/2` on cones that draw at t=13 s.
- **DIAG-26**, **A mission's intro cutscene holds the world: no objective completes, `--pos=` is
  withheld, and `SimHeld` skips every AI, turret and projectile phase while the clock, the frame
  counter and the screenshot advance normally, so a bounded probe reads as dead rather than held.**
  Reach a chapter's geometry through a mission with no intro, or give a `--campaign=` run a minute
  of sim and read its `cutscene:` lines first.

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
- **SHOT-20**, **Locate a saturated feature by the centroid of its plateau, not by `argmax`.** On a
  clipped highlight `argmax` returns wherever the tie breaks, 16 px off the sun's centre in C3.
- **SHOT-21**, **Keep radial probes clear of the frame edge.** A circular sample that runs
  off-frame reads clamped pixels as content and reports a confident zero.
- **SHOT-27**, **The true horizon is a CALIBRATED row, not the sky/terrain boundary: shoot the same
  position level and check the shift is `f·tan(pitch)`.** Comparing fog depth in rows from the top
  of frame hid a 5.7° pitch difference between two frames.
- **SHOT-28**, **A nadir shot cannot tell a painted rooftop from an extruded building, and a global
  instance count cannot tell you where the instances went; a placement claim needs a low oblique.**
  A gate that held two textures at 79 % and 96 % of baseline had emptied the near-field skyline.
- **SHOT-29**, **Before pinning a golden, measure how much of the frame the subject actually owns,
  and re-measure it for each subject; a subject no scripted camera can approach does not earn
  one.** An AI aircraft's whole damage ladder differed from pristine by 0.043 % of pixels while the
  same aircraft's fireball three seconds later differed by 25.7 %.
- **SHOT-32**, **A shot of an animated screen proves the frame it drew, never that the screen keeps
  drawing; check an animation by driving frames.** Timed briefing shots at five instants all read
  correctly while driving the live menu found 5193 stale frames of 5400.

- **SHOT-35**, **An Instant Action wrap-up board is not on screen at the frame the mission ends; a
  shot of it must drive frames past the decoded hold or it catches the live world instead.** The
  ending arms `InstantActionRuntime.WrapupHoldS` (3 s) of running world before the board is
  presented, so a `--screenshot` run whose `--frames` stops inside that hold photographs aeroplanes
  and a falling wreck, and reading the missing board as the board being broken is the error.

- **SHOT-36**, **How many lines a wrapped block takes is a font metric, and it moves with the
  window the shot was taken at: pin it at the authored 1:1 fit, not at a capture's resolution.** A
  board's text is rasterised at `round(size x fit.Scale)` points in a box of `width x fit.Scale`, so
  the same objective row wrapped to one line at 800x600 and two at 1280x720 in the same build, and a
  departure chosen from the shot alone was wrong at the fit the suite measures.

- **SHOT-37**, **Where a drawn icon points is its art's own nose plus the turn applied, so measure
  the bitmap before blaming the conversion.** The pause chart's `singledev` is drawn along the
  45-degree diagonal, which a per-degree mirror-symmetry scan of its alpha gives exactly (0.95
  against 0.83 one degree either side), and a scan plus the layout (propeller disc, wing at 30 % of
  the length, tailplane at the end) says which end of that axis is the nose.

- **SHOT-38**, **Render one pose twice with the same texture overridden to two different colours;
  the pixels that differ are exactly the pixels that population touched.** An ordinary
  `--tex-override` frame only marks where a sprite draws near-opaque, so it undercounts a blended
  population badly. Differencing two colour runs is alpha-aware and gives a per-pixel coverage mask
  to test a change against: at the C1 1,208 m cloud pose the two-colour mask covers 515,597 px of
  the 921,600, the cloud fade change moved 431,238 px, and 20 of those lay outside the mask at one
  LSB (blend rounding), which is what "confined to that population" looks like measured.

- **SHOT-39**, **Measure a filled bar against footage by the one edge that moves, not by the
  extent you can see.** A scrollbar thumb, a gauge fill or a progress bar is art with its own end
  caps sitting in a painted gutter, so an eye reading both ends off a frame picks up the caps and
  the gutter's own shadow and is a few pixels slack at each end. The filled-to-unfilled transition
  is a hard luminance step one column can find exactly. Reading BL-842's Instant Action thumb off
  `CAP-50` both ways: its extent measures as board y 182 to 356, which suggests 173 of the 230-pixel
  track, while the transition alone lands on 356, which is 170 and the exact 14-of-19 share the
  window shows. The slack figure would have pinned a suite one preset off the model that produced
  the picture.

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
  Sorting every particle back-to-front left the single-column sprays bit-identical.
- **GOLD-15**, **Every shot renders at `project.godot`'s pinned viewport, so a rule that depends on
  the window's ratio is only ever photographed at one of them; sweep the ratio in a suite instead.**
- **GOLD-16**, **A new degree of freedom on a pinned path keeps every golden only if its neutral
  value is the EXACT identity, which constrains how the arithmetic is WRITTEN and not just what it
  computes.** A head rotation kept 18 shots byte-identical because `new Basis(axis, 0)` is exactly
  the identity and the existing term order survived.

- **GOLD-17**, **A shot whose command line names machine-local user state pins whether that state
  is present, not the code alone; name the assumption in its `exercises` field.**
  `campaign-4p-grid` launches `--campaign=csvm-golden:6`, a `CampaignProfileStore` name under
  `user://Profiles/` that no checkout carries, so the frame it pins is a campaign launch flying
  with no director, and a machine holding that profile renders a different one.

- **GOLD-18**, **A golden that moves after a shader SOURCE change is not evidence the change had a
  visual effect; force the value to an extreme and see whether the arm owns pixels in that framing.**
  Routing the chapter mip bias through a shared include moved `c5-city-night` by 1 pixel at a channel
  delta of 1. Forcing that bias to +4 through the same function moved 62.6 % of the frame, while
  forcing +4 on the three newly routed arms alone, with the world arm held at its old call, left the
  shot byte-identical. So the 1 pixel is a shader-recompile artifact and the pinned framing
  photographs none of the sprite arms' mip levels, which the re-pin's reason has to say.

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
- **PERF-28**, **An exact-zero allocation claim needs more than one measurement window: take
  several and require that EVERY one reads zero, rather than widening the assertion to a
  tolerance.** A rule that accepts one clean window of several passes an allocator that charges
  intermittently. Report the first charged window's bytes, its gen-0 count, the thread and the
  module id, which a rerun cannot recover, and prove the windows with a negative control. Every
  window opens on an emptied allocation context, or PERF-29's charge lands in one of them.
- **PERF-29**, **`GC.GetAllocatedBytesForCurrentThread` reports granted minus unused, so retiring
  a thread's allocation context charges that thread for what the context still held. Empty it with
  a forced collection before any window that must read zero.** The step is that remainder alone,
  always under 8 KB: with per-thread pads 512 bytes apart it moved 512 bytes in lockstep, and
  28,800 windows opened on an emptied context charged nothing where seven charged without it.
- **PERF-30**, **A unit test asserts a wall-clock FLOOR, or a figure read off a clock it advances
  itself, never a fixed millisecond ceiling: a ceiling reads the scheduler on an oversubscribed
  machine, so raising it moves the threshold rather than removing the flake.** A six-walk mean
  required under 20 ms read 29.9, 34.9 and 51.3 ms in three of 55 unit runs made beside three
  concurrent builds.

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
  real saved profiles; copy a profile under a new name before naming it in `--campaign=`, and
  delete the copy.**
- **LOG-22**, **A `*.godot.log` mirror is not that run's log: exclude it when sweeping
  `.scratch/logs/` for what a run did or did not print.** Every quit copies Godot's shared log, so
  one deliberate probe was replayed by every later mirror.

- **LOG-23**, **`Log` renders only the holes the log call itself interpolates, so a string built
  before the call reaches the file in the machine's own culture.** The plane collider census read
  `3,8×3,0×5,6 m` on a German machine although its `Log.Info` line was invariant, because the
  summary property had already formatted the numbers. A suite never catches this: the test harness
  pins the thread to the invariant culture, so the fault appears only in a real run. Grep for a
  numeric format (`:0.0`, `ToString("0.#")`) OUTSIDE a `Log.*` call, in a summary property, a
  `reason` argument, a list entry or a `StringBuilder.Append`, and give each one `Log.Format` or
  `FormattableString.Invariant`.

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
  describes it.** C3 authors a sun yaw of 135 while its `sun` node sits at yaw 45.
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
  prove a lighting knob is live by driving it to an extreme and watching the goldens move.**
  `AmbientLightEnergy` 0.9 to 0.0 moved no golden; `LightEnergy` 1.6 to 0.5 moved 7.
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

- **WORLD-41**, **A new per-instance draw on a shared `Rng` subsystem stream reseeds every later
  consumer of that stream in the same process, so give it its own subsystem.** One
  `Rng.NewSystemRandom(Rng.Clouds)` taken before the cloud scatter's own loop, drawing nothing the
  placements read, still moved C5's pinned sprite count from 16,170 to 16,185 when a suite built
  C1's field first. The fix is a new `public const string` on `Rng`, not a reordering. The same
  ordering sensitivity is why a suite that builds two chapters' fields calls `Rng.Rewind()` before
  each one: a session builds exactly one field, so the second field in a process is off the pin
  unless the stream is reset.

- **WORLD-42**, **A D3D render state the original sets once covers every draw on the device, so its
  remake twin is ONE global that every arm reads, and an arm sampling around it draws that chapter
  at a level the original never chose.** `MipBias` is `D3DRENDERSTATE_MIPMAPLODBIAS`, one state over
  the world mesh, the camera-facing billboards, the cylindrical facades and the clutter alike; here
  it is `csky_mip_bias` behind the single `csky_sample_albedo` in `shaders/csky_mip_bias.gdshaderinc`,
  and `chapter-census` fails any mip-mapped sampler outside it. Count the arms from the shader
  generators, not from the one the setting was first wired into.

- **WORLD-43**, **A census that walks a built world only covers the arms the world build itself
  makes; one a game session adds afterwards is invisible to it and passes vacuously.**
  `chapter-census` sees no cloud-field shader in any of the eight chapters, because
  `FogVolumeClutter` is built by `GameSession` and not by the world build, so its sampler is
  asserted in `cloud-field-fade`, which builds the field.

- **WORLD-44**, **A mesh's polygon count is not its scatter surface; read the per-polygon skip flag
  before predicting anything from the geometry.** Every shipped `fvol` volume is a closed box or
  prism, so laying the cloud lattice on its faces looked like it would put cards under the deck and
  inside the streets. The census says the walls and the floor carry `no_clutter` (`0x800`) in
  every chapter (C1 45 of 54 polygons, C1C 71 of 175, C5 102 of 148), so the scatter surface is
  the upward skin alone and no shipped face points down or sideways. The flag, not the volume's
  shape, is what makes a slab chapter's field a sheet.

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
- **SHELL-16**, **An incremental `dotnet build` after restoring a file to byte-identical content
  can silently no-op, and `[IO.File]::Copy` preserves the SOURCE file's last-write time, so an A/B
  built by swapping file content needs `--no-incremental` or a fresh `LastWriteTime`.** A limiter
  margin table taken with a stale swap agreed to two decimals on every airframe.
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
  moved, shown or enabled after it was built: aim at bodies where they were created, build a world
  the object already stands in, or call `TestContext.SyncPhysics()` (`ForceUpdateTransform()` for
  one node) between the change and the cast.** An `AnimatableBody3D` is worse: the server reads a
  transform write as a kinematic target and no repair commits it, so a flown aeroplane leaves its
  collider behind.
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
  to its spawn with no crash flag, so gate a flown leg on an altitude floor.** Two such jumps were
  the whole of a 3797 m worst separation on a leg that plateaued at 254 m.
- **INSTR-26**, **Model realtime at the clock-adapter boundary, not by giving each consumer a
  private callback again.** Install a Realtime `GameClock`, confirm it is not `ParentDriven`, and
  step `SessionSimulation` with the physics delta.
- **INSTR-27**, **On a realtime leg a stand-in flies, so read the STATE under test, never a presence
  flag that its flying can also answer.** `InPlay` folds `Inert` with `Crashed`, so a stand-in
  flown into the sea reports exactly as a correct hide does.
- **INSTR-28**, **A `--screenshot=` on a Realtime (`--no-det`) clock cannot pick its `--frames=` to
  land on a transient live state; prefer a log line, or drive the state through a suite's own
  `_Process` loop.** A cutscene handoff landed anywhere from t=3.7 s to t=40.2 s across identical
  launches.
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
  rotation, so compare sorted magnitudes or the whole basis.** An authored `(1, 0.25, 1)` on a
  rotated arm reads back as `(1, 1, 0.25)`.
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
- **INSTR-42**, **A golden that produces NO image failed differently from one whose hash moved: read
  `broken` as a tooling fault and `moved` as a content change, and open the preserved log first.**
- **INSTR-45**, **A screenshot proves nothing about audio: read the `sound` log's pairing instead,
  one line per aircraft at build naming what each slot resolved to, then one per cull transition.**
  The pair is what separates silent past the cull from silent because the definition never resolved.
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
- **INSTR-63**, **A per-session seam that defaults to a null object hides a missing production
  wiring, so assert WHICH INSTANCE a built subsystem reads, by `ReferenceEquals` against the
  session's instance with a control that reads the null object, not only what that instance
  reports.** Every world emitter read the null ambience while every puffer suite passed against a
  fake renderer.
- **INSTR-64**, **A changed rebinding DEFAULT is invisible to any profile that has already saved
  that context, because a saved keymap lists every action of the context and the store loads it
  whole: verify new defaults on a fresh profile, and test the default table itself rather than a
  seat built from disk.** The keymap store versions its schema, not its action list, so a default
  change moves no stored file.
- **INSTR-65**, **A suite that can only reach a state through the one cause it is testing cannot
  see a flag the state ENTRY sets rather than the cause; enter the state through the scripted seam
  too, and assert the flag is still clear there.** Every evade phase arrived by damage, so a mode
  transition that stamped the evade flag stood unseen and swallowed the next steady-hand roll.
- **INSTR-66**, **Scope a modifier rule to the one binding table that names the modified key, never
  to the whole session: a global "a held Shift silences every bare key" also silences the free
  camera's Shift boost and every menu key, which no test of the flight map would catch.** Ask the
  map itself whether it holds that key under a modifier, and assert the silencing both where a map
  contests the key and where none does.
- **INSTR-67**, **Re-enabling a `CollisionShape3D` only QUEUES the shape's broadphase rebuild for
  the next physics step, so the node reads `Disabled=false` and the server still answers nothing;
  a suite that hides or shows anything must sync before it casts.** aagun32's woken mount answered
  0 of its 6 enabled shapes and 6 of 6 after the sync, and one unrelated `BodyTestMotion` anywhere
  in the world repaired all six, which is the server's own pending-shape flush.
- **INSTR-68**, **A suite passing on a space that is missing colliders is passing on geometry the
  game does not have, so re-read every verdict the sync changes rather than only the red one.**
  The armed zeppelin leg had been parking its bait 200 m from a ring, which is inside a 657 m by
  136 m hull, and it engaged only while the hull's own shapes were absent.
- **INSTR-69**, **An input read off a device the test host does not have needs a pinned seam beside
  the live read, or the mechanism is only reachable by hand.** The mouse flight scheme reads an
  absolute cursor offset inside the viewport, which is zero in every headless suite, so
  `FlightController.MouseStickForTest` supplies that offset and the live path stays the only reader
  of the real pointer.
- **INSTR-70**, **A new in-engine suite is not finished when it passes: `analysis/engine-suite-weights.json`
  must name it too, and a unit test fails until it does.** The balancer weighs every registered
  suite, so an unweighted one leaves a shard's plan guessing at its cost.
- **INSTR-71**, **Anything a screen animates off the wall clock keeps determinism only if the thing
  that installs its draw is the interactive path alone; install it from the build and every
  scripted and golden run inherits the wobble.** The load screen's fill and its 6 fps propeller are
  reported unconditionally by the session build, but the pump that draws them is installed by the
  load board's `_Ready` and cleared by its `_ExitTree`, so a CLI launch leaves the ambient null and
  every report is a no-op: 19 of 19 goldens unmoved, and the motion is testable only through a
  suite that installs its own pump.

## SRC, sources and documents

- **SRC-3**, **Use design documents for intent; retail evidence decides shipped details.**
- **SRC-5**, **A field you don't read may be REDUNDANT, not dropped; try to derive it from the
  fields you already read before deciding what it means.** All 51 `*_delta` vectors are exactly
  `(to − from) / run_time` of the sibling channel.
- **SRC-6**, **A test the binary computes is not a rule until you find what reads its result.** The
  take-hit body computes "same team or neutral" into a byte whose only read is an argument the
  callee never touches, so the original applies friendly damage.
- **SRC-7**, **A key the data authors, a name it carries, or a threshold the artwork separates on
  is not a feature until you find the reader; grep the executable for the key and read the one site
  that consumes it before modelling it.** `enemy_skill` exists in no string of `crimson.exe`; the
  `warning_shot_*` block read as a near-miss rating and is a shield; a puffer blend threshold that
  reached the right answer on the case it was built on is not the bit the engine reads.
- **SRC-8**, **A branch's effect is only half its meaning; the other half is what it is an
  alternative to, so record the jump it skips.** One arm read as a capacity top-up had the entire
  wave teleport as its `else`.
- **SRC-9**, **A negative about a site ("the flag is not tested", "the original only sets a flag")
  needs the control flow around it read, not a search for the immediate; read on past the write
  before treating a field as the whole behaviour.** A flag hoisted into a register at the loop top
  read as unfiltered, and a decode row that stopped three instructions short of the maneuver picker
  supported the opposite of what the handler does.
- **SRC-12**, **Which body axis a matrix row holds is a claim to re-derive at a point of use, not
  to take from another page; a wrong row sign reverses a ported direction without failing
  anything.** Row 2 of the orientation matrix is minus the nose, and read as the nose it put the
  player's shadow behind the chase camera.
- **SRC-14**, **A census over the shipped files counts records, not live things; resolve the record
  in the world that reads it before saying a mode offers it.** Six `IA1` files flag a radio tower
  target and only C1's world has a node of that name.
- **SRC-16**, **A named rectangle in a dialog primitive says nothing about whether it crops the
  source or clips the output; settle it by aligning a reference still, not by reading the name.**
- **SRC-17**, **Two definition files keyed alike are not copies of each other; compare the bodies
  before reading one screen's content out of the other's file.** Every `LOADING_SCRIPT` is a
  superset of the matching `ESC_SCRIPT` under identical keys.

## What this project cannot verify itself

The user must verify audio and control feel, multi-controller behaviour, skilled flying, live-input
flows, and fidelity claims requiring the original. Automated checks must state these gaps.

## Known non-deterministic surfaces

Scripted probes are pinned by `--det`; `--no-det` restores live input, clocks, spawn, and
randomness. Expect variation in flight framing, weather, water, particles, random animation,
damage effects, and liveries. Measure a fresh same-build floor for the exact scenario.

## The standing checklist

- [ ] `.\RunTests.ps1` exits 0; every `SKIP` is understood as not checked.
- [ ] The check can fail and the case exercises the changed mechanism.
- [ ] Inputs, pose, clock mode, binary, and output files are confirmed.
- [ ] Same-build noise and a current baseline were measured where relevant.
- [ ] Complete logs were checked and “pre-existing” failures reproduced on the baseline.
- [ ] Every moved golden is explained and updated in this commit.
- [ ] Relevant modes, chapters, branches, times, and viewpoints were sampled.
- [ ] Anything requiring the user or original game is stated as unverified.
