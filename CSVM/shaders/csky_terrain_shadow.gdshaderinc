#ifndef CSKY_TERRAIN_SHADOW_INC
#define CSKY_TERRAIN_SHADOW_INC

// The Enhanced terrain's baked sun shadow (Effects/TerrainShadow.cs): per 8 m texel the height of the
// sun ray grazing the ground upstream along the sun's azimuth (R) and the horizontal distance to that
// ground (G), over the rect (origin X/Z, then 1 / extent). The soft pair is the penumbra's growth in
// height per metre of blocker distance and its one-texel floor. With no bake live the texture is one
// texel far below any world, so everything reads lit.
global uniform sampler2D csky_terrain_shadow;
global uniform vec4 csky_terrain_shadow_rect;
global uniform vec2 csky_terrain_shadow_soft;

// The sun's share reaching a world position: 1 lit, 0 in terrain shadow, a ramp over the softness
// between. Off the rect everything is lit. Fetched texel by texel like the ocean mask, so no sampler
// state a global carries can change it. TerrainShadowRaster.Lit spells the same ramp.
float csky_terrain_sun(vec3 world) {
    vec2 uv = (world.xz - csky_terrain_shadow_rect.xy) * csky_terrain_shadow_rect.zw;
    if (any(lessThan(uv, vec2(0.0))) || any(greaterThan(uv, vec2(1.0)))) {
        return 1.0;
    }
    ivec2 size = textureSize(csky_terrain_shadow, 0);
    vec2 t = uv * vec2(size) - 0.5;
    ivec2 i = ivec2(floor(t));
    vec2 f = t - floor(t);
    ivec2 hi = size - 1;
    vec2 a = texelFetch(csky_terrain_shadow, clamp(i, ivec2(0), hi), 0).rg;
    vec2 b = texelFetch(csky_terrain_shadow, clamp(i + ivec2(1, 0), ivec2(0), hi), 0).rg;
    vec2 c = texelFetch(csky_terrain_shadow, clamp(i + ivec2(0, 1), ivec2(0), hi), 0).rg;
    vec2 d = texelFetch(csky_terrain_shadow, clamp(i + ivec2(1, 1), ivec2(0), hi), 0).rg;
    vec2 texel = mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
    float soft = max(texel.y * csky_terrain_shadow_soft.x, csky_terrain_shadow_soft.y);
    return 1.0 - clamp((texel.x - world.y) / soft + 0.5, 0.0, 1.0);
}

// Godot's own Lambert diffuse and Schlick-GGX specular (scene_forward_lights_inc.glsl), which a
// light() replaces, with the light's share scaled by `sun`: the baked terrain shadow's share for the
// directional light, 1 for any other. `f0` is the surface's 0.16 * SPECULAR * SPECULAR, which
// light() cannot read; METALLIC is 0 on every arm.
void csky_terrain_light(float sun, vec3 n, vec3 l, vec3 v, vec3 color, float attenuation,
        float roughness, float f0, float light_specular, out vec3 diffuse, out vec3 specular) {
    float att = attenuation * sun;
    float ndl = max(dot(n, l), 0.0);
    diffuse = color * (ndl / PI) * att;
    specular = vec3(0.0);
    if (roughness > 0.0) {
        vec3 h = normalize(v + l);
        float ndv = max(dot(n, v), 1e-4);
        float ndh = clamp(dot(n, h), 0.0, 1.0);
        float ldh = clamp(dot(l, h), 0.0, 1.0);
        float alpha = roughness * roughness;
        float a = ndh * alpha;
        float k = alpha / (1.0 - ndh * ndh + a * a);
        float d = k * k / PI;
        float g = 0.5 / mix(2.0 * ndl * ndv, ndl + ndv, alpha);
        float m = 1.0 - ldh;
        float f90 = clamp(f0 * 49.5, 0.0, 1.0);
        float f = f0 + (f90 - f0) * m * m * m * m * m;
        specular = color * att * light_specular * ndl * d * f * g;
    }
}

#endif
