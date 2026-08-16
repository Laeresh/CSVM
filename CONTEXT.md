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

## Clutter

**Stamp**:
One placement of a decoration by the UV-lattice stamper. Not a Godot instance, which is the
rendering mechanism a stamp may be drawn by.
_Avoid_: place, scatter, plant, dress, decorate

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
