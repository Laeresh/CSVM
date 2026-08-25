# Cinemas (`GOSDATA\ASSETS\GRAPHICS\MPG\`)

Part of the [format documentation](README.md). This page describes the ten video files the retail
install ships, what they actually are as containers and codecs, where the original names them from,
and how CSVM would play them. Nothing in CSVM plays video today; the decision below is what any
player work starts from.

## Contents

- [The shipped set](#the-shipped-set)
- [Container and codecs](#container-and-codecs)
- [Where the files are named from](#where-the-files-are-named-from)
- [Playing them in CSVM](#playing-them-in-csvm)
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
| `crimflag.mpg` | 0.9 MB | a short loop, the only mono-audio file in the set |

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

## Where the files are named from

Two places name them, and neither is a directory scan.

`fmv.zrd` holds the front-end sequence as `PLAYAVI` actions: its `INTRO` block plays
`MSopen1.mpg`, waits, fades and plays `zipper.mpg`, and its `CHAP0` block plays `Chap0.mpg`. Those
are the only three `.mpg` strings in the whole extracted reader set, and the block's `IMAGE_PATH` is
`video`. Note the case: the reader spells them with capitals the on-disk names do not have, so a
case-sensitive lookup would fail on all three.

The chapter cinemas come from the executable instead, as a `char[9]` array at `0x0061e68c` holding
`chap1.mpg` through `chap6.mpg` back to back with no terminator between entries. `chap6.mpg` has no
file in the install, and `final.mpg` and `crimflag.mpg` appear in neither place, so the naming of
the last three is not fully accounted for.

## Playing them in CSVM

**Godot will not play these as they ship, and a conversion step is required.** Godot 4.7 compiles
in exactly one video decoder, Theora in an Ogg container: `VideoStream`'s own reference says the
file "should be an Ogg Theora video file with the `.ogv` extension", `VideoStreamTheora` is the
only `VideoStream` subclass in the editor binary, and the binary carries no MPEG video decoder at
all. The one `.mpg` literal in it belongs to the Android exporter's list of already-compressed
extensions, and the one `webm` match is the colour name `webmaroon`.

**The decision is to transcode at extract time to Ogg Theora**, alongside the existing extraction
steps, and to play the result through a stock `VideoStreamPlayer`. The reasons are that it uses a
decoder the engine maintains rather than one this project would own and debug, that the playback
surface (a player node, the fade and wait steps `fmv.zrd` already spells out, a skip key) is
identical whichever route supplies the frames, and that the source is 320x240 and already lossy, so
a second encode at that size is not a fidelity question the original can lose.

The alternative, recorded because the choice is reversible and this is the form it would take: a
runtime MPEG-1 decoder written as a C# `VideoStreamPlayback` subclass. That is available without an
engine build or a GDExtension, because `VideoStreamPlayback`'s whole virtual set is scriptable and
it exposes `mix_audio` for the audio side. MPEG-1 video is a small codec by modern standards, and
320x240 at 30 fps is not a performance question. It wins on two grounds the transcode route loses:
the extraction step stays owned by this project's own code with no third-party media binary in it,
and the shipped bytes are what plays, so the output cannot drift from what the original showed.
Choosing the transcode first does not close this route, because the player node is the same either
way.

## Evidence and limits

The container and codec facts above are read out of the files' own pack, sequence and audio frame
headers in the retail install. Nothing here reproduces file content.

Where this stops:

- **Nobody has judged a transcode.** No transcoder is present in this checkout, so no `.ogv` sample
  was produced and no one has looked at one. Whether the encode holds up is a presentation call the
  user makes at the controls, the way every other presentation claim in this project is settled.
- **The chapter play order is not established.** The `char[9]` array's index base was not traced, so
  which chapter maps to which `chapN.mpg` is unread, and the reader that names `final.mpg` and
  `crimflag.mpg` was not found.
- **Per-file durations and frame counts are not stated.** Only the first sequence header of each
  file was parsed; the headers give the frame rate but the files were not walked to the end.
