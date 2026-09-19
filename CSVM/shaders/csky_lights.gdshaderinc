#ifndef CSKY_LIGHTS_INC
#define CSKY_LIGHTS_INC

// The live LIGHT_STATE point lights, as the original's per-vertex point-light term: the sum of
// weight(d) x colour, where colour already carries the light's ambient + diffuse scalar and weight
// runs linearly from 1 at the near range to 0 at the far one. No N.L: the original's point term
// has none (docs/org/vertexLighting.md, "Point lights"). The result is a factor on the authored
// vertex colour, added to the sun's, and the caller clamps the product.
//
// Packed as a 2xN data texture because global uniforms cannot be arrays; see WorldLights for the
// layout. csky_light_count = 0 skips the loop entirely.

global uniform sampler2D csky_light_data;
global uniform int csky_light_count;

// Measured with --perf on C1's harbour (16 lights live): this loop costs nothing worth
// optimising. The real cost of LIGHT_STATE was always C# (see AnimRuntime's host-resolution cache).
vec3 csky_point_light(vec3 world_pos) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < csky_light_count; i++) {
        vec4 pr = texelFetch(csky_light_data, ivec2(0, i), 0); // xyz = position, w = far range
        vec4 cr = texelFetch(csky_light_data, ivec2(1, i), 0); // rgb = factor colour, a = near range
        float dist = length(pr.xyz - world_pos);
        sum += cr.rgb * clamp((pr.w - dist) / max(pr.w - cr.a, 0.0001), 0.0, 1.0);
    }
    return sum;
}

#endif
