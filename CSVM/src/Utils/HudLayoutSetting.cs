using System.Collections.Generic;

namespace CSVM.Utils;

/// <summary>Where the outside views place the dial columns, compass tape and throttle plate.</summary>
public enum HudLayoutKind
{
    /// <summary>The original's placement, unchanged.</summary>
    Original,

    /// <summary>The ready-made preset with the instruments toward the screen sides.</summary>
    Sides,

    /// <summary>Your own placement, set in the layout editor and measured from the pane's edges.</summary>
    Custom,
}

/// <summary>
/// The HUD layout as a word, <c>original</c>, <c>sides</c> or <c>custom</c>, saved as
/// <c>hudLayout</c> in the options file. The custom placement is saved beside it as
/// <c>hudLayoutCustom</c> (<see cref="HudLayout.Format"/>). A remake-only setting apart from the HUD
/// mode: the saved word beats Original, and no deterministic run reads the file. The launch and every
/// options apply set <see cref="Current"/> and <see cref="Custom"/>; the layout editor saves both. Each
/// pilot pane follows them every frame (<c>Flight.Hud.FlightHud.SetHudLayout</c>).
/// docs/architecture/Utils.md.
/// </summary>
public static class HudLayoutSetting
{
    /// <summary>The word a launch with nothing saved runs, the original's placement.</summary>
    public const string Default = "original";

    /// <summary>The words, in the order a row offers them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { "original", "sides", "custom" };

    /// <summary>The lookup over <see cref="Words"/>. No config key is read and no flag beats it.</summary>
    public static readonly WordSetting Lookup = new("hud.layout", Words);

    private static readonly string[] LabelWords = { "Original", "Sides", "Custom" };

    /// <summary>The layout the running process places its outside views' instruments by, Original
    /// until a launch, an apply or the editor stores another.</summary>
    public static HudLayoutKind Current { get; set; } = HudLayoutKind.Original;

    /// <summary>The saved custom placement, or null while none was saved, which places Custom as the
    /// original does until the editor saves one.</summary>
    public static HudLayout? Custom { get; set; }

    /// <summary>The kind <paramref name="word"/> spells, Original for a null word or one outside
    /// <see cref="Words"/>.</summary>
    public static HudLayoutKind KindOf(string? word)
    {
        int at = Lookup.IndexOf(word);
        return at >= 0 ? (HudLayoutKind)at : HudLayoutKind.Original;
    }

    /// <summary>The saved word for <paramref name="kind"/>.</summary>
    public static string WordOf(HudLayoutKind kind) => Words[(int)kind];

    /// <summary>The message-stack line a saved edit posts, e.g. <c>HUD Layout: Custom</c>.</summary>
    public static string Line(HudLayoutKind kind) => "HUD Layout: " + LabelWords[(int)kind];

    /// <summary>The word the sources resolve to: <paramref name="savedWord"/>, then
    /// <see cref="Default"/>.</summary>
    public static ResolvedWord Resolve(string? savedWord) => Lookup.Resolve(null, savedWord, null, Default);

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>
    /// (the <c>--det</c> rule <see cref="WordSettings.ReadSaved"/> keeps).</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().HudLayout;

    /// <summary>The saved custom placement a launch reads, or null under <paramref name="det"/>.</summary>
    public static string? SavedCustom(bool det) => det ? null : OptionsStore.UserOptions().Load().HudLayoutCustom;

    /// <summary>The launch's read: stores the saved word and custom placement and logs them. Under
    /// <paramref name="det"/> it reads neither, so a deterministic launch runs Original.</summary>
    public static void ApplySaved(bool det) => Apply(SavedWord(det), SavedCustom(det));

    /// <summary>An options apply's read: the saved word and placement, as <see cref="ApplySaved"/>.
    /// Under <paramref name="det"/> it changes nothing, so an unrelated accept never overwrites the
    /// layout the run's own editor saved. Returns whether it applied.</summary>
    public static bool ApplyAfterOptions(bool det)
    {
        if (det)
        {
            return false;
        }

        ApplySaved(det: false);
        return true;
    }

    /// <summary>Stores <paramref name="word"/>'s kind as <see cref="Current"/> and
    /// <paramref name="custom"/>'s placement as <see cref="Custom"/> (null when it does not parse), and
    /// logs them.</summary>
    public static void Apply(string? word, string? custom)
    {
        var resolved = Resolve(word);
        Current = KindOf(resolved.Word);
        Custom = HudLayout.TryParse(custom, out var layout) ? layout : null;
        Log.Info("flight", $"hud layout={resolved.Word} source={Lookup.SourceName(resolved.Source)} custom={(Custom == null ? "none" : "saved")}");
    }

    /// <summary>The editor's save: Custom becomes <see cref="Current"/> with <paramref name="layout"/>
    /// as its placement, and both are written to <paramref name="store"/>. A null store (a <c>--det</c>
    /// run) changes the running layout and writes nothing.</summary>
    public static void SaveCustom(HudLayout layout, OptionsStore? store)
    {
        Current = HudLayoutKind.Custom;
        Custom = layout.InPane();
        if (store != null)
        {
            var options = store.Load();
            options.HudLayout = WordOf(HudLayoutKind.Custom);
            options.HudLayoutCustom = layout.Format();
            store.Save(options);
        }

        Log.Info("flight", $"hud layout=custom saved={(store != null)} placement={layout.Format()}");
    }

    /// <summary>The labels in row order.</summary>
    public static IReadOnlyList<string> Labels() => LabelWords;
}
