# Cinemas — the ten MPG movies play

**ACTIVE PLAN** (written 2026-09-07). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan makes the retail install's ten `.mpg` files play in CSVM: the looping flag background
behind the front end, the two boot logos, the opening cinema, the five chapter cinemas and the
closing cinema. It delivers a managed MPEG-1 decoder owned by this project, a frame surface that
composes through the existing board pipeline, and the sequence around them written as C# flow.
`BL-446` is the item it closes, and it was re-verified still-open in this session against both the
record (`git log --grep=BL-446` finds only its minting; `docs/PLAN-public-release.md:1078` lists it
as a shipped known limitation) and the code (the tree contains no `VideoStreamPlayer`, no `.ogv`
and no transcode step).

**Transcoding is out of scope and stays out.** No ffmpeg, no `.ogv`, no change to the extraction
scripts or the release manifest. The decode happens at runtime from the player's own shipped bytes.
The engine side of GUI script callbacks 2151 and 3104 is also out of scope: this plan reads the
chapter number and the completion gate from CSVM's own campaign state rather than tracing the
original's dispatch.

## Milestone goal

- The animated flag loops behind the front end where `LAYOUT.CSV` places it, composed as the
  bottom layer of the screen rather than as a separate node.
- A bare launch plays the two logos and the opening cinema, skippable by any key or click.
- Each chapter's cinema plays before its chapter and hands off to the passenger cabin; the closing
  cinema plays on campaign completion and hands off to the scrapbook.
- The release zip is the same size it is today, carrying no media binary and no new payload version.

**Nothing in this plan converts a video file.** The shipped bytes are what plays, which is what
makes the output incapable of drifting from what the original showed and what removes the transcode
judgement from the project entirely.

## Decisions (2026-09-07)

Settled by a grilling in the session that wrote this plan. This table is the authority where the
prose below disagrees with itself.

| # | Question | Decision |
|---|---|---|
| 1 | Does the flag background ship, and as what? | **Ships, faithful, as video** — its softness under `BoardFit` is the original's own softness at 2.5x and not a defect to correct. |
| 2 | Transcode at extract time, or decode at runtime? | **Decode at runtime, in managed code** — the recipient runs extraction on their own machine, so a transcoder ships in the download; a stock ffmpeg would roughly double a 78 MB zip for ten short videos. |
| 3 | What order do things land in? | **Decoder headless first, then the flag, then the cinemas** — the codec is the only risky part and the only part `RunTests.ps1` can gate, so it lands alone and green. |
| 4 | `VideoStreamPlayback`, or a texture? | **A texture through `ComposedBoard`** — a `movie` row is structurally a picture at Z=0 under the screen, and a player node would put it in a different rendering path from everything above it with `BoardFit`'s scaling mirrored by hand. |
| 5 | Do the movies carry audio? | **The flag plays silent; the nine cinemas carry their audio** — so the layer II decoder and A/V sync are both in scope. |
| 6 | Is the sequence data or code? | **Hand-written C#** — reading `fmv.zrd` and the GUI scripts at runtime would mean two interpreters for two authored languages to drive behaviour that now fits on a page. |
| 7 | What makes the decoder correct? | **Spec vectors, goldens as tripwire, and the user at the controls** — there is no reference decoder, and MPEG-1's permitted IDCT mismatch rules out exact-match testing against one. |
| 8 | When does the boot sequence play? | **On a bare launch only**, skippable by any key, plus a documented `--skip-intro` for the desktop-shortcut case. No preferences toggle, because the original had none. |
| 9 | How is the work tracked? | **This plan, three waves**, with `BL-446` closed by its completion. |
| 10 | Ported or written from the spec? | **Port `pl_mpeg`** (MIT), reshaped into this project's module conventions — with no oracle to check against, starting from an implementation known to work is worth more than it would be if we could diff against ffmpeg. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The chapter cinemas are named by the executable, from the `char[9]` array at `0x0061e68c`. | `get_xrefs_to` shows the array's only reference is a pointer at `0x0061daec`, and that slot has no code reference at all. `CAMPAIGNINTRO.SCRIPT` builds the name as `"chap" conv$(FC) ".mpg"`. The array is dead, which is also why `chap6.mpg` has no file. |
| 2 | Nothing names `final.mpg` or `crimflag.mpg`. | `ASSETS/LAYOUT.CSV` names both, as `movie` widgets on six screens. Neither string appears in `crimson.exe` at all. |
| 3 | A case-sensitive lookup fails on the three names `fmv.zrd` spells with capitals. | It fails on four of the ten files (`msopen1.mpg`, `chap0.mpg`, `crimflag.mpg`, `final.mpg`). `zipper.mpg` was always spelled to match, and the script-built chapter names are lower case. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, B12, C22, C23, C24 | Confirm the trace against [`docs/formats/cinemas.md`](formats/cinemas.md), then implement. |
| **Leads only — no mechanism yet** | A3, A4, C21 | Budget for investigation; C21's premise in particular rests on recall, not on a decode. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

