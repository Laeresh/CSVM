# Controls

The keys and pad buttons the game ships with, grouped by the mode that owns
them. These are **defaults**, not the definition of the keymap. Every gameplay
site reads a named action rather than a key or a pad button, and the shipped set
is data in [`CSVM/src/Bindings/DefaultBindings.cs`](../CSVM/src/Bindings/DefaultBindings.cs),
one `ActionMap` per input context: flight, menus and boards, and the free
camera. This page is the record of that table, so a row here and a line there
say the same thing.

A control can mean different things in different contexts, which is why the map
is per context rather than per player alone: `W` targets the next ally in flight,
moves a menu cursor up on a board, and flies forward in the free camera; `Space`
fires the guns in flight and confirms on a board; `Esc` pauses in flight and
closes a menu screen.

These are the defaults, not the whole keymap. The launchscreen's Options screen
carries a Controls door where each seat rebinds any action here, and the rows
below are the starting point a new player gets and the set that screen's reset
returns to. All three contexts read what that screen saved: a seat loads its own
player's file as it is built, per action, falling back to the row below for
anything the file does not carry (`Bindings/LaunchBindings.cs`). A `--det` or
`--run-tests` launch reads no file at all, so a scripted run is a function of the
committed tree and not of whoever ran it.

Not every row here is a bindable action. The debug overlays and the lab panels
(`F10` through `F19`, `C`, `H`, and the viewer and weapon-lab keys) are
development instruments and sit deliberately outside the action set, so no
rebinding screen offers them and no player can break their own diagnostics on
them. The few gameplay controls in the same position say so in their own row.

The **scripted twin** column names the CLI flag that does the same thing without
a human at the controls, for use in `--screenshot`/`--det` runs. Flags are
specified in [`cli.md`](cli.md).

## Flight (`--fly`, `--stunt`)

The keyboard column is the original's own shipped table, command by command: the
64 defaults `FUN_004936c0` emits, with the captions read off its keybind pages
(`docs/org/input.md`, `OriginalScreenshots/Keybinds *.png`). Where the original
binds a command this port has no action for, that key is simply free here; where
this port has an action the original never had, its row names the free key it
took and why. The pad column is this port's own throughout, since the original's
joystick column is ten buttons and a hat rather than a modern pad.

A modifier is part of a binding's identity, so `E` and `Shift+E` are two controls
driving two actions. A bare key stands down while a modifier the same keymap
holds that key under is down, which is how one letter carries a class's three
targeting rows. The rule is that keymap's own and not global, so the free
camera's bare keys keep working under its `Shift` boost.

⚠ **A saved keymap keeps whatever it saved.** A seat's file lists every action
and the store does not reset a profile when the shipped table changes, so this
table reaches only a player whose Controls door has never written a flight
profile. Anyone else sees their own rows, and reaches these through that screen's
reset (INSTR-64).

