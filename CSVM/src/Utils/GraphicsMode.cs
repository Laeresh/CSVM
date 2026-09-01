using System;

namespace CSVM.Utils;

/// <summary>
/// The opt-in enhanced-lighting mode's config key, modeled on <see cref="EffectsLevel"/>: a
/// <c>graphics.*</c> word read once at launch and resolved to the single boolean scene builders
/// consult. A future options-store layer (the menu plan's process-wide settings file) is the
/// intended future source of this key's user-facing value; readers stay on <see cref="Enhanced"/>
/// rather than on Config so that swap costs nothing here.
/// </summary>
public static class GraphicsMode
{
    /// <summary>The config key: <c>"original"</c> or <c>"enhanced"</c>.</summary>
    public const string Key = "graphics.mode";

    /// <summary>The shipped default: the faithful recreation, unchanged from every prior build.</summary>
    public const string Default = "original";

    /// <summary><b>Resolved once at launch</b> by <see cref="Resolve"/>, before any scene builds;
    /// every later reader (SceneBuilder's shader keys, the lighting rig) takes this rather than
    /// querying Config itself, so one resolution point can gain an options-store layer later
    /// without touching its readers.</summary>
    public static bool Enhanced { get; private set; }

    /// <summary>Whether a mode word is one the original vocabulary knows.</summary>
    public static bool TryParse(string mode, out bool enhanced)
    {
        switch (mode.Trim().ToLowerInvariant())
        {
            case "original":
                enhanced = false;
                return true;
            case "enhanced":
                enhanced = true;
                return true;
            default:
                enhanced = false;
                return false;
        }
    }

    /// <summary>Resolve <see cref="Enhanced"/> once: <paramref name="cliOverride"/> (from
    /// <c>--graphics=</c>) wins outright when given, since it is asked for explicitly and must
    /// survive a <c>--det</c> run's <see cref="Config.ClearOverrides"/>; otherwise the config key,
    /// with the default rule EffectsLevel uses (an unknown word warns and falls back).</summary>
    public static bool Resolve(string? cliOverride)
    {
        string mode = cliOverride ?? Config.GetString(Key, Default);
        if (!TryParse(mode, out bool enhanced))
        {
            Log.Warn("world", $"config {Key}={mode} is not original/enhanced; using {Default}");
            TryParse(Default, out enhanced);
        }
        Enhanced = enhanced;
        return enhanced;
    }
}
