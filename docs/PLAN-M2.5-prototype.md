# Milestone 2.5 — First Prototype

Working plan for the first *playable* build: the original's **Stunt Flying** instant-action
mode (fly through all Danger Zones, timed), a controller/keyboard launchscreen
(mode → chapter → plane), and 2–4-player splitscreen as the closing, explicitly cuttable
items. Planned 2026-07-19 after a grilling session; scope decisions are recorded in the
footer. Each item lists its goal, the data/code evidence it rests on, the approach, and how
it gets verified. Statuses: ☐ open · ◐ in progress · ☑ done.

Ground rules carried over from the polish runs: original-game data drives everything
(ia.json `dzones`, targets.json, messages.json, gamez `dzN` nodes); hand-tuned constants are
marked TUNE and validated by user playtests against the original; CLAUDE.md +
`docs/architecture.md` are updated in the same turn as each landed item; new reader decodes
land with their `docs/formats/` page in the same change (targets/messages → a new
`missions.md` or a `spawns.md` section).

## Checklist

1. ☐ Stunt mission core — dzones/targets/messages loaders, dz resolution, sphere detection, `--stunt` mode
2. ☐ Marker HUD — original-format text + distance + clock position, edge arrow, target cycling, run status line
3. ☐ Scoring — timer + splits, end scoreboard, best-time persistence, restart flow
4. ☐ Launchscreen — Mode → Chapter → Plane, keyboard/controller navigation, no-args default
5. ☐ Splitscreen foundation — shared-world multi-viewport rendering + per-player input (CLI-driven, free flight)
6. ☐ Splitscreen join + plane select — Start-to-join in the launchscreen, simultaneous pick, 2P/4P layouts, per-player HUD
7. ☐ Splitscreen stunt race — per-player progress/timers, shared end scoreboard

---

## 1. Stunt mission core

**Goal:** `--stunt` starts a stunt run: the mission's Danger Zones load from data, flying
close enough to a zone point completes it (logged), completing all of them ends the run.
Playable-but-ugly: no marker HUD yet (item 2), completion visible in the log/text HUD.

**Evidence (verified 2026-07-19):**
- Every chapter's IA1 `ia.json` has a `dzones` list of `[dzpathN, dzN]` pairs (C1: 5) plus
  `stunt_flying` spawn points (already consumed by `SpawnPoints.LoadIa`).
- In gamez, `dzN` is a pure point marker: `Object3d`, `mesh_index -1`, no children, world
  translation (C1 `dz1` = (−5186.19, 141.24, −6500.59)); `dzpathN` is an untextured
  vertex-colored polyline ribbon mesh under the `dzpaths` group (C1 meshes 998–1003) tracing
  the route — `dz1`'s point is literally a vertex of `dzpath1`'s polyline, i.e. the ribbon is
  the AI/guide route and the dz point sits on it. WorldBuilder currently skips the whole
  `dzpaths` subtree.
- `targets.json` (mission zrdr) maps each dz node to its display strings: `description`
  (e.g. `MSG_OBJ_TRAINTUNNEL_M`), `category_label` (`MSG_OBJ_DZ`), `help_label`
  (`MSG_OBJ_FLYTHROUGH` / `MSG_OBJ_FLYOVER`).
- `extracted/messages.json` resolves the keys: `MSG_OBJ_DZ` = "Danger Zone",
  `MSG_OBJ_FLYTHROUGH` = "Fly Through", `MSG_OBJ_TRAINTUNNEL_M` = "Train Tunnel Mid", plus
  "Fly through all the Danger Zones to win!" (id 3842) for the intro line.
- The user's original screenshot (`OriginalScreenshots/C1 IA1 Cloudcoverage 1.png`) shows the
  assembled marker text: `Danger Zone [Fly Through] - Train Tunnel Mid 7 o'clock`.

**Approach:**
- `src/Mech3/Messages.cs` — key→string map over `extracted/messages.json` (new default arg
  `--messages=`).
- `src/Flight/MissionTargets.cs` — targets.json loader (description/category/help keys per
  node name; generic, not dz-specific — future mission types reuse it).