| Input | Pad | Does |
|---|---|---|
| `↑` · `↓` | left stick | point the nose down and up, the original's "Point Nose Down" and "Point Nose Up". `↑` is nose down, the way a stick pushed forward is |
| `←` · `→` | left stick | roll left and right |
| `,` · `.` | shoulders | rudder, the original's "Turn Left" and "Turn Right" |
| `=` · `-` | triggers | throttle up / down |
| `1`–`9` | | set the throttle to an absolute eighth, `1` for idle through `9` for full: the original's own Throttle page, which is why no other flight action may take a digit. The key names the **desired** lever and the live one traverses to it at the rate the two keys above move it, which is the original's desired/live split rather than a jump |
| `Space` | B | fire guns |
| `X` | A | fire rockets, one per pull |
| `F3` | D-pad → | cycle the guns clockwise: step the gun-group selector forward (one group fires at a time). The pad side follows the cockpit dial: the **GUNS** gauge is in the right column, **ROCKETS** in the left (`docs/formats/hud.md`, "Weapon gauges") |
| `F5` | D-pad ← | cycle the rockets clockwise: step the hardpoint selector one mount along the belt, skipping every pylon that is spent |
| `F4` · `F6` | D-pad → / ← **held** | cycle the guns and the rockets counterclockwise, over the same order and skipping the same empties, so a press each way returns to the slot you started on. These four are the original's own keys, by the name its keybind page displays (`OriginalScreenshots/Keybinds Weapons.png`: `F3`/`F4` guns, `F5`/`F6` rockets; the names are inverted against the message keys behind them, `docs/org/input.md`). The pad has one button per class rather than two, as the original's joystick column does, so holding it past 250 ms steps that class the other way, one step per hold, and the tap resolves on release so a spent hold never also steps forward. A keyboard hold does nothing: each direction has its own key |
| `N` | X | nitro boost (the original's "Use Nitro-Booster"): engages only with a nitrous engine fitted and the tank at 99 % or more, then burns the whole tank (9.5 s) with no way to stop it, and re-arms after a 28 s refill. The nitro dial appears at the bottom of the right column, below the speedometer, with the injector fitted (`docs/org/flightModel.md`, "Nitro") |
| `F19` | | damage lab on the flown plane, `--damage=` |
| `B` | | weapon lab panel, `--weapon-lab` sessions only; its steppers arm the plane's live loadout and Space/`X` then fire it (`--weapon-lab=` picks the weapon, `--weapon-mount=` the mount, `--weapon-cycle=` steps the list) |
| click | | weapon lab: park the held plane on the surface under the cursor, at the panel's stand-off, nose on it, `--weapon-click=x,y`, or `--weapon-target=x,y,z` / `--weapon-surface=<registry name>` (any of the fourteen surface ids; `dirt` means id 13, not "untagged") to place without a mouse at all |
| shift-click | | weapon lab: aim at that point without moving the plane, `--weapon-click=x,y,aim` |
| stand-off slider | | weapon lab: how far back every placement parks (15–1100 m, default 90), `--weapon-standoff=` |
| `V` | | weapon lab: hand the view to a free camera and back, fly out and watch an impact from a metre away, then `V` returns the orbit where you left it. `--weapon-camera=free\|<frames>` |
| `WASD` / numpad `+`/`−` | | weapon lab: swing and zoom the orbit around the held plane (numpad `+` in, `−` out; it reads no stick input while held). With `V` out, the freecam's own controls apply instead |
| `Backspace` | Y | respawn · restart while a results board is up (the direct route to that board's Restart item). A campaign mission and Instant Action read it only from a crash, where it skips the crash camera; a respawn while still flying would repair, restock and refuel for free. Free flight, the stunt runs and the dogfight take it any time, where it means "put me back at the spawn". This port's own action: the original has no respawn, and `Backspace` is a key it binds nothing to and one the flying hand does not brush, which matters for a control that throws the airframe away. Its own Ctrl+`X` bail-out stays free for the bail-out this port does not have |
| `A` | left stick click | auto-land: only does something inside a story mission's `auto` approach sphere, where it starts the same hookup animation the manual approach cone would. The original's own key for it ("Auto-Dock", `OriginalScreenshots\Keybinds Other.png`). The HUD prompt is the original's own wording with whichever of these two controls the seat can use in it, so a pad-only splitscreen player is named the stick click and never the key |
| `E` · `Shift+E` · `Ctrl+E` | D-pad ↑ | the enemy/objective cycle: step it forward, step it **back** (wrapping off the head onto the tail), and restart it at its head from wherever the pilot had walked to. "Head" is the head of the decoded sector order (objectives, then ahead, behind, left, right), not the nearest thing in space. Forward reaches whoever shot you first, and objectives ride this cycle ahead of everything else, so in a stunt run it is also what steps the Danger Zone marker: a zone is an objective and has no key of its own. A hostile boat, ship or truck is on this cycle too, not the non-aircraft one: that class holds what a mission flagged, not what fails to fly (`docs/org/targeting.md`). The original lays its whole targeting scheme across one run of the top row, `Q W E R T`, with Shift stepping a cycle back and Ctrl restarting it (`OriginalScreenshots/Keybinds Targeting.png`, decoded in `docs/org/input.md`). D-pad up is the pad's one targeting binding: a tap steps this cycle and a hold selects the nearest, split by hold length inside the consumer rather than by two bindings. The original gives this action joystick button 3, one of only two targeting actions it puts on a pad at all |
| `W` · `Shift+W` · `Ctrl+W` | | the ally cycle, the same three directions over the friendly aircraft |
| `R` · `Shift+R` · `Ctrl+R` | | the non-aircraft cycle (turret emplacements, zeppelin sub-parts), the same three. The original gives this class's forward step its other pad targeting binding, joystick button 6 |
| `Q` | | target whatever is nearest the crosshair, a hard 15° cone about the **nose**, 2 km max, friend or foe. The pad reaches it by holding d-pad up past 250 ms, which is the target-cycle binding above dispatched by hold length, so this action carries no pad default of its own |
| `T` | | target nothing, clears the selection, and it **stays** cleared until one of the keys above |
| `F8` | D-pad ↓ | cycle the views: Cockpit → Nose → Chase → Cockpit, the original's own three-stop walk confirmed at its controls, `--view=cockpit` / `--view=nose` / `--view=chase`. The original's binding for "Cycle Cockpit Views" (`OriginalScreenshots/Keybinds Views 1.png`, which also gives it a joystick button); the D-pad slot is this port's pick of the free buttons, and gives a pad-only seat its way in |
| `F2` | Back/Select | select the chase view directly, without walking the cycle, `--view=chase` (the default). This port's own action, and `F2` is the one gap in the original's function-key run: `F1` is its View Help (a screen this port does not have), `F3` through `F6` its weapon selectors, `F7` the flyby below and `F8` the view cycle above. Back is this port's pick of the free pad buttons |
| `F7` | | the flyby camera, `--view=flyby`. The view leaves the aeroplane and takes a spot ahead of it and off to one side, holds that spot while the aircraft runs past within a few metres, then re-sites itself a few seconds later. The original's own binding, labelled "Access Chase View" (`OriginalScreenshots/Keybinds Views 1.png`), which its own code settles as camera mode 9 rather than the following chase view. `F8` or `F6` leaves it; so does a respawn. Placement, radii and timing all come from `camparam.json` (`docs/formats/camparam.md`) |
| `Shift+S` | Misc1 | arm or disarm the spyglass, on at level load. Armed, a selected target that is off screen and inside the fog-derived range gate is shown in a round inset picture at its edge marker, framed to a constant apparent size. The original's own chord ("Toggle Spyglass", `OriginalScreenshots/Keybinds Views 1.png`, which also gives it joystick button 5); Misc1 is the one pad control no other flight action holds |
| `numpad 1–9` (not `5`) | | head-look snap, in **every** view: hold a direction and the head swings there, release and it returns straight ahead. **The original's own bindings** (`OriginalScreenshots/Keybinds Views 2.png`): `Kp8` Look Up, `Kp4`/`Kp6` Look Left/Right, `Kp2` Look Back, `Kp7`/`Kp9` Look Up/Left and Up/Right, `Kp1`/`Kp3` Look Up/Left/Rear and Up/Right/Rear. So dead ahead looks straight **up**, every diagonal 45° up, and the flanks and astern look level. In Cockpit or Nose the head aims; in the chase view the same head swings the camera around the aeroplane, so looking left puts the camera on the aircraft's starboard side and `Kp8` gives the belly plan view. One head, so a bearing taken in the cockpit is the bearing the chase view shows, and several keys down compose as one direction rather than the lowest digit winning |
| `numpad 5` | | recenter the head, in every view, the original's `Kp5` "Look Forward" (same screenshot), the middle of the cluster its eight directions surround |
| RMB-held mouse | | free-look, in every view: the head pans at the decoded 2 rad/s in whichever direction the mouse moves, stopping hard at dead astern (±180°, the original's own stop) and at straight up. Elevation floors at level in Cockpit or Nose and a further quarter turn down, at straight down, in the chase view, each the floor the original's own placement passes the controller. Direction only, so a slow movement pans as fast as a quick one, the original's input is a hat switch. RMB-held matches the freecam's look posture; the mouse does nothing in flight otherwise. The pad does **not** ride this path: a stick has an absolute position to map and a mouse has none, so the stick aims absolutely (the right-stick row below) while the mouse keeps the original's relative law. The original reaches the two look modes through key selectors instead (`K` Access Snap Look Mode, `J` Access Smooth Look Mode) rather than by which device moved, which is a filed item |
| `numpad 0` | | hold the look-behind: in an external view the back camera (ahead of the nose looking back, at the authored `back_dist` range, `--view=back`); in Cockpit or Nose a head look-back, the head snapping to dead astern while held and returning on release, as the original does in both cockpit views |
| `numpad +`/`−`, **outside first person** | | chase-camera zoom: hold `−` to back off, `+` to come back in. The view rests at its NEAR end, so the only travel available is outward, up to the decoded flat 10 m; the axis moves at the decoded 2/s and eases at 1.5/s, clamped `[0, 1]` (the original's External Camera Zoom In/Out, `OriginalScreenshots/Keybinds Views 2.png`, decode in `docs/org/cameraViews.md`). Shared by the chase camera, a snapped or panned chase view and the pad look-around, which read the same radius; the look-behind takes its own bounds and no zoom. Also bound in the weapon lab (above), where the same two keys drive the free orbit's dolly instead. Both readings poll the two keys directly rather than through a named action, so this pair is outside the shipped default table and is not rebindable |
| | right stick | aim the view, absolutely, in every view: stick position is view position, over one shared envelope of ±150° round and ±60° up and down, returning to the settled pose as the stick centres. Outside first person it swings the camera around the plane; in Cockpit or Nose it aims the head, and it is the only input that aims the head below level there (the original's relative controls floor at level in first person, which would leave the bottom half of an absolute stick inert). Not in the original, a UX call for this port (`BL-372`); the scripted twin is `--look=x,y`. A pad reaches neither the straight-up of `numpad 8` nor the dead astern of the stick click: ±60° is the chase camera's gimbal margin, and widening it would gimbal that camera |
| | click right stick | hold to look back, the pad twin of `numpad 0` (`BL-372`), with the same split: external back camera outside, in-cockpit head look-back in the first-person views |
| `C` | | show the built colliders, coloured by the surface id they resolve to (see `--collision`), `--debug-colliders` |
| `H` | | colour world objects by class (destructible/facade/clutter/scenery), `--debug-classoverlay`. Moved off `X`, which is the original's Fire Rockets, so a tint no longer toggles under every rocket shot. It stays a letter rather than joining the `F13` upward block because that block's numbers follow physical keys on the maintainer's own keypad |
| `Esc` | Start | pause, the original's "Pause/Quit/Objectives". The board it opens is in the "Any mode" table below |
| `L` | | **reserved** for Track Target (the original's `Views 1 → Track Target`), bound to nothing yet; the camera behaviour is its own item, `BL-399` |
| - | | the original's commands this port has no action for leave their keys free: Shift+`L` level off, Ctrl+`X` bail out, `F9` the first of its four external cameras, and `K` / `J`, which select its snap and smooth look modes (this port picks the look mode by which device moved, `BL-399` and the free-look row above). The other three external-camera keys, `F10` through `F12`, are the one place this port sits on an original command's key: they carry the gltf export, the placement print and the screenshot, which are instruments rather than flight actions and are not in the shipped table a Controls door can rebind |

## Any mode

| Input | Pad | Does |
|---|---|---|
| `Esc` | Start | pause, halts the sim and opens the pause board's menu (Resume · Photo Mode · Restart · Exit); `.` steps one frame. Splitscreen: any player's press pauses everyone, and the board names who paused (`BL-373`), only that player resumes it and only that player drives the cursor |
| ↑↓ · `W`/`S` | d-pad ↑↓ · left stick | move the board menu's cursor. The stick counts as a press past half its travel |
| ←→ · `A`/`D` | d-pad ←→ · left stick | step the value under the cursor, on the rows that carry one |
| `Enter` · numpad `Enter` · `Space` | A | confirm the highlighted item |
| `Esc` | B | close the pause menu, which Start also does from inside it. A results board's menu has no way back and reads none |
| `L` | Y | on a menu screen, open the loadout for whatever the screen is about (a wingman's fit, a locked player seat's) |
| `P` | X | on a menu screen, open its contents list, which is the Instant Action presets today |
| | Start | on a menu screen, claim a seat with a pad no seat owns yet. Which pad pressed it is a raw device read rather than an action, because a seat's bindings answer for every pad it holds at once and cannot say which one moved. The join gesture has no default of its own for the same reason |
| mouse · LMB | | on an Original menu screen, the pointer focuses whatever row it stands on and a click confirms it. Built-in's own pointer is the `mouse` row further down this table |
| mouse wheel · thumb drag | | on an Original menu screen, a wheel step over a list moves that list's window by one row, and its scrollbar thumb can be dragged down the track, the window following in proportion. Every list takes both: the scrapbook's contents page, an open drop-down on a campaign board or in Plane Construction, Instant Action's dropdowns and its contents window, and the aircraft column on Free Flight and Dogfight. The wheel is a remake comfort on top of the decoded screens; the lists' own arrows are unchanged, and a list that fits its window ignores the wheel |
| - | | a results board (mission wrap-up, dogfight, race, stunt run) halts the sim and carries its own Photo Mode · Restart · Exit menu, driven by player 1. The pause key does nothing while one is up. Photo Mode leads because the resting row must be the harmless one, and on a results board Restart throws away the run just finished |
| - | | **while any board is up the camera holds still.** It keeps the pose it had when the board appeared, so moving the menu cursor no longer swings the view (`BL-429`); the free look is the Photo Mode row |
| Photo Mode row | | hands that player's pane to the `--freecam` controls over the frozen world, hides the board and the whole pilot HUD, and starts locked onto your own aircraft so entering never jumps. The halt is never dropped, so it stays a still frame with the audio paused. `Esc` (pad `B`) brings the board back, leaving the camera where you flew it. Splitscreen: only the pausing player's pane, the other panes stay frozen |
| - | | a pilot out of lives watches from the `--freecam` controls (WASD/QE move, RMB look, `F`/pad `X` to lock onto an aircraft) on its own pane. Splitscreen: each downed pilot's spectator reads only its own pad/keyboard (`BL-375`), two players watching at once move independently, not lockstep. Mouse look stays shared (one physical mouse) |
| any key · any button | any button | during a cutscene: skip it where the mission's own definition arms one (the intros, callback 20), which ends it the way the original's force-stop does. `Esc` is exempt, being the way out of the session. Splitscreen names who skipped |
| any key held · any button held | any button held | during a cutscene the mission arms no skip on (every mid-mission scene), holding that same input fast-forwards the picture and its sound to 4x over a quarter-second ramp instead, and releasing it spools back down. A remake-only rule with nothing in the original behind it: every authored beat still plays, in order, only sooner (`docs/formats/anim-definitions/cutscenes.md`) |
| `.` | | step one frame while paused |
| `F12` | | screenshot, from any screen: in flight, in the viewer, on the launchscreen and on the campaign boards. Every shot lands in the repo's git-ignored `Screenshots/` folder, so one folder collects them however they were taken. The menu screens take the key themselves rather than leaving it to the launcher (`LaunchMenu._UnhandledInput`), which is what makes a menu defect showable rather than only describable (`BL-489`) |
| `F13` | | AI patrol-net overlay (chapter worlds only), `--debug-ainets`. First tenant of the F13–F24 range reserved for debug overlays and lab panels; the letter-key overlays (`C`/`H`/…) are to migrate there. A gameplay action may not take a key in this range, and an instrument that has to leave a lower function key moves into it (the damage lab on `F19` left `F5` to the original's rocket selector) |
| `F14` | | frame-cost readout: fps / current frame cost / worst recent frame, cycling Off → Compact → Full, `--debug-fps=`. Works at the launchscreen too |
| `F15` | | targeting overlay, `--debug-targets`. A line from every turret gunner and AI gunner to the target it has acquired: red firing, amber tracking, grey held; the HUD names the gate holding each one (blocked / slewing / shot clock / bored / no solution) |
| `F16` | | all-aircraft markers on the targeting HUD, `--debug-markers`. Every live aircraft at once instead of the one selected target, each carrying its identity, slant range, health, armour and AI mode, red for a hostile team and blue for your own. Flight only, and it toggles every splitscreen pane at once. The node-name labels have no key and stay on `--debug-names`, being a viewer instrument rather than a flight one |
| `F17` | | kill the currently selected target through its own death path (an aircraft crashes, a zeppelin sub-part is destroyed), the playtester's escape hatch for a stray enemy blocking an objective chain. No CLI flag. Inert with nothing selected; also inert on a turret selection, which carries no `HEALTH` key in the decoded data (`BL-534`) |
| `F11` | | print the mode's subject placement as ready-to-paste `--pos=` / `--direction=` (in `--viewer`: `--pos=` / `--lookat=`, the orbit pivot) |
| `F10` | | export the plane on screen (current livery + damage) to a timestamped `.glb` under `Exports/`, the `--export-gltf=` twin |
| `Esc` | | at the launchscreen: back, and quit from the Mode screen. In flight it opens the pause board instead, a board menu's Exit item is what leaves a session, so a pad can reach it too. On the Controls screen `Esc` (or the pad's B) abandons an armed capture rather than being bound, in both presentations: the original writes Escape into the cell, and keeping a way out that needs no controller is a deliberate departure |
| `Esc` · `Space` · `Return` · LMB | any button | end a cinema early. The sets are the original's own: the chapter cinema takes all four, the closing cinema `Esc` or a click alone, and the boot sequence's films and stills take any press at all. A pad button is in every set, because a player holding one has no key to offer |
| mouse | | at the launchscreen (Built-in): the pointer over a row moves the cursor onto it, a press and release on the same row confirms it (letting go elsewhere confirms nothing), the wheel steps the cursor over a list, and the right button goes back a screen on its press. The Mode screen is the exception: Back there is the quit, which stays on `Esc` so a stray click cannot take it. Player 1's device, so on a split aircraft screen only player 1's pane takes it; the campaign boards under Built-in stay on the keys. Original's own pointer is described in `docs/menu-presentations.md` |

## `--viewer`

| Input | Does | Scripted twin |
|---|---|---|
| `F19` | damage lab | `--damage=` |
| `L` | livery lab | |
| `M` | mesh lab | `--debug-mesh=` |
| `K` | marker overlay | |

## `--freecam`, `--anim-lab`

| Input | Does | Scripted twin |
|---|---|---|
| `WASD` / arrows · left stick | move | |
| `Q` / `E` (alt `Z` / `U`) · shoulders | descend / ascend | |
| `Shift` · RT | move faster while held. The trigger counts from half its travel, where the dolly below reads the whole of it | |
| `Ctrl` · LT | move slower while held, on the same half-travel gate | |
| RMB-held mouse (alt `IJKL`) · right stick | look. The mouse path is an event handler rather than a bound control, so it is the one look input the default table does not carry | |
| `F` · pad `X` | lock onto the nearest aircraft and orbit it; press again to step outward, wrapping past the farthest back to the nearest. The only way back into a lock once you have flown off one (`BL-428`). Inert where the session offers no aircraft (the static viewer, an empty stage) | |
| RMB-drag · right stick | while locked: swing the orbit. Any translation (WASD/QE, left stick) releases the lock and flies off instead | |
| wheel · triggers | while locked: dolly the orbit (RT out, LT in). Unlocked, the wheel sets the fly speed | |
| click an object | select it | `--debug-select=` |
| `PgUp` / `PgDn` | walk the selection's `cs_name` ancestor ladder | |
| `Home` / `End` | jump to the ends of that ladder | |
| `N` | node lab | `--debug-nodelab=` |
| `M` | mesh lab, on that selection alone | `--debug-mesh=` |
| `C` | show the built colliders, coloured by the surface id they resolve to (see `--collision`), also bound in `--fly`/`--stunt` | `--debug-colliders` |
| `H` | colour world objects by class (destructible/facade/clutter/scenery), also bound in `--fly`/`--stunt` | `--debug-classoverlay` |
| `F19` | damage lab on the selected destructible | `--debug-damage=` |
| `F18` | anim lab: the def picker. Moved off `F`, which the camera's lock key now owns, the camera polls raw key state, so one key could not serve both (`BL-428`) | |
