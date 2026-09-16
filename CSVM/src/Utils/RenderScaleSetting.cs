using System.Globalization;
using System.Linq;

namespace CSVM.Utils;

/// <summary>One resolved render scale: the factor the 3D viewports render at, the word it was
/// spelled as, and the source that won, named so a log line can say which of the three layers the
/// run is obeying.</summary>
public readonly record struct RenderScalePlan(float Scale, string Word, string Source);

/// <summary>
/// The render scale: the multiple of its own size a 3D viewport renders at before the image is
/// resampled down to it, so a machine with GPU headroom spends it on edges the pixel grid would
/// otherwise stagger. The words are <see cref="DisplayWords.RenderScaleChoices"/>, percentages of
/// native, and the sources layer the way <see cref="VSyncSetting"/> layers its own: the saved
/// <c>renderScale</c> option, then the <see cref="Key"/> config key, then native.
/// <see cref="ViewportQuality"/> is the one reader of <see cref="Scale"/> and writes nothing at
/// native, which is what keeps a faithful run on Godot's own defaults.
/// </summary>
public static class RenderScaleSetting
{
    /// <summary>The config key under the saved option, a percentage word for a machine whose
    /// options file does not say.</summary>
    public const string Key = "graphics.renderScale";

    /// <summary>The word a launch with nothing saved runs at, native resolution.</summary>
    public const string Default = DisplayWords.RenderScaleNative;

    /// <summary>Native resolution, and the one factor <see cref="ViewportQuality"/> writes nothing
    /// for.</summary>
    public const float Native = 1.0f;

    /// <summary><b>Resolved once at launch</b> by <see cref="Resolve"/>, before any 3D viewport is
    /// built. A viewport takes the scale as it is constructed, so a choice made on the Options page
    /// is saved and reaches the image on the next start, which is what the row's description
    /// says.</summary>
    public static float Scale { get; private set; } = Native;

    /// <summary>The scale the sources resolve to, highest first: <paramref name="savedWord"/>, then
    /// <paramref name="configWord"/>, then native. A word this vocabulary does not know reads as
    /// never set and falls through, the same contract <see cref="OptionsStore"/> validates the field
    /// under. A config key spelling native is reported as the default, since native is what the
    /// absent key already means.</summary>
    public static RenderScalePlan Resolve(string? savedWord, string? configWord)
    {
        if (TryParseWord(savedWord, out float saved))
        {
            return Store(new RenderScalePlan(saved, savedWord!, "options.json"));
        }

        if (configWord != Default && TryParseWord(configWord, out float configured))
        {
            return Store(new RenderScalePlan(configured, configWord!, Key));
        }

        return Store(new RenderScalePlan(Native, Default, "default"));
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved display setting: options.json is one machine's state
    /// and a golden shot is a <c>--det</c> run against the player's own options directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().RenderScale;

    /// <summary>Whether <paramref name="word"/> is one of
    /// <see cref="DisplayWords.RenderScaleChoices"/>, and the factor it spells. False leaves
    /// <paramref name="scale"/> at <see cref="Native"/>, which is how a caller tells "not a word I
    /// know" from a deliberate 100.</summary>
    public static bool TryParseWord(string? word, out float scale)
    {
        scale = Native;
        if (word == null || !DisplayWords.RenderScaleChoices.Contains(word))
        {
            return false;
        }

        if (!int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out int percent) || percent <= 0)
        {
            return false;
        }

        scale = percent / 100f;
        return true;
    }

    private static RenderScalePlan Store(RenderScalePlan plan)
    {
        Scale = plan.Scale;
        return plan;
    }
}
