using System.Linq;

namespace CSVM.Utils;

/// <summary>The anti-aliasing methods <see cref="ViewportQuality"/> knows how to write, one per
/// <see cref="DisplayWords.AntiAliasingChoices"/> word.</summary>
public enum AntiAliasingMethod
{
    /// <summary>Nothing past the project's own MSAA.</summary>
    Off,

    /// <summary>Godot's FXAA screen-space filter.</summary>
    Fxaa,

    /// <summary>Godot's SMAA screen-space filter.</summary>
    Smaa,

    /// <summary>Godot's temporal anti-aliasing.</summary>
    Taa,

    /// <summary>FSR 2.2, which carries its own temporal pass and replaces Godot's.</summary>
    Fsr2,
}

/// <summary>One resolved anti-aliasing method, the word it was spelled as, and the source that won.
/// The source is named so a log line can say which of the three layers the run is obeying.</summary>
public readonly record struct AntiAliasingPlan(AntiAliasingMethod Method, string Word, string Source);

/// <summary>
/// The anti-aliasing method the 3D viewports run, one of
/// <see cref="DisplayWords.AntiAliasingChoices"/>. The sources layer the saved <c>antiAliasing</c>
/// option, then the <see cref="Key"/> config key, then <see cref="DefaultFor"/> the graphics mode.
/// It is a display setting, so a chosen method is written whichever mode won. Only the default
/// follows the mode. <see cref="ViewportQuality"/> is the one reader of <see cref="Method"/>. It
/// writes nothing for <see cref="AntiAliasingMethod.Off"/>, which keeps a faithful run on Godot's
/// own defaults. FSR 2.2 caps the render scale at native, which
/// <see cref="RenderScaleSetting.ClampFor"/> applies.
/// </summary>
public static class AntiAliasingSetting
{
    /// <summary>The config key under the saved option, one of the five words.</summary>
    public const string Key = "graphics.antiAliasing";

    /// <summary>Resolved once at launch by <see cref="Resolve"/>, before any 3D viewport is
    /// built, so every viewport in a run takes the same method.</summary>
    public static AntiAliasingMethod Method { get; private set; } = AntiAliasingMethod.Off;

    /// <summary>The word a launch with nothing saved and no config key runs. Off keeps the faithful
    /// mode on its own image, and the enhanced mode runs TAA.</summary>
    public static string DefaultFor(bool enhanced) =>
        enhanced ? DisplayWords.AntiAliasingTaa : DisplayWords.AntiAliasingOff;

    /// <summary>The method the sources resolve to, highest first: <paramref name="savedWord"/>, then
    /// <paramref name="configWord"/>, then <see cref="DefaultFor"/> the mode. A word this vocabulary
    /// does not know reads as never set and falls through. A config key spelling the mode's default
    /// is reported as the default, since that is what the absent key already means.</summary>
    public static AntiAliasingPlan Resolve(string? savedWord, string? configWord, bool enhanced)
    {
        if (TryParse(savedWord, out var saved))
        {
            return Store(new AntiAliasingPlan(saved, savedWord!, "options.json"));
        }

        string fallback = DefaultFor(enhanced);
        if (configWord != fallback && TryParse(configWord, out var configured))
        {
            return Store(new AntiAliasingPlan(configured, configWord!, Key));
        }

        if (configWord != null && configWord != fallback)
        {
            Log.Warn("world", $"config {Key}={configWord} is not one of {string.Join("/", DisplayWords.AntiAliasingChoices)}; using {fallback}");
        }

        TryParse(fallback, out var method);
        return Store(new AntiAliasingPlan(method, fallback, "default"));
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved display setting. The options file is one machine's
    /// state, and a golden shot is a <c>--det</c> run against the player's own directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().AntiAliasing;

    /// <summary>Whether <paramref name="word"/> is one of
    /// <see cref="DisplayWords.AntiAliasingChoices"/>, and the method it spells.</summary>
    public static bool TryParse(string? word, out AntiAliasingMethod method)
    {
        method = AntiAliasingMethod.Off;
        if (word == null || !DisplayWords.AntiAliasingChoices.Contains(word))
        {
            return false;
        }

        method = word switch
        {
            DisplayWords.AntiAliasingFxaa => AntiAliasingMethod.Fxaa,
            DisplayWords.AntiAliasingSmaa => AntiAliasingMethod.Smaa,
            DisplayWords.AntiAliasingTaa => AntiAliasingMethod.Taa,
            DisplayWords.AntiAliasingFsr2 => AntiAliasingMethod.Fsr2,
            _ => AntiAliasingMethod.Off,
        };
        return true;
    }

    private static AntiAliasingPlan Store(AntiAliasingPlan plan)
    {
        Method = plan.Method;
        return plan;
    }
}
