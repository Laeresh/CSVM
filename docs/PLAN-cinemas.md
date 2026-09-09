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

**Transcoding is out of scope and stays out.** No ffmpeg, no `.ogv`, no conversion of any kind, and
no media binary in the release. The decode happens at runtime from the player's own shipped bytes.
The engine side of GUI script callbacks 2151 and 3104 is also out of scope: this plan reads the
chapter number and the completion gate from CSVM's own campaign state rather than tracing the
original's dispatch.

**The extraction does gain one step, and only one.** The ten files are not in `extracted/` and no
extraction script has ever mentioned them, so `A5` copies them across verbatim. That is a copy, not
a conversion, and it is the whole of this plan's reach into `packaging/` beyond `A4`'s notice.

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
| 4 | The ten files are reachable, so playing them is only a decoding problem. | They are not in `extracted/` at all. They sit loose in the install under `GOSDATA\ASSETS\GRAPHICS\MPG\`; `extracted/rof/ASSETS/GRAPHICS/MPG/` is an empty directory entry, `ExtractRof.ps1` never mentions them, and `SessionPaths` resolves nothing outside `dataRoot/extracted/`. `A5` exists because of this. |
| 5 | `packaging/BuildThirdPartyNotices.ps1` assembles three sections, so pl_mpeg's is the fourth. | It assembles six, and pl_mpeg's is the seventh. The count in `A4`'s Evidence was wrong; the `Section` helper it named was right. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A5, B11, B12, C21, C22, C23, C24 | Confirm the trace against [`docs/formats/cinemas.md`](formats/cinemas.md), then implement. |
| **Leads only — no mechanism yet** | none open | A3 and A4 were the two leads and both landed. |

C21 was a lead resting on the author's recall that the flag plays silent. `A2` decoded every track
and measured `crimflag.mpg`'s as digital silence, so it is traced now. With Wave A complete, every
item still open rests on a mechanism read out of the data or the executable.

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
- **Every item here runs on Opus.** That is why each item's Model recommendation says only "Opus":
  the tier is a property of this project's orchestration rather than of any one item, and a cheaper
  tier on a wave of items has already cost this project two whole waves of work.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the decoder, headless

1. ☑ System-stream demux and MPEG-1 video decode, as a managed module with no Godot in it
2. ☑ MPEG-1 audio layer II decode
3. ☑ The test surface: spec vectors, and whole-file checks skipped when `extracted/` is absent
4. ☑ The port's third-party notice
5. ☑ The extraction copies the ten `.mpg` files into `extracted/`

### Wave B — the flag background

11. ☑ The frame surface: decoded frames as an `ImageTexture`, on a playback clock
12. ☑ `CrimFlag.MPG` composed into `MainMenu` and `Preferences`

### Wave C — the cinema sequence

21. ☑ Audio playback and A/V sync from the stream's presentation timestamps
22. ☐ The boot sequence on a bare launch, with the skip and `--skip-intro`
23. ☐ The chapter cinema and its passenger-cabin handoff
24. ☐ The closing cinema, its gate and its scrapbook handoff

## Dependency and parallelism notes

A1 blocks everything; nothing else in the plan can start until frames come out of a file. A2 blocks
C21 only, so it can run in parallel with all of Wave B. A3 follows A1, A2 and A5, and is the wave's
exit gate. A4 and A5 are independent of the code and can run at any point in Wave A.

A5 blocks every item that names a file rather than taking bytes: A3's whole-file half, B12, C22, C23
and C24 all resolve a path that does not resolve until it lands. A1, A2 and B11 are unaffected,
because each takes bytes or a stream from its caller and never names a file of its own.

B11 needs A1. B12 needs B11 and A5. C21 needs A2 and B11. C22, C23 and C24 each need C21, B11 and
A5, and are independent of each other.

File contention: B12, C23 and C24 all touch the front-end composition (`CampaignBoards.cs`,
`OriginalAssetManifest.cs`), so they must not run in parallel worktrees. A4 is the only item that
touches `packaging/`, and A5 the only one that touches `ExtractRof.ps1`.

---

# Wave A — the decoder, headless

## A1 ☑ System-stream demux and MPEG-1 video decode, as a managed module with no Godot in it

**Landed.** `CSVM.Video`, eight modules under `CSVM/src/Video/`, with `docs/architecture/Video.md`
and its index section. `MpegMovie` is the façade: `FromFile`/`FromBytes`, then `Width`, `Height`,
`FrameRate`, `PixelAspectRatio`, `NextFrame()` returning null at the end, and `Rewind()`. Frames
alias the decoder's rotating buffers and are valid only until the next call, which the member says.
`MpegSystemStream` and `MpegVideoDecoder` are usable on their own, which is what `B11` needs.

The VLC tables and the four 64-entry transform tables were generated and checked by script against
the reference rather than transcribed by hand, and the generator stayed out of the repo. The port
diverges from `pl_mpeg` in four places, each a defect in the reference rather than a preference:
its motion-compensation guard is a flat-offset check that permits a read past a plane and a
horizontally wrapped prediction, where this one checks rows and columns and repeats the edge sample;
its picture loop can spin forever on a header it rejects, where this one drops the start code and
rescans; it leaves a half-filled block behind after an invalid run, leaking into every later block of
the picture, where this one clears it; and its packet header parse reads two bits and then skips
sixteen for a sixteen-bit P-STD field, so the demux here is written from ISO 11172-1 byte by byte,
which is what the Approach asked for anyway.

**Verified.** The full battery on the merged tree, run by the orchestrator rather than reported by
the agent: `.\RunTests.ps1` PASS, exit 0, 275.5 s. Build 2.6 s with zero StyleCop warnings; units
84.6 s, 3639 passed, 0 failed, 0 skipped; engine 145.6 s, 263 passed, engine errors clean; goldens
42.6 s, 18 shots hash-identical. All ten files decode end to end, all 320x240 with square pixels:
`crimflag` 240 frames in 8.0 s, `msopen1` 404 in 13.5 s at 30000/1001 fps, `zipper` 608 in 20.3 s,
`chap0` 4349 in 145.0 s, `chap1` 3926, `chap2` 3698, `chap3` 2784, `chap4` 4124, `chap5` 2874, and
`final` 3330 in 111.0 s, the other nine all at 30.0 fps. Frame times increase strictly and land at
`firstPts + n/rate` throughout. The skip path was exercised separately: with the files unreachable
the theory reports one skipped and zero passed, so an absent install cannot read as a pass.
`VideoNamespaceDependencyTests` scans `CSVM.dll` for the `CSVM.Video` subject namespace and bans
every `Godot.` type, and `AssemblyDependencyScan.Violations` throws on a scan that matches nothing,
so the boundary cannot go green on an empty scan.

**⚠ The transform keeps the reference's precision loss, deliberately.** `pl_mpeg` holds its inverse
transform's scale factors to eight bits, which attenuates the topmost basis function by about 18%:
a lone coefficient at index 63 measures 16 against the standard's 19.677. ISO 11172-2 permits
exactly this mismatch between conformant decoders, and Decision 7 settles correctness at the
controls, so the arithmetic stands and the tables it comes from are pinned exactly instead.
`ThePremultiplierIsTheTransformsOwnScaleFactors` checks all 64 entries against
`round(32 * s(u) * s(v))`, and the transform test uses a documented loose bound that a wrong
constant or a transposed pass still overshoots by an order of magnitude. If anything looks soft or
ringy at the controls in `B12` or `C21`, this is the first thing to suspect and the cheapest to
change.

**⚠ The unit stage went over budget here, and `A3` brought it back.** The ten full decodes took
units from roughly 16 s to 84.6 s against a 30 s budget. `A3` split the walk rather than filtering
it: the always-on theory still opens all ten files and decodes one group of pictures from each, and
the exhaustive frame-count walk is opt-in behind `CSVM_MOVIE_WALK`. Units now run about 18 s. The
engine stage is separately over budget at about 127 s against 100 s, measured with no concurrent
load and with nothing in the engine referencing `CSVM.Video`, so that one is not this item's and
not this plan's.

### Original approach (kept for reference)

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

**Model recommendation.** Opus.

**Verify.** Frame count, dimensions and frame rate for each of the ten files match what the headers
declare, run against a real `extracted/` tree. `.\RunTests.ps1` green.

**⚠ Traps.** ⚠ Per-file parameters, not one profile. ⚠ A ported file that keeps C idioms will fight
`CheckCommentCaps.ps1` and the architecture docs forever; reshape it on the way in, not later.
⚠ There is no reference decoder to check against, and there deliberately will not be one, so a
subtly wrong VLC table produces plausible garbage rather than an error.

## A2 ☑ MPEG-1 audio layer II decode

**Landed.** Four modules added to `CSVM.Video`: `MpegAudioDecoder`, `AudioLayer2Tables` with
`Layer2Quantizer`, `AudioSubbandSynthesis` for the polyphase filter bank, and `AudioFrame`.
`MpegSystemStream` gained `AudioStream` and `AudioStartTime`, and `MpegMovie` gained `HasAudio`,
`AudioSampleRate`, `AudioChannels`, `AudioFramesDecoded` and `NextAudioFrame()`, with `Rewind()`
restarting both streams. The audio decoder opens lazily, so `B11`'s silent flag never pays for it.
`AudioFrame.Samples` is float, channel-interleaved, 1152 per channel, carrying `SampleRate`,
`Channels`, `Time` on the same 90 kHz clock as `VideoFrame.Time`, and `Duration`. Tables and the
512-entry window were generated and checked by script against the reference, and
`AudioLayer2TableTests` then checks them against ISO values spelled out in the test rather than read
back from the module.

**Verified.** `.\RunTests.ps1` PASS, exit 0, 244.3 s. Build 2.7 s with zero StyleCop warnings; units
77.2 s, 3688 passed, 0 failed, 0 skipped, against A1's 3688-minus-49; engine 127.8 s, 263 passed,
errors clean; goldens 36.7 s, 18 shots hash-identical. All ten files decode at 44100 Hz: nine plain
stereo, `crimflag` mono, none joint stereo, none dual channel, and none setting the protection bit,
so no file carries a CRC. Frame counts run from `crimflag`'s 307 to `chap0`'s 5549. The whole-file
audio checks add about 1.5 s of wall time, measured back to back with and without them, because
xunit overlaps them with A1's video decodes; that 1.5 s is the number `A3` should budget against,
not the 7 s the ten audio decodes cost run alone. With the install unreachable the audio theory
reports one skipped and 14 passed.

**The A1 contract held, with one addition.** Concatenating the packet payloads does yield a
well-formed elementary stream: every one of the ten begins with a frame header at byte 0 and every
later frame lands exactly where the previous frame's declared size puts it, with zero skips across
roughly 33000 frames. What the contract did not mention is that nine of the ten pad the tail of the
sound track with zero bytes after the last complete frame, `msopen1` ending with non-header data
instead. That is the end of the stream rather than damage, and a hunt that finds no further frame
now costs no resync, which keeps `ResyncCount` meaningful as a damage signal.

**Three defects found in `pl_mpeg`'s audio path**, none of which any of the ten files reaches, all
three fixed here and fixture-tested. Its free-format bit rate reads outside the table: it computes
`bitrate_index = read(4) - 1` and guards only `> 13`, so field value 0, the free format, gives -1
and indexes two tables at -1. Its joint stereo scales the shared code words with channel 0's scale
factor and copies the finished values to channel 1, where ISO 11172-3 transmits the code words once
but the scale factors per channel and requantises per channel, which is the point of intensity
stereo. And its rewind resets the buffer, time and sample count but not the filter bank's history,
so a replay's opening frames carry about a thousand samples of the previous pass.

**⚠ That third defect is `B11`'s, directly.** The flag loops endlessly, so it rewinds more than
anything else in the plan. `Rewind` here clears both channels' filter history and
`RewindDecodesTheSameSamplesAgain` fails against the reference behaviour.

**Goal.** The audio elementary stream decodes to PCM with timestamps, in the same headless module.

**Evidence (confidence: traced).** Every file carries MPEG-1 audio layer II at 44.1 kHz, 64 to
128 kbps, read from the audio frame headers. `crimflag.mpg` is the only mono file; `msopen1.mpg` is
stereo at 64 kbps where the other stereo files run higher.

**Approach.** The layer II half of the same `pl_mpeg` port. Same module, same no-Godot boundary.

**The contract `A1` left for this item.** `MpegMovie.AudioPackets` is an
`IReadOnlyList<MpegPacket>` in container order. Each packet carries `StreamId` (`0xC0` for all ten
files), `Time` in seconds on the container's 90 kHz clock with `MpegSystemStream.NoTimestamp`
(-1.0) where the packet carried none, and `Data` as a `ReadOnlyMemory<byte>` window into the file
bytes rather than a copy. Concatenating the payloads gives the layer II elementary stream. The
timestamps share a clock with `VideoFrame.Time`, so `C21` needs no second time base. Nothing in
`A1` decodes, inspects or validates an audio payload, so every claim about the contents of these
packets is this item's to establish. Packet counts per file are in `A1`'s Verified paragraph.

**Model recommendation.** Opus.

**Verify.** Sample rate, channel count and frame count per file match the headers. `.\RunTests.ps1`
green.

**⚠ Traps.** ⚠ Mono is not the exception to ignore: `crimflag.mpg` is the file Wave B plays, so a
stereo-only path breaks the first visible deliverable.

## A3 ☑ The test surface: spec vectors, and whole-file checks skipped when `extracted/` is absent

**Landed.** The whole-file walk is split rather than filtered. An always-on theory opens all ten
files and reads each one's declared shape, then decodes one group of pictures, 15 frames, from each,
so a file whose profile changed still fails the gate. The exhaustive per-file frame-count walk moved
behind `FullMovieWalkTheoryAttribute`, which reports **skipped with a reason naming the command**
rather than quietly not existing. `CSVM.Tests/TestData.cs` gained `FullMovieWalk`,
`NoFullWalkReason` and that attribute, which distinguishes a missing install from a missing opt-in
and says which. `docs/verification.md` gains **LOG-20**. The full ten run with:

```
$env:CSVM_MOVIE_WALK=1; .\RunTests.ps1
```

No file under `CSVM/src` was touched: no test needed a seam that did not exist.

**Verified.** Units **73.0 s before, 17.4 s after** as the agent measured it, and **18.2 s** on the
orchestrator's own run, against a 30 s budget, with `analysis/verification-budgets.json` unmodified.
Pre-A1 was 16.3 to 16.7 s, so the new coverage costs about a second. The opt-in walk costs 63 to
66 s when asked for, and all ten counts pass. `.\RunTests.ps1` PASS, exit 0, 184.4 s: build 2.8 s,
units 3721 passed with 1 skipped of 3722, engine 126.7 s with 263 passed and errors clean, goldens
36.7 s with 18 shots hash-identical and `manifest.json` unmodified in the working tree (GOLD-9).
`CheckCommentCaps.ps1`, `CheckDocEntries.ps1`, `CheckEncoding.ps1`, `CheckItemIds.ps1` and
`CheckGoldenProse.ps1` all exit 0.

**The corruption check ran, and the second one found a real gap.** Corrupting
`VideoVlcTables.CodedBlockPatternData`'s leaf value 32 to 33 failed
`CodedBlockPatternMatchesTheStandard` alone, 1 failed against 143 passed. Corrupting
`AudioLayer2Tables.QuantizerIndexData` row 2 entry 9 to 8 failed only the **new**
`EveryAllocationFieldValueSelectsTheStandardsQuantiser`, 1 failed against 50 passed, and that it
failed nothing else is the evidence that row 2 was previously untested. Both restored, with
`git diff -- CSVM/src` empty afterwards.

**Skipped-when-absent, proved rather than asserted.** With `CSVM_DATA_ROOT` and `CSVM_MPG_ROOT` both
pointed at an empty directory and no opt-in: 0 failed, 50 passed, 3 skipped of 53, in 31 ms, the
three skips being exactly the install-dependent theories while 50 fixture-driven checks still gate.

**⚠ Four gaps found in `A1`'s and `A2`'s own tests**, each now closed. `DctBlockTests` checked 3 of
64 intra-matrix entries and 4 of 64 scan positions, and a permutation check cannot see a transposed
pair, so both are exhaustive now and the scan is derived from the diagonal rule rather than
transcribed. `AudioLayer2TableTests` reached 4 of the 5 internal quantiser rows and only the two
ends of each, which is the gap the corruption landed in. The decoder's motion-vector
*reconstruction*, the VLC, the `r_size` residual, the differential carry and the range wrap, was
untested; only the sampling it feeds was. And `AVectorReachingOutsideThePlanePredictsNothing` used
plus or minus 64, which `pl_mpeg`'s flat-offset guard rejects too, so it could not distinguish A1's
row and column guard from the reference's: **a disproof taken in the one pose where the effect
cannot occur** (METHOD-1). The edge-repeat half of that same divergence had no test at all.

**⚠ Still over budget, and not this plan's.** The engine stage runs 126.7 s against 100 s, which
carries the total to 184.4 s against 180 s. `A1` measured the same figure, disowned it because
nothing in the engine references `CSVM.Video`, and `A3` reproduced it independently. It predates
this work.

### Original approach (kept for reference)

**Goal.** `.\RunTests.ps1` gates the decoder on a machine with no game install, and gates it harder
on a machine with one, without the landing gate costing five times what it did.

**⚠ Inherited from A1: the unit stage is over budget.** The ten full video decodes cost about 55 s,
taking units from roughly 16 s to the high seventies against a 30 s budget. **The cost is A1's video
walk, not A2's audio walk**: measured back to back, A2's ten whole-file audio checks add about 1.5 s,
because xunit overlaps them with the video decodes already running. Run alone they would cost about
7 s. So a switch that gates the video walk buys nearly all of the time back, and gating the audio
walk on its own buys almost nothing. `MpegAudioTests.EveryCinemaDecodesItsDeclaredSoundTrack` never
decodes video and is cheap enough to leave on. Put the whole-file video walk behind a switch, or
reduce it to a sample with the full ten opt-in. Do not resolve this by raising the budget in
`analysis/verification-budgets.json`: the budget is the tripwire, and moving it hides the thing it
was put there to show. `A1` also left `MovieDataFactAttribute`/`MovieDataTheoryAttribute` in
`CSVM.Tests/TestData.cs`, which probes `CSVM_MPG_ROOT` and then the install path with `crimflag.mpg`
as its marker; folding it into the existing extraction probe is this item's call to make.

**Evidence (confidence: lead-only).** `PROJECT_CONTEXT.md:101` describes `CSVM.Tests` as "reader
units on hand-authored fixtures + `extracted/` golden counts, skipped when absent", which is the
shape this needs, but no fixture for a compressed bitstream exists yet and the exact vector set is
unchosen.

**Approach.** Unit-test the pure pieces against tables written out of ISO 11172-2 itself: the
bitstream reader, the VLC tables, dequantisation, the IDCT, motion compensation. Those are
hand-authored fixtures and commit freely. The whole-file checks ride the existing skipped-when-absent
pattern. Per Decision 7, ffmpeg may be used once informally on a development machine as a sanity
check and is never committed and never shipped.

**Model recommendation.** Opus.

**Verify.** The suite passes with `extracted/` absent and with it present. Deliberately corrupt one
VLC table entry and confirm a test fails, because an unchanged number is not evidence until you have
seen it able to fail.

**⚠ Traps.** ⚠ MPEG-1 permits IDCT mismatch between conformant decoders, so any test that asserts
exact pixel equality against another decoder's output is wrong even when both decoders are right.
⚠ No `.mpg` from the install may be committed as a fixture.

## A4 ☑ The port's third-party notice

**Landed.** `packaging/LICENSE-plmpeg` holds the MIT terms with `Dominic Szablewski` as the holder
and no year, because upstream declares MIT by SPDX identifier alone: the repository ships no
`LICENSE` file and the header carries no licence block in its 3614 lines. The terms therefore live
in this repo rather than being read out of a shipped artefact, and both
`packaging/BuildThirdPartyNotices.ps1` and `packaging/MANIFEST.md` say so at the point a reader
would otherwise assume the opposite. The notice is section 7, guarded by a `Test-Path` whose error
explains why the text is local. `packaging/MANIFEST.md` gains a row for the new file and records
that it takes no `$ReleaseFiles` entry, since a licence file shipped loose would be a stray. The
notice covers source this project ported, not a binary it redistributes, and is worded that way.

**Verified.** The regenerated `LICENSE-thirdparty.txt` reproduces every upstream text and the whole
74-crate enumeration byte-identically, so the run read the same artefacts the committed file was
built from; the diff is section 7 plus the header lines that count the sections. `CheckEncoding.ps1`
reports no mojibake over the whole tree. The generated file has no BOM, 7119 CRLF pairs and no bare
LF, and decodes clean under a strict `UTF8Encoding($false, $true)` over all 367626 bytes. The
version lock is `ExportRelease.ps1:108-119`, three `[regex]::Match` calls over the whole document
for the Godot build, the .NET runtime version and the `mech3ax cs-anim` commit; each still matches
exactly once, inside the header, at the values the export compares against. Section 7 adds no fourth
stamp on purpose, because ported source moves with CSVM's own commit, which `BUILD-INFO.txt`
already records, and an unchecked stamp is a claim nothing enforces.

**⚠ Author review.** `packaging/README.md` gained a clause naming pl_mpeg in the recipient-facing
list of what `LICENSE-thirdparty.txt` covers. `MANIFEST.md` marks that file author-reviewed before
hand-off, so the clause needs your eyes rather than a check's.

**⚠ The Evidence below was wrong about the section count.** The script assembles six sections, not
three, so this notice is the seventh and not the fourth. The `Section` helper it named was right.

### Original approach (kept for reference)

**Goal.** The release carries `pl_mpeg`'s MIT notice, assembled the same way every other notice is.

**Evidence (confidence: lead-only).** `packaging/BuildThirdPartyNotices.ps1` assembles
`LICENSE-thirdparty.txt` from three sections (Godot, the .NET runtime, `unzbd`'s crates) through a
`Section` helper, and `packaging/MANIFEST.md` records that the export refuses to ship the file
against payload versions it does not speak for. A fourth section for this project's own third-party
source is a natural extension, but no such section exists yet and its exact shape is unchosen.

**Approach.** Add a fourth `Section` call reading the MIT text from a file in `packaging/`, and the
corresponding row in `packaging/MANIFEST.md`. Keep the script pure ASCII and write through the .NET
APIs, per the header's own note about PowerShell 5.1 double-encoding copyright signs.

**Model recommendation.** Opus.

**Verify.** `packaging/BuildThirdPartyNotices.ps1` regenerates cleanly, `CheckEncoding.ps1` passes,
and the generated file contains the notice.

**⚠ Traps.** ⚠ The notices file is version-locked and the export throws when the header disagrees
with what it is packaging; a new section must not break that check.

## A5 ☑ The extraction copies the ten `.mpg` files into `extracted/`

**Landed.** `ExtractRof.ps1` copies `GRAPHICS\MPG\*.mpg` from the install into
`<Dest>\ASSETS\GRAPHICS\MPG`, verbatim, and records the count as a `movies` field in the `rof`
section of `VERSION.json`. The copy runs under `-Raw` as well, because it decodes nothing. It is
idempotent on length, it takes whatever the folder holds under the name the folder holds it under,
and it names any of the ten that is missing rather than passing an incomplete install as a silent
success. An absent `GRAPHICS\MPG` is a warning, not a throw, and says what the consequence is.
`docs/formats/extraction.md` gains its row in the support matrix.

**⚠ The schema bump this item's Approach called for was correctly refused.** The Approach said to
bump `VERSION.json`'s schema and update every reader. The agent declined, on the rule that an added
output invalidates nothing until a reader requires it, which is the rule the last bump obeyed and
`docs/formats/menu-layout.md` states. No build opens a movie file yet, so bumping today would make
every currently valid extraction refuse to load on the strength of a file nothing reads. The stamp
stays at schema 2, `ExtractionStamp.cs` needs no change, and the first reader that actually requires
a movie bumps all three numbers together. **`B12` is that reader**, so the bump belongs to it.

**Verified.** Run against the real install into a scratch destination. First pass: 847 archive files
and `GRAPHICS\MPG (10 copied, 0 already current)`, 106 MB copied verbatim, in 1.4 s. Second pass:
`0 copied, 10 already current`, in 0.6 s, so the copy is idempotent and a re-extraction pays
nothing. All ten compared against the source by SHA-256, byte for byte: `chap0` 18696196,
`chap1` 16287748, `chap2` 15360004, `chap3` 11563012, `chap4` 17147908, `chap5` 11966468,
`crimflag` 970756, `final` 13568004, `msopen1` 2664262, `zipper` 2535428, zero mismatches. The
stamp reads `"schema": 2` with `"movies": 10`. The target sits under `/extracted/`, which is
`.gitignore` line 4, so no `.mpg` can reach a commit. `.\RunTests.ps1` PASS, exit 0, 243.1 s: 3688
units, 263 engine, 18 goldens hash-identical.

### Original approach (kept for reference)

**Goal.** After a recipient runs the extraction, the ten files sit under the data root where
`SessionPaths` can reach them, so every later item can name a file instead of being handed bytes.

**Evidence (confidence: traced).** Nothing puts them there today, checked four ways. The files are
loose in the install at `GOSDATA\ASSETS\GRAPHICS\MPG\`, outside the `.rof` archive, 10 files and
106 MB. `extracted/rof/ASSETS/GRAPHICS/MPG/` exists but is empty, a directory entry the archive
carries with no payload behind it. `ExtractRof.ps1` contains no occurrence of `MPG`, `GRAPHICS`,
`.mpg` or `Copy-Item`. `SessionPaths.cs` resolves only `dataRoot/extracted/...` and has no concept
of an install root, so reading them in place would need a path concept the project does not have.
For scale, the extraction is already 1534 MB across 61011 files, so the copy grows it by about 7%
and grows the 78 MB download by nothing.

**Approach.** `ExtractRof.ps1` copies the ten files verbatim from the install into
`extracted/rof/ASSETS/GRAPHICS/MPG/`. A copy, never a conversion. Record the step in
`extracted/VERSION.json`'s `rof` section and bump its schema, then find every reader of that schema
number and update it in the same turn. Make the copy idempotent and skip a file already present at
the right size, or a re-extraction pays 106 MB of copying it did not need to.

**Model recommendation.** Opus.

**Verify.** Run the extraction against the real install and confirm the ten files arrive with
byte-identical lengths, then re-run it and confirm the second pass copies nothing. `git status` must
stay clean afterwards, which is the check that the copy landed somewhere ignored.

**⚠ Traps.** ⚠ **No `.mpg` may ever be committed.** The target must be inside the ignored
`extracted/` tree, and that must be confirmed rather than assumed, because a 106 MB accident here is
exactly what `CheckCommitContent.ps1` exists to catch and exactly what it would be embarrassing to
need. ⚠ Four of the ten names are spelled in a case the data does not have, which Windows forgives
and a case-sensitive reader would not; keep the on-disk names verbatim and resolve
case-insensitively rather than renaming on the way in. ⚠ The schema bump is only safe once every
reader of it is found; a bumped number nothing reads is worse than no bump at all.

---

# Wave B — the flag background

## B11 ☑ The frame surface: decoded frames as an `ImageTexture`, on a playback clock

**Landed.** `MoviePlayback` in `CSVM.Video` owns the clock and the loop decision: an `MpegMovie`,
the elapsed time it has been played for, and the picture due now as RGBA in a buffer it rewrites in
place. Which picture is due comes from the frames' own presentation timestamps, so no rate is
written down anywhere and the two files that break the otherwise uniform profile need no case of
their own. A play count of zero plays endlessly, and every pass after the first restarts through
`MpegMovie.Rewind` and nothing else. `MovieSurface` in `CSVM.UI` is the other half: one
`ImageTexture` made once and updated in place, three engine calls, and `Open` answering null for a
file that will not read, because a screen missing its background still has everything else on it.
There is no node of any kind.

**The seam is enforced rather than intended.** `VideoNamespaceDependencyTests` already checks
`CSVM.Video`'s compiled metadata for engine types, so a clock placed below the boundary is
Godot-free by a test and not by convention, and `CSVM.Tests` being engine-free means anything with
a decision in it had to sit there to be testable at all. What is left above the seam is the upload.

**⚠ A `VideoFrame` is valid only until the next `NextFrame`**, so catching up across several due
pictures copies each one before pulling the next. That copy is the BT.601 conversion the texture
needs anyway, which is why the playback owns an RGBA buffer rather than handing out a frame. It
also reads one picture ahead, which is what lets it answer "not yet due" without consuming one.

**The lazy audio open survives.** `MoviePlayback` never reads `HasAudio`, `AudioSampleRate` or
`NextAudioFrame`, and `MpegMovie.Rewind` rewinds an audio decoder only when one was opened, so an
endlessly looping silent flag never pays for the audio path. `AMovieWithNoSoundTrackStillPlays`
pins that a movie with no audio packet at all still drives its clock.

**The check the Verify TODO asked for.** `MoviePlaybackTests` pins the moment a picture becomes due
from both sides. METHOD-1 rules out counting frames over a window, since 30000/1001 and 30 differ
by 0.3 frames over ten seconds, so the check drives the clock in 0.1 ms steps and asserts the count
either side of the boundary instead. The always-on synthetic rows put picture 300 at 11.96 s,
9.9766 s and 9.9667 s for sequence rate codes 3, 4 and 5; the skipped-when-absent
`TheCinemaClockRunsAtTheFilesOwnRate` shows `msopen1.mpg`'s sixteenth picture at 0.5005 s where
`crimflag.mpg`'s arrives at 0.5 s.

**Verified.** `.\RunTests.ps1` on the merged tree, run by the orchestrator rather than reported by
the agent: PASS, exit 0, 217.0 s. Build 3.4 s with zero StyleCop warnings; units 21.7 s against a
30 s budget (3761 passed, 0 failed, 1 skipped, the skip being `A3`'s opt-in whole-file walk);
engine 150.5 s, 265 passed, errors clean; goldens 41.5 s, 18 shots hash-identical with
`analysis/goldens/manifest.json` unmodified in `git diff` (GOLD-9). METHOD-9 exercised on the new
check: replacing the timestamp comparison with a hardcoded `n/30.0` fails exactly the 25 fps row,
the 30000/1001 synthetic row and `msopen1.mpg`, and leaves both 30 fps rows green.

**⚠ Still over budget on the engine stage, and still not this plan's.** 150.5 s against 100 s,
where `A1` measured 145.6 s and `A3` 126.7 s. Nothing in the engine constructs a `MovieSurface`.

**⚠ The Godot half is unexercised by any automated check**, and cannot be one from here:
`CSVM.Tests` is engine-free, and an in-engine suite would need a movie file the extraction does not
yet hold. `Image.SetData` and `ImageTexture.Update` compile and nothing more is proven about them.
`B12` is the first thing that runs them.

### Original approach (kept for reference)

**Goal.** A decoded stream drives a texture that updates at the file's own frame rate and loops
endlessly when asked, with no Godot node beyond the texture itself.

**Evidence (confidence: traced).** `ComposedBoard` resolves a screen into `Fills`, `Pictures` and
`Lines` in the original's 800x600 space as `BoardPicture(art, x, y)`, and `BoardFit` maps that board
onto the window with one uniform scale (`docs/architecture.md`, `src/UI/BoardFit.cs`). The
layout's `Loops` field is a play count in which zero means endless.

**Approach.** A surface that owns a decoder from A1, a clock and an `ImageTexture`, exposing the
texture for the composition to draw. Feed it the layout row's `Loops`. Nothing here knows which
screen it is on.

**Model recommendation.** Opus.

**Verify.** A headless check that pins the moment a picture becomes due rather than counting frames
over a window, so 30000/1001 is distinguishable from 30, with the synthetic rate codes always on
and the two real files in the skipped-when-absent half.

**⚠ Traps.** ⚠ The flag plays silent (Decision 5), so this surface must not assume an audio stream
exists to drive its clock; the video timestamps are the clock for Wave B. `A2` made the decoder open
lazily for exactly this reason, so a silent flag never pays for the audio path. ⚠ **This item
rewinds more than anything else in the plan**, because the flag loops endlessly, and rewind is where
`pl_mpeg` is broken: its own rewind resets the buffer, time and sample count but not the filter
bank's history, so a replay's opening frames carry about a thousand samples of the previous pass.
`A2` fixed that and pinned it with `RewindDecodesTheSameSamplesAgain`. Do not reintroduce it by
adding a cheaper reset path here.

## B12 ☑ `CrimFlag.MPG` composed into `MainMenu` and `Preferences`

**Landed.** `BoardArtLibrary` gained a fourth member, `Movie`, whose name carries its extension and
resolves under `extracted/rof/ASSETS/GRAPHICS/MPG/`, so a background film reaches `ComposedBoard`
without an engine type entering the engine-free half. The resolution happens where every other
library's does, in `ComposedBoardView.Load`: a movie opens one `MovieSurface`, the texture cache
holds that surface's single `ImageTexture`, and the surface rewrites its pixels in place, so the
picture animates with nothing invalidated and a file that will not open caches a null and is tried
once. `OriginalShell.ComposeMovie` puts the row at the bottom of the backdrop for the two screens
whose section authors one, taking the corner from the row's `X` and `Y` and the size from the
measurer's answer scaled by the row's own `ScaleX`/`ScaleY`, so neither 320x240 nor 250 is written
down anywhere. `OriginalPresentation.Measure` answers a movie's size from its sequence header
through a throwaway `MpegMovie`, since no bitmap loader can read one, and its `Tick` runs
`AdvanceMovies` off the step the host was given, repainting on the frames a new picture arrived on
and recomposing on none of them.

**The manifest learned a third class of row.** `MainMenu.MOVIE` and `Preferences.MOVIE` were listed
as not drawn, and they are drawn now while staying optional, which `NotDrawn` cannot express. A
`Degrades` table beside it holds a row Original draws whose file the screen survives the absence of,
and the two movies sit there: a top level with no film behind it is the screen it always was on a
plain ground. `OriginalAvailability.RelativeArtPath` puts a `.mpg` one directory deeper than the
bitmaps the same rows name, which is the base `FUN_004a7c70` resolves every movie name under, and
the manifest records that path per entry rather than assembling one of its own. Manifest schema 5.
The layout decode's `artMissing` count is 0 where it was 2, because the two names the archive does
not carry are these files and the extraction now copies them in.

**⚠ The capture had to stop reading the wall clock, and the harness had to stop reading `sim_frame`.**
A menu screen has no session, so a movie behind one advanced on `_Process`'s own delta and the
picture a shot landed on was a property of this machine's frame times (DET-7). `Launcher.MenuStep`
hands the menus `GameClock.FixedDt` under `--det` and the frame delta otherwise, which makes a
capture's picture a function of the frame count. The saved-shot line now carries
`frame=N clock=<sim|render>`: a run with a session reports its sim clock, a screen without one
reports the rendered frames its countdown waited, read off `Engine.GetProcessFrames` rather than off
the countdown it would otherwise only ever agree with itself about. `RunTests.ps1` compares that one
number against the manifest's `frame` and names the counter in the failure.

**The schema bump `A5` deferred landed here, with a test in place of three comments.**
`ExtractionStamp.Schema`, `ExtractRof.ps1`'s `$StampSchema` and `ExtractAssets.ps1`'s all moved from
2 to 3 in this item's commit, so a tree extracted before the copy step is refused with the
re-extract instruction rather than opened on a main menu with nothing running behind it.
`CSVM.Tests/ExtractionStampTests.cs` reads the number out of all three and fails a bump that moves
fewer than all of them.

**Verified.** The flag was judged at the controls on both screens and passed, which is the check
Decision 7 puts with the author and no instrument here replaces. `.\RunTests.ps1` on the plan tree,
run by the orchestrator rather than reported by the agent: PASS, exit 0, 212.6 s. Build 3.3 s with
zero StyleCop warnings; units 20.7 s against a 30 s budget (3766 passed, 0 failed, 1 skipped of
3767, the skip being `A3`'s opt-in whole-file walk); engine 149.0 s, 265 passed, errors clean;
goldens 39.7 s, **20** shots hash-identical, the two new ones among them. Those two were pinned in
one process set and reproduced in another, so the render clock's frame 120 is the same picture
twice. `analysis/goldens/manifest.json` carries the two added entries and nothing rewritten after
the run, which is the GOLD-9 read that a hash-identical report is worth anything at all. The layout
decode's own counts move only where the movies are: `artMissing` 2 to 0, every other number in
`MenuLayoutDecoderTests` untouched.

**⚠ Still over budget on the engine stage, and still not this plan's.** 149.0 s against 100 s,
where `A1` measured 145.6 s, `A3` 126.7 s and `B11` 150.5 s. Nothing in the engine plays a movie.

### Original approach (kept for reference)

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

**⚠ That Approach is wrong about `BoardPicture`, and `B11` found it.** A `BoardPicture` carries a
`BoardArt(BoardArtLibrary Library, string Name, int Frames)`, a name resolved through a library,
and `ComposedBoard` is deliberately engine-free so it cannot hold a `Texture2D` at all. The
resolution happens in `ComposedBoardView.Load(BoardArt)`, which maps library and name to a file
path and caches `Texture2D?` by that path. The cheap seam is a fourth `BoardArtLibrary` member
whose `Load` arm answers a live `MovieSurface.Texture`: the surface updates that same
`ImageTexture` in place, so the path cache needs no invalidation and the picture animates without
further work. This item still drives `MovieSurface.Advance` once a frame from wherever the view
ticks.

**⚠ The extraction has not been re-run since `A5` landed.**
`extracted/rof/ASSETS/GRAPHICS/MPG/` holds no files, so the ten movies are reachable only at the
install path, which is what the tests probe. This item resolves through `SessionPaths` and finds
nothing until the extraction runs again; its own schema bump then refuses the extraction made
before the copy step existed, which is the point of the bump.

**Deterministic capture, for the golden trap below.** `FramesShown` is a pure function of the
accumulated clock and `MoviePlayback` caps a single step at 0.25 s, so one enormous delta cannot
decode a whole movie. A capture path that wants picture N steps the surface N times rather than
passing one large delta.

**⚠ This item owns the extraction schema bump `A5` deferred.** `A5` added a `movies` count to
`VERSION.json` without moving the schema, because nothing read a movie file yet and a bump would
have refused every valid extraction on the strength of an unread output. This item is the first
reader. Bump `ExtractRof.ps1`'s `$StampSchema`, `ExtractAssets.ps1`'s and
`CSVM/src/Session/ExtractionStamp.cs`'s `Schema` together, in this item's commit, so an extraction
made before the movies were copied is refused rather than starting into a screen with no flag
behind it.

**Model recommendation.** Opus.

**Verify.** Both screens at the controls, judged by the user, per Decision 7. Then pin goldens for
both as the drift tripwire, and confirm `git diff` shows `analysis/goldens/manifest.json` unmodified
before believing any "identical" report (GOLD-9).

**⚠ Traps.** ⚠ A golden of a looping movie is only stable if the shot lands on a deterministic
frame; pin the surface to a fixed frame index for capture rather than to wall time, or every run
re-baselines. ⚠ Do not correct the softness. At 320x240 scaled 2.5x into the board and then again by
`BoardFit`, the flag is soft on a modern display, and that is the original's own look (Decision 1).

---

# Wave C — the cinema sequence

**The seam `C21` left for `C22`, `C23` and `C24`.** One call plays a named cinema and tells you when
it stopped:

```csharp
launcher.PlayCinema("chap3", () => /* hand off here */, CinemaScreen.ChapterKeys);
```

`Launcher.PlayCinema(string name, Action then, CinemaSkip skip)` resolves the name case-blind under
`extracted/rof/ASSETS/GRAPHICS/MPG/` (`.mpg` supplied when none is spelled), mounts one
`UI.CinemaScreen` on `HudLayers.Cinema` over whatever is on screen, and runs `then` on the frame the
cinema stops, played out or skipped. A file that will not read logs a warning and runs `then` at
once, so a flow never stalls on a cinema an install does not carry, and a sequence of movies is
those calls chained. `CinemaScreen.BootKeys`, `.ChapterKeys` and `.ClosingKeys` are the three
authored skip sets (`C22`, `C23`, `C24` in that order); `CinemaSkip` is the flags type behind them
and the per-cinema differences are the original's, not an oversight to unify. `CinemaScreen.Stop()`
ends one from outside, `Finished` and `FramesShown` are what a suite reads, and `--movie=<name>`
plays one from the command line through the same call.

## C21 ☑ Audio playback and A/V sync from the stream's presentation timestamps

**Landed.** `CinemaPlayback` in `CSVM.Video` is the sync, engine-free and unit-tested: it owns a
`MoviePlayback` for the picture and hands the movie's own track out as clamped PCM, and **the clock
is the sound the device has actually played**, not the frame delta. A device consumes at exactly the
rate it was opened at where a frame callback does not, so the picture cannot drift away from the
sound however long the file runs; `Advance(elapsedSeconds, soundFramesPlayed)` is the whole seam,
and the wall step drives the picture only for a movie carrying no track and for the tail past the
last sample. `CinemaScreen` in `CSVM.UI` is the engine half: the `ImageTexture` the pictures upload
into, the `AudioStreamGenerator` the samples are pushed to, the skip, and nothing else. The picture
fills the same 800x600 rectangle `BoardFit` maps every board into, so a cinema and the screen it
hands off to own one area of the window.

**The bus is `Voice`.** The nine cinemas are narrated films rather than score or world sound, and
`AudioBuses.Voice` is already the briefing narration's channel, which is the closest thing the mix
has to a non-diegetic narrated presentation. `Music` is documented as `MusicPlayer`'s one player and
nothing else, and `Effects` is what the world and the aircraft make; neither describes a cinema.

**`--movie=<name>` is the way in, and the seam C22 to C24 reuse.** It plays one cinema over the
whole window and quits, with no world, no menu and no session behind it. The name resolves
case-blind through `SessionPaths.Cinema` and takes `.mpg` when none is spelled, so `--movie=zipper`
and `--movie=CrimFlag.MPG` both work; `ComposedBoardView` now resolves `B12`'s flag through that
same member rather than assembling a path of its own. It deliberately does **not** imply `--det`:
the clock is the audio device's, so a fixed-step sim clock would say nothing about what this flag
exists to show.

**⚠ The Approach's second trap is wrong, and the data says so.** It reads "`AudioStartTime` is
nonzero in all ten files (roughly 0.04 to 0.22 s)" as an offset to honour. It is not one:
`VideoStartTime` carries **the same value to the tick in nine of the ten**, so those nine share an
origin, and the tenth goes the other way. `msopen1.mpg` starts its sound at 0.2177 s against its
picture's 0.2844 s, 0.0667 s **earlier**, so the correction there is a lagged picture clock and not
a silence pad. Both directions are implemented and neither discards a sample; the table is in
[`docs/formats/cinemas.md`](formats/cinemas.md) and the transferable rule is **SRC-10**.

**⚠ Two of the ten run their sound out before the picture, so the sound cannot be the only clock.**
`chap0.mpg`'s track ends 0.013 s and `msopen1.mpg`'s 0.015 s before their last picture's own display
interval expires. A clock that stopped with the sound would leave those two never finishing, so past
the last sample the picture runs on the caller's step. The Approach's fifth trap says sound outlasts
picture in every file by 0.01 to 0.25 s; measured against the moment the last picture is **put up**
that holds, at 0.018 to 0.054 s, and measured against the moment that picture's interval **expires**
it fails for those two. `ACinemaWhoseSoundEndsFirstStillFinishes` pins it.

**The other three traps held.** Six of the ten peak above 1.0 rather than five, up to `chap3.mpg`'s
1.0924, and `ReadSound` clamps; the synthetic fixture peaks at 1.3524 through the decoder alone, so
`EverySampleIsClamped` can fail (METHOD-9). `MoviePlayback.Clock` is indeed the video's own origin,
which is why the offset is taken here. And drift is checked over whole files rather than over a
window: `APlayedCinemaTracksItsSoundToTheLastPicture` plays `crimflag.mpg` and `zipper.mpg` end to
end under `CSVM_MOVIE_WALK`, and the worst gap between the picture's clock and the sound handed over
is under a millisecond across both.

**Verified.** `.\RunTests.ps1` on the plan tree, run by the orchestrator rather than reported by the
agent: PASS, exit 0, 280.1 s. Build 3.8 s with zero StyleCop warnings; units 3774 passed, 0 failed,
2 skipped of 3776, against `B12`'s 3766/0/1 of 3767, the second skip being this item's own opt-in
whole-file walk; engine 265 passed, errors clean; goldens 20 shots hash-identical with
`analysis/goldens/manifest.json` unmodified in `git diff` afterwards (GOLD-9), the two flag shots
among them, which is what clears the resolver this item rerouted `B12`'s flag through. The agent's
three live runs reproduce: `--movie=CrimFlag.MPG` ends itself at `frames=240 clock=8.008s`,
`--movie=zipper` at `frames=608 clock=20.274s`, and `--movie=nosuchfilm` warns and exits 0.

**The new tests cost nothing measurable, which was measured rather than assumed.** That battery run
put every stage over budget, units at 35.0 s against 30 s where `B12` measured 20.7 s, so the
question was whether this item's eight checks did it. Run back to back on one build, the unit stage
is **24 s with them filtered out and 22 s with them in** (3767 tests against 3776), so they overlap
with the decodes A1's walk already pays for, the way `A2`'s audio walk did. Four other worktrees were
under load on the same machine during the battery, and the engine and golden stages moved with it in
the same direction; the per-item measurement is the one that answers this item's question, and it
answers it no.

**⚠ What no instrument here can answer.** Whether the sound is in step with the picture at the
controls is the user's judgement and Decision 7 puts it there. Run
`.\RunGame.ps1 -- --movie=chap1 --volume=1.0`, or `RunProbe.ps1` with `--volume=1.0`, and watch a
whole file: `chap0` is the longest at 145 s and `chap3` the shortest of the chapter set at 93 s.
A check on the first thirty seconds proves nothing.

### Original approach (kept for reference)

**Goal.** A cinema plays with its audio in sync from the first frame to the last, on the project's
audio bus.

**Evidence (confidence: traced, upgraded by `A2`).** The premise was the author's recall, since the
`movie` widget row has no audio field of any kind and the engine side of the player was not traced.
`A2` decoded all ten tracks and settled it from the data: **`crimflag.mpg`'s track is digital
silence**, every sample exactly zero across all 307 frames, while the other nine peak between 0.964
and 1.092. The flag playing silent is a property of the file, not only of the presentation, so Wave
B needs no mute of its own and this item's scope is unchanged.

**Approach.** Push A2's PCM to an `AudioStreamGenerator` and drive the video clock from the system
stream's presentation timestamps. Route onto the bus layout `PLAN-audio-preferences` landed, which
is now on this branch: `CSVM/src/Utils/AudioBuses.cs` and `CSVM/default_bus_layout.tres` name the
buses, so this item picks one rather than reaching `MasterBus` directly.

**⚠ `B11`'s `MoviePlayback.Clock` has the video's origin, not the container's.** It is seconds into
the current pass measured from the first video frame's own timestamp, on the same base as
`VideoFrame.Time`, so this item takes `AudioStartTime` against `VideoStartTime` separately rather
than assuming the two streams share an origin.

**Model recommendation.** Opus.

**Verify.** The user watches a full cinema end to end and confirms the audio has not drifted by the
end, which is the failure this item exists to prevent.

**⚠ Traps.** ⚠ Drift accumulates, so a check on the first thirty seconds proves nothing; the
verification is a whole file. ⚠ This item depends on `PLAN-audio-preferences`'s bus layout, which is
in flight; do not invent a bus here. ⚠ **Clamp the samples.** Five of the ten peak above 1.0, up to
1.092, which is normal for layer II and will clip audibly if fixed-point output is fed unclamped.
⚠ **Do not start both streams at zero.** The first audio packet carries a presentation timestamp in
all ten files, so `AudioStartTime` is nonzero everywhere, between roughly 0.04 and 0.22 s; honour it
against `VideoStartTime` rather than assuming a common origin. ⚠ Sound outlasts picture in every
file, by 0.01 to 0.25 s, so a few frames of audio tail after the last picture is correct and is not
a sync bug to chase.

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

**Model recommendation.** Opus.

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

**Model recommendation.** Opus.

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

**Model recommendation.** Opus.

**Verify.** <TODO: name how a completed campaign is reached or simulated for this check>

**⚠ Traps.** ⚠ Escape and left mouse only. Space and Return do nothing here and do something in C23,
and that difference is authored, not accidental.
