using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>How far the headset's tunnel vignette closes in while the view turns.</summary>
public enum VrVignette
{
    /// <summary>No vignette, the shipped default.</summary>
    Off,

    /// <summary>A light restriction.</summary>
    Mild,

    /// <summary>A narrow tunnel.</summary>
    Strong,
}

/// <summary>
/// The headset's comfort vignette as a word: off, mild or strong. It dims the edge of view while the
/// aircraft turns the view (<c>UI/Boards/XrVignette.cs</c> holds the curve). The flag beats the
/// saved word, which beats off, and no deterministic run reads the file. The composition sets
/// <see cref="Current"/> at boot and from the options apply, and <c>UI/Boards/ComfortQuads.cs</c>
/// reads it every frame. docs/architecture/Utils.md.
/// </summary>
public static class VrVignetteSetting
{
    /// <summary>The flag that beats the saved word.</summary>
    public const string Flag = "--xr-vignette";

    /// <summary>The word a launch with nothing saved runs: none, since it is a choice the player
    /// opts into.</summary>
    public const string Default = "off";

    /// <summary>The words, in <see cref="VrVignette"/>'s order, which is the order a row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { "off", "mild", "strong" };

    /// <summary>The lookup over <see cref="Words"/>. No config key is read.</summary>
    public static readonly WordSetting Lookup = new("xr.vignette", Words, Flag);

    private static readonly string[] LabelWords = { "Off", "Mild", "Strong" };

    /// <summary>The vignette the running process draws.</summary>
    public static VrVignette Current { get; set; }

    /// <summary>The level <paramref name="word"/> spells, <see cref="VrVignette.Off"/> for a word
    /// outside <see cref="Words"/>.</summary>
    public static VrVignette LevelOf(string? word) =>
        Lookup.IndexOf(word) is int at and >= 0 ? (VrVignette)at : VrVignette.Off;

    /// <summary>The word the sources resolve to: <paramref name="flagWord"/>, then
    /// <paramref name="savedWord"/>, then <see cref="Default"/>.</summary>
    public static ResolvedWord Resolve(string? flagWord, string? savedWord) =>
        Lookup.Resolve(flagWord, savedWord, null, Default);

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (the <c>--det</c> rule <see cref="WordSettings.ReadSaved"/> keeps).</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().VrVignette;

    /// <summary>How a row shows <paramref name="word"/>: a never-set one as Off.</summary>
    public static string Label(string? word) => LabelWords[(int)LevelOf(word)];

    /// <summary>The labels in row order.</summary>
    public static IReadOnlyList<string> Labels() => LabelWords;
}
