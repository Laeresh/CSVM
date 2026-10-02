using System.Diagnostics;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// The enhanced graphics mode's settings on a sun and an Environment, in both directions. They go
/// on at launch, and on or off again when the mode switches live. Every magnitude here is TUNE,
/// since nothing in the original authors a shadow map or a screen-space pass. The off direction
/// puts back a fresh object's defaults, which is what the faithful path builds with.
/// </summary>
public static class EnhancedLook
{
    // TUNE, and the FALLBACK only. A flown mission overwrites this per zone from its pushed-out fog
    // near (WeatherRig.ApplyEnhancedLighting), so shadows end where the haze ramp begins. A
    // session with no weather.json keeps it; it sits mid-range of the shipped zones' pushed nears
    // (2000-4200 m).
    private const float ShadowMaxDistance = 3000f;

    // TUNE, paired with the distance above and judged the same way (WeatherRig.ApplyEnhancedLighting
    // sets the flown-mission copy). Fades the last cascade out before this fallback distance rather
    // than cutting at a hard edge.
    private const float ShadowFadeStart = 0.8f;

    // TUNE, judged at the controls. Lower values put dithered acne over lit surfaces at C1's 25°
    // sun, and higher ones dissolve a hangar's shadow. These keep the building and aircraft
    // silhouettes. The ground and the water cast no shadow (WorldBuilder.IsShadowlessGround), so
    // neither can band itself.
    private const float ShadowBias = 0.05f;
    private const float ShadowNormalBias = 1.25f;

    // TUNE. Fractions of ShadowMaxDistance, tighter than Godot's 0.1/0.2/0.5. A player reads the
    // aircraft's own shadow and the buildings it passes, all within a few hundred metres. The
    // outer cascades only carry a skyline into the haze.
    private const float ShadowSplit1 = 0.12f;
    private const float ShadowSplit2 = 0.17f;
    private const float ShadowSplit3 = 0.42f;

    // TUNE. Screen-space reflection on the glossy water arm. The step count buys reflection length
    // along the ray; the fades hide where a ray runs off the screen or past the depth buffer.
    // The fade-out exponent is the lever on the border and aircraft flicker, since it dims a ray
    // before it is lost. Depth tolerance measures inert here, from this value up to 8.0.
    private const int SsrMaxSteps = 64;
    private const float SsrFadeIn = 0.15f;
    private const float SsrFadeOut = 2.5f;
    private const float SsrDepthTolerance = 0.2f;

    // Where the faithful path's filter quality comes from: project.godot, or Godot's own default.
    private const string ShadowFilterQualitySetting =
        "rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality";

    // Where the faithful path's shadow atlas edge comes from, and Godot's own default for it.
    private const string ShadowAtlasSizeSetting = "rendering/lights_and_shadows/directional_shadow/size";
    private const int DefaultShadowAtlasSize = 4096;

    // TUNE, judged at the controls on C2/C5. Godot's own default (1.0 m) reads a building's own
    // trim but misses the wider contact shading a street canyon wants at this world's scale.
    // This radius picks up a block's base and a hangar's corner without darkening open tarmac.
    private const float SsaoRadius = 2.5f;

    // TUNE, judged at the controls: Godot's defaults (intensity 2.0, power 1.5). At this radius
    // they read as grounded contact shading rather than a grey wash.
    private const float SsaoIntensity = 2.0f;
    private const float SsaoPower = 1.5f;

    // TUNE, Godot defaults. Detail keeps small creases (window mullions, girders) from being
    // swallowed by the coarse term above. Horizon and sharpness are the denoise pair that keeps the
    // depth-buffer edges from shimmering worse than the effect is worth.
    private const float SsaoDetail = 0.5f;
    private const float SsaoHorizon = 0.06f;
    private const float SsaoSharpness = 0.98f;

