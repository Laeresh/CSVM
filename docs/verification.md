# Verifying a change in this project

This project verifies through scripted screenshots, pixel diffs, log counts and `--perf`
numbers, and every one of those instruments has lied at least once — usually by returning
exactly the answer the hypothesis predicted. Narratives live in `docs/HISTORY.md` and git
history; per-module constraints as ⚠ lines in that module's `docs/architecture.md` entry.

**How to use this file.** Read **METHOD** before measuring anything, and **DIAG** when
chasing a symptom. Then open only the section matching the instrument you are about to use:
**SHOT** (screenshots, pixel diffs), **GOLD** (golden images), **DET** (determinism, seeds,
clocks), **PERF** (timings), **LOG** (log lines, error counts, exit codes), **WORLD** (world
data, colliders, the anim runtime), **SHELL** (PowerShell, processes, windows), **INSTR**
(writing a new diagnostic), **SRC** (extracted data and reference documents). The tail
sections — what needs the user, the non-determinism table, the standing checklist — close
out any verification.

**Rule IDs are permanent.** A new rule appends at its section's next free number; a deleted
rule retires its ID and leaves the gap; never renumber — scripts, code comments and docs
cite these IDs.

## METHOD — designing any measurement

- **METHOD-1** — **Check the case a plan points at can actually discriminate before
  measuring it.**
- **METHOD-2** — **Measure the same-build noise floor before believing a difference.**
  Under the `--det` default this is now a `--no-det` measurement (DET-6).
- **METHOD-3** — **Re-measure the baseline before believing a regression.**
- **METHOD-4** — **Measure a pose's sensitivity to ±1 frame on ONE build before believing a
  screenshot A/B.**
- **METHOD-5** — **Never baseline with `git stash` — flip the one line under test, build,
  run, flip back.**
- **METHOD-6** — **When an A/B swaps files, prove the new binary runs — a log line only new
  code can emit.** `Copy-Item` keeps the source mtime, so `dotnet build` no-ops and Godot
  runs the old DLL.
- **METHOD-7** — **Verify each branch independently, not just combined.**
- **METHOD-8** — **Confirm "pre-existing" by reproducing it on the unchanged build, not by
  argument.**
- **METHOD-9** — **A clean compile, passing test or unchanged number is not evidence until
  seen able to fail.**
- **METHOD-10** — **A test that would pass identically with a no-op is not a test.** The
  livery lab looked right over an already-painted plane.
- **METHOD-11** — **One camera angle — or one scripted pose — is not a test; sweep and
  require a flip.** A collision relabel moved the logged part at exactly one of five spawn
  altitudes.
- **METHOD-12** — **A metric can legitimately have to not move.** The anim dedupe fix
  correctly held the op count at 3531; expecting movement would have read as failure.
- **METHOD-13** — **Place the subject instead of hunting for a scenario that happens to
  suit — `--pos`/`--direction` reach every mode** (the camera in
  `--freecam`/`--viewer`/`--anim-lab`, the plane in `--fly`/`--stunt`). Two traps:
  `--direction` is a vector and `--lookat` a point (the `--viewer` orbit pivots on the
  point and synthesizes one from a vector, announced in the log), and comma-bearing
  arguments must be quoted in PowerShell (SHELL-1).
- **METHOD-14** — **Before believing a consistency test that reconciles two measurements,
  check it is not degenerate — vary the unknown it supposedly pins and watch the
  residual.** A thrust-vs-drag reconciliation agreed to 0.6% for *every* clock factor,
  residual constant to five decimals.
- **METHOD-15** — **Confirm the intervention actually took effect before crediting the
  result to it.** Godot silently clamps `--position`, so a green goldens run "proved" an
  off-screen window renders while the window had never left the screen.
- **METHOD-16** — **After a perturb-and-revert, force the rebuild — a restored file can be
  OLDER than the build output, so the "reverted" run silently re-measures the
  perturbation.** A `.bak` moved back kept its pre-DLL mtime and `dotnet build` no-oped; an
  unchanged result after a revert is a stale artifact until the timestamp says otherwise.
- **METHOD-17** — **A perturb-and-revert is clean when `git diff` says so, not when the
  content looks right — a rewriting script can change the file's ENCODING while restoring
  its text.** A Python `utf-8-sig` round-trip added a BOM the committed file never had;
  pair METHOD-16's forced rebuild with `git diff --stat`, and restore with `git checkout --`
  when the content is meant to be unchanged.
- **METHOD-18** — **A fire probe measures nothing past the weapon's RANGE — gun rounds
  expire silently there, so "no impact" is the aim geometry's verdict, not the surface's;
  keep the slant under RANGE.** 4 s of continuous 40slug fire (RANGE 1000 m) at 1380 m
  slant logged zero impacts over a fully collidable sea and filed as "the sea has no
  collider" (BL-017, disproven).
- **METHOD-19** — **To show a new tripwire can fail, restore the OLD behaviour *and* disable
  the new mechanism — a fix that repairs state later in the build hides the reinstated bug.**
  Reinstating the pre-fix collider walk alone left `collision-visibility` green, because
  `WorldCollision`'s tree-entry sync ran after the bootstrap and repaired every shape it had
  wrongly enabled; only with `Track` also stubbed did the suite report the real 1,484
  offenders across the 8 chapters.

## DIAG — chasing a symptom

- **DIAG-1** — **Check a plan's premise against the data before writing code.**
- **DIAG-2** — **A premise check needs its own able-to-fail control, and an animation
  census must cover BOTH sources — compiled (`cam_anim`/`mis_anim`) and reader (`zrdr`).**
  C1's `cloudparent#` is reader-only; a compiled-only sweep "disproved" a true premise.
- **DIAG-3** — **Before scheduling work from a bug report, `git log` the interval since it
  was filed.**
- **DIAG-4** — **Confirm a symptom is a DEFECT before diagnosing it: does the original do
  the same?**
- **DIAG-5** — **When a fix makes a metric worse, look at the artifact — enumerate the
  states the FIXED code can produce, not the bug's.** An "is it reddish?" classifier scored
  a correctly-off grey panel as absent.
