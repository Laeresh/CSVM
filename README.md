# CSVM — Crimson Skies open-source remake

An XWVM-style remake of **Crimson Skies** (2000, Zipper Interactive / Microsoft): a modern
engine that plays the original game using your own legally-owned game files.

This repository ships **code and format documentation only**. It contains no game assets of
any kind. The engine reads your existing retail install at runtime.

## Status

Playable vertical slice. You can fly any of 11 aircraft over any of 8 chapter worlds — free
flight, timed stunt runs, or 2–4-player splitscreen racing — launched from an in-game menu,
in a livery painted the way the original paints it, over a world that is animated: trains,
doors, road vehicles, propellers, point lights, ambient sound, scrolled water, and
per-mission entity setup.

Asset extraction is complete: every ZBD archive type this game ships round-trips
byte-identically.

## Getting started

```
git clone https://github.com/Laeresh/CSVM.git
```

You need a legally-owned copy of Crimson Skies, [Godot 4.7 (.NET)](https://godotengine.org/)
and the .NET 8 SDK. Extract your install's assets with `ExtractAssets.ps1`, then see
[`CSVM/README.md`](CSVM/README.md) to build and run.

Extraction is done by our fork of mech3ax, which is where the Crimson Skies format support
lives: **[Laeresh/mech3ax, branch `cs-anim`](https://github.com/Laeresh/mech3ax/tree/cs-anim)**
— that branch is what the extractor binary is built from. (The fork's `main` is an upstream
mirror and does *not* carry the Crimson Skies work.)

## Format documentation

The reverse-engineered format reference lives in [`docs/formats/`](docs/formats/) — 17 pages
covering the GameZ container, reader archives, paint schemes, animation definitions, world
structure and more. Everything was decoded by inspecting extracted data and matching
behaviour against the original game. **No executable decompilation was performed.**

## Legal

This is an unofficial fan project, **not affiliated with, endorsed by, or sponsored by
Microsoft or Zipper Interactive**. "Crimson Skies" and all related names, marks and artwork
are the property of their respective owners.

No game content is distributed here. The engine requires — and reads at runtime — game files
from your own legally-obtained copy of Crimson Skies. Nothing in this repository will run
without it.

## License

Code is licensed under the **GNU General Public License, version 3 or later**
([`LICENSE`](LICENSE)).

    Copyright (C) 2026 Gabriel Unmüßig

    This program is free software: you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by the Free Software
    Foundation, either version 3 of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful, but WITHOUT ANY
    WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
    PARTICULAR PURPOSE. See the GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along with this
    program. If not, see <https://www.gnu.org/licenses/>.

The format documentation in `docs/formats/` is licensed separately and more permissively,
under **Creative Commons Attribution 4.0 International**
([`docs/formats/LICENSE`](docs/formats/LICENSE)), so that the format knowledge can be reused
by any project regardless of its own license. Attribute to **CSVM**, linking
<https://github.com/Laeresh/CSVM>.

Asset extraction uses [our fork](https://github.com/Laeresh/mech3ax) of
[mech3ax](https://github.com/TerranMechworks/mech3ax), which is licensed under the EUPL-1.2
and carries its own license terms. It is a separate tool, not linked into this engine.

## AI assistance disclosure

This project is developed with the help of [Claude Code](https://claude.com/claude-code).
Parsers, engine code and documentation are AI-assisted and human-reviewed.
