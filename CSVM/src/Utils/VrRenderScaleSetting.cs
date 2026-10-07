using System.Collections.Generic;
using System.Globalization;

namespace CSVM.Utils;

/// <summary>
/// The VR render scale: the multiple of the runtime's recommended eye size a headset session renders
/// at, OpenXR's <c>render_target_size_multiplier</c>. It cuts the GPU's work and the runtime's
/// compositing alike. The flat <see cref="RenderScaleSetting"/> only resamples the 3D inside that
/// size, and under a headset the two multiply. The words are percentages. The flag
/// beats the saved word, which beats 100, and no deterministic run reads the file.
/// <c>Launch/XrSession.cs</c> applies it, at boot and live from the options apply.
/// docs/architecture/Utils.md.
/// </summary>
public static class VrRenderScaleSetting
{
    /// <summary>The flag that beats the saved word.</summary>
    public const string Flag = "--xr-render-scale";

    /// <summary>The word a launch with nothing saved runs at, the runtime's own size.</summary>
    public const string Default = "100";

    /// <summary>The scales a row offers, percentages of the runtime's recommended eye size. The
    /// range reaches down to what a runtime's own lower resolution setting gives
    /// (docs/architecture/Utils.md).</summary>
    public static readonly IReadOnlyList<string> Words = new[] { "50", "60", "70", "80", "90", "100", "125" };

    /// <summary>The lookup over <see cref="Words"/>. No config key is read.</summary>
    public static readonly WordSetting Lookup = new("xr.renderScale", Words, Flag);

    /// <summary>The multiplier <paramref name="word"/> spells, 1 for a word outside
    /// <see cref="Words"/>.</summary>
    public static float ScaleOf(string? word) =>
        Lookup.IsWord(word) ? int.Parse(word!, NumberStyles.None, CultureInfo.InvariantCulture) / 100f : 1f;

    /// <summary>The word the sources resolve to: <paramref name="flagWord"/>, then
    /// <paramref name="savedWord"/>, then <see cref="Default"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord) =>
        Lookup.Resolve(flagWord, savedWord, null, Default);

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (<see cref="WordSetting.ReadSaved"/>).</summary>
    public static string? SavedWord(bool det) => WordSetting.ReadSaved(det, static o => o.VrRenderScale);

    /// <summary>How a row shows <paramref name="word"/>: a never-set one as the default.</summary>
    public static string Label(string? word) => (Lookup.IsWord(word) ? word : Default) + "%";

    /// <summary>The labels in row order.</summary>
    public static IReadOnlyList<string> Labels()
    {
        var labels = new string[Words.Count];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = Label(Words[i]);
        }

        return labels;
    }
}
