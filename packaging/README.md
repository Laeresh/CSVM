# CSVM — Crimson Skies open-source remake

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive /
Microsoft): a modern engine that plays the original game using your own legally-owned game
files.

**You must own Crimson Skies.** This zip contains no game data of any kind — only the
engine and an extraction tool. Everything you see and hear in the game is read from your
own retail install.

## Requirements

- Windows 10 or 11
- A retail Crimson Skies install (the original 2000 PC game)
- Nothing else — no runtimes or frameworks to install

## Setup: two commands

**1. Extract your game's assets** (once). Open a terminal in this folder (in Explorer:
right-click in the folder → "Open in Terminal"), then run — with the path replaced by
your own Crimson Skies install folder:

```
powershell -ExecutionPolicy Bypass -File Extract.ps1 "C:\Program Files (x86)\Microsoft Games\Crimson Skies"
```

The folder to pass is the one that contains the `ZBD` and `GOSDATA` subfolders — open
your install until you see those two side by side. Your install is only read, never
modified. The extracted data lands in an `extracted` folder next to `CSVM.exe`.

Use exactly the command spelling above: Windows' default script policy blocks a plain
`.\Extract.ps1`, and the `-ExecutionPolicy Bypass -File` form is the supported way around
that for this one script.

**2. Fly:**

```
.\CSVM.exe
```

(or double-click `CSVM.exe` in Explorer) — pick a mode, chapter, and plane in the menu.

## Disk space and time

Extraction produces about **0.6 GB** in the `extracted` folder (leave 1 GB free to be
safe) and takes **well under a minute** on an SSD — around 15 seconds on a fast machine.

## Windows SmartScreen

The first time you start `CSVM.exe`, Windows will likely show a blue **"Windows protected
your PC"** screen. That is expected: the exe is not code-signed (signing is for a later
public release). Click **"More info"**, then **"Run anyway"**. You got this zip from us
personally, and we are telling you to expect exactly this warning — if you got it from
anywhere else, don't run it.

## If something goes wrong

Logs are written to `logs\` next to `CSVM.exe`. When you report a problem, send
the newest log file from there along with what you did — that is usually all we need.

If extraction fails, copy the message it printed; if the game starts but warns about the
extraction at boot, re-run the extraction command from step 1.

## Where your files live

The engine, your `extracted` game assets and the `logs` folder all sit in this one folder,
wherever you unzipped it. Your settings, control bindings, campaign profiles, stunt scores
and custom planes are kept outside it, in:

```
%APPDATA%\Godot\app_userdata\CSVM
```

(paste that into Explorer's address bar). Deleting those two folders removes everything
CSVM has written; your Crimson Skies install is never modified.

## Legal

This is an unofficial fan project, **not affiliated with, endorsed by, or sponsored by
Microsoft or Zipper Interactive**. "Crimson Skies" and all related names, marks and
artwork are the property of their respective owners.

No game content is distributed in this zip. The engine requires — and reads at runtime —
game files from your own legally-obtained copy of Crimson Skies. Nothing in this zip will
run without it.

## License

The CSVM engine is licensed under the **GNU General Public License, version 3 or later**
(see `LICENSE` in this folder). Source code: <https://github.com/Laeresh/CSVM>.

    Copyright (C) 2026 Gabriel Unmüßig

    This program is free software: you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by the Free Software
    Foundation, either version 3 of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful, but WITHOUT ANY
    WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
    PARTICULAR PURPOSE. See the GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along with this
    program. If not, see <https://www.gnu.org/licenses/>.

The bundled extraction tool `tools\unzbd.exe` is built from
[our fork](https://github.com/Laeresh/mech3ax/tree/cs-anim) of
[mech3ax](https://github.com/TerranMechworks/mech3ax), which is licensed under the
**EUPL-1.2** (see `LICENSE-unzbd` in this folder) and carries its own license terms. It is
a separate tool, not linked into the engine.

## AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed.
