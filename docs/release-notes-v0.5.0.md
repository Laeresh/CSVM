## CSVM v0.5.0

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive / Microsoft). It
plays the original game from your own retail install, and neither download contains any game data.

### What you can play

- **The campaign**, with its cabin, briefing and flight-check screens and its cutscene films.
- **Instant Action's** four mission types, free flight, and the **Stunt Race** for one to four
  players.
- **Splitscreen Campaign Co-op, Dogfight and Stunt Race** for 2 to 4 players on one machine.
- **Multiplayer over a LAN or the internet:** Deathmatch, team Deathmatch, Capture the Flag,
  Zeppelin vs. Zeppelin, the Stunt Race, and the campaign in co-op. Bots can fill the empty seats.
- **11 aircraft and 8 chapter worlds** in the original's liveries, with the original's weather,
  world animation, music and sound.
- **Gamepads, flight sticks, keyboard and mouse**, with both control screens for binding them.
- **PC VR headsets** on Windows, seated, as an experimental mode.

### New in v0.5.0

**No more extraction**

CSVM now reads the game files where they are, with its own readers, and the extraction step and
the bundled extraction tool are gone.

- **Choosing your game files:** CSVM looks for the game by itself: a copy beside the executable (a
  `GAME` folder or an `.iso`), the copy you chose last, then the places Crimson Skies is normally
  installed. When it finds none, a screen asks for your install folder, the disc's drive or an
  image of the disc.
- **Disc images:** besides an `.iso`, CSVM reads a `.cue` with its `.bin` or `.img`, and a CloneCD
  `.ccd` with its `.img`.
- **Copy, then play:** once the files read, the screen offers to copy them (about 830 MB) into a
  `CrimsonSkiesGame` folder beside the executable, so the disc or image need not stay in place.
  **Play** goes straight to the menu without a copy.
- **No patch needed:** CSVM plays the same from the disc as from a patched install.
- **Modding:** an `extracted` folder from an older CSVM can still be read, as a modding folder you
  pick on the Modding Folder row of Options' GAME OPTIONS page. `docs/modding.md` in the repository
  has the details.

**Enhanced vegetation**

Under Enhanced graphics, the countryside is denser and moves. Three rows on the VIDEO page set it,
and each applies at once, mid-flight included:

- **Vegetation (Original, Rich):** under Rich the original's tree and bush cards sway in the wind,
  take a warmer light on their sun side and a contact shadow at their foot, and vary from stamp to
  stamp. C1 gains grassland scrub and hedged fields with clear ground around its landing strips,
  C2's brush rows break up, and C3's painted jungle grows a canopy of tree crowns.
- **3D Vegetation (Off, Near, Far):** firs, spruces, palms, bushes and brush are drawn as 3D models
  close to you and blend into the original's cards with distance. Near draws the models to 400 m
  and has handed over to the card by 600 m; Far moves that to 600 to 900 m.
- **Ground Cover (Off, Shells, Cards):** grass and crops grow on C1's fields and meadows and on
  C4's grassy slopes, and stay off roads, rails and paths.
- **Defaults:** a machine with a dedicated graphics card starts on Rich, Near and Cards. On an
  integrated GPU, the Steam Deck included, a single player gets Rich and Near with Ground Cover off,
  and splitscreen falls back to Original. Original graphics keeps the original's vegetation.

**HD model packs**

CSVM loads replacement models. A pack's glTF files, in a folder named `HDModels` beside the
executable, draw in place of the original's aircraft, cockpits, world objects and vegetation, and
a `rig.json` per aircraft moves its guns, pylons, exhaust, propellers, control surfaces and
wingtip lights to fit the new model. A pack
only changes what is drawn: every hit box and collision keeps the original's shape, and players
with different packs, or none, play together over the network.

- **Cockpits:** a pack can replace the cockpit interior, or fly an aircraft from a seat inside its
  own body, with the original's gauges moved into it. Both work in VR.
- **Liveries:** a replacement that keeps the original's skin material names and a similar UV
  layout wears every original livery.
- **Making one:** `F10` in the game exports the aircraft on screen, with its livery, damage and
  cockpit, as glTF files that load unchanged as a pack. The format is documented for pack authors
  in `docs/formats/model-packs.md` in the repository.

**Gun aids**

Two optional aids on the GAME OPTIONS page, both off by default so a fresh install plays like the
original:

