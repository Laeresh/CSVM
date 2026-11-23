using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Session.Career;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The mission generator over a hand-built catalogue. One contract yields one spec, and
/// the opposition stays inside its budget apart from the one plane a first wave always gets. A
/// patrol flies its route, and a strike places its targets and flak near one another. An empty
/// catalogue yields no spec.</summary>
public class MissionGeneratorTests
{
    [Fact]
    public void OneContract_YieldsOneSpec()
    {
        foreach (var job in new[] { JobType.Patrol, JobType.Strike })
        {
            var a = MissionGenerator.Generate(Contract(job, 3, 42), Catalogue(), 1)!;
            var b = MissionGenerator.Generate(Contract(job, 3, 42), Catalogue(), 1)!;
            Assert.Equal(a.Enemies, b.Enemies);
            Assert.Equal(a.Targets, b.Targets);
            Assert.Equal(a.PlayerStart, b.PlayerStart);
        }
    }

    [Theory]
    [InlineData(JobType.Patrol)]
    [InlineData(JobType.Strike)]
    public void Opposition_StaysInsideTheBudget(JobType job)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            for (int threat = 1; threat <= 5; threat++)
            {
                var spec = MissionGenerator.Generate(Contract(job, threat, seed), Catalogue(), 1)!;
                var initial = spec.Enemies.Where(e => e.Wave == EnemyWave.Initial).ToList();
                Assert.NotEmpty(initial);

                // Only the first wave's guaranteed plane may overrun the budget.
                int overrun = spec.Spent - spec.Budget;
                Assert.True(overrun <= 0 || initial.Count == 1, $"seed {seed} threat {threat} overran by {overrun}");
            }
        }
    }

    [Fact]
    public void HigherThreat_FieldsMoreOpposition()
    {
        double Mean(int threat) => Enumerable.Range(1, 100)
            .Average(s => MissionGenerator.Generate(Contract(JobType.Patrol, threat, (ulong)s), Catalogue(), 0)!.Enemies.Count);

        Assert.True(Mean(5) > Mean(1) * 2);
    }

    [Fact]
    public void Patrol_FliesItsRouteWithAPrimaryOnTheFirstWave()
    {
        var spec = MissionGenerator.Generate(Contract(JobType.Patrol, 4, 7), Catalogue(), 1)!;
        var route = Catalogue().Routes.Single(r => r.Net == spec.Route);

        Assert.All(spec.Enemies.Where(e => e.Wave == EnemyWave.Initial), e =>
        {
            Assert.Equal(spec.Route, e.Route);
            Assert.Contains(route.Points, p => p.X == e.Position.X && p.Z == e.Position.Z);
        });
        Assert.Equal(new MissionObjective(ObjectiveKind.DestroyWave, true, EnemyWave.Initial), spec.Objectives[0]);
        Assert.Equal(ReserveTrigger.TimeElapsed, spec.ReserveTrigger);
        Assert.Empty(spec.Targets);
    }

    [Fact]
    public void Strike_PlacesTargetsAndFlakNearOneAnother()
    {
        var tunes = CareerTunes.Default;
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var spec = MissionGenerator.Generate(Contract(JobType.Strike, 1, seed), Catalogue(), 1)!;
            var targets = spec.Targets.Where(t => t.Prefab != MissionGenerator.FlakPrefab).ToList();
            var flak = spec.Targets.Where(t => t.Prefab == MissionGenerator.FlakPrefab).ToList();

            Assert.InRange(targets.Count, tunes.MinStrikeTargets, tunes.MaxStrikeTargets);
            // One skull buys 30 % of $10,000 of flak at $1,500 a battery.
            Assert.Equal(2, flak.Count);
            Assert.All(spec.Targets, t => Assert.True(MathF.Sqrt(Flat(t.X, t.Z, spec.Centre)) < (2 * tunes.StrikeRadius) + 200f));
            Assert.Equal(ObjectiveKind.DestroyTargets, spec.Objectives[0].Kind);
            Assert.True(spec.Objectives[0].Primary);
            Assert.Equal(ReserveTrigger.FirstTargetDestroyed, spec.ReserveTrigger);
        }
    }

    [Fact]
    public void Strike_FlakIsCappedAtFourBatteries()
    {
        var spec = MissionGenerator.Generate(Contract(JobType.Strike, 5, 9), Catalogue(), 1)!;
        Assert.Equal(4, spec.Targets.Count(t => t.Prefab == MissionGenerator.FlakPrefab));
    }

    [Fact]
    public void TheFlightStartsOutAndFacesTheCentre()
    {
        var spec = MissionGenerator.Generate(Contract(JobType.Patrol, 2, 11), Catalogue(), 1)!;

        Assert.Equal(CareerTunes.Default.StartDistance, MathF.Sqrt(Flat(spec.PlayerStart.X, spec.PlayerStart.Z, spec.Centre)), 1);
        var forward = new SpawnPoint(spec.PlayerStart, spec.PlayerHeadingDeg).Forward;
        var toCentre = new Vector3(spec.Centre.X - spec.PlayerStart.X, 0, spec.Centre.Z - spec.PlayerStart.Z).Normalized();
        Assert.True(forward.Dot(toCentre) > 0.999f);
    }

    [Fact]
    public void WingmanSeats_AreCappedAtThree()
    {
        Assert.Equal(3, MissionGenerator.Generate(Contract(JobType.Patrol, 1, 1), Catalogue(), 5)!.WingmanSeats);
        Assert.Equal(0, MissionGenerator.Generate(Contract(JobType.Patrol, 1, 1), Catalogue(), -1)!.WingmanSeats);
    }

    [Fact]
    public void AnEmptyCatalogue_YieldsNoSpec()
    {
        var empty = SiteCatalogue.Build(new AiNet[0], new SpawnPoint[0], new ZeppelinSite[0]);
        Assert.Null(MissionGenerator.Generate(Contract(JobType.Patrol, 1, 1), empty, 1));
        Assert.Null(MissionGenerator.Generate(Contract(JobType.Strike, 1, 1), empty, 1));
    }

    [Fact]
    public void PlaneValue_IsTheDecodedHangarPrice()
    {
        // Warhawk: $2,423 airframe, $2,550 middle engine, 120 stock armour units at $4.
        Assert.Equal(2423 + 2550 + 480, MissionGenerator.PlaneValue(10));
    }

    private static Contract Contract(JobType job, int threat, ulong seed) =>
        new(1, job, EmployerKind.Militia, threat, 1000, 0, 5, seed);

    // Three fixed nets on a 500 m grid at 400 m altitude, so a strike finds a cluster anywhere.
    private static SiteCatalogue Catalogue()
    {
        var nets = new List<AiNet>();
        for (int n = 0; n < 3; n++)
        {
            var nodes = Enumerable.Range(0, 6)
                .Select(i => new AiNetNode(new Vector3((n * 3000f) + (i * 500f), 400f, -i * 500f), new float[0]))
                .ToList();
            nets.Add(new AiNet
            {
                Id = n + 1,
                Name = $"Route{n}",
                Nodes = nodes,
                Edges = Enumerable.Range(0, 5).Select(i => (i, i + 1)).ToList(),
            });
        }

        return SiteCatalogue.Build(nets, new SpawnPoint[0], new ZeppelinSite[0]);
    }

    private static float Flat(float x, float z, Vector3 c) => ((x - c.X) * (x - c.X)) + ((z - c.Z) * (z - c.Z));
}
