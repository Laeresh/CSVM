using System.Globalization;
using Godot;

namespace CSVM.Utils;

/// <summary>One resolved frame pacing: whether the loop waits for the screen, the cap it runs to
/// (<see cref="VSyncSetting.Uncapped"/> for none), and the source that won, named so a log line
/// can say which of the three layers the run is obeying.</summary>
public readonly record struct VSyncPlan(bool Enabled, int MaxFps, string Source);

/// <summary>
/// The frame pacing: whether the frame loop waits for the screen, and the cap it runs to when it
/// does not. One setting carries both, since a cap only means anything with V-Sync off, so the
/// words are <see cref="DisplayWords.VSyncChoices"/> and the ones that parse as integers are caps
/// in frames per second. The sources layer the way <see cref="GraphicsMode"/> layers its own:
/// <c>--no-vsync</c>, then the saved <c>vsync</c> option, then the <c>display.vsync</c> config key,
/// then V-Sync off. <see cref="Apply"/> is the only place the two engine calls are made.
/// </summary>
public static class VSyncSetting
{
    /// <summary>The config key under the saved option: true turns V-Sync on for a machine whose
    /// options file does not say, false is what its absence reads as.</summary>
    public const string Key = "display.vsync";

    /// <summary>The config key's value where the file does not carry it, which is also the
    /// setting's own default, so a machine with neither file runs uncapped and unpaced.</summary>
    public const bool ConfigDefault = false;

    /// <summary>The word a launch with nothing saved runs under, off with no cap, the same pacing
    /// <see cref="Resolve"/> lands on with no saved word and the config key at its default.</summary>
    public const string Default = DisplayWords.VSyncOff;

    /// <summary>The <see cref="VSyncPlan.MaxFps"/> of a loop under no cap, Godot's own spelling of
    /// "as fast as it renders".</summary>
    public const int Uncapped = 0;

    /// <summary>The pacing the three sources resolve to, highest first: <paramref name="flagOff"/>
    /// (<c>--no-vsync</c>), then <paramref name="savedWord"/>, then <paramref name="configOn"/>,
    /// then off. A saved word this vocabulary does not know reads as never set and falls through,
    /// the same contract <see cref="OptionsStore"/> validates the field under. A config key reading
    /// on is reported under the key's name, since on is the one value a file has to set.</summary>
    public static VSyncPlan Resolve(bool flagOff, string? savedWord, bool configOn)
    {
        if (flagOff)
        {
            return new VSyncPlan(false, Uncapped, "--no-vsync");
        }

        if (TryParseWord(savedWord, out bool savedOn, out int savedCap))
        {
            return new VSyncPlan(savedOn, savedCap, "options.json");
        }

        return configOn ? new VSyncPlan(true, Uncapped, Key) : new VSyncPlan(false, Uncapped, "default");
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved display setting: options.json is one machine's state
    /// and a golden shot is a <c>--det</c> run against the player's own options directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().VSync;

    /// <summary>Whether <paramref name="word"/> is one of <see cref="DisplayWords.VSyncChoices"/>,
    /// and what it means: on, off, or off at the cap it spells. False leaves both outputs
    /// meaningless, which is how a caller tells "not a word I know" from "off".</summary>
    public static bool TryParseWord(string? word, out bool enabled, out int maxFps)
    {
        enabled = true;
        maxFps = Uncapped;
        switch (word)
        {
            case DisplayWords.VSyncOn:
                return true;
            case DisplayWords.VSyncOff:
                enabled = false;
                return true;
            case null:
                return false;
            default:
                enabled = false;
                return int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out maxFps) && maxFps > 0;
        }
    }

    /// <summary>Applies <paramref name="plan"/> to the window and the frame loop and logs the
    /// source that won. Both engine calls live here, so the startup read and an Options apply
    /// cannot drift apart; nothing else in the tree assigns <see cref="Engine.MaxFps"/>.
    /// ⚠ The cap is a render rate and reaches no simulation. <c>GameClock.ParentDriven</c> is
    /// false in realtime, so the session steps the sim from the physics callback rather than once
    /// per rendered frame, and a capped frame loop leaves the flight model untouched.</summary>
    public static void Apply(VSyncPlan plan)
    {
        DisplayServer.WindowSetVsyncMode(plan.Enabled
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = plan.MaxFps;
        if (plan.Enabled)
        {
            Log.Info("perf", $"vsync on source={plan.Source}, frame/fps/script are floored at the refresh interval");
        }
        else if (plan.MaxFps > Uncapped)
        {
            Log.Info("perf", $"vsync off source={plan.Source} max_fps={plan.MaxFps}, frame/fps/script are floored at the cap, not the refresh");
        }
        else
        {
            Log.Info("perf", $"vsync off source={plan.Source} max_fps=0, frame/fps/script report work done, not a refresh cap");
        }
    }
}
