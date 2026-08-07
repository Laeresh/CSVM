---
name: analyse-capture
description: Analyse an owed capture (CAP-nn) — find its videos and screenshots, decode or watch them, and answer the backlog items it was filmed to unblock. Use when the user names a CAP-NN, hands over new footage of the original game, or asks what a capture shows.
---

Turn one `CAP-nn` recording of **the original game** into evidence a `backlog.md` item can close on.

The capture exists because a question could not be answered from extracted data or from our own
build — so the whole value is in reading the footage honestly. This skill is mostly **read-and-
measure**; it edits `backlog.md`/`playtest.md` only at the end, and never commits
unless asked.

⚠ **Never answer from the filename or from the backlog's expectation of what the clip shows.**
Every claim must cite a timestamp, a frame number, or a decoded number you actually produced. If
the footage does not show the thing, the finding is "the clip does not settle this" — which is a
real result and must be reported as one.

## 0. Work in a worktree

Enter a git worktree with `EnterWorktree` before touching anything else — name it after the
capture (e.g. `worktree-cap-08`). The skill edits `backlog.md` and `playtest.md`
and can span several sessions, so isolating the work keeps `main` clean and lets
other captures run in parallel. Exit with `action: "keep"` when you report (Section 8), so the user
can review and merge the branch themselves — never merge or push it yourself.

## 1. Resolve the capture

Argument may be `CAP-16`, a bare `16`, or a phrase like "the crash one".

Read the `CAP-nn` row in [`playtest.md`](playtest.md) §0 — it gives the capture's **purpose**, what
must be in frame, and the `BL-NNN`s it unblocks. Then read each of those backlog entries in full,
including the enclosing section heading. They carry the traps, the competing hypotheses, and the
specific number wanted; going to the footage without them produces a description instead of an
answer.

A missing `CAP-nn` means the item closed and its ID retired (IDs are never reused) — say so rather
than guessing at a neighbour. A phrase means **ask which**, never guess.

## 2. Find the media — the naming is not regular

Search `OriginalScreenshots/` and `OriginalScreenshots/Videos/`, plus `playtest/<ID>/` for anything
already staged. Four traps, all of them live in the current file set:

- **The separator varies.** `CAP-14 Graze and CAP 15 wing to red.mp4` writes one ID hyphenated and
  the other spaced. Match `CAP[-_ ]?0*<n>` case-insensitively, not `CAP-<n>`.
- **One file can carry several captures.** That same clip is the whole of `CAP-15`. A file named for
  a neighbour is not evidence of absence — grep the *other* IDs too before reporting one missing.
- **Multiple takes are normal and are not ranked.** `CAP-05` has four files (two knife-edge, two
  stalls). Analyse all of them; if they disagree, that disagreement is the finding.
- **Screenshots are named by subject, not by ID.** No image in `OriginalScreenshots/` carries a
  `CAP` number today — they read `C1 IA1 Cloud Puffs and Moon.png`. So also search by the row's
  subject words (`cloud`, `flare`, `spawn`), and by chapter/mission prefix.

List what you found with durations and resolutions before analysing, so the user can correct the
set early. Say plainly if nothing matches.

## 2b. Check the sidecar before you touch ffmpeg

A previous session may already have decoded this clip. `videodata/` holds one CSV per video
(git-ignored, **not** swept), and `show` prints a ~40-line header — stamps, a digest with every
column's extrema and when they occur, the cached `checkclip` gate verdict, and a prose shot-index:

```
python analysis/video-flight-calibration/clipdata.py show  <clip>
python analysis/video-flight-calibration/clipdata.py where <clip> "climb_fpm < -4000"
python analysis/video-flight-calibration/clipdata.py slice <clip> --from 7.5 --to 9.0
```

Read that header **first**, for every clip in the set. Three things it decides for you:

- **A cached `REJECT`** means the clip has auto head turn and cannot be decoded. Stop; do not
  re-derive that verdict, and do not sample frames hoping otherwise.
- **The digest's extrema and their timestamps** answer a surprising share of "where does X happen"
  outright — no sheet needed.
- **`⚠ STALE` on a column** means the producing script changed since the decode. Re-run
  `clipdata.py build <clip>` before quoting that column; other columns are unaffected.

⚠ **Never read a sidecar file whole** — 1,000–2,000 rows costs more context than the dumps it
replaces. `show`, `slice` and `where` are the interface.

⚠ **The shot-index is NAVIGATIONAL ONLY.** It tells you which second to open. It is *not* evidence
and is never cited in `backlog.md` — the ⚠ rule at the top of this skill stands unchanged: every claim cites a
timestamp, frame or number **you** produced this session. An index line by a previous session is a
lead, and a wrong one is exactly as wrong as a filename.

