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
- Extract rof file zlib 1.3.1 
- Dive sound not as present in the game
- color grading for the skybox
- FlightModel
    - Turn rates, especially rolling is a lot faster in original -> Added a factor
    - Should loose height on stalling -> Done
- Dmg Model
    - Collider for tail need to be retuned (Bloodhawk wings have tail colliders)
- Environment
    - Map extension not exactily the same as in original
    - C4 Cloud deck texture not following plane
    - generic way to find billboard sprites (flames on refinery?)
    - Still some lights not having correct look --campos=-4419.995,147.909,-5960.063 --lookat=-4419.357,147.405,-5960.646
    - C3 Massive z fighting at beach --campos=-6151.614,136.079,-3198.714 --lookat=-6150.76,135.796,-3199.151
    - C3 trees in water --campos=-6151.614,136.079,-3198.714 --lookat=-6150.76,135.796,-3199.151
    - C3 What is this: --campos=-6151.614,136.079,-3198.714 --lookat=-6150.76,135.796,-3199.151
    - C2 Seaplane Hangar has Zeppelin over its position and is nowhere near the sea:
        screenshot saved: Z:\Crimson Skies\Screenshots\crimsonskies_2026-07-19_16-26-53-115.png
        camera pose: --campos=-220.479,42.865,-315.996 --lookat=-219.928,42.708,-315.176
        screenshot saved: Z:\Crimson Skies\Screenshots\crimsonskies_2026-07-19_16-28-32-371.png
        camera pose: --campos=26.538,13.511,25.575 --lookat=25.895,13.551,24.81
- Sound
    - Mute when not focused
    - Engine retune: he confusing extra combs turned out to be the original playing the engine as a ~5%-detuned dual stack (a chorus effect — ours is a single loop, noted as a backlog fidelity nit)
- HUD
    - Needles not correct form (could create procedually instead of by texture in Hi-Def mode)
    - Crash Dmg display blinking completly red

### Work for me


Lighting
One open thread: SunIncidence = 0.46 is fit to that single overcast reference. The night/day self-scaling is a principled prediction — a matched C1B-night and a bright-day original would let me confirm or nudge that one constant. Cheap to grab whenever you're in-game.


## Milestone 2 Polishing Run 3
- Environment
    - We need a generic way to find billboard sprites. I found multiple more instances of billboard that are facing the player some are turned directly to camera, some only in x,y axis (flames on harbor raffinery)
    - Billboard should generally have no collision. The collision for trees is nice but not in the original
    - determing which zone is used on which chapter/missionn
    - Fine tuning fog and environment (Recording videos from spawn points flying straight for x seconds)
    - Better mission states, there is still a lot of difference in the maps. Perhaps we need a pipeline to find the difference or need to crack the mission loading states