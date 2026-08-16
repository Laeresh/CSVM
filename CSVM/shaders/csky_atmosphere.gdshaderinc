#ifndef CSKY_ATMOSPHERE_INC
#define CSKY_ATMOSPHERE_INC

// Per-mission atmosphere: the weather.json distance fog, plus the SUNLIGHT world dimming.
// Globals rather than material parameters so one write per flight reaches every world and
// aircraft surface without touching the cached materials.
//
// The original's fog volume is a vertical CYLINDER around the camera, not a sphere: only
// horizontal (x/z) distance fogs, scaled by an altitude fade-out — full fog below csky_fog_alt.x,
// none above csky_fog_alt.y. That keeps the overcast deck and anything at sky altitude overhead
// clear instead of graying out.
//
// The altitude term fades by FRAGMENT altitude, not the camera's — settled at the controls of
// the original in C2, the only chapter whose flown FOG_ALTITUDE band (256-1024 m) is inside the
// flight envelope: climbing well above 1024 m the distant city still dissolves into haze, where a
// camera fade would have switched the fog off outright (weather.md).
// ⚠ The old corroboration here — "zone1's altitudes 970 -> 1047 = cloud-band bottom -> whiteout
// centre" — was RETIRED by B12: C1 flies zone2, whose band is 4000-5000 m, so that identity lives
// in a zone C1 never flies. It is real and unexplained, not evidence; do not re-cite it.

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
//
// The RANGE term is a LINEAR ramp, D3D's own `(d - start) / (end - start)` clamped, because that
// is what the data declares: every gamez's `world1` node carries `fog_state == 1` = LINEAR, and
// mech3ax ASSERTS it (`nodes/src/cs/world/data.rs`), so the value is present and identical in all
// eight chapters. 0 in that field would be OFF — the rest of the struct being zeroed is what
// makes a written 1 significant, not what makes it doubtful. It replaced a `smoothstep`, which
// was ours and rested on nothing (2026-08-08).
// ⚠ The ALTITUDE term keeps its smoothstep deliberately. `fog_state` is D3D's distance-fog mode
// and says nothing about a vertical fade; the cylinder's altitude fade is the remake's own model,
// and exactly one flown band in the install (C2 ZONE1, 256-1024 m) is inside the flight envelope,
// so no render in seven of eight chapters can tell one altitude curve from another. Changing it
// would be a guess dressed as a symmetry.
float csky_fog_amount(vec3 fog_world, vec3 camera_world) {
    float d = distance(fog_world.xz, camera_world.xz);
    // max() only guards the divisor: no shipped FOG_RANGES has near == far (and the no-fog range
    // is 1e8/1e9), but a divide-by-zero here would NaN the whole world, not one surface.
    float span = max(csky_fog_range.y - csky_fog_range.x, 0.001);
    float ramp = clamp((d - csky_fog_range.x) / span, 0.0, 1.0);
    return ramp * (1.0 - smoothstep(csky_fog_alt.x, csky_fog_alt.y, fog_world.y));
}

#endif