    // TUNE, judged at the controls against C21's contract (only the glow-arm sprites exceed 1.0
    // in the HDR buffer). A threshold of 1.0 blooms exactly them. Bloom stays 0, so nothing below
    // threshold glows. Screen blend keeps a flare's halo additive without blowing its core out.
    private const float GlowHdrThreshold = 1.0f;
    private const float GlowBloom = 0.0f;
    private const float GlowIntensity = 0.9f;
    private const float GlowStrength = 1.1f;
    private const Godot.Environment.GlowBlendModeEnum GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen;

    // TUNE. Scale and cap on the values the glow pass reads before it thresholds them. A saturated
    // flare core (255 before the tonemap) still separates from its own falloff under them.
    private const float GlowHdrScale = 2.0f;
    private const float GlowHdrLuminanceCap = 8.0f;

    // TUNE, judged at the controls against a C4 horizon, a C1 horizon and C5 at night. AgX rolls
    // off the far-ridge washout the authored sun energy produces, and keeps the night city's
    // contrast where Filmic reads flatter. Exposure stays neutral. The AgX white point recovers the
    // horizon, since AgX ignores the general TonemapWhite.
    private const Godot.Environment.ToneMapper TonemapMode = Godot.Environment.ToneMapper.Agx;
    private const float TonemapExposure = 1.0f;
    private const float TonemapAgxWhite = 6.0f;
    private const float TonemapAgxContrast = 1.0f;

    // The zone default FOG_COLOR (Effects/Weather.cs's no-weather zone), which is the colour a
    // horizon dome fades into at eye level. Enhanced mode's sky until a flown zone writes its own
    // over it, so a world with no weather.json still reflects a plausible sky.
    private static readonly Color DefaultSkyColor = new(0.69f, 0.69f, 0.69f);


    /// <summary>Gets a value indicating whether this process has switched the graphics mode live, which
    /// is when a load warms the other mode's shaders by default.</summary>
    public static bool HasSwitched { get; private set; }

    /// <summary>The live graphics-mode switch, the one sequence the launcher runs and the round-trip
    /// suite drives. It sets the flag, regenerates the shaders and re-dresses <paramref name="sun"/>
    /// and <paramref name="env"/>. Then the clutter fade and the display quality are resolved again,
    /// and <paramref name="session"/> rebuilds what the two modes build differently. Returns whether it
    /// switched: not when the mode already stands, nor for a network session.</summary>
    public static bool Switch(bool enhanced, DirectionalLight3D sun, Godot.Environment? env,
        EnhancedPasses skipped, bool det, GameSession? session, string why)
    {
        if (enhanced == GraphicsMode.Enhanced)
            return false;
        // ⚠ Never switch a network session. Its shared world has no pause to hold the stall in.
        if (session?.Wire.Link != null)
        {
            Log.Info("world", $"graphics mode: {why} refused, a network session switches no graphics mode");
            return false;
        }
        long start = Stopwatch.GetTimestamp();
        Tooling.ShaderDiagnostics.BeginSwitch();
        SwitchProfile.Begin();
        GraphicsMode.Set(enhanced);
        HasSwitched = true;
        var regen = ShaderTwins.Regenerate();
        SwitchProfile.Mark("shaders");
        ApplySun(sun, enhanced, skipped);
        SwitchProfile.Mark("sun");
        if (env != null)
            ApplyEnvironment(env, enhanced, skipped);
        SwitchProfile.Mark("env");
        WriteClutterFadeScale();
        ReapplyDisplayQuality(det, why);
        SwitchProfile.Mark("display");
        session?.ApplyGraphicsMode();
        SwitchProfile.End();
        Log.Info("world", $"graphics mode: {GraphicsMode.Key}={(enhanced ? "enhanced" : "original")} (switched live by {why}) ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:0.0} shader_entries={regen.Entries} materials={regen.Tracked} moved={regen.Moved} twins_made={regen.TwinsMade} rewritten={regen.Retexted} steps=[{SwitchProfile.Line()}]");
        Tooling.ShaderDiagnostics.NoteSwitch();
        return true;
    }

