using System.Collections.Generic;
using CSVM.UI.Boards;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original screens' stepper over hand-placed rows: which rows a list's scroll bar keeps out
/// of the cursor's way. A list with rows of its own scrolls by walking off its end, so its arrows
/// are no stops. A window of text, such as the lobby's scores and chat, has nothing else to walk,
/// so its arrows stay stops.
/// </summary>
public class OriginalFocusTests
{
    [Fact]
    public void ATextOnlyWindowsArrowsStayStops()
    {
        var rows = new List<OriginalRow> { Button("ABOVE", 0f, 0f), Arrow("UP", 40f), Arrow("DOWN", 129f) };
        var lists = Window();

        Assert.True(OriginalFocus.IsStop(rows[1], rows, lists));
        Assert.True(OriginalFocus.IsStop(rows[2], rows, lists));
        Assert.Equal(1, OriginalFocus.Step(rows, lists, 0, 0, 1));
    }

    [Fact]
    public void AListWithRowsOfItsOwnTakesNoStopOnItsArrows()
    {
        var rows = new List<OriginalRow>
        {
            Button("ABOVE", 0f, 0f), Arrow("UP", 40f), Arrow("DOWN", 129f),
            new("ROW:0", "Row", OriginalRowKind.ListRow, 100f, 40f, 90f, 20f, true, 0, null),
        };
        var lists = Window();

        Assert.False(OriginalFocus.IsStop(rows[1], rows, lists));
        Assert.False(OriginalFocus.IsStop(rows[2], rows, lists));
        Assert.Equal(3, OriginalFocus.Step(rows, lists, 0, 0, 1));
    }

    private static OriginalRow Button(string key, float x, float y) =>
        new(key, key, OriginalRowKind.TextButton, x, y, 100f, 20f, true, 0, null);

    // An arrow on the window's bar, in the thumb's column inside the box's right edge.
    private static OriginalRow Arrow(string key, float y) =>
        new(key, string.Empty, OriginalRowKind.Button, 190f, y, 16f, 11f, true, 0, null);

    // A window of five rows over twelve at 100,40, its bar at x 190.
    private static IReadOnlyList<OriginalList> Window() => new[]
    {
        new OriginalList("LIST", new ListWindow(100f, 40f, 106f, 100f, 190f, 51f, 16f, 20f, 51f, 78f, 12, 5, 0), _ => { }),
    };
}
