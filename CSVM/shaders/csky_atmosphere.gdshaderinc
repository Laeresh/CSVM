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

// The per-view fog table (Session/World/FogViewTable.cs). Splitscreen panes and spyglass pictures
// each sit in their own camera weather state, so each wears its own zone's fog and world light.
// One mat4 per view, by column: xyz = eye, w = world light; rgb = fog colour; xy = range,
// zw = altitude. A fragment reads the view whose eye stands nearest its own camera.
// ⚠ Read the plain globals above whenever csky_view_count < 2. The table is published only while
// views disagree, and that branch is what keeps a single view's render the one it always was.
global uniform int csky_view_count;
global uniform mat4 csky_view_0;
global uniform mat4 csky_view_1;
global uniform mat4 csky_view_2;
global uniform mat4 csky_view_3;
global uniform mat4 csky_view_4;
global uniform mat4 csky_view_5;
global uniform mat4 csky_view_6;
global uniform mat4 csky_view_7;

// One view of the table by index; globals cannot be arrays.
mat4 csky_view(int i) {
    if (i == 0) return csky_view_0;
    if (i == 1) return csky_view_1;
    if (i == 2) return csky_view_2;
    if (i == 3) return csky_view_3;
    if (i == 4) return csky_view_4;
    if (i == 5) return csky_view_5;
    if (i == 6) return csky_view_6;
    return csky_view_7;
}

// The view standing nearest `camera_world`, ties to the lower index. Only reached with two or
// more views published.
mat4 csky_view_at(vec3 camera_world) {
    mat4 best = csky_view_0;
    vec3 d0 = best[0].xyz - camera_world;
    float best_d = dot(d0, d0);
    for (int i = 1; i < min(csky_view_count, 8); i++) {
        mat4 view = csky_view(i);
        vec3 d = view[0].xyz - camera_world;
        float dd = dot(d, d);
        if (dd < best_d) {
            best_d = dd;
            best = view;
        }
    }
    return best;
}

// The fog colour of the view drawing from `camera_world`. Pass CAMERA_POSITION_WORLD; a user
// function cannot read it itself.
vec3 csky_fog_color_at(vec3 camera_world) {
    if (csky_view_count > 1) {
        return csky_view_at(camera_world)[1].rgb;
    }
    return csky_fog_color;
}

// The SUNLIGHT world dimming of the view drawing from `camera_world`, as csky_fog_color_at.
float csky_world_light_at(vec3 camera_world) {
    if (csky_view_count > 1) {
        return csky_view_at(camera_world)[0].w;
    }
    return csky_world_light;
}

// The same SUNLIGHT uncollapsed, for the one surface the collapse cannot describe: a camera-facing
// card, whose authored normals turn with the view instead of holding the world's mean N.L
// (docs/org/vertexLighting.md). csky_sun_dir points FROM a surface TOWARD the light, the sense
// max(N.L, 0) wants; the pair is the zone's authored SUNLIGHT_AMBIENT and SUNLIGHT_DIFFUSE.
global uniform vec3 csky_sun_dir = vec3(0.0, 1.0, 0.0);
global uniform vec2 csky_sun_light = vec2(1.0, 0.0);

// The original's per-vertex directional term on one world-space normal. The caller multiplies it
// onto the AUTHORED vertex colour and clamps the PRODUCT, which is where the original clamps and
// the collapse does not.
// ⚠ Colourless on purpose, not a simplification of the light: every chapter authoring a lit cloud
// card writes white for both SUNLIGHT colours, so the triples the binary multiplies are identity.
float csky_sun_vertex_light(vec3 world_normal) {
    return csky_sun_light.x + csky_sun_light.y * max(dot(normalize(world_normal), csky_sun_dir), 0.0);
}

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
    vec2 fog_range = csky_fog_range;
    vec2 fog_alt = csky_fog_alt;
    if (csky_view_count > 1) {
        vec4 view = csky_view_at(camera_world)[2];
        fog_range = view.xy;
        fog_alt = view.zw;
    }
    float d = distance(fog_world.xz, camera_world.xz);
    // max() only guards the divisor: no shipped FOG_RANGES has near == far (and the no-fog range
    // is 1e8/1e9), but a divide-by-zero here would NaN the whole world, not one surface.
    float span = max(fog_range.y - fog_range.x, 0.001);
    float ramp = clamp((d - fog_range.x) / span, 0.0, 1.0);
    return ramp * (1.0 - smoothstep(fog_alt.x, fog_alt.y, fog_world.y));
}

#endif
