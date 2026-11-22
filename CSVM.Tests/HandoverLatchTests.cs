using CSVM.Bindings;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The handover latch: a control held through a handover reads released until let go. Its
/// <c>Any</c> lets the load gate's readers skip the walk once every held control is up.
/// </summary>
public class HandoverLatchTests
{
    [Fact]
    public void AnArmedHeldActionReadsReleasedUntilLetGo()
    {
        var latch = new HandoverLatch();

        latch.Arm(InputAction.ThrottleUp);

        Assert.True(latch.Any);
        Assert.False(latch.Read(InputAction.ThrottleUp, true));
        Assert.False(latch.Read(InputAction.ThrottleUp, false));
        Assert.False(latch.Any);
        Assert.True(latch.Read(InputAction.ThrottleUp, true));
    }

    [Fact]
    public void ArmingTwiceIsOneArmAndOtherActionsReadThrough()
    {
        var latch = new HandoverLatch();

        latch.Arm(InputAction.Pause);
        latch.Arm(InputAction.Pause);

        Assert.True(latch.Read(InputAction.FireGuns, true));
        Assert.False(latch.Read(InputAction.Pause, false));
        Assert.False(latch.Any);
    }
}