Full census in [`docs/formats/cinemas.md`](formats/cinemas.md); the facts this plan leans on:

- **Ten files, about 110 MB**, all MPEG-1 system streams, MPEG-1 video 320x240 at 856 to 1500 kbps
  with MPEG-1 audio layer II at 44.1 kHz. Two break the otherwise uniform profile and must be read
  per file: `msopen1.mpg` is 29.97 fps at 1500 kbps, and `crimflag.mpg` is mono at 64 kbps.
- **Six `movie` widget rows in `ASSETS/LAYOUT.CSV`**, all at X, Y and Z zero with the whole screen
  as their region and `ScaleX`/`ScaleY` of 250: `CrimFlag.MPG` with `Loops` 0 on `MainMenu`, `Save`,
  `Load` and `Preferences`; `CrimFlag.MPG` with `Loops` 1 on `CampaignIntro` as a placeholder the
  script overwrites; `Final.MPG` with `Loops` 1 on `FinalCinema`. `Loops` is a play count in which
  zero means endless. The 250 is a percentage against the original's fixed 800x600 authored space,
  so 320x240 fills it exactly at the same 4:3 aspect with no crop and no letterbox.
- **`FUN_004a7c70`** is the executable's whole involvement: it resolves `Assets\Graphics\MPG` into a
  `MAX_PATH` buffer at `DAT_0064fde4` as the base the `PLAYAVI` names resolve under, then runs
  `fmv.zrd` `INTRO` and `fmv.zrd` `CHAP0` through `FUN_0044ae70`, which has exactly one caller.
- **`CAMPAIGNINTRO.SCRIPT`** skips on Escape, Space, Return or left mouse and hands off with
  `script_continue @passengercabin@`. **`FINALCINEMA.SCRIPT`** skips on Escape or left mouse only,
  is gated on `callback($$E$$, 3104)`, and hands off by running `scrapbook.script` at `0x1000`.
  Both latch on `EC` against a double handoff.
- **`OriginalAssetManifest.cs:144-145`** records `MainMenu.MOVIE` and `Preferences.MOVIE` as not
  drawn. `Save` and `Load` are not in Original's composed section list, so the flag reaches two live
  screens today and four whenever those screens exist.

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
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the decoder, headless

1. ☐ System-stream demux and MPEG-1 video decode, as a managed module with no Godot in it
2. ☐ MPEG-1 audio layer II decode
3. ☐ The test surface: spec vectors, and whole-file checks skipped when `extracted/` is absent
4. ☐ The port's third-party notice

### Wave B — the flag background

11. ☐ The frame surface: decoded frames as an `ImageTexture`, on a playback clock
12. ☐ `CrimFlag.MPG` composed into `MainMenu` and `Preferences`

### Wave C — the cinema sequence

21. ☐ Audio playback and A/V sync from the stream's presentation timestamps
22. ☐ The boot sequence on a bare launch, with the skip and `--skip-intro`
23. ☐ The chapter cinema and its passenger-cabin handoff
24. ☐ The closing cinema, its gate and its scrapbook handoff

## Dependency and parallelism notes

A1 blocks everything; nothing else in the plan can start until frames come out of a file. A2 blocks
C21 only, so it can run in parallel with all of Wave B. A3 follows A1 and A2 and is the wave's exit
gate. A4 is independent of the code and can run at any point in Wave A.

B11 needs A1. B12 needs B11. C21 needs A2 and B11. C22, C23 and C24 each need C21 and B11, and are
independent of each other.

