#ifndef CSKY_INSTANCE_UNIFORMS_INC
#define CSKY_INSTANCE_UNIFORMS_INC

// THE canonical instance-uniform block. Every CSVM shader that declares ANY instance uniform
// includes this file and declares no others of its own.
//
// ⚠ THE ORDER BELOW IS A CONTRACT, NOT A STYLE CHOICE. Godot assigns instance-uniform indices
// by declaration order *within each shader*, and merges the resulting mapping across every
// material on one GeometryInstance3D. Two shaders that declare the same uniform at different
// indices therefore silently read each other's slots — that is the 2026-07-17 unfogged-hilltops
// bug, where `csky_fog_on` was index 0 in Clutter's shader and index 1 in SceneBuilder's, and
// fog vanished on the losing surfaces. Single-sourcing the block is what makes the indices
// agree structurally instead of by everyone remembering to append in the same order.
//
// Adding a uniform: append it at the END, here, once. Never declare an instance uniform in a
// shader body. Appending (rather than inserting) keeps every existing index stable, which
// matters because `GameSession.InstanceShaderParams` re-applies these by name after
// `Duplicate()` and any live `SetInstanceShaderParameter` call resolves through the same map.
//
// ⚠ DO NOT include this file from a shader that would otherwise declare NO instance uniform.
// Godot allocates a fixed 16-vec4 slot block per instance carrying *any* instance uniform, so
// a shader with none keeps its instances off that buffer entirely — which is deliberate for the
// opaque sprite variants (C5 alone stamps ~139k billboard sprites, and Run-2 item 2 already had
// to enlarge the buffer to 262144 for C4/C5). Adding uniforms to a shader that already carries
// one is free; giving one to a shader that carries none is not.

instance uniform float node_bias = 0.0;       // per-node coplanar draw-order tie-break (SceneBuilder)
instance uniform float csky_fog_on = 1.0;     // 0 = opt out of distance fog (the camera-anchored skydome)
instance uniform float csky_light_fade = 1.0; // 0 = no range fade (skydome stars)
instance uniform float csky_opacity = 1.0;    // OBJECT_OPACITY_STATE; only the alpha-writing variants USE it
instance uniform vec4 csky_tint = vec4(0.0);  // debug overlays: rgb = colour, a = strength (0 = untinted)

#endif
