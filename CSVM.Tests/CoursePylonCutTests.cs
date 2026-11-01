using System.Linq;
using CSVM.Flight.Modes;
using Xunit;

namespace CSVM.Tests;

public class CoursePylonCutTests
{
    private const float Height = 30f;
    private const float CutAt = 12f;

    private static readonly double[] Ages = Enumerable.Range(0, 301).Select(i => i * 0.01).ToArray();

    [Fact]
    public void ThePylonIsWholeBeforeTheCutAndFromTheRegrowTimeOn()
    {
        Assert.Equal(CoursePylonCut.Whole, Fresh(-0.01));
        Assert.Equal(CoursePylonCut.Whole, Fresh(CoursePylonCut.RegrowSeconds));
        Assert.Equal(CoursePylonCut.Whole, Fresh(100.0));
        Assert.NotEqual(CoursePylonCut.Whole, Fresh(CoursePylonCut.RegrowSeconds - 0.01));
        Assert.False(CoursePylonCut.Whole.TopShown);
    }

    [Fact]
    public void AtTheCutTheStumpEndsAtTheCutAndTheWholeTopHangsAboveIt()
    {
        var look = Fresh(0.0);
        Assert.Equal(CutAt, look.StumpTopM);
        Assert.Equal(CutAt, look.CutM);
        Assert.Equal(CoursePylonCut.NoClipM, look.TopM);
        Assert.Equal(0f, look.DropM);
        Assert.Equal(0f, look.TiltRad);
        Assert.Equal(0f, look.SagM);
        Assert.True(look.TopShown);
    }

    [Fact]
    public void TheStumpSagsHoldsAndRegrowsToTheFullHeight()
    {
        float last = CutAt;
        foreach (double age in Ages.Where(a => a < CoursePylonCut.RegrowSeconds))
        {
            var look = Fresh(age);
            Assert.True(look.StumpTopM >= last && look.StumpTopM <= Height, $"stump {look.StumpTopM} at {age}");
            Assert.True(look.SagM >= 0f && look.SagM <= CoursePylonCut.SagShare * CutAt, $"sag {look.SagM} at {age}");
            last = look.StumpTopM;
        }

        Assert.Equal(CutAt, Fresh(CoursePylonCut.HoldSeconds).StumpTopM);
        Assert.Equal(CoursePylonCut.SagShare * CutAt, Fresh(CoursePylonCut.SagInSeconds).SagM, 4);
        Assert.True(last > Height - 0.01f);
        Assert.True(Fresh(CoursePylonCut.RegrowSeconds - 0.01).SagM < 0.01f);
    }

    [Fact]
    public void TheTopFallsTipsAndFadesOutAndComesToRestOnTheGround()
    {
        var mid = Fresh(0.5 * CoursePylonCut.FadeSeconds);
        Assert.True(mid.TopShown && mid.DropM > 0f && mid.TiltRad > 0f && mid.Fade is > 0f and < 1f);
        var gone = Fresh(CoursePylonCut.FadeSeconds);
        Assert.False(gone.TopShown);
        Assert.Equal(1f, gone.Fade);
        Assert.Equal(Godot.Mathf.DegToRad(CoursePylonCut.MaxTiltDeg), gone.TiltRad, 5);
        Assert.All(Ages, a => Assert.True(Fresh(a).DropM <= CutAt));
        Assert.Equal(1f, CoursePylonCut.At(Height, 1f, CoursePylonCut.NoClipM, 2.0).DropM);
    }

    [Fact]
    public void AWholePylonIsCutAtTheTouchClampedToItsHeight()
    {
        Assert.Equal((CutAt, CoursePylonCut.NoClipM), CoursePylonCut.Recut(Height, CutAt, CoursePylonCut.Whole));
        Assert.Equal((Height, CoursePylonCut.NoClipM), CoursePylonCut.Recut(Height, Height + 2f, CoursePylonCut.Whole));
        Assert.Equal((0f, CoursePylonCut.NoClipM), CoursePylonCut.Recut(Height, -1f, CoursePylonCut.Whole));
    }

    [Fact]
    public void ARegrowingPylonIsCutWithinItsStumpAndAboveItDropsNothing()
    {
        var now = Fresh(1.5);
        var (cut, top) = CoursePylonCut.Recut(Height, 5f, now);
        Assert.Equal(5f, cut);
        Assert.Equal(now.StumpTopM, top);
        Assert.True(CoursePylonCut.At(Height, cut, top, 0.0).TopShown);

        (cut, top) = CoursePylonCut.Recut(Height, Height - 1f, now);
        Assert.Equal(now.StumpTopM, cut);
        Assert.False(CoursePylonCut.At(Height, cut, top, 0.0).TopShown);
    }

    private static PylonCutLook Fresh(double age) => CoursePylonCut.At(Height, CutAt, CoursePylonCut.NoClipM, age);
}
