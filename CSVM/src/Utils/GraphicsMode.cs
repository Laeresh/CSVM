using System;

namespace CSVM.Utils;

/// <summary>
/// The opt-in enhanced-lighting mode's setting, modeled on <see cref="EffectsLevel"/>: a
/// <c>graphics.*</c> word read once at launch and resolved to the single boolean scene builders
/// consult. Its user-facing home is <see cref="OptionsStore"/>'s <c>graphicsMode</c> field, which
/// both Options screens write; the <c>graphics.mode</c> config key stays under it as the
/// hand-edited source. Readers take <see cref="Enhanced"/> rather than either, so the layering is
/// this module's business alone.
/// </summary>
public static class GraphicsMode
{
    /// <summary>The config key, and the word both the flag and the saved option carry:
    /// <c>"original"</c> or <c>"enhanced"</c>.</summary>
    public const string Key = "graphics.mode";

    /// <summary>The opt-in word, the counterpart of <see cref="Default"/>.</summary>
    public const string EnhancedWord = "enhanced";

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

    /// <summary>Resolve <see cref="Enhanced"/> once, in the order the presentation option uses:
    /// <paramref name="cliOverride"/> (<c>--graphics=</c>), then <paramref name="savedOption"/>
    /// (the Options screens' saved word), then the <see cref="Key"/> config key, then
    /// <see cref="Default"/>. An unknown word at any layer warns and falls back.
    /// ⚠ The caller passes no <paramref name="savedOption"/> under <c>--det</c>: it is one
    /// machine's state, exactly what a deterministic capture must not depend on.</summary>
    public static bool Resolve(string? cliOverride, string? savedOption = null)
    {
        string mode = cliOverride
            ?? (string.IsNullOrEmpty(savedOption) ? Config.GetString(Key, Default) : savedOption);
        if (!TryParse(mode, out bool enhanced))
        {
            Log.Warn("world", $"config {Key}={mode} is not original/enhanced; using {Default}");
            TryParse(Default, out enhanced);
        }
        Enhanced = enhanced;
        return enhanced;
    }

    /// <summary>A live switch, after launch: G in flight or an Options apply, both saved. ⚠ Only the launcher calls
    /// this, since the flag alone moves nothing already built; its switch then rewrites the
    /// shaders, the lights and the session to match.</summary>
    public static void Set(bool enhanced) => Enhanced = enhanced;
}
