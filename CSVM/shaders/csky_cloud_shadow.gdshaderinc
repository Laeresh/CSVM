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

// The pattern's raw noise at a world position, in [0, 1), before the edge band.
float csky_cloud_noise(vec3 world) {
    vec2 p = (world.xz + csky_cloud_shadow.xy) * csky_cloud_shadow.z;
    return (2.0 * csky_cloud_octave(p, 256, 2654435769u) + csky_cloud_octave(p * 2.0, 512, 2246822507u)) / 3.0;
}

// How far a world position sits inside a shadow: 0 in the clear, 1 under its core.
float csky_cloud_cover(vec3 world) {
    return smoothstep(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, csky_cloud_noise(world));
}

// PROTOTYPE (--proto-clouds, Effects/ProtoClouds.cs): the volumetric slab's shadow on the ground. The
// vec4 is the deck's floor in metres, the sun share it takes (0 = prototype off), and its rifts' share
// and size; the second is the direction toward the sun and the heads' altitude the pattern is read at.
global uniform vec4 csky_pc_ground;
global uniform vec4 csky_pc_ground_sun;

// Hash and value noise over a 3D lattice, in [0, 1), the prototype's. The lattice wraps at 256 cells.
float csky_pc_hash3(ivec3 c, uint seed) {
    uvec3 u = uvec3(c & ivec3(255));
    uint h = (u.x * 2376512323u) ^ (u.y * 3625334849u) ^ (u.z * 1911520717u) ^ seed;
    h ^= h >> 16u;
    h *= 2146121005u;
    h ^= h >> 15u;
    h *= 2221713035u;
    h ^= h >> 16u;
    return float(h >> 8u) * (1.0 / 16777216.0);
}

float csky_pc_noise3(vec3 p, uint seed) {
    vec3 i = floor(p);
    vec3 f = p - i;
    vec3 u = f * f * (3.0 - 2.0 * f);
    ivec3 c = ivec3(i);
    float n000 = csky_pc_hash3(c, seed);
    float n100 = csky_pc_hash3(c + ivec3(1, 0, 0), seed);
    float n010 = csky_pc_hash3(c + ivec3(0, 1, 0), seed);
    float n110 = csky_pc_hash3(c + ivec3(1, 1, 0), seed);
    float n001 = csky_pc_hash3(c + ivec3(0, 0, 1), seed);
    float n101 = csky_pc_hash3(c + ivec3(1, 0, 1), seed);
    float n011 = csky_pc_hash3(c + ivec3(0, 1, 1), seed);
    float n111 = csky_pc_hash3(c + ivec3(1, 1, 1), seed);
    float x00 = mix(n000, n100, u.x);
    float x10 = mix(n010, n110, u.x);
    float x01 = mix(n001, n101, u.x);
    float x11 = mix(n011, n111, u.x);
    return mix(mix(x00, x10, u.y), mix(x01, x11, u.y), u.z);
}

// Three octaves of value noise, in [0, 1). The erosion detail every variant carves its coverage with.
float csky_pc_fbm(vec3 p) {
    float n = 0.5 * csky_pc_noise3(p, 2654435769u);
    n += 0.25 * csky_pc_noise3(p * 2.03, 2246822507u);
    n += 0.125 * csky_pc_noise3(p * 4.11, 3266489917u);
    return n / 0.875;
}

// The own top of the cloud standing over `p`, as a share of the layer. A low noise a few clouds
// across, so neighbours tower to different heights, from 30 % of the layer to its top.
float csky_pc_cloud_top(vec3 p, float detail_m) {
    float broad = csky_pc_noise3(vec3(p.x, 0.0, p.z) / (detail_m * 11.0), 1911520717u);
    float fine = csky_pc_noise3(vec3(p.x, 5.0, p.z) / (detail_m * 4.0), 2246822507u);
    return mix(0.3, 1.0, smoothstep(0.25, 0.85, broad * 0.75 + fine * 0.25));
}

