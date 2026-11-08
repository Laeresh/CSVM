## CSVM v0.6.0

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
- **Gamepads, flight sticks, keyboard and mouse**, with both control screens for binding them.
- **PC VR headsets** on Windows, seated, as an experimental mode.

### New in v0.6.0

**Air race courses**

Stunt Flying and the Stunt Race can now fly an authored course of gates in place of the Danger
Zones. This is a remake addition; the original has no such mode.

- **The race:** you take the gates in order from a flying start, through pylon gates and rings
  held up by balloons, some of them moving. Touching a gate costs 3 seconds, and a touched pylon
  loses its top, which falls while the stump grows back. Passing a gate shows your split against
  your best run. The HUD counts gates and
  penalties, a yellow fill marks the gate after the next one, and a thin red cross marks the next
  gate when you approach it from the wrong side. Best times are kept per course and per plane.
- **Seven courses ship:**
  - NW Boeing Field (C1): Airfield Out and Back
  - NW Lighthouse (C1B): Island Loop
  - Hollywood Studio (C2): Studio Lot Dash
  - Hawai'ian Islands (C3): Harbour Ring Run and Valley Run
  - Sky Haven (C4): Crater Lakes
  - Manhattan (C5): River Run
- **Starting one:** a Course box appears in Instant Action's Stunt Flying and on a lobby's Mission
  Options for a Stunt Race, in any chapter with a course. A chapter with a course offers these
  modes even when it has no Danger Zones.
- **Racing others:** courses work solo, in splitscreen, over the network and with race bots. Each
  splitscreen pane shows the cues of its own run, and a pylon cut shows for every racer on every
  machine. A lobby host's course reaches every guest, who keeps a copy, so a guest needs no file.
- **Race bots** fly the course on lines fitted to the world and to their own airframe, knife-edge
  where a level line does not fit.
- **Making one:** starting with `-- --course-edit=C1` opens the course editor over that chapter,
  and Shift+F3 shows or hides it. It places gates and the start point, reuses a gate in either
  direction, and saves into a `Courses` folder beside the executable. A course file there wins over
  a shipped one of the same name. `docs/cli.md` in the repository has the editor's controls.

**Enhanced light and shadow**

All of this applies under Enhanced graphics. Original graphics is unchanged.

- **Terrain shadows:** hills and cliffs now shade the ground, the water and the buildings under
  them. The light aims at the sun or moon you can see in the sky. Shadow Quality Off turns the
  shade off.
- **One shadow per object:** the original's painted shadow decals are hidden while the sun casts,
  so nothing draws two shadows. They return at Shadow Quality Off and under Original graphics.
- **Cloud Shadows (Off, On):** soft shadows drift over the ground, the water, buildings and
  vegetation by day, tuned per chapter. Aircraft do not receive them. The new row on the VIDEO page
  is on by default and applies at once.
- **Post-processing (Off, On):** a colour grade with more contrast and saturation, against the
  washed-out look of the early missions. The new row on the VIDEO page is on by default and applies
  at once.
- **Chapter moods:** each chapter now has its own light. C1, C1C and C4 are darker, C1B's cloud
  deck is darker, C2 has a warmer, stronger sun, C3 a bluer sky and C5 a softer grade.

**Shores and the jungle**

- **The ocean reaches the coast:** with Ocean Waves ticked, the waves now run up to the shoreline in
  C1, C1B, C2 and C3, and up to C2's quay walls. Procedural surf with moving foam breaks on the
  shore, each breaker's swash runs up the sand and rock and drains back, and spray goes up at the
  cliff feet in C1 and C2.
- **C3's jungle canopy:** under Rich vegetation the crowns are denser and come in three shapes and
  several sizes, with a lit and a shaded side. The far canopy is rendered from the same crowns, so
  near and far match.
- **Smoke and fire at a distance:** under Enhanced they now fade out at a range set by View
  Distance: twice the original's at Far, the default, four times at Very Far, and out to the fog at
  Unlimited.

**Enhanced Sound and sound packs**

Two new rows on the AUDIO page apply at once.

