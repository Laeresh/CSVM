<!--
Draft for PublishRelease.ps1 -NotesFile. HTML comments do not render on the release page; delete
this one before publishing.

VR's release-time checks, after the bump and .\ExportRelease.ps1:
1. In the clean-machine run (.\RunSandbox.ps1 -Driver sandbox\PublicRelease.ps1 -MapReadOnly
   <retail install> -KeepOpen), double-click CSVM-VR.cmd in Downloads\CSVM-v0.4.0-win64\. The
   sandbox has no OpenXR runtime: the game starts flat with "VR mode is on, but no headset started"
   on the menu, and its log shows the OpenXR warning.
2. At the headset with VDXR running, double-click .scratch\export\CSVM-VR.cmd: the newest file in
   .scratch\export\logs\ carries "xr: OpenXR live runtime=" and "[world] vr quality: profile=vr".
   Then turn VR Mode on in Options, quit, double-click CSVM.exe: the game restarts in the headset.
Never run CSVM-VR.cmd beside the real exe from an agent: it starts OpenXR on the attached headset.
-->
## CSVM v0.4.0

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

### New in v0.4.0

**VR (experimental)**

CSVM can be flown seated in a PC VR headset on Windows, through the OpenXR runtime that is active
on your PC. Virtual Desktop's own runtime (VDXR) and SteamVR (which Steam Link uses for a Quest)
have both been flown. VR is labelled experimental in this release: it has been flown on one
machine, and the known issues below are the ones found so far.

- **Starting it:** turn on VR Mode in Options, on the Video page, and the game restarts in the
  headset, or double-click `CSVM-VR.cmd` beside `CSVM.exe` for one launch in VR. With no headset
  running, the game plays flat and the menu says so.
- **Every mode, one player:** the campaign, Instant Action, the stunt race and network play all fly
  in VR. Splitscreen is not available in VR.
- **Views:** a flight opens in the 3D cockpit, and the headset is your head. `F8`, D-pad down or the
  left controller's Y button cycles Cockpit, Nose and a Chase view fixed to the aircraft.
- **A theater screen** in front of you carries the menus, briefings, films and the pause board. The
  right hand controller's laser points at it and its trigger clicks; a pad and the keyboard drive it
  as in flat play. Campaign cutscenes play on it in 3D, like looking through a window.
- **The HUD** sits on a panel fixed to the cockpit. Target boxes, the gun pipper and the arrows to
  targets out of view stand at their real place in the world, the gunsight's reticle is focused at
  infinity, and the spyglass picture appears beside the arrow to its target.
- **Hand controllers:** the left controller's menu button pauses, its stick click recenters and its
  Y button cycles the views. Both Controls pages rebind them while a headset is running, and the
  triggers can be bound to flight actions too (none is by default). You still fly with a gamepad,
  a flight stick or the keyboard.
- **Comfort:** every cut, respawn and view change fades through black. A Comfort Vignette (Off,
  Mild, Strong; off by default) darkens the edge of view while the aircraft turns, and VR Render
  Scale (50 to 125 %) trades sharpness for GPU time. `Home`, `numpad 5`, `numpad 0` or a pad's
  right-stick click recenters your view on the pilot's eye.
- **Performance:** a VR quality profile replaces your graphics options in the headset (no
  anti-aliasing filter over 4x MSAA, Low shadows, no foveated rendering), sized for 90 Hz. Set your
  runtime to 90 Hz at its default resolution. Starting with `CSVM-VR.cmd -- --xr-quality=flat` flies
  on your own graphics options instead, at several times the cost.

The `README.md` in the zip has a "VR (experimental)" section with the details.

**Bots in multiplayer**

Computer-flown pilots can take any empty seat in a Deathmatch, team Deathmatch, Capture the Flag,
Zeppelin vs. Zeppelin or Stunt Race, over the network, in splitscreen, or alone against them.

- **Adding them:** the Multiplayer Lobby adds, edits and removes bots one at a time or fills the
  lobby up to a number, and the local join board for splitscreen and solo matches has bot rows too. A player who joins a full lobby
  takes the newest bot's place.
- **Skill:** each bot is a Novice, Veteran or Ace.
- **Pilots:** each bot flies for a militia, in that militia's plane, weapons, paint and voice, under
  one of the game's character names. A BOT tag marks it on the scores and the target marker.
