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
/// A log line names that source.</summary>
public readonly record struct AntiAliasingPlan(AntiAliasingMethod Method, string Word, SettingSource Source);

/// <summary>
/// The anti-aliasing method the 3D viewports run, one of
/// <see cref="DisplayWords.AntiAliasingChoices"/>. <see cref="Lookup"/> reads the saved
/// <c>antiAliasing</c> option, then the <see cref="Key"/> config key, then
/// <see cref="DefaultFor"/> the graphics mode. It is a display setting, so a chosen method is
/// written whichever mode won. Only the default follows the mode.
/// <see cref="ViewportQuality"/> is the one reader of <see cref="Method"/>. It
/// writes nothing for <see cref="AntiAliasingMethod.Off"/>, which keeps a faithful run on Godot's
/// own defaults. FSR 2.2 caps the render scale at native, which
/// <see cref="RenderScaleSetting.ClampFor"/> applies.
/// </summary>
public static class AntiAliasingSetting
{
    /// <summary>The config key under the saved option, one of the five words.</summary>
    public const string Key = "graphics.antiAliasing";

    /// <summary>The source order over the five words. Anti-aliasing has no command-line flag.</summary>
    public static readonly WordSetting Lookup = new(Key, DisplayWords.AntiAliasingChoices);

    /// <summary>Resolved once at launch by <see cref="Resolve"/>, before any 3D viewport is
    /// built, so every viewport in a run takes the same method.</summary>
    public static AntiAliasingMethod Method { get; private set; } = AntiAliasingMethod.Off;

    /// <summary>The word a launch with nothing saved and no config key runs. Off keeps the faithful
    /// mode on its own image, and the enhanced mode runs TAA.</summary>
    public static string DefaultFor(bool enhanced) =>
        enhanced ? DisplayWords.AntiAliasingTaa : DisplayWords.AntiAliasingOff;

    /// <summary>The method <see cref="Lookup"/> resolves <paramref name="savedWord"/> and
    /// <paramref name="configWord"/> to over <see cref="DefaultFor"/> the mode, stored as
    /// <see cref="Method"/>.</summary>
    public static AntiAliasingPlan Resolve(string? savedWord, string? configWord, bool enhanced)
    {
        var resolved = Lookup.Resolve(null, savedWord, configWord, DefaultFor(enhanced));
        TryParse(resolved.Word, out var method);
        Method = method;
        return new AntiAliasingPlan(method, resolved.Word, resolved.Source);
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (<see cref="WordSetting.ReadSaved"/>).</summary>
    public static string? SavedWord(bool det) => WordSetting.ReadSaved(det, static o => o.AntiAliasing);

    /// <summary>Whether <paramref name="word"/> is one of
    /// <see cref="DisplayWords.AntiAliasingChoices"/>, and the method it spells.</summary>
    public static bool TryParse(string? word, out AntiAliasingMethod method)
    {
        method = AntiAliasingMethod.Off;
        if (!Lookup.IsWord(word))
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
}
