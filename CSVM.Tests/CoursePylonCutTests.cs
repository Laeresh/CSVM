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
    public void TheTopTipsAndFallsAndVanishesAsItLands()
    {
        double landing = System.Math.Sqrt(2.0 * CutAt / CoursePylonCut.FallAccel);
        var mid = Fresh(0.5 * landing);
        Assert.True(mid.TopShown && mid.DropM > 0f && mid.DropM < CutAt && mid.TiltRad > 0f);
        Assert.True(Fresh(landing - 0.01).TopShown);
        var landed = Fresh(landing + 0.001);
        Assert.False(landed.TopShown);
        Assert.Equal(CutAt, landed.DropM);
        Assert.Equal(Godot.Mathf.DegToRad(CoursePylonCut.MaxTiltDeg), Fresh(CoursePylonCut.TipSeconds).TiltRad, 5);
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

    [Fact]
    public void ASoloRunRegrowsInThreeSecondsAndAFieldOfMoreThanOneInOneAndAHalf()
    {
        Assert.Equal(3.0, CoursePylonCut.RegrowFor(1));
        Assert.Equal(3.0, CoursePylonCut.RegrowSeconds);
        Assert.Equal(1.5, CoursePylonCut.RegrowFor(2));
        Assert.Equal(1.5, CoursePylonCut.RegrowFor(4));

        // The solo law is the default, look for look.
        Assert.All(Ages, a => Assert.Equal(Fresh(a), Shared(a, CoursePylonCut.RegrowFor(1))));

        // ABLE-TO-FAIL CONTROL: a field's stump still regrows just short of 1.5 s and is whole from it.
        Assert.True(Shared(1.49).StumpTopM < Height);
        Assert.True(Shared(1.49).SagM > 0f);
        Assert.Equal((CoursePylonCut.NoClipM, 0f), (Shared(1.5).StumpTopM, Shared(1.5).SagM));
        Assert.True(Fresh(1.5).StumpTopM < Height);
    }

    [Fact]
    public void AFieldsFastRegrowLeavesTheFallingTopsTimelineUnchanged()
    {
        // A cut high on a tall course pylon lands after the stump is whole: about 2.02 s for 20 m.
        const float tall = 40f;
        const float high = 20f;
        double landing = System.Math.Sqrt(2.0 * high / CoursePylonCut.FallAccel);
        Assert.True(landing > CoursePylonCut.SharedRegrowSeconds);
        foreach (double age in Ages)
        {
            var solo = CoursePylonCut.At(tall, high, CoursePylonCut.NoClipM, age);
            var field = CoursePylonCut.At(tall, high, CoursePylonCut.NoClipM, age, CoursePylonCut.SharedRegrowSeconds);
            Assert.Equal(solo.TopShown, field.TopShown);
            if (solo.TopShown)
            {
                Assert.Equal((solo.CutM, solo.TopM, solo.DropM, solo.TiltRad), (field.CutM, field.TopM, field.DropM, field.TiltRad));
            }
        }

        var standing = CoursePylonCut.At(tall, high, CoursePylonCut.NoClipM, 1.8, CoursePylonCut.SharedRegrowSeconds);
        Assert.True(standing.TopShown && standing.StumpTopM == CoursePylonCut.NoClipM && standing.SagM == 0f);
        Assert.Equal(CoursePylonCut.Whole, CoursePylonCut.At(tall, high, CoursePylonCut.NoClipM, landing + 0.01, CoursePylonCut.SharedRegrowSeconds));

        // A recut of a whole stump while the old top falls starts a fresh full-height top.
        Assert.Equal((5f, CoursePylonCut.NoClipM), CoursePylonCut.Recut(tall, 5f, standing));
    }

    private static PylonCutLook Fresh(double age) => CoursePylonCut.At(Height, CutAt, CoursePylonCut.NoClipM, age);

    private static PylonCutLook Shared(double age, double regrow = CoursePylonCut.SharedRegrowSeconds) =>
        CoursePylonCut.At(Height, CutAt, CoursePylonCut.NoClipM, age, regrow);
}