- **DIAG-6** — **An arithmetic coincidence is not a mechanism — demand the exact value; a
  magnitude-only fit proves nothing.** The blown zeppelin transforms were proved by
  4.6109513952913965e27 matching the node's world X bit-for-bit.
- **DIAG-7** — **Isolate to the single node before naming a culprit; prefer a control
  changing only the suspected mechanism over removing geometry.** Hiding a sheet group and
  hiding one mesh gave the identical 0.19% flicker — hiding proves participation, never the
  partner.
- **DIAG-8** — **Audit inherited evidence: check what a pre-established claim was computed
  from.** Neither a claim's age nor the number of documents repeating it is evidence.
- **DIAG-9** — **A residual you have not driven to zero is not a floor.** A depth-only ramp
  control took a "mostly resampling noise" pose from 35.77% to 0.37% flicker.
- **DIAG-10** — **A change can be inert by construction in a chapter — say why, or
  "byte-identical" reads as "didn't work".** 7 of 8 chapters were unchanged because only C1
  has `OnStartup` light defs.
- **DIAG-11** — **The one deviating chapter can be the change working — identify what moved
  before "restoring" identity.** A new handler moved exactly one chapter: the install's
  single `ON_STARTUP` opacity fade, the case it exists for.
- **DIAG-12** — **Two different failures look identical from outside — inject input and log
  what each candidate receives.** "Handler doesn't fire" vs "node doesn't exist" reads the
  same.
- **DIAG-13** — **A visibly running system is not proof the numbers are right.** A
  degrees/radians slip made every rotation ~57× too small and nothing visibly turned.
- **DIAG-14** — **A probe that crashes has told you something — read the failure before
  "fixing" it.** The throw site alone can rule out an entire mechanism.
- **DIAG-15** — **Never silently skip a case in a diagnostic — recover and log.** A skipped
  case is a conclusion from half an A/B.
- **DIAG-16** — **To verify one effect, isolate it and remove whatever clears it early.**
  The 1.5 s auto-respawn kept clearing crash smoke that develops at ~2–3 s.
- **DIAG-17** — **"Never reached" and "reached but invisible" look identical in play — pick the
  instrument that holds the state, not the one that has to arrive at it.** BL-174 argued the
  plane's low-HP smoke might never appear in flight, fitting both a threshold no graze survives
  long enough to cross and an emitter parented so it draws nothing — see DIAG-18 for how that
  premise itself checked out.
- **DIAG-18** — **A mechanism argued from code alone is a hypothesis, not a finding — fly it
  before trusting it.** BL-174's two hypotheses (unreachable HP band; puffer parented under the
  mover, unrendered) were both reasoned from source, never played; a scripted
  `--pos --direction --hold` dive rendered the trail plainly before a later crash, even on the
  worst-case airframe (autogyro, all four parts 15 HP) — both hypotheses were false.

## SHOT — screenshots & pixel evidence

- **SHOT-1** — **A quantity read from an 8-bit capture is quantised — count the levels
  crossed; single digits means re-encode as a period, not a level.** A difference spanning
  ~1.4 quantisation steps gave 46 px/texel; a stripe-period probe gave ~4.
- **SHOT-2** — **A dead-still camera renders bit-identical frames — z-fighting needs
  `--jitter`.**
- **SHOT-3** — **Some real effects are below screenshot resolution.** The water flipbook
  differs by ~2/255 — a burst reads 0.01% and IS working; verify via the `--debug-anim`
  frame log.
- **SHOT-4** — **Check nothing occludes, fogs or washes out the thing under test.** Forcing
  sprite opacity to 0 moved 8 px under the cloud deck and fog; with deck hidden and fog
  off, 74,129 px.
- **SHOT-5** — **Compare pixel values, not an upscaled crop.** The gauge faces carry dark
  *unlit* STALL / LOW ALT copies (~58,0,0 vs 180+,0,0 lit) that read as lit when enlarged.
- **SHOT-6** — **Never compare images by encoded bytes — hash the raw pixel buffer.** All
  881 C1 texture PNGs differ byte-wise (encoder state only) while pixel-identical.
- **SHOT-7** — **`IsVisibleInTree()` is not "it renders" — when every metric says live and
  the frame is blank, diff against the nearest thing that DOES render.** Crash puffers
  drawing 462 instances drew nothing until parented at world level — `TopLevel` emitters
  draw nothing under the player subtree.
- **SHOT-8** — **A re-implemented render path must clip at the near plane, not cull — and a
  "no problem found" probe needs its own able-to-fail control.** A culling probe dropped
  the quads underfoot and said "nothing fights"; bias off gave 94.80% coincident.
- **SHOT-9** — **`--screenshot` needs a real GPU context — never pass `--headless` with
  it.** The dummy renderer's `texture_2d_get` returns null, so no file is written and the
  run still exits 0; `--headless` is only for the windowless `--dump-*` tools that never
  read back pixels. And the failure can FLOOD — thousands of `ERROR: Parameter "t" is
  null` lines that poison any error census; re-run the scenario without `--screenshot`
  using `--quit-after <frames>` and take THAT run's count as authoritative.
- **SHOT-10** — **`--screenshot=` takes an ABSOLUTE path, and the run exits 0 even when the
  save fails — `Test-Path` the file before trusting it.** The path is handed verbatim to
  `Image.SavePng` (no `GlobalizePath`), so a relative path resolves against Godot's own dir
  with only a quiet `ERROR: Can't save PNG at path` on stderr.
- **SHOT-11** — **Every screenshot baseline taken before 2026-07-25 is dead.** Shader-driven
  surfaces moved off Godot's `TIME` onto the clock-driven `csky_time` global, and the
  `--det` bundle (DET-6) moved framing a second time; re-capture rather than compare.
- **SHOT-12** — **A pose that renders identically twice is not proof a time-driven change
  works — most poses show no animated surface at all.** Only C1 (2 models), C1B (4) and C4
  (6) carry UV scroll in this install; read the `texture scroll: N model(s)` log line,
  frame the surface, and prove sensitivity by perturbing the time value.
