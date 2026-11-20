using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// Whether Enhanced's volumetric clouds glow round the rounds, bursts and fires inside them
/// (<c>Session.World.VolumetricClouds</c>), one of <see cref="Words"/>. In <see cref="Lookup"/>,
/// <c>--cloud-glow=</c> beats the saved <c>cloudGlow</c> option, which beats the <see cref="Key"/>
/// config key, then the GPU's fallback (<see cref="FallbackFor"/>). Under <c>--det</c> only the flag survives. The VIDEO page's
/// Cloud Glow row writes it. The clouds read it every frame, so it applies at once.
/// </summary>
public static class CloudGlowSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.cloudGlow";

    /// <summary>The clouds take no point light; the ground still does.</summary>
    public const string Off = "off";

    /// <summary>The nearest point lights glow inside the clouds.</summary>
    public const string On = "on";

    /// <summary>The word a discrete GPU, and every machine under <c>--det</c>, runs with nothing set.</summary>
    public const string Default = On;

    /// <summary>The word an integrated GPU (the Steam Deck's APU) runs with nothing set. On the Deck at
    /// 1280x800 the glow cost 12 to 13 ms a frame while firing in cloud, quarter or full march.</summary>
    public const string IntegratedDefault = Off;

    /// <summary>The log's name for the fallback rule that chose <see cref="IntegratedDefault"/>.</summary>
    public const string IntegratedRule = "default_integrated_gpu";

    /// <summary>Every word, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, On };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "On" };

    /// <summary>The setting's registry entry, beaten by <c>--cloud-glow=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until the launch resolves it.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>Whether the clouds glow round point lights this frame.</summary>
    public static bool Drawn => Word == On;

    /// <summary>The fallback on a GPU that is or is not <paramref name="integrated"/>.</summary>
    public static ResolvedWord FallbackFor(bool integrated) => integrated
        ? new ResolvedWord(IntegratedDefault, SettingSource.Default, IntegratedRule)
        : new ResolvedWord(Default, SettingSource.Default, null);

    /// <summary>The word the sources resolve to, stored as <see cref="Word"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord)
    {
        var resolved = Lookup.Resolve(flagWord, savedWord, configWord, Default, null);
        Word = resolved.Word;
        return resolved;
    }

    // Built in a method rather than inline, as VegetationQualitySetting's is.
    private static WordSetting Register() => new(Key, Words, "--cloud-glow")
    {
        SavedField = "cloudGlow",
        LogName = "cloud_glow",
        Labels = CloudGlowSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Tracers, rockets and explosions light the volumetric clouds round them. Off saves GPU time while firing in cloud. Applies at once."
            : "Enhanced Graphics only: the original world has no volumetric clouds.",
        Shown = static _ => Word,
        // ⚠ Ask the renderer only off --det: a unit test reads FallbackFor, having no renderer.
        Fallback = static context => FallbackFor(!context.Det && ShadowQualitySetting.IntegratedGpu),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
    };
}
