using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The mouse a flight seat takes from the desktop: the guard that decides whether it may
/// take one at all, and the virtual cursor that stands in for the OS pointer while it holds it. The
/// property the whole port rests on is that a relative motion stream accumulates to exactly the
/// cursor the absolute read used to give, so the stick, its dead bands and the hold-to-look read the
/// same under capture as off it.</summary>
public class MouseCaptureTests
{
    /// <summary>The guard: a session takes the mouse only on a real display with somebody at the
    /// controls, so the hidden test desktop's suites and every <c>--det</c> run keep the mouse mode
    /// the harness started them with.</summary>
    [Fact]
    public void Allowed_IsOffUnderDetAndOnEveryScriptedRun()
    {
        Assert.True(MouseCapture.Allowed(realDisplay: true, det: false, scripted: false));
        Assert.False(MouseCapture.Allowed(realDisplay: true, det: true, scripted: false));
        Assert.False(MouseCapture.Allowed(realDisplay: true, det: false, scripted: true));
        Assert.False(MouseCapture.Allowed(realDisplay: false, det: false, scripted: false));
        // The test desktop is a REAL display, so the scripted arm alone is what spares it.
        Assert.False(MouseCapture.Allowed(realDisplay: true, det: true, scripted: true));
    }

    /// <summary>A relative stream lands the virtual cursor where the OS pointer would have stood,
    /// and the stick reads exactly the offset the absolute read gave there.</summary>
    [Fact]
    public void StepCursor_AccumulatesARelativeStreamToTheAbsoluteCursor()
    {
        var pane = new Vector2(800f, 600f);
        var capture = new MouseCapture();
        capture.Take(new Vector2(400f, 300f));

        capture.Moved(new Vector2(40f, -30f));
        capture.Moved(new Vector2(60f, -20f));
        capture.Moved(new Vector2(20f, -30f));
        var cursor = capture.StepCursor(pane);

        Assert.Equal(new Vector2(520f, 220f), cursor);
        var half = pane * 0.5f;
        Assert.Equal(MouseFlight.Offset(new Vector2(520f, 220f), half, half),
            MouseFlight.Offset(cursor, half, half));
    }

    /// <summary>Each frame folds only that frame's travel, so the cursor walks the same path a
    /// pointer would rather than re-applying what it has already taken.</summary>
    [Fact]
    public void StepCursor_ConsumesThisFramesTravelOnly()
    {
        var pane = new Vector2(800f, 600f);
        var capture = new MouseCapture();
        capture.Take(new Vector2(100f, 100f));

        capture.Moved(new Vector2(10f, 10f));
        Assert.Equal(new Vector2(110f, 110f), capture.StepCursor(pane));
        Assert.Equal(new Vector2(110f, 110f), capture.StepCursor(pane));
        capture.Moved(new Vector2(5f, 0f));
        Assert.Equal(new Vector2(115f, 110f), capture.StepCursor(pane));
    }

    /// <summary>The confinement the window used to give: pushing past an edge saturates there and
    /// one step back moves at once, rather than paying off a debt the pane never had.</summary>
    [Fact]
    public void StepCursor_ConfinesTheCursorToThePane()
    {
        var pane = new Vector2(800f, 600f);
        var capture = new MouseCapture();
        capture.Take(new Vector2(400f, 300f));

        capture.Moved(new Vector2(5000f, -5000f));
        Assert.Equal(new Vector2(800f, 0f), capture.StepCursor(pane));
        capture.Moved(new Vector2(-30f, 30f));
        Assert.Equal(new Vector2(770f, 30f), capture.StepCursor(pane));

        capture.Moved(new Vector2(-5000f, 5000f));
        Assert.Equal(new Vector2(0f, 600f), capture.StepCursor(pane));
    }

    /// <summary>A pane with no extent pins the cursor at its corner rather than clamping to a
    /// negative span, which is what a seat asked for its stick before its viewport has a size would
    /// otherwise do.</summary>
    [Fact]
    public void StepCursor_ReadsACornerWithNoPaneToMeasure()
    {
        var capture = new MouseCapture();
        capture.Take(new Vector2(400f, 300f));

        capture.Moved(new Vector2(10f, 10f));

        Assert.Equal(Vector2.Zero, capture.StepCursor(Vector2.Zero));
    }

    /// <summary>The seed: the virtual cursor starts where the real one stood, so the frame the
    /// capture begins reads the same stick as the frame before it.</summary>
    [Fact]
    public void Take_SeedsTheVirtualCursorWhereTheRealOneStood()
    {
        var capture = new MouseCapture();

        capture.Take(new Vector2(613f, 42f));

        Assert.True(capture.Holding);
        Assert.Equal(new Vector2(613f, 42f), capture.Cursor);
        Assert.Equal(new Vector2(613f, 42f), capture.StepCursor(new Vector2(800f, 600f)));
    }

    /// <summary>Head-look's half: the travel since the last read, cleared by the read, so a frame
    /// nobody looks on banks nothing towards the next one.</summary>
    [Fact]
    public void TakeLook_ReturnsTheTravelSinceTheLastReadAndClearsIt()
    {
        var capture = new MouseCapture();
        capture.Take(Vector2.Zero);

        capture.Moved(new Vector2(3f, -4f));
        capture.Moved(new Vector2(1f, 1f));

        Assert.Equal(new Vector2(4f, -3f), capture.TakeLook());
        Assert.Equal(Vector2.Zero, capture.TakeLook());
    }

    /// <summary>The stick's cursor and head-look's delta are fed from the same events and consumed
    /// apart, so reading one does not empty the other.</summary>
    [Fact]
    public void Moved_FeedsTheCursorAndTheLookIndependently()
    {
        var capture = new MouseCapture();
        capture.Take(new Vector2(100f, 100f));

        capture.Moved(new Vector2(20f, 10f));
        var look = capture.TakeLook();
        var cursor = capture.StepCursor(new Vector2(800f, 600f));

        Assert.Equal(new Vector2(20f, 10f), look);
        Assert.Equal(new Vector2(120f, 110f), cursor);
    }

    /// <summary>Nothing is banked while nothing is held, which is what makes an uncaptured session
    /// read exactly as it did before there was a capture at all.</summary>
    [Fact]
    public void Moved_BanksNothingWhileNothingIsHeld()
    {
        var capture = new MouseCapture();

        capture.Moved(new Vector2(500f, 500f));

        Assert.False(capture.Holding);
        Assert.Equal(Vector2.Zero, capture.TakeLook());
    }

    /// <summary>The release drops the pending travel, so the next capture starts from where the real
    /// cursor then is instead of replaying the motion that ended the last one.</summary>
    [Fact]
    public void Release_DropsThePendingTravel()
    {
        var capture = new MouseCapture();
        capture.Take(new Vector2(100f, 100f));
        capture.Moved(new Vector2(50f, 50f));

        capture.Release();
        capture.Take(new Vector2(400f, 300f));

        Assert.Equal(Vector2.Zero, capture.TakeLook());
        Assert.Equal(new Vector2(400f, 300f), capture.StepCursor(new Vector2(800f, 600f)));
    }
}
