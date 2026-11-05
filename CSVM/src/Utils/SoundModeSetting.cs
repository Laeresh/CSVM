using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>
/// The sound mode and the Hit Confirm switch, the AUDIO page's two remake-only rows. Original Sound
/// plays the original's sounds as authored; Enhanced Sound remixes the engine and hit feedback
/// (<c>Flight.Audio.EnhancedSoundTuning</c> holds the amounts). Hit Confirm never set reads as on
/// under Enhanced Sound and off under Original Sound. No deterministic run reads the file, so a
/// <c>--det</c> run plays Original Sound with no hit confirm. The launch and every options apply set
/// the two flags, and the flight reads them live. docs/architecture/Utils.md.
/// </summary>
public static class SoundModeSetting
{
    /// <summary>The word for the original's sounds as authored.</summary>
    public const string Original = "original";

    /// <summary>The word for the remix.</summary>
    public const string Enhanced = "enhanced";

    /// <summary>The word a launch with nothing saved plays.</summary>
    public const string Default = Enhanced;

    /// <summary>Every word the options file may carry, with Original first. The row is a checkbox, ticked
    /// for <see cref="Enhanced"/>.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Original, Enhanced };

    /// <summary>Whether the running process plays Enhanced Sound. Off until a launch or an apply
    /// sets it, so a suite or a tool that never resolves the option hears Original Sound.</summary>
    public static bool EnhancedOn { get; set; }

    /// <summary>Whether the running process plays the hit confirm. Off until a launch or an apply
    /// sets it.</summary>
    public static bool HitConfirmOn { get; set; }

    /// <summary>Whether <paramref name="word"/> is one of <see cref="Words"/>.</summary>
    public static bool IsWord(string? word) => word is Original or Enhanced;

    /// <summary>Whether <paramref name="savedWord"/> plays Enhanced Sound: the saved word, else
    /// <see cref="Default"/>. A word outside <see cref="Words"/> reads as never set.</summary>
    public static bool IsEnhanced(string? savedWord) => (IsWord(savedWord) ? savedWord : Default) == Enhanced;

    /// <summary>Whether the hit confirm plays: the saved switch, else on exactly when the mode is
    /// Enhanced Sound.</summary>
    public static bool HitConfirmOf(bool? savedHitConfirm, bool enhanced) => savedHitConfirm ?? enhanced;

    /// <summary>Sets both flags from the saved pair.</summary>
    public static void Apply(string? savedWord, bool? savedHitConfirm)
    {
        EnhancedOn = IsEnhanced(savedWord);
        HitConfirmOn = HitConfirmOf(savedHitConfirm, EnhancedOn);
    }

    /// <summary>Sets both flags from <paramref name="saved"/>, the options file read here when null,
    /// or Original Sound with no hit confirm under <paramref name="det"/>, and logs them. The launch
    /// and every options apply call it after the file is written.</summary>
    public static void ApplySaved(bool det, OptionsDef? saved = null)
    {
        if (det)
        {
            EnhancedOn = false;
            HitConfirmOn = false;
        }
        else
        {
            saved ??= OptionsStore.UserOptions().Load();
            Apply(saved.SoundMode, saved.HitConfirm);
        }

        Log.Info("sound", $"sound mode={(EnhancedOn ? Enhanced : Original)} hitConfirm={(HitConfirmOn ? "on" : "off")}{(det ? " (--det)" : string.Empty)}");
    }
}
