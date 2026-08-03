using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Tap-vs-hold timing for <see cref="HoldToRepeat"/>: a tap alone never repeats, holding past
/// the initial delay repeats on the declared interval (zero meaning "every tick"), and releasing
/// mid-delay resets the timer so the next press waits the full delay again.
/// </summary>
public class HoldToRepeatTests
{
    // ---- a tap (press, a few short ticks, release) never repeats ----

    [Fact]
    public void TapAloneNeverRepeats()
    {
        var hold = new HoldToRepeat(initialDelay: 0.3f, repeatInterval: 0f);

        hold.Press();
        Assert.False(hold.Tick(0.05f));
        Assert.False(hold.Tick(0.05f));
        hold.Release();
        Assert.False(hold.Tick(1f)); // released: further ticks are inert
    }

    // ---- holding past the delay fires every tick when repeatInterval is zero ----

    [Fact]
    public void HoldPastDelayRepeatsEveryTickAtZeroInterval()
    {
        var hold = new HoldToRepeat(initialDelay: 0.3f, repeatInterval: 0f);

        hold.Press();
        Assert.False(hold.Tick(0.2f)); // 0.2s in: still short of the 0.3s delay
        Assert.True(hold.Tick(0.2f));  // 0.4s in: delay cleared, first repeat fires
        Assert.True(hold.Tick(0.016f)); // every subsequent frame repeats too
        Assert.True(hold.Tick(0.016f));
    }

    // ---- a nonzero repeatInterval paces repeats instead of firing every tick ----

    [Fact]
    public void HoldPastDelayPacesByRepeatInterval()
    {
        var hold = new HoldToRepeat(initialDelay: 0.3f, repeatInterval: 0.1f);

        hold.Press();
        Assert.False(hold.Tick(0.25f)); // still short of the 0.3s delay
        Assert.True(hold.Tick(0.1f));   // delay cleared: first repeat fires
        Assert.False(hold.Tick(0.03f)); // short of the 0.1s repeat interval
        Assert.True(hold.Tick(0.1f));   // interval elapsed: repeat fires again
    }

    // ---- releasing mid-delay and pressing again waits the full delay, not a remainder ----

    [Fact]
    public void ReleaseMidDelayResetsTheTimer()
    {
        var hold = new HoldToRepeat(initialDelay: 0.3f, repeatInterval: 0f);

        hold.Press();
        Assert.False(hold.Tick(0.25f)); // most of the delay elapsed
        hold.Release();

        hold.Press(); // re-press starts the delay over, not from the leftover 0.05s
        Assert.False(hold.Tick(0.25f));
        Assert.True(hold.Tick(0.06f));
    }
}
