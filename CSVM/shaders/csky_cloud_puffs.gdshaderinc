#ifndef CSKY_CLOUD_PUFFS_INC
#define CSKY_CLOUD_PUFFS_INC

// Enhanced Graphics only: the rendered cloud puff pool a cloud card draws in place of its authored
// mask, and the pose each card takes off a hash of its own world position (Mech3/CloudPuffs.cs).
// The pose reads no stream and no instance index, so a card's choice is a fixed function of where
// the data placed it. Include after csky_mip_bias.gdshaderinc.
uniform sampler2DArray puff_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform float puff_layers = 1.0;
// The largest in-plane turn, in degrees either way, and the size range, as factors on the card.
uniform float puff_turn_deg = 0.0;
uniform vec2 puff_size = vec2(1.0, 1.0);
// The authored mask's own colour (sRGB) and peak opacity: the puff carries shape and shading only.
uniform vec4 puff_tint = vec4(1.0);
// The layer and (cos, sin, mirror) turn vertex() writes from csky_puff_pose for fragment().
varying flat float v_puff_layer;
varying flat vec3 v_puff_pose;
// The card's upright UV, csky_puff_card_uv of each corner, interpolated across the quad.
varying vec2 v_puff_card_uv;

uint csky_puff_mix(uint h) {
    h ^= h >> 16u;
    h *= 0x7feb352du;
    h ^= h >> 15u;
    h *= 0x846ca68bu;
    h ^= h >> 16u;
    return h;
}

// Writes the card's layer and its (cos, sin, mirror) turn, and returns its size factor.
float csky_puff_pose(vec3 origin, out float layer, out vec3 pose) {
    uint h = csky_puff_mix(floatBitsToUint(origin.x) * 0x8da6b343u
        ^ floatBitsToUint(origin.y) * 0xd8163841u ^ floatBitsToUint(origin.z) * 0xcb1ab31fu);
    layer = float(h % uint(max(puff_layers, 1.0)));
    uint h2 = csky_puff_mix(h + 0x9e3779b9u);
    float turn = radians(puff_turn_deg) * (float(h2 & 0xffffu) / 65535.0 * 2.0 - 1.0);
    float mirror = (h2 & 0x10000u) != 0u ? -1.0 : 1.0;
    pose = vec3(cos(turn), sin(turn), mirror);
    return mix(puff_size.x, puff_size.y, float(h2 >> 17u) / 32767.0);
}

// A corner's UV off its own place on the recentred card, the image's top on the card's +Y.
// ⚠ Do not sample a puff through the authored UV: most shipped cloud cards map the mask turned a
// quarter and transposed, which a shapeless mask hides and a shaped puff turns on its side.
vec2 csky_puff_card_uv(vec3 local) {
    return vec2(local.x >= 0.0 ? 1.0 : 0.0, local.y >= 0.0 ? 0.0 : 1.0);
}

// The card's UV turned and mirrored about its centre, where the pose puts the puff.
vec2 csky_puff_uv(vec2 uv, vec3 pose) {
    vec2 d = (uv - 0.5) * vec2(pose.z, 1.0);
    return vec2(pose.x * d.x - pose.y * d.y, pose.y * d.x + pose.x * d.y) + 0.5;
}

// The posed puff, tinted by the authored mask, at the chapter's mip bias. Fragment stage only.
vec4 csky_puff_sample(vec2 uv, vec3 pose, float layer) {
    vec4 puff = texture(puff_tex, vec3(csky_puff_uv(uv, pose), layer), csky_mip_bias);
    return puff * vec4(csky_srgb_to_linear(puff_tint.rgb), puff_tint.a);
}

#endif