⚠ **An `UNINDEXED` span means nobody looked, not that nothing happens.** Never report absence from
a gap.

## 3. Classify the capture — the two paths are different work

**Gauge-decode** (`CAP-01` `CAP-03` `CAP-04` `CAP-05` `CAP-06`, and any clip whose answer is a
*number* off the cockpit panel). These go through `analysis/video-flight-calibration/`, which reads
altitude to ~0.5 ft and airspeed to ~0.3 mph per frame. Section 4.

**Qualitative** (everything else — camera layout, effects, audio, world). These are watched, frame-
sampled and listened to. Section 5.

A clip can be both: `CAP-10`'s engine note is audio, but correlating it against climb rate needs the
gauges decoded from the same frames.

## 4. Gauge-decode path

Read `analysis/video-flight-calibration/FINDINGS.md` first — its **traps** section is the accumulated
cost of getting this wrong, and its "Running it" block is the current cold-start order. Do not
re-derive the method.

`.scratch/` is swept, so a cold start rebuilds the cache:

```
python analysis/video-flight-calibration/extract.py <keys>   # video -> panel-strip cache
python analysis/video-flight-calibration/pool.py             # pooled median + std
python analysis/video-flight-calibration/fitdial.py          # must reach NCC ~0.98 on both dials
```

New footage needs a key in `extract.py`'s `CLIPS`, and an unlisted capture resolution needs a
`LAYOUTS` entry — it raises rather than guessing an origin. 32:9 and 16:9 both normalise to
canonical 1280×720 game coords, so the median and dial fits carry across sessions.

Then, per clip:

```
python -c "import sys;sys.path.insert(0,'analysis/video-flight-calibration');import checkclip;checkclip.report('<key>')"
```

⚠ **The gate is not optional and its verdict is not a formality.** `REJECT` on anti-correlated dx
means auto head turn was on and the clip must be re-recorded — no amount of care downstream fixes
it. A large *positively* correlated dx is shake, which `shake.py` handles. Two takes were already
lost to this.

Then `shake.py` if it shakes, `run2.py` for the needle angles, `anchor.py` to resolve the altimeter's
1,000 ft band. Sanity-check against `FINDINGS.md`'s published figures for the same aircraft before
trusting a new one.

⚠ **Time is not `frame / fps`.** The captures are variable-frame-rate; individual intervals are
wrong by up to ±50%. Use PTS. And the original's sim clock runs fast — **k = 1.390 ± 0.021** — so a
wall-clock duration is not a sim-seconds duration. State which one every number is in.

## 5. Qualitative path

`ffmpeg` is not on `PATH`; the bundled binary is:

```
FF=$(python -c "import imageio_ffmpeg;print(imageio_ffmpeg.get_ffmpeg_exe())")
```

⚠ **Crop the pillarbox before anything else on a 32:9 clip**, or half the width of every tile and
still is black and the gauges come out too small to read. `ffprobe` the size first: **2560×720**
needs `crop=1280:720:640:0`; **2560×1440** is already full-frame and needs no crop.

⚠ **Images are the single largest cost of a capture session** — a measured run spent most of its
context on ~20 sheets and stills, many at 2304×2160 or larger, and context once spent cannot be
recovered. Every sheet must be justified by a question the numbers could not answer.

- **Ask the sidecar first, not ffmpeg.** Sheets exist to find the interesting second; `clipdata.py
  show`'s digest and a `where` query usually name it outright, for zero pixels. If the clip has a
  decoded track, go to a timestamp — don't survey.
- **Contact sheet as the fallback**, when the question is genuinely visual (an effect, a camera
  move, world art) or the clip has no numeric track:
  `"$FF" -i "<clip>" -vf "crop=1280:720:640:0,fps=1,scale=420:-1,tile=5x3" -frames:v 1 sheet.png`
  (drop the `crop` for a 16:9 clip; raise `fps` and the tile count for a short, fast event).
  Scale it down and keep the tile count low — a sheet you can read at 420 px wide costs a fraction
  of a full-res one and answers the same "which second" question.
- **Stills at a moment**, high quality: `"$FF" -ss <t> -i "<clip>" -frames:v 1 -q:v 2 out.png`
  (put `-ss` *before* `-i` for speed, after it for exact-frame accuracy).
- **Audio**: `"$FF" -i "<clip>" -vn -ac 1 -ar 22050 out.wav`, then analyse the spectrum in numpy —
  for a pitch question, track the fundamental over time rather than describing the sound.
- **Read the stills you produce.** Actually open them with Read; do not infer from the command
  succeeding.

Write intermediates to the scratchpad, not the repo. Anything worth keeping for the user goes in
`playtest/<ID>/`, which is git-ignored and **not** swept, so it survives until the item closes.

