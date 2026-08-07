using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The near-miss cue's shipped accumulator and its geometry — the half that runs
/// without an engine. The shipped values are player.json's: max 2.0, dissipation 2.0/s,
/// interval 1.0 s.</summary>
public class WarningShotCueTests
{
    [Fact]
    public void FirstPassOfASortieSounds()
    {
        var cue = new WarningShotCue(2f, 2f, 1f);
        Assert.True(cue.Ready);
        Assert.True(cue.Register());
    }

    [Fact]
    public void SecondPassInsideTheIntervalIsSilent()
    {
        var cue = new WarningShotCue(2f, 2f, 1f);
        Assert.True(cue.Register());
        cue.Tick(0.5f);
        Assert.False(cue.Register());   // 0.5 s < interval 1.0
        cue.Tick(0.6f);
        Assert.True(cue.Register());    // 1.1 s since the last cue
    }

    [Fact]
    public void IntensitySaturatesAtMaxAndDrainsAtDissipation()
    {
        var cue = new WarningShotCue(2f, 2f, 1f);
        for (int i = 0; i < 5; i++)
            cue.Register();
        Assert.Equal(2f, cue.Intensity, 4);   // five passes, capped at max
        cue.Tick(0.5f);
        Assert.Equal(1f, cue.Intensity, 4);   // 2.0/s for half a second
        cue.Tick(1f);
        Assert.Equal(0f, cue.Intensity, 4);   // floored, never negative
    }

    /// <summary>A burst walking past the canopy is ONE warning, not thirty: 30 rounds arriving over
    /// half a second sound once, the next only after the interval.</summary>
    [Fact]
    public void ABurstSoundsOnce()
    {
        var cue = new WarningShotCue(2f, 2f, 1f);
        int heard = 0;
        for (int i = 0; i < 30; i++)
        {
            if (cue.Register())
                heard++;
            cue.Tick(1f / 60f);
        }
        Assert.Equal(1, heard);
    }

    [Fact]
    public void SegmentDistanceMeasuresThePassNotTheEndpoints()
    {
        // A round crossing 3 m abeam mid-step: both endpoints are far, the pass is close. This is
        // the whole reason the swept segment is tested — a gun round covers ~8 m per 60 Hz frame.
        var a = new Vector3(-50f, 0f, 0f);
        var b = new Vector3(50f, 0f, 0f);
        var p = new Vector3(0f, 3f, 0f);
        Assert.Equal(3f, WarningShotCue.SegmentPointDistance(a, b, p), 4);
        Assert.True((p - a).Length() > 40f);
        Assert.True((p - b).Length() > 40f);
    }

    [Fact]
    public void SegmentDistanceClampsToTheEndpointsAndSurvivesAZeroStep()
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(10f, 0f, 0f);
        // Behind the segment's start: the nearest point IS the start, not the infinite line.
        Assert.Equal(5f, WarningShotCue.SegmentPointDistance(a, b, new Vector3(-5f, 0f, 0f)), 4);
        Assert.Equal(5f, WarningShotCue.SegmentPointDistance(a, b, new Vector3(15f, 0f, 0f)), 4);
        // A stationary step (dt 0, or a round spawned and killed in one frame) is a point test.
        Assert.Equal(4f, WarningShotCue.SegmentPointDistance(a, a, new Vector3(0f, 4f, 0f)), 4);
    }
}
