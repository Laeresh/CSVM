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
## Milestone 2 Polishing Run 2
- Remove warning at start for pir_spinner.tif. the image is not in the source files and the stack traces eats tokens.
- Environment
    - Maps have different states (Hangars open/closed, Zeppelin present) depending on Mission and scenario. Need to analyze mission files.
    - Maps have animated objects like cars and trains (with steam clouds)
    - Maps have desctructable objects that are loaded together with the non-damaged ones that leads to flickering (oil-tanks on airport, hangars)
    - The game seems to have some kind of wraparound. In the original the terrain or water does not seem to end. Not sure how this works (Water should be easy by repeating but the terrain does not have any gaps). Perhaps procedually generated.
    - wrap around on maps or endless sea?
    - Cosmetics:
        - Clouds render through fog
        - clouddeck to bright
    - Errors thrown in New York and Rocky Mountains areas
    - Fog in Rocky Mountains not correct (bright white, hard cut off)
    - Rocky Mountains IA1 has rain weather effect
    - Documentation for .json parameters
- DMG Model
    - Collider too large on some planes (Tail of BloodHawk, a lot of empty space)
    - HP for components (vehicle.json)
    - Plains lose parts (damaged models) when getting shot or lightly crashing into things
    - Collision are not always crashes, depends on which part is hit and speed, sometimes only damages the plane
    - There are hints on damage animation in the vehicle.json
    - On Crash there is not only a explosion but the plane breaks apart and the parts are lying on the ground
### Work for me
Your side of the run, collected from the grill answers:

Original-game research: does part damage degrade handling/power before a critical part dies, or is it cosmetic until the explosion? (Decides a future run's scope.)
Measurements for item 12: time a full 360° roll and a sustained pitch maneuver in the original (Bloodhawk, cruise speed).
Reference screenshots for item 6: original night shots of the deck underside, sky gradient, and lit terrain at spots we can reproduce with --campos.

Wrap around, repeats parts of the mesh. Are there vertex that repeat, have vectors that have the same yz or xz coordinates as the edge of the map