- **SHOT-13** — **"Is this drawing at all?" is `--tex-override`, not a census count — the
  census is the map that tells you which texture to override.** The moored zeppelin
  measured 113,947 magenta px against 0 without the flag; the census read 88,301 at the
  same pose, a lower bound.
- **SHOT-14** — **Read a census count as a range (`px` lower, `px + contested` upper), run
  census shots with `--no-fog`, and treat a confident count under ~1,000 px as "not
  shown".** Fog dropped confident classification from 374,491 to 129,210 px; textures that
  exist only in other chapters still picked up 575 stray px worst-case.
- **SHOT-15** — **A chase-cam shot cannot support a claim about the underside —
  `--view=<1-9>` frames a *flying* plane from the belly, flanks or head-on (METHOD-11's
  sweep, cheap in flight).** Underside skin under `--tex-override`: 19,509 magenta px from
  `--view=2` against 127 from the chase camera at the same pose.
- **SHOT-16** — **A MINIMIZED window does not render; a HIDDEN one does — never minimize a
  window you are capturing from.** Creating the window minimized put 6 of 11 goldens on one
  identical blank hash while the other 5 passed; `ShowWindow(SW_HIDE)` from `_Ready` keeps
  all 11 hash-identical.
- **SHOT-17** — **"It fades" needs a mid-fade frame at visibly partial alpha — a piece that
  disappears at deactivate passes a frame-sparse capture as "faded".** The kkgate debris
  verification (BL-023) claimed fly + fade + deactivate from before/after shots; at the
  controls the pieces never turned transparent — the deactivate at end-of-ride had
  impersonated the fade. Same family as WORLD-12: a started opacity op that the material
  ignores measures as success unless you sample the pixels mid-ramp.
- **SHOT-18** — **`--tex-override` cannot shout through a COLORS ramp — the puffer shader
  multiplies the texture by the ramp colour, so a magenta drop-in on a dark-ramped emitter
  renders near-black and "zero loud pixels" reads as "not drawing".** The damage-stage
  `black_smoke` (ramp 5/255 ≈ 0.02 grey) was measured present at 55 live particles while
  three overridden textures showed 0 loud pixels; for ramped puffers, locate by frame-DIFF
  of consecutive `--shots` (moving pixels are the emitter) or a max-composite baseline,
  never by the override colour.
- **SHOT-19** — **A fading additive fireball reads as smoke — isolate the emitter before
  believing smoke works.** The converse bites harder: an additive emitter whose sprites go dark
  renders its whole late life as a dim haze that saturates into a glowing ball when the puffs
  overlap, so "there is fire here" is not evidence the fire is behaving. `large_30sec_fire`
  passed every headless check — resolves, builds a puffer, halts at 30 s — while showing a
  motionless red blob where the original climbs. Capture a **time series** (several `--frames=`
  values across the effect's life), never one frame: a shape defect only exists over time.
- **SHOT-20** — **`--screenshot=` into a directory that does not exist logs
  `screenshot saved: <path>` and writes nothing.** The exit code is 0 and the log line is
  identical to a real capture. `ls` the file before reading anything into a conclusion.

## GOLD — golden images

- **GOLD-1** — **A landed visual change updates `analysis/goldens/manifest.json` in the
  SAME commit and names the shots it moved; an unexplained golden flip is stop-the-line,
  not a regeneration.** The one legitimate mass flip is a GPU/driver change, announced as
  `GPU CHANGED: …`; a hash regenerated in a separate commit is indistinguishable from one
  regenerated to bury a regression, which is why `-RegenGoldens` reports `REGEN`, never
  `PASS`.
- **GOLD-2** — **A golden is a tripwire, not a diagnosis — investigate a failure with the
  headless instruments (`--tex-override`/`--tex-census` for "is this drawing",
  `--debug-anim` for pose and emitters, the mesh lab for shading), never by staring at the
  diff.** Perturbing the snow flutter constant moved exactly `c4-snow` and held the other
  ten — the moved shot names are the diagnosis's starting point, not its answer.
- **GOLD-3** — **A render-MODE change has a blast radius the repro shot cannot show. Sweep
  every golden both ways before calling the fix local, and read the big movers before
  reading the diff percentage as a regression.** Turning world backface culling on to stop
  the Hollywood facade panels z-fighting moved `c2-city` (which contains them) by 2.0 %,
  but moved `c1-flight` 29.6 %, `c4-snow` 26.6 % and `c1-crash` 18.0 % — none of which
  contains a facade panel. Those three were the same change fixing a *second* defect: the
  camera-anchored skydome's near wall had been drawing over distant terrain and cloud banks.
  The percentage said "regression"; the images said "fix".

## DET — determinism & randomness

- **DET-1** — **A sampler paced by the clock under test cannot see a rate error — take at
  least one measurement per wall frame or second.** Pose-per-sim-second matched while the
  lab ran at 2×; 20 sim-seconds in a ~10 s wall run caught it.
- **DET-2** — **Pass `--no-pads` on scripted runs — a drifting stick silently steers the
  free camera.** Automatic under the `--det` bundle (DET-6), but still yours to pass under
  `--no-det`.
- **DET-3** — **A weapon-impact test is reproducible ONLY under `--det`/`--seed` —
  `CANNON_SPREAD` is a per-round dice roll everywhere else.** Two `--det` dives log 8 of 8
  identical impact positions; unpinned pairs share none. Assert on the once-per-name effect
  breadcrumb rather than a fixed impact count (the impact log caps at 8), and place the
  plane with `--pos`/`--direction` (METHOD-13) instead of chapter-shopping for the surface
  you need.
- **DET-4** — **Godot's physics tick is ALREADY a fixed 1/60 s, so "two runs log identical
  telemetry" cannot discriminate a fixed sim clock — vary the RENDER rate instead.** The
  able-to-fail control is `--max-fps 30`: 900 rendered frames give 15 sim seconds and the
  same final pose under `--det`, 30 sim seconds and a different pose without it.
- **DET-5** — **Seeding a generator does not make a subsystem reproducible while it carries
  mutable state OUTSIDE that generator.** `SoundGroup._last` biases the next weighted pick,
  so a re-seeded `AnimRuntime` diverges on the first `SOUND_GROUPS` pick unless
  `ResetRecency()` clears it in the same breath.
- **DET-6** — **A scripted run is deterministic by DEFAULT — measuring wall-clock behaviour
  or live randomness takes `--no-det`.** `--screenshot=`, every `--dump-*` and
  `--damage-test` imply the `--det` bundle (fixed-dt clock + master seed 1 + `--spawn=0` +
  pinned liveries + `--no-pads` + `--jitter=0`), so a bare `--screenshot` is
  md5-reproducible. Two consequences: a noise-floor measurement (METHOD-2) is now a
  `--no-det` measurement, and the `det clock=… seed=… spawn=… livery_seed=… pads=off
  jitter=… via=…` log line is what a capture was taken under — read it instead of assuming;
  its absence means "this run was interactive".
- **DET-7** — **A deterministic run must be a function of the COMMITTED tree — audit what
  git-ignored state it still reads.** Honouring `CSVM/config.json` under `--det` made the
  same command produce different pixels per checkout; `--det` now drops the overrides and
  says `config=defaults dropped_overrides=N` (capture with your tuning via `--no-det`).
- **DET-8** — **A landed default can be silently reverted in play by the git-ignored
  `CSVM/config.json` — `--det` drops it and interactive runs do not, so the suites see the
  new value and the cockpit the old one.** After changing any `flightModel` default, check
  whether that file exists and what it overrides.
- **DET-9** — **A baseline holding a clock-derived value or an absolute path is not a
  baseline — it only reproduces on the machine and the minute that made it.** Report *that*
  a seed came from the clock, not which number came out; render paths against
  `{data}`/`{repo}` tokens; prove it by capturing twice and comparing hashes.
- **DET-10** — **A `--det` capture cannot show a realtime-cadence artifact — the fixed clock
  steps the sim once per rendered frame, so a 60 Hz-tick-vs-render-rate mismatch exists only
  under `--no-det`; measure it with a per-rendered-frame pose log, never a screenshot.** The
  live-flight plane stutter froze the plane's pose on 50.2% of rendered frames at ~120 fps
  while every `--det` flight capture stayed byte-identical.

## PERF — performance

- **PERF-1** — **`--perf`'s `script` reads ~2.2× the real frame time** (Godot's
  `TIME_PROCESS`); trust `frame`/`fps` for absolutes, `script` only as an A/B ratio.