- **Enhanced Sound** is on by default, so an existing install hears it too. The engine note spans
  a wider pitch and gets louder with the throttle, in every view, and hits on your own aircraft are
  louder. Untick it for the original's sound.
- **Hit Confirm** plays a high ricochet when your guns hit an enemy aircraft. It follows Enhanced
  Sound until you set it yourself.
- **Sound packs:** a folder named `HDSounds` beside the executable holds packs, as a zip or a
  subfolder each. A `.wav` or `.ogg` replaces the original sound of the same file name, under
  Enhanced Sound only. The menu screens' sounds cannot be replaced. A pack can also override the
  Enhanced Sound amounts, and starting with `-- --sound-lab` adds a panel on Shift+F9 that tunes
  them and saves into an unzipped pack's override file. The format is in
  `docs/formats/sounds.md` in the repository.

**HUD and camera**

All four are rows on the GAME OPTIONS page and apply at once, over the pause sheet too.

- **HUD Mode (Full, Minimal, None):** thins the HUD in the outside views. Minimal replaces the dials
  and compass with the flight text block. None keeps only the pipper, the Lead Marker, messages,
  prompts and the held scores. `F2` steps through them in flight. Full is the original's HUD and the
  default, and the cockpit view ignores the setting.
- **HUD Layout (Original, Sides, Custom):** Sides moves the two dial columns to the screen's edges,
  an ultrawide's included. `Shift+F2` in flight opens an editor where you drag or nudge the dial
  columns, compass tape and throttle plate, and `Enter` saves them as Custom. The editor takes
  keyboard and mouse, and does not open in a network session or under a headset.
- **Spyglass Size (100%, 125%, 150%, 200%):** scales the disc that shows the selected target.
- **Keep Camera Zoom:** on by default, the external view keeps its zoom across views, missions,
  reloads and restarts. In splitscreen this covers player 1 only. Off gives the original's resets.

### Fixed in v0.6.0

**Graphics and world**

- Under Enhanced, shadows on the water and on nearby objects no longer fade away as you fly closer,
  at Medium, High and Ultra.
- Under Enhanced, an aircraft's shadow on its own wings breaks into fewer dots and holes.
- Fire, smoke and tracers above the clouds no longer show through the cloud deck from below.
- Under Enhanced, the Workers' Voyage's gondola and window strips stay behind the cloud puffs in
  front of them, and a bridge truss in C4 covers the 3D trees behind it.
- Rich vegetation no longer grows inside C1's zeppelin landing circle, or on C3's airfield, coastal
  prisoner camp and British base on the cliff.
- 3D Vegetation Off keeps C3's jungle canopy up to the eye, and no crown model stands in the air at
  the mouths of the Tiki Tunnel.
- C1's rail line and ring road no longer have terrain drawn over them in three places.
- The Devastator's lower left wing carries its white line across the panel seam. The original draws
  that line broken, and the correction applies under both graphics modes.

**Missions and flight**

- A carried passenger now sits in the flown aircraft's rear seat. A model pack's cockpit inside the
  body has no such seat, so it shows no passenger.
- The seventh campaign mission's ladder pickup (CM07) no longer cuts to a view at sea, the ladder
  stays under the passenger through the climb, and the aircraft no longer jumps to the world's
  origin at the start.
- After the bomber capture in the second campaign mission (CM02), the cockpit view shows the
  Balmoral's own interior alone. Options changed over the pause sheet also survive that swap.
- A tapped return in a stunt run keeps the nitro charge the zone was cleared with. It used to refill
  the tank.
- Deathmatch bots with no target in reach fly at the nearest enemy. They used to hold their spawn
  heading for the whole match.
- Stunt Race bots fly as fast as their plane allows and slow for a bend by their plane's own turn.
  A field of different planes used to fly one speed. A bot's skill no longer sets its throttle, only
  how often it passes over a zone or gate and comes round again.
- In a network Stunt Race or Dogfight, the host's bots keep flying under the host's pause sheet and
  in photo mode. In a Stunt Race they used to stop there.
- The particle pass of a large dogfight costs about half of what it did.

**Menus and game files**

