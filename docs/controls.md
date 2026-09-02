# Controls

The keys and pad buttons the game ships with, grouped by the mode that owns
them. These are **defaults**, not the definition of the keymap. Every gameplay
site reads a named action rather than a key or a pad button, and the shipped set
is data in [`CSVM/src/Bindings/DefaultBindings.cs`](../CSVM/src/Bindings/DefaultBindings.cs),
one `ActionMap` per input context: flight, menus and boards, and the free
camera. This page is the record of that table, so a row here and a line there
say the same thing.

A control can mean different things in different contexts, which is why the map
is per context rather than per player alone: `W` pitches down in flight, moves a
menu cursor up on a board, and flies forward in the free camera; `P` pauses in
flight and opens the presets list on a menu screen; `Space` fires the guns in
flight and confirms on a board.

Per-player rebinding of these actions is being built. Until it ships the
defaults below are the whole keymap; once it does, they are the starting point a
new player gets and the set a reset returns to.

Not every row here is a bindable action. The debug overlays and the lab panels
(`F5`, `F10` through `F18`, `C`, `X`, and the viewer and weapon-lab keys) are
development instruments and sit deliberately outside the action set, so no
rebinding screen offers them and no player can break their own diagnostics on
them. The few gameplay controls in the same position say so in their own row.

The **scripted twin** column names the CLI flag that does the same thing without
a human at the controls, for use in `--screenshot`/`--det` runs. Flags are
specified in [`cli.md`](cli.md).

## Flight (`--fly`, `--stunt`)

