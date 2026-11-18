using CSVM.Flight.Airframe;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The headlight's switch dispatch over one seat's two device halves. A control that only
/// the view cycle or only the headlight holds acts for it at once. One both hold is split by hold
/// length, a tap cycling on release and a hold switching once.</summary>
public class HeadlightTests
{
    private const float Dt = 1f / 60f;

    // Frames either side of the 250 ms split at 60 Hz (15 frames).
    private const int TapFrames = 6;
    private const int HoldFrames = 40;

    [Fact]
    public void ASharedPadButtonCyclesOnATapReleaseAndSwitchesOnceOnAHold()
    {
        var light = new Headlight();
        int cycles = 0, switches = 0;
        for (int i = 0; i < TapFrames; i++)
            Count(light.Read(false, false, true, true, Dt), ref cycles, ref switches);
        Assert.Equal((0, 0), (cycles, switches));
        Count(light.Read(false, false, false, false, Dt), ref cycles, ref switches);
        Assert.Equal((1, 0), (cycles, switches));

        cycles = switches = 0;
        for (int i = 0; i < HoldFrames; i++)
            Count(light.Read(false, false, true, true, Dt), ref cycles, ref switches);
        Count(light.Read(false, false, false, false, Dt), ref cycles, ref switches);
        Assert.Equal((0, 1), (cycles, switches));
        Assert.True(HoldFrames * Dt > TapHoldButton.PadHoldSeconds && TapFrames * Dt < TapHoldButton.PadHoldSeconds);
    }

    [Fact]
    public void AControlOnOneActionAloneActsOnItsPress()
    {
        var light = new Headlight();
        var h = light.Read(false, true, false, false, Dt);
        Assert.True(h.Switch);
        Assert.False(h.CycleKey || h.CyclePad);

        var f8 = light.Read(true, false, false, false, Dt);
        Assert.True(f8.CycleKey);
        Assert.False(f8.Switch);

        var rebound = light.Read(false, false, false, true, Dt);
        Assert.True(rebound.Switch);
        Assert.False(rebound.CyclePad);
    }

    [Fact]
    public void TheSwitchStartsOff() => Assert.False(new Headlight().On);

    // Counts the frames each level reads true; a split's tap and hold each last one frame.
    private static void Count((bool CycleKey, bool CyclePad, bool Switch) read, ref int cycles, ref int switches)
    {
        if (read.CyclePad)
            cycles++;
        if (read.Switch)
            switches++;
    }
}
