#ifndef CSKY_CLOUD_SHADOW_INC
#define CSKY_CLOUD_SHADOW_INC

// The Enhanced cloud shadows (Effects/CloudShadow.cs owns all five globals, the tint, ambient and band
// ones declared below). The vec4 is the pattern's drift offset in metres (X, Z), one over the lattice cell
// in metres, and the darkness at a shadow's core; a darkness of 0 draws nothing. The vec2 is the noise
// band over which a shadow's edge ramps. The tunes behind them live in Effects/CloudShadowPattern.cs.
global uniform vec4 csky_cloud_shadow;
global uniform vec2 csky_cloud_shadow_edge;

// A lattice corner's value in [0, 1). The cell wraps at `period`, a power of two, so the pattern
// repeats exactly and the drift offset can wrap with it.
float csky_cloud_hash(ivec2 cell, int period, uint seed) {
    uvec2 c = uvec2(cell & ivec2(period - 1));
    uint h = (c.x * 2376512323u) ^ (c.y * 3625334849u) ^ seed;
    h ^= h >> 16u;
    h *= 2146121005u;
    h ^= h >> 15u;
    h *= 2221713035u;
    h ^= h >> 16u;
    return float(h >> 8u) * (1.0 / 16777216.0);
}

// Smooth value noise over one lattice, in [0, 1).
float csky_cloud_octave(vec2 p, int period, uint seed) {
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 c = ivec2(i);
    float a = csky_cloud_hash(c, period, seed);
    float b = csky_cloud_hash(c + ivec2(1, 0), period, seed);
    float d = csky_cloud_hash(c + ivec2(0, 1), period, seed);
    float e = csky_cloud_hash(c + ivec2(1, 1), period, seed);
    return mix(mix(a, b, u.x), mix(d, e, u.x), u.y);
}

// The cloud lab's tint (UI/Labs/CloudLab.cs): the light added under a shadow's core so the pattern
// and its drift show plainly. Black, the default, adds nothing.
global uniform vec3 csky_cloud_shadow_tint;

// The share of the ambient light a shadow's core takes away, written through AO. 0 takes none.
global uniform float csky_cloud_shadow_ambient;

// The pattern's raw noise at a world position, in [0, 1), before the edge band.
float csky_cloud_noise(vec3 world) {
    vec2 p = (world.xz + csky_cloud_shadow.xy) * csky_cloud_shadow.z;
    return (2.0 * csky_cloud_octave(p, 256, 2654435769u) + csky_cloud_octave(p * 2.0, 512, 2246822507u)) / 3.0;
}

// How far a world position sits inside a shadow: 0 in the clear, 1 under its core.
float csky_cloud_cover(vec3 world) {
    return smoothstep(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, csky_cloud_noise(world));
}

// The volumetric slab's shadow on the ground (Session/World/VolumetricClouds.cs). The
// vec4 is the deck's floor in metres, the sun share it takes (0 = clouds off), and its rifts' share
// and size; the second is the direction toward the sun and the heads' altitude the pattern is read at.
global uniform vec4 csky_pc_ground;
global uniform vec4 csky_pc_ground_sun;

// Hash and value noise over a 3D lattice, in [0, 1), the volumetric clouds'. The lattice wraps at 256 cells.
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

// Rifts torn in the deck: a broad noise opens `gaps` of the sheet in tears about `gap_m` across,
// with soft edges. The share of the deck kept: 1 under the sheet or with no gaps, 0 in a rift.
float csky_pc_deck_gaps(vec3 p, float gaps, float gap_m) {
    if (gaps <= 0.0) {
        return 1.0;
    }
    float broad = csky_pc_noise3(vec3(p.x, 0.0, p.z) / gap_m, 3266489917u);
    float fine = csky_pc_noise3(vec3(p.x, 11.0, p.z) / (gap_m * 0.37), 2246822507u);
    float n = broad * 0.8 + fine * 0.2;
    return smoothstep(gaps - 0.02, gaps + 0.04, n);
}

// Where the sun ray from `world` crosses altitude `y`; `world` itself with no volumetric clouds (a heads
// altitude of 0) or a low sun.
vec3 csky_pc_along_sun(vec3 world, float y) {
    vec3 s = csky_pc_ground_sun.xyz;
    if (csky_pc_ground_sun.w <= 0.0 || s.y < 0.05 || world.y >= y) {
        return world;
    }
    return world + s * ((y - world.y) / s.y);
}

// The slab's heads for the ground shadow (VolumetricClouds.PublishGround): the shape vec4 of
// csky_pc_cover_lat_gap; the layer's floor and top, erosion cell and coverage bias; the heads' gain, a
// free slot, the density gain, and the ambient share the deck takes (read by csky_cloud_ao).
global uniform vec4 csky_pc_heads_shape;
global uniform vec4 csky_pc_heads_layer;
global uniform vec4 csky_pc_heads_density;

