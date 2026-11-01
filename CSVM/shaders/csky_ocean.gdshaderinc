#ifndef CSKY_OCEAN_INC
#define CSKY_OCEAN_INC

#include "res://shaders/csky_time.gdshaderinc"

// The Enhanced wave ocean's switch, shared by the ocean, the base sea sheet it replaces, the
// surf ring over that sheet and the painted coast tiles that give way to it.
// 1 while an ocean node is live (Effects/Ocean.cs), 0 otherwise.
global uniform float csky_ocean_on;

// The live ocean's shore mask (sea coverage in R), its zone groups (R8) and the mask's world rect
// (origin X/Z, then 1 / extent), published by Effects/OceanMask.cs. With no ocean live they hold
// 1x1 defaults: no sea, and a zone every grid draws.
global uniform sampler2D csky_ocean_mask;
global uniform sampler2D csky_ocean_zone;
global uniform vec4 csky_ocean_rect;

// The signed distance from the coastline over the mask's rect (R8, OceanMaskRaster.InlandByte), and
// the live sea's surf: lines passing a second, 1 when it breaks surf, the swash's reach in metres and
// the inland texture's reach. Set by Effects/OceanMask.cs and Effects/Ocean.cs.
global uniform sampler2D csky_ocean_inland;
global uniform vec4 csky_ocean_surf;

// Two hashes in [0, 1] per integer cell (a PCG2D mix). It hashes the cell's integer coordinates
// exactly, so every cell sharing a corner hashes it alike. A float hash of a large coordinate rounds
// differently per call site and cuts the noise along straight lattice lines.
vec2 sea_hash(vec2 c) {
    uvec2 v = uvec2(ivec2(c)) * 1664525u + 1013904223u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v ^= v >> 16u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v ^= v >> 16u;
    return vec2(v) * (1.0 / 4294967295.0);
}

