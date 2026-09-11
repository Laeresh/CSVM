using System;
using CSVM.Mech3.Anim;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The ramp and the scope of the cutscene fast-forward, engine-free. The rate is a remake-only
/// rule with no original behind it, so what is pinned here is the shape the player feels: it
/// spools up over a fixed span rather than jumping, comes back down over the same span, reaches
/// exactly 1 on the way back, and never leaves 1 for a definition outside the episode.
/// </summary>
public class CutsceneFastForwardTests
{
    private const float Step = 1f / 60f;

    [Fact]
    public void UnscopedStaysAtRealSpeed()
    {
        var rate = new CutsceneFastForward { Held = true };
        for (int i = 0; i < 120; i++)
        {
            rate.Ramp(Step);
        }

        Assert.Equal(1f, rate.Rate);
        Assert.False(rate.Scoped);
    }

    [Fact]
    public void RampsUpOverItsSpanAndNotBefore()
    {
        var rate = Scoped();
        rate.Held = true;
        rate.Ramp(CutsceneFastForward.RampSeconds * 0.5f);
        Assert.InRange(rate.Rate, 2.4f, 2.6f);
        rate.Ramp(CutsceneFastForward.RampSeconds * 0.5f);
        Assert.Equal(CutsceneFastForward.Target, rate.Rate, 3);
    }

    [Fact]
    public void HoldingLongerNeverPassesTheTarget()
    {
        var rate = Scoped();
        rate.Held = true;
        for (int i = 0; i < 600; i++)
        {
            rate.Ramp(Step);
        }

        Assert.Equal(CutsceneFastForward.Target, rate.Rate, 3);
    }

    [Fact]
    public void ReleaseRampsBackToExactlyOne()
    {
        var rate = Scoped();
        rate.Held = true;
        rate.Ramp(CutsceneFastForward.RampSeconds);
        rate.Held = false;
        for (int i = 0; i < 600; i++)
        {
            rate.Ramp(Step);
        }

        // Exactly 1, not near it: an unscoped definition multiplies its dt by this value, and a
        // residue here would change every other definition's playback by a hair.
        Assert.Equal(1f, rate.Rate);
    }

    [Fact]
    public void OnlyScopedDefinitionsTakeTheRate()
    {
        var inside = Def("inside");
        var outside = Def("outside");
        var rate = new CutsceneFastForward();
        rate.Scope(new[] { inside });
        rate.Held = true;
        rate.Ramp(CutsceneFastForward.RampSeconds);
        Assert.Equal(CutsceneFastForward.Target, rate.RateFor(inside), 3);
        Assert.Equal(1f, rate.RateFor(outside));
        Assert.Equal(1f, rate.RateFor(null));
    }

    [Fact]
    public void ClearDropsTheRateWithNoRamp()
    {
        var inside = Def("inside");
        var rate = new CutsceneFastForward();
        rate.Scope(new[] { inside });
        rate.Held = true;
        rate.Ramp(CutsceneFastForward.RampSeconds);
        rate.Clear();
        Assert.Equal(1f, rate.Rate);
        Assert.Equal(1f, rate.RateFor(inside));
        Assert.False(rate.Held);
        Assert.False(rate.Scoped);
    }

    private static CutsceneFastForward Scoped()
    {
        var rate = new CutsceneFastForward();
        rate.Scope(new[] { Def("scoped") });
        return rate;
    }

    // Identity is the object, so two bare definitions are enough to tell inside from outside.
    private static CSVM.Mech3.AnimDefinition Def(string name) =>
        new CSVM.Mech3.AnimDefinition { Name = name, AnimName = name };
}
