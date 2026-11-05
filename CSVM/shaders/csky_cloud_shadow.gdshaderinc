#ifndef CSKY_CLOUD_SHADOW_INC
#define CSKY_CLOUD_SHADOW_INC

// The Enhanced cloud shadows (Effects/CloudShadow.cs owns all three globals). The map holds per texel
// the share of the sun the mission's clouds let through, cast along the sun onto the sea-level plane
// (Effects/CloudShadowRaster.cs). The rect is the map's origin X/Z there, then one over its extent. The
// vec4 is the metres a sun ray runs along X and Z per metre it rises, then the darkness of an opaque
// cloud; a darkness of 0 draws nothing.
global uniform sampler2D csky_cloud_shadow_map;
global uniform vec4 csky_cloud_shadow_rect;
global uniform vec4 csky_cloud_shadow;

// The share of the sun the clouds let through at a world position: 1 in the clear, down to one less
// the darkness under an opaque cloud. It is the one function every sun term reads the clouds through
// (csky_terrain_sun). The point reads the texel its sun ray crosses at sea level, fetched texel by
// texel like the terrain shadow so no sampler state a global carries can change it.
// CloudShadowRaster.Through spells the same read.
float csky_cloud_sun(vec3 world) {
    if (csky_cloud_shadow.z <= 0.0) {
        return 1.0;
    }
    vec2 foot = world.xz - world.y * csky_cloud_shadow.xy;
    vec2 uv = (foot - csky_cloud_shadow_rect.xy) * csky_cloud_shadow_rect.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) {
        return 1.0;
    }
    ivec2 size = textureSize(csky_cloud_shadow_map, 0);
    vec2 t = uv * vec2(size) - 0.5;
    ivec2 i = ivec2(floor(t));
    vec2 f = t - floor(t);
    ivec2 hi = size - 1;
    float a = texelFetch(csky_cloud_shadow_map, clamp(i, ivec2(0), hi), 0).r;
    float b = texelFetch(csky_cloud_shadow_map, clamp(i + ivec2(1, 0), ivec2(0), hi), 0).r;
    float c = texelFetch(csky_cloud_shadow_map, clamp(i + ivec2(0, 1), ivec2(0), hi), 0).r;
    float d = texelFetch(csky_cloud_shadow_map, clamp(i + ivec2(1, 1), ivec2(0), hi), 0).r;
    float through = mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
    return 1.0 - csky_cloud_shadow.z * (1.0 - through);
}

#endif
