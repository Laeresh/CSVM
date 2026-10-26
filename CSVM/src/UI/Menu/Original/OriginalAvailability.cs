using CSVM.Extraction;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// Original's availability answer, made before entry. The decoded layout loads with a top level in
/// it, and every file <see cref="OriginalAssetManifest"/> classes required is in the game files and
/// reads. A failure is one reason, the one the launcher's extraction screen names. What is optional
/// and absent comes back beside it, for the presentation to draw without.
/// </summary>
public static class OriginalAvailability
{
    /// <summary>The main-menu section's name in the layout.</summary>
    public const string MainMenuSection = "MainMenu";

    /// <summary>Where a layout art name sits in <see cref="RofFiles"/>, as the asset manifest
    /// records it, in <see cref="RofTree.Canonical"/>'s case whatever the layout's spelling. A movie
    /// sits one directory deeper, the base <c>FUN_004a7c70</c> resolves every movie name under
    /// (<c>docs/formats/cinemas.md</c>).</summary>
    public static string RelativeArtPath(string name) =>
        RofTree.Canonical(IsMovie(name) ? "ASSETS/GRAPHICS/MPG/" + name : "ASSETS/GRAPHICS/" + name);

    /// <summary>Whether a layout art name is one of the movies rather than a bitmap.</summary>
    public static bool IsMovie(string name) =>
        name != null && name.EndsWith(".mpg", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Loads the layout and checks the manifest over <paramref name="dataRoot"/>. Returns
    /// the layout when Original can run, else null with the reason.</summary>
    public static MenuLayout? Load(string dataRoot, out string? reason) =>
        Load(dataRoot, out reason, out _);

    /// <summary>The same check, also answering what is optional and absent so the caller can log
    /// once what the presentation will draw without.</summary>
    public static MenuLayout? Load(string dataRoot, out string? reason, out string? degraded)
    {
        degraded = null;
        var layout = MenuLayout.TryLoad(dataRoot, out reason);
        if (layout == null)
        {
            return null;
        }

        reason = Check(layout, dataRoot, out degraded);
        return reason == null ? layout : null;
    }

    /// <summary>Why a loaded layout cannot draw Original over <paramref name="dataRoot"/>, or null
    /// when it can.</summary>
    public static string? Check(MenuLayout layout, string dataRoot) => Check(layout, dataRoot, out _);

    /// <summary>The same answer, with the optional absences beside it.</summary>
    public static string? Check(MenuLayout layout, string dataRoot, out string? degraded)
    {
        degraded = null;
        if (layout.Screen(MainMenuSection) == null)
        {
            return $"the decoded menu layout has no [{MainMenuSection}] section";
        }

        var report = OriginalAssetManifest.Derive(layout).Check(dataRoot);
        degraded = report.Degraded;
        return report.Reason;
    }
}