- **PERF-2** — **Numbers pinned at the vsync cap are floors.** Read `physics`
  (`TIME_PHYSICS_PROCESS`, ratio-only too) for collision changes; with no monitor on the
  subsystem, call the effect unresolved.
- **PERF-3** — **Split the timer before choosing what to optimise — engine setters hide
  cost.**
- **PERF-4** — **Do not assume a cost is on the GPU.** Viewport GPU time was 0.27 ms while
  the frame was ~133 ms of C#; this trap fired twice.
- **PERF-5** — **Differences smaller than the instrument are not differences.** 5537 vs
  5606 ms over 3-run averages is noise.
- **PERF-6** — **Watch for cold caches.** An alarming first-run 8.1 s was the OS file cache
  on 630 freshly-written JSON files.
- **PERF-7** — **A startup timing without its cache state is meaningless — a cold run does
  not scale the profile, it reshapes it.** Same build, same chapter: 8578 ms cold, 1412 ms
  warm; the penalty lives in the phases opening thousands of small files (`anim` 21×) while
  `gamez` did not move. Discard the first iteration or say out loud that you did not.
- **PERF-8** — **Read the `[perf] startup` line rather than timing the process — the wall
  clock is mostly not startup.** `total = boot + Σ(phases) + rest + first_frame`; a
  `--quit-after N` process's wall time adds vsync-capped frames plus ~330–360 ms of spawn
  and shutdown, and `boot` silently contains menu time on a launchscreen-driven rebuild.
- **PERF-9** — **ONE same-build pair is not a noise floor — measure two; the second is
  routinely the noisier one.** ±10.5% then ±14.8% on back-to-back unchanged pairs — a band
  calibrated on the first marked five same-build rows on the second as regressions.
- **PERF-10** — **In a perf A/B, believe the counts before the milliseconds.**
  `draws`/`prims`/`nodes` came back identical to the digit in all 10 same-build pairings
  while every ms term jittered, and `prims` tracked a deliberate clutter perturbation to
  within a few percent, direction included.
- **PERF-11** — **`--perf`'s millisecond terms only speak with `--no-vsync`, and `physics`
  never speaks under `--det`** — the fixed clock is parent-driven, so `_PhysicsProcess`
  consumers no-op and collision cost lands in `script`. This machine paces at exactly
  120 fps even uncapped, so `fps`/`frame_ms` stay floors (PERF-2); read `render_cpu`,
  `gpu` and the counts.

## LOG — logs, error censuses & exit codes

- **LOG-1** — **A feature can run perfectly somewhere invisible.** 36 of C1's 38 sound
  emitters are built and stopped; `--debug-anim` prints visible-in-tree per node for this
  reason.
- **LOG-2** — **Ask what window an instrument covers before concluding from an absence.**
  The ambient-sound census prints inside `Bootstrap`; the police siren builds its emitter
  one frame later.
- **LOG-3** — **A tool limitation gets recorded as a data variant.** "24 of 48 SI-script
  parse failures" never existed — the walker lacked the header-declared counts.
- **LOG-4** — **A log line can be structurally unable to show the thing.** The motion line
  printed position only, and a spin turns in place.
- **LOG-5** — **A truncated diagnostic list can hide the entity under test *because* the
  fix worked.** The 12-motion print cap dropped looping cars (restarts re-register at the
  end); raise the cap for the measurement.
- **LOG-6** — **Grep the full stderr, not just the line you expect** — that has hidden a
  whole class of shader error.
- **LOG-7** — **`Assembly.Location` is empty under Godot's Mono loader, and an exception in
  `_Process` silently kills all per-frame debug logging.** Use an explicit literal build
  tag, not an assembly-mtime probe.
- **LOG-8** — **`--headless` compiles no shaders, so it cannot see a shader error — never
  take a headless run as an error census for anything that builds a material.** A
  missing-global error reproduced 1 → 0 windowed while reading 0 → 0 headless; SHOT-9's
  sibling — headless lies about pixels *and* shaders.
