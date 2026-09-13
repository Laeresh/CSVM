using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The crater's geometry law and the rule that bounds how many a mission can hold. Decode:
/// docs/org/craters.md. The mesh surgery itself needs a built world and is measured by the
/// <c>crater-carve</c> in-engine suite; what is asserted here is everything that is arithmetic.
/// </summary>
public class CraterShapeTests
{
    private static readonly Vector3 Ground = new(100f, 40f, -250f);

    [Fact]
    public void TheRimIsSevenVerticesAtRadiusTwenty()
    {
        var shape = CraterShape.At(Ground);
        Assert.Equal(7, shape.Rim.Count);
        Assert.Equal(20f, shape.Radius);
        foreach (var v in shape.Rim)
        {
            Assert.Equal(20f, new Vector2(v.X - Ground.X, v.Z - Ground.Z).Length(), 3);
            Assert.Equal(Ground.Y, v.Y, 3);
        }
    }

    [Fact]
    public void TheRimIsEvenlySpaced()
    {
        var rim = CraterShape.At(Ground).Rim;
        float first = Flat(rim[0]).DistanceTo(Flat(rim[1]));
        for (int i = 0; i < rim.Count; i++)
        {
            var a = Flat(rim[i]);
            var b = Flat(rim[(i + 1) % rim.Count]);
            Assert.Equal(first, a.DistanceTo(b), 3);
        }
    }

    [Fact]
    public void TheFloorIsSixUnderTheImpact()
    {
        var shape = CraterShape.At(Ground);
        Assert.Equal(3f, shape.Depth);
        Assert.Equal(Ground.Y - 3f, shape.BowlCentre.Y, 3);
        Assert.Equal(Ground.Y - 6f, shape.Floor.Y, 3);
        Assert.Equal(Ground.X, shape.Floor.X, 3);
        Assert.Equal(Ground.Z, shape.Floor.Z, 3);
    }

    [Fact]
    public void TheMidRingHangsHalfwayInAndOneDepthDown()
    {
        var shape = CraterShape.At(Ground);
        var mid = shape.MidRing(shape.Rim);
        Assert.Equal(shape.Rim.Count, mid.Length);
        for (int i = 0; i < mid.Length; i++)
        {
            Assert.Equal(10f, new Vector2(mid[i].X - Ground.X, mid[i].Z - Ground.Z).Length(), 3);
            // Halfway between the rim (at the impact) and the bowl centre (one depth down), then
            // one depth further: 40 - 1.5 - 3.
            Assert.Equal(Ground.Y - 4.5f, mid[i].Y, 3);
        }
    }

    // The decoration census the carve destroys: an origin's XZ distance alone, with no height test,
    // so a decoration standing on a rise inside the radius dies with the ones on the flat.
    [Fact]
    public void TheCensusIsTheXzDiscAndIgnoresHeight()
    {
        var shape = CraterShape.At(Ground);
        var standing = new[]
        {
            Ground,                                          // dead centre
            Ground + new Vector3(19.9f, 0f, 0f),             // just inside
            Ground + new Vector3(0f, 500f, 14f),             // inside, far above
            Ground + new Vector3(0f, -500f, -14f),           // inside, far below
            Ground + new Vector3(20.1f, 0f, 0f),             // just outside
            Ground + new Vector3(15f, 0f, 15f),              // outside on the diagonal
        };
        int census = 0;
        foreach (var at in standing)
        {
            if (shape.Covers(at))
            {
                census++;
            }
        }
        Assert.Equal(4, census);
    }

    [Fact]
    public void ACraterRefusesAnythingInsideItsClearance()
    {
        var carved = new List<CraterShape> { CraterShape.At(Ground) };
        Assert.True(CraterField.Refused(carved, CraterShape.At(Ground)));
        Assert.True(CraterField.Refused(carved, CraterShape.At(Ground + new Vector3(10f, 0f, 0f))));
        // The reach is one footprint's own width plus the clearance, because both boxes are the
        // same width: inside it the grown box is still met, outside it nothing is.
        float reach = carved[0].Footprint.Size.X + CraterShape.Clearance;
        Assert.True(CraterField.Refused(carved, CraterShape.At(Ground + new Vector3(reach - 1f, 0f, 0f))));
        Assert.False(CraterField.Refused(carved, CraterShape.At(Ground + new Vector3(reach + 1f, 0f, 0f))));
    }

    [Fact]
    public void TheRefusalIgnoresHeight()
    {
        var carved = new List<CraterShape> { CraterShape.At(Ground) };
        var above = CraterShape.At(Ground + new Vector3(0f, 900f, 0f));
        Assert.True(CraterField.Refused(carved, above));
    }

    // Permanence: nothing ages a crater out, so a spot bombed early in a mission still refuses a
    // second bomb after every other crater the mission carved.
    [Fact]
    public void AnEarlyCraterStillRefusesAfterAMissionOfOthers()
    {
        var carved = new List<CraterShape>();
        var first = CraterShape.At(Ground);
        carved.Add(first);
        for (int i = 1; i <= 40; i++)
        {
            var next = CraterShape.At(Ground + new Vector3(i * 60f, 0f, 0f));
            Assert.False(CraterField.Refused(carved, next));
            carved.Add(next);
        }
        Assert.Equal(41, carved.Count);
        Assert.Equal(first.Impact, carved[0].Impact);
        Assert.True(CraterField.Refused(carved, CraterShape.At(Ground)));
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
