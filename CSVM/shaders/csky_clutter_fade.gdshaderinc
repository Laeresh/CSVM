#ifndef CSKY_CLUTTER_FADE_INC
#define CSKY_CLUTTER_FADE_INC

// The templates-clutter distance fade: the original's per-node near/far test under its one global
// scale. Each MultiMesh instance carries (near², far², 1/(far² − near²), 0) as custom data, all
// zero for a decoration that never fades. The global is the graphics EffectsLevel's squared scale
// (1/4/9 for HIGH/MEDIUM/LOW, CSVM.Utils.EffectsLevel), multiplied into the SQUARED camera
// distance before the compare, so a lower level fades and culls at 1/2 and 1/3 of the authored
// metres. Decode: docs/formats/templates.md.
// A scale of 0 is the remake's own "never fades" setting (graphics.clutterFarFade): the scaled
// distance never reaches any near², so every stamp keeps full alpha and its full card.
global uniform float csky_clutter_fade_scale_sq = 1.0;

// One instance's opacity: 1 inside near, 0 at or beyond far, a ramp LINEAR IN SQUARED DISTANCE
// between, which is the original's own curve rather than a smoothstep. far² of 0 never fades.
float csky_clutter_fade_alpha(vec3 origin, vec3 camera, vec4 fade) {
    if (fade.y <= 0.0) {
        return 1.0;
    }
    vec3 d = origin - camera;
    float d2 = csky_clutter_fade_scale_sq * dot(d, d);
    if (d2 <= fade.x) {
        return 1.0;
    }
    if (d2 >= fade.y) {
        return 0.0;
    }
    return (fade.y - d2) * fade.z;
}

// Whether a fragment survives an ordered 4x4 dither at `alpha`. The original alpha-blends the
// ramp; dithering keeps the world's densest draw in the opaque/cutout pass instead of moving
// it into the transparent one. 0.996 is the original's own "fully opaque" cut.
bool csky_clutter_dither_keep(vec2 fragcoord, float alpha) {
    if (alpha >= 0.996) {
        return true;
    }
    int x = int(fragcoord.x) & 3;
    int y = int(fragcoord.y) & 3;
    int v = (((x ^ y) & 1) << 3) | ((y & 1) << 2) | ((x ^ y) & 2) | ((y & 2) >> 1);
    return alpha > (float(v) + 0.5) / 16.0;
}

#endif
