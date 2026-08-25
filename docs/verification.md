# Verifying a change in this project

This file contains transferable verification rules. Dated evidence belongs in commit messages
(pre-2026-08-06: `docs/HISTORY.md`), analysis findings, or git history; module constraints belong
in `docs/architecture.md`.

Read **METHOD** first, then only the relevant section. Rule IDs are permanent: append new rules
and leave gaps when retiring old ones.

## METHOD — designing any measurement

- **METHOD-1** — **Choose a case that distinguishes the hypothesis from its alternatives.**
- **METHOD-2** — **Measure same-build variation before treating an A/B difference as signal.**
- **METHOD-3** — **Re-measure the baseline near the changed run.**
- **METHOD-5** — **Change one variable at a time and keep the baseline reproducible.**
- **METHOD-6** — **Prove which binary and inputs each side of an A/B used.**
- **METHOD-7** — **Verify independent branches before their combination.**
- **METHOD-8** — **Reproduce every “pre-existing” failure on the unchanged build.**
- **METHOD-9** — **Show that verification can fail under a deliberate perturbation.**
- **METHOD-10** — **A check that passes a no-op does not verify the change.**
- **METHOD-11** — **Sweep representative poses, times, or inputs.**
- **METHOD-12** — **State which measurements should change and which should remain invariant.**
- **METHOD-13** — **Place the subject explicitly with `--pos`, `--direction`, or `--lookat`.**
- **METHOD-14** — **Test reconciliation metrics for degeneracy.**
- **METHOD-15** — **Confirm the intervention took effect before crediting it.**
- **METHOD-16** — **After restoring inputs, force or verify the rebuild.**
- **METHOD-17** — **Use `git diff` to prove temporary edits are restored.**
- **METHOD-19** — **Disable later mechanisms that would repair a deliberately restored fault.**
- **METHOD-20** — **Reproduce a published measurement's pose *and* its statistic, not just its subject.** A number is only comparable under the conditions it was taken in: `CAP-13`'s ring brightnesses were read off frames that already carried the sun wash, which composites toward white and scales every difference by (1−α), and its script reports the *brightest pixel* in a window where a median around the annulus reads systematically lower. Matching the subject but not the pose or the estimator produces a confident calibration that is wrong in a direction nothing reveals (BL-165, `analysis/bl-165-lens-flare/`).
- **METHOD-21** — **A scenario entered at a speed it cannot hold is not a measurement at that speed.** The original's 143 mph knife-edge take, replayed at full throttle, accelerates past 290 mph inside three seconds and reports the 300 mph take's numbers under the 143 mph label — the instrument manufactures its own operating point. `Probes.KnifeEdge` bisects a level-flight trim throttle for the entry speed instead; hold the *condition* the capture was flown in, not just its initial value.
- **METHOD-22** — **Re-measure a fitted constant ALONE once the mechanisms it was fitted on top of have been replaced — it may now push the wrong way.** `ClimbGravityScale = 0.6` was fitted to make a climb hold speed, on drag and thrust shapes that `B12`/`B13` later replaced. Removing it *by itself*, with nothing else changed, moved the sustained climb from 276.7 to 257.7 mph **toward** the original's 163.1: by then it was making the manoeuvre it existed for worse. A constant that survives a rewrite because it was never re-tested is indistinguishable from one that is still doing its job, and only its own single-variable ablation separates them.
- **METHOD-23** — **"Unreachable" is a measurement, not an inference from the constants around it.** The original's G limiter starts where the lift clamp ends (9 G against a ±5/9 clamp), so the structural argument proves only that the reduction is zero *at the boundary* — it says nothing about the margin. Flying it says: peak demand is 2.13–5.01 G across the eleven airframes, and the Bloodhawk's 5.01 sits 0.2 % **past** the executable's own fallback threshold of 5. Two constants that look far apart can be one hundredth of a unit apart in the quantity that matters, so pin the margin with an instrument and assert it against the loaded value, never against the literal (`ControlLimiterTests`).
- **METHOD-24** — **Fit a trend and a periodic signal simultaneously; never detrend first and fit
  the residual.** A sliding high-pass filter has real gain at the signal's own frequency: on the
  cadence-sweep data it moved the 930 ms amplitude by 45% as the filter's span changed. A
  polynomial and a sinusoid fitted together, inside a window of at least eight periods, leaves a
  cubic almost nothing of the fundamental to absorb (`ZzCadenceSweep`).
- **METHOD-25** — **Never equate a measured OUTPUT of an oscillator with its INPUT amplitude
  (a rendered RMS is not a kick).** The gun-wobble law `magnitude 7e-5×caliber = 2.80e-3 rad` was
derived by setting it equal to the clip's rendered RMS — but a kick of envelope `E` renders only
~0.28·E as RMS at the real 8/s fire rate (sawtooth duty × damp-envelope decay), so the engine
renders ~0.28× the law's literal number *by construction* and this was misread as a render-pipeline
loss. What the engine renders was decodable from the authored constants + oscillator math alone
(~0.20 px/frame) with no clip; the clip only settles the later fidelity target. A fitted constant
  is only interpretable if it names the quantity it multiplies at the right point in the chain
  (`BL-266(a)`; `analysis/gun-wobble-shake/`).
- **METHOD-26** — **Carry a decoded representation through its consumer before naming its physical
  quantity.** `obj+0x16c` looked like angular velocity, but `FUN_0053fbf0` consumes it as a
  quaternion half-angle: the matrix turns by twice the stored vector. Comparing the accumulator
  alone left pitch, yaw and roll exactly half-strength while every local value appeared correct.

## DIAG — chasing a symptom