// The slab's baked-noise density for the ground shadow and the veil (VolumetricClouds.PublishGround).
// x is the shape texture's tile in metres, y the coverage cap, z the bases' rag, w the density per metre.
global uniform vec4 csky_pc_pw;
// The baked-noise deck's shape tile in metres, and its density over the heads'.
// z and w are the heads' edge_cut and edge_k, so a shadow ends where its cloud does.
global uniform vec4 csky_pc_pw_deck;
// The hints matter: the lattices and the noise tile, so they must repeat in a canvas shader too.
global uniform sampler3D csky_pc_shape_tex : filter_linear, repeat_enable;
global uniform sampler3D csky_pc_warp_lat : filter_linear, repeat_enable;
global uniform sampler2D csky_pc_tops_lat : filter_linear, repeat_enable;

// csky_pc_noise3 off a lattice texture of its corners (CloudNoise.WarpLattice, TopsLattice). A shader
// that defines CSKY_PC_LAT_EXACT before this include fetches the corners and mixes them itself: the
// sampler's filter weighs in 1/256 steps, which terraces a 720 m cell every few metres on a cloud's
// face. The ground shadow keeps the one filtered read: its soft edge shows no terrace, and the exact
// read costs it eight fetches a lookup.
#ifdef CSKY_PC_LAT_EXACT
vec2 csky_pc_lat3(vec3 p) {
    vec3 i = floor(p);
    vec3 f = p - i;
    vec3 u = f * f * (3.0 - 2.0 * f);
    ivec3 a = ivec3(i) & ivec3(255);
    ivec3 b = (a + ivec3(1)) & ivec3(255);
    vec2 x00 = mix(texelFetch(csky_pc_warp_lat, ivec3(a.x, a.y, a.z), 0).rg, texelFetch(csky_pc_warp_lat, ivec3(b.x, a.y, a.z), 0).rg, u.x);
    vec2 x10 = mix(texelFetch(csky_pc_warp_lat, ivec3(a.x, b.y, a.z), 0).rg, texelFetch(csky_pc_warp_lat, ivec3(b.x, b.y, a.z), 0).rg, u.x);
    vec2 x01 = mix(texelFetch(csky_pc_warp_lat, ivec3(a.x, a.y, b.z), 0).rg, texelFetch(csky_pc_warp_lat, ivec3(b.x, a.y, b.z), 0).rg, u.x);
    vec2 x11 = mix(texelFetch(csky_pc_warp_lat, ivec3(a.x, b.y, b.z), 0).rg, texelFetch(csky_pc_warp_lat, ivec3(b.x, b.y, b.z), 0).rg, u.x);
    return mix(mix(x00, x10, u.y), mix(x01, x11, u.y), u.z);
}

vec2 csky_pc_lat2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = p - i;
    vec2 u = f * f * (3.0 - 2.0 * f);
    ivec2 a = ivec2(i) & ivec2(255);
    ivec2 b = (a + ivec2(1)) & ivec2(255);
    vec2 x0 = mix(texelFetch(csky_pc_tops_lat, ivec2(a.x, a.y), 0).rg, texelFetch(csky_pc_tops_lat, ivec2(b.x, a.y), 0).rg, u.x);
    vec2 x1 = mix(texelFetch(csky_pc_tops_lat, ivec2(a.x, b.y), 0).rg, texelFetch(csky_pc_tops_lat, ivec2(b.x, b.y), 0).rg, u.x);
    return mix(x0, x1, u.y);
}
#else
vec2 csky_pc_lat3(vec3 p) {
    vec3 i = floor(p);
    vec3 f = p - i;
    return texture(csky_pc_warp_lat, (i + 0.5 + f * f * (3.0 - 2.0 * f)) / 256.0).rg;
}

vec2 csky_pc_lat2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = p - i;
    return texture(csky_pc_tops_lat, (i + 0.5 + f * f * (3.0 - 2.0 * f)) / 256.0).rg;
}
#endif

