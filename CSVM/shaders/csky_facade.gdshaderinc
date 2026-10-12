#ifndef CSKY_FACADE_INC
#define CSKY_FACADE_INC

// The basis a SphericalY facade is turned by: the card's +Z points at the eye from any direction
// and its +Y is the world's up projected off that line, so the card tilts toward the eye and holds
// its roll in the world. The eye's own basis never enters, so rolling the aircraft leaves every
// card standing, and the up hint is constant, so translating past a card leaves it standing too.
// The original steps a per-instance quaternion by the shortest arc from the card's PREVIOUS facing
// each frame, which a shader cannot accumulate; each twist-free step keeps the roll a card started
// with, so away from the pole this is the pose that tracker settles into. Taking the arc from a
// fixed axis instead is not equivalent: its twist depends on where the eye stands and reads as a
// roll under sideways motion. Decode, with addresses: docs/org/cloudCards.md.
mat3 csky_facade_spherical(vec3 origin, vec3 eye) {
    vec3 f = eye - origin;
    float len2 = dot(f, f);
    f = len2 > 0.0 ? f * inversesqrt(len2) : vec3(0.0, 0.0, 1.0);
    vec3 right = cross(vec3(0.0, 1.0, 0.0), f);
    float r2 = dot(right, right);
    // The eye straight above or below the card: the world's up is along the line of sight and
    // says nothing about roll, so the card's up is taken along the world's +Z there. A round puff
    // seen face on hides the roll this leaves it with.
    if (r2 < 1e-8) {
        right = cross(vec3(0.0, 0.0, 1.0), f);
        r2 = dot(right, right);
    }
    right *= inversesqrt(r2);
    return mat3(right, cross(f, right), f);
}

// The model-view matrix of a quad centred on its instance origin, turned by the facade basis above
// and keeping the instance's own scale on each axis (Godot's billboard_keep_scale). A vertex()
// assigns it to MODELVIEW_MATRIX; the instance's rotation never reaches the quad.
mat4 csky_facade_modelview(mat4 view, mat4 model, vec3 eye) {
    mat3 face = csky_facade_spherical(model[3].xyz, eye);
    mat4 mv = view * mat4(vec4(face[0], 0.0), vec4(face[1], 0.0), vec4(face[2], 0.0), model[3]);
    mv[0] *= length(model[0].xyz);
    mv[1] *= length(model[1].xyz);
    mv[2] *= length(model[2].xyz);
    return mv;
}

#endif
