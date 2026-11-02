using System;
using System.Linq;
using CSVM.Flight.Modes;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class CourseMotionTests
{
    private static readonly CourseGate RingA = new("ring-a", CourseGateKind.BalloonRing, new Vector3(150f, 80f, -400f), 45f, 0f, 0f, 40f);
    private static readonly CourseGate Pylons = new("p1", CourseGateKind.PylonGate, new Vector3(0f, 12f, -200f), 30f, 40f, 30f, 0f);

    private static readonly double[] Times = Enumerable.Range(0, 2000).Select(i => i * 0.05).ToArray();

    [Fact]
    public void APylonGateStandsAtRest()
    {
        foreach (double t in Times)
        {
            Assert.Equal(Transform3D.Identity, CourseMotion.Offset(Pylons, t));
            Assert.Equal(CourseMotion.Rest(Pylons), CourseMotion.Pose(Pylons, t));
        }
    }

    [Fact]
    public void TheGateShapeStandsThePylonsAWidthApartAndTheBalloonsClearOfTheRing()
    {
        Assert.Equal(new Vector3(-20f, 0f, 0f), CourseGateShape.PylonBase(Pylons, 0));
        Assert.Equal(new Vector3(20f, 0f, 0f), CourseGateShape.PylonBase(Pylons, 1));
        float outer = (0.5f * RingA.Diameter) + (2f * CourseGateShape.TubeRadius(RingA));
        for (int i = 0; i < CourseGateShape.BalloonAnglesDeg.Length; i++)
        {
            var centre = CourseGateShape.BalloonCentre(RingA, i);
            Assert.Equal(0f, centre.Z);
            Assert.True(centre.Y > 0f);
            Assert.True(centre.Length() - CourseGateShape.BalloonRadius(RingA) * CourseGateShape.BalloonStretch > outer,
                $"balloon {i} reaches into the ring");
        }
    }

    [Fact]
    public void TheRestPoseFacesTheGatesForward()
    {
        var rest = CourseMotion.Rest(RingA);

        Assert.Equal(RingA.Position, rest.Origin);
        Assert.True((-rest.Basis.Z).IsEqualApprox(RingA.Forward));
    }

    [Fact]
    public void TheSameGateAndTimeGiveTheSamePose()
    {
        foreach (double t in Times)
        {
            Assert.Equal(CourseMotion.Pose(RingA, t), CourseMotion.Pose(RingA with { }, t));
        }
    }

    [Fact]
    public void ARingMovesWithinItsBounds()
    {
        float reach = MathF.Sqrt((CourseMotion.BobM * CourseMotion.BobM) + (CourseMotion.DriftM * CourseMotion.DriftM));
        float roll = Mathf.DegToRad(CourseMotion.RollDeg);
        float maxSeen = 0f;
        foreach (double t in Times)
        {
            var offset = CourseMotion.Offset(RingA, t);
            Assert.Equal(0f, offset.Origin.Z);
            Assert.True(offset.Origin.Length() <= reach + 1e-5f);
            // A roll about the ring's own axis: the axis and so the scoring plane's normal never turn.
            Assert.True(offset.Basis.Z.IsEqualApprox(Vector3.Back));
            Assert.True(MathF.Abs(offset.Basis.GetEuler().Z) <= roll + 1e-5f);
            maxSeen = MathF.Max(maxSeen, offset.Origin.Length());
        }

        Assert.True(maxSeen > 0.5f * CourseMotion.BobM, $"the ring barely moves, {maxSeen} m at most");
    }

    [Theory]
    [InlineData("ring-a", "ring-b")]
    [InlineData("g1", "g2")]
    [InlineData("1", "2")]
    public void TwoRingsDoNotMoveInLockstep(string a, string b)
    {
        float apart = Times.Max(t => CourseMotion.Offset(RingA with { Id = a }, t).Origin
            .DistanceTo(CourseMotion.Offset(RingA with { Id = b }, t).Origin));

        Assert.True(apart > 0.25f * CourseMotion.BobM, $"rings {a} and {b} stay within {apart} m of each other");
    }
}
