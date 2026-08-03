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
| `H` | D-pad → | select ordnance |
| `F5` | | damage lab on the flown plane — `--damage=` |
| `B` | | weapon lab panel — `--weapon-lab` sessions only; its steppers arm the plane's live loadout and Space/`F` then fire it (`--weapon-lab=` picks the weapon, `--weapon-mount=` the mount, `--weapon-cycle=` steps the list) |
| `R` | | respawn |
| `Tab` | | cycle stunt target |
| `numpad 1–9` (not `5`) | | hold a fixed camera view around the plane (P1's keyboard) — `--view=` |
| `C` | | show the built colliders (see `--collision`) — `--debug-colliders` |
| `X` | | colour world objects by class (destructible/facade/clutter/scenery) — `--debug-classoverlay` |

## Any mode

| Input | Does |
|---|---|
| `P` | pause — halts the sim; `.` steps one frame |
| `.` | step one frame while paused |
| `T` | node-name labels |
| `F12` | screenshot |
| `F11` | print the mode's subject placement as ready-to-paste `--pos=` / `--direction=` (in `--viewer`: `--pos=` / `--lookat=`, the orbit pivot) |
| `F10` | export the plane on screen (current livery + damage) to a timestamped `.glb` under `Exports/` — the `--export-gltf=` twin |
| `Esc` | quit |

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
| click an object | select it | `--debug-select=` |
| `PgUp` / `PgDn` | walk the selection's `cs_name` ancestor ladder | |
| `Home` / `End` | jump to the ends of that ladder | |
| `N` | node lab | `--debug-nodelab=` |
| `M` | mesh lab, on that selection alone | `--debug-mesh=` |
| `C` | show the built colliders (see `--collision`) — also bound in `--fly`/`--stunt` | `--debug-colliders` |
| `X` | colour world objects by class (destructible/facade/clutter/scenery) — also bound in `--fly`/`--stunt` | `--debug-classoverlay` |
| `F5` | damage lab on the selected destructible | `--debug-damage=` |
