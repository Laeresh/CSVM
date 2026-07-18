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
- Environment
    - Map extension not exactily the same as in original
    - C4 Cloud deck texture not following plane
## Milestone 2 Polishing Run 2

### Work for me
Your side of the run, collected from the grill answers:

Original-game research: does part damage degrade handling/power before a critical part dies, or is it cosmetic until the explosion? (Decides a future run's scope.)
 - Yes decreases turn rate depending on which part is damaged (elevator, ruder) Dmg Motor has decreased performance.
 - Probably depending on dmg state green, yellow red
Measurements for item 12: time a full 360° roll and a sustained pitch maneuver in the original (Bloodhawk, cruise speed).
1.0-1.2 seconds
Reference screenshots for item 6: original night shots of the deck underside, sky gradient, and lit terrain at spots we can reproduce with --campos.


Lighting
One open thread: SunIncidence = 0.46 is fit to that single overcast reference. The night/day self-scaling is a principled prediction — a matched C1B-night and a bright-day original would let me confirm or nudge that one constant. Cheap to grab whenever you're in-game.