- **Every mode:** bots rearm at their base, take and carry home the flag in Capture the Flag, attack
  the enemy hull part by part in Zeppelin vs. Zeppelin, and fly the course and post their times in
  a Stunt Race.
- **Cost:** fifteen bots leave the host's frame rate unchanged, on the Steam Deck too.

**Stunt Race**

Instant Action's stunt run is now a time attack, alone, in splitscreen or over the network.

- **Alone:** every start and restart runs a 3, 2, 1, GO count, and the clock starts at GO. A tap of
  Respawn returns you to the last Danger Zone you cleared, and holding it restarts the run.
- **Splitscreen:** 2 to 4 players race the course inside a time window, ranked by their best run,
  with a live leaderboard and a final run. Racers fly through each other, their guns cannot hit
  another racer, and they show as ghosts up close.
- **Network:** pick Stunt Race in the Multiplayer Lobby to race the chapter's stunt course on
  several machines. At the end the host can restart or return everyone to the lobby.
- **The race board** shows every racer's runs and times, and an Original-looking board under the
  Original presentation.

**The Enhanced ocean**

Under Enhanced, the sea in every chapter that has one (C1, C1B, C1C, C2, C2B, C3 and C5) is a
moving, wave-displaced ocean instead of the original's flat sheet.

- **Waves and foam:** the swell rolls without a visible repeating pattern, its foam reads as foam up
  close, and it fades to flat water at the coast.
- **Ship wakes:** every moving ship and boat draws its own foam wake.
- **Water Quality** in Options chooses waves or flat water. It defaults to waves on Windows and to
  flat water on the Steam Deck and Linux. Original graphics keeps the original's water.

**HD textures**

