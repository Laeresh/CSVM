using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Session.Career;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The placement rules over a hand-made ground. A target stands on flat land at its
/// requested point, or moves to the nearest spot that fits. It is dropped when nothing within
/// reach does. Its height is the lowest ground under its footprint, so no corner floats.</summary>
public class GroundPlacerTests
{
    [Fact]
    public void FlatLand_TakesTheTargetWhereItWasAsked()
    {
        var result = GroundPlacer.Place(Spec(("fuel_depot", 500f, 500f)), new Ground((x, z) => 12f));

        var placed = Assert.Single(result.Placed);
        Assert.Equal(new Vector3(500f, 12f, 500f), placed.Position);
        Assert.Empty(result.Dropped);
        Assert.False(result.NoTargets);
    }

    [Fact]
    public void Water_MovesTheTargetToTheNearestLand()
    {
        // Sea west of x = 600; the depot asked for at x = 500 needs its 40 m footprint on land.
        var ground = new Ground((x, z) => 5f) { Water = (x, z) => x < 600f };
        var placed = Assert.Single(GroundPlacer.Place(Spec(("fuel_depot", 500f, 0f)), ground).Placed);

        Assert.True(placed.Position.X - 40f >= 600f);
        Assert.True(placed.Position.X <= 500f + CareerTunes.Default.MaxPlacementShift);
    }

    [Fact]
    public void NothingWithinReach_DropsTheTarget()
    {
        var ground = new Ground((x, z) => 0f) { Water = (x, z) => true };
        var result = GroundPlacer.Place(Spec(("fuel_depot", 0f, 0f), ("flak_battery", 0f, 0f)), ground);

        Assert.Empty(result.Placed);
        Assert.Equal(2, result.Dropped.Count);
        Assert.True(result.NoTargets);
    }

    [Fact]
    public void OffTheWorld_DropsTheTarget()
    {
        var ground = new Ground((x, z) => 0f) { Edge = 100f };
        Assert.Single(GroundPlacer.Place(Spec(("supply_dump", 10_000f, 0f)), ground).Dropped);
    }

    [Theory]
    [InlineData(0.5f, false)]
    [InlineData(0.1f, true)]
    public void TheSlopeLimit_RefusesSteepGround(float gradient, bool fits)
    {
        // Gradient 0.5 is about 27 degrees, 0.1 about 6; the limit is 8.
        var ground = new Ground((x, z) => x * gradient);
        var spot = GroundPlacer.Fits(1000f, 0f, 30f, ground, CareerTunes.Default);

        Assert.Equal(fits, spot != null);
        if (fits)
        {
            // The lowest sample is the footprint's west edge.
            Assert.Equal((1000f - 30f) * gradient, spot!.Value.Y, 3);
        }
    }

    [Fact]
    public void AnOriginalNode_PushesTheTargetAside()
    {
        var ground = new Ground((x, z) => 0f) { Nodes = { (0f, 0f, 60f) } };
        var placed = Assert.Single(GroundPlacer.Place(Spec(("supply_dump", 0f, 0f)), ground).Placed);

        Assert.True(Distance(placed.Position, 0f, 0f) >= 60f + 30f);
    }

    [Fact]
    public void TwoTargetsOnOnePoint_StandApart()
    {
        var result = GroundPlacer.Place(Spec(("fuel_depot", 0f, 0f), ("flak_battery", 0f, 0f)), new Ground((x, z) => 0f));

        Assert.Equal(2, result.Placed.Count);
        Assert.True(Distance(result.Placed[1].Position, result.Placed[0].Position.X, result.Placed[0].Position.Z) >= 40f + 15f);
    }

    [Fact]
    public void TargetsArePlacedBeforeFlak()
    {
        var result = GroundPlacer.Place(Spec(("flak_battery", 0f, 0f), ("fuel_depot", 0f, 0f)), new Ground((x, z) => 0f));

        // The depot keeps the requested point; the battery moves off it.
        Assert.Equal("fuel_depot", result.Placed[0].Prefab);
        Assert.Equal(Vector3.Zero, result.Placed[0].Position);
    }

    [Fact]
    public void Yaw_IsPureInTheSpecSeed()
    {
        var ground = new Ground((x, z) => 0f);
        var a = GroundPlacer.Place(Spec(("fuel_depot", 0f, 0f)), ground).Placed[0];
        var b = GroundPlacer.Place(Spec(("fuel_depot", 0f, 0f)), ground).Placed[0];
        Assert.Equal(a.YawDeg, b.YawDeg);
        Assert.InRange(a.YawDeg, 0f, 360f);
    }

    private static float Distance(Vector3 p, float x, float z) => MathF.Sqrt(((p.X - x) * (p.X - x)) + ((p.Z - z) * (p.Z - z)));

    private static MissionSpec Spec(params (string Prefab, float X, float Z)[] targets) => new()
    {
        Job = JobType.Strike,
        Threat = 1,
        Seed = 77,
        Centre = Vector3.Zero,
        PlayerStart = Vector3.Zero,
        PlayerHeadingDeg = 0f,
        WingmanSeats = 0,
        Enemies = new List<EnemyFlight>(),
        Targets = targets.Select(t => new TargetPlacement(t.Prefab, t.X, t.Z, 0)).ToList(),
        Objectives = new List<MissionObjective>(),
        ReserveTrigger = ReserveTrigger.FirstTargetDestroyed,
        ReserveDelaySeconds = 0,
        Budget = 0,
        Spent = 0,
    };

    private sealed class Ground : IGroundProbe
    {
        private readonly Func<float, float, float> _height;

        public Ground(Func<float, float, float> height)
        {
            _height = height;
        }

        public Func<float, float, bool> Water { get; init; } = (x, z) => false;

        public float Edge { get; init; } = float.MaxValue;

        public List<(float X, float Z, float Radius)> Nodes { get; } = new();

        public GroundSample? Sample(float x, float z) =>
            MathF.Abs(x) > Edge || MathF.Abs(z) > Edge ? null : new GroundSample(_height(x, z), Water(x, z));

        public bool Occupied(float x, float z, float radius) =>
            Nodes.Any(n => MathF.Sqrt(((n.X - x) * (n.X - x)) + ((n.Z - z) * (n.Z - z))) < n.Radius + radius);
    }
}
