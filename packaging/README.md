# CSVM — Crimson Skies open-source remake

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive /
Microsoft): a modern engine that plays the original game using your own legally-owned
game files.

**You must own Crimson Skies.** This zip contains no game data of any kind, only the
engine. Everything you see and hear in the game is read from your own copy of the game:
the installed game, its disc, or an image of the disc.

## Where this download comes from

CSVM is published in one place, the releases page of its repository:

<https://github.com/Laeresh/CSVM/releases>

Every release lists the SHA-256 of its zip. To check the file you downloaded, open a
terminal in the folder holding the zip and run this, with the name replaced by the zip you
actually have:

```
Get-FileHash CSVM-v0.5.0-win64.zip -Algorithm SHA256
```

The printed hash must match the one on the release page (upper and lower case do not
matter). If it does not, delete the file and download it again from the link above.
Nothing in this zip is code-signed, so that hash is what tells you this is the build the
project published, rather than something a third party rebuilt or altered.

## Requirements

- **Windows 10 or 11**, 64-bit.
- **A graphics card with a working Vulkan or Direct3D 12 driver.** Either one is enough;
  the build picks whichever is available. Below that line the program starts and then
  vanishes at the intro film, before any menu; "The game disappears after it starts" below
  says how to recognise that case.
- **A retail Crimson Skies install** (the original 2000 PC game) on this machine, or the
  game's disc or an image of it (`.iso`, `.cue` with its `.bin` or `.img`, or `.ccd` with
  its `.img`).
- **About 1 GB of free disk space** only if you copy the game files beside CSVM, which is
  optional (see below).
- Nothing else. The .NET runtime this engine needs is inside the download, so there is no
  runtime or framework to install.

## Setup: choose your game files, then fly

**1. Choose your game files.** Double-click **`CSVM.exe`**. CSVM looks for the game by
itself: a copy next to `CSVM.exe` (a `GAME` folder or an `.iso` image), the copy you chose
last, then the places Crimson Skies is normally installed. When it finds none, it opens a
screen that asks for one: your install folder, the game disc's drive, or an image of the
disc. CSVM reads the files where they are and never changes them, and it remembers your
choice.

For an install, the folder it wants is the one containing the `ZBD` and `GOSDATA`
subfolders. Open your install until you see those two side by side, and use that folder; if
you choose a folder inside it or a folder that holds it, CSVM names the folder it expected.

Once the files read, the screen offers **Copy, then play**: it copies the game files CSVM
reads (about 830 MB) into a `CrimsonSkiesGame` folder next to `CSVM.exe`, so the disc or
image need not stay in place. **Play** goes straight to the menu without a copy.

**2. Fly.** Pick a mode, chapter and plane in the menu.

## An `extracted` folder from an older CSVM