    /// <summary>Has Godot build the advanced scene-shader variants a TAA frame needs, for every shader
    /// alive, while the load screen is still up. It draws one frame of a hidden 256x256 viewport under
    /// <paramref name="host"/> with TAA and SSAO on and an empty world. Only an Original process that
    /// has drawn no Enhanced frame needs it; the first Enhanced frame does it otherwise. Returns
    /// whether it raised the viewport. The engine's side is docs/verification.md PERF-45.</summary>
    public static bool WarmAdvancedVariants(Node host)
    {
        if (GraphicsMode.Enhanced || ShaderTwins.EnhancedDrawn)
            return false;
        // ⚠ Keep the viewport this size or larger. At 2x2 the screen-space passes ask for more mips
        // than the buffer holds, and the Deck crashed on the null texture that leaves.
        var view = new SubViewport
        {
            Name = "advanced_variant_warm",
            Size = new Vector2I(256, 256),
            OwnWorld3D = true,
            World3D = new World3D(),
            UseTaa = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        view.AddChild(new Camera3D
        {
            Current = true,
            Environment = new Godot.Environment { SsaoEnabled = true, SsrEnabled = true },
        });
        host.AddChild(view);
        // From here every new shader compiles the advanced group with its base one.
        ShaderTwins.EnhancedDrawn = true;
        var tree = host.GetTree();
        int frames = 0;
        void Drop()
        {
            if (++frames < 3 && GodotObject.IsInstanceValid(view))
                return;
            tree.ProcessFrame -= Drop;
            if (GodotObject.IsInstanceValid(view))
                view.QueueFree();
        }
        tree.ProcessFrame += Drop;
        return true;
    }

    /// <summary>The View Distance on the running world: the clutter fade's scale, and the clutter
    /// cells cut again at it. It moves nothing in original mode, but is resolved there too, so a
    /// later switch to Enhanced opens at the saved reach.</summary>
    public static void ApplyViewDistance(string? word, GameSession? session)
    {
        ViewDistance.Set(word);
        if (!WriteClutterFadeScale())
            return;
        session?.FollowClutterFade();
        Log.Info("world", $"view distance: {word}, clutter reach x{ViewDistance.ClutterReach():0.#} (applied live)");
    }

    /// <summary>A texture's alpha depth on the running world. The archive uploads its alpha-plane
    /// textures again at the standing mode's depth. Then the puffer atlases and painted skins made
    /// from them are baked again, each in place. The session calls it first on a switch.
    /// ⚠ Never move the truncation into a shader. Cutting after filtering changes the pixels.</summary>
    public static void FollowAlphaDepth(TextureArchive? textures)
    {
        if (textures == null)
            return;
        long start = Stopwatch.GetTimestamp();
        long bytes = textures.FollowAlphaDepth();
        double texturesMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        int atlases = Effects.Puffer.FollowAlphaDepth(textures);
        int painted = PlanePainter.FollowAlphaDepth(textures);
        double totalMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Log.Info("world", $"alpha depth: {(GraphicsMode.Enhanced ? 8 : 4)}-bit textures={textures.AlphaDepthFollowers.Count} bytes={bytes} atlases={atlases} painted={painted} textures_ms={texturesMs:0.0} total_ms={totalMs:0.0}");
    }

    /// <summary>The clutter fade's squared scale under the fog push and the View Distance reach, both
    /// identity in original mode. An infinite reach makes it 0, the never-fades scale.</summary>
    public static float ClutterFadeScaleSq() =>
        EffectsLevel.ResolveClutterFadeScaleSq(WeatherRig.EnhancedFogScale() * ViewDistance.ClutterReach());

    /// <summary>The anti-aliasing method and the render scale resolved again from the sources the
    /// startup reads, and written on every live 3D viewport. The method's default follows the mode.
    /// </summary>
    public static void ReapplyDisplayQuality(bool det, string why)
    {
        bool enhanced = GraphicsMode.Enhanced;
        var antiAliasing = AntiAliasingSetting.Resolve(AntiAliasingSetting.SavedWord(det),
            Config.GetString(AntiAliasingSetting.Key, AntiAliasingSetting.DefaultFor(enhanced)), enhanced);
        var renderScale = RenderScaleSetting.Resolve(RenderScaleSetting.SavedWord(det),
            Config.GetString(RenderScaleSetting.Key, RenderScaleSetting.Default), antiAliasing.Word);
        ViewportQuality.ReapplyAll();
        Log.Info("world", $"display quality: render_scale={renderScale.Word}% source={renderScale.Source} anti_aliasing={antiAliasing.Word} aa_source={antiAliasing.Source} (applied live by {why})");
    }

    /// <summary>The session sun's shadow maps and visual layer, on at the resolved shadow quality or
    /// back to a fresh light's defaults. ⚠ Also sets the renderer-wide soft-shadow filter and shadow
    /// atlas, which belong to this light alone; the off direction puts back the project's own pair.</summary>
    public static void ApplySun(DirectionalLight3D sun, bool enhanced, EnhancedPasses skipped)
    {
        if (!enhanced)
        {
            // The colour and specular too: the enhanced zone apply writes both, and the faithful
            // one writes neither back.
            var fresh = new DirectionalLight3D();
            sun.Layers = fresh.Layers;
            sun.LightColor = fresh.LightColor;
            sun.LightSpecular = fresh.LightSpecular;
            SunShadow.Copy(fresh, sun, fresh.DirectionalShadowMaxDistance);
            fresh.Free();
            RenderingServer.DirectionalSoftShadowFilterSetQuality((RenderingServer.ShadowQuality)
                ProjectSettings.GetSetting(ShadowFilterQualitySetting, (int)RenderingServer.ShadowQuality.SoftLow).AsInt32());
            RenderingServer.DirectionalShadowAtlasSetSize(
                ProjectSettings.GetSetting(ShadowAtlasSizeSetting, DefaultShadowAtlasSize).AsInt32(), true);
            return;
        }
        // On a layer every pane camera draws and the spyglass disc's does not. The disc is lit by a
        // shadowless copy instead (Flight/Camera/SpyglassSun), so it renders no shadow pass.
        sun.Layers = UI.Boards.SplitScreen.SunLayer;
        // Four splits because the useful range spans an aircraft's own shadow a few metres below
        // it and a skyline several kilometres out.
        sun.ShadowEnabled = true;
        sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
        sun.DirectionalShadowMaxDistance = ShadowMaxDistance;
        sun.DirectionalShadowFadeStart = ShadowFadeStart;
        sun.DirectionalShadowSplit1 = ShadowSplit1;
        sun.DirectionalShadowSplit2 = ShadowSplit2;
        sun.DirectionalShadowSplit3 = ShadowSplit3;
        sun.DirectionalShadowBlendSplits = true;
        sun.ShadowBias = ShadowBias;
        sun.ShadowNormalBias = ShadowNormalBias;
        ApplyShadowQuality(sun, true, skipped);
    }

    /// <summary>The resolved <see cref="ShadowQualitySetting"/> level on the sun and the renderer:
    /// whether it casts, its penumbra, the filter and the atlas. Writes nothing unless
    /// <paramref name="enhanced"/>. <c>--no-soft-shadows</c> keeps the shadow and drops its
    /// penumbra, which isolates the filter's screen-space sample pattern.</summary>
    public static void ApplyShadowQuality(DirectionalLight3D sun, bool enhanced, EnhancedPasses skipped) =>
        ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.Sun, enhanced,
            (skipped & EnhancedPasses.SoftShadows) != 0);

