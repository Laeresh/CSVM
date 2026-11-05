#ifndef CSKY_CLOUD_SHADOW_INC
#define CSKY_CLOUD_SHADOW_INC

// The Enhanced cloud shadows (Effects/CloudShadow.cs owns both globals). The vec4 is the pattern's
// drift offset in metres (X, Z), one over the lattice cell in metres, and the darkness at a shadow's
// core; a darkness of 0 draws nothing. The vec2 is the noise band over which a shadow's edge ramps.
// The tunes behind both live in Effects/CloudShadowPattern.cs.
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

// The share of the sun the clouds let through at a world position: 1 in the clear, down to one less
// the darkness under a shadow's core. It is the one function every sun term reads the clouds through
// (csky_terrain_sun), over the globals one C# owner writes.
float csky_cloud_sun(vec3 world) {
    if (csky_cloud_shadow.w <= 0.0) {
        return 1.0;
    }
    vec2 p = (world.xz + csky_cloud_shadow.xy) * csky_cloud_shadow.z;
    float n = (2.0 * csky_cloud_octave(p, 256, 2654435769u) + csky_cloud_octave(p * 2.0, 512, 2246822507u)) / 3.0;
    return 1.0 - csky_cloud_shadow.w * smoothstep(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, n);
}

#endif