- **Leading Aim** sends each round toward where the target will be, so a crossing shot hits.
- **Lead Marker** draws a circle on the point to aim at for the selected target, with a dotted
  line to the target, inside your guns' range. It is not drawn in VR.

**Smaller additions**

- **Throttle Plate:** a 1930s throttle lever plate on the HUD that shows the throttle you have set.
  It is a row on the GAME OPTIONS page, off by default.
- **Exhaust smoke on a pad:** a full trigger pushes the throttle ahead quickly enough that the
  engine puffs black smoke, as it does in the original when a stick's throttle is slammed forward.
- **Portable mode:** an empty file named `portable.txt` beside the executable, or starting it with
  `-- --portable`, keeps settings, saves and custom planes in a `userdata` folder beside it.
- **Update notice:** the build stamp in the menu's corner says when a newer release is on GitHub,
  and a click opens its page. The game asks GitHub once per launch and downloads nothing. Setting
  `"updates": { "checkReleases": false }` in a `config.json` beside the executable turns it off.
- **Version-free downloads:** every release now also carries `CSVM-win64.zip` and
  `CSVM-linux-x64.tar.gz`, so `releases/latest/download/` links always fetch the newest build.

### Fixed in v0.5.0

**Weapons and AI**

- The player's guns fire at the original's rate. Every stock gun pair fires twice per interval as
  in the original, about 2.3 times as many rounds a second as v0.4.0, so planes go down much
  faster.
- Turret gunners on the player's aircraft, single gun mounts and AI guns also fire at the
  original's rates.
- Dogfight lobby bots obey the outlaw list, as players do.
- A Stunt Race bot that misses a Danger Zone turns back in a U-turn's width and no longer circles
  it again.

**Graphics and world**

- Under Enhanced, no thin line shows along C5's city shoreline.
- On an integrated GPU on any system, Water Quality defaults to flat water; a Linux desktop with a
  dedicated graphics card gets the waves.
- An HD texture pack's file in the original's own format (PNG or JPG) wins over one in the other,
  so textures the original has in both no longer swap.

**Menus**

- On the multiplayer blackboard and the Dogfight pause sheet, the instruction rows stay inside the
  board and clear the buttons below it.
- The GAME OPTIONS page scrolls like the VIDEO page, and every option has its own row.

This is still an early build. Neither download is code-signed, so Windows SmartScreen warns you the
first time you run the Windows build; the `README.md` inside each download says how to check the
file against the SHA-256 below instead.

### What you need

**Windows:**

- Windows 10 or 11, 64-bit.
- A graphics card with a working Vulkan or Direct3D 12 driver. Below that line the program starts
  and vanishes a few seconds later, at the intro film, with no message; the `README.md` in the zip
  says how to recognise that case from the log.
- A retail Crimson Skies install on the same machine, or the game disc or an image of it. It is
  only read, never modified.
- For VR: a PC VR headset and an OpenXR runtime, such as Virtual Desktop or SteamVR.

**Linux and Steam Deck:**

- x86_64 Linux with a working Vulkan driver (current Mesa for AMD and Intel, or NVIDIA's own
  driver). SteamOS on the Deck has what it needs.
- A copy of the folder a Windows install of Crimson Skies creates, the one holding `ZBD` and
  `GOSDATA` side by side, or the game disc or an image of it. The case of the file names does not
  matter.
- For a flight stick only, the system's SDL2 library (`libSDL2-2.0.so.0`). SteamOS and most desktop
  distributions have it already.

**Both:** about 1 GB of free disk space only if you copy the game files beside CSVM. Nothing else
has to be installed; the .NET runtime the engine needs and the WebRTC library internet play uses
are inside the download.

### Setup

1. Unzip or unpack the download into a folder of its own. The Linux tarball's files sit at its
   root, so make the folder first (`README-linux.md` has the commands).
2. Start `CSVM.exe` (Windows) or `CSVM.x86_64` (Linux). If it does not find your game files by
   itself, it asks for them. Choose **Copy, then play** or **Play**.
3. Pick a mode, chapter and plane in the menu.

On the Steam Deck, add `CSVM.x86_64` to Steam as a non-Steam game, leave the compatibility tool off,
and start it the first time in Desktop mode to choose the game files. After that it starts from Game
mode like any other game. `README-linux.md` has the steps.