// The slab's head coverage at `p`, which the slab and its ground shadow both read. Each cloud has
// its own top, a low noise a few clouds across, from 30 % of the layer to its top. Up to it the noise
// threshold dips a little through the body, so the cloud bulges, then climbs by shape.x toward the
// top, rounding it into a dome, over a band shape.y wide; past the top nothing stands. The point
// billows by shape.z metres and leans shape.w downwind at the layer's top. `gap` is how far the
// pattern's noise stands under the coverage's lower edge, positive in clear air, which the slab's
// march strides by.
float csky_pc_cover_lat_gap(vec3 p, float bottom, float top, float detail_m, float cover_bias, vec4 shape, out float gap) {
    float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
    vec3 w = p / (detail_m * 4.0);
    vec2 warp = vec2(csky_pc_lat3(w).r, csky_pc_lat3(w + 17.0).g) - 0.5;
    vec3 q = p + vec3(warp.x * shape.z * (0.4 + h) + shape.w * h, 0.0, warp.y * shape.z * (0.4 + h));
    float broad = csky_pc_lat2(q.xz / (detail_m * 11.0)).r;
    float fine = csky_pc_lat2(q.xz / (detail_m * 4.0)).g;
    float r = h / mix(0.3, 1.0, smoothstep(0.25, 0.85, broad * 0.75 + fine * 0.25));
    float rc = clamp(r, 0.0, 1.0);
    float rise = shape.x * pow(rc, 2.5) - shape.x * 0.25 * sin(3.14159265 * rc) + smoothstep(0.85, 1.0, r) * 0.6;
    float lo = mix(csky_cloud_shadow_edge.x, csky_cloud_shadow_edge.y, 0.5) - cover_bias * 0.25 + rise;
    float x = csky_cloud_noise(q);
    gap = lo - shape.y * 0.5 - x;
    return smoothstep(lo - shape.y * 0.5, lo + shape.y * 0.5, x);
}

float csky_pc_cover_lat(vec3 p, float bottom, float top, float detail_m, float cover_bias, vec4 shape) {
    float gap;
    return csky_pc_cover_lat_gap(p, bottom, top, detail_m, cover_bias, shape, gap);
}

// The baked-noise heads' extinction per metre at `p` under coverage `c`, without the detail erosion.
float csky_pc_density_pw(vec3 p, float bottom, float top, float c) {
    if (c <= 0.0) {
        return 0.0;
    }
    float h = clamp((p.y - bottom) / max(top - bottom, 1.0), 0.0, 1.0);
    vec4 s = texture(csky_pc_shape_tex, (p + vec3(csky_cloud_shadow.x, 0.0, csky_cloud_shadow.y)) / csky_pc_pw.x);
    float wfbm = s.g * 0.625 + s.b * 0.25 + s.a * 0.125;
    float base = clamp((s.r - (wfbm - 1.0)) / max(2.0 - wfbm, 1e-4), 0.0, 1.0);
    float body = base * smoothstep(0.0, 0.04 + csky_pc_pw.z * (1.0 - s.g), h);
    float d = clamp((body - (1.0 - c)) / max(c, 1e-4), 0.0, 1.0);
    return clamp((d - csky_pc_pw_deck.z) * csky_pc_pw_deck.w, 0.0, 1.0) * csky_pc_pw.w;
}

// The heads' optical depth over `len` metres at `p`, under the share `keep` of their coverage (1 for
// a shadow; the veil passes what the zeppelins' bubbles leave).
float csky_pc_heads_od_pw(vec3 p, float len, float keep) {
    vec4 layer = csky_pc_heads_layer;
    float c = clamp(csky_pc_cover_lat(p, layer.x, layer.y, layer.z, layer.w, csky_pc_heads_shape) * csky_pc_heads_density.x, 0.0, 1.0) * csky_pc_pw.y * keep;
    return csky_pc_density_pw(p, layer.x, layer.y, c) * len;
}

// The baked-noise deck's body at `p` over `cover` of the sky, before its profile and erosion: the
// Perlin-Worley shape on a `tile_m` tile, so its underside lumps like stratocumulus.
float csky_pc_deck_body_pw(vec3 p, float cover, float tile_m) {
    vec4 s = texture(csky_pc_shape_tex,(p + vec3(csky_cloud_shadow.x, 0.0, csky_cloud_shadow.y)) / tile_m);
    float n = s.r * 0.6 + (s.g * 0.625 + s.b * 0.25 + s.a * 0.125) * 0.4;
    return clamp((n - (1.0 - cover)) / max(cover * 0.5, 0.05), 0.0, 1.0);
}

// The deck's sheet from `bottom` to `deck_top`: thin at the floor, gone by the top.
float csky_pc_deck_profile(float y, float bottom, float deck_top) {
    float h = (y - bottom) / max(deck_top - bottom, 1.0);
    return smoothstep(0.0, 0.12, h) * (1.0 - smoothstep(0.55, 1.0, h));
}

// The heads' cover over `world`. Under the volumetric clouds it is the light the slab's own density takes
// along the sun ray, four taps through the layer, so every head shades by its thickness.
float csky_cloud_cover_lit(vec3 world) {
    if (csky_pc_ground_sun.w <= 0.0) {
        return csky_cloud_cover(world);
    }
    vec3 s = csky_pc_ground_sun.xyz;
    vec4 layer = csky_pc_heads_layer;
    if (s.y < 0.05) {
        return 0.0;
    }
    float t0 = max(layer.x - world.y, 0.0) / s.y;
    float t1 = max(layer.y - world.y, 0.0) / s.y;
    float dt = (t1 - t0) * 0.25;
    float od = 0.0;
    for (int i = 0; i < 4; i++) {
        od += csky_pc_heads_od_pw(world + s * (t0 + dt * (float(i) + 0.5)), dt, 1.0);
    }
    return 1.0 - exp(-od);
}

