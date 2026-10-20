#ifndef CSKY_FOG_VOLUME_INC
#define CSKY_FOG_VOLUME_INC

// Enhanced Graphics only: an armed fog volume (C5's street strips) seen from outside greys what lies
// inside it into the volume zone's own fog, over the stretch of the view ray that runs inside the
// volume. WeatherRig.PublishVolumeFog fills the texture: row 0 holds the volume zone's linear fog
// colour, then its range and altitude; row v + 1 holds volume v's outward face planes (normal, D),
// padded with planes that never bind. Rule: docs/org/weather.md, the volume approach.
global uniform sampler2D csky_fog_volume_data;
global uniform int csky_fog_volume_count;

// The most face planes a volume carries (C5's busiest strip has 14). WeatherRig asserts it.
const int CSKY_FOG_VOLUME_MAX_FACES = 16;

// TUNE, judged at the controls. How deep inside its volume a view ray must pass before it wears the
// zone's whole fog; nearer a face the fog thins to none, so no edge of a volume reads as a cut.
// Rule: docs/org/weather.md, the volume approach.
const float CSKY_FOG_VOLUME_SOFT_METRES = 60.0;

// TUNE, with the metres above: further out the depth grows with the distance by this slope, so a
// far face fades over about the same arc of the view as a near one rather than over a pixel row.
const float CSKY_FOG_VOLUME_SOFT_SLOPE = 0.08;

// Samples along the clipped ray its deepest point is sought over.
const int CSKY_FOG_VOLUME_STEPS = 8;

// The volume zone's linear fog colour.
vec3 csky_fog_volume_color() {
    return texelFetch(csky_fog_volume_data, ivec2(0, 0), 0).rgb;
}

// How much of the volume zone's colour to lay over the pixel at eye + t_end * ray, which already
// wears the view's own fog. The zone's fog runs over the horizontal metres of the ray inside the
// volume, thinned by how deep the ray passes inside, and the view's share is taken off as an alpha
// over the fogged pixel. The volume is convex, so the stretch is the latest entry and earliest exit
// over its face planes. A camera inside a volume never draws this (WeatherRig.VolumeBody).
float csky_fog_volume_over(int row, vec3 eye, vec3 ray, float t_end) {
    if (row < 1 || row > csky_fog_volume_count) {
        return 0.0;
    }
    vec4 planes[CSKY_FOG_VOLUME_MAX_FACES];
    int faces = min(textureSize(csky_fog_volume_data, 0).x, CSKY_FOG_VOLUME_MAX_FACES);
    vec2 t = vec2(0.0, t_end);
    for (int i = 0; i < faces; i++) {
        planes[i] = texelFetch(csky_fog_volume_data, ivec2(i, row), 0);
        float outside = dot(planes[i].xyz, eye) - planes[i].w;
        float rate = dot(planes[i].xyz, ray);
        if (rate < 0.0) {
            t.x = max(t.x, -outside / rate);
        } else if (rate > 0.0) {
            t.y = min(t.y, -outside / rate);
        } else if (outside > 0.0) {
            t.y = -1.0;
        }
    }
    if (t.y <= t.x) {
        return 0.0;
    }
    float deepest = 0.0;
    for (int s = 0; s < CSKY_FOG_VOLUME_STEPS; s++) {
        float ts = mix(t.x, t.y, (float(s) + 0.5) / float(CSKY_FOG_VOLUME_STEPS));
        vec3 at = eye + ts * ray;
        float soft = max(CSKY_FOG_VOLUME_SOFT_METRES, CSKY_FOG_VOLUME_SOFT_SLOPE * ts * length(ray));
        float depth = soft;
        for (int i = 0; i < faces; i++) {
            depth = min(depth, planes[i].w - dot(planes[i].xyz, at));
        }
        deepest = max(deepest, depth / soft);
    }
    vec3 end = eye + t.y * ray;
    vec4 band = texelFetch(csky_fog_volume_data, ivec2(1, 0), 0);
    float inside = (t.y - t.x) * length(ray.xz);
    float span = max(band.y - band.x, 0.001);
    float amt = clamp((inside - band.x) / span, 0.0, 1.0) * (1.0 - smoothstep(band.z, band.w, end.y))
        * smoothstep(0.0, 1.0, deepest);
    float view = csky_fog_amount(end, eye);
    return amt > view ? (amt - view) / max(1.0 - view, 1e-4) : 0.0;
}

#endif
