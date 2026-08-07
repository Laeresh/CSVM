# What a capture session actually costs, and whether the sidecar cache pays for it

**Question.** `BL-308` added a per-clip decode cache (`analysis/video-flight-calibration/clipdata.py`)
on the claim that CAP tasks were burning context re-deriving what earlier sessions had already
measured. Is that claim true, and does the cache measurably reduce it?

**Answer: the premise holds; the payoff was never measured, and the item was closed anyway.**
Clips are revisited constantly, so there is a second visit for a cache to serve. Whether the cache
makes that second visit cheaper was never tested — `BL-308` was retired on 2026-08-07 by user
decision, because the measurement work kept costing more than the answer was worth.

⚠ **This file is a record, not a task list.** The cache itself stays: `clipdata.py`, `videodata/`
and the `/analyse-capture` §2b read-first / §7b write-back steps are unchanged and still in force.
What was dropped is the effort to prove its value. The A/B design below is kept only so that anyone
who revives the question does not re-derive the contaminated version of it — **it is not owed
work**, and nothing is waiting on it.

## The measured baseline: `CAP-14`, session `3ca1a395`

Scored with `bench.py`:

| metric | value |
|---|---|
| images read | **25** |
| output tokens | **347,404** |
| input tokens (new) | 706 |
| cache reads | **85,830,895** |
| tool calls | 224 over **375** assistant turns |
| tool-result text | 286,298 chars (~72k tok) |
| top tools | PowerShell 110, Write 46, Read 36, Edit 16 |

Two things this says:

**The self-report was accurate.** A session asked where its context went answered "~20 contact
sheets and stills, most at high resolution" — the transcript holds **25** image blocks. Worth
recording because that self-report is what the whole design was built on, and it could easily have
been an impression rather than a fact.

**Cache reads dominate, and they scale with turn count, not with work.** 85.8 M against 347 k output
tokens. Context accumulates and is re-sent every turn, so 375 turns of a growing transcript costs
more than anything any single tool call did. ⚠ This is why "read fewer images" and "print fewer
rows" are the same lever: both keep the transcript small for *all subsequent turns*, not just the
one they occur in.

⚠ **Images may be a smaller share than the self-report implies.** A control comparison against
session `47b3691f` — near-identical length (373 turns vs 375) but **0 images** — came in at 74.0 M
cache reads against 85.8 M, only **14% lower**, with 2% *more* output tokens. So 25 images account
for at most ~12 M of the 85.8 M, and probably less, since the two sessions differ in other ways
too. The dominant term really is turn count times accumulated transcript. This tempers, without
overturning, the "images are the bulk" premise the design was built on: images are the largest
*single discretionary* cost a capture session controls, but most of the bill is simply being long.
Run the A/B before concluding either way.

⚠ **A session total is not a task total.** This transcript's 46 `Write`s and 16 `Edit`s are build
work (it is the session that added `extract.py`'s 2560×728 `LAYOUTS` row), not CAP-14 analysis. Do
not quote 347 k as "the cost of analysing CAP-14". It is an upper bound on a session that contained
that analysis.

## Are clips actually revisited?

`revisit.py`, over all 237 transcripts in the project:

| | |
|---|---|
| clips on disk | 94 |
| clips ever mentioned | **94** |
| clips in **>1** session | **89 (95%)** |
| clips in **>2** sessions | **71** |
| most revisited | `CAP-06` 87, `CAP-18` 85, `C3 Spiderweb` 71, `CAP-10` 70 |

⚠ **These are mentions, not re-decodes.** A clip named in `FINDINGS.md` or `backlog.md` is counted
whenever that file is read into context, which inflates every figure — the top few almost certainly
ride on documentation reads rather than fresh analysis. The safe reading is directional: footage is
not analysed once and forgotten, so the second visit a cache exists to serve is the normal case.
A sharper version of this measurement would count only sessions that actually invoked `extract.py`
or `ffmpeg` on the clip; it has not been done.

## The A/B protocol — designed, never run

⚠ **The obvious test is contaminated and must not be used.** "Re-run a settled CAP and compare"
fails, because a settled CAP's answer is written in `backlog.md`: a cold session reads the entry,
finds the finding, and stops. That scores a spectacular saving which measures the backlog, not the
cache. `BL-308`'s original success criterion said exactly this and was wrong.

**New footage cannot A/B it either.** A fresh clip has no sidecar in either arm, so hiding the
store changes nothing. The cache is a second-visit instrument and has to be measured on a second
visit.

The design that works, once a capture with real open questions exists. Step 1 did in fact happen —
`CAP-31` was analysed cold on 2026-08-07 and left sidecars for `cap31` and `accel` — so a revival
would start at step 2 with a follow-up question that session did not answer. It got no further:

1. **First visit — real work, not overhead.** A cold session runs `/analyse-capture CAP-31`,
   answers its `BL-`, and writes the sidecar plus shot-index as the mandated write-back. Score for
   reference.
2. **Pick a follow-up question that step 1 did not answer** — for a deceleration clip, e.g. the
   altitude drift across the 290 → 150 mph decay. It must be answerable from the same footage.
3. **Arm A (no cache):** move `videodata/` aside, run `CleanScratch.ps1`, `/clear`, ask the question.
4. **Arm B (with cache):** restore `videodata/`, run `CleanScratch.ps1`, `/clear`, ask the *same*
   question verbatim.
5. `python analysis/session-cost/bench.py <id-a> <id-b>` prints both and the delta.

⚠ **Sweeping `.scratch/` before arm A is load-bearing.** Skip it and arm A inherits step 1's `.npy`
panel-strip cache, so the no-cache world looks far cheaper than it is and the comparison
understates the cache by most of its value.

⚠ **Neither arm may be run from a session that helped build the tool.** Such a session already
knows the footage and the answers; it will produce a flattering number that means nothing.

## What would falsify the cache

If arm B reads roughly as many images as arm A, the premise is wrong in its most important half:
the shot-index would not be displacing contact sheets, and the saving would be confined to numeric
traces — real, but far smaller than the design assumed. Record that outcome rather than retrying
until it looks good; the navigational-only rule means a session must still open frames to conclude,
so a modest reduction is the honest expectation, not a collapse to zero.
