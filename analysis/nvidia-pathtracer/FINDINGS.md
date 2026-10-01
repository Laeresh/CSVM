# NVIDIA's path-tracing Godot fork, read from source

Question (#20): is the path tracer in NVIDIA's Godot fork usable for Enhanced Graphics lighting?
This file answers the part that can be answered by reading the fork's source: what the fork is,
how it is built and switched on, and what it does with CSVM's world materials. Building it,
running CSVM on it, the look and the frame cost need a Windows machine with an RTX card and are
still owed.

## Method

Read on 2026-10-01 from a blobless partial clone of `https://github.com/NVIDIA-RTX/godot`, branch
`nvidia-pt-dlss`, at **`135dff3887a3d2ccf45cafbd9bdffa7b718e1e76`**. The branch moves; every
claim below is about that commit. Nothing was built or run. File references are to that tree.

## What the fork is

Three NVIDIA commits on upstream Godot `master` at `c3e6b2c` (a merge of 2026-06-23):

- `6d10056` "NVIDIA: Miscellaneous": CI actions under `.github/`, nothing that ships.
- `5e232ff` "NVIDIA: Dependencies": the Streamline SDK headers under `thirdparty/streamline/include`
  and glslang/SPIRV-Reflect patches for the hit-object opcodes.
- `135dff3` "NVIDIA: Pathtracer + DLSS": the feature, 191 files.

The issue's "one squashed commit" is the third of these.

## 1. Build

From the SCons changes, not tried:

- `use_streamline` defaults to on and is forced off on every platform but Windows, so DLSS (upscaling,
  Ray Reconstruction, frame generation, Reflex) is Windows-only. `use_aftermath` (Nsight crash
  dumps) defaults to off and downloads its SDK at build time when enabled.
- Only Streamline's headers are vendored. The runtime is loaded with `LoadLibraryA("sl.interposer.dll")`
  (`drivers/streamline/streamline_context.cpp:74`), and when the DLL is missing the loader returns
  silently. So an export has to ship `sl.interposer.dll` and the DLSS plugin DLLs from the Streamline
  SDK beside the executable, and a build without them runs with DLSS absent rather than failing.
- The commit does not touch `modules/mono`, so a .NET editor and export template should follow the
  stock route (`module_mono_enabled=yes`, glue generation, `build_assemblies.py` into a local NuGet
  feed carrying `Godot.NET.Sdk` 4.8.0-dev). This is the part most likely to cost time and is unverified.

## 2. Runs

Not tried. The C# API diff is upstream 4.7 to master-of-2026-06-23 plus the fork's additions
(`Streamline`, `RTProceduralInstance3D`, the `Environment.Pathtracing*` members,
`Viewport.Scaling3DModeEnum.Dlss`). One fork change shifts an enum: `Scaling3DMode.Max` moves from 6
to 7, which matters only to code that stores the raw value.

## 3. Hooking in

**Switch.** Path tracing is per `Environment`: `pathtracing_enabled`, `pathtracing_samples_per_pixel`
(default 1), `pathtracing_max_bounces` (default 3), `pathtracing_denoiser` (default DLSS Ray
Reconstruction, or none), plus `rendering/pathtracing/*` project settings. It runs in a Forward+
variant (`render_forward_clustered_pt.cpp`), which is the renderer CSVM already uses. The Environment
docs say shadow maps, SDFGI and SSR are bypassed while it is on. The cockpit and spyglass SubViewports
have their own environments, so they could stay rasterized.

**Hardware.** The tracer checks only `RD::SUPPORTS_RAYTRACING_PIPELINE`
(`render_forward_clustered_pt.cpp:499`) and warns once and falls back when it is absent. Nothing in the
path I read checks the vendor. So the tracer should run on any Vulkan ray-tracing GPU, and only the
DLSS denoiser and upscaler need an RTX card. Shader Execution Reordering is used where
`VK_EXT_ray_tracing_invocation_reorder` exists.

**Scene coverage.** The TLAS takes every mesh and MultiMesh on render layers 1 to 20 inside a cube of
half-size `z_far` around the camera, on screen or not (`renderer_scene_cull.cpp:3311-3335`, `:3442-3446`).
Hills behind the camera therefore shadow the ground in front of it, which is what the issue wants.
`CastShadow = Off` is not consulted; every traced surface occludes. Lights come from the same cull,
and the directional light is a cone with angular radius (`raytracing_lights_inc.glsl`), so the scene's
own `DirectionalLight3D` is used as it is.

**Custom `ShaderMaterial`s are traced, through a compiled copy of the material's own shader.**
`Shader` preprocesses its code a second time with `RT` defined (`scene/resources/shader.cpp`), and
`SceneShaderRaytracing::_preprocess_shader` compiles that copy's `vertex()` and `fragment()` into a
hit group. Instance uniforms, global uniforms, `TIME` and `INSTANCE_CUSTOM` are mapped. A shader can
therefore carry an `#ifdef RT` arm written for the tracer, and CSVM's generated world shader would need
one, for these reasons:

- **The geometry is the mesh, not what `vertex()` outputs.** BLASes are built from the mesh's vertex
  buffer; only skinning and blend shapes produce a deformed BLAS (`render_raytracing.cpp:2492-2506`).
  `vertex()` runs per hit, at the hit point, to compute varyings
  (`raytracing_custom_fragment_inc.glsl:56`). CSVM's depth bias, the scale toward the eye in
  `SceneBuilder.cs:2029`, never moves traced geometry, so every coplanar decal and overlay that the
  bias separates today lies exactly on its base polygon in the tracer, where the nearer hit is
  undefined.
- **`skip_vertex_transform` is ignored.** The define is emitted (`scene_shader_raytracing.cpp:1616`) but
  no ray-tracing include reads it, and the post-vertex model-view transform is unconditional
  (`raytracing_custom_fragment_inc.glsl:58-63`). CSVM's `vertex()` applies `MODELVIEW_MATRIX` and
  `MODELVIEW_NORMAL_MATRIX` itself, so its `NORMAL` would be transformed twice.
- **`unshaded` is ignored.** `MODE_UNSHADED` is defined but nothing in the tracer reads it. The hit
  result is built from `ALBEDO`, `ALPHA`, `ROUGHNESS`, `METALLIC`, `SPECULAR`, `EMISSION`, `NORMAL` and
  `NORMAL_MAP` only (`scene_raytracing_raygen.glsl:375-391`), and is then lit. A self-lit surface has to
  put its colour in `EMISSION` under `RT`. A custom `light()` is compiled only for procedural instances.
- **`FOG` is ignored.** CSVM's lit arms fog through `FOG` (`SceneBuilder.cs:2101-2106`). The tracer fogs
  only through the Environment's own fog (`apply_segment_fog`, `raytracing_closest_hit_common_inc.glsl:241`),
  which CSVM does not use. Traced surfaces would lose the mission's distance fog unless the Environment's
  fog is set to match it.
- **The tracer has no alpha blending.** A custom shader's instance is marked opaque unless it writes
  `ALPHA_SCISSOR_THRESHOLD` or declares a `hint_alpha` texture (`render_raytracing.cpp:2609-2613`), and
  the hit result's alpha is not read. Transparent instances are still rasterized afterwards over the
  traced image (`render_forward_clustered_pt.cpp:150-157`), but they are also in the TLAS. CSVM's
  soft-alpha world surfaces (shadow decals, clouds, waterfalls, smoke, `SceneBuilder.cs:1745-1750`)
  would be traced as opaque sheets that block the sun. An `RT` arm that writes
  `ALPHA_SCISSOR_THRESHOLD = 1.0; ALPHA = 0.0;` should make the any-hit stage ignore them, at the cost
  of an any-hit invocation per crossing; untested.
- **Gamma-space shading.** The world arms multiply texture by vertex colour in gamma space, and the
  original's baked vertex lighting lives in that colour. Traced bounce light on top of baked light
  lights those surfaces twice; the `RT` arm would have to choose between baked colour as albedo and
  plain texture albedo. That is a look decision.

None of this rules the fork out. It means that "takes the scene's materials as they are" is false
for CSVM, and the first code change on adoption is an `#ifdef RT` arm in `BiasShaderCode` and the
billboard generators.

## 4. Look, 5. Cost

Owed. They need the build from step 1 and the user's machine.

DLSS arrives as `Viewport.Scaling3DMode.Dlss` (scale 1.0 is DLAA, below 1.0 is Super Resolution, or
Ray Reconstruction when path tracing is on). For the anti-aliasing row, that makes DLSS a sixth method
beside `fsr2` in `AntiAliasingSetting`, written by `ViewportQuality.Apply`, rather than a replacement
for the TAA default. It exists only in a Windows build with the Streamline DLLs present.