File contention: B12, C23 and C24 all touch the front-end composition (`CampaignBoards.cs`,
`OriginalAssetManifest.cs`), so they must not run in parallel worktrees. A4 is the only item that
touches `packaging/`.

---

# Wave A — the decoder, headless

## A1 ☐ System-stream demux and MPEG-1 video decode, as a managed module with no Godot in it

**Goal.** Given the bytes of any of the ten files, the module yields decoded frames as raw pixel
buffers, in order, with their presentation timestamps, running in a plain unit test with no engine
present.

**Evidence (confidence: traced).** Every file's container and codec profile is read from its own
pack, sequence and audio frame headers, and tabulated in
[`docs/formats/cinemas.md`](formats/cinemas.md): MPEG-1 system stream with one video elementary
stream (`E0`) and one audio elementary stream (`C0`) under `BA` pack and `BB` system headers, video
320x240 with no sequence extension start code following any sequence header. Two files break the
uniform profile and the decoder must take parameters per file rather than assume one set:
`msopen1.mpg` at 29.97 fps and 1500 kbps, against 30 fps and 856 to 889 kbps for the rest.

**Approach.** Port `pl_mpeg`'s demux and video decoder (Decision 10), reshaped into this project's
module conventions rather than transliterated: a buffer reader, the VLC tables as data, and the
decode loop as a deep module whose public surface is "give me the next frame". No Godot types cross
the boundary, which is what makes A3 possible and what Decision 4 relies on. Do not touch the
extraction scripts or `packaging/`; this module reads bytes handed to it.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** Frame count, dimensions and frame rate for each of the ten files match what the headers
declare, run against a real `extracted/` tree. `.\RunTests.ps1` green.

**⚠ Traps.** ⚠ Per-file parameters, not one profile. ⚠ A ported file that keeps C idioms will fight
`CheckCommentCaps.ps1` and the architecture docs forever; reshape it on the way in, not later.
⚠ There is no reference decoder to check against, and there deliberately will not be one, so a
subtly wrong VLC table produces plausible garbage rather than an error.

## A2 ☐ MPEG-1 audio layer II decode

**Goal.** The audio elementary stream decodes to PCM with timestamps, in the same headless module.

**Evidence (confidence: traced).** Every file carries MPEG-1 audio layer II at 44.1 kHz, 64 to
128 kbps, read from the audio frame headers. `crimflag.mpg` is the only mono file; `msopen1.mpg` is
stereo at 64 kbps where the other stereo files run higher.

**Approach.** The layer II half of the same `pl_mpeg` port. Same module, same no-Godot boundary.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** Sample rate, channel count and frame count per file match the headers. `.\RunTests.ps1`
green.

**⚠ Traps.** ⚠ Mono is not the exception to ignore: `crimflag.mpg` is the file Wave B plays, so a
stereo-only path breaks the first visible deliverable.

## A3 ☐ The test surface: spec vectors, and whole-file checks skipped when `extracted/` is absent

**Goal.** `.\RunTests.ps1` gates the decoder on a machine with no game install, and gates it harder
on a machine with one.

**Evidence (confidence: lead-only).** `PROJECT_CONTEXT.md:101` describes `CSVM.Tests` as "reader
units on hand-authored fixtures + `extracted/` golden counts, skipped when absent", which is the
shape this needs, but no fixture for a compressed bitstream exists yet and the exact vector set is
unchosen.

**Approach.** Unit-test the pure pieces against tables written out of ISO 11172-2 itself: the
bitstream reader, the VLC tables, dequantisation, the IDCT, motion compensation. Those are
hand-authored fixtures and commit freely. The whole-file checks ride the existing skipped-when-absent
pattern. Per Decision 7, ffmpeg may be used once informally on a development machine as a sanity
check and is never committed and never shipped.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** The suite passes with `extracted/` absent and with it present. Deliberately corrupt one
VLC table entry and confirm a test fails, because an unchanged number is not evidence until you have
seen it able to fail.

**⚠ Traps.** ⚠ MPEG-1 permits IDCT mismatch between conformant decoders, so any test that asserts
exact pixel equality against another decoder's output is wrong even when both decoders are right.
⚠ No `.mpg` from the install may be committed as a fixture.

## A4 ☐ The port's third-party notice