    /// <summary>The screen-space passes, the tonemap and the mission sky on one Environment, or a
    /// fresh Environment's values for each. The faithful path's world is fullbright, so none of
    /// these passes has anything to work on there. A zone apply writes the ambient and the sky
    /// colour after this, in either mode.</summary>
    public static void ApplyEnvironment(Godot.Environment env, bool enhanced, EnhancedPasses skipped)
    {
        var fresh = new Godot.Environment();
        if (enhanced)
        {
            // The water's specular reflects the mission's own colour, not Godot's procedural
            // gradient; the dome hides the background itself. Each zone apply writes its FOG_COLOR
            // over this default (WeatherRig.WriteSkyColor).
            env.Sky = new Sky { SkyMaterial = new PanoramaSkyMaterial() };
            WeatherRig.WriteSkyColor(env, DefaultSkyColor);
        }
        else
        {
            env.Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() };
            env.ReflectedLightSource = fresh.ReflectedLightSource;
        }

        bool ssao = enhanced && (skipped & EnhancedPasses.Ssao) == 0;
        env.SsaoEnabled = ssao;
        env.SsaoRadius = ssao ? SsaoRadius : fresh.SsaoRadius;
        env.SsaoIntensity = ssao ? SsaoIntensity : fresh.SsaoIntensity;
        env.SsaoPower = ssao ? SsaoPower : fresh.SsaoPower;
        env.SsaoDetail = ssao ? SsaoDetail : fresh.SsaoDetail;
        env.SsaoHorizon = ssao ? SsaoHorizon : fresh.SsaoHorizon;
        env.SsaoSharpness = ssao ? SsaoSharpness : fresh.SsaoSharpness;

