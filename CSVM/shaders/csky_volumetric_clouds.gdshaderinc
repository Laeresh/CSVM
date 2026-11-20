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

// The bubbles of clear air the slab leaves round the zeppelins and the chapter's named structures (VolumetricClouds.FollowHulls): each
// one's centre and radius, a zero radius for an empty slot. Globals take no arrays, hence four.
global uniform vec4 csky_pc_bubble0;
global uniform vec4 csky_pc_bubble1;
global uniform vec4 csky_pc_bubble2;
global uniform vec4 csky_pc_bubble3;

// The share of the clouds' coverage one bubble leaves at `p`: none deep inside, all past its edge.
float csky_pc_bubble_keep(vec4 b, vec3 p) {
    return b.w > 0.0 ? smoothstep(b.w * 0.7, b.w, distance(p, b.xyz)) : 1.0;
}

// The share of the clouds' coverage every bubble leaves at `p`.
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
        od += csky_pc_heads_od_pw(q, dt, csky_pc_bubbles(q));
    }
    vec2 deck = csky_pc_slab_span(eye, dir, v.x, v.x + v.w);
    float d1 = min(deck.y, len);
    if (v.z > 0.0 && d1 > deck.x) {
        float ddt = (d1 - deck.x) / 3.0;
        for (int i = 0; i < 3; i++) {
            vec3 q = eye + dir * (deck.x + ddt * (float(i) + 0.5));
            // The deck without its detail erosion, the bubbles cut from its cover as the slab cuts them.
            float deck_d = csky_pc_deck_body_pw(q, v.z * csky_pc_bubbles(q), csky_pc_pw_deck.x) * csky_pc_deck_profile(q.y, v.x, v.x + v.w) * csky_pc_pw.w * csky_pc_pw_deck.y;
            od += deck_d * csky_pc_deck_gaps(q, csky_pc_ground.z, csky_pc_ground.w) * ddt;
        }
    }
    return exp(-od);
}

// Whether a view is the camera the reduced march traced, from that camera's eye, back axis and lens.
// TAA's jitter leaves the lens diagonal alone, and the eye test fails for a headset's eyes and every
// other camera. The slab's quad and the sprites over it both ask (EmitterRenderer.SlabMixComposite).
bool csky_pc_traced_view(vec3 eye, mat4 inv_view, mat4 inv_projection, vec3 traced_eye, vec3 traced_back, vec2 traced_lens) {
    if (distance(eye, traced_eye) > 0.01 || dot(inv_view[2].xyz, traced_back) < 0.99999) {
        return false;
    }
    vec2 lens = abs(vec2(inv_projection[0][0], inv_projection[1][1]));
    return all(lessThan(abs(lens - traced_lens), traced_lens * 0.001));
}

// Where the march along a ray through the layer's `span` ends: the exit, the scene or the cap.
float csky_pc_march_end(vec2 span, float scene, float far_m) {
    return min(min(span.y, scene), span.x + far_m);
}

// Whether a pixel shows the fog behind the slab: at or below the horizon, a sky pixel past the clipped
// sea shows the fog, not the dome's lower band.
bool csky_pc_fog_behind(float scene, vec2 span, vec3 dir) {
    return scene >= 1e8 && span.y < 0.0 && dir.y <= 0.02;
}

// The shafts' light gathered over share `f` of their ray, between the quarters the shafts' march keeps.
float csky_pc_shaft_at(vec4 quarters, float f) {
    float u = clamp(f, 0.0, 1.0) * 4.0;
    int k = min(int(u), 3);
    vec4 before = vec4(0.0, quarters.xyz);
    return mix(before[k], quarters[k], u - float(k));
}

