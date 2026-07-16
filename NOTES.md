# Project Ideas
# Milestones (unordered)
- Extract Files -> Done
- First Render -> Done
- First Area -> Done
- Free Flight -> in Progress
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


## Issues
- Dive sound not as present in the game
- color grading for the skybox
- wrap around on maps or endless sea?
- Maps have different states (Hangars open/closed, Zeppelin present, Cloud layer, Night Day)
## Milestone 2 Polishing
- World Select -> Done
- A lot of Z-Fighting -> Better but not Perfect (Z-Fighting is in the original too. Roof of airfield buildings still an issue)-> Done


- Planes
    - Plane Models are damaged -> Done (damage panels pdp1-8 now hidden, healthy pdpN_h panels restored — Bloodhawk wingtips, Kestrel outer wings; gyro interior lattice backface-culled like the original)
    - Animation of PlaneModels (Ailerons, Ruder, Elevator) (Prop is already in)
    - Lights on Planes visible with texture?
- Mesh Collision for Planes 
    - collision in the original is a lot finer. Tip of wing collides with objects. Need real collider instead of rays
- FlightModel
    - Stalling when too low airspeed is in but not as prominent and should not go relative to plane but to ground. If too slow the plane should go into dive to the ground no matter the position.
    - if plane is turned 90 degree it should have less lift -> nose going down
    - original flight model does not slow speed as much during ascend
- Environment
    - moonSize ?
    - trees on forest texture
- sky clouds move with plane
- Camera
    - Should flip with the plane (upside down)
