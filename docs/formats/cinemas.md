# Cinemas (`GOSDATA\ASSETS\GRAPHICS\MPG\`)

Part of the [format documentation](README.md). This page describes the ten video files the retail
install ships, what they actually are as containers and codecs, where the original names them from,
and how CSVM plays them. CSVM decodes them itself, in `CSVM.Video`; nothing composes a decoded
frame on screen yet.

## Contents

- [The shipped set](#the-shipped-set)
- [Container and codecs](#container-and-codecs)
- [Where the files are named from](#where-the-files-are-named-from)
- [How the original plays them](#how-the-original-plays-them)
- [Playing them in CSVM](#playing-them-in-csvm)
- [Frame counts and durations](#frame-counts-and-durations)
- [Evidence and limits](#evidence-and-limits)

## The shipped set

Ten files, about 110 MB together, all with the `.mpg` extension:

| File | Size | Role |
|---|---|---|
| `msopen1.mpg` | 2.5 MB | publisher logo, first thing the intro sequence plays |
| `zipper.mpg` | 2.4 MB | developer logo, second |
| `chap0.mpg` | 17.8 MB | the opening cinema |
| `chap1.mpg` .. `chap5.mpg` | 11.0 to 16.4 MB | one per story chapter |
| `final.mpg` | 12.9 MB | the closing cinema |
| `crimflag.mpg` | 0.9 MB | the looping front-end background, and the only mono-audio file in the set |

Nine of the ten are cinemas in the ordinary sense, played once for the player to watch.
`crimflag.mpg` is not: it is a decorative loop that runs behind four front-end screens as their
backmost layer, which is why it is a tenth the size of the others and the only one authored in mono.

They sit loose in the install under `GOSDATA\ASSETS\GRAPHICS\MPG\`, outside the `.rof` archive that
holds the rest of the front end's art. The archive carries an `ASSETS/GRAPHICS/MPG` directory entry
with nothing behind it, so `ExtractRof.ps1` copies the ten files in verbatim and every runtime
lookup resolves under `extracted/rof/ASSETS/GRAPHICS/MPG/`, one directory deeper than every bitmap
the same layout rows name. A tree extracted before that copy step existed is refused by the
extraction stamp's schema rather than opened on a screen with nothing running behind it
([menu-layout.md](menu-layout.md) has the rule each bump obeys).

## Container and codecs

Every file is the same profile, read out of its own headers:

| Layer | What it is |
|---|---|
| System | MPEG-1 system stream (ISO 11172-1). The first pack header carries the `0010` marker, never MPEG-2 program stream's `01`. One video elementary stream (`E0`) and one audio elementary stream (`C0`), multiplexed under `BA` pack and `BB` system headers |
| Video | MPEG-1 video (ISO 11172-2), 320x240, square pixel aspect, 856 to 1500 kbps. No sequence extension start code (`B5`) follows any sequence header, which is what would make it MPEG-2 |
| Audio | MPEG-1 audio layer II, 44.1 kHz, 64 to 128 kbps |

Two files break the otherwise uniform profile, so a reader must take these per file rather than
assume one set of parameters:

- `msopen1.mpg` is 29.97 fps and 1500 kbps; every other file is 30 fps at 856 to 889 kbps.
- `crimflag.mpg` is mono at 64 kbps; every other file is stereo. `msopen1.mpg` is stereo at 64 kbps.

Decoding the audio adds four facts the headers alone do not give. Every track runs at 44100 Hz; the
nine stereo files are all plain stereo, never joint stereo and never dual channel, and no file sets
the protection bit, so none carries a CRC. **`crimflag.mpg`'s track is digital silence**, every
sample exactly zero across all 307 of its audio frames, where the other nine peak between 0.964 and
1.092: the flag is silent in the data rather than only in the presentation. Five of the ten peak
above 1.0, which is ordinary for layer II and means a player feeding fixed-point output has to
clamp. Sound outlasts picture in every file, by 0.01 to 0.25 s, and the first audio packet carries a
presentation timestamp in all ten, between roughly 0.04 and 0.22 s, so the two streams do not share
an origin. Nine of the ten pad the tail of the sound track with zero bytes after the last complete
frame; `msopen1.mpg` ends with non-header data instead. That padding is the end of the stream, not
damage.

## Where the files are named from

Three places name them, and none is a directory scan. The executable itself names none of them.

**`fmv.zrd`, as `PLAYAVI` actions**, holds the boot sequence: its `INTRO` block plays `MSopen1.mpg`,
waits, fades and plays `zipper.mpg`, and its `CHAP0` block plays `Chap0.mpg`. Those are the only
three `.mpg` strings in the whole extracted reader set, and the block's `IMAGE_PATH` is `video`.

**`ASSETS/LAYOUT.CSV`, as `movie` widgets**, holds every other placement. A `movie` row is widget
type `M` with script class `@ctl@AL`, and it carries an `ArtPath`, a position, a `Loops` count and a
scale:

| Screen | Widget key | `ArtPath` | `Loops` |
|---|---|---|---|
| `MainMenu` | `MOVIE` | `CrimFlag.MPG` | 0 |
| `Save` | `MOVIE` | `CrimFlag.MPG` | 0 |
| `Load` | `MOVIE` | `CrimFlag.MPG` | 0 |
| `Preferences` | `MOVIE` | `CrimFlag.MPG` | 0 |
| `CampaignIntro` | `CM_MOVIE` | `CrimFlag.MPG` (a placeholder, see below) | 1 |
| `FinalCinema` | `CF_MOVIE` | `Final.MPG` | 1 |

Every one of the six sits at X, Y and Z all zero with the whole screen as its region, so the movie
is the backmost layer and the rest of the screen draws over it. All six carry `ScaleX` and `ScaleY`
of 250, and every one is a percentage of the 320x240 source against the original's fixed 800x600
authored dialog space: 320 x 2.5 is 800 and 240 x 2.5 is 600, so the movie fills that space exactly,
uniformly, at the same 4:3 aspect, with no crop and no letterbox. `Loops` is a play count in which
zero means endless, which is what separates the four background rows from the two cinema rows.

**`ASSETS/SCRIPTS/CAMPAIGNINTRO.SCRIPT` builds the chapter names at runtime.** Its `gui_create` runs
`callback($$E$$, 2151, FC)`, which fills `FC` with the chapter number, and its `gui_init` then sets
the control's art path to `"chap" conv$(FC) ".mpg"`. So the chapter-to-file mapping is the identity,
chapter N plays `chapN.mpg`, and the `CrimFlag.MPG` in that screen's layout row is a placeholder
overwritten before the control initialises. `FINALCINEMA.SCRIPT` sets no art path, so its screen
plays whatever its `CF_MOVIE` row names.

Four of the ten files are named in a case the on-disk names do not have, so a case-sensitive lookup
fails on `msopen1.mpg`, `chap0.mpg`, `crimflag.mpg` and `final.mpg`. `zipper.mpg` is spelled to
match, and the script-built chapter names are lower case throughout.

The executable does hold a `char[9]` array at `0x0061e68c` naming `chap1.mpg` through `chap6.mpg`
back to back with no terminator between entries, **and nothing reads it**. Its only reference is a
pointer stored at `0x0061daec`, and that slot has no code reference of any kind; the seven sibling
slots from `0x0061dad0` point at unrelated name strings and are orphaned the same way. That is why
`chap6.mpg` has no file in the install: it is a leftover in a table no code path reaches, and the
chapter names that are actually used come from the script above.

## How the original plays them

The executable's whole involvement is one function, `FUN_004a7c70`, the boot sequence. It resolves
the string `Assets\Graphics\MPG` and copies the result into a `MAX_PATH` buffer at `DAT_0064fde4`,
which is the base directory the `PLAYAVI` names resolve under, so those names are relative and the
folder is the caller's to set. It then runs two reader blocks through `FUN_0044ae70`, which takes a
file and a block name: `fmv.zrd` `INTRO`, then `fmv.zrd` `CHAP0`. `FUN_0044ae70` has exactly one
caller, so those two blocks are the only reader-driven video in the game. Everything else is
layout and script driven.

The two script-driven cinemas each end by handing off, and each accepts its own skip keys:

- **`CAMPAIGNINTRO`** ends on Escape, Space, Return or a left mouse button press, any of which posts
  message 11006. That runs `script_continue @passengercabin@` and ends the script, so a chapter
  cinema hands off to the passenger cabin.
- **`FINALCINEMA`** ends on Escape or a left mouse button press only, not Space and not Return.
  Message 11006 there runs `scrapbook.script` at priority `0x1000` and mails 11005. The whole screen
  is gated on `callback($$E$$, 3104)`: when that returns false the movie is skipped outright and the
  scrapbook runs directly.

Both guard against a double handoff with an `EC` latch, so a second key press during the transition
does nothing.

## Playing them in CSVM

**Godot will not play these as they ship, and a conversion step is required.** Godot 4.7 compiles
in exactly one video decoder, Theora in an Ogg container: `VideoStream`'s own reference says the
file "should be an Ogg Theora video file with the `.ogv` extension", `VideoStreamTheora` is the
only `VideoStream` subclass in the editor binary, and the binary carries no MPEG video decoder at
all. The one `.mpg` literal in it belongs to the Android exporter's list of already-compressed
extensions, and the one `webm` match is the colour name `webmaroon`.

**The decision is a managed MPEG-1 decoder in this project's own code**, producing an
`ImageTexture` per frame that the front end composes like any other picture, with the audio
elementary stream pushed to a generator. No `VideoStream` subclass and no `VideoStreamPlayer` are
involved, because the front end is not a node tree with a video slot in it: `ComposedBoard` resolves
each screen into pictures in the original's 800x600 space and `BoardFit` maps that onto the window,
so a `movie` row is structurally a picture at Z=0 under everything else, and routing it through a
player node would put the one element that must sit under the screen into a different rendering path
from everything above it, with `BoardFit`'s scaling mirrored there by hand.

Three things follow from owning the decode. The release carries no media binary, which matters
because the recipient runs extraction on their own machine, so a transcoder would ship in the
download rather than living on a developer's box. The shipped bytes are what plays, so the output
cannot drift from what the original showed. And nobody ever has to judge a transcode, which deletes
a presentation question rather than answering it.

The rejected alternative, recorded because the choice is reversible: transcode at extract time to
Ogg Theora and play through a stock `VideoStreamPlayer`, which uses a decoder the engine maintains
rather than one this project owns and debugs. It was rejected on the download it obliges. The
release zip is 78 MB and a stock ffmpeg is roughly 80 to 90 MB, so ten short videos would about
double what anyone downloads, on top of a second licence file and a third payload version for the
notices to lock. A trimmed ffmpeg built from source, on the `mech3ax` model, would land near 5 to
10 MB and remains the fallback if the decoder work stalls.

## Frame counts and durations

No header states a frame count: it is what a walk of the whole file produces. These are the counts
`CSVM.Video` yields, with the duration each implies at the file's own rate, and they are what
`CSVM.Tests/MpegMovieTests.cs` holds the decoder to.

| File | Frames | Duration |
|---|---|---|
| `crimflag.mpg` | 240 | 8.0 s |
| `msopen1.mpg` | 404 | 13.5 s |
| `zipper.mpg` | 608 | 20.3 s |
| `chap0.mpg` | 4349 | 145.0 s |
| `chap1.mpg` | 3926 | 130.9 s |
| `chap2.mpg` | 3698 | 123.3 s |
| `chap3.mpg` | 2784 | 92.8 s |
| `chap4.mpg` | 4124 | 137.5 s |
| `chap5.mpg` | 2874 | 95.8 s |
| `final.mpg` | 3330 | 111.0 s |

The flag loop is eight seconds long, the shortest of the ten by a wide margin, which is why it is
a tenth the size of the others. `msopen1.mpg` is the only one whose duration is read against
30000/1001 rather than 30.

## Evidence and limits

The container and codec facts above are read out of the files' own pack, sequence and audio frame
headers in the retail install. Nothing here reproduces file content. The naming and playback facts
come from the extracted `ASSETS/LAYOUT.CSV` and `ASSETS/SCRIPTS/*.SCRIPT` and from a read-only pass
over `crimson.exe`, with every address stated at the claim it supports.

Where this stops:

- **No frame of these files has been displayed by this project.** All ten decode end to end in
  `CSVM.Video`, which is where the frame counts below come from, but nothing composes a decoded
  frame on screen, so no claim here rests on a picture anyone has looked at. There is also no
  reference decoder to check the decode against, and MPEG-1 permits IDCT mismatch between
  conformant decoders, so exact-match testing against one would not work even if there were.
  Correctness is settled the way every other presentation claim in this project is settled, by
  the user at the controls.
- **The engine side of the script callbacks is not traced.** Callback 2151 supplies the chapter
  number `CAMPAIGNINTRO` builds its filename from, and callback 3104 gates the final cinema, but
  neither was followed into the executable: the script callback dispatch is not a plain switch on
  the id, so finding it is its own job. Neither blocks a player, since the chapter number is
  something the campaign already knows and the gate is campaign completion.
