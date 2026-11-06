#ifndef CSKY_CLOUD_SHADOW_INC
#define CSKY_CLOUD_SHADOW_INC

// The Enhanced cloud shadows (Effects/CloudShadow.cs owns all four globals, the tint and ambient ones
// declared below). The vec4 is the pattern's drift offset in metres (X, Z), one over the lattice cell
// in metres, and the darkness at a shadow's core; a darkness of 0 draws nothing. The vec2 is the noise
// band over which a shadow's edge ramps. The tunes behind them live in Effects/CloudShadowPattern.cs.
global uniform vec4 csky_cloud_shadow;
global uniform vec2 csky_cloud_shadow_edge;

// A lattice corner's value in [0, 1). The cell wraps at `period`, a power of two, so the pattern
// repeats exactly and the drift offset can wrap with it.
float csky_cloud_hash(ivec2 cell, int period, uint seed) {
    uvec2 c = uvec2(cell & ivec2(period - 1));
    uint h = (c.x * 2376512323u) ^ (c.y * 3625334849u) ^ seed;
    h ^= h >> 16u;
    h *= 2146121005u;
    h ^= h >> 15u;
    h *= 2221713035u;
    h ^= h >> 16u;
    return float(h >> 8u) * (1.0 / 16777216.0);
}

// Smooth value noise over one lattice, in [0, 1).
float csky_cloud_octave(vec2 p, int period, uint seed) {
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    float a = csky_cloud_hash(c, period, seed);
    float b = csky_cloud_hash(c + ivec2(1, 0), period, seed);
    float d = csky_cloud_hash(c + ivec2(0, 1), period, seed);
    float e = csky_cloud_hash(c + ivec2(1, 1), period, seed);
    return mix(mix(a, b, u.x), mix(d, e, u.x), u.y);
}

// The cloud lab's tint (UI/Labs/CloudLab.cs): the light added under a shadow's core so the pattern
// and its drift show plainly. Black, the default, adds nothing.
global uniform vec3 csky_cloud_shadow_tint;

// The share of the ambient light a shadow's core takes away, written through AO. 0 takes none.
global uniform float csky_cloud_shadow_ambient;

// How far a world position sits inside a shadow: 0 in the clear, 1 under its core.
float csky_cloud_cover(vec3 world) {
    vec2 p = (world.xz + csky_cloud_shadow.xy) * csky_cloud_shadow.z;
    float n = (2.0 * csky_cloud_octave(p, 256, 2654435769u) + csky_cloud_octave(p * 2.0, 512, 2246822507u)) / 3.0;
    return smoothstep(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, n);
}

// The share of the sun the clouds let through at a world position: 1 in the clear, down to one less
// the darkness under a shadow's core. It is the one function every sun term reads the clouds through
// (csky_terrain_sun), over the globals one C# owner writes.
float csky_cloud_sun(vec3 world) {
    if (csky_cloud_shadow.w <= 0.0) {
        return 1.0;
    }
    return 1.0 - csky_cloud_shadow.w * csky_cloud_cover(world);
}

// The ambient light's share left at a world position, for a fragment's AO: 1 in the clear or with
// no shadow drawn. Godot applies AO to the ambient light alone, so the sun keeps csky_cloud_sun.
float csky_cloud_ao(vec3 world) {
    if (csky_cloud_shadow_ambient <= 0.0) {
        return 1.0;
    }
    return 1.0 - csky_cloud_shadow_ambient * csky_cloud_cover(world);
}

// The lab tint's light at a world position, black with the tint off or no shadow drawn.
vec3 csky_cloud_tint(vec3 world) {
    if (csky_cloud_shadow.w <= 0.0 || csky_cloud_shadow_tint == vec3(0.0)) {
        return vec3(0.0);
    }
    return csky_cloud_shadow_tint * csky_cloud_cover(world);
}

// The gain on an unshaded surface's colour at a world position (the vegetation), lit as by `sun_pair`
// (csky_sun_light_at) on a half-lit normal: the darkness takes the diffuse part, the ambient share the rest.
vec3 csky_cloud_unlit(vec3 world, vec2 sun_pair) {
    if (csky_cloud_shadow.w <= 0.0) {
        return vec3(1.0);
    }
    float diffuse = 0.5 * sun_pair.y;
    float taken = (csky_cloud_shadow.w * diffuse + csky_cloud_shadow_ambient * sun_pair.x) / max(sun_pair.x + diffuse, 1e-3);
    return vec3(1.0 - taken * csky_cloud_cover(world)) + csky_cloud_tint(world);
}

#endif
