using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// How the Rich vegetation draws grass and crops near the camera, one of <see cref="Words"/>: none,
/// shells (<c>Mech3.GroundCoverShells</c>) or cards (<c>Mech3.GroundCover</c>). In
/// <see cref="Lookup"/>, <c>--ground-cover=</c> beats the saved <c>groundCover</c> option, which beats
/// the <see cref="Key"/> config key, then <see cref="Default"/> on every machine. Under <c>--det</c>
/// only the flag survives. It acts only while <see cref="VegetationQualitySetting.RichOn"/>. The VIDEO
/// page's Ground Cover row writes it, applied live by building one cover and dropping the other.
/// </summary>
public static class GroundCoverSetting
{
    /// <summary>The config key under the saved option, one of <see cref="Words"/>.</summary>
    public const string Key = "graphics.groundCover";

    /// <summary>No ground cover.</summary>
    public const string Off = "off";

    /// <summary>Stacked shells over the picked ground.</summary>
    public const string Shells = "shells";

    /// <summary>New-art grass tufts and crop rows on the picked ground.</summary>
    public const string Cards = "cards";

    /// <summary>The word every machine runs with nothing set, and under <c>--det</c>. The one place the
    /// default is set.</summary>
    public const string Default = Cards;

    /// <summary>Every word, the cheaper first, the order the Options row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, Shells, Cards };

    /// <summary>What the Options row shows for each of <see cref="Words"/>, index for index.</summary>
    public static readonly IReadOnlyList<string> Labels = new[] { "Off", "Shells", "Cards" };

    /// <summary>The setting's registry entry, beaten by <c>--ground-cover=</c>.</summary>
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
    private static WordSetting Register() => new(Key, Words, "--ground-cover")
    {
        SavedField = "groundCover",
        LogName = "ground_cover",
        Labels = GroundCoverSetting.Labels,
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Rich vegetation only: grass and crops near you, drawn as Cards or Shells. Applies at once."
            : "Enhanced Graphics only: the original world draws no grass.",
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
