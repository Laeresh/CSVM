using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The cover a session starts under, off engine: it holds opaque until the session says its first
/// real frame is ready, then fades up from a dark tone over a second, clamps the step a blocking
/// build hands it, releases itself on a cap rather than stranding a player, and covers nothing at
/// all under <c>--det</c>.
/// </summary>
public class StartCoverTests
{
    /// <summary>Opaque from the first frame, and for as long as the session is not ready, however
    /// many frames go by. This is the whole point: no frame of the world assembling.</summary>
    [Fact]
    public void TheCoverHoldsOpaqueUntilTheSessionIsReady()
    {
        var cover = new StartCover(det: false);

        Assert.Equal(1f, cover.Alpha);
        for (int frame = 0; frame < 60; frame++)
        {
            cover.Advance(1f / 60f, ready: false);
            Assert.Equal(1f, cover.Alpha);
        }

        Assert.False(cover.Released);
        Assert.False(cover.Finished);
    }

    /// <summary>The frame the session first reads ready still draws the cover opaque, so the fade
    /// starts on that frame rather than a frame of the world slipping out before it.</summary>
    [Fact]
    public void TheFrameThatReadsReadyStillDrawsOpaque()
    {
        var cover = new StartCover(det: false);

        cover.Advance(1f / 60f, ready: true);

        Assert.True(cover.Released);
        Assert.False(cover.TimedOut);
        Assert.Equal(1f, cover.Alpha);
    }

    /// <summary>A linear ramp over the fade's own second, gone at its end and never past 0.
    /// </summary>
    [Fact]
    public void TheFadeRunsLinearlyToNothingOverItsOwnLength()
    {
        var cover = new StartCover(det: false);
        cover.Advance(0.05f, ready: true);

        Advance(cover, StartCover.FadeSeconds * 0.5f);
        Assert.Equal(0.5f, cover.Alpha, 2);
        Assert.False(cover.Finished);

        Advance(cover, StartCover.FadeSeconds * 0.25f);
        Assert.Equal(0.25f, cover.Alpha, 2);

        Advance(cover, StartCover.FadeSeconds);
        Assert.Equal(0f, cover.Alpha);
        Assert.True(cover.Finished);

        // Past the end it stays gone rather than going negative or wrapping.
        Advance(cover, StartCover.FadeSeconds);
        Assert.Equal(0f, cover.Alpha);
    }

    /// <summary>A build is one synchronous block, so the frame closing over it carries the whole
    /// build as its delta. Unclamped, that one frame would spend the entire fade.</summary>
    [Fact]
    public void OneHugeFrameSpendsAtMostOneClampedStep()
    {
        var cover = new StartCover(det: false);
        cover.Advance(0.05f, ready: true);

        cover.Advance(12f, ready: true);

        Assert.Equal(1f - (StartCover.MaxStepSeconds / StartCover.FadeSeconds), cover.Alpha, 3);
        Assert.False(cover.Finished);
    }

    /// <summary>The hold cap, so a session that never reports a first frame cannot leave a player
    /// looking at an opaque screen. It reads as the cap, not as a ready session.</summary>
    [Fact]
    public void TheHoldCapReleasesACoverNoSessionEverAnswers()
    {
        var cover = new StartCover(det: false);

        int steps = 0;
        while (!cover.Released && steps < 1000)
        {
            cover.Advance(StartCover.MaxStepSeconds, ready: false);
            steps++;
        }

        Assert.True(cover.TimedOut);
        Assert.InRange(
            cover.HeldSeconds,
            StartCover.MaxHoldSeconds,
            StartCover.MaxHoldSeconds + StartCover.MaxStepSeconds);
        Assert.Equal(1f, cover.Alpha);
    }

    /// <summary>Under <c>--det</c> nothing is covered on any frame: a pinned golden hashes the
    /// frame this would paint over, and <c>--frames=N</c> counts from the first world frame.
    /// </summary>
    [Fact]
    public void ADeterministicRunIsNeverCovered()
    {
        var cover = new StartCover(det: true);

        Assert.False(cover.Enabled);
        Assert.Equal(0f, cover.Alpha);
        Assert.True(cover.Finished);

        Advance(cover, 5f);
        Assert.Equal(0f, cover.Alpha);
        Assert.False(cover.Released);
        Assert.False(cover.TimedOut);
        Assert.True(cover.Finished);
    }

    /// <summary>A frame that advanced no time changes nothing, so a paused or stepped session
    /// neither ages the hold nor moves the fade.</summary>
    [Fact]
    public void AnEmptyFrameMovesNothing()
    {
        var cover = new StartCover(det: false);

        cover.Advance(0f, ready: false);
        cover.Advance(-1f, ready: false);

        Assert.Equal(0f, cover.HeldSeconds);
        Assert.False(cover.Released);
    }

    /// <summary>The tone is dark and is not black, which is what the original comes up from. Its
    /// blue channel sits over the other two, so the cast is towards the sky rather than warm.
    /// </summary>
    [Fact]
    public void TheToneIsDarkAndNotBlack()
    {
        Assert.True(StartCover.ToneR > 0f && StartCover.ToneG > 0f && StartCover.ToneB > 0f);
        Assert.True(StartCover.ToneR < 0.15f && StartCover.ToneG < 0.15f && StartCover.ToneB < 0.15f);
        Assert.Equal(StartCover.ToneR, StartCover.ToneG);
        Assert.True(StartCover.ToneB > StartCover.ToneR);
        Assert.Equal(1f, StartCover.FadeSeconds);
    }

    // Frames at 60 Hz, the rate the cover is judged at, rather than one step of the whole span.
    private static void Advance(StartCover cover, float seconds, bool ready = true)
    {
        for (float spent = 0f; spent < seconds; spent += 1f / 60f)
        {
            cover.Advance(1f / 60f, ready);
        }
    }
}
