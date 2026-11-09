using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// Godot's real-time global illumination over the Enhanced world, behind the Global Illumination option
/// (<see cref="GlobalIlluminationSetting"/>): SDFGI plus SSIL. SDFGI is the engine's cascaded signed-distance-field GI over the static world the
/// session builds, with no bake. Sunlit ground bounces into building shadow and the sky light is
/// occluded under eaves and in streets. SSIL is the screen-space indirect light that fills what the
/// cascades are too coarse for. Both are plain Environment switches, so this only sets them with
/// cascades sized for a 16 km chapter; the faithful path never reads it.
/// </summary>
internal static class GlobalIllumination
{
    // Eight cascades at a 1.5 m finest cell reach about 24 km, the span of a chapter from altitude.
    private const int Cascades = 8;
    private const float MinCellSizeM = 1.5f;
    private const float BounceFeedback = 0.5f;
    private const float SsilRadiusM = 20f;
    private const float SsilIntensity = 1.0f;

    /// <summary>Gets or sets SDFGI's energy. Half by default: a sky at full energy over-fills a flat
    /// world's ground. The lighting lab's slider.</summary>
    public static float Energy { get; set; } = 0.5f;

    /// <summary>Gets or sets the share of the zone's flat ambient kept while SDFGI stands, 0 by
    /// default. The lighting lab's floor slider, for caves the GI's sky light cannot reach.</summary>
    public static float AmbientFloor { get; set; }

    /// <summary>Gets a value indicating whether SDFGI's own sky light stands on the live Environment.
    /// Then the zone's flat ambient steps aside (<c>WeatherRig.WriteColorAmbient</c>) instead of doubling it.</summary>
    public static bool SkyLightLive { get; private set; }

    /// <summary>Writes GI onto <paramref name="env"/>: on under Enhanced while the option stands on, a
    /// fresh Environment's values otherwise. The faithful path and the option off read what they
    /// always did.</summary>
    public static void Apply(Godot.Environment env, bool enhanced)
    {
        var fresh = new Godot.Environment();
        bool on = enhanced && GlobalIlluminationSetting.Enabled;
        SkyLightLive = on;
        env.SdfgiEnabled = on;
        env.SdfgiCascades = on ? Cascades : fresh.SdfgiCascades;
        env.SdfgiMinCellSize = on ? MinCellSizeM : fresh.SdfgiMinCellSize;
        // Flat worlds: half the vertical resolution buys horizontal reach.
        env.SdfgiYScale = on ? Godot.Environment.SdfgiyScale.Scale50Percent : fresh.SdfgiYScale;
        env.SdfgiUseOcclusion = on || fresh.SdfgiUseOcclusion;
        env.SdfgiReadSkyLight = on || fresh.SdfgiReadSkyLight;
        env.SdfgiBounceFeedback = on ? BounceFeedback : fresh.SdfgiBounceFeedback;
        env.SdfgiEnergy = on ? Energy : fresh.SdfgiEnergy;
        env.SsilEnabled = on;
        env.SsilRadius = on ? SsilRadiusM : fresh.SsilRadius;
        env.SsilIntensity = on ? SsilIntensity : fresh.SsilIntensity;
        if (on)
            Log.Info("world", $"global illumination: sdfgi {Cascades} cascades at {MinCellSizeM} m, ssil on");
    }
}
