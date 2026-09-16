#ifndef CSKY_SUN_INC
#define CSKY_SUN_INC

// The unit WORLD-space direction from a surface TOWARD the sun (or the moon at night, the zone's
// authored SUNLIGHT_ORIENTATION either way). Written from the session DirectionalLight3D's own
// basis by CSVM.Session.WeatherRig.WriteSunDirection, at lighting setup and on every zone apply.
// Godot shines a light down its local -Z, so this is the light's +Z and the negation of the
// direction the light travels: dot(this, surface_to_light) is 1 when the two agree.
//
// Registered in both graphics modes, because a global uniform has to exist before any shader
// declaring it compiles. It is READ only by the enhanced arms that grade a hand-billboarded
// sprite by the sun, which get no shading from Godot's light pipeline at all: the cloud cards
// (Effects/FogVolumeClutter.cs) and the smoke billboards.
//
// Declared here ONLY: a global uniform's type must agree across every shader that declares it,
// so a second declaration elsewhere risks a mismatch the compiler will not catch for you.
global uniform vec3 csky_sun_dir = vec3(0.0, 1.0, 0.0);

#endif