// Density at a world point of a layer whose horizontal coverage is `cover` (1 under a cloud's
// core); the coverage already carries each cloud's height and dome. `detail_m` is the erosion cell
// and `erosion` how deep the noise bites: lightly at the flat base, harder up the crown and hardest
// where the cover thins at the sides, so the walls break into billows.
float csky_pc_density(vec3 p, float bottom, float top, float cover, float detail_m, float erosion) {
    if (cover <= 0.0) {
        return 0.0;
    }
    float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
    float n = csky_pc_fbm(p / detail_m);
    float bite = erosion * (mix(0.3, 1.2, h) + (1.0 - min(cover, 1.0)) * 1.2);
    float d = min(cover, 1.0) * smoothstep(0.0, 0.05, h) - bite * (1.0 - n);
    return clamp(d, 0.0, 1.0);
}

// Rifts torn in the deck: a broad noise opens `gaps` of the sheet in tears about `gap_m` across,
// with soft edges. The share of the deck kept: 1 under the sheet or with no gaps, 0 in a rift.
float csky_pc_deck_gaps(vec3 p, float gaps, float gap_m) {
    if (gaps <= 0.0) {
        return 1.0;
    }
    float broad = csky_pc_noise3(vec3(p.x, 0.0, p.z) / gap_m, 3266489917u);
    float fine = csky_pc_noise3(vec3(p.x, 11.0, p.z) / (gap_m * 0.37), 2246822507u);
    float n = broad * 0.8 + fine * 0.2;
    return smoothstep(gaps - 0.02, gaps + 0.04, n);
}

// Where the sun ray from `world` crosses altitude `y`; `world` itself with no prototype (a heads
// altitude of 0) or a low sun.
vec3 csky_pc_along_sun(vec3 world, float y) {
    vec3 s = csky_pc_ground_sun.xyz;
    if (csky_pc_ground_sun.w <= 0.0 || s.y < 0.05 || world.y >= y) {
        return world;
    }
    return world + s * ((y - world.y) / s.y);
}

// The slab's head coverage at `p`, which the slab and its ground shadow both read. Each cloud has
// its own top (csky_pc_cloud_top). Up to it the noise threshold dips a little through the body, so
// the cloud bulges, then climbs by shape.x toward the top, rounding it into a dome, over a band
// shape.y wide; past the top nothing stands. The point billows by shape.z metres and leans shape.w
// downwind at the layer's top.
float csky_pc_cover_shaped(vec3 p, float bottom, float top, float detail_m, float cover_bias, vec4 shape) {
    float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
    vec3 w = p / (detail_m * 4.0);
    vec2 warp = vec2(csky_pc_noise3(w, 1013904223u), csky_pc_noise3(w + 17.0, 1664525u)) - 0.5;
    vec3 q = p + vec3(warp.x * shape.z * (0.4 + h) + shape.w * h, 0.0, warp.y * shape.z * (0.4 + h));
    float r = h / csky_pc_cloud_top(q, detail_m);
    float rc = clamp(r, 0.0, 1.0);
    float rise = shape.x * pow(rc, 2.5) - shape.x * 0.25 * sin(3.14159265 * rc) + smoothstep(0.85, 1.0, r) * 0.6;
    float lo = mix(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, 0.5) - cover_bias * 0.25 + rise;
    return smoothstep(lo - shape.y * 0.5, lo + shape.y * 0.5, csky_cloud_noise(q));
}

// The slab's heads for the ground shadow (ProtoClouds.PublishGround): the shape vec4 as above; the
// layer's floor and top, erosion cell and coverage bias; the heads' gain, erosion, density gain, and
// the ambient share the deck takes (read by csky_cloud_ao).
global uniform vec4 csky_pc_heads_shape;
global uniform vec4 csky_pc_heads_layer;
global uniform vec4 csky_pc_heads_density;

