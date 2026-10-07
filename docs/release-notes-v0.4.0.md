<!--
Draft for PublishRelease.ps1 -NotesFile. It holds the VR sections only. The rest of the notes (what
you can play, the other new features and fixes since v0.3.0, what you need, setup, multiplayer,
reporting, thanks, the AI disclosure) is written in the version-bump commit, in the shape of
release-notes-v0.3.0.md. The known issues below are provisional until the at-the-headset playtests
settle them. HTML comments do not render on the release page; delete this one before publishing.
-->
## CSVM v0.4.0

### New in v0.4.0

**VR (experimental)**

CSVM can be flown seated in a PC VR headset on Windows, through the OpenXR runtime that is active
on your PC. Virtual Desktop's own runtime (VDXR) is the one it was built on, and SteamVR (which
Steam Link uses for a Quest) is the second. VR is labelled experimental in this release: it has
been flown on one machine, and the known issues below are the ones found so far.

- **Starting it:** turn on VR Mode in Options, on the Video page, and the game restarts in the
  headset, or double-click `CSVM-VR.cmd` beside `CSVM.exe` for one launch in VR. With no headset
  running, the game plays flat and the menu says so.
- **Every mode, one player:** the campaign, Instant Action, the stunt race and network play all fly
  in VR. Splitscreen is not available in VR.
- **Views:** a flight opens in the 3D cockpit, and the headset is your head. `F8`, D-pad down or the
  left controller's Y button cycles Cockpit, Nose and a Chase view fixed to the aircraft.
- **A virtual screen** in front of you carries the menus, briefings, films and the pause board. The
  right hand controller's laser points at it and its trigger clicks; a pad and the keyboard drive it
  as in flat play. Campaign cutscenes play on it in 3D, like looking through a window.
- **The HUD** sits on a panel fixed to the cockpit. Target boxes, the gun pipper and the arrows to
  targets out of view stand at their real place in the world, the gunsight's reticle is focused at
  infinity, and the spyglass picture appears beside the arrow to its target.
- **Hand controllers:** the left controller's menu button pauses, its stick click recenters and its
  Y button cycles the views. Both Controls pages rebind them while a headset is running. You still
  fly with a gamepad, a flight stick or the keyboard.
- **Comfort:** every cut, respawn and view change fades through black. A Comfort Vignette (Off,
  Mild, Strong; off by default) darkens the edge of view while the aircraft turns, and VR Render
  Scale (50 to 125 %) trades sharpness for GPU time. `Home` recenters your view on the pilot's eye.
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

- **Runtimes:** VR has been flown on Virtual Desktop's runtime with a Quest 3 only. SteamVR and
  other headsets have not been tried yet. Hand controllers other than Meta Touch and Pico 4 reach
  the game through the runtime's own button mapping, such as SteamVR's binding screen, and the
  Controls pages name every controller's buttons by the Touch layout.
- **Graphics card:** the VR quality profile was sized on an RTX 5080. A slower card may need a
  lower VR Render Scale, a lower resolution in the runtime, or Original graphics.
- **Shadows:** in the headset the sun's shadows are hard-edged. The engine version CSVM uses makes
  the soft shadow cost more the farther you fly from one corner of the map, and that cost is too
  high for 90 Hz in VR.
- **Colours under Enhanced:** the HUD panel, the target markers, the spyglass picture and the pause
  and results boards look a little different from flat play under Enhanced graphics, because the
  headset's colour grading applies to them a second time.
- **Cockpit size:** every aircraft uses the same cockpit scale and eye position for now. Some
  cockpits may feel slightly too large or too small.
- **Recenter on an Xbox pad:** no pad button is free for it, so use `Home`, the left controller's
  stick click or the runtime's own recenter, or bind it on the Controls page.
- **Starting with VR Mode on** shows the flat window for a moment before the game restarts in the
  headset.
- **The desktop mouse** does not line up with the virtual screen. Use a hand controller's laser, a
  pad or the keyboard in the headset's menus.
- **Cutscenes** have shallow depth, and something close to the camera at the edge of the picture is
  cut off by the screen's edge. The campaign films play flat on the screen, as they were made.
- **Linux:** VR is Windows only.