- **LOG-9** — **Read WHERE an error sits in the log before attributing it to the code that
  ran nearby.** Four `det == 0` errors printed *after* the death sequence they were blamed
  for had completed and reported — an error and a symptom in the same run are not the same
  event.
- **LOG-10** — **An error allowlist needs a CAP and a printed count, or it stops being an
  instrument.** `TestHarness.ErrorAllowlist` carries `(pattern, max, why)` and prints
  `allowed N/max` for every entry whether or not it passed; over cap fails, unknown fails.
- **LOG-11** — **Native Godot `ERROR:` lines cannot be seen from C# — capture them with
  `--log-file` and read the file back.** Two traps: `OS.GetCmdlineArgs()` does not contain
  `--log-file` (use `Environment.GetCommandLineArgs()`), and Godot still owns the handle
  (open it `FileShare.ReadWrite`).
- **LOG-12** — **An instrument that quits must quit with its verdict, or a caller cannot
  tell a failed measurement from a clean one.** All four `--dump-*` reports printed their
  error and exited 0; check a probe's exit code with a deliberately broken input
  (`--data-root=` at a path that does not exist).
- **LOG-13** — **Never run a manual engine probe while `RunTests.ps1` is running — the two
  share `.scratch/` outputs (`test-report.json`, same-second log names) and the collision
  reads as a phantom engine FAIL.** A concurrent hand-run `--run-tests` made the gate's
  engine stage die in 0.9 s, "exited -1 with no report", while the code was fine (B7).
- **LOG-14** — **Read every field of a multi-metric report row before pronouncing the subject
  healthy — the failure can sit beside the checks you came for.** kkgate's damage-test row was
  cited as "death sequence working" from `swap✓ col✓` while `debris[0 launched]` on the same
  line WAS the bug (the chained genx12 exploder resolving to nothing).
- **LOG-15** — **A native error that names no node is located by dumping candidate state on the
  erroring frame and matching COUNTS, not by patching suspects and re-running.** A tree scan for
  singular global bases put beside the `det == 0` prints matched 4/4 on C2 and 3/3 on C3
  (`StaticBody3D`s under scale-to-zero anims); the prior suspect-patching attempt fixed the wrong
  interpreter and measured nothing. Godot defers transform→physics flushes to end of frame, so
  such errors print after the code that caused them — LOG-9's ordering, explained.

## WORLD — world data & engine traps

- **WORLD-1** — **When two coplanar layers look like variants (day/night, LOD), test the
  geometry relationship first.** The 4×-lower-res, 3×-brighter "pair" was base ground under
  an authored subface — 100.000% containment.
- **WORLD-2** — **A bounding-box overlap is not an overlap — compute true polygon ∩ polygon
  area.** The AABB substitution inflated a survey from 0.6–1.6% of polygons to 9–26% where
  exact clipping gives zero.
- **WORLD-3** — **When an ID map proves "nothing else is there", check its key — what it
  cannot distinguish hides in it.** Per-*texture* colour scores zero when the crack shows
  another surface with the same texture; the fix was per-*surface* colour.
- **WORLD-4** — **File-stored bounding boxes are in the node's own frame; a position
  predicate usually needs the world frame.** "Contains the world origin" gave 464 roots
  from local `child_bbox` but 122 — all vehicles — in world space.
- **WORLD-5** — **"After bootstrap" is not "after everything that places things" — prefer a
  reversible action plus a recheck.** Motions and OnCall defs place entities seconds later;
  a one-shot sweep switched off 35 entities merely not yet in place.
- **WORLD-6** — **Triangulate exactly as `SceneBuilder.EmitPolygon` does before computing
  normals or areas — a `tri_strip`'s raw index list is not an outline.** Treating it as one
  manufactured a false 7–14% normal-inversion rate.
- **WORLD-7** — **A sampling window that ignores the structure it samples describes the
  wrong bytes.** A fixed 90,000-byte window ran past a `.BM`'s base plane and "proved"
  pre-painted shading maps.
- **WORLD-8** — **Godot node names are not the game files' names (`.` → `_`, duplicates
  auto-renamed) — use the `cs_name` meta.** C1's two `box_car.flt` siblings build as
  `box_car_flt` and `@Node3D@5`.
- **WORLD-9** — **Colliders exist only in the flight build — a collision census in any
  non-fly mode reads zero and lies.** `--freecam`, `--viewer` and `--anim-lab` build NO
  `StaticBody3D`/`CollisionShape3D` at all; the same world with `Collision` forced on has
  1848. Before measuring what is solid, confirm the mode actually built collision.
- **WORLD-10** — **A net collider delta hides a real removal — report `off` and `on` counts
  separately, never the signed sum.** The C2 propane gate nets +8 enabled while the door's
  healthy collider *did* turn off; the death just added more wreck than it removed.
- **WORLD-11** — **A synchronous "do X, then check" reads only IMMEDIATE (t=0) effects —
  SCHEDULED effects need the clock advanced** (`runtime.Advance(dt)` past the schedule; add
  the subtree to the tree and set `ManualAdvance` first, or it spams `!is_inside_tree`).
  Measure immediate state (swap, colliders) before the tick, scheduled state (debris)
  after — the water tower's debris fires at t=2.2 s.
- **WORLD-12** — **A runtime `PUFFER_STATE` renders NOTHING in the flight build — the
  puffer factory is torn down after the world build** (`KeepArchivesOpen` is lab-only; the
  per-player crash runtime is the one exception, it builds its own factory). Confirm a
  `Puffer` was *built*, never that the def started. The dedicated world-effects runtime is
  not a blanket fix either — it binds a FIXED closure of effect names, so check the name is
  bound before reading "the def started" as "the effect showed".
- **WORLD-13** — **A heuristic tuned against a WHOLE-WORLD population inverts when you
  build part of that world — re-derive its premise before reusing it on a slice.** The
  `ANIMATION_ROOT_NAME` 16-match cap exists because `healthy` appears 217× in C1; a
  two-match `--node=` stage passed the cap and anchored 95 unrelated definitions onto the
  radio tower.
