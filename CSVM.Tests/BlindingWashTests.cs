using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.UI.Boards;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The target marker under a sonic or flash wash (<see cref="TargetHud.Blinds"/>). It hides for as
/// long as the wash shows, from the first weight the original's 32-level blend paints. The tests read
/// it over the wash's envelope and the burst's intensity curve. Rule: <c>docs/org/targeting.md</c>,
/// "Under a blinding wash".
/// </summary>
public class BlindingWashTests
{
    private const float Dt = 1f / 60f;

    private static readonly Color White = new(1f, 1f, 1f);

    [Fact]
    public void TheFloorIsTheFirstOfThirtyTwoBlendLevels()
    {
        Assert.False(TargetHud.Blinds(0f));
        Assert.False(TargetHud.Blinds(0.03f));
        Assert.True(TargetHud.Blinds(1f / 32f));
        Assert.True(TargetHud.Blinds(1f));
    }

    [Fact]
    public void ADeadCentreFlashHidesTheMarkerFromItsAttackToItsCut()
    {
        // The flash caller's arguments for a dead-centre hit: weight 1 for 5 s, after 1 s.
        var wash = new BlendWash();
        wash.Start(White, 1f, 5f, startDelay: 1f);

        StepFor(wash, 0.9f);
        Assert.False(TargetHud.Blinds(wash.Weight)); // the delay paints nothing

        StepFor(wash, 0.15f); // the attack's first frames
        Assert.True(TargetHud.Blinds(wash.Weight));

        // The release sheds the peak toward 1/e of it, so the wash still shows when it is cut.
        while (wash.Running)
        {
            Assert.True(TargetHud.Blinds(wash.Weight));
            wash.Step(Dt);
        }

        Assert.False(TargetHud.Blinds(wash.Weight));
    }

    [Fact]
    public void AWeakHitNearTheRadiusHidesTheMarkerForItsWholeWash()
    {
        // Past d = 0.8 R squared the fade takes over half the intensity: at 0.82 it leaves 0.45.
        Assert.True(DisablingIntensity.TryResolve(0.82f, 1f, requiresFacing: false, 0f,
            out float intensity, out float seconds));
        var wash = new BlendWash();
        wash.Start(White, intensity, seconds, startDelay: 1f);
        StepFor(wash, 1.1f);
        while (wash.Running)
        {
            Assert.True(TargetHud.Blinds(wash.Weight));
            wash.Step(Dt);
        }
    }

    [Fact]
    public void AHitAtTheRimTheOriginalPaintsNoColourForHidesNothing()
    {
        // At 0.99 R squared the intensity is 0.025, under the first blend level.
        Assert.True(DisablingIntensity.TryResolve(0.99f, 1f, requiresFacing: false, 0f,
            out float intensity, out float seconds));
        var wash = new BlendWash();
        wash.Start(White, intensity, seconds, startDelay: 1f);
        while (wash.Running)
        {
            Assert.False(TargetHud.Blinds(wash.Weight));
            wash.Step(Dt);
        }
    }

    private static void StepFor(BlendWash wash, float seconds)
    {
        int steps = (int)System.Math.Round(seconds / Dt);
        for (int i = 0; i < steps; i++)
            wash.Step(Dt);
    }
}