// The sun share the volumetric deck lets through at `world`: 1 in a rift or with no deck.
float csky_pc_deck_sun(vec3 world) {
    if (csky_pc_ground.y <= 0.0) {
        return 1.0;
    }
    vec3 at = csky_pc_along_sun(world, csky_pc_ground.x);
    // csky_pc_deck_gaps is the share of the deck kept, 1 under the sheet and 0 in a rift.
    return 1.0 - csky_pc_ground.y * csky_pc_deck_gaps(at, csky_pc_ground.z, csky_pc_ground.w);
}

// The share of the sun the clouds let through at a world position: 1 in the clear, down to one less
// the darkness under a shadow's core. It is the one function every sun term reads the clouds through
// (csky_terrain_sun), over the globals one C# owner writes.
float csky_cloud_sun(vec3 world) {
    float deck = csky_pc_deck_sun(world);
    if (csky_cloud_shadow.w <= 0.0) {
        return deck;
    }
    return (1.0 - csky_cloud_shadow.w * csky_cloud_cover_lit(world)) * deck;
}

// The mission's cloud band in metres (bottom, top), (0, 0) with none (Effects/CloudShadow.cs).
global uniform vec2 csky_cloud_shadow_band;

// The clouds' share of the sun on an aircraft at `world`. The flat pattern and the deck fade to clear
// through the band, so a plane over it flies in the clear; with no band they shade it at any altitude.
// The volumetric heads keep their own march, which starts at the plane's height and reaches above the band.
float csky_cloud_sun_aloft(vec3 world) {
    vec2 band = csky_cloud_shadow_band;
    float above = band.y > band.x ? smoothstep(band.x, band.y, world.y) : 0.0;
    float deck = mix(csky_pc_deck_sun(world), 1.0, above);
    if (csky_cloud_shadow.w <= 0.0) {
        return deck;
    }
    float heads = 1.0 - csky_cloud_shadow.w * csky_cloud_cover_lit(world);
    return mix(heads, 1.0, csky_pc_ground_sun.w > 0.0 ? 0.0 : above) * deck;
}

// The ambient light's share left at a world position, for a fragment's AO: 1 in the clear or with
// no shadow drawn. Godot applies AO to the ambient light alone, so the sun keeps csky_cloud_sun.
// Under the volumetric deck the sky light dims too, so a rift's patch reads lit on matte ground and water.
float csky_cloud_ao(vec3 world) {
    float deck = 1.0 - csky_pc_heads_density.w * (1.0 - csky_pc_deck_sun(world));
    if (csky_cloud_shadow_ambient <= 0.0) {
        return deck;
    }
    return (1.0 - csky_cloud_shadow_ambient * csky_cloud_cover_lit(world)) * deck;
}

// The lab tint's light at a world position, black with the tint off. The heads' shadow takes the
// tint's own colour and the volumetric deck's shadow the same strength in blue.
vec3 csky_cloud_tint(vec3 world) {
    if (csky_cloud_shadow_tint == vec3(0.0)) {
        return vec3(0.0);
    }
    vec3 heads = csky_cloud_shadow.w > 0.0 ? csky_cloud_shadow_tint * csky_cloud_cover_lit(world) : vec3(0.0);
    float strength = max(csky_cloud_shadow_tint.r, max(csky_cloud_shadow_tint.g, csky_cloud_shadow_tint.b));
    return heads + vec3(0.0, 0.0, strength) * (1.0 - csky_pc_deck_sun(world));
}

// The gain on an unshaded surface's colour at a world position (the vegetation), lit as by `sun_pair`
// (csky_sun_light_at) on a half-lit normal: the darkness takes the diffuse part, the ambient share the rest.
vec3 csky_cloud_unlit(vec3 world, vec2 sun_pair) {
    float diffuse = 0.5 * sun_pair.y;
    float deck = 1.0 - (1.0 - csky_pc_deck_sun(world)) * diffuse / max(sun_pair.x + diffuse, 1e-3);
    if (csky_cloud_shadow.w <= 0.0) {
        return vec3(deck);
    }
    float taken = (csky_cloud_shadow.w * diffuse + csky_cloud_shadow_ambient * sun_pair.x) / max(sun_pair.x + diffuse, 1e-3);
    return vec3((1.0 - taken * csky_cloud_cover_lit(world)) * deck) + csky_cloud_tint(world);
}

#endif