Earlier versions extracted the game into an `extracted` folder next to `CSVM.exe`. This
version reads the game files directly and does not use it. The first start asks once
whether to keep it, to mod the game with (it is read again only when you choose it on the
Modding Folder row of Options' GAME OPTIONS page), or to delete it and free the space.

## Windows SmartScreen

**Nothing in this zip is code-signed.** The first time you start `CSVM.exe`, Windows will
likely show a blue **"Windows protected your PC"** screen. Click **"More info"**, then
**"Run anyway"**.

That prompt means Windows does not recognise the publisher, which is true of every
unsigned download and says nothing about what the file does. The check that does mean
something is the SHA-256 above, taken against the release page. If this zip reached you
from anywhere other than <https://github.com/Laeresh/CSVM/releases>, do that check before
you run any of it.

Windows also marks files that came from the internet. If something refuses to run at all,
right-click the zip file, choose **Properties**, tick **Unblock**, click OK, and unzip it
again.

## VR (experimental)

CSVM can be flown seated in a PC VR headset, through the OpenXR runtime that is active on this
PC. VR is experimental in this release: it has been flown on one machine (a Quest 3 with an RTX 5080),
and the release page lists its known issues. Windows only.

**Runtimes.** Virtual Desktop's own runtime (VDXR) and SteamVR, which Steam Link uses for a
Quest, have both been flown. Whichever is set as the system's OpenXR runtime is
the one the game uses. Set it to **90 Hz** at its default resolution.

**Starting it.** Start the headset and its runtime first, then either:

- double-click **`CSVM-VR.cmd`** beside `CSVM.exe`, which starts that one launch in VR, or
- turn on **VR Mode** in Options, on the Video page. The game restarts in the headset, and every
  later start of `CSVM.exe` does the same until you turn VR Mode off again.

If no headset starts, the game plays flat and the menu says so. Windows may ask before it runs
`CSVM-VR.cmd`, as it does for `CSVM.exe` (see "Windows SmartScreen" above).

**Playing.** Menus, briefings and films appear on a large screen in front of you; the right hand
controller's laser points at it and its trigger clicks. You fly with a gamepad, a flight stick or
the keyboard, as in flat play. A flight opens in the Cockpit view, and the headset is your head,
so the look keys and the right stick's look do nothing. `F8`, D-pad down or the left controller's
Y button cycles Cockpit, Nose and Chase; the chase view is fixed to the aircraft, so the world
turns around you in a bank or a loop. The left controller's menu button pauses. VR is one player:
splitscreen is not available, and network play is.

**Recentering.** Sit as you would in the cockpit and recenter, which puts your eyes on the
pilot's. `Home`, `numpad 5`, `numpad 0`, a pad's right-stick click, the left controller's stick
click, a PlayStation pad's touchpad click, the first back paddle of an Elite-style pad, or the
runtime's own recenter all do it, and the game recenters by itself when the headset session
starts.

**Quality and comfort.** A headset flight renders with its own VR quality profile in place of
your graphics options: no anti-aliasing filter over 4x MSAA, Low shadows (hard-edged) and no
foveated rendering, sized so the reference machine has headroom at 90 Hz. To fly with your own
graphics options instead, which costs several times as much, start it from a terminal in this
folder as `CSVM-VR.cmd -- --xr-quality=flat`. Under a headset, the Video page adds **VR Render
Scale** (50 to 125 %, lower is lighter on the GPU) and **Comfort Vignette** (Off, Mild, Strong; it
darkens the edge of view while the aircraft turns). Every cut, respawn and view change fades
through black.

To go back to flat play, turn VR Mode off in Options. `CSVM.exe --xr-mode off` starts one launch
flat whatever VR Mode says.

## If something goes wrong

Logs are written to `logs\` next to `CSVM.exe`, one file per run, named for the mode and
the time it started. The newest file there is the run you just did, and its first line
states the build version. That same version is in the bottom-right corner of the menu.
The small page icon beside it opens the logs folder, and the floppy disk icon opens the user
folder, which holds your settings, controls, campaign profiles and custom planes.
`F12` saves a screenshot from any screen into `Screenshots\` next to `CSVM.exe`, ready to
attach to a report.

Report a problem at <https://github.com/Laeresh/CSVM/issues>, through the bug report form.
It asks for the build version, what you did, your graphics card and the newest log file
from `logs\`, which is usually all it takes to identify the fault; attaching that log
answers most of the form by itself. CSVM is written by one person, so a report is read and
worked on in its issue rather than answered to a schedule.

Three failures answer themselves:

- **The game files cannot be read.** The screen names the folder or image it tried and why
  it was refused. That text is the useful part of a report about it.
- **The game asks for the game files again when it starts.** The copy you chose last is no
  longer where it was: the disc is not in its drive, or the image or folder moved. Choose it
  again, or copy the game files next to `CSVM.exe` so this cannot happen.
- **The game disappears after it starts.** Read on.

### The game disappears after it starts

The window opens, the boot card shows, and a few seconds later, as the intro film begins,
the program vanishes with no window and no message. That is what this build does on a
machine below the requirement above, and there is no setting inside the game that changes
it. Without game files the same machine still shows the screen that asks for them, so
seeing that screen does not mean the machine can play.

To tell that case apart from a genuine crash, open the newest file in `logs\` and find the
line beginning `[perf] gpu=`. It names your graphics adapter and the renderer in use. A
`gpu=Microsoft Basic Render Driver` there means Windows is rendering in software, with no
graphics card driving it, and this build cannot fly a mission that way.

The engine's own startup output may also say `Your video card drivers seem not to support
Vulkan, switching to Direct3D 12`. On its own that line is not a fault, because Direct3D 12
is one of the two drivers this build runs on. It matters only when the `[perf] gpu=` line
then names a software device.

The usual cause is a machine with no graphics driver installed, or a virtual machine
without graphics passthrough. Installing your graphics card's current driver is the fix
where there is one.

## Where your files live

The engine, the `logs` and `Screenshots` folders and, if you made one, the
`CrimsonSkiesGame` copy of the game files all sit in this one folder, wherever you unzipped
it. Your settings, control bindings, campaign profiles, stunt scores
and custom planes are kept outside it, in:

```
%APPDATA%\Godot\app_userdata\CSVM
```

(paste that into Explorer's address bar). Deleting those two folders removes everything
CSVM has written; your Crimson Skies install is never modified.

To keep all of it inside this folder instead, for example on a USB stick, create an empty
file named `portable.txt` next to `CSVM.exe`. CSVM then writes everything to a `userdata`
folder here. Starting `CSVM.exe -- --portable` does the same for one launch. Files already
in the folder above are not moved; copy them into `userdata` to keep them.

Flight stick bindings are kept in the `stick_profiles` folder inside it, one file per stick
model. The Controls screen writes those files for you. To edit one by hand, for example to
set a deadzone, fly two sticks together, or stop a keypad being read as a stick, follow
<https://github.com/Laeresh/CSVM/blob/main/docs/stick-profiles.md>, which links an example
file that binds every action.

## What else is in this folder

- `CSVM.exe` and the `data_CSVM_windows_x86_64` folder beside it are the engine. They
  belong together; moving one without the other breaks the build.
- `CSVM-VR.cmd` starts `CSVM.exe` in VR ("VR (experimental)" above). It holds one command line,
  readable in any text editor.
- `LICENSE` is the GNU GPL v3 the engine is under.
- `LICENSE-thirdparty.txt` carries the copyright notices for the software built into the
  engine: the Godot engine and its own third-party components, and the bundled .NET
  runtime. It also carries the notice for `pl_mpeg`, the MIT-licensed library CSVM's video
  decoder is ported from, and the SIL Open Font License for PromptFont, the font the
  controller button pictures are drawn from.
- `libwebrtc_native.windows.template_release.x86_64.dll` is the WebRTC library internet games run
  over, and `LICENSE-webrtc\` holds its licences.
- `BUILD-INFO.txt` records which commit the engine was built from. Quote it in a bug report when you are not sure which build you have.

## Legal

This is an unofficial fan project, **not affiliated with, endorsed by, or sponsored by
Microsoft or Zipper Interactive**. "Crimson Skies" and all related names, marks and
artwork are the property of their respective owners. Crimson Skies © Microsoft Corporation.

No game content is distributed in this zip. The engine requires, and reads at runtime,
game files from your own legally-obtained copy of Crimson Skies. Nothing in this zip will
run without it.

## License

The CSVM engine is licensed under the **GNU General Public License, version 3 or later**
(see `LICENSE` in this folder). Source code: <https://github.com/Laeresh/CSVM>, at the
commit `BUILD-INFO.txt` names.

    Copyright (C) 2026 Gabriel Unmüßig

    This program is free software: you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by the Free Software
    Foundation, either version 3 of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful, but WITHOUT ANY
    WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
    PARTICULAR PURPOSE. See the GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along with this
    program. If not, see <https://www.gnu.org/licenses/>.

CSVM's readers of the game files build on the format research of
[TerranMechworks/mech3ax](https://github.com/TerranMechworks/mech3ax).

## AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed.