- `src/Flight/StuntMission.cs` — the mode's state: ordered zone list from ia.json `dzones`,
  world-space dz points resolved via the `cs_name` meta (the MissionState pattern — Godot
  mangles duplicate names), per-zone completed flag, active-target index (next incomplete in
  list order; completing **any** zone counts — order-free), sphere test
  `|planePos − dzPos| < DzRadius` (TUNE, start 60 m) each physics frame, `AllComplete` event.
  `Fly Over` help-label zones use the same sphere (note in docs; revisit only if a real
  mission reads wrong).
- Wiring: `--stunt` = `--fly` + scenario forced to `stunt_flying` + the StuntMission attached
  to the FlightController (crash/respawn: progress and clock untouched — deliberate
  divergence, the original exits to the selection screen on crash, annoying for testing).
- `--debug-dzpaths` builds the `dzpaths` subtree visibly (never seen rendered in the
  original — AI/route data; debug-only by decision).
- Docs: `docs/formats/spawns.md` gains the dzones/targets/messages decode (or a new
  `missions.md` if it outgrows the page).

**Verify:** scripted `--stunt --spawn=N --hold` run flies through a known dz sphere and logs
the completion + the switch to the next target; a run visiting all 5 C1 zones logs
`AllComplete`; all 8 chapters parse their dzones/targets (loader smoke over every IA1);
`--fly` without `--stunt` builds byte-identical behavior (no stunt state, dzpaths still
skipped); `--debug-dzpaths` screenshot shows the ribbons.

## 2. Marker HUD

**Goal:** The original's objective marker guidance: when the active zone is on screen, its
text block renders at the zone's projected position; when off screen, a screen-edge arrow
points the shortest rotation toward it, text beside it. Plus a run-status line (timer +
zones done) and a button to cycle the pointed-at target.

**Evidence:** the reference screenshot's marker (bottom-left, blue): arrow + two-line text
`Danger Zone [Fly Through] - Train Tunnel Mid 7 o'clock`. Clock position = the target's
bearing relative to the plane's heading, in clock hours. (The circular live-camera inset
next to it is **out of scope** this milestone — backlog.) Text pipeline fully data-driven
via item 1's loaders.

