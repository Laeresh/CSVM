## CSVM v0.8.0

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive / Microsoft). It
plays the original game from your own retail install, and neither download contains any game data.

### What you can play

- **The campaign**, with its cabin, briefing and flight-check screens and its cutscene films.
- **Instant Action's** four mission types, free flight, and the **Stunt Race** for one to four
  players, over the Danger Zones or an air race course.
- **Splitscreen Campaign Co-op, Dogfight and Stunt Race** for 2 to 4 players on one machine.
- **Multiplayer over a LAN or the internet:** Deathmatch, team Deathmatch, Capture the Flag,
  Zeppelin vs. Zeppelin, the Stunt Race, and the campaign in co-op. Bots can fill the empty seats.
- **11 aircraft and 8 chapter worlds** in the original's liveries, with the original's weather,
  world animation, music and sound.
- **Gamepads, flight sticks, keyboard and mouse**, with both control screens for binding them, and
  an optional Assisted control scheme.
- **PC VR headsets** on Windows, seated, as an experimental mode.

### New in v0.8.0

**New York rebuilt under Enhanced**

Under Enhanced graphics, C5's city is drawn from new 3D models in place of every original building.

- **Buildings:** towers, walk-ups and the downtown blocks are built with their own facades, lit
  windows, roof shapes and colours, posters on the billboards (the chapter's own among them) and
  cut-out fire escapes.
- **Streets and blocks:** the painted block interiors become a ground of their own, with low
  dressing on it, that fits the new buildings.
- **Traffic:** cars drive the streets on loops round the blocks and down the avenues, with their
  lamps lit.
- **The 3D Vegetation row is now called 3D Scenery,** because it also switches the city. The city
  is drawn with Vegetation at Rich and 3D Scenery on, which is the default. Off brings the original
  city back. Your saved choice carries over.
- In splitscreen the original city is drawn, to keep the frame rate.

**Headlight**

Under Enhanced graphics your plane has a headlight for the night missions: press **H**, or hold
the D-pad down (a tap still changes the view). It reaches about 400 m and casts shadows, and other
players see yours in a network game. It is listed on both Controls pages and can be rebound.

**Rounds light the world**

- Under Enhanced, tracers and rockets in flight light the ground, the aircraft and the volumetric
  clouds around them in their own colours, so a burst of fire glows in the cloud it passes through.
- A plane's muzzle flash seen from outside now lights the aircraft near it, under Original and
  Enhanced, as it does in the original game.
- **Cloud Glow (Off, On):** a new VIDEO row under Volumetric Clouds switches the glow in the
  clouds. It is on by default, and off on integrated graphics, the Steam Deck included, where it
  costs too much.

**Multiplayer**

- **The Multiplayer plaque opens the Multiplayer Games list directly.** It searches the LAN and the
  internet together, refreshes on its own, and has Create Game, Join Game, Join by Code and Build
  Custom Plane on one line. A failed join keeps you on the list. Joining by the host's address is
  no longer offered in the menu.
- **Capture the Flag markers:** the flags and bases that matter now carry a marker at all times:
  the enemy flag, your own flag while it is away from your base, and your base while you carry a
  flag. A new **Flag Markers** row on Game Options turns them off.

**Races**

- **The gate flash** now shows how the segment you just flew compares with the fastest you have
  ever flown it, beside the time against your best run.

**Light and clouds**

- **C1 and C1C under the volumetric clouds** see about 8 km through the fog below the cloud deck,
  the map's edge fades into that fog, and far clouds dissolve into their own haze.
- **C1's cloud deck under the volumetric clouds is thinner** and more of it is torn open, so its
  deck missions are less dark.
- **The CM07 hangar** you take the Bloodhawk from is lit by work lamps under Enhanced.
- **Night vegetation:** under Enhanced, the trees and bushes take the mission's light, so in CM08
  they stand in the moonlight with the ground instead of in daylight green.
- **The cockpit in dark missions:** under Enhanced the instruments keep enough light to read in the
  darkest missions.

**Shorter loads**

A mission load decodes the HD textures in parallel and compiles its shaders while the load screen
is up. On the development PC a C5 load with the HD texture pack went from about 18 to about 12
seconds. The main menu no longer stutters while those shaders compile.

### Fixed in v0.8.0

**Campaign**

- The Great Plane Robbery no longer gets stuck when the Spruce Goose stops at its second barge
  (#367).
- A mission's prebattle music loops instead of playing once and going silent (#358).
- After four failed attempts at a mission, the debrief asks whether you want to skip it, as the
  original does (#362).
- Your plane's turret gunners now fire on the ground targets and zeppelin engines they pick while
  an enemy aircraft is in reach. They used to hold fire on everything but aircraft (#360).

**Graphics**

- With Global Illumination on, the water close to the camera is no longer blotched dark (#341).
- A zeppelin hidden by the volumetric clouds no longer shows through the Speed Wisps (#349).
- A second flight in a session takes Global Illumination's sky light again.
- C5 under the volumetric clouds shows its night sky instead of a flat grey one.
- Under Enhanced, the rockets and bombs on a plane's pylons take the sun and the shadows like the
  plane they hang on (#373).
- In C5's new city, generic towers no longer stand inside the hand-modelled buildings where the
  original's layout stacks them (#363).

**Menus and races**

- A dropdown's text sits inside its box, on both AMMO SELECTION screens and in Instant Action
  (#376).
- Instant Action's loadout keeps its other fields drawn while a list is open.
- Instant Action's join board greys the planes a course refuses in its Edit Bot plane list (#343).
- The race results board names every zone of a course with more than 22 zones (#333).

**Bots**

- A Zeppelin vs. Zeppelin torpedo bot keeps after the enemy hull until it is in range and
  launches only when it can reach it, instead of turning away 1.6 km out (#247).

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

**Coming from v0.7.1 or earlier:** unpack v0.8.0 into a new folder. Your saved games, settings and
custom planes live outside the game folder and carry over. Move whatever you keep beside the old
executable into the new folder: a copy of the game files (`CrimsonSkiesGame`, `GAME` or an `.iso`),
`HD`, `HDModels` and `HDSounds` packs, a `Courses` folder, a `config.json`, and in portable mode
`portable.txt` and `userdata`.

### Multiplayer

- **Modes:** Deathmatch, team Deathmatch, Capture the Flag, Zeppelin vs. Zeppelin and the Stunt
  Race from the Multiplayer Lobby, with or without bots, and the campaign in co-op for up to four
  players, where guests follow the host through the cabin, briefing, hangar and debrief.
- **Courses:** a Stunt Race host picks a course in the Course box on Mission Options, and guests
  see its title in theirs. A guest who cannot take the host's course, or whose plane the course
  refuses, is told why and cannot stand Ready. A network course race starts from the lobby only.
- **Joining:** the Multiplayer Games list shows the public games on the internet and the games on
  the same network. A guest picks one, or enters the host's join code with Join by Code.
- **Hosting over the internet:** a host gets a join code from the master server, and a guest who
  joins by code needs no router setup on either side. The first time you host on Windows, the
  firewall asks whether to allow CSVM on your network; allow it.
- Every player needs version 0.8.x. A v0.7 build cannot join a v0.8.0 game, and is told so when it
  tries. Each player's graphics options are their own: one player can fly with volumetric clouds
  while another flies without.

### Known issues

Nothing here stops a mission from ending or loses progress. Only the larger ones are listed
here.

**Freeze on NVIDIA**

- **The game can freeze** on an NVIDIA card under Windows: the picture stops, the game stops
  responding and has to be closed, and the graphics card stays busy. It has happened on the
  development PC (RTX 5080) every 30 to 60 minutes of play on some days and not at all on others,
  with Enhanced graphics, Global Illumination and SSIL on (#380). Starting the game with
  `--render-thread safe` on its command line (a shortcut's target, or the launch options in Steam)
  may avoid it, at some cost in frame rate; that is not yet confirmed. If it happens to you, a bug
  report with the log helps.

**Volumetric clouds and global illumination**

- **Cost:** the clouds are the most expensive option CSVM has. On an RTX 5080 at 5120x1440, Full
  costs about 5 to 11 ms of GPU time a frame, less at lower resolutions. On the Steam Deck even
  Quarter costs too much, so leave them Off there. Global Illumination costs a dedicated card about
  1 ms a frame and the Deck 6 to 12 ms, which is why it starts Off on integrated graphics.
- **Not yet checked:** the clouds' current look in a VR headset and their cost at Half and Quarter
  on the Steam Deck (#348).

**C5's new city**

- **Traffic:** cars can drive through each other where two loops cross at a junction.

**VR**

- **Runtimes:** VR has been flown with a Quest 3 only, on Virtual Desktop's runtime and on SteamVR
  through Steam Link. Other headsets have not been tried yet. Hand controllers other than Meta
  Touch and Pico 4 reach the game through the runtime's own button mapping, such as SteamVR's
  binding screen, and the Controls pages name every controller's buttons by the Touch layout.
- **Graphics card:** the VR quality profile was sized on an RTX 5080. A slower card may need a
  lower VR Render Scale, a lower resolution in the runtime, or Original graphics. Global
  Illumination is off in the headset.
- **Shadows:** in the headset the sun's shadows are hard-edged. The engine version CSVM uses makes
  the soft shadow cost more the farther you fly from one corner of the map, and that cost is too
  high for 90 Hz in VR.
- **Cockpit gaps:** the original cockpit models were never meant to be seen in VR. They are very
  simple and have many gaps, so in some cockpits the world is visible through the cockpit, around
  the gauges and beside the Bloodhawk's centre panel, and parts are missing (#207).
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
  of up to about 0.3 s (#246).

**General**

- **Race bots:** on a course they ride fixed lines through the gates instead of flying them on the
  flight model (#322).
- **Performance:** on the Steam Deck, splitscreen under Enhanced Graphics runs well below 60 frames
  per second, and C3's Rich jungle canopy costs a solo flight 3.5 to 5.5 ms a frame. Vegetation set
  to Original or 3D Scenery Off gives that back.
- **Graphics switch:** the first switch to Enhanced in a session can wait 5 to 10 seconds behind
  its cover screen.
- **Graphics:** some details still differ from the original, among them the ground shadow on steep
  slopes.

### Reporting a problem

Bugs go in an issue through the bug report form at
<https://github.com/Laeresh/CSVM/issues/new?template=bug_report.yml>. It asks for your system
(Windows, Linux or Steam Deck), the build version and the newest file in the `logs` folder beside
the executable, which between them usually identify a fault without a round trip. The page icon
beside the version number in the menu opens that folder. Security problems go through the
repository's `SECURITY.md` rather than an issue.

### Thanks

- Thanks to **@WRK9722KK** for the request that became the headlight and the muzzle flash that
  lights the planes near it (#340).
- Thanks to **@MiG41Flatpack** for reporting the softlock in The Great Plane Robbery (#367) and
  for the subtitle request (#374), which is in progress.
- Thanks to **u/fallingliquid** for reporting the prebattle music that stopped after one play and
  the turret gunners that ignored ground targets.
- Thanks to **@GrummelFritz** for the cloud and fog changes in C1, the lit CM07 hangar, and the
  idea of tracers and rocket motors that glow in the clouds.

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
