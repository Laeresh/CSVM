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
  start by one tick shifts every particle in the frame. Measured: making a called sequence's first
  event fire in the calling tick (`BL-135`, 1/60 s earlier) left 7 of 8 chapter captures
  bit-identical and every runtime total unchanged, yet moved `c1-crash` by **79.7 % of its pixels**
  — the crash fireball covers the frame. So judge a timing change by what the moved shots *are*
  (all four movers were particle shots) before concluding either that it broke something or that it
  is harmless.
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

## SHELL — Windows, PowerShell, and processes

- **SHELL-2** — **Identify stray Godot processes by worktree and probe flag.**
- **SHELL-3** — **After bulk rewrites, run Godot as well as the compiler.**
- **SHELL-4** — **Detect and preserve BOM and encoding explicitly.**
- **SHELL-6** — **Judge piped native processes by exit code.**
- **SHELL-7** — **On PowerShell 5.1, read BOM-less UTF-8 through an explicit UTF-8 API.**
- **SHELL-10** — **Launch scripted Godot probes through `RunProbe.ps1`.**
- **SHELL-11** — **Assert exit codes, counts, and hashes are non-empty.**
- **SHELL-12** — **Give every scripted probe an exit condition, and check the flag you chose actually is one.** `--frames=N` is `ScreenshotFrames` (`SessionSpec.cs:586`) — a warm-up counter that terminates the run only alongside `--screenshot`. Passed on its own it reads as valid, changes nothing, and the probe runs until killed: one `--debug-anim` run left this way spent six hours writing a 45 MB log.

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
