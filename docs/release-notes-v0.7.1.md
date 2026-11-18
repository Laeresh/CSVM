## CSVM v0.7.1

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive / Microsoft). It
plays the original game from your own retail install, and neither download contains any game data.

v0.7.1 is an update to v0.7.0: fixes, a new feel of speed under Enhanced, and a few smaller
additions. It plays network games with v0.7.0.

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

### New in v0.7.1

**The feel of speed under Enhanced**

Under Enhanced graphics and sound, speed now comes across through sound and the camera instead of
the streaks past the plane.

- **Wind:** with Enhanced sound, a wind rises with your speed and in hard turns, and drops under a
  radio call.
- **Field of view:** the view widens by up to 6 degrees at high speed, now in the cockpit and nose
  views too, and by another 3 degrees while the nitro burns.
- **Buffet:** the plane starts to shake from three quarters of its top speed, ahead of the
  overspeed rattle.
- **Speed Wisps (Off, On):** a new VIDEO row under Global Illumination. The streaks past the plane
  are **off by default** under Enhanced; turn them on to keep them. Original graphics draws them
  as before.

The field of view and the buffet stay off in a VR headset.

**Shores**

Under Enhanced graphics with the waves water quality, the swell in C2 and C3 now runs toward every coast, so the waves come in
onto a sheltered beach instead of travelling out to sea, and the wind's swell is calmer in sheltered
water.

**C3's jungle**

Palms now stand among C3's jungle canopy and along its beaches under Rich vegetation.

**Assisted controls**

- **The cockpit and nose views stay level** under Assisted: the horizon holds and the cockpit rolls
  around it, as the chase camera already did.
- **Track Target holds the target** in a banked turn under Assisted. It used to miss it by 30 to 40
  degrees at a steep bank.

### Fixed in v0.7.1

- On AMD Radeon cards, Enhanced graphics drew black and white blotches over the picture (#356).
- A long load under Enhanced no longer makes Windows report that the game stopped responding.
- Under Enhanced, the cockpit gauges no longer cast shadows on the panel, and the cockpit's own
  shadows, the canopy frame's above all, are smooth instead of stepped.
- A small fire no longer keeps burning in the air after the truck or engine it was on is gone, as
  it did on CM01's bridge and in CM08 (#351).
- Volumetric clouds: the clouds' shadow on the ground takes each chapter's cloud bases as you see
  them.

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

**Coming from v0.7.0 or earlier:** unpack v0.7.1 into a new folder. Your saved games, settings and
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
- **Joining:** a guest picks a public game from the list, enters the host's join code, finds games
  on the same network through the LAN search, or joins by the host's address, IPv4 or IPv6.
- **Hosting over the internet:** a host gets a join code from the master server, and a guest who
  joins by code needs no router setup on either side. Joining by address instead needs UDP port
  47500 to reach the host; CSVM asks the router to open it (UPnP for IPv4, an IGD v2 pinhole for
  IPv6), and if the router does not allow that, forward UDP 47500 to the host by hand. The first
  time you host on Windows, the firewall asks whether to allow CSVM on your network; allow it.
- Every player needs version 0.7.x: v0.7.0 and v0.7.1 play together, and a v0.6.0 build cannot
  join, and is told so when it tries. Each player's graphics options are their own: one player can
  fly with volumetric clouds while another flies without.

### Known issues

Nothing here stops a mission from ending or loses progress. Only the larger ones are listed
here.

**Volumetric clouds and global illumination**

- **Cost:** the clouds are the most expensive option CSVM has. On an RTX 5080 at 5120x1440, Full
  costs about 5 to 11 ms of GPU time a frame, less at lower resolutions. On the Steam Deck even
  Quarter costs too much, so leave them Off there. Global Illumination costs a dedicated card about
  1 ms a frame and the Deck 6 to 12 ms, which is why it starts Off on integrated graphics.
- **Half** holds the frame rate at 60 frames per second even with vsync off, so a faster display
  does not run faster at Half (#346).
- **Speed Wisps,** when turned on, show a zeppelin that the clouds hide behind them (#349).
- **Dark patches** can show on the water when flying very close to it with Global Illumination on
  (#341).
- **Not yet checked:** the clouds' current look in a VR headset and their cost at Half and Quarter
  on the Steam Deck (#348).

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
- **Cockpit view:** in the darkest missions the cockpit's instruments are hard to read (#353).
- **Performance:** on the Steam Deck, splitscreen under Enhanced Graphics runs well below 60 frames
  per second, and C3's Rich jungle canopy costs a solo flight 3.5 to 5.5 ms a frame. Vegetation set
  to Original or 3D Vegetation Off gives that back.
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

- Thanks to **@GrummelFritz**, whose reports led to the level cockpit view and Track Target under
  Assisted, the cockpit's shadow fixes, and the fix for Windows calling the game hung while it
  loads.
- Thanks to **@Kanonenhund** for reporting the blotches on AMD cards (#356) with the logs that
  found them.

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