**Goal.** The release carries `pl_mpeg`'s MIT notice, assembled the same way every other notice is.

**Evidence (confidence: lead-only).** `packaging/BuildThirdPartyNotices.ps1` assembles
`LICENSE-thirdparty.txt` from three sections (Godot, the .NET runtime, `unzbd`'s crates) through a
`Section` helper, and `packaging/MANIFEST.md` records that the export refuses to ship the file
against payload versions it does not speak for. A fourth section for this project's own third-party
source is a natural extension, but no such section exists yet and its exact shape is unchosen.

**Approach.** Add a fourth `Section` call reading the MIT text from a file in `packaging/`, and the
corresponding row in `packaging/MANIFEST.md`. Keep the script pure ASCII and write through the .NET
APIs, per the header's own note about PowerShell 5.1 double-encoding copyright signs.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** `packaging/BuildThirdPartyNotices.ps1` regenerates cleanly, `CheckEncoding.ps1` passes,
and the generated file contains the notice.

**⚠ Traps.** ⚠ The notices file is version-locked and the export throws when the header disagrees
with what it is packaging; a new section must not break that check.

---

# Wave B — the flag background

## B11 ☐ The frame surface: decoded frames as an `ImageTexture`, on a playback clock

**Goal.** A decoded stream drives a texture that updates at the file's own frame rate and loops
endlessly when asked, with no Godot node beyond the texture itself.

**Evidence (confidence: traced).** `ComposedBoard` resolves a screen into `Fills`, `Pictures` and
`Lines` in the original's 800x600 space as `BoardPicture(art, x, y)`, and `BoardFit` maps that board
onto the window with one uniform scale (`docs/architecture.md:335`, `src/UI/BoardFit.cs`). The
layout's `Loops` field is a play count in which zero means endless.

**Approach.** A surface that owns a decoder from A1, a clock and an `ImageTexture`, exposing the
texture for the composition to draw. Feed it the layout row's `Loops`. Nothing here knows which
screen it is on.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** <TODO: name the headless check that proves the clock advances at the file's own rate,
including the 29.97 fps file>

**⚠ Traps.** ⚠ The flag plays silent (Decision 5), so this surface must not assume an audio stream
exists to drive its clock; the video timestamps are the clock for Wave B.

## B12 ☐ `CrimFlag.MPG` composed into `MainMenu` and `Preferences`

**Goal.** The animated flag runs behind the main menu and the preferences page, at the position and
scale the layout gives, under everything else on the screen.

**Evidence (confidence: traced).** `OriginalAssetManifest.cs:144-145` records both rows as not drawn
with the reason "the backdrop movie is not in the extraction and no screen plays one".
`CampaignBoards.cs:257` records the same absence from the other side, noting that the profile
dialog's title mark "stands alone and the ground behind it stays plain". Both layout rows are
`ArtPath` `CrimFlag.MPG` at X, Y, Z zero, `ScaleX`/`ScaleY` 250, `Loops` 0.

**Approach.** Compose the movie as a `BoardPicture` at Z=0 fed by B11's texture, and remove the two
`NotDrawn` entries. Take the position and scale from the layout row rather than hardcoding them, so
the two screens that are not yet composed (`Save`, `Load`) need no second implementation.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** Both screens at the controls, judged by the user, per Decision 7. Then pin goldens for
both as the drift tripwire, and confirm `git diff` shows `analysis/goldens/manifest.json` unmodified
before believing any "identical" report (GOLD-9).

**⚠ Traps.** ⚠ A golden of a looping movie is only stable if the shot lands on a deterministic
frame; pin the surface to a fixed frame index for capture rather than to wall time, or every run
re-baselines. ⚠ Do not correct the softness. At 320x240 scaled 2.5x into the board and then again by
`BoardFit`, the flag is soft on a modern display, and that is the original's own look (Decision 1).

---

# Wave C — the cinema sequence

## C21 ☐ Audio playback and A/V sync from the stream's presentation timestamps

**Goal.** A cinema plays with its audio in sync from the first frame to the last, on the project's
audio bus.

**Evidence (confidence: lead-only).** That the nine cinemas play their audio while the flag plays
silent is the author's recall of the original, not a decode: the `movie` widget row has no audio
field of any kind, and the engine side of the player was not traced. Every file does carry a layer
II track, read from its frame headers, so the tracks exist regardless.

**Approach.** Push A2's PCM to an `AudioStreamGenerator` and drive the video clock from the system
stream's presentation timestamps. Route onto whatever bus `PLAN-audio-preferences` lands; today
`Launcher.cs` touches only `MasterBus`.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** The user watches a full cinema end to end and confirms the audio has not drifted by the
end, which is the failure this item exists to prevent.

**⚠ Traps.** ⚠ Drift accumulates, so a check on the first thirty seconds proves nothing; the
verification is a whole file. ⚠ This item depends on `PLAN-audio-preferences`'s bus layout, which is
in flight; do not invent a bus here.

## C22 ☐ The boot sequence on a bare launch, with the skip and `--skip-intro`

**Goal.** Double-clicking `CSVM.exe` plays `msopen1`, `zipper` and then `chap0` before the launch
screen, and any key or click moves on.

**Evidence (confidence: traced).** `FUN_004a7c70` runs `fmv.zrd` `INTRO` and `fmv.zrd` `CHAP0` in
one unconditional block, with `Assets\Graphics\MPG` as the base path the `PLAYAVI` names resolve
under. `docs/cli.md:243` documents `--menu` as bare-only when other arguments are absent, which is
the existing precedent for bare-launch behaviour.

**Approach.** A boot sequence in C#, per Decision 6, suppressed whenever any CLI argument is
present so no test, golden or perf launch grows by two minutes. Add `--skip-intro` to `docs/cli.md`,
documented as the bare-launch desktop-shortcut case rather than as a general suppressor, since any
argument already suppresses.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** A bare launch plays the sequence and a key press reaches the launch screen. A launch with
any argument plays nothing. `.\RunTests.ps1` wall time is unchanged, checked against
`analysis/verification-budgets.json`.

**⚠ Traps.** ⚠ `chap0` is 17.8 MB and runs for minutes; a user who has not learned the skip meets it
before they learn it, which is the cost Decision 8 accepted knowingly. ⚠ The names resolve
case-insensitively or three of these four files are not found.

## C23 ☐ The chapter cinema and its passenger-cabin handoff

**Goal.** Each chapter's cinema plays before its chapter and hands off to the passenger cabin, with
the original's skip keys.

**Evidence (confidence: traced).** `CAMPAIGNINTRO.SCRIPT` builds the name as `"chap" conv$(FC)
".mpg"` from `callback($$E$$, 2151, FC)`, so chapter N plays `chapN.mpg`. It skips on Escape, Space,
Return or a left mouse press, all posting message 11006, which runs `script_continue
@passengercabin@` and ends the script, latched by `EC` against a double handoff.

**Approach.** A flow state in C# taking the chapter number from CSVM's own campaign state rather
than from callback 2151, which stays untraced and out of scope. The `CrimFlag.MPG` in the
`CampaignIntro` layout row is a placeholder the original overwrites, so do not read it.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** <TODO: name the chapter to run and the handoff to confirm at the controls>

**⚠ Traps.** ⚠ The skip keys differ from C24's and the asymmetry is the original's; put the reason on
the member or someone will unify them as a bug fix. ⚠ The dead array at `0x0061e68c` is not the
source of these names; see the disproven-claims table.

## C24 ☐ The closing cinema, its gate and its scrapbook handoff

**Goal.** `final.mpg` plays on campaign completion and hands off to the scrapbook, skipping straight
to the scrapbook when the campaign is not complete.

**Evidence (confidence: traced).** `FINALCINEMA.SCRIPT` is gated on `callback($$E$$, 3104)`: when
that returns false it runs `scrapbook.script` at priority `0x1000`, mails 11005 and ends without
playing anything. It sets no art path, so the screen plays whatever its `CF_MOVIE` layout row names,
which is `Final.MPG`. It skips on Escape or a left mouse press only, not Space and not Return.

**Approach.** A flow state in C#, with the gate read from CSVM's own campaign-completion state
rather than from callback 3104.

**Model recommendation.** <TODO: not settled in the session that wrote this plan>

**Verify.** <TODO: name how a completed campaign is reached or simulated for this check>

**⚠ Traps.** ⚠ Escape and left mouse only. Space and Return do nothing here and do something in C23,
and that difference is authored, not accidental.
