# Controls

The keys and pad buttons bound during development, grouped by the mode that
owns them. The **scripted twin** column names the CLI flag that does the same
thing without a human at the controls — use it in `--screenshot`/`--det` runs.
Flags are specified in [`cli.md`](cli.md).

## Flight (`--fly`, `--stunt`)

| Input | Pad | Does |
|---|---|---|
| `WASD` / arrows | left stick | pitch + roll |
| `Q` / `E` | shoulders | rudder |
| `Shift` / `Ctrl` | triggers | throttle up / down |
| `Space` | B | fire guns |
| `F` | A | fire rockets — one per pull |
| `G` | D-pad ← | select gun group (one at a time) |
| `H` | D-pad → | select ordnance (steps forward only; the original also steps back, `BL-357`) |
| `F5` | | damage lab on the flown plane — `--damage=` |
| `B` | | weapon lab panel — `--weapon-lab` sessions only; its steppers arm the plane's live loadout and Space/`F` then fire it (`--weapon-lab=` picks the weapon, `--weapon-mount=` the mount, `--weapon-cycle=` steps the list) |
| click | | weapon lab: park the held plane on the surface under the cursor, at the panel's stand-off, nose on it — `--weapon-click=x,y`, or `--weapon-target=x,y,z` / `--weapon-surface=<registry name>` (any of the fourteen surface ids; `dirt` means id 13, not "untagged") to place without a mouse at all |
| shift-click | | weapon lab: aim at that point without moving the plane — `--weapon-click=x,y,aim` |
| stand-off slider | | weapon lab: how far back every placement parks (15–1100 m, default 90) — `--weapon-standoff=` |
| `V` | | weapon lab: hand the view to a free camera and back — fly out and watch an impact from a metre away, then `V` returns the orbit where you left it. `--weapon-camera=free\|<frames>` |
| `WASD` / numpad `+`/`−` | | weapon lab: swing and zoom the orbit around the held plane (numpad `+` in, `−` out; it reads no stick input while held). With `V` out, the freecam's own controls apply instead |
| `R` | Y | respawn · restart while a results board is up (the direct route to that board's Restart item) |
| `T` | D-pad ↑ (tap) | target the next enemy/objective — steps the cycle, and reaches whoever shot you first |
| `Y` | | target the next ally |
| `U` | | target the next non-aircraft (turret emplacements, zeppelin sub-parts) |
| `I` | D-pad ↑ (hold 250 ms) | target whatever is nearest the crosshair — a hard 15° cone about the **nose**, 2 km max, friend or foe. Holding past the threshold fires this and the release does nothing |
| `O` | | target nothing — clears the selection, and it **stays** cleared until one of the keys above |
| `Tab` | | cycle stunt target |
| `numpad 1–9` (not `5`) | | hold a fixed camera view around the plane (P1's keyboard) — `--view=` |
| `numpad 0` | | hold the look-behind view: ahead of the nose looking back, at the authored `back_dist` range — `--view=back` |
| | right stick | swing the external view around the plane while deflected, snapping back to the ordinary chase view the instant the stick returns to centre (`BL-372`) — not in the original, a UX call for this port |
| | click right stick | hold to look back — the pad twin of `numpad 0` (`BL-372`) |
| `C` | | show the built colliders, coloured by the surface id they resolve to (see `--collision`) — `--debug-colliders` |
| `X` | | colour world objects by class (destructible/facade/clutter/scenery) — `--debug-classoverlay` |
| `L` | | **reserved** for Track Target (the original's `Views 1 → Track Target`) — bound to nothing yet; the camera behaviour is its own item, `BL-399` |

## Any mode

| Input | Does |
|---|---|
| `P` · `Esc` · Start | pause — halts the sim and opens the pause board's menu (Resume · Restart · Exit); `.` steps one frame. Splitscreen: any player's press pauses everyone, and the board names who paused (`BL-373`) — only that player resumes it and only that player drives the cursor |
| ↑↓ / d-pad / left stick | move the board menu's cursor |
| `Enter` · `Space` | A | confirm the highlighted item |
| — | B | close the pause menu — the pad twin of `Esc`/`P`/Start. A results board's menu has no way back and reads none |
| — | a results board (mission wrap-up, dogfight, race, stunt run) halts the sim and carries its own Restart · Exit menu, driven by player 1. The pause key does nothing while one is up |
| — | a pilot out of lives watches from the `--freecam` controls (WASD/QE move, RMB look) on its own pane. Splitscreen: each downed pilot's spectator reads only its own pad/keyboard (`BL-375`) — two players watching at once move independently, not lockstep. Mouse look stays shared (one physical mouse) |
| `.` | step one frame while paused |
| `F12` | screenshot |
| `F13` | AI patrol-net overlay (chapter worlds only) — `--debug-ainets`. First tenant of the F13–F24 range reserved for debug overlays; the letter-key overlays (`C`/`X`/…) are to migrate there |
| `F14` | frame-cost readout: fps / current frame cost / worst recent frame, cycling Off → Compact → Full — `--debug-fps=`. Works at the launchscreen too |
| `F15` | targeting overlay — `--debug-targets`. A line from every turret gunner and AI gunner to the target it has acquired: red firing, amber tracking, grey held; the HUD names the gate holding each one (blocked / slewing / shot clock / bored / no solution) |
| `F16` | node-name labels — `--debug-names[=meshes\|all]`. Migrated off `T`, which is free for targeting |
| `F11` | print the mode's subject placement as ready-to-paste `--pos=` / `--direction=` (in `--viewer`: `--pos=` / `--lookat=`, the orbit pivot) |
| `F10` | export the plane on screen (current livery + damage) to a timestamped `.glb` under `Exports/` — the `--export-gltf=` twin |
| `Esc` | at the launchscreen: back, and quit from the Mode screen. In flight it opens the pause board instead — a board menu's Exit item is what leaves a session, so a pad can reach it too |

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
| `WASD` / arrows | move | |
| `Q` / `E` (alt `Z` / `U`) | descend / ascend | |
| RMB-held mouse (alt `IJKL`) | look | |
| click an object | select it | `--debug-select=` |
| `PgUp` / `PgDn` | walk the selection's `cs_name` ancestor ladder | |
| `Home` / `End` | jump to the ends of that ladder | |
| `N` | node lab | `--debug-nodelab=` |
| `M` | mesh lab, on that selection alone | `--debug-mesh=` |
| `C` | show the built colliders, coloured by the surface id they resolve to (see `--collision`) — also bound in `--fly`/`--stunt` | `--debug-colliders` |
| `X` | colour world objects by class (destructible/facade/clutter/scenery) — also bound in `--fly`/`--stunt` | `--debug-classoverlay` |
| `F5` | damage lab on the selected destructible | `--debug-damage=` |
