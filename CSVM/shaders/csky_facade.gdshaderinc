#ifndef CSKY_FACADE_INC
#define CSKY_FACADE_INC

// The basis a SphericalY facade is turned by: the rotation that carries the card's authored +Z
// onto the direction from the card to the eye and adds no twist about it. The eye's own basis
// never enters, so rolling the aircraft leaves every card exactly where it was, which is the
// property the original has and a camera-basis billboard does not.
// The original steps a per-instance quaternion by the shortest arc from the card's PREVIOUS
// facing each frame; this is that arc taken from the authored facing in one go, the same pose on
// the first frame and afterwards apart from the twist a looping camera path transports.
// Decode, with addresses: docs/org/cloudCards.md.
mat3 csky_facade_spherical(vec3 origin, vec3 eye) {
    vec3 f = eye - origin;
    float len2 = dot(f, f);
    f = len2 > 0.0 ? f * inversesqrt(len2) : vec3(0.0, 0.0, 1.0);
    float d = 1.0 + f.z;
    // The eye exactly behind the authored facing: no shortest arc exists, and the original picks
    // an arbitrary perpendicular and turns a half circle about it, which for a +Z facing is x.
    if (d < 1e-6) {
        return mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, -1.0));
    }
    float k = 1.0 / d;
    return mat3(
        vec3(1.0 - (k * f.x * f.x), -(k * f.x * f.y), -f.x),
        vec3(-(k * f.x * f.y), 1.0 - (k * f.y * f.y), -f.y),
        f);
}

#endif