| Input | Pad | Does |
|---|---|---|
| `WASD` / arrows | left stick | pitch + roll |
| `Q` / `E` | shoulders | rudder |
| `Shift` / `Ctrl` | triggers | throttle up / down |
| `Space` | B | fire guns |
| `F` | A | fire rockets — one per pull |
| `G` | D-pad → | select gun group (one at a time). The pad side follows the cockpit dial: the **GUNS** gauge is in the right column, **ROCKETS** in the left (`docs/formats/hud.md`, "Weapon gauges") |
| `H` | D-pad ← | select ordnance (steps forward only; the original also steps back, `BL-357`) |
| `N` | X | nitro boost (the original's "Use Nitro-Booster"): engages only with a nitrous engine fitted and the tank at 99 % or more, then burns the whole tank (9.5 s) with no way to stop it, and re-arms after a 28 s refill. The nitro dial appears at the bottom of the right column, below the speedometer, with the injector fitted (`docs/org/flightModel.md`, "Nitro") |
| `F5` | | damage lab on the flown plane — `--damage=` |
| `B` | | weapon lab panel — `--weapon-lab` sessions only; its steppers arm the plane's live loadout and Space/`F` then fire it (`--weapon-lab=` picks the weapon, `--weapon-mount=` the mount, `--weapon-cycle=` steps the list) |
| click | | weapon lab: park the held plane on the surface under the cursor, at the panel's stand-off, nose on it — `--weapon-click=x,y`, or `--weapon-target=x,y,z` / `--weapon-surface=<registry name>` (any of the fourteen surface ids; `dirt` means id 13, not "untagged") to place without a mouse at all |
| shift-click | | weapon lab: aim at that point without moving the plane — `--weapon-click=x,y,aim` |
| stand-off slider | | weapon lab: how far back every placement parks (15–1100 m, default 90) — `--weapon-standoff=` |
| `V` | | weapon lab: hand the view to a free camera and back — fly out and watch an impact from a metre away, then `V` returns the orbit where you left it. `--weapon-camera=free\|<frames>` |
| `WASD` / numpad `+`/`−` | | weapon lab: swing and zoom the orbit around the held plane (numpad `+` in, `−` out; it reads no stick input while held). With `V` out, the freecam's own controls apply instead |
| `R` | Y | respawn · restart while a results board is up (the direct route to that board's Restart item) |
| `F9` | left stick click | auto-land: only does something inside a story mission's `auto` approach sphere, where it starts the same hookup animation the manual approach cone would. The original binds this to `A` (`OriginalScreenshots\Keybinds Other.png`, "Auto-Dock"), which this port's WASD scheme already spends on roll left, so it gets its own free slot instead |
| `T` | D-pad ↑ | target the next enemy/objective — steps the cycle, and reaches whoever shot you first. D-pad up is the pad's one targeting binding: a tap steps this cycle and a hold selects the nearest, split by hold length inside the consumer rather than by two bindings |
| `Y` | | target the next ally |
| `U` | | target the next non-aircraft (turret emplacements, zeppelin sub-parts) |
| `I` | | target whatever is nearest the crosshair — a hard 15° cone about the **nose**, 2 km max, friend or foe. The pad reaches it by holding d-pad up past 250 ms, which is the target-cycle binding above dispatched by hold length, so this action carries no pad default of its own |
| `O` | | target nothing — clears the selection, and it **stays** cleared until one of the keys above |
| `Tab` | D-pad ↑ | cycle stunt target. The pad slot is shared with the target cycle above on purpose: a stunt target is an objective marker, so cycling one is the objective cycle, and without it a pad-only stunt pilot cannot step the marker at all (`BL-686` carries folding the two together) |
| `F8` | D-pad ↓ | cycle the views: Cockpit → Nose → Chase → Cockpit, the original's own three-stop walk confirmed at its controls — `--view=cockpit` / `--view=nose` / `--view=chase`. The original's binding for "Cycle Cockpit Views" (`OriginalScreenshots/Keybinds Views 1.png`, which also gives it a joystick button); the D-pad slot is this port's pick of the free buttons, and gives a pad-only seat its way in |
| `F6` | Back/Select | select the chase view directly, without walking the cycle — `--view=chase` (the default). The original binds "Access Chase View" to `F7` (same screenshot); `F6` and Back are this port's choices because `F7` is recorded in `docs/org/cameraViews.md` as the flyby camera, which is a contradiction nobody has settled yet |
| `numpad 1–9` (not `5`), **in Cockpit or Nose** | | head-look snap: hold a direction and the head swings there, release and it returns straight ahead. **The original's own bindings** (`OriginalScreenshots/Keybinds Views 2.png`): `Kp8` Look Up, `Kp4`/`Kp6` Look Left/Right, `Kp2` Look Back, `Kp7`/`Kp9` Look Up/Left and Up/Right, `Kp1`/`Kp3` Look Up/Left/Rear and Up/Right/Rear. So dead ahead looks straight **up**, every diagonal 45° up, and the flanks and astern look level. While a first-person view is selected these keys hold no fixed view — the numpad is the look cluster there, as it is in the original |
| `numpad 5`, **in Cockpit or Nose** | | recenter the head — the original's `Kp5` "Look Forward" (same screenshot), the middle of the cluster its eight directions surround |
| RMB-held mouse | | first-person free-look: the head pans at the decoded 2 rad/s in whichever direction the mouse moves, stopping hard at dead astern (±180°, the original's own stop) and at level/straight-up in elevation. Direction only, so a slow movement pans as fast as a quick one — the original's input is a hat switch. RMB-held matches the freecam's look posture; the mouse does nothing in flight otherwise. The pad does **not** ride this path: a stick has an absolute position to map and a mouse has none, so the stick aims absolutely (the right-stick row below) while the mouse keeps the original's relative law. The original reaches the two look modes through key selectors instead (`K` Access Snap Look Mode, `J` Access Smooth Look Mode) rather than by which device moved, which is a filed item |
| `numpad 1–9` (not `5`), **outside first person** | | hold a fixed camera view around the plane (P1's keyboard) — `--view=` |
| `numpad 0` | | hold the look-behind: in an external view the back camera (ahead of the nose looking back, at the authored `back_dist` range — `--view=back`); in Cockpit or Nose a head look-back, the head snapping to dead astern while held and returning on release, as the original does in both cockpit views |
| `numpad +`/`−`, **outside first person** | | chase-camera zoom: hold `+` to close in, `−` to back off. Moves at the decoded 2/s and eases at 1.5/s, trimming up to the plane's own authored base distance as the value clamps to `[0, 1]` (`BL-433`, the original's External Camera Zoom In/Out, `OriginalScreenshots/Keybinds Views 2.png`). Shared by chase, the fixed views and the look-behind, since they all read the same dynamic radius. Also bound in the weapon lab (above), where the same two keys drive the free orbit's dolly instead. Both readings poll the two keys directly rather than through a named action, so this pair is outside the shipped default table and is not rebindable |
| | right stick | aim the view, absolutely, in every view: stick position is view position, over one shared envelope of ±150° round and ±60° up and down, returning to the settled pose as the stick centres. Outside first person it swings the camera around the plane; in Cockpit or Nose it aims the head, and it is the only input that aims the head below level (the original's relative controls floor there at level, which would leave the bottom half of an absolute stick inert). Not in the original, a UX call for this port (`BL-372`); the scripted twin is `--look=x,y`. A pad reaches neither the straight-up of `numpad 8` nor the dead astern of the stick click: ±60° is the chase camera's gimbal margin, and widening it would gimbal that camera |
| | click right stick | hold to look back — the pad twin of `numpad 0` (`BL-372`), with the same split: external back camera outside, in-cockpit head look-back in the first-person views |
| `C` | | show the built colliders, coloured by the surface id they resolve to (see `--collision`) — `--debug-colliders` |
| `X` | | colour world objects by class (destructible/facade/clutter/scenery) — `--debug-classoverlay` |
| `L` | | **reserved** for Track Target (the original's `Views 1 → Track Target`) — bound to nothing yet; the camera behaviour is its own item, `BL-399` |

## Any mode

| Input | Pad | Does |
|---|---|---|
| `P` · `Esc` | Start | pause — halts the sim and opens the pause board's menu (Resume · Photo Mode · Restart · Exit); `.` steps one frame. Splitscreen: any player's press pauses everyone, and the board names who paused (`BL-373`) — only that player resumes it and only that player drives the cursor |
| ↑↓ · `W`/`S` | d-pad ↑↓ · left stick | move the board menu's cursor. The stick counts as a press past half its travel |
| ←→ · `A`/`D` | d-pad ←→ · left stick | step the value under the cursor, on the rows that carry one |
| `Enter` · numpad `Enter` · `Space` | A | confirm the highlighted item |
| `Esc` | B | close the pause menu, which `P` and Start also do from inside it. A results board's menu has no way back and reads none |
| `L` | Y | on a menu screen, open the loadout for whatever the screen is about (a wingman's fit, a locked player seat's) |
| `P` | X | on a menu screen, open its contents list, which is the Instant Action presets today |
| | Start | on a menu screen, claim a seat with a pad no seat owns yet. Which pad pressed it is a raw device read rather than an action, because a seat's bindings answer for every pad it holds at once and cannot say which one moved. The join gesture has no default of its own for the same reason |
| — | | a results board (mission wrap-up, dogfight, race, stunt run) halts the sim and carries its own Photo Mode · Restart · Exit menu, driven by player 1. The pause key does nothing while one is up. Photo Mode leads because the resting row must be the harmless one, and on a results board Restart throws away the run just finished |
| — | | **while any board is up the camera holds still.** It keeps the pose it had when the board appeared, so moving the menu cursor no longer swings the view (`BL-429`); the free look is the Photo Mode row |
| Photo Mode row | | hands that player's pane to the `--freecam` controls over the frozen world, hides the board and the whole pilot HUD, and starts locked onto your own aircraft so entering never jumps. The halt is never dropped, so it stays a still frame with the audio paused. `Esc` (pad `B`) brings the board back, leaving the camera where you flew it. Splitscreen: only the pausing player's pane, the other panes stay frozen |
| — | | a pilot out of lives watches from the `--freecam` controls (WASD/QE move, RMB look, `F`/pad `X` to lock onto an aircraft) on its own pane. Splitscreen: each downed pilot's spectator reads only its own pad/keyboard (`BL-375`) — two players watching at once move independently, not lockstep. Mouse look stays shared (one physical mouse) |
| `.` | | step one frame while paused |
| `F12` | | screenshot, from any screen: in flight, in the viewer, on the launchscreen and on the campaign boards. Every shot lands in the repo's git-ignored `Screenshots/` folder, so one folder collects them however they were taken. The menu screens take the key themselves rather than leaving it to the launcher (`LaunchMenu._UnhandledInput`), which is what makes a menu defect showable rather than only describable (`BL-489`) |
| `F13` | | AI patrol-net overlay (chapter worlds only) — `--debug-ainets`. First tenant of the F13–F24 range reserved for debug overlays; the letter-key overlays (`C`/`X`/…) are to migrate there |
| `F14` | | frame-cost readout: fps / current frame cost / worst recent frame, cycling Off → Compact → Full — `--debug-fps=`. Works at the launchscreen too |
| `F15` | | targeting overlay — `--debug-targets`. A line from every turret gunner and AI gunner to the target it has acquired: red firing, amber tracking, grey held; the HUD names the gate holding each one (blocked / slewing / shot clock / bored / no solution) |
| `F16` | | node-name labels — `--debug-names[=meshes\|all]`. Migrated off `T`, which is free for targeting |
| `F17` | | kill the currently selected target through its own death path (an aircraft crashes, a zeppelin sub-part is destroyed) — the playtester's escape hatch for a stray enemy blocking an objective chain. No CLI flag. Inert with nothing selected; also inert on a turret selection, which carries no `HEALTH` key in the decoded data (`BL-534`) |
| `F11` | | print the mode's subject placement as ready-to-paste `--pos=` / `--direction=` (in `--viewer`: `--pos=` / `--lookat=`, the orbit pivot) |
| `F10` | | export the plane on screen (current livery + damage) to a timestamped `.glb` under `Exports/` — the `--export-gltf=` twin |
| `Esc` | | at the launchscreen: back, and quit from the Mode screen. In flight it opens the pause board instead — a board menu's Exit item is what leaves a session, so a pad can reach it too |

## `--viewer`

| Input | Does | Scripted twin |
|---|---|---|
| `F5` | damage lab | `--damage=` |
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
| `C` | show the built colliders, coloured by the surface id they resolve to (see `--collision`) — also bound in `--fly`/`--stunt` | `--debug-colliders` |
| `X` | colour world objects by class (destructible/facade/clutter/scenery) — also bound in `--fly`/`--stunt` | `--debug-classoverlay` |
| `F5` | damage lab on the selected destructible | `--debug-damage=` |
| `F18` | anim lab: the def picker. Moved off `F`, which the camera's lock key now owns — the camera polls raw key state, so one key could not serve both (`BL-428`) | |