For an A/B against our build, launch it with `./RunGame.ps1 --plane=… --chapter=…` (flags in
`docs/cli.md`) and frame the shot the same way — a comparison at a different angle or altitude is
not a comparison.

## 6. Ask, rather than guess — this is expected, not a failure

Batch questions with `AskUserQuestion` and keep working on the parts that don't depend on the
answer. Ask when:

- **You need a timestamp.** The user flew it and knows where the event is; a 34 s clip may contain
  one 2 s stall break. Ask "which second is the graze?" rather than sampling 34 frames hoping.
- **The footage is ambiguous.** Two takes disagree, the gauge is unreadable at that scale, the
  effect is off-screen or behind the wing, the HUD element you need is occluded.
- **You cannot tell what you're looking at.** Which plane, which mission, which of several similar
  passes, whether a visual is the effect under test or an unrelated one.
- **The clip may not answer the question at all.** Say what is missing and what a re-record would
  need to include — that is more useful than a hedged verdict, and `playtest.md`'s row already
  states what must be in frame.

Offer your reading as the first option when you have one, and say what you'd conclude if they
haven't a preference. Do not stack up questions you could answer by looking harder.

## 7. Record the finding

Write to the entry that owns the question, in its own voice:

- **Answers a `BL-NNN`** → update the entry with the measurement and how it was obtained, then
  offer `/close-backlog-item` rather than closing it here.
- **Refines it** → edit the entry so the next reader gets the sharper question, and note which
  hypothesis the footage rules out.
- **Settles nothing** → say so on the entry and in `playtest.md`'s row, with what a usable re-record
  needs. Do not silently leave the row looking discharged.
- **Discharges the capture** → the `CAP-nn` row goes only when every `BL-NNN` in its Unblocks column
  is served. If it still owes another item, leave the row and note what's done. When an ID does
  retire, it is never reused — take new IDs from `playtest.md`'s counter, don't scan for a free one.
- Findings that change what we believe about the original are recorded in the closing commit's
  message, per `/close-backlog-item` §4 (`docs/HISTORY.md` is frozen — never append to it).

⚠ **A capture is evidence, not proof of a constant.** Record the honest limit: one take is not a
distribution, a plateau is worth more than a transient, and a number read off a single frame carries
that frame's noise. `FINDINGS.md` has several traps about consistency checks that confirm whatever
value you feed them — re-read them before declaring a match.

## 7b. Write back to the sidecar — required, not optional

Before reporting, record what you learned about the *footage* so the next session does not pay for
it again. This has the same standing as updating `playtest.md`: the analysis is not finished
without it.

- **A clip you decoded** gets a sidecar: `clipdata.py build <clip>`. This also caches its gate
  verdict, which is what stops a `REJECT` clip ever being decoded twice.
- **Every span you opened frames on** gets one index line, in the clip's own terms:

  ```
  python analysis/video-flight-calibration/clipdata.py note <clip> 7.8 8.4 "wing contact with cliff, sparks; camera unshaken"
  ```

**Bound it to what you actually looked at.** Do not go exploring to fill the file — the cost is
writing down what you already know, not indexing the whole clip. Everything you did not open stays
`UNINDEXED`, which is a true statement and a useful one.

Write it in the voice of someone pointing, not concluding: *"nose drops here"*, not *"the stall
breaks at 8.1 s"*. The index is navigational; the conclusion belongs on the `BL-NNN` entry with its
own cited evidence.

⚠ `clipdata.py` writes to the **main checkout's** `videodata/`, resolved via `git rev-parse
--git-common-dir` — so it works unchanged from inside your worktree, and needs no junction (CLAUDE.md
forbids those) and no path typed by hand. Each index line records the branch that wrote it, so an
abandoned branch's observations stay traceable rather than anonymous.

## 8. Report

Before reporting, re-read the capture's `CAP-nn` row in `playtest.md` §0 and check it against what
you actually found. If the row still carries prose describing the shot as owed, unanalysed, or
open — and Section 7 discharged it or moved every open question onto the `BL-NNN` entries — that
prose is stale and must be updated or removed so the row doesn't contradict the backlog it points
to. Do not silently leave a discharged row reading as if the capture is still needed.

Give the user: which files were analysed (with the gate verdict for each gauge clip, saying whether
it was cached or freshly run), the finding per `BL-NNN` in plain prose with its evidence cited by
timestamp or frame, what remains open, and the files touched — including which sidecars you built or
added index lines to, and which spans are still `UNINDEXED`. Quote real numbers and real command
output — never a summary of what a run "should" produce.

Then stop. **Do not commit** unless asked; offer it in one line. If you entered a worktree, leave it
in place (`keep`) — mention its path and branch so the user can review and merge it.