- **WORLD-14** — **Measure a subject's bounding box before other systems parent nodes into
  its subtree.** `MeshLab`'s three empty overlay meshes at the session origin stretched a
  `--node=` subtree's box from 419 m to 5.3 km, framing the camera 12 km off.
- **WORLD-15** — **A world subtree's vertices are ABSOLUTE — its node transform is
  identity, so distance from that frame's origin is not a size; measure extent from the
  bounding BOX, never as max |v|.** A 4×14×4 m water tower "measured" a 7,420 m radius (its
  distance from the map corner); the origin-modelled aircraft is the case that hides this.
- **WORLD-16** — **One object can hold several destructible HP pools, and only ONE is
  reachable by damage — drive the wrong one and the object never reacts.** `DamageAt`
  re-resolves compiled-preferred, so spending health on the wildcard twin drains a pool
  nothing will ever hit; any instrument that damages a pool must name which pool it drove.
- **WORLD-17** — **Before using a per-chapter file or node flag to tell chapters apart,
  prove it discriminates — some are copy-paste boilerplate, some mean different things per
  chapter.** Seven of eight `map.json`s name the same map, and the gamez `terrain` flag
  marks land tiles in C1/C2 but the cloud deck or water plane in C1B/C1C/C2B.
- **WORLD-18** — **A per-item value read through a tier/level table must come from the
  item's own tier — check the index, never the first matching row.** The Bloodhawk's
  `engine` 11 is the Lvl-2 row (power 0.62), not Lvl-1's 0.47; the 32% error passed every
  downstream check because they only ever see the product.
- **WORLD-19** — **An effect has a MESH half and a PARTICLE half; a probe that counts
  particles is blind to the other one and will report a half-built effect as working.**
  `--effects-test`'s "built a puffer" column called the rocket explosion healthy for a whole
  milestone while every per-type ring mesh was missing: 19 of the 28 anchor roots its bound
  name closure needs were unstaged (so those defs were unanchored and played nothing) and the
  staged ones drew under a `Visible = false` stage. Two lessons that generalise past effects:
  a definition anchored on a node the build skipped **fails silently — no error, no event**,
  so measure the anchor set, not the play call; and when a probe answers one channel, name the
  channel it does NOT answer before trusting a pass.
- **WORLD-20** — **A class that covers a fraction of a percent of the map is not "unreachable"
  because a random probe never hit it — aim at named geometry, and census the map first.**
  `buildings`-classed surface is 0.07 % of C1's and C2's collidable area, ~0.00 % of C3's and
  C4's, and **absent outright from C1C and C2B**; only C5 (5.85 %) is easy to hit by accident.
  Rockets fired down a heading logged 8/8 `-> Default` and looked like proof the classifier
  was broken, while a shot at C1's `g306` hangar wall — located offline with
  `analysis/surface-classification/class_area_share.py` and `--destroy`'s world-centre line —
  logged `Buildings` first try.
- **WORLD-21** — **When a name resolves through two code paths, they will disagree, and the
  symptom is not "unresolved" — it is one half of a mechanism acting on a different copy of
  the object than the other half.** `fly_trail1`–`5` exists under `he_trails`, `ap_trails` AND
  `carnage_trails`, all staged side by side. `Targets` (motions) and `ResolveOne` (puffer hosts)
  each fell through to a **global** match, so an HE impact animated every copy — two of them
  still parked at the stage origin — while the emitter sat on one node and the authored
  `OBJECT_ACTIVE_STATE` stop fired on another, so the stop never reached it. Both bugs read as
  separate reports ("duplicates at 0,0,0", "the explosion never ends") and were one cause. Check
  for a **duplicate name across staged templates before trusting any name-keyed fix**, and route
  every lookup through one function.
- **WORLD-22** — **Do not read `IsVisibleInTree` as "is this running" in a subsystem that hides
  its hosts on purpose.** The world-effects stage keeps every template root hidden and its
  puffers still render, because particles go TopLevel into world space — so a `HIDDEN` line in
  `--debug-anim` says nothing about whether that node's emitter is emitting, and a visibility
  gate on emission (the rule `TickLights` correctly uses for lights) would silence every staged
  impact effect. Use the explicit state the data sets, not the flag it happens to share.

- **WORLD-23** — **Before simulating a decoded field, range-test it against the whole install: a
  unit shows up as a bound, not as a plausible number.** `translation_range`'s `xz`/`y` had been
  read as distances travelled, which produced arcs nobody could call obviously wrong. The census
  settled it in one pass: every `xz` in [−170, 359] and every `y` but one in [−90, 90] — those are
  **degrees**, an azimuth and an elevation, with `initial` the speed. Corroborate with the sign and
  with siblings: `y` goes negative exactly where the object falls, and the five trails of one
  explosion carry evenly spaced azimuth bands (35–55, 85–105, 135–165, 185–205, 235–255) that read
  as a starburst and as nothing else. A neighbouring constant can mislead here — the aircraft's
  arcade `nom_gravity` is 20, which invites reading `gravity: -3.0` as an offset to it, but the same
  census carries a literal **−9.8** on 173 events, so it is absolute m/s².

- **WORLD-24** — **An effect's own `PLAYER_RANGE` gate decides whether a scripted probe can see it
  at all — read the gate before flying the probe.** The `gunhit` puffer sits behind `PLAYER_RANGE
  500` (logged as `cond PlayerRange(250000)`, the squared metres). A strafing run set up at a
  natural standoff put the impacts ~800 m out: every round hit, the `impact:` breadcrumb resolved
  the right effect, and **not one puffer was built** — indistinguishable from unwired code. Two
  probes were spent on it. `--debug-anim` prints the verdict per condition, so grep the gate first
  and place the probe inside it; distance-gated effects are the norm in this data, not the
  exception (`gunhit` also gates its debris at 200 m and its light at 1000 m).

## SHELL — Windows, PowerShell & processes

- **SHELL-1** — **The repo path contains a space — a mis-quoted launch aborts every run
  instantly, reading as "the build is broken".** Use the call operator
  (`& "path\to.exe" args`) and quote comma-bearing args (they can arrive as
  `System.Object[]`); instant identical failure is the tell.
