using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// PROTOTYPE, throwaway: Godot's real-time global illumination over the Enhanced world, behind
/// <c>--proto-gi=sdfgi|ssil|both</c>. SDFGI is the engine's cascaded signed-distance-field GI over
/// the static world the session builds, with no bake. Sunlit ground bounces into building shadow
/// and the sky light is occluded under eaves and in streets. SSIL is the screen-space
/// indirect light that fills what the cascades are too coarse for. Both are plain Environment
/// switches, so the door only sets them with cascades sized for a 16 km chapter; the faithful
/// path never reads it. Beside the cloud prototype it answers whether GI is worth its cost here.
/// </summary>
internal static class ProtoGi
{
    // Eight cascades at a 1.5 m finest cell reach about 24 km, the span of a chapter from altitude.
    private const int Cascades = 8;
    private const float MinCellSizeM = 1.5f;
    private const float BounceFeedback = 0.5f;
    // Half energy: a procedural or panorama sky at full energy over-fills a flat world's ground.
    private const float Energy = 0.5f;
    private const float SsilRadiusM = 20f;
    private const float SsilIntensity = 1.0f;

    /// <summary>The mode under comparison.</summary>
    public enum Mode
    {
        Off,
        Sdfgi,
        Ssil,
        Both,
    }

    /// <summary>Gets the mode the launch asked for, Off until <see cref="Configure"/> runs.</summary>
    public static Mode Current { get; private set; }

    /// <summary>Gets a value indicating whether SDFGI's own sky light stands on the live Environment.
    /// Then the zone's flat ambient steps aside (<c>WeatherRig.WriteColorAmbient</c>) instead of doubling it.</summary>
    public static bool SkyLightLive { get; private set; }

    /// <summary>Reads the flag's word; an unknown word warns and leaves GI off.</summary>
    public static void Configure(string? word)
    {
        Current = word?.Trim().ToLowerInvariant() switch
        {
            null => Mode.Off,
            "off" => Mode.Off,
            "sdfgi" => Mode.Sdfgi,
            "ssil" => Mode.Ssil,
            "both" or "" => Mode.Both,
            _ => Unknown(word),
        };
    }

    /// <summary>Writes the mode onto <paramref name="env"/>: on under Enhanced with a mode chosen, a
    /// fresh Environment's values otherwise. The faithful path and an unflagged launch read what
    /// they always did.</summary>
    public static void Apply(Godot.Environment env, bool enhanced)
    {
        var fresh = new Godot.Environment();
        bool sdfgi = enhanced && Current is Mode.Sdfgi or Mode.Both;
        bool ssil = enhanced && Current is Mode.Ssil or Mode.Both;
        SkyLightLive = sdfgi;
        env.SdfgiEnabled = sdfgi;
        env.SdfgiCascades = sdfgi ? Cascades : fresh.SdfgiCascades;
        env.SdfgiMinCellSize = sdfgi ? MinCellSizeM : fresh.SdfgiMinCellSize;
        // Flat worlds: half the vertical resolution buys horizontal reach.
        env.SdfgiYScale = sdfgi ? Godot.Environment.SdfgiyScale.Scale50Percent : fresh.SdfgiYScale;
        env.SdfgiUseOcclusion = sdfgi || fresh.SdfgiUseOcclusion;
        env.SdfgiReadSkyLight = sdfgi || fresh.SdfgiReadSkyLight;
        env.SdfgiBounceFeedback = sdfgi ? BounceFeedback : fresh.SdfgiBounceFeedback;
        env.SdfgiEnergy = sdfgi ? Energy : fresh.SdfgiEnergy;
        env.SsilEnabled = ssil;
        env.SsilRadius = ssil ? SsilRadiusM : fresh.SsilRadius;
        env.SsilIntensity = ssil ? SsilIntensity : fresh.SsilIntensity;
        if (enhanced && Current != Mode.Off)
            Log.Info("world", $"proto gi: {Current.ToString().ToLowerInvariant()} (sdfgi {(sdfgi ? "on" : "off")}, {Cascades} cascades at {MinCellSizeM} m, ssil {(ssil ? "on" : "off")})");
    }

    private static Mode Unknown(string word)
    {
        Log.Warn("world", $"proto gi: unknown mode '{word}' (sdfgi, ssil or both), GI stays off");
        return Mode.Off;
    }
}
