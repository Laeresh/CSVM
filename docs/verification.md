# Verifying a change in this project

This file contains transferable verification rules. Dated evidence belongs in `docs/HISTORY.md`,
analysis findings, or git history; module constraints belong in `docs/architecture.md`.

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

- **GOLD-1** — **Update moved hashes with the visual change and explain each shot.**
- **GOLD-2** — **A golden is a tripwire, not a diagnosis.**
- **GOLD-3** — **For render-path changes, sweep every golden and inspect the largest movers.**

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

## INSTR — building instruments

- **INSTR-1** — **Assume diagnostics perturb their subject.**
- **INSTR-2** — **Give overlays an able-to-fail control and a shipped-render-equivalent mode.**
- **INSTR-3** — **Share derived predicates with production code.**
- **INSTR-4** — **An observer must not change the state it reports.**
- **INSTR-5** — **Log resolved outputs as well as lookup inputs.**

## SRC — sources and documents

- **SRC-1** — **Validate whether bytes are meaningful before numeric sanity checks.**
- **SRC-3** — **Use design documents for intent; retail evidence decides shipped details.**
- **SRC-4** — **When a fact is duplicated, name one description of record.**

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
