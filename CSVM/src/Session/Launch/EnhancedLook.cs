using CSVM.Session.World;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.Launch;

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

    // TUNE, judged at the controls. The sun's apparent size in degrees; the real sun is about 0.5.
    // It softens a cast edge into a penumbra. A 0.25/0.5/1.0/2.0 sweep at the C1 waterfall lake
    // held the edge at 4-6 px through 1.0. The penumbra grows with the caster's height, and at 2.0
    // an aircraft 40 m up blurs its own shadow away.
    private const float ShadowAngularDistance = 1.0f;

    // TUNE: the directional shadow map's edge in texels, twice Godot's 4096. The aircraft's own
    // shadow is the one read at the controls, and the first cascade spans 0.12 of the distance.
    private const int ShadowAtlasSize = 8192;

    // TUNE, judged at the controls: Godot's own default. Raising it alongside the angular distance
    // above widens the edge further.
    private const float ShadowBlur = 1.0f;

    // ⚠ Do not lower this while ShadowAngularDistance stays above the sun's real 0.5°.
    // Godot resolves a penumbra by sampling the shadow map through a disc rotated per screen
    // pixel. Too few samples for the disc's width leave that rotation as a woven pattern over
    // every lit surface. This width needs the top rung.
    private const RenderingServer.ShadowQuality ShadowFilterQuality = RenderingServer.ShadowQuality.SoftUltra;

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

    // The zone default FOG_COLOR (Flight/Airframe/Weather.cs's no-weather zone), which is the colour a
    // horizon dome fades into at eye level. Enhanced mode's sky until a flown zone writes its own
    // over it, so a world with no weather.json still reflects a plausible sky.
    private static readonly Color DefaultSkyColor = new(0.69f, 0.69f, 0.69f);

    /// <summary>The session sun's shadow maps, on or back to a fresh light's defaults. The one
    /// place the sun's shadow setup is written, so a later shadow setting re-applies through here.
    /// ⚠ Also sets the renderer-wide soft-shadow filter and shadow atlas, which belong to this light
    /// alone.</summary>
    public static void ApplySun(DirectionalLight3D sun, bool enhanced, EnhancedPasses skipped)
    {
        if (!enhanced)
        {
            // The colour and specular too: the enhanced zone apply writes both, and the faithful
            // one writes neither back.
            var fresh = new DirectionalLight3D();
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
        // Both zero leaves a hard shadow edge rather than no shadow. That isolates the penumbra
        // filter, which is the part resolving with a screen-space sample pattern.
        bool hard = (skipped & EnhancedPasses.SoftShadows) != 0;
        sun.LightAngularDistance = hard ? 0f : ShadowAngularDistance;
        sun.ShadowBlur = hard ? 0f : ShadowBlur;
        // Renderer-wide settings rather than light properties, set here beside the width they
        // carry rather than in project.godot, where the faithful path would inherit them.
        RenderingServer.DirectionalSoftShadowFilterSetQuality(hard ? RenderingServer.ShadowQuality.Hard : ShadowFilterQuality);
        RenderingServer.DirectionalShadowAtlasSetSize(ShadowAtlasSize, true);
    }

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
}
