#ifndef CSKY_PROTO_CLOUDS_INC
#define CSKY_PROTO_CLOUDS_INC

// PROTOTYPE (Effects/ProtoClouds.cs, --proto-clouds): the density field, the sun march and the
// screen-ray helpers the three volumetric cloud variants share. Throwaway code answering one
// question, which raymarched cloud technique should replace the cloud deck and the cloud sprites
// under Enhanced. Nothing outside the prototype includes it.

#include "res://shaders/csky_cloud_shadow.gdshaderinc"

// Hash and value noise over a 3D lattice, in [0, 1). The lattice wraps at 256 m cells scaled by
// the caller, so the field repeats seamlessly at any drift.
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

// The cumulus column at a world point: the layer's coverage shaped into heads. Fuller coverage
// towers higher (the column's own top is its coverage share of the layer), a low-frequency lobe
// term breaks the heads apart, and the base stays flat.
float csky_pc_column_top(vec3 p, float cover, float detail_m) {
    float lobes = csky_pc_noise3(vec3(p.x, 0.0, p.z) / (detail_m * 5.0), 1911520717u);
    return clamp(cover * mix(0.55, 1.25, lobes), 0.0, 1.0);
}

// The vertical profile of a column between `bottom` and `top` whose own top is `column_top` of
// the layer: a hard flat base, full through the body, rounding off under the crown.
float csky_pc_profile(float y, float bottom, float top, float column_top) {
    float h = clamp((y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
    if (column_top <= 0.05) {
        return 0.0;
    }
    return smoothstep(0.0, 0.06, h) * (1.0 - smoothstep(column_top * 0.55, column_top, h));
}

// Density at a world point of a layer whose horizontal coverage is `cover` in [0, 1] (1 under a
// cloud's core). `detail_m` is the erosion cell in metres and `erosion` how deep the noise bites,
// lightly at the base and hard at the crown, so the bottom reads flat and the top billows.
float csky_pc_density(vec3 p, float bottom, float top, float cover, float detail_m, float erosion) {
    if (cover <= 0.0) {
        return 0.0;
    }
    float column_top = csky_pc_column_top(p, cover, detail_m);
    float prof = csky_pc_profile(p.y, bottom, top, column_top);
    if (prof <= 0.0) {
        return 0.0;
    }
    float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
    float n = csky_pc_fbm(p / detail_m);
    float bite = erosion * mix(0.3, 1.4, clamp(h / column_top, 0.0, 1.0));
    float d = min(cover, 1.0) * prof - bite * (1.0 - n);
    return clamp(d, 0.0, 1.0);
}

// The stratus deck under the heads: a sheet from `bottom` to `deck_top` covering `cover` of the
// sky, its holes where the broad noise falls under the uncovered share. Zero with no cover.
float csky_pc_deck_density(vec3 p, float bottom, float deck_top, float cover, float detail_m) {
    if (cover <= 0.0 || p.y < bottom || p.y > deck_top) {
        return 0.0;
    }
    float h = (p.y - bottom) / max(deck_top - bottom, 1.0);
    float prof = smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.55, 1.0, h));
    float n = csky_pc_fbm(p / (detail_m * 1.8));
    float body = clamp((n - (1.0 - cover)) / max(cover * 0.5, 0.05), 0.0, 1.0);
    return body * prof;
}

// Rifts torn in the deck: a broad noise opens `gaps` of the sheet in tears about `gap_m` across,
// with soft edges, so the sun reaches the ground below in patches and shafts. 1 with no gaps.
float csky_pc_deck_gaps(vec3 p, float gaps, float gap_m) {
    if (gaps <= 0.0) {
        return 1.0;
    }
    float broad = csky_pc_noise3(vec3(p.x, 0.0, p.z) / gap_m, 3266489917u);
    float fine = csky_pc_noise3(vec3(p.x, 11.0, p.z) / (gap_m * 0.37), 2246822507u);
    float n = broad * 0.8 + fine * 0.2;
    return smoothstep(gaps - 0.02, gaps + 0.04, n);
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

// The ray's entry and exit distances through an axis-aligned box, or a negative pair where it misses.
vec2 csky_pc_box_span(vec3 eye, vec3 dir, vec3 box_min, vec3 box_max) {
    vec3 inv = 1.0 / dir;
    vec3 t_a = (box_min - eye) * inv;
    vec3 t_b = (box_max - eye) * inv;
    vec3 t_min = min(t_a, t_b);
    vec3 t_max = max(t_a, t_b);
    float near = max(max(t_min.x, t_min.y), max(t_min.z, 0.0));
    float far = min(min(t_max.x, t_max.y), t_max.z);
    return far <= near ? vec2(-1.0, -1.0) : vec2(near, far);
}

// A per-pixel jitter in [0, 1) off the screen position alone, so the march start dithers without
// the clock: the prototype has no temporal filter to hide a moving pattern behind.
float csky_pc_dither(vec2 frag_coord) {
    return fract(52.9829189 * fract(0.06711056 * frag_coord.x + 0.00583715 * frag_coord.y));
}

#endif
