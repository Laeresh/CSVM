#ifndef CSKY_LIGHTS_INC
#define CSKY_LIGHTS_INC

// The animated world's point lights (LIGHT_STATE). The world renders unshaded, so a real Godot
// light contributes nothing to it — the original's DX7 point lights modulated the same baked
// vertex lighting this shader reads, i.e. what they do is spill onto nearby geometry.
//
// Packed as a 2xN data texture because global uniforms cannot be arrays; see WorldLights for the
// layout. csky_light_count = 0 skips the loop entirely, which is what keeps an unlit world
// bit-identical to the pre-lights renderer.
//
// Fullbright passes only — a shaded (aircraft) surface is lit for real and never includes this.

global uniform sampler2D csky_light_data;
global uniform int csky_light_count;

// Measured with --perf on C1's harbour (16 lights live): this loop costs nothing worth
// optimising — the whole viewport's GPU time is 0.26 ms with lights on. An earlier
// bounding-sphere early-out here was removed as unmeasurable complexity; the real cost of
// LIGHT_STATE was always C# (see AnimRuntime's host-resolution cache).
vec3 csky_light_spill(vec3 world_pos, vec3 normal) {
    vec3 spill = vec3(0.0);
    for (int i = 0; i < csky_light_count; i++) {
        vec4 pr = texelFetch(csky_light_data, ivec2(0, i), 0); // xyz = position, w = range max
        vec4 cr = texelFetch(csky_light_data, ivec2(1, i), 0); // rgb = linear colour, a = range min
        vec3 to_light = pr.xyz - world_pos;
        float dist = length(to_light);
        float ndl = max(dot(normal, to_light / max(dist, 0.0001)), 0.0);
        spill += cr.rgb * ndl * (1.0 - smoothstep(cr.a, pr.w, dist));
    }
    return spill;
}

#endif