- The mouse pointer stays hidden during a mission's cutscenes and on the menu after a pause sheet's
  Exit. The pre-mission film and the briefing screens are unchanged.
- The game-file copy retries a folder rename that a scanner briefly refuses, and replacing an
  existing copy no longer fails at the end of the copy.
- A damaged game archive reads as absent instead of crashing, and choosing a drive's root in the
  folder picker no longer fails on its system folder.

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

**Coming from v0.5.0:** unpack v0.6.0 into a new folder. Your saved games, settings and custom
planes live outside the game folder and carry over. Move whatever you keep beside the old
executable into the new folder: a copy of the game files (`CrimsonSkiesGame`, `GAME` or an `.iso`),
`HD` and `HDModels` packs, a `config.json`, and in portable mode `portable.txt` and `userdata`.

### Multiplayer

- **Modes:** Deathmatch, team Deathmatch, Capture the Flag, Zeppelin vs. Zeppelin and the Stunt
  Race from the Multiplayer Lobby, with or without bots, and the campaign in co-op for up to four
  players, where guests follow the host through the cabin, briefing, hangar and debrief.
- **Courses:** a Stunt Race host picks a course in the Course box on Mission Options, and guests
  see its title in theirs. A guest who cannot take the host's course is told why and cannot stand
  Ready. A network course race starts from the lobby only.
- **Joining:** a guest picks a public game from the list, enters the host's join code, finds games
  on the same network through the LAN search, or joins by the host's address, IPv4 or IPv6.
- **Hosting over the internet:** a host gets a join code from the master server, and a guest who
  joins by code needs no router setup on either side. Joining by address instead needs UDP port
  47500 to reach the host; CSVM asks the router to open it (UPnP for IPv4, an IGD v2 pinhole for
  IPv6), and if the router does not allow that, forward UDP 47500 to the host by hand. The first
  time you host on Windows, the firewall asks whether to allow CSVM on your network; allow it.
- Every player needs the same version, 0.6.x. A v0.5.0 build cannot join a v0.6.0 game, and is
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

- **Race bots:** on a course they ride fixed lines through the gates instead of flying them on the
  flight model (#322). On Studio Lot Dash, bots in most planes can strike a tunnel, and the Balmoral
  has no clean line through two of them (#322, #323).
- **Race results:** on a course with many gates, the times on the results board's splits overlap
  (#330).
- **Aircraft self-shadow:** under Enhanced, the shadow an aircraft casts on itself is still hard to
  read in the day missions (#299), and it can flicker and show holes (#326).
- **Performance:** on the Steam Deck, splitscreen under Enhanced Graphics runs well below 60 frames
  per second. Outside C3, a solo flight under Enhanced runs as fast as in v0.5.0 or faster; Cloud
  Shadows and Post-processing together cost the Deck about 0.2 ms a frame. In C3, the Rich vegetation
  a solo Deck runs by default draws the new jungle canopy at 3.5 to 5.5 ms more a frame (58 to 68
  frames per second where v0.5.0 held 85 to 90). Vegetation set to Original or 3D Vegetation Off gives
  that back. Ocean Waves, which the Deck leaves off, cost about 8 ms more at C3's coast when ticked
  (47 frames per second where v0.5.0 held 78). These runs had Ground Cover on, which the Deck leaves
  off by default. Loading a mission takes 1 to 2 seconds longer than in v0.5.0, on the Deck and on a
  PC.
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

- Thanks to **@GrummelFritz**, whose reports and requests led to most of this release's options and
  many of its fixes: cloud shadows (#291), the HUD options (#290), Keep Camera Zoom (#289), Spyglass
  Size (#295), Enhanced Sound and Hit Confirm (#298), the longer reach of smoke and fire (#286), the
  light and grade of the early and night missions (#296, #299), the shadow fixes (#292, #297), fire
  showing through the clouds (#287), the mouse pointer in cutscenes (#288), the vegetation on C3's
  bases (#294) and the Balmoral's cockpit (#293).

### AI assistance disclosure

This project is developed with the help of AI coding agents. Parsers, engine code and
documentation are AI-assisted and human-reviewed; commit trailers in the repository name the
specific agent and model that did each piece of work.