- **DIAG-1** — **Check the premise against data before writing code.**
- **DIAG-4** — **Confirm fidelity defects against the original.**
- **DIAG-5** — **When a metric worsens, inspect the artifact and new states.**
- **DIAG-6** — **Correlation is not a mechanism.**
- **DIAG-7** — **Isolate the smallest subject and vary only the suspected mechanism.**
- **DIAG-8** — **Audit inherited claims back to their inputs and method.**
- **DIAG-9** — **Do not call a residual a floor until controls cannot reduce it.**
- **DIAG-10** — **Explain cases where the change is inert by construction.**
- **DIAG-11** — **Identify what moved before calling a deviation a regression.**
- **DIAG-12** — **Instrument the boundary between externally identical failures.**
- **DIAG-13** — **Visible activity proves execution, not correctness.**
- **DIAG-14** — **Read probe failures before modifying the probe.**
- **DIAG-15** — **Never silently skip unsupported or failed cases.**
- **DIAG-16** — **Prevent unrelated lifecycle events from clearing the effect.**
- **DIAG-17** — **Distinguish “never reached” from “reached but invisible.”**
- **DIAG-18** — **Test code-derived explanations at runtime.**
- **DIAG-19** — **Scripted repros inherit identity defaults (-Z heading, near-origin spawn); a
  pose-dependent symptom needs an off-axis heading AND a far-from-origin placement in the matrix,
  and a repro that works where the report fails means the poses differ, not that the report is wrong.**
- **DIAG-20** — **An effect that never starts passes every "it ends correctly" check — prove the
  START from the production call path before diagnosing the stop.** A suite that `Start`s the def
  directly verifies the halt but cannot see a call site that never dispatches, and "the fire ended"
  reads identically whether the stop fired or the fire never began. Measured: `large_30sec_fire`'s
  ~1,035 death calls sat in the unparsed `unknown_seq` block for the project's whole life while the
  direct-Start `stop-sequence` suite and two cockpit passes all read the absence as a working stop
  (`BL-276`).