- **SHELL-2** — **Confirm zero stray Godots (yours and other agents') before believing a
  broken capture or error burst — kill by command line filtered to your worktree AND your
  instrument's own flag.** Strays manufactured 16,576 errors and a 1/7-size capture; a
  dir-only filter killed another session's live flights (`RunTests.ps1` kills only
  `--run-tests` Godots on this tree and merely reports the others).
- **SHELL-3** — **`dotnet build` cannot fail on comment-encoding corruption; Godot's Mono
  loader validates UTF-8 strictly and refuses the file.** After any bulk text rewrite, run
  the game — the damage surfaces as a `Main.tscn` parse error.
- **SHELL-4** — **`[IO.File]::ReadAllText($path, $encoding)` ignores your encoding when the
  file starts with a BOM.** For byte-preserving rewrites: read bytes, detect the BOM
  yourself, decode with `UTF8Encoding($false, $true)` so invalid input throws, rewrite the
  BOM you found.
- **SHELL-5** — **Window focus IS scriptable — `AttachThreadInput` + `SetForegroundWindow`
  fires real focus notifications.** A background `SetForegroundWindow` alone is no-opped by
  the foreground lock; minimising from another process fires no focus notification at all.
- **SHELL-6** — **Piping a PowerShell script's own output makes a child process's stderr
  terminating under `$ErrorActionPreference = "Stop"` (PS 5.1) — the run dies mid-stage and
  reads as a crash in the thing being measured.** `.\RunTests.ps1 | Select-String …` died
  on the first allowlisted `ERROR:` line; set `Continue` around native calls and judge them
  by exit code.
- **SHELL-7** — **Read a BOM-less UTF-8 file with `[System.IO.File]::ReadAllText`, not
  `Get-Content -Raw`** — PS 5.1 decodes it as the system ANSI codepage and turned every
  em-dash in the golden manifest into `â€”`. SHELL-4's other half.
- **SHELL-8** — **A window-focus probe that identifies windows by TITLE cannot tell your
  window from an identically-titled one — attribute by PROCESS TREE, walking children.** A
  title probe read 0 of 52 against a baseline that was itself another `CSVM (DEBUG)`
  session; the process tree showed 13 of 16, and the console build spawns 3 processes.
- **SHELL-9** — **A window has to be CREATED without focus — taking it back afterwards is
  not available to you.** `WindowFlags.NoFocus` from `_Ready` is too late and
  `WindowMoveToForeground` is no-opped by the foreground lock (SHELL-5); the lever is
  `display/window/size/no_focus` in `project.godot` (13/16 → 0/158 samples). The launching
  console may hand the foreground over, which is why an interactive launch grabs focus from
  `RunGame.ps1`, not the engine.
- **SHELL-10** — **"My pipe captured nothing" is not "the process printed nothing" — a
  Windows GUI-subsystem binary started without std handles reattaches to the parent CONSOLE
  and writes past your redirection.** Godot calls `AttachConsole(ATTACH_PARENT_PROCESS)`;
  give the child real handles (`ProcessStartInfo.RedirectStandardOutput`) and check the
  bytes arrive — silence you can read is the only silence you have measured. For ad-hoc
  scripted launches the handles come free: go through `RunProbe.ps1`, never `& $GodotExe`.
- **SHELL-11** — **A PowerShell property that throws yields `$null` silently, so a scoring
  expression reads the failure as a value.** A disposed `Start-Process -PassThru
  -RedirectStandard*` object's `ExitCode` read as empty and scored 9/9 passing suites as
  FAIL; assert every exit code, count or hash non-empty before comparing it.
- **SHELL-12** — **`EnumWindows` only enumerates the CALLING thread's desktop — "no window
  found" is what success and a dead process look like alike.** Check both halves:
  `EnumDesktopWindows` on the desktop you expect the window ON, plus the render's own pixel
  md5; any one alone is satisfied by a crash on startup.
- **SHELL-13** — **An unquoted path argument can break every Godot launch on the machine —
  quote (SHELL-1) and check the repo root for debris when launching starts failing.** An
  unquoted `--screenshot=Z:\Crimson Skies\…` wrote the PNG to `Z:\Crimson`, after which
  every `Godot_*_console.exe` launch died with `CreateProcess failed, error 193` (the
  wrapper tries `Z:\Crimson.exe`, then `Z:\Crimson` — a PNG). The failure survives the
  process that caused it and points at the launcher, not the writer.

## INSTR — building instruments

- **INSTR-1** — **A diagnostic can perturb what it measures.** MeshLab's override materials
  must replicate SceneBuilder's vertex stage *verbatim*; its old hardcoded light re-aimed
  the sun.
- **INSTR-2** — **A diagnostic overlay must be provable against the shipped render — give
  it a mode that forces it ON at the data's own settings and require pixel-identity.**
  `--debug-mesh=force` measured 0 px for the derived-from-the-original shader; the
  hand-written replica it replaced moved 1,682 px of a 2,500 px subject.
- **INSTR-3** — **A derived predicate each consumer spells out for itself drifts one term
  at a time, and the consumer then reports the absence its own copy created.** Three
  hand-written spellings of "does this session build colliders" had each dropped a
  different term; a tool reporting on a build product must read the same expression the
  build read.
- **INSTR-4** — **An instrument that reports a rule must not be a term of that rule.**
  `--dump-session` (since deleted — the rule outlived it) implying `--det` would have
  printed `det.on = true` on every row of a matrix whose whole subject is which command
  lines enable the bundle. When a general rule would make an observer change what it
  observes, the observer is the exception — state it at the observer, keep the rule's own
  expression clean.

- **INSTR-5** — **A breadcrumb that logs the input to a lookup but not its output cannot
  falsify "the lookup collapsed" — log the resolved value.** The impact breadcrumb reported
  the surface class and not the effect the class selected, so `BL-019` sat as an
  unfalsifiable "the per-surface lookup isn't differentiating" for two milestones; adding
  `fx=`/`snd=` answered it in one probe (it differentiates, and the *data* makes dirt a
  superset of buildings). Whenever a report is going to be "these two cases behave the same",
  the instrument must print what each case selected, not what each case was.