**Approach:** a `MarkerHud` Control (CompassTape/GaugeCluster pattern — per-player-instantiable
later): project the active dz through the camera; on-screen → label at the point (name +
live distance, plain HUD font, original-blue); off-screen/behind → clamp to the screen edge
along the direction of the projected offset, draw an arrow pointing out (shortest-angle side
by construction), same label + the computed `N o'clock` suffix (12 sectors, relative
bearing, matching the screenshot's usage). Distance in the HUD's units. Target cycling:
Tab / gamepad Y (TUNE binding) advances the *displayed* target through incomplete zones;
auto-switch on completion keeps working. Run-status line near the timer position: elapsed
`m:ss.t` + `zones 2/5`; a one-shot intro line "Fly through all the Danger Zones to win!"
(messages id 3842) on run start. Completion feedback: brief highlight of the label + a log
line (sound cue if a matching `snd_*` def exists — check the SETS while in there, else none).

**Verify:** static/scripted shots: marker on-screen at the dz projected position with correct
text vs the reference screenshot's format; off-screen shots show the edge arrow on the
correct side (fly past the zone, arrow flips through the shortest angle); clock value matches
hand-computed bearing in a `--hold` run; cycling walks only incomplete zones; user A/B vs
the original's marker for feel/placement.

## 3. Scoring — timer, splits, scoreboard, persistence

**Goal:** A complete run produces a scoreboard: total time, per-zone splits, plane, chapter;
best time persists across sessions.

**Approach:** timer starts at spawn (first physics tick of the run), each completion records
its split; on `AllComplete` the timer stops and a scoreboard overlay renders (clean Godot
UI): zone list with split + cumulative times (zone display names from item 1), total, plane,
chapter/scenario, best-time comparison (`NEW BEST` flag). Persistence:
`user://stunt_scores.json` keyed `chapter/mission/plane` → best total + date (user:// only —
nothing in the repo). Input on the scoreboard: R = new run (fresh timer + zones, replicating
today's respawn), Esc = quit (item 4 later rewires it to the launchscreen). Crash rules per
item 1: the clock never stops, completed zones stay.

**Verify:** scripted full run produces the scoreboard with plausible monotonic splits; a
second, slower scripted run shows the best-time comparison and does not overwrite the best;
the JSON survives a relaunch; a crash mid-run + respawn continues the same clock (log
timestamps prove it); R on the scoreboard starts a clean run.

## 4. Launchscreen — Mode → Chapter → Plane

**Goal:** Launching with **no user args** shows an in-game menu instead of requiring the
CLI: pick Free Flight or Stunt Flying, then the chapter, then the plane; flies with the
existing pipeline. Any explicit mode arg (`--fly`, `--stunt`, `--plane`, `--chapter`,
`--screenshot`, …) bypasses the menu entirely — every automated/dev workflow keeps working,
RunDev.ps1's console menus stay as-is.

**Approach:** a Control-scene state machine in front of the existing arg-driven flow
(PlaneViewer gains a "menu → resolved args → build" path; the menu literally fills in the
same option set the CLI parser produces, so there is exactly one downstream code path).
Three screens, clean Godot UI (dark panels, focused-item highlight): **Mode** (Free Flight /
Stunt Flying), **Chapter** (the 8 chapter worlds; display names from the data where
available), **Plane** (the 7 `player_*` planes; display name + a few stats from
PlaneStats — no 3D preview this milestone). Navigation: keyboard (arrows/Enter/Esc) and
any controller (d-pad + stick / A = accept / B = back) drive a shared focus; mouse click =
bonus if free. Esc from Mode quits; scoreboard/crash Esc returns here (state machine owns
the world teardown/rebuild — verify a second consecutive launch builds clean, the first
in-process world rebuild we've ever done).

**Verify:** no-args launch → menu → C1 stunt run plays end-to-end with only a controller in
hand (and separately keyboard-only); every existing CLI invocation in CLAUDE.md's examples
bypasses the menu unchanged (`--screenshot` smokes stay deterministic); menu → fly → Esc →
menu → different chapter+plane → fly works twice in one process without errors; RunDev.ps1
still drives its own console flow.

## 5. Splitscreen foundation — shared world, per-player viewports + input

**Goal:** The technical core de-risked before any UI: N planes in one shared world, each
rendered in its own viewport with its own camera-anchored sky/weather, each flown by its own
device. CLI-driven (`--players=N`, debug default planes) so it's testable without item 6.

**Evidence / known constraints (from the module docs):** skydome + CloudDeck + CloudPuffs +
MapEdgeExtender are all *the-camera*-anchored singletons today (PlaneViewer re-centers the
dome per frame, re-anchors the deck under "the player", the extender windows around "the
camera"); Precipitation is already per-view for free (`CAMERA_POSITION_WORLD` in-shader);
global fog/world-light shader params are world-global — same chapter for everyone, fine.
FlightController reads global Input actions; audio is one global mix.

**Approach:**
- Rendering: one `World3D`; N `SubViewportContainer`+`SubViewport`, each with `world_3d`
  shared and its own Camera3D. Layouts: 2P = horizontal stack (top/bottom), 3–4P = 2×2 grid
  (3P leaves a quadrant black/logo). Per-viewport render resolution comes free from the
  split.
- Per-player camera-anchored elements: one skydome + cloud-deck + cloud-puff instance per
  player on a per-player **visibility layer**, each camera's cull mask seeing only its own
  set (the dome is already a cheap single mesh; deck/puffs likewise). PlaneViewer's per-frame
  anchoring loops over players.
- MapEdgeExtender: window = union of rings around every player's cell (the diff already
  works on a cell set; anchor list instead of single anchor). Whiteout overlay: per-viewport
  Control driven by that player's camera altitude.
- Input: FlightController input goes through a per-player device binding (P1 = keyboard
  **and** joy device 0 merged; P2–P4 = joy devices 1–3) using device-explicit joystick reads;
  R respawns only that player; P debug pause stays single-player-only.
- Audio: engine/whine/rattle loops per plane at a reduced per-player gain (TUNE), crash
  one-shots global, no positional audio (decision) — revisit only if the mix is mush.
- No plane-to-plane collision (decision): PlaneCollider sweeps ignore other planes' hulls;
  planes render in all viewports (they're normal world children).

**Verify:** `--players=2` (keyboard vs one controller): both planes flyable simultaneously,
each viewport shows its own correctly-anchored skydome/deck (fly apart 5+ km and screenshot
both views — no shared-dome parallax artifacts); one player crosses the map edge while the
other stays central (extender serves both, no window thrash); crash of P2 leaves P1 flying;
frame rate measured at 2P and 4P over C1 and the heaviest chapter (C5) — record numbers,
decide if any quality knob is needed; single-player path pixel-unchanged (screenshot smoke).

## 6. Splitscreen join + plane select + per-player HUD

**Goal:** The launchscreen grows the join flow: extra controllers press Start to join
(anywhere before plane select), P1 picks mode + chapter, then **all joined players pick
planes simultaneously**, each with their own cursor; the flight screens get the item-2/3 HUD
per player.

**Approach:** device assignment per decision: P1 = keyboard + first controller; each further
controller joins on Start (joined-player strip visible on every menu screen; Start on an
assigned pad does nothing; B/back un-joins). Plane select becomes a shared list with N
colored cursors (P1–P4 tags); a player's Accept locks their pick (visible lock), all locked →
flight. HUD: CompassTape, GaugeCluster, marker HUD, run-status, DMG line instantiated per
SubViewport (they're Controls — parent them into each container; half/quarter-screen
metrics: keep the HUD.png-derived sizes relative to viewport height, TUNE after a look).
Scoreboard overlay becomes per-player-aware (item 7 fills it).

**Verify:** join two pads + keyboard → 3P grid launches with the right device driving each
viewport (wiggle-test each); un-join works; simultaneous pick with pick-lock race (two
cursors on the same plane — duplicates allowed, verify both lock); every per-player HUD
element renders in its own viewport only (screenshot each quadrant); single-player HUD
unchanged.

## 7. Splitscreen stunt race

**Goal:** The prototype's multiplayer payoff: all players fly the same stunt mission
concurrently; each has their own zone progress, marker HUD, and timer; the shared end
scoreboard ranks by total time.

**Approach:** one StuntMission instance per player (item 1's state is already
self-contained); per-player marker/status HUD (item 6); spawn each player at a different
`stunt_flying` spawn index (list order, wrapping). A player's clock stops at their own
`AllComplete`; a `FINISHED m:ss.t` banner shows in their viewport while others fly on. When
the last player finishes, the shared scoreboard overlays the whole screen: per-player rows
(plane, total, splits collapsed to total + zones), winner highlighted; R = rematch (same
setup, fresh clocks), Esc = launchscreen. Best-time persistence stays single-player-only
(decision deferred — race times aren't comparable across player counts; note in docs).

**Verify:** 2P race with scripted device inputs (or user + one scripted): independent
progress (P1 completing a zone does not complete P2's), per-viewport markers point at each
player's *own* active zone, first-finisher banner while the second flies, final scoreboard
ranks correctly; rematch resets both cleanly; user playtest with a friend = the real
milestone gate.

---

*Scope decisions from the 2026-07-19 grilling session:*
- *Completion detection: sphere around the `dzN` point, radius TUNE (~60 m start). No gate
  planes, no altitude gate.*
- *`dzpathN` ribbons: never seen in the original (likely AI-route data) — hidden except
  under `--debug-dzpaths`.*
- *Marker: name + distance, plain HUD font, original text format
  `Danger Zone [Fly Through] - <name> <clock>` from targets.json + messages.json. The
  original's circular live-camera objective inset: out of scope, backlog.*
- *Scoring: elapsed time + per-zone splits; best time persisted per chapter+plane in
  `user://` JSON. No invented points formula.*
- *Crash mid-run: respawn, keep completed zones, clock keeps running — deliberate divergence
  (the original exits to the selection screen; bad for testing).*
- *Launchscreen: Mode (Free Flight / Stunt) → Chapter → Plane; no-args shows it, any
  explicit CLI arg bypasses; clean Godot UI now, original menu art later; new `--stunt` arg
  for scripted runs.*
- *Splitscreen: in plan as the last three items, explicitly cuttable. Race = same zones,
  per-player progress + timer, best time wins. Shared world, planes visible to each other,
  no plane-to-plane collision, no positional audio.*
- *Join: P1 = keyboard + first controller; each extra controller joins via Start in the
  launchscreen; simultaneous plane pick with per-player cursors. 2P horizontal, 3–4P grid.*
