#ifndef CSKY_OCEAN_INC
#define CSKY_OCEAN_INC

// PROTOTYPE: the Enhanced wave ocean's switch, shared by the ocean and the base sea sheet it
// replaces. 1 while an ocean node is live (Effects/OceanPrototype.cs), 0 otherwise.
global uniform float csky_ocean_on;

// Whether a base-sea vertex at world height `world_y` steps aside for the ocean. Only the
// sea-level sheet does; C3's raised water and C4's lakes keep their flat glossy surface.
bool csky_ocean_hides_sea(float world_y) {
    return csky_ocean_on > 0.5 && abs(world_y) < 0.25;
}

#endif
