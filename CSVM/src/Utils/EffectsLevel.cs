using System;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The original's graphics EffectsLevel option, the detail setting that scales every clutter
/// fade. Its <c>detail.zrd</c> default is HIGH on any CPU over 600 MHz, so HIGH is the default
/// here too, overridable through the <c>graphics.effectsLevel</c> config key.
/// Decode: docs/formats/templates.md.
/// ⚠ HIGH is level 0 and the scale GROWS as the level drops: the engine multiplies it into the
/// squared camera distance, so MEDIUM fades clutter at half the authored metres and LOW at a
/// third. HIGH leaves the authored metres literal.
/// </summary>
public static class EffectsLevel
{
    /// <summary>The config key: <c>"high"</c>, <c>"medium"</c> or <c>"low"</c>.</summary>
    public const string Key = "graphics.effectsLevel";

    /// <summary>The shipped default on every machine this remake runs on.</summary>
    public const string Default = "high";

    /// <summary>Whether the authored short-range clutter fade applies at all. The original's fade
    /// is a performance measure, so this is the opt-out for a machine that would rather draw
    /// clutter out to the fog; default on, which is the decoded behaviour.</summary>
    public const string FadeKey = "graphics.clutterFarFade";

    /// <summary>The default for <see cref="FadeKey"/>: the fade the data authors.</summary>
    public const bool FadeDefault = true;

    /// <summary>The global shader uniform the level drives; declared in
    /// <c>csky_clutter_fade.gdshaderinc</c>, registered once by the launcher.</summary>
    public const string ShaderParam = "csky_clutter_fade_scale_sq";

    /// <summary>The squared distance scale a level word sets, exactly the engine's own constants
    /// (1.0, 4.0, 9.0). False for a word the original does not know.</summary>
    public static bool TryClutterFadeScaleSq(string level, out float scaleSq)
    {
        switch (level.Trim().ToLowerInvariant())
        {
            case "high":
                scaleSq = 1f;
                return true;
            case "medium":
                scaleSq = 4f;
                return true;
            case "low":
                scaleSq = 9f;
                return true;
            default:
                scaleSq = 1f;
                return false;
        }
    }

    /// <summary>Whether the authored fade applies, per <see cref="FadeKey"/>.</summary>
    public static bool ClutterFarFadeEnabled() => Config.GetBool(FadeKey, FadeDefault);

    /// <summary>The scale a switch state and a level word run at, the whole resolution off Config.
    /// ⚠ Off is 0, a NEVER-fades scale rather than a fade-everything one: the shader multiplies it
    /// into the squared camera distance, so nothing ever reaches its near² and every stamp keeps
    /// full alpha and its full card, the same arm the engine's own far² of 0 takes.</summary>
    public static float ClutterFadeScaleSq(bool farFade, string level)
    {
        if (!farFade)
        {
            return 0f;
        }
        TryClutterFadeScaleSq(level, out float scaleSq);
        return scaleSq;
    }

    /// <summary>The configured scale, saying so when the level word is one the original does not
    /// know.</summary>
    public static float ResolveClutterFadeScaleSq()
    {
        bool farFade = ClutterFarFadeEnabled();
        string level = Config.GetString(Key, Default);
        if (farFade && !TryClutterFadeScaleSq(level, out _))
        {
            Log.Warn("world", $"config {Key}={level} is not high/medium/low; using {Default}");
        }
        return ClutterFadeScaleSq(farFade, level);
    }
}
