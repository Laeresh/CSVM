using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSVM.Utils;

/// <summary>One resolved render scale: the factor the 3D viewports render at, the word it was
/// spelled as, and the source that won. The source lets a log line say which source the run obeys.
/// <paramref name="Clamped"/> says FSR 2.2 pulled a scale above native down to it.</summary>
public readonly record struct RenderScalePlan(float Scale, string Word, SettingSource Source, bool Clamped = false);

/// <summary>
/// The render scale: the multiple of its own size a 3D viewport renders at. Above native the image
/// is resampled down, spending GPU headroom on edges. Below native it is upscaled, so a machine
/// without headroom (a Steam Deck) buys frame rate. The words are
/// <see cref="DisplayWords.RenderScaleChoices"/>, percentages of native. In <see cref="Lookup"/>
/// the saved option beats the <see cref="Key"/> config key, which beats native.
/// <see cref="ChoicesFor"/> narrows the choices under the anti-aliasing method.
/// <see cref="ViewportQuality"/> is the one reader of <see cref="Scale"/>. It writes nothing at
/// native under the defaults, which keeps a faithful run on Godot's own defaults.
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

    /// <summary>The source order over the percentage words. The render scale has no command-line
    /// flag.</summary>
    public static readonly WordSetting Lookup = new(Key, DisplayWords.RenderScaleChoices);

    // The words at or below native, the list FSR 2.2 offers.
    private static readonly string[] AtOrBelowNative =
        DisplayWords.RenderScaleChoices.Where(w => TryParseWord(w, out float s) && s <= Native).ToArray();

    /// <summary>Resolved once at launch by <see cref="Resolve"/>, before any 3D viewport is
    /// built. A viewport takes the scale as it is constructed. So a choice made on the Options page
    /// is saved and reaches the image on the next start, as the row's description says.</summary>
    public static float Scale { get; private set; } = Native;

    /// <summary>The scale <see cref="Lookup"/> resolves <paramref name="savedWord"/> and
    /// <paramref name="configWord"/> to over native, stored as <see cref="Scale"/>. The winner is
    /// then clamped under <paramref name="antiAliasingWord"/> the way the VIDEO page clamps it
    /// (<see cref="ClampFor"/>), keeping the source that chose it.</summary>
    public static RenderScalePlan Resolve(string? savedWord, string? configWord, string? antiAliasingWord = null)
    {
        var resolved = Lookup.Resolve(null, savedWord, configWord, Default);
        string clamped = ClampFor(resolved.Word, antiAliasingWord)!;
        TryParseWord(clamped, out float scale);
        Scale = scale;
        return new RenderScalePlan(scale, clamped, resolved.Source, Clamped: clamped != resolved.Word);
    }

    /// <summary>The render-scale words a row offers under <paramref name="antiAliasingWord"/>. FSR 2.2
    /// stops at native, since Godot's FSR modes refuse a factor above 1. Every other method takes the
    /// whole list.</summary>
    public static IReadOnlyList<string> ChoicesFor(string? antiAliasingWord) =>
        antiAliasingWord == DisplayWords.AntiAliasingFsr2 ? AtOrBelowNative : DisplayWords.RenderScaleChoices;

    /// <summary><paramref name="scaleWord"/>, or native where <paramref name="antiAliasingWord"/> is
    /// FSR 2.2 and the scale stands above native. A null word stays null, which already reads as
    /// native. Picking FSR 2.2 on the VIDEO page moves the scale through this, and
    /// <see cref="Resolve"/> clamps the saved pair the same way.</summary>
    public static string? ClampFor(string? scaleWord, string? antiAliasingWord) =>
        antiAliasingWord == DisplayWords.AntiAliasingFsr2 && TryParseWord(scaleWord, out float scale) && scale > Native
            ? Default
            : scaleWord;

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (<see cref="WordSetting.ReadSaved"/>).</summary>
    public static string? SavedWord(bool det) => WordSetting.ReadSaved(det, static o => o.RenderScale);

    /// <summary>Whether <paramref name="word"/> is one of
    /// <see cref="DisplayWords.RenderScaleChoices"/>, and the factor it spells. False leaves
    /// <paramref name="scale"/> at <see cref="Native"/>, which is how a caller tells "not a word I
    /// know" from a deliberate 100.</summary>
    public static bool TryParseWord(string? word, out float scale)
    {
        scale = Native;
        if (!Lookup.IsWord(word))
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
}
