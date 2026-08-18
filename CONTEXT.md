# CSVM domain language

Terms this project has fixed, and the words to avoid for them. Use the term as written here in
code comments, docs, backlog entries and commit messages. A concept with no entry is not
forbidden; it is untracked, and inventing a fifth word for a concept that already has one is what
this file exists to stop.

The original game's own names are a separate authority and always win: `cs_name` node names,
`zrdr` reader keys, `PUFFER_STATE` / `LIGHT_STATE` / `SOUND_NODE`, chapter IDs `C1` to `C5`. Never
rename those to something tidier. See `docs/agents/domain.md`.

## Decode and remake

**The original**:
The shipped 1998 program and its data, as the authority a behaviour is measured against.
_Avoid_: the engine, the game, the binary, retail, mech3

**Decode**:
A behaviour worked out from the original's data or code, reproducible from that evidence.
_Avoid_: port, derive, mirror, reproduce (as nouns for the result)

**Tune**:
A constant calibrated to a measured original with no decoded mechanism behind it. A tune is
replaced when a mechanism is found, never added to.
_Avoid_: fudge, hack, approximation, guess, magic number

**Remake-only rule**:
Behaviour with no counterpart in the original, kept deliberately and with a measured reason.
_Avoid_: divergence, deviation, our own, deliberate departure

## Teams

**Team id**:
The one integer space every combat actor shares: `0` neutral, `1` the player's side, `2` and up
hostile sides. Two actors are hostile when their ids differ and neither is `0`. There is no second
space and no per-kind mapping; an aircraft, an emplacement and a world object are compared as
plain integers.
_Avoid_: engine team, engine-space team, team index, side id, faction, alliance

**Authored team**:
The team id a data file carries. The same integer as the team id, with no conversion on load; an
absent `TEAM` key means `2`.
_Avoid_: original team id, raw team, data team, loader team

**Versus band**:
Where the extra humans of a splitscreen `--vs` session sit, kept clear of the ids world data
authors so a player cannot inherit an emplacement's side. A remake-only rule: the original has no
per-pilot team ladder.
_Avoid_: team band, emplacement band, pilot team offset

## Clutter

**Stamp**:
One placement of a decoration by the UV-lattice stamper. Not a Godot instance, which is the
rendering mechanism a stamp may be drawn by.
_Avoid_: place, scatter, plant, dress, decorate

## Aircraft assembly

**Flight roster**:
The session's aircraft set: the human field prepared at session start and the AI aircraft introduced later by missions, waves, or generators.
_Avoid_: AI roster (it excludes the human field)

## Boards and the sim clock

**Board**:
A full-screen overlay panel drawn over the flight, in the shared board style.
_Avoid_: overlay, screen, popup, dialog, panel

**Results board**:
The boards shown when a run is over: the Instant Action wrap-up, the dogfight result, the race
result, the solo stunt result.
_Avoid_: end screen, endscreen, game over screen, wrap-up screen

**Board menu**:
The cursor and item list a board carries, driven by pad or keyboard rather than Godot focus.
_Avoid_: pause menu (it appears on results boards too), overlay menu, button list

**Menu owner**:
The one player whose input drives a board menu. Every other pad is inert while it is up.
_Avoid_: focus (Godot focus is deliberately unused here), active player, controlling player

**Halt**:
The sim clock is not advancing, whatever the cause.
_Avoid_: freeze, frozen, stop, stopped

**Halt reason**:
One cause of a halt. The clock advances only when there are none.
_Avoid_: halt flag, pause flag, halt source

**Pause**:
A player-requested halt, as distinct from a halt any other reason caused.
_Avoid_: using it for a halt a results board caused

**Rerun**:
Resetting the running mode in place to its start, same world, with no session teardown. What the
race, the dogfight and a free flight do. Distinct from a respawn, which returns one plane to the
air mid-run.
_Avoid_: restart (that is the other mechanism, below), soft restart, reset

**Restart**:
Freeing the session and building a fresh one from the same settings, behind the load screen. What
an Instant Action mission does, because its opposition lives in the world and cannot be put back
in place. An unpinned restart draws a new mission; a pinned one repeats.
_Avoid_: relaunch, reload, hard reset

**Load screen**:
The board drawn over the whole window while a session builds. Carries no progress: a build is one
synchronous block.
_Avoid_: loading screen, splash, please-wait

## Format documentation

**Format family**:
A coherent group of related original-game data formats documented under one canonical page in `docs/formats/`.
_Avoid_: format page

**Format-family landing page**:
The canonical entry page for a format family; it routes independent detailed subtopics to child pages.
_Avoid_: parent page

**Format reader path**:
The explanation order: at a glance, conceptual model, reference, reader rules and edge cases, then evidence and related pages.
_Avoid_: boilerplate

**Format child page**:
An independently useful subtopic with its own source, evidence, terminology, or implementation concerns. A 25–30 KB landing page is a review prompt, not an automatic split.
_Avoid_: overflow page
