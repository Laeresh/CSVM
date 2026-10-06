#ifndef CSKY_OCEAN_INC
#define CSKY_OCEAN_INC

// The Enhanced wave ocean's switch, shared by the ocean and the base sea sheet it replaces.
// 1 while an ocean node is live (Effects/Ocean.cs), 0 otherwise.
global uniform float csky_ocean_on;

// The live ocean's shore mask (sea coverage in R), its zone groups (R8) and the mask's world rect
// (origin X/Z, then 1 / extent), published by Effects/OceanMask.cs. With no ocean live they hold
// 1x1 defaults: no sea, and a zone every grid draws.
global uniform sampler2D csky_ocean_mask;
global uniform sampler2D csky_ocean_zone;
global uniform vec4 csky_ocean_rect;

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

// Whether a base-sea fragment at world position `world` steps aside for the ocean: only where the
// ocean itself draws, so each texel has one owner and a sheet shown after the mask bake (C3's
// drained crater floor) draws flat instead of leaving a hole. The coverage threshold is the
// ocean's own discard (OceanMask.SeaThreshold). A zone-seam texel (OceanMaskRaster.AnyZone - 1) is
// one no grid draws. C3's raised water, C4's lakes and a ramp's raised part keep their flat surface.
bool csky_ocean_hides_sea(vec3 world) {
    if (csky_ocean_on < 0.5 || abs(world.y) >= 0.25) {
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