**Coming from v0.4.0:** unpack v0.5.0 into a new folder; there is nothing to extract. Your saved
games, settings and custom planes live outside the game folder and carry over. The old folder, with
its `extracted` data, can be deleted, unless you mod the game with it.

### Multiplayer

- **Modes:** Deathmatch, team Deathmatch, Capture the Flag, Zeppelin vs. Zeppelin and the Stunt
  Race from the Multiplayer Lobby, with or without bots, and the campaign in co-op for up to four
  players, where guests follow the host through the cabin, briefing, hangar and debrief.
- **Joining:** a guest picks a public game from the list, enters the host's join code, finds games
  on the same network through the LAN search, or joins by the host's address, IPv4 or IPv6.
- **Hosting over the internet:** a host gets a join code from the master server, and a guest who
  joins by code needs no router setup on either side. Joining by address instead needs UDP port
  47500 to reach the host; CSVM asks the router to open it (UPnP for IPv4, an IGD v2 pinhole for
  IPv6), and if the router does not allow that, forward UDP 47500 to the host by hand. The first
  time you host on Windows, the firewall asks whether to allow CSVM on your network; allow it.
- Every player needs the same version, 0.5.x. A v0.4.0 build cannot join a v0.5.0 game, and is
  told so when it tries.

### Known issues

Nothing here stops a mission from ending or loses progress. Only the larger ones are listed
here.

**VR**

- **Runtimes:** VR has been flown with a Quest 3 only, on Virtual Desktop's runtime and on SteamVR
  through Steam Link. Other headsets have not been tried yet. Hand controllers other than Meta
  Touch and Pico 4 reach the game through the runtime's own button mapping, such as SteamVR's
  binding screen, and the Controls pages name every controller's buttons by the Touch layout.
- **Graphics card:** the VR quality profile was sized on an RTX 5080. A slower card may need a
  lower VR Render Scale, a lower resolution in the runtime, or Original graphics.
- **Shadows:** in the headset the sun's shadows are hard-edged. The engine version CSVM uses makes
  the soft shadow cost more the farther you fly from one corner of the map, and that cost is too
  high for 90 Hz in VR.
- **Cockpit gaps:** the original cockpit models were never meant to be seen in VR. They are very
  simple and have many gaps, so in some cockpits the world is visible through the cockpit, around
  the gauges and beside the Bloodhawk's centre panel, and parts are missing.
- **Starting with VR Mode on** shows the flat window for a moment before the game restarts in the
  headset.
- **The desktop mouse** does not line up with the theater screen. Use a hand controller's laser, a
  pad or the keyboard in the headset's menus.
- **Cutscenes:** something close to the camera at the left or right edge of the picture is cut off
  by the screen's edge. The campaign films play flat on the screen, as they were made.
- **Linux:** VR is Windows only.

**Network play**

- **Co-op hitches under Enhanced:** in two-player network co-op on C5's third mission (CM23) under
  Enhanced graphics, both machines hold their frame rate but hitch now and then, with single frames
  of up to about 0.3 s.

**General**

- **Gun aim:** the guns' auto-aim is still being compared with the original's (#261); this
  release fixes the rate of fire.
- **Performance:** on the Steam Deck, splitscreen under Enhanced Graphics runs well below 60 frames
  per second.
- **Graphics switch:** the first switch to Enhanced in a session can wait 5 to 10 seconds behind
  its cover screen.
- **Graphics:** some details still differ from the original, among them the ground shadow on steep
  slopes and some building faces in New York.

### Reporting a problem

Bugs go in an issue through the bug report form at
<https://github.com/Laeresh/CSVM/issues/new?template=bug_report.yml>. It asks for your system
(Windows, Linux or Steam Deck), the build version and the newest file in the `logs` folder beside
the executable, which between them usually identify a fault without a round trip. The page icon
beside the version number in the menu opens that folder. Security problems go through the
repository's `SECURITY.md` rather than an issue.

### Thanks

- Thanks to **@GrummelFritz** for reporting that planes took far too long to shoot down, which led
  to the rate-of-fire fix (#261), and that the engine's black smoke was missing on acceleration
  (#262).
- Thanks to **@legluondunet** for asking for a portable mode for the Lutris installer (#259).
- Thanks to **@phly95** for reporting the HD textures that the original has in two formats (#255).

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
