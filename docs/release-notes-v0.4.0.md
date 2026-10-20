<!--
Draft for PublishRelease.ps1 -NotesFile. It holds the VR sections only, settled from the headset
playtests. The rest of the notes (what you can play, the other new features and fixes since v0.3.0,
what you need, setup, multiplayer, reporting, thanks, the AI disclosure) is written in the
version-bump commit, in the shape of release-notes-v0.3.0.md. HTML comments do not render on the
release page; delete this one before publishing.

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

**Graphics**

- Under Enhanced, the sun's lens flare is drawn in the world: an aircraft or a hill in front of the
  sun covers its core, and the core glows. Original graphics keeps the original's flare.

### Known issues

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
  simple and have many gaps, so in some cockpits the world shows through around the gauges, and
  beside the Bloodhawk's centre panel.
- **Starting with VR Mode on** shows the flat window for a moment before the game restarts in the
  headset.
- **The desktop mouse** does not line up with the theater screen. Use a hand controller's laser, a
  pad or the keyboard in the headset's menus.
- **Cutscenes:** something close to the camera at the left or right edge of the picture is cut off
  by the screen's edge. The campaign films play flat on the screen, as they were made.
- **Linux:** VR is Windows only.
