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

    /// <summary>The setting's registry entry. The render scale has no command-line flag, and its
    /// stored scale follows the anti-aliasing method whichever of the pair resolves last.</summary>
    public static readonly WordSetting Lookup = new(Key, DisplayWords.RenderScaleChoices)
    {
        SavedField = "renderScale",
        LogName = "render_scale",
        Title = "Render scale",
        Labels = DisplayWords.RenderScaleChoices.Select(w => w + "%").ToArray(),
        Detail = static _ => "Render the world below native to spare the GPU, or above it for cleaner edges. Applies at once.",
        Shown = static _ => Default,
        Offered = static (staged, enhanced) => ChoicesFor(AntiAliasingSetting.Lookup.Standing(staged, enhanced)),
        Fallback = static _ => new ResolvedWord(Default, SettingSource.Default),
        Take = static resolved => new ResolvedWord(Store(resolved, AntiAliasingSetting.Word).Word, resolved.Source),
        // The anti-aliasing method reaches the same viewports, so this one write carries both.
        Apply = static _ => ViewportQuality.ReapplyAll(),
        LogNote = static () => Clamped ? " clamped_by=fsr2" : null,
    };

    // The words at or below native, the list FSR 2.2 offers.
    private static readonly string[] AtOrBelowNative =
        DisplayWords.RenderScaleChoices.Where(w => TryParseWord(w, out float s) && s <= Native).ToArray();

    /// <summary>Resolved once at launch by <see cref="Resolve"/>, before any 3D viewport is
    /// built. A viewport takes the scale as it is constructed, and an Options apply writes it again on
    /// every live one.</summary>
    public static float Scale { get; private set; } = Native;

    /// <summary>Whether FSR 2.2 pulled the stored scale down to native.</summary>
    public static bool Clamped { get; private set; }

    /// <summary>The scale <see cref="Lookup"/> resolves <paramref name="savedWord"/> and
    /// <paramref name="configWord"/> to over native, stored as <see cref="Scale"/>. The winner is
    /// then clamped under <paramref name="antiAliasingWord"/> the way the VIDEO page clamps it
    /// (<see cref="ClampFor"/>), keeping the source that chose it.</summary>
    public static RenderScalePlan Resolve(string? savedWord, string? configWord, string? antiAliasingWord = null) =>
        Store(Lookup.Resolve(null, savedWord, configWord, Default), antiAliasingWord);

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

    // The resolved word clamped under the method, stored as Scale and Clamped.
    private static RenderScalePlan Store(ResolvedWord resolved, string? antiAliasingWord)
    {
        string clamped = ClampFor(resolved.Word, antiAliasingWord)!;
        TryParseWord(clamped, out float scale);
        Scale = scale;
        Clamped = clamped != resolved.Word;
        return new RenderScalePlan(scale, clamped, resolved.Source, Clamped);
    }
}
