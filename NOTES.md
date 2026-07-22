# Project Ideas
# Milestones (unordered)
- Extract Files -> Done
- First Render -> Done
- First Area -> Done
- Free Flight -> in Progress
- Plane paints and decals
- Player Controller with shooting
- Flight AI
- First Enemy with behaviour
- HUD
- Instant Action
- UI for Instant Action
- Campaign
- Multiplayer
- VR Support
- GameModes
    - Stunt Flying
        - Objectives are named dzpaths<x>, dz<x>
        - dzones in ia.json
## Player
- Control via Mouse-Keyboard, Gamepad, Joystick and HOTAS/HOSAS
## Free Floating Ideas
- Visual Improvements
- Coop (Far Far away)
- Additional Weapons/Equipment like YAML Mod for Mechwarrior 5: Mercenaries
- Dynamic Weapons on the models.
- Hangar to move in and look at your planes, like in Mechwarrior 5: Mercenaries
- Editor
## Features
- Nitro (Special Look, Smoke, Wobble, Speed Boost)
## Resouces
https://boardgamegeek.com/thread/2882301/crimson-skies-new-components-for-2022
https://github.com/bethington/ghidra-mcp


## Issues

**Moved to [backlog.md](backlog.md) on 2026-07-22.** They are filed there under "Open bugs",
"Milestone 2 polish run 3 — candidate scope", the feature backlog, the open fidelity questions and
the owed playtests. Every item was checked against the code before being moved: only the two
already marked done here were in fact done (roll-rate factor, height loss on stall), and those are
deleted. Three of the checks pinned a root cause that was not written down anywhere — the C4 cloud
deck, the C5 `csky_fog_on` warning and the non-looping C1 cars — and those causes are recorded with
the backlog entries.

New issues go here; they get triaged into `backlog.md` in the same way.

(The old "C3 what is this" line was dropped 2026-07-22 — probably a mis-copied camera pose. If
something really is wrong there it will resurface.)