## SRC — sources & documents

- **SRC-1** — **"Is it a number" is the weakest check on junk-capable data — prefer a
  format flag saying whether the bytes are meaningful.** A NaN/∞ guard sails past finite
  garbage.
- **SRC-2** — **An absent asset filename is not evidence a feature was cut — features ship
  under implementation names, and view/camera features may need no art at all.** The
  spyglass is `MSG_CAM2_TOG` "Toggle Spyglass" — *camera 2*; ask "did feature X ship?"
  against `extracted/messages.json`'s `MSG_CMD_*`/`MSG_CAM*` table
  (inventory: `docs/formats/strings.md`).
- **SRC-3** — **Trust the original design document for system shape and field meaning,
  never for specific numbers or per-item art behaviour — retail captures and extracted data
  supersede it wherever both exist.** It gates shell ejection to 50/70-cal from the
  underbelly; the 40/30-cal Bloodhawk visibly ejects brass from its wing mounts in
  `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png`.
- **SRC-4** — **Where one fact must appear twice, name which copy is the description of
  record — descriptions drift long before lists diverge, and a name-level sync check cannot
  see it.** CLAUDE.md's flag table vs `docs/cli.md` passed a both-directions diff at 28/28
  flags while four rows contradicted in prose.

## What this project cannot verify itself

These need the user:

- **Audio** — mix levels, falloff, per-pane listeners; ambient world audio has never been
  listened to.
- **Feel** — turn rates, HUD placement, menu repeat, look sensitivity; the TUNE list in
  `backlog.md` exists for this.
- **Anything needing two controllers** — this machine has one pad.
- **Skilled flying** — a full 5-zone stunt run is not blind-scriptable.
- **Live keypresses** — R-restart is verified by construction only.
- **Fidelity against the original** — "nothing looks wrong in our build" is not a side-by-side.

## Known non-deterministic surfaces

**Read this table as a description of `--no-det` and interactive runs.** Every row except the first
is now pinned outright in a scripted one, because `--screenshot`/`--dump-*`/`--damage-test` imply the
`--det` bundle (DET-6) — so these are the numbers you get back the moment you pass `--no-det`, and
the reason you would. If your diff lands here, suspect noise first.

| Surface | Behaviour |
|---|---|
| `--fly` / `--stunt`, any pose | Same-build floor 30–84% of pixels **unpinned**; under `--det` a bare `--screenshot` C1 flight is byte-identical at frames 15/120/300 (0 of 921,600 px), since the chase camera takes the sim clock's dt while running. Goldens may frame flight poses |
| `--freecam` default camera | Random spawn per launch — `--det` forces `--spawn=0` (a pinned choice, stable across data changes); `--spawn=N`/`--pos`/`--direction` still override |
| Precipitation (C1C/C2B/C4) | ~5–25% frame difference under `--no-det`. Pinned outright by `--det` — the fall from `csky_time`, the per-instance seeds from the `precip` stream: two runs measured C2B rain 5.44% → **0.00%**, C4 snow 25.84% → **0.00%** |
| C3 water flipbook | Baseline flips between two states (~35,250 px, delta ≤3); open water moves 14.4% of pixels with the camera frozen, amplitude ≤12/255 — a real depth flip is delta ~100+. `--det` pins it (CPU `TextureCycler` on the sim clock) |
| UV scroll (C1 waterfall, C1B wakes, C4) | Wall-time `TIME` moved 30% of the C1 falls between two runs; `--det` pins it to **0.00%** |
| Puffer particle spread (waterfall mist, crash smoke) | Pinned by the master seed (`puffer` stream): the C1 waterfall moved 0.47% of the frame before A3, **0.00%** after |
| Bootstrap `unresolved` op count | `RandomWeight` dice — varies run-to-run under `--no-det` (100–107 measured once); pinned by `--det`/`--seed`, which seeds the world `AnimRuntime` in every mode, not just the lab |
| Damage-lab fire trails | 413–479 px between runs before A3/A4; **0.00%** now — measured md5-equal on a `--damage=nose:0.05,leftwing:0.05,rightwing:0.05 --frames=200` pair, which differs 3.05% (max delta 227) from a lightly-damaged pose, so the fires are genuinely burning in it |
| Liveries in flight | Randomised per player per load — pinned by `--det`/`--seed=N`, or by `--paint-seed=N` alone (measured: a random-livery `--viewer` shot moves 3.39% of pixels unpinned, 0.00% under `--det`) |
| Any world view | Not frame-deterministic under `--no-det` — measure the same-build floor first. **A `--freecam`/`--viewer`/`--anim-lab` capture is byte-identical by default now**: clock, shader time and every RNG are pinned (measured md5-equal on C1 falls, C2B rain, C4 snow, a parked plane and an anim-lab stage) |

## The standing checklist

Before calling a change verified:

- [ ] **`.\RunTests.ps1` green, exit 0** — one command for the build, the unit tests and the
      in-engine suites, and its exit code is the verdict. **A `SKIP` row is not a pass**: read its
      "not checked" lines before believing the run, and the `TODO` rows name what nobody checks yet
- [ ] Every golden hash that moved is explained, and `analysis/goldens/manifest.json` is updated in
      **this** commit with the moved shots named in its message (GOLD-1)
- [ ] If the change could cost frame time or startup time: a paired `-Perf` A/B, base and change
      measured back to back, read against a *freshly* measured same-build band (PERF-9…PERF-11)
- [ ] Build succeeds, and the **baseline** build succeeded too
- [ ] Camera and spawn pinned; noise floor (a `--no-det` measurement now — DET-6) measured same-build-vs-same-build
- [ ] The instrument has been shown capable of reporting failure
- [ ] Nothing is occluding, fogging, deactivating or rounding away the effect
- [ ] "Pre-existing" claims reproduced on the unchanged build
- [ ] 8-chapter regression: zero errors, and counts explained rather than just unchanged
- [ ] Static plane viewer byte-identical (md5) if the change should not touch aircraft
- [ ] Full mode battery: fly / stunt / viewer / damage / 4P race / menu
- [ ] What remains unverifiable is stated plainly, not implied to be done