// The shafts' light at this pixel's depth from the reduced march target's right half, at column `x` of
// its left half. Neighbouring texels start their steps at different dithers. Four half-texel taps
// average them, which TAA cannot: it rejects a thin beam's lone bright sample.
float csky_pc_reduced_shafts(sampler2D march, vec2 uv, float x, vec2 span, float scene, float shaft_far_m, float shaft_gain, float shaft_scale) {
    float l0 = min(span.y >= 0.0 ? span.x : shaft_far_m, shaft_far_m);
    if (l0 <= 1.0 || shaft_gain <= 0.0) {
        return 0.0;
    }
    vec2 px = 1.0 / vec2(textureSize(march, 0));
    vec2 c = vec2(0.5, 0.0) + clamp(vec2(x, uv.y) * shaft_scale, px * 2.0, vec2(0.5, 1.0) * shaft_scale - px * 2.0);
    vec4 quarters = 0.25 * (texture(march, c + px * vec2(0.5, 0.5)) + texture(march, c + px * vec2(-0.5, 0.5))
        + texture(march, c + px * vec2(0.5, -0.5)) + texture(march, c + px * vec2(-0.5, -0.5)));
    return csky_pc_shaft_at(quarters, min(scene, l0) / l0);
}

// The slab's lighting and fog need the atmosphere's globals, so only a shader that includes them first
// gets these. The slab and the sprites over it both call them, so a sprite composites against the
// cloud as the slab draws it.
#ifdef CSKY_ATMOSPHERE_INC

// The scene distance the slab works to: past the full fog nothing is seen, so the dome, which writes
// depth, reads as open sky.
float csky_pc_slab_scene(float raw, vec2 screen_uv, mat4 inv_projection) {
    float scene = csky_pc_scene_distance(raw, screen_uv, inv_projection);
    return scene > csky_fog_range.y ? 1e9 : scene;
}

// The lit cloud body from the march's two weights, the sky's and the sun's, before fog. Their colours
// are the same along a ray, so they apply once per pixel. The sun's phase is in w.y already.
// The mission's SUNLIGHT dims the body and the zone tune tints it.
vec3 csky_pc_cloud_light(vec2 w, vec3 cam, vec3 amb_top_rgb, float amb_fog_mix, float amb_gain, vec3 sun_rgb, float sun_gain) {
    vec3 amb = mix(amb_top_rgb, csky_fog_color_at(cam), amb_fog_mix) * amb_gain;
    vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * sun_gain;
    return (amb * w.x + sun * w.y) * csky_world_light_at(cam) * csky_tune_cloud_tint;
}

// A march cut off inside the layer at `t1` leaves the rest of it to the fog, not the dome, by the layer
// left unmarched. `m` is the lit cloud, premultiplied, over its transmittance. A ray leaving just past the cap takes little.
vec4 csky_pc_unmarched(vec4 m, vec3 eye, vec3 dir, vec2 span, float scene, float t1, float haze_near_m, float far_m) {
    float unmarched = min(span.y, scene) - t1;
    if (unmarched > 1.0) {
        float rest = max(csky_fog_amount(eye + dir * t1, eye), smoothstep(haze_near_m, far_m, t1))
            * (1.0 - exp(-unmarched / 5000.0));
        m.rgb += m.a * rest * csky_fog_color_at(eye);
        m.a *= 1.0 - rest;
    }
    return m;
}

// The shafts' colour from the light the shafts' march gathered along `dir`.
vec3 csky_pc_shaft_colour(float light, vec3 eye, vec3 dir, vec3 sun_rgb, float shaft_gain) {
    float phase = csky_pc_phase(dot(dir, csky_sun_dir), 0.5) * 4.0 * 3.14159265;
    vec3 sun = sun_rgb * max(csky_sun_light.y, 0.35) * csky_world_light_at(eye);
    return sun * light * shaft_gain * 0.00006 * phase;
}

// The lit cloud `rgb` of coverage `a`, fogged at its entry point as the slab writes it.
vec3 csky_pc_cloud_fogged(vec3 rgb, float a, vec3 entry, vec3 cam, float haze_near_m, float far_m, float cloud_fog) {
    float fog = max(csky_fog_amount(entry, cam), smoothstep(haze_near_m, far_m, distance(entry, cam))) * cloud_fog;
    return mix(rgb, csky_fog_color_at(cam) * a, fog);
}

#endif

// A per-pixel jitter in [0, 1) off the screen position alone. The slab adds its own per-frame
// golden-ratio step on top, for TAA to average out.
float csky_pc_dither(vec2 frag_coord) {
    return fract(52.9829189 * fract(0.06711056 * frag_coord.x + 0.00583715 * frag_coord.y));
}

#endif
