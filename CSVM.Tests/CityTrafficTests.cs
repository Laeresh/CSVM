using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The paths <see cref="CityTraffic"/>'s cars drive. Every path closes on itself, so its car stream does too. A block
/// loop keeps right-hand traffic: a right loop the kerb lanes inside its block, a left loop the inner lanes outside.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CityTrafficTests
{
    /// <summary>A U-turn loop runs out along its lane, back along its twin and ends where it began.</summary>
    [Fact]
    public void UTurnLoopCloses()
    {
        var run = Line(new Vector2(100f, 50f), new Vector2(0.6f, 0.8f), 400f);
        float off = run.Lanes()[1];
        var path = CityTraffic.UTurnLoop(run, off);
        AssertClosed(path);
        Assert.Equal((2f * (400f - (2f * off))) + (2f * MathF.PI * off), path.Length, 2);
        // Out on the right of the centreline, -Normal.
        Assert.True((path.Segs[0].Start - run.Start).Dot(run.Normal) < 0f);
    }

    /// <summary>One block between two streets each way takes one loop of each kind. The right loop drives inside the
    /// centrelines, block on its right. The left loop drives outside them, block on its left.</summary>
    [Fact]
    public void BlockLoopsKeepTheirSides()
    {
        var runs = new List<CityTraffic.Run>
        {
            Line(new Vector2(128f, -100f), new Vector2(0f, 1f), 600f), Line(new Vector2(384f, -100f), new Vector2(0f, 1f), 600f),
            Line(new Vector2(600f, 128f), new Vector2(-1f, 0f), 700f), Line(new Vector2(600f, 384f), new Vector2(-1f, 0f), 700f),
        };
        var grid = CityTraffic.Grid(runs);
        var centre = new Vector2(256f, 256f);
        var driven = new Dictionary<(CityTraffic.Run Run, int Lane), List<(float From, float To)>>();
        foreach (bool right in new[] { true, false })
        {
            var path = Assert.Single(CityTraffic.BlockLoops(grid, right, driven));
            AssertClosed(path);
            // Each side's run records the stretch of its lane the loop drives, corners included.
            foreach (var run in runs)
            {
                var (from, to) = Assert.Single(driven[(run, right ? 2 : 0)]);
                float a = (new Vector2(128f, 128f) - run.Start).Dot(run.Dir), b = (new Vector2(384f, 384f) - run.Start).Dot(run.Dir);
                Assert.InRange(MathF.Min(a, b) - from, -20f, 20f);
                Assert.InRange(to - MathF.Max(a, b), -20f, 20f);
            }
            float lane = right ? runs[0].Lanes()[^1] : runs[0].Lanes()[0];
            float edge = right ? 128f - lane : 128f + lane;
            foreach (var seg in path.Segs.Where(s => s.Radius == 0f))
            {
                // The block's side of a straight: + its right, - its left.
                float side = (centre - seg.Start).Dot(new Vector2(-seg.Dir.Y, seg.Dir.X));
                Assert.True(right ? side > 0f : side < 0f);
                Assert.Equal(edge, MathF.Abs((seg.Start - centre).Dot(new Vector2(-seg.Dir.Y, seg.Dir.X))), 2);
            }
        }
    }

    /// <summary>No piece of a path outruns its car period, so a car never blinks out before the next piece takes it.
    /// The period divides the path's length.</summary>
    [Fact]
    public void NoPieceOutrunsItsPeriod()
    {
        foreach (float length in new[] { 300f, 400f, 2000f })
        {
            var path = CityTraffic.UTurnLoop(Line(Vector2.Zero, new Vector2(1f, 0f), length), 13f);
            float period = CityTraffic.Period(path.Length);
            Assert.All(path.Segs, s => Assert.True(s.Length <= period + 0.001f, $"a {s.Length} m piece outruns a {period} m period"));
            float laps = path.Length / period;
            Assert.Equal(MathF.Round(laps), laps, 3);
        }
    }

    /// <summary>The undriven stretches are the gaps between driven ones, overlaps merged and short gaps dropped.</summary>
    [Fact]
    public void UndrivenLeavesTheGaps()
    {
        var gaps = CityTraffic.Undriven(1000f, new List<(float, float)> { (300f, 500f), (100f, 350f), (520f, 560f), (900f, 1100f) }, 48f);
        Assert.Equal(new List<(float, float)> { (0f, 100f), (560f, 900f) }, gaps);
        Assert.Equal(new List<(float, float)> { (0f, 1000f) }, CityTraffic.Undriven(1000f, null, 48f));
    }

    /// <summary>The shader's unpacking of a car's speed and phase returns them to its quantum.</summary>
    [Fact]
    public void PackedSpeedAndPhaseRoundTrip()
    {
        foreach (var (speed, phase) in new[] { (8f, 0f), (11.37f, 123.456f), (15f, 383.99f) })
        {
            float packed = CityTraffic.Pack(speed, phase);
            Assert.Equal(speed, MathF.Floor(packed / 8192f) / 32f, 1f / 64f);
            Assert.Equal(phase, (packed % 8192f) / 16f, 1f / 32f);
        }
    }

    // Each piece starts where the one before it ended, the last piece ending at the first's start and heading.
    internal static void AssertClosed(CityTraffic.Path path)
    {
        Assert.Equal(path.Segs.Sum(s => s.Length), path.Length, 2);
        for (int i = 0; i < path.Segs.Count; i++)
        {
            var (end, dir) = path.Segs[i].Along(path.Segs[i].Length);
            var next = path.Segs[(i + 1) % path.Segs.Count];
            Assert.True((end - next.Start).Length() < 0.01f, $"piece {i} ends {end}, the next starts {next.Start}");
            Assert.True((dir - next.Dir).Length() < 0.001f, $"piece {i} ends heading {dir}, the next {next.Dir}");
        }
    }

    private static CityTraffic.Run Line(Vector2 start, Vector2 dir, float length) =>
        new(start, dir, new Vector2(dir.Y, -dir.X), length, 16.5f, new float[(int)(length / 4f) + 1]);
}
