## CSVM v0.7.0

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

### New in v0.7.0

**Volumetric clouds**

A new **Volumetric Clouds** row on the VIDEO page (Off, Quarter, Half, Full) replaces the cloud
deck and the cloud sprites with clouds you can fly over, under and through. It applies under
Enhanced graphics only, switches at once, and is **off by default**, since it costs more than any
other option.

- **Weather per chapter:** each chapter has its own sky. Some have scattered fair-weather cumulus,
  some a deck you climb through and look down on, and the storm missions a dark layer of towers. A
  thick deck thins in patches about a kilometre across, so an overcast has lighter and darker
  masses and opens onto the ground or sea in places.
- **Light:** the sun lights the clouds from its side, shades their undersides and casts light
  shafts through the gaps under a deck. The cloud shadows on the ground follow the clouds you see.
- **Clear air around the zeppelins:** every zeppelin, and the Black Hat fortress in C4, sits in a
  bubble of clear air, so the clouds never hide a mission's target or the hull you are docking with.
- **Everything else takes the clouds:** rain, smoke, fire, explosions, tracers, rocket trails and
  see-through world objects such as radio masts are veiled by the cloud in front of them, and drawn
  in full where no cloud is between them and you.
- **Resolution:** Full marches the clouds at the screen's resolution; Half and Quarter march fewer
  pixels, cost less and look softer.

**Global illumination**

A new **Global Illumination** row on the VIDEO page (Off, On), under Enhanced graphics. Light now
bounces off the ground and the walls around you: a hangar's lamps light the zeppelin and the cars
inside, and a tunnel's lit walls brighten its track. It is on by default on a computer with a
dedicated graphics card, and off on integrated graphics, the Steam Deck included, and in a VR
headset.

**More light and shadow**

All of this applies under Enhanced graphics.

- **Aircraft take the world's shadows:** a plane flying through a mountain's shadow or under the
  cloud shadows darkens with the ground around it, and comes back into the sun above the cloud band.
- **Vegetation takes the terrain shadow:** Rich vegetation, the 3D tree models and C3's jungle
  canopy darken under a hill's shadow, so bushes in a shaded valley no longer glow.
- **The Pandora's hangar is lit:** in the campaign's opening cutscenes the hangar lamps are on, as
  they are in the landing cutscenes. It used to be dark under the hull's own shadow.
- **Shadow Distance (Normal, Far, Extreme):** a new VIDEO row under Cloud Shadows. Far and Extreme
  let the sun's shadows reach 1.5 and 2 times further. Normal is the default and unchanged.
- **3D Vegetation reaches further:** the row gains **Very Far** and **Extreme**, which draw the
  trees and their 3D models 1.5 and 2 times further out than Far.

**Assisted controls**

A new **Controls** row on each player's CONTROLS page (Original, Assisted), for a pad, a stick, the
keyboard or the mouse.

- **Steering:** a sideways stick turns the plane, and the assist banks it for you. A light push
  turns on the rudder alone, a full push makes the hardest level turn the plane can hold.
- **Climbs and loops:** a centred stick holds the climb or dive you left it in. A held pull loops
  the plane, and letting go over the top rolls it upright.
- **On its back,** steering stays relative to the screen: the plane stays inverted while you hold
  the stick, and rolls upright only when you let go.
- **The chase camera stays level** in turns instead of banking with the plane, goes over the top
  of a loop with it, and rolls upright with it.

Original is the default and flies as the original game does.

**Smaller additions**

- **HUD Mode None** now keeps the selected target's box and its off-screen arrow, without the text.
- **Air race courses can name the planes they admit.** A course file can leave out a plane too wide
  for its gates, or hold a race to one plane. Studio Lot Dash (C2) now refuses the Balmoral, which
  could not fit two of its tunnels. Instant Action greys a refused plane in its Pilot Plane lists, and the
  lobby takes a refused plane off a guest's Ready and puts a bot on it back on Random.
- **Next Enemy in a race** selects the next gate first, ahead of the other planes, and passing a
  gate selects the one after it.
- **One name per map:** Free Flight, Instant Action and Multiplayer now use the same name for each
  chapter. A translated install shows the game's own word.

### Fixed in v0.7.0

**Graphics**

- Under Enhanced, an aircraft's shadow on itself holds through a roll: the upper wing's shadow on
  the lower wing no longer breaks into a moving gap, dots or holes.
- A shot-down aircraft's falling wreck moves smoothly. It used to jump in whole simulation steps
  against the camera, most visibly where the game ran slower than the display.

**Races**

- A race no longer freezes for about half a second at GO with many bots on a course.
- A live respawn held before time up now gets its run, even when the count or the flight to the
  course's first gate runs past time up. It used to lose both runs. This holds for network guests
  too.
- On a course with many gates, the race results board shows ten gates at a time with a scroll bar
  under them, instead of overlapping the times.
- A spectating pane keeps the mouse pointer hidden, also after a right-button look.

**Controls**

- A controller's B button can be bound: a tap binds it, and holding it for a second cancels the
  capture.

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

**Coming from v0.6.0:** unpack v0.7.0 into a new folder. Your saved games, settings and custom
planes live outside the game folder and carry over. Move whatever you keep beside the old
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
- Every player needs the same version, 0.7.x. A v0.6.0 build cannot join a v0.7.0 game, and is
  told so when it tries. Each player's graphics options are their own: one player can fly with
  volumetric clouds while another flies without.

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
- **Speed cue wisps** show a zeppelin that the clouds hide behind them (#349).
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
- **Fire sprites:** a small fire can keep burning in the air after the truck or engine it was on is
  gone, seen on CM01's bridge and in CM08 (#351).
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

- Thanks to **@GrummelFritz**, whose reports and requests again led to many of this release's
  options and fixes: aircraft in the terrain and cloud shadows (#324), vegetation in the terrain
  shadow (#339), the lit Pandora hangar (#338), the aircraft self-shadow fix (#326), Shadow
  Distance, the Very Far and Extreme 3D Vegetation, the target marker under HUD Mode None, and the
  Assisted controls.

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