- **DIAG-21** — **A puffer's particle spread is unseeded RNG, not the pinned run seed.** Two
  captures at the same step can differ in particle placement alone; do not read that difference as
  a behaviour change (the anim lab's own determinism boundary).

## SHOT — screenshots and pixel evidence

- **SHOT-1** — **Respect capture quantisation; tiny effects need another metric.**
- **SHOT-3** — **Use state logs when pixels cannot resolve an effect.**
- **SHOT-4** — **Remove occlusion, fog, lighting, and competing effects.**
- **SHOT-5** — **Compare pixel values, not enlarged impressions.**
- **SHOT-6** — **Compare decoded pixels, not encoded image bytes.**
- **SHOT-7** — **Tree visibility is not proof that pixels rendered.**
- **SHOT-9** — **Do not combine screenshots with `--headless`.**
- **SHOT-10** — **Create the output directory, use an absolute path, and verify the screenshot exists.**
- **SHOT-12** — **Frame the time-driven surface, then perturb time.**
- **SHOT-13** — **Use `--tex-override` for one texture and `--tex-census` to locate candidates.**
- **SHOT-14** — **Treat census counts as lower bounds; disable fog and reject tiny counts.**
- **SHOT-16** — **Hide capture windows; never minimize them.**
- **SHOT-17** — **Sample transitions mid-ramp.**
- **SHOT-19** — **Use a time series and isolate overlapping emitters.**
- **SHOT-20** — **Locate a saturated feature by the centroid of its plateau, not by `argmax`.** On a clipped highlight `argmax` returns wherever the tie happens to break — 16 px off the sun's centre in C3 — and any position derived from it inherits that error multiplied. Ring 2.0 of the lens flare sits at twice the sun→centre vector, so it inherited double and measured as pure sky (BL-165).
- **SHOT-21** — **Keep radial probes clear of the frame edge.** A circular sample that runs off-frame reads clamped pixels as if they were content, so a thin annulus averages to background and reports a confident zero. Place the subject diagonally when the probe radius would otherwise cross an edge (BL-165).
- **SHOT-22** — **A full-screen effect needs an off switch before it contaminates unrelated captures.** The lens flare's wash reaches α ≈ 0.66, survives terrain occlusion and whitens the HUD, so in C2/C3 every screenshot with the sun near centre becomes useless for judging terrain colour, fog gradient, deck brightness or clutter density. `--no-flare` exists for the same reason `--no-fog` does (BL-165).
- **SHOT-23** — **Measure how far fog lets a texture survive as a PLATEAU-RELATIVE high-pass, and
  compare it in ELEVATION ABOVE THE TRUE HORIZON — never in rows from the top of frame.** Both
  halves bit at C1's river pose. (a) Fog scales a surface's texture
  contrast by `1 − φ` while leaving a smooth gradient behind, so a per-row sd measures the gradient
  and an *absolute* contrast threshold measures the texture's own contrast rather than the fog: the
  original's overcast mottling runs at high-pass RMS ≈ 0.59 and ours at ≈ 0.26, so only a threshold
  set as a fraction of each image's own unfogged plateau compares the same thing. (b) Two frames of
  "the same pose" routinely differ in pitch and aspect — the original is level at 1250×713 with its
  terrain 41 px *below* the true horizon, our matched render pitches 5.7° down at 1280×720 with
  terrain rising 17 px *above* it — so a depth-from-top row number silently compares different
  angles and can name a target the pose cannot reach whatever the change does. Convert to elevation
  (and, where a ceiling's `f·h` is known, to a distance in metres) before drawing a conclusion.
- **SHOT-24** — **Measure cloud-sheet structure by the sky→tops transition depth, not
  column-autocorrelation or crest-spacing.** Perspective makes a fixed world-space placement
  period aperiodic in screen space, so a row/column ACF over the sheet found no peak above 0.33
  on either side of the A/B — including a 0.69 peak in the original's own underside that turned
  out to be capture noise at sd 0.91–2.58 (METHOD-14). *(Minted as
  SHOT-20 on the plan's branch; renumbered at the 2026-08-09 merge — BL-165's session minted
  SHOT-20/21/22 first.)*
- **SHOT-25** — **`--tex-override` cannot separate the `fvol` cloud-sprite field from
  `cloudparent` clusters — they share their textures.** All 626 of C1's `cloudparent` facades are
  skinned `cloud1.tif`/`cloud2.tif`, the same two textures the `cloudsprite1`/`cloudsprite2`
  templates use, so a green override paints both populations at once; separate them by altitude
  or cluster position instead. *(Branch-minted as SHOT-21; renumbered
  at the merge.)*
- **SHOT-26** — **A horizon-band artifact is a full-width, DEAD-FLAT run of rows (per-row sd ≈ 0)
  bounded by a hard jump — measure it as the largest jump whose rows are BOTH flat, never as the
  largest jump.** Unrestricted, terrain silhouettes and cloud edges dominate the statistic and a
  55-luminance flat-band edge reads as ordinary scene contrast.
  *(Branch-minted as SHOT-22; renumbered at the merge.)*

- **SHOT-27** — **The true horizon is a CALIBRATED row, not the sky/terrain boundary — shoot the
  same position LEVEL and check the shift is `f·tan(pitch)`.** `SHOT-23`(b) says to convert a row
  into an elevation above the true horizon; this is how that row is obtained without guessing.
  Measured: the C1 river pose pitches 5.712° down at
  `f` = 599.1 px (fov_y 62° over 720 rows), so its horizon is row **419.9** — and the same
  position shot level puts every feature exactly **60 px** higher, against the predicted 59.9.
  An earlier reading took the `--no-fog` control's sky/terrain boundary (row 299)
  as "17 px above the horizon" and worked from ~316, ~104 px off, which silently scaled every
  elevation and every `f·h` distance derived from one. The boundary is the terrain SILHOUETTE, and
  a silhouette can sit either side of the horizon: ours rises 115 px above it at that pose while
  the original still's ridge sits 48 px below its own. When two frames' skies do not overlap in
  elevation, anchor the statistic on the silhouette both frames actually have.
- **SHOT-28** — **A nadir shot cannot tell a painted rooftop from an extruded building, and a global
  instance count cannot tell you where the instances went — a placement claim needs a low oblique.**
  Directly overhead, a ground texture depicting city blocks and the 3D blocks standing on it are the
  same pixels, so "the streets are clear" reads identically whether the placement improved or the
  buildings vanished. A per-kind census fails the same way from the other side: placement changes
  **relocate** a population as well as thin it, so the totals can barely move while a whole
  viewpoint empties. Measured on C5's clutter placement, against the `no_clutter` decode:
  gating clutter on polygon bit `0x800` made C5's crossroads look markedly closer to `CAP-22`'s
  original at nadir, moved the pinned frame hash, and passed the interpenetration check; the
  per-kind counts said the city was *intact*, with `cb00a` at 79 % and `cb12a` at 96 % of baseline
  and nothing at zero; and a 180 m oblique over the same crossroads showed the near-field skyline
  completely gone, flat painted ground to the horizon, even brightened 3.2×. Three instruments, two
  of them reassuring, one of them right. The nadir pose was the founding evidence of the bug, which
  is exactly why it was trusted alone.

- **SHOT-29** — **Before pinning a golden, measure how much of the frame the subject actually
  owns; a subject no scripted camera can approach does not earn one.** A shot is named for its
  subject but hashed over the whole frame, so a small subject makes a tripwire for everything
  else. Measured on an AI aircraft's damage stages: `--ai=` places the plane 250 m ahead of the
  chase camera and it outruns the player from there, so the same C1 pose with the AI staging its
  whole ladder (five `random_remote_damage` bursts and the `pfsmoketrail` heavy trail, visibly
  burning under 4× magnification) differs from the pristine control by **396 of 921,600 pixels,
  0.043 %**. A hash there would move on any render change and hold still through a total staging
  regression. The A/B against the undamaged control is the cheap check, and it answers both ways:
  it is also how a shot that IS worth pinning proves it.

- **SHOT-30** — **A headless AI shootdown is reachable, and it writes `DESTROYED …`, not
  `CRASH into …`.** `RunProbe.ps1 --chapter=C1 --plane=player_bhawk --ai=player_fury
  --ai-damage=0.02 --fire --frames=1200` kills the AI plane in about a second and logs
  `DESTROYED by gunfire (…) def=fury wreck=falling (flight model) lands=anim (bounce sequence)`.
  Grepping a run for `CRASH` alone reads a working shootdown as nothing having happened, because
  the two death stages are two different log lines and only the ground contact writes the second.

- **SHOT-31** — **`SHOT-29`'s verdict is about the SUBJECT, not about AI aircraft, so re-measure
  it for each one.** The same `--ai=` kill three seconds later, at the destroy def's handover,
  differs from an unkilled control by **236,925 of 921,600 pixels, 25.7 %** (17,438, 1.9 %, past a
  channel delta of 16), in a readable 215 × 206 px fireball; the shot repeats bit for bit across
  runs and is 54.5 % frame-sensitive. A pristine AI aircraft's staged damage ladder moved 0.043 %
  of the same frame. Same camera, same flags, 600× the signal: what fails a golden is the size of
  the subject in frame, and a burning wreck is a different subject from the plane that was flying.

## GOLD — golden images

- **GOLD-1** — **Update moved hashes with the visual change, and explain each moved shot in the
  commit message.** The explanation is history and belongs in the git log; `manifest.json`'s
  `exercises` field says what a shot covers *today* — do not append a re-pin note to it.
- **GOLD-2** — **A golden is a tripwire, not a diagnosis.**
- **GOLD-3** — **For render-path changes, sweep every golden and inspect the largest movers.**
- **GOLD-4** — **Reproduce a golden with its OWN `frame` count, or the A/B is meaningless.** The
  runner appends each shot's manifest `frame` field to its args; an ad-hoc `RunProbe.ps1` repro
  that omits it renders a different sim frame, and two shots of the same flight seconds apart
  differ everywhere — it reads as a catastrophic regression when nothing is wrong.
- **GOLD-5** — **Which goldens move is itself evidence — check the pattern, not just the count.**
  A change that should touch one subsystem should move exactly the shots exercising it and no
  others. Per-plane chase distance moved all four flown-aircraft shots and none of the nine
  without an aircraft, which localises the change far better than any single image diff.
- **GOLD-6** — **A pure TIMING change moves pixels, and a particle shot amplifies it without
  limit.** Nothing appears, disappears or ends up elsewhere, so "behaviour-neutral" feels safe —
  but a capture is one instant, and an emitter's output is an integral over frames, so shifting a
  start by one tick shifts every particle in the frame. Measured on the `CALL_SEQUENCE` dispatch
  timing (`analysis/bl-135-callsequence-lag/FINDINGS.md`): moving every called sequence's first
  event 1/60 s earlier left 7 of 8 chapter captures bit-identical and every runtime total
  unchanged, yet moved `c1-crash` by **79.7 % of its pixels** — the crash fireball covers the
  frame. So judge a timing change by what the moved shots *are* (every mover was a particle shot)
  before concluding either that it broke something or that it is harmless. The same shot is also
  the size lesson: the rule that actually matches the original shifts a subset of those calls and
  moves `c1-crash` by **1.5 %**, so the magnitude measures how MUCH timing moved, never whether the
  change was right.
- **GOLD-8** — **When an earlier item deliberately left goldens un-repinned, a later item's "moved"
  list is about BOTH changes — recover the current item's own movers by A/B-ing hashes against a
  temporarily reverted build.** Measured: the run reported the same 9
  movers the previous item had, yet only 8 moved for this one — `c5-city-night` was byte-identical
  across it, and the census predicting exactly that would have been credited to the wrong change.
- **GOLD-9** — **"13/13 hash-identical" proves nothing until you have checked `manifest.json` is
  unmodified in the working tree.** The stage compares against the file on disk, not against HEAD,
  so a `-RegenGoldens` left behind by an earlier agent (or an earlier attempt at the same item)
  silently re-baselines the tripwire onto the very build under test — the run then reports a clean
  PASS for a change that moved six shots. Measured: a prior
  session's regeneration was already in the tree, three separate full runs reported 13/13 identical,
  and `git diff -- analysis/goldens/manifest.json` showed six hashes had in fact moved. **Run that
  `git diff` before believing a golden PASS**, and when you need genuine before-images, reproduce
  HEAD's own hashes from a temporarily neutralised build and check they match the committed ones
  digit for digit — a before-image that does not reproduce HEAD's hash is not a before-image.
- **GOLD-10** — **Hash the raw pixel buffer, never the saved PNG.** Encoded bytes differ between
  two byte-identical images — all 881 of C1's texture PNGs do — so a PNG hash reports encoder
  state, not pixels.
- **GOLD-11** — **Do not move where a `Puffer` is constructed, or construct one on a capture
  path outside its normal init.** Each emitter draws one RNG seed off `Rng.Puffer` at
  construction, so construction order alone re-pins every puffer-bearing golden.
- **GOLD-7** — **A golden shot that exits nonzero with no PNG is retried once, with evidence kept
  either way.** `RunTests.ps1`'s `goldens` stage reuses `.scratch\goldens\` every run, so a silent
  exit-1 (`BL-039`: a `c1-flight` shot once built its world, rendered a frame, then died with no
  PNG, no exception, nothing in any log) left nothing behind — the next shot's launch overwrote its
  `.log`/`.out`/`.err` before anyone could look. The stage now re-runs that exact shot once with
  Godot's own `--verbose`, and copies every attempt's full `.log`/`.log.out`/`.log.err` (plus the
  PNG, if any) into a dated `.scratch\goldens-failures\<timestamp>\` folder with a `report.txt`
  line recording the exit code and the log's last line, before the retry can overwrite them. A
  shot that dies silently but recovers on retry still passes the stage (its `Add-Unchecked` line
  says so) — the point is evidence for the *next* silent death, not a stricter pass/fail. Confirmed
  live 2026-08-04: killing a shot's Godot process mid-flight produced `exit=-1 png=False` evidence
  in the failure folder and a clean `ok` on the `--verbose` retry.

## DET — determinism and randomness

- **DET-1** — **Do not pace a sampler with the clock being tested.**
- **DET-2** — **Disable live input during scripted runs.**
- **DET-4** — **Vary render cadence when testing clock independence.**
- **DET-5** — **Audit mutable state outside seeded generators.**
- **DET-6** — **Scripted probes imply `--det`; use `--no-det` for realtime behaviour.**
- **DET-7** — **Deterministic results must depend only on committed inputs.**
- **DET-8** — **`--det` ignores `config.json`; use committed or CLI inputs.**
- **DET-9** — **Keep pure baselines free of clocks, absolute paths, and machine state.**
- **DET-10** — **Fixed-step captures cannot reveal realtime cadence artifacts.**
- **DET-11** — **A rate decoded from video of the original is quoted in SIM seconds (k = 1.390);
  implementing the wall figure runs it 39% fast.** The two numbers look equally plausible in a
  constant and neither a build nor a golden can tell them apart — only a dwell/duration logged in
  sim time can. Two cues have been landed against the sim figure (`BL-184`'s 168.7 °/sim-s arrow
  sweep, `BL-148`'s 643 ms stall-lamp half-period), each carrying the wall figure beside it in the
  source so the next reader cannot re-derive the wrong one; `CamSmooth` 8 is a known wall-rate still
  awaiting the conversion.

- **DET-12** — **An angle measured off footage cannot confirm a decode; at best it ranks two
  readings, and it will happily rank a third one you have not thought of.** `CAP-16`'s wing-panel
  strip measured 20–30 °/s and was recorded as confirming `forward_rotation` as a total angle over
  `RUN_TIME`. The binary says the crash pieces do not turn at all,
  which that same 10–15° of apparent motion under a moving camera fits at least as well. One piece,
  near edge-on, is one axis of one sample.

- **DET-13** — **A per-instance draw sequence off a dedicated `Rng` stream pins its field's whole
  layout to the master seed.** Measured on the same `--det` pose across two runs: `Precipitation`'s
  `Rng.Precip` draw took C2B rain from 5.44% of pixels differing to 0.00%, C4 snow from 25.84% to
  0.00%.

## PERF — performance

- **PERF-1** — **Do not interpret `script_ms` as literal frame cost.**
- **PERF-2** — **Capped metrics are floors, not costs.**
- **PERF-3** — **Split broad timers before choosing what to optimize.**
- **PERF-4** — **Attribute cost with CPU/GPU evidence.**
- **PERF-5** — **Ignore differences below measured noise and an absolute floor.**
- **PERF-6** — **Discard warm-up effects and record cache state.**
- **PERF-7** — **Compare startup timings under identical cache conditions.**
- **PERF-8** — **Use the engine startup report, not whole-process time.**
- **PERF-9** — **Use two unchanged pairs for noise, then measure A/B back to back.**
- **PERF-10** — **Check exact workload counts before noisy timings.**
- **PERF-11** — **Use `--no-vsync` and metrics valid for the clock mode.**
- **PERF-12** — **A frame ordinal does not convert to wall time at an assumed refresh rate.**
  `HitchMonitor`'s grace window is milliseconds; a `--hitch-inject=` frame chosen assuming a 60 Hz
  cap can land well inside it on a faster box — measured on the dev machine, vsync's *actual*
  refresh is 120 Hz (~8.33 ms/frame, same as `--no-vsync` there), so frame 120 is ~1000 ms, half
  the default 2000 ms grace, and neither a 50 ms nor an 80 ms stall tripped until past ~frame 240.
- **PERF-13** — **A hitch count only compares across runs in the same vsync mode; per-frame cost
  transfers, frequency does not.** Vsync paces frames per wall second, which paces hitch frequency
  directly; it also pins the rolling median at the refresh interval, collapsing `HitchMonitor`'s
  relative trigger into a fixed threshold whose winner against the floor depends on the refresh
  rate, not the defaults alone — at the 60 Hz cap this plan's defaults assume,
  `medianMultiple × refresh_interval` is 66.7 ms, ABOVE the 40 ms floor, so the relative term
  decides there, not the floor (`HitchMonitor.cs`, PERF-12).
- **PERF-14** — **A build-time CLI preset can never trip `HitchMonitor`; verify with a live,
  post-grace event instead.** `--damage=`/`--destroy=`-style presets apply inside
  `Launcher.LaunchSession`'s build, always finished before `Rearm()` starts the grace window
  (`HitchMonitor.cs`, disproven). `--crash=<frame>` picked past grace
  (PERF-12) is a live, scriptable event that does trip it — measured: `--crash=300` under
  `--no-vsync` produced two real sidecar records, `part_detach` 48.4 ms and `effect_pool_miss`
  62.1 ms, both ~500 ms clear of the 2000 ms grace.
- **PERF-15** — **A flat-leaf sampler's "negligible" cost only holds outside per-particle loops; a
  scope placed inside one becomes the thing it measures.** `PerfSample`'s open+close costs ~60 ns
  (three runs, ~59–63 ns, zero allocation) — a third of a microsecond for six coarse scopes a
  frame, but the same 60 ns times a few thousand particle draws rivals the frame budget it exists
  to explain.
- **PERF-16** — **`StartupProfile`'s `boot` figure is engine start → build start, so on a
  launchscreen-driven rebuild it also holds however long the menu sat idle** — do not read it as
  pure engine overhead on a relaunch.
- **PERF-17** — **`StartupProfile`'s `rest` term is `build − Σ(phases)`, real uninstrumented work
  — on a probe run it also holds whatever the probe itself did before the build closed, not only
  build overhead.**

## LOG — logs, error censuses, and exit codes

- **LOG-1** — **An empty report may mean the mode did not build the feature.**
- **LOG-2** — **State the time, count, and lifecycle window before concluding from absence.**
- **LOG-3** — **Report unsupported data as an instrument limitation.**
- **LOG-4** — **Check that the log can express the state sought.**
- **LOG-5** — **Report caps and truncation; never infer absence from a shortened list.**
- **LOG-6** — **Inspect complete stdout, stderr, and engine logs.**
- **LOG-8** — **Run shader checks windowed; `--headless` skips the render path.**
- **LOG-9** — **Use ordering and lifecycle position before attributing an error.**
- **LOG-10** — **Error allowlists need narrow patterns, caps, and actual counts.**
- **LOG-11** — **Capture native engine errors outside C#.**
- **LOG-12** — **Automated instruments must return their verdict in the exit code.**
- **LOG-13** — **Do not overlap engine probes.**
- **LOG-14** — **Read every field in a multi-metric row.**
- **LOG-15** — **When an error lacks identity, log candidate state at the failure boundary.**
- **LOG-16** — **A census printed at the end of setup cannot report a runtime miss** 
- **LOG-17** — **In a worktree, `RunTests.ps1` exits 0 having run only the units.** A worktree has
  no `tools/godot` and no `extracted/` — both git-ignored — so the in-engine suites and the golden
  hashes skip silently and the exit code still says PASS. Set `$env:CSVM_DATA_ROOT="Z:\CSVM"`
  before the run, and confirm the printed suite and golden counts are non-zero; a green with no
  golden line is a green that measured nothing.

- **LOG-18** — **`--debug-anim`'s motion line prints a GLOBAL position and a LOCAL rotation.** Read
  its `rot` as world attitude and every driven node under a moving parent reads wrong; it is world
  attitude only for a placed template root, which the stage sets `TopLevel`. The parachute's line
  read `rot (0.7, -92.2, -9.3)` while its wreck read `(39.5, 19.6, -118.3)`, two frames that only
  looked comparable.

## WORLD — world data and runtime traps

- **WORLD-8** — **Resolve objects by source identity, not normalized node names.**
- **WORLD-9** — **Verify that the selected mode builds the product being measured.**
- **WORLD-10** — **Report opposing transitions separately; a net can hide both.**
- **WORLD-11** — **Match sampling time to lifecycle.**
- **WORLD-12** — **A started definition is not proof of output; verify its runtime product.**
- **WORLD-14** — **Measure bounds before unrelated runtime children expand them.**
- **WORLD-15** — **Establish each subtree’s coordinate frame before applying transforms.**
- **WORLD-19** — **Verify every output channel of a compound effect.**
- **WORLD-20** — **For rare classes, census first and aim at named geometry.**
- **WORLD-21** — **Route equivalent lookups through one resolver.**
- **WORLD-22** — **Use explicit subsystem state when hosts are hidden by design.**
- **WORLD-23** — **Range-test decoded fields and corroborate their units.**
- **WORLD-24** — **Read authored range and condition gates before placing a probe.**
- **WORLD-25** — **A registry total counts bindings, not coverage: a larger census can mean one definition claimed objects it does not describe.** 
- **WORLD-26** — **Anchor an effect to the object it decorates, not to a parameter that merely describes it.** C3 authors `SUNLIGHT_ORIENTATION` yaw 135 while its gamez `sun` node sits at yaw 45 — the parameter is the shading direction, not the object's position, and a flare anchored to it draws 90° from the visible sun. Anchoring to the object also survives any coordinate-conversion error, since the object and its decoration go through the same conversion (BL-165).

## SHELL — Windows, PowerShell, and processes

- **SHELL-2** — **Identify stray Godot processes by worktree and probe flag.**
- **SHELL-3** — **After bulk rewrites, run Godot as well as the compiler.**
- **SHELL-4** — **Detect and preserve BOM and encoding explicitly.**
- **SHELL-6** — **Judge piped native processes by exit code.**
- **SHELL-7** — **On PowerShell 5.1, read BOM-less UTF-8 through an explicit UTF-8 API.**
- **SHELL-10** — **Launch scripted Godot probes through `RunProbe.ps1`.**
- **SHELL-11** — **Assert exit codes, counts, and hashes are non-empty.**
- **SHELL-12** — **Give every scripted probe an exit condition, and check the flag you chose actually is one.** `--frames=N` is `ScreenshotFrames` (`SessionSpec.cs:586`) — a warm-up counter that terminates the run only alongside `--screenshot`. Passed on its own it reads as valid, changes nothing, and the probe runs until killed: one `--debug-anim` run left this way spent six hours writing a 45 MB log.
- **SHELL-13** — **A scripted run must not steal desktop focus.** The window is created with
  `no_focus` in project.godot; setting `WindowSetFlag` at runtime after the fact does not hand
  focus back, so an interactive session must request focus explicitly instead
  (`Launcher._Ready`).
- **SHELL-14** — **Test window-focus handling by alt-tabbing, not minimising.** Minimising a
  Godot window delivers only the mouse enter/exit pair, never a focus notification; on Windows
  11 / Godot 4.7 a real focus change delivers `APPLICATION_FOCUS_OUT`/`_IN`, not the
  `WM_WINDOW_FOCUS_*` pair.

## INSTR — building instruments

- **INSTR-1** — **Assume diagnostics perturb their subject.**
- **INSTR-2** — **Give overlays an able-to-fail control and a shipped-render-equivalent mode.**
- **INSTR-3** — **Share derived predicates with production code.**
- **INSTR-4** — **An observer must not change the state it reports.**
- **INSTR-5** — **Log resolved outputs as well as lookup inputs.**
- **INSTR-6** — **An able-to-fail control over randomised state must sweep seeds, not pin one.** A pinned seed makes one draw, and a bug that fires on some draws is invisible on the rest. Measured: with the BL-240 retirement hold removed, the recorded `--destroy=m_build` probe reports 0 misses at seed 1 but 2/1/1/1 at seeds 4/7/9/10 — the control that a single run was recorded as passing.
- **INSTR-8** — **Z-fighting is instability, not appearance: measure it as pixels that SWAP WINNER
  between captures a millimetre apart, never by looking at one frame.** Flatten the two contested
  textures to loud colours (`--tex-override`), shoot the same pose with the camera moved 1 mm, and
  count. Measured at the C1B water/shoreline pair: a single frame looks settled, yet 16,260 px
  (1.76 % of frame) swap winner under a 1 mm move at the shipped separation, 1,774 at 5e-6, and 0
  at 1.2e-5 — which is how the separation got bracketed at all.
- **INSTR-9** — **A score normalised per axis cannot judge a question about which axis is which — the degenerate axis decides it.** Scoring a candidate placement by "how far outside the host's bounding box, in units of that axis' extent" rules against whichever reading is vertical, because effects legitimately sit above flat things and a flat thing's vertical extent is ~0. Measured on the `AT_NODE` axis-order census: that score reported 63:101 *against* the reading three sound instruments confirm, led by a fireball 12 m over a ground ring 8.4 m wide and **0.0 m tall** — an escape of 11,900, which is a division by the ring's thickness, not a finding. Prefer a statistic invariant to the thing you are not testing (there, the *spread* of a host's sibling offsets, which a constant offset cannot move).
- **INSTR-10** — **An assertion keyed on a field that is not unique reports on whichever subject it
  reaches first — qualify the key, or the check is about something else.** The bug it hides is the
  one you were testing for. Measured on the `BL-229` suite: puffer names repeat across definitions
  (`small_fireball` declares a `trailpuffer2`, the same name a building's debris trail uses), so
  `Census.Any(r => r.Name == "trailpuffer2" && !r.Emitting)` was answered by the fireball's row and
  passed a runtime with the stop under test **deleted outright**. Host-qualifying the read exposed
  it. The rule generalises past names: any registry read whose selector is coarser than the thing
  being asserted about is a check on a different object. Its companion is that the deletion control
  must be run for EVERY half of a two-sided rule (METHOD-9/METHOD-10) — one half's control passing
  is what surfaced this.
- **INSTR-11** — **A probe that reports one half of a compound thing reads as a full pass on the
  half it can see.** Not a wrong answer — a narrow one, and the narrowness is invisible in the
  output. Measured: `--effects-test` reported `33/33 resolved, 30 built a puffer` for months while
  the MESH half of several of those effects never drew at all (`BL-061` — the rocket's called ring
  templates were placed at the site and left hidden). Nothing in the report was false; "renders" had
  quietly come to mean "emits particles". Two corollaries, both of which bit while closing it:
  (a) **sample over the window, not at the end** — the data turns its own meshes off inside it
  (`large_fireball` deactivates `flame_ball_01` 0.3 s in, before the 0.5 s the puffer count needs),
  so one final sample reports a working effect as a blank one; (b) **ask what is left behind** —
  the residual reading after the stop is a different question from the peak, and it is the one that
  found a mesh lit at the impact point for the rest of the session.
- **INSTR-7** — **"Not decidable from this data" is a fact about the instrument, not the question — when a census comes back uniform, ask what else varies the quantity.** A degenerate reading blocks the *inference*, not the *answer*. Measured: all 88 `destroyable_parts` pairs ship equal, which correctly made (armor, hp) undecidable from `extracted/`, and the reading sat blocked for nine days — the original's armory varies armor independently of health and settled it in one screen.
- **INSTR-13** — **An in-engine suite runs inside ONE frame: a physics body MOVED after creation
  never re-enters the space queries — aim at bodies where they were created.** Creation-time
  insertion is queryable immediately, but a later `GlobalTransform` write reaches the physics
  space only on a physics flush a synchronous suite never gets. Measured (`ai-actor`): the ray
  that returns the aircraft body at its spawn pose returns nothing at the flown-to position
  3.7 km away, and every scripted round missed — reading as broken hittability.
- **INSTR-12** — **A straight-up billboard probe reads edge-on and reports nothing about
  altitude.** A `cloudsprite` card is a `Facade`/`SphericalY` billboard, so a zero-green-pixels
  result looking straight up is a fact about billboard orientation, not proof the field is absent
  below that altitude — a compound-thing narrowness in INSTR-11's shape (the A3 probe it corrects).

- **INSTR-15** — **Report a collider-swap census by direction, never as a signed sum.** A death both
  disables the healthy collider and enables wreck colliders, so a net count can read positive while
  the real removal happened — count OFF and ON separately (see `Probes.EnabledColliders`).
- **INSTR-14** — **Every automated check here runs on a PARENT-DRIVEN clock, so a consumer stepped
  only from `GameSession.DriveSimSteps` is invisible to all of them.** `--det` — implied by
  `--run-tests`, `--screenshot=` and every other flag that drives a session by itself — makes
  `GameClock.ParentDriven` true, and a realtime (interactive) session never enters that method at
  all: it paces each consumer from its own `_PhysicsProcess`. Measured: E11's wave sequencer was
  stepped only there, so waves 2 to 4 could never arrive at the controls, while its suite and its
  scripted probes were both green. A per-step consumer belongs in one
  method called from both paths, the way the match clock already was.

- **INSTR-16** — **A capture taken on the very first rendered frame can beat the first per-frame
  publish.** `EffectAmbience`'s camera pose is written once per frame by `WeatherRig.Tick`; an
  emitter that draws before that call sees `HasCamera` still false and renders one frame unfaded —
  not evidence the distance fade is broken.
- **INSTR-17** — **A high per-site violation count on a dominant `PerfSample` site is not proof the
  instrument is broken — cross-site nesting is routine.** A death's event dispatch legitimately
  reaches the effect and audio sites, and a pool miss always reaches its own material-create site,
  so the outer site's record absorbs the inner ones' cost by construction. Read a high
  `sample_violations` on a dominant site as "more happened here than the named sites show," not as
  a defect to chase.
- **INSTR-18** — **Never place a flight-model probe above `flightModel.altitudeCapM`: the first step
  teleports it down to the cap and every distance downstream measures the teleport.** The clamp
  snaps any position over 2003 m to 2045.8 m, so a wreck released at 2500 m reports 454 m of
  "falling" in one frame and a distance check passes on nothing having flown. Measured on the death
  path, where the same wreck spawned at 1500 m holds its altitude to the metre across all three
  seconds, a dead hull gliding rather than dropping.
- **INSTR-19** — **The flight-dump hash agrees across the Godot runtime and the `dotnet test` host,
  but only because the print is rounded past where they diverge. Prefer a same-host A/B anyway.**
  The two runtimes' knife-edge values themselves differ by 1e-4 to 3e-2 (float32 accumulation, not
  the plant), so the agreement is a property of the shipped samples clearing their rounding
  boundaries, not a guarantee: a new sample, or a plant change that lands a value near a boundary,
  can put the two hosts back on different hashes. Treat a cross-host mismatch as a rounding
  boundary to investigate before it is a plant change. **Cost of the rounding: Δalt reads to the
  nearest 10 m**, so an altitude move smaller than that does not show in the dump at all; every
  other column lost one digit. The asserted rows are unaffected, since `KnifeEdgeTests` and
  `ParityLedgerTests` read the probe's values rather than this text.
- **INSTR-20** — **A negative test that also steps the mission script can arm the very gate it is
  asserting stays shut.** Drive only the subsystem under test through the control leg. Measured on
  the `landings-approach-trigger` suite: "flying the drop approach before the mission arms it starts
  nothing" was written as a flight that stepped the objective graph too, and C3/M01's drop cones sit
  9 m from the `shipwreck` the first primary approaches, so the same 6 s flight completed that
  objective and its 2 s + 3 s nap chain armed the trigger mid-leg. The assertion still passed, on
  the aircraft having left the cone by then rather than on the gate being shut.
- **INSTR-21** — **An animation that completes instantly satisfies every end-state assertion, so a
  suite that only reads end state is blind to a sequence that never played.** Assert the episode's
  DURATION as well: that it still owns its host a frame on, or how long it ran. Measured on the
  `landings-approach-trigger` suite: with `camera1` unbound, C3/M01's drop reported `ANIM_STATE`
  EXECUTED and completed its gated primary in the frame it started, and the suite was green for the
  whole of `BL-467`; bound, the same episode runs 4.43 s. The suite was also building a world with
  no `camera1` in it at all, so a suite over a subsystem must build the world the way the session
  that runs it does.

## SRC — sources and documents

- **SRC-1** — **Validate whether bytes are meaningful before numeric sanity checks.**
- **SRC-3** — **Use design documents for intent; retail evidence decides shipped details.**
- **SRC-4** — **When a fact is duplicated, name one description of record.**
- **SRC-5** — **A field you don't read may be REDUNDANT, not dropped — try to derive it from the
  fields you already read before deciding what it means.** A field with a shape your parser
  silently rejects looks identical to a missing feature, and the invented reading then doubles
  the effect. Measured: all 51 `OBJECT_MOTION_FROM_TO` `*_delta` vectors are exactly
  `(to − from) / run_time` of the sibling channel already implemented (worst residual 4e-6), so
  the "dropped" relative motion was the same tween's precomputed rate.
- **SRC-6** — **A test the binary computes is not a rule until you find what reads its result.**
  Finding the question asked is not finding the answer acted on. Measured: the take-hit body
  `FUN_004b9bc0` computes "shooter and victim are on the same team, or either is neutral" into a
  stack byte at `0x004b9d5e`, and its only read passes it to `FUN_0042e840` as an argument that
  function never touches, so the one place the damage path asks about teams decides nothing and the
  original applies friendly damage (`org/vehicleDamage.md`).
- **SRC-7** — **A key the data authors is not a feature until you find the parser that reads it;
  grep the executable for the key string before modelling it.** The dual of SRC-6, and the cheapest
  check in this project: the key name is a literal in the binary or it is not. Measured: `ia.json`
  authors `enemy_skill` in all 8 chapters and no such string exists in `crimson.exe`. The wave
  parser reads four keys and that is not one of them, so every file-launched wave takes the
  built-in default instead. Second instance of the same shape as `formats/turrets.md`'s unread
  `HEALTH`.
- **SRC-8** — **A branch's effect is only half its meaning; the other half is what it is an
  alternative to.** Recording what an arm *does* without recording which arm it excludes reads as
  an addition when it may be a replacement, and the two produce opposite implementations. Measured:
  the M4 B7 pass noted that `FUN_0045b9d0`'s mission-type-2 arm tops up a generator's capacity, and
  a plan was written around that being one contribution among several. Reading the function whole
  showed the discriminator at `0x0045ba9b` is an `if`/`else` whose other arm is the entire wave
  teleport, so on `zeppelin_run` the generator is not a supplement but the only route into the air
  (`formats/instant-action.md`). When you record a branch, record the
  jump it skips.
- **SRC-9** — **Searching a function for a flag's bitmask does not prove the flag is not tested
  there; compilers hoist a bit test into a shifted boolean.** Measured: `_DAT_0071d2fc` (the
  wrap-up's Shot % denominator) increments inside `FUN_004b6820`, and searching that function for
  the immediate `0x40` returns only stack offsets, which reads as "the fire counter has no weapon
  filter" and would have made Shot % asymmetric. The function actually computes
  `(weaponFlags >> 6) & 1` once at the top of each station loop and keeps it in a register, and the
  increment sits inside that arm, so both halves of the ratio are `CANNON`-only after all. Confirm a
  negative by reading the control flow around the site, not by failing to find the constant
  (`formats/instant-action.md`).

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
