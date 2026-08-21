using CSVM.UI;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The d-pad shape driving every menu cursor, via <see cref="MenuInput.StepAxis"/> over a real
/// <see cref="HoldToRepeat"/>: a fresh press fires immediately, a held direction repeats after the
/// initial delay at the repeat interval, a direction flip fires immediately with a fresh delay,
/// releasing resets the delay, and idle input produces nothing. The device reads themselves are
/// unreachable here; this is the timing rule alone.
/// </summary>
public class MenuInputTests
{
    private const float Delay = 0.42f;
    private const float Interval = 0.12f;
    private const float Frame = 0.016f;

    [Fact]
    public void FreshPressFiresImmediately()
    {
        var repeat = new HoldToRepeat(Delay, Interval);
        int prev = 0;

        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, Frame));
        Assert.Equal(1, prev);
    }

    [Fact]
    public void HeldDirectionRepeatsAfterDelayAtInterval()
    {
        var repeat = new HoldToRepeat(Delay, Interval);
        int prev = 0;

        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, Frame));
        Assert.Equal(0, MenuInput.StepAxis(1, ref prev, repeat, 0.3f));   // inside the delay
        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, 0.12f));  // delay spent exactly
        Assert.Equal(0, MenuInput.StepAxis(1, ref prev, repeat, 0.05f));  // inside the interval
        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, 0.07f));  // interval elapsed
    }

    [Fact]
    public void DirectionFlipFiresImmediatelyWithAFreshDelay()
    {
        var repeat = new HoldToRepeat(Delay, Interval);
        int prev = 0;

        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, Frame));
        Assert.Equal(0, MenuInput.StepAxis(1, ref prev, repeat, 0.4f));   // most of the delay spent
        Assert.Equal(-1, MenuInput.StepAxis(-1, ref prev, repeat, Frame)); // the flip fires now
        Assert.Equal(0, MenuInput.StepAxis(-1, ref prev, repeat, 0.3f));  // and re-armed the FULL delay
        Assert.Equal(-1, MenuInput.StepAxis(-1, ref prev, repeat, 0.2f));
    }

    [Fact]
    public void ReleaseThenRepressWaitsTheFullDelayAgain()
    {
        var repeat = new HoldToRepeat(Delay, Interval);
        int prev = 0;

        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, Frame));
        Assert.Equal(0, MenuInput.StepAxis(1, ref prev, repeat, 0.4f));   // most of the delay spent
        Assert.Equal(0, MenuInput.StepAxis(0, ref prev, repeat, Frame));  // let go
        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, Frame));  // re-press fires
        Assert.Equal(0, MenuInput.StepAxis(1, ref prev, repeat, 0.3f));   // not a leftover 0.02s
        Assert.Equal(1, MenuInput.StepAxis(1, ref prev, repeat, 0.2f));
    }

    [Fact]
    public void IdleProducesNothing()
    {
        var repeat = new HoldToRepeat(Delay, Interval);
        int prev = 0;

        Assert.Equal(0, MenuInput.StepAxis(0, ref prev, repeat, 1f));
        Assert.Equal(0, MenuInput.StepAxis(0, ref prev, repeat, 1f));
        Assert.Equal(0, prev);
    }
}
