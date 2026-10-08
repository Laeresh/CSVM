using CSVM.Effects;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The ship calm zone the wave ocean flattens its waves in (<see cref="OceanCalmZone"/>). It is a
/// box along the hull's heading, grown over the waterline and the wake sheets' footprints. The
/// shader's smoothstep fades the waves back in past it.
/// </summary>
public class OceanCalmZoneTests
{
    private const float Eps = 1e-3f;

    [Fact]
    public void AnEmptyZoneIsADiscAtItsOrigin()
    {
        var zone = new OceanCalmZone(new Vector2(100f, -40f), new Vector2(0f, 1f));
        Assert.True(zone.IsEmpty);
        Assert.Equal(Vector2.Zero, zone.HalfExtents);
        Assert.Equal(5f, zone.Distance(new Vector2(103f, -36f)), Eps);
    }

    [Fact]
    public void TheBoxRunsAlongTheHeading()
    {
        var zone = new OceanCalmZone(Vector2.Zero, new Vector2(0f, 3f));
        zone.Add(new Vector2(-10f, -100f));
        zone.Add(new Vector2(10f, 50f));
        Assert.Equal(new Vector2(0f, 1f), zone.Axis);
        Assert.Equal(75f, zone.HalfExtents.X, Eps);
        Assert.Equal(10f, zone.HalfExtents.Y, Eps);
        Assert.Equal(0f, zone.Center.X, Eps);
        Assert.Equal(-25f, zone.Center.Y, Eps);
        Assert.Equal(0f, zone.Distance(new Vector2(9f, -99f)), Eps);
        Assert.Equal(5f, zone.Distance(new Vector2(0f, 55f)), Eps);
        Assert.Equal(5f, zone.Distance(new Vector2(-15f, 0f)), Eps);
        // Past a corner the distance is to the corner, which rounds the zone's ends.
        Assert.Equal(5f, zone.Distance(new Vector2(13f, 54f)), Eps);
    }

    [Fact]
    public void AMeshFootprintTurnsWithItsShip()
    {
        // A sheet 10 m wide and 100 m long, on a ship turned 30 degrees and moved off the origin.
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(30f));
        var ship = new Transform3D(basis, new Vector3(-5000f, 0f, -10000f));
        var sheet = new Aabb(new Vector3(-5f, 0.1f, -50f), new Vector3(10f, 9f, 100f));
        var heading = new Vector2(basis.Z.X, basis.Z.Z);
        var zone = new OceanCalmZone(new Vector2(ship.Origin.X, ship.Origin.Z), heading);
        zone.Add(sheet, ship);
        Assert.Equal(50f, zone.HalfExtents.X, Eps);
        Assert.Equal(5f, zone.HalfExtents.Y, Eps);
        Assert.Equal(-5000f, zone.Center.X, 0.01f);
        Assert.Equal(-10000f, zone.Center.Y, 0.01f);
        var bow = new Vector2(ship.Origin.X, ship.Origin.Z) + (heading.Normalized() * 60f);
        Assert.Equal(10f, zone.Distance(bow), 0.01f);
    }

    [Fact]
    public void AWakeBehindTheHullStretchesTheZoneOverBoth()
    {
        // The hull's waterline from -60 to 60 m along its heading, a wake sheet trailing it to -200.
        var ship = new Transform3D(Basis.Identity, new Vector3(0f, 0f, 0f));
        var zone = new OceanCalmZone(Vector2.Zero, new Vector2(0f, 1f));
        zone.Add(new Aabb(new Vector3(-10f, -5f, -60f), new Vector3(20f, 10f, 120f)), ship);
        zone.Add(new Aabb(new Vector3(-25f, 0.2f, -200f), new Vector3(50f, 0.1f, 140f)), ship);
        Assert.Equal(130f, zone.HalfExtents.X, Eps);
        Assert.Equal(25f, zone.HalfExtents.Y, Eps);
        Assert.Equal(-70f, zone.Center.Y, Eps);
        Assert.Equal(0f, zone.Distance(new Vector2(24f, -199f)), Eps);
    }

    [Fact]
    public void AHeadingWithNoLengthFallsBackToX()
    {
        var zone = new OceanCalmZone(Vector2.Zero, Vector2.Zero);
        Assert.Equal(new Vector2(1f, 0f), zone.Axis);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(6f, 0f)]
    [InlineData(31f, 0.5f)]
    [InlineData(56f, 1f)]
    [InlineData(500f, 1f)]
    [InlineData(18.5f, 0.15625f)]
    public void TheFadeIsTheShadersSmoothstep(float distance, float expected) =>
        Assert.Equal(expected, OceanCalmZone.Calm(distance, 6f, 50f), Eps);
}
