# Verifying a change in this project

This file contains transferable verification rules. Dated evidence belongs in commit messages,
analysis findings, or git history; a module constraint belongs as a comment on the member it binds,
and format or decode knowledge in `docs/formats/` or `docs/org/`.

Read **METHOD** first, then only the relevant section. Rule IDs are permanent: append new rules
and leave gaps when retiring old ones. Each rule is a bold one- or two-line imperative plus at most
one sentence of measured evidence; everything else belongs in the commit that landed it.

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
- **METHOD-20**, **Reproduce a published measurement's pose *and* its statistic, not just its
  subject.** `CAP-13`'s ring brightnesses were read off frames carrying the sun wash, which scales
  every difference by (1 − α), under a brightest-pixel statistic where an annulus median reads lower
  (`git show analysis-archive:analysis/bl-165-lens-flare/FINDINGS.md`).
- **METHOD-21**, **A scenario entered at a speed it cannot hold is not a measurement at that
  speed; hold the condition the capture was flown in, not just its initial value.** The original's
  143 mph knife-edge take, replayed at full throttle, passes 290 mph inside three seconds, so
  `Probes.KnifeEdge` bisects a level-flight trim throttle for the entry speed instead.
- **METHOD-22**, **Re-measure a fitted constant ALONE once the mechanisms it was fitted on top of
  have been replaced; it may now push the wrong way.** Removing `ClimbGravityScale = 0.6` by itself,
  with nothing else changed, moved the sustained climb from 276.7 to 257.7 mph, toward the
  original's 163.1.
- **METHOD-23**, **"Unreachable" is a measurement, not an inference from the constants around it:
  pin the margin with an instrument and assert it against the loaded value, never the literal.**
  Peak G demand across the eleven airframes is 2.13 to 5.01 G, and the Bloodhawk's 5.01 sits 0.2 %
  past the executable's own fallback threshold of 5 (`ControlLimiterTests`).
- **METHOD-24**, **Fit a trend and a periodic signal simultaneously; never detrend first and fit
  the residual.** A sliding high-pass filter moved the cadence sweep's 930 ms amplitude by 45 % as
  its span changed, while a polynomial and a sinusoid fitted together, inside a window of at least
  eight periods, leave a cubic almost nothing of the fundamental (`ZzCadenceSweep`).
- **METHOD-25**, **Never equate a measured OUTPUT of an oscillator with its INPUT amplitude; a
  fitted constant is interpretable only if it names the quantity it multiplies at the right point in
  the chain.** A kick of envelope `E` renders about 0.28·E as RMS at the real 8/s fire rate, so the
  gun-wobble law's 2.80e-3 rad renders about 0.20 px/frame (`analysis/gun-wobble-shake/`).
- **METHOD-27**, **A count over CSVM's own scene structure is not a count over the original's;
  name the parent before reading a number as a count of objects.** One flak over a C1 aagun dealt
  four splash shares over two nodes, each split into a `col` and a `col_buildings` body by
  `SceneBuilder.AttachCollision`'s per-surface-class carve.
