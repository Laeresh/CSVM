# Flight stick profiles

CSVM keeps a flight stick's bindings in a small text file, one per stick model. The Controls
screen writes that file for you whenever you rebind a stick and accept, so most players never need
to open it. This guide is for editing it by hand: to set a deadzone (the Controls screen has no
control for one), to set up two sticks flown together, to keep a device such as a gaming keypad from
being read as a stick, or to share a layout with another player.

[`stick-profile-example.json`](stick-profile-example.json) is a complete example that binds every
action a stick can bind, with a comment on each group. Copying it is the quickest way to start.

Flight sticks are always player 1's. Gamepads, the keyboard and the mouse are not affected by
anything in this guide.

## Where the files go

Your own profiles live in the `stick_profiles` folder inside the CSVM user folder:

| System | Folder |
| --- | --- |
| Windows | `%APPDATA%\Godot\app_userdata\CSVM\stick_profiles` (paste it into Explorer's address bar) |
| Linux | `~/.local/share/godot/app_userdata/CSVM/stick_profiles` (under `$XDG_DATA_HOME/godot/app_userdata/CSVM` when that is set) |

The remake Controls screen has an **Open profiles folder** row, which creates the folder if it is
missing and opens it.

CSVM also ships profiles of its own for a few devices (the VKB Gladiator EVO R and L, and the Razer
Tartarus keypad). Those are built into the game and cannot be edited. A file of yours for the same
model replaces the shipped one. The shipped files are in the repository under
[`CSVM/data/stick_profiles/`](../CSVM/data/stick_profiles/) if you want to start from one of them.

**CSVM reads these files once, when it starts.** Close the game before you edit a file and start it
again afterwards. If the game is running while you edit, accepting the Controls screen overwrites
your edit with the bindings the game still has in memory.

## Step by step

1. **Find your stick's model.** Plug the stick in and start CSVM. Open the newest file in the `logs`
   folder next to `CSVM.exe` and find the line beginning `stick roster`. It lists each stick with its
   name and model, a pair of four-digit hex numbers such as `231D/0200` (the USB vendor and product
   id). If no `stick roster` line names your stick, CSVM is not reading it as a flight stick, and a
   profile will not change that.
2. **Copy the example** into the `stick_profiles` folder. Name the copy after the model with a dash
   instead of the slash, for example `231D-0200.json`. That is the name the Controls screen would
   give it, though any name ending in `.json` works.
3. **Set the model.** Replace `"1234/5678"` in the file with your stick's model.
4. **Change the controls** to match your stick (see "Finding a control's number" below), and delete
   the rows for actions you do not want on the stick.
5. **Start CSVM** and check the log (see "Checking that it worked").

## Finding a control's number

The file counts buttons, axes and hats from 0, the way SDL2 (the library CSVM reads sticks through)
numbers them. The Controls screen counts from 1, the way Windows and most stick software do. So a
control the Controls screen shows as `Button 5` is `button:#4` in the file, `Axis 3` is axis `2`, and
`Hat 2 Left` is `hat:1:Left`. The first hat shows without a number (`Hat Up` is `hat:0:Up`).

Two ways to learn a number:

- **Bind it on the Controls screen** and read the label it shows, then subtract one.
- **Watch the stick from the command line.** Start the game as `CSVM.exe -- --dump-sticks=30`
  (`./CSVM.x86_64 -- --dump-sticks=30` on Linux). For 30 seconds it records every control you move,
  then quits. Move one control at a time. The newest log file then has a `sticks watch:` line for
  each movement, such as `axis 5 0.00 -> -0.86` or `button 4 pressed`, and those numbers are already
  the ones the file uses.

## The file

```json
{
  "version": 1,
  "model": "231D/0200",
  "name": "R",
  "companions": [],
  "ignore": false,
  "contexts": {
    "flight": { "FireGuns": ["button:#0"], "PitchUp": ["fullaxis:1+@0.02"] },
    "menu": { "MenuAccept": ["button:#0"] },
    "camera": { }
  }
}
```

- **`version`** is `1`.
- **`model`** is the stick's model as the log prints it, `VVVV/PPPP` in hex. Upper or lower case
  both work. A missing or malformed model makes the game skip the whole file.
- **`name`** is the short label the Controls screen puts in front of the stick's controls, such as
  `R` in `R Button 5`. Leave it out and the screen shows `Stick 231D/0200` instead.
- **`companions`** lists other stick models that must be plugged in at the same time for this file
  to be used. Leave it empty for a stick flown on its own. See "Two sticks" below.
- **`ignore`**, when `true`, makes the game read nothing from this model. See "A device that is not
  a stick" below.
- **`contexts`** holds the bindings. There are three contexts: `flight` (flying), `menu` (menus,
  boards and cutscenes) and `camera` (the free camera you fly while spectating). Each binds action
  names to a list of controls. A list can hold more than one control, and any of them triggers the
  action.

Only actions you bind need to appear. An action the file does not name has no stick binding. A
profile has no defaults of its own, so a missing row is never filled in from somewhere else.

`//` comments and a trailing comma are accepted, which is why the example file can explain itself.
A save from the Controls screen rewrites the file without its comments.

## Controls

A control is written as text in one of four shapes.

| Shape | Example | Meaning |
| --- | --- | --- |
| `button:#<n>` | `button:#4` | Button `n`. |
| `hat:<n>:<direction>` | `hat:0:Up` | One direction of hat `n`: `Up`, `Right`, `Down` or `Left`. A hat has no diagonals. |
| `axis:#<n><sign>@<deadzone>` | `axis:#4+@0.05` | One half of axis `n`: `+` is the half where the axis reads positive, `-` the other. |
| `fullaxis:<n><sign>@<deadzone>` | `fullaxis:1+@0.02` | All of axis `n`, for the four flight controls that have two directions. `-` inverts it. |

**Full axes.** Pitch, roll, yaw and throttle each have two actions, one per direction, and one full
axis drives both. Write it once, under the first action of the pair, and leave the other out:

| Write the full axis under | It also drives |
| --- | --- |
| `PitchUp` | `PitchDown` |
| `RollRight` | `RollLeft` |
| `YawRight` | `YawLeft` |
| `ThrottleUp` | `ThrottleDown` (the throttle moves up or down while the axis is held off centre) |

`+` means the axis reading positive drives the first action of the pair: pulled back pitches up,
pushed right rolls right, twisted right yaws right. If the plane answers backwards, change the `+`
to `-`. The deadzone is the part around the centre that does nothing, from `0` to `0.95`; `0.02` is
a good start for a precise stick.

**The throttle lever.** `ThrottleLever` takes a full axis too, but reads it differently: the lever's
whole travel is the throttle setting, one end idle and the other full. `-` swaps the ends. Here the
deadzone trims both ends of the travel, so the lever reaches idle and full a little before its stops.
The game ignores a parked lever until you move it, so a lever left at full does not open the
throttle when a flight starts.

**Half axes.** `axis:` reads one half of an axis, for actions that have one direction each, such as
the head look (`LookAimUp`) and the free camera. Bind the other half to the opposite action. The
deadzone is the part near the centre that does nothing, and for an on/off action (a menu move, a
weapon) it is how far the axis must travel before the action counts as pressed.

**Buttons and hats** work on any action, including the flight controls: a button on `PitchUp` pitches
up at full deflection while it is held.

## Two sticks

When you fly two sticks together (one in each hand, or a stick and a throttle), each stick model has
its own file, and each file names the other as a companion. For a right stick `231D/0200` and a left
stick `231D/0201`:

- `231D-0200+231D-0201.json` has `"model": "231D/0200"` and `"companions": ["231D/0201"]`.
- `231D-0201+231D-0200.json` has `"model": "231D/0201"` and `"companions": ["231D/0200"]`.

Give each one a `name` (`"R"` and `"L"`) so the Controls screen can tell their buttons apart.

A model may have several files. When more than one applies, CSVM uses:

1. the file naming the most companions that are all plugged in,
2. then a file of yours over a shipped one,
3. then the file whose name sorts first.

So you can keep a solo file and a two-stick file for the same stick side by side: the stick uses its
solo file on its own and the two-stick file when its partner is plugged in. The choice is made again
whenever a stick is plugged in or unplugged, even mid-flight.

Watch out for copies. A copy made in the same folder (`231D-0200 - Copy.json`) applies exactly when
the original does and sorts first, so the game uses the copy. Move backups out of the folder.

## A device that is not a stick

Some devices report themselves as joysticks without being one, such as a gaming keypad. If CSVM sees
exactly one unprofiled device that looks like a stick, it gives that device a default layout, so a
keypad can end up flying the plane. A file with `"ignore": true` stops that:

```json
{ "version": 1, "model": "1532/022B", "ignore": true }
```

## Checking that it worked

Start CSVM with the stick plugged in and open the newest log in `logs`. For each stick there is a
line such as:

```
stick profile for 1234/5678: user 1234-5678.json
```

`user` means your file is in use, `shipped` one of the game's own, and `generic` the default layout.
`none` means no file applies, usually because the model in the file does not match or a companion is
not plugged in. A file the game could not read at all gets a line containing `skipped`, with the
reason.

A single row the game cannot read (a misspelt action name, a control in the wrong shape, a deadzone
above 0.95) leaves just that action unbound and the rest of the file working. The Controls screen
shows that action as unbound for the stick. A later save from the Controls screen keeps that row in
the file exactly as you typed it, so you can still fix it by hand, unless you bind that action on
the screen first, in which case the new binding replaces it.

## Every action

The name in the file comes first, then the name the Controls screen shows.

### `flight`

| Action | On screen | Notes |
| --- | --- | --- |
| `PitchUp` | Point Nose Up | Takes a full axis for pitch. |
| `PitchDown` | Point Nose Down | Leave out when `PitchUp` has a full axis. |
| `RollRight` | Roll Right | Takes a full axis for roll. |
| `RollLeft` | Roll Left | Leave out when `RollRight` has a full axis. |
| `YawRight` | Turn Right | Takes a full axis for yaw (a twist grip or pedals). |
| `YawLeft` | Turn Left | Leave out when `YawRight` has a full axis. |
| `ThrottleUp` | Throttle Up | Moves the throttle up while held. Takes a full axis. |
| `ThrottleDown` | Throttle Down | Leave out when `ThrottleUp` has a full axis. |
| `ThrottleLever` | Throttle (lever) | An absolute lever. Takes a full axis only. |
| `ThrottleSet0` to `ThrottleSet8` | Throttle 0/8 to Throttle 8/8 | Nine fixed settings, idle to full. |
| `FireGuns` | Fire Guns | |
| `FireRockets` | Fire Rockets | |
| `SelectGunGroup` | Cycle guns clockwise | |
| `SelectGunGroupPrev` | Cycle guns counterclockwise | |
| `SelectOrdnance` | Cycle rockets clockwise | |
| `SelectOrdnancePrev` | Cycle rockets counterclockwise | |
| `Nitro` | Use Nitro-Booster | |
| `TargetNextEnemy` | Next Enemy/Objective | |
| `TargetPreviousEnemy` | Previous Enemy/Objective | |
| `TargetNearestEnemy` | Nearest Enemy/Objective | |
| `TargetNextAlly` | Next Ally | |
| `TargetPreviousAlly` | Previous Ally | |
| `TargetNearestAlly` | Nearest Ally | |
| `TargetNextNonAircraft` | Next Non-Aircraft | |
| `TargetPreviousNonAircraft` | Previous Non-Aircraft | |
| `TargetNearestNonAircraft` | Nearest Non-Aircraft | |
| `TargetNearest` | Select Target Nearest Crosshairs | |
| `TargetClear` | Target Nothing | |
| `CycleCockpitViews` | Cycle Cockpit Views | |
| `SelectChaseView` | Select Chase View | |
| `FlybyView` | Access Chase View | The flyby camera. |
| `ToggleSpyglass` | Toggle Spyglass | |
| `ZoomIn` | Zoom In | The outside camera's zoom. |
| `ZoomOut` | Zoom Out | |
| `LookUp` | Look Up | |
| `LookUpRight` | Look Up/Right | |
| `LookRight` | Look Right | |
| `LookUpRightRear` | Look Up/Right/Rear | |
| `LookRear` | Look Back | |
| `LookUpLeftRear` | Look Up/Left/Rear | |
| `LookLeft` | Look Left | |
| `LookUpLeft` | Look Up/Left | |
| `LookCenter` | Look Forward | |
| `LookBack` | Look Behind (hold) | Looks behind while held. |
| `LookAimUp` | Look Aim Up | Moves the head; suits a half axis. |
| `LookAimDown` | Look Aim Down | |
| `LookAimLeft` | Look Aim Left | |
| `LookAimRight` | Look Aim Right | |
| `FreeLook` | Free Look | Free look while held. |
| `SnapLookMode` | Access Snap Look Mode | |
| `SmoothLookMode` | Access Smooth Look Mode | |
| `TrackTarget` | Track Target | |
| `AutoLand` | Auto-Dock | |
| `Pause` | Pause/Quit/Objectives | |
| `Respawn` | Respawn | |
| `ChatEveryone` | Chat to Everyone | Network games. |
| `ChatTeam` | Chat to Team | Network games. |
| `ToggleGraphicsMode` | Toggle Graphics Mode | Switches between the original and the enhanced graphics. |

### `menu`

| Action | On screen | Notes |
| --- | --- | --- |
| `MenuUp` | Menu Up | |
| `MenuDown` | Menu Down | |
| `MenuLeft` | Menu Left | |
| `MenuRight` | Menu Right | |
| `MenuAccept` | Menu Accept | |
| `MenuBack` | Menu Back | |
| `MenuStart` | Menu Start | |
| `MenuLoadout` | Menu Loadout | |
| `MenuPresets` | Menu Presets | |
| `SkipCutscene` | Skip Cutscene | Skips a cutscene, or speeds it up while held where it cannot be skipped. May share a control with another menu action. Without this row the stick cannot skip. |

`MenuJoin` also exists, but nothing reads it from a stick, so leave it out.

### `camera`

| Action | On screen |
| --- | --- |
| `CameraForward` | Camera Forward |
| `CameraBack` | Camera Back |
| `CameraLeft` | Camera Left |
| `CameraRight` | Camera Right |
| `CameraUp` | Camera Up |
| `CameraDown` | Camera Down |
| `CameraBoost` | Camera Boost |
| `CameraSlow` | Camera Slow |
| `CameraLockTarget` | Camera Lock Target |
| `CameraLookUp` | Camera Look Up |
| `CameraLookDown` | Camera Look Down |
| `CameraLookLeft` | Camera Look Left |
| `CameraLookRight` | Camera Look Right |
| `CameraDollyOut` | Camera Dolly Out |
| `CameraDollyIn` | Camera Dolly In |

The developer reference for this format, including how a save and a reset choose their target
file, is [`org/input.md`](org/input.md), "The CSVM stick profile files".
