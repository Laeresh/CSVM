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
- Sound
    - Mute when not focused
    - Engine retune: he confusing extra combs turned out to be the original playing the engine as a ~5%-detuned dual stack (a chorus effect — ours is a single loop, noted as a backlog fidelity nit)
- HUD
    - Needles not correct form (could create procedually instead of by texture in Hi-Def mode)
    - Crash Dmg display blinking completly red
## Milestone 2 Polishing Run 2

### Work for me
Your side of the run, collected from the grill answers:

Original-game research: does part damage degrade handling/power before a critical part dies, or is it cosmetic until the explosion? (Decides a future run's scope.)
 - Yes decreases turn rate depending on which part is damaged (elevator, ruder) Dmg Motor has decreased performance.
 - Probably depending on dmg state green, yellow red
Measurements for item 12: time a full 360° roll and a sustained pitch maneuver in the original (Bloodhawk, cruise speed).
roll 2sec
pitch 11 sec(with slow down of speed?)
yaw 30sec
Reference screenshots for item 6: original night shots of the deck underside, sky gradient, and lit terrain at spots we can reproduce with --campos.


Lighting
One open thread: SunIncidence = 0.46 is fit to that single overcast reference. The night/day self-scaling is a principled prediction — a matched C1B-night and a bright-day original would let me confirm or nudge that one constant. Cheap to grab whenever you're in-game.


First Prototype:
 - Stunt Mission flyable
    - A stunt mission goal is to fly through dangerous positions (under bridges, through hangars and tunnels). Each mission has a set number of objectives which need to be flown through or reach a minimum distance. Objectives can be completed in any order. The objective switches to the next if it is completed
    - Correct target nodes -> Objectives are named dzpaths<x>, dz<x>, dzones in ia.json
    - Mission Marker to Nodes -> Arrow with text if not in sight pointing to the Marker (shortest angle), else only  text on position of Marker. Button to switch to next Marker
    - Checks if objective was flown through
    - Keep list of fulfilled objectives
    - Scoring when all objectives are met (timed)
- Simple launchscreen -> Select Chapter -> Select Plane
    - Controllable by keyboard and Controller. Mouse select would be a bonus but not necessary.
- Splitscreen 2/4 Player (Nice to have for testing with friends, does not need complete features like plane to plane collision and 3d Sound)
    - add new player if additional controller presses start in launchscreen
    - P1 selects chapter
    - Then Player can select their planes
    - 2 Player horizontal, 4 player grid