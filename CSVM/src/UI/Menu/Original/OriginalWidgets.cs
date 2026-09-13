using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.UI.Menu.Original;

/// <summary>The layout-widget readings more than one Original screen module needs: the slot number a
/// numbered widget key carries, and where a section's background pane lands on the board. Held here
/// because a module is a sealed class of its own, so a rule two of them follow can live in neither.
/// The open-dropdown rule is the other such reading, in <c>OriginalDropList.cs</c>.</summary>
internal static class OriginalWidgets
{
    // A slot number a keyed widget carries after a shared prefix, or null for a key from another
    // family or a plain key with none. The ammunition, pylon, zone and paint fields are numbered
    // this way.
    internal static int? Indexed(string key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return int.TryParse(key.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
    }

    // Where a section's pane lands on the board, which for art smaller than the board is centred
    // rather than left at the corner it is authored at. PLANENAME.SCRIPT initializes
    // pn_p_background with relative = 1 and then sets the screen's own location to
    // ((getresx() - its width) / 2, (getresy() - its height) / 2); the messagebox and the loadout
    // section do the same, the 410x300 pane landing on the 195,150 the shots measure. A pane that
    // fills the board centres onto its own corner, and one authored away from the corner keeps it.
    internal static (float X, float Y) PaneOrigin(
        MenuLayoutScreen screen, string key, Func<string, (int Width, int Height)?> measure)
    {
        if (screen.Widget(key) is not { Art.Count: > 0 } pane)
        {
            return (0f, 0f);
        }

        float x = pane.Int("X");
        float y = pane.Int("Y");
        if (x != 0f || y != 0f || measure(pane.Art[0]) is not { } size)
        {
            return (x, y);
        }

        return (
            Math.Max(0f, (float)Math.Floor((BoardFit.AuthoredWidth - size.Width) / 2f)),
            Math.Max(0f, (float)Math.Floor((BoardFit.AuthoredHeight - size.Height) / 2f)));
    }

    internal static void AddPane(
        MenuLayoutScreen screen, List<BoardPicture> pictures, string key, Func<string, (int Width, int Height)?> measure)
    {
        if (screen.Widget(key) is { Art.Count: > 0 } pane)
        {
            var at = PaneOrigin(screen, key, measure);
            pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, pane.Art[0], Math.Max(1, pane.Frames)), at.X, at.Y));
        }
    }
}
