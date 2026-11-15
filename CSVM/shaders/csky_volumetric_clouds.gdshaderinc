#ifndef CSKY_VOLUMETRIC_CLOUDS_INC
#define CSKY_VOLUMETRIC_CLOUDS_INC

// The volumetric cloud slab's helpers (Session/World/VolumetricClouds.cs): the phase term, the
// screen-ray helpers its full-screen march reads, the zeppelins' bubbles and the veil. The slab's
// shader includes it, and so do the effects drawn after the slab, for csky_pc_veil_at.

#include "res://shaders/csky_cloud_shadow.gdshaderinc"

// The noise, the rifts, the head coverage and the head density live in csky_cloud_shadow, whose
// ground shadow marches the same field.

// Henyey-Greenstein phase: forward scattering lights a cloud's rim when the sun stands behind it.
float csky_pc_phase(float cos_theta, float g) {
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.14159265 * pow(1.0 + g2 - 2.0 * g * cos_theta, 1.5));
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

// The bubbles of clear air the slab leaves round the zeppelins (VolumetricClouds.FollowHulls): each
// one's centre and radius, a zero radius for an empty slot. Globals take no arrays, hence four.
global uniform vec4 csky_pc_bubble0;
global uniform vec4 csky_pc_bubble1;
global uniform vec4 csky_pc_bubble2;
global uniform vec4 csky_pc_bubble3;

// The share of the field one bubble leaves at `p`: none deep inside, all past its edge.
float csky_pc_bubble_keep(vec4 b, vec3 p) {
    return b.w > 0.0 ? smoothstep(b.w * 0.7, b.w, distance(p, b.xyz)) : 1.0;
}

// The share of the cloud field every bubble leaves at `p`.
float csky_pc_bubbles(vec3 p) {
    return csky_pc_bubble_keep(csky_pc_bubble0, p) * csky_pc_bubble_keep(csky_pc_bubble1, p)
        * csky_pc_bubble_keep(csky_pc_bubble2, p) * csky_pc_bubble_keep(csky_pc_bubble3, p);
}

// The slab's layer as the effects drawn after it read it (VolumetricClouds.PublishGround): its floor
// and top, and the deck's cover and depth. All zero while no slab draws.
global uniform vec4 csky_pc_veil;

// The share of an effect at `p` the slab lets through toward `eye`: 1 with no slab, or no layer
// between them. It marches the slab's density over the ground shadow's globals: the heads in twelve
// steps, and the deck in three over its own depth, which the heads' steps would step over. The
// zeppelins' bubbles hold no cloud here either, so a hit on a hull in its bubble shows undimmed.
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
    // Both densities are per metre already, so the optical depth takes no density gain.
    float dt = (t1 - span.x) / 12.0;
    float od = 0.0;
    for (int i = 0; i < 12; i++) {
        vec3 q = eye + dir * (span.x + dt * (float(i) + 0.5));
        od += csky_pc_heads_od_pw(q, dt) * csky_pc_bubbles(q);
    }
    vec2 deck = csky_pc_slab_span(eye, dir, v.x, v.x + v.w);
    float d1 = min(deck.y, len);
    if (v.z > 0.0 && d1 > deck.x) {
        float ddt = (d1 - deck.x) / 3.0;
        for (int i = 0; i < 3; i++) {
            vec3 q = eye + dir * (deck.x + ddt * (float(i) + 0.5));
            // The deck without its detail erosion.
            float deck_d = csky_pc_deck_body_pw(q, v.z, csky_pc_pw_deck.x) * csky_pc_deck_profile(q.y, v.x, v.x + v.w) * csky_pc_pw.w * csky_pc_pw_deck.y;
            od += deck_d * csky_pc_deck_gaps(q, csky_pc_ground.z, csky_pc_ground.w) * csky_pc_bubbles(q) * ddt;
        }
    }
    return exp(-od);
}

// A per-pixel jitter in [0, 1) off the screen position alone. The slab adds its own per-frame
// golden-ratio step on top, for TAA to average out.
float csky_pc_dither(vec2 frag_coord) {
    return fract(52.9829189 * fract(0.06711056 * frag_coord.x + 0.00583715 * frag_coord.y));
}

#endif
