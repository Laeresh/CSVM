using CSVM.Flight;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// BL-184 (CAP-18): the weapon-gauge pointer sweeps at a single measured constant rate —
/// 168.7 °/sim-s — toward the selected belt slot, routed the shortest way round, and snaps
/// instead of sweeping in when it has no prior pose (NaN: gauge just appeared, or a respawn
/// cleared it via <c>GaugeCluster.Reset</c>). Moved from the in-engine <c>gauge-arrow-tween</c>
/// suite — every member here is a pure static with no engine dependency.
/// </summary>
public class GaugeArrowTweenTests
{
    [Fact]
    public void TargetAngleStepsEvenlyAroundTheRing()
    {
        Assert.Equal(0f, GaugeCluster.TargetArrowAngle(4, 0));
        Assert.Equal(-90f, GaugeCluster.TargetArrowAngle(4, 1)); // 4 positions, 90° apart
        Assert.Equal(-45f, GaugeCluster.TargetArrowAngle(8, 1)); // 8 positions, 45° apart
        Assert.Equal(0f, GaugeCluster.TargetArrowAngle(0, 0)); // no positions targets 0°, never NaN/inf
    }

    [Fact]
    public void ANaNPoseSnapsToTargetInsteadOfSweepingIn()
    {
        Assert.Equal(-90f, GaugeCluster.TweenArrow(float.NaN, -90f, 0.5f));
    }

    /// <summary>A 90° step at 168.7 °/sim-s is 533 ms of pure interior-rate sim time; one 16.6 ms
    /// sim-step (1/60 s) advances it by exactly the rate — neither clamped early nor overshooting.
    /// CAP-18's end-to-end capture reads ~633 ms for the same step because it also carries a
    /// ~97 ms ease unimplemented here — the Approach was a constant-rate tween with no easing, and
    /// the ease's own shape is only known as "not a smoothstep", not measured well enough to build
    /// (a lead, not a finding); the gap is real and owed a follow-up if the capture A/B this item
    /// still owes reads as visibly wrong at the ends.</summary>
    [Fact]
    public void OneSimStepAdvancesByRateTimesDt()
    {
        float step = GaugeCluster.ArrowSweepDegPerSimS * (1f / 60f);
        float afterOneStep = GaugeCluster.TweenArrow(0f, 90f, 1f / 60f);
        Assert.Equal(step, afterOneStep, 3);
    }

    [Fact]
    public void ANinetyDegreeSweepSpansAboutFiveHundredThirtyThreeMillisecondsSim()
    {
        float angle = 0f;
        int steps = 0;
        while (!Mathf.IsEqualApprox(angle, 90f) && steps < 200)
        {
            angle = GaugeCluster.TweenArrow(angle, 90f, 1f / 60f);
            steps++;
        }
        float simMs = steps * (1000f / 60f);
        Assert.Equal(90f, angle, 3);
        Assert.InRange(simMs, 510f, 560f);
    }

    /// <summary>Shortest-way wrap: 170° → -170° is only 20° apart going UP through the ±180° seam,
    /// 340° apart going down through 0° — the step must move toward 180°, not back toward 0°.</summary>
    [Fact]
    public void TheShortestWayWrapStepsUpThroughTheSeam()
    {
        float wrapped = GaugeCluster.TweenArrow(170f, -170f, 1f / 60f);
        Assert.True(wrapped > 170f, $"the shortest-way wrap steps up through the ±180° seam rather than back through 0°, got {wrapped:0.00}");
    }
}
