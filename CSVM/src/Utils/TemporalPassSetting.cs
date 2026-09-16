namespace CSVM.Utils;

/// <summary>
/// Which temporal pass the enhanced presentation runs: Godot's own TAA, or FSR 2.2 at native
/// resolution, which carries a temporal pass of its own and replaces Godot's rather than joining
/// it. A trial door while the two are flown against each other, so it is one <c>graphics.temporal</c>
/// config key with no saved option and no menu row, and the loser is deleted rather than kept as a
/// second way in. <see cref="ViewportQuality"/> is the one reader of <see cref="Fsr2"/>, and the
/// pass reaches the image under <see cref="GraphicsMode.Enhanced"/> alone.
/// </summary>
public static class TemporalPassSetting
{
    /// <summary>The config key, and the two words it carries.</summary>
    public const string Key = "graphics.temporal";

    /// <summary>The shipped default, Godot's own temporal anti-aliasing.</summary>
    public const string Default = "taa";

    /// <summary>The alternative word, AMD FidelityFX Super Resolution 2.2 at native.</summary>
    public const string Fsr2Word = "fsr2";

    /// <summary><b>Resolved once at launch</b> by <see cref="Resolve"/>, before any 3D viewport is
    /// built, so every viewport in a run takes the same pass. ⚠ A deterministic run reads the
    /// shipped default: the config file is dropped under <c>--det</c> like every other key, and no
    /// flag stands above it.</summary>
    public static bool Fsr2 { get; private set; }

    /// <summary>Whether <paramref name="word"/> is one of the two this key knows, and whether it
    /// spells FSR 2.2.</summary>
    public static bool TryParse(string word, out bool fsr2)
    {
        switch (word.Trim().ToLowerInvariant())
        {
            case Default:
                fsr2 = false;
                return true;
            case Fsr2Word:
                fsr2 = true;
                return true;
            default:
                fsr2 = false;
                return false;
        }
    }

    /// <summary>Resolve <see cref="Fsr2"/> from <paramref name="configWord"/>, warning and falling
    /// back to <see cref="Default"/> on a word this key does not know. Returns the word that won,
    /// for the launcher's graphics line.</summary>
    public static string Resolve(string configWord)
    {
        if (!TryParse(configWord, out bool fsr2))
        {
            Log.Warn("world", $"config {Key}={configWord} is not {Default}/{Fsr2Word}; using {Default}");
        }

        Fsr2 = fsr2;
        return fsr2 ? Fsr2Word : Default;
    }
}
