## CSVM v0.3.0

CSVM is a fan-made remake engine for **Crimson Skies** (2000, Zipper Interactive / Microsoft). It
plays the original game from your own retail install, and neither download contains any game data.

### What you can play

- **The campaign**, with its cabin, briefing and flight-check screens and its cutscene films.
- **Instant Action's** four mission types, and free flight.
- **Splitscreen Campaign Co-op and Dogfight** for 2 to 4 players on one machine.
- **Multiplayer over a LAN or the internet:** Deathmatch, team Deathmatch, Capture the Flag,
  Zeppelin vs. Zeppelin, and the campaign in co-op.
- **11 aircraft and 8 chapter worlds** in the original's liveries, with the original's weather,
  world animation, music and sound.
- **Gamepads, flight sticks, keyboard and mouse**, with both control screens for binding them.

### New in v0.3.0

**Multiplayer**

Multiplayer now finds games through a master server, so players can see open games and join over
the internet without setting up their routers.

- **Join by code:** players connect through WebRTC by entering a short join code, so the host needs
  no open router port.
- **Public and private games:** a private game stays off the master server's games list but can
  still be joined by code or found by a LAN search.
- **Copying the code:** the host copies the join code by clicking, tapping or pressing a pad
  button, and the Connection page has its own "Join by code" option.
- **Capture the Flag** and **Zeppelin vs. Zeppelin** start from the lobby and are scored by team.
- **Teams:** named lobby teams play a team Deathmatch, and teammates' markers show as friendly.
- **Custom planes over the network** are allowed by Allow Custom Planes and Outlaw Components, and
  the lobby's Select... opens the outlaw list.
- **Co-op seats:** each co-op player picks their own plane from the host's hangar, and a guest
  machine with several pads flies one seat per player.
- **In-flight chat** goes to everyone or to your lobby team.
- **Rearm bases** restore health, armour and the full loadout each time you fly into your base.
- **Host controls:** the host can remove a guest and set an optional password.
- **Callsigns:** your callsign appears in kill lines, on co-op seats and on the HUD marker other
  players see.
- **Pilot voices:** each player's in-flight lines use the pilot voice they chose.
- **Mode briefings:** the Dogfight load and pause screens show the original's briefing for the
  mode being flown.

**Graphics**

- **Live graphics switch:** G in flight, or an Options change, switches between Original and
  Enhanced without a restart.
- **Anti-aliasing** offers Off, FXAA, SMAA, TAA or FSR 2.2.
- **Render Scale** runs from 50% to 200%, and FSR upscales anything below 100%.
- **Shadow Quality** runs from Off to Ultra. On an integrated GPU such as the Steam Deck's, the
  default is High, and Off in three- and four-pane splitscreen.
- **View Distance** runs from Normal to Maximum under Enhanced, with Far as the default.
- **Lit explosions:** under Enhanced, fireballs and burning debris light their surroundings, smoke
  is shaded by the sun, and rockets and bombs leave scorch marks.
- **Lit clouds:** under Enhanced, the clouds are shaded by the sun and their puffs are fully
  rendered.
- **Performance:** CM24 under Enhanced runs at about 70 fps on the Steam Deck at 67% Render Scale.

**Controls and Steam Deck**

- **On-screen keyboard:** on the Steam Deck, selecting a text field opens Steam's keyboard.
- **Smooth look mode:** a released right stick leaves the view where you pointed it.

**Audio**

- **Cockpit engine pitch:** in the Cockpit view, the engine sound follows the throttle, and the
  original's constant sound is an option.

**Menus**

- **Folder shortcuts:** two icons beside the version number open the logs folder and your user
  data folder.

### Fixed in v0.3.0

**Multiplayer**

- A network flight starts on every machine at the same moment.
- A co-op guest plays the campaign films in step with the host.
- A co-op guest sees the host's wingmen in the planes the host picked.
- A co-op guest hears the host's AI radio call-outs.
- CM09 co-op no longer leaves a frozen plane in the sky.
- Deathmatch posts the original's kill messages once on every machine and names the host by their
  player name.
- A menu Deathmatch flies the chapter's MP1 map with its rearm bases.
- Above the Clouds flies on C1C, as in the original.
- Every multiplayer mode uses the original's point values.
- A Dogfight pause lays out its buttons like the Instant Action pause.
- The AI uses the same pilot voice on every machine.
- Another player's plane shows its torn panels and fire as its armour goes down, not only when it
  crashes.

**Graphics**

- Under Enhanced, the banding across open water and the noise in the aircraft's self-shadow are
  gone.
- Under Enhanced, the plane's shadow stays visible at longer distances.
- Under both graphics modes, the moon, stars and glow sprites no longer show square outlines.
- Original graphics keeps four bits of alpha on alpha textures, as the original game does.
- Each splitscreen pane shows the fog of its own camera's zone.
- In splitscreen Cockpit view, you can see the other pilots' bodies in their aircraft.
- On the Steam Deck, four-pane splitscreen no longer crashes under Enhanced.
- CM20 under Enhanced no longer shows a flipped black roof above the clouds.
- A hidden island's palm trees no longer stand in the water.
- Tree cards blend correctly with the ground behind them.
- A fired flare's star fades in and out and flashes only once.
- Wing-light flares face the camera from every angle.
- A shot hitting an aircraft no longer draws an extra flash.
- The exhaust plume matches the original's darkness and width.
- The cabin memento fits its tilted frame.