        // ⚠ SSR reflects only what the camera already draws; content off-screen or behind the
        // near plane has no reflection at all. Water is the one glossy population it serves.
        bool ssr = enhanced && (skipped & EnhancedPasses.Ssr) == 0;
        env.SsrEnabled = ssr;
        env.SsrMaxSteps = ssr ? SsrMaxSteps : fresh.SsrMaxSteps;
        env.SsrFadeIn = ssr ? SsrFadeIn : fresh.SsrFadeIn;
        env.SsrFadeOut = ssr ? SsrFadeOut : fresh.SsrFadeOut;
        env.SsrDepthTolerance = ssr ? SsrDepthTolerance : fresh.SsrDepthTolerance;

        // Only C21's glow-arm sprites and the puffer fire flipbook bloom out of the HDR buffer.
        // MultiMeshEmitterRenderer lifts the fire_f01-fire_f06 columns alone over this threshold.
        bool glow = enhanced && (skipped & EnhancedPasses.Glow) == 0;
        env.GlowEnabled = glow;
        env.GlowHdrThreshold = glow ? GlowHdrThreshold : fresh.GlowHdrThreshold;
        env.GlowBloom = glow ? GlowBloom : fresh.GlowBloom;
        env.GlowIntensity = glow ? GlowIntensity : fresh.GlowIntensity;
        env.GlowStrength = glow ? GlowStrength : fresh.GlowStrength;
        env.GlowBlendMode = glow ? GlowBlendMode : fresh.GlowBlendMode;
        env.GlowHdrScale = glow ? GlowHdrScale : fresh.GlowHdrScale;
        env.GlowHdrLuminanceCap = glow ? GlowHdrLuminanceCap : fresh.GlowHdrLuminanceCap;

        // Without the tonemap the HDR values a lit world produces clip instead of rolling off. No
        // bisect door closes it, since every enhanced frame's exposure depends on it.
        env.TonemapMode = enhanced ? TonemapMode : fresh.TonemapMode;
        env.TonemapExposure = enhanced ? TonemapExposure : fresh.TonemapExposure;
        env.TonemapAgxWhite = enhanced ? TonemapAgxWhite : fresh.TonemapAgxWhite;
        env.TonemapAgxContrast = enhanced ? TonemapAgxContrast : fresh.TonemapAgxContrast;
    }

    // The clutter fade's scale on the shader global and on EffectsLevel, which the clutter cells
    // read when they are cut. Returns whether the scale moved.
    private static bool WriteClutterFadeScale()
    {
        float scaleSq = ClutterFadeScaleSq();
        bool moved = scaleSq != EffectsLevel.RegisteredScaleSq;
        RenderingServer.GlobalShaderParameterSet(EffectsLevel.ShaderParam, scaleSq);
        EffectsLevel.RegisteredScaleSq = scaleSq;
        return moved;
    }
}