// The heads' cover over `world`. Under the prototype it is the light the slab's own density takes
// along the sun ray, four taps through the layer, so every head shades by its thickness.
float csky_cloud_cover_lit(vec3 world) {
    if (csky_pc_ground_sun.w <= 0.0) {
        return csky_cloud_cover(world);
    }
    vec3 s = csky_pc_ground_sun.xyz;
    vec4 layer = csky_pc_heads_layer;
    vec4 k = csky_pc_heads_density;
    if (s.y < 0.05) {
        return 0.0;
    }
    float t0 = max(layer.x - world.y, 0.0) / s.y;
    float t1 = max(layer.y - world.y, 0.0) / s.y;
    float dt = (t1 - t0) * 0.25;
    float od = 0.0;
    for (int i = 0; i < 4; i++) {
        vec3 p = world + s * (t0 + dt * (float(i) + 0.5));
        float c = csky_pc_cover_shaped(p, layer.x, layer.y, layer.z, layer.w, csky_pc_heads_shape) * k.x;
        od += csky_pc_density(p, layer.x, layer.y, c, layer.z, k.y) * k.z * dt;
    }
    return 1.0 - exp(-od);
}

// The sun share the prototype's deck lets through at `world`: 1 in a rift or with no deck.
float csky_pc_deck_sun(vec3 world) {
    if (csky_pc_ground.y <= 0.0) {
        return 1.0;
    }
    vec3 at = csky_pc_along_sun(world, csky_pc_ground.x);
    // csky_pc_deck_gaps is the share of the deck kept, 1 under the sheet and 0 in a rift.
    return 1.0 - csky_pc_ground.y * csky_pc_deck_gaps(at, csky_pc_ground.z, csky_pc_ground.w);
}

// The share of the sun the clouds let through at a world position: 1 in the clear, down to one less
// the darkness under a shadow's core. It is the one function every sun term reads the clouds through
// (csky_terrain_sun), over the globals one C# owner writes.
float csky_cloud_sun(vec3 world) {
    float deck = csky_pc_deck_sun(world);
    if (csky_cloud_shadow.w <= 0.0) {
        return deck;
    }
    return (1.0 - csky_cloud_shadow.w * csky_cloud_cover_lit(world)) * deck;
}

// The ambient light's share left at a world position, for a fragment's AO: 1 in the clear or with
// no shadow drawn. Godot applies AO to the ambient light alone, so the sun keeps csky_cloud_sun.
// Under the prototype's deck the sky light dims too, so a rift's patch reads lit on matte ground and water.
float csky_cloud_ao(vec3 world) {
    float deck = 1.0 - csky_pc_heads_density.w * (1.0 - csky_pc_deck_sun(world));
    if (csky_cloud_shadow_ambient <= 0.0) {
        return deck;
    }
    return (1.0 - csky_cloud_shadow_ambient * csky_cloud_cover_lit(world)) * deck;
}

// The lab tint's light at a world position, black with the tint off. The heads' shadow takes the
// tint's own colour and the prototype's deck shadow the same strength in blue.
vec3 csky_cloud_tint(vec3 world) {
    if (csky_cloud_shadow_tint == vec3(0.0)) {
        return vec3(0.0);
    }
    vec3 heads = csky_cloud_shadow.w > 0.0 ? csky_cloud_shadow_tint * csky_cloud_cover_lit(world) : vec3(0.0);
    float strength = max(csky_cloud_shadow_tint.r, max(csky_cloud_shadow_tint.g, csky_cloud_shadow_tint.b));
    return heads + vec3(0.0, 0.0, strength) * (1.0 - csky_pc_deck_sun(world));
}

// The gain on an unshaded surface's colour at a world position (the vegetation), lit as by `sun_pair`
// (csky_sun_light_at) on a half-lit normal: the darkness takes the diffuse part, the ambient share the rest.
vec3 csky_cloud_unlit(vec3 world, vec2 sun_pair) {
    float diffuse = 0.5 * sun_pair.y;
    float deck = 1.0 - (1.0 - csky_pc_deck_sun(world)) * diffuse / max(sun_pair.x + diffuse, 1e-3);
    if (csky_cloud_shadow.w <= 0.0) {
        return vec3(deck);
    }
    float taken = (csky_cloud_shadow.w * diffuse + csky_cloud_shadow_ambient * sun_pair.x) / max(sun_pair.x + diffuse, 1e-3);
    return vec3((1.0 - taken * csky_cloud_cover_lit(world)) * deck) + csky_cloud_tint(world);
}

#endif
