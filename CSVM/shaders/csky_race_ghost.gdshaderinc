#ifndef CSKY_RACE_GHOST_INC
#define CSKY_RACE_GHOST_INC

// The race ghost: another race pilot's aircraft fades from solid to a dithered ghost as the camera
// drawing it closes in. The distance is the drawing camera's own, so each splitscreen pane fades
// every other aircraft at that pane's distance from it, in one shared scene. The law and its
// constants are CSVM.Mech3.RaceGhost's, passed in as literals by the generated shader; the dither
// is csky_clutter_dither_keep, which keeps the airframe in the opaque pass.

// One instance's alpha for the camera drawing it. 1 while the instance is unarmed (csky_ghost.w),
// for its own pilot's cameras, which drop that pilot's first-person layer (csky_ghost.x), and for
// its own armed Danger Zone photograph (csky_photo_eye). Otherwise floor_alpha within `within`
// metres, 1 beyond `beyond`, linear in distance between.
float csky_race_ghost_alpha(vec3 origin, vec3 camera, uint layers, vec4 ghost, vec4 eye,
        float within, float beyond, float floor_alpha, float eye_reach) {
    if (ghost.w < 0.5 || (layers & uint(ghost.x)) == 0u) {
        return 1.0;
    }
    if (eye.w > 0.5 && distance(camera, eye.xyz) < eye_reach) {
        return 1.0;
    }
    float t = clamp((distance(origin, camera) - within) / (beyond - within), 0.0, 1.0);
    return mix(floor_alpha, 1.0, t);
}

#endif