- **METHOD-28**, **A/B a new opt-in flag against the code it replaces, never against the flag
  switched off once that code is already deleted.** Turning `applyActive` off in a tree whose
  root-level `built.Visible = node.Active` had gone measured a third state nobody ships, and the
  census read four placed roots (C1's fuel trucks, `piratezep`, `barracuda`) as changed by the fix
  until the real baseline was put back.
- **METHOD-29**, **Before blaming a steady-state residual on a state variable, check that the
  variable has any freedom at steady state.** The flight plant's cross-path acceleration composes to
  `sin α · [positive]`, so a sustained straight climb has exactly one solution and α is zero in it;
  the off-path force balance that reads as an explanation of the climb's 25 % speed residual is a
  transient the plant leaves in under a second (`docs/org/flightModel.md`, "The sustained climb").
- **METHOD-30**, **Span a hand-back with the press itself: a modal that consumes a press hands
  input back while that press is still down, so a check that presses only after the hand-back cannot
  see the new owner read its tail.** The board behind a campaign film is live again on the frame the
  film stops, so the click that skipped the film armed the plaque under the pointer and fired it on
  the release; the hand-off's own tests pressed after the stop and passed throughout
  (`ChapterCinemaWiringTests`, `CinemaFilm`).

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
  pose-dependent symptom needs an off-axis heading AND a far-from-origin placement in the matrix,
  and a repro that works where the report fails means the poses differ, not that the report is wrong.**
- **DIAG-20**, **An effect that never starts passes every "it ends correctly" check; prove the
  START from the production call path before diagnosing the stop.** `large_30sec_fire`'s ~1,035
  death calls sat in the unparsed `unknown_seq` block while a direct-`Start` `stop-sequence` suite
  and two cockpit passes all read the absence as a working stop.
- **DIAG-21**, **A puffer's particle spread is unseeded RNG, not the pinned run seed.** Two
  captures at the same step can differ in particle placement alone; do not read that difference as
  a behaviour change (the anim lab's own determinism boundary).
- **DIAG-22**, **A suite that asserts over a directive the data may not author fails vacuously;
  loop over the authored candidates, then branch on the count.** The `campaign-objectives-hud` wake
  cue asserted a `WAKEUP_SOUND_GROUP` one-shot with no candidate in hand, and `c4/m01` and `c5/m01`
  author none, so both chapters read as a sound defect while their objective audio plays.
- **DIAG-23**, **Dispatching a callback directly tests what the code does, never what the codes
  around it undo; play the definition that raises it.** Driving CM02's 967 alone left the captured
  Balmoral hidden and the suite green, while playing `ww_balmoral1` showed its `913`/`914` pair
  putting that aeroplane back **19.25 s** later.
- **DIAG-24**, **A fixed sim window shorter than a `CallAnimation` chain's own completion time
  reads as an unresolved bind, not a slow clock.** CM13's `pzhomebase` gates two landing-cone
  activations behind `pz_deploy_hook`'s `WAIT_FOR_COMPLETION`, a 3 s offset plus a 10 s rotate, so
  `zeppelin-hull-activation`'s 12 s window read `cones 0/2` on cones that draw together at t=13 s.

- **DIAG-25**, **A destructible killed by another definition's call logs no `[anim] damage:`
  line; read the pool's status, or the state its death latches, and never the damage log.** Only
  `AnimRuntime.DamageAt` writes that line, while `KillCalledDestructible` zeroes the pool and runs
  the death straight, which is how a Gemini whose gasbag deaths had demolished four cannon bays
  reads as "no cannon destroyed at all".
- **DIAG-26**, **No objective completes while a mission's intro cutscene holds the world, so a
  scripted `--debug-objective=` run on such a mission marks nothing.** `CampaignDirector.Step`
  returns before `Graph.Step` under the cutscene hold, and `--pos=` is withheld until the handoff
  too, so a probe waiting on either needs a mission whose intro hands off inside the run: C3/M01's
  had not by 43 s of sim, where C1/M02's marks its row well before 25 s.
- **DIAG-27**, **An arithmetic shape in a dispatcher (`index = code − N`) names no table; read the
  callee and check which subsystem owns it, then confirm against where the data authors the code.**
  The `CALLBACK` reference read 701 and 702 as "applies camera-parameter set `code − 700`", on the
  strength of the subtraction alone. `FUN_0049a210` is the multiplayer flag list, gated on a live
  network session, and the two occurrences are `flg_throw1`/`flg_throw2` in the `MP2` missions, so
  the codes do nothing in single player at all.

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
  clipped highlight `argmax` returns wherever the tie breaks, 16 px off the sun's centre in C3, and
  ring 2.0 of the lens flare sits at twice the sun-to-centre vector, so it inherited double that
  error and measured as pure sky (BL-165).
- **SHOT-21**, **Keep radial probes clear of the frame edge.** A circular sample that runs
  off-frame reads clamped pixels as content, so a thin annulus averages to background and reports a
  confident zero; place the subject diagonally when the probe radius would cross an edge (BL-165).
- **SHOT-22**, **A full-screen effect needs an off switch before it contaminates unrelated
  captures.** The lens flare's wash reaches α ≈ 0.66, survives terrain occlusion and whitens the
  HUD, so `--no-flare` exists for the same reason `--no-fog` does (BL-165).
- **SHOT-23**, **Measure how far fog lets a texture survive as a PLATEAU-RELATIVE high-pass, and
  compare it in ELEVATION ABOVE THE TRUE HORIZON, never in rows from the top of frame.** At C1's
  river pose the original's overcast mottling runs at high-pass RMS ≈ 0.59 against ours at ≈ 0.26,
  and the two frames' pitches differ by 5.7°, which a depth-from-top row number hides.
- **SHOT-24**, **Measure cloud-sheet structure by the sky-to-tops transition depth, not by
  column-autocorrelation or crest-spacing.** Perspective makes a fixed world-space placement period
  aperiodic in screen space, so a row/column ACF over the sheet found no peak above 0.33 on either
  side of the A/B, including a 0.69 peak that was capture noise at sd 0.91 to 2.58 (METHOD-14).
- **SHOT-27**, **The true horizon is a CALIBRATED row, not the sky/terrain boundary: shoot the same
  position LEVEL and check the shift is `f·tan(pitch)`.** C1's river pose pitches 5.712° down at
  `f` = 599.1 px, putting its horizon at row **419.9**, and the same position shot level puts every
  feature exactly **60 px** higher against a predicted 59.9.
- **SHOT-28**, **A nadir shot cannot tell a painted rooftop from an extruded building, and a global
  instance count cannot tell you where the instances went; a placement claim needs a low oblique.**
  Gating C5's clutter on polygon bit `0x800` held `cb00a` at 79 % and `cb12a` at 96 % of baseline
  while a 180 m oblique over the same crossroads showed the near-field skyline completely gone.
- **SHOT-29**, **Before pinning a golden, measure how much of the frame the subject actually owns;
  a subject no scripted camera can approach does not earn one.** An AI aircraft staging its whole
  damage ladder differs from the pristine control by **396 of 921,600 pixels, 0.043 %**, because
  `--ai=` places the plane 250 m ahead of the chase camera and it outruns the player from there.
- **SHOT-30**, **A headless AI shootdown is reachable, and it writes `DESTROYED …`, not `CRASH into
  …`, so grepping a run for `CRASH` alone reads a working shootdown as nothing having happened.**
  `RunProbe.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury --ai-damage=0.02 --fire
  --frames=1200` kills the AI plane in about a second; only the ground contact writes the second line.
- **SHOT-31**, **`SHOT-29`'s verdict is about the SUBJECT, not about AI aircraft, so re-measure it
  for each one.** The same `--ai=` kill three seconds later, at the destroy def's handover, differs
  from an unkilled control by **236,925 of 921,600 pixels, 25.7 %** in a 215 × 206 px fireball,
  against 0.043 % for the same aircraft's staged damage ladder.
- **SHOT-32**, **A shot of an animated screen proves the frame it drew, never that the screen keeps
  drawing; check an animation by driving frames.** `--menu=campaign-briefing:<s>` advances the reveal
  itself before rendering, so timed shots at 6, 12, 24, 40 and 70 s all read correctly while driving
  5400 frames of the live menu found 5193 stale frames (BL-485).
- **SHOT-33**, **`--debug-pointer=x,y,down` cannot press a button: it holds one. Whatever a press
  opens is shot through a `--menu=` aid, not through the pointer.** The aid's press arrives as an
  edge once and never releases, and a menu row fires on the release on it, so a run aimed at a live
  plaque shoots the held frame and nothing behind it. Reading that shot as the button doing nothing
  is the error the aid invites.
- **SHOT-34**, **Before attributing a brightness gap on a blended population to a colour term,
  check whether its texture carries any colour at all; a constant-RGB alpha mask makes the gap a
  coverage measurement.** Every texel of `cloud1`/`cloud2` is RGB 239 with the whole image in the
  alpha channel, so an `fvol` card has exactly one colour and one rendered ceiling
  (239 × 240/255 = 224.9); the original's measured 209 plateau is 32 % of the fogged background
  showing through, and a vertex-colour scale calibrated to it corrects the wrong quantity
  (`docs/org/cloudCards.md`).

## GOLD, golden images

- **GOLD-1**, **Update moved hashes with the visual change, and explain each moved shot in the
  commit message.** The explanation is history and belongs in the git log; `manifest.json`'s
  `exercises` field says what a shot covers *today*, so do not append a re-pin note to it.
- **GOLD-2**, **A golden is a tripwire, not a diagnosis.**
- **GOLD-3**, **For render-path changes, sweep every golden and inspect the largest movers.**
- **GOLD-4**, **Reproduce a golden with its OWN `frame` count, or the A/B is meaningless.** The
  runner appends each shot's manifest `frame` field to its args, so an ad-hoc `RunProbe.ps1` repro
  that omits it renders a different sim frame and reads as a catastrophic regression.
- **GOLD-5**, **Which goldens move is itself evidence; check the pattern, not just the count.** A
  change that should touch one subsystem should move exactly the shots exercising it: per-plane
  chase distance moved all four flown-aircraft shots and none of the nine without an aircraft.
- **GOLD-6**, **A pure TIMING change moves pixels, and a particle shot amplifies it without limit;
  judge one by what the moved shots ARE, never by the magnitude.** Moving every `CALL_SEQUENCE`
  event 1/60 s earlier left 7 of 8 chapter captures bit-identical and moved `c1-crash` by **79.7 %**
  of its pixels, against **1.5 %** for the dispatch rule that matches the original.
- **GOLD-7**, **A golden shot that exits nonzero with no PNG is retried once under Godot's
  `--verbose`, with every attempt's evidence kept either way.** `RunTests.ps1`'s `goldens` stage
  reuses `.scratch\goldens\` every run, so the next shot's launch would overwrite a silent exit-1's
  logs; each attempt's `.log`/`.out`/`.err` and PNG are copied into `.scratch\goldens-failures\`.
- **GOLD-8**, **When an earlier item deliberately left goldens un-repinned, a later item's "moved"
  list is about BOTH changes; recover the current item's own movers by A/B-ing hashes against a
  temporarily reverted build.** The run reported the same 9 movers as the previous item, yet only 8
  moved for this one, and `c5-city-night` was byte-identical across it.
- **GOLD-9**, **"13/13 hash-identical" proves nothing until `git diff` shows
  `analysis/goldens/manifest.json` unmodified in the working tree.** The stage compares against the
  file on disk, so a stray `-RegenGoldens` re-baselines the tripwire onto the build under test:
  three full runs reported 13/13 identical while that diff showed six hashes had moved.
- **GOLD-10**, **Hash the raw pixel buffer, never the saved PNG.** Encoded bytes differ between
  two byte-identical images (all 881 of C1's texture PNGs do), so a PNG hash reports encoder
  state, not pixels.
- **GOLD-11**, **Do not move where a `Puffer` is constructed, or construct one on a capture
  path outside its normal init.** Each emitter draws one RNG seed off `Rng.Puffer` at
  construction, so construction order alone re-pins every puffer-bearing golden.
- **GOLD-12**, **A change to a shader-key generator dumps every reachable key's `Shader.Code`
  before and after and sweeps every golden; original mode stays byte-identical on both.** The dump
  catches a text change the goldens' camera poses never frame, and the goldens catch a runtime
  effect, a light or a tonemap curve, that the shader text cannot show.
- **GOLD-13**, **Every pinned shot is a settled frame, so a defect that lives on one transition
  frame is invisible to the whole set.** The three menu shots stand on a presentation that has
  finished standing up, and no `--menu=` aid can press an Options apply, so nothing in the goldens
  can photograph the frame a presentation switch passes through. Hash-identical shots prove such a
  change moved no settled pixel, never that it fixed the transition; settle that from the frame
  order in code and from a suite that reads the state across it.
- **GOLD-14**, **A blend-ORDER change is invisible wherever the overlapping quads share a colour
  and an alpha, so judge one by the shots that CAN move and never by a count of unmoved ones.**
  Alpha-mixing identical colours is a convex combination of identical colours, so the pixel is
  order-independent. Sorting every particle back-to-front left `c1-waterfall` and `c1-stunt-marker`
  bit-identical, both single-column sprays, and moved the five shots carrying two live flipbook
  columns by 0.007 % to 0.712 % of pixels; two of three frames of the scene the item was filed on
  read byte-identical for the same reason.

## DET, determinism and randomness

- **DET-2**, **Disable live input during scripted runs.**
- **DET-6**, **Scripted probes imply `--det`; use `--no-det` for realtime behaviour.**
- **DET-7**, **Deterministic results must depend only on committed inputs.**
- **DET-8**, **`--det` ignores `config.json`; use committed or CLI inputs.**
- **DET-9**, **Keep pure baselines free of clocks, absolute paths, and machine state.**
- **DET-10**, **Fixed-step captures cannot reveal realtime cadence artifacts.**
- **DET-11**, **A rate decoded from video of the original is quoted in SIM seconds (k = 1.390);
  implementing the wall figure runs it 39 % fast.** Neither a build nor a golden can tell the two
  apart, only a dwell or duration logged in sim time: `BL-184`'s 168.7 °/sim-s arrow sweep and
  `BL-148`'s 643 ms stall-lamp half-period carry the wall figure beside them in the source.
  ⚠ **It applies to a measurement, never to a rate the binary itself denominates.** Where a decode
  puts a constant on a dt the engine builds from the system clock (`DAT_009ad744`, a
  `GetTickCount()` delta at `0059c0c0`), that constant is already per real second and converting it
  is the error. The chase camera's relaxation carried a converted 0.65 against an authored
  `dist_catch_up` of 1.0, because the clip's raw 0.90 /wall-s was converted before the comparison.
- **DET-12**, **An angle measured off footage cannot confirm a decode; at best it ranks two
  readings, and it will happily rank a third one you have not thought of.** `CAP-16`'s wing-panel
  strip measured 20 to 30 °/s and was recorded as confirming `forward_rotation` as a total angle
  over `RUN_TIME`, while the binary says the crash pieces do not turn at all.
- **DET-13**, **A per-instance draw sequence off a dedicated `Rng` stream pins its field's whole
  layout to the master seed.** Measured on the same `--det` pose across two runs: `Precipitation`'s
  `Rng.Precip` draw took C2B rain from 5.44 % of pixels differing to 0.00 %, C4 snow from 25.84 %
  to 0.00 %.
- **DET-14**, **A golden sweep cannot see a `--det` drop fail while a second guard stands in front
  of it, so test the drop where it is written and not by its pixels.** Removing the `--det` guard
  from `ResolutionSetting.SavedWord` left all 18 goldens hash-identical, `Launcher`'s
  `!_spec.IsScripted` still holding the size back; removing both moved every shot to 1920x1080.

## PERF, performance

- **PERF-1**, **`script_ms` is Godot's `TIME_PROCESS`: the WORST single `_Process` pass of the
  last wall second, refreshed about 1 Hz, so it is neither a per-frame cost nor a mean; read
  `proc_ms` for the measured pass and `proc_max_ms` for the worst one in the window.** Over 20
  windows of a flown C1 it ran a 9.44 ms median against `proc_ms`'s **1.645 ms**, exceeded the
  window's own worst frame in 17 of them, and held two distinct values across a 120-frame hitch
  ring whose `frame_ms` spanned 5.93 to 47.51 ms (`Launcher.ReportPerf`).
- **PERF-2**, **Capped metrics are floors, not costs.**
- **PERF-3**, **Split broad timers before choosing what to optimize.**
- **PERF-5**, **Ignore differences below measured noise and an absolute floor.**
- **PERF-7**, **Compare startup timings under identical cache conditions.**
- **PERF-8**, **Use the engine startup report, not whole-process time.**
- **PERF-9**, **Use two unchanged pairs for noise, then measure A/B back to back.**
- **PERF-11**, **Use `--no-vsync` and metrics valid for the clock mode.**
- **PERF-12**, **A frame ordinal does not convert to wall time at an assumed refresh rate.**
  `HitchMonitor`'s grace window is milliseconds and vsync's actual refresh on the dev machine is
  120 Hz, so a `--hitch-inject=` frame 120 chosen assuming a 60 Hz cap lands at ~1000 ms, half the
  default 2000 ms grace, and neither a 50 ms nor an 80 ms stall tripped until past ~frame 240.
- **PERF-13**, **A hitch count only compares across runs in the same vsync mode; per-frame cost
  transfers, frequency does not.** Vsync paces hitch frequency directly and pins `HitchMonitor`'s
  rolling median at the refresh interval, so at a 60 Hz cap `medianMultiple × refresh_interval` is
  66.7 ms, above the 40 ms floor, and the relative term decides (`HitchMonitor.cs`, PERF-12).
- **PERF-14**, **A build-time CLI preset can never trip `HitchMonitor`; verify with a live,
  post-grace event instead.** `--damage=`/`--destroy=` presets apply inside
  `Launcher.LaunchSession`'s build, always finished before `Rearm()` starts the grace window, while
  `--crash=300` under `--no-vsync` produced `part_detach` 48.4 ms and `effect_pool_miss` 62.1 ms.
- **PERF-15**, **A flat-leaf sampler's "negligible" cost only holds outside per-particle loops; a
  scope placed inside one becomes the thing it measures.** `PerfSample`'s open plus close costs about
  60 ns with zero allocation, a third of a microsecond for six coarse scopes a frame and a rival to
  the frame budget across a few thousand particle draws.
- **PERF-16**, **`StartupProfile`'s `boot` figure is engine start → build start, so on a
  launchscreen-driven rebuild it also holds however long the menu sat idle.** Do not read it as
  pure engine overhead on a relaunch.
- **PERF-17**, **`StartupProfile`'s `rest` term is `build − Σ(phases)`, real uninstrumented work;
  on a probe run it also holds whatever the probe itself did before the build closed, not only
  build overhead.**
- **PERF-18**, **A verification-time budget is an awareness threshold, never a verdict: it may
  print, it may not change an exit code.** `RunTests.ps1`'s budgets
  (`analysis/verification-budgets.json`) are set at the slowest of three back-to-back warm runs plus
  50 %, and a stage past one prints `over budget` beside a summary whose exit code is unchanged.
- **PERF-19**, **A GC capture that ends inside the first minute of a session measures the world
  build settling, not the steady state; let the session settle, and say which regime a number comes
  from.** Under `--fly --chapter=C1 --perf --no-vsync` the first seven or eight collections are gen1
  and gen2, promoting 18 to 52 MB at 20 to 45 ms, against a settled gen0 every 13 s at 10 to 13 ms.
- **PERF-20**, **A .NET GC pause here is set by how many FINALIZABLE objects died, not by bytes
  promoted, so judge such a change on pause per wall second and on dropped frames, never on the
  per-collection figure.** The pause tracks the `GCHeapStats` finalization-promoted count at roughly
  0.4 to 0.5 ms per thousand, and halving allocation stretched 13 s/10-13 ms into 33 s/25-31 ms.
- **PERF-21**, **`physics_ms` is the WORST single physics tick of the last wall second, neither a
  per-frame cost nor a mean; read `phys_tick_ms` for the step cost and `phys_hz` for whether the sim
  keeps up.** Over 333 windows of a flown C2/M02 session `physics_ms` ran a 16.96 ms median against
  `phys_tick_ms`'s **1.81 ms** (`Launcher.ReadFrameCounters`; `script_ms` is the same shape, PERF-1).
- **PERF-22**, **A memo whose entries are a pure function of their key belongs to the process, not
  to the builder instance; split fresh-key work from repeat-key work before optimising anything
  else.** Of a ~300 ms `ai_spawn` frame on CM18, 190 ms sat in the 13 to 15 `ShaderMaterial`
  assignments whose `Shader` was fresh, about 13 ms each, against 0.1 ms across the other 45.
- **PERF-23**, **A per-object diagnostic on a shared periodic boundary is a BURST, not a spread
  load, so ask the log filter at the call site before the values are formatted.** On a flown C2/M02,
  `FlightController`'s ungated once-a-sim-second print cost 17.0 ms of an 18.3 ms tick with nine
  aircraft alive, and gating it took the windows over the 16.7 ms budget from 47 of 82 to 3 of 82.
- **PERF-24**, **When a frame's cost is in neither physics, the render terms nor any named script
  scope, count the nodes ASKING for `_Process` before looking at what the callbacks do, and price
  gating one off at one frame of deferred first draw.** On C2/M03 the C# `_Process` pass cost 9.4 ms
  of a 12.4 ms frame while its bodies summed to 1.06 ms over 3,814 `Puffer` nodes, about 2.3 µs each.
- **PERF-25**, **Deferring a block off a hitching frame moves its cost rather than removing it, and
  a block sized by its data must be split along that data's grain; name the phase with no seam of
  its own first.** Splitting CM18's `cargozep1` crash rig one `effect_pools.json` slot a step took
  one 35 ms frame to a dozen of about 3 ms, leaving `AnimRuntime.PrewarmEmitters` at 15 to 103 ms.
- **PERF-27**, **A Godot wrapper built per call on the frame path is a finalizable object, so look
  for the ones a call MAKES (a string handed to a `StringName` parameter, a physics query parameter
  object) and judge the fix on `[perf] gc`'s `fin_per_s`, not its `pause_per_s_ms`: the count
  reproduces to a tenth of a percent, the pause it sets to a few.** Caching one per-frame
  `StringName` and reusing the flight query objects took a 150 s C1 cruise from 1352/1353 objects a
  second to 927/929, and its pause from 0.510/0.529 to 0.401/0.374 ms a wall second.

- **PERF-26**, **A process-wide "nothing to do" guard is not a fast path in a real session; read
  its state at the moment you measure, not at process start.** `WorldCollision`'s live-faded-root
  count never returns to zero once a world is freed with an effect resting at opacity 0, so the
  `fade-walk-bound` suite's no-fade baseline reads 0 ancestor steps run alone and 44 once
  `effect-pool-reset` has run in the same process.
- **PERF-28**, **An exact-zero allocation claim needs more than one measurement window: take
  several `GC.GetAllocatedBytesForCurrentThread` windows and require that ONE of them reads zero,
  rather than widening the assertion to a tolerance.** A real allocator charges every window alike
  (a one-byte array added to `PerfSample.Close` reads 320,000 bytes over 10,000 iterations in all
  five windows), while 60,000 windows in the unit host under load, 294 of them crossed by a
  collection, all read exactly zero, so a lone charged window is the runtime's and not the path's.

## LOG, logs, error censuses, and exit codes

- **LOG-1**, **An empty report may mean the mode did not build the feature.**
- **LOG-2**, **State the time, count, and lifecycle window before concluding from absence.**
- **LOG-5**, **Report caps and truncation; never infer absence from a shortened list.**
- **LOG-8**, **Run shader checks windowed; `--headless` skips the render path.**
- **LOG-12**, **Automated instruments must return their verdict in the exit code.**
- **LOG-13**, **Do not overlap engine probes.** `RunTests.ps1`'s own engine shards and its
  `-GoldenWorkers` are the exception, each carrying its own `--log-file`, report, scratch directory
  and watchdog, and its hitch stage runs last for the same reason; what still does not isolate is a
  suite whose store sits outside `.scratch/`, such as `campaign-loop`'s `user://` profile.
- **LOG-16**, **A census printed at the end of setup cannot report a runtime miss.**
- **LOG-17**, **In a worktree, `RunTests.ps1` exits 0 having run only the units.** A worktree has
  no `tools/godot` and no `extracted/`, both git-ignored, so the in-engine suites and the golden
  hashes skip silently; set `$env:CSVM_DATA_ROOT="Z:\CSVM"` and confirm the printed suite and golden
  counts are non-zero.
- **LOG-18**, **`--debug-anim`'s motion line prints a GLOBAL position and a LOCAL rotation, so its
  `rot` is world attitude only for a placed template root.** The parachute's line read
  `rot (0.7, -92.2, -9.3)` while its wreck read `(39.5, 19.6, -118.3)`, two frames that only looked
  comparable.
- **LOG-19**, **`RunProbe.ps1`'s hidden desktop is shared by every worktree on the machine, so a
  sibling agent's probe finishing mid-run kills your viewport; count the Godot processes naming
  another worktree before believing an error census.** The signature is a repeating per-frame
  `NullReferenceException`, a `global_shader_parameter_set` condition and two `viewport is null` lines.
- **LOG-20**, **A worktree's `user://` is the SAME directory as the main checkout's, so a probe
  reads and writes the real saved profiles, planes, bindings and options; copy a profile under a new
  name before naming it in `--campaign=`, and delete the copy.** Godot derives `user://` from
  `project.godot`'s `config/name` alone, so every tree of this project shares
  `%APPDATA%\Godot\app_userdata\CSVM\`, which each run prints as its `[core] user=` line.
- **LOG-21**, **An ordinary `.\RunTests.ps1` decodes the opening frames of the ten `.mpg` cinemas,
  not all of them; run `$env:CSVM_MOVIE_WALK=1; .\RunTests.ps1` for the whole-file walk and its
  frame-count pins.** Ten whole decodes cost about 65 s against the unit stage's 30 s budget, so
  what runs every time reads all ten files and decodes one group of pictures from each, and
  `MpegMovieTests.EveryCinemaDecodesEveryFrameItCarries` reports skipped with that command in its
  reason rather than passing silently.
- **LOG-22**, **A `*.godot.log` mirror is not that run's log: exclude it when sweeping
  `.scratch/logs/` for what a run did or did not print.** Godot appends to one shared
  `app_userdata/CSVM/logs/godot.log` and every quit copies the whole file, so a sweep for "no
  battery launch played a cinema" reported 24 of 72 logs carrying one; the same sweep over CSVM's
  own `fly-*.log` reported 0 of 48, the earlier hits all being one deliberate probe replayed by
  every later mirror.

## WORLD, world data and runtime traps

- **WORLD-8**, **Resolve objects by source identity, not normalized node names.**
- **WORLD-9**, **Verify that the selected mode builds the product being measured.**
- **WORLD-10**, **Report opposing transitions separately; a net can hide both.**
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
  describes it.** C3 authors `SUNLIGHT_ORIENTATION` yaw 135 while its gamez `sun` node sits at yaw
  45, so a flare anchored to the parameter draws 90° from the visible sun; anchoring to the object
  also survives any coordinate-conversion error (BL-165).
- **WORLD-27**, **`--play-anim` proves a definition RUNS; it says nothing about whether the game
  ever reaches it, so drive the real entry point (`--debug-damage=node=…,kill` for a destructible)
  before concluding the definition is at fault.** CM10's `lifefall11` played all eight of its motions
  in the anim lab while no shot in the mission could reach it, a second pool owning the hit.
- **WORLD-28**, **A suite world with no `ContactMask` wired silently poses every untimed
  `OBJECT_MOTION` at rest, so a suite that must see such a launch wires the mask itself.** A launch
  with no `RUN_TIME` ends only at a contact tier: CM10's lifeboat drop reads 0.0 m fallen without a
  mask and 6.2 m with `collision: true` plus `runtime.ContactMask = CollisionLayers.World`.
- **WORLD-29**, **A term you meant to redirect can leave instead, and the frame looks like a win
  either way; prove the new source arrives with a control colour.** Setting the Environment's
  background colour as its own reflected light source gives Godot no radiance map (the reflection
  needs a `Sky` resource), so the water's specular vanished and the day sea read as a success.
- **WORLD-30**, **`Wake(n)` fires only that objective's wake verbs synchronously;
  `ADD_OBJECTIVE_TARGET` applies through its own completion, one `Step()` later when nothing else
  gates it.** CM10's OBJECTIVE10 leaves its site unoffered until the next `Step()`, a gap real in
  isolation and invisible in play, since both happen inside one Step call at normal frame rates.
- **WORLD-32**, **A Godot property that accepts a write is not a property the renderer reads:
  prove a lighting knob is live by driving it to an extreme and watching the goldens move.**
  `Environment.AmbientLightEnergy` is inert in the faithful path, 0.9 to 0.0 leaving all 18 goldens
  byte-identical, while `DirectionalLight3D.LightEnergy` 1.6 to 0.5 moved 7.
- **WORLD-33**, **An upward ray reports open air under a one-sided collider, so read a column
  DOWNWARD from above instead.** World colliders honour the polygon's own `SHOW_BACKFACE` and 64 %
  of the install's collision faces clear it: across all eight chapters a downward ray answers in
  1,376 of 1,376 partition cells and an upward one in 24 (`chapter-census`).
- **WORLD-34**, **A coplanar base/overlay texture pair can resemble a day/night or LOD variant
  set; compare UV scale and vertex colour before assuming duality.** C5's flagged `cblock1/2/3`
  overlay differs from its unflagged `cblock4/5/6` base only in resolution and brightness, sharing
  an identical 256 m UV scale, y-plane and all-white vertex colours.
- **WORLD-35**, **A suite world built with `collision: false` runs both `OBJECT_MOTION` contact
  tiers structurally off, so it can play a whole breakup and say nothing about where any piece comes
  to rest.** `gemini-gasbag-bays` drives `killgmzep` end to end over such a world while C2B/M04's
  `gasbag1` and `gasbag4` were resting 13.1 m and 26.9 m under the sea.

- **WORLD-36**, **Every vehicle a mission never placed stands at the world origin, one hull inside
  another, so scope a ray's answer to the subtree you asked about.** An upward probe from a parked
  `multiplayer1zep` belly ring answers `workersvoyage_cargobay` at 5.9 m in C1C's Instant Action
  world and `g375` at 24.4 m in C5's, neither of them any part of the hull the ring hangs from.
- **WORLD-37**, **A hull the mission's `.gw` switched off has its colliders disabled by design, so
  "the ray found nothing" reports the switch rather than a missing collision mesh.** Both
  multiplayer zeppelins are switched off by every mission script but `mp3.gw`; over the eight `MP3`
  worlds that keep them, all 48 belly rings meet hull 12.8 m straight up.
- **WORLD-38**, **A boot-script setting counts only if a retail run reaches the script that sets
  it.** `support\main.gw` sources `support\<chapter>\load.gw` in its `ifndef USEZBD` arm alone, and
  that arm loads `%CAMPAIGN_DIR%\terrain\*.flt`, which no retail install ships. So `load.gw` is the
  data-compile path: a command found there (`MipBias -1.0` in C1 to C4) never executes at retail,
  while the same command in `adjust.gw`, sourced unconditionally, always does. Grep both before
  reading a count of "the chapters that set X".

## SHELL, Windows, PowerShell, and processes

- **SHELL-2**, **Identify stray Godot processes by worktree and probe flag.**
- **SHELL-3**, **After bulk rewrites, run Godot as well as the compiler.**
- **SHELL-7**, **On PowerShell 5.1, read BOM-less UTF-8 through an explicit UTF-8 API.**
- **SHELL-10**, **Launch scripted Godot probes through `RunProbe.ps1`.**
- **SHELL-12**, **Give every scripted probe an exit condition, and check the flag you chose actually
  is one.** `--frames=N` sets `SessionSpec.ScreenshotFrames` (bound in `SessionSpec.Parse`), a
  warm-up counter that terminates a run only alongside `--screenshot`; passed alone it reads as valid
  and the probe runs until killed, one `--debug-anim` run writing a 45 MB log over six hours.
- **SHELL-13**, **A scripted run must not steal desktop focus.** The window is created with
  `no_focus` in project.godot; setting `WindowSetFlag` at runtime after the fact does not hand
  focus back, so an interactive session must request focus explicitly instead
  (`Launcher._Ready`).
- **SHELL-14**, **Test window-focus handling by alt-tabbing, not minimising.** Minimising a
  Godot window delivers only the mouse enter/exit pair, never a focus notification; on Windows
  11 / Godot 4.7 a real focus change delivers `APPLICATION_FOCUS_OUT`/`_IN`, not the
  `WM_WINDOW_FOCUS_*` pair.
- **SHELL-15**, **A BOM a PowerShell write left on a source file comes BACK the next time the Edit
  tool touches that file, so strip it as the LAST step and confirm with `git diff`, not with
  `ReadAllBytes`.** Edit rewrites from its own cached read, which still carries the byte-order mark;
  the visible symptom is a diff whose first line is the file's unchanged `using`.
- **SHELL-16**, **An incremental `dotnet build` after restoring a file to byte-identical content
  can silently no-op, so an A/B built by swapping file content needs `--no-incremental`.** MSBuild's
  up-to-date check sees content it has already compiled: an after-the-fix probe log showed the same
  reader files as the before probe until `--no-incremental` produced a real ~8 s rebuild.
- **SHELL-17**, **A census script must not name its accumulator after one of its own parameters:
  PowerShell variable names are case-insensitive, so `$nodes = @()` under `param([string]$Nodes)`
  writes the empty array into the TYPED parameter and turns every later `+=` into string
  concatenation.** A terrain scan reported "terrain nodes: 1" where the same parse inlined found 231.
- **SHELL-18**, **Quote a comma-bearing command-line value in PowerShell, or the unquoted comma
  splits it into an array and the run fails.** `--pos=-6500,300,-1500` and a multi-name
  `--tex-census=` list both die instantly unless quoted.
- **SHELL-19**, **Quote a launched argument that carries a space, or the process re-splits it
  before the callee sees it.** `-ArgumentList` quotes nothing itself, so a data-root or Godot
  path with a space arrives split unless the launch scripts quote it.
- **SHELL-20**, **Under `$ErrorActionPreference = 'Stop'`, redirecting a native command's stderr
  makes its failure terminating, so a probe whose failure is the answer must lift the preference and
  read the exit code instead.** `gh release view v0.1.0 2>$null` ended the run on "release not
  found", which is the expected answer when the tag has not been published yet; an unredirected
  call is unaffected, which is why the idiom survives everywhere the command happens not to write
  to stderr.

- **SHELL-21**, **`[IO.File]::Copy` preserves the SOURCE file's last-write time, so swapping a
  baseline file in for an A/B can leave the build stale and measure the same binary twice.** The
  format hook's `-t:Rebuild` runs BEFORE the command that does the swap, and the swapped-in file is
  then older than the outputs, so the incremental build that follows has nothing to do. A limiter
  margin table taken that way agreed to two decimals on all eleven airframes, reading as "the change
  moves nothing", where the real A/B moved every row. Swap in one command and measure in the next,
  or set `LastWriteTime` to now after the copy.

## INSTR, building instruments

- **INSTR-3**, **Share derived predicates with production code.**
- **INSTR-5**, **Log resolved outputs as well as lookup inputs.**
- **INSTR-6**, **An able-to-fail control over randomised state must sweep seeds, not pin one.** A
  pinned seed makes one draw, and a bug that fires on some draws is invisible on the rest: with the
  `BL-240` hold removed, the recorded `--destroy=m_build` probe reports 0 misses at seed 1 and
  2/1/1/1 at seeds 4/7/9/10.
- **INSTR-7**, **"Not decidable from this data" is a fact about the instrument, not the question:
  when a census comes back uniform, ask what else varies the quantity.** All 88 `destroyable_parts`
  pairs ship equal, which correctly made (armor, hp) undecidable from `extracted/`, while the
  original's armory varies armor independently of health and settled it in one screen.
- **INSTR-8**, **Z-fighting is instability, not appearance: measure it as pixels that SWAP WINNER
  between captures a millimetre apart, never by looking at one frame.** Flattened to loud colours
  with `--tex-override`, the C1B water/shoreline pair swaps 16,260 px (1.76 % of frame) at the
  shipped separation, 1,774 at 5e-6 and 0 at 1.2e-5.
- **INSTR-9**, **A score normalised per axis cannot judge a question about which axis is which;
  prefer a statistic invariant to the thing you are not testing.** The `AT_NODE` axis-order census
  scored 63:101 against the reading three sound instruments confirm, led by an escape of 11,900 that
  is a division by a ground ring's **0.0 m** thickness.
- **INSTR-10**, **An assertion keyed on a field that is not unique reports on whichever subject it
  reaches first; qualify the key, or the check is about something else.** Puffer names repeat across
  definitions, so the `BL-229` suite's `Census.Any(r => r.Name == "trailpuffer2" && !r.Emitting)` was
  answered by a fireball's row and passed a runtime with the stop under test deleted outright.
- **INSTR-11**, **A probe that reports one half of a compound thing reads as a full pass on the half
  it can see, so sample over the window rather than at its end, and ask what is left behind.**
  `--effects-test` reported `33/33 resolved, 30 built a puffer` while the MESH half of several of
  those effects never drew at all (`BL-061`).
- **INSTR-12**, **A straight-up billboard probe reads edge-on and reports nothing about
  altitude.** A `cloudsprite` card is a `Facade`/`SphericalY` billboard, so a zero-green-pixels
  result looking straight up is a fact about billboard orientation, not proof the field is absent
  below that altitude (a compound-thing narrowness in INSTR-11's shape).
- **INSTR-13**, **An in-engine suite runs inside ONE frame: a physics body MOVED after creation
  never re-enters the space queries, so aim at bodies where they were created.** A later
  `GlobalTransform` write reaches the physics space only on a flush a synchronous suite never gets:
  in `ai-actor` the ray returns the aircraft at its spawn pose and nothing 3.7 km away.
  ⚠ **An aircraft body is worse than a static one, and the usual repair does nothing for it.**
  `AircraftBody` is an `AnimatableBody3D`, so the server reads a transform write as a kinematic
  motion target and moves the collider only when it steps: `ForceUpdateTransform()`, which does
  commit a plain `StaticBody3D`, leaves it where it was, and so does writing the transform onto the
  server by hand (`PhysicsServer3D.BodySetState` followed by `BodyGetState` reads back the spawn
  pose). A flown aeroplane therefore leaves its own collider behind, and a ray cast at where it is
  drawn comes back clear. A suite that needs one aeroplane's ray to meet another flies the pair back
  to the pose they spawned at (`campaign-bomber-crash-probe`), or drops the cast and measures the
  geometry on the hulls through the node transform (`AircraftBody.SegmentDistance`).
- **INSTR-14**, **Every automated session check runs on a PARENT-DRIVEN clock, so verify the shared
  step owner rather than either clock adapter alone.** `--det`, implied by `--run-tests` and
  `--screenshot=`, makes `GameClock.ParentDriven` true: E11's wave sequencer lived only in that path
  and waves 2 to 4 never arrived at the controls while every scripted check stayed green.
- **INSTR-15**, **Report a collider-swap census by direction, never as a signed sum.** A death both
  disables the healthy collider and enables wreck colliders, so a net count can read positive while
  the real removal happened; count OFF and ON separately (`Probes.EnabledColliders`).
- **INSTR-16**, **A capture taken on the very first rendered frame can beat the first per-frame
  publish.** `EffectAmbience`'s camera pose is written once per frame by `WeatherRig.Tick`; an
  emitter that draws before that call sees `HasCamera` still false and renders one frame unfaded,
  which is not evidence the distance fade is broken.
- **INSTR-17**, **A high per-site violation count on a dominant `PerfSample` site is not proof the
  instrument is broken; cross-site nesting is routine.** A death's event dispatch legitimately
  reaches the effect and audio sites, so the outer record absorbs the inner ones' cost by
  construction: read it as "more happened here than the named sites show", not as a defect.
- **INSTR-18**, **A flight-model probe placed above 2000 m is flying in the thin atmosphere band,
  where lift is 16.73× and thrust 22.0× smaller, so every aerodynamic number it reports is the wrong
  regime's.** Nothing clamps altitude, so the probe does not teleport and the run looks plausible; it
  simply cannot hold a load factor of 1 on any airframe but the autogyro. Start below the edge.
- **INSTR-19**, **The flight-dump hash agrees across the Godot runtime and the `dotnet test` host
  only because the print is rounded past where they diverge; prefer a same-host A/B, and treat a
  cross-host mismatch as a rounding boundary before a plant change.** The two runtimes' knife-edge
  values differ by 1e-4 to 3e-2, and the dump's Δalt reads to the nearest 10 m.
- **INSTR-20**, **A negative test that also steps the mission script can arm the very gate it is
  asserting stays shut; drive only the subsystem under test through the control leg.** C3/M01's drop
  cones sit 9 m from the `shipwreck` the first primary approaches, so `landings-approach-trigger`'s
  6 s flight completed that objective and its nap chain armed the trigger mid-leg.
- **INSTR-21**, **An animation that completes instantly satisfies every end-state assertion, so
  assert the episode's DURATION too, and build the world the way the session that runs it does.**
  With `camera1` unbound, C3/M01's drop reported `ANIM_STATE` EXECUTED and completed its gated
  primary in the frame it started; bound, the same episode runs 4.43 s.
- **INSTR-22**, **An aircraft that sinks below `FlightController`'s under-map backstop is teleported
  to its spawn with no crash flag, so gate a flown leg on an altitude floor.** The backstop fires at
  y = 0, sets no `Crashed` and writes only a rate-limited `under-map backstop:` line: two such jumps
  are the whole of `wingman-station`'s 3797 m worst separation on a leg that plateaued at 254 m.
- **INSTR-24**, **A raycast taken in the same call that moved a static body reads the collider at
  its OLD pose and reports empty space as "nothing there"; call `ForceUpdateTransform()` over the
  moved subtree first.** Placing C3/M01's `cargozep1` at its authored pose and immediately casting
  180 rays at `hydrogentank1` returned 108 clean misses through 140 live collider bodies.
- **INSTR-25**, **`wingman-station` builds its own pair at 1200 m over a stage with no terrain, so
  spawn jitter and the `avoid crash` climb-out cannot reach it; measure those in a `--campaign=`
  run.** Its flown leg reads mean 270 m while the same pair over Hawaii at 150 m breaks off nine
  seconds in and ends 128 m above the player and 302 m behind.
- **INSTR-26**, **Model realtime at the clock-adapter boundary, not by giving each consumer a
  private callback again.** Install a Realtime `GameClock`, confirm it is not `ParentDriven`, and
  request one `SessionSimulation.Step` with the physics delta; a consumer's own callback missing the
  hold measured 4809.9 m of wingman drift.
- **INSTR-27**, **On a realtime leg a stand-in flies, so read the STATE under test, never a presence
  flag that its flying can also answer.** `FlightController.InPlay` folds `Inert` together with
  `Crashed`, so a stand-in flown into the sea reports "out of the world" exactly as a correct hide
  does: on CM02's capture the un-fixed build read `in-play=False` and passed.
- **INSTR-28**, **A `--screenshot=` on a Realtime (`--no-det`) clock cannot pick its `--frames=` to
  land on a transient live state; prefer a log line, or drive the state through a suite's own
  `_Process` loop (INSTR-26).** On a `--campaign=` run the intro cutscene's handoff landed anywhere
  from t=3.7 s to t=40.2 s across identical launches, caught on one run in nine.
- **INSTR-30**, **A world-coordinate precision effect cannot be measured at heading 0, nor with a
  luminance centroid: fly it, rotate it, take consecutive frames, and keep a control region that
  shares the transform but carries no drive.** A pinned heading-0 nudge read 0.03 to 0.12 px at the
  origin and 10 km, where a flown `--det --shots=4` phase correlation read 0.7 px at 10 km.
- **INSTR-31**, **A cache or fixture that hands a suite the WRONG subject is invisible to any
  assertion that holds on both subjects; prove the key with an identity test, never with the
  catalog.** Keying `DecodeCache`'s chapter document on its file name (every chapter's is
  `gamez.zip`) resolved all eight chapters to C1 while `chapter-census` still reported PASS.
- **INSTR-32**, **A `--campaign=` probe's first seconds of sim belong to the intro cutscene, which
  holds the mission clock, so give a campaign probe a minute of sim or read the `cutscene:` lines
  first.** On C5/M04 with `--wake-generators` a 900-frame run logged the credit and nothing after it,
  while 3600 frames logged the door, the booking and the drop.
- **INSTR-33**, **A smoothness complaint about a sim-driven object cannot be reproduced under
  `--det`, which every `--shots=` and `--screenshot=` capture implies, so read `fps` against
  `phys_hz` on a `--no-det` run first.** `FixedStep` pins the render/sim ratio at 1 and skips
  `FlightController`'s interpolation: CM12 logged `phys_hz` 59.99 against `fps` 97.9 when realtime.
- **INSTR-35**, **A player-piloted rig cannot stand in for an AI aircraft, because the plant is
  selected on range to the nearest human; drive an AI rig with `AiPilot.HoldingCourse` and assert
  `FarFieldPlant` reads the branch you meant.** Three player-rig probes at CM12's ace's authored pose
  descended 150 m to 16 m, where the ace 3.9 km from the player does not sink at all.
- **INSTR-36**, **A golden that moves on your branch is not yours until you have run it without your
  change, which costs one `git worktree add --detach <merge-commit>` and one golden stage.**
  `campaign-intro-fill` moved to `be23e13d…` under a render-interpolation change provably inert in
  `--det`, and a detached worktree at the merge commit produced the same hash without it.
- **INSTR-37**, **`Node3D.Scale` does not read back axis for axis off a basis carrying real
  rotation, so compare sorted magnitudes or the whole basis, and never assert
  `Scale.X == authored.X` on anything that also rotates.** An authored `(1, 0.25, 1)` on a rotated
  hook arm reads back as `(1, 1, 0.25)` on every airframe `landings-hookup-airframe` drives.
- **INSTR-38**, **A fault that needs a hash collision fires on a minority of runs, so re-running a
  suite measures the collision rate and not the fault; assert the stale state directly.** In
  `landings-hookup-airframe` the `landings` filter threw `ObjectDisposedException` on 1 run in 14
  while the same drive left 134 to 562 stale rows every time (`AnimRuntime.FreedNodeRows`).
- **INSTR-39**, **A "did it move" distance is scored by the defect, so it passes hardest on the
  worst behaviour: read the DISPATCH for "did the event fire" and the RESTING POSITION for "did it
  end in the right place".** `zeppelin-breakup`'s 1 m threshold passed four gasbags falling 1,228 m
  through the sea, and fails a gasbag that stops on the water at 0.5 m.
- **INSTR-40**, **A self-test whose rows all call helpers proves nothing about the entry point the
  caller actually uses; drive the entry point.** `CheckCommitContent.ps1 -Root <tree>` exited 2 on a
  real comment-cap violation while `-Command 'git -C <same tree> commit …'`, the shape the hook
  passes, exited 0 before any check ran, with all 17 self-test rows passing throughout.
- **INSTR-41**, **A `--weapon-lab --weapon-fire` run with no target parks the plane level, so a
  rocket expires at its authored `RANGE` in mid-air and the probe films the AIR burst.** Aim it with
  `--weapon-surface=<SurfaceRegistry name>` plus `--weapon-standoff=<m>`, an unknown name being a
  WARN and an ignored flag, and confirm the `impact:` line names a real surface node.
- **INSTR-42**, **A golden that produces NO image failed differently from one whose hash moved: read
  `broken` as a tooling fault and `moved` as a content change, and open the preserved log first.**
  Two shots hit the 300 s ceiling logging `Cannot instantiate C# script … Launcher.cs` because the
  assembly was rebuilt under the running battery; `.scratch/goldens-failures/<stamp>/` holds the log.
- **INSTR-43**, **Read the staged `player` marker's pose from the last playing frame of a cutscene,
  not from the frame after the handoff.** The restore parks the marker back at the world origin, so
  a suite comparing the handed-back aeroplane against it one frame late measures against the origin.
- **INSTR-44**, **`--freecam` starts at the mission spawn, which is RANDOM per launch: pin `--pos`
  and `--direction` on any run a later run is compared against.** The spectator camera takes the
  same spawn the mission places the player at, so two otherwise identical captures frame different
  scenery.
- **INSTR-45**, **A screenshot proves nothing about audio: read the `sound` log's pairing
  instead, one line per aircraft at build naming what each slot resolved to, then one per cull
  transition and one per damaged-engine swap.** The pair is what separates silent past the cull
  from silent because the definition never resolved.
- **INSTR-46**, **An unpacked developer tree cannot reproduce a zip-only asset bug: verify a
  release's asset shape with `--zip-assets`.** A directory-backed `SoundArchive` re-reads a path
  per lookup and cannot be closed under itself, while a zip-backed one holds a handle that can,
  which threw `ObjectDisposedException` out of `SessionSimulation.Step` for every sound the
  prewarm had missed.
- **INSTR-47**, **A body's net world displacement is zero for as long as something is holding it in
  place, so it cannot answer "is this body still travelling"; read the velocity of the frame it is
  solved in.** Held on the sea by its own column read, C2B/M04's `gasbag4` measured 0.01 m of world
  step a frame while the wreck carrying it fell 3.44 m a frame.

- **INSTR-48**, **A sweep that stops at the FIRST obstruction it finds tests the geometry nearest
  the instrument, not the geometry the report is about; choose the bearing by how much of the
  subject the segment passes through.** `turret-hull-blocks-own-fire`'s first-hit sweep read 14 of
  17 `piratezep` rings as blocked at 35 to 55 m panel edges, while the same rings' deepest in-arc
  bearings, crossing 162 to 255 m of the same hull, fired straight through it.
- **INSTR-49**, **A synchronous in-engine suite reads `GameClock.Current.Time` as a CONSTANT, so
  anything behind a time-expiring cache runs its first verdict for the whole leg; install a clock
  and call `BeginFrame(dt)` per step, then assert the elapsed game time.** Without one, an 8 s
  turret leg spanning "several of the 1-2 s line-of-sight cache windows" was one cast per ring.
- **INSTR-50**, **A collider that a simulation step moved trails its node by that step's motion
  even in a live session, because Godot flushes transform notifications once a frame; read the lag
  off `PhysicsServer3D.BodyGetState`, never off a ray.** `piratezep` on its net measures 0.25 m,
  enough to slip a line-of-sight ray past a body grazed 6 m away and not enough to open a hull.
- **INSTR-51**, **A gate is only as good as its trigger, and the trigger belongs to the gate, not
  to each harness that calls it: a passing self-test says nothing about a harness that exits before
  the script runs.** Three `PreToolUse` copies of `git\s+commit` required the two words to be
  adjacent, so `git -C <tree> commit`, the form `CLAUDE.md` prescribes for naming a tree, skipped
  `CheckCommitContent.ps1` silently while all 21 of its rows passed; four comment-cap violations and
  an over-cap doc entry reached `main`. Drive the harness's own command text with a crafted payload
  against a fixture carrying a known fault.
- **INSTR-52**, **Reading the two writers you expected does not prove a property is never
  written: grep every assignment of the member before filing "it is never copied".** The cockpit
  pass's cloned sun takes no bearing in `CockpitOverlay.NewOverlay` or
  `WeatherRig.RegisterExtraLighting` and is aimed from the world sun in `CockpitOverlay.Sync`
  every frame, which a C1/IA1 cockpit shot proves by moving pixel md5 `50b5fcf1` to `274ed29c`
  when that third writer alone is cut.
- **INSTR-53**, **Stubbing out a MEMOISED lookup does not turn a fix off: the first call still
  fills the cache and every later one reads the answer back. Stub the cache fill as well, or the
  red check passes and pins nothing.** A zeppelin broadside whose world-node resolve was stubbed to
  return null still volleyed on that node, because the same call had already stored it.
- **INSTR-54**, **A collider switched on after the world was built is not in the physics broadphase
  inside a synchronous suite, which yields no frame for the server to flush: build a world the
  object already stands in rather than showing it and casting.** Showing `multiplayer1zep` left all
  six axis rays from every belly ring reading open air with 146 of its 196 shapes enabled.
- **INSTR-55**, **Time a lease from the event that renewed it, never from wherever the previous
  check ended: a step count started mid-lease measures the remainder and reads a working lease as an
  expired one.** The turret gun voice's 0.5 s lease was called dead at "0.4 s" because 0.25 s of
  continuity checks had already run since the shot; stepping to a fresh round first and timing from
  that frame put both edges where the decode says they are.
- **INSTR-56**, **One `GD.Print` on a code path makes that path untestable in `dotnet test`, and it
  fails as a crashed HOST rather than as a failing test: read the `.trx` before blaming the
  harness.** `TargetSelection.Rebuild`'s once-per-selector breadcrumb aborted the whole run with
  `System.AccessViolationException` inside `godotsharp_string_new_with_utf16_chars`, reported as
  "no test matched" with a stack dump and no failure count. A breadcrumb goes through `Log` at
  DEBUG, which writes no console line under the default threshold and therefore makes no engine
  call at all.

- **INSTR-60**, **Walk a menu screen to the row's own text or key, never by a counted number of
  cursor steps: a row added above it silently redirects every later press.** Two suites reached
  Built-in's Options apply row with four `Down` presses; four display rows landed those presses on
  the monitor stepper instead, so the walk stepped a setting and asserted on the wrong row. The
  screens expose their row text, so the walk can name what it is looking for.

- **INSTR-61**, **A pointer check on a scrolled list's chrome can be swallowed by its thumb: the
  thumb takes a click before any row is hit-tested, so an arrow standing under an oversized thumb
  rectangle does nothing and reads as a broken arrow.** A decal-grid case's down arrow never fired
  because the fixture's thumb art measures 128 pixels against a 68-pixel track and covered it;
  clamping a proportional thumb to its own track put the press back on the arrow.

- **INSTR-59**, **A positional-audio check must read the node the CULL measures from, not only
  whether the emitter is playing: an audible verdict passes while the two are different nodes, as
  long as the wrong one happens to stand near the listener.** `GunVoice` moved its child
  `AudioStreamPlayer3D` to the muzzle and left the node its own cull reads at its parent's
  position; every turret assertion passed, and the split only showed when a surface hull hung the
  same component on the world sound node kilometres from where it was firing and came out silent.

- **INSTR-58**, **Compare two runs of a cutscene on a node that episode alone drives, never on
  `camera1`: the cutscene camera is one shared node, and a definition outside the episode takes it
  over on its own schedule.** Reading CM02's capture at 1x and at a held 4x off `camera1` showed
  260 m of apparent drift over a 558 m shot, all of it `gi_scene2` claiming the node 15.9 real
  seconds in, which the faster leg never reached; the wing walk's own `wingwalk_parent` reads 0.2 m
  of drift over 577 m across the same pair.

- **INSTR-57**, **`FlightController` overrides no `_PhysicsProcess`, so a suite stepping a rig with
  it advances the CAMERA and nothing else; the simulation step is `SimStep`.** A flyby suite driven
  that way read the aeroplane 31 m from where it started after four seconds at 100 m/s, and the
  camera it was measuring looked correct against a subject that had never flown.

## SRC, sources and documents

- **SRC-3**, **Use design documents for intent; retail evidence decides shipped details.**
- **SRC-5**, **A field you don't read may be REDUNDANT, not dropped; try to derive it from the
  fields you already read before deciding what it means.** All 51 `OBJECT_MOTION_FROM_TO` `*_delta`
  vectors are exactly `(to − from) / run_time` of the sibling channel already implemented, worst
  residual 4e-6.
- **SRC-6**, **A test the binary computes is not a rule until you find what reads its result.** The
  take-hit body `FUN_004b9bc0` computes "shooter and victim are on the same team, or either is
  neutral" into a stack byte, and its only read passes it to `FUN_0042e840` as an argument that
  function never touches, so the original applies friendly damage (`org/vehicleDamage.md`).
- **SRC-7**, **A key the data authors is not a feature until you find the parser that reads it;
  grep the executable for the key string before modelling it.** `ia.json` authors `enemy_skill` in
  all 8 chapters and no such string exists in `crimson.exe`, so every file-launched wave takes the
  built-in default (`formats/turrets.md`'s unread `HEALTH` is the same shape).
- **SRC-8**, **A branch's effect is only half its meaning; the other half is what it is an
  alternative to, so record the jump it skips.** `FUN_0045b9d0`'s mission-type-2 arm was read as one
  contribution topping up a generator's capacity, while the discriminator at `0x0045ba9b` is an
  `if`/`else` whose other arm is the entire wave teleport (`formats/instant-action.md`).
- **SRC-9**, **Searching a function for a flag's bitmask does not prove the flag is not tested
  there; confirm a negative by reading the control flow around the site.** `FUN_004b6820` computes
  `(weaponFlags >> 6) & 1` once at the top of each station loop and keeps it in a register, so
  searching it for the immediate `0x40` reads as an unfiltered fire counter.
- **SRC-10**, **A stream's own start timestamp is not an offset between streams; read both and
  subtract.** Nine of the ten cinemas carry the identical presentation timestamp on their first
  video and first audio packet, and only `msopen1.mpg` differs, its sound starting 0.0667 s before
  its picture (`formats/cinemas.md`).
- **SRC-11**, **A name in the data is a label, not a specification: what the feature IS comes from
  the one site that reads it.** `player.json`'s `warning_shot_*` block and its `bullet_warning_sg`
  read as a near-miss rating and were built as one, geometry and all; the single read of the handle
  (`0x004b9ea9`) is an arm of the damage routine that zeroes the incoming damage pair, so the block
  is a shield on the player and the cue says a round was absorbed. Before building a feature on a
  block of constants, find every reader of every field: here all four had three sites between them
  (`org/weaponFire.md`).
- **SRC-12**, **Which body axis a matrix row holds is a claim to re-derive at a point of use, not
  to take from another page; a wrong row sign reverses a ported direction without failing
  anything.** Row 2 of the orientation matrix at `plane+0x180` is **minus** the nose, proved where
  the thrust magnitude is negated before being multiplied by it (`0x48fe91`, `org/flightModel.md`);
  read as the nose, the shadow skew that `org/shadows.md` decoded came out backwards, putting the
  player's own shadow behind the chase camera instead of ahead of the aircraft where it is drawn.
- **SRC-13**, **A decode row about a field is a decode of the field, not of the path that writes
  it. Read on past the write before treating the field as the whole behaviour.**
  `org/aiControlLaw.md`'s row for the AI evade flag at `obj+0xBA` recorded the set, the alignment
  clear and the two suppressions, every one of them correct, and stopped three instructions short of
  the same handler's call into the maneuver picker at `0x004b9ff8`, which writes the mode. Read on
  its own the row supported "the original only sets a flag", the opposite of what the handler does,
  and an entry was filed against the remake on the strength of it. A row that names only a field's
  writers earns a sentence saying what else that writer does.
- **SRC-14**, **A census over the shipped files counts records, not live things; resolve the record
  in the world that reads it before saying a mode offers it.** Six `IA1` `targets.zrd` files flag
  `ap_transmitter` `other_target`, which reads as six chapters whose Instant Action carries a radio
  tower target. Only C1's gamez has a node of that name, so the other five flag nothing their own
  world builds, and the same census's sixteen `MP3` rearm bases do resolve in all eight
  (`org/targeting.md`). The check is one grep of each world's `nodes.json`, and it separates a data
  author's copied record from a feature the player can reach.
- **SRC-15**, **A rule inferred from the artwork can agree with the original on the shipped data
  and still be the wrong rule; find the field the engine reads before believing a threshold that
  separates cleanly.** The puffer blend verdict thresholded the dying sprite's alpha-weighted
  luminance at `16/255`, a population that separates with nothing between 0.018 and 0.12, and it
  reached the right answer for the case it was built on; the engine reads bit 2 of the texture
  header's render-flags word, which no puffer sprite in the install carries
  (`org/textures.md`).
- **SRC-16**, **A named rectangle in a dialog primitive says nothing about whether it crops the
  source or clips the output; settle it by aligning a reference still, not by reading the name.**
  The pause screen's `MAP` primitive authors `POSITION [16,19]` and `CLIP [211,51]..[784,551]`, and
  the two readings put the map 195 pixels apart. Sampling a 462-point grid of the filmed still
  against the extracted sheet gave a mean per-pixel channel-sum distance of 7.8 for the source crop
  and 180.0 for the screen clip, which is not a close call in either direction
  (`org/pause-screen.md`).
- **SRC-17**, **Two definition files keyed alike are not copies of each other; compare the bodies
  before reading one screen's content out of the other's file.** `Loading.zrd` and `escape.zrd`
  carry the same 24 `loading_c<world><mission>` keys and their `PRIMITIVES` blocks agree entry for
  entry, which is what "content-identical" was inferred from. Flattening and diffing every pair
  showed every `LOADING_SCRIPT` to be a superset of the matching `ESC_SCRIPT`: 1422 characters
  against 788 on `loading_c31`, and 20 authored waits against 7 across the set, the extra beats
  being the propeller cycle and the mission's device icons (`org/loading-screen.md`).

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