CSVM loads HD texture packs. Drop a pack's release zips, unchanged, into a folder named `HD` beside
the executable, and the game reads them in place. The first pack is phly95's
[crimson-texture-upscale](https://github.com/phly95/crimson-texture-upscale), which replaces the
world, aircraft and menu art. A pack makes a chapter load about twice as long and uses 1 to 2 GB of
video memory.

**Smaller additions**

- **Installing from the disc:** the extraction screen also reads the Crimson Skies disc or its ISO.
- **In-flight scores:** under Original, holding Display Scores (`Tab`, a pad's Back) shows the
  original's scores, with kills and deaths.
- **Capture the Flag under Enhanced:** the carried flag is a banner towed behind the carrier.
- **Lists that scroll:** Game Scores and the race board scroll past ten pilots, and the lobby chat
  scrolls back.
- **The end of a match** holds for five seconds, then shows Game Scores, as in the original.
- **The lobby** shows each player's plane, and a second player on one machine appears as their
  callsign with a pane number ("Gabriel P2").
- **Splitscreen in a network Dogfight:** every player on a machine flies, and each can fly a custom
  plane.
- **Free Flight and Dogfight** open a local lobby laid out like the Multiplayer Lobby.
- **Briefings** play no music.
- **Free flight and the stunt race** pause on the Instant Action pause board.
- **A stick profile guide** for players, with an example binding for every action.

### Fixed in v0.4.0

**Controls**

- On Windows, a flight stick that the system also lists as a gamepad is read as a stick, as on
  Linux since v0.3.0.
- With the mouse in Fly mode, the aircraft no longer pitches up at the start of a mission or after
  the Controls page. The mouse stick now starts centred.
- After Smooth Look Mode (`J`), the look stick can look down again in the Cockpit and Nose views.
- A pad axis rebound to a flight row takes the stick's deadzone.
- Holding Respawn respawns once, not on every frame. In a network game an in-flight Respawn press
  does nothing, except in a Stunt Race.

**Multiplayer**

- A co-op guest's world follows the host's progress, so CM02's bridge no longer stands whole.
- In CM07 and C4/M03, the player who flies into the hangar or the drop marker owns that scene on
  every machine.
- A kill by a guest, by a scripted event or from the mission log counts alike on both co-op
  machines.
- In Zeppelin vs. Zeppelin the hulls' turrets fire at the other team, and a pilot a turret shoots
  down is no longer charged with a suicide.
- Matches no longer show permanent P1/P2 markers; the match status line stays.
- An AI aircraft near a co-op guest flies at full detail.
- Quitting a network game, or the game itself, no longer crashes now and then.
- CM23 hosted as 2-player network co-op under Enhanced holds 100 frames per second or more, where
  v0.3.0 managed about 30.

**AI**

- AI turrets and pilots shoot down torpedoes in flight.
- Turrets favour the player and target enemy turrets as the original's do.
- AI and bots aim at a moving zeppelin's gas bags, cannons and engines where they will be, not where
  they are.

**Graphics and world**

- Under Enhanced, C5's night city fades into its fog with distance instead of standing out bright
  against a black background, and no band of buildings darkens at a fixed distance.
- The C3 suspension bridge takes hits on its deck and towers, not only one rope.
- The flat moon in C1, C1C and C5 no longer rolls with the aircraft.
- The autogyro's second front propeller shows the blur disc.
- In the cockpit, the Hellhound's and Balmoral's ailerons hold still as in the original, and the
  Warhawk's lights and rudder sit on their hinges.
- The pause chart's icons point where the compass says.

**Menus and Steam Deck**

- The menu cursor steps to the button in the direction you pressed.
- The lobby's Own Flag Home box no longer overlaps Limited Lives.
- A ZBD folder without chapter folders is refused at extraction, and a menu flight that fails says
  why instead of returning to the main menu.
- In Steam's Game Mode, the folder buttons say they only work in Desktop Mode.
- Saves and settings survive the game being killed mid-write, and a settings file that cannot be
  read is set aside instead of overwritten.

This is still an early build. Neither download is code-signed, so Windows SmartScreen warns you the
first time you run the Windows build; the `README.md` inside each download says how to check the
file against the SHA-256 below instead.

### What you need

**Windows:**

- Windows 10 or 11, 64-bit.
- A graphics card with a working Vulkan or Direct3D 12 driver. Below that line the program starts
  and vanishes a few seconds later, at the intro film, with no message; the `README.md` in the zip
  says how to recognise that case from the log.
- A retail Crimson Skies install on the same machine, or the game disc or its ISO. It is only read,
  never modified.
- For VR: a PC VR headset and an OpenXR runtime, such as Virtual Desktop or SteamVR.

**Linux and Steam Deck:**

- x86_64 Linux with a working Vulkan driver (current Mesa for AMD and Intel, or NVIDIA's own
  driver). SteamOS on the Deck has what it needs.
- A copy of the folder a Windows install of Crimson Skies creates, the one holding `ZBD` and
  `GOSDATA` side by side, or the game disc or its ISO. The case of the file names does not matter.
- For a flight stick only, the system's SDL2 library (`libSDL2-2.0.so.0`). SteamOS and most desktop
  distributions have it already.

**Both:** about 1 GB of free disk space for the extracted data. Nothing else has to be installed;
the .NET runtime the engine needs and the WebRTC library internet play uses are inside the
download.

### Setup

1. Unzip or unpack the download into a folder of its own. The Linux tarball's files sit at its
   root, so make the folder first (`README-linux.md` has the commands).
2. Start `CSVM.exe` (Windows) or `CSVM.x86_64` (Linux). With no game data yet it opens a screen
   that offers to extract it, with your install folder filled in when it can find one. Confirm, and
   the menu opens when the extraction is done, after about half a minute.
3. Pick a mode, chapter and plane in the menu.

On the Steam Deck, add `CSVM.x86_64` to Steam as a non-Steam game, leave the compatibility tool off,
and run the first extraction in Desktop mode. After that it starts from Game mode like any other
game. `README-linux.md` has the steps.

**Coming from v0.3.0:** unpack v0.4.0 into a new folder and extract once more there. Your saved
games, settings and custom planes live outside the game folder and carry over.

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
- Every player needs the same version, 0.4.x. A v0.3.0 build cannot join a v0.4.0 game, and is
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
  simple and have many gaps, so in some cockpits the world is visible through the cockpit and parts
  are missing.
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

- Thanks to **@phly95** for the HD texture pack and for asking the game to load it (#214).
- Thanks to **@velmeran** for reporting, with a log, that VIRPIL flight sticks were read as gamepads
  on Windows (#248).
- Thanks to **@nrbk999** for reporting that the aircraft pitched up with the stick at rest, and for
  tracking it down to the mouse's Fly mode (#138).
- Thanks to **@legluondunet** for reporting that a menu flight on Linux returned to the main menu
  (#177).

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
