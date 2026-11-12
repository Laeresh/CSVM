#ifndef CSKY_VOLUMETRIC_CLOUDS_INC
#define CSKY_VOLUMETRIC_CLOUDS_INC

// The volumetric cloud slab's helpers (Session/World/VolumetricClouds.cs): the deck's density, the
// phase and transmittance terms, and the screen-ray helpers its full-screen march reads. The slab's
// shader includes it, and so do the effects drawn after the slab, for csky_pc_veil_at.

#include "res://shaders/csky_cloud_shadow.gdshaderinc"

// The noise, the rifts, the head coverage and the head density live in csky_cloud_shadow, whose
// ground shadow marches the same field.

// The stratus deck under the heads: a sheet from `bottom` to `deck_top` covering `cover` of the
// sky, its holes where the broad noise falls under the uncovered share. Zero with no cover.
float csky_pc_deck_density(vec3 p, float bottom, float deck_top, float cover, float detail_m) {
    if (cover <= 0.0 || p.y < bottom || p.y > deck_top) {
        return 0.0;
    }
    // Cells about a kilometre across thin the sheet, lift its base and lower its top, so the sun
    // leaks through the thin ones, the underside reads lumpy rather than one even grey, and from
    // above the thinnest open onto the ground.
    float cells = csky_pc_noise3(vec3(p.x, 3.0, p.z) / (detail_m * 6.0), 1911520717u);
    float thick = mix(0.15, 1.0, smoothstep(0.25, 0.75, cells));
    float h = (p.y - bottom) / max(deck_top - bottom, 1.0);
    float base = (1.0 - thick) * 0.45;
    float prof = smoothstep(base, base + 0.12, h) * (1.0 - smoothstep(0.3 + 0.25 * thick, 0.5 + 0.5 * thick, h));
    float n = csky_pc_fbm(p / (detail_m * 1.8));
    float body = clamp((n - (1.0 - cover)) / max(cover * 0.5, 0.05), 0.0, 1.0);
    return body * prof * thick;
}

// Henyey-Greenstein phase: forward scattering lights a cloud's rim when the sun stands behind it.
float csky_pc_phase(float cos_theta, float g) {
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.14159265 * pow(1.0 + g2 - 2.0 * g * cos_theta, 1.5));
}

// Beer-Lambert transmittance with the powder term, which darkens a thick cloud's lit face less
// than a thin one's and keeps the sun side from going flat white.
float csky_pc_beer_powder(float optical_depth) {
    float beer = exp(-optical_depth);
    float powder = 1.0 - exp(-optical_depth * 2.0);
    return beer * mix(1.0, powder, 0.5);
}

// Scene distance along the pixel's ray from the depth buffer, in metres: the far plane reads as
// no hit at all (1e9), so sky pixels march the whole layer.
float csky_pc_scene_distance(float raw, vec2 screen_uv, mat4 inv_projection) {
    if (raw <= 0.0) {
        return 1e9;
    }
    vec4 v = inv_projection * vec4(screen_uv * 2.0 - 1.0, raw, 1.0);
    return length(v.xyz / v.w);
}

// The world-space direction of the pixel's ray, from the view's projection and camera basis.
vec3 csky_pc_ray_dir(vec2 screen_uv, mat4 inv_projection, mat4 inv_view) {
    vec4 v = inv_projection * vec4(screen_uv * 2.0 - 1.0, 1.0, 1.0);
    vec3 view_dir = normalize(v.xyz / v.w);
    return normalize((inv_view * vec4(view_dir, 0.0)).xyz);
}

// The ray's entry and exit distances through a horizontal slab, or a negative pair where it misses.
vec2 csky_pc_slab_span(vec3 eye, vec3 dir, float bottom, float top) {
    if (abs(dir.y) < 1e-4) {
        return eye.y > bottom && eye.y < top ? vec2(0.0, 1e9) : vec2(-1.0, -1.0);
    }
    float t0 = (bottom - eye.y) / dir.y;
    float t1 = (top - eye.y) / dir.y;
    float near = max(min(t0, t1), 0.0);
    float far = max(t0, t1);
    return far <= near ? vec2(-1.0, -1.0) : vec2(near, far);
}

// The slab's layer as the effects drawn after it read it (VolumetricClouds.PublishGround): its floor
// and top, and the deck's cover and depth. All zero while no slab draws.
global uniform vec4 csky_pc_veil;

// The share of an effect at `p` the slab lets through toward `eye`: 1 with no slab, or no layer
// between them. It marches the slab's density over the ground shadow's globals: the heads in twelve
// steps, and the deck in three over its own depth, which the heads' steps would step over.
float csky_pc_veil_at(vec3 eye, vec3 p) {
    vec4 v = csky_pc_veil;
    vec3 d = p - eye;
    float len = length(d);
    if (v.y <= v.x || len < 1.0) {
        return 1.0;
    }
    vec3 dir = d / len;
    vec2 span = csky_pc_slab_span(eye, dir, v.x, v.y);
    float t1 = min(span.y, len);
    if (t1 <= span.x) {
        return 1.0;
    }
    vec4 layer = csky_pc_heads_layer;
    vec4 k = csky_pc_heads_density;
    float dt = (t1 - span.x) / 12.0;
    float od = 0.0;
    for (int i = 0; i < 12; i++) {
        vec3 q = eye + dir * (span.x + dt * (float(i) + 0.5));
        float c = csky_pc_cover_shaped(q, v.x, v.y, layer.z, layer.w, csky_pc_heads_shape) * k.x;
        od += csky_pc_density(q, v.x, v.y, c, layer.z, k.y) * dt;
    }
    vec2 deck = csky_pc_slab_span(eye, dir, v.x, v.x + v.w);
    float d1 = min(deck.y, len);
    if (v.z > 0.0 && d1 > deck.x) {
        float ddt = (d1 - deck.x) / 3.0;
        for (int i = 0; i < 3; i++) {
            vec3 q = eye + dir * (deck.x + ddt * (float(i) + 0.5));
            od += csky_pc_deck_density(q, v.x, v.x + v.w, v.z, layer.z) * csky_pc_deck_gaps(q, csky_pc_ground.z, csky_pc_ground.w) * ddt;
        }
    }
    return exp(-od * k.z);
}

// A per-pixel jitter in [0, 1) off the screen position alone. The slab adds its own per-frame
// golden-ratio step on top, for TAA to average out.
float csky_pc_dither(vec2 frag_coord) {
    return fract(52.9829189 * fract(0.06711056 * frag_coord.x + 0.00583715 * frag_coord.y));
}

#endif
