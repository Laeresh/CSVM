# CSVM — Crimson Skies open-source remake

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive / Microsoft):
a modern engine that plays the original game using your own legally-owned game files.

This repository ships **code and documentation only**. It contains no game assets of
any kind, and reproduces no game code. The engine reads your own copy of the game in place at
runtime: an install, the disc's drive, or an image of the disc (`.iso`, `.cue`/`.bin`/`.img`,
`.ccd`/`.img`).

## Status

What this build plays:

- The single-player campaign, with its cabin, briefing and flight-check screens.
- Instant Action's four mission types: dogfighting an ace, dogfighting a squadron, attacking
  a zeppelin, and stunt flying against the clock.
- 2 to 4-player splitscreen Dogfight deathmatch, and free flight.
- Network play: the Dogfight deathmatch and campaign co-op, hosted from the menu and joined by
  LAN search or by IP address.
- Seated PC VR through OpenXR on Windows, experimental: flight in stereo from the cockpit, menus
  and films on a theater screen. [`packaging/README.md`](packaging/README.md#vr-experimental) says
  how to start it.
- 11 aircraft and 8 chapter worlds, in the original's liveries, animated and with sound.
- Guns and rockets, AI aircraft that patrol, engage and evade, turrets, zeppelins, and world
  objects that take damage and die.
- Every game file format the engine uses, read by CSVM's own C# readers straight from the
  original files, with no extraction step.

This is an early build; the release page lists what is known broken in it.

## Download and play

You need Windows 10 or 11, 64-bit, a graphics card with a working Vulkan or Direct3D 12 driver,
and a retail Crimson Skies install, the game's disc or an image of it. Nothing else: the .NET
runtime the engine needs is inside the download, so there is no runtime or framework to install.

1. Download the `CSVM-v<version>-win64.zip` asset from the
   [latest release](https://github.com/Laeresh/CSVM/releases/latest).
2. Unzip it wherever you like.
3. Double-click **`CSVM.exe`**. It looks for the game by itself, and when it finds none it asks
   for your install folder (the one holding the `ZBD` and `GOSDATA` subfolders), the disc's
   drive or a disc image. Your copy is only read, never modified. An optional copy puts the
   game files beside `CSVM.exe` (about 830 MB), so a disc or image need not stay in place.
4. Pick a mode, chapter and plane in the menu.

[`packaging/README.md`](packaging/README.md) is the long form of those four steps and ships in
the zip as the `README.md` beside `CSVM.exe`: the SHA-256 to check the download against, the
Windows SmartScreen warning an unsigned download raises, where your logs and saved games live,
and what to read when the game vanishes after it starts.

## Reporting a problem, and contributing

Bugs go in an [issue](https://github.com/Laeresh/CSVM/issues/new?template=bug_report.yml).
The form asks for the build version and the newest log file from the `logs\` folder beside
`CSVM.exe`, which between them usually identify a fault without a round trip. A report is
worked on in its own issue and is not copied into the author's `backlog` issues, so the issue
is the thread to follow.

Small self-contained pull requests are welcome for `packaging/`, the repo scripts,
the documentation and typo fixes. A change under `CSVM/src` needs an issue first, because
engine changes land through a golden-image tier a contributor cannot run.
[`CONTRIBUTING.md`](.github/CONTRIBUTING.md) has the detail. Security problems go through
[`SECURITY.md`](.github/SECURITY.md) rather than an issue.

## Building from source

The rest of this page is about the source tree; nothing in it is needed to play.

```
git clone https://github.com/Laeresh/CSVM.git
```

You need a legally-owned copy of Crimson Skies, [Godot 4.7 (.NET)](https://godotengine.org/)
and the .NET 8 SDK. Put your install (or a copy of it) in `CrimsonSkiesGame\` at the repo
root, or point `CSVM_INSTALL_ROOT` at it, then see [`CSVM/README.md`](CSVM/README.md) to build
and run. The engine reads the game files in place through its own readers
(`CSVM/src/Extraction/`, `CSVM/src/Mech3/`); there is no extraction step and no external tool.

**Modding.** Players who want to edit the game's archives run unzbd, mech3ax's extractor,
themselves (the build CSVM v0.4.0 bundled writes the layout CSVM reads), edit its output, and
choose its folder on the Modding Folder row of Options' GAME OPTIONS page (Choose folder...),
which keeps it for later launches and also offers an old `extracted` folder, or name it for one
launch with `--mod-folder=<folder>`. CSVM loads each
archive the folder holds from there and everything else from the game files; [`docs/modding.md`](docs/modding.md) lists the files it reads.

### Build and run

```
dotnet build CSVM/CSVM.sln
godot --path CSVM res://scenes/Main.tscn -- --plane=player_bhawk
```

First time only, run Godot once with `--headless --import` before running scenes. `RunGame.ps1`
wraps the build step and launches the in-game menu with no args; `RunDev.ps1` is the same with
interactive console prompts. Verify a change with `.\RunTests.ps1` — one command, one exit code
(build → unit tests → in-engine suites → golden-image hashes).

### Package a release build

Godot export templates are user-global, not part of the repo. One-time setup: extract the inner
`templates/` files of `tools/godot-4.7-mono-export-templates.tpz` directly into
`%APPDATA%\Godot\export_templates\4.7.stable.mono\`.

```
.\ExportRelease.ps1
```

`ExportRelease.ps1` builds, imports, and exports the release preset to `.scratch\export\CSVM.exe`,
checking the export templates are installed and clearing `.scratch\export\` before it starts.
This produces a self-contained `CSVM.exe` (the .NET runtime is bundled, so a recipient installs
nothing) plus its data folder. It then copies the rest of the release in beside it,
`packaging/README.md` / `LICENSE` / `LICENSE-thirdparty.txt`, SDL2 and the WebRTC library,
generates `BUILD-INFO.txt`, and zips the folder to
`.scratch\CSVM-v<version>-win64.zip`, the archive to hand over. The version is
`application/config/version` in `CSVM/project.godot`, which the exe's file properties and the
first line of every log state as well. See [`packaging/MANIFEST.md`](packaging/MANIFEST.md) for
the layout and [`docs/tooling.md`](docs/tooling.md) for the full export and packaging detail.

```
.\PublishRelease.ps1 -NotesFile <file>
```

`PublishRelease.ps1` is the publish itself, in one run: it reads the same version, runs
`ExportRelease.ps1 -Linux`, runs `sandbox\LinuxRelease.ps1` on the tarball (a failure stops
the run before the tag), computes both archives' SHA-256s, creates the annotated tag on the
commit that was built, pushes it, and creates the GitHub release with the zip and the tarball,
so the tag, the executables' version, the archives' names, the published checksums and the
notes cannot disagree with each other. It needs `gh` installed and authenticated and WSL
Debian for the Linux export, and it refuses a dirty tree and a tag that already exists; a tag
is never re-pointed. `-DryRun` runs every check and the export and stops before the tag.

## Format documentation

The reverse-engineered format reference lives in [`docs/formats/`](docs/formats/) — 48 pages
covering the GameZ container, reader archives, paint schemes, animation definitions, world
structure and more. Most of it was decoded by inspecting the game's data and matching
behaviour against the original game. Most of those pages also rest on static analysis of the
retail `crimson.exe` wherever it settles a question the data cannot, naming the function or
address at the point of use rather than reproducing code. Findings that come from
the executable wholesale live apart from the format reference, in [`docs/org/`](docs/org/): the
flight model, the weapon and particle runtimes, the AI, and the rest of the original behaviour
this engine matches. Those pages describe behaviour and constants, and name the function
addresses so any claim can be re-checked at source. **No game code and no game assets are
reproduced or redistributed here**; the engine is an independent implementation.

## Supporting the project

CSVM is free and will stay free. The engine is GPL-3.0, every release is
on the releases page, and nothing is held back for supporters.

If you'd like to support the work anyway, you can do so on
[Ko-fi](https://ko-fi.com/Laeresh). Contributions go towards the
monthly running costs of the master server, and towards development
time on the engine, its documentation and its code. They buy no early
access, no exclusive builds, no server privileges and no game content.

The master server is optional: CSVM plays without it, and it is open
to everyone whether they contribute or not. You can even host your own.

Playing CSVM still requires your own legally-owned copy of
Crimson Skies. This is an unofficial fan project and is not affiliated
with, endorsed by, or sponsored by Microsoft or Zipper Interactive.

## Legal

This is an unofficial fan project, **not affiliated with, endorsed by, or sponsored by
Microsoft or Zipper Interactive**. "Crimson Skies" and all related names, marks and artwork
are the property of their respective owners. Crimson Skies © Microsoft Corporation.

No game content is distributed here. The engine requires — and reads at runtime — game files
from your own legally-obtained copy of Crimson Skies. Nothing in this repository will run
without it.

Screenshots and videos of CSVM running show the original game's artwork and sound, which
makes them Game Content under Microsoft's
[Game Content Usage Rules](https://www.xbox.com/en-us/developers/rules). Wherever this project
publishes one, it carries the notice those rules ask for:

> Crimson Skies © Microsoft Corporation. CSVM's screenshots and videos were created under
> Microsoft's "Game Content Usage Rules" using assets from Crimson Skies, and they are not
> endorsed by or affiliated with Microsoft.

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

CSVM's readers of the game files build on the format research of
[TerranMechworks/mech3ax](https://github.com/TerranMechworks/mech3ax).

## AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers name the specific agent and
model that did the work.
