using CSVM.UI.Boards;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The one cursor rule every Original screen and the pause sheet step by, over hand-placed
/// rectangles. The pause sheet's own walks pin the equal-sized case in <see cref="PauseScreensTests"/>.
/// </summary>
public class SpatialFocusTests
{
    /// <summary>Overlapping rectangles, the scrapbook's photographs, order by their centres. A
    /// tall picture whose corner stands above a short one's still lies below it, so Up from the tall
    /// one lands on the short one. Measured from corners, both presses would take the strip above.</summary>
    [Fact]
    public void OverlappingRectanglesAreMeasuredFromTheirCentres()
    {
        var stops = new (float X, float Y, float Width, float Height)?[]
        {
            (100f, 100f, 100f, 300f),
            (120f, 150f, 100f, 50f),
            (400f, 120f, 50f, 50f),
            (100f, 0f, 100f, 40f),
        };

        Assert.Equal(1, SpatialFocus.Step(stops, 0, stops[0]!.Value, 0, -1));
        Assert.Equal(0, SpatialFocus.Step(stops, 1, stops[1]!.Value, 0, 1));
    }

    /// <summary>A null stop takes no cursor, and a band with nothing ahead wraps to its far end
    /// before the press looks outside it.</summary>
    [Fact]
    public void ANullStopIsSkippedAndTheBandWrapsBeforeTheRowsBesideIt()
    {
        var stops = new (float X, float Y, float Width, float Height)?[]
        {
            (0f, 0f, 100f, 20f),
            null,
            (0f, 60f, 100f, 20f),
            (300f, 90f, 100f, 20f),
        };

        Assert.Equal(2, SpatialFocus.Step(stops, 0, stops[0]!.Value, 0, 1));
        Assert.Equal(0, SpatialFocus.Step(stops, 2, stops[2]!.Value, 0, 1));
        Assert.Equal(3, SpatialFocus.Step(stops, 2, stops[2]!.Value, 1, 0));
    }

    /// <summary>A press out of a band measures by centre, the Video page's two buttons beside its
    /// value column. A button whose top stands level with the cursor lies below it. One straddling
    /// the cursor with its centre past does too. Step, the control, skips both.</summary>
    [Fact]
    public void LeavingABandMeasuresStopsByTheirCentres()
    {
        (float X, float Y, float Width, float Height) accept = (240f, 470f, 240f, 50f);
        (float X, float Y, float Width, float Height) cancel = (240f, 520f, 240f, 50f);
        (float X, float Y, float Width, float Height) checkbox = (0f, 465f, 16f, 16f);
        (float X, float Y, float Width, float Height) foot = (0f, 530f, 70f, 17f);

        var fromCheckbox = new (float X, float Y, float Width, float Height)?[] { checkbox, accept, cancel };
        Assert.Equal(1, SpatialFocus.Leave(fromCheckbox, 0, checkbox, 0, 1));
        Assert.Equal(2, SpatialFocus.Step(fromCheckbox, 0, checkbox, 0, 1));

        var fromFoot = new (float X, float Y, float Width, float Height)?[] { foot, accept, cancel };
        Assert.Equal(2, SpatialFocus.Leave(fromFoot, 0, foot, 0, 1));
        Assert.Equal(1, SpatialFocus.Step(fromFoot, 0, foot, 0, 1));
    }
}
