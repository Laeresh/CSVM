#ifndef CSKY_ATMOSPHERE_INC
#define CSKY_ATMOSPHERE_INC

// Per-mission atmosphere: the weather.json distance fog, plus the SUNLIGHT world dimming.
// Globals rather than material parameters so one write per flight reaches every world and
// aircraft surface without touching the cached materials.
//
// The original's fog volume is a vertical CYLINDER around the camera, not a sphere: only
// horizontal (x/z) distance fogs, scaled by an altitude fade-out — full fog below csky_fog_alt.x,
// none above csky_fog_alt.y. That keeps the overcast deck and anything at sky altitude overhead
// clear instead of graying out (user-diagnosed; corroborated by zone1's altitudes 970 -> 1047 =
// exactly cloud-band bottom -> whiteout-band centre).

global uniform vec3 csky_fog_color;
global uniform vec2 csky_fog_range;      // x = near (clear), y = far (full fog), horizontal metres
global uniform vec2 csky_fog_alt;        // fragment altitude: full fog below x, fades to none at y
global uniform float csky_world_light = 1.0; // per-mission SUNLIGHT dimming (M2-polish item 6)

// How much fog covers a fragment. `fog_world` is the fragment in WORLD space (under
// skip_vertex_transform, VERTEX is view-space — lift it with INV_VIEW_MATRIX first).
//
// `camera_world` is passed in rather than read from CAMERA_POSITION_WORLD because Godot's
// stage built-ins are not reachable inside a user-defined shader function; call it as
// csky_fog_amount(fog_world, CAMERA_POSITION_WORLD) from fragment().
//
// This expression was duplicated verbatim in four shaders before 2026-07-23.
float csky_fog_amount(vec3 fog_world, vec3 camera_world) {
    return smoothstep(csky_fog_range.x, csky_fog_range.y, distance(fog_world.xz, camera_world.xz))
         * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));
}

#endif