**Controls**

- The chase camera sits where the original's does and trails the aircraft's turns at the
  original's rates.
- The look stick no longer makes the camera jump as it passes its centre.
- On Linux, a flight stick or throttle that the system also lists as a gamepad is read as a stick
  instead of a gamepad.
- A flight stick lever bound on two stick models reads the one that is connected.
- A throttle resting near full travel registers full when pushed all the way.
- Two unnamed sticks on one binding row keep their model names.
- On Linux, the log says why SDL2 failed to load.
- On the controls screen, a new binding on Control A or Control B replaces that slot and leaves the
  other column alone.
- The Views 2 page lists the original's nine look rows, one numpad key each.
- The pause menu's pad and arrow-key cursor moves between buttons by their position on screen.

**Audio**

- The Nose view uses the cockpit engine sound, as the original does.
- The engine sound starts at full volume on spawn and respawn.
- Radio lines lower the volume of every engine while they play.
- The autogyro plays its own rotor sound.
- The menu music pauses during a film and resumes afterwards.
- Capturing the Balmoral during a burst of fire no longer leaves its gun sounds playing.
- A downed pursuer no longer sets off the "Bandit at six" call-out.

**Campaign and missions**

- In CM21, the Cabbie flies low between the buildings, no longer restarts his engine in the air,
  and leaves at his destination.
- In CM10, an attack balloon's target marker no longer appears on the water before the balloon
  does.
- In CM15, the gun assist aims at the crane's yellow striped arm.
- In CM19, the captured Warhawk keeps its own paint.
- A campaign wingman flies the weapons picked for it in the hangar.
- Special wingmen use the correct AI.
- An AI aircraft that becomes active keeps its patrol route.
- A lost campaign aircraft no longer offers a respawn.
- After a respawn, the pilot is back in his seat.
- The cabin ammo page shows one list at a time and skips empty slots.
- The loading screen no longer shows a still propeller behind the spinning one.
- Destroying a zeppelin's gas bag or engine no longer freezes the game for a moment.

**General**

- The flight view no longer shows the speed, altitude, throttle and damage text in its upper left
  corner, which the original does not have. The dials carry the same readings.
- F12 screenshots are saved in `Screenshots/` beside the executable.
- The free-flight pause menu responds to the mouse.

This is still an early build. Neither download is code-signed, so Windows SmartScreen warns you the
first time you run the Windows build; the `README.md` inside each download says how to check the
file against the SHA-256 below instead.

### What you need

**Windows:**

- Windows 10 or 11, 64-bit.
- A graphics card with a working Vulkan or Direct3D 12 driver. Below that line the program starts
  and vanishes a few seconds later, at the intro film, with no message; the `README.md` in the zip
  says how to recognise that case from the log.
- A retail Crimson Skies install on the same machine. It is only read, never modified.

**Linux and Steam Deck:**

- x86_64 Linux with a working Vulkan driver (current Mesa for AMD and Intel, or NVIDIA's own
  driver). SteamOS on the Deck has what it needs.
- A copy of the folder a Windows install of Crimson Skies creates, the one holding `ZBD` and
  `GOSDATA` side by side. The case of the file names does not matter.
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

**Coming from v0.2.0:** unpack v0.3.0 into a new folder and extract once more there. Your saved
games, settings and custom planes live outside the game folder and carry over.

### Multiplayer

- **Modes:** Deathmatch, team Deathmatch, Capture the Flag and Zeppelin vs. Zeppelin from the
  Multiplayer Lobby, and the campaign in co-op for up to four players, where guests follow the host
  through the cabin, briefing, hangar and debrief.
- **Joining:** a guest picks a public game from the list, enters the host's join code, finds games
  on the same network through the LAN search, or joins by the host's address, IPv4 or IPv6.
- **Hosting over the internet:** a host gets a join code from the master server, and a guest who
  joins by code needs no router setup on either side. Joining by address instead needs UDP port
  47500 to reach the host; CSVM asks the router to open it (UPnP for IPv4, an IGD v2 pinhole for
  IPv6), and if the router does not allow that, forward UDP 47500 to the host by hand. The first
  time you host on Windows, the firewall asks whether to allow CSVM on your network; allow it.
- Every player needs the same version, 0.3.x. A v0.2.0 build cannot join a v0.3.0 game, and is
  told so when it tries.

### Known issues

Nothing here stops a mission from ending or loses progress. Only the larger ones are listed
here.

- **Performance:** on the Steam Deck, splitscreen under Enhanced Graphics runs well below 60 frames
  per second. CM23 hosted as 2-player network co-op under Enhanced runs at about 30 frames per
  second.
- **Graphics switch:** the first switch to Enhanced in a session can wait 5 to 10 seconds behind
  its cover screen.
- **Linux:** flight sticks have been tried on few models, and finding an install inside a Wine or
  Proton prefix is untested. Reports either way are welcome.
- **Radio chatter** during a fight is much rarer than in the original.
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

- Thanks to **Daniil Sokolyuk** (@DaniilSokolyuk) for the live graphics switch and the View
  Distance option (#27).
- Thanks to **@ajaygunn** for reporting that the menu music kept playing during campaign films
  (#83).
- Thanks to **@nrbk999** for reporting, with logs, that a HOTAS on Linux was read as a gamepad
  (#130), and that the controls screen moved bindings between Control A and Control B (#131).

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
