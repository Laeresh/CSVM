using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="RocketTriggerLatch"/>: the rocket trigger's consumed-press latch, armed the
/// instant flight regains input after a cutscene skip or a pause-menu Resume so the button that
/// confirmed it cannot also read as the rocket trigger's next pull. The arm takes no button
/// reading, so the first read after it is what decides whether a press was in progress.
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
    public void ArmingWithTheButtonUpCostsTheNextPullNothing()
    {
        var latch = new RocketTriggerLatch();

        latch.Arm();
        Assert.False(latch.Read(false)); // the first read finds nothing down and clears the latch
        Assert.True(latch.Read(true));   // so a real pull fires
    }

    [Fact]
    public void HoldingThroughASkipSuppressesUntilTheButtonIsReleased()
    {
        var latch = new RocketTriggerLatch();

        latch.Arm();                     // the skip press is still down as flight regains input
        Assert.False(latch.Read(true));  // same press: no launch
        Assert.False(latch.Read(true));  // still held: still no launch
        Assert.False(latch.Read(false)); // released: the latch clears here
        Assert.True(latch.Read(true));   // a fresh pull launches as before
    }

    [Fact]
    public void HoldingThroughAResumeSuppressesUntilTheButtonIsReleased()
    {
        var latch = new RocketTriggerLatch();

        latch.Arm();                     // the pause board's Resume confirm is still down
        Assert.False(latch.Read(true));
        Assert.False(latch.Read(false));
        Assert.True(latch.Read(true));
    }

    [Fact]
    public void ASecondArmWhileTheSamePressIsDownChangesNothing()
    {
        var latch = new RocketTriggerLatch();

        latch.Arm();                     // the skip hands flight back
        Assert.False(latch.Read(true));
        latch.Arm();                     // and the pause board's resume edge arms again
        Assert.False(latch.Read(true));
        Assert.False(latch.Read(false));
        Assert.True(latch.Read(true));
    }
}
