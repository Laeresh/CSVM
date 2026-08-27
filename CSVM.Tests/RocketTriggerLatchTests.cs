using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="RocketTriggerLatch"/> (BL-556): the rocket trigger's consumed-press latch, armed the
/// instant flight regains input after a cutscene skip or a pause-menu Resume so the button that
/// confirmed it cannot also read as the rocket trigger's next pull.
/// </summary>
public class RocketTriggerLatchTests
{
    [Fact]
    public void AFreshPullWithNothingArmedReadsHeld()
    {
        var latch = new RocketTriggerLatch();

        Assert.True(latch.Read(true));
    }

    [Fact]
    public void ArmingWithTheButtonUpIsANoOp()
    {
        var latch = new RocketTriggerLatch();

        latch.ArmIfHeld(false); // nothing was down to consume
        Assert.True(latch.Read(true)); // a real pull still fires
    }

    [Fact]
    public void HoldingThroughASkipSuppressesUntilTheButtonIsReleased()
    {
        var latch = new RocketTriggerLatch();

        latch.ArmIfHeld(true); // the skip press is still down as flight regains input
        Assert.False(latch.Read(true)); // same press: no launch
        Assert.False(latch.Read(true)); // still held: still no launch
        Assert.False(latch.Read(false)); // released: the latch clears here
        Assert.True(latch.Read(true)); // a fresh pull launches as before
    }

    [Fact]
    public void HoldingThroughAResumeSuppressesUntilTheButtonIsReleased()
    {
        var latch = new RocketTriggerLatch();

        latch.ArmIfHeld(true); // the pause board's Resume confirm is still down
        Assert.False(latch.Read(true));
        Assert.False(latch.Read(false));
        Assert.True(latch.Read(true));
    }
}
