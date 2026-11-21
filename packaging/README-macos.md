# CSVM, a Crimson Skies open-source remake (macOS build)

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive /
Microsoft): a modern engine that plays the original game using your own legally-owned
game files.

**You must own Crimson Skies.** This archive contains no game data of any kind, only the
engine. Everything you see and hear in the game is read from your own copy of the original
game.

**The macOS build is experimental.** It is built from the same source as the Windows build,
but the project has no Mac to try it on. On a Mac the engine draws through Metal, which the
Windows and Linux builds never use, so a graphics fault or a crash at startup is possible.
A report from your Mac is how such a problem becomes known.

## Where this download comes from

CSVM is published in one place, the releases page of its repository:

<https://github.com/Laeresh/CSVM/releases>

To check the file you downloaded, open Terminal in the folder holding it and run this, with
the name replaced by the archive you actually have:

```
shasum -a 256 CSVM-v0.8.0-macos.zip
```

The printed hash must match the one on the release page. If it does not, delete the file and
download it again from the link above.

## Requirements

- **A Mac with Apple Silicon or an Intel processor.** `CSVM.app` runs natively on both.
- **A copy of the original game's installed folder**, the folder a Windows install of
  Crimson Skies creates, or an image of the game's disc (`.iso`, `.cue` with its `.bin` or
  `.img`, or `.ccd` with its `.img`).
- **For a flight stick only:** the SDL2 library from Homebrew, installed with
  `brew install sdl2`. Without it CSVM runs as usual and reads no flight stick. Gamepads, the
  keyboard and the mouse do not need it.

## Unpacking and the first start

1. Double-click the zip. It unpacks into a folder holding `CSVM.app` and the files listed at
   the end of this README. Move that folder somewhere your user can write to, for example
   your `Documents` folder. Logs are written next to `CSVM.app`.
2. The app is not notarized by Apple, so macOS refuses to open it the first time and
   reports that it cannot check it for malicious software. Open Terminal and run the
   following, dragging `CSVM.app` into the Terminal window in place of the path:

   ```
   xattr -dr com.apple.quarantine ~/Documents/CSVM/CSVM.app
   ```

   This removes the "downloaded from the internet" mark from the app, and macOS then opens
   it like any other. The alternative is to double-click `CSVM.app` once, then open System
   Settings > Privacy & Security and click **Open Anyway** under the message about CSVM. macOS
   may then run the app from a temporary read-only location, where CSVM cannot write its
   logs, so the Terminal command is the better route.
3. Double-click `CSVM.app`. CSVM looks for the game by itself; when it finds none, it opens
   a screen that asks for the game's folder (the one that holds the `ZBD` and `GOSDATA`
   folders side by side) or an image of the disc. Your copy is only read, never modified,
   and CSVM remembers your choice.

The first flight in each place may pause briefly while the Mac compiles the graphics
shaders; later flights reuse them.

## If something goes wrong

Logs are written to `logs/` next to `CSVM.app`, one file per run. The newest file there is
the run you just did. `F12` saves a screenshot into `Screenshots/` next to `CSVM.app`.

Report a problem at <https://github.com/Laeresh/CSVM/issues>. Say that you are on a Mac,
and which model. Attach the newest file from `logs/` and, if something looks wrong, a
screenshot of it. When the app closes before any log appears, start it from Terminal
instead, which prints the engine's own startup messages:

```
~/Documents/CSVM/CSVM.app/Contents/MacOS/CSVM
```

and copy what it printed into the report.

## Where your files live

The `logs` and `Screenshots` folders and, if you made one, the `CrimsonSkiesGame` copy of
the game files sit next to `CSVM.app`. Your settings, control bindings, campaign profiles,
stunt scores and custom planes are kept in:

```
~/Library/Application Support/Godot/app_userdata/CSVM
```

## What else is in this folder

- `CSVM.app` is the engine, with the .NET runtime and the WebRTC library internet games run
  over inside it.
- `LICENSE` is the GNU GPL v3 the engine is under.
- `LICENSE-thirdparty.txt` carries the copyright notices for the software built into the
  engine: the Godot engine and its own third-party components, and the bundled .NET
  runtime, plus the notices for `pl_mpeg` and PromptFont.
- `LICENSE-webrtc/` holds the WebRTC library's licences.
- `BUILD-INFO.txt` records which commit the engine was built from.

## Legal

This is an unofficial fan project, **not affiliated with, endorsed by, or sponsored by
Microsoft or Zipper Interactive**. "Crimson Skies" and all related names, marks and
artwork are the property of their respective owners. Crimson Skies © Microsoft Corporation.

No game content is distributed in this archive. The engine requires, and reads at runtime,
game files from your own legally-obtained copy of Crimson Skies.

## License

The CSVM engine is licensed under the **GNU General Public License, version 3 or later**
(see `LICENSE` in this folder). Source code: <https://github.com/Laeresh/CSVM>, at the
commit `BUILD-INFO.txt` names.

    Copyright (C) 2026 Gabriel Unmüßig

CSVM's readers of the game files build on the format research of
[TerranMechworks/mech3ax](https://github.com/TerranMechworks/mech3ax).

## AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed.
