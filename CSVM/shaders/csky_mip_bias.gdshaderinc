#ifndef CSKY_MIP_BIAS_INC
#define CSKY_MIP_BIAS_INC

// The chapter's authored mip LOD bias, read off adjust.gw's MipBias command. The original sets it
// as D3DRENDERSTATE_MIPMAPLODBIAS, ONE device render state that every texture sample on the device
// takes, so it is one global here and every mip-mapped arm fetches through the function below.
// 0 is the device default, which is what a chapter authoring no MipBias renders at; of the shipped
// chapters only C5 authors one. Decode: docs/org/textures.md.
global uniform float csky_mip_bias = 0.0;

// The biased albedo fetch every mip-mapped world arm takes: the world mesh, the camera-facing
// billboards, the cylindrical facades and the templates clutter. Negative sharpens (a
// higher-resolution level is chosen sooner), positive blurs.
// Fragment stage only: GLSL accepts an explicit bias on texture() nowhere else.
vec4 csky_sample_albedo(sampler2D tex, vec2 uv) {
    return texture(tex, uv, csky_mip_bias);
}

#endif
