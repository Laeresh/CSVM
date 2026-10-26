using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// How far the Rich vegetation draws its near 3D models (<c>Mech3.VegetationModels</c>), one of
/// <see cref="Words"/>: none, the near band or the far one. In <see cref="Lookup"/>,
/// <c>--vegetation-models=</c> beats the saved <c>vegetationModels</c> option, which beats the
/// <see cref="Key"/> config key, then <see cref="Default"/> on every machine. Under <c>--det</c> only
/// the flag survives. It acts only while <see cref="VegetationQualitySetting.RichOn"/>. The VIDEO
/// page's 3D Vegetation row writes it, applied live by ranging the model cells again.
/// </summary>
public static class VegetationModelsSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.vegetationModels";

    /// <summary>No model: every card draws as the Rich vegetation draws it without one.</summary>
    public const string Off = "off";

    /// <summary>Models inside the near band, the default.</summary>
    public const string Near = "near";

    /// <summary>Models out to the far band.</summary>
    public const string Far = "far";

    /// <summary>The word every machine runs with nothing set, the Deck and Linux included until a
    /// measurement there says otherwise, and under <c>--det</c>.</summary>
    public const string Default = Near;

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, Near, Far };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "Near", "Far" };

    /// <summary>The setting's registry entry, beaten by <c>--vegetation-models=</c>.</summary>
    public static readonly WordSetting Lookup = Register();

    /// <summary>The word the run resolved, <see cref="Default"/> until <see cref="Resolve"/> runs.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>The word the sources resolve to, stored as <see cref="Word"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord, string? configWord)
    {
        var resolved = Lookup.Resolve(flagWord, savedWord, configWord, Default, null);
        Word = resolved.Word;
        return resolved;
    }

    // Built in a method rather than inline, as VegetationQualitySetting's is.
    private static WordSetting Register() => new(Key, Words, "--vegetation-models")
    {
        SavedField = "vegetationModels",
        LogName = "vegetation_models",
        Labels = VegetationModelsSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Rich vegetation only: Near and Far draw the trees near you in 3D, Off keeps every tree a card. Applies at once."
            : "Enhanced Graphics only: the original world's trees are always cards.",
        Shown = static _ => Word,
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default, null),
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world => world.FollowSession(),
    };
}
