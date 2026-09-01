using System.IO;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The minimal availability check the Original presentation makes before it can be selected:
/// the decoded layout file reads, its main-menu section is there, and the art that section names
/// is on disk. A failure is a reason for the host to select Built-in for this run and leave the
/// saved request alone. The full required/optional asset manifest replaces this later; what is
/// checked here is exactly what the top level cannot be drawn without.
/// </summary>
public static class OriginalAvailability
{
    /// <summary>The main-menu section's name in the layout.</summary>
    public const string MainMenuSection = "MainMenu";

    /// <summary>The rows the top level is composed from: the two panes and the six buttons.</summary>
    public static readonly string[] MainMenuKeys =
    {
        "MM_LOGO", "BFRAME", "MM_B_CAMPAIGN", "MM_B_INSTANTACTION", "MM_B_MULTIPLAYER",
        "MM_B_PREFERENCES", "MM_B_CREDITS", "MM_B_QUIT",
    };

    /// <summary>Where a layout art name resolves under a data root.</summary>
    public static string ArtPath(string dataRoot, string name) =>
        Path.Combine(dataRoot, "extracted", "rof", "ASSETS", "GRAPHICS", name);

    /// <summary>Loads the layout and checks the top level's needs. Returns the layout when
    /// Original can run, else null with the first reason found.</summary>
    public static MenuLayout? Load(string dataRoot, out string? reason)
    {
        var layout = MenuLayout.TryLoad(MenuLayout.PathUnder(dataRoot), out reason);
        if (layout == null)
        {
            return null;
        }

        reason = Check(layout, dataRoot);
        return reason == null ? layout : null;
    }

    /// <summary>Why a loaded layout cannot draw the top level over <paramref name="dataRoot"/>,
    /// or null when it can.</summary>
    public static string? Check(MenuLayout layout, string dataRoot)
    {
        var screen = layout.Screen(MainMenuSection);
        if (screen == null)
        {
            return $"the decoded menu layout has no [{MainMenuSection}] section";
        }

        foreach (string key in MainMenuKeys)
        {
            var widget = screen.Widget(key);
            if (widget == null)
            {
                return $"[{MainMenuSection}] has no {key} row";
            }

            foreach (string art in widget.Art)
            {
                if (!File.Exists(ArtPath(dataRoot, art)))
                {
                    return $"{key} names {art}, which is not under extracted/rof/ASSETS/GRAPHICS";
                }
            }
        }

        return null;
    }
}