// Gradient noise at q, about -0.7 to 0.7, smooth across its cells. The cell's x repeats every
// period cells. The half cell keeps the modulo exact, so no corner hashes differently in two cells.
float foam_noise(vec2 q, float period) {
    vec2 i = floor(q);
    vec2 f = fract(q);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec2 x = vec2(i.x, i.x + 1.0);
    x -= period * floor((x + 0.5) / period);
    float a = dot(sea_hash(vec2(x.x, i.y)) * 2.0 - 1.0, f);
    float b = dot(sea_hash(vec2(x.y, i.y)) * 2.0 - 1.0, f - vec2(1.0, 0.0));
    float c = dot(sea_hash(vec2(x.x, i.y + 1.0)) * 2.0 - 1.0, f - vec2(0.0, 1.0));
    float d = dot(sea_hash(vec2(x.y, i.y + 1.0)) * 2.0 - 1.0, f - vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// The surf's breaking lines' phase in cycles where they reach the land, at world X/Z p with
// lines_per_sec lines a second (whole per csky_time wrap). The ocean's lines and the coasts' swash
// both read it, so each swash runs up with the line that made it. TUNE: a still noise field bends
// the lines by up to 5 radians over 40 m cells, off the coast's own offset.
float csky_surf_arrive(vec2 p, float lines_per_sec) {
    return csky_time * lines_per_sec + 0.795775 * foam_noise(p * 0.025, 65536.0);
}

// The mask's sea coverage at a world X/Z, filtered as the ocean's own linear, clamp-to-edge sampler
// reads it. Fetched texel by texel, so no sampler state a global uniform carries can change it.
float csky_ocean_sea(vec2 xz) {
    ivec2 size = textureSize(csky_ocean_mask, 0);
    vec2 t = (xz - csky_ocean_rect.xy) * csky_ocean_rect.zw * vec2(size) - 0.5;
    ivec2 i = ivec2(floor(t));
    vec2 f = t - floor(t);
    ivec2 hi = size - 1;
    float a = texelFetch(csky_ocean_mask, clamp(i, ivec2(0), hi), 0).r;
    float b = texelFetch(csky_ocean_mask, clamp(i + ivec2(1, 0), ivec2(0), hi), 0).r;
    float c = texelFetch(csky_ocean_mask, clamp(i + ivec2(0, 1), ivec2(0), hi), 0).r;
    float d = texelFetch(csky_ocean_mask, clamp(i + ivec2(1, 1), ivec2(0), hi), 0).r;
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// The signed metres from the coastline at a world X/Z, landward positive, filtered as csky_ocean_sea
// reads the mask (OceanMask.InlandAt spells it in C#).
float csky_ocean_inland_at(vec2 xz) {
    ivec2 size = textureSize(csky_ocean_inland, 0);
    vec2 t = (xz - csky_ocean_rect.xy) * csky_ocean_rect.zw * vec2(size) - 0.5;
    ivec2 i = ivec2(floor(t));
    vec2 f = t - floor(t);
    ivec2 hi = size - 1;
    float a = texelFetch(csky_ocean_inland, clamp(i, ivec2(0), hi), 0).r;
    float b = texelFetch(csky_ocean_inland, clamp(i + ivec2(1, 0), ivec2(0), hi), 0).r;
    float c = texelFetch(csky_ocean_inland, clamp(i + ivec2(0, 1), ivec2(0), hi), 0).r;
    float d = texelFetch(csky_ocean_inland, clamp(i + ivec2(1, 1), ivec2(0), hi), 0).r;
    return (mix(mix(a, b, f.x), mix(c, d, f.x), f.y) * 2.0 - 1.0) * csky_ocean_surf.w;
}

// TUNE. The wet sand's tint over the albedo, and the foam film's colour (the ocean's own foam) and
// its densest cover.
const vec3 CSKY_SWASH_WET = vec3(0.55, 0.55, 0.6);
const vec3 CSKY_SWASH_FOAM = vec3(0.6, 0.65, 0.68);
const float CSKY_SWASH_COVER = 0.8;

// A painted coast's albedo under the swash, at a fragment `world` drawn at a footprint of metres per
// pixel. On the land side of the coastline within the reach, each breaker runs up as a foam film over
// wet sand and drains before the next. Sea-level fragments only, so a pier or a hull takes none.
vec3 csky_ocean_swash(vec3 world, vec3 albedo, float footprint) {
    float reach = csky_ocean_surf.z;
    if (csky_ocean_surf.y < 0.5 || abs(world.y) >= 0.5) {
        return albedo;
    }
    float d = csky_ocean_inland_at(world.xz);
    if (d <= 0.0 || d >= reach) {
        return albedo;
    }
    // Cycles since the line's crest (sin(2 pi cyc) at its peak) reached the coastline.
    float s = fract(csky_surf_arrive(world.xz, csky_ocean_surf.x) - 0.25);
    // TUNE. The run-up climbs for 0.3 of a cycle, the backwash drains for the rest.
    float run = s < 0.3 ? sin(1.5707963 * s / 0.3) : 0.5 + 0.5 * cos(3.1415927 * (s - 0.3) / 0.7);
    // A still noise field of 5 m cells ragged the front, so it never runs up as one smooth curve.
    float front = reach * run * (1.0 + 0.3 * foam_noise(world.xz * 0.2 + vec2(-31.0, 17.0), 65536.0));
    float film = 1.0 - smoothstep(front - 1.5, front, d);
    // The sand under the reach's lower part stays damp between breakers.
    float wet = max(film, 0.6 * (1.0 - smoothstep(0.5 * reach, reach, d)));
    // A foamy bore at the front over a thin film, which thins further as it drains.
    float density = film * mix(0.15, 1.0, smoothstep(front - 3.0, front - 0.3, d)) * (1.0 - 0.6 * smoothstep(0.3, 1.0, s));
    // The film's lace: 1.6 m cells drifting 900 cells per csky_time wrap, faded to its mean where a
    // pixel spans too much of a cell to show it without shimmer.
    float n = foam_noise(world.xz * 0.625 - vec2(csky_time * (900.0 / 3600.0), 0.0), 900.0);
    float lace = mix(0.5, smoothstep(0.0, 0.3, n + 0.4 * density - 0.1), smoothstep(4.0, 9.0, 1.6 / footprint));
    vec3 col = albedo * mix(vec3(1.0), CSKY_SWASH_WET, wet);
    return mix(col, CSKY_SWASH_FOAM, CSKY_SWASH_COVER * density * lace);
}

// Whether any texel the filter reads at a world X/Z is a ramp's open sea (OceanMaskRaster.RampSea).
bool csky_ocean_ramp(vec2 xz) {
    ivec2 size = textureSize(csky_ocean_mask, 0);
    ivec2 i = ivec2(floor((xz - csky_ocean_rect.xy) * csky_ocean_rect.zw * vec2(size) - 0.5));
    ivec2 hi = size - 1;
    vec4 r = vec4(texelFetch(csky_ocean_mask, clamp(i, ivec2(0), hi), 0).r,
        texelFetch(csky_ocean_mask, clamp(i + ivec2(1, 0), ivec2(0), hi), 0).r,
        texelFetch(csky_ocean_mask, clamp(i + ivec2(0, 1), ivec2(0), hi), 0).r,
        texelFetch(csky_ocean_mask, clamp(i + ivec2(1, 1), ivec2(0), hi), 0).r);
    return any(lessThan(abs(r * 255.0 - 254.0), vec4(0.5)));
}

// Whether a base-sea, surf-ring or painted sea-tile fragment at world position `world` steps aside for the ocean: only
// where the ocean itself draws, so each texel has one owner and a sheet shown after the mask bake (C3's
// drained crater floor) draws flat instead of leaving a hole. The coverage threshold is the
// ocean's own discard (OceanMask.SeaThreshold). A zone-seam texel (OceanMaskRaster.AnyZone - 1) is
// one no grid draws. A ramp clears up to OceanMask.RampTop; C3's raised water and C4's lakes stay.
bool csky_ocean_hides_sea(vec3 world) {
    if (csky_ocean_on < 0.5 || world.y <= -0.25 || world.y >= 4.0) {
        return false;
    }
    if (world.y >= 0.25 && !csky_ocean_ramp(world.xz)) {
        return false;
    }
    if (csky_ocean_sea(world.xz) < 0.02) {
        return false;
    }
    ivec2 size = textureSize(csky_ocean_zone, 0);
    ivec2 i = ivec2(floor((world.xz - csky_ocean_rect.xy) * csky_ocean_rect.zw * vec2(size)));
    float zone = texelFetch(csky_ocean_zone, clamp(i, ivec2(0), size - 1), 0).r * 255.0;
    return abs(zone - 254.0) > 0.5;
}

#endif
